using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// FloorCurves implements a 3D Spatial Bezier formulation for the Floor Plan / Floor Lines.
/// Forward span is Cubic 3D Bezier (P0..P3) and Aft span is Quadratic 3D Bezier (P0..P2), matching the exact Bezier weighting of TopLine, Breadth, Bilge, and Ribband curves.
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

    [Header("Inner Line Settings")]
    public bool showInnerLine = true;
    [Tooltip("Spacing distance between the outer floor curve and the parallel inner floor curve.")]
    [Range(0f, 2f)] public float innerLineSpacing = 0.2f;
    public Color innerLineColor = new Color(1.0f, 0.65f, 0.2f, 0.95f);

    [Header("Initialization State")]
    public bool isInitialized = false;

    // --- DIRECT 3D SPATIAL CONTROL POINTS ---
    [Header("Forward Cubic 3D Control Points (P0..P3)")]
    [Tooltip("Bow Stem Intersect Point (P0). Double pointed at X = Keel Half-Width on Stem Post profile.")]
    public Vector3 fwdP0;

    [Tooltip("Bow Entry Tangent Handle (P1). Controls bow entrance shape.")]
    public Vector3 fwdP1;

    [Tooltip("Midship Forward Tangent Handle (P2). Controls forward midship sweep.")]
    public Vector3 fwdP2;

    [Tooltip("Midship Apex Point (P3). Z driven by BreadthCurves midship apex Z. X defines Max Floor Beam.")]
    public Vector3 fwdP3;

    [Header("Aft Quadratic 3D Control Points (P0..P2)")]
    [Tooltip("Midship Aft Tangent Handle (P1). Controls aft midship sweep.")]
    public Vector3 aftP1;

    [Tooltip("Stern Profile Intersect Point (P2). Double pointed at X = Keel Half-Width on Stern profile.")]
    public Vector3 aftP2;

    /// <summary>
    /// Property fallback for aftP3 to maintain transparent backwards compatibility with scripts accessing aftP3 as Floor Stern Tip.
    /// </summary>
    public Vector3 aftP3
    {
        get => aftP2;
        set => aftP2 = value;
    }

    [Header("Floor Beam & Midship Height Settings")]
    [Tooltip("Maximum half-width of the floor at midship (synced to fwdP3.x).")]
    public float maxFloorBeam = 3f;

    [Tooltip("Height ratio of the Floor Midship Apex point between the Keel Line (0.0) and the Breadth Curves Midship Apex height (1.0).")]
    [Range(0f, 1f)] public float midshipHeightRatio = 0.5f;

    [Header("Proportional Control Arm Ratios")]
    [Tooltip("Normalized relative Z position of Midship Forward Arm (P2) along [Midship Apex Z, Bow Tip Z].")]
    [Range(0.01f, 0.99f)] public float fwdP2Ratio = 0.50f;

    [Tooltip("Normalized relative Z position of Bow Entry Arm (P1) along [Midship Apex Z, Bow Tip Z].")]
    [Range(0.01f, 0.99f)] public float fwdP1Ratio = 0.75f;

    [Tooltip("Normalized relative Z position of Midship Aft Arm (P1) along [Stern Z, Midship Apex Z].")]
    [Range(0.01f, 0.99f)] public float aftP1Ratio = 0.50f;

    [Header("Proportional Control Arm X Ratios")]
    [Tooltip("Normalized relative X position of Bow Entry Arm (P1) relative to [Keel Half-Width, Max Floor Beam].")]
    [Range(0.0f, 1.0f)] public float fwdP1XRatio = 0.50f;

    // --- CACHED BEZIER CONTROL POINTS ---
    [HideInInspector] public Vector3 cachedFwdP0;
    [HideInInspector] public Vector3 cachedFwdP1;
    [HideInInspector] public Vector3 cachedFwdP2;
    [HideInInspector] public Vector3 cachedFwdP3;

    [HideInInspector] public Vector3 cachedAftP0;
    [HideInInspector] public Vector3 cachedAftP1;
    [HideInInspector] public Vector3 cachedAftP2;

    [HideInInspector] public Vector3 cachedInnerMidshipPt;
    [HideInInspector] public Vector3 cachedOffsetVector;
    private Vector3[] cachedStbdInnerPts;
    private Vector3[] cachedPortInnerPts;

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
        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);

        float fwdSpan = bowZ;

        // Double Pointed (X = hw on centerline)
        fwdP0 = new Vector3(hw, baseY + 1f, bowZ);
        fwdP3 = new Vector3(currentMaxFloor, baseY, 0f); // Midship Apex (Z=0)
        fwdP2 = new Vector3(currentMaxFloor, baseY, fwdSpan * 0.5f);
        fwdP1 = new Vector3(hw + (currentMaxFloor - hw) * 0.5f, baseY + 0.5f, fwdSpan * 0.75f);

        aftP2 = new Vector3(hw, baseY + 1f, sternZ);
        aftP1 = new Vector3(currentMaxFloor, baseY, sternZ * 0.5f);

        maxFloorBeam = currentMaxFloor;
        fwdP2Ratio = 0.50f;
        fwdP1Ratio = 0.75f;
        aftP1Ratio = 0.50f;
        fwdP1XRatio = 0.50f;
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
            fwdP3.z = breadthCurves.fwdP3.z;
        }

        if (bodyCurves != null && bodyCurves.showInnerLine)
        {
            innerLineSpacing = bodyCurves.GetInnerLineSpacingAtY(fwdP3.y);
        }

        // Calculate Midship Apex Y height between Keel Line Y and Breadth Curves Midship Apex Y
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;
        float breadthApexY = (breadthCurves != null) ? breadthCurves.fwdP3.y : keelY + 3.0f;
        fwdP3.y = Mathf.Lerp(keelY, breadthApexY, Mathf.Clamp01(midshipHeightRatio));

        // Midship Apex Y height (minY bound for Bow and Stern points)
        float minY = fwdP3.y;

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
        float targetApexZ = fwdP3.z;

        float maxAllowedBeam = 20.0f;
        if (breadthCurves != null)
        {
            float bX = Mathf.Max(breadthCurves.maxBeam, breadthCurves.fwdP3.x);
            if (bX > 0.1f) maxAllowedBeam = bX;
        }
        maxFloorBeam = Mathf.Clamp(maxFloorBeam, hw, maxAllowedBeam);

        fwdP3.x = maxFloorBeam;

        // Lock Midship Apex Control Arms straight on the Z axis (X = maxFloorBeam) for smooth C1 tangent continuity
        fwdP2.x = maxFloorBeam;
        aftP1.x = maxFloorBeam;
        fwdP2.y = fwdP3.y;
        aftP1.y = fwdP3.y;

        // Proportionally scale Bow Control Arm (fwdP2) Z
        float fwdSpan = bowZ - targetApexZ;
        if (fwdSpan > 0.001f)
        {
            fwdP2.z = targetApexZ + fwdP2Ratio * fwdSpan;
        }
        else
        {
            fwdP2.z = targetApexZ;
        }
        fwdP1 = fwdP2; // Move Bow Tip arm (fwdP1) to exact same position as Midship Apex arm (fwdP2)
        fwdP1Ratio = fwdP2Ratio;

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

        // Cache Forward Cubic Bezier Points (P0, P1, P2, P3)
        cachedFwdP0 = fwdP0;
        cachedFwdP1 = fwdP1;
        cachedFwdP2 = fwdP2;
        cachedFwdP3 = fwdP3;

        // Cache Aft Quadratic Bezier Points (P0, P1, P2) where Aft P0 is Midship Apex (fwdP3)
        cachedAftP0 = fwdP3;
        cachedAftP1 = aftP1;
        cachedAftP2 = aftP2;

        UpdateInnerParallelCurve();

        changeVersion++;
        }
        finally
        {
            isUpdatingCurves = false;
        }
    }

    private void UpdateInnerParallelCurve()
    {
        if (!showInnerLine)
        {
            return;
        }

        Vector3 pOuter = cachedFwdP3;
        Vector3 pInner = pOuter + new Vector3(-innerLineSpacing, 0f, 0f);

        if (bodyCurves != null && bodyCurves.showInnerLine)
        {
            pInner = bodyCurves.GetInnerNodePosition(1, cachedFwdP3.z, 1f);
        }

        cachedInnerMidshipPt = pInner;

        // Inward transverse offset vector at midship (angle and distance)
        float deltaX = pInner.x - pOuter.x; // Negative on starboard (inward toward centerline)
        float deltaY = pInner.y - pOuter.y; // Vertical offset
        cachedOffsetVector = new Vector3(deltaX, deltaY, 0f);

        const int segmentsFwd = 40;
        const int segmentsAft = 30;
        int totalPts = segmentsFwd + segmentsAft + 1;

        if (cachedStbdInnerPts == null || cachedStbdInnerPts.Length != totalPts)
        {
            cachedStbdInnerPts = new Vector3[totalPts];
            cachedPortInnerPts = new Vector3[totalPts];
        }

        float absDeltaX = Mathf.Abs(deltaX);

        // Forward Span (Cubic Bezier: Bow P0 -> Midship P3)
        for (int i = 0; i <= segmentsFwd; i++)
        {
            if (i == segmentsFwd)
            {
                // Exact midship apex point guaranteed to pass directly through BodyCurves inner Floor point
                cachedStbdInnerPts[i] = pInner;
                cachedPortInnerPts[i] = MirrorX(pInner);
                continue;
            }

            float t = (float)i / segmentsFwd;
            Vector3 pt = CalculateCubicBezierPoint(t, cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3);
            Vector3 deriv = CalculateCubicBezierDerivative(t, cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3);

            // Horizontal tangent is (deriv.x, deriv.z). Curve moves bow (+Z) -> midship (0), so deriv.z <= 0.
            // Inward normal towards centerline on starboard (+X) has negative X: (deriv.z, -deriv.x).
            float len = Mathf.Sqrt(deriv.z * deriv.z + deriv.x * deriv.x);
            Vector2 nXZ = (len > 0.0001f) ? new Vector2(deriv.z / len, -deriv.x / len) : new Vector2(-1f, 0f);

            Vector3 innerPt = pt + new Vector3(absDeltaX * nXZ.x, deltaY, absDeltaX * nXZ.y);
            if (innerPt.x < 0f) innerPt.x = 0f; // Clip at centerline so it never crosses port

            cachedStbdInnerPts[i] = innerPt;
            cachedPortInnerPts[i] = MirrorX(innerPt);
        }

        // Aft Span (Quadratic Bezier: Midship P0 -> Stern P2)
        // Harmonized Stern Inset & Height:
        // Solves for parameter tEnd where the continuous parallel inner curve intersects
        // the inner transom plane (Z = sternZ + absDeltaX), ensuring the inner transom line
        // has the exact uniform spacing absDeltaX without any vertical pitch kink.
        float targetZ = cachedAftP2.z + absDeltaX;
        float tEnd = 1.0f;

        Vector3 derivEndCheck = CalculateQuadraticBezierDerivative(1f, cachedAftP0, cachedAftP1, cachedAftP2);
        float lenEndCheck = Mathf.Sqrt(derivEndCheck.z * derivEndCheck.z + derivEndCheck.x * derivEndCheck.x);
        float nZEndCheck = (lenEndCheck > 0.0001f) ? (-derivEndCheck.x / lenEndCheck) : 0f;
        float zInnerAt1 = cachedAftP2.z + absDeltaX * nZEndCheck;

        if (zInnerAt1 < targetZ && (cachedAftP0.z - targetZ) > 0.001f)
        {
            float tLow = 0f;
            float tHigh = 1f;
            for (int iter = 0; iter < 16; iter++)
            {
                float tMid = (tLow + tHigh) * 0.5f;
                Vector3 ptMid = CalculateQuadraticBezierPoint(tMid, cachedAftP0, cachedAftP1, cachedAftP2);
                Vector3 derivMid = CalculateQuadraticBezierDerivative(tMid, cachedAftP0, cachedAftP1, cachedAftP2);
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
            Vector3 pt = CalculateQuadraticBezierPoint(t, cachedAftP0, cachedAftP1, cachedAftP2);
            Vector3 deriv = CalculateQuadraticBezierDerivative(t, cachedAftP0, cachedAftP1, cachedAftP2);

            float len = Mathf.Sqrt(deriv.z * deriv.z + deriv.x * deriv.x);
            Vector2 nXZ = (len > 0.0001f) ? new Vector2(deriv.z / len, -deriv.x / len) : new Vector2(-1f, 0f);

            Vector3 innerPt = pt + new Vector3(absDeltaX * nXZ.x, deltaY, absDeltaX * nXZ.y);
            if (j == segmentsAft)
            {
                innerPt.z = targetZ;
            }
            if (innerPt.x < 0f) innerPt.x = 0f;

            int idx = segmentsFwd + j;
            cachedStbdInnerPts[idx] = innerPt;
            cachedPortInnerPts[idx] = MirrorX(innerPt);
        }
    }

    // --- 3D SPATIAL EVALUATION METHODS ---

    public virtual Vector3[] GetInnerFloorPoints(bool starboard = true)
    {
        EnsureInitialized();
        if (cachedStbdInnerPts == null || cachedStbdInnerPts.Length == 0)
        {
            UpdateInnerParallelCurve();
        }
        return starboard ? cachedStbdInnerPts : cachedPortInnerPts;
    }

    public virtual Vector3 GetInnerFloorPointAtZ(float z)
    {
        EnsureInitialized();

        Vector3 outerPt = GetFloorPointAtZ(z);
        Vector3 pOuter = cachedFwdP3;
        Vector3 pInner = pOuter + new Vector3(-innerLineSpacing, 0f, 0f);

        if (bodyCurves != null && bodyCurves.showInnerLine)
        {
            pInner = bodyCurves.GetInnerNodePosition(1, cachedFwdP3.z, 1f);
        }

        float deltaX = pInner.x - pOuter.x;
        float deltaY = pInner.y - pOuter.y;
        float absDeltaX = Mathf.Abs(deltaX);

        if (Mathf.Abs(z - cachedFwdP3.z) < 0.001f)
        {
            return pInner;
        }

        if (z <= cachedAftP2.z)
        {
            if (cachedStbdInnerPts != null && cachedStbdInnerPts.Length > 0)
            {
                return cachedStbdInnerPts[cachedStbdInnerPts.Length - 1];
            }
        }

        Vector3 deriv;
        if (z >= cachedFwdP3.z)
        {
            float t = GetBezierTAtZ(cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3, z);
            deriv = CalculateCubicBezierDerivative(t, cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3);
        }
        else
        {
            float t = GetBezierTAtZQuadratic(cachedAftP0, cachedAftP1, cachedAftP2, z);
            deriv = CalculateQuadraticBezierDerivative(t, cachedAftP0, cachedAftP1, cachedAftP2);
        }

        float len = Mathf.Sqrt(deriv.z * deriv.z + deriv.x * deriv.x);
        Vector2 nXZ = (len > 0.0001f) ? new Vector2(deriv.z / len, -deriv.x / len) : new Vector2(-1f, 0f);

        Vector3 innerPt = outerPt + new Vector3(absDeltaX * nXZ.x, deltaY, absDeltaX * nXZ.y);
        if (innerPt.x < 0f) innerPt.x = 0f;
        return innerPt;
    }

    public virtual float GetInnerFloorXAtZ(float z)
    {
        return GetInnerFloorPointAtZ(z).x;
    }

    public virtual float GetInnerFloorYAtZ(float z)
    {
        return GetInnerFloorPointAtZ(z).y;
    }

    public virtual Vector3 GetInnerFloorSternTip(bool starboard = true)
    {
        EnsureInitialized();
        if (cachedStbdInnerPts != null && cachedStbdInnerPts.Length > 0)
        {
            Vector3 tip = cachedStbdInnerPts[cachedStbdInnerPts.Length - 1];
            return starboard ? tip : MirrorX(tip);
        }
        return starboard ? (cachedAftP2 + cachedOffsetVector) : MirrorX(cachedAftP2 + cachedOffsetVector);
    }

    public virtual Vector3 GetInnerFloorBowTip(bool starboard = true)
    {
        EnsureInitialized();
        if (cachedStbdInnerPts != null && cachedStbdInnerPts.Length > 0)
        {
            Vector3 tip = cachedStbdInnerPts[0];
            return starboard ? tip : MirrorX(tip);
        }
        return starboard ? (cachedFwdP0 + cachedOffsetVector) : MirrorX(cachedFwdP0 + cachedOffsetVector);
    }

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
        EnsureInitialized();

        if (z >= cachedFwdP3.z)
        {
            if (z >= cachedFwdP0.z) return cachedFwdP0;
            float t = GetBezierTAtZ(cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3, z);
            return CalculateCubicBezierPoint(t, cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3);
        }
        else
        {
            if (z <= cachedAftP2.z) return cachedAftP2;
            float t = GetBezierTAtZQuadratic(cachedAftP0, cachedAftP1, cachedAftP2, z);
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

    public Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        return (uuu * p0) + (3f * uu * t * p1) + (3f * u * tt * p2) + (ttt * p3);
    }

    public float GetBezierTAtZQuadratic(Vector3 p0, Vector3 p1, Vector3 p2, float targetZ)
    {
        float min = 0f;
        float max = 1f;
        float t = 0.5f;

        for (int i = 0; i < 12; i++)
        {
            float u = 1f - t;
            float zVal = (u * u * p0.z) + (2f * u * t * p1.z) + (t * t * p2.z);
            float error = zVal - targetZ;

            if (Mathf.Abs(error) < 0.00005f) return t;

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

    public float GetBezierTAtZScalar(float z0, float z1, float z2, float z3, float targetZ)
    {
        float dzTotal = z3 - z0;
        if (Mathf.Abs(dzTotal) < 0.0001f) return 0f;

        float min = 0f;
        float max = 1f;
        float t = Mathf.Clamp01((targetZ - z0) / dzTotal);

        for (int i = 0; i < 12; i++)
        {
            float u = 1f - t;
            float zVal = (u * u * u * z0) + (3f * u * u * t * z1) + (3f * u * t * t * z2) + (t * t * t * z3);
            float error = zVal - targetZ;

            if (Mathf.Abs(error) < 0.00005f) return t;

            if (z0 > z3)
            {
                if (zVal > targetZ) min = t;
                else max = t;
            }
            else
            {
                if (zVal < targetZ) min = t;
                else max = t;
            }

            float dzdt = 3f * u * u * (z1 - z0) + 6f * u * t * (z2 - z1) + 3f * t * t * (z3 - z2);
            if (Mathf.Abs(dzdt) > 0.0001f)
            {
                float nextT = t - error / dzdt;
                if (nextT >= min && nextT <= max)
                {
                    t = nextT;
                    continue;
                }
            }

            t = (min + max) * 0.5f;
        }

        return t;
    }

    public float GetBezierTAtZ(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float targetZ)
    {
        return GetBezierTAtZScalar(p0.z, p1.z, p2.z, p3.z, targetZ);
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

        // Draw Starboard 3D Floor Curve (Vibrant Orange/Coral) - Cubic Forward, Quadratic Aft
        DrawCubicBezierCurve(cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3, floorCurveColor);
        DrawQuadraticBezierCurve(cachedAftP0, cachedAftP1, cachedAftP2, floorCurveColor);

        // Draw Port 3D Floor Curve (Mirrored X)
        Color portColor = new Color(floorCurveColor.r, floorCurveColor.g * 0.6f, floorCurveColor.b, floorCurveColor.a);
        DrawCubicBezierCurve(MirrorX(cachedFwdP0), MirrorX(cachedFwdP1), MirrorX(cachedFwdP2), MirrorX(cachedFwdP3), portColor);
        DrawQuadraticBezierCurve(MirrorX(cachedAftP0), MirrorX(cachedAftP1), MirrorX(cachedAftP2), portColor);

        // Draw Transom Line connecting Starboard Stern Corner and Port Stern Corner
        Gizmos.color = floorCurveColor;
        Gizmos.DrawLine(cachedAftP2, MirrorX(cachedAftP2));

        // Draw 3D Control Polygon Skeleton Arms
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        Gizmos.DrawLine(cachedFwdP0, cachedFwdP1);
        Gizmos.DrawLine(cachedFwdP1, cachedFwdP2);
        Gizmos.DrawLine(cachedFwdP2, cachedFwdP3);
        Gizmos.DrawLine(cachedAftP0, cachedAftP1);
        Gizmos.DrawLine(cachedAftP1, cachedAftP2);

        // Draw Control Points
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(cachedFwdP0, 0.12f);
        Gizmos.DrawSphere(cachedFwdP1, 0.09f);
        Gizmos.DrawSphere(cachedFwdP2, 0.09f);
        Gizmos.DrawSphere(cachedFwdP3, 0.15f); // Midship Apex
        Gizmos.DrawSphere(cachedAftP1, 0.09f);
        Gizmos.DrawSphere(cachedAftP2, 0.12f); // Floor Stern Tip (aftP2)

        // Draw Inner Floor Lines (Parallel Inset from BodyCurves Floor Point)
        if (showInnerLine && cachedStbdInnerPts != null && cachedStbdInnerPts.Length > 1)
        {
            Gizmos.color = innerLineColor;
            for (int i = 0; i < cachedStbdInnerPts.Length - 1; i++)
            {
                Gizmos.DrawLine(cachedStbdInnerPts[i], cachedStbdInnerPts[i + 1]);
            }

            Color portInnerColor = new Color(innerLineColor.r, innerLineColor.g * 0.6f, innerLineColor.b, innerLineColor.a);
            Gizmos.color = portInnerColor;
            for (int i = 0; i < cachedPortInnerPts.Length - 1; i++)
            {
                Gizmos.DrawLine(cachedPortInnerPts[i], cachedPortInnerPts[i + 1]);
            }

            // Draw Inner Transom Connection Line
            int last = cachedStbdInnerPts.Length - 1;
            Gizmos.color = innerLineColor;
            Gizmos.DrawLine(cachedStbdInnerPts[last], cachedPortInnerPts[last]);

            // Inner Midship Point Sphere
            Gizmos.color = innerLineColor;
            Gizmos.DrawSphere(cachedInnerMidshipPt, 0.10f);
        }

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = GetCachedLabelStyle();
            Handles.Label(cachedFwdP0 + Vector3.up * 0.2f, "Floor Bow Tip (Stem)", labelStyle);
            Handles.Label(cachedFwdP3 + Vector3.up * 0.2f, $"Floor Midship Apex ({maxFloorBeam:F2}m)", labelStyle);
            Handles.Label(cachedAftP2 + Vector3.up * 0.2f, "Floor Stern Tip", labelStyle);

            if (showInnerLine && cachedStbdInnerPts != null && cachedStbdInnerPts.Length > 40)
            {
                Handles.Label(cachedInnerMidshipPt + Vector3.up * 0.2f, "Floor Inner Midship", labelStyle);
            }
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

        // 2. Bow Entry Control Arm Handle (Orange)
        Handles.color = new Color(1f, 0.6f, 0.1f);
        Vector3 newFwdP1 = Handles.FreeMoveHandle(curves.fwdP1, 0.12f, Vector3.zero, Handles.SphereHandleCap);

        // 3. Midship Forward Control Arm Handle (Orange)
        Handles.color = new Color(1f, 0.6f, 0.1f);
        Vector3 newFwdP2 = Handles.FreeMoveHandle(curves.fwdP2, 0.12f, Vector3.zero, Handles.SphereHandleCap);

        // 4. Midship Apex Handle (Green Sphere + Red X Arrow & Green Y Arrow)
        Handles.color = Color.green;
        Vector3 newFwdP3 = Handles.FreeMoveHandle(curves.fwdP3, 0.18f, Vector3.zero, Handles.SphereHandleCap);

        Handles.color = Color.red;
        Vector3 xArrowFwdP3 = Handles.Slider(curves.fwdP3, Vector3.right, 0.8f, Handles.ArrowHandleCap, 0.05f);
        if (Mathf.Abs(xArrowFwdP3.x - curves.fwdP3.x) > 0.001f) newFwdP3.x = xArrowFwdP3.x;

        Handles.color = Color.green;
        Vector3 yArrowFwdP3 = Handles.Slider(curves.fwdP3, Vector3.up, 0.8f, Handles.ArrowHandleCap, 0.05f);
        if (Mathf.Abs(yArrowFwdP3.y - curves.fwdP3.y) > 0.001f) newFwdP3.y = yArrowFwdP3.y;

        // 5. Midship Aft Control Arm Handle (Orange)
        Handles.color = new Color(1f, 0.6f, 0.1f);
        Vector3 newAftP1 = Handles.FreeMoveHandle(curves.aftP1, 0.12f, Vector3.zero, Handles.SphereHandleCap);

        // 6. Stern Point Handle (Yellow) - Double-pointed on Stern Profile (aftP2)
        Handles.color = Color.yellow;
        Vector3 newAftP2 = Handles.FreeMoveHandle(curves.aftP2, 0.16f, Vector3.zero, Handles.SphereHandleCap);
        newAftP2.x = hw;

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(curves, "Manipulate Floor Curves Handles");

            float keelY = (curves.sheerCurves != null) ? curves.sheerCurves.keelY : 0f;
            float breadthApexY = (curves.breadthCurves != null) ? curves.breadthCurves.fwdP3.y : keelY + 3.0f;
            float minY = newFwdP3.y;
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
            curves.maxFloorBeam = Mathf.Clamp(Mathf.Abs(newFwdP3.x), hw, maxAllowedBeam);
            float apexSpan = breadthApexY - keelY;
            if (apexSpan > 0.001f)
            {
                curves.midshipHeightRatio = Mathf.Clamp01((newFwdP3.y - keelY) / apexSpan);
            }

            // Update Control Arm Z Ratios
            float bowZ = curves.fwdP0.z;
            float sternZ = curves.aftP2.z;
            float targetApexZ = curves.fwdP3.z;

            float fwdSpan = bowZ - targetApexZ;
            if (fwdSpan > 0.001f)
            {
                if (Mathf.Abs(newFwdP1.z - curves.fwdP1.z) > 0.001f)
                    curves.fwdP2Ratio = Mathf.Clamp01((newFwdP1.z - targetApexZ) / fwdSpan);
                else
                    curves.fwdP2Ratio = Mathf.Clamp01((newFwdP2.z - targetApexZ) / fwdSpan);
                curves.fwdP1Ratio = curves.fwdP2Ratio;
            }

            float aftSpan = targetApexZ - sternZ;
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

            FashionCurves fashionCurves = FindFirstObjectByType<FashionCurves>();
            if (fashionCurves != null)
            {
                fashionCurves.UpdateCachedCurves();
                EditorUtility.SetDirty(fashionCurves);
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
