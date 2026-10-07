using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boundary Lines Practice Script.
/// 
/// Non-Pinching Frame Arc-Length Girth Evaluation:
/// 1. 3D Arc-Length Girth Sampling: Samples intermediate boundary lines at equal 3D surface arc-length
///    girth intervals along the BodyPlan frame mould curves.
/// 2. Proportional Stem Arc Distribution: Distributes line landing points evenly along the stem post
///    curve, preventing points from pinching together into a single vertex.
/// </summary>
[ExecuteInEditMode]
public class BoundaryLinesPractice : MonoBehaviour
{
    public enum SpacingMode
    {
        TargetPlankWidthAtMasterFrame,
        ManualLineCounts
    }

    [Header("Component References")]
    public BodyPlan bodyPlan;
    public SheerPlan sheerPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;

    [Header("Spacing Mode at Master Frame (Z=0)")]
    public SpacingMode spacingMode = SpacingMode.TargetPlankWidthAtMasterFrame;

    [Header("Mode 1: Target Plank Width (Meters at Master Frame)")]
    [Range(0.05f, 1.5f)]
    public float targetPlankWidth = 0.35f;

    [Header("Mode 2: Manual Line Counts Per Division")]
    [Range(1, 20)]
    public int manualLinesFloorToKeel = 2;

    [Range(1, 20)]
    public int manualLinesBilgeToFloor = 3;

    [Range(1, 20)]
    public int manualLinesWaleToBilge = 4;

    [Header("Resolution Settings")]
    [Range(20, 300)]
    public int sampleResolution = 100;

    [Header("Visualization Toggles")]
    public bool showStarboard = true;
    public bool showPort = true;
    public bool showSpheresOnFrames = false;

    [Header("Line Colors")]
    public Color floorToKeelColor = new Color(0.2f, 0.85f, 1f, 0.95f);    // Cyan
    public Color bilgeToFloorColor = new Color(0.15f, 0.95f, 0.45f, 0.95f); // Green
    public Color waleToBilgeColor = new Color(1f, 0.4f, 0.1f, 0.95f);    // Orange

    [Header("Spline Fairing (Physical Wooden Board Bending)")]
    [Tooltip("Enable Centripetal Catmull-Rom longitudinal spline fairing to simulate physical wooden board bending stiffness.")]
    public bool enableCatmullRomFairing = true;

    [Tooltip("Centripetal parameter alpha (0.5 = Centripetal, 0.0 = Uniform, 1.0 = Chordal). Centripetal (0.5) eliminates kinks and overshoots.")]
    [Range(0f, 1f)]
    public float catmullRomAlpha = 0.5f;

    [Tooltip("Sub-segment evaluation density between adjacent frame control points along the wooden board spline.")]
    [Range(2, 20)]
    public int subsegmentDensity = 8;

    [Header("Master Frame Readouts (Read Only)")]
    public int derivedLinesFloorToKeel;
    public int derivedLinesBilgeToFloor;
    public int derivedLinesWaleToBilge;
    public float masterFrameGirthFloorToKeel;
    public float masterFrameGirthBilgeToFloor;
    public float masterFrameGirthWaleToBilge;

    [System.Serializable]
    private struct CachedPolyline
    {
        public List<Vector3> points;
        public Color color;
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
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
        if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (bodyPlan == null || sheerPlan == null || breadthPlan == null) return;

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
            DrawPolyline(poly.points, poly.color);
        }
    }

    private void RebuildPolylinesCache()
    {
        if (m_CachedPolylines == null) m_CachedPolylines = new List<CachedPolyline>();
        else m_CachedPolylines.Clear();

        if (bodyPlan == null || sheerPlan == null || breadthPlan == null) return;

        float hw = (bodyPlan != null) ? (bodyPlan.keelWidth / 2f) : 0.25f;

        CalculateMasterFrameCountsAndRatios(hw,
            out List<float> ratiosDiv1,
            out List<float> ratiosDiv2,
            out List<float> ratiosDiv3);

        GenerateBoundaryLinesForSide(1f, hw, ratiosDiv1, ratiosDiv2, ratiosDiv3);
        GenerateBoundaryLinesForSide(-1f, hw, ratiosDiv1, ratiosDiv2, ratiosDiv3);
    }

    private void CalculateMasterFrameCountsAndRatios(float hw,
        out List<float> ratiosDiv1,
        out List<float> ratiosDiv2,
        out List<float> ratiosDiv3)
    {
        ratiosDiv1 = new List<float>();
        ratiosDiv2 = new List<float>();
        ratiosDiv3 = new List<float>();

        // Master Frame Z station is 0f
        float zMaster = 0f;

        Vector3 keelMaster = GetFrameGirthPoint(1f, hw, zMaster, 1, 0f);
        Vector3 floorMaster = GetFrameGirthPoint(1f, hw, zMaster, 1, 1f);
        Vector3 bilgeMaster = GetFrameGirthPoint(1f, hw, zMaster, 2, 1f);
        Vector3 waleMaster = GetFrameGirthPoint(1f, hw, zMaster, 3, 1f);

        masterFrameGirthFloorToKeel = Vector3.Distance(keelMaster, floorMaster);
        masterFrameGirthBilgeToFloor = Vector3.Distance(floorMaster, bilgeMaster);
        masterFrameGirthWaleToBilge = Vector3.Distance(bilgeMaster, waleMaster);

        if (spacingMode == SpacingMode.TargetPlankWidthAtMasterFrame)
        {
            float targetW = Mathf.Max(0.05f, targetPlankWidth);
            derivedLinesFloorToKeel = Mathf.Max(1, Mathf.RoundToInt(masterFrameGirthFloorToKeel / targetW) - 1);
            derivedLinesBilgeToFloor = Mathf.Max(1, Mathf.RoundToInt(masterFrameGirthBilgeToFloor / targetW) - 1);
            derivedLinesWaleToBilge = Mathf.Max(1, Mathf.RoundToInt(masterFrameGirthWaleToBilge / targetW) - 1);
        }
        else
        {
            derivedLinesFloorToKeel = Mathf.Max(1, manualLinesFloorToKeel);
            derivedLinesBilgeToFloor = Mathf.Max(1, manualLinesBilgeToFloor);
            derivedLinesWaleToBilge = Mathf.Max(1, manualLinesWaleToBilge);
        }

        // Compute step ratios (evenly dividing each division at Master Frame)
        for (int i = 1; i <= derivedLinesFloorToKeel; i++)
        {
            ratiosDiv1.Add(i / (float)(derivedLinesFloorToKeel + 1));
        }
        for (int i = 1; i <= derivedLinesBilgeToFloor; i++)
        {
            ratiosDiv2.Add(i / (float)(derivedLinesBilgeToFloor + 1));
        }
        for (int i = 1; i <= derivedLinesWaleToBilge; i++)
        {
            ratiosDiv3.Add(i / (float)(derivedLinesWaleToBilge + 1));
        }
    }

    private void GenerateBoundaryLinesForSide(float sideSign, float hw,
        List<float> ratiosDiv1, List<float> ratiosDiv2, List<float> ratiosDiv3)
    {
        // Division 1: Floor to Keel
        foreach (float ratio in ratiosDiv1)
        {
            List<Vector3> linePts = SampleIntermediateBoundaryLine(sideSign, hw, 1, ratio);
            AddCachedPolyline(linePts, floorToKeelColor, sideSign);
        }

        // Division 2: Bilge to Floor
        foreach (float ratio in ratiosDiv2)
        {
            List<Vector3> linePts = SampleIntermediateBoundaryLine(sideSign, hw, 2, ratio);
            AddCachedPolyline(linePts, bilgeToFloorColor, sideSign);
        }

        // Division 3: Wale to Bilge
        foreach (float ratio in ratiosDiv3)
        {
            List<Vector3> linePts = SampleIntermediateBoundaryLine(sideSign, hw, 3, ratio);
            AddCachedPolyline(linePts, waleToBilgeColor, sideSign);
        }
    }

    private void AddCachedPolyline(List<Vector3> pts, Color color, float sideSign)
    {
        if (pts == null || pts.Count < 2) return;
        m_CachedPolylines.Add(new CachedPolyline
        {
            points = pts,
            color = color,
            sideSign = sideSign
        });
    }

    private List<Vector3> SampleIntermediateBoundaryLine(float sideSign, float hw, int divisionNumber, float ratio)
    {
        List<Vector3> pts = new List<Vector3>();
        if (sheerPlan == null || framingModel == null || bodyPlan == null) return pts;

        // 1. Determine lower and upper bow/stern termination heights for this division
        float lowerBowY = 0f, upperBowY = 0f, lowerSternY = 0f, upperSternY = 0f;

        float bilgeBowZ = sheerPlan.GetBilgeBowZ();
        float bilgeSternZ = sheerPlan.GetBilgeSternZ();
        float bilgeBowY = sheerPlan.GetBilgeYAtZ(bilgeBowZ);
        float bilgeSternY = sheerPlan.GetBilgeYAtZ(bilgeSternZ);
        float floorBowY = sheerPlan.cachedBowRisingEnd.y;
        float floorSternY = sheerPlan.cachedSternRisingEnd.y;

        if (divisionNumber == 1)
        {
            lowerBowY = sheerPlan.cachedKeelBowPoint.y;
            upperBowY = floorBowY;
            lowerSternY = sheerPlan.cachedKeelSternPoint.y;
            upperSternY = floorSternY;
        }
        else if (divisionNumber == 2)
        {
            lowerBowY = floorBowY;
            upperBowY = bilgeBowY;
            lowerSternY = floorSternY;
            upperSternY = bilgeSternY;
        }
        else if (divisionNumber == 3)
        {
            float waleBowZ = sheerPlan.cachedHobBowIntersect.z;
            float waleSternZ = sheerPlan.cachedHobSternIntersect.z;
            lowerBowY = bilgeBowY;
            upperBowY = sheerPlan.GetMainWaleYAtZ(waleBowZ);
            lowerSternY = bilgeSternY;
            upperSternY = sheerPlan.GetMainWaleYAtZ(waleSternZ);
        }

        float lineBowY = Mathf.Lerp(lowerBowY, upperBowY, ratio);
        float lineBowZ = sheerPlan.GetStemProfileZAtY(lineBowY);

        float lineSternY = Mathf.Lerp(lowerSternY, upperSternY, ratio);
        float lineSternZ = sheerPlan.GetSternProfileZAtY(lineSternY);

        // 2. Sample continuous stations Z along the hull length
        int res = Mathf.Clamp(sampleResolution, 40, 300);
        for (int i = 0; i <= res; i++)
        {
            float t = i / (float)res;
            float z = Mathf.Lerp(lineSternZ, lineBowZ, t);

            // Compute proportional target Y height at station Z
            float lowY = 0f, highY = 0f;
            if (divisionNumber == 1)
            {
                lowY = sheerPlan.GetKeelBaselineYAtZ(z);
                highY = sheerPlan.GetRisingLineYAtZ(z);
            }
            else if (divisionNumber == 2)
            {
                lowY = sheerPlan.GetRisingLineYAtZ(z);
                highY = (z >= bilgeBowZ) ? sheerPlan.GetProjectedBowBilgeYAtZ(z) : 
                       ((z <= bilgeSternZ) ? sheerPlan.GetProjectedSternBilgeYAtZ(z) : sheerPlan.GetBilgeYAtZ(z));
            }
            else // divisionNumber == 3
            {
                lowY = (z >= bilgeBowZ) ? sheerPlan.GetProjectedBowBilgeYAtZ(z) : 
                      ((z <= bilgeSternZ) ? sheerPlan.GetProjectedSternBilgeYAtZ(z) : sheerPlan.GetBilgeYAtZ(z));
                highY = sheerPlan.GetMainWaleYAtZ(z);
            }

            float targetY = Mathf.Lerp(lowY, highY, ratio);

            // Fetch exact control points from FramingModel for station Z
            Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
            bool hasFrame = framingModel.GetFrameControlPointsAtZ(z, sideSign, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3);

            if (!hasFrame) continue;

            float frameBottomY = pL0.y;
            if (targetY < frameBottomY - 0.05f)
            {
                continue; // Omit virtual points below physical frame bottom at stem/stern
            }

            // Intersect targetY directly with FramingModel's 3D frame control points
            Vector3 pt = framingModel.GetTransverseFramePointAtYFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, targetY);
            pts.Add(pt);
        }

        // 3. Optional Centripetal Catmull-Rom Longitudinal Spline Fairing (Smooths longitudinal station points)
        if (enableCatmullRomFairing && pts.Count >= 2)
        {
            return StrakesPointsCatmullRom.BuildCentripetalCatmullRomSpline(pts, subsegmentDensity, catmullRomAlpha);
        }

        return pts;
    }

    private Vector3 GetFrameGirthPoint(float sideSign, float hw, float z, int divisionNumber, float ratio)
    {
        // 1. Fetch exact frame nodes from FramingModel (the authoritative 3D frame provider)
        SectionNodes nodes;
        if (framingModel != null)
        {
            nodes = framingModel.GetSectionNodesForFrame(z, sideSign);
        }
        else if (bodyPlan != null)
        {
            nodes = GetUncollapsedSectionNodesAtZ(z, sideSign);
        }
        else
        {
            Vector3 pL = GetUncollapsedGuidePoint(sideSign, hw, z, divisionNumber, false);
            Vector3 pU = GetUncollapsedGuidePoint(sideSign, hw, z, divisionNumber, true);
            return Vector3.Lerp(pL, pU, ratio);
        }

        float uLower = 0f;
        float uUpper = 0.25f;

        if (divisionNumber == 1)
        {
            uLower = 0f;
            uUpper = 0.25f;
        }
        else if (divisionNumber == 2)
        {
            uLower = 0.25f;
            uUpper = 0.50f;
        }
        else if (divisionNumber == 3)
        {
            uLower = 0.50f;
            uUpper = 1.00f;
        }

        // 2. Calculate total 3D arc length along frame mould between uLower and uUpper
        int steps = 16;
        float totalArcLength = 0f;
        float[] arcLengths = new float[steps + 1];
        Vector3 prevPt = bodyPlan.EvaluateBodySectionAtU(uLower, nodes);
        arcLengths[0] = 0f;

        for (int i = 1; i <= steps; i++)
        {
            float uStep = Mathf.Lerp(uLower, uUpper, i / (float)steps);
            Vector3 currPt = bodyPlan.EvaluateBodySectionAtU(uStep, nodes);
            totalArcLength += Vector3.Distance(prevPt, currPt);
            arcLengths[i] = totalArcLength;
            prevPt = currPt;
        }

        Vector3 finalPt;
        if (totalArcLength < 0.0001f)
        {
            float uDirect = Mathf.Lerp(uLower, uUpper, ratio);
            finalPt = bodyPlan.EvaluateBodySectionAtU(uDirect, nodes);
        }
        else
        {
            // 3. Find parameter u for target ratio of 3D arc length
            float targetArc = ratio * totalArcLength;
            int segIndex = 0;
            for (int i = 0; i < steps; i++)
            {
                if (targetArc <= arcLengths[i + 1])
                {
                    segIndex = i;
                    break;
                }
            }

            float segStartArc = arcLengths[segIndex];
            float segEndArc = arcLengths[segIndex + 1];
            float segLength = segEndArc - segStartArc;
            float segT = (segLength > 0.0001f) ? (targetArc - segStartArc) / segLength : 0f;

            float u1 = Mathf.Lerp(uLower, uUpper, segIndex / (float)steps);
            float u2 = Mathf.Lerp(uLower, uUpper, (segIndex + 1) / (float)steps);
            float targetU = Mathf.Lerp(u1, u2, segT);

            finalPt = bodyPlan.EvaluateBodySectionAtU(targetU, nodes);
        }

        // 4. Respect FramingModel physical frame bottom cutoffs (prevents dipping below physical frame timbers)
        if (framingModel != null && sheerPlan != null)
        {
            float bottomY = framingModel.GetFrameBottomYAtZ(z);
            if (finalPt.y < bottomY)
            {
                finalPt.y = bottomY;
            }
        }

        return finalPt;
    }

    private SectionNodes GetUncollapsedSectionNodesAtZ(float z, float sideSign)
    {
        float hw = (bodyPlan != null) ? (bodyPlan.keelWidth / 2f) : 0.25f;

        float keelY = (sheerPlan != null) ? sheerPlan.GetKeelBaselineYAtZ(z) : 0f;
        float floorY = (sheerPlan != null) ? sheerPlan.GetRisingLineYAtZ(z) : 0.5f;
        floorY = Mathf.Max(floorY, keelY);

        float bilgeY = (sheerPlan != null) ? 
            (z >= sheerPlan.GetBilgeBowZ() ? sheerPlan.GetProjectedBowBilgeYAtZ(z) : 
            (z <= sheerPlan.GetBilgeSternZ() ? sheerPlan.GetProjectedSternBilgeYAtZ(z) : sheerPlan.GetBilgeYAtZ(z))) 
            : (floorY + 1f);

        float hobY = (sheerPlan != null) ? sheerPlan.GetTheoreticalHobYAtZ(z) : (bilgeY + 1f);
        float hobX = (breadthPlan != null) ? breadthPlan.GetTheoreticalBreadthXAtZ(z) : (hw + 1.5f);

        float floorX = (breadthPlan != null) ? breadthPlan.GetFloorLineXAtZ(z) : (hw + 0.5f);
        float bilgeX = (breadthPlan != null) ? breadthPlan.GetBilgeXAtZ(z) : (hw + 1.0f);

        // Preserve full uncollapsed frame surface breadth in bow/stern overhangs matching physical frames 100%
        if (sheerPlan != null && breadthPlan != null)
        {
            float bowRisingZ = sheerPlan.cachedBowRisingEnd.z;
            float sternRisingZ = sheerPlan.cachedSternRisingEnd.z;

            if (z > bowRisingZ)
            {
                floorX = Mathf.Lerp(hw, hobX, 0.35f);
                bilgeX = Mathf.Lerp(hw, hobX, 0.70f);
            }
            else if (z < sternRisingZ)
            {
                floorX = Mathf.Lerp(hw, hobX, 0.35f);
                bilgeX = Mathf.Lerp(hw, hobX, 0.70f);
            }
        }

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

    private Vector3 GetUncollapsedGuidePoint(float sideSign, float hw, float z, int divisionNumber, bool getUpper)
    {
        float keelY = (sheerPlan != null) ? sheerPlan.GetKeelBaselineYAtZ(z) : 0f;
        float floorY = (sheerPlan != null) ? sheerPlan.GetRisingLineYAtZ(z) : 0.5f;
        floorY = Mathf.Max(floorY, keelY);

        float bilgeY = (sheerPlan != null) ? 
            (z >= sheerPlan.GetBilgeBowZ() ? sheerPlan.GetProjectedBowBilgeYAtZ(z) : 
            (z <= sheerPlan.GetBilgeSternZ() ? sheerPlan.GetProjectedSternBilgeYAtZ(z) : sheerPlan.GetBilgeYAtZ(z))) 
            : (floorY + 1f);

        float floorX = (breadthPlan != null) ? breadthPlan.GetFloorLineXAtZ(z) : (hw + 1f);
        float bilgeX = (breadthPlan != null) ? breadthPlan.GetBilgeXAtZ(z) : (floorX + 1f);

        if (sheerPlan != null && breadthPlan != null)
        {
            float bilgeBowZ = sheerPlan.GetBilgeBowZ();
            if (z > bilgeBowZ)
            {
                float uncollapsedHobX = breadthPlan.GetTheoreticalBreadthXAtZ(z);
                float floorXUncollapsed = breadthPlan.GetFloorLineXAtZ(z);
                bilgeX = Mathf.Lerp(floorXUncollapsed, uncollapsedHobX, breadthPlan.bilgeWidthRatio);
            }
        }

        float hobY = (sheerPlan != null) ? sheerPlan.GetTheoreticalHobYAtZ(z) : (bilgeY + 1f);
        float hobX = (breadthPlan != null) ? breadthPlan.GetTheoreticalBreadthXAtZ(z) : (bilgeX + 1f);

        float waleY = (sheerPlan != null) ? sheerPlan.GetMainWaleYAtZ(z) : (hobY + 1f);
        float waleX = hobX;

        if (divisionNumber == 1)
        {
            return getUpper ? new Vector3(sideSign * floorX, floorY, z) : new Vector3(sideSign * hw, keelY, z);
        }
        else if (divisionNumber == 2)
        {
            return getUpper ? new Vector3(sideSign * bilgeX, bilgeY, z) : new Vector3(sideSign * floorX, floorY, z);
        }
        else // divisionNumber == 3
        {
            return getUpper ? new Vector3(sideSign * waleX, waleY, z) : new Vector3(sideSign * bilgeX, bilgeY, z);
        }
    }

    private void DrawPolyline(List<Vector3> pts, Color color)
    {
        if (pts == null || pts.Count < 2) return;

        Gizmos.color = color;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Gizmos.DrawLine(pts[i], pts[i + 1]);
        }

        if (showSpheresOnFrames)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                Gizmos.DrawSphere(pts[i], 0.04f);
            }
        }
    }
}
