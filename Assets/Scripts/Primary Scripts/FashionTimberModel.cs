using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class FashionTimberModel : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;

    [Header("Visualization Settings")]
    [Tooltip("Show the 3D Fashion Timber lines along Port and Starboard sides.")]
    public bool showFashionTimbers = true;

    [Tooltip("Show Lower Mould segment (Deadwood/Stern Post to HoB).")]
    public bool showLowerMould = true;

    [Tooltip("Show Upper Mould segment (HoB to Transom Top).")]
    public bool showUpperMould = true;

    public Color fashionColor = Color.magenta;
    public Color lowerMouldColor = new Color(1f, 0.4f, 0.7f);
    public Color upperMouldColor = Color.magenta;

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

    public float GetMinZ()
    {
        if (sheerPlan == null) return 0f;
        return Mathf.Min(sheerPlan.cachedKeelSternPoint.z, Mathf.Min(sheerPlan.cachedHobSternIntersect.z, sheerPlan.cachedTransomTopPoint.z));
    }

    public float GetMaxZ()
    {
        if (sheerPlan == null) return 0f;
        return Mathf.Max(sheerPlan.cachedKeelSternPoint.z, Mathf.Max(sheerPlan.cachedHobSternIntersect.z, sheerPlan.cachedTransomTopPoint.z));
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null) return;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        breadthPlan.UpdateCachedCurves();

        if (showFashionTimbers)
        {
            DrawFashionTimbers();
        }
    }

    public void DrawFashionTimbers()
    {
        Vector3 lowerBound = (sheerPlan.showDeadwoodRake)
            ? sheerPlan.cachedDeadwoodAftPoint
            : sheerPlan.cachedKeelSternPoint;

        Vector3 risingEnd = lowerBound;
        Vector3 hobIntersect = sheerPlan.cachedHobSternIntersect;
        Vector3 transomTop = sheerPlan.cachedTransomTopPoint;

        // 1. Lower Mould (Deadwood/Stern Post -> HoB Intersection)
        if (showLowerMould)
        {
            if (Mathf.Abs(risingEnd.z - hobIntersect.z) > 0.001f || Mathf.Abs(risingEnd.y - hobIntersect.y) > 0.001f)
            {
                DrawFashionTimberSegment(risingEnd, hobIntersect, lowerMouldColor);
            }
        }

        // 2. Upper Mould (HoB Intersection -> Transom Top Point)
        if (showUpperMould)
        {
            if (Mathf.Abs(hobIntersect.z - transomTop.z) > 0.001f || Mathf.Abs(hobIntersect.y - transomTop.y) > 0.001f)
            {
                DrawFashionTimberSegment(hobIntersect, transomTop, upperMouldColor);
            }
        }
    }

    private void DrawFashionTimberSegment(Vector3 startPoint, Vector3 endPoint, Color drawColor, int segments = 50)
    {
        float startZ = startPoint.z;
        float endZ = endPoint.z;
        float startY = startPoint.y;
        float endY = endPoint.y;

        bool isVertical = Mathf.Abs(startZ - endZ) < 0.001f;

        Gizmos.color = drawColor;

        Vector3? prevStbd = null;
        Vector3? prevPort = null;

        for (int i = 0; i <= segments; i++)
        {
            float fraction = i / (float)segments;
            float currZ = isVertical ? startZ : Mathf.Lerp(startZ, endZ, fraction);
            float exactY = isVertical ? Mathf.Lerp(startY, endY, fraction) : sheerPlan.GetSternProfileYAtZ(currZ);

            float exactX = GetNaturalHullXAtYZ(exactY, currZ);

            Vector3 currStbd = new Vector3(exactX, exactY, currZ);
            Vector3 currPort = new Vector3(-exactX, exactY, currZ);

            if (prevStbd.HasValue && prevPort.HasValue)
            {
                Gizmos.DrawLine(prevStbd.Value, currStbd);
                Gizmos.DrawLine(prevPort.Value, currPort);
            }

            prevStbd = currStbd;
            prevPort = currPort;
        }
    }

    public float GetNaturalHullXAtYZ(float targetY, float originalZ)
    {
        if (bodyPlan == null || sheerPlan == null || breadthPlan == null) return 0f;

        float side = 1f; // Starboard half-breadth
        float localTopY = sheerPlan.GetTopTimberYAtZ(originalZ);
        float hw = GetKeelHalfWidth();
        float keelY = GetFrameBottomYAtZ(originalZ);
        float originalKeelY = keelY;

        float hobY = sheerPlan.GetTheoreticalHobYAtZ(originalZ);
        float hobBreadth = breadthPlan.GetTheoreticalBreadthXAtZ(originalZ);
        float xTop = breadthPlan.GetTopTimberXAtZ(originalZ);

        float maxSheerYAtZ = sheerPlan.GetBaseDeckYAtZ(originalZ);
        if (sheerPlan.upperDecks != null && sheerPlan.upperDecks.Count > 0)
        {
            maxSheerYAtZ += sheerPlan.GetMaxDeckHeight() + sheerPlan.GetGlobalSheerOffsetAtZ(originalZ);
        }
        else
        {
            maxSheerYAtZ = sheerPlan.GetTopTimberYAtZ(originalZ);
        }

        // UPPER MOULD SEARCH (targetY >= hobY)
        if (targetY >= hobY - 0.001f)
        {
            Vector3 pU0 = new Vector3(side * hobBreadth, hobY, originalZ);
            Vector3 pU3 = new Vector3(side * xTop, maxSheerYAtZ, originalZ);

            float masterUpperDx = bodyPlan.cachedU3.x - bodyPlan.cachedU0.x;
            float masterUpperDy = bodyPlan.cachedU3.y - bodyPlan.cachedU0.y;

            float localUpperDx = xTop - hobBreadth;
            float localUpperDy = maxSheerYAtZ - hobY;

            float sXUpper = Mathf.Abs(masterUpperDx) > 0.001f ? localUpperDx / masterUpperDx : 0f;
            float sYUpper = Mathf.Abs(masterUpperDy) > 0.001f ? localUpperDy / masterUpperDy : 0f;

            Vector3 masterU1Offset = bodyPlan.cachedU1 - bodyPlan.cachedU0;
            Vector3 masterU2Offset = bodyPlan.cachedU2 - bodyPlan.cachedU3;

            Vector3 pU1 = pU0 + new Vector3(side * masterU1Offset.x * sXUpper, masterU1Offset.y * sYUpper, 0);
            Vector3 pU2 = pU3 + new Vector3(side * masterU2Offset.x * sXUpper, masterU2Offset.y * sYUpper, 0);

            float minT = 0f;
            float maxT = 1f;
            for (int i = 0; i < 15; i++)
            {
                float midT = (minT + maxT) * 0.5f;
                float py = CalculateCubicBezierPoint(midT, pU0, pU1, pU2, pU3).y;
                if (py < targetY) minT = midT;
                else maxT = midT;
            }
            float t = (minT + maxT) * 0.5f;
            return CalculateCubicBezierPoint(t, pU0, pU1, pU2, pU3).x;
        }
        else
        {
            // LOWER MOULD SEARCH (targetY < hobY)
            float xBot = hw;
            Vector3 pL0 = new Vector3(side * xBot, keelY, originalZ);
            Vector3 pL3 = new Vector3(side * hobBreadth, hobY, originalZ);

            Vector3 masterL1 = bodyPlan.cachedL1;
            Vector3 masterLR = bodyPlan.cachedLR;

            float targetL1_X = hw;
            float targetL1_Y = originalKeelY;
            float targetLR_X = hw;
            float targetLR_Y = originalKeelY;

            if (originalZ >= 0)
            {
                float bowZ = sheerPlan.cachedBowRisingEnd.z;
                if (bowZ > 0.001f)
                {
                    float t = sheerPlan.GetRisingLineTAtZ(originalZ);
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

                    if (originalZ < sternZ)
                    {
                        float dzL1 = p2_L1.z - p1_L1.z;
                        if (Mathf.Abs(dzL1) > 0.0001f)
                        {
                            float ratio = (originalZ - p2_L1.z) / dzL1;
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
                            float ratio = (originalZ - p2_LR.z) / dzLR;
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
                        float t = sheerPlan.GetRisingLineTAtZ(originalZ);
                        targetL1_X = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).x;
                        targetL1_Y = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).y;

                        targetLR_X = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).x;
                        targetLR_Y = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).y;
                    }
                }
            }

            targetL1_Y = Mathf.Max(targetL1_Y, originalKeelY);
            targetLR_Y = Mathf.Max(targetLR_Y, originalKeelY);

            Vector3 pL1 = new Vector3(side * targetL1_X, targetL1_Y, originalZ);
            float currentBoxHeight = hobY - originalKeelY;
            float hobDrop = currentBoxHeight * bodyPlan.hobLowerArmLength;
            Vector3 pL2 = new Vector3(side * hobBreadth, Mathf.Max(hobY - hobDrop, targetLR_Y), originalZ);

            float dx = Mathf.Abs(pL2.x - pL1.x);
            float lrPosRatio = bodyPlan.risingLinePosition;
            if (dx > 0.0001f)
            {
                lrPosRatio = Mathf.Clamp01((targetLR_X - targetL1_X) / dx);
            }
            Vector3 pLR = Vector3.Lerp(pL1, pL2, lrPosRatio);

            float sternRisingZ = (sheerPlan != null) ? sheerPlan.cachedSternRisingEnd.z : 0f;
            float profileY = keelY;

            if (sheerPlan != null)
            {
                if (originalZ > sheerPlan.cachedKeelBowPoint.z)
                {
                    profileY = sheerPlan.GetStemProfileYAtZ(originalZ);
                }
                else if (originalZ <= sternRisingZ + 0.001f && sternRisingZ < -0.001f)
                {
                    profileY = targetLR_Y;
                }
            }

            Vector3 pPseudo = new Vector3(side * xBot, profileY, originalZ);

            bool isBehindSternRisingEnd = (originalZ <= sternRisingZ + 0.001f && sternRisingZ < -0.001f);
            if (profileY >= targetLR_Y - 0.001f || isBehindSternRisingEnd)
            {
                pL1 = pPseudo;
                pLR = pPseudo;
                pL2.y = Mathf.Max(pL2.y, profileY);
            }
            else
            {
                pL1.y = Mathf.Clamp(pL1.y, profileY, pLR.y);
                pL2.y = Mathf.Max(pL2.y, pLR.y);
            }

            pL0 = pPseudo;

            float minT = 0f;
            float maxT = 1f;
            for (int i = 0; i < 15; i++)
            {
                float midT = (minT + maxT) * 0.5f;
                Vector3 pt;
                if (midT <= 0.5f) pt = CalculateQuadraticBezierPoint(midT * 2f, pL0, pL1, pLR);
                else pt = CalculateQuadraticBezierPoint((midT - 0.5f) * 2f, pLR, pL2, pL3);

                if (pt.y < targetY) minT = midT;
                else maxT = midT;
            }
            float finalT = (minT + maxT) * 0.5f;
            if (finalT <= 0.5f) return CalculateQuadraticBezierPoint(finalT * 2f, pL0, pL1, pLR).x;
            else return CalculateQuadraticBezierPoint((finalT - 0.5f) * 2f, pLR, pL2, pL3).x;
        }
    }

    public Vector3 GetFashionTimberHobIntersection()
    {
        if (sheerPlan == null || breadthPlan == null) return Vector3.zero;
        Vector3 intersect = sheerPlan.cachedHobSternIntersect;
        intersect.x = breadthPlan.GetTheoreticalBreadthXAtZ(intersect.z);
        return intersect;
    }

    public List<Vector3> GetFashionTimberPoints(bool isStbd, int segments = 50)
    {
        List<Vector3> pts = new List<Vector3>();
        if (sheerPlan == null) return pts;

        Vector3 lowerBound = (sheerPlan.showDeadwoodRake)
            ? sheerPlan.cachedDeadwoodAftPoint
            : sheerPlan.cachedKeelSternPoint;

        Vector3 risingEnd = lowerBound;
        float startZ = risingEnd.z;
        float endZ = sheerPlan.cachedTransomTopPoint.z;
        bool isVertical = Mathf.Abs(startZ - endZ) < 0.001f;

        float startY = risingEnd.y;
        float endY = sheerPlan.cachedTransomTopPoint.y;

        for (int i = 0; i <= segments; i++)
        {
            float fraction = i / (float)segments;
            float currZ = isVertical ? startZ : Mathf.Lerp(startZ, endZ, fraction);
            float exactY = isVertical ? Mathf.Lerp(startY, endY, fraction) : sheerPlan.GetSternProfileYAtZ(currZ);

            float exactX = GetNaturalHullXAtYZ(exactY, currZ);

            if (isStbd) pts.Add(new Vector3(exactX, exactY, currZ));
            else pts.Add(new Vector3(-exactX, exactY, currZ));
        }
        return pts;
    }

    private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        return (u * u * p0) + (2 * u * t * p1) + (t * t * p2);
    }

    private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        return (u * u * u * p0) + (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t * p3);
    }
}
