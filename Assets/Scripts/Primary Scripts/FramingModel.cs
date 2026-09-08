using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class FramingModel : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FashionTimberModel fashionTimberModel;

    [Header("Frame Layout Settings")]
    [Tooltip("Spacing distance between adjacent transverse frames along the Z axis.")]
    public float frameSpacing = 0.8f;

    [Tooltip("Cut frames as they pass below the Fashion Timbers on the Y-axis.")]
    public bool cutFramesAtFashionTimber = true;

    [Header("Gizmo Visualization Settings")]
    public bool showLabels = true;
    public bool showBackboneSideLines = true;
    public bool showMasterFrame = true;
    public bool showFrames = true;

    public Color starboardColor = Color.cyan;
    public Color portColor = Color.magenta;
    public Color masterFrameColor = Color.yellow;
    public Color frameColor = new Color(0.2f, 0.8f, 1f, 0.7f);

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

        if (fashionTimberModel == null) fashionTimberModel = GetComponentInParent<FashionTimberModel>();
        if (fashionTimberModel == null) fashionTimberModel = FindFirstObjectByType<FashionTimberModel>();
    }

    public float GetKeelHalfWidth()
    {
        if (bodyPlan != null) return bodyPlan.keelWidth / 2f;
        return 0.25f;
    }

    public float GetFrameBottomYAtZ(float z)
    {
        if (sheerPlan == null) return 0f;
        
        // 1. Forward of Keel Bow Point: Climb the Stem Line
        if (z > sheerPlan.cachedKeelBowPoint.z)
        {
            return sheerPlan.GetStemProfileYAtZ(z);
        }

        // 2. Aft of Keel Stern Point: STAY ON KEEL LINE (never climb Stern Post or deadwood)
        if (z < sheerPlan.cachedKeelSternPoint.z)
        {
            return sheerPlan.cachedKeelSternPoint.y;
        }

        // 3. Between Keel Bow and Keel Stern: evaluate Keel Line Y
        if (sheerPlan.keelMidshipDrop != 0f)
        {
            float t = z >= 0 ? (z / sheerPlan.cachedKeelBowPoint.z) : (z / sheerPlan.cachedKeelSternPoint.z);
            Vector3 midP1 = z >= 0 ? new Vector3(0, 0, sheerPlan.cachedKeelBowPoint.z / 2f) : new Vector3(0, 0, sheerPlan.cachedKeelSternPoint.z / 2f);
            Vector3 endP = z >= 0 ? sheerPlan.cachedKeelBowPoint : sheerPlan.cachedKeelSternPoint;
            return CalculateQuadraticBezierPoint(t, sheerPlan.cachedMidshipKeel, midP1, endP).y;
        }

        return sheerPlan.cachedKeelSternPoint.y;
    }

    public List<float> CalculateFrameStations()
    {
        List<float> stations = new List<float>();
        if (sheerPlan == null) return stations;

        sheerPlan.UpdateCachedValues();
        float aftZ = sheerPlan.GetAftMostZ();
        float fwdZ = sheerPlan.GetFwdMostZ();

        if (frameSpacing <= 0.05f) frameSpacing = 0.05f;

        // Master Frame at Z = 0
        stations.Add(0f);

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

        // Filter out near-duplicate stations (within 5mm)
        List<float> uniqueStations = new List<float>();
        foreach (float s in stations)
        {
            if (uniqueStations.Count == 0 || Mathf.Abs(s - uniqueStations[uniqueStations.Count - 1]) > 0.005f)
            {
                uniqueStations.Add(s);
            }
        }

        return uniqueStations;
    }

    private Vector3 OffsetX(Vector3 point, float xOffset)
    {
        return new Vector3(xOffset, point.y, point.z);
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null) return;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        breadthPlan.UpdateCachedCurves();

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
        float hw = GetKeelHalfWidth();
        float keelY = GetFrameBottomYAtZ(z);
        float yTop = sheerPlan.GetPureSheerYAtZ(z);
        float hobY = sheerPlan.GetTheoreticalHobYAtZ(z);
        float hobBreadth = breadthPlan.GetTheoreticalBreadthXAtZ(z);
        float xTop = breadthPlan.GetTopTimberXAtZ(z);

        // Bottom X: Always respect Keel half-width
        float xBot = hw;

        // Baseline keel height for deadrise taper & scaling (Natural Floor Rise)
        float originalKeelY = keelY;

        // 1. ANCHORS (pL0 & pL3)
        Vector3 pL0 = new Vector3(side * xBot, keelY, z);
        Vector3 pL3 = new Vector3(side * hobBreadth, hobY, z);

        // 2. DIAGONAL DEADRISE TAPER (pL1 & targetLR)
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

                // Swept curve for P1 (Keel Control Arm)
                Vector3 p0_L1 = masterL1;
                Vector3 p1_L1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedBowP1Ctrl.z);
                Vector3 p2_L1 = bowEnd;
                targetL1_X = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).x;
                targetL1_Y = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).y;

                // Swept curve for LR (Rising Line Point)
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
                    // Phantom projection past Stern Rising End
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

        // 3. HOB DOWNWARD ARM (pL2)
        float currentBoxHeight = hobY - originalKeelY;
        float hobDrop = currentBoxHeight * bodyPlan.hobLowerArmLength;
        Vector3 pL2 = new Vector3(side * hobBreadth, Mathf.Max(hobY - hobDrop, targetLR_Y), z);

        // 4. RISING LINE POINT (pLR)
        float dx = Mathf.Abs(pL2.x - pL1.x);
        float lrPosRatio = bodyPlan.risingLinePosition;
        if (dx > 0.0001f)
        {
            lrPosRatio = Mathf.Clamp01((targetLR_X - targetL1_X) / dx);
        }
        Vector3 pLR = Vector3.Lerp(pL1, pL2, lrPosRatio);

        // 5. PROPORTIONAL Y-SCALING (STEM CLIMB)
        if (keelY > originalKeelY + 0.001f && hobY > originalKeelY + 0.001f)
        {
            float scaleY = (hobY - keelY) / (hobY - originalKeelY);

            pL1.y = keelY + (pL1.y - originalKeelY) * scaleY;
            pLR.y = keelY + (pLR.y - originalKeelY) * scaleY;
            pL2.y = keelY + (pL2.y - originalKeelY) * scaleY;
        }

        // FASHION TIMBER CUTOFF HEIGHT (Y-AXIS)
        float yCutoff = keelY;
        if (cutFramesAtFashionTimber && sheerPlan != null)
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

        // DRAW LOWER MOULD SEGMENTS (IF NOT COMPLETELY CUT BELOW FASHION TIMBER)
        if (yCutoff < hobY - 0.001f)
        {
            float lowerMinT = 0f;
            if (yCutoff > keelY + 0.001f)
            {
                float minT = 0f;
                float maxT = 1f;
                for (int i = 0; i < 15; i++)
                {
                    float midT = (minT + maxT) * 0.5f;
                    Vector3 pt = midT <= 0.5f ? CalculateQuadraticBezierPoint(midT * 2f, pL0, pL1, pLR) : CalculateQuadraticBezierPoint((midT - 0.5f) * 2f, pLR, pL2, pL3);
                    if (pt.y < yCutoff) minT = midT;
                    else maxT = midT;
                }
                lowerMinT = (minT + maxT) * 0.5f;
            }

            if (lowerMinT <= 0.5f)
            {
                DrawQuadraticBezierCurve(pL0, pL1, pLR, color, 15, lowerMinT * 2f, 1f);
                DrawQuadraticBezierCurve(pLR, pL2, pL3, color, 15, 0f, 1f);
            }
            else
            {
                DrawQuadraticBezierCurve(pLR, pL2, pL3, color, 15, (lowerMinT - 0.5f) * 2f, 1f);
            }
        }

        // UPPER MOULD SEGMENT (HoB -> Top Line)
        float maxSheerYAtZ = sheerPlan.GetBaseDeckYAtZ(z);
        if (sheerPlan.upperDecks != null && sheerPlan.upperDecks.Count > 0)
        {
            maxSheerYAtZ += sheerPlan.GetMaxDeckHeight() + sheerPlan.GetGlobalSheerOffsetAtZ(z);
        }
        else
        {
            maxSheerYAtZ = sheerPlan.GetTopTimberYAtZ(z);
        }

        float localTopY = sheerPlan.GetTopTimberYAtZ(z);

        Vector3 pU0 = pL3;
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

        // UPPER MOULD CLIPPING LOGIC: Cut at top height and cut below Fashion Timber if yCutoff >= hobY
        float upperMinT = 0f;
        if (yCutoff >= hobY - 0.001f)
        {
            float minT = 0f;
            float maxT = 1f;
            for (int i = 0; i < 15; i++)
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
            for (int i = 0; i < 10; i++)
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

        // Gizmo Node Spheres for Master Frame
        if (isMaster)
        {
            Gizmos.color = color;
            if (yCutoff < hobY) Gizmos.DrawSphere(pL0, 0.12f);
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
