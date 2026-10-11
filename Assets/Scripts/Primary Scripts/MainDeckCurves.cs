using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// MainDeckCurves defines the 3D Main Deck surface and boundary curves for the ship hull.
/// 
/// Characteristics & Boundaries:
/// 1. Longitudinal Deck Curve: Lives strictly inside the hull and follows the inner lines of the hull model.
/// 2. Vertical Corridor: Bounded between BreadthCurves (lower ceiling) and TopLineCurves (upper sheer ceiling).
/// 3. Positive Sheer Rule: Bow Tip and Stern Point controls can sit above their Midship Apex, but never below it.
/// 4. Sheer & Camber Curvature:
///    - Centerline Sheer Curve: Controls longitudinal Z/Y curvature running Bow -> Midship Apex -> Stern.
///    - Midship Camber (Round of Beam): Controlled by the height difference between the Midship Centerline Apex 
///      and the Midship Deck Edge (sits on the inner hull boundary).
/// 5. No Manual Bezier Tangent Arms: The curve is purely evaluated from the primary control points and the 
///    inner lines of the hull model.
/// 6. Deck Edge Line (Waterway): At every station Z, the deck edge sits 100% on the inner hull boundary.
/// </summary>
[ExecuteInEditMode]
public class MainDeckCurves : MonoBehaviour
{
    [Header("Primary Curve References")]
    [Tooltip("Reference to SheerCurves for 2D profile curves (Stem, Keel, Stern, Counter, Transom Rake).")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to TopLineCurves for upper sheer ceiling constraint and inner top line.")]
    public TopLineCurves topLineCurves;

    [Tooltip("Reference to BreadthCurves for lower ceiling constraint and inner breadth line.")]
    public BreadthCurves breadthCurves;

    [Tooltip("Reference to FashionCurves for 3D transom stern inner boundary curve.")]
    public FashionCurves fashionCurves;

    [Tooltip("Reference to SectionModelCurves for inner frame section evaluation (optional).")]
    public SectionModelCurves sectionModelCurves;

    [Tooltip("Reference to BodyCurves for midship section parameters.")]
    public BodyCurves bodyCurves;

    [Tooltip("Reference to BackboneCurves for keel width half-offset.")]
    public BackboneCurves backboneCurves;

    [Header("Interactive Scene Handles")]
    [Tooltip("Enable interactive 3D position handles in Scene view to move Bow Tip, Midship Apex, and Stern Points.")]
    public bool enableInteractiveHandles = true;

    [Header("Primary 3D Control Points")]
    [Tooltip("Bow Tip Point on the centerline (X = 0) at the inner stem post.")]
    public Vector3 bowTip;

    [Tooltip("Midship Centerline Apex Point (X = 0, Z = Midship). Lowest point of the deck sheer curve.")]
    public Vector3 midshipCenter;

    [Tooltip("Starboard Midship Deck Edge Point. Sits on the inner hull line. Height sets the Midship Camber.")]
    public Vector3 stbdMidshipEdge;

    [Tooltip("Stern Centerline Point on the centerline (X = 0) at the stern transom profile.")]
    public Vector3 sternCenter;

    [Tooltip("Starboard Transom Deck Corner Point. Sits on the Inner Fashion Timber curve.")]
    public Vector3 stbdTransomCorner;

    [Header("Deck Camber Settings")]
    [Tooltip("Camber (Round of Beam) height drop at midships: midshipCenter.y - stbdMidshipEdge.y.")]
    [Range(0f, 1.5f)] public float midshipCamber = 0.25f;

    [Tooltip("Scale camber drop proportionally to local deck beam at station Z (standard wooden shipbuilding rule).")]
    public bool proportionalCamberToBeam = true;

    [Header("Longitudinal Sheer Settings")]
    [Tooltip("Z position of Midship Apex as a normalized percentage (0 to 1) between Stern and Bow.")]
    [Range(0.1f, 0.9f)] public float midshipApexZPercentage = 0.50f;

    [Tooltip("Sheer power exponent (2.0 = standard parabolic sheer, >2.0 = flatter midships with steeper rise at ends).")]
    [Range(1.5f, 3.5f)] public float sheerCurvePower = 2.0f;

    [Header("Deck Thickness / Bottom Deck Settings")]
    [Tooltip("Enable rendering and evaluation of the Bottom Main Deck curves.")]
    public bool showBottomDeck = true;

    [Tooltip("Physical vertical thickness / spacing between the top and bottom deck surfaces.")]
    [Range(0.01f, 1.0f)] public float deckThickness = 0.20f;

    [Tooltip("Draw vertical edge cap lines connecting top deck edge to bottom deck edge across the hull.")]
    public bool showThicknessEdgeCaps = true;

    [Header("Visualization Settings")]
    public bool showCenterlineSheer = true;
    public bool showDeckEdges = true;
    public bool showDeckBeams = true;
    public bool showNodeSpheres = true;
    public bool showLabels = true;

    [Tooltip("Number of transverse deck camber beam arcs to render across the deck.")]
    [Range(3, 40)] public int deckBeamCount = 15;

    [Range(0.04f, 0.25f)] public float nodeSphereRadius = 0.12f;

    [Header("Gizmo Colors - Top Surface")]
    public Color centerlineColor = new Color(1.0f, 0.75f, 0.15f, 0.95f); // Vibrant Gold / Amber
    public Color deckEdgeColor = new Color(0.15f, 0.90f, 0.85f, 0.95f);   // Vibrant Cyan / Seafoam
    public Color deckBeamColor = new Color(0.95f, 0.85f, 0.65f, 0.65f);   // Translucent Wheat / Timber
    public Color bowNodeColor = Color.yellow;
    public Color midNodeColor = new Color(0.2f, 0.95f, 0.4f, 1f);          // Emerald Green
    public Color sternNodeColor = Color.magenta;

    [Header("Gizmo Colors - Bottom Surface")]
    public Color bottomCenterlineColor = new Color(0.85f, 0.60f, 0.10f, 0.85f);
    public Color bottomDeckEdgeColor = new Color(0.10f, 0.75f, 0.70f, 0.85f);
    public Color bottomDeckBeamColor = new Color(0.80f, 0.70f, 0.50f, 0.50f);
    public Color thicknessCapColor = new Color(0.30f, 0.85f, 0.95f, 0.70f);

    [Header("Initialization State")]
    public bool isInitialized = false;

    // --- CACHED 3D GEOMETRY BUFFERS (Zero GC Runtime Performance) ---
    private const int RESOLUTION = 60;
    private Vector3[] cachedCenterlinePts;
    private Vector3[] cachedStbdEdgePts;
    private Vector3[] cachedPortEdgePts;
    private List<Vector3[]> cachedDeckBeams = new List<Vector3[]>();

    private Vector3[] cachedBottomCenterlinePts;
    private Vector3[] cachedStbdBottomEdgePts;
    private Vector3[] cachedPortBottomEdgePts;
    private List<Vector3[]> cachedBottomDeckBeams = new List<Vector3[]>();

    [HideInInspector] public Vector3 portMidshipEdge;
    [HideInInspector] public Vector3 portTransomCorner;

    // --- BOTTOM DECK LANDMARK NODES ---
    [HideInInspector] public Vector3 bowBottomTip;
    [HideInInspector] public Vector3 midshipBottomCenter;
    [HideInInspector] public Vector3 stbdMidshipBottomEdge;
    [HideInInspector] public Vector3 portMidshipBottomEdge;
    [HideInInspector] public Vector3 sternBottomCenter;
    [HideInInspector] public Vector3 stbdTransomBottomCorner;
    [HideInInspector] public Vector3 portTransomBottomCorner;

    [HideInInspector] public int changeVersion = 0;

    private bool isUpdatingCurves = false;

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Reset()
    {
        InitializeMainDeckCurves();
    }

    public void EnsureInitialized()
    {
        if (!isInitialized || 
            cachedCenterlinePts == null || cachedCenterlinePts.Length == 0 ||
            cachedBottomCenterlinePts == null || cachedBottomCenterlinePts.Length == 0 ||
            cachedDeckBeams == null || cachedBottomDeckBeams == null)
        {
            InitializeMainDeckCurves();
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

        if (fashionCurves == null) fashionCurves = GetComponentInParent<FashionCurves>();
        if (fashionCurves == null) fashionCurves = FindFirstObjectByType<FashionCurves>();

        if (sectionModelCurves == null) sectionModelCurves = GetComponentInParent<SectionModelCurves>();
        if (sectionModelCurves == null) sectionModelCurves = FindFirstObjectByType<SectionModelCurves>();

        if (bodyCurves == null) bodyCurves = GetComponentInParent<BodyCurves>();
        if (bodyCurves == null) bodyCurves = FindFirstObjectByType<BodyCurves>();

        if (backboneCurves == null) backboneCurves = GetComponentInParent<BackboneCurves>();
        if (backboneCurves == null) backboneCurves = FindFirstObjectByType<BackboneCurves>();
    }

    [ContextMenu("Reset Main Deck Curves")]
    public void InitializeMainDeckCurves()
    {
        FindReferences();

        if (topLineCurves != null) topLineCurves.UpdateCachedCurves();
        if (breadthCurves != null) breadthCurves.UpdateCachedCurves();
        if (fashionCurves != null) fashionCurves.UpdateCachedCurves();

        float bowZ = (topLineCurves != null) ? topLineCurves.cachedFwdP0.z : 15f;
        float sternZ = (fashionCurves != null) ? fashionCurves.innerStbdP4.z : ((sheerCurves != null) ? sheerCurves.sternP3.z : -15f);
        float midZ = (breadthCurves != null) ? breadthCurves.cachedFwdP3.z : Mathf.Lerp(sternZ, bowZ, midshipApexZPercentage);

        // Compute midpoint heights between Breadth and TopLine as natural default
        float brMidY = GetInnerBreadthYAtZ(midZ);
        float tlMidY = GetInnerTopLineYAtZ(midZ);
        float midY = Mathf.Lerp(brMidY, tlMidY, 0.40f);

        float brBowY = GetInnerBreadthYAtZ(bowZ);
        float tlBowY = GetInnerTopLineYAtZ(bowZ);
        float bowY = Mathf.Lerp(brBowY, tlBowY, 0.60f);

        float brSternY = GetInnerBreadthYAtZ(sternZ);
        float tlSternY = GetInnerTopLineYAtZ(sternZ);
        float sternY = Mathf.Lerp(brSternY, tlSternY, 0.55f);

        midshipCenter = new Vector3(0f, midY, midZ);
        bowTip = new Vector3(0f, Mathf.Max(bowY, midY), bowZ);
        sternCenter = new Vector3(0f, Mathf.Max(sternY, midY), sternZ);

        midshipCamber = Mathf.Clamp(midshipCamber, 0.05f, 0.8f);

        float edgeY = midshipCenter.y - midshipCamber;
        float edgeX = GetInnerHullHalfBreadthAt(edgeY, midZ);
        stbdMidshipEdge = new Vector3(edgeX, edgeY, midZ);

        UpdateCachedCurves();
        isInitialized = true;
    }

    private void OnValidate()
    {
        UpdateCachedCurves();
    }

    /// <summary>
    /// Re-evaluates all Main Deck 3D curves, enforcing all boundary constraints, zero GC buffers, and mirroring.
    /// </summary>
    public virtual void UpdateCachedCurves()
    {
        if (isUpdatingCurves) return;
        isUpdatingCurves = true;
        try
        {
            FindReferences();

            if (topLineCurves != null) topLineCurves.UpdateCachedCurves();
            if (breadthCurves != null) breadthCurves.UpdateCachedCurves();
            if (fashionCurves != null) fashionCurves.UpdateCachedCurves();

            float bowZ = (topLineCurves != null) ? topLineCurves.cachedFwdP0.z : 15f;
            float sternZ = (fashionCurves != null) ? fashionCurves.innerStbdP4.z : ((sheerCurves != null) ? sheerCurves.sternP3.z : -15f);

            // 1. Enforce Midship Apex Z position & vertical corridor constraints
            float midZ = (breadthCurves != null) ? breadthCurves.cachedFwdP3.z : Mathf.Lerp(-10f, bowZ, midshipApexZPercentage);
            midshipCenter.x = 0f;
            midshipCenter.z = midZ;

            float brMidY = GetInnerBreadthYAtZ(midZ);
            float tlMidY = GetInnerTopLineYAtZ(midZ);
            midshipCenter.y = Mathf.Clamp(midshipCenter.y, brMidY + 0.01f, tlMidY - 0.01f);

            // 2. Enforce Positive Sheer Rule & Corridor for Bow Tip (Bow Y >= Midship Y, and within Breadth/TopLine)
            bowTip.x = 0f;
            bowTip.z = bowZ;
            float brBowY = GetInnerBreadthYAtZ(bowZ);
            float tlBowY = GetInnerTopLineYAtZ(bowZ);
            float minBowY = Mathf.Max(brBowY, midshipCenter.y); // CANNOT BE BELOW MIDSHIP APEX
            bowTip.y = Mathf.Clamp(bowTip.y, minBowY, tlBowY);

            // 3. Enforce Positive Sheer Rule & Corridor for Stern Center Point
            // The Stern Center Z is derived directly from the Stern Profile at sternCenter.y!
            float minSternY = midshipCenter.y; // CANNOT BE BELOW MIDSHIP APEX
            float maxSternY = (topLineCurves != null) ? topLineCurves.cachedAftP2.y : ((sheerCurves != null) ? sheerCurves.transomP1.y : midshipCenter.y + 2f);
            sternCenter.x = 0f;
            sternCenter.y = Mathf.Clamp(sternCenter.y, minSternY, maxSternY);

            // Lock sternCenter.z 100% to SheerCurves stern profile at height sternCenter.y
            float sternProfileZ = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(sternCenter.y) : -12f;
            sternCenter.z = sternProfileZ;

            // 4. Update Midship Camber & Side Apex Point
            midshipCamber = Mathf.Clamp(midshipCamber, 0f, 1.5f);
            float desiredEdgeY = midshipCenter.y - midshipCamber;
            desiredEdgeY = Mathf.Max(desiredEdgeY, brMidY); // Cannot dip below Breadth
            midshipCamber = midshipCenter.y - desiredEdgeY;

            float edgeX = GetInnerHullHalfBreadthAt(desiredEdgeY, midZ);
            stbdMidshipEdge = new Vector3(edgeX, desiredEdgeY, midZ);
            portMidshipEdge = new Vector3(-edgeX, desiredEdgeY, midZ);

            // 5. Update Transom Corners resting on Inner Fashion Timber curve
            float sternCamber = proportionalCamberToBeam ? EvaluateDeckCamberAtZ(sternCenter.z) : midshipCamber;
            float sternEdgeY = Mathf.Max(sternCenter.y - sternCamber, (breadthCurves != null) ? breadthCurves.cachedAftP2.y : sternCenter.y - 1f);

            if (fashionCurves != null)
            {
                Vector3 inFashionPt = fashionCurves.GetInnerFashionPointAtY(sternEdgeY, 1f);
                stbdTransomCorner = new Vector3(Mathf.Abs(inFashionPt.x), sternEdgeY, inFashionPt.z);
            }
            else
            {
                float sternX = GetInnerHullHalfBreadthAt(sternEdgeY, sternCenter.z);
                stbdTransomCorner = new Vector3(sternX, sternEdgeY, sternCenter.z);
            }
            portTransomCorner = new Vector3(-stbdTransomCorner.x, stbdTransomCorner.y, stbdTransomCorner.z);

            // 6. Update Bottom Deck Landmark Nodes (Offset downward by deckThickness)
            deckThickness = Mathf.Clamp(deckThickness, 0.01f, 1.0f);
            midshipBottomCenter = new Vector3(0f, midshipCenter.y - deckThickness, midshipCenter.z);

            float stbdBottomEdgeY = stbdMidshipEdge.y - deckThickness;
            float stbdBottomEdgeX = GetInnerHullHalfBreadthAt(stbdBottomEdgeY, midZ);
            stbdMidshipBottomEdge = new Vector3(stbdBottomEdgeX, stbdBottomEdgeY, midZ);
            portMidshipBottomEdge = new Vector3(-stbdBottomEdgeX, stbdBottomEdgeY, midZ);

            float bowBottomY = bowTip.y - deckThickness;
            float bowBottomZ = (sheerCurves != null) ? sheerCurves.GetStemProfileZAtY(bowBottomY) : bowTip.z;
            bowBottomTip = new Vector3(0f, bowBottomY, bowBottomZ);

            float sternBottomCenterY = sternCenter.y - deckThickness;
            float sternBottomCenterZ = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(sternBottomCenterY) : sternCenter.z;
            sternBottomCenter = new Vector3(0f, sternBottomCenterY, sternBottomCenterZ);

            float sternBottomEdgeY = stbdTransomCorner.y - deckThickness;
            if (fashionCurves != null)
            {
                Vector3 inFashionBottomPt = fashionCurves.GetInnerFashionPointAtY(sternBottomEdgeY, 1f);
                stbdTransomBottomCorner = new Vector3(Mathf.Abs(inFashionBottomPt.x), sternBottomEdgeY, inFashionBottomPt.z);
            }
            else
            {
                float sternBottomX = GetInnerHullHalfBreadthAt(sternBottomEdgeY, sternBottomCenterZ);
                stbdTransomBottomCorner = new Vector3(sternBottomX, sternBottomEdgeY, sternBottomCenterZ);
            }
            portTransomBottomCorner = new Vector3(-stbdTransomBottomCorner.x, sternBottomEdgeY, stbdTransomBottomCorner.z);

            // 7. Precompute 3D Curve Polylines (Top Surface & Bottom Surface)
            BuildCachedPolylines();

            // 8. Precompute Transverse Camber Beams (Top & Bottom Arcs)
            BuildCachedDeckBeams();

            changeVersion++;
        }
        finally
        {
            isUpdatingCurves = false;
        }
    }

    private void BuildCachedPolylines()
    {
        if (cachedCenterlinePts == null || cachedCenterlinePts.Length != RESOLUTION + 1)
            cachedCenterlinePts = new Vector3[RESOLUTION + 1];
        if (cachedStbdEdgePts == null || cachedStbdEdgePts.Length != RESOLUTION + 1)
            cachedStbdEdgePts = new Vector3[RESOLUTION + 1];
        if (cachedPortEdgePts == null || cachedPortEdgePts.Length != RESOLUTION + 1)
            cachedPortEdgePts = new Vector3[RESOLUTION + 1];

        if (cachedBottomCenterlinePts == null || cachedBottomCenterlinePts.Length != RESOLUTION + 1)
            cachedBottomCenterlinePts = new Vector3[RESOLUTION + 1];
        if (cachedStbdBottomEdgePts == null || cachedStbdBottomEdgePts.Length != RESOLUTION + 1)
            cachedStbdBottomEdgePts = new Vector3[RESOLUTION + 1];
        if (cachedPortBottomEdgePts == null || cachedPortBottomEdgePts.Length != RESOLUTION + 1)
            cachedPortBottomEdgePts = new Vector3[RESOLUTION + 1];

        float bowZ = bowTip.z;

        // 1. Build Top Centerline Sheer Polyline from sternCenter to bowTip
        float centerSternZ = sternCenter.z;
        for (int i = 0; i <= RESOLUTION; i++)
        {
            float frac = (float)i / RESOLUTION; // 0 = Stern, 1 = Bow
            if (i == 0)
            {
                cachedCenterlinePts[0] = sternCenter;
            }
            else if (i == RESOLUTION)
            {
                cachedCenterlinePts[RESOLUTION] = bowTip;
            }
            else
            {
                float z = Mathf.Lerp(centerSternZ, bowZ, frac);
                float centerY = EvaluateCenterlineSheerYAtZ(z);
                cachedCenterlinePts[i] = new Vector3(0f, centerY, z);
            }
        }

        // 2. Build Top Deck Edge Polylines from stbdTransomCorner to Bow Edge
        float edgeSternZ = stbdTransomCorner.z;
        for (int i = 0; i <= RESOLUTION; i++)
        {
            float frac = (float)i / RESOLUTION; // 0 = Stern, 1 = Bow
            if (i == 0)
            {
                cachedStbdEdgePts[0] = stbdTransomCorner;
                cachedPortEdgePts[0] = portTransomCorner;
            }
            else if (i == RESOLUTION)
            {
                float bowEdgeY = bowTip.y - EvaluateDeckCamberAtZ(bowZ);
                float bowEdgeX = GetInnerHullHalfBreadthAt(bowEdgeY, bowZ);
                cachedStbdEdgePts[RESOLUTION] = new Vector3(bowEdgeX, bowEdgeY, bowZ);
                cachedPortEdgePts[RESOLUTION] = new Vector3(-bowEdgeX, bowEdgeY, bowZ);
            }
            else
            {
                float z = Mathf.Lerp(edgeSternZ, bowZ, frac);
                float centerY = EvaluateCenterlineSheerYAtZ(z);
                float camber = EvaluateDeckCamberAtZ(z);
                float edgeY = centerY - camber;

                float edgeX = GetInnerHullHalfBreadthAt(edgeY, z);
                cachedStbdEdgePts[i] = new Vector3(edgeX, edgeY, z);
                cachedPortEdgePts[i] = new Vector3(-edgeX, edgeY, z);
            }
        }

        // 3. Build Bottom Centerline Sheer Polyline from sternBottomCenter to bowBottomTip
        float bottomCenterSternZ = sternBottomCenter.z;
        float bottomCenterBowZ = bowBottomTip.z;
        for (int i = 0; i <= RESOLUTION; i++)
        {
            float frac = (float)i / RESOLUTION;
            if (i == 0)
            {
                cachedBottomCenterlinePts[0] = sternBottomCenter;
            }
            else if (i == RESOLUTION)
            {
                cachedBottomCenterlinePts[RESOLUTION] = bowBottomTip;
            }
            else
            {
                float z = Mathf.Lerp(bottomCenterSternZ, bottomCenterBowZ, frac);
                float centerY = EvaluateBottomCenterlineSheerYAtZ(z);
                cachedBottomCenterlinePts[i] = new Vector3(0f, centerY, z);
            }
        }

        // 4. Build Bottom Deck Edge Polylines from stbdTransomBottomCorner to Bow Bottom Edge
        float bottomEdgeSternZ = stbdTransomBottomCorner.z;
        float bottomEdgeBowZ = bowBottomTip.z;
        for (int i = 0; i <= RESOLUTION; i++)
        {
            float frac = (float)i / RESOLUTION;
            if (i == 0)
            {
                cachedStbdBottomEdgePts[0] = stbdTransomBottomCorner;
                cachedPortBottomEdgePts[0] = portTransomBottomCorner;
            }
            else if (i == RESOLUTION)
            {
                float bowEdgeY = bowBottomTip.y - EvaluateDeckCamberAtZ(bottomEdgeBowZ);
                float bowEdgeX = GetInnerHullHalfBreadthAt(bowEdgeY, bottomEdgeBowZ);
                cachedStbdBottomEdgePts[RESOLUTION] = new Vector3(bowEdgeX, bowEdgeY, bottomEdgeBowZ);
                cachedPortBottomEdgePts[RESOLUTION] = new Vector3(-bowEdgeX, bowEdgeY, bottomEdgeBowZ);
            }
            else
            {
                float z = Mathf.Lerp(bottomEdgeSternZ, bottomEdgeBowZ, frac);
                float centerY = EvaluateBottomCenterlineSheerYAtZ(z);
                float camber = EvaluateDeckCamberAtZ(z);
                float edgeY = centerY - camber;

                float edgeX = GetInnerHullHalfBreadthAt(edgeY, z);
                cachedStbdBottomEdgePts[i] = new Vector3(edgeX, edgeY, z);
                cachedPortBottomEdgePts[i] = new Vector3(-edgeX, edgeY, z);
            }
        }
    }

    private void BuildCachedDeckBeams()
    {
        if (cachedDeckBeams == null) cachedDeckBeams = new List<Vector3[]>();
        else cachedDeckBeams.Clear();

        if (cachedBottomDeckBeams == null) cachedBottomDeckBeams = new List<Vector3[]>();
        else cachedBottomDeckBeams.Clear();

        int beams = Mathf.Clamp(deckBeamCount, 2, 50);
        float bowZ = bowTip.z;
        float sternMostZ = Mathf.Min(sternCenter.z, stbdTransomCorner.z);

        const int beamSteps = 24;

        for (int b = 0; b < beams; b++)
        {
            float t = (float)b / (beams - 1);
            float z = Mathf.Lerp(sternMostZ, bowZ, t);

            float centerY = EvaluateCenterlineSheerYAtZ(z);
            float camber = EvaluateDeckCamberAtZ(z);
            float edgeY = centerY - camber;

            // Check if station Z is within the transom cut region
            bool isTransomRegion = (fashionCurves != null && z <= fashionCurves.stbdP1.z + 0.001f);
            float minSectionY = -999f;
            float cutX = 0f;
            if (isTransomRegion)
            {
                Vector3 inFashionPt = fashionCurves.GetInnerFashionPointAtZ(z, 1f);
                minSectionY = inFashionPt.y;
                cutX = Mathf.Abs(inFashionPt.x);

                // If the entire centerline is below the transom cut, this station is behind the ship
                if (centerY < minSectionY - 0.01f)
                {
                    continue;
                }
            }

            // Top Beam Arc
            float halfWidth = GetInnerHullHalfBreadthAt(edgeY, z);
            if (halfWidth >= 0.005f)
            {
                Vector3[] arc = new Vector3[beamSteps + 1];
                for (int s = 0; s <= beamSteps; s++)
                {
                    float normX = Mathf.Lerp(-1f, 1f, (float)s / beamSteps); // -1 (Port) to +1 (Starboard)
                    float x = normX * halfWidth;

                    // Standard Naval Architecture Parabolic Round of Beam
                    float y = centerY - camber * (normX * normX);

                    // If in transom region, clamp point so it never dips below the Fashion Curve cut
                    if (isTransomRegion && y < minSectionY)
                    {
                        y = minSectionY;
                    }

                    arc[s] = new Vector3(x, y, z);
                }
                cachedDeckBeams.Add(arc);
            }

            // Bottom Beam Arc (Underside of Deck Beams)
            float bottomCenterY = EvaluateBottomCenterlineSheerYAtZ(z);
            float bottomEdgeY = bottomCenterY - camber;
            float bottomHalfWidth = GetInnerHullHalfBreadthAt(bottomEdgeY, z);

            if (bottomHalfWidth >= 0.005f)
            {
                Vector3[] bottomArc = new Vector3[beamSteps + 1];
                for (int s = 0; s <= beamSteps; s++)
                {
                    float normX = Mathf.Lerp(-1f, 1f, (float)s / beamSteps);
                    float x = normX * bottomHalfWidth;
                    float y = bottomCenterY - camber * (normX * normX);

                    if (isTransomRegion && y < minSectionY)
                    {
                        y = minSectionY;
                    }

                    bottomArc[s] = new Vector3(x, y, z);
                }
                cachedBottomDeckBeams.Add(bottomArc);
            }
        }
    }

    // --- MATHEMATICAL EVALUATION METHODS ---

    /// <summary>
    /// Evaluates the Centerline Sheer height Y on the deck at any longitudinal station Z.
    /// Perfectly smooth parabolic sheer curve anchored with horizontal tangent (dY/dZ = 0) at Midship Apex.
    /// Guaranteed to never dip below midshipCenter.y.
    /// </summary>
    public float EvaluateCenterlineSheerYAtZ(float z)
    {
        float midZ = midshipCenter.z;
        float midY = midshipCenter.y;

        if (z >= midZ)
        {
            float bowSpan = bowTip.z - midZ;
            if (bowSpan < 0.001f) return midY;
            float u = Mathf.Clamp01((z - midZ) / bowSpan);
            float rise = Mathf.Pow(u, sheerCurvePower) * (bowTip.y - midY);
            return midY + rise;
        }
        else
        {
            float sternSpan = midZ - sternCenter.z;
            if (sternSpan < 0.001f) return midY;
            float u = Mathf.Clamp01((midZ - z) / sternSpan);
            float rise = Mathf.Pow(u, sheerCurvePower) * (sternCenter.y - midY);
            return midY + rise;
        }
    }

    /// <summary>
    /// Evaluates the Bottom Centerline Sheer height Y at any longitudinal station Z.
    /// Represents the underside centerline offset down by deckThickness, spanning cleanly from sternBottomCenter to bowBottomTip.
    /// </summary>
    public float EvaluateBottomCenterlineSheerYAtZ(float z)
    {
        float midZ = midshipBottomCenter.z;
        float midY = midshipBottomCenter.y;

        if (z >= midZ)
        {
            float bowSpan = bowBottomTip.z - midZ;
            if (bowSpan < 0.001f) return midY;
            float u = Mathf.Clamp01((z - midZ) / bowSpan);
            float rise = Mathf.Pow(u, sheerCurvePower) * (bowBottomTip.y - midY);
            return midY + rise;
        }
        else
        {
            float sternSpan = midZ - sternBottomCenter.z;
            if (sternSpan < 0.001f) return midY;
            float u = Mathf.Clamp01((midZ - z) / sternSpan);
            float rise = Mathf.Pow(u, sheerCurvePower) * (sternBottomCenter.y - midY);
            return midY + rise;
        }
    }

    /// <summary>
    /// Evaluates the Deck Camber (Round of Beam drop) at any longitudinal station Z.
    /// Scales proportionally to local half-breadth if proportionalCamberToBeam is enabled.
    /// </summary>
    public float EvaluateDeckCamberAtZ(float z)
    {
        if (!proportionalCamberToBeam) return midshipCamber;

        float midEdgeX = Mathf.Max(0.001f, stbdMidshipEdge.x);
        float centerY = EvaluateCenterlineSheerYAtZ(z);
        float approxEdgeX = GetInnerHullHalfBreadthAt(centerY - midshipCamber, z);

        float ratio = Mathf.Clamp01(approxEdgeX / midEdgeX);
        return midshipCamber * ratio;
    }

    /// <summary>
    /// Evaluates the 3D surface point on the Main Deck at transverse coordinate X and longitudinal station Z.
    /// </summary>
    public Vector3 GetDeckSurfacePoint(float x, float z)
    {
        EnsureInitialized();

        float centerY = EvaluateCenterlineSheerYAtZ(z);
        float camber = EvaluateDeckCamberAtZ(z);
        float edgeY = centerY - camber;
        float halfWidth = GetInnerHullHalfBreadthAt(edgeY, z);

        if (halfWidth < 0.0001f) return new Vector3(0f, centerY, z);

        float clampedX = Mathf.Clamp(x, -halfWidth, halfWidth);
        float normX = clampedX / halfWidth;
        float y = centerY - camber * (normX * normX);

        return new Vector3(clampedX, y, z);
    }

    /// <summary>
    /// Evaluates the 3D Deck Edge point at station Z for specified side (+1 Starboard, -1 Port).
    /// </summary>
    public Vector3 GetDeckEdgePointAtZ(float z, bool starboard = true)
    {
        EnsureInitialized();

        float centerY = EvaluateCenterlineSheerYAtZ(z);
        float camber = EvaluateDeckCamberAtZ(z);
        float edgeY = centerY - camber;
        float edgeX = GetInnerHullHalfBreadthAt(edgeY, z);

        return new Vector3(starboard ? edgeX : -edgeX, edgeY, z);
    }

    /// <summary>
    /// Evaluates the exact inner hull half-breadth at height Y and station Z.
    /// In the transom region, conforms 100% to the transverse section cut by the Fashion Curve at station Z.
    /// Ahead of the transom, conforms strictly between Inner Breadth and Inner TopLine.
    /// </summary>
    public float GetInnerHullHalfBreadthAt(float y, float z)
    {
        // 1. Transom Region: Station Z is cut from below by the Inner Fashion Curve
        bool isTransomRegion = (fashionCurves != null && z <= fashionCurves.stbdP1.z + 0.001f);
        if (isTransomRegion)
        {
            // Bottom base of the inner section cut by the Fashion Curve at station Z
            Vector3 cutPt = fashionCurves.GetInnerFashionPointAtZ(z, 1f);
            float cutY = cutPt.y;
            float cutX = Mathf.Abs(cutPt.x);

            // Inner TopLine at station Z
            float inTlX = (topLineCurves != null) ? Mathf.Abs(topLineCurves.GetInnerTopLinePointAtZ(z, true).x) : 2.2f;
            float inTlY = GetInnerTopLineYAtZ(z);

            // Inner Breadth at station Z
            float inBrX = (breadthCurves != null) ? Mathf.Abs(breadthCurves.GetInnerBreadthPointAtZ(z, true).x) : 2.5f;
            float inBrY = GetInnerBreadthYAtZ(z);

            // If height Y is at or below the fashion curve cut at this station:
            // The section ends at the fashion timber cut
            if (y <= cutY + 0.005f)
            {
                return cutX;
            }

            // If height Y is at or above TopLine:
            if (y >= inTlY - 0.005f)
            {
                return inTlX;
            }

            // If Inner Breadth is active and ABOVE the fashion cut:
            if (inBrY > cutY + 0.02f)
            {
                if (y <= inBrY)
                {
                    float u = Mathf.Clamp01((y - cutY) / Mathf.Max(0.001f, inBrY - cutY));
                    return Mathf.Lerp(cutX, inBrX, u);
                }
                else
                {
                    float u = Mathf.Clamp01((y - inBrY) / Mathf.Max(0.001f, inTlY - inBrY));
                    return Mathf.Lerp(inBrX, inTlX, u);
                }
            }
            else
            {
                // Breadth was cut away by the fashion curve:
                // The section runs directly from the Fashion Cut (cutX, cutY) up to TopLine (inTlX, inTlY)
                float u = Mathf.Clamp01((y - cutY) / Mathf.Max(0.001f, inTlY - cutY));
                return Mathf.Lerp(cutX, inTlX, u);
            }
        }

        // 2. Ahead of the transom cut region: Standard hull topsides between Breadth and TopLine
        float bX = (breadthCurves != null) ? Mathf.Abs(breadthCurves.GetInnerBreadthPointAtZ(z, true).x) : 2.5f;
        float bY = GetInnerBreadthYAtZ(z);

        float tX = (topLineCurves != null) ? Mathf.Abs(topLineCurves.GetInnerTopLinePointAtZ(z, true).x) : 2.2f;
        float tY = GetInnerTopLineYAtZ(z);

        float span = tY - bY;
        if (Mathf.Abs(span) < 0.001f) return bX;

        float frac = Mathf.Clamp01((y - bY) / span);
        float x = Mathf.Lerp(bX, tX, frac);

        // Never exceed outer breadth ceiling
        if (breadthCurves != null)
        {
            float maxBr = breadthCurves.GetBreadthXAtZ(z);
            if (x > maxBr) x = maxBr;
        }

        return Mathf.Max(0.01f, x);
    }

    private float GetInnerBreadthYAtZ(float z)
    {
        if (breadthCurves != null)
        {
            return breadthCurves.GetInnerBreadthPointAtZ(z, true).y;
        }
        return (sheerCurves != null) ? sheerCurves.keelY + 2.0f : 2.0f;
    }

    private float GetInnerTopLineYAtZ(float z)
    {
        if (topLineCurves != null)
        {
            return topLineCurves.GetInnerTopLinePointAtZ(z, true).y;
        }
        return (sheerCurves != null) ? sheerCurves.keelY + 3.5f : 3.5f;
    }

    /// <summary>
    /// Evaluates the 3D surface point on the Bottom Main Deck at transverse coordinate X and longitudinal station Z.
    /// Represents the underside of the deck planks / deck beams.
    /// </summary>
    public Vector3 GetBottomDeckSurfacePoint(float x, float z)
    {
        EnsureInitialized();

        float centerY = EvaluateBottomCenterlineSheerYAtZ(z);
        float camber = EvaluateDeckCamberAtZ(z);
        float edgeY = centerY - camber;
        float halfWidth = GetInnerHullHalfBreadthAt(edgeY, z);

        if (halfWidth < 0.0001f) return new Vector3(0f, centerY, z);

        float clampedX = Mathf.Clamp(x, -halfWidth, halfWidth);
        float normX = clampedX / halfWidth;
        float y = centerY - camber * (normX * normX);

        return new Vector3(clampedX, y, z);
    }

    /// <summary>
    /// Evaluates the 3D Bottom Deck Edge point at station Z for specified side (+1 Starboard, -1 Port).
    /// </summary>
    public Vector3 GetBottomDeckEdgePointAtZ(float z, bool starboard = true)
    {
        EnsureInitialized();

        float centerY = EvaluateBottomCenterlineSheerYAtZ(z);
        float camber = EvaluateDeckCamberAtZ(z);
        float edgeY = centerY - camber;
        float edgeX = GetInnerHullHalfBreadthAt(edgeY, z);

        return new Vector3(starboard ? edgeX : -edgeX, edgeY, z);
    }

    /// <summary>
    /// Evaluates the 3D Bottom Deck Centerline point at station Z.
    /// </summary>
    public Vector3 GetBottomDeckCenterlinePointAtZ(float z)
    {
        EnsureInitialized();
        float centerY = EvaluateBottomCenterlineSheerYAtZ(z);
        return new Vector3(0f, centerY, z);
    }

    // --- ZERO-ALLOCATION POINT BUFFER ACCESSORS ---

    public Vector3[] GetCenterlinePoints()
    {
        EnsureInitialized();
        return cachedCenterlinePts;
    }

    public Vector3[] GetDeckEdgePoints(bool starboard = true)
    {
        EnsureInitialized();
        return starboard ? cachedStbdEdgePts : cachedPortEdgePts;
    }

    public List<Vector3[]> GetDeckBeamArcs()
    {
        EnsureInitialized();
        return cachedDeckBeams;
    }

    public Vector3[] GetBottomCenterlinePoints()
    {
        EnsureInitialized();
        return cachedBottomCenterlinePts;
    }

    public Vector3[] GetBottomDeckEdgePoints(bool starboard = true)
    {
        EnsureInitialized();
        return starboard ? cachedStbdBottomEdgePts : cachedPortBottomEdgePts;
    }

    public List<Vector3[]> GetBottomDeckBeamArcs()
    {
        EnsureInitialized();
        return cachedBottomDeckBeams;
    }

    // --- GIZMO RENDERING ---

    private static GUIStyle cachedLabelStyle;
#if UNITY_EDITOR
    private static GUIStyle GetCachedLabelStyle(Color col)
    {
        if (cachedLabelStyle == null)
        {
            cachedLabelStyle = new GUIStyle();
            cachedLabelStyle.fontSize = 10;
            cachedLabelStyle.alignment = TextAnchor.LowerCenter;
            cachedLabelStyle.fontStyle = FontStyle.Bold;
        }
        cachedLabelStyle.normal.textColor = col;
        return cachedLabelStyle;
    }
#endif

    private void OnDrawGizmos()
    {
        EnsureInitialized();

        // 1. Draw Top Centerline Sheer Curve
        if (showCenterlineSheer && cachedCenterlinePts != null && cachedCenterlinePts.Length > 1)
        {
            Gizmos.color = centerlineColor;
            for (int i = 0; i < cachedCenterlinePts.Length - 1; i++)
            {
                Gizmos.DrawLine(cachedCenterlinePts[i], cachedCenterlinePts[i + 1]);
            }
        }

        // 2. Draw Top Starboard and Port Deck Edge Curves
        if (showDeckEdges && cachedStbdEdgePts != null && cachedStbdEdgePts.Length > 1)
        {
            Gizmos.color = deckEdgeColor;
            for (int i = 0; i < cachedStbdEdgePts.Length - 1; i++)
            {
                Gizmos.DrawLine(cachedStbdEdgePts[i], cachedStbdEdgePts[i + 1]);
                Gizmos.DrawLine(cachedPortEdgePts[i], cachedPortEdgePts[i + 1]);
            }

            // Draw Bow Cap (connecting Bow Tip to edges)
            Gizmos.DrawLine(bowTip, cachedStbdEdgePts[cachedStbdEdgePts.Length - 1]);
            Gizmos.DrawLine(bowTip, cachedPortEdgePts[cachedPortEdgePts.Length - 1]);

            // Draw Curved Transom Rim Margin Arc (connecting Port Transom Corner -> Stern Center -> Starboard Transom Corner)
            const int transomSteps = 16;
            Vector3 prevTransomPt = portTransomCorner;
            for (int s = 1; s <= transomSteps; s++)
            {
                float normX = Mathf.Lerp(-1f, 1f, (float)s / transomSteps);
                float x = normX * stbdTransomCorner.x;
                float y = sternCenter.y - (sternCenter.y - stbdTransomCorner.y) * (normX * normX);
                float z = Mathf.Lerp(stbdTransomCorner.z, sternCenter.z, 1f - Mathf.Abs(normX));
                Vector3 curTransomPt = new Vector3(x, y, z);
                Gizmos.DrawLine(prevTransomPt, curTransomPt);
                prevTransomPt = curTransomPt;
            }
        }

        // 3. Draw Top Transverse Camber Beams
        if (showDeckBeams && cachedDeckBeams != null)
        {
            Gizmos.color = deckBeamColor;
            for (int b = 0; b < cachedDeckBeams.Count; b++)
            {
                Vector3[] arc = cachedDeckBeams[b];
                if (arc == null || arc.Length < 2) continue;
                for (int i = 0; i < arc.Length - 1; i++)
                {
                    Gizmos.DrawLine(arc[i], arc[i + 1]);
                }
            }

            // Highlight Midship Camber Beam
            Gizmos.color = midNodeColor;
            Gizmos.DrawLine(midshipCenter, stbdMidshipEdge);
            Gizmos.DrawLine(midshipCenter, portMidshipEdge);
        }

        // 4. Draw Bottom Deck Curves (Deck Thickness Underside)
        if (showBottomDeck)
        {
            // Bottom Centerline Sheer
            if (showCenterlineSheer && cachedBottomCenterlinePts != null && cachedBottomCenterlinePts.Length > 1)
            {
                Gizmos.color = bottomCenterlineColor;
                for (int i = 0; i < cachedBottomCenterlinePts.Length - 1; i++)
                {
                    Gizmos.DrawLine(cachedBottomCenterlinePts[i], cachedBottomCenterlinePts[i + 1]);
                }
            }

            // Bottom Deck Edges
            if (showDeckEdges && cachedStbdBottomEdgePts != null && cachedStbdBottomEdgePts.Length > 1)
            {
                Gizmos.color = bottomDeckEdgeColor;
                for (int i = 0; i < cachedStbdBottomEdgePts.Length - 1; i++)
                {
                    Gizmos.DrawLine(cachedStbdBottomEdgePts[i], cachedStbdBottomEdgePts[i + 1]);
                    Gizmos.DrawLine(cachedPortBottomEdgePts[i], cachedPortBottomEdgePts[i + 1]);
                }

                // Bottom Bow Cap
                Gizmos.DrawLine(bowBottomTip, cachedStbdBottomEdgePts[cachedStbdBottomEdgePts.Length - 1]);
                Gizmos.DrawLine(bowBottomTip, cachedPortBottomEdgePts[cachedPortBottomEdgePts.Length - 1]);

                // Bottom Curved Transom Rim Margin Arc
                const int transomSteps = 16;
                Vector3 prevBottomTransomPt = portTransomBottomCorner;
                for (int s = 1; s <= transomSteps; s++)
                {
                    float normX = Mathf.Lerp(-1f, 1f, (float)s / transomSteps);
                    float x = normX * stbdTransomBottomCorner.x;
                    float y = sternBottomCenter.y - (sternBottomCenter.y - stbdTransomBottomCorner.y) * (normX * normX);
                    float z = Mathf.Lerp(stbdTransomBottomCorner.z, sternBottomCenter.z, 1f - Mathf.Abs(normX));
                    Vector3 curBottomTransomPt = new Vector3(x, y, z);
                    Gizmos.DrawLine(prevBottomTransomPt, curBottomTransomPt);
                    prevBottomTransomPt = curBottomTransomPt;
                }
            }

            // Bottom Camber Beams
            if (showDeckBeams && cachedBottomDeckBeams != null)
            {
                Gizmos.color = bottomDeckBeamColor;
                for (int b = 0; b < cachedBottomDeckBeams.Count; b++)
                {
                    Vector3[] arc = cachedBottomDeckBeams[b];
                    if (arc == null || arc.Length < 2) continue;
                    for (int i = 0; i < arc.Length - 1; i++)
                    {
                        Gizmos.DrawLine(arc[i], arc[i + 1]);
                    }
                }
            }

            // Thickness Cap Lines (Connecting Top Surface to Bottom Surface to show solid thickness)
            if (showThicknessEdgeCaps && cachedStbdEdgePts != null && cachedStbdBottomEdgePts != null)
            {
                Gizmos.color = thicknessCapColor;

                // Centerline caps at Bow and Stern
                Gizmos.DrawLine(bowTip, bowBottomTip);
                Gizmos.DrawLine(sternCenter, sternBottomCenter);

                // Transom corner caps
                Gizmos.DrawLine(stbdTransomCorner, stbdTransomBottomCorner);
                Gizmos.DrawLine(portTransomCorner, portTransomBottomCorner);

                // Midship edge caps
                Gizmos.DrawLine(stbdMidshipEdge, stbdMidshipBottomEdge);
                Gizmos.DrawLine(portMidshipEdge, portMidshipBottomEdge);

                // Edge thickness caps along hull length (sample every few points)
                int step = Mathf.Max(1, RESOLUTION / 12);
                for (int i = 0; i <= RESOLUTION; i += step)
                {
                    if (i < cachedStbdEdgePts.Length && i < cachedStbdBottomEdgePts.Length)
                    {
                        Gizmos.DrawLine(cachedStbdEdgePts[i], cachedStbdBottomEdgePts[i]);
                        Gizmos.DrawLine(cachedPortEdgePts[i], cachedPortBottomEdgePts[i]);
                    }
                }
            }
        }

        // 5. Draw Structural Node Spheres
        if (showNodeSpheres)
        {
            // Bow Tip
            Gizmos.color = bowNodeColor;
            Gizmos.DrawSphere(bowTip, nodeSphereRadius);

            // Midship Apexes
            Gizmos.color = midNodeColor;
            Gizmos.DrawSphere(midshipCenter, nodeSphereRadius * 1.1f);
            Gizmos.DrawSphere(stbdMidshipEdge, nodeSphereRadius * 0.9f);
            Gizmos.DrawSphere(portMidshipEdge, nodeSphereRadius * 0.9f);

            // Stern Points & Transom Corners
            Gizmos.color = sternNodeColor;
            Gizmos.DrawSphere(sternCenter, nodeSphereRadius);
            Gizmos.DrawSphere(stbdTransomCorner, nodeSphereRadius * 0.9f);
            Gizmos.DrawSphere(portTransomCorner, nodeSphereRadius * 0.9f);

            if (showBottomDeck)
            {
                Gizmos.color = new Color(bowNodeColor.r, bowNodeColor.g, bowNodeColor.b, 0.6f);
                Gizmos.DrawSphere(bowBottomTip, nodeSphereRadius * 0.8f);

                Gizmos.color = new Color(midNodeColor.r, midNodeColor.g, midNodeColor.b, 0.6f);
                Gizmos.DrawSphere(midshipBottomCenter, nodeSphereRadius * 0.85f);
                Gizmos.DrawSphere(stbdMidshipBottomEdge, nodeSphereRadius * 0.75f);
                Gizmos.DrawSphere(portMidshipBottomEdge, nodeSphereRadius * 0.75f);

                Gizmos.color = new Color(sternNodeColor.r, sternNodeColor.g, sternNodeColor.b, 0.6f);
                Gizmos.DrawSphere(sternBottomCenter, nodeSphereRadius * 0.8f);
                Gizmos.DrawSphere(stbdTransomBottomCorner, nodeSphereRadius * 0.75f);
                Gizmos.DrawSphere(portTransomBottomCorner, nodeSphereRadius * 0.75f);
            }
        }

#if UNITY_EDITOR
        if (showLabels)
        {
            Handles.Label(bowTip + Vector3.up * 0.18f, "Main Deck Bow Tip", GetCachedLabelStyle(bowNodeColor));
            Handles.Label(midshipCenter + Vector3.up * 0.18f, $"Main Deck Midship Apex (Sheer Y={midshipCenter.y:F2})", GetCachedLabelStyle(midNodeColor));
            Handles.Label(stbdMidshipEdge + Vector3.up * 0.15f, $"Midship Deck Edge (Camber={midshipCamber:F2}m)", GetCachedLabelStyle(midNodeColor));
            Handles.Label(sternCenter + Vector3.up * 0.18f, "Main Deck Stern Center", GetCachedLabelStyle(sternNodeColor));
            Handles.Label(stbdTransomCorner + Vector3.up * 0.15f, "Deck Transom Corner", GetCachedLabelStyle(sternNodeColor));

            if (showBottomDeck)
            {
                Handles.Label(midshipBottomCenter + Vector3.down * 0.12f, $"Bottom Deck (Thickness={deckThickness:F2}m)", GetCachedLabelStyle(bottomCenterlineColor));
            }
        }
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MainDeckCurves))]
public class MainDeckCurvesEditor : Editor
{
    private void OnSceneGUI()
    {
        MainDeckCurves curves = (MainDeckCurves)target;
        if (!curves.enableInteractiveHandles) return;

        curves.EnsureInitialized();

        // 1. Bow Tip Handle (Centerline X = 0)
        EditorGUI.BeginChangeCheck();
        Vector3 newBow = Handles.PositionHandle(curves.bowTip, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(curves, "Move Main Deck Bow Tip");
            newBow.x = 0f;
            curves.bowTip.y = newBow.y;
            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);
        }

        // 2. Midship Centerline Apex Handle (Centerline X = 0)
        EditorGUI.BeginChangeCheck();
        Vector3 newMidCenter = Handles.PositionHandle(curves.midshipCenter, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(curves, "Move Main Deck Midship Apex");
            newMidCenter.x = 0f;
            curves.midshipCenter.y = newMidCenter.y;
            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);
        }

        // 3. Midship Side Edge Handle (Camber Adjustment)
        EditorGUI.BeginChangeCheck();
        Vector3 newMidEdge = Handles.PositionHandle(curves.stbdMidshipEdge, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(curves, "Adjust Main Deck Camber");
            float newCamber = curves.midshipCenter.y - newMidEdge.y;
            curves.midshipCamber = Mathf.Max(0f, newCamber);
            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);
        }

        // 4. Stern Centerline Point Handle (Centerline X = 0)
        EditorGUI.BeginChangeCheck();
        Vector3 newSternCenter = Handles.PositionHandle(curves.sternCenter, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(curves, "Move Main Deck Stern Center");
            newSternCenter.x = 0f;
            curves.sternCenter.y = newSternCenter.y;
            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);
        }
    }

    public override void OnInspectorGUI()
    {
        MainDeckCurves curves = (MainDeckCurves)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Main Deck Top Surface Readout", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Bow Tip Point (X=0):", $"Y: {curves.bowTip.y:F2}, Z: {curves.bowTip.z:F2}");
        EditorGUILayout.LabelField("Midship Center Apex (X=0):", $"Y: {curves.midshipCenter.y:F2}, Z: {curves.midshipCenter.z:F2}");
        EditorGUILayout.LabelField("Midship Deck Edge (Inner Hull):", $"X: {curves.stbdMidshipEdge.x:F2}, Y: {curves.stbdMidshipEdge.y:F2}, Z: {curves.stbdMidshipEdge.z:F2}");
        EditorGUILayout.LabelField("Midship Camber (Round of Beam):", $"{curves.midshipCamber:F2} m");
        EditorGUILayout.LabelField("Stern Center Point (X=0):", $"Y: {curves.sternCenter.y:F2}, Z: {curves.sternCenter.z:F2}");
        EditorGUILayout.LabelField("Transom Corner (Inner Fashion):", $"X: {curves.stbdTransomCorner.x:F2}, Y: {curves.stbdTransomCorner.y:F2}, Z: {curves.stbdTransomCorner.z:F2}");

        if (curves.showBottomDeck)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Main Deck Bottom Surface Readout", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Deck Physical Thickness:", $"{curves.deckThickness:F2} m");
            EditorGUILayout.LabelField("Bow Bottom Tip (X=0):", $"Y: {curves.bowBottomTip.y:F2}, Z: {curves.bowBottomTip.z:F2}");
            EditorGUILayout.LabelField("Midship Bottom Center (X=0):", $"Y: {curves.midshipBottomCenter.y:F2}, Z: {curves.midshipBottomCenter.z:F2}");
            EditorGUILayout.LabelField("Midship Bottom Edge:", $"X: {curves.stbdMidshipBottomEdge.x:F2}, Y: {curves.stbdMidshipBottomEdge.y:F2}, Z: {curves.stbdMidshipBottomEdge.z:F2}");
            EditorGUILayout.LabelField("Stern Bottom Center (X=0):", $"Y: {curves.sternBottomCenter.y:F2}, Z: {curves.sternBottomCenter.z:F2}");
            EditorGUILayout.LabelField("Transom Bottom Corner:", $"X: {curves.stbdTransomBottomCorner.x:F2}, Y: {curves.stbdTransomBottomCorner.y:F2}, Z: {curves.stbdTransomBottomCorner.z:F2}");
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update Main Deck Profile", GUILayout.Height(30)))
        {
            Undo.RecordObject(curves, "Update Main Deck Profile");
            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);
        }
    }
}
#endif
