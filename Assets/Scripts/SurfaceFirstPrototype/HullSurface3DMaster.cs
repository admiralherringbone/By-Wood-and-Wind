using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFirstPrototype
{
    /// <summary>
    /// Master 3D Surface Evaluator (3D-First Hull Architecture).
    /// Serves as the single 3D source of truth for the entire vessel.
    /// Reads design parameters from SheerPlan, BreadthPlan, and BodyPlan, or uses standalone inspector inputs.
    /// Evaluates 100% fair 3D spatial surface points S(u, z) across the hull.
    /// </summary>
    [ExecuteInEditMode]
    public class HullSurface3DMaster : MonoBehaviour
    {
        [Header("Plan References (Optional - Auto-found if present)")]
        public SheerPlan sheerPlan;
        public BreadthPlan breadthPlan;
        public BodyPlan bodyPlan;
        public FramingModel framingModel;

        [Header("Master Dimensions (Fallback if Plan References Missing)")]
        [Range(5f, 100f)] public float hullLength = 20f;
        [Range(1f, 30f)]  public float hullBeam = 6f;
        [Range(1f, 20f)]  public float hullDepth = 5f;
        [Range(0.1f, 2f)] public float keelWidth = 0.5f;

        [Header("Change Tracking")]
        [HideInInspector] public int changeVersion = 0;
        private int m_LastSheerVer = -1;
        private int m_LastBodyVer = -1;
        private int m_LastBreadthVer = -1;

        private List<C2NaturalSpline.Knot2D> m_SheerKnots = new List<C2NaturalSpline.Knot2D>();
        private List<C2NaturalSpline.Knot2D> m_BreadthKnots = new List<C2NaturalSpline.Knot2D>();
        private List<C2NaturalSpline.Knot2D> m_RisingKnots = new List<C2NaturalSpline.Knot2D>();

        private void Awake()
        {
            FindReferences();
            RebuildSplineCaches();
        }

        private void OnValidate()
        {
            FindReferences();
            MarkDirty();
        }

        public void MarkDirty()
        {
            changeVersion++;
        }

        public void FindReferences()
        {
            if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
            if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
            if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
            if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();
        }

        public float GetAftMostZ()
        {
            if (sheerPlan != null) return sheerPlan.GetAftMostZ();
            return -hullLength * 0.5f;
        }

        public float GetFwdMostZ()
        {
            if (sheerPlan != null) return sheerPlan.GetFwdMostZ();
            return hullLength * 0.5f;
        }

        public float GetKeelHalfWidth()
        {
            if (bodyPlan != null) return bodyPlan.keelWidth / 2f;
            return keelWidth / 2f;
        }

        private void Update()
        {
            int sVer = (sheerPlan != null) ? sheerPlan.changeVersion : 0;
            int bVer = (bodyPlan != null) ? bodyPlan.changeVersion : 0;
            int brVer = (breadthPlan != null) ? breadthPlan.changeVersion : 0;

            if (sVer != m_LastSheerVer || bVer != m_LastBodyVer || brVer != m_LastBreadthVer)
            {
                RebuildSplineCaches();
                m_LastSheerVer = sVer;
                m_LastBodyVer = bVer;
                m_LastBreadthVer = brVer;
                MarkDirty();
            }
        }

        public void RebuildSplineCaches()
        {
            m_SheerKnots.Clear();
            m_BreadthKnots.Clear();
            m_RisingKnots.Clear();

            float aftZ = GetAftMostZ();
            float fwdZ = GetFwdMostZ();
            const int knotCount = 15;

            for (int i = 0; i <= knotCount; i++)
            {
                float t = i / (float)knotCount;
                float z = Mathf.Lerp(aftZ, fwdZ, t);

                float sheerY = (sheerPlan != null) ? sheerPlan.GetMainSheerYAtZ(z) : (hullDepth + Mathf.Abs(z) * 0.05f);
                float maxBreadthX = (breadthPlan != null) ? breadthPlan.GetTheoreticalBreadthXAtZ(z) : (hullBeam * 0.5f * (1f - Mathf.Pow(t - 0.5f, 2f)));
                float risingY = (sheerPlan != null) ? sheerPlan.GetRisingLineYAtZ(z) : (0.2f + Mathf.Abs(z) * 0.08f);

                m_SheerKnots.Add(new C2NaturalSpline.Knot2D(z, sheerY));
                m_BreadthKnots.Add(new C2NaturalSpline.Knot2D(z, maxBreadthX));
                m_RisingKnots.Add(new C2NaturalSpline.Knot2D(z, risingY));
            }
        }

        /// <summary>
        /// Single Source of 3D Truth for the entire hull.
        /// Evaluates exact 3D spatial coordinate S(u, z) on the master hull surface.
        /// z in [Z_stern, Z_bow], u in [0, 1] (0 = Keel, 0.25 = Floor, 0.50 = Bilge, 1.0 = Sheer).
        /// </summary>
        public Vector3 EvaluateHullSurface(float u, float z, float sideSign)
        {
            u = Mathf.Clamp01(u);
            sideSign = Mathf.Approximately(sideSign, 0f) ? 1f : Mathf.Sign(sideSign);

            float hw = GetKeelHalfWidth();
            float aftZ = GetAftMostZ();
            float fwdZ = GetFwdMostZ();

            z = Mathf.Clamp(z, Mathf.Min(aftZ, fwdZ), Mathf.Max(aftZ, fwdZ));

            // 1. Evaluate C² Natural Spline guide line values at station Z
            float sheerY = C2NaturalSpline.Evaluate1DSpline(m_SheerKnots, z);
            float maxBreadthX = C2NaturalSpline.Evaluate1DSpline(m_BreadthKnots, z);
            float risingY = C2NaturalSpline.Evaluate1DSpline(m_RisingKnots, z);

            float keelY = (sheerPlan != null) ? sheerPlan.GetKeelBaselineYAtZ(z) : 0f;
            risingY = Mathf.Max(risingY, keelY);

            float bilgeY = (sheerPlan != null) ? 
                (z >= sheerPlan.GetBilgeBowZ() ? sheerPlan.GetProjectedBowBilgeYAtZ(z) : 
                (z <= sheerPlan.GetBilgeSternZ() ? sheerPlan.GetProjectedSternBilgeYAtZ(z) : sheerPlan.GetBilgeYAtZ(z))) 
                : (risingY + 0.8f);

            float hobY = (sheerPlan != null) ? sheerPlan.GetTheoreticalHobYAtZ(z) : (bilgeY + 1f);

            // Uncollapsed floor and bilge widths for 100% frame surface coverage
            float floorX = (breadthPlan != null) ? breadthPlan.GetFloorLineXAtZ(z) : (hw + 0.5f);
            float bilgeX = (breadthPlan != null) ? breadthPlan.GetBilgeXAtZ(z) : (hw + 1.2f);

            if (sheerPlan != null)
            {
                float bowRisingZ = sheerPlan.cachedBowRisingEnd.z;
                float sternRisingZ = sheerPlan.cachedSternRisingEnd.z;

                if (z > bowRisingZ || z < sternRisingZ)
                {
                    floorX = Mathf.Lerp(hw, maxBreadthX, 0.35f);
                    bilgeX = Mathf.Lerp(hw, maxBreadthX, 0.70f);
                }
            }

            floorX = Mathf.Max(floorX, hw);
            bilgeX = Mathf.Clamp(bilgeX, hw, maxBreadthX);
            float sheerX = (breadthPlan != null) ? breadthPlan.GetTopTimberXAtZ(z) : maxBreadthX;

            // 2. Construct uncollapsed 5-node section for station Z
            SectionNodes nodes = new SectionNodes();
            nodes.p0 = new Vector3(sideSign * hw, keelY, z);
            nodes.p1 = new Vector3(sideSign * floorX, risingY, z);
            nodes.p2 = new Vector3(sideSign * bilgeX, bilgeY, z);
            nodes.p3 = new Vector3(sideSign * maxBreadthX, hobY, z);
            nodes.p4 = new Vector3(sideSign * sheerX, sheerY, z);

            // 3. Evaluate 3D surface point on mould section curve
            Vector3 pt;
            if (bodyPlan != null)
            {
                pt = bodyPlan.EvaluateBodySectionAtU(u, nodes);
            }
            else
            {
                // Fallback C² spline sweep across 5 nodes
                pt = SimpleSectionSweep(u, nodes);
            }

            // 4. Respect Stem/Stern timber post bottom landing cutoff
            if (sheerPlan != null)
            {
                float stemY = 0f;
                bool isPastBow = z >= sheerPlan.cachedBowRisingEnd.z;
                bool isPastStern = z <= sheerPlan.cachedSternRisingEnd.z;

                if (isPastBow)
                {
                    stemY = sheerPlan.GetStemProfileYAtZ(z);
                    if (pt.y < stemY)
                    {
                        pt.y = stemY;
                        pt.x = sideSign * hw;
                    }
                }
                else if (isPastStern)
                {
                    stemY = sheerPlan.GetSternProfileYAtZ(z);
                    if (pt.y < stemY)
                    {
                        pt.y = stemY;
                        pt.x = sideSign * hw;
                    }
                }
            }

            return pt;
        }

        private Vector3 SimpleSectionSweep(float u, SectionNodes nodes)
        {
            if (u <= 0.25f)
            {
                float t = u / 0.25f;
                return Vector3.Lerp(nodes.p0, nodes.p1, t);
            }
            else if (u <= 0.50f)
            {
                float t = (u - 0.25f) / 0.25f;
                return Vector3.Lerp(nodes.p1, nodes.p2, t);
            }
            else if (u <= 0.75f)
            {
                float t = (u - 0.50f) / 0.25f;
                return Vector3.Lerp(nodes.p2, nodes.p3, t);
            }
            else
            {
                float t = (u - 0.75f) / 0.25f;
                return Vector3.Lerp(nodes.p3, nodes.p4, t);
            }
        }
    }
}
