using System.Collections.Generic;
using UnityEngine;

namespace HullSurfaceArchitecture
{
    /// <summary>
    /// HullBreadthPlanView provides direct 2D Plan View breadth control and visualization
    /// for the unified 3D Hull Surface Architecture.
    /// 
    /// Uses the exact 4-Point Cubic Bézier Control Arm Architecture (bowArmAngle, bowArmLength,
    /// midshipFwdArmLength, midshipAftArmLength, sternArmAngle, sternArmLength) from BreadthPlan,
    /// seamlessly feeding station maximum breadths into the 3D HullSurfaceGrid!
    /// </summary>
    [ExecuteInEditMode]
    public class HullBreadthPlanView : MonoBehaviour
    {
        [Header("Component References")]
        [Tooltip("Reference to the master HullSurfaceGrid component.")]
        public HullSurfaceGrid surfaceGrid;

        [Tooltip("Reference to MasterHullSurfaceController.")]
        public MasterHullSurfaceController masterController;

        [Header("Primary Breadth Dimensions")]
        [Tooltip("Maximum half-beam at Midships (Starboard +X distance from centerline).")]
        public float maxBeam = 4.0f;

        [Tooltip("Transom half-width at the Stern as a percentage (0 to 1) of Max Beam.")]
        [Range(0f, 1f)]
        public float transomWidthPercentage = 0.5f;

        [Tooltip("Floor Line max half-width at Midships.")]
        public float floorMaxBeam = 2.0f;

        [Header("Forward Cubic Bézier Controls")]
        [Tooltip("Angle of the Bow control arm (0 = Horizontal X-axis, 90 = Pointing at Midship).")]
        [Range(0f, 90f)]
        public float bowArmAngle = 0f;

        [Tooltip("Length of the Bow control arm as a percentage (0 to 1) of maximum distance.")]
        [Range(0f, 1f)]
        public float bowArmLength = 0.8f;

        [Tooltip("Length of the Midship Forward control arm as a percentage (0 to 1) of the half length.")]
        [Range(0f, 1f)]
        public float midshipFwdArmLength = 0.5f;

        [Header("Aft Cubic Bézier Controls")]
        [Tooltip("Length of the Midship Aft control arm as a percentage (0 to 1) of the half length.")]
        [Range(0f, 1f)]
        public float midshipAftArmLength = 0.5f;

        [Tooltip("Angle of the Stern control arm (only active when Transom Width is 0). (0 = Horizontal X-axis, 90 = Pointing at Midship).")]
        [Range(0f, 90f)]
        public float sternArmAngle = 0f;

        [Tooltip("Length of the Stern control arm as a percentage (0 to 1) of maximum distance.")]
        [Range(0f, 1f)]
        public float sternArmLength = 0.8f;

        [Header("Legacy Fullness Slider Presets")]
        [Tooltip("High-level forward bow curve fullness slider (adjusts control arm lengths automatically).")]
        [Range(0.1f, 2.5f)]
        public float bowCurveFullness = 0.7f;

        [Tooltip("High-level aft stern curve fullness slider (adjusts control arm lengths automatically).")]
        [Range(0.1f, 2.5f)]
        public float sternCurveFullness = 0.7f;

        [Header("Visualization Settings")]
        [Tooltip("Display 2D Plan View breadth curves in Scene View.")]
        public bool showBreadthPlanCurves = true;

        [Tooltip("Show Starboard (+X) breadth curves.")]
        public bool showStarboard = true;

        [Tooltip("Show Port (-X) breadth curves.")]
        public bool showPort = true;

        [Tooltip("Show sphere markers at key fullness control stations (Bow, Midships, Stern, Shoulders).")]
        public bool showFullnessCurveMarkers = true;

        [Tooltip("Display the Bézier control arms and shoulder tangent vectors in Scene View.")]
        public bool showControlArms = true;

        [Tooltip("Radius of sphere markers drawn at fullness control stations.")]
        public float markerGizmoRadius = 0.15f;

        [Header("Color Settings")]
        public Color deckFullnessCurveColor = new Color(0f, 1f, 0.4f, 1f);
        public Color midHeightBreadthCurveColor = new Color(0.2f, 0.8f, 1f, 0.85f);
        public Color fullnessMarkerColor = Color.yellow;
        public Color controlArmLineColor = new Color(1f, 0.5f, 0f, 0.95f); // Bright Orange

        // --- CACHED CUBIC BÉZIER CONTROL POINTS ---
        [HideInInspector] public Vector3 cachedFwdP0;
        [HideInInspector] public Vector3 cachedFwdP1;
        [HideInInspector] public Vector3 cachedFwdP2;
        [HideInInspector] public Vector3 cachedFwdP3;

        [HideInInspector] public Vector3 cachedAftP0;
        [HideInInspector] public Vector3 cachedAftP1;
        [HideInInspector] public Vector3 cachedAftP2;
        [HideInInspector] public Vector3 cachedAftP3;
        [HideInInspector] public bool cachedIsTransom;

        private List<Vector3> m_CachedStbdDeckFullnessCurve = new List<Vector3>();
        private List<Vector3> m_CachedPortDeckFullnessCurve = new List<Vector3>();
        private List<Vector3> m_CachedStbdMidBreadthCurve = new List<Vector3>();
        private List<Vector3> m_CachedPortMidBreadthCurve = new List<Vector3>();

        private bool m_IsCacheDirty = true;

        private void Awake()
        {
            FindReferences();
            TriggerMasterPipeline();
        }

        private void OnEnable()
        {
            FindReferences();
            if (surfaceGrid != null)
            {
                surfaceGrid.OnGridModified -= OnGridModifiedHandler;
                surfaceGrid.OnGridModified += OnGridModifiedHandler;
            }
        }

        private void OnDisable()
        {
            if (surfaceGrid != null)
            {
                surfaceGrid.OnGridModified -= OnGridModifiedHandler;
            }
        }

        private void OnGridModifiedHandler()
        {
            m_IsCacheDirty = true;
        }

        private void OnValidate()
        {
            FindReferences();
            TriggerMasterPipeline();
        }

        public void TriggerMasterPipeline()
        {
            if (masterController != null)
            {
                masterController.RebuildFullHullPipeline();
            }
            else
            {
                ApplyBreadthInternal();
                if (surfaceGrid != null) surfaceGrid.MarkDirty();
            }
            MarkDirty();
        }

        public void MarkDirty()
        {
            m_IsCacheDirty = true;
        }

        public void FindReferences()
        {
            if (surfaceGrid == null) surfaceGrid = GetComponentInParent<HullSurfaceGrid>();
            if (surfaceGrid == null) surfaceGrid = FindFirstObjectByType<HullSurfaceGrid>();

            if (masterController == null) masterController = GetComponentInParent<MasterHullSurfaceController>();
            if (masterController == null) masterController = FindFirstObjectByType<MasterHullSurfaceController>();
        }

        public float GetSheerHeightAtZ(float z)
        {
            float halfLength = (masterController != null) ? (masterController.lengthBetweenPerpendiculars / 2f) : 15f;
            float midY = (masterController != null) ? masterController.sheerHeightMidship : 3f;

            HullSheerPlanView sheerView = GetComponentInParent<HullSheerPlanView>();
            if (sheerView == null) sheerView = FindFirstObjectByType<HullSheerPlanView>();

            float bowRise = (sheerView != null) ? sheerView.bowSheerRise : 1.5f;
            float sternRise = (sheerView != null) ? sheerView.sternSheerRise : 0.8f;
            float exp = (sheerView != null) ? sheerView.sheerCurveExponent : 2f;
            if (sheerView != null) midY = sheerView.sheerHeightMidship;

            float normZ = Mathf.Abs(z) / halfLength;
            float rise = (z >= 0f) ? bowRise * Mathf.Pow(normZ, exp) : sternRise * Mathf.Pow(normZ, exp);
            return midY + rise;
        }

        /// <summary>
        /// Stage 3 of Master Pipeline: Applies 2D Breadth Plan half-width X coordinates to control grid
        /// using exact 4-point Cubic Bézier curve math.
        /// </summary>
        public void ApplyBreadthInternal()
        {
            FindReferences();
            if (surfaceGrid == null) return;

            int M = surfaceGrid.transverseRows;
            int N = surfaceGrid.longitudinalCols;

            float halfLength = (masterController != null) ? (masterController.lengthBetweenPerpendiculars / 2f) : 15f;
            float hw = surfaceGrid.keelHalfWidth;

            float bowY = GetSheerHeightAtZ(halfLength);
            float sternY = GetSheerHeightAtZ(-halfLength);
            float midY = GetSheerHeightAtZ(0f);

            // 1. FORWARD CUBIC BÉZIER CONTROL POINTS (P0 -> P1 -> P2 -> P3)
            // P0: Stem tip (hw, bowY, halfLength)
            // P3: Midships peak (maxBeam, midY, 0)
            Vector3 fwdP0 = new Vector3(hw, bowY, halfLength);
            Vector3 fwdP3 = new Vector3(maxBeam, midY, 0f);

            // P2: Midship Forward arm extending along +Z at X = maxBeam
            float fwdArmZLen = halfLength * Mathf.Clamp(midshipFwdArmLength, 0.05f, 0.95f);
            Vector3 fwdP2 = fwdP3 + new Vector3(0, 0, fwdArmZLen);

            // P1: Bow control point extending from stem P0 at bowArmAngle & bowArmLength
            // bowArmAngle = 0° => Arm is horizontal along +X axis (full/round bow)
            // bowArmAngle = 90° => Arm points diagonally toward Midship P2 (fine/sharp bow)
            float deltaX = maxBeam - hw;
            float deltaZ = fwdP2.z - halfLength; // Negative value (pointing aft)

            float maxFwdAngleRad = Mathf.Atan2(deltaZ, deltaX); // Angle from +X axis towards P2
            float fwdAngleRad = Mathf.Lerp(0f, maxFwdAngleRad, bowArmAngle / 90f);

            float fwdDirX = Mathf.Cos(fwdAngleRad);
            float fwdDirZ = Mathf.Sin(fwdAngleRad);

            float fwdMaxDistX = deltaX / (Mathf.Abs(fwdDirX) > 0.0001f ? Mathf.Abs(fwdDirX) : 1f);
            float fwdMaxDistZ = Mathf.Abs(deltaZ) / (Mathf.Abs(fwdDirZ) > 0.0001f ? Mathf.Abs(fwdDirZ) : 1f);
            float fwdMaxDist = (Mathf.Abs(fwdDirZ) < 0.0001f) ? deltaX : Mathf.Min(fwdMaxDistX, fwdMaxDistZ);

            Vector3 fwdP1 = fwdP0 + new Vector3(fwdDirX, 0, fwdDirZ) * (fwdMaxDist * bowArmLength);

            // 2. AFT BÉZIER CONTROL POINTS (P0 -> P1 -> P2 -> P3)
            // P0: Midships peak
            Vector3 aftP0 = fwdP3;
            float aftArmZLen = halfLength * Mathf.Clamp(midshipAftArmLength, 0.05f, 0.95f);
            Vector3 aftP1 = aftP0 + new Vector3(0, 0, -aftArmZLen);

            Vector3 aftP2, aftP3;
            bool isTransom = transomWidthPercentage > 0.001f;
            if (!isTransom)
            {
                aftP3 = new Vector3(hw, sternY, -halfLength);
                float aftDeltaX = maxBeam - hw;
                float aftDeltaZ = aftP1.z - (-halfLength); // Positive value (pointing forward)

                float maxAftAngleRad = Mathf.Atan2(aftDeltaZ, aftDeltaX); // Angle from +X axis towards P1
                float aftAngleRad = Mathf.Lerp(0f, maxAftAngleRad, sternArmAngle / 90f);

                float aftDirX = Mathf.Cos(aftAngleRad);
                float aftDirZ = Mathf.Sin(aftAngleRad);

                float aftMaxDistX = aftDeltaX / (Mathf.Abs(aftDirX) > 0.0001f ? Mathf.Abs(aftDirX) : 1f);
                float aftMaxDistZ = aftDeltaZ / (Mathf.Abs(aftDirZ) > 0.0001f ? Mathf.Abs(aftDirZ) : 1f);
                float aftMaxDist = (Mathf.Abs(aftDirZ) < 0.0001f) ? aftDeltaX : Mathf.Min(aftMaxDistX, aftMaxDistZ);

                aftP2 = aftP3 + new Vector3(aftDirX, 0, aftDirZ) * (aftMaxDist * sternArmLength);
            }
            else
            {
                float xTransom = Mathf.Lerp(hw, maxBeam, transomWidthPercentage);
                aftP2 = new Vector3(xTransom, sternY, -halfLength);
                aftP3 = aftP2;
            }

            // Cache Control Points
            cachedFwdP0 = fwdP0; cachedFwdP1 = fwdP1; cachedFwdP2 = fwdP2; cachedFwdP3 = fwdP3;
            cachedAftP0 = aftP0; cachedAftP1 = aftP1; cachedAftP2 = aftP2; cachedAftP3 = aftP3;
            cachedIsTransom = isTransom;

            // 3. EVALUATE MAXIMUM BREADTH X(Z) FOR ALL STATIONS ALONG THE LENGTH
            for (int j = 0; j < N; j++)
            {
                float v = (float)j / (N - 1);
                float z = Mathf.Lerp(-halfLength, halfLength, v);

                float stationMaxX;
                if (z >= 0f)
                {
                    if (z >= fwdP0.z) stationMaxX = fwdP0.x;
                    else
                    {
                        float t = GetBezierTAtZ(fwdP0, fwdP1, fwdP2, fwdP3, z);
                        stationMaxX = CalculateCubicBezierPoint(t, fwdP0, fwdP1, fwdP2, fwdP3).x;
                    }
                }
                else
                {
                    if (!isTransom)
                    {
                        if (z <= aftP3.z) stationMaxX = aftP3.x;
                        else
                        {
                            float t = GetBezierTAtZ(aftP0, aftP1, aftP2, aftP3, z);
                            stationMaxX = CalculateCubicBezierPoint(t, aftP0, aftP1, aftP2, aftP3).x;
                        }
                    }
                    else
                    {
                        if (z <= aftP2.z) stationMaxX = aftP2.x;
                        else
                        {
                            float t = GetBezierTAtZ(aftP0, aftP1, aftP2, Vector3.zero, z);
                            stationMaxX = CalculateQuadraticBezierPoint(t, aftP0, aftP1, aftP2).x;
                        }
                    }
                }

                for (int i = 0; i < M; i++)
                {
                    float u = (float)i / (M - 1);
                    Vector3 currentPt = surfaceGrid.GetControlPoint(i, j);

                    float uWidth = Mathf.Lerp(hw, stationMaxX, u);
                    surfaceGrid.SetControlPointDirect(i, j, new Vector3(uWidth, currentPt.y, z));
                }
            }
        }

        // --- BÉZIER UTILITY SOLVERS ---

        public static Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            return (uu * p0) + (2f * u * t * p1) + (tt * p2);
        }

        public static Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            return (uuu * p0) + (3f * uu * t * p1) + (3f * u * tt * p2) + (ttt * p3);
        }

        public static float GetBezierTAtZ(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float targetZ)
        {
            float min = 0f;
            float max = 1f;
            float endZ = (p3 == Vector3.zero) ? p2.z : p3.z;
            bool isDecreasing = (p0.z > endZ); // True if Z decreases as t goes 0 -> 1

            for (int i = 0; i < 24; i++)
            {
                float mid = (min + max) * 0.5f;
                float z = (p3 == Vector3.zero)
                    ? CalculateQuadraticBezierPoint(mid, p0, p1, p2).z
                    : CalculateCubicBezierPoint(mid, p0, p1, p2, p3).z;

                if (isDecreasing)
                {
                    if (z < targetZ) max = mid;
                    else min = mid;
                }
                else
                {
                    if (z < targetZ) min = mid;
                    else max = mid;
                }
            }
            return (min + max) * 0.5f;
        }

        [ContextMenu("Rebuild Cache")]
        public void ForceRebuildCache()
        {
            MarkDirty();
            RebuildCache();
        }

        private void RebuildCache()
        {
            FindReferences();
            if (surfaceGrid == null) return;

            if (m_CachedStbdDeckFullnessCurve == null) m_CachedStbdDeckFullnessCurve = new List<Vector3>();
            if (m_CachedPortDeckFullnessCurve == null) m_CachedPortDeckFullnessCurve = new List<Vector3>();
            if (m_CachedStbdMidBreadthCurve == null) m_CachedStbdMidBreadthCurve = new List<Vector3>();
            if (m_CachedPortMidBreadthCurve == null) m_CachedPortMidBreadthCurve = new List<Vector3>();

            m_CachedStbdDeckFullnessCurve.Clear();
            m_CachedPortDeckFullnessCurve.Clear();
            m_CachedStbdMidBreadthCurve.Clear();
            m_CachedPortMidBreadthCurve.Clear();

            float hw = surfaceGrid.keelHalfWidth;
            int samplesPerSegment = 60;

            // 1. Sample Aft Segment (Stern -> Midships)
            for (int i = 0; i <= samplesPerSegment; i++)
            {
                float t = (float)i / samplesPerSegment; // t: 0.0 (Midships) -> 1.0 (Stern)
                Vector3 pt = cachedIsTransom
                    ? CalculateQuadraticBezierPoint(t, cachedAftP0, cachedAftP1, cachedAftP2)
                    : CalculateCubicBezierPoint(t, cachedAftP0, cachedAftP1, cachedAftP2, cachedAftP3);

                m_CachedStbdDeckFullnessCurve.Add(pt);
                m_CachedPortDeckFullnessCurve.Add(MirrorX(pt));

                float midX = Mathf.Lerp(hw, pt.x, 0.5f);
                Vector3 midPt = new Vector3(midX, pt.y * 0.5f, pt.z);
                m_CachedStbdMidBreadthCurve.Add(midPt);
                m_CachedPortMidBreadthCurve.Add(MirrorX(midPt));
            }

            // 2. Sample Forward Segment (Midships -> Bow)
            for (int i = 1; i <= samplesPerSegment; i++)
            {
                float t = (float)i / samplesPerSegment; // t: 1.0 (Midships) down to 0.0 (Bow P0)
                float tFwd = 1.0f - t;
                Vector3 pt = CalculateCubicBezierPoint(tFwd, cachedFwdP0, cachedFwdP1, cachedFwdP2, cachedFwdP3);

                m_CachedStbdDeckFullnessCurve.Add(pt);
                m_CachedPortDeckFullnessCurve.Add(MirrorX(pt));

                float midX = Mathf.Lerp(hw, pt.x, 0.5f);
                Vector3 midPt = new Vector3(midX, pt.y * 0.5f, pt.z);
                m_CachedStbdMidBreadthCurve.Add(midPt);
                m_CachedPortMidBreadthCurve.Add(MirrorX(midPt));
            }

            m_IsCacheDirty = false;
        }

        private void OnDrawGizmos()
        {
            if (!showBreadthPlanCurves) return;
            FindReferences();
            if (surfaceGrid == null) return;

            if (m_IsCacheDirty)
            {
                RebuildCache();
            }

            if (showStarboard)
            {
                DrawPolyline(m_CachedStbdDeckFullnessCurve, deckFullnessCurveColor);
                DrawPolyline(m_CachedStbdMidBreadthCurve, midHeightBreadthCurveColor);
                if (showFullnessCurveMarkers) DrawMarkers(m_CachedStbdDeckFullnessCurve);
            }

            if (showPort)
            {
                DrawPolyline(m_CachedPortDeckFullnessCurve, deckFullnessCurveColor);
                DrawPolyline(m_CachedPortMidBreadthCurve, midHeightBreadthCurveColor);
                if (showFullnessCurveMarkers) DrawMarkers(m_CachedPortDeckFullnessCurve);
            }

            if (showControlArms)
            {
                DrawControlArmSkeleton();
            }
        }

        private void DrawControlArmSkeleton()
        {
            float hw = surfaceGrid.keelHalfWidth;

            // Starboard Points
            Vector3 stbdP0 = cachedFwdP0;
            Vector3 stbdP1 = cachedFwdP1;
            Vector3 stbdP2 = cachedFwdP2;
            Vector3 stbdP3 = cachedFwdP3;

            Vector3 stbdAftP1 = cachedAftP1;
            Vector3 stbdAftP2 = cachedAftP2;
            Vector3 stbdAftP3 = cachedAftP3;

            // Port Points (Mirrored)
            Vector3 portP0 = MirrorX(stbdP0);
            Vector3 portP1 = MirrorX(stbdP1);
            Vector3 portP2 = MirrorX(stbdP2);
            Vector3 portP3 = MirrorX(stbdP3);
            Vector3 portAftP1 = MirrorX(stbdAftP1);
            Vector3 portAftP2 = MirrorX(stbdAftP2);
            Vector3 portAftP3 = MirrorX(stbdAftP3);

            // 1. Draw Centerline Axis (White/Cyan)
            Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
            Gizmos.DrawLine(new Vector3(0, stbdAftP3.y, stbdAftP3.z), new Vector3(0, stbdP0.y, stbdP0.z));

            // 2. Draw Transom Width Bar, Bow Keel Width Bar, & Midship Beam Bar
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.DrawLine(portAftP3, stbdAftP3);
            Gizmos.DrawLine(portP0, stbdP0);
            Gizmos.DrawLine(portP3, stbdP3);

            // 3. Draw Tangent Control Arm Skeleton Lines (Bright Orange)
            Gizmos.color = controlArmLineColor;
            
            // Forward Control Arms
            Gizmos.DrawLine(stbdP0, stbdP1);
            Gizmos.DrawLine(stbdP3, stbdP2);
            Gizmos.DrawLine(portP0, portP1);
            Gizmos.DrawLine(portP3, portP2);

            // Aft Control Arms
            Gizmos.DrawLine(stbdP3, stbdAftP1);
            Gizmos.DrawLine(portP3, portAftP1);
            if (cachedIsTransom)
            {
                Gizmos.DrawLine(stbdAftP1, stbdAftP2);
                Gizmos.DrawLine(portAftP1, portAftP2);
            }
            else
            {
                Gizmos.DrawLine(stbdAftP3, stbdAftP2);
                Gizmos.DrawLine(portAftP3, portAftP2);
            }

            // 4. Draw Control Node Handles (Spheres)
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(stbdP3, markerGizmoRadius * 1.2f);
            Gizmos.DrawSphere(portP3, markerGizmoRadius * 1.2f);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(stbdP1, markerGizmoRadius);
            Gizmos.DrawSphere(stbdP2, markerGizmoRadius);
            Gizmos.DrawSphere(portP1, markerGizmoRadius);
            Gizmos.DrawSphere(portP2, markerGizmoRadius);

            Gizmos.DrawSphere(stbdAftP1, markerGizmoRadius);
            Gizmos.DrawSphere(stbdAftP2, markerGizmoRadius);
            Gizmos.DrawSphere(portAftP1, markerGizmoRadius);
            Gizmos.DrawSphere(portAftP2, markerGizmoRadius);
        }

        private Vector3 MirrorX(Vector3 v)
        {
            return new Vector3(-v.x, v.y, v.z);
        }

        private void DrawMarkers(List<Vector3> pts)
        {
            if (pts == null || pts.Count == 0) return;
            Gizmos.color = fullnessMarkerColor;

            int count = pts.Count;
            int midIdx = count / 2;
            int fwdShoulderIdx = (midIdx + count - 1) / 2;
            int aftShoulderIdx = midIdx / 2;

            Gizmos.DrawSphere(pts[0], markerGizmoRadius);
            Gizmos.DrawSphere(pts[aftShoulderIdx], markerGizmoRadius);
            Gizmos.DrawSphere(pts[midIdx], markerGizmoRadius * 1.3f);
            Gizmos.DrawSphere(pts[fwdShoulderIdx], markerGizmoRadius);
            Gizmos.DrawSphere(pts[count - 1], markerGizmoRadius);
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
}
