using UnityEngine;

[ExecuteInEditMode]
public class ForeFashionTimberSpecialFramesTest3 : MonoBehaviour
{
    public FramingModelTest3 framingModel;
    public ForeFaceFashionTimberModelTest3 foreFaceFashionTimberModelTest;
    public int frameCount = 5;
    public Color frameColor = Color.gray;

    private void OnDrawGizmos()
    {
        if (framingModel == null || foreFaceFashionTimberModelTest == null || framingModel.sheerPlan == null) return;
        
        if (foreFaceFashionTimberModelTest.cachedInnerPoints != null && foreFaceFashionTimberModelTest.cachedInnerPoints.Count > 0)
        {
            float bottomZ = foreFaceFashionTimberModelTest.cachedInnerPoints[0].x;
            float endZ = foreFaceFashionTimberModelTest.GetHobIntersection().z;

            FramingModelTest3.FrameProfile masterFp = framingModel.GetRawFrameProfile(bottomZ, false);
            if (!masterFp.isValid || !masterFp.hasLower) return;

            for (int i = 0; i < frameCount; i++)
            {
                float fraction = frameCount > 1 ? (float)i / (frameCount - 1) : 0f;
                float z = Mathf.Lerp(bottomZ, endZ, fraction);
                DrawScaledMasterFrame(z, masterFp, bottomZ, frameColor);
            }
        }
    }

    public FramingModelTest3.FrameProfile GetScaledMasterFrameProfile(float z, FramingModelTest3.FrameProfile masterFp, float bottomZ)
    {
        if (framingModel == null || foreFaceFashionTimberModelTest == null || framingModel.sheerPlan == null) return new FramingModelTest3.FrameProfile { isValid = false };
        
        if (!masterFp.isValid || !masterFp.hasLower) return new FramingModelTest3.FrameProfile { isValid = false };

        float masterHobBreadth = framingModel.breadthPlan.GetBreadthXAtZ(bottomZ);
        float masterHobY = framingModel.sheerPlan.GetTheoreticalHobYAtZ(bottomZ);
        float masterKeelY = masterFp.lowerP0.y; // Guarantees we respect the exact Keel or Deadwood Y used by the frame

        float customKeelY = masterKeelY;
        if (z < bottomZ - 0.001f)
        {
            float yOffset = masterHobY - masterKeelY;
            customKeelY = framingModel.sheerPlan.GetTheoreticalHobYAtZ(z) - yOffset;
        }

        float currHobBreadth = framingModel.breadthPlan.GetBreadthXAtZ(z);
        float currHobY = framingModel.sheerPlan.GetTheoreticalHobYAtZ(z);
        
        float hw = framingModel.bodyPlan.keelWidth / 2f;
        float masterXDist = masterHobBreadth - hw;
        float currXDist = currHobBreadth - hw;
        float scaleX = masterXDist > 0.001f ? currXDist / masterXDist : 1f;
        
        float masterHeight = masterHobY - masterKeelY;
        float currHeight = currHobY - customKeelY;
        float scaleY = masterHeight > 0.001f ? currHeight / masterHeight : 1f;

        FramingModelTest3.FrameProfile fp = new FramingModelTest3.FrameProfile();
        fp.isValid = true;
        fp.hasLower = true;
        fp.lowerCutoffT = masterFp.lowerCutoffT;
        fp.lowerTopCutoffT = masterFp.lowerTopCutoffT;

        Vector3 ScalePoint(Vector3 masterPt)
        {
            float nxOffset = (masterPt.x - hw) * scaleX;
            float finalX = hw + Mathf.Max(0f, nxOffset);

            float nyOffset = (masterPt.y - masterKeelY) * scaleY;
            float finalY = customKeelY + nyOffset;
            
            return new Vector3(finalX, finalY, z);
        }

        fp.lowerP0 = ScalePoint(masterFp.lowerP0);
        fp.lowerP1_inner = ScalePoint(masterFp.lowerP1_inner);
        fp.lowerP1 = ScalePoint(masterFp.lowerP1);
        fp.lowerPMid = ScalePoint(masterFp.lowerPMid);
        fp.lowerP2 = ScalePoint(masterFp.lowerP2);
        fp.lowerP3 = ScalePoint(masterFp.lowerP3);

        return fp;
    }

    public float GetSpecialHullXAtYZ(float targetY, float z, FramingModelTest3.FrameProfile masterFp, float bottomZ)
    {
        if (framingModel == null || framingModel.sheerPlan == null) return 0f;

        float hobY = framingModel.sheerPlan.GetTheoreticalHobYAtZ(z);
        if (targetY > hobY - 0.001f && foreFaceFashionTimberModelTest != null && foreFaceFashionTimberModelTest.fashionTimberModel != null)
        {
            // We only scaled the lower moulds; upper moulds fall back to normal math
            return foreFaceFashionTimberModelTest.fashionTimberModel.GetNaturalHullXAtYZ(targetY, z);
        }

        FramingModelTest3.FrameProfile fp = GetScaledMasterFrameProfile(z, masterFp, bottomZ);
        if (!fp.isValid) return 0f;

        return SolveAnalyticalLowerMouldXAtY(fp, targetY);
    }

    public static float SolveAnalyticalLowerMouldXAtY(FramingModelTest3.FrameProfile fp, float targetY)
    {
        if (!fp.isValid || !fp.hasLower) return 0f;

        Vector3 p0 = fp.lowerP0;
        Vector3 p1 = fp.lowerP1;
        Vector3 pR = fp.lowerLR;
        Vector3 p2 = fp.lowerP2;
        Vector3 p3 = fp.lowerP3;

        // Piecewise selection based on Rising Line height (pR.y)
        if (targetY <= pR.y)
        {
            if (TrySolveQuadraticBezierY(p0, p1, pR, targetY, out float tau))
            {
                return EvaluateQuadraticBezierX(p0, p1, pR, tau);
            }
            return p0.x;
        }
        else
        {
            if (TrySolveQuadraticBezierY(pR, p2, p3, targetY, out float tau))
            {
                return EvaluateQuadraticBezierX(pR, p2, p3, tau);
            }
            return p3.x;
        }
    }

    private static bool TrySolveQuadraticBezierY(Vector3 A, Vector3 B, Vector3 C, float targetY, out float tau)
    {
        tau = 0f;
        float a = A.y - 2f * B.y + C.y;
        float b = 2f * (B.y - A.y);
        float c = A.y - targetY;

        if (Mathf.Abs(a) < 0.00001f)
        {
            if (Mathf.Abs(b) > 0.00001f)
            {
                float root = -c / b;
                if (root >= -0.01f && root <= 1.01f)
                {
                    tau = Mathf.Clamp01(root);
                    return true;
                }
            }
            return false;
        }

        float disc = b * b - 4f * a * c;
        if (disc < 0f) return false;

        float sqrtDisc = Mathf.Sqrt(disc);
        float r1 = (-b + sqrtDisc) / (2f * a);
        float r2 = (-b - sqrtDisc) / (2f * a);

        if (r1 >= -0.01f && r1 <= 1.01f)
        {
            tau = Mathf.Clamp01(r1);
            return true;
        }
        if (r2 >= -0.01f && r2 <= 1.01f)
        {
            tau = Mathf.Clamp01(r2);
            return true;
        }

        return false;
    }

    private static float EvaluateQuadraticBezierX(Vector3 A, Vector3 B, Vector3 C, float tau)
    {
        float u = 1f - tau;
        return u * u * A.x + 2f * u * tau * B.x + tau * tau * C.x;
    }

    private void DrawScaledMasterFrame(float z, FramingModelTest3.FrameProfile masterFp, float bottomZ, Color color)
    {
        FramingModelTest3.FrameProfile fp = GetScaledMasterFrameProfile(z, masterFp, bottomZ);
        if (!fp.isValid) return;

        Gizmos.color = color;
        int segments = 20;
        
        float startT = 0f; 
        float endT = fp.lowerTopCutoffT;
        
        Vector3 prevStbd = framingModel.GetSurfacePointAtT(fp, 1f, startT);
        Vector3 prevPort = framingModel.GetSurfacePointAtT(fp, -1f, startT);

        for (int i = 1; i <= segments; i++)
        {
            float t = startT + (endT - startT) * (i / (float)segments);

            Vector3 currStbd = framingModel.GetSurfacePointAtT(fp, 1f, t);
            Vector3 currPort = framingModel.GetSurfacePointAtT(fp, -1f, t);

            if (i % 2 != 0) 
            {
                Gizmos.DrawLine(prevStbd, currStbd);
                Gizmos.DrawLine(prevPort, currPort);
            }

            prevStbd = currStbd;
            prevPort = currPort;
        }
    }
}
