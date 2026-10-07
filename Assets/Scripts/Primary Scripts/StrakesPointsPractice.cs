using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Master Frame Strake Points Practice.
/// 
/// 1. Establishes fixed division point counts at the Master Frame (Z = 0) based on targetInterval (e.g. 0.25m).
/// 2. Evaluates points across all frames ahead and behind the Master Frame between their respective guide line boundaries:
///    - Main Wale to Bilge (including extended Bilge/Wale curve above their endpoints)
///    - Bilge to Floor (including extended Floor/Bilge curve above their endpoints)
///    - Floor to Keel (including vertical height rise from Keel baseline Y=0 to climbing stem/stern)
/// 3. Placed points falling below the physical Stem or Stern profile curve (Y < Y_profile) are DISCARDED.
///    Only points that land directly on the physical frame timber at or above the stem/stern profile are KEPT and RENDERED.
/// </summary>
[ExecuteInEditMode]
public class StrakesPointsPractice : MonoBehaviour
{
    [Header("Component References")]
    public BodyPlan bodyPlan;
    public SheerPlan sheerPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;

    [Header("Spacing Settings")]
    [Tooltip("Target physical spacing distance between points on the Master Frame (in meters). Defines fixed point counts.")]
    [Range(0.05f, 1f)]
    public float targetInterval = 0.25f;

    [Header("Sampling Resolution Settings")]
    [Tooltip("Render points across all transverse frame stations (from FramingModel). If false, uses sampleResolution.")]
    public bool useFrameStations = true;

    [Tooltip("Number of continuous Z stations sampled along the hull length (when useFrameStations is false).")]
    [Range(10, 200)]
    public int sampleResolution = 60;

    [Header("Visualization Toggles")]
    public bool showStarboard = true;
    public bool showPort = true;
    public bool showPoints = true;
    public bool showStrakeLines = true;
    public bool showLabels = true;
    public bool showDiscardedPoints = false;

    [Range(0.02f, 0.2f)]
    public float sphereRadius = 0.06f;

    [Header("Division Colors")]
    public Color waleToBilgeColor = new Color(1f, 0.4f, 0.1f, 0.95f);   // Orange / Wale to Bilge
    public Color bilgeToFloorColor = new Color(0.15f, 0.95f, 0.45f, 0.95f); // Green / Bilge to Floor
    public Color floorToKeelColor = new Color(0.2f, 0.85f, 1f, 0.95f);    // Cyan / Floor to Keel
    public Color discardedColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);    // Faint Grey for discarded virtual points

    [Header("Frame Geometry Sync Settings")]
    [Tooltip("If true, evaluates points using un-collapsed full section curves (including projected guide line extensions) so points are discarded cleanly at the stem/stern profile height.")]
    public bool useExtendedBoundaryCurves = true;

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        FindReferences();
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
        if (bodyPlan == null || sheerPlan == null) return;

        // 1. Calculate fixed point counts for each division at the Master Frame (Z = 0)
        SectionNodes masterStbd = bodyPlan.GetSectionNodesAtZ(0f, 1f);
        int countD3 = CalculateDivisionPointCount(masterStbd, 0.50f, 0.75f); // Wale to Bilge
        int countD2 = CalculateDivisionPointCount(masterStbd, 0.25f, 0.50f); // Bilge to Floor
        int countD1 = CalculateDivisionPointCount(masterStbd, 0.00f, 0.25f); // Floor to Keel

        // 2. Fetch Z stations across the hull
        List<float> zStations = GetZStations();

        // 3. Render points and continuous strake lines for each side
        if (showStarboard) ProcessDivisionPointsForSide(1f, zStations, countD3, countD2, countD1);
        if (showPort) ProcessDivisionPointsForSide(-1f, zStations, countD3, countD2, countD1);
    }

    private int CalculateDivisionPointCount(SectionNodes nodes, float startU, float endU)
    {
        float arcLength = CalculateDivisionArcLength(nodes, startU, endU);
        return Mathf.Max(1, Mathf.RoundToInt(arcLength / Mathf.Max(0.01f, targetInterval)));
    }

    private List<float> GetZStations()
    {
        List<float> stations = new List<float>();
        if (useFrameStations && framingModel != null)
        {
            return framingModel.CalculateFrameStations();
        }

        float aftZ = (sheerPlan != null) ? sheerPlan.GetAftMostZ() : -10f;
        float fwdZ = (sheerPlan != null) ? sheerPlan.GetFwdMostZ() : 10f;
        int res = Mathf.Clamp(sampleResolution, 10, 200);

        for (int i = 0; i <= res; i++)
        {
            float t = i / (float)res;
            stations.Add(Mathf.Lerp(aftZ, fwdZ, t));
        }
        return stations;
    }

    private void ProcessDivisionPointsForSide(float sideSign, List<float> zStations, int countD3, int countD2, int countD1)
    {
        float hw = (bodyPlan != null) ? (bodyPlan.keelWidth / 2f) : 0.25f;

        // 1. Division: Main Wale (u = 0.75) to Bilge (u = 0.50)
        ProcessDivision(sideSign, hw, zStations, 0.50f, 0.75f, countD3, waleToBilgeColor, "Wale-Bilge", 3);

        // 2. Division: Bilge (u = 0.50) to Floor (u = 0.25)
        ProcessDivision(sideSign, hw, zStations, 0.25f, 0.50f, countD2, bilgeToFloorColor, "Bilge-Floor", 2);

        // 3. Division: Floor (u = 0.25) to Keel (u = 0.00)
        ProcessDivision(sideSign, hw, zStations, 0.00f, 0.25f, countD1, floorToKeelColor, "Floor-Keel", 1);
    }

    private void ProcessDivision(float sideSign, float hw, List<float> zStations, float startU, float endU, int count, Color color, string divisionLabel, int divisionNumber)
    {
        int numStations = zStations.Count;
        Vector3?[,] grid = new Vector3?[count + 1, numStations];
        Vector3?[,] virtualGrid = new Vector3?[count + 1, numStations];

        for (int j = 0; j < numStations; j++)
        {
            float z = zStations[j];
            SectionNodes nodes = (bodyPlan != null) ? bodyPlan.GetSectionNodesAtZ(z, sideSign) : GetFullSectionNodesAtZ(z, sideSign, hw);
            float yProfile = GetProfileYLimitAtZ(z);

            float lowerBoundaryY = 0f;
            if (divisionNumber == 1) lowerBoundaryY = (sheerPlan != null) ? sheerPlan.GetKeelBaselineYAtZ(z) : 0f;
            else if (divisionNumber == 2) lowerBoundaryY = (sheerPlan != null) ? sheerPlan.GetRisingLineYAtZ(z) : 0.5f;
            else if (divisionNumber == 3) lowerBoundaryY = (sheerPlan != null) ? sheerPlan.GetBilgeYAtZ(z) : 1.5f;

            float frameArc = CalculateDivisionArcLength(nodes, startU, endU);
            float frameBottomY = Mathf.Max(nodes.p0.y, yProfile);
            float verticalRise = Mathf.Max(0f, frameBottomY - lowerBoundaryY);
            float totalCompoundArc = verticalRise + frameArc;

            for (int i = 0; i <= count; i++)
            {
                float proportion = (float)i / count;
                float targetArc = proportion * totalCompoundArc;

                Vector3 pt;
                bool isDiscarded = false;

                if (verticalRise > 0.0001f && targetArc < verticalRise - 0.0001f)
                {
                    isDiscarded = true;
                    float targetY = lowerBoundaryY + targetArc;
                    pt = new Vector3(sideSign * hw, targetY, nodes.p0.z);
                }
                else
                {
                    float remainingArc = targetArc - verticalRise;
                    float frameFraction = (totalCompoundArc > 0.0001f && frameArc > 0.0001f) ? (remainingArc / frameArc) : proportion;
                    pt = EvaluateMasterFramePointByArcFraction(nodes, startU, endU, frameFraction);

                    if (pt.y < frameBottomY - 0.001f)
                    {
                        isDiscarded = true;
                    }
                }

                virtualGrid[i, j] = pt;

                if (!isDiscarded)
                {
                    grid[i, j] = pt;
                }
                else if (showDiscardedPoints)
                {
                    Gizmos.color = discardedColor;
                    Gizmos.DrawSphere(pt, sphereRadius * 0.7f);
                }
            }
        }

        // Render Points and Continuous Strake Lines
        Gizmos.color = color;
#if UNITY_EDITOR
        GUIStyle labelStyle = new GUIStyle();
        labelStyle.normal.textColor = color;
        labelStyle.fontSize = 10;
        labelStyle.fontStyle = FontStyle.Bold;
#endif

        for (int i = 0; i <= count; i++)
        {
            for (int j = 0; j < numStations; j++)
            {
                Vector3? pt = grid[i, j];

                // Render spheres for valid frame points
                if (showPoints && pt.HasValue)
                {
                    Gizmos.color = color;
                    Gizmos.DrawSphere(pt.Value, sphereRadius);

#if UNITY_EDITOR
                    if (showLabels && sideSign > 0 && zStations[j] == 0f)
                    {
                        UnityEditor.Handles.Label(pt.Value + new Vector3(0.08f, 0, 0), $"{divisionLabel} [{i}/{count}]", labelStyle);
                    }
#endif
                }

                // Connect continuous strake lines across stations, stopping cleanly at the frame where point ends
                if (showStrakeLines && j < numStations - 1)
                {
                    Vector3? ptA = grid[i, j];
                    Vector3? ptB = grid[i, j + 1];

                    if (ptA.HasValue && ptB.HasValue)
                    {
                        // Connect frame-to-frame line segment
                        Gizmos.color = color;
                        Gizmos.DrawLine(ptA.Value, ptB.Value);
                    }
                    else if (ptA.HasValue && !ptB.HasValue)
                    {
                        // Strake i terminates between station j and j+1: connect up to the frame where point ends
                        // (Line stops cleanly at frame station j, the last frame that the strake point ends on)
                        Gizmos.color = color;
                        Vector3? virtB = virtualGrid[i, j + 1];
                        if (virtB.HasValue)
                        {
                            // Optional smooth extension to stem profile cutoff boundary
                            float yLimitB = GetProfileYLimitAtZ(zStations[j + 1]);
                            float dy = ptA.Value.y - virtB.Value.y;
                            float t = (dy > 0.0001f) ? Mathf.Clamp01((ptA.Value.y - yLimitB) / dy) : 0f;
                            Vector3 stemPt = Vector3.Lerp(ptA.Value, virtB.Value, t);
                            Gizmos.DrawLine(ptA.Value, stemPt);
                        }
                    }
                }
            }
        }
    }

    private SectionNodes GetFullSectionNodesAtZ(float z, float sideSign, float hw)
    {
        // Continuous section nodes including baseline Y = 0 and projected guide line curve extensions
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

    public float CalculateDivisionArcLength(SectionNodes nodes, float startU, float endU)
    {
        const int steps = 40;
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

    public Vector3 EvaluateMasterFramePointByArcFraction(SectionNodes nodes, float startU, float endU, float arcFraction)
    {
        const int steps = 40;
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
}
