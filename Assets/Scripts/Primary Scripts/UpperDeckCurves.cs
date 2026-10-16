using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Serializable configuration and geometry cache for a single Upper Deck Segment
/// (e.g., Forecastle, Quarterdeck, Midship Catwalk, Poop Deck).
/// </summary>
[System.Serializable]
public class UpperDeckSegment
{
    public string segmentName = "Quarterdeck";
    public bool isEnabled = true;

    [Header("Inner Boundary Flush Snapping")]
    [Tooltip("Automatically lock the forward edge flush against the inner stem post profile at this deck's elevation.")]
    public bool flushToStem = false;

    [Tooltip("Automatically lock the aft edge flush against the inner stern transom / fashion timber profile at this deck's elevation.")]
    public bool flushToTransom = false;

    [Header("Longitudinal Span")]
    [Tooltip("Forward boundary station Z of this deck segment.")]
    public float forwardZ = 2.0f;

    [Tooltip("Aft boundary station Z of this deck segment.")]
    public float aftZ = -12.0f;

    [Header("Supporting Deck (Tier 2+ Only)")]
    [Tooltip("Index of the supporting lower-tier deck segment that this superstructure rests upon.")]
    public int supportingSegmentIndex = 0;

    [Header("Height Overrides (Optional)")]
    [Tooltip("If true, overrides the master Tier headroom with custom headroom for this specific segment.")]
    public bool overrideTierHeight = false;
    [Range(1.0f, 3.5f)] public float customHeadroom = 2.05f;

    [Header("Stem & Stern Limits")]
    [Tooltip("Safety margin offset (in meters) inside the stem post (0 = 100% flush against stem).")]
    [Range(0f, 2.0f)] public float stemClearance = 0f;

    [Tooltip("Safety margin offset (in meters) forward of the stern profile / transom (0 = 100% flush against transom).")]
    [Range(0f, 2.0f)] public float sternClearance = 0f;

    [Header("Solid Thickness")]
    [Tooltip("Enable rendering and evaluation of the bottom ceiling surface for this segment.")]
    public bool showBottomDeck = true;

    [Tooltip("Physical vertical thickness / spacing between the top and bottom deck surfaces.")]
    [Range(0.01f, 0.6f)] public float deckThickness = 0.18f;

    [Tooltip("Draw vertical edge cap lines connecting top deck edge to bottom deck edge across the hull.")]
    public bool showThicknessEdgeCaps = true;

    [Header("Waist Closures & Breastworks")]
    [Tooltip("Render forward transverse breastwork bulkhead facing the waist.")]
    public bool showForwardBreastwork = true;

    [Tooltip("Render aft transverse breastwork bulkhead facing the waist or stern.")]
    public bool showAftBreastwork = false;

    [Tooltip("Number of vertical timber stanchions drawn across the breastwork bulkhead.")]
    [Range(2, 16)] public int breastworkStanchionCount = 6;

    [Header("Visualization Settings")]
    public bool showCenterline = true;
    public bool showEdges = true;
    public bool showBeams = true;
    public bool showEndMargins = true;
    public bool showNodeSpheres = true;
    [Range(3, 40)] public int deckBeamCount = 10;

    [Header("Gizmo Colors")]
    public Color deckColor = new Color(0.95f, 0.65f, 0.25f, 0.95f);     // Warm Amber / Oak
    public Color deckEdgeColor = new Color(0.85f, 0.50f, 0.15f, 0.95f); // Rich Chestnut
    public Color beamColor = new Color(0.95f, 0.80f, 0.40f, 0.60f);     // Translucent Gold
    public Color bottomColor = new Color(0.65f, 0.40f, 0.15f, 0.75f);   // Dark Walnut
    public Color breastworkColor = new Color(0.90f, 0.85f, 0.55f, 0.90f);// Light Sand Bulkhead

    // --- LANDMARK NODES ---
    [HideInInspector] public Vector3 midshipCenter;
    [HideInInspector] public Vector3 stbdMidshipEdge;
    [HideInInspector] public Vector3 portMidshipEdge;
    [HideInInspector] public Vector3 fwdCenterTip;
    [HideInInspector] public Vector3 aftCenterTip;

    [HideInInspector] public Vector3 midshipBottomCenter;
    [HideInInspector] public Vector3 stbdMidshipBottomEdge;
    [HideInInspector] public Vector3 portMidshipBottomEdge;
    [HideInInspector] public Vector3 fwdBottomCenterTip;
    [HideInInspector] public Vector3 aftBottomCenterTip;

    // --- READOUT DATA ---
    [HideInInspector] public float actualHeadroom = 2.05f;
    [HideInInspector] public float bulwarkHeightToTopLine = 1.10f;
    [HideInInspector] public float midshipBeamWidth = 4.0f;

    // --- CACHED 3D GEOMETRY BUFFERS (Zero GC Runtime Performance) ---
    [System.NonSerialized] public Vector3[] cachedCenterlinePts;
    [System.NonSerialized] public Vector3[] cachedStbdEdgePts;
    [System.NonSerialized] public Vector3[] cachedPortEdgePts;
    [System.NonSerialized] public List<Vector3[]> cachedDeckBeams = new List<Vector3[]>();

    [System.NonSerialized] public Vector3[] cachedBottomCenterlinePts;
    [System.NonSerialized] public Vector3[] cachedStbdBottomEdgePts;
    [System.NonSerialized] public Vector3[] cachedPortBottomEdgePts;
    [System.NonSerialized] public List<Vector3[]> cachedBottomDeckBeams = new List<Vector3[]>();

    // Transverse end cap arcs at forwardZ and aftZ
    [System.NonSerialized] public Vector3[] fwdEndMarginArc;
    [System.NonSerialized] public Vector3[] aftEndMarginArc;
    [System.NonSerialized] public Vector3[] bottomFwdEndMarginArc;
    [System.NonSerialized] public Vector3[] bottomAftEndMarginArc;

    // Breastwork transverse closure arcs and vertical framing
    [System.NonSerialized] public Vector3[] forwardBreastworkTopArc;
    [System.NonSerialized] public Vector3[] forwardBreastworkBottomArc;
    [System.NonSerialized] public List<Vector3[]> forwardBreastworkStanchions = new List<Vector3[]>();

    [System.NonSerialized] public Vector3[] aftBreastworkTopArc;
    [System.NonSerialized] public Vector3[] aftBreastworkBottomArc;
    [System.NonSerialized] public List<Vector3[]> aftBreastworkStanchions = new List<Vector3[]>();
}

/// <summary>
/// Serializable configuration for an Upper Deck Tier.
/// Tier 1 represents decks directly above the Main Deck (Forecastle, Midship Gangway, Quarterdeck).
/// Tier 2 represents superstructures resting atop Tier 1 decks (Poop Deck, Topgallant Forecastle).
/// </summary>
[System.Serializable]
public class UpperDeckTier
{
    public string tierName = "Tier 1";
    [Tooltip("1 = Upper Deck directly above Main Deck; 2 = Superstructure above Tier 1.")]
    public int tierLevel = 1;

    [Header("Master Elevation & Headroom")]
    [Tooltip("Target headroom (in meters) above the deck directly below (Main Deck for Tier 1).")]
    [Range(1.0f, 3.5f)] public float tierHeadroom = 2.05f;

    [Tooltip("Lock deck directly to TopLineCurves (Tier 1) or lower bulwark ceiling (Tier 2).")]
    public bool snapToCeilingBoundary = false;

    [Header("Bulwark Settings")]
    [Tooltip("Bulwark height extending upward from this tier's deck surface.")]
    [Range(0.3f, 2.5f)] public float bulwarkHeight = 1.10f;

    [Header("Master Sheer & Camber")]
    [Tooltip("Ratio of Main Deck sheer curvature applied to this tier (0 = flat, 1 = same rise as Main Deck).")]
    [Range(0f, 1.5f)] public float sheerScale = 0.8f;

    [Tooltip("Camber (Round of Beam drop) at midships.")]
    [Range(0f, 0.8f)] public float midshipCamber = 0.20f;

    [Tooltip("Scale camber drop proportionally to local deck beam at station Z.")]
    public bool proportionalCamberToBeam = true;

    [Header("Bulwark Cap Rail Visualization")]
    [Tooltip("Enable rendering of the 3D Bulwark Cap Rail curve and vertical framing.")]
    public bool showBulwark = true;
    public Color bulwarkRailColor = new Color(0.95f, 0.40f, 0.15f, 0.95f); // Rich Vermilion / Burnt Orange
    public Color bulwarkStanchionColor = new Color(0.85f, 0.70f, 0.30f, 0.75f); // Warm Stanchion Gold
    [Range(4, 30)] public int bulwarkStanchionCount = 12;

    [System.NonSerialized] public Vector3[] cachedBulwarkRailPts;
    [System.NonSerialized] public Vector3[] cachedInnerBulwarkRailPts;
    [System.NonSerialized] public List<Vector3[]> cachedBulwarkStanchions = new List<Vector3[]>();

    [Header("Segments on this Tier")]
    public List<UpperDeckSegment> segments = new List<UpperDeckSegment>();
}

/// <summary>
/// UpperDeckCurves manages a multi-tier, multi-segment upper deck system
/// (Forecastle, Midship Catwalks, Quarterdeck, Poop Deck)
/// bounded between MainDeckCurves (floor datum) and TopLineCurves (hull sheer ceiling),
/// with complete physical thickness, open waist closures, and footprint support safeguards.
/// </summary>
[ExecuteInEditMode]
public class UpperDeckCurves : MonoBehaviour
{
    [Header("Primary Datum References")]
    [Tooltip("Reference to MainDeckCurves which acts as the floor datum for Tier 1.")]
    public MainDeckCurves mainDeckCurves;

    [Tooltip("Reference to TopLineCurves which acts as the ceiling boundary for Tier 1.")]
    public TopLineCurves topLineCurves;

    [Header("Hull Geometry References")]
    public SheerCurves sheerCurves;
    public BreadthCurves breadthCurves;
    public BilgeCurves bilgeCurves;
    public FloorCurves floorCurves;
    public FashionCurves fashionCurves;
    public BodyCurves bodyCurves;
    public BackboneCurves backboneCurves;

    [Header("Master TopLine Synchronization")]
    [Tooltip("Automatically synchronize TopLine elevation and Transom Rake Height to crown the highest active upper deck tier (e.g. Poop Deck).")]
    public bool syncTopLineToHighestDeck = true;

    [SerializeField, HideInInspector] private float baseTopLineMidshipY = -1f;
    [SerializeField, HideInInspector] private float baseTransomRakeHeight = -1f;
    [SerializeField, HideInInspector] private float baseUpperStemRatio = -1f;

    [Header("Main Deck Waist Bulwark (Open Waist)")]
    [Tooltip("Render a physical bulwark and cap rail along the Main Deck open waist between upper deck breaks.")]
    public bool showWaistBulwark = true;
    [Range(0.4f, 2.5f)] public float waistBulwarkHeight = 1.10f;
    [Range(0.05f, 0.40f)] public float waistBulwarkThickness = 0.15f;
    [Range(2, 24)] public int waistStanchionCount = 8;
    public Color waistBulwarkColor = new Color(0.92f, 0.55f, 0.20f, 0.95f);
    public Color waistStanchionColor = new Color(0.85f, 0.70f, 0.30f, 0.75f);

    [HideInInspector] public Vector3 waistMidshipHandlePt;

    // Zero-GC Cached buffers for Main Deck Waist Bulwark
    [System.NonSerialized] public Vector3[] cachedWaistRailPts;
    [System.NonSerialized] public Vector3[] cachedInnerWaistRailPts;
    [System.NonSerialized] public List<Vector3[]> cachedWaistStanchions = new List<Vector3[]>();

    [Header("Interactive Scene Handles")]
    [Tooltip("Enable interactive 3D position handles in Scene view to move deck endpoints and boundaries.")]
    public bool enableInteractiveHandles = true;

    [Range(0.04f, 0.25f)] public float nodeSphereRadius = 0.10f;
    public bool showLabels = true;

    [Header("Upper Deck Tiers")]
    [Tooltip("List of Upper Deck Tiers (Tier 1 = Upper Decks; Tier 2 = Superstructures).")]
    public List<UpperDeckTier> tiers = new List<UpperDeckTier>();

    [HideInInspector] public int changeVersion = 0;
    [HideInInspector] public bool isInitialized = false;
    private bool isUpdatingCurves = false;
    private const int RESOLUTION = 48;

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Reset()
    {
        InitializeDefaultTiers();
    }

    public void EnsureInitialized()
    {
        if (isInitialized && tiers != null && tiers.Count > 0 && tiers[0].cachedBulwarkRailPts != null)
            return;

        if (tiers == null || tiers.Count == 0)
        {
            InitializeDefaultTiers();
        }
        else
        {
            MigrateLegacyClearances();
            UpdateCachedCurves();
        }
        isInitialized = true;
    }

    public void MigrateLegacyClearances()
    {
        if (tiers == null) return;
        float midZ = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.z : 0f;

        for (int t = 0; t < tiers.Count; t++)
        {
            UpperDeckTier tier = tiers[t];
            if (tier == null || tier.segments == null) continue;

            for (int s = 0; s < tier.segments.Count; s++)
            {
                UpperDeckSegment seg = tier.segments[s];
                if (seg == null) continue;

                // Auto-migrate old 0.35m clearance defaults from initial instantiation
                if (Mathf.Approximately(seg.stemClearance, 0.35f) && !seg.flushToStem)
                {
                    seg.stemClearance = 0f;
                    if (seg.segmentName.IndexOf("Fore", System.StringComparison.OrdinalIgnoreCase) >= 0 || seg.forwardZ > midZ + 2f)
                    {
                        seg.flushToStem = true;
                    }
                }
                if (Mathf.Approximately(seg.sternClearance, 0.35f) && !seg.flushToTransom)
                {
                    seg.sternClearance = 0f;
                    if (seg.segmentName.IndexOf("Quarter", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        seg.segmentName.IndexOf("Poop", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        seg.aftZ < midZ - 2f)
                    {
                        seg.flushToTransom = true;
                    }
                }
            }
        }
    }

    public void FindReferences()
    {
        if (mainDeckCurves == null) mainDeckCurves = GetComponentInParent<MainDeckCurves>();
        if (mainDeckCurves == null) mainDeckCurves = FindFirstObjectByType<MainDeckCurves>();

        if (topLineCurves == null) topLineCurves = GetComponentInParent<TopLineCurves>();
        if (topLineCurves == null) topLineCurves = FindFirstObjectByType<TopLineCurves>();

        if (sheerCurves == null) sheerCurves = GetComponentInParent<SheerCurves>();
        if (sheerCurves == null) sheerCurves = FindFirstObjectByType<SheerCurves>();

        if (breadthCurves == null) breadthCurves = GetComponentInParent<BreadthCurves>();
        if (breadthCurves == null) breadthCurves = FindFirstObjectByType<BreadthCurves>();

        if (bilgeCurves == null) bilgeCurves = GetComponentInParent<BilgeCurves>();
        if (bilgeCurves == null) bilgeCurves = FindFirstObjectByType<BilgeCurves>();

        if (floorCurves == null) floorCurves = GetComponentInParent<FloorCurves>();
        if (floorCurves == null) floorCurves = FindFirstObjectByType<FloorCurves>();

        if (fashionCurves == null) fashionCurves = GetComponentInParent<FashionCurves>();
        if (fashionCurves == null) fashionCurves = FindFirstObjectByType<FashionCurves>();

        if (bodyCurves == null) bodyCurves = GetComponentInParent<BodyCurves>();
        if (bodyCurves == null) bodyCurves = FindFirstObjectByType<BodyCurves>();

        if (backboneCurves == null) backboneCurves = GetComponentInParent<BackboneCurves>();
        if (backboneCurves == null) backboneCurves = FindFirstObjectByType<BackboneCurves>();
    }

    [ContextMenu("Reset to Default Upper Decks")]
    public void InitializeDefaultTiers()
    {
        FindReferences();
        if (mainDeckCurves != null) mainDeckCurves.EnsureInitialized();

        tiers.Clear();

        float bowZ = (mainDeckCurves != null) ? mainDeckCurves.bowTip.z : 14f;
        float sternZ = (mainDeckCurves != null) ? mainDeckCurves.sternCenter.z : -12f;
        float midZ = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.z : 0f;

        // Tier 1: Upper Decks (Forecastle + Quarterdeck with Open Waist)
        UpperDeckTier tier1 = new UpperDeckTier
        {
            tierName = "Tier 1 (Upper Decks)",
            tierLevel = 1,
            tierHeadroom = 2.05f,
            snapToCeilingBoundary = false,
            bulwarkHeight = 1.10f,
            sheerScale = 0.8f,
            midshipCamber = 0.20f,
            proportionalCamberToBeam = true
        };

        // 1. Forecastle (Bow to forward waist break, flush against inner stem)
        float fwdStemZ = (sheerCurves != null) ? sheerCurves.GetStemProfileZAtY(6.0f) : bowZ;
        UpperDeckSegment forecastle = new UpperDeckSegment
        {
            segmentName = "Forecastle",
            isEnabled = true,
            flushToStem = true,
            forwardZ = fwdStemZ,
            aftZ = Mathf.Lerp(midZ, fwdStemZ, 0.45f),
            stemClearance = 0f,
            showForwardBreastwork = false,
            showAftBreastwork = true,
            deckThickness = 0.18f,
            deckColor = new Color(0.95f, 0.70f, 0.30f, 0.95f)
        };
        tier1.segments.Add(forecastle);

        // 2. Quarterdeck (Aft waist break to Stern, flush against inner transom)
        float aftSternZ = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(6.0f) : sternZ;
        UpperDeckSegment quarterdeck = new UpperDeckSegment
        {
            segmentName = "Quarterdeck",
            isEnabled = true,
            flushToTransom = true,
            forwardZ = Mathf.Lerp(sternZ, midZ, 0.75f),
            aftZ = aftSternZ,
            sternClearance = 0f,
            showForwardBreastwork = true,
            showAftBreastwork = false,
            deckThickness = 0.18f,
            deckColor = new Color(0.90f, 0.60f, 0.20f, 0.95f)
        };
        tier1.segments.Add(quarterdeck);

        tiers.Add(tier1);

        isInitialized = true;
        UpdateCachedCurves();
    }

    /// <summary>
    /// Evaluates the exact inner stem post / bow inner boundary Z station at a given elevation Y.
    /// Conforms directly to TopLineCurves and BreadthCurves inner lines when enabled, or SheerCurves stem profile.
    /// </summary>
    public float GetInnerStemZAtElevation(float elevationY)
    {
        if (topLineCurves != null && topLineCurves.showInnerLine)
        {
            Vector3 inTopTip = topLineCurves.GetInnerTopLineBowTip(true);
            float tlBowY = inTopTip.y;

            if (breadthCurves != null && breadthCurves.showInnerLine)
            {
                Vector3 inBreadthTip = breadthCurves.GetInnerBreadthBowTip(true);
                float brBowY = inBreadthTip.y;

                if (elevationY >= tlBowY)
                {
                    return inTopTip.z;
                }
                else if (elevationY <= brBowY)
                {
                    return inBreadthTip.z;
                }
                else
                {
                    float u = Mathf.Clamp01((elevationY - brBowY) / Mathf.Max(0.001f, tlBowY - brBowY));
                    return Mathf.Lerp(inBreadthTip.z, inTopTip.z, u);
                }
            }

            return inTopTip.z;
        }

        if (sheerCurves != null)
        {
            return sheerCurves.GetStemProfileZAtY(elevationY);
        }

        return (topLineCurves != null) ? topLineCurves.cachedFwdP0.z : 14f;
    }

    /// <summary>
    /// Evaluates the exact inner stern profile / transom centerline Z station at a given elevation Y.
    /// Conforms directly to FashionCurves inner fashion timbers when enabled, or SheerCurves stern transom profile.
    /// </summary>
    public float GetInnerSternZAtElevation(float elevationY)
    {
        if (fashionCurves != null && fashionCurves.showInnerLine)
        {
            Vector3 inFashionPt = fashionCurves.GetInnerFashionPointAtY(elevationY, 1f);



            return inFashionPt.z;
        }

        if (sheerCurves != null)
        {
            return sheerCurves.GetSternProfileZAtY(elevationY);
        }

        return (fashionCurves != null) ? fashionCurves.stbdP1.z : -12f;
    }

    /// <summary>
    /// Computes the remaining vertical clearance (in meters) between MainDeckCurves and TopLineCurves
    /// across the specified longitudinal station range.
    /// </summary>
    public float GetRemainingUpperSpace(float forwardZ, float aftZ)
    {
        if (mainDeckCurves == null || topLineCurves == null) return 0f;
        mainDeckCurves.EnsureInitialized();

        float minClearance = float.MaxValue;
        const int samples = 10;
        for (int i = 0; i <= samples; i++)
        {
            float z = Mathf.Lerp(aftZ, forwardZ, (float)i / samples);
            float mainDeckY = mainDeckCurves.GetDeckSurfacePoint(0, z).y;
            float topLineY = topLineCurves.GetInnerTopLinePointAtZ(z, true).y;
            float clearance = topLineY - mainDeckY;
            if (clearance < minClearance) minClearance = clearance;
        }

        return Mathf.Max(0f, minClearance);
    }

    /// <summary>
    /// Checks whether there is sufficient vertical space between Main Deck and TopLine
    /// to accommodate an upper deck tier with physical thickness and minimal headroom.
    /// </summary>
    public bool CanAddUpperDeck(float forwardZ, float aftZ)
    {
        const float minRequiredSpace = 0.68f; // 0.18m deck thickness + 0.50m minimum headroom
        return GetRemainingUpperSpace(forwardZ, aftZ) >= minRequiredSpace;
    }

    /// <summary>
    /// Checks whether a valid supporting Tier 1 deck exists to support a Tier 2 superstructure.
    /// </summary>
    public bool CanAddTier2Deck(out UpperDeckSegment supportingSegment)
    {
        supportingSegment = null;
        if (tiers == null || tiers.Count == 0) return false;

        UpperDeckTier tier1 = tiers[0];
        if (tier1.segments != null && tier1.segments.Count > 0)
        {
            for (int i = 0; i < tier1.segments.Count; i++)
            {
                if (tier1.segments[i].segmentName.IndexOf("Quarter", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    supportingSegment = tier1.segments[i];
                    return true;
                }
            }
            supportingSegment = tier1.segments[tier1.segments.Count - 1];
            return true;
        }
        return false;
    }

    [ContextMenu("Snap Forecastle Flush to Stem")]
    public void SnapForecastleFlushToStem()
    {
        EnsureInitialized();
        float maxBowZ = (topLineCurves != null) ? topLineCurves.cachedFwdP0.z : 15f;
        for (int t = 0; t < tiers.Count; t++)
        {
            for (int s = 0; s < tiers[t].segments.Count; s++)
            {
                UpperDeckSegment seg = tiers[t].segments[s];
                if (seg != null && seg.segmentName.IndexOf("Fore", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    seg.flushToStem = true;
                    seg.stemClearance = 0f;
                    float stemTipY = EvaluateCenterlineSheerYAtZ(tiers[t], seg, t, maxBowZ);
                    seg.forwardZ = GetInnerStemZAtElevation(stemTipY);
                }
            }
        }
        UpdateCachedCurves();
    }

    [ContextMenu("Snap Quarterdeck Flush to Transom")]
    public void SnapQuarterdeckFlushToTransom()
    {
        EnsureInitialized();
        float minSternZ = (fashionCurves != null) ? fashionCurves.innerStbdP4.z : -15f;
        for (int t = 0; t < tiers.Count; t++)
        {
            for (int s = 0; s < tiers[t].segments.Count; s++)
            {
                UpperDeckSegment seg = tiers[t].segments[s];
                if (seg != null && (seg.segmentName.IndexOf("Quarter", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    seg.segmentName.IndexOf("Poop", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    seg.flushToTransom = true;
                    seg.sternClearance = 0f;
                    float sternTipY = EvaluateCenterlineSheerYAtZ(tiers[t], seg, t, minSternZ);
                    seg.aftZ = GetInnerSternZAtElevation(sternTipY);
                }
            }
        }
        UpdateCachedCurves();
    }

    [ContextMenu("Apply Frigate Layout Preset (2-Deck)")]
    public void ApplyFrigatePreset()
    {
        EnsureInitialized();
        if (mainDeckCurves != null) mainDeckCurves.EnsureInitialized();

        float remaining = GetRemainingUpperSpace(10f, -10f);
        if (remaining < 0.68f)
        {
            Debug.LogWarning($"[UpperDeckCurves] Cannot apply Frigate layout: Insufficient vertical clearance ({remaining:F2}m < 0.68m) between MainDeckCurves and TopLineCurves.");
            return;
        }

        tiers.Clear();

        float bowZ = (mainDeckCurves != null) ? mainDeckCurves.bowTip.z : 14f;
        float sternZ = (mainDeckCurves != null) ? mainDeckCurves.sternCenter.z : -12f;
        float midZ = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.z : 0f;

        // Tier 1: Classic Frigate 2-Deck Layout
        UpperDeckTier tier1 = new UpperDeckTier
        {
            tierName = "Tier 1 (Upper Decks)",
            tierLevel = 1,
            tierHeadroom = 2.05f,
            snapToCeilingBoundary = false,
            bulwarkHeight = 1.10f,
            sheerScale = 0.8f,
            midshipCamber = 0.20f,
            proportionalCamberToBeam = true
        };

        // 1. Forecastle (Bow to waist break, flush against inner stem)
        float fwdStemZ = GetInnerStemZAtElevation(6.0f);
        UpperDeckSegment forecastle = new UpperDeckSegment
        {
            segmentName = "Forecastle",
            isEnabled = true,
            flushToStem = true,
            forwardZ = fwdStemZ,
            aftZ = Mathf.Lerp(midZ, fwdStemZ, 0.45f), // ~8m
            stemClearance = 0f,
            showForwardBreastwork = false,
            showAftBreastwork = true,
            breastworkStanchionCount = 6,
            deckThickness = 0.18f,
            deckColor = new Color(0.95f, 0.70f, 0.30f, 0.95f)
        };
        tier1.segments.Add(forecastle);

        // 2. Quarterdeck (Waist break to Stern, flush against inner transom)
        float aftSternZ = GetInnerSternZAtElevation(6.0f);
        UpperDeckSegment quarterdeck = new UpperDeckSegment
        {
            segmentName = "Quarterdeck",
            isEnabled = true,
            flushToTransom = true,
            forwardZ = Mathf.Lerp(sternZ, midZ, 0.75f), // ~ -2m
            aftZ = aftSternZ,
            sternClearance = 0f,
            showForwardBreastwork = true,
            showAftBreastwork = false,
            breastworkStanchionCount = 6,
            deckThickness = 0.18f,
            deckColor = new Color(0.90f, 0.60f, 0.20f, 0.95f)
        };
        tier1.segments.Add(quarterdeck);

        tiers.Add(tier1);
        UpdateCachedCurves();
    }

    [ContextMenu("Apply Ship-of-the-Line Layout Preset (Multi-Tier)")]
    public void ApplyShipOfTheLinePreset()
    {
        EnsureInitialized();
        if (mainDeckCurves != null) mainDeckCurves.EnsureInitialized();

        float remaining = GetRemainingUpperSpace(10f, -10f);
        if (remaining < 0.68f)
        {
            Debug.LogWarning($"[UpperDeckCurves] Cannot apply Ship-of-the-Line layout: Insufficient vertical clearance ({remaining:F2}m < 0.68m) between MainDeckCurves and TopLineCurves.");
            return;
        }

        // 1. Configure standard Tier 1 Frigate hull base
        ApplyFrigatePreset();

        // 2. Add Midship Gangway bridging the waist between Forecastle and Quarterdeck
        AddMidshipCatwalkPreset();

        // 3. Add Tier 2 Poop Deck atop Quarterdeck
        AddPoopDeckPreset();

        UpdateCachedCurves();
    }

    [ContextMenu("Add Quarterdeck Preset")]
    public void AddQuarterdeckPreset()
    {
        EnsureInitialized();
        if (tiers.Count == 0) return;

        float remaining = GetRemainingUpperSpace(10f, -10f);
        if (remaining < 0.68f)
        {
            Debug.LogWarning($"[UpperDeckCurves] Cannot add Quarterdeck: Insufficient vertical clearance ({remaining:F2}m < 0.68m) between MainDeckCurves and TopLineCurves.");
            return;
        }

        UpperDeckTier tier1 = tiers[0];
        float midZ = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.z : 0f;
        float sternZ = (mainDeckCurves != null) ? mainDeckCurves.sternCenter.z : -12f;
        float aftSternZ = GetInnerSternZAtElevation(6.0f);

        UpperDeckSegment qd = new UpperDeckSegment
        {
            segmentName = "Quarterdeck",
            isEnabled = true,
            flushToTransom = true,
            forwardZ = Mathf.Lerp(sternZ, midZ, 0.70f),
            aftZ = aftSternZ,
            sternClearance = 0f,
            showForwardBreastwork = true,
            showAftBreastwork = false,
            deckThickness = 0.18f,
            deckColor = new Color(0.90f, 0.60f, 0.20f, 0.95f)
        };
        tier1.segments.Add(qd);
        UpdateCachedCurves();
    }

    [ContextMenu("Add Forecastle Preset")]
    public void AddForecastlePreset()
    {
        EnsureInitialized();
        if (tiers.Count == 0) return;

        float remaining = GetRemainingUpperSpace(10f, -10f);
        if (remaining < 0.68f)
        {
            Debug.LogWarning($"[UpperDeckCurves] Cannot add Forecastle: Insufficient vertical clearance ({remaining:F2}m < 0.68m) between MainDeckCurves and TopLineCurves.");
            return;
        }

        UpperDeckTier tier1 = tiers[0];
        float midZ = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.z : 0f;
        float bowZ = (mainDeckCurves != null) ? mainDeckCurves.bowTip.z : 14f;
        float fwdStemZ = GetInnerStemZAtElevation(6.0f);

        UpperDeckSegment fc = new UpperDeckSegment
        {
            segmentName = "Forecastle",
            isEnabled = true,
            flushToStem = true,
            forwardZ = fwdStemZ,
            aftZ = Mathf.Lerp(midZ, fwdStemZ, 0.45f),
            stemClearance = 0f,
            showForwardBreastwork = false,
            showAftBreastwork = true,
            deckThickness = 0.18f,
            deckColor = new Color(0.95f, 0.70f, 0.30f, 0.95f)
        };
        tier1.segments.Add(fc);
        UpdateCachedCurves();
    }

    [ContextMenu("Add Midship Gangway Preset")]
    public void AddMidshipCatwalkPreset()
    {
        EnsureInitialized();
        if (tiers.Count == 0) return;

        float remaining = GetRemainingUpperSpace(10f, -10f);
        if (remaining < 0.68f)
        {
            Debug.LogWarning($"[UpperDeckCurves] Cannot add Midship Gangway: Insufficient vertical clearance ({remaining:F2}m < 0.68m) between MainDeckCurves and TopLineCurves.");
            return;
        }

        UpperDeckTier tier1 = tiers[0];

        // Locate existing Forecastle and Quarterdeck in Tier 1 to bridge the waist gap
        UpperDeckSegment fc = tier1.segments.Find(s => s != null && s.isEnabled && s.segmentName.IndexOf("Forecastle", System.StringComparison.OrdinalIgnoreCase) >= 0);
        UpperDeckSegment qd = tier1.segments.Find(s => s != null && s.isEnabled && s.segmentName.IndexOf("Quarterdeck", System.StringComparison.OrdinalIgnoreCase) >= 0);

        float fwdZ;
        float aftZ;

        if (fc != null && qd != null && fc.aftZ > qd.forwardZ + 0.1f)
        {
            // Bridge the exact longitudinal gap between Forecastle aft break and Quarterdeck forward break
            fwdZ = fc.aftZ;
            aftZ = qd.forwardZ;
        }
        else
        {
            float midZ = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.z : 0f;
            fwdZ = midZ + 2.0f;
            aftZ = midZ - 2.0f;
        }

        // Check if a Midship Gangway already exists; if so, update its extent
        UpperDeckSegment existingBridge = tier1.segments.Find(s => s != null && (s.segmentName.IndexOf("Gangway", System.StringComparison.OrdinalIgnoreCase) >= 0 || s.segmentName.IndexOf("Catwalk", System.StringComparison.OrdinalIgnoreCase) >= 0));
        if (existingBridge != null)
        {
            existingBridge.forwardZ = fwdZ;
            existingBridge.aftZ = aftZ;
            existingBridge.isEnabled = true;
        }
        else
        {
            UpperDeckSegment bridge = new UpperDeckSegment
            {
                segmentName = "Midship Gangway",
                isEnabled = true,
                flushToStem = false,
                flushToTransom = false,
                forwardZ = fwdZ,
                aftZ = aftZ,
                stemClearance = 0f,
                sternClearance = 0f,
                showForwardBreastwork = false,
                showAftBreastwork = false,
                deckThickness = 0.16f,
                deckColor = new Color(0.85f, 0.75f, 0.35f, 0.95f)
            };
            tier1.segments.Add(bridge);
        }
        UpdateCachedCurves();
    }

    [ContextMenu("Add Poop Deck Preset")]
    public void AddPoopDeckPreset()
    {
        EnsureInitialized();
        float remaining = GetRemainingUpperSpace(10f, -10f);
        if (remaining < 0.68f)
        {
            Debug.LogWarning($"[UpperDeckCurves] Cannot add Poop Deck: Insufficient vertical clearance ({remaining:F2}m < 0.68m) between MainDeckCurves and TopLineCurves.");
            return;
        }

        UpperDeckSegment supportingDeck;
        if (!CanAddTier2Deck(out supportingDeck))
        {
            Debug.LogWarning("[UpperDeckCurves] Cannot add Poop Deck: No valid Tier 1 deck exists to support it.");
            return;
        }

        UpperDeckTier tier2 = null;
        for (int i = 0; i < tiers.Count; i++)
        {
            if (tiers[i].tierLevel == 2)
            {
                tier2 = tiers[i];
                break;
            }
        }

        if (tier2 == null)
        {
            tier2 = new UpperDeckTier
            {
                tierName = "Tier 2 (Superstructure)",
                tierLevel = 2,
                tierHeadroom = 2.00f,
                snapToCeilingBoundary = false,
                bulwarkHeight = 1.05f,
                sheerScale = 0.85f,
                midshipCamber = 0.18f,
                proportionalCamberToBeam = true
            };
            tiers.Add(tier2);
        }

        float poopFwdZ = Mathf.Lerp(supportingDeck.forwardZ, supportingDeck.aftZ, 0.50f);
        float poopAftZ = GetInnerSternZAtElevation(7.5f);

        UpperDeckSegment poop = new UpperDeckSegment
        {
            segmentName = "Poop Deck",
            isEnabled = true,
            flushToTransom = true,
            forwardZ = poopFwdZ,
            aftZ = poopAftZ,
            sternClearance = 0f,
            supportingSegmentIndex = 0,
            showForwardBreastwork = true,
            showAftBreastwork = false,
            deckThickness = 0.16f,
            deckColor = new Color(0.85f, 0.45f, 0.15f, 0.95f)
        };
        tier2.segments.Add(poop);
        UpdateCachedCurves();
    }

    private void OnValidate()
    {
        MigrateLegacyClearances();
        UpdateCachedCurves();
    }

    /// <summary>
    /// Re-evaluates all Upper Deck Tiers and Segments, enforcing vertical corridors,
    /// inner hull fit, solid thickness, open waist bulkheads, and flush boundary conformance.
    /// </summary>
    public virtual void UpdateCachedCurves()
    {
        if (isUpdatingCurves) return;
        isUpdatingCurves = true;

        try
        {
            FindReferences();

            if (mainDeckCurves == null || topLineCurves == null) return;
            mainDeckCurves.EnsureInitialized();

            // 0. Synchronize TopLine elevation to crown the highest active upper deck tier (e.g. Poop Deck)
            if (syncTopLineToHighestDeck)
            {
                SynchronizeTopLineElevation();
            }

            float midZ = mainDeckCurves.midshipCenter.z;
            float maxBowZ = (topLineCurves != null) ? topLineCurves.cachedFwdP0.z : 15f;
            float minSternZ = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(12f) : ((fashionCurves != null) ? fashionCurves.innerStbdP4.z : -15f);

            for (int t = 0; t < tiers.Count; t++)
            {
                UpperDeckTier tier = tiers[t];
                if (tier == null || tier.segments == null) continue;

                for (int s = 0; s < tier.segments.Count; s++)
                {
                    UpperDeckSegment segment = tier.segments[s];
                    if (segment == null) continue;

                    EnsureSegmentBuffers(segment);

                    // 1. Clamping & Footprint Safeguards (including Flush Snapping)
                    ClampSegmentBoundaries(tier, segment, t, maxBowZ, minSternZ, midZ);

                    // 2. Centerline and Midship Landmark Evaluation
                    float segMidZ = (segment.forwardZ + segment.aftZ) * 0.5f;
                    float segMidY = EvaluateCenterlineSheerYAtZ(tier, segment, t, segMidZ);
                    segment.midshipCenter = new Vector3(0f, segMidY, segMidZ);

                    float fwdCenterY = EvaluateCenterlineSheerYAtZ(tier, segment, t, segment.forwardZ);
                    segment.fwdCenterTip = new Vector3(0f, fwdCenterY, segment.forwardZ);

                    float aftCenterY = EvaluateCenterlineSheerYAtZ(tier, segment, t, segment.aftZ);
                    segment.aftCenterTip = new Vector3(0f, aftCenterY, segment.aftZ);

                    // 3. Camber & Waterway Deck Edges
                    float camberMid = EvaluateCamberAtZ(tier, segment, segMidZ);
                    float edgeMidY = segMidY - camberMid;
                    float edgeMidX = GetInnerHullHalfBreadthAt(edgeMidY, segMidZ);
                    segment.stbdMidshipEdge = new Vector3(edgeMidX, edgeMidY, segMidZ);
                    segment.portMidshipEdge = new Vector3(-edgeMidX, edgeMidY, segMidZ);
                    segment.midshipBeamWidth = edgeMidX * 2f;

                    // Readouts
                    float lowerFloorY = GetFloorYAtZ(t, segment, segMidZ);
                    segment.actualHeadroom = segMidY - lowerFloorY;
                    float tlY = topLineCurves.GetInnerTopLinePointAtZ(segMidZ, true).y;
                    segment.bulwarkHeightToTopLine = tlY - segMidY;

                    // 4. Solid Bottom Landmark Nodes
                    segment.deckThickness = Mathf.Clamp(segment.deckThickness, 0.01f, 0.6f);
                    segment.midshipBottomCenter = new Vector3(0f, segMidY - segment.deckThickness, segMidZ);
                    float botEdgeMidY = edgeMidY - segment.deckThickness;
                    float botEdgeMidX = GetInnerHullHalfBreadthAt(botEdgeMidY, segMidZ);
                    segment.stbdMidshipBottomEdge = new Vector3(botEdgeMidX, botEdgeMidY, segMidZ);
                    segment.portMidshipBottomEdge = new Vector3(-botEdgeMidX, botEdgeMidY, segMidZ);

                    segment.fwdBottomCenterTip = new Vector3(0f, fwdCenterY - segment.deckThickness, segment.forwardZ);
                    segment.aftBottomCenterTip = new Vector3(0f, aftCenterY - segment.deckThickness, segment.aftZ);

                    // 5. Build Polylines, Deck Beams, End Margins, and Transverse Breastworks
                    BuildSegmentPolylines(tier, segment, t);
                    BuildSegmentDeckBeams(tier, segment, t);
                    BuildSegmentEndMargins(tier, segment, t);
                    BuildSegmentBreastworks(tier, segment, t);
                }
            }

            // 6. Build Longitudinal Bulwark Cap Rail curves and vertical stanchions
            BuildTierBulwarks();

            // 7. Build Main Deck Waist Bulwark across open waist
            BuildWaistBulwark();

            if (sheerCurves != null) sheerCurves.UpdateCachedProfile();
            if (fashionCurves != null) fashionCurves.UpdateCachedCurves();

            changeVersion++;
        }
        finally
        {
            isUpdatingCurves = false;
        }
    }

    private void ClampSegmentBoundaries(UpperDeckTier tier, UpperDeckSegment segment, int tierIndex, float maxBowZ, float minSternZ, float midZ)
    {
        // 1. Evaluate deck heights at extremities to query elevation-aware profiles
        float stemTipY = EvaluateCenterlineSheerYAtZ(tier, segment, tierIndex, maxBowZ);
        float maxAllowedFwdZ = GetInnerStemZAtElevation(stemTipY);

        float sternTipY = EvaluateCenterlineSheerYAtZ(tier, segment, tierIndex, minSternZ);
        float minAllowedAftZ = GetInnerSternZAtElevation(sternTipY);

        if (tier.tierLevel == 1)
        {
            if (segment.flushToStem)
            {
                segment.stemClearance = 0f;
                segment.forwardZ = maxAllowedFwdZ;
            }
            else
            {
                // If user dragged or set forwardZ near/beyond the stem boundary, snap flush to stem!
                if (segment.forwardZ >= maxAllowedFwdZ - 0.05f)
                {
                    segment.flushToStem = true;
                    segment.stemClearance = 0f;
                    segment.forwardZ = maxAllowedFwdZ;
                }
                else
                {
                    float clampMaxZ = maxAllowedFwdZ - segment.stemClearance;
                    segment.forwardZ = Mathf.Clamp(segment.forwardZ, minAllowedAftZ + 0.3f, clampMaxZ);
                }
            }

            if (segment.flushToTransom)
            {
                segment.sternClearance = 0f;
                segment.aftZ = minAllowedAftZ;
            }
            else
            {
                // If user dragged or set aftZ near/beyond the stern boundary, snap flush to transom!
                if (segment.aftZ <= minAllowedAftZ + 0.05f)
                {
                    segment.flushToTransom = true;
                    segment.sternClearance = 0f;
                    segment.aftZ = minAllowedAftZ;
                }
                else
                {
                    float clampMinZ = minAllowedAftZ + segment.sternClearance;
                    segment.aftZ = Mathf.Clamp(segment.aftZ, clampMinZ, segment.forwardZ - 0.3f);
                }
            }
        }
        else
        {
            // Tier 2+: NO FLOATING DECKS SAFEGUARD!
            UpperDeckSegment supportingSegment = GetSupportingSegment(segment);
            if (supportingSegment != null)
            {
                float supFwd = supportingSegment.forwardZ;
                float supAft = supportingSegment.aftZ;

                // Transom / Aft boundary alignment
                if (segment.flushToTransom)
                {
                    segment.sternClearance = 0f;
                    segment.aftZ = minAllowedAftZ;
                }
                else
                {
                    if (segment.aftZ <= minAllowedAftZ + 0.05f)
                    {
                        segment.flushToTransom = true;
                        segment.sternClearance = 0f;
                        segment.aftZ = minAllowedAftZ;
                    }
                    else
                    {
                        float clampMinZ = Mathf.Max(supAft, minAllowedAftZ + segment.sternClearance);
                        segment.aftZ = Mathf.Clamp(segment.aftZ, clampMinZ, supFwd - 0.2f);
                    }
                }

                // Forward boundary cannot extend forward of supporting deck forward break
                segment.forwardZ = Mathf.Min(segment.forwardZ, supFwd);
                if (segment.forwardZ < segment.aftZ + 0.2f)
                {
                    segment.forwardZ = segment.aftZ + 0.2f;
                    if (segment.forwardZ > supFwd)
                    {
                        segment.forwardZ = supFwd;
                        segment.aftZ = Mathf.Max(supAft, segment.forwardZ - 0.2f);
                    }
                }
            }
        }
    }

    public UpperDeckSegment GetSupportingSegment(UpperDeckSegment segment)
    {
        if (tiers == null || tiers.Count < 2) return null;
        UpperDeckTier tier1 = tiers[0];
        if (tier1.segments == null || tier1.segments.Count == 0) return null;

        int index = Mathf.Clamp(segment.supportingSegmentIndex, 0, tier1.segments.Count - 1);
        return tier1.segments[index];
    }

    private void EnsureSegmentBuffers(UpperDeckSegment segment)
    {
        if (segment.cachedCenterlinePts == null || segment.cachedCenterlinePts.Length != RESOLUTION + 1)
            segment.cachedCenterlinePts = new Vector3[RESOLUTION + 1];
        if (segment.cachedStbdEdgePts == null || segment.cachedStbdEdgePts.Length != RESOLUTION + 1)
            segment.cachedStbdEdgePts = new Vector3[RESOLUTION + 1];
        if (segment.cachedPortEdgePts == null || segment.cachedPortEdgePts.Length != RESOLUTION + 1)
            segment.cachedPortEdgePts = new Vector3[RESOLUTION + 1];

        if (segment.cachedBottomCenterlinePts == null || segment.cachedBottomCenterlinePts.Length != RESOLUTION + 1)
            segment.cachedBottomCenterlinePts = new Vector3[RESOLUTION + 1];
        if (segment.cachedStbdBottomEdgePts == null || segment.cachedStbdBottomEdgePts.Length != RESOLUTION + 1)
            segment.cachedStbdBottomEdgePts = new Vector3[RESOLUTION + 1];
        if (segment.cachedPortBottomEdgePts == null || segment.cachedPortBottomEdgePts.Length != RESOLUTION + 1)
            segment.cachedPortBottomEdgePts = new Vector3[RESOLUTION + 1];

        if (segment.cachedDeckBeams == null) segment.cachedDeckBeams = new List<Vector3[]>();
        if (segment.cachedBottomDeckBeams == null) segment.cachedBottomDeckBeams = new List<Vector3[]>();

        const int endSteps = 24;
        if (segment.fwdEndMarginArc == null || segment.fwdEndMarginArc.Length != endSteps + 1)
            segment.fwdEndMarginArc = new Vector3[endSteps + 1];
        if (segment.aftEndMarginArc == null || segment.aftEndMarginArc.Length != endSteps + 1)
            segment.aftEndMarginArc = new Vector3[endSteps + 1];
        if (segment.bottomFwdEndMarginArc == null || segment.bottomFwdEndMarginArc.Length != endSteps + 1)
            segment.bottomFwdEndMarginArc = new Vector3[endSteps + 1];
        if (segment.bottomAftEndMarginArc == null || segment.bottomAftEndMarginArc.Length != endSteps + 1)
            segment.bottomAftEndMarginArc = new Vector3[endSteps + 1];

        if (segment.forwardBreastworkTopArc == null || segment.forwardBreastworkTopArc.Length != endSteps + 1)
            segment.forwardBreastworkTopArc = new Vector3[endSteps + 1];
        if (segment.forwardBreastworkBottomArc == null || segment.forwardBreastworkBottomArc.Length != endSteps + 1)
            segment.forwardBreastworkBottomArc = new Vector3[endSteps + 1];
        if (segment.forwardBreastworkStanchions == null)
            segment.forwardBreastworkStanchions = new List<Vector3[]>();

        if (segment.aftBreastworkTopArc == null || segment.aftBreastworkTopArc.Length != endSteps + 1)
            segment.aftBreastworkTopArc = new Vector3[endSteps + 1];
        if (segment.aftBreastworkBottomArc == null || segment.aftBreastworkBottomArc.Length != endSteps + 1)
            segment.aftBreastworkBottomArc = new Vector3[endSteps + 1];
        if (segment.aftBreastworkStanchions == null)
            segment.aftBreastworkStanchions = new List<Vector3[]>();
    }

    private void BuildSegmentPolylines(UpperDeckTier tier, UpperDeckSegment segment, int tierIndex)
    {
        float fwdZ = segment.forwardZ;
        float aftZ = segment.aftZ;

        float fwdLimitZ = GetInnerStemZAtElevation(segment.fwdCenterTip.y);
        bool isAtStem = (segment.flushToStem || fwdZ >= fwdLimitZ - 0.05f);

        float aftLimitZ = GetInnerSternZAtElevation(segment.aftCenterTip.y);
        bool isAtTransom = (segment.flushToTransom || aftZ <= aftLimitZ + 0.05f);

        for (int i = 0; i <= RESOLUTION; i++)
        {
            float frac = (float)i / RESOLUTION;
            float z = Mathf.Lerp(aftZ, fwdZ, frac);

            float centerY = EvaluateCenterlineSheerYAtZ(tier, segment, tierIndex, z);
            float camber = EvaluateCamberAtZ(tier, segment, z);
            float edgeY = centerY - camber;
            float edgeX = GetInnerHullHalfBreadthAt(edgeY, z);

            float bottomCenterY = centerY - segment.deckThickness;
            float bottomEdgeY = bottomCenterY - camber;
            float bottomEdgeX = GetInnerHullHalfBreadthAt(bottomEdgeY, z);

            if (i == RESOLUTION && isAtStem)
            {
                // Bow tip flush convergence hugging inner stem post
                float innerStemX = (topLineCurves != null && topLineCurves.showInnerLine)
                    ? Mathf.Abs(topLineCurves.GetInnerTopLineBowTip(true).x)
                    : ((backboneCurves != null) ? backboneCurves.keelWidth * 0.5f : 0.15f);

                segment.cachedCenterlinePts[i] = new Vector3(0f, centerY, fwdZ);
                segment.cachedStbdEdgePts[i] = new Vector3(innerStemX, edgeY, fwdZ);
                segment.cachedPortEdgePts[i] = new Vector3(-innerStemX, edgeY, fwdZ);

                segment.cachedBottomCenterlinePts[i] = new Vector3(0f, bottomCenterY, fwdZ);
                segment.cachedStbdBottomEdgePts[i] = new Vector3(innerStemX, bottomEdgeY, fwdZ);
                segment.cachedPortBottomEdgePts[i] = new Vector3(-innerStemX, bottomEdgeY, fwdZ);
            }
            else if (i == 0 && isAtTransom && fashionCurves != null)
            {
                // Stern transom flush cut hugging inner fashion timbers at edges and transom profile at centerline
                Vector3 inFashionPt = fashionCurves.GetInnerFashionPointAtY(edgeY, 1f);
                float transomCornerX = Mathf.Abs(inFashionPt.x);
                float transomCornerZ = inFashionPt.z;
                float centerTransomZ = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(centerY) : transomCornerZ;

                // Re-evaluate centerline height at the exact longitudinal station of the transom profile (centerTransomZ)
                float transomCenterY = EvaluateCenterlineSheerYAtZ(tier, segment, tierIndex, centerTransomZ);
                float botTransomCenterY = transomCenterY - segment.deckThickness;

                segment.cachedCenterlinePts[0] = new Vector3(0f, transomCenterY, centerTransomZ);
                segment.cachedStbdEdgePts[0] = new Vector3(transomCornerX, edgeY, transomCornerZ);
                segment.cachedPortEdgePts[0] = new Vector3(-transomCornerX, edgeY, transomCornerZ);

                Vector3 inBotFashionPt = fashionCurves.GetInnerFashionPointAtY(bottomEdgeY, 1f);
                float botTransomCornerX = Mathf.Abs(inBotFashionPt.x);
                float botTransomCornerZ = inBotFashionPt.z;
                float botCenterTransomZ = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(bottomCenterY) : botTransomCornerZ;

                segment.cachedBottomCenterlinePts[0] = new Vector3(0f, botTransomCenterY, botCenterTransomZ);
                segment.cachedStbdBottomEdgePts[0] = new Vector3(botTransomCornerX, bottomEdgeY, botTransomCornerZ);
                segment.cachedPortBottomEdgePts[0] = new Vector3(-botTransomCornerX, bottomEdgeY, botTransomCornerZ);
            }
            else
            {
                segment.cachedCenterlinePts[i] = new Vector3(0f, centerY, z);
                segment.cachedStbdEdgePts[i] = new Vector3(edgeX, edgeY, z);
                segment.cachedPortEdgePts[i] = new Vector3(-edgeX, edgeY, z);

                segment.cachedBottomCenterlinePts[i] = new Vector3(0f, bottomCenterY, z);
                segment.cachedStbdBottomEdgePts[i] = new Vector3(bottomEdgeX, bottomEdgeY, z);
                segment.cachedPortBottomEdgePts[i] = new Vector3(-bottomEdgeX, bottomEdgeY, z);
            }
        }

        // Synchronize landmark nodes with exact evaluated polylines
        segment.fwdCenterTip = segment.cachedCenterlinePts[RESOLUTION];
        segment.aftCenterTip = segment.cachedCenterlinePts[0];
        segment.fwdBottomCenterTip = segment.cachedBottomCenterlinePts[RESOLUTION];
        segment.aftBottomCenterTip = segment.cachedBottomCenterlinePts[0];
    }

    private void BuildSegmentDeckBeams(UpperDeckTier tier, UpperDeckSegment segment, int tierIndex)
    {
        if (segment.cachedDeckBeams == null) segment.cachedDeckBeams = new List<Vector3[]>();
        if (segment.cachedBottomDeckBeams == null) segment.cachedBottomDeckBeams = new List<Vector3[]>();

        int beams = Mathf.Clamp(segment.deckBeamCount, 2, 40);
        float aftZ = segment.aftZ;
        float fwdZ = segment.forwardZ;
        const int beamSteps = 24;
        int beamIdx = 0;

        for (int b = 0; b < beams; b++)
        {
            float t = (float)b / (beams - 1);
            float z = Mathf.Lerp(aftZ, fwdZ, t);

            float centerY = EvaluateCenterlineSheerYAtZ(tier, segment, tierIndex, z);
            float camber = EvaluateCamberAtZ(tier, segment, z);
            float edgeY = centerY - camber;
            float halfWidth = GetInnerHullHalfBreadthAt(edgeY, z);

            if (halfWidth >= 0.005f)
            {
                // Top Camber Beam Arc (Reusing array if available)
                Vector3[] topArc;
                if (beamIdx < segment.cachedDeckBeams.Count && segment.cachedDeckBeams[beamIdx] != null && segment.cachedDeckBeams[beamIdx].Length == beamSteps + 1)
                {
                    topArc = segment.cachedDeckBeams[beamIdx];
                }
                else
                {
                    topArc = new Vector3[beamSteps + 1];
                    if (beamIdx < segment.cachedDeckBeams.Count)
                        segment.cachedDeckBeams[beamIdx] = topArc;
                    else
                        segment.cachedDeckBeams.Add(topArc);
                }

                for (int s = 0; s <= beamSteps; s++)
                {
                    float normX = Mathf.Lerp(-1f, 1f, (float)s / beamSteps);
                    float x = normX * halfWidth;
                    float y = centerY - camber * (normX * normX);
                    topArc[s] = new Vector3(x, y, z);
                }

                // Bottom Camber Beam Arc (Reusing array if available)
                float bottomCenterY = centerY - segment.deckThickness;
                float bottomEdgeY = bottomCenterY - camber;
                float bottomHalfWidth = GetInnerHullHalfBreadthAt(bottomEdgeY, z);

                Vector3[] bottomArc;
                if (beamIdx < segment.cachedBottomDeckBeams.Count && segment.cachedBottomDeckBeams[beamIdx] != null && segment.cachedBottomDeckBeams[beamIdx].Length == beamSteps + 1)
                {
                    bottomArc = segment.cachedBottomDeckBeams[beamIdx];
                }
                else
                {
                    bottomArc = new Vector3[beamSteps + 1];
                    if (beamIdx < segment.cachedBottomDeckBeams.Count)
                        segment.cachedBottomDeckBeams[beamIdx] = bottomArc;
                    else
                        segment.cachedBottomDeckBeams.Add(bottomArc);
                }

                for (int s = 0; s <= beamSteps; s++)
                {
                    float normX = Mathf.Lerp(-1f, 1f, (float)s / beamSteps);
                    float x = normX * bottomHalfWidth;
                    float y = bottomCenterY - camber * (normX * normX);
                    bottomArc[s] = new Vector3(x, y, z);
                }

                beamIdx++;
            }
        }

        while (segment.cachedDeckBeams.Count > beamIdx)
            segment.cachedDeckBeams.RemoveAt(segment.cachedDeckBeams.Count - 1);
        while (segment.cachedBottomDeckBeams.Count > beamIdx)
            segment.cachedBottomDeckBeams.RemoveAt(segment.cachedBottomDeckBeams.Count - 1);
    }

    private void BuildSegmentEndMargins(UpperDeckTier tier, UpperDeckSegment segment, int tierIndex)
    {
        const int steps = 24;

        // Forward End Margin Arcs
        float fwdZ = segment.cachedCenterlinePts[RESOLUTION].z;
        float fwdCenterY = segment.cachedCenterlinePts[RESOLUTION].y;
        float fwdCamber = EvaluateCamberAtZ(tier, segment, fwdZ);
        float fwdHalfWidth = Mathf.Abs(segment.cachedStbdEdgePts[RESOLUTION].x);

        float botFwdCenterY = segment.cachedBottomCenterlinePts[RESOLUTION].y;
        float botFwdHalfWidth = Mathf.Abs(segment.cachedStbdBottomEdgePts[RESOLUTION].x);

        for (int s = 0; s <= steps; s++)
        {
            float normX = Mathf.Lerp(-1f, 1f, (float)s / steps);
            float xTop = normX * fwdHalfWidth;
            float yTop = fwdCenterY - fwdCamber * (normX * normX);
            segment.fwdEndMarginArc[s] = new Vector3(xTop, yTop, fwdZ);

            float xBot = normX * botFwdHalfWidth;
            float yBot = botFwdCenterY - fwdCamber * (normX * normX);
            segment.bottomFwdEndMarginArc[s] = new Vector3(xBot, yBot, fwdZ);
        }

        // Aft End Margin Arcs (conforming to angled transom rake / corners)
        float aftCenterZ = segment.cachedCenterlinePts[0].z;
        float aftCornerZ = segment.cachedStbdEdgePts[0].z;
        float aftCenterY = segment.cachedCenterlinePts[0].y;
        float aftCamber = EvaluateCamberAtZ(tier, segment, aftCenterZ);
        float aftHalfWidth = Mathf.Abs(segment.cachedStbdEdgePts[0].x);

        float botAftCenterZ = segment.cachedBottomCenterlinePts[0].z;
        float botAftCornerZ = segment.cachedStbdBottomEdgePts[0].z;
        float botAftCenterY = segment.cachedBottomCenterlinePts[0].y;
        float botAftHalfWidth = Mathf.Abs(segment.cachedStbdBottomEdgePts[0].x);

        for (int s = 0; s <= steps; s++)
        {
            float normX = Mathf.Lerp(-1f, 1f, (float)s / steps);
            float xTop = normX * aftHalfWidth;
            float yTop = aftCenterY - aftCamber * (normX * normX);
            float zTop = Mathf.Lerp(aftCornerZ, aftCenterZ, 1f - (normX * normX));
            segment.aftEndMarginArc[s] = new Vector3(xTop, yTop, zTop);

            float xBot = normX * botAftHalfWidth;
            float yBot = botAftCenterY - aftCamber * (normX * normX);
            float zBot = Mathf.Lerp(botAftCornerZ, botAftCenterZ, 1f - (normX * normX));
            segment.bottomAftEndMarginArc[s] = new Vector3(xBot, yBot, zBot);
        }
    }

    private void BuildSegmentBreastworks(UpperDeckTier tier, UpperDeckSegment segment, int tierIndex)
    {
        const int steps = 24;

        // 1. Forward Breastwork (Facing forward into the waist or bow)
        if (segment.showForwardBreastwork)
        {
            if (segment.forwardBreastworkStanchions == null)
                segment.forwardBreastworkStanchions = new List<Vector3[]>();

            float fwdZ = segment.forwardZ;
            float fwdCenterY = EvaluateCenterlineSheerYAtZ(tier, segment, tierIndex, fwdZ);
            float fwdCamber = EvaluateCamberAtZ(tier, segment, fwdZ);
            float fwdHalfWidth = GetInnerHullHalfBreadthAt(fwdCenterY - fwdCamber, fwdZ);

            float lowerFloorY = GetFloorYAtZ(tierIndex, segment, fwdZ);

            for (int s = 0; s <= steps; s++)
            {
                float normX = Mathf.Lerp(-1f, 1f, (float)s / steps);
                float x = normX * fwdHalfWidth;
                float yTop = fwdCenterY - fwdCamber * (normX * normX);
                segment.forwardBreastworkTopArc[s] = new Vector3(x, yTop, fwdZ);

                float yBottom = lowerFloorY;
                if (tierIndex == 0 && mainDeckCurves != null)
                {
                    yBottom = mainDeckCurves.GetDeckSurfacePoint(x, fwdZ).y;
                }
                else if (tierIndex > 0)
                {
                    UpperDeckSegment supporting = GetSupportingSegment(segment);
                    if (supporting != null && tiers != null && tiers.Count > 0)
                    {
                        UpperDeckTier supTier = tiers[0];
                        float supCenterY = EvaluateCenterlineSheerYAtZ(supTier, supporting, 0, fwdZ);
                        float supCamber = EvaluateCamberAtZ(supTier, supporting, fwdZ);
                        float supEdgeY = supCenterY - supCamber;
                        float supHalfWidth = GetInnerHullHalfBreadthAt(supEdgeY, fwdZ);
                        float supNormX = (supHalfWidth > 0.001f) ? Mathf.Clamp(x / supHalfWidth, -1f, 1f) : 0f;
                        yBottom = supCenterY - supCamber * (supNormX * supNormX);
                    }
                }
                segment.forwardBreastworkBottomArc[s] = new Vector3(x, yBottom, fwdZ);
            }

            int stanchions = Mathf.Clamp(segment.breastworkStanchionCount, 2, 16);
            int fwdIdx = 0;
            for (int st = 0; st < stanchions; st++)
            {
                int sampleIdx = Mathf.RoundToInt((float)st / (stanchions - 1) * steps);
                Vector3 pTop = segment.forwardBreastworkTopArc[sampleIdx];
                Vector3 pBot = segment.forwardBreastworkBottomArc[sampleIdx];

                Vector3[] pair;
                if (fwdIdx < segment.forwardBreastworkStanchions.Count && segment.forwardBreastworkStanchions[fwdIdx] != null && segment.forwardBreastworkStanchions[fwdIdx].Length == 2)
                {
                    pair = segment.forwardBreastworkStanchions[fwdIdx];
                    pair[0] = pBot;
                    pair[1] = pTop;
                }
                else
                {
                    pair = new Vector3[] { pBot, pTop };
                    if (fwdIdx < segment.forwardBreastworkStanchions.Count)
                        segment.forwardBreastworkStanchions[fwdIdx] = pair;
                    else
                        segment.forwardBreastworkStanchions.Add(pair);
                }
                fwdIdx++;
            }
            while (segment.forwardBreastworkStanchions.Count > fwdIdx)
                segment.forwardBreastworkStanchions.RemoveAt(segment.forwardBreastworkStanchions.Count - 1);
        }
        else
        {
            if (segment.forwardBreastworkStanchions != null)
                segment.forwardBreastworkStanchions.Clear();
        }

        // 2. Aft Breastwork (Facing aft into the waist or stern)
        if (segment.showAftBreastwork)
        {
            if (segment.aftBreastworkStanchions == null)
                segment.aftBreastworkStanchions = new List<Vector3[]>();

            float aftZ = segment.aftZ;
            float aftCenterY = EvaluateCenterlineSheerYAtZ(tier, segment, tierIndex, aftZ);
            float aftCamber = EvaluateCamberAtZ(tier, segment, aftZ);
            float aftHalfWidth = GetInnerHullHalfBreadthAt(aftCenterY - aftCamber, aftZ);

            float lowerFloorY = GetFloorYAtZ(tierIndex, segment, aftZ);

            for (int s = 0; s <= steps; s++)
            {
                float normX = Mathf.Lerp(-1f, 1f, (float)s / steps);
                float x = normX * aftHalfWidth;
                float yTop = aftCenterY - aftCamber * (normX * normX);
                segment.aftBreastworkTopArc[s] = new Vector3(x, yTop, aftZ);

                float yBottom = lowerFloorY;
                if (tierIndex == 0 && mainDeckCurves != null)
                {
                    yBottom = mainDeckCurves.GetDeckSurfacePoint(x, aftZ).y;
                }
                else if (tierIndex > 0)
                {
                    UpperDeckSegment supporting = GetSupportingSegment(segment);
                    if (supporting != null && tiers != null && tiers.Count > 0)
                    {
                        UpperDeckTier supTier = tiers[0];
                        float supCenterY = EvaluateCenterlineSheerYAtZ(supTier, supporting, 0, aftZ);
                        float supCamber = EvaluateCamberAtZ(supTier, supporting, aftZ);
                        float supEdgeY = supCenterY - supCamber;
                        float supHalfWidth = GetInnerHullHalfBreadthAt(supEdgeY, aftZ);
                        float supNormX = (supHalfWidth > 0.001f) ? Mathf.Clamp(x / supHalfWidth, -1f, 1f) : 0f;
                        yBottom = supCenterY - supCamber * (supNormX * supNormX);
                    }
                }
                segment.aftBreastworkBottomArc[s] = new Vector3(x, yBottom, aftZ);
            }

            int stanchions = Mathf.Clamp(segment.breastworkStanchionCount, 2, 16);
            int aftIdx = 0;
            for (int st = 0; st < stanchions; st++)
            {
                int sampleIdx = Mathf.RoundToInt((float)st / (stanchions - 1) * steps);
                Vector3 pTop = segment.aftBreastworkTopArc[sampleIdx];
                Vector3 pBot = segment.aftBreastworkBottomArc[sampleIdx];

                Vector3[] pair;
                if (aftIdx < segment.aftBreastworkStanchions.Count && segment.aftBreastworkStanchions[aftIdx] != null && segment.aftBreastworkStanchions[aftIdx].Length == 2)
                {
                    pair = segment.aftBreastworkStanchions[aftIdx];
                    pair[0] = pBot;
                    pair[1] = pTop;
                }
                else
                {
                    pair = new Vector3[] { pBot, pTop };
                    if (aftIdx < segment.aftBreastworkStanchions.Count)
                        segment.aftBreastworkStanchions[aftIdx] = pair;
                    else
                        segment.aftBreastworkStanchions.Add(pair);
                }
                aftIdx++;
            }
            while (segment.aftBreastworkStanchions.Count > aftIdx)
                segment.aftBreastworkStanchions.RemoveAt(segment.aftBreastworkStanchions.Count - 1);
        }
        else
        {
            if (segment.aftBreastworkStanchions != null)
                segment.aftBreastworkStanchions.Clear();
        }
    }

    // --- MATHEMATICAL EVALUATION HELPERS ---

    public float GetFloorYAtZ(int tierIndex, UpperDeckSegment segment, float z)
    {
        if (tierIndex == 0)
        {
            if (mainDeckCurves != null)
            {
                return mainDeckCurves.EvaluateCenterlineSheerYAtZ(z);
            }
            return 3.5f;
        }
        else
        {
            UpperDeckSegment supporting = GetSupportingSegment(segment);
            if (supporting != null && tiers != null && tiers.Count > 0)
            {
                UpperDeckTier supTier = tiers[0];
                return EvaluateCenterlineSheerYAtZ(supTier, supporting, 0, z);
            }
            return 5.5f;
        }
    }

    public float EvaluateCenterlineSheerYAtZ(UpperDeckTier tier, UpperDeckSegment segment, int tierIndex, float z)
    {
        if (tier.snapToCeilingBoundary)
        {
            if (tier.tierLevel == 1)
            {
                if (topLineCurves != null)
                    return topLineCurves.GetInnerTopLinePointAtZ(z, true).y;
            }
            else
            {
                UpperDeckSegment supporting = GetSupportingSegment(segment);
                if (supporting != null && tiers != null && tiers.Count > 0)
                {
                    UpperDeckTier supTier = tiers[0];
                    float supY = EvaluateCenterlineSheerYAtZ(supTier, supporting, 0, z);
                    return supY + supTier.bulwarkHeight;
                }
            }
        }

        float headroom = segment.overrideTierHeight ? segment.customHeadroom : tier.tierHeadroom;

        // Base midship floor elevation
        float midFloorY = 3.5f;
        if (tierIndex == 0)
        {
            midFloorY = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.y : 3.5f;
        }
        else
        {
            UpperDeckSegment supporting = GetSupportingSegment(segment);
            if (supporting != null && tiers != null && tiers.Count > 0)
            {
                float midZ = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.z : 0f;
                midFloorY = EvaluateCenterlineSheerYAtZ(tiers[0], supporting, 0, midZ);
            }
            else
            {
                midFloorY = 5.5f;
            }
        }

        // Mathematical sheer rise: sheerScale ratio of Main Deck sheer curvature applied to this tier
        float sheerRise = 0f;
        if (mainDeckCurves != null)
        {
            float mainSheerY = mainDeckCurves.EvaluateCenterlineSheerYAtZ(z);
            sheerRise = (mainSheerY - mainDeckCurves.midshipCenter.y) * tier.sheerScale;
        }

        float desiredY = midFloorY + headroom + sheerRise;

        // Clamping to ceiling boundary for Tier 1 only if snapToCeilingBoundary is explicitly enabled
        if (tier.tierLevel == 1 && topLineCurves != null && tier.snapToCeilingBoundary)
        {
            float maxCeilingY = topLineCurves.GetInnerTopLinePointAtZ(z, true).y;
            desiredY = Mathf.Min(desiredY, maxCeilingY);
        }

        return desiredY;
    }

    public float EvaluateCamberAtZ(UpperDeckTier tier, UpperDeckSegment segment, float z)
    {
        float baseCamber = tier.midshipCamber;
        if (!tier.proportionalCamberToBeam) return baseCamber;

        float midBeam = (mainDeckCurves != null) ? mainDeckCurves.stbdMidshipEdge.x * 2f : 8f;
        if (midBeam <= 0.001f) return baseCamber;

        float segY = EvaluateCenterlineSheerYAtZ(tier, segment, 0, z);
        float localHalfWidth = GetInnerHullHalfBreadthAt(segY - baseCamber, z);
        float localBeam = localHalfWidth * 2f;

        return baseCamber * Mathf.Clamp01(localBeam / midBeam);
    }

    /// <summary>
    /// Evaluates the inner hull half-breadth X at height Y and station Z across the full transverse profile:
    /// Keel/Keelson -> Floor -> Bilge -> Breadth -> TopLine, with smooth topsides extrapolation above TopLine.
    /// </summary>
    public float GetInnerHullHalfBreadthAt(float y, float z)
    {
        // 1. Transom Cut Region
        if (fashionCurves != null && z <= fashionCurves.stbdP1.z + 0.001f)
        {
            Vector3 cutPt = fashionCurves.GetInnerFashionPointAtZ(z, 1f);
            float cutY = cutPt.y;
            float cutX = Mathf.Abs(cutPt.x);

            float inTlX = (topLineCurves != null) ? Mathf.Abs(topLineCurves.GetInnerTopLinePointAtZ(z, true).x) : 2.2f;
            float inTlY = (topLineCurves != null) ? topLineCurves.GetInnerTopLinePointAtZ(z, true).y : 3.5f;

            float inBrX = (breadthCurves != null) ? Mathf.Abs(breadthCurves.GetInnerBreadthPointAtZ(z, true).x) : 2.5f;
            float inBrY = (breadthCurves != null) ? breadthCurves.GetInnerBreadthPointAtZ(z, true).y : 2.5f;

            if (y <= cutY + 0.005f) return cutX;
            if (y >= inTlY - 0.005f)
            {
                float slope = (inTlY > inBrY + 0.01f) ? (inTlX - inBrX) / (inTlY - inBrY) : 0f;
                return Mathf.Max(0.1f, inTlX + slope * (y - inTlY));
            }

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
                float u = Mathf.Clamp01((y - cutY) / Mathf.Max(0.001f, inTlY - cutY));
                return Mathf.Lerp(cutX, inTlX, u);
            }
        }

        // 2. Standard Inner Hull Profile: Keel -> Floor -> Bilge -> Breadth -> TopLine
        float bottomSpacing = (bodyCurves != null) ? bodyCurves.innerLineSpacing : 0.2f;
        float keelBottomY = (sheerCurves != null) ? sheerCurves.keelY + bottomSpacing : bottomSpacing;
        float keelHalfWidth = (backboneCurves != null) ? backboneCurves.keelWidth * 0.5f : 0.15f;

        bool hasFloor = (floorCurves != null && floorCurves.showInnerLine);
        Vector3 inFloorPt = hasFloor ? floorCurves.GetInnerFloorPointAtZ(z) : Vector3.zero;

        bool hasBilge = (bilgeCurves != null && bilgeCurves.showInnerLine);
        Vector3 inBilgePt = hasBilge ? bilgeCurves.GetInnerBilgePointAtZ(z) : Vector3.zero;

        bool hasBreadth = (breadthCurves != null && breadthCurves.showInnerLine);
        Vector3 inBreadthPt = hasBreadth ? breadthCurves.GetInnerBreadthPointAtZ(z, true) : Vector3.zero;

        bool hasTopLine = (topLineCurves != null && topLineCurves.showInnerLine);
        Vector3 inTopLinePt = hasTopLine ? topLineCurves.GetInnerTopLinePointAtZ(z, true) : Vector3.zero;

        if (y <= keelBottomY + 0.005f) return keelHalfWidth;

        if (hasFloor && y <= inFloorPt.y)
        {
            float span = Mathf.Max(0.001f, inFloorPt.y - keelBottomY);
            float u = Mathf.Clamp01((y - keelBottomY) / span);
            return Mathf.Lerp(keelHalfWidth, Mathf.Abs(inFloorPt.x), u);
        }

        if (hasFloor && hasBilge && y <= inBilgePt.y)
        {
            float span = Mathf.Max(0.001f, inBilgePt.y - inFloorPt.y);
            float u = Mathf.Clamp01((y - inFloorPt.y) / span);
            return Mathf.Lerp(Mathf.Abs(inFloorPt.x), Mathf.Abs(inBilgePt.x), u);
        }

        if (hasBilge && hasBreadth && y <= inBreadthPt.y)
        {
            float span = Mathf.Max(0.001f, inBreadthPt.y - inBilgePt.y);
            float u = Mathf.Clamp01((y - inBilgePt.y) / span);
            return Mathf.Lerp(Mathf.Abs(inBilgePt.x), Mathf.Abs(inBreadthPt.x), u);
        }

        if (hasBreadth && hasTopLine && y <= inTopLinePt.y)
        {
            float span = Mathf.Max(0.001f, inTopLinePt.y - inBreadthPt.y);
            float u = Mathf.Clamp01((y - inBreadthPt.y) / span);
            return Mathf.Lerp(Mathf.Abs(inBreadthPt.x), Mathf.Abs(inTopLinePt.x), u);
        }

        if (hasBreadth && hasTopLine && inTopLinePt.y > inBreadthPt.y + 0.01f)
        {
            float slope = (Mathf.Abs(inTopLinePt.x) - Mathf.Abs(inBreadthPt.x)) / (inTopLinePt.y - inBreadthPt.y);
            return Mathf.Max(0.1f, Mathf.Abs(inTopLinePt.x) + slope * (y - inTopLinePt.y));
        }

        if (hasTopLine) return Mathf.Abs(inTopLinePt.x);
        if (hasBreadth) return Mathf.Abs(inBreadthPt.x);
        return 2.0f;
    }

    // --- PUBLIC SURFACE EVALUATION API ---

    public Vector3 GetDeckSurfacePoint(int tierIndex, int segmentIndex, float x, float z)
    {
        EnsureInitialized();
        if (tierIndex < 0 || tierIndex >= tiers.Count) return Vector3.zero;
        UpperDeckTier tier = tiers[tierIndex];
        if (segmentIndex < 0 || segmentIndex >= tier.segments.Count) return Vector3.zero;
        UpperDeckSegment segment = tier.segments[segmentIndex];

        float centerY = EvaluateCenterlineSheerYAtZ(tier, segment, tierIndex, z);
        float camber = EvaluateCamberAtZ(tier, segment, z);
        float halfWidth = GetInnerHullHalfBreadthAt(centerY - camber, z);

        if (halfWidth < 0.0001f) return new Vector3(0f, centerY, z);
        float clampedX = Mathf.Clamp(x, -halfWidth, halfWidth);
        float normX = clampedX / halfWidth;
        float y = centerY - camber * (normX * normX);

        return new Vector3(clampedX, y, z);
    }

    /// <summary>
    /// Evaluates the 3D Upper Deck Edge points (waterway on inner frame and bottom ceiling edge) at station Z.
    /// Returns true if an active upper deck segment spans station Z.
    /// If multiple tiers span station Z, evaluates the highest active deck tier.
    /// </summary>
    public bool GetDeckEdgePointsAtZ(float z, out Vector3 stbdEdge, out Vector3 portEdge, out Vector3 stbdBottomEdge, out Vector3 portBottomEdge)
    {
        stbdEdge = Vector3.zero;
        portEdge = Vector3.zero;
        stbdBottomEdge = Vector3.zero;
        portBottomEdge = Vector3.zero;

        EnsureInitialized();
        if (tiers == null || tiers.Count == 0) return false;

        // Search from highest tier downwards for active deck at station Z
        for (int t = tiers.Count - 1; t >= 0; t--)
        {
            if (GetDeckEdgePointsAtZ(t, z, out stbdEdge, out portEdge, out stbdBottomEdge, out portBottomEdge))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Evaluates the 3D Upper Deck Edge points (waterway on inner frame and bottom ceiling edge) at station Z for a specific tier.
    /// Returns true if an active segment on this tier spans station Z.
    /// </summary>
    public bool GetDeckEdgePointsAtZ(int tierIndex, float z, out Vector3 stbdEdge, out Vector3 portEdge, out Vector3 stbdBottomEdge, out Vector3 portBottomEdge)
    {
        stbdEdge = Vector3.zero;
        portEdge = Vector3.zero;
        stbdBottomEdge = Vector3.zero;
        portBottomEdge = Vector3.zero;

        EnsureInitialized();
        if (tiers == null || tierIndex < 0 || tierIndex >= tiers.Count) return false;
        UpperDeckTier tier = tiers[tierIndex];
        if (tier == null || tier.segments == null) return false;

        float bulwarkSternZ = GetBulwarkTransomLimitZ(tierIndex);
        float bulwarkBowZ = GetBulwarkStemLimitZ(tierIndex);

        for (int s = 0; s < tier.segments.Count; s++)
        {
            UpperDeckSegment seg = tier.segments[s];
            if (seg == null || !seg.isEnabled) continue;

            float fwdLimit = seg.flushToStem ? Mathf.Max(seg.forwardZ, bulwarkBowZ) : seg.forwardZ;
            float aftLimit = seg.flushToTransom ? Mathf.Min(seg.aftZ, bulwarkSternZ) : seg.aftZ;

            if (z <= fwdLimit + 0.001f && z >= aftLimit - 0.001f)
            {
                float centerY = EvaluateCenterlineSheerYAtZ(tier, seg, tierIndex, z);
                float camber = EvaluateCamberAtZ(tier, seg, z);
                float edgeY = centerY - camber;
                float halfWidth = GetInnerHullHalfBreadthAt(edgeY, z);

                stbdEdge = new Vector3(halfWidth, edgeY, z);
                portEdge = new Vector3(-halfWidth, edgeY, z);

                float bottomCenterY = centerY - seg.deckThickness;
                float bottomEdgeY = bottomCenterY - camber;
                float bottomHalfWidth = GetInnerHullHalfBreadthAt(bottomEdgeY, z);

                stbdBottomEdge = new Vector3(bottomHalfWidth, bottomEdgeY, z);
                portBottomEdge = new Vector3(-bottomHalfWidth, bottomEdgeY, z);
                return true;
            }
        }

        return false;
    }

    // --- BULWARK CAP RAIL GEOMETRY & QUERY API ---

    public bool HasAnyActiveTier()
    {
        if (tiers == null) return false;
        for (int t = 0; t < tiers.Count; t++)
        {
            if (tiers[t] != null && tiers[t].segments != null)
            {
                for (int s = 0; s < tiers[t].segments.Count; s++)
                {
                    if (tiers[t].segments[s] != null && tiers[t].segments[s].isEnabled)
                        return true;
                }
            }
        }
        return false;
    }

    public float GetTotalBulwarkHeightAboveTopLine()
    {
        return 0f;
    }

    public float GetTotalBulwarkHeightAtTier(int maxTierIndex)
    {
        return 0f;
    }

    /// <summary>
    /// Gets the furthest aft Z boundary of the bulwark cap rail for specified tier.
    /// This corresponds to the TopLineCurves aft extremity.
    /// </summary>
    public float GetBulwarkTransomLimitZ(int tierIndex)
    {
        return (topLineCurves != null) ? (topLineCurves.cachedIsAftCubic ? topLineCurves.cachedAftP3.z : topLineCurves.cachedAftP2.z) : -13.5f;
    }

    public float GetBulwarkStemLimitZ(int tierIndex)
    {
        if (tiers != null && tierIndex >= 0 && tierIndex < tiers.Count)
        {
            UpperDeckTier tier = tiers[tierIndex];
            if (tier != null && tier.cachedBulwarkRailPts != null && tier.cachedBulwarkRailPts.Length > 0)
            {
                return tier.cachedBulwarkRailPts[0].z;
            }
        }

        return (sheerCurves != null) ? sheerCurves.stemP3.z : ((topLineCurves != null) ? topLineCurves.cachedFwdP0.z : 15f);
    }

    public bool DoesSegmentSpanZ(int tierIndex, float z)
    {
        if (tiers == null || tierIndex < 0 || tierIndex >= tiers.Count) return false;
        UpperDeckTier tier = tiers[tierIndex];
        if (tier == null || tier.segments == null) return false;

        float bulwarkSternZ = GetBulwarkTransomLimitZ(tierIndex);
        float bulwarkBowZ = GetBulwarkStemLimitZ(tierIndex);
        return DoesSegmentSpanZ(tier, z, bulwarkBowZ, bulwarkSternZ);
    }

    public bool DoesSegmentSpanZ(UpperDeckTier tier, float z, float bulwarkBowZ, float bulwarkSternZ)
    {
        if (tier == null || tier.segments == null) return false;

        for (int s = 0; s < tier.segments.Count; s++)
        {
            UpperDeckSegment seg = tier.segments[s];
            if (seg != null && seg.isEnabled)
            {
                float fwdLimit = seg.flushToStem ? Mathf.Max(seg.forwardZ, bulwarkBowZ) : seg.forwardZ;
                float aftLimit = seg.flushToTransom ? Mathf.Min(seg.aftZ, bulwarkSternZ) : seg.aftZ;

                if (z <= fwdLimit + 0.001f && z >= aftLimit - 0.001f)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Evaluates the 3D Starboard Outer and Inner Bulwark Cap Rail points at station Z for specified tier.
    /// Uses high-resolution piecewise interpolation along the cached unified parallel rail curves.
    /// </summary>
    public bool GetBulwarkRailPointsAtZ(int tierIndex, float z, out Vector3 stbdRail, out Vector3 innerStbdRail)
    {
        stbdRail = Vector3.zero;
        innerStbdRail = Vector3.zero;

        if (tiers == null || tierIndex < 0 || tierIndex >= tiers.Count) return false;
        UpperDeckTier tier = tiers[tierIndex];
        if (tier == null || !tier.showBulwark || tier.cachedBulwarkRailPts == null || tier.cachedBulwarkRailPts.Length < 2) return false;

        Vector3[] outerPts = tier.cachedBulwarkRailPts;
        Vector3[] innerPts = tier.cachedInnerBulwarkRailPts;

        float maxBowZ = outerPts[0].z;
        float minSternZ = outerPts[outerPts.Length - 1].z;
        float spanZ = maxBowZ - minSternZ;
        if (spanZ < 0.001f) return false;

        float tNorm = Mathf.Clamp01((maxBowZ - z) / spanZ);
        float fIndex = tNorm * (outerPts.Length - 1);
        int i0 = Mathf.Clamp(Mathf.FloorToInt(fIndex), 0, outerPts.Length - 2);
        int i1 = i0 + 1;
        float u = fIndex - i0;

        stbdRail = Vector3.Lerp(outerPts[i0], outerPts[i1], u);
        innerStbdRail = (innerPts != null && innerPts.Length == outerPts.Length)
            ? Vector3.Lerp(innerPts[i0], innerPts[i1], u)
            : stbdRail;

        return true;
    }

    public bool HasBulwarkAtZ(float z, out Vector3 stbdRail, out Vector3 portRail, out Vector3 innerStbdRail, out Vector3 innerPortRail)
    {
        stbdRail = Vector3.zero;
        portRail = Vector3.zero;
        innerStbdRail = Vector3.zero;
        innerPortRail = Vector3.zero;

        if (tiers == null || tiers.Count == 0) return false;

        bool found = false;
        float highestRailY = float.MinValue;
        float railX = 0f;
        float inRailX = 0f;

        for (int t = 0; t < tiers.Count; t++)
        {
            UpperDeckTier tier = tiers[t];
            if (tier == null || tier.segments == null) continue;

            float bulwarkSternZ = GetBulwarkTransomLimitZ(t);
            float bulwarkBowZ = GetBulwarkStemLimitZ(t);

            for (int s = 0; s < tier.segments.Count; s++)
            {
                UpperDeckSegment seg = tier.segments[s];
                if (seg == null || !seg.isEnabled) continue;

                float fwdLimit = seg.flushToStem ? Mathf.Max(seg.forwardZ, bulwarkBowZ) : seg.forwardZ;
                float aftLimit = seg.flushToTransom ? Mathf.Min(seg.aftZ, bulwarkSternZ) : seg.aftZ;

                if (z <= fwdLimit + 0.001f && z >= aftLimit - 0.001f)
                {
                    if (GetBulwarkRailPointsAtZ(t, z, out Vector3 curOuter, out Vector3 curInner))
                    {
                        if (curOuter.y > highestRailY)
                        {
                            found = true;
                            highestRailY = curOuter.y;
                            railX = curOuter.x;
                            inRailX = curInner.x;
                        }
                    }
                    else
                    {
                        found = true;
                        Vector3 tlPt = (topLineCurves != null) ? topLineCurves.GetTopLinePointAtZ(z) : Vector3.zero;
                        highestRailY = tlPt.y;
                        railX = tlPt.x;
                        float spacing = (bodyCurves != null) ? bodyCurves.topInnerLineSpacing : 0.10f;
                        inRailX = Mathf.Max(0.01f, railX - spacing);
                    }
                }
            }
        }

        // Fallback to Main Deck Waist Bulwark across the open waist
        if (!found && showWaistBulwark && GetWaistExtents(out float waistFwdZ, out float waistAftZ))
        {
            if (z <= waistFwdZ + 0.001f && z >= waistAftZ - 0.001f && mainDeckCurves != null)
            {
                float floorY = mainDeckCurves.GetDeckSurfacePoint(0, z).y;
                float camber = mainDeckCurves.EvaluateDeckCamberAtZ(z);
                float edgeY = floorY - camber;
                float railY = edgeY + waistBulwarkHeight;
                float x = GetInnerHullHalfBreadthAt(railY, z);
                float inX = Mathf.Max(0.01f, x - waistBulwarkThickness);

                stbdRail = new Vector3(x, railY, z);
                portRail = new Vector3(-x, railY, z);
                innerStbdRail = new Vector3(inX, railY, z);
                innerPortRail = new Vector3(-inX, railY, z);
                return true;
            }
        }

        if (found)
        {
            stbdRail = new Vector3(railX, highestRailY, z);
            portRail = new Vector3(-railX, highestRailY, z);
            innerStbdRail = new Vector3(inRailX, highestRailY, z);
            innerPortRail = new Vector3(-inRailX, highestRailY, z);
            return true;
        }

        return false;
    }

    public int GetMaxActiveTierLevel()
    {
        int maxActive = 1;
        if (tiers == null) return maxActive;
        for (int t = 0; t < tiers.Count; t++)
        {
            if (tiers[t] == null || tiers[t].segments == null) continue;
            for (int s = 0; s < tiers[t].segments.Count; s++)
            {
                if (tiers[t].segments[s] != null && tiers[t].segments[s].isEnabled)
                {
                    if (tiers[t].tierLevel > maxActive)
                        maxActive = tiers[t].tierLevel;
                }
            }
        }
        return maxActive;
    }

    public UpperDeckSegment GetSegmentAtZ(UpperDeckTier tier, float z)
    {
        if (tier == null || tier.segments == null) return null;
        for (int s = 0; s < tier.segments.Count; s++)
        {
            UpperDeckSegment seg = tier.segments[s];
            if (seg != null && seg.isEnabled)
            {
                if (z <= seg.forwardZ + 0.05f && z >= seg.aftZ - 0.05f)
                    return seg;
            }
        }
        return null;
    }

    /// <summary>
    /// Evaluates whether a physical upper deck bulwark exists at longitudinal station Z on the highest active tier.
    /// Called by TopLineCurves to render solid lines over active deck bulwarks and dashed lines over open air spans.
    /// </summary>
    public bool IsHighestPhysicalDeckAtZ(float z)
    {
        if (tiers == null || tiers.Count == 0) return true;

        int maxActiveTier = GetMaxActiveTierLevel();
        bool hasAnySegment = false;
        for (int t = 0; t < tiers.Count; t++)
        {
            if (tiers[t] != null && tiers[t].segments != null)
            {
                for (int s = 0; s < tiers[t].segments.Count; s++)
                {
                    if (tiers[t].segments[s] != null && tiers[t].segments[s].isEnabled)
                    {
                        hasAnySegment = true;
                        break;
                    }
                }
            }
            if (hasAnySegment) break;
        }
        if (!hasAnySegment) return true; // No upper decks -> TopLine is Main Deck bulwark (solid)

        // Check if any segment on the supreme tier covers z
        for (int t = 0; t < tiers.Count; t++)
        {
            UpperDeckTier tier = tiers[t];
            if (tier == null || tier.tierLevel < maxActiveTier || tier.segments == null) continue;

            for (int s = 0; s < tier.segments.Count; s++)
            {
                UpperDeckSegment seg = tier.segments[s];
                if (seg != null && seg.isEnabled)
                {
                    if (z <= seg.forwardZ + 0.05f && z >= seg.aftZ - 0.05f)
                        return true;
                }
            }
        }

        // If maxActiveTier >= 2 (e.g. Poop Deck at stern), Forecastle on Tier 1 at the bow is also crowned by TopLineCurves at the bow!
        if (maxActiveTier >= 2)
        {
            for (int t = 0; t < tiers.Count; t++)
            {
                UpperDeckTier tier = tiers[t];
                if (tier == null || tier.tierLevel != 1 || tier.segments == null) continue;

                for (int s = 0; s < tier.segments.Count; s++)
                {
                    UpperDeckSegment seg = tier.segments[s];
                    if (seg != null && seg.isEnabled && seg.forwardZ > 0f) // Forecastle
                    {
                        if (z <= seg.forwardZ + 0.05f && z >= seg.aftZ - 0.05f)
                            return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Computes the forward and aft extents of the Main Deck open waist between upper deck breaks.
    /// Returns true if an open waist exists, or false if upper decks cover the full length.
    /// </summary>
    public bool GetWaistExtents(out float waistFwdZ, out float waistAftZ)
    {
        waistFwdZ = 0f;
        waistAftZ = 0f;
        if (tiers == null || tiers.Count == 0) return false;

        UpperDeckTier tier1 = null;
        for (int t = 0; t < tiers.Count; t++)
        {
            if (tiers[t].tierLevel == 1) { tier1 = tiers[t]; break; }
        }
        if (tier1 == null || tier1.segments == null || tier1.segments.Count == 0) return false;

        float midZ = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.z : 0f;
        float maxBowZ = (topLineCurves != null) ? topLineCurves.cachedFwdP0.z : 14f;
        float minSternZ = (topLineCurves != null) ? (topLineCurves.cachedIsAftCubic ? topLineCurves.cachedAftP3.z : topLineCurves.cachedAftP2.z) : -13.5f;

        UpperDeckSegment fwdSeg = null;
        UpperDeckSegment aftSeg = null;

        for (int s = 0; s < tier1.segments.Count; s++)
        {
            UpperDeckSegment seg = tier1.segments[s];
            if (seg == null || !seg.isEnabled) continue;

            if (seg.forwardZ > midZ)
            {
                if (fwdSeg == null || seg.aftZ < fwdSeg.aftZ)
                    fwdSeg = seg;
            }
            if (seg.aftZ < midZ)
            {
                if (aftSeg == null || seg.forwardZ > aftSeg.forwardZ)
                    aftSeg = seg;
            }
        }

        if (fwdSeg != null && aftSeg != null)
        {
            waistFwdZ = fwdSeg.aftZ;
            waistAftZ = aftSeg.forwardZ;
            return waistFwdZ > waistAftZ + 0.1f;
        }
        else if (fwdSeg != null && aftSeg == null)
        {
            waistFwdZ = fwdSeg.aftZ;
            waistAftZ = minSternZ;
            return waistFwdZ > waistAftZ + 0.1f;
        }
        else if (fwdSeg == null && aftSeg != null)
        {
            waistFwdZ = maxBowZ;
            waistAftZ = aftSeg.forwardZ;
            return waistFwdZ > waistAftZ + 0.1f;
        }

        return false;
    }

    /// <summary>
    /// Dynamically synchronizes TopLineCurves midship elevation (and BodyCurves P4), transom rake height,
    /// and bow upper stem height to crown the highest active upper deck tier (e.g. Poop Deck at Tier 2).
    /// </summary>
    public void SynchronizeTopLineElevation()
    {
        if (!syncTopLineToHighestDeck || sheerCurves == null) return;

        // 1. Identify highest active tier
        int highestActiveTierIdx = -1;
        UpperDeckTier highestActiveTier = null;

        if (tiers != null)
        {
            for (int t = 0; t < tiers.Count; t++)
            {
                UpperDeckTier tier = tiers[t];
                if (tier == null || tier.segments == null) continue;
                for (int s = 0; s < tier.segments.Count; s++)
                {
                    UpperDeckSegment seg = tier.segments[s];
                    if (seg != null && seg.isEnabled)
                    {
                        if (t > highestActiveTierIdx)
                        {
                            highestActiveTierIdx = t;
                            highestActiveTier = tier;
                        }
                    }
                }
            }
        }

        bool hasActiveUpperDeck = (highestActiveTierIdx >= 0 && highestActiveTier != null);

        // 2. Fallback / restoration if no upper decks are active
        if (!hasActiveUpperDeck)
        {
            bool fallbackChanged = false;
            if (baseTopLineMidshipY > 0f && topLineCurves != null && Mathf.Abs(topLineCurves.fwdP3.y - baseTopLineMidshipY) > 0.005f)
            {
                topLineCurves.fwdP3.y = baseTopLineMidshipY;
                topLineCurves.fwdP2.y = baseTopLineMidshipY;
                topLineCurves.aftP1.y = baseTopLineMidshipY;
                fallbackChanged = true;
            }
            if (baseTransomRakeHeight > 0f && Mathf.Abs(sheerCurves.transomRakeHeight - baseTransomRakeHeight) > 0.005f)
            {
                sheerCurves.transomRakeHeight = baseTransomRakeHeight;
                fallbackChanged = true;
            }
            if (sheerCurves.targetUpperStemTopY > 0f)
            {
                sheerCurves.targetUpperStemTopY = -1f;
                fallbackChanged = true;
            }
            if (fallbackChanged)
            {
                sheerCurves.UpdateCachedProfile();
                if (topLineCurves != null) topLineCurves.UpdateCachedCurves();
                if (fashionCurves != null) fashionCurves.UpdateCachedCurves();
                if (bodyCurves != null) bodyCurves.UpdateCachedCurves();
            }
            return;
        }

        // 3. Cache baseline values on first elevation
        if (baseTopLineMidshipY <= 0f && topLineCurves != null)
            baseTopLineMidshipY = topLineCurves.fwdP3.y;
        if (baseTransomRakeHeight <= 0f && sheerCurves != null)
            baseTransomRakeHeight = sheerCurves.transomRakeHeight;
        if (baseUpperStemRatio < 0f && sheerCurves != null)
            baseUpperStemRatio = sheerCurves.upperStemRatio;

        // 4. Calculate target Midship Apex Y (supreme deck bulwark height at midships)
        float targetMidshipY = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.y : 3.5f;
        for (int t = 0; t <= highestActiveTierIdx; t++)
        {
            UpperDeckTier tier = tiers[t];
            if (tier == null) continue;
            float headroom = tier.tierHeadroom;
            if (tier.segments != null)
            {
                for (int s = 0; s < tier.segments.Count; s++)
                {
                    UpperDeckSegment seg = tier.segments[s];
                    if (seg != null && seg.isEnabled && seg.overrideTierHeight)
                    {
                        headroom = Mathf.Max(headroom, seg.customHeadroom);
                    }
                }
            }
            targetMidshipY += headroom;
        }
        targetMidshipY += highestActiveTier.bulwarkHeight;

        // 5. Calculate target Stern Transom Y
        float midZ = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.z : 0f;
        float defaultRakeHeight = (baseTransomRakeHeight > 0f) ? baseTransomRakeHeight : 2.5f;
        float highestSternCapRailY = float.MinValue;
        bool foundSternDeck = false;

        for (int t = 0; t <= highestActiveTierIdx; t++)
        {
            UpperDeckTier tier = tiers[t];
            if (tier == null || tier.segments == null) continue;

            for (int s = 0; s < tier.segments.Count; s++)
            {
                UpperDeckSegment seg = tier.segments[s];
                if (seg == null || !seg.isEnabled) continue;

                if (seg.flushToTransom || seg.aftZ < midZ - 2f)
                {
                    float deckY = EvaluateCenterlineSheerYAtZ(tier, seg, t, seg.aftZ);
                    float capRailY = deckY + tier.bulwarkHeight;
                    if (capRailY > highestSternCapRailY)
                    {
                        highestSternCapRailY = capRailY;
                        foundSternDeck = true;
                    }
                }
            }
        }

        float sternZ = (sheerCurves != null) ? sheerCurves.sternP3.z : -15f;
        float mainSheerRiseStern = (mainDeckCurves != null)
            ? Mathf.Max(0f, mainDeckCurves.EvaluateCenterlineSheerYAtZ(sternZ) - mainDeckCurves.midshipCenter.y)
            : 1.5f;
        float sternSheerY = targetMidshipY + mainSheerRiseStern * highestActiveTier.sheerScale;
        float targetSternY = foundSternDeck ? Mathf.Max(highestSternCapRailY, sternSheerY) : sternSheerY;
        targetSternY = Mathf.Max(targetSternY, targetMidshipY);

        float counterY = sheerCurves.transomP0.y;
        float targetRakeHeight = Mathf.Max(defaultRakeHeight, targetSternY - counterY);

        // 6. Calculate target Bow Stem Y
        float highestBowCapRailY = float.MinValue;
        bool foundBowDeck = false;

        for (int t = 0; t <= highestActiveTierIdx; t++)
        {
            UpperDeckTier tier = tiers[t];
            if (tier == null || tier.segments == null) continue;

            for (int s = 0; s < tier.segments.Count; s++)
            {
                UpperDeckSegment seg = tier.segments[s];
                if (seg == null || !seg.isEnabled) continue;

                if (seg.flushToStem || seg.forwardZ > midZ + 2f)
                {
                    float deckY = EvaluateCenterlineSheerYAtZ(tier, seg, t, seg.forwardZ);
                    float capRailY = deckY + tier.bulwarkHeight;
                    if (capRailY > highestBowCapRailY)
                    {
                        highestBowCapRailY = capRailY;
                        foundBowDeck = true;
                    }
                }
            }
        }

        float bowZ = (sheerCurves != null) ? sheerCurves.stemP3.z : 15f;
        float mainSheerRiseBow = (mainDeckCurves != null)
            ? Mathf.Max(0f, mainDeckCurves.EvaluateCenterlineSheerYAtZ(bowZ) - mainDeckCurves.midshipCenter.y)
            : 1.8f;
        float bowSheerY = targetMidshipY + mainSheerRiseBow * highestActiveTier.sheerScale;
        float targetBowY = foundBowDeck ? Mathf.Max(highestBowCapRailY, bowSheerY) : bowSheerY;
        targetBowY = Mathf.Max(targetBowY, targetMidshipY);

        // 7. Apply updates and propagate changes across curve hierarchy
        bool changed = false;

        if (Mathf.Abs(sheerCurves.transomRakeHeight - targetRakeHeight) > 0.005f)
        {
            sheerCurves.transomRakeHeight = targetRakeHeight;
            changed = true;
        }

        if (Mathf.Abs(sheerCurves.targetUpperStemTopY - targetBowY) > 0.005f)
        {
            sheerCurves.targetUpperStemTopY = targetBowY;
            changed = true;
        }

        if (topLineCurves != null && Mathf.Abs(topLineCurves.fwdP3.y - targetMidshipY) > 0.005f)
        {
            topLineCurves.fwdP3.y = targetMidshipY;
            topLineCurves.fwdP2.y = targetMidshipY;
            topLineCurves.aftP1.y = targetMidshipY;
            changed = true;
        }

        if (changed)
        {
            sheerCurves.UpdateCachedProfile();
            if (topLineCurves != null) topLineCurves.UpdateCachedCurves();
            if (fashionCurves != null) fashionCurves.UpdateCachedCurves();
            if (bodyCurves != null) bodyCurves.UpdateCachedCurves();
        }
    }

    /// <summary>
    /// Builds 3D geometric polylines and vertical framing for the Main Deck open waist bulwark.
    /// </summary>
    public void BuildWaistBulwark()
    {
        if (!showWaistBulwark || !GetWaistExtents(out float waistFwdZ, out float waistAftZ) || mainDeckCurves == null)
        {
            cachedWaistRailPts = null;
            cachedInnerWaistRailPts = null;
            if (cachedWaistStanchions != null) cachedWaistStanchions.Clear();
            waistMidshipHandlePt = Vector3.zero;
            return;
        }

        mainDeckCurves.EnsureInitialized();
        const int steps = 32;
        int totalPts = steps + 1;
        if (cachedWaistRailPts == null || cachedWaistRailPts.Length != totalPts)
        {
            cachedWaistRailPts = new Vector3[totalPts];
            cachedInnerWaistRailPts = new Vector3[totalPts];
        }
        if (cachedWaistStanchions == null) cachedWaistStanchions = new List<Vector3[]>();
        cachedWaistStanchions.Clear();

        for (int i = 0; i <= steps; i++)
        {
            float frac = (float)i / steps;
            float z = Mathf.Lerp(waistFwdZ, waistAftZ, frac);

            float floorY = mainDeckCurves.GetDeckSurfacePoint(0, z).y;
            float camber = mainDeckCurves.EvaluateDeckCamberAtZ(z);
            float edgeY = floorY - camber;

            float railY = edgeY + waistBulwarkHeight;
            float railX = GetInnerHullHalfBreadthAt(railY, z);
            float inRailX = Mathf.Max(0.01f, railX - waistBulwarkThickness);

            cachedWaistRailPts[i] = new Vector3(railX, railY, z);
            cachedInnerWaistRailPts[i] = new Vector3(inRailX, railY, z);
        }

        float waistMidZ = (waistFwdZ + waistAftZ) * 0.5f;
        float midFloorY = mainDeckCurves.GetDeckSurfacePoint(0, waistMidZ).y;
        float midCamber = mainDeckCurves.EvaluateDeckCamberAtZ(waistMidZ);
        float midEdgeY = midFloorY - midCamber;
        float midRailY = midEdgeY + waistBulwarkHeight;
        float midRailX = GetInnerHullHalfBreadthAt(midRailY, waistMidZ);
        waistMidshipHandlePt = new Vector3(midRailX, midRailY, waistMidZ);

        int count = Mathf.Max(2, waistStanchionCount);
        for (int j = 0; j <= count; j++)
        {
            float frac = (float)j / count;
            float zSt = Mathf.Lerp(waistFwdZ, waistAftZ, frac);

            float floorY = mainDeckCurves.GetDeckSurfacePoint(0, zSt).y;
            float camber = mainDeckCurves.EvaluateDeckCamberAtZ(zSt);
            float edgeY = floorY - camber;
            float edgeX = GetInnerHullHalfBreadthAt(edgeY, zSt);

            float railY = edgeY + waistBulwarkHeight;
            float railX = GetInnerHullHalfBreadthAt(railY, zSt);

            Vector3 botPt = new Vector3(edgeX, edgeY, zSt);
            Vector3 topPt = new Vector3(railX, railY, zSt);

            cachedWaistStanchions.Add(new Vector3[] { botPt, topPt });
        }
    }

    public void DrawWaistBulwark()
    {
        if (!showWaistBulwark || cachedWaistRailPts == null || cachedWaistRailPts.Length < 2) return;

        Gizmos.color = waistBulwarkColor;
        for (int i = 0; i < cachedWaistRailPts.Length - 1; i++)
        {
            Vector3 p0 = cachedWaistRailPts[i];
            Vector3 p1 = cachedWaistRailPts[i + 1];
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(MirrorX(p0), MirrorX(p1));
        }

        if (cachedInnerWaistRailPts != null)
        {
            Gizmos.color = new Color(waistBulwarkColor.r, waistBulwarkColor.g * 0.8f, waistBulwarkColor.b, 0.85f);
            for (int i = 0; i < cachedInnerWaistRailPts.Length - 1; i++)
            {
                Vector3 p0 = cachedInnerWaistRailPts[i];
                Vector3 p1 = cachedInnerWaistRailPts[i + 1];
                Gizmos.DrawLine(p0, p1);
                Gizmos.DrawLine(MirrorX(p0), MirrorX(p1));
            }

            Vector3 fwdOut = cachedWaistRailPts[0];
            Vector3 fwdIn = cachedInnerWaistRailPts[0];
            Gizmos.DrawLine(fwdOut, fwdIn);
            Gizmos.DrawLine(MirrorX(fwdOut), MirrorX(fwdIn));

            Vector3 aftOut = cachedWaistRailPts[cachedWaistRailPts.Length - 1];
            Vector3 aftIn = cachedInnerWaistRailPts[cachedInnerWaistRailPts.Length - 1];
            Gizmos.DrawLine(aftOut, aftIn);
            Gizmos.DrawLine(MirrorX(aftOut), MirrorX(aftIn));
        }

        if (cachedWaistStanchions != null)
        {
            Gizmos.color = waistStanchionColor;
            for (int j = 0; j < cachedWaistStanchions.Count; j++)
            {
                Vector3[] st = cachedWaistStanchions[j];
                if (st != null && st.Length >= 2)
                {
                    Gizmos.DrawLine(st[0], st[1]);
                    Gizmos.DrawLine(MirrorX(st[0]), MirrorX(st[1]));
                }
            }
        }
    }

    private void BuildTierBulwarks()
    {
        int maxActiveTier = GetMaxActiveTierLevel();

        for (int t = 0; t < tiers.Count; t++)
        {
            UpperDeckTier tier = tiers[t];
            if (tier == null) continue;

            float maxBowZ = (topLineCurves != null) ? topLineCurves.cachedFwdP0.z : 14f;
            float minSternZ = (topLineCurves != null) ? (topLineCurves.cachedIsAftCubic ? topLineCurves.cachedAftP3.z : topLineCurves.cachedAftP2.z) : -13.5f;

            const int steps = 64;
            int totalPts = steps + 1;
            if (tier.cachedBulwarkRailPts == null || tier.cachedBulwarkRailPts.Length != totalPts)
            {
                tier.cachedBulwarkRailPts = new Vector3[totalPts];
                tier.cachedInnerBulwarkRailPts = new Vector3[totalPts];
            }
            if (tier.cachedBulwarkStanchions == null)
            {
                tier.cachedBulwarkStanchions = new List<Vector3[]>();
            }
            tier.cachedBulwarkStanchions.Clear();

            bool isSupremeTier = (tier.tierLevel >= maxActiveTier);

            for (int i = 0; i <= steps; i++)
            {
                float frac = (float)i / steps;
                float z = Mathf.Lerp(maxBowZ, minSternZ, frac);

                if (isSupremeTier)
                {
                    // Supreme tier rail conforms to TopLineCurves
                    Vector3 tlPt = (topLineCurves != null) ? topLineCurves.GetTopLinePointAtZ(z) : new Vector3(2f, 6.3f, z);
                    Vector3 inTlPt = (topLineCurves != null && topLineCurves.showInnerLine) ? topLineCurves.GetInnerTopLinePointAtZ(z, true) : tlPt;

                    tier.cachedBulwarkRailPts[i] = tlPt;
                    tier.cachedInnerBulwarkRailPts[i] = inTlPt;
                }
                else
                {
                    // Intermediate tier rail: deck waterway Y + bulwarkHeight, sampled along transverse frames
                    UpperDeckSegment seg = GetSegmentAtZ(tier, z);
                    float deckEdgeY;
                    if (seg != null)
                    {
                        float centerY = EvaluateCenterlineSheerYAtZ(tier, seg, t, z);
                        float camber = EvaluateCamberAtZ(tier, seg, z);
                        deckEdgeY = centerY - camber;
                    }
                    else
                    {
                        float mainY = (mainDeckCurves != null) ? mainDeckCurves.GetDeckSurfacePoint(0, z).y : 3f;
                        deckEdgeY = mainY + tier.tierHeadroom;
                    }

                    float railY = deckEdgeY + tier.bulwarkHeight;
                    float railX = GetInnerHullHalfBreadthAt(railY, z);
                    float spacing = (bodyCurves != null) ? bodyCurves.topInnerLineSpacing : 0.10f;
                    float inRailX = Mathf.Max(0.01f, railX - spacing);

                    tier.cachedBulwarkRailPts[i] = new Vector3(railX, railY, z);
                    tier.cachedInnerBulwarkRailPts[i] = new Vector3(inRailX, railY, z);
                }
            }

            // Build vertical stanchions along active segments
            int stanchionIdx = 0;
            if (tier.segments != null)
            {
                for (int s = 0; s < tier.segments.Count; s++)
                {
                    UpperDeckSegment seg = tier.segments[s];
                    if (seg == null || !seg.isEnabled) continue;

                    float segFwd = seg.forwardZ;
                    float segAft = seg.aftZ;

                    int count = Mathf.Max(2, tier.bulwarkStanchionCount);
                    for (int j = 0; j <= count; j++)
                    {
                        if (seg.flushToTransom && j == count) continue;
                        if (seg.flushToStem && j == 0) continue;

                        float frac = (float)j / count;
                        float zStanchion = Mathf.Lerp(segFwd, segAft, frac);

                        float deckCenterY = EvaluateCenterlineSheerYAtZ(tier, seg, t, zStanchion);
                        float camber = EvaluateCamberAtZ(tier, seg, zStanchion);
                        float deckEdgeY = deckCenterY - camber;
                        float deckEdgeX = GetInnerHullHalfBreadthAt(deckEdgeY, zStanchion);

                        Vector3 bottomPt = new Vector3(deckEdgeX, deckEdgeY, zStanchion);
                        Vector3 topPt;

                        if (isSupremeTier)
                        {
                            topPt = (topLineCurves != null) ? topLineCurves.GetTopLinePointAtZ(zStanchion) : new Vector3(deckEdgeX, deckEdgeY + tier.bulwarkHeight, zStanchion);
                        }
                        else
                        {
                            float railY = deckEdgeY + tier.bulwarkHeight;
                            float railX = GetInnerHullHalfBreadthAt(railY, zStanchion);
                            topPt = new Vector3(railX, railY, zStanchion);
                        }

                        if (topPt.y - bottomPt.y < 0.15f) continue;

                        Vector3[] pair;
                        if (stanchionIdx < tier.cachedBulwarkStanchions.Count && tier.cachedBulwarkStanchions[stanchionIdx] != null && tier.cachedBulwarkStanchions[stanchionIdx].Length == 2)
                        {
                            pair = tier.cachedBulwarkStanchions[stanchionIdx];
                            pair[0] = bottomPt;
                            pair[1] = topPt;
                        }
                        else
                        {
                            pair = new Vector3[] { bottomPt, topPt };
                            if (stanchionIdx < tier.cachedBulwarkStanchions.Count)
                                tier.cachedBulwarkStanchions[stanchionIdx] = pair;
                            else
                                tier.cachedBulwarkStanchions.Add(pair);
                        }
                        stanchionIdx++;
                    }
                }
            }
            while (tier.cachedBulwarkStanchions.Count > stanchionIdx)
                tier.cachedBulwarkStanchions.RemoveAt(tier.cachedBulwarkStanchions.Count - 1);
        }
    }

    // --- GIZMO RENDERING ---

    private void OnDrawGizmos()
    {
        if (tiers == null || tiers.Count == 0) return;

        for (int t = 0; t < tiers.Count; t++)
        {
            UpperDeckTier tier = tiers[t];
            if (tier == null) continue;

            DrawTierBulwarks(tier, t);

            if (tier.segments == null) continue;
            for (int s = 0; s < tier.segments.Count; s++)
            {
                UpperDeckSegment seg = tier.segments[s];
                if (seg == null || !seg.isEnabled) continue;

                DrawSegmentGizmos(tier, seg, t);
            }
        }

        DrawWaistBulwark();
    }

    private Vector3 MirrorX(Vector3 v)
    {
        return new Vector3(-v.x, v.y, v.z);
    }

    private void DrawTierBulwarks(UpperDeckTier tier, int tierIndex)
    {
        if (!tier.showBulwark || tier.cachedBulwarkRailPts == null || tier.cachedBulwarkRailPts.Length < 2) return;

        int maxActiveTier = GetMaxActiveTierLevel();
        bool isSupremeTier = (tier.tierLevel >= maxActiveTier);

        int count = tier.cachedBulwarkRailPts.Length;
        float bulwarkSternZ = tier.cachedBulwarkRailPts[count - 1].z;
        float bulwarkBowZ = tier.cachedBulwarkRailPts[0].z;

        // Supreme tier bulwark rail is drawn by TopLineCurves (solid over active deck, dashed over open air).
        // Intermediate tiers draw their intermediate bulwark rail here.
        bool shouldDrawRail = !isSupremeTier || (topLineCurves == null || !topLineCurves.gameObject.activeInHierarchy);

        if (shouldDrawRail)
        {
            for (int i = 0; i < count - 1; i++)
            {
                Vector3 ptA = tier.cachedBulwarkRailPts[i];
                Vector3 ptB = tier.cachedBulwarkRailPts[i + 1];
                Vector3 inPtA = tier.cachedInnerBulwarkRailPts[i];
                Vector3 inPtB = tier.cachedInnerBulwarkRailPts[i + 1];

                float zMid = (ptA.z + ptB.z) * 0.5f;
                bool isSolid = DoesSegmentSpanZ(tier, zMid, bulwarkBowZ, bulwarkSternZ);

                if (isSolid)
                {
                    // Solid Cap Rail for active deck spans
                    Gizmos.color = tier.bulwarkRailColor;
                    Gizmos.DrawLine(ptA, ptB);
                    Gizmos.DrawLine(MirrorX(ptA), MirrorX(ptB));

                    // Inner Rail
                    Gizmos.color = new Color(tier.bulwarkRailColor.r, tier.bulwarkRailColor.g * 0.8f, tier.bulwarkRailColor.b, 0.85f);
                    Gizmos.DrawLine(inPtA, inPtB);
                    Gizmos.DrawLine(MirrorX(inPtA), MirrorX(inPtB));
                }
                else
                {
                    // Dashed line outside segment span
#if UNITY_EDITOR
                    Handles.color = new Color(tier.bulwarkRailColor.r, tier.bulwarkRailColor.g, tier.bulwarkRailColor.b, 0.40f);
                    Handles.DrawDottedLine(ptA, ptB, 4f);
                    Handles.DrawDottedLine(MirrorX(ptA), MirrorX(ptB), 4f);

                    Handles.color = new Color(tier.bulwarkRailColor.r, tier.bulwarkRailColor.g * 0.8f, tier.bulwarkRailColor.b, 0.35f);
                    Handles.DrawDottedLine(inPtA, inPtB, 4f);
                    Handles.DrawDottedLine(MirrorX(inPtA), MirrorX(inPtB), 4f);
#endif
                }
            }

            // Draw Transom Closure for intermediate deck
            int lastIdx = count - 1;
            Vector3 lastOut = tier.cachedBulwarkRailPts[lastIdx];
            Vector3 lastIn = tier.cachedInnerBulwarkRailPts[lastIdx];
            float aftZMid = (lastOut.z + tier.cachedBulwarkRailPts[lastIdx - 1].z) * 0.5f;
            bool spansTransom = DoesSegmentSpanZ(tier, lastOut.z, bulwarkBowZ, bulwarkSternZ) || DoesSegmentSpanZ(tier, aftZMid, bulwarkBowZ, bulwarkSternZ);

            if (spansTransom)
            {
                Gizmos.color = tier.bulwarkRailColor;
                Gizmos.DrawLine(lastOut, MirrorX(lastOut));

                Gizmos.color = new Color(tier.bulwarkRailColor.r, tier.bulwarkRailColor.g * 0.8f, tier.bulwarkRailColor.b, 0.85f);
                Gizmos.DrawLine(lastIn, MirrorX(lastIn));

                Gizmos.color = tier.bulwarkRailColor;
                Gizmos.DrawLine(lastOut, lastIn);
                Gizmos.DrawLine(MirrorX(lastOut), MirrorX(lastIn));
            }
        }

        // Draw vertical bulwark framing / stanchions along active spans (connecting deck waterway to cap rail)
        if (tier.cachedBulwarkStanchions != null)
        {
            Gizmos.color = tier.bulwarkStanchionColor;
            for (int j = 0; j < tier.cachedBulwarkStanchions.Count; j++)
            {
                Vector3[] stanchion = tier.cachedBulwarkStanchions[j];
                if (stanchion != null && stanchion.Length >= 2)
                {
                    Gizmos.DrawLine(stanchion[0], stanchion[1]);
                    Gizmos.DrawLine(MirrorX(stanchion[0]), MirrorX(stanchion[1]));
                }
            }
        }
    }

    private void DrawSegmentGizmos(UpperDeckTier tier, UpperDeckSegment seg, int tierIndex)
    {
        // 1. Centerline
        if (seg.showCenterline && seg.cachedCenterlinePts != null)
        {
            Gizmos.color = seg.deckColor;
            for (int i = 0; i < seg.cachedCenterlinePts.Length - 1; i++)
            {
                Gizmos.DrawLine(seg.cachedCenterlinePts[i], seg.cachedCenterlinePts[i + 1]);
            }

            if (seg.showBottomDeck && seg.cachedBottomCenterlinePts != null)
            {
                Gizmos.color = seg.bottomColor;
                for (int i = 0; i < seg.cachedBottomCenterlinePts.Length - 1; i++)
                {
                    Gizmos.DrawLine(seg.cachedBottomCenterlinePts[i], seg.cachedBottomCenterlinePts[i + 1]);
                }
            }
        }

        // 2. Deck Waterways / Edges
        if (seg.showEdges && seg.cachedStbdEdgePts != null)
        {
            Gizmos.color = seg.deckEdgeColor;
            for (int i = 0; i < seg.cachedStbdEdgePts.Length - 1; i++)
            {
                Gizmos.DrawLine(seg.cachedStbdEdgePts[i], seg.cachedStbdEdgePts[i + 1]);
                Gizmos.DrawLine(seg.cachedPortEdgePts[i], seg.cachedPortEdgePts[i + 1]);
            }

            if (seg.showBottomDeck && seg.cachedStbdBottomEdgePts != null)
            {
                Gizmos.color = seg.bottomColor;
                for (int i = 0; i < seg.cachedStbdBottomEdgePts.Length - 1; i++)
                {
                    Gizmos.DrawLine(seg.cachedStbdBottomEdgePts[i], seg.cachedStbdBottomEdgePts[i + 1]);
                    Gizmos.DrawLine(seg.cachedPortBottomEdgePts[i], seg.cachedPortBottomEdgePts[i + 1]);
                }
            }
        }

        // 3. Cambered Deck Beams
        if (seg.showBeams && seg.cachedDeckBeams != null)
        {
            Gizmos.color = seg.beamColor;
            for (int b = 0; b < seg.cachedDeckBeams.Count; b++)
            {
                Vector3[] beam = seg.cachedDeckBeams[b];
                for (int i = 0; i < beam.Length - 1; i++)
                {
                    Gizmos.DrawLine(beam[i], beam[i + 1]);
                }
            }

            if (seg.showBottomDeck && seg.cachedBottomDeckBeams != null)
            {
                Gizmos.color = seg.bottomColor;
                for (int b = 0; b < seg.cachedBottomDeckBeams.Count; b++)
                {
                    Vector3[] beam = seg.cachedBottomDeckBeams[b];
                    for (int i = 0; i < beam.Length - 1; i++)
                    {
                        Gizmos.DrawLine(beam[i], beam[i + 1]);
                    }

                    // Vertical edge caps joining top and bottom beam outer tips along the hull
                    if (seg.showThicknessEdgeCaps && b < seg.cachedDeckBeams.Count)
                    {
                        Vector3[] topBeam = seg.cachedDeckBeams[b];
                        if (topBeam != null && topBeam.Length == beam.Length && topBeam.Length > 0)
                        {
                            Gizmos.DrawLine(topBeam[0], beam[0]); // Port edge cap
                            Gizmos.DrawLine(topBeam[topBeam.Length - 1], beam[beam.Length - 1]); // Starboard edge cap
                        }
                    }
                }
            }
        }

        // 4. End Margin Arcs & Thickness Edge Caps
        if (seg.showEndMargins && seg.fwdEndMarginArc != null)
        {
            Gizmos.color = seg.deckEdgeColor;
            for (int i = 0; i < seg.fwdEndMarginArc.Length - 1; i++)
            {
                Gizmos.DrawLine(seg.fwdEndMarginArc[i], seg.fwdEndMarginArc[i + 1]);
                Gizmos.DrawLine(seg.aftEndMarginArc[i], seg.aftEndMarginArc[i + 1]);
            }

            if (seg.showBottomDeck && seg.bottomFwdEndMarginArc != null)
            {
                Gizmos.color = seg.bottomColor;
                for (int i = 0; i < seg.bottomFwdEndMarginArc.Length - 1; i++)
                {
                    Gizmos.DrawLine(seg.bottomFwdEndMarginArc[i], seg.bottomFwdEndMarginArc[i + 1]);
                    Gizmos.DrawLine(seg.bottomAftEndMarginArc[i], seg.bottomAftEndMarginArc[i + 1]);
                }

                if (seg.showThicknessEdgeCaps)
                {
                    Gizmos.color = seg.bottomColor;
                    Gizmos.DrawLine(seg.fwdCenterTip, seg.fwdBottomCenterTip);
                    Gizmos.DrawLine(seg.aftCenterTip, seg.aftBottomCenterTip);
                    Gizmos.DrawLine(seg.fwdEndMarginArc[0], seg.bottomFwdEndMarginArc[0]);
                    Gizmos.DrawLine(seg.fwdEndMarginArc[seg.fwdEndMarginArc.Length - 1], seg.bottomFwdEndMarginArc[seg.bottomFwdEndMarginArc.Length - 1]);
                    Gizmos.DrawLine(seg.aftEndMarginArc[0], seg.bottomAftEndMarginArc[0]);
                    Gizmos.DrawLine(seg.aftEndMarginArc[seg.aftEndMarginArc.Length - 1], seg.bottomAftEndMarginArc[seg.bottomAftEndMarginArc.Length - 1]);
                }
            }
        }

        // 5. Transverse Breastwork Bulkheads (Facing the open waist)
        if (seg.showForwardBreastwork && seg.forwardBreastworkTopArc != null)
        {
            Gizmos.color = seg.breastworkColor;
            for (int i = 0; i < seg.forwardBreastworkTopArc.Length - 1; i++)
            {
                Gizmos.DrawLine(seg.forwardBreastworkTopArc[i], seg.forwardBreastworkTopArc[i + 1]);
                Gizmos.DrawLine(seg.forwardBreastworkBottomArc[i], seg.forwardBreastworkBottomArc[i + 1]);
            }
            if (seg.forwardBreastworkStanchions != null)
            {
                for (int st = 0; st < seg.forwardBreastworkStanchions.Count; st++)
                {
                    Vector3[] pair = seg.forwardBreastworkStanchions[st];
                    Gizmos.DrawLine(pair[0], pair[1]);
                }
            }
        }

        if (seg.showAftBreastwork && seg.aftBreastworkTopArc != null)
        {
            Gizmos.color = seg.breastworkColor;
            for (int i = 0; i < seg.aftBreastworkTopArc.Length - 1; i++)
            {
                Gizmos.DrawLine(seg.aftBreastworkTopArc[i], seg.aftBreastworkTopArc[i + 1]);
                Gizmos.DrawLine(seg.aftBreastworkBottomArc[i], seg.aftBreastworkBottomArc[i + 1]);
            }
            if (seg.aftBreastworkStanchions != null)
            {
                for (int st = 0; st < seg.aftBreastworkStanchions.Count; st++)
                {
                    Vector3[] pair = seg.aftBreastworkStanchions[st];
                    Gizmos.DrawLine(pair[0], pair[1]);
                }
            }
        }

        // 6. Landmark Spheres
        if (seg.showNodeSpheres)
        {
            Gizmos.color = seg.deckColor;
            Gizmos.DrawSphere(seg.fwdCenterTip, nodeSphereRadius);
            Gizmos.DrawSphere(seg.aftCenterTip, nodeSphereRadius);
            Gizmos.DrawSphere(seg.midshipCenter, nodeSphereRadius);
            Gizmos.DrawSphere(seg.stbdMidshipEdge, nodeSphereRadius * 0.8f);
            Gizmos.DrawSphere(seg.portMidshipEdge, nodeSphereRadius * 0.8f);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(UpperDeckCurves))]
public class UpperDeckCurvesEditor : Editor
{
    private UpperDeckCurves targetScript;

    private void OnEnable()
    {
        targetScript = (UpperDeckCurves)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Upper Deck Curves (Multi-Tier & Superstructures)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "UpperDeckCurves manages multi-tier upper decks (Forecastle, Gangways, Quarterdeck, Poop Deck) " +
            "bounded between MainDeckCurves (floor) and TopLineCurves (ceiling). Higher tier decks are physically supported by lower decks.",
            MessageType.Info);

        // Safeguard Checks
        float remainingSpace = targetScript.GetRemainingUpperSpace(10f, -10f);
        bool hasSufficientClearance = remainingSpace >= 0.68f;
        if (!hasSufficientClearance)
        {
            EditorGUILayout.HelpBox(
                $"⚠️ Low Vertical Clearance: Only {remainingSpace:F2}m clearance between Main Deck and TopLine across the hull. " +
                "Upper deck creation is blocked until vertical clearance is restored (minimum 0.68m required). " +
                "Consider raising TopLineCurves or lowering MainDeckCurves to restore headroom.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(6);
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Inner Boundary Flush Snapping", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("⚡ Snap Forecastle Flush to Stem"))
        {
            Undo.RecordObject(targetScript, "Snap Forecastle Flush to Stem");
            targetScript.SnapForecastleFlushToStem();
            EditorUtility.SetDirty(targetScript);
        }
        if (GUILayout.Button("⚡ Snap Quarterdeck Flush to Transom"))
        {
            Undo.RecordObject(targetScript, "Snap Quarterdeck Flush to Transom");
            targetScript.SnapQuarterdeckFlushToTransom();
            EditorUtility.SetDirty(targetScript);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Hull Layout Presets", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(!hasSufficientClearance);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("⚓ Frigate (2-Deck Layout)", GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog("Apply Frigate Preset?", "Configure standard 2-deck layout (Forecastle, Open Waist, Quarterdeck)?", "Apply", "Cancel"))
            {
                Undo.RecordObject(targetScript, "Apply Frigate Layout Preset");
                targetScript.ApplyFrigatePreset();
                EditorUtility.SetDirty(targetScript);
            }
        }
        if (GUILayout.Button("⚓ Ship-of-the-Line (Multi-Tier)", GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog("Apply Ship-of-the-Line Preset?", "Configure multi-tier layout (Forecastle, Midship Gangway, Quarterdeck, Poop Deck)?", "Apply", "Cancel"))
            {
                Undo.RecordObject(targetScript, "Apply Ship-of-the-Line Layout Preset");
                targetScript.ApplyShipOfTheLinePreset();
                EditorUtility.SetDirty(targetScript);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Quick Presets & Tier Actions", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(!hasSufficientClearance);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Forecastle (Bow)"))
        {
            Undo.RecordObject(targetScript, "Add Forecastle Preset");
            targetScript.AddForecastlePreset();
            EditorUtility.SetDirty(targetScript);
        }
        if (GUILayout.Button("+ Add Quarterdeck (Aft)"))
        {
            Undo.RecordObject(targetScript, "Add Quarterdeck Preset");
            targetScript.AddQuarterdeckPreset();
            EditorUtility.SetDirty(targetScript);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Midship Gangway"))
        {
            Undo.RecordObject(targetScript, "Add Midship Gangway Preset");
            targetScript.AddMidshipCatwalkPreset();
            EditorUtility.SetDirty(targetScript);
        }

        UpperDeckSegment sup;
        bool canAddPoop = targetScript.CanAddTier2Deck(out sup);
        EditorGUI.BeginDisabledGroup(!canAddPoop);
        if (GUILayout.Button("+ Add Poop Deck (Tier 2)"))
        {
            Undo.RecordObject(targetScript, "Add Poop Deck Preset");
            targetScript.AddPoopDeckPreset();
            EditorUtility.SetDirty(targetScript);
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        if (!canAddPoop)
        {
            EditorGUILayout.HelpBox("Poop Deck requires an active Quarterdeck on Tier 1 to act as its supporting deck.", MessageType.None);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Main Deck Waist Bulwark", EditorStyles.boldLabel);
        targetScript.showWaistBulwark = EditorGUILayout.Toggle("Show Waist Bulwark", targetScript.showWaistBulwark);
        if (targetScript.showWaistBulwark)
        {
            EditorGUI.indentLevel++;
            targetScript.waistBulwarkHeight = EditorGUILayout.Slider("Waist Bulwark Height", targetScript.waistBulwarkHeight, 0.40f, 2.50f);
            targetScript.waistBulwarkThickness = EditorGUILayout.Slider("Waist Bulwark Thickness", targetScript.waistBulwarkThickness, 0.05f, 0.40f);
            targetScript.waistStanchionCount = EditorGUILayout.IntSlider("Waist Stanchion Count", targetScript.waistStanchionCount, 2, 24);
            targetScript.waistBulwarkColor = EditorGUILayout.ColorField("Waist Bulwark Color", targetScript.waistBulwarkColor);
            targetScript.waistStanchionColor = EditorGUILayout.ColorField("Waist Stanchion Color", targetScript.waistStanchionColor);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Recalculate & Refresh Curves"))
        {
            Undo.RecordObject(targetScript, "Recalculate Upper Decks");
            targetScript.MigrateLegacyClearances();
            targetScript.UpdateCachedCurves();
            EditorUtility.SetDirty(targetScript);
        }
        if (GUILayout.Button("Reset to Defaults"))
        {
            if (EditorUtility.DisplayDialog("Reset Upper Decks?", "Reset upper decks to default Forecastle and Quarterdeck?", "Reset", "Cancel"))
            {
                Undo.RecordObject(targetScript, "Reset Upper Decks");
                targetScript.InitializeDefaultTiers();
                EditorUtility.SetDirty(targetScript);
            }
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        targetScript = (UpperDeckCurves)target;
        if (!targetScript.enableInteractiveHandles) return;
        if (targetScript.tiers == null || targetScript.tiers.Count == 0) return;

        bool changed = false;

        for (int t = 0; t < targetScript.tiers.Count; t++)
        {
            UpperDeckTier tier = targetScript.tiers[t];
            if (tier == null || tier.segments == null) continue;

            for (int s = 0; s < tier.segments.Count; s++)
            {
                UpperDeckSegment seg = tier.segments[s];
                if (seg == null || !seg.isEnabled) continue;

                float maxAllowedFwdZ = targetScript.GetInnerStemZAtElevation(seg.fwdCenterTip.y);
                float minAllowedAftZ = targetScript.GetInnerSternZAtElevation(seg.aftCenterTip.y);

                UpperDeckSegment supSeg = (tier.tierLevel > 1) ? targetScript.GetSupportingSegment(seg) : null;
                float maxFwdLimit = (supSeg != null) ? supSeg.forwardZ : maxAllowedFwdZ;
                float minAftLimit = (supSeg != null) ? supSeg.aftZ : minAllowedAftZ;

                // 1. Forward Z Slider Handle
                float fwdHandleSize = HandleUtility.GetHandleSize(seg.fwdCenterTip) * 0.15f;
                Handles.color = seg.flushToStem ? new Color(0.2f, 0.95f, 0.4f, 0.95f) : seg.deckColor;
                EditorGUI.BeginChangeCheck();
                Vector3 newFwd = Handles.Slider(seg.fwdCenterTip, Vector3.forward, fwdHandleSize, Handles.ConeHandleCap, 0.05f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(targetScript, $"Move {seg.segmentName} Forward Limit");
                    if (tier.tierLevel > 1)
                    {
                        // Tier 2 Poop Deck cannot float forward of supporting deck forward break
                        newFwd.z = Mathf.Clamp(newFwd.z, seg.aftZ + 0.2f, maxFwdLimit);
                        seg.forwardZ = newFwd.z;
                    }
                    else
                    {
                        if (newFwd.z >= maxAllowedFwdZ - 0.05f)
                        {
                            seg.flushToStem = true;
                            seg.stemClearance = 0f;
                            seg.forwardZ = maxAllowedFwdZ;
                        }
                        else
                        {
                            if (seg.flushToStem && newFwd.z < maxAllowedFwdZ - 0.10f)
                            {
                                seg.flushToStem = false;
                            }
                            seg.forwardZ = newFwd.z;
                        }
                    }
                    changed = true;
                }

                // 2. Aft Z Slider Handle
                float aftHandleSize = HandleUtility.GetHandleSize(seg.aftCenterTip) * 0.15f;
                Handles.color = seg.flushToTransom ? new Color(0.2f, 0.95f, 0.4f, 0.95f) : seg.deckColor;
                EditorGUI.BeginChangeCheck();
                Vector3 newAft = Handles.Slider(seg.aftCenterTip, Vector3.forward, aftHandleSize, Handles.ConeHandleCap, 0.05f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(targetScript, $"Move {seg.segmentName} Aft Limit");
                    if (newAft.z <= minAllowedAftZ + 0.05f)
                    {
                        seg.flushToTransom = true;
                        seg.sternClearance = 0f;
                        seg.aftZ = minAllowedAftZ;
                    }
                    else
                    {
                        if (seg.flushToTransom && newAft.z > minAllowedAftZ + 0.10f)
                        {
                            seg.flushToTransom = false;
                        }
                        if (tier.tierLevel > 1)
                        {
                            seg.aftZ = Mathf.Clamp(newAft.z, minAftLimit, seg.forwardZ - 0.2f);
                        }
                        else
                        {
                            seg.aftZ = newAft.z;
                        }
                    }
                    changed = true;
                }

                // 3. Elevation Slider Handle at Midship
                float midHandleSize = HandleUtility.GetHandleSize(seg.midshipCenter) * 0.12f;
                Handles.color = Color.yellow;
                EditorGUI.BeginChangeCheck();
                Vector3 newMid = Handles.Slider(seg.midshipCenter, Vector3.up, midHandleSize, Handles.DotHandleCap, 0.05f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(targetScript, $"Move {seg.segmentName} Elevation");
                    float lowerFloorY = targetScript.GetFloorYAtZ(t, seg, seg.midshipCenter.z);
                    float newHeadroom = Mathf.Max(1.0f, newMid.y - lowerFloorY);
                    if (seg.overrideTierHeight)
                    {
                        seg.customHeadroom = newHeadroom;
                    }
                    else
                    {
                        tier.tierHeadroom = newHeadroom;
                    }
                    changed = true;
                }

                if (targetScript.showLabels)
                {
                    string fwdLabel = seg.flushToStem ? $"{seg.segmentName} Fwd: [Flush to Stem]" : $"{seg.segmentName} Fwd: {seg.forwardZ:F2}m";
                    string aftLabel = seg.flushToTransom ? $"{seg.segmentName} Aft: [Flush to Transom]" : $"{seg.segmentName} Aft: {seg.aftZ:F2}m";
                    Handles.Label(seg.fwdCenterTip + Vector3.up * 0.2f, fwdLabel);
                    Handles.Label(seg.aftCenterTip + Vector3.up * 0.2f, aftLabel);
                    Handles.Label(seg.midshipCenter + Vector3.up * 0.25f, $"{seg.segmentName} Headroom: {seg.actualHeadroom:F2}m");
                }
            }
        }

        // 4. Interactive Scene Handle for Main Deck Waist Bulwark Height
        if (targetScript.showWaistBulwark && targetScript.waistMidshipHandlePt != Vector3.zero && targetScript.GetWaistExtents(out float wFwdZ, out float wAftZ))
        {
            float wMidZ = (wFwdZ + wAftZ) * 0.5f;
            Vector3 handlePos = targetScript.waistMidshipHandlePt;
            float handleSize = HandleUtility.GetHandleSize(handlePos) * 0.12f;
            Handles.color = targetScript.waistBulwarkColor;
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.Slider(handlePos, Vector3.up, handleSize, Handles.DotHandleCap, 0.05f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(targetScript, "Change Waist Bulwark Height");
                if (targetScript.mainDeckCurves != null)
                {
                    float floorY = targetScript.mainDeckCurves.GetDeckSurfacePoint(0, wMidZ).y;
                    float camber = targetScript.mainDeckCurves.EvaluateDeckCamberAtZ(wMidZ);
                    float edgeY = floorY - camber;
                    targetScript.waistBulwarkHeight = Mathf.Clamp(newPos.y - edgeY, 0.40f, 2.50f);
                    changed = true;
                }
            }
            if (targetScript.showLabels)
            {
                Handles.Label(handlePos + Vector3.up * 0.2f, $"Waist Bulwark: {targetScript.waistBulwarkHeight:F2}m");
            }
        }

        if (changed)
        {
            targetScript.UpdateCachedCurves();
            EditorUtility.SetDirty(targetScript);
        }
    }
}
#endif
