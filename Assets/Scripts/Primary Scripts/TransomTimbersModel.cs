using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Transom Timbers Model.
/// Generates horizontal/transverse transom timber beams across the stern face,
/// connecting the Port and Starboard stern landing points of each strake
/// from the Connection Point (Projected Bow Rising Line) upward along the Fashion Timbers
/// to the very top of the Transom.
/// Also generates proportional node points along the top Transom Timber matching UL strakes.
/// </summary>
[ExecuteInEditMode]
public class TransomTimbersModel : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FashionTimberModel fashionTimberModel;
    public RibbandStrakesModel ribbandStrakesModel;

    [Header("Transom Timber Settings")]
    [Tooltip("Master toggle to show/hide transverse transom timbers.")]
    public bool showTransomTimbers = true;

    [Range(2, 50)]
    [Tooltip("Number of sample points along each transverse transom timber beam across X.")]
    public int transomSamplesPerTimber = 20;

    [Header("Connected Strakes (Read-Only)")]
    [Tooltip("Dynamically calculated transom timber count matching ribband strakes from Connection Point to Transom Top.")]
    public int connectedTimberCount;

    [Header("Top Transom Timber Proportional Node Points")]
    [Tooltip("Master toggle to show/hide proportional node points along the top Transom Timber beam.")]
    public bool showTopTransomPoints = true;

    [Range(0.01f, 0.2f)]
    [Tooltip("Sphere radius for top transom point node markers.")]
    public float topPointRadius = 0.05f;

    public Color topPointColor = Color.yellow;

    [Tooltip("Number of proportional node points generated along the top Transom Timber per side.")]
    public int topPointCountPerSide;

    [Header("Visualization Settings")]
    public bool showLabels = true;
    public Color timberColor = Color.cyan;

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        FindReferences();
        transomSamplesPerTimber = Mathf.Max(2, transomSamplesPerTimber);
        topPointRadius = Mathf.Max(0.01f, topPointRadius);
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

        if (ribbandStrakesModel == null) ribbandStrakesModel = GetComponentInParent<RibbandStrakesModel>();
        if (ribbandStrakesModel == null) ribbandStrakesModel = FindFirstObjectByType<RibbandStrakesModel>();
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || ribbandStrakesModel == null) return;

        sheerPlan.UpdateCachedValues();

        if (showTransomTimbers)
        {
            DrawTransomTimbers();
        }

        if (showTopTransomPoints)
        {
            DrawTopTransomPoints();
        }
    }

    public void DrawTransomTimbers()
    {
        if (!showTransomTimbers) return;

        List<Vector3> stbdLandings = ribbandStrakesModel != null
            ? ribbandStrakesModel.GetSternLandingPointsFromConnectionUpward(true)
            : null;
        List<Vector3> portLandings = ribbandStrakesModel != null
            ? ribbandStrakesModel.GetSternLandingPointsFromConnectionUpward(false)
            : null;

        if (stbdLandings == null || stbdLandings.Count == 0 || portLandings == null || portLandings.Count == 0)
        {
            connectedTimberCount = 0;
            return;
        }

        connectedTimberCount = Mathf.Min(stbdLandings.Count, portLandings.Count);
        int samples = Mathf.Max(2, transomSamplesPerTimber);

        Gizmos.color = timberColor;

        for (int k = 0; k < connectedTimberCount; k++)
        {
            Vector3 pPort = portLandings[k];
            Vector3 pStbd = stbdLandings[k];

            Vector3? prevPt = null;

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 currPt = Vector3.Lerp(pPort, pStbd, t);

                if (prevPt.HasValue)
                {
                    Gizmos.DrawLine(prevPt.Value, currPt);
                }

                prevPt = currPt;
            }

#if UNITY_EDITOR
            if (showLabels)
            {
                if (_labelStyle == null)
                {
                    _labelStyle = new GUIStyle();
                    _labelStyle.alignment = TextAnchor.MiddleCenter;
                }
                _labelStyle.normal.textColor = timberColor;

                Vector3 midPt = (pPort + pStbd) * 0.5f;
                Handles.Label(midPt + new Vector3(0, 0.15f, 0), $"TT {k + 1}", _labelStyle);
            }
#endif
        }
    }

    public void DrawTopTransomPoints()
    {
        List<Vector3> stbdTopPts = GetTopTransomPoints(true);
        List<Vector3> portTopPts = GetTopTransomPoints(false);

        topPointCountPerSide = stbdTopPts.Count;
        if (stbdTopPts.Count == 0) return;

        Gizmos.color = topPointColor;

        for (int k = 0; k < stbdTopPts.Count; k++)
        {
            Vector3 pStbd = stbdTopPts[k];
            Gizmos.DrawSphere(pStbd, topPointRadius);

            if (k < portTopPts.Count)
            {
                Vector3 pPort = portTopPts[k];
                Gizmos.DrawSphere(pPort, topPointRadius);

#if UNITY_EDITOR
                if (showLabels)
                {
                    if (_topLabelStyle == null)
                    {
                        _topLabelStyle = new GUIStyle();
                        _topLabelStyle.alignment = TextAnchor.MiddleCenter;
                    }
                    _topLabelStyle.normal.textColor = topPointColor;

                    Handles.Label(pStbd + new Vector3(0, 0.2f, 0), $"TP S{k + 1}", _topLabelStyle);
                    Handles.Label(pPort + new Vector3(0, 0.2f, 0), $"TP P{k + 1}", _topLabelStyle);
                }
#endif
            }
        }
    }

    /// <summary>
    /// Returns proportional node points along the top Transom Timber beam (at transom top height)
    /// matching each Upper-Lower Mould (UL) strake landing ratio.
    /// </summary>
    public List<Vector3> GetTopTransomPoints(bool isStbd)
    {
        List<Vector3> topPoints = new List<Vector3>();
        FindReferences();
        if (sheerPlan == null || ribbandStrakesModel == null || fashionTimberModel == null) return topPoints;

        List<Vector3> ulLandings = ribbandStrakesModel.GetULSternLandingPoints(isStbd);
        if (ulLandings == null || ulLandings.Count == 0) return topPoints;

        // Obtain exact Y, Z, and half-breadth of the top Transom Timber line
        List<Vector3> allTransomLandings = ribbandStrakesModel.GetSternLandingPointsFromConnectionUpward(isStbd);

        float topY = sheerPlan.cachedTransomTopPoint.y;
        float topZ = sheerPlan.cachedTransomTopPoint.z;
        float topOuterX = fashionTimberModel.GetNaturalHullXAtYZ(topY, topZ);

        if (allTransomLandings != null && allTransomLandings.Count > 0)
        {
            Vector3 topLanding = allTransomLandings[allTransomLandings.Count - 1];
            topY = topLanding.y;
            topZ = topLanding.z;
            topOuterX = Mathf.Abs(topLanding.x);
        }

        float sideSign = isStbd ? 1f : -1f;

        // Measure maximum HoB width at the Fashion Timbers among the UL strakes
        float maxHobX = 0.0001f;
        for (int k = 0; k < ulLandings.Count; k++)
        {
            maxHobX = Mathf.Max(maxHobX, Mathf.Abs(ulLandings[k].x));
        }

        for (int k = 0; k < ulLandings.Count; k++)
        {
            Vector3 pLanding = ulLandings[k];
            float ratio = Mathf.Clamp01(Mathf.Abs(pLanding.x) / maxHobX);

            float topX = sideSign * ratio * topOuterX;
            topPoints.Add(new Vector3(topX, topY, topZ));
        }

        return topPoints;
    }

    /// <summary>
    /// Returns 3D points along a specific transverse transom timber beam (from Port landing to Starboard landing).
    /// </summary>
    public List<Vector3> GetTransomTimberPoints(int timberIndex, int samplesOverride = 0)
    {
        List<Vector3> pts = new List<Vector3>();
        FindReferences();
        if (ribbandStrakesModel == null) return pts;

        List<Vector3> stbdLandings = ribbandStrakesModel.GetSternLandingPointsFromConnectionUpward(true);
        List<Vector3> portLandings = ribbandStrakesModel.GetSternLandingPointsFromConnectionUpward(false);

        if (stbdLandings == null || portLandings == null) return pts;
        if (timberIndex < 0 || timberIndex >= Mathf.Min(stbdLandings.Count, portLandings.Count)) return pts;

        Vector3 pPort = portLandings[timberIndex];
        Vector3 pStbd = stbdLandings[timberIndex];

        int samples = samplesOverride > 0 ? samplesOverride : Mathf.Max(2, transomSamplesPerTimber);

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            pts.Add(Vector3.Lerp(pPort, pStbd, t));
        }

        return pts;
    }

#if UNITY_EDITOR
    private static GUIStyle _labelStyle;
    private static GUIStyle _topLabelStyle;
#endif
}
