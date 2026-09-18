using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ConnectionGuideModeTest
{
    [Tooltip("Endpoints are strictly locked to the Rising Line endpoints.")]
    RisingLineAnchored,

    [Tooltip("Endpoints transition dynamically between Floor Line ends and Primary Breadth ends.")]
    BlendedEndpoints
}

[ExecuteInEditMode]
public class ConnectionGuideShapeTest : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Reference to the SheerPlanTest to get Rising Line and HoB endpoints.")]
    public SheerPlanTest sheerPlan;

    [Tooltip("Reference to the BodyPlanTest to get Master Connection Point (cachedLR) and Floor/Primary widths.")]
    public BodyPlanTest bodyPlan;

    [Tooltip("Reference to the BreadthPlanTest to get Floor Line and Max Beam breadth curves.")]
    public BreadthPlanTest breadthPlan;

    [Header("Mode Selection")]
    [Tooltip("Selection mode for Connection Guide shape endpoint anchoring and curve blending.")]
    public ConnectionGuideModeTest guideMode = ConnectionGuideModeTest.RisingLineAnchored;

    [Header("Gizmo Settings")]
    [Tooltip("Show the Connection Point Guide shape curve in Scene view gizmos.")]
    public bool showConnectionGuide = true;

    [Tooltip("Color of the Connection Point guide curve.")]
    public Color guideColor = new Color(0.1f, 1f, 0.4f, 0.9f);

    [Tooltip("Show node labels in the Scene view.")]
    public bool showLabels = true;

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        FindReferences();
        if (breadthPlan != null)
        {
            breadthPlan.UpdateCachedCurves();
        }
    }

    public void FindReferences()
    {
        if (sheerPlan == null)
        {
            sheerPlan = GetComponentInParent<SheerPlanTest>();
            if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlanTest>();
        }

        if (bodyPlan == null)
        {
            bodyPlan = GetComponentInParent<BodyPlanTest>();
            if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlanTest>();
        }

        if (breadthPlan == null)
        {
            breadthPlan = GetComponentInParent<BreadthPlanTest>();
            if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlanTest>();
        }
    }

    /// <summary>
    /// Calculates the normalized blend weight of BodyPlanTest's cachedLR.x 
    /// between Floor Control arm X (0 = cachedL1.x) and Max Beam X (1 = maxBeam).
    /// </summary>
    public float GetBlendWeight()
    {
        if (bodyPlan == null || breadthPlan == null) return 0.5f;

        float masterL1_X = bodyPlan.cachedL1.x;
        float masterLR_X = bodyPlan.cachedLR.x;
        float masterPrimary_X = breadthPlan.maxBeam;

        float masterDenom = masterPrimary_X - masterL1_X;
        if (masterDenom <= 0.0001f) return 0.5f;

        return Mathf.Clamp01((masterLR_X - masterL1_X) / masterDenom);
    }

    /// <summary>
    /// Gets the forward Z-endpoint of the Connection Guide curve based on guideMode.
    /// </summary>
    public float GetBowZ()
    {
        if (sheerPlan == null) return 10f;

        if (guideMode == ConnectionGuideModeTest.RisingLineAnchored)
        {
            return sheerPlan.cachedBowRisingEnd.z;
        }
        else
        {
            float floorBowZ = sheerPlan.cachedBowRisingEnd.z;
            float primaryBowZ = sheerPlan.cachedHobBowIntersect.z;
            return Mathf.Lerp(floorBowZ, primaryBowZ, GetBlendWeight());
        }
    }

    /// <summary>
    /// Gets the aft Z-endpoint of the Connection Guide curve based on guideMode.
    /// </summary>
    public float GetSternZ()
    {
        if (sheerPlan == null) return -10f;

        if (guideMode == ConnectionGuideModeTest.RisingLineAnchored)
        {
            return sheerPlan.cachedSternRisingEnd.z;
        }
        else
        {
            float floorSternZ = sheerPlan.cachedSternRisingEnd.z;
            float primarySternZ = sheerPlan.cachedHobSternIntersect.z;
            return Mathf.Lerp(floorSternZ, primarySternZ, GetBlendWeight());
        }
    }

    /// <summary>
    /// Evaluates the half-width X of the Connection Guide curve at station z based on guideMode.
    /// </summary>
    public float GetConnectionGuideXAtZ(float targetZ)
    {
        if (breadthPlan == null || sheerPlan == null || bodyPlan == null) return 0.25f;

        float blendWeight = GetBlendWeight();
        float bowZ = GetBowZ();
        float sternZ = GetSternZ();
        float hw = bodyPlan.keelWidth / 2f;

        if (targetZ >= bowZ && bowZ > 0.001f) return hw;
        if (targetZ <= sternZ && sternZ < -0.001f) return hw;

        if (targetZ >= 0f)
        {
            float floorBowZ = sheerPlan.cachedBowRisingEnd.z;
            float primaryBowZ = sheerPlan.cachedHobBowIntersect.z;

            float u = Mathf.Clamp01(targetZ / bowZ);
            float floorX = breadthPlan.GetFloorLineXAtZ(u * floorBowZ);
            float primaryX_raw = breadthPlan.GetTheoreticalBreadthXAtZ(u * primaryBowZ);
            float primaryBowEnd = breadthPlan.GetTheoreticalBreadthXAtZ(primaryBowZ);

            float primaryX = primaryX_raw;
            float primaryDenom = breadthPlan.maxBeam - primaryBowEnd;
            if (primaryDenom > 0.0001f)
            {
                float normPrimary = Mathf.Clamp01((primaryX_raw - primaryBowEnd) / primaryDenom);
                primaryX = hw + (breadthPlan.maxBeam - hw) * normPrimary;
            }

            return Mathf.Lerp(floorX, primaryX, blendWeight);
        }
        else
        {
            float floorSternZ = sheerPlan.cachedSternRisingEnd.z;
            float primarySternZ = sheerPlan.cachedHobSternIntersect.z;

            float u = Mathf.Clamp01(targetZ / sternZ);
            float floorX = breadthPlan.GetFloorLineXAtZ(u * floorSternZ);
            float primaryX_raw = breadthPlan.GetTheoreticalBreadthXAtZ(u * primarySternZ);
            float primarySternEnd = breadthPlan.GetTheoreticalBreadthXAtZ(primarySternZ);

            float primaryX = primaryX_raw;
            float primaryDenom = breadthPlan.maxBeam - primarySternEnd;
            if (primaryDenom > 0.0001f)
            {
                float normPrimary = Mathf.Clamp01((primaryX_raw - primarySternEnd) / primaryDenom);
                primaryX = hw + (breadthPlan.maxBeam - hw) * normPrimary;
            }

            return Mathf.Lerp(floorX, primaryX, blendWeight);
        }
    }

    private void OnDrawGizmos()
    {
        if (!showConnectionGuide) return;
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null)
        {
            FindReferences();
            if (sheerPlan == null || bodyPlan == null || breadthPlan == null) return;
        }

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        breadthPlan.UpdateCachedCurves();

        float guideY = bodyPlan.cachedLR.y;
        float bowZ = GetBowZ();
        float sternZ = GetSternZ();
        float midZ = 0f;

        Gizmos.color = guideColor;
        const int steps = 150;

        for (int sideSign = -1; sideSign <= 1; sideSign += 2)
        {
            float side = sideSign;
            Vector3 prevPt = Vector3.zero;
            bool first = true;

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float z = Mathf.Lerp(sternZ, bowZ, t);
                float targetX = GetConnectionGuideXAtZ(z);

                Vector3 pt = new Vector3(side * targetX, guideY, z);
                if (!first)
                {
                    Gizmos.DrawLine(prevPt, pt);
                }
                prevPt = pt;
                first = false;
            }
        }

        // Draw node spheres at endpoints and midship
        Vector3 bowPt = new Vector3(GetConnectionGuideXAtZ(bowZ), guideY, bowZ);
        Vector3 midPt = new Vector3(bodyPlan.cachedLR.x, guideY, midZ);
        Vector3 sternPt = new Vector3(GetConnectionGuideXAtZ(sternZ), guideY, sternZ);

        Gizmos.DrawSphere(bowPt, 0.10f);
        Gizmos.DrawSphere(midPt, 0.14f);
        Gizmos.DrawSphere(sternPt, 0.10f);

        Vector3 portBowPt = MirrorX(bowPt);
        Vector3 portMidPt = MirrorX(midPt);
        Vector3 portSternPt = MirrorX(sternPt);

        Gizmos.DrawSphere(portBowPt, 0.10f);
        Gizmos.DrawSphere(portMidPt, 0.14f);
        Gizmos.DrawSphere(portSternPt, 0.10f);

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = guideColor;
            labelStyle.alignment = TextAnchor.LowerCenter;
            labelStyle.contentOffset = new Vector2(0, -10);

            Handles.Label(midPt, $"Connection Guide ({guideMode})", labelStyle);
        }
#endif
    }

    private Vector3 MirrorX(Vector3 v)
    {
        return new Vector3(-v.x, v.y, v.z);
    }
}
