using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Serializable configuration and geometry cache for a single Lower Deck Tier.
/// </summary>
[System.Serializable]
public class LowerDeckTier
{
    public string tierName = "Lower Deck 1";
    public bool isEnabled = true;

    [Header("Headroom & Elevation")]
    [Tooltip("Target vertical headroom clearance (in meters) below the deck directly above.")]
    [Range(0.5f, 3.5f)] public float headroom = 1.95f;

    [Header("Longitudinal Span")]
    [Tooltip("Forward end station Z of this lower deck.")]
    public float forwardZ = 12.0f;

    [Tooltip("Aft end station Z of this lower deck.")]
    public float aftZ = -12.0f;

    [Header("Stem & Stern Limits")]
    [Tooltip("Safety margin offset (in meters) inside the stem post to terminate the forward deck break.")]
    [Range(0.05f, 2.0f)] public float stemClearance = 0.35f;

    [Tooltip("Safety margin offset (in meters) forward of the stern profile to terminate the aft deck break.")]
    [Range(0.05f, 2.0f)] public float sternClearance = 0.35f;

    [Header("Sheer & Camber")]
    [Tooltip("Ratio of Main Deck sheer curvature applied to this deck (0 = completely flat, 1 = same rise as Main Deck).")]
    [Range(0f, 1.2f)] public float sheerScale = 0.5f;

    [Tooltip("Camber (Round of Beam drop) at midships.")]
    [Range(0f, 0.8f)] public float midshipCamber = 0.20f;

    [Tooltip("Scale camber drop proportionally to local deck beam at station Z.")]
    public bool proportionalCamberToBeam = true;

    [Header("Solid Thickness")]
    [Tooltip("Enable rendering and evaluation of the bottom surface for this tier.")]
    public bool showBottomDeck = true;

    [Tooltip("Physical vertical thickness / spacing between the top and bottom deck surfaces.")]
    [Range(0.01f, 0.8f)] public float deckThickness = 0.18f;

    [Tooltip("Draw vertical edge cap lines connecting top deck edge to bottom deck edge across the hull.")]
    public bool showThicknessEdgeCaps = true;

    [Header("Visualization Settings")]
    public bool showCenterline = true;
    public bool showEdges = true;
    public bool showBeams = true;
    public bool showEndMargins = true;
    public bool showNodeSpheres = true;
    [Range(3, 40)] public int deckBeamCount = 12;

    [Header("Gizmo Colors")]
    public Color centerlineColor = new Color(0.2f, 0.85f, 0.95f, 0.95f); // Bright Aqua
    public Color deckEdgeColor = new Color(0.15f, 0.70f, 0.85f, 0.95f);   // Cyan / Blue
    public Color beamColor = new Color(0.70f, 0.85f, 0.95f, 0.60f);       // Translucent Ice Blue
    public Color bottomColor = new Color(0.10f, 0.55f, 0.70f, 0.75f);     // Deep Sea Blue
    public Color edgeCapColor = new Color(0.30f, 0.90f, 0.80f, 0.70f);    // Mint Teal

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
    [HideInInspector] public float actualHeadroom = 1.95f;
    [HideInInspector] public float holdClearanceToBottom = 1.50f;
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
}

/// <summary>
/// LowerDeckCurves manages a variable, stacked tier system of lower decks (e.g. Gun Deck, Orlop Deck, Hold Platforms)
/// bounded between the bottom of MainDeckCurves and the lowest inner hull boundary (keelson / rising floors).
/// </summary>
[ExecuteInEditMode]
public class LowerDeckCurves : MonoBehaviour
{
    [Header("Primary Datum Reference")]
    [Tooltip("Reference to MainDeckCurves which acts as the top ceiling datum for Tier 1.")]
    public MainDeckCurves mainDeckCurves;

    [Header("Hull Geometry References")]
    public SheerCurves sheerCurves;
    public TopLineCurves topLineCurves;
    public BreadthCurves breadthCurves;
    public BilgeCurves bilgeCurves;
    public FloorCurves floorCurves;
    public FashionCurves fashionCurves;
    public BodyCurves bodyCurves;
    public BackboneCurves backboneCurves;

    [Header("Interactive Scene Handles")]
    [Tooltip("Enable interactive 3D position handles in Scene view to move midship apex and span endpoints.")]
    public bool enableInteractiveHandles = true;

    [Range(0.04f, 0.25f)] public float nodeSphereRadius = 0.10f;
    public bool showLabels = true;

    [Header("Lower Deck Tiers")]
    [Tooltip("List of Lower Deck Tiers stacked from top to bottom.")]
    public List<LowerDeckTier> tiers = new List<LowerDeckTier>();

    [HideInInspector] public int changeVersion = 0;
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
        if (tiers == null || tiers.Count == 0)
        {
            InitializeDefaultTiers();
        }
        else
        {
            UpdateCachedCurves();
        }
    }

    public void FindReferences()
    {
        if (mainDeckCurves == null) mainDeckCurves = GetComponentInParent<MainDeckCurves>();
        if (mainDeckCurves == null) mainDeckCurves = FindFirstObjectByType<MainDeckCurves>();

        if (sheerCurves == null) sheerCurves = GetComponentInParent<SheerCurves>();
        if (sheerCurves == null) sheerCurves = FindFirstObjectByType<SheerCurves>();

        if (topLineCurves == null) topLineCurves = GetComponentInParent<TopLineCurves>();
        if (topLineCurves == null) topLineCurves = FindFirstObjectByType<TopLineCurves>();

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

    [ContextMenu("Reset to Default Lower Decks")]
    public void InitializeDefaultTiers()
    {
        FindReferences();
        if (mainDeckCurves != null) mainDeckCurves.EnsureInitialized();

        tiers.Clear();

        float bowZ = (mainDeckCurves != null) ? mainDeckCurves.bowTip.z : 14f;
        float sternZ = (mainDeckCurves != null) ? mainDeckCurves.sternCenter.z : -12f;

        float defaultFwdZ = (sheerCurves != null) ? sheerCurves.GetStemProfileZAtY(2.5f) - 0.5f : bowZ - 2.5f;
        float defaultAftZ = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(2.5f) + 0.5f : sternZ + 2.5f;

        // Tier 1: Lower Deck 1 (Gun / Berth Deck)
        LowerDeckTier tier1 = new LowerDeckTier
        {
            tierName = "Lower Deck 1",
            isEnabled = true,
            headroom = 1.95f,
            forwardZ = defaultFwdZ,
            aftZ = defaultAftZ,
            stemClearance = 0.35f,
            sternClearance = 0.35f,
            sheerScale = 0.5f,
            midshipCamber = 0.20f,
            deckThickness = 0.18f,
            centerlineColor = new Color(0.20f, 0.85f, 0.95f, 0.95f),
            deckEdgeColor = new Color(0.15f, 0.70f, 0.85f, 0.95f),
            beamColor = new Color(0.70f, 0.85f, 0.95f, 0.60f),
            bottomColor = new Color(0.12f, 0.55f, 0.70f, 0.75f)
        };
        tiers.Add(tier1);

        UpdateCachedCurves();
    }

    /// <summary>
    /// Computes the remaining vertical clearance (in meters) at midships between the bottom of the lowest tier
    /// (or Main Deck if no lower decks exist) and the inner hull bottom / keelson.
    /// </summary>
    public float GetRemainingHoldSpace()
    {
        if (mainDeckCurves == null) return 0f;
        mainDeckCurves.EnsureInitialized();

        float midZ = mainDeckCurves.midshipCenter.z;
        float floorMidY = GetInnerHullBottomYAt(midZ);

        float lowestDeckBottomY;
        if (tiers == null || tiers.Count == 0)
        {
            lowestDeckBottomY = mainDeckCurves.GetBottomDeckCenterlinePointAtZ(midZ).y;
        }
        else
        {
            LowerDeckTier lowestTier = tiers[tiers.Count - 1];
            lowestDeckBottomY = lowestTier.midshipBottomCenter.y;
        }

        return Mathf.Max(0f, lowestDeckBottomY - floorMidY);
    }

    /// <summary>
    /// Checks whether there is sufficient vertical space in the hold to accommodate an additional lower deck tier.
    /// Requires at least minimum physical deck thickness (0.16m) plus minimal vertical margin (0.35m) = 0.51m.
    /// </summary>
    public bool CanAddTier()
    {
        const float minRequiredSpace = 0.50f;
        return GetRemainingHoldSpace() >= minRequiredSpace;
    }

    public void AddTier()
    {
        FindReferences();
        if (mainDeckCurves != null) mainDeckCurves.EnsureInitialized();

        if (!CanAddTier())
        {
            Debug.LogWarning($"[LowerDeckCurves] Cannot add new lower deck: remaining hold space ({GetRemainingHoldSpace():F2}m) is insufficient to accommodate another deck tier with physical thickness.");
            return;
        }

        int nextIndex = tiers.Count + 1;
        float bowZ = (mainDeckCurves != null) ? mainDeckCurves.bowTip.z : 14f;
        float sternZ = (mainDeckCurves != null) ? mainDeckCurves.sternCenter.z : -12f;
        float midZ = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.z : 0f;

        // Progressive longitudinal shortening for lower decks (e.g. Orlop / Hold platforms)
        float approxY = Mathf.Max(1.0f, 2.5f - 1.5f * (nextIndex - 1));
        float stemZ = (sheerCurves != null) ? sheerCurves.GetStemProfileZAtY(approxY) - 0.5f : bowZ - 3.0f;
        float sternZAtY = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(approxY) + 0.5f : sternZ + 3.0f;

        float fwd = Mathf.Lerp(stemZ, midZ + 1f, 0.12f * nextIndex);
        float aft = Mathf.Lerp(sternZAtY, midZ - 1f, 0.12f * nextIndex);

        Color[] colorPresets = new Color[]
        {
            new Color(0.20f, 0.85f, 0.95f, 0.95f), // Cyan
            new Color(0.35f, 0.95f, 0.65f, 0.95f), // Mint Green
            new Color(0.95f, 0.75f, 0.25f, 0.95f), // Warm Amber
            new Color(0.85f, 0.40f, 0.95f, 0.95f)  // Orchid
        };

        Color c = colorPresets[(nextIndex - 1) % colorPresets.Length];

        LowerDeckTier newTier = new LowerDeckTier
        {
            tierName = $"Lower Deck {nextIndex}",
            isEnabled = true,
            headroom = 1.80f,
            forwardZ = fwd,
            aftZ = aft,
            stemClearance = 0.35f,
            sternClearance = 0.35f,
            sheerScale = Mathf.Max(0.2f, 0.5f - 0.1f * nextIndex),
            midshipCamber = Mathf.Max(0.10f, 0.20f - 0.03f * nextIndex),
            deckThickness = 0.16f,
            centerlineColor = c,
            deckEdgeColor = new Color(c.r * 0.85f, c.g * 0.85f, c.b * 0.85f, 0.95f),
            beamColor = new Color(c.r, c.g, c.b, 0.60f),
            bottomColor = new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 0.75f)
        };

        tiers.Add(newTier);
        UpdateCachedCurves();
    }

    public void RemoveLastTier()
    {
        if (tiers.Count > 0)
        {
            tiers.RemoveAt(tiers.Count - 1);
            UpdateCachedCurves();
        }
    }

    private void OnValidate()
    {
        UpdateCachedCurves();
    }

    /// <summary>
    /// Re-evaluates all Lower Deck Tiers, enforcing stacked vertical boundaries, deep inner hull fit,
    /// solid physical thickness, and zero GC runtime buffers.
    /// </summary>
    public virtual void UpdateCachedCurves()
    {
        if (isUpdatingCurves) return;
        isUpdatingCurves = true;

        try
        {
            FindReferences();

            if (mainDeckCurves == null) return;
            mainDeckCurves.EnsureInitialized();

            float midZ = mainDeckCurves.midshipCenter.z;
            float maxBowZ = (topLineCurves != null) ? topLineCurves.cachedFwdP0.z : 15f;
            float minSternZ = (fashionCurves != null) ? fashionCurves.innerStbdP4.z : ((sheerCurves != null) ? sheerCurves.sternP3.z : -15f);

            for (int k = 0; k < tiers.Count; k++)
            {
                LowerDeckTier tier = tiers[k];
                if (tier == null) continue;

                // Ensure buffers are non-null
                EnsureTierBuffers(tier);

                // 1. Determine Midship Ceiling Y
                float ceilingMidY;
                if (k == 0)
                {
                    // Underside of Main Deck
                    ceilingMidY = mainDeckCurves.GetBottomDeckCenterlinePointAtZ(midZ).y;
                }
                else
                {
                    // Underside of the tier directly above
                    LowerDeckTier tierAbove = tiers[k - 1];
                    ceilingMidY = tierAbove.midshipBottomCenter.y;
                }

                // 2. Determine Midship Floor Y (Keelson / Inner Bottom)
                float floorMidY = GetInnerHullBottomYAt(midZ);

                // 3. Calculate Midship Center Y from desired headroom
                tier.headroom = Mathf.Clamp(tier.headroom, 0.5f, 3.5f);
                float desiredMidY = ceilingMidY - tier.headroom;

                // Enforce safety margins: minimum 0.15m above floor, minimum 0.20m below ceiling
                float minAllowedMidY = floorMidY + 0.15f;
                float maxAllowedMidY = ceilingMidMidClamped(ceilingMidY - 0.20f, minAllowedMidY);

                float finalMidY = Mathf.Clamp(desiredMidY, minAllowedMidY, maxAllowedMidY);
                tier.midshipCenter = new Vector3(0f, finalMidY, midZ);

                tier.actualHeadroom = ceilingMidY - finalMidY;
                tier.holdClearanceToBottom = finalMidY - floorMidY;

                // 4. Calculate Elevation-Aware Stem & Stern Limits
                // The deck at height finalMidY cannot extend further forward than the stem post,
                // nor further aft than the stern post / deadwood at that exact elevation.
                float stemZAtElevation = (sheerCurves != null) ? sheerCurves.GetStemProfileZAtY(finalMidY) : maxBowZ;
                float maxAllowedFwdZ = Mathf.Min(stemZAtElevation - tier.stemClearance, maxBowZ);
                maxAllowedFwdZ = Mathf.Max(midZ + 0.5f, maxAllowedFwdZ);

                float sternZAtElevation = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(finalMidY) : minSternZ;
                float minAllowedAftZ = Mathf.Max(sternZAtElevation + tier.sternClearance, minSternZ);
                minAllowedAftZ = Mathf.Min(midZ - 0.5f, minAllowedAftZ);

                // Clamp longitudinal span so the deck never climbs or penetrates the stem/stern
                tier.forwardZ = Mathf.Clamp(tier.forwardZ, midZ + 0.5f, maxAllowedFwdZ);
                tier.aftZ = Mathf.Clamp(tier.aftZ, minAllowedAftZ, midZ - 0.5f);

                // 5. Calculate Midship Deck Edge (Camber)
                tier.midshipCamber = Mathf.Clamp(tier.midshipCamber, 0f, 0.8f);
                float midEdgeY = tier.midshipCenter.y - tier.midshipCamber;
                midEdgeY = Mathf.Max(midEdgeY, floorMidY + 0.05f);
                float midEdgeX = GetInnerHullHalfBreadthAt(midEdgeY, midZ);

                tier.stbdMidshipEdge = new Vector3(midEdgeX, midEdgeY, midZ);
                tier.portMidshipEdge = new Vector3(-midEdgeX, midEdgeY, midZ);
                tier.midshipBeamWidth = midEdgeX * 2f;

                // 6. Calculate Forward and Aft Centerline Tips
                // Decks terminate horizontally against the stem/stern timbers - they do NOT climb the rake!
                float fwdCeilY = GetCeilingYAtZ(k, tier.forwardZ);
                float fwdRise = (mainDeckCurves.EvaluateCenterlineSheerYAtZ(tier.forwardZ) - mainDeckCurves.midshipCenter.y) * tier.sheerScale;
                float fwdCenterY = Mathf.Clamp(finalMidY + fwdRise, floorMidY + 0.05f, fwdCeilY - 0.15f);
                tier.fwdCenterTip = new Vector3(0f, fwdCenterY, tier.forwardZ);

                float aftCeilY = GetCeilingYAtZ(k, tier.aftZ);
                float aftRise = (mainDeckCurves.EvaluateCenterlineSheerYAtZ(tier.aftZ) - mainDeckCurves.midshipCenter.y) * tier.sheerScale;
                float aftCenterY = Mathf.Clamp(finalMidY + aftRise, floorMidY + 0.05f, aftCeilY - 0.15f);
                tier.aftCenterTip = new Vector3(0f, aftCenterY, tier.aftZ);

                // 7. Calculate Bottom Landmark Nodes
                tier.deckThickness = Mathf.Clamp(tier.deckThickness, 0.01f, 0.8f);
                tier.midshipBottomCenter = new Vector3(0f, tier.midshipCenter.y - tier.deckThickness, midZ);

                float bottomMidEdgeY = tier.stbdMidshipEdge.y - tier.deckThickness;
                float bottomMidEdgeX = GetInnerHullHalfBreadthAt(bottomMidEdgeY, midZ);
                tier.stbdMidshipBottomEdge = new Vector3(bottomMidEdgeX, bottomMidEdgeY, midZ);
                tier.portMidshipBottomEdge = new Vector3(-bottomMidEdgeX, bottomMidEdgeY, midZ);

                tier.fwdBottomCenterTip = new Vector3(0f, tier.fwdCenterTip.y - tier.deckThickness, tier.forwardZ);
                tier.aftBottomCenterTip = new Vector3(0f, tier.aftCenterTip.y - tier.deckThickness, tier.aftZ);

                // 8. Build Cached Polylines and Transverse Beams
                BuildTierPolylines(tier, k);
                BuildTierDeckBeams(tier, k);
                BuildTierEndMargins(tier, k);
            }

            changeVersion++;
        }
        finally
        {
            isUpdatingCurves = false;
        }
    }

    private float ceilingMidMidClamped(float maxVal, float minVal)
    {
        return Mathf.Max(minVal, maxVal);
    }

    private void EnsureTierBuffers(LowerDeckTier tier)
    {
        if (tier.cachedCenterlinePts == null || tier.cachedCenterlinePts.Length != RESOLUTION + 1)
            tier.cachedCenterlinePts = new Vector3[RESOLUTION + 1];
        if (tier.cachedStbdEdgePts == null || tier.cachedStbdEdgePts.Length != RESOLUTION + 1)
            tier.cachedStbdEdgePts = new Vector3[RESOLUTION + 1];
        if (tier.cachedPortEdgePts == null || tier.cachedPortEdgePts.Length != RESOLUTION + 1)
            tier.cachedPortEdgePts = new Vector3[RESOLUTION + 1];

        if (tier.cachedBottomCenterlinePts == null || tier.cachedBottomCenterlinePts.Length != RESOLUTION + 1)
            tier.cachedBottomCenterlinePts = new Vector3[RESOLUTION + 1];
        if (tier.cachedStbdBottomEdgePts == null || tier.cachedStbdBottomEdgePts.Length != RESOLUTION + 1)
            tier.cachedStbdBottomEdgePts = new Vector3[RESOLUTION + 1];
        if (tier.cachedPortBottomEdgePts == null || tier.cachedPortBottomEdgePts.Length != RESOLUTION + 1)
            tier.cachedPortBottomEdgePts = new Vector3[RESOLUTION + 1];

        if (tier.cachedDeckBeams == null) tier.cachedDeckBeams = new List<Vector3[]>();
        if (tier.cachedBottomDeckBeams == null) tier.cachedBottomDeckBeams = new List<Vector3[]>();

        const int marginSteps = 24;
        if (tier.fwdEndMarginArc == null || tier.fwdEndMarginArc.Length != marginSteps + 1)
            tier.fwdEndMarginArc = new Vector3[marginSteps + 1];
        if (tier.aftEndMarginArc == null || tier.aftEndMarginArc.Length != marginSteps + 1)
            tier.aftEndMarginArc = new Vector3[marginSteps + 1];

        if (tier.bottomFwdEndMarginArc == null || tier.bottomFwdEndMarginArc.Length != marginSteps + 1)
            tier.bottomFwdEndMarginArc = new Vector3[marginSteps + 1];
        if (tier.bottomAftEndMarginArc == null || tier.bottomAftEndMarginArc.Length != marginSteps + 1)
            tier.bottomAftEndMarginArc = new Vector3[marginSteps + 1];
    }

    private void BuildTierPolylines(LowerDeckTier tier, int tierIndex)
    {
        float aftZ = tier.aftZ;
        float fwdZ = tier.forwardZ;

        for (int i = 0; i <= RESOLUTION; i++)
        {
            float frac = (float)i / RESOLUTION;
            float z = Mathf.Lerp(aftZ, fwdZ, frac);

            float centerY = EvaluateTierCenterlineSheerYAtZ(tier, tierIndex, z);
            float camber = EvaluateTierCamberAtZ(tier, z);
            float edgeY = centerY - camber;
            float edgeX = GetInnerHullHalfBreadthAt(edgeY, z);

            // Top polylines
            tier.cachedCenterlinePts[i] = new Vector3(0f, centerY, z);
            tier.cachedStbdEdgePts[i] = new Vector3(edgeX, edgeY, z);
            tier.cachedPortEdgePts[i] = new Vector3(-edgeX, edgeY, z);

            // Bottom polylines
            float bottomCenterY = centerY - tier.deckThickness;
            float bottomEdgeY = bottomCenterY - camber;
            float bottomEdgeX = GetInnerHullHalfBreadthAt(bottomEdgeY, z);

            tier.cachedBottomCenterlinePts[i] = new Vector3(0f, bottomCenterY, z);
            tier.cachedStbdBottomEdgePts[i] = new Vector3(bottomEdgeX, bottomEdgeY, z);
            tier.cachedPortBottomEdgePts[i] = new Vector3(-bottomEdgeX, bottomEdgeY, z);
        }
    }

    private void BuildTierDeckBeams(LowerDeckTier tier, int tierIndex)
    {
        tier.cachedDeckBeams.Clear();
        tier.cachedBottomDeckBeams.Clear();

        int beams = Mathf.Clamp(tier.deckBeamCount, 2, 50);
        float aftZ = tier.aftZ;
        float fwdZ = tier.forwardZ;
        const int beamSteps = 24;

        for (int b = 0; b < beams; b++)
        {
            float t = (float)b / (beams - 1);
            float z = Mathf.Lerp(aftZ, fwdZ, t);

            float centerY = EvaluateTierCenterlineSheerYAtZ(tier, tierIndex, z);
            float camber = EvaluateTierCamberAtZ(tier, z);
            float edgeY = centerY - camber;
            float halfWidth = GetInnerHullHalfBreadthAt(edgeY, z);

            if (halfWidth >= 0.005f)
            {
                // Top Camber Beam Arc
                Vector3[] topArc = new Vector3[beamSteps + 1];
                for (int s = 0; s <= beamSteps; s++)
                {
                    float normX = Mathf.Lerp(-1f, 1f, (float)s / beamSteps);
                    float x = normX * halfWidth;
                    float y = centerY - camber * (normX * normX);
                    topArc[s] = new Vector3(x, y, z);
                }
                tier.cachedDeckBeams.Add(topArc);

                // Bottom Camber Beam Arc
                float bottomCenterY = centerY - tier.deckThickness;
                float bottomEdgeY = bottomCenterY - camber;
                float bottomHalfWidth = GetInnerHullHalfBreadthAt(bottomEdgeY, z);

                Vector3[] bottomArc = new Vector3[beamSteps + 1];
                for (int s = 0; s <= beamSteps; s++)
                {
                    float normX = Mathf.Lerp(-1f, 1f, (float)s / beamSteps);
                    float x = normX * bottomHalfWidth;
                    float y = bottomCenterY - camber * (normX * normX);
                    bottomArc[s] = new Vector3(x, y, z);
                }
                tier.cachedBottomDeckBeams.Add(bottomArc);
            }
        }
    }

    private void BuildTierEndMargins(LowerDeckTier tier, int tierIndex)
    {
        const int steps = 24;

        // 1. Forward End Margin Arcs (at forwardZ)
        float fwdZ = tier.forwardZ;
        float fwdCenterY = EvaluateTierCenterlineSheerYAtZ(tier, tierIndex, fwdZ);
        float fwdCamber = EvaluateTierCamberAtZ(tier, fwdZ);
        float fwdHalfWidth = GetInnerHullHalfBreadthAt(fwdCenterY - fwdCamber, fwdZ);

        float bottomFwdCenterY = fwdCenterY - tier.deckThickness;
        float bottomFwdHalfWidth = GetInnerHullHalfBreadthAt(bottomFwdCenterY - fwdCamber, fwdZ);

        for (int s = 0; s <= steps; s++)
        {
            float normX = Mathf.Lerp(-1f, 1f, (float)s / steps);
            float xTop = normX * fwdHalfWidth;
            float yTop = fwdCenterY - fwdCamber * (normX * normX);
            tier.fwdEndMarginArc[s] = new Vector3(xTop, yTop, fwdZ);

            float xBot = normX * bottomFwdHalfWidth;
            float yBot = bottomFwdCenterY - fwdCamber * (normX * normX);
            tier.bottomFwdEndMarginArc[s] = new Vector3(xBot, yBot, fwdZ);
        }

        // 2. Aft End Margin Arcs (at aftZ)
        float aftZ = tier.aftZ;
        float aftCenterY = EvaluateTierCenterlineSheerYAtZ(tier, tierIndex, aftZ);
        float aftCamber = EvaluateTierCamberAtZ(tier, aftZ);
        float aftHalfWidth = GetInnerHullHalfBreadthAt(aftCenterY - aftCamber, aftZ);

        float bottomAftCenterY = aftCenterY - tier.deckThickness;
        float bottomAftHalfWidth = GetInnerHullHalfBreadthAt(bottomAftCenterY - aftCamber, aftZ);

        for (int s = 0; s <= steps; s++)
        {
            float normX = Mathf.Lerp(-1f, 1f, (float)s / steps);
            float xTop = normX * aftHalfWidth;
            float yTop = aftCenterY - aftCamber * (normX * normX);
            tier.aftEndMarginArc[s] = new Vector3(xTop, yTop, aftZ);

            float xBot = normX * bottomAftHalfWidth;
            float yBot = bottomAftCenterY - aftCamber * (normX * normX);
            tier.bottomAftEndMarginArc[s] = new Vector3(xBot, yBot, aftZ);
        }
    }

    // --- MATHEMATICAL EVALUATION HELPERS ---

    public float GetCeilingYAtZ(int tierIndex, float z)
    {
        if (tierIndex == 0)
        {
            if (mainDeckCurves != null)
            {
                return mainDeckCurves.GetBottomDeckCenterlinePointAtZ(z).y;
            }
            return 4.0f;
        }
        else
        {
            LowerDeckTier above = tiers[tierIndex - 1];
            return EvaluateTierCenterlineSheerYAtZ(above, tierIndex - 1, z) - above.deckThickness;
        }
    }

    public float EvaluateTierCenterlineSheerYAtZ(LowerDeckTier tier, int tierIndex, float z)
    {
        float midZ = (mainDeckCurves != null) ? mainDeckCurves.midshipCenter.z : 0f;
        float midY = tier.midshipCenter.y;

        float sheerRise = 0f;
        if (mainDeckCurves != null)
        {
            float mainSheerY = mainDeckCurves.EvaluateCenterlineSheerYAtZ(z);
            float mainMidY = mainDeckCurves.midshipCenter.y;
            sheerRise = (mainSheerY - mainMidY) * tier.sheerScale;
        }

        float y = midY + sheerRise;

        // Enforce boundary clamping: stays below ceiling, stays above flat keelson
        float ceilY = GetCeilingYAtZ(tierIndex, z) - 0.15f;
        float keelFloorY = (sheerCurves != null ? sheerCurves.keelY : 0f) + (bodyCurves != null ? bodyCurves.innerLineSpacing : 0.2f) + 0.05f;

        return Mathf.Clamp(y, keelFloorY, ceilY);
    }

    public float EvaluateTierCamberAtZ(LowerDeckTier tier, float z)
    {
        if (!tier.proportionalCamberToBeam) return tier.midshipCamber;

        float midEdgeX = Mathf.Max(0.001f, tier.stbdMidshipEdge.x);
        float centerY = tier.midshipCenter.y;
        float approxEdgeX = GetInnerHullHalfBreadthAt(centerY - tier.midshipCamber, z);

        float ratio = Mathf.Clamp01(approxEdgeX / midEdgeX);
        return tier.midshipCamber * ratio;
    }

    /// <summary>
    /// Evaluates the lowest inner hull boundary Y height at station Z (keelson, floors, or transom cut).
    /// </summary>
    public float GetInnerHullBottomYAt(float z)
    {
        float bottomSpacing = (bodyCurves != null) ? bodyCurves.innerLineSpacing : 0.2f;

        // 1. Transom Region: Base of the section is cut by the Inner Fashion Curve
        if (fashionCurves != null && z <= fashionCurves.stbdP1.z + 0.001f)
        {
            Vector3 inFashionPt = fashionCurves.GetInnerFashionPointAtZ(z, 1f);
            return inFashionPt.y;
        }

        // 2. Profile Rakes: Stem and Stern Deadwood
        if (sheerCurves != null)
        {
            if (z >= sheerCurves.stemP0.z)
            {
                return sheerCurves.GetStemProfileYAtZ(z) + bottomSpacing;
            }
            if (z <= sheerCurves.sternP0.z)
            {
                return sheerCurves.GetSternProfileYAtZ(z) + bottomSpacing;
            }
            return sheerCurves.keelY + bottomSpacing;
        }

        return bottomSpacing;
    }

    /// <summary>
    /// Evaluates the exact inner hull half-breadth X at height Y and station Z across the full transverse profile:
    /// Keel/Keelson -> Inner Floor -> Inner Bilge -> Inner Breadth -> Inner TopLine.
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
            if (y >= inTlY - 0.005f) return inTlX;

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
        float keelBottomY = GetInnerHullBottomYAt(z);
        float keelHalfWidth = (backboneCurves != null) ? backboneCurves.keelWidth * 0.5f : 0.15f;

        // Retrieve inner points if curves are active
        bool hasFloor = (floorCurves != null && floorCurves.showInnerLine);
        Vector3 inFloorPt = hasFloor ? floorCurves.GetInnerFloorPointAtZ(z) : Vector3.zero;

        bool hasBilge = (bilgeCurves != null && bilgeCurves.showInnerLine);
        Vector3 inBilgePt = hasBilge ? bilgeCurves.GetInnerBilgePointAtZ(z) : Vector3.zero;

        bool hasBreadth = (breadthCurves != null && breadthCurves.showInnerLine);
        Vector3 inBreadthPt = hasBreadth ? breadthCurves.GetInnerBreadthPointAtZ(z, true) : Vector3.zero;

        bool hasTopLine = (topLineCurves != null && topLineCurves.showInnerLine);
        Vector3 inTopLinePt = hasTopLine ? topLineCurves.GetInnerTopLinePointAtZ(z, true) : Vector3.zero;

        // If height is at or below inner keel
        if (y <= keelBottomY + 0.005f) return keelHalfWidth;

        // Below Floor
        if (hasFloor && y <= inFloorPt.y)
        {
            float span = Mathf.Max(0.001f, inFloorPt.y - keelBottomY);
            float u = Mathf.Clamp01((y - keelBottomY) / span);
            return Mathf.Lerp(keelHalfWidth, Mathf.Abs(inFloorPt.x), u);
        }

        // Between Floor and Bilge
        if (hasFloor && hasBilge && y <= inBilgePt.y)
        {
            float span = Mathf.Max(0.001f, inBilgePt.y - inFloorPt.y);
            float u = Mathf.Clamp01((y - inFloorPt.y) / span);
            return Mathf.Lerp(Mathf.Abs(inFloorPt.x), Mathf.Abs(inBilgePt.x), u);
        }

        // Between Bilge and Breadth
        if (hasBilge && hasBreadth && y <= inBreadthPt.y)
        {
            float span = Mathf.Max(0.001f, inBreadthPt.y - inBilgePt.y);
            float u = Mathf.Clamp01((y - inBilgePt.y) / span);
            return Mathf.Lerp(Mathf.Abs(inBilgePt.x), Mathf.Abs(inBreadthPt.x), u);
        }

        // Between Breadth and TopLine
        if (hasBreadth && hasTopLine && y <= inTopLinePt.y)
        {
            float span = Mathf.Max(0.001f, inTopLinePt.y - inBreadthPt.y);
            float u = Mathf.Clamp01((y - inBreadthPt.y) / span);
            return Mathf.Lerp(Mathf.Abs(inBreadthPt.x), Mathf.Abs(inTopLinePt.x), u);
        }

        // Fallback: Use Breadth or TopLine width
        if (hasBreadth) return Mathf.Abs(inBreadthPt.x);
        return 2.0f;
    }

    // --- PUBLIC SURFACE EVALUATION API ---

    public Vector3 GetTierSurfacePoint(int tierIndex, float x, float z)
    {
        EnsureInitialized();
        if (tierIndex < 0 || tierIndex >= tiers.Count) return Vector3.zero;

        LowerDeckTier tier = tiers[tierIndex];
        float centerY = EvaluateTierCenterlineSheerYAtZ(tier, tierIndex, z);
        float camber = EvaluateTierCamberAtZ(tier, z);
        float edgeY = centerY - camber;
        float halfWidth = GetInnerHullHalfBreadthAt(edgeY, z);

        if (halfWidth < 0.0001f) return new Vector3(0f, centerY, z);

        float clampedX = Mathf.Clamp(x, -halfWidth, halfWidth);
        float normX = clampedX / halfWidth;
        float y = centerY - camber * (normX * normX);

        return new Vector3(clampedX, y, z);
    }

    public Vector3 GetTierBottomSurfacePoint(int tierIndex, float x, float z)
    {
        EnsureInitialized();
        if (tierIndex < 0 || tierIndex >= tiers.Count) return Vector3.zero;

        LowerDeckTier tier = tiers[tierIndex];
        float centerY = EvaluateTierCenterlineSheerYAtZ(tier, tierIndex, z) - tier.deckThickness;
        float camber = EvaluateTierCamberAtZ(tier, z);
        float edgeY = centerY - camber;
        float halfWidth = GetInnerHullHalfBreadthAt(edgeY, z);

        if (halfWidth < 0.0001f) return new Vector3(0f, centerY, z);

        float clampedX = Mathf.Clamp(x, -halfWidth, halfWidth);
        float normX = clampedX / halfWidth;
        float y = centerY - camber * (normX * normX);

        return new Vector3(clampedX, y, z);
    }

    public Vector3 GetTierCenterlinePointAtZ(int tierIndex, float z)
    {
        EnsureInitialized();
        if (tierIndex < 0 || tierIndex >= tiers.Count) return Vector3.zero;

        LowerDeckTier tier = tiers[tierIndex];
        float centerY = EvaluateTierCenterlineSheerYAtZ(tier, tierIndex, z);
        return new Vector3(0f, centerY, z);
    }

    public Vector3 GetTierEdgePointAtZ(int tierIndex, float z, bool starboard = true)
    {
        EnsureInitialized();
        if (tierIndex < 0 || tierIndex >= tiers.Count) return Vector3.zero;

        LowerDeckTier tier = tiers[tierIndex];
        float centerY = EvaluateTierCenterlineSheerYAtZ(tier, tierIndex, z);
        float camber = EvaluateTierCamberAtZ(tier, z);
        float edgeY = centerY - camber;
        float edgeX = GetInnerHullHalfBreadthAt(edgeY, z);

        return new Vector3(starboard ? edgeX : -edgeX, edgeY, z);
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
        if (tiers == null) return;

        for (int k = 0; k < tiers.Count; k++)
        {
            LowerDeckTier tier = tiers[k];
            if (tier == null || !tier.isEnabled) continue;

            // 1. Centerline Sheer
            if (tier.showCenterline && tier.cachedCenterlinePts != null && tier.cachedCenterlinePts.Length > 1)
            {
                Gizmos.color = tier.centerlineColor;
                for (int i = 0; i < tier.cachedCenterlinePts.Length - 1; i++)
                {
                    Gizmos.DrawLine(tier.cachedCenterlinePts[i], tier.cachedCenterlinePts[i + 1]);
                }
            }

            // 2. Deck Edges
            if (tier.showEdges && tier.cachedStbdEdgePts != null && tier.cachedStbdEdgePts.Length > 1)
            {
                Gizmos.color = tier.deckEdgeColor;
                for (int i = 0; i < tier.cachedStbdEdgePts.Length - 1; i++)
                {
                    Gizmos.DrawLine(tier.cachedStbdEdgePts[i], tier.cachedStbdEdgePts[i + 1]);
                    Gizmos.DrawLine(tier.cachedPortEdgePts[i], tier.cachedPortEdgePts[i + 1]);
                }
            }

            // 3. Transverse Camber Beams
            if (tier.showBeams && tier.cachedDeckBeams != null)
            {
                Gizmos.color = tier.beamColor;
                for (int b = 0; b < tier.cachedDeckBeams.Count; b++)
                {
                    Vector3[] arc = tier.cachedDeckBeams[b];
                    if (arc == null || arc.Length < 2) continue;
                    for (int i = 0; i < arc.Length - 1; i++)
                    {
                        Gizmos.DrawLine(arc[i], arc[i + 1]);
                    }
                }
            }

            // 4. Transverse End Margin Caps (Forward & Aft Partial Ends)
            if (tier.showEndMargins)
            {
                Gizmos.color = tier.centerlineColor;
                if (tier.fwdEndMarginArc != null)
                {
                    for (int i = 0; i < tier.fwdEndMarginArc.Length - 1; i++)
                        Gizmos.DrawLine(tier.fwdEndMarginArc[i], tier.fwdEndMarginArc[i + 1]);
                }
                if (tier.aftEndMarginArc != null)
                {
                    for (int i = 0; i < tier.aftEndMarginArc.Length - 1; i++)
                        Gizmos.DrawLine(tier.aftEndMarginArc[i], tier.aftEndMarginArc[i + 1]);
                }
            }

            // 5. Bottom Surface Curves
            if (tier.showBottomDeck)
            {
                if (tier.showCenterline && tier.cachedBottomCenterlinePts != null && tier.cachedBottomCenterlinePts.Length > 1)
                {
                    Gizmos.color = tier.bottomColor;
                    for (int i = 0; i < tier.cachedBottomCenterlinePts.Length - 1; i++)
                        Gizmos.DrawLine(tier.cachedBottomCenterlinePts[i], tier.cachedBottomCenterlinePts[i + 1]);
                }

                if (tier.showEdges && tier.cachedStbdBottomEdgePts != null && tier.cachedStbdBottomEdgePts.Length > 1)
                {
                    Gizmos.color = tier.bottomColor;
                    for (int i = 0; i < tier.cachedStbdBottomEdgePts.Length - 1; i++)
                    {
                        Gizmos.DrawLine(tier.cachedStbdBottomEdgePts[i], tier.cachedStbdBottomEdgePts[i + 1]);
                        Gizmos.DrawLine(tier.cachedPortBottomEdgePts[i], tier.cachedPortBottomEdgePts[i + 1]);
                    }
                }

                if (tier.showBeams && tier.cachedBottomDeckBeams != null)
                {
                    Gizmos.color = new Color(tier.bottomColor.r, tier.bottomColor.g, tier.bottomColor.b, 0.40f);
                    for (int b = 0; b < tier.cachedBottomDeckBeams.Count; b++)
                    {
                        Vector3[] arc = tier.cachedBottomDeckBeams[b];
                        if (arc == null || arc.Length < 2) continue;
                        for (int i = 0; i < arc.Length - 1; i++)
                            Gizmos.DrawLine(arc[i], arc[i + 1]);
                    }
                }

                if (tier.showEndMargins)
                {
                    Gizmos.color = tier.bottomColor;
                    if (tier.bottomFwdEndMarginArc != null)
                    {
                        for (int i = 0; i < tier.bottomFwdEndMarginArc.Length - 1; i++)
                            Gizmos.DrawLine(tier.bottomFwdEndMarginArc[i], tier.bottomFwdEndMarginArc[i + 1]);
                    }
                    if (tier.bottomAftEndMarginArc != null)
                    {
                        for (int i = 0; i < tier.bottomAftEndMarginArc.Length - 1; i++)
                            Gizmos.DrawLine(tier.bottomAftEndMarginArc[i], tier.bottomAftEndMarginArc[i + 1]);
                    }
                }

                // Vertical Thickness Edge Caps
                if (tier.showThicknessEdgeCaps && tier.cachedStbdEdgePts != null && tier.cachedStbdBottomEdgePts != null)
                {
                    Gizmos.color = tier.edgeCapColor;

                    // Midship edge caps
                    Gizmos.DrawLine(tier.stbdMidshipEdge, tier.stbdMidshipBottomEdge);
                    Gizmos.DrawLine(tier.portMidshipEdge, tier.portMidshipBottomEdge);

                    // End centerline caps
                    Gizmos.DrawLine(tier.fwdCenterTip, tier.fwdBottomCenterTip);
                    Gizmos.DrawLine(tier.aftCenterTip, tier.aftBottomCenterTip);

                    // Edge caps along sides (sample every few points)
                    int step = Mathf.Max(1, RESOLUTION / 8);
                    for (int i = 0; i <= RESOLUTION; i += step)
                    {
                        if (i < tier.cachedStbdEdgePts.Length && i < tier.cachedStbdBottomEdgePts.Length)
                        {
                            Gizmos.DrawLine(tier.cachedStbdEdgePts[i], tier.cachedStbdBottomEdgePts[i]);
                            Gizmos.DrawLine(tier.cachedPortEdgePts[i], tier.cachedPortBottomEdgePts[i]);
                        }
                    }
                }
            }

            // 6. Landmark Node Spheres
            if (tier.showNodeSpheres)
            {
                Gizmos.color = tier.centerlineColor;
                Gizmos.DrawSphere(tier.midshipCenter, nodeSphereRadius);
                Gizmos.DrawSphere(tier.fwdCenterTip, nodeSphereRadius * 0.85f);
                Gizmos.DrawSphere(tier.aftCenterTip, nodeSphereRadius * 0.85f);

                Gizmos.color = tier.deckEdgeColor;
                Gizmos.DrawSphere(tier.stbdMidshipEdge, nodeSphereRadius * 0.85f);
                Gizmos.DrawSphere(tier.portMidshipEdge, nodeSphereRadius * 0.85f);
            }

#if UNITY_EDITOR
            if (showLabels)
            {
                Handles.Label(tier.midshipCenter + Vector3.up * 0.15f, $"{tier.tierName} (Headroom={tier.actualHeadroom:F2}m)", GetCachedLabelStyle(tier.centerlineColor));
            }
#endif
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LowerDeckCurves))]
public class LowerDeckCurvesEditor : Editor
{
    private bool[] tierFoldouts;

    private void OnSceneGUI()
    {
        LowerDeckCurves curves = (LowerDeckCurves)target;
        if (!curves.enableInteractiveHandles || curves.tiers == null) return;

        curves.EnsureInitialized();

        for (int k = 0; k < curves.tiers.Count; k++)
        {
            LowerDeckTier tier = curves.tiers[k];
            if (tier == null || !tier.isEnabled) continue;

            // 1. Midship Height Handle (adjusts headroom)
            EditorGUI.BeginChangeCheck();
            Vector3 newMid = Handles.PositionHandle(tier.midshipCenter, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(curves, $"Adjust {tier.tierName} Height");
                float ceilY = curves.GetCeilingYAtZ(k, tier.midshipCenter.z);
                tier.headroom = Mathf.Max(0.5f, ceilY - newMid.y);
                curves.UpdateCachedCurves();
                EditorUtility.SetDirty(curves);
            }

            // 2. Forward Z Slider Handle
            EditorGUI.BeginChangeCheck();
            Vector3 newFwd = Handles.Slider(tier.fwdCenterTip, Vector3.forward, 0.4f, Handles.ConeHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(curves, $"Adjust {tier.tierName} Forward Z");
                tier.forwardZ = newFwd.z;
                curves.UpdateCachedCurves();
                EditorUtility.SetDirty(curves);
            }

            // 3. Aft Z Slider Handle
            EditorGUI.BeginChangeCheck();
            Vector3 newAft = Handles.Slider(tier.aftCenterTip, Vector3.back, 0.4f, Handles.ConeHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(curves, $"Adjust {tier.tierName} Aft Z");
                tier.aftZ = newAft.z;
                curves.UpdateCachedCurves();
                EditorUtility.SetDirty(curves);
            }
        }
    }

    public override void OnInspectorGUI()
    {
        LowerDeckCurves curves = (LowerDeckCurves)target;

        EditorGUILayout.Space(6);
        if (curves.mainDeckCurves == null)
        {
            EditorGUILayout.HelpBox("MainDeckCurves reference is missing! Please assign MainDeckCurves as the primary datum.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox($"Referencing Main Deck (Sheer Apex Y = {curves.mainDeckCurves.midshipCenter.y:F2}m)", MessageType.Info);
        }

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Lower Deck Tier Management", EditorStyles.boldLabel);

        bool canAdd = curves.CanAddTier();
        float remainingSpace = curves.GetRemainingHoldSpace();

        if (!canAdd)
        {
            EditorGUILayout.HelpBox($"Hold space limit reached! Remaining clearance above the inner hull bottom ({remainingSpace:F2}m) cannot accommodate an additional deck tier.", MessageType.Info);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!canAdd);
        if (GUILayout.Button("+ Add Lower Deck Tier", GUILayout.Height(30)))
        {
            Undo.RecordObject(curves, "Add Lower Deck Tier");
            curves.AddTier();
            EditorUtility.SetDirty(curves);
        }
        EditorGUI.EndDisabledGroup();

        if (curves.tiers.Count > 0 && GUILayout.Button("- Remove Last Tier", GUILayout.Height(30)))
        {
            Undo.RecordObject(curves, "Remove Lower Deck Tier");
            curves.RemoveLastTier();
            EditorUtility.SetDirty(curves);
        }
        EditorGUILayout.EndHorizontal();

        // Tiers Summary Readout
        if (curves.tiers != null && curves.tiers.Count > 0)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Tier Stack Readout", EditorStyles.boldLabel);

            for (int k = 0; k < curves.tiers.Count; k++)
            {
                LowerDeckTier tier = curves.tiers[k];
                if (tier == null) continue;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"{tier.tierName} (Tier {k + 1})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Headroom Clearance:", $"{tier.actualHeadroom:F2} m");
                EditorGUILayout.LabelField("Hold Depth to Bottom:", $"{tier.holdClearanceToBottom:F2} m");
                EditorGUILayout.LabelField("Midship Beam Width:", $"{tier.midshipBeamWidth:F2} m");
                float fwdBeam = (tier.cachedStbdEdgePts != null) ? tier.cachedStbdEdgePts[tier.cachedStbdEdgePts.Length - 1].x * 2f : 0f;
                float aftBeam = (tier.cachedStbdEdgePts != null) ? tier.cachedStbdEdgePts[0].x * 2f : 0f;
                EditorGUILayout.LabelField("End Margin Beams:", $"Fwd Beam: {fwdBeam:F2}m | Aft Beam: {aftBeam:F2}m");
                EditorGUILayout.LabelField("Longitudinal Span:", $"Z {tier.aftZ:F2}m to {tier.forwardZ:F2}m (Length: {tier.forwardZ - tier.aftZ:F2}m)");
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update All Lower Decks", GUILayout.Height(28)))
        {
            Undo.RecordObject(curves, "Update All Lower Decks");
            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);
        }
    }
}
#endif
