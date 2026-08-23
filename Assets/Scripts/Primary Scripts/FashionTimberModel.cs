using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class FashionTimberModel : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the ModularBodyPlan.")]
    public BodyPlan bodyPlan;

    [Tooltip("Reference to the FramingModel.")]
    public FramingModel framingModel;

    [Header("Visualization")]
    public bool showFashionTimbers = true;
    public bool showLabels = true;
    public Color fashionColor = Color.magenta;

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null || framingModel == null) return;
        DrawFashionTimbers();
    }

    public void DrawFashionTimbers()
    {
        if (!showFashionTimbers) return;

        Gizmos.color = fashionColor;

        Vector3 lowerBound = sheerPlan.cachedSternRisingEnd;
        if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
        {
            lowerBound = sheerPlan.cachedDeadwoodAftPoint;
        }
        Vector3 risingEnd = lowerBound;
        Vector3 hobIntersect = sheerPlan.cachedHobSternIntersect;
        Vector3 transomTop = sheerPlan.cachedTransomTopPoint;

        // 1. Lower Fashion Timber (Rising Line End -> HoB Intersection)
        if (Mathf.Abs(risingEnd.z - hobIntersect.z) > 0.001f || Mathf.Abs(risingEnd.y - hobIntersect.y) > 0.001f)
        {
            DrawFashionTimberSegment(risingEnd, hobIntersect);
        }
        
        // 2. Upper Fashion Timber (HoB Intersection -> Transom Top)
        if (Mathf.Abs(hobIntersect.z - transomTop.z) > 0.001f || Mathf.Abs(hobIntersect.y - transomTop.y) > 0.001f)
        {
            DrawFashionTimberSegment(hobIntersect, transomTop);
        }

        // ----------------------------------------------
    }
    public Vector3 GetFashionTimberHobIntersection()
    {
        Vector3 intersect = sheerPlan.cachedHobSternIntersect;
        intersect.x = breadthPlan.GetBreadthXAtZ(intersect.z);
        return intersect;
    }

    public float GetNaturalHullXAtYZ(float targetY, float originalZ)
    {
        bool isUpper = targetY >= sheerPlan.GetHobYAtZ(originalZ) - 0.001f;
        
        Vector3 lowerBound = sheerPlan.cachedSternRisingEnd;
        if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
        {
            lowerBound = sheerPlan.cachedDeadwoodAftPoint;
        }
        float lowerAftZ = lowerBound.z;
        float evalZ = originalZ;

        // Lower frames do not mathematically exist aft of the Stern Post, so we must clamp Z for lower hull evaluations.
        if (!isUpper && evalZ < lowerAftZ)
        {
            evalZ = lowerAftZ;
        }

        FramingModel.FrameProfile fp = framingModel.GetRawFrameProfile(evalZ, true);
        if (!fp.isValid) return 0f;

        float exactX = 0f;

        if (isUpper && fp.hasUpper)
        {
            float tMinT = 0f;
            float tMaxT = 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (tMinT + tMaxT) / 2f;
                float py = FramingModel.CalculateCubicBezierPoint(midT, fp.upperP0, fp.upperP1, fp.upperP2, fp.upperP3).y;
                if (py < targetY) tMinT = midT;
                else tMaxT = midT;
            }
            exactX = FramingModel.CalculateCubicBezierPoint((tMinT + tMaxT) / 2f, fp.upperP0, fp.upperP1, fp.upperP2, fp.upperP3).x;
            
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
            exactX = framingModel.GetSurfacePointAtT(fp, 1f, (fMinT + fMaxT) / 2f, true).x;

            if (originalZ < lowerAftZ)
            {
                float sternHobX = breadthPlan.GetBreadthXAtZ(lowerAftZ);
                float currHobX = breadthPlan.GetBreadthXAtZ(originalZ);
                if (sternHobX > 0.001f)
                {
                    exactX *= (currHobX / sternHobX);
                }
            }
        }

        return exactX;
    }

    private void DrawFashionTimberSegment(Vector3 startPoint, Vector3 endPoint)
    {
        float startZ = startPoint.z;
        float endZ = endPoint.z;
        float startY = startPoint.y;
        float endY = endPoint.y;
        
        bool isVertical = Mathf.Abs(startZ - endZ) < 0.001f;
        
        Vector3 prevStbd = new Vector3(-1, -1, 1);
        Vector3 prevPort = new Vector3(-1, -1, 1);
        bool first = true;
        
        for (int i = 0; i <= 50; i++)
        {
            float fraction = i / 50f;
            float currZ = isVertical ? startZ : Mathf.Lerp(startZ, endZ, fraction);
            float exactY = isVertical ? Mathf.Lerp(startY, endY, fraction) : sheerPlan.GetSternProfileYAtZ(currZ);
            
            float exactX = GetNaturalHullXAtYZ(exactY, currZ);
            
            Vector3 currStbd = new Vector3(exactX, exactY, currZ);
            Vector3 currPort = new Vector3(-exactX, exactY, currZ);
            
            if (!first)
            {
                Gizmos.DrawLine(prevStbd, currStbd);
                Gizmos.DrawLine(prevPort, currPort);
            }
            
            prevStbd = currStbd;
            prevPort = currPort;
            first = false;
        }
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
}
