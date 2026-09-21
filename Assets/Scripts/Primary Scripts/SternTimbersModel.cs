using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Stern Timbers Model.
/// Generates stern timbers ascending along the stern profile rake and transom curve.
/// Each stern timber spans from the stern landing point of an Upper-Lower Mould (UL) strake
/// on the Fashion Timber up to its corresponding top Transom Timber point on the top Transom beam.
/// Curvature is blended based on proximity to the backbone centerline (X=0): timbers near the
/// centerline are vertically straight in X/Y projection, while outer timbers follow the 3D Fashion Timber contour.
/// </summary>
[ExecuteInEditMode]
public class SternTimbersModel : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FashionTimberModel fashionTimberModel;
    public RibbandStrakesModel ribbandStrakesModel;
    public TransomTimbersModel transomTimbersModel;

    [Header("Stern Timber Anchor Settings")]
    [Tooltip("When enabled, stern timbers anchor directly to UL ribband strakes and top Transom Timber points.")]
    public bool useRibbandStrakeAnchors = true;

    [Header("Stern Timber Contour Settings")]
    [Range(0.5f, 3.0f)]
    [Tooltip("Controls how quickly timber curvature blends from 100% straight near the centerline to curved at the Fashion Timber.")]
    public float contourBlendPower = 1.0f;

    [Header("Connected Ribband Strakes (Read-Only)")]
    [Tooltip("Dynamically calculated stern timber count matching UL ribband strakes.")]
    public int connectedStrakeCount;

    [Header("Fallback Fixed Timber Settings (When Anchors Disabled)")]
    [Range(1, 20)]
    [Tooltip("Number of stern timbers per side used if strake anchoring is disabled.")]
    public int sternTimberCount = 4;

    [Range(10, 100)]
    [Tooltip("Number of segment samples along each stern timber curve.")]
    public int resolution = 40;

    [Header("Visualization Settings")]
    public bool showSternTimbers = true;
    public bool showLabels = true;
    public Color timberColor = Color.yellow;

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        FindReferences();
        sternTimberCount = Mathf.Max(1, sternTimberCount);
        resolution = Mathf.Max(10, resolution);
        contourBlendPower = Mathf.Max(0.1f, contourBlendPower);
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

        if (transomTimbersModel == null) transomTimbersModel = GetComponentInParent<TransomTimbersModel>();
        if (transomTimbersModel == null) transomTimbersModel = FindFirstObjectByType<TransomTimbersModel>();
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null || fashionTimberModel == null) return;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        breadthPlan.UpdateCachedCurves();

        if (showSternTimbers)
        {
            DrawSternTimbers();
        }
    }

    public void DrawSternTimbers()
    {
        if (!showSternTimbers) return;

        Gizmos.color = timberColor;

        int samples = Mathf.Max(10, resolution);

        List<Vector3> stbdBottom = (useRibbandStrakeAnchors && ribbandStrakesModel != null)
            ? ribbandStrakesModel.GetULSternLandingPoints(true)
            : null;
        List<Vector3> portBottom = (useRibbandStrakeAnchors && ribbandStrakesModel != null)
            ? ribbandStrakesModel.GetULSternLandingPoints(false)
            : null;

        List<Vector3> stbdTop = (useRibbandStrakeAnchors && transomTimbersModel != null)
            ? transomTimbersModel.GetTopTransomPoints(true)
            : null;
        List<Vector3> portTop = (useRibbandStrakeAnchors && transomTimbersModel != null)
            ? transomTimbersModel.GetTopTransomPoints(false)
            : null;

        if (stbdBottom != null && stbdBottom.Count > 0 && portBottom != null && portBottom.Count > 0)
        {
            connectedStrakeCount = stbdBottom.Count;
            int count = stbdBottom.Count;

            for (int k = 0; k < count; k++)
            {
                Vector3 pBottomStbd = stbdBottom[k];
                Vector3 pBottomPort = portBottom[k];

                Vector3 pTopStbd = (stbdTop != null && k < stbdTop.Count)
                    ? stbdTop[k]
                    : sheerPlan.cachedTransomTopPoint;
                Vector3 pTopPort = (portTop != null && k < portTop.Count)
                    ? portTop[k]
                    : sheerPlan.cachedTransomTopPoint;

                float outerXBottomStbd = fashionTimberModel.GetNaturalHullXAtYZ(pBottomStbd.y, pBottomStbd.z);
                float rBottomStbd = (outerXBottomStbd > 0.0001f) ? Mathf.Clamp01(Mathf.Abs(pBottomStbd.x) / outerXBottomStbd) : 0.5f;

                float outerXTopStbd = fashionTimberModel.GetNaturalHullXAtYZ(pTopStbd.y, pTopStbd.z);
                float rTopStbd = (outerXTopStbd > 0.0001f) ? Mathf.Clamp01(Mathf.Abs(pTopStbd.x) / outerXTopStbd) : 0.5f;

                float outerXBottomPort = fashionTimberModel.GetNaturalHullXAtYZ(pBottomPort.y, pBottomPort.z);
                float rBottomPort = (outerXBottomPort > 0.0001f) ? Mathf.Clamp01(Mathf.Abs(pBottomPort.x) / outerXBottomPort) : 0.5f;

                float outerXTopPort = fashionTimberModel.GetNaturalHullXAtYZ(pTopPort.y, pTopPort.z);
                float rTopPort = (outerXTopPort > 0.0001f) ? Mathf.Clamp01(Mathf.Abs(pTopPort.x) / outerXTopPort) : 0.5f;

                // Calculate normalized proximity ratio u_k in [0, 1] across the array of stern timbers from centerline (0) to fashion timber (1)
                float uStbd = (count > 1) ? (float)k / (count - 1) : 0f;
                float uPort = (count > 1) ? (float)k / (count - 1) : 0f;

                float blendWeightStbd = Mathf.Pow(uStbd, Mathf.Max(0.1f, contourBlendPower));
                float blendWeightPort = Mathf.Pow(uPort, Mathf.Max(0.1f, contourBlendPower));

                Vector3? prevStbd = null;
                Vector3? prevPort = null;

                for (int i = 0; i <= samples; i++)
                {
                    float t = i / (float)samples;

                    float currZStbd = Mathf.Lerp(pBottomStbd.z, pTopStbd.z, t);
                    float exactYStbd = sheerPlan.GetSternProfileYAtZ(currZStbd);

                    float xStraightStbd = Mathf.Lerp(pBottomStbd.x, pTopStbd.x, t);
                    float ratioStbd = Mathf.Lerp(rBottomStbd, rTopStbd, t);
                    float outerXStbd = fashionTimberModel.GetNaturalHullXAtYZ(exactYStbd, currZStbd);
                    float xCurvedStbd = ratioStbd * outerXStbd;
                    float timberXStbd = Mathf.Lerp(xStraightStbd, xCurvedStbd, blendWeightStbd);

                    float currZPort = Mathf.Lerp(pBottomPort.z, pTopPort.z, t);
                    float exactYPort = sheerPlan.GetSternProfileYAtZ(currZPort);

                    float xStraightPort = Mathf.Lerp(pBottomPort.x, pTopPort.x, t);
                    float ratioPort = Mathf.Lerp(rBottomPort, rTopPort, t);
                    float outerXPort = fashionTimberModel.GetNaturalHullXAtYZ(exactYPort, currZPort);
                    float xCurvedPort = -ratioPort * outerXPort;
                    float timberXPort = Mathf.Lerp(xStraightPort, xCurvedPort, blendWeightPort);

                    Vector3 currStbd = new Vector3(timberXStbd, exactYStbd, currZStbd);
                    Vector3 currPort = new Vector3(timberXPort, exactYPort, currZPort);

                    if (prevStbd.HasValue && prevPort.HasValue)
                    {
                        Gizmos.DrawLine(prevStbd.Value, currStbd);
                        Gizmos.DrawLine(prevPort.Value, currPort);
                    }

                    prevStbd = currStbd;
                    prevPort = currPort;
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

                    float topOuterXStbd = fashionTimberModel.GetNaturalHullXAtYZ(pTopStbd.y, pTopStbd.z);
                    float topCurvedStbd = rTopStbd * topOuterXStbd;
                    float topXStbd = Mathf.Lerp(pTopStbd.x, topCurvedStbd, blendWeightStbd);

                    float topOuterXPort = fashionTimberModel.GetNaturalHullXAtYZ(pTopPort.y, pTopPort.z);
                    float topCurvedPort = -rTopPort * topOuterXPort;
                    float topXPort = Mathf.Lerp(pTopPort.x, topCurvedPort, blendWeightPort);

                    Handles.Label(new Vector3(topXStbd, pTopStbd.y + 0.3f, pTopStbd.z), $"ST S{k + 1}", _labelStyle);
                    Handles.Label(new Vector3(topXPort, pTopPort.y + 0.3f, pTopPort.z), $"ST P{k + 1}", _labelStyle);
                }
#endif
            }
        }
        else
        {
            // Fallback: Fixed timber count mode
            int count = Mathf.Max(1, sternTimberCount);
            connectedStrakeCount = count;

            Vector3 startPoint = (sheerPlan.enableCounter && sheerPlan.cachedCounterBasePoint.y < sheerPlan.cachedHobSternIntersect.y)
                ? sheerPlan.cachedCounterBasePoint
                : sheerPlan.cachedHobSternIntersect;
            Vector3 endPoint = sheerPlan.cachedTransomTopPoint;

            for (int k = 1; k <= count; k++)
            {
                float ratio = (float)k / (count + 1);
                float blendWeight = Mathf.Pow(ratio, Mathf.Max(0.1f, contourBlendPower));

                Vector3? prevStbd = null;
                Vector3? prevPort = null;

                for (int i = 0; i <= samples; i++)
                {
                    float t = i / (float)samples;
                    float currZ = Mathf.Lerp(startPoint.z, endPoint.z, t);
                    float exactY = sheerPlan.GetSternProfileYAtZ(currZ);

                    float xStraight = ratio * endPoint.x;
                    float outerX = fashionTimberModel.GetNaturalHullXAtYZ(exactY, currZ);
                    float xCurved = ratio * outerX;
                    float timberX = Mathf.Lerp(xStraight, xCurved, blendWeight);

                    Vector3 currStbd = new Vector3(timberX, exactY, currZ);
                    Vector3 currPort = new Vector3(-timberX, exactY, currZ);

                    if (prevStbd.HasValue && prevPort.HasValue)
                    {
                        Gizmos.DrawLine(prevStbd.Value, currStbd);
                        Gizmos.DrawLine(prevPort.Value, currPort);
                    }

                    prevStbd = currStbd;
                    prevPort = currPort;
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

                    float topZ = endPoint.z;
                    float topY = sheerPlan.GetSternProfileYAtZ(topZ);
                    float topOuterX = fashionTimberModel.GetNaturalHullXAtYZ(topY, topZ);
                    float topCurved = ratio * topOuterX;
                    float topTimberX = Mathf.Lerp(0f, topCurved, blendWeight);

                    Handles.Label(new Vector3(topTimberX, topY + 0.3f, topZ), $"ST S{k}", _labelStyle);
                    Handles.Label(new Vector3(-topTimberX, topY + 0.3f, topZ), $"ST P{k}", _labelStyle);
                }
#endif
            }
        }
    }

    public List<Vector3> GetSternTimbersPoints(int timberIndex, bool isStbd, int samplesOverride = 0)
    {
        List<Vector3> pts = new List<Vector3>();
        FindReferences();
        if (sheerPlan == null || fashionTimberModel == null) return pts;

        int samples = samplesOverride > 0 ? samplesOverride : Mathf.Max(10, resolution);

        List<Vector3> bottomLandings = (useRibbandStrakeAnchors && ribbandStrakesModel != null)
            ? ribbandStrakesModel.GetULSternLandingPoints(isStbd)
            : null;
        List<Vector3> topLandings = (useRibbandStrakeAnchors && transomTimbersModel != null)
            ? transomTimbersModel.GetTopTransomPoints(isStbd)
            : null;

        if (bottomLandings != null && timberIndex >= 0 && timberIndex < bottomLandings.Count)
        {
            Vector3 pBottom = bottomLandings[timberIndex];
            Vector3 pTop = (topLandings != null && timberIndex < topLandings.Count)
                ? topLandings[timberIndex]
                : sheerPlan.cachedTransomTopPoint;

            float outerXBottom = fashionTimberModel.GetNaturalHullXAtYZ(pBottom.y, pBottom.z);
            float rBottom = (outerXBottom > 0.0001f) ? Mathf.Clamp01(Mathf.Abs(pBottom.x) / outerXBottom) : 0.5f;

            float outerXTop = fashionTimberModel.GetNaturalHullXAtYZ(pTop.y, pTop.z);
            float rTop = (outerXTop > 0.0001f) ? Mathf.Clamp01(Mathf.Abs(pTop.x) / outerXTop) : 0.5f;

            float uRatio = (bottomLandings.Count > 1) ? (float)timberIndex / (bottomLandings.Count - 1) : 0f;
            float blendWeight = Mathf.Pow(uRatio, Mathf.Max(0.1f, contourBlendPower));

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float currZ = Mathf.Lerp(pBottom.z, pTop.z, t);
                float exactY = sheerPlan.GetSternProfileYAtZ(currZ);

                float xStraight = Mathf.Lerp(pBottom.x, pTop.x, t);
                float ratio = Mathf.Lerp(rBottom, rTop, t);
                float outerX = fashionTimberModel.GetNaturalHullXAtYZ(exactY, currZ);
                float xCurved = (isStbd ? 1f : -1f) * ratio * outerX;
                float timberX = Mathf.Lerp(xStraight, xCurved, blendWeight);

                pts.Add(new Vector3(timberX, exactY, currZ));
            }
        }
        else
        {
            Vector3 startPoint = (sheerPlan.enableCounter && sheerPlan.cachedCounterBasePoint.y < sheerPlan.cachedHobSternIntersect.y)
                ? sheerPlan.cachedCounterBasePoint
                : sheerPlan.cachedHobSternIntersect;
            Vector3 endPoint = sheerPlan.cachedTransomTopPoint;

            int count = Mathf.Max(1, sternTimberCount);
            float ratio = Mathf.Clamp01((float)(timberIndex + 1) / (count + 1));
            float blendWeight = Mathf.Pow(ratio, Mathf.Max(0.1f, contourBlendPower));

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float currZ = Mathf.Lerp(startPoint.z, endPoint.z, t);
                float exactY = sheerPlan.GetSternProfileYAtZ(currZ);

                float xStraight = (isStbd ? 1f : -1f) * ratio * endPoint.x;
                float outerX = fashionTimberModel.GetNaturalHullXAtYZ(exactY, currZ);
                float xCurved = (isStbd ? 1f : -1f) * ratio * outerX;
                float timberX = Mathf.Lerp(xStraight, xCurved, blendWeight);

                pts.Add(new Vector3(timberX, exactY, currZ));
            }
        }

        return pts;
    }

#if UNITY_EDITOR
    private static GUIStyle _labelStyle;
#endif
}
