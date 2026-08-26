using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class InnerForeFaceFashionTimberModel : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the ModularBodyPlan.")]
    public BodyPlan bodyPlan;

    [Tooltip("Reference to the FramingModel.")]
    public FramingModel framingModel;

    [Tooltip("Reference to the outer ForeFaceFashionTimberModel.")]
    public ForeFaceFashionTimberModel foreFaceFashionTimberModel;

    [Header("Inner Timber Settings")]
    public bool showInnerForeFaceFashionTimber = true;
    public Color innerForeFaceColor = Color.cyan;

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null || framingModel == null || foreFaceFashionTimberModel == null) return;
        DrawInnerForeFaceFashionTimbers();
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

    public void DrawInnerForeFaceFashionTimbers()
    {
        if (!showInnerForeFaceFashionTimber) return;

        Gizmos.color = innerForeFaceColor;

        foreFaceFashionTimberModel.UpdateCachedPoints();
        if (foreFaceFashionTimberModel.cachedInnerPoints == null || foreFaceFashionTimberModel.cachedInnerPoints.Count == 0) return;

        Vector3 prevStbd = new Vector3(-1, -1, 1);
        Vector3 prevPort = new Vector3(-1, -1, 1);
        bool first = true;

        List<Vector3> outerPoints = new List<Vector3>();
        List<float> normalXs = new List<float>();
        for (int i = 0; i < foreFaceFashionTimberModel.cachedInnerPoints.Count; i++)
        {
            float innerZ = foreFaceFashionTimberModel.cachedInnerPoints[i].x;
            float innerY = foreFaceFashionTimberModel.cachedInnerPoints[i].y;

            float topTimberY = sheerPlan.GetTopTimberYAtZ(innerZ);
            if (innerY >= topTimberY)
            {
                if (i > 0)
                {
                    float prevZ = foreFaceFashionTimberModel.cachedInnerPoints[i - 1].x;
                    float prevY = foreFaceFashionTimberModel.cachedInnerPoints[i - 1].y;
                    float prevTop = sheerPlan.GetTopTimberYAtZ(prevZ);
                    
                    float denom = (innerY - prevY) - (topTimberY - prevTop);
                    if (Mathf.Abs(denom) > 0.0001f)
                    {
                        float t = (prevTop - prevY) / denom;
                        innerZ = Mathf.Lerp(prevZ, innerZ, t);
                        innerY = Mathf.Lerp(prevY, innerY, t);
                    }
                }
                else
                {
                    innerY = topTimberY;
                }
                
                float outerX = GetOuterHullXAndNormalAtYZ(innerY, innerZ, out float nx);
                outerPoints.Add(new Vector3(outerX, innerY, innerZ));
                normalXs.Add(nx);
                break; // reached top
            }
            else
            {
                float outerX = GetOuterHullXAndNormalAtYZ(innerY, innerZ, out float nx);
                outerPoints.Add(new Vector3(outerX, innerY, innerZ));
                normalXs.Add(nx);
            }
        }
        


        for (int i = 0; i < outerPoints.Count; i++)
        {
            Vector3 pt = outerPoints[i];
            float nx = normalXs[i];
            
            float tKeel = Mathf.Clamp01(pt.z / sheerPlan.cachedKeelSternPoint.z);
            float theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelSternPoint.y, tKeel);
            float theoreticalPeakY = sheerPlan.GetTopTimberYAtZ(pt.z);
            float theoreticalTotalHeight = Mathf.Max(0.01f, theoreticalPeakY - theoreticalKeelY);
            
            float unifiedOverallT = Mathf.Clamp01((pt.y - theoreticalKeelY) / theoreticalTotalHeight);
            float currentThickness = Mathf.Lerp(bodyPlan.baseFrameThickness, bodyPlan.topFrameThickness, unifiedOverallT);
            
            float exactX = pt.x - (currentThickness / nx);
            
            if (exactX <= 0f) continue;
            
            Vector3 currStbd = new Vector3(exactX, pt.y, pt.z);
            Vector3 currPort = new Vector3(-exactX, pt.y, pt.z);
            
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
}
