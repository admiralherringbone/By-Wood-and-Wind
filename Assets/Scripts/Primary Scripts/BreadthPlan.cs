using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class BreadthPlan : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan to get hull lengths and base deck heights.")]
    public SheerPlan sheerPlan;
    
    [Tooltip("Reference to the ModularBodyPlan to get physical Keel Width.")]
    public BodyPlan bodyPlan;

    [Header("Gizmo Settings")]
    public bool showLabels = true;

    [Header("Core Dimensions")]
    [Tooltip("The maximum half-width of the ship at midship.")]
    public float maxBeam = 4f;

    [Header("Stern Dimensions")]
    [Tooltip("The width of the flat transom at the stern as a percentage (0 to 1) of the Max Beam. If 0, the stern comes to a point.")]
    [Range(0f, 1f)]
    public float transomWidth = 0.5f;

    [Header("Forward Curve Controls")]
    [Tooltip("Angle of the Bow control arm (0 = Horizontal X-axis, 90 = Pointing at Midship).")]
    [Range(0f, 90f)]
    public float bowArmAngle = 0f;

    [Tooltip("Length of the Bow control arm as a percentage (0 to 1) of the bounding box.")]
    [Range(0f, 1f)]
    public float bowArmLength = 1f;

    [Tooltip("Length of the Midship Forward control arm as a percentage (0 to 1) of the half length.")]
    [Range(0f, 1f)]
    public float midshipFwdArmLength = 0.5f;

    [Header("Aft Curve Controls")]
    [Tooltip("Length of the Midship Aft control arm as a percentage (0 to 1) of the half length.")]
    [Range(0f, 1f)]
    public float midshipAftArmLength = 0.5f;

    [Tooltip("Angle of the Stern control arm (only active when Transom Width is 0). (0 = Horizontal X-axis, 90 = Pointing at Midship).")]
    [Range(0f, 90f)]
    public float sternArmAngle = 0f;

    [Tooltip("Length of the Stern control arm as a percentage (0 to 1) of the bounding box (only active when Transom Width is 0).")]
    [Range(0f, 1f)]
    public float sternArmLength = 1f;

    [Header("Top Timber Dimensions")]
    [HideInInspector] public float topTimberMaxBeam = 3.5f;
    [Tooltip("The width of the transom at the Top Timber line as a percentage of Top Timber Max Beam.")]
    [Range(0f, 1f)] public float topTimberTransomWidth = 0.5f;

    [Header("Top Timber Forward Controls")]
    [Range(0f, 90f)] public float topTimberBowArmAngle = 0f;
    [Range(0f, 1f)] public float topTimberBowArmLength = 1f;
    [Range(0f, 1f)] public float topTimberMidshipFwdArmLength = 0.5f;

    [Header("Top Timber Aft Controls")]
    [Range(0f, 1f)] public float topTimberMidshipAftArmLength = 0.5f;
    [Range(0f, 90f)] public float topTimberSternArmAngle = 0f;
    [Range(0f, 1f)] public float topTimberSternArmLength = 1f;

    [Header("Floor Line Controls")]
    [Tooltip("Show the Floor Line curves in Scene view gizmos.")]
    public bool showFloorLine = true;

    [Tooltip("Color of the Floor Line curves.")]
    public Color floorLineColor = new Color(1f, 0.85f, 0.2f, 0.9f);

    [Tooltip("Half-width of the Floor Line at Midships (Z = 0). Must sit between Keel half-width and Max Beam.")]
    public float midshipFloorWidth = 2.0f;

    [Tooltip("Control arm bias for the forward Floor Line quadratic curve (0 = straight/narrow, 1 = full width/curve).")]
    [Range(0f, 1f)] public float floorBowControl = 0.5f;

    [Tooltip("Control arm bias for the aft Floor Line quadratic curve (0 = straight/narrow, 1 = full width/curve).")]
    [Range(0f, 1f)] public float floorSternControl = 0.5f;

    [Header("Diagonal Bilge Line Controls")]
    [Tooltip("Show the Diagonal Bilge Width curves in Scene view gizmos.")]
    public bool showBilgeCurve = true;

    [Tooltip("Color of the Diagonal Bilge Width curve.")]
    public Color bilgeCurveColor = new Color(0.1f, 0.9f, 0.4f, 0.9f);

    [Tooltip("Relative width of the Diagonal Bilge Line between Floor Line (0.0) and Max Breadth (1.0).")]
    [Range(0.05f, 0.95f)]
    public float bilgeWidthRatio = 0.6f;

    // --- CACHED POINTS FOR FAST ACCESS ---
    [HideInInspector] public Vector3 cachedFwdP0;
    [HideInInspector] public Vector3 cachedFwdP1;
    [HideInInspector] public Vector3 cachedFwdP2;
    [HideInInspector] public Vector3 cachedFwdP3;
    
    [HideInInspector] public Vector3 cachedAftP0;
    [HideInInspector] public Vector3 cachedAftP1;
    [HideInInspector] public Vector3 cachedAftP2;
    [HideInInspector] public Vector3 cachedAftP3;
    [HideInInspector] public bool cachedIsAftCubic;
    
    [HideInInspector] public Vector3 cachedTTFwdP0;
    [HideInInspector] public Vector3 cachedTTFwdP1;
    [HideInInspector] public Vector3 cachedTTFwdP2;
    [HideInInspector] public Vector3 cachedTTFwdP3;
    
    [HideInInspector] public Vector3 cachedTTAftP0;
    [HideInInspector] public Vector3 cachedTTAftP1;
    [HideInInspector] public Vector3 cachedTTAftP2;
    [HideInInspector] public Vector3 cachedTTAftP3;
    [HideInInspector] public bool cachedIsTTAftCubic;

    [HideInInspector] public Vector3 cachedFloorBowPoint;
    [HideInInspector] public Vector3 cachedFloorBowCp;
    [HideInInspector] public Vector3 cachedFloorMidshipPoint;
    [HideInInspector] public Vector3 cachedFloorSternCp;
    [HideInInspector] public Vector3 cachedFloorSternPoint;
    [HideInInspector] public int changeVersion = 0;

    private bool isUpdatingCurves = false;

    private void OnValidate()
    {
        float hw = (bodyPlan != null) ? (bodyPlan.keelWidth / 2f) : 0.1f;
        midshipFloorWidth = Mathf.Clamp(midshipFloorWidth, hw + 0.01f, Mathf.Max(hw + 0.05f, maxBeam - 0.01f));
        bilgeWidthRatio = Mathf.Clamp(bilgeWidthRatio, 0.05f, 0.95f);
        UpdateCachedCurves();
    }

    public virtual void UpdateCachedCurves()
    {
        if (isUpdatingCurves) return;
        if (sheerPlan == null) return;

        isUpdatingCurves = true;
        try
        {
            if (bodyPlan == null) bodyPlan = GetComponentInParent<BodyPlan>();
            if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();

            sheerPlan.UpdateCachedValues(); // Ensure sheer plan is up to date

        float halfLength = sheerPlan.lengthBetweenPerpendiculars / 2f;
        float midshipZ = 0f;
        
        // Fetch boundaries from the ModularSheerPlan
        float bowZ = sheerPlan.cachedHobBowIntersect.z;
        float sternZ = sheerPlan.cachedHobSternIntersect.z;
        float baseY = sheerPlan.cachedHobMidshipPoint.y; // Draw breadth curves at the flat HoB height for Gizmos

        float hw = (bodyPlan != null) ? (bodyPlan.keelWidth / 2f) : 0f;

        // FORWARD CURVE
        Vector3 fwdP0 = new Vector3(hw, baseY, bowZ);
        Vector3 fwdP3 = new Vector3(maxBeam, baseY, midshipZ);
        Vector3 fwdP2 = fwdP3 + new Vector3(0, 0, halfLength * midshipFwdArmLength);

        float maxFwdAngle = Mathf.Atan2(fwdP2.z - bowZ, maxBeam) * Mathf.Rad2Deg;
        float fwdAngleRad = Mathf.Lerp(0, maxFwdAngle, bowArmAngle / 90f) * Mathf.Deg2Rad;
        float fwdDirX = Mathf.Cos(fwdAngleRad);
        float fwdDirZ = Mathf.Sin(fwdAngleRad);

        float fwdMaxDist;
        if (Mathf.Abs(fwdDirZ) < 0.0001f) fwdMaxDist = maxBeam;
        else if (Mathf.Abs(fwdDirX) < 0.0001f) fwdMaxDist = Mathf.Abs(bowZ - fwdP2.z);
        else
        {
            float fwdDistX = maxBeam / Mathf.Abs(fwdDirX);
            float fwdDistZ = Mathf.Abs(bowZ - fwdP2.z) / Mathf.Abs(fwdDirZ);
            fwdMaxDist = Mathf.Min(fwdDistX, fwdDistZ);
        }

        Vector3 fwdP1 = fwdP0 + new Vector3(fwdDirX, 0, fwdDirZ) * (fwdMaxDist * bowArmLength);

        // AFT CURVE
        Vector3 aftP0 = fwdP3;
        Vector3 aftP1 = aftP0 + new Vector3(0, 0, -halfLength * midshipAftArmLength);

        Vector3 aftP2 = Vector3.zero;
        Vector3 aftP3 = Vector3.zero;
        bool isAftCubic = transomWidth <= 0.001f;

        if (isAftCubic)
        {
            aftP3 = new Vector3(hw, baseY, sternZ);

            float maxAftAngle = Mathf.Atan2(aftP1.z - sternZ, maxBeam) * Mathf.Rad2Deg;
            float aftAngleRad = Mathf.Lerp(0, maxAftAngle, sternArmAngle / 90f) * Mathf.Deg2Rad;
            float aftDirX = Mathf.Cos(aftAngleRad);
            float aftDirZ = Mathf.Sin(aftAngleRad);

            float aftMaxDist;
            if (Mathf.Abs(aftDirZ) < 0.0001f) aftMaxDist = maxBeam;
            else if (Mathf.Abs(aftDirX) < 0.0001f) aftMaxDist = Mathf.Abs(aftP1.z - sternZ);
            else
            {
                float aftDistX = maxBeam / Mathf.Abs(aftDirX);
                float aftDistZ = Mathf.Abs(aftP1.z - sternZ) / Mathf.Abs(aftDirZ);
                aftMaxDist = Mathf.Min(aftDistX, aftDistZ);
            }

            aftP2 = aftP3 + new Vector3(aftDirX, 0, aftDirZ) * (aftMaxDist * sternArmLength);
        }
        else
        {
            aftP2 = new Vector3(maxBeam * transomWidth, baseY, sternZ);
        }

        cachedFwdP0 = fwdP0; cachedFwdP1 = fwdP1; cachedFwdP2 = fwdP2; cachedFwdP3 = fwdP3;
        cachedAftP0 = aftP0; cachedAftP1 = aftP1; cachedAftP2 = aftP2; cachedAftP3 = aftP3;
        cachedIsAftCubic = isAftCubic;

        // TOP TIMBER FORWARD CURVE
        float ttBowZ = sheerPlan.cachedUpperStemTopPoint.z;
        float ttSternZ = sheerPlan.cachedTransomTopPoint.z;
        
        // Find the absolute highest part of the hull for the Top Timber breadth plan
        float ttBaseY = sheerPlan.cachedBaseDeckMidshipPoint.y;
        if (sheerPlan.upperDecks != null && sheerPlan.upperDecks.Count > 0)
        {
            ttBaseY += sheerPlan.GetMaxDeckHeight() + sheerPlan.GetGlobalSheerOffsetAtZ(0);
        }
        else
        {
            ttBaseY = sheerPlan.cachedTopTimberMidshipPoint.y;
        }

        Vector3 ttFwdP0 = new Vector3(hw, ttBaseY, ttBowZ);
        Vector3 ttFwdP3 = new Vector3(topTimberMaxBeam, ttBaseY, midshipZ);
        Vector3 ttFwdP2 = ttFwdP3 + new Vector3(0, 0, halfLength * topTimberMidshipFwdArmLength);

        float ttMaxFwdAngle = Mathf.Atan2(ttFwdP2.z - ttBowZ, topTimberMaxBeam) * Mathf.Rad2Deg;
        float ttFwdAngleRad = Mathf.Lerp(0, ttMaxFwdAngle, topTimberBowArmAngle / 90f) * Mathf.Deg2Rad;
        float ttFwdDirX = Mathf.Cos(ttFwdAngleRad);
        float ttFwdDirZ = Mathf.Sin(ttFwdAngleRad);

        float ttFwdMaxDist;
        if (Mathf.Abs(ttFwdDirZ) < 0.0001f) ttFwdMaxDist = topTimberMaxBeam;
        else if (Mathf.Abs(ttFwdDirX) < 0.0001f) ttFwdMaxDist = Mathf.Abs(ttBowZ - ttFwdP2.z);
        else
        {
            float fwdDistX = topTimberMaxBeam / Mathf.Abs(ttFwdDirX);
            float fwdDistZ = Mathf.Abs(ttBowZ - ttFwdP2.z) / Mathf.Abs(ttFwdDirZ);
            ttFwdMaxDist = Mathf.Min(fwdDistX, fwdDistZ);
        }

        Vector3 ttFwdP1 = ttFwdP0 + new Vector3(ttFwdDirX, 0, ttFwdDirZ) * (ttFwdMaxDist * topTimberBowArmLength);

        // TOP TIMBER AFT CURVE
        Vector3 ttAftP0 = ttFwdP3;
        Vector3 ttAftP1 = ttAftP0 + new Vector3(0, 0, -halfLength * topTimberMidshipAftArmLength);

        Vector3 ttAftP2 = Vector3.zero;
        Vector3 ttAftP3 = Vector3.zero;
        bool isTTAftCubic = topTimberTransomWidth <= 0.001f;

        if (isTTAftCubic)
        {
            ttAftP3 = new Vector3(hw, ttBaseY, ttSternZ);

            float ttMaxAftAngle = Mathf.Atan2(ttAftP1.z - ttSternZ, topTimberMaxBeam) * Mathf.Rad2Deg;
            float ttAftAngleRad = Mathf.Lerp(0, ttMaxAftAngle, topTimberSternArmAngle / 90f) * Mathf.Deg2Rad;
            float ttAftDirX = Mathf.Cos(ttAftAngleRad);
            float ttAftDirZ = Mathf.Sin(ttAftAngleRad);

            float ttAftMaxDist;
            if (Mathf.Abs(ttAftDirZ) < 0.0001f) ttAftMaxDist = topTimberMaxBeam;
            else if (Mathf.Abs(ttAftDirX) < 0.0001f) ttAftMaxDist = Mathf.Abs(ttAftP1.z - ttSternZ);
            else
            {
                float aftDistX = topTimberMaxBeam / Mathf.Abs(ttAftDirX);
                float aftDistZ = Mathf.Abs(ttAftP1.z - ttSternZ) / Mathf.Abs(ttAftDirZ);
                ttAftMaxDist = Mathf.Min(aftDistX, aftDistZ);
            }

            ttAftP2 = ttAftP3 + new Vector3(ttAftDirX, 0, ttAftDirZ) * (ttAftMaxDist * topTimberSternArmLength);
        }
        else
        {
            ttAftP2 = new Vector3(topTimberMaxBeam * topTimberTransomWidth, ttBaseY, ttSternZ);
        }

        cachedTTFwdP0 = ttFwdP0; cachedTTFwdP1 = ttFwdP1; cachedTTFwdP2 = ttFwdP2; cachedTTFwdP3 = ttFwdP3;
        cachedTTAftP0 = ttAftP0; cachedTTAftP1 = ttAftP1; cachedTTAftP2 = ttAftP2; cachedTTAftP3 = ttAftP3;
        cachedIsTTAftCubic = isTTAftCubic;

        // FLOOR LINE (QUADRATIC BEZIERS FOR BOW AND AFT CURVES - FLAT 2D PLANE AT RISING LINE Y)
        float floorBowZ = sheerPlan.cachedBowRisingEnd.z;
        float floorSternZ = sheerPlan.cachedSternRisingEnd.z;
        float floorMaxBeam = Mathf.Clamp(midshipFloorWidth, hw + 0.01f, Mathf.Max(hw + 0.05f, maxBeam - 0.01f));
        float floorBaseY = sheerPlan.GetRisingLineYAtZ(0f);

        Vector3 floorBowPoint = new Vector3(hw, floorBaseY, floorBowZ);
        Vector3 floorMidshipPoint = new Vector3(floorMaxBeam, floorBaseY, midshipZ);
        Vector3 floorSternPoint = new Vector3(hw, floorBaseY, floorSternZ);

        float floorBowControlZ = Mathf.Lerp(midshipZ, floorBowZ, Mathf.Clamp(floorBowControl, 0f, 0.95f));
        Vector3 floorBowCp = new Vector3(floorMaxBeam, floorBaseY, floorBowControlZ);

        float floorSternControlZ = Mathf.Lerp(midshipZ, floorSternZ, Mathf.Clamp(floorSternControl, 0f, 0.95f));
        Vector3 floorSternCp = new Vector3(floorMaxBeam, floorBaseY, floorSternControlZ);

        cachedFloorBowPoint = floorBowPoint;
        cachedFloorBowCp = floorBowCp;
        cachedFloorMidshipPoint = floorMidshipPoint;
        cachedFloorSternCp = floorSternCp;
        cachedFloorSternPoint = floorSternPoint;
        changeVersion++;
        }
        finally
        {
            isUpdatingCurves = false;
        }
    }

    // --- MATHEMATICAL EVALUATION METHODS ---

    public float GetConnectionPointXAtZ(float z)
    {
        if (bodyPlan == null || sheerPlan == null) return (bodyPlan != null) ? (bodyPlan.keelWidth / 2f) : 0.25f;

        float floorX = GetFloorLineXAtZ(z);
        float hobX = GetTheoreticalBreadthXAtZ(z);
        float connRatio = (bodyPlan != null) ? bodyPlan.GetConnectionPointPositionAtZ(z) : 0.5f;

        return Mathf.Lerp(floorX, hobX, connRatio);
    }

    public float GetBreadthXAtZ(float z)
    {
        if (z >= 0)
        {
            if (z >= cachedFwdP0.z) return cachedFwdP0.x;
            float t = GetBezierTAtZ(cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3, z, false);
            return CalculateCubicBezierPoint(t, cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3).x;
        }
        else
        {
            if (cachedIsAftCubic)
            {
                if (z <= cachedAftP3.z) return cachedAftP3.x;
                float t = GetBezierTAtZ(cachedAftP0, cachedAftP1, cachedAftP2, cachedAftP3, z, false);
                return CalculateCubicBezierPoint(t, cachedAftP0, cachedAftP1, cachedAftP2, cachedAftP3).x;
            }
            else
            {
                if (z <= cachedAftP2.z) return cachedAftP2.x;
                float t = GetBezierTAtZ(cachedAftP0, cachedAftP1, cachedAftP2, Vector3.zero, z, false);
                return CalculateQuadraticBezierPoint(t, cachedAftP0, cachedAftP1, cachedAftP2).x;
            }
        }
    }

    public float GetTheoreticalBreadthXAtZ(float z)
    {
        if (z >= 0) return GetBreadthXAtZ(z);

        if (cachedIsAftCubic)
        {
            if (z > cachedAftP3.z) return GetBreadthXAtZ(z);
            
            float dz = cachedAftP3.z - cachedAftP2.z;
            float dx = cachedAftP3.x - cachedAftP2.x;
            if (Mathf.Abs(dz) < 0.001f) return cachedAftP3.x;
            
            return cachedAftP3.x + (dx / dz) * (z - cachedAftP3.z);
        }
        else
        {
            if (z > cachedAftP2.z) return GetBreadthXAtZ(z);
            
            float dz = cachedAftP2.z - cachedAftP1.z;
            float dx = cachedAftP2.x - cachedAftP1.x;
            if (Mathf.Abs(dz) < 0.001f) return cachedAftP2.x;
            
            return cachedAftP2.x + (dx / dz) * (z - cachedAftP2.z);
        }
    }

    public float GetTopTimberXAtZ(float z)
    {
        float rawX;
        if (z >= 0)
        {
            if (z >= cachedTTFwdP0.z) rawX = cachedTTFwdP0.x;
            else
            {
                float t = GetBezierTAtZ(cachedTTFwdP0, cachedTTFwdP1, cachedTTFwdP2, cachedTTFwdP3, z, false);
                rawX = CalculateCubicBezierPoint(t, cachedTTFwdP0, cachedTTFwdP1, cachedTTFwdP2, cachedTTFwdP3).x;
            }
        }
        else
        {
            if (cachedIsTTAftCubic)
            {
                if (z <= cachedTTAftP3.z) rawX = cachedTTAftP3.x;
                else
                {
                    float t = GetBezierTAtZ(cachedTTAftP0, cachedTTAftP1, cachedTTAftP2, cachedTTAftP3, z, false);
                    rawX = CalculateCubicBezierPoint(t, cachedTTAftP0, cachedTTAftP1, cachedTTAftP2, cachedTTAftP3).x;
                }
            }
            else
            {
                if (z <= cachedTTAftP2.z) rawX = cachedTTAftP2.x;
                else
                {
                    float t = GetBezierTAtZ(cachedTTAftP0, cachedTTAftP1, cachedTTAftP2, Vector3.zero, z, false);
                    rawX = CalculateQuadraticBezierPoint(t, cachedTTAftP0, cachedTTAftP1, cachedTTAftP2).x;
                }
            }
        }

        // Restriction 5: Tumblehome / Topsides boundary restriction (p4 must not narrow by more than 40% of HOB max breadth)
        float hobX = GetTheoreticalBreadthXAtZ(z);
        float minTumblehomeX = hobX * 0.60f;
        return Mathf.Max(rawX, minTumblehomeX);
    }

    public float GetFloorLineXAtZ(float z)
    {
        float rawX;
        if (z >= 0)
        {
            if (z >= cachedFloorBowPoint.z) rawX = cachedFloorBowPoint.x;
            else
            {
                float t = GetBezierTAtZ(cachedFloorBowPoint, cachedFloorBowCp, cachedFloorMidshipPoint, Vector3.zero, z, false);
                rawX = CalculateQuadraticBezierPoint(t, cachedFloorBowPoint, cachedFloorBowCp, cachedFloorMidshipPoint).x;
            }
        }
        else
        {
            if (z <= cachedFloorSternPoint.z) rawX = cachedFloorSternPoint.x;
            else
            {
                float t = GetBezierTAtZ(cachedFloorMidshipPoint, cachedFloorSternCp, cachedFloorSternPoint, Vector3.zero, z, false);
                rawX = CalculateQuadraticBezierPoint(t, cachedFloorMidshipPoint, cachedFloorSternCp, cachedFloorSternPoint).x;
            }
        }

        float minKeelX = (bodyPlan != null) ? (bodyPlan.keelWidth / 2f) : 0.25f;
        return Mathf.Clamp(rawX, minKeelX, GetTheoreticalBreadthXAtZ(z));
    }

    /// <summary>
    /// Computes the 3D Diagonal Bilge Width at station Z.
    /// Positioned smoothly between Floor Line width and Max Breadth.
    /// </summary>
    public float GetBilgeXAtZ(float z)
    {
        float floorX = GetFloorLineXAtZ(z);
        float hobX = GetTheoreticalBreadthXAtZ(z);
        float ratio = Mathf.Clamp(bilgeWidthRatio, 0.05f, 0.95f);
        float bilgeX = Mathf.Lerp(floorX, hobX, ratio);

        if (sheerPlan != null)
        {
            float floorY = sheerPlan.GetRisingLineYAtZ(z);
            float hobY = sheerPlan.GetTheoreticalHobYAtZ(z);
            float ratioY = Mathf.Clamp(sheerPlan.bilgeHeightRatio, 0.05f, 0.95f);
            float approxBilgeY = Mathf.Lerp(floorY, hobY, ratioY);

            // Inward clamp (line p1 -> p3)
            float dy = hobY - floorY;
            if (Mathf.Abs(dy) > 0.0001f)
            {
                float tY = Mathf.Clamp01((approxBilgeY - floorY) / dy);
                float minBilgeX = Mathf.Lerp(floorX, hobX, tY);
                bilgeX = Mathf.Max(bilgeX, minBilgeX);
            }

            // Restriction 3: Bilge point (p2) can never go outward past HOB vertical boundary (p3)
            Vector3 p1 = new Vector3(floorX, floorY, z);
            Vector3 p3 = new Vector3(hobX, hobY, z);
            Vector3 approxP2 = new Vector3(bilgeX, approxBilgeY, z);
            float d13 = Vector3.Distance(p1, p3);
            float d23 = Vector3.Distance(approxP2, p3);
            if (d13 > 0.0001f)
            {
                float tangentOffset = (hobX - floorX) / d13 * (d23 / 3f);
                float maxBilgeX = hobX - tangentOffset;
                bilgeX = Mathf.Min(bilgeX, maxBilgeX);
            }
        }
        return bilgeX;
    }

    // --- UTILITIES ---

    private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        return (uu * p0) + (2 * u * t * p1) + (tt * p2);
    }

    private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        return (uuu * p0) + (3 * uu * t * p1) + (3 * u * tt * p2) + (ttt * p3);
    }

    private float GetBezierTAtZ(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float targetZ, bool isForward)
    {
        float min = 0f;
        float max = 1f;
        for (int i = 0; i < 20; i++)
        {
            float mid = (min + max) / 2f;
            
            float z;
            if (p3 == Vector3.zero) z = CalculateQuadraticBezierPoint(mid, p0, p1, p2).z; // Hack to reuse for Quadratic
            else z = CalculateCubicBezierPoint(mid, p0, p1, p2, p3).z;

            if ((isForward && z < targetZ) || (!isForward && z > targetZ)) min = mid;
            else max = mid;
        }
        return (min + max) / 2f;
    }

    // --- GIZMOS ---

    private void OnDrawGizmos()
    {
        if (sheerPlan == null) return;
        
        UpdateCachedCurves();

        // Draw Starboard Curve (Green)
        Gizmos.color = Color.green;
        DrawCubicBezierCurve(cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3, Color.green);
        if (cachedIsAftCubic) DrawCubicBezierCurve(cachedAftP0, cachedAftP1, cachedAftP2, cachedAftP3, Color.green);
        else DrawQuadraticBezierCurve(cachedAftP0, cachedAftP1, cachedAftP2, Color.green);

        // Draw Port Curve (Mirrored X) (Red)
        Gizmos.color = Color.red;
        DrawCubicBezierCurve(MirrorX(cachedFwdP0), MirrorX(cachedFwdP1), MirrorX(cachedFwdP2), MirrorX(cachedFwdP3), Color.red);
        if (cachedIsAftCubic) DrawCubicBezierCurve(MirrorX(cachedAftP0), MirrorX(cachedAftP1), MirrorX(cachedAftP2), MirrorX(cachedAftP3), Color.red);
        else DrawQuadraticBezierCurve(MirrorX(cachedAftP0), MirrorX(cachedAftP1), MirrorX(cachedAftP2), Color.red);

        // Draw Top Timber Starboard Curve (Cyan)
        Gizmos.color = Color.cyan;
        DrawCubicBezierCurve(cachedTTFwdP0, cachedTTFwdP1, cachedTTFwdP2, cachedTTFwdP3, Color.cyan);
        if (cachedIsTTAftCubic) DrawCubicBezierCurve(cachedTTAftP0, cachedTTAftP1, cachedTTAftP2, cachedTTAftP3, Color.cyan);
        else DrawQuadraticBezierCurve(cachedTTAftP0, cachedTTAftP1, cachedTTAftP2, Color.cyan);

        // Draw Top Timber Port Curve (Mirrored X) (Magenta)
        Gizmos.color = Color.magenta;
        DrawCubicBezierCurve(MirrorX(cachedTTFwdP0), MirrorX(cachedTTFwdP1), MirrorX(cachedTTFwdP2), MirrorX(cachedTTFwdP3), Color.magenta);
        if (cachedIsTTAftCubic) DrawCubicBezierCurve(MirrorX(cachedTTAftP0), MirrorX(cachedTTAftP1), MirrorX(cachedTTAftP2), MirrorX(cachedTTAftP3), Color.magenta);
        else DrawQuadraticBezierCurve(MirrorX(cachedTTAftP0), MirrorX(cachedTTAftP1), MirrorX(cachedTTAftP2), Color.magenta);

        // Draw Control Arms (Starboard only)
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawLine(cachedFwdP0, cachedFwdP1);
        Gizmos.DrawLine(cachedFwdP3, cachedFwdP2);
        
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawLine(cachedAftP0, cachedAftP1);
        if (cachedIsAftCubic) Gizmos.DrawLine(cachedAftP3, cachedAftP2);

        // Draw Transom Line
        if (!cachedIsAftCubic)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(cachedAftP2, MirrorX(cachedAftP2));
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(cachedAftP2, 0.15f);
            Gizmos.DrawSphere(MirrorX(cachedAftP2), 0.15f);
        }

        // Draw Top Timber Transom Line
        if (!cachedIsTTAftCubic)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(cachedTTAftP2, MirrorX(cachedTTAftP2));
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(cachedTTAftP2, 0.15f);
            Gizmos.DrawSphere(MirrorX(cachedTTAftP2), 0.15f);
        }

        // Draw Nodes (Starboard)
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(cachedFwdP1, 0.1f);
        Gizmos.DrawSphere(cachedFwdP2, 0.1f);
        Gizmos.DrawSphere(cachedAftP1, 0.1f);
        if (cachedIsAftCubic) Gizmos.DrawSphere(cachedAftP2, 0.1f);
        
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(cachedFwdP0, 0.15f);
        Gizmos.DrawSphere(cachedFwdP3, 0.15f);
        if (cachedIsAftCubic) Gizmos.DrawSphere(cachedAftP3, 0.15f);

        // Draw Top Timber Nodes (Starboard)
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(cachedTTFwdP1, 0.1f);
        Gizmos.DrawSphere(cachedTTFwdP2, 0.1f);
        Gizmos.DrawSphere(cachedTTAftP1, 0.1f);
        if (cachedIsTTAftCubic) Gizmos.DrawSphere(cachedTTAftP2, 0.1f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(cachedTTFwdP0, 0.15f);
        Gizmos.DrawSphere(cachedTTFwdP3, 0.15f);
        if (cachedIsTTAftCubic) Gizmos.DrawSphere(cachedTTAftP3, 0.15f);

        // Draw Floor Line Curves (Starboard & Port Mirrored)
        if (showFloorLine)
        {
            Gizmos.color = floorLineColor;
            const int floorSamples = 80;
            float fAftZ = cachedFloorSternPoint.z;
            float fFwdZ = cachedFloorBowPoint.z;
            Vector3 prevStbd = Vector3.zero;
            Vector3 prevPort = Vector3.zero;
            for (int i = 0; i <= floorSamples; i++)
            {
                float t = i / (float)floorSamples;
                float z = Mathf.Lerp(fAftZ, fFwdZ, t);
                float x = GetFloorLineXAtZ(z);
                float y = (sheerPlan != null) ? sheerPlan.GetRisingLineYAtZ(z) : cachedFloorMidshipPoint.y;

                Vector3 currStbd = new Vector3(x, y, z);
                Vector3 currPort = new Vector3(-x, y, z);
                if (i > 0)
                {
                    Gizmos.DrawLine(prevStbd, currStbd);
                    Gizmos.DrawLine(prevPort, currPort);
                }
                prevStbd = currStbd;
                prevPort = currPort;
            }

            Gizmos.color = new Color(floorLineColor.r, floorLineColor.g, floorLineColor.b, 0.4f);
            Gizmos.DrawLine(cachedFloorMidshipPoint, cachedFloorBowCp);
            Gizmos.DrawLine(cachedFloorBowCp, cachedFloorBowPoint);
            Gizmos.DrawLine(cachedFloorMidshipPoint, cachedFloorSternCp);
            Gizmos.DrawLine(cachedFloorSternCp, cachedFloorSternPoint);

            Gizmos.color = floorLineColor;
            Gizmos.DrawSphere(cachedFloorBowPoint, 0.12f);
            Gizmos.DrawSphere(cachedFloorBowCp, 0.08f);
            Gizmos.DrawSphere(cachedFloorMidshipPoint, 0.12f);
            Gizmos.DrawSphere(cachedFloorSternCp, 0.08f);
            Gizmos.DrawSphere(cachedFloorSternPoint, 0.12f);
        }

        // Draw Diagonal Bilge Width Curves (Starboard & Port Mirrored)
        if (showBilgeCurve)
        {
            Gizmos.color = bilgeCurveColor;
            float sternZ = (sheerPlan != null) ? sheerPlan.GetBilgeSternZ() : -10f;
            float bowZ = (sheerPlan != null) ? sheerPlan.GetBilgeBowZ() : 10f;
            const int samples = 80;
            Vector3 prevStbd = Vector3.zero;
            Vector3 prevPort = Vector3.zero;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float z = Mathf.Lerp(sternZ, bowZ, t);
                float x = GetBilgeXAtZ(z);
                float y = (sheerPlan != null) ? sheerPlan.GetBilgeYAtZ(z) : sheerPlan.cachedHobMidshipPoint.y;
                Vector3 currStbd = new Vector3(x, y, z);
                Vector3 currPort = new Vector3(-x, y, z);
                if (i > 0)
                {
                    Gizmos.DrawLine(prevStbd, currStbd);
                    Gizmos.DrawLine(prevPort, currPort);
                }
                prevStbd = currStbd;
                prevPort = currPort;
            }
        }

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.LowerCenter;
            labelStyle.contentOffset = new Vector2(0, -10);

            Handles.Label(cachedFwdP0, "Stem Top (Breadth)", labelStyle);
            Handles.Label(cachedFwdP3, "Max Beam", labelStyle);
            if (cachedIsAftCubic) Handles.Label(cachedAftP3, "Stern Post (Breadth)", labelStyle);
            else Handles.Label(cachedAftP2, "Transom Edge", labelStyle);

            if (showFloorLine)
            {
                GUIStyle floorLabelStyle = new GUIStyle();
                floorLabelStyle.normal.textColor = floorLineColor;
                floorLabelStyle.alignment = TextAnchor.LowerCenter;
                floorLabelStyle.contentOffset = new Vector2(0, -10);

                Handles.Label(cachedFloorBowPoint, "Floor Line Bow End", floorLabelStyle);
                Handles.Label(cachedFloorMidshipPoint, "Floor Line Midship", floorLabelStyle);
                Handles.Label(cachedFloorSternPoint, "Floor Line Stern End", floorLabelStyle);
            }
        }
#endif
    }

    protected Vector3 MirrorX(Vector3 v)
    {
        return new Vector3(-v.x, v.y, v.z);
    }

    protected void DrawQuadraticBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Color color, int segments = 20)
    {
        Gizmos.color = color;
        Vector3 previousPoint = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 currentPoint = CalculateQuadraticBezierPoint(t, p0, p1, p2);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }

    protected void DrawCubicBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color, int segments = 20)
    {
        Gizmos.color = color;
        Vector3 previousPoint = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 currentPoint = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }
}
