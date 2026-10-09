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

    [Tooltip("Reference to TopLineCurves for Top Line 3D points.")]
    public TopLineCurves topLineCurves;

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

    [Tooltip("Reference to BowCtrlArmGuide for unified forward bow control arms.")]
    public BowCtrlArmGuide bowCtrlArmGuide;

    [Header("Interactive Scene Handles")]
    [Tooltip("Enable interactive 3D position handles in Scene view to sculpt control points along X, Y, and Z axes.")]
    public bool enableInteractiveHandles = true;

    [Header("Gizmo Settings")]
    public bool showLabels = true;
    public Color bilgeCurveColor = new Color(0.1f, 0.9f, 0.4f, 0.95f);

    [Header("Forward Control Arm Guides (Dashed Lines)")]
    [Tooltip("Show the dashed 3D Bow Mould P1 section guide line (TopLine down to Floor).")]
    public bool showBowMouldP1Guide = true;

    [Tooltip("Show the dashed 3D Forward Midship P2 section guide line (TopLine down to Keel).")]
    public bool showFwdP2Guide = true;

    public Color bowMouldGuideColor = new Color(0.1f, 0.95f, 0.85f, 0.95f); // Vibrant Cyan
    public Color fwdP2GuideColor = new Color(0.95f, 0.85f, 0.1f, 0.95f);    // Vibrant Gold

    [Header("Aft Control Arm Guides (Dashed Lines)")]
    [Tooltip("Show the dashed 3D Aft Mould A1 section guide line (TopLine down to Floor).")]
    public bool showAftMouldA1Guide = true;

    [Tooltip("Show the dashed 3D Stern Transom Boundary A2 guide line (TopLine down to Keel).")]
    public bool showSternTransomGuide = true;

    public Color aftMouldGuideColor = new Color(0.75f, 0.25f, 0.95f, 0.95f);   // Vibrant Purple
    public Color sternTransomGuideColor = new Color(0.95f, 0.25f, 0.55f, 0.95f); // Vibrant Rose / Pink

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

        if (fashionCurves == null) fashionCurves = GetComponentInParent<FashionCurves>();
        if (fashionCurves == null) fashionCurves = FindFirstObjectByType<FashionCurves>();

        if (bowCtrlArmGuide == null) bowCtrlArmGuide = GetComponentInParent<BowCtrlArmGuide>();
        if (bowCtrlArmGuide == null) bowCtrlArmGuide = FindFirstObjectByType<BowCtrlArmGuide>();
    }

    [ContextMenu("Reset Control Points to Default Hull")]
    public void InitializeControlPointsFromShip()
    {
        FindReferences();

        float bowZ = (sheerCurves != null) ? sheerCurves.stemP3.z : 15f;
        float sternZ = (sheerCurves != null) ? sheerCurves.sternP3.z : -15f;
        float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;

        float floorX = (floorCurves != null) ? floorCurves.fwdP3.x : 2f;
        float floorY = (floorCurves != null) ? floorCurves.fwdP3.y : 1f;
        float hobX = (breadthCurves != null) ? breadthCurves.fwdP3.x : 4f;
        float hobY = (breadthCurves != null) ? breadthCurves.fwdP3.y : 3f;

        float midX = Mathf.Lerp(floorX, hobX, bilgeWidthRatio);
        float midY = Mathf.Lerp(floorY, hobY, bilgeHeightRatio);

        fwdP0 = new Vector3(0f, midY + 1f, bowZ);
        fwdP3 = new Vector3(midX, midY, midZ); // Midship Apex (P3)
        fwdP2 = new Vector3(midX, midY, midZ + (bowZ - midZ) * 0.5f);
        fwdP1 = new Vector3(midX * 0.5f, midY, midZ + (bowZ - midZ) * 0.75f);

        float floorSternY = (floorCurves != null) ? floorCurves.aftP3.y : midY;
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
        if (topLineCurves != null) topLineCurves.UpdateCachedCurves();
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
            float floorMidX = (floorCurves != null) ? floorCurves.fwdP3.x : 2f;
            float floorMidY = (floorCurves != null) ? floorCurves.fwdP3.y : 0.5f;
            float hobMidX = (breadthCurves != null) ? breadthCurves.fwdP3.x : 4f;
            float hobMidY = (breadthCurves != null) ? breadthCurves.fwdP3.y : 3f;

            fwdP3.x = Mathf.Lerp(floorMidX, hobMidX, Mathf.Clamp01(bilgeWidthRatio));
            fwdP3.y = Mathf.Lerp(floorMidY, hobMidY, Mathf.Clamp01(bilgeHeightRatio));
            fwdP3.z = targetApexZ;
        }

        float floorMidYVal = (floorCurves != null) ? floorCurves.fwdP3.y : 0.5f;
        float hobMidYVal = (breadthCurves != null) ? breadthCurves.fwdP3.y : 3f;
        float heightSpanMid = hobMidYVal - floorMidYVal;
        float tMid = (heightSpanMid > 0.001f) ? Mathf.Clamp01((fwdP3.y - floorMidYVal) / heightSpanMid) : Mathf.Clamp01(bilgeHeightRatio);

        float minY = fwdP3.y;
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
            Vector3 floorSternPt = (floorCurves != null) ? floorCurves.aftP3 : new Vector3(0f, minY, (sheerCurves != null) ? sheerCurves.sternP3.z : -15f);
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

        // 5. AUTOMATIC CONTROL ARM PLACEMENT (Native Bow & Aft Mould Evaluation)
        float uBilge = 0.50f; // Bilge curve section parameter (u=0.50)
        if (bowCtrlArmGuide != null)
        {
            bowCtrlArmGuide.EnsureInitialized();
            fwdP1 = bowCtrlArmGuide.EvaluateBowMouldP1AtU(uBilge);
            Vector3 guideP2 = bowCtrlArmGuide.EvaluateFwdP2AtU(uBilge);
            fwdP2.z = guideP2.z;
        }
        else
        {
            fwdP1 = EvaluateBowMouldP1AtU(uBilge);
            Vector3 guideP2 = EvaluateFwdP2AtU(uBilge);
            fwdP2.z = guideP2.z;
        }

        Vector3 guideAftA1 = EvaluateAftMouldA1AtU(uBilge);
        aftP1.z = guideAftA1.z;

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

    /// <summary>
    /// Mathematically evaluates the 3D Bow Mould entry control handle (fwdP1) at parameter u
    /// by scaling the master BodyCurves section through the Key Shape Bow Tip arms
    /// (TopLineCurves.fwdP1, BreadthCurves.fwdP1, FloorCurves.fwdP1) and locking Z to the Stem Post profile.
    /// Range for P1 guide is Floor (u=0.25) to TopLine (u=1.00).
    /// </summary>
    public Vector3 EvaluateBowMouldP1AtU(float u)
    {
        float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
        float bowZ = (sheerCurves != null) ? sheerCurves.stemP3.z : 15f;
        float fwdSpan = bowZ - midZ;

        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        Vector3 floorP1 = (floorCurves != null) ? floorCurves.fwdP1 : new Vector3(hw + 1f, keelY + 0.5f, midZ + fwdSpan * 0.5f);
        Vector3 breadthP1 = (breadthCurves != null) ? breadthCurves.fwdP1 : new Vector3(hw + 3f, keelY + 2.5f, midZ + fwdSpan * 0.75f);
        Vector3 topLineP1 = (topLineCurves != null) ? topLineCurves.fwdP1 : new Vector3(hw + 3.5f, keelY + 3.5f, midZ + fwdSpan * 0.85f);

        SectionNodes midNodes = (bodyCurves != null) ? bodyCurves.GetSectionNodesAtZ(midZ, 1f) : new SectionNodes();

        Vector3 secFloor = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(0.25f, midNodes) : new Vector3(hw + 1f, keelY + 0.5f, midZ);
        Vector3 secBreadth = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(0.75f, midNodes) : new Vector3(hw + 3f, keelY + 2.5f, midZ);
        Vector3 secTop = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(1.00f, midNodes) : new Vector3(hw + 3.5f, keelY + 3.5f, midZ);

        float floorMidX = Mathf.Max(hw + 0.001f, Mathf.Abs(secFloor.x));
        float floorMidY = Mathf.Max(keelY + 0.001f, secFloor.y);
        float sX_floor = (floorP1.x - hw) / (floorMidX - hw);
        float sY_floor = (floorP1.y - keelY) / (floorMidY - keelY);

        float bMidX = Mathf.Max(hw + 0.001f, Mathf.Abs(secBreadth.x));
        float bMidY = Mathf.Max(keelY + 0.001f, secBreadth.y);
        float sX_breadth = (breadthP1.x - hw) / (bMidX - hw);
        float sY_breadth = (breadthP1.y - keelY) / (bMidY - keelY);

        float topMidX = Mathf.Max(hw + 0.001f, Mathf.Abs(secTop.x));
        float topMidY = Mathf.Max(keelY + 0.001f, secTop.y);
        float sX_top = (topLineP1.x - hw) / (topMidX - hw);
        float sY_top = (topLineP1.y - keelY) / (topMidY - keelY);

        float sX, sY;
        if (u >= 0.75f)
        {
            float tUpper = (u - 0.75f) / 0.25f;
            sX = Mathf.Lerp(sX_breadth, sX_top, tUpper);
            sY = Mathf.Lerp(sY_breadth, sY_top, tUpper);
        }
        else if (u >= 0.25f)
        {
            float tMidBand = (u - 0.25f) / 0.50f;
            sX = Mathf.Lerp(sX_floor, sX_breadth, tMidBand);
            sY = Mathf.Lerp(sY_floor, sY_breadth, tMidBand);
        }
        else // u < 0.25f (At and below Floor level, fwdP1 merges into fwdP2)
        {
            float tLower = u / 0.25f;
            sX = Mathf.Lerp(1.0f, sX_floor, tLower);
            sY = Mathf.Lerp(1.0f, sY_floor, tLower);
        }

        Vector3 secPt = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(u, midNodes) : new Vector3(hw + u * 3f, keelY + u * 2.5f, midZ);

        float xVal = hw + (Mathf.Abs(secPt.x) - hw) * sX;
        float yVal = keelY + (secPt.y - keelY) * sY;

        Vector3 pt = new Vector3(xVal, yVal, 0f);
        pt.z = EvaluateCenterlineStemProfileZAtY(pt.y);
        return pt;
    }

    /// <summary>
    /// Mathematically evaluates the 3D Forward Midship control handle (fwdP2) at parameter u.
    /// Range for P2 guide is Keel (u=0.00) up to TopLine (u=1.00).
    /// </summary>
    public Vector3 EvaluateFwdP2AtU(float u)
    {
        float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
        float bowZ = (sheerCurves != null) ? sheerCurves.stemP3.z : 15f;
        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : 0.25f;
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        SectionNodes midNodes = (bodyCurves != null) ? bodyCurves.GetSectionNodesAtZ(midZ, 1f) : new SectionNodes();
        Vector3 secPtP3 = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(u, midNodes) : new Vector3(hw + u * 4f, keelY + u * 3f, midZ);
        Vector3 p3 = new Vector3(Mathf.Abs(secPtP3.x), secPtP3.y, midZ);

        Vector3 pt2 = new Vector3(p3.x, p3.y, 0f);
        pt2.z = EvaluateCenterlineFwdP2ProfileZAtY(pt2.y);
        return pt2;
    }

    /// <summary>
    /// Mathematically evaluates the 3D Aft Mould tangent control handle (aftP1) at parameter u
    /// by scaling the master BodyCurves section through the Key Shape Aft arms
    /// (TopLineCurves.aftP1, BreadthCurves.aftP1, FloorCurves.aftP1) and locking Z to the Stern Profile.
    /// Range for A1 guide is Keel (u=0.00) to TopLine (u=1.00).
    /// </summary>
    public Vector3 EvaluateAftMouldA1AtU(float u)
    {
        float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        Vector3 floorA1 = (floorCurves != null) ? floorCurves.aftP1 : new Vector3(hw + 1f, keelY + 0.5f, midZ - 5f);
        Vector3 breadthA1 = (breadthCurves != null) ? breadthCurves.aftP1 : new Vector3(hw + 3f, keelY + 2.5f, midZ - 7f);
        Vector3 topLineA1 = (topLineCurves != null) ? topLineCurves.aftP1 : new Vector3(hw + 3.5f, keelY + 3.5f, midZ - 8f);

        SectionNodes midNodes = (bodyCurves != null) ? bodyCurves.GetSectionNodesAtZ(midZ, 1f) : new SectionNodes();

        Vector3 secFloor = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(0.25f, midNodes) : new Vector3(hw + 1f, keelY + 0.5f, midZ);
        Vector3 secBreadth = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(0.75f, midNodes) : new Vector3(hw + 3f, keelY + 2.5f, midZ);
        Vector3 secTop = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(1.00f, midNodes) : new Vector3(hw + 3.5f, keelY + 3.5f, midZ);

        float floorMidX = Mathf.Max(hw + 0.001f, Mathf.Abs(secFloor.x));
        float floorMidY = Mathf.Max(keelY + 0.001f, secFloor.y);
        float sX_floor = (floorA1.x - hw) / (floorMidX - hw);
        float sY_floor = (floorA1.y - keelY) / (floorMidY - keelY);

        float bMidX = Mathf.Max(hw + 0.001f, Mathf.Abs(secBreadth.x));
        float bMidY = Mathf.Max(keelY + 0.001f, secBreadth.y);
        float sX_breadth = (breadthA1.x - hw) / (bMidX - hw);
        float sY_breadth = (breadthA1.y - keelY) / (bMidY - keelY);

        float topMidX = Mathf.Max(hw + 0.001f, Mathf.Abs(secTop.x));
        float topMidY = Mathf.Max(keelY + 0.001f, secTop.y);
        float sX_top = (topLineA1.x - hw) / (topMidX - hw);
        float sY_top = (topLineA1.y - keelY) / (topMidY - keelY);

        float sX, sY;
        if (u >= 0.75f)
        {
            float tUpper = (u - 0.75f) / 0.25f;
            sX = Mathf.Lerp(sX_breadth, sX_top, tUpper);
            sY = Mathf.Lerp(sY_breadth, sY_top, tUpper);
        }
        else if (u >= 0.25f)
        {
            float tMidBand = (u - 0.25f) / 0.50f;
            sX = Mathf.Lerp(sX_floor, sX_breadth, tMidBand);
            sY = Mathf.Lerp(sY_floor, sY_breadth, tMidBand);
        }
        else
        {
            float tLower = u / 0.25f;
            sX = Mathf.Lerp(1.0f, sX_floor, tLower);
            sY = Mathf.Lerp(1.0f, sY_floor, tLower);
        }

        Vector3 secPt = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(u, midNodes) : new Vector3(hw + u * 3f, keelY + u * 2.5f, midZ);

        float xVal = hw + (Mathf.Abs(secPt.x) - hw) * sX;
        float yVal = keelY + (secPt.y - keelY) * sY;

        Vector3 pt = new Vector3(xVal, yVal, 0f);

        pt.z = EvaluateCenterlineSternProfileZAtY(pt.y);

        return pt;
    }

    /// <summary>
    /// Mathematically evaluates the 3D Stern Transom Corner / Boundary node (aftP2) at parameter u.
    /// Range for A2 guide is Keel (u=0.00) up to TopLine (u=1.00).
    /// </summary>
    public Vector3 EvaluateAftTransomA2AtU(float u)
    {
        if (fashionCurves != null)
        {
            if (u >= 0.25f)
            {
                float tFashion = (u - 0.25f) / 0.75f;
                return fashionCurves.GetFashionPointAtT(tFashion, 1f);
            }
            else
            {
                float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;
                float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : 0.25f;
                float keelZ = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(keelY) : -15f;
                Vector3 keelPt = new Vector3(hw, keelY, keelZ);
                Vector3 floorPt = fashionCurves.stbdP1;
                float tLower = u / 0.25f;
                return Vector3.Lerp(keelPt, floorPt, tLower);
            }
        }
        else
        {
            float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
            float sternZ = (sheerCurves != null) ? sheerCurves.sternP3.z : -15f;
            float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : 0.25f;
            float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

            Vector3 floorA2 = (floorCurves != null) ? floorCurves.aftP3 : new Vector3(hw, keelY + 0.5f, sternZ + 2f);
            Vector3 breadthA2 = (breadthCurves != null) ? (breadthCurves.cachedIsAftCubic ? breadthCurves.aftP3 : breadthCurves.aftP2) : new Vector3(hw + 3f, keelY + 2.5f, sternZ);
            Vector3 topLineA2 = (topLineCurves != null) ? (topLineCurves.cachedIsAftCubic ? topLineCurves.aftP3 : topLineCurves.aftP2) : new Vector3(hw + 3.5f, keelY + 3.5f, sternZ);

            if (u >= 0.75f)
            {
                float tUpper = (u - 0.75f) / 0.25f;
                return Vector3.Lerp(breadthA2, topLineA2, tUpper);
            }
            else if (u >= 0.25f)
            {
                float tMidBand = (u - 0.25f) / 0.50f;
                return Vector3.Lerp(floorA2, breadthA2, tMidBand);
            }
            else
            {
                float keelZ = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(keelY) : sternZ;
                Vector3 keelPt = new Vector3(hw, keelY, keelZ);
                float tLower = u / 0.25f;
                return Vector3.Lerp(keelPt, floorA2, tLower);
            }
        }
    }

    public float EvaluateCenterlineSternProfileZAtY(float targetY)
    {
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        Vector3 floorA1 = (floorCurves != null) ? floorCurves.aftP1 : new Vector3(1f, keelY + 0.5f, -10f);
        Vector3 breadthA1 = (breadthCurves != null) ? breadthCurves.aftP1 : new Vector3(3f, keelY + 2.5f, -12f);
        Vector3 topLineA1 = (topLineCurves != null) ? topLineCurves.aftP1 : new Vector3(3.5f, keelY + 3.5f, -13f);
        float keelZ = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(keelY) : floorA1.z;

        // 1. Upper Band: Between Breadth (u=0.75) and TopLine (u=1.00)
        if (targetY >= breadthA1.y)
        {
            float dyUpper = topLineA1.y - breadthA1.y;
            if (Mathf.Abs(dyUpper) < 0.0001f) return breadthA1.z;

            if (sheerCurves != null)
            {
                float sternZ_breadth = sheerCurves.GetSternProfileZAtY(breadthA1.y);
                float sternZ_topLine = sheerCurves.GetSternProfileZAtY(topLineA1.y);
                float sternZ_target = sheerCurves.GetSternProfileZAtY(targetY);

                float dSternSpan = sternZ_topLine - sternZ_breadth;
                if (Mathf.Abs(dSternSpan) > 0.0001f)
                {
                    float tSternShape = Mathf.Clamp01((sternZ_target - sternZ_breadth) / dSternSpan);
                    return Mathf.Lerp(breadthA1.z, topLineA1.z, tSternShape);
                }
            }

            float tUpper = Mathf.Clamp01((targetY - breadthA1.y) / dyUpper);
            return Mathf.Lerp(breadthA1.z, topLineA1.z, tUpper);
        }

        // 2. Middle Band: Between Floor (u=0.25) and Breadth (u=0.75)
        if (targetY >= floorA1.y)
        {
            float dyMid = breadthA1.y - floorA1.y;
            if (Mathf.Abs(dyMid) < 0.0001f) return floorA1.z;

            if (sheerCurves != null)
            {
                float sternZ_floor = sheerCurves.GetSternProfileZAtY(floorA1.y);
                float sternZ_breadth = sheerCurves.GetSternProfileZAtY(breadthA1.y);
                float sternZ_target = sheerCurves.GetSternProfileZAtY(targetY);

                float dSternSpan = sternZ_breadth - sternZ_floor;
                if (Mathf.Abs(dSternSpan) > 0.0001f)
                {
                    float tSternShape = Mathf.Clamp01((sternZ_target - sternZ_floor) / dSternSpan);
                    return Mathf.Lerp(floorA1.z, breadthA1.z, tSternShape);
                }
            }

            float tMid = Mathf.Clamp01((targetY - floorA1.y) / dyMid);
            return Mathf.Lerp(floorA1.z, breadthA1.z, tMid);
        }

        // 3. Lower Band (Below Floor down to Keel):
        // Follow the scaled/stretched Stern Profile continuation from Floor down to Keel
        float sternZ_floorLower = sheerCurves.GetSternProfileZAtY(floorA1.y);
        float sternZ_breadthLower = sheerCurves.GetSternProfileZAtY(breadthA1.y);
        float sternZ_targetLower = sheerCurves.GetSternProfileZAtY(targetY);

        float dSternMidSpan = sternZ_breadthLower - sternZ_floorLower;
        float scaleZ = 1.0f;
        if (Mathf.Abs(dSternMidSpan) > 0.0001f)
        {
            scaleZ = (breadthA1.z - floorA1.z) / dSternMidSpan;
        }

        return floorA1.z + (sternZ_targetLower - sternZ_floorLower) * scaleZ;
    }

    public float EvaluateCenterlineStemProfileZAtY(float targetY)
    {
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        Vector3 floorP1 = (floorCurves != null) ? floorCurves.fwdP1 : new Vector3(1f, keelY + 0.5f, 10f);
        Vector3 breadthP1 = (breadthCurves != null) ? breadthCurves.fwdP1 : new Vector3(3f, keelY + 2.5f, 12f);
        Vector3 topLineP1 = (topLineCurves != null) ? topLineCurves.fwdP1 : new Vector3(3.5f, keelY + 3.5f, 13f);

        float stemBottomZRatio = (sheerCurves != null) ? sheerCurves.stemBottomControlZ : 0.5f;
        float stemBottomYRatio = (sheerCurves != null) ? sheerCurves.stemBottomControlY : 0.1f;
        float stemTopZRatio = (sheerCurves != null) ? sheerCurves.stemTopControlZ : 1.0f;
        float stemTopYRatio = (sheerCurves != null) ? sheerCurves.stemTopControlY : 0.6f;

        float y0 = keelY;
        float y3 = breadthP1.y;
        float z3 = breadthP1.z;

        if (targetY >= y3)
        {
            float dyUpper = topLineP1.y - y3;
            if (Mathf.Abs(dyUpper) < 0.0001f) return z3;
            float tUpper = Mathf.Clamp01((targetY - y3) / dyUpper);
            return Mathf.Lerp(z3, topLineP1.z, tUpper);
        }

        float y1C = y0 + (y3 - y0) * stemBottomYRatio;
        float y2C = y0 + (y3 - y0) * stemTopYRatio;

        float targetFloorY = Mathf.Clamp(floorP1.y, y0 + 0.001f, y3 - 0.001f);
        float tFloor = SolveBezierTAtY(y0, y1C, y2C, y3, targetFloorY);
        float uFloor = 1f - tFloor;
        float c0Keel = (uFloor * uFloor * uFloor) + 3f * uFloor * uFloor * tFloor * (1f - stemBottomZRatio) + 3f * uFloor * tFloor * tFloor * (1f - stemTopZRatio);
        c0Keel = Mathf.Clamp(c0Keel, 0.0001f, 0.9999f);
        float z0 = (floorP1.z - (1f - c0Keel) * z3) / c0Keel;

        if (targetY <= y0) return z0;

        float tParam = SolveBezierTAtY(y0, y1C, y2C, y3, targetY);

        float dzStem = z3 - z0;
        float z1C = z0 + dzStem * stemBottomZRatio;
        float z2C = z0 + dzStem * stemTopZRatio;

        float uP = 1f - tParam;
        return (uP * uP * uP * z0) + (3f * uP * uP * tParam * z1C) + (3f * uP * tParam * tParam * z2C) + (tParam * tParam * tParam * z3);
    }

    public float EvaluateCenterlineFwdP2ProfileZAtY(float targetY)
    {
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;
        float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
        float bowZ = (sheerCurves != null) ? sheerCurves.stemP3.z : 15f;
        float fwdSpan = bowZ - midZ;

        Vector3 floorP2 = (floorCurves != null) ? floorCurves.fwdP1 : new Vector3(1f, keelY + 0.5f, midZ + fwdSpan * 0.5f);
        Vector3 breadthP2 = (breadthCurves != null) ? breadthCurves.fwdP2 : new Vector3(3f, keelY + 2.5f, midZ + fwdSpan * 0.35f);
        Vector3 topLineP2 = (topLineCurves != null) ? topLineCurves.fwdP2 : new Vector3(3.5f, keelY + 3.5f, midZ + fwdSpan * 0.45f);

        float stemBottomZRatio = (sheerCurves != null) ? sheerCurves.stemBottomControlZ : 0.5f;
        float stemBottomYRatio = (sheerCurves != null) ? sheerCurves.stemBottomControlY : 0.1f;
        float stemTopZRatio = (sheerCurves != null) ? sheerCurves.stemTopControlZ : 1.0f;
        float stemTopYRatio = (sheerCurves != null) ? sheerCurves.stemTopControlY : 0.6f;

        float y0 = keelY;
        float y3 = breadthP2.y;
        float z3 = breadthP2.z;

        if (targetY >= y3)
        {
            float dyUpper = topLineP2.y - y3;
            if (Mathf.Abs(dyUpper) < 0.0001f) return z3;
            float tUpper = Mathf.Clamp01((targetY - y3) / dyUpper);
            return Mathf.Lerp(z3, topLineP2.z, tUpper);
        }

        float y1C = y0 + (y3 - y0) * stemBottomYRatio;
        float y2C = y0 + (y3 - y0) * stemTopYRatio;

        float targetFloorY = Mathf.Clamp(floorP2.y, y0 + 0.001f, y3 - 0.001f);
        float tFloor = SolveBezierTAtY(y0, y1C, y2C, y3, targetFloorY);
        float uFloor = 1f - tFloor;
        float c0Keel = (uFloor * uFloor * uFloor) + 3f * uFloor * uFloor * tFloor * (1f - stemBottomZRatio) + 3f * uFloor * tFloor * tFloor * (1f - stemTopZRatio);
        c0Keel = Mathf.Clamp(c0Keel, 0.0001f, 0.9999f);
        float z0 = (floorP2.z - (1f - c0Keel) * z3) / c0Keel;

        if (targetY <= y0) return z0;

        float tParam = SolveBezierTAtY(y0, y1C, y2C, y3, targetY);

        float dzStem = z3 - z0;
        float z1C = z0 + dzStem * stemBottomZRatio;
        float z2C = z0 + dzStem * stemTopZRatio;

        float uP = 1f - tParam;
        return (uP * uP * uP * z0) + (3f * uP * uP * tParam * z1C) + (3f * uP * tParam * tParam * z2C) + (tParam * tParam * tParam * z3);
    }

    private float SolveBezierTAtY(float y0, float y1, float y2, float y3, float targetY)
    {
        float t = 0.5f;
        float minT = 0f;
        float maxT = 1f;

        for (int k = 0; k < 12; k++)
        {
            float u = 1f - t;
            float yVal = (u * u * u * y0) + (3f * u * u * t * y1) + (3f * u * t * t * y2) + (t * t * t * y3);
            if (yVal < targetY) minT = t;
            else maxT = t;
            t = (minT + maxT) * 0.5f;
        }
        return t;
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

        for (int i = 0; i < 24; i++)
        {
            float u = 1f - t;
            float zVal = (u * u * p0.z) + (2f * u * t * p1.z) + (t * t * p2.z);
            float error = zVal - targetZ;

            if (Mathf.Abs(error) < 0.000001f) return t;

            if (p0.z > p2.z)
            {
                if (zVal > targetZ) min = t;
                else max = t;
            }
            else
            {
                if (zVal < targetZ) min = t;
                else max = t;
            }
            t = (min + max) * 0.5f;
        }

        return t;
    }

    public float GetBezierTAtZ(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float targetZ, bool isForward)
    {
        float min = 0f;
        float max = 1f;
        float t = 0.5f;

        for (int i = 0; i < 24; i++)
        {
            float u = 1f - t;
            float zVal = (u * u * u * p0.z) + (3f * u * u * t * p1.z) + (3f * u * t * t * p2.z) + (t * t * t * p3.z);
            float error = zVal - targetZ;

            if (Mathf.Abs(error) < 0.000001f) return t;

            if (p0.z > p3.z)
            {
                if (zVal > targetZ) min = t;
                else max = t;
            }
            else
            {
                if (zVal < targetZ) min = t;
                else max = t;
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

        // --- DASHED FORWARD CONTROL ARM GUIDES ---
        if (showBowMouldP1Guide)
        {
            DrawBowMouldP1GuideDashed(bowMouldGuideColor);
        }

        if (showFwdP2Guide)
        {
            DrawFwdP2GuideDashed(fwdP2GuideColor);
        }

        // --- DASHED AFT CONTROL ARM GUIDES ---
        if (showAftMouldA1Guide)
        {
            DrawAftMouldA1GuideDashed(aftMouldGuideColor);
        }

        if (showSternTransomGuide)
        {
            DrawSternTransomGuideDashed(sternTransomGuideColor);
        }
#endif
    }

#if UNITY_EDITOR
    private void DrawBowMouldP1GuideDashed(Color color, int steps = 30)
    {
        Handles.color = color;
        Vector3 prev = EvaluateBowMouldP1AtU(0.25f);
        for (int i = 1; i <= steps; i++)
        {
            float u = Mathf.Lerp(0.25f, 1.00f, (float)i / steps);
            Vector3 curr = EvaluateBowMouldP1AtU(u);
            Handles.DrawDottedLine(prev, curr, 4f);
            Handles.DrawDottedLine(MirrorX(prev), MirrorX(curr), 4f);
            prev = curr;
        }

        // Draw node spheres and labels at key u levels: Floor (u=0.25), Bilge (u=0.50), Breadth (u=0.75), TopLine (u=1.00)
        float[] sampleUs = new float[] { 0.25f, 0.50f, 0.75f, 1.00f };
        string[] labels = new string[] { "Floor P1 (u=0.25)", "Bilge P1 (u=0.50)", "Breadth P1 (u=0.75)", "TopLine P1 (u=1.00)" };

        for (int k = 0; k < sampleUs.Length; k++)
        {
            Vector3 pt = EvaluateBowMouldP1AtU(sampleUs[k]);
            Gizmos.color = color;
            Gizmos.DrawSphere(pt, 0.07f);
            Gizmos.DrawSphere(MirrorX(pt), 0.07f);

            if (showLabels)
            {
                GUIStyle labelStyle = GetCachedLabelStyle();
                labelStyle.normal.textColor = color;
                Handles.Label(pt + Vector3.up * 0.12f, labels[k], labelStyle);
            }
        }
    }

    private void DrawFwdP2GuideDashed(Color color, int steps = 40)
    {
        Handles.color = color;
        Vector3 prev = EvaluateFwdP2AtU(0.00f);
        for (int i = 1; i <= steps; i++)
        {
            float u = (float)i / steps;
            Vector3 curr = EvaluateFwdP2AtU(u);
            Handles.DrawDottedLine(prev, curr, 4f);
            Handles.DrawDottedLine(MirrorX(prev), MirrorX(curr), 4f);
            prev = curr;
        }

        // Draw node spheres and labels at key u levels: Keel (u=0.00), Floor (u=0.25), Bilge (u=0.50), Breadth (u=0.75), TopLine (u=1.00)
        float[] sampleUs = new float[] { 0.00f, 0.25f, 0.50f, 0.75f, 1.00f };
        string[] labels = new string[] { "Keel P2 (u=0.00)", "Floor P2 (u=0.25)", "Bilge P2 (u=0.50)", "Breadth P2 (u=0.75)", "TopLine P2 (u=1.00)" };

        for (int k = 0; k < sampleUs.Length; k++)
        {
            Vector3 pt = EvaluateFwdP2AtU(sampleUs[k]);
            Gizmos.color = color;
            Gizmos.DrawSphere(pt, 0.07f);
            Gizmos.DrawSphere(MirrorX(pt), 0.07f);

            if (showLabels)
            {
                GUIStyle labelStyle = GetCachedLabelStyle();
                labelStyle.normal.textColor = color;
                Handles.Label(pt + Vector3.up * 0.12f, labels[k], labelStyle);
            }
        }
    }

    private void DrawAftMouldA1GuideDashed(Color color, int steps = 40)
    {
        Handles.color = color;
        Vector3 prev = EvaluateAftMouldA1AtU(0.00f);
        for (int i = 1; i <= steps; i++)
        {
            float u = (float)i / steps;
            Vector3 curr = EvaluateAftMouldA1AtU(u);
            Handles.DrawDottedLine(prev, curr, 4f);
            Handles.DrawDottedLine(MirrorX(prev), MirrorX(curr), 4f);
            prev = curr;
        }

        float[] sampleUs = new float[] { 0.00f, 0.25f, 0.50f, 0.75f, 1.00f };
        string[] labels = new string[] { "Keel A1 (u=0.00)", "Floor A1 (u=0.25)", "Bilge A1 (u=0.50)", "Breadth A1 (u=0.75)", "TopLine A1 (u=1.00)" };

        for (int k = 0; k < sampleUs.Length; k++)
        {
            Vector3 pt = EvaluateAftMouldA1AtU(sampleUs[k]);
            Gizmos.color = color;
            Gizmos.DrawSphere(pt, 0.07f);
            Gizmos.DrawSphere(MirrorX(pt), 0.07f);

            if (showLabels)
            {
                GUIStyle labelStyle = GetCachedLabelStyle();
                labelStyle.normal.textColor = color;
                Handles.Label(pt + Vector3.up * 0.12f, labels[k], labelStyle);
            }
        }
    }

    private void DrawSternTransomGuideDashed(Color color, int steps = 40)
    {
        Handles.color = color;
        Vector3 prev = EvaluateAftTransomA2AtU(0.00f);
        for (int i = 1; i <= steps; i++)
        {
            float u = (float)i / steps;
            Vector3 curr = EvaluateAftTransomA2AtU(u);
            Handles.DrawDottedLine(prev, curr, 4f);
            Handles.DrawDottedLine(MirrorX(prev), MirrorX(curr), 4f);
            prev = curr;
        }

        float[] sampleUs = new float[] { 0.00f, 0.25f, 0.50f, 0.75f, 1.00f };
        string[] labels = new string[] { "Stern Keel A2 (u=0.00)", "Floor A2 (u=0.25)", "Transom Bilge A2 (u=0.50)", "Fashion Breadth A2 (u=0.75)", "Fashion Top A2 (u=1.00)" };

        for (int k = 0; k < sampleUs.Length; k++)
        {
            Vector3 pt = EvaluateAftTransomA2AtU(sampleUs[k]);
            Gizmos.color = color;
            Gizmos.DrawSphere(pt, 0.07f);
            Gizmos.DrawSphere(MirrorX(pt), 0.07f);

            if (showLabels)
            {
                GUIStyle labelStyle = GetCachedLabelStyle();
                labelStyle.normal.textColor = color;
                Handles.Label(pt + Vector3.up * 0.12f, labels[k], labelStyle);
            }
        }
    }
#endif

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

            float floorMidY = (curves.floorCurves != null) ? curves.floorCurves.fwdP3.y : 0.5f;
            float breadthMidY = (curves.breadthCurves != null) ? curves.breadthCurves.fwdP3.y : floorMidY + 3.0f;
            float floorMidX = (curves.floorCurves != null) ? curves.floorCurves.fwdP3.x : 2.0f;
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
