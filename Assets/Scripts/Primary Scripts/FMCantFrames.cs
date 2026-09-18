using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum CantFrameStartLocation
{
    BowRisingEnd,
    KeelBow
}

/// <summary>
/// FMCantFrames defines longitudinal plane cuts at the bow of the ship.
/// They are straight lengthwise cuts (constant X planes parallel to the Z axis) extending 
/// from either the Bow End of the Rising Line or the Keel Bow forward to the Stem.
/// </summary>
[ExecuteInEditMode]
public class FMCantFrames : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Reference to the SheerPlan to get Stem, Rising Line, and Keel endpoints.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the BodyPlan to get Keel Width settings.")]
    public BodyPlan bodyPlan;

    [Tooltip("Reference to the BreadthPlan to get Max Beam and Half-Breadths.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the FramingModel to evaluate 3D transverse frame sections.")]
    public FramingModel framingModel;

    [Tooltip("Reference to the ConnectionGuideShape to get active connection guide curves.")]
    public ConnectionGuideShape connectionGuideShape;

    [Header("Cant Frame Settings")]
    [Tooltip("Show Cant Frames in Scene view gizmos.")]
    public bool showCantFrames = true;

    [Tooltip("Select starting Z location for bow cant frames (Bow End of Rising Line or Keel Bow).")]
    public CantFrameStartLocation startLocation = CantFrameStartLocation.BowRisingEnd;

    [Tooltip("Number of longitudinal cant frames per side between Keel half-width and Max Breadth at the start Z location.")]
    [Range(1, 20)]
    public int numberOfCantFrames = 5;

    [Tooltip("Line segment resolution / detail steps along Z for evaluating each cant frame curve.")]
    [Range(10, 100)]
    public int cantFrameResolution = 40;

    [Header("Gizmo Settings")]
    [Tooltip("Color of the cant frame curves.")]
    public Color cantFrameColor = new Color(1f, 0.8f, 0.2f, 0.95f);

    [Tooltip("Show X-offset labels for cant frames in Scene view.")]
    public bool showLabels = true;

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

        if (framingModel == null)
        {
            framingModel = GetComponentInParent<FramingModel>();
            if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();
        }

        if (connectionGuideShape == null)
        {
            connectionGuideShape = GetComponentInParent<ConnectionGuideShape>();
            if (connectionGuideShape == null) connectionGuideShape = FindFirstObjectByType<ConnectionGuideShape>();
        }
    }

    public float GetKeelHalfWidth()
    {
        if (bodyPlan != null) return bodyPlan.keelWidth / 2f;
        if (framingModel != null) return framingModel.GetKeelHalfWidth();
        return 0.25f;
    }

    /// <summary>
    /// Gets the starting Z coordinate based on startLocation setting (KeelBow vs BowRisingEnd).
    /// </summary>
    public float GetStartZ()
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        return startLocation == CantFrameStartLocation.KeelBow
            ? sheerPlan.cachedKeelBowPoint.z
            : sheerPlan.cachedBowRisingEnd.z;
    }

    /// <summary>
    /// Calculates the list of X offsets for the bow cant frames.
    /// Spaced with equal step distances between Keel half-width (hw) and Max Breadth at the start Z location.
    /// </summary>
    public List<float> GetCantFrameXOffsets()
    {
        List<float> offsets = new List<float>();
        if (sheerPlan == null) return offsets;

        float startZ = GetStartZ();
        float hw = GetKeelHalfWidth();

        float maxBreadthAtStart = hw;
        if (connectionGuideShape != null)
        {
            maxBreadthAtStart = Mathf.Max(hw, connectionGuideShape.GetConnectionGuideXAtZ(startZ));
        }
        else if (breadthPlan != null)
        {
            maxBreadthAtStart = Mathf.Max(hw, breadthPlan.GetConnectionPointXAtZ(startZ));
        }

        int count = Mathf.Max(1, numberOfCantFrames);
        float step = (maxBreadthAtStart - hw) / count;

        for (int i = 0; i <= count; i++)
        {
            float xOffset = hw + (i * step);
            offsets.Add(xOffset);
        }

        return offsets;
    }

    /// <summary>
    /// Generates list of 3D points defining the longitudinal bow cut curve at constant xOffset 
    /// from startZ forward to the Stem.
    /// </summary>
    public List<Vector3> GetCantFrameCutPoints(float xOffset, float side, int resolution)
    {
        List<Vector3> pts = new List<Vector3>();
        if (sheerPlan == null || framingModel == null) return pts;

        float startZ = GetStartZ();
        float stemTopZ = sheerPlan.cachedUpperStemTopPoint.z;

        side = Mathf.Sign(side);
        resolution = Mathf.Max(5, resolution);

        for (int i = 0; i <= resolution; i++)
        {
            float t = (float)i / resolution;
            float z = Mathf.Lerp(startZ, stemTopZ, t);

            Vector3 pt = framingModel.GetTransverseFramePointAtX(z, side, xOffset);
            pts.Add(pt);
        }

        return pts;
    }

    private void OnDrawGizmos()
    {
        if (!showCantFrames) return;
        FindReferences();
        if (sheerPlan == null || bodyPlan == null || framingModel == null) return;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        if (breadthPlan != null) breadthPlan.UpdateCachedCurves();

        List<float> xOffsets = GetCantFrameXOffsets();
        Gizmos.color = cantFrameColor;

        foreach (float xOffset in xOffsets)
        {
            for (int sideSign = -1; sideSign <= 1; sideSign += 2)
            {
                float side = sideSign;

                List<Vector3> cutPts = GetCantFrameCutPoints(xOffset, side, cantFrameResolution);
                for (int i = 0; i < cutPts.Count - 1; i++)
                {
                    Gizmos.DrawLine(cutPts[i], cutPts[i + 1]);
                }

#if UNITY_EDITOR
                if (showLabels && side > 0 && cutPts.Count > 0)
                {
                    Vector3 labelPt = cutPts[cutPts.Count - 1];
                    GUIStyle labelStyle = new GUIStyle();
                    labelStyle.normal.textColor = cantFrameColor;
                    labelStyle.alignment = TextAnchor.LowerCenter;
                    labelStyle.contentOffset = new Vector2(0, -10);

                    Handles.Label(labelPt, $"Cant Frame (X={xOffset:F2})", labelStyle);
                }
#endif
            }
        }
    }
}
