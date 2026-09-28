using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Practice script for converting 2D Sheer View height curves Y(Z) (from MainSheerKeelLineTransitionPractice)
/// and 2D Plan View breadth shapes X(Z) (from BreadthShapeTransitionPractice) into 3D Strake Ribbands P(Z) = (X(Z), Y(Z), Z).
/// 
/// Operates in 3D ship space for both Starboard (+X) and Port (-X) sides.
/// </summary>
[ExecuteInEditMode]
public class StrakesRibbands3DPractice : MonoBehaviour
{
    public enum TransitionMode
    {
        Lower_KeelToRising,
        Upper_RisingToSheer,
        Both_FullStack
    }

    [Header("Component References")]
    [Tooltip("Reference to MainSheerKeelLineTransitionPractice component.")]
    public MainSheerKeelLineTransitionPractice sheerTransitionPractice;

    [Tooltip("Reference to BreadthShapeTransitionPractice component.")]
    public BreadthShapeTransitionPractice breadthTransitionPractice;

    [Header("Transition Settings")]
    [Tooltip("Which 3D ribband region to evaluate and display.")]
    public TransitionMode transitionMode = TransitionMode.Both_FullStack;

    [Tooltip("Longitudinal resolution along each 3D ribband from Stern to Bow.")]
    [Range(10, 300)]
    public int resolution = 100;

    [Header("Visualization Settings")]
    [Tooltip("Display 3D ribband space curves in Scene View.")]
    public bool showRibbands = true;

    [Tooltip("Show visualization on Starboard (+X) side.")]
    public bool showStarboard = true;

    [Tooltip("Show visualization on Port (-X) side.")]
    public bool showPort = true;

    [Tooltip("Show sphere markers at the Bow and Stern endpoints of each 3D ribband.")]
    public bool showEndpoints = true;

    [Tooltip("Radius of Gizmo spheres drawn at ribband endpoints.")]
    public float endpointGizmoRadius = 0.10f;

    [Tooltip("Color of Keel 3D ribband (bottommost baseline).")]
    public Color keelColor = new Color(0.2f, 0.8f, 1f, 0.9f);

    [Tooltip("Color of Lower Region intermediate 3D ribbands.")]
    public Color lowerRibbandColor = new Color(0.3f, 0.9f, 0.6f, 0.95f);

    [Tooltip("Color of Rising Line / Floor Line 3D ribband (middle boundary).")]
    public Color middleBoundaryColor = new Color(1f, 0.85f, 0.2f, 0.9f);

    [Tooltip("Color of Upper Region intermediate 3D ribbands.")]
    public Color upperRibbandColor = new Color(0.95f, 0.55f, 0.15f, 0.95f);

    [Tooltip("Color of Main Sheer / Max Breadth 3D ribband (topmost boundary).")]
    public Color sheerColor = Color.yellow;

    private List<List<Vector3>> m_CachedStbdLowerRibbands = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortLowerRibbands = new List<List<Vector3>>();

    private List<List<Vector3>> m_CachedStbdUpperRibbands = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortUpperRibbands = new List<List<Vector3>>();

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
        if (sheerTransitionPractice == null) sheerTransitionPractice = FindFirstObjectByType<MainSheerKeelLineTransitionPractice>();
        if (breadthTransitionPractice == null) breadthTransitionPractice = FindFirstObjectByType<BreadthShapeTransitionPractice>();
    }

    /// <summary>
    /// Evaluates 3D Ribbands for the Lower Region (Keel Line -> Rising Line / Floor Breadth).
    /// Midship X width matches the exact Body Plan Mould curve point X at Midships.
    /// </summary>
    public List<List<Vector3>> GetLowerRibbands(float sideSign)
    {
        List<List<Vector3>> ribbands = new List<List<Vector3>>();
        if (sheerTransitionPractice == null || breadthTransitionPractice == null) return ribbands;

        BreadthPlan breadthPlan = breadthTransitionPractice.breadthPlan;
        if (breadthPlan == null) return ribbands;

        breadthPlan.UpdateCachedCurves();
        if (breadthPlan.sheerPlan != null) breadthPlan.sheerPlan.UpdateCachedValues();

        BodyPlan bodyPlan = (breadthPlan != null) ? breadthPlan.bodyPlan : null;
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();

        List<float> vProportions = sheerTransitionPractice.GetLowerVProportions();
        int halfRes = Mathf.Max(5, resolution / 2);
        float hw = breadthTransitionPractice.GetEffectiveKeelHalfWidth();

        float zFloorStern = breadthPlan.cachedFloorSternPoint.z;
        float zFloorBow = breadthPlan.cachedFloorBowPoint.z;

        float midKeelY = sheerTransitionPractice.GetPureKeelYAtZ(0f);
        float midRisingY = sheerTransitionPractice.GetExtendedRisingLineYAtZ(0f);
        float xFloorMid = breadthPlan.GetFloorLineXAtZ(0f);

        foreach (float v in vProportions)
        {
            float zSternEnd = sheerTransitionPractice.GetSternIntersectionZForV(v);
            float zBowEnd = sheerTransitionPractice.GetStemIntersectionZForV(v);

            float yMid = Mathf.Lerp(midKeelY, midRisingY, v);
            float xMouldMid = (bodyPlan != null) ? bodyPlan.GetLowerMouldXAtY(yMid) : Mathf.Lerp(hw, xFloorMid, v);

            List<Vector3> pts = new List<Vector3>();

            // 1. Aft Section (Stern End zSternEnd -> Midships Z=0)
            for (int i = 0; i <= halfRes; i++)
            {
                float s = 1f - (float)i / halfRes; // s: 1.0 (stern tip) -> 0.0 (midships)
                float z = s * zSternEnd;

                float keelY = sheerTransitionPractice.GetPureKeelYAtZ(z);
                float risingY = sheerTransitionPractice.GetExtendedRisingLineYAtZ(z);
                float y = Mathf.Lerp(keelY, risingY, v);

                float zFloorSample = s * zFloorStern;
                float xFloor = breadthPlan.GetFloorLineXAtZ(zFloorSample);

                float shapeRatio = (Mathf.Abs(xFloorMid - hw) > 0.0001f) ? Mathf.Clamp01((xFloor - hw) / (xFloorMid - hw)) : (1f - s);
                float x = hw + (xMouldMid - hw) * shapeRatio;

                pts.Add(new Vector3(sideSign * x, y, z));
            }

            // 2. Forward Section (Midships Z=0 -> Bow End zBowEnd)
            for (int i = 1; i <= halfRes; i++)
            {
                float s = (float)i / halfRes; // s: 0.0 (midships) -> 1.0 (bow tip)
                float z = s * zBowEnd;

                float keelY = sheerTransitionPractice.GetPureKeelYAtZ(z);
                float risingY = sheerTransitionPractice.GetExtendedRisingLineYAtZ(z);
                float y = Mathf.Lerp(keelY, risingY, v);

                float zFloorSample = s * zFloorBow;
                float xFloor = breadthPlan.GetFloorLineXAtZ(zFloorSample);

                float shapeRatio = (Mathf.Abs(xFloorMid - hw) > 0.0001f) ? Mathf.Clamp01((xFloor - hw) / (xFloorMid - hw)) : (1f - s);
                float x = hw + (xMouldMid - hw) * shapeRatio;

                pts.Add(new Vector3(sideSign * x, y, z));
            }

            ribbands.Add(pts);
        }

        return ribbands;
    }

    /// <summary>
    /// Evaluates 3D Ribbands for the Upper Region (Rising Line / Floor Breadth -> Main Sheer Line / Max Breadth).
    /// Midship X width matches the exact Body Plan Mould curve point X at Midships.
    /// </summary>
    public List<List<Vector3>> GetUpperRibbands(float sideSign)
    {
        List<List<Vector3>> ribbands = new List<List<Vector3>>();
        if (sheerTransitionPractice == null || breadthTransitionPractice == null) return ribbands;

        BreadthPlan breadthPlan = breadthTransitionPractice.breadthPlan;
        if (breadthPlan == null) return ribbands;

        breadthPlan.UpdateCachedCurves();
        if (breadthPlan.sheerPlan != null) breadthPlan.sheerPlan.UpdateCachedValues();

        BodyPlan bodyPlan = (breadthPlan != null) ? breadthPlan.bodyPlan : null;
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();

        List<float> uProportions = sheerTransitionPractice.GetUpperUProportions();
        int halfRes = Mathf.Max(5, resolution / 2);

        float zFloorStern = breadthPlan.cachedFloorSternPoint.z;
        float zFloorBow = breadthPlan.cachedFloorBowPoint.z;

        float zHobStern = (breadthPlan.sheerPlan != null) ? breadthPlan.sheerPlan.cachedHobSternIntersect.z : (breadthPlan.cachedIsAftCubic ? breadthPlan.cachedAftP3.z : breadthPlan.cachedAftP2.z);
        float zHobBow = (breadthPlan.sheerPlan != null) ? breadthPlan.sheerPlan.cachedHobBowIntersect.z : breadthPlan.cachedFwdP0.z;

        float midRisingY = sheerTransitionPractice.GetExtendedRisingLineYAtZ(0f);
        float midSheerY = sheerTransitionPractice.GetPureMainSheerYAtZ(0f);

        float xFloorMid = breadthPlan.GetFloorLineXAtZ(0f);
        float xHobMid = breadthPlan.GetTheoreticalBreadthXAtZ(0f);

        float hobY = (bodyPlan != null) ? bodyPlan.EvaluateLowerMouldAtT(1.0f).y : 1.5f;

        foreach (float u in uProportions)
        {
            float zSternEnd = sheerTransitionPractice.GetSternIntersectionZForU(u);
            float zBowEnd = sheerTransitionPractice.GetStemIntersectionZForU(u);

            float yMid = Mathf.Lerp(midRisingY, midSheerY, u);
            float xMouldMid = (bodyPlan != null) ? ((yMid <= hobY + 0.001f) ? bodyPlan.GetLowerMouldXAtY(yMid) : bodyPlan.GetUpperMouldXAtY(yMid)) : Mathf.Lerp(xFloorMid, xHobMid, u);

            float uEff = (Mathf.Abs(xHobMid - xFloorMid) > 0.0001f) ? Mathf.Clamp01((xMouldMid - xFloorMid) / (xHobMid - xFloorMid)) : u;

            List<Vector3> pts = new List<Vector3>();

            // 1. Aft Section (Stern End zSternEnd -> Midships Z=0)
            for (int i = 0; i <= halfRes; i++)
            {
                float s = 1f - (float)i / halfRes; // s: 1.0 (stern tip) -> 0.0 (midships)
                float z = s * zSternEnd;

                float risingY = sheerTransitionPractice.GetExtendedRisingLineYAtZ(z);
                float sheerY = sheerTransitionPractice.GetPureMainSheerYAtZ(z);
                float y = Mathf.Lerp(risingY, sheerY, u);

                float zFloorSample = s * zFloorStern;
                float zHobSample = s * zHobStern;
                float xFloor = breadthPlan.GetFloorLineXAtZ(zFloorSample);
                float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(zHobSample);
                float x = Mathf.Lerp(xFloor, xHob, uEff);

                pts.Add(new Vector3(sideSign * x, y, z));
            }

            // 2. Forward Section (Midships Z=0 -> Bow End zBowEnd)
            for (int i = 1; i <= halfRes; i++)
            {
                float s = (float)i / halfRes; // s: 0.0 (midships) -> 1.0 (bow tip)
                float z = s * zBowEnd;

                float risingY = sheerTransitionPractice.GetExtendedRisingLineYAtZ(z);
                float sheerY = sheerTransitionPractice.GetPureMainSheerYAtZ(z);
                float y = Mathf.Lerp(risingY, sheerY, u);

                float zFloorSample = s * zFloorBow;
                float zHobSample = s * zHobBow;
                float xFloor = breadthPlan.GetFloorLineXAtZ(zFloorSample);
                float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(zHobSample);
                float x = Mathf.Lerp(xFloor, xHob, uEff);

                pts.Add(new Vector3(sideSign * x, y, z));
            }

            ribbands.Add(pts);
        }

        return ribbands;
    }

    [ContextMenu("Rebuild Cache")]
    public void ForceRebuildCache()
    {
        MarkDirty();
    }

    private void RebuildCache()
    {
        FindReferences();
        if (sheerTransitionPractice == null || breadthTransitionPractice == null) return;

        m_CachedStbdLowerRibbands = GetLowerRibbands(1f);
        m_CachedPortLowerRibbands = GetLowerRibbands(-1f);

        m_CachedStbdUpperRibbands = GetUpperRibbands(1f);
        m_CachedPortUpperRibbands = GetUpperRibbands(-1f);

        m_IsCacheDirty = false;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (!showRibbands || sheerTransitionPractice == null || breadthTransitionPractice == null) return;

        if (m_IsCacheDirty)
        {
            RebuildCache();
        }

        bool drawLower = (transitionMode == TransitionMode.Lower_KeelToRising || transitionMode == TransitionMode.Both_FullStack);
        bool drawUpper = (transitionMode == TransitionMode.Upper_RisingToSheer || transitionMode == TransitionMode.Both_FullStack);

        if (drawLower)
        {
            if (showStarboard) DrawLowerRibbandStack(m_CachedStbdLowerRibbands);
            if (showPort) DrawLowerRibbandStack(m_CachedPortLowerRibbands);
        }

        if (drawUpper)
        {
            if (showStarboard) DrawUpperRibbandStack(m_CachedStbdUpperRibbands);
            if (showPort) DrawUpperRibbandStack(m_CachedPortUpperRibbands);
        }
    }

    private void DrawLowerRibbandStack(List<List<Vector3>> ribbandList)
    {
        if (ribbandList == null) return;
        int count = ribbandList.Count;

        for (int k = 0; k < count; k++)
        {
            float v = (count > 1) ? (float)k / (count - 1) : 0f;
            Color c = Color.Lerp(keelColor, middleBoundaryColor, v);

            if (k == 0) c = keelColor;
            else if (k == count - 1) c = middleBoundaryColor;

            DrawRibband(ribbandList[k], c);
        }
    }

    private void DrawUpperRibbandStack(List<List<Vector3>> ribbandList)
    {
        if (ribbandList == null) return;
        int count = ribbandList.Count;

        for (int k = 0; k < count; k++)
        {
            float u = (count > 1) ? (float)k / (count - 1) : 0f;
            Color c = Color.Lerp(middleBoundaryColor, sheerColor, u);

            if (k == 0) c = middleBoundaryColor;
            else if (k == count - 1) c = sheerColor;

            DrawRibband(ribbandList[k], c);
        }
    }

    private void DrawRibband(List<Vector3> pts, Color color)
    {
        if (pts == null || pts.Count < 2) return;
        Gizmos.color = color;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Gizmos.DrawLine(pts[i], pts[i + 1]);
        }

        if (showEndpoints)
        {
            Gizmos.DrawSphere(pts[0], endpointGizmoRadius); // Stern tip
            Gizmos.DrawSphere(pts[pts.Count - 1], endpointGizmoRadius); // Bow tip
        }
    }
}
