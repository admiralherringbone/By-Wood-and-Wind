using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Strakes Model for generating and visualizing realistic hull planking strakes bent along the 
/// Framing Model (Sheer Plan, Breadth Plan, Body Plan) and connecting seamlessly to the Stem 
/// at the bow and Fashion Timbers at the stern without singularities or artificial dipping.
/// </summary>
[ExecuteInEditMode]
public class StrakesModel : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FashionTimberModel fashionTimberModel;

    [Header("Strake Layout Settings")]
    [Tooltip("Number of strakes running longitudinally along each side of the hull.")]
    [Range(2, 50)]
    public int strakesPerSide = 12;

    [Tooltip("Number of longitudinal sampling steps along each strake from Stern (Fashion Timbers) to Bow (Stem).")]
    [Range(10, 300)]
    public int strakeResolution = 100;

    [Tooltip("Align top strakes to follow the sweep of the Main Sheer Line.")]
    public bool followMainSheer = true;

    [Tooltip("Space strakes uniformly by arc length (girth) along frame contours to ensure realistic planking and avoid singularities.")]
    public bool useArcLengthSpacing = true;

    [Header("Visualization Settings")]
    [Tooltip("Display 3D strake lines in Scene View.")]
    public bool showStrakes = true;

    [Tooltip("Display strake index labels at midship in Scene View.")]
    public bool showLabels = true;

    [Tooltip("Color of standard hull strakes.")]
    public Color strakeColor = new Color(0.9f, 0.6f, 0.2f, 0.9f);

    [Tooltip("Color of the Main Sheer strake (topmost strake).")]
    public Color sheerStrakeColor = Color.yellow;

    [Tooltip("Color of the Garboard strake (bottommost strake along keel).")]
    public Color garboardStrakeColor = new Color(0.2f, 0.8f, 1f, 0.9f);

    private List<List<Vector3>> m_CachedStbdStrakes = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortStrakes = new List<List<Vector3>>();
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
        if (sheerPlan == null)
        {
            sheerPlan = GetComponentInParent<SheerPlan>();
            if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        }

        if (bodyPlan == null)
        {
            bodyPlan = GetComponentInParent<BodyPlan>();
            if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        }

        if (breadthPlan == null)
        {
            breadthPlan = GetComponentInParent<BreadthPlan>();
            if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
        }

        if (framingModel == null)
        {
            framingModel = GetComponentInParent<FramingModel>();
            if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();
        }

        if (fashionTimberModel == null)
        {
            fashionTimberModel = GetComponentInParent<FashionTimberModel>();
            if (fashionTimberModel == null) fashionTimberModel = FindFirstObjectByType<FashionTimberModel>();
        }
    }

    /// <summary>
    /// Gets all 3D sample points along a specific strake index from Stern to Bow.
    /// </summary>
    public List<Vector3> GetStrakePoints(int strakeIndex, float side, int resolution = -1)
    {
        FindReferences();
        List<Vector3> points = new List<Vector3>();

        if (sheerPlan == null || bodyPlan == null || breadthPlan == null || framingModel == null || fashionTimberModel == null)
            return points;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        breadthPlan.UpdateCachedCurves();

        int res = (resolution > 0) ? resolution : Mathf.Clamp(strakeResolution, 10, 300);
        int totalStrakes = Mathf.Max(2, strakesPerSide);
        strakeIndex = Mathf.Clamp(strakeIndex, 0, totalStrakes - 1);
        float t = (totalStrakes > 1) ? (float)strakeIndex / (totalStrakes - 1) : 0f;
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);

        // 1. Calculate EXACT unique terminal point on Fashion Timber at Stern (anchored to SheerPlan's Main Sheer Stern Intersect)
        float ySternBot = sheerPlan.cachedKeelSternPoint.y;
        float ySternTop = sheerPlan.cachedMainSheerSternIntersect.y;
        float yStern = Mathf.Lerp(ySternBot, ySternTop, t);
        float zStern = (t >= 0.9999f) ? sheerPlan.cachedMainSheerSternIntersect.z : sheerPlan.GetSternProfileZAtY(yStern);
        float xStern = sideSign * fashionTimberModel.GetNaturalHullXAtYZ(yStern, zStern);
        Vector3 pStern = new Vector3(xStern, yStern, zStern);

        // 2. Calculate EXACT unique terminal point on Stem at Bow (anchored to SheerPlan's Main Sheer Bow Intersect)
        float yBowBot = sheerPlan.cachedKeelBowPoint.y;
        float yBowTop = sheerPlan.cachedMainSheerBowIntersect.y;
        float yBow = Mathf.Lerp(yBowBot, yBowTop, t);
        float zBow = (t >= 0.9999f) ? sheerPlan.cachedMainSheerBowIntersect.z : sheerPlan.GetStemProfileZAtY(yBow);
        float xBow = sideSign * framingModel.GetKeelHalfWidth();
        Vector3 pBow = new Vector3(xBow, yBow, zBow);

        // 3. Sample longitudinal stations from zStern to zBow specifically tailored for strake t
        for (int i = 0; i <= res; i++)
        {
            float alpha = (float)i / res;
            Vector3 pt = EvaluateStrakePoint(strakeIndex, sideSign, t, alpha, pStern, pBow, zStern, zBow);
            points.Add(pt);
        }

        return points;
    }

    /// <summary>
    /// Gets points for all strakes on the given side.
    /// </summary>
    public List<List<Vector3>> GetAllStrakes(float side, int resolution = -1)
    {
        List<List<Vector3>> strakes = new List<List<Vector3>>();
        int count = Mathf.Max(2, strakesPerSide);
        for (int k = 0; k < count; k++)
        {
            strakes.Add(GetStrakePoints(k, side, resolution));
        }
        return strakes;
    }

    /// <summary>
    /// Evaluates the 3D point of a strake at normalized longitudinal parameter alpha in [0, 1] (0 = Stern/Fashion Timber, 1 = Bow/Stem).
    /// </summary>
    public Vector3 GetStrakePointAtT(int strakeIndex, float side, float alpha)
    {
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null || framingModel == null || fashionTimberModel == null)
            return Vector3.zero;

        int totalStrakes = Mathf.Max(2, strakesPerSide);
        strakeIndex = Mathf.Clamp(strakeIndex, 0, totalStrakes - 1);
        float t = (totalStrakes > 1) ? (float)strakeIndex / (totalStrakes - 1) : 0f;
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);

        float ySternBot = sheerPlan.cachedKeelSternPoint.y;
        float ySternTop = sheerPlan.cachedMainSheerSternIntersect.y;
        float yStern = Mathf.Lerp(ySternBot, ySternTop, t);
        float zStern = (t >= 0.9999f) ? sheerPlan.cachedMainSheerSternIntersect.z : sheerPlan.GetSternProfileZAtY(yStern);
        float xStern = sideSign * fashionTimberModel.GetNaturalHullXAtYZ(yStern, zStern);
        Vector3 pStern = new Vector3(xStern, yStern, zStern);

        float yBowBot = sheerPlan.cachedKeelBowPoint.y;
        float yBowTop = sheerPlan.cachedMainSheerBowIntersect.y;
        float yBow = Mathf.Lerp(yBowBot, yBowTop, t);
        float zBow = (t >= 0.9999f) ? sheerPlan.cachedMainSheerBowIntersect.z : sheerPlan.GetStemProfileZAtY(yBow);
        float xBow = sideSign * framingModel.GetKeelHalfWidth();
        Vector3 pBow = new Vector3(xBow, yBow, zBow);

        return EvaluateStrakePoint(strakeIndex, sideSign, t, alpha, pStern, pBow, zStern, zBow);
    }

    private Vector3 EvaluateStrakePoint(int strakeIndex, float sideSign, float t, float alpha, Vector3 pStern, Vector3 pBow, float zStern, float zBow)
    {
        if (alpha <= 0.0001f) return pStern;
        if (alpha >= 0.9999f) return pBow;

        float z = Mathf.Lerp(zStern, zBow, alpha);
        return SampleFramePointAtStrakeFraction(z, sideSign, t);
    }

    /// <summary>
    /// Evaluates the 3D point of a strake at exact station Z along the hull.
    /// </summary>
    public Vector3 GetStrakePointAtZ(int strakeIndex, float side, float z)
    {
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null || framingModel == null || fashionTimberModel == null)
            return Vector3.zero;

        int totalStrakes = Mathf.Max(2, strakesPerSide);
        strakeIndex = Mathf.Clamp(strakeIndex, 0, totalStrakes - 1);
        float t = (totalStrakes > 1) ? (float)strakeIndex / (totalStrakes - 1) : 0f;
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);

        float ySternBot = sheerPlan.cachedKeelSternPoint.y;
        float ySternTop = sheerPlan.cachedMainSheerSternIntersect.y;
        float yStern = Mathf.Lerp(ySternBot, ySternTop, t);
        float zStern = (t >= 0.9999f) ? sheerPlan.cachedMainSheerSternIntersect.z : sheerPlan.GetSternProfileZAtY(yStern);

        float yBowBot = sheerPlan.cachedKeelBowPoint.y;
        float yBowTop = sheerPlan.cachedMainSheerBowIntersect.y;
        float yBow = Mathf.Lerp(yBowBot, yBowTop, t);
        float zBow = (t >= 0.9999f) ? sheerPlan.cachedMainSheerBowIntersect.z : sheerPlan.GetStemProfileZAtY(yBow);

        if (Mathf.Abs(zBow - zStern) < 0.0001f) return SampleFramePointAtStrakeFraction(z, sideSign, t);

        float alpha = Mathf.Clamp01((z - zStern) / (zBow - zStern));
        return GetStrakePointAtT(strakeIndex, sideSign, alpha);
    }

    private Vector3 SampleFramePointAtStrakeFraction(float z, float side, float t)
    {
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float hw = (framingModel != null) ? framingModel.GetKeelHalfWidth() : 0.25f;

        float keelY = sheerPlan.GetKeelYAtZ(z);
        float sheerY = sheerPlan.GetMainSheerYAtZ(z);

        Vector3 pL0 = Vector3.zero, pL1 = Vector3.zero, pLR = Vector3.zero, pL2 = Vector3.zero, pL3 = Vector3.zero;
        Vector3 pU0 = Vector3.zero, pU1 = Vector3.zero, pU2 = Vector3.zero, pU3 = Vector3.zero;
        bool hasFrame = (framingModel != null) && framingModel.GetFrameControlPointsAtZ(z, sideSign, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3);

        // =========================================================================================
        // LOCKED CORE LOGIC: MAIN SHEER STRAKE (t >= 0.9999f)
        // Directly samples the frame contour at exact sheerY height. Y is locked to sheerPlan.GetMainSheerYAtZ(z).
        // =========================================================================================
        if (t >= 0.9999f)
        {
            if (hasFrame)
            {
                Vector3 sheerPt = framingModel.GetTransverseFramePointAtYFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, sheerY);
                sheerPt.y = sheerY;
                return sheerPt;
            }
            else
            {
                float breadth = (breadthPlan != null) ? breadthPlan.GetTheoreticalBreadthXAtZ(z) : hw;
                return new Vector3(sideSign * breadth, sheerY, z);
            }
        }

        if (!hasFrame)
        {
            float breadth = breadthPlan.GetTheoreticalBreadthXAtZ(z);
            float targetY = Mathf.Lerp(keelY, sheerY, t);
            float x = sideSign * Mathf.Lerp(hw, breadth, t);
            return new Vector3(x, targetY, z);
        }

        // Determine top u parameter on upper mould corresponding to Main Sheer Line height
        float uSheer = 1.0f;
        if (followMainSheer && sheerY < pU3.y)
        {
            if (sheerY >= pL3.y)
            {
                float minU = 0.5f, maxU = 1.0f;
                for (int i = 0; i < 8; i++)
                {
                    float midU = (minU + maxU) * 0.5f;
                    Vector3 pt = framingModel.EvaluateFramePointFromControlPoints(midU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                    if (pt.y < sheerY) minU = midU;
                    else maxU = midU;
                }
                uSheer = (minU + maxU) * 0.5f;
            }
            else
            {
                float minU = 0f, maxU = 0.5f;
                for (int i = 0; i < 8; i++)
                {
                    float midU = (minU + maxU) * 0.5f;
                    Vector3 pt = framingModel.EvaluateFramePointFromControlPoints(midU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                    if (pt.y < sheerY) minU = midU;
                    else maxU = midU;
                }
                uSheer = (minU + maxU) * 0.5f;
            }
        }

        // Determine bottom u parameter on lower mould corresponding to Keel height
        float uKeel = 0f;
        if (keelY > pL0.y + 0.001f)
        {
            float minU = 0f, maxU = (uSheer > 0.5f) ? 0.5f : uSheer;
            for (int i = 0; i < 8; i++)
            {
                float midU = (minU + maxU) * 0.5f;
                Vector3 pt = framingModel.EvaluateFramePointFromControlPoints(midU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                if (pt.y < keelY) minU = midU;
                else maxU = midU;
            }
            uKeel = (minU + maxU) * 0.5f;
        }

        Vector3 framePt;
        if (useArcLengthSpacing)
        {
            const int samples = 15;
            float[] cumDistances = new float[samples + 1];
            Vector3[] sampledPts = new Vector3[samples + 1];

            Vector3 prev = framingModel.EvaluateFramePointFromControlPoints(uKeel, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
            sampledPts[0] = prev;
            cumDistances[0] = 0f;
            float totalDist = 0f;

            for (int i = 1; i <= samples; i++)
            {
                float u = Mathf.Lerp(uKeel, uSheer, (float)i / samples);
                Vector3 curr = framingModel.EvaluateFramePointFromControlPoints(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                sampledPts[i] = curr;
                totalDist += Vector3.Distance(prev, curr);
                cumDistances[i] = totalDist;
                prev = curr;
            }

            if (totalDist < 0.0001f) framePt = sampledPts[0];
            else
            {
                float targetDist = t * totalDist;
                framePt = sampledPts[samples];
                for (int i = 0; i < samples; i++)
                {
                    if (targetDist >= cumDistances[i] && targetDist <= cumDistances[i + 1])
                    {
                        float segLen = cumDistances[i + 1] - cumDistances[i];
                        float frac = (segLen > 0.00001f) ? (targetDist - cumDistances[i]) / segLen : 0f;
                        framePt = Vector3.Lerp(sampledPts[i], sampledPts[i + 1], frac);
                        break;
                    }
                }
            }
        }
        else
        {
            float targetY = Mathf.Lerp(keelY, sheerY, t);
            framePt = framingModel.GetTransverseFramePointAtYFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, targetY);
        }

        return framePt;
    }


    [ContextMenu("Rebuild Strakes Cache")]
    public void ForceRebuildCache()
    {
        m_IsCacheDirty = true;
    }

    private List<Vector3> m_CachedLabelPositions = new List<Vector3>();

    private void RebuildStrakeCache()
    {
        m_CachedStbdStrakes = GetAllStrakes(1f);
        m_CachedPortStrakes = GetAllStrakes(-1f);

        m_CachedLabelPositions.Clear();
        int totalStrakes = Mathf.Max(2, strakesPerSide);
        for (int k = 0; k < totalStrakes; k++)
        {
            m_CachedLabelPositions.Add(GetStrakePointAtZ(k, 1f, 0f));
        }

        m_IsCacheDirty = false;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null || framingModel == null || fashionTimberModel == null)
            return;

        if (!showStrakes) return;

        if (m_IsCacheDirty || m_CachedStbdStrakes == null || m_CachedStbdStrakes.Count != strakesPerSide)
        {
            sheerPlan.UpdateCachedValues();
            bodyPlan.UpdateCachedCurves();
            breadthPlan.UpdateCachedCurves();

            RebuildStrakeCache();
        }

        DrawCachedStrakes(m_CachedStbdStrakes, 1f);
        DrawCachedStrakes(m_CachedPortStrakes, -1f);
    }


    private void DrawCachedStrakes(List<List<Vector3>> strakes, float side)
    {
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


