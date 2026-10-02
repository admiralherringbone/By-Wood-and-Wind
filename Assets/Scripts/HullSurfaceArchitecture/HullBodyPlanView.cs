using System.Collections.Generic;
using UnityEngine;

namespace HullSurfaceArchitecture
{
    /// <summary>
    /// HullBodyPlanView provides 2D Body Plan View (Transverse Frame Section) shape controls
    /// and visualization for the unified 3D Hull Surface Architecture.
    /// 
    /// Controls:
    /// - Deadrise Rise Percentage (bottom angle from keel out to turn of bilge)
    /// - Midship Hull Flare Convexity (fullness of the midship transverse arc)
    /// - Transverse Section Frame Overlays (Forward body on right, Aft body on left)
    /// </summary>
    [ExecuteInEditMode]
    public class HullBodyPlanView : MonoBehaviour
    {
        [Header("Component References")]
        [Tooltip("Reference to master HullSurfaceGrid engine.")]
        public HullSurfaceGrid surfaceGrid;

        [Tooltip("Reference to MasterHullSurfaceController.")]
        public MasterHullSurfaceController masterController;

        [Header("Body Section Shape Parameters")]
        [Tooltip("Deadrise floor rise as percentage (0 = flat bottom, 0.3 = 30% rise at bilge turn).")]
        [Range(0f, 0.5f)]
        public float deadrisePercentage = 0.10f;

        [Tooltip("Midship hull flare / convexity factor (0 = straight linear flare, 0.3 = full rounded bilge/topside).")]
        [Range(0f, 0.5f)]
        public float midshipFlareFactor = 0.15f;

        [Header("Visualization Settings")]
        [Tooltip("Display 2D Body Plan section curves in Scene View.")]
        public bool showBodyPlanSections = true;

        [Tooltip("Number of preview stations to draw in Body Plan View.")]
        [Range(3, 21)]
        public int displayStationCount = 11;

        [Header("Color Settings")]
        public Color forwardBodyColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        public Color masterSectionColor = Color.yellow;
        public Color aftBodyColor = new Color(1f, 0.5f, 0.2f, 0.9f);

        private List<List<Vector3>> m_CachedBodySections = new List<List<Vector3>>();

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
                ApplyBodyShapeInternal();
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
        /// Stage 4 of Master Pipeline: Applies transverse body section parameters (deadrise, flare) to control grid.
        /// </summary>
        public void ApplyBodyShapeInternal()
        {
            FindReferences();
            if (surfaceGrid == null) return;

            int M = surfaceGrid.transverseRows;
            int N = surfaceGrid.longitudinalCols;

            for (int j = 0; j < N; j++)
            {
                // Station max breadth X at top timber (u = 1.0)
                float stationMaxX = surfaceGrid.GetControlPoint(M - 1, j).x;

                for (int i = 0; i < M; i++)
                {
                    float u = (float)i / (M - 1);
                    Vector3 pt = surfaceGrid.GetControlPoint(i, j);

                    // 1. Deadrise floor rise boost (elevates bilge turn)
                    if (u > 0f && u < 0.5f)
                    {
                        float deadriseBoost = pt.x * deadrisePercentage * (1f - (u / 0.5f));
                        pt.y += deadriseBoost;
                    }

                    // 2. Midship transverse section flare convexity
                    if (u > 0.05f && u < 0.95f)
                    {
                        float flareArc = Mathf.Sin(u * Mathf.PI) * (stationMaxX * midshipFlareFactor * 0.4f);
                        pt.x = Mathf.Min(stationMaxX, pt.x + flareArc);
                    }

                    surfaceGrid.SetControlPointDirect(i, j, pt);
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
            m_CachedBodySections.Clear();
            if (surfaceGrid == null) return;

            int count = Mathf.Clamp(displayStationCount, 3, 21);
            for (int k = 0; k < count; k++)
            {
                float v = (float)k / (count - 1); // 0 (Stern) -> 0.5 (Midships) -> 1.0 (Bow)
                m_CachedBodySections.Add(surfaceGrid.GetTransverseFrameIsoCurve(v, 1f));
            }

            m_IsCacheDirty = false;
        }

        private void OnDrawGizmos()
        {
            if (!showBodyPlanSections) return;
            FindReferences();
            if (surfaceGrid == null) return;

            if (m_IsCacheDirty)
            {
                RebuildCache();
            }

            int count = m_CachedBodySections.Count;
            int midIdx = count / 2;

            for (int k = 0; k < count; k++)
            {
                Color c = (k == midIdx) ? masterSectionColor : ((k > midIdx) ? forwardBodyColor : aftBodyColor);
                DrawPolyline(m_CachedBodySections[k], c);
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
}
