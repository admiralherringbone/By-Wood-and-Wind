using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class FramingModel : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the ModularBodyPlan.")]
    public BodyPlan bodyPlan;

    public enum FrameThicknessRatio
    {
        Full,
        Half,
        Quarter,
        Eighth
    }

    [Header("Frame Dimensions")]
    [Tooltip("The longitudinal distance between the centers of each frame (Z-axis).")]
    public float frameSpacing = 1.0f;
    [Tooltip("The thickness of each frame relative to the frame spacing.")]
    public FrameThicknessRatio frameThicknessRatio = FrameThicknessRatio.Half;
    [Tooltip("The inward depth of the frame from the moulded line (structural thickness).")]
    public float frameDepth = 0.5f;

    [Header("Gizmo Settings")]
    public Color frameColor = Color.cyan;
    [Tooltip("Reference to the ForeFaceFashionTimberModel.")]
    public ForeFaceFashionTimberModel foreFaceFashionTimberModel;

    [Header("Frame Visualization Settings")]
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
        public float lowerCutoffT; // LOWER cutoff for Lower Mould (e.g. for Counter / Fashion Timber)
        public float lowerTopCutoffT; // UPPER cutoff for Lower Mould (e.g. for Fashion Timber)
        public Vector3 upperP0, upperP1, upperP2, upperP3;
    }

    public virtual FrameProfile GetRawFrameProfile(float z, bool ignoreFashionTimber = false)
    {
        FrameProfile fp = new FrameProfile();
        fp.isValid = false;
        fp.hasLower = true;
        fp.hasUpper = false;
        fp.lowerCutoffT = 0f;
        fp.lowerTopCutoffT = 1f;

        if (sheerPlan == null || breadthPlan == null || bodyPlan == null) return fp;

        if (z < sheerPlan.cachedHobSternIntersect.z)
        {
            fp.hasLower = false;
        }

        // We no longer arbitrarily disable the lower mould aft of the Stern Post.
        // The Counter sliding system now handles clipping the lower moulds dynamically in GetFrameProfile.

        float hobBreadth = breadthPlan.GetBreadthXAtZ(z);
        float hobY = sheerPlan.GetHobYAtZ(z);
        
        if (fp.hasLower)
        {
            float keelY = sheerPlan.GetKeelYAtZ(z);
            if (sheerPlan.showDeadwoodRake && z < sheerPlan.cachedDeadwoodForePoint.z && z >= sheerPlan.cachedDeadwoodAftPoint.z)
            {
                keelY = sheerPlan.GetDeadwoodYAtZ(z);
            }
            
        float risingY = sheerPlan.GetRisingLineYAtZ(z);
        
        Vector3 fashionLowerBound = sheerPlan.cachedSternRisingEnd;
        if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
        {
            fashionLowerBound = sheerPlan.cachedDeadwoodAftPoint;
        }

        // If we are aft of the Fashion Lower Bound, the frame must extend down to the virtual Fashion Projection line
        // rather than stopping at the physical Stern Post/Counter backbone.
        if (z < fashionLowerBound.z)
        {
            float startHobY = sheerPlan.GetTheoreticalHobYAtZ(fashionLowerBound.z);
            float yOffset = startHobY - fashionLowerBound.y;
            keelY = sheerPlan.GetTheoreticalHobYAtZ(z) - yOffset;
        }
        
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
        
        float minZ = sheerPlan.cachedTransomTopPoint.z;
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
            
            float baseX = breadthPlan.GetTheoreticalBreadthXAtZ(z);

            fp.upperP0 = fp.hasLower ? fp.lowerP3 : new Vector3(baseX, theoreticalHobY, z);
            
            // upperP3 mathematically reaches the max sheer height, preserving sheer curve scaling
            fp.upperP3 = new Vector3(topTimberBreadth, maxSheerYAtZ, z);

            float scaleX = 1f;
            if (bodyPlan.cachedU0.x > 0.01f) scaleX = baseX / bodyPlan.cachedU0.x;
            
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

        if (foreFaceFashionTimberModel == null)
        {
            foreFaceFashionTimberModel = Object.FindObjectOfType<ForeFaceFashionTimberModel>();
        }

        float sternBoundaryZ = foreFaceFashionTimberModel != null ? foreFaceFashionTimberModel.GetMinZ() : sheerPlan.cachedSternRisingEnd.z;
        
        // Remove any frames that are past the stern boundary
        if (z < sternBoundaryZ - 0.001f)
        {
            fp.isValid = false;
            return fp;
        }

        float sternMaxZ = foreFaceFashionTimberModel != null ? foreFaceFashionTimberModel.GetMaxZ() : sheerPlan.cachedSternRisingEnd.z;
        bool hitsSternProfile = z < sternMaxZ;
        
        if (hitsSternProfile)
        {
            float hobY = sheerPlan.GetTheoreticalHobYAtZ(z);
            float fashionY = foreFaceFashionTimberModel != null ? foreFaceFashionTimberModel.GetInnerYAtZ(z) : sheerPlan.GetSternProfileYAtZ(z);
            
            // Removed Fashion Timber boundary override

            if (fashionY >= sheerPlan.GetTopTimberYAtZ(z))
            {
                fp.isValid = false;
                return fp;
            }
            
            if (fashionY > hobY || !fp.hasLower)
            {
                fp.hasLower = false;
                fp.lowerCutoffT = 0f;
                
                if (fp.hasUpper && fashionY > hobY)
                {
                    float fMinT = 0f;
                    float fMaxT = 1f;
                    for (int i = 0; i < 20; i++)
                    {
                        float midT = (fMinT + fMaxT) / 2f;
                        float py = GetUpperSurfacePointAtT(fp, 1f, midT).y;
                        if (py < fashionY) fMinT = midT;
                        else fMaxT = midT;
                    }
                    fp.upperCutoffT = (fMinT + fMaxT) / 2f;
                }
            }
            else
            {
                // Cut the bottom of the lower mould at the actual Lower Fashion Timber boundary (which follows the Stern Profile!)
                float tMin = 0f;
                float tMax = 1f;
                for (int i = 0; i < 20; i++)
                {
                    float midT = (tMin + tMax) / 2f;
                    float py = GetSurfacePointAtT(fp, 1f, midT).y;
                    if (py < fashionY) tMin = midT;
                    else tMax = midT;
                }
                fp.lowerCutoffT = (tMin + tMax) / 2f;
                
                fp.lowerTopCutoffT = 1f; // Top reaches the HoB
                fp.upperCutoffT = 0f;
            }
        }
        
        return fp;
    }

    // Removed GetFashionLineXAtZ

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

        if (foreFaceFashionTimberModel == null)
        {
            foreFaceFashionTimberModel = Object.FindObjectOfType<ForeFaceFashionTimberModel>();
        }

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
            
            // Fashion Timbers extracted to FashionTimberModel.cs
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

        // Draw Deadwood Rake (Top Surface)
        if (sheerPlan.showDeadwoodRake)
        {
            Gizmos.color = new Color(0.6f, 0.2f, 0.8f, 1f); // Purple to match Sheer Plan 2D line
            
            float dForeZ = sheerPlan.cachedDeadwoodForePoint.z;
            float dAftZ = sheerPlan.cachedDeadwoodAftPoint.z;
            
            int dSegments = 20;
            Vector3 prevPort = Vector3.zero;
            Vector3 prevStbd = Vector3.zero;
            
            for (int i = 0; i <= dSegments; i++)
            {
                float t = i / (float)dSegments;
                float z = Mathf.Lerp(dForeZ, dAftZ, t);
                float y = sheerPlan.GetDeadwoodYAtZ(z);
                
                Vector3 currPort = new Vector3(-hw, y, z);
                Vector3 currStbd = new Vector3(hw, y, z);
                
                // Draw cross-section at each segment
                Gizmos.DrawLine(currPort, currStbd);
                
                if (i > 0)
                {
                    Gizmos.DrawLine(prevPort, currPort);
                    Gizmos.DrawLine(prevStbd, currStbd);
                }
                
                prevPort = currPort;
                prevStbd = currStbd;
            }
        }
    }

    public virtual List<float> GetFrameZPositions()
    {
        List<float> positions = new List<float>();
        if (sheerPlan == null) return positions;

        if (foreFaceFashionTimberModel == null)
        {
            foreFaceFashionTimberModel = Object.FindObjectOfType<ForeFaceFashionTimberModel>();
        }

        if (foreFaceFashionTimberModel != null)
        {
            foreFaceFashionTimberModel.UpdateCachedPoints();
        }

        float bowZ = sheerPlan.cachedStemTopPoint.z;
        float sternBoundaryZ = foreFaceFashionTimberModel != null ? foreFaceFashionTimberModel.GetMinZ() : sheerPlan.cachedTransomTopPoint.z; // Extend transverse frames to the inner timber
        float keelSternZ = sheerPlan.cachedKeelSternPoint.z;
        float keelBowZ = sheerPlan.cachedKeelBowPoint.z;

        for (float z = 0; z <= bowZ;)
        {
            if (!positions.Contains(z)) positions.Add(z);
            z += frameSpacing;
        }

        for (float z = 0; z >= sternBoundaryZ;)
        {
            if (!positions.Contains(z)) positions.Add(z);
            z -= frameSpacing;
        }
        
        // Ensure the exact boundary frame is added if the loop missed it
        if (!positions.Contains(sternBoundaryZ))
        {
            positions.Add(sternBoundaryZ);
        }
        
        // Explicitly inject the structural keel corners so the mesh doesn't cut them off
        if (!positions.Contains(keelBowZ)) positions.Add(keelBowZ);
        
        float risingSternZ = sheerPlan.cachedSternRisingEnd.z;
        if (!positions.Contains(risingSternZ)) positions.Add(risingSternZ);
        
        positions.Sort();

        if (sheerPlan.upperDecks != null)
        {
            float totalLength = sheerPlan.cachedTopTimberBowIntersect.z - sheerPlan.cachedTopTimberSternIntersect.z;
            float startZ = sheerPlan.cachedTopTimberSternIntersect.z;
            foreach (var deck in sheerPlan.upperDecks)
            {
                float aftZ = startZ + (deck.globalAftExtent * totalLength);
                float foreZ = startZ + (deck.globalForeExtent * totalLength);
                
                if (aftZ > sternBoundaryZ && aftZ < bowZ)
                {
                    positions.Add(aftZ - 0.001f);
                    positions.Add(aftZ + 0.001f);
                }
                if (foreZ > sternBoundaryZ && foreZ < bowZ)
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

        // Draw Upper Phantom HoB Projection for Fashion Timber region (Dashed Light Grey)
        float phantomStartZ = sheerPlan.cachedHobSternIntersect.z;
        float phantomEndZ = sheerPlan.cachedTransomTopPoint.z;
        
        Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 1f); // Light Grey
        
        FashionTimberModel fModel = Object.FindObjectOfType<FashionTimberModel>();
        
        if (fModel != null && fModel.showFashionTimbers)
        {
            if (phantomEndZ < phantomStartZ)
            {
                int dashSegments = Mathf.CeilToInt(Mathf.Abs(phantomStartZ - phantomEndZ) / 0.1f);
                if (dashSegments % 2 == 0) dashSegments++; // Guarantee it ends on a solid dash
                float dashStep = (phantomEndZ - phantomStartZ) / dashSegments;
                
                for (int i = 0; i < dashSegments; i++)
                {
                    if (i % 2 != 0) continue; // Dashed effect
                    
                    float z1 = phantomStartZ + (i * dashStep);
                    float z2 = phantomStartZ + ((i + 1) * dashStep);
                    
                    float y1 = sheerPlan.GetTheoreticalHobYAtZ(z1);
                    float y2 = sheerPlan.GetTheoreticalHobYAtZ(z2);
                    
                    // Accurately project the mathematically extrapolated Primary Breadth Plan curve
                    float x1 = breadthPlan.GetTheoreticalBreadthXAtZ(z1);
                    float x2 = breadthPlan.GetTheoreticalBreadthXAtZ(z2);

                    Gizmos.DrawLine(new Vector3(x1, y1, z1), new Vector3(x2, y2, z2));
                    Gizmos.DrawLine(new Vector3(-x1, y1, z1), new Vector3(-x2, y2, z2));
                }
            }

            // Draw Lower Phantom HoB Projection (Dashed Light Grey)
            Vector3 fashionLowerBound = sheerPlan.cachedSternRisingEnd;
            if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
            {
                fashionLowerBound = sheerPlan.cachedDeadwoodAftPoint;
            }
            
            float lowerStartZ = fashionLowerBound.z;
            float lowerEndZ = sheerPlan.cachedHobSternIntersect.z;

            if (lowerEndZ < lowerStartZ)
            {
                int dashSegments = Mathf.CeilToInt(Mathf.Abs(lowerStartZ - lowerEndZ) / 0.1f);
                if (dashSegments % 2 == 0) dashSegments++; // Guarantee it ends on a solid dash
                float dashStep = (lowerEndZ - lowerStartZ) / dashSegments;
                
                float startHobY = sheerPlan.GetTheoreticalHobYAtZ(lowerStartZ);
                float yOffset = startHobY - fashionLowerBound.y;
                float hw = bodyPlan.keelWidth / 2f;

                for (int i = 0; i < dashSegments; i++)
                {
                    if (i % 2 != 0) continue; // Dashed effect
                    
                    float z1 = lowerStartZ + (i * dashStep);
                    float z2 = lowerStartZ + ((i + 1) * dashStep);
                    
                    float y1 = sheerPlan.GetTheoreticalHobYAtZ(z1) - yOffset;
                    float y2 = sheerPlan.GetTheoreticalHobYAtZ(z2) - yOffset;
                    
                    Gizmos.DrawLine(new Vector3(hw, y1, z1), new Vector3(hw, y2, z2));
                    Gizmos.DrawLine(new Vector3(-hw, y1, z1), new Vector3(-hw, y2, z2));
                }
            }
        }
    }

    private void DrawTopTimberRibband()
    {
        float deckStartZ = sheerPlan.cachedTopTimberSternIntersect.z;
        float startZ = sheerPlan.cachedTransomTopPoint.z;
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
            float totalLength = sheerPlan.cachedTopTimberBowIntersect.z - deckStartZ;

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
            FrameProfile fp = GetRawFrameProfile(z);
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
                    float x = breadthPlan.GetCutBreadthXAtZ(z);
                    currStbd = new Vector3(x, localTtY, z);
                    currPort = new Vector3(-x, localTtY, z);
                }
            }
            else
            {
                float x = breadthPlan.GetCutBreadthXAtZ(z);
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
        float startZ = sheerPlan.cachedSternPostTopPoint.z;
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

        Vector3? prevUpStbdT25 = null;
        Vector3? prevUpStbdT50 = null;
        Vector3? prevUpStbdT75 = null;
        Vector3? prevUpPortT25 = null;
        Vector3? prevUpPortT50 = null;
        Vector3? prevUpPortT75 = null;

        for (int i = 0; i <= segments; i++)
        {
            float z = startZ + (i * step);
            if (i == segments) z = endZ;

            FrameProfile fp = GetFrameProfile(z);
            if (!fp.isValid) continue;

            // --- LOWER MOULD RIBBANDS ---
            if (fp.hasLower)
            {
                if (fp.lowerCutoffT <= 0.25f) 
                {
                    Vector3 stbd25 = GetSurfacePointAtT(fp, 1f, 0.25f);
                    Vector3 port25 = GetSurfacePointAtT(fp, -1f, 0.25f);
                    if (prevStbdT25.HasValue)
                    {
#if UNITY_EDITOR
                        Handles.DrawDottedLine(prevStbdT25.Value, stbd25, 4f);
                        Handles.DrawDottedLine(prevPortT25.Value, port25, 4f);
#else
                        Gizmos.DrawLine(prevStbdT25.Value, stbd25);
                        Gizmos.DrawLine(prevPortT25.Value, port25);
#endif
                    }
                    prevStbdT25 = stbd25;
                    prevPortT25 = port25;
                } else { prevStbdT25 = null; prevPortT25 = null; }

                if (fp.lowerCutoffT <= 0.50f)
                {
                    Vector3 stbd50 = GetSurfacePointAtT(fp, 1f, 0.50f);
                    Vector3 port50 = GetSurfacePointAtT(fp, -1f, 0.50f);
                    if (prevStbdT50.HasValue)
                    {
#if UNITY_EDITOR
                        Handles.DrawDottedLine(prevStbdT50.Value, stbd50, 4f);
                        Handles.DrawDottedLine(prevPortT50.Value, port50, 4f);
#else
                        Gizmos.DrawLine(prevStbdT50.Value, stbd50);
                        Gizmos.DrawLine(prevPortT50.Value, port50);
#endif
                    }
                    prevStbdT50 = stbd50;
                    prevPortT50 = port50;
                } else { prevStbdT50 = null; prevPortT50 = null; }

                if (fp.lowerCutoffT <= 0.75f)
                {
                    Vector3 stbd75 = GetSurfacePointAtT(fp, 1f, 0.75f);
                    Vector3 port75 = GetSurfacePointAtT(fp, -1f, 0.75f);
                    if (prevStbdT75.HasValue)
                    {
#if UNITY_EDITOR
                        Handles.DrawDottedLine(prevStbdT75.Value, stbd75, 4f);
                        Handles.DrawDottedLine(prevPortT75.Value, port75, 4f);
#else
                        Gizmos.DrawLine(prevStbdT75.Value, stbd75);
                        Gizmos.DrawLine(prevPortT75.Value, port75);
#endif
                    }
                    prevStbdT75 = stbd75;
                    prevPortT75 = port75;
                } else { prevStbdT75 = null; prevPortT75 = null; }
            }
            else
            {
                prevStbdT25 = null; prevStbdT50 = null; prevStbdT75 = null;
                prevPortT25 = null; prevPortT50 = null; prevPortT75 = null;
            }

            // --- UPPER MOULD RIBBANDS ---
            if (fp.hasUpper)
            {
                if (fp.upperTopCutoffT >= 0.25f)
                {
                    Vector3 stbd25 = GetUpperSurfacePointAtT(fp, 1f, 0.25f);
                    Vector3 port25 = GetUpperSurfacePointAtT(fp, -1f, 0.25f);
                    if (prevUpStbdT25.HasValue)
                    {
#if UNITY_EDITOR
                        Handles.DrawDottedLine(prevUpStbdT25.Value, stbd25, 4f);
                        Handles.DrawDottedLine(prevUpPortT25.Value, port25, 4f);
#else
                        Gizmos.DrawLine(prevUpStbdT25.Value, stbd25);
                        Gizmos.DrawLine(prevUpPortT25.Value, port25);
#endif
                    }
                    prevUpStbdT25 = stbd25;
                    prevUpPortT25 = port25;
                } else { prevUpStbdT25 = null; prevUpPortT25 = null; }

                if (fp.upperTopCutoffT >= 0.50f)
                {
                    Vector3 stbd50 = GetUpperSurfacePointAtT(fp, 1f, 0.50f);
                    Vector3 port50 = GetUpperSurfacePointAtT(fp, -1f, 0.50f);
                    if (prevUpStbdT50.HasValue)
                    {
#if UNITY_EDITOR
                        Handles.DrawDottedLine(prevUpStbdT50.Value, stbd50, 4f);
                        Handles.DrawDottedLine(prevUpPortT50.Value, port50, 4f);
#else
                        Gizmos.DrawLine(prevUpStbdT50.Value, stbd50);
                        Gizmos.DrawLine(prevUpPortT50.Value, port50);
#endif
                    }
                    prevUpStbdT50 = stbd50;
                    prevUpPortT50 = port50;
                } else { prevUpStbdT50 = null; prevUpPortT50 = null; }

                if (fp.upperTopCutoffT >= 0.75f)
                {
                    Vector3 stbd75 = GetUpperSurfacePointAtT(fp, 1f, 0.75f);
                    Vector3 port75 = GetUpperSurfacePointAtT(fp, -1f, 0.75f);
                    if (prevUpStbdT75.HasValue)
                    {
#if UNITY_EDITOR
                        Handles.DrawDottedLine(prevUpStbdT75.Value, stbd75, 4f);
                        Handles.DrawDottedLine(prevUpPortT75.Value, port75, 4f);
#else
                        Gizmos.DrawLine(prevUpStbdT75.Value, stbd75);
                        Gizmos.DrawLine(prevUpPortT75.Value, port75);
#endif
                    }
                    prevUpStbdT75 = stbd75;
                    prevUpPortT75 = port75;
                } else { prevUpStbdT75 = null; prevUpPortT75 = null; }
            }
            else
            {
                prevUpStbdT25 = null; prevUpStbdT50 = null; prevUpStbdT75 = null;
                prevUpPortT25 = null; prevUpPortT50 = null; prevUpPortT75 = null;
            }
        }
    }

    private void DrawWaterlines()
    {
        float startZ = sheerPlan.cachedSternPostTopPoint.z;
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
        Vector3 u0 = new Vector3(fp.upperP0.x, fp.upperP0.y, fp.upperP0.z);
        Vector3 u1 = new Vector3(fp.upperP1.x, fp.upperP1.y, fp.upperP1.z);
        Vector3 u2 = new Vector3(fp.upperP2.x, fp.upperP2.y, fp.upperP2.z);
        Vector3 u3 = new Vector3(fp.upperP3.x, fp.upperP3.y, fp.upperP3.z);
        
        Vector3 pt = CalculateCubicBezierPoint(t, u0, u1, u2, u3);


        pt.x *= flipX;
        return pt;
    }

    public Vector3 GetSurfacePointAtT(FrameProfile fp, float flipX, float t, bool ignoreFashionTimber = false)
    {
        Vector3 p0 = new Vector3(fp.lowerP0.x, fp.lowerP0.y, fp.lowerP0.z);
        Vector3 p1Inner = new Vector3(fp.lowerP1_inner.x, fp.lowerP1_inner.y, fp.lowerP1_inner.z);
        Vector3 p1 = new Vector3(fp.lowerP1.x, fp.lowerP1.y, fp.lowerP1.z);
        Vector3 pMid = new Vector3(fp.lowerPMid.x, fp.lowerPMid.y, fp.lowerPMid.z);
        Vector3 p2 = new Vector3(fp.lowerP2.x, fp.lowerP2.y, fp.lowerP2.z);
        Vector3 p3 = new Vector3(fp.lowerP3.x, fp.lowerP3.y, fp.lowerP3.z);

        Vector3 pt;
        if (bodyPlan.useQuinticBezier)
            pt = CalculateQuinticBezierPoint(t, p0, p1Inner, p1, pMid, p2, p3);
        else if (bodyPlan.useQuarticBezier)
            pt = CalculateQuarticBezierPoint(t, p0, p1, pMid, p2, p3);
        else
            pt = CalculateCubicBezierPoint(t, p0, p1, p2, p3);

        if (!ignoreFashionTimber && fp.lowerP0.z <= sheerPlan.cachedSternRisingEnd.z)
        {
            FashionTimberModel fModel = GetComponent<FashionTimberModel>();
            if (fModel != null && fModel.showFashionTimbers)
            {
                float exactX = fModel.GetNaturalHullXAtYZ(pt.y, fp.lowerP0.z);
                if (exactX > 0.001f) pt.x = exactX;
            }
        }

        pt.x *= flipX;
        return pt;
    }

#if UNITY_EDITOR
    private static GUIStyle _labelStyle;
#endif

    public float GetFrameThicknessRatioValue()
    {
        switch (frameThicknessRatio)
        {
            case FrameThicknessRatio.Full: return 1.0f;
            case FrameThicknessRatio.Half: return 0.5f;
            case FrameThicknessRatio.Quarter: return 0.25f;
            case FrameThicknessRatio.Eighth: return 0.125f;
            default: return 0.5f;
        }
    }

    private void DrawFrameAtZ(float z)
    {
        FrameProfile fpCenter = GetFrameProfile(z);
        if (!fpCenter.isValid) return;

        Gizmos.color = frameColor;

        float thickness = frameSpacing * GetFrameThicknessRatioValue();
        float aftZ = z - (thickness / 2f);
        float foreZ = z + (thickness / 2f);
        
        FrameProfile fpAft = GetFrameProfile(aftZ);
        FrameProfile fpFore = GetFrameProfile(foreZ);

        // Draw Starboard
        if (fpAft.isValid) DrawMould(fpAft, 1f, false);
        DrawMould(fpCenter, 1f, true);
        if (fpFore.isValid) DrawMould(fpFore, 1f, false);

        // Draw Port (Mirrored)
        if (fpAft.isValid) DrawMould(fpAft, -1f, false);
        DrawMould(fpCenter, -1f, true);
        if (fpFore.isValid) DrawMould(fpFore, -1f, false);
        
#if UNITY_EDITOR
        if (showFrameLabels)
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle();
                _labelStyle.alignment = TextAnchor.MiddleCenter;
            }
            _labelStyle.normal.textColor = frameColor;
            
            float labelY = fpCenter.hasUpper ? fpCenter.upperP3.y : fpCenter.lowerP3.y;
            Vector3 labelPos = new Vector3(0, labelY + 0.5f, z);
            string labelText = z == 0 ? "Master" : z > 0 ? $"F{z}" : $"A{Mathf.Abs(z)}";
            Handles.Label(labelPos, labelText, _labelStyle);
        }
#endif
    }

    private void DrawMould(FrameProfile fp, float flipX, bool isDashed = false)
    {
        if (fp.hasLower)
        {
            float startT = fp.lowerCutoffT;
            float endT = fp.lowerTopCutoffT;
            
            int segments = 20;
            Vector3 prev = GetSurfacePointAtT(fp, flipX, startT);
            for (int i = 1; i <= segments; i++)
            {
                float t = startT + (endT - startT) * (i / (float)segments);
                Vector3 curr = GetSurfacePointAtT(fp, flipX, t);
                if (!isDashed || i % 2 != 0) Gizmos.DrawLine(prev, curr);
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
            
            Vector3 prev = GetUpperSurfacePointAtT(fp, flipX, startT);
            int segments = 20;
            for (int i = 1; i <= segments; i++)
            {
                float t = startT + (endT - startT) * (i / (float)segments);
                Vector3 curr = GetUpperSurfacePointAtT(fp, flipX, t);
                if (!isDashed || i % 2 != 0) Gizmos.DrawLine(prev, curr);
                prev = curr;
            }
            
            if (startT > 0.001f && flipX > 0)
            {
                Vector3 stbdCutoff = GetUpperSurfacePointAtT(fp, 1f, startT);
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
    public Vector3 GetInnerNormalAtT(FrameProfile fp, bool isUpper, float t, float zPos, Vector3 framePlaneNormal)
    {
        Vector3 outerPt = isUpper ? GetUpperSurfacePointAtT(fp, 1f, t) : GetSurfacePointAtT(fp, 1f, t, true);
        Vector3 nextPt = isUpper ? GetUpperSurfacePointAtT(fp, 1f, Mathf.Min(1f, t + 0.01f)) : GetSurfacePointAtT(fp, 1f, Mathf.Min(1f, t + 0.01f), true);

        Vector3 tangent = (nextPt - outerPt).normalized;
        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.up;

        Vector3 normal = Vector3.Cross(tangent, framePlaneNormal).normalized;
        
        Vector3 toCenter = new Vector3(-outerPt.x, 0, zPos - outerPt.z);
        if (Vector3.Dot(normal, toCenter) < 0) normal = -normal;

        return normal;
    }

    public Vector3 GetInnerPointAtT(FrameProfile fp, bool isUpper, float t, float zPos, Vector3 origin, Vector3 framePlaneNormal, float keelY, float totalHeight)
    {
        Vector3 outerPt = isUpper ? GetUpperSurfacePointAtT(fp, 1f, t) : GetSurfacePointAtT(fp, 1f, t, true);
        Vector3 nextPt = isUpper ? GetUpperSurfacePointAtT(fp, 1f, Mathf.Min(1f, t + 0.01f)) : GetSurfacePointAtT(fp, 1f, Mathf.Min(1f, t + 0.01f), true);

        Vector3 tangent = (nextPt - outerPt).normalized;
        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.up;

        Vector3 normal = Vector3.Cross(tangent, framePlaneNormal).normalized;
        
        Vector3 toCenter = new Vector3(-outerPt.x, 0, zPos - outerPt.z);
        if (Vector3.Dot(normal, toCenter) < 0) normal = -normal;

        float overallT = Mathf.Clamp01((outerPt.y - keelY) / totalHeight);
        float currentThickness = Mathf.Lerp(bodyPlan.baseFrameThickness, bodyPlan.topFrameThickness, overallT);
        
        return outerPt + normal * currentThickness;
    }
}
