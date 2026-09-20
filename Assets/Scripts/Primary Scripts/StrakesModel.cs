using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// StrakesModel places and visualizes the two primary defining strake reference lines:
/// 1. Connection Point Sheer Strake Line (pLR): dividing the floor sweep from the side hull.
/// 2. Main Deck Sheer Strake Line: dividing the lower hull from the upper hull / bulwark.
/// Supports a clean 2-line reference mode as well as optional intermediate strake subdivisions.
/// </summary>
[ExecuteInEditMode]
public class StrakesModel : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Reference to the SheerPlan to get Bow Rising End, Keel, and Sheer Plan endpoints.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the BodyPlan to get Master Frame lower mould settings.")]
    public BodyPlan bodyPlan;

    [Tooltip("Reference to the BreadthPlan to get half-breadth curves.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the FramingModel to evaluate 3D frame sections.")]
    public FramingModel framingModel;

    [Tooltip("Reference to the FashionTimberModel for stern fashion timber profile points.")]
    public FashionTimberModel fashionTimberModel;

    [Header("Strake Mode Settings")]
    [Tooltip("Show Strake Points and Guide Lines in Scene view gizmos.")]
    public bool showStrakes = true;

    [Tooltip("If false, renders ONLY the 2 primary defining reference lines (Connection Point & Main Deck Sheer). If true, also renders intermediate strake subdivisions.")]
    public bool showIntermediateStrakes = true;

    [Header("Primary Defining Line #1 - Main Deck Sheer Line")]
    [Tooltip("Show the Main Deck Sheer Strake Line (dividing lower mould from upper mould).")]
    public bool showMainDeckSheerLine = true;

    [Tooltip("Color of the Main Deck Sheer Strake Line.")]
    public Color mainDeckSheerColor = new Color(0.1f, 0.95f, 0.95f, 1f);

    [Tooltip("Lock the Main Deck Sheer strake line to align 100% exactly with the Sheer Plan Base Deck Sheer line.")]
    public bool lockMainDeckSheerToSheerPlan = true;

    [Header("Primary Defining Line #2 - Connection Point Line")]
    [Tooltip("Show the Connection Point Strake Line (pLR dividing lower mould below and above floor).")]
    public bool showConnectionPointLine = true;

    [Tooltip("Color of the Connection Point Strake Line.")]
    public Color connectionPointColor = new Color(1f, 0.2f, 0.8f, 1f);

    [Tooltip("Lock the Connection Point strake line to align 100% exactly with the Sheer Plan Rising Line.")]
    public bool lockConnectionPointToRisingLine = true;

    [Header("Intermediate Strake Subdivisions Settings")]
    [Tooltip("Number of strake points along Lower-Lower Mould segment (from Keel pL0 to Connection Point pLR).")]
    [Range(2, 20)]
    public int strakesPerLowerLowerMould = 5;

    [Tooltip("Number of strake points along Upper-Lower Mould segment (from Connection Point pLR up to Main Deck Sheer line).")]
    [Range(2, 20)]
    public int strakesPerUpperLowerMould = 5;

    [Tooltip("Show Upper Mould Strake Points and Guide Lines (from Main Deck Sheer line up to Top Timber Sheer line).")]
    public bool showUpperMouldStrakes = true;

    [Tooltip("Number of strake points along Upper Mould segment (Main Deck Sheer line to Top Timber Sheer line).")]
    [Range(2, 20)]
    public int strakesPerUpperMould = 5;

    [Tooltip("Color of Upper Mould strake point nodes.")]
    public Color upperMouldPointColor = new Color(0.9f, 0.4f, 1f, 1f);

    [Tooltip("Color of Upper Mould longitudinal guide lines.")]
    public Color upperMouldLineColor = new Color(0.95f, 0.6f, 1f, 0.8f);

    [Tooltip("Color of Lower-Lower Mould strake point spheres.")]
    public Color lowerLowerPointColor = new Color(0.1f, 0.9f, 0.4f, 1f);

    [Tooltip("Color of Lower-Lower Mould longitudinal guide lines.")]
    public Color lowerLowerLineColor = new Color(0.2f, 1f, 0.7f, 0.8f);

    [Tooltip("Color of Upper-Lower Mould strake point spheres.")]
    public Color upperLowerPointColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("Color of Upper-Lower Mould longitudinal guide lines.")]
    public Color upperLowerLineColor = new Color(1f, 0.92f, 0.4f, 0.8f);

    [Header("Frame Cutoffs & Node Settings")]
    [Tooltip("Clip Upper Mould strakes to respect frame cutoffs (e.g. Top Timber sheer height and Fashion Timber cuts).")]
    public bool respectFrameCutoffs = true;

    [Tooltip("Lock top defining strake line to align 100% exactly with the Sheer Plan Top Timber Sheer line.")]
    public bool lockTopStrakeToSheerPlan = true;

    [Tooltip("Size of the strake point node markers in Scene view.")]
    [Range(0.01f, 0.3f)]
    public float pointRadius = 0.06f;

    [Tooltip("Connect strake points across adjacent frame stations to form longitudinal strake guide lines.")]
    public bool showStrakeLines = true;

    [Header("Strake Smoothing & Longitudinal Fairing Settings")]
    [Tooltip("Enable Y-Profile Longitudinal Fairing to smooth the side-view Y-height profile of strakes across stations Z.")]
    public bool enableLongitudinalFairing = true;

    [Range(0f, 2f)]
    [Tooltip("Strength blend for Y-Profile Fairing (0 = raw station math, 1 = 100% smoothed blend, >1 = over-relaxation for maximum curvature stiffness).")]
    public float fairingStrengthY = 1.0f;

    [Range(0f, 2f)]
    [Tooltip("Strength blend for X-Breadth Fairing (0 = raw station math, 1 = 100% smoothed blend, >1 = over-relaxation for maximum curvature stiffness). Eliminates Z/X and X/Y wavy ripples.")]
    public float fairingStrengthX = 1.0f;

    [Tooltip("Legacy single fairing strength setting (controls both X and Y if set).")]
    public float fairingStrength { get => fairingStrengthY; set { fairingStrengthY = value; fairingStrengthX = value; } }

    [Range(1, 20)]
    [Tooltip("Number of longitudinal smoothing passes (higher values apply stronger longitudinal fairing across frames).")]
    public int fairingPasses = 5;

    [Tooltip("Enable graduated non-linear tapering from Keel to Main Deck Sheer.")]
    public bool enableStrakeSmoothing = true;

    [Range(0.3f, 1.0f)]
    [Tooltip("Minimum width ratio limit for strakes near sheer relative to midship width.")]
    public float minHobTaperRatio = 0.5f;

public enum ProfileIntersectionMarkerMode
{
    OnStructuralTimbers,
    OnStrakes,
    OnBoth
}

    [Tooltip("Show strake index labels in Scene view.")]
    public bool showLabels = false;

    [Header("Stem & Stern Profile Intersection Settings")]
    [Tooltip("Show Stem and Stern profile intersection points on strakes in Scene view.")]
    public bool showProfileIntersectionPoints = true;

    [Tooltip("Controls whether profile intersection markers appear on the Structural Timbers (Stem/Fashion Timbers), on the Strake lines themselves, or on Both.")]
    public ProfileIntersectionMarkerMode intersectionMarkerMode = ProfileIntersectionMarkerMode.OnBoth;

    [Tooltip("Color of Stem profile intersection point node markers at the bow.")]
    public Color stemIntersectionColor = new Color(1f, 0.1f, 0.5f, 1f);

    [Tooltip("Color of Stern profile intersection point node markers at the stern.")]
    public Color sternIntersectionColor = new Color(0.1f, 0.6f, 1f, 1f);

    [Tooltip("Size of the Stem and Stern profile intersection point node markers in Scene view.")]
    [Range(0.01f, 0.3f)]
    public float profileIntersectionRadius = 0.08f;

    [Header("Structural & Marking Strake Lines")]
    [Tooltip("Show Structural / Marking Strake Lines (Main Deck Bulwark, Upper Decks, and Upper Deck Bulwarks).")]
    public bool showStructuralStrakes = true;

    [Range(20, 300)]
    [Tooltip("Number of longitudinal sampling points along each structural / marking ribband line (higher values create a silkier smooth 3D ribband curve).")]
    public int structuralStrakeSamples = 120;

    [Tooltip("Color of Main Deck Bulwark structural strake line.")]
    public Color mainDeckBulwarkColor = new Color(0.2f, 0.8f, 1f, 1f);

    [Tooltip("Color of Upper Deck structural strake lines.")]
    public Color upperDeckColor = new Color(1f, 0.7f, 0.2f, 1f);

    [Tooltip("Color of Upper Deck Bulwark structural strake lines.")]
    public Color upperDeckBulwarkColor = new Color(1f, 0.4f, 0.2f, 1f);

    // Static buffers for zero heap allocation during calculation passes
    private static readonly Vector3[] ptTableBuffer = new Vector3[64];
    private static readonly float[] distTableBuffer = new float[64];
    private static readonly float[] rawXBuffer = new float[512];
    private static readonly float[] targetXBuffer = new float[512];
    private static readonly float[] rawYBuffer = new float[512];
    private static readonly float[] targetYBuffer = new float[512];
    private static readonly bool[] validBuffer = new bool[512];

    // Caching fields
    private List<float> cachedValidStations = new List<float>();
    private float cachedMidshipLength = 0f;
    private bool isCacheDirty = true;

    // Cached fully faired 3D strake point data for ultra-fast Gizmo drawing
    private readonly Dictionary<int, List<Vector3>> cachedPortLLPoints = new Dictionary<int, List<Vector3>>();
    private readonly Dictionary<int, List<Vector3>> cachedStbdLLPoints = new Dictionary<int, List<Vector3>>();
    private readonly Dictionary<int, List<Vector3>> cachedPortULPoints = new Dictionary<int, List<Vector3>>();
    private readonly Dictionary<int, List<Vector3>> cachedStbdULPoints = new Dictionary<int, List<Vector3>>();
    private readonly Dictionary<int, List<Vector3>> cachedPortUMPoints = new Dictionary<int, List<Vector3>>();
    private readonly Dictionary<int, List<Vector3>> cachedStbdUMPoints = new Dictionary<int, List<Vector3>>();

    // Cached Structural Timber Profile Intersection Points
    private readonly List<Vector3> cachedPortStemIntersections = new List<Vector3>();
    private readonly List<Vector3> cachedStbdStemIntersections = new List<Vector3>();
    private readonly List<Vector3> cachedPortSternIntersections = new List<Vector3>();
    private readonly List<Vector3> cachedStbdSternIntersections = new List<Vector3>();

    // Cached Per-Segment Strake Profile Intersection Endpoints
    private readonly List<Vector3> cachedPortLLStemStrakes = new List<Vector3>();
    private readonly List<Vector3> cachedPortLLSternTimbers = new List<Vector3>();
    private readonly List<Vector3> cachedStbdLLStemStrakes = new List<Vector3>();
    private readonly List<Vector3> cachedStbdLLSternTimbers = new List<Vector3>();

    private readonly List<Vector3> cachedPortULStemStrakes = new List<Vector3>();
    private readonly List<Vector3> cachedPortULSternTimbers = new List<Vector3>();
    private readonly List<Vector3> cachedStbdULStemStrakes = new List<Vector3>();
    private readonly List<Vector3> cachedStbdULSternTimbers = new List<Vector3>();

    private readonly List<Vector3> cachedPortUMStemStrakes = new List<Vector3>();
    private readonly List<Vector3> cachedPortUMSternTimbers = new List<Vector3>();
    private readonly List<Vector3> cachedStbdUMStemStrakes = new List<Vector3>();
    private readonly List<Vector3> cachedStbdUMSternTimbers = new List<Vector3>();

    // Cached Structural & Marking Strake Lines
    private readonly List<List<Vector3>> cachedPortStructuralLines = new List<List<Vector3>>();
    private readonly List<List<Vector3>> cachedStbdStructuralLines = new List<List<Vector3>>();
    private readonly List<Color> cachedStructuralLineColors = new List<Color>();



    private void Awake()
    {
        FindReferences();
        SetDirty();
    }

    private void OnEnable()
    {
        FindReferences();
        SetDirty();
    }

    private void OnValidate()
    {
        SetDirty();
    }

    public void SetDirty()
    {
        isCacheDirty = true;
    }

    public void FindReferences()
    {
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
        if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();
        if (fashionTimberModel == null) fashionTimberModel = FindFirstObjectByType<FashionTimberModel>();
    }

    private void RefreshCacheIfNeeded()
    {
        if (!isCacheDirty) return;
        FindReferences();

        // 1. Cache Midship Length ONCE
        cachedMidshipLength = 0f;
        if (framingModel != null)
        {
            Vector3 mL0, mL1, mLR, mL2, mL3, mU0, mU1, mU2, mU3;
            if (framingModel.GetFrameControlPointsAtZ(0f, 1f, out mL0, out mL1, out mLR, out mL2, out mL3, out mU0, out mU1, out mU2, out mU3))
            {
                cachedMidshipLength = Vector3.Distance(mL0, mLR) + Vector3.Distance(mLR, mL3);
            }
        }

        // 2. Cache Valid Frame Stations ONCE (including the bow apex vertical line at fwdZ)
        cachedValidStations.Clear();
        if (framingModel != null && sheerPlan != null)
        {
            sheerPlan.UpdateCachedValues();
            List<float> allStations = framingModel.CalculateFrameStations();
            if (allStations.Count > 0)
            {
                cachedValidStations.AddRange(allStations);
            }
        }

        RebuildCachedGizmoPoints();

        // 3. Second pass: update framingModel stations with exact calculated profile intersection Z coordinates
        if (framingModel != null && sheerPlan != null)
        {
            framingModel.MarkStationsDirty();
            List<float> allStations = framingModel.CalculateFrameStations();
            cachedValidStations.Clear();
            if (allStations.Count > 0)
            {
                cachedValidStations.AddRange(allStations);
            }

            float fwdZ = sheerPlan.GetFwdMostZ();
            if (cachedValidStations.Count == 0 || cachedValidStations[cachedValidStations.Count - 1] < fwdZ - 0.001f)
            {
                cachedValidStations.Add(fwdZ);
            }

            RebuildCachedGizmoPoints();
        }

        isCacheDirty = false;
    }

    /// <summary>
    /// Collects the exact Z coordinates of all computed stem and stern profile intersection points across all strakes.
    /// </summary>
    public void CollectProfileIntersectionZs(List<float> outZs)
    {
        if (outZs == null) return;

        AddZsFromList(cachedPortLLStemStrakes, outZs);
        AddZsFromList(cachedPortLLSternTimbers, outZs);

        AddZsFromList(cachedPortULStemStrakes, outZs);
        AddZsFromList(cachedPortULSternTimbers, outZs);

        AddZsFromList(cachedPortUMStemStrakes, outZs);
        AddZsFromList(cachedPortUMSternTimbers, outZs);
    }

    private void AddZsFromList(List<Vector3> pts, List<float> outZs)
    {
        if (pts == null) return;
        foreach (Vector3 p in pts)
        {
            if (p != Vector3.zero) outZs.Add(p.z);
        }
    }

    private void RebuildCachedGizmoPoints()
    {
        cachedPortLLPoints.Clear();
        cachedStbdLLPoints.Clear();
        cachedPortULPoints.Clear();
        cachedStbdULPoints.Clear();
        cachedPortUMPoints.Clear();
        cachedStbdUMPoints.Clear();

        List<float> stations = cachedValidStations;
        if (stations.Count == 0 || framingModel == null || sheerPlan == null) return;

        int llCount = Mathf.Max(2, strakesPerLowerLowerMould);
        int ulCount = Mathf.Max(2, strakesPerUpperLowerMould);
        int umCount = Mathf.Max(2, strakesPerUpperMould);

        for (int s = 0; s < stations.Count; s++)
        {
            float z = stations[s];

            // Port Side (-1f)
            cachedPortLLPoints[s] = GetLowerLowerMouldPoints(z, -1f, llCount);
            cachedPortULPoints[s] = GetUpperLowerMouldPointsToMainDeckSheer(z, -1f, ulCount);
            cachedPortUMPoints[s] = GetUpperMouldPoints(z, -1f, umCount);

            // Starboard Side (+1f)
            cachedStbdLLPoints[s] = GetLowerLowerMouldPoints(z, 1f, llCount);
            cachedStbdULPoints[s] = GetUpperLowerMouldPointsToMainDeckSheer(z, 1f, ulCount);
            cachedStbdUMPoints[s] = GetUpperMouldPoints(z, 1f, umCount);
        }

        // 1. Apply Y-Profile Longitudinal Fairing FIRST (smoothing strake guide lines smoothly across stations Z)
        if (enableLongitudinalFairing)
        {
            ApplyYProfileLongitudinalFairing(stations, cachedPortLLPoints, -1f, llCount, 0);
            ApplyYProfileLongitudinalFairing(stations, cachedStbdLLPoints, 1f, llCount, 0);

            ApplyYProfileLongitudinalFairing(stations, cachedPortULPoints, -1f, ulCount, 1);
            ApplyYProfileLongitudinalFairing(stations, cachedStbdULPoints, 1f, ulCount, 1);

            if (showUpperMouldStrakes)
            {
                ApplyYProfileLongitudinalFairing(stations, cachedPortUMPoints, -1f, umCount, 2);
                ApplyYProfileLongitudinalFairing(stations, cachedStbdUMPoints, 1f, umCount, 2);
            }
        }

        // 2. Rebuild Stem and Stern profile intersection point markers BEFORE frame cutoffs (while strake lines run through uncut frames)
        cachedPortStemIntersections.Clear();
        cachedStbdStemIntersections.Clear();
        cachedPortSternIntersections.Clear();
        cachedStbdSternIntersections.Clear();

        cachedPortLLStemStrakes.Clear();
        cachedPortLLSternTimbers.Clear();
        cachedStbdLLStemStrakes.Clear();
        cachedStbdLLSternTimbers.Clear();

        cachedPortULStemStrakes.Clear();
        cachedPortULSternTimbers.Clear();
        cachedStbdULStemStrakes.Clear();
        cachedStbdULSternTimbers.Clear();

        cachedPortUMStemStrakes.Clear();
        cachedPortUMSternTimbers.Clear();
        cachedStbdUMStemStrakes.Clear();
        cachedStbdUMSternTimbers.Clear();

        RebuildProfileIntersectionsForSegment(stations, cachedPortLLPoints, llCount, -1f, cachedPortStemIntersections, cachedPortSternIntersections, cachedPortLLStemStrakes, cachedPortLLSternTimbers);
        RebuildProfileIntersectionsForSegment(stations, cachedStbdLLPoints, llCount, 1f, cachedStbdStemIntersections, cachedStbdSternIntersections, cachedStbdLLStemStrakes, cachedStbdLLSternTimbers);

        RebuildProfileIntersectionsForSegment(stations, cachedPortULPoints, ulCount, -1f, cachedPortStemIntersections, cachedPortSternIntersections, cachedPortULStemStrakes, cachedPortULSternTimbers);
        RebuildProfileIntersectionsForSegment(stations, cachedStbdULPoints, ulCount, 1f, cachedStbdStemIntersections, cachedStbdSternIntersections, cachedStbdULStemStrakes, cachedStbdULSternTimbers);

        if (showUpperMouldStrakes)
        {
            RebuildProfileIntersectionsForSegment(stations, cachedPortUMPoints, umCount, -1f, cachedPortStemIntersections, cachedPortSternIntersections, cachedPortUMStemStrakes, cachedPortUMSternTimbers);
            RebuildProfileIntersectionsForSegment(stations, cachedStbdUMPoints, umCount, 1f, cachedStbdStemIntersections, cachedStbdSternIntersections, cachedStbdUMStemStrakes, cachedStbdUMSternTimbers);
        }

        // 3. Apply Frame Cutoffs AFTER profile intersections are computed
        if (respectFrameCutoffs)
        {
            ApplyFrameCutoffsToPoints(stations, cachedPortLLPoints, -1f, llCount);
            ApplyFrameCutoffsToPoints(stations, cachedPortULPoints, -1f, ulCount);
            ApplyFrameCutoffsToPoints(stations, cachedPortUMPoints, -1f, umCount);

            ApplyFrameCutoffsToPoints(stations, cachedStbdLLPoints, 1f, llCount);
            ApplyFrameCutoffsToPoints(stations, cachedStbdULPoints, 1f, ulCount);
            ApplyFrameCutoffsToPoints(stations, cachedStbdUMPoints, 1f, umCount);
        }

        RebuildStructuralStrakes(stations);
    }

    /// <summary>
    /// Evaluates the 3D strake point at height targetY at station z.
    /// For Strakes, the Keel points climbing the stem curve in FramingModel are ignored;
    /// if targetY is below the framing model's stem/profile cutoff (pL0.y), the point is placed
    /// on the vertical Pseudo Frame backbone line at (side * hw, targetY, z).
    /// </summary>
    private Vector3 GetStrakeFramePointAtY(float z, float side, float targetY)
    {
        if (framingModel == null) return Vector3.zero;

        float hw = framingModel.GetKeelHalfWidth();

        Vector3 pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3;
        if (framingModel.GetFrameControlPointsAtZ(z, side, out pL0, out pL1, out pLR, out pL2, out pL3, out pU0, out pU1, out pU2, out pU3))
        {
            if (targetY < pL0.y - 0.0005f)
            {
                // Target Y is below the framing model section curve bottom (pL0.y):
                // For strakes, ignore climbing stem keel point and place on vertical Pseudo Frame line measured up from flat keel
                return new Vector3(side * hw, targetY, z);
            }

            Vector3 pt = framingModel.GetTransverseFramePointAtY(z, side, targetY);
            if (pt != Vector3.zero)
            {
                return pt;
            }
        }

        return new Vector3(side * hw, targetY, z);
    }

    /// <summary>
    /// Evaluates Lower-Lower Mould strake points between the Keel line (flat baseline keel) and the Connection Point strake line from SheerPlan.
    /// Points are equally spaced by 3D arc length along the combined Pseudo Frame vertical line and frame mould section curve path.
    /// Connection Point strake line height remains on the projected Rising Line height (GetProjectedBowRisingYAtZ) without climbing the stem.
    /// The climbing stem keel points from FramingModel are ignored for Strakes.
    /// </summary>
    private List<Vector3> GetLowerLowerMouldPoints(float z, float side, int count)
    {
        List<Vector3> points = new List<Vector3>(count);
        if (sheerPlan == null || framingModel == null) return points;

        count = Mathf.Max(2, count);

        float keelY = framingModel.GetFrameBottomYAtZ(z); // Flat baseline keel Y
        float connY = sheerPlan.GetProjectedBowRisingYAtZ(z); // Connection Point Strake line height

        if (connY < keelY + 0.001f)
        {
            connY = keelY + 0.001f;
        }

        const int N = 40;
        float[] sampleDists = new float[N + 1];
        Vector3[] samplePts = new Vector3[N + 1];

        Vector3 startPt = GetStrakeFramePointAtY(z, side, keelY);

        samplePts[0] = startPt;
        sampleDists[0] = 0f;

        float totalDist = 0f;
        Vector3 prevPt = startPt;

        for (int i = 1; i <= N; i++)
        {
            float y = Mathf.Lerp(keelY, connY, (float)i / N);
            Vector3 currPt = GetStrakeFramePointAtY(z, side, y);

            samplePts[i] = currPt;
            float step = Vector3.Distance(prevPt, currPt);
            totalDist += step;
            sampleDists[i] = totalDist;
            prevPt = currPt;
        }

        if (totalDist < 0.0001f)
        {
            for (int k = 0; k < count; k++) points.Add(startPt);
            return points;
        }

        // Space points EQUALLY by 3D arc length along the path (Pseudo Frame + Frame Curve)
        for (int k = 0; k < count; k++)
        {
            float t = (float)k / (count - 1);
            float targetDist = t * totalDist;

            int idx = 0;
            while (idx < N && sampleDists[idx + 1] < targetDist)
            {
                idx++;
            }

            if (idx >= N)
            {
                points.Add(samplePts[N]);
            }
            else
            {
                float segLen = sampleDists[idx + 1] - sampleDists[idx];
                float frac = segLen > 0.00001f ? (targetDist - sampleDists[idx]) / segLen : 0f;
                points.Add(Vector3.Lerp(samplePts[idx], samplePts[idx + 1], frac));
            }
        }

        return points;
    }

    /// <summary>
    /// Evaluates Upper-Lower Mould strake points between Connection Point line and the Main Deck Sheer line from SheerPlan.
    /// Points are equally spaced by 3D arc length along the combined Pseudo Frame vertical line and frame mould section curve path.
    /// Connection Point strake line height remains on the projected Rising Line height (GetProjectedBowRisingYAtZ) without climbing the stem.
    /// </summary>
    private List<Vector3> GetUpperLowerMouldPointsToMainDeckSheer(float z, float side, int count)
    {
        List<Vector3> points = new List<Vector3>(count);
        if (sheerPlan == null || framingModel == null) return points;

        count = Mathf.Max(2, count);

        float startY = sheerPlan.GetProjectedBowRisingYAtZ(z);
        float mainDeckSheerY = sheerPlan.GetBaseDeckYAtZ(z);

        if (mainDeckSheerY < startY + 0.001f)
        {
            mainDeckSheerY = startY + 0.01f;
        }

        const int N = 40;
        float[] sampleDists = new float[N + 1];
        Vector3[] samplePts = new Vector3[N + 1];

        Vector3 startPt = GetStrakeFramePointAtY(z, side, startY);

        samplePts[0] = startPt;
        sampleDists[0] = 0f;

        float totalDist = 0f;
        Vector3 prevPt = startPt;

        for (int i = 1; i <= N; i++)
        {
            float y = Mathf.Lerp(startY, mainDeckSheerY, (float)i / N);
            Vector3 currPt = GetStrakeFramePointAtY(z, side, y);

            samplePts[i] = currPt;
            float step = Vector3.Distance(prevPt, currPt);
            totalDist += step;
            sampleDists[i] = totalDist;
            prevPt = currPt;
        }

        if (totalDist < 0.0001f)
        {
            for (int k = 0; k < count; k++) points.Add(startPt);
            return points;
        }

        // Space points EQUALLY by 3D arc length along the path (Pseudo Frame + Frame Curve)
        for (int k = 0; k < count; k++)
        {
            float t = (float)k / (count - 1);
            float targetDist = t * totalDist;

            int idx = 0;
            while (idx < N && sampleDists[idx + 1] < targetDist)
            {
                idx++;
            }

            if (idx >= N)
            {
                points.Add(samplePts[N]);
            }
            else
            {
                float segLen = sampleDists[idx + 1] - sampleDists[idx];
                float frac = segLen > 0.00001f ? (targetDist - sampleDists[idx]) / segLen : 0f;
                points.Add(Vector3.Lerp(samplePts[idx], samplePts[idx + 1], frac));
            }
        }

        return points;
    }

    /// <summary>
    /// Evaluates Upper Mould strake points between the Main Deck Sheer line and the Top Timber Sheer line.
    /// Points are equally spaced by 3D arc length along the upper frame section curve.
    /// </summary>
    private List<Vector3> GetUpperMouldPoints(float z, float side, int count)
    {
        List<Vector3> points = new List<Vector3>(count);
        if (sheerPlan == null || framingModel == null) return points;

        count = Mathf.Max(2, count);

        float startY = sheerPlan.GetBaseDeckYAtZ(z);
        // Stack upper mould strakes evenly across the full uncut frame height from Main Deck to Top Line Sheer
        float endY = sheerPlan.GetPureSheerYAtZ(z);

        if (endY < startY + 0.001f)
        {
            endY = startY + 0.01f;
        }

        for (int k = 0; k < count; k++)
        {
            float t = (float)k / (count - 1);
            float y = Mathf.Lerp(startY, endY, t);
            Vector3 pt = GetStrakeFramePointAtY(z, side, y);
            points.Add(pt);
        }

        return points;
    }

    /// <summary>
    /// Applies frame cutoffs (top timber sheer height & fashion timber cutoffs) to strake points across stations.
    /// Excludes/cuts any strake point exceeding the station's top timber height so strakes stop dead at deck cut frames.
    /// </summary>
    private void ApplyFrameCutoffsToPoints(List<float> stations, Dictionary<int, List<Vector3>> pointDict, float side, int strakeCount)
    {
        if (stations == null || pointDict == null || framingModel == null || sheerPlan == null) return;
        if (framingModel.disableFrameCutoffs) return;

        for (int s = 0; s < stations.Count; s++)
        {
            if (!pointDict.ContainsKey(s)) continue;
            float z = stations[s];

            float localTopY = sheerPlan.GetTopTimberYAtZ(z);

            for (int k = 0; k < pointDict[s].Count; k++)
            {
                Vector3 pt = pointDict[s][k];
                if (pt != Vector3.zero)
                {
                    float sternLimitZ = sheerPlan.GetSternProfileZAtY(pt.y);
                    float stemLimitZ = sheerPlan.GetStemProfileZAtY(pt.y);
                    if (pt.y > localTopY + 0.001f || z < sternLimitZ - 0.001f || z > stemLimitZ + 0.001f)
                    {
                        // Mark strake point at station s as cut/excluded
                        pointDict[s][k] = Vector3.zero;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Applies Full 3D Multi-Axis (X/Y and Z/X) Surface-Constrained Longitudinal Fairing across stations Z.
    /// Smooths both Y-elevation and X-breadth to eliminate wavy/undulating ripples from all view angles.
    /// segmentType: 0 = lower-lower mould, 1 = upper-lower mould (pLR -> Main Deck Sheer), 2 = upper mould (Main Deck Sheer -> Top Line)
    /// </summary>
    private void ApplyYProfileLongitudinalFairing(List<float> stations, Dictionary<int, List<Vector3>> pointDict, float side, int strakeCount, int segmentType = 0)
    {
        if (!enableLongitudinalFairing || stations == null || stations.Count < 3 || pointDict == null || framingModel == null) return;

        float hw = framingModel.GetKeelHalfWidth();
        int passes = Mathf.Clamp(fairingPasses, 1, 20);
        int stationCount = Mathf.Min(stations.Count, 512);

        for (int k = 0; k < strakeCount; k++)
        {
            bool isLockingConnectionPoint = (lockConnectionPointToRisingLine && segmentType == 0 && k == strakeCount - 1) ||
                                            (lockConnectionPointToRisingLine && segmentType == 1 && k == 0);
            bool isLockingDeckSheer = (lockMainDeckSheerToSheerPlan && segmentType == 1 && k == strakeCount - 1) ||
                                      (lockMainDeckSheerToSheerPlan && segmentType == 2 && k == 0);
            bool isLockingTopSheer = (lockTopStrakeToSheerPlan && segmentType == 2 && k == strakeCount - 1);

            // 1. Fetch un-faired 3D points (Y and X) for strake k across all stations
            for (int s = 0; s < stationCount; s++)
            {
                validBuffer[s] = false;
                if (pointDict.ContainsKey(s) && pointDict[s].Count > k && pointDict[s][k] != Vector3.zero)
                {
                    rawYBuffer[s] = pointDict[s][k].y;
                    rawXBuffer[s] = pointDict[s][k].x;
                    validBuffer[s] = true;
                }
            }

            // 2. Perform 3D multi-axis binomial smoothing passes ONLY in float array memory
            for (int pass = 0; pass < passes; pass++)
            {
                for (int s = 0; s < stationCount; s++)
                {
                    if (!validBuffer[s]) continue;

                    // --- Y-AXIS (HEIGHT) FAIRING ---
                    if (isLockingConnectionPoint)
                    {
                        float z = stations[s];
                        targetYBuffer[s] = sheerPlan.GetProjectedBowRisingYAtZ(z);
                    }
                    else if (isLockingDeckSheer)
                    {
                        float z = stations[s];
                        targetYBuffer[s] = sheerPlan.GetBaseDeckYAtZ(z);
                    }
                    else if (isLockingTopSheer)
                    {
                        float z = stations[s];
                        targetYBuffer[s] = sheerPlan.GetPureSheerYAtZ(z);
                    }
                    else if (s == 0 || s == stationCount - 1 || !validBuffer[s - 1] || !validBuffer[s + 1])
                    {
                        targetYBuffer[s] = rawYBuffer[s];
                    }
                    else
                    {
                        float zPrev = stations[s - 1];
                        float zCurr = stations[s];
                        float zNext = stations[s + 1];

                        float d1 = Mathf.Abs(zCurr - zPrev);
                        float d2 = Mathf.Abs(zNext - zCurr);
                        float totalD = d1 + d2;

                        float yLinearInterp = (totalD > 0.00001f) ? (d2 * rawYBuffer[s - 1] + d1 * rawYBuffer[s + 1]) / totalD : rawYBuffer[s];
                        float ySmoothed = 0.5f * rawYBuffer[s] + 0.5f * yLinearInterp;
                        float fairedY = Mathf.Lerp(rawYBuffer[s], ySmoothed, fairingStrengthY);

                        // Clamp to strict frame station boundary range defined by segment boundary lines
                        float z = stations[s];
                        float minY = 0f, maxY = 0f;
                        if (segmentType == 0) // Lower-Lower Mould (Keel to Connection Point)
                        {
                            minY = framingModel.GetFrameBottomYAtZ(z);
                            maxY = sheerPlan.GetProjectedBowRisingYAtZ(z);
                        }
                        else if (segmentType == 1) // Upper-Lower Mould (Connection Point to Main Deck Sheer)
                        {
                            minY = sheerPlan.GetProjectedBowRisingYAtZ(z);
                            maxY = sheerPlan.GetBaseDeckYAtZ(z);
                        }
                        else if (segmentType == 2) // Upper Mould (Main Deck Sheer to Top Line)
                        {
                            minY = sheerPlan.GetBaseDeckYAtZ(z);
                            maxY = sheerPlan.GetPureSheerYAtZ(z);
                        }

                        if (maxY < minY + 0.001f) maxY = minY + 0.001f;
                        targetYBuffer[s] = Mathf.Clamp(fairedY, minY, maxY);
                    }

                    // --- X-AXIS (BREADTH / TOP VIEW) FAIRING ---
                    if (s == 0 || s == stationCount - 1 || !validBuffer[s - 1] || !validBuffer[s + 1])
                    {
                        targetXBuffer[s] = rawXBuffer[s];
                    }
                    else
                    {
                        float zPrev = stations[s - 1];
                        float zCurr = stations[s];
                        float zNext = stations[s + 1];

                        float d1 = Mathf.Abs(zCurr - zPrev);
                        float d2 = Mathf.Abs(zNext - zCurr);
                        float totalD = d1 + d2;

                        float xLinearInterp = (totalD > 0.00001f) ? (d2 * rawXBuffer[s - 1] + d1 * rawXBuffer[s + 1]) / totalD : rawXBuffer[s];
                        float xSmoothed = 0.5f * rawXBuffer[s] + 0.5f * xLinearInterp;
                        targetXBuffer[s] = Mathf.Lerp(rawXBuffer[s], xSmoothed, fairingStrengthX);
                    }
                }

                for (int s = 0; s < stationCount; s++)
                {
                    if (validBuffer[s])
                    {
                        rawYBuffer[s] = targetYBuffer[s];
                        rawXBuffer[s] = targetXBuffer[s];
                    }
                }
            }

            // 3. Project faired Y target heights back onto 3D frame surfaces ONCE after all passes complete
            for (int s = 0; s < stationCount; s++)
            {
                if (!validBuffer[s]) continue;
                float z = stations[s];

                // GetStrakeFramePointAtY evaluates the point strictly on the 3D frame surface
                Vector3 fairedPt = GetStrakeFramePointAtY(z, side, targetYBuffer[s]);
                if (fairedPt != Vector3.zero)
                {
                    pointDict[s][k] = fairedPt;
                }
            }
        }
    }

    /// <summary>
    /// Evaluates list of 3D points equally spaced by arc length along a sub-segment u in [minU, maxU] 
    /// using pre-fetched control points.
    /// </summary>
    public List<Vector3> GetSegmentStrakePointsFromControlPoints(
        Vector3 pL0, Vector3 pL1, Vector3 pLR, Vector3 pL2, Vector3 pL3, Vector3 pU0, Vector3 pU1, Vector3 pU2, Vector3 pU3,
        float minU, float maxU, int count, float keelY = float.NaN, float midshipLen = float.NaN)
    {
        List<Vector3> points = new List<Vector3>(count);
        if (framingModel == null) return points;

        count = Mathf.Max(2, count);

        float fwdZVal = (sheerPlan != null) ? sheerPlan.GetFwdMostZ() : float.MinValue;
        bool isBowApex = (!float.IsNaN(keelY) && (Mathf.Abs(pL0.z - fwdZVal) < 0.005f || Vector3.Distance(pL0, pL3) < 0.005f));

        if (isBowApex)
        {
            float risingProjY = (sheerPlan != null) ? sheerPlan.GetProjectedBowRisingYAtZ(pL0.z) : pLR.y;
            float mainDeckY = (sheerPlan != null) ? sheerPlan.GetBaseDeckYAtZ(pL0.z) : pL3.y;

            float EvalApexY(float u)
            {
                if (u <= 0.25f) return Mathf.Lerp(keelY, risingProjY, u / 0.25f);
                return Mathf.Lerp(risingProjY, mainDeckY, (u - 0.25f) / 0.75f);
            };

            float startY = EvalApexY(minU);
            float endY = EvalApexY(maxU);
            for (int k = 0; k < count; k++)
            {
                float t = (float)k / (count - 1);
                float y = Mathf.Lerp(startY, endY, t);
                points.Add(new Vector3(pL0.x, y, pL0.z));
            }
            return points;
        }

        bool hasPseudoFrame = (Mathf.Approximately(minU, 0f) && !float.IsNaN(keelY) && pL0.y > keelY + 0.001f);

        const int curveSamples = 20;
        const int pseudoSamples = 10;
        int totalSamples = hasPseudoFrame ? (pseudoSamples + curveSamples) : curveSamples;

        distTableBuffer[0] = 0f;
        float totalLength = 0f;

        if (hasPseudoFrame)
        {
            Vector3 keelBaselinePt = new Vector3(pL0.x, keelY, pL0.z);
            ptTableBuffer[0] = keelBaselinePt;

            for (int i = 1; i <= pseudoSamples; i++)
            {
                float t = i / (float)pseudoSamples;
                ptTableBuffer[i] = Vector3.Lerp(keelBaselinePt, pL0, t);
                float stepDist = Vector3.Distance(ptTableBuffer[i - 1], ptTableBuffer[i]);
                totalLength += stepDist;
                distTableBuffer[i] = totalLength;
            }

            for (int i = 1; i <= curveSamples; i++)
            {
                float u = Mathf.Lerp(minU, maxU, i / (float)curveSamples);
                int tableIdx = pseudoSamples + i;
                ptTableBuffer[tableIdx] = framingModel.EvaluateFramePointFromControlPoints(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                float stepDist = Vector3.Distance(ptTableBuffer[tableIdx - 1], ptTableBuffer[tableIdx]);
                totalLength += stepDist;
                distTableBuffer[tableIdx] = totalLength;
            }
        }
        else
        {
            ptTableBuffer[0] = framingModel.EvaluateFramePointFromControlPoints(minU, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);

            for (int i = 1; i <= curveSamples; i++)
            {
                float u = Mathf.Lerp(minU, maxU, i / (float)curveSamples);
                ptTableBuffer[i] = framingModel.EvaluateFramePointFromControlPoints(u, pL0, pL1, pLR, pL2, pL3, pU0, pU1, pU2, pU3);
                float stepDist = Vector3.Distance(ptTableBuffer[i - 1], ptTableBuffer[i]);
                totalLength += stepDist;
                distTableBuffer[i] = totalLength;
            }
        }

        if (totalLength < 0.0001f)
        {
            for (int k = 0; k < count; k++) points.Add(ptTableBuffer[0]);
            return points;
        }

        float midLen = float.IsNaN(midshipLen) ? cachedMidshipLength : midshipLen;
        float girthRatio = (enableStrakeSmoothing && midLen > 0.001f) ? Mathf.Clamp01(totalLength / midLen) : 1.0f;
        float power = Mathf.Lerp(1.0f / Mathf.Clamp(minHobTaperRatio, 0.3f, 1.0f), 1.0f, girthRatio);

        for (int k = 0; k < count; k++)
        {
            float linearT = (float)k / (count - 1);
            float targetDist = linearT * totalLength;

            int idx = 0;
            while (idx < totalSamples && distTableBuffer[idx + 1] < targetDist)
            {
                idx++;
            }

            if (idx >= totalSamples)
            {
                points.Add(ptTableBuffer[totalSamples]);
            }
            else
            {
                float segmentLength = distTableBuffer[idx + 1] - distTableBuffer[idx];
                float t = segmentLength > 0.00001f ? (targetDist - distTableBuffer[idx]) / segmentLength : 0f;
                Vector3 pt = Vector3.Lerp(ptTableBuffer[idx], ptTableBuffer[idx + 1], t);
                points.Add(pt);
            }
        }

        return points;
    }

    /// <summary>
    /// Gets list of strake points along Lower-Lower Mould (Keel to Connection Point line) at station z.
    /// </summary>
    public List<Vector3> GetLowerLowerMouldStrakePoints(float z, float side, int count)
    {
        if (framingModel == null || sheerPlan == null) return new List<Vector3>();
        RefreshCacheIfNeeded();
        return GetLowerLowerMouldPoints(z, side, count);
    }

    /// <summary>
    /// Gets list of strake points along Upper-Lower Mould (Connection Point line to Main Deck Sheer line) at station z.
    /// </summary>
    public List<Vector3> GetUpperLowerMouldStrakePoints(float z, float side, int count)
    {
        if (framingModel == null || sheerPlan == null) return new List<Vector3>();
        RefreshCacheIfNeeded();
        return GetUpperLowerMouldPointsToMainDeckSheer(z, side, count);
    }

    /// <summary>
    /// Gets list of strake points along Upper Mould (Main Deck Sheer line to Top Line) at station z.
    /// </summary>
    public List<Vector3> GetUpperMouldStrakePoints(float z, float side, int count)
    {
        if (framingModel == null || sheerPlan == null) return new List<Vector3>();
        RefreshCacheIfNeeded();

        return GetUpperMouldPoints(z, side, count);
    }

    // Alias helper methods for backwards compatibility
    public List<Vector3> GetLowerUpperMouldStrakePoints(float z, float side, int count) => GetUpperMouldStrakePoints(z, side, count);
    public List<Vector3> GetUpperUpperMouldStrakePoints(float z, float side, int count) => GetUpperMouldStrakePoints(z, side, count);

    /// <summary>
    /// Gets list of frame station Z coordinates for strakes.
    /// </summary>
    public List<float> GetValidFrameStations()
    {
        RefreshCacheIfNeeded();
        return cachedValidStations;
    }

    /// <summary>
    /// Draws longitudinal strake guide line connecting across valid frame stations.
    /// When frame cutoffs are enabled, connects the final aft segment directly to the exact point on the Fashion Timber curve (sternEndPt).
    /// </summary>
    private void DrawFairedStrakeLine(
        Dictionary<int, List<Vector3>> pointDict, 
        List<float> stations, 
        int k, 
        Vector3 stemEndPt = default, 
        Vector3 sternEndPt = default)
    {
        if (pointDict == null || stations == null || stations.Count < 2) return;

        Vector3 lastValidPt = Vector3.zero;
        Vector3 firstValidPt = Vector3.zero;
        int firstValidIdx = -1;
        int lastValidIdx = -1;

        for (int s = 0; s < stations.Count - 1; s++)
        {
            if (pointDict.ContainsKey(s) && pointDict.ContainsKey(s + 1) &&
                pointDict[s].Count > k && pointDict[s + 1].Count > k)
            {
                Vector3 p1 = pointDict[s][k];
                Vector3 p2 = pointDict[s + 1][k];
                if (p1 != Vector3.zero && p2 != Vector3.zero)
                {
                    if (firstValidPt == Vector3.zero)
                    {
                        firstValidPt = p1;
                        firstValidIdx = s;
                    }
                    lastValidPt = p2;
                    lastValidIdx = s + 1;
                    Gizmos.DrawLine(p1, p2);
                }
            }
        }

        if (respectFrameCutoffs)
        {
            float maxGap = (framingModel != null) ? Mathf.Max(3.0f, framingModel.frameSpacing * 3.0f) : 3.0f;

            // Connect to sternEndPt if firstValidPt is adjacent to sternEndPt
            if (firstValidPt != Vector3.zero && sternEndPt != Vector3.zero &&
                firstValidPt.z >= sternEndPt.z - 0.05f && (firstValidPt.z - sternEndPt.z) <= maxGap)
            {
                Gizmos.DrawLine(firstValidPt, sternEndPt);
            }

            // Connect to stemEndPt if lastValidPt is adjacent to stemEndPt
            if (lastValidPt != Vector3.zero && stemEndPt != Vector3.zero &&
                lastValidPt.z <= stemEndPt.z + 0.05f && (stemEndPt.z - lastValidPt.z) <= maxGap)
            {
                Gizmos.DrawLine(lastValidPt, stemEndPt);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showStrakes) return;
        RefreshCacheIfNeeded();
        if (framingModel == null || sheerPlan == null) return;

        List<float> stations = cachedValidStations;
        if (stations.Count == 0) return;

        int llCount = Mathf.Max(2, strakesPerLowerLowerMould);
        int ulCount = Mathf.Max(2, strakesPerUpperLowerMould);
        int umCount = Mathf.Max(2, strakesPerUpperMould);

        Vector3 cubeSize = Vector3.one * (pointRadius * 1.5f);
        Vector3 highlightSize = Vector3.one * (pointRadius * 2.2f);

        // --- RESET MODE (Primary Defining Lines Only) ---
        if (!showIntermediateStrakes)
        {
            // 1. Connection Point Line (pLR) - Last element of LL points
            if (showConnectionPointLine)
            {
                Gizmos.color = connectionPointColor;

                // Port Side
                for (int s = 0; s < stations.Count; s++)
                {
                    if (cachedPortLLPoints.ContainsKey(s) && cachedPortLLPoints[s].Count >= llCount)
                    {
                        Vector3 pt = cachedPortLLPoints[s][llCount - 1];
                        if (pt != Vector3.zero) Gizmos.DrawCube(pt, highlightSize);
                    }
                }
                if (showStrakeLines)
                {
                    Vector3 portStem = (llCount - 1 < cachedPortLLStemStrakes.Count) ? cachedPortLLStemStrakes[llCount - 1] : Vector3.zero;
                    Vector3 portStern = (llCount - 1 < cachedPortLLSternTimbers.Count) ? cachedPortLLSternTimbers[llCount - 1] : Vector3.zero;
                    DrawFairedStrakeLine(cachedPortLLPoints, stations, llCount - 1, portStem, portStern);
                }

                // Starboard Side
                for (int s = 0; s < stations.Count; s++)
                {
                    if (cachedStbdLLPoints.ContainsKey(s) && cachedStbdLLPoints[s].Count >= llCount)
                    {
                        Vector3 pt = cachedStbdLLPoints[s][llCount - 1];
                        if (pt != Vector3.zero) Gizmos.DrawCube(pt, highlightSize);
                    }
                }
                if (showStrakeLines)
                {
                    Vector3 stbdStem = (llCount - 1 < cachedStbdLLStemStrakes.Count) ? cachedStbdLLStemStrakes[llCount - 1] : Vector3.zero;
                    Vector3 stbdStern = (llCount - 1 < cachedStbdLLSternTimbers.Count) ? cachedStbdLLSternTimbers[llCount - 1] : Vector3.zero;
                    DrawFairedStrakeLine(cachedStbdLLPoints, stations, llCount - 1, stbdStem, stbdStern);
                }
            }

            // 2. Main Deck Sheer Line - Last element of UL points
            if (showMainDeckSheerLine)
            {
                Gizmos.color = mainDeckSheerColor;

                // Port Side
                for (int s = 0; s < stations.Count; s++)
                {
                    if (cachedPortULPoints.ContainsKey(s) && cachedPortULPoints[s].Count >= ulCount)
                    {
                        Vector3 pt = cachedPortULPoints[s][ulCount - 1];
                        if (pt != Vector3.zero) Gizmos.DrawCube(pt, highlightSize);
                    }
                }
                if (showStrakeLines)
                {
                    Vector3 portStem = (ulCount - 1 < cachedPortULStemStrakes.Count) ? cachedPortULStemStrakes[ulCount - 1] : Vector3.zero;
                    Vector3 portStern = (ulCount - 1 < cachedPortULSternTimbers.Count) ? cachedPortULSternTimbers[ulCount - 1] : Vector3.zero;
                    DrawFairedStrakeLine(cachedPortULPoints, stations, ulCount - 1, portStem, portStern);
                }

                // Starboard Side
                for (int s = 0; s < stations.Count; s++)
                {
                    if (cachedStbdULPoints.ContainsKey(s) && cachedStbdULPoints[s].Count >= ulCount)
                    {
                        Vector3 pt = cachedStbdULPoints[s][ulCount - 1];
                        if (pt != Vector3.zero) Gizmos.DrawCube(pt, highlightSize);
                    }
                }
                if (showStrakeLines)
                {
                    Vector3 stbdStem = (ulCount - 1 < cachedStbdULStemStrakes.Count) ? cachedStbdULStemStrakes[ulCount - 1] : Vector3.zero;
                    Vector3 stbdStern = (ulCount - 1 < cachedStbdULSternTimbers.Count) ? cachedStbdULSternTimbers[ulCount - 1] : Vector3.zero;
                    DrawFairedStrakeLine(cachedStbdULPoints, stations, ulCount - 1, stbdStem, stbdStern);
                }
            }
        }
        else
        {

        // --- FULL INTERMEDIATE STRAKES MODE ---
        // 1. Draw Frame Station Nodes
        for (int s = 0; s < stations.Count; s++)
        {
            if (cachedPortLLPoints.ContainsKey(s))
            {
                Gizmos.color = lowerLowerPointColor;
                foreach (Vector3 pt in cachedPortLLPoints[s]) if (pt != Vector3.zero) Gizmos.DrawCube(pt, cubeSize);
                if (showConnectionPointLine && cachedPortLLPoints[s].Count > 0)
                {
                    Gizmos.color = connectionPointColor;
                    Vector3 pt = cachedPortLLPoints[s][cachedPortLLPoints[s].Count - 1];
                    if (pt != Vector3.zero) Gizmos.DrawCube(pt, highlightSize);
                }
            }

            if (cachedPortULPoints.ContainsKey(s))
            {
                Gizmos.color = upperLowerPointColor;
                foreach (Vector3 pt in cachedPortULPoints[s]) if (pt != Vector3.zero) Gizmos.DrawCube(pt, cubeSize);
                if (showMainDeckSheerLine && cachedPortULPoints[s].Count > 0)
                {
                    Gizmos.color = mainDeckSheerColor;
                    Vector3 pt = cachedPortULPoints[s][cachedPortULPoints[s].Count - 1];
                    if (pt != Vector3.zero) Gizmos.DrawCube(pt, highlightSize);
                }
            }

            if (cachedPortUMPoints.ContainsKey(s))
            {
                Gizmos.color = upperMouldPointColor;
                foreach (Vector3 pt in cachedPortUMPoints[s]) if (pt != Vector3.zero) Gizmos.DrawCube(pt, cubeSize);
            }

            if (cachedStbdLLPoints.ContainsKey(s))
            {
                Gizmos.color = lowerLowerPointColor;
                foreach (Vector3 pt in cachedStbdLLPoints[s]) if (pt != Vector3.zero) Gizmos.DrawCube(pt, cubeSize);
                if (showConnectionPointLine && cachedStbdLLPoints[s].Count > 0)
                {
                    Gizmos.color = connectionPointColor;
                    Vector3 pt = cachedStbdLLPoints[s][cachedStbdLLPoints[s].Count - 1];
                    if (pt != Vector3.zero) Gizmos.DrawCube(pt, highlightSize);
                }
            }

            if (cachedStbdULPoints.ContainsKey(s))
            {
                Gizmos.color = upperLowerPointColor;
                foreach (Vector3 pt in cachedStbdULPoints[s]) if (pt != Vector3.zero) Gizmos.DrawCube(pt, cubeSize);
                if (showMainDeckSheerLine && cachedStbdULPoints[s].Count > 0)
                {
                    Gizmos.color = mainDeckSheerColor;
                    Vector3 pt = cachedStbdULPoints[s][cachedStbdULPoints[s].Count - 1];
                    if (pt != Vector3.zero) Gizmos.DrawCube(pt, highlightSize);
                }
            }

            if (cachedStbdUMPoints.ContainsKey(s))
            {
                Gizmos.color = upperMouldPointColor;
                foreach (Vector3 pt in cachedStbdUMPoints[s]) if (pt != Vector3.zero) Gizmos.DrawCube(pt, cubeSize);
            }
        }

        // 2. Draw Longitudinal Guide Lines (Connecting across stations)
        if (showStrakeLines && stations.Count > 0)
        {
            // Lower-Lower Mould Lines
            Gizmos.color = lowerLowerLineColor;
            for (int k = 0; k < llCount; k++)
            {
                Vector3 portStem = (k < cachedPortLLStemStrakes.Count) ? cachedPortLLStemStrakes[k] : Vector3.zero;
                Vector3 portStern = (k < cachedPortLLSternTimbers.Count) ? cachedPortLLSternTimbers[k] : Vector3.zero;
                DrawFairedStrakeLine(cachedPortLLPoints, stations, k, portStem, portStern);

                Vector3 stbdStem = (k < cachedStbdLLStemStrakes.Count) ? cachedStbdLLStemStrakes[k] : Vector3.zero;
                Vector3 stbdStern = (k < cachedStbdLLSternTimbers.Count) ? cachedStbdLLSternTimbers[k] : Vector3.zero;
                DrawFairedStrakeLine(cachedStbdLLPoints, stations, k, stbdStem, stbdStern);
            }

            // Upper-Lower Mould Lines
            Gizmos.color = upperLowerLineColor;
            for (int k = 0; k < ulCount; k++)
            {
                Vector3 portStem = (k < cachedPortULStemStrakes.Count) ? cachedPortULStemStrakes[k] : Vector3.zero;
                Vector3 portStern = (k < cachedPortULSternTimbers.Count) ? cachedPortULSternTimbers[k] : Vector3.zero;
                DrawFairedStrakeLine(cachedPortULPoints, stations, k, portStem, portStern);

                Vector3 stbdStem = (k < cachedStbdULStemStrakes.Count) ? cachedStbdULStemStrakes[k] : Vector3.zero;
                Vector3 stbdStern = (k < cachedStbdULSternTimbers.Count) ? cachedStbdULSternTimbers[k] : Vector3.zero;
                DrawFairedStrakeLine(cachedStbdULPoints, stations, k, stbdStem, stbdStern);
            }

            // Upper Mould Lines
            Gizmos.color = upperMouldLineColor;
            for (int k = 0; k < umCount; k++)
            {
                Vector3 portStem = (k < cachedPortUMStemStrakes.Count) ? cachedPortUMStemStrakes[k] : Vector3.zero;
                Vector3 portStern = (k < cachedPortUMSternTimbers.Count) ? cachedPortUMSternTimbers[k] : Vector3.zero;
                DrawFairedStrakeLine(cachedPortUMPoints, stations, k, portStem, portStern);

                Vector3 stbdStem = (k < cachedStbdUMStemStrakes.Count) ? cachedStbdUMStemStrakes[k] : Vector3.zero;
                Vector3 stbdStern = (k < cachedStbdUMSternTimbers.Count) ? cachedStbdUMSternTimbers[k] : Vector3.zero;
                DrawFairedStrakeLine(cachedStbdUMPoints, stations, k, stbdStem, stbdStern);
            }
        }
        }

        // 3. Draw Stem and Stern Profile Intersection Point Markers
        if (showProfileIntersectionPoints)
        {
            bool drawTimbers = (intersectionMarkerMode == ProfileIntersectionMarkerMode.OnStructuralTimbers || intersectionMarkerMode == ProfileIntersectionMarkerMode.OnBoth);
            bool drawStrakes = (intersectionMarkerMode == ProfileIntersectionMarkerMode.OnStrakes || intersectionMarkerMode == ProfileIntersectionMarkerMode.OnBoth);

            Gizmos.color = stemIntersectionColor;
            if (drawTimbers)
            {
                foreach (Vector3 pt in cachedPortStemIntersections) Gizmos.DrawSphere(pt, profileIntersectionRadius);
                foreach (Vector3 pt in cachedStbdStemIntersections) Gizmos.DrawSphere(pt, profileIntersectionRadius);
            }
            if (drawStrakes)
            {
                foreach (Vector3 pt in cachedPortLLStemStrakes) Gizmos.DrawSphere(pt, profileIntersectionRadius);
                foreach (Vector3 pt in cachedStbdLLStemStrakes) Gizmos.DrawSphere(pt, profileIntersectionRadius);
                foreach (Vector3 pt in cachedPortULStemStrakes) Gizmos.DrawSphere(pt, profileIntersectionRadius);
                foreach (Vector3 pt in cachedStbdULStemStrakes) Gizmos.DrawSphere(pt, profileIntersectionRadius);
                foreach (Vector3 pt in cachedPortUMStemStrakes) Gizmos.DrawSphere(pt, profileIntersectionRadius);
                foreach (Vector3 pt in cachedStbdUMStemStrakes) Gizmos.DrawSphere(pt, profileIntersectionRadius);
            }

            Gizmos.color = sternIntersectionColor;
            if (drawTimbers)
            {
                foreach (Vector3 pt in cachedPortSternIntersections) Gizmos.DrawSphere(pt, profileIntersectionRadius);
                foreach (Vector3 pt in cachedStbdSternIntersections) Gizmos.DrawSphere(pt, profileIntersectionRadius);
            }
            if (drawStrakes)
            {
                foreach (Vector3 pt in cachedPortLLSternTimbers) Gizmos.DrawSphere(pt, profileIntersectionRadius);
                foreach (Vector3 pt in cachedStbdLLSternTimbers) Gizmos.DrawSphere(pt, profileIntersectionRadius);
                foreach (Vector3 pt in cachedPortULSternTimbers) Gizmos.DrawSphere(pt, profileIntersectionRadius);
                foreach (Vector3 pt in cachedStbdULSternTimbers) Gizmos.DrawSphere(pt, profileIntersectionRadius);
                foreach (Vector3 pt in cachedPortUMSternTimbers) Gizmos.DrawSphere(pt, profileIntersectionRadius);
                foreach (Vector3 pt in cachedStbdUMSternTimbers) Gizmos.DrawSphere(pt, profileIntersectionRadius);
            }
        }

        // 4. Draw Structural & Marking Strake Lines
        if (showStructuralStrakes)
        {
            for (int i = 0; i < cachedStructuralLineColors.Count; i++)
            {
                Color col = cachedStructuralLineColors[i];
                if (i < cachedPortStructuralLines.Count) DrawStructuralLinePoints(cachedPortStructuralLines[i], col);
                if (i < cachedStbdStructuralLines.Count) DrawStructuralLinePoints(cachedStbdStructuralLines[i], col);
            }
        }
    }

    /// <summary>
    /// Rebuilds Structural & Marking Strake Lines (Main Deck Bulwark, Upper Decks, and Upper Deck Bulwarks).
    /// Evaluates fine-resolution 3D sampling across each line's exact longitudinal range (ribband mode).
    /// </summary>
    private void RebuildStructuralStrakes(List<float> stations)
    {
        cachedPortStructuralLines.Clear();
        cachedStbdStructuralLines.Clear();
        cachedStructuralLineColors.Clear();

        if (sheerPlan == null || framingModel == null) return;
        if (!showStructuralStrakes) return;

        float bowZ = (sheerPlan.cachedTopTimberBowIntersect != Vector3.zero) ? sheerPlan.cachedTopTimberBowIntersect.z : sheerPlan.GetFwdMostZ();
        float sternZ = (sheerPlan.cachedTopTimberSternIntersect != Vector3.zero) ? sheerPlan.cachedTopTimberSternIntersect.z : sheerPlan.GetAftMostZ();
        float totalLength = bowZ - sternZ;
        if (totalLength < 0.001f) return;

        int samples = Mathf.Clamp(structuralStrakeSamples, 20, 300);

        // 1. Main Deck Bulwark Line (Pure Top Timber Line with Global Sheer Offset)
        AddStructuralLine(z => sheerPlan.GetTopTimberYAtZ(z, false), sternZ, bowZ, mainDeckBulwarkColor, samples);

        // 2. Upper Deck Floor & Bulwark Lines
        if (sheerPlan.upperDecks != null)
        {
            foreach (var deck in sheerPlan.upperDecks)
            {
                if (deck == null) continue;
                SheerPlan.ModularUpperDeck d = deck;

                float deckAftZ = sternZ + (d.globalAftExtent * totalLength);
                float deckForeZ = sternZ + (d.globalForeExtent * totalLength);

                if (deckForeZ <= deckAftZ + 0.001f) continue;

                // Upper Deck Floor Line
                AddStructuralLine(
                    z => sheerPlan.GetBaseDeckYAtZ(z) + (sheerPlan.GetAbsoluteDeckHeight(d) - d.height), 
                    deckAftZ, deckForeZ, upperDeckColor, samples);

                // Upper Deck Bulwark Line
                AddStructuralLine(
                    z => sheerPlan.GetBaseDeckYAtZ(z) + sheerPlan.GetAbsoluteDeckHeight(d) + d.bulwarkHeight + sheerPlan.GetGlobalSheerOffsetAtZ(z), 
                    deckAftZ, deckForeZ, upperDeckBulwarkColor, samples);
            }
        }
    }

    private void AddStructuralLine(
        System.Func<float, float> getYAtZ, 
        float startZ, 
        float endZ, 
        Color lineCol, 
        int samples)
    {
        if (endZ <= startZ + 0.001f) return;
        samples = Mathf.Max(10, samples);

        List<Vector3> portPts = new List<Vector3>(samples);
        List<Vector3> stbdPts = new List<Vector3>(samples);

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / (samples - 1);
            float z = Mathf.Lerp(startZ, endZ, t);

            float targetY = getYAtZ(z);

            // Port (-1f)
            Vector3 pPt = GetStrakeFramePointAtY(z, -1f, targetY);

            // Starboard (+1f)
            Vector3 sPt = GetStrakeFramePointAtY(z, 1f, targetY);

            portPts.Add(pPt);
            stbdPts.Add(sPt);
        }

        cachedPortStructuralLines.Add(portPts);
        cachedStbdStructuralLines.Add(stbdPts);
        cachedStructuralLineColors.Add(lineCol);
    }

    private void DrawStructuralLinePoints(List<Vector3> pts, Color color)
    {
        if (pts == null || pts.Count < 2) return;
        Gizmos.color = color;
        for (int s = 0; s < pts.Count - 1; s++)
        {
            Vector3 p1 = pts[s];
            Vector3 p2 = pts[s + 1];
            if (p1 != Vector3.zero && p2 != Vector3.zero)
            {
                Gizmos.DrawLine(p1, p2);
            }
        }
    }

    /// <summary>
    /// Calculates Stem (bow) and Stern (stern) profile intersection points for strakes by traversing
    /// from the Master Frame (amidships Z=0) forward and aft along each 3D strake line.
    /// </summary>
    private void RebuildProfileIntersectionsForSegment(
        List<float> stations,
        Dictionary<int, List<Vector3>> pointDict,
        int strakeCount,
        float side,
        List<Vector3> stemOutTimberList,
        List<Vector3> sternOutTimberList,
        List<Vector3> stemOutStrakeList,
        List<Vector3> sternOutStrakeList)
    {
        if (stations == null || stations.Count < 2 || pointDict == null || sheerPlan == null) return;

        // Find Master Frame station index (closest to Z = 0)
        int masterIdx = 0;
        float minAbsZ = float.MaxValue;
        for (int s = 0; s < stations.Count; s++)
        {
            float absZ = Mathf.Abs(stations[s]);
            if (absZ < minAbsZ)
            {
                minAbsZ = absZ;
                masterIdx = s;
            }
        }

        float stemHalfWidth = (framingModel != null) ? framingModel.GetKeelHalfWidth() : ((bodyPlan != null) ? bodyPlan.keelWidth / 2f : 0.25f);

        for (int k = 0; k < strakeCount; k++)
        {
            // 1. FORWARD TRAVERSAL (Master Frame -> Bow / Stem Profile)
            for (int s = masterIdx; s < stations.Count - 1; s++)
            {
                if (!pointDict.ContainsKey(s) || !pointDict.ContainsKey(s + 1)) continue;
                if (pointDict[s].Count <= k || pointDict[s + 1].Count <= k) continue;

                Vector3 pA = pointDict[s][k];
                Vector3 pB = pointDict[s + 1][k];
                if (pA == Vector3.zero) continue;

                float stemZ_A = sheerPlan.GetStemProfileZAtY(pA.y);
                float stemZ_B = (pB != Vector3.zero) ? sheerPlan.GetStemProfileZAtY(pB.y) : stemZ_A;

                bool isLastStation = (s == stations.Count - 2);
                bool isCutOff = (pB == Vector3.zero);
                bool isPastStemCurve = (pB != Vector3.zero && pB.z > stemZ_B + 0.001f && pA.z <= stemZ_A + 0.001f);

                if (isPastStemCurve || isCutOff || isLastStation)
                {
                    Vector3 intersectPt;
                    if (isPastStemCurve)
                    {
                        float minT = 0f;
                        float maxT = 1f;
                        for (int iter = 0; iter < 10; iter++)
                        {
                            float midT = (minT + maxT) * 0.5f;
                            Vector3 midPt = Vector3.Lerp(pA, pB, midT);
                            float stemZ = sheerPlan.GetStemProfileZAtY(midPt.y);
                            if (midPt.z <= stemZ) minT = midT;
                            else maxT = midT;
                        }
                        float tFinal = (minT + maxT) * 0.5f;
                        intersectPt = Vector3.Lerp(pA, pB, tFinal);
                    }
                    else if (!isCutOff && pB != Vector3.zero)
                    {
                        intersectPt = pB;
                    }
                    else
                    {
                        intersectPt = pA;
                    }

                    if (intersectPt != Vector3.zero)
                    {
                        // 1. Point on the Strake line itself
                        stemOutStrakeList.Add(intersectPt);

                        // 2. Point directly on the Stem backbone of the Framing Model (side * stemHalfWidth, stemY, Z)
                        Vector3 stemBackbonePt = new Vector3(side * stemHalfWidth, intersectPt.y, intersectPt.z);
                        stemOutTimberList.Add(stemBackbonePt);
                    }
                    break;
                }
            }

            // 2. AFT TRAVERSAL (Master Frame -> Stern / Stern Profile)
            for (int s = masterIdx; s > 0; s--)
            {
                if (!pointDict.ContainsKey(s) || !pointDict.ContainsKey(s - 1)) continue;
                if (pointDict[s].Count <= k || pointDict[s - 1].Count <= k) continue;

                Vector3 pA = pointDict[s][k];
                Vector3 pB = pointDict[s - 1][k];
                if (pA == Vector3.zero) continue;

                float sternZ_A = sheerPlan.GetSternProfileZAtY(pA.y);
                float sternZ_B = (pB != Vector3.zero) ? sheerPlan.GetSternProfileZAtY(pB.y) : sternZ_A;

                bool isLastStation = (s == 1);
                bool isCutOff = (pB == Vector3.zero);
                bool isPastSternCurve = (pB != Vector3.zero && pB.z < sternZ_B - 0.001f && pA.z >= sternZ_A - 0.001f);

                if (isPastSternCurve || isCutOff || isLastStation)
                {
                    Vector3 intersectPt;
                    if (isPastSternCurve)
                    {
                        float minT = 0f;
                        float maxT = 1f;
                        for (int iter = 0; iter < 10; iter++)
                        {
                            float midT = (minT + maxT) * 0.5f;
                            Vector3 midPt = Vector3.Lerp(pA, pB, midT);
                            float sternZ = sheerPlan.GetSternProfileZAtY(midPt.y);
                            if (midPt.z >= sternZ) minT = midT;
                            else maxT = midT;
                        }
                        float tFinal = (minT + maxT) * 0.5f;
                        intersectPt = Vector3.Lerp(pA, pB, tFinal);
                    }
                    else if (!isCutOff && pB != Vector3.zero)
                    {
                        intersectPt = pB;
                    }
                    else
                    {
                        intersectPt = pA;
                    }

                    if (intersectPt != Vector3.zero)
                    {
                        // 1. Point on the Strake line itself
                        sternOutStrakeList.Add(intersectPt);

                        // 2. Point directly on the Fashion Timber curve of the FashionTimberModel (side * fashionX, y, z)
                        float fashionX = 0f;
                        if (fashionTimberModel != null)
                        {
                            fashionX = fashionTimberModel.GetNaturalHullXAtYZ(intersectPt.y, intersectPt.z);
                        }
                        else if (framingModel != null)
                        {
                            fashionX = Mathf.Abs(framingModel.GetTransverseFramePointAtY(intersectPt.z, 1f, intersectPt.y).x);
                        }

                        Vector3 fashionTimberPt = new Vector3(side * fashionX, intersectPt.y, intersectPt.z);
                        sternOutTimberList.Add(fashionTimberPt);
                    }
                    break;
                }
            }
        }
    }
}
