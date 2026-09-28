using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Practice script for evaluating frame points using the Master Frame N_master method
/// and applying Centripetal Catmull-Rom Longitudinal Spline Interpolation across frame stations.
/// 
/// Leaves StrakesModel.cs and StrakesPointsPractice.cs 100% untouched.
/// 
/// 1. Master Frame (z = 0) evaluates N_master segments based on target pointInterval (default 0.25).
/// 2. All other frames use N_master defining segments across their complete path (Keel height to Main Sheer).
/// 3. Virtual points below elevated frame bottoms (e.g. at stem/stern) are discarded.
/// 4. Matching index points k across frames are linked into continuous 3D Centripetal Catmull-Rom Splines.
/// </summary>
[ExecuteInEditMode]
public class StrakesPointsCatmullRom : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FashionTimberModel fashionTimberModel;

    [Header("Point Spacing Settings")]
    [Tooltip("Target interval distance for placing points on Master Frame (default 0.25).")]
    [Range(0.01f, 2.0f)]
    public float pointInterval = 0.25f;

    [Header("Spline Fairing Settings")]
    [Tooltip("Enable Centripetal Catmull-Rom longitudinal spline fairing across frame stations.")]
    public bool enableCatmullRomFairing = true;

    [Tooltip("Centripetal parameter alpha (0.5 = Centripetal, 0.0 = Uniform, 1.0 = Chordal). Centripetal (0.5) avoids loops & overshooting.")]
    [Range(0f, 1f)]
    public float catmullRomAlpha = 0.5f;

    [Tooltip("Number of sub-segment evaluation steps between adjacent frame stations along each spline.")]
    [Range(2, 30)]
    public int subsegmentDensity = 10;

    [Header("Visualization Settings")]
    [Tooltip("Display raw discrete frame points in Scene View.")]
    public bool showPoints = true;

    [Tooltip("Display longitudinal strake curves in Scene View.")]
    public bool showStrakeLines = true;

    [Tooltip("Display linear polylines behind the faired splines for direct visual comparison.")]
    public bool showLinearComparison = false;

    [Tooltip("Show visualization on Starboard side.")]
    public bool showStarboard = true;

    [Tooltip("Show visualization on Port side.")]
    public bool showPort = true;

    [Tooltip("Radius of point gizmo spheres.")]
    [Range(0.01f, 0.2f)]
    public float pointRadius = 0.04f;

    [Tooltip("Color of evaluated raw frame points.")]
    public Color pointColor = Color.cyan;

    [Tooltip("Color of faired Catmull-Rom strake splines.")]
    public Color strakeLineColor = new Color(0.95f, 0.55f, 0.15f, 0.95f);

    [Tooltip("Color of linear comparison polylines.")]
    public Color linearComparisonColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Tooltip("Color of the Main Sheer strake spline.")]
    public Color sheerLineColor = Color.yellow;

    private void Awake()
    {
        FindReferences();
        MarkDirty();
    }

    private void OnValidate()
    {
        FindReferences();
        MarkDirty();
    }

    public void FindReferences()
    {
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
        if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();
        if (fashionTimberModel == null) fashionTimberModel = FindFirstObjectByType<FashionTimberModel>();
    }

    /// <summary>
    /// Evaluates the defining number of segments N_master from the Master Frame at z = 0.
    /// </summary>
    public int GetMasterFrameSegmentCount(float sideSign = 1f)
    {
        if (sheerPlan == null || framingModel == null || pointInterval <= 0.001f)
            return 12;

        float masterZ = 0f;
        float keelY = sheerPlan.cachedMidshipKeel.y;
        float sheerY = sheerPlan.GetMainSheerYAtZ(masterZ);

        Vector3 pL0 = Vector3.zero, pL1 = Vector3.zero, pLR = Vector3.zero, pL2 = Vector3.zero, pL3 = Vector3.zero;
        Vector3 pU0 = Vector3.zero, pU1 = Vector3.zero, pU2 = Vector3.zero, pU3 = Vector3.zero;
        bool hasFrame = framingModel.GetFrameControlPointsAtZ(masterZ, sideSign, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3);

        if (!hasFrame) return 12;

        float uSheer = GetUSheerAtY(sheerY, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);

        const int samples = 100;
        float masterLen = 0f;
        Vector3 prevPt = framingModel.EvaluateFramePointFromControlPoints(0f, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);

        for (int i = 1; i <= samples; i++)
        {
            float u = Mathf.Lerp(0f, uSheer, (float)i / samples);
            Vector3 currPt = framingModel.EvaluateFramePointFromControlPoints(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
            masterLen += Vector3.Distance(prevPt, currPt);
            prevPt = currPt;
        }

        float frameBottomY = pL0.y;
        if (frameBottomY > keelY + 0.0001f)
        {
            masterLen += (frameBottomY - keelY);
        }

        return Mathf.Max(1, Mathf.RoundToInt(masterLen / pointInterval));
    }

    /// <summary>
    /// Evaluates 3D point for global index k (0 <= k <= N_master) on frame station Z.
    /// </summary>
    public bool TryGetFramePointAtIndex(float z, float sideSign, int k, int numSegments, out Vector3 point)
    {
        point = Vector3.zero;
        if (sheerPlan == null || framingModel == null || numSegments < 1) return false;

        float hw = framingModel.GetKeelHalfWidth();
        float keelY = sheerPlan.cachedMidshipKeel.y;
        float sheerY = sheerPlan.GetMainSheerYAtZ(z);

        Vector3 pL0 = Vector3.zero, pL1 = Vector3.zero, pLR = Vector3.zero, pL2 = Vector3.zero, pL3 = Vector3.zero;
        Vector3 pU0 = Vector3.zero, pU1 = Vector3.zero, pU2 = Vector3.zero, pU3 = Vector3.zero;
        bool hasFrame = framingModel.GetFrameControlPointsAtZ(z, sideSign, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3);

        if (!hasFrame) return false;

        float frameBottomY = pL0.y;
        float uSheer = GetUSheerAtY(sheerY, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);

        List<Vector3> polyline = new List<Vector3>();
        float vertLen = 0f;

        if (frameBottomY > keelY + 0.0001f)
        {
            vertLen = frameBottomY - keelY;
            const int vertSamples = 20;
            for (int i = 0; i <= vertSamples; i++)
            {
                float vertY = Mathf.Lerp(keelY, frameBottomY, (float)i / vertSamples);
                polyline.Add(new Vector3(sideSign * hw, vertY, z));
            }
        }

        const int frameSamples = 80;
        int startIdx = (polyline.Count > 0) ? 1 : 0;
        for (int i = startIdx; i <= frameSamples; i++)
        {
            float u = Mathf.Lerp(0f, uSheer, (float)i / frameSamples);
            Vector3 framePt = framingModel.EvaluateFramePointFromControlPoints(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
            polyline.Add(framePt);
        }

        if (polyline.Count < 2) return false;

        float totalLen = 0f;
        float[] cumDistances = new float[polyline.Count];
        cumDistances[0] = 0f;

        for (int i = 1; i < polyline.Count; i++)
        {
            float dist = Vector3.Distance(polyline[i - 1], polyline[i]);
            totalLen += dist;
            cumDistances[i] = totalLen;
        }

        if (totalLen <= 0.00001f) return false;

        float targetS = (float)k / numSegments * totalLen;

        if (vertLen > 0.0001f && targetS < vertLen - 0.001f)
        {
            return false;
        }

        point = polyline[polyline.Count - 1];
        for (int i = 0; i < polyline.Count - 1; i++)
        {
            if (targetS >= cumDistances[i] && targetS <= cumDistances[i + 1])
            {
                float segLen = cumDistances[i + 1] - cumDistances[i];
                float frac = (segLen > 0.00001f) ? (targetS - cumDistances[i]) / segLen : 0f;
                point = Vector3.Lerp(polyline[i], polyline[i + 1], frac);
                break;
            }
        }

        return true;
    }

    /// <summary>
    /// Evaluates raw station points for given side.
    /// </summary>
    public List<Vector3> GetFramePointsAtStation(float z, float side)
    {
        List<Vector3> resultPoints = new List<Vector3>();
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        int numSegments = GetMasterFrameSegmentCount(sideSign);

        for (int k = 0; k <= numSegments; k++)
        {
            Vector3 pt;
            if (TryGetFramePointAtIndex(z, sideSign, k, numSegments, out pt))
            {
                resultPoints.Add(pt);
            }
        }

        return resultPoints;
    }

    /// <summary>
    /// Evaluates raw linear strake polylines connecting discrete station points.
    /// </summary>
    public List<List<Vector3>> GetLinearStrakeLines(float side)
    {
        List<List<Vector3>> strakeLines = new List<List<Vector3>>();
        if (sheerPlan == null || framingModel == null) return strakeLines;

        List<float> stations = framingModel.CalculateFrameStations();
        if (stations == null || stations.Count == 0) return strakeLines;

        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        int numSegments = GetMasterFrameSegmentCount(sideSign);

        for (int k = 0; k <= numSegments; k++)
        {
            List<Vector3> linePoints = new List<Vector3>();

            foreach (float z in stations)
            {
                Vector3 pt;
                if (TryGetFramePointAtIndex(z, sideSign, k, numSegments, out pt))
                {
                    linePoints.Add(pt);
                }
            }

            if (linePoints.Count >= 2)
            {
                strakeLines.Add(linePoints);
            }
        }

        return strakeLines;
    }

    /// <summary>
    /// Generates high-density Centripetal Catmull-Rom faired longitudinal strake splines.
    /// </summary>
    public List<List<Vector3>> GetFairedStrakeSplines(float side)
    {
        List<List<Vector3>> fairedSplines = new List<List<Vector3>>();
        List<List<Vector3>> linearLines = GetLinearStrakeLines(side);

        if (linearLines == null || linearLines.Count == 0) return fairedSplines;

        int density = Mathf.Clamp(subsegmentDensity, 2, 30);

        foreach (List<Vector3> ctrlPoints in linearLines)
        {
            if (ctrlPoints.Count < 2) continue;

            if (!enableCatmullRomFairing || ctrlPoints.Count < 3)
            {
                fairedSplines.Add(new List<Vector3>(ctrlPoints));
                continue;
            }

            List<Vector3> splinePoints = BuildCentripetalCatmullRomSpline(ctrlPoints, density, catmullRomAlpha);
            fairedSplines.Add(splinePoints);
        }

        return fairedSplines;
    }

    /// <summary>
    /// Evaluates a 3D Centripetal Catmull-Rom spline across a sequence of control points.
    /// </summary>
    public static List<Vector3> BuildCentripetalCatmullRomSpline(List<Vector3> pts, int densityPerBay, float alpha)
    {
        List<Vector3> result = new List<Vector3>();
        int n = pts.Count;
        if (n < 2) return result;

        if (n == 2)
        {
            for (int i = 0; i <= densityPerBay; i++)
            {
                float u = (float)i / densityPerBay;
                result.Add(Vector3.Lerp(pts[0], pts[1], u));
            }
            return result;
        }

        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = (i > 0) ? pts[i - 1] : pts[0] + (pts[0] - pts[1]);
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = (i + 2 < n) ? pts[i + 2] : pts[i + 1] + (pts[i + 1] - pts[i]);

            int steps = (i == n - 2) ? densityPerBay : densityPerBay - 1;
            for (int s = 0; s <= steps; s++)
            {
                float u = (float)s / densityPerBay;
                Vector3 evalPt = EvaluateCentripetalCatmullRom(p0, p1, p2, p3, u, alpha);
                result.Add(evalPt);
            }
        }

        return result;
    }

    /// <summary>
    /// Evaluates 3D Centripetal Catmull-Rom interpolation for 4 control points at local fraction u in [0, 1].
    /// </summary>
    public static Vector3 EvaluateCentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u, float alpha)
    {
        u = Mathf.Clamp01(u);
        alpha = Mathf.Clamp01(alpha);

        float GetKnot(float t, Vector3 a, Vector3 b)
        {
            float d2 = (b - a).sqrMagnitude;
            if (d2 <= 0.000001f) return t + 0.0001f;
            return t + Mathf.Pow(d2, alpha * 0.5f);
        }

        float t0 = 0f;
        float t1 = GetKnot(t0, p0, p1);
        float t2 = GetKnot(t1, p1, p2);
        float t3 = GetKnot(t2, p2, p3);

        if (t2 <= t1 + 0.00001f) return p1;

        float t = Mathf.Lerp(t1, t2, u);

        Vector3 a1 = (t1 > t0) ? (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1 : p1;
        Vector3 a2 = (t2 > t1) ? (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2 : p2;
        Vector3 a3 = (t3 > t2) ? (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3 : p3;

        Vector3 b1 = (t2 > t0) ? (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2 : a2;
        Vector3 b2 = (t3 > t1) ? (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3 : a3;

        Vector3 c = (t2 > t1) ? (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2 : b2;

        return c;
    }

    private float GetUSheerAtY(float sheerY, Vector3 pL0, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3, Vector3 pU0, Vector3 pU1, Vector3 pU2, Vector3 pU3)
    {
        if (sheerY >= pU3.y) return 1.0f;

        if (sheerY >= pL3.y)
        {
            float minU = 0.5f, maxU = 1.0f;
            for (int i = 0; i < 12; i++)
            {
                float midU = (minU + maxU) * 0.5f;
                Vector3 pt = framingModel.EvaluateFramePointFromControlPoints(midU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                if (pt.y < sheerY) minU = midU;
                else maxU = midU;
            }
            return (minU + maxU) * 0.5f;
        }
        else
        {
            float minU = 0f, maxU = 0.5f;
            for (int i = 0; i < 12; i++)
            {
                float midU = (minU + maxU) * 0.5f;
                Vector3 pt = framingModel.EvaluateFramePointFromControlPoints(midU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                if (pt.y < sheerY) minU = midU;
                else maxU = midU;
            }
            return (minU + maxU) * 0.5f;
        }
    }

    private List<List<Vector3>> m_CachedStbdFaired = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortFaired = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedStbdLinear = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortLinear = new List<List<Vector3>>();
    private List<Vector3> m_CachedStbdPoints = new List<Vector3>();
    private List<Vector3> m_CachedPortPoints = new List<Vector3>();
    private bool m_IsCacheDirty = true;
    private int m_LastChangeVersion = -1;

    public void MarkDirty()
    {
        m_IsCacheDirty = true;
    }

    [ContextMenu("Rebuild Cache")]
    public void ForceRebuildCache()
    {
        MarkDirty();
    }

    private void RebuildCache()
    {
        if (sheerPlan == null || framingModel == null) return;

        sheerPlan.UpdateCachedValues();
        if (bodyPlan != null) bodyPlan.UpdateCachedCurves();
        if (breadthPlan != null) breadthPlan.UpdateCachedCurves();

        m_CachedStbdLinear = GetLinearStrakeLines(1f);
        m_CachedPortLinear = GetLinearStrakeLines(-1f);

        m_CachedStbdFaired = GetFairedStrakeSplines(1f);
        m_CachedPortFaired = GetFairedStrakeSplines(-1f);

        m_CachedStbdPoints.Clear();
        m_CachedPortPoints.Clear();

        List<float> stations = framingModel.CalculateFrameStations();
        if (stations != null)
        {
            foreach (float z in stations)
            {
                m_CachedStbdPoints.AddRange(GetFramePointsAtStation(z, 1f));
                m_CachedPortPoints.AddRange(GetFramePointsAtStation(z, -1f));
            }
        }

        m_IsCacheDirty = false;
        if (framingModel != null) m_LastChangeVersion = framingModel.changeVersion;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || framingModel == null) return;

        int currentVersion = framingModel.changeVersion;
        if (m_IsCacheDirty || m_LastChangeVersion != currentVersion || m_CachedStbdFaired == null)
        {
            RebuildCache();
        }

        // 1. DRAW RAW FRAME POINTS
        if (showPoints)
        {
            Gizmos.color = pointColor;

            if (showStarboard)
            {
                foreach (Vector3 pt in m_CachedStbdPoints)
                {
                    Gizmos.DrawSphere(pt, pointRadius);
                }
            }

            if (showPort)
            {
                foreach (Vector3 pt in m_CachedPortPoints)
                {
                    Gizmos.DrawSphere(pt, pointRadius);
                }
            }
        }

        // 2. DRAW LINEAR COMPARISON POLYLINES
        if (showLinearComparison)
        {
            if (showStarboard) DrawLineList(m_CachedStbdLinear, linearComparisonColor);
            if (showPort) DrawLineList(m_CachedPortLinear, linearComparisonColor);
        }

        // 3. DRAW FAIRED CATMULL-ROM STRAKE SPLINES
        if (showStrakeLines)
        {
            if (showStarboard) DrawLineList(m_CachedStbdFaired, strakeLineColor);
            if (showPort) DrawLineList(m_CachedPortFaired, strakeLineColor);
        }
    }

    private void DrawLineList(List<List<Vector3>> lines, Color defaultColor)
    {
        if (lines == null) return;
        int count = lines.Count;

        for (int index = 0; index < count; index++)
        {
            Color c = defaultColor;
            if (index == count - 1 && defaultColor != linearComparisonColor) c = sheerLineColor;

            Gizmos.color = c;
            List<Vector3> pts = lines[index];

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Gizmos.DrawLine(pts[i], pts[i + 1]);
            }
        }
    }
}
