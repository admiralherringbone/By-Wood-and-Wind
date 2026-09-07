using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class InnerForeFaceFashionTimberModelTest3 : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the ModularBodyPlan.")]
    public BodyPlanTest bodyPlan;

    [Tooltip("Reference to the FramingModelTest3.")]
    public FramingModelTest3 framingModel;

    [Tooltip("Reference to the outer ForeFaceFashionTimberModelTest3.")]
    public ForeFaceFashionTimberModelTest3 foreFaceFashionTimberModelTest;

    [Header("Inner Timber Settings")]
    public bool showInnerForeFaceFashionTimber = true;
    public Color innerForeFaceColor = Color.cyan;

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null || framingModel == null || foreFaceFashionTimberModelTest == null) return;
        UpdateCachedPoints();
        DrawInnerForeFaceFashionTimbers();
    }
    public void DrawInnerForeFaceFashionTimbers()
    {
        if (!showInnerForeFaceFashionTimber) return;

        Gizmos.color = innerForeFaceColor;

        List<Vector3> stbdPts = GetFashionTimberPoints(true);
        List<Vector3> portPts = GetFashionTimberPoints(false);

        if (stbdPts.Count < 2) return;

        for (int i = 0; i < stbdPts.Count - 1; i++)
        {
            Gizmos.DrawLine(stbdPts[i], stbdPts[i+1]);
            Gizmos.DrawLine(portPts[i], portPts[i+1]);
        }
    }

    private List<Vector3> cachedStbdPoints = new List<Vector3>();
    private List<Vector3> cachedPortPoints = new List<Vector3>();

    public Vector3 GetInnerPointAtYZ(float targetY, float originalZ)
    {
        bool isUpper = targetY >= sheerPlan.GetHobYAtZ(originalZ) - 0.001f;
        FramingModelTest3.FrameProfile fp;

        if (isUpper)
        {
            fp = framingModel.GetRawFrameProfile(originalZ, true);
        }
        else
        {
            float bottomZ = (foreFaceFashionTimberModelTest != null && foreFaceFashionTimberModelTest.cachedInnerPoints != null && foreFaceFashionTimberModelTest.cachedInnerPoints.Count > 0)
                ? foreFaceFashionTimberModelTest.cachedInnerPoints[0].x
                : -999f;

            if (originalZ < bottomZ - 0.001f && foreFaceFashionTimberModelTest != null && foreFaceFashionTimberModelTest.specialFrames != null)
            {
                FramingModelTest3.FrameProfile masterFp = framingModel.GetRawFrameProfile(bottomZ, false);
                fp = foreFaceFashionTimberModelTest.specialFrames.GetScaledMasterFrameProfile(originalZ, masterFp, bottomZ);
            }
            else
            {
                fp = framingModel.GetRawFrameProfile(originalZ, true);
            }
        }

        if (!fp.isValid) return new Vector3(0f, targetY, originalZ);

        Vector3 origin = new Vector3(0, 0, originalZ);
        Vector3 pStart = fp.hasLower ? fp.lowerP0 : fp.upperP0;
        Vector3 pEnd = fp.hasUpper ? fp.upperP3 : fp.lowerP3;
        Vector3 uAxis = new Vector3(pEnd.x, 0, pEnd.z) - new Vector3(origin.x, 0, origin.z);
        if (uAxis.sqrMagnitude < 0.0001f) uAxis = Vector3.right;
        else uAxis.Normalize();
        Vector3 vAxis = Vector3.up;
        Vector3 framePlaneNormal = Vector3.Cross(uAxis, vAxis).normalized;

        float peakY = fp.hasUpper ? fp.upperP3.y : fp.lowerP3.y;
        float theoreticalKeelY = sheerPlan.cachedMidshipKeel.y;
        if (originalZ > 0)
        {
            float t = Mathf.Clamp01(originalZ / sheerPlan.cachedKeelBowPoint.z);
            theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelBowPoint.y, t);
        }
        else
        {
            float t = Mathf.Clamp01(originalZ / sheerPlan.cachedKeelSternPoint.z);
            theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelSternPoint.y, t);
        }
        float theoreticalTotalHeight = Mathf.Max(0.01f, peakY - theoreticalKeelY);

        if (isUpper && fp.hasUpper)
        {
            float tMinT = 0f;
            float tMaxT = 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (tMinT + tMaxT) / 2f;
                Vector3 pt = framingModel.GetInnerPointAtT(fp, true, midT, originalZ, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                if (pt.y < targetY) tMinT = midT;
                else tMaxT = midT;
            }
            float tVal = (tMinT + tMaxT) / 2f;
            return framingModel.GetInnerPointAtT(fp, true, tVal, originalZ, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
        }
        else if (!isUpper && fp.hasLower)
        {
            float tMinT = 0f;
            float tMaxT = 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (tMinT + tMaxT) / 2f;
                Vector3 pt = framingModel.GetInnerPointAtT(fp, false, midT, originalZ, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                if (pt.y < targetY) tMinT = midT;
                else tMaxT = midT;
            }
            float tVal = (tMinT + tMaxT) / 2f;
            return framingModel.GetInnerPointAtT(fp, false, tVal, originalZ, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
        }

        return new Vector3(0f, targetY, originalZ);
    }

    public float GetInnerXAtYZ(float targetY, float originalZ)
    {
        return GetInnerPointAtYZ(targetY, originalZ).x;
    }

    public void UpdateCachedPoints()
    {
        cachedStbdPoints.Clear();
        cachedPortPoints.Clear();

        if (sheerPlan == null) sheerPlan = Object.FindObjectOfType<SheerPlan>();
        if (bodyPlan == null) bodyPlan = Object.FindObjectOfType<BodyPlanTest>();
        if (foreFaceFashionTimberModelTest == null) foreFaceFashionTimberModelTest = Object.FindObjectOfType<ForeFaceFashionTimberModelTest3>();

        if (sheerPlan == null || bodyPlan == null || foreFaceFashionTimberModelTest == null) return;

        foreFaceFashionTimberModelTest.UpdateCachedPoints();
        List<Vector2> innerProfilePts = foreFaceFashionTimberModelTest.cachedInnerPoints;
        if (innerProfilePts == null || innerProfilePts.Count == 0) return;

        List<Vector3> stbdRaw = new List<Vector3>();

        for (int i = 0; i < innerProfilePts.Count; i++)
        {
            Vector2 profilePt = innerProfilePts[i];
            float curZ = profilePt.x;
            float curY = profilePt.y;

            Vector3 innerPt = GetInnerPointAtYZ(curY, curZ);
            stbdRaw.Add(innerPt);
        }

        stbdRaw.Sort((p1, p2) => p1.y.CompareTo(p2.y));

        for (int i = 0; i < stbdRaw.Count; i++)
        {
            Vector3 stbdPt = stbdRaw[i];
            cachedStbdPoints.Add(stbdPt);
            cachedPortPoints.Add(new Vector3(-stbdPt.x, stbdPt.y, stbdPt.z));
        }
    }

    private void RemoveSwallowtails(List<Vector3> pts)
    {
        bool intersectionFound = true;
        int safetyLoop = 0;
        while (intersectionFound && safetyLoop++ < 100)
        {
            intersectionFound = false;
            for (int i = 0; i < pts.Count - 3; i++)
            {
                for (int j = i + 2; j < pts.Count - 1; j++)
                {
                    Vector2 p1 = new Vector2(pts[i].x, pts[i].y);
                    Vector2 p2 = new Vector2(pts[i+1].x, pts[i+1].y);
                    Vector2 p3 = new Vector2(pts[j].x, pts[j].y);
                    Vector2 p4 = new Vector2(pts[j+1].x, pts[j+1].y);

                    if (LineIntersection(p1, p2, p3, p4, out Vector2 intersect))
                    {
                        int removeCount = j - i;
                        float avgZ = (pts[i+1].z + pts[j].z) * 0.5f;
                        pts.RemoveRange(i + 1, removeCount);
                        pts.Insert(i + 1, new Vector3(intersect.x, intersect.y, avgZ));
                        
                        intersectionFound = true;
                        break;
                    }
                }
                if (intersectionFound) break;
            }
        }
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

    public List<Vector3> GetFashionTimberPoints(bool isStbd)
    {
        if (cachedStbdPoints.Count == 0) UpdateCachedPoints();
        return isStbd ? cachedStbdPoints : cachedPortPoints;
    }

    public List<Vector3> GetFashionTimberPoints(bool isStbd, int segments)
    {
        List<Vector3> basePts = GetFashionTimberPoints(isStbd);
        if (basePts == null || basePts.Count == 0) return basePts;
        if (basePts.Count == segments + 1) return basePts;

        List<Vector3> resampled = new List<Vector3>();
        for (int i = 0; i <= segments; i++)
        {
            float fraction = i / (float)segments;
            float indexF = fraction * (basePts.Count - 1);
            int idx1 = Mathf.FloorToInt(indexF);
            int idx2 = Mathf.Min(idx1 + 1, basePts.Count - 1);
            float tLerp = indexF - idx1;
            resampled.Add(Vector3.Lerp(basePts[idx1], basePts[idx2], tLerp));
        }
        return resampled;
    }

    public int GetHobIndex()
    {
        List<Vector3> pts = GetFashionTimberPoints(true);
        if (pts == null || pts.Count == 0) return 0;
        
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i+1];
            
            float hob1 = sheerPlan != null ? sheerPlan.GetTheoreticalHobYAtZ(p1.z) : 0f;
            float hob2 = sheerPlan != null ? sheerPlan.GetTheoreticalHobYAtZ(p2.z) : 0f;
            
            if ((p1.y <= hob1 && p2.y >= hob2) || (p1.y >= hob1 && p2.y <= hob2))
            {
                return i;
            }
        }
        return pts.Count / 2;
    }

    public Vector3 GetFashionTimberPointAtZ(float z, bool isStbd, bool isUpper = false)
    {
        float targetY = GetInnerYAtZ(z, isUpper);
        Vector3 innerPt = GetInnerPointAtYZ(targetY, z);
        if (!isStbd) innerPt.x = -innerPt.x;
        return innerPt;
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

    public float GetInnerYAtZ(float z, bool isUpper = false)
    {
        List<Vector3> pts = GetFashionTimberPoints(true);
        if (pts == null || pts.Count == 0) return 0f;
        
        int hobIdx = GetHobIndex();
        int count = pts.Count;

        int start1 = isUpper ? hobIdx : 0;
        int end1 = isUpper ? count - 1 : hobIdx;

        int start2 = isUpper ? 0 : hobIdx;
        int end2 = isUpper ? hobIdx : count - 1;

        for (int i = start1; i < end1 && i < count - 1; i++)
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

        for (int i = start2; i < end2 && i < count - 1; i++)
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
        return pts[count - 1].y;
    }
}
