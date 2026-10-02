using UnityEngine;

public class BiArcTangentSweepEvaluator : ISectionCurveEvaluator
{
    public SectionCurveMode Mode => SectionCurveMode.BiArcTangentSweeps;
    public string DisplayName => "Bi-Arc Tangent Sweeps (Chapman)";
    public bool UsesControlArms => false;

    public Vector3 EvaluateSectionPoint(float u, SectionNodes nodes, float floorHollow, float tension)
    {
        u = Mathf.Clamp01(u);
        float sideSign = Mathf.Sign(nodes.p3.x);

        // Compute segment lengths and directions
        float d01 = Vector3.Distance(nodes.p0, nodes.p1);
        float d12 = Vector3.Distance(nodes.p1, nodes.p2);
        float d23 = Vector3.Distance(nodes.p2, nodes.p3);
        float d34 = Vector3.Distance(nodes.p3, nodes.p4);

        Vector3 v01 = d01 > 0.0001f ? (nodes.p1 - nodes.p0) / d01 : Vector3.up;
        Vector3 v12 = d12 > 0.0001f ? (nodes.p2 - nodes.p1) / d12 : new Vector3(sideSign, 0f, 0f);
        Vector3 v23 = d23 > 0.0001f ? (nodes.p3 - nodes.p2) / d23 : new Vector3(sideSign, 1f, 0f).normalized;
        Vector3 v34 = d34 > 0.0001f ? (nodes.p4 - nodes.p3) / d34 : Vector3.up;

        // Continuous tangency chain across all nodes:
        // T0: Keel (vertical garboard)
        // T1: Floor (weighted average fairing between Keel-Floor & Bilge-Floor)
        // T2: Bilge (weighted average fairing between Bilge-Floor & HOB-Bilge)
        // T3: HOB (vertical max breadth)
        // T4: Sheer (topsides direction)
        Vector3 T0 = Vector3.up;
        Vector3 T1 = (v01 * d12 + v12 * d01).sqrMagnitude > 0.0001f ? (v01 * d12 + v12 * d01).normalized : v12;
        Vector3 T2 = (v12 * d23 + v23 * d12).sqrMagnitude > 0.0001f ? (v12 * d23 + v23 * d12).normalized : v23;
        Vector3 T3 = Vector3.up;
        Vector3 T4 = v34;

        if (u <= 0.25f)
        {
            float t = u * 4f;
            Vector3 linearPt = Vector3.Lerp(nodes.p0, nodes.p1, t);
            Vector3 biArcPt = EvaluateBiArcSegment(nodes.p0, T0, nodes.p1, T1, t);
            return Vector3.Lerp(linearPt, biArcPt, floorHollow);
        }
        else if (u <= 0.50f)
        {
            float t = (u - 0.25f) * 4f;
            return EvaluateBiArcSegment(nodes.p1, T1, nodes.p2, T2, t);
        }
        else if (u <= 0.75f)
        {
            float t = (u - 0.50f) * 4f;
            return EvaluateBiArcSegment(nodes.p2, T2, nodes.p3, T3, t);
        }
        else
        {
            float t = (u - 0.75f) * 4f;
            return EvaluateBiArcSegment(nodes.p3, T3, nodes.p4, T4, t);
        }
    }

    public Vector3 GetNodeTangent(int nodeIndex, SectionNodes nodes)
    {
        float sideSign = Mathf.Sign(nodes.p3.x);

        float d01 = Vector3.Distance(nodes.p0, nodes.p1);
        float d12 = Vector3.Distance(nodes.p1, nodes.p2);
        float d23 = Vector3.Distance(nodes.p2, nodes.p3);
        float d34 = Vector3.Distance(nodes.p3, nodes.p4);

        Vector3 v01 = d01 > 0.0001f ? (nodes.p1 - nodes.p0) / d01 : Vector3.up;
        Vector3 v12 = d12 > 0.0001f ? (nodes.p2 - nodes.p1) / d12 : new Vector3(sideSign, 0f, 0f);
        Vector3 v23 = d23 > 0.0001f ? (nodes.p3 - nodes.p2) / d23 : new Vector3(sideSign, 1f, 0f).normalized;
        Vector3 v34 = d34 > 0.0001f ? (nodes.p4 - nodes.p3) / d34 : Vector3.up;

        Vector3 T1 = (v01 * d12 + v12 * d01).sqrMagnitude > 0.0001f ? (v01 * d12 + v12 * d01).normalized : v12;
        Vector3 T2 = (v12 * d23 + v23 * d12).sqrMagnitude > 0.0001f ? (v12 * d23 + v23 * d12).normalized : v23;

        switch (nodeIndex)
        {
            case 0: return Vector3.up;
            case 1: return T1;
            case 2: return T2;
            case 3: return Vector3.up;
            case 4: return v34;
            default: return Vector3.up;
        }
    }

    private Vector3 EvaluateBiArcSegment(Vector3 pA, Vector3 tA, Vector3 pB, Vector3 tB, float t)
    {
        t = Mathf.Clamp01(t);
        Vector3 d = pB - pA;
        float dist = d.magnitude;
        if (dist <= 0.0001f) return pA;

        Vector3 control;
        float denom = tA.x * tB.y - tA.y * tB.x;
        if (Mathf.Abs(denom) > 0.0001f)
        {
            float s = ((pB.x - pA.x) * tB.y - (pB.y - pA.y) * tB.x) / denom;
            s = Mathf.Clamp(s, 0.01f, dist * 1.5f);
            control = pA + tA * s;
        }
        else
        {
            control = (pA + pB) * 0.5f;
        }

        float u = 1f - t;
        return (u * u * pA) + (2f * u * t * control) + (t * t * pB);
    }
}
