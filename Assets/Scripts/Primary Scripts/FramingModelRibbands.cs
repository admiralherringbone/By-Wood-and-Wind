using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class FramingModelRibbands : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FashionTimberModel fashionTimberModel;

    [Header("Ribband Visualization Settings")]
    [Tooltip("Show the 3D floor sweeps and rising line guide ribbands (yellow/green).")]
    public bool showFloorSweepsGuide = true;

    [Tooltip("Cut ribbands as they pass behind (aft on the Z-axis) the Fashion Timbers.")]
    public bool cutRibbandsAtFashionTimber = true;

    [Tooltip("Show 3D lower deck floor ribband lines tracing along the hull frames for each lower deck tier.")]
    public bool showLowerDeckLines = true;
    public Color lowerDeckColor = new Color(0.6f, 0.3f, 0.9f);

    [Tooltip("Show 3D primary deck ribband line tracing along the hull frames at base deck height.")]
    public bool showPrimaryDeckLine = true;
    public Color primaryDeckColor = new Color(1f, 0.5f, 0f);

    [Tooltip("Show 3D upper deck floor ribband lines tracing along the hull frames for each upper deck.")]
    public bool showUpperDeckLines = true;
    public Color upperDeckColor = new Color(0.2f, 0.6f, 1f);

    [Tooltip("Show 3D top timber ribband lines for each sheer level (base bulwark & upper deck bulwarks).")]
    public bool showTopTimberLine = true;
    public Color topTimberColor = Color.cyan;

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

        // 1. FLOOR SWEEPS GUIDES (Yellow & Green 3D Ribbands)
        if (showFloorSweepsGuide)
        {
            DrawFloorSweepsGuide();
        }

        // 2. LOWER DECK RIBBANDS (Lower Deck Floor 3D Lines)
        if (showLowerDeckLines)
        {
            DrawLowerDeckRibbands();
        }

        // 3. PRIMARY DECK RIBBAND (Base Deck Height 3D Line)
        if (showPrimaryDeckLine)
        {
            DrawPrimaryDeckRibband();
        }

        // 4. UPPER DECK RIBBANDS (Upper Deck Floor 3D Lines)
        if (showUpperDeckLines)
        {
            DrawUpperDeckRibbands();
        }

        // 5. TOP TIMBER RIBBANDS (Bulwark Top 3D Lines)
        if (showTopTimberLine)
        {
            DrawTopTimberRibbands();
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

    private void DrawFloorSweepsGuide()
    {
        if (bodyPlan == null || sheerPlan == null) return;

        float hw = GetKeelHalfWidth();
        Vector3 masterL1 = bodyPlan.cachedL1;
        Vector3 masterLR = bodyPlan.cachedLR;

        Vector3 bowEnd = new Vector3(hw, sheerPlan.cachedBowRisingEnd.y, sheerPlan.cachedBowRisingEnd.z);
        Vector3 sternEnd = new Vector3(hw, sheerPlan.cachedSternRisingEnd.y, sheerPlan.cachedSternRisingEnd.z);

        Vector3 bowP1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedBowP1Ctrl.z);
        Vector3 sternP1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedSternP1Ctrl.z);

        Vector3 bowP1_LR = new Vector3(masterLR.x, masterLR.y, sheerPlan.cachedBowP1Ctrl.z);
        Vector3 sternP1_LR = new Vector3(masterLR.x, masterLR.y, sheerPlan.cachedSternP1Ctrl.z);

        // Starboard Keel Arm Ribband (Yellow)
        DrawQuadraticBezierCurve(masterL1, sternP1, sternEnd, Color.yellow);
        DrawQuadraticBezierCurve(masterL1, bowP1, bowEnd, Color.yellow);

        // Starboard Rising Line Ribband (Green)
        DrawQuadraticBezierCurve(masterLR, sternP1_LR, sternEnd, Color.green);
        DrawQuadraticBezierCurve(masterLR, bowP1_LR, bowEnd, Color.green);

        // Port Ribbands
        Vector3 mMasterL1 = new Vector3(-masterL1.x, masterL1.y, masterL1.z);
        Vector3 mBowP1 = new Vector3(-bowP1.x, bowP1.y, bowP1.z);
        Vector3 mSternP1 = new Vector3(-sternP1.x, sternP1.y, sternP1.z);
        Vector3 mBowEnd = new Vector3(-hw, bowEnd.y, bowEnd.z);
        Vector3 mSternEnd = new Vector3(-hw, sternEnd.y, sternEnd.z);

        Vector3 mMasterLR = new Vector3(-masterLR.x, masterLR.y, masterLR.z);
        Vector3 mBowP1_LR = new Vector3(-bowP1_LR.x, bowP1_LR.y, bowP1_LR.z);
        Vector3 mSternP1_LR = new Vector3(-sternP1_LR.x, sternP1_LR.y, sternP1_LR.z);

        DrawQuadraticBezierCurve(mMasterL1, mSternP1, mSternEnd, Color.yellow);
        DrawQuadraticBezierCurve(mMasterL1, mBowP1, mBowEnd, Color.yellow);

        DrawQuadraticBezierCurve(mMasterLR, mSternP1_LR, mSternEnd, Color.green);
        DrawQuadraticBezierCurve(mMasterLR, mBowP1_LR, mBowEnd, Color.green);
    }

    private float FindSternFashionTimberIntersectionZ(System.Func<float, float> getTargetYAtZ)
    {
        if (sheerPlan == null) return 0f;

        float minZ = sheerPlan.GetAftMostZ();
        float maxZ = sheerPlan.cachedKeelBowPoint.z;

        float lowZ = minZ;
        float highZ = maxZ;

        for (int i = 0; i < 25; i++)
        {
            float midZ = (lowZ + highZ) * 0.5f;
            float yRibband = getTargetYAtZ(midZ);
            float yFT = sheerPlan.GetSternProfileYAtZ(midZ);

            if (yRibband < yFT)
            {
                lowZ = midZ;
            }
            else
            {
                highZ = midZ;
            }
        }
        return (lowZ + highZ) * 0.5f;
    }

    private void DrawRibbandCurve(System.Func<float, float> getTargetYAtZ, Color color, float minAllowedZ = float.MinValue, float maxAllowedZ = float.MaxValue)
    {
        float aftZ = sheerPlan.GetAftMostZ();
        float fwdZ = sheerPlan.GetFwdMostZ();

        // 1. Calculate exact stern Fashion Timber intersection Z for this ribband curve
        float sternZCutoff = FindSternFashionTimberIntersectionZ(getTargetYAtZ);

        // 2. Build Z sample list starting from sternZCutoff (if cut enabled) up to fwdZ
        List<float> rawZList = new List<float>();

        if (cutRibbandsAtFashionTimber)
        {
            rawZList.Add(sternZCutoff);
        }
        else
        {
            rawZList.Add(aftZ);
        }

        if (framingModel != null)
        {
            foreach (float s in framingModel.CalculateFrameStations())
            {
                if (s >= (cutRibbandsAtFashionTimber ? sternZCutoff : aftZ))
                {
                    rawZList.Add(s);
                }
            }
        }

        int uniformCount = 80;
        for (int i = 0; i < uniformCount; i++)
        {
            float z = Mathf.Lerp(aftZ, fwdZ, i / (float)(uniformCount - 1));
            if (z >= (cutRibbandsAtFashionTimber ? sternZCutoff : aftZ))
            {
                rawZList.Add(z);
            }
        }

        rawZList.Sort();
        List<float> zPoints = new List<float>();
        foreach (float z in rawZList)
        {
            if (z < minAllowedZ - 0.001f || z > maxAllowedZ + 0.001f) continue;
            if (zPoints.Count == 0 || Mathf.Abs(z - zPoints[zPoints.Count - 1]) > 0.001f)
            {
                zPoints.Add(z);
            }
        }

        Gizmos.color = color;

        Vector3? prevStbd = null;
        Vector3? prevPort = null;

        foreach (float z in zPoints)
        {
            float targetY = getTargetYAtZ(z);

            Vector3? currStbd = GetFrameSurfacePointAtY(z, 1f, targetY, true);
            Vector3? currPort = GetFrameSurfacePointAtY(z, -1f, targetY, true);

            if (currStbd.HasValue && prevStbd.HasValue)
            {
                Gizmos.DrawLine(prevStbd.Value, currStbd.Value);
            }
            if (currPort.HasValue && prevPort.HasValue)
            {
                Gizmos.DrawLine(prevPort.Value, currPort.Value);
            }

            prevStbd = currStbd;
            prevPort = currPort;
        }
    }

    private void DrawLowerDeckRibbands()
    {
        if (bodyPlan == null || sheerPlan == null || breadthPlan == null) return;
        if (sheerPlan.lowerDecks == null || sheerPlan.lowerDecks.Count == 0) return;

        float startZ = sheerPlan.GetAftMostZ();
        float endZ = sheerPlan.GetFwdMostZ();
        float totalLength = endZ - startZ;
        if (totalLength <= 0.001f) return;

        foreach (var deck in sheerPlan.lowerDecks)
        {
            float aftZ = startZ + (deck.aftExtent * totalLength);
            float foreZ = startZ + (deck.foreExtent * totalLength);
            float yDrop = sheerPlan.GetLowerDeckYDrop(deck);

            DrawRibbandCurve(z => sheerPlan.GetBaseDeckYAtZ(z) - yDrop, lowerDeckColor, aftZ, foreZ);
        }
    }

    private void DrawPrimaryDeckRibband()
    {
        if (bodyPlan == null || sheerPlan == null || breadthPlan == null) return;
        DrawRibbandCurve(z => sheerPlan.GetBaseDeckYAtZ(z) + sheerPlan.GetGlobalSheerOffsetAtZ(z), primaryDeckColor);
    }

    private void DrawUpperDeckRibbands()
    {
        if (bodyPlan == null || sheerPlan == null || breadthPlan == null) return;
        if (sheerPlan.upperDecks == null || sheerPlan.upperDecks.Count == 0) return;

        foreach (var deck in sheerPlan.upperDecks)
        {
            float hOffset = sheerPlan.GetAbsoluteDeckHeight(deck);
            DrawRibbandCurve(z => sheerPlan.GetBaseDeckYAtZ(z) + hOffset + sheerPlan.GetGlobalSheerOffsetAtZ(z), upperDeckColor);
        }
    }

    private void DrawTopTimberRibbands()
    {
        if (bodyPlan == null || sheerPlan == null || breadthPlan == null) return;

        List<float> sheerHeights = new List<float>();

        // 1. Base Top Timber Sheer Height
        sheerHeights.Add(sheerPlan.bulwarkHeight);

        // 2. Upper Decks Sheer Heights
        if (sheerPlan.upperDecks != null)
        {
            foreach (var deck in sheerPlan.upperDecks)
            {
                float absH = sheerPlan.GetAbsoluteDeckHeight(deck) + deck.bulwarkHeight;
                if (!sheerHeights.Contains(absH)) sheerHeights.Add(absH);
            }
        }

        foreach (float hOffset in sheerHeights)
        {
            DrawRibbandCurve(z => sheerPlan.GetBaseDeckYAtZ(z) + hOffset + sheerPlan.GetGlobalSheerOffsetAtZ(z), topTimberColor);
        }
    }

    private Vector3? GetFrameSurfacePointAtY(float z, float side, float targetY, bool ignoreFashionCutoff = false)
    {
        if (!ignoreFashionCutoff && cutRibbandsAtFashionTimber && sheerPlan != null)
        {
            float sternZCutoff = sheerPlan.GetSternProfileZAtY(targetY);
            if (z < sternZCutoff - 0.001f) return null; // Ribband passes behind (aft of) Fashion Timber at targetY
        }

        float localTopY = sheerPlan.GetTopTimberYAtZ(z);
        if (targetY > localTopY + 0.005f) return null; // Frame structure does not extend to this height at Z

        float hw = GetKeelHalfWidth();
        float keelY = GetFrameBottomYAtZ(z);
        float originalKeelY = keelY;

        if (targetY < keelY - 0.005f) return null; // Below keel

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

        // UPPER MOULD SEARCH (targetY >= hobY)
        if (targetY >= hobY - 0.001f)
        {
            Vector3 pU0 = new Vector3(side * hobBreadth, hobY, z);
            Vector3 pU3 = new Vector3(side * xTop, maxSheerYAtZ, z);

            float masterUpperDx = bodyPlan.cachedU3.x - bodyPlan.cachedU0.x;
            float masterUpperDy = bodyPlan.cachedU3.y - bodyPlan.cachedU0.y;

            float localUpperDx = xTop - hobBreadth;
            float localUpperDy = maxSheerYAtZ - hobY;

            float sXUpper = Mathf.Abs(masterUpperDx) > 0.001f ? localUpperDx / masterUpperDx : 0f;
            float sYUpper = Mathf.Abs(masterUpperDy) > 0.001f ? localUpperDy / masterUpperDy : 0f;

            Vector3 masterU1Offset = bodyPlan.cachedU1 - bodyPlan.cachedU0;
            Vector3 masterU2Offset = bodyPlan.cachedU2 - bodyPlan.cachedU3;

            Vector3 pU1 = pU0 + new Vector3(side * masterU1Offset.x * sXUpper, masterU1Offset.y * sYUpper, 0);
            Vector3 pU2 = pU3 + new Vector3(side * masterU2Offset.x * sXUpper, masterU2Offset.y * sYUpper, 0);

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
            // LOWER MOULD SEARCH (targetY < hobY)
            float xBot = hw;
            Vector3 pL0 = new Vector3(side * xBot, keelY, z);
            Vector3 pL3 = new Vector3(side * hobBreadth, hobY, z);

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

            Vector3 pL1 = new Vector3(side * targetL1_X, targetL1_Y, z);
            float currentBoxHeight = hobY - originalKeelY;
            float hobDrop = currentBoxHeight * bodyPlan.hobLowerArmLength;
            Vector3 pL2 = new Vector3(side * hobBreadth, Mathf.Max(hobY - hobDrop, targetLR_Y), z);

            float dx = Mathf.Abs(pL2.x - pL1.x);
            float lrPosRatio = bodyPlan.risingLinePosition;
            if (dx > 0.0001f)
            {
                lrPosRatio = Mathf.Clamp01((targetLR_X - targetL1_X) / dx);
            }
            Vector3 pLR = Vector3.Lerp(pL1, pL2, lrPosRatio);

            if (keelY > originalKeelY + 0.001f && hobY > originalKeelY + 0.001f)
            {
                float scaleY = (hobY - keelY) / (hobY - originalKeelY);
                pL1.y = keelY + (pL1.y - originalKeelY) * scaleY;
                pLR.y = keelY + (pLR.y - originalKeelY) * scaleY;
                pL2.y = keelY + (pL2.y - originalKeelY) * scaleY;
            }

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

    private void DrawQuadraticBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Color color, int segments = 20)
    {
        Gizmos.color = color;
        Vector3 previousPoint = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 currentPoint = CalculateQuadraticBezierPoint(t, p0, p1, p2);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
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
