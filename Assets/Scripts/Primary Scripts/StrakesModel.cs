using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// StrakesModel places and visualizes highly optimized strake points and longitudinal guide lines 
/// separated into Lower-Lower Mould strakes (u in [0, 0.25]) and Upper-Lower Mould strakes (u in [0.25, 0.50]),
/// divided by the Connection Points along the lower frame moulds up to the Keel Bow.
/// </summary>
[ExecuteInEditMode]
public class StrakesModel : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Reference to the SheerPlan to get Bow Rising End and Keel endpoints.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the BodyPlan to get Master Frame lower mould settings.")]
    public BodyPlan bodyPlan;

    [Tooltip("Reference to the BreadthPlan to get half-breadth curves.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the FramingModel to evaluate 3D frame sections.")]
    public FramingModel framingModel;

    [Header("Strake Settings")]
    [Tooltip("Show Strake Points and Guide Lines in Scene view gizmos.")]
    public bool showStrakes = true;

    [Tooltip("Number of strake points along the Lower-Lower Mould segment (from Keel pL0 to Connection Point pLR).")]
    [Range(2, 20)]
    public int strakesPerLowerLowerMould = 5;

    [Tooltip("Number of strake points along the Upper-Lower Mould segment (from Connection Point pLR to pL3).")]
    [Range(2, 20)]
    public int strakesPerUpperLowerMould = 5;

    [Tooltip("Size of the strake point node spheres in Scene view.")]
    [Range(0.01f, 0.3f)]
    public float pointRadius = 0.05f;

    [Tooltip("Connect strake points across adjacent frame stations to form longitudinal strake guide lines.")]
    public bool showStrakeLines = true;

    [Header("Gizmo Settings - Lower Lower Mould")]
    [Tooltip("Color of Lower-Lower Mould strake point spheres.")]
    public Color lowerLowerPointColor = new Color(0.1f, 0.9f, 0.4f, 1f);

    [Tooltip("Color of Lower-Lower Mould longitudinal guide lines.")]
    public Color lowerLowerLineColor = new Color(0.2f, 1f, 0.7f, 0.8f);

    [Header("Gizmo Settings - Upper Lower Mould")]
    [Tooltip("Color of Upper-Lower Mould strake point spheres.")]
    public Color upperLowerPointColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("Color of Upper-Lower Mould longitudinal guide lines.")]
    public Color upperLowerLineColor = new Color(1f, 0.92f, 0.4f, 0.8f);

    [Header("Gizmo Settings - Connection Point Boundary")]
    [Tooltip("Highlight the Connection Points (pLR) dividing the lower-lower and upper-lower mould strakes.")]
    public bool highlightConnectionPoints = true;

    [Tooltip("Color of the Connection Point boundary nodes.")]
    public Color connectionPointColor = new Color(1f, 0.2f, 0.8f, 1f);

    [Tooltip("Show strake index labels in Scene view.")]
    public bool showLabels = false;

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        FindReferences();
    }

    public void FindReferences()
    {
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
        if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();
    }

    /// <summary>
    /// Evaluates list of 3D points equally spaced by arc length along a sub-segment u in [minU, maxU] 
    /// using pre-fetched control points. High-performance stack math with zero component queries.
    /// </summary>
    public List<Vector3> GetSegmentStrakePointsFromControlPoints(
        Vector3 pL0, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3, Vector3 pU0, Vector3 pU1, Vector3 pU2, Vector3 pU3,
        float minU, float maxU, int count)
    {
        List<Vector3> points = new List<Vector3>(count);
        if (framingModel == null) return points;

        count = Mathf.Max(2, count);
        const int samples = 20;

        float[] uTable = new float[samples + 1];
        Vector3[] ptTable = new Vector3[samples + 1];
        float[] distTable = new float[samples + 1];

        distTable[0] = 0f;
        uTable[0] = minU;
        ptTable[0] = framingModel.EvaluateFramePointFromControlPoints(minU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);

        float totalLength = 0f;
        for (int i = 1; i <= samples; i++)
        {
            float u = Mathf.Lerp(minU, maxU, (float)i / samples);
            uTable[i] = u;
            ptTable[i] = framingModel.EvaluateFramePointFromControlPoints(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
            float stepDist = Vector3.Distance(ptTable[i - 1], ptTable[i]);
            totalLength += stepDist;
            distTable[i] = totalLength;
        }

        if (totalLength < 0.0001f)
        {
            for (int k = 0; k < count; k++) points.Add(ptTable[0]);
            return points;
        }

        for (int k = 0; k < count; k++)
        {
            float targetDist = ((float)k / (count - 1)) * totalLength;

            int idx = 0;
            while (idx < samples && distTable[idx + 1] < targetDist)
            {
                idx++;
            }

            if (idx >= samples)
            {
                points.Add(ptTable[samples]);
            }
            else
            {
                float segmentLength = distTable[idx + 1] - distTable[idx];
                float t = segmentLength > 0.00001f ? (targetDist - distTable[idx]) / segmentLength : 0f;
                float solvedU = Mathf.Lerp(uTable[idx], uTable[idx + 1], t);
                Vector3 pt = framingModel.EvaluateFramePointFromControlPoints(solvedU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                points.Add(pt);
            }
        }

        return points;
    }

    /// <summary>
    /// Gets the starting Z coordinate of the Deadwood (aft boundary for strakes).
    /// </summary>
    public float GetDeadwoodStartZ()
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        return sheerPlan.showDeadwoodRake ? sheerPlan.cachedDeadwoodForePoint.z : sheerPlan.cachedSternRisingEnd.z;
    }

    /// <summary>
    /// Gets strake points along the Lower-Lower Mould segment (Keel pL0 to Connection Point pLR).
    /// </summary>
    public List<Vector3> GetLowerLowerMouldStrakePoints(float z, float side, int count)
    {
        if (framingModel == null || sheerPlan == null) return new List<Vector3>();
        sheerPlan.UpdateCachedValues();
        float keelBowZ = sheerPlan.cachedKeelBowPoint.z;
        float deadwoodStartZ = GetDeadwoodStartZ();
        if (z < deadwoodStartZ - 0.001f || z > keelBowZ + 0.001f) return new List<Vector3>();

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!framingModel.GetFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return new List<Vector3>();

        return GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0f, 0.25f, count);
    }

    /// <summary>
    /// Gets strake points along the Upper-Lower Mould segment (Connection Point pLR to pL3).
    /// </summary>
    public List<Vector3> GetUpperLowerMouldStrakePoints(float z, float side, int count)
    {
        if (framingModel == null || sheerPlan == null) return new List<Vector3>();
        sheerPlan.UpdateCachedValues();
        float keelBowZ = sheerPlan.cachedKeelBowPoint.z;
        float deadwoodStartZ = GetDeadwoodStartZ();
        if (z < deadwoodStartZ - 0.001f || z > keelBowZ + 0.001f) return new List<Vector3>();

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!framingModel.GetFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return new List<Vector3>();

        return GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0.25f, 0.50f, count);
    }

    /// <summary>
    /// Gets list of frame station Z coordinates between the start of the Deadwood (aft) and the Keel Bow point (fwd).
    /// </summary>
    public List<float> GetValidFrameStations()
    {
        List<float> stations = new List<float>();
        if (framingModel == null || sheerPlan == null) return stations;

        sheerPlan.UpdateCachedValues();
        float keelBowZ = sheerPlan.cachedKeelBowPoint.z;
        float deadwoodStartZ = GetDeadwoodStartZ();

        List<float> allStations = framingModel.CalculateFrameStations();
        foreach (float z in allStations)
        {
            if (z >= deadwoodStartZ - 0.001f && z <= keelBowZ + 0.001f)
            {
                stations.Add(z);
            }
        }

        return stations;
    }

    private void OnDrawGizmos()
    {
        if (!showStrakes) return;
        FindReferences();
        if (framingModel == null || sheerPlan == null) return;

        List<float> stations = GetValidFrameStations();
        if (stations.Count == 0) return;

        int llCount = Mathf.Max(2, strakesPerLowerLowerMould);
        int ulCount = Mathf.Max(2, strakesPerUpperLowerMould);

        // Store points for longitudinal line connections
        Dictionary<int, List<Vector3>> portLLPoints = new Dictionary<int, List<Vector3>>();
        Dictionary<int, List<Vector3>> stbdLLPoints = new Dictionary<int, List<Vector3>>();
        Dictionary<int, List<Vector3>> portULPoints = new Dictionary<int, List<Vector3>>();
        Dictionary<int, List<Vector3>> stbdULPoints = new Dictionary<int, List<Vector3>>();

        for (int s = 0; s < stations.Count; s++)
        {
            float z = stations[s];

            // Port Side (-1f) - Fetch control points ONCE per station
            Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
            if (framingModel.GetFrameControlPointsAtZ(z, -1f, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            {
                List<Vector3> portLL = GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0f, 0.25f, llCount);
                List<Vector3> portUL = GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0.25f, 0.50f, ulCount);

                portLLPoints[s] = portLL;
                portULPoints[s] = portUL;

                Gizmos.color = lowerLowerPointColor;
                foreach (Vector3 pt in portLL) Gizmos.DrawSphere(pt, pointRadius);

                Gizmos.color = upperLowerPointColor;
                foreach (Vector3 pt in portUL) Gizmos.DrawSphere(pt, pointRadius);

                if (highlightConnectionPoints && portLL.Count > 0)
                {
                    Gizmos.color = connectionPointColor;
                    Gizmos.DrawSphere(portLL[portLL.Count - 1], pointRadius * 1.3f);
                }
            }

            // Starboard Side (+1f) - Fetch control points ONCE per station
            if (framingModel.GetFrameControlPointsAtZ(z, 1f, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            {
                List<Vector3> stbdLL = GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0f, 0.25f, llCount);
                List<Vector3> stbdUL = GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0.25f, 0.50f, ulCount);

                stbdLLPoints[s] = stbdLL;
                stbdULPoints[s] = stbdUL;

                Gizmos.color = lowerLowerPointColor;
                foreach (Vector3 pt in stbdLL) Gizmos.DrawSphere(pt, pointRadius);

                Gizmos.color = upperLowerPointColor;
                foreach (Vector3 pt in stbdUL) Gizmos.DrawSphere(pt, pointRadius);

                if (highlightConnectionPoints && stbdLL.Count > 0)
                {
                    Gizmos.color = connectionPointColor;
                    Gizmos.DrawSphere(stbdLL[stbdLL.Count - 1], pointRadius * 1.3f);
                }
            }
        }

        // Draw Longitudinal Guide Lines
        if (showStrakeLines && stations.Count > 1)
        {
            // Lower-Lower Mould Lines
            Gizmos.color = lowerLowerLineColor;
            for (int k = 0; k < llCount; k++)
            {
                for (int s = 0; s < stations.Count - 1; s++)
                {
                    if (portLLPoints.ContainsKey(s) && portLLPoints.ContainsKey(s + 1) &&
                        portLLPoints[s].Count > k && portLLPoints[s + 1].Count > k)
                    {
                        Gizmos.DrawLine(portLLPoints[s][k], portLLPoints[s + 1][k]);
                    }

                    if (stbdLLPoints.ContainsKey(s) && stbdLLPoints.ContainsKey(s + 1) &&
                        stbdLLPoints[s].Count > k && stbdLLPoints[s + 1].Count > k)
                    {
                        Gizmos.DrawLine(stbdLLPoints[s][k], stbdLLPoints[s + 1][k]);
                    }
                }
            }

            // Upper-Lower Mould Lines
            Gizmos.color = upperLowerLineColor;
            for (int k = 0; k < ulCount; k++)
            {
                for (int s = 0; s < stations.Count - 1; s++)
                {
                    if (portULPoints.ContainsKey(s) && portULPoints.ContainsKey(s + 1) &&
                        portULPoints[s].Count > k && portULPoints[s + 1].Count > k)
                    {
                        Gizmos.DrawLine(portULPoints[s][k], portULPoints[s + 1][k]);
                    }

                    if (stbdULPoints.ContainsKey(s) && stbdULPoints.ContainsKey(s + 1) &&
                        stbdULPoints[s].Count > k && stbdULPoints[s + 1].Count > k)
                    {
                        Gizmos.DrawLine(stbdULPoints[s][k], stbdULPoints[s + 1][k]);
                    }
                }
            }
        }
    }
}
