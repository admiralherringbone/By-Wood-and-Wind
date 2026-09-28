using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RibbandStrakes script for generating and displaying the core 3D baseline ribbands of the hull:
/// 1. Floor Breadth shape following the Rising Line.
/// 2. Maximum Breadth shape following the Height of Breadth (HoB) Line.
/// 3. Top Timber Breadth shape following the Top Line of the Sheer Plan.
/// 4. Keel Line following the Keel Line from Keel Bow point to Keel Stern point.
/// </summary>
[ExecuteInEditMode]
public class RibbandStrakes : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Reference to SheerPlan component.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to BreadthPlan component.")]
    public BreadthPlan breadthPlan;

    [Header("Resolution & Visibility Settings")]
    [Tooltip("Longitudinal resolution along each 3D ribband from Stern to Bow.")]
    [Range(10, 300)]
    public int resolution = 100;

    [Tooltip("Show visualization on Starboard (+X) side.")]
    public bool showStarboard = true;

    [Tooltip("Show visualization on Port (-X) side.")]
    public bool showPort = true;

    [Header("Line Display Toggles")]
    [Tooltip("Display the Keel Line curve.")]
    public bool showKeelLine = true;

    [Tooltip("Display stacked intermediate ribbands between Floor Rising Line and Keel Line.")]
    public bool showLowerStackedRibbands = true;

    [Tooltip("Display the Floor Breadth Ribband (following the Rising Line).")]
    public bool showFloorRibband = true;

    [Tooltip("Display stacked intermediate ribbands between Main Sheer Line and Floor Rising Line.")]
    public bool showMiddleStackedRibbands = true;

    [Tooltip("Display the Maximum Breadth Ribband (following the HoB Line).")]
    public bool showMaxBreadthRibband = true;

    [Tooltip("Display the Hybrid Main Sheer Line Ribband.")]
    public bool showMainSheerRibband = true;

    [Tooltip("Display stacked intermediate ribbands between Top Timber Line and Main Sheer Line.")]
    public bool showBulwarkStackedRibbands = true;

    [Tooltip("Display the Top Timber Breadth Ribband (following the Top Line of Sheer Plan).")]
    public bool showTopTimberRibband = true;

    [Tooltip("Target vertical interval / arc length for stacked ribbands at Midships (default 0.25).")]
    public float pointInterval = 0.25f;

    [Header("Color Settings")]
    public Color keelColor = new Color(0.2f, 0.8f, 1f, 0.95f);
    public Color lowerRibbandColor = new Color(0.3f, 0.9f, 0.6f, 0.95f);
    public Color floorRibbandColor = new Color(0.3f, 0.9f, 0.6f, 0.95f);
    public Color middleRibbandColor = new Color(0.95f, 0.55f, 0.15f, 0.95f);
    public Color maxBreadthRibbandColor = new Color(1f, 0.85f, 0.2f, 0.95f);
    public Color mainSheerRibbandColor = Color.yellow;
    public Color bulwarkRibbandColor = new Color(0.95f, 0.25f, 0.35f, 0.95f);
    public Color topTimberRibbandColor = new Color(0.9f, 0.1f, 0.2f, 0.95f);

    private List<Vector3> m_CachedKeelLine = new List<Vector3>();
    private List<List<Vector3>> m_CachedStbdLowerRibbands = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortLowerRibbands = new List<List<Vector3>>();
    private List<Vector3> m_CachedStbdFloorRibband = new List<Vector3>();
    private List<Vector3> m_CachedPortFloorRibband = new List<Vector3>();
    private List<List<Vector3>> m_CachedStbdMiddleRibbands = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortMiddleRibbands = new List<List<Vector3>>();
    private List<Vector3> m_CachedStbdMaxBreadthRibband = new List<Vector3>();
    private List<Vector3> m_CachedPortMaxBreadthRibband = new List<Vector3>();
    private List<Vector3> m_CachedStbdMainSheerRibband = new List<Vector3>();
    private List<Vector3> m_CachedPortMainSheerRibband = new List<Vector3>();
    private List<List<Vector3>> m_CachedStbdBulwarkRibbands = new List<List<Vector3>>();
    private List<List<Vector3>> m_CachedPortBulwarkRibbands = new List<List<Vector3>>();
    private List<Vector3> m_CachedStbdTopTimberRibband = new List<Vector3>();
    private List<Vector3> m_CachedPortTopTimberRibband = new List<Vector3>();

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
        if (sheerPlan != null && breadthPlan == null) breadthPlan = sheerPlan.breadthPlan;
    }

    /// <summary>
    /// Evaluates baseline Keel Line from Keel Stern point to Keel Bow point.
    /// </summary>
    public List<Vector3> GetKeelLine()
    {
        List<Vector3> pts = new List<Vector3>();
        if (sheerPlan == null) return pts;
        sheerPlan.UpdateCachedValues();

        float zStern = sheerPlan.cachedKeelSternPoint.z;
        float zBow = sheerPlan.cachedKeelBowPoint.z;
        int res = Mathf.Clamp(resolution, 10, 300);

        for (int i = 0; i <= res; i++)
        {
            float alpha = (float)i / res;
            float z = Mathf.Lerp(zStern, zBow, alpha);
            float y = sheerPlan.GetKeelYAtZ(z);
            pts.Add(new Vector3(0f, y, z));
        }

        return pts;
    }

    /// <summary>
    /// Evaluates stacked ribbands between Floor Rising Line ribband and Keel Line:
    /// - Uses BodyPlanLowerMouldPointsPractice points to anchor midship Y heights and X half-widths.
    /// - Uses MainSheerKeelLineTransitionPractice Z-axis Stem/Stern profile intersections.
    /// - Transitions 3D breadth shapes between Keel baseline and Floor Breadth Ribband.
    /// </summary>
    public List<List<Vector3>> GetLowerStackedRibbands(float sideSign)
    {
        List<List<Vector3>> ribbandStack = new List<List<Vector3>>();
        if (sheerPlan == null || breadthPlan == null) return ribbandStack;

        sheerPlan.UpdateCachedValues();
        breadthPlan.UpdateCachedCurves();

        MainSheerKeelLineTransitionPractice transPractice = FindFirstObjectByType<MainSheerKeelLineTransitionPractice>();
        BodyPlanLowerMouldPointsPractice bodyPointsPractice = FindFirstObjectByType<BodyPlanLowerMouldPointsPractice>();

        float midKeelY = sheerPlan.GetKeelYAtZ(0f);
        float midRisingY = sheerPlan.GetRisingLineYAtZ(0f);
        float lowerSpan = Mathf.Max(0.001f, midRisingY - midKeelY);

        float hw = (breadthPlan.bodyPlan != null) ? Mathf.Max(0.001f, breadthPlan.bodyPlan.keelWidth / 2f) : 0.25f;
        float xFloorMid = breadthPlan.GetFloorLineXAtZ(0f);
        float xSpan = Mathf.Max(0.001f, xFloorMid - hw);

        float zFloorStern = breadthPlan.cachedFloorSternPoint.z;
        float zFloorBow = breadthPlan.cachedFloorBowPoint.z;

        List<Vector3> midshipPts = (bodyPointsPractice != null) ? bodyPointsPractice.GetSampledPoints(1f) : null;
        List<Vector3> lowerRegionPts = new List<Vector3>();

        if (midshipPts != null && midshipPts.Count > 0)
        {
            foreach (Vector3 pt in midshipPts)
            {
                if (pt.y >= midKeelY - 0.0001f && pt.y <= midRisingY + 0.0001f)
                {
                    lowerRegionPts.Add(pt);
                }
            }
        }

        if (lowerRegionPts.Count == 0)
        {
            float interval = Mathf.Max(0.01f, pointInterval);
            int n = Mathf.Max(1, Mathf.RoundToInt(lowerSpan / interval));
            for (int k = 0; k <= n; k++)
            {
                float v = (float)k / n;
                float y = Mathf.Lerp(midKeelY, midRisingY, v);
                float x = (sheerPlan.bodyPlan != null) ? sheerPlan.bodyPlan.GetLowerMouldXAtY(y) : Mathf.Lerp(hw, xFloorMid, v);
                lowerRegionPts.Add(new Vector3(x, y, 0f));
            }
        }

        int halfRes = Mathf.Max(5, resolution / 2);

        foreach (Vector3 pt in lowerRegionPts)
        {
            float v = Mathf.Clamp01((pt.y - midKeelY) / lowerSpan);

            float zSternEnd = (transPractice != null) ? transPractice.GetSternIntersectionZForV(v) : (sheerPlan.cachedKeelSternPoint.z * (1f - v) + zFloorStern * v);
            float zBowEnd = (transPractice != null) ? transPractice.GetStemIntersectionZForV(v) : (sheerPlan.cachedKeelBowPoint.z * (1f - v) + zFloorBow * v);

            float vEff = (Mathf.Abs(xFloorMid - hw) > 0.0001f) ? Mathf.Clamp01((pt.x - hw) / xSpan) : v;

            List<Vector3> pts = new List<Vector3>();

            // 1. Aft Section (Stern End -> Midships Z=0)
            for (int i = 0; i <= halfRes; i++)
            {
                float s = 1f - (float)i / halfRes;
                float z = s * zSternEnd;

                float keelY = (transPractice != null) ? transPractice.GetPureKeelYAtZ(z) : sheerPlan.GetKeelYAtZ(z);
                float risingY = (transPractice != null) ? transPractice.GetExtendedRisingLineYAtZ(z) : sheerPlan.GetRisingLineYAtZ(z);
                float y = Mathf.Lerp(keelY, risingY, v);

                float xFloor = breadthPlan.GetFloorLineXAtZ(z);
                float x = Mathf.Lerp(hw, xFloor, vEff);
                pts.Add(new Vector3(sideSign * x, y, z));
            }

            // 2. Forward Section (Midships Z=0 -> Bow End)
            for (int i = 1; i <= halfRes; i++)
            {
                float s = (float)i / halfRes;
                float z = s * zBowEnd;

                float keelY = (transPractice != null) ? transPractice.GetPureKeelYAtZ(z) : sheerPlan.GetKeelYAtZ(z);
                float risingY = (transPractice != null) ? transPractice.GetExtendedRisingLineYAtZ(z) : sheerPlan.GetRisingLineYAtZ(z);
                float y = Mathf.Lerp(keelY, risingY, v);

                float xFloor = breadthPlan.GetFloorLineXAtZ(z);
                float x = Mathf.Lerp(hw, xFloor, vEff);
                pts.Add(new Vector3(sideSign * x, y, z));
            }

            ribbandStack.Add(pts);
        }

        return ribbandStack;
    }

    /// <summary>
    /// Evaluates Floor Breadth 3D Ribband (Floor Breadth X shape following Rising Line Y height).
    /// </summary>
    public List<Vector3> GetFloorBreadthRibband(float sideSign)
    {
        List<Vector3> pts = new List<Vector3>();
        if (sheerPlan == null || breadthPlan == null) return pts;

        sheerPlan.UpdateCachedValues();
        breadthPlan.UpdateCachedCurves();

        float zStern = breadthPlan.cachedFloorSternPoint.z;
        float zBow = breadthPlan.cachedFloorBowPoint.z;
        int res = Mathf.Clamp(resolution, 10, 300);

        for (int i = 0; i <= res; i++)
        {
            float alpha = (float)i / res;
            float z = Mathf.Lerp(zStern, zBow, alpha);

            float x = breadthPlan.GetFloorLineXAtZ(z);
            float y = sheerPlan.GetRisingLineYAtZ(z);

            pts.Add(new Vector3(sideSign * x, y, z));
        }

        return pts;
    }

    /// <summary>
    /// Evaluates stacked ribbands between Main Sheer Line ribband and Floor Rising Line ribband:
    /// - Uses BodyPlanLowerMouldPointsPractice points to anchor midship Y heights and X half-widths.
    /// - Uses MainSheerKeelLineTransitionPractice Z-axis Stem/Stern profile intersections.
    /// - Transitions 3D breadth shapes between Floor Breadth Ribband, Maximum Breadth Ribband, and Hybrid Main Sheer Ribband.
    /// </summary>
    public List<List<Vector3>> GetMiddleStackedRibbands(float sideSign)
    {
        List<List<Vector3>> ribbandStack = new List<List<Vector3>>();
        if (sheerPlan == null || breadthPlan == null) return ribbandStack;

        sheerPlan.UpdateCachedValues();
        breadthPlan.UpdateCachedCurves();

        MainSheerKeelLineTransitionPractice transPractice = FindFirstObjectByType<MainSheerKeelLineTransitionPractice>();
        BodyPlanLowerMouldPointsPractice bodyPointsPractice = FindFirstObjectByType<BodyPlanLowerMouldPointsPractice>();
        BodyPlan bodyPlan = (sheerPlan != null) ? sheerPlan.bodyPlan : null;
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (bodyPlan != null) bodyPlan.UpdateCachedCurves();

        float midRisingY = sheerPlan.GetRisingLineYAtZ(0f);
        float midSheerY = sheerPlan.GetMainSheerYAtZ(0f);
        float midHobY = (bodyPlan != null) ? bodyPlan.EvaluateLowerMouldAtT(1.0f).y : sheerPlan.GetHobYAtZ(0f);
        float midSpan = Mathf.Max(0.001f, midSheerY - midRisingY);

        float xFloorMid = breadthPlan.GetFloorLineXAtZ(0f);
        float xHobMid = breadthPlan.GetTheoreticalBreadthXAtZ(0f);
        float xSheerMid = GetMainSheerRibbandXAtZ(0f);

        float zFloorStern = breadthPlan.cachedFloorSternPoint.z;
        float zFloorBow = breadthPlan.cachedFloorBowPoint.z;
        float zHobStern = sheerPlan.cachedHobSternIntersect.z;
        float zHobBow = sheerPlan.cachedHobBowIntersect.z;

        List<Vector3> midshipPts = (bodyPointsPractice != null) ? bodyPointsPractice.GetSampledPoints(1f) : null;
        List<Vector3> middleRegionPts = new List<Vector3>();

        if (midshipPts != null && midshipPts.Count > 0)
        {
            foreach (Vector3 pt in midshipPts)
            {
                if (pt.y > midRisingY - 0.0001f && pt.y < midSheerY + 0.0001f)
                {
                    middleRegionPts.Add(pt);
                }
            }
        }

        if (middleRegionPts.Count == 0)
        {
            float interval = Mathf.Max(0.01f, pointInterval);
            int n = Mathf.Max(1, Mathf.RoundToInt(midSpan / interval));
            for (int k = 0; k <= n; k++)
            {
                float m = (float)k / n;
                float y = Mathf.Lerp(midRisingY, midSheerY, m);
                float x = (bodyPlan != null) ? ((y <= midHobY + 0.001f) ? bodyPlan.GetLowerMouldXAtY(y) : bodyPlan.GetUpperMouldXAtY(y)) : Mathf.Lerp(xFloorMid, xHobMid, m);
                middleRegionPts.Add(new Vector3(x, y, 0f));
            }
        }

        int halfRes = Mathf.Max(5, resolution / 2);

        foreach (Vector3 pt in middleRegionPts)
        {
            float m = Mathf.Clamp01((pt.y - midRisingY) / midSpan);

            float zSternEnd = (transPractice != null) ? transPractice.GetSternIntersectionZForU(m) : Mathf.Lerp(zFloorStern, zHobStern, m);
            float zBowEnd = (transPractice != null) ? transPractice.GetStemIntersectionZForU(m) : Mathf.Lerp(zFloorBow, zHobBow, m);

            bool isAboveHob = (pt.y > midHobY + 0.0001f);
            float mEff;
            if (!isAboveHob)
            {
                mEff = (Mathf.Abs(xHobMid - xFloorMid) > 0.0001f) ? Mathf.Clamp01((pt.x - xFloorMid) / (xHobMid - xFloorMid)) : m;
            }
            else
            {
                mEff = (Mathf.Abs(xSheerMid - xHobMid) > 0.0001f) ? Mathf.Clamp01((pt.x - xHobMid) / (xSheerMid - xHobMid)) : m;
            }

            List<Vector3> pts = new List<Vector3>();

            // 1. Aft Section (Stern End -> Midships Z=0)
            for (int i = 0; i <= halfRes; i++)
            {
                float s = 1f - (float)i / halfRes;
                float z = s * zSternEnd;

                float risingY = sheerPlan.GetRisingLineYAtZ(z);
                float sheerY = sheerPlan.GetMainSheerYAtZ(z);
                float y = Mathf.Lerp(risingY, sheerY, m);

                float x;
                if (!isAboveHob)
                {
                    float xFloor = breadthPlan.GetFloorLineXAtZ(z);
                    float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(z);
                    x = Mathf.Lerp(xFloor, xHob, mEff);
                }
                else
                {
                    float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(z);
                    float xSheerRib = GetMainSheerRibbandXAtZ(z);
                    x = Mathf.Lerp(xHob, xSheerRib, mEff);
                }

                pts.Add(new Vector3(sideSign * x, y, z));
            }

            // 2. Forward Section (Midships Z=0 -> Bow End)
            for (int i = 1; i <= halfRes; i++)
            {
                float s = (float)i / halfRes;
                float z = s * zBowEnd;

                float risingY = sheerPlan.GetRisingLineYAtZ(z);
                float sheerY = sheerPlan.GetMainSheerYAtZ(z);
                float y = Mathf.Lerp(risingY, sheerY, m);

                float x;
                if (!isAboveHob)
                {
                    float xFloor = breadthPlan.GetFloorLineXAtZ(z);
                    float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(z);
                    x = Mathf.Lerp(xFloor, xHob, mEff);
                }
                else
                {
                    float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(z);
                    float xSheerRib = GetMainSheerRibbandXAtZ(z);
                    x = Mathf.Lerp(xHob, xSheerRib, mEff);
                }

                pts.Add(new Vector3(sideSign * x, y, z));
            }

            ribbandStack.Add(pts);
        }

        return ribbandStack;
    }

    /// <summary>
    /// Evaluates Maximum Breadth 3D Ribband (Theoretical/Max Breadth X shape following Height of Breadth HoB Y height).
    /// </summary>
    public List<Vector3> GetMaxBreadthRibband(float sideSign)
    {
        List<Vector3> pts = new List<Vector3>();
        if (sheerPlan == null || breadthPlan == null) return pts;

        sheerPlan.UpdateCachedValues();
        breadthPlan.UpdateCachedCurves();

        float zStern = sheerPlan.cachedHobSternIntersect.z;
        float zBow = sheerPlan.cachedHobBowIntersect.z;
        int res = Mathf.Clamp(resolution, 10, 300);

        for (int i = 0; i <= res; i++)
        {
            float alpha = (float)i / res;
            float z = Mathf.Lerp(zStern, zBow, alpha);

            float x = breadthPlan.GetTheoreticalBreadthXAtZ(z);
            float y = sheerPlan.GetHobYAtZ(z);

            pts.Add(new Vector3(sideSign * x, y, z));
        }

        return pts;
    }

    /// <summary>
    /// Evaluates X breadth position on the Hybrid Main Sheer Line 3D Ribband at station z.
    /// </summary>
    public float GetMainSheerRibbandXAtZ(float z)
    {
        if (sheerPlan == null || breadthPlan == null) return 0f;
        BodyPlan bodyPlan = (sheerPlan != null) ? sheerPlan.bodyPlan : null;
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (bodyPlan != null) bodyPlan.UpdateCachedCurves();

        float midSheerY = sheerPlan.GetMainSheerYAtZ(0f);
        float midHobX = breadthPlan.GetTheoreticalBreadthXAtZ(0f);
        float midTTX = breadthPlan.GetTopTimberXAtZ(0f);

        float xMouldMid = (bodyPlan != null) ? bodyPlan.GetUpperMouldXAtY(midSheerY) : Mathf.Lerp(midHobX, midTTX, 0.5f);
        float uMould = (Mathf.Abs(midTTX - midHobX) > 0.0001f) ? Mathf.Clamp01((xMouldMid - midHobX) / (midTTX - midHobX)) : 0.5f;

        float y = sheerPlan.GetMainSheerYAtZ(z);
        float yHob = sheerPlan.GetHobYAtZ(z);
        float yTop = sheerPlan.GetPureSheerYAtZ(z);

        float topSpan = Mathf.Max(0.001f, yTop - yHob);
        float uSheer = Mathf.Clamp01((y - yHob) / topSpan);

        float uEff = Mathf.Lerp(uMould, uSheer, 0.5f);

        float xHob = breadthPlan.GetTheoreticalBreadthXAtZ(z);
        float xTT = breadthPlan.GetTopTimberXAtZ(z);
        return Mathf.Lerp(xHob, xTT, uEff);
    }

    /// <summary>
    /// Evaluates Hybrid Main Sheer Line 3D Ribband:
    /// - Height Y(Z) follows the exact Main Sheer Line curve sheerPlan.GetMainSheerYAtZ(z).
    /// - Width X(Z) is a hybrid blend between Maximum Breadth shape and Top Timber Breadth shape,
    ///   anchored to the Body Plan Master Frame Upper Mould curve at Midships.
    /// </summary>
    public List<Vector3> GetMainSheerRibband(float sideSign)
    {
        List<Vector3> pts = new List<Vector3>();
        if (sheerPlan == null || breadthPlan == null) return pts;

        sheerPlan.UpdateCachedValues();
        breadthPlan.UpdateCachedCurves();

        float zStern = sheerPlan.cachedMainSheerSternIntersect.z;
        float zBow = sheerPlan.cachedMainSheerBowIntersect.z;
        int res = Mathf.Clamp(resolution, 10, 300);

        for (int i = 0; i <= res; i++)
        {
            float alpha = (float)i / res;
            float z = Mathf.Lerp(zStern, zBow, alpha);

            float y = sheerPlan.GetMainSheerYAtZ(z);
            float x = GetMainSheerRibbandXAtZ(z);

            pts.Add(new Vector3(sideSign * x, y, z));
        }

        return pts;
    }

    /// <summary>
    /// Evaluates Top Timber Breadth 3D Ribband (Top Timber X shape following Top Line Y height of Sheer Plan).
    /// </summary>
    public List<Vector3> GetTopTimberBreadthRibband(float sideSign)
    {
        List<Vector3> pts = new List<Vector3>();
        if (sheerPlan == null || breadthPlan == null) return pts;

        sheerPlan.UpdateCachedValues();
        breadthPlan.UpdateCachedCurves();

        float zStern = sheerPlan.cachedTransomTopPoint.z;
        float zBow = sheerPlan.cachedMainSheerBowIntersect.z;
        if (sheerPlan.cachedUpperStemTopPoint.z > zBow) zBow = sheerPlan.cachedUpperStemTopPoint.z;
        int res = Mathf.Clamp(resolution, 10, 300);

        for (int i = 0; i <= res; i++)
        {
            float alpha = (float)i / res;
            float z = Mathf.Lerp(zStern, zBow, alpha);

            float x = breadthPlan.GetTopTimberXAtZ(z);
            float y = sheerPlan.GetPureSheerYAtZ(z); // Pure Top Line of Sheer Plan

            pts.Add(new Vector3(sideSign * x, y, z));
        }

        return pts;
    }

    /// <summary>
    /// Evaluates stacked ribbands between Main Sheer Line ribband and Top Timber Line ribband:
    /// - Uses BodyPlanLowerMouldPointsPractice points to anchor midship Y heights and X half-widths.
    /// - Uses MainSheerKeelLineTransitionPractice Z-axis Stem/Stern profile intersections.
    /// - Transitions 3D breadth shapes between Hybrid Main Sheer Ribband and Top Timber Ribband.
    /// </summary>
    public List<List<Vector3>> GetBulwarkStackedRibbands(float sideSign)
    {
        List<List<Vector3>> ribbandStack = new List<List<Vector3>>();
        if (sheerPlan == null || breadthPlan == null) return ribbandStack;

        sheerPlan.UpdateCachedValues();
        breadthPlan.UpdateCachedCurves();

        MainSheerKeelLineTransitionPractice transPractice = FindFirstObjectByType<MainSheerKeelLineTransitionPractice>();
        BodyPlanLowerMouldPointsPractice bodyPointsPractice = FindFirstObjectByType<BodyPlanLowerMouldPointsPractice>();

        float midSheerY = sheerPlan.GetMainSheerYAtZ(0f);
        float midTopY = sheerPlan.GetPureSheerYAtZ(0f);
        float topSpan = Mathf.Max(0.001f, midTopY - midSheerY);

        float zSheerStern = sheerPlan.cachedMainSheerSternIntersect.z;
        float zSheerBow = sheerPlan.cachedMainSheerBowIntersect.z;
        float zTopStern = sheerPlan.cachedTransomTopPoint.z;
        float zTopBow = sheerPlan.cachedUpperStemTopPoint.z;

        float xSheerMid = GetMainSheerRibbandXAtZ(0f);
        float xTopMid = breadthPlan.GetTopTimberXAtZ(0f);
        float bulwarkSpanX = Mathf.Max(0.001f, xTopMid - xSheerMid);

        List<Vector3> midshipPts = (bodyPointsPractice != null) ? bodyPointsPractice.GetSampledPoints(1f) : null;
        List<Vector3> bulwarkRegionPts = new List<Vector3>();

        if (midshipPts != null && midshipPts.Count > 0)
        {
            foreach (Vector3 pt in midshipPts)
            {
                if (pt.y > midSheerY - 0.0001f && pt.y <= midTopY + 0.0001f)
                {
                    bulwarkRegionPts.Add(pt);
                }
            }
        }

        if (bulwarkRegionPts.Count == 0)
        {
            float interval = Mathf.Max(0.01f, pointInterval);
            int n = Mathf.Max(1, Mathf.RoundToInt(topSpan / interval));
            for (int k = 0; k <= n; k++)
            {
                float u = (float)k / n;
                float y = Mathf.Lerp(midSheerY, midTopY, u);
                float x = (sheerPlan.bodyPlan != null) ? sheerPlan.bodyPlan.GetUpperMouldXAtY(y) : Mathf.Lerp(xSheerMid, xTopMid, u);
                bulwarkRegionPts.Add(new Vector3(x, y, 0f));
            }
        }

        int halfRes = Mathf.Max(5, resolution / 2);

        foreach (Vector3 pt in bulwarkRegionPts)
        {
            float u = Mathf.Clamp01((pt.y - midSheerY) / topSpan);

            float zSternEnd = (transPractice != null) ? transPractice.GetSternIntersectionZForBulwark(u) : Mathf.Lerp(zSheerStern, zTopStern, u);
            float zBowEnd = (transPractice != null) ? transPractice.GetStemIntersectionZForBulwark(u) : Mathf.Lerp(zSheerBow, zTopBow, u);

            float uEff = (Mathf.Abs(xTopMid - xSheerMid) > 0.0001f) ? Mathf.Clamp01((pt.x - xSheerMid) / (xTopMid - xSheerMid)) : u;

            List<Vector3> pts = new List<Vector3>();

            // 1. Aft Section (Stern End -> Midships Z=0)
            for (int i = 0; i <= halfRes; i++)
            {
                float s = 1f - (float)i / halfRes;
                float z = s * zSternEnd;

                float sheerY = sheerPlan.GetMainSheerYAtZ(z);
                float topY = sheerPlan.GetPureSheerYAtZ(z);
                float y = Mathf.Lerp(sheerY, topY, u);

                float xSheerRib = GetMainSheerRibbandXAtZ(z);
                float xTT_top = breadthPlan.GetTopTimberXAtZ(z);
                float x = Mathf.Lerp(xSheerRib, xTT_top, uEff);

                pts.Add(new Vector3(sideSign * x, y, z));
            }

            // 2. Forward Section (Midships Z=0 -> Bow End)
            for (int i = 1; i <= halfRes; i++)
            {
                float s = (float)i / halfRes;
                float z = s * zBowEnd;

                float sheerY = sheerPlan.GetMainSheerYAtZ(z);
                float topY = sheerPlan.GetPureSheerYAtZ(z);
                float y = Mathf.Lerp(sheerY, topY, u);

                float xSheerRib = GetMainSheerRibbandXAtZ(z);
                float xTT_top = breadthPlan.GetTopTimberXAtZ(z);
                float x = Mathf.Lerp(xSheerRib, xTT_top, uEff);

                pts.Add(new Vector3(sideSign * x, y, z));
            }

            ribbandStack.Add(pts);
        }

        return ribbandStack;
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

        m_CachedKeelLine = GetKeelLine();

        m_CachedStbdLowerRibbands = GetLowerStackedRibbands(1f);
        m_CachedPortLowerRibbands = GetLowerStackedRibbands(-1f);

        m_CachedStbdFloorRibband = GetFloorBreadthRibband(1f);
        m_CachedPortFloorRibband = GetFloorBreadthRibband(-1f);

        m_CachedStbdMiddleRibbands = GetMiddleStackedRibbands(1f);
        m_CachedPortMiddleRibbands = GetMiddleStackedRibbands(-1f);

        m_CachedStbdMaxBreadthRibband = GetMaxBreadthRibband(1f);
        m_CachedPortMaxBreadthRibband = GetMaxBreadthRibband(-1f);

        m_CachedStbdMainSheerRibband = GetMainSheerRibband(1f);
        m_CachedPortMainSheerRibband = GetMainSheerRibband(-1f);

        m_CachedStbdBulwarkRibbands = GetBulwarkStackedRibbands(1f);
        m_CachedPortBulwarkRibbands = GetBulwarkStackedRibbands(-1f);

        m_CachedStbdTopTimberRibband = GetTopTimberBreadthRibband(1f);
        m_CachedPortTopTimberRibband = GetTopTimberBreadthRibband(-1f);

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

        // 1. Keel Line
        if (showKeelLine && m_CachedKeelLine != null)
        {
            DrawPolyline(m_CachedKeelLine, keelColor);
        }

        // 2. Lower Stacked Ribbands (Keel -> Floor Rising Line)
        if (showLowerStackedRibbands && m_CachedStbdLowerRibbands != null)
        {
            foreach (var rib in m_CachedStbdLowerRibbands)
            {
                if (showStarboard) DrawPolyline(rib, lowerRibbandColor);
            }
            foreach (var rib in m_CachedPortLowerRibbands)
            {
                if (showPort) DrawPolyline(rib, lowerRibbandColor);
            }
        }

        // 3. Floor Breadth Ribbands
        if (showFloorRibband)
        {
            if (showStarboard && m_CachedStbdFloorRibband != null) DrawPolyline(m_CachedStbdFloorRibband, floorRibbandColor);
            if (showPort && m_CachedPortFloorRibband != null) DrawPolyline(m_CachedPortFloorRibband, floorRibbandColor);
        }

        // 4. Middle Stacked Ribbands (Floor Rising Line -> Main Sheer Line)
        if (showMiddleStackedRibbands && m_CachedStbdMiddleRibbands != null)
        {
            foreach (var rib in m_CachedStbdMiddleRibbands)
            {
                if (showStarboard) DrawPolyline(rib, middleRibbandColor);
            }
            foreach (var rib in m_CachedPortMiddleRibbands)
            {
                if (showPort) DrawPolyline(rib, middleRibbandColor);
            }
        }

        // 5. Maximum Breadth Ribbands
        if (showMaxBreadthRibband)
        {
            if (showStarboard && m_CachedStbdMaxBreadthRibband != null) DrawPolyline(m_CachedStbdMaxBreadthRibband, maxBreadthRibbandColor);
            if (showPort && m_CachedPortMaxBreadthRibband != null) DrawPolyline(m_CachedPortMaxBreadthRibband, maxBreadthRibbandColor);
        }

        // 6. Hybrid Main Sheer Line Ribbands
        if (showMainSheerRibband)
        {
            if (showStarboard && m_CachedStbdMainSheerRibband != null) DrawPolyline(m_CachedStbdMainSheerRibband, mainSheerRibbandColor);
            if (showPort && m_CachedPortMainSheerRibband != null) DrawPolyline(m_CachedPortMainSheerRibband, mainSheerRibbandColor);
        }

        // 7. Bulwark Stacked Ribbands (Main Sheer Line -> Top Timber Line)
        if (showBulwarkStackedRibbands && m_CachedStbdBulwarkRibbands != null)
        {
            foreach (var rib in m_CachedStbdBulwarkRibbands)
            {
                if (showStarboard) DrawPolyline(rib, bulwarkRibbandColor);
            }
            foreach (var rib in m_CachedPortBulwarkRibbands)
            {
                if (showPort) DrawPolyline(rib, bulwarkRibbandColor);
            }
        }

        // 8. Top Timber Breadth Ribbands
        if (showTopTimberRibband)
        {
            if (showStarboard && m_CachedStbdTopTimberRibband != null) DrawPolyline(m_CachedStbdTopTimberRibband, topTimberRibbandColor);
            if (showPort && m_CachedPortTopTimberRibband != null) DrawPolyline(m_CachedPortTopTimberRibband, topTimberRibbandColor);
        }
    }

    private void DrawPolyline(List<Vector3> pts, Color color)
    {
        if (pts == null || pts.Count < 2) return;
        Gizmos.color = color;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Gizmos.DrawLine(pts[i], pts[i + 1]);
        }
    }
}
