using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// FashionCurves defines the 3D Transom / Fashion Timber boundary curve at the stern.
/// Formed using the exact same NURBS C1 B-Spline section evaluator (NurbsSectionEvaluator) as BodyCurves.
/// Spans from Floor Stern Tip (P1, u=0.25) through Transom Bilge Node (P2, u=0.50),
/// up to Fashion Breadth (P3, u=0.75, from BreadthCurves), and extending through the Upper Mould
/// to Transom Rake Top Corner (P4, u=1.00, from TopLineCurves).
/// Every point evaluated along the NURBS curve locks 100% to the raked Z stern profile of SheerCurves.
/// </summary>
[ExecuteInEditMode]
public class FashionCurves : MonoBehaviour
{
    [Header("Primary Curve References")]
    [Tooltip("Reference to SheerCurves for raked Z stern profile lookup.")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to TopLineCurves for Transom Rake Top Corner (P4) top point.")]
    public TopLineCurves topLineCurves;

    [Tooltip("Reference to BreadthCurves for Fashion Breadth (P3) mid-section point.")]
    public BreadthCurves breadthCurves;

    [Tooltip("Reference to BilgeCurves for Bilge transom corner point.")]
    public BilgeCurves bilgeCurves;

    [Tooltip("Reference to FloorCurves for Floor Stern Tip (P1) bottom point.")]
    public FloorCurves floorCurves;

    [Tooltip("Reference to BodyCurves for proportional midship section ratios.")]
    public BodyCurves bodyCurves;

    [Tooltip("Reference to BackboneCurves for keel width half-offset.")]
    public BackboneCurves backboneCurves;


    [Header("Fashion Curve Proportional Settings")]
    [Tooltip("Relative height ratio of the Transom Bilge Point (P2) between Floor Stern Tip (0.0) and Fashion Breadth (1.0). Synced to BodyCurves.")]
    [Range(0.05f, 0.95f)] public float bilgeHeightRatio = 0.50f;

    [Tooltip("Relative width ratio of the Transom Bilge Point (P2) between Floor Stern Tip (0.0) and Fashion Breadth (1.0). Synced to BodyCurves.")]
    [Range(0.05f, 0.95f)] public float bilgeWidthRatio = 0.50f;

    [Header("Inner Line Settings")]
    [Tooltip("Enable rendering and evaluation of the Inner Fashion Timber curve passing through the transom corners.")]
    public bool showInnerLine = true;
    public Color innerLineColor = new Color(0.2f, 0.95f, 0.6f, 0.95f); // Vibrant Seafoam Green

    [Header("Gizmo Visualization Settings")]
    [Tooltip("Show or hide the node spheres in the Scene view for clean inspection.")]
    public bool showNodeSpheres = true;
    public bool showLabels = true;
    public Color fashionCurveColor = new Color(0.95f, 0.25f, 0.85f, 0.95f); // Vibrant Rose / Magenta
    public Color counterTopColor = new Color(1.0f, 0.6f, 0.1f, 0.95f); // Vibrant Amber / Gold for Counter Top

    [Header("Initialization State")]
    public bool isInitialized = false;

    private readonly NurbsSectionEvaluator nurbsEvaluator = new NurbsSectionEvaluator();

    // --- CACHED STARBOARD 3D CONTROL NODES (P1..P4 & Counter Top) ---
    [HideInInspector] public Vector3 stbdP1; // Floor Stern Tip (Bottom, u=0.25)
    [HideInInspector] public Vector3 stbdP2; // Transom Bilge Node (Middle, u=0.50)
    [HideInInspector] public Vector3 stbdCounterTop; // Counter Top Node (Apex of Counter / Base of Transom)
    [HideInInspector] public Vector3 stbdP3; // Fashion Breadth (u=0.75)
    [HideInInspector] public Vector3 stbdP4; // Transom Rake Top Corner / Fashion TopLine (Top, u=1.00)

    // --- CACHED PORT 3D CONTROL NODES (P1..P4 & Counter Top) ---
    [HideInInspector] public Vector3 portP1;
    [HideInInspector] public Vector3 portP2;
    [HideInInspector] public Vector3 portCounterTop;
    [HideInInspector] public Vector3 portP3;
    [HideInInspector] public Vector3 portP4;

    // --- CACHED STARBOARD 3D INNER CONTROL NODES (P1..P4 & Counter Top) ---
    [HideInInspector] public Vector3 innerStbdP1; // Floor Inner Transom Corner / Stern Tip
    [HideInInspector] public Vector3 innerStbdP2; // Bilge Inner Transom Corner
    [HideInInspector] public Vector3 innerStbdCounterTop; // Counter Top Inner Transom Corner
    [HideInInspector] public Vector3 innerStbdP3; // Breadth Inner Transom Corner
    [HideInInspector] public Vector3 innerStbdP4; // TopLine Inner Transom Corner

    // --- CACHED PORT 3D INNER CONTROL NODES (P1..P4 & Counter Top) ---
    [HideInInspector] public Vector3 innerPortP1;
    [HideInInspector] public Vector3 innerPortP2;
    [HideInInspector] public Vector3 innerPortCounterTop;
    [HideInInspector] public Vector3 innerPortP3;
    [HideInInspector] public Vector3 innerPortP4;



    private Vector3[] cachedStbdOuterPts;
    private Vector3[] cachedPortOuterPts;
    private Vector3[] cachedStbdInnerPts;
    private Vector3[] cachedPortInnerPts;
    private bool isUpdatingCurves = false;

    [HideInInspector] public int changeVersion = 0;

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Reset()
    {
        InitializeFashionCurves();
    }

    public void EnsureInitialized()
    {
        if (!isInitialized || cachedStbdOuterPts == null || cachedStbdOuterPts.Length == 0)
        {
            InitializeFashionCurves();
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

    }

    [ContextMenu("Reset Fashion Curves Profile")]
    public void InitializeFashionCurves()
    {
        FindReferences();
        if (bodyCurves != null)
        {
            bilgeHeightRatio = bodyCurves.bilgeHeightRatio;
            bilgeWidthRatio = bodyCurves.bilgeWidthRatio;
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
        if (isUpdatingCurves) return;
        isUpdatingCurves = true;
        try
        {
            FindReferences();

            if (sheerCurves != null) sheerCurves.UpdateCachedProfile();
            if (topLineCurves != null) topLineCurves.UpdateCachedCurves();
            if (breadthCurves != null) breadthCurves.UpdateCachedCurves();
            if (floorCurves != null) floorCurves.UpdateCachedCurves();

            float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);

            // Sync ratios from BodyCurves if assigned
            if (bodyCurves != null)
            {
                bilgeHeightRatio = bodyCurves.bilgeHeightRatio;
                bilgeWidthRatio = bodyCurves.bilgeWidthRatio;
            }

            // 1. Bottom Node P1: Floor Stern Tip (u = 0.25)
            float p1X = (floorCurves != null) ? floorCurves.aftP3.x : hw;
            float p1Y = (floorCurves != null) ? floorCurves.aftP3.y : ((sheerCurves != null) ? sheerCurves.keelY + 0.5f : 0.5f);
            float p1Z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p1Y) : -10f;

            stbdP1 = new Vector3(Mathf.Abs(p1X), p1Y, p1Z);

            // 2. Middle Node P3: Fashion Breadth from BreadthCurves (u = 0.75)
            float p3X = (breadthCurves != null) ? (breadthCurves.cachedIsAftCubic ? breadthCurves.aftP3.x : breadthCurves.aftP2.x) : (hw + 2f);
            float p3Y = (breadthCurves != null) ? (breadthCurves.cachedIsAftCubic ? breadthCurves.aftP3.y : breadthCurves.aftP2.y) : (p1Y + 2f);
            float p3Z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p3Y) : -10f;

            stbdP3 = new Vector3(Mathf.Abs(p3X), p3Y, p3Z);

            // 3. Lower Mould Node P2: Transom Bilge Node (u = 0.50, derived from BodyCurves master bilge node)
            float tY = Mathf.Clamp01(bilgeHeightRatio);
            float tX = Mathf.Clamp01(bilgeWidthRatio);

            if (bodyCurves != null)
            {
                float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
                SectionNodes bodyNodes = bodyCurves.GetSectionNodesAtZ(midZ, 1f);
                float bSpanX = bodyNodes.p3.x - bodyNodes.p1.x;
                float bSpanY = bodyNodes.p3.y - bodyNodes.p1.y;

                if (Mathf.Abs(bSpanX) > 0.001f) tX = Mathf.Clamp01((Mathf.Abs(bodyNodes.p2.x) - bodyNodes.p1.x) / bSpanX);
                if (Mathf.Abs(bSpanY) > 0.001f) tY = Mathf.Clamp01((bodyNodes.p2.y - bodyNodes.p1.y) / bSpanY);
            }

            float p2Y = Mathf.Lerp(stbdP1.y, stbdP3.y, tY);
            float p2X = Mathf.Lerp(stbdP1.x, stbdP3.x, tX);
            float p2Z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p2Y) : Mathf.Lerp(stbdP1.z, stbdP3.z, tY);

            stbdP2 = new Vector3(p2X, p2Y, p2Z);

            // 4. Upper Mould Top Node P4: Fashion TopLine Point (u = 1.00)
            // MUST ALWAYS BE 100% COMPLETELY ALIGNED WITH TRANSOM RAKE TOP CORNER (topLineCurves.aftP2 / sheerCurves.transomP1)
            float p4Y = (topLineCurves != null) ? topLineCurves.aftP2.y : ((sheerCurves != null) ? sheerCurves.transomP1.y : p3Y + 1.5f);
            float p4Z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p4Y) : ((topLineCurves != null) ? topLineCurves.aftP2.z : -10f);
            float p4X = (topLineCurves != null) ? topLineCurves.aftP2.x : ((breadthCurves != null) ? breadthCurves.GetAftProjectionPointAtZ(p4Z).x : p3X);

            stbdP4 = new Vector3(Mathf.Abs(p4X), p4Y, p4Z);

            // 5. Counter Top Node: Counter Top Landmark (counterP3 / transomP0)
            // MUST ALWAYS BE 100% COMPLETELY ALIGNED WITH SHEERCURVES COUNTER TOP (sheerCurves.counterP3)
            float cTopY = (sheerCurves != null) ? (sheerCurves.enableCounter ? sheerCurves.counterP3.y : sheerCurves.sternP3.y) : (p1Y + 2f);
            float cTopZ = (sheerCurves != null) ? (sheerCurves.enableCounter ? sheerCurves.counterP3.z : sheerCurves.sternP3.z) : -10f;
            float tCTop = Mathf.Clamp(GetOuterTAtY(cTopY), 0.01f, 0.99f);
            Vector3 cTopPt = EvaluateFashionPointRaw(tCTop, 1f);
            stbdCounterTop = new Vector3(Mathf.Abs(cTopPt.x), cTopY, cTopZ);

            // Mirror Port Nodes (-X)
            portP1 = MirrorX(stbdP1);
            portP2 = MirrorX(stbdP2);
            portCounterTop = MirrorX(stbdCounterTop);
            portP3 = MirrorX(stbdP3);
            portP4 = MirrorX(stbdP4);


            // Update BilgeCurves so its aftP2 reflects stbdP2 and its inner curve is synchronized
            if (bilgeCurves != null)
            {
                bilgeCurves.UpdateCachedCurves();
            }

            UpdateOuterFashionCurve();
            UpdateInnerFashionCurve();

            changeVersion++;
        }
        finally
        {
            isUpdatingCurves = false;
        }
    }

    /// <summary>
    /// Constructs a SectionNodes struct representing the 3D Transom section curve.
    /// Nodes p1, p2, p3, p4 correspond to Floor Stern Tip (u=0.25), Transom Bilge (u=0.50),
    /// Fashion Breadth (u=0.75), and Transom Rake Top Corner (u=1.00).
    /// </summary>
    public SectionNodes GetTransomSectionNodes(float side)
    {
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : 0.25f;
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        SectionNodes nodes = new SectionNodes();
        nodes.p0 = new Vector3(sideSign * hw, keelY, stbdP1.z); // Keel baseline anchor
        nodes.p1 = new Vector3(sideSign * stbdP1.x, stbdP1.y, stbdP1.z); // Floor Stern Tip (u=0.25)
        nodes.p2 = new Vector3(sideSign * stbdP2.x, stbdP2.y, stbdP2.z); // Transom Bilge Node (u=0.50)
        nodes.p3 = new Vector3(sideSign * stbdP3.x, stbdP3.y, stbdP3.z); // Fashion Breadth (u=0.75)
        nodes.p4 = new Vector3(sideSign * stbdP4.x, stbdP4.y, stbdP4.z); // Transom Rake Top Corner (u=1.00)

        return nodes;
    }

    // --- SPATIAL EVALUATION METHODS ---

    /// <summary>
    /// Evaluates 3D point on Fashion Curve using continuous C1 Hermite spline across the 4 transom nodes:
    /// - P1 (Floor Stern Tip at t = 0.00)
    /// - P2 (Transom Bilge Node at t = 0.33)
    /// - P3 (Fashion Breadth Node at t = 0.67)
    /// - P4 (Transom Rake Top Corner at t = 1.00)
    /// Half-breadth is strictly restricted under the Breadth Curves Aft Curves Projection Line.
    /// Z is locked 100% to SheerCurves raked stern profile.
    /// </summary>
    public Vector3 GetFashionPointAtT(float t, float side)
    {
        EnsureInitialized();
        return EvaluateFashionPointRaw(t, side);
    }

    private Vector3 EvaluateFashionPointRaw(float t, float side)
    {
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        Vector3 p1 = (sideSign > 0f) ? stbdP1 : portP1;
        Vector3 p2 = (sideSign > 0f) ? stbdP2 : portP2;
        Vector3 p3 = (sideSign > 0f) ? stbdP3 : portP3;
        Vector3 p4 = (sideSign > 0f) ? stbdP4 : portP4;

        float clampedT = Mathf.Clamp01(t);
        if (clampedT <= 0.0001f) return p1;

        // 1. Evaluate 2D (X, Y) curve through the 4 structural transom nodes
        Vector2 p1XY = new Vector2(p1.x, p1.y);
        Vector2 p2XY = new Vector2(p2.x, p2.y);
        Vector2 p3XY = new Vector2(p3.x, p3.y);
        Vector2 p4XY = new Vector2(p4.x, p4.y);

        Vector2 t2XY = (p3XY - p1XY).sqrMagnitude > 0.0001f ? (p3XY - p1XY).normalized : Vector2.up;
        Vector2 t3XY = (p4XY - p2XY).sqrMagnitude > 0.0001f ? (p4XY - p2XY).normalized : Vector2.up;

        Vector2 d12XY = (p2XY - p1XY).sqrMagnitude > 0.0001f ? (p2XY - p1XY).normalized : Vector2.up;
        Vector2 v1XY = 2f * d12XY - t2XY;
        Vector2 t1XY = v1XY.sqrMagnitude > 0.001f ? v1XY.normalized : d12XY;

        Vector2 d34XY = (p4XY - p3XY).sqrMagnitude > 0.0001f ? (p4XY - p3XY).normalized : Vector2.up;
        Vector2 v4XY = 2f * d34XY - t3XY;
        Vector2 t4XY = v4XY.sqrMagnitude > 0.001f ? v4XY.normalized : d34XY;

        Vector2 ptXY;
        if (clampedT <= (1f / 3f))
        {
            float s = clampedT * 3f;
            ptXY = EvaluateHermiteSegment2D(p1XY, t1XY, p2XY, t2XY, s);
        }
        else if (clampedT <= (2f / 3f))
        {
            float s = (clampedT - (1f / 3f)) * 3f;
            ptXY = EvaluateHermiteSegment2D(p2XY, t2XY, p3XY, t3XY, s);
        }
        else
        {
            float s = (clampedT - (2f / 3f)) * 3f;
            ptXY = EvaluateHermiteSegment2D(p3XY, t3XY, p4XY, t4XY, s);
        }

        // 2. Lock Z to the Stern Profile at evaluated height Y
        float z = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(ptXY.y) : Mathf.Lerp(p1.z, p4.z, clampedT);

        // 3. Restriction: Below Breadth (t <= 2/3), Fashion Curve half-breadth must remain under the restriction of Breadth Curves and its Aft Projection Line
        if (breadthCurves != null && clampedT <= (2f / 3f))
        {
            float maxBreadthX = (z <= stbdP3.z)
                ? breadthCurves.GetAftProjectionPointAtZ(z).x
                : breadthCurves.GetBreadthXAtZ(z);

            if (Mathf.Abs(ptXY.x) > maxBreadthX)
            {
                ptXY.x = sideSign * maxBreadthX;
            }
        }

        return new Vector3(ptXY.x, ptXY.y, z);
    }

    private float GetOuterTAtY(float targetY)
    {
        float ySpan = stbdP4.y - stbdP1.y;
        if (ySpan < 0.0001f) return 0.5f;

        float low = 0f;
        float high = 1f;
        float t = Mathf.Clamp01((targetY - stbdP1.y) / ySpan);

        for (int i = 0; i < 20; i++)
        {
            Vector3 pt = EvaluateFashionPointRaw(t, 1f);
            float err = pt.y - targetY;
            if (Mathf.Abs(err) < 0.0001f) return t;

            if (pt.y < targetY) low = Mathf.Max(low, t);
            else high = Mathf.Min(high, t);

            float delta = 0.001f;
            Vector3 ptPlus = EvaluateFashionPointRaw(Mathf.Clamp01(t + delta), 1f);
            float dydt = (ptPlus.y - pt.y) / delta;
            if (Mathf.Abs(dydt) > 0.0001f)
            {
                float nextT = t - err / dydt;
                if (nextT >= low && nextT <= high)
                {
                    t = nextT;
                    continue;
                }
            }
            t = (low + high) * 0.5f;
        }
        return t;
    }

    private float GetInnerTAtY(float targetY)
    {
        float ySpan = innerStbdP4.y - innerStbdP1.y;
        if (ySpan < 0.0001f) return 0.5f;

        float low = 0f;
        float high = 1f;
        float t = Mathf.Clamp01((targetY - innerStbdP1.y) / ySpan);

        for (int i = 0; i < 20; i++)
        {
            Vector3 pt = EvaluateInnerFashionPointRaw(t, 1f);
            float err = pt.y - targetY;
            if (Mathf.Abs(err) < 0.0001f) return t;

            if (pt.y < targetY) low = Mathf.Max(low, t);
            else high = Mathf.Min(high, t);

            float delta = 0.001f;
            Vector3 ptPlus = EvaluateInnerFashionPointRaw(Mathf.Clamp01(t + delta), 1f);
            float dydt = (ptPlus.y - pt.y) / delta;
            if (Mathf.Abs(dydt) > 0.0001f)
            {
                float nextT = t - err / dydt;
                if (nextT >= low && nextT <= high)
                {
                    t = nextT;
                    continue;
                }
            }
            t = (low + high) * 0.5f;
        }
        return t;
    }

    /// <summary>
    /// Evaluates 3D point on Fashion Curve at target height Y for specified side (+1 Starboard, -1 Port).
    /// Uses precomputed curve samples with fast binary search for high performance.
    /// </summary>
    public Vector3 GetFashionPointAtY(float targetY, float side)
    {
        EnsureInitialized();

        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        Vector3[] pts = (sideSign > 0f) ? cachedStbdOuterPts : cachedPortOuterPts;

        if (pts != null && pts.Length >= 2)
        {
            if (targetY <= pts[0].y) return pts[0];
            if (targetY >= pts[pts.Length - 1].y) return pts[pts.Length - 1];

            int low = 0;
            int high = pts.Length - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (pts[mid].y < targetY)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            int i0 = Mathf.Clamp(high, 0, pts.Length - 2);
            int i1 = i0 + 1;
            float dy = pts[i1].y - pts[i0].y;
            float frac = (Mathf.Abs(dy) > 0.0001f) ? Mathf.Clamp01((targetY - pts[i0].y) / dy) : 0f;
            return Vector3.Lerp(pts[i0], pts[i1], frac);
        }

        float ySpan = stbdP4.y - stbdP1.y;
        if (ySpan < 0.0001f) return GetFashionPointAtT(0f, side);
        float t = Mathf.Clamp01((targetY - stbdP1.y) / ySpan);
        return EvaluateFashionPointRaw(t, side);
    }

    /// <summary>
    /// Evaluates 3D point on Fashion Curve at target station Z for specified side (+1 Starboard, -1 Port).
    /// Uses precomputed curve samples with fast O(log N) binary search for instant, zero-allocation evaluation.
    /// </summary>
    public Vector3 GetFashionPointAtZ(float targetZ, float side)
    {
        EnsureInitialized();

        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        Vector3[] pts = (sideSign > 0f) ? cachedStbdOuterPts : cachedPortOuterPts;

        if (pts != null && pts.Length >= 2)
        {
            // Stern profile rakes aft: pts[0].z >= targetZ >= pts[last].z
            if (targetZ >= pts[0].z) return pts[0];
            if (targetZ <= pts[pts.Length - 1].z) return pts[pts.Length - 1];

            int low = 0;
            int high = pts.Length - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (pts[mid].z > targetZ)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            int i0 = Mathf.Clamp(high, 0, pts.Length - 2);
            int i1 = i0 + 1;
            float dz = pts[i1].z - pts[i0].z;
            float frac = (Mathf.Abs(dz) > 0.0001f) ? Mathf.Clamp01((targetZ - pts[i0].z) / dz) : 0f;
            return Vector3.Lerp(pts[i0], pts[i1], frac);
        }

        return (sideSign > 0f) ? stbdP1 : portP1;
    }

    /// <summary>
    /// Returns precomputed points along the Outer Fashion Curve with 0 GC heap allocations.
    /// </summary>
    public virtual Vector3[] GetFashionPoints(bool starboard = true)
    {
        EnsureInitialized();
        return starboard ? cachedStbdOuterPts : cachedPortOuterPts;
    }

    private Vector3 MirrorX(Vector3 pt)
    {
        return new Vector3(-pt.x, pt.y, pt.z);
    }

    // --- CURVE PRECOMPUTATION & CACHING ---

    private void UpdateOuterFashionCurve()
    {
        float cTopY = (sheerCurves != null) ? (sheerCurves.enableCounter ? sheerCurves.counterP3.y : sheerCurves.sternP3.y) : (stbdP1.y + 2f);
        bool hasCounterKnuckle = (sheerCurves != null && sheerCurves.enableCounter && cTopY > stbdP1.y + 0.01f && cTopY < stbdP4.y - 0.01f);

        if (hasCounterKnuckle)
        {
            const int stepsLower = 35;
            const int stepsUpper = 25;
            const int totalPts = stepsLower + stepsUpper + 1;
            if (cachedStbdOuterPts == null || cachedStbdOuterPts.Length != totalPts)
            {
                cachedStbdOuterPts = new Vector3[totalPts];
                cachedPortOuterPts = new Vector3[totalPts];
            }

            float tCTop = Mathf.Clamp(GetOuterTAtY(cTopY), 0.01f, 0.99f);

            // Lower portion: Floor (t=0) up to Counter Top (t=tCTop) with exact knuckle knot
            for (int i = 0; i <= stepsLower; i++)
            {
                float frac = (float)i / stepsLower;
                float t = frac * tCTop;
                Vector3 pt = (i == stepsLower) ? stbdCounterTop : ((i == 0) ? stbdP1 : EvaluateFashionPointRaw(t, 1f));
                cachedStbdOuterPts[i] = pt;
                cachedPortOuterPts[i] = MirrorX(pt);
            }

            // Upper portion: Counter Top (t=tCTop) to TopLine (t=1.0)
            for (int i = 1; i <= stepsUpper; i++)
            {
                float frac = (float)i / stepsUpper;
                float t = Mathf.Lerp(tCTop, 1f, frac);
                Vector3 pt = (i == stepsUpper) ? stbdP4 : EvaluateFashionPointRaw(t, 1f);
                int idx = stepsLower + i;
                cachedStbdOuterPts[idx] = pt;
                cachedPortOuterPts[idx] = MirrorX(pt);
            }
        }
        else
        {
            const int totalSteps = 60;
            int totalPts = totalSteps + 1;
            if (cachedStbdOuterPts == null || cachedStbdOuterPts.Length != totalPts)
            {
                cachedStbdOuterPts = new Vector3[totalPts];
                cachedPortOuterPts = new Vector3[totalPts];
            }

            for (int i = 0; i <= totalSteps; i++)
            {
                float t = (float)i / totalSteps;
                Vector3 pt = (i == totalSteps) ? stbdP4 : ((i == 0) ? stbdP1 : EvaluateFashionPointRaw(t, 1f));
                cachedStbdOuterPts[i] = pt;
                cachedPortOuterPts[i] = MirrorX(cachedStbdOuterPts[i]);
            }
        }
    }

    // --- INNER FASHION TIMBER CURVE IMPLEMENTATION ---

    private void UpdateInnerFashionCurve()
    {
        // Sample exact inner transom corners from longitudinal curves
        innerStbdP1 = (floorCurves != null) ? floorCurves.GetInnerFloorSternTip(true) : stbdP1;
        innerStbdP2 = (bilgeCurves != null) ? bilgeCurves.GetInnerBilgeSternTip(true) : stbdP2;
        innerStbdP3 = (breadthCurves != null) ? breadthCurves.GetInnerBreadthSternTip(true) : stbdP3;
        innerStbdP4 = (topLineCurves != null && topLineCurves.showInnerLine) ? topLineCurves.GetInnerTopLineSternTip(true) : stbdP4;

        // Ensure innerStbdP4 remains inside outer stbdP4
        if (innerStbdP4.x > stbdP4.x)
        {
            innerStbdP4.x = stbdP4.x;
        }

        float cTopY = (sheerCurves != null) ? (sheerCurves.enableCounter ? sheerCurves.counterP3.y : sheerCurves.sternP3.y) : (innerStbdP1.y + 2f);
        bool hasCounterKnuckle = (sheerCurves != null && sheerCurves.enableCounter && cTopY > innerStbdP1.y + 0.01f && cTopY < innerStbdP4.y - 0.01f);

        float tInnerCTop = hasCounterKnuckle ? Mathf.Clamp(GetInnerTAtY(cTopY), 0.01f, 0.99f) : 0.5f;
        Vector3 innerCTopPt = EvaluateInnerFashionPointRaw(tInnerCTop, 1f);
        innerStbdCounterTop = new Vector3(Mathf.Abs(innerCTopPt.x), cTopY, innerCTopPt.z);

        innerPortP1 = MirrorX(innerStbdP1);
        innerPortP2 = MirrorX(innerStbdP2);
        innerPortCounterTop = MirrorX(innerStbdCounterTop);
        innerPortP3 = MirrorX(innerStbdP3);
        innerPortP4 = MirrorX(innerStbdP4);

        if (hasCounterKnuckle)
        {
            const int stepsLower = 35;
            const int stepsUpper = 25;
            const int totalPts = stepsLower + stepsUpper + 1;
            if (cachedStbdInnerPts == null || cachedStbdInnerPts.Length != totalPts)
            {
                cachedStbdInnerPts = new Vector3[totalPts];
                cachedPortInnerPts = new Vector3[totalPts];
            }

            // Lower portion: Floor to Counter Top
            for (int i = 0; i <= stepsLower; i++)
            {
                float frac = (float)i / stepsLower;
                float t = frac * tInnerCTop;
                Vector3 pt = (i == stepsLower) ? innerStbdCounterTop : ((i == 0) ? innerStbdP1 : EvaluateInnerFashionPointRaw(t, 1f));
                cachedStbdInnerPts[i] = pt;
                cachedPortInnerPts[i] = MirrorX(pt);
            }

            // Upper portion: Counter Top to TopLine
            for (int i = 1; i <= stepsUpper; i++)
            {
                float frac = (float)i / stepsUpper;
                float t = Mathf.Lerp(tInnerCTop, 1f, frac);
                Vector3 pt = (i == stepsUpper) ? innerStbdP4 : EvaluateInnerFashionPointRaw(t, 1f);
                int idx = stepsLower + i;
                cachedStbdInnerPts[idx] = pt;
                cachedPortInnerPts[idx] = MirrorX(pt);
            }
        }
        else
        {
            const int totalSteps = 60;
            int totalPts = totalSteps + 1;
            if (cachedStbdInnerPts == null || cachedStbdInnerPts.Length != totalPts)
            {
                cachedStbdInnerPts = new Vector3[totalPts];
                cachedPortInnerPts = new Vector3[totalPts];
            }

            for (int i = 0; i <= totalSteps; i++)
            {
                float t = (float)i / totalSteps;
                Vector3 pt = (i == totalSteps) ? innerStbdP4 : ((i == 0) ? innerStbdP1 : EvaluateInnerFashionPointRaw(t, 1f));
                cachedStbdInnerPts[i] = pt;
                cachedPortInnerPts[i] = MirrorX(pt);
            }
        }
    }

    private Vector2 EvaluateHermiteSegment2D(Vector2 pA, Vector2 tA, Vector2 pB, Vector2 tB, float s)
    {
        s = Mathf.Clamp01(s);
        float dist = Vector2.Distance(pA, pB);
        if (dist <= 0.0001f) return pA;

        Vector2 cA = pA + tA * (dist / 3f);
        Vector2 cB = pB - tB * (dist / 3f);

        float u = 1f - s;
        float u2 = u * u;
        float u3 = u2 * u;
        float s2 = s * s;
        float s3 = s2 * s;

        return (u3 * pA) + (3f * u2 * s * cA) + (3f * u * s2 * cB) + (s3 * pB);
    }

    private float EvaluateHermite1D(float pA, float tA, float pB, float tB, float s)
    {
        s = Mathf.Clamp01(s);
        float s2 = s * s;
        float s3 = s2 * s;
        float h00 = 2f * s3 - 3f * s2 + 1f;
        float h10 = s3 - 2f * s2 + s;
        float h01 = -2f * s3 + 3f * s2;
        float h11 = s3 - s2;
        return (h00 * pA) + (h10 * tA) + (h01 * pB) + (h11 * tB);
    }

    /// <summary>
    /// Evaluates 3D point on the Inner Fashion Timber curve:
    /// Parallels the 3D Stern Profile from SheerCurves while passing 100% exactly through:
    /// - Floor Inner Transom Corner / Stern Tip at t = 0.00 (innerStbdP1)
    /// - Bilge Inner Transom Corner at t = 0.33 (innerStbdP2)
    /// - Breadth Inner Transom Corner at t = 0.67 (innerStbdP3)
    /// - TopLine Inner Transom Corner at t = 1.00 (innerStbdP4)
    /// Half-breadth is strictly restricted under the Inner Breadth Curves Projection Line and Outer Fashion Curve.
    /// </summary>
    public virtual Vector3 GetInnerFashionPointAtT(float t, float side = 1f)
    {
        EnsureInitialized();
        return EvaluateInnerFashionPointRaw(t, side);
    }

    private Vector3 EvaluateInnerFashionPointRaw(float t, float side = 1f)
    {
        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        Vector3 p1 = (sideSign > 0f) ? innerStbdP1 : innerPortP1;
        Vector3 p2 = (sideSign > 0f) ? innerStbdP2 : innerPortP2;
        Vector3 p3 = (sideSign > 0f) ? innerStbdP3 : innerPortP3;
        Vector3 p4 = (sideSign > 0f) ? innerStbdP4 : innerPortP4;

        float clampedT = Mathf.Clamp01(t);
        if (clampedT <= 0.0001f) return p1;

        // 1. Evaluate 2D (X, Y) curve through the 4 inner transom corners
        Vector2 p1XY = new Vector2(p1.x, p1.y);
        Vector2 p2XY = new Vector2(p2.x, p2.y);
        Vector2 p3XY = new Vector2(p3.x, p3.y);
        Vector2 p4XY = new Vector2(p4.x, p4.y);

        Vector2 t2XY = (p3XY - p1XY).sqrMagnitude > 0.0001f ? (p3XY - p1XY).normalized : Vector2.up;
        Vector2 t3XY = (p4XY - p2XY).sqrMagnitude > 0.0001f ? (p4XY - p2XY).normalized : Vector2.up;

        Vector2 d12XY = (p2XY - p1XY).sqrMagnitude > 0.0001f ? (p2XY - p1XY).normalized : Vector2.up;
        Vector2 v1XY = 2f * d12XY - t2XY;
        Vector2 t1XY = v1XY.sqrMagnitude > 0.001f ? v1XY.normalized : d12XY;

        Vector2 d34XY = (p4XY - p3XY).sqrMagnitude > 0.0001f ? (p4XY - p3XY).normalized : Vector2.up;
        Vector2 v4XY = 2f * d34XY - t3XY;
        Vector2 t4XY = v4XY.sqrMagnitude > 0.001f ? v4XY.normalized : d34XY;

        // 2. Compute Stern Profile forward offsets deltaZ at all 4 corners
        float zStern1 = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p1.y) : p1.z;
        float zStern2 = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p2.y) : p2.z;
        float zStern3 = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p3.y) : p3.z;
        float zStern4 = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(p4.y) : p4.z;

        float dz1 = p1.z - zStern1;
        float dz2 = p2.z - zStern2;
        float dz3 = p3.z - zStern3;
        float dz4 = p4.z - zStern4;

        float slope2 = 0.5f * (dz3 - dz1);
        float slope3 = 0.5f * (dz4 - dz2);
        float slope1 = 2f * (dz2 - dz1) - slope2;
        float slope4 = 2f * (dz4 - dz3) - slope3;

        Vector2 ptXY;
        float deltaZ;

        if (clampedT <= (1f / 3f))
        {
            float s = clampedT * 3f;
            ptXY = EvaluateHermiteSegment2D(p1XY, t1XY, p2XY, t2XY, s);
            deltaZ = EvaluateHermite1D(dz1, slope1, dz2, slope2, s);
        }
        else if (clampedT <= (2f / 3f))
        {
            float s = (clampedT - (1f / 3f)) * 3f;
            ptXY = EvaluateHermiteSegment2D(p2XY, t2XY, p3XY, t3XY, s);
            deltaZ = EvaluateHermite1D(dz2, slope2, dz3, slope3, s);
        }
        else
        {
            float s = (clampedT - (2f / 3f)) * 3f;
            ptXY = EvaluateHermiteSegment2D(p3XY, t3XY, p4XY, t4XY, s);
            deltaZ = EvaluateHermite1D(dz3, slope3, dz4, slope4, s);
        }

        // 3. Synthesize Z: lock 100% to Stern Profile at evaluated height Y, offset by smooth deltaZ
        float zStern = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(ptXY.y) : Mathf.Lerp(p1.z, p4.z, clampedT);
        float finalZ = zStern + deltaZ;

        // 4. Restriction: Below Breadth (t <= 2/3), Inner Fashion Curve half-breadth must remain under the restriction of the Inner Breadth Curves Projection Line
        if (breadthCurves != null && clampedT <= (2f / 3f))
        {
            float maxInnerBreadthX = (finalZ <= innerStbdP3.z)
                ? breadthCurves.GetInnerAftProjectionPointAtZ(finalZ, true).x
                : breadthCurves.GetInnerBreadthXAtZ(finalZ, true);

            if (Mathf.Abs(ptXY.x) > maxInnerBreadthX)
            {
                ptXY.x = sideSign * maxInnerBreadthX;
            }
        }

        // 5. Restriction: Inner Fashion Curve must remain inside the Outer Fashion Curve
        Vector3 outerPt = EvaluateFashionPointRaw(clampedT, sideSign);
        if (Mathf.Abs(ptXY.x) > Mathf.Abs(outerPt.x))
        {
            ptXY.x = sideSign * Mathf.Abs(outerPt.x);
        }

        return new Vector3(ptXY.x, ptXY.y, finalZ);
    }

    /// <summary>
    /// Evaluates 3D point on the Inner Fashion Timber curve at target height Y for specified side (+1 Starboard, -1 Port).
    /// Uses precomputed inner curve samples with fast binary search for high performance.
    /// </summary>
    public virtual Vector3 GetInnerFashionPointAtY(float targetY, float side = 1f)
    {
        EnsureInitialized();

        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        Vector3[] pts = (sideSign > 0f) ? cachedStbdInnerPts : cachedPortInnerPts;

        if (pts != null && pts.Length >= 2)
        {
            if (targetY <= pts[0].y) return pts[0];
            if (targetY >= pts[pts.Length - 1].y) return pts[pts.Length - 1];

            int low = 0;
            int high = pts.Length - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (pts[mid].y < targetY)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            int i0 = Mathf.Clamp(high, 0, pts.Length - 2);
            int i1 = i0 + 1;
            float dy = pts[i1].y - pts[i0].y;
            float frac = (Mathf.Abs(dy) > 0.0001f) ? Mathf.Clamp01((targetY - pts[i0].y) / dy) : 0f;
            return Vector3.Lerp(pts[i0], pts[i1], frac);
        }

        float ySpan = innerStbdP4.y - innerStbdP1.y;
        if (ySpan < 0.0001f) return GetInnerFashionPointAtT(0f, sideSign);
        float t = Mathf.Clamp01((targetY - innerStbdP1.y) / ySpan);
        return EvaluateInnerFashionPointRaw(t, sideSign);
    }

    /// <summary>
    /// Evaluates 3D point on the Inner Fashion Timber curve at target station Z for specified side (+1 Starboard, -1 Port).
    /// Uses precomputed curve samples with fast O(log N) binary search for instant, zero-allocation evaluation.
    /// </summary>
    public virtual Vector3 GetInnerFashionPointAtZ(float targetZ, float side = 1f)
    {
        EnsureInitialized();

        float sideSign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        Vector3[] pts = (sideSign > 0f) ? cachedStbdInnerPts : cachedPortInnerPts;

        if (pts != null && pts.Length >= 2)
        {
            if (targetZ >= pts[0].z) return pts[0];
            if (targetZ <= pts[pts.Length - 1].z) return pts[pts.Length - 1];

            int low = 0;
            int high = pts.Length - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (pts[mid].z > targetZ)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            int i0 = Mathf.Clamp(high, 0, pts.Length - 2);
            int i1 = i0 + 1;
            float dz = pts[i1].z - pts[i0].z;
            float frac = (Mathf.Abs(dz) > 0.0001f) ? Mathf.Clamp01((targetZ - pts[i0].z) / dz) : 0f;
            return Vector3.Lerp(pts[i0], pts[i1], frac);
        }

        return (sideSign > 0f) ? innerStbdP1 : innerPortP1;
    }

    /// <summary>
    /// Returns precomputed points along the Inner Fashion Timber curve with 0 GC heap allocations.
    /// </summary>
    public virtual Vector3[] GetInnerFashionPoints(bool starboard = true)
    {
        EnsureInitialized();
        return starboard ? cachedStbdInnerPts : cachedPortInnerPts;
    }

    // --- GIZMO RENDERING ---

    private static GUIStyle cachedLabelStyle;
    private static GUIStyle cachedInnerLabelStyle;
#if UNITY_EDITOR
    private static GUIStyle GetCachedLabelStyle()
    {
        if (cachedLabelStyle == null)
        {
            cachedLabelStyle = new GUIStyle();
            cachedLabelStyle.normal.textColor = Color.magenta;
            cachedLabelStyle.fontSize = 10;
            cachedLabelStyle.alignment = TextAnchor.LowerCenter;
            cachedLabelStyle.fontStyle = FontStyle.Bold;
        }
        return cachedLabelStyle;
    }

    private static GUIStyle GetCachedInnerLabelStyle()
    {
        if (cachedInnerLabelStyle == null)
        {
            cachedInnerLabelStyle = new GUIStyle();
            cachedInnerLabelStyle.normal.textColor = new Color(0.2f, 0.95f, 0.6f);
            cachedInnerLabelStyle.fontSize = 9;
            cachedInnerLabelStyle.alignment = TextAnchor.LowerCenter;
            cachedInnerLabelStyle.fontStyle = FontStyle.Normal;
        }
        return cachedInnerLabelStyle;
    }
#endif

    private void OnDrawGizmos()
    {
        if (sheerCurves == null) return;

        EnsureInitialized();

        // 1. Draw Starboard 3D Fashion NURBS Curve (Vibrant Rose / Magenta)
        DrawFashionNurbsCurve(1f, fashionCurveColor);

        // 2. Draw Port 3D Fashion NURBS Curve (Mirrored X)
        Color portColor = new Color(fashionCurveColor.r, fashionCurveColor.g * 0.6f, fashionCurveColor.b, fashionCurveColor.a);
        DrawFashionNurbsCurve(-1f, portColor);

        // 3. Draw Transverse Crossbars connecting Starboard and Port at P1..P4 and Counter Top
        Gizmos.color = new Color(fashionCurveColor.r, fashionCurveColor.g, fashionCurveColor.b, 0.6f);
        Gizmos.DrawLine(stbdP1, portP1);
        Gizmos.DrawLine(stbdP2, portP2);
        if (sheerCurves != null && sheerCurves.enableCounter)
        {
            Gizmos.color = new Color(counterTopColor.r, counterTopColor.g, counterTopColor.b, 0.85f);
            Gizmos.DrawLine(stbdCounterTop, portCounterTop);
            Gizmos.color = new Color(fashionCurveColor.r, fashionCurveColor.g, fashionCurveColor.b, 0.6f);
        }
        Gizmos.DrawLine(stbdP3, portP3);
        Gizmos.DrawLine(stbdP4, portP4);



        // 4. Draw Structural Node Spheres (P1..P4 & Counter Top lie EXACTLY on the curve)
        if (showNodeSpheres)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(stbdP1, 0.12f); // Floor Stern Tip (P1, u=0.25)
            Gizmos.color = new Color(0.1f, 0.9f, 0.4f);
            Gizmos.DrawSphere(stbdP2, 0.13f); // Transom Bilge Node (P2, u=0.50)
            if (sheerCurves != null && sheerCurves.enableCounter)
            {
                Gizmos.color = counterTopColor;
                Gizmos.DrawSphere(stbdCounterTop, 0.14f); // Counter Top Landmark (Apex of Counter / Base of Transom)
            }
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(stbdP3, 0.14f); // Fashion Breadth (P3, u=0.75)
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(stbdP4, 0.14f); // Transom Rake Top Corner (P4, u=1.00)

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(portP1, 0.12f);
            Gizmos.color = new Color(0.1f, 0.9f, 0.4f);
            Gizmos.DrawSphere(portP2, 0.13f);
            if (sheerCurves != null && sheerCurves.enableCounter)
            {
                Gizmos.color = counterTopColor;
                Gizmos.DrawSphere(portCounterTop, 0.14f);
            }
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(portP3, 0.14f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(portP4, 0.14f);

        }

        // 5. Draw Inner Fashion Timber Curve (Zero GC heap allocations)
        if (showInnerLine && cachedStbdInnerPts != null && cachedStbdInnerPts.Length > 1)
        {
            Gizmos.color = innerLineColor;
            for (int i = 0; i < cachedStbdInnerPts.Length - 1; i++)
            {
                Gizmos.DrawLine(cachedStbdInnerPts[i], cachedStbdInnerPts[i + 1]);
            }

            Color portInnerColor = new Color(innerLineColor.r * 0.8f, innerLineColor.g, innerLineColor.b * 0.8f, innerLineColor.a);
            Gizmos.color = portInnerColor;
            for (int i = 0; i < cachedPortInnerPts.Length - 1; i++)
            {
                Gizmos.DrawLine(cachedPortInnerPts[i], cachedPortInnerPts[i + 1]);
            }

            // Inner crossbars connecting Starboard and Port at inner corners
            Gizmos.color = new Color(innerLineColor.r, innerLineColor.g, innerLineColor.b, 0.5f);
            Gizmos.DrawLine(innerStbdP1, innerPortP1);
            Gizmos.DrawLine(innerStbdP2, innerPortP2);
            if (sheerCurves != null && sheerCurves.enableCounter)
            {
                Gizmos.color = new Color(counterTopColor.r, counterTopColor.g, counterTopColor.b, 0.65f);
                Gizmos.DrawLine(innerStbdCounterTop, innerPortCounterTop);
                Gizmos.color = new Color(innerLineColor.r, innerLineColor.g, innerLineColor.b, 0.5f);
            }
            Gizmos.DrawLine(innerStbdP3, innerPortP3);
            Gizmos.DrawLine(innerStbdP4, innerPortP4);


            // Inner Transom Corner Spheres (matching corner colors)
            if (showNodeSpheres)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(innerStbdP1, 0.08f);
                Gizmos.DrawSphere(innerPortP1, 0.08f);

                Gizmos.color = new Color(0.1f, 0.9f, 0.4f);
                Gizmos.DrawSphere(innerStbdP2, 0.09f);
                Gizmos.DrawSphere(innerPortP2, 0.09f);

                if (sheerCurves != null && sheerCurves.enableCounter)
                {
                    Gizmos.color = counterTopColor;
                    Gizmos.DrawSphere(innerStbdCounterTop, 0.09f);
                    Gizmos.DrawSphere(innerPortCounterTop, 0.09f);
                }

                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(innerStbdP3, 0.09f);
                Gizmos.DrawSphere(innerPortP3, 0.09f);

                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(innerStbdP4, 0.09f);
                Gizmos.DrawSphere(innerPortP4, 0.09f);

            }
        }

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = GetCachedLabelStyle();
            Handles.Label(stbdP4 + Vector3.up * 0.18f, "Fashion TopLine / Transom Rake Top (u=1.00)", labelStyle);

            if (sheerCurves != null && sheerCurves.enableCounter)
            {
                GUIStyle counterLabelStyle = new GUIStyle(labelStyle);
                counterLabelStyle.normal.textColor = counterTopColor;
                Handles.Label(stbdCounterTop + Vector3.up * 0.18f, "Fashion Counter Top (counterP3)", counterLabelStyle);
            }
            Handles.Label(stbdP3 + Vector3.up * 0.18f, "Fashion Breadth (u=0.75)", labelStyle);
            Handles.Label(stbdP2 + Vector3.up * 0.15f, "Fashion Bilge Node (u=0.50)", labelStyle);
            Handles.Label(stbdP1 + Vector3.up * 0.15f, "Fashion NURBS Base (u=0.25)", labelStyle);

            if (showInnerLine)
            {
                GUIStyle innerLabelStyle = GetCachedInnerLabelStyle();
                Handles.Label(innerStbdP4 + Vector3.up * 0.12f, "Inner Fashion TopLine", innerLabelStyle);

                if (sheerCurves != null && sheerCurves.enableCounter)
                {
                    GUIStyle innerCounterStyle = new GUIStyle(innerLabelStyle);
                    innerCounterStyle.normal.textColor = counterTopColor;
                    Handles.Label(innerStbdCounterTop + Vector3.up * 0.12f, "Inner Fashion Counter Top", innerCounterStyle);
                }
                Handles.Label(innerStbdP3 + Vector3.up * 0.12f, "Inner Fashion Breadth", innerLabelStyle);
                Handles.Label(innerStbdP2 + Vector3.up * 0.12f, "Inner Fashion Bilge", innerLabelStyle);
                Handles.Label(innerStbdP1 + Vector3.up * 0.12f, "Inner Fashion Base", innerLabelStyle);
            }
        }
#endif
    }

    private void DrawFashionNurbsCurve(float side, Color color, int steps = 50)
    {
        Vector3[] pts = (side > 0f) ? cachedStbdOuterPts : cachedPortOuterPts;
        if (pts != null && pts.Length > 1)
        {
            Gizmos.color = color;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Gizmos.DrawLine(pts[i], pts[i + 1]);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(FashionCurves))]
public class FashionCurvesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        FashionCurves curves = (FashionCurves)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Transom Fashion NURBS Nodes Readout", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("P1 Floor Stern Base (u=0.25):", $"X: {curves.stbdP1.x:F2}, Y: {curves.stbdP1.y:F2}, Z: {curves.stbdP1.z:F2}");
        EditorGUILayout.LabelField("P2 Transom Bilge Node (u=0.50):", $"X: {curves.stbdP2.x:F2}, Y: {curves.stbdP2.y:F2}, Z: {curves.stbdP2.z:F2}");
        if (curves.sheerCurves != null && curves.sheerCurves.enableCounter)
        {
            EditorGUILayout.LabelField("Counter Top Node (counterP3):", $"X: {curves.stbdCounterTop.x:F2}, Y: {curves.stbdCounterTop.y:F2}, Z: {curves.stbdCounterTop.z:F2}");
        }
        EditorGUILayout.LabelField("P3 Fashion Breadth (u=0.75):", $"X: {curves.stbdP3.x:F2}, Y: {curves.stbdP3.y:F2}, Z: {curves.stbdP3.z:F2}");
        EditorGUILayout.LabelField("P4 Transom Rake Top Corner (u=1.00):", $"X: {curves.stbdP4.x:F2}, Y: {curves.stbdP4.y:F2}, Z: {curves.stbdP4.z:F2}");

        if (curves.showInnerLine)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Inner Fashion Curve Transom Nodes Readout", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Inner P1 Floor Transom Corner:", $"X: {curves.innerStbdP1.x:F2}, Y: {curves.innerStbdP1.y:F2}, Z: {curves.innerStbdP1.z:F2}");
            EditorGUILayout.LabelField("Inner P2 Bilge Transom Corner:", $"X: {curves.innerStbdP2.x:F2}, Y: {curves.innerStbdP2.y:F2}, Z: {curves.innerStbdP2.z:F2}");
            if (curves.sheerCurves != null && curves.sheerCurves.enableCounter)
            {
                EditorGUILayout.LabelField("Inner Counter Top Corner:", $"X: {curves.innerStbdCounterTop.x:F2}, Y: {curves.innerStbdCounterTop.y:F2}, Z: {curves.innerStbdCounterTop.z:F2}");
            }
            EditorGUILayout.LabelField("Inner P3 Breadth Transom Corner:", $"X: {curves.innerStbdP3.x:F2}, Y: {curves.innerStbdP3.y:F2}, Z: {curves.innerStbdP3.z:F2}");
            EditorGUILayout.LabelField("Inner P4 TopLine Transom Corner:", $"X: {curves.innerStbdP4.x:F2}, Y: {curves.innerStbdP4.y:F2}, Z: {curves.innerStbdP4.z:F2}");
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update Fashion Curves Profile", GUILayout.Height(30)))
        {
            Undo.RecordObject(curves, "Update Fashion Profile");
            curves.UpdateCachedCurves();
            EditorUtility.SetDirty(curves);

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

