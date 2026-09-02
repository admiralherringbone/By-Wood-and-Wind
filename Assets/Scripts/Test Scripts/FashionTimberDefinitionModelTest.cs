using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class FashionTimberDefinitionModelTest : MonoBehaviour
{
    public SheerPlan sheerPlan;
    public FashionTimberModelTest fashionTimberModel;
    public ForeFaceFashionTimberModelTest foreFaceModel;
    public ForeFashionTimberSpecialFramesTest specialFrames;

    [Range(1, 10)]
    public int intermediateTimberCount = 3;
    public Color timberColor = Color.cyan;
    public float dashSize = 0.5f;
    public bool useDashedLines = true;

    [HideInInspector]
    public List<List<Vector3>> cachedStbdPoints = new List<List<Vector3>>();
    [HideInInspector]
    public List<List<Vector3>> cachedPortPoints = new List<List<Vector3>>();

    private float lastThickness;
    private int lastCount;
    private Vector3 lastSternEnd;

    public void UpdateCachedPoints()
    {
        if (sheerPlan == null || foreFaceModel == null || fashionTimberModel == null || fashionTimberModel.framingModel == null) return;

        float totalThickness = foreFaceModel.thickness;
        if (totalThickness <= 0.001f) return;

        Vector3 lowerBound = sheerPlan.cachedSternRisingEnd;
        if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
        {
            lowerBound = sheerPlan.cachedDeadwoodAftPoint;
        }
        
        if (totalThickness == lastThickness && intermediateTimberCount == lastCount && lowerBound == lastSternEnd && cachedStbdPoints.Count > 0)
        {
            return;
        }

        lastThickness = totalThickness;
        lastCount = intermediateTimberCount;
        lastSternEnd = lowerBound;

        cachedStbdPoints.Clear();
        cachedPortPoints.Clear();
        Vector3 transomTop = sheerPlan.cachedTransomTopPoint;

        // Start mathematical generation from the very bottom of the Stern Post 
        // to ensure the offset curve crosses the Rising Line.
        float startZ = sheerPlan.cachedKeelSternPoint.z;
        float endZ = transomTop.z;
        
        bool isVertical = Mathf.Abs(startZ - endZ) < 0.001f;
        
        List<Vector3> outerStbdPts = fashionTimberModel.GetFashionTimberPoints(true);
        List<Vector3> innerStbdPts = foreFaceModel.GetFashionTimberPoints(true);
        if (outerStbdPts == null || innerStbdPts == null || outerStbdPts.Count == 0 || innerStbdPts.Count == 0) return;
        
        int segments = 100;

        if (this.specialFrames == null)
        {
            this.specialFrames = Object.FindObjectOfType<ForeFashionTimberSpecialFramesTest>();
        }
        
        float bottomZ = 0f;
        FramingModelTest.FrameProfile masterFp = new FramingModelTest.FrameProfile { isValid = false };
        if (foreFaceModel.cachedInnerPoints != null && foreFaceModel.cachedInnerPoints.Count > 0)
        {
            bottomZ = foreFaceModel.cachedInnerPoints[0].x;
            if (specialFrames != null && fashionTimberModel != null && fashionTimberModel.framingModel != null)
            {
                masterFp = fashionTimberModel.framingModel.GetRawFrameProfile(bottomZ, false);
            }
        }
        
        for (int k = 1; k <= intermediateTimberCount; k++)
        {
            float fraction = (float)k / (intermediateTimberCount + 1);
            float currentThickness = totalThickness * fraction;

            List<Vector2> tempPoints = new List<Vector2>();

            for (int i = 0; i <= segments; i++)
            {
                float curveFraction = i / (float)segments;
                float outerZ = isVertical ? startZ : Mathf.Lerp(startZ, endZ, curveFraction);
                float outerY = isVertical ? Mathf.Lerp(sheerPlan.cachedKeelSternPoint.y, transomTop.y, curveFraction) : sheerPlan.GetSternProfileYAtZ(outerZ);
                
                float delta = 0.01f;
                float zPlus = outerZ + delta;
                float yPlus = isVertical ? Mathf.Lerp(sheerPlan.cachedKeelSternPoint.y, transomTop.y, curveFraction + (delta/Mathf.Abs(startZ - endZ))) : sheerPlan.GetSternProfileYAtZ(zPlus);
                
                float zMinus = outerZ - delta;
                float yMinus = isVertical ? Mathf.Lerp(sheerPlan.cachedKeelSternPoint.y, transomTop.y, curveFraction - (delta/Mathf.Abs(startZ - endZ))) : sheerPlan.GetSternProfileYAtZ(zMinus);
                
                Vector2 tangent = new Vector2(zPlus - zMinus, yPlus - yMinus).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                if (normal.x < 0) normal = -normal;

                // Intermediate slice Z/Y
                float tz = outerZ + normal.x * currentThickness;
                float ty = outerY + normal.y * currentThickness;
                
                tempPoints.Add(new Vector2(tz, ty));
            }

            // Chop off any points that plunge mathematically below the Rising Line
            List<Vector2> rawPoints = new List<Vector2>();
            bool foundIntersection = false;
            
            for (int i = 0; i < tempPoints.Count; i++)
            {
                Vector2 pt = tempPoints[i];
                float risingY = sheerPlan.GetRisingLineYAtZ(pt.x);
                
                if (!foundIntersection)
                {
                    if (pt.y >= risingY)
                    {
                        rawPoints.Add(new Vector2(pt.x, risingY));
                        foundIntersection = true;
                    }
                }
                else
                {
                    rawPoints.Add(pt);
                }
            }
            
            if (rawPoints.Count == 0)
            {
                rawPoints = tempPoints;
            }

            // Add the vertical parallel drop
            bool outerStartsBelowHob = sheerPlan.cachedSternRisingEnd.y < sheerPlan.GetTheoreticalHobYAtZ(sheerPlan.cachedSternRisingEnd.z) - 0.001f;
            
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
                    
                    if (i == dropSegments) continue;
                    rawPoints.Insert(0, new Vector2(actualDropZ, y));
                }
            }

            // Clean self-intersections in Z/Y
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

            // Apply to lists
            List<Vector3> stbdPts = new List<Vector3>();
            List<Vector3> portPts = new List<Vector3>();

            for (int i = 0; i < rawPoints.Count; i++)
            {
                float tz = rawPoints[i].x;
                float ty = rawPoints[i].y;

                float topTimberY = sheerPlan.GetTopTimberYAtZ(tz);
                bool reachedTop = false;
                if (ty >= topTimberY)
                {
                    if (i > 0)
                    {
                        float prevZ = rawPoints[i - 1].x;
                        float prevY = rawPoints[i - 1].y;
                        float prevTop = sheerPlan.GetTopTimberYAtZ(prevZ);
                        
                        float denom = (ty - prevY) - (topTimberY - prevTop);
                        if (Mathf.Abs(denom) > 0.0001f)
                        {
                            float t = (prevTop - prevY) / denom;
                            tz = Mathf.Lerp(prevZ, tz, t);
                            ty = Mathf.Lerp(prevY, ty, t);
                        }
                    }
                    else
                    {
                        ty = topTimberY;
                    }
                    reachedTop = true;
                }

                // Evaluate the perfectly blended X coordinate based ONLY on the actual physical boundary curves!
                float localOuterX = GetInterpolatedXAtY(outerStbdPts, ty);
                float localInnerX = GetInterpolatedXAtY(innerStbdPts, ty);
                float exactX = Mathf.Lerp(localOuterX, localInnerX, fraction);

                stbdPts.Add(new Vector3(exactX, ty, tz));
                portPts.Add(new Vector3(-exactX, ty, tz));
                
                if (reachedTop) break;
            }
            
            cachedStbdPoints.Add(stbdPts);
            cachedPortPoints.Add(portPts);
        }
    }

    private float GetInterpolatedXAtY(List<Vector3> pts, float targetY)
    {
        if (pts == null || pts.Count == 0) return 0f;
        if (pts.Count == 1) return pts[0].x;

        if (targetY <= pts[0].y) return pts[0].x;
        if (targetY >= pts[pts.Count - 1].y) return pts[pts.Count - 1].x;

        for (int i = 0; i < pts.Count - 1; i++)
        {
            float y1 = pts[i].y;
            float y2 = pts[i+1].y;

            if ((targetY >= y1 && targetY <= y2) || (targetY <= y1 && targetY >= y2))
            {
                float denom = y2 - y1;
                if (Mathf.Abs(denom) < 0.0001f) return Mathf.Min(pts[i].x, pts[i+1].x);
                float t = (targetY - y1) / denom;
                return Mathf.Lerp(pts[i].x, pts[i+1].x, t);
            }
        }
        return pts[pts.Count - 1].x;
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

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || fashionTimberModel == null || foreFaceModel == null) return;

        UpdateCachedPoints();

        // Draw black spheres at the bottom points of Outer and Inner models for debugging
        List<Vector3> outerPts = fashionTimberModel.GetFashionTimberPoints(true);
        if (outerPts != null && outerPts.Count > 0)
        {
            Gizmos.color = Color.black;
            Gizmos.DrawSphere(outerPts[0], 0.1f);
            Gizmos.DrawSphere(new Vector3(-outerPts[0].x, outerPts[0].y, outerPts[0].z), 0.1f);
        }

        List<Vector3> innerPts = foreFaceModel.GetFashionTimberPoints(true);
        if (innerPts != null && innerPts.Count > 0)
        {
            Gizmos.color = Color.black;
            Gizmos.DrawSphere(innerPts[0], 0.1f);
            Gizmos.DrawSphere(new Vector3(-innerPts[0].x, innerPts[0].y, innerPts[0].z), 0.1f);
        }

        Gizmos.color = timberColor;
        
        for (int k = 0; k < cachedStbdPoints.Count; k++)
        {
            if (cachedStbdPoints[k].Count > 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(cachedStbdPoints[k][0], 0.1f);
                Gizmos.color = timberColor;
            }

            if (useDashedLines)
            {
                DrawDashedLine(cachedStbdPoints[k]);
                DrawDashedLine(cachedPortPoints[k]);
            }
            else
            {
                DrawSolidLine(cachedStbdPoints[k]);
                DrawSolidLine(cachedPortPoints[k]);
            }
        }
    }

    private void DrawSolidLine(List<Vector3> pts)
    {
        if (pts == null || pts.Count < 2) return;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Gizmos.DrawLine(pts[i], pts[i+1]);
        }
    }

    private void DrawDashedLine(List<Vector3> pts)
    {
        if (pts == null || pts.Count < 2) return;

        float currentDashLength = dashSize;
        float currentGapLength = dashSize;
        
        Vector3 currentPos = pts[0];
        int currentSegment = 0;
        bool drawingDash = true;

        while (currentSegment < pts.Count - 1)
        {
            Vector3 segmentStart = currentPos;
            Vector3 segmentEnd = pts[currentSegment + 1];
            float remainingSegmentDist = Vector3.Distance(segmentStart, segmentEnd);
            
            float targetDist = drawingDash ? currentDashLength : currentGapLength;

            if (remainingSegmentDist >= targetDist)
            {
                Vector3 endPos = Vector3.MoveTowards(currentPos, segmentEnd, targetDist);
                if (drawingDash)
                {
                    Gizmos.DrawLine(currentPos, endPos);
                }
                currentPos = endPos;
                drawingDash = !drawingDash;
                currentDashLength = dashSize;
                currentGapLength = dashSize;
            }
            else
            {
                if (drawingDash)
                {
                    Gizmos.DrawLine(currentPos, segmentEnd);
                }
                if (drawingDash) currentDashLength -= remainingSegmentDist;
                else currentGapLength -= remainingSegmentDist;
                
                currentPos = segmentEnd;
                currentSegment++;
            }
        }
    }

    private Vector3 GetInterpolatedPointAtY(List<Vector3> pts, float targetY)
    {
        if (pts == null || pts.Count == 0) return Vector3.zero;
        if (pts.Count == 1) return pts[0];

        if (targetY <= pts[0].y) return pts[0];
        if (targetY >= pts[pts.Count - 1].y) return pts[pts.Count - 1];

        for (int i = 0; i < pts.Count - 1; i++)
        {
            float y1 = pts[i].y;
            float y2 = pts[i+1].y;

            if ((targetY >= y1 && targetY <= y2) || (targetY <= y1 && targetY >= y2))
            {
                float denom = y2 - y1;
                if (Mathf.Abs(denom) < 0.0001f) return pts[i];
                float t = (targetY - y1) / denom;
                return Vector3.Lerp(pts[i], pts[i+1], t);
            }
        }
        return pts[pts.Count - 1];
    }
}
