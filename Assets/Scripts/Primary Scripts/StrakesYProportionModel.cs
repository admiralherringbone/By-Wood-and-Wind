using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Two-Group Y-Proportion Strakes Model.
/// 
/// Transition Architecture:
/// Group 1 (Upper Strakes): Main Sheer Line down to Rising Line
/// Group 2 (Lower Strakes): Rising Line down to Keel Line
/// 
/// Solves the strake wrinkling and bow kinking problem by replacing 2D section arc-length
/// parameterization with normalized Y-height parameterization inside each zone.
/// 
/// 1. Group 1 evaluates normalized height proportions u_i in [0, 1] between Rising Line and Main Sheer.
/// 2. Group 2 evaluates normalized height proportions w_j in [0, 1] between Keel Line and Rising Line.
/// 3. Seam at Rising Line (u_0 and w_max) produces a 100% locked 3D ribband along the Rising Line.
/// 4. Intersects targetY with 3D frame sections using FramingModel.GetTransverseFramePointAtY()
///    to produce true 3D ribbands on the outer surface of the frames.
/// 5. Threads Centripetal Catmull-Rom splines across station points for high-density visual smoothing.
/// </summary>
[ExecuteInEditMode]
public class StrakesYProportionModel : MonoBehaviour
{
    public enum SpacingMode
    {
        MidshipArcLength, // Uses arc-length on midship master frame to define natural plank width Y proportions
        UniformY          // Simple uniform vertical height proportions within each group
    }

    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FashionTimberModel fashionTimberModel;

    [Header("Strake Layout Settings")]
    [Tooltip("Method for establishing normalized Y height proportions within each group.")]
    public SpacingMode spacingMode = SpacingMode.MidshipArcLength;

    [Tooltip("Target interval distance for placing points on Master Frame at z=0 (used in MidshipArcLength mode).")]
    [Range(0.01f, 2.0f)]
    public float pointInterval = 0.25f;

    [Tooltip("Fixed number of strakes in Group 1 (Main Sheer down to Rising Line, used in UniformY mode).")]
    [Range(1, 40)]
    public int upperGroupStrakeCount = 8;

    [Tooltip("Fixed number of strakes in Group 2 (Rising Line down to Keel Line, used in UniformY mode).")]
    [Range(1, 40)]
    public int lowerGroupStrakeCount = 6;

    [Tooltip("Lock topmost strake of Group 1 100% to SheerPlan Main Sheer Line height.")]
    public bool lockTopStrakeToSheer = true;

    [Tooltip("Lock bottommost strake of Group 2 100% to Keel line.")]
    public bool lockBottomStrakeToKeel = true;

    [Tooltip("Omit virtual points if target Y falls below physical frame bottom at stem/stern (drop strakes).")]
    public bool filterVirtualBelowKeel = true;

    [Header("Spline Fairing Settings")]
    [Tooltip("Enable Centripetal Catmull-Rom longitudinal spline fairing across frame stations.")]
    public bool enableCatmullRomFairing = true;

    [Tooltip("Number of sub-segment evaluation steps between adjacent frame stations along each strake spline.")]
    [Range(2, 30)]
    public int subsegmentDensity = 10;

    [Tooltip("Centripetal parameter alpha (0.5 = Centripetal, 0.0 = Uniform, 1.0 = Chordal). Centripetal (0.5) avoids loops & overshooting.")]
    [Range(0f, 1f)]
    public float catmullRomAlpha = 0.5f;

    [Header("Visualization Settings")]
    [Tooltip("Display 3D faired strake lines in Scene View.")]
    public bool showStrakes = true;

    [Tooltip("Display raw frame station anchor points (cyan spheres) to inspect raw smoothness before spline fairing.")]
    public bool showRawFramePoints = false;

    [Tooltip("Radius of raw frame station point spheres.")]
    [Range(0.01f, 0.2f)]
    public float pointRadius = 0.035f;

    [Tooltip("Display strake index labels at midship in Scene View.")]
    public bool showLabels = true;

    [Tooltip("Show visualization on Starboard side.")]
    public bool showStarboard = true;

    [Tooltip("Show visualization on Port side.")]
    public bool showPort = true;

    [Header("Strake Group Colors")]
    [Tooltip("Color of Group 1 Upper Strakes (Main Sheer to Rising Line).")]
    public Color upperGroupColor = new Color(0.9f, 0.6f, 0.2f, 0.9f);

    [Tooltip("Color of Group 2 Lower Strakes (Rising Line to Keel).")]
    public Color lowerGroupColor = new Color(0.3f, 0.7f, 0.9f, 0.9f);

    [Tooltip("Color of the Rising Line boundary seam strake.")]
    public Color risingSeamColor = new Color(0.1f, 0.95f, 0.3f, 0.95f);

    [Tooltip("Color of the Main Sheer strake (topmost strake).")]
    public Color sheerStrakeColor = Color.yellow;

    [Tooltip("Color of the Garboard strake (bottommost strake along keel).")]
    public Color garboardStrakeColor = new Color(0.2f, 0.8f, 1f, 0.95f);

    [Tooltip("Color of raw frame station anchor points.")]
    public Color pointColor = Color.cyan;

    // Data structures for caching
    public class StrakeData
    {
        public int groupIndex; // 1 = Upper (Sheer->Rising), 2 = Lower (Rising->Keel)
        public int indexInGroup;
        public int totalInGroup;
        public float localProportion; // 0 to 1 inside zone
        public string label;
        public List<Vector3> fairedPoints = new List<Vector3>();
        public List<Vector3> rawPoints = new List<Vector3>();
        public Vector3 midshipLabelPos;
    }

    private List<StrakeData> m_CachedStbdStrakesData = new List<StrakeData>();
    private List<StrakeData> m_CachedPortStrakesData = new List<StrakeData>();
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
    /// Computes local normalized height proportions for Group 1 (Upper: Main Sheer down to Rising Line)
    /// and Group 2 (Lower: Rising Line down to Keel Line).
    /// </summary>
    public void GetGroupProportions(float sideSign, out List<float> upperProps, out List<float> lowerProps)
    {
        upperProps = new List<float>();
        lowerProps = new List<float>();

        if (sheerPlan == null || framingModel == null)
        {
            for (int i = 0; i <= 6; i++) upperProps.Add((float)i / 6f);
            for (int i = 0; i <= 6; i++) lowerProps.Add((float)i / 6f);
            return;
        }

        if (spacingMode == SpacingMode.UniformY)
        {
            int uCount = Mathf.Max(1, upperGroupStrakeCount);
            for (int i = 0; i <= uCount; i++) upperProps.Add((float)i / uCount);

            int lCount = Mathf.Max(1, lowerGroupStrakeCount);
            for (int i = 0; i <= lCount; i++) lowerProps.Add((float)i / lCount);
            return;
        }

        // Midship Arc-Length Mode:
        // Sample Master Frame at z = 0 separately for Upper and Lower zones
        float masterZ = 0f;
        float keelY = sheerPlan.cachedMidshipKeel.y;
        float risingY = sheerPlan.GetRisingLineYAtZ(masterZ);
        float sheerY = sheerPlan.GetMainSheerYAtZ(masterZ);

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        bool hasFrame = framingModel.GetFrameControlPointsAtZ(masterZ, sideSign, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3);

        if (!hasFrame)
        {
            for (int i = 0; i <= 6; i++) upperProps.Add((float)i / 6f);
            for (int i = 0; i <= 6; i++) lowerProps.Add((float)i / 6f);
            return;
        }

        // Build Master Frame arc polylines for Upper Zone (Rising -> Sheer) and Lower Zone (Keel -> Rising)
        List<Vector3> upperPolyline = SampleFramePolylineBetweenHeights(masterZ, sideSign, risingY, sheerY, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
        List<Vector3> lowerPolyline = SampleFramePolylineBetweenHeights(masterZ, sideSign, keelY, risingY, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);

        upperProps = ArcLengthProportionsFromPolyline(upperPolyline, risingY, sheerY, pointInterval);
        lowerProps = ArcLengthProportionsFromPolyline(lowerPolyline, keelY, risingY, pointInterval);
    }

    private List<Vector3> SampleFramePolylineBetweenHeights(float z, float sideSign, float minY, float maxY, Vector3 pL0, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3, Vector3 pU0, Vector3 pU1, Vector3 pU2, Vector3 pU3)
    {
        List<Vector3> polyline = new List<Vector3>();
        if (maxY <= minY + 0.0001f) return polyline;

        float hw = framingModel.GetKeelHalfWidth();
        float frameBottomY = pL0.y;

        // Vertical keel drop segment if applicable
        if (minY < frameBottomY && maxY > minY)
        {
            float topVertY = Mathf.Min(maxY, frameBottomY);
            const int vertSamples = 10;
            for (int i = 0; i <= vertSamples; i++)
            {
                float vy = Mathf.Lerp(minY, topVertY, (float)i / vertSamples);
                polyline.Add(new Vector3(sideSign * hw, vy, z));
            }
        }

        if (maxY > frameBottomY)
        {
            float uMin = GetUAtY(minY, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
            float uMax = GetUAtY(maxY, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);

            const int frameSamples = 80;
            int startIdx = (polyline.Count > 0) ? 1 : 0;
            for (int i = startIdx; i <= frameSamples; i++)
            {
                float u = Mathf.Lerp(uMin, uMax, (float)i / frameSamples);
                Vector3 pt = framingModel.EvaluateFramePointFromControlPoints(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                polyline.Add(pt);
            }
        }

        return polyline;
    }

    private List<float> ArcLengthProportionsFromPolyline(List<Vector3> polyline, float minY, float maxY, float interval)
    {
        List<float> props = new List<float>();
        if (polyline.Count < 2 || maxY <= minY + 0.0001f)
        {
            for (int i = 0; i <= 6; i++) props.Add((float)i / 6f);
            return props;
        }

        float totalArcLen = 0f;
        float[] cumDist = new float[polyline.Count];
        cumDist[0] = 0f;
        for (int i = 1; i < polyline.Count; i++)
        {
            totalArcLen += Vector3.Distance(polyline[i - 1], polyline[i]);
            cumDist[i] = totalArcLen;
        }

        int segCount = Mathf.Max(1, Mathf.RoundToInt(totalArcLen / Mathf.Max(0.01f, interval)));

        for (int k = 0; k <= segCount; k++)
        {
            if (k == 0) { props.Add(0f); continue; }
            if (k == segCount) { props.Add(1f); continue; }

            float targetS = (float)k / segCount * totalArcLen;
            Vector3 samplePt = polyline[polyline.Count - 1];

            for (int i = 0; i < polyline.Count - 1; i++)
            {
                if (targetS >= cumDist[i] && targetS <= cumDist[i + 1])
                {
                    float segLen = cumDist[i + 1] - cumDist[i];
                    float frac = (segLen > 0.00001f) ? (targetS - cumDist[i]) / segLen : 0f;
                    samplePt = Vector3.Lerp(polyline[i], polyline[i + 1], frac);
                    break;
                }
            }

            float normY = Mathf.Clamp01((samplePt.y - minY) / (maxY - minY));
            props.Add(normY);
        }

        return props;
    }

    /// <summary>
    /// Evaluates 3D frame point at station Z for target group and local proportion (0 <= localProp <= 1).
    /// Group 1 = Upper (Rising Line -> Main Sheer)
    /// Group 2 = Lower (Keel Line -> Rising Line)
    /// </summary>
    public bool TryGetFramePointInGroup(float z, float sideSign, int groupIndex, float localProp, out Vector3 point)
    {
        point = Vector3.zero;
        if (sheerPlan == null || framingModel == null) return false;

        float keelY = framingModel.GetFrameBottomYAtZ(z);
        float risingY = sheerPlan.GetRisingLineYAtZ(z);
        float sheerY = sheerPlan.GetMainSheerYAtZ(z);

        float targetY = 0f;

        if (groupIndex == 1) // Upper Group: Rising Line to Main Sheer Line
        {
            if (lockTopStrakeToSheer && Mathf.Approximately(localProp, 1.0f))
            {
                Vector3 sheerPt = framingModel.GetTransverseFramePointAtY(z, sideSign, sheerY);
                sheerPt.y = sheerY;
                point = sheerPt;
                return true;
            }
            targetY = Mathf.Lerp(risingY, sheerY, localProp);
        }
        else // Lower Group: Keel Line to Rising Line
        {
            if (lockBottomStrakeToKeel && Mathf.Approximately(localProp, 0f))
            {
                Vector3 bottomPt = framingModel.GetTransverseFramePointAtY(z, sideSign, keelY);
                bottomPt.y = keelY;
                point = bottomPt;
                return true;
            }
            targetY = Mathf.Lerp(keelY, risingY, localProp);
        }

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        bool hasFrame = framingModel.GetFrameControlPointsAtZ(z, sideSign, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3);

        if (!hasFrame) return false;

        float frameBottomY = pL0.y;
        if (filterVirtualBelowKeel && targetY < frameBottomY - 0.05f)
        {
            return false; // Point falls below physical frame bottom at stem/stern (drop strake)
        }

        point = framingModel.GetTransverseFramePointAtYFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, targetY);
        return true;
    }

    /// <summary>
    /// Evaluates all strakes divided into Group 1 (Upper) and Group 2 (Lower) for given side.
    /// </summary>
    public List<StrakeData> EvaluateAllStrakes(float side)
    {
        List<StrakeData> result = new List<StrakeData>();
        if (sheerPlan == null || framingModel == null) return result;

        List<float> stations = framingModel.CalculateFrameStations();
        if (stations == null || stations.Count == 0) return result;

        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        List<float> upperProps, lowerProps;
        GetGroupProportions(sideSign, out upperProps, out lowerProps);
        int density = Mathf.Clamp(subsegmentDensity, 2, 30);

        // --- GROUP 2: LOWER STRAKES (Rising Line down to Keel Line) ---
        // Evaluate from Garboard (j=0) up to Rising Line (j=lowerProps.Count - 1)
        for (int j = 0; j < lowerProps.Count; j++)
        {
            float w = lowerProps[j];
            StrakeData strake = new StrakeData();
            strake.groupIndex = 2;
            strake.indexInGroup = j;
            strake.totalInGroup = lowerProps.Count;
            strake.localProportion = w;

            if (j == 0) strake.label = "Garboard (Keel)";
            else if (j == lowerProps.Count - 1) strake.label = "Rising Line Seam";
            else strake.label = $"Lower Strake L-{j}";

            foreach (float z in stations)
            {
                Vector3 pt;
                if (TryGetFramePointInGroup(z, sideSign, 2, w, out pt))
                {
                    strake.rawPoints.Add(pt);
                }
            }

            if (strake.rawPoints.Count >= 2)
            {
                if (enableCatmullRomFairing && strake.rawPoints.Count >= 3)
                {
                    strake.fairedPoints = StrakesPointsCatmullRom.BuildCentripetalCatmullRomSpline(strake.rawPoints, density, catmullRomAlpha);
                }
                else
                {
                    strake.fairedPoints = new List<Vector3>(strake.rawPoints);
                }

                TryGetFramePointInGroup(0f, sideSign, 2, w, out strake.midshipLabelPos);
                result.Add(strake);
            }
        }

        // --- GROUP 1: UPPER STRAKES (Main Sheer Line down to Rising Line) ---
        // Evaluate from Rising Line (i=0) up to Main Sheer Line (i=upperProps.Count - 1)
        // Skip i=0 if lower group already produced the Rising Line seam to avoid duplicate overlapping spline
        int startUpper = 1; 
        for (int i = startUpper; i < upperProps.Count; i++)
        {
            float u = upperProps[i];
            StrakeData strake = new StrakeData();
            strake.groupIndex = 1;
            strake.indexInGroup = i;
            strake.totalInGroup = upperProps.Count;
            strake.localProportion = u;

            if (i == upperProps.Count - 1) strake.label = "Main Sheer Strake";
            else strake.label = $"Upper Strake U-{i}";

            foreach (float z in stations)
            {
                Vector3 pt;
                if (TryGetFramePointInGroup(z, sideSign, 1, u, out pt))
                {
                    strake.rawPoints.Add(pt);
                }
            }

            if (strake.rawPoints.Count >= 2)
            {
                if (lockTopStrakeToSheer && i == upperProps.Count - 1)
                {
                    for (int idx = 0; idx < strake.rawPoints.Count; idx++)
                    {
                        Vector3 cp = strake.rawPoints[idx];
                        cp.y = sheerPlan.GetMainSheerYAtZ(cp.z);
                        strake.rawPoints[idx] = cp;
                    }
                }

                if (enableCatmullRomFairing && strake.rawPoints.Count >= 3)
                {
                    strake.fairedPoints = StrakesPointsCatmullRom.BuildCentripetalCatmullRomSpline(strake.rawPoints, density, catmullRomAlpha);
                }
                else
                {
                    strake.fairedPoints = new List<Vector3>(strake.rawPoints);
                }

                if (lockTopStrakeToSheer && i == upperProps.Count - 1)
                {
                    for (int idx = 0; idx < strake.fairedPoints.Count; idx++)
                    {
                        Vector3 sp = strake.fairedPoints[idx];
                        sp.y = sheerPlan.GetMainSheerYAtZ(sp.z);
                        strake.fairedPoints[idx] = sp;
                    }
                }

                TryGetFramePointInGroup(0f, sideSign, 1, u, out strake.midshipLabelPos);
                result.Add(strake);
            }
        }

        return result;
    }

    private float GetUAtY(float targetY, Vector3 pL0, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3, Vector3 pU0, Vector3 pU1, Vector3 pU2, Vector3 pU3)
    {
        if (targetY >= pU3.y) return 1.0f;
        if (targetY <= pL0.y) return 0.0f;

        float minU = 0f, maxU = 1.0f;
        for (int i = 0; i < 15; i++)
        {
            float midU = (minU + maxU) * 0.5f;
            Vector3 pt = framingModel.EvaluateFramePointFromControlPoints(midU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
            if (pt.y < targetY) minU = midU;
            else maxU = midU;
        }
        return (minU + maxU) * 0.5f;
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

        m_CachedStbdStrakesData = EvaluateAllStrakes(1f);
        m_CachedPortStrakesData = EvaluateAllStrakes(-1f);

        m_IsCacheDirty = false;
        if (framingModel != null) m_LastChangeVersion = framingModel.changeVersion;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || framingModel == null) return;

        int currentVersion = framingModel.changeVersion;
        if (m_IsCacheDirty || m_LastChangeVersion != currentVersion || m_CachedStbdStrakesData == null)
        {
            RebuildCache();
        }

        if (showStrakes)
        {
            if (showStarboard) DrawCachedStrakes(m_CachedStbdStrakesData, 1f);
            if (showPort) DrawCachedStrakes(m_CachedPortStrakesData, -1f);
        }

        if (showRawFramePoints)
        {
            if (showStarboard) DrawRawFramePoints(m_CachedStbdStrakesData);
            if (showPort) DrawRawFramePoints(m_CachedPortStrakesData);
        }
    }

    private void DrawCachedStrakes(List<StrakeData> strakesData, float side)
    {
        if (strakesData == null) return;

        for (int k = 0; k < strakesData.Count; k++)
        {
            StrakeData strake = strakesData[k];
            Color c = upperGroupColor;

            if (strake.groupIndex == 2)
            {
                if (strake.indexInGroup == 0) c = garboardStrakeColor;
                else if (strake.indexInGroup == strake.totalInGroup - 1) c = risingSeamColor;
                else c = lowerGroupColor;
            }
            else // Group 1
            {
                if (strake.indexInGroup == strake.totalInGroup - 1) c = sheerStrakeColor;
                else c = upperGroupColor;
            }

            Gizmos.color = c;
            List<Vector3> pts = strake.fairedPoints;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Gizmos.DrawLine(pts[i], pts[i + 1]);
            }
        }

#if UNITY_EDITOR
        if (showLabels && side > 0)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.fontSize = 10;
            labelStyle.fontStyle = FontStyle.Bold;

            for (int k = 0; k < strakesData.Count; k++)
            {
                StrakeData strake = strakesData[k];
                Color c = upperGroupColor;

                if (strake.groupIndex == 2)
                {
                    if (strake.indexInGroup == 0) c = garboardStrakeColor;
                    else if (strake.indexInGroup == strake.totalInGroup - 1) c = risingSeamColor;
                    else c = lowerGroupColor;
                }
                else
                {
                    if (strake.indexInGroup == strake.totalInGroup - 1) c = sheerStrakeColor;
                    else c = upperGroupColor;
                }

                labelStyle.normal.textColor = c;
                UnityEditor.Handles.Label(strake.midshipLabelPos + new Vector3(0.08f, 0, 0), strake.label, labelStyle);
            }
        }
#endif
    }

    private void DrawRawFramePoints(List<StrakeData> strakesData)
    {
        if (strakesData == null) return;

        Gizmos.color = pointColor;
        foreach (StrakeData strake in strakesData)
        {
            foreach (Vector3 pt in strake.rawPoints)
            {
                Gizmos.DrawSphere(pt, pointRadius);
            }
        }
    }
}
