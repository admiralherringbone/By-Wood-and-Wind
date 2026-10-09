using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// FashionCurves defines the 3D Transom / Fashion Timber boundary curve at the stern.
/// Formed using the exact same NURBS C1 B-Spline section evaluator (NurbsSectionEvaluator) as BodyCurves.
/// Spans from Floor Stern Tip (P1, u=0.25) through Transom Bilge Node (P2, u=0.50),
/// up to Fashion Breadth (P3, u=0.75, from BreadthCurves), and extending through the Upper Mould
/// to Transom Rake Top Corner (P4, u=1.00, from TopLineCurves).
/// Every point evaluated along the NURBS curve locks 100% to the raked Z stern profile of SheerCurves.
/// </summary>
[ExecuteInEditMode]
public class FashionCurves : MonoBehaviour
{
    [Header("Primary Curve References")]
    [Tooltip("Reference to SheerCurves for raked Z stern profile lookup.")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to TopLineCurves for Transom Rake Top Corner (P4) top point.")]
    public TopLineCurves topLineCurves;

    [Tooltip("Reference to BreadthCurves for Fashion Breadth (P3) mid-section point.")]
    public BreadthCurves breadthCurves;

    [Tooltip("Reference to FloorCurves for Floor Stern Tip (P1) bottom point.")]
    public FloorCurves floorCurves;

    [Tooltip("Reference to BodyCurves for proportional midship section ratios.")]
    public BodyCurves bodyCurves;

    [Tooltip("Reference to BackboneCurves for keel width half-offset.")]
    public BackboneCurves backboneCurves;

    [Header("Fashion Curve Proportional Settings")]
    [Tooltip("Relative height ratio of the Transom Bilge Point (P2) between Floor Stern Tip (0.0) and Fashion Breadth (1.0). Synced to BodyCurves.")]
    [Range(0.05f, 0.95f)] public float bilgeHeightRatio = 0.50f;

    [Tooltip("Relative width ratio of the Transom Bilge Point (P2) between Floor Stern Tip (0.0) and Fashion Breadth (1.0). Synced to BodyCurves.")]
    [Range(0.05f, 0.95f)] public float bilgeWidthRatio = 0.50f;

    [Header("Gizmo Visualization Settings")]
    public bool showLabels = true;
    public Color fashionCurveColor = new Color(0.95f, 0.25f, 0.85f, 0.95f); // Vibrant Rose / Magenta

    [Header("Initialization State")]
    public bool isInitialized = false;

    private readonly NurbsSectionEvaluator nurbsEvaluator = new NurbsSectionEvaluator();

    // --- CACHED STARBOARD 3D CONTROL NODES (P1..P4) ---
    [HideInInspector] public Vector3 stbdP1; // Floor Stern Tip (Bottom, u=0.25)
    [HideInInspector] public Vector3 stbdP2; // Transom Bilge Node (Middle, u=0.50)
    [HideInInspector] public Vector3 stbdP3; // Fashion Breadth (u=0.75)
    [HideInInspector] public Vector3 stbdP4; // Transom Rake Top Corner / Fashion TopLine (Top, u=1.00)

    // --- CACHED PORT 3D CONTROL NODES (P1..P4) ---
    [HideInInspector] public Vector3 portP1;
    [HideInInspector] public Vector3 portP2;
    [HideInInspector] public Vector3 portP3;
    [HideInInspector] public Vector3 portP4;

    [HideInInspector] public int changeVersion = 0;

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Reset()
    {
        InitializeFashionCurves();
    }

    public void EnsureInitialized()
    {
        if (!isInitialized)
        {
            InitializeFashionCurves();
        }
    }

    public void FindReferences()
    {
        if (sheerCurves == null) sheerCurves = GetComponentInParent<SheerCurves>();
        if (sheerCurves == null) sheerCurves = FindFirstObjectByType<SheerCurves>();

        if (topLineCurves == null) topLineCurves = GetComponentInParent<TopLineCurves>();
        if (topLineCurves == null) topLineCurves = FindFirstObjectByType<TopLineCurves>();

        if (breadthCurves == null) breadthCurves = GetComponentInParent<BreadthCurves>();
        if (breadthCurves == null) breadthCurves = FindFirstObjectByType<BreadthCurves>();

        if (floorCurves == null) floorCurves = GetComponentInParent<FloorCurves>();
        if (floorCurves == null) floorCurves = FindFirstObjectByType<FloorCurves>();

        if (bodyCurves == null) bodyCurves = GetComponentInParent<BodyCurves>();
        if (bodyCurves == null) bodyCurves = FindFirstObjectByType<BodyCurves>();

        if (backboneCurves == null) backboneCurves = GetComponentInParent<BackboneCurves>();
        if (backboneCurves == null) backboneCurves = FindFirstObjectByType<BackboneCurves>();
    }

    [ContextMenu("Reset Fashion Curves Profile")]
    public void InitializeFashionCurves()
    {
        FindReferences();
        if (bodyCurves != null)
        {
            bilgeHeightRatio = bodyCurves.bilgeHeightRatio;
            bilgeWidthRatio = bodyCurves.bilgeWidthRatio;
        }
        UpdateCachedCurves();
        isInitialized = true;
    }

    private void OnValidate()
    {
        UpdateCachedCurves();
    }

    public virtual void UpdateCachedCurves()
    {
        FindReferences();

        if (sheerCurves != null) sheerCurves.UpdateCachedProfile();
        if (topLineCurves != null) topLineCurves.UpdateCachedCurves();
        if (breadthCurves != null) breadthCurves.UpdateCachedCurves();
        if (floorCurves != null) floorCurves.UpdateCachedCurves();

        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);

        // Sync ratios from BodyCurves if assigned
        if (bodyCurves != null)
        {
            bilgeHeightRatio = bodyCurves.bilgeHeightRatio;
            bilgeWidthRatio = bodyCurves.bilgeWidthRatio;
        }

        // 1. Bottom Node P1: Floor Stern Tip (u = 0.25)
        float p1X = (floorCurves != null) ? floorCurves.aftP3.x : hw;
        float p1Y = (floorCurves != null) ? floorCurves.aftP3.y : ((sheerCurves != null) ? sheerCurves.keelY + 0.5f : 0.5f);
        float p1Z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p1Y) : -10f;

        stbdP1 = new Vector3(Mathf.Abs(p1X), p1Y, p1Z);

        // 2. Middle Node P3: Fashion Breadth from BreadthCurves (u = 0.75)
        float p3X = (breadthCurves != null) ? (breadthCurves.cachedIsAftCubic ? breadthCurves.aftP3.x : breadthCurves.aftP2.x) : (hw + 2f);
        float p3Y = (breadthCurves != null) ? (breadthCurves.cachedIsAftCubic ? breadthCurves.aftP3.y : breadthCurves.aftP2.y) : (p1Y + 2f);
        float p3Z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p3Y) : -10f;

        stbdP3 = new Vector3(Mathf.Abs(p3X), p3Y, p3Z);

        // 3. Lower Mould Node P2: Transom Bilge Node (u = 0.50, derived from BodyCurves master bilge node)
        float tY = Mathf.Clamp01(bilgeHeightRatio);
        float tX = Mathf.Clamp01(bilgeWidthRatio);

        if (bodyCurves != null)
        {
            float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
            SectionNodes bodyNodes = bodyCurves.GetSectionNodesAtZ(midZ, 1f);
            float bSpanX = bodyNodes.p3.x - bodyNodes.p1.x;
            float bSpanY = bodyNodes.p3.y - bodyNodes.p1.y;

            if (Mathf.Abs(bSpanX) > 0.001f) tX = Mathf.Clamp01((Mathf.Abs(bodyNodes.p2.x) - bodyNodes.p1.x) / bSpanX);
            if (Mathf.Abs(bSpanY) > 0.001f) tY = Mathf.Clamp01((bodyNodes.p2.y - bodyNodes.p1.y) / bSpanY);
        }

        float p2Y = Mathf.Lerp(stbdP1.y, stbdP3.y, tY);
        float p2X = Mathf.Lerp(stbdP1.x, stbdP3.x, tX);
        float p2Z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p2Y) : Mathf.Lerp(stbdP1.z, stbdP3.z, tY);

        stbdP2 = new Vector3(p2X, p2Y, p2Z);

        // 4. Upper Mould Top Node P4: Fashion TopLine Point (u = 1.00)
        // MUST ALWAYS BE 100% COMPLETELY ALIGNED WITH TRANSOM RAKE TOP CORNER (topLineCurves.aftP2 / sheerCurves.transomP1)
        float p4X = (topLineCurves != null) ? topLineCurves.aftP2.x : ((breadthCurves != null) ? breadthCurves.GetAftProjectionPointAtZ(stbdP3.z).x : p3X);
        float p4Y = (topLineCurves != null) ? topLineCurves.aftP2.y : ((sheerCurves != null) ? sheerCurves.transomP1.y : p3Y + 1.5f);
        float p4Z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p4Y) : ((topLineCurves != null) ? topLineCurves.aftP2.z : -10f);

        stbdP4 = new Vector3(Mathf.Abs(p4X), p4Y, p4Z);

        // Mirror Port Nodes (-X)
        portP1 = MirrorX(stbdP1);
        portP2 = MirrorX(stbdP2);
        portP3 = MirrorX(stbdP3);
        portP4 = MirrorX(stbdP4);

        changeVersion++;
    }

    /// <summary>
    /// Constructs a SectionNodes struct representing the 3D Transom section curve.
    /// Nodes p1, p2, p3, p4 correspond to Floor Stern Tip (u=0.25), Transom Bilge (u=0.50),
    /// Fashion Breadth (u=0.75), and Transom Rake Top Corner (u=1.00).
    /// </summary>
    public SectionNodes GetTransomSectionNodes(float side)
    {
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : 0.25f;
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        SectionNodes nodes = new SectionNodes();
        nodes.p0 = new Vector3(sideSign * hw, keelY, stbdP1.z); // Keel baseline anchor
        nodes.p1 = new Vector3(sideSign * stbdP1.x, stbdP1.y, stbdP1.z); // Floor Stern Tip (u=0.25)
        nodes.p2 = new Vector3(sideSign * stbdP2.x, stbdP2.y, stbdP2.z); // Transom Bilge Node (u=0.50)
        nodes.p3 = new Vector3(sideSign * stbdP3.x, stbdP3.y, stbdP3.z); // Fashion Breadth (u=0.75)
        nodes.p4 = new Vector3(sideSign * stbdP4.x, stbdP4.y, stbdP4.z); // Transom Rake Top Corner (u=1.00)

        return nodes;
    }

    // --- SPATIAL EVALUATION METHODS ---

    /// <summary>
    /// Evaluates 3D point on Fashion Curve using synthesis across Lower and Upper Moulds:
    /// - Lower Mould (t in [0, 2/3], u in [0.25, 0.75]): Evaluates Floor Stern Tip P1 -> Transom Bilge P2 -> Fashion Breadth P3.
    /// - Upper Mould (t in [2/3, 1.0], u_upper in [0, 1]): Synthesizes X from Breadth Curves Aft Projection Line X_proj(Z)
    ///   scaled by Body Curves Upper Mould Curvature Ratio ratioBody(u_upper), strictly clamped so X <= X_proj(Z) at all points.
    /// - Z is locked 100% to SheerCurves raked stern profile.
    /// - At t = 1.0 (u_upper = 1.0), Fashion TopLine (P4) is ALWAYS 100% COMPLETELY ALIGNED WITH TRANSOM RAKE TOP CORNER (topLineCurves.aftP2).
    /// </summary>
    public Vector3 GetFashionPointAtT(float t, float side)
    {
        UpdateCachedCurves();

        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float clampedT = Mathf.Clamp01(t);

        if (clampedT <= (2f / 3f))
        {
            // --- LOWER MOULD EVALUATION (t in [0, 2/3] -> u in [0.25, 0.75]) ---
            float tLower = clampedT / (2f / 3f);
            float u = Mathf.Lerp(0.25f, 0.75f, tLower);

            if (bodyCurves != null)
            {
                float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
                SectionNodes bodyNodes = bodyCurves.GetSectionNodesAtZ(midZ, 1f);
                float hollowVal = bodyCurves.floorHollow;
                Vector3 bodyPt = nurbsEvaluator.EvaluateSectionPoint(u, bodyNodes, hollowVal, 1.0f);

                float bFloorX = bodyNodes.p1.x;
                float bFloorY = bodyNodes.p1.y;
                float bBreadthX = bodyNodes.p3.x;
                float bBreadthY = bodyNodes.p3.y;

                float dSpanX = bBreadthX - bFloorX;
                float dSpanY = bBreadthY - bFloorY;

                float tX = (Mathf.Abs(dSpanX) > 0.0001f) ? (Mathf.Abs(bodyPt.x) - bFloorX) / dSpanX : tLower;
                float tY = (Mathf.Abs(dSpanY) > 0.0001f) ? (bodyPt.y - bFloorY) / dSpanY : tLower;

                float xLower = Mathf.Lerp(stbdP1.x, stbdP3.x, tX);
                float yLower = Mathf.Lerp(stbdP1.y, stbdP3.y, tY);
                float zLower = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(yLower) : stbdP1.z;

                return new Vector3(sideSign * xLower, yLower, zLower);
            }
            else
            {
                SectionNodes nodes = GetTransomSectionNodes(sideSign);
                Vector3 nurbsPt = nurbsEvaluator.EvaluateSectionPoint(u, nodes, 0.5f, 1.0f);

                float xLower = Mathf.Abs(nurbsPt.x);
                float yLower = nurbsPt.y;
                float zLower = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(yLower) : nurbsPt.z;

                return new Vector3(sideSign * xLower, yLower, zLower);
            }
        }
        else
        {
            // --- UPPER MOULD EVALUATION (t in [2/3, 1.0] -> u_upper in [0, 1.0]) ---
            float uUpper = Mathf.Clamp01((clampedT - (2f / 3f)) / (1f / 3f));

            float y = Mathf.Lerp(stbdP3.y, stbdP4.y, uUpper);
            float z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(y) : Mathf.Lerp(stbdP3.z, stbdP4.z, uUpper);

            // 1. Base half-breadth along Breadth Curves Aft Curves Projection Line at station Z (Outer Boundary Ceiling)
            float xProj = (breadthCurves != null) ? breadthCurves.GetAftProjectionPointAtZ(z).x : Mathf.Lerp(stbdP3.x, stbdP4.x, uUpper);
            float xChord = Mathf.Lerp(stbdP3.x, stbdP4.x, uUpper);

            // 2. Tumblehome / Curvature Ratio from Body Curves Upper Mould
            float ratioBody = 1.0f;
            if (bodyCurves != null)
            {
                float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
                SectionNodes bodyNodes = bodyCurves.GetSectionNodesAtZ(midZ, 1f);
                float uNurbs = 0.75f + 0.25f * uUpper;
                Vector3 bodyPt = nurbsEvaluator.EvaluateSectionPoint(uNurbs, bodyNodes, bodyCurves.floorHollow, 1.0f);
                float bChord = Mathf.Lerp(bodyNodes.p3.x, bodyNodes.p4.x, uUpper);
                if (bChord > 0.001f)
                {
                    ratioBody = Mathf.Abs(bodyPt.x) / bChord;
                }
            }

            // 3. Synthesize X: Blend towards xChord at endpoints, shaped by xProj, scaled by body ratio, clamped <= xProj
            float xShape = Mathf.Min(xProj, xChord);
            float xUpper = Mathf.Min(xShape * ratioBody, xProj);

            // Force 100% exact alignment at P4 (uUpper = 1.0) to stbdP4 (Transom Rake Top Corner)
            if (uUpper >= 0.999f)
            {
                xUpper = stbdP4.x;
            }

            return new Vector3(sideSign * xUpper, y, z);
        }
    }

    /// <summary>
    /// Evaluates 3D point on Fashion Curve at target height Y for specified side (+1 Starboard, -1 Port).
    /// </summary>
    public Vector3 GetFashionPointAtY(float targetY, float side)
    {
        UpdateCachedCurves();

        float ySpan = stbdP4.y - stbdP1.y;
        if (ySpan < 0.0001f)
        {
            return GetFashionPointAtT(0f, side);
        }

        // Iterative parameter solve for target Y height across the NURBS curve
        float t = Mathf.Clamp01((targetY - stbdP1.y) / ySpan);
        for (int i = 0; i < 6; i++)
        {
            Vector3 pt = GetFashionPointAtT(t, side);
            float error = pt.y - targetY;
            if (Mathf.Abs(error) < 0.0001f) break;

            float delta = 0.001f;
            Vector3 ptPlus = GetFashionPointAtT(Mathf.Clamp01(t + delta), side);
            float dydt = (ptPlus.y - pt.y) / delta;
            if (Mathf.Abs(dydt) > 0.0001f)
            {
                t = Mathf.Clamp01(t - error / dydt);
            }
        }

        return GetFashionPointAtT(t, side);
    }

    /// <summary>
    /// Evaluates 3D point on Fashion Curve at target station Z for specified side (+1 Starboard, -1 Port).
    /// </summary>
    public Vector3 GetFashionPointAtZ(float targetZ, float side)
    {
        UpdateCachedCurves();

        float zSpan = stbdP4.z - stbdP1.z;
        if (Mathf.Abs(zSpan) < 0.0001f)
        {
            return GetFashionPointAtT(0f, side);
        }

        if (targetZ >= stbdP1.z) return GetFashionPointAtT(0f, side);
        if (targetZ <= stbdP4.z) return GetFashionPointAtT(1f, side);

        float minT = 0f;
        float maxT = 1f;
        float t = Mathf.Clamp01((targetZ - stbdP1.z) / zSpan);

        for (int i = 0; i < 12; i++)
        {
            Vector3 pt = GetFashionPointAtT(t, side);
            float error = pt.z - targetZ;
            if (Mathf.Abs(error) < 0.0001f) return pt;

            if (stbdP1.z > stbdP4.z)
            {
                if (pt.z > targetZ) minT = t;
                else maxT = t;
            }
            else
            {
                if (pt.z < targetZ) minT = t;
                else maxT = t;
            }
            t = (minT + maxT) * 0.5f;
        }

        return GetFashionPointAtT(t, side);
    }

    private Vector3 MirrorX(Vector3 pt)
    {
        return new Vector3(-pt.x, pt.y, pt.z);
    }

    // --- GIZMO RENDERING ---

    private static GUIStyle cachedLabelStyle;
#if UNITY_EDITOR
    private static GUIStyle GetCachedLabelStyle()
    {
        if (cachedLabelStyle == null)
        {
            cachedLabelStyle = new GUIStyle();
            cachedLabelStyle.normal.textColor = Color.magenta;
            cachedLabelStyle.fontSize = 10;
            cachedLabelStyle.alignment = TextAnchor.LowerCenter;
            cachedLabelStyle.fontStyle = FontStyle.Bold;
        }
        return cachedLabelStyle;
    }
#endif

    private void OnDrawGizmos()
    {
        if (sheerCurves == null) return;

        UpdateCachedCurves();

        // 1. Draw Starboard 3D Fashion NURBS Curve (Vibrant Rose / Magenta)
        DrawFashionNurbsCurve(1f, fashionCurveColor);

        // 2. Draw Port 3D Fashion NURBS Curve (Mirrored X)
        Color portColor = new Color(fashionCurveColor.r, fashionCurveColor.g * 0.6f, fashionCurveColor.b, fashionCurveColor.a);
        DrawFashionNurbsCurve(-1f, portColor);

        // 3. Draw Transverse Crossbars connecting Starboard and Port at P1..P4
        Gizmos.color = new Color(fashionCurveColor.r, fashionCurveColor.g, fashionCurveColor.b, 0.6f);
        Gizmos.DrawLine(stbdP1, portP1);
        Gizmos.DrawLine(stbdP2, portP2);
        Gizmos.DrawLine(stbdP3, portP3);
        Gizmos.DrawLine(stbdP4, portP4);

        // 4. Draw Structural Node Spheres (P1..P4 lie EXACTLY on the curve)
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(stbdP1, 0.12f); // Floor Stern Tip (P1, u=0.25)
        Gizmos.color = new Color(0.1f, 0.9f, 0.4f);
        Gizmos.DrawSphere(stbdP2, 0.13f); // Transom Bilge Node (P2, u=0.50)
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(stbdP3, 0.14f); // Fashion Breadth (P3, u=0.75)
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(stbdP4, 0.14f); // Transom Rake Top Corner (P4, u=1.00)

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(portP1, 0.12f);
        Gizmos.color = new Color(0.1f, 0.9f, 0.4f);
        Gizmos.DrawSphere(portP2, 0.13f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(portP3, 0.14f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(portP4, 0.14f);

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = GetCachedLabelStyle();
            Handles.Label(stbdP4 + Vector3.up * 0.18f, "Fashion TopLine / Transom Rake Top (u=1.00)", labelStyle);
            Handles.Label(stbdP3 + Vector3.up * 0.18f, "Fashion Breadth (u=0.75)", labelStyle);
            Handles.Label(stbdP2 + Vector3.up * 0.15f, "Fashion Bilge Node (u=0.50)", labelStyle);
            Handles.Label(stbdP1 + Vector3.up * 0.15f, "Fashion NURBS Base (u=0.25)", labelStyle);
        }
#endif
    }

    private void DrawFashionNurbsCurve(float side, Color color, int steps = 50)
    {
        Gizmos.color = color;
        Vector3 prev = GetFashionPointAtT(0f, side);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 curr = GetFashionPointAtT(t, side);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(FashionCurves))]
public class FashionCurvesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        FashionCurves curves = (FashionCurves)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Transom Fashion NURBS Nodes Readout", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("P1 Floor Stern Base (u=0.25):", $"X: {curves.stbdP1.x:F2}, Y: {curves.stbdP1.y:F2}, Z: {curves.stbdP1.z:F2}");
        EditorGUILayout.LabelField("P2 Transom Bilge Node (u=0.50):", $"X: {curves.stbdP2.x:F2}, Y: {curves.stbdP2.y:F2}, Z: {curves.stbdP2.z:F2}");
        EditorGUILayout.LabelField("P3 Fashion Breadth (u=0.75):", $"X: {curves.stbdP3.x:F2}, Y: {curves.stbdP3.y:F2}, Z: {curves.stbdP3.z:F2}");
        EditorGUILayout.LabelField("P4 Transom Rake Top Corner (u=1.00):", $"X: {curves.stbdP4.x:F2}, Y: {curves.stbdP4.y:F2}, Z: {curves.stbdP4.z:F2}");

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update Fashion Curves Profile", GUILayout.Height(30)))
        {
            Undo.RecordObject(curves, "Update Fashion Profile");
            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);

            BilgeCurves bilgeCurves = FindFirstObjectByType<BilgeCurves>();
            if (bilgeCurves != null)
            {
                bilgeCurves.UpdateCachedCurves();
                EditorUtility.SetDirty(bilgeCurves);
            }

            RibbandCurves ribbandCurves = FindFirstObjectByType<RibbandCurves>();
            if (ribbandCurves != null)
            {
                ribbandCurves.UpdateCachedCurves();
                EditorUtility.SetDirty(ribbandCurves);
            }
        }
    }
}
#endif

