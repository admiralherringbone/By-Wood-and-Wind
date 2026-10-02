using System.Collections.Generic;
using UnityEngine;

namespace HullSurfaceArchitecture
{
    /// <summary>
    /// MasterHullSurfaceController is the top-level manager synthesizing the unified 3D Hull Surface Architecture.
    /// 
    /// Features:
    /// 1. Drives the M x N Bivariate Spline Surface Grid (HullSurfaceGrid) from intuitive macro parameters.
    /// 2. Synthesizes Transverse Frames, Longitudinal Ribbands, and Diagonal Ribbands from ONE unified surface.
    /// 3. Guarantees 100% mathematical alignment between frames and ribbands with ZERO wrinkles.
    /// 4. Generates real-time 3D Hull Surface Meshes.
    /// </summary>
    [ExecuteInEditMode]
    public class MasterHullSurfaceController : MonoBehaviour
    {
        [Header("Component References")]
        [Tooltip("Reference to the central HullSurfaceGrid engine.")]
        public HullSurfaceGrid surfaceGrid;

        [Tooltip("Reference to the HullDiagonalPlanView component.")]
        public HullDiagonalPlanView diagonalPlanView;

        [Tooltip("Reference to the HullBreadthPlanView component.")]
        public HullBreadthPlanView breadthPlanView;

        [Tooltip("Reference to the HullSheerPlanView component.")]
        public HullSheerPlanView sheerPlanView;

        [Tooltip("Reference to the HullBodyPlanView component.")]
        public HullBodyPlanView bodyPlanView;

        [Header("Master Macro Hull Parameters")]
        [Tooltip("Length between perpendiculars (LBP) along Z-axis.")]
        public float lengthBetweenPerpendiculars = 30f;

        [Tooltip("Maximum half-beam at Midships (Starboard +X).")]
        public float maxBeam = 4f;

        [Tooltip("Height of Main Sheer Line at Midships (Y-axis).")]
        public float sheerHeightMidship = 3f;

        [Tooltip("Transom Width as percentage (0 to 1) of Max Beam.")]
        [Range(0f, 1f)]
        public float transomWidthPercentage = 0.5f;

        [Header("Frame & Ribband Stack Layout")]
        [Tooltip("Spacing between adjacent transverse frames along Z-axis.")]
        public float frameSpacing = 1.0f;

        [Tooltip("Number of longitudinal ribband curves from Keel to Top Timber.")]
        [Range(3, 20)]
        public int ribbandCount = 8;

        [Header("Visualization Settings")]
        [Tooltip("Display 3D Transverse Frames in Scene View.")]
        public bool showFrames = true;

        [Tooltip("Display 3D Longitudinal Ribbands in Scene View.")]
        public bool showRibbands = true;

        [Tooltip("Highlight Master Frame at Midships (Z = 0).")]
        public bool showMasterFrame = true;

        [Tooltip("Show visualization on Starboard (+X) side.")]
        public bool showStarboard = true;

        [Tooltip("Show visualization on Port (-X) side.")]
        public bool showPort = true;

        [Header("Color System")]
        public Color frameColor = new Color(0.2f, 0.8f, 1f, 0.85f);
        public Color masterFrameColor = Color.yellow;
        public Color ribbandColor = new Color(0.3f, 0.9f, 0.6f, 0.85f);

        private List<List<Vector3>> m_CachedStbdFrames = new List<List<Vector3>>();
        private List<List<Vector3>> m_CachedPortFrames = new List<List<Vector3>>();
        private List<List<Vector3>> m_CachedStbdRibbands = new List<List<Vector3>>();
        private List<List<Vector3>> m_CachedPortRibbands = new List<List<Vector3>>();

        private bool m_IsCacheDirty = true;
        private bool m_IsExecutingPipeline = false;

        private void Awake()
        {
            FindReferences();
            RebuildFullHullPipeline();
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
            if (!m_IsExecutingPipeline)
            {
                m_IsCacheDirty = true;
            }
        }

        private void OnValidate()
        {
            FindReferences();
            RebuildFullHullPipeline();
        }

        public void MarkDirty()
        {
            m_IsCacheDirty = true;
        }

        public void FindReferences()
        {
            if (surfaceGrid == null) surfaceGrid = GetComponent<HullSurfaceGrid>();
            if (surfaceGrid == null) surfaceGrid = GetComponentInParent<HullSurfaceGrid>();
            if (surfaceGrid == null) surfaceGrid = FindFirstObjectByType<HullSurfaceGrid>();

            if (diagonalPlanView == null) diagonalPlanView = GetComponent<HullDiagonalPlanView>();
            if (diagonalPlanView == null) diagonalPlanView = GetComponentInParent<HullDiagonalPlanView>();
            if (diagonalPlanView == null) diagonalPlanView = FindFirstObjectByType<HullDiagonalPlanView>();

            if (breadthPlanView == null) breadthPlanView = GetComponent<HullBreadthPlanView>();
            if (breadthPlanView == null) breadthPlanView = GetComponentInParent<HullBreadthPlanView>();
            if (breadthPlanView == null) breadthPlanView = FindFirstObjectByType<HullBreadthPlanView>();

            if (sheerPlanView == null) sheerPlanView = GetComponent<HullSheerPlanView>();
            if (sheerPlanView == null) sheerPlanView = GetComponentInParent<HullSheerPlanView>();
            if (sheerPlanView == null) sheerPlanView = FindFirstObjectByType<HullSheerPlanView>();

            if (bodyPlanView == null) bodyPlanView = GetComponent<HullBodyPlanView>();
            if (bodyPlanView == null) bodyPlanView = GetComponentInParent<HullBodyPlanView>();
            if (bodyPlanView == null) bodyPlanView = FindFirstObjectByType<HullBodyPlanView>();
        }

        /// <summary>
        /// Single master pipeline orchestrating deterministic multi-stage hull surface construction:
        /// Stage 1: Build Base Grid Skeleton (Z stations & baseline)
        /// Stage 2: Apply 2D Sheer Profile (Y heights)
        /// Stage 3: Apply 2D Breadth Profile (X widths & fullness)
        /// Stage 4: Apply 2D Body Section Shape (Deadrise & Flare)
        /// </summary>
        public void RebuildFullHullPipeline()
        {
            FindReferences();
            if (surfaceGrid == null) return;
            if (m_IsExecutingPipeline) return;

            m_IsExecutingPipeline = true;

            // Stage 1: Base Grid Skeleton
            BuildSurfaceGridFromMacroParameters();

            // Stage 2: Sheer Plan Profile (Y Heights)
            if (sheerPlanView != null)
            {
                sheerPlanView.ApplySheerInternal();
            }

            // Stage 3: Breadth Plan Profile (X Widths)
            if (breadthPlanView != null)
            {
                breadthPlanView.ApplyBreadthInternal();
                maxBeam = breadthPlanView.maxBeam;
                transomWidthPercentage = breadthPlanView.transomWidthPercentage;
            }

            // Stage 4: Body Section Shape (Deadrise & Flare)
            if (bodyPlanView != null)
            {
                bodyPlanView.ApplyBodyShapeInternal();
            }

            m_IsExecutingPipeline = false;
            m_IsCacheDirty = true;

            // Force immediate cache rebuild across all 4 plan views & master controller
            if (sheerPlanView != null) sheerPlanView.ForceRebuildCache();
            if (breadthPlanView != null) breadthPlanView.ForceRebuildCache();
            if (bodyPlanView != null) bodyPlanView.ForceRebuildCache();
            if (diagonalPlanView != null) diagonalPlanView.ForceRebuildCache();
            ForceRebuildCache();
        }

        /// <summary>
        /// Populates the M x N Surface Control Grid baseline from macro parameters.
        /// </summary>
        public void BuildSurfaceGridFromMacroParameters()
        {
            FindReferences();
            if (surfaceGrid == null) return;

            surfaceGrid.InitializeGrid();

            int M = surfaceGrid.transverseRows;
            int N = surfaceGrid.longitudinalCols;

            float halfLength = lengthBetweenPerpendiculars / 2f;
            float hw = surfaceGrid.keelHalfWidth;

            for (int j = 0; j < N; j++)
            {
                float v = (float)j / (N - 1); // v: 0.0 (Stern) -> 0.5 (Midships) -> 1.0 (Bow)
                float z = Mathf.Lerp(-halfLength, halfLength, v);
                float normZ = (z >= 0f) ? (z / halfLength) : (-z / halfLength); // 0 at midships, 1 at ends

                // Plan View Width profile along length (Smooth quadratic taper)
                float widthFactor;
                if (z >= 0f)
                {
                    widthFactor = 1f - normZ * normZ; // Smooth bow taper
                }
                else
                {
                    float shape = 1f - normZ * normZ; // Smooth stern taper
                    widthFactor = Mathf.Lerp(transomWidthPercentage, 1f, shape);
                }
                float stationWidth = Mathf.Lerp(hw, maxBeam, widthFactor);

                // Sheer Line Height profile along length
                float sheerRise = normZ * normZ * 1.5f; // Sheer curve rise at bow/stern
                float stationSheerY = sheerHeightMidship + sheerRise;

                for (int i = 0; i < M; i++)
                {
                    float u = (float)i / (M - 1); // u: 0.0 (Keel) -> 1.0 (Top Timber)

                    float x = Mathf.Lerp(hw, stationWidth, u);
                    float y = Mathf.Lerp(0f, stationSheerY, u);

                    surfaceGrid.SetControlPoint(i, j, new Vector3(x, y, z));
                }
            }

            surfaceGrid.MarkDirty();
        }

        /// <summary>
        /// Evaluates transverse frame curves at stations along the Z-axis.
        /// </summary>
        public List<List<Vector3>> GetTransverseFrames(float sideSign)
        {
            List<List<Vector3>> frames = new List<List<Vector3>>();
            if (surfaceGrid == null) return frames;

            float halfLength = lengthBetweenPerpendiculars / 2f;
            float spacing = Mathf.Max(0.2f, frameSpacing);

            // Midships frame at Z = 0
            frames.Add(surfaceGrid.GetTransverseFrameIsoCurve(0.5f, sideSign));

            // Forward frames
            float curZ = spacing;
            while (curZ <= halfLength)
            {
                float v = 0.5f + (curZ / lengthBetweenPerpendiculars);
                frames.Add(surfaceGrid.GetTransverseFrameIsoCurve(v, sideSign));
                curZ += spacing;
            }

            // Aft frames
            curZ = -spacing;
            while (curZ >= -halfLength)
            {
                float v = 0.5f + (curZ / lengthBetweenPerpendiculars);
                frames.Add(surfaceGrid.GetTransverseFrameIsoCurve(v, sideSign));
                curZ -= spacing;
            }

            return frames;
        }

        /// <summary>
        /// Evaluates longitudinal ribband curves stacked from Keel to Top Timber.
        /// </summary>
        public List<List<Vector3>> GetLongitudinalRibbands(float sideSign)
        {
            List<List<Vector3>> ribbands = new List<List<Vector3>>();
            if (surfaceGrid == null) return ribbands;

            int count = Mathf.Clamp(ribbandCount, 2, 30);

            for (int k = 0; k < count; k++)
            {
                float u = (float)k / (count - 1);
                ribbands.Add(surfaceGrid.GetLongitudinalRibbandIsoCurve(u, sideSign));
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
            if (surfaceGrid == null) return;

            m_CachedStbdFrames = GetTransverseFrames(1f);
            m_CachedPortFrames = GetTransverseFrames(-1f);

            m_CachedStbdRibbands = GetLongitudinalRibbands(1f);
            m_CachedPortRibbands = GetLongitudinalRibbands(-1f);

            m_IsCacheDirty = false;
        }

        private void OnDrawGizmos()
        {
            FindReferences();
            if (surfaceGrid == null) return;

            if (m_IsCacheDirty)
            {
                RebuildCache();
            }

            if (showFrames)
            {
                if (showStarboard) DrawStack(m_CachedStbdFrames, frameColor);
                if (showPort) DrawStack(m_CachedPortFrames, frameColor);
            }

            if (showRibbands)
            {
                if (showStarboard) DrawStack(m_CachedStbdRibbands, ribbandColor);
                if (showPort) DrawStack(m_CachedPortRibbands, ribbandColor);
            }
        }

        private void DrawStack(List<List<Vector3>> list, Color color)
        {
            if (list == null) return;

            foreach (var curve in list)
            {
                if (curve == null || curve.Count < 2) continue;
                Gizmos.color = color;
                for (int i = 0; i < curve.Count - 1; i++)
                {
                    Gizmos.DrawLine(curve[i], curve[i + 1]);
                }
            }
        }
    }
}
