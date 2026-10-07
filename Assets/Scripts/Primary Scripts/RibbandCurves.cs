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
    }

    [Header("Primary Curve References")]
    [Tooltip("Reference to SheerCurves for profile lines (Stem, Stern, Keel).")]
    public SheerCurves sheerCurves;

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
    public bool showPolygonSkeleton = false;
    public Color ribbandColor = new Color(0.2f, 0.75f, 1.0f, 0.95f);

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
        if (breadthCurves != null) breadthCurves.UpdateCachedCurves();
        if (fashionCurves != null) fashionCurves.UpdateCachedCurves();
        if (bilgeCurves != null) bilgeCurves.UpdateCachedCurves();
        if (floorCurves != null) floorCurves.UpdateCachedCurves();

        generatedRibbands.Clear();

        if (region == RibbandRegion.BreadthToBilge)
        {
            GenerateSubRegion(RibbandRegion.BreadthToBilge, "Upper", ribbandCountPerBand, 0.50f, 0.75f);
        }
        else if (region == RibbandRegion.BilgeToFloor)
        {
            GenerateSubRegion(RibbandRegion.BilgeToFloor, "Middle", ribbandCountPerBand, 0.25f, 0.50f);
        }
        else if (region == RibbandRegion.FloorToKeel)
        {
            GenerateSubRegion(RibbandRegion.FloorToKeel, "Lower", ribbandCountPerBand, 0.00f, 0.25f);
        }
        else if (region == RibbandRegion.FullHullSpan)
        {
            // Generate all 3 distinct bands simultaneously in order from top to bottom
            GenerateSubRegion(RibbandRegion.BreadthToBilge, "Upper (Breadth to Bilge)", ribbandCountPerBand, 0.50f, 0.75f);
            GenerateSubRegion(RibbandRegion.BilgeToFloor, "Middle (Bilge to Floor)", ribbandCountPerBand, 0.25f, 0.50f);
            GenerateSubRegion(RibbandRegion.FloorToKeel, "Lower (Floor to Keel)", ribbandCountPerBand, 0.00f, 0.25f);
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

            // 3. Stern Corner Point (aftP2): Bound directly to FashionCurves if uRib >= 0.25f
            if (fashionCurves != null && uRib >= 0.25f)
            {
                float tFashion = Mathf.Clamp01((uRib - 0.25f) / 0.75f);
                rib.aftP2 = fashionCurves.GetFashionPointAtT(tFashion, 1f);
            }
            else
            {
                rib.aftP2.x = Mathf.Lerp(lowerAftP2.x, upperAftP2.x, t);
                rib.aftP2.y = Mathf.Lerp(lowerAftP2.y, upperAftP2.y, t);
                if (sheerCurves != null)
                {
                    rib.aftP2.z = sheerCurves.GetSternProfileZAtY(rib.aftP2.y);
                }
                else
                {
                    rib.aftP2.z = Mathf.Lerp(lowerAftP2.z, upperAftP2.z, t);
                }
            }

            // 4. Bow Entry Arm (fwdP1): 3D lerp between lower and upper bow entry handles
            rib.fwdP1.x = Mathf.Lerp(lowerFwdP1.x, upperFwdP1.x, t);
            rib.fwdP1.y = Mathf.Lerp(lowerFwdP1.y, upperFwdP1.y, t);
            rib.fwdP1.z = Mathf.Lerp(lowerFwdP1.z, upperFwdP1.z, t);

            // 5. Midship Forward Arm (fwdP2): Z lerped, X and Y locked to midship apex for C1 continuity
            rib.fwdP2.z = Mathf.Lerp(lowerFwdP2.z, upperFwdP2.z, t);
            rib.fwdP2.x = rib.fwdP3.x;
            rib.fwdP2.y = rib.fwdP3.y;

            // 6. Stern Arm (aftP1): Z lerped, X and Y locked to midship apex for C1 continuity
            rib.aftP1.z = Mathf.Lerp(lowerAftP1.z, upperAftP1.z, t);
            rib.aftP1.x = rib.fwdP3.x;
            rib.aftP1.y = rib.fwdP3.y;

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

        if (subReg == RibbandRegion.BreadthToBilge)
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

            lF0 = (floorCurves != null) ? floorCurves.fwdP0 : keelBowPt;
            lF1 = (floorCurves != null) ? floorCurves.fwdP1 : keelBowArm;
            lF2 = (floorCurves != null) ? floorCurves.fwdP1 : keelBowArm;
            lF3 = (floorCurves != null) ? floorCurves.fwdP2 : keelMidPt;
            lA1 = (floorCurves != null) ? floorCurves.aftP1 : keelSternArm;
            lA2 = (floorCurves != null) ? floorCurves.aftP2 : keelSternPt;
        }
        else // FloorToKeel
        {
            uF0 = (floorCurves != null) ? floorCurves.fwdP0 : defaultBow * 0.5f;
            uF1 = (floorCurves != null) ? floorCurves.fwdP1 : defaultBow * 0.4f;
            uF2 = (floorCurves != null) ? floorCurves.fwdP1 : defaultApex * 0.5f + Vector3.forward * 4f;
            uF3 = (floorCurves != null) ? floorCurves.fwdP2 : defaultApex * 0.5f;
            uA1 = (floorCurves != null) ? floorCurves.aftP1 : defaultApex * 0.5f - Vector3.forward * 4f;
            uA2 = (floorCurves != null) ? floorCurves.aftP2 : defaultStern * 0.5f;

            lF0 = keelBowPt;
            lF1 = keelBowArm;
            lF2 = keelBowArm;
            lF3 = keelMidPt;
            lA1 = keelSternArm;
            lA2 = keelSternPt;
        }
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

            if (showPolygonSkeleton)
            {
                Gizmos.color = new Color(c.r, c.g, c.b, 0.4f);
                Gizmos.DrawLine(rib.fwdP0, rib.fwdP1);
                Gizmos.DrawLine(rib.fwdP3, rib.fwdP2);
                Gizmos.DrawLine(rib.aftP0, rib.aftP1);
                Gizmos.DrawLine(rib.aftP1, rib.aftP2);
            }

            // Draw Control Point Spheres
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(rib.fwdP0, 0.08f);
            Gizmos.DrawSphere(rib.fwdP3, 0.11f); // Midship Apex
            Gizmos.DrawSphere(rib.aftP2, 0.08f);

#if UNITY_EDITOR
            if (showLabels)
            {
                GUIStyle labelStyle = GetCachedLabelStyle();
                Handles.Label(rib.fwdP3 + Vector3.up * 0.15f, $"{rib.name} ({rib.fwdP3.x:F2}m)", labelStyle);
            }
#endif
        }
    }

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
