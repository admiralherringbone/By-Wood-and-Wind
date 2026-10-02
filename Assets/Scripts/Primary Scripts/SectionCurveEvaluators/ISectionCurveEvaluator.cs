using UnityEngine;

public enum SectionCurveMode
{
    FairSplineBatten,
    BiArcTangentSweeps,
    AkimaSpline,
    NurbsBSpline,
    QuarticBezier
}

public struct SectionNodes
{
    public Vector3 p0; // Keel Baseline
    public Vector3 p1; // Floor Line (Rising Line)
    public Vector3 p2; // Diagonal Bilge Point
    public Vector3 p3; // Height of Breadth (Max Beam)
    public Vector3 p4; // Sheer Line (Top Timber)
}

public interface ISectionCurveEvaluator
{
    SectionCurveMode Mode { get; }
    string DisplayName { get; }
    bool UsesControlArms { get; }
    Vector3 EvaluateSectionPoint(float u, SectionNodes nodes, float floorHollow, float tension);
    Vector3 GetNodeTangent(int nodeIndex, SectionNodes nodes);
}
