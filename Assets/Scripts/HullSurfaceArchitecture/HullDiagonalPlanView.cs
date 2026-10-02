using System.Collections.Generic;
using UnityEngine;

namespace HullSurfaceArchitecture
{
    /// <summary>
    /// HullDiagonalPlanView provides the 4th Perspective Plan View (Diagonal Ribband Plan View).
    /// 
    /// Historical & Architectural Role:
    /// Historically on the Mold Loft floor, diagonal lines (ribbands) cut across the bilge at ~45-degree angles.
    /// Because 90% of hull surface wrinkles occur along the turn of the bilge, this script evaluates and
    /// visualizes diagonal ribband curves, ensuring the bilge and floor sweeps are 100% glass-smooth.
    /// </summary>
    [ExecuteInEditMode]
    public class HullDiagonalPlanView : MonoBehaviour
    {
        [Header("Component References")]
        [Tooltip("Reference to the master HullSurfaceGrid component.")]
        public HullSurfaceGrid surfaceGrid;

        [Header("Diagonal Ribband Settings")]
        [Tooltip("Number of diagonal ribband plane cuts across the hull.")]
        [Range(1, 10)]
        public int diagonalCount = 3;

        [Tooltip("Longitudinal resolution along each diagonal ribband.")]
        [Range(10, 300)]
        public int resolution = 80;

        [Header("Visualization Settings")]
        [Tooltip("Display diagonal ribbands in Scene View.")]
        public bool showDiagonals = true;

        [Tooltip("Show visualization on Starboard (+X) side.")]
        public bool showStarboard = true;

        [Tooltip("Show visualization on Port (-X) side.")]
        public bool showPort = true;

        [Tooltip("Show sphere markers at the Bow and Stern endpoints of each diagonal.")]
        public bool showEndpoints = true;

        [Tooltip("Radius of Gizmo spheres drawn at diagonal endpoints.")]
        public float endpointGizmoRadius = 0.10f;

        [Header("Color Settings")]
        public Color diagonalColor = new Color(0f, 1f, 0.8f, 0.95f);
        public Color endpointColor = Color.cyan;

        private List<List<Vector3>> m_CachedStbdDiagonals = new List<List<Vector3>>();
        private List<List<Vector3>> m_CachedPortDiagonals = new List<List<Vector3>>();

        private bool m_IsCacheDirty = true;

        private void Awake()
        {
            FindReferences();
            MarkDirty();
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
        }

        /// <summary>
        /// Evaluates all diagonal ribband curves for specified side (+1 for Starboard, -1 for Port).
        /// </summary>
        public List<List<Vector3>> GetDiagonalRibbands(float sideSign)
        {
            List<List<Vector3>> diagonals = new List<List<Vector3>>();
            FindReferences();
            if (surfaceGrid == null) return diagonals;

            int count = Mathf.Clamp(diagonalCount, 1, 10);

            for (int k = 0; k < count; k++)
            {
                // Angle diagonal progression from lower keel region to upper topside region
                float uStart = (count > 1) ? (0.1f + 0.3f * ((float)k / (count - 1))) : 0.2f;
                float uEnd = (count > 1) ? (0.5f + 0.4f * ((float)k / (count - 1))) : 0.7f;

                List<Vector3> pts = surfaceGrid.GetDiagonalRibbandCurve(uStart, uEnd, sideSign, resolution);
                diagonals.Add(pts);
            }

            return diagonals;
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

            m_CachedStbdDiagonals = GetDiagonalRibbands(1f);
            m_CachedPortDiagonals = GetDiagonalRibbands(-1f);

            m_IsCacheDirty = false;
        }

        private void OnDrawGizmos()
        {
            FindReferences();
            if (surfaceGrid == null || !showDiagonals) return;

            if (m_IsCacheDirty)
            {
                RebuildCache();
            }

            if (showStarboard && m_CachedStbdDiagonals != null)
            {
                DrawDiagonalStack(m_CachedStbdDiagonals);
            }

            if (showPort && m_CachedPortDiagonals != null)
            {
                DrawDiagonalStack(m_CachedPortDiagonals);
            }
        }

        private void DrawDiagonalStack(List<List<Vector3>> list)
        {
            if (list == null) return;

            foreach (var diag in list)
            {
                DrawPolyline(diag, diagonalColor);

                if (showEndpoints && diag.Count > 1)
                {
                    Gizmos.color = endpointColor;
                    Gizmos.DrawSphere(diag[0], endpointGizmoRadius); // Stern tip
                    Gizmos.DrawSphere(diag[diag.Count - 1], endpointGizmoRadius); // Bow tip
                }
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
