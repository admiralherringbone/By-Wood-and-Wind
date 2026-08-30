using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class FramingModelMeshTest1 : MonoBehaviour
{
    [Tooltip("Reference to the FramingModelTest.")]
    public FramingModelTest framingModelTest;
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    [Tooltip("Reference to the FashionTimberModelTest (Aft Face).")]
    public FashionTimberModelTest sternFashionTimberTest;
    [Tooltip("Reference to the ForeFaceFashionTimberModelTest.")]
    public ForeFaceFashionTimberModelTest foreFaceFashionTimberTest;
    public InnerFramingModelTest innerFramingModel;
    [Tooltip("Reference to the InnerFashionTimberModelTest (Aft Inner).")]
    public InnerFashionTimberModelTest innerFashionTimberTest;
    [Tooltip("Reference to the InnerForeFaceFashionTimberModelTest.")]
    public InnerForeFaceFashionTimberModelTest innerForeFaceFashionTimberTest;

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

    [ContextMenu("Generate Frame Mesh")]
    public void GenerateMesh()
    {
        if (framingModelTest == null || sheerPlan == null || bodyPlan == null)
        {
            Debug.LogError("FramingModelMeshTest1: Missing required references.");
            return;
        }

        transform.position = framingModelTest.transform.position;
        transform.rotation = framingModelTest.transform.rotation;
        transform.localScale = framingModelTest.transform.localScale;

        Mesh mesh = new Mesh();
        mesh.name = "Ship Frames Mesh (Test 1)";

        List<Vector3> vertices = new List<Vector3>();
        // 1. Get Frame Z Positions from the FramingModelTest
        List<float> frameZs = framingModelTest.GetFrameZPositions();
        if (frameZs == null || frameZs.Count == 0) return;

        float maxShipHeight = (bodyPlan.cachedU3.y - sheerPlan.cachedMidshipKeel.y) * 1.5f;
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

        // Add Fashion Timbers
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
            if (mat == null || mat.name != "AutoHullMaterial")
            {
                GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mat = new Material(tempCube.GetComponent<MeshRenderer>().sharedMaterial);
                mat.name = "AutoHullMaterial";
                DestroyImmediate(tempCube);
            }
            
            mat.color = new Color(0.85f, 0.85f, 0.85f, 1f); 
            renderer.sharedMaterial = mat;
        }
        else
        {
            renderer.sharedMaterial = hullMaterial;
        }
    }

    private FrameBox GenerateFrameBox(float centerZ, float foreZ, float aftZ, float maxShipHeight)
    {
        FrameBox box = new FrameBox();

        FramingModelTest.FrameProfile fpFore = framingModelTest.GetFrameProfile(foreZ);
        FramingModelTest.FrameProfile fpAft = framingModelTest.GetFrameProfile(aftZ);

        if (!fpFore.isValid || !fpAft.isValid) return null;

        // Fetch exact inner curves
        if (innerFramingModel != null)
        {
            box.foreInnerStbd = innerFramingModel.GetCleanedInnerCurve(foreZ, fpFore);
            box.aftInnerStbd = innerFramingModel.GetCleanedInnerCurve(aftZ, fpAft);
            
            foreach (var pt in box.foreInnerStbd) box.foreInnerPort.Add(new Vector3(-pt.x, pt.y, pt.z));
            foreach (var pt in box.aftInnerStbd) box.aftInnerPort.Add(new Vector3(-pt.x, pt.y, pt.z));
        }

        // Generate matching outer curves
        box.foreOuterStbd = GetOuterCurve(foreZ, fpFore, maxShipHeight);
        box.aftOuterStbd = GetOuterCurve(aftZ, fpAft, maxShipHeight);
        
        foreach (var pt in box.foreOuterStbd) box.foreOuterPort.Add(new Vector3(-pt.x, pt.y, pt.z));
        foreach (var pt in box.aftOuterStbd) box.aftOuterPort.Add(new Vector3(-pt.x, pt.y, pt.z));

        return box;
    }

    private List<Vector3> GetOuterCurve(float z, FramingModelTest.FrameProfile fp, float maxShipHeight)
    {
        List<Vector3> outer = new List<Vector3>();
        float keelY = sheerPlan.GetKeelYAtZ(z);
        if (fp.hasLower)
        {
            keelY = Mathf.Max(keelY, fp.lowerP0.y);
            if (sheerPlan.showDeadwoodRake && z < sheerPlan.cachedDeadwoodForePoint.z && z >= sheerPlan.cachedDeadwoodAftPoint.z)
            {
                keelY = Mathf.Max(sheerPlan.GetDeadwoodYAtZ(z), fp.lowerP0.y);
            }
        }
        float topTimberY = fp.hasUpper ? Mathf.Min(fp.upperP3.y, sheerPlan.GetTopTimberYAtZ(z)) : fp.lowerP3.y;
        if (!fp.isValid) topTimberY = sheerPlan.GetTopTimberYAtZ(z);

        float fashionY = sheerPlan.GetSternProfileYAtZ(z);
        if (foreFaceFashionTimberTest != null)
        {
            float sternMaxZ = foreFaceFashionTimberTest.GetMaxZ();
            if (z < sternMaxZ) fashionY = foreFaceFashionTimberTest.GetInnerYAtZ(z);
        }

        if (fashionY > 0f)
        {
            keelY = Mathf.Max(keelY, fashionY);
            if (keelY >= topTimberY) return outer;
        }

        float height = Mathf.Max(0.01f, topTimberY - keelY);
        int segs = Mathf.Max(2, Mathf.RoundToInt(verticalSegments * (height / maxShipHeight)));

        for (int j = 0; j < segs; j++)
        {
            float v = j / (float)(segs - 1);
            float y = Mathf.Lerp(keelY, topTimberY, v);
            
            if (framingModelTest.GetTAtY(fp, y, out float tVal, out bool isUpperProfile))
            {
                outer.Add(isUpperProfile ? framingModelTest.GetUpperSurfacePointAtT(fp, 1f, tVal) : framingModelTest.GetSurfacePointAtT(fp, 1f, tVal, true));
            }
            else
            {
                outer.Add(new Vector3(framingModelTest.GetOverhangOuterXAndNormalAtYZ(y, z, out _), y, z));
            }
        }
        return outer;
    }

    private FrameBox GenerateFashionTimberBox()
    {
        FrameBox box = new FrameBox();

        // Stbd
        box.foreOuterStbd = foreFaceFashionTimberTest.GetFashionTimberPoints(true);
        box.aftOuterStbd = sternFashionTimberTest.GetFashionTimberPoints(true, verticalSegments);
        box.foreInnerStbd = innerForeFaceFashionTimberTest.GetFashionTimberPoints(true);
        box.aftInnerStbd = innerFashionTimberTest.GetFashionTimberPoints(true, verticalSegments);

        // Port
        box.foreOuterPort = foreFaceFashionTimberTest.GetFashionTimberPoints(false);
        box.aftOuterPort = sternFashionTimberTest.GetFashionTimberPoints(false, verticalSegments);
        box.foreInnerPort = innerForeFaceFashionTimberTest.GetFashionTimberPoints(false);
        box.aftInnerPort = innerFashionTimberTest.GetFashionTimberPoints(false, verticalSegments);

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

        // Stitch vertically using Zipper logic for variable length arrays
        StitchLines(triangles, box.foreInnerStbd, box.foreOuterStbd, fiOffset, foOffset, true); // Stbd Front
        StitchLines(triangles, box.aftOuterStbd, box.aftInnerStbd, aoOffset, aiOffset, true); // Stbd Back
        StitchLines(triangles, box.foreOuterStbd, box.aftOuterStbd, foOffset, aoOffset, true); // Stbd Outer
        StitchLines(triangles, box.aftInnerStbd, box.foreInnerStbd, aiOffset, fiOffset, true); // Stbd Inner

        StitchLines(triangles, box.foreOuterPort, box.foreInnerPort, fopOffset, fipOffset, true); // Port Front
        StitchLines(triangles, box.aftInnerPort, box.aftOuterPort, aipOffset, aopOffset, true); // Port Back
        StitchLines(triangles, box.aftOuterPort, box.foreOuterPort, aopOffset, fopOffset, true); // Port Outer
        StitchLines(triangles, box.foreInnerPort, box.aftInnerPort, fipOffset, aipOffset, true); // Port Inner

        // Caps
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
