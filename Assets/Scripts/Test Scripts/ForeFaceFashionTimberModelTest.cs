using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class ForeFaceFashionTimberModelTest : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the FashionTimberModel.")]
    public FashionTimberModel fashionTimberModel;

    [Header("Inner Timber Settings")]
    [Tooltip("Thickness of the Fashion Timber (offset inwards).")]
    public float thickness = 0.3f;
    public bool showInnerFashionTimber = true;
    public Color innerColor = Color.magenta;

    [HideInInspector] public List<Vector2> cachedInnerPoints = new List<Vector2>();
    
    [Header("Debug Visualizer")]
    public float debugDropZ = 0f;

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

        // 1. Find the target frame BEFORE generating the curve
        float lowestInnerZ = risingEnd.z;
        float customDropZ = lowestInnerZ;
        
        FramingModelTest framingModelTest = Object.FindObjectOfType<FramingModelTest>();
        if (framingModelTest != null)
        {
            float spacing = framingModelTest.frameSpacing;
            float thickness = spacing * framingModelTest.GetFrameThicknessRatioValue();
            
            int maxFrames = Mathf.CeilToInt(Mathf.Abs(transomTop.z) / spacing) + 5;
            for (int i = 0; i <= maxFrames; i++)
            {
                float centerZ = -i * spacing;
                float aftZ = centerZ - (thickness / 2f);
                
                // If this frame crosses or is behind lowestInnerZ, it is invalid!
                if (aftZ <= lowestInnerZ) 
                {
                    // The valid frame was the PREVIOUS frame (i - 1).
                    if (i > 0)
                    {
                        customDropZ = (- (i - 1) * spacing) - (thickness / 2f);
                    }
                    break;
                }
            }
        }
        
        debugDropZ = customDropZ;

        // 2. Generate the curve starting from customDropZ
        float startZ = customDropZ;
        float endZ = transomTop.z;
        
        bool isVertical = Mathf.Abs(startZ - endZ) < 0.001f;
        int segments = 100;
        
        List<Vector2> rawPoints = new List<Vector2>();

        for (int i = 0; i <= segments; i++)
        {
            float fraction = i / (float)segments;
            float outerZ = isVertical ? startZ : Mathf.Lerp(startZ, endZ, fraction);
            
            float outerY = isVertical ? Mathf.Lerp(sheerPlan.GetKeelYAtZ(startZ), transomTop.y, fraction) : sheerPlan.GetSternProfileYAtZ(outerZ);
            
            float delta = 0.01f;
            float zPlus = outerZ + delta;
            float yPlus = isVertical ? Mathf.Lerp(sheerPlan.GetKeelYAtZ(startZ), transomTop.y, fraction + (delta/Mathf.Abs(startZ - endZ))) : sheerPlan.GetSternProfileYAtZ(zPlus);
            
            float zMinus = outerZ - delta;
            float yMinus = isVertical ? Mathf.Lerp(sheerPlan.GetKeelYAtZ(startZ), transomTop.y, fraction - (delta/Mathf.Abs(startZ - endZ))) : sheerPlan.GetSternProfileYAtZ(zMinus);
            
            Vector2 tangent = new Vector2(zPlus - zMinus, yPlus - yMinus).normalized;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            if (normal.x < 0) normal = -normal;

            float innerZ = outerZ + normal.x * thickness;
            float innerY = outerY + normal.y * thickness;

            rawPoints.Add(new Vector2(innerZ, innerY));
        }

        if (rawPoints.Count > 0)
        {
            // Mathematically precise guillotine trim to find the exact intersection at customDropZ
            Vector2 p1 = Vector2.zero;
            Vector2 p2 = Vector2.zero;
            bool foundIntersection = false;
            
            // rawPoints is decreasing in Z. We want to find where it crosses customDropZ.
            for (int i = 0; i < rawPoints.Count - 1; i++)
            {
                if (rawPoints[i].x >= customDropZ && rawPoints[i+1].x < customDropZ)
                {
                    p1 = rawPoints[i];
                    p2 = rawPoints[i+1];
                    foundIntersection = true;
                    break;
                }
            }
            
            // Remove all points strictly forward of customDropZ
            for (int i = rawPoints.Count - 1; i >= 0; i--)
            {
                if (rawPoints[i].x > customDropZ)
                {
                    rawPoints.RemoveAt(i);
                }
            }
            
            if (foundIntersection)
            {
                float t = (customDropZ - p1.x) / (p2.x - p1.x);
                float exactY = Mathf.Lerp(p1.y, p2.y, t);
                rawPoints.Insert(0, new Vector2(customDropZ, exactY));
            }
            else if (rawPoints.Count > 0)
            {
                // Fallback
                rawPoints.Insert(0, new Vector2(customDropZ, rawPoints[0].y));
            }
            
            if (rawPoints.Count == 0) return; // Failsafe

            float dropY = sheerPlan.GetKeelYAtZ(customDropZ);
            if (sheerPlan.showDeadwoodRake && customDropZ < sheerPlan.cachedDeadwoodForePoint.z && customDropZ >= sheerPlan.cachedDeadwoodAftPoint.z)
            {
                dropY = sheerPlan.GetDeadwoodYAtZ(customDropZ);
            }
            
            float topY = rawPoints[0].y;
            if (dropY < topY)
            {
                int dropSegments = 20;
                for (int i = 1; i <= dropSegments; i++)
                {
                    float y = Mathf.Lerp(topY, dropY, i / (float)dropSegments);
                    rawPoints.Insert(0, new Vector2(customDropZ, y));
                }
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
        
        cachedInnerPoints = rawPoints;
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

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || fashionTimberModel == null) return;
        DrawInnerFashionTimbers();
    }

    public void DrawInnerFashionTimbers()
    {
        if (debugDropZ < 0f)
        {
            Gizmos.color = Color.red;
            float topY = sheerPlan.GetTopTimberYAtZ(debugDropZ);
            float botY = sheerPlan.GetKeelYAtZ(debugDropZ);
            if (sheerPlan.showDeadwoodRake && debugDropZ < sheerPlan.cachedDeadwoodForePoint.z && debugDropZ >= sheerPlan.cachedDeadwoodAftPoint.z)
            {
                botY = sheerPlan.GetDeadwoodYAtZ(debugDropZ);
            }
            float width = 15f; 
            
            // Draw a single red vertical line on the center axis, and a horizontal line at the top
            Gizmos.DrawLine(new Vector3(0, botY, debugDropZ), new Vector3(0, topY, debugDropZ));
            Gizmos.DrawLine(new Vector3(-width, topY, debugDropZ), new Vector3(width, topY, debugDropZ));
        }

        if (!showInnerFashionTimber) return;

        Gizmos.color = innerColor;

        List<Vector3> stbdPoints = GetFashionTimberPoints(true);
        List<Vector3> portPoints = GetFashionTimberPoints(false);

        if (stbdPoints.Count < 2) return;

        for (int i = 0; i < stbdPoints.Count - 1; i++)
        {
            Gizmos.DrawLine(stbdPoints[i], stbdPoints[i+1]);
            Gizmos.DrawLine(portPoints[i], portPoints[i+1]);
        }
    }

    public List<Vector3> GetFashionTimberPoints(bool isStbd)
    {
        List<Vector3> pts = new List<Vector3>();
        UpdateCachedPoints();
        if (cachedInnerPoints == null || cachedInnerPoints.Count == 0) return pts;

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

            float exactX = fashionTimberModel.GetNaturalHullXAtYZ(innerY, innerZ);
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
