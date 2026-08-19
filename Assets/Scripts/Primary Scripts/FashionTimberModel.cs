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

        // The Lower Fashion Timber begins at the Stern End of the Rising Line.
        Vector3 risingEnd = sheerPlan.cachedSternRisingEnd;

        // 1. Draw from Rising Line End to Stern Post Top
        Vector3 postTop = sheerPlan.cachedSternPostTopPoint;
        if (Mathf.Abs(risingEnd.z - postTop.z) > 0.001f || Mathf.Abs(risingEnd.y - postTop.y) > 0.001f)
        {
            DrawFashionTimberSegment(risingEnd, postTop);
        }

        // 2. Draw the Counter and Transom Rake
        if (sheerPlan.enableCounter)
        {
            DrawFashionTimberSegment(postTop, sheerPlan.cachedCounterTopPoint);
            DrawFashionTimberSegment(sheerPlan.cachedCounterTopPoint, sheerPlan.cachedTransomTopPoint);
        }
        else if (Mathf.Abs(postTop.z - sheerPlan.cachedTransomTopPoint.z) > 0.01f)
        {
            DrawFashionTimberSegment(postTop, sheerPlan.cachedTransomTopPoint);
        }

        // --- DEBUG: Draw Upper Bezier Control Arms ---
        var bezier = GetUpperFashionTimberBezier();
        
        // Starboard Side
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(bezier.p0, bezier.p1);
        Gizmos.DrawLine(bezier.p3, bezier.p2);
        
        Gizmos.DrawWireSphere(bezier.p0, 0.05f);
        Gizmos.DrawWireSphere(bezier.p1, 0.05f);
        Gizmos.DrawWireSphere(bezier.p2, 0.05f);
        Gizmos.DrawWireSphere(bezier.p3, 0.05f);

        // Port Side
        Vector3 p0_port = new Vector3(-bezier.p0.x, bezier.p0.y, bezier.p0.z);
        Vector3 p1_port = new Vector3(-bezier.p1.x, bezier.p1.y, bezier.p1.z);
        Vector3 p2_port = new Vector3(-bezier.p2.x, bezier.p2.y, bezier.p2.z);
        Vector3 p3_port = new Vector3(-bezier.p3.x, bezier.p3.y, bezier.p3.z);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(p0_port, p1_port);
        Gizmos.DrawLine(p3_port, p2_port);

        Gizmos.DrawWireSphere(p0_port, 0.05f);
        Gizmos.DrawWireSphere(p1_port, 0.05f);
        Gizmos.DrawWireSphere(p2_port, 0.05f);
        Gizmos.DrawWireSphere(p3_port, 0.05f);
        // ----------------------------------------------

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.normal.textColor = fashionColor;
            
            float upperTransomY = sheerPlan.cachedTransomTopPoint.y;
            Vector3 labelPos = new Vector3(0, upperTransomY + 0.5f, sheerPlan.cachedTransomTopPoint.z);
            Handles.Label(labelPos, "Fashion Timber", labelStyle);
        }
#endif
    }

    public Vector3 GetFashionTimberHobIntersection()
    {
        Vector3 intersect = sheerPlan.cachedHobSternIntersect;
        intersect.x = breadthPlan.GetBreadthXAtZ(intersect.z);
        return intersect;
    }

    public (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) GetUpperFashionTimberBezier()
    {
        Vector3 intersect = GetFashionTimberHobIntersection();
        float baseZ = intersect.z;
        float baseY = intersect.y;
        float baseWidth = intersect.x;
        
        float topZ = sheerPlan.cachedTransomTopPoint.z;
        float topY = sheerPlan.cachedTransomTopPoint.y;
        float topWidth = breadthPlan.GetTopTimberXAtZ(topZ);

        Vector3 p0 = new Vector3(baseWidth, baseY, baseZ);
        Vector3 p3 = new Vector3(topWidth, topY, topZ);

        float masterHeight = bodyPlan.cachedU3.y - bodyPlan.cachedU0.y;
        float scaleZ = masterHeight > 0.01f ? (topZ - baseZ) / masterHeight : 0f;
        float scaleY = masterHeight > 0.01f ? (topY - baseY) / masterHeight : 0f;
        float scaleX = 1f;
        if (bodyPlan.cachedU0.x > 0.01f) scaleX = baseWidth / bodyPlan.cachedU0.x;

        Vector3 u1Vector = bodyPlan.cachedU1 - bodyPlan.cachedU0;
        Vector3 u2Vector = bodyPlan.cachedU2 - bodyPlan.cachedU3;

        float p1z = p0.z + u1Vector.y * scaleZ;
        
        Vector3 tangentVec = breadthPlan.cachedIsAftCubic 
            ? (breadthPlan.cachedAftP3 - breadthPlan.cachedAftP2)
            : (breadthPlan.cachedAftP2 - breadthPlan.cachedAftP1);

        float dXdZ = 0f;
        if (Mathf.Abs(tangentVec.z) > 0.0001f)
        {
            dXdZ = tangentVec.x / tangentVec.z;
        }

        float deltaZ = p1z - p0.z;
        float fairedX = p0.x + (deltaZ * dXdZ);

        Vector3 p1 = new Vector3(fairedX, p0.y + (u1Vector.y * scaleY), p1z);

        float p2z = p3.z + u2Vector.y * scaleZ; 
        float p2x_baseline = breadthPlan.GetTopTimberXAtZ(p2z);
        Vector3 p2 = new Vector3(p2x_baseline + (u2Vector.x * scaleX), p3.y + (u2Vector.y * scaleY), p2z);

        return (p0, p1, p2, p3);
    }

    public float GetFashionTimberTAtY(float targetY, (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) bezier)
    {
        float min = -2f;
        float max = 3f;
        for (int i = 0; i < 40; i++)
        {
            float mid = (min + max) / 2f;
            float y = FramingModel.CalculateCubicBezierPoint(mid, bezier.p0, bezier.p1, bezier.p2, bezier.p3).y;
            
            if (y < targetY) min = mid;
            else max = mid;
        }
        return (min + max) / 2f;
    }

    public float GetFashionTimberTAtZ(float targetZ, (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) bezier)
    {
        float min = -2f;
        float max = 3f;
        for (int i = 0; i < 40; i++)
        {
            float mid = (min + max) / 2f;
            float z = FramingModel.CalculateCubicBezierPoint(mid, bezier.p0, bezier.p1, bezier.p2, bezier.p3).z;
            
            if (bezier.p3.z < bezier.p0.z)
            {
                if (z > targetZ) min = mid;
                else max = mid;
            }
            else
            {
                if (z < targetZ) min = mid;
                else max = mid;
            }
        }
        return (min + max) / 2f;
    }

    public float GetNaturalHullXAtYZ(float targetY, float originalZ)
    {
        float lowerAftZ = sheerPlan.cachedSternRisingEnd.z;
        float z = originalZ;
        
        if (z < lowerAftZ) z = lowerAftZ;

        FramingModel.FrameProfile fp = framingModel.GetRawFrameProfile(z);
        if (!fp.isValid) return 0f;

        float exactX = 0f;

        // TRUE physical check against the HoB curve at the actual Z coordinate
        if (fp.hasUpper && targetY > sheerPlan.GetHobYAtZ(originalZ))
        {
            var bezier = GetUpperFashionTimberBezier();
            float fMinT = 0f;
            float fMaxT = 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (fMinT + fMaxT) / 2f;
                float pz = FramingModel.CalculateCubicBezierPoint(midT, bezier.p0, bezier.p1, bezier.p2, bezier.p3).z;
                
                // Pure True Z-axis binary search.
                // Note: The Bezier starts at HoB (forward) and ends at Transom Top (aft).
                // Z decreases as you move aft.
                // If pz is greater than originalZ, it means we are too far forward, so we need to increase midT to move aft.
                if (pz > originalZ) fMinT = midT;
                else fMaxT = midT;
            }
            exactX = FramingModel.CalculateCubicBezierPoint((fMinT + fMaxT) / 2f, bezier.p0, bezier.p1, bezier.p2, bezier.p3).x;
        }
        else if (fp.hasLower)
        {
            // Lower Hull needs mappedTargetY because it evaluates against a clamped 2D frame
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
            
            // Re-apply the taper specifically for the clamped 2D lower frames
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
        
        Vector3 risingEnd = sheerPlan.cachedSternRisingEnd;
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
