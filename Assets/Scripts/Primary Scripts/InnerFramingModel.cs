using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class InnerFramingModel : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the ModularBodyPlan.")]
    public BodyPlan bodyPlan;

    [Tooltip("Reference to the FramingModel.")]
    public FramingModel framingModel;

    [Header("Visualization")]
    public bool showInnerFrames = true;
    public Color innerFrameColor = Color.magenta;
    public int resolution = 50; // Segments per half-frame

    public bool showInnerHobLine = true;
    public Color innerHobLineColor = Color.yellow;

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || bodyPlan == null || framingModel == null || !showInnerFrames) return;
        DrawInnerFrames();
    }

    public void DrawInnerFrames()
    {
        Gizmos.color = innerFrameColor;
        List<float> zPositions = framingModel.GetFrameZPositions();

        float thickness = framingModel.frameSpacing * framingModel.GetFrameThicknessRatioValue();

        foreach (float z in zPositions)
        {
            FramingModel.FrameProfile fpCenter = framingModel.GetFrameProfile(z);
            if (!fpCenter.isValid) continue;

            float aftZ = z - (thickness / 2f);
            float foreZ = z + (thickness / 2f);

            FramingModel.FrameProfile fpAft = framingModel.GetFrameProfile(aftZ);
            FramingModel.FrameProfile fpFore = framingModel.GetFrameProfile(foreZ);

            if (fpAft.isValid) DrawSingleInnerFrame(fpAft, aftZ, false);
            DrawSingleInnerFrame(fpCenter, z, true);
            if (fpFore.isValid) DrawSingleInnerFrame(fpFore, foreZ, false);
        }

        if (showInnerHobLine)
        {
            Gizmos.color = innerHobLineColor;
            Vector3 prevStbd = Vector3.zero;
            Vector3 prevPort = Vector3.zero;
            bool first = true;
            foreach (float z in zPositions)
            {
                FramingModel.FrameProfile fpCenter = framingModel.GetFrameProfile(z);
                if (!fpCenter.isValid) continue;

                Vector3 origin = new Vector3(0, 0, z);
                Vector3 pStart = fpCenter.hasLower ? fpCenter.lowerP0 : fpCenter.upperP0;
                Vector3 pEnd = fpCenter.hasUpper ? fpCenter.upperP3 : fpCenter.lowerP3;
                Vector3 uAxis = new Vector3(pEnd.x, 0, pEnd.z) - new Vector3(origin.x, 0, origin.z);
                if (uAxis.sqrMagnitude < 0.0001f) uAxis = Vector3.right;
                else uAxis.Normalize();
                Vector3 vAxis = Vector3.up;
                Vector3 framePlaneNormal = Vector3.Cross(uAxis, vAxis).normalized;

                float keelY = sheerPlan.GetKeelYAtZ(z);
                if (sheerPlan.showDeadwoodRake && z < sheerPlan.cachedDeadwoodForePoint.z && z >= sheerPlan.cachedDeadwoodAftPoint.z)
                {
                    keelY = sheerPlan.GetDeadwoodYAtZ(z);
                }
                float peakY = fpCenter.hasUpper ? fpCenter.upperP3.y : fpCenter.lowerP3.y;
                
                float theoreticalKeelY = sheerPlan.cachedMidshipKeel.y;
                if (z > 0)
                {
                    float t = Mathf.Clamp01(z / sheerPlan.cachedKeelBowPoint.z);
                    theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelBowPoint.y, t);
                }
                else
                {
                    float t = Mathf.Clamp01(z / sheerPlan.cachedKeelSternPoint.z);
                    theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelSternPoint.y, t);
                }
                float theoreticalTotalHeight = Mathf.Max(0.01f, peakY - theoreticalKeelY);

                Vector3 innerPtStbd = Vector3.zero;
                if (fpCenter.hasUpper)
                {
                    innerPtStbd = framingModel.GetInnerPointAtT(fpCenter, true, 0f, z, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                }
                else if (fpCenter.hasLower)
                {
                    innerPtStbd = framingModel.GetInnerPointAtT(fpCenter, false, 1f, z, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                }

                if (innerPtStbd != Vector3.zero)
                {
                    Vector3 innerPtPort = new Vector3(-innerPtStbd.x, innerPtStbd.y, innerPtStbd.z);
                    if (!first)
                    {
                        Gizmos.DrawLine(prevStbd, innerPtStbd);
                        Gizmos.DrawLine(prevPort, innerPtPort);
                    }
                    prevStbd = innerPtStbd;
                    prevPort = innerPtPort;
                    first = false;
                }
            }
        }
    }

    private void DrawSingleInnerFrame(FramingModel.FrameProfile fp, float zPos, bool isDashed = false)
    {
        int totalSegments = resolution;
        
        // Define the local 2D coordinate system for this specific frame
        Vector3 origin = new Vector3(0, 0, zPos);
        
        // Calculate the horizontal direction of the frame (U-axis)
        // We use the lowest and highest X/Z points to define the frame's angle
        Vector3 pStart = fp.hasLower ? fp.lowerP0 : fp.upperP0;
        Vector3 pEnd = fp.hasUpper ? fp.upperP3 : fp.lowerP3;
        
        // Define the outward horizontal axis by pointing from the origin to the maximum breadth of the frame
        Vector3 uAxis = new Vector3(pEnd.x, 0, pEnd.z) - new Vector3(origin.x, 0, origin.z);
        if (uAxis.sqrMagnitude < 0.0001f) uAxis = Vector3.right; // Fallback for perfectly straight vertical frames
        else uAxis.Normalize();
        
        Vector3 vAxis = Vector3.up;
        
        // The normal to the plane of the frame
        Vector3 framePlaneNormal = Vector3.Cross(uAxis, vAxis).normalized;

        List<Vector2> rawLocalStbdPointsLower = new List<Vector2>();
        List<Vector2> rawLocalStbdPointsUpper = new List<Vector2>();

        float keelY = fp.hasLower ? fp.lowerP0.y : sheerPlan.GetKeelYAtZ(zPos);
        if (!fp.hasLower && sheerPlan.showDeadwoodRake && zPos < sheerPlan.cachedDeadwoodForePoint.z && zPos >= sheerPlan.cachedDeadwoodAftPoint.z)
        {
            keelY = sheerPlan.GetDeadwoodYAtZ(zPos);
        }
        float peakY = fp.hasUpper ? fp.upperP3.y : fp.lowerP3.y;
        
        float theoreticalKeelY = sheerPlan.cachedMidshipKeel.y;
        if (zPos > 0)
        {
            float t = Mathf.Clamp01(zPos / sheerPlan.cachedKeelBowPoint.z);
            theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelBowPoint.y, t);
        }
        else
        {
            float t = Mathf.Clamp01(zPos / sheerPlan.cachedKeelSternPoint.z);
            theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelSternPoint.y, t);
        }
        float theoreticalTotalHeight = Mathf.Max(0.01f, peakY - theoreticalKeelY);

        if (fp.hasLower)
        {
            float fashionY = 0f;
            if (framingModel.foreFaceFashionTimberModel != null)
            {
                float sternMaxZ = framingModel.foreFaceFashionTimberModel.GetMaxZ();
                if (zPos < sternMaxZ) fashionY = framingModel.foreFaceFashionTimberModel.GetInnerYAtZ(zPos);
            }

            float innerLowerCutoffT = 0f;
            if (fashionY > 0f)
            {
                float tMin = 0f;
                float tMax = 1f;
                for (int i = 0; i < 20; i++)
                {
                    float midT = (tMin + tMax) / 2f;
                    Vector3 pt = framingModel.GetInnerPointAtT(fp, false, midT, zPos, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                    if (pt.y < fashionY) tMin = midT;
                    else tMax = midT;
                }
                innerLowerCutoffT = (tMin + tMax) / 2f;
            }

            int lSegments = Mathf.CeilToInt(resolution * (fp.lowerTopCutoffT - innerLowerCutoffT));
            if (lSegments < 1) lSegments = 1;
            
            for (int i = 0; i <= lSegments; i++)
            {
                float t = i / (float)lSegments;
                float actualT = Mathf.Lerp(innerLowerCutoffT, fp.lowerTopCutoffT, t);
                
                Vector3 innerPtStbd = framingModel.GetInnerPointAtT(fp, false, actualT, zPos, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                
                Vector3 localOffset = innerPtStbd - origin;
                float localX = Vector3.Dot(localOffset, uAxis);
                float localY = Vector3.Dot(localOffset, vAxis);
                
                rawLocalStbdPointsLower.Add(new Vector2(localX, localY));
            }
        }

        if (fp.hasUpper)
        {
            float fashionY = 0f;
            if (framingModel.foreFaceFashionTimberModel != null)
            {
                float sternMaxZ = framingModel.foreFaceFashionTimberModel.GetMaxZ();
                if (zPos < sternMaxZ) fashionY = framingModel.foreFaceFashionTimberModel.GetInnerYAtZ(zPos);
            }

            float innerUpperCutoffT = 0f;
            if (fashionY > 0f)
            {
                float tMin = 0f;
                float tMax = 1f;
                for (int i = 0; i < 20; i++)
                {
                    float midT = (tMin + tMax) / 2f;
                    Vector3 pt = framingModel.GetInnerPointAtT(fp, true, midT, zPos, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                    if (pt.y < fashionY) tMin = midT;
                    else tMax = midT;
                }
                innerUpperCutoffT = (tMin + tMax) / 2f;
            }
            
            // Also find the correct top cutoff for the inner frame
            float localTtY = sheerPlan.GetTopTimberYAtZ(zPos);
            float innerUpperTopCutoffT = 1f;
            {
                float tMin = 0f;
                float tMax = 1f;
                for (int i = 0; i < 20; i++)
                {
                    float midT = (tMin + tMax) / 2f;
                    Vector3 pt = framingModel.GetInnerPointAtT(fp, true, midT, zPos, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                    if (pt.y < localTtY) tMin = midT;
                    else tMax = midT;
                }
                innerUpperTopCutoffT = (tMin + tMax) / 2f;
            }

            int uSegments = Mathf.CeilToInt(resolution * (innerUpperTopCutoffT - innerUpperCutoffT));
            if (uSegments < 1) uSegments = 1;
            
            int startI = 0;
            if (fp.hasLower && Mathf.Abs(fp.lowerTopCutoffT - 1f) < 0.001f && Mathf.Abs(innerUpperCutoffT - 0f) < 0.001f)
            {
                startI = 1;
            }
            
            for (int i = startI; i <= uSegments; i++)
            {
                float t = i / (float)uSegments;
                float actualT = Mathf.Lerp(innerUpperCutoffT, innerUpperTopCutoffT, t);
                
                Vector3 innerPtStbd = framingModel.GetInnerPointAtT(fp, true, actualT, zPos, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                
                Vector3 localOffset = innerPtStbd - origin;
                float localX = Vector3.Dot(localOffset, uAxis);
                float localY = Vector3.Dot(localOffset, vAxis);
                
                rawLocalStbdPointsUpper.Add(new Vector2(localX, localY));
            }
        }

        // Apply swallowtail removal on the 2D projected points separately to prevent cross-mould deletion
        RemoveSwallowtails(rawLocalStbdPointsLower);
        RemoveSwallowtails(rawLocalStbdPointsUpper);

        // Prevent port and starboard from crossing each other
        ClampToCenterline(rawLocalStbdPointsLower);
        ClampToCenterline(rawLocalStbdPointsUpper);

        List<Vector2> rawLocalStbdPoints = new List<Vector2>();
        rawLocalStbdPoints.AddRange(rawLocalStbdPointsLower);
        rawLocalStbdPoints.AddRange(rawLocalStbdPointsUpper);

        // Convert cleaned 2D points back to 3D and draw them
        Vector3 prevStbd = Vector3.zero;
        Vector3 prevPort = Vector3.zero;
        bool first = true;

        for (int i = 0; i < rawLocalStbdPoints.Count; i++)
        {
            float localX = rawLocalStbdPoints[i].x;
            float localY = rawLocalStbdPoints[i].y;

            Vector3 currStbd = origin + uAxis * localX + vAxis * localY;
            Vector3 currPort = new Vector3(-currStbd.x, currStbd.y, currStbd.z); // Port is just mirrored across X

            if (!first)
            {
                if (!isDashed || i % 2 != 0)
                {
                    Gizmos.DrawLine(prevStbd, currStbd);
                    Gizmos.DrawLine(prevPort, currPort);
                }
            }
            
            prevStbd = currStbd;
            prevPort = currPort;
            first = false;
        }
    }

    private void RemoveSwallowtails(List<Vector2> rawPoints)
    {
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

    private void ClampToCenterline(List<Vector2> points)
    {
        if (points.Count < 2) return;

        // Find where the line crosses from negative X to positive X
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 p1 = points[i];
            Vector2 p2 = points[i + 1];

            if (p1.x < 0 && p2.x >= 0)
            {
                // Find exact intersection with X = 0
                float t = -p1.x / (p2.x - p1.x);
                float intersectY = p1.y + t * (p2.y - p1.y);
                
                Vector2 intersection = new Vector2(0f, intersectY);
                
                // Replace the last negative point with the exact intersection
                points[i] = intersection;
                
                // Remove all points before this one
                if (i > 0)
                {
                    points.RemoveRange(0, i);
                }
                break;
            }
        }
        
        // Edge case: If the entire array is negative, clear it
        if (points.Count > 0 && points[points.Count - 1].x < 0)
        {
            points.Clear();
        }
    }

}
