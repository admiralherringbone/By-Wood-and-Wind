using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class FramingModel : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;

    [Header("Frame Layout Settings")]
    [Tooltip("Spacing distance between adjacent transverse frames along the Z axis.")]
    public float frameSpacing = 0.8f;

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

        stations.Sort();
        return stations;
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
        float yBot = GetFrameBottomYAtZ(z);
        float yTop = sheerPlan.GetPureSheerYAtZ(z);

        // Bottom X: 0 if climbing stem above keel, otherwise keel half-width
        float xBot = (z > sheerPlan.cachedKeelBowPoint.z) ? 0f : hw;

        // HoB Line anchor (Division point between Lower and Upper Moulds)
        float yHob = sheerPlan.GetHobYAtZ(z);
        float xHob = breadthPlan.GetBreadthXAtZ(z);
        float xTop = breadthPlan.GetTopTimberXAtZ(z);

        // Master Frame reference dimensions at Z = 0
        float masterLowerDx = bodyPlan.cachedL3.x - bodyPlan.cachedL0.x;
        float masterLowerDy = bodyPlan.cachedL3.y - bodyPlan.cachedL0.y;
        float masterUpperDx = bodyPlan.cachedU3.x - bodyPlan.cachedU0.x;
        float masterUpperDy = bodyPlan.cachedU3.y - bodyPlan.cachedU0.y;

        float localLowerDx = xHob - xBot;
        float localLowerDy = yHob - yBot;
        float localUpperDx = xTop - xHob;
        float localUpperDy = yTop - yHob;

        float sXLower = Mathf.Abs(masterLowerDx) > 0.001f ? localLowerDx / masterLowerDx : 0f;
        float sYLower = Mathf.Abs(masterLowerDy) > 0.001f ? localLowerDy / masterLowerDy : 0f;
        float sXUpper = Mathf.Abs(masterUpperDx) > 0.001f ? localUpperDx / masterUpperDx : 0f;
        float sYUpper = Mathf.Abs(masterUpperDy) > 0.001f ? localUpperDy / masterUpperDy : 0f;

        // LOWER MOULD SEGMENT 1 (Keel/Stem -> Rising Line Point)
        Vector3 pL0 = new Vector3(side * xBot, yBot, z);
        Vector3 pL3 = new Vector3(side * xHob, yHob, z);

        Vector3 masterL1Offset = bodyPlan.cachedL1 - bodyPlan.cachedL0;
        Vector3 masterL2Offset = bodyPlan.cachedL2 - bodyPlan.cachedL3;

        Vector3 pL1 = pL0 + new Vector3(side * masterL1Offset.x * sXLower, masterL1Offset.y * sYLower, 0);
        Vector3 pL2 = pL3 + new Vector3(side * masterL2Offset.x * sXLower, masterL2Offset.y * sYLower, 0);

        Vector3 pLR = Vector3.Lerp(pL1, pL2, bodyPlan.risingLinePosition);

        // LOWER MOULD SEGMENTS DRAWING
        DrawQuadraticBezierCurve(pL0, pL1, pLR, color, 15);
        DrawQuadraticBezierCurve(pLR, pL2, pL3, color, 15);

        // UPPER MOULD SEGMENT (HoB -> Top Line)
        Vector3 pU0 = pL3;
        Vector3 pU3 = new Vector3(side * xTop, yTop, z);

        Vector3 masterU1Offset = bodyPlan.cachedU1 - bodyPlan.cachedU0;
        Vector3 masterU2Offset = bodyPlan.cachedU2 - bodyPlan.cachedU3;

        Vector3 pU1 = pU0 + new Vector3(side * masterU1Offset.x * sXUpper, masterU1Offset.y * sYUpper, 0);
        Vector3 pU2 = pU3 + new Vector3(side * masterU2Offset.x * sXUpper, masterU2Offset.y * sYUpper, 0);

        DrawCubicBezierCurve(pU0, pU1, pU2, pU3, color, 15);

        // Gizmo Node Spheres for Master Frame
        if (isMaster)
        {
            Gizmos.color = color;
            Gizmos.DrawSphere(pL0, 0.12f);
            Gizmos.DrawSphere(pLR, 0.12f);
            Gizmos.DrawSphere(pL3, 0.12f);
            Gizmos.DrawSphere(pU3, 0.12f);
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

    private void DrawCubicBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color, int segments = 20)
    {
        Gizmos.color = color;
        Vector3 previousPoint = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
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
