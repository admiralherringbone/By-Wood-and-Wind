using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// BilgeCurves implements an automatic 3D Spatial Bezier in-between curve between FloorCurves and BreadthCurves.
/// Controls arms (fwdP1, fwdP2, aftP1) are automatically positioned on all axes based on proximity to BreadthCurves and FloorCurves.
/// </summary>
[ExecuteInEditMode]
public class BilgeCurves : MonoBehaviour
{


    [Tooltip("Reference to SheerCurves for profile lines (Stem, Stern, Keel).")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to BreadthCurves for Max Breadth (p3) 3D points.")]
    public BreadthCurves breadthCurves;

    [Tooltip("Reference to FloorCurves for Floor Line (p1) 3D points.")]
    public FloorCurves floorCurves;

    [Tooltip("Reference to BodyCurves for Midship Bilge section nodes.")]
    public BodyCurves bodyCurves;

    [Tooltip("Reference to BackboneCurves for stem and stern keel width offset.")]
    public BackboneCurves backboneCurves;

    [Tooltip("Reference to FashionCurves for 3D transom stern boundary node.")]
    public FashionCurves fashionCurves;

    [Header("Interactive Scene Handles")]
    [Tooltip("Enable interactive 3D position handles in Scene view to sculpt control points along X, Y, and Z axes.")]
    public bool enableInteractiveHandles = true;

    [Header("Gizmo Settings")]
    public bool showLabels = true;
    public Color bilgeCurveColor = new Color(0.1f, 0.9f, 0.4f, 0.95f);

    [Header("Initialization State")]
    public bool isInitialized = false;

    // --- DIRECT 3D SPATIAL CONTROL POINTS ---
    [Header("Forward Cubic 3D Control Points (P0..P3)")]
    [Tooltip("Bow Stem Intersect Point (P0). Positioned automatically between FloorCurves.fwdP0 and BreadthCurves.fwdP0.")]
    public Vector3 fwdP0;

    [Tooltip("Bow Entry Tangent Handle (P1). Positioned automatically between FloorCurves.fwdP1 and BreadthCurves.fwdP1.")]
    public Vector3 fwdP1;

    [Tooltip("Midship Forward Tangent Handle (P2). Positioned automatically on Z between FloorCurves and BreadthCurves.")]
    public Vector3 fwdP2;

    [Tooltip("Midship Bilge Apex Point (P3). Z slaved to BreadthCurves fwdP3.z. X and Y derived from bilge ratios.")]
    public Vector3 fwdP3;

    [Header("Aft Quadratic 3D Control Points (P0..P2)")]
    [Tooltip("Stern Quarter Tangent Handle (P1). Positioned automatically on Z between FloorCurves and BreadthCurves.")]
    public Vector3 aftP1;

    [Tooltip("Stern Transom Corner Point (P2). Positioned automatically between FloorCurves.aftP2 and BreadthCurves.aftP2.")]
    public Vector3 aftP2;

    [Header("Diagonal Bilge Ratios (Interpolated between FloorCurves and BreadthCurves)")]
    [Tooltip("Relative width ratio of the Diagonal Bilge Line between FloorCurves (0.0) and BreadthCurves (1.0).")]
    [Range(0.05f, 0.95f)] public float bilgeWidthRatio = 0.50f;

    [Tooltip("Relative height ratio of the Diagonal Bilge Line between FloorCurves (0.0) and BreadthCurves (1.0).")]
    [Range(0.05f, 0.95f)] public float bilgeHeightRatio = 0.50f;

    [Header("Proportional Control Arm Ratios (Read Only / Auto-Derived)")]
    [Tooltip("Derived relative Z position of Midship Forward Arm (P2) along [Midship Apex Z, Bow Tip Z].")]
    [Range(0.01f, 0.99f)] public float fwdP2Ratio = 0.50f;

    [Tooltip("Derived relative Z position of Bow Entry Arm (P1) along [Midship Apex Z, Bow Tip Z].")]
    [Range(0.01f, 0.99f)] public float fwdP1Ratio = 0.75f;

    [Tooltip("Derived relative Z position of Stern Arm (P1) along [Stern Z, Midship Apex Z].")]
    [Range(0.01f, 0.99f)] public float aftP1Ratio = 0.50f;

    [Header("Proportional Control Arm X & Y Ratios (Read Only / Auto-Derived)")]
    [Tooltip("Derived relative X position of Bow Entry Arm (P1).")]
    [Range(0.0f, 1.0f)] public float fwdP1XRatio = 0.50f;

    [Tooltip("Derived relative Y position of Bow Entry Arm (P1).")]
    [Range(0.0f, 1.0f)] public float fwdP1YRatio = 0.50f;

    // --- CACHED BEZIER CONTROL POINTS ---
    [HideInInspector] public Vector3 cachedFwdP0;
    [HideInInspector] public Vector3 cachedFwdP1;
    [HideInInspector] public Vector3 cachedFwdP2;
    [HideInInspector] public Vector3 cachedFwdP3;

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

        if (fashionCurves == null) fashionCurves = GetComponentInParent<FashionCurves>();
        if (fashionCurves == null) fashionCurves = FindFirstObjectByType<FashionCurves>();
    }

    [ContextMenu("Reset Control Points to Default Hull")]
    public void InitializeControlPointsFromShip()
    {
        FindReferences();

        float bowZ = (sheerCurves != null) ? sheerCurves.stemP3.z : 15f;
        float sternZ = (sheerCurves != null) ? sheerCurves.sternP3.z : -15f;
        float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;

        float floorX = (floorCurves != null) ? floorCurves.fwdP2.x : 2f;
        float floorY = (floorCurves != null) ? floorCurves.fwdP2.y : 1f;
        float hobX = (breadthCurves != null) ? breadthCurves.fwdP3.x : 4f;
        float hobY = (breadthCurves != null) ? breadthCurves.fwdP3.y : 3f;

        float midX = Mathf.Lerp(floorX, hobX, bilgeWidthRatio);
        float midY = Mathf.Lerp(floorY, hobY, bilgeHeightRatio);

        fwdP0 = new Vector3(0f, midY + 1f, bowZ);
        fwdP3 = new Vector3(midX, midY, midZ); // Midship Apex (P3)
        fwdP2 = new Vector3(midX, midY, midZ + (bowZ - midZ) * 0.5f);
        fwdP1 = new Vector3(midX * 0.5f, midY, midZ + (bowZ - midZ) * 0.75f);

        float floorSternY = (floorCurves != null) ? floorCurves.aftP2.y : midY;
        float breadthSternX = (breadthCurves != null) ? breadthCurves.aftP2.x : hobX * 0.5f;
        float breadthSternY = (breadthCurves != null) ? breadthCurves.aftP2.y : midY + 2f;

        float transomX = Mathf.Lerp(0f, breadthSternX, bilgeWidthRatio);
        float transomY = Mathf.Lerp(floorSternY, breadthSternY, bilgeHeightRatio);
        aftP2 = new Vector3(transomX, transomY, sternZ);
        aftP1 = new Vector3(midX, midY, sternZ + (midZ - sternZ) * 0.5f);

        fwdP2Ratio = 0.50f;
        fwdP1Ratio = 0.75f;
        aftP1Ratio = 0.50f;
        fwdP1XRatio = 0.50f;
        fwdP1YRatio = 0.50f;
        isInitialized = true;
    }

    private void OnValidate()
    {
        UpdateCachedCurves();
    }

    public virtual void UpdateCachedCurves()
    {
        EnsureInitialized();
        FindReferences();

        if (sheerCurves != null) sheerCurves.UpdateCachedProfile();
        if (breadthCurves != null) breadthCurves.UpdateCachedCurves();
        if (floorCurves != null) floorCurves.UpdateCachedCurves();

        float targetApexZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;

        // 1. Obtain starting Midship Apex (P3) directly from BodyCurves Midship p2 node
        if (bodyCurves != null)
        {
            SectionNodes midNodes = bodyCurves.GetSectionNodesAtZ(targetApexZ, 1f);
            fwdP3.x = Mathf.Abs(midNodes.p2.x);
            fwdP3.y = midNodes.p2.y;
            fwdP3.z = targetApexZ;

            bilgeWidthRatio = bodyCurves.bilgeWidthRatio;
            bilgeHeightRatio = bodyCurves.bilgeHeightRatio;
        }
        else
        {
            float floorMidX = (floorCurves != null) ? floorCurves.fwdP2.x : 2f;
            float floorMidY = (floorCurves != null) ? floorCurves.fwdP2.y : 0.5f;
            float hobMidX = (breadthCurves != null) ? breadthCurves.fwdP3.x : 4f;
            float hobMidY = (breadthCurves != null) ? breadthCurves.fwdP3.y : 3f;

            fwdP3.x = Mathf.Lerp(floorMidX, hobMidX, Mathf.Clamp01(bilgeWidthRatio));
            fwdP3.y = Mathf.Lerp(floorMidY, hobMidY, Mathf.Clamp01(bilgeHeightRatio));
            fwdP3.z = targetApexZ;
        }

        float floorMidYVal = (floorCurves != null) ? floorCurves.fwdP2.y : 0.5f;
        float hobMidYVal = (breadthCurves != null) ? breadthCurves.fwdP3.y : 3f;
        float heightSpanMid = hobMidYVal - floorMidYVal;
        float tMid = (heightSpanMid > 0.001f) ? Mathf.Clamp01((fwdP3.y - floorMidYVal) / heightSpanMid) : Mathf.Clamp01(bilgeHeightRatio);

        float minY = fwdP3.y;
        float maxStemY = (sheerCurves != null) ? sheerCurves.stemP3.y : minY + 10f;
        float maxSternY = (sheerCurves != null) ? sheerCurves.sternP3.y : minY + 10f;

        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);

        // 3. Bow Point (fwdP0): Positioned automatically between FloorCurves.fwdP0 and BreadthCurves.fwdP0 on Backbone (+hw)
        Vector3 floorBowPt = (floorCurves != null) ? floorCurves.fwdP0 : new Vector3(hw, minY, (sheerCurves != null) ? sheerCurves.stemP3.z : 15f);
        Vector3 breadthBowPt = (breadthCurves != null) ? breadthCurves.fwdP0 : new Vector3(hw, minY + 2f, (sheerCurves != null) ? sheerCurves.stemP3.z : 15f);

        fwdP0.x = hw;
        fwdP0.y = Mathf.Lerp(floorBowPt.y, breadthBowPt.y, tMid);
        if (sheerCurves != null)
        {
            fwdP0.z = sheerCurves.GetStemProfileZAtY(fwdP0.y);
        }
        else
        {
            fwdP0.z = Mathf.Lerp(floorBowPt.z, breadthBowPt.z, tMid);
        }

        // 4. Stern Transom Corner Point (aftP2): Bound directly to FashionCurves P2 (Transom Bilge Node) if assigned
        if (fashionCurves != null)
        {
            fashionCurves.UpdateCachedCurves();
            aftP2 = fashionCurves.stbdP2;
        }
        else
        {
            Vector3 floorSternPt = (floorCurves != null) ? floorCurves.aftP2 : new Vector3(0f, minY, (sheerCurves != null) ? sheerCurves.sternP3.z : -15f);
            Vector3 breadthSternPt = (breadthCurves != null) ? (breadthCurves.cachedIsAftCubic ? breadthCurves.aftP3 : breadthCurves.aftP2) : new Vector3(fwdP3.x * 0.5f, minY + 2f, (sheerCurves != null) ? sheerCurves.sternP3.z : -15f);

            aftP2.x = Mathf.Lerp(floorSternPt.x, breadthSternPt.x, tMid);
            aftP2.y = Mathf.Lerp(floorSternPt.y, breadthSternPt.y, tMid);
            if (sheerCurves != null)
            {
                aftP2.z = sheerCurves.GetSternProfileZAtY(aftP2.y);
            }
            else
            {
                aftP2.z = Mathf.Lerp(floorSternPt.z, breadthSternPt.z, tMid);
            }
        }

        float bowZ = fwdP0.z;
        float sternZ = aftP2.z;

        // 5. AUTOMATIC CONTROL ARM PLACEMENT (Interpolated between FloorCurves and BreadthCurves based on tMid)
        if (floorCurves != null && breadthCurves != null)
        {
            // --- MIDSHIP FORWARD ARM (fwdP2): Positioned on Z between FloorCurves.fwdP1 and BreadthCurves.fwdP2 based on tMid ---
            fwdP2.z = Mathf.Lerp(floorCurves.fwdP1.z, breadthCurves.fwdP2.z, tMid);

            // --- BOW ENTRY ARM (fwdP1): Positioned on X, Y, Z between FloorCurves.fwdP1 and BreadthCurves.fwdP1 based on tMid ---
            fwdP1.z = Mathf.Lerp(floorCurves.fwdP1.z, breadthCurves.fwdP1.z, tMid);
            fwdP1.x = Mathf.Lerp(floorCurves.fwdP1.x, breadthCurves.fwdP1.x, tMid);
            fwdP1.y = Mathf.Lerp(floorCurves.fwdP1.y, breadthCurves.fwdP1.y, tMid);

            // --- STERN ARM (aftP1): Positioned on Z between FloorCurves.aftP1 and BreadthCurves.aftP1 based on tMid ---
            aftP1.z = Mathf.Lerp(floorCurves.aftP1.z, breadthCurves.aftP1.z, tMid);
        }

        // --- C1 TANGENT CONTINUITY AT MIDSHIP APEX (P3) ---
        // Lock Midship Forward Arm (fwdP2) and Midship Aft Arm (aftP1) straight on Z axis (X = fwdP3.x, Y = fwdP3.y)
        fwdP2.x = fwdP3.x;
        fwdP2.y = fwdP3.y;

        aftP1.x = fwdP3.x;
        aftP1.y = fwdP3.y;

        // Update derived ratios for inspector readouts
        float fwdSpan = bowZ - targetApexZ;
        if (fwdSpan > 0.001f)
        {
            fwdP2Ratio = Mathf.Clamp01((fwdP2.z - targetApexZ) / fwdSpan);
            fwdP1Ratio = Mathf.Clamp01((fwdP1.z - targetApexZ) / fwdSpan);
        }

        float aftSpan = targetApexZ - sternZ;
        if (aftSpan > 0.001f)
        {
            aftP1Ratio = Mathf.Clamp01((aftP1.z - sternZ) / aftSpan);
        }

        float bilgeWidthSpan = fwdP3.x - hw;
        if (bilgeWidthSpan > 0.001f)
        {
            fwdP1XRatio = Mathf.Clamp01((fwdP1.x - hw) / bilgeWidthSpan);
        }

        // Cache Forward Cubic Bezier Control Points (P0..P3)
        cachedFwdP0 = fwdP0;
        cachedFwdP1 = fwdP1;
        cachedFwdP2 = fwdP2;
        cachedFwdP3 = fwdP3;

        // Cache Aft Quadratic Bezier Control Points (P0..P2, where P0 is Midship Bilge Apex fwdP3)
        cachedAftP0 = fwdP3;
        cachedAftP1 = aftP1;
        cachedAftP2 = aftP2;

        changeVersion++;
    }

    // --- 3D SPATIAL EVALUATION METHODS ---

    public virtual float GetBilgeXAtZ(float z)
    {
        return GetBilgePointAtZ(z).x;
    }

    public virtual float GetBilgeYAtZ(float z)
    {
        return GetBilgePointAtZ(z).y;
    }

    public virtual Vector3 GetBilgePointAtZ(float z)
    {
        EnsureInitialized();

        if (z >= cachedFwdP3.z)
        {
            if (z >= cachedFwdP0.z) return cachedFwdP0;
            float t = GetBezierTAtZ(cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3, z, true);
            return CalculateCubicBezierPoint(t, cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3);
        }
        else
        {
            if (z <= cachedAftP2.z) return cachedAftP2;
            float t = GetBezierTAtZQuadratic(cachedAftP0, cachedAftP1, cachedAftP2, z, false);
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

    public Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        return (uuu * p0) + (3f * uu * t * p1) + (3f * u * tt * p2) + (ttt * p3);
    }

    public float GetBezierTAtZQuadratic(Vector3 p0, Vector3 p1, Vector3 p2, float targetZ, bool isForward)
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

            if ((isForward && zVal < targetZ) || (!isForward && zVal > targetZ)) min = t;
            else max = t;
            t = (min + max) * 0.5f;
        }

        return t;
    }

    public float GetBezierTAtZ(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float targetZ, bool isForward)
    {
        float min = 0f;
        float max = 1f;
        float t = 0.5f;

        for (int i = 0; i < 10; i++)
        {
            float u = 1f - t;
            float zVal = (u * u * u * p0.z) + (3f * u * u * t * p1.z) + (3f * u * t * t * p2.z) + (t * t * t * p3.z);
            float error = zVal - targetZ;

            if (Mathf.Abs(error) < 0.00005f) return t;

            if ((isForward && zVal < targetZ) || (!isForward && zVal > targetZ)) min = t;
            else max = t;
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

        // Draw Starboard 3D Bilge Curve (Cubic Forward, Quadratic Aft)
        DrawCubicBezierCurve(cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3, bilgeCurveColor);
        DrawQuadraticBezierCurve(cachedAftP0, cachedAftP1, cachedAftP2, bilgeCurveColor);

        // Draw Port 3D Bilge Curve (Mirrored X)
        Color portColor = new Color(bilgeCurveColor.r, bilgeCurveColor.g * 0.6f, bilgeCurveColor.b, bilgeCurveColor.a);
        DrawCubicBezierCurve(MirrorX(cachedFwdP0), MirrorX(cachedFwdP1), MirrorX(cachedFwdP2), MirrorX(cachedFwdP3), portColor);
        DrawQuadraticBezierCurve(MirrorX(cachedAftP0), MirrorX(cachedAftP1), MirrorX(cachedAftP2), portColor);

        // Draw Bilge Transom Line connecting Starboard Transom Corner and Port Transom Corner
        Gizmos.color = bilgeCurveColor;
        Gizmos.DrawLine(cachedAftP2, MirrorX(cachedAftP2));

        // Draw 3D Control Polygon Skeleton Arms
        Gizmos.color = new Color(0.1f, 0.9f, 0.4f, 0.6f);
        Gizmos.DrawLine(cachedFwdP0, cachedFwdP1);
        Gizmos.DrawLine(cachedFwdP3, cachedFwdP2);
        Gizmos.DrawLine(cachedAftP0, cachedAftP1);
        Gizmos.DrawLine(cachedAftP1, cachedAftP2);

        // Draw Control Point Spheres
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(cachedFwdP0, 0.12f);
        Gizmos.DrawSphere(cachedFwdP1, 0.09f);
        Gizmos.DrawSphere(cachedFwdP2, 0.09f);
        Gizmos.DrawSphere(cachedFwdP3, 0.15f); // Midship Bilge Apex (P3)
        Gizmos.DrawSphere(cachedAftP1, 0.09f);
        Gizmos.DrawSphere(cachedAftP2, 0.12f);

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = GetCachedLabelStyle();
            Handles.Label(cachedFwdP0 + Vector3.up * 0.2f, "Bilge Bow Tip (Stem)", labelStyle);
            Handles.Label(cachedFwdP3 + Vector3.up * 0.2f, $"Bilge Midship Apex ({cachedFwdP3.x:F2}m)", labelStyle);
            Handles.Label(cachedAftP2 + Vector3.up * 0.2f, "Bilge Transom Corner", labelStyle);

            // Render Inner Boundary Chord Line (Cyan Dotted)
            Handles.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            Handles.DrawDottedLine(cachedFwdP0, cachedFwdP2, 3f);
            Handles.DrawDottedLine(MirrorX(cachedFwdP0), MirrorX(cachedFwdP2), 3f);
        }
#endif
    }

    private void DrawCubicBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color, int segments = 40)
    {
        Gizmos.color = color;
        Vector3 prev = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 curr = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }
    }

    private void DrawQuadraticBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Color color, int segments = 30)
    {
        Gizmos.color = color;
        Vector3 prev = p0;
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
[CustomEditor(typeof(BilgeCurves))]
public class BilgeCurvesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BilgeCurves curves = (BilgeCurves)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update Bilge Curves Profile", GUILayout.Height(30)))
        {
            Undo.RecordObject(curves, "Update Bilge Profile");
            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);
        }
    }

    private void OnSceneGUI()
    {
        BilgeCurves curves = (BilgeCurves)target;
        if (!curves.enableInteractiveHandles) return;

        curves.UpdateCachedCurves();

        EditorGUI.BeginChangeCheck();

        // Midship Bilge Apex Handle (fwdP3) - Primary control handle driving Bilge Beam (X), Bilge Height (Y), and Station Z
        Handles.color = Color.green;
        Vector3 newFwdP3 = Handles.FreeMoveHandle(curves.fwdP3, 0.18f, Vector3.zero, Handles.SphereHandleCap);
        Handles.color = Color.red; Vector3 xArrowFwdP3 = Handles.Slider(curves.fwdP3, Vector3.right, 0.8f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(xArrowFwdP3.x - curves.fwdP3.x) > 0.001f) newFwdP3.x = xArrowFwdP3.x;
        Handles.color = Color.green; Vector3 yArrowFwdP3 = Handles.Slider(curves.fwdP3, Vector3.up, 0.8f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(yArrowFwdP3.y - curves.fwdP3.y) > 0.001f) newFwdP3.y = yArrowFwdP3.y;
        Handles.color = Color.blue; Vector3 zArrowFwdP3 = Handles.Slider(curves.fwdP3, Vector3.forward, 0.8f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(zArrowFwdP3.z - curves.fwdP3.z) > 0.001f) newFwdP3.z = zArrowFwdP3.z;

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(curves, "Manipulate Midship Bilge Apex Handle");

            float floorMidY = (curves.floorCurves != null) ? curves.floorCurves.fwdP2.y : 0.5f;
            float breadthMidY = (curves.breadthCurves != null) ? curves.breadthCurves.fwdP3.y : floorMidY + 3.0f;
            float floorMidX = (curves.floorCurves != null) ? curves.floorCurves.fwdP2.x : 2.0f;
            float breadthMidX = (curves.breadthCurves != null) ? curves.breadthCurves.fwdP3.x : 4.0f;

            // Update Midship Bilge Apex ratios from newFwdP3
            float widthSpan = breadthMidX - floorMidX;
            if (widthSpan > 0.001f)
            {
                curves.bilgeWidthRatio = Mathf.Clamp01((Mathf.Abs(newFwdP3.x) - floorMidX) / widthSpan);
            }

            float heightSpan = breadthMidY - floorMidY;
            if (heightSpan > 0.001f)
            {
                curves.bilgeHeightRatio = Mathf.Clamp01((newFwdP3.y - floorMidY) / heightSpan);
            }

            if (curves.bodyCurves != null)
            {
                Undo.RecordObject(curves.bodyCurves, "Manipulate Midship Bilge Apex Handle");
                curves.bodyCurves.bilgeWidthRatio = curves.bilgeWidthRatio;
                curves.bodyCurves.bilgeHeightRatio = curves.bilgeHeightRatio;
                curves.bodyCurves.UpdateCachedCurves();
                EditorUtility.SetDirty(curves.bodyCurves);
            }
            else
            {
                curves.UpdateCachedCurves();
            }
            EditorUtility.SetDirty(curves);
        }
    }
}
#endif
