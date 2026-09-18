using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// FMButtockLines generates and visualizes buttock lines (lengthwise vertical cuts) of the FramingModel.
/// The centerline (X = 0 or keel half-width) matches the Sheer Plan profile, while additional 
/// buttock lines are spaced outward at constant X offsets from the centerline across the hull.
/// </summary>
[ExecuteInEditMode]
public class FMButtockLines : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Reference to the SheerPlan to get profile endpoints.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the BodyPlan to get Keel Width and Master Frame settings.")]
    public BodyPlan bodyPlan;

    [Tooltip("Reference to the BreadthPlan to get Max Beam and Half-Breadths.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the FramingModel to evaluate 3D transverse frame sections.")]
    public FramingModel framingModel;

    [Header("Buttock Line Settings")]
    [Tooltip("Show Buttock Lines in Scene view gizmos.")]
    public bool showButtockLines = true;

    [Tooltip("Number of buttock lines per side (including centerline if enabled).")]
    [Range(1, 20)]
    public int numberOfButtockLines = 5;

    [Tooltip("Line segment resolution / detail steps along Z for evaluating each buttock curve.")]
    [Range(20, 300)]
    public int buttockLineResolution = 100;

    [Tooltip("Draw centerline buttock line at X = 0 (or Keel half-width).")]
    public bool showCenterline = true;

    [Header("Gizmo Settings")]
    [Tooltip("Color of the buttock line curves.")]
    public Color buttockLineColor = new Color(0.2f, 0.8f, 1f, 0.95f);

    [Tooltip("Show X-offset labels for buttock lines in Scene view.")]
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
    }

    public float GetKeelHalfWidth()
    {
        if (bodyPlan != null) return bodyPlan.keelWidth / 2f;
        if (framingModel != null) return framingModel.GetKeelHalfWidth();
        return 0.25f;
    }

    /// <summary>
    /// Calculates the list of X offsets (buttock distances from centerline) for the buttock lines.
    /// Spaced with equal step distances between Centerline (X = 0) and Maximum Beam (half-breadth).
    /// </summary>
    public List<float> GetButtockXOffsets()
    {
        List<float> offsets = new List<float>();
        if (bodyPlan == null && framingModel == null && breadthPlan == null) return offsets;

        float maxBreadthX = 5f;
        if (breadthPlan != null)
        {
            maxBreadthX = breadthPlan.maxBeam;
        }
        else if (bodyPlan != null && bodyPlan.breadthPlan != null)
        {
            maxBreadthX = bodyPlan.breadthPlan.maxBeam;
        }

        int count = Mathf.Max(1, numberOfButtockLines);
        float step = maxBreadthX / count;

        if (showCenterline)
        {
            offsets.Add(0f);
        }

        for (int i = 1; i <= count; i++)
        {
            float xOffset = i * step;
            offsets.Add(xOffset);
        }

        return offsets;
    }

    /// <summary>
    /// Evaluates the 3D surface point on a buttock line at constant xOffset, given side (+1/-1) and station z.
    /// </summary>
    public Vector3 GetButtockLinePointAtZ(float xOffset, float side, float z)
    {
        if (framingModel == null) return Vector3.zero;

        if (Mathf.Approximately(xOffset, 0f))
        {
            float hw = GetKeelHalfWidth();
            Vector3 pt = framingModel.GetTransverseFramePointAtU(z, side, 0f);
            return new Vector3(0f, pt.y, pt.z);
        }

        return framingModel.GetTransverseFramePointAtX(z, side, xOffset);
    }

    /// <summary>
    /// Generates list of 3D point sequences for all branches (lower hull and upper tumblehome intersections) 
    /// of the buttock line at constant xOffset.
    /// </summary>
    public List<List<Vector3>> GetButtockLineBranches(float xOffset, float side, int resolution)
    {
        List<List<Vector3>> branches = new List<List<Vector3>>();
        if (sheerPlan == null || framingModel == null) return branches;

        sheerPlan.UpdateCachedValues();
        float sternZ = sheerPlan.cachedProjectedSternRisingEnd.z;
        float bowZ = sheerPlan.cachedUpperStemTopPoint.z;

        side = Mathf.Sign(side);
        resolution = Mathf.Max(10, resolution);

        List<Vector3> lowerBranch = new List<Vector3>();
        List<Vector3> upperBranch = new List<Vector3>();

        for (int i = 0; i <= resolution; i++)
        {
            float t = (float)i / resolution;
            float z = Mathf.Lerp(sternZ, bowZ, t);

            if (Mathf.Approximately(xOffset, 0f))
            {
                Vector3 pt = framingModel.GetTransverseFramePointAtU(z, side, 0f);
                lowerBranch.Add(new Vector3(0f, pt.y, z));
            }
            else
            {
                List<Vector3> ptsAtZ = framingModel.GetAllTransverseFramePointsAtX(z, side, xOffset);
                if (ptsAtZ.Count > 0)
                {
                    lowerBranch.Add(ptsAtZ[0]); // Primary lower/main hull intersection
                }
                if (ptsAtZ.Count > 1)
                {
                    upperBranch.Add(ptsAtZ[1]); // Secondary upper tumblehome intersection
                }
            }
        }

        if (lowerBranch.Count > 0) branches.Add(lowerBranch);
        if (upperBranch.Count > 0) branches.Add(upperBranch);

        return branches;
    }

    /// <summary>
    /// Generates primary list of 3D points defining the longitudinal buttock curve at constant xOffset 
    /// from sternZ to bowZ.
    /// </summary>
    public List<Vector3> GetButtockLinePoints(float xOffset, float side, int resolution)
    {
        List<List<Vector3>> branches = GetButtockLineBranches(xOffset, side, resolution);
        if (branches.Count > 0) return branches[0];
        return new List<Vector3>();
    }

    private void OnDrawGizmos()
    {
        if (!showButtockLines) return;
        FindReferences();
        if (sheerPlan == null || bodyPlan == null || framingModel == null) return;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        if (breadthPlan != null) breadthPlan.UpdateCachedCurves();

        List<float> xOffsets = GetButtockXOffsets();
        Gizmos.color = buttockLineColor;

        foreach (float xOffset in xOffsets)
        {
            // For X = 0 (centerline), only draw once; for X > 0 draw Port & Starboard sides
            int startSide = Mathf.Approximately(xOffset, 0f) ? 1 : -1;
            int stepSide = Mathf.Approximately(xOffset, 0f) ? 2 : 2;

            for (int sideSign = startSide; sideSign <= 1; sideSign += stepSide)
            {
                float side = sideSign;

                List<List<Vector3>> branches = GetButtockLineBranches(xOffset, side, buttockLineResolution);

                foreach (List<Vector3> branch in branches)
                {
                    for (int i = 0; i < branch.Count - 1; i++)
                    {
                        Gizmos.DrawLine(branch[i], branch[i + 1]);
                    }

#if UNITY_EDITOR
                    if (showLabels && side > 0 && branch.Count > 0)
                    {
                        Vector3 labelPt = branch[branch.Count - 1];
                        GUIStyle labelStyle = new GUIStyle();
                        labelStyle.normal.textColor = buttockLineColor;
                        labelStyle.alignment = TextAnchor.LowerCenter;
                        labelStyle.contentOffset = new Vector2(0, -10);

                        string labelText = Mathf.Approximately(xOffset, 0f) ? "Centerline Buttock" : $"Buttock Line (X={xOffset:F2})";
                        Handles.Label(labelPt, labelText, labelStyle);
                    }
#endif
                }
            }
        }
    }
}
