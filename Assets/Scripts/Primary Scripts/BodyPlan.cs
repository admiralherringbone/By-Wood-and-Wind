using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class BodyPlan : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan to get hull heights.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan to get max beam.")]
    public BreadthPlan breadthPlan;

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

    [Header("Rising Line Point")]
    [Tooltip("Position of the Rising Line Point along the line from Keel Control to HoB Control (0 = at Keel Control, 1 = at HoB Control).")]
    [Range(0f, 1f)]
    public float risingLinePosition = 0.5f;

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

    [Header("Inner Frame")]
    [Tooltip("Perpendicular thickness of the inner frame spline at the Keel.")]
    public float baseFrameThickness = 0.3f;
    [Tooltip("Perpendicular thickness of the inner frame spline at the Top Timber.")]
    public float topFrameThickness = 0.15f;
    public bool showInnerFrame = true;
    public Color innerFrameColor = Color.magenta;


    // --- BACKWARD COMPATIBILITY FIELDS (for legacy reference scripts) ---
    [HideInInspector] public bool useQuarticBezier = false;
    [HideInInspector] public bool useQuinticBezier = false;
    [HideInInspector] public float innerKeelPosition = 0.5f;
    [HideInInspector] public float middleControlPosition = 0.5f;


    // --- CACHED POINTS ---
    [HideInInspector] public Vector3 cachedL0;
    [HideInInspector] public Vector3 cachedL1;
    [HideInInspector] public Vector3 cachedLR;
    [HideInInspector] public Vector3 cachedL2;
    [HideInInspector] public Vector3 cachedL3;
    
    [HideInInspector] public Vector3 cachedU0;
    [HideInInspector] public Vector3 cachedU1;
    [HideInInspector] public Vector3 cachedU2;
    [HideInInspector] public Vector3 cachedU3;

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

        // Cache Lower Mould points
        // Segment 1: Keel (L0) → Keel Control (L1) → Rising Line Point (LR)
        cachedL0 = new Vector3(keelHalfWidth, keelY, 0);
        cachedL1 = cachedL0 + new Vector3(keelDirX, keelDirY, 0) * (keelMaxDist * keelArmLength);

        // Segment 2: Rising Line Point (LR) → HoB Control (L2) → HoB (L3)
        float hobDrop = boxHeight * hobLowerArmLength;
        cachedL3 = new Vector3(maxBeam, hobY, 0);
        cachedL2 = new Vector3(maxBeam, hobY - hobDrop, 0);

        // Rising Line Point — constrained to lie on the line from L1 to L2
        // This guarantees G1 tangent continuity at the join between the two quadratic segments
        cachedLR = Vector3.Lerp(cachedL1, cachedL2, risingLinePosition);

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
        return EvaluateLowerMouldAtT(t).x;
    }

    public float GetLowerMouldTAtY(float targetY)
    {
        float min = 0f;
        float max = 1f;
        for (int i = 0; i < 20; i++)
        {
            float mid = (min + max) / 2f;
            float y = EvaluateLowerMouldAtT(mid).y;

            if (y < targetY) min = mid;
            else max = mid;
        }
        return (min + max) / 2f;
    }

    /// <summary>
    /// Evaluates the lower mould at a unified t parameter [0, 1] spanning both quadratic segments.
    /// t [0, 0.5] = Segment 1 (Keel → Rising Line), t [0.5, 1] = Segment 2 (Rising Line → HoB).
    /// </summary>
    public Vector3 EvaluateLowerMouldAtT(float t)
    {
        if (t <= 0.5f)
        {
            float localT = t * 2f;
            return CalculateQuadraticBezierPoint(localT, cachedL0, cachedL1, cachedLR);
        }
        else
        {
            float localT = (t - 0.5f) * 2f;
            return CalculateQuadraticBezierPoint(localT, cachedLR, cachedL2, cachedL3);
        }
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

    private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        return (u * u * p0) + (2 * u * t * p1) + (t * t * p2);
    }

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
        Gizmos.DrawLine(new Vector3(-keelHalfWidth, keelBottomY, 0), new Vector3(-keelHalfWidth, keelBottomY, 0));
        Gizmos.DrawLine(new Vector3(-keelHalfWidth, keelTopY, 0), new Vector3(keelHalfWidth, keelTopY, 0));

        // Draw Starboard (Green)
        DrawMirroredMould(1f, Color.green);
        if (showInnerFrame) DrawInnerMirroredMould(1f, innerFrameColor);

        // Draw Port (Red)
        DrawMirroredMould(-1f, Color.red);
        if (showInnerFrame) DrawInnerMirroredMould(-1f, innerFrameColor);
    }

    protected void DrawMirroredMould(float flipX, Color curveColor)
    {
        // Lower Points
        Vector3 m0 = new Vector3(cachedL0.x * flipX, cachedL0.y, cachedL0.z);
        Vector3 m1 = new Vector3(cachedL1.x * flipX, cachedL1.y, cachedL1.z);
        Vector3 mR = new Vector3(cachedLR.x * flipX, cachedLR.y, cachedLR.z);
        Vector3 m2 = new Vector3(cachedL2.x * flipX, cachedL2.y, cachedL2.z);
        Vector3 m3 = new Vector3(cachedL3.x * flipX, cachedL3.y, cachedL3.z);

        // Upper Points
        Vector3 u0 = new Vector3(cachedU0.x * flipX, cachedU0.y, cachedU0.z);
        Vector3 u1 = new Vector3(cachedU1.x * flipX, cachedU1.y, cachedU1.z);
        Vector3 u2 = new Vector3(cachedU2.x * flipX, cachedU2.y, cachedU2.z);
        Vector3 u3 = new Vector3(cachedU3.x * flipX, cachedU3.y, cachedU3.z);

        // Draw Lower Curves (Two Quadratic Segments)
        DrawQuadraticBezierCurve(m0, m1, mR, curveColor, 20);
        DrawQuadraticBezierCurve(mR, m2, m3, curveColor, 20);

        // Draw Upper Curve
        DrawCubicBezierCurve(u0, u1, u2, u3, new Color(0.8f, 0.8f, 1f), 40);

        // Draw Handles
        Gizmos.color = new Color(curveColor.r, curveColor.g, curveColor.b, 0.3f);
        Gizmos.DrawLine(m0, m1);
        Gizmos.DrawLine(m1, mR);
        Gizmos.DrawLine(mR, m2);
        Gizmos.DrawLine(m2, m3);
        
        Gizmos.color = new Color(0.8f, 0.8f, 1f, 0.3f);
        Gizmos.DrawLine(u0, u1);
        Gizmos.DrawLine(u3, u2);

        // Draw Nodes
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(m1, 0.1f);
        Gizmos.DrawSphere(m2, 0.1f);
        Gizmos.DrawSphere(u1, 0.1f);
        Gizmos.DrawSphere(u2, 0.1f);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(m0, 0.15f);
        Gizmos.DrawSphere(mR, 0.15f);
        Gizmos.DrawSphere(m3, 0.15f);
        Gizmos.DrawSphere(u3, 0.15f);

#if UNITY_EDITOR
        if (showLabels && flipX > 0) // Only draw labels on Starboard to prevent clutter
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            
            Handles.Label(m0, " Keel", labelStyle);
            Handles.Label(mR, " Rising Line", labelStyle);
            Handles.Label(m3, " HoB Midship", labelStyle);
            Handles.Label(u3, " Top Timber Midship", labelStyle);
            Handles.Label(m1, " Keel Control", labelStyle);
            Handles.Label(m2, " HoB Lower Control", labelStyle);
            Handles.Label(u1, " HoB Upper Control", labelStyle);
            Handles.Label(u2, " Top Timber Lower Control", labelStyle);
        }
#endif
    }

    protected void DrawQuadraticBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Color color, int segments)
    {
        Gizmos.color = color;
        Vector3 previousPoint = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = CalculateQuadraticBezierPoint(t, p0, p1, p2);
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }
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

    protected void DrawInnerMirroredMould(float flipX, Color curveColor)
    {
        Gizmos.color = curveColor;

        List<Vector2> rawPoints = new List<Vector2>();
        int segments = 100;

        float topTimberY = cachedU3.y;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            
            // Re-parameterize t across Lower (0-0.5) and Upper (0.5-1.0) curves
            Vector2 outerPt;
            Vector2 nextPt;
            
            float rawT = t * 2f;
            if (rawT <= 1f)
            {
                outerPt = GetLowerMouldPoint(rawT, flipX);
                nextPt = GetLowerMouldPoint(Mathf.Min(1f, rawT + 0.01f), flipX);
            }
            else
            {
                rawT -= 1f;
                outerPt = GetUpperMouldPoint(rawT, flipX);
                nextPt = GetUpperMouldPoint(Mathf.Min(1f, rawT + 0.01f), flipX);
            }

            Vector2 tangent = (nextPt - outerPt).normalized;
            if (tangent == Vector2.zero) tangent = Vector2.up; // Fallback
            
            // Calculate normal pointing inwards. 
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            
            // Ensure normal always points towards the centerline
            if (flipX > 0 && normal.x > 0) normal = -normal;
            if (flipX < 0 && normal.x < 0) normal = -normal;

            float currentThickness = Mathf.Lerp(baseFrameThickness, topFrameThickness, t);
            Vector2 innerPt = outerPt + normal * currentThickness;
            rawPoints.Add(innerPt);
        }

        // Remove self-intersections (swallowtail loops)
        bool intersectionFound = true;
        while (intersectionFound)
        {
            intersectionFound = false;
            for (int i = 0; i < rawPoints.Count - 3; i++)
            {
                for (int j = i + 2; j < rawPoints.Count - 1; j++)
                {
                    if (LineIntersection(rawPoints[i], rawPoints[i+1], rawPoints[j], rawPoints[j+1], out Vector2 intersect))
                    {
                        int removeCount = j - i;
                        rawPoints.RemoveRange(i + 1, removeCount);
                        rawPoints.Insert(i + 1, intersect);
                        intersectionFound = true;
                        break;
                    }
                }
                if (intersectionFound) break;
            }
        }

        Vector3 previousPoint = Vector3.zero;
        bool first = true;

        for (int i = 0; i < rawPoints.Count; i++)
        {
            Vector3 point = new Vector3(rawPoints[i].x, rawPoints[i].y, 0f);

            // Clamp to Top Timber
            if (point.y >= topTimberY)
            {
                point.y = topTimberY;
                if (!first) Gizmos.DrawLine(previousPoint, point);
                break;
            }

            if (!first)
            {
                Gizmos.DrawLine(previousPoint, point);
            }
            
            previousPoint = point;
            first = false;
        }
    }
    
    private Vector2 GetLowerMouldPoint(float t, float flipX)
    {
        // t [0, 1] spans both quadratic segments
        Vector3 pt;
        if (t <= 0.5f)
        {
            float localT = t * 2f;
            pt = CalculateQuadraticBezierPoint(localT, cachedL0, cachedL1, cachedLR);
        }
        else
        {
            float localT = (t - 0.5f) * 2f;
            pt = CalculateQuadraticBezierPoint(localT, cachedLR, cachedL2, cachedL3);
        }
        return new Vector2(pt.x * flipX, pt.y);
    }
    
    private Vector2 GetUpperMouldPoint(float t, float flipX)
    {
        Vector3 pt = CalculateCubicBezierPoint(t, cachedU0, cachedU1, cachedU2, cachedU3);
        return new Vector2(pt.x * flipX, pt.y);
    }

    private bool LineIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersection)
    {
        intersection = Vector2.zero;
        float d = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);
        if (d == 0f) return false;
        
        float u = ((p3.x - p1.x) * (p4.y - p3.y) - (p3.y - p1.y) * (p4.x - p3.x)) / d;
        float v = ((p3.x - p1.x) * (p2.y - p1.y) - (p3.y - p1.y) * (p2.x - p1.x)) / d;
        
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false;
        
        intersection.x = p1.x + u * (p2.x - p1.x);
        intersection.y = p1.y + u * (p2.y - p1.y);
        return true;
    }
}
