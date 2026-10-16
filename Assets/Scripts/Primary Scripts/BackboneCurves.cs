using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// BackboneCurves generates two 3D spatial profile boundary curves running parallel to SheerCurves 
/// offset on Starboard (+X) and Port (-X) at Keel Width (keelWidth / 2).
/// Strictly covers:
/// 1. Whole Stem Post (Cubic Bezier Stem Post & Upper Stem Post Extension)
/// 2. Keel Line (Baseline from Stem P0 to Stern P0)
/// 3. Stern Post (Line from Stern P0 to Stern P3)
/// </summary>
[ExecuteInEditMode]
public class BackboneCurves : MonoBehaviour
{
    [Header("Primary Reference Dependencies")]
    [Tooltip("Reference to SheerCurves for profile geometry (Stem Post, Keel Line, Stern Post).")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to BodyCurves or KeelBreadth for physical keel width harmonization.")]
    public BodyCurves bodyCurves;

    [Tooltip("Reference to KeelBreadth for keel width synchronization.")]
    public KeelBreadth keelBreadth;

    [Header("Backbone Dimensions")]
    [Tooltip("Physical width of the backbone assembly. Synced automatically with KeelBreadth / BodyCurves.")]
    public float keelWidth = 0.5f;

    [Header("Gizmo Visualization Settings")]
    public bool showLabels = true;
    public Color backboneColor = new Color(0.95f, 0.6f, 0.1f, 0.95f); // Amber / Warm Gold

    [Header("Initialization State")]
    public bool isInitialized = false;

    // --- CACHED STARBOARD 3D BACKBONE POINTS (+X) ---
    [HideInInspector] public Vector3 stbdStemP0;
    [HideInInspector] public Vector3 stbdStemP1;
    [HideInInspector] public Vector3 stbdStemP2;
    [HideInInspector] public Vector3 stbdStemP3;
    [HideInInspector] public Vector3 stbdUpperStemTop;

    [HideInInspector] public Vector3 stbdKeelStart;
    [HideInInspector] public Vector3 stbdKeelEnd;

    [HideInInspector] public Vector3 stbdSternP0;
    [HideInInspector] public Vector3 stbdSternP3;

    // --- CACHED PORT 3D BACKBONE POINTS (-X) ---
    [HideInInspector] public Vector3 portStemP0;
    [HideInInspector] public Vector3 portStemP1;
    [HideInInspector] public Vector3 portStemP2;
    [HideInInspector] public Vector3 portStemP3;
    [HideInInspector] public Vector3 portUpperStemTop;

    [HideInInspector] public Vector3 portKeelStart;
    [HideInInspector] public Vector3 portKeelEnd;

    [HideInInspector] public Vector3 portSternP0;
    [HideInInspector] public Vector3 portSternP3;

    [HideInInspector] public int changeVersion = 0;

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Reset()
    {
        InitializeBackbone();
    }

    public void EnsureInitialized()
    {
        if (!isInitialized)
        {
            InitializeBackbone();
        }
    }

    public void FindReferences()
    {
        if (sheerCurves == null) sheerCurves = GetComponentInParent<SheerCurves>();
        if (sheerCurves == null) sheerCurves = FindFirstObjectByType<SheerCurves>();

        if (bodyCurves == null) bodyCurves = GetComponentInParent<BodyCurves>();
        if (bodyCurves == null) bodyCurves = FindFirstObjectByType<BodyCurves>();

        if (keelBreadth == null) keelBreadth = GetComponentInParent<KeelBreadth>();
        if (keelBreadth == null) keelBreadth = FindFirstObjectByType<KeelBreadth>();
    }

    [ContextMenu("Reset Backbone Curves to Default")]
    public void InitializeBackbone()
    {
        FindReferences();
        if (keelBreadth != null)
        {
            keelWidth = keelBreadth.keelWidth;
        }
        else if (bodyCurves != null)
        {
            keelWidth = bodyCurves.keelWidth;
        }
        UpdateCachedCurves();
        isInitialized = true;
    }

    private static bool isSyncingKeelWidth = false;

    public void SyncKeelWidth(float newWidth)
    {
        if (isSyncingKeelWidth) return;
        isSyncingKeelWidth = true;
        try
        {
            float clampedWidth = Mathf.Max(0.01f, newWidth);
            keelWidth = clampedWidth;

            FindReferences();
            if (bodyCurves != null && Mathf.Abs(bodyCurves.keelWidth - clampedWidth) > 0.0001f)
            {
                bodyCurves.keelWidth = clampedWidth;
                bodyCurves.UpdateCachedCurves();
            }
            if (keelBreadth != null && Mathf.Abs(keelBreadth.keelWidth - clampedWidth) > 0.0001f)
            {
                keelBreadth.keelWidth = clampedWidth;
                keelBreadth.UpdateCachedCurves();
            }

            UpdateCachedCurves();
        }
        finally
        {
            isSyncingKeelWidth = false;
        }
    }

    private void OnValidate()
    {
        SyncKeelWidth(keelWidth);
    }

    public virtual void UpdateCachedCurves()
    {
        FindReferences();

        if (sheerCurves != null) sheerCurves.UpdateCachedProfile();

        float hw = keelWidth / 2f;

        Vector3 sP0 = (sheerCurves != null) ? sheerCurves.stemP0 : new Vector3(0, 0, 10);
        Vector3 sP1 = (sheerCurves != null) ? sheerCurves.stemP1 : new Vector3(0, 2, 11);
        Vector3 sP2 = (sheerCurves != null) ? sheerCurves.stemP2 : new Vector3(0, 5, 12);
        Vector3 sP3 = (sheerCurves != null) ? sheerCurves.stemP3 : new Vector3(0, 7, 13);
        Vector3 uTop = (sheerCurves != null) ? sheerCurves.upperStemTop : sP3 + Vector3.up;

        Vector3 stP0 = (sheerCurves != null) ? sheerCurves.sternP0 : new Vector3(0, 0, -10);
        Vector3 stP3 = (sheerCurves != null) ? sheerCurves.sternP3 : new Vector3(0, 7, -11);

        // --- STARBOARD (+X) BACKBONE SEGMENTS ---
        stbdStemP0 = OffsetX(sP0, hw);
        stbdStemP1 = OffsetX(sP1, hw);
        stbdStemP2 = OffsetX(sP2, hw);
        stbdStemP3 = OffsetX(sP3, hw);
        stbdUpperStemTop = OffsetX(uTop, hw);

        stbdKeelStart = stbdStemP0;
        stbdKeelEnd = OffsetX(stP0, hw);

        stbdSternP0 = stbdKeelEnd;
        stbdSternP3 = OffsetX(stP3, hw);

        // --- PORT (-X) BACKBONE SEGMENTS ---
        portStemP0 = OffsetX(sP0, -hw);
        portStemP1 = OffsetX(sP1, -hw);
        portStemP2 = OffsetX(sP2, -hw);
        portStemP3 = OffsetX(sP3, -hw);
        portUpperStemTop = OffsetX(uTop, -hw);

        portKeelStart = portStemP0;
        portKeelEnd = OffsetX(stP0, -hw);

        portSternP0 = portKeelEnd;
        portSternP3 = OffsetX(stP3, -hw);

        changeVersion++;
    }

    private Vector3 OffsetX(Vector3 pt, float xOffset)
    {
        return new Vector3(xOffset, pt.y, pt.z);
    }

    // --- SPATIAL EVALUATION METHODS ---

    /// <summary>
    /// Evaluates 3D point on the Stem Post Backbone curve at target Y height for specified side (+1 Starboard, -1 Port).
    /// </summary>
    public Vector3 GetStemBackbonePointAtY(float targetY, float side)
    {
        UpdateCachedCurves();
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float hw = keelWidth / 2f;
        float z = (sheerCurves != null) ? sheerCurves.GetStemProfileZAtY(targetY) : 10f;
        return new Vector3(sideSign * hw, targetY, z);
    }

    /// <summary>
    /// Evaluates 3D point on the Stern Post Backbone line at target Y height for specified side (+1 Starboard, -1 Port).
    /// </summary>
    public Vector3 GetSternBackbonePointAtY(float targetY, float side)
    {
        UpdateCachedCurves();
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float hw = keelWidth / 2f;
        float z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(targetY) : -10f;
        return new Vector3(sideSign * hw, targetY, z);
    }

    /// <summary>
    /// Evaluates 3D point on the Keel Line Backbone at station Z for specified side (+1 Starboard, -1 Port).
    /// </summary>
    public Vector3 GetKeelBackbonePointAtZ(float z, float side)
    {
        UpdateCachedCurves();
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float hw = keelWidth / 2f;
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;
        return new Vector3(sideSign * hw, keelY, z);
    }

    // --- GIZMO RENDERING ---

    private static GUIStyle cachedLabelStyle;
#if UNITY_EDITOR
    private static GUIStyle GetCachedLabelStyle()
    {
        if (cachedLabelStyle == null)
        {
            cachedLabelStyle = new GUIStyle();
            cachedLabelStyle.normal.textColor = Color.yellow;
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

        float physBowY = (sheerCurves != null) ? sheerCurves.GetPhysicalBowCapY() : stbdStemP3.y;
        bool isCutBelowUpperStem = (stbdUpperStemTop.y > physBowY + 0.05f);

        // 1. Draw Starboard (+X) Backbone Profile Lines
        DrawCubicBezier(stbdStemP0, stbdStemP1, stbdStemP2, stbdStemP3, backboneColor);
        if (stbdUpperStemTop.y > stbdStemP3.y + 0.001f)
        {
            if (!isCutBelowUpperStem)
            {
                Gizmos.color = backboneColor;
                Gizmos.DrawLine(stbdStemP3, stbdUpperStemTop);
            }
            else
            {
                Vector3 basePt = stbdStemP3;
                if (physBowY > basePt.y + 0.001f)
                {
                    Gizmos.color = backboneColor;
                    Vector3 physPt = new Vector3(basePt.x, physBowY, basePt.z);
                    Gizmos.DrawLine(basePt, physPt);
                    basePt = physPt;
                }
                Color dashStbd = new Color(backboneColor.r, backboneColor.g, backboneColor.b, 0.40f);
                DrawDashedLine(basePt, stbdUpperStemTop, dashStbd);
            }
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(stbdKeelStart, stbdKeelEnd);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(stbdSternP0, stbdSternP3);

        // 2. Draw Port (-X) Backbone Profile Lines
        Color portColor = new Color(backboneColor.r, backboneColor.g * 0.7f, backboneColor.b, backboneColor.a);
        DrawCubicBezier(portStemP0, portStemP1, portStemP2, portStemP3, portColor);
        if (portUpperStemTop.y > portStemP3.y + 0.001f)
        {
            if (!isCutBelowUpperStem)
            {
                Gizmos.color = portColor;
                Gizmos.DrawLine(portStemP3, portUpperStemTop);
            }
            else
            {
                Vector3 basePt = portStemP3;
                if (physBowY > basePt.y + 0.001f)
                {
                    Gizmos.color = portColor;
                    Vector3 physPt = new Vector3(basePt.x, physBowY, basePt.z);
                    Gizmos.DrawLine(basePt, physPt);
                    basePt = physPt;
                }
                Color dashPort = new Color(portColor.r, portColor.g, portColor.b, 0.40f);
                DrawDashedLine(basePt, portUpperStemTop, dashPort);
            }
        }
        Gizmos.color = new Color(0.8f, 0.8f, 0.1f);
        Gizmos.DrawLine(portKeelStart, portKeelEnd);
        Gizmos.color = new Color(0.1f, 0.8f, 0.8f);
        Gizmos.DrawLine(portSternP0, portSternP3);

        // 3. Draw Transverse Crossbars Connecting Starboard and Port
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.6f);
        Gizmos.DrawLine(stbdStemP0, portStemP0);
        Gizmos.DrawLine(stbdStemP3, portStemP3);

        if (stbdUpperStemTop.y > stbdStemP3.y + 0.001f)
        {
            if (!isCutBelowUpperStem)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.6f);
                Gizmos.DrawLine(stbdUpperStemTop, portUpperStemTop);
            }
            else
            {
                // Physical crossbar capping the stem timbers at physicalBowY
                if (physBowY > stbdStemP3.y + 0.001f)
                {
                    Vector3 stbdPhys = new Vector3(stbdStemP3.x, physBowY, stbdStemP3.z);
                    Vector3 portPhys = new Vector3(portStemP3.x, physBowY, portStemP3.z);
                    Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.6f);
                    Gizmos.DrawLine(stbdPhys, portPhys);
                }

                // Semi-transparent crossbar at upperStemTop datum
                Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.25f);
                Gizmos.DrawLine(stbdUpperStemTop, portUpperStemTop);
            }
        }
        Gizmos.DrawLine(stbdSternP0, portSternP0);
        Gizmos.DrawLine(stbdSternP3, portSternP3);

        // 4. Draw Backbone Nodes Spheres
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(stbdStemP0, 0.11f); Gizmos.DrawSphere(portStemP0, 0.11f);
        Gizmos.DrawSphere(stbdStemP3, 0.13f); Gizmos.DrawSphere(portStemP3, 0.13f);
        if (stbdUpperStemTop.y > stbdStemP3.y + 0.001f)
        {
            Color sphereCol = isCutBelowUpperStem ? new Color(1f, 0.9f, 0.1f, 0.40f) : Color.yellow;
            Gizmos.color = sphereCol;
            Gizmos.DrawSphere(stbdUpperStemTop, 0.12f); Gizmos.DrawSphere(portUpperStemTop, 0.12f);
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(stbdSternP0, 0.11f); Gizmos.DrawSphere(portSternP0, 0.11f);
        Gizmos.DrawSphere(stbdSternP3, 0.13f); Gizmos.DrawSphere(portSternP3, 0.13f);

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = GetCachedLabelStyle();
            Handles.Label(stbdStemP3 + Vector3.up * 0.2f, "Backbone Stem Top", labelStyle);
            Handles.Label(stbdSternP3 + Vector3.up * 0.2f, "Backbone Stern Post Top", labelStyle);
        }
#endif
    }

    private void DrawDashedLine(Vector3 p0, Vector3 p1, Color color, float dashLength = 0.20f)
    {
        float totalDist = Vector3.Distance(p0, p1);
        if (totalDist < 0.001f) return;

        int segments = Mathf.Max(2, Mathf.RoundToInt(totalDist / dashLength));
        Gizmos.color = color;
        Vector3 prev = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 curr = Vector3.Lerp(p0, p1, t);
            Vector3 half = Vector3.Lerp(prev, curr, 0.5f);
            Gizmos.DrawLine(prev, half);
            prev = curr;
        }
    }

    private void DrawCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color, int segments = 30)
    {
        Gizmos.color = color;
        Vector3 prev = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 curr = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }
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

#if UNITY_EDITOR
[CustomEditor(typeof(BackboneCurves))]
public class BackboneCurvesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BackboneCurves plan = (BackboneCurves)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Backbone Profile Readout", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Backbone Half-Width:", $"{plan.keelWidth / 2f:F3}m");
        EditorGUILayout.LabelField("Starboard Stem Top:", $"X: {plan.stbdStemP3.x:F2}, Y: {plan.stbdStemP3.y:F2}, Z: {plan.stbdStemP3.z:F2}");
        EditorGUILayout.LabelField("Starboard Stern Post Top:", $"X: {plan.stbdSternP3.x:F2}, Y: {plan.stbdSternP3.y:F2}, Z: {plan.stbdSternP3.z:F2}");

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update Backbone Profile Curves", GUILayout.Height(30)))
        {
            Undo.RecordObject(plan, "Update Backbone Profile");
            plan.UpdateCachedCurves();
            EditorUtility.SetDirty(plan);
        }
    }

    private void OnSceneGUI()
    {
        BackboneCurves plan = (BackboneCurves)target;
        if (plan == null) return;

        plan.UpdateCachedCurves();

        Vector3 midStbd = (plan.stbdKeelStart + plan.stbdKeelEnd) * 0.5f;

        EditorGUI.BeginChangeCheck();

        // Interactive Red Arrow Handle on Midship Starboard Backbone Line to adjust Keel Width in Scene View
        Handles.color = Color.red;
        Vector3 newMidStbd = Handles.Slider(midStbd, Vector3.right, 0.8f, Handles.ArrowHandleCap, 0.05f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(plan, "Drag Backbone Width Handle");
            if (plan.keelBreadth != null) Undo.RecordObject(plan.keelBreadth, "Drag Backbone Width Handle");
            if (plan.bodyCurves != null) Undo.RecordObject(plan.bodyCurves, "Drag Backbone Width Handle");

            float newHw = Mathf.Max(0.05f, newMidStbd.x);
            plan.SyncKeelWidth(newHw * 2f);

            EditorUtility.SetDirty(plan);
            if (plan.keelBreadth != null) EditorUtility.SetDirty(plan.keelBreadth);
            if (plan.bodyCurves != null) EditorUtility.SetDirty(plan.bodyCurves);
        }
    }
}
#endif
