using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class BodyPlan : MonoBehaviour
{
    [Tooltip("Reference to the SheerPlan to get hull heights.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the BreadthPlan to get max beam.")]
    public BreadthPlan breadthPlan;

    [Header("Section Architecture & Fairness")]
    [Tooltip("Select the mathematical curve formulation for evaluating hull section sweeps.")]
    public SectionCurveMode curveMode = SectionCurveMode.FairSplineBatten;

    [Tooltip("Influence magnitude / flexibility of the spline curve between nodes (0.5 = stiff/flat, 1.0 = natural wooden batten, 1.5 = full sweeping hull curves).")]
    [Range(0.5f, 2.0f)] public float splineTension = 1.0f;

    [Header("Gizmo Settings")]
    public bool showLabels = true;
    [Tooltip("Show dashed straight lines connecting the section nodes (p0 -> p1 -> p2 -> p3 -> p4) to visualize the pure polygonal skeleton.")]
    public bool showPolygonSkeleton = true;
    [Tooltip("Show the tangent control arms for the section curve in Scene view.")]
    public bool showControlArms = true;
    [Tooltip("Length scale of the rendered tangent control arms.")]
    [Range(0.1f, 2f)] public float controlArmLengthScale = 0.6f;
    [Tooltip("Render all alternative curve modes simultaneously in translucent colors for side-by-side visual comparison.")]
    public bool showComparisonOverlay = false;

    private static Dictionary<SectionCurveMode, ISectionCurveEvaluator> evaluators;

    private static void EnsureEvaluatorsInitialized()
    {
        if (evaluators == null || evaluators.Count == 0)
        {
            evaluators = new Dictionary<SectionCurveMode, ISectionCurveEvaluator>
            {
                { SectionCurveMode.FairSplineBatten, new FairSplineBattenEvaluator() },
                { SectionCurveMode.BiArcTangentSweeps, new BiArcTangentSweepEvaluator() },
                { SectionCurveMode.AkimaSpline, new AkimaSplineEvaluator() },
                { SectionCurveMode.NurbsBSpline, new NurbsSectionEvaluator() },
                { SectionCurveMode.QuarticBezier, new QuarticBezierEvaluator() }
            };
        }
    }

    public static ISectionCurveEvaluator GetEvaluator(SectionCurveMode mode)
    {
        EnsureEvaluatorsInitialized();
        if (evaluators.TryGetValue(mode, out var eval)) return eval;
        return evaluators[SectionCurveMode.FairSplineBatten];
    }

    [Header("Keel & Master Dimensions")]
    [Tooltip("The width of the physical Keel (P/S distance). The mould will start at keelWidth / 2.")]
    public float keelWidth = 0.5f;

    [Header("Floor Hollow (Keel to Floor Arc)")]
    [Tooltip("Curvature of the Floor Hollow between Keel (p0) and Floor (p1). (0 = Straight deadrise, 1 = Full Bi-Arc hollow curve).")]
    [Range(0f, 1f)] public float floorHollow = 0.5f;

    [Header("Backward Compatibility Settings")]
    [Tooltip("Length of the HoB downward control arm as a percentage (0 to 1) of the total HoB height.")]
    [Range(0f, 1f)] public float hobLowerArmLength = 0.5f;
    [Range(0f, 1f)] public float connectionPointPosition = 0.5f;
    [Range(0f, 1f)] public float bowConnectionPointPosition = 0.5f;
    [Range(0f, 1f)] public float sternConnectionPointPosition = 0.5f;
    [HideInInspector] public bool useQuarticBezier = false;
    [HideInInspector] public bool useQuinticBezier = false;
    [HideInInspector] public float innerKeelPosition = 0.5f;
    [HideInInspector] public float middleControlPosition = 0.5f;

    public float GetConnectionPointPositionAtZ(float z)
    {
        if (sheerPlan == null) return connectionPointPosition;

        if (z >= 0f)
        {
            float bowZ = sheerPlan.cachedBowRisingEnd.z;
            if (bowZ <= 0.001f) return connectionPointPosition;
            float t = Mathf.Clamp01(z / bowZ);
            return Mathf.Lerp(connectionPointPosition, bowConnectionPointPosition, t);
        }
        else
        {
            float sternZ = sheerPlan.cachedSternRisingEnd.z;
            if (sternZ >= -0.001f) return connectionPointPosition;
            float t = Mathf.Clamp01(z / sternZ);
            return Mathf.Lerp(connectionPointPosition, sternConnectionPointPosition, t);
        }
    }

    [Header("Visualization Settings")]
    public Color starboardColor = Color.green;
    public Color portColor = Color.red;
    public Color nodeColor = Color.cyan;

    [HideInInspector] public Vector3 cachedL0;
    [HideInInspector] public Vector3 cachedL1;
    [HideInInspector] public Vector3 cachedLR;
    [HideInInspector] public Vector3 cachedL2;
    [HideInInspector] public Vector3 cachedL3;
    [HideInInspector] public Vector3 cachedU0;
    [HideInInspector] public Vector3 cachedU1;
    [HideInInspector] public Vector3 cachedU2;
    [HideInInspector] public Vector3 cachedU3;
    [HideInInspector] public int changeVersion = 0;

    private bool isUpdatingCurves = false;

    private void OnValidate()
    {
        UpdateCachedCurves();
    }

    public virtual void UpdateCachedCurves()
    {
        if (isUpdatingCurves) return;
        FindReferences();
        if (sheerPlan == null || breadthPlan == null) return;

        isUpdatingCurves = true;
        try
        {
            sheerPlan.UpdateCachedValues();
            breadthPlan.UpdateCachedCurves();

            SectionNodes master = GetSectionNodesAtZ(0f, 1f);
            cachedL0 = master.p0;
            cachedL1 = master.p1;
            cachedLR = master.p2;
            cachedL2 = master.p2;
            cachedL3 = master.p3;

            cachedU0 = master.p3;
            cachedU1 = master.p3;
            cachedU2 = master.p4;
            cachedU3 = master.p4;

            changeVersion++;
        }
        finally
        {
            isUpdatingCurves = false;
        }
    }

    public void FindReferences()
    {
        if (sheerPlan == null) sheerPlan = GetComponentInParent<SheerPlan>();
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();

        if (breadthPlan == null) breadthPlan = GetComponentInParent<BreadthPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
    }

    /// <summary>
    /// Samples the 5 paired 3D structural nodes at station Z for specified side (+1 Starboard, -1 Port).
    /// </summary>
    public SectionNodes GetSectionNodesAtZ(float z, float side)
    {
        FindReferences();
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float hw = keelWidth / 2f;

        float keelY = (sheerPlan != null) ? sheerPlan.GetKeelYAtZ(z) : 0f;
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

        if (sheerPlan != null)
        {
            float bilgeBowZ = sheerPlan.GetBilgeBowZ();
            float bilgeSternZ = sheerPlan.GetBilgeSternZ();

            // Forward stem profile convergences
            if (z >= sheerPlan.cachedKeelBowPoint.z)
            {
                float stemY = sheerPlan.GetStemProfileYAtZ(z);
                keelY = stemY;
                if (z >= sheerPlan.cachedBowRisingEnd.z) { floorY = stemY; floorX = hw; }
                if (z >= bilgeBowZ) { bilgeY = stemY; bilgeX = hw; }
                if (z >= sheerPlan.cachedHobBowIntersect.z) { hobY = stemY; hobX = hw; }
            }
            // Aft stern profile convergences
            else if (z <= sheerPlan.cachedKeelSternPoint.z)
            {
                float sternY = sheerPlan.GetSternProfileYAtZ(z);
                keelY = sternY;
                if (z <= sheerPlan.cachedSternRisingEnd.z) { floorY = sternY; floorX = hw; }
                if (z <= bilgeSternZ) { bilgeY = sternY; bilgeX = hw; }
                if (z <= sheerPlan.cachedHobSternIntersect.z) { hobY = sternY; hobX = hw; }
            }
        }

        // Floor point (p1) X position directly follows the Floor line from BreadthPlan (clamped to keel half-width hw)
        floorX = Mathf.Max(floorX, hw);

        // Bilge point (p2) X position directly follows the Bilge line from BreadthPlan (clamped between keel half-width hw and hobX)
        bilgeX = Mathf.Clamp(bilgeX, hw, hobX);

        // Restriction 4: Non-sagging Bilge Y height restriction (bilgeY must not dip below floorY)
        Vector3 checkP1 = new Vector3(floorX, floorY, z);
        Vector3 checkP2 = new Vector3(bilgeX, bilgeY, z);
        Vector3 checkP3 = new Vector3(hobX, hobY, z);
        float d13_y = Vector3.Distance(checkP1, checkP3);
        float d12_y = Vector3.Distance(checkP1, checkP2);
        if (d13_y > 0.0001f && (hobY - floorY) > 0.0001f)
        {
            float minBilgeY = floorY + (hobY - floorY) * (d12_y / (3f * d13_y));
            bilgeY = Mathf.Max(bilgeY, minBilgeY);
        }

        // Restriction 5: Tumblehome / Topsides boundary restriction (sheerX / p4 must not narrow by more than 40% of HOB max breadth p3)
        float minTumblehomeX = hobX * 0.60f;
        sheerX = Mathf.Max(sheerX, minTumblehomeX);

        SectionNodes nodes = new SectionNodes();
        nodes.p0 = new Vector3(sideSign * hw, keelY, z);
        nodes.p1 = new Vector3(sideSign * floorX, floorY, z);
        nodes.p2 = new Vector3(sideSign * bilgeX, bilgeY, z);
        nodes.p3 = new Vector3(sideSign * hobX, hobY, z);
        nodes.p4 = new Vector3(sideSign * sheerX, sheerY, z);
        return nodes;
    }

    /// <summary>
    /// Evaluates a 3D section point at parameter u [0, 1] across the 5 nodes using G1 Bi-Arc Tangent Sweeps.
    /// u [0.00, 0.25] = Keel Flat (p0 -> p1)
    /// u [0.25, 0.50] = Futtock Sweep Arc (p1 -> p2)
    /// u [0.50, 0.75] = Reconciling Sweep Arc (p2 -> p3)
    /// u [0.75, 1.00] = Top Timber Arc (p3 -> p4)
    /// </summary>
    /// <summary>
    /// Evaluates a 3D section point at parameter u [0, 1] across the 5 nodes using the active Curve Mode evaluator.
    /// </summary>
    public Vector3 EvaluateBodySectionAtU(float u, SectionNodes nodes)
    {
        Vector3 pt = GetEvaluator(curveMode).EvaluateSectionPoint(u, nodes, floorHollow, splineTension);

        // Restriction 2: Section curve points sweeping toward the Keel (p0) can never cross inward past the vertical line extending upward from the Keel point (p0)
        float sideSign = Mathf.Sign(nodes.p3.x);
        float keelX = nodes.p0.x;
        if (sideSign > 0f)
        {
            pt.x = Mathf.Max(keelX, pt.x);
        }
        else if (sideSign < 0f)
        {
            pt.x = Mathf.Min(keelX, pt.x);
        }
        return pt;
    }

    /// <summary>
    /// Evaluates 3D section point at station Z for parameter u [0, 1] on given side.
    /// </summary>
    public Vector3 EvaluateBodySectionAtZ(float z, float side, float u)
    {
        SectionNodes nodes = GetSectionNodesAtZ(z, side);
        return EvaluateBodySectionAtU(u, nodes);
    }

    private Vector3 EvaluateBiArcSegment(Vector3 pA, Vector3 tA, Vector3 pB, Vector3 tB, float t)
    {
        t = Mathf.Clamp01(t);
        Vector3 d = pB - pA;
        float dist = d.magnitude;
        if (dist <= 0.0001f) return pA;

        Vector3 control;
        float denom = tA.x * tB.y - tA.y * tB.x;
        if (Mathf.Abs(denom) > 0.0001f)
        {
            float s = ((pB.x - pA.x) * tB.y - (pB.y - pA.y) * tB.x) / denom;
            s = Mathf.Clamp(s, 0.01f, dist * 1.5f);
            control = pA + tA * s;
        }
        else
        {
            control = (pA + pB) * 0.5f;
        }

        float u = 1f - t;
        return (u * u * pA) + (2f * u * t * control) + (t * t * pB);
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || breadthPlan == null) return;
        UpdateCachedCurves();

        // Keel Block
        float hw = keelWidth / 2f;
        float keelTopY = sheerPlan.GetKeelYAtZ(0f);
        float keelThickness = sheerPlan.keelThickness;
        float keelBottomY = keelTopY - keelThickness;

        Gizmos.color = new Color(0.5f, 0.25f, 0f);
        Gizmos.DrawLine(new Vector3(-hw, keelTopY, 0), new Vector3(-hw, keelBottomY, 0));
        Gizmos.DrawLine(new Vector3(hw, keelTopY, 0), new Vector3(hw, keelBottomY, 0));
        Gizmos.DrawLine(new Vector3(-hw, keelBottomY, 0), new Vector3(hw, keelBottomY, 0));
        Gizmos.DrawLine(new Vector3(-hw, keelTopY, 0), new Vector3(hw, keelTopY, 0));

        // Draw Starboard (+1) and Port (-1) Master Frame Section
        DrawMasterSectionHalf(1f, starboardColor);
        DrawMasterSectionHalf(-1f, portColor);
    }

    public Color GetCurveModeColor(SectionCurveMode mode)
    {
        switch (mode)
        {
            case SectionCurveMode.FairSplineBatten: return Color.green;
            case SectionCurveMode.BiArcTangentSweeps: return Color.red;
            case SectionCurveMode.AkimaSpline: return Color.yellow;
            case SectionCurveMode.NurbsBSpline: return Color.cyan;
            case SectionCurveMode.QuarticBezier: return Color.magenta;
            default: return Color.white;
        }
    }

    private void DrawMasterSectionHalf(float sideSign, Color color)
    {
        SectionNodes nodes = GetSectionNodesAtZ(0f, sideSign);
        const int steps = 60;

        // Render Comparison Overlay Curves (if enabled)
        if (showComparisonOverlay)
        {
            SectionCurveMode[] modes = (SectionCurveMode[])System.Enum.GetValues(typeof(SectionCurveMode));
            foreach (var mode in modes)
            {
                if (mode == curveMode) continue;
                ISectionCurveEvaluator altEval = GetEvaluator(mode);
                Color overlayColor = GetCurveModeColor(mode);
                overlayColor.a = 0.45f;
                Gizmos.color = overlayColor;

                Vector3 prevAlt = altEval.EvaluateSectionPoint(0f, nodes, floorHollow, splineTension);
                for (int i = 1; i <= steps; i++)
                {
                    float u = (float)i / steps;
                    Vector3 currAlt = altEval.EvaluateSectionPoint(u, nodes, floorHollow, splineTension);
                    Gizmos.DrawLine(prevAlt, currAlt);
                    prevAlt = currAlt;
                }
            }
        }

        // Render Active Main Section Curve
        Vector3 prevPt = nodes.p0;
        Gizmos.color = color;
        for (int i = 1; i <= steps; i++)
        {
            float u = (float)i / steps;
            Vector3 currPt = EvaluateBodySectionAtU(u, nodes);
            Gizmos.DrawLine(prevPt, currPt);
            prevPt = currPt;
        }

        // Draw the 5 Master Node Spheres
        Gizmos.color = Color.yellow; Gizmos.DrawSphere(nodes.p0, 0.14f); // Keel
        Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawSphere(nodes.p1, 0.14f); // Floor
        Gizmos.color = new Color(0.1f, 0.9f, 0.4f); Gizmos.DrawSphere(nodes.p2, 0.14f); // Diagonal Bilge
        Gizmos.color = Color.cyan; Gizmos.DrawSphere(nodes.p3, 0.14f); // HOB
        Gizmos.color = Color.magenta; Gizmos.DrawSphere(nodes.p4, 0.14f); // Sheer

#if UNITY_EDITOR
        // Render Dashed Lines for Pure Polygonal Hull Skeleton (Lower & Upper Moulds)
        if (showPolygonSkeleton)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.55f);
            Handles.DrawDottedLine(nodes.p0, nodes.p1, 4f); // Keel -> Floor
            Handles.DrawDottedLine(nodes.p1, nodes.p2, 4f); // Floor -> Bilge
            Handles.DrawDottedLine(nodes.p2, nodes.p3, 4f); // Bilge -> HOB
            Handles.DrawDottedLine(nodes.p3, nodes.p4, 4f); // HOB -> Sheer

            // Draw Floor (p1) to HOB (p3) Bilge Inward Boundary Constraint Line
            Handles.color = new Color(1f, 0.45f, 0f, 0.5f);
            Handles.DrawDottedLine(nodes.p1, nodes.p3, 2f);

            // Draw Keel (p0) Vertical Upward Floor-to-Keel Non-Crossing Boundary Line
            Handles.color = new Color(1f, 0.25f, 0.25f, 0.5f);
            Handles.DrawDottedLine(nodes.p0, nodes.p0 + new Vector3(0f, 15f, 0f), 2f);

            // Draw Vertical HOB (p3) Downward Max Breadth Boundary Line
            Handles.color = new Color(0.1f, 0.8f, 0.9f, 0.5f);
            Handles.DrawDottedLine(nodes.p3, nodes.p3 - new Vector3(0f, 15f, 0f), 2f);

            // Draw Non-Sagging Bilge Minimum Height Boundary Line
            float d13_g = Vector3.Distance(nodes.p1, nodes.p3);
            float d12_g = Vector3.Distance(nodes.p1, nodes.p2);
            if (d13_g > 0.0001f)
            {
                float minY = nodes.p1.y + (nodes.p3.y - nodes.p1.y) * (d12_g / (3f * d13_g));
                Handles.color = new Color(1f, 0.8f, 0.1f, 0.5f);
                Handles.DrawDottedLine(new Vector3(0f, minY, nodes.p0.z), new Vector3(nodes.p3.x * 1.2f, minY, nodes.p0.z), 2f);
            }

            // Draw Tumblehome / Topsides Minimum Boundary Line
            float minTTX = Mathf.Abs(nodes.p3.x) * 0.60f * Mathf.Sign(nodes.p3.x);
            Handles.color = new Color(0.9f, 0.2f, 0.9f, 0.5f);
            Handles.DrawDottedLine(new Vector3(minTTX, nodes.p3.y, nodes.p0.z), new Vector3(minTTX, nodes.p4.y + 2f, nodes.p0.z), 2f);
        }
#endif

        // Draw Tangent Control Arm Handle Visualization
        ISectionCurveEvaluator currentEval = GetEvaluator(curveMode);
        if (showControlArms && currentEval.UsesControlArms)
        {
            Vector3 P0 = nodes.p0;
            Vector3 P1 = nodes.p1;
            Vector3 P2 = nodes.p2;
            Vector3 P3 = nodes.p3;
            Vector3 P4 = nodes.p4;

            float d01 = Vector3.Distance(P0, P1);
            float d12 = Vector3.Distance(P1, P2);
            float d23 = Vector3.Distance(P2, P3);
            float d34 = Vector3.Distance(P3, P4);

            Vector3 T0 = currentEval.GetNodeTangent(0, nodes);
            Vector3 T1 = currentEval.GetNodeTangent(1, nodes);
            Vector3 T2 = currentEval.GetNodeTangent(2, nodes);
            Vector3 T3 = currentEval.GetNodeTangent(3, nodes);
            Vector3 T4 = currentEval.GetNodeTangent(4, nodes);

            DrawControlArmHandle(P0, T0, "T0 (Keel)", Color.yellow, d01, false, true);
            DrawControlArmHandle(P1, T1, "T1 (Floor)", new Color(1f, 0.5f, 0f), (d01 + d12) * 0.5f, true, true);
            DrawControlArmHandle(P2, T2, "T2 (Bilge)", new Color(0.1f, 0.9f, 0.4f), (d12 + d23) * 0.5f, true, true);
            DrawControlArmHandle(P3, T3, "T3 (HOB)", Color.cyan, (d23 + d34) * 0.5f, true, true);
            DrawControlArmHandle(P4, T4, "T4 (Sheer)", Color.magenta, d34, true, false);
        }

#if UNITY_EDITOR
        if (showLabels && sideSign > 0)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.fontSize = 11;
            labelStyle.fontStyle = FontStyle.Bold;

            Handles.Label(nodes.p0 + new Vector3(0.08f, 0, 0), " p0: Keel Baseline", labelStyle);
            Handles.Label(nodes.p1 + new Vector3(0.08f, 0, 0), " p1: Floor Line", labelStyle);
            Handles.Label(nodes.p2 + new Vector3(0.08f, 0, 0), " p2: Diagonal Bilge", labelStyle);
            Handles.Label(nodes.p3 + new Vector3(0.08f, 0, 0), " p3: Max Breadth (HOB)", labelStyle);
            Handles.Label(nodes.p4 + new Vector3(0.08f, 0, 0), " p4: Top Timber (Sheer)", labelStyle);
        }
#endif
    }

    private void DrawControlArmHandle(Vector3 nodePos, Vector3 tangent, string label, Color color, float segDist = 1f, bool drawPrevArm = true, bool drawNextArm = true)
    {
        float tensionFactor = Mathf.Clamp(splineTension, 0.5f, 2.0f);
        float d = segDist * 0.4f * tensionFactor * controlArmLengthScale;
        Vector3 handleA = nodePos - tangent * d;
        Vector3 handleB = nodePos + tangent * d;

        Gizmos.color = new Color(color.r, color.g, color.b, 0.85f);
        
        if (drawPrevArm && drawNextArm)
        {
            Gizmos.DrawLine(handleA, handleB);
            Gizmos.DrawSphere(handleA, 0.05f);
            Gizmos.DrawSphere(handleB, 0.05f);
        }
        else if (drawNextArm)
        {
            Gizmos.DrawLine(nodePos, handleB);
            Gizmos.DrawSphere(handleB, 0.05f);
        }
        else if (drawPrevArm)
        {
            Gizmos.DrawLine(nodePos, handleA);
            Gizmos.DrawSphere(handleA, 0.05f);
        }

#if UNITY_EDITOR
        // Render Dotted Line along the Angle Bisector Normal (Perpendicular to Tangent Control Arm)
        Vector3 bisectorNormal = new Vector3(-tangent.y, tangent.x, 0f).normalized;
        Handles.color = new Color(color.r, color.g, color.b, 0.35f);
        Handles.DrawDottedLine(nodePos - bisectorNormal * (d * 0.75f), nodePos + bisectorNormal * (d * 0.75f), 2f);

        if (showLabels)
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = color;
            style.fontSize = 9;
            style.fontStyle = FontStyle.Bold;
            Vector3 labelPos = drawNextArm ? handleB : handleA;
            Handles.Label(labelPos + new Vector3(0.02f, 0.02f, 0), label, style);
        }
#endif
    }

    [Header("Interactive Node Handles")]
    [Tooltip("Enable interactive click-and-drag handles in Scene view to sculpt Floor (p1) and Bilge (p2) nodes directly on the Body Plan.")]
    public bool enableInteractiveNodeHandles = true;

    // Direct C# Proxy Properties to manipulate Floor Line (p1) and Bilge Line (p2) parameters directly from BodyPlan
    public float FloorRisingHeight
    {
        get => (sheerPlan != null) ? sheerPlan.midshipRisingHeight : 0.5f;
        set
        {
            if (sheerPlan != null)
            {
                sheerPlan.midshipRisingHeight = Mathf.Max(0f, value);
                sheerPlan.UpdateCachedValues();
                UpdateCachedCurves();
            }
        }
    }

    public float FloorWidth
    {
        get => (breadthPlan != null) ? breadthPlan.midshipFloorWidth : 2.0f;
        set
        {
            if (breadthPlan != null)
            {
                float hw = keelWidth / 2f;
                float maxB = (breadthPlan != null) ? breadthPlan.maxBeam : 5.0f;
                breadthPlan.midshipFloorWidth = Mathf.Clamp(value, hw + 0.01f, maxB - 0.01f);
                breadthPlan.UpdateCachedCurves();
                UpdateCachedCurves();
            }
        }
    }

    public float BilgeHeightRatio
    {
        get => (sheerPlan != null) ? sheerPlan.bilgeHeightRatio : 0.5f;
        set
        {
            if (sheerPlan != null)
            {
                sheerPlan.bilgeHeightRatio = Mathf.Clamp(value, 0.05f, 0.95f);
                sheerPlan.UpdateCachedValues();
                UpdateCachedCurves();
            }
        }
    }

    public float BilgeWidthRatio
    {
        get => (breadthPlan != null) ? breadthPlan.bilgeWidthRatio : 0.5f;
        set
        {
            if (breadthPlan != null)
            {
                breadthPlan.bilgeWidthRatio = Mathf.Clamp(value, 0.05f, 0.95f);
                breadthPlan.UpdateCachedCurves();
                UpdateCachedCurves();
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BodyPlan))]
public class BodyPlanEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BodyPlan plan = (BodyPlan)target;
        if (plan.sheerPlan == null || plan.breadthPlan == null) return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Midship Master Shape Controls (Direct Proxy)", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        float floorY = plan.FloorRisingHeight;
        float newFloorY = EditorGUILayout.FloatField(new GUIContent("Floor Rising Height (Y)", "Midship Floor Line height on SheerPlan"), floorY);

        float floorX = plan.FloorWidth;
        float newFloorX = EditorGUILayout.FloatField(new GUIContent("Floor Half-Width (X)", "Midship Floor Line half-width on BreadthPlan"), floorX);

        float bilgeYRatio = plan.BilgeHeightRatio;
        float newBilgeYRatio = EditorGUILayout.Slider(new GUIContent("Diagonal Bilge Height Ratio (Y)", "Bilge Line height ratio on SheerPlan (0.05 - 0.95)"), bilgeYRatio, 0.05f, 0.95f);

        float bilgeXRatio = plan.BilgeWidthRatio;
        float newBilgeXRatio = EditorGUILayout.Slider(new GUIContent("Diagonal Bilge Width Ratio (X)", "Bilge Line width ratio on BreadthPlan (0.05 - 0.95)"), bilgeXRatio, 0.05f, 0.95f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObjects(new Object[] { plan.sheerPlan, plan.breadthPlan, plan }, "Modify BodyPlan Midship Shapes");
            plan.FloorRisingHeight = newFloorY;
            plan.FloorWidth = newFloorX;
            plan.BilgeHeightRatio = newBilgeYRatio;
            plan.BilgeWidthRatio = newBilgeXRatio;
            EditorUtility.SetDirty(plan.sheerPlan);
            EditorUtility.SetDirty(plan.breadthPlan);
            EditorUtility.SetDirty(plan);
        }
    }

    private void OnSceneGUI()
    {
        BodyPlan plan = (BodyPlan)target;
        if (plan.sheerPlan == null || plan.breadthPlan == null || !plan.enableInteractiveNodeHandles) return;

        SectionNodes stbdNodes = plan.GetSectionNodesAtZ(0f, 1f);

        // Floor Point (p1) Interactive Scene Handle
        Handles.color = new Color(1f, 0.5f, 0f, 0.9f);
        EditorGUI.BeginChangeCheck();
        Vector3 newP1 = Handles.FreeMoveHandle(stbdNodes.p1, 0.15f, Vector3.zero, Handles.SphereHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObjects(new Object[] { plan.sheerPlan, plan.breadthPlan, plan }, "Drag Midship Floor Point");
            float hw = plan.keelWidth / 2f;
            float minFloorX = hw;
            float d02 = Vector3.Distance(stbdNodes.p0, stbdNodes.p2);
            float d01 = Vector3.Distance(stbdNodes.p0, stbdNodes.p1);
            if (d02 > 0.0001f)
            {
                float tangentOffset = (stbdNodes.p2.x - hw) / d02 * (d01 / 3f);
                minFloorX = hw + tangentOffset;
            }
            plan.FloorWidth = Mathf.Max(minFloorX, Mathf.Abs(newP1.x));
            plan.FloorRisingHeight = Mathf.Max(0f, newP1.y);
            EditorUtility.SetDirty(plan.sheerPlan);
            EditorUtility.SetDirty(plan.breadthPlan);
            EditorUtility.SetDirty(plan);
        }

        // Diagonal Bilge Point (p2) Interactive Scene Handle
        Handles.color = new Color(0.1f, 0.9f, 0.4f, 0.9f);
        EditorGUI.BeginChangeCheck();
        Vector3 newP2 = Handles.FreeMoveHandle(stbdNodes.p2, 0.15f, Vector3.zero, Handles.SphereHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObjects(new Object[] { plan.sheerPlan, plan.breadthPlan, plan }, "Drag Midship Bilge Point");
            float p1x = stbdNodes.p1.x;
            float p3x = stbdNodes.p3.x;
            float p1y = stbdNodes.p1.y;
            float p3y = stbdNodes.p3.y;

            if (Mathf.Abs(p3x - p1x) > 0.001f)
            {
                // Clamp handle X to not go inward past line p1 -> p3 nor outward past HOB vertical boundary
                float dy = p3y - p1y;
                float tY = (Mathf.Abs(dy) > 0.0001f) ? Mathf.Clamp01((newP2.y - p1y) / dy) : 0.5f;
                float minP2x = Mathf.Lerp(p1x, p3x, tY);

                float d13 = Vector3.Distance(stbdNodes.p1, stbdNodes.p3);
                float d23 = Vector3.Distance(newP2, stbdNodes.p3);
                float maxP2x = p3x;
                if (d13 > 0.0001f)
                {
                    float offset = (p3x - p1x) / d13 * (d23 / 3f);
                    maxP2x = p3x - offset;
                }

                float clampedX = Mathf.Clamp(Mathf.Abs(newP2.x), minP2x, maxP2x);

                float xRatio = (clampedX - p1x) / (p3x - p1x);
                plan.BilgeWidthRatio = Mathf.Clamp(xRatio, 0.05f, 0.95f);
            }

            if (Mathf.Abs(p3y - p1y) > 0.001f)
            {
                float dy = p3y - p1y;
                float d13 = Vector3.Distance(stbdNodes.p1, stbdNodes.p3);
                float d12 = Vector3.Distance(stbdNodes.p1, newP2);
                float minBilgeY = p1y;
                if (d13 > 0.0001f)
                {
                    minBilgeY = p1y + dy * (d12 / (3f * d13));
                }

                float clampedY = Mathf.Max(newP2.y, minBilgeY);
                float yRatio = (clampedY - p1y) / dy;
                plan.BilgeHeightRatio = Mathf.Clamp(yRatio, 0.05f, 0.95f);
            }

            EditorUtility.SetDirty(plan.sheerPlan);
            EditorUtility.SetDirty(plan.breadthPlan);
            EditorUtility.SetDirty(plan);
        }
    }
}
#endif
