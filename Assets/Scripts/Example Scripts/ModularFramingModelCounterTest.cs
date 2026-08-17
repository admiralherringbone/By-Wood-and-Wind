using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class ModularFramingModelCounterTest : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public ModularSheerPlanCounterTest sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan.")]
    public ModularBreadthPlanCounterTest breadthPlan;

    [Tooltip("Reference to the ModularBodyPlan.")]
    public ModularBodyPlanCounterTest bodyPlan;

    [Header("Frame Dimensions")]
    [Tooltip("The longitudinal width of each timber frame (Z-axis).")]
    public float frameThickness = 0.3f;
    [Tooltip("The inward depth of the frame from the moulded line (structural thickness).")]
    public float frameDepth = 0.5f;

    [Header("Gizmo Settings")]
    public Color frameColor = Color.cyan;
    [Header("Visualization")]
    public bool showFrames = true;
    public bool showFrameLabels = false;
    public bool showFloorSweepsGuide = true;
    [Tooltip("Draws lines showing the control arms for each frame.")]
    public bool showSurfaceRibbands = true;
    public bool showWaterlines = false;
    public bool showFrameControlArms = false;

    public struct FrameProfile
    {
        public bool isValid;
        public bool hasLower;
        public Vector3 lowerP0, lowerP1_inner, lowerP1, lowerPMid, lowerP2, lowerP3;
        
        public bool hasUpper;
        public float upperCutoffT; // LOWER cutoff (e.g. for Fashion Timber)
        public float upperTopCutoffT; // UPPER cutoff (e.g. for partial bulwarks)
        public float lowerCutoffT; // LOWER cutoff for Lower Mould (e.g. for Counter)
        public Vector3 upperP0, upperP1, upperP2, upperP3;
    }

    public virtual FrameProfile GetRawFrameProfile(float z)
    {
        FrameProfile fp = new FrameProfile();
        fp.isValid = false;
        fp.hasLower = true;
        fp.hasUpper = false;
        fp.lowerCutoffT = 0f;

        if (sheerPlan == null || breadthPlan == null || bodyPlan == null) return fp;

        // We no longer arbitrarily disable the lower mould aft of the Stern Post.
        // The Counter sliding system now handles clipping the lower moulds dynamically in GetFrameProfile.

        float hobBreadth = breadthPlan.GetBreadthXAtZ(z);
        float hobY = sheerPlan.GetHobYAtZ(z);
        
        if (fp.hasLower)
        {
            float keelY = sheerPlan.GetKeelYAtZ(z);
            float risingY = sheerPlan.GetRisingLineYAtZ(z);
        
        float keelHalfWidth = bodyPlan.keelWidth / 2f;
        fp.lowerP0 = new Vector3(keelHalfWidth, keelY, z); 
        fp.lowerP3 = new Vector3(hobBreadth, hobY, z);

        // 1. DIAGONAL DEADRISE TAPER (LOWER RIBBAND)
        Vector3 masterL1 = bodyPlan.cachedL1;
        float targetX = keelHalfWidth; // Defaults to the keel width instead of 0
        float targetY = keelY;

        if (z >= 0)
        {
            float bowZ = sheerPlan.cachedBowRisingEnd.z;
            if (bowZ > 0.001f)
            {
                float t = sheerPlan.GetRisingLineTAtZ(z);
                Vector3 p0 = masterL1;
                Vector3 p2 = new Vector3(keelHalfWidth, sheerPlan.cachedBowRisingEnd.y, sheerPlan.cachedBowRisingEnd.z);
                // Control point mimics the Rising Line's control Z, and projects X/Y to form a swept curve
                Vector3 p1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedBowP1Ctrl.z); 
                
                targetX = CalculateQuadraticBezierPoint(t, p0, p1, p2).x;
                targetY = CalculateQuadraticBezierPoint(t, p0, p1, p2).y;
            }
        }
        else
        {
            float sternZ = sheerPlan.cachedSternRisingEnd.z;
            if (sternZ < -0.001f)
            {
                float t = sheerPlan.GetRisingLineTAtZ(z);
                Vector3 p0 = masterL1;
                Vector3 p2 = new Vector3(keelHalfWidth, sheerPlan.cachedSternRisingEnd.y, sheerPlan.cachedSternRisingEnd.z);
                Vector3 p1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedSternP1Ctrl.z);
                
                targetX = CalculateQuadraticBezierPoint(t, p0, p1, p2).x;
                targetY = CalculateQuadraticBezierPoint(t, p0, p1, p2).y;
            }
        }

        targetY = Mathf.Max(targetY, keelY);

        fp.lowerP1 = new Vector3(targetX, targetY, z);

        // 2. HOB DOWNWARD ARM (LOWER P2)
        float currentBoxHeight = hobY - keelY;
        float hobDrop = currentBoxHeight * bodyPlan.hobLowerArmLength;
        fp.lowerP2 = new Vector3(hobBreadth, Mathf.Max(hobY - hobDrop, targetY), z);

        // 3. INNER CONTROLS (Quintic / Quartic)
        float ribbandRatio = 0f;
        if (masterL1.x > 0.001f)
        {
            ribbandRatio = Mathf.Clamp01(1f - (targetX / masterL1.x));
        }
        
        float dynamicInnerKeel = Mathf.Lerp(bodyPlan.innerKeelPosition, 1.0f, ribbandRatio);
        float dynamicMiddlePosition = Mathf.Lerp(bodyPlan.middleControlPosition, 0.0f, ribbandRatio);
        
        fp.lowerP1_inner = Vector3.Lerp(fp.lowerP0, fp.lowerP1, dynamicInnerKeel);
        fp.lowerPMid = Vector3.Lerp(fp.lowerP1, fp.lowerP2, dynamicMiddlePosition);
        }
        else
        {
            fp.lowerP3 = new Vector3(hobBreadth, hobY, z);
        }

        // UPPER HULL
        float topTimberY = sheerPlan.GetTopTimberYAtZ(z);
        float topTimberBreadth = breadthPlan.GetTopTimberXAtZ(z);
        
        float minZ = sheerPlan.cachedTopTimberSternIntersect.z;
        float maxZ = sheerPlan.cachedTopTimberBowIntersect.z;

        // Added 0.005f tolerance to ensure exact bow/stern slices aren't skipped by floating point drift
        if (topTimberY > hobY + 0.01f && z >= minZ - 0.005f && z <= maxZ + 0.005f)
        {
            fp.hasUpper = true;
            
            // GLOBAL MAX SCALING PARADIGM
            // Scale the Upper Mould to the highest possible sheer curve at this Z to preserve true curvature while following the sheer
            float maxSheerYAtZ = sheerPlan.GetBaseDeckYAtZ(z);
            if (sheerPlan.upperDecks != null && sheerPlan.upperDecks.Count > 0) 
            {
                maxSheerYAtZ += sheerPlan.GetMaxDeckHeight() + sheerPlan.GetGlobalSheerOffsetAtZ(z);
            }
            else 
            {
                maxSheerYAtZ = sheerPlan.GetTopTimberYAtZ(z); // If no decks, it's just the base Top Timber sheer
            }
            
            float localTtY = sheerPlan.GetTopTimberYAtZ(z);
            float theoreticalHobY = sheerPlan.GetTheoreticalHobYAtZ(z);
            
            fp.upperP0 = fp.hasLower ? fp.lowerP3 : new Vector3(hobBreadth, theoreticalHobY, z);
            
            // upperP3 mathematically reaches the max sheer height, preserving sheer curve scaling
            fp.upperP3 = new Vector3(topTimberBreadth, maxSheerYAtZ, z);

            float scaleX = 1f;
            if (breadthPlan.topTimberMaxBeam > 0.01f) scaleX = topTimberBreadth / breadthPlan.topTimberMaxBeam;
            
            float masterHeight = bodyPlan.cachedU3.y - bodyPlan.cachedU0.y;
            float scaleY = 1f;
            if (masterHeight > 0.01f) scaleY = (maxSheerYAtZ - theoreticalHobY) / masterHeight;

            Vector3 u1Vector = bodyPlan.cachedU1 - bodyPlan.cachedU0;
            Vector3 u2Vector = bodyPlan.cachedU2 - bodyPlan.cachedU3;

            fp.upperP1 = fp.upperP0 + new Vector3(u1Vector.x * scaleX, u1Vector.y * scaleY, 0);
            fp.upperP2 = fp.upperP3 + new Vector3(u2Vector.x * scaleX, u2Vector.y * scaleY, 0);

            // CLIPPING LOGIC: Stop drawing the frame when it crosses the local Top Timber height (bulwark dropoff)
            float minT = 0f;
            float maxT = 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (minT + maxT) / 2f;
                float py = CalculateCubicBezierPoint(midT, fp.upperP0, fp.upperP1, fp.upperP2, fp.upperP3).y;
                if (py < localTtY) minT = midT;
                else maxT = midT;
            }
            fp.upperTopCutoffT = (minT + maxT) / 2f;
        }

        fp.isValid = true;
        return fp;
    }

    public virtual FrameProfile GetFrameProfile(float z)
    {
        FrameProfile fp = GetRawFrameProfile(z);
        if (!fp.isValid) return fp;

        float sternBoundaryZ = sheerPlan.cachedSternRisingEnd.z;
        bool hitsSternProfile = z < sternBoundaryZ;
        
        if (hitsSternProfile)
        {
            float hobY = sheerPlan.GetTheoreticalHobYAtZ(z);
            float fashionY = sheerPlan.GetSternProfileYAtZ(z);
            float fashionX = GetFashionLineXAtZ(z);
            
            if (fashionY > hobY || !fp.hasLower)
            {
                float originalUpperP1OffsetX = fp.upperP1.x - fp.upperP0.x;
                float dynamicMiddlePosition = 0f;
                if (fp.hasLower)
                {
                    float targetX = fp.lowerP1.x;
                    Vector3 masterL1 = bodyPlan.cachedL1;
                    float ribbandRatio = 0f;
                    if (masterL1.x > 0.001f) ribbandRatio = Mathf.Clamp01(1f - (targetX / masterL1.x));
                    dynamicMiddlePosition = Mathf.Lerp(bodyPlan.middleControlPosition, 0.0f, ribbandRatio);
                }

                float minPhantom = 0f;
                float maxPhantom = 20f;
                float finalT = 0f;

                for (int iter = 0; iter < 15; iter++)
                {
                    float testPhantom = (minPhantom + maxPhantom) / 2f;
                    
                    fp.upperP0.x = testPhantom;
                    fp.upperP1.x = testPhantom + originalUpperP1OffsetX;
                    if (fp.hasLower)
                    {
                        fp.lowerP3.x = testPhantom;
                        fp.lowerP2.x = testPhantom;
                        fp.lowerPMid = Vector3.Lerp(fp.lowerP1, fp.lowerP2, dynamicMiddlePosition);
                    }

                    float fMinT = 0f;
                    float fMaxT = fp.upperTopCutoffT; 
                    for (int i = 0; i < 15; i++)
                    {
                        float midT = (fMinT + fMaxT) / 2f;
                        float py = CalculateCubicBezierPoint(midT, fp.upperP0, fp.upperP1, fp.upperP2, fp.upperP3).y;
                        if (py < fashionY) fMinT = midT;
                        else fMaxT = midT;
                    }
                    finalT = (fMinT + fMaxT) / 2f;
                    float evalX = CalculateCubicBezierPoint(finalT, fp.upperP0, fp.upperP1, fp.upperP2, fp.upperP3).x;

                    if (evalX < fashionX) minPhantom = testPhantom;
                    else maxPhantom = testPhantom;
                }

                fp.upperCutoffT = finalT;
                fp.lowerCutoffT = 0f;
            }
            else
            {
                float fMinT = 0f;
                float fMaxT = 1f;
                for (int i = 0; i < 20; i++)
                {
                    float midT = (fMinT + fMaxT) / 2f;
                    float py = GetSurfacePointAtT(fp, 1f, midT).y;
                    if (py < fashionY) fMinT = midT;
                    else fMaxT = midT;
                }
                float finalT = (fMinT + fMaxT) / 2f;
                
                fp.lowerCutoffT = finalT;
                fp.upperCutoffT = 0f;
            }
        }
        
        return fp;
    }

    protected float GetFashionLineXAtZ(float z)
    {
        float fashionTimberLowerZ = sheerPlan.GetLowerAftMostZ();
        float upperAftZ = sheerPlan.cachedTransomTopPoint.z;

        float lowerTransomBreadth = breadthPlan.GetBreadthXAtZ(fashionTimberLowerZ);
        float upperTransomBreadth = breadthPlan.GetTopTimberXAtZ(upperAftZ);

        Vector3 p0 = new Vector3(lowerTransomBreadth, 0, fashionTimberLowerZ);
        Vector3 p3 = new Vector3(upperTransomBreadth, 0, upperAftZ);

        // Body Plan Bulges (X-Y mapped to X-Z)
        float masterHeight = bodyPlan.cachedU3.y - bodyPlan.cachedU0.y;
        float deltaZ = upperAftZ - fashionTimberLowerZ;
        float scaleZ = 1f;
        if (masterHeight > 0.01f) scaleZ = deltaZ / masterHeight;

        float scaleX = 1f;
        if (breadthPlan.topTimberMaxBeam > 0.01f) scaleX = upperTransomBreadth / breadthPlan.topTimberMaxBeam;

        Vector3 u1Vector = bodyPlan.cachedU1 - bodyPlan.cachedU0;
        Vector3 u2Vector = bodyPlan.cachedU2 - bodyPlan.cachedU3;

        // Tangents
        float T0 = 0f;
        float T3 = 0f;
        if (Mathf.Abs(deltaZ) > 0.001f)
        {
            float zForward0 = fashionTimberLowerZ + 0.01f;
            T0 = (breadthPlan.GetBreadthXAtZ(zForward0) - lowerTransomBreadth) / 0.01f;
            
            float zForward3 = upperAftZ + 0.01f;
            T3 = (breadthPlan.GetTopTimberXAtZ(zForward3) - upperTransomBreadth) / 0.01f;

            // Extreme Rake Fallback (Option A):
            // If raked aft, and the lower tangent projects further inward than the Top Timber breadth,
            // it will create an inverted hollow. Fallback to a straight baseline.
            if (deltaZ < -0.01f)
            {
                float projectedX = p0.x + T0 * deltaZ;
                if (projectedX < p3.x)
                {
                    float tStraight = (p3.x - p0.x) / deltaZ;
                    T0 = tStraight;
                    T3 = tStraight;
                }
            }
        }

        // Apply Option A: Baseline Relative
        float p1z = p0.z + u1Vector.y * scaleZ;
        float p1x_baseline = p0.x + T0 * (p1z - p0.z);
        Vector3 p1 = new Vector3(p1x_baseline + (u1Vector.x * scaleX), 0, p1z);

        float p2z = p3.z + u2Vector.y * scaleZ;
        float p2x_baseline = p3.x + T3 * (p2z - p3.z);
        Vector3 p2 = new Vector3(p2x_baseline + (u2Vector.x * scaleX), 0, p2z);

        // Find t for given z
        float minT = 0f;
        float maxT = 1f;
        for (int i = 0; i < 20; i++)
        {
            float midT = (minT + maxT) / 2f;
            float pz = CalculateCubicBezierPoint(midT, p0, p1, p2, p3).z;
            if (deltaZ < 0)
            {
                if (pz > z) minT = midT;
                else maxT = midT;
            }
            else
            {
                if (pz < z) minT = midT;
                else maxT = midT;
            }
        }
        float tFashion = (minT + maxT) / 2f;
        return CalculateCubicBezierPoint(tFashion, p0, p1, p2, p3).x;
    }

    private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        return (u * u * p0) + (2 * u * t * p1) + (t * t * p2);
    }

    private void OnDrawGizmos()
    {
        if (sheerPlan == null || breadthPlan == null || bodyPlan == null) return;
        
        sheerPlan.UpdateCachedValues();
        breadthPlan.UpdateCachedCurves();
        bodyPlan.UpdateCachedCurves();

        if (!showFrames && !showFloorSweepsGuide && !showSurfaceRibbands && !showWaterlines) return;

        // Draw Floor Sweeps Guide (Control Curves)
        if (showFloorSweepsGuide)
        {
            float hw = bodyPlan.keelWidth / 2f;
            Vector3 masterL1 = bodyPlan.cachedL1;
            Vector3 bowEnd = new Vector3(hw, sheerPlan.cachedBowRisingEnd.y, sheerPlan.cachedBowRisingEnd.z);
            Vector3 sternEnd = new Vector3(hw, sheerPlan.cachedSternRisingEnd.y, sheerPlan.cachedSternRisingEnd.z);

            Vector3 bowP1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedBowP1Ctrl.z);
            Vector3 sternP1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedSternP1Ctrl.z);

            // Starboard Ribband
            DrawQuadraticBezier3D(masterL1, sternP1, sternEnd, Color.yellow);
            DrawQuadraticBezier3D(masterL1, bowP1, bowEnd, Color.yellow);
            
            // Port Ribband
            Vector3 mMasterL1 = new Vector3(-masterL1.x, masterL1.y, masterL1.z);
            Vector3 mBowP1 = new Vector3(-bowP1.x, bowP1.y, bowP1.z);
            Vector3 mSternP1 = new Vector3(-sternP1.x, sternP1.y, sternP1.z);
            Vector3 mBowEnd = new Vector3(-hw, bowEnd.y, bowEnd.z);
            Vector3 mSternEnd = new Vector3(-hw, sternEnd.y, sternEnd.z);
            
            DrawQuadraticBezier3D(mMasterL1, mSternP1, mSternEnd, Color.yellow);
            DrawQuadraticBezier3D(mMasterL1, mBowP1, mBowEnd, Color.yellow);
        }

        // Draw HoB / Base Deck Ribband
        DrawHobRibband();
        
        // Draw Top Timber Ribband
        DrawTopTimberRibband();

        // Draw Surface Ribbands
        if (showSurfaceRibbands)
        {
            DrawSurfaceRibbands();
        }

        // Draw Waterlines
        if (showWaterlines)
        {
            DrawWaterlines();
        }

        // Draw the double line for the Keel / Stem / Sternpost width
        DrawKeelSpineWidth();

        // Draw Frames
        if (showFrames)
        {
            foreach (float z in GetFrameZPositions())
            {
                DrawFrameAtZ(z);
            }
            
            // Draw Fashion Timbers
            if (sheerPlan.enableCounter || Mathf.Abs(sheerPlan.cachedSternPostTopPoint.z - sheerPlan.cachedTransomTopPoint.z) > 0.01f)
            {
                DrawUpperFashionTimber();
                
                if (sheerPlan.enableCounter)
                {
                    DrawLowerFashionTimber();
                    
                    // Draw Red Dots where the Fashion Timber crosses the HoB line
                    Vector3 intersect = GetFashionTimberHobIntersection();
                    float trueHobX = breadthPlan.GetBreadthXAtZ(intersect.z);
                    
                    // Remove red spheres for clear view
                }
            }
        }
    }

    protected virtual void DrawKeelSpineWidth()
    {
        if (sheerPlan == null || bodyPlan == null) return;
        
        float hw = bodyPlan.keelWidth / 2f;
        Gizmos.color = Color.cyan; // High contrast for Unity background
        
        List<Vector3> innerPts;
        List<Vector3> outerPts;
        sheerPlan.GetKeelSpineProfiles(out innerPts, out outerPts);
        
        if (innerPts.Count == 0 || outerPts.Count == 0) return;

        // Draw longitudinal lines
        for (int i = 0; i < innerPts.Count; i++)
        {
            if (i > 0)
            {
                // Top Port and Top Stbd
                Gizmos.DrawLine(new Vector3(-hw, innerPts[i-1].y, innerPts[i-1].z), new Vector3(-hw, innerPts[i].y, innerPts[i].z));
                Gizmos.DrawLine(new Vector3(hw, innerPts[i-1].y, innerPts[i-1].z), new Vector3(hw, innerPts[i].y, innerPts[i].z));
                
                // Bottom Port and Bottom Stbd
                Gizmos.DrawLine(new Vector3(-hw, outerPts[i-1].y, outerPts[i-1].z), new Vector3(-hw, outerPts[i].y, outerPts[i].z));
                Gizmos.DrawLine(new Vector3(hw, outerPts[i-1].y, outerPts[i-1].z), new Vector3(hw, outerPts[i].y, outerPts[i].z));
            }
            
            // Draw cross-sections (connecting the 4 corners at each point)
            Vector3 inPort = new Vector3(-hw, innerPts[i].y, innerPts[i].z);
            Vector3 inStbd = new Vector3(hw, innerPts[i].y, innerPts[i].z);
            Vector3 outPort = new Vector3(-hw, outerPts[i].y, outerPts[i].z);
            Vector3 outStbd = new Vector3(hw, outerPts[i].y, outerPts[i].z);
            
            Gizmos.DrawLine(inPort, inStbd);
            Gizmos.DrawLine(outPort, outStbd);
            Gizmos.DrawLine(inPort, outPort);
            Gizmos.DrawLine(inStbd, outStbd);
        }


    }

    public virtual List<float> GetFrameZPositions()
    {
        List<float> positions = new List<float>();
        if (sheerPlan == null) return positions;

        float bowZ = sheerPlan.cachedStemTopPoint.z;
        float sternZ = sheerPlan.enableCounter ? sheerPlan.cachedSternRisingEnd.z : sheerPlan.cachedTransomTopPoint.z;
        float keelSternZ = sheerPlan.cachedKeelSternPoint.z;
        float keelBowZ = sheerPlan.cachedKeelBowPoint.z;

        for (float z = 0; z < bowZ;)
        {
            positions.Add(z);
            if (z >= keelBowZ) z += 0.25f;
            else z += 1f;
        }
        positions.Add(bowZ);

        for (float z = -1f; z > sternZ;)
        {
            positions.Add(z);
            if (z <= keelSternZ) z -= 0.25f;
            else z -= 1f;
        }
        positions.Add(sternZ);
        
        // Explicitly inject the structural keel corners so the mesh doesn't cut them off
        if (!positions.Contains(keelBowZ)) positions.Add(keelBowZ);
        if (!positions.Contains(keelSternZ)) positions.Add(keelSternZ);
        
        positions.Sort();

        if (sheerPlan.upperDecks != null)
        {
            float totalLength = sheerPlan.cachedTopTimberBowIntersect.z - sheerPlan.cachedTopTimberSternIntersect.z;
            float startZ = sheerPlan.cachedTopTimberSternIntersect.z;
            foreach (var deck in sheerPlan.upperDecks)
            {
                float aftZ = startZ + (deck.globalAftExtent * totalLength);
                float foreZ = startZ + (deck.globalForeExtent * totalLength);
                
                if (aftZ > sternZ && aftZ < bowZ)
                {
                    positions.Add(aftZ - 0.001f);
                    positions.Add(aftZ + 0.001f);
                }
                if (foreZ > sternZ && foreZ < bowZ)
                {
                    positions.Add(foreZ - 0.001f);
                    positions.Add(foreZ + 0.001f);
                }
            }
        }

        positions.Sort();
        return positions;
    }

    private void DrawHobRibband()
    {
        float startZ = sheerPlan.cachedHobSternIntersect.z;
        float endZ = sheerPlan.cachedStemTopPoint.z;
        int segments = 40;
        float step = (endZ - startZ) / segments;

        Gizmos.color = Color.white;
        Vector3? prevStbd = null;
        Vector3? prevPort = null;

        for (int i = 0; i <= segments; i++)
        {
            float z = startZ + (i * step);
            // Cap to exact extents to prevent precision overshoot
            if (i == segments) z = endZ;
            
            float x = breadthPlan.GetBreadthXAtZ(z);
            float y = sheerPlan.GetHobYAtZ(z);

            Vector3 currStbd = new Vector3(x, y, z);
            Vector3 currPort = new Vector3(-x, y, z);

            if (i == 0)
            {
                // Draw Transverse Transom Timber at the stern
                Gizmos.DrawLine(currStbd, currPort);
            }

            if (prevStbd.HasValue)
            {
                Gizmos.DrawLine(prevStbd.Value, currStbd);
                Gizmos.DrawLine(prevPort.Value, currPort);
            }

            prevStbd = currStbd;
            prevPort = currPort;
        }
    }

    private void DrawTopTimberRibband()
    {
        float startZ = sheerPlan.cachedTopTimberSternIntersect.z;
        float endZ = sheerPlan.cachedUpperStemTopPoint.z;
        int segments = 80; // Increased for better vertical resolution
        float step = (endZ - startZ) / segments;

        Gizmos.color = Color.cyan;
        Vector3? prevStbd = null;
        Vector3? prevPort = null;
        
        List<float> zSlices = new List<float>();
        for (int i = 0; i <= segments; i++)
        {
            float z = startZ + (i * step);
            if (i == segments) z = endZ;
            zSlices.Add(z);
        }

        if (sheerPlan.upperDecks != null)
        {
            float totalLength = sheerPlan.cachedTopTimberBowIntersect.z - startZ;
            float deckStartZ = startZ;

            // Removed inner plan check for Counter Test

            foreach (var deck in sheerPlan.upperDecks)
            {
                float aftZ = deckStartZ + (deck.globalAftExtent * totalLength);
                float foreZ = deckStartZ + (deck.globalForeExtent * totalLength);

                if (aftZ > startZ + 0.005f && aftZ < endZ - 0.005f)
                {
                    zSlices.Add(aftZ - 0.001f);
                    zSlices.Add(aftZ + 0.001f);
                }
                if (foreZ > startZ + 0.005f && foreZ < endZ - 0.005f)
                {
                    zSlices.Add(foreZ - 0.001f);
                    zSlices.Add(foreZ + 0.001f);
                }
            }
        }

        if (sheerPlan.enableBeakheadBulwark && sheerPlan.curveBeakheadBulwark)
        {
            float bowZ = sheerPlan.cachedTopTimberBowIntersect.z;
            float sternZ = sheerPlan.cachedTopTimberSternIntersect.z;
            float startDropZ = bowZ - (sheerPlan.beakheadBulwarkStartPercent * (bowZ - sternZ));
            
            // Add 20 high-resolution slices specifically for the curved drop
            for (int i = 1; i <= 20; i++)
            {
                zSlices.Add(Mathf.Lerp(startDropZ, bowZ, i / 20f));
            }
        }

        zSlices.Sort();

        for (int i = 0; i < zSlices.Count; i++)
        {
            float z = zSlices[i];
            FrameProfile fp = GetFrameProfile(z);
            Vector3 currStbd;
            Vector3 currPort;

            if (fp.isValid)
            {
                float localTtY = sheerPlan.GetTopTimberYAtZ(z);
                if (fp.hasUpper && localTtY > fp.upperP0.y)
                {
                    float tCutoff = fp.upperTopCutoffT;
                    currStbd = GetUpperSurfacePointAtT(fp, 1f, tCutoff);
                    currPort = GetUpperSurfacePointAtT(fp, -1f, tCutoff);
                }
                else if (fp.hasLower)
                {
                    // The Beakhead Bulwark has dropped BELOW the Height of Breadth (into the lower frame)
                    // We must find the T on the lower frame that matches localTtY!
                    float minT = 0f;
                    float maxT = 1f;
                    for (int j = 0; j < 20; j++)
                    {
                        float midT = (minT + maxT) / 2f;
                        float py = GetSurfacePointAtT(fp, 1f, midT).y;
                        
                        if (py < localTtY) minT = midT;
                        else maxT = midT;
                    }
                    float lowerT = (minT + maxT) / 2f;
                    currStbd = GetSurfacePointAtT(fp, 1f, lowerT);
                    currPort = GetSurfacePointAtT(fp, -1f, lowerT);
                }
                else
                {
                    // Complete fallback
                    float x = breadthPlan.GetTopTimberXAtZ(z);
                    currStbd = new Vector3(x, localTtY, z);
                    currPort = new Vector3(-x, localTtY, z);
                }
            }
            else
            {
                float x = breadthPlan.GetTopTimberXAtZ(z);
                float y = sheerPlan.GetTopTimberYAtZ(z);
                currStbd = new Vector3(x, y, z);
                currPort = new Vector3(-x, y, z);
            }

            if (i == 0)
            {
                // Draw Transverse Top Timber Transom at the stern
                Gizmos.DrawLine(currStbd, currPort);
            }

            if (prevStbd.HasValue)
            {
                Gizmos.DrawLine(prevStbd.Value, currStbd);
                Gizmos.DrawLine(prevPort.Value, currPort);
            }

            prevStbd = currStbd;
            prevPort = currPort;
        }
    }

    private void DrawSurfaceRibbands()
    {
        float startZ = sheerPlan.GetLowerAftMostZ();
        float endZ = sheerPlan.cachedStemTopPoint.z;
        int segments = 40; // Reduced density to save Handles rendering performance
        float step = (endZ - startZ) / segments;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f); // Orange for surface ribbands
#if UNITY_EDITOR
        Handles.color = new Color(1f, 0.5f, 0f, 0.8f);
#endif

        Vector3? prevStbdT25 = null;
        Vector3? prevStbdT50 = null;
        Vector3? prevStbdT75 = null;
        Vector3? prevPortT25 = null;
        Vector3? prevPortT50 = null;
        Vector3? prevPortT75 = null;

        for (int i = 0; i <= segments; i++)
        {
            float z = startZ + (i * step);
            if (i == segments) z = endZ;

            FrameProfile fp = GetFrameProfile(z);
            if (!fp.isValid) continue;

            // Evaluate surface points at t=0.25, 0.5, 0.75
            Vector3 stbd25 = GetSurfacePointAtT(fp, 1f, 0.25f);
            Vector3 stbd50 = GetSurfacePointAtT(fp, 1f, 0.50f);
            Vector3 stbd75 = GetSurfacePointAtT(fp, 1f, 0.75f);

            Vector3 port25 = GetSurfacePointAtT(fp, -1f, 0.25f);
            Vector3 port50 = GetSurfacePointAtT(fp, -1f, 0.50f);
            Vector3 port75 = GetSurfacePointAtT(fp, -1f, 0.75f);

            if (prevStbdT25.HasValue)
            {
#if UNITY_EDITOR
                Handles.DrawDottedLine(prevStbdT25.Value, stbd25, 4f);
                Handles.DrawDottedLine(prevStbdT50.Value, stbd50, 4f);
                Handles.DrawDottedLine(prevStbdT75.Value, stbd75, 4f);

                Handles.DrawDottedLine(prevPortT25.Value, port25, 4f);
                Handles.DrawDottedLine(prevPortT50.Value, port50, 4f);
                Handles.DrawDottedLine(prevPortT75.Value, port75, 4f);
#else
                Gizmos.DrawLine(prevStbdT25.Value, stbd25);
                Gizmos.DrawLine(prevStbdT50.Value, stbd50);
                Gizmos.DrawLine(prevStbdT75.Value, stbd75);

                Gizmos.DrawLine(prevPortT25.Value, port25);
                Gizmos.DrawLine(prevPortT50.Value, port50);
                Gizmos.DrawLine(prevPortT75.Value, port75);
#endif
            }

            prevStbdT25 = stbd25;
            prevStbdT50 = stbd50;
            prevStbdT75 = stbd75;
            prevPortT25 = port25;
            prevPortT50 = port50;
            prevPortT75 = port75;
        }
    }

    private void DrawWaterlines()
    {
        float startZ = sheerPlan.GetLowerAftMostZ();
        float endZ = sheerPlan.cachedStemTopPoint.z;
        float minY = Mathf.Min(sheerPlan.cachedMidshipKeel.y, 0f);
        float maxY = sheerPlan.cachedBaseDeckMidshipPoint.y;
        
        int segments = 40; // Reduced from 80 for performance
        float step = (endZ - startZ) / segments;

        // Precompute slices to save binary search operations
        FrameProfile[] profiles = new FrameProfile[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float z = startZ + (i * step);
            if (i == segments) z = endZ;
            profiles[i] = GetFrameProfile(z);
        }

        Gizmos.color = Color.blue;

        for (float y = Mathf.Ceil(minY); y <= maxY; y += 1f)
        {
            Vector3? prevStbd = null;
            Vector3? prevPort = null;
            
            // Exact stern profile connection
            float exactSternZ = sheerPlan.GetSternProfileZAtY(y);
            float hw = bodyPlan.keelWidth / 2f;
            prevStbd = new Vector3(hw, y, exactSternZ);
            prevPort = new Vector3(-hw, y, exactSternZ);

            for (int i = 0; i <= segments; i++)
            {
                if (!profiles[i].isValid) continue;

                Vector3? currStbd = GetSurfacePointAtY(profiles[i], 1f, y);
                Vector3? currPort = GetSurfacePointAtY(profiles[i], -1f, y);

                if (currStbd.HasValue && prevStbd.HasValue)
                {
                    Gizmos.DrawLine(prevStbd.Value, currStbd.Value);
                    Gizmos.DrawLine(prevPort.Value, currPort.Value);
                }

                if (currStbd.HasValue) prevStbd = currStbd;
                if (currPort.HasValue) prevPort = currPort;
            }
            
            // Exact bow profile connection
            float exactStemZ = sheerPlan.GetStemProfileZAtY(y);
            hw = bodyPlan.keelWidth / 2f;
            Vector3 stemStbd = new Vector3(hw, y, exactStemZ);
            Vector3 stemPort = new Vector3(-hw, y, exactStemZ);
            if (prevStbd.HasValue)
            {
                Gizmos.DrawLine(prevStbd.Value, stemStbd);
                Gizmos.DrawLine(prevPort.Value, stemPort);
            }
        }
    }

    private float CalculateCubicBezierY(float t, float y0, float y1, float y2, float y3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        return (uuu * y0) + (3 * uu * t * y1) + (3 * u * tt * y2) + (ttt * y3);
    }

    public Vector3? GetSurfacePointAtY(FrameProfile fp, float flipX, float targetY)
    {
        if (targetY < fp.lowerP0.y) return null;

        if (fp.hasUpper && targetY > fp.lowerP3.y && targetY <= fp.upperP3.y)
        {
            float min = 0f;
            float max = 1f;
            for (int i = 0; i < 20; i++)
            {
                float mid = (min + max) / 2f;
                float py = CalculateCubicBezierY(mid, fp.upperP0.y, fp.upperP1.y, fp.upperP2.y, fp.upperP3.y);
                if (py < targetY) min = mid;
                else max = mid;
            }
            return GetUpperSurfacePointAtT(fp, flipX, (min + max) / 2f);
        }

        if (targetY > fp.lowerP3.y) return null;

        float minL = 0f;
        float maxL = 1f;
        for (int i = 0; i < 20; i++)
        {
            float mid = (minL + maxL) / 2f;
            // Full evaluation still required for lower mould if quintic/quartic are used
            Vector3 pt = GetSurfacePointAtT(fp, flipX, mid);
            if (pt.y < targetY) minL = mid;
            else maxL = mid;
        }
        return GetSurfacePointAtT(fp, flipX, (minL + maxL) / 2f);
    }



    public Vector3 GetUpperSurfacePointAtT(FrameProfile fp, float flipX, float t)
    {
        Vector3 u0 = new Vector3(fp.upperP0.x * flipX, fp.upperP0.y, fp.upperP0.z);
        Vector3 u1 = new Vector3(fp.upperP1.x * flipX, fp.upperP1.y, fp.upperP1.z);
        Vector3 u2 = new Vector3(fp.upperP2.x * flipX, fp.upperP2.y, fp.upperP2.z);
        Vector3 u3 = new Vector3(fp.upperP3.x * flipX, fp.upperP3.y, fp.upperP3.z);
        return CalculateCubicBezierPoint(t, u0, u1, u2, u3);
    }

    public Vector3 GetSurfacePointAtT(FrameProfile fp, float flipX, float t)
    {
        Vector3 p0 = new Vector3(fp.lowerP0.x * flipX, fp.lowerP0.y, fp.lowerP0.z);
        Vector3 p1Inner = new Vector3(fp.lowerP1_inner.x * flipX, fp.lowerP1_inner.y, fp.lowerP1_inner.z);
        Vector3 p1 = new Vector3(fp.lowerP1.x * flipX, fp.lowerP1.y, fp.lowerP1.z);
        Vector3 pMid = new Vector3(fp.lowerPMid.x * flipX, fp.lowerPMid.y, fp.lowerPMid.z);
        Vector3 p2 = new Vector3(fp.lowerP2.x * flipX, fp.lowerP2.y, fp.lowerP2.z);
        Vector3 p3 = new Vector3(fp.lowerP3.x * flipX, fp.lowerP3.y, fp.lowerP3.z);

        if (bodyPlan.useQuinticBezier)
            return CalculateQuinticBezierPoint(t, p0, p1Inner, p1, pMid, p2, p3);
        else if (bodyPlan.useQuarticBezier)
            return CalculateQuarticBezierPoint(t, p0, p1, pMid, p2, p3);
        else
            return CalculateCubicBezierPoint(t, p0, p1, p2, p3);
    }

#if UNITY_EDITOR
    private static GUIStyle _labelStyle;
#endif

    private void DrawFrameAtZ(float z)
    {
        FrameProfile fp = GetFrameProfile(z);
        if (!fp.isValid) return;

        Gizmos.color = frameColor;

        // Draw Starboard
        DrawMould(fp, 1f);

        // Draw Port (Mirrored)
        DrawMould(fp, -1f);
        
#if UNITY_EDITOR
        if (showFrameLabels)
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle();
                _labelStyle.alignment = TextAnchor.MiddleCenter;
            }
            _labelStyle.normal.textColor = frameColor;
            
            float labelY = fp.hasUpper ? fp.upperP3.y : fp.lowerP3.y;
            Vector3 labelPos = new Vector3(0, labelY + 0.5f, z);
            string labelText = z == 0 ? "Master" : z > 0 ? $"F{z}" : $"A{Mathf.Abs(z)}";
            Handles.Label(labelPos, labelText, _labelStyle);
        }
#endif
    }

    private void DrawMould(FrameProfile fp, float flipX)
    {
        if (fp.hasLower)
        {
            float startT = fp.lowerCutoffT;
            float endT = 1f;
            
            int segments = 20;
            Vector3 prev = GetSurfacePointAtT(fp, flipX, startT);
            for (int i = 1; i <= segments; i++)
            {
                float t = startT + (endT - startT) * (i / (float)segments);
                Vector3 curr = GetSurfacePointAtT(fp, flipX, t);
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }

            if (showFrameControlArms)
            {
                Gizmos.color = new Color(frameColor.r, frameColor.g, frameColor.b, 0.3f);
                Vector3 p0 = new Vector3(fp.lowerP0.x * flipX, fp.lowerP0.y, fp.lowerP0.z);
                Vector3 p1Inner = new Vector3(fp.lowerP1_inner.x * flipX, fp.lowerP1_inner.y, fp.lowerP1_inner.z);
                Vector3 p1 = new Vector3(fp.lowerP1.x * flipX, fp.lowerP1.y, fp.lowerP1.z);
                Vector3 pMid = new Vector3(fp.lowerPMid.x * flipX, fp.lowerPMid.y, fp.lowerPMid.z);
                Vector3 p2 = new Vector3(fp.lowerP2.x * flipX, fp.lowerP2.y, fp.lowerP2.z);
                Vector3 p3 = new Vector3(fp.lowerP3.x * flipX, fp.lowerP3.y, fp.lowerP3.z);
                
                if (bodyPlan.useQuinticBezier)
                {
                    Gizmos.DrawLine(p0, p1Inner);
                    Gizmos.DrawLine(p1Inner, p1);
                }
                else
                {
                    Gizmos.DrawLine(p0, p1);
                }
                Gizmos.DrawLine(p1, pMid);
                Gizmos.DrawLine(pMid, p2);
                Gizmos.DrawLine(p2, p3);
            }
        }

        if (fp.hasUpper)
        {
            Gizmos.color = frameColor;
            Vector3 u0 = new Vector3(fp.upperP0.x * flipX, fp.upperP0.y, fp.upperP0.z);
            Vector3 u1 = new Vector3(fp.upperP1.x * flipX, fp.upperP1.y, fp.upperP1.z);
            Vector3 u2 = new Vector3(fp.upperP2.x * flipX, fp.upperP2.y, fp.upperP2.z);
            Vector3 u3 = new Vector3(fp.upperP3.x * flipX, fp.upperP3.y, fp.upperP3.z);
            
            float startT = fp.upperCutoffT;
            float endT = fp.upperTopCutoffT;
            
            Vector3 prev = CalculateCubicBezierPoint(startT, u0, u1, u2, u3);
            int segments = 20;
            for (int i = 1; i <= segments; i++)
            {
                float t = startT + (endT - startT) * (i / (float)segments);
                Vector3 curr = CalculateCubicBezierPoint(t, u0, u1, u2, u3);
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }
            
            if (startT > 0.001f && flipX > 0)
            {
                Vector3 stbdCutoff = CalculateCubicBezierPoint(startT, u0, u1, u2, u3);
                Vector3 portCutoff = new Vector3(-stbdCutoff.x, stbdCutoff.y, stbdCutoff.z);
                Gizmos.DrawLine(stbdCutoff, portCutoff);
            }
            
            if (showFrameControlArms)
            {
                Gizmos.color = new Color(frameColor.r, frameColor.g, frameColor.b, 0.3f);
                Gizmos.DrawLine(u0, u1);
                Gizmos.DrawLine(u3, u2);
                
                Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                float radius = 0.05f;
                Gizmos.DrawSphere(u1, radius);
                Gizmos.DrawSphere(u2, radius);
            }
        }
    }

    private Vector3 GetFashionTimberHobIntersection()
    {
        float startZ = sheerPlan.enableCounter ? sheerPlan.cachedCounterBasePoint.z : sheerPlan.cachedSternRisingEnd.z;
        float startY = sheerPlan.enableCounter ? sheerPlan.cachedCounterBasePoint.y : sheerPlan.cachedSternRisingEnd.y;
        float startHobY = sheerPlan.GetTheoreticalHobYAtZ(startZ);
        
        if (startY >= startHobY)
        {
            return new Vector3(GetNaturalHullXAtYZ(startY, startZ), startY, startZ);
        }
        
        float endZ = sheerPlan.cachedTransomTopPoint.z;
        
        float minZ = Mathf.Min(startZ, endZ);
        float maxZ = Mathf.Max(startZ, endZ);
        
        for (int i = 0; i < 25; i++)
        {
            float midZ = (minZ + maxZ) / 2f;
            float fY = sheerPlan.GetSternProfileYAtZ(midZ);
            float hY = sheerPlan.GetTheoreticalHobYAtZ(midZ);
            
            // As we go aft (more negative Z), stern profile rises relative to HoB
            if (fY < hY) maxZ = midZ;
            else minZ = midZ;
        }
        
        float intersectZ = (minZ + maxZ) / 2f;
        float intersectY = sheerPlan.GetSternProfileYAtZ(intersectZ);
        return new Vector3(GetNaturalHullXAtYZ(intersectY, intersectZ), intersectY, intersectZ);
    }

    private (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) GetUpperFashionTimberBezier()
    {
        Vector3 intersect = GetFashionTimberHobIntersection();
        float fashionBaseZ = intersect.z;
        float fashionBaseY = intersect.y;
        
        // Pin the base width directly to the true HoB intersection width (where the red spheres are)
        float lowerTransomBreadth = GetNaturalHullXAtYZ(intersect.y, intersect.z);
        float upperAftZ = sheerPlan.cachedTransomTopPoint.z;
        float upperAftY = sheerPlan.cachedTransomTopPoint.y;

        float upperTransomBreadth = breadthPlan.GetTopTimberXAtZ(upperAftZ);

        Vector3 p0 = new Vector3(lowerTransomBreadth, fashionBaseY, fashionBaseZ);
        Vector3 p3 = new Vector3(upperTransomBreadth, upperAftY, upperAftZ);

        // Body Plan Bulges (X-Y)
        float masterHeight = bodyPlan.cachedU3.y - bodyPlan.cachedU0.y;
        float deltaY = upperAftY - fashionBaseY;
        float scaleY = 1f;
        if (masterHeight > 0.01f) scaleY = deltaY / masterHeight;
        
        float deltaZ = upperAftZ - fashionBaseZ;
        float scaleZ = 1f;
        if (masterHeight > 0.01f) scaleZ = deltaZ / masterHeight;

        float scaleX = 1f;
        if (breadthPlan.topTimberMaxBeam > 0.01f) scaleX = upperTransomBreadth / breadthPlan.topTimberMaxBeam;

        Vector3 u1Vector = bodyPlan.cachedU1 - bodyPlan.cachedU0;
        Vector3 u2Vector = bodyPlan.cachedU2 - bodyPlan.cachedU3; // Note: u2Vector.y is negative since U2 is below U3

        // Tangents: Align with the Primary Breadth Plan's aft lines
        float T0 = 0f;
        float T3 = 0f;
        if (Mathf.Abs(deltaZ) > 0.001f)
        {
            float zForward0 = fashionBaseZ + 0.01f;
            T0 = (breadthPlan.GetBreadthXAtZ(zForward0) - lowerTransomBreadth) / 0.01f;
            
            float zForward3 = upperAftZ + 0.01f;
            T3 = (breadthPlan.GetTopTimberXAtZ(zForward3) - upperTransomBreadth) / 0.01f;

            // Extreme Rake Fallback: if the tangent is too aggressive for the distance, fall back to a straight taper to prevent inward caving
            if (deltaZ < -0.01f)
            {
                float projectedX = p0.x + T0 * deltaZ;
                if (projectedX < p3.x)
                {
                    float tStraight = (p3.x - p0.x) / deltaZ;
                    T0 = tStraight;
                    T3 = tStraight;
                }
            }
        }

        // Apply Option A: True 3D Relative
        float p1z = p0.z + u1Vector.y * scaleZ;
        float p1x_baseline = p0.x + T0 * (p1z - p0.z);
        Vector3 p1 = new Vector3(p1x_baseline + (u1Vector.x * scaleX), p0.y + (u1Vector.y * scaleY), p1z);

        float p2z = p3.z + u2Vector.y * scaleZ;
        float p2x_baseline = p3.x + T3 * (p2z - p3.z);
        Vector3 p2 = new Vector3(p2x_baseline + (u2Vector.x * scaleX), p3.y + (u2Vector.y * scaleY), p2z);

        return (p0, p1, p2, p3);
    }

    private float GetFashionTimberTAtZ(float targetZ, (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) bezier)
    {
        float min = 0f;
        float max = 1f;
        for (int i = 0; i < 20; i++)
        {
            float mid = (min + max) / 2f;
            float z = CalculateCubicBezierPoint(mid, bezier.p0, bezier.p1, bezier.p2, bezier.p3).z;
            
            if (bezier.p3.z < bezier.p0.z)
            {
                if (z > targetZ) min = mid;
                else max = mid;
            }
            else
            {
                if (z < targetZ) min = mid;
                else max = mid;
            }
        }
        return (min + max) / 2f;
    }

    public float GetNaturalHullXAtYZ(float targetY, float originalZ)
    {
        float lowerAftZ = sheerPlan.cachedSternRisingEnd.z;
        float z = originalZ;
        
        // Clamp to Stern Post to prevent the Rising Line and lower moulds from collapsing to the centerline
        if (z < lowerAftZ) z = lowerAftZ;

        // Re-generate the raw frame
        FrameProfile fp = GetRawFrameProfile(z);
        if (!fp.isValid) return 0f;

        float mappedTargetY = targetY;
        
        // Remap Y by shifting it down by the HoB difference, so that the Stern Post frame perfectly aligns with the 3D HoB line!
        if (originalZ < lowerAftZ)
        {
            float origHobY = sheerPlan.GetTheoreticalHobYAtZ(originalZ);
            float sternHobY = sheerPlan.GetTheoreticalHobYAtZ(lowerAftZ);
            
            float hobDifference = origHobY - sternHobY;
            mappedTargetY = targetY - hobDifference;
        }

        float exactX = 0f;

        if (fp.hasUpper && mappedTargetY > fp.upperP0.y)
        {
            float fMinT = 0f;
            float fMaxT = 1f; 
            for (int i = 0; i < 20; i++)
            {
                float midT = (fMinT + fMaxT) / 2f;
                float py = CalculateCubicBezierPoint(midT, fp.upperP0, fp.upperP1, fp.upperP2, fp.upperP3).y;
                if (py < mappedTargetY) fMinT = midT;
                else fMaxT = midT;
            }
            exactX = CalculateCubicBezierPoint((fMinT + fMaxT) / 2f, fp.upperP0, fp.upperP1, fp.upperP2, fp.upperP3).x;
        }
        else if (fp.hasLower)
        {
            float fMinT = 0f;
            float fMaxT = 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (fMinT + fMaxT) / 2f;
                float py = GetSurfacePointAtT(fp, 1f, midT).y;
                if (py < mappedTargetY) fMinT = midT;
                else fMaxT = midT;
            }
            exactX = GetSurfacePointAtT(fp, 1f, (fMinT + fMaxT) / 2f).x;
        }

        // Linearly scale the width to match the tapering 3D HoB curve!
        if (originalZ < lowerAftZ)
        {
            float sternHobX = breadthPlan.GetBreadthXAtZ(lowerAftZ);
            float currHobX = breadthPlan.GetBreadthXAtZ(originalZ);
            if (sternHobX > 0.001f)
            {
                exactX *= (currHobX / sternHobX);
            }
        }

        return exactX;
    }

    private float GetUpperFashionTimberXAtZ(float z)
    {
        var bezier = GetUpperFashionTimberBezier();
        float t = GetFashionTimberTAtZ(z, bezier);
        return CalculateCubicBezierPoint(t, bezier.p0, bezier.p1, bezier.p2, bezier.p3).x;
    }

    private void DrawUpperFashionTimber()
    {
        float lowerAftZ = sheerPlan.GetLowerAftMostZ();
        float upperAftZ = sheerPlan.cachedTransomTopPoint.z;
        if (Mathf.Abs(lowerAftZ - upperAftZ) < 0.01f && sheerPlan.cachedTransomTopPoint.y < sheerPlan.cachedBaseDeckSternIntersect.y + 0.01f) return;

        float startZ = sheerPlan.enableCounter ? sheerPlan.cachedCounterTopPoint.z : sheerPlan.cachedSternPostTopPoint.z;
        float endZ = sheerPlan.cachedTransomTopPoint.z;
        bool isVertical = Mathf.Abs(startZ - endZ) < 0.001f;
        
        float startY = sheerPlan.enableCounter ? sheerPlan.cachedCounterTopPoint.y : sheerPlan.cachedSternPostTopPoint.y;
        float endY = sheerPlan.cachedTransomTopPoint.y;
        
        Vector3 prevStbd = new Vector3(-1, -1, 1);
        Vector3 prevPort = new Vector3(-1, -1, 1);
        bool first = true;
        
        for (int i = 0; i <= 50; i++)
        {
            float fraction = i / 50f;
            float currZ = isVertical ? startZ : Mathf.Lerp(startZ, endZ, fraction);
            float exactY = isVertical ? Mathf.Lerp(startY, endY, fraction) : sheerPlan.GetSternProfileYAtZ(currZ);
            float hobY = sheerPlan.GetTheoreticalHobYAtZ(currZ);
            
            float exactX = exactY <= hobY ? GetNaturalHullXAtYZ(exactY, currZ) : GetUpperFashionTimberXAtZ(currZ);
            
            Vector3 currStbd = new Vector3(exactX, exactY, currZ);
            Vector3 currPort = new Vector3(-exactX, exactY, currZ);
            
            Gizmos.color = exactY <= hobY ? Color.red : Color.green;
            
            if (!first)
            {
                Gizmos.DrawLine(prevStbd, currStbd);
                Gizmos.DrawLine(prevPort, currPort);
            }
            
            prevStbd = currStbd;
            prevPort = currPort;
            first = false;
        }
        
#if UNITY_EDITOR
        if (showFrameLabels)
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle();
                _labelStyle.alignment = TextAnchor.MiddleCenter;
            }
            _labelStyle.normal.textColor = frameColor;
            
            float upperTransomY = sheerPlan.cachedTransomTopPoint.y;
            Vector3 labelPos = new Vector3(0, upperTransomY + 0.5f, upperAftZ);
            Handles.Label(labelPos, "Upper Fashion Timber", _labelStyle);
        }
#endif
    }

    private void DrawLowerFashionTimber()
    {
        float startZ = sheerPlan.cachedSternRisingEnd.z;
        float endZ = sheerPlan.cachedCounterTopPoint.z;
        bool isVertical = Mathf.Abs(startZ - endZ) < 0.001f;
        
        float startY = sheerPlan.cachedSternRisingEnd.y;
        float endY = sheerPlan.cachedCounterTopPoint.y;
        
        Vector3 prevStbd = new Vector3(-1, -1, 1);
        Vector3 prevPort = new Vector3(-1, -1, 1);
        bool first = true;
        
        for (int i = 0; i <= 50; i++)
        {
            float fraction = i / 50f;
            float currZ = isVertical ? startZ : Mathf.Lerp(startZ, endZ, fraction);
            float exactY = isVertical ? Mathf.Lerp(startY, endY, fraction) : sheerPlan.GetSternProfileYAtZ(currZ);
            float hobY = sheerPlan.GetTheoreticalHobYAtZ(currZ);
            
            float exactX = exactY <= hobY ? GetNaturalHullXAtYZ(exactY, currZ) : GetUpperFashionTimberXAtZ(currZ);
            
            Vector3 currStbd = new Vector3(exactX, exactY, currZ);
            Vector3 currPort = new Vector3(-exactX, exactY, currZ);
            
            Gizmos.color = exactY <= hobY ? Color.red : Color.green;
            
            if (!first)
            {
                Gizmos.DrawLine(prevStbd, currStbd);
                Gizmos.DrawLine(prevPort, currPort);
            }
            
            prevStbd = currStbd;
            prevPort = currPort;
            first = false;
        }
        
#if UNITY_EDITOR
        if (showFrameLabels)
        {
            float lowerTransomY = sheerPlan.cachedCounterTopPoint.y;
            Vector3 labelPos = new Vector3(0, lowerTransomY - 0.5f, sheerPlan.cachedCounterTopPoint.z);
            Handles.Label(labelPos, "Lower Fashion Timber", _labelStyle);
        }
#endif
    }

    /// <summary>
    /// Returns the true 3D outer points of the sweeping Fashion Timber.
    /// Used by the mesh generator to build the thick 3D structural rib.
    /// </summary>
    public List<Vector3> GetUpperFashionTimberPoints(bool isStbd, int segments = 20)
    {
        List<Vector3> pts = new List<Vector3>();
        
        float startZ = sheerPlan.enableCounter ? sheerPlan.cachedCounterTopPoint.z : sheerPlan.cachedSternPostTopPoint.z;
        float endZ = sheerPlan.cachedTransomTopPoint.z;
        bool isVertical = Mathf.Abs(startZ - endZ) < 0.001f;
        
        float startY = sheerPlan.enableCounter ? sheerPlan.cachedCounterTopPoint.y : sheerPlan.cachedSternPostTopPoint.y;
        float endY = sheerPlan.cachedTransomTopPoint.y;
        
        for (int i = 0; i <= segments; i++)
        {
            float fraction = i / (float)segments;
            float currZ = isVertical ? startZ : Mathf.Lerp(startZ, endZ, fraction);
            float exactY = isVertical ? Mathf.Lerp(startY, endY, fraction) : sheerPlan.GetSternProfileYAtZ(currZ);
            float hobY = sheerPlan.GetTheoreticalHobYAtZ(currZ);
            
            float exactX = exactY <= hobY ? GetNaturalHullXAtYZ(exactY, currZ) : GetUpperFashionTimberXAtZ(currZ);
            
            if (isStbd) pts.Add(new Vector3(exactX, exactY, currZ));
            else pts.Add(new Vector3(-exactX, exactY, currZ));
        }
        return pts;
    }

    public List<Vector3> GetLowerFashionTimberPoints(bool isStbd, int segments = 50)
    {
        List<Vector3> pts = new List<Vector3>();
        
        float startZ = sheerPlan.cachedSternRisingEnd.z;
        float endZ = sheerPlan.cachedCounterTopPoint.z;
        bool isVertical = Mathf.Abs(startZ - endZ) < 0.001f;
        
        float startY = sheerPlan.cachedSternRisingEnd.y;
        float endY = sheerPlan.cachedCounterTopPoint.y;
        
        for (int i = 0; i <= segments; i++)
        {
            float fraction = i / (float)segments;
            float currZ = isVertical ? startZ : Mathf.Lerp(startZ, endZ, fraction);
            float exactY = isVertical ? Mathf.Lerp(startY, endY, fraction) : sheerPlan.GetSternProfileYAtZ(currZ);
            float hobY = sheerPlan.GetTheoreticalHobYAtZ(currZ);
            
            float exactX = exactY <= hobY ? GetNaturalHullXAtYZ(exactY, currZ) : GetUpperFashionTimberXAtZ(currZ);
            
            if (isStbd) pts.Add(new Vector3(exactX, exactY, currZ));
            else pts.Add(new Vector3(-exactX, exactY, currZ));
        }
        return pts;
    }

    // --- BEZIER UTILITIES ---

    private void DrawQuadraticBezier3D(Vector3 p0, Vector3 p1, Vector3 p2, Color color, int segments = 20)
    {
        Gizmos.color = color;
        Vector3 prev = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float u = 1 - t;
            Vector3 p = (u * u * p0) + (2 * u * t * p1) + (t * t * p2);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    private void DrawCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int segments)
    {
        Vector3 prev = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    private void DrawQuarticBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, int segments)
    {
        Vector3 prev = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = CalculateQuarticBezierPoint(t, p0, p1, p2, p3, p4);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    private void DrawQuinticBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 p5, int segments)
    {
        Vector3 prev = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = CalculateQuinticBezierPoint(t, p0, p1, p2, p3, p4, p5);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    public static Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        return (u * u * u * p0) + (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t * p3);
    }

    private Vector3 CalculateQuarticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
    {
        float u = 1 - t;
        return (Mathf.Pow(u, 4) * p0) + 
               (4 * Mathf.Pow(u, 3) * t * p1) + 
               (6 * u * u * t * t * p2) + 
               (4 * u * Mathf.Pow(t, 3) * p3) + 
               (Mathf.Pow(t, 4) * p4);
    }

    private Vector3 CalculateQuinticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 p5)
    {
        float u = 1 - t;
        return (Mathf.Pow(u, 5) * p0) + 
               (5 * Mathf.Pow(u, 4) * t * p1) + 
               (10 * Mathf.Pow(u, 3) * t * t * p2) + 
               (10 * u * u * Mathf.Pow(t, 3) * p3) + 
               (5 * u * Mathf.Pow(t, 4) * p4) + 
               (Mathf.Pow(t, 5) * p5);
    }
}
