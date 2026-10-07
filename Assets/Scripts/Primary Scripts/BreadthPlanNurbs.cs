using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// BreadthPlanNurbs implements an 8-Point High-Resolution Interpolating NURBS B-Spline for BreadthPlan.
/// Includes a dedicated Bow Entry Cutwater Waypoint (W6) for ultra-smooth bow curvature.
/// All waypoints lie 100% DIRECTLY ON THE CURVE with high-density segment sampling.
/// </summary>
[ExecuteInEditMode]
public class BreadthPlanNurbs : BreadthPlan
{
    [Header("Interactive Scene Handles")]
    [Tooltip("Enable interactive click-and-drag handles in Scene view to sculpt waypoints directly on the curve.")]
    public bool enableInteractiveNodeHandles = true;

    [Header("NURBS Interpolating Spline Controls")]
    [Tooltip("Tension factor of the interpolating B-Spline curve (1.0 = standard Hermite B-Spline, 0.5 = stiff, 1.5 = flexible).")]
    [Range(0.5f, 2.0f)] public float splineTension = 1.0f;

    [Tooltip("Number of high-resolution curve samples rendered per segment to eliminate any polygonal artifacts.")]
    [Range(15, 60)] public int stepsPerSegment = 35;

    [Header("Arced Inward Boundary Constraints")]
    [Tooltip("Curvature bulge factor for inward arced boundary constraints (0.0 = straight chord, 0.2 = smooth convex fairing arc).")]
    [Range(0.0f, 0.5f)] public float boundaryArcBulge = 0.20f;

    [Tooltip("Flag to initialize default waypoints if unassigned.")]
    public bool isInitialized = false;

    // --- PURE INDEPENDENT WAYPOINTS (8 WAYPOINTS FOR HIGH-RES BOW SCULPTING) ---
    [Header("Max Breadth Line Waypoints (W0..W7)")]
    public Vector3[] nurbsBreadthWaypoints = new Vector3[8];

    [Header("Top Timber Line Waypoints (TT0..TT7)")]
    public Vector3[] nurbsTopTimberWaypoints = new Vector3[8];

    [Header("Floor Line Waypoints (FL0..FL5)")]
    public Vector3[] nurbsFloorWaypoints = new Vector3[6];

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Reset()
    {
        InitializeWaypointsFromShip();
    }

    public void EnsureInitialized()
    {
        if (!isInitialized || nurbsBreadthWaypoints == null || nurbsBreadthWaypoints.Length != 8)
        {
            InitializeWaypointsFromShip();
        }
    }

    [ContextMenu("Reset Waypoints to Default Ship Shape")]
    public void InitializeWaypointsFromShip()
    {
        if (sheerPlan == null) sheerPlan = GetComponentInParent<SheerPlan>();
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (bodyPlan == null) bodyPlan = GetComponentInParent<BodyPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();

        float bowZ = (sheerPlan != null) ? sheerPlan.cachedHobBowIntersect.z : 15f;
        float sternZ = (sheerPlan != null) ? sheerPlan.cachedHobSternIntersect.z : -15f;
        float baseY = (sheerPlan != null) ? sheerPlan.cachedHobMidshipPoint.y : 2f;
        float currentMaxBeam = maxBeam > 0.5f ? maxBeam : 4f;
        float hw = (bodyPlan != null) ? bodyPlan.keelWidth / 2f : 0.25f;

        // 8 Waypoints on Max Breadth line with high-resolution Bow Entry (W6)
        // W0 = Stern Tip/Transom, W1 = Stern Quarter, W2 = Aft Run, W3 = Midship Apex (Z=0)
        // W4 = Fwd Entrance, W5 = Bow Shoulder, W6 = Bow Entry Cutwater (Extra Resolution), W7 = Bow Stem Tip
        nurbsBreadthWaypoints = new Vector3[8]
        {
            new Vector3(currentMaxBeam * transomWidth, baseY, sternZ),           // W0: Stern Tip
            new Vector3(currentMaxBeam * 0.75f, baseY, sternZ * 0.66f),          // W1: Stern Quarter
            new Vector3(currentMaxBeam * 0.95f, baseY, sternZ * 0.33f),          // W2: Aft Run
            new Vector3(currentMaxBeam, baseY, 0f),                              // W3: Midship Apex (Z=0)
            new Vector3(currentMaxBeam * 0.95f, baseY, bowZ * 0.30f),            // W4: Fwd Entrance
            new Vector3(currentMaxBeam * 0.75f, baseY, bowZ * 0.60f),            // W5: Bow Shoulder
            new Vector3(currentMaxBeam * 0.40f, baseY, bowZ * 0.85f),            // W6: Bow Entry Cutwater (High-Res)
            new Vector3(hw, baseY, bowZ)                                         // W7: Bow Stem Tip
        };

        // Top Timber waypoints (8 Points)
        float ttBowZ = (sheerPlan != null) ? sheerPlan.cachedUpperStemTopPoint.z : bowZ;
        float ttSternZ = (sheerPlan != null) ? sheerPlan.cachedTransomTopPoint.z : sternZ;
        float ttBaseY = (sheerPlan != null) ? sheerPlan.cachedTopTimberMidshipPoint.y : (baseY + 3f);
        float ttBeam = topTimberMaxBeam > 0.5f ? topTimberMaxBeam : (currentMaxBeam * 0.9f);

        nurbsTopTimberWaypoints = new Vector3[8]
        {
            new Vector3(ttBeam * topTimberTransomWidth, ttBaseY, ttSternZ),
            new Vector3(ttBeam * 0.75f, ttBaseY, ttSternZ * 0.66f),
            new Vector3(ttBeam * 0.95f, ttBaseY, ttSternZ * 0.33f),
            new Vector3(ttBeam, ttBaseY, 0f),
            new Vector3(ttBeam * 0.95f, ttBaseY, ttBowZ * 0.30f),
            new Vector3(ttBeam * 0.75f, ttBaseY, ttBowZ * 0.60f),
            new Vector3(ttBeam * 0.40f, ttBaseY, ttBowZ * 0.85f),
            new Vector3(hw, ttBaseY, ttBowZ)
        };

        // Floor Line waypoints (6 Points)
        float floorBeam = midshipFloorWidth > 0.5f ? midshipFloorWidth : (currentMaxBeam * 0.5f);
        float floorBowZ = (sheerPlan != null) ? sheerPlan.cachedBowRisingEnd.z : (bowZ * 0.8f);
        float floorSternZ = (sheerPlan != null) ? sheerPlan.cachedSternRisingEnd.z : (sternZ * 0.8f);
        float floorY = (sheerPlan != null) ? sheerPlan.GetRisingLineYAtZ(0f) : 0.5f;

        nurbsFloorWaypoints = new Vector3[6]
        {
            new Vector3(hw, floorY, floorSternZ),
            new Vector3(floorBeam * 0.7f, floorY, floorSternZ * 0.5f),
            new Vector3(floorBeam, floorY, 0f),
            new Vector3(floorBeam * 0.8f, floorY, floorBowZ * 0.4f),
            new Vector3(floorBeam * 0.4f, floorY, floorBowZ * 0.8f),
            new Vector3(hw, floorY, floorBowZ)
        };

        isInitialized = true;
    }

    public override void UpdateCachedCurves()
    {
        EnsureInitialized();

        if (sheerPlan == null) sheerPlan = GetComponentInParent<SheerPlan>();
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (bodyPlan == null) bodyPlan = GetComponentInParent<BodyPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();

        if (sheerPlan == null) return;

        // Sync core parameters from pure waypoint positions for inter-script compatibility
        maxBeam = nurbsBreadthWaypoints[3].x;
        nurbsBreadthWaypoints[3].z = 0f; // Midship is strictly Z = 0

        cachedFwdP0 = nurbsBreadthWaypoints[7]; // Bow Stem Tip
        cachedFwdP3 = nurbsBreadthWaypoints[3]; // Midship Apex
        cachedAftP0 = nurbsBreadthWaypoints[3]; // Midship Apex
        cachedAftP3 = nurbsBreadthWaypoints[0]; // Stern Tip

        cachedTTFwdP0 = nurbsTopTimberWaypoints[7];
        cachedTTFwdP3 = nurbsTopTimberWaypoints[3];
        cachedTTAftP0 = nurbsTopTimberWaypoints[3];
        cachedTTAftP3 = nurbsTopTimberWaypoints[0];

        cachedFloorBowPoint = nurbsFloorWaypoints[5];
        cachedFloorMidshipPoint = nurbsFloorWaypoints[2];
        cachedFloorSternPoint = nurbsFloorWaypoints[0];

        midshipFloorWidth = nurbsFloorWaypoints[2].x;

        changeVersion++;
    }

    // --- HIGH-RESOLUTION INTERPOLATING B-SPLINE EVALUATION ---

    public override float GetBreadthXAtZ(float z)
    {
        if (nurbsBreadthWaypoints == null || nurbsBreadthWaypoints.Length < 8) return maxBeam;

        int lastIdx = nurbsBreadthWaypoints.Length - 1;
        if (z >= nurbsBreadthWaypoints[lastIdx].z) return nurbsBreadthWaypoints[lastIdx].x;
        if (z <= nurbsBreadthWaypoints[0].z) return nurbsBreadthWaypoints[0].x;

        Vector3 pt = EvaluateInterpolatingSpline(nurbsBreadthWaypoints, z);
        // Safety Cap: Evaluated breadth curve cannot exceed maxBeam (W3.x)
        return Mathf.Min(pt.x, maxBeam);
    }

    public override float GetTheoreticalBreadthXAtZ(float z)
    {
        if (nurbsBreadthWaypoints == null || nurbsBreadthWaypoints.Length < 8) return maxBeam;

        if (z >= 0) return GetBreadthXAtZ(z);

        if (z > nurbsBreadthWaypoints[0].z) return GetBreadthXAtZ(z);

        // Linear extrapolation past stern tip (W0)
        float dz = nurbsBreadthWaypoints[1].z - nurbsBreadthWaypoints[0].z;
        float dx = nurbsBreadthWaypoints[1].x - nurbsBreadthWaypoints[0].x;
        if (Mathf.Abs(dz) < 0.001f) return nurbsBreadthWaypoints[0].x;

        return nurbsBreadthWaypoints[0].x + (dx / dz) * (z - nurbsBreadthWaypoints[0].z);
    }

    public override float GetTopTimberXAtZ(float z)
    {
        if (nurbsTopTimberWaypoints == null || nurbsTopTimberWaypoints.Length < 8) return topTimberMaxBeam;

        int lastIdx = nurbsTopTimberWaypoints.Length - 1;
        float rawX;
        if (z >= nurbsTopTimberWaypoints[lastIdx].z) rawX = nurbsTopTimberWaypoints[lastIdx].x;
        else if (z <= nurbsTopTimberWaypoints[0].z) rawX = nurbsTopTimberWaypoints[0].x;
        else
        {
            Vector3 pt = EvaluateInterpolatingSpline(nurbsTopTimberWaypoints, z);
            rawX = pt.x;
        }

        float hobX = GetTheoreticalBreadthXAtZ(z);
        float minTumblehomeX = hobX * 0.60f;
        return Mathf.Max(rawX, minTumblehomeX);
    }

    public override float GetFloorLineXAtZ(float z)
    {
        if (nurbsFloorWaypoints == null || nurbsFloorWaypoints.Length < 6) return midshipFloorWidth;

        int lastIdx = nurbsFloorWaypoints.Length - 1;
        float rawX;
        if (z >= nurbsFloorWaypoints[lastIdx].z) rawX = nurbsFloorWaypoints[lastIdx].x;
        else if (z <= nurbsFloorWaypoints[0].z) rawX = nurbsFloorWaypoints[0].x;
        else
        {
            Vector3 pt = EvaluateInterpolatingSpline(nurbsFloorWaypoints, z);
            rawX = pt.x;
        }

        float minKeelX = (bodyPlan != null) ? (bodyPlan.keelWidth / 2f) : 0.25f;
        return Mathf.Clamp(rawX, minKeelX, GetTheoreticalBreadthXAtZ(z));
    }

    public Vector3 EvaluateInterpolatingSpline(Vector3[] waypoints, float targetZ)
    {
        int n = waypoints.Length;
        if (n < 2) return waypoints[0];

        int idx = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (targetZ >= waypoints[i].z && targetZ <= waypoints[i + 1].z)
            {
                idx = i;
                break;
            }
            if (i == n - 2) idx = n - 2;
        }

        Vector3 pA = waypoints[idx];
        Vector3 pB = waypoints[idx + 1];

        float zSpan = pB.z - pA.z;
        float t = (Mathf.Abs(zSpan) > 0.0001f) ? Mathf.Clamp01((targetZ - pA.z) / zSpan) : 0f;

        Vector3 tA = GetWaypointTangent(idx, waypoints);
        Vector3 tB = GetWaypointTangent(idx + 1, waypoints);

        return EvaluateCubicHermiteSegment(pA, tA, pB, tB, t, splineTension);
    }

    public Vector3 GetWaypointTangent(int i, Vector3[] waypoints)
    {
        int n = waypoints.Length;
        if (n < 2) return Vector3.forward;

        if (n >= 7 && i == 3) return Vector3.forward; // Midship zero X-tangent

        if (i == 0) return (waypoints[1] - waypoints[0]).normalized;
        if (i == n - 1) return (waypoints[n - 1] - waypoints[n - 2]).normalized;

        return (waypoints[i + 1] - waypoints[i - 1]).normalized;
    }

    private Vector3 EvaluateCubicHermiteSegment(Vector3 pA, Vector3 tA, Vector3 pB, Vector3 tB, float t, float tension)
    {
        t = Mathf.Clamp01(t);
        float dist = Vector3.Distance(pA, pB);
        if (dist <= 0.0001f) return pA;

        float armLen = (dist / 3f) * Mathf.Clamp(tension, 0.5f, 2.0f);
        Vector3 cA = pA + tA * armLen;
        Vector3 cB = pB - tB * armLen;

        float u = 1f - t;
        float u2 = u * u;
        float u3 = u2 * u;
        float t2 = t * t;
        float t3 = t2 * t;

        return (u3 * pA) + (3f * u2 * t * cA) + (3f * u * t2 * cB) + (t3 * pB);
    }

    // --- ARCED INWARD BOUNDARY CALCULATIONS & RENDERING ---

    public float GetArcedBoundaryX(Vector3 pA, Vector3 pB, float z)
    {
        float dz = pB.z - pA.z;
        if (Mathf.Abs(dz) < 0.0001f) return Mathf.Abs(pA.x);

        float t = Mathf.Clamp01((z - pA.z) / dz);
        float absAx = Mathf.Abs(pA.x);
        float absBx = Mathf.Abs(pB.x);

        float linearX = Mathf.Lerp(absAx, absBx, t);
        float deltaX = Mathf.Abs(absAx - absBx);
        float bulgeOffset = 4f * (boundaryArcBulge * deltaX) * t * (1f - t);

        float mag = linearX + bulgeOffset;
        return (pA.x < 0f || pB.x < 0f) ? -mag : mag;
    }

    public float GetOuterArcedBoundaryX(Vector3 pA, Vector3 pB, float z)
    {
        float dz = pB.z - pA.z;
        if (Mathf.Abs(dz) < 0.0001f) return Mathf.Abs(pA.x);

        float t = Mathf.Clamp01((z - pA.z) / dz);
        float absAx = Mathf.Abs(pA.x);
        float absBx = Mathf.Abs(pB.x);

        float linearX = Mathf.Lerp(absAx, absBx, t);
        float deltaX = Mathf.Abs(absAx - absBx);
        float bulgeOffset = 4f * (boundaryArcBulge * deltaX) * t * (1f - t);

        // Outer arc is bounded by the max of adjacent points to form a smooth outer decay envelope
        float maxAllowed = Mathf.Max(absAx, absBx);
        float mag = Mathf.Min(maxAllowed, linearX + bulgeOffset * 1.5f);
        return (pA.x < 0f || pB.x < 0f) ? -mag : mag;
    }

    public void DrawDottedArcBoundary(Vector3 pA, Vector3 pB, int steps = 15)
    {
#if UNITY_EDITOR
        Vector3 prevPt = new Vector3(GetArcedBoundaryX(pA, pB, pA.z), pA.y, pA.z);
        for (int k = 1; k <= steps; k++)
        {
            float t = (float)k / steps;
            float z = Mathf.Lerp(pA.z, pB.z, t);
            float minX = GetArcedBoundaryX(pA, pB, z);
            Vector3 currPt = new Vector3(minX, pA.y, z);
            Handles.DrawDottedLine(prevPt, currPt, 3f);
            prevPt = currPt;
        }
#endif
    }

    public void DrawDottedOuterArcBoundary(Vector3 pA, Vector3 pB, int steps = 15)
    {
#if UNITY_EDITOR
        Vector3 prevPt = new Vector3(GetOuterArcedBoundaryX(pA, pB, pA.z), pA.y, pA.z);
        for (int k = 1; k <= steps; k++)
        {
            float t = (float)k / steps;
            float z = Mathf.Lerp(pA.z, pB.z, t);
            float maxX = GetOuterArcedBoundaryX(pA, pB, z);
            Vector3 currPt = new Vector3(maxX, pA.y, z);
            Handles.DrawDottedLine(prevPt, currPt, 3f);
            prevPt = currPt;
        }
#endif
    }

    private void OnDrawGizmos()
    {
        if (sheerPlan == null) return;
        UpdateCachedCurves();

        // Draw High-Resolution Max Breadth Spline (Vibrant Gold)
        Color splineColor = new Color(0.95f, 0.75f, 0.1f, 0.95f);
        DrawInterpolatingCurveGizmo(nurbsBreadthWaypoints, splineColor, stepsPerSegment);
        DrawInterpolatingCurveGizmo(MirrorPolygon(nurbsBreadthWaypoints), new Color(0.95f, 0.35f, 0.1f, 0.95f), stepsPerSegment);

        // Draw Waypoint Spheres DIRECTLY ON THE CURVE
        for (int i = 0; i < nurbsBreadthWaypoints.Length; i++)
        {
            Gizmos.color = (i == 3) ? Color.yellow : Color.cyan;
            Gizmos.DrawSphere(nurbsBreadthWaypoints[i], 0.14f);
            Gizmos.DrawSphere(MirrorX(nurbsBreadthWaypoints[i]), 0.14f);
        }

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.yellow;
            labelStyle.alignment = TextAnchor.LowerCenter;
            labelStyle.fontSize = 10;
            labelStyle.fontStyle = FontStyle.Bold;

            for (int i = 0; i < nurbsBreadthWaypoints.Length; i++)
            {
                Handles.Label(nurbsBreadthWaypoints[i] + new Vector3(0, 0.15f, 0), $"W{i} ({nurbsBreadthWaypoints[i].x:F2}, {nurbsBreadthWaypoints[i].z:F2})", labelStyle);
            }

            // Render Dotted Arced Boundary Constraint Fairing Corridor in Scene View
            Handles.color = new Color(0.2f, 0.9f, 1f, 0.6f); // Cyan Inner Arc
            DrawDottedArcBoundary(nurbsBreadthWaypoints[3], nurbsBreadthWaypoints[5]); // W3 -> W5 (Inner Arc for W4)
            DrawDottedArcBoundary(nurbsBreadthWaypoints[4], nurbsBreadthWaypoints[6]); // W4 -> W6 (Inner Arc for W5)
            DrawDottedArcBoundary(nurbsBreadthWaypoints[5], nurbsBreadthWaypoints[7]); // W5 -> W7 (Inner Arc for W6)

            DrawDottedArcBoundary(nurbsBreadthWaypoints[3], nurbsBreadthWaypoints[1]); // W3 -> W1 (Inner Arc for W2)
            DrawDottedArcBoundary(nurbsBreadthWaypoints[2], nurbsBreadthWaypoints[0]); // W2 -> W0 (Inner Arc for W1)

            DrawDottedArcBoundary(MirrorX(nurbsBreadthWaypoints[3]), MirrorX(nurbsBreadthWaypoints[5]));
            DrawDottedArcBoundary(MirrorX(nurbsBreadthWaypoints[4]), MirrorX(nurbsBreadthWaypoints[6]));
            DrawDottedArcBoundary(MirrorX(nurbsBreadthWaypoints[5]), MirrorX(nurbsBreadthWaypoints[7]));
            DrawDottedArcBoundary(MirrorX(nurbsBreadthWaypoints[3]), MirrorX(nurbsBreadthWaypoints[1]));
            DrawDottedArcBoundary(MirrorX(nurbsBreadthWaypoints[2]), MirrorX(nurbsBreadthWaypoints[0]));

            Handles.color = new Color(1f, 0.65f, 0.1f, 0.7f); // Orange Outer Arc Envelope
            DrawDottedOuterArcBoundary(nurbsBreadthWaypoints[3], nurbsBreadthWaypoints[5]); // Outer Arc for W4
            DrawDottedOuterArcBoundary(nurbsBreadthWaypoints[4], nurbsBreadthWaypoints[6]); // Outer Arc for W5
            DrawDottedOuterArcBoundary(nurbsBreadthWaypoints[5], nurbsBreadthWaypoints[7]); // Outer Arc for W6

            DrawDottedOuterArcBoundary(nurbsBreadthWaypoints[3], nurbsBreadthWaypoints[1]); // Outer Arc for W2
            DrawDottedOuterArcBoundary(nurbsBreadthWaypoints[2], nurbsBreadthWaypoints[0]); // Outer Arc for W1

            DrawDottedOuterArcBoundary(MirrorX(nurbsBreadthWaypoints[3]), MirrorX(nurbsBreadthWaypoints[5]));
            DrawDottedOuterArcBoundary(MirrorX(nurbsBreadthWaypoints[4]), MirrorX(nurbsBreadthWaypoints[6]));
            DrawDottedOuterArcBoundary(MirrorX(nurbsBreadthWaypoints[5]), MirrorX(nurbsBreadthWaypoints[7]));
            DrawDottedOuterArcBoundary(MirrorX(nurbsBreadthWaypoints[3]), MirrorX(nurbsBreadthWaypoints[1]));
            DrawDottedOuterArcBoundary(MirrorX(nurbsBreadthWaypoints[2]), MirrorX(nurbsBreadthWaypoints[0]));

            // Render Outer Bounding Rectangle around whole Max Breadth Plan
            int lastIdx = nurbsBreadthWaypoints.Length - 1;
            float bowZ = nurbsBreadthWaypoints[lastIdx].z;
            float sternZ = nurbsBreadthWaypoints[0].z;
            float maxB = nurbsBreadthWaypoints[3].x;
            float yHeight = nurbsBreadthWaypoints[3].y;

            Vector3 bStbdBow = new Vector3(maxB, yHeight, bowZ);
            Vector3 bPortBow = new Vector3(-maxB, yHeight, bowZ);
            Vector3 bPortStern = new Vector3(-maxB, yHeight, sternZ);
            Vector3 bStbdStern = new Vector3(maxB, yHeight, sternZ);

            Handles.color = new Color(1f, 0.6f, 0.1f, 0.7f); // Vibrant Orange Outer Box
            Handles.DrawDottedLine(bStbdBow, bPortBow, 4f);
            Handles.DrawDottedLine(bPortBow, bPortStern, 4f);
            Handles.DrawDottedLine(bPortStern, bStbdStern, 4f);
            Handles.DrawDottedLine(bStbdStern, bStbdBow, 4f);
        }
#endif
    }

    private Vector3[] MirrorPolygon(Vector3[] pts)
    {
        Vector3[] mirrored = new Vector3[pts.Length];
        for (int i = 0; i < pts.Length; i++) mirrored[i] = MirrorX(pts[i]);
        return mirrored;
    }

    private void DrawInterpolatingCurveGizmo(Vector3[] waypoints, Color color, int stepsPerSeg = 35)
    {
        Gizmos.color = color;
        int n = waypoints.Length;
        if (n < 2) return;

        for (int seg = 0; seg < n - 1; seg++)
        {
            float startZ = waypoints[seg].z;
            float endZ = waypoints[seg + 1].z;

            Vector3 prevPt = EvaluateInterpolatingSpline(waypoints, startZ);
            for (int k = 1; k <= stepsPerSeg; k++)
            {
                float t = (float)k / stepsPerSeg;
                float z = Mathf.Lerp(startZ, endZ, t);
                Vector3 currPt = EvaluateInterpolatingSpline(waypoints, z);
                Gizmos.DrawLine(prevPt, currPt);
                prevPt = currPt;
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BreadthPlanNurbs))]
public class BreadthPlanNurbsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BreadthPlanNurbs plan = (BreadthPlanNurbs)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Reset Waypoints to Default Hull", GUILayout.Height(30)))
        {
            Undo.RecordObject(plan, "Reset NURBS Waypoints");
            plan.InitializeWaypointsFromShip();
            plan.UpdateCachedCurves();
            EditorUtility.SetDirty(plan);
        }
    }

    private void OnSceneGUI()
    {
        BreadthPlanNurbs plan = (BreadthPlanNurbs)target;
        if (plan == null) return;

        if (plan.sheerPlan == null) plan.sheerPlan = plan.GetComponentInParent<SheerPlan>();
        if (plan.sheerPlan == null) plan.sheerPlan = Object.FindFirstObjectByType<SheerPlan>();
        if (plan.bodyPlan == null) plan.bodyPlan = plan.GetComponentInParent<BodyPlan>();
        if (plan.bodyPlan == null) plan.bodyPlan = Object.FindFirstObjectByType<BodyPlan>();

        if (plan.sheerPlan == null || !plan.enableInteractiveNodeHandles) return;

        plan.UpdateCachedCurves();

        Vector3[] wpts = plan.nurbsBreadthWaypoints;
        if (wpts == null || wpts.Length < 8) return;

        float hw = (plan.bodyPlan != null) ? (plan.bodyPlan.keelWidth / 2f) : 0.1f;
        float maxBeamWidth = wpts[3].x;

        // Render interactive scene handles DIRECTLY ON THE CURVE WAYPOINTS (W0..W7)
        for (int i = 0; i < wpts.Length; i++)
        {
            Handles.color = (i == 3) ? Color.yellow : (i == 6 ? new Color(0.2f, 1f, 0.4f) : Color.cyan);
            EditorGUI.BeginChangeCheck();
            Vector3 newW = Handles.FreeMoveHandle(wpts[i], 0.16f, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(new Object[] { plan, plan.sheerPlan }, $"Drag Spline Waypoint W{i}");

                // 1. Enforce Z-ordering: W0.z < W1.z < W2.z < W3.z(0) < W4.z < W5.z < W6.z < W7.z
                float minZ = (i > 0) ? wpts[i - 1].z + 0.01f : -100f;
                float maxZ = (i < wpts.Length - 1) ? wpts[i + 1].z - 0.01f : 100f;

                if (i == 3) // Midship W3: Z is locked to 0
                {
                    newW.z = 0f;
                }
                else
                {
                    newW.z = Mathf.Clamp(newW.z, minZ, maxZ);
                }

                // 2. Enforce Dual-Arc Fairing Corridor (Inner & Outer Arced Boundaries)
                float minX = hw; // Default baseline minimum (Keel half-width)
                float maxX = (i == 3) ? 100f : maxBeamWidth;

                if (i == 4) // W4: Bounded inside fairing corridor between W3 and W5
                {
                    minX = plan.GetArcedBoundaryX(wpts[3], wpts[5], newW.z);
                    maxX = plan.GetOuterArcedBoundaryX(wpts[3], wpts[5], newW.z);
                }
                else if (i == 5) // W5: Bounded inside fairing corridor between W4 and W6
                {
                    minX = plan.GetArcedBoundaryX(wpts[4], wpts[6], newW.z);
                    maxX = plan.GetOuterArcedBoundaryX(wpts[4], wpts[6], newW.z);
                }
                else if (i == 6) // W6 (Cutwater): Bounded inside fairing corridor between W5 and W7
                {
                    minX = plan.GetArcedBoundaryX(wpts[5], wpts[7], newW.z);
                    maxX = plan.GetOuterArcedBoundaryX(wpts[5], wpts[7], newW.z);
                }
                else if (i == 2) // W2 (Aft): Bounded inside fairing corridor between W3 and W1
                {
                    minX = plan.GetArcedBoundaryX(wpts[3], wpts[1], newW.z);
                    maxX = plan.GetOuterArcedBoundaryX(wpts[3], wpts[1], newW.z);
                }
                else if (i == 1) // W1 (Aft): Bounded inside fairing corridor between W2 and W0
                {
                    minX = plan.GetArcedBoundaryX(wpts[2], wpts[0], newW.z);
                    maxX = plan.GetOuterArcedBoundaryX(wpts[2], wpts[0], newW.z);
                }

                newW.x = Mathf.Clamp(Mathf.Abs(newW.x), minX, maxX);
                newW.y = wpts[i].y; // Maintain baseline Y

                // Directly mutate pure waypoint position!
                wpts[i] = newW;

                plan.UpdateCachedCurves();
                if (plan.sheerPlan != null) plan.sheerPlan.UpdateCachedValues();
                EditorUtility.SetDirty(plan);
                if (plan.sheerPlan != null) EditorUtility.SetDirty(plan.sheerPlan);
            }
        }
    }
}
#endif
