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

    [Header("Strake Smoothing & Longitudinal Fairing Settings")]
    [Tooltip("Enable Y-Profile Longitudinal Fairing to smooth the side-view Y-height profile of strakes across stations Z.")]
    public bool enableLongitudinalFairing = true;

    [Range(0f, 1f)]
    [Tooltip("Strength blend for Y-Profile Fairing (0 = raw station math, 1 = fully faired Y profile).")]
    public float fairingStrength = 0.75f;

    [Range(1, 5)]
    [Tooltip("Number of longitudinal smoothing passes.")]
    public int fairingPasses = 2;

    [Tooltip("Enable graduated non-linear tapering from Keel to HOB (narrowing near HOB, widening near Keel).")]
    public bool enableStrakeSmoothing = true;

    [Range(0.3f, 1.0f)]
    [Tooltip("Minimum width ratio limit for strakes near HOB relative to midship width (e.g. 0.5 = 1/2 min width).")]
    public float minHobTaperRatio = 0.5f;

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
    /// using pre-fetched control points. Includes Pseudo Frame vertical segment when present (pL0.y > keelY).
    /// </summary>
    public List<Vector3> GetSegmentStrakePointsFromControlPoints(
        Vector3 pL0, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3, Vector3 pU0, Vector3 pU1, Vector3 pU2, Vector3 pU3,
        float minU, float maxU, int count, float keelY = float.NaN)
    {
        List<Vector3> points = new List<Vector3>(count);
        if (framingModel == null) return points;

        count = Mathf.Max(2, count);
        bool hasPseudoFrame = (Mathf.Approximately(minU, 0f) && !float.IsNaN(keelY) && pL0.y > keelY + 0.001f);

        const int curveSamples = 20;
        const int pseudoSamples = 10;
        int totalSamples = hasPseudoFrame ? (pseudoSamples + curveSamples) : curveSamples;

        Vector3[] ptTable = new Vector3[totalSamples + 1];
        float[] distTable = new float[totalSamples + 1];

        distTable[0] = 0f;
        float totalLength = 0f;

        if (hasPseudoFrame)
        {
            Vector3 keelBaselinePt = new Vector3(pL0.x, keelY, pL0.z);
            ptTable[0] = keelBaselinePt;

            // 1. Sample vertical Pseudo Frame segment (keelBaselinePt -> pL0)
            for (int i = 1; i <= pseudoSamples; i++)
            {
                float t = i / (float)pseudoSamples;
                ptTable[i] = Vector3.Lerp(keelBaselinePt, pL0, t);
                float stepDist = Vector3.Distance(ptTable[i - 1], ptTable[i]);
                totalLength += stepDist;
                distTable[i] = totalLength;
            }

            // 2. Sample Lower Mould Bezier curve (pL0 -> pLR)
            for (int i = 1; i <= curveSamples; i++)
            {
                float u = Mathf.Lerp(minU, maxU, i / (float)curveSamples);
                int tableIdx = pseudoSamples + i;
                ptTable[tableIdx] = framingModel.EvaluateFramePointFromControlPoints(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                float stepDist = Vector3.Distance(ptTable[tableIdx - 1], ptTable[tableIdx]);
                totalLength += stepDist;
                distTable[tableIdx] = totalLength;
            }
        }
        else
        {
            ptTable[0] = framingModel.EvaluateFramePointFromControlPoints(minU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);

            for (int i = 1; i <= curveSamples; i++)
            {
                float u = Mathf.Lerp(minU, maxU, i / (float)curveSamples);
                ptTable[i] = framingModel.EvaluateFramePointFromControlPoints(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                float stepDist = Vector3.Distance(ptTable[i - 1], ptTable[i]);
                totalLength += stepDist;
                distTable[i] = totalLength;
            }
        }

        if (totalLength < 0.0001f)
        {
            for (int k = 0; k < count; k++) points.Add(ptTable[0]);
            return points;
        }

        float midshipLength = 0f;
        if (enableStrakeSmoothing && framingModel != null)
        {
            Vector3 mL0, mL1, mLR, mL2, mL3, mU0, mU1, mU2, mU3;
            if (framingModel.GetFrameControlPointsAtZ(0f, 1f, out mL0, out mL1, out mLR, out mL2, out mL3, out mU0, out mU1, out mU2, out mU3))
            {
                midshipLength = Vector3.Distance(mL0, mLR) + Vector3.Distance(mLR, mL3);
            }
        }

        float girthRatio = (enableStrakeSmoothing && midshipLength > 0.001f) ? Mathf.Clamp01(totalLength / midshipLength) : 1.0f;
        float power = Mathf.Lerp(1.0f / Mathf.Clamp(minHobTaperRatio, 0.3f, 1.0f), 1.0f, girthRatio);

        for (int k = 0; k < count; k++)
        {
            float linearT = (float)k / (count - 1);
            float tBiased = enableStrakeSmoothing ? Mathf.Pow(linearT, power) : linearT;
            float targetDist = tBiased * totalLength;

            int idx = 0;
            while (idx < totalSamples && distTable[idx + 1] < targetDist)
            {
                idx++;
            }

            if (idx >= totalSamples)
            {
                points.Add(ptTable[totalSamples]);
            }
            else
            {
                float segmentLength = distTable[idx + 1] - distTable[idx];
                float t = segmentLength > 0.00001f ? (targetDist - distTable[idx]) / segmentLength : 0f;
                Vector3 pt = Vector3.Lerp(ptTable[idx], ptTable[idx + 1], t);
                points.Add(pt);
            }
        }

        return points;
    }

    /// <summary>
    /// Applies Y-Profile Surface-Constrained Longitudinal Fairing across stations Z.
    /// Smooths Y-height trajectories while projecting points back onto transverse frame surfaces.
    /// </summary>
    private void ApplyYProfileLongitudinalFairing(List<float> stations, Dictionary<int, List<Vector3>> pointDict, float side, int strakeCount)
    {
        if (!enableLongitudinalFairing || stations == null || stations.Count < 3 || pointDict == null || framingModel == null) return;

        int passes = Mathf.Clamp(fairingPasses, 1, 5);

        for (int pass = 0; pass < passes; pass++)
        {
            for (int k = 0; k < strakeCount; k++)
            {
                float[] rawY = new float[stations.Count];
                bool[] valid = new bool[stations.Count];

                for (int s = 0; s < stations.Count; s++)
                {
                    if (pointDict.ContainsKey(s) && pointDict[s].Count > k)
                    {
                        rawY[s] = pointDict[s][k].y;
                        valid[s] = true;
                    }
                }

                float[] targetY = new float[stations.Count];
                for (int s = 0; s < stations.Count; s++)
                {
                    if (!valid[s]) continue;

                    if (s == 0 || s == stations.Count - 1 || !valid[s - 1] || !valid[s + 1])
                    {
                        targetY[s] = rawY[s];
                    }
                    else
                    {
                        float ySmoothed = 0.25f * rawY[s - 1] + 0.50f * rawY[s] + 0.25f * rawY[s + 1];
                        targetY[s] = Mathf.Lerp(rawY[s], ySmoothed, fairingStrength);
                    }
                }

                for (int s = 0; s < stations.Count; s++)
                {
                    if (!valid[s]) continue;
                    float z = stations[s];
                    Vector3 fairedPt = framingModel.GetTransverseFramePointAtY(z, side, targetY[s]);
                    if (fairedPt != Vector3.zero)
                    {
                        pointDict[s][k] = fairedPt;
                    }
                }
            }
        }
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
        List<float> allStations = framingModel.CalculateFrameStations();
        if (allStations.Count == 0) return new List<Vector3>();
        float maxBowZ = allStations[allStations.Count - 1];
        if (z >= maxBowZ - 0.001f) return new List<Vector3>();

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!framingModel.GetFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return new List<Vector3>();

        float keelY = framingModel.GetFrameBottomYAtZ(z);
        return GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0f, 0.25f, count, keelY);
    }

    /// <summary>
    /// Gets strake points along the Upper-Lower Mould segment (Connection Point pLR to pL3).
    /// </summary>
    public List<Vector3> GetUpperLowerMouldStrakePoints(float z, float side, int count)
    {
        if (framingModel == null || sheerPlan == null) return new List<Vector3>();
        sheerPlan.UpdateCachedValues();
        List<float> allStations = framingModel.CalculateFrameStations();
        if (allStations.Count == 0) return new List<Vector3>();
        float maxBowZ = allStations[allStations.Count - 1];
        if (z >= maxBowZ - 0.001f) return new List<Vector3>();

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!framingModel.GetFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return new List<Vector3>();

        float keelY = framingModel.GetFrameBottomYAtZ(z);
        return GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0.25f, 0.50f, count, keelY);
    }

    /// <summary>
    /// Gets list of frame station Z coordinates for strakes, starting from the stern 
    /// through the Deadwood, main body, and bow, excluding the single station at the very bow tip.
    /// </summary>
    public List<float> GetValidFrameStations()
    {
        List<float> stations = new List<float>();
        if (framingModel == null || sheerPlan == null) return stations;

        sheerPlan.UpdateCachedValues();
        List<float> allStations = framingModel.CalculateFrameStations();
        if (allStations.Count <= 1) return stations;

        float maxBowZ = allStations[allStations.Count - 1];

        foreach (float z in allStations)
        {
            if (z < maxBowZ - 0.001f)
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
            float keelY = framingModel.GetFrameBottomYAtZ(z);

            // Port Side (-1f) - Fetch control points ONCE per station
            Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
            if (framingModel.GetFrameControlPointsAtZ(z, -1f, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            {
                List<Vector3> portLL = GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0f, 0.25f, llCount, keelY);
                List<Vector3> portUL = GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0.25f, 0.50f, ulCount, keelY);

                portLLPoints[s] = portLL;
                portULPoints[s] = portUL;
            }

            // Starboard Side (+1f) - Fetch control points ONCE per station
            if (framingModel.GetFrameControlPointsAtZ(z, 1f, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            {
                List<Vector3> stbdLL = GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0f, 0.25f, llCount, keelY);
                List<Vector3> stbdUL = GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0.25f, 0.50f, ulCount, keelY);

                stbdLLPoints[s] = stbdLL;
                stbdULPoints[s] = stbdUL;
            }
        }

        // Apply Y-Profile Surface-Constrained Longitudinal Fairing
        if (enableLongitudinalFairing)
        {
            ApplyYProfileLongitudinalFairing(stations, portLLPoints, -1f, llCount);
            ApplyYProfileLongitudinalFairing(stations, portULPoints, -1f, ulCount);
            ApplyYProfileLongitudinalFairing(stations, stbdLLPoints, 1f, llCount);
            ApplyYProfileLongitudinalFairing(stations, stbdULPoints, 1f, ulCount);
        }

        // Draw Node Spheres
        for (int s = 0; s < stations.Count; s++)
        {
            if (portLLPoints.ContainsKey(s))
            {
                Gizmos.color = lowerLowerPointColor;
                foreach (Vector3 pt in portLLPoints[s]) Gizmos.DrawSphere(pt, pointRadius);
                if (highlightConnectionPoints && portLLPoints[s].Count > 0)
                {
                    Gizmos.color = connectionPointColor;
                    Gizmos.DrawSphere(portLLPoints[s][portLLPoints[s].Count - 1], pointRadius * 1.3f);
                }
            }

            if (portULPoints.ContainsKey(s))
            {
                Gizmos.color = upperLowerPointColor;
                foreach (Vector3 pt in portULPoints[s]) Gizmos.DrawSphere(pt, pointRadius);
            }

            if (stbdLLPoints.ContainsKey(s))
            {
                Gizmos.color = lowerLowerPointColor;
                foreach (Vector3 pt in stbdLLPoints[s]) Gizmos.DrawSphere(pt, pointRadius);
                if (highlightConnectionPoints && stbdLLPoints[s].Count > 0)
                {
                    Gizmos.color = connectionPointColor;
                    Gizmos.DrawSphere(stbdLLPoints[s][stbdLLPoints[s].Count - 1], pointRadius * 1.3f);
                }
            }

            if (stbdULPoints.ContainsKey(s))
            {
                Gizmos.color = upperLowerPointColor;
                foreach (Vector3 pt in stbdULPoints[s]) Gizmos.DrawSphere(pt, pointRadius);
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
