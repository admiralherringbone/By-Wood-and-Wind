using UnityEngine;

public class QuarticBezierEvaluator : ISectionCurveEvaluator
{
    public SectionCurveMode Mode => SectionCurveMode.QuarticBezier;
    public string DisplayName => "Quartic Bezier (Single Continuous Arc)";
    public bool UsesControlArms => false;

    public Vector3 EvaluateSectionPoint(float u, SectionNodes nodes, float floorHollow, float tension, float keelEntryInfluence = 0.0f)
    {
        u = Mathf.Clamp01(u);
        Vector3 P0 = nodes.p0;
        Vector3 P1 = nodes.p1;
        Vector3 P2 = nodes.p2;
        Vector3 P3 = nodes.p3;
        Vector3 P4 = nodes.p4;

        if (u <= 0.25f)
        {
            float t = u * 4f;
            Vector3 linearPt = Vector3.Lerp(P0, P1, t);
            Vector3 bezierPt = EvaluateQuarticBezier(P0, P1, P2, P3, P4, u);
            return Vector3.Lerp(linearPt, bezierPt, floorHollow);
        }
        else
        {
            return EvaluateQuarticBezier(P0, P1, P2, P3, P4, u);
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
            case 0: return Vector3.up;
            case 1: return (P2 - P0).normalized;
            case 2: return (P3 - P1).normalized;
            case 3: return Vector3.up;
            case 4: return (P4 - P3).normalized;
            default: return Vector3.up;
        }
    }

    private Vector3 EvaluateQuarticBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, float t)
    {
        float u = 1f - t;
        float u2 = u * u;
        float u3 = u2 * u;
        float u4 = u3 * u;

        float t2 = t * t;
        float t3 = t2 * t;
        float t4 = t3 * t;

        return (u4 * p0) + (4f * u3 * t * p1) + (6f * u2 * t2 * p2) + (4f * u * t3 * p3) + (t4 * p4);
    }
}
