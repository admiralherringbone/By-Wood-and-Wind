using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class ModularBodyPlanCounterTest : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan to get hull heights.")]
    public ModularSheerPlanCounterTest sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan to get max beam.")]
    public ModularBreadthPlanCounterTest breadthPlan;

    [Header("Gizmo Settings")]
    public bool showLabels = true;

    [Header("Lower Curve Controls")]
    [Tooltip("Angle of the Keel control arm (0 = Horizontal, 90 = Vertical).")]
    [Range(0f, 90f)]
    public float keelArmAngle = 0f;

    [Tooltip("Length of the Keel control arm as a percentage (0 to 1) of the bounding box intersection.")]
    [Range(0f, 1f)]
    public float keelArmLength = 1f;

    [Tooltip("Length of the HoB downward control arm as a percentage (0 to 1) of the total HoB height.")]
    [Range(0f, 1f)]
    public float hobLowerArmLength = 0.5f;

    [Header("Middle Control (Quartic Bezier)")]
    [Tooltip("If true, adds a 5th control point between the arms, converting the curve into a Degree-4 Quartic Bezier.")]
    public bool useQuarticBezier = false;

    [Tooltip("Position of the Middle Control point along the line between the Keel Arm and the HoB Arm.")]
    [Range(0f, 1f)] public float middleControlPosition = 0.5f;

    [Header("Inner Keel Control (Quintic Bezier)")]
    [Tooltip("If true, adds a 6th control point on the Keel Arm, converting the curve into a Degree-5 Quintic Bezier.")]
    public bool useQuinticBezier = false;
    
    [Tooltip("Position of the Inner Keel Control point along the Keel Arm.")]
    [Range(0f, 1f)] public float innerKeelPosition = 0.5f;

    [Tooltip("If true, restricts the manual sliders to perfectly obey the Convex Hull property, mathematically preventing ripples.")]
    public bool enforceConvexHull = true;

    [Header("Upper Mould Controls")]
    [Tooltip("The maximum half-width of the ship at the Top Timber line as a percentage of the Primary Max Beam. 1.0 = Straight. < 1.0 = Tumblehome. > 1.0 = Flare (Max 2.0 = 50% further out).")]
    [Range(0f, 2f)]
    public float flareTumblehomePercentage = 1f;

    [Tooltip("Angle of the HoB Upper control arm. 0 = Straight Up. Positive = Flare Out, Negative = Tumblehome In.")]
    [Range(-90f, 90f)]
    public float upperHobArmAngle = 0f;

    [Tooltip("Length of the HoB Upper control arm as a percentage (0 to 1) of the upper bounding box height.")]
    [Range(0f, 1f)]
    public float upperHobArmLength = 0.5f;

    [Tooltip("Angle of the Top Timber control arm. 0 = Straight Down. Positive = Flare Out, Negative = Tumblehome In.")]
    [Range(-90f, 90f)]
    public float topTimberArmAngle = 0f;

    [Tooltip("Length of the Top Timber control arm as a percentage (0 to 1) of the upper bounding box height.")]
    [Range(0f, 1f)]
    public float topTimberArmLength = 0.5f;

    [Header("Keel & Stem Width")]
    [Tooltip("The width of the physical Keel (P/S distance). The mould will start at keelWidth / 2.")]
    public float keelWidth = 0.5f;

    [HideInInspector, SerializeField] private float lastKeelArmLength = -1f;
    [HideInInspector] [SerializeField] private float lastKeelArmAngle = -1f;
    [HideInInspector] [SerializeField] private float lastHobLowerArmLength = -1f;
    [HideInInspector] [SerializeField] private float lastMiddleControlPosition = -1f;

    // --- CACHED POINTS ---
    [HideInInspector] public Vector3 cachedL0;
    [HideInInspector] public Vector3 cachedL1Inner;
    [HideInInspector] public Vector3 cachedL1;
    [HideInInspector] public Vector3 cachedLMid;
    [HideInInspector] public Vector3 cachedL2;
    [HideInInspector] public Vector3 cachedL3;
    
    [HideInInspector] public Vector3 cachedU0;
    [HideInInspector] public Vector3 cachedU1;
    [HideInInspector] public Vector3 cachedU2;
    [HideInInspector] public Vector3 cachedU3;
    
    [HideInInspector] public bool cachedUseQuartic;
    [HideInInspector] public bool cachedUseQuintic;

    private void OnValidate()
    {
        UpdateCachedCurves();
    }

    public virtual void UpdateCachedCurves()
    {
        if (sheerPlan == null || breadthPlan == null) return;
        
        // Push the Flare/Tumblehome value to the Breadth Plan BEFORE it calculates its curves
        breadthPlan.topTimberMaxBeam = breadthPlan.maxBeam * flareTumblehomePercentage;
        
        // Ensure dependent plans are updated
        sheerPlan.UpdateCachedValues();
        breadthPlan.UpdateCachedCurves();

        float keelY = sheerPlan.GetKeelYAtZ(0f);
        float hobY = sheerPlan.cachedHobMidshipPoint.y;
        float maxBeam = breadthPlan.maxBeam;
        float boxHeight = hobY - keelY;

        // Initialize last values if needed
        if (lastKeelArmLength < 0)
        {
            lastKeelArmLength = keelArmLength;
            lastKeelArmAngle = keelArmAngle;
            lastHobLowerArmLength = hobLowerArmLength;
            lastMiddleControlPosition = middleControlPosition;
        }

        float keelAngleRad = keelArmAngle * Mathf.Deg2Rad;
        float keelDirX = Mathf.Cos(keelAngleRad);
        float keelDirY = Mathf.Sin(keelAngleRad);

        float keelHalfWidth = keelWidth / 2f;
        float availableBeam = maxBeam - keelHalfWidth;

        float keelMaxDist;
        if (keelArmAngle >= 89.999f) keelMaxDist = boxHeight;
        else if (keelArmAngle <= 0.001f) keelMaxDist = availableBeam;
        else
        {
            float distToRight = availableBeam / keelDirX;
            float distToTop = boxHeight / keelDirY;
            keelMaxDist = Mathf.Min(distToRight, distToTop);
        }
        
        float p1y = keelY + keelDirY * (keelMaxDist * keelArmLength);
        float p2y = hobY - (boxHeight * hobLowerArmLength);

        bool keelChanged = (keelArmLength != lastKeelArmLength || keelArmAngle != lastKeelArmAngle);
        bool hobChanged = (hobLowerArmLength != lastHobLowerArmLength);

        if (enforceConvexHull && p1y > p2y)
        {
            if (hobChanged && !keelChanged)
            {
                if (keelDirY > 0.0001f && keelMaxDist > 0.0001f) keelArmLength = Mathf.Clamp01((p2y - keelY) / (keelMaxDist * keelDirY));
                else keelArmLength = 0f;
            }
            else
            {
                hobLowerArmLength = Mathf.Clamp01((hobY - p1y) / boxHeight);
            }
        }

        lastKeelArmLength = keelArmLength;
        lastKeelArmAngle = keelArmAngle;
        lastHobLowerArmLength = hobLowerArmLength;
        lastMiddleControlPosition = middleControlPosition;

        // Cache final points
        cachedL0 = new Vector3(keelWidth / 2f, keelY, 0);
        cachedL1 = cachedL0 + new Vector3(keelDirX, keelDirY, 0) * (keelMaxDist * keelArmLength);
        
        float hobDrop = boxHeight * hobLowerArmLength;
        cachedL3 = new Vector3(maxBeam, hobY, 0);
        cachedL2 = new Vector3(maxBeam, hobY - hobDrop, 0);

        cachedLMid = Vector3.Lerp(cachedL1, cachedL2, middleControlPosition);
        cachedL1Inner = Vector3.Lerp(cachedL0, cachedL1, innerKeelPosition);

        cachedUseQuartic = useQuarticBezier;
        cachedUseQuintic = useQuinticBezier;

        // UPPER MOULD
        // Find the absolute highest part of the hull for the master Upper Mould
        float topTimberY = sheerPlan.cachedBaseDeckMidshipPoint.y;
        if (sheerPlan.upperDecks != null && sheerPlan.upperDecks.Count > 0)
        {
            topTimberY += sheerPlan.GetMaxDeckHeight();
        }
        else
        {
            topTimberY = sheerPlan.cachedTopTimberMidshipPoint.y;
        }

        float topTimberMaxBeam = breadthPlan.topTimberMaxBeam;
        float upperBoxHeight = topTimberY - hobY;

        cachedU0 = cachedL3; // Starts at HoB
        cachedU3 = new Vector3(topTimberMaxBeam, topTimberY, 0);

        // U1 (HoB Upper Arm)
        float u1AngleRad = upperHobArmAngle * Mathf.Deg2Rad;
        float u1DirX = Mathf.Sin(u1AngleRad);
        float u1DirY = Mathf.Cos(u1AngleRad);
        
        float u1MaxDist;
        if (Mathf.Abs(u1DirX) < 0.0001f) u1MaxDist = upperBoxHeight;
        else if (Mathf.Abs(u1DirY) < 0.0001f) u1MaxDist = maxBeam; // Arbitrary wide limit
        else
        {
            float distToTop = upperBoxHeight / Mathf.Abs(u1DirY);
            float distToSide = maxBeam / Mathf.Abs(u1DirX);
            u1MaxDist = Mathf.Min(distToTop, distToSide);
        }
        cachedU1 = cachedU0 + new Vector3(u1DirX, u1DirY, 0) * (u1MaxDist * upperHobArmLength);

        // U2 (Top Timber Downward Arm)
        float u2AngleRad = topTimberArmAngle * Mathf.Deg2Rad;
        float u2DirX = Mathf.Sin(u2AngleRad);
        float u2DirY = -Mathf.Cos(u2AngleRad); // Points DOWN
        
        float u2MaxDist;
        if (Mathf.Abs(u2DirX) < 0.0001f) u2MaxDist = upperBoxHeight;
        else if (Mathf.Abs(u2DirY) < 0.0001f) u2MaxDist = topTimberMaxBeam;
        else
        {
            float distToBot = upperBoxHeight / Mathf.Abs(u2DirY);
            float distToSide = topTimberMaxBeam / Mathf.Abs(u2DirX);
            u2MaxDist = Mathf.Min(distToBot, distToSide);
        }
        cachedU2 = cachedU3 + new Vector3(u2DirX, u2DirY, 0) * (u2MaxDist * topTimberArmLength);
    }

    // --- MATHEMATICAL EVALUATION METHODS ---

    public float GetLowerMouldXAtY(float targetY)
    {
        float t = GetLowerMouldTAtY(targetY);
        if (cachedUseQuintic) return CalculateQuinticBezierPoint(t, cachedL0, cachedL1Inner, cachedL1, cachedLMid, cachedL2, cachedL3).x;
        else if (cachedUseQuartic) return CalculateQuarticBezierPoint(t, cachedL0, cachedL1, cachedLMid, cachedL2, cachedL3).x;
        else return CalculateCubicBezierPoint(t, cachedL0, cachedL1, cachedL2, cachedL3).x;
    }

    public float GetLowerMouldTAtY(float targetY)
    {
        float min = 0f;
        float max = 1f;
        for (int i = 0; i < 20; i++)
        {
            float mid = (min + max) / 2f;
            float y;
            if (cachedUseQuintic) y = CalculateQuinticBezierPoint(mid, cachedL0, cachedL1Inner, cachedL1, cachedLMid, cachedL2, cachedL3).y;
            else if (cachedUseQuartic) y = CalculateQuarticBezierPoint(mid, cachedL0, cachedL1, cachedLMid, cachedL2, cachedL3).y;
            else y = CalculateCubicBezierPoint(mid, cachedL0, cachedL1, cachedL2, cachedL3).y;

            if (y < targetY) min = mid;
            else max = mid;
        }
        return (min + max) / 2f;
    }

    public float GetUpperMouldXAtY(float targetY)
    {
        float t = GetUpperMouldTAtY(targetY);
        return CalculateCubicBezierPoint(t, cachedU0, cachedU1, cachedU2, cachedU3).x;
    }

    public float GetUpperMouldTAtY(float targetY)
    {
        float min = 0f;
        float max = 1f;
        for (int i = 0; i < 20; i++)
        {
            float mid = (min + max) / 2f;
            float y = CalculateCubicBezierPoint(mid, cachedU0, cachedU1, cachedU2, cachedU3).y;
            if (y < targetY) min = mid;
            else max = mid;
        }
        return (min + max) / 2f;
    }

    // --- UTILITIES ---

    private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * p0;
        p += 3 * uu * t * p1;
        p += 3 * u * tt * p2;
        p += ttt * p3;
        return p;
    }

    private Vector3 CalculateQuarticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
    {
        float u = 1 - t;
        float u2 = u * u;
        float u3 = u2 * u;
        float u4 = u3 * u;
        
        float t2 = t * t;
        float t3 = t2 * t;
        float t4 = t3 * t;

        Vector3 p = u4 * p0;
        p += 4 * u3 * t * p1;
        p += 6 * u2 * t2 * p2;
        p += 4 * u * t3 * p3;
        p += t4 * p4;
        return p;
    }

    private Vector3 CalculateQuinticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 p5)
    {
        float u = 1 - t;
        float u2 = u * u;
        float u3 = u2 * u;
        float u4 = u3 * u;
        float u5 = u4 * u;
        
        float t2 = t * t;
        float t3 = t2 * t;
        float t4 = t3 * t;
        float t5 = t4 * t;

        Vector3 p = u5 * p0;
        p += 5 * u4 * t * p1;
        p += 10 * u3 * t2 * p2;
        p += 10 * u2 * t3 * p3;
        p += 5 * u * t4 * p4;
        p += t5 * p5;
        return p;
    }

    // --- GIZMOS ---

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || breadthPlan == null) return;
        UpdateCachedCurves();

        // Draw Keel Cross-Section Block
        float keelHalfWidth = keelWidth / 2f;
        float keelTopY = sheerPlan.GetKeelYAtZ(0f);
        float keelThickness = sheerPlan.keelThickness;
        float keelBottomY = keelTopY - keelThickness;

        Gizmos.color = new Color(0.5f, 0.25f, 0f); // Brown
        Gizmos.DrawLine(new Vector3(-keelHalfWidth, keelTopY, 0), new Vector3(-keelHalfWidth, keelBottomY, 0));
        Gizmos.DrawLine(new Vector3(keelHalfWidth, keelTopY, 0), new Vector3(keelHalfWidth, keelBottomY, 0));
        Gizmos.DrawLine(new Vector3(-keelHalfWidth, keelBottomY, 0), new Vector3(keelHalfWidth, keelBottomY, 0));
        Gizmos.DrawLine(new Vector3(-keelHalfWidth, keelTopY, 0), new Vector3(keelHalfWidth, keelTopY, 0));

        // Draw Starboard (Green)
        DrawMirroredMould(1f, Color.green);

        // Draw Port (Red)
        DrawMirroredMould(-1f, Color.red);
    }

    protected void DrawMirroredMould(float flipX, Color curveColor)
    {
        // Lower Points
        Vector3 m0 = new Vector3(cachedL0.x * flipX, cachedL0.y, cachedL0.z);
        Vector3 m1Inner = new Vector3(cachedL1Inner.x * flipX, cachedL1Inner.y, cachedL1Inner.z);
        Vector3 m1 = new Vector3(cachedL1.x * flipX, cachedL1.y, cachedL1.z);
        Vector3 mMid = new Vector3(cachedLMid.x * flipX, cachedLMid.y, cachedLMid.z);
        Vector3 m2 = new Vector3(cachedL2.x * flipX, cachedL2.y, cachedL2.z);
        Vector3 m3 = new Vector3(cachedL3.x * flipX, cachedL3.y, cachedL3.z);

        // Upper Points
        Vector3 u0 = new Vector3(cachedU0.x * flipX, cachedU0.y, cachedU0.z);
        Vector3 u1 = new Vector3(cachedU1.x * flipX, cachedU1.y, cachedU1.z);
        Vector3 u2 = new Vector3(cachedU2.x * flipX, cachedU2.y, cachedU2.z);
        Vector3 u3 = new Vector3(cachedU3.x * flipX, cachedU3.y, cachedU3.z);

        // Draw Lower Curve
        if (cachedUseQuintic) DrawQuinticBezierCurve(m0, m1Inner, m1, mMid, m2, m3, curveColor, 40);
        else if (cachedUseQuartic) DrawQuarticBezierCurve(m0, m1, mMid, m2, m3, curveColor, 40);
        else DrawCubicBezierCurve(m0, m1, m2, m3, curveColor, 40);

        // Draw Upper Curve
        DrawCubicBezierCurve(u0, u1, u2, u3, new Color(0.8f, 0.8f, 1f), 40);

        // Draw Handles
        Gizmos.color = new Color(curveColor.r, curveColor.g, curveColor.b, 0.3f);
        if (cachedUseQuintic)
        {
            Gizmos.DrawLine(m0, m1Inner);
            Gizmos.DrawLine(m1Inner, m1);
        }
        else
        {
            Gizmos.DrawLine(m0, m1);
        }
        Gizmos.DrawLine(m1, mMid);
        Gizmos.DrawLine(mMid, m2);
        Gizmos.DrawLine(m2, m3);
        
        Gizmos.color = new Color(0.8f, 0.8f, 1f, 0.3f);
        Gizmos.DrawLine(u0, u1);
        Gizmos.DrawLine(u3, u2);

        // Draw Nodes
        Gizmos.color = Color.green;
        if (cachedUseQuintic) Gizmos.DrawSphere(m1Inner, 0.08f);
        Gizmos.DrawSphere(m1, 0.1f);
        Gizmos.DrawSphere(mMid, 0.1f);
        Gizmos.DrawSphere(m2, 0.1f);
        Gizmos.DrawSphere(u1, 0.1f);
        Gizmos.DrawSphere(u2, 0.1f);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(m0, 0.15f);
        Gizmos.DrawSphere(m3, 0.15f);
        Gizmos.DrawSphere(u3, 0.15f);

#if UNITY_EDITOR
        if (showLabels && flipX > 0) // Only draw labels on Starboard to prevent clutter
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            
            Handles.Label(m0, " Keel", labelStyle);
            Handles.Label(m3, " HoB Midship", labelStyle);
            Handles.Label(u3, " Top Timber Midship", labelStyle);
            if (cachedUseQuintic) Handles.Label(m1Inner, " Inner Keel Control", labelStyle);
            Handles.Label(m1, " Keel Control", labelStyle);
            Handles.Label(mMid, " Middle Control", labelStyle);
            Handles.Label(m2, " HoB Lower Control", labelStyle);
            Handles.Label(u1, " HoB Upper Control", labelStyle);
            Handles.Label(u2, " Top Timber Lower Control", labelStyle);
        }
#endif
    }

    protected void DrawCubicBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color, int segments)
    {
        Gizmos.color = color;
        Vector3 previousPoint = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }
    }

    protected void DrawQuarticBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Color color, int segments)
    {
        Gizmos.color = color;
        Vector3 previousPoint = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = CalculateQuarticBezierPoint(t, p0, p1, p2, p3, p4);
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }
    }

    protected void DrawQuinticBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 p5, Color color, int segments)
    {
        Gizmos.color = color;
        Vector3 previousPoint = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = CalculateQuinticBezierPoint(t, p0, p1, p2, p3, p4, p5);
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }
    }
}
