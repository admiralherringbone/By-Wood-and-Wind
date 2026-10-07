using System.Collections.Generic;
using UnityEngine;

namespace SurfaceFirstPrototype
{
    /// <summary>
    /// Natural C² Cubic Spline Evaluator (Elastic Batten Simulation).
    /// Enforces continuous second derivative (curvature) across all knots.
    /// Minimizes physical bending energy \int \kappa^2 ds, eliminating all derivative kinks and polynomial spikes.
    /// </summary>
    public static class C2NaturalSpline
    {
        public struct Knot2D
        {
            public float z;
            public float val;
            public Knot2D(float z, float val) { this.z = z; this.val = val; }
        }

        /// <summary>
        /// Evaluates a 1D value (e.g. Y height or X width) along station Z using a C² Natural Cubic Spline.
        /// </summary>
        public static float Evaluate1DSpline(List<Knot2D> knots, float evalZ)
        {
            int n = (knots != null) ? knots.Count : 0;
            if (n == 0) return 0f;
            if (n == 1) return knots[0].val;

            // Ensure knots are sorted by Z ascending
            List<Knot2D> k = new List<Knot2D>(knots);
            k.Sort((a, b) => a.z.CompareTo(b.z));

            if (evalZ <= k[0].z) return k[0].val;
            if (evalZ >= k[n - 1].z) return k[n - 1].val;

            // Solve C² Tridiagonal system for second derivatives M_i
            float[] h = new float[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                h[i] = Mathf.Max(0.0001f, k[i + 1].z - k[i].z);
            }

            float[] a = new float[n];
            float[] b = new float[n];
            float[] c = new float[n];
            float[] d = new float[n];

            // Natural boundary conditions: M_0 = 0, M_{n-1} = 0
            b[0] = 1f; c[0] = 0f; d[0] = 0f;
            a[n - 1] = 0f; b[n - 1] = 1f; d[n - 1] = 0f;

            for (int i = 1; i < n - 1; i++)
            {
                a[i] = h[i - 1];
                b[i] = 2f * (h[i - 1] + h[i]);
                c[i] = h[i];
                d[i] = 6f * ((k[i + 1].val - k[i].val) / h[i] - (k[i].val - k[i - 1].val) / h[i - 1]);
            }

            // Thomas algorithm for tridiagonal system
            float[] M = SolveTridiagonal(a, b, c, d, n);

            // Find interval [k[idx], k[idx+1]] containing evalZ
            int idx = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (evalZ >= k[i].z && evalZ <= k[i + 1].z)
                {
                    idx = i;
                    break;
                }
            }

            float dz = h[idx];
            float dt1 = k[idx + 1].z - evalZ;
            float dt2 = evalZ - k[idx].z;

            // C² Cubic Interpolation Formula
            float val = (M[idx] * dt1 * dt1 * dt1 + M[idx + 1] * dt2 * dt2 * dt2) / (6f * dz)
                        + (k[idx].val - M[idx] * dz * dz / 6f) * (dt1 / dz)
                        + (k[idx + 1].val - M[idx + 1] * dz * dz / 6f) * (dt2 / dz);

            return val;
        }

        /// <summary>
        /// Evaluates a 3D point along a C² Natural Cubic Spline threaded through 3D control knots.
        /// </summary>
        public static List<Vector3> BuildNatural3DSpline(List<Vector3> knots, int stepsPerBay)
        {
            List<Vector3> result = new List<Vector3>();
            int n = (knots != null) ? knots.Count : 0;
            if (n < 2) return result;

            stepsPerBay = Mathf.Clamp(stepsPerBay, 2, 50);

            // Extract X, Y, Z as 1D knot lists vs Z (or cumulative distance)
            List<Knot2D> knotsX = new List<Knot2D>();
            List<Knot2D> knotsY = new List<Knot2D>();

            for (int i = 0; i < n; i++)
            {
                knotsX.Add(new Knot2D(knots[i].z, knots[i].x));
                knotsY.Add(new Knot2D(knots[i].z, knots[i].y));
            }

            float minZ = knots[0].z;
            float maxZ = knots[n - 1].z;
            int totalSteps = (n - 1) * stepsPerBay;

            for (int step = 0; step <= totalSteps; step++)
            {
                float t = step / (float)totalSteps;
                float curZ = Mathf.Lerp(minZ, maxZ, t);
                float curX = Evaluate1DSpline(knotsX, curZ);
                float curY = Evaluate1DSpline(knotsY, curZ);
                result.Add(new Vector3(curX, curY, curZ));
            }

            return result;
        }

        private static float[] SolveTridiagonal(float[] a, float[] b, float[] c, float[] d, int n)
        {
            float[] cPrime = new float[n];
            float[] dPrime = new float[n];
            float[] x = new float[n];

            cPrime[0] = c[0] / b[0];
            dPrime[0] = d[0] / b[0];

            for (int i = 1; i < n; i++)
            {
                float temp = b[i] - a[i] * cPrime[i - 1];
                if (Mathf.Abs(temp) < 0.00001f) temp = 0.00001f;
                if (i < n - 1) cPrime[i] = c[i] / temp;
                dPrime[i] = (d[i] - a[i] * dPrime[i - 1]) / temp;
            }

            x[n - 1] = dPrime[n - 1];
            for (int i = n - 2; i >= 0; i--)
            {
                x[i] = dPrime[i] - cPrime[i] * x[i + 1];
            }

            return x;
        }
    }
}
