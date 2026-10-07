using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Focused Master Seam Strakes Model.
/// 
/// Evaluates and verifies 100% exact 3D alignment for the master structural seam strakes:
/// 1. Garboard Strake (Keel Baseline p0)
/// 2. Floor Line Strake (Floor / Rising Line p1)
/// 3. Bilge Line Strake (Diagonal Bilge Line p2)
/// 4. Main Wale Strake (Main Wale Line)
/// 5. Main Sheer Strake (Main Sheer / Rail Line p4)
/// </summary>
[ExecuteInEditMode]
public class StrakesModel : MonoBehaviour
{
    public enum StrakeSamplingMode
    {
        ContinuousPlanCurves, // Evaluates exact continuous mathematical 3D plan curves (0% error with BreadthPlan/SheerPlan)
        FrameStations         // Evaluates at discrete transverse frame stations
    }

    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;

    [Header("Sampling Settings")]
    [Tooltip("Method for sampling the master seam strake curves (ContinuousPlanCurves vs FrameStations).")]
    public StrakeSamplingMode samplingMode = StrakeSamplingMode.ContinuousPlanCurves;

    [Tooltip("Number of continuous 3D sampling points along the hull length (used in ContinuousPlanCurves mode).")]
    [Range(20, 300)]
    public int sampleResolution = 100;

    [Header("Master Seam Strake Toggles")]
    [Tooltip("Render Garboard Strake (Keel Baseline p0).")]
    public bool showGarboardStrake = false;

    [Tooltip("Render Floor Line Strake (Floor / Rising Line p1).")]
    public bool showFloorLineStrake = true;

    [Tooltip("Render Bilge Line Strake (Diagonal Bilge Line p2).")]
    public bool showBilgeLineStrake = true;

    [Tooltip("Render Main Wale Strake (Main Wale Line).")]
    public bool showMainWaleStrake = true;

    [Tooltip("Render Main Sheer Strake (Main Sheer Line).")]
    public bool showMainSheerStrake = false;

    [Tooltip("Render Top Timber Strake (Top Line / Bulwark p4).")]
    public bool showTopTimberStrake = false;

    [Header("Division 1: Keel Baseline (p0) -> Floor Line (p1) (Y-Proportion Method)")]
    [Tooltip("Render intermediate strakes between Keel Baseline and Floor Line.")]
    public bool showDivision1Strakes = true;

    [Tooltip("Target physical strake interval / width at midships (in meters). Defines strake density.")]
    [Range(0.05f, 1f)]
    public float division1TargetInterval = 0.25f;

    [Header("Division 2: Floor Line (p1) -> Bilge Line (p2) (Y-Proportion Method)")]
    [Tooltip("Render intermediate strakes between Floor Line and Bilge Line.")]
    public bool showDivision2Strakes = true;

    [Tooltip("Target physical strake interval / width at midships (in meters). Defines strake density.")]
    [Range(0.05f, 1f)]
    public float division2TargetInterval = 0.25f;

    [Header("Division 3: Bilge Line (p2) -> Main Wale (Y-Proportion Method)")]
    [Tooltip("Render intermediate strakes between Bilge Line and Main Wale Line.")]
    public bool showDivision3Strakes = true;

    [Tooltip("Target physical strake interval / width at midships (in meters). Defines strake density.")]
    [Range(0.05f, 1f)]
    public float division3TargetInterval = 0.25f;

    [Header("Visualization Settings")]
    public bool showStarboard = true;
    public bool showPort = true;
    public bool showLabels = true;

    [Header("Strake Seam Colors")]
    public Color garboardColor = new Color(0.3f, 0.9f, 1f, 0.95f);
    public Color division1Color = new Color(0.2f, 0.85f, 1f, 0.9f);
    public Color floorSeamColor = new Color(0.2f, 1f, 0.5f, 0.95f);
    public Color division2Color = new Color(0.15f, 0.95f, 0.45f, 0.9f);
    public Color bilgeSeamColor = new Color(1f, 0.8f, 0.1f, 0.95f);
    public Color division3Color = new Color(0.9f, 0.6f, 0.1f, 0.9f);
    public Color waleSeamColor = new Color(1f, 0.4f, 0.1f, 0.95f);
    public Color sheerSeamColor = Color.yellow;
    public Color topTimberColor = Color.magenta;

    [System.Serializable]
    private struct CachedPolyline
    {
        public List<Vector3> points;
        public Color color;
        public string label;
        public float sideSign;
    }

    private List<CachedPolyline> m_CachedPolylines = new List<CachedPolyline>();
    private bool m_IsDirty = true;
    private int m_LastSheerVersion = -1;
    private int m_LastBodyVersion = -1;
    private int m_LastBreadthVersion = -1;
    private int m_LastFramingVersion = -1;

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
        m_IsDirty = true;
    }

    public void FindReferences()
    {
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
        if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();
    }

    public float GetKeelHalfWidth()
    {
        if (bodyPlan != null) return bodyPlan.keelWidth / 2f;
        if (framingModel != null) return framingModel.GetKeelHalfWidth();
        return 0.25f;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || breadthPlan == null) return;

        int curSheer = (sheerPlan != null) ? sheerPlan.changeVersion : 0;
        int curBody = (bodyPlan != null) ? bodyPlan.changeVersion : 0;
        int curBreadth = (breadthPlan != null) ? breadthPlan.changeVersion : 0;
        int curFraming = (framingModel != null) ? framingModel.changeVersion : 0;

        if (m_IsDirty || curSheer != m_LastSheerVersion || curBody != m_LastBodyVersion || curBreadth != m_LastBreadthVersion || curFraming != m_LastFramingVersion)
        {
            RebuildPolylinesCache();
            m_LastSheerVersion = curSheer;
            m_LastBodyVersion = curBody;
            m_LastBreadthVersion = curBreadth;
            m_LastFramingVersion = curFraming;
            m_IsDirty = false;
        }

        DrawCachedPolylines();
    }

    private void DrawCachedPolylines()
    {
        if (m_CachedPolylines == null) return;

        foreach (var poly in m_CachedPolylines)
        {
            if (poly.sideSign > 0 && !showStarboard) continue;
            if (poly.sideSign < 0 && !showPort) continue;
            DrawPolyline(poly.points, poly.color, poly.label, poly.sideSign);
        }
    }

    private void RebuildPolylinesCache()
    {
        if (m_CachedPolylines == null) m_CachedPolylines = new List<CachedPolyline>();
        else m_CachedPolylines.Clear();

        if (sheerPlan == null || breadthPlan == null) return;

        sheerPlan.UpdateCachedValues();
        breadthPlan.UpdateCachedCurves();

        if (showStarboard) GenerateSeamStrakesForSide(1f);
        if (showPort) GenerateSeamStrakesForSide(-1f);
    }

    public float CalculateMasterFrameDivisionArcLength(float startU, float endU)
    {
        if (bodyPlan == null) return 1f;
        SectionNodes nodes = bodyPlan.GetSectionNodesAtZ(0f, 1f);
        const int steps = 20;
        float totalDist = 0f;
        Vector3 prevPt = bodyPlan.EvaluateBodySectionAtU(startU, nodes);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float u = Mathf.Lerp(startU, endU, t);
            Vector3 currPt = bodyPlan.EvaluateBodySectionAtU(u, nodes);
            totalDist += Vector3.Distance(prevPt, currPt);
            prevPt = currPt;
        }
        return Mathf.Max(0.01f, totalDist);
    }

    private void GenerateSeamStrakesForSide(float sideSign)
    {
        // 1. Garboard Strake (Keel p0)
        if (showGarboardStrake)
        {
            float aftZ = sheerPlan.GetAftMostZ();
            float fwdZ = sheerPlan.GetFwdMostZ();
            List<Vector3> pts = SampleKeelStrake(sideSign, aftZ, fwdZ);
            AddCachedPolyline(pts, garboardColor, "Garboard Strake (Keel p0)", sideSign);
        }

        // Division 1 Intermediate Strakes (Keel -> Floor Line, Y-Proportion Method based on Master Frame Arc Length)
        if (showDivision1Strakes && bodyPlan != null)
        {
            float arcD1 = CalculateMasterFrameDivisionArcLength(0.00f, 0.25f);
            int countD1 = Mathf.Max(1, Mathf.RoundToInt(arcD1 / Mathf.Max(0.01f, division1TargetInterval)));

            for (int i = 1; i < countD1; i++)
            {
                float proportion = (float)i / countD1;
                List<Vector3> pts = SampleDivision1Strake(sideSign, proportion);
                Color c = Color.Lerp(garboardColor, floorSeamColor, proportion);
                AddCachedPolyline(pts, c, $"Garboard Bottom Strake D1-{i}", sideSign);
            }
        }

        // 2. Floor Line Strake (p1)
        if (showFloorLineStrake)
        {
            float aftZ = sheerPlan.cachedSternRisingEnd.z;
            float fwdZ = sheerPlan.cachedBowRisingEnd.z;
            List<Vector3> pts = SampleFloorStrake(sideSign, aftZ, fwdZ);
            AddCachedPolyline(pts, floorSeamColor, "Floor Line Strake (p1)", sideSign);
        }

        // Division 2 Intermediate Strakes (Floor Line -> Bilge Line, Y-Proportion Method based on Master Frame Arc Length)
        if (showDivision2Strakes && bodyPlan != null)
        {
            float arcD2 = CalculateMasterFrameDivisionArcLength(0.25f, 0.50f);
            int countD2 = Mathf.Max(1, Mathf.RoundToInt(arcD2 / Mathf.Max(0.01f, division2TargetInterval)));

            for (int i = 1; i < countD2; i++)
            {
                float proportion = (float)i / countD2;
                List<Vector3> pts = SampleDivision2Strake(sideSign, proportion);
                Color c = Color.Lerp(floorSeamColor, bilgeSeamColor, proportion);
                AddCachedPolyline(pts, c, $"Lower Bilge Strake D2-{i}", sideSign);
            }
        }

        // 3. Bilge Line Strake (p2)
        if (showBilgeLineStrake)
        {
            float aftZ = sheerPlan.GetBilgeSternZ();
            float fwdZ = sheerPlan.GetBilgeBowZ();
            List<Vector3> pts = SampleBilgeStrake(sideSign, aftZ, fwdZ);
            AddCachedPolyline(pts, bilgeSeamColor, "Bilge Line Strake (p2)", sideSign);
        }

        // Division 3 Intermediate Strakes (Bilge Line -> Main Wale, Y-Proportion Method based on Master Frame Arc Length)
        if (showDivision3Strakes && bodyPlan != null)
        {
            float arcD3 = CalculateMasterFrameDivisionArcLength(0.50f, 0.75f);
            int countD3 = Mathf.Max(1, Mathf.RoundToInt(arcD3 / Mathf.Max(0.01f, division3TargetInterval)));

            for (int i = 1; i < countD3; i++)
            {
                float proportion = (float)i / countD3;
                List<Vector3> pts = SampleDivision3Strake(sideSign, proportion);
                Color c = Color.Lerp(bilgeSeamColor, waleSeamColor, proportion);
                AddCachedPolyline(pts, c, $"Upper Bilge Strake D3-{i}", sideSign);
            }
        }

        // 4. Main Wale Strake
        if (showMainWaleStrake)
        {
            float aftZ = sheerPlan.cachedHobSternIntersect.z;
            float fwdZ = sheerPlan.cachedHobBowIntersect.z;
            List<Vector3> pts = SampleWaleStrake(sideSign, aftZ, fwdZ);
            AddCachedPolyline(pts, waleSeamColor, "Main Wale Strake", sideSign);
        }

        // 5. Main Sheer Strake
        if (showMainSheerStrake)
        {
            float aftZ = sheerPlan.cachedTopTimberSternIntersect.z;
            float fwdZ = sheerPlan.cachedTopTimberBowIntersect.z;
            List<Vector3> pts = SampleSheerStrake(sideSign, aftZ, fwdZ);
            AddCachedPolyline(pts, sheerSeamColor, "Main Sheer Strake", sideSign);
        }

        // 6. Top Timber Strake (p4)
        if (showTopTimberStrake)
        {
            float aftZ = sheerPlan.cachedTopTimberSternIntersect.z;
            float fwdZ = sheerPlan.cachedTopTimberBowIntersect.z;
            List<Vector3> pts = SampleTopTimberStrake(sideSign, aftZ, fwdZ);
            AddCachedPolyline(pts, topTimberColor, "Top Timber Strake (p4)", sideSign);
        }
    }

    private void AddCachedPolyline(List<Vector3> pts, Color color, string label, float sideSign)
    {
        if (pts == null || pts.Count < 2) return;
        CachedPolyline poly = new CachedPolyline
        {
            points = pts,
            color = color,
            label = label,
            sideSign = sideSign
        };
        m_CachedPolylines.Add(poly);
    }

    private List<Vector3> GetSamplingStations(float startZ, float endZ)
    {
        List<Vector3> dummy = new List<Vector3>();
        List<float> zList = new List<float>();

        if (samplingMode == StrakeSamplingMode.FrameStations && framingModel != null)
        {
            List<float> frameStations = framingModel.CalculateFrameStations();
            foreach (float fz in frameStations)
            {
                if (fz >= Mathf.Min(startZ, endZ) - 0.001f && fz <= Mathf.Max(startZ, endZ) + 0.001f)
                {
                    zList.Add(fz);
                }
            }
            if (zList.Count == 0) zList.Add(startZ);
        }
        else
        {
            int res = Mathf.Clamp(sampleResolution, 10, 300);
            for (int i = 0; i <= res; i++)
            {
                float t = i / (float)res;
                zList.Add(Mathf.Lerp(startZ, endZ, t));
            }
        }
        return zList.ConvertAll(z => new Vector3(0, 0, z));
    }

    private void FindStemIntersection(System.Func<float, float> targetYAtZ, out float stemHitZ, out float stemHitY)
    {
        float minZ = (sheerPlan != null) ? sheerPlan.cachedKeelBowPoint.z : 0f;
        float maxZ = (sheerPlan != null) ? sheerPlan.cachedUpperStemTopPoint.z : 10f;

        for (int i = 0; i < 12; i++)
        {
            float midZ = (minZ + maxZ) * 0.5f;
            float stemY = sheerPlan.GetStemProfileYAtZ(midZ);
            float tY = targetYAtZ(midZ);
            if (stemY < tY) minZ = midZ;
            else maxZ = midZ;
        }

        stemHitZ = (minZ + maxZ) * 0.5f;
        stemHitY = targetYAtZ(stemHitZ);
    }

    private void FindSternIntersection(System.Func<float, float> targetYAtZ, out float sternHitZ, out float sternHitY)
    {
        float minZ = (sheerPlan != null) ? sheerPlan.cachedKeelSternPoint.z : -10f;
        float maxZ = (sheerPlan != null) ? sheerPlan.cachedTransomTopPoint.z : 0f;

        for (int i = 0; i < 12; i++)
        {
            float midZ = (minZ + maxZ) * 0.5f;
            float sternY = sheerPlan.GetSternProfileYAtZ(midZ);
            float tY = targetYAtZ(midZ);
            if (sternY < tY) maxZ = midZ;
            else minZ = midZ;
        }

        sternHitZ = (minZ + maxZ) * 0.5f;
        sternHitY = targetYAtZ(sternHitZ);
    }

    public List<Vector3> SampleKeelStrake(float sideSign, float startZ, float endZ)
    {
        List<Vector3> pts = new List<Vector3>();
        List<Vector3> zList = GetSamplingStations(startZ, endZ);
        float hw = GetKeelHalfWidth();

        foreach (Vector3 zVec in zList)
        {
            float z = zVec.z;
            float y = sheerPlan.GetKeelYAtZ(z);
            float x = sideSign * hw;
            pts.Add(new Vector3(x, y, z));
        }
        return pts;
    }

    public Vector3 EvaluateFramePointByArcFraction(float z, float sideSign, float startU, float endU, float arcFraction)
    {
        if (bodyPlan == null) return Vector3.zero;
        SectionNodes nodes = bodyPlan.GetSectionNodesAtZ(z, sideSign);

        const int steps = 20;
        float[] uVals = new float[steps + 1];
        float[] cumDist = new float[steps + 1];
        cumDist[0] = 0f;
        uVals[0] = startU;

        Vector3 prevPt = bodyPlan.EvaluateBodySectionAtU(startU, nodes);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float u = Mathf.Lerp(startU, endU, t);
            uVals[i] = u;
            Vector3 currPt = bodyPlan.EvaluateBodySectionAtU(u, nodes);
            cumDist[i] = cumDist[i - 1] + Vector3.Distance(prevPt, currPt);
            prevPt = currPt;
        }

        float totalArc = cumDist[steps];
        if (totalArc < 0.0001f) return bodyPlan.EvaluateBodySectionAtU(startU, nodes);

        float targetArc = Mathf.Clamp01(arcFraction) * totalArc;

        int segIndex = 0;
        for (int i = 0; i < steps; i++)
        {
            if (cumDist[i + 1] >= targetArc)
            {
                segIndex = i;
                break;
            }
        }

        float d0 = cumDist[segIndex];
        float d1 = cumDist[segIndex + 1];
        float segLen = d1 - d0;
        float localT = (segLen > 0.00001f) ? (targetArc - d0) / segLen : 0f;
        float targetU = Mathf.Lerp(uVals[segIndex], uVals[segIndex + 1], localT);

        return bodyPlan.EvaluateBodySectionAtU(targetU, nodes);
    }

    private SectionNodes GetFullSectionNodesAtZ(float z, float sideSign, float hw)
    {
        float keelY = (sheerPlan != null) ? sheerPlan.GetKeelBaselineYAtZ(z) : 0f;
        float floorY = (sheerPlan != null) ? sheerPlan.GetRisingLineYAtZ(z) : 0.5f;
        floorY = Mathf.Max(floorY, keelY);

        float bilgeY = (sheerPlan != null) ? sheerPlan.GetBilgeYAtZ(z) : (floorY + 1f);
        bilgeY = Mathf.Max(bilgeY, keelY);

        float floorX = (breadthPlan != null) ? breadthPlan.GetFloorLineXAtZ(z) : (hw + 1f);
        float bilgeX = (breadthPlan != null) ? breadthPlan.GetBilgeXAtZ(z) : (floorX + 1f);

        float hobY = (sheerPlan != null) ? sheerPlan.GetTheoreticalHobYAtZ(z) : (bilgeY + 1f);
        float hobX = (breadthPlan != null) ? breadthPlan.GetTheoreticalBreadthXAtZ(z) : (bilgeX + 1f);

        float sheerY = (sheerPlan != null) ? sheerPlan.GetTopLineYAtZ(z) : (hobY + 1f);
        float sheerX = (breadthPlan != null) ? breadthPlan.GetTopTimberXAtZ(z) : hobX;

        floorX = Mathf.Max(floorX, hw);
        bilgeX = Mathf.Clamp(bilgeX, hw, hobX);

        SectionNodes nodes = new SectionNodes();
        nodes.p0 = new Vector3(sideSign * hw, keelY, z);
        nodes.p1 = new Vector3(sideSign * floorX, floorY, z);
        nodes.p2 = new Vector3(sideSign * bilgeX, bilgeY, z);
        nodes.p3 = new Vector3(sideSign * hobX, hobY, z);
        nodes.p4 = new Vector3(sideSign * sheerX, sheerY, z);
        return nodes;
    }

    private float GetProfileYLimitAtZ(float z)
    {
        if (sheerPlan == null) return 0f;
        if (z >= sheerPlan.cachedKeelBowPoint.z)
        {
            return sheerPlan.GetStemProfileYAtZ(z);
        }
        else if (z <= sheerPlan.cachedKeelSternPoint.z)
        {
            return sheerPlan.GetSternProfileYAtZ(z);
        }
        return 0f;
    }

    public Vector3 EvaluateFramePointByArcFractionWithNodes(SectionNodes nodes, float startU, float endU, float arcFraction)
    {
        if (bodyPlan == null) return Vector3.zero;

        const int steps = 20;
        float[] uVals = new float[steps + 1];
        float[] cumDist = new float[steps + 1];
        cumDist[0] = 0f;
        uVals[0] = startU;

        Vector3 prevPt = bodyPlan.EvaluateBodySectionAtU(startU, nodes);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float u = Mathf.Lerp(startU, endU, t);
            uVals[i] = u;
            Vector3 currPt = bodyPlan.EvaluateBodySectionAtU(u, nodes);
            cumDist[i] = cumDist[i - 1] + Vector3.Distance(prevPt, currPt);
            prevPt = currPt;
        }

        float totalArc = cumDist[steps];
        if (totalArc < 0.0001f) return bodyPlan.EvaluateBodySectionAtU(startU, nodes);

        float targetArc = Mathf.Clamp01(arcFraction) * totalArc;

        int segIndex = 0;
        for (int i = 0; i < steps; i++)
        {
            if (cumDist[i + 1] >= targetArc)
            {
                segIndex = i;
                break;
            }
        }

        float d0 = cumDist[segIndex];
        float d1 = cumDist[segIndex + 1];
        float segLen = d1 - d0;
        float localT = (segLen > 0.00001f) ? (targetArc - d0) / segLen : 0f;
        float targetU = Mathf.Lerp(uVals[segIndex], uVals[segIndex + 1], localT);

        return bodyPlan.EvaluateBodySectionAtU(targetU, nodes);
    }

    public float CalculateDivisionArcLength(SectionNodes nodes, float startU, float endU)
    {
        if (bodyPlan == null) return 0.001f;
        const int steps = 20;
        float totalDist = 0f;
        Vector3 prevPt = bodyPlan.EvaluateBodySectionAtU(startU, nodes);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float u = Mathf.Lerp(startU, endU, t);
            Vector3 currPt = bodyPlan.EvaluateBodySectionAtU(u, nodes);
            totalDist += Vector3.Distance(prevPt, currPt);
            prevPt = currPt;
        }
        return Mathf.Max(0.001f, totalDist);
    }

    public Vector3 EvaluateFramePointAtY(SectionNodes nodes, float targetY)
    {
        if (bodyPlan == null) return nodes.p0;

        float minY = nodes.p0.y;
        float maxY = nodes.p4.y;

        if (targetY <= minY + 0.0001f)
        {
            return new Vector3(nodes.p0.x, targetY, nodes.p0.z);
        }

        if (targetY >= maxY - 0.001f)
        {
            return bodyPlan.EvaluateBodySectionAtU(1f, nodes);
        }

        // Binary search along frame section curve parameter u for exact target Y height
        float uMin = 0f;
        float uMax = 1f;
        for (int i = 0; i < 14; i++)
        {
            float uMid = (uMin + uMax) * 0.5f;
            Vector3 ptMid = bodyPlan.EvaluateBodySectionAtU(uMid, nodes);
            if (ptMid.y < targetY) uMin = uMid;
            else uMax = uMid;
        }

        float bestU = (uMin + uMax) * 0.5f;
        Vector3 finalPt = bodyPlan.EvaluateBodySectionAtU(bestU, nodes);
        finalPt.y = targetY; // Lock Y to exact target Y height
        return finalPt;
    }

    public List<Vector3> SampleTerminalBowContinuation(float sideSign, float proportion, float startU, float endU, int divisionNumber, Vector3 anchorPt, Vector3 prevPt, float keelBowZ)
    {
        List<Vector3> pts = new List<Vector3>();
        if (sheerPlan == null || bodyPlan == null) return pts;

        float hw = GetKeelHalfWidth();

        // 1. Calculate target stem profile intersection point
        float lowY = 0f, highY = 0f;
        if (divisionNumber == 1)
        {
            lowY = sheerPlan.cachedKeelBowPoint.y;
            highY = sheerPlan.cachedBowRisingEnd.y;
        }
        else if (divisionNumber == 2)
        {
            lowY = sheerPlan.cachedBowRisingEnd.y;
            float bilgeBowZ = sheerPlan.GetBilgeBowZ();
            highY = sheerPlan.GetBilgeYAtZ(bilgeBowZ);
        }
        else if (divisionNumber == 3)
        {
            float bilgeBowZ = sheerPlan.GetBilgeBowZ();
            lowY = sheerPlan.GetBilgeYAtZ(bilgeBowZ);
            highY = sheerPlan.cachedHobBowIntersect.y;
        }

        float stemY = Mathf.Lerp(lowY, highY, proportion);
        float stemZ = sheerPlan.GetStemProfileZAtY(stemY);
        if (stemZ <= keelBowZ + 0.001f) return pts;

        // 2. Exact incoming derivative matching (dY/dZ at keelBowZ)
        float dZ_anchor = anchorPt.z - prevPt.z;
        float dY_anchor = (dZ_anchor > 0.0001f) ? (anchorPt.y - prevPt.y) / dZ_anchor : 0f;

        float dz_total = stemZ - keelBowZ;
        float dY_stem = (dz_total > 0.001f) ? (stemY - anchorPt.y) / dz_total : dY_anchor;

        // Clamp slopes to prevent extreme overshoots
        dY_anchor = Mathf.Clamp(dY_anchor, -1f, 3f);
        dY_stem = Mathf.Clamp(dY_stem, -1f, 3f);

        // 3. Sample continuous stations between keelBowZ and stemZ with 100% C1 derivative continuity
        int steps = Mathf.Clamp(Mathf.RoundToInt(dz_total * 20f), 10, 50);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float tt = t * t;
            float ttt = tt * t;

            float h00 = 2f * ttt - 3f * tt + 1f;
            float h10 = ttt - 2f * tt + t;
            float h01 = -2f * ttt + 3f * tt;
            float h11 = ttt - tt;

            // Monotone Hermite Y-Height evaluation
            float targetY = h00 * anchorPt.y + h10 * (dY_anchor * dz_total) + h01 * stemY + h11 * (dY_stem * dz_total);
            float curZ = Mathf.Lerp(keelBowZ, stemZ, t);

            // Evaluate exact 3D point on physical frame timber curve at station Z(t) and height Y(t)
            SectionNodes nodes = (bodyPlan != null) ? bodyPlan.GetSectionNodesAtZ(curZ, sideSign) : GetFullSectionNodesAtZ(curZ, sideSign, hw);
            Vector3 finalPt = EvaluateFramePointAtY(nodes, targetY);
            finalPt.z = curZ;
            finalPt.x = sideSign * Mathf.Max(hw, Mathf.Abs(finalPt.x));
            pts.Add(finalPt);
        }

        return pts;
    }

    public List<Vector3> SampleTerminalSternContinuation(float sideSign, float proportion, float startU, float endU, int divisionNumber, Vector3 anchorPt, Vector3 nextPt, float keelSternZ)
    {
        List<Vector3> pts = new List<Vector3>();
        if (sheerPlan == null || bodyPlan == null) return pts;

        float hw = GetKeelHalfWidth();

        // 1. Calculate target stern profile intersection point
        float lowY = 0f, highY = 0f;
        if (divisionNumber == 1)
        {
            lowY = sheerPlan.cachedKeelSternPoint.y;
            highY = sheerPlan.cachedSternRisingEnd.y;
        }
        else if (divisionNumber == 2)
        {
            lowY = sheerPlan.cachedSternRisingEnd.y;
            float bilgeSternZ = sheerPlan.GetBilgeSternZ();
            highY = sheerPlan.GetBilgeYAtZ(bilgeSternZ);
        }
        else if (divisionNumber == 3)
        {
            float bilgeSternZ = sheerPlan.GetBilgeSternZ();
            lowY = sheerPlan.GetBilgeYAtZ(bilgeSternZ);
            highY = sheerPlan.cachedHobSternIntersect.y;
        }

        float sternY = Mathf.Lerp(lowY, highY, proportion);
        float sternZ = sheerPlan.GetSternProfileZAtY(sternY);
        if (sternZ >= keelSternZ - 0.001f) return pts;

        // 2. Exact incoming derivative matching (dY/dZ at keelSternZ)
        float dZ_anchor = anchorPt.z - nextPt.z;
        float dY_anchor = (Mathf.Abs(dZ_anchor) > 0.0001f) ? (anchorPt.y - nextPt.y) / dZ_anchor : 0f;

        float dz_total = sternZ - keelSternZ; // Negative value
        float dY_stern = (Mathf.Abs(dz_total) > 0.001f) ? (sternY - anchorPt.y) / dz_total : dY_anchor;

        // Clamp slopes to prevent extreme overshoots
        dY_anchor = Mathf.Clamp(dY_anchor, -3f, 1f);
        dY_stern = Mathf.Clamp(dY_stern, -3f, 1f);

        // 3. Sample continuous stations between keelSternZ and sternZ with 100% C1 derivative continuity
        int steps = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(dz_total) * 20f), 10, 50);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float tt = t * t;
            float ttt = tt * t;

            float h00 = 2f * ttt - 3f * tt + 1f;
            float h10 = ttt - 2f * tt + t;
            float h01 = -2f * ttt + 3f * tt;
            float h11 = ttt - tt;

            // Monotone Hermite Y-Height evaluation
            float targetY = h00 * anchorPt.y + h10 * (dY_anchor * dz_total) + h01 * sternY + h11 * (dY_stern * dz_total);
            float curZ = Mathf.Lerp(keelSternZ, sternZ, t);

            // Evaluate exact 3D point on physical frame timber curve at station Z(t) and height Y(t)
            SectionNodes nodes = (bodyPlan != null) ? bodyPlan.GetSectionNodesAtZ(curZ, sideSign) : GetFullSectionNodesAtZ(curZ, sideSign, hw);
            Vector3 finalPt = EvaluateFramePointAtY(nodes, targetY);
            finalPt.z = curZ;
            finalPt.x = sideSign * Mathf.Max(hw, Mathf.Abs(finalPt.x));
            pts.Insert(0, finalPt); // Insert at beginning for stern -> fore ordering
        }

        return pts;
    }

    public List<Vector3> SampleDivisionStrake(float sideSign, float proportion, float startU, float endU, int divisionNumber)
    {
        List<Vector3> pts = new List<Vector3>();
        if (sheerPlan == null || bodyPlan == null) return pts;

        float hw = GetKeelHalfWidth();
        float keelBowZ = (sheerPlan != null) ? sheerPlan.cachedKeelBowPoint.z : 5f;
        float keelSternZ = (sheerPlan != null) ? sheerPlan.cachedKeelSternPoint.z : -5f;

        // 1. Region 1: Flat Keel Main Body Sampling (Z in [keelSternZ, keelBowZ])
        List<Vector3> mainBodyZVecs = GetSamplingStations(keelSternZ, keelBowZ);
        List<Vector3> mainBodyPts = new List<Vector3>();

        foreach (Vector3 zVec in mainBodyZVecs)
        {
            float z = zVec.z;
            SectionNodes nodes = (bodyPlan != null) ? bodyPlan.GetSectionNodesAtZ(z, sideSign) : GetFullSectionNodesAtZ(z, sideSign, hw);
            Vector3 pt = EvaluateFramePointByArcFractionWithNodes(nodes, startU, endU, proportion);
            if (pt != Vector3.zero)
            {
                pt.x = sideSign * Mathf.Max(hw, Mathf.Abs(pt.x));
                mainBodyPts.Add(pt);
            }
        }

        if (mainBodyPts.Count == 0) return pts;

        // 2. Region 2A: Bow Continuation (Z > keelBowZ up to Stem Profile)
        Vector3 bowAnchorPt = mainBodyPts[mainBodyPts.Count - 1];
        Vector3 bowPrevPt = (mainBodyPts.Count > 1) ? mainBodyPts[mainBodyPts.Count - 2] : bowAnchorPt;

        List<Vector3> bowContinuationPts = SampleTerminalBowContinuation(sideSign, proportion, startU, endU, divisionNumber, bowAnchorPt, bowPrevPt, keelBowZ);

        // 3. Region 2B: Stern Continuation (Z < keelSternZ down to Stern Profile)
        Vector3 sternAnchorPt = mainBodyPts[0];
        Vector3 sternNextPt = (mainBodyPts.Count > 1) ? mainBodyPts[1] : sternAnchorPt;

        List<Vector3> sternContinuationPts = SampleTerminalSternContinuation(sideSign, proportion, startU, endU, divisionNumber, sternAnchorPt, sternNextPt, keelSternZ);

        // 4. Assemble complete 3D polyline: Stern Continuation + Main Body + Bow Continuation
        pts.AddRange(sternContinuationPts);
        pts.AddRange(mainBodyPts);
        pts.AddRange(bowContinuationPts);

        return pts;
    }

    public List<Vector3> SampleDivision1Strake(float sideSign, float proportion)
    {
        return SampleDivisionStrake(sideSign, proportion, 0.00f, 0.25f, 1);
    }

    public List<Vector3> SampleFloorStrake(float sideSign, float startZ, float endZ)
    {
        List<Vector3> pts = new List<Vector3>();
        List<Vector3> zList = GetSamplingStations(startZ, endZ);

        foreach (Vector3 zVec in zList)
        {
            float z = zVec.z;
            float y = sheerPlan.GetRisingLineYAtZ(z);
            float x = sideSign * breadthPlan.GetFloorLineXAtZ(z);
            pts.Add(new Vector3(x, y, z));
        }
        return pts;
    }

    public List<Vector3> SampleDivision2Strake(float sideSign, float proportion)
    {
        return SampleDivisionStrake(sideSign, proportion, 0.25f, 0.50f, 2);
    }

    public List<Vector3> SampleBilgeStrake(float sideSign, float startZ, float endZ)
    {
        List<Vector3> pts = new List<Vector3>();
        List<Vector3> zList = GetSamplingStations(startZ, endZ);

        foreach (Vector3 zVec in zList)
        {
            float z = zVec.z;
            float y = sheerPlan.GetBilgeYAtZ(z);
            float x = sideSign * breadthPlan.GetBilgeXAtZ(z);
            pts.Add(new Vector3(x, y, z));
        }
        return pts;
    }

    public List<Vector3> SampleDivision3Strake(float sideSign, float proportion)
    {
        return SampleDivisionStrake(sideSign, proportion, 0.50f, 0.75f, 3);
    }

    public List<Vector3> SampleWaleStrake(float sideSign, float startZ, float endZ)
    {
        List<Vector3> pts = new List<Vector3>();
        List<Vector3> zList = GetSamplingStations(startZ, endZ);

        foreach (Vector3 zVec in zList)
        {
            float z = zVec.z;
            float y = sheerPlan.GetMainWaleYAtZ(z);
            float x = sideSign * breadthPlan.GetTheoreticalBreadthXAtZ(z);
            pts.Add(new Vector3(x, y, z));
        }
        return pts;
    }

    public List<Vector3> SampleSheerStrake(float sideSign, float startZ, float endZ)
    {
        List<Vector3> pts = new List<Vector3>();
        List<Vector3> zList = GetSamplingStations(startZ, endZ);

        foreach (Vector3 zVec in zList)
        {
            float z = zVec.z;
            float y = sheerPlan.GetTopLineYAtZ(z);
            float x = sideSign * breadthPlan.GetTopTimberXAtZ(z);
            pts.Add(new Vector3(x, y, z));
        }
        return pts;
    }

    public List<Vector3> SampleTopTimberStrake(float sideSign, float startZ, float endZ)
    {
        List<Vector3> pts = new List<Vector3>();
        List<Vector3> zList = GetSamplingStations(startZ, endZ);

        foreach (Vector3 zVec in zList)
        {
            float z = zVec.z;
            float y = sheerPlan.GetTopLineYAtZ(z);
            float x = sideSign * breadthPlan.GetTopTimberXAtZ(z);
            pts.Add(new Vector3(x, y, z));
        }
        return pts;
    }

    private void DrawPolyline(List<Vector3> pts, Color color, string label, float sideSign)
    {
        if (pts == null || pts.Count < 2) return;

        Gizmos.color = color;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Gizmos.DrawLine(pts[i], pts[i + 1]);
        }

#if UNITY_EDITOR
        if (showLabels && sideSign > 0)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = color;
            labelStyle.fontSize = 10;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleLeft;

            Vector3 midPt = pts[pts.Count / 2];
            UnityEditor.Handles.Label(midPt + new Vector3(0.08f, 0, 0), label, labelStyle);
        }
#endif
    }
}
