using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class FramingModelMesh : MonoBehaviour
{
    public FramingModel framingModel;
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public FashionTimberModel sternFashionTimber;

    [Header("Mesh Settings")]
    public int verticalSegments = 50;
    public Material hullMaterial;

    private class MeshSlice
    {
        public List<Vector3> outerStbd = new List<Vector3>();
        public List<Vector3> innerStbd = new List<Vector3>();
        public List<Vector3> outerPort = new List<Vector3>();
        public List<Vector3> innerPort = new List<Vector3>();
        
        public int stbdOuterOffset = -1;
        public int stbdInnerOffset = -1;
        public int portOuterOffset = -1;
        public int portInnerOffset = -1;
    }

    [ContextMenu("Generate Mesh")]
    public void GenerateMesh()
    {
        if (framingModel == null || sheerPlan == null || bodyPlan == null)
        {
            Debug.LogError("FramingModelMesh: Missing required references.");
            return;
        }

        transform.position = framingModel.transform.position;
        transform.rotation = framingModel.transform.rotation;
        transform.localScale = framingModel.transform.localScale;

        Mesh mesh = new Mesh();
        mesh.name = "Ship Hull Mesh";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        List<float> rawZSlices = framingModel.GetFrameZPositions();
        if (rawZSlices == null || rawZSlices.Count == 0) return;
        
        if (!rawZSlices.Contains(sheerPlan.cachedStemTopPoint.z)) rawZSlices.Add(sheerPlan.cachedStemTopPoint.z);
        rawZSlices.Sort();

        float maxFrameZ = rawZSlices[rawZSlices.Count - 1];
        float stemZ = sheerPlan.cachedStemTopPoint.z;
        if (stemZ > maxFrameZ + 0.01f)
        {
            int extraSlices = 10;
            for (int i = 0; i <= extraSlices; i++)
            {
                float z = Mathf.Lerp(maxFrameZ, stemZ, (float)i / extraSlices);
                if (!rawZSlices.Contains(z)) rawZSlices.Add(z);
            }
        }

        rawZSlices.Sort();

        float maxShipHeight = (bodyPlan.cachedU3.y - sheerPlan.cachedMidshipKeel.y) * 1.5f;

        List<MeshSlice> lowerSlices = new List<MeshSlice>();
        List<MeshSlice> upperSlices = new List<MeshSlice>();

        for (int i = 0; i < rawZSlices.Count; i++)
        {
            float z = rawZSlices[i];
            FramingModel.FrameProfile fp = framingModel.GetRawFrameProfile(z, true);
            
            float hobY = sheerPlan.GetHobYAtZ(z);
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
            if (!fp.isValid) topTimberY = sheerPlan.GetTopTimberYAtZ(z); // Overhang

            float tKeel = Mathf.Clamp01(z / (z > 0 ? sheerPlan.cachedKeelBowPoint.z : sheerPlan.cachedKeelSternPoint.z));
            float theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, (z > 0 ? sheerPlan.cachedKeelBowPoint.y : sheerPlan.cachedKeelSternPoint.y), tKeel);

            Vector3 origin = new Vector3(0, 0, z);
            Vector3 pEnd = fp.hasUpper ? fp.upperP3 : fp.lowerP3;
            Vector3 uAxis = new Vector3(pEnd.x, 0, pEnd.z) - new Vector3(origin.x, 0, origin.z);
            if (uAxis.sqrMagnitude < 0.0001f) uAxis = Vector3.right;
            else uAxis.Normalize();
            Vector3 vAxis = Vector3.up;
            Vector3 framePlaneNormal = Vector3.Cross(uAxis, vAxis).normalized;

            // Generate Lower Slice
            if (z >= sheerPlan.cachedSternRisingEnd.z - 0.01f) // Ensure it stops at Stern Post
            {
                MeshSlice lowerSlice = new MeshSlice();
                float lowerHeight = Mathf.Max(0.01f, hobY - keelY);
                int lowerSegs = Mathf.Max(2, Mathf.RoundToInt(verticalSegments * (lowerHeight / maxShipHeight)));

                for (int j = 0; j < lowerSegs; j++)
                {
                    float v = j / (float)(lowerSegs - 1);
                    float y = Mathf.Lerp(keelY, hobY, v);
                    BuildSlicePoint(y, z, fp, false, theoreticalKeelY, maxShipHeight, framePlaneNormal, lowerSlice);
                }
                RegisterSlice(lowerSlice, vertices);
                lowerSlices.Add(lowerSlice);
            }

            // Generate Upper Slice
            MeshSlice upperSlice = new MeshSlice();
            float upperHeight = Mathf.Max(0.01f, topTimberY - hobY);
            int upperSegs = Mathf.Max(2, Mathf.RoundToInt(verticalSegments * (upperHeight / maxShipHeight)));

            for (int j = 0; j < upperSegs; j++)
            {
                float v = j / (float)(upperSegs - 1);
                float y = Mathf.Lerp(hobY, topTimberY, v);
                BuildSlicePoint(y, z, fp, true, theoreticalKeelY, maxShipHeight, framePlaneNormal, upperSlice);
            }
            RegisterSlice(upperSlice, vertices);
            upperSlices.Add(upperSlice);
        }

        // 3. Inject Canted Stern Fashion Timber into Upper Slices
        if (sternFashionTimber != null)
        {
            MeshSlice fashionSlice = new MeshSlice();
            
            float theoreticalTotalHeight = Mathf.Max(0.01f, sheerPlan.cachedTransomTopPoint.y - sheerPlan.cachedHobSternIntersect.y);
            int currentSegments = Mathf.Max(2, Mathf.RoundToInt(verticalSegments * (theoreticalTotalHeight / maxShipHeight)));

            // Fetch Upper Fashion Timber curve specifically
            Vector3 hobIntersect = sternFashionTimber.GetFashionTimberHobIntersection();
            Vector3 transomTop = sheerPlan.cachedTransomTopPoint;
            
            for (int j = 0; j < currentSegments; j++)
            {
                float fraction = j / (float)(currentSegments - 1);
                float currZ = Mathf.Lerp(hobIntersect.z, transomTop.z, fraction);
                float exactY = Mathf.Lerp(hobIntersect.y, transomTop.y, fraction);
                if (Mathf.Abs(hobIntersect.z - transomTop.z) > 0.001f) exactY = sheerPlan.GetSternProfileYAtZ(currZ);
                
                float exactX = sternFashionTimber.GetNaturalHullXAtYZ(exactY, currZ);
                
                Vector3 ptStbd = new Vector3(exactX, exactY, currZ);
                Vector3 ptPort = new Vector3(-exactX, exactY, currZ);
                
                float nx = 1f;
                framingModel.GetOverhangOuterXAndNormalAtYZ(ptStbd.y, ptStbd.z, out nx);
                
                float tKeel = Mathf.Clamp01(ptStbd.z / sheerPlan.cachedKeelSternPoint.z);
                float theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelSternPoint.y, tKeel);
                float theoreticalTotalShipHeight = Mathf.Max(0.01f, transomTop.y - theoreticalKeelY);
                
                float overallT = Mathf.Clamp01((ptStbd.y - theoreticalKeelY) / theoreticalTotalShipHeight);
                float currentThickness = Mathf.Lerp(bodyPlan.baseFrameThickness, bodyPlan.topFrameThickness, overallT);
                
                float innerX = ptStbd.x - (currentThickness / nx);
                
                fashionSlice.outerStbd.Add(ptStbd);
                fashionSlice.innerStbd.Add(new Vector3(innerX, ptStbd.y, ptStbd.z));
                
                fashionSlice.outerPort.Add(ptPort);
                fashionSlice.innerPort.Add(new Vector3(-innerX, ptPort.y, ptPort.z));
            }

            RegisterSlice(fashionSlice, vertices);
            upperSlices.Insert(0, fashionSlice); // Insert at the very beginning of the Upper hull (Stern-most)

            // Inject Lower Fashion Timber into Lower Slices
            MeshSlice lowerFashionSlice = new MeshSlice();
            
            Vector3 lowerBound = sheerPlan.cachedSternRisingEnd;
            if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
            {
                lowerBound = sheerPlan.cachedDeadwoodAftPoint;
            }

            float lowerTotalHeight = Mathf.Max(0.01f, hobIntersect.y - lowerBound.y);
            int lowerSegments = Mathf.Max(2, Mathf.RoundToInt(verticalSegments * (lowerTotalHeight / maxShipHeight)));
            
            for (int j = 0; j < lowerSegments; j++)
            {
                float fraction = j / (float)(lowerSegments - 1);
                float currZ = Mathf.Lerp(lowerBound.z, hobIntersect.z, fraction);
                float exactY = Mathf.Lerp(lowerBound.y, hobIntersect.y, fraction);
                if (Mathf.Abs(lowerBound.z - hobIntersect.z) > 0.001f) exactY = sheerPlan.GetSternProfileYAtZ(currZ);
                
                float exactX = sternFashionTimber.GetNaturalHullXAtYZ(exactY, currZ);
                
                Vector3 ptStbd = new Vector3(exactX, exactY, currZ);
                Vector3 ptPort = new Vector3(-exactX, exactY, currZ);
                
                float nx = 1f;
                framingModel.GetOverhangOuterXAndNormalAtYZ(ptStbd.y, ptStbd.z, out nx);
                
                float tKeel = Mathf.Clamp01(ptStbd.z / sheerPlan.cachedKeelSternPoint.z);
                float theoreticalKeelY = Mathf.Lerp(sheerPlan.cachedMidshipKeel.y, sheerPlan.cachedKeelSternPoint.y, tKeel);
                
                float overallT = Mathf.Clamp01((ptStbd.y - theoreticalKeelY) / maxShipHeight);
                float currentThickness = Mathf.Lerp(bodyPlan.baseFrameThickness, bodyPlan.topFrameThickness, overallT);
                
                float innerX = ptStbd.x - (currentThickness / nx);
                
                lowerFashionSlice.outerStbd.Add(ptStbd);
                lowerFashionSlice.innerStbd.Add(new Vector3(innerX, ptStbd.y, ptStbd.z));
                
                lowerFashionSlice.outerPort.Add(ptPort);
                lowerFashionSlice.innerPort.Add(new Vector3(-innerX, ptPort.y, ptPort.z));
            }

            RegisterSlice(lowerFashionSlice, vertices);
            lowerSlices.Insert(0, lowerFashionSlice); // Insert at the very beginning of the Lower hull
        }

        // Triangulate using Mesh Zipper
        ZipperList(lowerSlices, triangles);
        ZipperList(upperSlices, triangles);

        // Caps
        CapTopRail(upperSlices, triangles);
        CapKeel(lowerSlices, triangles);
        CapOpenFaces(lowerSlices, upperSlices, triangles);

        Vector3[] localVertices = new Vector3[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            localVertices[i] = transform.InverseTransformPoint(vertices[i]);
        }
        mesh.vertices = localVertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        GetComponent<MeshFilter>().mesh = mesh;
        
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

    private void BuildSlicePoint(float y, float z, FramingModel.FrameProfile fp, bool isUpperSection, float theoreticalKeelY, float maxShipHeight, Vector3 framePlaneNormal, MeshSlice slice)
    {
        Vector3 outerStbdPt, innerStbdPt, outerPortPt, innerPortPt;

        if (framingModel.GetTAtY(fp, y, out float tVal, out bool isUpperProfile))
        {
            outerStbdPt = isUpperProfile ? framingModel.GetUpperSurfacePointAtT(fp, 1f, tVal) : framingModel.GetSurfacePointAtT(fp, 1f, tVal, true);
            Vector3 normalStbd = framingModel.GetInnerNormalAtT(fp, isUpperProfile, tVal, z, framePlaneNormal);
            
            float theoreticalTotalHeight = maxShipHeight; // Used for thickness gradient estimation
            float overallT = Mathf.Clamp01((outerStbdPt.y - theoreticalKeelY) / theoreticalTotalHeight);
            float currentThickness = Mathf.Lerp(bodyPlan.baseFrameThickness, bodyPlan.topFrameThickness, overallT);
            innerStbdPt = outerStbdPt + normalStbd * currentThickness;
            
            outerPortPt = isUpperProfile ? framingModel.GetUpperSurfacePointAtT(fp, -1f, tVal) : framingModel.GetSurfacePointAtT(fp, -1f, tVal, true);
            Vector3 normalPort = new Vector3(-normalStbd.x, normalStbd.y, normalStbd.z);
            innerPortPt = outerPortPt + normalPort * currentThickness;
        }
        else
        {
            float nx = 1f;
            float outerX = framingModel.GetOverhangOuterXAndNormalAtYZ(y, z, out nx);

            float theoreticalTotalHeight = maxShipHeight;
            float overallT = Mathf.Clamp01((y - theoreticalKeelY) / theoreticalTotalHeight);
            float currentThickness = Mathf.Lerp(bodyPlan.baseFrameThickness, bodyPlan.topFrameThickness, overallT);
            float innerX = outerX - (currentThickness / nx);

            outerStbdPt = new Vector3(outerX, y, z);
            innerStbdPt = new Vector3(innerX, y, z);
            outerPortPt = new Vector3(-outerX, y, z);
            innerPortPt = new Vector3(-innerX, y, z);
        }

        slice.outerStbd.Add(outerStbdPt);
        slice.innerStbd.Add(innerStbdPt);
        slice.outerPort.Add(outerPortPt);
        slice.innerPort.Add(innerPortPt);
    }

    private void RegisterSlice(MeshSlice slice, List<Vector3> vertices)
    {
        slice.stbdOuterOffset = vertices.Count;
        vertices.AddRange(slice.outerStbd);
        
        slice.stbdInnerOffset = vertices.Count;
        vertices.AddRange(slice.innerStbd);
        
        slice.portOuterOffset = vertices.Count;
        vertices.AddRange(slice.outerPort);
        
        slice.portInnerOffset = vertices.Count;
        vertices.AddRange(slice.innerPort);
    }

    private void ZipperList(List<MeshSlice> slices, List<int> triangles)
    {
        for (int i = 0; i < slices.Count - 1; i++)
        {
            MeshSlice A = slices[i];
            MeshSlice B = slices[i + 1];

            StitchLines(triangles, A.outerStbd, B.outerStbd, A.stbdOuterOffset, B.stbdOuterOffset, true);
            StitchLines(triangles, A.innerStbd, B.innerStbd, A.stbdInnerOffset, B.stbdInnerOffset, false);
            StitchLines(triangles, A.outerPort, B.outerPort, A.portOuterOffset, B.portOuterOffset, false);
            StitchLines(triangles, A.innerPort, B.innerPort, A.portInnerOffset, B.portInnerOffset, true);
            
            // Centerline Weld Cap (only weld segments between the slices)
            AddQuad(triangles, A.stbdOuterOffset, B.stbdOuterOffset, B.portOuterOffset, A.portOuterOffset); // Bottom weld
            AddQuad(triangles, A.portInnerOffset, B.portInnerOffset, B.stbdInnerOffset, A.stbdInnerOffset); // Inner bottom weld
        }
    }

    private void CapTopRail(List<MeshSlice> upperSlices, List<int> triangles)
    {
        for (int i = 0; i < upperSlices.Count - 1; i++)
        {
            MeshSlice A = upperSlices[i];
            MeshSlice B = upperSlices[i + 1];

            AddQuad(triangles, 
                A.stbdInnerOffset + A.innerStbd.Count - 1, 
                B.stbdInnerOffset + B.innerStbd.Count - 1, 
                B.stbdOuterOffset + B.outerStbd.Count - 1, 
                A.stbdOuterOffset + A.outerStbd.Count - 1);

            AddQuad(triangles, 
                A.portOuterOffset + A.outerPort.Count - 1, 
                B.portOuterOffset + B.outerPort.Count - 1, 
                B.portInnerOffset + B.innerPort.Count - 1, 
                A.portInnerOffset + A.innerPort.Count - 1);
        }
    }

    private void CapKeel(List<MeshSlice> lowerSlices, List<int> triangles)
    {
        for (int i = 0; i < lowerSlices.Count - 1; i++)
        {
            MeshSlice A = lowerSlices[i];
            MeshSlice B = lowerSlices[i + 1];

            AddQuad(triangles, 
                A.stbdInnerOffset, 
                B.stbdInnerOffset, 
                B.stbdOuterOffset, 
                A.stbdOuterOffset);

            AddQuad(triangles, 
                A.portOuterOffset, 
                B.portOuterOffset, 
                B.portInnerOffset, 
                A.portInnerOffset);
        }
    }

    private void CapOpenFaces(List<MeshSlice> lowerSlices, List<MeshSlice> upperSlices, List<int> triangles)
    {
        if (lowerSlices.Count > 0)
        {
            MeshSlice stern = lowerSlices[0];
            for (int j = 0; j < stern.outerStbd.Count - 1; j++)
            {
                AddQuad(triangles, stern.stbdInnerOffset + j, stern.stbdInnerOffset + j + 1, stern.stbdOuterOffset + j + 1, stern.stbdOuterOffset + j);
                AddQuad(triangles, stern.portOuterOffset + j, stern.portOuterOffset + j + 1, stern.portInnerOffset + j + 1, stern.portInnerOffset + j);
            }
        }
        
        if (upperSlices.Count > 0)
        {
            MeshSlice stern = upperSlices[0]; // This is the canted fashion timber!
            for (int j = 0; j < stern.outerStbd.Count - 1; j++)
            {
                AddQuad(triangles, stern.stbdInnerOffset + j, stern.stbdInnerOffset + j + 1, stern.stbdOuterOffset + j + 1, stern.stbdOuterOffset + j);
                AddQuad(triangles, stern.portOuterOffset + j, stern.portOuterOffset + j + 1, stern.portInnerOffset + j + 1, stern.portInnerOffset + j);
            }
        }

        if (upperSlices.Count > 0)
        {
            MeshSlice bow = upperSlices[upperSlices.Count - 1]; // Assume bow is same for both
            for (int j = 0; j < bow.outerStbd.Count - 1; j++)
            {
                AddQuad(triangles, bow.stbdOuterOffset + j, bow.stbdOuterOffset + j + 1, bow.stbdInnerOffset + j + 1, bow.stbdInnerOffset + j);
                AddQuad(triangles, bow.portInnerOffset + j, bow.portInnerOffset + j + 1, bow.portOuterOffset + j + 1, bow.portOuterOffset + j);
            }
        }
        
        if (lowerSlices.Count > 0)
        {
            MeshSlice bow = lowerSlices[lowerSlices.Count - 1];
            for (int j = 0; j < bow.outerStbd.Count - 1; j++)
            {
                AddQuad(triangles, bow.stbdOuterOffset + j, bow.stbdOuterOffset + j + 1, bow.stbdInnerOffset + j + 1, bow.stbdInnerOffset + j);
                AddQuad(triangles, bow.portInnerOffset + j, bow.portInnerOffset + j + 1, bow.portOuterOffset + j + 1, bow.portOuterOffset + j);
            }
        }
    }

    private void StitchLines(List<int> triangles, List<Vector3> A, List<Vector3> B, int offsetA, int offsetB, bool reverseWinding)
    {
        int i = 0;
        int j = 0;

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
