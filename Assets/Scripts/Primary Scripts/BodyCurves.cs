using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// BodyCurves evaluates 3D hull section curves using a NURBS B-Spline formulation.
/// Harmonizes seamlessly with SheerCurves (2D profile lines), BreadthCurves (Max Breadth line),
/// and FloorCurves (Floor line).
/// </summary>
[ExecuteInEditMode]
public class BodyCurves : MonoBehaviour
{
    [Tooltip("Baseline keel thickness for visualization.")]
    public float keelThickness = 0.4f;

    [Header("Primary Curve References (Harmonized)")]
    [Tooltip("Reference to SheerCurves for profile lines (Stem, Stern, Keel, Sheer).")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to BreadthCurves for Max Breadth (p3) 3D spatial points.")]
    public BreadthCurves breadthCurves;

    [Tooltip("Reference to FloorCurves for Floor Line (p1) 3D spatial points.")]
    public FloorCurves floorCurves;

    [Tooltip("Reference to BilgeCurves for Diagonal Bilge Line (p2) 3D spatial points.")]
    public BilgeCurves bilgeCurves;

    [Header("Keel & Master Dimensions")]
    [Tooltip("Physical Keel half-width offset (starts at keelWidth / 2).")]
    public float keelWidth = 0.5f;

    [Header("Floor Hollow (Keel to Floor Arc)")]
    [Tooltip("Curvature of the Floor Hollow between Keel (p0) and Floor (p1). (0 = Straight deadrise, 1 = Full hollow curve).")]
    [Range(0f, 1f)] public float floorHollow = 0.5f;

    [Header("Diagonal Bilge Positioning")]
    [Tooltip("Relative height ratio of the Diagonal Bilge point (p2) between Floor (p1) and Max Breadth (p3).")]
    [Range(0.05f, 0.95f)] public float bilgeHeightRatio = 0.50f;

    [Tooltip("Relative width ratio of the Diagonal Bilge point (p2) between Floor (p1) and Max Breadth (p3).")]
    [Range(0.05f, 0.95f)] public float bilgeWidthRatio = 0.50f;

    [Tooltip("Minimum outward convex arc factor between Floor (p1) and Max Breadth (p3) to prevent hollow bilge concavity.")]
    [Range(0f, 0.5f)] public float minConvexArcFactor = 0.20f;

    [Header("Gizmo Visualization Settings")]
    public bool showLabels = true;
    public bool showPolygonSkeleton = true;
    public bool showControlArms = true;
    [Range(0.1f, 2f)] public float controlArmLengthScale = 0.6f;
    public Color starboardColor = Color.green;
    public Color portColor = Color.red;

    private readonly NurbsSectionEvaluator nurbsEvaluator = new NurbsSectionEvaluator();

    [HideInInspector] public Vector3 cachedP0;
    [HideInInspector] public Vector3 cachedP1;
    [HideInInspector] public Vector3 cachedP2;
    [HideInInspector] public Vector3 cachedP3;
    [HideInInspector] public Vector3 cachedP4;
    [HideInInspector] public int changeVersion = 0;

    private void OnEnable()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        UpdateCachedCurves();
    }

    public void FindReferences()
    {
        if (sheerCurves == null) sheerCurves = GetComponentInParent<SheerCurves>();
        if (sheerCurves == null) sheerCurves = FindFirstObjectByType<SheerCurves>();

        if (breadthCurves == null) breadthCurves = GetComponentInParent<BreadthCurves>();
        if (breadthCurves == null) breadthCurves = FindFirstObjectByType<BreadthCurves>();

        if (floorCurves == null) floorCurves = GetComponentInParent<FloorCurves>();
        if (floorCurves == null) floorCurves = FindFirstObjectByType<FloorCurves>();

        if (bilgeCurves == null) bilgeCurves = GetComponentInParent<BilgeCurves>();
        if (bilgeCurves == null) bilgeCurves = FindFirstObjectByType<BilgeCurves>();
    }

    public virtual void UpdateCachedCurves()
    {
        FindReferences();

        if (sheerCurves != null) sheerCurves.UpdateCachedProfile();
        if (breadthCurves != null) breadthCurves.UpdateCachedCurves();
        if (floorCurves != null) floorCurves.UpdateCachedCurves();

        float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
        SectionNodes master = GetSectionNodesAtZ(midZ, 1f);
        cachedP0 = master.p0;
        cachedP1 = master.p1;
        cachedP2 = master.p2;
        cachedP3 = master.p3;
        cachedP4 = master.p4;

        if (bilgeCurves != null)
        {
            bilgeCurves.bilgeWidthRatio = bilgeWidthRatio;
            bilgeCurves.bilgeHeightRatio = bilgeHeightRatio;
            bilgeCurves.UpdateCachedCurves();
        }

        RibbandCurves ribbandCurves = GetComponentInParent<RibbandCurves>();
        if (ribbandCurves == null) ribbandCurves = FindFirstObjectByType<RibbandCurves>();
        if (ribbandCurves != null)
        {
            ribbandCurves.UpdateCachedCurves();
        }

        KeelBreadth keelBreadth = GetComponentInParent<KeelBreadth>();
        if (keelBreadth == null) keelBreadth = FindFirstObjectByType<KeelBreadth>();
        if (keelBreadth != null)
        {
            keelBreadth.UpdateCachedCurves();
        }

        BackboneCurves backboneCurves = GetComponentInParent<BackboneCurves>();
        if (backboneCurves == null) backboneCurves = FindFirstObjectByType<BackboneCurves>();
        if (backboneCurves != null)
        {
            backboneCurves.UpdateCachedCurves();
        }

        FashionCurves fashionCurves = GetComponentInParent<FashionCurves>();
        if (fashionCurves == null) fashionCurves = FindFirstObjectByType<FashionCurves>();
        if (fashionCurves != null)
        {
            fashionCurves.UpdateCachedCurves();
        }

        changeVersion++;
    }

    /// <summary>
    /// Samples the 5 paired 3D structural nodes at station Z for specified side (+1 Starboard, -1 Port).
    /// Seamlessly harmonized with SheerCurves, BreadthCurves, FloorCurves, and BilgeCurves.
    /// </summary>
    public SectionNodes GetSectionNodesAtZ(float z, float side)
    {
        FindReferences();

        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float hw = keelWidth / 2f;

        // 1. Keel Point (p0): Baseline height from SheerCurves
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        // 2. Floor Point (p1): Bound directly to FloorCurves Midship Apex (fwdP2)
        float floorX = (floorCurves != null) ? floorCurves.fwdP2.x : (hw + 1f);
        float floorY = (floorCurves != null) ? floorCurves.fwdP2.y : (keelY + 0.5f);
        if (floorCurves != null && Mathf.Abs(z - floorCurves.fwdP2.z) > 0.01f)
        {
            floorX = floorCurves.GetFloorXAtZ(z);
            floorY = floorCurves.GetFloorYAtZ(z);
        }
        floorY = Mathf.Max(floorY, keelY + 0.05f); // Ensure floor stays strictly above keel baseline Y

        // 3. Max Breadth / HOB Point (p3): Bound directly to BreadthCurves Midship Apex (fwdP3)
        float hobX = (breadthCurves != null) ? breadthCurves.fwdP3.x : (floorX + 2f);
        float hobY = (breadthCurves != null) ? breadthCurves.fwdP3.y : (floorY + 2f);
        if (breadthCurves != null && Mathf.Abs(z - breadthCurves.fwdP3.z) > 0.01f)
        {
            hobX = breadthCurves.GetBreadthXAtZ(z);
            hobY = breadthCurves.GetBreadthYAtZ(z);
        }
        hobX = Mathf.Max(hobX, hw + 0.1f);
        hobY = Mathf.Max(hobY, floorY);

        float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
        bool isMidshipStation = Mathf.Abs(z - midZ) < 0.01f;

        // --- ORGANIC TANGENTIAL FLOOR BOUNDARY (relative to Keel P0 and Bilge P2) ---
        float bilgeEstX, bilgeEstY;
        if (!isMidshipStation && bilgeCurves != null)
        {
            bilgeEstX = bilgeCurves.GetBilgeXAtZ(z);
            bilgeEstY = bilgeCurves.GetBilgeYAtZ(z);
        }
        else
        {
            bilgeEstX = Mathf.Lerp(floorX, hobX, Mathf.Clamp01(bilgeWidthRatio));
            bilgeEstY = Mathf.Lerp(floorY, hobY, Mathf.Clamp01(bilgeHeightRatio));
        }
        Vector3 p0 = new Vector3(hw, keelY, z);
        Vector3 p1Est = new Vector3(floorX, floorY, z);
        Vector3 p2Est = new Vector3(bilgeEstX, bilgeEstY, z);

        float d01 = Vector3.Distance(p0, p1Est);
        float d02 = Mathf.Max(0.001f, Vector3.Distance(p0, p2Est));

        // Organic minimum floor X bound to clear vertical keel face (X >= hw) without S-bend or flattening
        float minOrganicFloorX = hw + ((d01 / (3f * d02)) * (bilgeEstX - hw));
        floorX = Mathf.Clamp(floorX, minOrganicFloorX, hobX);

        // 4. Diagonal Bilge Point (p2): Bound directly to BilgeCurves if assigned and not midship, or calculated organically
        float bilgeX, bilgeY;
        if (!isMidshipStation && bilgeCurves != null)
        {
            Vector3 bilgePt = bilgeCurves.GetBilgePointAtZ(z);
            bilgeX = bilgePt.x;
            bilgeY = bilgePt.y;
        }
        else
        {
            bilgeX = Mathf.Lerp(floorX, hobX, Mathf.Clamp01(bilgeWidthRatio));
            bilgeY = Mathf.Lerp(floorY, hobY, Mathf.Clamp01(bilgeHeightRatio));

            // Non-sagging Bilge Y height restriction (bilgeY must not dip below floorY)
            Vector3 checkP1 = new Vector3(floorX, floorY, z);
            Vector3 checkP2 = new Vector3(bilgeX, bilgeY, z);
            Vector3 checkP3 = new Vector3(hobX, hobY, z);
            float d13 = Vector3.Distance(checkP1, checkP3);
            float d12 = Vector3.Distance(checkP1, checkP2);
            if (d13 > 0.0001f && (hobY - floorY) > 0.0001f)
            {
                float minBilgeY = floorY + (hobY - floorY) * (d12 / (3f * d13));
                bilgeY = Mathf.Max(bilgeY, minBilgeY);
            }

            // --- ORGANIC BILGE BOUNDARY RESTRICTIONS ---
            float heightSpan = hobY - floorY;
            float widthSpan = hobX - floorX;
            if (heightSpan > 0.0001f && widthSpan > 0.0001f)
            {
                float tHeight = Mathf.Clamp01((bilgeY - floorY) / heightSpan);
                float chordX = Mathf.Lerp(floorX, hobX, tHeight);
                float minConvexArcX = chordX + 4f * tHeight * (1f - tHeight) * widthSpan * minConvexArcFactor;
                float d23 = Vector3.Distance(new Vector3(bilgeX, bilgeY, z), checkP3);
                float d13Len = Mathf.Max(0.001f, d13);
                float maxOrganicBilgeX = hobX - ((d23 / (3f * d13Len)) * widthSpan);
                maxOrganicBilgeX = Mathf.Max(minConvexArcX + 0.01f, maxOrganicBilgeX);
                bilgeX = Mathf.Clamp(bilgeX, minConvexArcX, maxOrganicBilgeX);
            }
            else
            {
                bilgeX = Mathf.Clamp(bilgeX, floorX, hobX);
            }
        }

        // 5. Top Timber / Sheer Point (p4): Height from SheerCurves, width from BreadthCurves
        float sheerY = (sheerCurves != null) ? (sheerCurves.keelY + sheerCurves.stemHeight) : (hobY + 1.5f);
        float sheerX = (breadthCurves != null) ? breadthCurves.GetTheoreticalBreadthXAtZ(z) : hobX;

        // Tumblehome boundary restriction (p4 sheerX cannot narrow by more than 40% of HOB max breadth)
        float minTumblehomeX = hobX * 0.60f;
        sheerX = Mathf.Max(sheerX, minTumblehomeX);

        // Profile convergence at Stem and Stern
        if (sheerCurves != null)
        {
            if (z >= sheerCurves.stemP0.z)
            {
                float stemY = sheerCurves.GetStemProfileZAtY(keelY);
                if (z >= sheerCurves.stemP3.z)
                {
                    floorX = hw;
                    bilgeX = hw;
                    hobX = hw;
                    sheerX = hw;
                }
            }
            else if (z <= sheerCurves.sternP0.z)
            {
                if (z <= sheerCurves.sternP3.z)
                {
                    floorX = hw;
                    bilgeX = hw;
                    hobX = hw;
                    sheerX = hw;
                }
            }
        }

        SectionNodes nodes = new SectionNodes();
        nodes.p0 = new Vector3(sideSign * hw, keelY, z);
        nodes.p1 = new Vector3(sideSign * floorX, floorY, z);
        nodes.p2 = new Vector3(sideSign * bilgeX, bilgeY, z);
        nodes.p3 = new Vector3(sideSign * hobX, hobY, z);
        nodes.p4 = new Vector3(sideSign * sheerX, sheerY, z);
        return nodes;
    }

    /// <summary>
    /// Evaluates a 3D section point at parameter u [0, 1] across the 5 nodes using NURBS B-Spline.
    /// </summary>
    public Vector3 EvaluateBodySectionAtU(float u, SectionNodes nodes)
    {
        Vector3 pt = nurbsEvaluator.EvaluateSectionPoint(u, nodes, floorHollow, 1.0f);

        // Keel inward crossing protection (allows natural organic spline shape without hard line flattening)
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

    private void OnDrawGizmos()
    {
        FindReferences();
        UpdateCachedCurves();

        // Keel Block
        float hw = keelWidth / 2f;
        float keelTopY = (sheerCurves != null) ? sheerCurves.keelY : 0f;
        float keelBottomY = keelTopY - keelThickness;

        float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;

        Gizmos.color = new Color(0.5f, 0.25f, 0f);
        Gizmos.DrawLine(new Vector3(-hw, keelTopY, midZ), new Vector3(-hw, keelBottomY, midZ));
        Gizmos.DrawLine(new Vector3(hw, keelTopY, midZ), new Vector3(hw, keelBottomY, midZ));
        Gizmos.DrawLine(new Vector3(-hw, keelBottomY, midZ), new Vector3(hw, keelBottomY, midZ));
        Gizmos.DrawLine(new Vector3(-hw, keelTopY, midZ), new Vector3(hw, keelTopY, midZ));

        // Draw Master Section (Starboard +1, Port -1) at Midship Apex Z station
        DrawMasterSectionHalf(midZ, 1f, starboardColor);
        DrawMasterSectionHalf(midZ, -1f, portColor);
    }

    private void DrawMasterSectionHalf(float z, float sideSign, Color color)
    {
        SectionNodes nodes = GetSectionNodesAtZ(z, sideSign);
        const int steps = 60;

        Vector3 prevPt = nodes.p0;
        Gizmos.color = color;
        for (int i = 1; i <= steps; i++)
        {
            float u = (float)i / steps;
            Vector3 currPt = EvaluateBodySectionAtU(u, nodes);
            Gizmos.DrawLine(prevPt, currPt);
            prevPt = currPt;
        }

        // Draw 5 Structural Node Spheres
        Gizmos.color = Color.yellow; Gizmos.DrawSphere(nodes.p0, 0.14f); // Keel
        Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawSphere(nodes.p1, 0.14f); // Floor
        Gizmos.color = new Color(0.1f, 0.9f, 0.4f); Gizmos.DrawSphere(nodes.p2, 0.14f); // Diagonal Bilge
        Gizmos.color = Color.cyan; Gizmos.DrawSphere(nodes.p3, 0.14f); // Max Breadth (HOB)
        Gizmos.color = Color.magenta; Gizmos.DrawSphere(nodes.p4, 0.14f); // Sheer

#if UNITY_EDITOR
        if (showPolygonSkeleton)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.55f);
            Handles.DrawDottedLine(nodes.p0, nodes.p1, 4f);
            Handles.DrawDottedLine(nodes.p1, nodes.p2, 4f);
            Handles.DrawDottedLine(nodes.p2, nodes.p3, 4f);
            Handles.DrawDottedLine(nodes.p3, nodes.p4, 4f);

            // Draw Outer Vertical Max Breadth Boundary Wall (Orange Dotted Line)
            Handles.color = new Color(1f, 0.5f, 0.1f, 0.75f);
            Vector3 wallTop = new Vector3(sideSign * Mathf.Abs(nodes.p3.x), nodes.p3.y, z);
            Vector3 wallBottom = new Vector3(sideSign * Mathf.Abs(nodes.p3.x), nodes.p0.y, z);
            Handles.DrawDottedLine(wallTop, wallBottom, 4f);

            // Draw Minimum Convex Bilge Arc Boundary (Yellow Dotted Arc)
            Handles.color = new Color(1f, 0.85f, 0.1f, 0.7f);
            int arcSteps = 15;
            Vector3 prevArcPt = nodes.p1;
            float hSpan = nodes.p3.y - nodes.p1.y;
            float wSpan = Mathf.Abs(nodes.p3.x) - Mathf.Abs(nodes.p1.x);
            float fX = Mathf.Abs(nodes.p1.x);
            float hX = Mathf.Abs(nodes.p3.x);

            if (hSpan > 0.0001f && wSpan > 0.0001f)
            {
                for (int i = 1; i <= arcSteps; i++)
                {
                    float t = i / (float)arcSteps;
                    float arcY = Mathf.Lerp(nodes.p1.y, nodes.p3.y, t);
                    float chordX = Mathf.Lerp(fX, hX, t);
                    float minArcX = chordX + 4f * t * (1f - t) * wSpan * minConvexArcFactor;
                    Vector3 currArcPt = new Vector3(sideSign * minArcX, arcY, z);
                    Handles.DrawDottedLine(prevArcPt, currArcPt, 3f);
                    prevArcPt = currArcPt;
                }
            }
        }

        if (showControlArms && nurbsEvaluator.UsesControlArms)
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

            Vector3 T0 = nurbsEvaluator.GetNodeTangent(0, nodes);
            Vector3 T1 = nurbsEvaluator.GetNodeTangent(1, nodes);
            Vector3 T2 = nurbsEvaluator.GetNodeTangent(2, nodes);
            Vector3 T3 = nurbsEvaluator.GetNodeTangent(3, nodes);
            Vector3 T4 = nurbsEvaluator.GetNodeTangent(4, nodes);

            DrawControlArmHandle(P0, T0, "T0 (Keel)", Color.yellow, d01, false, true);
            DrawControlArmHandle(P1, T1, "T1 (Floor)", new Color(1f, 0.5f, 0f), (d01 + d12) * 0.5f, true, true);
            DrawControlArmHandle(P2, T2, "T2 (Bilge)", new Color(0.1f, 0.9f, 0.4f), (d12 + d23) * 0.5f, true, true);
            DrawControlArmHandle(P3, T3, "T3 (HOB)", Color.cyan, (d23 + d34) * 0.5f, true, true);
            DrawControlArmHandle(P4, T4, "T4 (Sheer)", Color.magenta, d34, true, false);
        }

        if (showLabels && sideSign > 0)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.cyan;
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.fontSize = 11;
            labelStyle.fontStyle = FontStyle.Bold;

            Handles.Label(nodes.p0 + new Vector3(0.08f, 0, 0), " p0: Keel Baseline", labelStyle);
            Handles.Label(nodes.p1 + new Vector3(0.08f, 0, 0), " p1: Floor Curves Intersect", labelStyle);
            Handles.Label(nodes.p2 + new Vector3(0.08f, 0, 0), " p2: Diagonal Bilge", labelStyle);
            Handles.Label(nodes.p3 + new Vector3(0.08f, 0, 0), " p3: Breadth Curves Intersect", labelStyle);
            Handles.Label(nodes.p4 + new Vector3(0.08f, 0, 0), " p4: Top Timber (Sheer)", labelStyle);
        }
#endif
    }

    private void DrawControlArmHandle(Vector3 nodePos, Vector3 tangent, string label, Color color, float segDist = 1f, bool drawPrevArm = true, bool drawNextArm = true)
    {
        float d = segDist * 0.4f * controlArmLengthScale;
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
}

#if UNITY_EDITOR
[CustomEditor(typeof(BodyCurves))]
public class BodyCurvesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BodyCurves curves = (BodyCurves)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Harmonized Reference Status", EditorStyles.boldLabel);

        bool missingRef = false;
        if (curves.sheerCurves == null) { EditorGUILayout.HelpBox("SheerCurves reference is missing!", MessageType.Warning); missingRef = true; }
        if (curves.breadthCurves == null) { EditorGUILayout.HelpBox("BreadthCurves reference is missing!", MessageType.Warning); missingRef = true; }
        if (curves.floorCurves == null) { EditorGUILayout.HelpBox("FloorCurves reference is missing!", MessageType.Warning); missingRef = true; }

        if (missingRef)
        {
            if (GUILayout.Button("Auto-Find Scene References", GUILayout.Height(25)))
            {
                Undo.RecordObject(curves, "Find Scene References");
                curves.FindReferences();
                curves.UpdateCachedCurves();
                EditorUtility.SetDirty(curves);
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Live Master Node Coordinates", EditorStyles.boldLabel);
        float midZ = (curves.breadthCurves != null) ? curves.breadthCurves.fwdP3.z : 0f;
        SectionNodes nodes = curves.GetSectionNodesAtZ(midZ, 1f);

        EditorGUILayout.LabelField($"Station Z:", $"{midZ:F2}m");
        EditorGUILayout.LabelField($"p0 Keel Baseline:", $"X: {nodes.p0.x:F2}, Y: {nodes.p0.y:F2}");
        EditorGUILayout.LabelField($"p1 Floor Apex (FloorCurves):", $"X: {nodes.p1.x:F2}, Y: {nodes.p1.y:F2}");
        EditorGUILayout.LabelField($"p2 Diagonal Bilge:", $"X: {nodes.p2.x:F2}, Y: {nodes.p2.y:F2}");
        EditorGUILayout.LabelField($"p3 Max Breadth Apex (BreadthCurves):", $"X: {nodes.p3.x:F2}, Y: {nodes.p3.y:F2}");
        EditorGUILayout.LabelField($"p4 Sheer Top Timber:", $"X: {nodes.p4.x:F2}, Y: {nodes.p4.y:F2}");

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update Body Curves Profile", GUILayout.Height(30)))
        {
            Undo.RecordObject(curves, "Update Body Profile");
            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);
        }
    }

    private void OnSceneGUI()
    {
        BodyCurves curves = (BodyCurves)target;
        if (curves.floorCurves == null || curves.breadthCurves == null || curves.sheerCurves == null)
        {
            curves.FindReferences();
        }
        curves.UpdateCachedCurves();

        float midZ = (curves.breadthCurves != null) ? curves.breadthCurves.fwdP3.z : 0f;
        SectionNodes stbdNodes = curves.GetSectionNodesAtZ(midZ, 1f);

        EditorGUI.BeginChangeCheck();

        // 1. Interactive Handle for p1 (Floor Point) -> Updates FloorCurves maxFloorBeam & midshipHeightRatio
        Handles.color = new Color(1f, 0.5f, 0f, 0.95f);
        Vector3 newP1 = Handles.FreeMoveHandle(stbdNodes.p1, 0.18f, Vector3.zero, Handles.SphereHandleCap);

        Handles.color = Color.red;
        Vector3 xArrowP1 = Handles.Slider(stbdNodes.p1, Vector3.right, 0.8f, Handles.ArrowHandleCap, 0.05f);
        if (Mathf.Abs(xArrowP1.x - stbdNodes.p1.x) > 0.001f) newP1.x = xArrowP1.x;

        Handles.color = Color.green;
        Vector3 yArrowP1 = Handles.Slider(stbdNodes.p1, Vector3.up, 0.8f, Handles.ArrowHandleCap, 0.05f);
        if (Mathf.Abs(yArrowP1.y - stbdNodes.p1.y) > 0.001f) newP1.y = yArrowP1.y;

        // 2. Interactive Handle for p2 (Diagonal Bilge Point) -> Updates BodyCurves bilgeWidthRatio & bilgeHeightRatio (Z locked to midZ)
        Handles.color = new Color(0.1f, 0.9f, 0.4f, 0.95f);
        Vector3 newP2 = Handles.FreeMoveHandle(stbdNodes.p2, 0.18f, Vector3.zero, Handles.SphereHandleCap);
        newP2.z = midZ; // Strictly locked to Midship Apex Z

        Handles.color = Color.red;
        Vector3 xArrowP2 = Handles.Slider(stbdNodes.p2, Vector3.right, 0.8f, Handles.ArrowHandleCap, 0.05f);
        if (Mathf.Abs(xArrowP2.x - stbdNodes.p2.x) > 0.001f) newP2.x = xArrowP2.x;

        Handles.color = Color.green;
        Vector3 yArrowP2 = Handles.Slider(stbdNodes.p2, Vector3.up, 0.8f, Handles.ArrowHandleCap, 0.05f);
        if (Mathf.Abs(yArrowP2.y - stbdNodes.p2.y) > 0.001f) newP2.y = yArrowP2.y;

        // 3. Interactive Handle for p3 (Max Breadth HOB Midship Apex) -> Updates BreadthCurves maxBeam, fwdP3.y, & fwdP3.z
        Handles.color = Color.cyan;
        Vector3 newP3 = Handles.FreeMoveHandle(stbdNodes.p3, 0.18f, Vector3.zero, Handles.SphereHandleCap);

        Handles.color = Color.red;
        Vector3 xArrowP3 = Handles.Slider(stbdNodes.p3, Vector3.right, 0.8f, Handles.ArrowHandleCap, 0.05f);
        if (Mathf.Abs(xArrowP3.x - stbdNodes.p3.x) > 0.001f) newP3.x = xArrowP3.x;

        Handles.color = Color.green;
        Vector3 yArrowP3 = Handles.Slider(stbdNodes.p3, Vector3.up, 0.8f, Handles.ArrowHandleCap, 0.05f);
        if (Mathf.Abs(yArrowP3.y - stbdNodes.p3.y) > 0.001f) newP3.y = yArrowP3.y;

        Handles.color = Color.blue;
        Vector3 zArrowP3 = Handles.Slider(stbdNodes.p3, Vector3.forward, 0.8f, Handles.ArrowHandleCap, 0.05f);
        if (Mathf.Abs(zArrowP3.z - stbdNodes.p3.z) > 0.001f) newP3.z = zArrowP3.z;

        if (EditorGUI.EndChangeCheck())
        {
            // --- HANDLE 1: Floor Point (p1) ---
            if (curves.floorCurves != null && Vector3.Distance(newP1, stbdNodes.p1) > 0.001f)
            {
                Undo.RecordObjects(new Object[] { curves.floorCurves, curves }, "Drag BodyCurves Floor Handle");

                float hw = curves.keelWidth / 2f;
                float maxAllowedBeam = 20.0f;
                if (curves.breadthCurves != null)
                {
                    float bX = Mathf.Max(curves.breadthCurves.maxBeam, curves.breadthCurves.fwdP3.x);
                    if (bX > 0.1f) maxAllowedBeam = bX;
                }

                // Organic Floor minimum bound calculation (relative to Keel P0 and Bilge P2)
                Vector3 p0 = stbdNodes.p0;
                Vector3 p2 = stbdNodes.p2;
                Vector3 p1Candidate = new Vector3(Mathf.Abs(newP1.x), newP1.y, midZ);

                float d01 = Vector3.Distance(p0, p1Candidate);
                float d02 = Mathf.Max(0.001f, Vector3.Distance(p0, p2));
                float minOrganicFloorX = hw + ((d01 / (3f * d02)) * (p2.x - hw));

                float clampedFloorX = Mathf.Clamp(Mathf.Abs(newP1.x), minOrganicFloorX, maxAllowedBeam);

                curves.floorCurves.maxFloorBeam = clampedFloorX;
                curves.floorCurves.fwdP2.x = curves.floorCurves.maxFloorBeam;

                float keelY = (curves.sheerCurves != null) ? curves.sheerCurves.keelY : 0f;
                float breadthApexY = (curves.breadthCurves != null) ? curves.breadthCurves.fwdP3.y : keelY + 3.0f;
                float apexSpan = breadthApexY - keelY;
                if (apexSpan > 0.001f)
                {
                    float clampedFloorY = Mathf.Max(keelY + 0.05f, newP1.y);
                    curves.floorCurves.midshipHeightRatio = Mathf.Clamp01((clampedFloorY - keelY) / apexSpan);
                }

                curves.floorCurves.UpdateCachedCurves();
                EditorUtility.SetDirty(curves.floorCurves);
            }

            // --- HANDLE 2: Diagonal Bilge Point (p2) ---
            if (Vector3.Distance(newP2, stbdNodes.p2) > 0.001f)
            {
                float floorX = stbdNodes.p1.x;
                float floorY = stbdNodes.p1.y;
                float hobX = stbdNodes.p3.x;
                float hobY = stbdNodes.p3.y;

                float widthSpan = hobX - floorX;
                float heightSpan = hobY - floorY;

                Undo.RecordObject(curves, "Drag BodyCurves Diagonal Bilge Handle");

                if (heightSpan > 0.001f)
                {
                    curves.bilgeHeightRatio = Mathf.Clamp01((newP2.y - floorY) / heightSpan);
                }

                if (widthSpan > 0.001f)
                {
                    curves.bilgeWidthRatio = Mathf.Clamp01((Mathf.Abs(newP2.x) - floorX) / widthSpan);
                }

                if (curves.bilgeCurves != null)
                {
                    Undo.RecordObject(curves.bilgeCurves, "Drag BodyCurves Diagonal Bilge Handle");
                    curves.bilgeCurves.bilgeWidthRatio = curves.bilgeWidthRatio;
                    curves.bilgeCurves.bilgeHeightRatio = curves.bilgeHeightRatio;
                }

                curves.UpdateCachedCurves();
                EditorUtility.SetDirty(curves);
                if (curves.bilgeCurves != null)
                {
                    EditorUtility.SetDirty(curves.bilgeCurves);
                }
            }

            // --- HANDLE 3: Midship Apex / Max Breadth Point (p3) ---
            if (curves.breadthCurves != null && Vector3.Distance(newP3, stbdNodes.p3) > 0.001f)
            {
                Undo.RecordObjects(new Object[] { curves.breadthCurves, curves }, "Drag BodyCurves HOB Handle");
                float hw = curves.keelWidth / 2f;
                curves.breadthCurves.maxBeam = Mathf.Max(hw, Mathf.Abs(newP3.x));
                curves.breadthCurves.fwdP3.y = Mathf.Max(stbdNodes.p0.y + 0.1f, newP3.y);

                if (Mathf.Abs(newP3.z - stbdNodes.p3.z) > 0.001f)
                {
                    float bowZ = (curves.sheerCurves != null) ? curves.sheerCurves.stemP3.z : 15f;
                    float sternZ = (curves.sheerCurves != null) ? curves.sheerCurves.sternP3.z : -15f;
                    float dzTotal = bowZ - sternZ;
                    if (dzTotal > 0.001f)
                    {
                        curves.breadthCurves.midshipApexZPercentage = Mathf.Clamp((newP3.z - sternZ) / dzTotal, 0.05f, 0.95f);
                    }
                }

                curves.breadthCurves.UpdateCachedCurves();
                EditorUtility.SetDirty(curves.breadthCurves);

                if (curves.floorCurves != null)
                {
                    curves.floorCurves.UpdateCachedCurves();
                    EditorUtility.SetDirty(curves.floorCurves);
                }
            }

            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);
        }
    }
}
#endif
