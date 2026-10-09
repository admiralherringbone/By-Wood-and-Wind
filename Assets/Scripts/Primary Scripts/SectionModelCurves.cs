using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// SectionModelCurves generates pure transverse frame section cuts (on the X/Y plane) at equal 
/// intervals down the length of the hull. Each transverse cut samples 3D points directly from 
/// the longitudinal Ribband curves, Primary Hull curves (Floor, Bilge, Breadth, TopLine), 
/// Sheer/Keel profiles, and Fashion curves.
/// 
/// As station Z slides stem-to-stern, the transverse frame points dynamically adjust to the exact 
/// positions dictated by the longitudinal ribbands, visually proving the smoothness and continuity 
/// of the hull lofting.
/// </summary>
[ExecuteInEditMode]
public class SectionModelCurves : MonoBehaviour
{
    [System.Serializable]
    public struct SectionPointNode
    {
        public string name;
        public float uParam; // 0.0 = Keel, 0.25 = Floor, 0.50 = Bilge, 0.75 = Breadth, 1.0 = TopLine
        public Vector3 point;
    }

    [Header("Primary Curve References")]
    [Tooltip("Reference to SheerCurves for profile lines (Stem, Stern, Keel).")]
    public SheerCurves sheerCurves;

    [Tooltip("Reference to TopLineCurves for Top Line 3D points.")]
    public TopLineCurves topLineCurves;

    [Tooltip("Reference to BreadthCurves for Max Breadth 3D points.")]
    public BreadthCurves breadthCurves;

    [Tooltip("Reference to BilgeCurves for Diagonal Bilge 3D points.")]
    public BilgeCurves bilgeCurves;

    [Tooltip("Reference to FloorCurves for Floor Line 3D points.")]
    public FloorCurves floorCurves;

    [Tooltip("Reference to BodyCurves for hull section node alignment.")]
    public BodyCurves bodyCurves;

    [Tooltip("Reference to RibbandCurves for intermediate longitudinal battens.")]
    public RibbandCurves ribbandCurves;

    [Tooltip("Reference to FashionCurves for 3D transom stern boundary curve.")]
    public FashionCurves fashionCurves;

    [Tooltip("Reference to BackboneCurves for stem and stern keel width offset.")]
    public BackboneCurves backboneCurves;

    [Header("Transverse Cut Settings")]
    [Tooltip("Number of transverse frame cuts generated at equal intervals down the hull length.")]
    [Range(3, 50)] public int sectionCount = 15;

    [Tooltip("Include primary boundary nodes (Floor, Bilge, Breadth, TopLine) alongside Ribbands.")]
    public bool includePrimaryBoundaryNodes = true;

    [Header("Gizmo Visualization Settings")]
    public bool showTransverseSections = true;
    public bool showSectionNodes = true;
    public bool showPolygonSkeleton = false;
    public bool showLabels = true;

    [Range(0.02f, 0.25f)] public float nodeRadius = 0.06f;
    public Color sectionColor = new Color(0.1f, 0.85f, 0.95f, 0.9f);
    public Color starboardColor = new Color(0.2f, 0.95f, 0.4f, 0.95f);
    public Color portColor = new Color(0.95f, 0.25f, 0.4f, 0.95f);

    [Header("Master Frame Settings")]
    [Tooltip("Construct section frames purely from the 5 Master Key Curves (Keel, Floor, Bilge, Breadth, TopLine) to guarantee lump-free hull cuts.")]
    public bool useKeyCurvesOnlyForSections = true;

    [Header("Initialization State")]
    public bool isInitialized = false;

    [HideInInspector] public int changeVersion = 0;

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Reset()
    {
        InitializeSectionModel();
    }

    public void EnsureInitialized()
    {
        if (!isInitialized)
        {
            InitializeSectionModel();
        }
    }

    public void FindReferences()
    {
        if (sheerCurves == null) sheerCurves = GetComponentInParent<SheerCurves>();
        if (sheerCurves == null) sheerCurves = FindFirstObjectByType<SheerCurves>();

        if (topLineCurves == null) topLineCurves = GetComponentInParent<TopLineCurves>();
        if (topLineCurves == null) topLineCurves = FindFirstObjectByType<TopLineCurves>();

        if (breadthCurves == null) breadthCurves = GetComponentInParent<BreadthCurves>();
        if (breadthCurves == null) breadthCurves = FindFirstObjectByType<BreadthCurves>();

        if (bilgeCurves == null) bilgeCurves = GetComponentInParent<BilgeCurves>();
        if (bilgeCurves == null) bilgeCurves = FindFirstObjectByType<BilgeCurves>();

        if (floorCurves == null) floorCurves = GetComponentInParent<FloorCurves>();
        if (floorCurves == null) floorCurves = FindFirstObjectByType<FloorCurves>();

        if (bodyCurves == null) bodyCurves = GetComponentInParent<BodyCurves>();
        if (bodyCurves == null) bodyCurves = FindFirstObjectByType<BodyCurves>();

        if (ribbandCurves == null) ribbandCurves = GetComponentInParent<RibbandCurves>();
        if (ribbandCurves == null) ribbandCurves = FindFirstObjectByType<RibbandCurves>();

        if (fashionCurves == null) fashionCurves = GetComponentInParent<FashionCurves>();
        if (fashionCurves == null) fashionCurves = FindFirstObjectByType<FashionCurves>();

        if (backboneCurves == null) backboneCurves = GetComponentInParent<BackboneCurves>();
        if (backboneCurves == null) backboneCurves = FindFirstObjectByType<BackboneCurves>();
    }

    [ContextMenu("Reset Section Model Curves")]
    public void InitializeSectionModel()
    {
        FindReferences();
        UpdateCachedCurves();
        isInitialized = true;
    }

    private void OnValidate()
    {
        UpdateCachedCurves();
    }

    public virtual void UpdateCachedCurves()
    {
        FindReferences();

        if (sheerCurves != null) sheerCurves.UpdateCachedProfile();
        if (topLineCurves != null) topLineCurves.UpdateCachedCurves();
        if (breadthCurves != null) breadthCurves.UpdateCachedCurves();
        if (bilgeCurves != null) bilgeCurves.UpdateCachedCurves();
        if (floorCurves != null) floorCurves.UpdateCachedCurves();
        if (fashionCurves != null) fashionCurves.UpdateCachedCurves();
        if (ribbandCurves != null) ribbandCurves.UpdateCachedCurves();

        changeVersion++;
    }

    /// <summary>
    /// Checks whether station Z is physically within the active longitudinal span [aftTip, fwdTip] of a curve.
    /// </summary>
    public bool IsStationWithinCurveSpan(float z, Vector3 fwdTip, Vector3 aftTip, float tolerance = 0.01f)
    {
        float maxZ = Mathf.Max(fwdTip.z, aftTip.z) + tolerance;
        float minZ = Mathf.Min(fwdTip.z, aftTip.z) - tolerance;
        return z >= minZ && z <= maxZ;
    }

    /// <summary>
    /// Gets the ordered transverse section nodes (Keel up to TopLine/Fashion) at station Z 
    /// for the specified side (+1 Starboard, -1 Port).
    /// </summary>
    public List<SectionPointNode> GetTransverseSectionNodesAtZ(float z, float sideSign)
    {
        FindReferences();

        List<SectionPointNode> nodes = new List<SectionPointNode>();
        float sSign = Mathf.Approximately(sideSign, 0f) ? 1f : Mathf.Sign(sideSign);
        float hw = (backboneCurves != null) ? (backboneCurves.keelWidth / 2f) : ((bodyCurves != null) ? (bodyCurves.keelWidth / 2f) : 0.25f);

        // 1. Keel or Transom Base Node (u = 0.0 or uParam corresponding to Fashion boundary)
        bool isTransomRegion = (fashionCurves != null && z <= fashionCurves.stbdP1.z + 0.001f);
        float minTransomY = -999f;
        if (isTransomRegion)
        {
            Vector3 fashionPt = fashionCurves.GetFashionPointAtZ(z, sSign);
            minTransomY = fashionPt.y;

            float uTransom = 0.25f;
            float ySpan = fashionCurves.stbdP4.y - fashionCurves.stbdP1.y;
            if (ySpan > 0.001f)
            {
                uTransom = Mathf.Lerp(0.25f, 1.00f, Mathf.Clamp01((fashionPt.y - fashionCurves.stbdP1.y) / ySpan));
            }

            nodes.Add(new SectionPointNode
            {
                name = "Fashion Transom Base",
                uParam = uTransom,
                point = fashionPt
            });
        }
        else
        {
            float keelY = 0f;
            if (sheerCurves != null)
            {
                if (z >= sheerCurves.stemP0.z)
                {
                    keelY = sheerCurves.GetStemProfileYAtZ(z);
                }
                else if (z <= sheerCurves.sternP0.z)
                {
                    keelY = sheerCurves.GetSternProfileYAtZ(z);
                }
                else
                {
                    keelY = sheerCurves.keelY;
                }
            }

            nodes.Add(new SectionPointNode
            {
                name = "Keel",
                uParam = 0.0f,
                point = new Vector3(sSign * hw, keelY, z)
            });
        }

        // 2. Sample Primary Boundary Curves if requested and active at station Z
        if (includePrimaryBoundaryNodes)
        {
            if (floorCurves != null && IsStationWithinCurveSpan(z, floorCurves.fwdP0, floorCurves.aftP3))
            {
                Vector3 fPt = floorCurves.GetFloorPointAtZ(z);
                nodes.Add(new SectionPointNode
                {
                    name = "Floor Curve",
                    uParam = 0.25f,
                    point = new Vector3(sSign * Mathf.Abs(fPt.x), fPt.y, z)
                });
            }

            if (bilgeCurves != null && IsStationWithinCurveSpan(z, bilgeCurves.fwdP0, bilgeCurves.aftP2))
            {
                Vector3 bPt = bilgeCurves.GetBilgePointAtZ(z);
                nodes.Add(new SectionPointNode
                {
                    name = "Bilge Curve",
                    uParam = 0.50f,
                    point = new Vector3(sSign * Mathf.Abs(bPt.x), bPt.y, z)
                });
            }

            if (breadthCurves != null)
            {
                Vector3 bAft = breadthCurves.cachedIsAftCubic ? breadthCurves.aftP3 : breadthCurves.aftP2;
                if (IsStationWithinCurveSpan(z, breadthCurves.fwdP0, bAft))
                {
                    Vector3 brPt = breadthCurves.GetBreadthPointAtZ(z);
                    nodes.Add(new SectionPointNode
                    {
                        name = "Breadth Curve",
                        uParam = 0.75f,
                        point = new Vector3(sSign * Mathf.Abs(brPt.x), brPt.y, z)
                    });
                }
            }

            if (topLineCurves != null)
            {
                Vector3 tlAft = topLineCurves.cachedIsAftCubic ? topLineCurves.aftP3 : topLineCurves.aftP2;
                if (IsStationWithinCurveSpan(z, topLineCurves.fwdP0, tlAft))
                {
                    Vector3 tlPt = topLineCurves.GetTopLinePointAtZ(z);
                    nodes.Add(new SectionPointNode
                    {
                        name = "TopLine Curve",
                        uParam = 1.00f,
                        point = new Vector3(sSign * Mathf.Abs(tlPt.x), tlPt.y, z)
                    });
                }
            }
        }

        // 3. Sample Longitudinal Ribbands from RibbandCurves if enabled (discard if station Z is outside ribband span)
        if (!useKeyCurvesOnlyForSections && ribbandCurves != null && ribbandCurves.generatedRibbands != null)
        {
            for (int i = 0; i < ribbandCurves.generatedRibbands.Count; i++)
            {
                RibbandCurves.SingleRibband rib = ribbandCurves.generatedRibbands[i];
                if (IsStationWithinCurveSpan(z, rib.fwdP0, rib.aftP2))
                {
                    Vector3 ribPt = ribbandCurves.GetRibbandPointAtZ(i, z);

                    nodes.Add(new SectionPointNode
                    {
                        name = rib.name,
                        uParam = rib.uSectionParam,
                        point = new Vector3(sSign * Mathf.Abs(ribPt.x), ribPt.y, z)
                    });
                }
            }
        }

        // 4. Discard any points falling below the Fashion / Transom boundary curve at station Z
        if (isTransomRegion && minTransomY > -900f)
        {
            nodes.RemoveAll(n => n.name != "Fashion Transom Base" && n.point.y < minTransomY - 0.005f);
        }

        // 5. Sort nodes strictly by uParam (from Keel/Transom uParam to TopLine u=1)
        nodes.Sort((a, b) => a.uParam.CompareTo(b.uParam));

        // 5. Remove any duplicate nodes positioned at almost identical locations
        List<SectionPointNode> uniqueNodes = new List<SectionPointNode>();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (uniqueNodes.Count == 0)
            {
                uniqueNodes.Add(nodes[i]);
            }
            else
            {
                Vector3 prev = uniqueNodes[uniqueNodes.Count - 1].point;
                if (Vector3.Distance(prev, nodes[i].point) > 0.005f)
                {
                    uniqueNodes.Add(nodes[i]);
                }
            }
        }

        return uniqueNodes;
    }

    // --- GIZMO RENDERING ---

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
        if (sheerCurves == null) return;

        UpdateCachedCurves();

        float sternZ = (fashionCurves != null) ? fashionCurves.stbdP4.z : ((sheerCurves != null) ? sheerCurves.sternP3.z : -15f);
        float stemZ = (sheerCurves != null) ? sheerCurves.stemP3.z : 15f;

        int count = Mathf.Max(2, sectionCount);

        for (int s = 0; s < count; s++)
        {
            float t = (float)s / (count - 1);
            float z = Mathf.Lerp(stemZ, sternZ, t);

            // Sample Starboard and Port transverse section nodes
            List<SectionPointNode> stbdNodes = GetTransverseSectionNodesAtZ(z, 1f);
            List<SectionPointNode> portNodes = GetTransverseSectionNodesAtZ(z, -1f);

            // Draw Transverse Section Curves (X/Y cuts)
            if (showTransverseSections)
            {
                DrawSectionPolyline(stbdNodes, starboardColor);
                DrawSectionPolyline(portNodes, portColor);
            }

            // Draw Node Spheres at Ribband Intersections
            if (showSectionNodes)
            {
                Gizmos.color = starboardColor;
                for (int n = 0; n < stbdNodes.Count; n++)
                {
                    Gizmos.DrawSphere(stbdNodes[n].point, nodeRadius);
                }

                Gizmos.color = portColor;
                for (int n = 0; n < portNodes.Count; n++)
                {
                    Gizmos.DrawSphere(portNodes[n].point, nodeRadius);
                }
            }

            // Draw Polygon Skeleton connecting nodes
            if (showPolygonSkeleton)
            {
                Gizmos.color = new Color(sectionColor.r, sectionColor.g, sectionColor.b, 0.4f);
                DrawSectionSkeleton(stbdNodes);
                DrawSectionSkeleton(portNodes);
            }

#if UNITY_EDITOR
            if (showLabels && stbdNodes.Count > 0)
            {
                GUIStyle labelStyle = GetCachedLabelStyle();
                Vector3 topPt = stbdNodes[stbdNodes.Count - 1].point;
                Handles.Label(topPt + Vector3.up * 0.2f, $"Section {s + 1} (Z={z:F2}m)", labelStyle);
            }
#endif
        }
    }

    private void DrawSectionPolyline(List<SectionPointNode> nodes, Color color, int stepsPerSegment = 8)
    {
        if (nodes == null || nodes.Count < 2) return;

        Gizmos.color = color;

        // Smooth Catmull-Rom or linear interpolation across ordered transverse section nodes
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Vector3 p0 = (i == 0) ? nodes[i].point : nodes[i - 1].point;
            Vector3 p1 = nodes[i].point;
            Vector3 p2 = nodes[i + 1].point;
            Vector3 p3 = (i + 2 < nodes.Count) ? nodes[i + 2].point : nodes[i + 1].point;

            Vector3 prev = p1;
            for (int s = 1; s <= stepsPerSegment; s++)
            {
                float t = (float)s / stepsPerSegment;
                Vector3 curr = GetCatmullRomPoint(t, p0, p1, p2, p3);
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }
        }
    }

    private void DrawSectionSkeleton(List<SectionPointNode> nodes)
    {
        if (nodes == null || nodes.Count < 2) return;
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Gizmos.DrawLine(nodes[i].point, nodes[i + 1].point);
        }
    }

    private Vector3 GetCatmullRomPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float alpha = 0.5f)
    {
        if (p0 == p1 && p1 == p2 && p2 == p3) return p1;

        float GetT(float tPrev, Vector3 pA, Vector3 pB)
        {
            float d = Vector3.Distance(pA, pB);
            return tPrev + Mathf.Max(0.0001f, Mathf.Pow(d, alpha));
        }

        float t0 = 0f;
        float t1 = GetT(t0, p0, p1);
        float t2 = GetT(t1, p1, p2);
        float t3 = GetT(t2, p2, p3);

        float u = Mathf.Lerp(t1, t2, t);

        Vector3 a1 = (t1 - u) / (t1 - t0) * p0 + (u - t0) / (t1 - t0) * p1;
        Vector3 a2 = (t2 - u) / (t2 - t1) * p1 + (u - t1) / (t2 - t1) * p2;
        Vector3 a3 = (t3 - u) / (t3 - t2) * p2 + (u - t2) / (t3 - t2) * p3;

        Vector3 b1 = (t2 - u) / (t2 - t0) * a1 + (u - t0) / (t2 - t0) * a2;
        Vector3 b2 = (t3 - u) / (t3 - t1) * a2 + (u - t1) / (t3 - t1) * a3;

        return (t2 - u) / (t2 - t1) * b1 + (u - t1) / (t2 - t1) * b2;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SectionModelCurves))]
public class SectionModelCurvesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SectionModelCurves model = (SectionModelCurves)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Transverse Section Model Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Active Stations Count:", $"{model.sectionCount}");

        if (model.ribbandCurves != null)
        {
            EditorGUILayout.LabelField("Tracked Ribbands per Section:", $"{model.ribbandCurves.generatedRibbands.Count}");
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Update Section Model Curves", GUILayout.Height(30)))
        {
            Undo.RecordObject(model, "Update Section Model Profile");
            model.UpdateCachedCurves();
            EditorUtility.SetDirty(model);
        }
    }
}
#endif
