using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Practice script for generating Plan View breadth transition shapes using Normalized Shape Morphing.
/// 
/// Modes:
/// 1. Lower Region: Keel Line -> Floor Breadth Shape
/// 2. Upper Region: Floor Breadth Shape -> Maximum Breadth Shape
/// 3. Both Regions: Full Plan View Transition Stack
/// 
/// All shapes lie flat in 2D Plan View (Y = 0).
/// Each shape curve is normalized (s in [0, 1]) and scaled longitudinally between its own bow/stern endpoints,
/// ensuring every shape always reaches the centerline/keel baseline at its bow and stern tips.
/// </summary>
[ExecuteInEditMode]
public class BreadthShapeTransitionPractice : MonoBehaviour
{
    public enum TransitionMode
    {
        Lower_KeelToFloor,
        Upper_FloorToMax,
        Both_FullStack
    }

    [Header("Component References")]
    [Tooltip("Reference to BreadthPlan component.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to MainSheerKeelLineTransitionPractice component.")]
    public MainSheerKeelLineTransitionPractice sheerTransitionPractice;

    [Header("Transition Settings")]
    [Tooltip("Which transition region to evaluate and display.")]
    public TransitionMode transitionMode = TransitionMode.Both_FullStack;

    [Tooltip("Number of transition shapes in Lower Region (Keel -> Floor Breadth).")]
    [Range(2, 50)]
    public int lowerShapeCount = 6;

    [Tooltip("Number of transition shapes in Upper Region (Floor Breadth -> Max Breadth).")]
    [Range(2, 50)]
    public int upperShapeCount = 6;

    [Tooltip("Keel Half-Width baseline (P/S distance from centerline).")]
    public float keelHalfWidth = 0.25f;

    [Tooltip("Longitudinal resolution along each shape from Stern to Bow.")]
    [Range(10, 300)]
    public int resolution = 100;

    [Header("Visualization Settings")]
    [Tooltip("Display transition shapes in Scene View.")]
    public bool showShapes = true;

    [Tooltip("Show visualization on Starboard side.")]
    public bool showStarboard = true;

    [Tooltip("Show visualization on Port side.")]
    public bool showPort = true;

    [Tooltip("Color of Keel shape (bottommost baseline).")]
    public Color keelColor = new Color(0.2f, 0.8f, 1f, 0.9f);

    [Tooltip("Color of Lower Region intermediate shapes.")]
    public Color lowerShapeColor = new Color(0.3f, 0.9f, 0.6f, 0.95f);

    [Tooltip("Color of Floor Line shape (middle boundary).")]
    public Color floorLineColor = new Color(1f, 0.85f, 0.2f, 0.9f);

    [Tooltip("Color of Upper Region intermediate shapes.")]
    public Color upperShapeColor = new Color(0.95f, 0.55f, 0.15f, 0.95f);

    [Tooltip("Color of Maximum Breadth shape (topmost boundary).")]
    public Color maxBreadthColor = Color.yellow;

    [Header("Master Frame Point Sampling Settings")]
    [Tooltip("Target arc length interval for placing points on Body Plan Lower Mould (default 0.25).")]
    public float pointInterval = 0.25f;

    [Tooltip("Display points on the Midship X axis derived from Body Plan Lower Mould arc intervals.")]
    public bool showMidshipXPoints = true;

    [Tooltip("Radius of Gizmo spheres drawn for midship X points.")]
    public float pointGizmoRadius = 0.12f;

    [Tooltip("Color of midship X points.")]
    public Color pointColor = Color.magenta;

    [Header("Stem Intersection Point Settings")]
    [Tooltip("Display points on the Z-axis (Bow) based on Stem intersections from MainSheerKeelLineTransitionPractice.")]
    public bool showStemIntersectionPoints = true;

    [Tooltip("Color of Stem intersection points on the Z-axis.")]
    public Color stemPointColor = Color.cyan;

    [Tooltip("Radius of Gizmo spheres drawn for Stem intersection Z points.")]
    public float stemPointGizmoRadius = 0.12f;

    [Header("Stern Intersection Point Settings")]
    [Tooltip("Display points on the Z-axis (Stern) based on Stern profile intersections from MainSheerKeelLineTransitionPractice.")]
    public bool showSternIntersectionPoints = true;

    [Tooltip("Color of Stern intersection points on the Z-axis.")]
    public Color sternPointColor = Color.cyan;

    [Tooltip("Radius of Gizmo spheres drawn for Stern intersection Z points.")]
    public float sternPointGizmoRadius = 0.12f;

    private List<float> m_CachedMidshipXPoints = new List<float>();
    private List<float> m_CachedStemIntersectionZPoints = new List<float>();
    private List<float> m_CachedSternIntersectionZPoints = new List<float>();

    private List<List<Vector3>> m_CachedStbdLowerShapes = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortLowerShapes = new List<List<Vector3>>();

    private List<List<Vector3>> m_CachedStbdUpperShapes = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortUpperShapes = new List<List<Vector3>>();

    private bool m_IsCacheDirty = true;

    private void Awake()
    {
        FindReferences();
        MarkDirty();
    }

    private void OnValidate()
    {
        FindReferences();
        MarkDirty();
    }

    public void MarkDirty()
    {
        m_IsCacheDirty = true;
    }

    public void FindReferences()
    {
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
        if (sheerTransitionPractice == null) sheerTransitionPractice = FindFirstObjectByType<MainSheerKeelLineTransitionPractice>();
    }

    public float GetEffectiveKeelHalfWidth()
    {
        if (breadthPlan != null && breadthPlan.bodyPlan != null)
        {
            return Mathf.Max(0.001f, breadthPlan.bodyPlan.keelWidth / 2f);
        }
        return Mathf.Max(0.001f, keelHalfWidth);
    }

    /// <summary>
    /// Evaluates the X positions of points placed at equal arc-length intervals (default 0.25)
    /// along the Body Plan's Lower Mould curve.
    /// </summary>
    public List<float> GetBodyPlanLowerMouldXPoints()
    {
        List<float> xPositions = new List<float>();

        BodyPlan bodyPlan = (breadthPlan != null) ? breadthPlan.bodyPlan : null;
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (bodyPlan == null) return xPositions;

        bodyPlan.UpdateCachedCurves();

        const int samples = 200;
        List<Vector3> polyline = new List<Vector3>();
        List<float> cumDist = new List<float> { 0f };

        Vector3 prevPt = bodyPlan.EvaluateLowerMouldAtT(0f);
        polyline.Add(prevPt);

        float totalArcLen = 0f;
        for (int i = 1; i <= samples; i++)
        {
            float t = (float)i / samples;
            Vector3 currPt = bodyPlan.EvaluateLowerMouldAtT(t);
            totalArcLen += Vector3.Distance(prevPt, currPt);
            polyline.Add(currPt);
            cumDist.Add(totalArcLen);
            prevPt = currPt;
        }

        if (totalArcLen <= 0.001f) return xPositions;

        float interval = Mathf.Max(0.01f, pointInterval);
        int numSegments = Mathf.Max(1, Mathf.RoundToInt(totalArcLen / interval));

        for (int j = 0; j <= numSegments; j++)
        {
            float targetDist = totalArcLen * ((float)j / numSegments);
            float x = GetXAtArcDistance(polyline, cumDist, targetDist);
            xPositions.Add(x);
        }

        return xPositions;
    }

    private float GetXAtArcDistance(List<Vector3> polyline, List<float> cumDist, float targetDist)
    {
        int count = cumDist.Count;
        if (targetDist <= 0f) return polyline[0].x;
        if (targetDist >= cumDist[count - 1]) return polyline[count - 1].x;

        for (int i = 0; i < count - 1; i++)
        {
            if (targetDist >= cumDist[i] && targetDist <= cumDist[i + 1])
            {
                float segmentLen = cumDist[i + 1] - cumDist[i];
                float lerpT = (segmentLen > 0.0001f) ? ((targetDist - cumDist[i]) / segmentLen) : 0f;
                return Mathf.Lerp(polyline[i].x, polyline[i + 1].x, lerpT);
            }
        }

        return polyline[count - 1].x;
    }

    /// <summary>
    /// Evaluates Lower Region shapes: Keel Line (v = 0) -> Floor Breadth Shape (v = 1.0).
    /// Boundaries (Stem/Stern tips) are defined by the intersections of MainSheerKeelLineTransitionPractice lines with the Stem/Stern profiles.
    /// Midship X width matches the exact Body Plan Mould curve point X at Midships.
    /// </summary>
    public List<List<Vector3>> GetLowerTransitionShapes(float sideSign)
    {
        List<List<Vector3>> shapes = new List<List<Vector3>>();
        if (breadthPlan == null) return shapes;

        breadthPlan.UpdateCachedCurves();
        if (breadthPlan.sheerPlan != null) breadthPlan.sheerPlan.UpdateCachedValues();
        if (sheerTransitionPractice == null) sheerTransitionPractice = FindFirstObjectByType<MainSheerKeelLineTransitionPractice>();

        BodyPlan bodyPlan = (breadthPlan != null) ? breadthPlan.bodyPlan : null;
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();

        int halfRes = Mathf.Max(5, resolution / 2);
        float hw = GetEffectiveKeelHalfWidth();

        float zKeelStern = (breadthPlan.sheerPlan != null) ? breadthPlan.sheerPlan.cachedKeelSternPoint.z : (breadthPlan.cachedIsAftCubic ? breadthPlan.cachedAftP3.z : breadthPlan.cachedAftP2.z);
        float zKeelBow = (breadthPlan.sheerPlan != null) ? breadthPlan.sheerPlan.cachedKeelBowPoint.z : breadthPlan.cachedFwdP0.z;

        float zFloorStern = breadthPlan.cachedFloorSternPoint.z;
        float zFloorBow = breadthPlan.cachedFloorBowPoint.z;

        float midKeelY = (sheerTransitionPractice != null) ? sheerTransitionPractice.GetPureKeelYAtZ(0f) : 0f;
        float midRisingY = (sheerTransitionPractice != null) ? sheerTransitionPractice.GetExtendedRisingLineYAtZ(0f) : 1f;
        float xFloorMid = breadthPlan.GetFloorLineXAtZ(0f);

        List<float> vProportions;
        if (sheerTransitionPractice != null)
        {
            vProportions = sheerTransitionPractice.GetLowerVProportions();
        }
        else
        {
            vProportions = new List<float>();
            int n = Mathf.Max(2, lowerShapeCount);
            for (int k = 0; k < n; k++)
            {
                vProportions.Add((n > 1) ? (float)k / (n - 1) : 0f);
            }
        }

        foreach (float v in vProportions)
        {
            float zSternEnd = (sheerTransitionPractice != null) ? sheerTransitionPractice.GetSternIntersectionZForV(v) : Mathf.Lerp(zKeelStern, zFloorStern, v);
            float zBowEnd = (sheerTransitionPractice != null) ? sheerTransitionPractice.GetStemIntersectionZForV(v) : Mathf.Lerp(zKeelBow, zFloorBow, v);

            float yMid = Mathf.Lerp(midKeelY, midRisingY, v);
            float xMouldMid = (bodyPlan != null) ? bodyPlan.GetLowerMouldXAtY(yMid) : Mathf.Lerp(hw, xFloorMid, v);

            List<Vector3> pts = new List<Vector3>();

            // 1. Aft Section (Stern End zSternEnd -> Midships Z=0)
            for (int i = 0; i <= halfRes; i++)
            {
                float s = 1f - (float)i / halfRes; // s: 1.0 (stern tip) -> 0.0 (midships)
                float z = s * zSternEnd;

                float zFloorSample = s * zFloorStern;
                float xFloor = breadthPlan.GetFloorLineXAtZ(zFloorSample);

                float shapeRatio = (Mathf.Abs(xFloorMid - hw) > 0.0001f) ? Mathf.Clamp01((xFloor - hw) / (xFloorMid - hw)) : (1f - s);
                float x = hw + (xMouldMid - hw) * shapeRatio;

                pts.Add(new Vector3(sideSign * x, 0f, z));
            }

            // 2. Forward Section (Midships Z=0 -> Bow End zBowEnd)
            for (int i = 1; i <= halfRes; i++)
            {
                float s = (float)i / halfRes; // s: 0.0 (midships) -> 1.0 (bow tip)
                float z = s * zBowEnd;

                float zFloorSample = s * zFloorBow;
                float xFloor = breadthPlan.GetFloorLineXAtZ(zFloorSample);

                float shapeRatio = (Mathf.Abs(xFloorMid - hw) > 0.0001f) ? Mathf.Clamp01((xFloor - hw) / (xFloorMid - hw)) : (1f - s);
                float x = hw + (xMouldMid - hw) * shapeRatio;

                pts.Add(new Vector3(sideSign * x, 0f, z));
            }

            shapes.Add(pts);
        }

        return shapes;
    }

    /// <summary>
    /// Evaluates Upper Region shapes: Floor Breadth Shape (u = 0) -> Max Breadth Shape (u = 1.0).
    /// Boundaries (Stem/Stern tips) are defined by the intersections of MainSheerKeelLineTransitionPractice lines with the Stem/Stern profiles.
    /// Midship X width matches the exact Body Plan Mould curve point X at Midships.
    /// </summary>
    public List<List<Vector3>> GetUpperTransitionShapes(float sideSign)
    {
        List<List<Vector3>> shapes = new List<List<Vector3>>();
        if (breadthPlan == null) return shapes;

        breadthPlan.UpdateCachedCurves();
        if (breadthPlan.sheerPlan != null) breadthPlan.sheerPlan.UpdateCachedValues();
        if (sheerTransitionPractice == null) sheerTransitionPractice = FindFirstObjectByType<MainSheerKeelLineTransitionPractice>();

        BodyPlan bodyPlan = (breadthPlan != null) ? breadthPlan.bodyPlan : null;
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();

        int halfRes = Mathf.Max(5, resolution / 2);

        float zFloorStern = breadthPlan.cachedFloorSternPoint.z;
        float zFloorBow = breadthPlan.cachedFloorBowPoint.z;

        float zHobStern = (breadthPlan.sheerPlan != null) ? breadthPlan.sheerPlan.cachedHobSternIntersect.z : (breadthPlan.cachedIsAftCubic ? breadthPlan.cachedAftP3.z : breadthPlan.cachedAftP2.z);
        float zHobBow = (breadthPlan.sheerPlan != null) ? breadthPlan.sheerPlan.cachedHobBowIntersect.z : breadthPlan.cachedFwdP0.z;

        float midRisingY = (sheerTransitionPractice != null) ? sheerTransitionPractice.GetExtendedRisingLineYAtZ(0f) : 1f;
        float midSheerY = (sheerTransitionPractice != null) ? sheerTransitionPractice.GetPureMainSheerYAtZ(0f) : 2f;

        float xFloorMid = breadthPlan.GetFloorLineXAtZ(0f);
        float xHobMid = breadthPlan.GetTheoreticalBreadthXAtZ(0f);

        float hobY = (bodyPlan != null) ? bodyPlan.EvaluateLowerMouldAtT(1.0f).y : 1.5f;

        List<float> uProportions;
        if (sheerTransitionPractice != null)
        {
            uProportions = sheerTransitionPractice.GetUpperUProportions();
        }
        else
        {
            uProportions = new List<float>();
            int n = Mathf.Max(2, upperShapeCount);
            for (int k = 0; k < n; k++)
            {
                uProportions.Add((n > 1) ? (float)k / (n - 1) : 0f);
            }
        }

        foreach (float u in uProportions)
        {
            float zSternEnd = (sheerTransitionPractice != null) ? sheerTransitionPractice.GetSternIntersectionZForU(u) : Mathf.Lerp(zFloorStern, zHobStern, u);
            float zBowEnd = (sheerTransitionPractice != null) ? sheerTransitionPractice.GetStemIntersectionZForU(u) : Mathf.Lerp(zFloorBow, zHobBow, u);

            float yMid = Mathf.Lerp(midRisingY, midSheerY, u);
            float xMouldMid = (bodyPlan != null) ? ((yMid <= hobY + 0.001f) ? bodyPlan.GetLowerMouldXAtY(yMid) : bodyPlan.GetUpperMouldXAtY(yMid)) : Mathf.Lerp(xFloorMid, xHobMid, u);

            float uEff = (Mathf.Abs(xHobMid - xFloorMid) > 0.0001f) ? Mathf.Clamp01((xMouldMid - xFloorMid) / (xHobMid - xFloorMid)) : u;

            List<Vector3> pts = new List<Vector3>();

            // 1. Aft Section (Stern End zSternEnd -> Midships Z=0)
            for (int i = 0; i <= halfRes; i++)
            {
                float s = 1f - (float)i / halfRes; // s: 1.0 (stern tip) -> 0.0 (midships)
                float z = s * zSternEnd;

                float zFloorSample = s * zFloorStern;
                float zHobSample = s * zHobStern;

                float xFloor = breadthPlan.GetFloorLineXAtZ(zFloorSample);
                float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(zHobSample);

                float x = Mathf.Lerp(xFloor, xHob, uEff);
                pts.Add(new Vector3(sideSign * x, 0f, z));
            }

            // 2. Forward Section (Midships Z=0 -> Bow End zBowEnd)
            for (int i = 1; i <= halfRes; i++)
            {
                float s = (float)i / halfRes; // s: 0.0 (midships) -> 1.0 (bow tip)
                float z = s * zBowEnd;

                float zFloorSample = s * zFloorBow;
                float zHobSample = s * zHobBow;

                float xFloor = breadthPlan.GetFloorLineXAtZ(zFloorSample);
                float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(zHobSample);

                float x = Mathf.Lerp(xFloor, xHob, uEff);
                pts.Add(new Vector3(sideSign * x, 0f, z));
            }

            shapes.Add(pts);
        }

        return shapes;
    }

    /// <summary>
    /// Evaluates the Z-axis intersection points of the transition lines from MainSheerKeelLineTransitionPractice with the Stem Rake curve.
    /// Returns the Z positions of those stem intersections.
    /// </summary>
    public List<float> GetStemIntersectionZPoints()
    {
        if (sheerTransitionPractice == null)
        {
            sheerTransitionPractice = FindFirstObjectByType<MainSheerKeelLineTransitionPractice>();
        }
        if (sheerTransitionPractice == null) return new List<float>();

        return sheerTransitionPractice.GetStemIntersectionZPoints();
    }

    /// <summary>
    /// Evaluates the Z-axis intersection points of the transition lines from MainSheerKeelLineTransitionPractice with the Stern profile curve.
    /// Returns the Z positions of those stern intersections.
    /// </summary>
    public List<float> GetSternIntersectionZPoints()
    {
        if (sheerTransitionPractice == null)
        {
            sheerTransitionPractice = FindFirstObjectByType<MainSheerKeelLineTransitionPractice>();
        }
        if (sheerTransitionPractice == null) return new List<float>();

        return sheerTransitionPractice.GetSternIntersectionZPoints();
    }

    [ContextMenu("Rebuild Cache")]
    public void ForceRebuildCache()
    {
        MarkDirty();
    }

    private void RebuildCache()
    {
        if (breadthPlan == null) return;

        breadthPlan.UpdateCachedCurves();

        m_CachedMidshipXPoints = GetBodyPlanLowerMouldXPoints();
        m_CachedStemIntersectionZPoints = GetStemIntersectionZPoints();
        m_CachedSternIntersectionZPoints = GetSternIntersectionZPoints();

        m_CachedStbdLowerShapes = GetLowerTransitionShapes(1f);
        m_CachedPortLowerShapes = GetLowerTransitionShapes(-1f);

        m_CachedStbdUpperShapes = GetUpperTransitionShapes(1f);
        m_CachedPortUpperShapes = GetUpperTransitionShapes(-1f);

        m_IsCacheDirty = false;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (breadthPlan == null || !showShapes) return;

        if (m_IsCacheDirty)
        {
            RebuildCache();
        }

        if (showMidshipXPoints && m_CachedMidshipXPoints != null)
        {
            Gizmos.color = pointColor;
            foreach (float x in m_CachedMidshipXPoints)
            {
                if (showStarboard) Gizmos.DrawSphere(new Vector3(x, 0f, 0f), pointGizmoRadius);
                if (showPort) Gizmos.DrawSphere(new Vector3(-x, 0f, 0f), pointGizmoRadius);
            }
        }

        if (showStemIntersectionPoints && m_CachedStemIntersectionZPoints != null)
        {
            Gizmos.color = stemPointColor;
            foreach (float z in m_CachedStemIntersectionZPoints)
            {
                Gizmos.DrawSphere(new Vector3(0f, 0f, z), stemPointGizmoRadius);
            }
        }

        if (showSternIntersectionPoints && m_CachedSternIntersectionZPoints != null)
        {
            Gizmos.color = sternPointColor;
            foreach (float z in m_CachedSternIntersectionZPoints)
            {
                Gizmos.DrawSphere(new Vector3(0f, 0f, z), sternPointGizmoRadius);
            }
        }

        bool drawLower = (transitionMode == TransitionMode.Lower_KeelToFloor || transitionMode == TransitionMode.Both_FullStack);
        bool drawUpper = (transitionMode == TransitionMode.Upper_FloorToMax || transitionMode == TransitionMode.Both_FullStack);

        if (drawLower)
        {
            if (showStarboard) DrawLowerStack(m_CachedStbdLowerShapes);
            if (showPort) DrawLowerStack(m_CachedPortLowerShapes);
        }

        if (drawUpper)
        {
            if (showStarboard) DrawUpperStack(m_CachedStbdUpperShapes);
            if (showPort) DrawUpperStack(m_CachedPortUpperShapes);
        }
    }

    private void DrawLowerStack(List<List<Vector3>> shapeList)
    {
        if (shapeList == null) return;
        int count = shapeList.Count;

        for (int k = 0; k < count; k++)
        {
            float v = (count > 1) ? (float)k / (count - 1) : 0f;
            Color c = Color.Lerp(keelColor, floorLineColor, v);

            if (k == 0) c = keelColor;
            else if (k == count - 1) c = floorLineColor;

            DrawCurve(shapeList[k], c);
        }
    }

    private void DrawUpperStack(List<List<Vector3>> shapeList)
    {
        if (shapeList == null) return;
        int count = shapeList.Count;

        for (int k = 0; k < count; k++)
        {
            float u = (count > 1) ? (float)k / (count - 1) : 0f;
            Color c = Color.Lerp(floorLineColor, maxBreadthColor, u);

            if (k == 0) c = floorLineColor;
            else if (k == count - 1) c = maxBreadthColor;

            DrawCurve(shapeList[k], c);
        }
    }

    private void DrawCurve(List<Vector3> pts, Color color)
    {
        if (pts == null || pts.Count < 2) return;
        Gizmos.color = color;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Gizmos.DrawLine(pts[i], pts[i + 1]);
        }
    }
}





