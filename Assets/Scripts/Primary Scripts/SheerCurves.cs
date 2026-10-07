using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// SheerCurves defines the primary 2D profile curves of the hull in the Y/Z plane:
/// - Stem Post curve (tied to Bow Tip of BreadthCurves)
/// - Keel Line (bottom hull baseline)
/// - Stern Post (default height matches Stem Post)
/// - Counter Rake curve
/// - Transom Rake line
/// 
/// Provides GetSternProfileZAtY(y) so BreadthCurves transom corners can lock to moving along 
/// the Transom Rake, Counter, or Stern Post depending on their Y-height.
/// </summary>
[ExecuteInEditMode]
public class SheerCurves : MonoBehaviour
{
    [Header("Linked Plans")]
    public BreadthCurves breadthCurves;

    [Header("Gizmo Settings")]
    public bool showLabels = true;
    public Color profileColor = new Color(0.95f, 0.75f, 0.1f, 0.95f);

    [Header("Main Dimensions")]
    [Tooltip("Length between Perpendiculars (hull base length).")]
    public float lengthBetweenPerpendiculars = 20f;

    [Tooltip("Keel baseline Y position.")]
    public float keelY = 0f;

    [Header("Stem Post (Bow Profile)")]
    [Tooltip("Vertical height of the Stem Post top above Keel Y.")]
    public float stemHeight = 7f;

    [Tooltip("Forward rake distance of the Stem Post top.")]
    public float stemRake = 3f;

    [Range(0f, 1f)] public float stemBottomControlZ = 0.5f;
    [Range(0f, 1f)] public float stemBottomControlY = 0.1f;
    [Range(0f, 1f)] public float stemTopControlZ = 1.0f;
    [Range(0f, 1f)] public float stemTopControlY = 0.6f;

    [Header("Stern Post Profile")]
    [Tooltip("Link Stern Post height to Stem Post height by default.")]
    public bool linkSternHeightToStem = true;

    [Tooltip("Vertical height of the Stern Post top above Keel Y (active if unlinked).")]
    public float sternPostHeight = 7f;

    [Tooltip("Aft rake distance of the Stern Post.")]
    public float sternPostRake = 1f;

    [Header("Counter Rake Curve")]
    public bool enableCounter = true;
    public float counterLength = 2f;
    public float counterHeight = 1.5f;
    [Range(0f, 1f)] public float counterBottomControlZ = 0.5f;
    [Range(0f, 1f)] public float counterBottomControlY = 0.2f;
    [Range(0f, 1f)] public float counterTopControlZ = 0.8f;
    [Range(0f, 1f)] public float counterTopControlY = 0.7f;

    [Header("Transom Rake Line")]
    public float transomRakeAft = 1.2f;
    public float transomRakeHeight = 2.5f;

    [Header("Upper Stem Post Extension")]
    [Tooltip("Normalized extension ratio of the Upper Stem Post between Stem Post Top (0.0) and Transom Rake Top (1.0).")]
    [Range(0f, 1f)] public float upperStemRatio = 0.5f;

    // --- CACHED PROFILE POINTS ---
    [HideInInspector] public Vector3 stemP0; // Bottom of Stem
    [HideInInspector] public Vector3 stemP1;
    [HideInInspector] public Vector3 stemP2;
    [HideInInspector] public Vector3 stemP3; // Top of Stem Post (Bow Tip)

    [HideInInspector] public Vector3 keelStart;
    [HideInInspector] public Vector3 keelEnd;

    [HideInInspector] public Vector3 sternP0; // Bottom of Stern Post
    [HideInInspector] public Vector3 sternP3; // Top of Stern Post

    [HideInInspector] public Vector3 counterP0; // Top of Stern Post
    [HideInInspector] public Vector3 counterP1;
    [HideInInspector] public Vector3 counterP2;
    [HideInInspector] public Vector3 counterP3; // Top of Counter / Base of Transom

    [HideInInspector] public Vector3 transomP0; // Base of Transom
    [HideInInspector] public Vector3 transomP1; // Top of Transom Rake

    [HideInInspector] public Vector3 upperStemTop; // Top of Upper Stem Post Extension

    [HideInInspector] public bool isInitialized = false;
    [HideInInspector] public int changeVersion = 0;

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Reset()
    {
        InitializeDefaultProfile();
    }

    public void EnsureInitialized()
    {
        if (!isInitialized)
        {
            InitializeDefaultProfile();
        }
    }

    [ContextMenu("Reset Profile to Default Ship Hull")]
    public void InitializeDefaultProfile()
    {
        UpdateCachedProfile();
        isInitialized = true;
    }

    private void OnValidate()
    {
        UpdateCachedProfile();
    }

    public void UpdateCachedProfile()
    {
        if (breadthCurves == null) breadthCurves = GetComponentInParent<BreadthCurves>();
        if (breadthCurves == null) breadthCurves = FindFirstObjectByType<BreadthCurves>();

        // Enforce Y-Axis Height Restriction: Stem Top (stemP3) and Stern Post Top (sternP3) Y cannot be lower than Midship Apex Y (BreadthCurves.fwdP3.y)
        if (breadthCurves != null)
        {
            float minApexY = breadthCurves.fwdP3.y;
            if (keelY + stemHeight < minApexY)
            {
                stemHeight = Mathf.Max(0.1f, minApexY - keelY);
            }
            if (keelY + sternPostHeight < minApexY)
            {
                sternPostHeight = Mathf.Max(0.1f, minApexY - keelY);
            }
        }

        if (linkSternHeightToStem)
        {
            sternPostHeight = stemHeight;
        }

        float halfLen = lengthBetweenPerpendiculars / 2f;
        float bowPerpZ = halfLen;
        float sternPerpZ = -halfLen;

        // 1. STEM POST CUBIC BEZIER (P0..P3)
        stemP0 = new Vector3(0f, keelY, bowPerpZ);
        stemP3 = new Vector3(0f, keelY + stemHeight, bowPerpZ + stemRake);

        float stemDz = stemP3.z - stemP0.z;
        float stemDy = stemP3.y - stemP0.y;

        if (stemTopControlY < stemBottomControlY) stemTopControlY = stemBottomControlY;

        stemP1 = stemP0 + new Vector3(0f, stemDy * stemBottomControlY, stemDz * stemBottomControlZ);
        stemP2 = stemP0 + new Vector3(0f, stemDy * stemTopControlY, stemDz * stemTopControlZ);

        // --- STEM CONTROL ARM BOUNDARY CORRIDOR RESTRICTIONS ---
        // 1. Main Chord Line Restriction (stemP0 -> stemP3): Control arms cannot pass inward (aft) of chord line
        float t1Chord = (stemDy > 0.001f) ? Mathf.Clamp01((stemP1.y - stemP0.y) / stemDy) : 0f;
        float z1Chord = Mathf.Lerp(stemP0.z, stemP3.z, t1Chord);

        float t2Chord = (stemDy > 0.001f) ? Mathf.Clamp01((stemP2.y - stemP0.y) / stemDy) : 0f;
        float z2Chord = Mathf.Lerp(stemP0.z, stemP3.z, t2Chord);

        // 2. Cross-Chord Inward Restrictions:
        // stemP1 (Bottom control arm) cannot pass inward of line (stemP0 -> stemP2)
        float dy02 = stemP2.y - stemP0.y;
        float t1Cross = (dy02 > 0.001f) ? Mathf.Clamp01((stemP1.y - stemP0.y) / dy02) : 0f;
        float z1Cross = Mathf.Lerp(stemP0.z, stemP2.z, t1Cross);

        // stemP2 (Top control arm) cannot pass inward of line (stemP1 -> stemP3)
        float dy13 = stemP3.y - stemP1.y;
        float t2Cross = (dy13 > 0.001f) ? Mathf.Clamp01((stemP2.y - stemP1.y) / dy13) : 0f;
        float z2Cross = Mathf.Lerp(stemP1.z, stemP3.z, t2Cross);

        // Apply combined inward Z bounds and outward rectangular bounding box (Z <= stemP3.z, Y in [stemP0.y, stemP3.y])
        float z1Min = Mathf.Max(z1Chord, z1Cross);
        stemP1.z = Mathf.Clamp(stemP1.z, z1Min, stemP3.z);
        stemP1.y = Mathf.Clamp(stemP1.y, stemP0.y, stemP3.y);

        float z2Min = Mathf.Max(z2Chord, z2Cross);
        stemP2.z = Mathf.Clamp(stemP2.z, z2Min, stemP3.z);
        stemP2.y = Mathf.Clamp(stemP2.y, stemP0.y, stemP3.y);

        // 2. KEEL LINE
        sternP0 = new Vector3(0f, keelY, sternPerpZ);
        keelStart = stemP0;
        keelEnd = sternP0;

        // 3. STERN POST (P0..P3)
        sternPostRake = Mathf.Max(0f, sternPostRake); // Stern Post Top cannot move further forward than Stern Post Bottom (sternP0)
        sternP3 = new Vector3(0f, keelY + sternPostHeight, sternPerpZ - sternPostRake);

        // 4. COUNTER RAKE CURVE (P0..P3)
        counterP0 = sternP3;
        counterLength = Mathf.Max(0f, counterLength); // Counter Top (counterP3) cannot move further forward than Stern Post Top (sternP3)
        counterP3 = counterP0 + new Vector3(0f, counterHeight, -counterLength);

        float cDz = counterP3.z - counterP0.z; // negative value (-counterLength)
        float cDy = counterP3.y - counterP0.y; // positive value (+counterHeight)

        if (counterTopControlY < counterBottomControlY) counterTopControlY = counterBottomControlY;

        counterP1 = counterP0 + new Vector3(0f, cDy * counterBottomControlY, cDz * counterBottomControlZ);
        counterP2 = counterP0 + new Vector3(0f, cDy * counterTopControlY, cDz * counterTopControlZ);

        // --- COUNTER CONTROL ARM BOUNDARY CORRIDOR RESTRICTIONS ---
        // 4-Sided Bounding Box (Z in [counterP3.z, counterP0.z], Y in [counterP0.y, counterP3.y]) - Allows Concave curves
        counterP1.y = Mathf.Clamp(counterP1.y, counterP0.y, counterP3.y);
        counterP1.z = Mathf.Clamp(counterP1.z, counterP3.z, counterP0.z);

        counterP2.y = Mathf.Clamp(counterP2.y, counterP1.y, counterP3.y);
        counterP2.z = Mathf.Clamp(counterP2.z, counterP3.z, counterP0.z);

        // 5. TRANSOM RAKE LINE (P0..P1)
        transomP0 = enableCounter ? counterP3 : sternP3;
        transomRakeAft = Mathf.Max(0f, transomRakeAft); // Transom Top (transomP1) cannot move further forward than Counter Top (transomP0)
        transomP1 = transomP0 + new Vector3(0f, transomRakeHeight, -transomRakeAft);

        // 6. UPPER STEM POST EXTENSION
        float maxUpperY = Mathf.Max(stemP3.y, transomP1.y);
        float upperStemY = Mathf.Lerp(stemP3.y, maxUpperY, Mathf.Clamp01(upperStemRatio));
        upperStemTop = new Vector3(0f, upperStemY, stemP3.z);

        changeVersion++;
    }

    // --- STERN PROFILE INTERSECT EVALUATION (Z at Y) ---

    /// <summary>
    /// Evaluates the exact Z coordinate on the 3D Stern Profile (Transom Rake, Counter Rake, or Stern Post)
    /// for a given Y height.
    /// </summary>
    public float GetSternProfileZAtY(float targetY)
    {
        UpdateCachedProfile();

        // 1. Transom Rake Section
        if (targetY >= transomP0.y)
        {
            float dy = transomP1.y - transomP0.y;
            if (Mathf.Abs(dy) < 0.0001f) return transomP0.z;
            float t = Mathf.Clamp01((targetY - transomP0.y) / dy);
            return Mathf.Lerp(transomP0.z, transomP1.z, t);
        }

        // 2. Counter Rake Section (if enabled)
        if (enableCounter && targetY >= counterP0.y)
        {
            float dy = counterP3.y - counterP0.y;
            if (Mathf.Abs(dy) < 0.0001f) return counterP0.z;
            float t = GetBezierTAtY(counterP0, counterP1, counterP2, counterP3, targetY);
            return CalculateCubicBezierPoint(t, counterP0, counterP1, counterP2, counterP3).z;
        }

        // 3. Stern Post Section
        float sternDy = sternP3.y - sternP0.y;
        if (Mathf.Abs(sternDy) < 0.0001f) return sternP3.z;
        float sternT = Mathf.Clamp01((targetY - sternP0.y) / sternDy);
        return Mathf.Lerp(sternP0.z, sternP3.z, sternT);
    }

    /// <summary>
    /// Evaluates the exact Z coordinate on the 3D Stem Profile (Stem Post & Upper Stem Post Extension) for a given Y height.
    /// </summary>
    public float GetStemProfileZAtY(float targetY)
    {
        UpdateCachedProfile();

        if (targetY >= stemP3.y)
        {
            return stemP3.z;
        }

        if (targetY <= stemP0.y)
        {
            return stemP0.z;
        }

        float t = GetBezierTAtY(stemP0, stemP1, stemP2, stemP3, targetY);
        return CalculateCubicBezierPoint(t, stemP0, stemP1, stemP2, stemP3).z;
    }

    public float GetBezierTAtYScalar(float y0, float y1, float y2, float y3, float targetY)
    {
        float dyTotal = y3 - y0;
        if (Mathf.Abs(dyTotal) < 0.0001f) return 0f;

        float t = Mathf.Clamp01((targetY - y0) / dyTotal);
        float min = 0f;
        float max = 1f;

        for (int i = 0; i < 4; i++)
        {
            float u = 1f - t;
            float yVal = (u * u * u * y0) + (3f * u * u * t * y1) + (3f * u * t * t * y2) + (t * t * t * y3);
            float error = yVal - targetY;

            if (Mathf.Abs(error) < 0.00005f) return t;

            if (yVal < targetY) min = Mathf.Max(min, t);
            else max = Mathf.Min(max, t);

            float dydt = 3f * u * u * (y1 - y0) + 6f * u * t * (y2 - y1) + 3f * t * t * (y3 - y2);
            if (Mathf.Abs(dydt) > 0.0001f)
            {
                float nextT = t - error / dydt;
                if (nextT >= min && nextT <= max)
                {
                    t = nextT;
                    continue;
                }
            }

            t = (min + max) * 0.5f;
        }

        return t;
    }

    private float GetBezierTAtY(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float targetY)
    {
        return GetBezierTAtYScalar(p0.y, p1.y, p2.y, p3.y, targetY);
    }

    private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        return (uuu * p0) + (3f * uu * t * p1) + (3f * u * tt * p2) + (ttt * p3);
    }

    private static GUIStyle cachedLabelStyle;
#if UNITY_EDITOR
    private static GUIStyle GetCachedLabelStyle()
    {
        if (cachedLabelStyle == null)
        {
            cachedLabelStyle = new GUIStyle();
            cachedLabelStyle.normal.textColor = Color.yellow;
            cachedLabelStyle.fontSize = 10;
            cachedLabelStyle.alignment = TextAnchor.LowerCenter;
            cachedLabelStyle.fontStyle = FontStyle.Bold;
        }
        return cachedLabelStyle;
    }
#endif

    private void OnDrawGizmos()
    {
        UpdateCachedProfile();

        // 1. Draw Stem Post & Upper Extension (Gold)
        DrawCubicBezier(stemP0, stemP1, stemP2, stemP3, profileColor);
        if (upperStemTop.y > stemP3.y + 0.001f)
        {
            Gizmos.color = profileColor;
            Gizmos.DrawLine(stemP3, upperStemTop);
        }

        // 2. Draw Keel Line (Yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(keelStart, keelEnd);

        // 3. Draw Stern Post (Cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(sternP0, sternP3);

        // 4. Draw Counter Rake (Green)
        if (enableCounter)
        {
            DrawCubicBezier(counterP0, counterP1, counterP2, counterP3, Color.green);
        }

        // 5. Draw Transom Rake (Magenta)
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transomP0, transomP1);

#if UNITY_EDITOR
        // Draw 3D Control Polygon Skeleton Arms (Yellow / Green Dotted)
        Handles.color = new Color(1f, 0.9f, 0.1f, 0.65f);
        Handles.DrawDottedLine(stemP0, stemP1, 3f);
        Handles.DrawDottedLine(stemP3, stemP2, 3f);

        // Draw Stem Boundary Corridor Lines (Dashed Imaginary Lines)
        // 1. Chord Line (stemP0 -> stemP3) - Cyan Dotted
        Handles.color = new Color(0.2f, 0.9f, 1f, 0.75f);
        Handles.DrawDottedLine(stemP0, stemP3, 3f);

        // 2. Outward Rectangular Box Bounds - Orange Dotted
        Vector3 stemCorner = new Vector3(0f, stemP0.y, stemP3.z);
        Handles.color = new Color(1f, 0.65f, 0.1f, 0.75f);
        Handles.DrawDottedLine(stemP3, stemCorner, 4f);
        Handles.DrawDottedLine(stemP0, stemCorner, 4f);

        // 3. Unique Cross-Chord Lines - Cyan Thin Dotted
        Handles.color = new Color(0.2f, 0.9f, 1f, 0.45f);
        Handles.DrawDottedLine(stemP0, stemP2, 2f);
        Handles.DrawDottedLine(stemP1, stemP3, 2f);

        if (enableCounter)
        {
            // Counter Skeleton Control Arms (Green Dotted)
            Handles.color = new Color(0.2f, 0.9f, 0.3f, 0.65f);
            Handles.DrawDottedLine(counterP0, counterP1, 3f);
            Handles.DrawDottedLine(counterP3, counterP2, 3f);

            // Counter 4-Sided Bounding Box Rectangle (Green Dotted)
            Vector3 cCornerAftLow = new Vector3(0f, counterP0.y, counterP3.z);
            Vector3 cCornerFwdHigh = new Vector3(0f, counterP3.y, counterP0.z);

            Handles.color = new Color(0.2f, 0.95f, 0.4f, 0.7f);
            Handles.DrawDottedLine(counterP0, cCornerAftLow, 4f); // Bottom Horizontal
            Handles.DrawDottedLine(cCornerAftLow, counterP3, 4f); // Aft Vertical
            Handles.DrawDottedLine(counterP3, cCornerFwdHigh, 4f); // Top Horizontal
            Handles.DrawDottedLine(cCornerFwdHigh, counterP0, 4f); // Forward Vertical

            // Counter Cross-Chords (Thin Green Dotted)
            Handles.color = new Color(0.2f, 0.95f, 0.4f, 0.4f);
            Handles.DrawDottedLine(counterP0, counterP2, 2f);
            Handles.DrawDottedLine(counterP1, counterP3, 2f);
        }
#endif

        // Draw Profile Handle Spheres
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(stemP0, 0.14f);
        Gizmos.DrawSphere(stemP1, 0.11f); // Stem Bottom Control Arm
        Gizmos.DrawSphere(stemP2, 0.11f); // Stem Top Control Arm
        Gizmos.DrawSphere(stemP3, 0.18f); // Stem Top (Bow Tip)
        if (upperStemTop.y > stemP3.y + 0.001f) Gizmos.DrawSphere(upperStemTop, 0.16f); // Upper Stem Post Top

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(sternP0, 0.14f);
        Gizmos.DrawSphere(sternP3, 0.16f); // Stern Post Top
        if (enableCounter)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(counterP1, 0.11f); // Counter Lower Control Arm
            Gizmos.DrawSphere(counterP2, 0.11f); // Counter Upper Control Arm
            Gizmos.DrawSphere(counterP3, 0.16f); // Counter Top
        }
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(transomP1, 0.16f);

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle style = GetCachedLabelStyle();

            Handles.Label(stemP3 + new Vector3(0, 0.2f, 0), "Stem Post Top (Bow Tip)", style);
            Handles.Label(sternP3 + new Vector3(0, 0.2f, 0), "Stern Post Top", style);
            if (enableCounter) Handles.Label(counterP3 + new Vector3(0, 0.2f, 0), "Counter Top", style);
            Handles.Label(transomP1 + new Vector3(0, 0.2f, 0), "Transom Rake Top", style);
        }
#endif
    }

    private void DrawCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color col, int segs = 30)
    {
        Gizmos.color = col;
        Vector3 prev = p0;
        for (int i = 1; i <= segs; i++)
        {
            float t = (float)i / segs;
            Vector3 curr = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SheerCurves))]
public class SheerCurvesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SheerCurves curves = (SheerCurves)target;

        if (curves.linkSternHeightToStem)
        {
            EditorGUILayout.HelpBox("Stern Post Height is currently LINKED to Stem Height. Uncheck 'Link Stern Height To Stem' below to adjust Stern Post Height independently.", MessageType.Info);
        }

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update Sheer Curves Profile", GUILayout.Height(30)))
        {
            Undo.RecordObject(curves, "Update Sheer Profile");
            curves.UpdateCachedProfile();
            EditorUtility.SetDirty(curves);
        }
    }

    private void OnSceneGUI()
    {
        SheerCurves curves = (SheerCurves)target;
        curves.UpdateCachedProfile();

        float halfLen = curves.lengthBetweenPerpendiculars / 2f;
        float bowPerpZ = halfLen;
        float sternPerpZ = -halfLen;

        EditorGUI.BeginChangeCheck();

        // 1. Stem Control Point Handles (Yellow / Gold)
        Handles.color = Color.yellow;
        Vector3 newStemP3 = Handles.FreeMoveHandle(curves.stemP3, 0.18f, Vector3.zero, Handles.SphereHandleCap);
        newStemP3.x = 0f;

        Vector3 newStemP1 = Handles.FreeMoveHandle(curves.stemP1, 0.12f, Vector3.zero, Handles.SphereHandleCap);
        newStemP1.x = 0f;

        Vector3 newStemP2 = Handles.FreeMoveHandle(curves.stemP2, 0.12f, Vector3.zero, Handles.SphereHandleCap);
        newStemP2.x = 0f;

        // Render Stem Control Skeleton Dotted Arms & Corridor Boundaries
        Handles.color = new Color(1f, 0.9f, 0.1f, 0.75f);
        Handles.DrawDottedLine(curves.stemP0, curves.stemP1, 3f);
        Handles.DrawDottedLine(curves.stemP3, curves.stemP2, 3f);

        // 1. Render Chord Line (Cyan Dotted)
        Handles.color = new Color(0.2f, 0.9f, 1f, 0.65f);
        Handles.DrawDottedLine(curves.stemP0, curves.stemP3, 3f);

        // 2. Render Outward Rectangular Box Bounds (Orange Dotted)
        Vector3 cornerPt = new Vector3(0f, curves.stemP0.y, curves.stemP3.z);
        Handles.color = new Color(1f, 0.65f, 0.1f, 0.65f);
        Handles.DrawDottedLine(curves.stemP3, cornerPt, 4f);
        Handles.DrawDottedLine(curves.stemP0, cornerPt, 4f);

        // 3. Render Cross-Chord Inward Lines (Cyan Thin Dotted)
        Handles.color = new Color(0.2f, 0.9f, 1f, 0.45f);
        Handles.DrawDottedLine(curves.stemP0, curves.stemP2, 2f);
        Handles.DrawDottedLine(curves.stemP1, curves.stemP3, 2f);

        // 2. Counter Control Point Handles (Green)
        Vector3 newCounterP1 = curves.counterP1;
        Vector3 newCounterP2 = curves.counterP2;
        Vector3 newCounterP3 = curves.counterP3;
        if (curves.enableCounter)
        {
            Handles.color = Color.green;
            newCounterP1 = Handles.FreeMoveHandle(curves.counterP1, 0.12f, Vector3.zero, Handles.SphereHandleCap);
            newCounterP1.x = 0f;

            newCounterP2 = Handles.FreeMoveHandle(curves.counterP2, 0.12f, Vector3.zero, Handles.SphereHandleCap);
            newCounterP2.x = 0f;

            newCounterP3 = Handles.FreeMoveHandle(curves.counterP3, 0.16f, Vector3.zero, Handles.SphereHandleCap);
            newCounterP3.x = 0f;

            Handles.color = new Color(0.2f, 0.9f, 0.3f, 0.75f);
            Handles.DrawDottedLine(curves.counterP0, curves.counterP1, 3f);
            Handles.DrawDottedLine(curves.counterP3, curves.counterP2, 3f);
        }

        // 5. Upper Stem Post Top Handle (Gold / Yellow)
        Handles.color = Color.yellow;
        Vector3 newUpperStemTop = Handles.FreeMoveHandle(curves.upperStemTop, 0.16f, Vector3.zero, Handles.SphereHandleCap);
        newUpperStemTop.x = 0f;

        // 6. Stern Post Top Handle (Cyan)
        Handles.color = Color.cyan;
        Vector3 newSternP3 = Handles.FreeMoveHandle(curves.sternP3, 0.16f, Vector3.zero, Handles.SphereHandleCap);
        newSternP3.x = 0f;

        // 4. Transom Rake Top Handle (Magenta)
        Handles.color = Color.magenta;
        Vector3 newTransomP1 = Handles.FreeMoveHandle(curves.transomP1, 0.16f, Vector3.zero, Handles.SphereHandleCap);
        newTransomP1.x = 0f;

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(curves, "Manipulate Sheer Profile Handles");

            // Enforce Y height constraint on Stem Top: stemP3.y >= Midship Apex Y
            float minApexY = (curves.breadthCurves != null) ? curves.breadthCurves.fwdP3.y : curves.keelY + 0.5f;
            newStemP3.y = Mathf.Max(newStemP3.y, minApexY);

            // Update Stem Height and Stem Rake
            // Apply Stem Boundary Corridor Restrictions during handle dragging
            float sDz = newStemP3.z - curves.stemP0.z;
            float sDy = newStemP3.y - curves.stemP0.y;

            newStemP1.y = Mathf.Clamp(newStemP1.y, curves.stemP0.y, newStemP3.y);
            newStemP2.y = Mathf.Clamp(newStemP2.y, newStemP1.y, newStemP3.y);

            // Inward Min Z limit 1 (Chord Line stemP0 -> stemP3)
            float t1C = (sDy > 0.001f) ? Mathf.Clamp01((newStemP1.y - curves.stemP0.y) / sDy) : 0f;
            float z1C = Mathf.Lerp(curves.stemP0.z, newStemP3.z, t1C);

            float t2C = (sDy > 0.001f) ? Mathf.Clamp01((newStemP2.y - curves.stemP0.y) / sDy) : 0f;
            float z2C = Mathf.Lerp(curves.stemP0.z, newStemP3.z, t2C);

            // Inward Min Z limit 2 (Cross-Chords)
            float dY02 = newStemP2.y - curves.stemP0.y;
            float t1X = (dY02 > 0.001f) ? Mathf.Clamp01((newStemP1.y - curves.stemP0.y) / dY02) : 0f;
            float z1X = Mathf.Lerp(curves.stemP0.z, newStemP2.z, t1X);

            float dY13 = newStemP3.y - newStemP1.y;
            float t2X = (dY13 > 0.001f) ? Mathf.Clamp01((newStemP2.y - newStemP1.y) / dY13) : 0f;
            float z2X = Mathf.Lerp(newStemP1.z, newStemP3.z, t2X);

            newStemP1.z = Mathf.Clamp(newStemP1.z, Mathf.Max(z1C, z1X), newStemP3.z);
            newStemP2.z = Mathf.Clamp(newStemP2.z, Mathf.Max(z2C, z2X), newStemP3.z);

            // Update Stem Height and Stem Rake
            curves.stemHeight = newStemP3.y - curves.keelY;
            curves.stemRake = newStemP3.z - bowPerpZ;

            // Update Stem Control Arm Normalized Ratios
            if (Mathf.Abs(sDz) > 0.001f)
            {
                curves.stemBottomControlZ = Mathf.Clamp01((newStemP1.z - curves.stemP0.z) / sDz);
                curves.stemTopControlZ = Mathf.Clamp01((newStemP2.z - curves.stemP0.z) / sDz);
            }
            if (Mathf.Abs(sDy) > 0.001f)
            {
                curves.stemBottomControlY = Mathf.Clamp01((newStemP1.y - curves.stemP0.y) / sDy);
                curves.stemTopControlY = Mathf.Clamp01((newStemP2.y - curves.stemP0.y) / sDy);
            }

            // Update Counter Rake Curve Control Arms
            if (curves.enableCounter)
            {
                newCounterP3.z = Mathf.Min(newCounterP3.z, curves.counterP0.z); // Counter Top cannot move forward past Stern Post Top
                curves.counterHeight = Mathf.Max(0.1f, newCounterP3.y - curves.counterP0.y);
                curves.counterLength = Mathf.Max(0f, curves.counterP0.z - newCounterP3.z);

                // Enforce 4-Sided Bounding Box Rectangle Constraints on Counter Control Arms
                newCounterP1.y = Mathf.Clamp(newCounterP1.y, curves.counterP0.y, newCounterP3.y);
                newCounterP1.z = Mathf.Clamp(newCounterP1.z, newCounterP3.z, curves.counterP0.z);

                newCounterP2.y = Mathf.Clamp(newCounterP2.y, newCounterP1.y, newCounterP3.y);
                newCounterP2.z = Mathf.Clamp(newCounterP2.z, newCounterP3.z, curves.counterP0.z);

                float cDz = newCounterP3.z - curves.counterP0.z; // negative
                float cDy = newCounterP3.y - curves.counterP0.y; // positive

                if (Mathf.Abs(cDz) > 0.001f)
                {
                    curves.counterBottomControlZ = Mathf.Clamp01((newCounterP1.z - curves.counterP0.z) / cDz);
                    curves.counterTopControlZ = Mathf.Clamp01((newCounterP2.z - curves.counterP0.z) / cDz);
                }
                if (Mathf.Abs(cDy) > 0.001f)
                {
                    curves.counterBottomControlY = Mathf.Clamp01((newCounterP1.y - curves.counterP0.y) / cDy);
                    curves.counterTopControlY = Mathf.Clamp01((newCounterP2.y - curves.counterP0.y) / cDy);
                }
            }

            // Update Upper Stem Post Extension Ratio
            float upperSpan = curves.transomP1.y - curves.stemP3.y;
            if (upperSpan > 0.001f)
            {
                curves.upperStemRatio = Mathf.Clamp01((newUpperStemTop.y - curves.stemP3.y) / upperSpan);
            }

            // Update Stern Post (Stern Post Top cannot move further forward than Stern Post Bottom sternP0.z & cannot go lower than Midship Apex Y)
            float minSternApexY = (curves.breadthCurves != null) ? curves.breadthCurves.fwdP3.y : curves.keelY + 0.5f;

            if (curves.linkSternHeightToStem)
            {
                newSternP3.y = curves.keelY + curves.stemHeight;
            }
            else
            {
                newSternP3.y = Mathf.Max(newSternP3.y, minSternApexY);
                curves.sternPostHeight = Mathf.Max(0.1f, newSternP3.y - curves.keelY);
            }

            newSternP3.z = Mathf.Min(newSternP3.z, curves.sternP0.z);
            curves.sternPostRake = Mathf.Max(0f, sternPerpZ - newSternP3.z);

            // Update Transom Rake
            Vector3 t0 = curves.transomP0;
            newTransomP1.z = Mathf.Min(newTransomP1.z, t0.z); // Transom Top cannot move forward past Counter Top
            curves.transomRakeHeight = Mathf.Max(0.1f, newTransomP1.y - t0.y);
            curves.transomRakeAft = Mathf.Max(0f, t0.z - newTransomP1.z);

            curves.UpdateCachedProfile();
            EditorUtility.SetDirty(curves);

            // Bi-directionally sync updated Stem Top position into BreadthCurves
            if (curves.breadthCurves != null)
            {
                Undo.RecordObject(curves.breadthCurves, "Sync Stem Top to BreadthCurves");
                curves.breadthCurves.UpdateCachedCurves();
                EditorUtility.SetDirty(curves.breadthCurves);
            }

            TopLineCurves topLineCurves = Object.FindFirstObjectByType<TopLineCurves>();
            if (topLineCurves != null)
            {
                topLineCurves.UpdateCachedCurves();
                EditorUtility.SetDirty(topLineCurves);
            }
        }
    }
}
#endif
