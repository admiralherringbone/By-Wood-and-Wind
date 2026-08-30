using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class InnerForeFaceFashionTimberModelTest : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the ModularBodyPlan.")]
    public BodyPlan bodyPlan;

    [Tooltip("Reference to the FramingModelTest.")]
    public FramingModelTest framingModel;

    [Tooltip("Reference to the outer ForeFaceFashionTimberModelTest.")]
    public ForeFaceFashionTimberModelTest foreFaceFashionTimberModelTest;

    [Header("Inner Timber Settings")]
    public bool showInnerForeFaceFashionTimber = true;
    public Color innerForeFaceColor = Color.cyan;

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null || framingModel == null || foreFaceFashionTimberModelTest == null) return;
        DrawInnerForeFaceFashionTimbers();
    }



    public Vector3 GetOuterNormalAtYZ(float targetY, float originalZ)
    {
        bool isUpper = targetY >= sheerPlan.GetHobYAtZ(originalZ) - 0.001f;
        FramingModelTest.FrameProfile fp;

        if (isUpper)
        {
            fp = framingModel.GetRawFrameProfile(originalZ, true);
        }
        else
        {
            if (foreFaceFashionTimberModelTest != null && foreFaceFashionTimberModelTest.specialFrames != null && foreFaceFashionTimberModelTest.cachedInnerPoints != null && foreFaceFashionTimberModelTest.cachedInnerPoints.Count > 0)
            {
                float bottomZ = foreFaceFashionTimberModelTest.cachedInnerPoints[0].x;
                FramingModelTest.FrameProfile masterFp = framingModel.GetRawFrameProfile(bottomZ, false);
                fp = foreFaceFashionTimberModelTest.specialFrames.GetScaledMasterFrameProfile(originalZ, masterFp, bottomZ);
            }
            else
            {
                fp = framingModel.GetRawFrameProfile(originalZ, true);
            }
        }

        if (!fp.isValid) return Vector3.right;

        Vector3 origin = new Vector3(0, 0, originalZ);
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
            return framingModel.GetInnerNormalAtT(fp, true, tVal, originalZ, framePlaneNormal);
        }
        else if (!isUpper && fp.hasLower)
        {
            float fMinT = 0f;
            float fMaxT = fp.lowerTopCutoffT > 0f ? fp.lowerTopCutoffT : 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (fMinT + fMaxT) / 2f;
                float py = framingModel.GetSurfacePointAtT(fp, 1f, midT, true).y;
                if (py < targetY) fMinT = midT;
                else fMaxT = midT;
            }
            float tVal = (fMinT + fMaxT) / 2f;
            return framingModel.GetInnerNormalAtT(fp, false, tVal, originalZ, framePlaneNormal);
        }

        return Vector3.right;
    }
    public void DrawInnerForeFaceFashionTimbers()
    {
        if (!showInnerForeFaceFashionTimber) return;

        Gizmos.color = innerForeFaceColor;

        List<Vector3> stbdPts = GetFashionTimberPoints(true);
        List<Vector3> portPts = GetFashionTimberPoints(false);

        if (stbdPts.Count < 2) return;

        Vector3 hobIntersect = foreFaceFashionTimberModelTest.GetHobIntersection();
        
        // Find the index where the line crosses the HoB intersection
        int splitIndex = stbdPts.Count - 1;
        for (int i = 0; i < stbdPts.Count; i++)
        {
            if (stbdPts[i].y >= hobIntersect.y)
            {
                splitIndex = i;
                break;
            }
        }

        Vector3 lowerBound = sheerPlan.cachedSternRisingEnd;
        if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
        {
            lowerBound = sheerPlan.cachedDeadwoodAftPoint;
        }
        Vector3 risingEnd = lowerBound;
        Vector3 transomTop = sheerPlan.cachedTransomTopPoint;

        // 1. Lower Fashion Timber
        if (Mathf.Abs(risingEnd.z - hobIntersect.z) > 0.001f || Mathf.Abs(risingEnd.y - hobIntersect.y) > 0.001f)
        {
            for (int i = 0; i < splitIndex; i++)
            {
                Gizmos.DrawLine(stbdPts[i], stbdPts[i+1]);
                Gizmos.DrawLine(portPts[i], portPts[i+1]);
            }
        }

        // 2. Upper Fashion Timber
        if (Mathf.Abs(hobIntersect.z - transomTop.z) > 0.001f || Mathf.Abs(hobIntersect.y - transomTop.y) > 0.001f)
        {
            for (int i = splitIndex; i < stbdPts.Count - 1; i++)
            {
                Gizmos.DrawLine(stbdPts[i], stbdPts[i+1]);
                Gizmos.DrawLine(portPts[i], portPts[i+1]);
            }
        }
    }

    private List<Vector3> cachedStbdPoints = new List<Vector3>();
    private List<Vector3> cachedPortPoints = new List<Vector3>();

    public void UpdateCachedPoints()
    {
        cachedStbdPoints.Clear();
        cachedPortPoints.Clear();

        if (foreFaceFashionTimberModelTest == null) return;

        List<Vector3> outerPoints = foreFaceFashionTimberModelTest.GetFashionTimberPoints(true);
        if (outerPoints == null || outerPoints.Count == 0) return;

        for (int i = 0; i < outerPoints.Count; i++)
        {
            Vector3 pt = outerPoints[i];
            Vector3 normal = GetOuterNormalAtYZ(pt.y, pt.z);
            
            float tKeel = Mathf.Clamp01(pt.z / sheerPlan.cachedKeelSternPoint.z);
            float theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelSternPoint.y, tKeel);
            float theoreticalPeakY = sheerPlan.GetTopTimberYAtZ(pt.z);
            float theoreticalTotalHeight = Mathf.Max(0.01f, theoreticalPeakY - theoreticalKeelY);
            
            float unifiedOverallT = Mathf.Clamp01((pt.y - theoreticalKeelY) / theoreticalTotalHeight);
            float currentThickness = Mathf.Lerp(bodyPlan.baseFrameThickness, bodyPlan.topFrameThickness, unifiedOverallT);
            
            Vector3 innerPt = pt + normal * currentThickness;
            
            if (innerPt.x > 0f)
            {
                cachedStbdPoints.Add(innerPt);
                cachedPortPoints.Add(new Vector3(-innerPt.x, innerPt.y, innerPt.z));
            }
        }
    }

    public List<Vector3> GetFashionTimberPoints(bool isStbd)
    {
        if (cachedStbdPoints.Count == 0) UpdateCachedPoints();
        return isStbd ? cachedStbdPoints : cachedPortPoints;
    }
    public float GetMinZ()
    {
        List<Vector3> pts = GetFashionTimberPoints(true);
        if (pts == null || pts.Count == 0) return sheerPlan != null ? sheerPlan.cachedTransomTopPoint.z : 0;
        float minZ = float.MaxValue;
        foreach (var p in pts)
        {
            if (p.z < minZ) minZ = p.z;
        }
        return minZ;
    }

    public float GetMaxZ()
    {
        List<Vector3> pts = GetFashionTimberPoints(true);
        if (pts == null || pts.Count == 0) return sheerPlan != null ? sheerPlan.cachedSternRisingEnd.z : 0;
        float maxZ = float.MinValue;
        foreach (var p in pts)
        {
            if (p.z > maxZ) maxZ = p.z;
        }
        return maxZ;
    }

    public float GetInnerYAtZ(float z)
    {
        List<Vector3> pts = GetFashionTimberPoints(true);
        if (pts == null || pts.Count == 0) return 0f;
        
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i+1];
            
            if ((z >= p1.z && z <= p2.z) || (z <= p1.z && z >= p2.z))
            {
                if (Mathf.Abs(p2.z - p1.z) < 0.0001f) return Mathf.Min(p1.y, p2.y);
                float t = (z - p1.z) / (p2.z - p1.z);
                return Mathf.Lerp(p1.y, p2.y, t);
            }
        }
        
        if (z < GetMinZ()) return pts[0].y;
        return pts[pts.Count - 1].y;
    }
}
