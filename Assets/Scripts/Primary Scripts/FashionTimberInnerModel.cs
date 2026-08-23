using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class FashionTimberInnerModel : MonoBehaviour
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

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || fashionTimberModel == null) return;
        DrawInnerFashionTimbers();
    }

    public void DrawInnerFashionTimbers()
    {
        if (!showInnerFashionTimber) return;

        Gizmos.color = innerColor;

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
            
            // Calculate Tangent in Z/Y plane
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

        // Remove self-intersections (swallowtail loops)
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

        Vector3 prevStbd = new Vector3(-1, -1, 1);
        Vector3 prevPort = new Vector3(-1, -1, 1);
        bool first = true;

        for (int i = 0; i < rawPoints.Count; i++)
        {
            float innerZ = rawPoints[i].x;
            float innerY = rawPoints[i].y;

            if (innerZ > risingEnd.z) continue;


            float topTimberY = sheerPlan.GetTopTimberYAtZ(innerZ);
            bool reachedTop = false;
            if (innerY >= topTimberY)
            {
                innerY = topTimberY;
                reachedTop = true;
            }

            float exactX = fashionTimberModel.GetNaturalHullXAtYZ(innerY, innerZ);
            
            Vector3 currStbd = new Vector3(exactX, innerY, innerZ);
            Vector3 currPort = new Vector3(-exactX, innerY, innerZ);
            
            if (!first)
            {
                Gizmos.DrawLine(prevStbd, currStbd);
                Gizmos.DrawLine(prevPort, currPort);
            }
            
            if (reachedTop) break;
            
            prevStbd = currStbd;
            prevPort = currPort;
            first = false;
        }
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
