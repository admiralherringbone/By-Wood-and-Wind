using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Backbone Model Script.
/// 
/// Visualizes the structural backbone of the ship in 3D:
/// 1. Centerline Backbone: Keel Line, Stem Curve (cubic Bezier), and Stern Post Line.
/// 2. Keel Width Boundaries: Port & Starboard side lines at X = +/- (keelWidth / 2) for Keel, Stem, and Stern Post.
/// 3. Cross Bars & Joint Markers: Visualizes the 3D width envelope and rabbet line where hull curves meet the stem/keel.
/// </summary>
[ExecuteInEditMode]
public class BackboneModel : MonoBehaviour
{
    [Header("Component References")]
    public BodyPlan bodyPlan;
    public SheerPlan sheerPlan;
    public BreadthPlan breadthPlan;

    [Header("Visualization Toggles - Centerline")]
    public bool showCenterlineKeel = true;
    public bool showCenterlineStem = true;
    public bool showCenterlineSternPost = true;

    [Header("Visualization Toggles - Keel Width (Port & Starboard)")]
    public bool showKeelSideLines = true;
    public bool showStemSideLines = true;
    public bool showSternPostSideLines = true;
    public bool showKeelWidthCrossBars = true;

    [Header("Visualization Toggles - Markers")]
    public bool showJointSpheres = true;
    [Range(0.01f, 0.5f)]
    public float jointSphereRadius = 0.1f;

    [Header("Resolution Settings")]
    [Range(10, 200)]
    public int sampleResolution = 50;

    [Header("Colors")]
    public Color centerlineColor = new Color(1f, 0.9f, 0.2f, 1f);       // Bright Yellow
    public Color starboardSideColor = new Color(0.2f, 0.8f, 1f, 0.9f);    // Cyan
    public Color portSideColor = new Color(1f, 0.3f, 0.3f, 0.9f);         // Red
    public Color crossBarColor = new Color(0.8f, 0.8f, 0.8f, 0.7f);       // Light Gray
    public Color jointColor = new Color(0.2f, 1f, 0.4f, 1f);              // Green

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        FindReferences();
    }

    public void FindReferences()
    {
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null) return;

        float hw = (bodyPlan != null) ? (bodyPlan.keelWidth / 2f) : 0.25f;

        // Key Points from SheerPlan
        Vector3 keelStern = sheerPlan.cachedKeelSternPoint;
        Vector3 keelBow = sheerPlan.cachedKeelBowPoint;
        Vector3 stemP1 = sheerPlan.cachedStemP1;
        Vector3 stemP2 = sheerPlan.cachedStemP2;
        Vector3 stemTop = sheerPlan.cachedStemTopPoint;
        Vector3 upperStemTop = sheerPlan.cachedUpperStemTopPoint;
        Vector3 sternPostTop = sheerPlan.cachedSternPostTopPoint;

        // --- 1. KEEL LINE ---
        DrawKeel(keelStern, keelBow, hw);

        // --- 2. STEM CURVE ---
        DrawStem(keelBow, stemP1, stemP2, stemTop, upperStemTop, hw);

        // --- 3. STERN POST ---
        DrawSternPost(keelStern, sternPostTop, hw);

        // --- 4. JOINT SPHERES ---
        if (showJointSpheres)
        {
            Gizmos.color = jointColor;
            Gizmos.DrawSphere(keelStern, jointSphereRadius);
            Gizmos.DrawSphere(keelBow, jointSphereRadius);
            Gizmos.DrawSphere(stemTop, jointSphereRadius);
            if (upperStemTop.y > stemTop.y + 0.001f)
            {
                Gizmos.DrawSphere(upperStemTop, jointSphereRadius);
            }
            Gizmos.DrawSphere(sternPostTop, jointSphereRadius);
        }
    }

    private void DrawKeel(Vector3 keelStern, Vector3 keelBow, float hw)
    {
        // Centerline Keel
        if (showCenterlineKeel)
        {
            Gizmos.color = centerlineColor;
            Gizmos.DrawLine(keelStern, keelBow);
        }

        // Side Keel Lines (Starboard +hw, Port -hw)
        Vector3 stbdStern = new Vector3(hw, keelStern.y, keelStern.z);
        Vector3 stbdBow = new Vector3(hw, keelBow.y, keelBow.z);

        Vector3 portStern = new Vector3(-hw, keelStern.y, keelStern.z);
        Vector3 portBow = new Vector3(-hw, keelBow.y, keelBow.z);

        if (showKeelSideLines)
        {
            Gizmos.color = starboardSideColor;
            Gizmos.DrawLine(stbdStern, stbdBow);

            Gizmos.color = portSideColor;
            Gizmos.DrawLine(portStern, portBow);
        }

        if (showKeelWidthCrossBars)
        {
            Gizmos.color = crossBarColor;
            // Crossbar at Stern Keel joint
            Gizmos.DrawLine(portStern, stbdStern);
            // Crossbar at Bow Keel joint
            Gizmos.DrawLine(portBow, stbdBow);
        }
    }

    private void DrawStem(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 upperTop, float hw)
    {
        List<Vector3> centerlineStemPts = SampleStemCurve(p0, p1, p2, p3, upperTop);

        // Centerline Stem
        if (showCenterlineStem)
        {
            DrawPolyline(centerlineStemPts, centerlineColor);
        }

        // Side Stem Curves
        if (showStemSideLines)
        {
            List<Vector3> stbdPts = OffsetX(centerlineStemPts, hw);
            List<Vector3> portPts = OffsetX(centerlineStemPts, -hw);

            DrawPolyline(stbdPts, starboardSideColor);
            DrawPolyline(portPts, portSideColor);
        }

        // Crossbars along Stem
        if (showKeelWidthCrossBars && centerlineStemPts.Count > 0)
        {
            Gizmos.color = crossBarColor;
            int step = Mathf.Max(1, centerlineStemPts.Count / 10);
            for (int i = 0; i < centerlineStemPts.Count; i += step)
            {
                Vector3 pt = centerlineStemPts[i];
                Gizmos.DrawLine(new Vector3(-hw, pt.y, pt.z), new Vector3(hw, pt.y, pt.z));
            }
            Vector3 topPt = centerlineStemPts[centerlineStemPts.Count - 1];
            Gizmos.DrawLine(new Vector3(-hw, topPt.y, topPt.z), new Vector3(hw, topPt.y, topPt.z));
        }
    }

    private void DrawSternPost(Vector3 keelStern, Vector3 sternPostTop, float hw)
    {
        // Centerline Stern Post
        if (showCenterlineSternPost)
        {
            Gizmos.color = centerlineColor;
            Gizmos.DrawLine(keelStern, sternPostTop);
        }

        // Side Stern Post Lines
        Vector3 stbdBottom = new Vector3(hw, keelStern.y, keelStern.z);
        Vector3 stbdTop = new Vector3(hw, sternPostTop.y, sternPostTop.z);

        Vector3 portBottom = new Vector3(-hw, keelStern.y, keelStern.z);
        Vector3 portTop = new Vector3(-hw, sternPostTop.y, sternPostTop.z);

        if (showSternPostSideLines)
        {
            Gizmos.color = starboardSideColor;
            Gizmos.DrawLine(stbdBottom, stbdTop);

            Gizmos.color = portSideColor;
            Gizmos.DrawLine(portBottom, portTop);
        }

        if (showKeelWidthCrossBars)
        {
            Gizmos.color = crossBarColor;
            Gizmos.DrawLine(portTop, stbdTop);
        }
    }

    private List<Vector3> SampleStemCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 upperTop)
    {
        List<Vector3> pts = new List<Vector3>();
        int res = Mathf.Clamp(sampleResolution, 10, 200);

        // Cubic Bezier curve section
        for (int i = 0; i <= res; i++)
        {
            float t = i / (float)res;
            Vector3 pt = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            pts.Add(pt);
        }

        // Extension to Upper Stem Top Point if present
        if (upperTop.y > p3.y + 0.001f)
        {
            int upperRes = Mathf.Max(5, res / 4);
            for (int i = 1; i <= upperRes; i++)
            {
                float t = i / (float)upperRes;
                Vector3 upperPt = Vector3.Lerp(p3, upperTop, t);
                pts.Add(upperPt);
            }
        }

        return pts;
    }

    private List<Vector3> OffsetX(List<Vector3> inputPts, float xOffset)
    {
        List<Vector3> output = new List<Vector3>(inputPts.Count);
        for (int i = 0; i < inputPts.Count; i++)
        {
            Vector3 p = inputPts[i];
            output.Add(new Vector3(p.x + xOffset, p.y, p.z));
        }
        return output;
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

    public static Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * p0;
        p += 3f * uu * t * p1;
        p += 3f * u * tt * p2;
        p += ttt * p3;

        return p;
    }
}
