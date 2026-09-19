using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// StrakesModel places and visualizes highly optimized strake points and longitudinal guide lines 
/// separated into Lower-Lower Mould strakes (u in [0, 0.25]) and Upper-Lower Mould strakes (u in [0.25, 0.50]),
/// divided by the Connection Points along the lower frame moulds up to the Keel Bow.
/// Optimized for maximum Edit-Mode performance with full 3D point caching and 1D-decoupled fairing passes.
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

    [Tooltip("Size of the strake point node markers in Scene view.")]
    [Range(0.01f, 0.3f)]
    public float pointRadius = 0.05f;

    [Tooltip("Connect strake points across adjacent frame stations to form longitudinal strake guide lines.")]
    public bool showStrakeLines = true;

    [Header("Strake Smoothing & Longitudinal Fairing Settings")]
    [Tooltip("Enable Y-Profile Longitudinal Fairing to smooth the side-view Y-height profile of strakes across stations Z.")]
    public bool enableLongitudinalFairing = true;

    [Range(0f, 2f)]
    [Tooltip("Strength blend for Y-Profile Fairing (0 = raw station math, 1 = 100% smoothed blend, >1 = over-relaxation for maximum curvature stiffness).")]
    public float fairingStrength = 1.0f;

    [Range(1, 20)]
    [Tooltip("Number of longitudinal smoothing passes (higher values apply stronger longitudinal fairing across frames).")]
    public int fairingPasses = 5;

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

    // Static buffers for zero heap allocation during calculation passes
    private static readonly Vector3[] ptTableBuffer = new Vector3[64];
    private static readonly float[] distTableBuffer = new float[64];
    private static readonly float[] rawYBuffer = new float[512];
    private static readonly float[] targetYBuffer = new float[512];
    private static readonly bool[] validBuffer = new bool[512];

    // Caching fields
    private List<float> cachedValidStations = new List<float>();
    private float cachedMidshipLength = 0f;
    private bool isCacheDirty = true;

    // Cached fully faired 3D strake point data for ultra-fast Gizmo drawing
    private readonly Dictionary<int, List<Vector3>> cachedPortLLPoints = new Dictionary<int, List<Vector3>>();
    private readonly Dictionary<int, List<Vector3>> cachedStbdLLPoints = new Dictionary<int, List<Vector3>>();
    private readonly Dictionary<int, List<Vector3>> cachedPortULPoints = new Dictionary<int, List<Vector3>>();
    private readonly Dictionary<int, List<Vector3>> cachedStbdULPoints = new Dictionary<int, List<Vector3>>();

    private void Awake()
    {
        FindReferences();
        SetDirty();
    }

    private void OnEnable()
    {
        FindReferences();
        SetDirty();
    }

    private void OnValidate()
    {
        SetDirty();
    }

    public void SetDirty()
    {
        isCacheDirty = true;
    }

    public void FindReferences()
    {
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
        if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();
    }

    private void RefreshCacheIfNeeded()
    {
        if (!isCacheDirty) return;
        FindReferences();

        // 1. Cache Midship Length ONCE
        cachedMidshipLength = 0f;
        if (framingModel != null)
        {
            Vector3 mL0, mL1, mLR, mL2, mL3, mU0, mU1, mU2, mU3;
            if (framingModel.GetFrameControlPointsAtZ(0f, 1f, out mL0, out mL1, out mLR, out mL2, out mL3, out mU0, out mU1, out mU2, out mU3))
            {
                cachedMidshipLength = Vector3.Distance(mL0, mLR) + Vector3.Distance(mLR, mL3);
            }
        }

        // 2. Cache Valid Frame Stations ONCE (including the bow apex vertical line at fwdZ)
        cachedValidStations.Clear();
        if (framingModel != null && sheerPlan != null)
        {
            sheerPlan.UpdateCachedValues();
            List<float> allStations = framingModel.CalculateFrameStations();
            if (allStations.Count > 0)
            {
                cachedValidStations.AddRange(allStations);
            }

            float fwdZ = sheerPlan.GetFwdMostZ();
            if (cachedValidStations.Count == 0 || cachedValidStations[cachedValidStations.Count - 1] < fwdZ - 0.001f)
            {
                cachedValidStations.Add(fwdZ);
            }
        }

        RebuildCachedGizmoPoints();
        isCacheDirty = false;
    }

    private void RebuildCachedGizmoPoints()
    {
        cachedPortLLPoints.Clear();
        cachedStbdLLPoints.Clear();
        cachedPortULPoints.Clear();
        cachedStbdULPoints.Clear();

        List<float> stations = cachedValidStations;
        if (stations.Count == 0 || framingModel == null) return;

        int llCount = Mathf.Max(2, strakesPerLowerLowerMould);
        int ulCount = Mathf.Max(2, strakesPerUpperLowerMould);

        for (int s = 0; s < stations.Count; s++)
        {
            float z = stations[s];
            float keelY = framingModel.GetFrameBottomYAtZ(z);

            // Port Side (-1f)
            Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
            if (framingModel.GetFrameControlPointsAtZ(z, -1f, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            {
                cachedPortLLPoints[s] = GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0f, 0.25f, llCount, keelY, cachedMidshipLength);
                cachedPortULPoints[s] = GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0.25f, 0.50f, ulCount, keelY, cachedMidshipLength);
            }

            // Starboard Side (+1f)
            if (framingModel.GetFrameControlPointsAtZ(z, 1f, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            {
                cachedStbdLLPoints[s] = GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0f, 0.25f, llCount, keelY, cachedMidshipLength);
                cachedStbdULPoints[s] = GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0.25f, 0.50f, ulCount, keelY, cachedMidshipLength);
            }
        }

        // Apply Y-Profile Surface-Constrained Longitudinal Fairing across cached station points
        if (enableLongitudinalFairing)
        {
            ApplyYProfileLongitudinalFairing(stations, cachedPortLLPoints, -1f, llCount);
            ApplyYProfileLongitudinalFairing(stations, cachedPortULPoints, -1f, ulCount);
            ApplyYProfileLongitudinalFairing(stations, cachedStbdLLPoints, 1f, llCount);
            ApplyYProfileLongitudinalFairing(stations, cachedStbdULPoints, 1f, ulCount);
        }
    }

    /// <summary>
    /// Applies Y-Profile Surface-Constrained Longitudinal Fairing across stations Z.
    /// Operates in 1D array space for N smoothing passes, then projects back to 3D frame surfaces ONCE.
    /// </summary>
    private void ApplyYProfileLongitudinalFairing(List<float> stations, Dictionary<int, List<Vector3>> pointDict, float side, int strakeCount)
    {
        if (!enableLongitudinalFairing || stations == null || stations.Count < 3 || pointDict == null || framingModel == null) return;

        float hw = framingModel.GetKeelHalfWidth();
        int passes = Mathf.Clamp(fairingPasses, 1, 20);
        int stationCount = Mathf.Min(stations.Count, 512);

        for (int k = 0; k < strakeCount; k++)
        {
            // 1. Fetch un-faired Y points for strake k across all stations
            for (int s = 0; s < stationCount; s++)
            {
                validBuffer[s] = false;
                if (pointDict.ContainsKey(s) && pointDict[s].Count > k)
                {
                    rawYBuffer[s] = pointDict[s][k].y;
                    validBuffer[s] = true;
                }
            }

            // 2. Perform 1D binomial smoothing passes ONLY in float array memory (0 heavy 3D math here!)
            for (int pass = 0; pass < passes; pass++)
            {
                for (int s = 0; s < stationCount; s++)
                {
                    if (!validBuffer[s]) continue;

                    if (s == 0 || s == stationCount - 1 || !validBuffer[s - 1] || !validBuffer[s + 1])
                    {
                        targetYBuffer[s] = rawYBuffer[s];
                    }
                    else
                    {
                        float ySmoothed = 0.25f * rawYBuffer[s - 1] + 0.50f * rawYBuffer[s] + 0.25f * rawYBuffer[s + 1];
                        targetYBuffer[s] = Mathf.Lerp(rawYBuffer[s], ySmoothed, fairingStrength);
                    }
                }

                for (int s = 0; s < stationCount; s++)
                {
                    if (validBuffer[s]) rawYBuffer[s] = targetYBuffer[s];
                }
            }

            // 3. Project faired Y target heights back onto 3D frame surfaces ONCE after all passes complete
            for (int s = 0; s < stationCount; s++)
            {
                if (!validBuffer[s]) continue;
                float z = stations[s];
                float keelY = framingModel.GetFrameBottomYAtZ(z);

                Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
                if (framingModel.GetFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
                {
                    float profileY = pL0.y;

                    Vector3 fairedPt;
                    float fwdZVal = (sheerPlan != null) ? sheerPlan.GetFwdMostZ() : float.MinValue;
                    if (Mathf.Abs(z - fwdZVal) < 0.005f || targetYBuffer[s] < profileY - 0.001f)
                    {
                        float hobY = Mathf.Max(pL3.y, profileY);
                        float clampedY = Mathf.Clamp(targetYBuffer[s], keelY, hobY);
                        fairedPt = new Vector3(side * hw, clampedY, z);
                    }
                    else
                    {
                        fairedPt = framingModel.GetTransverseFramePointAtY(z, side, targetYBuffer[s]);
                    }

                    if (fairedPt != Vector3.zero)
                    {
                        pointDict[s][k] = fairedPt;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Evaluates list of 3D points equally spaced by arc length along a sub-segment u in [minU, maxU] 
    /// using pre-fetched control points. Includes Pseudo Frame vertical segment when present (pL0.y > keelY).
    /// Optimized with zero heap allocations during calculation.
    /// </summary>
    public List<Vector3> GetSegmentStrakePointsFromControlPoints(
        Vector3 pL0, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3, Vector3 pU0, Vector3 pU1, Vector3 pU2, Vector3 pU3,
        float minU, float maxU, int count, float keelY = float.NaN, float midshipLen = float.NaN)
    {
        List<Vector3> points = new List<Vector3>(count);
        if (framingModel == null) return points;

        count = Mathf.Max(2, count);

        float fwdZVal = (sheerPlan != null) ? sheerPlan.GetFwdMostZ() : float.MinValue;
        bool isBowApex = (!float.IsNaN(keelY) && (Mathf.Abs(pL0.z - fwdZVal) < 0.005f || Vector3.Distance(pL0, pL3) < 0.005f));

        if (isBowApex)
        {
            float hobY = Mathf.Max(pL3.y, pL0.y);
            float startY = Mathf.Lerp(keelY, hobY, minU * 2.0f);
            float endY = Mathf.Lerp(keelY, hobY, maxU * 2.0f);
            for (int k = 0; k < count; k++)
            {
                float t = (float)k / (count - 1);
                float y = Mathf.Lerp(startY, endY, t);
                points.Add(new Vector3(pL0.x, y, pL0.z));
            }
            return points;
        }
        bool hasPseudoFrame = (Mathf.Approximately(minU, 0f) && !float.IsNaN(keelY) && pL0.y > keelY + 0.001f);

        const int curveSamples = 20;
        const int pseudoSamples = 10;
        int totalSamples = hasPseudoFrame ? (pseudoSamples + curveSamples) : curveSamples;

        distTableBuffer[0] = 0f;
        float totalLength = 0f;

        if (hasPseudoFrame)
        {
            Vector3 keelBaselinePt = new Vector3(pL0.x, keelY, pL0.z);
            ptTableBuffer[0] = keelBaselinePt;

            for (int i = 1; i <= pseudoSamples; i++)
            {
                float t = i / (float)pseudoSamples;
                ptTableBuffer[i] = Vector3.Lerp(keelBaselinePt, pL0, t);
                float stepDist = Vector3.Distance(ptTableBuffer[i - 1], ptTableBuffer[i]);
                totalLength += stepDist;
                distTableBuffer[i] = totalLength;
            }

            for (int i = 1; i <= curveSamples; i++)
            {
                float u = Mathf.Lerp(minU, maxU, i / (float)curveSamples);
                int tableIdx = pseudoSamples + i;
                ptTableBuffer[tableIdx] = framingModel.EvaluateFramePointFromControlPoints(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                float stepDist = Vector3.Distance(ptTableBuffer[tableIdx - 1], ptTableBuffer[tableIdx]);
                totalLength += stepDist;
                distTableBuffer[tableIdx] = totalLength;
            }
        }
        else
        {
            ptTableBuffer[0] = framingModel.EvaluateFramePointFromControlPoints(minU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);

            for (int i = 1; i <= curveSamples; i++)
            {
                float u = Mathf.Lerp(minU, maxU, i / (float)curveSamples);
                ptTableBuffer[i] = framingModel.EvaluateFramePointFromControlPoints(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                float stepDist = Vector3.Distance(ptTableBuffer[i - 1], ptTableBuffer[i]);
                totalLength += stepDist;
                distTableBuffer[i] = totalLength;
            }
        }

        if (totalLength < 0.0001f)
        {
            for (int k = 0; k < count; k++) points.Add(ptTableBuffer[0]);
            return points;
        }

        float midLen = float.IsNaN(midshipLen) ? cachedMidshipLength : midshipLen;
        float girthRatio = (enableStrakeSmoothing && midLen > 0.001f) ? Mathf.Clamp01(totalLength / midLen) : 1.0f;
        float power = Mathf.Lerp(1.0f / Mathf.Clamp(minHobTaperRatio, 0.3f, 1.0f), 1.0f, girthRatio);

        for (int k = 0; k < count; k++)
        {
            float linearT = (float)k / (count - 1);
            float tBiased = enableStrakeSmoothing ? Mathf.Pow(linearT, power) : linearT;
            float targetDist = tBiased * totalLength;

            int idx = 0;
            while (idx < totalSamples && distTableBuffer[idx + 1] < targetDist)
            {
                idx++;
            }

            if (idx >= totalSamples)
            {
                points.Add(ptTableBuffer[totalSamples]);
            }
            else
            {
                float segmentLength = distTableBuffer[idx + 1] - distTableBuffer[idx];
                float t = segmentLength > 0.00001f ? (targetDist - distTableBuffer[idx]) / segmentLength : 0f;
                Vector3 pt = Vector3.Lerp(ptTableBuffer[idx], ptTableBuffer[idx + 1], t);
                points.Add(pt);
            }
        }

        return points;
    }

    /// <summary>
    /// Gets list of strake points along Lower-Lower Mould (Keel to Connection Point pLR) at station z.
    /// </summary>
    public List<Vector3> GetLowerLowerMouldStrakePoints(float z, float side, int count)
    {
        if (framingModel == null || sheerPlan == null) return new List<Vector3>();
        RefreshCacheIfNeeded();

        List<float> allStations = framingModel.CalculateFrameStations();
        if (allStations.Count == 0) return new List<Vector3>();
        float maxBowZ = allStations[allStations.Count - 1];

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!framingModel.GetFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return new List<Vector3>();

        float keelY = framingModel.GetFrameBottomYAtZ(z);
        return GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0f, 0.25f, count, keelY, cachedMidshipLength);
    }

    /// <summary>
    /// Gets list of strake points along Upper-Lower Mould (Connection Point pLR to pL3) at station z.
    /// </summary>
    public List<Vector3> GetUpperLowerMouldStrakePoints(float z, float side, int count)
    {
        if (framingModel == null || sheerPlan == null) return new List<Vector3>();
        RefreshCacheIfNeeded();

        List<float> allStations = framingModel.CalculateFrameStations();
        if (allStations.Count == 0) return new List<Vector3>();
        float maxBowZ = allStations[allStations.Count - 1];

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!framingModel.GetFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return new List<Vector3>();

        float keelY = framingModel.GetFrameBottomYAtZ(z);
        return GetSegmentStrakePointsFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, 0.25f, 0.50f, count, keelY, cachedMidshipLength);
    }

    /// <summary>
    /// Gets list of frame station Z coordinates for strakes, starting from the stern 
    /// through the Deadwood, main body, and bow, excluding the single station at the very bow tip.
    /// </summary>
    public List<float> GetValidFrameStations()
    {
        RefreshCacheIfNeeded();
        return cachedValidStations;
    }

    private void OnDrawGizmos()
    {
        if (!showStrakes) return;
        RefreshCacheIfNeeded();
        if (framingModel == null || sheerPlan == null) return;

        List<float> stations = cachedValidStations;
        if (stations.Count == 0) return;

        int llCount = Mathf.Max(2, strakesPerLowerLowerMould);
        int ulCount = Mathf.Max(2, strakesPerUpperLowerMould);

        // Draw Strake Nodes (Fast Cube Markers)
        Vector3 cubeSize = Vector3.one * (pointRadius * 1.5f);
        Vector3 highlightSize = Vector3.one * (pointRadius * 2.0f);

        for (int s = 0; s < stations.Count; s++)
        {
            if (cachedPortLLPoints.ContainsKey(s))
            {
                Gizmos.color = lowerLowerPointColor;
                foreach (Vector3 pt in cachedPortLLPoints[s]) Gizmos.DrawCube(pt, cubeSize);
                if (highlightConnectionPoints && cachedPortLLPoints[s].Count > 0)
                {
                    Gizmos.color = connectionPointColor;
                    Gizmos.DrawCube(cachedPortLLPoints[s][cachedPortLLPoints[s].Count - 1], highlightSize);
                }
            }

            if (cachedPortULPoints.ContainsKey(s))
            {
                Gizmos.color = upperLowerPointColor;
                foreach (Vector3 pt in cachedPortULPoints[s]) Gizmos.DrawCube(pt, cubeSize);
            }

            if (cachedStbdLLPoints.ContainsKey(s))
            {
                Gizmos.color = lowerLowerPointColor;
                foreach (Vector3 pt in cachedStbdLLPoints[s]) Gizmos.DrawCube(pt, cubeSize);
                if (highlightConnectionPoints && cachedStbdLLPoints[s].Count > 0)
                {
                    Gizmos.color = connectionPointColor;
                    Gizmos.DrawCube(cachedStbdLLPoints[s][cachedStbdLLPoints[s].Count - 1], highlightSize);
                }
            }

            if (cachedStbdULPoints.ContainsKey(s))
            {
                Gizmos.color = upperLowerPointColor;
                foreach (Vector3 pt in cachedStbdULPoints[s]) Gizmos.DrawCube(pt, cubeSize);
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
                    if (cachedPortLLPoints.ContainsKey(s) && cachedPortLLPoints.ContainsKey(s + 1) &&
                        cachedPortLLPoints[s].Count > k && cachedPortLLPoints[s + 1].Count > k)
                    {
                        Gizmos.DrawLine(cachedPortLLPoints[s][k], cachedPortLLPoints[s + 1][k]);
                    }

                    if (cachedStbdLLPoints.ContainsKey(s) && cachedStbdLLPoints.ContainsKey(s + 1) &&
                        cachedStbdLLPoints[s].Count > k && cachedStbdLLPoints[s + 1].Count > k)
                    {
                        Gizmos.DrawLine(cachedStbdLLPoints[s][k], cachedStbdLLPoints[s + 1][k]);
                    }
                }
            }

            // Upper-Lower Mould Lines
            Gizmos.color = upperLowerLineColor;
            for (int k = 0; k < ulCount; k++)
            {
                for (int s = 0; s < stations.Count - 1; s++)
                {
                    if (cachedPortULPoints.ContainsKey(s) && cachedPortULPoints.ContainsKey(s + 1) &&
                        cachedPortULPoints[s].Count > k && cachedPortULPoints[s + 1].Count > k)
                    {
                        Gizmos.DrawLine(cachedPortULPoints[s][k], cachedPortULPoints[s + 1][k]);
                    }

                    if (cachedStbdULPoints.ContainsKey(s) && cachedStbdULPoints.ContainsKey(s + 1) &&
                        cachedStbdULPoints[s].Count > k && cachedStbdULPoints[s + 1].Count > k)
                    {
                        Gizmos.DrawLine(cachedStbdULPoints[s][k], cachedStbdULPoints[s + 1][k]);
                    }
                }
            }
        }
    }
}
