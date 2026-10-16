using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// SectionModelCurves generates pure transverse frame section cuts (on the X/Y plane) at equal 
/// intervals down the length of the hull. Each transverse cut samples 3D points directly from 
/// the longitudinal Ribband curves, Primary Hull curves (Floor, Bilge, Breadth, TopLine), 
/// Sheer/Keel profiles, and Fashion curves.
/// 
/// As station Z slides stem-to-stern, the transverse frame points dynamically adjust to the exact 
/// positions dictated by the longitudinal ribbands, visually proving the smoothness and continuity 
/// of the hull lofting.
/// </summary>
[ExecuteInEditMode]
public class SectionModelCurves : MonoBehaviour
{
    [System.Serializable]
    public struct SectionPointNode
    {
        public string name;
        public float uParam; // 0.0 = Keel, 0.25 = Floor, 0.50 = Bilge, 0.75 = Breadth, 1.0 = TopLine / Bulwark Rail
        public Vector3 point;
    }

    [Header("Primary Curve References")]
    [Tooltip("Reference to SheerCurves for profile lines (Stem, Stern, Keel).")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to TopLineCurves for Top Line 3D points.")]
    public TopLineCurves topLineCurves;

    [Tooltip("Reference to BreadthCurves for Max Breadth 3D points.")]
    public BreadthCurves breadthCurves;

    [Tooltip("Reference to BilgeCurves for Diagonal Bilge 3D points.")]
    public BilgeCurves bilgeCurves;

    [Tooltip("Reference to FloorCurves for Floor Line 3D points.")]
    public FloorCurves floorCurves;

    [Tooltip("Reference to BodyCurves for hull section node alignment.")]
    public BodyCurves bodyCurves;

    [Tooltip("Reference to RibbandCurves for intermediate longitudinal battens.")]
    public RibbandCurves ribbandCurves;

    [Tooltip("Reference to FashionCurves for 3D transom stern boundary curve.")]
    public FashionCurves fashionCurves;

    [Tooltip("Reference to BackboneCurves for stem and stern keel width offset.")]
    public BackboneCurves backboneCurves;

    [Tooltip("Reference to UpperDeckCurves for upper deck bulwarks and cap rails.")]
    public UpperDeckCurves upperDeckCurves;

    [Header("Transverse Cut Settings")]
    [Tooltip("Number of forward transverse frame cuts generated between Master Frame (BodyCurves) and Bow Stem.")]
    [Range(1, 50)] public int forwardSectionCount = 10;

    [Tooltip("Number of aft transverse frame cuts generated between Master Frame (BodyCurves) and Stern Transom.")]
    [Range(1, 50)] public int aftSectionCount = 10;

    [Tooltip("Include the central Master Frame section cut at Z = 0 (BodyCurves station).")]
    public bool includeMasterSection = true;

    [HideInInspector] public int sectionCount = 20;

    public int TotalSectionCount => (includeMasterSection ? 1 : 0) + forwardSectionCount + aftSectionCount;

    [Tooltip("Include primary boundary nodes (Floor, Bilge, Breadth, TopLine) alongside Ribbands.")]
    public bool includePrimaryBoundaryNodes = true;

    [Header("Gizmo Visualization Settings")]
    public bool showTransverseSections = true;
    public bool showSectionNodes = true;
    public bool showPolygonSkeleton = false;
    public bool showLabels = true;

    [Range(0.02f, 0.25f)] public float nodeRadius = 0.06f;
    public Color sectionColor = new Color(0.1f, 0.85f, 0.95f, 0.9f);
    public Color starboardColor = new Color(0.2f, 0.95f, 0.4f, 0.95f);
    public Color portColor = new Color(0.95f, 0.25f, 0.4f, 0.95f);

    [Header("Deck Clamp / Shelf Notches")]
    [Tooltip("Display upper deck shelf and clamp landing markers on transverse frame cuts.")]
    public bool showDeckClamps = true;
    [Tooltip("Color of deck clamp and shelf markers.")]
    public Color deckClampColor = new Color(0.95f, 0.65f, 0.20f, 0.90f);

    [Header("Master Frame Settings")]
    [Tooltip("Construct section frames purely from the 5 Master Key Curves (Keel, Floor, Bilge, Breadth, TopLine) to guarantee lump-free hull cuts.")]
    public bool useKeyCurvesOnlyForSections = true;

    [Header("Initialization State")]
    public bool isInitialized = false;

    [HideInInspector] public int changeVersion = 0;

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Reset()
    {
        InitializeSectionModel();
    }

    public void EnsureInitialized()
    {
        if (!isInitialized)
        {
            InitializeSectionModel();
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

        if (bilgeCurves == null) bilgeCurves = GetComponentInParent<BilgeCurves>();
        if (bilgeCurves == null) bilgeCurves = FindFirstObjectByType<BilgeCurves>();

        if (floorCurves == null) floorCurves = GetComponentInParent<FloorCurves>();
        if (floorCurves == null) floorCurves = FindFirstObjectByType<FloorCurves>();

        if (bodyCurves == null) bodyCurves = GetComponentInParent<BodyCurves>();
        if (bodyCurves == null) bodyCurves = FindFirstObjectByType<BodyCurves>();

        if (ribbandCurves == null) ribbandCurves = GetComponentInParent<RibbandCurves>();
        if (ribbandCurves == null) ribbandCurves = FindFirstObjectByType<RibbandCurves>();

        if (fashionCurves == null) fashionCurves = GetComponentInParent<FashionCurves>();
        if (fashionCurves == null) fashionCurves = FindFirstObjectByType<FashionCurves>();

        if (backboneCurves == null) backboneCurves = GetComponentInParent<BackboneCurves>();
        if (backboneCurves == null) backboneCurves = FindFirstObjectByType<BackboneCurves>();

        if (upperDeckCurves == null) upperDeckCurves = GetComponentInParent<UpperDeckCurves>();
        if (upperDeckCurves == null) upperDeckCurves = FindFirstObjectByType<UpperDeckCurves>();
    }

    [ContextMenu("Reset Section Model Curves")]
    public void InitializeSectionModel()
    {
        FindReferences();
        UpdateCachedCurves();
        isInitialized = true;
    }

    private void OnValidate()
    {
        UpdateCachedCurves();
    }

    private bool isUpdatingCurves = false;

    public virtual void UpdateCachedCurves()
    {
        if (isUpdatingCurves) return;
        isUpdatingCurves = true;
        try
        {
            FindReferences();

            if (sheerCurves != null) sheerCurves.UpdateCachedProfile();
            if (topLineCurves != null) topLineCurves.UpdateCachedCurves();
            if (breadthCurves != null) breadthCurves.UpdateCachedCurves();
            if (bilgeCurves != null) bilgeCurves.UpdateCachedCurves();
            if (floorCurves != null) floorCurves.UpdateCachedCurves();
            if (fashionCurves != null) fashionCurves.UpdateCachedCurves();
            if (ribbandCurves != null) ribbandCurves.UpdateCachedCurves();
            if (upperDeckCurves != null) upperDeckCurves.UpdateCachedCurves();

            changeVersion++;
        }
        finally
        {
            isUpdatingCurves = false;
        }
    }

    /// <summary>
    /// Checks whether station Z is physically within the active longitudinal span [aftTip, fwdTip] of a curve.
    /// </summary>
    public bool IsStationWithinCurveSpan(float z, Vector3 fwdTip, Vector3 aftTip, float tolerance = 0.01f)
    {
        float maxZ = Mathf.Max(fwdTip.z, aftTip.z) + tolerance;
        float minZ = Mathf.Min(fwdTip.z, aftTip.z) - tolerance;
        return z >= minZ && z <= maxZ;
    }

    /// <summary>
    /// Gets the ordered transverse section nodes (Keel up to TopLine/Fashion) at station Z 
    /// for the specified side (+1 Starboard, -1 Port).
    /// </summary>
    public List<SectionPointNode> GetTransverseSectionNodesAtZ(float z, float sideSign)
    {
        EnsureInitialized();

        List<SectionPointNode> nodes = new List<SectionPointNode>();
        float sSign = Mathf.Approximately(sideSign, 0f) ? 1f : Mathf.Sign(sideSign);
        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);

        // 1. Keel or Transom Base Node (u = 0.0 or uParam corresponding to Fashion boundary)
        bool isTransomRegion = (fashionCurves != null && z <= fashionCurves.stbdP1.z + 0.001f);
        float minTransomY = -999f;
        if (isTransomRegion)
        {
            Vector3 fashionPt = fashionCurves.GetFashionPointAtZ(z, sSign);
            fashionPt = new Vector3(sSign * Mathf.Abs(fashionPt.x), fashionPt.y, z);
            minTransomY = fashionPt.y;

            float uTransom = 0.25f;
            float ySpan = fashionCurves.stbdP4.y - fashionCurves.stbdP1.y;
            if (ySpan > 0.001f)
            {
                uTransom = Mathf.Lerp(0.25f, 1.00f, Mathf.Clamp01((fashionPt.y - fashionCurves.stbdP1.y) / ySpan));
            }

            nodes.Add(new SectionPointNode
            {
                name = "Fashion Transom Base",
                uParam = uTransom,
                point = fashionPt
            });
        }
        else
        {
            float keelY = 0f;
            if (sheerCurves != null)
            {
                if (z >= sheerCurves.stemP0.z)
                {
                    keelY = sheerCurves.GetStemProfileYAtZ(z);
                }
                else if (z <= sheerCurves.sternP0.z)
                {
                    keelY = sheerCurves.GetSternProfileYAtZ(z);
                }
                else
                {
                    keelY = sheerCurves.keelY;
                }
            }

            nodes.Add(new SectionPointNode
            {
                name = "Keel",
                uParam = 0.0f,
                point = new Vector3(sSign * hw, keelY, z)
            });
        }

        // 2. Sample Primary Boundary Curves if requested and active at station Z
        if (includePrimaryBoundaryNodes)
        {
            if (floorCurves != null && IsStationWithinCurveSpan(z, floorCurves.fwdP0, floorCurves.aftP3))
            {
                Vector3 fPt = floorCurves.GetFloorPointAtZ(z);
                if (!isTransomRegion || fPt.y > minTransomY + 0.01f)
                {
                    nodes.Add(new SectionPointNode
                    {
                        name = "Floor Curve",
                        uParam = 0.25f,
                        point = new Vector3(sSign * Mathf.Abs(fPt.x), fPt.y, z)
                    });
                }
            }

            if (bilgeCurves != null && IsStationWithinCurveSpan(z, bilgeCurves.fwdP0, bilgeCurves.aftP2))
            {
                Vector3 bPt = bilgeCurves.GetBilgePointAtZ(z);
                if (!isTransomRegion || bPt.y > minTransomY + 0.01f)
                {
                    nodes.Add(new SectionPointNode
                    {
                        name = "Bilge Curve",
                        uParam = 0.50f,
                        point = new Vector3(sSign * Mathf.Abs(bPt.x), bPt.y, z)
                    });
                }
            }

            if (breadthCurves != null)
            {
                Vector3 bAft = breadthCurves.cachedIsAftCubic ? breadthCurves.aftP3 : breadthCurves.aftP2;
                if (IsStationWithinCurveSpan(z, breadthCurves.fwdP0, bAft))
                {
                    Vector3 brPt = breadthCurves.GetBreadthPointAtZ(z);
                    if (!isTransomRegion || brPt.y > minTransomY + 0.01f)
                    {
                        nodes.Add(new SectionPointNode
                        {
                            name = "Breadth Curve",
                            uParam = 0.75f,
                            point = new Vector3(sSign * Mathf.Abs(brPt.x), brPt.y, z)
                        });
                    }
                }
            }

            if (topLineCurves != null)
            {
                Vector3 tlAft = topLineCurves.cachedIsAftCubic ? topLineCurves.aftP3 : topLineCurves.aftP2;
                if (IsStationWithinCurveSpan(z, topLineCurves.fwdP0, tlAft))
                {
                    bool isPhysical = (upperDeckCurves == null) || upperDeckCurves.IsHighestPhysicalDeckAtZ(z);
                    if (isPhysical)
                    {
                        Vector3 tlPt = topLineCurves.GetTopLinePointAtZ(z);
                        if (!isTransomRegion || tlPt.y > minTransomY + 0.01f)
                        {
                            nodes.Add(new SectionPointNode
                            {
                                name = "TopLine Curve",
                                uParam = 1.00f,
                                point = new Vector3(sSign * Mathf.Abs(tlPt.x), tlPt.y, z)
                            });
                        }
                    }
                }
            }

            if (upperDeckCurves != null && upperDeckCurves.HasBulwarkAtZ(z, out Vector3 stbdRail, out Vector3 portRail, out Vector3 inStbdRail, out Vector3 inPortRail))
            {
                bool isPhysicalTopLine = (upperDeckCurves == null) || upperDeckCurves.IsHighestPhysicalDeckAtZ(z);
                if (!isPhysicalTopLine)
                {
                    Vector3 railPt = (sSign > 0f) ? stbdRail : portRail;
                    if (!isTransomRegion || railPt.y > minTransomY + 0.01f)
                    {
                        nodes.Add(new SectionPointNode
                        {
                            name = "Upper Deck Bulwark Rail",
                            uParam = 1.00f,
                            point = new Vector3(sSign * Mathf.Abs(railPt.x), railPt.y, z)
                        });
                    }
                }
            }
        }

        // 3. Sample Longitudinal Ribbands from RibbandCurves if enabled (discard if station Z is outside ribband span)
        if (!useKeyCurvesOnlyForSections && ribbandCurves != null && ribbandCurves.generatedRibbands != null)
        {
            for (int i = 0; i < ribbandCurves.generatedRibbands.Count; i++)
            {
                RibbandCurves.SingleRibband rib = ribbandCurves.generatedRibbands[i];
                if (IsStationWithinCurveSpan(z, rib.fwdP0, rib.aftP2))
                {
                    Vector3 ribPt = ribbandCurves.GetRibbandPointAtZ(i, z);
                    if (!isTransomRegion || ribPt.y > minTransomY + 0.01f)
                    {
                        nodes.Add(new SectionPointNode
                        {
                            name = rib.name,
                            uParam = rib.uSectionParam,
                            point = new Vector3(sSign * Mathf.Abs(ribPt.x), ribPt.y, z)
                        });
                    }
                }
            }
        }

        // 4. Sort nodes strictly by uParam (from Keel/Transom uParam to TopLine u=1)
        nodes.Sort((a, b) => a.uParam.CompareTo(b.uParam));

        // 5. Remove any duplicate nodes positioned at almost identical locations
        List<SectionPointNode> uniqueNodes = new List<SectionPointNode>();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (uniqueNodes.Count == 0)
            {
                uniqueNodes.Add(nodes[i]);
            }
            else
            {
                Vector3 prev = uniqueNodes[uniqueNodes.Count - 1].point;
                if (Vector3.Distance(prev, nodes[i].point) > 0.005f)
                {
                    uniqueNodes.Add(nodes[i]);
                }
            }
        }

        return uniqueNodes;
    }

    /// <summary>
    /// Gets the ordered transverse inner section nodes (from Inner Fashion Base or Inner Keel up to Inner TopLine)
    /// at station Z for specified side (+1 Starboard, -1 Port).
    /// Guarantees that cut frames lock 100% to the Inner Fashion Curve and longitudinal inner lines.
    /// </summary>
    public List<SectionPointNode> GetTransverseInnerSectionNodesAtZ(float z, float sideSign)
    {
        EnsureInitialized();

        List<SectionPointNode> nodes = new List<SectionPointNode>();
        float sSign = Mathf.Approximately(sideSign, 0f) ? 1f : Mathf.Sign(sideSign);
        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);
        float bottomSpacing = (bodyCurves != null) ? bodyCurves.innerLineSpacing : 0.2f;

        bool isTransomRegion = (fashionCurves != null && z <= fashionCurves.stbdP1.z + 0.001f);
        float minInnerTransomY = -999f;

        if (isTransomRegion)
        {
            Vector3 inFashionPt = fashionCurves.GetInnerFashionPointAtZ(z, sSign);
            inFashionPt = new Vector3(sSign * Mathf.Abs(inFashionPt.x), inFashionPt.y, z);
            minInnerTransomY = inFashionPt.y;

            float uTransomInner = 0.25f;
            float ySpanInner = (fashionCurves.innerStbdP4.y - fashionCurves.innerStbdP1.y);
            if (ySpanInner > 0.001f)
            {
                uTransomInner = Mathf.Lerp(0.25f, 1.00f, Mathf.Clamp01((inFashionPt.y - fashionCurves.innerStbdP1.y) / ySpanInner));
            }

            nodes.Add(new SectionPointNode
            {
                name = "Inner Fashion Transom Base",
                uParam = uTransomInner,
                point = inFashionPt
            });

            if (includePrimaryBoundaryNodes)
            {
                if (bilgeCurves != null && bilgeCurves.showInnerLine)
                {
                    Vector3 bAft = bilgeCurves.GetInnerBilgeSternTip(true);
                    Vector3 bFwd = bilgeCurves.GetInnerBilgeBowTip(true);
                    if (IsStationWithinCurveSpan(z, bFwd, bAft))
                    {
                        Vector3 inBilge = bilgeCurves.GetInnerBilgePointAtZ(z);
                        if (inBilge.y > minInnerTransomY + 0.01f)
                        {
                            nodes.Add(new SectionPointNode
                            {
                                name = "Inner Bilge Curve",
                                uParam = 0.50f,
                                point = new Vector3(sSign * Mathf.Abs(inBilge.x), inBilge.y, z)
                            });
                        }
                    }
                }

                if (breadthCurves != null && breadthCurves.showInnerLine)
                {
                    Vector3 brAft = breadthCurves.GetInnerBreadthSternTip(true);
                    Vector3 brFwd = breadthCurves.GetInnerBreadthBowTip(true);
                    if (IsStationWithinCurveSpan(z, brFwd, brAft))
                    {
                        Vector3 inBreadth = breadthCurves.GetInnerBreadthPointAtZ(z, sSign > 0f);
                        if (inBreadth.y > minInnerTransomY + 0.01f)
                        {
                            nodes.Add(new SectionPointNode
                            {
                                name = "Inner Breadth Curve",
                                uParam = 0.75f,
                                point = new Vector3(sSign * Mathf.Abs(inBreadth.x), inBreadth.y, z)
                            });
                        }
                    }
                }

                if (topLineCurves != null && topLineCurves.showInnerLine)
                {
                    Vector3 tlAft = topLineCurves.GetInnerTopLineSternTip(true);
                    Vector3 tlFwd = topLineCurves.GetInnerTopLineBowTip(true);
                    if (IsStationWithinCurveSpan(z, tlFwd, tlAft))
                    {
                        bool isPhysical = (upperDeckCurves == null) || upperDeckCurves.IsHighestPhysicalDeckAtZ(z);
                        if (isPhysical)
                        {
                            Vector3 inTopLine = topLineCurves.GetInnerTopLinePointAtZ(z, sSign > 0f);
                            if (inTopLine.y > minInnerTransomY + 0.01f)
                            {
                                nodes.Add(new SectionPointNode
                                {
                                    name = "Inner TopLine Curve",
                                    uParam = 1.00f,
                                    point = new Vector3(sSign * Mathf.Abs(inTopLine.x), inTopLine.y, z)
                                });
                            }
                        }
                    }
                }

                if ((topLineCurves == null || topLineCurves.showInnerLine) && (bodyCurves == null || bodyCurves.showInnerLine))
                {
                    if (upperDeckCurves != null && upperDeckCurves.HasBulwarkAtZ(z, out Vector3 stbdRail, out Vector3 portRail, out Vector3 inStbdRail, out Vector3 inPortRail))
                    {
                        bool isPhysicalTopLine = (upperDeckCurves == null) || upperDeckCurves.IsHighestPhysicalDeckAtZ(z);
                        if (!isPhysicalTopLine)
                        {
                            Vector3 inRailPt = (sSign > 0f) ? inStbdRail : inPortRail;
                            if (inRailPt.y > minInnerTransomY + 0.01f)
                            {
                                nodes.Add(new SectionPointNode
                                {
                                    name = "Inner Upper Deck Bulwark Rail",
                                    uParam = 1.00f,
                                    point = new Vector3(sSign * Mathf.Abs(inRailPt.x), inRailPt.y, z)
                                });
                            }
                        }
                    }
                }
            }
        }
        else
        {
            float keelY = 0f;
            if (sheerCurves != null)
            {
                if (z >= sheerCurves.stemP0.z)
                {
                    keelY = sheerCurves.GetStemProfileYAtZ(z);
                }
                else if (z <= sheerCurves.sternP0.z)
                {
                    keelY = sheerCurves.GetSternProfileYAtZ(z);
                }
                else
                {
                    keelY = sheerCurves.keelY;
                }
            }

            // Determine projected deadrise height at centerline X = 0 from lowest active curve
            float yKeelTop = keelY + bottomSpacing;
            float yCenter = yKeelTop;

            if (floorCurves != null && floorCurves.showInnerLine)
            {
                Vector3 fAft = floorCurves.GetInnerFloorSternTip(true);
                Vector3 fFwd = floorCurves.GetInnerFloorBowTip(true);
                if (IsStationWithinCurveSpan(z, fFwd, fAft))
                {
                    Vector3 outerFloor = floorCurves.GetFloorPointAtZ(z);
                    Vector3 inFloor = floorCurves.GetInnerFloorPointAtZ(z);
                    float dX = Mathf.Abs(outerFloor.x) - hw;
                    float dY = outerFloor.y - keelY;
                    if (dX > 0.005f)
                    {
                        float deadriseSlope = dY / dX;
                        yCenter = inFloor.y - deadriseSlope * Mathf.Abs(inFloor.x);
                    }
                }
            }
            else if (bilgeCurves != null && bilgeCurves.showInnerLine)
            {
                Vector3 bAft = bilgeCurves.GetInnerBilgeSternTip(true);
                Vector3 bFwd = bilgeCurves.GetInnerBilgeBowTip(true);
                if (IsStationWithinCurveSpan(z, bFwd, bAft))
                {
                    Vector3 outerBilge = bilgeCurves.GetBilgePointAtZ(z);
                    Vector3 inBilge = bilgeCurves.GetInnerBilgePointAtZ(z);
                    float dX = Mathf.Abs(outerBilge.x) - hw;
                    float dY = outerBilge.y - keelY;
                    if (dX > 0.005f)
                    {
                        float deadriseSlope = dY / dX;
                        yCenter = inBilge.y - deadriseSlope * Mathf.Abs(inBilge.x);
                    }
                }
            }

            float yCross = Mathf.Max(yKeelTop, yCenter);

            // Allow the inner line to naturally project across the centerline (into opposite side)
            // so the resulting spline crosses X = 0 and the portion beyond the crossing can be discarded
            float xExt = -0.25f * hw;
            nodes.Add(new SectionPointNode
            {
                name = "Inner Keel Extension",
                uParam = 0.0f,
                point = new Vector3(sSign * xExt, yCross, z)
            });

            if (includePrimaryBoundaryNodes)
            {
                if (floorCurves != null && floorCurves.showInnerLine)
                {
                    Vector3 fAft = floorCurves.GetInnerFloorSternTip(true);
                    Vector3 fFwd = floorCurves.GetInnerFloorBowTip(true);
                    if (IsStationWithinCurveSpan(z, fFwd, fAft))
                    {
                        Vector3 inFloor = floorCurves.GetInnerFloorPointAtZ(z);
                        nodes.Add(new SectionPointNode
                        {
                            name = "Inner Floor Curve",
                            uParam = 0.25f,
                            point = new Vector3(sSign * Mathf.Abs(inFloor.x), inFloor.y, z)
                        });
                    }
                }

                if (bilgeCurves != null && bilgeCurves.showInnerLine)
                {
                    Vector3 bAft = bilgeCurves.GetInnerBilgeSternTip(true);
                    Vector3 bFwd = bilgeCurves.GetInnerBilgeBowTip(true);
                    if (IsStationWithinCurveSpan(z, bFwd, bAft))
                    {
                        Vector3 inBilge = bilgeCurves.GetInnerBilgePointAtZ(z);
                        nodes.Add(new SectionPointNode
                        {
                            name = "Inner Bilge Curve",
                            uParam = 0.50f,
                            point = new Vector3(sSign * Mathf.Abs(inBilge.x), inBilge.y, z)
                        });
                    }
                }

                if (breadthCurves != null && breadthCurves.showInnerLine)
                {
                    Vector3 brAft = breadthCurves.GetInnerBreadthSternTip(true);
                    Vector3 brFwd = breadthCurves.GetInnerBreadthBowTip(true);
                    if (IsStationWithinCurveSpan(z, brFwd, brAft))
                    {
                        Vector3 inBreadth = breadthCurves.GetInnerBreadthPointAtZ(z, sSign > 0f);
                        nodes.Add(new SectionPointNode
                        {
                            name = "Inner Breadth Curve",
                            uParam = 0.75f,
                            point = new Vector3(sSign * Mathf.Abs(inBreadth.x), inBreadth.y, z)
                        });
                    }
                }

                if (topLineCurves != null && topLineCurves.showInnerLine)
                {
                    Vector3 tlAft = topLineCurves.GetInnerTopLineSternTip(true);
                    Vector3 tlFwd = topLineCurves.GetInnerTopLineBowTip(true);
                    if (IsStationWithinCurveSpan(z, tlFwd, tlAft))
                    {
                        Vector3 inTopLine = topLineCurves.GetInnerTopLinePointAtZ(z, sSign > 0f);
                        nodes.Add(new SectionPointNode
                        {
                            name = "Inner TopLine Curve",
                            uParam = 1.00f,
                            point = new Vector3(sSign * Mathf.Abs(inTopLine.x), inTopLine.y, z)
                        });
                    }
                }

                if ((topLineCurves == null || topLineCurves.showInnerLine) && (bodyCurves == null || bodyCurves.showInnerLine))
                {
                    if (upperDeckCurves != null && upperDeckCurves.HasBulwarkAtZ(z, out Vector3 stbdRail, out Vector3 portRail, out Vector3 inStbdRail, out Vector3 inPortRail))
                    {
                        Vector3 inRailPt = (sSign > 0f) ? inStbdRail : inPortRail;
                        nodes.Add(new SectionPointNode
                        {
                            name = "Inner Upper Deck Bulwark Rail",
                            uParam = 1.00f,
                            point = new Vector3(sSign * Mathf.Abs(inRailPt.x), inRailPt.y, z)
                        });
                    }
                }
            }
        }

        // Sample Longitudinal Inner Ribbands if enabled (discard if station Z is outside ribband span)
        if (!useKeyCurvesOnlyForSections && ribbandCurves != null && ribbandCurves.showInnerLines && ribbandCurves.generatedRibbands != null)
        {
            for (int i = 0; i < ribbandCurves.generatedRibbands.Count; i++)
            {
                RibbandCurves.SingleRibband rib = ribbandCurves.generatedRibbands[i];
                Vector3 ribFwd = ribbandCurves.GetInnerRibbandBowTip(i, true);
                Vector3 ribAft = ribbandCurves.GetInnerRibbandSternTip(i, true);
                if (IsStationWithinCurveSpan(z, ribFwd, ribAft))
                {
                    Vector3 inRibPt = ribbandCurves.GetInnerRibbandPointAtZ(i, z);
                    if (!isTransomRegion || inRibPt.y > minInnerTransomY + 0.01f)
                    {
                        nodes.Add(new SectionPointNode
                        {
                            name = $"Inner {rib.name}",
                            uParam = rib.uSectionParam,
                            point = new Vector3(sSign * Mathf.Abs(inRibPt.x), inRibPt.y, z)
                        });
                    }
                }
            }
        }

        // Sort nodes strictly by uParam
        nodes.Sort((a, b) => a.uParam.CompareTo(b.uParam));

        // Deduplicate nodes
        List<SectionPointNode> uniqueNodes = new List<SectionPointNode>();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (uniqueNodes.Count == 0)
            {
                uniqueNodes.Add(nodes[i]);
            }
            else
            {
                Vector3 prev = uniqueNodes[uniqueNodes.Count - 1].point;
                if (Vector3.Distance(prev, nodes[i].point) > 0.005f)
                {
                    uniqueNodes.Add(nodes[i]);
                }
            }
        }

        return uniqueNodes;
    }

    public struct StationInfo
    {
        public float z;
        public string label;
        public bool isMasterFrame;
        public bool isForward;
        public bool isAft;
    }

    /// <summary>
    /// Computes all Z stations for transverse frame cuts divided into Forward Sections (Master Frame to Bow) 
    /// and Aft Sections (Master Frame to Stern), ordered stern-to-stem.
    /// </summary>
    public List<StationInfo> GetAllSectionStations()
    {
        EnsureInitialized();
        List<StationInfo> stations = new List<StationInfo>();

        float masterZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
        float bowZ = (sheerCurves != null) ? sheerCurves.stemP3.z : 15f;
        float sternZ = (fashionCurves != null) ? fashionCurves.stbdP4.z : ((sheerCurves != null) ? sheerCurves.sternP3.z : -15f);

        // 1. Aft Sections (from Stern Z up to near Master Frame Z)
        int aftCount = Mathf.Max(1, aftSectionCount);
        for (int i = aftCount; i >= 1; i--)
        {
            float t = (float)i / aftCount;
            float z = Mathf.Lerp(masterZ, sternZ, t);
            stations.Add(new StationInfo
            {
                z = z,
                label = $"Aft Sec {i} (Z={z:F2}m)",
                isMasterFrame = false,
                isForward = false,
                isAft = true
            });
        }

        // 2. Master Section Cut (at Z = masterZ)
        if (includeMasterSection)
        {
            stations.Add(new StationInfo
            {
                z = masterZ,
                label = $"Master Frame (Z={masterZ:F2}m)",
                isMasterFrame = true,
                isForward = false,
                isAft = false
            });
        }

        // 3. Forward Sections (from near Master Frame Z out to Bow Stem Z)
        int fwdCount = Mathf.Max(1, forwardSectionCount);
        for (int i = 1; i <= fwdCount; i++)
        {
            float t = (float)i / fwdCount;
            float z = Mathf.Lerp(masterZ, bowZ, t);
            stations.Add(new StationInfo
            {
                z = z,
                label = $"Fwd Sec {i} (Z={z:F2}m)",
                isMasterFrame = false,
                isForward = true,
                isAft = false
            });
        }

        return stations;
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

        List<StationInfo> stations = GetAllSectionStations();

        for (int s = 0; s < stations.Count; s++)
        {
            StationInfo info = stations[s];
            float z = info.z;

            // Sample Starboard and Port transverse section nodes
            List<SectionPointNode> stbdNodes = GetTransverseSectionNodesAtZ(z, 1f);
            List<SectionPointNode> portNodes = GetTransverseSectionNodesAtZ(z, -1f);

            Color stbdCol = info.isMasterFrame ? Color.yellow : starboardColor;
            Color portCol = info.isMasterFrame ? Color.yellow : portColor;

            // Compute full outer section polylines BEFORE discarding points
            List<Vector3> stbdOuterPts = GetOuterSectionPolylinePoints(stbdNodes);
            List<Vector3> portOuterPts = GetOuterSectionPolylinePoints(portNodes);

            float bottomSpacing = (bodyCurves != null) ? bodyCurves.innerLineSpacing : 0.2f;
            bool hasBulwarkAtStation = (upperDeckCurves != null && upperDeckCurves.HasBulwarkAtZ(z, out _, out _, out _, out _));
            float topSpacing = (bodyCurves != null) ? (hasBulwarkAtStation ? bodyCurves.topInnerLineSpacing : bodyCurves.GetInnerLineSpacingAtY(stbdNodes.Count > 0 ? stbdNodes[stbdNodes.Count - 1].point.y : 0f)) : 0.1f;

            bool shouldComputeInner = showTransverseSections && (bodyCurves != null && bodyCurves.showInnerLine && bodyCurves.innerLineSpacing > 0.0001f);
            bool isTransomRegion = (fashionCurves != null && z <= fashionCurves.stbdP1.z + 0.001f);

            // Compute inner section polylines: in the transom region, sample directly from inner curve nodes
            List<Vector3> stbdInnerPts = null;
            List<Vector3> portInnerPts = null;
            List<SectionPointNode> stbdInnerNodes = null;
            List<SectionPointNode> portInnerNodes = null;

            if (shouldComputeInner)
            {
                if (isTransomRegion || useKeyCurvesOnlyForSections || ribbandCurves == null || ribbandCurves.generatedRibbands == null || ribbandCurves.generatedRibbands.Count == 0)
                {
                    stbdInnerNodes = GetTransverseInnerSectionNodesAtZ(z, 1f);
                    portInnerNodes = GetTransverseInnerSectionNodesAtZ(z, -1f);

                    stbdInnerPts = (stbdInnerNodes.Count >= 2) ? GetOuterSectionPolylinePoints(stbdInnerNodes) : null;
                    portInnerPts = (portInnerNodes.Count >= 2) ? GetOuterSectionPolylinePoints(portInnerNodes) : null;
                }
                else
                {
                    stbdInnerPts = (stbdNodes.Count >= 2) ? GetInnerSectionPolylinePoints(stbdOuterPts, stbdNodes[0].point.y, stbdNodes[stbdNodes.Count - 1].point.y, bottomSpacing, topSpacing, 1f) : null;
                    portInnerPts = (portNodes.Count >= 2) ? GetInnerSectionPolylinePoints(portOuterPts, portNodes[0].point.y, portNodes[portNodes.Count - 1].point.y, bottomSpacing, topSpacing, -1f) : null;
                }
            }

            // Discard points below fashion / transom boundary (filter each line at its OWN base height)
            if (isTransomRegion)
            {
                float stbdMinY = (stbdNodes != null && stbdNodes.Count > 0) ? stbdNodes[0].point.y : fashionCurves.GetFashionPointAtZ(z, 1f).y;
                float portMinY = (portNodes != null && portNodes.Count > 0) ? portNodes[0].point.y : fashionCurves.GetFashionPointAtZ(z, -1f).y;

                FilterPointsBelowY(stbdOuterPts, stbdMinY);
                FilterPointsBelowY(portOuterPts, portMinY);

                float stbdInnerMinY = (stbdInnerNodes != null && stbdInnerNodes.Count > 0) ? stbdInnerNodes[0].point.y : ((fashionCurves != null) ? fashionCurves.GetInnerFashionPointAtZ(z, 1f).y : stbdMinY);
                float portInnerMinY = (portInnerNodes != null && portInnerNodes.Count > 0) ? portInnerNodes[0].point.y : ((fashionCurves != null) ? fashionCurves.GetInnerFashionPointAtZ(z, -1f).y : portMinY);

                if (stbdInnerPts != null) FilterPointsBelowY(stbdInnerPts, stbdInnerMinY);
                if (portInnerPts != null) FilterPointsBelowY(portInnerPts, portInnerMinY);
            }
            else
            {
                // Discard inner curve portion extending beyond centerline crossing point (clipping at X = 0)
                if (shouldComputeInner)
                {
                    if (stbdInnerPts != null) ClipInnerPolylineAtCenterline(stbdInnerPts, true);
                    if (portInnerPts != null) ClipInnerPolylineAtCenterline(portInnerPts, false);
                }
            }

            // Draw Transverse Section Curves (X/Y cuts)
            if (showTransverseSections)
            {
                DrawPolyline(stbdOuterPts, stbdCol);
                DrawPolyline(portOuterPts, portCol);

                // Outer-to-Outer Bottom Connection across Keel width (ONLY ahead of transom cut)
                if (!isTransomRegion && stbdOuterPts.Count > 0 && portOuterPts.Count > 0)
                {
                    Gizmos.color = stbdCol;
                    Gizmos.DrawLine(portOuterPts[0], stbdOuterPts[0]);
                }

                if (bodyCurves != null && bodyCurves.showInnerLine && bodyCurves.innerLineSpacing > 0.0001f && stbdInnerPts != null && portInnerPts != null)
                {
                    DrawPolyline(stbdInnerPts, bodyCurves.innerLineColor);
                    DrawPolyline(portInnerPts, bodyCurves.innerLineColor);

                    if (isTransomRegion)
                    {
                        // Transom Cut End Cap Lines (Outer cut to Inner cut on each side)
                        if (stbdOuterPts.Count > 0 && stbdInnerPts.Count > 0)
                        {
                            Gizmos.color = bodyCurves.innerLineColor;
                            Gizmos.DrawLine(stbdOuterPts[0], stbdInnerPts[0]);
                        }
                        if (portOuterPts.Count > 0 && portInnerPts.Count > 0)
                        {
                            Gizmos.color = bodyCurves.innerLineColor;
                            Gizmos.DrawLine(portOuterPts[0], portInnerPts[0]);
                        }
                    }
                    else
                    {
                        // Inner Keel Floor Connection across Keel width
                        if (stbdInnerPts.Count > 0 && portInnerPts.Count > 0)
                        {
                            Gizmos.color = bodyCurves.innerLineColor;
                            Gizmos.DrawLine(portInnerPts[0], stbdInnerPts[0]);
                        }
                    }

                    // Outer-to-Inner Top Cap Lines
                    if (stbdOuterPts.Count > 0 && stbdInnerPts.Count > 0)
                    {
                        Gizmos.color = bodyCurves.innerLineColor;
                        Gizmos.DrawLine(stbdOuterPts[stbdOuterPts.Count - 1], stbdInnerPts[stbdInnerPts.Count - 1]);
                    }
                    if (portOuterPts.Count > 0 && portInnerPts.Count > 0)
                    {
                        Gizmos.color = bodyCurves.innerLineColor;
                        Gizmos.DrawLine(portOuterPts[portOuterPts.Count - 1], portInnerPts[portInnerPts.Count - 1]);
                    }
                }

                // Draw Upper Deck Clamp and Shelf Landing Notches on Frame Cuts
                if (showDeckClamps && upperDeckCurves != null && upperDeckCurves.GetDeckEdgePointsAtZ(z, out Vector3 stbdEdge, out Vector3 portEdge, out Vector3 stbdBotEdge, out Vector3 portBotEdge))
                {
                    Gizmos.color = deckClampColor;
                    // Draw deck edge waterway to shelf connection along frame inner face
                    Gizmos.DrawLine(stbdEdge, stbdBotEdge);
                    Gizmos.DrawLine(portEdge, portBotEdge);

                    // Draw shelf clamp timber protruding inward from frame face to support deck beam
                    float clampInwardDepth = 0.15f;
                    Vector3 stbdShelfIn = stbdBotEdge + Vector3.left * clampInwardDepth;
                    Vector3 portShelfIn = portBotEdge + Vector3.right * clampInwardDepth;

                    Gizmos.DrawLine(stbdBotEdge, stbdShelfIn);
                    Gizmos.DrawLine(portBotEdge, portShelfIn);
                    Gizmos.DrawLine(stbdShelfIn, stbdShelfIn + Vector3.up * Mathf.Max(0.02f, stbdEdge.y - stbdBotEdge.y));
                    Gizmos.DrawLine(portShelfIn, portShelfIn + Vector3.up * Mathf.Max(0.02f, portEdge.y - portBotEdge.y));

                    Gizmos.DrawSphere(stbdBotEdge, 0.03f);
                    Gizmos.DrawSphere(portBotEdge, 0.03f);

#if UNITY_EDITOR
                    if (showLabels)
                    {
                        GUIStyle labelStyle = GetCachedLabelStyle();
                        Handles.Label(stbdShelfIn + Vector3.up * 0.05f, "Deck Clamp", labelStyle);
                    }
#endif
                }
            }

            // Draw Node Spheres at Ribband Intersections
            if (showSectionNodes)
            {
                Gizmos.color = stbdCol;
                for (int n = 0; n < stbdNodes.Count; n++)
                {
                    Gizmos.DrawSphere(stbdNodes[n].point, nodeRadius);
                }

                Gizmos.color = portCol;
                for (int n = 0; n < portNodes.Count; n++)
                {
                    Gizmos.DrawSphere(portNodes[n].point, nodeRadius);
                }
            }

            // Draw Polygon Skeleton connecting nodes
            if (showPolygonSkeleton)
            {
                Gizmos.color = new Color(sectionColor.r, sectionColor.g, sectionColor.b, 0.4f);
                DrawSectionSkeleton(stbdNodes);
                DrawSectionSkeleton(portNodes);
            }

#if UNITY_EDITOR
            if (showLabels && stbdNodes.Count > 0)
            {
                GUIStyle labelStyle = GetCachedLabelStyle();
                Vector3 topPt = stbdNodes[stbdNodes.Count - 1].point;
                Handles.Label(topPt + Vector3.up * 0.2f, info.label, labelStyle);
            }
#endif
        }
    }

    private List<Vector3> GetOuterSectionPolylinePoints(List<SectionPointNode> nodes, int stepsPerSegment = 8)
    {
        List<Vector3> outerPoints = new List<Vector3>();
        if (nodes == null || nodes.Count < 2) return outerPoints;

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Vector3 p0 = (i == 0) ? nodes[i].point : nodes[i - 1].point;
            Vector3 p1 = nodes[i].point;
            Vector3 p2 = nodes[i + 1].point;
            Vector3 p3 = (i + 2 < nodes.Count) ? nodes[i + 2].point : nodes[i + 1].point;

            int startS = (i == 0) ? 0 : 1;
            for (int s = startS; s <= stepsPerSegment; s++)
            {
                float t = (float)s / stepsPerSegment;
                outerPoints.Add(GetCatmullRomPoint(t, p0, p1, p2, p3));
            }
        }
        return outerPoints;
    }

    private List<Vector3> GetInnerSectionPolylinePoints(List<Vector3> outerPoints, float keelY, float topY, float bottomSpacing, float topSpacing, float sideSign)
    {
        if (outerPoints == null || outerPoints.Count < 2 || bottomSpacing <= 0.0001f) return null;
        topSpacing = Mathf.Clamp(topSpacing, 0f, bottomSpacing);

        float totalYSpan = topY - keelY;
        if (totalYSpan <= 0.0001f) totalYSpan = 1.0f;

        List<Vector3> innerPoints = new List<Vector3>(outerPoints.Count);

        for (int i = 0; i < outerPoints.Count; i++)
        {
            Vector3 curr = outerPoints[i];

            // Evaluate uTrue based on height relative to full hull span [keelY, topY]
            float uTrue = Mathf.Clamp01((curr.y - keelY) / totalYSpan);
            float currentSpacing = Mathf.Lerp(bottomSpacing, topSpacing, uTrue);

            Vector3 prev = (i == 0) ? outerPoints[0] : outerPoints[i - 1];
            Vector3 next = (i == outerPoints.Count - 1) ? outerPoints[outerPoints.Count - 1] : outerPoints[i + 1];

            Vector3 tangent = (next - prev);
            if (tangent.sqrMagnitude < 0.000001f)
            {
                tangent = (i > 0) ? (curr - outerPoints[i - 1]) : (outerPoints[1] - curr);
            }
            Vector2 t2D = new Vector2(tangent.x, tangent.y).normalized;

            Vector2 normal2D = (sideSign >= 0f) ? new Vector2(-t2D.y, t2D.x) : new Vector2(t2D.y, -t2D.x);

            Vector3 offsetPt = curr + new Vector3(normal2D.x, normal2D.y, 0f) * currentSpacing;
            offsetPt.y = Mathf.Min(offsetPt.y, topY);
            innerPoints.Add(offsetPt);
        }

        if (innerPoints.Count > 0)
        {
            Vector3 topPt = innerPoints[innerPoints.Count - 1];
            topPt.y = topY;
            innerPoints[innerPoints.Count - 1] = topPt;
        }

        return innerPoints;
    }

    private void FilterPointsBelowY(List<Vector3> pts, float minY)
    {
        if (pts == null || pts.Count == 0) return;
        pts.RemoveAll(p => p.y < minY - 0.005f);
        if (pts.Count > 0 && pts[0].y < minY)
        {
            Vector3 p = pts[0];
            p.y = minY;
            pts[0] = p;
        }
    }

    /// <summary>
    /// Clips the inner section polyline at the centerline plane (X = 0).
    /// If the curve extends past the centerline crossing point (X < 0 for Starboard, X > 0 for Port),
    /// the tail extending beyond the crossing is discarded, and the exact crossing point at X = 0
    /// becomes the base endpoint of the inner line.
    /// If the curve stops short of X = 0, it is extended to the centerline.
    /// </summary>
    private void ClipInnerPolylineAtCenterline(List<Vector3> pts, bool isStarboard)
    {
        if (pts == null || pts.Count < 2) return;

        // Starboard valid domain is X >= 0; Port valid domain is X <= 0.
        bool startsOnOppositeSide = isStarboard ? (pts[0].x < -0.0001f) : (pts[0].x > 0.0001f);

        if (startsOnOppositeSide)
        {
            // Find the first segment that crosses the centerline plane (X = 0)
            int crossIdx = -1;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                bool curOpposite = isStarboard ? (pts[i].x < 0f) : (pts[i].x > 0f);
                bool nextCorrect = isStarboard ? (pts[i + 1].x >= 0f) : (pts[i + 1].x <= 0f);

                if (curOpposite && nextCorrect)
                {
                    crossIdx = i;
                    break;
                }
            }

            if (crossIdx >= 0)
            {
                Vector3 pA = pts[crossIdx];
                Vector3 pB = pts[crossIdx + 1];

                float dx = pB.x - pA.x;
                float t = (Mathf.Abs(dx) > 0.00001f) ? Mathf.Clamp01((0f - pA.x) / dx) : 0f;
                Vector3 crossPt = Vector3.Lerp(pA, pB, t);
                crossPt.x = 0f;

                // Discard all points extending beyond the crossing point
                pts.RemoveRange(0, crossIdx + 1);

                // Insert exact crossing point at index 0
                pts.Insert(0, crossPt);
            }
            else
            {
                Vector3 p0 = pts[0];
                p0.x = 0f;
                pts[0] = p0;
            }
        }
        else
        {
            // If the curve stopped short of the centerline, extend cleanly to X = 0
            float threshold = 0.001f;
            bool stoppedShort = isStarboard ? (pts[0].x > threshold) : (pts[0].x < -threshold);
            if (stoppedShort)
            {
                Vector3 extPt = pts[0];
                extPt.x = 0f;
                pts.Insert(0, extPt);
            }
            else
            {
                Vector3 p0 = pts[0];
                p0.x = 0f;
                pts[0] = p0;
            }
        }
    }

    private void DrawPolyline(List<Vector3> pts, Color color)
    {
        if (pts == null || pts.Count < 2) return;
        Gizmos.color = color;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Gizmos.DrawLine(pts[i], pts[i + 1]);
        }
    }

    private void DrawSectionSkeleton(List<SectionPointNode> nodes)
    {
        if (nodes == null || nodes.Count < 2) return;
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Gizmos.DrawLine(nodes[i].point, nodes[i + 1].point);
        }
    }

    private Vector3 GetCatmullRomPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float alpha = 0.5f)
    {
        if (p0 == p1 && p1 == p2 && p2 == p3) return p1;

        float GetT(float tPrev, Vector3 pA, Vector3 pB)
        {
            float d = Vector3.Distance(pA, pB);
            return tPrev + Mathf.Max(0.0001f, Mathf.Pow(d, alpha));
        }

        float t0 = 0f;
        float t1 = GetT(t0, p0, p1);
        float t2 = GetT(t1, p1, p2);
        float t3 = GetT(t2, p2, p3);

        float u = Mathf.Lerp(t1, t2, t);

        Vector3 a1 = (t1 - u) / (t1 - t0) * p0 + (u - t0) / (t1 - t0) * p1;
        Vector3 a2 = (t2 - u) / (t2 - t1) * p1 + (u - t1) / (t2 - t1) * p2;
        Vector3 a3 = (t3 - u) / (t3 - t2) * p2 + (u - t2) / (t3 - t2) * p3;

        Vector3 b1 = (t2 - u) / (t2 - t0) * a1 + (u - t0) / (t2 - t0) * a2;
        Vector3 b2 = (t3 - u) / (t3 - t1) * a2 + (u - t1) / (t3 - t1) * a3;

        return (t2 - u) / (t2 - t1) * b1 + (u - t1) / (t2 - t1) * b2;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SectionModelCurves))]
public class SectionModelCurvesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SectionModelCurves model = (SectionModelCurves)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Transverse Section Model Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Forward Frame Cuts:", $"{model.forwardSectionCount}");
        EditorGUILayout.LabelField("Aft Frame Cuts:", $"{model.aftSectionCount}");
        EditorGUILayout.LabelField("Master Frame Rendered:", model.includeMasterSection ? "Yes (Z=0.0m)" : "No");
        EditorGUILayout.LabelField("Total Frame Cuts Rendered:", $"{model.TotalSectionCount}");

        if (model.ribbandCurves != null)
        {
            EditorGUILayout.LabelField("Tracked Ribbands per Section:", $"{model.ribbandCurves.generatedRibbands.Count}");
        }

        if (model.upperDeckCurves != null && model.showDeckClamps)
        {
            EditorGUILayout.LabelField("Upper Deck Clamps:", "Active & Linked");
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update Section Model Curves", GUILayout.Height(30)))
        {
            Undo.RecordObject(model, "Update Section Model Profile");
            model.UpdateCachedCurves();
            EditorUtility.SetDirty(model);
        }
    }
}
#endif
