using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Master Strakes & Ribbands Model synthesizing all 3 primary plan systems:
/// 1. Sheer Plan (Heights Y(Z) and Stem/Stern profile boundaries)
/// 2. Breadth Plan (Transverse widths X(Z) and Plan View breadth shapes)
/// 3. Body Plan (Master Frame Mould curves X(Y) at Midships Z=0)
/// 
/// Spans the complete hull height from the Keel Line up to the Top Timber Line:
/// - Lower Region: Keel Line -> Extended Rising Line / Floor Breadth
/// - Middle Region: Extended Rising Line -> Main Sheer Line / Max Breadth (HOB)
/// - Upper Region: Main Sheer Line -> Top Timber Line / Top Timber Breadth
/// </summary>
[ExecuteInEditMode]
public class MasterStrakesRibbandsModel : MonoBehaviour
{
    public enum StackRegion
    {
        Lower_KeelToRising,
        Middle_RisingToMainSheer,
        Upper_MainSheerToTopTimber,
        Full_HullStack
    }

    public enum SpacingMode
    {
        Uniform_Interval,
        BodyPlan_Mould_Arc
    }

    [Header("Component References")]
    [Tooltip("Reference to SheerPlan component.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to BreadthPlan component.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to BodyPlan component.")]
    public BodyPlan bodyPlan;

    [Header("Region & Spacing Settings")]
    [Tooltip("Which hull region to evaluate and display.")]
    public StackRegion stackRegion = StackRegion.Full_HullStack;

    [Tooltip("How strake/ribband heights are spaced: Uniform linear interval or Arc-length sampled from Body Plan Mould curves.")]
    public SpacingMode spacingMode = SpacingMode.BodyPlan_Mould_Arc;

    [Tooltip("Target arc length or vertical interval between strakes/ribbands at Midships (default 0.25).")]
    public float pointInterval = 0.25f;

    [Tooltip("Longitudinal resolution along each 3D ribband from Stern to Bow.")]
    [Range(10, 300)]
    public int resolution = 100;

    [Header("Visualization Settings")]
    [Tooltip("Display 3D ribband space curves in Scene View.")]
    public bool show3DRibbands = true;

    [Tooltip("Show visualization on Starboard (+X) side.")]
    public bool showStarboard = true;

    [Tooltip("Show visualization on Port (-X) side.")]
    public bool showPort = true;

    [Tooltip("Draw vertical dashed lines at the exact intersection of each ribband with the Stem and Stern profiles.")]
    public bool showProfileIntersectionDashedLines = true;

    [Tooltip("Display Gizmo points on the Midship Y-axis / Mould curve.")]
    public bool showMidshipBodyPlanPoints = true;

    [Tooltip("Show sphere markers at the Bow and Stern endpoints of each 3D ribband.")]
    public bool showEndpoints = true;

    [Tooltip("Radius of Gizmo spheres drawn at ribband endpoints.")]
    public float endpointGizmoRadius = 0.10f;

    [Tooltip("Radius of Gizmo spheres drawn for Midship Body Plan points.")]
    public float pointGizmoRadius = 0.12f;

    [Tooltip("Length of each dash segment for profile intersection lines.")]
    public float dashLength = 0.15f;

    [Header("Color System")]
    public Color keelColor = new Color(0.2f, 0.8f, 1f, 0.9f);
    public Color lowerRegionColor = new Color(0.3f, 0.9f, 0.6f, 0.95f);
    public Color risingLineColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    public Color middleRegionColor = new Color(0.95f, 0.55f, 0.15f, 0.95f);
    public Color mainSheerColor = Color.yellow;
    public Color upperRegionColor = new Color(0.9f, 0.4f, 0.8f, 0.95f);
    public Color topTimberColor = new Color(0.95f, 0.25f, 0.35f, 0.95f);

    public Color dashedLineColor = Color.cyan;
    public Color midshipPointColor = Color.magenta;

    private List<List<Vector3>> m_CachedStbdLowerRibbands = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortLowerRibbands = new List<List<Vector3>>();

    private List<List<Vector3>> m_CachedStbdMiddleRibbands = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortMiddleRibbands = new List<List<Vector3>>();

    private List<List<Vector3>> m_CachedStbdUpperRibbands = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortUpperRibbands = new List<List<Vector3>>();

    private List<Vector3> m_CachedMidshipPoints = new List<Vector3>();
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
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (sheerPlan != null)
        {
            if (breadthPlan == null) breadthPlan = sheerPlan.breadthPlan;
            if (bodyPlan == null) bodyPlan = sheerPlan.bodyPlan;
        }
    }

    // --- 2D SHEER PLAN QUERIES ---

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

    public float GetPureMainSheerYAtZ(float z)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        return sheerPlan.GetMainSheerYAtZ(z);
    }

    public float GetPureTopTimberYAtZ(float z)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        return sheerPlan.GetPureSheerYAtZ(z);
    }

    // --- PROPORTIONS EVALUATION ---

    public List<float> GetLowerProportions()
    {
        List<float> vProportions = new List<float>();
        if (sheerPlan == null) return vProportions;
        sheerPlan.UpdateCachedValues();

        float midKeelY = GetPureKeelYAtZ(0f);
        float midRisingY = GetExtendedRisingLineYAtZ(0f);
        float lowerSpan = Mathf.Max(0.001f, midRisingY - midKeelY);

        if (spacingMode == SpacingMode.BodyPlan_Mould_Arc && bodyPlan != null)
        {
            bodyPlan.UpdateCachedCurves();
            const int samples = 100;
            Vector3 prevPt = bodyPlan.EvaluateLowerMouldAtT(0f);
            float totalArc = 0f;
            List<Vector3> poly = new List<Vector3> { prevPt };
            List<float> dists = new List<float> { 0f };

            for (int i = 1; i <= samples; i++)
            {
                float t = 0.5f * ((float)i / samples); // Keel -> Rising Line
                Vector3 curr = bodyPlan.EvaluateLowerMouldAtT(t);
                totalArc += Vector3.Distance(prevPt, curr);
                poly.Add(curr);
                dists.Add(totalArc);
                prevPt = curr;
            }

            float interval = Mathf.Max(0.01f, pointInterval);
            int n = Mathf.Max(1, Mathf.RoundToInt(totalArc / interval));
            for (int j = 0; j <= n; j++)
            {
                float targetDist = totalArc * ((float)j / n);
                Vector3 pt = GetPolylinePointAtArcDistance(poly, dists, targetDist);
                float v = Mathf.Clamp01((pt.y - midKeelY) / lowerSpan);
                vProportions.Add(v);
            }
        }
        else
        {
            float interval = Mathf.Max(0.01f, pointInterval);
            int n = Mathf.Max(1, Mathf.RoundToInt(lowerSpan / interval));
            for (int k = 0; k <= n; k++)
            {
                vProportions.Add((float)k / n);
            }
        }
        return vProportions;
    }

    public List<float> GetMiddleProportions()
    {
        List<float> mProportions = new List<float>();
        if (sheerPlan == null) return mProportions;
        sheerPlan.UpdateCachedValues();

        float midRisingY = GetExtendedRisingLineYAtZ(0f);
        float midSheerY = GetPureMainSheerYAtZ(0f);
        float midSpan = Mathf.Max(0.001f, midSheerY - midRisingY);

        if (spacingMode == SpacingMode.BodyPlan_Mould_Arc && bodyPlan != null)
        {
            bodyPlan.UpdateCachedCurves();
            const int samples = 100;
            Vector3 prevPt = bodyPlan.EvaluateLowerMouldAtT(0.5f);
            float totalArc = 0f;
            List<Vector3> poly = new List<Vector3> { prevPt };
            List<float> dists = new List<float> { 0f };

            // Lower Mould segment 2 (Rising Line -> HOB)
            for (int i = 1; i <= samples; i++)
            {
                float t = 0.5f + 0.5f * ((float)i / samples);
                Vector3 curr = bodyPlan.EvaluateLowerMouldAtT(t);
                totalArc += Vector3.Distance(prevPt, curr);
                poly.Add(curr);
                dists.Add(totalArc);
                prevPt = curr;
            }

            // Upper Mould segment (HOB -> Main Sheer)
            float maxUpperT = bodyPlan.GetUpperMouldTAtY(midSheerY);
            if (maxUpperT > 0.001f)
            {
                for (int i = 1; i <= samples; i++)
                {
                    float t = maxUpperT * ((float)i / samples);
                    Vector3 curr = bodyPlan.EvaluateUpperMouldAtT(t);
                    totalArc += Vector3.Distance(prevPt, curr);
                    poly.Add(curr);
                    dists.Add(totalArc);
                    prevPt = curr;
                }
            }

            float interval = Mathf.Max(0.01f, pointInterval);
            int n = Mathf.Max(1, Mathf.RoundToInt(totalArc / interval));
            for (int j = 0; j <= n; j++)
            {
                float targetDist = totalArc * ((float)j / n);
                Vector3 pt = GetPolylinePointAtArcDistance(poly, dists, targetDist);
                float m = Mathf.Clamp01((pt.y - midRisingY) / midSpan);
                mProportions.Add(m);
            }
        }
        else
        {
            float interval = Mathf.Max(0.01f, pointInterval);
            int n = Mathf.Max(1, Mathf.RoundToInt(midSpan / interval));
            for (int k = 0; k <= n; k++)
            {
                mProportions.Add((float)k / n);
            }
        }
        return mProportions;
    }

    public List<float> GetUpperProportions()
    {
        List<float> uProportions = new List<float>();
        if (sheerPlan == null) return uProportions;
        sheerPlan.UpdateCachedValues();

        float midSheerY = GetPureMainSheerYAtZ(0f);
        float midTopTimberY = GetPureTopTimberYAtZ(0f);
        float upperSpan = Mathf.Max(0.001f, midTopTimberY - midSheerY);

        if (spacingMode == SpacingMode.BodyPlan_Mould_Arc && bodyPlan != null)
        {
            bodyPlan.UpdateCachedCurves();
            float minUpperT = bodyPlan.GetUpperMouldTAtY(midSheerY);
            float maxUpperT = bodyPlan.GetUpperMouldTAtY(midTopTimberY);
            if (maxUpperT < minUpperT) maxUpperT = 1.0f;

            const int samples = 100;
            Vector3 prevPt = bodyPlan.EvaluateUpperMouldAtT(minUpperT);
            float totalArc = 0f;
            List<Vector3> poly = new List<Vector3> { prevPt };
            List<float> dists = new List<float> { 0f };

            for (int i = 1; i <= samples; i++)
            {
                float t = Mathf.Lerp(minUpperT, maxUpperT, (float)i / samples);
                Vector3 curr = bodyPlan.EvaluateUpperMouldAtT(t);
                totalArc += Vector3.Distance(prevPt, curr);
                poly.Add(curr);
                dists.Add(totalArc);
                prevPt = curr;
            }

            float interval = Mathf.Max(0.01f, pointInterval);
            int n = Mathf.Max(1, Mathf.RoundToInt(totalArc / interval));
            for (int j = 0; j <= n; j++)
            {
                float targetDist = totalArc * ((float)j / n);
                Vector3 pt = GetPolylinePointAtArcDistance(poly, dists, targetDist);
                float u = Mathf.Clamp01((pt.y - midSheerY) / upperSpan);
                uProportions.Add(u);
            }
        }
        else
        {
            float interval = Mathf.Max(0.01f, pointInterval);
            int n = Mathf.Max(1, Mathf.RoundToInt(upperSpan / interval));
            for (int k = 0; k <= n; k++)
            {
                uProportions.Add((float)k / n);
            }
        }
        return uProportions;
    }

    private Vector3 GetPolylinePointAtArcDistance(List<Vector3> polyline, List<float> cumDist, float targetDist)
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

    // --- STEM & STERN BISECTION INTERSECTION SOLVERS ---

    public float GetStemIntersectionZForLower(float v)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        float minZ = sheerPlan.cachedKeelBowPoint.z;
        float maxZ = sheerPlan.cachedMainSheerBowIntersect.z;
        if (maxZ <= minZ + 0.001f) return maxZ;

        for (int step = 0; step < 25; step++)
        {
            float midZ = (minZ + maxZ) * 0.5f;
            float keelY = GetPureKeelYAtZ(midZ);
            float risingY = GetExtendedRisingLineYAtZ(midZ);
            float lineY = Mathf.Lerp(keelY, risingY, v);
            float stemY = sheerPlan.GetStemProfileYAtZ(midZ);

            if (lineY > stemY) minZ = midZ;
            else maxZ = midZ;
        }
        return (minZ + maxZ) * 0.5f;
    }

    public float GetSternIntersectionZForLower(float v)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        float zKeelStern = sheerPlan.cachedKeelSternPoint.z;
        float zSheerStern = sheerPlan.cachedMainSheerSternIntersect.z;
        float minZ = Mathf.Min(zKeelStern, zSheerStern);
        float maxZ = Mathf.Max(zKeelStern, zSheerStern);
        if (maxZ <= minZ + 0.001f) return maxZ;

        float lowZ = minZ, highZ = maxZ;
        for (int step = 0; step < 25; step++)
        {
            float midZ = (lowZ + highZ) * 0.5f;
            float keelY = GetPureKeelYAtZ(midZ);
            float risingY = GetExtendedRisingLineYAtZ(midZ);
            float lineY = Mathf.Lerp(keelY, risingY, v);
            float sternY = sheerPlan.GetSternProfileYAtZ(midZ);

            if (lineY > sternY) highZ = midZ;
            else lowZ = midZ;
        }
        return (lowZ + highZ) * 0.5f;
    }

    public float GetStemIntersectionZForMiddle(float m)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        float minZ = sheerPlan.cachedKeelBowPoint.z;
        float maxZ = sheerPlan.cachedMainSheerBowIntersect.z;
        if (maxZ <= minZ + 0.001f) return maxZ;

        for (int step = 0; step < 25; step++)
        {
            float midZ = (minZ + maxZ) * 0.5f;
            float risingY = GetExtendedRisingLineYAtZ(midZ);
            float sheerY = GetPureMainSheerYAtZ(midZ);
            float lineY = Mathf.Lerp(risingY, sheerY, m);
            float stemY = sheerPlan.GetStemProfileYAtZ(midZ);

            if (lineY > stemY) minZ = midZ;
            else maxZ = midZ;
        }
        return (minZ + maxZ) * 0.5f;
    }

    public float GetSternIntersectionZForMiddle(float m)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        float zKeelStern = sheerPlan.cachedKeelSternPoint.z;
        float zSheerStern = sheerPlan.cachedMainSheerSternIntersect.z;
        float minZ = Mathf.Min(zKeelStern, zSheerStern);
        float maxZ = Mathf.Max(zKeelStern, zSheerStern);
        if (maxZ <= minZ + 0.001f) return maxZ;

        float lowZ = minZ, highZ = maxZ;
        for (int step = 0; step < 25; step++)
        {
            float midZ = (lowZ + highZ) * 0.5f;
            float risingY = GetExtendedRisingLineYAtZ(midZ);
            float sheerY = GetPureMainSheerYAtZ(midZ);
            float lineY = Mathf.Lerp(risingY, sheerY, m);
            float sternY = sheerPlan.GetSternProfileYAtZ(midZ);

            if (lineY > sternY) highZ = midZ;
            else lowZ = midZ;
        }
        return (lowZ + highZ) * 0.5f;
    }

    public float GetStemIntersectionZForUpper(float u)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        float minZ = sheerPlan.cachedMainSheerBowIntersect.z;
        float maxZ = sheerPlan.cachedTopTimberBowIntersect.z;
        if (maxZ <= minZ + 0.001f) return maxZ;

        for (int step = 0; step < 25; step++)
        {
            float midZ = (minZ + maxZ) * 0.5f;
            float sheerY = GetPureMainSheerYAtZ(midZ);
            float topY = GetPureTopTimberYAtZ(midZ);
            float lineY = Mathf.Lerp(sheerY, topY, u);
            float stemY = sheerPlan.GetStemProfileYAtZ(midZ);

            if (lineY > stemY) minZ = midZ;
            else maxZ = midZ;
        }
        return (minZ + maxZ) * 0.5f;
    }

    public float GetSternIntersectionZForUpper(float u)
    {
        if (sheerPlan == null) return 0f;
        sheerPlan.UpdateCachedValues();
        float zSheerStern = sheerPlan.cachedMainSheerSternIntersect.z;
        float zTopStern = sheerPlan.cachedTopTimberSternIntersect.z;
        float minZ = Mathf.Min(zSheerStern, zTopStern);
        float maxZ = Mathf.Max(zSheerStern, zTopStern);
        if (maxZ <= minZ + 0.001f) return maxZ;

        float lowZ = minZ, highZ = maxZ;
        for (int step = 0; step < 25; step++)
        {
            float midZ = (lowZ + highZ) * 0.5f;
            float sheerY = GetPureMainSheerYAtZ(midZ);
            float topY = GetPureTopTimberYAtZ(midZ);
            float lineY = Mathf.Lerp(sheerY, topY, u);
            float sternY = sheerPlan.GetSternProfileYAtZ(midZ);

            if (lineY > sternY) highZ = midZ;
            else lowZ = midZ;
        }
        return (lowZ + highZ) * 0.5f;
    }

    // --- 3D RIBBAND GENERATION ---

    public List<List<Vector3>> GetLower3DRibbands(float sideSign)
    {
        List<List<Vector3>> ribbands = new List<List<Vector3>>();
        FindReferences();
        if (sheerPlan == null || breadthPlan == null) return ribbands;

        breadthPlan.UpdateCachedCurves();
        sheerPlan.UpdateCachedValues();
        if (bodyPlan != null) bodyPlan.UpdateCachedCurves();

        List<float> vProps = GetLowerProportions();
        int halfRes = Mathf.Max(5, resolution / 2);
        float hw = (breadthPlan.bodyPlan != null) ? Mathf.Max(0.001f, breadthPlan.bodyPlan.keelWidth / 2f) : 0.25f;

        float zFloorStern = breadthPlan.cachedFloorSternPoint.z;
        float zFloorBow = breadthPlan.cachedFloorBowPoint.z;

        float midKeelY = GetPureKeelYAtZ(0f);
        float midRisingY = GetExtendedRisingLineYAtZ(0f);
        float xFloorMid = breadthPlan.GetFloorLineXAtZ(0f);

        foreach (float v in vProps)
        {
            float zSternEnd = GetSternIntersectionZForLower(v);
            float zBowEnd = GetStemIntersectionZForLower(v);

            float yMid = Mathf.Lerp(midKeelY, midRisingY, v);
            float xMouldMid = (bodyPlan != null) ? bodyPlan.GetLowerMouldXAtY(yMid) : Mathf.Lerp(hw, xFloorMid, v);

            List<Vector3> pts = new List<Vector3>();

            // 1. Aft Section (Stern End zSternEnd -> Midships Z=0)
            for (int i = 0; i <= halfRes; i++)
            {
                float s = 1f - (float)i / halfRes;
                float z = s * zSternEnd;

                float keelY = GetPureKeelYAtZ(z);
                float risingY = GetExtendedRisingLineYAtZ(z);
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
                float s = (float)i / halfRes;
                float z = s * zBowEnd;

                float keelY = GetPureKeelYAtZ(z);
                float risingY = GetExtendedRisingLineYAtZ(z);
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

    public List<List<Vector3>> GetMiddle3DRibbands(float sideSign)
    {
        List<List<Vector3>> ribbands = new List<List<Vector3>>();
        FindReferences();
        if (sheerPlan == null || breadthPlan == null) return ribbands;

        breadthPlan.UpdateCachedCurves();
        sheerPlan.UpdateCachedValues();
        if (bodyPlan != null) bodyPlan.UpdateCachedCurves();

        List<float> mProps = GetMiddleProportions();
        int halfRes = Mathf.Max(5, resolution / 2);

        float zFloorStern = breadthPlan.cachedFloorSternPoint.z;
        float zFloorBow = breadthPlan.cachedFloorBowPoint.z;

        float zHobStern = sheerPlan.cachedHobSternIntersect.z;
        float zHobBow = sheerPlan.cachedHobBowIntersect.z;

        float midRisingY = GetExtendedRisingLineYAtZ(0f);
        float midSheerY = GetPureMainSheerYAtZ(0f);

        float xFloorMid = breadthPlan.GetFloorLineXAtZ(0f);
        float xHobMid = breadthPlan.GetTheoreticalBreadthXAtZ(0f);
        float hobY = (bodyPlan != null) ? bodyPlan.EvaluateLowerMouldAtT(1.0f).y : 1.5f;

        foreach (float m in mProps)
        {
            float zSternEnd = GetSternIntersectionZForMiddle(m);
            float zBowEnd = GetStemIntersectionZForMiddle(m);

            float yMid = Mathf.Lerp(midRisingY, midSheerY, m);
            float xMouldMid = (bodyPlan != null) ? ((yMid <= hobY + 0.001f) ? bodyPlan.GetLowerMouldXAtY(yMid) : bodyPlan.GetUpperMouldXAtY(yMid)) : Mathf.Lerp(xFloorMid, xHobMid, m);

            float mEff = (Mathf.Abs(xHobMid - xFloorMid) > 0.0001f) ? Mathf.Clamp01((xMouldMid - xFloorMid) / (xHobMid - xFloorMid)) : m;

            List<Vector3> pts = new List<Vector3>();

            // 1. Aft Section (Stern End zSternEnd -> Midships Z=0)
            for (int i = 0; i <= halfRes; i++)
            {
                float s = 1f - (float)i / halfRes;
                float z = s * zSternEnd;

                float risingY = GetExtendedRisingLineYAtZ(z);
                float sheerY = GetPureMainSheerYAtZ(z);
                float y = Mathf.Lerp(risingY, sheerY, m);

                float zFloorSample = s * zFloorStern;
                float zHobSample = s * zHobStern;
                float xFloor = breadthPlan.GetFloorLineXAtZ(zFloorSample);
                float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(zHobSample);
                float x = Mathf.Lerp(xFloor, xHob, mEff);

                pts.Add(new Vector3(sideSign * x, y, z));
            }

            // 2. Forward Section (Midships Z=0 -> Bow End zBowEnd)
            for (int i = 1; i <= halfRes; i++)
            {
                float s = (float)i / halfRes;
                float z = s * zBowEnd;

                float risingY = GetExtendedRisingLineYAtZ(z);
                float sheerY = GetPureMainSheerYAtZ(z);
                float y = Mathf.Lerp(risingY, sheerY, m);

                float zFloorSample = s * zFloorBow;
                float zHobSample = s * zHobBow;
                float xFloor = breadthPlan.GetFloorLineXAtZ(zFloorSample);
                float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(zHobSample);
                float x = Mathf.Lerp(xFloor, xHob, mEff);

                pts.Add(new Vector3(sideSign * x, y, z));
            }

            ribbands.Add(pts);
        }
        return ribbands;
    }

    public List<List<Vector3>> GetUpper3DRibbands(float sideSign)
    {
        List<List<Vector3>> ribbands = new List<List<Vector3>>();
        FindReferences();
        if (sheerPlan == null || breadthPlan == null) return ribbands;

        breadthPlan.UpdateCachedCurves();
        sheerPlan.UpdateCachedValues();
        if (bodyPlan != null) bodyPlan.UpdateCachedCurves();

        List<float> uProps = GetUpperProportions();
        int halfRes = Mathf.Max(5, resolution / 2);

        float zHobStern = sheerPlan.cachedHobSternIntersect.z;
        float zHobBow = sheerPlan.cachedHobBowIntersect.z;

        float zTTStern = sheerPlan.cachedTopTimberSternIntersect.z;
        float zTTBow = sheerPlan.cachedTopTimberBowIntersect.z;

        float midSheerY = GetPureMainSheerYAtZ(0f);
        float midTopTimberY = GetPureTopTimberYAtZ(0f);

        float xHobMid = breadthPlan.GetTheoreticalBreadthXAtZ(0f);
        float xTTMid = breadthPlan.GetTopTimberXAtZ(0f);

        foreach (float u in uProps)
        {
            float zSternEnd = GetSternIntersectionZForUpper(u);
            float zBowEnd = GetStemIntersectionZForUpper(u);

            float yMid = Mathf.Lerp(midSheerY, midTopTimberY, u);
            float xMouldMid = (bodyPlan != null) ? bodyPlan.GetUpperMouldXAtY(yMid) : Mathf.Lerp(xHobMid, xTTMid, u);

            float uEff = (Mathf.Abs(xTTMid - xHobMid) > 0.0001f) ? Mathf.Clamp01((xMouldMid - xHobMid) / (xTTMid - xHobMid)) : u;

            List<Vector3> pts = new List<Vector3>();

            // 1. Aft Section (Stern End zSternEnd -> Midships Z=0)
            for (int i = 0; i <= halfRes; i++)
            {
                float s = 1f - (float)i / halfRes;
                float z = s * zSternEnd;

                float sheerY = GetPureMainSheerYAtZ(z);
                float topY = GetPureTopTimberYAtZ(z);
                float y = Mathf.Lerp(sheerY, topY, u);

                float zHobSample = s * zHobStern;
                float zTTSample = s * zTTStern;
                float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(zHobSample);
                float xTT = breadthPlan.GetTopTimberXAtZ(zTTSample);
                float x = Mathf.Lerp(xHob, xTT, uEff);

                pts.Add(new Vector3(sideSign * x, y, z));
            }

            // 2. Forward Section (Midships Z=0 -> Bow End zBowEnd)
            for (int i = 1; i <= halfRes; i++)
            {
                float s = (float)i / halfRes;
                float z = s * zBowEnd;

                float sheerY = GetPureMainSheerYAtZ(z);
                float topY = GetPureTopTimberYAtZ(z);
                float y = Mathf.Lerp(sheerY, topY, u);

                float zHobSample = s * zHobBow;
                float zTTSample = s * zTTBow;
                float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(zHobSample);
                float xTT = breadthPlan.GetTopTimberXAtZ(zTTSample);
                float x = Mathf.Lerp(xHob, xTT, uEff);

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
        if (sheerPlan == null || breadthPlan == null) return;

        m_CachedStbdLowerRibbands = GetLower3DRibbands(1f);
        m_CachedPortLowerRibbands = GetLower3DRibbands(-1f);

        m_CachedStbdMiddleRibbands = GetMiddle3DRibbands(1f);
        m_CachedPortMiddleRibbands = GetMiddle3DRibbands(-1f);

        m_CachedStbdUpperRibbands = GetUpper3DRibbands(1f);
        m_CachedPortUpperRibbands = GetUpper3DRibbands(-1f);

        m_CachedMidshipPoints.Clear();
        if (bodyPlan != null)
        {
            bodyPlan.UpdateCachedCurves();
            List<float> vProps = GetLowerProportions();
            float midKeelY = GetPureKeelYAtZ(0f);
            float midRisingY = GetExtendedRisingLineYAtZ(0f);
            foreach (float v in vProps)
            {
                float y = Mathf.Lerp(midKeelY, midRisingY, v);
                float x = bodyPlan.GetLowerMouldXAtY(y);
                m_CachedMidshipPoints.Add(new Vector3(x, y, 0f));
            }

            List<float> mProps = GetMiddleProportions();
            float midSheerY = GetPureMainSheerYAtZ(0f);
            float hobY = bodyPlan.EvaluateLowerMouldAtT(1.0f).y;
            foreach (float m in mProps)
            {
                float y = Mathf.Lerp(midRisingY, midSheerY, m);
                float x = (y <= hobY + 0.001f) ? bodyPlan.GetLowerMouldXAtY(y) : bodyPlan.GetUpperMouldXAtY(y);
                m_CachedMidshipPoints.Add(new Vector3(x, y, 0f));
            }

            List<float> uProps = GetUpperProportions();
            float midTopY = GetPureTopTimberYAtZ(0f);
            foreach (float u in uProps)
            {
                float y = Mathf.Lerp(midSheerY, midTopY, u);
                float x = bodyPlan.GetUpperMouldXAtY(y);
                m_CachedMidshipPoints.Add(new Vector3(x, y, 0f));
            }
        }

        m_IsCacheDirty = false;
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || breadthPlan == null) return;

        if (m_IsCacheDirty)
        {
            RebuildCache();
        }

        bool drawLower = (stackRegion == StackRegion.Lower_KeelToRising || stackRegion == StackRegion.Full_HullStack);
        bool drawMiddle = (stackRegion == StackRegion.Middle_RisingToMainSheer || stackRegion == StackRegion.Full_HullStack);
        bool drawUpper = (stackRegion == StackRegion.Upper_MainSheerToTopTimber || stackRegion == StackRegion.Full_HullStack);

        if (show3DRibbands)
        {
            if (drawLower)
            {
                if (showStarboard) DrawStack(m_CachedStbdLowerRibbands, keelColor, risingLineColor);
                if (showPort) DrawStack(m_CachedPortLowerRibbands, keelColor, risingLineColor);
            }

            if (drawMiddle)
            {
                if (showStarboard) DrawStack(m_CachedStbdMiddleRibbands, risingLineColor, mainSheerColor);
                if (showPort) DrawStack(m_CachedPortMiddleRibbands, risingLineColor, mainSheerColor);
            }

            if (drawUpper)
            {
                if (showStarboard) DrawStack(m_CachedStbdUpperRibbands, mainSheerColor, topTimberColor);
                if (showPort) DrawStack(m_CachedPortUpperRibbands, mainSheerColor, topTimberColor);
            }
        }

        if (showProfileIntersectionDashedLines)
        {
            DrawDashedProfileIntersections();
        }

        if (showMidshipBodyPlanPoints && m_CachedMidshipPoints != null)
        {
            Gizmos.color = midshipPointColor;
            foreach (Vector3 pt in m_CachedMidshipPoints)
            {
                if (showStarboard) Gizmos.DrawSphere(pt, pointGizmoRadius);
                if (showPort) Gizmos.DrawSphere(new Vector3(-pt.x, pt.y, pt.z), pointGizmoRadius);
            }
        }
    }

    private void DrawStack(List<List<Vector3>> ribbandList, Color startColor, Color endColor)
    {
        if (ribbandList == null) return;
        int count = ribbandList.Count;

        for (int k = 0; k < count; k++)
        {
            float t = (count > 1) ? (float)k / (count - 1) : 0f;
            Color c = Color.Lerp(startColor, endColor, t);
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
            Gizmos.DrawSphere(pts[0], endpointGizmoRadius);
            Gizmos.DrawSphere(pts[pts.Count - 1], endpointGizmoRadius);
        }
    }

    private void DrawDashedProfileIntersections()
    {
        bool drawLower = (stackRegion == StackRegion.Lower_KeelToRising || stackRegion == StackRegion.Full_HullStack);
        bool drawMiddle = (stackRegion == StackRegion.Middle_RisingToMainSheer || stackRegion == StackRegion.Full_HullStack);
        bool drawUpper = (stackRegion == StackRegion.Upper_MainSheerToTopTimber || stackRegion == StackRegion.Full_HullStack);

        if (drawLower)
        {
            List<float> vProps = GetLowerProportions();
            foreach (float v in vProps)
            {
                float zStem = GetStemIntersectionZForLower(v);
                float yStem = sheerPlan.GetStemProfileYAtZ(zStem);
                DrawDashedLine(new Vector3(0f, 0f, zStem), new Vector3(0f, yStem, zStem), dashLength, dashedLineColor);

                float zStern = GetSternIntersectionZForLower(v);
                float yStern = sheerPlan.GetSternProfileYAtZ(zStern);
                DrawDashedLine(new Vector3(0f, 0f, zStern), new Vector3(0f, yStern, zStern), dashLength, dashedLineColor);
            }
        }

        if (drawMiddle)
        {
            List<float> mProps = GetMiddleProportions();
            foreach (float m in mProps)
            {
                float zStem = GetStemIntersectionZForMiddle(m);
                float yStem = sheerPlan.GetStemProfileYAtZ(zStem);
                DrawDashedLine(new Vector3(0f, 0f, zStem), new Vector3(0f, yStem, zStem), dashLength, dashedLineColor);

                float zStern = GetSternIntersectionZForMiddle(m);
                float yStern = sheerPlan.GetSternProfileYAtZ(zStern);
                DrawDashedLine(new Vector3(0f, 0f, zStern), new Vector3(0f, yStern, zStern), dashLength, dashedLineColor);
            }
        }

        if (drawUpper)
        {
            List<float> uProps = GetUpperProportions();
            foreach (float u in uProps)
            {
                float zStem = GetStemIntersectionZForUpper(u);
                float yStem = sheerPlan.GetStemProfileYAtZ(zStem);
                DrawDashedLine(new Vector3(0f, 0f, zStem), new Vector3(0f, yStem, zStem), dashLength, dashedLineColor);

                float zStern = GetSternIntersectionZForUpper(u);
                float yStern = sheerPlan.GetSternProfileYAtZ(zStern);
                DrawDashedLine(new Vector3(0f, 0f, zStern), new Vector3(0f, yStern, zStern), dashLength, dashedLineColor);
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
}
