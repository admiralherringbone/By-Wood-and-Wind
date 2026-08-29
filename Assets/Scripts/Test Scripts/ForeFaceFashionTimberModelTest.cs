using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class ForeFaceFashionTimberModelTest : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the FashionTimberModelTest.")]
    public FashionTimberModelTest fashionTimberModel;

    [Tooltip("Reference to the ForeFashionTimberSpecialFramesTest.")]
    public ForeFashionTimberSpecialFramesTest specialFrames;

    [Header("Inner Timber Settings")]
    [Tooltip("Thickness of the Fashion Timber (offset inwards).")]
    public float thickness = 0.3f;
    public bool showInnerFashionTimber = true;
    public Color innerColor = Color.magenta;

    [HideInInspector] public List<Vector2> cachedInnerPoints = new List<Vector2>();

    public void UpdateCachedPoints()
    {
        cachedInnerPoints.Clear();
        if (sheerPlan == null || fashionTimberModel == null) return;

        Vector3 lowerBound = sheerPlan.cachedSternRisingEnd;
        if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
        {
            lowerBound = sheerPlan.cachedDeadwoodAftPoint;
        }
        Vector3 risingEnd = lowerBound;
        Vector3 transomTop = sheerPlan.cachedTransomTopPoint;

        float startZ = risingEnd.z;
        float endZ = transomTop.z;
        
        bool isVertical = Mathf.Abs(startZ - endZ) < 0.001f;
        int segments = 100;
        
        List<Vector2> rawPoints = new List<Vector2>();

        for (int i = 0; i <= segments; i++)
        {
            float fraction = i / (float)segments;
            float outerZ = isVertical ? startZ : Mathf.Lerp(startZ, endZ, fraction);
            float outerY = isVertical ? Mathf.Lerp(risingEnd.y, transomTop.y, fraction) : sheerPlan.GetSternProfileYAtZ(outerZ);
            
            float delta = 0.01f;
            float zPlus = outerZ + delta;
            float yPlus = isVertical ? Mathf.Lerp(risingEnd.y, transomTop.y, fraction + (delta/Mathf.Abs(startZ - endZ))) : sheerPlan.GetSternProfileYAtZ(zPlus);
            
            float zMinus = outerZ - delta;
            float yMinus = isVertical ? Mathf.Lerp(risingEnd.y, transomTop.y, fraction - (delta/Mathf.Abs(startZ - endZ))) : sheerPlan.GetSternProfileYAtZ(zMinus);
            
            Vector2 tangent = new Vector2(zPlus - zMinus, yPlus - yMinus).normalized;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            if (normal.x < 0) normal = -normal;

            float innerZ = outerZ + normal.x * thickness;
            float innerY = outerY + normal.y * thickness;

            rawPoints.Add(new Vector2(innerZ, innerY));
        }

        // Add the vertical parallel drop down to the bottom of the hull for inner
        // ONLY if the Outer Timber actually has lower moulds (i.e., it starts below the HoB line).
        bool outerStartsBelowHob = risingEnd.y < sheerPlan.GetTheoreticalHobYAtZ(risingEnd.z) - 0.001f;
        
        if (outerStartsBelowHob)
        {
            float actualDropZ = rawPoints[0].x;
            float dropY = sheerPlan.GetKeelYAtZ(actualDropZ);
            if (sheerPlan.showDeadwoodRake && actualDropZ < sheerPlan.cachedDeadwoodForePoint.z && actualDropZ >= sheerPlan.cachedDeadwoodAftPoint.z)
            {
                dropY = sheerPlan.GetDeadwoodYAtZ(actualDropZ);
            }
            
            float topDropY = rawPoints[0].y;
            int dropSegments = 20;
            for (int i = dropSegments; i >= 0; i--)
            {
                float t = i / (float)dropSegments;
                float y = Mathf.Lerp(dropY, topDropY, t);
                if (i == dropSegments) continue; // Skip the top point as it's already rawPoints[0]
                rawPoints.Insert(0, new Vector2(actualDropZ, y));
            }
        }

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
        
        Vector3 lowerBound = sheerPlan.cachedSternRisingEnd;
        if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
        {
            lowerBound = sheerPlan.cachedDeadwoodAftPoint;
        }
        
        // The backbone trace logic has been completely removed as per user request, so the black sphere is no longer needed here.

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
        
        // Find the index where the line crosses the HoB intersection
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

        if (this.specialFrames == null)
        {
            this.specialFrames = Object.FindObjectOfType<ForeFashionTimberSpecialFramesTest>();
        }
        
        float bottomZ = cachedInnerPoints[0].x;
        FramingModelTest.FrameProfile masterFp = new FramingModelTest.FrameProfile { isValid = false };
        if (specialFrames != null && fashionTimberModel != null && fashionTimberModel.framingModel != null)
        {
            masterFp = fashionTimberModel.framingModel.GetRawFrameProfile(bottomZ, false);
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
            if (specialFrames != null && masterFp.isValid)
            {
                exactX = specialFrames.GetSpecialHullXAtYZ(innerY, innerZ, masterFp, bottomZ);
            }
            else
            {
                exactX = fashionTimberModel.GetNaturalHullXAtYZ(innerY, innerZ);
            }
            
            if (!isStbd) exactX = -exactX;
            
            pts.Add(new Vector3(exactX, innerY, innerZ));
            
            if (reachedTop) break;
        }
        return pts;
    }

    private bool LineIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersection)
    {
        intersection = Vector2.zero;
        float d = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);
        if (d == 0f) return false; // Parallel lines
        
        float u = ((p3.x - p1.x) * (p4.y - p3.y) - (p3.y - p1.y) * (p4.x - p3.x)) / d;
        float v = ((p3.x - p1.x) * (p2.y - p1.y) - (p3.y - p1.y) * (p2.x - p1.x)) / d;
        
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false; // Intersection not within segments
        
        intersection.x = p1.x + u * (p2.x - p1.x);
        intersection.y = p1.y + u * (p2.y - p1.y);
        return true;
    }
}
