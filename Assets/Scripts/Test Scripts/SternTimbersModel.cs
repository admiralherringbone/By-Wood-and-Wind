using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class SternTimbersModel : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the ModularBodyPlan.")]
    public BodyPlanTest bodyPlan;

    [Tooltip("Reference to the FashionTimberModelTest3.")]
    public FashionTimberModelTest3 fashionTimberModel;

    [Header("Stern Timber Settings")]
    [Range(1, 20)]
    [Tooltip("Number of stern timbers on each side of the centerline (Starboard & Port).")]
    public int sternTimberCount = 4;

    [Range(10, 100)]
    [Tooltip("Number of segment samples along each stern timber curve.")]
    public int resolution = 40;

    [Header("Visualization")]
    public bool showSternTimbers = true;
    public bool showLabels = true;
    public Color timberColor = Color.yellow;

    private void OnValidate()
    {
        sternTimberCount = Mathf.Max(1, sternTimberCount);
        resolution = Mathf.Max(10, resolution);
    }

    private void OnDrawGizmos()
    {
        AutoCacheReferences();
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null || fashionTimberModel == null) return;
        DrawSternTimbers();
    }

    private void AutoCacheReferences()
    {
        if (sheerPlan == null) sheerPlan = Object.FindObjectOfType<SheerPlan>();
        if (breadthPlan == null) breadthPlan = Object.FindObjectOfType<BreadthPlan>();
        if (bodyPlan == null) bodyPlan = Object.FindObjectOfType<BodyPlanTest>();
        if (fashionTimberModel == null) fashionTimberModel = Object.FindObjectOfType<FashionTimberModelTest3>();
    }

    public void DrawSternTimbers()
    {
        if (!showSternTimbers) return;

        Gizmos.color = timberColor;

        Vector3 startPoint = sheerPlan.cachedHobSternIntersect;
        Vector3 endPoint = sheerPlan.cachedTransomTopPoint;

        int count = Mathf.Max(1, sternTimberCount);
        int samples = Mathf.Max(10, resolution);

        for (int k = 1; k <= count; k++)
        {
            float ratio = (float)k / count;

            Vector3 prevStbd = Vector3.zero;
            Vector3 prevPort = Vector3.zero;

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float currZ = Mathf.Lerp(startPoint.z, endPoint.z, t);
                float exactY = sheerPlan.GetSternProfileYAtZ(currZ);

                float outerX = fashionTimberModel.GetNaturalHullXAtYZ(exactY, currZ);
                float timberX = ratio * outerX;

                Vector3 currStbd = new Vector3(timberX, exactY, currZ);
                Vector3 currPort = new Vector3(-timberX, exactY, currZ);

                if (i > 0)
                {
                    Gizmos.DrawLine(prevStbd, currStbd);
                    Gizmos.DrawLine(prevPort, currPort);
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

    public List<Vector3> GetSternTimberPoints(int timberIndex, bool isStbd, int samplesOverride = 0)
    {
        List<Vector3> pts = new List<Vector3>();
        AutoCacheReferences();
        if (sheerPlan == null || fashionTimberModel == null) return pts;

        Vector3 startPoint = sheerPlan.cachedHobSternIntersect;
        Vector3 endPoint = sheerPlan.cachedTransomTopPoint;

        int samples = samplesOverride > 0 ? samplesOverride : Mathf.Max(10, resolution);
        int count = Mathf.Max(1, sternTimberCount);
        float ratio = Mathf.Clamp01((float)timberIndex / count);

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
