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

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || bodyPlan == null || framingModel == null || !showInnerFrames) return;
        DrawInnerFrames();
    }

    public void DrawInnerFrames()
    {
        Gizmos.color = innerFrameColor;
        List<float> zPositions = framingModel.GetFrameZPositions();

        foreach (float z in zPositions)
        {
            FramingModel.FrameProfile fp = framingModel.GetFrameProfile(z);
            if (!fp.isValid) continue;

            DrawSingleInnerFrame(fp, z);
        }
    }

    private void DrawSingleInnerFrame(FramingModel.FrameProfile fp, float zPos)
    {
        int totalSegments = resolution;
        
        // Define the local 2D coordinate system for this specific frame
        Vector3 origin = new Vector3(0, 0, zPos);
        
        // Calculate the horizontal direction of the frame (U-axis)
        // We use the lowest and highest X/Z points to define the frame's angle
        Vector3 pStart = fp.hasLower ? fp.lowerP0 : fp.upperP0;
        Vector3 pEnd = fp.hasUpper ? fp.upperP3 : fp.lowerP3;
        
        Vector3 uAxis = new Vector3(pEnd.x, 0, pEnd.z) - new Vector3(pStart.x, 0, pStart.z);
        if (uAxis.sqrMagnitude < 0.0001f) uAxis = Vector3.right; // Fallback for perfectly straight vertical frames
        else uAxis.Normalize();
        
        Vector3 vAxis = Vector3.up;
        
        // The normal to the plane of the frame
        Vector3 framePlaneNormal = Vector3.Cross(uAxis, vAxis).normalized;

        List<Vector2> rawLocalStbdPointsLower = new List<Vector2>();
        List<Vector2> rawLocalStbdPointsUpper = new List<Vector2>();

        float keelY = fp.hasLower ? fp.lowerP0.y : origin.y;
        float peakY = fp.hasUpper ? fp.upperP3.y : fp.lowerP3.y;
        float totalHeight = Mathf.Max(0.01f, peakY - keelY);

        if (fp.hasLower)
        {
            int lSegments = Mathf.CeilToInt(resolution * (fp.lowerTopCutoffT - fp.lowerCutoffT));
            if (lSegments < 1) lSegments = 1;
            
            for (int i = 0; i <= lSegments; i++)
            {
                float t = i / (float)lSegments;
                float actualT = Mathf.Lerp(fp.lowerCutoffT, fp.lowerTopCutoffT, t);
                
                Vector3 outerPt = framingModel.GetSurfacePointAtT(fp, 1f, actualT, true);
                Vector3 nextPt = framingModel.GetSurfacePointAtT(fp, 1f, Mathf.Min(1f, actualT + 0.01f), true);

                Vector3 tangent = (nextPt - outerPt).normalized;
                if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.up;

                Vector3 normal = Vector3.Cross(tangent, framePlaneNormal).normalized;
                
                Vector3 toCenter = new Vector3(-outerPt.x, 0, zPos - outerPt.z);
                if (Vector3.Dot(normal, toCenter) < 0) normal = -normal;

                float overallT = Mathf.Clamp01((outerPt.y - keelY) / totalHeight);
                float currentThickness = Mathf.Lerp(bodyPlan.baseFrameThickness, bodyPlan.topFrameThickness, overallT);
                
                Vector3 innerPtStbd = outerPt + normal * currentThickness;
                
                Vector3 localOffset = innerPtStbd - origin;
                float localX = Vector3.Dot(localOffset, uAxis);
                float localY = Vector3.Dot(localOffset, vAxis);
                
                rawLocalStbdPointsLower.Add(new Vector2(localX, localY));
            }
        }

        if (fp.hasUpper)
        {
            int uSegments = Mathf.CeilToInt(resolution * (fp.upperTopCutoffT - fp.upperCutoffT));
            if (uSegments < 1) uSegments = 1;
            
            // Start from i=1 if lower mould exists and there is no gap in cutoffs
            int startI = 0;
            if (fp.hasLower && Mathf.Abs(fp.lowerTopCutoffT - 1f) < 0.001f && Mathf.Abs(fp.upperCutoffT - 0f) < 0.001f)
            {
                startI = 1;
            }
            
            for (int i = startI; i <= uSegments; i++)
            {
                float t = i / (float)uSegments;
                float actualT = Mathf.Lerp(fp.upperCutoffT, fp.upperTopCutoffT, t);
                
                Vector3 outerPt = framingModel.GetUpperSurfacePointAtT(fp, 1f, actualT);
                Vector3 nextPt = framingModel.GetUpperSurfacePointAtT(fp, 1f, Mathf.Min(1f, actualT + 0.01f));

                Vector3 tangent = (nextPt - outerPt).normalized;
                if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.up;

                Vector3 normal = Vector3.Cross(tangent, framePlaneNormal).normalized;
                
                Vector3 toCenter = new Vector3(-outerPt.x, 0, zPos - outerPt.z);
                if (Vector3.Dot(normal, toCenter) < 0) normal = -normal;

                float overallT = Mathf.Clamp01((outerPt.y - keelY) / totalHeight);
                float currentThickness = Mathf.Lerp(bodyPlan.baseFrameThickness, bodyPlan.topFrameThickness, overallT);
                
                Vector3 innerPtStbd = outerPt + normal * currentThickness;
                
                Vector3 localOffset = innerPtStbd - origin;
                float localX = Vector3.Dot(localOffset, uAxis);
                float localY = Vector3.Dot(localOffset, vAxis);
                
                rawLocalStbdPointsUpper.Add(new Vector2(localX, localY));
            }
        }

        // Apply swallowtail removal on the 2D projected points separately to prevent cross-mould deletion
        RemoveSwallowtails(rawLocalStbdPointsLower);
        RemoveSwallowtails(rawLocalStbdPointsUpper);

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
                Gizmos.DrawLine(prevStbd, currStbd);
                Gizmos.DrawLine(prevPort, currPort);
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
}
