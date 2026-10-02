using System.Collections.Generic;
using UnityEngine;

namespace HullSurfaceArchitecture
{
    /// <summary>
    /// HullSheerPlanView provides 2D Sheer Plan View (Profile / Side View) height controls
    /// and visualization for the unified 3D Hull Surface Architecture.
    /// 
    /// Controls:
    /// - Sheer Height at Midships (Y elevation of top timber)
    /// - Forward Sheer Rise (Bow sheer elevation curve)
    /// - Aft Sheer Rise (Stern sheer elevation curve)
    /// - Stem Rake & Keel Line Profile
    /// </summary>
    [ExecuteInEditMode]
    public class HullSheerPlanView : MonoBehaviour
    {
        [Header("Component References")]
        [Tooltip("Reference to master HullSurfaceGrid engine.")]
        public HullSurfaceGrid surfaceGrid;

        [Tooltip("Reference to MasterHullSurfaceController.")]
        public MasterHullSurfaceController masterController;

        [Header("Sheer Line Profile Parameters")]
        [Tooltip("Height of the main sheer line at Midships (Y-axis elevation).")]
        public float sheerHeightMidship = 3.0f;

        [Tooltip("Additional sheer rise height at the Bow (Z = +Length/2).")]
        public float bowSheerRise = 1.5f;

        [Tooltip("Additional sheer rise height at the Stern (Z = -Length/2).")]
        public float sternSheerRise = 0.8f;

        [Tooltip("Curvature exponent of the sheer line profile (1 = linear rise, 2 = quadratic curve).")]
        [Range(1f, 3f)]
        public float sheerCurveExponent = 2.0f;

        [Header("Visualization Settings")]
        [Tooltip("Display 2D Sheer Plan side elevation curves in Scene View.")]
        public bool showSheerPlanCurves = true;

        [Header("Color Settings")]
        public Color sheerLineCurveColor = Color.magenta;
        public Color keelLineCurveColor = Color.yellow;

        private List<Vector3> m_CachedSheerProfileCurve = new List<Vector3>();
        private List<Vector3> m_CachedKeelProfileCurve = new List<Vector3>();

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
                ApplySheerInternal();
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

        /// <summary>
        /// Stage 2 of Master Pipeline: Applies vertical Y elevations from Sheer Profile curve to control grid.
        /// </summary>
        public void ApplySheerInternal()
        {
            FindReferences();
            if (surfaceGrid == null) return;

            int M = surfaceGrid.transverseRows;
            int N = surfaceGrid.longitudinalCols;

            float halfLength = (masterController != null) ? (masterController.lengthBetweenPerpendiculars / 2f) : 15f;

            for (int j = 0; j < N; j++)
            {
                float v = (float)j / (N - 1);
                float z = Mathf.Lerp(-halfLength, halfLength, v);
                float normZ = (z >= 0f) ? (z / halfLength) : (-z / halfLength);

                float rise = (z >= 0f) ? (bowSheerRise * Mathf.Pow(normZ, sheerCurveExponent))
                                       : (sternSheerRise * Mathf.Pow(normZ, sheerCurveExponent));
                float stationSheerY = sheerHeightMidship + rise;

                for (int i = 0; i < M; i++)
                {
                    float u = (float)i / (M - 1);
                    Vector3 currentPt = surfaceGrid.GetControlPoint(i, j);

                    float uHeight = Mathf.Lerp(0f, stationSheerY, u);
                    surfaceGrid.SetControlPointDirect(i, j, new Vector3(currentPt.x, uHeight, z));
                }
            }
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

            // Sample Top Sheer Line (u = 1.0) and Keel Baseline (u = 0.0)
            m_CachedSheerProfileCurve = surfaceGrid.GetLongitudinalRibbandIsoCurve(1.0f, 1f);
            m_CachedKeelProfileCurve = surfaceGrid.GetLongitudinalRibbandIsoCurve(0.0f, 1f);

            m_IsCacheDirty = false;
        }

        private void OnDrawGizmos()
        {
            if (!showSheerPlanCurves) return;
            FindReferences();
            if (surfaceGrid == null) return;

            if (m_IsCacheDirty)
            {
                RebuildCache();
            }

            DrawPolyline(m_CachedSheerProfileCurve, sheerLineCurveColor);
            DrawPolyline(m_CachedKeelProfileCurve, keelLineCurveColor);
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
