using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// FloorCurves implements a 3D Spatial Quadratic Bezier formulation for the Floor Plan / Floor Lines.
/// Both forward and aft curves are quadratic and double-pointed (bow and stern terminate at X = 0 on centerline).
/// Bow point intersects the Stem Post and Stern point intersects the Stern Profile.
/// Neither point can rise higher than its respective Stem/Stern Post Top, nor drop lower than the Midship Apex height.
/// </summary>
[ExecuteInEditMode]
public class FloorCurves : MonoBehaviour
{
    [Tooltip("Reference to BodyCurves script for physical Keel Width if available.")]
    public BodyCurves bodyCurves;

    [Tooltip("Reference to BackboneCurves script for stem and stern keel width offset.")]
    public BackboneCurves backboneCurves;

    [Tooltip("Reference to the SheerCurves script for 2D profile curves (Stem Post, Stern Post, Counter, Transom Rake).")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to BreadthCurves to sync midship apex Y height if needed.")]
    public BreadthCurves breadthCurves;

    [Header("Interactive Scene Handles")]
    [Tooltip("Enable interactive 3D position handles in Scene view to sculpt control points along X, Y, and Z axes.")]
    public bool enableInteractiveHandles = true;

    [Header("Gizmo Settings")]
    public bool showLabels = true;
    public Color floorCurveColor = new Color(0.95f, 0.45f, 0.1f, 0.95f);

    [Header("Initialization State")]
    public bool isInitialized = false;

    // --- DIRECT 3D SPATIAL CONTROL POINTS ---
    [Header("Forward Quadratic 3D Control Points (P0..P2)")]
    [Tooltip("Bow Stem Intersect Point (P0). Double pointed at X = 0 on Stem Post profile.")]
    public Vector3 fwdP0;

    [Tooltip("Bow Entry Tangent Handle (P1). Controls bow entrance shape.")]
    public Vector3 fwdP1;

    [Tooltip("Midship Apex Point (P2/P0). Z driven by midshipApexZPercentage. X defines Max Floor Beam.")]
    public Vector3 fwdP2;

    [Header("Aft Quadratic 3D Control Points (P0..P2)")]
    [Tooltip("Stern Quarter Tangent Handle (P1). Controls stern sweep shape.")]
    public Vector3 aftP1;

    [Tooltip("Stern Profile Intersect Point (P2). Double pointed at X = 0 on Stern profile.")]
    public Vector3 aftP2;

    [Header("Floor Beam & Midship Height Settings")]
    [Tooltip("Maximum half-width of the floor at midship (synced to fwdP2.x).")]
    public float maxFloorBeam = 3f;

    [Tooltip("Height ratio of the Floor Midship Apex point between the Keel Line (0.0) and the Breadth Curves Midship Apex height (1.0).")]
    [Range(0f, 1f)] public float midshipHeightRatio = 0.5f;

    [Header("Proportional Control Arm Ratios")]
    [Tooltip("Normalized relative Z position of Bow Entry Arm (P1) along [Midship Apex Z, Bow Tip Z].")]
    [Range(0.01f, 0.99f)] public float fwdP1Ratio = 0.50f;

    [Tooltip("Normalized relative Z position of Stern Arm (P1) along [Stern Z, Midship Apex Z].")]
    [Range(0.01f, 0.99f)] public float aftP1Ratio = 0.50f;

    // --- CACHED BEZIER CONTROL POINTS ---
    [HideInInspector] public Vector3 cachedFwdP0;
    [HideInInspector] public Vector3 cachedFwdP1;
    [HideInInspector] public Vector3 cachedFwdP2;

    [HideInInspector] public Vector3 cachedAftP0;
    [HideInInspector] public Vector3 cachedAftP1;
    [HideInInspector] public Vector3 cachedAftP2;

    [HideInInspector] public int changeVersion = 0;

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Reset()
    {
        InitializeControlPointsFromShip();
    }

    public void EnsureInitialized()
    {
        if (!isInitialized)
        {
            InitializeControlPointsFromShip();
        }
    }

    [ContextMenu("Reset Control Points to Default Hull")]
    public void InitializeControlPointsFromShip()
    {
        if (sheerCurves == null) sheerCurves = GetComponentInParent<SheerCurves>();
        if (sheerCurves == null) sheerCurves = FindFirstObjectByType<SheerCurves>();
        if (bodyCurves == null) bodyCurves = GetComponentInParent<BodyCurves>();
        if (bodyCurves == null) bodyCurves = FindFirstObjectByType<BodyCurves>();
        if (backboneCurves == null) backboneCurves = GetComponentInParent<BackboneCurves>();
        if (backboneCurves == null) backboneCurves = FindFirstObjectByType<BackboneCurves>();
        if (breadthCurves == null) breadthCurves = GetComponentInParent<BreadthCurves>();
        if (breadthCurves == null) breadthCurves = FindFirstObjectByType<BreadthCurves>();

        float bowZ = (sheerCurves != null) ? sheerCurves.stemP3.z : 15f;
        float sternZ = (sheerCurves != null) ? sheerCurves.sternP3.z : -15f;
        float baseY = (breadthCurves != null) ? breadthCurves.fwdP3.y : 1.5f;
        float currentMaxFloor = maxFloorBeam > 0.5f ? maxFloorBeam : 3f;

        // Double Pointed (X = 0 on centerline)
        fwdP0 = new Vector3(0f, baseY + 1f, bowZ);
        fwdP2 = new Vector3(currentMaxFloor, baseY, 0f); // Midship Apex (Z=0)
        fwdP1 = new Vector3(currentMaxFloor * 0.6f, baseY, bowZ * 0.5f);

        aftP2 = new Vector3(0f, baseY + 1f, sternZ);
        aftP1 = new Vector3(currentMaxFloor * 0.6f, baseY, sternZ * 0.5f);

        maxFloorBeam = currentMaxFloor;
        fwdP1Ratio = 0.50f;
        aftP1Ratio = 0.50f;
        isInitialized = true;
    }

    private void OnValidate()
    {
        UpdateCachedCurves();
    }

    public virtual void UpdateCachedCurves()
    {
        EnsureInitialized();

        if (sheerCurves == null) sheerCurves = GetComponentInParent<SheerCurves>();
        if (sheerCurves == null) sheerCurves = FindFirstObjectByType<SheerCurves>();
        if (bodyCurves == null) bodyCurves = GetComponentInParent<BodyCurves>();
        if (bodyCurves == null) bodyCurves = FindFirstObjectByType<BodyCurves>();
        if (backboneCurves == null) backboneCurves = GetComponentInParent<BackboneCurves>();
        if (backboneCurves == null) backboneCurves = FindFirstObjectByType<BackboneCurves>();
        if (breadthCurves == null) breadthCurves = GetComponentInParent<BreadthCurves>();
        if (breadthCurves == null) breadthCurves = FindFirstObjectByType<BreadthCurves>();

        // Slave Midship Apex Z position to Breadth Curves's Midship Apex Z position (fwdP3.z)
        if (breadthCurves != null)
        {
            breadthCurves.UpdateCachedCurves();
            fwdP2.z = breadthCurves.fwdP3.z;
        }

        // Calculate Midship Apex Y height between Keel Line Y and Breadth Curves Midship Apex Y
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;
        float breadthApexY = (breadthCurves != null) ? breadthCurves.fwdP3.y : keelY + 3.0f;
        fwdP2.y = Mathf.Lerp(keelY, breadthApexY, Mathf.Clamp01(midshipHeightRatio));

        // Midship Apex Y height (minY bound for Bow and Stern points)
        float minY = fwdP2.y;

        // Stem Post Top Y & Stern Post Top Y upper bounds
        float maxStemY = (sheerCurves != null) ? sheerCurves.stemP3.y : minY + 10f;
        float maxSternY = (sheerCurves != null) ? sheerCurves.sternP3.y : minY + 10f;

        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);

        // 1. Bow Point (fwdP0): X = hw, minY <= fwdP0.y <= maxStemY, Z intersects Stem Post
        fwdP0.x = hw;
        fwdP0.y = Mathf.Clamp(fwdP0.y, minY, maxStemY);
        if (sheerCurves != null)
        {
            fwdP0.z = sheerCurves.GetStemProfileZAtY(fwdP0.y);
        }

        // 2. Stern Point (aftP2): X = hw, minY <= aftP2.y <= maxSternY, Z intersects Stern Profile
        aftP2.x = hw;
        aftP2.y = Mathf.Clamp(aftP2.y, minY, maxSternY);
        if (sheerCurves != null)
        {
            aftP2.z = sheerCurves.GetSternProfileZAtY(aftP2.y);
        }

        float bowZ = fwdP0.z;
        float sternZ = aftP2.z;
        float targetApexZ = fwdP2.z;

        float maxAllowedBeam = 20.0f;
        if (breadthCurves != null)
        {
            float bX = Mathf.Max(breadthCurves.maxBeam, breadthCurves.fwdP3.x);
            if (bX > 0.1f) maxAllowedBeam = bX;
        }
        maxFloorBeam = Mathf.Clamp(maxFloorBeam, hw, maxAllowedBeam);

        fwdP2.x = maxFloorBeam;

        // Lock Midship Apex Control Arms straight on the Z axis (X = maxFloorBeam) for smooth C1 tangent continuity
        fwdP1.x = maxFloorBeam;
        aftP1.x = maxFloorBeam;
        fwdP1.y = fwdP2.y;
        aftP1.y = fwdP2.y;

        // Proportionally scale Bow Control Arm (fwdP1) Z
        float fwdSpan = bowZ - targetApexZ;
        if (fwdSpan > 0.001f)
        {
            fwdP1.z = targetApexZ + fwdP1Ratio * fwdSpan;
        }
        else
        {
            fwdP1.z = targetApexZ;
        }

        // Proportionally scale Stern Control Arm (aftP1) Z
        float aftSpan = targetApexZ - sternZ;
        if (aftSpan > 0.001f)
        {
            aftP1.z = sternZ + aftP1Ratio * aftSpan;
        }
        else
        {
            aftP1.z = sternZ;
        }

        // Cache Forward Quadratic Bezier Points (P0, P1, P2)
        cachedFwdP0 = fwdP0;
        cachedFwdP1 = fwdP1;
        cachedFwdP2 = fwdP2;

        // Cache Aft Quadratic Bezier Points (P0, P1, P2) where Aft P0 is Midship Apex (fwdP2)
        cachedAftP0 = fwdP2;
        cachedAftP1 = aftP1;
        cachedAftP2 = aftP2;

        changeVersion++;
    }

    // --- 3D SPATIAL EVALUATION METHODS ---

    public virtual float GetFloorXAtZ(float z)
    {
        return GetFloorPointAtZ(z).x;
    }

    public virtual float GetFloorYAtZ(float z)
    {
        return GetFloorPointAtZ(z).y;
    }

    public virtual Vector3 GetFloorPointAtZ(float z)
    {
        if (z >= cachedFwdP2.z)
        {
            if (z >= cachedFwdP0.z) return cachedFwdP0;
            float t = GetBezierTAtZ(cachedFwdP0, cachedFwdP1, cachedFwdP2, z, true);
            return CalculateQuadraticBezierPoint(t, cachedFwdP0, cachedFwdP1, cachedFwdP2);
        }
        else
        {
            if (z <= cachedAftP2.z) return cachedAftP2;
            float t = GetBezierTAtZ(cachedAftP0, cachedAftP1, cachedAftP2, z, false);
            return CalculateQuadraticBezierPoint(t, cachedAftP0, cachedAftP1, cachedAftP2);
        }
    }

    // --- BEZIER MATH UTILITIES ---

    public Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        return (uu * p0) + (2f * u * t * p1) + (tt * p2);
    }

    public float GetBezierTAtZ(Vector3 p0, Vector3 p1, Vector3 p2, float targetZ, bool isForward)
    {
        float min = 0f;
        float max = 1f;
        float t = 0.5f;

        for (int i = 0; i < 8; i++)
        {
            float u = 1f - t;
            float zVal = (u * u * p0.z) + (2f * u * t * p1.z) + (t * t * p2.z);
            float error = zVal - targetZ;

            if (Mathf.Abs(error) < 0.00005f) return t;

            if ((isForward && zVal < targetZ) || (!isForward && zVal > targetZ))
            {
                min = t;
            }
            else
            {
                max = t;
            }
            t = (min + max) * 0.5f;
        }

        return t;
    }

    private Vector3 MirrorX(Vector3 point)
    {
        return new Vector3(-point.x, point.y, point.z);
    }

    // --- GIZMO RENDERING ---

    private static GUIStyle cachedLabelStyle;
#if UNITY_EDITOR
    private static GUIStyle GetCachedLabelStyle()
    {
        if (cachedLabelStyle == null)
        {
            cachedLabelStyle = new GUIStyle();
            cachedLabelStyle.normal.textColor = Color.cyan;
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

        // Draw Starboard 3D Floor Curve (Vibrant Orange/Coral)
        DrawQuadraticBezierCurve(cachedFwdP0, cachedFwdP1, cachedFwdP2, floorCurveColor);
        DrawQuadraticBezierCurve(cachedAftP0, cachedAftP1, cachedAftP2, floorCurveColor);

        // Draw Port 3D Floor Curve (Mirrored X)
        Color portColor = new Color(floorCurveColor.r, floorCurveColor.g * 0.6f, floorCurveColor.b, floorCurveColor.a);
        DrawQuadraticBezierCurve(MirrorX(cachedFwdP0), MirrorX(cachedFwdP1), MirrorX(cachedFwdP2), portColor);
        DrawQuadraticBezierCurve(MirrorX(cachedAftP0), MirrorX(cachedAftP1), MirrorX(cachedAftP2), portColor);

        // Draw 3D Control Polygon Skeleton Arms
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        Gizmos.DrawLine(cachedFwdP0, cachedFwdP1);
        Gizmos.DrawLine(cachedFwdP1, cachedFwdP2);
        Gizmos.DrawLine(cachedAftP0, cachedAftP1);
        Gizmos.DrawLine(cachedAftP1, cachedAftP2);

        // Draw Control Points
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(cachedFwdP0, 0.12f);
        Gizmos.DrawSphere(cachedFwdP1, 0.09f);
        Gizmos.DrawSphere(cachedFwdP2, 0.15f); // Midship Apex
        Gizmos.DrawSphere(cachedAftP1, 0.09f);
        Gizmos.DrawSphere(cachedAftP2, 0.12f);

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = GetCachedLabelStyle();
            Handles.Label(cachedFwdP0 + Vector3.up * 0.2f, "Floor Bow Tip (Stem)", labelStyle);
            Handles.Label(cachedFwdP2 + Vector3.up * 0.2f, $"Floor Midship Apex ({maxFloorBeam:F2}m)", labelStyle);
            Handles.Label(cachedAftP2 + Vector3.up * 0.2f, "Floor Stern Tip", labelStyle);
        }
#endif
    }

    private void DrawQuadraticBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Color color)
    {
        Gizmos.color = color;
        Vector3 prev = p0;
        int segments = 30;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 curr = CalculateQuadraticBezierPoint(t, p0, p1, p2);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(FloorCurves))]
public class FloorCurvesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        FloorCurves curves = (FloorCurves)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update Floor Curves Profile", GUILayout.Height(30)))
        {
            Undo.RecordObject(curves, "Update Floor Profile");
            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);
        }
    }

    private void OnSceneGUI()
    {
        FloorCurves curves = (FloorCurves)target;
        if (!curves.enableInteractiveHandles) return;

        curves.UpdateCachedCurves();

        EditorGUI.BeginChangeCheck();

        float hw = (curves.backboneCurves != null) ? (curves.backboneCurves.keelWidth / 2f) : ((curves.bodyCurves != null) ? (curves.bodyCurves.keelWidth / 2f) : 0.25f);

        // 1. Bow Point Handle (Yellow) - Double-pointed on Stem Profile
        Handles.color = Color.yellow;
        Vector3 newFwdP0 = Handles.FreeMoveHandle(curves.fwdP0, 0.16f, Vector3.zero, Handles.SphereHandleCap);
        newFwdP0.x = hw;

        // 2. Bow Control Arm Handle (Orange)
        Handles.color = new Color(1f, 0.6f, 0.1f);
        Vector3 newFwdP1 = Handles.FreeMoveHandle(curves.fwdP1, 0.12f, Vector3.zero, Handles.SphereHandleCap);

        // 3. Midship Apex Handle (Green Sphere + Red X Arrow & Green Y Arrow)
        Handles.color = Color.green;
        Vector3 newFwdP2 = Handles.FreeMoveHandle(curves.fwdP2, 0.18f, Vector3.zero, Handles.SphereHandleCap);

        Handles.color = Color.red;
        Vector3 xArrowFwdP2 = Handles.Slider(curves.fwdP2, Vector3.right, 0.8f, Handles.ArrowHandleCap, 0.05f);
        if (Mathf.Abs(xArrowFwdP2.x - curves.fwdP2.x) > 0.001f) newFwdP2.x = xArrowFwdP2.x;

        Handles.color = Color.green;
        Vector3 yArrowFwdP2 = Handles.Slider(curves.fwdP2, Vector3.up, 0.8f, Handles.ArrowHandleCap, 0.05f);
        if (Mathf.Abs(yArrowFwdP2.y - curves.fwdP2.y) > 0.001f) newFwdP2.y = yArrowFwdP2.y;

        // 4. Stern Control Arm Handle (Orange)
        Handles.color = new Color(1f, 0.6f, 0.1f);
        Vector3 newAftP1 = Handles.FreeMoveHandle(curves.aftP1, 0.12f, Vector3.zero, Handles.SphereHandleCap);

        // 5. Stern Point Handle (Yellow) - Double-pointed on Stern Profile
        Handles.color = Color.yellow;
        Vector3 newAftP2 = Handles.FreeMoveHandle(curves.aftP2, 0.16f, Vector3.zero, Handles.SphereHandleCap);
        newAftP2.x = hw;

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(curves, "Manipulate Floor Curves Handles");

            float keelY = (curves.sheerCurves != null) ? curves.sheerCurves.keelY : 0f;
            float breadthApexY = (curves.breadthCurves != null) ? curves.breadthCurves.fwdP3.y : keelY + 3.0f;
            float minY = newFwdP2.y;
            float maxStemY = (curves.sheerCurves != null) ? curves.sheerCurves.stemP3.y : minY + 10f;
            float maxSternY = (curves.sheerCurves != null) ? curves.sheerCurves.sternP3.y : minY + 10f;

            // Update Bow Point Y (clamped between Midship Apex Y and Stem Post Top Y)
            curves.fwdP0.y = Mathf.Clamp(newFwdP0.y, minY, maxStemY);

            // Update Stern Point Y (clamped between Midship Apex Y and Stern Post Top Y)
            curves.aftP2.y = Mathf.Clamp(newAftP2.y, minY, maxSternY);

            // Update Midship Apex (Max Floor Beam & Midship Height Ratio)
            float maxAllowedBeam = 20.0f;
            if (curves.breadthCurves != null)
            {
                float bX = Mathf.Max(curves.breadthCurves.maxBeam, curves.breadthCurves.fwdP3.x);
                if (bX > 0.1f) maxAllowedBeam = bX;
            }
            curves.maxFloorBeam = Mathf.Clamp(Mathf.Abs(newFwdP2.x), hw, maxAllowedBeam);
            float apexSpan = breadthApexY - keelY;
            if (apexSpan > 0.001f)
            {
                curves.midshipHeightRatio = Mathf.Clamp01((newFwdP2.y - keelY) / apexSpan);
            }

            // Update Control Arm Z Ratios
            float bowZ = curves.fwdP0.z;
            float sternZ = curves.aftP2.z;

            float fwdSpan = bowZ - curves.fwdP2.z;
            if (fwdSpan > 0.001f)
            {
                curves.fwdP1Ratio = Mathf.Clamp01((newFwdP1.z - curves.fwdP2.z) / fwdSpan);
            }

            float aftSpan = curves.fwdP2.z - sternZ;
            if (aftSpan > 0.001f)
            {
                curves.aftP1Ratio = Mathf.Clamp01((newAftP1.z - sternZ) / aftSpan);
            }

            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);

            BodyCurves bodyCurves = FindFirstObjectByType<BodyCurves>();
            if (bodyCurves != null)
            {
                bodyCurves.UpdateCachedCurves();
                EditorUtility.SetDirty(bodyCurves);
            }

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
