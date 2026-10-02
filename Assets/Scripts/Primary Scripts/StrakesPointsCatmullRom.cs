using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centripetal Catmull-Rom Longitudinal Spline Fairing utility for strake lines.
/// </summary>
public static class StrakesPointsCatmullRom
{
    /// <summary>
    /// Evaluates a 3D Centripetal Catmull-Rom spline across a sequence of control points.
    /// </summary>
    public static List<Vector3> BuildCentripetalCatmullRomSpline(List<Vector3> pts, int densityPerBay, float alpha)
    {
        List<Vector3> result = new List<Vector3>();
        int n = pts.Count;
        if (n < 2) return result;

        if (n == 2)
        {
            for (int i = 0; i <= densityPerBay; i++)
            {
                float u = (float)i / densityPerBay;
                result.Add(Vector3.Lerp(pts[0], pts[1], u));
            }
            return result;
        }

        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = (i > 0) ? pts[i - 1] : pts[0] + (pts[0] - pts[1]);
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = (i + 2 < n) ? pts[i + 2] : pts[i + 1] + (pts[i + 1] - pts[i]);

            int steps = (i == n - 2) ? densityPerBay : densityPerBay - 1;
            for (int s = 0; s <= steps; s++)
            {
                float u = (float)s / densityPerBay;
                Vector3 evalPt = EvaluateCentripetalCatmullRom(p0, p1, p2, p3, u, alpha);
                result.Add(evalPt);
            }
        }

        return result;
    }

    /// <summary>
    /// Evaluates 3D Centripetal Catmull-Rom interpolation for 4 control points at local fraction u in [0, 1].
    /// </summary>
    public static Vector3 EvaluateCentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u, float alpha)
    {
        u = Mathf.Clamp01(u);
        alpha = Mathf.Clamp01(alpha);

        float GetKnot(float t, Vector3 a, Vector3 b)
        {
            float d2 = (b - a).sqrMagnitude;
            if (d2 <= 0.000001f) return t + 0.0001f;
            return t + Mathf.Pow(d2, alpha * 0.5f);
        }

        float t0 = 0f;
        float t1 = GetKnot(t0, p0, p1);
        float t2 = GetKnot(t1, p1, p2);
        float t3 = GetKnot(t2, p2, p3);

        if (t2 <= t1 + 0.00001f) return p1;

        float t = Mathf.Lerp(t1, t2, u);

        Vector3 a1 = (t1 > t0) ? (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1 : p1;
        Vector3 a2 = (t2 > t1) ? (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2 : p2;
        Vector3 a3 = (t3 > t2) ? (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3 : p3;

        Vector3 b1 = (t2 > t0) ? (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2 : a2;
        Vector3 b2 = (t3 > t1) ? (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3 : a3;

        Vector3 c = (t2 > t1) ? (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2 : b2;

        return c;
    }
}
