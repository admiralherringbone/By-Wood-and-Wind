using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class FramingModelWaterlines : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FashionTimberModel fashionTimberModel;

    [Header("Waterline Settings")]
    [Tooltip("Show 3D horizontal waterline slices tracing the hull surface defined by the frames.")]
    public bool showWaterlines = true;

    [Tooltip("Vertical spacing between adjacent horizontal waterline slices.")]
    public float waterlineSpacing = 1.0f;

    [Tooltip("Color of the waterline gizmo curves.")]
    public Color waterlineColor = new Color(0.1f, 0.5f, 1f, 0.8f);

    [Tooltip("Cut waterlines as they pass behind (aft on the Z-axis) the Fashion Timbers.")]
    public bool cutWaterlinesAtFashionTimber = true;

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        FindReferences();
    }

    public void FindReferences()
    {
        if (sheerPlan == null) sheerPlan = GetComponentInParent<SheerPlan>();
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();

        if (bodyPlan == null) bodyPlan = GetComponentInParent<BodyPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();

        if (breadthPlan == null) breadthPlan = GetComponentInParent<BreadthPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();

        if (framingModel == null) framingModel = GetComponentInParent<FramingModel>();
        if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();

        if (fashionTimberModel == null) fashionTimberModel = GetComponentInParent<FashionTimberModel>();
        if (fashionTimberModel == null) fashionTimberModel = FindFirstObjectByType<FashionTimberModel>();
    }

    public float GetKeelHalfWidth()
    {
        if (bodyPlan != null) return bodyPlan.keelWidth / 2f;
        if (framingModel != null) return framingModel.GetKeelHalfWidth();
        return 0.25f;
    }

    public float GetFrameBottomYAtZ(float z)
    {
        if (framingModel != null) return framingModel.GetFrameBottomYAtZ(z);
        if (sheerPlan != null) return sheerPlan.GetKeelYAtZ(z);
        return 0f;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null) return;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        breadthPlan.UpdateCachedCurves();

        if (showWaterlines)
        {
            DrawWaterlines();
        }
    }

    private List<float> GetZPoints()
    {
        float startZ = sheerPlan.GetAftMostZ();
        float endZ = sheerPlan.GetFwdMostZ();

        List<float> rawZList = new List<float>();

        if (framingModel != null)
        {
            rawZList.AddRange(framingModel.CalculateFrameStations());
        }

        int uniformCount = 80;
        for (int i = 0; i < uniformCount; i++)
        {
            float z = Mathf.Lerp(startZ, endZ, i / (float)(uniformCount - 1));
            rawZList.Add(z);
        }

        rawZList.Sort();
        List<float> zPoints = new List<float>();
        foreach (float z in rawZList)
        {
            if (zPoints.Count == 0 || Mathf.Abs(z - zPoints[zPoints.Count - 1]) > 0.001f)
            {
                zPoints.Add(z);
            }
        }
        return zPoints;
    }

    private void DrawWaterlines()
    {
        if (bodyPlan == null || sheerPlan == null || breadthPlan == null) return;
        if (waterlineSpacing <= 0.1f) waterlineSpacing = 0.1f;

        float minY = sheerPlan.cachedMidshipKeel.y;
        float maxY = sheerPlan.cachedTopTimberBowIntersect.y;
        if (sheerPlan.upperDecks != null && sheerPlan.upperDecks.Count > 0)
        {
            maxY += sheerPlan.GetMaxDeckHeight();
        }

        List<float> zPoints = GetZPoints();
        Gizmos.color = waterlineColor;

        float hw = GetKeelHalfWidth();

        for (float y = Mathf.Ceil(minY); y <= maxY; y += waterlineSpacing)
        {
            // Exact boundaries of the hull at height Y according to the Sheer Plan
            float exactSternZ = sheerPlan.GetSternProfileZAtY(y);
            float exactStemZ = sheerPlan.GetStemProfileZAtY(y);

            Vector3? prevStbd = null;
            Vector3? prevPort = null;

            // Start exact stern Fashion Timber / Stern Profile connection if cut enabled
            if (cutWaterlinesAtFashionTimber)
            {
                Vector3? sternPt = GetFrameSurfacePointAtY(exactSternZ, 1f, y, true);
                float sternX = sternPt.HasValue ? Mathf.Abs(sternPt.Value.x) : hw;
                prevStbd = new Vector3(sternX, y, exactSternZ);
                prevPort = new Vector3(-sternX, y, exactSternZ);
            }

            foreach (float z in zPoints)
            {
                // Strict boundary check: station Z must be within [exactSternZ, exactStemZ]
                if (z < exactSternZ - 0.001f || z > exactStemZ + 0.001f) continue;

                Vector3? currStbd = GetFrameSurfacePointAtY(z, 1f, y);
                Vector3? currPort = GetFrameSurfacePointAtY(z, -1f, y);

                if (currStbd.HasValue && prevStbd.HasValue)
                {
                    Gizmos.DrawLine(prevStbd.Value, currStbd.Value);
                    Gizmos.DrawLine(prevPort.Value, currPort.Value);
                }

                // Unconditionally update state so nulls properly break line rendering across missing frames
                prevStbd = currStbd;
                prevPort = currPort;
            }

            // Draw exact bow stem connection at exactStemZ
            Vector3 stemStbd = new Vector3(hw, y, exactStemZ);
            Vector3 stemPort = new Vector3(-hw, y, exactStemZ);
            if (prevStbd.HasValue && prevStbd.Value.z < exactStemZ - 0.001f)
            {
                Gizmos.DrawLine(prevStbd.Value, stemStbd);
                Gizmos.DrawLine(prevPort.Value, stemPort);
            }
        }
    }

    public bool GetFrameControlPoints(float z, float side,
        out Vector3 pL0, out Vector3 pL1, out Vector3 pLR, out Vector3 pL2, out Vector3 pL3,
        out Vector3 pU0, out Vector3 pU1, out Vector3 pU2, out Vector3 pU3)
    {
        pL0 = pL1 = pLR = pL2 = pL3 = pU0 = pU1 = pU2 = pU3 = Vector3.zero;
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null) return false;

        float hw = GetKeelHalfWidth();
        float keelY = GetFrameBottomYAtZ(z);
        float originalKeelY = keelY;

        float hobY = sheerPlan.GetTheoreticalHobYAtZ(z);
        float hobBreadth = breadthPlan.GetTheoreticalBreadthXAtZ(z);
        float xTop = breadthPlan.GetTopTimberXAtZ(z);

        float maxSheerYAtZ = sheerPlan.GetBaseDeckYAtZ(z);
        if (sheerPlan.upperDecks != null && sheerPlan.upperDecks.Count > 0)
        {
            maxSheerYAtZ += sheerPlan.GetMaxDeckHeight() + sheerPlan.GetGlobalSheerOffsetAtZ(z);
        }
        else
        {
            maxSheerYAtZ = sheerPlan.GetTopTimberYAtZ(z);
        }

        // UPPER MOULD SEARCH
        pU0 = new Vector3(side * hobBreadth, hobY, z);
        pU3 = new Vector3(side * xTop, maxSheerYAtZ, z);

        float masterUpperDx = bodyPlan.cachedU3.x - bodyPlan.cachedU0.x;
        float masterUpperDy = bodyPlan.cachedU3.y - bodyPlan.cachedU0.y;

        float localUpperDx = xTop - hobBreadth;
        float localUpperDy = maxSheerYAtZ - hobY;

        float sXUpper = Mathf.Abs(masterUpperDx) > 0.001f ? localUpperDx / masterUpperDx : 0f;
        float sYUpper = Mathf.Abs(masterUpperDy) > 0.001f ? localUpperDy / masterUpperDy : 0f;

        Vector3 masterU1Offset = bodyPlan.cachedU1 - bodyPlan.cachedU0;
        Vector3 masterU2Offset = bodyPlan.cachedU2 - bodyPlan.cachedU3;

        pU1 = pU0 + new Vector3(side * masterU1Offset.x * sXUpper, masterU1Offset.y * sYUpper, 0);
        pU2 = pU3 + new Vector3(side * masterU2Offset.x * sXUpper, masterU2Offset.y * sYUpper, 0);

        // LOWER MOULD SEARCH
        float xBot = hw;
        pL0 = new Vector3(side * xBot, keelY, z);
        pL3 = new Vector3(side * hobBreadth, hobY, z);

        Vector3 masterL1 = bodyPlan.cachedL1;
        Vector3 masterLR = bodyPlan.cachedLR;

        float targetL1_X = hw;
        float targetL1_Y = originalKeelY;
        float targetLR_X = hw;
        float targetLR_Y = originalKeelY;

        if (z >= 0)
        {
            float bowZ = sheerPlan.cachedBowRisingEnd.z;
            if (bowZ > 0.001f)
            {
                float t = sheerPlan.GetRisingLineTAtZ(z);
                Vector3 bowEnd = new Vector3(hw, sheerPlan.cachedBowRisingEnd.y, sheerPlan.cachedBowRisingEnd.z);

                Vector3 p0_L1 = masterL1;
                Vector3 p1_L1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedBowP1Ctrl.z);
                Vector3 p2_L1 = bowEnd;
                targetL1_X = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).x;
                targetL1_Y = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).y;

                Vector3 p0_LR = masterLR;
                Vector3 p1_LR = new Vector3(masterLR.x, masterLR.y, sheerPlan.cachedBowP1Ctrl.z);
                Vector3 p2_LR = bowEnd;
                targetLR_X = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).x;
                targetLR_Y = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).y;
            }
        }
        else
        {
            float sternZ = sheerPlan.cachedSternRisingEnd.z;
            if (sternZ < -0.001f)
            {
                Vector3 sternEnd = new Vector3(hw, sheerPlan.cachedSternRisingEnd.y, sheerPlan.cachedSternRisingEnd.z);

                Vector3 p0_L1 = masterL1;
                Vector3 p1_L1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedSternP1Ctrl.z);
                Vector3 p2_L1 = sternEnd;

                Vector3 p0_LR = masterLR;
                Vector3 p1_LR = new Vector3(masterLR.x, masterLR.y, sheerPlan.cachedSternP1Ctrl.z);
                Vector3 p2_LR = sternEnd;

                if (z < sternZ)
                {
                    float dzL1 = p2_L1.z - p1_L1.z;
                    if (Mathf.Abs(dzL1) > 0.0001f)
                    {
                        float ratio = (z - p2_L1.z) / dzL1;
                        targetL1_X = p2_L1.x;
                        targetL1_Y = p2_L1.y + (p2_L1.y - p1_L1.y) * ratio;
                    }
                    else
                    {
                        targetL1_X = p2_L1.x;
                        targetL1_Y = p2_L1.y;
                    }

                    float dzLR = p2_LR.z - p1_LR.z;
                    if (Mathf.Abs(dzLR) > 0.0001f)
                    {
                        float ratio = (z - p2_LR.z) / dzLR;
                        targetLR_X = p2_LR.x;
                        targetLR_Y = p2_LR.y + (p2_LR.y - p1_LR.y) * ratio;
                    }
                    else
                    {
                        targetLR_X = p2_LR.x;
                        targetLR_Y = p2_LR.y;
                    }
                }
                else
                {
                    float t = sheerPlan.GetRisingLineTAtZ(z);
                    targetL1_X = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).x;
                    targetL1_Y = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).y;

                    targetLR_X = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).x;
                    targetLR_Y = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).y;
                }
            }
        }

        targetL1_Y = Mathf.Max(targetL1_Y, originalKeelY);
        targetLR_Y = Mathf.Max(targetLR_Y, originalKeelY);

        pL1 = new Vector3(side * targetL1_X, targetL1_Y, z);
        float currentBoxHeight = hobY - originalKeelY;
        float hobDrop = currentBoxHeight * bodyPlan.hobLowerArmLength;
        pL2 = new Vector3(side * hobBreadth, Mathf.Max(hobY - hobDrop, targetLR_Y), z);

        float dx = Mathf.Abs(pL2.x - pL1.x);
        float lrPosRatio = bodyPlan.risingLinePosition;
        if (dx > 0.0001f)
        {
            lrPosRatio = Mathf.Clamp01((targetLR_X - targetL1_X) / dx);
        }
        pLR = Vector3.Lerp(pL1, pL2, lrPosRatio);

        float sternRisingZ = (sheerPlan != null) ? sheerPlan.cachedSternRisingEnd.z : 0f;
        float profileY = keelY;

        if (sheerPlan != null)
        {
            if (z > sheerPlan.cachedKeelBowPoint.z)
            {
                profileY = sheerPlan.GetStemProfileYAtZ(z);
            }
            else if (z <= sternRisingZ + 0.001f && sternRisingZ < -0.001f)
            {
                profileY = targetLR_Y;
            }
        }

        Vector3 pPseudo = new Vector3(side * xBot, profileY, z);

        bool isBehindSternRisingEnd = (z <= sternRisingZ + 0.001f && sternRisingZ < -0.001f);
        if (profileY >= targetLR_Y - 0.001f || isBehindSternRisingEnd)
        {
            pL1 = pPseudo;
            pLR = pPseudo;
            pL2.y = Mathf.Max(pL2.y, profileY);
        }
        else
        {
            pL1.y = Mathf.Clamp(pL1.y, profileY, pLR.y);
            pL2.y = Mathf.Max(pL2.y, pLR.y);
        }

        pL0 = pPseudo;
        return true;
    }

    public Vector3? GetFrameSurfacePointAtY(float z, float side, float targetY, bool ignoreFashionCutoff = false)
    {
        if (sheerPlan == null || bodyPlan == null) return null;

        // Stem Backbone Cutoff
        if (z > 0)
        {
            float stemZAtY = sheerPlan.GetStemProfileZAtY(targetY);
            if (z > stemZAtY + 0.001f) return null;
        }

        // Fashion Timber Cutoff
        if (!ignoreFashionCutoff && cutWaterlinesAtFashionTimber)
        {
            float sternZCutoff = sheerPlan.GetSternProfileZAtY(targetY);
            if (z < sternZCutoff - 0.001f) return null;
        }

        float localTopY = sheerPlan.GetTopTimberYAtZ(z);
        if (targetY > localTopY + 0.005f) return null;

        float keelY = GetFrameBottomYAtZ(z);
        if (targetY < keelY - 0.005f) return null;

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!GetFrameControlPoints(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return null;

        float hobY = sheerPlan.GetTheoreticalHobYAtZ(z);

        if (targetY >= hobY - 0.001f)
        {
            float minT = 0f;
            float maxT = 1f;
            for (int i = 0; i < 15; i++)
            {
                float midT = (minT + maxT) * 0.5f;
                float py = CalculateCubicBezierPoint(midT, pU0, pU1, pU2, pU3).y;
                if (py < targetY) minT = midT;
                else maxT = midT;
            }
            float t = (minT + maxT) * 0.5f;
            return CalculateCubicBezierPoint(t, pU0, pU1, pU2, pU3);
        }
        else
        {
            float minT = 0f;
            float maxT = 1f;
            for (int i = 0; i < 15; i++)
            {
                float midT = (minT + maxT) * 0.5f;
                Vector3 pt;
                if (midT <= 0.5f) pt = CalculateQuadraticBezierPoint(midT * 2f, pL0, pL1, pLR);
                else pt = CalculateQuadraticBezierPoint((midT - 0.5f) * 2f, pLR, pL2, pL3);

                if (pt.y < targetY) minT = midT;
                else maxT = midT;
            }
            float finalT = (minT + maxT) * 0.5f;
            if (finalT <= 0.5f) return CalculateQuadraticBezierPoint(finalT * 2f, pL0, pL1, pLR);
            else return CalculateQuadraticBezierPoint((finalT - 0.5f) * 2f, pLR, pL2, pL3);
        }
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
