using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Experimental Ribband Strakes Model.
/// Replicates bending actual flexible wooden plank ribbands across the hull surface,
/// following the continuous sheer lines and cutting cleanly at the Stem Profile
/// (bow post) and Stern Profile (fashion timbers / sternpost) without any singularities
/// or pinching at intermediate reference curve apexes.
/// Leaves StrakesModel.cs completely untouched.
/// </summary>
[ExecuteInEditMode]
public class RibbandStrakesModel : MonoBehaviour
{
    public enum ProfileIntersectionMarkerMode
    {
        OnStructuralTimbers,
        OnStrakes,
        OnBoth
    }

    public enum StrakeSpacingMode
    {
        TargetPhysicalSpacing,
        FixedStrakeCounts
    }

    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FashionTimberModel fashionTimberModel;

    [Header("Ribband Strake Layout Settings")]
    [Tooltip("Master toggle to show/hide experimental continuous surface-batten ribbands.")]
    public bool showRibbands = true;

    [Tooltip("Method for spacing strakes (TargetPhysicalSpacing calculates counts dynamically from Master Frame Z=0 arc length; FixedStrakeCounts uses manual sliders).")]
    public StrakeSpacingMode spacingMode = StrakeSpacingMode.TargetPhysicalSpacing;

    [Range(0.05f, 1.0f)]
    [Tooltip("Target physical spacing distance between adjacent strakes along the Master Frame (Z=0) 3D arc length (meters). Default is 0.25m (25 cm).")]
    public float targetStrakeSpacing = 0.25f;

    [Header("Master Frame (Z=0) Dynamic Measurements (Read-Only)")]
    [Tooltip("Calculated 3D arc length of Lower-Lower Mould at Master Frame Z=0 (meters).")]
    public float masterFrameLLArcLength;

    [Tooltip("Calculated 3D arc length of Upper-Lower Mould at Master Frame Z=0 (meters).")]
    public float masterFrameULArcLength;

    [Tooltip("Calculated 3D arc length of Upper Mould at Master Frame Z=0 (meters).")]
    public float masterFrameUMArcLength;

    [Tooltip("Dynamically calculated strake count for Lower-Lower Mould based on target spacing.")]
    public int calculatedLLStrakeCount;

    [Tooltip("Dynamically calculated strake count for Upper-Lower Mould based on target spacing.")]
    public int calculatedULStrakeCount;

    [Tooltip("Dynamically calculated strake count for Upper Mould based on target spacing.")]
    public int calculatedUMStrakeCount;

    [Header("Fixed Strake Count Sliders (FixedStrakeCounts Mode Only)")]
    [Range(2, 20)]
    [Tooltip("Number of strake lines in Lower-Lower Mould (Keel to Connection/Floor Line). Used in FixedStrakeCounts mode.")]
    public int strakesPerLowerLowerMould = 5;

    [Range(2, 20)]
    [Tooltip("Number of strake lines in Upper-Lower Mould (Connection/Floor Line to Main Deck Sheer). Used in FixedStrakeCounts mode.")]
    public int strakesPerUpperLowerMould = 5;

    [Range(2, 20)]
    [Tooltip("Number of strake lines in Upper Mould (Main Deck Sheer to Top Line Sheer). Used in FixedStrakeCounts mode.")]
    public int strakesPerUpperMould = 5;

    [Tooltip("Toggle to display Upper Mould ribband strakes.")]
    public bool showUpperMouldRibbands = true;

    [Range(20, 300)]
    [Tooltip("Longitudinal sample resolution per ribband line across Z (continuous batten quality).")]
    public int ribbandSamplesPerStrake = 120;

    [Tooltip("Respect upper deck boundaries and stem/stern profile cutoffs.")]
    public bool respectFrameCutoffs = true;

    [Header("Longitudinal Fairing")]
    [Tooltip("Enable continuous 3D multi-axis longitudinal fairing across samples Z.")]
    public bool enableLongitudinalFairing = true;

    [Range(0f, 1f)]
    [Tooltip("Smoothing blend factor for longitudinal fairing.")]
    public float fairingStrength = 0.5f;

    [Range(1, 10)]
    [Tooltip("Number of binomial smoothing passes.")]
    public int fairingPasses = 3;

    [Header("Profile Intersection Markers")]
    [Tooltip("Show sphere node markers at Stem and Stern profile intersection points.")]
    public bool showProfileIntersectionPoints = true;

    public ProfileIntersectionMarkerMode intersectionMarkerMode = ProfileIntersectionMarkerMode.OnBoth;

    [Range(0.01f, 0.2f)]
    public float profileIntersectionRadius = 0.06f;

    public Color stemIntersectionColor = new Color(0.2f, 1f, 0.4f, 1f);
    public Color sternIntersectionColor = new Color(1f, 0.3f, 0.2f, 1f);

    [Header("Ribband Gizmo Colors")]
    public Color lowerLowerColor = Color.yellow;
    public Color upperLowerColor = Color.cyan;
    public Color upperMouldColor = Color.magenta;

    // Cache structures
    private readonly List<List<Vector3>> cachedPortLLRibbands = new List<List<Vector3>>();
    private readonly List<List<Vector3>> cachedStbdLLRibbands = new List<List<Vector3>>();

    private readonly List<List<Vector3>> cachedPortULRibbands = new List<List<Vector3>>();
    private readonly List<List<Vector3>> cachedStbdULRibbands = new List<List<Vector3>>();

    private readonly List<List<Vector3>> cachedPortUMRibbands = new List<List<Vector3>>();
    private readonly List<List<Vector3>> cachedStbdUMRibbands = new List<List<Vector3>>();

    // Profile Marker Cache
    private readonly List<Vector3> cachedStemStrakeMarkers = new List<Vector3>();
    private readonly List<Vector3> cachedStemTimberMarkers = new List<Vector3>();
    private readonly List<Vector3> cachedSternStrakeMarkers = new List<Vector3>();
    private readonly List<Vector3> cachedSternTimberMarkers = new List<Vector3>();

    private bool isCacheDirty = true;

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        isCacheDirty = true;
        FindReferences();
    }

    public void FindReferences()
    {
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
        if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();
        if (fashionTimberModel == null) fashionTimberModel = FindFirstObjectByType<FashionTimberModel>();
    }

    public void MarkCacheDirty()
    {
        isCacheDirty = true;
    }

    /// <summary>
    /// Evaluates a 3D point on the frame surface at station z and height targetY.
    /// Handles Pseudo Frame vertical line for points below section curve bottom.
    /// </summary>
    public Vector3 GetStrakeFramePointAtY(float z, float side, float targetY)
    {
        if (framingModel == null) return Vector3.zero;

        float hw = framingModel.GetKeelHalfWidth();

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (framingModel.GetFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
        {
            if (targetY < pL0.y - 0.0005f)
            {
                return new Vector3(side * hw, targetY, z);
            }

            Vector3 pt = framingModel.GetTransverseFramePointAtY(z, side, targetY);
            if (pt != Vector3.zero) return pt;
        }

        return new Vector3(side * hw, targetY, z);
    }

    public float CalculateMasterFrameLLArcLength()
    {
        if (framingModel == null || sheerPlan == null) return 0f;
        const int samples = 50;
        float totalArc = 0f;

        float keelY = framingModel.GetFrameBottomYAtZ(0f);
        float connY = sheerPlan.GetProjectedBowRisingYAtZ(0f);
        Vector3 prevPt = GetStrakeFramePointAtY(0f, 1f, keelY);

        for (int i = 1; i <= samples; i++)
        {
            float y = Mathf.Lerp(keelY, connY, (float)i / samples);
            Vector3 currPt = GetStrakeFramePointAtY(0f, 1f, y);
            totalArc += Vector3.Distance(prevPt, currPt);
            prevPt = currPt;
        }

        return totalArc;
    }

    public float CalculateMasterFrameULArcLength()
    {
        if (framingModel == null || sheerPlan == null) return 0f;
        const int samples = 50;
        float totalArc = 0f;

        float connY = sheerPlan.GetProjectedBowRisingYAtZ(0f);
        float deckY = sheerPlan.GetBaseDeckYAtZ(0f);
        Vector3 prevPt = GetStrakeFramePointAtY(0f, 1f, connY);

        for (int i = 1; i <= samples; i++)
        {
            float y = Mathf.Lerp(connY, deckY, (float)i / samples);
            Vector3 currPt = GetStrakeFramePointAtY(0f, 1f, y);
            totalArc += Vector3.Distance(prevPt, currPt);
            prevPt = currPt;
        }

        return totalArc;
    }

    public float CalculateMasterFrameUMArcLength()
    {
        if (framingModel == null || sheerPlan == null) return 0f;
        const int samples = 50;
        float totalArc = 0f;

        float deckY = sheerPlan.GetBaseDeckYAtZ(0f);
        float topY = sheerPlan.GetPureSheerYAtZ(0f);
        Vector3 prevPt = GetStrakeFramePointAtY(0f, 1f, deckY);

        for (int i = 1; i <= samples; i++)
        {
            float y = Mathf.Lerp(deckY, topY, (float)i / samples);
            Vector3 currPt = GetStrakeFramePointAtY(0f, 1f, y);
            totalArc += Vector3.Distance(prevPt, currPt);
            prevPt = currPt;
        }

        return totalArc;
    }

    private void RefreshCacheIfNeeded()
    {
        if (!isCacheDirty) return;
        FindReferences();
        if (sheerPlan == null || framingModel == null) return;

        sheerPlan.UpdateCachedValues();

        cachedPortLLRibbands.Clear();
        cachedStbdLLRibbands.Clear();
        cachedPortULRibbands.Clear();
        cachedStbdULRibbands.Clear();
        cachedPortUMRibbands.Clear();
        cachedStbdUMRibbands.Clear();

        cachedStemStrakeMarkers.Clear();
        cachedStemTimberMarkers.Clear();
        cachedSternStrakeMarkers.Clear();
        cachedSternTimberMarkers.Clear();

        int llCount, ulCount, umCount;

        if (spacingMode == StrakeSpacingMode.TargetPhysicalSpacing)
        {
            float spacing = Mathf.Max(0.01f, targetStrakeSpacing);

            masterFrameLLArcLength = CalculateMasterFrameLLArcLength();
            masterFrameULArcLength = CalculateMasterFrameULArcLength();
            masterFrameUMArcLength = CalculateMasterFrameUMArcLength();

            calculatedLLStrakeCount = Mathf.Max(2, Mathf.RoundToInt(masterFrameLLArcLength / spacing) + 1);
            calculatedULStrakeCount = Mathf.Max(2, Mathf.RoundToInt(masterFrameULArcLength / spacing) + 1);
            calculatedUMStrakeCount = Mathf.Max(2, Mathf.RoundToInt(masterFrameUMArcLength / spacing) + 1);

            llCount = calculatedLLStrakeCount;
            ulCount = calculatedULStrakeCount;
            umCount = calculatedUMStrakeCount;
        }
        else
        {
            llCount = Mathf.Max(2, strakesPerLowerLowerMould);
            ulCount = Mathf.Max(2, strakesPerUpperLowerMould);
            umCount = Mathf.Max(2, strakesPerUpperMould);

            masterFrameLLArcLength = CalculateMasterFrameLLArcLength();
            masterFrameULArcLength = CalculateMasterFrameULArcLength();
            masterFrameUMArcLength = CalculateMasterFrameUMArcLength();

            calculatedLLStrakeCount = llCount;
            calculatedULStrakeCount = ulCount;
            calculatedUMStrakeCount = umCount;
        }

        // Build Port (-1) and Starboard (1) continuous plank ribbands
        RebuildContinuousPlankSegment(llCount, -1f, 0, cachedPortLLRibbands);
        RebuildContinuousPlankSegment(llCount, 1f, 0, cachedStbdLLRibbands);

        RebuildContinuousPlankSegment(ulCount, -1f, 1, cachedPortULRibbands);
        RebuildContinuousPlankSegment(ulCount, 1f, 1, cachedStbdULRibbands);

        if (showUpperMouldRibbands)
        {
            RebuildContinuousPlankSegment(umCount, -1f, 2, cachedPortUMRibbands);
            RebuildContinuousPlankSegment(umCount, 1f, 2, cachedStbdUMRibbands);
        }

        isCacheDirty = false;
    }

    /// <summary>
    /// Evaluates continuous plank ribbands for a mould segment.
    /// Planks bend continuously across the hull surface following sheer curves,
    /// and terminate at exact Stem Profile and Stern Profile (Fashion Timber) intersection points
    /// with ZERO pinching or singularities.
    /// segmentType: 0 = LL (Keel to Connection Line), 1 = UL (Connection Line to Main Deck Sheer), 2 = UM (Main Deck Sheer to Top Line)
    /// </summary>
    private void RebuildContinuousPlankSegment(
        int strakeCount,
        float side,
        int segmentType,
        List<List<Vector3>> outRibbandList)
    {
        if (sheerPlan == null || framingModel == null) return;

        float sternZ = sheerPlan.GetAftMostZ();
        float bowZ = sheerPlan.GetFwdMostZ();
        int samples = Mathf.Clamp(ribbandSamplesPerStrake, 20, 300);

        for (int k = 0; k < strakeCount; k++)
        {
            float frac = (float)k / (strakeCount - 1);

            // 1. Evaluate Target Elevation Function Y_target(z) for Strake k
            System.Func<float, float> getTargetY = (z) =>
            {
                if (segmentType == 0)
                {
                    // Lower-Lower Mould: baseline Keel to projected Rising Line height
                    float keelY = framingModel.GetFrameBottomYAtZ(z);
                    float connY = sheerPlan.GetProjectedBowRisingYAtZ(z);
                    return Mathf.Lerp(keelY, connY, frac);
                }
                else if (segmentType == 1)
                {
                    // Upper-Lower Mould: projected Rising Line height to Main Deck Sheer height
                    float startY = sheerPlan.GetProjectedBowRisingYAtZ(z);
                    float deckY = sheerPlan.GetBaseDeckYAtZ(z);
                    return Mathf.Lerp(startY, deckY, frac);
                }
                else
                {
                    // Upper Mould: Main Deck Sheer height to un-cut Pure Top Line Sheer height (strakes never drop down in the waist)
                    float deckY = sheerPlan.GetBaseDeckYAtZ(z);
                    float topY = sheerPlan.GetPureSheerYAtZ(z);
                    return Mathf.Lerp(deckY, topY, frac);
                }
            };

            // 2. Find Exact Stem Profile Intersection Z_stem (Bisection Search)
            float zStem = FindStemIntersectionZ(getTargetY, bowZ);

            // 3. Find Exact Stern Profile Intersection Z_stern (Bisection Search)
            float zStern = FindSternIntersectionZ(getTargetY, sternZ);

            if (zStem <= zStern + 0.01f) continue;

            // 4. Evaluate Continuous 3D Plank Points P(z) across Z in [zStern, zStem]
            List<Vector3> ribbandPts = new List<Vector3>(samples);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / (samples - 1);
                float z = Mathf.Lerp(zStern, zStem, t);
                float targetY = getTargetY(z);

                Vector3 pt = GetStrakeFramePointAtY(z, side, targetY);

                // Upper Deck & Bulwark Frame Cutoffs
                if (respectFrameCutoffs)
                {
                    float localTopY = sheerPlan.GetTopTimberYAtZ(z);
                    if (pt.y > localTopY + 0.005f)
                    {
                        pt = Vector3.zero;
                    }
                }

                ribbandPts.Add(pt);
            }

            // 5. Perform 3D Vector Longitudinal Fairing across Samples
            if (enableLongitudinalFairing && samples >= 3)
            {
                Apply3DLongitudinalFairing(ribbandPts, side);
            }

            outRibbandList.Add(ribbandPts);

            // 6. Cache Stem & Stern Profile Markers
            if (ribbandPts.Count > 0)
            {
                Vector3 stemPt = ribbandPts[ribbandPts.Count - 1];
                if (stemPt != Vector3.zero)
                {
                    cachedStemStrakeMarkers.Add(stemPt);
                    float hw = framingModel.GetKeelHalfWidth();
                    cachedStemTimberMarkers.Add(new Vector3(side * hw, stemPt.y, stemPt.z));
                }

                Vector3 sternPt = ribbandPts[0];
                if (sternPt != Vector3.zero)
                {
                    cachedSternStrakeMarkers.Add(sternPt);
                    float fashionX = (fashionTimberModel != null)
                        ? fashionTimberModel.GetNaturalHullXAtYZ(sternPt.y, sternPt.z)
                        : Mathf.Abs(framingModel.GetTransverseFramePointAtY(sternPt.z, 1f, sternPt.y).x);
                    cachedSternTimberMarkers.Add(new Vector3(side * fashionX, sternPt.y, sternPt.z));
                }
            }
        }
    }

    private void Apply3DLongitudinalFairing(List<Vector3> pts, float side)
    {
        if (pts == null || pts.Count < 3) return;

        int passes = Mathf.Clamp(fairingPasses, 1, 10);
        float alpha = Mathf.Clamp01(fairingStrength);
        int count = pts.Count;

        for (int p = 0; p < passes; p++)
        {
            Vector3[] temp = new Vector3[count];
            temp[0] = pts[0];
            temp[count - 1] = pts[count - 1];

            for (int i = 1; i < count - 1; i++)
            {
                if (pts[i] == Vector3.zero || pts[i - 1] == Vector3.zero || pts[i + 1] == Vector3.zero)
                {
                    temp[i] = pts[i];
                    continue;
                }

                Vector3 smoothed = (pts[i - 1] + 2f * pts[i] + pts[i + 1]) * 0.25f;
                Vector3 blended = Vector3.Lerp(pts[i], smoothed, alpha);

                // Re-project blended Y onto 3D frame surface to maintain 100% surface lock
                temp[i] = GetStrakeFramePointAtY(pts[i].z, side, blended.y);
            }

            for (int i = 0; i < count; i++)
            {
                pts[i] = temp[i];
            }
        }
    }

    private float FindStemIntersectionZ(System.Func<float, float> getTargetY, float bowZ)
    {
        float minZ = 0f;
        float maxZ = bowZ;
        float targetZ = maxZ;

        for (int iter = 0; iter < 12; iter++)
        {
            float z = (minZ + maxZ) * 0.5f;
            float y = getTargetY(z);
            float stemProfileZ = sheerPlan.GetStemProfileZAtY(y);

            if (z >= stemProfileZ)
            {
                targetZ = z;
                maxZ = z;
            }
            else
            {
                minZ = z;
            }
        }

        return targetZ;
    }

    private float FindSternIntersectionZ(System.Func<float, float> getTargetY, float sternZ)
    {
        float minZ = sternZ;
        float maxZ = 0f;
        float targetZ = minZ;

        for (int iter = 0; iter < 12; iter++)
        {
            float z = (minZ + maxZ) * 0.5f;
            float y = getTargetY(z);
            float sternProfileZ = sheerPlan.GetSternProfileZAtY(y);

            if (z <= sternProfileZ)
            {
                targetZ = z;
                minZ = z;
            }
            else
            {
                maxZ = z;
            }
        }

        return targetZ;
    }

    private void OnDrawGizmos()
    {
        if (!showRibbands) return;
        RefreshCacheIfNeeded();
        if (framingModel == null || sheerPlan == null) return;

        // 1. Draw LL Ribbands
        Gizmos.color = lowerLowerColor;
        DrawRibbandList(cachedPortLLRibbands);
        DrawRibbandList(cachedStbdLLRibbands);

        // 2. Draw UL Ribbands
        Gizmos.color = upperLowerColor;
        DrawRibbandList(cachedPortULRibbands);
        DrawRibbandList(cachedStbdULRibbands);

        // 3. Draw UM Ribbands
        if (showUpperMouldRibbands)
        {
            Gizmos.color = upperMouldColor;
            DrawRibbandList(cachedPortUMRibbands);
            DrawRibbandList(cachedStbdUMRibbands);
        }

        // 4. Draw Profile Markers
        if (showProfileIntersectionPoints)
        {
            bool drawTimbers = (intersectionMarkerMode == ProfileIntersectionMarkerMode.OnStructuralTimbers || intersectionMarkerMode == ProfileIntersectionMarkerMode.OnBoth);
            bool drawStrakes = (intersectionMarkerMode == ProfileIntersectionMarkerMode.OnStrakes || intersectionMarkerMode == ProfileIntersectionMarkerMode.OnBoth);

            Gizmos.color = stemIntersectionColor;
            if (drawTimbers) foreach (Vector3 pt in cachedStemTimberMarkers) Gizmos.DrawSphere(pt, profileIntersectionRadius);
            if (drawStrakes) foreach (Vector3 pt in cachedStemStrakeMarkers) Gizmos.DrawSphere(pt, profileIntersectionRadius);

            Gizmos.color = sternIntersectionColor;
            if (drawTimbers) foreach (Vector3 pt in cachedSternTimberMarkers) Gizmos.DrawSphere(pt, profileIntersectionRadius);
            if (drawStrakes) foreach (Vector3 pt in cachedSternStrakeMarkers) Gizmos.DrawSphere(pt, profileIntersectionRadius);
        }
    }

    private void DrawRibbandList(List<List<Vector3>> ribbandGroup)
    {
        if (ribbandGroup == null) return;

        foreach (List<Vector3> pts in ribbandGroup)
        {
            if (pts == null || pts.Count < 2) continue;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 p1 = pts[i];
                Vector3 p2 = pts[i + 1];

                if (p1 != Vector3.zero && p2 != Vector3.zero)
                {
                    Gizmos.DrawLine(p1, p2);
                }
                else if (p1 != Vector3.zero && p2 == Vector3.zero)
                {
                    Vector3 edgePt = FindDeckEdgeCutoffPoint(p1, pts, i, 1);
                    if (edgePt != Vector3.zero) Gizmos.DrawLine(p1, edgePt);
                }
                else if (p1 == Vector3.zero && p2 != Vector3.zero)
                {
                    Vector3 edgePt = FindDeckEdgeCutoffPoint(p2, pts, i + 1, -1);
                    if (edgePt != Vector3.zero) Gizmos.DrawLine(edgePt, p2);
                }
            }
        }
    }

    private Vector3 FindDeckEdgeCutoffPoint(Vector3 validPt, List<Vector3> pts, int validIdx, int direction)
    {
        if (sheerPlan == null || framingModel == null) return Vector3.zero;

        int nextIdx = validIdx + direction;
        if (nextIdx < 0 || nextIdx >= pts.Count) return Vector3.zero;

        float zValid = validPt.z;
        float zCut = validPt.z + direction * (framingModel.frameSpacing > 0.05f ? framingModel.frameSpacing : 0.8f);

        float minZ = Mathf.Min(zValid, zCut);
        float maxZ = Mathf.Max(zValid, zCut);

        float targetEdgeZ = zValid;
        for (int iter = 0; iter < 6; iter++)
        {
            float midZ = (minZ + maxZ) * 0.5f;
            float topY = sheerPlan.GetTopTimberYAtZ(midZ);
            if (validPt.y <= topY + 0.005f)
            {
                targetEdgeZ = midZ;
                if (direction > 0) minZ = midZ; else maxZ = midZ;
            }
            else
            {
                if (direction > 0) maxZ = midZ; else minZ = midZ;
            }
        }

        return GetStrakeFramePointAtY(targetEdgeZ, Mathf.Sign(validPt.x), validPt.y);
    }
}
