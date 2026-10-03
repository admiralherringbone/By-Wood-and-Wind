using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum FrameSpacingMode
{
    UniformZ,
    PrimaryBreadthCurve
}

[ExecuteInEditMode]
public class FramingModel : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FashionTimberModel fashionTimberModel;

    [Header("Master Frame Settings")]
    [Tooltip("Render the Master Frame at midship (Z = 0) matching the Body Plan curve.")]
    public bool showMasterFrame = true;

    [Tooltip("Color of the Master Frame section curve.")]
    public Color masterFrameColor = Color.yellow;

    [Tooltip("Draw node spheres (p0 Keel, p1 Floor, p2 Bilge, p3 HOB, p4 Sheer) on the Master Frame.")]
    public bool showMasterFrameNodes = true;

    [Header("Longitudinal Ribband Lines Settings")]
    [Tooltip("Render longitudinal 3D ribband guide lines (strakes) tracing structural nodes across the hull.")]
    public bool showRibbands = true;

    [Tooltip("Render Top Timber Ribband (p4 Sheer / Rail Line).")]
    public bool showTopTimberRibband = true;
    public Color topTimberRibbandColor = new Color(0.9f, 0.2f, 0.9f, 0.85f);

    [Tooltip("Render Max Breadth Ribband (p3 Height of Breadth Line).")]
    public bool showMaxBreadthRibband = true;
    public Color maxBreadthRibbandColor = new Color(0.1f, 0.85f, 0.9f, 0.85f);

    [Tooltip("Render Bilge Ribband (p2 Diagonal Bilge Line).")]
    public bool showBilgeRibband = true;
    public Color bilgeRibbandColor = new Color(0.1f, 0.9f, 0.3f, 0.85f);

    [Tooltip("Render Floor Ribband (p1 Floor / Deadrise Line).")]
    public bool showFloorRibband = true;
    public Color floorRibbandColor = new Color(1f, 0.6f, 0.1f, 0.85f);

    [Header("Frame Layout Settings")]
    [Tooltip("Method for spacing transverse frames along the hull (Uniform Z vs. Primary Breadth curve).")]
    public FrameSpacingMode frameSpacingMode = FrameSpacingMode.UniformZ;

    [Tooltip("Spacing distance between adjacent transverse frames along the Z axis (or along Primary Breadth curve).")]
    public float frameSpacing = 0.8f;

    [Tooltip("Cut frames as they pass below the Fashion Timbers on the Y-axis.")]
    public bool cutFramesAtFashionTimber = true;

    [Tooltip("Disable all frame cutoffs to display frames completely whole and uncut by Fashion Timbers, Upper Decks, or Sheer line profile.")]
    public bool disableFrameCutoffs = false;

    [Header("Longitudinal Control Point Trajectory Fairing")]
    [Tooltip("Enable 3D C2 longitudinal spline trajectory fairing across frame control points to guarantee 100% glass-smooth frames stem-to-stern.")]
    public bool enableLongitudinalTrajectoryFairing = true;

    [Range(0f, 1f)]
    [Tooltip("Strength blend factor for longitudinal control point trajectory fairing (0 = raw station math, 1 = 100% C2 smooth trajectories).")]
    public float trajectoryFairingStrength = 1.0f;

    [Range(5, 50)]
    [Tooltip("Number of master longitudinal anchor stations sampled to build the smooth control point trajectories.")]
    public int trajectoryAnchorStations = 20;

    [Header("Gizmo Visualization Settings")]
    public bool showLabels = true;
    public bool showFrames = true;

    public Color starboardColor = Color.cyan;
    public Color portColor = Color.magenta;
    public Color frameColor = new Color(0.2f, 0.8f, 1f, 0.7f);

    [HideInInspector] public int changeVersion = 0;

    private List<float> cachedStationList = new List<float>();
    private bool isStationListDirty = true;

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        isStationListDirty = true;
        changeVersion++;
        FindReferences();
    }

    public void MarkStationsDirty()
    {
        isStationListDirty = true;
        changeVersion++;
    }

    public void FindReferences()
    {
        if (sheerPlan == null)
        {
            sheerPlan = GetComponentInParent<SheerPlan>();
            if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        }

        if (bodyPlan == null)
        {
            bodyPlan = GetComponentInParent<BodyPlan>();
            if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        }

        if (breadthPlan == null)
        {
            breadthPlan = GetComponentInParent<BreadthPlan>();
            if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
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
        sheerPlan.UpdateCachedValues();

        // For any frames at or aft of Stern End of Rising Line:
        if (z <= sheerPlan.cachedSternRisingEnd.z)
        {
            float ftY = sheerPlan.GetSternProfileYAtZ(z);
            float minPostZ = Mathf.Min(sheerPlan.cachedKeelSternPoint.z, sheerPlan.cachedSternPostTopPoint.z);
            if (z > minPostZ && z <= sheerPlan.cachedSternRisingEnd.z)
            {
                float dz = sheerPlan.cachedSternRisingEnd.z - minPostZ;
                if (Mathf.Abs(dz) > 0.0001f)
                {
                    float t = (z - minPostZ) / dz;
                    float interpY = Mathf.Lerp(sheerPlan.cachedKeelSternPoint.y, sheerPlan.cachedSternRisingEnd.y, t);
                    ftY = Mathf.Max(ftY, interpY);
                }
            }
            return ftY;
        }

        if (z >= sheerPlan.cachedBowRisingEnd.z)
        {
            return sheerPlan.GetStemProfileYAtZ(z);
        }

        return sheerPlan.GetKeelYAtZ(z);
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

        stations.Add(0f);
        stations.Add(fwdZ);

        if (frameSpacingMode == FrameSpacingMode.PrimaryBreadthCurve && breadthPlan != null)
        {
            breadthPlan.UpdateCachedCurves();

            float dZ = 0.02f;
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
            float curZ = frameSpacing;
            while (curZ <= fwdZ)
            {
                stations.Add(curZ);
                curZ += frameSpacing;
            }

            curZ = -frameSpacing;
            while (curZ >= aftZ)
            {
                stations.Add(curZ);
                curZ -= frameSpacing;
            }
        }

        stations.Sort();

        List<float> uniqueStations = new List<float>();
        foreach (float s in stations)
        {
            if (uniqueStations.Count == 0 || Mathf.Abs(s - uniqueStations[uniqueStations.Count - 1]) > 0.025f)
            {
                uniqueStations.Add(s);
            }
        }

        cachedStationList = uniqueStations;
        isStationListDirty = false;
        return cachedStationList;
    }

    public SectionNodes GetSectionNodesForFrame(float z, float side)
    {
        if (bodyPlan == null) return new SectionNodes();
        SectionNodes nodes = bodyPlan.GetSectionNodesAtZ(z, side);

        // For any frames aft of Stern end of Rising Line (z < cachedSternRisingEnd.z):
        // p0 and p1 sit on the Projected Stern Rising Line, NOT climbing the Stern Profile curve!
        if (sheerPlan != null && z < sheerPlan.cachedSternRisingEnd.z)
        {
            float projRisingY = sheerPlan.GetProjectedSternRisingYAtZ(z);
            nodes.p0.y = projRisingY;
            nodes.p1.y = projRisingY;
        }

        return nodes;
    }

    public bool GetFrameControlPointsAtZ(float z, float side,
        out Vector3 pL0, out Vector3 pL1, out Vector3 pLR, out Vector3 pL2, out Vector3 pL3,
        out Vector3 pU0, out Vector3 pU1, out Vector3 pU2, out Vector3 pU3)
    {
        pL0 = pL1 = pLR = pL2 = pL3 = pU0 = pU1 = pU2 = pU3 = Vector3.zero;
        if (bodyPlan == null) return false;

        SectionNodes nodes = GetSectionNodesForFrame(z, side);
        pL0 = nodes.p0;
        pL1 = nodes.p1;
        pLR = nodes.p1;
        pL2 = nodes.p2;
        pL3 = nodes.p3;
        pU0 = nodes.p3;
        pU1 = nodes.p3;
        pU2 = nodes.p4;
        pU3 = nodes.p4;
        return true;
    }

    public Vector3 EvaluateFramePointFromControlPoints(float u, Vector3 pL0, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3, Vector3 pU0, Vector3 pU1, Vector3 pU2, Vector3 pU3)
    {
        float z = pL0.z;
        float side = Mathf.Sign(pL0.x);
        if (bodyPlan != null)
        {
            SectionNodes nodes = GetSectionNodesForFrame(z, side);
            return bodyPlan.EvaluateBodySectionAtU(u, nodes);
        }
        return Vector3.Lerp(pL0, pU3, u);
    }

    public Vector3 GetTransverseFramePointAtYFromControlPoints(
        Vector3 pL0, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3,
        Vector3 pU0, Vector3 pU1, Vector3 pU2, Vector3 pU3,
        float targetY)
    {
        float z = pL0.z;
        float side = Mathf.Sign(pL0.x);
        return GetTransverseFramePointAtY(z, side, targetY);
    }

    public Vector3 GetTransverseFramePointAtY(float z, float side, float targetY)
    {
        if (bodyPlan == null) return Vector3.zero;
        SectionNodes nodes = GetSectionNodesForFrame(z, side);

        float minU = 0f;
        float maxU = 1f;
        for (int i = 0; i < 20; i++)
        {
            float midU = (minU + maxU) * 0.5f;
            Vector3 pt = bodyPlan.EvaluateBodySectionAtU(midU, nodes);
            if (pt.y < targetY) minU = midU;
            else maxU = midU;
        }

        return bodyPlan.EvaluateBodySectionAtU((minU + maxU) * 0.5f, nodes);
    }

    // --- GIZMOS VISUALIZATION ---

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null) return;

        // 1. Master Frame at Z = 0
        if (showMasterFrame)
        {
            DrawMasterFrameHalf(1f, starboardColor);
            DrawMasterFrameHalf(-1f, portColor);
        }

        // 2. Longitudinal Ribband Lines (Strakes / Skeleton Guides)
        if (showRibbands)
        {
            DrawRibbands();
        }

        // 3. Transverse Frames
        if (showFrames)
        {
            DrawTransverseFrames();
        }

    }

    private void DrawMasterFrameHalf(float sideSign, Color color)
    {
        SectionNodes nodes = bodyPlan.GetSectionNodesAtZ(0f, sideSign);
        const int steps = 60;

        Vector3 prevPt = nodes.p0;
        Gizmos.color = masterFrameColor;
        for (int i = 1; i <= steps; i++)
        {
            float u = (float)i / steps;
            Vector3 currPt = bodyPlan.EvaluateBodySectionAtU(u, nodes);
            Gizmos.DrawLine(prevPt, currPt);
            prevPt = currPt;
        }

        if (showMasterFrameNodes)
        {
            Gizmos.color = Color.yellow; Gizmos.DrawSphere(nodes.p0, 0.12f);
            Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawSphere(nodes.p1, 0.12f);
            Gizmos.color = new Color(0.1f, 0.9f, 0.4f); Gizmos.DrawSphere(nodes.p2, 0.12f);
            Gizmos.color = Color.cyan; Gizmos.DrawSphere(nodes.p3, 0.12f);
            Gizmos.color = Color.magenta; Gizmos.DrawSphere(nodes.p4, 0.12f);

#if UNITY_EDITOR
            if (showLabels)
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.fontSize = 11;
                style.fontStyle = FontStyle.Bold;

                Handles.Label(nodes.p0 + new Vector3(0, -0.2f, 0), "p0 Keel", style);
                Handles.Label(nodes.p1 + new Vector3(0, -0.2f, 0), "p1 Floor", style);
                Handles.Label(nodes.p2 + new Vector3(0.2f, 0, 0), "p2 Bilge", style);
                Handles.Label(nodes.p3 + new Vector3(0.2f, 0, 0), "p3 HOB", style);
                Handles.Label(nodes.p4 + new Vector3(0.2f, 0, 0), "p4 Sheer", style);
            }
#endif
        }
    }

    private void DrawRibbands()
    {
        const int samples = 100;

        // 1. Top Timber Ribband (Sheer / Top Line p4)
        if (showTopTimberRibband)
        {
            Gizmos.color = topTimberRibbandColor;
            float ttAftZ = sheerPlan.cachedTopTimberSternIntersect.z;
            float ttFwdZ = sheerPlan.cachedTopTimberBowIntersect.z;
            Vector3 prevStbd = Vector3.zero;
            Vector3 prevPort = Vector3.zero;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float z = Mathf.Lerp(ttAftZ, ttFwdZ, t);
                SectionNodes nodesStbd = bodyPlan.GetSectionNodesAtZ(z, 1f);
                SectionNodes nodesPort = bodyPlan.GetSectionNodesAtZ(z, -1f);

                Vector3 currStbd = nodesStbd.p4;
                Vector3 currPort = nodesPort.p4;
                if (i > 0)
                {
                    Gizmos.DrawLine(prevStbd, currStbd);
                    Gizmos.DrawLine(prevPort, currPort);
                }
                prevStbd = currStbd;
                prevPort = currPort;
            }
        }

        // 2. Max Breadth Ribband (Height of Breadth Line p3)
        if (showMaxBreadthRibband)
        {
            Gizmos.color = maxBreadthRibbandColor;
            float hobAftZ = sheerPlan.cachedHobSternIntersect.z;
            float hobFwdZ = sheerPlan.cachedHobBowIntersect.z;
            Vector3 prevStbd = Vector3.zero;
            Vector3 prevPort = Vector3.zero;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float z = Mathf.Lerp(hobAftZ, hobFwdZ, t);
                SectionNodes nodesStbd = bodyPlan.GetSectionNodesAtZ(z, 1f);
                SectionNodes nodesPort = bodyPlan.GetSectionNodesAtZ(z, -1f);

                Vector3 currStbd = nodesStbd.p3;
                Vector3 currPort = nodesPort.p3;
                if (i > 0)
                {
                    Gizmos.DrawLine(prevStbd, currStbd);
                    Gizmos.DrawLine(prevPort, currPort);
                }
                prevStbd = currStbd;
                prevPort = currPort;
            }
        }

        // 3. Diagonal Bilge Ribband (Bilge Line p2)
        if (showBilgeRibband)
        {
            Gizmos.color = bilgeRibbandColor;
            float bilgeAftZ = sheerPlan.GetBilgeSternZ();
            float bilgeFwdZ = sheerPlan.GetBilgeBowZ();
            Vector3 prevStbd = Vector3.zero;
            Vector3 prevPort = Vector3.zero;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float z = Mathf.Lerp(bilgeAftZ, bilgeFwdZ, t);
                SectionNodes nodesStbd = bodyPlan.GetSectionNodesAtZ(z, 1f);
                SectionNodes nodesPort = bodyPlan.GetSectionNodesAtZ(z, -1f);

                Vector3 currStbd = nodesStbd.p2;
                Vector3 currPort = nodesPort.p2;
                if (i > 0)
                {
                    Gizmos.DrawLine(prevStbd, currStbd);
                    Gizmos.DrawLine(prevPort, currPort);
                }
                prevStbd = currStbd;
                prevPort = currPort;
            }
        }

        // 4. Floor Ribband (Floor / Deadrise Line p1)
        if (showFloorRibband)
        {
            Gizmos.color = floorRibbandColor;
            float floorAftZ = sheerPlan.cachedSternRisingEnd.z;
            float floorFwdZ = sheerPlan.cachedBowRisingEnd.z;
            Vector3 prevStbd = Vector3.zero;
            Vector3 prevPort = Vector3.zero;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float z = Mathf.Lerp(floorAftZ, floorFwdZ, t);
                SectionNodes nodesStbd = bodyPlan.GetSectionNodesAtZ(z, 1f);
                SectionNodes nodesPort = bodyPlan.GetSectionNodesAtZ(z, -1f);

                Vector3 currStbd = nodesStbd.p1;
                Vector3 currPort = nodesPort.p1;
                if (i > 0)
                {
                    Gizmos.DrawLine(prevStbd, currStbd);
                    Gizmos.DrawLine(prevPort, currPort);
                }
                prevStbd = currStbd;
                prevPort = currPort;
            }
        }
    }

    private void DrawTransverseFrames()
    {
        if (!showFrames || bodyPlan == null) return;

        List<float> stations = CalculateFrameStations();
        const int steps = 60;

        foreach (float z in stations)
        {
            SectionNodes nodesStbd = GetSectionNodesForFrame(z, 1f);
            SectionNodes nodesPort = GetSectionNodesForFrame(z, -1f);

            bool applyCutoff = !disableFrameCutoffs && sheerPlan != null;
            float bottomY = applyCutoff ? GetFrameBottomYAtZ(z) : -9999f;

            // Starboard Frame Half
            Gizmos.color = starboardColor;
            Vector3 prevStbd = nodesStbd.p0;
            for (int i = 1; i <= steps; i++)
            {
                float u = (float)i / steps;
                Vector3 currStbd = bodyPlan.EvaluateBodySectionAtU(u, nodesStbd);

                if (applyCutoff)
                {
                    if (currStbd.y < bottomY - 0.0001f && prevStbd.y < bottomY - 0.0001f)
                    {
                        prevStbd = currStbd;
                        continue;
                    }

                    Vector3 drawStart = prevStbd;
                    Vector3 drawEnd = currStbd;

                    if (prevStbd.y < bottomY && currStbd.y >= bottomY)
                    {
                        float dy = currStbd.y - prevStbd.y;
                        float t = (Mathf.Abs(dy) > 0.00001f) ? Mathf.Clamp01((bottomY - prevStbd.y) / dy) : 0f;
                        drawStart = Vector3.Lerp(prevStbd, currStbd, t);
                    }
                    else if (prevStbd.y >= bottomY && currStbd.y < bottomY)
                    {
                        float dy = currStbd.y - prevStbd.y;
                        float t = (Mathf.Abs(dy) > 0.00001f) ? Mathf.Clamp01((bottomY - prevStbd.y) / dy) : 0f;
                        drawEnd = Vector3.Lerp(prevStbd, currStbd, t);
                    }

                    Gizmos.DrawLine(drawStart, drawEnd);
                }
                else
                {
                    Gizmos.DrawLine(prevStbd, currStbd);
                }
                prevStbd = currStbd;
            }

            // Port Frame Half
            Gizmos.color = portColor;
            Vector3 prevPort = nodesPort.p0;
            for (int i = 1; i <= steps; i++)
            {
                float u = (float)i / steps;
                Vector3 currPort = bodyPlan.EvaluateBodySectionAtU(u, nodesPort);

                if (applyCutoff)
                {
                    if (currPort.y < bottomY - 0.0001f && prevPort.y < bottomY - 0.0001f)
                    {
                        prevPort = currPort;
                        continue;
                    }

                    Vector3 drawStart = prevPort;
                    Vector3 drawEnd = currPort;

                    if (prevPort.y < bottomY && currPort.y >= bottomY)
                    {
                        float dy = currPort.y - prevPort.y;
                        float t = (Mathf.Abs(dy) > 0.00001f) ? Mathf.Clamp01((bottomY - prevPort.y) / dy) : 0f;
                        drawStart = Vector3.Lerp(prevPort, currPort, t);
                    }
                    else if (prevPort.y >= bottomY && currPort.y < bottomY)
                    {
                        float dy = currPort.y - prevPort.y;
                        float t = (Mathf.Abs(dy) > 0.00001f) ? Mathf.Clamp01((bottomY - prevPort.y) / dy) : 0f;
                        drawEnd = Vector3.Lerp(prevPort, currPort, t);
                    }

                    Gizmos.DrawLine(drawStart, drawEnd);
                }
                else
                {
                    Gizmos.DrawLine(prevPort, currPort);
                }
                prevPort = currPort;
            }
        }
    }

}
