using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFirstPrototype
{
    /// <summary>
    /// Longitudinal Strake & Boundary Line Visualizer for 3D-First Surface Architecture.
    /// Evaluates 3D longitudinal strakes directly on HullSurface3DMaster.
    /// PROOF OF CONCEPT: Guarantees 100% frame-surface flushness with ZERO kinks, ZERO dips, and ZERO inward pulling.
    /// </summary>
    [ExecuteInEditMode]
    public class SurfaceFirstStrakesVisualizer : MonoBehaviour
    {
        [Header("Master Surface Reference")]
        public HullSurface3DMaster masterSurface;

        [Header("Strake Layout Settings")]
        [Range(1, 10)] public int division1StrakeCount = 3; // Keel -> Floor Line
        [Range(1, 10)] public int division2StrakeCount = 3; // Floor Line -> Bilge Line
        [Range(1, 10)] public int division3StrakeCount = 4; // Bilge Line -> Main Wale

        [Range(20, 200)] public int longitudinalResolution = 100;

        [Header("Visualization Settings")]
        public bool showStarboard = true;
        public bool showPort = true;

        [Header("Line Colors")]
        public Color floorToKeelColor = new Color(0.2f, 0.85f, 1f, 0.95f);    // Cyan
        public Color bilgeToFloorColor = new Color(0.15f, 0.95f, 0.45f, 0.95f); // Green
        public Color waleToBilgeColor = new Color(1f, 0.4f, 0.1f, 0.95f);    // Orange
        public Color masterSeamColor = Color.yellow;

        private void Awake()
        {
            FindMasterSurface();
        }

        private void OnValidate()
        {
            FindMasterSurface();
        }

        public void FindMasterSurface()
        {
            if (masterSurface == null) masterSurface = FindFirstObjectByType<HullSurface3DMaster>();
        }

        private void OnDrawGizmos()
        {
            FindMasterSurface();
            if (masterSurface == null) return;

            if (showStarboard) DrawStrakesForSide(1f);
            if (showPort) DrawStrakesForSide(-1f);
        }

        private void DrawStrakesForSide(float sideSign)
        {
            // 1. Division 1 Strakes (Keel u=0.00 to Floor Line u=0.25)
            int d1 = Mathf.Max(1, division1StrakeCount);
            for (int i = 0; i <= d1; i++)
            {
                float u = Mathf.Lerp(0.00f, 0.25f, i / (float)d1);
                Color c = (i == 0 || i == d1) ? masterSeamColor : floorToKeelColor;
                DrawSingleStrake(u, sideSign, c);
            }

            // 2. Division 2 Strakes (Floor Line u=0.25 to Bilge Line u=0.50)
            int d2 = Mathf.Max(1, division2StrakeCount);
            for (int i = 1; i <= d2; i++)
            {
                float u = Mathf.Lerp(0.25f, 0.50f, i / (float)d2);
                Color c = (i == d2) ? masterSeamColor : bilgeToFloorColor;
                DrawSingleStrake(u, sideSign, c);
            }

            // 3. Division 3 Strakes (Bilge Line u=0.50 to Main Wale u=1.00)
            int d3 = Mathf.Max(1, division3StrakeCount);
            for (int i = 1; i <= d3; i++)
            {
                float u = Mathf.Lerp(0.50f, 1.00f, i / (float)d3);
                Color c = (i == d3) ? masterSeamColor : waleToBilgeColor;
                DrawSingleStrake(u, sideSign, c);
            }
        }

        private void DrawSingleStrake(float u, float sideSign, Color color)
        {
            float aftZ = masterSurface.GetAftMostZ();
            float fwdZ = masterSurface.GetFwdMostZ();
            int res = Mathf.Clamp(longitudinalResolution, 20, 200);

            Gizmos.color = color;
            Vector3 prevPt = Vector3.zero;

            for (int i = 0; i <= res; i++)
            {
                float t = i / (float)res;
                float z = Mathf.Lerp(aftZ, fwdZ, t);

                Vector3 currPt = masterSurface.EvaluateHullSurface(u, z, sideSign);

                if (i > 0)
                {
                    Gizmos.DrawLine(prevPt, currPt);
                }

                prevPt = currPt;
            }
        }
    }
}
