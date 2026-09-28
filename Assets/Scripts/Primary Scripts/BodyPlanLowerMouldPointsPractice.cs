using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Practice script for evaluating the Body Plan Mould curve at Midships (Z = 0)
/// and placing points at target arc-length intervals (default 0.25) from the
/// Top Line's Midship Y position down to the Keel point.
/// </summary>
[ExecuteInEditMode]
public class BodyPlanLowerMouldPointsPractice : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Reference to BodyPlan component.")]
    public BodyPlan bodyPlan;

    [Tooltip("Reference to SheerPlan component.")]
    public SheerPlan sheerPlan;

    [Header("Sampling Settings")]
    [Tooltip("Target arc length interval for placing points (default 0.25).")]
    public float pointInterval = 0.25f;

    [Header("Visualization Settings")]
    [Tooltip("Display sampled points in Scene View.")]
    public bool showPoints = true;

    [Tooltip("Display the sampled mould curve overlay in Scene View.")]
    public bool showMouldCurve = true;

    [Tooltip("Show points on Starboard (+X) side.")]
    public bool showStarboard = true;

    [Tooltip("Show points on Port (-X) side.")]
    public bool showPort = true;

    [Tooltip("Radius of Gizmo spheres drawn for sampled points.")]
    public float pointGizmoRadius = 0.12f;

    [Tooltip("Color of sampled points.")]
    public Color pointColor = Color.magenta;

    [Tooltip("Color of the Mould curve overlay.")]
    public Color curveColor = new Color(0.3f, 0.9f, 0.6f, 0.8f);

    private List<Vector3> m_CachedStbdPoints = new List<Vector3>();
    private List<Vector3> m_CachedPortPoints = new List<Vector3>();
    private List<Vector3> m_CachedPolyline = new List<Vector3>();
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
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (bodyPlan != null && sheerPlan == null) sheerPlan = bodyPlan.sheerPlan;
    }

    /// <summary>
    /// Constructs a high-resolution polyline along the Master Frame Mould curve at Midships (Z = 0)
    /// from the Top Line Midship Y position down to the Keel point.
    /// </summary>
    public List<Vector3> EvaluateMouldPolylineFromSheerToKeel(int totalSamples = 300)
    {
        List<Vector3> polyline = new List<Vector3>();

        FindReferences();
        if (bodyPlan == null) return polyline;

        bodyPlan.UpdateCachedCurves();

        SheerPlan sheer = (sheerPlan != null) ? sheerPlan : bodyPlan.sheerPlan;
        if (sheer == null) sheer = FindFirstObjectByType<SheerPlan>();

        float midKeelY = (sheer != null) ? sheer.GetKeelYAtZ(0f) : 0f;
        float midTopY = (sheer != null) ? sheer.GetPureSheerYAtZ(0f) : bodyPlan.cachedU3.y;

        List<Vector3> lowerPoly = BuildMouldSegment(bodyPlan, midKeelY, midTopY);
        return lowerPoly;
    }

    /// <summary>
    /// Arc-length samples the Master Frame Mould curve at target intervals (default 0.25)
    /// divided into 3 regions (Lower, Upper, Bulwark) matching MainSheerKeelLineTransitionPractice.
    /// Returns 3D point positions on the specified side (+X for Starboard, -X for Port).
    /// </summary>
    public List<Vector3> GetSampledPoints(float sideSign = 1f)
    {
        List<Vector3> sampledPoints = new List<Vector3>();

        FindReferences();
        if (bodyPlan == null) return sampledPoints;

        bodyPlan.UpdateCachedCurves();

        SheerPlan sheer = (sheerPlan != null) ? sheerPlan : bodyPlan.sheerPlan;
        if (sheer == null) sheer = FindFirstObjectByType<SheerPlan>();

        float midKeelY = (sheer != null) ? sheer.GetKeelYAtZ(0f) : 0f;
        float midRisingY = (sheer != null) ? sheer.GetRisingLineYAtZ(0f) : bodyPlan.cachedLR.y;
        float midSheerY = (sheer != null) ? sheer.GetMainSheerYAtZ(0f) : bodyPlan.cachedU3.y;
        float midTopY = (sheer != null) ? sheer.GetPureSheerYAtZ(0f) : (midSheerY + 1f);

        float interval = Mathf.Max(0.01f, pointInterval);

        // 1. BULWARK REGION (Main Sheer Line -> Top Line)
        List<Vector3> bulwarkPoly = BuildMouldSegment(bodyPlan, midSheerY, midTopY);
        List<Vector3> bulwarkPts = SampleSegmentByArcLength(bulwarkPoly, interval);

        // 2. UPPER REGION (Rising Line -> Main Sheer Line)
        List<Vector3> upperPoly = BuildMouldSegment(bodyPlan, midRisingY, midSheerY);
        List<Vector3> upperPts = SampleSegmentByArcLength(upperPoly, interval);

        // 3. LOWER REGION (Keel -> Rising Line)
        List<Vector3> lowerPoly = BuildMouldSegment(bodyPlan, midKeelY, midRisingY);
        List<Vector3> lowerPts = SampleSegmentByArcLength(lowerPoly, interval);

        // Combine points from Top Line down to Keel Line (avoiding duplicate boundary points)
        for (int i = bulwarkPts.Count - 1; i >= 0; i--)
        {
            Vector3 pt = bulwarkPts[i];
            sampledPoints.Add(new Vector3(sideSign * pt.x, pt.y, 0f));
        }

        for (int i = upperPts.Count - 2; i >= 0; i--)
        {
            Vector3 pt = upperPts[i];
            sampledPoints.Add(new Vector3(sideSign * pt.x, pt.y, 0f));
        }

        for (int i = lowerPts.Count - 2; i >= 0; i--)
        {
            Vector3 pt = lowerPts[i];
            sampledPoints.Add(new Vector3(sideSign * pt.x, pt.y, 0f));
        }

        return sampledPoints;
    }

    private List<Vector3> BuildMouldSegment(BodyPlan body, float minY, float maxY)
    {
        List<Vector3> polyline = new List<Vector3>();
        if (body == null || maxY <= minY + 0.0001f) return polyline;

        const int samples = 100;
        Vector3 hobPt = body.EvaluateLowerMouldAtT(1.0f);

        // If segment is entirely on Upper Mould
        if (minY >= hobPt.y - 0.001f)
        {
            float minT = body.GetUpperMouldTAtY(minY);
            float maxT = body.GetUpperMouldTAtY(maxY);
            for (int i = 0; i <= samples; i++)
            {
                float t = Mathf.Lerp(minT, maxT, (float)i / samples);
                polyline.Add(body.EvaluateUpperMouldAtT(t));
            }
        }
        // If segment spans across Lower and Upper Moulds
        else if (maxY > hobPt.y + 0.001f)
        {
            float minLowerT = body.GetLowerMouldTAtY(minY);
            for (int i = 0; i <= samples / 2; i++)
            {
                float t = Mathf.Lerp(minLowerT, 1.0f, (float)i / (samples / 2));
                polyline.Add(body.EvaluateLowerMouldAtT(t));
            }

            float maxUpperT = body.GetUpperMouldTAtY(maxY);
            for (int i = 1; i <= samples / 2; i++)
            {
                float t = Mathf.Lerp(0f, maxUpperT, (float)i / (samples / 2));
                polyline.Add(body.EvaluateUpperMouldAtT(t));
            }
        }
        // If segment is entirely on Lower Mould
        else
        {
            float minT = body.GetLowerMouldTAtY(minY);
            float maxT = body.GetLowerMouldTAtY(maxY);
            for (int i = 0; i <= samples; i++)
            {
                float t = Mathf.Lerp(minT, maxT, (float)i / samples);
                polyline.Add(body.EvaluateLowerMouldAtT(t));
            }
        }

        return polyline;
    }

    private List<Vector3> SampleSegmentByArcLength(List<Vector3> polyline, float interval)
    {
        List<Vector3> pts = new List<Vector3>();
        if (polyline == null || polyline.Count < 2) return pts;

        int count = polyline.Count;
        List<float> cumDist = new List<float> { 0f };
        float totalArcLen = 0f;

        for (int i = 0; i < count - 1; i++)
        {
            float dist = Vector3.Distance(polyline[i], polyline[i + 1]);
            totalArcLen += dist;
            cumDist.Add(totalArcLen);
        }

        if (totalArcLen <= 0.001f)
        {
            pts.Add(polyline[0]);
            return pts;
        }

        int numSegments = Mathf.Max(1, Mathf.RoundToInt(totalArcLen / interval));
        for (int j = 0; j <= numSegments; j++)
        {
            float targetDist = totalArcLen * ((float)j / numSegments);
            pts.Add(GetPointAtArcDistance(polyline, cumDist, targetDist));
        }

        return pts;
    }

    /// <summary>
    /// Returns the Y-axis positions of all sampled points from Top Line Midship Y down to Keel.
    /// </summary>
    public List<float> GetSampledYPositions()
    {
        List<float> yList = new List<float>();
        List<Vector3> pts = GetSampledPoints(1f);
        foreach (Vector3 p in pts)
        {
            yList.Add(p.y);
        }
        return yList;
    }

    /// <summary>
    /// Returns the X-axis half-width positions of all sampled points from Top Line Midship Y down to Keel.
    /// </summary>
    public List<float> GetSampledXPositions()
    {
        List<float> xList = new List<float>();
        List<Vector3> pts = GetSampledPoints(1f);
        foreach (Vector3 p in pts)
        {
            xList.Add(p.x);
        }
        return xList;
    }

    private Vector3 GetPointAtArcDistance(List<Vector3> polyline, List<float> cumDist, float targetDist)
    {
        int count = cumDist.Count;
        if (targetDist <= 0f) return polyline[0];
        if (targetDist >= cumDist[count - 1]) return polyline[count - 1];

        for (int i = 0; i < count - 1; i++)
        {
            if (targetDist >= cumDist[i] && targetDist <= cumDist[i + 1])
            {
                float segmentLen = cumDist[i + 1] - cumDist[i];
                float lerpT = (segmentLen > 0.0001f) ? ((targetDist - cumDist[i]) / segmentLen) : 0f;
                return Vector3.Lerp(polyline[i], polyline[i + 1], lerpT);
            }
        }

        return polyline[count - 1];
    }

    [ContextMenu("Rebuild Cache")]
    public void ForceRebuildCache()
    {
        MarkDirty();
    }

    private void RebuildCache()
    {
        FindReferences();
        if (bodyPlan == null) return;

        m_CachedPolyline = EvaluateMouldPolylineFromSheerToKeel(300);
        m_CachedStbdPoints = GetSampledPoints(1f);
        m_CachedPortPoints = GetSampledPoints(-1f);

        m_IsCacheDirty = false;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (bodyPlan == null) return;

        if (m_IsCacheDirty)
        {
            RebuildCache();
        }

        if (showMouldCurve && m_CachedPolyline != null && m_CachedPolyline.Count > 1)
        {
            Gizmos.color = curveColor;
            for (int i = 0; i < m_CachedPolyline.Count - 1; i++)
            {
                if (showStarboard)
                {
                    Vector3 p1 = new Vector3(m_CachedPolyline[i].x, m_CachedPolyline[i].y, 0f);
                    Vector3 p2 = new Vector3(m_CachedPolyline[i + 1].x, m_CachedPolyline[i + 1].y, 0f);
                    Gizmos.DrawLine(p1, p2);
                }
                if (showPort)
                {
                    Vector3 p1 = new Vector3(-m_CachedPolyline[i].x, m_CachedPolyline[i].y, 0f);
                    Vector3 p2 = new Vector3(-m_CachedPolyline[i + 1].x, m_CachedPolyline[i + 1].y, 0f);
                    Gizmos.DrawLine(p1, p2);
                }
            }
        }

        if (showPoints)
        {
            Gizmos.color = pointColor;
            if (showStarboard && m_CachedStbdPoints != null)
            {
                foreach (Vector3 pt in m_CachedStbdPoints)
                {
                    Gizmos.DrawSphere(pt, pointGizmoRadius);
                }
            }
            if (showPort && m_CachedPortPoints != null)
            {
                foreach (Vector3 pt in m_CachedPortPoints)
                {
                    Gizmos.DrawSphere(pt, pointGizmoRadius);
                }
            }
        }
    }
}
