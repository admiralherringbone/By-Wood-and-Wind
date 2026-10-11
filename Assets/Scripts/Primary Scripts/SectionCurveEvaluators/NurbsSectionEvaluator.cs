using UnityEngine;

public class NurbsSectionEvaluator : ISectionCurveEvaluator
{
    public SectionCurveMode Mode => SectionCurveMode.NurbsBSpline;
    public string DisplayName => "Cubic B-Spline (Modern CAD)";
    public bool UsesControlArms => false;

    public Vector3 EvaluateSectionPoint(float u, SectionNodes nodes, float floorHollow, float tension, float keelEntryInfluence = 0.0f)
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
            Vector3 nurbsPt = EvaluateInterpolatingSegment(P0, T0, P1, T1, t, keelEntryInfluence);
            return Vector3.Lerp(linearPt, nurbsPt, floorHollow);
        }
        else if (u <= 0.50f)
        {
            float t = (u - 0.25f) * 4f;
            return EvaluateInterpolatingSegment(P1, T1, P2, T2, t);
        }
        else if (u <= 0.75f)
        {
            float t = (u - 0.50f) * 4f;
            return EvaluateInterpolatingSegment(P2, T2, P3, T3, t);
        }
        else
        {
            float t = (u - 0.75f) * 4f;
            return EvaluateInterpolatingSegment(P3, T3, P4, T4, t);
        }
    }

    public Vector3 GetNodeTangent(int nodeIndex, SectionNodes nodes)
    {
        Vector3 P0 = nodes.p0;
        Vector3 P1 = nodes.p1;
        Vector3 P2 = nodes.p2;
        Vector3 P3 = nodes.p3;
        Vector3 P4 = nodes.p4;

        switch (nodeIndex)
        {
            case 0: return (P1 - P0).sqrMagnitude > 0.0001f ? (P1 - P0).normalized : Vector3.up;
            case 1: return (P2 - P0).sqrMagnitude > 0.0001f ? (P2 - P0).normalized : Vector3.right;
            case 2: return (P3 - P1).sqrMagnitude > 0.0001f ? (P3 - P1).normalized : Vector3.right;
            case 3: return Vector3.up;
            case 4: return (P4 - P2).sqrMagnitude > 0.0001f ? (P4 - P2).normalized : Vector3.up;
            default: return Vector3.up;
        }
    }

    private Vector3 EvaluateInterpolatingSegment(Vector3 pA, Vector3 tA, Vector3 pB, Vector3 tB, float t, float startTangentWeight = 1.0f)
    {
        t = Mathf.Clamp01(t);
        float dist = Vector3.Distance(pA, pB);
        if (dist <= 0.0001f) return pA;

        Vector3 cA = pA + tA * (dist / 3f) * Mathf.Clamp01(startTangentWeight);
        Vector3 cB = pB - tB * (dist / 3f);

        float u = 1f - t;
        float u2 = u * u;
        float u3 = u2 * u;
        float t2 = t * t;
        float t3 = t2 * t;

        return (u3 * pA) + (3f * u2 * t * cA) + (3f * u * t2 * cB) + (t3 * pB);
    }
}
