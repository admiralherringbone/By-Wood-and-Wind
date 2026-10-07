using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// KeelBreadth defines the 3D spatial lines representing the physical width of the keel timber
/// running along both sides of the Keel Line in SheerCurves.
/// These lines pass directly through the p0 (Keel Baseline) nodes of BodyCurves on Starboard (+X) and Port (-X).
/// </summary>
[ExecuteInEditMode]
public class KeelBreadth : MonoBehaviour
{
    [Header("Primary Curve References")]
    [Tooltip("Reference to SheerCurves for baseline keel height (keelY) and length bounds.")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to BodyCurves for physical keel width harmonization.")]
    public BodyCurves bodyCurves;

    [Header("Keel Dimension Settings")]
    [Tooltip("Physical full width of the keel timber. Synced automatically with BodyCurves.")]
    public float keelWidth = 0.5f;

    [Header("Gizmo Visualization Settings")]
    public bool showLabels = true;
    public Color keelColor = new Color(1.0f, 0.85f, 0.1f, 0.95f); // Vibrant Yellow / Gold

    [Header("Initialization State")]
    public bool isInitialized = false;

    // --- CACHED SPATIAL LINE ENDPOINTS ---
    [HideInInspector] public Vector3 stbdKeelStart; // Starboard Bow (+X, keelY, stemP0.z)
    [HideInInspector] public Vector3 stbdKeelEnd;   // Starboard Stern (+X, keelY, sternP0.z)
    [HideInInspector] public Vector3 portKeelStart; // Port Bow (-X, keelY, stemP0.z)
    [HideInInspector] public Vector3 portKeelEnd;   // Port Stern (-X, keelY, sternP0.z)

    [HideInInspector] public int changeVersion = 0;

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Reset()
    {
        InitializeKeelBreadth();
    }

    public void EnsureInitialized()
    {
        if (!isInitialized)
        {
            InitializeKeelBreadth();
        }
    }

    public void FindReferences()
    {
        if (sheerCurves == null) sheerCurves = GetComponentInParent<SheerCurves>();
        if (sheerCurves == null) sheerCurves = FindFirstObjectByType<SheerCurves>();

        if (bodyCurves == null) bodyCurves = GetComponentInParent<BodyCurves>();
        if (bodyCurves == null) bodyCurves = FindFirstObjectByType<BodyCurves>();
    }

    [ContextMenu("Reset Keel Breadth to Default")]
    public void InitializeKeelBreadth()
    {
        FindReferences();
        if (bodyCurves != null)
        {
            keelWidth = bodyCurves.keelWidth;
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

        float hw = keelWidth / 2f;
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;
        float bowZ = (sheerCurves != null) ? sheerCurves.stemP0.z : 10f;
        float sternZ = (sheerCurves != null) ? sheerCurves.sternP0.z : -10f;

        // Sync keelWidth with BodyCurves if assigned
        if (bodyCurves != null && Mathf.Abs(bodyCurves.keelWidth - keelWidth) > 0.0001f)
        {
            bodyCurves.keelWidth = keelWidth;
            bodyCurves.UpdateCachedCurves();
        }

        // Starboard Keel Line (+X)
        stbdKeelStart = new Vector3(hw, keelY, bowZ);
        stbdKeelEnd = new Vector3(hw, keelY, sternZ);

        // Port Keel Line (-X)
        portKeelStart = new Vector3(-hw, keelY, bowZ);
        portKeelEnd = new Vector3(-hw, keelY, sternZ);

        changeVersion++;
    }

    // --- SPATIAL EVALUATION METHODS ---

    /// <summary>
    /// Returns the 3D point on the Keel Breadth line at station Z for specified side (+1 Starboard, -1 Port).
    /// </summary>
    public Vector3 GetKeelPointAtZ(float z, float side)
    {
        UpdateCachedCurves();

        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float hw = keelWidth / 2f;
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        return new Vector3(sideSign * hw, keelY, z);
    }

    /// <summary>
    /// Returns the X half-width coordinate of the keel at station Z.
    /// </summary>
    public float GetKeelHalfWidthAtZ(float z)
    {
        return keelWidth / 2f;
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

        // Draw Starboard Keel Line
        Gizmos.color = keelColor;
        Gizmos.DrawLine(stbdKeelStart, stbdKeelEnd);

        // Draw Port Keel Line
        Gizmos.color = keelColor;
        Gizmos.DrawLine(portKeelStart, portKeelEnd);

        // Draw Transverse End Crossbars at Bow and Stern
        Gizmos.color = new Color(keelColor.r, keelColor.g, keelColor.b, 0.6f);
        Gizmos.DrawLine(stbdKeelStart, portKeelStart);
        Gizmos.DrawLine(stbdKeelEnd, portKeelEnd);

        // Draw Corner Handle Spheres
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(stbdKeelStart, 0.12f);
        Gizmos.DrawSphere(stbdKeelEnd, 0.12f);
        Gizmos.DrawSphere(portKeelStart, 0.12f);
        Gizmos.DrawSphere(portKeelEnd, 0.12f);

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = GetCachedLabelStyle();
            Vector3 midStbd = (stbdKeelStart + stbdKeelEnd) * 0.5f;
            Handles.Label(midStbd + Vector3.up * 0.15f, $"Keel Width ({keelWidth:F2}m)", labelStyle);
        }
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(KeelBreadth))]
public class KeelBreadthEditor : Editor
{
    public override void OnInspectorGUI()
    {
        KeelBreadth plan = (KeelBreadth)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Keel Coordinates Readout", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Keel Half-Width:", $"{plan.keelWidth / 2f:F3}m");
        EditorGUILayout.LabelField("Starboard Bow (P0):", $"X: {plan.stbdKeelStart.x:F2}, Y: {plan.stbdKeelStart.y:F2}, Z: {plan.stbdKeelStart.z:F2}");
        EditorGUILayout.LabelField("Starboard Stern (P0):", $"X: {plan.stbdKeelEnd.x:F2}, Y: {plan.stbdKeelEnd.y:F2}, Z: {plan.stbdKeelEnd.z:F2}");

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update Keel Breadth Lines", GUILayout.Height(30)))
        {
            Undo.RecordObject(plan, "Update Keel Breadth");
            plan.UpdateCachedCurves();
            EditorUtility.SetDirty(plan);
        }
    }

    private void OnSceneGUI()
    {
        KeelBreadth plan = (KeelBreadth)target;
        if (plan == null) return;

        plan.UpdateCachedCurves();

        Vector3 midStbd = (plan.stbdKeelStart + plan.stbdKeelEnd) * 0.5f;

        EditorGUI.BeginChangeCheck();

        // Interactive Red Arrow Handle on Midship Starboard Keel Line to adjust Keel Width in Scene View
        Handles.color = Color.red;
        Vector3 newMidStbd = Handles.Slider(midStbd, Vector3.right, 0.8f, Handles.ArrowHandleCap, 0.05f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(plan, "Drag Keel Width Handle");
            float newHw = Mathf.Max(0.05f, newMidStbd.x);
            plan.keelWidth = newHw * 2f;
            plan.UpdateCachedCurves();
            EditorUtility.SetDirty(plan);

            if (plan.bodyCurves != null)
            {
                Undo.RecordObject(plan.bodyCurves, "Sync Keel Width to BodyCurves");
                plan.bodyCurves.keelWidth = plan.keelWidth;
                plan.bodyCurves.UpdateCachedCurves();
                EditorUtility.SetDirty(plan.bodyCurves);
            }
        }
    }
}
#endif
