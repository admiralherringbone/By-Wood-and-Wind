using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Production Hybrid Strakes Model combining Master Frame (N_master) point anchoring,
/// virtual baseline measurement filtering, and Centripetal Catmull-Rom Longitudinal Spline Fairing.
/// 
/// Leaves StrakesModel.cs and StrakesPointsPractice.cs 100% untouched.
/// 
/// Core Architecture:
/// 1. Master Frame (z = 0) defines N_master segments from pointInterval (default 0.25).
/// 2. All frame stations evaluate points along the uniform baseline height (y = midship keel) up to Main Sheer Line height.
/// 3. Virtual points below elevated frame bottoms (stem/stern) are discarded (natural drop strakes/stealers).
/// 4. Matching index points k across frame stations are faired longitudinally using 3D Centripetal Catmull-Rom Splines.
/// 5. Top strake (k = N_master) is 100% locked to SheerPlan.GetMainSheerYAtZ(z).
/// </summary>
[ExecuteInEditMode]
public class StrakesHybridModel : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FashionTimberModel fashionTimberModel;

    [Header("Strake Layout Settings")]
    [Tooltip("Target interval distance for placing points on Master Frame (default 0.25).")]
    [Range(0.01f, 2.0f)]
    public float pointInterval = 0.25f;

    [Tooltip("Number of sub-segment evaluation steps between adjacent frame stations along each strake spline.")]
    [Range(2, 30)]
    public int subsegmentDensity = 10;

    [Tooltip("Centripetal parameter alpha (0.5 = Centripetal, 0.0 = Uniform, 1.0 = Chordal). Centripetal (0.5) avoids loops & overshooting.")]
    [Range(0f, 1f)]
    public float catmullRomAlpha = 0.5f;

    [Header("Visualization Settings")]
    [Tooltip("Display 3D faired strake lines in Scene View.")]
    public bool showStrakes = true;

    [Tooltip("Display strake index labels at midship in Scene View.")]
    public bool showLabels = true;

    [Tooltip("Show visualization on Starboard side.")]
    public bool showStarboard = true;

    [Tooltip("Show visualization on Port side.")]
    public bool showPort = true;

    [Tooltip("Color of standard hull strakes.")]
    public Color strakeColor = new Color(0.9f, 0.6f, 0.2f, 0.9f);

    [Tooltip("Color of the Main Sheer strake (topmost strake).")]
    public Color sheerStrakeColor = Color.yellow;

    [Tooltip("Color of the Garboard strake (bottommost strake along keel).")]
    public Color garboardStrakeColor = new Color(0.2f, 0.8f, 1f, 0.9f);

    private List<List<Vector3>> m_CachedStbdStrakes = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortStrakes = new List<List<Vector3>>();
    private List<Vector3> m_CachedLabelPositions = new List<Vector3>();
    private bool m_IsCacheDirty = true;
    private int m_LastChangeVersion = -1;

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

    public void MarkDirty()
    {
        m_IsCacheDirty = true;
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
    /// Evaluates defining segment count N_master from the Master Frame at z = 0.
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
    /// Evaluates 3D frame point for global index k (0 <= k <= N_master) at station Z.
    /// Returns true if point lands on actual frame; returns false if point is virtual.
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

        // LOCKED CORE LOGIC: Topmost strake (k == numSegments) is 100% locked to Main Sheer Line
        if (k == numSegments)
        {
            Vector3 sheerPt = framingModel.GetTransverseFramePointAtYFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, sheerY);
            sheerPt.y = sheerY;
            point = sheerPt;
            return true;
        }

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
    /// Gets high-density Centripetal Catmull-Rom faired strake splines for given side.
    /// </summary>
    public List<List<Vector3>> GetAllHybridStrakes(float side)
    {
        List<List<Vector3>> strakeSplines = new List<List<Vector3>>();
        if (sheerPlan == null || framingModel == null) return strakeSplines;

        List<float> stations = framingModel.CalculateFrameStations();
        if (stations == null || stations.Count == 0) return strakeSplines;

        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        int numSegments = GetMasterFrameSegmentCount(sideSign);
        int density = Mathf.Clamp(subsegmentDensity, 2, 30);

        for (int k = 0; k <= numSegments; k++)
        {
            List<Vector3> ctrlPoints = new List<Vector3>();

            foreach (float z in stations)
            {
                Vector3 pt;
                if (TryGetFramePointAtIndex(z, sideSign, k, numSegments, out pt))
                {
                    ctrlPoints.Add(pt);
                }
            }

            if (ctrlPoints.Count < 2) continue;

            if (k == numSegments)
            {
                // Force top sheer strake points to lock 100% to SheerPlan Main Sheer height
                for (int i = 0; i < ctrlPoints.Count; i++)
                {
                    Vector3 cp = ctrlPoints[i];
                    cp.y = sheerPlan.GetMainSheerYAtZ(cp.z);
                    ctrlPoints[i] = cp;
                }
            }

            List<Vector3> splinePoints = StrakesPointsCatmullRom.BuildCentripetalCatmullRomSpline(ctrlPoints, density, catmullRomAlpha);

            if (k == numSegments)
            {
                for (int i = 0; i < splinePoints.Count; i++)
                {
                    Vector3 sp = splinePoints[i];
                    sp.y = sheerPlan.GetMainSheerYAtZ(sp.z);
                    splinePoints[i] = sp;
                }
            }

            strakeSplines.Add(splinePoints);
        }

        return strakeSplines;
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

        m_CachedStbdStrakes = GetAllHybridStrakes(1f);
        m_CachedPortStrakes = GetAllHybridStrakes(-1f);

        m_CachedLabelPositions.Clear();
        int numSegments = GetMasterFrameSegmentCount(1f);
        for (int k = 0; k <= numSegments; k++)
        {
            Vector3 midPt;
            if (TryGetFramePointAtIndex(0f, 1f, k, numSegments, out midPt))
            {
                m_CachedLabelPositions.Add(midPt);
            }
        }

        m_IsCacheDirty = false;
        if (framingModel != null) m_LastChangeVersion = framingModel.changeVersion;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || framingModel == null) return;

        if (!showStrakes) return;

        int currentVersion = framingModel.changeVersion;
        if (m_IsCacheDirty || m_LastChangeVersion != currentVersion || m_CachedStbdStrakes == null)
        {
            RebuildCache();
        }

        if (showStarboard) DrawCachedStrakes(m_CachedStbdStrakes, 1f);
        if (showPort) DrawCachedStrakes(m_CachedPortStrakes, -1f);
    }

    private void DrawCachedStrakes(List<List<Vector3>> strakes, float side)
    {
        if (strakes == null) return;
        int totalStrakes = strakes.Count;

        for (int k = 0; k < totalStrakes; k++)
        {
            Color c = strakeColor;
            if (k == 0) c = garboardStrakeColor;
            else if (k == totalStrakes - 1) c = sheerStrakeColor;

            Gizmos.color = c;
            List<Vector3> pts = strakes[k];

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Gizmos.DrawLine(pts[i], pts[i + 1]);
            }
        }

#if UNITY_EDITOR
        if (showLabels && side > 0 && m_CachedLabelPositions.Count == totalStrakes)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.fontSize = 10;
            labelStyle.fontStyle = FontStyle.Bold;

            for (int k = 0; k < totalStrakes; k++)
            {
                Vector3 midPt = m_CachedLabelPositions[k];
                string text = $"Strake {k + 1}";
                if (k == 0) text += " (Garboard)";
                else if (k == totalStrakes - 1) text += " (Main Sheer)";

                Color c = strakeColor;
                if (k == 0) c = garboardStrakeColor;
                else if (k == totalStrakes - 1) c = sheerStrakeColor;

                labelStyle.normal.textColor = c;
                UnityEditor.Handles.Label(midPt + new Vector3(0.08f, 0, 0), text, labelStyle);
            }
        }
#endif
    }
}
