using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// FashionCurves defines the 3D Transom / Fashion Timber boundary curve at the stern.
/// Formed using the exact same NURBS C1 B-Spline section evaluator (NurbsSectionEvaluator) as BodyCurves.
/// Spans from the Floor Stern Tip (P1, u=0.25) through Transom Bilge Node (P2, u=0.50) up to Transom Corner (P3, u=0.75).
/// Every point evaluated along the NURBS curve locks 100% to the raked Z stern profile of SheerCurves.
/// </summary>
[ExecuteInEditMode]
public class FashionCurves : MonoBehaviour
{
    [Header("Primary Curve References")]
    [Tooltip("Reference to SheerCurves for raked Z stern profile lookup.")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to BreadthCurves for Transom Corner (P3) top point.")]
    public BreadthCurves breadthCurves;

    [Tooltip("Reference to FloorCurves for Floor Stern Tip (P1) bottom point.")]
    public FloorCurves floorCurves;

    [Tooltip("Reference to BodyCurves for proportional midship section ratios.")]
    public BodyCurves bodyCurves;

    [Tooltip("Reference to BackboneCurves for keel width half-offset.")]
    public BackboneCurves backboneCurves;

    [Header("Fashion Curve Proportional Settings")]
    [Tooltip("Relative height ratio of the Transom Bilge Point (P2) between Floor Stern Tip (0.0) and Transom Corner (1.0). Synced to BodyCurves.")]
    [Range(0.05f, 0.95f)] public float bilgeHeightRatio = 0.50f;

    [Tooltip("Relative width ratio of the Transom Bilge Point (P2) between Floor Stern Tip (0.0) and Transom Corner (1.0). Synced to BodyCurves.")]
    [Range(0.05f, 0.95f)] public float bilgeWidthRatio = 0.50f;

    [Header("Gizmo Visualization Settings")]
    public bool showLabels = true;
    public Color fashionCurveColor = new Color(0.95f, 0.25f, 0.85f, 0.95f); // Vibrant Rose / Magenta

    [Header("Initialization State")]
    public bool isInitialized = false;

    private readonly NurbsSectionEvaluator nurbsEvaluator = new NurbsSectionEvaluator();

    // --- CACHED STARBOARD 3D CONTROL NODES (P1..P3) ---
    [HideInInspector] public Vector3 stbdP1; // Floor Stern Tip (Bottom, u=0.25)
    [HideInInspector] public Vector3 stbdP2; // Transom Bilge Node (Middle, u=0.50)
    [HideInInspector] public Vector3 stbdP3; // Transom Corner (Top, u=0.75)

    // --- CACHED PORT 3D CONTROL NODES (P1..P3) ---
    [HideInInspector] public Vector3 portP1;
    [HideInInspector] public Vector3 portP2;
    [HideInInspector] public Vector3 portP3;

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
        float p1X = (floorCurves != null) ? floorCurves.aftP2.x : hw;
        float p1Y = (floorCurves != null) ? floorCurves.aftP2.y : ((sheerCurves != null) ? sheerCurves.keelY + 0.5f : 0.5f);
        float p1Z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p1Y) : -10f;

        stbdP1 = new Vector3(Mathf.Abs(p1X), p1Y, p1Z);

        // 2. Top Node P3: Transom Corner from BreadthCurves (u = 0.75)
        float p3X = (breadthCurves != null) ? (breadthCurves.cachedIsAftCubic ? breadthCurves.aftP3.x : breadthCurves.aftP2.x) : (hw + 2f);
        float p3Y = (breadthCurves != null) ? (breadthCurves.cachedIsAftCubic ? breadthCurves.aftP3.y : breadthCurves.aftP2.y) : (p1Y + 2f);
        float p3Z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p3Y) : -10f;

        stbdP3 = new Vector3(Mathf.Abs(p3X), p3Y, p3Z);

        // 3. Middle Node P2: Transom Bilge Node (u = 0.50, proportionally positioned between P1 and P3)
        float p2Y = Mathf.Lerp(stbdP1.y, stbdP3.y, Mathf.Clamp01(bilgeHeightRatio));
        float p2X = Mathf.Lerp(stbdP1.x, stbdP3.x, Mathf.Clamp01(bilgeWidthRatio));
        float p2Z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p2Y) : Mathf.Lerp(stbdP1.z, stbdP3.z, Mathf.Clamp01(bilgeHeightRatio));

        stbdP2 = new Vector3(p2X, p2Y, p2Z);

        // Mirror Port Nodes (-X)
        portP1 = MirrorX(stbdP1);
        portP2 = MirrorX(stbdP2);
        portP3 = MirrorX(stbdP3);

        changeVersion++;
    }

    /// <summary>
    /// Constructs a SectionNodes struct representing the 3D Transom section curve.
    /// Nodes p1, p2, p3 correspond to Floor Stern Tip (u=0.25), Transom Bilge (u=0.50), and Transom Corner (u=0.75).
    /// </summary>
    public SectionNodes GetTransomSectionNodes(float side)
    {
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : 0.25f;
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        SectionNodes nodes = new SectionNodes();
        nodes.p0 = new Vector3(sideSign * hw, keelY, stbdP1.z); // Keel baseline anchor
        nodes.p1 = new Vector3(sideSign * stbdP1.x, stbdP1.y, stbdP1.z); // Floor Stern Tip
        nodes.p2 = new Vector3(sideSign * stbdP2.x, stbdP2.y, stbdP2.z); // Transom Bilge Node
        nodes.p3 = new Vector3(sideSign * stbdP3.x, stbdP3.y, stbdP3.z); // Transom Corner
        nodes.p4 = new Vector3(sideSign * stbdP3.x, stbdP3.y + 1.5f, stbdP3.z); // Sheer timber extension

        return nodes;
    }

    // --- SPATIAL EVALUATION METHODS ---

    /// <summary>
    /// Evaluates 3D point on Fashion Curve using NurbsSectionEvaluator for parameter t in [0, 1]
    /// (t=0 maps to Floor Stern Tip P1 at u=0.25, t=0.5 maps to Transom Bilge P2 at u=0.50, t=1 maps to Transom Corner P3 at u=0.75).
    /// Points P1, P2, and P3 lie EXACTLY on the evaluated NURBS B-Spline curve.
    /// Z coordinate conforms 100% to SheerCurves raked stern profile.
    /// </summary>
    public Vector3 GetFashionPointAtT(float t, float side)
    {
        UpdateCachedCurves();

        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float clampedT = Mathf.Clamp01(t);

        // Map parameter t in [0, 1] to section parameter u in [0.25, 0.75]
        float u = Mathf.Lerp(0.25f, 0.75f, clampedT);

        SectionNodes nodes = GetTransomSectionNodes(sideSign);
        Vector3 nurbsPt = nurbsEvaluator.EvaluateSectionPoint(u, nodes, 0.5f, 1.0f);

        float x = Mathf.Abs(nurbsPt.x);
        float y = nurbsPt.y;

        // Lock Z coordinate directly onto raked stern profile at height Y
        float z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(y) : nurbsPt.z;

        return new Vector3(sideSign * x, y, z);
    }

    /// <summary>
    /// Evaluates 3D point on Fashion Curve at target height Y for specified side (+1 Starboard, -1 Port).
    /// </summary>
    public Vector3 GetFashionPointAtY(float targetY, float side)
    {
        UpdateCachedCurves();

        float ySpan = stbdP3.y - stbdP1.y;
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

        // 3. Draw Transverse Crossbars connecting Starboard and Port at P1 (Floor Stern), P2 (Transom Bilge), P3 (Transom Corner)
        Gizmos.color = new Color(fashionCurveColor.r, fashionCurveColor.g, fashionCurveColor.b, 0.6f);
        Gizmos.DrawLine(stbdP1, portP1);
        Gizmos.DrawLine(stbdP2, portP2);
        Gizmos.DrawLine(stbdP3, portP3);

        // 4. Draw Structural Node Spheres (P1, P2, P3 lie EXACTLY on the curve)
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(stbdP1, 0.12f); // Floor Stern Tip (P1, u=0.25)
        Gizmos.color = new Color(0.1f, 0.9f, 0.4f);
        Gizmos.DrawSphere(stbdP2, 0.13f); // Transom Bilge Node (P2, u=0.50)
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(stbdP3, 0.14f); // Transom Corner (P3, u=0.75)

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(portP1, 0.12f);
        Gizmos.color = new Color(0.1f, 0.9f, 0.4f);
        Gizmos.DrawSphere(portP2, 0.13f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(portP3, 0.14f);

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = GetCachedLabelStyle();
            Handles.Label(stbdP3 + Vector3.up * 0.18f, "Fashion NURBS Top (u=0.75)", labelStyle);
            Handles.Label(stbdP2 + Vector3.up * 0.15f, "Fashion Bilge Node (u=0.50)", labelStyle);
            Handles.Label(stbdP1 + Vector3.up * 0.15f, "Fashion NURBS Base (u=0.25)", labelStyle);
        }
#endif
    }

    private void DrawFashionNurbsCurve(float side, Color color, int steps = 40)
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
        EditorGUILayout.LabelField("P3 Transom Corner Top (u=0.75):", $"X: {curves.stbdP3.x:F2}, Y: {curves.stbdP3.y:F2}, Z: {curves.stbdP3.z:F2}");

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
