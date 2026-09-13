using System.Collections.Generic;
using UnityEngine;

public enum StrakeBeltType
{
    FloorGarboard,    // P_L0 -> P_floor_mark (First half of lower mould curve 1)
    Bilge,            // P_floor_mark -> P_LR_raw (Second half of lower mould curve 1)
    BottomTopsides,   // P_LR_raw -> P_L3 (Lower mould curve 2)
    UpperTopsides     // P_L3 -> P_sheer (Upper mould cubic curve)
}

[ExecuteInEditMode]
public class StrakesModel : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FashionTimberModel fashionTimberModel;

    [Header("Strake Visualization Settings")]
    [Tooltip("Show 3D planking strakes shrink-wrapped to frame surface cross-sections.")]
    public bool showStrakes = true;

    [Tooltip("Show 2D Z/Y Profile Guide lines in side view.")]
    public bool show2DProfileGuides = true;

    [Tooltip("Cut strake lines and profile guides at the Sheer Plan boundaries (Stem profile curve, Stern profile curve, and Top Sheer line).")]
    public bool cutStrakesToSheerPlan = true;

    [Tooltip("X offset for 2D Profile plane. Set to 0 for Centerline (Sheer Plan), or offset to side (e.g. 2.0).")]
    public float profilePlaneXOffset = 0f;

    [Header("Strake Belt Counts")]
    [Range(1, 15)]
    [Tooltip("Number of strakes in Floor/Garboard Belt (yellow pL0 Keel -> orange pFloorMark Midpoint).")]
    public int floorGarboardStrakeCount = 3;

    [Range(1, 15)]
    [Tooltip("Number of strakes in Bilge Belt (orange pFloorMark Midpoint -> green pLR Rising Line).")]
    public int bilgeStrakeCount = 3;

    [Range(1, 15)]
    [Tooltip("Number of strakes in Bottom Topsides Belt (green pLR Rising Line -> cyan pL3 HOB).")]
    public int bottomTopsidesStrakeCount = 4;

    [Range(1, 15)]
    [Tooltip("Number of strakes in Upper Topsides Belt (cyan pL3 HOB -> pSheer Top Sheer).")]
    public int upperTopsidesStrakeCount = 4;

    [Header("Frame Perpendicular Settings")]
    [Tooltip("Show normal vectors (perpendiculars) extending outward from each frame surface.")]
    public bool showFramePerpendiculars = false;

    [Range(1, 30)]
    public int strakeCount = 10;

    [Tooltip("Physical thickness of the planking strakes (offset along frame perpendiculars).")]
    public float plankThickness = 0.1f;

    [Tooltip("Length of frame perpendicular gizmo rays.")]
    public float perpendicularLength = 0.3f;

    [Header("Strake Belt Colors")]
    public Color floorGarboardColor = new Color(1f, 0.8f, 0.2f, 0.9f);  // Amber/Gold
    public Color bilgeColor = new Color(1f, 0.5f, 0f, 0.9f);          // Orange
    public Color bottomTopsidesColor = new Color(0.2f, 0.8f, 1f, 0.9f);  // Cyan
    public Color upperTopsidesColor = new Color(1f, 0.3f, 0.8f, 0.9f);   // Magenta
    public Color perpendicularColor = Color.green;

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        FindReferences();
    }

    public void FindReferences()
    {
        if (sheerPlan == null) sheerPlan = GetComponentInParent<SheerPlan>();
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();

        if (bodyPlan == null) bodyPlan = GetComponentInParent<BodyPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();

        if (breadthPlan == null) breadthPlan = GetComponentInParent<BreadthPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();

        if (framingModel == null) framingModel = GetComponentInParent<FramingModel>();
        if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();

        if (fashionTimberModel == null) fashionTimberModel = GetComponentInParent<FashionTimberModel>();
        if (fashionTimberModel == null) fashionTimberModel = FindFirstObjectByType<FashionTimberModel>();
    }

    public float GetKeelHalfWidth()
    {
        if (bodyPlan != null) return bodyPlan.keelWidth / 2f;
        if (framingModel != null) return framingModel.GetKeelHalfWidth();
        return 0.25f;
    }

    public float GetFrameBottomYAtZ(float z)
    {
        if (framingModel != null) return framingModel.GetFrameBottomYAtZ(z);
        if (sheerPlan != null) return sheerPlan.GetKeelYAtZ(z);
        return 0f;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null) return;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        breadthPlan.UpdateCachedCurves();

        if (show2DProfileGuides)
        {
            Draw2DProfileGuides();
        }

        if (showFramePerpendiculars)
        {
            DrawFramePerpendiculars();
        }

        if (showStrakes)
        {
            DrawStrakes();
        }
    }

    private List<float> GetZPoints()
    {
        if (framingModel != null)
        {
            return framingModel.CalculateFrameStations();
        }

        List<float> stations = new List<float>();
        if (sheerPlan == null) return stations;

        float aftZ = sheerPlan.GetAftMostZ();
        float fwdZ = sheerPlan.GetFwdMostZ();

        int uniformCount = 80;
        for (int i = 0; i < uniformCount; i++)
        {
            float z = Mathf.Lerp(aftZ, fwdZ, i / (float)(uniformCount - 1));
            stations.Add(z);
        }

        return stations;
    }

    public bool GetPureFrameControlPoints(float z, float side,
        out Vector3 pL0, out Vector3 pL1_raw, out Vector3 pLR_raw, out Vector3 pFloorMark,
        out Vector3 pL2, out Vector3 pL3,
        out Vector3 pU0, out Vector3 pU1, out Vector3 pU2, out Vector3 pU3)
    {
        pL0 = pL1_raw = pLR_raw = pFloorMark = pL2 = pL3 = pU0 = pU1 = pU2 = pU3 = Vector3.zero;
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null) return false;

        float hw = GetKeelHalfWidth();
        float keelY = GetFrameBottomYAtZ(z);
        float originalKeelY = keelY;

        float hobY = sheerPlan.GetTheoreticalHobYAtZ(z);
        float hobBreadth = breadthPlan.GetTheoreticalBreadthXAtZ(z);
        float xTop = breadthPlan.GetTopTimberXAtZ(z);

        float maxSheerYAtZ = sheerPlan.GetPureSheerYAtZ(z);

        // UPPER MOULD SEARCH
        pU0 = new Vector3(side * hobBreadth, hobY, z);
        pU3 = new Vector3(side * xTop, maxSheerYAtZ, z);

        float masterUpperDx = bodyPlan.cachedU3.x - bodyPlan.cachedU0.x;
        float masterUpperDy = bodyPlan.cachedU3.y - bodyPlan.cachedU0.y;

        float localUpperDx = xTop - hobBreadth;
        float localUpperDy = maxSheerYAtZ - hobY;

        float sXUpper = Mathf.Abs(masterUpperDx) > 0.001f ? localUpperDx / masterUpperDx : 0f;
        float sYUpper = Mathf.Abs(masterUpperDy) > 0.001f ? localUpperDy / masterUpperDy : 0f;

        Vector3 masterU1Offset = bodyPlan.cachedU1 - bodyPlan.cachedU0;
        Vector3 masterU2Offset = bodyPlan.cachedU2 - bodyPlan.cachedU3;

        pU1 = pU0 + new Vector3(side * masterU1Offset.x * sXUpper, masterU1Offset.y * sYUpper, 0);
        pU2 = pU3 + new Vector3(side * masterU2Offset.x * sXUpper, masterU2Offset.y * sYUpper, 0);

        // LOWER MOULD SEARCH (STRICTLY PURE: NO PSEUDO KEEL ADJUSTMENTS)
        float xBot = hw;
        pL0 = new Vector3(side * xBot, keelY, z);
        pL3 = new Vector3(side * hobBreadth, hobY, z);

        Vector3 masterL1 = bodyPlan.cachedL1;
        Vector3 masterLR = bodyPlan.cachedLR;

        float targetL1_X = hw;
        float targetL1_Y = originalKeelY;
        float targetLR_X = hw;
        float targetLR_Y = originalKeelY;

        if (z >= 0)
        {
            float bowZ = sheerPlan.cachedBowRisingEnd.z;
            if (bowZ > 0.001f)
            {
                float t = sheerPlan.GetRisingLineTAtZ(z);
                Vector3 bowEnd = new Vector3(hw, sheerPlan.cachedBowRisingEnd.y, sheerPlan.cachedBowRisingEnd.z);

                Vector3 p0_L1 = masterL1;
                Vector3 p1_L1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedBowP1Ctrl.z);
                Vector3 p2_L1 = bowEnd;
                targetL1_X = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).x;
                targetL1_Y = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).y;

                Vector3 p0_LR = masterLR;
                Vector3 p1_LR = new Vector3(masterLR.x, masterLR.y, sheerPlan.cachedBowP1Ctrl.z);
                Vector3 p2_LR = bowEnd;
                targetLR_X = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).x;
                targetLR_Y = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).y;
            }
        }
        else
        {
            float sternZ = sheerPlan.cachedSternRisingEnd.z;
            if (sternZ < -0.001f)
            {
                Vector3 sternEnd = new Vector3(hw, sheerPlan.cachedSternRisingEnd.y, sheerPlan.cachedSternRisingEnd.z);

                Vector3 p0_L1 = masterL1;
                Vector3 p1_L1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedSternP1Ctrl.z);
                Vector3 p2_L1 = sternEnd;

                Vector3 p0_LR = masterLR;
                Vector3 p1_LR = new Vector3(masterLR.x, masterLR.y, sheerPlan.cachedSternP1Ctrl.z);
                Vector3 p2_LR = sternEnd;

                if (z < sternZ)
                {
                    float dzL1 = p2_L1.z - p1_L1.z;
                    if (Mathf.Abs(dzL1) > 0.0001f)
                    {
                        float ratio = (z - p2_L1.z) / dzL1;
                        targetL1_X = p2_L1.x;
                        targetL1_Y = p2_L1.y + (p2_L1.y - p1_L1.y) * ratio;
                    }
                    else
                    {
                        targetL1_X = p2_L1.x;
                        targetL1_Y = p2_L1.y;
                    }

                    float dzLR = p2_LR.z - p1_LR.z;
                    if (Mathf.Abs(dzLR) > 0.0001f)
                    {
                        float ratio = (z - p2_LR.z) / dzLR;
                        targetLR_X = p2_LR.x;
                        targetLR_Y = p2_LR.y + (p2_LR.y - p1_LR.y) * ratio;
                    }
                    else
                    {
                        targetLR_X = p2_LR.x;
                        targetLR_Y = p2_LR.y;
                    }
                }
                else
                {
                    float t = sheerPlan.GetRisingLineTAtZ(z);
                    targetL1_X = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).x;
                    targetL1_Y = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).y;

                    targetLR_X = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).x;
                    targetLR_Y = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).y;
                }
            }
        }

        targetL1_Y = Mathf.Max(targetL1_Y, originalKeelY);
        targetLR_Y = Mathf.Max(targetLR_Y, originalKeelY);

        pL1_raw = new Vector3(side * targetL1_X, targetL1_Y, z);

        float currentBoxHeight = hobY - originalKeelY;
        float hobDrop = currentBoxHeight * bodyPlan.hobLowerArmLength;
        pL2 = new Vector3(side * hobBreadth, Mathf.Max(hobY - hobDrop, targetLR_Y), z);

        float dx = Mathf.Abs(pL2.x - pL1_raw.x);
        float lrPosRatio = bodyPlan.risingLinePosition;
        if (dx > 0.0001f)
        {
            lrPosRatio = Mathf.Clamp01((targetLR_X - targetL1_X) / dx);
        }
        pLR_raw = Vector3.Lerp(pL1_raw, pL2, lrPosRatio);

        // Floor Mark Point (50% arc length between yellow pL0 and green pLR_raw)
        pFloorMark = GetQuadraticBezierPointByArcLength(0.5f, pL0, pL1_raw, pLR_raw);

        return true;
    }

    public Vector3? GetPointOnFrameBelt(float z, float side, StrakeBeltType belt, float fraction)
    {
        Vector3 pL0, pL1_raw, pLR_raw, pFloorMark, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!GetPureFrameControlPoints(z, side, out pL0, out pL1_raw, out pLR_raw, out pFloorMark, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return null;

        fraction = Mathf.Clamp01(fraction);

        switch (belt)
        {
            case StrakeBeltType.FloorGarboard:
                return GetQuadraticBezierPointByArcLength(fraction * 0.5f, pL0, pL1_raw, pLR_raw);

            case StrakeBeltType.Bilge:
                return GetQuadraticBezierPointByArcLength(0.5f + fraction * 0.5f, pL0, pL1_raw, pLR_raw);

            case StrakeBeltType.BottomTopsides:
                return GetQuadraticBezierPointByArcLength(fraction, pLR_raw, pL2, pL3);

            case StrakeBeltType.UpperTopsides:
                return GetCubicBezierPointByArcLength(fraction, pU0, pU1, pU2, pU3);

            default:
                return null;
        }
    }

    public bool GetFramePointAndNormal(float z, float side, float girthFraction, out Vector3 surfacePoint, out Vector3 normal)
    {
        surfacePoint = Vector3.zero;
        normal = Vector3.up;

        Vector3 pL0, pL1_raw, pLR_raw, pFloorMark, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!GetPureFrameControlPoints(z, side, out pL0, out pL1_raw, out pLR_raw, out pFloorMark, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return false;

        float lenBelt1 = ApproximateQuadraticLength(pL0, pL1_raw, pLR_raw) * 0.5f;
        float lenBelt2 = lenBelt1;
        float lenBelt3 = ApproximateQuadraticLength(pLR_raw, pL2, pL3);
        float lenBelt4 = ApproximateCubicLength(pU0, pU1, pU2, pU3);

        float totalLength = lenBelt1 + lenBelt2 + lenBelt3 + lenBelt4;
        if (totalLength <= 0.0001f) return false;

        float targetDist = Mathf.Clamp01(girthFraction) * totalLength;
        Vector3 tangent;

        if (targetDist <= lenBelt1)
        {
            float u = lenBelt1 > 0.0001f ? (targetDist / lenBelt1) * 0.5f : 0f;
            surfacePoint = CalculateQuadraticBezierPoint(u, pL0, pL1_raw, pLR_raw);
            tangent = CalculateQuadraticBezierDerivative(u, pL0, pL1_raw, pLR_raw);
        }
        else if (targetDist <= lenBelt1 + lenBelt2)
        {
            float u = lenBelt2 > 0.0001f ? 0.5f + ((targetDist - lenBelt1) / lenBelt2) * 0.5f : 0.5f;
            surfacePoint = CalculateQuadraticBezierPoint(u, pL0, pL1_raw, pLR_raw);
            tangent = CalculateQuadraticBezierDerivative(u, pL0, pL1_raw, pLR_raw);
        }
        else if (targetDist <= lenBelt1 + lenBelt2 + lenBelt3)
        {
            float u = lenBelt3 > 0.0001f ? (targetDist - lenBelt1 - lenBelt2) / lenBelt3 : 0f;
            surfacePoint = CalculateQuadraticBezierPoint(u, pLR_raw, pL2, pL3);
            tangent = CalculateQuadraticBezierDerivative(u, pLR_raw, pL2, pL3);
        }
        else
        {
            float u = lenBelt4 > 0.0001f ? (targetDist - lenBelt1 - lenBelt2 - lenBelt3) / lenBelt4 : 0f;
            surfacePoint = CalculateCubicBezierPoint(u, pU0, pU1, pU2, pU3);
            tangent = CalculateCubicBezierDerivative(u, pU0, pU1, pU2, pU3);
        }

        Vector2 t2D = new Vector2(tangent.x, tangent.y);
        if (t2D.sqrMagnitude > 0.000001f)
        {
            t2D.Normalize();
            Vector2 n2D = new Vector2(-side * t2D.y, side * t2D.x).normalized;
            normal = new Vector3(n2D.x, n2D.y, 0f);
        }
        else
        {
            normal = new Vector3(-Mathf.Sign(side), 0f, 0f);
        }
        return true;
    }

    private void DrawFramePerpendiculars()
    {
        List<float> zPoints = GetZPoints();
        Gizmos.color = perpendicularColor;

        foreach (float z in zPoints)
        {
            for (int k = 0; k <= strakeCount; k++)
            {
                float girthT = (float)k / strakeCount;

                Vector3 ptStbd, nStbd;
                if (GetFramePointAndNormal(z, 1f, girthT, out ptStbd, out nStbd))
                {
                    Gizmos.color = perpendicularColor;
                    Gizmos.DrawRay(ptStbd, nStbd * perpendicularLength);
                    Gizmos.DrawSphere(ptStbd, 0.03f);
                }

                Vector3 ptPort, nPort;
                if (GetFramePointAndNormal(z, -1f, girthT, out ptPort, out nPort))
                {
                    Gizmos.color = perpendicularColor;
                    Gizmos.DrawRay(ptPort, nPort * perpendicularLength);
                    Gizmos.DrawSphere(ptPort, 0.03f);
                }
            }
        }
    }

    public bool IsPointInsideSheerPlan(Vector3 pt)
    {
        if (sheerPlan == null) return true;

        // 1. Top Sheer & Upper Deck Edge Limit
        float maxSheerY = sheerPlan.GetTopTimberYAtZ(pt.z);
        if (pt.y > maxSheerY + 0.001f) return false;

        // 2. Bow Stem Limit
        if (pt.z > 0)
        {
            float stemZ = sheerPlan.GetStemProfileZAtY(pt.y);
            if (pt.z > stemZ + 0.001f) return false;
        }

        // 3. Stern Profile Limit
        if (pt.z < 0)
        {
            float sternZ = sheerPlan.GetSternProfileZAtY(pt.y);
            if (pt.z < sternZ - 0.001f) return false;
        }

        return true;
    }

    private void DrawClippedLine(Vector3 pA, Vector3 pB)
    {
        if (!cutStrakesToSheerPlan || sheerPlan == null)
        {
            Gizmos.DrawLine(pA, pB);
            return;
        }

        bool insideA = IsPointInsideSheerPlan(pA);
        bool insideB = IsPointInsideSheerPlan(pB);

        if (insideA && insideB)
        {
            Gizmos.DrawLine(pA, pB);
        }
        else if (insideA && !insideB)
        {
            Vector3 clippedB = ClipPointToSheerPlanBoundary(pA, pB);
            Gizmos.DrawLine(pA, clippedB);
        }
        else if (!insideA && insideB)
        {
            Vector3 clippedA = ClipPointToSheerPlanBoundary(pB, pA);
            Gizmos.DrawLine(clippedA, pB);
        }
    }

    private Vector3 ClipPointToSheerPlanBoundary(Vector3 insidePt, Vector3 outsidePt)
    {
        Vector3 low = insidePt;
        Vector3 high = outsidePt;

        for (int i = 0; i < 12; i++)
        {
            Vector3 mid = (low + high) * 0.5f;
            if (IsPointInsideSheerPlan(mid))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }
        return (low + high) * 0.5f;
    }

    private void Draw2DProfileGuides()
    {
        List<float> zPoints = GetZPoints();
        if (zPoints.Count < 2) return;

        float xOffset = profilePlaneXOffset;

        DrawBelt2DProfileGuides(StrakeBeltType.FloorGarboard, floorGarboardStrakeCount, floorGarboardColor, zPoints, xOffset, true);
        DrawBelt2DProfileGuides(StrakeBeltType.Bilge, bilgeStrakeCount, bilgeColor, zPoints, xOffset, false);
        DrawBelt2DProfileGuides(StrakeBeltType.BottomTopsides, bottomTopsidesStrakeCount, bottomTopsidesColor, zPoints, xOffset, false);
        DrawBelt2DProfileGuides(StrakeBeltType.UpperTopsides, upperTopsidesStrakeCount, upperTopsidesColor, zPoints, xOffset, false);
    }

    private void DrawBelt2DProfileGuides(StrakeBeltType belt, int count, Color color, List<float> zPoints, float xOffset, bool includeFirstBoundary)
    {
        if (count <= 0) return;

        Gizmos.color = color;
        int startIndex = includeFirstBoundary ? 0 : 1;

        for (int k = startIndex; k <= count; k++)
        {
            float fraction = (float)k / count;

            Vector3? prevStbdPt = null;
            Vector3? prevPortPt = null;
            Vector3? prevCenterPt = null;

            for (int i = 0; i < zPoints.Count; i++)
            {
                float z = zPoints[i];
                Vector3? framePt = GetPointOnFrameBelt(z, 1f, belt, fraction);
                if (!framePt.HasValue) continue;

                float y = framePt.Value.y;

                if (Mathf.Abs(xOffset) < 0.001f)
                {
                    Vector3 centerPt = new Vector3(0f, y, z);
                    if (!cutStrakesToSheerPlan || IsPointInsideSheerPlan(centerPt))
                    {
                        Gizmos.DrawSphere(centerPt, 0.025f);
                    }

                    if (prevCenterPt.HasValue)
                    {
                        DrawClippedLine(prevCenterPt.Value, centerPt);
                    }
                    prevCenterPt = centerPt;
                }
                else
                {
                    Vector3 stbdPt = new Vector3(Mathf.Abs(xOffset), y, z);
                    Vector3 portPt = new Vector3(-Mathf.Abs(xOffset), y, z);

                    if (!cutStrakesToSheerPlan || IsPointInsideSheerPlan(stbdPt)) Gizmos.DrawSphere(stbdPt, 0.025f);
                    if (!cutStrakesToSheerPlan || IsPointInsideSheerPlan(portPt)) Gizmos.DrawSphere(portPt, 0.025f);

                    if (prevStbdPt.HasValue) DrawClippedLine(prevStbdPt.Value, stbdPt);
                    if (prevPortPt.HasValue) DrawClippedLine(prevPortPt.Value, portPt);

                    prevStbdPt = stbdPt;
                    prevPortPt = portPt;
                }
            }
        }
    }

    private void DrawStrakes()
    {
        if (bodyPlan == null || sheerPlan == null || breadthPlan == null) return;

        List<float> zPoints = GetZPoints();
        if (zPoints.Count < 2) return;

        DrawBeltStrakes(StrakeBeltType.FloorGarboard, floorGarboardStrakeCount, floorGarboardColor, zPoints, true);
        DrawBeltStrakes(StrakeBeltType.Bilge, bilgeStrakeCount, bilgeColor, zPoints, false);
        DrawBeltStrakes(StrakeBeltType.BottomTopsides, bottomTopsidesStrakeCount, bottomTopsidesColor, zPoints, false);
        DrawBeltStrakes(StrakeBeltType.UpperTopsides, upperTopsidesStrakeCount, upperTopsidesColor, zPoints, false);
    }

    private void DrawBeltStrakes(StrakeBeltType belt, int count, Color color, List<float> zPoints, bool includeFirstBoundary)
    {
        if (count <= 0) return;

        Gizmos.color = color;
        int startIndex = includeFirstBoundary ? 0 : 1;

        for (int k = startIndex; k <= count; k++)
        {
            float fraction = (float)k / count;

            Vector3? prevStbd = null;
            Vector3? prevPort = null;

            for (int i = 0; i < zPoints.Count; i++)
            {
                float z = zPoints[i];

                Vector3? currStbd = GetPointOnFrameBelt(z, 1f, belt, fraction);
                Vector3? currPort = GetPointOnFrameBelt(z, -1f, belt, fraction);

                if (currStbd.HasValue && prevStbd.HasValue)
                {
                    DrawClippedLine(prevStbd.Value, currStbd.Value);
                }
                if (currPort.HasValue && prevPort.HasValue)
                {
                    DrawClippedLine(prevPort.Value, currPort.Value);
                }

                prevStbd = currStbd;
                prevPort = currPort;
            }
        }
    }

    private float ApproximateQuadraticLength(Vector3 p0, Vector3 p1, Vector3 p2, int samples = 6)
    {
        float len = 0f;
        Vector3 prev = p0;
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 curr = CalculateQuadraticBezierPoint(t, p0, p1, p2);
            len += Vector3.Distance(prev, curr);
            prev = curr;
        }
        return len;
    }

    private float ApproximateCubicLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples = 6)
    {
        float len = 0f;
        Vector3 prev = p0;
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 curr = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            len += Vector3.Distance(prev, curr);
            prev = curr;
        }
        return len;
    }

    private Vector3 GetQuadraticBezierPointByArcLength(float targetFraction, Vector3 p0, Vector3 p1, Vector3 p2, int samples = 20)
    {
        targetFraction = Mathf.Clamp01(targetFraction);
        if (targetFraction <= 0f) return p0;
        if (targetFraction >= 1f) return p2;

        float[] cumulativeLengths = new float[samples + 1];
        Vector3 prev = p0;
        float totalLength = 0f;
        cumulativeLengths[0] = 0f;

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 curr = CalculateQuadraticBezierPoint(t, p0, p1, p2);
            totalLength += Vector3.Distance(prev, curr);
            cumulativeLengths[i] = totalLength;
            prev = curr;
        }

        if (totalLength < 0.0001f) return p0;

        float targetLength = targetFraction * totalLength;

        for (int i = 0; i < samples; i++)
        {
            if (targetLength >= cumulativeLengths[i] && targetLength <= cumulativeLengths[i + 1])
            {
                float segmentLen = cumulativeLengths[i + 1] - cumulativeLengths[i];
                float segFraction = segmentLen > 0.0001f ? (targetLength - cumulativeLengths[i]) / segmentLen : 0f;
                float tLow = i / (float)samples;
                float tHigh = (i + 1) / (float)samples;
                float t = Mathf.Lerp(tLow, tHigh, segFraction);
                return CalculateQuadraticBezierPoint(t, p0, p1, p2);
            }
        }

        return p2;
    }

    private Vector3 GetCubicBezierPointByArcLength(float targetFraction, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples = 20)
    {
        targetFraction = Mathf.Clamp01(targetFraction);
        if (targetFraction <= 0f) return p0;
        if (targetFraction >= 1f) return p3;

        float[] cumulativeLengths = new float[samples + 1];
        Vector3 prev = p0;
        float totalLength = 0f;
        cumulativeLengths[0] = 0f;

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 curr = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            totalLength += Vector3.Distance(prev, curr);
            cumulativeLengths[i] = totalLength;
            prev = curr;
        }

        if (totalLength < 0.0001f) return p0;

        float targetLength = targetFraction * totalLength;

        for (int i = 0; i < samples; i++)
        {
            if (targetLength >= cumulativeLengths[i] && targetLength <= cumulativeLengths[i + 1])
            {
                float segmentLen = cumulativeLengths[i + 1] - cumulativeLengths[i];
                float segFraction = segmentLen > 0.0001f ? (targetLength - cumulativeLengths[i]) / segmentLen : 0f;
                float tLow = i / (float)samples;
                float tHigh = (i + 1) / (float)samples;
                float t = Mathf.Lerp(tLow, tHigh, segFraction);
                return CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            }
        }

        return p3;
    }

    private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        return (u * u * p0) + (2 * u * t * p1) + (t * t * p2);
    }

    private Vector3 CalculateQuadraticBezierDerivative(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        return 2 * u * (p1 - p0) + 2 * t * (p2 - p1);
    }

    private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        return (u * u * u * p0) + (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t * p3);
    }

    private Vector3 CalculateCubicBezierDerivative(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        return 3 * u * u * (p1 - p0) + 6 * u * t * (p2 - p1) + 3 * t * t * (p3 - p2);
    }
}
