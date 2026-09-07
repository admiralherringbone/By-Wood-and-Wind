using UnityEngine;
using System.Collections.Generic;
using OuterShellFramingModel = FramingModelTest3;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class FashionTimberModelTest3 : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the ModularBodyPlan.")]
    public BodyPlanTest bodyPlan;

    [Tooltip("Reference to the FramingModelTest3.")]
    public FramingModelTest3 framingModel;

    [Tooltip("Reference to the OuterShellFramingModel.")]
    public OuterShellFramingModel outerShellFramingModel;

    private bool _searchedOuterShell = false;

    [Header("Visualization")]
    public bool showFashionTimbers = true;
    public bool showLabels = true;
    public Color fashionColor = Color.magenta;

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null) return;
        if (framingModel == null && outerShellFramingModel == null)
        {
            outerShellFramingModel = Object.FindObjectOfType<OuterShellFramingModel>();
            _searchedOuterShell = true;
            if (framingModel == null && outerShellFramingModel == null) return;
        }
        DrawFashionTimbers();
    }

    public void DrawFashionTimbers()
    {
        if (!showFashionTimbers) return;

        Gizmos.color = fashionColor;

        Vector3 lowerBound = (sheerPlan != null && sheerPlan.showDeadwoodRake)
            ? sheerPlan.cachedDeadwoodAftPoint
            : (sheerPlan != null ? sheerPlan.cachedSternRisingEnd : Vector3.zero);
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

        // 3. Transverse Counter Lines
        if (sheerPlan.enableCounter)
        {
            Vector3 counterBase = sheerPlan.cachedCounterBasePoint;
            Vector3 counterTop = sheerPlan.cachedCounterTopPoint;

            float baseX = GetNaturalHullXAtYZ(counterBase.y, counterBase.z);
            Vector3 baseStbd = new Vector3(baseX, counterBase.y, counterBase.z);
            Vector3 basePort = new Vector3(-baseX, counterBase.y, counterBase.z);
            Gizmos.DrawLine(baseStbd, basePort);
            
            float topX = GetNaturalHullXAtYZ(counterTop.y, counterTop.z);
            Vector3 topStbd = new Vector3(topX, counterTop.y, counterTop.z);
            Vector3 topPort = new Vector3(-topX, counterTop.y, counterTop.z);
            Gizmos.DrawLine(topStbd, topPort);
        }

        // ----------------------------------------------
    }
    public Vector3 GetFashionTimberHobIntersection()
    {
        Vector3 intersect = sheerPlan.cachedHobSternIntersect;
        intersect.x = breadthPlan.GetBreadthXAtZ(intersect.z);
        return intersect;
    }

    public Vector3 GetHobIntersection()
    {
        return GetFashionTimberHobIntersection();
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

    public float GetYAtZ(float z)
    {
        if (sheerPlan == null) return 0f;
        return sheerPlan.GetSternProfileYAtZ(z);
    }


    public float GetNaturalHullXAtYZ(float targetY, float originalZ)
    {
        if (outerShellFramingModel == null && !_searchedOuterShell)
        {
            outerShellFramingModel = Object.FindObjectOfType<OuterShellFramingModel>();
            _searchedOuterShell = true;
        }

        bool isUpper = targetY >= sheerPlan.GetTheoreticalHobYAtZ(originalZ) - 0.001f;

        if (outerShellFramingModel != null)
        {
            OuterShellFramingModel.FrameProfile fp = outerShellFramingModel.GetRawFrameProfile(originalZ, true);
            if (!fp.isValid) return 0f;

            if (isUpper && fp.hasUpper)
            {
                float tMinT = 0f;
                float tMaxT = 1f;
                for (int i = 0; i < 20; i++)
                {
                    float midT = (tMinT + tMaxT) * 0.5f;
                    float py = outerShellFramingModel.GetUpperSurfacePointAtT(fp, 1f, midT).y;
                    if (py < targetY) tMinT = midT;
                    else tMaxT = midT;
                }
                return outerShellFramingModel.GetUpperSurfacePointAtT(fp, 1f, (tMinT + tMaxT) * 0.5f).x;
            }
            else if (!isUpper && fp.hasLower)
            {
                float fMinT = 0f;
                float fMaxT = 1f;
                for (int i = 0; i < 20; i++)
                {
                    float midT = (fMinT + fMaxT) * 0.5f;
                    float py = outerShellFramingModel.GetSurfacePointAtT(fp, 1f, midT).y;
                    if (py < targetY) fMinT = midT;
                    else fMaxT = midT;
                }
                return outerShellFramingModel.GetSurfacePointAtT(fp, 1f, (fMinT + fMaxT) * 0.5f).x;
            }

            return 0f;
        }

        if (framingModel == null) return 0f;

        FramingModelTest3.FrameProfile fpLegacy = framingModel.GetRawFrameProfile(originalZ, true);
        if (!fpLegacy.isValid) return 0f;

        float exactX = 0f;

        if (isUpper && fpLegacy.hasUpper)
        {
            float tMinT = 0f;
            float tMaxT = 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (tMinT + tMaxT) / 2f;
                float py = FramingModelTest3.CalculateCubicBezierPoint(midT, fpLegacy.upperP0, fpLegacy.upperP1, fpLegacy.upperP2, fpLegacy.upperP3).y;
                if (py < targetY) tMinT = midT;
                else tMaxT = midT;
            }
            exactX = FramingModelTest3.CalculateCubicBezierPoint((tMinT + tMaxT) / 2f, fpLegacy.upperP0, fpLegacy.upperP1, fpLegacy.upperP2, fpLegacy.upperP3).x;
        }
        else if (!isUpper && fpLegacy.hasLower)
        {
            float fMinT = 0f;
            float fMaxT = 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (fMinT + fMaxT) / 2f;
                float py = framingModel.GetSurfacePointAtT(fpLegacy, 1f, midT, true).y;
                if (py < targetY) fMinT = midT;
                else fMaxT = midT;
            }
            exactX = framingModel.GetSurfacePointAtT(fpLegacy, 1f, (fMinT + fMaxT) / 2f, true).x;
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
        
        Vector3 lowerBound = (sheerPlan != null && sheerPlan.showDeadwoodRake)
            ? sheerPlan.cachedDeadwoodAftPoint
            : (sheerPlan != null ? sheerPlan.cachedSternRisingEnd : Vector3.zero);
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
