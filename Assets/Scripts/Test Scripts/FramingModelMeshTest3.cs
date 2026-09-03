using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[ExecuteInEditMode]
public class FramingModelMeshTest3 : MonoBehaviour
{
    [Header("Model References")]
    [Tooltip("Reference to the FramingModelTest3.")]
    public FramingModelTest3 framingModelTest;
    public SheerPlan sheerPlan;
    public BodyPlanTest bodyPlan;

    [Header("Fashion Timber References")]
    [Tooltip("Reference to the FashionTimberModelTest3 (Aft Face Outer).")]
    public FashionTimberModelTest3 sternFashionTimberTest;
    [Tooltip("Reference to the ForeFaceFashionTimberModelTest3 (Fore Face Outer).")]
    public ForeFaceFashionTimberModelTest3 foreFaceFashionTimberTest;
    [Tooltip("Reference to the InnerFramingModelTest3 (Inner Frames).")]
    public InnerFramingModelTest3 innerFramingModel;
    [Tooltip("Reference to the InnerFashionTimberModelTest3 (Aft Face Inner).")]
    public InnerFashionTimberModelTest3 innerFashionTimberTest;
    [Tooltip("Reference to the InnerForeFaceFashionTimberModelTest3 (Fore Face Inner).")]
    public InnerForeFaceFashionTimberModelTest3 innerForeFaceFashionTimberTest;

    [Header("Mesh Settings")]
    public int verticalSegments = 50;
    public Material hullMaterial;

    private class FrameBox
    {
        // Stbd Lines
        public List<Vector3> foreOuterStbd = new List<Vector3>();
        public List<Vector3> foreInnerStbd = new List<Vector3>();
        public List<Vector3> aftOuterStbd = new List<Vector3>();
        public List<Vector3> aftInnerStbd = new List<Vector3>();
        
        // Port Lines
        public List<Vector3> foreOuterPort = new List<Vector3>();
        public List<Vector3> foreInnerPort = new List<Vector3>();
        public List<Vector3> aftOuterPort = new List<Vector3>();
        public List<Vector3> aftInnerPort = new List<Vector3>();

        public int offsetBase = -1;
    }

    private void AutoFindReferences()
    {
        if (framingModelTest == null) framingModelTest = Object.FindObjectOfType<FramingModelTest3>();
        if (sheerPlan == null) sheerPlan = Object.FindObjectOfType<SheerPlan>();
        if (bodyPlan == null) bodyPlan = Object.FindObjectOfType<BodyPlanTest>();
        if (sternFashionTimberTest == null) sternFashionTimberTest = Object.FindObjectOfType<FashionTimberModelTest3>();
        if (foreFaceFashionTimberTest == null) foreFaceFashionTimberTest = Object.FindObjectOfType<ForeFaceFashionTimberModelTest3>();
        if (innerFramingModel == null) innerFramingModel = Object.FindObjectOfType<InnerFramingModelTest3>();
        if (innerFashionTimberTest == null) innerFashionTimberTest = Object.FindObjectOfType<InnerFashionTimberModelTest3>();
        if (innerForeFaceFashionTimberTest == null) innerForeFaceFashionTimberTest = Object.FindObjectOfType<InnerForeFaceFashionTimberModelTest3>();
    }

    [ContextMenu("Generate Frame Mesh")]
    public void GenerateMesh()
    {
        AutoFindReferences();

        if (framingModelTest == null || sheerPlan == null || bodyPlan == null)
        {
            Debug.LogError("FramingModelMeshTest3: Missing required references (FramingModelTest3, SheerPlan, or BodyPlanTest).");
            return;
        }

        transform.position = framingModelTest.transform.position;
        transform.rotation = framingModelTest.transform.rotation;
        transform.localScale = framingModelTest.transform.localScale;

        Mesh mesh = new Mesh();
        mesh.name = "Ship Frames Mesh (Test 3)";

        List<Vector3> vertices = new List<Vector3>();
        List<float> frameZs = framingModelTest.GetFrameZPositions();
        if (frameZs == null || frameZs.Count == 0) return;

        float maxShipHeight = (sheerPlan.cachedTopTimberSternIntersect.y - sheerPlan.cachedMidshipKeel.y) * 1.5f;
        float thickness = framingModelTest.frameSpacing * framingModelTest.GetFrameThicknessRatioValue();

        List<int> triangles = new List<int>();
        List<FrameBox> generatedFrames = new List<FrameBox>();

        foreach (float z in frameZs)
        {
            float foreZ = z + (thickness / 2f);
            float aftZ = z - (thickness / 2f);

            FrameBox box = GenerateFrameBox(z, foreZ, aftZ, maxShipHeight);
            if (box != null)
            {
                generatedFrames.Add(box);
            }
        }

        // Add Fashion Timbers Box if all references are present
        if (sternFashionTimberTest != null && foreFaceFashionTimberTest != null && innerFashionTimberTest != null && innerForeFaceFashionTimberTest != null)
        {
            FrameBox fashionTimberBox = GenerateFashionTimberBox();
            if (fashionTimberBox != null) generatedFrames.Add(fashionTimberBox);
        }

        // Stitch Each Box
        foreach (FrameBox box in generatedFrames)
        {
            StitchBox(box, vertices, triangles);
        }

        Vector3[] localVertices = new Vector3[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            localVertices[i] = transform.InverseTransformPoint(vertices[i]);
        }
        mesh.vertices = localVertices;
        
        int[] tris = triangles.ToArray();
        for (int i = 0; i < tris.Length; i += 3)
        {
            int temp = tris[i];
            tris[i] = tris[i + 2];
            tris[i + 2] = temp;
        }
        mesh.triangles = tris;
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
        
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (hullMaterial == null)
        {
            Material mat = renderer.sharedMaterial;
            if (mat == null || mat.name != "AutoHullMaterialTest3")
            {
                GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mat = new Material(tempCube.GetComponent<MeshRenderer>().sharedMaterial);
                mat.name = "AutoHullMaterialTest3";
                DestroyImmediate(tempCube);
            }
            
            mat.color = new Color(0.85f, 0.85f, 0.85f, 1f); 
            renderer.sharedMaterial = mat;
        }
        else
        {
            renderer.sharedMaterial = hullMaterial;
        }

        Debug.Log($"FramingModelMeshTest3: Generated mesh with {vertices.Count} vertices and {triangles.Count / 3} triangles across {generatedFrames.Count} frame solids.");
    }

    private FrameBox GenerateFrameBox(float centerZ, float foreZ, float aftZ, float maxShipHeight)
    {
        FrameBox box = new FrameBox();

        FramingModelTest3.FrameProfile fpFore = framingModelTest.GetFrameProfile(foreZ);
        FramingModelTest3.FrameProfile fpAft = framingModelTest.GetFrameProfile(aftZ);

        if (!fpFore.isValid || !fpAft.isValid) return null;

        // Generate matched 1:1 outer and inner curves for Fore face
        GetMatchedFrameCurves(foreZ, fpFore, out List<Vector3> foreOuterStbd, out List<Vector3> foreInnerStbd);
        box.foreOuterStbd = foreOuterStbd;
        box.foreInnerStbd = foreInnerStbd;

        // Generate matched 1:1 outer and inner curves for Aft face
        GetMatchedFrameCurves(aftZ, fpAft, out List<Vector3> aftOuterStbd, out List<Vector3> aftInnerStbd);
        box.aftOuterStbd = aftOuterStbd;
        box.aftInnerStbd = aftInnerStbd;

        if (box.foreOuterStbd.Count < 2 || box.aftOuterStbd.Count < 2) return null;

        // Mirror for Port side
        foreach (var pt in box.foreOuterStbd) box.foreOuterPort.Add(new Vector3(-pt.x, pt.y, pt.z));
        foreach (var pt in box.aftOuterStbd) box.aftOuterPort.Add(new Vector3(-pt.x, pt.y, pt.z));
        foreach (var pt in box.foreInnerStbd) box.foreInnerPort.Add(new Vector3(-pt.x, pt.y, pt.z));
        foreach (var pt in box.aftInnerStbd) box.aftInnerPort.Add(new Vector3(-pt.x, pt.y, pt.z));

        return box;
    }

    private void GetMatchedFrameCurves(float zPos, FramingModelTest3.FrameProfile fp, out List<Vector3> outerStbd, out List<Vector3> innerStbd)
    {
        outerStbd = new List<Vector3>();
        innerStbd = new List<Vector3>();

        Vector3 origin = new Vector3(0, 0, zPos);
        Vector3 pStart = fp.hasLower ? fp.lowerP0 : fp.upperP0;
        Vector3 pEnd = fp.hasUpper ? fp.upperP3 : fp.lowerP3;

        Vector3 uAxis = new Vector3(pEnd.x, 0, pEnd.z) - new Vector3(origin.x, 0, origin.z);
        if (uAxis.sqrMagnitude < 0.0001f) uAxis = Vector3.right;
        else uAxis.Normalize();
        Vector3 vAxis = Vector3.up;
        Vector3 framePlaneNormal = Vector3.Cross(uAxis, vAxis).normalized;

        float peakY = fp.hasUpper ? fp.upperP3.y : fp.lowerP3.y;
        float theoreticalKeelY = sheerPlan.cachedMidshipKeel.y;
        if (zPos > 0)
        {
            float t = Mathf.Clamp01(zPos / sheerPlan.cachedKeelBowPoint.z);
            theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelBowPoint.y, t);
        }
        else
        {
            float t = Mathf.Clamp01(zPos / sheerPlan.cachedKeelSternPoint.z);
            theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelSternPoint.y, t);
        }
        float theoreticalTotalHeight = Mathf.Max(0.01f, peakY - theoreticalKeelY);

        bool hasLower = fp.hasLower;
        bool hasUpper = fp.hasUpper;

        float innerLowerCutoffT = fp.lowerCutoffT;
        float innerUpperCutoffT = fp.upperCutoffT;

        // Evaluate inner cutoffs matching fashion timber / deadwood
        if (innerFramingModel != null && innerForeFaceFashionTimberTest != null && framingModelTest.cutFramesAtFashionTimber)
        {
            float sternMinZ = innerForeFaceFashionTimberTest.GetMinZ();
            float sternMaxZ = innerForeFaceFashionTimberTest.GetMaxZ();

            if (zPos < sternMinZ - 0.001f) return; // Completely behind fashion timber

            if (zPos <= sternMaxZ + 0.001f)
            {
                float fashionY = innerForeFaceFashionTimberTest.GetInnerYAtZ(zPos);
                float topTimberY = sheerPlan.GetTopTimberYAtZ(zPos);
                float hobY = sheerPlan.GetTheoreticalHobYAtZ(zPos);

                if (fashionY >= topTimberY - 0.001f) return;

                if (fashionY > hobY || !hasLower)
                {
                    hasLower = false;
                    if (hasUpper)
                    {
                        float fMin = 0f;
                        float fMax = 1f;
                        for (int i = 0; i < 20; i++)
                        {
                            float midT = (fMin + fMax) * 0.5f;
                            Vector3 pt = framingModelTest.GetInnerPointAtT(fp, true, midT, zPos, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                            if (pt.y < fashionY) fMin = midT;
                            else fMax = midT;
                        }
                        innerUpperCutoffT = (fMin + fMax) * 0.5f;
                    }
                    else return;
                }
                else
                {
                    float fMin = 0f;
                    float fMax = 1f;
                    for (int i = 0; i < 20; i++)
                    {
                        float midT = (fMin + fMax) * 0.5f;
                        Vector3 pt = framingModelTest.GetInnerPointAtT(fp, false, midT, zPos, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                        if (pt.y < fashionY) fMin = midT;
                        else fMax = midT;
                    }
                    innerLowerCutoffT = (fMin + fMax) * 0.5f;
                }
            }
        }

        if (hasLower && framingModelTest.cutFramesAtDeadwood && sheerPlan != null && sheerPlan.showDeadwoodRake)
        {
            float dForeZ = sheerPlan.cachedDeadwoodForePoint.z;
            float dAftZ = sheerPlan.cachedDeadwoodAftPoint.z;

            if (zPos >= dAftZ - 0.001f && zPos <= dForeZ + 0.001f)
            {
                float deadwoodY = sheerPlan.GetDeadwoodYAtZ(zPos);
                float dMin = 0f;
                float dMax = 1f;
                for (int i = 0; i < 20; i++)
                {
                    float midT = (dMin + dMax) * 0.5f;
                    Vector3 pt = framingModelTest.GetInnerPointAtT(fp, false, midT, zPos, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                    if (pt.y < deadwoodY) dMin = midT;
                    else dMax = midT;
                }
                float deadwoodCutoffT = (dMin + dMax) * 0.5f;
                innerLowerCutoffT = Mathf.Max(innerLowerCutoffT, deadwoodCutoffT);
            }
        }

        int lowerSegs = 25;
        int upperSegs = 25;

        if (hasLower)
        {
            for (int i = 0; i <= lowerSegs; i++)
            {
                float t = i / (float)lowerSegs;
                float actualT = Mathf.Lerp(innerLowerCutoffT, fp.lowerTopCutoffT, t);

                Vector3 outerPt = framingModelTest.GetSurfacePointAtT(fp, 1f, actualT, true);
                Vector3 innerPt = framingModelTest.GetInnerPointAtT(fp, false, actualT, zPos, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);

                outerStbd.Add(outerPt);
                innerStbd.Add(innerPt);
            }
        }

        if (hasUpper)
        {
            float localTtY = sheerPlan.GetTopTimberYAtZ(zPos);
            float innerUpperTopCutoffT = 1f;
            {
                float tMin = 0f;
                float tMax = 1f;
                for (int i = 0; i < 20; i++)
                {
                    float midT = (tMin + tMax) / 2f;
                    Vector3 pt = framingModelTest.GetInnerPointAtT(fp, true, midT, zPos, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);
                    if (pt.y < localTtY) tMin = midT;
                    else tMax = midT;
                }
                innerUpperTopCutoffT = (tMin + tMax) / 2f;
            }

            if (innerUpperCutoffT < innerUpperTopCutoffT - 0.001f)
            {
                int startI = (hasLower && Mathf.Abs(fp.lowerTopCutoffT - 1f) < 0.001f && Mathf.Abs(innerUpperCutoffT - 0f) < 0.001f) ? 1 : 0;

                for (int i = startI; i <= upperSegs; i++)
                {
                    float t = i / (float)upperSegs;
                    float actualT = Mathf.Lerp(innerUpperCutoffT, innerUpperTopCutoffT, t);

                    Vector3 outerPt = framingModelTest.GetUpperSurfacePointAtT(fp, 1f, actualT);
                    Vector3 innerPt = framingModelTest.GetInnerPointAtT(fp, true, actualT, zPos, origin, framePlaneNormal, theoreticalKeelY, theoreticalTotalHeight);

                    outerStbd.Add(outerPt);
                    innerStbd.Add(innerPt);
                }
            }
        }
    }

    private FrameBox GenerateFashionTimberBox()
    {
        FrameBox box = new FrameBox();

        int segs = verticalSegments;

        // Stbd
        box.foreOuterStbd = foreFaceFashionTimberTest.GetFashionTimberPoints(true, segs);
        box.aftOuterStbd = sternFashionTimberTest.GetFashionTimberPoints(true, segs);
        box.foreInnerStbd = innerForeFaceFashionTimberTest.GetFashionTimberPoints(true, segs);
        box.aftInnerStbd = innerFashionTimberTest.GetFashionTimberPoints(true, segs);

        // Port
        box.foreOuterPort = foreFaceFashionTimberTest.GetFashionTimberPoints(false, segs);
        box.aftOuterPort = sternFashionTimberTest.GetFashionTimberPoints(false, segs);
        box.foreInnerPort = innerForeFaceFashionTimberTest.GetFashionTimberPoints(false, segs);
        box.aftInnerPort = innerFashionTimberTest.GetFashionTimberPoints(false, segs);

        return box;
    }

    private void StitchBox(FrameBox box, List<Vector3> vertices, List<int> triangles)
    {
        box.offsetBase = vertices.Count;

        // Register all points
        vertices.AddRange(box.foreOuterStbd); // 0
        int foOffset = box.offsetBase;
        
        vertices.AddRange(box.aftOuterStbd);  // 1
        int aoOffset = foOffset + box.foreOuterStbd.Count;
        
        vertices.AddRange(box.foreInnerStbd); // 2
        int fiOffset = aoOffset + box.aftOuterStbd.Count;
        
        vertices.AddRange(box.aftInnerStbd);  // 3
        int aiOffset = fiOffset + box.foreInnerStbd.Count;

        vertices.AddRange(box.foreOuterPort); // 4
        int fopOffset = aiOffset + box.aftInnerStbd.Count;
        
        vertices.AddRange(box.aftOuterPort);  // 5
        int aopOffset = fopOffset + box.foreOuterPort.Count;
        
        vertices.AddRange(box.foreInnerPort); // 6
        int fipOffset = aopOffset + box.aftOuterPort.Count;
        
        vertices.AddRange(box.aftInnerPort);  // 7
        int aipOffset = fipOffset + box.foreInnerPort.Count;

        // Stitch faces vertically using Zipper logic for variable length arrays
        StitchLines(triangles, box.foreInnerStbd, box.foreOuterStbd, fiOffset, foOffset, true);  // Stbd Front
        StitchLines(triangles, box.aftOuterStbd, box.aftInnerStbd, aoOffset, aiOffset, true);    // Stbd Back
        StitchLines(triangles, box.foreOuterStbd, box.aftOuterStbd, foOffset, aoOffset, true);   // Stbd Outer
        StitchLines(triangles, box.aftInnerStbd, box.foreInnerStbd, aiOffset, fiOffset, true);   // Stbd Inner

        StitchLines(triangles, box.foreOuterPort, box.foreInnerPort, fopOffset, fipOffset, true); // Port Front
        StitchLines(triangles, box.aftInnerPort, box.aftOuterPort, aipOffset, aopOffset, true);   // Port Back
        StitchLines(triangles, box.aftOuterPort, box.foreOuterPort, aopOffset, fopOffset, true);   // Port Outer
        StitchLines(triangles, box.foreInnerPort, box.aftInnerPort, fipOffset, aipOffset, true);   // Port Inner

        // Top & Bottom Caps
        if (box.foreOuterStbd.Count > 0 && box.aftOuterStbd.Count > 0 && box.foreInnerStbd.Count > 0 && box.aftInnerStbd.Count > 0)
        {
            AddQuad(triangles, aoOffset + box.aftOuterStbd.Count - 1, foOffset + box.foreOuterStbd.Count - 1, fiOffset + box.foreInnerStbd.Count - 1, aiOffset + box.aftInnerStbd.Count - 1); // Top Stbd
            AddQuad(triangles, aiOffset + 0, fiOffset + 0, foOffset + 0, aoOffset + 0); // Bottom Stbd
        }

        if (box.foreOuterPort.Count > 0 && box.aftOuterPort.Count > 0 && box.foreInnerPort.Count > 0 && box.aftInnerPort.Count > 0)
        {
            AddQuad(triangles, fopOffset + box.foreOuterPort.Count - 1, aopOffset + box.aftOuterPort.Count - 1, aipOffset + box.aftInnerPort.Count - 1, fipOffset + box.foreInnerPort.Count - 1); // Top Port
            AddQuad(triangles, fipOffset + 0, aipOffset + 0, aopOffset + 0, fopOffset + 0); // Bottom Port
        }
    }

    private void StitchLines(List<int> triangles, List<Vector3> A, List<Vector3> B, int offsetA, int offsetB, bool reverseWinding)
    {
        int i = 0;
        int j = 0;
        if (A.Count == 0 || B.Count == 0) return;

        while (i < A.Count - 1 || j < B.Count - 1)
        {
            if (i == A.Count - 1)
            {
                AddTri(triangles, offsetA + i, offsetB + j, offsetB + j + 1, reverseWinding);
                j++;
            }
            else if (j == B.Count - 1)
            {
                AddTri(triangles, offsetA + i, offsetB + j, offsetA + i + 1, reverseWinding);
                i++;
            }
            else
            {
                float dist1 = Vector3.Distance(A[i + 1], B[j]);
                float dist2 = Vector3.Distance(A[i], B[j + 1]);

                if (dist1 < dist2)
                {
                    AddTri(triangles, offsetA + i, offsetB + j, offsetA + i + 1, reverseWinding);
                    i++;
                }
                else
                {
                    AddTri(triangles, offsetA + i, offsetB + j, offsetB + j + 1, reverseWinding);
                    j++;
                }
            }
        }
    }

    private void AddTri(List<int> triangles, int v0, int v1, int v2, bool reverse)
    {
        if (reverse)
        {
            triangles.Add(v0);
            triangles.Add(v2);
            triangles.Add(v1);
        }
        else
        {
            triangles.Add(v0);
            triangles.Add(v1);
            triangles.Add(v2);
        }
    }

    private void AddQuad(List<int> triangles, int v0, int v1, int v2, int v3)
    {
        triangles.Add(v0);
        triangles.Add(v1);
        triangles.Add(v2);

        triangles.Add(v0);
        triangles.Add(v2);
        triangles.Add(v3);
    }
}
