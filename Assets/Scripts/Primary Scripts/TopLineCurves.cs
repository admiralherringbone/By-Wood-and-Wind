using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// TopLineCurves implements a 3D Spatial Bezier formulation for the Top Line Plan.
/// Spans from the Upper Stem Post (fwdP0) to the Transom Rake Top (aftP2/aftP3).
/// Allows direct 3D control of each Bezier control point on X, Y (Height), and Z (Station) axes,
/// providing full spatial freedom to sculpt rising/dipping 3D top lines.
/// </summary>
[ExecuteInEditMode]
public class TopLineCurves : MonoBehaviour
{
    [Tooltip("Reference to the BodyCurves script to get physical Keel Width if available.")]
    public BodyCurves bodyCurves;

    [Tooltip("Reference to the BackboneCurves script for stem and stern keel width offset.")]
    public BackboneCurves backboneCurves;

    [Tooltip("Reference to the SheerCurves script for 2D profile curves (Upper Stem Post, Transom Rake Top).")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to the BreadthCurves script for Max Beam upper ceiling constraint.")]
    public BreadthCurves breadthCurves;

    [Header("Interactive Scene Handles")]
    [Tooltip("Enable interactive 3D position handles in Scene view to sculpt control points along X, Y, and Z axes.")]
    public bool enableInteractiveHandles = true;

    [Header("Gizmo Settings")]
    public bool showLabels = true;
    public Color topLineCurveColor = new Color(0.85f, 0.2f, 0.95f, 0.95f); // Vibrant Magenta / Purple

    [Header("Inner Line Settings")]
    public bool showInnerLine = true;
    [Tooltip("Spacing distance between the outer top line curve and the parallel inner top line curve.")]
    [Range(0f, 2f)] public float innerLineSpacing = 0.2f;
    public Color innerLineColor = new Color(0.95f, 0.4f, 0.95f, 0.95f);

    [Header("Initialization State")]
    public bool isInitialized = false;

    // --- DIRECT 3D SPATIAL CONTROL POINTS ---
    [Header("Forward 3D Control Points (P0..P3)")]
    [Tooltip("Upper Stem Post Tip Point (P0). Tied to SheerCurves.upperStemTop.")]
    public Vector3 fwdP0;

    [Tooltip("Bow Entry Tangent Handle (P1). Controls bow entrance shape and Y height.")]
    public Vector3 fwdP1;

    [Tooltip("Midship Forward Tangent Handle (P2). Controls midship forward sweep and Y height.")]
    public Vector3 fwdP2;

    [Tooltip("Midship Apex Point (P3). Z driven by midshipApexZPercentage. X defines Max Beam.")]
    public Vector3 fwdP3;

    [Header("Aft 3D Control Points (P1..P3)")]
    [Tooltip("Midship Aft Tangent Handle (P1). Controls midship aft sweep and Y height.")]
    public Vector3 aftP1;

    [Tooltip("Transom Rake Top Corner (P2). Tied to SheerCurves.transomP1.")]
    public Vector3 aftP2;

    [Tooltip("Stern Tip Point (P3). Only active when Transom Width is 0.")]
    public Vector3 aftP3;

    [Header("Transom Settings")]
    [Tooltip("Transom width ratio (0 = pointed stern, > 0 = flat transom).")]
    [Range(0f, 1f)] public float transomWidth = 0.5f;

    [Tooltip("Maximum half-width of the ship at midship (synced to fwdP3.x).")]
    public float maxBeam = 4f;

    [Header("Midship Apex Z Position")]
    [Tooltip("The Z position of the Midship Apex as a percentage (0 to 1) of the total length between Transom Corner and Bow Tip.")]
    [Range(0.05f, 0.95f)] public float midshipApexZPercentage = 0.5f;

    [Header("Proportional Control Arm Ratios")]
    [Tooltip("Normalized relative Z position of Midship Forward Arm (P2) along [Midship Apex Z, Bow Tip Z].")]
    [Range(0.01f, 0.99f)] public float fwdP2Ratio = 0.50f;

    [Tooltip("Normalized relative Z position of Bow Entry Arm (P1) along [Midship Apex Z, Bow Tip Z].")]
    [Range(0.01f, 0.99f)] public float fwdP1Ratio = 0.75f;

    [Tooltip("Normalized relative Z position of Midship Aft Arm (P1) along [Stern Z, Midship Apex Z].")]
    [Range(0.01f, 0.99f)] public float aftP1Ratio = 0.50f;

    [Header("Proportional Control Arm X Ratios")]
    [Tooltip("Normalized relative X position of Midship Forward Arm (P2) relative to [Keel Half-Width, Max Beam].")]
    [Range(0.0f, 1.0f)] public float fwdP2XRatio = 0.90f;

    [Tooltip("Normalized relative X position of Bow Entry Arm (P1) relative to [Keel Half-Width, Max Beam].")]
    [Range(0.0f, 1.0f)] public float fwdP1XRatio = 0.50f;

    [Tooltip("Normalized relative X position of Midship Aft Arm (P1) relative to [Keel Half-Width, Max Beam].")]
    [Range(0.0f, 1.0f)] public float aftP1XRatio = 0.90f;

    [Header("Proportional Control Arm Y Ratios")]
    [Tooltip("Normalized relative Y position of Bow Entry Arm (P1) between Midship Apex Y and Bow Chord Y Ceiling.")]
    [Range(0.0f, 1.0f)] public float fwdP1YRatio = 0.50f;

    // --- CACHED BEZIER CONTROL POINTS ---
    [HideInInspector] public Vector3 cachedFwdP0;
    [HideInInspector] public Vector3 cachedFwdP1;
    [HideInInspector] public Vector3 cachedFwdP2;
    [HideInInspector] public Vector3 cachedFwdP3;

    [HideInInspector] public Vector3 cachedAftP0;
    [HideInInspector] public Vector3 cachedAftP1;
    [HideInInspector] public Vector3 cachedAftP2;
    [HideInInspector] public Vector3 cachedAftP3;
    [HideInInspector] public bool cachedIsAftCubic;
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

    public void FindReferences()
    {
        if (sheerCurves == null) sheerCurves = GetComponentInParent<SheerCurves>();
        if (sheerCurves == null) sheerCurves = FindFirstObjectByType<SheerCurves>();
        if (breadthCurves == null) breadthCurves = GetComponentInParent<BreadthCurves>();
        if (breadthCurves == null) breadthCurves = FindFirstObjectByType<BreadthCurves>();
        if (bodyCurves == null) bodyCurves = GetComponentInParent<BodyCurves>();
        if (bodyCurves == null) bodyCurves = FindFirstObjectByType<BodyCurves>();
        if (backboneCurves == null) backboneCurves = GetComponentInParent<BackboneCurves>();
        if (backboneCurves == null) backboneCurves = FindFirstObjectByType<BackboneCurves>();
    }

    [ContextMenu("Reset Control Points to Default Hull")]
    public void InitializeControlPointsFromShip()
    {
        FindReferences();

        float bowZ = (sheerCurves != null) ? sheerCurves.upperStemTop.z : 15f;
        float sternZ = (sheerCurves != null) ? sheerCurves.transomP1.z : -16.2f;
        float bowY = (sheerCurves != null) ? sheerCurves.upperStemTop.y : 8.25f;
        float sternY = (sheerCurves != null) ? sheerCurves.transomP1.y : 9.5f;

        float currentMaxBeam = maxBeam > 0.5f ? maxBeam : 4f;
        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);

        float halfLength = (bowZ - sternZ) / 2f;
        float defaultApexY = Mathf.Min(bowY, sternY);

        // Forward Control Points (P0..P3)
        fwdP0 = new Vector3(hw, bowY, bowZ);
        fwdP3 = new Vector3(currentMaxBeam, defaultApexY, 0f); // Midship Apex (Z=0)
        fwdP2 = fwdP3 + new Vector3(0, 0, halfLength * 0.5f);
        fwdP1 = fwdP0 + new Vector3(currentMaxBeam * 0.5f, 0, -(bowZ - fwdP2.z) * 0.5f);

        // Aft Control Points (P0..P3)
        aftP1 = fwdP3 + new Vector3(0, 0, -halfLength * 0.5f);
        aftP2 = new Vector3(hw + (currentMaxBeam - hw) * transomWidth, sternY, sternZ);
        aftP3 = new Vector3(hw, sternY, sternZ);

        maxBeam = currentMaxBeam;
        fwdP2XRatio = 0.90f;
        fwdP1XRatio = 0.50f;
        aftP1XRatio = 0.90f;
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
            FindReferences();

            cachedIsAftCubic = transomWidth <= 0.001f;

        // TIE TO SHEER CURVES PROFILE LINES (Upper Stem Post Top, Transom Rake Top)
        if (sheerCurves != null)
        {
            sheerCurves.UpdateCachedProfile();

            // 1. Bow Tip (fwdP0) is locked 100% to Upper Stem Post Top (upperStemTop)
            fwdP0.z = sheerCurves.upperStemTop.z;
            fwdP0.y = sheerCurves.upperStemTop.y;

            // 2. Transom Corners (aftP2 / aftP3) are locked to Transom Rake Top (transomP1)
            aftP2.z = sheerCurves.transomP1.z;
            aftP2.y = sheerCurves.transomP1.y;
            if (cachedIsAftCubic)
            {
                aftP3.z = sheerCurves.transomP1.z;
                aftP3.y = sheerCurves.transomP1.y;
            }
        }

        float bowZ = fwdP0.z;
        float sternZ = cachedIsAftCubic ? aftP3.z : aftP2.z;

        float targetApexZ;
        if (breadthCurves != null)
        {
            targetApexZ = Mathf.Clamp(breadthCurves.fwdP3.z, sternZ + 0.1f, bowZ - 0.1f);
            float dzTotal = bowZ - sternZ;
            if (dzTotal > 0.001f)
            {
                midshipApexZPercentage = Mathf.Clamp((targetApexZ - sternZ) / dzTotal, 0.05f, 0.95f);
            }
        }
        else
        {
            targetApexZ = Mathf.Lerp(sternZ, bowZ, Mathf.Clamp(midshipApexZPercentage, 0.05f, 0.95f));
        }

        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);

        // Enforce Max Beam & Transom Width Restrictions: TopLine Max Beam and Transom Width cannot exceed BreadthCurves
        float maxAllowedBeam = 20.0f;
        if (breadthCurves != null)
        {
            breadthCurves.UpdateCachedCurves();
            float bX = Mathf.Max(breadthCurves.maxBeam, breadthCurves.fwdP3.x);
            if (bX > 0.1f) maxAllowedBeam = bX;

            transomWidth = Mathf.Clamp(transomWidth, 0f, breadthCurves.transomWidth);
        }
        maxBeam = Mathf.Clamp(maxBeam, hw, maxAllowedBeam);

        fwdP3.z = targetApexZ;
        fwdP3.x = maxBeam;

        // Enforce Y-Axis Restriction: Midship Apex Y height (fwdP3.y) cannot be higher than Bow Tip Y (fwdP0.y) or Stern Tip Y (aftP2.y)
        float maxAllowedApexY = Mathf.Min(fwdP0.y, aftP2.y);
        fwdP3.y = Mathf.Min(fwdP3.y, maxAllowedApexY);

        // Proportionally scale control arm Z handles based on normalized span ratios
        float fwdSpan = bowZ - targetApexZ;
        if (fwdSpan > 0.001f)
        {
            fwdP2.z = targetApexZ + fwdP2Ratio * fwdSpan;
            fwdP1.z = targetApexZ + fwdP1Ratio * fwdSpan;
        }

        float aftSpan = targetApexZ - sternZ;
        if (aftSpan > 0.001f)
        {
            aftP1.z = sternZ + aftP1Ratio * aftSpan;
        }

        // Proportionally scale Bow Entry Arm X handle based on normalized maxBeam span
        float beamSpan = maxBeam - hw;
        if (beamSpan > 0.001f)
        {
            fwdP1.x = hw + fwdP1XRatio * beamSpan;
        }
        else
        {
            fwdP1.x = hw;
        }

        // Lock Midship Apex Control Arms straight on the Z axis (X = maxBeam) for C1 tangent continuity
        fwdP2.x = maxBeam;
        aftP1.x = maxBeam;

        if (breadthCurves != null)
        {
            aftP2.x = breadthCurves.GetAftProjectionPointAtZ(aftP2.z).x;
        }
        else
        {
            aftP2.x = hw + (maxBeam - hw) * transomWidth;
        }
        fwdP0.x = hw;
        if (cachedIsAftCubic) aftP3.x = hw;

        // Lock Midship Apex Control Arms Y-height (both forward fwdP2 and aft aftP1) to fwdP3.y for flat C1 Y-tangent continuity
        fwdP2.y = fwdP3.y;
        aftP1.y = fwdP3.y;

        float minY = fwdP3.y;

        // Enforce Upward Y-Ceiling & Proportional Y position for Bow Control Arm (fwdP1) using chord line (fwdP0 -> fwdP2)
        float fwdDz = fwdP0.z - fwdP2.z;
        float fwdT = (fwdDz > 0.001f) ? Mathf.Clamp01((fwdP1.z - fwdP2.z) / fwdDz) : 0f;
        float maxBowY = Mathf.Lerp(fwdP2.y, fwdP0.y, fwdT);
        fwdP1.y = Mathf.Lerp(minY, maxBowY, fwdP1YRatio);

        // Enforce Upward Y-Ceiling for Aft Control Arm (aftP1) using chord line (aftP2 -> fwdP3)
        float aftDz = fwdP3.z - aftP2.z;
        float aftT = (aftDz > 0.001f) ? Mathf.Clamp01((aftP1.z - aftP2.z) / aftDz) : 0f;
        float maxAftY = Mathf.Lerp(aftP2.y, fwdP3.y, aftT);

        cachedFwdP0 = fwdP0;
        cachedFwdP1 = fwdP1;
        cachedFwdP2 = fwdP2;
        cachedFwdP3 = fwdP3;

        cachedAftP0 = fwdP3; // Aft start is midship apex (fwdP3)
        cachedAftP1 = aftP1;
        cachedAftP2 = aftP2;
        cachedAftP3 = aftP3;

        if (bodyCurves != null)
        {
            innerLineSpacing = bodyCurves.GetInnerLineSpacingAtY(fwdP3.y);
        }

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
            pInner = bodyCurves.GetInnerNodePosition(4, cachedFwdP3.z, 1f);
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
                // Exact midship apex point guaranteed to pass directly through BodyCurves inner TopLine point
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

        // Aft Span:
        // Harmonized Stern Inset & Height:
        // Solves for parameter tEnd where the continuous parallel inner curve intersects
        // the inner transom plane (Z = sternZ + absDeltaX), ensuring the inner transom line
        // has the exact uniform spacing absDeltaX without any vertical pitch kink.
        if (cachedIsAftCubic)
        {
            for (int j = 1; j <= segmentsAft; j++)
            {
                float t = (float)j / segmentsAft;
                Vector3 pt = CalculateCubicBezierPoint(t, cachedAftP0, cachedAftP1, cachedAftP2, cachedAftP3);
                Vector3 deriv = CalculateCubicBezierDerivative(t, cachedAftP0, cachedAftP1, cachedAftP2, cachedAftP3);

                float len = Mathf.Sqrt(deriv.z * deriv.z + deriv.x * deriv.x);
                Vector2 nXZ = (len > 0.0001f) ? new Vector2(deriv.z / len, -deriv.x / len) : new Vector2(-1f, 0f);

                Vector3 innerPt = pt + new Vector3(absDeltaX * nXZ.x, deltaY, absDeltaX * nXZ.y);
                if (innerPt.x < 0f) innerPt.x = 0f;

                int idx = segmentsFwd + j;
                cachedStbdInnerPts[idx] = innerPt;
                cachedPortInnerPts[idx] = MirrorX(innerPt);
            }
        }
        else
        {
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
    }

    // --- 3D INNER PARALLEL EVALUATION METHODS ---

    public virtual Vector3[] GetInnerTopLinePoints(bool starboard = true)
    {
        EnsureInitialized();
        if (cachedStbdInnerPts == null || cachedStbdInnerPts.Length == 0)
        {
            UpdateInnerParallelCurve();
        }
        return starboard ? cachedStbdInnerPts : cachedPortInnerPts;
    }

    public virtual Vector3 GetInnerTopLinePointAtZ(float z, bool starboard = true)
    {
        EnsureInitialized();

        Vector3 outerPt = GetTopLinePointAtZ(z);
        Vector3 pOuter = cachedFwdP3;
        Vector3 pInner = pOuter + new Vector3(-innerLineSpacing, 0f, 0f);

        if (bodyCurves != null && bodyCurves.showInnerLine)
        {
            pInner = bodyCurves.GetInnerNodePosition(4, cachedFwdP3.z, 1f);
        }

        float deltaX = pInner.x - pOuter.x;
        float deltaY = pInner.y - pOuter.y;
        float absDeltaX = Mathf.Abs(deltaX);

        if (Mathf.Abs(z - cachedFwdP3.z) < 0.001f)
        {
            return starboard ? pInner : MirrorX(pInner);
        }

        float sternTipZ = cachedIsAftCubic ? cachedAftP3.z : cachedAftP2.z;
        if (z <= sternTipZ)
        {
            if (cachedStbdInnerPts != null && cachedStbdInnerPts.Length > 0)
            {
                Vector3 tip = cachedStbdInnerPts[cachedStbdInnerPts.Length - 1];
                return starboard ? tip : MirrorX(tip);
            }
        }

        Vector3 deriv;
        if (z >= cachedFwdP3.z)
        {
            float t = GetBezierTAtZ(cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3, z, false);
            deriv = CalculateCubicBezierDerivative(t, cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3);
        }
        else
        {
            if (cachedIsAftCubic)
            {
                float t = GetBezierTAtZ(cachedAftP0, cachedAftP1, cachedAftP2, cachedAftP3, z, false);
                deriv = CalculateCubicBezierDerivative(t, cachedAftP0, cachedAftP1, cachedAftP2, cachedAftP3);
            }
            else
            {
                float t = GetBezierTAtZ(cachedAftP0, cachedAftP1, cachedAftP2, Vector3.zero, z, false);
                deriv = CalculateQuadraticBezierDerivative(t, cachedAftP0, cachedAftP1, cachedAftP2);
            }
        }

        float len = Mathf.Sqrt(deriv.z * deriv.z + deriv.x * deriv.x);
        Vector2 nXZ = (len > 0.0001f) ? new Vector2(deriv.z / len, -deriv.x / len) : new Vector2(-1f, 0f);

        Vector3 innerPt = outerPt + new Vector3(absDeltaX * nXZ.x, deltaY, absDeltaX * nXZ.y);
        if (innerPt.x < 0f) innerPt.x = 0f;
        return starboard ? innerPt : MirrorX(innerPt);
    }

    public virtual float GetInnerTopLineXAtZ(float z, bool starboard = true)
    {
        return GetInnerTopLinePointAtZ(z, starboard).x;
    }

    public virtual float GetInnerTopLineYAtZ(float z)
    {
        return GetInnerTopLinePointAtZ(z, true).y;
    }

    public virtual Vector3 GetInnerTopLineSternTip(bool starboard = true)
    {
        EnsureInitialized();
        if (cachedStbdInnerPts != null && cachedStbdInnerPts.Length > 0)
        {
            Vector3 tip = cachedStbdInnerPts[cachedStbdInnerPts.Length - 1];
            return starboard ? tip : MirrorX(tip);
        }
        return starboard ? (cachedAftP2 + cachedOffsetVector) : MirrorX(cachedAftP2 + cachedOffsetVector);
    }

    public virtual Vector3 GetInnerTopLineBowTip(bool starboard = true)
    {
        EnsureInitialized();
        if (cachedStbdInnerPts != null && cachedStbdInnerPts.Length > 0)
        {
            Vector3 tip = cachedStbdInnerPts[0];
            return starboard ? tip : MirrorX(tip);
        }
        return starboard ? (cachedFwdP0 + cachedOffsetVector) : MirrorX(cachedFwdP0 + cachedOffsetVector);
    }

    // --- 3D SPATIAL EVALUATION METHODS ---

    public virtual float GetTopLineXAtZ(float z)
    {
        return GetTopLinePointAtZ(z).x;
    }

    public virtual float GetTopLineYAtZ(float z)
    {
        return GetTopLinePointAtZ(z).y;
    }

    public virtual Vector3 GetTopLinePointAtZ(float z)
    {
        if (z >= cachedFwdP3.z)
        {
            if (z >= cachedFwdP0.z) return cachedFwdP0;
            float t = GetBezierTAtZ(cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3, z, false);
            return CalculateCubicBezierPoint(t, cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3);
        }
        else
        {
            if (cachedIsAftCubic)
            {
                if (z <= cachedAftP3.z) return cachedAftP3;
                float t = GetBezierTAtZ(cachedAftP0, cachedAftP1, cachedAftP2, cachedAftP3, z, false);
                return CalculateCubicBezierPoint(t, cachedAftP0, cachedAftP1, cachedAftP2, cachedAftP3);
            }
            else
            {
                if (z <= cachedAftP2.z) return cachedAftP2;
                float t = GetBezierTAtZ(cachedAftP0, cachedAftP1, cachedAftP2, Vector3.zero, z, false);
                return CalculateQuadraticBezierPoint(t, cachedAftP0, cachedAftP1, cachedAftP2);
            }
        }
    }

    public virtual float GetTheoreticalTopLineXAtZ(float z)
    {
        if (z >= 0) return GetTopLineXAtZ(z);

        if (cachedIsAftCubic)
        {
            if (z > cachedAftP3.z) return GetTopLineXAtZ(z);

            float dz = cachedAftP3.z - cachedAftP2.z;
            float dx = cachedAftP3.x - cachedAftP2.x;
            if (Mathf.Abs(dz) < 0.001f) return cachedAftP3.x;

            return cachedAftP3.x + (dx / dz) * (z - cachedAftP3.z);
        }
        else
        {
            if (z > cachedAftP2.z) return GetTopLineXAtZ(z);

            float dz = cachedAftP2.z - cachedAftP1.z;
            float dx = cachedAftP2.x - cachedAftP1.x;
            if (Mathf.Abs(dz) < 0.001f) return cachedAftP2.x;

            return cachedAftP2.x + (dx / dz) * (z - cachedAftP2.z);
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

    public float GetBezierTAtZ(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float targetZ, bool isForward)
    {
        if (p3 == Vector3.zero)
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
            cachedLabelStyle.normal.textColor = Color.magenta;
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

        // Draw Starboard 3D Top Line Curve (Vibrant Magenta)
        DrawCubicBezierCurve(cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3, topLineCurveColor);
        if (cachedIsAftCubic) DrawCubicBezierCurve(cachedAftP0, cachedAftP1, cachedAftP2, cachedAftP3, topLineCurveColor);
        else DrawQuadraticBezierCurve(cachedAftP0, cachedAftP1, cachedAftP2, topLineCurveColor);

        // Draw Port 3D Top Line Curve (Mirrored X)
        Color portColor = new Color(topLineCurveColor.r, topLineCurveColor.g * 0.6f, topLineCurveColor.b, topLineCurveColor.a);
        DrawCubicBezierCurve(MirrorX(cachedFwdP0), MirrorX(cachedFwdP1), MirrorX(cachedFwdP2), MirrorX(cachedFwdP3), portColor);
        if (cachedIsAftCubic) DrawCubicBezierCurve(MirrorX(cachedAftP0), MirrorX(cachedAftP1), MirrorX(cachedAftP2), MirrorX(cachedAftP3), portColor);
        else DrawQuadraticBezierCurve(MirrorX(cachedAftP0), MirrorX(cachedAftP1), MirrorX(cachedAftP2), portColor);

        // Draw Transom Line connecting Starboard Transom Corner and Port Transom Corner
        if (!cachedIsAftCubic)
        {
            Gizmos.color = topLineCurveColor;
            Gizmos.DrawLine(cachedAftP2, MirrorX(cachedAftP2));
        }

        // Draw Inner Top Line Curves (Parallel Inset from BodyCurves TopLine Point)
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
            if (!cachedIsAftCubic)
            {
                int last = cachedStbdInnerPts.Length - 1;
                Gizmos.color = innerLineColor;
                Gizmos.DrawLine(cachedStbdInnerPts[last], cachedPortInnerPts[last]);
            }

            // Inner Midship Point Sphere
            Gizmos.color = innerLineColor;
            Gizmos.DrawSphere(cachedInnerMidshipPt, 0.10f);
        }

        // Draw 3D Control Polygon Skeleton Arms
        Gizmos.color = new Color(0.9f, 0.2f, 0.9f, 0.6f);
        Gizmos.DrawLine(cachedFwdP0, cachedFwdP1);
        Gizmos.DrawLine(cachedFwdP3, cachedFwdP2);
        Gizmos.DrawLine(cachedAftP0, cachedAftP1);
        if (cachedIsAftCubic) Gizmos.DrawLine(cachedAftP3, cachedAftP2);

        Gizmos.DrawLine(MirrorX(cachedFwdP0), MirrorX(cachedFwdP1));
        Gizmos.DrawLine(MirrorX(cachedFwdP3), MirrorX(cachedFwdP2));
        Gizmos.DrawLine(MirrorX(cachedAftP0), MirrorX(cachedAftP1));
        if (cachedIsAftCubic) Gizmos.DrawLine(MirrorX(cachedAftP3), MirrorX(cachedAftP2));

        // Draw Control Point Handles Spheres
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(cachedFwdP0, 0.14f);
        Gizmos.DrawSphere(cachedFwdP1, 0.12f);
        Gizmos.DrawSphere(cachedFwdP2, 0.12f);
        Gizmos.DrawSphere(cachedFwdP3, 0.16f); // Midship Apex
        Gizmos.DrawSphere(cachedAftP1, 0.12f);
        Gizmos.DrawSphere(cachedAftP2, 0.14f);
        if (cachedIsAftCubic) Gizmos.DrawSphere(cachedAftP3, 0.14f);

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle style = GetCachedLabelStyle();

            Handles.Label(cachedFwdP3 + new Vector3(0, 0.2f, 0), $"Top Line Midship Apex (X={maxBeam:F2}, Y={cachedFwdP3.y:F2})", style);
            Handles.Label(cachedFwdP0 + new Vector3(0, 0.2f, 0), $"Upper Stem Tip (Y={cachedFwdP0.y:F2})", style);
            Handles.Label(cachedAftP2 + new Vector3(0, 0.2f, 0), cachedIsAftCubic ? $"Transom Rake Top Tip (Y={cachedAftP3.y:F2})" : $"Transom Rake Top Corner (Y={cachedAftP2.y:F2})", style);

            if (showInnerLine && cachedStbdInnerPts != null && cachedStbdInnerPts.Length > 40)
            {
                Handles.Label(cachedInnerMidshipPt + new Vector3(0, 0.25f, 0), $"Inner Midship ({cachedInnerMidshipPt.x:F2}m)", style);
            }

            // Render Inner Boundary Chord Lines (Cyan Dotted)
            Handles.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            Handles.DrawDottedLine(cachedFwdP0, cachedFwdP2, 3f);
            Handles.DrawDottedLine(cachedFwdP3, cachedAftP2, 3f);

            Handles.DrawDottedLine(MirrorX(cachedFwdP0), MirrorX(cachedFwdP2), 3f);
            Handles.DrawDottedLine(MirrorX(cachedFwdP3), MirrorX(cachedAftP2), 3f);
        }
#endif
    }

    private void DrawCubicBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color, int segments = 40)
    {
        Gizmos.color = color;
        Vector3 prevPt = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 currPt = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            Gizmos.DrawLine(prevPt, currPt);
            prevPt = currPt;
        }
    }

    private void DrawQuadraticBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Color color, int segments = 40)
    {
        Gizmos.color = color;
        Vector3 prevPt = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 currPt = CalculateQuadraticBezierPoint(t, p0, p1, p2);
            Gizmos.DrawLine(prevPt, currPt);
            prevPt = currPt;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TopLineCurves))]
public class TopLineCurvesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TopLineCurves plan = (TopLineCurves)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Reset Control Points to Default Hull", GUILayout.Height(30)))
        {
            Undo.RecordObject(plan, "Reset 3D Control Points");
            plan.InitializeControlPointsFromShip();
            plan.UpdateCachedCurves();
            EditorUtility.SetDirty(plan);
        }
    }

    private void OnSceneGUI()
    {
        TopLineCurves plan = (TopLineCurves)target;
        if (plan == null || !plan.enableInteractiveHandles) return;

        plan.UpdateCachedCurves();

        float hw = (plan.backboneCurves != null) ? (plan.backboneCurves.keelWidth / 2f) : ((plan.bodyCurves != null) ? (plan.bodyCurves.keelWidth / 2f) : 0.25f);

        // Interactive 3D Handles for Control Points
        EditorGUI.BeginChangeCheck();

        // 1. Upper Stem Post Tip (fwdP0)
        Handles.color = Color.cyan;
        Vector3 newFwdP0 = Handles.FreeMoveHandle(plan.fwdP0, 0.16f, Vector3.zero, Handles.SphereHandleCap);
        Handles.color = Color.red; Vector3 xArrowFwdP0 = Handles.Slider(plan.fwdP0, Vector3.right, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(xArrowFwdP0.x - plan.fwdP0.x) > 0.001f) newFwdP0.x = xArrowFwdP0.x;
        Handles.color = Color.green; Vector3 yArrowFwdP0 = Handles.Slider(plan.fwdP0, Vector3.up, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(yArrowFwdP0.y - plan.fwdP0.y) > 0.001f) newFwdP0.y = yArrowFwdP0.y;
        Handles.color = Color.blue; Vector3 zArrowFwdP0 = Handles.Slider(plan.fwdP0, Vector3.forward, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(zArrowFwdP0.z - plan.fwdP0.z) > 0.001f) newFwdP0.z = zArrowFwdP0.z;

        // 2. Bow Handle 1 (fwdP1)
        Handles.color = Color.yellow;
        Vector3 newFwdP1 = Handles.FreeMoveHandle(plan.fwdP1, 0.14f, Vector3.zero, Handles.SphereHandleCap);
        Handles.color = Color.red; Vector3 xArrowFwdP1 = Handles.Slider(plan.fwdP1, Vector3.right, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(xArrowFwdP1.x - plan.fwdP1.x) > 0.001f) newFwdP1.x = xArrowFwdP1.x;
        Handles.color = Color.green; Vector3 yArrowFwdP1 = Handles.Slider(plan.fwdP1, Vector3.up, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(yArrowFwdP1.y - plan.fwdP1.y) > 0.001f) newFwdP1.y = yArrowFwdP1.y;
        Handles.color = Color.blue; Vector3 zArrowFwdP1 = Handles.Slider(plan.fwdP1, Vector3.forward, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(zArrowFwdP1.z - plan.fwdP1.z) > 0.001f) newFwdP1.z = zArrowFwdP1.z;

        // 3. Midship Forward Handle 2 (fwdP2)
        Handles.color = Color.yellow;
        Vector3 newFwdP2 = Handles.FreeMoveHandle(plan.fwdP2, 0.14f, Vector3.zero, Handles.SphereHandleCap);
        Handles.color = Color.red; Vector3 xArrowFwdP2 = Handles.Slider(plan.fwdP2, Vector3.right, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(xArrowFwdP2.x - plan.fwdP2.x) > 0.001f) newFwdP2.x = xArrowFwdP2.x;
        Handles.color = Color.green; Vector3 yArrowFwdP2 = Handles.Slider(plan.fwdP2, Vector3.up, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(yArrowFwdP2.y - plan.fwdP2.y) > 0.001f) newFwdP2.y = yArrowFwdP2.y;
        Handles.color = Color.blue; Vector3 zArrowFwdP2 = Handles.Slider(plan.fwdP2, Vector3.forward, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(zArrowFwdP2.z - plan.fwdP2.z) > 0.001f) newFwdP2.z = zArrowFwdP2.z;

        // 4. Midship Apex (fwdP3)
        Handles.color = Color.magenta;
        Vector3 newFwdP3 = Handles.FreeMoveHandle(plan.fwdP3, 0.18f, Vector3.zero, Handles.SphereHandleCap);
        Handles.color = Color.red; Vector3 xArrowFwdP3 = Handles.Slider(plan.fwdP3, Vector3.right, 0.8f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(xArrowFwdP3.x - plan.fwdP3.x) > 0.001f) newFwdP3.x = xArrowFwdP3.x;
        Handles.color = Color.green; Vector3 yArrowFwdP3 = Handles.Slider(plan.fwdP3, Vector3.up, 0.8f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(yArrowFwdP3.y - plan.fwdP3.y) > 0.001f) newFwdP3.y = yArrowFwdP3.y;
        Handles.color = Color.blue; Vector3 zArrowFwdP3 = Handles.Slider(plan.fwdP3, Vector3.forward, 0.8f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(zArrowFwdP3.z - plan.fwdP3.z) > 0.001f) newFwdP3.z = zArrowFwdP3.z;

        // 5. Midship Aft Handle 1 (aftP1)
        Handles.color = Color.yellow;
        Vector3 newAftP1 = Handles.FreeMoveHandle(plan.aftP1, 0.14f, Vector3.zero, Handles.SphereHandleCap);
        Handles.color = Color.red; Vector3 xArrowAftP1 = Handles.Slider(plan.aftP1, Vector3.right, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(xArrowAftP1.x - plan.aftP1.x) > 0.001f) newAftP1.x = xArrowAftP1.x;
        Handles.color = Color.green; Vector3 yArrowAftP1 = Handles.Slider(plan.aftP1, Vector3.up, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(yArrowAftP1.y - plan.aftP1.y) > 0.001f) newAftP1.y = yArrowAftP1.y;
        Handles.color = Color.blue; Vector3 zArrowAftP1 = Handles.Slider(plan.aftP1, Vector3.forward, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(zArrowAftP1.z - plan.aftP1.z) > 0.001f) newAftP1.z = zArrowAftP1.z;

        // 6. Transom Rake Top Corner (aftP2)
        Handles.color = Color.cyan;
        Vector3 newAftP2 = Handles.FreeMoveHandle(plan.aftP2, 0.16f, Vector3.zero, Handles.SphereHandleCap);
        Handles.color = Color.red; Vector3 xArrowAftP2 = Handles.Slider(plan.aftP2, Vector3.right, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(xArrowAftP2.x - plan.aftP2.x) > 0.001f) newAftP2.x = xArrowAftP2.x;
        Handles.color = Color.green; Vector3 yArrowAftP2 = Handles.Slider(plan.aftP2, Vector3.up, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(yArrowAftP2.y - plan.aftP2.y) > 0.001f) newAftP2.y = yArrowAftP2.y;
        Handles.color = Color.blue; Vector3 zArrowAftP2 = Handles.Slider(plan.aftP2, Vector3.forward, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(zArrowAftP2.z - plan.aftP2.z) > 0.001f) newAftP2.z = zArrowAftP2.z;

        // 7. Stern Tip (aftP3)
        Vector3 newAftP3 = plan.aftP3;
        if (plan.transomWidth <= 0.001f)
        {
            Handles.color = Color.cyan;
            newAftP3 = Handles.FreeMoveHandle(plan.aftP3, 0.16f, Vector3.zero, Handles.SphereHandleCap);
            Handles.color = Color.red; Vector3 xArrowAftP3 = Handles.Slider(plan.aftP3, Vector3.right, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(xArrowAftP3.x - plan.aftP3.x) > 0.001f) newAftP3.x = xArrowAftP3.x;
            Handles.color = Color.green; Vector3 yArrowAftP3 = Handles.Slider(plan.aftP3, Vector3.up, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(yArrowAftP3.y - plan.aftP3.y) > 0.001f) newAftP3.y = yArrowAftP3.y;
            Handles.color = Color.blue; Vector3 zArrowAftP3 = Handles.Slider(plan.aftP3, Vector3.forward, 0.7f, Handles.ArrowHandleCap, 0.05f); if (Mathf.Abs(zArrowAftP3.z - plan.aftP3.z) > 0.001f) newAftP3.z = zArrowAftP3.z;
        }

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(plan, "Manipulate 3D Control Point");

            float bowZ = newFwdP0.z;
            float sternZ = plan.cachedIsAftCubic ? newAftP3.z : newAftP2.z;
            float maxAllowedBeam = 20.0f;
            if (plan.breadthCurves != null)
            {
                float bX = Mathf.Max(plan.breadthCurves.maxBeam, plan.breadthCurves.fwdP3.x);
                if (bX > 0.1f) maxAllowedBeam = bX;
            }
            float maxBeamWidth = Mathf.Clamp(Mathf.Abs(newFwdP3.x), hw, maxAllowedBeam);
            plan.maxBeam = maxBeamWidth;

            newFwdP0.z = Mathf.Max(newFwdP0.z, 0.1f);
            newFwdP0.x = Mathf.Clamp(Mathf.Abs(newFwdP0.x), hw, maxBeamWidth);

            if (plan.cachedIsAftCubic) newAftP3.z = Mathf.Min(newAftP3.z, -0.1f);
            newAftP2.z = Mathf.Min(newAftP2.z, -0.1f);

            newFwdP2.z = Mathf.Clamp(newFwdP2.z, newFwdP3.z + 0.01f, newFwdP0.z - 0.02f);
            newFwdP1.z = Mathf.Clamp(newFwdP1.z, newFwdP2.z + 0.01f, newFwdP0.z - 0.01f);

            float fwdDz = newFwdP0.z - newFwdP2.z;
            float fwdT = (fwdDz > 0.001f) ? Mathf.Clamp01((newFwdP1.z - newFwdP2.z) / fwdDz) : 0f;
            float innerBowMinX = Mathf.Lerp(newFwdP2.x, newFwdP0.x, fwdT);

            newFwdP1.x = Mathf.Clamp(Mathf.Abs(newFwdP1.x), innerBowMinX, maxBeamWidth);

            newFwdP2.x = maxBeamWidth;
            newAftP1.x = maxBeamWidth;

            // Clamp Midship Apex Y height so it never exceeds Bow Tip Y or Stern Corner Y
            float maxAllowedApexY = Mathf.Min(newFwdP0.y, newAftP2.y);
            newFwdP3.y = Mathf.Min(newFwdP3.y, maxAllowedApexY);

            float apexY = newFwdP3.y;
            newFwdP2.y = apexY;
            newAftP1.y = apexY;

            float maxBowY = Mathf.Lerp(newFwdP2.y, newFwdP0.y, fwdT);
            newFwdP1.y = Mathf.Clamp(newFwdP1.y, apexY, maxBowY);

            newAftP1.z = Mathf.Clamp(newAftP1.z, sternZ + 0.01f, newFwdP3.z - 0.01f);

            float maxTransomRatio = (plan.breadthCurves != null) ? plan.breadthCurves.transomWidth : 1.0f;
            float maxAllowedTransomX = hw + (maxBeamWidth - hw) * maxTransomRatio;
            newAftP2.x = Mathf.Clamp(Mathf.Abs(newAftP2.x), hw, maxAllowedTransomX);

            float tSpan = maxBeamWidth - hw;
            if (tSpan > 0.001f)
            {
                plan.transomWidth = Mathf.Clamp((newAftP2.x - hw) / tSpan, 0f, maxTransomRatio);
            }

            float dzTotal = bowZ - sternZ;
            if (dzTotal > 0.001f)
            {
                plan.midshipApexZPercentage = Mathf.Clamp((newFwdP3.z - sternZ) / dzTotal, 0.05f, 0.95f);
            }

            if (plan.breadthCurves != null && Mathf.Abs(newFwdP3.z - plan.fwdP3.z) > 0.001f)
            {
                Undo.RecordObject(plan.breadthCurves, "Drag Midship Apex Z Handle");
                float bBowZ = (plan.sheerCurves != null) ? plan.sheerCurves.stemP3.z : 15f;
                float bSternZ = (plan.sheerCurves != null) ? plan.sheerCurves.sternP3.z : -15f;
                float bDz = bBowZ - bSternZ;
                if (bDz > 0.001f)
                {
                    plan.breadthCurves.midshipApexZPercentage = Mathf.Clamp((newFwdP3.z - bSternZ) / bDz, 0.05f, 0.95f);
                }
                plan.breadthCurves.UpdateCachedCurves();
                EditorUtility.SetDirty(plan.breadthCurves);
            }

            float newFwdSpan = bowZ - newFwdP3.z;
            if (newFwdSpan > 0.001f)
            {
                plan.fwdP2Ratio = Mathf.Clamp01((newFwdP2.z - newFwdP3.z) / newFwdSpan);
                plan.fwdP1Ratio = Mathf.Clamp01((newFwdP1.z - newFwdP3.z) / newFwdSpan);
            }

            float newAftSpan = newFwdP3.z - sternZ;
            if (newAftSpan > 0.001f)
            {
                plan.aftP1Ratio = Mathf.Clamp01((newAftP1.z - sternZ) / newAftSpan);
            }

            float newBeamSpan = newFwdP3.x - hw;
            if (newBeamSpan > 0.001f)
            {
                plan.fwdP1XRatio = Mathf.Clamp01((newFwdP1.x - hw) / newBeamSpan);
            }

            float ySpan = maxBowY - apexY;
            if (ySpan > 0.001f)
            {
                plan.fwdP1YRatio = Mathf.Clamp01((newFwdP1.y - apexY) / ySpan);
            }

            // Sync with SheerCurves Upper Stem Post and Transom Rake Top
            if (plan.sheerCurves != null)
            {
                newFwdP0.z = plan.sheerCurves.upperStemTop.z;
                newFwdP0.y = plan.sheerCurves.upperStemTop.y;

                newAftP2.z = plan.sheerCurves.transomP1.z;
                newAftP2.y = plan.sheerCurves.transomP1.y;
                if (plan.cachedIsAftCubic)
                {
                    newAftP3.z = plan.sheerCurves.transomP1.z;
                    newAftP3.y = plan.sheerCurves.transomP1.y;
                }
            }

            plan.maxBeam = maxBeamWidth;

            plan.fwdP0 = newFwdP0;
            plan.fwdP1 = newFwdP1;
            plan.fwdP2 = newFwdP2;
            plan.fwdP3 = newFwdP3;

            plan.aftP1 = newAftP1;
            plan.aftP2 = newAftP2;
            plan.aftP3 = newAftP3;

            plan.UpdateCachedCurves();
            EditorUtility.SetDirty(plan);

            BodyCurves bodyCurves = Object.FindFirstObjectByType<BodyCurves>();
            if (bodyCurves != null)
            {
                bodyCurves.UpdateCachedCurves();
                EditorUtility.SetDirty(bodyCurves);
            }
        }
    }
}
#endif
