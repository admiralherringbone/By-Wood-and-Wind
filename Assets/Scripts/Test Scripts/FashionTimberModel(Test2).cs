using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class FashionTimberModel_Test2 : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the ModularBodyPlan.")]
    public BodyPlan bodyPlan;



    [Header("Visualization")]
    public bool showFashionTimbers = true;
    public bool showLabels = true;
    public Color fashionColor = Color.magenta;

    // Cached guides for performance optimization during OnDrawGizmos
    private (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) cachedBodyGuide;
    private (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) cachedBreadthGuide;

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null) return;
        DrawFashionTimbers();
    }

    public void DrawFashionTimbers()
    {
        if (!showFashionTimbers) return;

        // Cache guides once per frame to prevent heavy loop calculations
        cachedBodyGuide = GetFashionTimber2DGuide();
        cachedBreadthGuide = GetFashionTimberBreadthGuide();

        Gizmos.color = fashionColor;

        // Draw ONLY the Upper Fashion Timber
        Vector3 baseIntersect = GetFashionTimberHobIntersection();
        Vector3 topPoint = sheerPlan.cachedTransomTopPoint;

        if (Mathf.Abs(baseIntersect.z - topPoint.z) > 0.001f || Mathf.Abs(baseIntersect.y - topPoint.y) > 0.001f)
        {
            DrawFashionTimberSegment(baseIntersect, topPoint);
        }

        // --- 2D GUIDE VISUALIZATION (GREY) ---
        var guide = cachedBodyGuide;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(guide.p0, guide.p1);
        Gizmos.DrawLine(guide.p3, guide.p2);
        
        Gizmos.DrawWireSphere(guide.p0, 0.05f);
        Gizmos.DrawWireSphere(guide.p1, 0.05f);
        Gizmos.DrawWireSphere(guide.p2, 0.05f);
        Gizmos.DrawWireSphere(guide.p3, 0.05f);

        Vector3 g0_port = new Vector3(-guide.p0.x, guide.p0.y, guide.p0.z);
        Vector3 g1_port = new Vector3(-guide.p1.x, guide.p1.y, guide.p1.z);
        Vector3 g2_port = new Vector3(-guide.p2.x, guide.p2.y, guide.p2.z);
        Vector3 g3_port = new Vector3(-guide.p3.x, guide.p3.y, guide.p3.z);

        Gizmos.DrawLine(g0_port, g1_port);
        Gizmos.DrawLine(g3_port, g2_port);

        Gizmos.DrawWireSphere(g0_port, 0.05f);
        Gizmos.DrawWireSphere(g1_port, 0.05f);
        Gizmos.DrawWireSphere(g2_port, 0.05f);
        Gizmos.DrawWireSphere(g3_port, 0.05f);
        
        // --- BREADTH GUIDE VISUALIZATION (GREY) ---
        var breadthGuide = cachedBreadthGuide;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(breadthGuide.p0, breadthGuide.p1);
        Gizmos.DrawLine(breadthGuide.p3, breadthGuide.p2);
        
        Gizmos.DrawWireSphere(breadthGuide.p0, 0.05f);
        Gizmos.DrawWireSphere(breadthGuide.p1, 0.05f);
        Gizmos.DrawWireSphere(breadthGuide.p2, 0.05f);
        Gizmos.DrawWireSphere(breadthGuide.p3, 0.05f);

        Vector3 b0_port = new Vector3(-breadthGuide.p0.x, breadthGuide.p0.y, breadthGuide.p0.z);
        Vector3 b1_port = new Vector3(-breadthGuide.p1.x, breadthGuide.p1.y, breadthGuide.p1.z);
        Vector3 b2_port = new Vector3(-breadthGuide.p2.x, breadthGuide.p2.y, breadthGuide.p2.z);
        Vector3 b3_port = new Vector3(-breadthGuide.p3.x, breadthGuide.p3.y, breadthGuide.p3.z);

        Gizmos.DrawLine(b0_port, b1_port);
        Gizmos.DrawLine(b3_port, b2_port);

        Gizmos.DrawWireSphere(b0_port, 0.05f);
        Gizmos.DrawWireSphere(b1_port, 0.05f);
        Gizmos.DrawWireSphere(b2_port, 0.05f);
        Gizmos.DrawWireSphere(b3_port, 0.05f);

#if UNITY_EDITOR
        Handles.color = Color.gray;
        Vector3 prevStbdGuide = guide.p0;
        Vector3 prevPortGuide = g0_port;
        
        Vector3 prevStbdBGuide = breadthGuide.p0;
        Vector3 prevPortBGuide = b0_port;

        for (int i = 1; i <= 20; i++)
        {
            float t = i / 20f;
            // Body Guide
            Vector3 currStbdGuide = FramingModel.CalculateCubicBezierPoint(t, guide.p0, guide.p1, guide.p2, guide.p3);
            Vector3 currPortGuide = new Vector3(-currStbdGuide.x, currStbdGuide.y, currStbdGuide.z);
            Handles.DrawDottedLine(prevStbdGuide, currStbdGuide, 2f);
            Handles.DrawDottedLine(prevPortGuide, currPortGuide, 2f);
            prevStbdGuide = currStbdGuide;
            prevPortGuide = currPortGuide;
            
            // Breadth Guide
            Vector3 currStbdBGuide = FramingModel.CalculateCubicBezierPoint(t, breadthGuide.p0, breadthGuide.p1, breadthGuide.p2, breadthGuide.p3);
            Vector3 currPortBGuide = new Vector3(-currStbdBGuide.x, currStbdBGuide.y, currStbdBGuide.z);
            Handles.DrawDottedLine(prevStbdBGuide, currStbdBGuide, 2f);
            Handles.DrawDottedLine(prevPortBGuide, currPortBGuide, 2f);
            prevStbdBGuide = currStbdBGuide;
            prevPortBGuide = currPortBGuide;
        }

        if (showLabels)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.normal.textColor = fashionColor;
            
            float upperTransomY = sheerPlan.cachedTransomTopPoint.y;
            Vector3 labelPos = new Vector3(0, upperTransomY + 0.5f, sheerPlan.cachedTransomTopPoint.z);
            Handles.Label(labelPos, "Fashion Timber", labelStyle);
            Handles.Label(labelPos + new Vector3(0, 0.5f, 0), "(Test2 Guide)", labelStyle);
        }
#endif
    }

    public Vector3 GetFashionTimberHobIntersection()
    {
        Vector3 intersect = sheerPlan.cachedHobSternIntersect;
        intersect.x = breadthPlan.GetBreadthXAtZ(intersect.z);
        return intersect;
    }

    public (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) GetFashionTimber2DGuide()
    {
        Vector3 intersect = GetFashionTimberHobIntersection();
        float baseZ = intersect.z;
        float baseY = intersect.y;
        float baseWidth = intersect.x;
        
        float topZ = sheerPlan.cachedTransomTopPoint.z;
        float topY = sheerPlan.cachedTransomTopPoint.y;
        float topWidth = breadthPlan.GetTopTimberXAtZ(topZ);

        Vector3 p0 = new Vector3(baseWidth, baseY, baseZ);
        // Guide is entirely on the Z-plane of the base
        Vector3 p3 = new Vector3(topWidth, topY, baseZ);

        float masterHeight = bodyPlan.cachedU3.y - bodyPlan.cachedU0.y;
        float scaleY = masterHeight > 0.01f ? (topY - baseY) / masterHeight : 0f;
        float scaleX = 1f;
        if (bodyPlan.cachedU0.x > 0.01f) scaleX = baseWidth / bodyPlan.cachedU0.x;

        Vector3 u1Vector = bodyPlan.cachedU1 - bodyPlan.cachedU0;
        Vector3 u2Vector = bodyPlan.cachedU2 - bodyPlan.cachedU3;

        Vector3 p1 = p0 + new Vector3(u1Vector.x * scaleX, u1Vector.y * scaleY, 0);
        Vector3 p2 = p3 + new Vector3(u2Vector.x * scaleX, u2Vector.y * scaleY, 0);

        return (p0, p1, p2, p3);
    }

    public (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) GetFashionTimberBreadthGuide()
    {
        Vector3 intersect = GetFashionTimberHobIntersection();
        float baseZ = intersect.z;
        float baseY = intersect.y;
        float baseWidth = intersect.x;
        
        float topZ = sheerPlan.cachedTransomTopPoint.z;
        float topWidth = breadthPlan.GetTopTimberXAtZ(topZ);

        Vector3 p0 = new Vector3(baseWidth, baseY, baseZ);
        // Guide sits flat on the baseY plane for easy top-down viewing
        Vector3 p3 = new Vector3(topWidth, baseY, topZ);

        float masterHeight = bodyPlan.cachedU3.y - bodyPlan.cachedU0.y;
        float deltaZ = topZ - baseZ;
        
        float scaleZ = 1f;
        if (masterHeight > 0.01f) scaleZ = deltaZ / masterHeight;

        float scaleX = 1f;
        if (breadthPlan.topTimberMaxBeam > 0.01f) scaleX = topWidth / breadthPlan.topTimberMaxBeam;

        Vector3 u1Vector = bodyPlan.cachedU1 - bodyPlan.cachedU0;
        Vector3 u2Vector = bodyPlan.cachedU2 - bodyPlan.cachedU3;

        // Tangents from Breadth Plan
        float T0 = 0f;
        float T3 = 0f;
        if (Mathf.Abs(deltaZ) > 0.001f)
        {
            float zForward0 = baseZ + 0.01f;
            T0 = (breadthPlan.GetBreadthXAtZ(zForward0) - baseWidth) / 0.01f;
            
            float zForward3 = topZ + 0.01f;
            T3 = (breadthPlan.GetTopTimberXAtZ(zForward3) - topWidth) / 0.01f;
        }

        // Apply Baseline Tangent Fairing
        float p1z = p0.z + u1Vector.y * scaleZ;
        float p1x_baseline = p0.x + T0 * (p1z - p0.z);
        Vector3 p1 = new Vector3(p1x_baseline + (u1Vector.x * scaleX), baseY, p1z);

        float p2z = p3.z + u2Vector.y * scaleZ;
        float p2x_baseline = p3.x + T3 * (p2z - p3.z);
        Vector3 p2 = new Vector3(p2x_baseline + (u2Vector.x * scaleX), baseY, p2z);

        return (p0, p1, p2, p3);
    }


    public float GetNaturalHullXAtYZ(float targetY, float originalZ)
    {
        // 1. Evaluate against the X/Y Body Guide (Upper Mould)
        float fMinT = 0f;
        float fMaxT = 1f;
        for (int i = 0; i < 20; i++)
        {
            float midT = (fMinT + fMaxT) / 2f;
            float py = FramingModel.CalculateCubicBezierPoint(midT, cachedBodyGuide.p0, cachedBodyGuide.p1, cachedBodyGuide.p2, cachedBodyGuide.p3).y;
            
            // Binary search for Y on the X/Y Guide Curve
            if (py < targetY) fMinT = midT;
            else fMaxT = midT;
        }
        float bodyGuideX = FramingModel.CalculateCubicBezierPoint((fMinT + fMaxT) / 2f, cachedBodyGuide.p0, cachedBodyGuide.p1, cachedBodyGuide.p2, cachedBodyGuide.p3).x;

        // 2. Evaluate against the Z/X Breadth Guide (Hard Boundary)
        fMinT = 0f;
        fMaxT = 1f;
        for (int i = 0; i < 20; i++)
        {
            float midT = (fMinT + fMaxT) / 2f;
            float pz = FramingModel.CalculateCubicBezierPoint(midT, cachedBreadthGuide.p0, cachedBreadthGuide.p1, cachedBreadthGuide.p2, cachedBreadthGuide.p3).z;
            
            // Binary search for Z on the Z/X Guide Curve (Z decreases moving aft)
            if (pz > originalZ) fMinT = midT;
            else fMaxT = midT;
        }
        float breadthGuideX = FramingModel.CalculateCubicBezierPoint((fMinT + fMaxT) / 2f, cachedBreadthGuide.p0, cachedBreadthGuide.p1, cachedBreadthGuide.p2, cachedBreadthGuide.p3).x;

        // 3. Apply the Hard Outer Limit
        // The Fashion Timber follows the X/Y guide natively, but is pushed in if it exceeds the Z/X guide
        float exactX = Mathf.Min(bodyGuideX, breadthGuideX);

        // 4. Apply the Inner Boundary Limit
        // The Fashion Timber cannot cross inward past the Top Timber Line on the X-axis
        float innerLimitX = breadthPlan.GetTopTimberXAtZ(originalZ);
        
        return Mathf.Max(exactX, innerLimitX);
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
        cachedBodyGuide = GetFashionTimber2DGuide();
        cachedBreadthGuide = GetFashionTimberBreadthGuide();

        List<Vector3> pts = new List<Vector3>();
        
        Vector3 risingEnd = GetFashionTimberHobIntersection();
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
