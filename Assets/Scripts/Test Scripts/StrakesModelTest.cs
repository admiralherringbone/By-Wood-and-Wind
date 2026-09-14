using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum StrakeSizingMode
{
    FixedStrakeCount,       // Master frame determines strake count; all frames have same number of divisions (tapered width)
    AbsoluteDistance        // Points placed at fixed targetSpacing distances from origin (smooth parallel strakes, no zig-zags)
}

[ExecuteInEditMode]
public class StrakesModelTest : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FramingModelWaterlines waterlines;

    [Header("Settings")]
    [Tooltip("Method for sizing strakes across frames (Fixed Count vs. Absolute Distance Placement).")]
    public StrakeSizingMode strakeSizingMode = StrakeSizingMode.AbsoluteDistance;

    [Tooltip("Target spacing distance along each frame's lower mould between the Rising Line and HoB.")]
    public float targetSpacing = 0.25f;

    [Tooltip("When using AbsoluteDistance mode, anchor points from Height of Breadth (Top) downward instead of Rising Line (Bottom) upward.")]
    public bool anchorFromTop = false;

    [Tooltip("Draw on both Port (+X) and Starboard (-X) sides.")]
    public bool drawStarboard = true;

    [Header("Point Visualization Settings")]
    [Tooltip("Draw point markers (spheres) at calculated strake positions.")]
    public bool showStrakePoints = true;

    [Tooltip("Only draw point markers on forward frames/stem ahead of the Bow End of the Rising Line.")]
    public bool showForwardPointsOnly = true;

    [Tooltip("Radius of the strake point marker spheres.")]
    public float strakePointRadius = 0.04f;

    private bool isCacheDirty = true;
    private List<List<Vector3>> cachedStationPoints = new List<List<Vector3>>();
    private List<float> cachedStations = new List<float>();
    private float cachedBowHobZ;

    private void OnValidate()
    {
        isCacheDirty = true;
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
        if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();
        if (waterlines == null) waterlines = FindFirstObjectByType<FramingModelWaterlines>();
    }

    private void RebuildCache()
    {
        cachedStationPoints.Clear();
        cachedStations.Clear();

        if (bodyPlan == null || sheerPlan == null) return;

        bodyPlan.UpdateCachedCurves();
        sheerPlan.UpdateCachedValues();

        List<float> rawStations = (framingModel != null)
            ? framingModel.CalculateFrameStations()
            : new List<float> { 0f };

        if (rawStations == null || rawStations.Count == 0) return;

        float minRisingZ = Mathf.Min(sheerPlan.cachedSternRisingEnd.z, sheerPlan.cachedProjectedSternRisingEnd.z);
        float bowRisingZ = sheerPlan.cachedBowRisingEnd.z;
        float fwdZ = sheerPlan.GetFwdMostZ();

        foreach (float z in rawStations)
        {
            if (z >= minRisingZ - 0.001f && z <= fwdZ + 0.001f)
            {
                cachedStations.Add(z);
            }
        }
        cachedStations.Sort();

        if (cachedStations.Count == 0) return;

        float masterArcLength = GetFrameLowerMouldArcLength(0f);
        if (masterArcLength <= 0.0001f) masterArcLength = 1f;

        int masterSegments = Mathf.Max(1, Mathf.RoundToInt(masterArcLength / targetSpacing));

        foreach (float z in cachedStations)
        {
            if (strakeSizingMode == StrakeSizingMode.AbsoluteDistance)
            {
                cachedStationPoints.Add(GetPointsOnFrameAbsoluteDistance(z, targetSpacing, anchorFromTop));
            }
            else
            {
                if (z > bowRisingZ + 0.001f && framingModel != null)
                {
                    cachedStationPoints.Add(framingModel.GetForwardFrameStrakePoints(z, 1f, masterSegments));
                }
                else
                {
                    cachedStationPoints.Add(GetPointsOnFrameWithSegmentCount(z, masterSegments));
                }
            }
        }

        cachedBowHobZ = sheerPlan.cachedHobBowIntersect.z;
        isCacheDirty = false;
    }

    private void OnDrawGizmos()
    {
        AutoFindReferences();
        if (bodyPlan == null || sheerPlan == null) return;

        if (isCacheDirty)
        {
            RebuildCache();
        }

        if (cachedStationPoints.Count == 0 || cachedStations.Count == 0) return;

        int maxLineCount = 0;
        for (int s = 0; s < cachedStationPoints.Count; s++)
        {
            if (cachedStationPoints[s] != null && cachedStationPoints[s].Count > maxLineCount)
            {
                maxLineCount = cachedStationPoints[s].Count;
            }
        }
        if (maxLineCount == 0) return;

        // Draw interpolated lines running longitudinally across frame stations
        for (int i = 0; i < maxLineCount; i++)
        {
            float t = (maxLineCount > 1) ? (float)i / (maxLineCount - 1) : 0f;
            Gizmos.color = Color.Lerp(Color.green, new Color(1f, 0.5f, 0f), t); // Green (Rising Line) to Orange (HoB)

            // Port side (+X)
            for (int s = 0; s < cachedStations.Count - 1; s++)
            {
                if (i < cachedStationPoints[s].Count && i < cachedStationPoints[s + 1].Count)
                {
                    Gizmos.DrawLine(cachedStationPoints[s][i], cachedStationPoints[s + 1][i]);
                }
            }

            // Starboard side (-X)
            if (drawStarboard)
            {
                for (int s = 0; s < cachedStations.Count - 1; s++)
                {
                    if (i < cachedStationPoints[s].Count && i < cachedStationPoints[s + 1].Count)
                    {
                        Vector3 p1 = new Vector3(-cachedStationPoints[s][i].x, cachedStationPoints[s][i].y, cachedStationPoints[s][i].z);
                        Vector3 p2 = new Vector3(-cachedStationPoints[s + 1][i].x, cachedStationPoints[s + 1][i].y, cachedStationPoints[s + 1][i].z);
                        Gizmos.DrawLine(p1, p2);
                    }
                }
            }
        }

        // Complete the HoB line strake (top index of last station) all the way to the bow
        int lastStationPointCount = cachedStationPoints[cachedStations.Count - 1].Count;
        int hobStrakeIndex = lastStationPointCount - 1;
        float lastZ = cachedStations[cachedStations.Count - 1];

        if (cachedBowHobZ > lastZ + 0.001f && hobStrakeIndex >= 0)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f); // Orange (HoB Line)

            Vector3 prevPortHob = cachedStationPoints[cachedStations.Count - 1][hobStrakeIndex];
            Vector3 prevStbdHob = new Vector3(-prevPortHob.x, prevPortHob.y, prevPortHob.z);

            const int bowSteps = 20;
            for (int k = 1; k <= bowSteps; k++)
            {
                float z = Mathf.Lerp(lastZ, cachedBowHobZ, (float)k / bowSteps);
                float x = (breadthPlan != null) ? breadthPlan.GetTheoreticalBreadthXAtZ(z) : prevPortHob.x;
                float y = sheerPlan.GetTheoreticalHobYAtZ(z);

                Vector3 currPortHob = new Vector3(x, y, z);
                Vector3 currStbdHob = new Vector3(-x, y, z);

                // Port (+X)
                Gizmos.DrawLine(prevPortHob, currPortHob);
                prevPortHob = currPortHob;

                // Starboard (-X)
                if (drawStarboard)
                {
                    Gizmos.DrawLine(prevStbdHob, currStbdHob);
                    prevStbdHob = currStbdHob;
                }
            }
        }

        // Draw strake point markers (spheres) on stations
        if (showStrakePoints)
        {
            float bowRisingZ = (sheerPlan != null) ? sheerPlan.cachedBowRisingEnd.z : 0f;

            for (int s = 0; s < cachedStations.Count; s++)
            {
                float z = cachedStations[s];

                // If showForwardPointsOnly is true, skip stations at or behind the bow end of the rising line
                if (showForwardPointsOnly && z <= bowRisingZ + 0.001f)
                    continue;

                List<Vector3> pts = cachedStationPoints[s];
                if (pts == null) continue;

                int pCount = pts.Count;
                for (int i = 0; i < pCount; i++)
                {
                    float t = (pCount > 1) ? (float)i / (pCount - 1) : 0f;
                    Gizmos.color = Color.Lerp(Color.green, new Color(1f, 0.5f, 0f), t);

                    Vector3 ptPort = pts[i];
                    Gizmos.DrawSphere(ptPort, strakePointRadius);

                    if (drawStarboard)
                    {
                        Vector3 ptStbd = new Vector3(-ptPort.x, ptPort.y, ptPort.z);
                        Gizmos.DrawSphere(ptStbd, strakePointRadius);
                    }
                }
            }
        }
    }

    private List<Vector3> GetPointsOnFrameAbsoluteDistance(float z, float stepSpacing, bool anchorFromTop)
    {
        if (stepSpacing <= 0.001f) stepSpacing = 0.25f;

        float[] cumDistances;
        Vector3[] sampledPts;
        float totalArcLength;

        SampleStationPath(z, out cumDistances, out sampledPts, out totalArcLength);

        if (totalArcLength <= 0.0001f || sampledPts == null || sampledPts.Length == 0)
            return new List<Vector3>();

        List<Vector3> points = new List<Vector3>();
        int numFullSteps = Mathf.FloorToInt(totalArcLength / stepSpacing);
        int sampleCount = sampledPts.Length - 1;

        if (anchorFromTop)
        {
            // Anchored from Top (HoB) down to Bottom (Rising Line/Stem origin)
            for (int k = 0; k <= numFullSteps; k++)
            {
                float targetDist = totalArcLength - (k * stepSpacing);
                Vector3 pt = SamplePointFromArcLengthArray(targetDist, cumDistances, sampledPts, sampleCount);
                points.Add(pt);
            }

            float remDist = totalArcLength - (numFullSteps * stepSpacing);
            if (remDist > 0.001f)
            {
                points.Add(sampledPts[0]);
            }
        }
        else
        {
            // Anchored from Bottom (Rising Line/Stem origin) up to Top (HoB)
            for (int k = 0; k <= numFullSteps; k++)
            {
                float targetDist = k * stepSpacing;
                Vector3 pt = SamplePointFromArcLengthArray(targetDist, cumDistances, sampledPts, sampleCount);
                points.Add(pt);
            }

            float remDist = totalArcLength - (numFullSteps * stepSpacing);
            if (remDist > 0.001f)
            {
                points.Add(sampledPts[sampledPts.Length - 1]);
            }
        }

        return points;
    }

    private void SampleStationPath(float z, out float[] cumDistances, out Vector3[] sampledPts, out float totalArcLength)
    {
        float bowRisingZ = (sheerPlan != null) ? sheerPlan.cachedBowRisingEnd.z : 0f;

        if (z > bowRisingZ + 0.001f && framingModel != null)
        {
            List<Vector3> rawPts = framingModel.GetForwardFrameStrakePoints(z, 1f, 50);
            int samples = (rawPts != null && rawPts.Count > 0) ? rawPts.Count - 1 : 0;
            if (samples < 1)
            {
                cumDistances = new float[1];
                sampledPts = new Vector3[1];
                totalArcLength = 0f;
                return;
            }

            sampledPts = rawPts.ToArray();
            cumDistances = new float[samples + 1];
            cumDistances[0] = 0f;
            totalArcLength = 0f;

            for (int i = 1; i <= samples; i++)
            {
                float dist = Vector3.Distance(sampledPts[i - 1], sampledPts[i]);
                totalArcLength += dist;
                cumDistances[i] = totalArcLength;
            }
        }
        else
        {
            const int samples = 50;
            sampledPts = new Vector3[samples + 1];
            cumDistances = new float[samples + 1];

            Vector3 prevPt = (framingModel != null)
                ? framingModel.GetTransverseFramePointAtU(z, 1f, 0.25f)
                : bodyPlan.EvaluateLowerMouldAtT(0.5f);

            sampledPts[0] = prevPt;
            cumDistances[0] = 0f;
            totalArcLength = 0f;

            for (int i = 1; i <= samples; i++)
            {
                float u = Mathf.Lerp(0.25f, 0.5f, (float)i / samples);
                Vector3 currPt = (framingModel != null)
                    ? framingModel.GetTransverseFramePointAtU(z, 1f, u)
                    : bodyPlan.EvaluateLowerMouldAtT(u * 2f);

                sampledPts[i] = currPt;
                float dist = Vector3.Distance(prevPt, currPt);
                totalArcLength += dist;
                cumDistances[i] = totalArcLength;
                prevPt = currPt;
            }
        }
    }

    private float GetForwardFrameArcLength(float z)
    {
        if (sheerPlan == null) return 1f;

        float hw = (bodyPlan != null) ? bodyPlan.keelWidth / 2f : 0.25f;
        float bowRisingZ = sheerPlan.cachedBowRisingEnd.z;

        // 1. Stem profile segment arc length
        const int samples = 20;
        float totalDist = 0f;
        Vector3 prevStemPt = new Vector3(hw, sheerPlan.cachedBowRisingEnd.y, bowRisingZ);
        for (int i = 1; i <= samples; i++)
        {
            float sz = Mathf.Lerp(bowRisingZ, z, (float)i / samples);
            float sy = sheerPlan.GetStemProfileYAtZ(sz);
            Vector3 currStemPt = new Vector3(hw, sy, sz);
            totalDist += Vector3.Distance(prevStemPt, currStemPt);
            prevStemPt = currStemPt;
        }

        // 2. Frame mould segment arc length (u=0.25 to u=0.50)
        Vector3 pL0_curve, pL1, pLR, pL2, pL3;
        float profileY, projY;
        if (framingModel != null && framingModel.GetAdjustedFrameControlPointsAtZ(z, 1f, out pL0_curve, out pL1, out pLR, out pL2, out pL3, out profileY, out projY))
        {
            Vector3 prevFramePt = pLR;
            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / samples;
                Vector3 currFramePt = CalculateQuadraticBezierPoint(t, pLR, pL2, pL3);
                totalDist += Vector3.Distance(prevFramePt, currFramePt);
                prevFramePt = currFramePt;
            }
        }
        else
        {
            totalDist += GetFrameLowerMouldArcLength(z);
        }

        return totalDist;
    }

    private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    private float GetFrameLowerMouldArcLength(float z)
    {
        const int samples = 40;
        float totalArcLength = 0f;
        Vector3 prevPt = (framingModel != null)
            ? framingModel.GetTransverseFramePointAtU(z, 1f, 0.25f)
            : bodyPlan.EvaluateLowerMouldAtT(0.5f);

        for (int i = 1; i <= samples; i++)
        {
            float u = Mathf.Lerp(0.25f, 0.5f, (float)i / samples);
            Vector3 currPt = (framingModel != null)
                ? framingModel.GetTransverseFramePointAtU(z, 1f, u)
                : bodyPlan.EvaluateLowerMouldAtT(u * 2f);

            float dist = Vector3.Distance(prevPt, currPt);
            totalArcLength += dist;
            prevPt = currPt;
        }
        return totalArcLength;
    }

    /// <summary>
    /// Computes points along a frame station's lower mould curve using a fixed segment count.
    /// </summary>
    private List<Vector3> GetPointsOnFrameWithSegmentCount(float z, int numSegments)
    {
        if (numSegments < 1) numSegments = 1;

        const int samples = 40;
        float totalArcLength = 0f;
        Vector3 prevPt = (framingModel != null)
            ? framingModel.GetTransverseFramePointAtU(z, 1f, 0.25f)
            : bodyPlan.EvaluateLowerMouldAtT(0.5f);

        Vector3[] sampledPts = new Vector3[samples + 1];
        sampledPts[0] = prevPt;

        float[] cumDistances = new float[samples + 1];
        cumDistances[0] = 0f;

        for (int i = 1; i <= samples; i++)
        {
            float u = Mathf.Lerp(0.25f, 0.5f, (float)i / samples);
            Vector3 currPt = (framingModel != null)
                ? framingModel.GetTransverseFramePointAtU(z, 1f, u)
                : bodyPlan.EvaluateLowerMouldAtT(u * 2f);

            sampledPts[i] = currPt;
            float dist = Vector3.Distance(prevPt, currPt);
            totalArcLength += dist;
            cumDistances[i] = totalArcLength;
            prevPt = currPt;
        }

        if (totalArcLength <= 0.0001f) return new List<Vector3>();

        float stepArcLength = totalArcLength / numSegments;
        List<Vector3> points = new List<Vector3>(numSegments + 1);

        for (int i = 0; i <= numSegments; i++)
        {
            float targetDist = i * stepArcLength;
            Vector3 pt = SamplePointFromArcLengthArray(targetDist, cumDistances, sampledPts, samples);
            points.Add(pt);
        }

        return points;
    }

    private Vector3 SamplePointFromArcLengthArray(float targetDist, float[] cumDistances, Vector3[] sampledPts, int samples)
    {
        if (targetDist <= 0f) return sampledPts[0];
        if (targetDist >= cumDistances[samples]) return sampledPts[samples];

        int low = 0;
        int high = samples;
        while (low < high - 1)
        {
            int mid = (low + high) / 2;
            if (cumDistances[mid] <= targetDist) low = mid;
            else high = mid;
        }

        float segLen = cumDistances[high] - cumDistances[low];
        float fraction = (segLen > 0.00001f) ? (targetDist - cumDistances[low]) / segLen : 0f;

        return Vector3.Lerp(sampledPts[low], sampledPts[high], fraction);
    }
}
