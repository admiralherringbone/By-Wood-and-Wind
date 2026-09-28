using System.Collections.Generic;
using UnityEngine;

public enum FrameSpacingModeTest
{
    UniformZ,
    PrimaryBreadthCurve
}

[ExecuteInEditMode]
public class FramingModelTest : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlanTest sheerPlan;
    public BodyPlanTest bodyPlan;
    public BreadthPlanTest breadthPlan;
    public FashionTimberModel fashionTimberModel;

    [Header("Frame Layout Settings")]
    [Tooltip("Method for spacing transverse frames along the hull (Uniform Z vs. Primary Breadth curve).")]
    public FrameSpacingModeTest frameSpacingMode = FrameSpacingModeTest.UniformZ;

    [Tooltip("Spacing distance between adjacent transverse frames along the Z axis (or along Primary Breadth curve).")]
    public float frameSpacing = 0.8f;

    [Tooltip("Cut frames as they pass below the Fashion Timbers on the Y-axis.")]
    public bool cutFramesAtFashionTimber = true;

    [Tooltip("Disable all frame cutoffs to display frames completely whole and uncut by Fashion Timbers, Upper Decks, or Sheer line profile.")]
    public bool disableFrameCutoffs = false;

    [Header("Angled Frames Overlay Settings")]
    [Tooltip("Overlay angled frame lines radiating from the Bow End of the Rising Line to the Stem atop the primary transverse frames.")]
    public bool showAngledFrames = true;

    [Tooltip("Angle increment (degrees) between adjacent angled frames radiating from the Bow End of the Rising Line.")]
    public float angledFrameAngleStep = 15f;

    [Range(10, 300)]
    [Tooltip("Line segment resolution / detail steps for drawing angled frames.")]
    public int angledFrameResolution = 100;

    [Tooltip("Color of the overlaid angled frame lines.")]
    public Color angledFrameColor = new Color(1f, 0.9f, 0.2f, 0.9f);

    [Tooltip("Display degree labels for angled frames in the Scene View.")]
    public bool showAngledFrameLabels = true;

    [Header("Connection Point Blended Curve Settings")]
    [Tooltip("Blend ratio for forward connection points between Floor Line (0) and Primary Max Beam Line (1).")]
    [Range(0f, 1f)]
    public float connectionPointFwdBlend = 0.5f;

    [Tooltip("Blend ratio for aft connection points between Floor Line (0) and Primary Max Beam Line (1).")]
    [Range(0f, 1f)]
    public float connectionPointAftBlend = 0.5f;

    [Header("Control Point Visualization Settings")]
    [Tooltip("Show key control points (Keel Point, Connection Point, HOB Point) on each frame.")]
    public bool showFrameControlPoints = true;

    [Tooltip("Show red marking points on forward frames along the Projected Bow Rising Line.")]
    public bool showProjectedRisingMarkPoints = true;

    [Tooltip("Show control arm lines connecting frame Bezier control points.")]
    public bool showFrameControlArms = true;

    [Tooltip("Radius of the frame control point spheres.")]
    public float controlPointRadius = 0.05f;

    [Tooltip("Color of the Keel Point (pL0) at the keel line.")]
    public Color keelPointColor = Color.yellow;

    [Tooltip("Color of the Pseudo Keel Point (pPseudo) climbing the stem line or stern post / deadwood line.")]
    public Color pseudoKeelPointColor = Color.gray;

    [Tooltip("Color of the Connection Point (pLR) on the floor sweep.")]
    public Color connectionPointColor = Color.green;

    [System.Obsolete("Use connectionPointColor instead.")]
    public Color risingLinePointColor
    {
        get => connectionPointColor;
        set => connectionPointColor = value;
    }

    [Tooltip("Color of the marking point on forward frames for the Projected Bow Rising Line.")]
    public Color projectedRisingMarkColor = Color.red;

    [Range(1, 15)]
    [Tooltip("Number of strake division segments along forward frames from the projected mark on the vertical to HOB.")]
    public int projectedRisingMarkSegments = 4;

    [Tooltip("Color of the HOB Point (pL3) at the Height of Breadth.")]
    public Color hobPointColor = Color.cyan;

    [Tooltip("Color of the control arm lines connecting Bezier control points.")]
    public Color controlArmColor = new Color(1f, 0.5f, 0f, 0.8f);

    [Tooltip("Color of intermediate Bezier control handle points (pL1, pL2, pU1, pU2, pU3).")]
    public Color controlArmPointColor = new Color(1f, 0.8f, 0.2f, 1f);

    [Header("Keel Control Arm Model")]
    [Tooltip("Use Quadratic (t^2) scaling for Keel control arm to unload geometric tension and dive inward near stern/bow for a smooth rounded bilge.")]
    public bool useQuadraticScaling = true;

    [Header("Gizmo Visualization Settings")]
    public bool showLabels = true;
    public bool showBackboneSideLines = true;
    public bool showMasterFrame = true;
    public bool showFrames = true;

    [Tooltip("Overlay a grey projection guide line tracing the Rising Line points (pLR) across all frames.")]
    public bool showRisingLineGuide = true;

    [Tooltip("Overlay a guide line tracing the Keel Control Arm points (pL1) across all frames.")]
    public bool showKeelControlGuide = true;

    public Color starboardColor = Color.cyan;
    public Color portColor = Color.magenta;
    public Color masterFrameColor = Color.yellow;
    public Color frameColor = new Color(0.2f, 0.8f, 1f, 0.7f);

    [Tooltip("Color of the grey projection guide line tracing the Rising Line points (pLR).")]
    public Color risingLineGuideColor = Color.gray;

    [Tooltip("Color of the guide line tracing the Keel Control Arm points (pL1).")]
    public Color keelControlGuideColor = new Color(1f, 0.8f, 0.2f, 0.9f);

    private List<List<Vector3>> cachedAngledFrameLines = new List<List<Vector3>>();
    private bool isAngledFrameCacheDirty = true;
    private List<float> cachedStationList = new List<float>();
    private bool isStationListDirty = true;

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        isAngledFrameCacheDirty = true;
        isStationListDirty = true;
        FindReferences();
    }

    public void FindReferences()
    {
        if (sheerPlan == null)
        {
            sheerPlan = GetComponentInParent<SheerPlanTest>();
            if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlanTest>();
        }

        if (bodyPlan == null)
        {
            bodyPlan = GetComponentInParent<BodyPlanTest>();
            if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlanTest>();
        }

        if (breadthPlan == null)
        {
            breadthPlan = GetComponentInParent<BreadthPlanTest>();
            if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlanTest>();
        }

        if (fashionTimberModel == null)
        {
            fashionTimberModel = GetComponentInParent<FashionTimberModel>();
            if (fashionTimberModel == null) fashionTimberModel = FindFirstObjectByType<FashionTimberModel>();
        }
    }

    public float GetKeelHalfWidth()
    {
        if (bodyPlan != null) return bodyPlan.keelWidth / 2f;
        return 0.25f;
    }

    public float GetFrameBottomYAtZ(float z)
    {
        if (sheerPlan == null) return 0f;
        return sheerPlan.cachedMidshipKeel.y;
    }

    public float GetConnectionPointBlendAtZ(float z)
    {
        if (sheerPlan == null) return 0f;

        if (z >= 0f)
        {
            float bowZ = sheerPlan.cachedBowRisingEnd.z;
            if (bowZ <= 0.001f) return 0f;
            float t = Mathf.Clamp01(z / bowZ);
            return Mathf.Lerp(0f, connectionPointFwdBlend, t);
        }
        else
        {
            float sternZ = sheerPlan.cachedSternRisingEnd.z;
            if (sternZ >= -0.001f) return 0f;
            float t = Mathf.Clamp01(z / sternZ);
            return Mathf.Lerp(0f, connectionPointAftBlend, t);
        }
    }

    public List<float> CalculateFrameStations()
    {
        if (!isStationListDirty && cachedStationList != null && cachedStationList.Count > 0)
        {
            return cachedStationList;
        }

        List<float> stations = new List<float>();
        if (sheerPlan == null) return stations;

        sheerPlan.UpdateCachedValues();
        float aftZ = sheerPlan.GetAftMostZ();
        float fwdZ = sheerPlan.GetFwdMostZ();

        if (frameSpacing <= 0.05f) frameSpacing = 0.05f;

        // Master Frame at Z = 0
        stations.Add(0f);

        // Furthest forward station at bow
        stations.Add(fwdZ);

        if (frameSpacingMode == FrameSpacingModeTest.PrimaryBreadthCurve && breadthPlan != null)
        {
            breadthPlan.UpdateCachedCurves();

            float dZ = 0.02f;

            // Forward Stations (Z > 0)
            float curZ = 0f;
            float accDist = 0f;
            float prevX = breadthPlan.GetTheoreticalBreadthXAtZ(0f);

            while (curZ < fwdZ)
            {
                curZ += dZ;
                float currX = breadthPlan.GetTheoreticalBreadthXAtZ(curZ);
                float stepDist = Mathf.Sqrt(dZ * dZ + (currX - prevX) * (currX - prevX));
                accDist += stepDist;
                prevX = currX;

                if (accDist >= frameSpacing)
                {
                    stations.Add(curZ);
                    accDist -= frameSpacing;
                }
            }

            // Aft Stations (Z < 0)
            curZ = 0f;
            accDist = 0f;
            prevX = breadthPlan.GetTheoreticalBreadthXAtZ(0f);

            while (curZ > aftZ)
            {
                curZ -= dZ;
                float currX = breadthPlan.GetTheoreticalBreadthXAtZ(curZ);
                float stepDist = Mathf.Sqrt(dZ * dZ + (currX - prevX) * (currX - prevX));
                accDist += stepDist;
                prevX = currX;

                if (accDist >= frameSpacing)
                {
                    stations.Add(curZ);
                    accDist -= frameSpacing;
                }
            }
        }
        else
        {
            // Forward Stations (Z > 0)
            float curZ = frameSpacing;
            while (curZ <= fwdZ)
            {
                stations.Add(curZ);
                curZ += frameSpacing;
            }

            // Aft Stations (Z < 0)
            curZ = -frameSpacing;
            while (curZ >= aftZ)
            {
                stations.Add(curZ);
                curZ -= frameSpacing;
            }
        }

        // Upper Deck Boundary Frames (Add frame at fore/aft edge of any upper deck not at absolute ends)
        if (sheerPlan.upperDecks != null)
        {
            float startZ = sheerPlan.cachedTopTimberSternIntersect.z;
            float endZ = sheerPlan.cachedTopTimberBowIntersect.z;
            float totalLength = endZ - startZ;

            foreach (var deck in sheerPlan.upperDecks)
            {
                float deckAftZ = startZ + (deck.globalAftExtent * totalLength);
                float deckForeZ = startZ + (deck.globalForeExtent * totalLength);

                if (deck.globalAftExtent > 0.001f && deckAftZ > aftZ + 0.01f && deckAftZ < fwdZ - 0.01f)
                {
                    stations.Add(deckAftZ);
                }
                if (deck.globalForeExtent < 0.999f && deckForeZ < fwdZ - 0.01f && deckForeZ > aftZ + 0.01f)
                {
                    stations.Add(deckForeZ);
                }
            }
        }

        stations.Sort();

        // Filter out near-duplicate stations (within 2.5cm)
        List<float> uniqueStations = new List<float>();
        foreach (float s in stations)
        {
            if (uniqueStations.Count == 0 || Mathf.Abs(s - uniqueStations[uniqueStations.Count - 1]) > 0.025f)
            {
                uniqueStations.Add(s);
            }
        }

        // Ensure the absolute furthest forward station (fwdZ) is always included
        if (uniqueStations.Count > 0 && Mathf.Abs(uniqueStations[uniqueStations.Count - 1] - fwdZ) > 0.0001f)
        {
            if (Mathf.Abs(uniqueStations[uniqueStations.Count - 1] - fwdZ) <= 0.005f)
            {
                uniqueStations[uniqueStations.Count - 1] = fwdZ;
            }
            else
            {
                uniqueStations.Add(fwdZ);
            }
        }

        cachedStationList = uniqueStations;
        isStationListDirty = false;
        return cachedStationList;
    }

    public Vector3 EvaluateFramePointFromControlPoints(float u, Vector3 pL0, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3, Vector3 pU0, Vector3 pU1, Vector3 pU2, Vector3 pU3)
    {
        u = Mathf.Clamp01(u);
        if (u <= 0.5f)
        {
            float t = u * 2f;
            if (t <= 0.5f)
            {
                float localT = t * 2f;
                return CalculateQuadraticBezierPoint(localT, pL0, pL1, pLR);
            }
            else
            {
                float localT = (t - 0.5f) * 2f;
                return CalculateQuadraticBezierPoint(localT, pLR, pL2, pL3);
            }
        }
        else
        {
            float t = (u - 0.5f) * 2f;
            return CalculateCubicBezierPoint(t, pU0, pU1, pU2, pU3);
        }
    }

    public Vector3 GetTransverseFramePointAtU(float z, float side, float u)
    {
        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!GetFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return Vector3.zero;

        return EvaluateFramePointFromControlPoints(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
    }

    public Vector3 GetTransverseFramePointAtYFromControlPoints(
        Vector3 pL0, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3,
        Vector3 pU0, Vector3 pU1, Vector3 pU2, Vector3 pU3,
        float targetY)
    {
        float minU = 0f;
        float maxU = 1.0f;

        Vector3 minPt = EvaluateFramePointFromControlPoints(minU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
        Vector3 maxPt = EvaluateFramePointFromControlPoints(maxU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);

        if (targetY < minPt.y - 0.0005f)
        {
            float keelY = GetFrameBottomYAtZ(minPt.z);
            if (targetY >= keelY - 0.05f)
            {
                return new Vector3(minPt.x, targetY, minPt.z);
            }
            return minPt;
        }
        if (targetY >= maxPt.y) return maxPt;

        for (int i = 0; i < 10; i++)
        {
            float midU = (minU + maxU) * 0.5f;
            Vector3 midPt = EvaluateFramePointFromControlPoints(midU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
            if (midPt.y < targetY) minU = midU;
            else maxU = midU;
        }

        return EvaluateFramePointFromControlPoints((minU + maxU) * 0.5f, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
    }

    public Vector3 GetTransverseFramePointAtY(float z, float side, float targetY)
    {
        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!GetFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return Vector3.zero;

        return GetTransverseFramePointAtYFromControlPoints(pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3, targetY);
    }

    public List<Vector3> GetAllTransverseFramePointsAtX(float z, float side, float targetX)
    {
        List<Vector3> results = new List<Vector3>();
        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!GetFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return results;

        float absTargetX = Mathf.Abs(targetX);
        float sign = Mathf.Approximately(side, 0f) ? 1f : Mathf.Sign(side);
        float fixedX = sign * absTargetX;

        const int samples = 50;
        float prevU = 0f;
        Vector3 prevPt = EvaluateFramePointFromControlPoints(prevU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
        float prevX = Mathf.Abs(prevPt.x);

        for (int i = 1; i <= samples; i++)
        {
            float currU = (float)i / samples;
            Vector3 currPt = EvaluateFramePointFromControlPoints(currU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
            float currX = Mathf.Abs(currPt.x);

            // Check if absTargetX falls between prevX and currX
            if ((prevX <= absTargetX && absTargetX <= currX) || (currX <= absTargetX && absTargetX <= prevX))
            {
                float minU = prevU;
                float maxU = currU;

                for (int b = 0; b < 10; b++)
                {
                    float midU = (minU + maxU) * 0.5f;
                    Vector3 midPt = EvaluateFramePointFromControlPoints(midU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                    Vector3 minPt = EvaluateFramePointFromControlPoints(minU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                    Vector3 maxPt = EvaluateFramePointFromControlPoints(maxU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);

                    if (Mathf.Abs(maxPt.x - minPt.x) < 0.0001f) break;

                    if ((Mathf.Abs(midPt.x) < absTargetX && Mathf.Abs(minPt.x) <= Mathf.Abs(maxPt.x)) ||
                        (Mathf.Abs(midPt.x) > absTargetX && Mathf.Abs(minPt.x) > Mathf.Abs(maxPt.x)))
                    {
                        minU = midU;
                    }
                    else
                    {
                        maxU = midU;
                    }
                }

                Vector3 solvedPt = EvaluateFramePointFromControlPoints((minU + maxU) * 0.5f, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                Vector3 finalPt = new Vector3(fixedX, solvedPt.y, z);

                if (results.Count == 0 || Vector3.Distance(results[results.Count - 1], finalPt) > 0.005f)
                {
                    results.Add(finalPt);
                }
            }

            prevU = currU;
            prevX = currX;
        }

        if (results.Count == 0)
        {
            Vector3 minPt = EvaluateFramePointFromControlPoints(0f, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
            Vector3 maxPt = EvaluateFramePointFromControlPoints(1f, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);

            if (absTargetX <= Mathf.Abs(minPt.x)) results.Add(new Vector3(fixedX, minPt.y, z));
            else if (absTargetX >= Mathf.Abs(maxPt.x)) results.Add(new Vector3(fixedX, maxPt.y, z));
        }

        return results;
    }

    public Vector3 GetTransverseFramePointAtX(float z, float side, float targetX)
    {
        List<Vector3> pts = GetAllTransverseFramePointsAtX(z, side, targetX);
        if (pts.Count > 0) return pts[0];
        return Vector3.zero;
    }

    public float CalculateMasterFrameLowerMouldArcLength()
    {
        const int samples = 100;
        float totalArcLength = 0f;
        Vector3 prevPt = GetTransverseFramePointAtU(0f, 1f, 0.25f);
        for (int i = 1; i <= samples; i++)
        {
            float u = Mathf.Lerp(0.25f, 0.5f, (float)i / samples);
            Vector3 currPt = GetTransverseFramePointAtU(0f, 1f, u);
            totalArcLength += Vector3.Distance(prevPt, currPt);
            prevPt = currPt;
        }
        return totalArcLength;
    }

    public int GetStrakeSegmentCount()
    {
        return projectedRisingMarkSegments;
    }

    public bool GetAdjustedFrameControlPointsAtZ(float z, float side,
        out Vector3 pL0_curve, out Vector3 pL1, out Vector3 pLR, out Vector3 pL2, out Vector3 pL3,
        out float profileY, out float projY)
    {
        pL0_curve = pL1 = pLR = pL2 = pL3 = Vector3.zero;
        profileY = projY = 0f;
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null) return false;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        breadthPlan.UpdateCachedCurves();

        float hw = GetKeelHalfWidth();
        float keelY = GetFrameBottomYAtZ(z);
        float originalKeelY = keelY;
        float hobY = sheerPlan.GetTheoreticalHobYAtZ(z);
        float hobBreadth = breadthPlan.GetTheoreticalBreadthXAtZ(z);

        profileY = keelY;
        float sternRisingZ = (sheerPlan != null) ? sheerPlan.cachedSternRisingEnd.z : 0f;
        if (sheerPlan != null)
        {
            if (z > sheerPlan.cachedKeelBowPoint.z)
            {
                profileY = sheerPlan.GetStemProfileYAtZ(z);
            }
            else if (sheerPlan.showDeadwoodRake && z <= sheerPlan.cachedDeadwoodForePoint.z && z >= sternRisingZ - 0.001f)
            {
                profileY = sheerPlan.GetDeadwoodYAtZ(z);
            }
            else if (z < sternRisingZ - 0.001f)
            {
                profileY = sheerPlan.GetRisingLineYAtZ(z);
            }
        }

        projY = sheerPlan.GetProjectedBowRisingYAtZ(z);

        Vector3 backboneKeelPoint = new Vector3(side * hw, profileY, z);
        pL0_curve = backboneKeelPoint;

        // --- CONNECTION POINT (pLR) FOLLOWS FLOOR BREADTH LINE & RISING LINE ---
        float floorX = Mathf.Min(breadthPlan.GetFloorLineXAtZ(z), hobBreadth - 0.001f);
        float risingY = sheerPlan.GetRisingLineYAtZ(z);

        float pLR_Y = Mathf.Max(risingY, profileY);
        pLR = new Vector3(side * floorX, pLR_Y, z);

        if (profileY >= risingY - 0.001f)
        {
            pLR = backboneKeelPoint;
        }

        // --- EQUAL FLOOR BREADTH SPACING OR QUADRATIC t^2 SCALED CONTROL ARMS AT CONNECTION POINT (pLR) ---
        // 1. Proportional HOB lower arm drop calculation relative to Rising Line & HOB height
        float masterHobY = bodyPlan.cachedL3.y;
        float masterLR_Y = bodyPlan.cachedLR.y;
        float masterL2_Y = bodyPlan.cachedL2.y;

        float masterAvailHeight = Mathf.Max(0.001f, masterHobY - masterLR_Y);
        float masterArmDrop = masterHobY - masterL2_Y;
        float armDropRatio = masterArmDrop / masterAvailHeight;

        float localAvailHeight = Mathf.Max(0f, hobY - pLR.y);
        float localArmDrop = localAvailHeight * armDropRatio;
        float pL2_Y = hobY - localArmDrop;
        pL2_Y = Mathf.Clamp(pL2_Y, profileY, hobY);

        // 2. Position pL2 at (side * hobBreadth, pL2_Y, z) and pL3 at HOB
        pL2 = new Vector3(side * hobBreadth, pL2_Y, z);
        pL3 = new Vector3(side * hobBreadth, hobY, z);

        // 3. Evaluate 2D magnitude slope of tangent ray from pLR to pL2 for bilateral symmetry
        float deltaX_hob = Mathf.Max(0.0001f, hobBreadth - floorX);
        float deltaY_hob = pL2.y - pLR.y;
        float slope = deltaY_hob / deltaX_hob;

        // 4. Calculate pL1.x position (Quadratic t^2 scaling or Linear constant spacing)
        Vector3 masterL1 = bodyPlan.cachedL1;
        Vector3 masterLR = bodyPlan.cachedLR;

        float pL1_X_abs;
        if (useQuadraticScaling)
        {
            // Normalized floor breadth width factor t in [0, 1] relative to midship master floor line
            float floorX_mid = breadthPlan.GetFloorLineXAtZ(0f);
            float maxAvailBeam = Mathf.Max(0.001f, floorX_mid - hw);
            float t_width = Mathf.Clamp01((floorX - hw) / maxAvailBeam);
            float t_squared = t_width * t_width;

            float masterKeelSpan = Mathf.Max(0.001f, masterL1.x - hw);
            pL1_X_abs = hw + masterKeelSpan * t_squared;
        }
        else
        {
            // Linear constant horizontal spacing from Floor breadth line
            float deltaX_mid = Mathf.Abs(masterLR.x - masterL1.x);
            if (deltaX_mid <= 0.001f) deltaX_mid = 0.2f;
            pL1_X_abs = floorX - deltaX_mid;
        }

        pL1_X_abs = Mathf.Clamp(pL1_X_abs, hw, Mathf.Max(hw + 0.001f, floorX - 0.001f));
        float dx_abs = floorX - pL1_X_abs;
        float pL1_Y = Mathf.Max(profileY, pLR.y - dx_abs * slope);
        pL1 = new Vector3(side * pL1_X_abs, pL1_Y, z);

        // Edge case override when profile cutoff exceeds rising line height
        if (profileY >= risingY - 0.001f)
        {
            pLR = backboneKeelPoint;
            pL1 = backboneKeelPoint;
            pL2.y = Mathf.Max(pL2.y, profileY);
        }

        return true;
    }

    public List<Vector3> GetForwardFrameStrakePoints(float z, float side, int numSegments)
    {
        Vector3 pL0_curve, pL1, pLR, pL2, pL3;
        float profileY, projY;
        if (GetAdjustedFrameControlPointsAtZ(z, side, out pL0_curve, out pL1, out pLR, out pL2, out pL3, out profileY, out projY))
        {
            return GetForwardFrameStrakePoints(z, side, numSegments, pL0_curve, pL1, pLR, pL2, pL3, profileY, projY);
        }
        return new List<Vector3>();
    }

    public List<Vector3> GetForwardFrameStrakePoints(float z, float side, int numSegments,
        Vector3 pL0_curve, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3, float profileY, float projY)
    {
        List<Vector3> points = new List<Vector3>();
        if (numSegments < 1) return points;

        float hw = GetKeelHalfWidth();
        float bowRisingZ = (sheerPlan != null) ? sheerPlan.cachedBowRisingEnd.z : 0f;

        // 1. STEM PROFILE SEGMENT (from Bow End of Rising Line up to frame station z)
        const int stemSamples = 50;
        float dStem = 0f;
        float[] stemCumDistances = new float[stemSamples + 1];
        Vector3[] sampledStemPts = new Vector3[stemSamples + 1];

        if (z > bowRisingZ + 0.0001f && sheerPlan != null)
        {
            Vector3 prevStemPt = new Vector3(side * hw, sheerPlan.cachedBowRisingEnd.y, bowRisingZ);
            sampledStemPts[0] = prevStemPt;
            stemCumDistances[0] = 0f;

            for (int i = 1; i <= stemSamples; i++)
            {
                float sampleZ = Mathf.Lerp(bowRisingZ, z, (float)i / stemSamples);
                float sampleY = sheerPlan.GetStemProfileYAtZ(sampleZ);
                Vector3 currStemPt = new Vector3(side * hw, sampleY, sampleZ);
                sampledStemPts[i] = currStemPt;

                float dist = Vector3.Distance(prevStemPt, currStemPt);
                dStem += dist;
                stemCumDistances[i] = dStem;
                prevStemPt = currStemPt;
            }
        }
        else
        {
            Vector3 startPt = new Vector3(side * hw, profileY, z);
            sampledStemPts[0] = startPt;
            stemCumDistances[0] = 0f;
        }

        // 2. FRAME MOULD CURVE SEGMENT (at station z, from Stem profile u=0.25 to HOB u=0.50)
        Vector3 GetCurvePt(float uVal)
        {
            uVal = Mathf.Clamp(uVal, 0.25f, 0.5f);
            float t = (uVal - 0.25f) * 4f;
            return CalculateQuadraticBezierPoint(t, pLR, pL2, pL3);
        }

        const int frameSamples = 50;
        float dFrame = 0f;
        float[] frameCumDistances = new float[frameSamples + 1];
        Vector3[] sampledFramePts = new Vector3[frameSamples + 1];

        Vector3 prevFramePt = GetCurvePt(0.25f);
        sampledFramePts[0] = prevFramePt;
        frameCumDistances[0] = 0f;

        for (int i = 1; i <= frameSamples; i++)
        {
            float u = Mathf.Lerp(0.25f, 0.5f, (float)i / frameSamples);
            Vector3 currFramePt = GetCurvePt(u);
            sampledFramePts[i] = currFramePt;

            float dist = Vector3.Distance(prevFramePt, currFramePt);
            dFrame += dist;
            frameCumDistances[i] = dFrame;
            prevFramePt = currFramePt;
        }

        // 3. COMBINED PATH ARC LENGTH
        float totalDist = dStem + dFrame;
        if (totalDist <= 0.0001f)
        {
            Vector3 pStart = sampledStemPts[0];
            for (int k = 0; k <= numSegments; k++) points.Add(pStart);
            return points;
        }

        float stepDist = totalDist / numSegments;
        for (int k = 0; k <= numSegments; k++)
        {
            float targetDist = k * stepDist;

            if (targetDist <= dStem && dStem > 0.0001f)
            {
                // Point lies on the Stem profile segment
                int low = 0;
                int high = stemSamples;
                while (low < high - 1)
                {
                    int mid = (low + high) / 2;
                    if (stemCumDistances[mid] <= targetDist) low = mid;
                    else high = mid;
                }

                float segLen = stemCumDistances[high] - stemCumDistances[low];
                float fraction = (segLen > 0.00001f) ? (targetDist - stemCumDistances[low]) / segLen : 0f;
                points.Add(Vector3.Lerp(sampledStemPts[low], sampledStemPts[high], fraction));
            }
            else
            {
                // Point lies on the frame mould curve segment at station z
                float targetFrameDist = targetDist - dStem;
                int low = 0;
                int high = frameSamples;
                while (low < high - 1)
                {
                    int mid = (low + high) / 2;
                    if (frameCumDistances[mid] <= targetFrameDist) low = mid;
                    else high = mid;
                }

                float segLen = frameCumDistances[high] - frameCumDistances[low];
                float fraction = (segLen > 0.00001f) ? (targetFrameDist - frameCumDistances[low]) / segLen : 0f;
                points.Add(Vector3.Lerp(sampledFramePts[low], sampledFramePts[high], fraction));
            }
        }

        return points;
    }

    public bool GetFrameControlPointsAtZ(float z, float side,
        out Vector3 pL0, out Vector3 pL1, out Vector3 pLR, out Vector3 pL2, out Vector3 pL3,
        out Vector3 pU0, out Vector3 pU1, out Vector3 pU2, out Vector3 pU3)
    {
        pL0 = pL1 = pLR = pL2 = pL3 = pU0 = pU1 = pU2 = pU3 = Vector3.zero;
        float profileY, projY;
        if (!GetAdjustedFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out profileY, out projY))
            return false;

        float hobY = pL3.y;
        float hobBreadth = Mathf.Abs(pL3.x);
        float yTop = sheerPlan.GetPureSheerYAtZ(z);
        float xTop = breadthPlan.GetTopTimberXAtZ(z);

        pU0 = pL3;
        pU3 = new Vector3(side * xTop, yTop, z);

        float masterUpperDx = bodyPlan.cachedU3.x - bodyPlan.cachedU0.x;
        float masterUpperDy = bodyPlan.cachedU3.y - bodyPlan.cachedU0.y;

        float localUpperDx = xTop - hobBreadth;
        float localUpperDy = yTop - hobY;

        float sXUpper = Mathf.Abs(masterUpperDx) > 0.001f ? localUpperDx / masterUpperDx : 0f;
        float sYUpper = Mathf.Abs(masterUpperDy) > 0.001f ? localUpperDy / masterUpperDy : 0f;

        Vector3 masterU1Offset = bodyPlan.cachedU1 - bodyPlan.cachedU0;
        Vector3 masterU2Offset = bodyPlan.cachedU2 - bodyPlan.cachedU3;

        pU1 = pU0 + new Vector3(side * masterU1Offset.x * sXUpper, masterU1Offset.y * sYUpper, 0);
        pU2 = pU3 + new Vector3(side * masterU2Offset.x * sXUpper, masterU2Offset.y * sYUpper, 0);

        return true;
    }

    public Vector3 GetAngledFramePointAtU(float angleDeg, float side, float u)
    {
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null) return Vector3.zero;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        breadthPlan.UpdateCachedCurves();

        float zHeel = sheerPlan.cachedBowRisingEnd.z;
        float zFwd = sheerPlan.GetFwdMostZ();
        float hw = GetKeelHalfWidth();

        angleDeg = Mathf.Clamp(angleDeg, 0f, 90f);

        // Case 1: 90 degrees (Longitudinal Stem profile line on centerline)
        if (Mathf.Approximately(angleDeg, 90f))
        {
            float yBottom = sheerPlan.cachedBowRisingEnd.y;
            float yHob = sheerPlan.cachedHobBowIntersect.y;
            float yTop = sheerPlan.GetPureTopTimberYAtZ(sheerPlan.cachedUpperStemTopPoint.z);

            float targetY;
            if (u <= 0.5f)
            {
                float t = u * 2f;
                targetY = Mathf.Lerp(yBottom, yHob, t);
            }
            else
            {
                float t = (u - 0.5f) * 2f;
                targetY = Mathf.Lerp(yHob, yTop, t);
            }

            float stemMinZ = zHeel;
            float stemMaxZ = zFwd;
            for (int i = 0; i < 20; i++)
            {
                float midZ = (stemMinZ + stemMaxZ) * 0.5f;
                float profY = sheerPlan.GetStemProfileYAtZ(midZ);
                if (profY < targetY) stemMinZ = midZ;
                else stemMaxZ = midZ;
            }
            float stemZ = (stemMinZ + stemMaxZ) * 0.5f;
            float stemY = sheerPlan.GetStemProfileYAtZ(stemZ);
            return new Vector3(side * hw, stemY, stemZ);
        }

        // Case 2: 0 degrees (Pure transverse frame at Bow End of Rising Line)
        if (Mathf.Approximately(angleDeg, 0f))
        {
            return GetTransverseFramePointAtU(zHeel, side, u);
        }

        // Case 3: Angles between 0 and 90 degrees (Robust Bisection Search across [zHeel, zFwd])
        float tanPhi = Mathf.Tan(angleDeg * Mathf.Deg2Rad);

        float minZ = zHeel;
        float maxZ = zFwd;

        for (int iter = 0; iter < 8; iter++)
        {
            float midZ = (minZ + maxZ) * 0.5f;
            Vector3 midPt = GetTransverseFramePointAtU(midZ, side, u);
            float xHalfBreadth = Mathf.Max(0f, Mathf.Abs(midPt.x) - hw);
            float expectedZ = zHeel + xHalfBreadth * tanPhi;

            if (midZ < expectedZ)
            {
                minZ = midZ;
            }
            else
            {
                maxZ = midZ;
            }
        }

        float finalZ = (minZ + maxZ) * 0.5f;
        Vector3 rawPt = GetTransverseFramePointAtU(finalZ, side, u);

        // Ensure angled frame point Y does not dip below the stem profile line at finalZ
        float stemProfY = sheerPlan != null ? sheerPlan.GetStemProfileYAtZ(finalZ) : rawPt.y;
        if (rawPt.y < stemProfY)
        {
            rawPt.y = stemProfY;
        }

        return rawPt;
    }

    private Vector3 OffsetX(Vector3 point, float xOffset)
    {
        return new Vector3(xOffset, point.y, point.z);
    }

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null)
        {
            FindReferences();
            if (sheerPlan == null || bodyPlan == null || breadthPlan == null) return;
        }

        float hw = GetKeelHalfWidth();

        // 1. BACKBONE SIDE LINES
        if (showBackboneSideLines)
        {
            DrawBackboneSide(hw, starboardColor);
            DrawBackboneSide(-hw, portColor);
        }

        // 2. FRAMES LAYOUT
        if (showFrames || showMasterFrame)
        {
            List<float> stations = CalculateFrameStations();

            foreach (float z in stations)
            {
                bool isMaster = Mathf.Abs(z) < 0.001f;

                if (isMaster && !showMasterFrame) continue;
                if (!isMaster && !showFrames) continue;

                Color c = isMaster ? masterFrameColor : frameColor;
                DrawFrameAtZ(z, c, isMaster);
            }

#if UNITY_EDITOR
            if (showLabels)
            {
                GUIStyle labelStyle = new GUIStyle();
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.fontSize = 11;
                labelStyle.fontStyle = FontStyle.Bold;

                List<float> fwdStations = new List<float>();
                List<float> aftStations = new List<float>();

                foreach (float z in stations)
                {
                    if (z > 0.001f) fwdStations.Add(z);
                    else if (z < -0.001f) aftStations.Add(z);
                }
                fwdStations.Sort();
                aftStations.Sort();
                aftStations.Reverse();

                Dictionary<float, string> stationLabels = new Dictionary<float, string>();
                stationLabels[0f] = "Master";

                for (int i = 0; i < fwdStations.Count; i++)
                {
                    stationLabels[fwdStations[i]] = $"{i + 1}";
                }

                for (int i = 0; i < aftStations.Count; i++)
                {
                    stationLabels[aftStations[i]] = $"{i + 1} Aft";
                }

                foreach (float z in stations)
                {
                    bool isMaster = Mathf.Abs(z) < 0.001f;
                    if (isMaster && !showMasterFrame) continue;
                    if (!isMaster && !showFrames) continue;

                    if (stationLabels.TryGetValue(z, out string text))
                    {
                        float yTop = sheerPlan.GetPureSheerYAtZ(z);
                        Vector3 labelPos = new Vector3(0f, yTop + 0.3f, z);
                        labelStyle.normal.textColor = isMaster ? masterFrameColor : frameColor;
                        UnityEditor.Handles.Label(labelPos, text, labelStyle);
                    }
                }
            }
#endif
        }

        // 3. ANGLED FRAMES OVERLAY
        if (showAngledFrames)
        {
            DrawAngledFramesOverlay();
        }

        // 4. RISING LINE PROJECTION GUIDES (pLR & pL1 Curves)
        if (showRisingLineGuide || showKeelControlGuide)
        {
            DrawRisingLineGuide();
        }
    }

    private void DrawRisingLineGuide()
    {
        float aftZ = sheerPlan.cachedProjectedSternRisingEnd.z;
        float fwdZ = sheerPlan.cachedProjectedBowRisingEnd.z;

        float connAftZ = aftZ;
        float connFwdZ = fwdZ;

        const int steps = 100;
        Vector3 pL0, pL1_stbd, pLR_stbd, pL2, pL3, pU0, pU1, pU2, pU3;
        Vector3 pL1_port, pLR_port;

        // 1. Draw Connection Point Ribband Guide (pLR)
        if (showRisingLineGuide)
        {
            Gizmos.color = risingLineGuideColor;

            for (int sideSign = -1; sideSign <= 1; sideSign += 2)
            {
                float side = sideSign;
                Vector3 prevPt = Vector3.zero;
                bool first = true;

                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float z = Mathf.Lerp(connAftZ, connFwdZ, t);

                    float profY, projY;
                    if (GetAdjustedFrameControlPointsAtZ(z, side, out pL0, out pL1_stbd, out pLR_stbd, out pL2, out pL3, out profY, out projY))
                    {
                        if (!first)
                        {
                            Gizmos.DrawLine(prevPt, pLR_stbd);
                        }
                        prevPt = pLR_stbd;
                        first = false;
                    }
                }
            }

            // Draw sphere nodes at each frame station along the Connection Point ribband
            List<float> stations = CalculateFrameStations();
            foreach (float z in stations)
            {
                if (GetFrameControlPointsAtZ(z, 1f, out pL0, out pL1_stbd, out pLR_stbd, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
                {
                    Gizmos.color = risingLineGuideColor;
                    Gizmos.DrawSphere(pLR_stbd, controlPointRadius * 1.2f);
                }

                if (GetFrameControlPointsAtZ(z, -1f, out pL0, out pL1_port, out pLR_port, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
                {
                    Gizmos.color = risingLineGuideColor;
                    Gizmos.DrawSphere(pLR_port, controlPointRadius * 1.2f);
                }
            }
        }

        // 2. Draw Keel Control Guide (pL1)
        if (showKeelControlGuide)
        {
            Gizmos.color = keelControlGuideColor;

            for (int sideSign = -1; sideSign <= 1; sideSign += 2)
            {
                float side = sideSign;
                Vector3 prevPtL1 = Vector3.zero;
                bool first = true;

                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float z = Mathf.Lerp(aftZ, fwdZ, t);

                    float profY, projY;
                    if (GetAdjustedFrameControlPointsAtZ(z, side, out pL0, out pL1_stbd, out pLR_stbd, out pL2, out pL3, out profY, out projY))
                    {
                        if (!first)
                        {
                            Gizmos.DrawLine(prevPtL1, pL1_stbd);
                        }
                        prevPtL1 = pL1_stbd;
                        first = false;
                    }
                }
            }
        }
    }

    private void RebuildAngledFrameCache()
    {
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null) return;

        cachedAngledFrameLines.Clear();
        float step = Mathf.Max(1f, angledFrameAngleStep);
        int steps = Mathf.Max(10, angledFrameResolution);

        for (float deg = 0f; deg <= 90.01f; deg += step)
        {
            float clampedAngle = Mathf.Min(90f, deg);

            // Starboard (+1)
            List<Vector3> stbdPts = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float u = (float)i / steps;
                stbdPts.Add(GetAngledFramePointAtU(clampedAngle, 1f, u));
            }
            cachedAngledFrameLines.Add(stbdPts);

            // Port (-1)
            List<Vector3> portPts = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float u = (float)i / steps;
                portPts.Add(GetAngledFramePointAtU(clampedAngle, -1f, u));
            }
            cachedAngledFrameLines.Add(portPts);
        }
        isAngledFrameCacheDirty = false;
    }

    private void DrawAngledFramesOverlay()
    {
        if (isAngledFrameCacheDirty || cachedAngledFrameLines.Count == 0)
        {
            RebuildAngledFrameCache();
        }

        Gizmos.color = angledFrameColor;
        foreach (var line in cachedAngledFrameLines)
        {
            for (int i = 0; i < line.Count - 1; i++)
            {
                Gizmos.DrawLine(line[i], line[i + 1]);
            }
        }
    }


    private void DrawFrameAtZ(float z, Color color, bool isMaster = false)
    {
        // Draw Starboard (+1) and Port (-1) sides of the frame
        DrawFrameHalfAtZ(z, 1f, color, isMaster);
        DrawFrameHalfAtZ(z, -1f, color, isMaster);
    }

    private void DrawFrameHalfAtZ(float z, float side, Color color, bool isMaster = false)
    {
        Vector3 pL0_curve, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!GetFrameControlPointsAtZ(z, side, out pL0_curve, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return;

        float hw = GetKeelHalfWidth();
        float keelY = GetFrameBottomYAtZ(z);
        float hobY = pL3.y;
        float profileY = keelY;

        if (sheerPlan != null)
        {
            if (z > sheerPlan.cachedKeelBowPoint.z)
            {
                profileY = sheerPlan.GetStemProfileYAtZ(z);
            }
            else if (sheerPlan.showDeadwoodRake && z <= sheerPlan.cachedDeadwoodForePoint.z && z >= sheerPlan.cachedSternRisingEnd.z - 0.001f)
            {
                profileY = sheerPlan.GetDeadwoodYAtZ(z);
            }
        }

        // BOTTOM CUTOFF HEIGHT (STEM PROFILE AT BOW, FASHION TIMBER AT STERN)
        float yCutoff = keelY;
        if (!disableFrameCutoffs && sheerPlan != null)
        {
            if (z > sheerPlan.cachedKeelBowPoint.z)
            {
                if (profileY > keelY)
                {
                    yCutoff = profileY;
                }
            }
            else if (cutFramesAtFashionTimber)
            {
                float ftMaxZ = (fashionTimberModel != null) ? fashionTimberModel.GetMaxZ() : sheerPlan.cachedTransomTopPoint.z;
                if (z <= ftMaxZ + 0.001f)
                {
                    float ftY = sheerPlan.GetSternProfileYAtZ(z);
                    if (ftY > keelY)
                    {
                        yCutoff = ftY;
                    }
                }
            }
        }



        // DRAW LOWER MOULD SEGMENTS (IF NOT COMPLETELY CUT BELOW FASHION TIMBER)
        if (yCutoff < hobY - 0.001f)
        {
            float lowerMinT = 0f;
            if (yCutoff > profileY + 0.001f)
            {
                float minT = 0f;
                float maxT = 1f;
                for (int i = 0; i < 8; i++)
                {
                    float midT = (minT + maxT) * 0.5f;
                    Vector3 pt = midT <= 0.5f ? CalculateQuadraticBezierPoint(midT * 2f, pL0_curve, pL1, pLR) : CalculateQuadraticBezierPoint((midT - 0.5f) * 2f, pLR, pL2, pL3);
                    if (pt.y < yCutoff) minT = midT;
                    else maxT = midT;
                }
                lowerMinT = (minT + maxT) * 0.5f;
            }

            if (lowerMinT <= 0.5f)
            {
                DrawQuadraticBezierCurve(pL0_curve, pL1, pLR, color, 15, lowerMinT * 2f, 1f);
                DrawQuadraticBezierCurve(pLR, pL2, pL3, color, 15, 0f, 1f);
            }
            else
            {
                DrawQuadraticBezierCurve(pLR, pL2, pL3, color, 15, (lowerMinT - 0.5f) * 2f, 1f);
            }
        }

        // UPPER MOULD SEGMENT (HoB -> Top Line)
        float maxSheerYAtZ = sheerPlan.GetPureSheerYAtZ(z);
        float localTopY = disableFrameCutoffs ? maxSheerYAtZ : sheerPlan.GetTopTimberYAtZ(z);

        // UPPER MOULD CLIPPING LOGIC: Cut at top height and cut below Fashion Timber if yCutoff >= hobY
        float upperMinT = 0f;
        if (yCutoff >= hobY - 0.001f)
        {
            float minT = 0f;
            float maxT = 1f;
            for (int i = 0; i < 8; i++)
            {
                float midT = (minT + maxT) * 0.5f;
                float py = CalculateCubicBezierPoint(midT, pU0, pU1, pU2, pU3).y;
                if (py < yCutoff) minT = midT;
                else maxT = midT;
            }
            upperMinT = (minT + maxT) * 0.5f;
        }

        float upperTopCutoffT = 1f;
        if (maxSheerYAtZ > localTopY + 0.001f)
        {
            float minT = 0f;
            float maxT = 1f;
            for (int i = 0; i < 8; i++)
            {
                float midT = (minT + maxT) * 0.5f;
                float py = CalculateCubicBezierPoint(midT, pU0, pU1, pU2, pU3).y;
                if (py < localTopY) minT = midT;
                else maxT = midT;
            }
            upperTopCutoffT = (minT + maxT) * 0.5f;
        }

        if (upperMinT < upperTopCutoffT)
        {
            DrawCubicBezierCurve(pU0, pU1, pU2, pU3, color, 15, upperMinT, upperTopCutoffT);
        }

        // CONTROL POINT VISUALIZATION (Keel Point, Connection Point, HOB Point)
        if (showFrameControlPoints)
        {
            // 1. Keel Point (pL0_curve)
            if (yCutoff <= pL0_curve.y + 0.001f)
            {
                Gizmos.color = keelPointColor;
                Gizmos.DrawSphere(pL0_curve, controlPointRadius);
            }

            // 2. Connection Point (pLR) - Only draw if distinct from Keel Point
            if (yCutoff <= pLR.y + 0.001f && Vector3.Distance(pLR, pL0_curve) > 0.005f)
            {
                Gizmos.color = connectionPointColor;
                Gizmos.DrawSphere(pLR, controlPointRadius);
            }

            // 3. HOB Point (pL3)
            if (yCutoff <= pL3.y + 0.001f)
            {
                Gizmos.color = hobPointColor;
                Gizmos.DrawSphere(pL3, controlPointRadius);
            }
        }

        // CONTROL ARM VISUALIZATION (Bezier Control Polygons & Control Handles)
        if (showFrameControlArms)
        {
            // Lower Mould Control Arms: pL0_curve -> pL1 -> pLR -> pL2 -> pL3
            if (yCutoff < hobY - 0.001f)
            {
                Gizmos.color = controlArmColor;
                if (Vector3.Distance(pL0_curve, pL1) > 0.005f) Gizmos.DrawLine(pL0_curve, pL1);
                if (Vector3.Distance(pL1, pLR) > 0.005f) Gizmos.DrawLine(pL1, pLR);
                Gizmos.DrawLine(pLR, pL2);
                Gizmos.DrawLine(pL2, pL3);

                Gizmos.color = controlArmPointColor;
                if (yCutoff <= pL1.y + 0.001f && Vector3.Distance(pL1, pL0_curve) > 0.005f) Gizmos.DrawSphere(pL1, controlPointRadius * 0.8f);
                if (yCutoff <= pL2.y + 0.001f) Gizmos.DrawSphere(pL2, controlPointRadius * 0.8f);
            }

            // Upper Mould Control Arms: pU0 -> pU1 -> pU2 -> pU3
            if (upperMinT < upperTopCutoffT)
            {
                Gizmos.color = controlArmColor;
                Gizmos.DrawLine(pU0, pU1);
                Gizmos.DrawLine(pU1, pU2);
                Gizmos.DrawLine(pU2, pU3);

                Gizmos.color = controlArmPointColor;
                Gizmos.DrawSphere(pU1, controlPointRadius * 0.8f);
                Gizmos.DrawSphere(pU2, controlPointRadius * 0.8f);
                Gizmos.DrawSphere(pU3, controlPointRadius * 0.8f);
            }
        }

        // Gizmo Node Spheres for Master Frame
        if (isMaster)
        {
            Gizmos.color = color;
            if (yCutoff < hobY) Gizmos.DrawSphere(pL0_curve, 0.12f);
            Gizmos.DrawSphere(pLR, 0.12f);
            Gizmos.DrawSphere(pL3, 0.12f);
            Vector3 topSpherePt = CalculateCubicBezierPoint(upperTopCutoffT, pU0, pU1, pU2, pU3);
            Gizmos.DrawSphere(topSpherePt, 0.12f);
        }
    }

    private void DrawBackboneSide(float xOffset, Color color)
    {
        Gizmos.color = color;

        // --- Keel Line ---
        Vector3 keelBow = OffsetX(sheerPlan.cachedKeelBowPoint, xOffset);
        Vector3 keelStern = OffsetX(sheerPlan.cachedKeelSternPoint, xOffset);
        Vector3 keelMid = OffsetX(sheerPlan.cachedMidshipKeel, xOffset);

        if (sheerPlan.keelMidshipDrop != 0f)
        {
            Vector3 midP1Bow = new Vector3(xOffset, 0, keelBow.z / 2f);
            Vector3 midP1Stern = new Vector3(xOffset, 0, keelStern.z / 2f);
            DrawQuadraticBezierCurve(keelMid, midP1Bow, keelBow, color);
            DrawQuadraticBezierCurve(keelMid, midP1Stern, keelStern, color);
        }
        else
        {
            Gizmos.DrawLine(keelStern, keelBow);
        }

        // --- Stem Rake Line ---
        Vector3 stemP0 = keelBow;
        Vector3 stemP1 = OffsetX(sheerPlan.cachedStemP1, xOffset);
        Vector3 stemP2 = OffsetX(sheerPlan.cachedStemP2, xOffset);
        Vector3 stemP3 = OffsetX(sheerPlan.cachedStemTopPoint, xOffset);
        DrawCubicBezierCurve(stemP0, stemP1, stemP2, stemP3, color);

        // --- Upper Stem Line ---
        Vector3 upperStemTop = OffsetX(new Vector3(0, sheerPlan.GetTopTimberYAtZ(sheerPlan.cachedUpperStemTopPoint.z), sheerPlan.cachedUpperStemTopPoint.z), xOffset);
        Gizmos.DrawLine(stemP3, upperStemTop);

        // --- Stern Post Rake Line ---
        Vector3 sternPostTop = OffsetX(sheerPlan.cachedSternPostTopPoint, xOffset);
        
        if (sheerPlan.showDeadwoodRake)
        {
            Vector3 dwFore = OffsetX(sheerPlan.cachedDeadwoodForePoint, xOffset);
            Vector3 dwP1 = OffsetX(sheerPlan.cachedDeadwoodP1, xOffset);
            Vector3 dwP2 = OffsetX(sheerPlan.cachedDeadwoodP2, xOffset);
            Vector3 dwAft = OffsetX(sheerPlan.cachedDeadwoodAftPoint, xOffset);

            Gizmos.DrawLine(keelStern, dwFore);
            DrawCubicBezierCurve(dwFore, dwP1, dwP2, dwAft, color);
            Gizmos.DrawLine(dwAft, sternPostTop);
        }
        else
        {
            Gizmos.DrawLine(keelStern, sternPostTop);
        }

        // --- Counter Line (if enabled) ---
        if (sheerPlan.enableCounter)
        {
            Vector3 counterBase = OffsetX(sheerPlan.cachedCounterBasePoint, xOffset);
            Vector3 counterTop = OffsetX(sheerPlan.cachedCounterTopPoint, xOffset);
            Vector3 counterCp1 = OffsetX(sheerPlan.cachedCounterCp1, xOffset);
            Vector3 counterCp2 = OffsetX(sheerPlan.cachedCounterCp2, xOffset);
            DrawCubicBezierCurve(counterBase, counterCp1, counterCp2, counterTop, color);
        }

        // --- Joint Spheres ---
        Gizmos.DrawSphere(keelBow, 0.12f);
        Gizmos.DrawSphere(keelStern, 0.12f);
        Gizmos.DrawSphere(stemP3, 0.12f);
        Gizmos.DrawSphere(upperStemTop, 0.12f);
        Gizmos.DrawSphere(sternPostTop, 0.12f);
        if (sheerPlan.enableCounter) Gizmos.DrawSphere(OffsetX(sheerPlan.cachedCounterTopPoint, xOffset), 0.12f);
    }

    private void DrawQuadraticBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Color color, int segments = 20, float minT = 0f, float maxT = 1f)
    {
        Gizmos.color = color;
        Vector3 previousPoint = CalculateQuadraticBezierPoint(minT, p0, p1, p2);
        for (int i = 1; i <= segments; i++)
        {
            float t = Mathf.Lerp(minT, maxT, i / (float)segments);
            Vector3 currentPoint = CalculateQuadraticBezierPoint(t, p0, p1, p2);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }

    private void DrawCubicBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color, int segments = 20, float minT = 0f, float maxT = 1f)
    {
        Gizmos.color = color;
        Vector3 previousPoint = CalculateCubicBezierPoint(minT, p0, p1, p2, p3);
        for (int i = 1; i <= segments; i++)
        {
            float t = Mathf.Lerp(minT, maxT, i / (float)segments);
            Vector3 currentPoint = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }

    private Vector3 GetQuadraticBezierPointByArcLength(float targetFraction, Vector3 p0, Vector3 p1, Vector3 p2, int samples = 20)
    {
        targetFraction = Mathf.Clamp01(targetFraction);
        if (targetFraction <= 0f) return p0;
        if (targetFraction >= 1f) return p2;

        float[] cumulativeLengths = new float[samples + 1];
        Vector3 prev = p0;
        float totalLength = 0f;
        cumulativeLengths[0] = 0f;

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 curr = CalculateQuadraticBezierPoint(t, p0, p1, p2);
            totalLength += Vector3.Distance(prev, curr);
            cumulativeLengths[i] = totalLength;
            prev = curr;
        }

        if (totalLength < 0.0001f) return p0;

        float targetLength = targetFraction * totalLength;

        for (int i = 0; i < samples; i++)
        {
            if (targetLength >= cumulativeLengths[i] && targetLength <= cumulativeLengths[i + 1])
            {
                float segmentLen = cumulativeLengths[i + 1] - cumulativeLengths[i];
                float segFraction = segmentLen > 0.0001f ? (targetLength - cumulativeLengths[i]) / segmentLen : 0f;
                float tLow = i / (float)samples;
                float tHigh = (i + 1) / (float)samples;
                float t = Mathf.Lerp(tLow, tHigh, segFraction);
                return CalculateQuadraticBezierPoint(t, p0, p1, p2);
            }
        }

        return p2;
    }

    private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        return (u * u * p0) + (2 * u * t * p1) + (t * t * p2);
    }

    private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        return (u * u * u * p0) + (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t * p3);
    }
}
