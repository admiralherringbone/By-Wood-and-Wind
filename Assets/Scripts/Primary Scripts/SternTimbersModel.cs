using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class SternTimbersModel : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FashionTimberModel fashionTimberModel;

    [Header("Stern Timber Settings")]
    [Range(1, 20)]
    [Tooltip("Number of stern timbers on each side of the centerline (Starboard & Port).")]
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

        Vector3 startPoint = (sheerPlan.enableCounter && sheerPlan.cachedCounterBasePoint.y < sheerPlan.cachedHobSternIntersect.y)
            ? sheerPlan.cachedCounterBasePoint
            : sheerPlan.cachedHobSternIntersect;
        Vector3 endPoint = sheerPlan.cachedTransomTopPoint;

        int count = Mathf.Max(1, sternTimberCount);
        int samples = Mathf.Max(10, resolution);

        for (int k = 1; k <= count; k++)
        {
            float ratio = (float)k / (count + 1);

            Vector3? prevStbd = null;
            Vector3? prevPort = null;

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float currZ = Mathf.Lerp(startPoint.z, endPoint.z, t);
                float exactY = sheerPlan.GetSternProfileYAtZ(currZ);

                float outerX = fashionTimberModel.GetNaturalHullXAtYZ(exactY, currZ);
                float timberX = ratio * outerX;

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
                float topTimberX = ratio * topOuterX;

                Handles.Label(new Vector3(topTimberX, topY + 0.3f, topZ), $"ST S{k}", _labelStyle);
                Handles.Label(new Vector3(-topTimberX, topY + 0.3f, topZ), $"ST P{k}", _labelStyle);
            }
#endif
        }
    }

    public List<Vector3> GetSternTimbersPoints(int timberIndex, bool isStbd, int samplesOverride = 0)
    {
        List<Vector3> pts = new List<Vector3>();
        FindReferences();
        if (sheerPlan == null || fashionTimberModel == null) return pts;

        Vector3 startPoint = (sheerPlan.enableCounter && sheerPlan.cachedCounterBasePoint.y < sheerPlan.cachedHobSternIntersect.y)
            ? sheerPlan.cachedCounterBasePoint
            : sheerPlan.cachedHobSternIntersect;
        Vector3 endPoint = sheerPlan.cachedTransomTopPoint;

        int samples = samplesOverride > 0 ? samplesOverride : Mathf.Max(10, resolution);
        int count = Mathf.Max(1, sternTimberCount);
        float ratio = Mathf.Clamp01((float)timberIndex / (count + 1));

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            float currZ = Mathf.Lerp(startPoint.z, endPoint.z, t);
            float exactY = sheerPlan.GetSternProfileYAtZ(currZ);

            float outerX = fashionTimberModel.GetNaturalHullXAtYZ(exactY, currZ);
            float timberX = ratio * outerX;

            if (isStbd) pts.Add(new Vector3(timberX, exactY, currZ));
            else pts.Add(new Vector3(-timberX, exactY, currZ));
        }

        return pts;
    }

#if UNITY_EDITOR
    private static GUIStyle _labelStyle;
#endif
}
