using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class InnerFashionTimberModelTest : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the ModularBodyPlan.")]
    public BodyPlan bodyPlan;

    [Tooltip("Reference to the FramingModel.")]
    public FramingModel framingModel;

    [Tooltip("Reference to the outer FashionTimberModelTest.")]
    public FashionTimberModelTest fashionTimberModelTest;

    [Header("Visualization")]
    public bool showInnerFashionTimbers = true;
    public Color innerFashionColor = Color.cyan;

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null || framingModel == null || fashionTimberModelTest == null) return;
        DrawInnerFashionTimbers();
    }

    public void DrawInnerFashionTimbers()
    {
        if (!showInnerFashionTimbers) return;

        Gizmos.color = innerFashionColor;

        Vector3 lowerBound = sheerPlan.cachedSternRisingEnd;
        if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
        {
            lowerBound = sheerPlan.cachedDeadwoodAftPoint;
        }
        Vector3 risingEnd = lowerBound;
        Vector3 hobIntersect = fashionTimberModelTest.GetFashionTimberHobIntersection();
        Vector3 transomTop = sheerPlan.cachedTransomTopPoint;

        // 1. Lower Fashion Timber (Rising Line End -> HoB Intersection)
        if (Mathf.Abs(risingEnd.z - hobIntersect.z) > 0.001f || Mathf.Abs(risingEnd.y - hobIntersect.y) > 0.001f)
        {
            DrawInnerFashionTimberSegment(risingEnd, hobIntersect);
        }
        
        // 2. Upper Fashion Timber (HoB Intersection -> Transom Top)
        if (Mathf.Abs(hobIntersect.z - transomTop.z) > 0.001f || Mathf.Abs(hobIntersect.y - transomTop.y) > 0.001f)
        {
            DrawInnerFashionTimberSegment(hobIntersect, transomTop);
        }
    }

    public float GetOuterHullXAndNormalAtYZ(float targetY, float originalZ, out float nx)
    {
        nx = 1f;
        bool isUpper = targetY >= sheerPlan.GetHobYAtZ(originalZ) - 0.001f;
        
        Vector3 lowerBound = sheerPlan.cachedSternRisingEnd;
        if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
        {
            lowerBound = sheerPlan.cachedDeadwoodAftPoint;
        }
        float lowerAftZ = lowerBound.z;
        float evalZ = originalZ;

        if (!isUpper && evalZ < lowerAftZ)
        {
            evalZ = lowerAftZ;
        }

        FramingModel.FrameProfile fp = framingModel.GetRawFrameProfile(evalZ, true);
        if (!fp.isValid) return 0f;

        float exactX = 0f;

        float keelY = sheerPlan.GetKeelYAtZ(evalZ);
        if (sheerPlan.showDeadwoodRake && evalZ < sheerPlan.cachedDeadwoodForePoint.z && evalZ >= sheerPlan.cachedDeadwoodAftPoint.z)
        {
            keelY = sheerPlan.GetDeadwoodYAtZ(evalZ);
        }
        
        float peakY = fp.hasUpper ? fp.upperP3.y : fp.lowerP3.y;
        float totalHeight = Mathf.Max(0.01f, peakY - keelY);

        Vector3 origin = new Vector3(0, 0, evalZ);
        Vector3 pStart = fp.hasLower ? fp.lowerP0 : fp.upperP0;
        Vector3 pEnd = fp.hasUpper ? fp.upperP3 : fp.lowerP3;
        Vector3 uAxis = new Vector3(pEnd.x, 0, pEnd.z) - new Vector3(origin.x, 0, origin.z);
        if (uAxis.sqrMagnitude < 0.0001f) uAxis = Vector3.right;
        else uAxis.Normalize();
        Vector3 vAxis = Vector3.up;
        Vector3 framePlaneNormal = Vector3.Cross(uAxis, vAxis).normalized;

        if (isUpper && fp.hasUpper)
        {
            float tMinT = 0f;
            float tMaxT = 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (tMinT + tMaxT) / 2f;
                float py = framingModel.GetUpperSurfacePointAtT(fp, 1f, midT).y;
                if (py < targetY) tMinT = midT;
                else tMaxT = midT;
            }
            float tVal = (tMinT + tMaxT) / 2f;
            Vector3 outerPt = framingModel.GetUpperSurfacePointAtT(fp, 1f, tVal);
            exactX = outerPt.x;
            
            Vector3 normal = framingModel.GetInnerNormalAtT(fp, true, tVal, evalZ, framePlaneNormal);
            nx = Mathf.Max(0.01f, Mathf.Abs(normal.x));
        }
        else if (!isUpper && fp.hasLower)
        {
            float mappedTargetY = targetY;
            if (originalZ < lowerAftZ)
            {
                float origHobY = sheerPlan.GetHobYAtZ(originalZ);
                float sternHobY = sheerPlan.GetHobYAtZ(lowerAftZ);
                float hobDifference = origHobY - sternHobY;
                mappedTargetY = targetY - hobDifference;
            }

            float fMinT = 0f;
            float fMaxT = 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (fMinT + fMaxT) / 2f;
                float py = framingModel.GetSurfacePointAtT(fp, 1f, midT, true).y;
                if (py < mappedTargetY) fMinT = midT;
                else fMaxT = midT;
            }
            float tVal = (fMinT + fMaxT) / 2f;
            Vector3 outerPt = framingModel.GetSurfacePointAtT(fp, 1f, tVal, true);
            exactX = outerPt.x;

            if (originalZ < lowerAftZ)
            {
                float sternHobX = breadthPlan.GetBreadthXAtZ(lowerAftZ);
                float currHobX = breadthPlan.GetBreadthXAtZ(originalZ);
                if (sternHobX > 0.001f)
                {
                    exactX *= (currHobX / sternHobX);
                }
            }
            
            Vector3 normal = framingModel.GetInnerNormalAtT(fp, false, tVal, evalZ, framePlaneNormal);
            nx = Mathf.Max(0.01f, Mathf.Abs(normal.x));
        }

        return exactX;
    }

    public List<Vector3> GetFashionTimberPoints(bool isStbd, int segments = 50)
    {
        List<Vector3> pts = new List<Vector3>();

        Vector3 lowerBound = sheerPlan.cachedSternRisingEnd;
        if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
        {
            lowerBound = sheerPlan.cachedDeadwoodAftPoint;
        }
        Vector3 risingEnd = lowerBound;
        Vector3 hobIntersect = fashionTimberModelTest.GetFashionTimberHobIntersection();
        Vector3 transomTop = sheerPlan.cachedTransomTopPoint;

        // --- VERTICAL DROP TO DEADWOOD/KEEL ---
        float startZ = risingEnd.z;
        float startY = risingEnd.y;
        float dropY = sheerPlan.GetKeelYAtZ(startZ);
        if (sheerPlan.showDeadwoodRake && startZ < sheerPlan.cachedDeadwoodForePoint.z && startZ >= sheerPlan.cachedDeadwoodAftPoint.z)
        {
            dropY = sheerPlan.GetDeadwoodYAtZ(startZ);
        }

        if (dropY < startY)
        {
            Vector3 dropStart = new Vector3(0, dropY, startZ);
            Vector3 dropEnd = new Vector3(0, startY, startZ); // Exclusive top to avoid duplicate point
            pts.AddRange(GetInnerFashionTimberSegmentPoints(dropStart, dropEnd, isStbd, 20));
            if (pts.Count > 0) pts.RemoveAt(pts.Count - 1); // Remove the top point because the next segment will add it
        }
        // --------------------------------------

        // 1. Lower Segment
        if (Mathf.Abs(risingEnd.z - hobIntersect.z) > 0.001f || Mathf.Abs(risingEnd.y - hobIntersect.y) > 0.001f)
        {
            pts.AddRange(GetInnerFashionTimberSegmentPoints(risingEnd, hobIntersect, isStbd, segments / 2));
        }

        // 2. Upper Segment
        if (Mathf.Abs(hobIntersect.z - transomTop.z) > 0.001f || Mathf.Abs(hobIntersect.y - transomTop.y) > 0.001f)
        {
            pts.AddRange(GetInnerFashionTimberSegmentPoints(hobIntersect, transomTop, isStbd, segments / 2));
        }

        return pts;
    }

    private List<Vector3> GetInnerFashionTimberSegmentPoints(Vector3 startPoint, Vector3 endPoint, bool isStbd, int segments)
    {
        List<Vector3> pts = new List<Vector3>();
        float startZ = startPoint.z;
        float endZ = endPoint.z;
        float startY = startPoint.y;
        float endY = endPoint.y;
        
        bool isVertical = Mathf.Abs(startZ - endZ) < 0.001f;
        
        for (int i = 0; i <= segments; i++)
        {
            float fraction = i / (float)segments;
            float currZ = isVertical ? startZ : Mathf.Lerp(startZ, endZ, fraction);
            float exactY = isVertical ? Mathf.Lerp(startY, endY, fraction) : sheerPlan.GetSternProfileYAtZ(currZ);
            
            float outerX = GetOuterHullXAndNormalAtYZ(exactY, currZ, out float nx);
            
            float tKeel = Mathf.Clamp01(currZ / sheerPlan.cachedKeelSternPoint.z);
            float theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelSternPoint.y, tKeel);
            float theoreticalPeakY = sheerPlan.GetTopTimberYAtZ(currZ);
            float theoreticalTotalHeight = Mathf.Max(0.01f, theoreticalPeakY - theoreticalKeelY);
            
            float unifiedOverallT = Mathf.Clamp01((exactY - theoreticalKeelY) / theoreticalTotalHeight);
            float currentThickness = Mathf.Lerp(bodyPlan.baseFrameThickness, bodyPlan.topFrameThickness, unifiedOverallT);
            
            float exactX = outerX - (currentThickness / nx);
            
            exactX = Mathf.Max(0.001f, exactX);
            
            if (!isStbd) exactX = -exactX;
            pts.Add(new Vector3(exactX, exactY, currZ));
        }
        return pts;
    }

    private void DrawInnerFashionTimberSegment(Vector3 startPoint, Vector3 endPoint)
    {
        List<Vector3> stbdPts = GetInnerFashionTimberSegmentPoints(startPoint, endPoint, true, 50);
        List<Vector3> portPts = GetInnerFashionTimberSegmentPoints(startPoint, endPoint, false, 50);
        
        if (stbdPts.Count < 2) return;
        
        for (int i = 0; i < stbdPts.Count - 1; i++)
        {
            Gizmos.DrawLine(stbdPts[i], stbdPts[i+1]);
            Gizmos.DrawLine(portPts[i], portPts[i+1]);
        }
    }
}
