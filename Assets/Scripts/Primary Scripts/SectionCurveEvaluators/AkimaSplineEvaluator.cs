using UnityEngine;

public class AkimaSplineEvaluator : ISectionCurveEvaluator
{
    public SectionCurveMode Mode => SectionCurveMode.AkimaSpline;
    public string DisplayName => "Akima Monotone Spline (Non-Overshoot)";
    public bool UsesControlArms => true;

    public Vector3 EvaluateSectionPoint(float u, SectionNodes nodes, float floorHollow, float tension)
    {
        u = Mathf.Clamp01(u);
        Vector3 P0 = nodes.p0;
        Vector3 P1 = nodes.p1;
        Vector3 P2 = nodes.p2;
        Vector3 P3 = nodes.p3;
        Vector3 P4 = nodes.p4;

        Vector3 T0 = GetNodeTangent(0, nodes);
        Vector3 T1 = GetNodeTangent(1, nodes);
        Vector3 T2 = GetNodeTangent(2, nodes);
        Vector3 T3 = GetNodeTangent(3, nodes);
        Vector3 T4 = GetNodeTangent(4, nodes);

        if (u <= 0.25f)
        {
            float t = u * 4f;
            Vector3 linearPt = Vector3.Lerp(P0, P1, t);
            Vector3 akimaPt = EvaluateHermiteSegment(P0, T0, P1, T1, t, tension);
            return Vector3.Lerp(linearPt, akimaPt, floorHollow);
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

        if (nodeIndex == 0) return Vector3.up;
        if (nodeIndex == 3) return Vector3.up;

        Vector3 d01 = (P1 - P0).normalized;
        Vector3 d12 = (P2 - P1).normalized;
        Vector3 d23 = (P3 - P2).normalized;
        Vector3 d34 = (P4 - P3).normalized;

        if (nodeIndex == 1)
        {
            float w1 = Vector3.Distance(d23, d12);
            float w2 = Vector3.Distance(d12, d01);
            if (w1 + w2 < 0.0001f) return (d01 + d12).normalized;
            return ((w1 * d01 + w2 * d12) / (w1 + w2)).normalized;
        }
        else if (nodeIndex == 2)
        {
            float w1 = Vector3.Distance(d34, d23);
            float w2 = Vector3.Distance(d23, d12);
            if (w1 + w2 < 0.0001f) return (d12 + d23).normalized;
            return ((w1 * d12 + w2 * d23) / (w1 + w2)).normalized;
        }
        else
        {
            return d34;
        }
    }

    private Vector3 EvaluateHermiteSegment(Vector3 pA, Vector3 tA, Vector3 pB, Vector3 tB, float t, float tension)
    {
        float d = Vector3.Distance(pA, pB);
        float scale = Mathf.Clamp(tension, 0.5f, 2.0f) * 0.65f;
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
