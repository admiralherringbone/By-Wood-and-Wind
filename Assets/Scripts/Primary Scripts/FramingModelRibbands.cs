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

    [Header("Planking Strakes (Proportional Arc-Length Girth)")]
    [Tooltip("Show 3D proportional arc-length planking strakes (historical Lining Off).")]
    public bool showPlankingStrakes = true;
    [Range(1, 25)]
    public int plankingStrakeCount = 8;
    public Color plankingColor = new Color(1f, 0.7f, 0.2f, 0.9f);

    [Header("Historical Body Plan Diagonals")]
    [Tooltip("Show 3D historical Body Plan Diagonals (plane cuts across hull).")]
    public bool showDiagonalsGuide = false;
    public Color diagonalsColor = Color.magenta;

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

        // 6. PLANKING STRAKES (Arc-Length Girth / Lining Off)
        if (showPlankingStrakes)
        {
            DrawPlankingStrakes();
        }

        // 7. HISTORICAL DIAGONALS (Body Plan Plane Cuts)
        if (showDiagonalsGuide)
        {
            DrawDiagonalsGuide();
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

        float targetL1_X = breadthPlan.GetFloorLineXAtZ(z);
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
                        targetL1_Y = p2_L1.y + (p2_L1.y - p1_L1.y) * ratio;
                    }
                    else
                    {
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
                    targetL1_Y = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).y;

                    targetLR_X = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).x;
                    targetLR_Y = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).y;
                }
            }
        }

        targetL1_Y = Mathf.Max(targetL1_Y, originalKeelY);
        targetLR_Y = Mathf.Max(targetLR_Y, originalKeelY);

        float availableLowerHeight = Mathf.Max(0.001f, hobY - targetL1_Y);
        float hobDrop = availableLowerHeight * bodyPlan.hobLowerArmLength;
        pL2 = new Vector3(side * hobBreadth, Mathf.Max(hobY - hobDrop, targetL1_Y), z);

        pLR = Vector3.Lerp(pL1, pL2, bodyPlan.GetConnectionPointPositionAtZ(z));

        float profileY = keelY;
        float sternRisingZ = (sheerPlan != null) ? sheerPlan.cachedSternRisingEnd.z : 0f;

        if (sheerPlan != null)
        {
            if (z > sheerPlan.cachedKeelBowPoint.z)
            {
                profileY = sheerPlan.GetStemProfileYAtZ(z);
            }
            else if (z < sternRisingZ - 0.001f && sheerPlan.showDeadwoodRake && z <= sheerPlan.cachedDeadwoodForePoint.z && z >= sheerPlan.cachedSternPostTopPoint.z)
            {
                profileY = sheerPlan.GetDeadwoodYAtZ(z);
            }
        }

        Vector3 pPseudo = new Vector3(side * xBot, profileY, z);

        if (profileY >= pLR.y - 0.001f)
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

    public Vector3? GetFrameSurfacePointByGirthFraction(float z, float side, float girthFraction, bool ignoreFashionCutoff = false)
    {
        if (sheerPlan == null || bodyPlan == null) return null;

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!GetFrameControlPoints(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return null;

        float lenL1 = ApproximateQuadraticLength(pL0, pL1, pLR, 6);
        float lenL2 = ApproximateQuadraticLength(pLR, pL2, pL3, 6);
        float lenU  = ApproximateCubicLength(pU0, pU1, pU2, pU3, 6);

        float totalLength = lenL1 + lenL2 + lenU;
        if (totalLength <= 0.0001f) return null;

        float targetDist = Mathf.Clamp01(girthFraction) * totalLength;
        Vector3 pt;

        if (targetDist <= lenL1)
        {
            float u = lenL1 > 0.0001f ? targetDist / lenL1 : 0f;
            pt = CalculateQuadraticBezierPoint(u, pL0, pL1, pLR);
        }
        else if (targetDist <= lenL1 + lenL2)
        {
            float u = lenL2 > 0.0001f ? (targetDist - lenL1) / lenL2 : 0f;
            pt = CalculateQuadraticBezierPoint(u, pLR, pL2, pL3);
        }
        else
        {
            float u = lenU > 0.0001f ? (targetDist - lenL1 - lenL2) / lenU : 0f;
            pt = CalculateCubicBezierPoint(u, pU0, pU1, pU2, pU3);
        }

        // Apply Stem Backbone Cutoff
        if (z > 0)
        {
            float stemZAtY = sheerPlan.GetStemProfileZAtY(pt.y);
            if (z > stemZAtY + 0.001f) return null;
        }

        // Apply Fashion Timber Cutoff
        if (!ignoreFashionCutoff && cutRibbandsAtFashionTimber)
        {
            float sternZCut = sheerPlan.GetSternProfileZAtY(pt.y);
            if (z < sternZCut - 0.001f) return null;
        }

        return pt;
    }

    public Vector3? GetFrameSurfacePointOnDiagonal(float z, float side, float originY, float angleDeg, bool ignoreFashionCutoff = false)
    {
        if (sheerPlan == null || bodyPlan == null) return null;

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (!GetFrameControlPoints(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
            return null;

        float rad = angleDeg * Mathf.Deg2Rad;
        float cosA = Mathf.Cos(rad);
        float sinA = Mathf.Sin(rad);

        float minT = 0f;
        float maxT = 1f;

        for (int i = 0; i < 15; i++)
        {
            float midT = (minT + maxT) * 0.5f;
            Vector3 pt;
            if (midT <= 0.5f)
            {
                float u = midT * 2f;
                pt = u <= 0.5f ? CalculateQuadraticBezierPoint(u * 2f, pL0, pL1, pLR) : CalculateQuadraticBezierPoint((u - 0.5f) * 2f, pLR, pL2, pL3);
            }
            else
            {
                float u = (midT - 0.5f) * 2f;
                pt = CalculateCubicBezierPoint(u, pU0, pU1, pU2, pU3);
            }

            float dist = sinA * Mathf.Abs(pt.x) - cosA * (pt.y - originY);
            if (dist < 0f) minT = midT;
            else maxT = midT;
        }

        float finalT = (minT + maxT) * 0.5f;
        Vector3 finalPt;
        if (finalT <= 0.5f)
        {
            float u = finalT * 2f;
            finalPt = u <= 0.5f ? CalculateQuadraticBezierPoint(u * 2f, pL0, pL1, pLR) : CalculateQuadraticBezierPoint((u - 0.5f) * 2f, pLR, pL2, pL3);
        }
        else
        {
            float u = (finalT - 0.5f) * 2f;
            finalPt = CalculateCubicBezierPoint(u, pU0, pU1, pU2, pU3);
        }

        // Apply Stem Backbone Cutoff
        if (z > 0)
        {
            float stemZAtY = sheerPlan.GetStemProfileZAtY(finalPt.y);
            if (z > stemZAtY + 0.001f) return null;
        }

        // Apply Fashion Timber Cutoff
        if (!ignoreFashionCutoff && cutRibbandsAtFashionTimber)
        {
            float sternZCut = sheerPlan.GetSternProfileZAtY(finalPt.y);
            if (z < sternZCut - 0.001f) return null;
        }

        return finalPt;
    }

    private Vector3? GetFrameSurfacePointAtY(float z, float side, float targetY, bool ignoreFashionCutoff = false)
    {
        if (sheerPlan == null || bodyPlan == null) return null;

        // Stem Backbone Cutoff
        if (z > 0)
        {
            float stemZAtY = sheerPlan.GetStemProfileZAtY(targetY);
            if (z > stemZAtY + 0.001f) return null;
        }

        // Fashion Timber Cutoff
        if (!ignoreFashionCutoff && cutRibbandsAtFashionTimber)
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

    private void DrawPlankingStrakes()
    {
        if (bodyPlan == null || sheerPlan == null || breadthPlan == null) return;
        if (plankingStrakeCount <= 0) return;

        List<float> zPoints = GetZPoints();
        Gizmos.color = plankingColor;

        for (int k = 1; k <= plankingStrakeCount; k++)
        {
            float girthT = (float)k / (plankingStrakeCount + 1);

            Vector3? prevStbd = null;
            Vector3? prevPort = null;

            foreach (float z in zPoints)
            {
                Vector3? currStbd = GetFrameSurfacePointByGirthFraction(z, 1f, girthT);
                Vector3? currPort = GetFrameSurfacePointByGirthFraction(z, -1f, girthT);

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
    }

    private void DrawDiagonalsGuide()
    {
        if (bodyPlan == null || sheerPlan == null || breadthPlan == null) return;

        float[] angles = new float[] { 15f, 30f, 45f, 60f, 75f };
        List<float> zPoints = GetZPoints();
        Gizmos.color = diagonalsColor;

        float originY = GetFrameBottomYAtZ(0f);

        foreach (float deg in angles)
        {
            Vector3? prevStbd = null;
            Vector3? prevPort = null;

            foreach (float z in zPoints)
            {
                Vector3? currStbd = GetFrameSurfacePointOnDiagonal(z, 1f, originY, deg);
                Vector3? currPort = GetFrameSurfacePointOnDiagonal(z, -1f, originY, deg);

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
    }



    private float ApproximateQuadraticLength(Vector3 p0, Vector3 p1, Vector3 p2, int samples = 6)
    {
        float len = 0f;
        Vector3 prev = p0;
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 curr = CalculateQuadraticBezierPoint(t, p0, p1, p2);
            len += Vector3.Distance(prev, curr);
            prev = curr;
        }
        return len;
    }

    private float ApproximateCubicLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples = 6)
    {
        float len = 0f;
        Vector3 prev = p0;
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 curr = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            len += Vector3.Distance(prev, curr);
            prev = curr;
        }
        return len;
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

