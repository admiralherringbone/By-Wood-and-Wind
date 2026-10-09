using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// BowCtrlArmGuide
/// Dedicated visualizer for rendering 3D forward and aft control arm guide curves:
/// 1. Bow Mould Guide (P1): Forward entry tangent points across u (TopLine down to Floor).
/// 2. Forward Midship Guide (P2): Forward midship tangent points across u (TopLine down to Keel).
/// 3. Aft Mould Guide (A1): Stern quarter control points scaled across all bands (TopLine down to Keel).
/// 4. Stern Transom Guide (A2): Transom corner points across u (TopLine down to Keel).
/// </summary>
[ExecuteInEditMode]
public class BowCtrlArmGuide : MonoBehaviour
{
    [Header("Primary Curve References")]
    public SheerCurves sheerCurves;
    public TopLineCurves topLineCurves;
    public BreadthCurves breadthCurves;
    public BilgeCurves bilgeCurves;
    public FloorCurves floorCurves;
    public BodyCurves bodyCurves;
    public BackboneCurves backboneCurves;
    public FashionCurves fashionCurves;

    [Header("Forward Control Arm Guides (Dashed Lines)")]
    [Tooltip("Show the dashed 3D Bow Mould P1 section guide line (TopLine down to Floor).")]
    public bool showBowMouldP1Guide = true;

    [Tooltip("Show the dashed 3D Forward Midship P2 section guide line (TopLine down to Keel).")]
    public bool showFwdP2Guide = true;

    public Color bowMouldGuideColor = new Color(0.1f, 0.95f, 0.85f, 0.95f); // Vibrant Cyan
    public Color fwdP2GuideColor = new Color(0.95f, 0.85f, 0.1f, 0.95f);    // Vibrant Gold

    [Header("Aft Control Arm Guides (Dashed Lines)")]
    [Tooltip("Show the dashed 3D Aft Mould A1 section guide line (TopLine down to Keel).")]
    public bool showAftMouldA1Guide = true;

    [Tooltip("Show the dashed 3D Stern Transom Boundary A2 guide line (TopLine down to Keel).")]
    public bool showSternTransomGuide = true;

    public Color aftMouldGuideColor = new Color(0.75f, 0.25f, 0.95f, 0.95f);   // Vibrant Purple
    public Color sternTransomGuideColor = new Color(0.95f, 0.25f, 0.55f, 0.95f); // Vibrant Rose / Pink

    [Header("Visualization Settings")]
    [Range(10, 100)] public int guideSteps = 40;
    public bool showLabels = true;

    private Vector3 MirrorX(Vector3 pt) => new Vector3(-pt.x, pt.y, pt.z);

    private void OnEnable() => AutoFindReferences();
    private void Reset() => AutoFindReferences();

    public void EnsureInitialized()
    {
        AutoFindReferences();
    }

    public void AutoFindReferences()
    {
        if (sheerCurves == null) sheerCurves = FindFirstObjectByType<SheerCurves>();
        if (topLineCurves == null) topLineCurves = FindFirstObjectByType<TopLineCurves>();
        if (breadthCurves == null) breadthCurves = FindFirstObjectByType<BreadthCurves>();
        if (bilgeCurves == null) bilgeCurves = FindFirstObjectByType<BilgeCurves>();
        if (floorCurves == null) floorCurves = FindFirstObjectByType<FloorCurves>();
        if (bodyCurves == null) bodyCurves = FindFirstObjectByType<BodyCurves>();
        if (backboneCurves == null) backboneCurves = FindFirstObjectByType<BackboneCurves>();
        if (fashionCurves == null) fashionCurves = FindFirstObjectByType<FashionCurves>();
    }

    public Vector3 EvaluateBowMouldP1AtU(float u)
    {
        if (bilgeCurves != null) return bilgeCurves.EvaluateBowMouldP1AtU(u);

        float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
        float bowZ = (sheerCurves != null) ? sheerCurves.stemP3.z : 15f;
        float fwdSpan = bowZ - midZ;

        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : 0.25f;
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        Vector3 floorP1 = (floorCurves != null) ? floorCurves.fwdP1 : new Vector3(hw + 1f, keelY + 0.5f, midZ + fwdSpan * 0.5f);
        Vector3 breadthP1 = (breadthCurves != null) ? breadthCurves.fwdP1 : new Vector3(hw + 3f, keelY + 2.5f, midZ + fwdSpan * 0.75f);
        Vector3 topLineP1 = (topLineCurves != null) ? topLineCurves.fwdP1 : new Vector3(hw + 3.5f, keelY + 3.5f, midZ + fwdSpan * 0.85f);

        SectionNodes midNodes = (bodyCurves != null) ? bodyCurves.GetSectionNodesAtZ(midZ, 1f) : new SectionNodes();
        Vector3 secPt = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(u, midNodes) : new Vector3(hw + u * 3f, keelY + u * 2.5f, midZ);

        float sX, sY;
        if (u >= 0.75f)
        {
            float t = (u - 0.75f) / 0.25f;
            sX = Mathf.Lerp(1.0f, 1.1f, t);
            sY = Mathf.Lerp(1.0f, 1.1f, t);
        }
        else
        {
            float t = u / 0.75f;
            sX = Mathf.Lerp(0.9f, 1.0f, t);
            sY = Mathf.Lerp(0.9f, 1.0f, t);
        }

        float xVal = hw + (Mathf.Abs(secPt.x) - hw) * sX;
        float yVal = keelY + (secPt.y - keelY) * sY;
        float zVal = (bilgeCurves != null) ? bilgeCurves.EvaluateCenterlineStemProfileZAtY(yVal) : Mathf.Lerp(midZ, bowZ, u);

        return new Vector3(xVal, yVal, zVal);
    }

    public Vector3 EvaluateFwdP2AtU(float u)
    {
        if (bilgeCurves != null) return bilgeCurves.EvaluateFwdP2AtU(u);

        float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
        float bowZ = (sheerCurves != null) ? sheerCurves.stemP3.z : 15f;
        float fwdSpan = bowZ - midZ;

        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : 0.25f;
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        SectionNodes midNodes = (bodyCurves != null) ? bodyCurves.GetSectionNodesAtZ(midZ, 1f) : new SectionNodes();
        Vector3 secPt = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(u, midNodes) : new Vector3(hw + u * 3f, keelY + u * 2.5f, midZ);

        float yVal = secPt.y;
        float zVal = (bilgeCurves != null) ? bilgeCurves.EvaluateCenterlineFwdP2ProfileZAtY(yVal) : (midZ + fwdSpan * 0.4f);

        return new Vector3(secPt.x, yVal, zVal);
    }

    public Vector3 EvaluateAftMouldA1AtU(float u)
    {
        if (bilgeCurves != null) return bilgeCurves.EvaluateAftMouldA1AtU(u);

        float midZ = (breadthCurves != null) ? breadthCurves.fwdP3.z : 0f;
        float sternZ = (sheerCurves != null) ? sheerCurves.GetSternProfileZAtY(sheerCurves.keelY) : -15f;

        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : 0.25f;
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        SectionNodes midNodes = (bodyCurves != null) ? bodyCurves.GetSectionNodesAtZ(midZ, 1f) : new SectionNodes();
        Vector3 secPt = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(u, midNodes) : new Vector3(hw + u * 3f, keelY + u * 2.5f, midZ);

        float yVal = secPt.y;
        float zVal = (bilgeCurves != null) ? bilgeCurves.EvaluateCenterlineSternProfileZAtY(yVal) : Mathf.Lerp(midZ, sternZ, 0.5f);

        return new Vector3(secPt.x, yVal, zVal);
    }

    public Vector3 EvaluateAftTransomA2AtU(float u)
    {
        if (bilgeCurves != null) return bilgeCurves.EvaluateAftTransomA2AtU(u);

        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : 0.25f;
        float keelY = (sheerCurves != null) ? sheerCurves.keelY : 0f;

        SectionNodes midNodes = (bodyCurves != null) ? bodyCurves.GetSectionNodesAtZ(0f, 1f) : new SectionNodes();
        Vector3 secPt = (bodyCurves != null) ? bodyCurves.EvaluateBodySectionAtU(u, midNodes) : new Vector3(hw + u * 3f, keelY + u * 2.5f, 0f);

        if (fashionCurves != null)
        {
            Vector3 fashionPt = fashionCurves.GetFashionPointAtZ(fashionCurves.stbdP1.z, 1f);
            float xVal = Mathf.Lerp(hw, fashionPt.x, u);
            float yVal = Mathf.Lerp(keelY, fashionPt.y, u);
            return new Vector3(xVal, yVal, fashionPt.z);
        }

        return new Vector3(secPt.x, secPt.y, -15f);
    }

    private void OnDrawGizmos()
    {
        AutoFindReferences();

#if UNITY_EDITOR
        if (showBowMouldP1Guide) DrawBowMouldP1GuideDashed(bowMouldGuideColor);
        if (showFwdP2Guide) DrawFwdP2GuideDashed(fwdP2GuideColor);
        if (showAftMouldA1Guide) DrawAftMouldA1GuideDashed(aftMouldGuideColor);
        if (showSternTransomGuide) DrawSternTransomGuideDashed(sternTransomGuideColor);
#endif
    }

#if UNITY_EDITOR
    private void DrawBowMouldP1GuideDashed(Color color)
    {
        Handles.color = color;
        Vector3 prev = EvaluateBowMouldP1AtU(0.25f);
        for (int i = 1; i <= guideSteps; i++)
        {
            float u = Mathf.Lerp(0.25f, 1.00f, (float)i / guideSteps);
            Vector3 curr = EvaluateBowMouldP1AtU(u);
            Handles.DrawDottedLine(prev, curr, 4f);
            Handles.DrawDottedLine(MirrorX(prev), MirrorX(curr), 4f);
            prev = curr;
        }

        float[] sampleUs = new float[] { 0.25f, 0.50f, 0.75f, 1.00f };
        string[] labels = new string[] { "Floor P1 (u=0.25)", "Bilge P1 (u=0.50)", "Breadth P1 (u=0.75)", "TopLine P1 (u=1.00)" };

        for (int k = 0; k < sampleUs.Length; k++)
        {
            Vector3 pt = EvaluateBowMouldP1AtU(sampleUs[k]);
            Gizmos.color = color;
            Gizmos.DrawSphere(pt, 0.07f);
            Gizmos.DrawSphere(MirrorX(pt), 0.07f);

            if (showLabels)
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = color;
                style.fontSize = 10;
                style.fontStyle = FontStyle.Bold;
                Handles.Label(pt + Vector3.up * 0.12f, labels[k], style);
            }
        }
    }

    private void DrawFwdP2GuideDashed(Color color)
    {
        Handles.color = color;
        Vector3 prev = EvaluateFwdP2AtU(0.00f);
        for (int i = 1; i <= guideSteps; i++)
        {
            float u = (float)i / guideSteps;
            Vector3 curr = EvaluateFwdP2AtU(u);
            Handles.DrawDottedLine(prev, curr, 4f);
            Handles.DrawDottedLine(MirrorX(prev), MirrorX(curr), 4f);
            prev = curr;
        }

        float[] sampleUs = new float[] { 0.00f, 0.25f, 0.50f, 0.75f, 1.00f };
        string[] labels = new string[] { "Keel P2 (u=0.00)", "Floor P2 (u=0.25)", "Bilge P2 (u=0.50)", "Breadth P2 (u=0.75)", "TopLine P2 (u=1.00)" };

        for (int k = 0; k < sampleUs.Length; k++)
        {
            Vector3 pt = EvaluateFwdP2AtU(sampleUs[k]);
            Gizmos.color = color;
            Gizmos.DrawSphere(pt, 0.07f);
            Gizmos.DrawSphere(MirrorX(pt), 0.07f);

            if (showLabels)
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = color;
                style.fontSize = 10;
                style.fontStyle = FontStyle.Bold;
                Handles.Label(pt + Vector3.up * 0.12f, labels[k], style);
            }
        }
    }

    private void DrawAftMouldA1GuideDashed(Color color)
    {
        Handles.color = color;
        Vector3 prev = EvaluateAftMouldA1AtU(0.00f);
        for (int i = 1; i <= guideSteps; i++)
        {
            float u = (float)i / guideSteps;
            Vector3 curr = EvaluateAftMouldA1AtU(u);
            Handles.DrawDottedLine(prev, curr, 4f);
            Handles.DrawDottedLine(MirrorX(prev), MirrorX(curr), 4f);
            prev = curr;
        }

        float[] sampleUs = new float[] { 0.00f, 0.25f, 0.50f, 0.75f, 1.00f };
        string[] labels = new string[] { "Keel A1 (u=0.00)", "Floor A1 (u=0.25)", "Bilge A1 (u=0.50)", "Breadth A1 (u=0.75)", "TopLine A1 (u=1.00)" };

        for (int k = 0; k < sampleUs.Length; k++)
        {
            Vector3 pt = EvaluateAftMouldA1AtU(sampleUs[k]);
            Gizmos.color = color;
            Gizmos.DrawSphere(pt, 0.07f);
            Gizmos.DrawSphere(MirrorX(pt), 0.07f);

            if (showLabels)
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = color;
                style.fontSize = 10;
                style.fontStyle = FontStyle.Bold;
                Handles.Label(pt + Vector3.up * 0.12f, labels[k], style);
            }
        }
    }

    private void DrawSternTransomGuideDashed(Color color)
    {
        Handles.color = color;
        Vector3 prev = EvaluateAftTransomA2AtU(0.00f);
        for (int i = 1; i <= guideSteps; i++)
        {
            float u = (float)i / guideSteps;
            Vector3 curr = EvaluateAftTransomA2AtU(u);
            Handles.DrawDottedLine(prev, curr, 4f);
            Handles.DrawDottedLine(MirrorX(prev), MirrorX(curr), 4f);
            prev = curr;
        }

        float[] sampleUs = new float[] { 0.00f, 0.25f, 0.50f, 0.75f, 1.00f };
        string[] labels = new string[] { "Keel A2 (u=0.00)", "Floor A2 (u=0.25)", "Bilge A2 (u=0.50)", "Breadth A2 (u=0.75)", "TopLine A2 (u=1.00)" };

        for (int k = 0; k < sampleUs.Length; k++)
        {
            Vector3 pt = EvaluateAftTransomA2AtU(sampleUs[k]);
            Gizmos.color = color;
            Gizmos.DrawSphere(pt, 0.07f);
            Gizmos.DrawSphere(MirrorX(pt), 0.07f);

            if (showLabels)
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = color;
                style.fontSize = 10;
                style.fontStyle = FontStyle.Bold;
                Handles.Label(pt + Vector3.up * 0.12f, labels[k], style);
            }
        }
    }
#endif
}
