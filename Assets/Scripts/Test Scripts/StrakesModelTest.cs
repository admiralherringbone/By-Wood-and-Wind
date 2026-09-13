using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [Tooltip("Target spacing distance along each frame's lower mould between the Rising Line and HoB.")]
    public float targetSpacing = 0.25f;

    [Tooltip("Draw on both Port (+X) and Starboard (-X) sides.")]
    public bool drawStarboard = true;

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
        float maxRisingZ = sheerPlan.cachedBowRisingEnd.z;
        float fwdZ = sheerPlan.GetFwdMostZ();
        if (minRisingZ > maxRisingZ)
        {
            float temp = minRisingZ;
            minRisingZ = maxRisingZ;
            maxRisingZ = temp;
        }

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

        int numSegments = Mathf.Max(1, Mathf.RoundToInt(masterArcLength / targetSpacing));

        foreach (float z in cachedStations)
        {
            if (z > maxRisingZ + 0.001f && framingModel != null)
            {
                cachedStationPoints.Add(framingModel.GetForwardFrameStrakePoints(z, 1f, numSegments));
            }
            else
            {
                cachedStationPoints.Add(GetPointsOnFrameWithSegmentCount(z, numSegments));
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

        int lineCount = (cachedStationPoints[0] != null) ? cachedStationPoints[0].Count : 0;
        if (lineCount == 0) return;

        // Draw interpolated lines running longitudinally across frame stations
        for (int i = 0; i < lineCount; i++)
        {
            float t = (lineCount > 1) ? (float)i / (lineCount - 1) : 0f;
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

        // Complete the HoB line strake (top index = lineCount - 1) all the way to the bow
        int hobStrakeIndex = lineCount - 1;
        float lastZ = cachedStations[cachedStations.Count - 1];

        if (cachedBowHobZ > lastZ + 0.001f)
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
