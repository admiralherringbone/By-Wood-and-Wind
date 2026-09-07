using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class FramingModelTest3 : MonoBehaviour
{
    [Tooltip("Reference to the ModularSheerPlan.")]
    public SheerPlan sheerPlan;

    [Tooltip("Reference to the ModularBreadthPlan.")]
    public BreadthPlan breadthPlan;

    [Tooltip("Reference to the ModularBodyPlan.")]
    public BodyPlanTest bodyPlan;

    public enum FrameThicknessRatio
    {
        Full,
        Half,
        Quarter,
        Eighth
    }

    [Header("Frame Dimensions")]
    [Tooltip("The longitudinal distance between the centers of each frame (Z-axis).")]
    [Min(0.25f)]
    public float frameSpacing = 1.0f;
    [Tooltip("The thickness of each frame relative to the frame spacing.")]
    public FrameThicknessRatio frameThicknessRatio = FrameThicknessRatio.Half;

    private void OnValidate()
    {
        frameSpacing = Mathf.Max(0.25f, frameSpacing);
        _searchedForeFace = false;
        _searchedFashionModel = false;
        _profileCache.Clear();
    }

    private void OnEnable()
    {
        _searchedForeFace = false;
        _searchedFashionModel = false;
        _profileCache.Clear();
    }

    [Header("Gizmo Settings")]
    public Color frameColor = Color.cyan;
    [Tooltip("Color of frames that are cut at the Fashion Timber boundary.")]
    public Color cutFrameColor = new Color(0f, 0.75f, 0.75f, 1f);
    [Tooltip("Reference to the ForeFaceFashionTimberModelTest3.")]
    public ForeFaceFashionTimberModelTest3 foreFaceFashionTimberModelTest;

    [Tooltip("Reference to the FashionTimberModelTest3.")]
    public FashionTimberModelTest3 fashionTimberModelTest;

    private ForeFaceFashionTimberModelTest3 _cachedForeFace;
    private FashionTimberModelTest3 _cachedFashionModel;
    private bool _searchedForeFace = false;
    private bool _searchedFashionModel = false;
    private readonly Dictionary<float, FrameProfile> _profileCache = new Dictionary<float, FrameProfile>();
    private readonly List<float> _zPosListCache = new List<float>();
    private readonly List<float> _basicZPosListCache = new List<float>();
    private readonly List<float> _landmarkZPosListCache = new List<float>();

    [Header("Frame Visualization Settings")]
    public bool showFrames = true;
    public bool showFrameLabels = false;
    public bool showFloorSweepsGuide = true;
    [Range(5, 100)]
    [Tooltip("The number of line segments used to draw each mould curve (lower & upper) of a frame.")]
    public int frameSegments = 20;
    [Tooltip("If true, frames are cut/clipped by the Fore Face Fashion Timber boundary at the stern.")]
    public bool cutFramesAtFashionTimber = true;
    [Tooltip("If true, frames are cut/clipped at the Deadwood top surface rather than having their base points pushed up.")]
    public bool cutFramesAtDeadwood = true;
    [Tooltip("If true, structural landmark boundaries render as partial fill frames spanning between the nearest basic frame edge and the landmark point. If false, single landmark frames render directly at exact boundary points.")]
    public bool enableLandmarkFillFrames = false;
    [Tooltip("Draws lines showing the control arms for each frame.")]
    public bool showSurfaceRibbands = true;
    public bool useTargetSpacing = true;
    [Tooltip("Target physical spacing between intermediate surface ribbands (e.g. 0.25 units).")]
    public float targetRibbandSpacing = 0.25f;
    [Tooltip("Manual lower mould ribband count when useTargetSpacing is false.")]
    [Range(1, 15)] public int manualLowerRibbandCount = 3;
    [Tooltip("Manual upper mould ribband count when useTargetSpacing is false.")]
    [Range(1, 15)] public int manualUpperRibbandCount = 3;
    public bool showRibbandCutoffDots = false;
    public bool showWaterlines = false;
    public bool showFrameControlArms = false;

    public struct FillFrame
    {
        public bool isValid;
        public float startZ;
        public float endZ;
        public FrameProfile startFp;
        public FrameProfile endFp;
        public string label;
    }

    private readonly List<FillFrame> _fillFrameListCache = new List<FillFrame>();

    public Color GetFillFrameColor()
    {
        // Darker shade of current frame color as requested by user
        return new Color(frameColor.r * 0.65f, frameColor.g * 0.65f, frameColor.b * 0.65f, frameColor.a);
    }

    public struct FrameProfile
    {
        public bool isValid;
        public bool hasLower;
        public Vector3 lowerP0, lowerP1_inner, lowerP1, lowerLR, lowerPMid, lowerP2, lowerP3;
        
        public bool hasUpper;
        public float upperCutoffT; // LOWER cutoff (e.g. for Fashion Timber)
        public float upperTopCutoffT; // UPPER cutoff (e.g. for partial bulwarks)
        public float lowerCutoffT; // LOWER cutoff for Lower Mould (e.g. for Counter / Fashion Timber)
        public float lowerTopCutoffT; // UPPER cutoff for Lower Mould (e.g. for Fashion Timber)
        public Vector3 upperP0, upperP1, upperP2, upperP3;
    }

    public virtual FrameProfile GetRawFrameProfile(float z, bool ignoreFashionTimber = false, float? keelYOverride = null)
    {
        FrameProfile fp = new FrameProfile();
        fp.isValid = false;
        fp.hasLower = true;
        fp.hasUpper = false;
        fp.lowerCutoffT = 0f;
        fp.lowerTopCutoffT = 1f;

        if (sheerPlan == null || breadthPlan == null || bodyPlan == null) return fp;

        // We no longer cull the lower mould aft of the HoB stern intersection, allowing full frames to the transom
        // if (z < sheerPlan.cachedHobSternIntersect.z)
        // {
        //     fp.hasLower = false;
        // }

        // We no longer arbitrarily disable the lower mould aft of the Stern Post.
        // The Counter sliding system now handles clipping the lower moulds dynamically in GetFrameProfile.

        float hobBreadth = breadthPlan.GetTheoreticalBreadthXAtZ(z);
        float hobY = sheerPlan.GetTheoreticalHobYAtZ(z);
        
        if (fp.hasLower)
        {
            float keelY = sheerPlan.GetKeelYAtZ(z);
            float risingY = sheerPlan.GetRisingLineYAtZ(z);
        
        Vector3 fashionLowerBound = sheerPlan.cachedSternRisingEnd;
        if (sheerPlan.showDeadwoodRake && sheerPlan.cachedSternRisingEnd.y < sheerPlan.cachedDeadwoodAftPoint.y)
        {
            fashionLowerBound = sheerPlan.cachedDeadwoodAftPoint;
        }

        // If we are aft of the Stern Post, extend the Keel mathematically (Phantom Keel Projection)
        // anchored at the flat Keel baseline rather than elevating to Deadwood Top.
        if (z < sheerPlan.cachedKeelSternPoint.z)
        {
            if (sheerPlan.keelMidshipDrop == 0f)
            {
                keelY = 0f;
            }
            else
            {
                float t = z / sheerPlan.cachedKeelSternPoint.z; // Continues past 1.0
                Vector3 p0 = sheerPlan.cachedMidshipKeel;
                Vector3 p2 = sheerPlan.cachedKeelSternPoint;
                Vector3 p1 = new Vector3(0, 0, p2.z / 2f);
                keelY = CalculateQuadraticBezierPoint(t, p0, p1, p2).y;
            }
        }

        float originalKeelY = keelY;
        if (keelYOverride.HasValue)
        {
            keelY = keelYOverride.Value;
        }
        
        float keelHalfWidth = bodyPlan.keelWidth / 2f;
        
        bool usedSpecialScaling = false;
        // The user requested to completely ignore the Fashion Timber's phantom projection line
        // and just let the frames continue naturally along the keel.
        /*
        if (!ignoreFashionTimber)
        {
            if (this.foreFaceFashionTimberModelTest == null)
            {
                this.foreFaceFashionTimberModelTest = Object.FindObjectOfType<ForeFaceFashionTimberModelTest3>();
            }
            ForeFaceFashionTimberModelTest3 foreFace = this.foreFaceFashionTimberModelTest;
            if (foreFace != null && foreFace.showInnerFashionTimber && foreFace.cachedInnerPoints != null && foreFace.cachedInnerPoints.Count > 0)
            {
                float bottomZ = foreFace.cachedInnerPoints[0].x;
                if (z < bottomZ - 0.001f)
                {
                    FrameProfile masterFp = GetRawFrameProfile(bottomZ, true, keelYOverride);
                    if (masterFp.isValid && masterFp.hasLower)
                    {
                        usedSpecialScaling = true;
                        
                        float masterHobBreadth = breadthPlan.GetBreadthXAtZ(bottomZ);
                        float masterHobY = sheerPlan.GetTheoreticalHobYAtZ(bottomZ);
                        float masterKeelY = masterFp.lowerP0.y;

                        float customKeelY = masterKeelY;
                        float yOffset = masterHobY - masterKeelY;
                        customKeelY = sheerPlan.GetTheoreticalHobYAtZ(z) - yOffset;

                        float currHobBreadth = breadthPlan.GetBreadthXAtZ(z);
                        float currHobY = sheerPlan.GetTheoreticalHobYAtZ(z);
                        
                        float hw = keelHalfWidth;
                        float masterXDist = masterHobBreadth - hw;
                        float currXDist = currHobBreadth - hw;
                        float scaleX = masterXDist > 0.001f ? currXDist / masterXDist : 1f;
                        
                        float masterHeight = masterHobY - masterKeelY;
                        float currHeight = currHobY - customKeelY;
                        float scaleY = masterHeight > 0.001f ? currHeight / masterHeight : 1f;

                        Vector3 ScalePoint(Vector3 masterPt)
                        {
                            float nxOffset = (masterPt.x - hw) * scaleX;
                            float finalX = hw + Mathf.Max(0f, nxOffset);
                            float nyOffset = (masterPt.y - masterKeelY) * scaleY;
                            float finalY = customKeelY + nyOffset;
                            return new Vector3(finalX, finalY, z);
                        }

                        fp.lowerP0 = ScalePoint(masterFp.lowerP0);
                        fp.lowerP1_inner = ScalePoint(masterFp.lowerP1_inner);
                        fp.lowerP1 = ScalePoint(masterFp.lowerP1);
                        fp.lowerPMid = ScalePoint(masterFp.lowerPMid);
                        fp.lowerP2 = ScalePoint(masterFp.lowerP2);
                        fp.lowerP3 = ScalePoint(masterFp.lowerP3);
                        
                        fp.lowerCutoffT = masterFp.lowerCutoffT;
                        fp.lowerTopCutoffT = masterFp.lowerTopCutoffT;
                    }
                }
            }
        }
        */

        if (!usedSpecialScaling)
        {
            fp.lowerP0 = new Vector3(keelHalfWidth, keelY, z); 
            fp.lowerP3 = new Vector3(hobBreadth, hobY, z);

            // 1. DIAGONAL DEADRISE TAPER (LOWER RIBBAND FOR P1 AND RISING LINE POINT LR)
            Vector3 masterL1 = bodyPlan.cachedL1;
            Vector3 masterLR = bodyPlan.cachedLR;

            float targetL1_X = keelHalfWidth;
            float targetL1_Y = originalKeelY;

            float targetLR_X = keelHalfWidth;
            float targetLR_Y = originalKeelY;

            if (z >= 0)
            {
                float bowZ = sheerPlan.cachedBowRisingEnd.z;
                if (bowZ > 0.001f)
                {
                    float t = sheerPlan.GetRisingLineTAtZ(z);
                    Vector3 bowEnd = new Vector3(keelHalfWidth, sheerPlan.cachedBowRisingEnd.y, sheerPlan.cachedBowRisingEnd.z);

                    // Swept curve for P1 (Keel Control Arm)
                    Vector3 p0_L1 = masterL1;
                    Vector3 p1_L1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedBowP1Ctrl.z);
                    Vector3 p2_L1 = bowEnd;
                    targetL1_X = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).x;
                    targetL1_Y = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).y;

                    // Swept curve for LR (Rising Line Point)
                    Vector3 p0_LR = masterLR;
                    Vector3 p1_LR = new Vector3(masterLR.x, masterLR.y, sheerPlan.cachedBowP1Ctrl.z);
                    Vector3 p2_LR = bowEnd;
                    targetLR_X = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).x;
                    targetLR_Y = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).y;
                }
            }
            else
            {
                float sternZ = sheerPlan.cachedSternRisingEnd.z;
                if (sternZ < -0.001f)
                {
                    Vector3 sternEnd = new Vector3(keelHalfWidth, sheerPlan.cachedSternRisingEnd.y, sheerPlan.cachedSternRisingEnd.z);

                    Vector3 p0_L1 = masterL1;
                    Vector3 p1_L1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedSternP1Ctrl.z);
                    Vector3 p2_L1 = sternEnd;

                    Vector3 p0_LR = masterLR;
                    Vector3 p1_LR = new Vector3(masterLR.x, masterLR.y, sheerPlan.cachedSternP1Ctrl.z);
                    Vector3 p2_LR = sternEnd;

                    if (z < sternZ)
                    {
                        // Phantom projection past Stern Rising End
                        float dzL1 = p2_L1.z - p1_L1.z;
                        if (Mathf.Abs(dzL1) > 0.0001f)
                        {
                            float ratio = (z - p2_L1.z) / dzL1;
                            targetL1_X = p2_L1.x;
                            targetL1_Y = p2_L1.y + (p2_L1.y - p1_L1.y) * ratio;
                        }
                        else
                        {
                            targetL1_X = p2_L1.x;
                            targetL1_Y = p2_L1.y;
                        }

                        float dzLR = p2_LR.z - p1_LR.z;
                        if (Mathf.Abs(dzLR) > 0.0001f)
                        {
                            float ratio = (z - p2_LR.z) / dzLR;
                            targetLR_X = p2_LR.x;
                            targetLR_Y = p2_LR.y + (p2_LR.y - p1_LR.y) * ratio;
                        }
                        else
                        {
                            targetLR_X = p2_LR.x;
                            targetLR_Y = p2_LR.y;
                        }
                    }
                    else
                    {
                        float t = sheerPlan.GetRisingLineTAtZ(z);
                        targetL1_X = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).x;
                        targetL1_Y = CalculateQuadraticBezierPoint(t, p0_L1, p1_L1, p2_L1).y;

                        targetLR_X = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).x;
                        targetLR_Y = CalculateQuadraticBezierPoint(t, p0_LR, p1_LR, p2_LR).y;
                    }
                }
            }

            targetL1_Y = Mathf.Max(targetL1_Y, originalKeelY);
            targetLR_Y = Mathf.Max(targetLR_Y, originalKeelY);

            fp.lowerP1 = new Vector3(targetL1_X, targetL1_Y, z);

            // 2. HOB DOWNWARD ARM (LOWER P2)
            float currentBoxHeight = hobY - originalKeelY;
            float hobDrop = currentBoxHeight * bodyPlan.hobLowerArmLength;
            fp.lowerP2 = new Vector3(hobBreadth, Mathf.Max(hobY - hobDrop, targetLR_Y), z);

            // 3. RISING LINE POINT (L_R) FOR 2-SEGMENT QUADRATIC BEZIER
            // Position fp.lowerLR on the line between fp.lowerP1 and fp.lowerP2 such that fp.lowerLR.x matches targetLR_X
            float dx = fp.lowerP2.x - fp.lowerP1.x;
            float lrPosRatio = bodyPlan.risingLinePosition;
            if (dx > 0.0001f)
            {
                lrPosRatio = Mathf.Clamp01((targetLR_X - fp.lowerP1.x) / dx);
            }
            fp.lowerLR = Vector3.Lerp(fp.lowerP1, fp.lowerP2, lrPosRatio);
            fp.lowerPMid = fp.lowerLR;
            
            Vector3 unprojectedP0 = new Vector3(keelHalfWidth, originalKeelY, z);
            fp.lowerP1_inner = Vector3.Lerp(unprojectedP0, fp.lowerP1, 0.5f);
            
            // 4. PROPORTIONAL Y-SCALING
            if (keelY > originalKeelY + 0.001f && hobY > originalKeelY + 0.001f)
            {
                float scaleY = (hobY - keelY) / (hobY - originalKeelY);
                
                fp.lowerP1.y = keelY + (fp.lowerP1.y - originalKeelY) * scaleY;
                fp.lowerP1_inner.y = keelY + (fp.lowerP1_inner.y - originalKeelY) * scaleY;
                fp.lowerLR.y = keelY + (fp.lowerLR.y - originalKeelY) * scaleY;
                fp.lowerPMid.y = fp.lowerLR.y;
                fp.lowerP2.y = keelY + (fp.lowerP2.y - originalKeelY) * scaleY;
            }
        }
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
            for (int i = 0; i < 10; i++)
            {
                float midT = (minT + maxT) * 0.5f;
                float py = CalculateCubicBezierPoint(midT, fp.upperP0, fp.upperP1, fp.upperP2, fp.upperP3).y;
                if (py < localTtY) minT = midT;
                else maxT = midT;
            }
            fp.upperTopCutoffT = (minT + maxT) * 0.5f;
        }

        fp.isValid = true;
        return fp;
    }

    public virtual FrameProfile GetFrameProfile(float z)
    {
        if (_profileCache.TryGetValue(z, out FrameProfile cachedFp))
        {
            return cachedFp;
        }

        FrameProfile fp = GetRawFrameProfile(z);
        if (!fp.isValid)
        {
            _profileCache[z] = fp;
            return fp;
        }

        if (_cachedForeFace == null)
        {
            _cachedForeFace = foreFaceFashionTimberModelTest != null ? foreFaceFashionTimberModelTest : Object.FindObjectOfType<ForeFaceFashionTimberModelTest3>();
            _searchedForeFace = _cachedForeFace != null;
        }

        if (cutFramesAtFashionTimber && _cachedForeFace != null)
        {
            float sternMinZ = _cachedForeFace.GetMinZ();
            float sternMaxZ = _cachedForeFace.GetMaxZ();
            
            // If a frame is completely aft of (behind) the Fashion Timber curve, invalidate it entirely
            if (z < sternMinZ - 0.001f)
            {
                fp.isValid = false;
                _profileCache[z] = fp;
                return fp;
            }
            
            if (z <= sternMaxZ + 0.001f)
            {
                float hobY = sheerPlan.GetTheoreticalHobYAtZ(z);
                Vector3 hobPt = _cachedForeFace.GetHobIntersection();
                bool isUpper = z < hobPt.z - 0.001f;
                float fashionY = _cachedForeFace.GetInnerYAtZ(z, isUpper);
                float topTimberY = sheerPlan.GetTopTimberYAtZ(z);

                // If fashion timber height is at or above top timber, frame is completely behind fashion timber
                if (fashionY >= topTimberY - 0.001f)
                {
                    fp.isValid = false;
                    _profileCache[z] = fp;
                    return fp;
                }
                
                if (fashionY > hobY || !fp.hasLower)
                {
                    fp.hasLower = false;
                    fp.lowerCutoffT = 0f;
                    
                    if (fp.hasUpper)
                    {
                        float fMinT = 0f;
                        float fMaxT = 1f;
                        for (int i = 0; i < 10; i++)
                        {
                            float midT = (fMinT + fMaxT) * 0.5f;
                            float py = GetUpperSurfacePointAtT(fp, 1f, midT).y;
                            if (py < fashionY) fMinT = midT;
                            else fMaxT = midT;
                        }
                        fp.upperCutoffT = (fMinT + fMaxT) * 0.5f;

                        if (fp.upperCutoffT >= fp.upperTopCutoffT - 0.001f)
                        {
                            fp.isValid = false;
                            _profileCache[z] = fp;
                            return fp;
                        }
                    }
                    else
                    {
                        fp.isValid = false;
                        _profileCache[z] = fp;
                        return fp;
                    }
                }
                else
                {
                    // Cut the bottom of the lower mould at the actual Lower Fashion Timber boundary
                    float tMin = 0f;
                    float tMax = 1f;
                    for (int i = 0; i < 10; i++)
                    {
                        float midT = (tMin + tMax) * 0.5f;
                        float py = GetSurfacePointAtT(fp, 1f, midT).y;
                        if (py < fashionY) tMin = midT;
                        else tMax = midT;
                    }
                    fp.lowerCutoffT = (tMin + tMax) * 0.5f;
                    
                    fp.lowerTopCutoffT = 1f; // Top reaches the HoB
                    fp.upperCutoffT = 0f;
                }
            }
        }

        if (cutFramesAtDeadwood && sheerPlan != null && sheerPlan.showDeadwoodRake)
        {
            float dForeZ = sheerPlan.cachedDeadwoodForePoint.z;
            float dAftZ = sheerPlan.cachedDeadwoodAftPoint.z;

            if (z >= dAftZ - 0.001f && z <= dForeZ + 0.001f)
            {
                float deadwoodY = sheerPlan.GetDeadwoodYAtZ(z);
                float hobY = sheerPlan.GetTheoreticalHobYAtZ(z);

                if (deadwoodY > fp.lowerP0.y + 0.001f)
                {
                    if (deadwoodY >= hobY - 0.001f)
                    {
                        fp.hasLower = false;
                        fp.lowerCutoffT = 0f;

                        if (fp.hasUpper)
                        {
                            float uMinT = 0f;
                            float uMaxT = 1f;
                            for (int i = 0; i < 10; i++)
                            {
                                float midT = (uMinT + uMaxT) * 0.5f;
                                float py = GetUpperSurfacePointAtT(fp, 1f, midT).y;
                                if (py < deadwoodY) uMinT = midT;
                                else uMaxT = midT;
                            }
                            fp.upperCutoffT = (uMinT + uMaxT) * 0.5f;

                            if (fp.upperCutoffT >= fp.upperTopCutoffT - 0.001f)
                            {
                                fp.isValid = false;
                                _profileCache[z] = fp;
                                return fp;
                            }
                        }
                        else
                        {
                            fp.isValid = false;
                            _profileCache[z] = fp;
                            return fp;
                        }
                    }
                    else if (fp.hasLower)
                    {
                        float tMin = 0f;
                        float tMax = 1f;
                        for (int i = 0; i < 10; i++)
                        {
                            float midT = (tMin + tMax) * 0.5f;
                            float py = GetSurfacePointAtT(fp, 1f, midT).y;
                            if (py < deadwoodY) tMin = midT;
                            else tMax = midT;
                        }
                        float deadwoodCutoffT = (tMin + tMax) * 0.5f;
                        fp.lowerCutoffT = Mathf.Max(fp.lowerCutoffT, deadwoodCutoffT);
                    }
                }
            }
        }
        
        _profileCache[z] = fp;
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
        
        _profileCache.Clear();

        sheerPlan.UpdateCachedValues();
        breadthPlan.UpdateCachedCurves();
        bodyPlan.UpdateCachedCurves();

        if (_cachedForeFace == null && !_searchedForeFace)
        {
            _cachedForeFace = foreFaceFashionTimberModelTest != null ? foreFaceFashionTimberModelTest : Object.FindObjectOfType<ForeFaceFashionTimberModelTest3>();
            _searchedForeFace = true;
        }
        if (_cachedForeFace != null)
        {
            _cachedForeFace.UpdateCachedPoints();
        }

        if (!showFrames && !showFloorSweepsGuide && !showSurfaceRibbands && !showWaterlines) return;

        // Draw Floor Sweeps Guide (Control Curves)
        if (showFloorSweepsGuide)
        {
            float hw = bodyPlan.keelWidth / 2f;
            Vector3 masterL1 = bodyPlan.cachedL1;
            Vector3 masterLR = bodyPlan.cachedLR;

            Vector3 bowEnd = new Vector3(hw, sheerPlan.cachedBowRisingEnd.y, sheerPlan.cachedBowRisingEnd.z);
            Vector3 sternEnd = new Vector3(hw, sheerPlan.cachedSternRisingEnd.y, sheerPlan.cachedSternRisingEnd.z);

            Vector3 bowP1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedBowP1Ctrl.z);
            Vector3 sternP1 = new Vector3(masterL1.x, masterL1.y, sheerPlan.cachedSternP1Ctrl.z);

            Vector3 bowP1_LR = new Vector3(masterLR.x, masterLR.y, sheerPlan.cachedBowP1Ctrl.z);
            Vector3 sternP1_LR = new Vector3(masterLR.x, masterLR.y, sheerPlan.cachedSternP1Ctrl.z);

            // Starboard Keel Arm Ribband (Yellow)
            DrawQuadraticBezier3D(masterL1, sternP1, sternEnd, Color.yellow);
            DrawQuadraticBezier3D(masterL1, bowP1, bowEnd, Color.yellow);

            // Starboard Rising Line Ribband (Green)
            DrawQuadraticBezier3D(masterLR, sternP1_LR, sternEnd, Color.green);
            DrawQuadraticBezier3D(masterLR, bowP1_LR, bowEnd, Color.green);
            
            // Port Ribbands
            Vector3 mMasterL1 = new Vector3(-masterL1.x, masterL1.y, masterL1.z);
            Vector3 mBowP1 = new Vector3(-bowP1.x, bowP1.y, bowP1.z);
            Vector3 mSternP1 = new Vector3(-sternP1.x, sternP1.y, sternP1.z);
            Vector3 mBowEnd = new Vector3(-hw, bowEnd.y, bowEnd.z);
            Vector3 mSternEnd = new Vector3(-hw, sternEnd.y, sternEnd.z);

            Vector3 mMasterLR = new Vector3(-masterLR.x, masterLR.y, masterLR.z);
            Vector3 mBowP1_LR = new Vector3(-bowP1_LR.x, bowP1_LR.y, bowP1_LR.z);
            Vector3 mSternP1_LR = new Vector3(-sternP1_LR.x, sternP1_LR.y, sternP1_LR.z);
            
            DrawQuadraticBezier3D(mMasterL1, mSternP1, mSternEnd, Color.yellow);
            DrawQuadraticBezier3D(mMasterL1, mBowP1, mBowEnd, Color.yellow);

            DrawQuadraticBezier3D(mMasterLR, mSternP1_LR, mSternEnd, Color.green);
            DrawQuadraticBezier3D(mMasterLR, mBowP1_LR, mBowEnd, Color.green);
            
            // Draw Phantom Projection of Stern Rising Line (Gray Dashed)
            float finalZ = sheerPlan.cachedTransomTopPoint.z;
            if (finalZ < sternEnd.z - 0.001f)
            {
                Gizmos.color = Color.gray;
                int dashCount = 20;
                float dzTotal = finalZ - sternEnd.z;
                float dzTangent = sternEnd.z - sternP1.z;
                
                if (Mathf.Abs(dzTangent) > 0.0001f)
                {
                    for (int i = 0; i < dashCount; i++)
                    {
                        if (i % 2 == 1) continue; // Skip every other segment to make it dashed
                        
                        float t1 = (float)i / dashCount;
                        float t2 = (float)(i + 1) / dashCount;
                        
                        float z1 = sternEnd.z + t1 * dzTotal;
                        float z2 = sternEnd.z + t2 * dzTotal;
                        
                        float ratio1 = (z1 - sternEnd.z) / dzTangent;
                        float ratio2 = (z2 - sternEnd.z) / dzTangent;
                        
                        float y1 = sternEnd.y + (sternEnd.y - sternP1.y) * ratio1;
                        float y2 = sternEnd.y + (sternEnd.y - sternP1.y) * ratio2;
                        
                        // Draw Starboard side
                        Gizmos.DrawLine(new Vector3(hw, y1, z1), new Vector3(hw, y2, z2));
                        // Draw Port side
                        Gizmos.DrawLine(new Vector3(-hw, y1, z1), new Vector3(-hw, y2, z2));
                    }
                }
            }
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

            if (enableLandmarkFillFrames)
            {
                foreach (FillFrame fill in GetFillFrames())
                {
                    DrawFillFrame(fill);
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

    public virtual List<float> GetBasicFrameZPositions()
    {
        _basicZPosListCache.Clear();
        if (sheerPlan == null) return _basicZPosListCache;

        float bowZ = sheerPlan.cachedStemTopPoint.z;
        float sternBoundaryZ = sheerPlan.cachedTransomTopPoint.z;
        float spacing = Mathf.Max(0.25f, frameSpacing);

        int safetyCount = 0;
        int maxSafetyFrames = 400;

        for (float z = 0; z <= bowZ && safetyCount < maxSafetyFrames; safetyCount++)
        {
            if (!_basicZPosListCache.Contains(z)) _basicZPosListCache.Add(z);
            z += spacing;
        }

        for (float z = -spacing; z >= sternBoundaryZ && safetyCount < maxSafetyFrames; safetyCount++)
        {
            if (!_basicZPosListCache.Contains(z)) _basicZPosListCache.Add(z);
            z -= spacing;
        }

        _basicZPosListCache.Sort();
        return _basicZPosListCache;
    }

    public virtual List<float> GetLandmarkZPositions()
    {
        _landmarkZPosListCache.Clear();
        if (sheerPlan == null) return _landmarkZPosListCache;

        float bowZ = sheerPlan.cachedStemTopPoint.z;
        float sternBoundaryZ = sheerPlan.cachedTransomTopPoint.z;
        float keelBowZ = sheerPlan.cachedKeelBowPoint.z;
        float risingSternZ = sheerPlan.cachedSternRisingEnd.z;

        if (!_landmarkZPosListCache.Contains(sternBoundaryZ)) _landmarkZPosListCache.Add(sternBoundaryZ);
        if (!_landmarkZPosListCache.Contains(keelBowZ)) _landmarkZPosListCache.Add(keelBowZ);
        if (!_landmarkZPosListCache.Contains(risingSternZ)) _landmarkZPosListCache.Add(risingSternZ);

        if (sheerPlan.upperDecks != null)
        {
            float totalLength = sheerPlan.cachedTopTimberBowIntersect.z - sheerPlan.cachedTopTimberSternIntersect.z;
            float startZ = sheerPlan.cachedTopTimberSternIntersect.z;
            foreach (var deck in sheerPlan.upperDecks)
            {
                float aftZ = startZ + (deck.globalAftExtent * totalLength);
                float foreZ = startZ + (deck.globalForeExtent * totalLength);

                if (aftZ > sternBoundaryZ && aftZ < bowZ && !_landmarkZPosListCache.Contains(aftZ)) _landmarkZPosListCache.Add(aftZ);
                if (foreZ > sternBoundaryZ && foreZ < bowZ && !_landmarkZPosListCache.Contains(foreZ)) _landmarkZPosListCache.Add(foreZ);
            }
        }

        _landmarkZPosListCache.Sort();
        return _landmarkZPosListCache;
    }

    public virtual List<FillFrame> GetFillFrames()
    {
        _fillFrameListCache.Clear();
        if (sheerPlan == null) return _fillFrameListCache;

        List<float> basicZList = GetBasicFrameZPositions();
        List<float> landmarkZList = GetLandmarkZPositions();
        if (basicZList.Count == 0 || landmarkZList.Count == 0) return _fillFrameListCache;

        float thickness = frameSpacing * GetFrameThicknessRatioValue();
        float halfThickness = thickness / 2f;

        foreach (float landmarkZ in landmarkZList)
        {
            float nearestBasicZ = basicZList[0];
            float minDist = Mathf.Abs(landmarkZ - nearestBasicZ);

            for (int i = 1; i < basicZList.Count; i++)
            {
                float dist = Mathf.Abs(landmarkZ - basicZList[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestBasicZ = basicZList[i];
                }
            }

            float basicEdgeZ = landmarkZ >= nearestBasicZ ? nearestBasicZ + halfThickness : nearestBasicZ - halfThickness;

            float startZ = Mathf.Min(basicEdgeZ, landmarkZ);
            float endZ = Mathf.Max(basicEdgeZ, landmarkZ);

            if (endZ - startZ > 0.005f)
            {
                FrameProfile startFp = GetFrameProfile(startZ);
                FrameProfile endFp = GetFrameProfile(endZ);

                if (startFp.isValid || endFp.isValid)
                {
                    FillFrame fill = new FillFrame
                    {
                        isValid = true,
                        startZ = startZ,
                        endZ = endZ,
                        startFp = startFp,
                        endFp = endFp,
                        label = $"Fill Z:{landmarkZ:F1}"
                    };
                    _fillFrameListCache.Add(fill);
                }
            }
        }

        return _fillFrameListCache;
    }

    public virtual List<float> GetFrameZPositions()
    {
        if (enableLandmarkFillFrames)
        {
            return GetBasicFrameZPositions();
        }

        _zPosListCache.Clear();
        if (sheerPlan == null) return _zPosListCache;

        List<float> basicList = GetBasicFrameZPositions();
        _zPosListCache.AddRange(basicList);

        List<float> landmarkList = GetLandmarkZPositions();
        foreach (float lmZ in landmarkList)
        {
            if (!_zPosListCache.Contains(lmZ)) _zPosListCache.Add(lmZ);
        }

        _zPosListCache.Sort();
        return _zPosListCache;
    }

    private void DrawHobRibband()
    {
        float startZ = sheerPlan.cachedHobSternIntersect.z;
        float endZ = sheerPlan.cachedStemTopPoint.z;
        int totalPoints = 60;

        Gizmos.color = Color.white;
        Vector3? prevStbd = null;
        Vector3? prevPort = null;

        for (int i = 0; i < totalPoints; i++)
        {
            float t = i / (float)(totalPoints - 1);
            float angle = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, t);
            float mappedT = (Mathf.Sin(angle) + 1f) / 2f;
            float z = Mathf.Lerp(startZ, endZ, mappedT);
            
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
        
        if (this.fashionTimberModelTest == null && !_searchedFashionModel)
        {
            this.fashionTimberModelTest = Object.FindObjectOfType<FashionTimberModelTest3>();
            _searchedFashionModel = true;
        }
        FashionTimberModelTest3 fModel = this.fashionTimberModelTest;
        
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

            // Draw Lower Phantom HoB Projection for Fore Face Fashion Timber (Dashed Darker Grey)
            if (foreFaceFashionTimberModelTest != null && foreFaceFashionTimberModelTest.showInnerFashionTimber && foreFaceFashionTimberModelTest.cachedInnerPoints != null && foreFaceFashionTimberModelTest.cachedInnerPoints.Count > 0)
            {
                float ffLowerStartZ = foreFaceFashionTimberModelTest.cachedInnerPoints[0].x;
                float ffLowerEndZ = foreFaceFashionTimberModelTest.GetHobIntersection().z;

                if (ffLowerEndZ < ffLowerStartZ)
                {
                    int dashSegments = Mathf.CeilToInt(Mathf.Abs(ffLowerStartZ - ffLowerEndZ) / 0.1f);
                    if (dashSegments % 2 == 0) dashSegments++; // Guarantee it ends on a solid dash
                    float dashStep = (ffLowerEndZ - ffLowerStartZ) / dashSegments;
                    
                    float startHobY = sheerPlan.GetTheoreticalHobYAtZ(ffLowerStartZ);
                    float ffDropY = foreFaceFashionTimberModelTest.cachedInnerPoints[0].y;
                    float yOffset = startHobY - ffDropY;
                    float hw = bodyPlan.keelWidth / 2f;

                    Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 1f); // Slightly darker grey to differentiate

                    for (int i = 0; i < dashSegments; i++)
                    {
                        if (i % 2 != 0) continue; // Dashed effect
                        
                        float z1 = ffLowerStartZ + (i * dashStep);
                        float z2 = ffLowerStartZ + ((i + 1) * dashStep);
                        
                        float y1 = sheerPlan.GetTheoreticalHobYAtZ(z1) - yOffset;
                        float y2 = sheerPlan.GetTheoreticalHobYAtZ(z2) - yOffset;
                        
                        Gizmos.DrawLine(new Vector3(hw, y1, z1), new Vector3(hw, y2, z2));
                        Gizmos.DrawLine(new Vector3(-hw, y1, z1), new Vector3(-hw, y2, z2));
                    }
                }
            }
        }
    }

    private void DrawTopTimberRibband()
    {
        float deckStartZ = sheerPlan.cachedTopTimberSternIntersect.z;
        float startZ = sheerPlan.cachedTransomTopPoint.z;
        float endZ = sheerPlan.cachedUpperStemTopPoint.z;
        int totalPoints = 100; // Increased for better vertical resolution

        Gizmos.color = Color.cyan;
        Vector3? prevStbd = null;
        Vector3? prevPort = null;
        
        List<float> zSlices = new List<float>();
        for (int i = 0; i < totalPoints; i++)
        {
            float t = i / (float)(totalPoints - 1);
            float angle = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, t);
            float mappedT = (Mathf.Sin(angle) + 1f) / 2f;
            zSlices.Add(Mathf.Lerp(startZ, endZ, mappedT));
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

    public Vector3? GetCutoffSurfacePointAtY(FrameProfile fp, float flipX, float targetY)
    {
        if (!fp.isValid) return null;
        if (GetTAtY(fp, targetY, out float t, out bool isUpper))
        {
            if (isUpper)
            {
                if (fp.hasUpper && t >= fp.upperCutoffT && t <= fp.upperTopCutoffT)
                    return GetUpperSurfacePointAtT(fp, flipX, t);
            }
            else
            {
                if (fp.hasLower && t >= fp.lowerCutoffT && t <= fp.lowerTopCutoffT)
                    return GetSurfacePointAtT(fp, flipX, t, true);
            }
        }
        return null;
    }

    private Vector3 GetExactFashionTimberCutoffPointByOffset(float yOffsetFromHob, bool isUpper, float flipX)
    {
        if (sheerPlan == null) return Vector3.zero;

        float minZ = sheerPlan.GetAftMostZ();
        float maxZ = 0f;

        for (int step = 0; step < 25; step++)
        {
            float midZ = (minZ + maxZ) * 0.5f;
            float yMid = isUpper
                ? sheerPlan.GetTheoreticalHobYAtZ(midZ) + yOffsetFromHob
                : sheerPlan.GetTheoreticalHobYAtZ(midZ) - yOffsetFromHob;
            float stemZMid = sheerPlan.GetSternProfileZAtY(yMid);

            if (midZ < stemZMid) minZ = midZ;
            else maxZ = midZ;
        }

        float exactZ = (minZ + maxZ) * 0.5f;
        float exactY = isUpper
            ? sheerPlan.GetTheoreticalHobYAtZ(exactZ) + yOffsetFromHob
            : sheerPlan.GetTheoreticalHobYAtZ(exactZ) - yOffsetFromHob;

        FrameProfile fpRaw = GetRawFrameProfile(exactZ, true);
        Vector3? pt = GetSurfacePointAtY(fpRaw, flipX, exactY);

        float hw = (bodyPlan != null) ? bodyPlan.keelWidth / 2f : 0.05f;
        float xVal = pt.HasValue ? pt.Value.x : Mathf.Sign(flipX) * hw;
        return new Vector3(xVal, exactY, exactZ);
    }

    private bool GetExactDeadwoodCutoffPointByOffset(float yOffsetFromHob, bool isUpper, float flipX, out Vector3 cutoffPoint)
    {
        cutoffPoint = Vector3.zero;
        if (sheerPlan == null || !sheerPlan.showDeadwoodRake || !cutFramesAtDeadwood) return false;

        float foreZ = sheerPlan.cachedDeadwoodForePoint.z;
        float aftZ = sheerPlan.cachedDeadwoodAftPoint.z;
        float minDZ = Mathf.Min(aftZ, foreZ);
        float maxDZ = Mathf.Max(aftZ, foreZ);

        float yAtAft = isUpper
            ? sheerPlan.GetTheoreticalHobYAtZ(minDZ) + yOffsetFromHob
            : sheerPlan.GetTheoreticalHobYAtZ(minDZ) - yOffsetFromHob;
        float dYAtAft = sheerPlan.GetDeadwoodYAtZ(minDZ);

        if (yAtAft >= dYAtAft) return false;

        float minZ = minDZ;
        float maxZ = maxDZ;

        for (int step = 0; step < 25; step++)
        {
            float midZ = (minZ + maxZ) * 0.5f;
            float yMid = isUpper
                ? sheerPlan.GetTheoreticalHobYAtZ(midZ) + yOffsetFromHob
                : sheerPlan.GetTheoreticalHobYAtZ(midZ) - yOffsetFromHob;
            float dYMid = sheerPlan.GetDeadwoodYAtZ(midZ);

            if (yMid < dYMid) minZ = midZ;
            else maxZ = midZ;
        }

        float exactZ = (minZ + maxZ) * 0.5f;
        float exactY = sheerPlan.GetDeadwoodYAtZ(exactZ);

        FrameProfile fpRaw = GetRawFrameProfile(exactZ, true);
        Vector3? pt = GetSurfacePointAtY(fpRaw, flipX, exactY);
        float hw = (bodyPlan != null) ? bodyPlan.keelWidth / 2f : 0.05f;
        float xVal = pt.HasValue ? pt.Value.x : Mathf.Sign(flipX) * hw;

        cutoffPoint = new Vector3(xVal, exactY, exactZ);
        return true;
    }

    private Vector3 GetExactBowCutoffPointByOffset(float yOffsetFromHob, bool isUpper, float flipX)
    {
        if (sheerPlan == null) return Vector3.zero;

        float minZ = 0f;
        float maxZ = sheerPlan.cachedUpperStemTopPoint.z;

        for (int step = 0; step < 25; step++)
        {
            float midZ = (minZ + maxZ) * 0.5f;
            float yMid = isUpper
                ? sheerPlan.GetTheoreticalHobYAtZ(midZ) + yOffsetFromHob
                : sheerPlan.GetTheoreticalHobYAtZ(midZ) - yOffsetFromHob;
            float stemZMid = sheerPlan.GetStemProfileZAtY(yMid);

            if (midZ < stemZMid) minZ = midZ;
            else maxZ = midZ;
        }

        float exactZ = (minZ + maxZ) * 0.5f;
        float exactY = isUpper
            ? sheerPlan.GetTheoreticalHobYAtZ(exactZ) + yOffsetFromHob
            : sheerPlan.GetTheoreticalHobYAtZ(exactZ) - yOffsetFromHob;
        float exactStemZ = sheerPlan.GetStemProfileZAtY(exactY);

        float hw = (bodyPlan != null) ? bodyPlan.keelWidth / 2f : 0.05f;
        return new Vector3(Mathf.Sign(flipX) * hw, exactY, exactStemZ);
    }

    private void ProcessRibbandChannelByY(
        ref Vector3? prevStbd, ref Vector3? prevPort,
        float prevZ, float currZ, float yOffsetFromHob, bool isUpper,
        FrameProfile fp, bool isFirstPoint,
        Vector3 stbdSternCut, Vector3 portSternCut, float sternCutoffZ,
        Vector3 stbdBowCut, Vector3 portBowCut, float bowCutoffZ)
    {
        float targetY = isUpper
            ? sheerPlan.GetTheoreticalHobYAtZ(currZ) + yOffsetFromHob
            : sheerPlan.GetTheoreticalHobYAtZ(currZ) - yOffsetFromHob;

        bool pastFashion = currZ < sternCutoffZ - 0.001f;
        bool pastStem = currZ > bowCutoffZ + 0.001f;

        FrameProfile fpRaw = GetRawFrameProfile(currZ, true);
        Vector3? stbdPt = (pastStem || pastFashion) ? null : GetSurfacePointAtY(fpRaw, 1f, targetY);
        Vector3? portPt = (pastStem || pastFashion) ? null : GetSurfacePointAtY(fpRaw, -1f, targetY);

        bool isValid = stbdPt.HasValue && portPt.HasValue;

        if (isValid)
        {
            Vector3 currStbd = stbdPt.Value;
            Vector3 currPort = portPt.Value;

            if (prevStbd.HasValue)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
                Gizmos.DrawLine(prevStbd.Value, currStbd);
                Gizmos.DrawLine(prevPort.Value, currPort);
            }
            else if (!isFirstPoint)
            {
                // Transition from INVALID (stern Fashion Timber/Deadwood) to VALID
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
                Gizmos.DrawLine(stbdSternCut, currStbd);
                Gizmos.DrawLine(portSternCut, currPort);

                if (showRibbandCutoffDots)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(stbdSternCut, 0.08f);
                    Gizmos.DrawSphere(portSternCut, 0.08f);
                }
            }

            prevStbd = currStbd;
            prevPort = currPort;
        }
        else
        {
            if (prevStbd.HasValue)
            {
                // Transition from VALID to INVALID (Bow Stem profile termination point)
                if (pastStem)
                {
                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
                    Gizmos.DrawLine(prevStbd.Value, stbdBowCut);
                    Gizmos.DrawLine(prevPort.Value, portBowCut);

                    if (showRibbandCutoffDots)
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere(stbdBowCut, 0.08f);
                        Gizmos.DrawSphere(portBowCut, 0.08f);
                    }
                }

                prevStbd = null;
                prevPort = null;
            }
            else
            {
                prevStbd = null;
                prevPort = null;
            }
        }
    }

    private void DrawSurfaceRibbands()
    {
        float startZ = sheerPlan.GetAftMostZ();
        float endZ = sheerPlan.cachedUpperStemTopPoint.z;
        int totalPoints = 100;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
#if UNITY_EDITOR
        Handles.color = new Color(1f, 0.5f, 0f, 0.8f);
#endif

        // Measure reference heights at midship to compute target ribband counts and offsets
        FrameProfile midFp = GetFrameProfile(0f);
        float hLower = midFp.isValid ? Mathf.Abs(midFp.lowerP3.y - midFp.lowerP0.y) : 1f;
        float hUpper = (midFp.isValid && midFp.hasUpper) ? Mathf.Abs(midFp.upperP3.y - midFp.upperP0.y) : 1f;

        float spacing = Mathf.Max(0.05f, targetRibbandSpacing);
        int lowerCount = useTargetSpacing ? Mathf.Max(1, Mathf.RoundToInt(hLower / spacing) - 1) : manualLowerRibbandCount;
        int upperCount = useTargetSpacing ? Mathf.Max(1, Mathf.RoundToInt(hUpper / spacing) - 1) : manualUpperRibbandCount;

        Vector3?[] prevStbdLower = new Vector3?[lowerCount];
        Vector3?[] prevPortLower = new Vector3?[lowerCount];
        Vector3?[] prevStbdUpper = new Vector3?[upperCount];
        Vector3?[] prevPortUpper = new Vector3?[upperCount];

        float[] yOffsetLower = new float[lowerCount];
        for (int k = 0; k < lowerCount; k++)
        {
            float ratio = (k + 1) / (float)(lowerCount + 1);
            yOffsetLower[k] = hLower * ratio; // Offset below HoB at midship
        }

        float[] yOffsetUpper = new float[upperCount];
        for (int k = 0; k < upperCount; k++)
        {
            float ratio = (k + 1) / (float)(upperCount + 1);
            yOffsetUpper[k] = hUpper * ratio; // Offset above HoB at midship
        }

        // Precompute channel cutoff points once before iterating over Z
        Vector3[] stbdSternCutLower = new Vector3[lowerCount];
        Vector3[] portSternCutLower = new Vector3[lowerCount];
        float[] sternCutoffZLower = new float[lowerCount];

        Vector3[] stbdBowCutLower = new Vector3[lowerCount];
        Vector3[] portBowCutLower = new Vector3[lowerCount];
        float[] bowCutoffZLower = new float[lowerCount];

        for (int k = 0; k < lowerCount; k++)
        {
            bool isDeadwoodCut = GetExactDeadwoodCutoffPointByOffset(yOffsetLower[k], false, 1f, out stbdSternCutLower[k]);
            if (isDeadwoodCut)
            {
                GetExactDeadwoodCutoffPointByOffset(yOffsetLower[k], false, -1f, out portSternCutLower[k]);
            }
            else
            {
                stbdSternCutLower[k] = GetExactFashionTimberCutoffPointByOffset(yOffsetLower[k], false, 1f);
                portSternCutLower[k] = GetExactFashionTimberCutoffPointByOffset(yOffsetLower[k], false, -1f);
            }
            sternCutoffZLower[k] = stbdSternCutLower[k].z;

            stbdBowCutLower[k] = GetExactBowCutoffPointByOffset(yOffsetLower[k], false, 1f);
            portBowCutLower[k] = GetExactBowCutoffPointByOffset(yOffsetLower[k], false, -1f);
            bowCutoffZLower[k] = stbdBowCutLower[k].z;
        }

        Vector3[] stbdSternCutUpper = new Vector3[upperCount];
        Vector3[] portSternCutUpper = new Vector3[upperCount];
        float[] sternCutoffZUpper = new float[upperCount];

        Vector3[] stbdBowCutUpper = new Vector3[upperCount];
        Vector3[] portBowCutUpper = new Vector3[upperCount];
        float[] bowCutoffZUpper = new float[upperCount];

        for (int k = 0; k < upperCount; k++)
        {
            bool isDeadwoodCut = GetExactDeadwoodCutoffPointByOffset(yOffsetUpper[k], true, 1f, out stbdSternCutUpper[k]);
            if (isDeadwoodCut)
            {
                GetExactDeadwoodCutoffPointByOffset(yOffsetUpper[k], true, -1f, out portSternCutUpper[k]);
            }
            else
            {
                stbdSternCutUpper[k] = GetExactFashionTimberCutoffPointByOffset(yOffsetUpper[k], true, 1f);
                portSternCutUpper[k] = GetExactFashionTimberCutoffPointByOffset(yOffsetUpper[k], true, -1f);
            }
            sternCutoffZUpper[k] = stbdSternCutUpper[k].z;

            stbdBowCutUpper[k] = GetExactBowCutoffPointByOffset(yOffsetUpper[k], true, 1f);
            portBowCutUpper[k] = GetExactBowCutoffPointByOffset(yOffsetUpper[k], true, -1f);
            bowCutoffZUpper[k] = stbdBowCutUpper[k].z;
        }

        float prevZ = startZ;

        for (int i = 0; i < totalPoints; i++)
        {
            float t = i / (float)(totalPoints - 1);
            float angle = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, t);
            float mappedT = (Mathf.Sin(angle) + 1f) / 2f;
            float z = Mathf.Lerp(startZ, endZ, mappedT);

            FrameProfile fp = GetFrameProfile(z);
            bool isFirstPoint = (i == 0);

            if (!fp.isValid)
            {
                for (int k = 0; k < lowerCount; k++) { prevStbdLower[k] = null; prevPortLower[k] = null; }
                for (int k = 0; k < upperCount; k++) { prevStbdUpper[k] = null; prevPortUpper[k] = null; }
                prevZ = z;
                continue;
            }

            // Lower Mould Surface Ribbands (sheer-parallel height offset below HoB)
            for (int k = 0; k < lowerCount; k++)
            {
                ProcessRibbandChannelByY(
                    ref prevStbdLower[k], ref prevPortLower[k], prevZ, z, yOffsetLower[k], false, fp, isFirstPoint,
                    stbdSternCutLower[k], portSternCutLower[k], sternCutoffZLower[k],
                    stbdBowCutLower[k], portBowCutLower[k], bowCutoffZLower[k]);
            }

            // Upper Mould Surface Ribbands (sheer-parallel height offset above HoB)
            for (int k = 0; k < upperCount; k++)
            {
                ProcessRibbandChannelByY(
                    ref prevStbdUpper[k], ref prevPortUpper[k], prevZ, z, yOffsetUpper[k], true, fp, isFirstPoint,
                    stbdSternCutUpper[k], portSternCutUpper[k], sternCutoffZUpper[k],
                    stbdBowCutUpper[k], portBowCutUpper[k], bowCutoffZUpper[k]);
            }

            prevZ = z;
        }

        // Guarantee every remaining active channel terminates exactly on the Bow Stem Line
        for (int k = 0; k < lowerCount; k++)
        {
            if (prevStbdLower[k].HasValue)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
                Gizmos.DrawLine(prevStbdLower[k].Value, stbdBowCutLower[k]);
                Gizmos.DrawLine(prevPortLower[k].Value, portBowCutLower[k]);

                if (showRibbandCutoffDots)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(stbdBowCutLower[k], 0.08f);
                    Gizmos.DrawSphere(portBowCutLower[k], 0.08f);
                }
            }
        }

        for (int k = 0; k < upperCount; k++)
        {
            if (prevStbdUpper[k].HasValue)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
                Gizmos.DrawLine(prevStbdUpper[k].Value, stbdBowCutUpper[k]);
                Gizmos.DrawLine(prevPortUpper[k].Value, portBowCutUpper[k]);

                if (showRibbandCutoffDots)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(stbdBowCutUpper[k], 0.08f);
                    Gizmos.DrawSphere(portBowCutUpper[k], 0.08f);
                }
            }
        }
    }

    private void DrawWaterlines()
    {
        float startZ = sheerPlan.cachedSternPostTopPoint.z;
        float endZ = sheerPlan.cachedStemTopPoint.z;
        float minY = Mathf.Min(sheerPlan.cachedMidshipKeel.y, 0f);
        float maxY = sheerPlan.cachedBaseDeckMidshipPoint.y;
        
        int totalPoints = 50; // Keeps performance high while drastically improving bow/stern resolution
        
        List<float> zPositions = new List<float>();
        for (int i = 0; i < totalPoints; i++)
        {
            float t = i / (float)(totalPoints - 1);
            
            // Use a sine wave to non-linearly map 't'. 
            // This natively clusters points densely at the 0 (stern) and 1 (bow) extremes.
            float angle = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, t);
            float mappedT = (Mathf.Sin(angle) + 1f) / 2f;
            
            zPositions.Add(Mathf.Lerp(startZ, endZ, mappedT));
        }

        // Precompute slices to save binary search operations
        FrameProfile[] profiles = new FrameProfile[totalPoints];
        for (int i = 0; i < totalPoints; i++)
        {
            profiles[i] = GetFrameProfile(zPositions[i]);
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

            for (int i = 0; i < totalPoints; i++)
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

    public bool GetTAtY(FrameProfile fp, float targetY, out float t, out bool isUpper)
    {
        t = 0f;
        isUpper = false;
        
        if (targetY < fp.lowerP0.y) return false;

        float z = fp.lowerP0.z;
        float localTtY = sheerPlan != null ? sheerPlan.GetTopTimberYAtZ(z) : fp.upperP3.y;
        if (targetY > localTtY + 0.001f) return false;

        if (fp.hasUpper && targetY > fp.lowerP3.y && targetY <= fp.upperP3.y)
        {
            float min = 0f;
            float max = 1f;
            for (int i = 0; i < 25; i++)
            {
                float mid = (min + max) * 0.5f;
                float py = CalculateCubicBezierY(mid, fp.upperP0.y, fp.upperP1.y, fp.upperP2.y, fp.upperP3.y);
                if (py < targetY) min = mid;
                else max = mid;
            }
            t = (min + max) * 0.5f;
            isUpper = true;
            return true;
        }

        if (targetY > fp.lowerP3.y) return false;

        float minL = 0f;
        float maxL = 1f;
        for (int i = 0; i < 25; i++)
        {
            float mid = (minL + maxL) * 0.5f;
            float py = GetSurfacePointAtT(fp, 1f, mid, true).y;
            if (py < targetY) minL = mid;
            else maxL = mid;
        }
        t = (minL + maxL) * 0.5f;
        isUpper = false;
        return true;
    }

    public Vector3? GetSurfacePointAtY(FrameProfile fp, float flipX, float targetY)
    {
        if (GetTAtY(fp, targetY, out float t, out bool isUpper))
        {
            if (isUpper) return GetUpperSurfacePointAtT(fp, flipX, t);
            else return GetSurfacePointAtT(fp, flipX, t, true);
        }
        return null;
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

    public float GetOverhangOuterXAndNormalAtYZ(float targetY, float originalZ, out float nx)
    {
        nx = 1f;
        bool isUpper = targetY >= sheerPlan.GetTheoreticalHobYAtZ(originalZ) - 0.001f;

        FrameProfile fp = GetRawFrameProfile(originalZ, true);
        if (!fp.isValid) return 0f;

        float exactX = 0f;

        float keelY = sheerPlan.GetKeelYAtZ(originalZ);
        if (sheerPlan.showDeadwoodRake && originalZ < sheerPlan.cachedDeadwoodForePoint.z && originalZ >= sheerPlan.cachedDeadwoodAftPoint.z)
        {
            keelY = sheerPlan.GetDeadwoodYAtZ(originalZ);
        }
        
        Vector3 origin = new Vector3(0, 0, originalZ);
        Vector3 pEnd = fp.hasUpper ? fp.upperP3 : fp.lowerP3;
        Vector3 uAxis = new Vector3(pEnd.x, 0, pEnd.z) - new Vector3(origin.x, 0, origin.z);
        if (uAxis.sqrMagnitude < 0.0001f) uAxis = Vector3.right;
        else uAxis.Normalize();
        Vector3 vAxis = Vector3.up;
        Vector3 framePlaneNormal = Vector3.Cross(uAxis, vAxis).normalized;

        if (isUpper && fp.hasUpper)
        {
            float tMinT = 0f;
            float tMaxT = 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (tMinT + tMaxT) * 0.5f;
                float py = GetUpperSurfacePointAtT(fp, 1f, midT).y;
                if (py < targetY) tMinT = midT;
                else tMaxT = midT;
            }
            float tVal = (tMinT + tMaxT) * 0.5f;
            Vector3 outerPt = GetUpperSurfacePointAtT(fp, 1f, tVal);
            exactX = outerPt.x;
            
            Vector3 normal = GetInnerNormalAtT(fp, true, tVal, originalZ, framePlaneNormal);
            nx = Mathf.Max(0.01f, Mathf.Abs(normal.x));
        }
        else if (!isUpper && fp.hasLower)
        {
            float fMinT = 0f;
            float fMaxT = 1f;
            for (int i = 0; i < 20; i++)
            {
                float midT = (fMinT + fMaxT) * 0.5f;
                float py = GetSurfacePointAtT(fp, 1f, midT, true).y;
                if (py < targetY) fMinT = midT;
                else fMaxT = midT;
            }
            float tVal = (fMinT + fMaxT) * 0.5f;
            Vector3 outerPt = GetSurfacePointAtT(fp, 1f, tVal, true);
            exactX = outerPt.x;
            
            Vector3 normal = GetInnerNormalAtT(fp, false, tVal, originalZ, framePlaneNormal);
            nx = Mathf.Max(0.01f, Mathf.Abs(normal.x));
        }

        return exactX;
    }

    public Vector3 GetSurfacePointAtT(FrameProfile fp, float flipX, float t, bool ignoreFashionTimber = false)
    {
        Vector3 p0 = new Vector3(fp.lowerP0.x, fp.lowerP0.y, fp.lowerP0.z);
        Vector3 p1 = new Vector3(fp.lowerP1.x, fp.lowerP1.y, fp.lowerP1.z);
        Vector3 pR = new Vector3(fp.lowerLR.x, fp.lowerLR.y, fp.lowerLR.z);
        Vector3 p2 = new Vector3(fp.lowerP2.x, fp.lowerP2.y, fp.lowerP2.z);
        Vector3 p3 = new Vector3(fp.lowerP3.x, fp.lowerP3.y, fp.lowerP3.z);

        Vector3 pt;
        if (t <= 0.5f)
        {
            float localT = t * 2f;
            pt = CalculateQuadraticBezierPoint(localT, p0, p1, pR);
        }
        else
        {
            float localT = (t - 0.5f) * 2f;
            pt = CalculateQuadraticBezierPoint(localT, pR, p2, p3);
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

    protected virtual void DrawDashedFrameAtZ(float z, Color color)
    {
        FrameProfile fp = GetFrameProfile(z);
        if (!fp.isValid) return;

        Gizmos.color = color;
        int segments = Mathf.Max(2, frameSegments);

        if (fp.hasLower)
        {
            float startT = fp.lowerCutoffT;
            float endT = fp.lowerTopCutoffT;
            
            Vector3 prevStbd = GetSurfacePointAtT(fp, 1f, startT);
            Vector3 prevPort = GetSurfacePointAtT(fp, -1f, startT);

            for (int i = 1; i <= segments; i++)
            {
                float t = startT + (endT - startT) * (i / (float)segments);

                Vector3 currStbd = GetSurfacePointAtT(fp, 1f, t);
                Vector3 currPort = GetSurfacePointAtT(fp, -1f, t);

                if (i % 2 != 0) // Draw every other segment to make it dashed
                {
                    Gizmos.DrawLine(prevStbd, currStbd);
                    Gizmos.DrawLine(prevPort, currPort);
                }

                prevStbd = currStbd;
                prevPort = currPort;
            }
        }
    }

    private void DrawFrameAtZ(float z)
    {
        FrameProfile fpCenter = GetFrameProfile(z);
        if (!fpCenter.isValid) return;

        if (_cachedForeFace == null)
        {
            _cachedForeFace = foreFaceFashionTimberModelTest != null ? foreFaceFashionTimberModelTest : Object.FindObjectOfType<ForeFaceFashionTimberModelTest3>();
        }

        bool isCutByFashionTimber = false;
        if (cutFramesAtFashionTimber && _cachedForeFace != null)
        {
            float sternMinZ = _cachedForeFace.GetMinZ();
            float sternMaxZ = _cachedForeFace.GetMaxZ();
            if (z >= sternMinZ - 0.001f && z <= sternMaxZ + 0.001f)
            {
                isCutByFashionTimber = true;
            }
        }

        Color currentColor = isCutByFashionTimber ? cutFrameColor : frameColor;
        Gizmos.color = currentColor;

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

        // Draw 3D curved connecting cut lines along Fashion Timber curve (Outer Frame Only)
        if (isCutByFashionTimber && _cachedForeFace != null)
        {
            bool isUpperMesh = fpCenter.isValid && fpCenter.hasUpper;

            int cutSubSteps = 40;
            Vector3 prevOuterStbd = Vector3.zero;
            Vector3 prevOuterPort = Vector3.zero;

            for (int s = 0; s <= cutSubSteps; s++)
            {
                float subZ = Mathf.Lerp(aftZ, foreZ, s / (float)cutSubSteps);
                Vector3 outerStbd = _cachedForeFace.GetFashionTimberPointAtZ(subZ, true, isUpperMesh);
                Vector3 outerPort = _cachedForeFace.GetFashionTimberPointAtZ(subZ, false, isUpperMesh);

                if (s > 0)
                {
                    Gizmos.DrawLine(prevOuterStbd, outerStbd);
                    Gizmos.DrawLine(prevOuterPort, outerPort);
                }

                prevOuterStbd = outerStbd;
                prevOuterPort = outerPort;
            }
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
            
            float labelY = fpCenter.hasUpper ? fpCenter.upperP3.y : fpCenter.lowerP3.y;
            Vector3 labelPos = new Vector3(0, labelY + 0.5f, z);
            string labelText = z == 0 ? "Master" : z > 0 ? $"F{z}" : $"A{Mathf.Abs(z)}";
            Handles.Label(labelPos, labelText, _labelStyle);
        }
#endif
    }

    private void DrawFillFrame(FillFrame fill)
    {
        if (!fill.isValid) return;

        float centerZ = (fill.startZ + fill.endZ) * 0.5f;
        bool isCutByFashionTimber = false;
        if (cutFramesAtFashionTimber && _cachedForeFace != null)
        {
            float sternMinZ = _cachedForeFace.GetMinZ();
            float sternMaxZ = _cachedForeFace.GetMaxZ();
            if (centerZ >= sternMinZ - 0.001f && centerZ <= sternMaxZ + 0.001f)
            {
                isCutByFashionTimber = true;
            }
        }

        Color baseColor = isCutByFashionTimber ? cutFrameColor : frameColor;
        Gizmos.color = new Color(baseColor.r * 0.65f, baseColor.g * 0.65f, baseColor.b * 0.65f, baseColor.a);

        if (fill.startFp.isValid)
        {
            DrawMould(fill.startFp, 1f, false);
            DrawMould(fill.startFp, -1f, false);
        }

        if (fill.endFp.isValid)
        {
            DrawMould(fill.endFp, 1f, false);
            DrawMould(fill.endFp, -1f, false);
        }

        if (fill.startFp.isValid && fill.endFp.isValid)
        {
            int edgeSteps = 4;
            for (int side = -1; side <= 1; side += 2)
            {
                float flipX = (float)side;

                if (fill.startFp.hasLower && fill.endFp.hasLower)
                {
                    for (int s = 0; s <= edgeSteps; s++)
                    {
                        float t = s / (float)edgeSteps;
                        Vector3 pStart = GetSurfacePointAtT(fill.startFp, flipX, t);
                        Vector3 pEnd = GetSurfacePointAtT(fill.endFp, flipX, t);
                        Gizmos.DrawLine(pStart, pEnd);
                    }
                }

                if (fill.startFp.hasUpper && fill.endFp.hasUpper)
                {
                    for (int s = 0; s <= edgeSteps; s++)
                    {
                        float t = s / (float)edgeSteps;
                        Vector3 pStart = GetUpperSurfacePointAtT(fill.startFp, flipX, t);
                        Vector3 pEnd = GetUpperSurfacePointAtT(fill.endFp, flipX, t);
                        Gizmos.DrawLine(pStart, pEnd);
                    }
                }
            }
        }

        // Draw 3D curved connecting cut lines along Fashion Timber curve for fill frames if cut
        if (isCutByFashionTimber && _cachedForeFace != null)
        {
            bool isUpperMesh = (fill.startFp.isValid && fill.startFp.hasUpper) || (fill.endFp.isValid && fill.endFp.hasUpper);
            float startZ = Mathf.Min(fill.startZ, fill.endZ);
            float endZ = Mathf.Max(fill.startZ, fill.endZ);

            int cutSubSteps = 40;
            Vector3 prevOuterStbd = Vector3.zero;
            Vector3 prevOuterPort = Vector3.zero;

            for (int s = 0; s <= cutSubSteps; s++)
            {
                float subZ = Mathf.Lerp(startZ, endZ, s / (float)cutSubSteps);
                Vector3 outerStbd = _cachedForeFace.GetFashionTimberPointAtZ(subZ, true, isUpperMesh);
                Vector3 outerPort = _cachedForeFace.GetFashionTimberPointAtZ(subZ, false, isUpperMesh);

                if (s > 0)
                {
                    Gizmos.DrawLine(prevOuterStbd, outerStbd);
                    Gizmos.DrawLine(prevOuterPort, outerPort);
                }

                prevOuterStbd = outerStbd;
                prevOuterPort = outerPort;
            }
        }
    }

    private void DrawMould(FrameProfile fp, float flipX, bool isDashed = false)
    {
        Color activeColor = Gizmos.color;

        if (fp.hasLower)
        {
            float startT = fp.lowerCutoffT;
            float endT = fp.lowerTopCutoffT;
            
            int segments = Mathf.Max(2, frameSegments);
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
                Vector3 p1 = new Vector3(fp.lowerP1.x * flipX, fp.lowerP1.y, fp.lowerP1.z);
                Vector3 pR = new Vector3(fp.lowerLR.x * flipX, fp.lowerLR.y, fp.lowerLR.z);
                Vector3 p2 = new Vector3(fp.lowerP2.x * flipX, fp.lowerP2.y, fp.lowerP2.z);
                Vector3 p3 = new Vector3(fp.lowerP3.x * flipX, fp.lowerP3.y, fp.lowerP3.z);
                
                Gizmos.DrawLine(p0, p1);
                Gizmos.DrawLine(p1, pR);
                Gizmos.DrawLine(pR, p2);
                Gizmos.DrawLine(p2, p3);
                Gizmos.color = activeColor;
            }
        }

        if (fp.hasUpper)
        {
            Gizmos.color = activeColor;
            Vector3 u0 = new Vector3(fp.upperP0.x * flipX, fp.upperP0.y, fp.upperP0.z);
            Vector3 u1 = new Vector3(fp.upperP1.x * flipX, fp.upperP1.y, fp.upperP1.z);
            Vector3 u2 = new Vector3(fp.upperP2.x * flipX, fp.upperP2.y, fp.upperP2.z);
            Vector3 u3 = new Vector3(fp.upperP3.x * flipX, fp.upperP3.y, fp.upperP3.z);
            
            float startT = fp.upperCutoffT;
            float endT = fp.upperTopCutoffT;
            
            int upperSegments = Mathf.Max(2, frameSegments);
            Vector3 prev = GetUpperSurfacePointAtT(fp, flipX, startT);
            for (int i = 1; i <= upperSegments; i++)
            {
                float t = startT + (endT - startT) * (i / (float)upperSegments);
                Vector3 curr = GetUpperSurfacePointAtT(fp, flipX, t);
                if (!isDashed || i % 2 != 0) Gizmos.DrawLine(prev, curr);
                prev = curr;
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
                Gizmos.color = activeColor;
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
        float delta = 0.0001f;
        float t1 = Mathf.Max(0f, t - delta);
        float t2 = Mathf.Min(1f, t + delta);

        Vector3 pt1 = isUpper ? GetUpperSurfacePointAtT(fp, 1f, t1) : GetSurfacePointAtT(fp, 1f, t1, true);
        Vector3 pt2 = isUpper ? GetUpperSurfacePointAtT(fp, 1f, t2) : GetSurfacePointAtT(fp, 1f, t2, true);

        Vector3 tangent = (pt2 - pt1).normalized;
        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.up;

        Vector3 outerPt = isUpper ? GetUpperSurfacePointAtT(fp, 1f, t) : GetSurfacePointAtT(fp, 1f, t, true);
        Vector3 normal = Vector3.Cross(tangent, framePlaneNormal).normalized;
        
        Vector3 toCenter = new Vector3(-outerPt.x, 0, zPos - outerPt.z);
        if (Vector3.Dot(normal, toCenter) < 0) normal = -normal;

        return normal;
    }

    public Vector3 GetInnerPointAtT(FrameProfile fp, bool isUpper, float t, float zPos, Vector3 origin, Vector3 framePlaneNormal, float keelY, float totalHeight)
    {
        float delta = 0.0001f;
        float t1 = Mathf.Max(0f, t - delta);
        float t2 = Mathf.Min(1f, t + delta);

        Vector3 pt1 = isUpper ? GetUpperSurfacePointAtT(fp, 1f, t1) : GetSurfacePointAtT(fp, 1f, t1, true);
        Vector3 pt2 = isUpper ? GetUpperSurfacePointAtT(fp, 1f, t2) : GetSurfacePointAtT(fp, 1f, t2, true);

        Vector3 tangent = (pt2 - pt1).normalized;
        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.up;

        Vector3 outerPt = isUpper ? GetUpperSurfacePointAtT(fp, 1f, t) : GetSurfacePointAtT(fp, 1f, t, true);

        Vector3 normal = Vector3.Cross(tangent, framePlaneNormal).normalized;
        
        Vector3 toCenter = new Vector3(-outerPt.x, 0, zPos - outerPt.z);
        if (Vector3.Dot(normal, toCenter) < 0) normal = -normal;

        float overallT = Mathf.Clamp01((outerPt.y - keelY) / totalHeight);
        float currentThickness = Mathf.Lerp(bodyPlan.baseFrameThickness, bodyPlan.topFrameThickness, overallT);
        
        return outerPt + normal * currentThickness;
    }
}
