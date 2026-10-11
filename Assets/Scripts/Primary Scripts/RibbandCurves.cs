using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// RibbandCurves generates a configurable collection of intermediate 3D spatial Bezier ribband curves
/// (longitudinal battens / planking guide lines) across specified anatomical hull regions.
/// In FullHullSpan mode, it generates ribbands across all 3 distinct bands simultaneously:
/// 1. Upper Band (Breadth to Bilge)
/// 2. Middle Band (Bilge to Floor)
/// 3. Lower Band (Floor to Keel)
/// </summary>
[ExecuteInEditMode]
public class RibbandCurves : MonoBehaviour
{
    public enum RibbandRegion
    {
        TopLineToBreadth,
        BreadthToBilge,
        BilgeToFloor,
        FloorToKeel,
        FullHullSpan
    }

    [System.Serializable]
    public class SingleRibband
    {
        public string name = "Ribband";
        public string subRegionName = "Upper";
        public int index;
        public float tRatio;
        public float uSectionParam;

        // Forward Cubic 3D Control Points (P0..P3)
        public Vector3 fwdP0;
        public Vector3 fwdP1;
        public Vector3 fwdP2;
        public Vector3 fwdP3;

        // Aft Quadratic 3D Control Points (P0..P2)
        public Vector3 aftP0; // Synced to fwdP3
        public Vector3 aftP1;
        public Vector3 aftP2;

        [HideInInspector] public Vector3 cachedInnerMidshipPt;
        [HideInInspector] public Vector3 cachedOffsetVector;
        [HideInInspector] public Vector3[] cachedStbdInnerPts;
        [HideInInspector] public Vector3[] cachedPortInnerPts;
    }

    [Header("Primary Curve References")]
    [Tooltip("Reference to SheerCurves for profile lines (Stem, Stern, Keel).")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to TopLineCurves for Top Line (u=1.00) 3D points.")]
    public TopLineCurves topLineCurves;

    [Tooltip("Reference to BreadthCurves for Max Breadth (p3) 3D points.")]
    public BreadthCurves breadthCurves;

    [Tooltip("Reference to BilgeCurves for Diagonal Bilge (p2) 3D points.")]
    public BilgeCurves bilgeCurves;

    [Tooltip("Reference to FloorCurves for Floor Line (p1) 3D points.")]
    public FloorCurves floorCurves;

    [Tooltip("Reference to BodyCurves for hull section node alignment.")]
    public BodyCurves bodyCurves;

    [Tooltip("Reference to BackboneCurves for stem and stern keel width offset.")]
    public BackboneCurves backboneCurves;

    [Tooltip("Reference to FashionCurves for 3D transom stern boundary curve.")]
    public FashionCurves fashionCurves;

    [Header("Ribband Generation Settings")]
    [Tooltip("Region of the hull across which ribbands are generated.")]
    public RibbandRegion region = RibbandRegion.BreadthToBilge;

    [Tooltip("Number of intermediate ribband curves to generate per region/band.")]
    [Range(1, 10)] public int ribbandCountPerBand = 3;

    [Header("Gizmo Visualization Settings")]
    public bool showLabels = true;
    public bool showControlArms = true;
    public bool showPolygonSkeleton = false;
    public Color ribbandColor = new Color(0.2f, 0.75f, 1.0f, 0.95f);

    [Header("Inner Line Settings")]
    public bool showInnerLines = true;
    [Tooltip("Spacing distance between the outer ribbands and the parallel inner ribbands. Synced to BodyCurves.")]
    [Range(0f, 2f)] public float innerLineSpacing = 0.2f;
    [Tooltip("Use harmonized bright hue derived from each ribband's unique color, or use innerLineColor fallback.")]
    public bool useHarmonizedInnerColor = true;
    public Color innerLineColor = new Color(0.2f, 0.85f, 0.95f, 0.95f);

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

    [HideInInspector] public List<SingleRibband> generatedRibbands = new List<SingleRibband>();
    [HideInInspector] public int changeVersion = 0;

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Reset()
    {
        InitializeRibbands();
    }

    public void EnsureInitialized()
    {
        if (!isInitialized || generatedRibbands == null)
        {
            InitializeRibbands();
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

        if (backboneCurves == null) backboneCurves = GetComponentInParent<BackboneCurves>();
        if (backboneCurves == null) backboneCurves = FindFirstObjectByType<BackboneCurves>();

        if (fashionCurves == null) fashionCurves = GetComponentInParent<FashionCurves>();
        if (fashionCurves == null) fashionCurves = FindFirstObjectByType<FashionCurves>();
    }

    [ContextMenu("Reset Ribband Curves Profile")]
    public void InitializeRibbands()
    {
        FindReferences();
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
        if (topLineCurves != null) topLineCurves.UpdateCachedCurves();
        if (breadthCurves != null) breadthCurves.UpdateCachedCurves();
        if (fashionCurves != null) fashionCurves.UpdateCachedCurves();
        if (bilgeCurves != null) bilgeCurves.UpdateCachedCurves();
        if (floorCurves != null) floorCurves.UpdateCachedCurves();

        generatedRibbands.Clear();

        if (region == RibbandRegion.TopLineToBreadth)
        {
            GenerateSubRegion(RibbandRegion.TopLineToBreadth, "Upper Mould (TopLine to Breadth)", ribbandCountPerBand, 0.75f, 1.00f);
        }
        else if (region == RibbandRegion.BreadthToBilge)
        {
            GenerateSubRegion(RibbandRegion.BreadthToBilge, "Upper (Breadth to Bilge)", ribbandCountPerBand, 0.50f, 0.75f);
        }
        else if (region == RibbandRegion.BilgeToFloor)
        {
            GenerateSubRegion(RibbandRegion.BilgeToFloor, "Middle (Bilge to Floor)", ribbandCountPerBand, 0.25f, 0.50f);
        }
        else if (region == RibbandRegion.FloorToKeel)
        {
            GenerateSubRegion(RibbandRegion.FloorToKeel, "Lower (Floor to Keel)", ribbandCountPerBand, 0.00f, 0.25f);
        }
        else if (region == RibbandRegion.FullHullSpan)
        {
            // Generate all 4 distinct bands simultaneously in order from top to bottom
            GenerateSubRegion(RibbandRegion.TopLineToBreadth, "Upper Mould (TopLine to Breadth)", ribbandCountPerBand, 0.75f, 1.00f);
            GenerateSubRegion(RibbandRegion.BreadthToBilge, "Upper (Breadth to Bilge)", ribbandCountPerBand, 0.50f, 0.75f);
            GenerateSubRegion(RibbandRegion.BilgeToFloor, "Middle (Bilge to Floor)", ribbandCountPerBand, 0.25f, 0.50f);
            GenerateSubRegion(RibbandRegion.FloorToKeel, "Lower (Floor to Keel)", ribbandCountPerBand, 0.00f, 0.25f);
        }

        if (bodyCurves != null && bodyCurves.showInnerLine)
        {
            float midY = (breadthCurves != null) ? breadthCurves.fwdP3.y : 2f;
            innerLineSpacing = bodyCurves.GetInnerLineSpacingAtY(midY);
        }

        if (showInnerLines)
        {
            for (int r = 0; r < generatedRibbands.Count; r++)
            {
                UpdateInnerParallelCurveForRibband(generatedRibbands[r]);
            }
        }

        changeVersion++;
    }

    private void GenerateSubRegion(RibbandRegion subReg, string subRegionName, int count, float uMin, float uMax)
    {
        float targetApexZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);

        for (int i = 0; i < count; i++)
        {
            float t = (float)(i + 1) / (count + 1);

            SingleRibband rib = new SingleRibband();
            rib.index = generatedRibbands.Count;
            rib.subRegionName = subRegionName;
            rib.tRatio = t;
            rib.name = $"{subRegionName} {i + 1}";

            GetBoundaryControlPoints(subReg, t, hw, targetApexZ,
                out Vector3 upperFwdP0, out Vector3 lowerFwdP0,
                out Vector3 upperFwdP1, out Vector3 lowerFwdP1,
                out Vector3 upperFwdP2, out Vector3 lowerFwdP2,
                out Vector3 upperFwdP3, out Vector3 lowerFwdP3,
                out Vector3 upperAftP1, out Vector3 lowerAftP1,
                out Vector3 upperAftP2, out Vector3 lowerAftP2);

            // 1. Midship Apex (P3): Evaluate directly on BodyCurves section spline if available
            float uRib = Mathf.Lerp(uMin, uMax, t);
            rib.uSectionParam = uRib;

            if (bodyCurves != null)
            {
                SectionNodes midNodes = bodyCurves.GetSectionNodesAtZ(targetApexZ, 1f);
                Vector3 sectionPt = bodyCurves.EvaluateBodySectionAtU(uRib, midNodes);
                rib.fwdP3.x = Mathf.Abs(sectionPt.x);
                rib.fwdP3.y = sectionPt.y;
                rib.fwdP3.z = targetApexZ;
            }
            else
            {
                rib.fwdP3.x = Mathf.Lerp(lowerFwdP3.x, upperFwdP3.x, t);
                rib.fwdP3.y = Mathf.Lerp(lowerFwdP3.y, upperFwdP3.y, t);
                rib.fwdP3.z = targetApexZ;
            }

            // 2. Bow Tip (P0): Positioned on Backbone (+hw), Y interpolated, Z locked to Stem Post profile
            rib.fwdP0.x = hw;
            rib.fwdP0.y = Mathf.Lerp(lowerFwdP0.y, upperFwdP0.y, t);
            if (sheerCurves != null)
            {
                rib.fwdP0.z = sheerCurves.GetStemProfileZAtY(rib.fwdP0.y);
            }
            else
            {
                rib.fwdP0.z = Mathf.Lerp(lowerFwdP0.z, upperFwdP0.z, t);
            }

            // 3. Bow Entry Arm (fwdP1): Evaluated natively from Bow Mould P1 Guide
            rib.fwdP1 = EvaluateBowMouldP1AtU(uRib);

            // 4. Midship Forward Arm (fwdP2): Z from Forward P2 Guide, X and Y locked to midship apex for C1 continuity
            Vector3 guideFwdP2 = EvaluateFwdP2AtU(uRib);
            rib.fwdP2.z = guideFwdP2.z;
            rib.fwdP2.x = rib.fwdP3.x;
            rib.fwdP2.y = rib.fwdP3.y;

            // 5. Stern Arm (aftP1): Z from Aft Mould A1 Guide, X and Y locked to midship apex for C1 continuity
            Vector3 guideAftA1 = EvaluateAftMouldA1AtU(uRib);
            rib.aftP1.z = guideAftA1.z;
            rib.aftP1.x = rib.fwdP3.x;
            rib.aftP1.y = rib.fwdP3.y;

            // 6. Stern Transom Corner (aftP2): Evaluated natively from Stern Transom A2 Guide / FashionCurves
            rib.aftP2 = EvaluateAftTransomA2AtU(uRib);

            rib.aftP0 = rib.fwdP3;

            generatedRibbands.Add(rib);
        }
    }

    private void GetBoundaryControlPoints(RibbandRegion subReg, float t, float hw, float midZ,
        out Vector3 uF0, out Vector3 lF0,
        out Vector3 uF1, out Vector3 lF1,
        out Vector3 uF2, out Vector3 lF2,
        out Vector3 uF3, out Vector3 lF3,
        out Vector3 uA1, out Vector3 lA1,
        out Vector3 uA2, out Vector3 lA2)
    {
        // Default fallbacks if references are unassigned
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;
        float bowZ = (sheerCurves != null) ? sheerCurves.stemP3.z : 15f;
        float sternZ = (sheerCurves != null) ? sheerCurves.sternP3.z : -15f;
        Vector3 defaultBow = new Vector3(hw, keelY + 5f, bowZ);
        Vector3 defaultStern = new Vector3(hw, keelY + 5f, sternZ);
        Vector3 defaultApex = new Vector3(4, keelY + 2f, midZ);

        // Keel Line Fallback for Lower Band
        Vector3 keelBowPt = new Vector3(hw, keelY, (sheerCurves != null) ? sheerCurves.stemP0.z : bowZ);
        Vector3 keelSternPt = new Vector3(hw, keelY, (sheerCurves != null) ? sheerCurves.sternP0.z : sternZ);
        Vector3 keelMidPt = new Vector3(hw, keelY, midZ);

        Vector3 keelBowArm = keelMidPt + Vector3.forward * (bowZ - midZ) * 0.5f;
        Vector3 keelSternArm = keelSternPt + Vector3.forward * (midZ - sternZ) * 0.5f;

        if (subReg == RibbandRegion.TopLineToBreadth)
        {
            uF0 = (topLineCurves != null) ? topLineCurves.fwdP0 : defaultBow * 1.2f;
            uF1 = (topLineCurves != null) ? topLineCurves.fwdP1 : defaultBow * 1.1f;
            uF2 = (topLineCurves != null) ? topLineCurves.fwdP2 : defaultApex * 1.2f + Vector3.forward * 5f;
            uF3 = (topLineCurves != null) ? topLineCurves.fwdP3 : defaultApex * 1.2f;
            uA1 = (topLineCurves != null) ? topLineCurves.aftP1 : defaultApex * 1.2f - Vector3.forward * 5f;
            uA2 = (topLineCurves != null) ? (topLineCurves.cachedIsAftCubic ? topLineCurves.aftP3 : topLineCurves.aftP2) : defaultStern * 1.2f;

            lF0 = (breadthCurves != null) ? breadthCurves.fwdP0 : defaultBow;
            lF1 = (breadthCurves != null) ? breadthCurves.fwdP1 : defaultBow * 0.75f;
            lF2 = (breadthCurves != null) ? breadthCurves.fwdP2 : defaultApex + Vector3.forward * 5f;
            lF3 = (breadthCurves != null) ? breadthCurves.fwdP3 : defaultApex;
            lA1 = (breadthCurves != null) ? breadthCurves.aftP1 : defaultApex - Vector3.forward * 5f;
            lA2 = (breadthCurves != null) ? (breadthCurves.cachedIsAftCubic ? breadthCurves.aftP3 : breadthCurves.aftP2) : defaultStern;
        }
        else if (subReg == RibbandRegion.BreadthToBilge)
        {
            uF0 = (breadthCurves != null) ? breadthCurves.fwdP0 : defaultBow;
            uF1 = (breadthCurves != null) ? breadthCurves.fwdP1 : defaultBow * 0.75f;
            uF2 = (breadthCurves != null) ? breadthCurves.fwdP2 : defaultApex + Vector3.forward * 5f;
            uF3 = (breadthCurves != null) ? breadthCurves.fwdP3 : defaultApex;
            uA1 = (breadthCurves != null) ? breadthCurves.aftP1 : defaultApex - Vector3.forward * 5f;
            uA2 = (breadthCurves != null) ? (breadthCurves.cachedIsAftCubic ? breadthCurves.aftP3 : breadthCurves.aftP2) : defaultStern;

            lF0 = (bilgeCurves != null) ? bilgeCurves.fwdP0 : defaultBow * 0.5f;
            lF1 = (bilgeCurves != null) ? bilgeCurves.fwdP1 : defaultBow * 0.4f;
            lF2 = (bilgeCurves != null) ? bilgeCurves.fwdP2 : defaultApex * 0.5f + Vector3.forward * 4f;
            lF3 = (bilgeCurves != null) ? bilgeCurves.fwdP3 : defaultApex * 0.5f;
            lA1 = (bilgeCurves != null) ? bilgeCurves.aftP1 : defaultApex * 0.5f - Vector3.forward * 4f;
            lA2 = (bilgeCurves != null) ? bilgeCurves.aftP2 : defaultStern * 0.5f;
        }
        else if (subReg == RibbandRegion.BilgeToFloor)
        {
            uF0 = (bilgeCurves != null) ? bilgeCurves.fwdP0 : defaultBow * 0.5f;
            uF1 = (bilgeCurves != null) ? bilgeCurves.fwdP1 : defaultBow * 0.4f;
            uF2 = (bilgeCurves != null) ? bilgeCurves.fwdP2 : defaultApex * 0.5f + Vector3.forward * 4f;
            uF3 = (bilgeCurves != null) ? bilgeCurves.fwdP3 : defaultApex * 0.5f;
            uA1 = (bilgeCurves != null) ? bilgeCurves.aftP1 : defaultApex * 0.5f - Vector3.forward * 4f;
            uA2 = (bilgeCurves != null) ? bilgeCurves.aftP2 : defaultStern * 0.5f;

            if (floorCurves != null)
            {
                lF0 = floorCurves.fwdP0;
                lF1 = floorCurves.fwdP1;
                lF2 = floorCurves.fwdP2;
                lF3 = floorCurves.fwdP3;
                lA1 = floorCurves.aftP1;
                lA2 = floorCurves.aftP3;
            }
            else
            {
                lF0 = keelBowPt;
                lF1 = keelBowArm;
                lF2 = keelBowArm;
                lF3 = keelMidPt;
                lA1 = keelSternArm;
                lA2 = keelSternPt;
            }
        }
        else // FloorToKeel
        {
            if (floorCurves != null)
            {
                uF0 = floorCurves.fwdP0;
                uF1 = floorCurves.fwdP1;
                uF2 = floorCurves.fwdP2;
                uF3 = floorCurves.fwdP3;
                uA1 = floorCurves.aftP1;
                uA2 = floorCurves.aftP3;
            }
            else
            {
                uF0 = defaultBow * 0.5f;
                uF1 = defaultBow * 0.4f;
                uF2 = defaultApex * 0.5f + Vector3.forward * 4f;
                uF3 = defaultApex * 0.5f;
                uA1 = defaultApex * 0.5f - Vector3.forward * 4f;
                uA2 = defaultStern * 0.5f;
            }

            lF0 = keelBowPt;
            lF1 = keelBowArm;
            lF2 = keelBowArm;
            lF3 = keelMidPt;
            lA1 = keelSternArm;
            lA2 = keelSternPt;
        }
    }

    /// <summary>
    /// Mathematically evaluates the 3D Bow Mould entry control handle (fwdP1) at parameter u
    /// by scaling the master BodyCurves section through the Key Shape Bow Tip arms
    /// (TopLineCurves.fwdP1, BreadthCurves.fwdP1, FloorCurves.fwdP1) and locking Z to the Stem Post profile.
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

    public int GetRibbandCount()
    {
        return generatedRibbands.Count;
    }

    public float GetRibbandXAtZ(int ribbandIndex, float z)
    {
        return GetRibbandPointAtZ(ribbandIndex, z).x;
    }

    public float GetRibbandYAtZ(int ribbandIndex, float z)
    {
        return GetRibbandPointAtZ(ribbandIndex, z).y;
    }

    public Vector3 GetRibbandPointAtZ(int ribbandIndex, float z)
    {
        EnsureInitialized();

        if (ribbandIndex < 0 || ribbandIndex >= generatedRibbands.Count)
        {
            return Vector3.zero;
        }

        SingleRibband rib = generatedRibbands[ribbandIndex];

        if (z >= rib.fwdP3.z)
        {
            if (z >= rib.fwdP0.z) return rib.fwdP0;
            float t = GetBezierTAtZ(rib.fwdP0, rib.fwdP1, rib.fwdP2, rib.fwdP3, z, true);
            return CalculateCubicBezierPoint(t, rib.fwdP0, rib.fwdP1, rib.fwdP2, rib.fwdP3);
        }
        else
        {
            if (z <= rib.aftP2.z) return rib.aftP2;
            float t = GetBezierTAtZQuadratic(rib.aftP0, rib.aftP1, rib.aftP2, z, false);
            return CalculateQuadraticBezierPoint(t, rib.aftP0, rib.aftP1, rib.aftP2);
        }
    }

    public Vector3 GetInnerRibbandPointAtZ(int ribbandIndex, float z)
    {
        EnsureInitialized();
        if (ribbandIndex < 0 || ribbandIndex >= generatedRibbands.Count) return Vector3.zero;

        SingleRibband rib = generatedRibbands[ribbandIndex];
        if (rib.cachedStbdInnerPts == null || rib.cachedStbdInnerPts.Length == 0)
        {
            UpdateInnerParallelCurveForRibband(rib);
        }

        Vector3 outerPt = GetRibbandPointAtZ(ribbandIndex, z);
        Vector3 pOuter = rib.fwdP3;
        Vector3 pInner = rib.cachedInnerMidshipPt;

        float deltaX = pInner.x - pOuter.x;
        float deltaY = pInner.y - pOuter.y;
        float absDeltaX = Mathf.Abs(deltaX);

        if (Mathf.Abs(z - rib.fwdP3.z) < 0.001f)
        {
            return pInner;
        }

        if (z <= rib.aftP2.z)
        {
            if (rib.cachedStbdInnerPts != null && rib.cachedStbdInnerPts.Length > 0)
            {
                return rib.cachedStbdInnerPts[rib.cachedStbdInnerPts.Length - 1];
            }
        }

        Vector3 deriv;
        if (z >= rib.fwdP3.z)
        {
            float t = GetBezierTAtZ(rib.fwdP0, rib.fwdP1, rib.fwdP2, rib.fwdP3, z, true);
            deriv = CalculateCubicBezierDerivative(t, rib.fwdP0, rib.fwdP1, rib.fwdP2, rib.fwdP3);
        }
        else
        {
            float t = GetBezierTAtZQuadratic(rib.aftP0, rib.aftP1, rib.aftP2, z, false);
            deriv = CalculateQuadraticBezierDerivative(t, rib.aftP0, rib.aftP1, rib.aftP2);
        }

        float len = Mathf.Sqrt(deriv.z * deriv.z + deriv.x * deriv.x);
        Vector2 nXZ = (len > 0.0001f) ? new Vector2(deriv.z / len, -deriv.x / len) : new Vector2(-1f, 0f);

        Vector3 innerPt = outerPt + new Vector3(absDeltaX * nXZ.x, deltaY, absDeltaX * nXZ.y);
        if (innerPt.x < 0f) innerPt.x = 0f;
        return innerPt;
    }

    public Vector3 GetInnerRibbandBowTip(int ribbandIndex, bool starboard = true)
    {
        EnsureInitialized();
        if (ribbandIndex < 0 || ribbandIndex >= generatedRibbands.Count) return Vector3.zero;
        SingleRibband rib = generatedRibbands[ribbandIndex];
        if (rib.cachedStbdInnerPts != null && rib.cachedStbdInnerPts.Length > 0)
        {
            Vector3 tip = rib.cachedStbdInnerPts[0];
            return starboard ? tip : MirrorX(tip);
        }
        return starboard ? (rib.fwdP0 + rib.cachedOffsetVector) : MirrorX(rib.fwdP0 + rib.cachedOffsetVector);
    }

    public Vector3 GetInnerRibbandSternTip(int ribbandIndex, bool starboard = true)
    {
        EnsureInitialized();
        if (ribbandIndex < 0 || ribbandIndex >= generatedRibbands.Count) return Vector3.zero;
        SingleRibband rib = generatedRibbands[ribbandIndex];
        if (rib.cachedStbdInnerPts != null && rib.cachedStbdInnerPts.Length > 0)
        {
            Vector3 tip = rib.cachedStbdInnerPts[rib.cachedStbdInnerPts.Length - 1];
            return starboard ? tip : MirrorX(tip);
        }
        return starboard ? (rib.aftP2 + rib.cachedOffsetVector) : MirrorX(rib.aftP2 + rib.cachedOffsetVector);
    }

    public Vector3[] GetInnerRibbandPoints(int ribbandIndex, bool starboard = true)
    {
        EnsureInitialized();
        if (ribbandIndex < 0 || ribbandIndex >= generatedRibbands.Count) return null;
        SingleRibband rib = generatedRibbands[ribbandIndex];
        if (rib.cachedStbdInnerPts == null || rib.cachedStbdInnerPts.Length == 0)
        {
            UpdateInnerParallelCurveForRibband(rib);
        }
        return starboard ? rib.cachedStbdInnerPts : rib.cachedPortInnerPts;
    }

    private void UpdateInnerParallelCurveForRibband(SingleRibband rib)
    {
        if (rib == null || !showInnerLines) return;

        float targetApexZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
        Vector3 pOuter = rib.fwdP3;
        Vector3 pInner;

        if (bodyCurves != null && bodyCurves.showInnerLine)
        {
            SectionNodes midNodes = bodyCurves.GetSectionNodesAtZ(targetApexZ, 1f);
            float du = 0.002f;
            Vector3 secPtNext = bodyCurves.EvaluateBodySectionAtU(Mathf.Min(1.0f, rib.uSectionParam + du), midNodes);
            Vector3 secPtPrev = bodyCurves.EvaluateBodySectionAtU(Mathf.Max(0.0f, rib.uSectionParam - du), midNodes);
            Vector2 t2D = new Vector2(secPtNext.x - secPtPrev.x, secPtNext.y - secPtPrev.y).normalized;
            if (t2D.sqrMagnitude < 0.000001f) t2D = Vector2.right;

            Vector2 n2D = new Vector2(-t2D.y, t2D.x); // Inward normal towards centerline on Starboard
            float spacing = bodyCurves.GetInnerLineSpacingAtY(pOuter.y);
            pInner = pOuter + new Vector3(n2D.x, n2D.y, 0f) * spacing;
            if (rib.uSectionParam >= 0.999f) pInner.y = midNodes.p4.y;
        }
        else
        {
            pInner = pOuter + new Vector3(-innerLineSpacing, 0f, 0f);
        }

        rib.cachedInnerMidshipPt = pInner;

        float deltaX = pInner.x - pOuter.x; // Negative on Starboard
        float deltaY = pInner.y - pOuter.y;
        rib.cachedOffsetVector = new Vector3(deltaX, deltaY, 0f);

        const int segmentsFwd = 40;
        const int segmentsAft = 30;
        int totalPts = segmentsFwd + segmentsAft + 1;

        if (rib.cachedStbdInnerPts == null || rib.cachedStbdInnerPts.Length != totalPts)
        {
            rib.cachedStbdInnerPts = new Vector3[totalPts];
            rib.cachedPortInnerPts = new Vector3[totalPts];
        }

        float absDeltaX = Mathf.Abs(deltaX);

        // Forward Span (Cubic Bezier: Bow P0 -> Midship P3)
        for (int i = 0; i <= segmentsFwd; i++)
        {
            if (i == segmentsFwd)
            {
                rib.cachedStbdInnerPts[i] = pInner;
                rib.cachedPortInnerPts[i] = MirrorX(pInner);
                continue;
            }

            float t = (float)i / segmentsFwd;
            Vector3 pt = CalculateCubicBezierPoint(t, rib.fwdP0, rib.fwdP1, rib.fwdP2, rib.fwdP3);
            Vector3 deriv = CalculateCubicBezierDerivative(t, rib.fwdP0, rib.fwdP1, rib.fwdP2, rib.fwdP3);

            float len = Mathf.Sqrt(deriv.z * deriv.z + deriv.x * deriv.x);
            Vector2 nXZ = (len > 0.0001f) ? new Vector2(deriv.z / len, -deriv.x / len) : new Vector2(-1f, 0f);

            Vector3 innerPt = pt + new Vector3(absDeltaX * nXZ.x, deltaY, absDeltaX * nXZ.y);
            if (innerPt.x < 0f) innerPt.x = 0f;

            rib.cachedStbdInnerPts[i] = innerPt;
            rib.cachedPortInnerPts[i] = MirrorX(innerPt);
        }

        // Aft Span (Quadratic Bezier: Midship P0 -> Stern P2)
        float targetZ = rib.aftP2.z + absDeltaX;
        float tEnd = 1.0f;

        Vector3 derivEndCheck = CalculateQuadraticBezierDerivative(1f, rib.aftP0, rib.aftP1, rib.aftP2);
        float lenEndCheck = Mathf.Sqrt(derivEndCheck.z * derivEndCheck.z + derivEndCheck.x * derivEndCheck.x);
        float nZEndCheck = (lenEndCheck > 0.0001f) ? (-derivEndCheck.x / lenEndCheck) : 0f;
        float zInnerAt1 = rib.aftP2.z + absDeltaX * nZEndCheck;

        if (zInnerAt1 < targetZ && (rib.aftP0.z - targetZ) > 0.001f)
        {
            float tLow = 0f;
            float tHigh = 1f;
            for (int iter = 0; iter < 16; iter++)
            {
                float tMid = (tLow + tHigh) * 0.5f;
                Vector3 ptMid = CalculateQuadraticBezierPoint(tMid, rib.aftP0, rib.aftP1, rib.aftP2);
                Vector3 derivMid = CalculateQuadraticBezierDerivative(tMid, rib.aftP0, rib.aftP1, rib.aftP2);
                float lenMid = Mathf.Sqrt(derivMid.z * derivMid.z + derivMid.x * derivMid.x);
                float nZMid = (lenMid > 0.0001f) ? (-derivMid.x / lenMid) : 0f;
                float zInnerMid = ptMid.z + absDeltaX * nZMid;
                if (zInnerMid > targetZ)
                {
                    tLow = tMid;
                }
                else
                {
                    tHigh = tMid;
                }
            }
            tEnd = (tLow + tHigh) * 0.5f;
        }

        for (int j = 1; j <= segmentsAft; j++)
        {
            float t = ((float)j / segmentsAft) * tEnd;
            Vector3 pt = CalculateQuadraticBezierPoint(t, rib.aftP0, rib.aftP1, rib.aftP2);
            Vector3 deriv = CalculateQuadraticBezierDerivative(t, rib.aftP0, rib.aftP1, rib.aftP2);

            float len = Mathf.Sqrt(deriv.z * deriv.z + deriv.x * deriv.x);
            Vector2 nXZ = (len > 0.0001f) ? new Vector2(deriv.z / len, -deriv.x / len) : new Vector2(-1f, 0f);

            Vector3 innerPt = pt + new Vector3(absDeltaX * nXZ.x, deltaY, absDeltaX * nXZ.y);
            if (j == segmentsAft)
            {
                innerPt.z = targetZ;
            }
            if (innerPt.x < 0f) innerPt.x = 0f;

            int idx = segmentsFwd + j;
            rib.cachedStbdInnerPts[idx] = innerPt;
            rib.cachedPortInnerPts[idx] = MirrorX(innerPt);
        }
    }

    // --- BEZIER MATH UTILITIES ---

    public Vector3 CalculateQuadraticBezierDerivative(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1f - t;
        return 2f * u * (p1 - p0) + 2f * t * (p2 - p1);
    }

    public Vector3 CalculateCubicBezierDerivative(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1f - t;
        return 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
    }

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

        for (int i = 0; i < generatedRibbands.Count; i++)
        {
            SingleRibband rib = generatedRibbands[i];
            Color c = Color.HSVToRGB((float)i / Mathf.Max(1, generatedRibbands.Count) * 0.65f + 0.40f, 0.85f, 0.95f);

            // Draw Starboard 3D Ribband Curve (Cubic Forward, Quadratic Aft)
            DrawCubicBezierCurve(rib.fwdP0, rib.fwdP1, rib.fwdP2, rib.fwdP3, c);
            DrawQuadraticBezierCurve(rib.aftP0, rib.aftP1, rib.aftP2, c);

            // Draw Port 3D Ribband Curve (Mirrored X)
            Color portColor = new Color(c.r, c.g * 0.6f, c.b, c.a);
            DrawCubicBezierCurve(MirrorX(rib.fwdP0), MirrorX(rib.fwdP1), MirrorX(rib.fwdP2), MirrorX(rib.fwdP3), portColor);
            DrawQuadraticBezierCurve(MirrorX(rib.aftP0), MirrorX(rib.aftP1), MirrorX(rib.aftP2), portColor);

            // Draw Transom Line connecting Starboard and Port Transom Corners
            Gizmos.color = c;
            Gizmos.DrawLine(rib.aftP2, MirrorX(rib.aftP2));

            // Draw Inner Ribband Lines
            if (showInnerLines && rib.cachedStbdInnerPts != null && rib.cachedStbdInnerPts.Length > 1)
            {
                Color innerColor = useHarmonizedInnerColor
                    ? Color.HSVToRGB((float)i / Mathf.Max(1, generatedRibbands.Count) * 0.65f + 0.40f, 0.40f, 1.0f)
                    : innerLineColor;

                Gizmos.color = innerColor;
                for (int p = 0; p < rib.cachedStbdInnerPts.Length - 1; p++)
                {
                    Gizmos.DrawLine(rib.cachedStbdInnerPts[p], rib.cachedStbdInnerPts[p + 1]);
                }

                Color portInnerColor = new Color(innerColor.r, innerColor.g * 0.6f, innerColor.b, innerColor.a);
                Gizmos.color = portInnerColor;
                for (int p = 0; p < rib.cachedPortInnerPts.Length - 1; p++)
                {
                    Gizmos.DrawLine(rib.cachedPortInnerPts[p], rib.cachedPortInnerPts[p + 1]);
                }

                // Inner Transom Connection Line
                int last = rib.cachedStbdInnerPts.Length - 1;
                Gizmos.color = innerColor;
                Gizmos.DrawLine(rib.cachedStbdInnerPts[last], rib.cachedPortInnerPts[last]);
            }

            // Draw 3D Control Polygon Skeleton Arms (Forward & Aft)
            if (showControlArms || showPolygonSkeleton)
            {
                Gizmos.color = new Color(c.r, c.g, c.b, 0.65f);
                // Forward Control Arms
                Gizmos.DrawLine(rib.fwdP0, rib.fwdP1);
                Gizmos.DrawLine(rib.fwdP1, rib.fwdP2);
                Gizmos.DrawLine(rib.fwdP2, rib.fwdP3);
                // Aft Control Arms
                Gizmos.DrawLine(rib.fwdP3, rib.aftP1);
                Gizmos.DrawLine(rib.aftP1, rib.aftP2);

                // Port Control Arms
                Gizmos.DrawLine(MirrorX(rib.fwdP0), MirrorX(rib.fwdP1));
                Gizmos.DrawLine(MirrorX(rib.fwdP1), MirrorX(rib.fwdP2));
                Gizmos.DrawLine(MirrorX(rib.fwdP2), MirrorX(rib.fwdP3));
                Gizmos.DrawLine(MirrorX(rib.fwdP3), MirrorX(rib.aftP1));
                Gizmos.DrawLine(MirrorX(rib.aftP1), MirrorX(rib.aftP2));

                // Draw Control Point Spheres for ALL 6 nodes (P0, P1, P2, P3, A1, A2)
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(rib.fwdP0, 0.10f); // Bow Tip (P0)

                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(rib.fwdP1, 0.08f); // Bow Entry Arm (P1)

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(rib.fwdP2, 0.08f); // Midship Forward Arm (P2)

                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(rib.fwdP3, 0.12f); // Midship Apex (P3)

                Gizmos.color = new Color(1f, 0.5f, 0f);
                Gizmos.DrawSphere(rib.aftP1, 0.08f); // Midship Aft Arm (A1)

                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(rib.aftP2, 0.10f); // Stern Transom Corner (A2)

                // Mirrored Port Spheres
                Gizmos.color = Color.yellow; Gizmos.DrawSphere(MirrorX(rib.fwdP0), 0.10f);
                Gizmos.color = Color.cyan; Gizmos.DrawSphere(MirrorX(rib.fwdP1), 0.08f);
                Gizmos.color = Color.green; Gizmos.DrawSphere(MirrorX(rib.fwdP2), 0.08f);
                Gizmos.color = Color.yellow; Gizmos.DrawSphere(MirrorX(rib.fwdP3), 0.12f);
                Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawSphere(MirrorX(rib.aftP1), 0.08f);
                Gizmos.color = Color.magenta; Gizmos.DrawSphere(MirrorX(rib.aftP2), 0.10f);
            }

#if UNITY_EDITOR
            if (showLabels)
            {
                GUIStyle labelStyle = GetCachedLabelStyle();
                Handles.Label(rib.fwdP3 + Vector3.up * 0.15f, $"{rib.name} (u={rib.uSectionParam:F2})", labelStyle);
                if (showControlArms)
                {
                    Handles.Label(rib.fwdP1 + Vector3.up * 0.10f, $"P1 (Bow)", labelStyle);
                    Handles.Label(rib.fwdP2 + Vector3.up * 0.10f, $"P2 (FwdArm)", labelStyle);
                }
            }
#endif
        }

#if UNITY_EDITOR
        // --- DASHED FORWARD CONTROL ARM GUIDES ---
        if (showBowMouldP1Guide) DrawBowMouldP1GuideDashed(bowMouldGuideColor);
        if (showFwdP2Guide) DrawFwdP2GuideDashed(fwdP2GuideColor);

        // --- DASHED AFT CONTROL ARM GUIDES ---
        if (showAftMouldA1Guide) DrawAftMouldA1GuideDashed(aftMouldGuideColor);
        if (showSternTransomGuide) DrawSternTransomGuideDashed(sternTransomGuideColor);
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

        float[] sampleUs = new float[] { 0.25f, 0.50f, 0.75f, 1.00f };
        string[] labels = new string[] { "Floor P1 (u=0.25)", "Ribband P1 (u=0.50)", "Breadth P1 (u=0.75)", "TopLine P1 (u=1.00)" };

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

        float[] sampleUs = new float[] { 0.00f, 0.25f, 0.50f, 0.75f, 1.00f };
        string[] labels = new string[] { "Keel P2 (u=0.00)", "Floor P2 (u=0.25)", "Ribband P2 (u=0.50)", "Breadth P2 (u=0.75)", "TopLine P2 (u=1.00)" };

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
        string[] labels = new string[] { "Keel A1 (u=0.00)", "Floor A1 (u=0.25)", "Ribband A1 (u=0.50)", "Breadth A1 (u=0.75)", "TopLine A1 (u=1.00)" };

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
        string[] labels = new string[] { "Keel A2 (u=0.00)", "Floor A2 (u=0.25)", "Ribband A2 (u=0.50)", "Breadth A2 (u=0.75)", "TopLine A2 (u=1.00)" };

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

    private void DrawCubicBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color, int segments = 30)
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

    private void DrawQuadraticBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Color color, int segments = 25)
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
[CustomEditor(typeof(RibbandCurves))]
public class RibbandCurvesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RibbandCurves curves = (RibbandCurves)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Generated Ribbands Readout", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Active Region Mode:", $"{curves.region}");
        EditorGUILayout.LabelField("Total Active Ribbands:", $"{curves.generatedRibbands.Count}");

        for (int i = 0; i < curves.generatedRibbands.Count; i++)
        {
            RibbandCurves.SingleRibband rib = curves.generatedRibbands[i];
            EditorGUILayout.LabelField($" {rib.name} (u={rib.uSectionParam:F2}):", $"Midship X: {rib.fwdP3.x:F2}m, Y: {rib.fwdP3.y:F2}m");
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update Ribband Curves Profile", GUILayout.Height(30)))
        {
            Undo.RecordObject(curves, "Update Ribband Profile");
            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);
        }
    }
}
#endif
