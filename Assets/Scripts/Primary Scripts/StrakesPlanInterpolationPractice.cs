using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Practice script for generating longitudinal hull strakes directly from the 3 Plans
/// (SheerPlan, BreadthPlan, BodyPlan) WITHOUT requiring FramingModel.
/// 
/// Rethought Architecture Rules:
/// 1. BodyPlan defines Master Frame arc length intervals (default 0.25) to derive N_master strake count,
///    2D point (X_k, Y_k) on Master Frame, and proximity weights between Keel, Floor Line, HOB, and Top Timber.
/// 2. Z/Y Elevation: 100% Horizontal strakes (Y(z) = Y_k constant across all z, NO sheer curvature).
/// 3. Z/Y Extents: Bounded by Bow Stem Profile z_Bow = SheerPlan.GetStemProfileZAtY(Y_k) and
///    Stern Profile z_Stern = SheerPlan.GetSternProfileZAtY(Y_k).
/// 4. Z/X Breadth: Interpolated between HOB, Floor Line, and Keel breadth curves,
///    converging 100% to centerline keel half-width (hw) at z_Bow and z_Stern.
/// </summary>
[ExecuteInEditMode]
public class StrakesPlanInterpolationPractice : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FashionTimberModel fashionTimberModel;

    [Header("Strake Spacing Settings")]
    [Tooltip("Target interval distance for placing points on Master Frame (default 0.25).")]
    [Range(0.01f, 2.0f)]
    public float pointInterval = 0.25f;

    [Tooltip("Longitudinal sampling steps along each horizontal strake from Stern to Bow.")]
    [Range(10, 300)]
    public int strakeResolution = 100;

    [Header("Visualization Settings")]
    [Tooltip("Display 3D horizontal strake lines in Scene View.")]
    public bool showStrakeLines = true;

    [Tooltip("Display strake index labels at midship in Scene View.")]
    public bool showLabels = true;

    [Tooltip("Show visualization on Starboard side.")]
    public bool showStarboard = true;

    [Tooltip("Show visualization on Port side.")]
    public bool showPort = true;

    [Tooltip("Color of standard hull strakes.")]
    public Color strakeColor = new Color(0.95f, 0.55f, 0.15f, 0.95f);

    [Tooltip("Color of the Main Sheer strake (topmost strake).")]
    public Color sheerStrakeColor = Color.yellow;

    [Tooltip("Color of the Garboard strake (bottommost strake along keel).")]
    public Color garboardStrakeColor = new Color(0.2f, 0.8f, 1f, 0.9f);

    private List<List<Vector3>> m_CachedStbdStrakes = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortStrakes = new List<List<Vector3>>();
    private List<Vector3> m_CachedLabelPositions = new List<Vector3>();
    private bool m_IsCacheDirty = true;

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
        if (fashionTimberModel == null) fashionTimberModel = FindFirstObjectByType<FashionTimberModel>();
    }

    /// <summary>
    /// Evaluates defining segment count N_master and Master Frame points (X_k, Y_k) from BodyPlan at z = 0.
    /// </summary>
    public int GetMasterFrameSegmentPoints(out Vector2[] masterPoints)
    {
        masterPoints = new Vector2[0];
        if (sheerPlan == null || bodyPlan == null || pointInterval <= 0.001f)
            return 12;

        bodyPlan.UpdateCachedCurves();

        Vector3 pL0 = bodyPlan.cachedL0;
        Vector3 pL1 = bodyPlan.cachedL1;
        Vector3 pLR = bodyPlan.cachedLR;
        Vector3 pL2 = bodyPlan.cachedL2;
        Vector3 pL3 = bodyPlan.cachedL3;
        Vector3 pU0 = bodyPlan.cachedU0;
        Vector3 pU1 = bodyPlan.cachedU1;
        Vector3 pU2 = bodyPlan.cachedU2;
        Vector3 pU3 = bodyPlan.cachedU3;

        const int samples = 200;
        float masterLen = 0f;
        float[] sampleDistances = new float[samples + 1];
        Vector2[] samplePts = new Vector2[samples + 1];

        // Sample strictly along lower mould curves (u = 0 to 0.5: Keel to HOB)
        Vector3 prevPt = EvaluateMasterFramePointAtU(0f, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
        samplePts[0] = new Vector2(Mathf.Abs(prevPt.x), prevPt.y);
        sampleDistances[0] = 0f;

        for (int i = 1; i <= samples; i++)
        {
            float u = (float)i / samples * 0.5f; // u in [0, 0.5] (Keel to HOB)
            Vector3 currPt = EvaluateMasterFramePointAtU(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
            Vector2 curr2D = new Vector2(Mathf.Abs(currPt.x), currPt.y);
            samplePts[i] = curr2D;
            masterLen += Vector2.Distance(samplePts[i - 1], curr2D);
            sampleDistances[i] = masterLen;
        }

        int numSegments = Mathf.Max(1, Mathf.RoundToInt(masterLen / pointInterval));
        masterPoints = new Vector2[numSegments + 1];

        for (int k = 0; k <= numSegments; k++)
        {
            float targetS = (float)k / numSegments * masterLen;
            Vector2 foundPt = samplePts[samples];

            for (int i = 0; i < samples; i++)
            {
                if (targetS >= sampleDistances[i] && targetS <= sampleDistances[i + 1])
                {
                    float segLen = sampleDistances[i + 1] - sampleDistances[i];
                    float frac = (segLen > 0.00001f) ? (targetS - sampleDistances[i]) / segLen : 0f;
                    foundPt = Vector2.Lerp(samplePts[i], samplePts[i + 1], frac);
                    break;
                }
            }
            masterPoints[k] = foundPt;
        }

        return numSegments;
    }

    /// <summary>
    /// Evaluates 3D Master Frame point at parameter u in [0, 1] on midship BodyPlan section.
    /// </summary>
    private Vector3 EvaluateMasterFramePointAtU(float u, Vector3 pL0, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3, Vector3 pU0, Vector3 pU1, Vector3 pU2, Vector3 pU3)
    {
        u = Mathf.Clamp01(u);
        if (u <= 0.5f)
        {
            float t = u * 2f;
            if (t <= 0.5f)
            {
                float localT = t * 2f;
                return CalculateQuadraticBezierPoint(localT, pL0, pL1, pLR);
            }
            else
            {
                float localT = (t - 0.5f) * 2f;
                return CalculateQuadraticBezierPoint(localT, pLR, pL2, pL3);
            }
        }
        else
        {
            float t = (u - 0.5f) * 2f;
            return CalculateCubicBezierPoint(t, pU0, pU1, pU2, pU3);
        }
    }

    /// <summary>
    /// Evaluates a 100% horizontal longitudinal strake curve for Master Frame point (X_k, Y_k) on given side.
    /// </summary>
    public List<Vector3> GetHorizontalStrakePoints(int k, float sideSign, Vector2 masterPoint, bool isTopStrake, int resolution = -1)
    {
        List<Vector3> points = new List<Vector3>();
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null) return points;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        breadthPlan.UpdateCachedCurves();

        int res = (resolution > 0) ? resolution : Mathf.Clamp(strakeResolution, 10, 300);
        float hw = bodyPlan.keelWidth / 2f;
        float yK = masterPoint.y;

        // 1. Z/Y Elevation Extents: Bounded exactly by Stem and Stern Profiles at height Y_k
        float zStern = sheerPlan.GetSternProfileZAtY(yK);
        float zBow = sheerPlan.GetStemProfileZAtY(yK);

        if (zBow <= zStern + 0.001f) return points;

        // 2. Sample 100% horizontal points along Z from zStern to zBow
        for (int i = 0; i <= res; i++)
        {
            float alpha = (float)i / res;
            float z = Mathf.Lerp(zStern, zBow, alpha);

            // Breadth X(z) with 100% centerline convergence at zStern and zBow
            float x = EvaluateHorizontalStrakeX(z, sideSign, alpha, masterPoint, hw);
            points.Add(new Vector3(x, yK, z));
        }

        return points;
    }

    /// <summary>
    /// Evaluates Z/X breadth for horizontal height Y_k, interpolating BreadthPlan shapes and converging to centerline hw at terminals.
    /// </summary>
    private float EvaluateHorizontalStrakeX(float z, float sideSign, float alpha, Vector2 masterPoint, float hw)
    {
        float yK = masterPoint.y;
        float masterX = masterPoint.x;

        float pL0_Y = bodyPlan.cachedL0.y;
        float pLR_Y = bodyPlan.cachedLR.y;
        float pL3_Y = bodyPlan.cachedL3.y;
        float pU3_Y = bodyPlan.cachedU3.y;

        float xFloor = breadthPlan.GetFloorLineXAtZ(z);
        float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(z);
        float xTop = breadthPlan.GetTopTimberXAtZ(z);

        float xCurve;

        if (yK <= pLR_Y + 0.0001f)
        {
            // Zone 1: Floor / Deadrise Zone (Keel to Floor Line)
            float denom = Mathf.Max(0.001f, pLR_Y - pL0_Y);
            float w = Mathf.Clamp01((yK - pL0_Y) / denom);
            xCurve = Mathf.Lerp(hw, xFloor, w);
        }
        else if (yK <= pL3_Y + 0.0001f)
        {
            // Zone 2: Lower Bilge Zone (Floor Line to HOB)
            float denom = Mathf.Max(0.001f, pL3_Y - pLR_Y);
            float w = Mathf.Clamp01((yK - pLR_Y) / denom);
            xCurve = Mathf.Lerp(xFloor, xHob, w);
        }
        else
        {
            // Zone 3: Upper Mould Zone (HOB to Top Timber)
            float denom = Mathf.Max(0.001f, pU3_Y - pL3_Y);
            float w = Mathf.Clamp01((yK - pL3_Y) / denom);
            xCurve = Mathf.Lerp(xHob, xTop, w);
        }

        // Centerline envelope factor S(alpha) = sin(pi * alpha)
        // S(0) = 0 (Stern terminal = hw), S(0.5) = 1.0 (Midships = full curve), S(1.0) = 0 (Bow terminal = hw)
        float envelopeFactor = Mathf.Sin(alpha * Mathf.PI);

        float xFaired = hw + envelopeFactor * (xCurve - hw);
        return sideSign * xFaired;
    }

    /// <summary>
    /// Gets all horizontal strake lines for given side.
    /// </summary>
    public List<List<Vector3>> GetAllHorizontalStrakes(float side)
    {
        List<List<Vector3>> strakes = new List<List<Vector3>>();
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null) return strakes;

        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        Vector2[] masterPoints;
        int numSegments = GetMasterFrameSegmentPoints(out masterPoints);

        for (int k = 0; k <= numSegments; k++)
        {
            bool isTopStrake = (k == numSegments);
            Vector2 mPt = masterPoints[k];
            List<Vector3> pts = GetHorizontalStrakePoints(k, sideSign, mPt, isTopStrake);
            if (pts.Count >= 2)
            {
                strakes.Add(pts);
            }
        }

        return strakes;
    }

    [ContextMenu("Rebuild Cache")]
    public void ForceRebuildCache()
    {
        MarkDirty();
    }

    private void RebuildCache()
    {
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null) return;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        breadthPlan.UpdateCachedCurves();

        m_CachedStbdStrakes = GetAllHorizontalStrakes(1f);
        m_CachedPortStrakes = GetAllHorizontalStrakes(-1f);

        m_CachedLabelPositions.Clear();
        Vector2[] masterPts;
        int numSegments = GetMasterFrameSegmentPoints(out masterPts);

        for (int k = 0; k <= numSegments; k++)
        {
            Vector2 mPt = masterPts[k];
            m_CachedLabelPositions.Add(new Vector3(mPt.x, mPt.y, 0f));
        }

        m_IsCacheDirty = false;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null) return;

        if (!showStrakeLines) return;

        if (m_IsCacheDirty || m_CachedStbdStrakes == null)
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

    private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        return (uuu * p0) + (3f * uu * t * p1) + (3f * u * tt * p2) + (ttt * p3);
    }
}
