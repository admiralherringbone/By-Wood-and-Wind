using UnityEngine;

public class FairSplineBattenEvaluator : ISectionCurveEvaluator
{
    public SectionCurveMode Mode => SectionCurveMode.FairSplineBatten;
    public string DisplayName => "Fair Spline Batten (C2 Smooth)";
    public bool UsesControlArms => true;

    public Vector3 EvaluateSectionPoint(float u, SectionNodes nodes, float floorHollow, float tension)
    {
        u = Mathf.Clamp01(u);
        Vector3 P0 = nodes.p0;
        Vector3 P1 = nodes.p1;
        Vector3 P2 = nodes.p2;
        Vector3 P3 = nodes.p3;
        Vector3 P4 = nodes.p4;

        float d01 = Vector3.Distance(P0, P1);
        float d12 = Vector3.Distance(P1, P2);
        float d23 = Vector3.Distance(P2, P3);
        float d34 = Vector3.Distance(P3, P4);

        Vector3 v01 = d01 > 0.0001f ? (P1 - P0) / d01 : Vector3.up;
        Vector3 v12 = d12 > 0.0001f ? (P2 - P1) / d12 : Vector3.right;
        Vector3 v23 = d23 > 0.0001f ? (P3 - P2) / d23 : Vector3.right;
        Vector3 v34 = d34 > 0.0001f ? (P4 - P3) / d34 : Vector3.up;

        Vector3 T0 = Vector3.up;
        Vector3 T1 = (v01 * d12 + v12 * d01).normalized;
        Vector3 T2 = (v12 * d23 + v23 * d12).normalized;
        Vector3 T3 = Vector3.up;
        Vector3 T4 = v34;

        if (u <= 0.25f)
        {
            float t = u * 4f;
            Vector3 linearPt = Vector3.Lerp(P0, P1, t);
            Vector3 splinePt = EvaluateHermiteSegment(P0, T0, P1, T1, t, tension);
            return Vector3.Lerp(linearPt, splinePt, floorHollow);
        }
        else if (u <= 0.50f)
        {
            float t = (u - 0.25f) * 4f;
            return EvaluateHermiteSegment(P1, T1, P2, T2, t, tension);
        }
        else if (u <= 0.75f)
        {
            float t = (u - 0.50f) * 4f;
            return EvaluateHermiteSegment(P2, T2, P3, T3, t, tension);
        }
        else
        {
            float t = (u - 0.75f) * 4f;
            return EvaluateHermiteSegment(P3, T3, P4, T4, t, tension);
        }
    }

    public Vector3 GetNodeTangent(int nodeIndex, SectionNodes nodes)
    {
        Vector3 P0 = nodes.p0;
        Vector3 P1 = nodes.p1;
        Vector3 P2 = nodes.p2;
        Vector3 P3 = nodes.p3;
        Vector3 P4 = nodes.p4;

        float d01 = Vector3.Distance(P0, P1);
        float d12 = Vector3.Distance(P1, P2);
        float d23 = Vector3.Distance(P2, P3);
        float d34 = Vector3.Distance(P3, P4);

        Vector3 v01 = d01 > 0.0001f ? (P1 - P0) / d01 : Vector3.up;
        Vector3 v12 = d12 > 0.0001f ? (P2 - P1) / d12 : Vector3.right;
        Vector3 v23 = d23 > 0.0001f ? (P3 - P2) / d23 : Vector3.right;
        Vector3 v34 = d34 > 0.0001f ? (P4 - P3) / d34 : Vector3.up;

        switch (nodeIndex)
        {
            case 0: return Vector3.up;
            case 1: return (v01 * d12 + v12 * d01).normalized;
            case 2: return (v12 * d23 + v23 * d12).normalized;
            case 3: return Vector3.up;
            case 4: return v34;
            default: return Vector3.up;
        }
    }

    private Vector3 EvaluateHermiteSegment(Vector3 pA, Vector3 tA, Vector3 pB, Vector3 tB, float t, float tension)
    {
        float d = Vector3.Distance(pA, pB);
        float scale = Mathf.Clamp(tension, 0.5f, 2.0f) * 0.75f;
        Vector3 mA = tA * d * scale;
        Vector3 mB = tB * d * scale;

        float t2 = t * t;
        float t3 = t2 * t;

        float h00 = 2 * t3 - 3 * t2 + 1;
        float h10 = t3 - 2 * t2 + t;
        float h01 = -2 * t3 + 3 * t2;
        float h11 = t3 - t2;

        return h00 * pA + h10 * mA + h01 * pB + h11 * mB;
    }
}
