using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Practice script for generating vertical transition lines in the Sheer Plan (Z/Y plane).
/// Transitions as separate lines from:
/// Main Sheer Line -> Rising Line (with projected extension lines) -> Keel Line.
/// 
/// The number of lines is determined by a target vertical interval (default 0.25) at Midships.
/// Toward the bow and stern, lines automatically pack closer together as the boundary curves converge.
/// </summary>
[ExecuteInEditMode]
public class MainSheerKeelLineTransitionPractice : MonoBehaviour
{
    public enum TransitionMode
    {
        Lower_KeelToRising = 0,
        Upper_RisingToSheer = 1,
        Both_FullStack = 2,
        Bulwark_SheerToTopTimber = 3,
        All_FullStack = 4
    }

    public enum SpacingMode
    {
        Uniform_Interval,
        BodyPlan_Mould_Arc
    }

    [Header("Component References")]
    [Tooltip("Reference to SheerPlan component.")]
    public SheerPlan sheerPlan;

    [Header("Transition Settings")]
    [Tooltip("Which vertical transition region to evaluate and display.")]
    public TransitionMode transitionMode = TransitionMode.All_FullStack;

    [Tooltip("How vertical line heights are spaced: Uniform linear interval or Arc-length sampled from Body Plan Moulds.")]
    public SpacingMode spacingMode = SpacingMode.Uniform_Interval;

    [Tooltip("Target vertical interval between lines at Midships (default 0.25).")]
    public float pointInterval = 0.25f;

    [Tooltip("Longitudinal resolution along each transition line from Stern to Bow.")]
    [Range(10, 300)]
    public int resolution = 100;

    [Header("Visualization Settings")]
    [Tooltip("Display vertical transition lines in Scene View.")]
    public bool showLines = true;

    [Tooltip("Show baseline Keel Line.")]
    public bool showKeelLine = true;

    [Tooltip("Show middle Rising Line (including projected extensions).")]
    public bool showRisingLine = true;

    [Tooltip("Show top Main Sheer Line.")]
    public bool showSheerLine = true;

    [Tooltip("Show top Top Timber Line (top of Sheer Plan).")]
    public bool showTopTimberLine = true;

    [Tooltip("Color of Keel line (bottommost baseline).")]
    public Color keelColor = new Color(0.2f, 0.8f, 1f, 0.9f);

    [Tooltip("Color of Lower Region intermediate lines.")]
    public Color lowerLineColor = new Color(0.3f, 0.9f, 0.6f, 0.95f);

    [Tooltip("Color of Rising Line (middle boundary).")]
    public Color risingLineColor = new Color(1f, 0.85f, 0.2f, 0.9f);

    [Tooltip("Color of Upper Region intermediate lines.")]
    public Color upperLineColor = new Color(0.95f, 0.55f, 0.15f, 0.95f);

    [Tooltip("Color of Main Sheer line.")]
    public Color sheerLineColor = Color.yellow;

    [Tooltip("Color of Bulwark / Top Timber Region intermediate lines.")]
    public Color bulwarkLineColor = new Color(0.95f, 0.25f, 0.35f, 0.95f);

    [Tooltip("Color of Top Timber line (topmost boundary of Sheer Plan).")]
    public Color topTimberLineColor = new Color(0.9f, 0.1f, 0.2f, 0.95f);

    [Header("Body Plan Point Sampling")]
    [Tooltip("Display Gizmo points on the Midship Y-axis based on arc-length sampling of Body Plan's Lower Mould.")]
    public bool showMidshipYPoints = true;

    [Tooltip("Color of the sampled Midship Y Gizmo points.")]
    public Color pointColor = Color.magenta;

    [Tooltip("Radius of the Gizmo spheres drawn for Midship Y points.")]
    public float pointGizmoRadius = 0.15f;

    [Header("Stem Intersection Settings")]
    [Tooltip("Draw Gizmo spheres at the exact intersection of each transition line with the Stem.")]
    public bool showStemIntersectionPoints = true;

    [Tooltip("Color of Gizmo spheres drawn for Stem intersections.")]
    public Color stemPointColor = Color.cyan;

    [Tooltip("Radius of the Gizmo spheres drawn for Stem intersections.")]
    public float stemPointGizmoRadius = 0.15f;

    [Tooltip("Draw vertical dashed lines at the exact intersection of each transition line with the Stem.")]
    public bool showStemIntersectionDashedLines = true;

    [Tooltip("Color of the vertical dashed lines at Stem intersections.")]
    public Color stemDashedLineColor = Color.cyan;

    [Tooltip("Length of each dash segment for the vertical dashed lines.")]
    public float dashLength = 0.15f;

    [Header("Stern Intersection Settings")]
    [Tooltip("Draw Gizmo spheres at the exact intersection of each transition line with the Stern profile.")]
    public bool showSternIntersectionPoints = true;

    [Tooltip("Color of Gizmo spheres drawn for Stern intersections.")]
    public Color sternPointColor = Color.cyan;

    [Tooltip("Radius of the Gizmo spheres drawn for Stern intersections.")]
    public float sternPointGizmoRadius = 0.15f;

    [Tooltip("Draw vertical dashed lines at the exact intersection of each transition line with the Stern profile.")]
    public bool showSternIntersectionDashedLines = true;

    [Tooltip("Color of the vertical dashed lines at Stern intersections.")]
    public Color sternDashedLineColor = Color.cyan;

    private List<List<Vector3>> m_CachedLowerLines = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedUpperLines = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedBulwarkLines = new List<List<Vector3>>();
    private List<float> m_CachedMidshipYPoints = new List<float>();
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
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
    }

    /// <summary>
    /// Evaluates the extended Rising Line Y at station Z.
    /// Evaluates quadratic Bezier between Rising Line ends.
    /// Extended forward/aft past the active Rising Line ends using pure linear tangent projections of the Rising Line.
    /// Ignores sheerPlan's projected ends which reference Top Timber, Decks, or Transom.
    /// </summary>
    public float GetExtendedRisingLineYAtZ(float z)
    {
        if (sheerPlan == null) return 0f;

        sheerPlan.UpdateCachedValues();

        float zBowRisingEnd = sheerPlan.cachedBowRisingEnd.z;
        float zSternRisingEnd = sheerPlan.cachedSternRisingEnd.z;

        if (z > zBowRisingEnd)
        {
            float dz = zBowRisingEnd - sheerPlan.cachedBowP1Ctrl.z;
            float dy = sheerPlan.cachedBowRisingEnd.y - sheerPlan.cachedBowP1Ctrl.y;
            float slope = (Mathf.Abs(dz) > 0.0001f) ? (dy / dz) : 0f;
            return sheerPlan.cachedBowRisingEnd.y + slope * (z - zBowRisingEnd);
        }
        else if (z < zSternRisingEnd)
        {
            float dz = zSternRisingEnd - sheerPlan.cachedSternP1Ctrl.z;
            float dy = sheerPlan.cachedSternRisingEnd.y - sheerPlan.cachedSternP1Ctrl.y;
            float slope = (Mathf.Abs(dz) > 0.0001f) ? (dy / dz) : 0f;
            return sheerPlan.cachedSternRisingEnd.y + slope * (z - zSternRisingEnd);
        }
        else
        {
            return sheerPlan.GetRisingLineYAtZ(z);
        }
    }

    /// <summary>
    /// Evaluates the pure Keel Line Y at station Z.
    /// Returns the baseline keel height (Y = 0, or keel drop curve if present).
    /// Does NOT ascend up the Stem Rake or Stern Post Rake.
    /// </summary>
    public float GetPureKeelYAtZ(float z)
    {
        if (sheerPlan == null) return 0f;

        sheerPlan.UpdateCachedValues();

        if (sheerPlan.keelMidshipDrop == 0f) return 0f;

        float zBowKeel = sheerPlan.cachedKeelBowPoint.z;
        float zSternKeel = sheerPlan.cachedKeelSternPoint.z;

        if (z >= 0f)
        {
            if (z >= zBowKeel) return 0f;
            return sheerPlan.GetKeelYAtZ(z);
        }
        else
        {
            if (z <= zSternKeel) return 0f;
            return sheerPlan.GetKeelYAtZ(z);
        }
    }

    /// <summary>
    /// Evaluates the pure Main Sheer Line Y at station Z.
    /// </summary>
    public float GetPureMainSheerYAtZ(float z)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        return sheerPlan.GetMainSheerYAtZ(z);
    }

    /// <summary>
    /// Calculates midship vertical line counts for lower and upper regions based on pointInterval.
    /// </summary>
    public void GetMidshipLineCounts(out int numLower, out int numUpper)
    {
        numLower = 6;
        numUpper = 6;

        if (sheerPlan == null) return;
        sheerPlan.UpdateCachedValues();

        float midKeelY = GetPureKeelYAtZ(0f);
        float midRisingY = GetExtendedRisingLineYAtZ(0f);
        float midSheerY = GetPureMainSheerYAtZ(0f);

        float interval = Mathf.Max(0.01f, pointInterval);

        float lowerSpan = Mathf.Max(0.001f, midRisingY - midKeelY);
        float upperSpan = Mathf.Max(0.001f, midSheerY - midRisingY);

        numLower = Mathf.Max(1, Mathf.RoundToInt(lowerSpan / interval));
        numUpper = Mathf.Max(1, Mathf.RoundToInt(upperSpan / interval));
    }

    public List<float> GetLowerVProportions()
    {
        List<float> vProportions = new List<float>();

        if (spacingMode == SpacingMode.BodyPlan_Mould_Arc)
        {
            List<float> midYPoints = GetBodyPlanLowerMouldYPoints();
            float midKeelY = GetPureKeelYAtZ(0f);
            float midRisingY = GetExtendedRisingLineYAtZ(0f);
            float lowerSpan = Mathf.Max(0.001f, midRisingY - midKeelY);

            if (midYPoints != null && midYPoints.Count > 0)
            {
                foreach (float y in midYPoints)
                {
                    if (y <= midRisingY + 0.0001f)
                    {
                        float v = Mathf.Clamp01((y - midKeelY) / lowerSpan);
                        vProportions.Add(v);
                    }
                }
            }
            if (vProportions.Count == 0)
            {
                vProportions.Add(0f);
                vProportions.Add(1f);
            }
        }
        else
        {
            GetMidshipLineCounts(out int numLower, out _);
            for (int k = 0; k <= numLower; k++)
            {
                float v = (numLower > 0) ? (float)k / numLower : 0f;
                vProportions.Add(v);
            }
        }

        return vProportions;
    }

    public List<float> GetUpperUProportions()
    {
        List<float> uProportions = new List<float>();

        if (spacingMode == SpacingMode.BodyPlan_Mould_Arc)
        {
            List<float> midYPoints = GetBodyPlanLowerMouldYPoints();
            float midRisingY = GetExtendedRisingLineYAtZ(0f);
            float midSheerY = GetPureMainSheerYAtZ(0f);
            float upperSpan = Mathf.Max(0.001f, midSheerY - midRisingY);

            if (midYPoints != null && midYPoints.Count > 0)
            {
                foreach (float y in midYPoints)
                {
                    if (y > midRisingY - 0.0001f)
                    {
                        float u = Mathf.Clamp01((y - midRisingY) / upperSpan);
                        uProportions.Add(u);
                    }
                }
            }
            if (uProportions.Count == 0)
            {
                uProportions.Add(0f);
                uProportions.Add(1f);
            }
        }
        else
        {
            GetMidshipLineCounts(out _, out int numUpper);
            for (int k = 0; k <= numUpper; k++)
            {
                float u = (numUpper > 0) ? (float)k / numUpper : 0f;
                uProportions.Add(u);
            }
        }

        return uProportions;
    }

    /// <summary>
    /// Evaluates Lower Region vertical transition lines (Keel Line -> Rising Line).
    /// Uses selected SpacingMode (Uniform_Interval or BodyPlan_Mould_Arc).
    /// </summary>
    public List<List<Vector3>> GetLowerTransitionLines()
    {
        List<List<Vector3>> lines = new List<List<Vector3>>();
        if (sheerPlan == null) return lines;

        sheerPlan.UpdateCachedValues();

        List<float> vProportions = GetLowerVProportions();
        int res = Mathf.Clamp(resolution, 10, 300);
        float zStern = sheerPlan.cachedMainSheerSternIntersect.z;
        float zBow = sheerPlan.cachedMainSheerBowIntersect.z;

        if (zBow <= zStern + 0.001f) return lines;

        foreach (float v in vProportions)
        {
            List<Vector3> pts = new List<Vector3>();
            for (int i = 0; i <= res; i++)
            {
                float alpha = (float)i / res;
                float z = Mathf.Lerp(zStern, zBow, alpha);

                float keelY = GetPureKeelYAtZ(z);
                float risingY = GetExtendedRisingLineYAtZ(z);

                float y = Mathf.Lerp(keelY, risingY, v);
                pts.Add(new Vector3(0f, y, z));
            }
            lines.Add(pts);
        }

        return lines;
    }

    /// <summary>
    /// Evaluates Upper Region vertical transition lines (Rising Line -> Main Sheer Line).
    /// Uses selected SpacingMode (Uniform_Interval or BodyPlan_Mould_Arc).
    /// </summary>
    public List<List<Vector3>> GetUpperTransitionLines()
    {
        List<List<Vector3>> lines = new List<List<Vector3>>();
        if (sheerPlan == null) return lines;

        sheerPlan.UpdateCachedValues();

        List<float> uProportions = GetUpperUProportions();
        int res = Mathf.Clamp(resolution, 10, 300);
        float zStern = sheerPlan.cachedMainSheerSternIntersect.z;
        float zBow = sheerPlan.cachedMainSheerBowIntersect.z;

        if (zBow <= zStern + 0.001f) return lines;

        foreach (float u in uProportions)
        {
            List<Vector3> pts = new List<Vector3>();
            for (int i = 0; i <= res; i++)
            {
                float alpha = (float)i / res;
                float z = Mathf.Lerp(zStern, zBow, alpha);

                float risingY = GetExtendedRisingLineYAtZ(z);
                float sheerY = GetPureMainSheerYAtZ(z);

                float y = Mathf.Lerp(risingY, sheerY, u);
                pts.Add(new Vector3(0f, y, z));
            }
            lines.Add(pts);
        }

        return lines;
    }

    /// <summary>
    /// Evaluates vertical transition lines stacked above the Main Sheer Line up to the Top Line of the Sheer Plan.
    /// Full length lines stacked smoothly between Main Sheer Line and Top Line (the dashed line at the Top).
    /// </summary>
    public List<List<Vector3>> GetBulwarkTransitionLines()
    {
        List<List<Vector3>> lines = new List<List<Vector3>>();
        if (sheerPlan == null) return lines;

        sheerPlan.UpdateCachedValues();

        List<float> uProportions = GetTopTimberUProportions();
        int res = Mathf.Clamp(resolution, 10, 300);

        foreach (float u in uProportions)
        {
            List<Vector3> pts = new List<Vector3>();
            float zSternLine = GetSternIntersectionZForBulwark(u);
            float zBowLine = GetStemIntersectionZForBulwark(u);

            for (int i = 0; i <= res; i++)
            {
                float alpha = (float)i / res;
                float z = Mathf.Lerp(zSternLine, zBowLine, alpha);

                float sheerY = GetPureMainSheerYAtZ(z);
                float topY = sheerPlan.GetPureSheerYAtZ(z);

                float y = Mathf.Lerp(sheerY, topY, u);
                pts.Add(new Vector3(0f, y, z));
            }
            lines.Add(pts);
        }

        return lines;
    }

    public List<float> GetTopTimberUProportions()
    {
        List<float> uProportions = new List<float>();
        if (sheerPlan == null) return uProportions;

        sheerPlan.UpdateCachedValues();

        float midSheerY = GetPureMainSheerYAtZ(0f);
        float midTopY = sheerPlan.GetPureSheerYAtZ(0f);
        float topSpan = Mathf.Max(0.001f, midTopY - midSheerY);

        if (spacingMode == SpacingMode.BodyPlan_Mould_Arc)
        {
            List<float> midYPoints = GetBodyPlanBulwarkMouldYPoints();
            if (midYPoints != null && midYPoints.Count > 0)
            {
                foreach (float y in midYPoints)
                {
                    float u = Mathf.Clamp01((y - midSheerY) / topSpan);
                    uProportions.Add(u);
                }
            }
            if (uProportions.Count == 0)
            {
                uProportions.Add(0f);
                uProportions.Add(1f);
            }
        }
        else
        {
            float interval = Mathf.Max(0.01f, pointInterval);
            int numTop = Mathf.Max(1, Mathf.RoundToInt(topSpan / interval));
            for (int k = 0; k <= numTop; k++)
            {
                float u = (numTop > 0) ? (float)k / numTop : 0f;
                uProportions.Add(u);
            }
        }

        return uProportions;
    }

    public List<float> GetBodyPlanBulwarkMouldYPoints()
    {
        List<float> yPositions = new List<float>();

        BodyPlan bodyPlan = (sheerPlan != null) ? sheerPlan.bodyPlan : null;
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (bodyPlan == null) return yPositions;

        bodyPlan.UpdateCachedCurves();

        float midSheerY = GetPureMainSheerYAtZ(0f);
        float midTopY = sheerPlan.GetPureSheerYAtZ(0f);

        if (midTopY <= midSheerY + 0.001f) return yPositions;

        float minT = bodyPlan.GetUpperMouldTAtY(midSheerY);
        float maxT = bodyPlan.GetUpperMouldTAtY(midTopY);
        if (maxT < minT) maxT = 1.0f;

        const int samples = 200;
        List<Vector3> polyline = new List<Vector3>();
        List<float> cumDist = new List<float> { 0f };

        Vector3 prevPt = bodyPlan.EvaluateUpperMouldAtT(minT);
        polyline.Add(prevPt);

        float totalArcLen = 0f;
        for (int i = 1; i <= samples; i++)
        {
            float t = Mathf.Lerp(minT, maxT, (float)i / samples);
            Vector3 currPt = bodyPlan.EvaluateUpperMouldAtT(t);
            totalArcLen += Vector3.Distance(prevPt, currPt);
            polyline.Add(currPt);
            cumDist.Add(totalArcLen);
            prevPt = currPt;
        }

        if (totalArcLen <= 0.001f) return yPositions;

        float interval = Mathf.Max(0.01f, pointInterval);
        int numSegments = Mathf.Max(1, Mathf.RoundToInt(totalArcLen / interval));

        for (int j = 0; j <= numSegments; j++)
        {
            float targetDist = totalArcLen * ((float)j / numSegments);
            float y = GetYAtArcDistance(polyline, cumDist, targetDist);
            yPositions.Add(y);
        }

        return yPositions;
    }

    /// <summary>
    /// Calculates the exact Z-axis intersection point of each transition line with the Stem Rake curve
    /// using bisection root-finding on the 2D curve difference Y_line(Z) - Y_stem(Z).
    /// </summary>
    /// <summary>
    /// Evaluates the exact 3D Stern profile intersection points (0, Y, Z) for all active transition lines.
    /// Uses SheerPlan's continuous GetSternProfileZAtY method.
    /// </summary>
    public List<Vector3> GetSternIntersectionPoints()
    {
        List<Vector3> points = new List<Vector3>();
        if (sheerPlan == null) return points;

        sheerPlan.UpdateCachedValues();

        bool drawLower = (transitionMode == TransitionMode.Lower_KeelToRising || transitionMode == TransitionMode.Both_FullStack || transitionMode == TransitionMode.All_FullStack);
        bool drawUpper = (transitionMode == TransitionMode.Upper_RisingToSheer || transitionMode == TransitionMode.Both_FullStack || transitionMode == TransitionMode.All_FullStack);
        bool drawBulwark = (transitionMode == TransitionMode.Bulwark_SheerToTopTimber || transitionMode == TransitionMode.Both_FullStack || transitionMode == TransitionMode.All_FullStack);

        List<float> vProportions = drawLower ? GetLowerVProportions() : new List<float>();
        List<float> uProportions = drawUpper ? GetUpperUProportions() : new List<float>();
        List<float> bulwarkProportions = drawBulwark ? GetTopTimberUProportions() : new List<float>();

        foreach (float v in vProportions)
        {
            float yKeelStern = sheerPlan.cachedKeelSternPoint.y;
            float yRisingStern = sheerPlan.cachedSternRisingEnd.y;
            float yInt = Mathf.Lerp(yKeelStern, yRisingStern, v);
            float zInt = sheerPlan.GetSternProfileZAtY(yInt);
            points.Add(new Vector3(0f, yInt, zInt));
        }

        int startIdx = (vProportions.Count > 0 && uProportions.Count > 0 && Mathf.Abs(uProportions[0]) < 0.0001f) ? 1 : 0;
        for (int i = startIdx; i < uProportions.Count; i++)
        {
            float u = uProportions[i];
            float yRisingStern = sheerPlan.cachedSternRisingEnd.y;
            float ySheerStern = sheerPlan.cachedMainSheerSternIntersect.y;
            float yInt = Mathf.Lerp(yRisingStern, ySheerStern, u);
            float zInt = sheerPlan.GetSternProfileZAtY(yInt);
            points.Add(new Vector3(0f, yInt, zInt));
        }

        int startBulwarkIdx = (uProportions.Count > 0 && bulwarkProportions.Count > 0 && Mathf.Abs(bulwarkProportions[0]) < 0.0001f) ? 1 : 0;
        for (int i = startBulwarkIdx; i < bulwarkProportions.Count; i++)
        {
            float uBulwark = bulwarkProportions[i];
            float ySheerStern = sheerPlan.cachedMainSheerSternIntersect.y;
            float yTopStern = sheerPlan.GetTopTimberYAtZ(sheerPlan.cachedTransomTopPoint.z);
            float yInt = Mathf.Lerp(ySheerStern, yTopStern, uBulwark);
            float zInt = sheerPlan.GetSternProfileZAtY(yInt);
            points.Add(new Vector3(0f, yInt, zInt));
        }

        return points;
    }

    /// <summary>
    /// Evaluates the exact 3D Stem profile intersection points (0, Y, Z) for all active transition lines.
    /// Uses SheerPlan's continuous GetStemProfileZAtY method.
    /// </summary>
    public List<Vector3> GetStemIntersectionPoints()
    {
        List<Vector3> points = new List<Vector3>();
        if (sheerPlan == null) return points;

        sheerPlan.UpdateCachedValues();

        bool drawLower = (transitionMode == TransitionMode.Lower_KeelToRising || transitionMode == TransitionMode.Both_FullStack || transitionMode == TransitionMode.All_FullStack);
        bool drawUpper = (transitionMode == TransitionMode.Upper_RisingToSheer || transitionMode == TransitionMode.Both_FullStack || transitionMode == TransitionMode.All_FullStack);
        bool drawBulwark = (transitionMode == TransitionMode.Bulwark_SheerToTopTimber || transitionMode == TransitionMode.Both_FullStack || transitionMode == TransitionMode.All_FullStack);

        List<float> vProportions = drawLower ? GetLowerVProportions() : new List<float>();
        List<float> uProportions = drawUpper ? GetUpperUProportions() : new List<float>();
        List<float> bulwarkProportions = drawBulwark ? GetTopTimberUProportions() : new List<float>();

        foreach (float v in vProportions)
        {
            float yKeelBow = sheerPlan.cachedKeelBowPoint.y;
            float yRisingBow = sheerPlan.cachedBowRisingEnd.y;
            float yInt = Mathf.Lerp(yKeelBow, yRisingBow, v);
            float zInt = sheerPlan.GetStemProfileZAtY(yInt);
            points.Add(new Vector3(0f, yInt, zInt));
        }

        int startIdx = (vProportions.Count > 0 && uProportions.Count > 0 && Mathf.Abs(uProportions[0]) < 0.0001f) ? 1 : 0;
        for (int i = startIdx; i < uProportions.Count; i++)
        {
            float u = uProportions[i];
            float yRisingBow = sheerPlan.cachedBowRisingEnd.y;
            float ySheerBow = sheerPlan.cachedMainSheerBowIntersect.y;
            float yInt = Mathf.Lerp(yRisingBow, ySheerBow, u);
            float zInt = sheerPlan.GetStemProfileZAtY(yInt);
            points.Add(new Vector3(0f, yInt, zInt));
        }

        int startBulwarkIdx = (uProportions.Count > 0 && bulwarkProportions.Count > 0 && Mathf.Abs(bulwarkProportions[0]) < 0.0001f) ? 1 : 0;
        for (int i = startBulwarkIdx; i < bulwarkProportions.Count; i++)
        {
            float uBulwark = bulwarkProportions[i];
            float ySheerBow = sheerPlan.cachedMainSheerBowIntersect.y;
            float yTopBow = sheerPlan.GetTopTimberYAtZ(sheerPlan.cachedUpperStemTopPoint.z);
            float yInt = Mathf.Lerp(ySheerBow, yTopBow, uBulwark);
            float zInt = sheerPlan.GetStemProfileZAtY(yInt);
            points.Add(new Vector3(0f, yInt, zInt));
        }

        return points;
    }

    public List<float> GetStemIntersectionZPoints()
    {
        List<float> zPoints = new List<float>();
        List<Vector3> pts = GetStemIntersectionPoints();
        foreach (Vector3 p in pts) zPoints.Add(p.z);
        return zPoints;
    }

    public List<float> GetSternIntersectionZPoints()
    {
        List<float> zPoints = new List<float>();
        List<Vector3> pts = GetSternIntersectionPoints();
        foreach (Vector3 p in pts) zPoints.Add(p.z);
        return zPoints;
    }

    public float GetStemIntersectionZForV(float v)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        float yKeelBow = sheerPlan.cachedKeelBowPoint.y;
        float yRisingBow = sheerPlan.cachedBowRisingEnd.y;
        float yLineBow = Mathf.Lerp(yKeelBow, yRisingBow, v);
        return sheerPlan.GetStemProfileZAtY(yLineBow);
    }

    public float GetStemIntersectionZForU(float u)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        float yRisingBow = sheerPlan.cachedBowRisingEnd.y;
        float ySheerBow = sheerPlan.cachedMainSheerBowIntersect.y;
        float yLineBow = Mathf.Lerp(yRisingBow, ySheerBow, u);
        return sheerPlan.GetStemProfileZAtY(yLineBow);
    }

    public float GetSternIntersectionZForV(float v)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        float yKeelStern = sheerPlan.cachedKeelSternPoint.y;
        float yRisingStern = sheerPlan.cachedSternRisingEnd.y;
        float yLineStern = Mathf.Lerp(yKeelStern, yRisingStern, v);
        return sheerPlan.GetSternProfileZAtY(yLineStern);
    }

    public float GetSternIntersectionZForU(float u)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        float yRisingStern = sheerPlan.cachedSternRisingEnd.y;
        float ySheerStern = sheerPlan.cachedMainSheerSternIntersect.y;
        float yLineStern = Mathf.Lerp(yRisingStern, ySheerStern, u);
        return sheerPlan.GetSternProfileZAtY(yLineStern);
    }

    public float GetSternIntersectionZForBulwark(float u)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        float ySheerStern = sheerPlan.cachedMainSheerSternIntersect.y;
        float yTopStern = sheerPlan.GetTopTimberYAtZ(sheerPlan.cachedTransomTopPoint.z);
        float yLineStern = Mathf.Lerp(ySheerStern, yTopStern, u);
        return sheerPlan.GetSternProfileZAtY(yLineStern);
    }

    public float GetStemIntersectionZForBulwark(float u)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        float ySheerBow = sheerPlan.cachedMainSheerBowIntersect.y;
        float yTopBow = sheerPlan.GetTopTimberYAtZ(sheerPlan.cachedUpperStemTopPoint.z);
        float yLineBow = Mathf.Lerp(ySheerBow, yTopBow, u);
        return sheerPlan.GetStemProfileZAtY(yLineBow);
    }

    /// <summary>
    /// Arc-length samples the Body Plan's Lower Mould curve at intervals of pointInterval (default 0.25)
    /// and extracts the Y-axis positions of those sampled points.
    /// </summary>
    public List<float> GetBodyPlanLowerMouldYPoints()
    {
        List<float> yPositions = new List<float>();

        BodyPlan bodyPlan = (sheerPlan != null) ? sheerPlan.bodyPlan : null;
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (bodyPlan == null) return yPositions;

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

        if (totalArcLen <= 0.001f) return yPositions;

        float interval = Mathf.Max(0.01f, pointInterval);
        int numSegments = Mathf.Max(1, Mathf.RoundToInt(totalArcLen / interval));

        for (int j = 0; j <= numSegments; j++)
        {
            float targetDist = totalArcLen * ((float)j / numSegments);
            float y = GetYAtArcDistance(polyline, cumDist, targetDist);
            yPositions.Add(y);
        }

        return yPositions;
    }

    /// <summary>
    /// Arc-length samples the Body Plan's Upper Mould curve up to Main Sheer Line height
    /// at intervals of pointInterval (default 0.25) and extracts the Y-axis positions of those sampled points.
    /// </summary>
    public List<float> GetBodyPlanUpperMouldYPoints()
    {
        List<float> yPositions = new List<float>();

        BodyPlan bodyPlan = (sheerPlan != null) ? sheerPlan.bodyPlan : null;
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (bodyPlan == null) return yPositions;

        bodyPlan.UpdateCachedCurves();

        float midRisingY = GetExtendedRisingLineYAtZ(0f);
        float midSheerY = GetPureMainSheerYAtZ(0f);

        if (midSheerY <= midRisingY + 0.001f) return yPositions;

        float maxT = bodyPlan.GetUpperMouldTAtY(midSheerY);
        if (maxT <= 0.0001f) return yPositions;

        const int samples = 200;
        List<Vector3> polyline = new List<Vector3>();
        List<float> cumDist = new List<float> { 0f };

        Vector3 prevPt = bodyPlan.EvaluateUpperMouldAtT(0f);
        polyline.Add(prevPt);

        float totalArcLen = 0f;
        for (int i = 1; i <= samples; i++)
        {
            float t = maxT * ((float)i / samples);
            Vector3 currPt = bodyPlan.EvaluateUpperMouldAtT(t);
            totalArcLen += Vector3.Distance(prevPt, currPt);
            polyline.Add(currPt);
            cumDist.Add(totalArcLen);
            prevPt = currPt;
        }

        if (totalArcLen <= 0.001f) return yPositions;

        float interval = Mathf.Max(0.01f, pointInterval);
        int numSegments = Mathf.Max(1, Mathf.RoundToInt(totalArcLen / interval));

        for (int j = 0; j <= numSegments; j++)
        {
            float targetDist = totalArcLen * ((float)j / numSegments);
            float y = GetYAtArcDistance(polyline, cumDist, targetDist);
            yPositions.Add(y);
        }

        return yPositions;
    }

    private float GetYAtArcDistance(List<Vector3> polyline, List<float> cumDist, float targetDist)
    {
        int count = cumDist.Count;
        if (targetDist <= 0f) return polyline[0].y;
        if (targetDist >= cumDist[count - 1]) return polyline[count - 1].y;

        for (int i = 0; i < count - 1; i++)
        {
            if (targetDist >= cumDist[i] && targetDist <= cumDist[i + 1])
            {
                float segmentLen = cumDist[i + 1] - cumDist[i];
                float lerpT = (segmentLen > 0.0001f) ? ((targetDist - cumDist[i]) / segmentLen) : 0f;
                return Mathf.Lerp(polyline[i].y, polyline[i + 1].y, lerpT);
            }
        }

        return polyline[count - 1].y;
    }

    [ContextMenu("Rebuild Cache")]
    public void ForceRebuildCache()
    {
        MarkDirty();
    }

    private void RebuildCache()
    {
        if (sheerPlan == null) return;

        sheerPlan.UpdateCachedValues();

        m_CachedLowerLines = GetLowerTransitionLines();
        m_CachedUpperLines = GetUpperTransitionLines();
        m_CachedBulwarkLines = GetBulwarkTransitionLines();
        m_CachedMidshipYPoints = GetBodyPlanLowerMouldYPoints();
        m_CachedMidshipYPoints.AddRange(GetBodyPlanUpperMouldYPoints());
        m_CachedMidshipYPoints.AddRange(GetBodyPlanBulwarkMouldYPoints());

        m_IsCacheDirty = false;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || !showLines) return;

        if (m_IsCacheDirty)
        {
            RebuildCache();
        }

        if (showMidshipYPoints && m_CachedMidshipYPoints != null)
        {
            Gizmos.color = pointColor;
            foreach (float y in m_CachedMidshipYPoints)
            {
                Gizmos.DrawSphere(new Vector3(0f, y, 0f), pointGizmoRadius);
            }
        }

        if (showStemIntersectionDashedLines || showStemIntersectionPoints)
        {
            List<Vector3> stemPoints = GetStemIntersectionPoints();
            if (stemPoints != null)
            {
                foreach (Vector3 topPt in stemPoints)
                {
                    Vector3 bottomPt = new Vector3(0f, 0f, topPt.z);

                    if (showStemIntersectionDashedLines)
                    {
                        DrawDashedLine(bottomPt, topPt, dashLength, stemDashedLineColor);
                    }
                    if (showStemIntersectionPoints)
                    {
                        Gizmos.color = stemPointColor;
                        Gizmos.DrawSphere(topPt, stemPointGizmoRadius);
                    }
                }
            }
        }

        if (showSternIntersectionDashedLines || showSternIntersectionPoints)
        {
            List<Vector3> sternPoints = GetSternIntersectionPoints();
            if (sternPoints != null)
            {
                foreach (Vector3 topPt in sternPoints)
                {
                    Vector3 bottomPt = new Vector3(0f, 0f, topPt.z);

                    if (showSternIntersectionDashedLines)
                    {
                        DrawDashedLine(bottomPt, topPt, dashLength, sternDashedLineColor);
                    }
                    if (showSternIntersectionPoints)
                    {
                        Gizmos.color = sternPointColor;
                        Gizmos.DrawSphere(topPt, sternPointGizmoRadius);
                    }
                }
            }
        }

        bool drawLower = (transitionMode == TransitionMode.Lower_KeelToRising || transitionMode == TransitionMode.Both_FullStack || transitionMode == TransitionMode.All_FullStack);
        bool drawUpper = (transitionMode == TransitionMode.Upper_RisingToSheer || transitionMode == TransitionMode.Both_FullStack || transitionMode == TransitionMode.All_FullStack);
        bool drawBulwark = (transitionMode == TransitionMode.Bulwark_SheerToTopTimber || transitionMode == TransitionMode.Both_FullStack || transitionMode == TransitionMode.All_FullStack);

        if (drawLower && m_CachedLowerLines != null)
        {
            int count = m_CachedLowerLines.Count;
            for (int k = 0; k < count; k++)
            {
                float v = (count > 1) ? (float)k / (count - 1) : 0f;
                Color c = Color.Lerp(keelColor, risingLineColor, v);

                if (k == 0)
                {
                    if (!showKeelLine) continue;
                    c = keelColor;
                }
                else if (k == count - 1)
                {
                    if (!showRisingLine && (drawUpper || drawBulwark)) continue;
                    c = risingLineColor;
                }

                DrawCurve(m_CachedLowerLines[k], c);
            }
        }

        if (drawUpper && m_CachedUpperLines != null)
        {
            int count = m_CachedUpperLines.Count;
            for (int k = 0; k < count; k++)
            {
                float u = (count > 1) ? (float)k / (count - 1) : 0f;
                Color c = Color.Lerp(risingLineColor, sheerLineColor, u);

                if (k == 0)
                {
                    if (!showRisingLine) continue;
                    c = risingLineColor;
                }
                else if (k == count - 1)
                {
                    if (!showSheerLine && drawBulwark) continue;
                    c = sheerLineColor;
                }

                DrawCurve(m_CachedUpperLines[k], c);
            }
        }

        if (drawBulwark && m_CachedBulwarkLines != null)
        {
            int count = m_CachedBulwarkLines.Count;
            for (int k = 0; k < count; k++)
            {
                float u = (count > 1) ? (float)k / (count - 1) : 0f;
                Color c = Color.Lerp(sheerLineColor, topTimberLineColor, u);

                if (k == 0)
                {
                    if (!showSheerLine) continue;
                    c = sheerLineColor;
                }
                else if (k == count - 1)
                {
                    if (!showTopTimberLine) continue;
                    c = topTimberLineColor;
                }

                DrawCurve(m_CachedBulwarkLines[k], c);
            }
        }
    }

    private void DrawDashedLine(Vector3 from, Vector3 to, float dashLen, Color color)
    {
        Gizmos.color = color;
        float dist = Vector3.Distance(from, to);
        if (dist <= 0.001f) return;

        Vector3 dir = (to - from).normalized;
        float currentDist = 0f;
        bool draw = true;

        while (currentDist < dist)
        {
            float nextDist = Mathf.Min(currentDist + dashLen, dist);
            if (draw)
            {
                Gizmos.DrawLine(from + dir * currentDist, from + dir * nextDist);
            }
            draw = !draw;
            currentDist = nextDist;
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
