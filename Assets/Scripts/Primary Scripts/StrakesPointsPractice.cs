using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Practice script for evaluating frame points anchored to the Master Frame (midship)
/// and linking matching index points across frames into longitudinal strake lines.
/// 
/// 1. Master Frame (z = 0) evaluates N_master segments based on target pointInterval (default 0.25).
/// 2. All other frames use N_master defining segments across their complete path (Keel height to Main Sheer).
/// 3. Virtual points below elevated frame bottoms (e.g. at stem/stern) are used for measurement only and discarded.
/// 4. Matching index points k across frames are linked into longitudinal strake lines that run across all
///    relevant frames and stop at the last frame that has a matching partner at the bow or stern.
/// </summary>
[ExecuteInEditMode]
public class StrakesPointsPractice : MonoBehaviour
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

    [Header("Visualization Settings")]
    [Tooltip("Display frame points in Scene View.")]
    public bool showPoints = true;

    [Tooltip("Display longitudinal strake lines connecting matching points across frames.")]
    public bool showStrakeLines = true;

    [Tooltip("Show visualization on Starboard side.")]
    public bool showStarboard = true;

    [Tooltip("Show visualization on Port side.")]
    public bool showPort = true;

    [Tooltip("Radius of point gizmo spheres.")]
    [Range(0.01f, 0.2f)]
    public float pointRadius = 0.04f;

    [Tooltip("Color of evaluated frame points.")]
    public Color pointColor = Color.cyan;

    [Tooltip("Color of standard strake lines.")]
    public Color strakeLineColor = new Color(0.9f, 0.6f, 0.2f, 0.9f);

    [Tooltip("Color of the Main Sheer strake line.")]
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
    /// Attempts to evaluate the 3D point for global index k (0 <= k <= N_master) on frame station Z.
    /// Returns true if index k lands on the actual frame contour; returns false if index k falls in virtual space.
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

        // Construct full measurement path (Virtual Vertical Ascent from Keel Baseline + Actual Frame Profile)
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

        // Discard virtual points below actual frame bottom
        if (vertLen > 0.0001f && targetS < vertLen - 0.001f)
        {
            return false;
        }

        // Interpolate point at distance targetS along polyline
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
    /// Evaluates all placed frame points for a station Z and side.
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
    /// Links matching index points k across consecutive frame stations into continuous longitudinal strake lines.
    /// Each line starts at the aft-most frame that has point k and ends at the fore-most frame that has point k.
    /// </summary>
    public List<List<Vector3>> GetStrakeLines(float side)
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

    private List<List<Vector3>> m_CachedStbdLines = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortLines = new List<List<Vector3>>();
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

        m_CachedStbdLines = GetStrakeLines(1f);
        m_CachedPortLines = GetStrakeLines(-1f);

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
        if (m_IsCacheDirty || m_LastChangeVersion != currentVersion || m_CachedStbdLines == null)
        {
            RebuildCache();
        }

        // 1. DRAW FRAME POINTS
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

        // 2. DRAW LONGITUDINAL STRAKE LINES
        if (showStrakeLines)
        {
            if (showStarboard) DrawCachedLines(m_CachedStbdLines);
            if (showPort) DrawCachedLines(m_CachedPortLines);
        }
    }

    private void DrawCachedLines(List<List<Vector3>> lines)
    {
        if (lines == null) return;
        int count = lines.Count;

        for (int index = 0; index < count; index++)
        {
            Color c = strakeLineColor;
            if (index == count - 1) c = sheerLineColor;

            Gizmos.color = c;
            List<Vector3> pts = lines[index];

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Gizmos.DrawLine(pts[i], pts[i + 1]);
            }
        }
    }
}
