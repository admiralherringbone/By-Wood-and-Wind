using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class ForeFaceFashionTimberModelTest3_1 : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the FashionTimberModelTest3.")]
    public FashionTimberModelTest3 fashionTimberModel;

    [Header("Inner Timber Settings")]
    [Tooltip("Thickness of the Fashion Timber (offset inwards).")]
    public float thickness = 0.3f;
    [Range(20, 200)]
    [Tooltip("Resolution (number of segment samples) along the lower timber path from the Deadwood baseline to the Counter.")]
    public int lowerTimberResolution = 100;
    public bool showInnerFashionTimber = true;
    public Color innerColor = Color.cyan;

    [HideInInspector] public List<Vector2> cachedInnerPoints = new List<Vector2>();

    public void UpdateCachedPoints()
    {
        cachedInnerPoints.Clear();
        if (sheerPlan == null || fashionTimberModel == null) return;

        List<Vector2> rawPoints = new List<Vector2>();

        // 1. STERN POST SEGMENT (Straight parallel line extending down to Deadwood/Keel)
        Vector3 postA3 = sheerPlan.cachedKeelSternPoint;
        Vector3 postB3 = sheerPlan.cachedSternPostTopPoint;
        
        Vector2 postA = new Vector2(postA3.z, postA3.y);
        Vector2 postB = new Vector2(postB3.z, postB3.y);
        
        Vector2 postDir = (postB - postA);
        if (postDir.sqrMagnitude < 0.0001f) postDir = Vector2.up;
        
        Vector2 postTangent = postDir.normalized;
        Vector2 postNormal = new Vector2(-postTangent.y, postTangent.x);
        if (postNormal.x < 0) postNormal = -postNormal;

        Vector2 offsetA = postA + postNormal * thickness;
        Vector2 offsetB = postB + postNormal * thickness;

        // Calculate where the straight offset line meets the Deadwood/Keel baseline
        float bottomY = sheerPlan.GetKeelYAtZ(offsetA.x);
        if (sheerPlan.showDeadwoodRake)
        {
            if (offsetA.x >= sheerPlan.cachedDeadwoodAftPoint.z && offsetA.x < sheerPlan.cachedDeadwoodForePoint.z)
            {
                bottomY = sheerPlan.GetDeadwoodYAtZ(offsetA.x);
            }
            else if (offsetA.x < sheerPlan.cachedDeadwoodAftPoint.z)
            {
                bottomY = sheerPlan.cachedDeadwoodAftPoint.y;
            }
        }

        Vector2 startPt = offsetA;
        if (Mathf.Abs(postTangent.y) > 0.0001f)
        {
            float sStart = (bottomY - offsetA.y) / postTangent.y;
            startPt = offsetA + postTangent * sStart;
        }

        int postSegments = Mathf.Max(10, lowerTimberResolution);
        for (int i = 0; i <= postSegments; i++)
        {
            float t = i / (float)postSegments;
            rawPoints.Add(Vector2.Lerp(startPt, offsetB, t));
        }

        // Inject exact Rising Line landmark vertex to prevent chordal corner-cutting across the Rising Line height
        float risingY = sheerPlan.cachedSternRisingEnd.y;
        if (risingY > startPt.y && risingY < offsetB.y && Mathf.Abs(postTangent.y) > 0.0001f)
        {
            float sRising = (risingY - startPt.y) / postTangent.y;
            Vector2 risingPt = startPt + postTangent * sRising;
            rawPoints.Add(risingPt);
        }

        rawPoints.Sort((p1, p2) => p1.y.CompareTo(p2.y));

        // 2. COUNTER & TRANSOM SEGMENTS
        if (sheerPlan.enableCounter)
        {
            Vector3 cBase = sheerPlan.cachedCounterBasePoint;
            Vector3 cCp1 = sheerPlan.cachedCounterCp1;
            Vector3 cCp2 = sheerPlan.cachedCounterCp2;
            Vector3 cTop = sheerPlan.cachedCounterTopPoint;

            int counterSegments = 40;
            for (int i = 1; i <= counterSegments; i++)
            {
                float t = i / (float)counterSegments;
                Vector3 p = CalculateCubicBezierPoint(t, cBase, cCp1, cCp2, cTop);
                Vector3 tangent3 = CalculateCubicBezierTangent(t, cBase, cCp1, cCp2, cTop);
                
                Vector2 tan2 = new Vector2(tangent3.z, tangent3.y).normalized;
                Vector2 norm2 = new Vector2(-tan2.y, tan2.x);
                if (norm2.x < 0) norm2 = -norm2;

                Vector2 offsetPt = new Vector2(p.z, p.y) + norm2 * thickness;
                rawPoints.Add(offsetPt);
            }

            // Upper Transom Rake above Counter
            Vector3 tTop = sheerPlan.cachedTransomTopPoint;
            Vector2 tBase2 = new Vector2(cTop.z, cTop.y);
            Vector2 tTop2 = new Vector2(tTop.z, tTop.y);
            Vector2 tDir = (tTop2 - tBase2);
            if (tDir.sqrMagnitude > 0.0001f)
            {
                Vector2 tTan = tDir.normalized;
                Vector2 tNorm = new Vector2(-tTan.y, tTan.x);
                if (tNorm.x < 0) tNorm = -tNorm;

                Vector2 tOffsetBase = tBase2 + tNorm * thickness;
                Vector2 tOffsetTop = tTop2 + tNorm * thickness;

                int transomSegments = 20;
                for (int i = 1; i <= transomSegments; i++)
                {
                    float t = i / (float)transomSegments;
                    rawPoints.Add(Vector2.Lerp(tOffsetBase, tOffsetTop, t));
                }
            }
        }
        else
        {
            // Direct Transom Rake from Stern Post Top to Transom Top
            Vector3 tTop = sheerPlan.cachedTransomTopPoint;
            Vector2 tBase2 = offsetB;
            Vector2 tTop2 = new Vector2(tTop.z, tTop.y);
            
            Vector2 tDir = (tTop2 - new Vector2(postB3.z, postB3.y));
            if (tDir.sqrMagnitude > 0.0001f)
            {
                Vector2 tTan = tDir.normalized;
                Vector2 tNorm = new Vector2(-tTan.y, tTan.x);
                if (tNorm.x < 0) tNorm = -tNorm;

                Vector2 tOffsetTop = tTop2 + tNorm * thickness;

                int transomSegments = 20;
                for (int i = 1; i <= transomSegments; i++)
                {
                    float t = i / (float)transomSegments;
                    rawPoints.Add(Vector2.Lerp(tBase2, tOffsetTop, t));
                }
            }
        }

        // 3. REMOVE SWALLOWTAILS
        bool intersectionFound = true;
        int safetyLoop = 0;
        while (intersectionFound && safetyLoop++ < 100)
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
        
        cachedInnerPoints = new List<Vector2>(rawPoints);
    }

    public float GetMinZ()
    {
        if (cachedInnerPoints == null || cachedInnerPoints.Count == 0) return sheerPlan != null ? sheerPlan.cachedTransomTopPoint.z : 0;
        float minZ = float.MaxValue;
        foreach (var p in cachedInnerPoints)
        {
            if (p.x < minZ) minZ = p.x;
        }
        return minZ;
    }

    public float GetMaxZ()
    {
        if (cachedInnerPoints == null || cachedInnerPoints.Count == 0) return sheerPlan != null ? sheerPlan.cachedSternRisingEnd.z : 0;
        float maxZ = float.MinValue;
        foreach (var p in cachedInnerPoints)
        {
            if (p.x > maxZ) maxZ = p.x;
        }
        return maxZ;
    }

    public float GetInnerYAtZ(float z)
    {
        if (cachedInnerPoints == null || cachedInnerPoints.Count == 0) return 0f;
        
        for (int i = 0; i < cachedInnerPoints.Count - 1; i++)
        {
            Vector2 p1 = cachedInnerPoints[i];
            Vector2 p2 = cachedInnerPoints[i+1];
            
            if ((z >= p1.x && z <= p2.x) || (z <= p1.x && z >= p2.x))
            {
                if (Mathf.Abs(p2.x - p1.x) < 0.0001f) return Mathf.Min(p1.y, p2.y);
                float t = (z - p1.x) / (p2.x - p1.x);
                return Mathf.Lerp(p1.y, p2.y, t);
            }
        }
        
        if (z < GetMinZ()) return cachedInnerPoints[0].y;
        return cachedInnerPoints[cachedInnerPoints.Count - 1].y;
    }

    public Vector3 GetHobIntersection()
    {
        if (cachedInnerPoints == null || cachedInnerPoints.Count == 0) return Vector3.zero;
        
        for (int i = 0; i < cachedInnerPoints.Count - 1; i++)
        {
            Vector2 p1 = cachedInnerPoints[i];
            Vector2 p2 = cachedInnerPoints[i+1];
            
            float hob1 = sheerPlan.GetTheoreticalHobYAtZ(p1.x);
            float hob2 = sheerPlan.GetTheoreticalHobYAtZ(p2.x);
            
            if ((p1.y <= hob1 && p2.y >= hob2) || (p1.y >= hob1 && p2.y <= hob2))
            {
                float minT = 0f;
                float maxT = 1f;
                for (int j = 0; j < 15; j++)
                {
                    float mid = (minT + maxT) / 2f;
                    float py = Mathf.Lerp(p1.y, p2.y, mid);
                    float pz = Mathf.Lerp(p1.x, p2.x, mid);
                    float phob = sheerPlan.GetTheoreticalHobYAtZ(pz);
                    if (py < phob) minT = mid;
                    else maxT = mid;
                }
                float finalT = (minT + maxT) / 2f;
                float finalZ = Mathf.Lerp(p1.x, p2.x, finalT);
                float finalY = Mathf.Lerp(p1.y, p2.y, finalT);
                
                float finalX = 0f;
                if (fashionTimberModel != null && fashionTimberModel.breadthPlan != null)
                {
                    finalX = fashionTimberModel.breadthPlan.GetBreadthXAtZ(finalZ);
                }
                return new Vector3(finalX, finalY, finalZ);
            }
        }
        return new Vector3(0, cachedInnerPoints[0].y, cachedInnerPoints[0].x);
    }

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || fashionTimberModel == null) return;
        DrawInnerFashionTimbers();
    }

    public void DrawInnerFashionTimbers()
    {
        if (!showInnerFashionTimber) return;

        Gizmos.color = innerColor;

        List<Vector3> stbdPoints = GetFashionTimberPoints(true);
        List<Vector3> portPoints = GetFashionTimberPoints(false);

        if (stbdPoints.Count < 2) return;

        Vector3 hobIntersect = GetHobIntersection();
        
        int splitIndex = stbdPoints.Count - 1;
        for (int i = 0; i < stbdPoints.Count; i++)
        {
            if (stbdPoints[i].y >= hobIntersect.y)
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
                Gizmos.DrawLine(stbdPoints[i], stbdPoints[i+1]);
                Gizmos.DrawLine(portPoints[i], portPoints[i+1]);
            }
        }

        // 2. Upper Fashion Timber
        if (Mathf.Abs(hobIntersect.z - transomTop.z) > 0.001f || Mathf.Abs(hobIntersect.y - transomTop.y) > 0.001f)
        {
            for (int i = splitIndex; i < stbdPoints.Count - 1; i++)
            {
                Gizmos.DrawLine(stbdPoints[i], stbdPoints[i+1]);
                Gizmos.DrawLine(portPoints[i], portPoints[i+1]);
            }
        }
    }

    public List<Vector3> GetFashionTimberPoints(bool isStbd)
    {
        List<Vector3> pts = new List<Vector3>();
        UpdateCachedPoints();
        if (cachedInnerPoints == null || cachedInnerPoints.Count == 0) return pts;

        FramingModelTest3 framingModel = null;
        if (fashionTimberModel != null && fashionTimberModel.framingModel != null)
        {
            framingModel = fashionTimberModel.framingModel;
        }

        for (int i = 0; i < cachedInnerPoints.Count; i++)
        {
            float innerZ = cachedInnerPoints[i].x;
            float innerY = cachedInnerPoints[i].y;

            float topTimberY = sheerPlan.GetTopTimberYAtZ(innerZ);
            bool reachedTop = false;
            if (innerY >= topTimberY)
            {
                if (i > 0)
                {
                    float prevZ = cachedInnerPoints[i - 1].x;
                    float prevY = cachedInnerPoints[i - 1].y;
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
                reachedTop = true;
            }

            float exactX = 0f;
            if (framingModel != null)
            {
                // Re-evaluate a raw frame profile from scratch directly at local innerZ!
                FramingModelTest3.FrameProfile rawFp = framingModel.GetRawFrameProfile(innerZ, false);
                if (rawFp.isValid)
                {
                    float hobY = sheerPlan.GetTheoreticalHobYAtZ(innerZ);
                    if (innerY <= hobY && rawFp.hasLower)
                    {
                        // Exact piecewise analytical solver for raw frame profile
                        exactX = ForeFashionTimberSpecialFramesTest3.SolveAnalyticalLowerMouldXAtY(rawFp, innerY);
                    }
                    else if (rawFp.hasUpper)
                    {
                        // Evaluate local upper mould from scratch
                        float uMinT = rawFp.upperCutoffT;
                        float uMaxT = rawFp.upperTopCutoffT;
                        for (int k = 0; k < 20; k++)
                        {
                            float midT = (uMinT + uMaxT) * 0.5f;
                            float py = framingModel.GetUpperSurfacePointAtT(rawFp, 1f, midT).y;
                            if (py < innerY) uMinT = midT;
                            else uMaxT = midT;
                        }
                        exactX = framingModel.GetUpperSurfacePointAtT(rawFp, 1f, (uMinT + uMaxT) * 0.5f).x;
                    }
                    else
                    {
                        exactX = fashionTimberModel.GetNaturalHullXAtYZ(innerY, innerZ);
                    }
                }
                else
                {
                    exactX = fashionTimberModel.GetNaturalHullXAtYZ(innerY, innerZ);
                }
            }
            else if (fashionTimberModel != null)
            {
                exactX = fashionTimberModel.GetNaturalHullXAtYZ(innerY, innerZ);
            }

            if (!isStbd) exactX = -exactX;

            pts.Add(new Vector3(exactX, innerY, innerZ));

            if (reachedTop) break;
        }
        return pts;
    }

    private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        return (u * u * u * p0) + (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t * p3);
    }

    private Vector3 CalculateCubicBezierTangent(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        return 3 * u * u * (p1 - p0) + 6 * u * t * (p2 - p1) + 3 * t * t * (p3 - p2);
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
