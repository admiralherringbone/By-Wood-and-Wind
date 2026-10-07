using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFirstPrototype
{
    /// <summary>
    /// Transverse Frame Visualizer for the 3D-First Surface Prototype.
    /// Evaluates 3D transverse frames cut directly from HullSurface3DMaster.
    /// Guarantees 100% mathematical consistency with the master 3D surface.
    /// </summary>
    [ExecuteInEditMode]
    public class SurfaceFirstFramingVisualizer : MonoBehaviour
    {
        [Header("Master Surface Reference")]
        public HullSurface3DMaster masterSurface;

        [Header("Frame Layout Settings")]
        [Range(5, 100)] public int frameStationCount = 25;
        [Range(10, 100)] public int stepsPerFrameHalf = 40;

        [Header("Visualization Settings")]
        public bool showStarboard = true;
        public bool showPort = true;
        public Color starboardColor = new Color(0.15f, 0.95f, 0.35f, 0.95f); // Green
        public Color portColor = new Color(0.95f, 0.25f, 0.2f, 0.95f);      // Red
        public Color nodeColor = Color.cyan;
        public bool showNodeSpheres = true;
        [Range(0.01f, 0.2f)] public float sphereRadius = 0.05f;

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

            float aftZ = masterSurface.GetAftMostZ();
            float fwdZ = masterSurface.GetFwdMostZ();

            int count = Mathf.Clamp(frameStationCount, 5, 100);
            int steps = Mathf.Clamp(stepsPerFrameHalf, 10, 100);

            for (int k = 0; k <= count; k++)
            {
                float t = k / (float)count;
                float z = Mathf.Lerp(aftZ, fwdZ, t);

                if (showStarboard) DrawFrameHalf(z, 1f, starboardColor, steps);
                if (showPort) DrawFrameHalf(z, -1f, portColor, steps);
            }
        }

        private void DrawFrameHalf(float z, float sideSign, Color color, int steps)
        {
            Gizmos.color = color;
            Vector3 prevPt = masterSurface.EvaluateHullSurface(0f, z, sideSign);

            if (showNodeSpheres)
            {
                Gizmos.color = nodeColor;
                Gizmos.DrawSphere(prevPt, sphereRadius);
                Gizmos.color = color;
            }

            for (int i = 1; i <= steps; i++)
            {
                float u = i / (float)steps;
                Vector3 currPt = masterSurface.EvaluateHullSurface(u, z, sideSign);

                Gizmos.DrawLine(prevPt, currPt);

                if (showNodeSpheres && (Mathf.Approximately(u, 0.25f) || Mathf.Approximately(u, 0.50f) || Mathf.Approximately(u, 0.75f) || Mathf.Approximately(u, 1.0f)))
                {
                    Gizmos.color = nodeColor;
                    Gizmos.DrawSphere(currPt, sphereRadius);
                    Gizmos.color = color;
                }

                prevPt = currPt;
            }
        }
    }
}
