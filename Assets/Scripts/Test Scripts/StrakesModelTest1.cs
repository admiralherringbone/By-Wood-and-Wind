using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class StrakesModelTest1 : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FramingModelWaterlines waterlines;

    [Header("Master Frame Spacing Settings")]
    [Tooltip("Target spacing distance along the Master Frame (Z=0) curve between HoB and Rising Line.")]
    public float targetSpacing = 0.25f;

    [Tooltip("Draw points on both Port (+X) and Starboard (-X) sides of the frames.")]
    public bool drawStarboard = true;

    [Header("Angled Frame Settings")]
    [Tooltip("Show points on the angled frames from the Bow End of the Rising Line to the Stem.")]
    public bool showAngledFramePoints = true;

    [Header("Point Visualization Colors")]
    [Tooltip("Color of the Gizmo spheres drawn on the primary transverse frames.")]
    public Color pointColor = new Color(1f, 0.6f, 0f);

    [Tooltip("Color of the Gizmo spheres drawn on the Master Frame.")]
    public Color masterFramePointColor = Color.yellow;

    [Tooltip("Color of the Gizmo spheres drawn on the angled frames.")]
    public Color angledFramePointColor = Color.cyan;

    [Tooltip("Radius of the Gizmo spheres.")]
    public float pointRadius = 0.04f;

    // Cache containers for zero-lag Gizmos rendering
    private List<Vector3> cachedTransversePtsPort = new List<Vector3>();
    private List<Vector3> cachedTransversePtsStbd = new List<Vector3>();
    private List<Vector3> cachedMasterPtsPort = new List<Vector3>();
    private List<Vector3> cachedMasterPtsStbd = new List<Vector3>();
    private List<Vector3> cachedAngledPtsPort = new List<Vector3>();
    private List<Vector3> cachedAngledPtsStbd = new List<Vector3>();

    private bool isCacheDirty = true;

    private void OnValidate()
    {
        isCacheDirty = true;
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
        if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();
        if (waterlines == null) waterlines = FindFirstObjectByType<FramingModelWaterlines>();
    }

    private void RebuildCache()
    {
        cachedTransversePtsPort.Clear();
        cachedTransversePtsStbd.Clear();
        cachedMasterPtsPort.Clear();
        cachedMasterPtsStbd.Clear();
        cachedAngledPtsPort.Clear();
        cachedAngledPtsStbd.Clear();

        if (sheerPlan == null || bodyPlan == null) return;

        bodyPlan.UpdateCachedCurves();
        sheerPlan.UpdateCachedValues();

        // 1. Calculate Master Frame (Z=0) lower mould arc length and fix segment count N
        float masterArcLength = GetFrameLowerMouldArcLength(0f);
        if (masterArcLength <= 0.0001f) masterArcLength = 1f;

        int numSegments = Mathf.Max(1, Mathf.RoundToInt(masterArcLength / Mathf.Max(0.01f, targetSpacing)));

        // 2. PRIMARY TRANSVERSE FRAMES: Get stations from aft up to Bow End of the Rising Line
        float aftZ = sheerPlan.GetAftMostZ();
        float bowRisingZ = sheerPlan.cachedBowRisingEnd.z;

        List<float> rawStations = (framingModel != null)
            ? framingModel.CalculateFrameStations()
            : new List<float> { 0f };

        if (rawStations != null && rawStations.Count > 0)
        {
            List<float> stations = new List<float>();
            foreach (float z in rawStations)
            {
                if (z >= aftZ - 0.001f && z <= bowRisingZ + 0.001f)
                {
                    stations.Add(z);
                }
            }
            stations.Sort();

            foreach (float z in stations)
            {
                bool isMaster = Mathf.Abs(z) < 0.001f;
                List<Vector3> pts = GetPointsOnFrameWithSegmentCount(z, numSegments);

                foreach (Vector3 ptPort in pts)
                {
                    Vector3 ptStbd = new Vector3(-ptPort.x, ptPort.y, ptPort.z);
                    if (isMaster)
                    {
                        cachedMasterPtsPort.Add(ptPort);
                        cachedMasterPtsStbd.Add(ptStbd);
                    }
                    else
                    {
                        cachedTransversePtsPort.Add(ptPort);
                        cachedTransversePtsStbd.Add(ptStbd);
                    }
                }
            }
        }

        // 3. ANGLED FRAMES: Lay out N + 1 points on each angled frame between its bottom (u=0) and HoB (u=0.5)
        if (showAngledFramePoints && framingModel != null && framingModel.showAngledFrames)
        {
            float step = Mathf.Max(1f, framingModel.angledFrameAngleStep);

            for (float deg = 0f; deg <= 90.01f; deg += step)
            {
                float clampedAngle = Mathf.Min(90f, deg);

                List<Vector3> portAngledPts = GetPointsOnAngledFrameWithSegmentCount(clampedAngle, 1f, numSegments);
                cachedAngledPtsPort.AddRange(portAngledPts);

                List<Vector3> stbdAngledPts = GetPointsOnAngledFrameWithSegmentCount(clampedAngle, -1f, numSegments);
                cachedAngledPtsStbd.AddRange(stbdAngledPts);
            }
        }

        isCacheDirty = false;
    }

    private void OnDrawGizmos()
    {
        AutoFindReferences();
        if (sheerPlan == null || bodyPlan == null) return;

        if (isCacheDirty)
        {
            RebuildCache();
        }

        // 1. Primary Transverse Frame Points
        Gizmos.color = pointColor;
        foreach (Vector3 ptPort in cachedTransversePtsPort)
        {
            Gizmos.DrawSphere(ptPort, pointRadius);
        }
        if (drawStarboard)
        {
            foreach (Vector3 ptStbd in cachedTransversePtsStbd)
            {
                Gizmos.DrawSphere(ptStbd, pointRadius);
            }
        }

        // 2. Master Frame Points
        Gizmos.color = masterFramePointColor;
        foreach (Vector3 ptPort in cachedMasterPtsPort)
        {
            Gizmos.DrawSphere(ptPort, pointRadius);
        }
        if (drawStarboard)
        {
            foreach (Vector3 ptStbd in cachedMasterPtsStbd)
            {
                Gizmos.DrawSphere(ptStbd, pointRadius);
            }
        }

        // 3. Angled Frame Points
        if (showAngledFramePoints)
        {
            Gizmos.color = angledFramePointColor;
            foreach (Vector3 ptPort in cachedAngledPtsPort)
            {
                Gizmos.DrawSphere(ptPort, pointRadius);
            }
            if (drawStarboard)
            {
                foreach (Vector3 ptStbd in cachedAngledPtsStbd)
                {
                    Gizmos.DrawSphere(ptStbd, pointRadius);
                }
            }
        }
    }

    /// <summary>
    /// Computes lower mould arc length of frame station z between Rising Line (u=0.25) and HoB (u=0.50).
    /// </summary>
    private float GetFrameLowerMouldArcLength(float z)
    {
        const int samples = 40;
        float totalArcLength = 0f;
        Vector3 prevPt = GetFramePointAtU(z, 0.25f);

        for (int i = 1; i <= samples; i++)
        {
            float u = Mathf.Lerp(0.25f, 0.5f, (float)i / samples);
            Vector3 currPt = GetFramePointAtU(z, u);
            totalArcLength += Vector3.Distance(prevPt, currPt);
            prevPt = currPt;
        }
        return totalArcLength;
    }

    /// <summary>
    /// Computes N + 1 points equally spaced by arc-length along frame station z's lower mould curve
    /// between Rising Line (u=0.25) and HoB (u=0.50).
    /// </summary>
    private List<Vector3> GetPointsOnFrameWithSegmentCount(float z, int numSegments)
    {
        if (numSegments < 1) numSegments = 1;

        const int samples = 40;
        float totalArcLength = 0f;
        Vector3 prevPt = GetFramePointAtU(z, 0.25f);

        Vector3[] sampledPts = new Vector3[samples + 1];
        sampledPts[0] = prevPt;

        float[] cumDistances = new float[samples + 1];
        cumDistances[0] = 0f;

        for (int i = 1; i <= samples; i++)
        {
            float u = Mathf.Lerp(0.25f, 0.5f, (float)i / samples);
            Vector3 currPt = GetFramePointAtU(z, u);
            sampledPts[i] = currPt;
            float dist = Vector3.Distance(prevPt, currPt);
            totalArcLength += dist;
            cumDistances[i] = totalArcLength;
            prevPt = currPt;
        }

        List<Vector3> points = new List<Vector3>(numSegments + 1);

        if (totalArcLength <= 0.0001f)
        {
            Vector3 pLR = sampledPts[0];
            for (int i = 0; i <= numSegments; i++) points.Add(pLR);
            return points;
        }

        float stepArcLength = totalArcLength / numSegments;

        for (int i = 0; i <= numSegments; i++)
        {
            float targetDist = i * stepArcLength;
            Vector3 pt = SamplePointFromArcLengthArray(targetDist, cumDistances, sampledPts, samples);
            points.Add(pt);
        }

        return points;
    }

    /// <summary>
    /// Computes N + 1 points equally spaced by arc-length along an angled frame at angleDeg
    /// between its bottom (u=0) and the HoB line above (u=0.5).
    /// </summary>
    private List<Vector3> GetPointsOnAngledFrameWithSegmentCount(float angleDeg, float side, int numSegments)
    {
        if (numSegments < 1) numSegments = 1;

        const int samples = 40;
        float totalArcLength = 0f;
        Vector3 prevPt = framingModel.GetAngledFramePointAtU(angleDeg, side, 0f);

        Vector3[] sampledPts = new Vector3[samples + 1];
        sampledPts[0] = prevPt;

        float[] cumDistances = new float[samples + 1];
        cumDistances[0] = 0f;

        for (int i = 1; i <= samples; i++)
        {
            float u = Mathf.Lerp(0f, 0.5f, (float)i / samples);
            Vector3 currPt = framingModel.GetAngledFramePointAtU(angleDeg, side, u);
            sampledPts[i] = currPt;
            float dist = Vector3.Distance(prevPt, currPt);
            totalArcLength += dist;
            cumDistances[i] = totalArcLength;
            prevPt = currPt;
        }

        List<Vector3> points = new List<Vector3>(numSegments + 1);

        if (totalArcLength <= 0.0001f)
        {
            Vector3 pBot = sampledPts[0];
            for (int i = 0; i <= numSegments; i++) points.Add(pBot);
            return points;
        }

        float stepArcLength = totalArcLength / numSegments;

        for (int i = 0; i <= numSegments; i++)
        {
            float targetDist = i * stepArcLength;
            Vector3 pt = SamplePointFromArcLengthArray(targetDist, cumDistances, sampledPts, samples);
            points.Add(pt);
        }

        return points;
    }

    private Vector3 GetFramePointAtU(float z, float u)
    {
        if (framingModel != null)
        {
            return framingModel.GetTransverseFramePointAtU(z, 1f, u);
        }
        else if (bodyPlan != null)
        {
            return bodyPlan.EvaluateLowerMouldAtT(u * 2f);
        }
        return Vector3.zero;
    }

    private Vector3 SamplePointFromArcLengthArray(float targetDist, float[] cumDistances, Vector3[] sampledPts, int samples)
    {
        if (targetDist <= 0f) return sampledPts[0];
        if (targetDist >= cumDistances[samples]) return sampledPts[samples];

        int low = 0;
        int high = samples;
        while (low < high - 1)
        {
            int mid = (low + high) / 2;
            if (cumDistances[mid] <= targetDist) low = mid;
            else high = mid;
        }

        float segLen = cumDistances[high] - cumDistances[low];
        float fraction = (segLen > 0.00001f) ? (targetDist - cumDistances[low]) / segLen : 0f;

        return Vector3.Lerp(sampledPts[low], sampledPts[high], fraction);
    }
}
