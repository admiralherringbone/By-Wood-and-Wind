using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;

[ExecuteInEditMode]
public class SheerPlan : MonoBehaviour
{
    [Header("Gizmo Settings")]
    public bool showLabels = true;
    public bool showControlArms = true;
    [Tooltip("Color of the Main Sheer Line curve and gizmo markers in Scene view.")]
    public Color mainSheerLineColor = Color.yellow;
    [Tooltip("Color of the Height of Breadth (HoB) curve line and gizmo markers in Scene view.")]
    public Color hobLineColor = Color.cyan;
    [Tooltip("Draw the Diagonal Bilge Height curve in profile view.")]
    public bool showBilgeLine = true;
    [Tooltip("Color of the Diagonal Bilge Height curve line in Scene view.")]
    public Color bilgeLineColor = new Color(0.1f, 0.9f, 0.4f, 0.9f);

    [Tooltip("Draw a grey dashed line extending aft from the Keel Stern Point to the furthest aft Z coordinate.")]
    public bool showProjectedAftKeelLine = true;
    [Tooltip("Draw a grey dashed curve continuing the Stern Rising Line curve extending aft to the furthest aft Z coordinate.")]
    public bool showProjectedSternRisingLine = true;
    [Tooltip("Draw a grey dashed curve continuing the Bow Rising Line curve extending forward to the furthest forward Z coordinate.")]
    public bool showProjectedBowRisingLine = true;
    [Tooltip("Draw a grey dashed curve continuing the Bow Bilge Line curve extending forward to the furthest forward Z coordinate.")]
    public bool showProjectedBowBilgeLine = true;
    [Tooltip("Draw a grey dashed curve continuing the Stern Bilge Line curve extending aft to the furthest aft Z coordinate.")]
    public bool showProjectedSternBilgeLine = true;
    [Tooltip("Draw a grey dashed horizontal line extending from the furthest forward to furthest aft Z coordinates.")]
    public bool showProjectedTopLine = true;

    [Header("Basic Dimensions")]
    [Tooltip("The basic length of the model between the Stem Rake and the Stern Post Rake.")]
    public float lengthBetweenPerpendiculars = 20f;

    [Tooltip("The vertical height of the Lower Hull from the Keel up to the Base Deck.")]
    public float lowerHullHeight = 5f;

    [Tooltip("The total physical height of the Upper Hull (from Base Deck to Upper Stem/Stern).")]
    public float upperHullHeight = 7f;

    [Header("Keel & Stem Thickness")]
    [Tooltip("The vertical thickness of the keel and stem.")]
    public float keelThickness = 0.5f;
    [HideInInspector] public bool drawOuterKeelProfile = true;
    [HideInInspector] public bool drawDetailedLines = true;
    [Tooltip("Percentage of the keel thickness at the top of the stern post (1 = full thickness, 0 = sharp point).")]
    [Range(0f, 1f)] public float sternPostThickness = 1f;
    [Tooltip("If true, the top of the stem is cut flush with the sheer line. If false, it is cut perpendicular to the stem curvature.")]
    public bool stemInheritsSheerAngle = true;
    [HideInInspector, SerializeField] private float lastLowerHullHeight = 5f;

    [Tooltip("The vertical height of the lowest compartment (from the Keel to the first Lower Deck).")]
    public float holdDepth = 5f;
    [HideInInspector, SerializeField] private float lastHoldDepth = 5f;

    // TODO: Investigate if a curved keel is mathematically necessary. Hidden from inspector for now to prevent structural conflicts.
    [HideInInspector]
    public float keelMidshipDrop = 0f;
    [Header("Stem Rake")]
    [Tooltip("Percentage of the length for the Stem Rake.")]
    [Range(0f, 0.5f)] public float stemRakePercentage = 0.15f;
    
    [Header("Stem Rake Curve Controls")]
    [Range(0f, 1f)] public float stemBottomControlZ = 0.5f;
    [Range(0f, 1f)] public float stemBottomControlY = 0f;
    [Range(0f, 1f)] public float stemTopControlZ = 1f;
    [Range(0f, 1f)] public float stemTopControlY = 0.5f;

    [Header("Stern Post Rake")]
    [Tooltip("Percentage of the length for the Stern Post Rake.")]
    [Range(0f, 0.5f)] public float sternPostRakePercentage = 0.05f;

    // Deadwood Rake (removed customization per requirements)
    [HideInInspector] public bool showDeadwoodRake = false;
    [HideInInspector] public float deadwoodRakeForeSlider = 0.8f;
    [HideInInspector] public float deadwoodRakeAftSlider = 0.2f;
    [HideInInspector] public float deadwoodBottomControlZ = 0.5f;
    [HideInInspector] public float deadwoodBottomControlY = 0f;
    [HideInInspector] public float deadwoodTopControlZ = 1f;
    [HideInInspector] public float deadwoodTopControlY = 0.5f;

    [Header("Counter")]
    [Tooltip("Enable or disable the optional Counter overhang at the stern.")]
    public bool enableCounter = true;
    [Tooltip("Slide the counter vertically. -1 is below the Base Deck, 1 is above.")]
    [Range(-1f, 1f)] public float counterVerticalSlide = 0f;
    public float counterLength = 2f;
    [Tooltip("The height of the counter as a ratio of the total stern height.")]
    [Range(0f, 1f)] public float counterHeightRatio = 0.5f;
    [Range(0f, 1f)] public float counterCurveAngle = 0.5f;
    [Range(0f, 1f)] public float counterCurveLength = 0.5f;

    [Header("Upper Deck Sheer Line")]
    [Tooltip("The vertical height of the Upper Deck Sheer line (Bulwark) above the Base Deck sheer.")]
    public float bulwarkHeight = 1.5f;

    [Tooltip("Extra bulwark height added at the bow (applies globally to the highest deck).")]
    public float bulwarkSheerFwd = 0f;
    [Tooltip("Extra bulwark height added at the stern (applies globally to the highest deck).")]
    public float bulwarkSheerAft = 0f;

    [Header("Beakhead Bulwark Transition")]
    [Tooltip("Enable a custom bulwark height drop at the very bow of the ship.")]
    public bool enableBeakheadBulwark = false;
    [Tooltip("The dropped height of the bulwark at the very bow.")]
    [Range(0f, 10f)] public float beakheadBulwarkHeight = 1f;
    [Tooltip("Percentage from the bow where the drop begins (0 = At Bow, 1 = At Stern).")]
    [Range(0f, 1f)] public float beakheadBulwarkStartPercent = 0.1f;
    [Tooltip("Use a smooth Bezier curve instead of a straight slope.")]
    public bool curveBeakheadBulwark = false;
    [Tooltip("Angle of the top curve control arm (0 = flat, 1 = vertical).")]
    [Range(0f, 1f)] public float beakheadBulwarkTopCurveAngle = 0.5f;
    [Tooltip("Angle of the bottom curve control arm (0 = flat, 1 = vertical).")]
    [Range(0f, 1f)] public float beakheadBulwarkBottomCurveAngle = 0.5f;
    [Tooltip("Length of the curve control arms.")]
    [Range(0f, 1f)] public float beakheadBulwarkCurveLength = 0.5f;

    [System.Serializable]
    public class ModularUpperDeck
    {
        [Tooltip("The height this deck adds to the sheer line.")]
        public float height = 2f;
        [Tooltip("Relative percentage from the aft-most point of its parent deck (0 = Aft, 1 = Fore).")]
        [Range(0f, 1f)] public float aftExtent = 0f;
        [Tooltip("Relative percentage from the aft-most point of its parent deck (0 = Aft, 1 = Fore).")]
        [Range(0f, 1f)] public float foreExtent = 1f;

        [Tooltip("The ID of the deck this rests upon. (0 = Base Main Deck).")]
        public int parentDeckID = 0;
        
        [Tooltip("The height of the bulwark drawn in the negative space above this deck.")]
        [Range(0f, 10f)]
        public float bulwarkHeight = 1f;
        
        [Header("Hierarchy Info (Auto-Generated)")]
        public string name = "Upper Deck";
        [Tooltip("Use this ID as the 'Parent Deck ID' for any deck you want to stack on top of this one.")]
        public int deckID;
        public int tierLevel = 1;
        
        [HideInInspector] public float globalAftExtent = 0f;
        [HideInInspector] public float globalForeExtent = 1f;
    }

    [System.Serializable]
    public class ModularLowerDeck
    {
        [Tooltip("The vertical space this deck tier occupies.")]
        public float height = 2f;
        
        [Tooltip("Relative percentage from the aft-most point of the hull (0 = Aft, 1 = Fore).")]
        [Range(0f, 1f)] public float aftExtent = 0f;
        [Tooltip("Relative percentage from the aft-most point of the hull (0 = Aft, 1 = Fore).")]
        [Range(0f, 1f)] public float foreExtent = 1f;

        [Tooltip("Integer defining which vertical slice this deck occupies (1 = highest lower deck, 2 = next one down).")]
        public int tierLevel = 1;
        
        public string name = "Lower Deck";
    }

    [Header("Upper Decks")]
    [Tooltip("Add upper decks to automatically step up the Top Timber sheer line.")]
    public List<ModularUpperDeck> upperDecks = new List<ModularUpperDeck>();

    [Header("Lower Decks")]
    [Tooltip("Add lower decks to subdivide the Lower Hull Height.")]
    public List<ModularLowerDeck> lowerDecks = new List<ModularLowerDeck>();

    [Header("Transom Rake Line")]
    [Tooltip("How far aft the transom extends from the Counter Top / Base Deck up to the Top Timber line.")]
    public float transomAftRake = 0.5f;

    [Header("Base Deck Line")]
    [Tooltip("The sheer rise of the base deck at the bow (bounded between 0 and Main Sheer Fwd).")]
    public float baseDeckSheerFwd = 0f;
    [Tooltip("The sheer rise of the base deck at the stern (bounded between 0 and Main Sheer Aft).")]
    public float baseDeckSheerAft = 0f;

    [System.Obsolete("Use mainSheerCurveControlLength instead. Base Deck control arms match Main Sheer Line.")]
    public float baseDeckCurveControlLength
    {
        get => mainSheerCurveControlLength;
        set => mainSheerCurveControlLength = value;
    }

    [Header("Main Sheer Line (Top Guide Line)")]
    [Tooltip("The sheer rise of the main sheer line at the bow.")]
    public float mainSheerFwd = 0f;
    [Tooltip("The sheer rise of the main sheer line at the stern.")]
    public float mainSheerAft = 0f;
    [Tooltip("How much the main sheer curve is biased towards the extremities vs the midship.")]
    [Range(0.1f, 1f)] public float mainSheerCurveControlLength = 0.6f;

    [Tooltip("If true, the Main Sheer Line control handle Z-distances slave to BreadthPlan's topTimberMidshipFwdArmLength, topTimberMidshipAftArmLength, and topTimberBowArmLength for 2D curve synchronization.")]
    public bool slaveMainSheerHandlesToBreadthPlan = true;

    [Header("Main Wale Line (Dependent Mid-Hull Strake)")]
    [Tooltip("Draw the Main Wale line in profile view gizmos.")]
    public bool showMainWale = true;
    [Tooltip("Color of the Main Wale line in profile view.")]
    public Color mainWaleColor = new Color(0.9f, 0.6f, 0.1f, 0.9f);
    [Tooltip("Relative height position of the Main Wale between HOB (0.0) and Main Sheer Line (1.0).")]
    [Range(0f, 1f)] public float mainWaleHeightRatio = 0.35f;

    [Header("Rising Lines (Height of Breadth)")]
    [Tooltip("If checked, allows independent customization of the HoB line. If unchecked, the HoB perfectly matches the Base Deck line heights.")]
    public bool customizeHob = false;

    [Tooltip("The height of the HoB at the Midship section.")]
    public float hobMidshipHeight = 2.5f;
    [Tooltip("The height of the HoB at the Stem Rake intersection.")]
    public float hobBowHeight = 3.5f;
    [Tooltip("The height of the HoB at the Stern Post intersection.")]
    public float hobSternHeight = 3.5f;
    [Tooltip("Shifts the lowest point of the HoB aft of the midship (0).")]
    [Range(-10f, 10f)] public float hobDropAft = -1f;
    [Tooltip("How much the HoB curve is biased towards the extremities vs the midship.")]
    [Range(0.1f, 1f)] public float hobCurveControlLength = 0.5f;

    [Tooltip("If true, the HoB control handle Z-distances slave to BreadthPlan's midshipFwdArmLength and midshipAftArmLength for 2D curve synchronization.")]
    public bool slaveHobHandlesToBreadthPlan = true;

    [Tooltip("Reference to the BreadthPlan to sync HoB control handles for 2D curve synchronization.")]
    public BreadthPlan breadthPlan;

    [Header("Floor Line / Rising Line Controls")]
    [Tooltip("Reference to the ModularBodyPlan to sync Midship Rising Line height.")]
    public BodyPlan bodyPlan;

    [Tooltip("Height of the Floor Line / Rising Line at Midships (Z = 0) above the Keel baseline.")]
    public float midshipRisingHeight = 0.5f;

    /// <summary>
    /// Alias for midshipRisingHeight (Floor Line height at midships).
    /// </summary>
    public float midshipFloorHeight
    {
        get => midshipRisingHeight;
        set => midshipRisingHeight = value;
    }

    [Header("Diagonal Bilge Height Controls")]
    [Tooltip("Relative height of the Diagonal Bilge Line between Floor Line (0.0) and HOB (1.0).")]
    [Range(0.05f, 0.95f)]
    public float bilgeHeightRatio = 0.5f;

    [Tooltip("If true, the Rising Line control handle Z-distances slave to BreadthPlan's floorBowControl and floorSternControl for 2D curve synchronization.")]
    public bool slaveRisingLineHandlesToBreadthPlan = true;

    [Tooltip("The vertical position where the Bow Rising Line terminates on the Stem Rake (0 = Keel, 1 = HoB).")]
    [Range(0f, 1f)] public float bowRisingLineEndPos = 1f;
    [Tooltip("How far forward the control arm for the Bow Rising Line extends (0 = Midship, 1 = Max forward tangency).")]
    [Range(0f, 1f)] public float bowRisingLineControl = 0.5f;

    [Tooltip("The vertical position where the Stern Rising Line terminates on the Stern Post (0 = Keel, 1 = Stern Post Top).")]
    [Range(0f, 1f)] public float sternRisingLineEndPos = 1f;
    [Tooltip("How far aft the control arm for the Stern Rising Line extends (0 = Midship, 1 = Keel Stern).")]
    [Range(0f, 1f)] public float sternRisingLineControl = 0.5f;

    // --- CACHED POINTS FOR FAST ACCESS ---
    [HideInInspector] public Vector3 cachedKeelSternPoint;
    [HideInInspector] public Vector3 cachedKeelBowPoint;
    [HideInInspector] public Vector3 cachedMidshipKeel;
    [HideInInspector] public Vector3 cachedMidshipRisingPoint;
    [HideInInspector] public Vector3 cachedProjectedAftKeelEnd;
    [HideInInspector] public Vector3 cachedProjectedTopLineStart;
    [HideInInspector] public Vector3 cachedProjectedTopLineEnd;
    
    [HideInInspector] public Vector3 cachedBowRisingEnd;
    [HideInInspector] public Vector3 cachedBowP1Ctrl;
    [HideInInspector] public float cachedBowRisingLineEndT = 1f;
    [HideInInspector] public Vector3 cachedProjectedBowRisingEnd;
    [HideInInspector] public Vector3 cachedProjectedBowBilgeEnd;
    [HideInInspector] public Vector3 cachedSternRisingEnd;
    [HideInInspector] public Vector3 cachedSternP1Ctrl;
    [HideInInspector] public Vector3 cachedProjectedSternRisingEnd;
    [HideInInspector] public Vector3 cachedProjectedSternBilgeEnd;
    [HideInInspector] public float cachedSternRisingLineEndT = 1f;
    [HideInInspector] public int changeVersion = 0;

    [HideInInspector] public Vector3 cachedStemTopPoint;
    [HideInInspector] public Vector3 cachedSternPostTopPoint;
    
    [HideInInspector] public Vector3 cachedCounterBasePoint;
    [HideInInspector] public Vector3 cachedCounterTopPoint;
    [HideInInspector] public Vector3 cachedCounterCp1;
    [HideInInspector] public Vector3 cachedCounterCp2;
    
    [HideInInspector] public Vector3 cachedDeadwoodForePoint;
    [HideInInspector] public Vector3 cachedDeadwoodAftPoint;
    [HideInInspector] public Vector3 cachedDeadwoodP1;
    [HideInInspector] public Vector3 cachedDeadwoodP2;
    
    [HideInInspector] public Vector3 cachedBaseDeckSternIntersect;
    [HideInInspector] public Vector3 cachedBaseDeckSternCp;
    [HideInInspector] public Vector3 cachedBaseDeckMidCpAft;
    [HideInInspector] public Vector3 cachedBaseDeckMidshipPoint;
    [HideInInspector] public Vector3 cachedBaseDeckMidCpFwd;
    [HideInInspector] public Vector3 cachedBaseDeckBowCp;
    [HideInInspector] public Vector3 cachedBaseDeckBowIntersect;
    
    [HideInInspector] public Vector3 cachedMainSheerSternIntersect;
    [HideInInspector] public Vector3 cachedMainSheerSternCp;
    [HideInInspector] public Vector3 cachedMainSheerMidCpAft;
    [HideInInspector] public Vector3 cachedMainSheerMidshipPoint;
    [HideInInspector] public Vector3 cachedMainSheerMidCpFwd;
    [HideInInspector] public Vector3 cachedMainSheerBowCp;
    [HideInInspector] public Vector3 cachedMainSheerBowIntersect;
    
    [HideInInspector] public Vector3 cachedUpperStemTopPoint;
    [HideInInspector] public Vector3 cachedTransomTopPoint;
    
    [HideInInspector] public Vector3 cachedTopTimberSternIntersect;
    [HideInInspector] public Vector3 cachedTopTimberSternCp;
    [HideInInspector] public Vector3 cachedTopTimberMidCpAft;
    [HideInInspector] public Vector3 cachedTopTimberMidshipPoint;
    [HideInInspector] public Vector3 cachedTopTimberMidCpFwd;
    [HideInInspector] public Vector3 cachedTopTimberBowCp;
    [HideInInspector] public Vector3 cachedTopTimberBowIntersect;

    [HideInInspector] public Vector3 cachedHobSternIntersect;
    [HideInInspector] public Vector3 cachedHobSternCp;
    [HideInInspector] public Vector3 cachedHobMidCpAft;
    [HideInInspector] public Vector3 cachedHobMidshipPoint;
    [HideInInspector] public Vector3 cachedHobMidCpFwd;
    [HideInInspector] public Vector3 cachedHobBowCp;
    [HideInInspector] public Vector3 cachedHobBowIntersect;
    
    [HideInInspector] public Vector3 cachedStemP1;
    [HideInInspector] public Vector3 cachedStemP2;

    private bool isUpdatingValues = false;

    private void OnValidate()
    {
        bilgeHeightRatio = Mathf.Clamp(bilgeHeightRatio, 0.05f, 0.95f);
        midshipRisingHeight = Mathf.Max(0f, midshipRisingHeight);
        mainSheerFwd = Mathf.Max(0f, mainSheerFwd);
        mainSheerAft = Mathf.Max(0f, mainSheerAft);
        baseDeckSheerFwd = Mathf.Clamp(baseDeckSheerFwd, 0f, mainSheerFwd);
        baseDeckSheerAft = Mathf.Clamp(baseDeckSheerAft, 0f, mainSheerAft);
        // 1. Calculate sum of lower deck tiers
        float lowerDecksSum = 0f;
        if (lowerDecks != null)
        {
            Dictionary<int, float> tierMaxHeights = new Dictionary<int, float>();
            foreach (var deck in lowerDecks)
            {
                if (!tierMaxHeights.ContainsKey(deck.tierLevel)) tierMaxHeights[deck.tierLevel] = deck.height;
                else tierMaxHeights[deck.tierLevel] = Mathf.Max(tierMaxHeights[deck.tierLevel], deck.height);
            }
            foreach (var h in tierMaxHeights.Values) lowerDecksSum += h;
        }

        holdDepth = Mathf.Max(0f, holdDepth);
        float currentLowerSum = holdDepth + lowerDecksSum;

        // Auto-scaling logic
        bool holdDepthChanged = Mathf.Abs(holdDepth - lastHoldDepth) > 0.0001f;
        bool masterHeightChanged = Mathf.Abs(lowerHullHeight - lastLowerHullHeight) > 0.0001f;

        if (masterHeightChanged)
        {
            if (currentLowerSum > 0.0001f)
            {
                float scale = lowerHullHeight / currentLowerSum;
                holdDepth *= scale;
                if (lowerDecks != null)
                {
                    foreach (var deck in lowerDecks)
                    {
                        deck.height *= scale;
                    }
                }
            }
            else
            {
                holdDepth = lowerHullHeight;
            }
            currentLowerSum = lowerHullHeight;
        }
        else if (holdDepthChanged || Mathf.Abs(lowerHullHeight - currentLowerSum) > 0.0001f)
        {
            lowerHullHeight = currentLowerSum;
        }

        lastHoldDepth = holdDepth;
        lastLowerHullHeight = lowerHullHeight;

        UpdateCachedValues();
    }

    public virtual void UpdateCachedValues()
    {
        if (isUpdatingValues) return;
        isUpdatingValues = true;
        try
        {
            float halfLength = lengthBetweenPerpendiculars / 2f;
        float sternRakeLength = lengthBetweenPerpendiculars * sternPostRakePercentage;
        float stemRakeLength = lengthBetweenPerpendiculars * stemRakePercentage;

        if (upperDecks != null)
        {
            for (int i = 0; i < upperDecks.Count; i++)
            {
                upperDecks[i].deckID = i + 1; // 1-based indexing, 0 is base deck
            }

            // Validate Tree Extents, Tiers, and Naming
            Dictionary<int, int> tierCounts = new Dictionary<int, int>();
            for (int i = 0; i < upperDecks.Count; i++)
            {
                if (upperDecks[i] == null) continue;
                
                // Auto-calculate tier level based on parent
                if (upperDecks[i].parentDeckID == 0) 
                {
                    upperDecks[i].tierLevel = 1;
                }
                else
                {
                    int pIdx = upperDecks.FindIndex(d => d.deckID == upperDecks[i].parentDeckID);
                    if (pIdx != -1) upperDecks[i].tierLevel = upperDecks[pIdx].tierLevel + 1;
                    else upperDecks[i].tierLevel = 1; // Fallback if parent not found
                }

                int tier = upperDecks[i].tierLevel;
                if (!tierCounts.ContainsKey(tier)) tierCounts[tier] = 0;
                tierCounts[tier]++;
                
                // Name the deck based on its order in this tier
                string suffix = "th";
                if (tierCounts[tier] % 10 == 1 && tierCounts[tier] % 100 != 11) suffix = "st";
                else if (tierCounts[tier] % 10 == 2 && tierCounts[tier] % 100 != 12) suffix = "nd";
                else if (tierCounts[tier] % 10 == 3 && tierCounts[tier] % 100 != 13) suffix = "rd";
                
                upperDecks[i].name = $"{tierCounts[tier]}{suffix} Upper Deck Tier {tier} [ID: {upperDecks[i].deckID}]";

                // Resolve self overlaps safely (if they dragged aft past fore)
                if (upperDecks[i].aftExtent > upperDecks[i].foreExtent)
                {
                    upperDecks[i].aftExtent = upperDecks[i].foreExtent;
                }
            }

            // Enforce parent/child physical boundaries recursively
            CalculateGlobalExtentsRecursive(0, 0f, 1f, 0);
        }

        // Keel
        cachedKeelSternPoint = new Vector3(0, 0, -halfLength + sternRakeLength);
        cachedKeelBowPoint = new Vector3(0, 0, halfLength - stemRakeLength);
        cachedMidshipKeel = new Vector3(0, -keelMidshipDrop, 0);

        // Main Sheer Heights (Sets maximum sheer for the ship)
        mainSheerFwd = Mathf.Max(0f, mainSheerFwd);
        mainSheerAft = Mathf.Max(0f, mainSheerAft);
        float mainSheerMidY = lowerHullHeight;
        float mainSheerBowY = lowerHullHeight + mainSheerFwd;
        float mainSheerSternY = lowerHullHeight + mainSheerAft;

        // Base Deck Heights (Clamped between 0 and Main Sheer Line heights)
        baseDeckSheerFwd = Mathf.Clamp(baseDeckSheerFwd, 0f, mainSheerFwd);
        baseDeckSheerAft = Mathf.Clamp(baseDeckSheerAft, 0f, mainSheerAft);
        float baseDeckMidY = lowerHullHeight;
        float baseDeckBowY = lowerHullHeight + baseDeckSheerFwd;
        float baseDeckSternY = lowerHullHeight + baseDeckSheerAft;

        // Calculate absolute maximum transom top height beforehand for Counter logic
        float sternMaxHeight = mainSheerSternY;
        ModularUpperDeck sternDeck = null;
        if (upperDecks != null) {
            foreach (var d in upperDecks) {
                if (d.globalAftExtent <= 0.001f && GetAbsoluteDeckHeight(d) > (sternDeck == null ? -1f : GetAbsoluteDeckHeight(sternDeck))) sternDeck = d;
            }
        }
        if (sternDeck == null) sternMaxHeight += bulwarkHeight;
        else sternMaxHeight += GetAbsoluteDeckHeight(sternDeck) + sternDeck.bulwarkHeight;
        
        float absoluteTransomTopY = sternMaxHeight + bulwarkSheerAft;

        float actualTransomAftRake = transomAftRake;

        // Stern Post & Counter
        Vector3 originalSPT = new Vector3(0, mainSheerSternY, -halfLength);
        
        if (enableCounter)
        {
            float totalSternHeight = absoluteTransomTopY - cachedKeelSternPoint.y;
            float actualCounterHeight = totalSternHeight * counterHeightRatio;
            
            // Hard Limit: The Counter cannot be taller than the Stern Post.
            // For the Stern Post to be at least as tall as the Counter, the Counter can never be taller than 50% of the ship.
            actualCounterHeight = Mathf.Min(actualCounterHeight, totalSternHeight * 0.5f); 

            float tSlide = (counterVerticalSlide + 1f) / 2f;
            
            // minCbY must be high enough that the Stern Post (cbY - keel.y) is at least actualCounterHeight
            float minCbY = cachedKeelSternPoint.y + actualCounterHeight;
            float maxCbY = absoluteTransomTopY - actualCounterHeight;
            
            float cbY = Mathf.Lerp(minCbY, maxCbY, tSlide);
            float ctY = cbY + actualCounterHeight;

            // Reduce Transom Rake to 0 at the very top of the slide
            float remainingTransomHeight = absoluteTransomTopY - ctY;
            float rakeThreshold = 0.5f; // Fade out rake over the last 0.5 meters
            if (remainingTransomHeight < rakeThreshold)
            {
                actualTransomAftRake = Mathf.Lerp(0f, transomAftRake, remainingTransomHeight / rakeThreshold);
            }

            float sternPostT = 1f;
            if (originalSPT.y - cachedKeelSternPoint.y > 0.001f)
                sternPostT = (cbY - cachedKeelSternPoint.y) / (originalSPT.y - cachedKeelSternPoint.y);
            
            cachedSternPostTopPoint = new Vector3(0, cbY, Mathf.LerpUnclamped(cachedKeelSternPoint.z, originalSPT.z, sternPostT));
            cachedCounterBasePoint = cachedSternPostTopPoint;
            cachedCounterTopPoint = new Vector3(0, ctY, cachedCounterBasePoint.z - counterLength);

            // Enforce Counter Rake curve arms not crossing each other
            float bottomAngleRad = Mathf.Lerp(Mathf.PI / 2f, 0f, counterCurveAngle);
            float topAngleRad = Mathf.Lerp(Mathf.PI, 1.5f * Mathf.PI, counterCurveAngle);
            float ccLength = Mathf.Min(counterCurveLength, 1f); // max length

            float bottomNormalizedZ = Mathf.Cos(bottomAngleRad) * ccLength * counterLength;
            float bottomNormalizedY = Mathf.Sin(bottomAngleRad) * ccLength * actualCounterHeight;
            cachedCounterCp1 = new Vector3(0, cachedCounterBasePoint.y + bottomNormalizedY, cachedCounterBasePoint.z - bottomNormalizedZ);

            float topNormalizedZ = Mathf.Cos(topAngleRad) * ccLength * counterLength;
            float topNormalizedY = Mathf.Sin(topAngleRad) * ccLength * actualCounterHeight;
            cachedCounterCp2 = new Vector3(0, cachedCounterTopPoint.y + topNormalizedY, cachedCounterTopPoint.z - topNormalizedZ);
        }
        else
        {
            cachedSternPostTopPoint = originalSPT;
            cachedCounterBasePoint = cachedSternPostTopPoint;
            cachedCounterTopPoint = cachedSternPostTopPoint;
            cachedCounterCp1 = cachedSternPostTopPoint;
            cachedCounterCp2 = cachedSternPostTopPoint;
        }

        // Deadwood Rake (removed, defaulting cached points)
        cachedDeadwoodForePoint = cachedKeelSternPoint;
        cachedDeadwoodAftPoint = cachedKeelSternPoint;
        cachedDeadwoodP1 = cachedKeelSternPoint;
        cachedDeadwoodP2 = cachedKeelSternPoint;

        // Stem
        cachedStemTopPoint = new Vector3(0, mainSheerBowY, halfLength);
        
        float minZ = cachedKeelBowPoint.z;
        float maxZ = cachedStemTopPoint.z;
        float minY = cachedKeelBowPoint.y;
        float maxY = cachedStemTopPoint.y;
        cachedStemP1 = new Vector3(0, Mathf.Lerp(minY, maxY, stemBottomControlY), Mathf.Lerp(minZ, maxZ, stemBottomControlZ));
        cachedStemP2 = new Vector3(0, Mathf.Lerp(minY, maxY, stemTopControlY), Mathf.Lerp(minZ, maxZ, stemTopControlZ));

        // Upper Stem (Always perfectly vertical)
        float bowMaxHeight = mainSheerBowY;
        ModularUpperDeck bowDeck = null;
        if (upperDecks != null) {
            foreach (var d in upperDecks) {
                if (d.foreExtent >= 0.999f && GetAbsoluteDeckHeight(d) > (bowDeck == null ? -1f : GetAbsoluteDeckHeight(bowDeck))) bowDeck = d;
            }
        }
        if (bowDeck == null) bowMaxHeight += bulwarkHeight;
        else bowMaxHeight += GetAbsoluteDeckHeight(bowDeck) + bowDeck.bulwarkHeight;
        
        cachedUpperStemTopPoint = new Vector3(0, bowMaxHeight + bulwarkSheerFwd, cachedStemTopPoint.z);

        // Transom Rake & Transom Top Point
        float transomBaseZ = (enableCounter) ? cachedCounterTopPoint.z : cachedBaseDeckSternIntersect.z;
        float transomTopZ = transomBaseZ - actualTransomAftRake;
        cachedTransomTopPoint = new Vector3(0, absoluteTransomTopY, transomTopZ);

        // Base Deck
        cachedBaseDeckMidshipPoint = new Vector3(0, baseDeckMidY, 0);
        
        // Base Deck Bow Intersect (Stem)
        if (baseDeckBowY <= cachedStemTopPoint.y)
        {
            cachedBaseDeckBowIntersect = GetBezierPointAtY(cachedKeelBowPoint, cachedStemP1, cachedStemP2, cachedStemTopPoint, baseDeckBowY);
        }
        else
        {
            cachedBaseDeckBowIntersect = new Vector3(0, baseDeckBowY, cachedStemTopPoint.z);
        }
        
        // Base Deck Stern Intersect
        if (enableCounter)
        {
            if (baseDeckSternY > cachedCounterTopPoint.y)
            {
                // State 3: Counter is BELOW Base Deck. Base Deck intersects the Transom Rake.
                float tBaseZ = cachedCounterTopPoint.z;
                float tTopZ = tBaseZ - actualTransomAftRake;
                float transomTotalY = absoluteTransomTopY - cachedCounterTopPoint.y;
                float tTransom = (transomTotalY > 0.001f) ? (baseDeckSternY - cachedCounterTopPoint.y) / transomTotalY : 0f;
                float hitZ = Mathf.LerpUnclamped(tBaseZ, tTopZ, tTransom);
                cachedBaseDeckSternIntersect = new Vector3(0, baseDeckSternY, hitZ);
            }
            else if (baseDeckSternY < cachedCounterBasePoint.y)
            {
                // State 2: Counter is ABOVE Base Deck. Base Deck intersects the Stern Post.
                float postTotalY = cachedSternPostTopPoint.y - cachedKeelSternPoint.y;
                float postT = (postTotalY > 0.001f) ? (baseDeckSternY - cachedKeelSternPoint.y) / postTotalY : 1f;
                float hitZ = Mathf.LerpUnclamped(cachedKeelSternPoint.z, cachedSternPostTopPoint.z, postT);
                cachedBaseDeckSternIntersect = new Vector3(0, baseDeckSternY, hitZ);
            }
            else
            {
                // State 1: Counter overlaps Base Deck. Base Deck intersects the Counter face.
                float hitT = GetBezierTAtY(cachedCounterBasePoint, cachedCounterCp1, cachedCounterCp2, cachedCounterTopPoint, baseDeckSternY);
                float hitZ = CalculateCubicBezierPoint(hitT, cachedCounterBasePoint, cachedCounterCp1, cachedCounterCp2, cachedCounterTopPoint).z;
                cachedBaseDeckSternIntersect = new Vector3(0, baseDeckSternY, hitZ);
            }
        }
        else
        {
            float postTotalY = cachedSternPostTopPoint.y - cachedKeelSternPoint.y;
            float postT = (postTotalY > 0.001f) ? (baseDeckSternY - cachedKeelSternPoint.y) / postTotalY : 1f;
            float hitZ = Mathf.LerpUnclamped(cachedKeelSternPoint.z, cachedSternPostTopPoint.z, postT);
            cachedBaseDeckSternIntersect = new Vector3(0, baseDeckSternY, hitZ);
        }

        float deckControlDist = halfLength * baseDeckCurveControlLength;
        float deckFwdDist = Mathf.Min(deckControlDist, cachedBaseDeckBowIntersect.z * 0.95f);
        float deckAftDist = Mathf.Min(deckControlDist, -cachedBaseDeckSternIntersect.z * 0.95f);
        
        cachedBaseDeckSternCp = cachedBaseDeckSternIntersect;
        cachedBaseDeckMidCpAft = cachedBaseDeckMidshipPoint + new Vector3(0, 0, -deckAftDist);
        cachedBaseDeckMidCpFwd = cachedBaseDeckMidshipPoint + new Vector3(0, 0, deckFwdDist);
        cachedBaseDeckBowCp = cachedBaseDeckBowIntersect;

        // Main Sheer Line Points (Spans between Stem Top at Bow and Transom Top at Stern)
        cachedMainSheerMidshipPoint = new Vector3(0, mainSheerMidY, 0);
        cachedMainSheerBowIntersect = cachedStemTopPoint;
        float sheerSternZ = GetSternProfileZAtY(mainSheerSternY);
        cachedMainSheerSternIntersect = new Vector3(0, mainSheerSternY, sheerSternZ);

        if (slaveMainSheerHandlesToBreadthPlan && breadthPlan != null)
        {
            breadthPlan.UpdateCachedCurves();

            float mMidY = cachedMainSheerMidshipPoint.y;
            float mMidZ = cachedMainSheerMidshipPoint.z;
            float mBowZ = cachedMainSheerBowIntersect.z;
            float mSternZ = cachedMainSheerSternIntersect.z;

            // 1. Midship Forward Handle Z (slaved to topTimberMidshipFwdArmLength)
            float fwdArmDist = halfLength * breadthPlan.topTimberMidshipFwdArmLength;
            float fwdLimit = Mathf.Abs(mBowZ - mMidZ) * 0.95f;
            float fwdDist = Mathf.Min(fwdArmDist, fwdLimit);
            cachedMainSheerMidCpFwd = new Vector3(0, mMidY, mMidZ + fwdDist);

            // 2. Midship Aft Handle Z (slaved to topTimberMidshipAftArmLength)
            float aftArmDist = halfLength * breadthPlan.topTimberMidshipAftArmLength;
            float aftLimit = Mathf.Abs(mMidZ - mSternZ) * 0.95f;
            float aftDist = Mathf.Min(aftArmDist, aftLimit);
            cachedMainSheerMidCpAft = new Vector3(0, mMidY, mMidZ - aftDist);

            // 3. Bow Handle (Z-position strictly matched to BreadthPlan's Top Timber Bow control arm cachedTTFwdP1.z)
            float targetZ = breadthPlan.cachedTTFwdP1.z;
            float dzTotal = cachedMainSheerMidCpFwd.z - cachedMainSheerBowIntersect.z;
            float tZ = (Mathf.Abs(dzTotal) > 0.001f) ? (targetZ - cachedMainSheerBowIntersect.z) / dzTotal : 0f;
            tZ = Mathf.Clamp01(tZ);
            float targetY = Mathf.Lerp(cachedMainSheerBowIntersect.y, cachedMainSheerMidCpFwd.y, tZ);

            cachedMainSheerBowCp = new Vector3(0, targetY, targetZ);

            // 4. Stern Handle (Pinned to Stern Intersect)
            cachedMainSheerSternCp = cachedMainSheerSternIntersect;
        }
        else
        {
            float mainSheerControlDist = halfLength * mainSheerCurveControlLength;
            float mainSheerFwdDist = Mathf.Min(mainSheerControlDist, cachedMainSheerBowIntersect.z * 0.95f);
            float mainSheerAftDist = Mathf.Min(mainSheerControlDist, -cachedMainSheerSternIntersect.z * 0.95f);

            cachedMainSheerSternCp = cachedMainSheerSternIntersect;
            cachedMainSheerMidCpAft = cachedMainSheerMidshipPoint + new Vector3(0, 0, -mainSheerAftDist);
            cachedMainSheerMidCpFwd = cachedMainSheerMidshipPoint + new Vector3(0, 0, mainSheerFwdDist);
            cachedMainSheerBowCp = cachedMainSheerBowIntersect;
        }

        // Top Timber Sheer
        cachedTopTimberMidshipPoint = cachedMainSheerMidshipPoint + new Vector3(0, bulwarkHeight, 0);
        
        // The Top Timber anchors for the sheer bezier curve mirror the Main Sheer Line.
        cachedTopTimberBowIntersect = new Vector3(0, cachedMainSheerBowIntersect.y + bulwarkHeight, cachedMainSheerBowIntersect.z);
        cachedTopTimberSternIntersect = cachedTransomTopPoint;

        cachedTopTimberMidCpFwd = cachedMainSheerMidCpFwd + new Vector3(0, bulwarkHeight, 0);
        cachedTopTimberBowCp = cachedMainSheerBowCp + new Vector3(0, bulwarkHeight, 0);
        cachedTopTimberMidCpAft = cachedMainSheerMidCpAft + new Vector3(0, bulwarkHeight, 0);
        cachedTopTimberSternCp = cachedMainSheerSternCp + new Vector3(0, bulwarkHeight, 0);

        // HoB (Height of Breadth) - Defaults to Main Sheer Line heights unless customized
        if (!customizeHob)
        {
            cachedHobMidshipPoint = cachedMainSheerMidshipPoint;
            cachedHobBowIntersect = cachedMainSheerBowIntersect;
            cachedHobSternIntersect = cachedMainSheerSternIntersect;
        }
        else
        {
            cachedHobMidshipPoint = new Vector3(0, hobMidshipHeight, hobDropAft);

            // HoB Bow Intersect (Stem)
            if (hobBowHeight <= cachedStemTopPoint.y) {
                cachedHobBowIntersect = GetBezierPointAtY(cachedKeelBowPoint, cachedStemP1, cachedStemP2, cachedStemTopPoint, hobBowHeight);
            } else {
                cachedHobBowIntersect = new Vector3(0, hobBowHeight, cachedStemTopPoint.z);
            }
            
            // HoB Stern Intersect
            float searchMinZ = cachedTransomTopPoint.z;
            float searchMaxZ = cachedKeelSternPoint.z;
            if (searchMinZ > searchMaxZ) { float temp = searchMinZ; searchMinZ = searchMaxZ; searchMaxZ = temp; }
            
            for (int i = 0; i < 25; i++)
            {
                float midZ = (searchMinZ + searchMaxZ) / 2f;
                float fY = GetSternProfileYAtZ(midZ);
                float hY = GetHobYAtZ(midZ);
                
                if (fY < hY) searchMaxZ = midZ;
                else searchMinZ = midZ;
            }
            float finalZ = (searchMinZ + searchMaxZ) / 2f;
            float finalY = GetHobYAtZ(finalZ);
            float exactSternZ = GetSternProfileZAtY(finalY);
            cachedHobSternIntersect = new Vector3(0, finalY, exactSternZ);
        }
            
            if (breadthPlan == null)
            {
                breadthPlan = GetComponentInParent<BreadthPlan>();
                if (breadthPlan == null) breadthPlan = FindObjectOfType<BreadthPlan>();
            }

            if (slaveHobHandlesToBreadthPlan && breadthPlan != null)
            {
                breadthPlan.UpdateCachedCurves();

                float hMidY = cachedHobMidshipPoint.y;
                float hBowY = cachedHobBowIntersect.y;
                float hSternY = cachedHobSternIntersect.y;

                float hMidZ = cachedHobMidshipPoint.z;
                float hBowZ = cachedHobBowIntersect.z;
                float hSternZ = cachedHobSternIntersect.z;

                // 1. Midship Forward Handle Z
                float fwdArmDist = halfLength * breadthPlan.midshipFwdArmLength;
                float fwdLimit = Mathf.Abs(hBowZ - hMidZ) * 0.95f;
                float fwdDist = Mathf.Min(fwdArmDist, fwdLimit);
                cachedHobMidCpFwd = new Vector3(0, hMidY, hMidZ + fwdDist);

                // 2. Midship Aft Handle Z
                float aftArmDist = halfLength * breadthPlan.midshipAftArmLength;
                float aftLimit = Mathf.Abs(hMidZ - hSternZ) * 0.95f;
                float aftDist = Mathf.Min(aftArmDist, aftLimit);
                cachedHobMidCpAft = new Vector3(0, hMidY, hMidZ - aftDist);

                // 3. Bow Stem Handle (Z-position strictly matched to BreadthPlan's Max Breadth Bow control arm cachedFwdP1.z)
                float targetZ = breadthPlan.cachedFwdP1.z;
                float dzTotal = cachedHobMidCpFwd.z - cachedHobBowIntersect.z;
                float tZ = (Mathf.Abs(dzTotal) > 0.001f) ? (targetZ - cachedHobBowIntersect.z) / dzTotal : 0f;
                tZ = Mathf.Clamp01(tZ);
                float targetY = Mathf.Lerp(cachedHobBowIntersect.y, cachedHobMidCpFwd.y, tZ);

                cachedHobBowCp = new Vector3(0, targetY, targetZ);

                // 4. Stern Post Handle (Aft Curve - Quadratic, pinned to Stern Intersect)
                cachedHobSternCp = cachedHobSternIntersect;
            }
            else
            {
                float hobControlDist = halfLength * hobCurveControlLength;
                float hobFwdDist = Mathf.Min(hobControlDist, (cachedHobBowIntersect.z - hobDropAft) * 0.95f);
                float hobAftDist = Mathf.Min(hobControlDist, -(cachedHobSternIntersect.z - hobDropAft) * 0.95f);

                cachedHobBowCp = cachedHobBowIntersect;
                cachedHobSternCp = cachedHobSternIntersect;
                cachedHobMidCpAft = cachedHobMidshipPoint + new Vector3(0, 0, -hobAftDist);
                cachedHobMidCpFwd = cachedHobMidshipPoint + new Vector3(0, 0, hobFwdDist);
            }

        // --- Rising Lines ---
        float risingLineMidshipY = cachedMidshipKeel.y + Mathf.Max(0f, midshipRisingHeight);
        cachedMidshipRisingPoint = new Vector3(0, risingLineMidshipY, 0);

        float targetBowY = Mathf.Lerp(cachedKeelBowPoint.y, cachedHobBowIntersect.y, bowRisingLineEndPos);
        if (targetBowY <= cachedStemTopPoint.y) {
            cachedBowRisingEnd = GetBezierPointAtY(cachedKeelBowPoint, cachedStemP1, cachedStemP2, cachedStemTopPoint, targetBowY);
        } else {
            cachedBowRisingEnd = new Vector3(0, targetBowY, cachedStemTopPoint.z);
        }
        
        if (slaveRisingLineHandlesToBreadthPlan && breadthPlan != null)
        {
            breadthPlan.UpdateCachedCurves();
        }

        float maxBowControlZ = cachedKeelBowPoint.z;
        if (targetBowY <= cachedStemTopPoint.y && targetBowY > 0.001f)
        {
            float bowT = GetBezierTAtY(cachedKeelBowPoint, cachedStemP1, cachedStemP2, cachedStemTopPoint, targetBowY);
            Vector3 bowTangent = CalculateBezierTangent(bowT, cachedKeelBowPoint, cachedStemP1, cachedStemP2, cachedStemTopPoint);
            if (Mathf.Abs(bowTangent.y) > 0.0001f) maxBowControlZ = cachedBowRisingEnd.z - cachedBowRisingEnd.y * (bowTangent.z / bowTangent.y);
        }
        else if (targetBowY > cachedStemTopPoint.y) maxBowControlZ = cachedBowRisingEnd.z;
        
        float controlZ = (slaveRisingLineHandlesToBreadthPlan && breadthPlan != null) ? breadthPlan.cachedFloorBowCp.z : Mathf.Lerp(0f, maxBowControlZ, bowRisingLineControl);
        cachedBowP1Ctrl = new Vector3(0, cachedMidshipRisingPoint.y, controlZ);

        float targetSternY = Mathf.Lerp(cachedKeelSternPoint.y, cachedSternPostTopPoint.y, sternRisingLineEndPos);
        
        float hobSternY = enableCounter ? cachedHobSternIntersect.y : GetHobYAtZ(cachedSternPostTopPoint.z);
        if (targetSternY > hobSternY) targetSternY = hobSternY;
        
        float sternTotalY = cachedSternPostTopPoint.y - cachedKeelSternPoint.y;
        float sternT = sternTotalY > 0.0001f ? (targetSternY - cachedKeelSternPoint.y) / sternTotalY : 0f;
        cachedSternRisingEnd = new Vector3(0, targetSternY, Mathf.LerpUnclamped(cachedKeelSternPoint.z, cachedSternPostTopPoint.z, sternT));
        
        float maxSternControlZ = cachedKeelSternPoint.z;
        Vector3 sternTangent = cachedSternPostTopPoint - cachedKeelSternPoint;
        if (Mathf.Abs(sternTangent.y) > 0.0001f) maxSternControlZ = cachedSternRisingEnd.z - cachedSternRisingEnd.y * (sternTangent.z / sternTangent.y);
        else maxSternControlZ = cachedSternRisingEnd.z;
        
        float sternControlZ = (slaveRisingLineHandlesToBreadthPlan && breadthPlan != null) ? breadthPlan.cachedFloorSternCp.z : Mathf.Lerp(0f, maxSternControlZ, sternRisingLineControl);
        cachedSternP1Ctrl = new Vector3(0, cachedMidshipRisingPoint.y, sternControlZ);

        float aftMostZ = GetAftMostZ();
        cachedProjectedAftKeelEnd = new Vector3(0, cachedKeelSternPoint.y, aftMostZ);

        // Projected Stern Rising Line (linear tangent extension from cachedSternRisingEnd aft to aftMostZ)
        float tAftRising = 1.0f;
        if (cachedSternRisingEnd.z > aftMostZ + 0.001f)
        {
            float dz = cachedSternRisingEnd.z - cachedSternP1Ctrl.z;
            float dy = cachedSternRisingEnd.y - cachedSternP1Ctrl.y;
            float slopeZY = (Mathf.Abs(dz) > 0.0001f) ? (dy / dz) : 0f;
            float targetDz = aftMostZ - cachedSternRisingEnd.z;
            float targetY = cachedSternRisingEnd.y + slopeZY * targetDz;

            cachedProjectedSternRisingEnd = new Vector3(0, targetY, aftMostZ);
            tAftRising = 1.0f;
        }
        else
        {
            cachedProjectedSternRisingEnd = cachedSternRisingEnd;
            tAftRising = 1.0f;
        }
        cachedSternRisingLineEndT = tAftRising;

        // Projected Bow Rising Line (tangent extension forward from cachedBowRisingEnd to GetFwdMostZ)
        float fwdMostZ = GetFwdMostZ();
        if (cachedBowRisingEnd.z < fwdMostZ - 0.001f)
        {
            Vector3 tangent = (cachedBowRisingEnd - cachedBowP1Ctrl).normalized;
            if (Mathf.Abs(tangent.z) > 0.0001f)
            {
                float distZ = fwdMostZ - cachedBowRisingEnd.z;
                float scale = distZ / tangent.z;
                cachedProjectedBowRisingEnd = cachedBowRisingEnd + tangent * scale;
            }
            else
            {
                cachedProjectedBowRisingEnd = new Vector3(0, cachedBowRisingEnd.y, fwdMostZ);
            }
        }
        else
        {
            cachedProjectedBowRisingEnd = cachedBowRisingEnd;
        }

        // Projected Bow & Stern Bilge Lines (tangent extensions from Bilge endpoints)
        float bilgeBowZ = GetBilgeBowZ();
        float bilgeSternZ = GetBilgeSternZ();

        Vector3 bilgeBowPt = new Vector3(0, GetBilgeYAtZ(bilgeBowZ), bilgeBowZ);
        Vector3 bilgeBowPrev = new Vector3(0, GetBilgeYAtZ(bilgeBowZ - 0.2f), bilgeBowZ - 0.2f);
        Vector3 bilgeBowTangent = (bilgeBowPt - bilgeBowPrev).normalized;

        if (bilgeBowZ < fwdMostZ - 0.001f && Mathf.Abs(bilgeBowTangent.z) > 0.0001f)
        {
            float distZ = fwdMostZ - bilgeBowZ;
            float scale = distZ / bilgeBowTangent.z;
            cachedProjectedBowBilgeEnd = bilgeBowPt + bilgeBowTangent * scale;
        }
        else
        {
            cachedProjectedBowBilgeEnd = bilgeBowPt;
        }

        aftMostZ = GetAftMostZ();
        Vector3 bilgeSternPt = new Vector3(0, GetBilgeYAtZ(bilgeSternZ), bilgeSternZ);
        Vector3 bilgeSternPrev = new Vector3(0, GetBilgeYAtZ(bilgeSternZ + 0.2f), bilgeSternZ + 0.2f);
        Vector3 bilgeSternTangent = (bilgeSternPt - bilgeSternPrev).normalized;

        if (bilgeSternZ > aftMostZ + 0.001f && Mathf.Abs(bilgeSternTangent.z) > 0.0001f)
        {
            float distZ = aftMostZ - bilgeSternZ;
            float scale = distZ / bilgeSternTangent.z;
            cachedProjectedSternBilgeEnd = bilgeSternPt + bilgeSternTangent * scale;
        }
        else
        {
            cachedProjectedSternBilgeEnd = bilgeSternPt;
        }

        // Projected Top Line (Spans continuously from Transom Top at Aft Perpendicular to Stem Top at Forward Perpendicular)
        cachedProjectedTopLineStart = cachedMainSheerSternIntersect;
        cachedProjectedTopLineEnd = cachedMainSheerBowIntersect;
        changeVersion++;
        }
        finally
        {
            isUpdatingValues = false;
        }
    }

    // --- MATHEMATICAL EVALUATION METHODS ---

    public virtual float GetKeelBaselineYAtZ(float z)
    {
        if (keelMidshipDrop == 0f) return 0f;
        if (z >= 0)
        {
            float maxZ = (cachedKeelBowPoint.z != 0f) ? cachedKeelBowPoint.z : 10f;
            float t = Mathf.Clamp01(z / maxZ);
            return CalculateQuadraticBezierPoint(t, cachedMidshipKeel, new Vector3(0, 0, maxZ / 2f), cachedKeelBowPoint).y;
        }
        else
        {
            float minZ = (cachedKeelSternPoint.z != 0f) ? cachedKeelSternPoint.z : -10f;
            float t = Mathf.Clamp01(z / minZ);
            return CalculateQuadraticBezierPoint(t, cachedMidshipKeel, new Vector3(0, 0, minZ / 2f), cachedKeelSternPoint).y;
        }
    }

    public virtual float GetKeelYAtZ(float z)
    {
        if (z >= 0)
        {
            if (z >= cachedKeelBowPoint.z) return GetStemProfileYAtZ(z);
            if (keelMidshipDrop == 0f) return 0f;
            float t = z / cachedKeelBowPoint.z;
            return CalculateQuadraticBezierPoint(t, cachedMidshipKeel, new Vector3(0, 0, cachedKeelBowPoint.z / 2f), cachedKeelBowPoint).y;
        }
        else
        {
            if (z <= cachedKeelSternPoint.z) return GetSternProfileYAtZ(z);
            if (keelMidshipDrop == 0f) return 0f;
            float t = z / cachedKeelSternPoint.z;
            return CalculateQuadraticBezierPoint(t, cachedMidshipKeel, new Vector3(0, 0, cachedKeelSternPoint.z / 2f), cachedKeelSternPoint).y;
        }
    }

    public virtual float GetDeadwoodYAtZ(float z)
    {
        if (z >= cachedKeelSternPoint.z) return GetKeelYAtZ(z);
        return GetSternProfileYAtZ(z);
    }

    public virtual float GetStemProfileYAtZ(float z)
    {
        if (z > cachedStemTopPoint.z + 0.001f)
        {
            if (z >= cachedUpperStemTopPoint.z) return cachedUpperStemTopPoint.y;
            float totalZ = cachedUpperStemTopPoint.z - cachedStemTopPoint.z;
            if (Mathf.Abs(totalZ) < 0.001f) return cachedStemTopPoint.y;
            float t = (z - cachedStemTopPoint.z) / totalZ;
            return Mathf.Lerp(cachedStemTopPoint.y, cachedUpperStemTopPoint.y, t);
        }
        
        if (z <= cachedKeelBowPoint.z) return cachedKeelBowPoint.y;
        float tBase = GetBezierTAtZ(cachedKeelBowPoint, cachedStemP1, cachedStemP2, cachedStemTopPoint, z, true);
        return CalculateCubicBezierPoint(tBase, cachedKeelBowPoint, cachedStemP1, cachedStemP2, cachedStemTopPoint).y;
    }

    public virtual float GetAftMostZ()
    {
        float minZ = cachedTransomTopPoint.z;
        minZ = Mathf.Min(minZ, cachedSternPostTopPoint.z);
        minZ = Mathf.Min(minZ, cachedKeelSternPoint.z);
        if (enableCounter)
        {
            minZ = Mathf.Min(minZ, cachedCounterBasePoint.z);
            minZ = Mathf.Min(minZ, cachedCounterTopPoint.z);
        }
        return minZ;
    }

    public virtual float GetFwdMostZ()
    {
        float maxZ = cachedUpperStemTopPoint.z;
        maxZ = Mathf.Max(maxZ, cachedKeelBowPoint.z);
        maxZ = Mathf.Max(maxZ, cachedStemTopPoint.z);

        for (int i = 0; i <= 20; i++)
        {
            float t = i / 20f;
            Vector3 pt = CalculateCubicBezierPoint(t, cachedKeelBowPoint, cachedStemP1, cachedStemP2, cachedStemTopPoint);
            maxZ = Mathf.Max(maxZ, pt.z);
        }

        maxZ = Mathf.Max(maxZ, cachedTopTimberBowIntersect.z);
        return maxZ;
    }

    public virtual float GetMaxY()
    {
        float maxY = cachedUpperStemTopPoint.y;
        maxY = Mathf.Max(maxY, cachedStemTopPoint.y);
        maxY = Mathf.Max(maxY, cachedSternPostTopPoint.y);
        if (enableCounter)
        {
            maxY = Mathf.Max(maxY, cachedCounterBasePoint.y);
            maxY = Mathf.Max(maxY, cachedCounterTopPoint.y);
            maxY = Mathf.Max(maxY, cachedCounterCp1.y);
            maxY = Mathf.Max(maxY, cachedCounterCp2.y);
        }
        maxY = Mathf.Max(maxY, cachedTransomTopPoint.y);
        maxY = Mathf.Max(maxY, cachedTopTimberBowIntersect.y);
        maxY = Mathf.Max(maxY, cachedTopTimberSternIntersect.y);
        maxY = Mathf.Max(maxY, cachedTopTimberMidshipPoint.y);

        float aftZ = GetAftMostZ();
        float fwdZ = cachedUpperStemTopPoint.z;
        int samples = 30;
        for (int i = 0; i <= samples; i++)
        {
            float z = Mathf.Lerp(aftZ, fwdZ, i / (float)samples);
            maxY = Mathf.Max(maxY, GetTopTimberYAtZ(z));
        }

        if (upperDecks != null)
        {
            foreach (var deck in upperDecks)
            {
                float deckTopY = GetMainSheerYAtZ(0) + GetAbsoluteDeckHeight(deck) + deck.bulwarkHeight;
                maxY = Mathf.Max(maxY, deckTopY);
            }
        }

        return maxY;
    }



    public virtual float GetSternProfileYAtZ(float z)
    {
        float maxY = -9999f;
        bool found = false;

        // 1. Transom segment
        Vector3 transomBase = (enableCounter) ? cachedCounterTopPoint : cachedSternPostTopPoint;
        float minTransomZ = Mathf.Min(transomBase.z, cachedTransomTopPoint.z);
        float maxTransomZ = Mathf.Max(transomBase.z, cachedTransomTopPoint.z);
        float actualTransomTopY = GetTopTimberYAtZ(cachedTransomTopPoint.z);

        if (z >= minTransomZ - 0.001f && z <= maxTransomZ + 0.001f)
        {
            float totalZ = cachedTransomTopPoint.z - transomBase.z;
            if (Mathf.Abs(totalZ) > 0.001f)
            {
                float t = Mathf.Clamp01((z - transomBase.z) / totalZ);
                maxY = Mathf.Max(maxY, Mathf.Lerp(transomBase.y, actualTransomTopY, t));
                found = true;
            }
        }

        // 2. Counter Curve & Counter Gap (if enabled)
        if (enableCounter)
        {
            float minCounterZ = Mathf.Min(cachedCounterBasePoint.z, cachedCounterTopPoint.z);
            float maxCounterZ = Mathf.Max(cachedCounterBasePoint.z, cachedCounterTopPoint.z);
            if (z >= minCounterZ - 0.001f && z <= maxCounterZ + 0.001f)
            {
                float tb = GetBezierTAtZ(cachedCounterBasePoint, cachedCounterCp1, cachedCounterCp2, cachedCounterTopPoint, z, false);
                float counterY = CalculateCubicBezierPoint(tb, cachedCounterBasePoint, cachedCounterCp1, cachedCounterCp2, cachedCounterTopPoint).y;
                maxY = Mathf.Max(maxY, counterY);
                found = true;
            }

            float minGapZ = Mathf.Min(cachedSternPostTopPoint.z, cachedCounterBasePoint.z);
            float maxGapZ = Mathf.Max(cachedSternPostTopPoint.z, cachedCounterBasePoint.z);
            if (z >= minGapZ - 0.001f && z <= maxGapZ + 0.001f)
            {
                float gapZ = cachedCounterBasePoint.z - cachedSternPostTopPoint.z;
                if (Mathf.Abs(gapZ) > 0.001f)
                {
                    float t = Mathf.Clamp01((z - cachedSternPostTopPoint.z) / gapZ);
                    maxY = Mathf.Max(maxY, Mathf.Lerp(cachedSternPostTopPoint.y, cachedCounterBasePoint.y, t));
                    found = true;
                }
            }
        }

        // 3. Stern Post Rake segment (between cachedKeelSternPoint and cachedSternPostTopPoint)
        float minPostZ = Mathf.Min(cachedKeelSternPoint.z, cachedSternPostTopPoint.z);
        float maxPostZ = Mathf.Max(cachedKeelSternPoint.z, cachedSternPostTopPoint.z);
        if (z >= minPostZ - 0.001f && z <= maxPostZ + 0.001f)
        {
            float postZ = cachedSternPostTopPoint.z - cachedKeelSternPoint.z;
            if (Mathf.Abs(postZ) > 0.001f)
            {
                float t = Mathf.Clamp01((z - cachedKeelSternPoint.z) / postZ);
                maxY = Mathf.Max(maxY, Mathf.Lerp(cachedKeelSternPoint.y, cachedSternPostTopPoint.y, t));
                found = true;
            }
        }

        if (found) return maxY;
        return cachedKeelSternPoint.y;
    }

    public virtual float GetStemProfileZAtY(float y)
    {
        float actualStemTopY = GetTopTimberYAtZ(cachedUpperStemTopPoint.z);
        if (y >= actualStemTopY) return cachedUpperStemTopPoint.z;
        if (y >= cachedStemTopPoint.y)
        {
            float totalY = actualStemTopY - cachedStemTopPoint.y;
            if (Mathf.Abs(totalY) < 0.001f) return cachedUpperStemTopPoint.z;
            float t = (y - cachedStemTopPoint.y) / totalY;
            return Mathf.Lerp(cachedStemTopPoint.z, cachedUpperStemTopPoint.z, t);
        }
        if (y <= cachedKeelBowPoint.y) return cachedKeelBowPoint.z;
        float tBase = GetBezierTAtY(cachedKeelBowPoint, cachedStemP1, cachedStemP2, cachedStemTopPoint, y);
        return CalculateCubicBezierPoint(tBase, cachedKeelBowPoint, cachedStemP1, cachedStemP2, cachedStemTopPoint).z;
    }

    public virtual float GetSternProfileZAtY(float y)
    {
        float actualTransomTopY = GetTopTimberYAtZ(cachedTransomTopPoint.z);
        if (y >= actualTransomTopY) return cachedTransomTopPoint.z;
        
        Vector3 transomBase = (enableCounter) ? cachedCounterTopPoint : cachedSternPostTopPoint;
        
        if (y >= transomBase.y)
        {
            float totalY = actualTransomTopY - transomBase.y;
            if (Mathf.Abs(totalY) < 0.001f) return cachedTransomTopPoint.z;
            float t = (y - transomBase.y) / totalY;
            return Mathf.Lerp(transomBase.z, cachedTransomTopPoint.z, t);
        }
        
        if (enableCounter)
        {
            if (y >= cachedCounterBasePoint.y && y <= cachedCounterTopPoint.y)
            {
                float t = GetBezierTAtY(cachedCounterBasePoint, cachedCounterCp1, cachedCounterCp2, cachedCounterTopPoint, y);
                return CalculateCubicBezierPoint(t, cachedCounterBasePoint, cachedCounterCp1, cachedCounterCp2, cachedCounterTopPoint).z;
            }
            if (y > cachedSternPostTopPoint.y) return cachedCounterBasePoint.z; // Gap between post and counter base
        }
        
        if (y <= cachedKeelSternPoint.y) return cachedKeelSternPoint.z;
        
        float postTotalY = cachedSternPostTopPoint.y - cachedKeelSternPoint.y;
        if (Mathf.Abs(postTotalY) < 0.001f) return cachedSternPostTopPoint.z;
        
        float postT = (y - cachedKeelSternPoint.y) / postTotalY;
        return Mathf.Lerp(cachedKeelSternPoint.z, cachedSternPostTopPoint.z, postT);
    }

    public virtual float GetBaseDeckYAtZ(float z)
    {
        float deckY;
        if (z >= 0)
        {
            if (z >= cachedBaseDeckBowIntersect.z)
            {
                float dz = cachedBaseDeckBowIntersect.z - cachedBaseDeckMidCpFwd.z;
                float dy = cachedBaseDeckBowIntersect.y - cachedBaseDeckMidCpFwd.y;
                if (Mathf.Abs(dz) < 0.001f) deckY = cachedBaseDeckBowIntersect.y;
                else deckY = cachedBaseDeckBowIntersect.y + (dy / dz) * (z - cachedBaseDeckBowIntersect.z);
            }
            else
            {
                float t = GetBezierTAtZ(cachedBaseDeckMidshipPoint, cachedBaseDeckMidCpFwd, cachedBaseDeckBowCp, cachedBaseDeckBowIntersect, z, true);
                deckY = CalculateCubicBezierPoint(t, cachedBaseDeckMidshipPoint, cachedBaseDeckMidCpFwd, cachedBaseDeckBowCp, cachedBaseDeckBowIntersect).y;
            }
        }
        else
        {
            if (z <= cachedBaseDeckSternIntersect.z)
            {
                float dz = cachedBaseDeckSternIntersect.z - cachedBaseDeckMidCpAft.z;
                float dy = cachedBaseDeckSternIntersect.y - cachedBaseDeckMidCpAft.y;
                if (Mathf.Abs(dz) < 0.001f) deckY = cachedBaseDeckSternIntersect.y;
                else deckY = cachedBaseDeckSternIntersect.y + (dy / dz) * (z - cachedBaseDeckSternIntersect.z);
            }
            else
            {
                float t = GetBezierTAtZ(cachedBaseDeckMidshipPoint, cachedBaseDeckMidCpAft, cachedBaseDeckSternCp, cachedBaseDeckSternIntersect, z, false);
                deckY = CalculateCubicBezierPoint(t, cachedBaseDeckMidshipPoint, cachedBaseDeckMidCpAft, cachedBaseDeckSternCp, cachedBaseDeckSternIntersect).y;
            }
        }
        return Mathf.Min(deckY, GetMainSheerYAtZ(z));
    }

    public virtual float GetMainSheerYAtZ(float z)
    {
        if (z >= 0)
        {
            if (z >= cachedMainSheerBowIntersect.z)
            {
                Vector3 dir = (cachedMainSheerBowIntersect - cachedMainSheerBowCp);
                if (dir.magnitude < 0.001f) dir = (cachedMainSheerBowIntersect - cachedMainSheerMidCpFwd);
                float dz = dir.z;
                float dy = dir.y;
                if (Mathf.Abs(dz) < 0.001f) return cachedMainSheerBowIntersect.y;
                return cachedMainSheerBowIntersect.y + (dy / dz) * (z - cachedMainSheerBowIntersect.z);
            }
            float t = GetBezierTAtZ(cachedMainSheerMidshipPoint, cachedMainSheerMidCpFwd, cachedMainSheerBowCp, cachedMainSheerBowIntersect, z, true);
            return CalculateCubicBezierPoint(t, cachedMainSheerMidshipPoint, cachedMainSheerMidCpFwd, cachedMainSheerBowCp, cachedMainSheerBowIntersect).y;
        }
        else
        {
            if (z <= cachedMainSheerSternIntersect.z)
            {
                float dz = cachedMainSheerSternIntersect.z - cachedMainSheerMidCpAft.z;
                float dy = cachedMainSheerSternIntersect.y - cachedMainSheerMidCpAft.y;
                if (Mathf.Abs(dz) < 0.001f) return cachedMainSheerSternIntersect.y;
                return cachedMainSheerSternIntersect.y + (dy / dz) * (z - cachedMainSheerSternIntersect.z);
            }
            float t = GetBezierTAtZ(cachedMainSheerMidshipPoint, cachedMainSheerMidCpAft, cachedMainSheerSternCp, cachedMainSheerSternIntersect, z, false);
            return CalculateCubicBezierPoint(t, cachedMainSheerMidshipPoint, cachedMainSheerMidCpAft, cachedMainSheerSternCp, cachedMainSheerSternIntersect).y;
        }
    }

    public ModularUpperDeck GetHighestActiveDeckAtZ(float z)
    {
        if (upperDecks == null || upperDecks.Count == 0) return null;

        float bowZ = cachedTopTimberBowIntersect.z;
        float sternZ = cachedTopTimberSternIntersect.z;
        float totalLength = bowZ - sternZ;
        float currentPercent = (z - sternZ) / totalLength;

        ModularUpperDeck highestDeck = null;
        float maxAbsHeight = -1f;

        foreach (var deck in upperDecks)
        {
            if (currentPercent >= deck.globalAftExtent - 0.0001f && currentPercent <= deck.globalForeExtent + 0.0001f)
            {
                float absH = GetAbsoluteDeckHeight(deck);
                if (absH > maxAbsHeight)
                {
                    maxAbsHeight = absH;
                    highestDeck = deck;
                }
            }
        }
        return highestDeck;
    }

    private void CalculateGlobalExtentsRecursive(int pID, float parentGlobalAft, float parentGlobalFore, int depth)
    {
        if (depth > 100 || upperDecks == null) return; // infinite loop fallback
        
        float parentLength = parentGlobalFore - parentGlobalAft;
        
        for (int j = 0; j < upperDecks.Count; j++)
        {
            if (upperDecks[j].parentDeckID == pID)
            {
                // Child's extents are relative percentages of the parent's length
                upperDecks[j].globalAftExtent = parentGlobalAft + (upperDecks[j].aftExtent * parentLength);
                upperDecks[j].globalForeExtent = parentGlobalAft + (upperDecks[j].foreExtent * parentLength);
                
                // Recurse into children
                CalculateGlobalExtentsRecursive(upperDecks[j].deckID, upperDecks[j].globalAftExtent, upperDecks[j].globalForeExtent, depth + 1);
            }
        }
    }

    public float GetAbsoluteDeckHeight(ModularUpperDeck deck)
    {
        if (deck == null) return 0f;
        float h = deck.height;
        if (deck.parentDeckID != 0 && upperDecks != null)
        {
            // Find parent
            var parent = upperDecks.Find(d => d.deckID == deck.parentDeckID);
            if (parent != null)
            {
                h += GetAbsoluteDeckHeight(parent);
            }
        }
        return h;
    }

    public float GetMaxDeckHeight()
    {
        float maxDeckHeight = 0f;
        if (upperDecks != null)
        {
            foreach (var deck in upperDecks)
            {
                float totalH = GetAbsoluteDeckHeight(deck) + deck.bulwarkHeight;
                if (totalH > maxDeckHeight) maxDeckHeight = totalH;
            }
        }
        return maxDeckHeight;
    }

    public float GetUpperDecksHeightAtZ(float z)
    {
        ModularUpperDeck highest = GetHighestActiveDeckAtZ(z);
        return highest != null ? GetAbsoluteDeckHeight(highest) : 0f;
    }

    public float GetLowerDeckYDrop(ModularLowerDeck deck)
    {
        float drop = 0f;
        if (lowerDecks != null)
        {
            Dictionary<int, float> tierMaxHeights = new Dictionary<int, float>();
            foreach (var d in lowerDecks)
            {
                if (!tierMaxHeights.ContainsKey(d.tierLevel)) tierMaxHeights[d.tierLevel] = d.height;
                else tierMaxHeights[d.tierLevel] = Mathf.Max(tierMaxHeights[d.tierLevel], d.height);
            }
            
            for (int i = 1; i <= deck.tierLevel; i++)
            {
                if (tierMaxHeights.ContainsKey(i))
                {
                    drop += tierMaxHeights[i];
                }
            }
        }
        return drop;
    }

    public float GetHighestDeckYAtZ(float z)
    {
        return GetMainSheerYAtZ(z) + GetUpperDecksHeightAtZ(z);
    }

    public float GetGlobalSheerOffsetAtZ(float z)
    {
        float globalSheerOffset = 0f;
        if (z >= 0)
        {
            float t = Mathf.Clamp01(z / cachedTopTimberBowIntersect.z);
            globalSheerOffset = bulwarkSheerFwd * (t * t); // Quadratic easing
        }
        else
        {
            float t = Mathf.Clamp01(z / cachedTopTimberSternIntersect.z);
            globalSheerOffset = bulwarkSheerAft * (t * t); // Quadratic easing
        }
        return globalSheerOffset;
    }

    public virtual float GetPureTopTimberYAtZ(float z)
    {
        float baseTopTimberY;
        if (z >= 0)
        {
            if (z >= cachedTopTimberBowIntersect.z)
            {
                Vector3 dir = (cachedTopTimberBowIntersect - cachedTopTimberBowCp);
                if (dir.magnitude < 0.001f) dir = (cachedTopTimberBowIntersect - cachedTopTimberMidCpFwd);
                float dz = dir.z;
                float dy = dir.y;
                if (Mathf.Abs(dz) < 0.001f) baseTopTimberY = cachedTopTimberBowIntersect.y;
                else baseTopTimberY = cachedTopTimberBowIntersect.y + (dy / dz) * (z - cachedTopTimberBowIntersect.z);
            }
            else
            {
                float t = GetBezierTAtZ(cachedTopTimberMidshipPoint, cachedTopTimberMidCpFwd, cachedTopTimberBowCp, cachedTopTimberBowIntersect, z, true);
                baseTopTimberY = CalculateCubicBezierPoint(t, cachedTopTimberMidshipPoint, cachedTopTimberMidCpFwd, cachedTopTimberBowCp, cachedTopTimberBowIntersect).y;
            }
        }
        else
        {
            if (z <= cachedTopTimberSternIntersect.z)
            {
                float dz = cachedTopTimberSternIntersect.z - cachedTopTimberMidCpAft.z;
                float dy = cachedTopTimberSternIntersect.y - cachedTopTimberMidCpAft.y;
                if (Mathf.Abs(dz) < 0.001f) baseTopTimberY = cachedTopTimberSternIntersect.y;
                else baseTopTimberY = cachedTopTimberSternIntersect.y + (dy / dz) * (z - cachedTopTimberSternIntersect.z);
            }
            else
            {
                float t = GetBezierTAtZ(cachedTopTimberMidshipPoint, cachedTopTimberMidCpAft, cachedTopTimberSternCp, cachedTopTimberSternIntersect, z, false);
                baseTopTimberY = CalculateCubicBezierPoint(t, cachedTopTimberMidshipPoint, cachedTopTimberMidCpAft, cachedTopTimberSternCp, cachedTopTimberSternIntersect).y;
            }
        }
        return baseTopTimberY;
    }

    public virtual float GetUpperDeckFloorYAtZ(float z)
    {
        ModularUpperDeck highestDeck = GetHighestActiveDeckAtZ(z);
        if (highestDeck != null)
        {
            return GetBaseDeckYAtZ(z) + GetAbsoluteDeckHeight(highestDeck);
        }
        return GetBaseDeckYAtZ(z);
    }

    public virtual float GetUpperDeckSheerYAtZ(float z, bool ignoreBeakhead = false)
    {
        return GetTopTimberYAtZ(z, ignoreBeakhead);
    }

    public virtual float GetTopTimberYAtZ(float z, bool ignoreBeakhead = false)
    {
        float globalSheerOffset = GetGlobalSheerOffsetAtZ(z);

        ModularUpperDeck highestDeck = GetHighestActiveDeckAtZ(z);
        
        float finalBulwarkY = 0f;

        if (highestDeck != null)
        {
            float mainSheerY = GetMainSheerYAtZ(z);
            finalBulwarkY = mainSheerY + GetAbsoluteDeckHeight(highestDeck) + highestDeck.bulwarkHeight;
        }
        else
        {
            finalBulwarkY = GetPureTopTimberYAtZ(z);
        }

        // Apply Global Beakhead Bulwark Transition
        if (!ignoreBeakhead && enableBeakheadBulwark)
        {
            float bowZ = cachedTopTimberBowIntersect.z;
            float sternZ = cachedTopTimberSternIntersect.z;
            float totalLength = bowZ - sternZ;
            
            float startDropZ = bowZ - (beakheadBulwarkStartPercent * totalLength);
            
            if (z >= startDropZ)
            {
                // Verify this deck is the one that reaches the bow!
                ModularUpperDeck bowDeck = GetHighestActiveDeckAtZ(bowZ);
                if (highestDeck == bowDeck)
                {
                    float activeBulwarkHeight = (highestDeck != null) ? highestDeck.bulwarkHeight : bulwarkHeight;
                    
                    float heightDrop = activeBulwarkHeight - beakheadBulwarkHeight;

                    if (curveBeakheadBulwark)
                    {
                        float dropLengthZ = bowZ - startDropZ;
                        float dropHeightY = activeBulwarkHeight - beakheadBulwarkHeight;

                        float topAngleRad = Mathf.Lerp(0f, -Mathf.PI / 2f, beakheadBulwarkTopCurveAngle);
                        float bottomAngleRad = Mathf.Lerp(Mathf.PI, Mathf.PI / 2f, beakheadBulwarkBottomCurveAngle);
                        float bbcLength = Mathf.Min(beakheadBulwarkCurveLength, 1f);

                        float topNormalizedZ = Mathf.Cos(topAngleRad) * bbcLength * dropLengthZ;
                        float topNormalizedY = Mathf.Sin(topAngleRad) * bbcLength * dropHeightY; 
                        float bottomNormalizedZ = Mathf.Cos(bottomAngleRad) * bbcLength * dropLengthZ; 
                        float bottomNormalizedY = Mathf.Sin(bottomAngleRad) * bbcLength * dropHeightY;

                        Vector3 p0 = new Vector3(0, activeBulwarkHeight, startDropZ);
                        Vector3 p3 = new Vector3(0, beakheadBulwarkHeight, bowZ);
                        Vector3 p1 = new Vector3(0, p0.y + topNormalizedY, p0.z + topNormalizedZ);
                        Vector3 p2 = new Vector3(0, p3.y + bottomNormalizedY, p3.z + bottomNormalizedZ);

                        float t = GetBezierTAtZ(p0, p1, p2, p3, z, true);
                        float dynamicBulwarkHeight = CalculateCubicBezierPoint(t, p0, p1, p2, p3).y;
                        
                        float dynamicBaseDeckY = GetMainSheerYAtZ(z) + ((bowDeck != null) ? GetAbsoluteDeckHeight(bowDeck) : 0f);
                        finalBulwarkY = dynamicBaseDeckY + dynamicBulwarkHeight;
                    }
                    else
                    {
                        float t = (z - startDropZ) / (bowZ - startDropZ);
                        float dynamicBulwarkHeight = Mathf.Lerp(activeBulwarkHeight, beakheadBulwarkHeight, t);
                        
                        float dynamicBaseDeckY = GetMainSheerYAtZ(z) + ((bowDeck != null) ? GetAbsoluteDeckHeight(bowDeck) : 0f);
                        finalBulwarkY = dynamicBaseDeckY + dynamicBulwarkHeight;
                    }
                }
            }
        }

        return finalBulwarkY + globalSheerOffset;
    }

    public virtual float GetHobYAtZ(float z)
    {
        if (z >= hobDropAft)
        {
            if (z >= cachedHobBowIntersect.z)
            {
                float dz = cachedHobBowIntersect.z - cachedHobBowCp.z;
                float dy = cachedHobBowIntersect.y - cachedHobBowCp.y;
                if (Mathf.Abs(dz) < 0.001f) return cachedHobBowIntersect.y;
                return cachedHobBowIntersect.y + (dy / dz) * (z - cachedHobBowIntersect.z);
            }
            float t = GetBezierTAtZ(cachedHobMidshipPoint, cachedHobMidCpFwd, cachedHobBowCp, cachedHobBowIntersect, z, true);
            return CalculateCubicBezierPoint(t, cachedHobMidshipPoint, cachedHobMidCpFwd, cachedHobBowCp, cachedHobBowIntersect).y;
        }
        else
        {
            if (z <= cachedHobSternIntersect.z)
            {
                // Mathematically extrapolate the terminal tangent of the HoB cubic bezier
                float dz = cachedHobSternIntersect.z - cachedHobSternCp.z;
                float dy = cachedHobSternIntersect.y - cachedHobSternCp.y;
                if (Mathf.Abs(dz) < 0.001f) return cachedHobSternIntersect.y;
                return cachedHobSternIntersect.y + (dy / dz) * (z - cachedHobSternIntersect.z);
            }
            float t = GetBezierTAtZ(cachedHobMidshipPoint, cachedHobMidCpAft, cachedHobSternCp, cachedHobSternIntersect, z, false);
            return CalculateCubicBezierPoint(t, cachedHobMidshipPoint, cachedHobMidCpAft, cachedHobSternCp, cachedHobSternIntersect).y;
        }
    }

    public float GetTheoreticalHobYAtZ(float z)
    {
        // For most of the ship, return the actual physical HoB
        if (z >= cachedHobSternIntersect.z) return GetHobYAtZ(z);
        
        // For the overhang zone, extrapolate the sheer curve's tangent linearly backward.
        // This ensures upper hull frames maintain their shape profile instead of squishing to the counter.
        float zStern = cachedHobSternIntersect.z;
        float yStern = cachedHobSternIntersect.y;
        float zForward = zStern + 0.01f;
        float yForward = GetHobYAtZ(zForward);
        
        float slope = (yForward - yStern) / 0.01f;
        return yStern + slope * (z - zStern);
    }

    /// <summary>
    /// Computes the 3D Main Wale height at station Z (slaved in 100% parallel harmony to the Main Sheer Line).
    /// At midships (Z=0), it sits at the HoB midship height (or lerped toward Main Sheer by mainWaleHeightRatio).
    /// </summary>
    public float GetMainWaleYAtZ(float z)
    {
        float midshipHobY = cachedHobMidshipPoint.y;
        float midshipSheerY = cachedMainSheerMidshipPoint.y;
        float midshipWaleY = Mathf.Lerp(midshipHobY, midshipSheerY, Mathf.Clamp01(mainWaleHeightRatio));
        float sheerOffset = midshipSheerY - midshipWaleY;

        return GetMainSheerYAtZ(z) - sheerOffset;
    }

    /// <summary>
    /// Computes the 3D Diagonal Bilge Height at station Z.
    /// Positioned smoothly between Floor Line (Rising Line) height and HOB height.
    /// </summary>
    public float GetBilgeYAtZ(float z)
    {
        float risingY = GetRisingLineYAtZ(z);
        float hobY = GetTheoreticalHobYAtZ(z);
        float ratio = Mathf.Clamp(bilgeHeightRatio, 0.05f, 0.95f);
        float bilgeY = Mathf.Lerp(risingY, hobY, ratio);

        // Restriction 4: Non-sagging Bilge Y height restriction (bilgeY must not cause curve to dip below risingY)
        float dy = hobY - risingY;
        if (dy > 0.0001f && breadthPlan != null)
        {
            float floorX = breadthPlan.GetFloorLineXAtZ(z);
            float hobX = breadthPlan.GetTheoreticalBreadthXAtZ(z);
            float approxBilgeX = Mathf.Lerp(floorX, hobX, Mathf.Clamp(breadthPlan.bilgeWidthRatio, 0.05f, 0.95f));

            Vector3 p1 = new Vector3(floorX, risingY, z);
            Vector3 p3 = new Vector3(hobX, hobY, z);
            Vector3 p2 = new Vector3(approxBilgeX, bilgeY, z);

            float d13 = Vector3.Distance(p1, p3);
            float d12 = Vector3.Distance(p1, p2);
            if (d13 > 0.0001f)
            {
                float minBilgeY = risingY + dy * (d12 / (3f * d13));
                bilgeY = Mathf.Max(bilgeY, minBilgeY);
            }
        }

        return bilgeY;
    }

    public float GetBilgeSternZ()
    {
        float z = cachedSternRisingEnd.z;
        for (int i = 0; i < 4; i++)
        {
            float y = GetBilgeYAtZ(z);
            z = GetSternProfileZAtY(y);
        }
        return z;
    }

    public float GetBilgeBowZ()
    {
        float z = cachedBowRisingEnd.z;
        for (int i = 0; i < 4; i++)
        {
            float y = GetBilgeYAtZ(z);
            z = GetStemProfileZAtY(y);
        }
        return z;
    }

    public float GetRisingLineYAtZ(float z)
    {
        if (z >= 0)
        {
            if (z >= cachedBowRisingEnd.z) return GetProjectedBowRisingYAtZ(z);
            float t = GetBezierTAtZ(cachedMidshipRisingPoint, cachedBowP1Ctrl, cachedBowRisingEnd, Vector3.zero, z, true);
            return CalculateQuadraticBezierPoint(t, cachedMidshipRisingPoint, cachedBowP1Ctrl, cachedBowRisingEnd).y;
        }
        else
        {
            if (z < cachedSternRisingEnd.z) return GetProjectedSternRisingYAtZ(z);
            float t = GetBezierTAtZ(cachedMidshipRisingPoint, cachedSternP1Ctrl, cachedSternRisingEnd, Vector3.zero, z, false);
            return CalculateQuadraticBezierPoint(t, cachedMidshipRisingPoint, cachedSternP1Ctrl, cachedSternRisingEnd).y;
        }
    }

    public float GetProjectedSternRisingYAtZ(float z)
    {
        float sternZ = cachedSternRisingEnd.z;
        float projZ = cachedProjectedSternRisingEnd.z;
        if (z >= sternZ) return cachedSternRisingEnd.y;
        float dz = projZ - sternZ;
        if (Mathf.Abs(dz) < 0.0001f) return cachedSternRisingEnd.y;
        float t = Mathf.Clamp01((z - sternZ) / dz);
        return Mathf.Lerp(cachedSternRisingEnd.y, cachedProjectedSternRisingEnd.y, t);
    }

    public float GetProjectedBowRisingYAtZ(float z)
    {
        float bowZ = cachedBowRisingEnd.z;
        float projZ = cachedProjectedBowRisingEnd.z;
        if (z <= bowZ) return cachedBowRisingEnd.y;
        float dz = projZ - bowZ;
        if (Mathf.Abs(dz) < 0.0001f) return cachedBowRisingEnd.y;
        float t = Mathf.Clamp01((z - bowZ) / dz);
        return Mathf.Lerp(cachedBowRisingEnd.y, cachedProjectedBowRisingEnd.y, t);
    }

    public float GetProjectedBowBilgeYAtZ(float z)
    {
        float bilgeBowZ = GetBilgeBowZ();
        float projZ = cachedProjectedBowBilgeEnd.z;
        float bowY = GetBilgeYAtZ(bilgeBowZ);
        if (z <= bilgeBowZ) return bowY;
        float dz = projZ - bilgeBowZ;
        if (Mathf.Abs(dz) < 0.0001f) return bowY;
        float t = Mathf.Clamp01((z - bilgeBowZ) / dz);
        return Mathf.Lerp(bowY, cachedProjectedBowBilgeEnd.y, t);
    }

    public float GetProjectedSternBilgeYAtZ(float z)
    {
        float bilgeSternZ = GetBilgeSternZ();
        float projZ = cachedProjectedSternBilgeEnd.z;
        float sternY = GetBilgeYAtZ(bilgeSternZ);
        if (z >= bilgeSternZ) return sternY;
        float dz = projZ - bilgeSternZ;
        if (Mathf.Abs(dz) < 0.0001f) return sternY;
        float t = Mathf.Clamp01((z - bilgeSternZ) / dz);
        return Mathf.Lerp(sternY, cachedProjectedSternBilgeEnd.y, t);
    }

    public float GetRisingLineTAtZ(float z)
    {
        if (z >= 0)
        {
            if (z >= cachedBowRisingEnd.z) return 1f;
            return GetBezierTAtZ(cachedMidshipRisingPoint, cachedBowP1Ctrl, cachedBowRisingEnd, Vector3.zero, z, true);
        }
        else
        {
            if (z <= cachedSternRisingEnd.z) return 1f;
            return GetBezierTAtZ(cachedMidshipRisingPoint, cachedSternP1Ctrl, cachedSternRisingEnd, Vector3.zero, z, false);
        }
    }

    private float GetBezierTAtZExt(Vector3 p0, Vector3 p1, Vector3 p2, float targetZ, float minT, float maxT)
    {
        float min = minT;
        float max = maxT;
        for (int i = 0; i < 20; i++)
        {
            float mid = (min + max) / 2f;
            float z = CalculateQuadraticBezierPoint(mid, p0, p1, p2).z;
            
            if (z > targetZ) min = mid;
            else max = mid;
        }
        return (min + max) / 2f;
    }

    // --- UTILITIES ---

    protected Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        return (uu * p0) + (2 * u * t * p1) + (tt * p2);
    }

    protected Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        return (uuu * p0) + (3 * uu * t * p1) + (3 * u * tt * p2) + (ttt * p3);
    }

    private Vector3 GetBezierPointAtY(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float targetY)
    {
        float min = 0f;
        float max = 1f;
        for (int i = 0; i < 20; i++)
        {
            float mid = (min + max) / 2f;
            float y = CalculateCubicBezierPoint(mid, p0, p1, p2, p3).y;
            if (y < targetY) min = mid;
            else max = mid;
        }
        return CalculateCubicBezierPoint((min + max) / 2f, p0, p1, p2, p3);
    }

    protected Vector3 CalculateBezierTangent(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        float uu = u * u;
        float tu = t * u;
        float tt = t * t;
        return 3 * uu * (p1 - p0) + 6 * tu * (p2 - p1) + 3 * tt * (p3 - p2);
    }

    protected float GetBezierTAtY(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float targetY)
    {
        float min = 0f;
        float max = 1f;
        for (int i = 0; i < 20; i++)
        {
            float mid = (min + max) / 2f;
            float y = CalculateCubicBezierPoint(mid, p0, p1, p2, p3).y;
            if (y < targetY) min = mid;
            else max = mid;
        }
        return (min + max) / 2f;
    }

    private float GetBezierTAtZ(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float targetZ, bool isForward)
    {
        float min = 0f;
        float max = 1f;
        for (int i = 0; i < 20; i++)
        {
            float mid = (min + max) / 2f;
            float z;
            if (p3 == Vector3.zero) z = CalculateQuadraticBezierPoint(mid, p0, p1, p2).z;
            else z = CalculateCubicBezierPoint(mid, p0, p1, p2, p3).z;
            
            if ((isForward && z < targetZ) || (!isForward && z > targetZ)) min = mid;
            else max = mid;
        }
        return (min + max) / 2f;
    }

    // --- GIZMOS ---

    private void OnDrawGizmos()
    {
        UpdateCachedValues();

        // 1. Keel Line
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(cachedKeelSternPoint, cachedKeelBowPoint);
        if (keelMidshipDrop != 0f)
        {
            DrawQuadraticBezierCurve(cachedMidshipKeel, new Vector3(0, 0, cachedKeelBowPoint.z / 2f), cachedKeelBowPoint, Color.blue);
            DrawQuadraticBezierCurve(cachedMidshipKeel, new Vector3(0, 0, cachedKeelSternPoint.z / 2f), cachedKeelSternPoint, Color.blue);
        }

        // Projected Aft Keel Line
        if (showProjectedAftKeelLine && cachedKeelSternPoint.z > cachedProjectedAftKeelEnd.z + 0.001f)
        {
            DrawDashedLine(cachedKeelSternPoint, cachedProjectedAftKeelEnd, Color.gray, 20);
        }

        // Projected Top Line (Curved Sheer Profile)
        if (showProjectedTopLine)
        {
            float aftZ = GetAftMostZ();
            float fwdZ = GetFwdMostZ();
            DrawDashedTopLineCurve(aftZ, fwdZ, Color.gray, 80);
            Gizmos.color = Color.gray;
            Gizmos.DrawSphere(cachedProjectedTopLineStart, 0.2f);
            Gizmos.DrawSphere(cachedProjectedTopLineEnd, 0.2f);
        }

        // Keel Points
        Gizmos.color = Color.green; Gizmos.DrawSphere(cachedKeelBowPoint, 0.2f);
        Gizmos.color = Color.red; Gizmos.DrawSphere(cachedKeelSternPoint, 0.2f);

        // 2. Stem Rake
        Gizmos.color = Color.yellow;
        DrawCubicBezierCurve(cachedKeelBowPoint, cachedStemP1, cachedStemP2, cachedStemTopPoint, Color.yellow);
        Gizmos.color = Color.green; Gizmos.DrawSphere(cachedStemTopPoint, 0.2f);
        
        // 3. Stern Post Rake
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(cachedKeelSternPoint, cachedSternPostTopPoint);
        Gizmos.color = Color.red; Gizmos.DrawSphere(cachedSternPostTopPoint, 0.2f);
        

        // Counter
        if (enableCounter)
        {
            DrawCubicBezierCurve(cachedCounterBasePoint, cachedCounterCp1, cachedCounterCp2, cachedCounterTopPoint, Color.yellow);
            Gizmos.color = Color.yellow; Gizmos.DrawSphere(cachedCounterTopPoint, 0.2f);
        }

        // Transom
        Gizmos.color = Color.blue;
        Vector3 transomBase = (enableCounter) ? cachedCounterTopPoint : cachedSternPostTopPoint;
        Vector3 transomTopPoint = cachedTransomTopPoint;
        Gizmos.DrawLine(transomBase, transomTopPoint);
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(cachedTransomTopPoint, 0.2f);

        // --- DRAW OUTER PROFILE THICKNESS ---
        if (drawOuterKeelProfile)
        {
            List<Vector3> innerPts;
            List<Vector3> outerPts;
            GetKeelSpineProfiles(out innerPts, out outerPts);
            
            Gizmos.color = Color.cyan; // High contrast for Unity background
            
            for (int i = 0; i < outerPts.Count; i++)
            {
                if (i > 0) Gizmos.DrawLine(outerPts[i-1], outerPts[i]);
            }
            
            // Draw enclosing caps
            if (innerPts.Count > 0 && outerPts.Count > 0)
            {
                // Horizontal cap at the stern post
                Gizmos.DrawLine(innerPts[0], outerPts[0]);
                
                // Perpendicular cap at the stem
                Gizmos.DrawLine(innerPts[innerPts.Count-1], outerPts[outerPts.Count-1]);
            }
        }

        if (!drawDetailedLines) return;

        // 4. Base Deck
        DrawCubicBezierCurve(cachedBaseDeckMidshipPoint, cachedBaseDeckMidCpFwd, cachedBaseDeckBowCp, cachedBaseDeckBowIntersect, Color.white);
        DrawCubicBezierCurve(cachedBaseDeckMidshipPoint, cachedBaseDeckMidCpAft, cachedBaseDeckSternCp, cachedBaseDeckSternIntersect, Color.white);
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(cachedBaseDeckBowIntersect, 0.2f);
        Gizmos.DrawSphere(cachedBaseDeckMidshipPoint, 0.2f);
        Gizmos.DrawSphere(cachedBaseDeckSternIntersect, 0.2f);

        // 4b. Main Sheer Line
        DrawCubicBezierCurve(cachedMainSheerMidshipPoint, cachedMainSheerMidCpFwd, cachedMainSheerBowCp, cachedMainSheerBowIntersect, mainSheerLineColor);
        DrawCubicBezierCurve(cachedMainSheerMidshipPoint, cachedMainSheerMidCpAft, cachedMainSheerSternCp, cachedMainSheerSternIntersect, mainSheerLineColor);
        Gizmos.color = mainSheerLineColor;
        Gizmos.DrawSphere(cachedMainSheerBowIntersect, 0.18f);
        Gizmos.DrawSphere(cachedMainSheerMidshipPoint, 0.18f);
        Gizmos.DrawSphere(cachedMainSheerSternIntersect, 0.18f);

        // 5. HoB
        DrawCubicBezierCurve(cachedHobMidshipPoint, cachedHobMidCpFwd, cachedHobBowCp, cachedHobBowIntersect, hobLineColor);
        DrawCubicBezierCurve(cachedHobMidshipPoint, cachedHobMidCpAft, cachedHobSternCp, cachedHobSternIntersect, hobLineColor);
        Gizmos.color = hobLineColor;
        Gizmos.DrawSphere(cachedHobBowIntersect, 0.15f);
        Gizmos.DrawSphere(cachedHobMidshipPoint, 0.15f);
        Gizmos.DrawSphere(cachedHobSternIntersect, 0.15f);

        // 5b. Main Wale Line
        if (showMainWale)
        {
            Gizmos.color = mainWaleColor;
            float waleAftY = GetMainWaleYAtZ(cachedMainSheerSternIntersect.z);
            float waleFwdY = GetMainWaleYAtZ(cachedMainSheerBowIntersect.z);
            float aftZ = GetSternProfileZAtY(waleAftY);
            float fwdZ = GetStemProfileZAtY(waleFwdY);
            const int samples = 80;
            Vector3 prevPt = Vector3.zero;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float z = Mathf.Lerp(aftZ, fwdZ, t);
                float y = GetMainWaleYAtZ(z);
                Vector3 currPt = new Vector3(0, y, z);
                if (i > 0) Gizmos.DrawLine(prevPt, currPt);
                prevPt = currPt;
            }
        }

        // 5c. Diagonal Bilge Height Curve
        if (showBilgeLine)
        {
            Gizmos.color = bilgeLineColor;
            float aftZ = GetBilgeSternZ();
            float fwdZ = GetBilgeBowZ();
            const int samples = 80;
            Vector3 prevPt = Vector3.zero;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float z = Mathf.Lerp(aftZ, fwdZ, t);
                float y = GetBilgeYAtZ(z);
                Vector3 currPt = new Vector3(0, y, z);
                if (i > 0) Gizmos.DrawLine(prevPt, currPt);
                prevPt = currPt;
            }
        }

        if (showControlArms)
        {
            Gizmos.color = new Color(hobLineColor.r, hobLineColor.g, hobLineColor.b, 0.6f);
            
            // Midship Forward Handle
            Gizmos.DrawLine(cachedHobMidshipPoint, cachedHobMidCpFwd);
            Gizmos.DrawSphere(cachedHobMidCpFwd, 0.12f);
            
            // Midship Aft Handle
            Gizmos.DrawLine(cachedHobMidshipPoint, cachedHobMidCpAft);
            Gizmos.DrawSphere(cachedHobMidCpAft, 0.12f);

            // Bow Stem Handle
            if (Vector3.Distance(cachedHobBowCp, cachedHobBowIntersect) > 0.001f)
            {
                Gizmos.DrawLine(cachedHobBowIntersect, cachedHobBowCp);
                Gizmos.DrawSphere(cachedHobBowCp, 0.12f);
            }

            // Stern Post Handle
            if (Vector3.Distance(cachedHobSternCp, cachedHobSternIntersect) > 0.001f)
            {
                Gizmos.DrawLine(cachedHobSternIntersect, cachedHobSternCp);
                Gizmos.DrawSphere(cachedHobSternCp, 0.12f);
            }
        }

        // Top Timber Line (Bulwark)
        Gizmos.color = new Color(0.8f, 0.8f, 1f);
        
        float startZ = cachedTopTimberSternIntersect.z;
        float endZ = cachedTopTimberBowIntersect.z;
        
        // 1. Base Top Timber Sheer Line
        Vector3? prevBasePt = null;
        int baseSegs = 80;
        for (int i = 0; i <= baseSegs; i++)
        {
            float z = Mathf.Lerp(startZ, endZ, i / (float)baseSegs);
            float baseDeckY = GetMainSheerYAtZ(z);
            float baseBulwarkY = baseDeckY + bulwarkHeight + GetGlobalSheerOffsetAtZ(z);
            
            if (enableBeakheadBulwark)
            {
                baseBulwarkY = GetTopTimberYAtZ(z, false);
            }

            Vector3 currPt = new Vector3(0, baseBulwarkY, z);
            if (prevBasePt.HasValue) Gizmos.DrawLine(prevBasePt.Value, currPt);
            prevBasePt = currPt;
        }

        // 2. Upper Deck Top Timber Sheer Lines & Vertical Bulkhead Drop Lines
        if (upperDecks != null && upperDecks.Count > 0)
        {
            float totalLength = endZ - startZ;
            foreach (var deck in upperDecks)
            {
                float aftZ = startZ + (deck.globalAftExtent * totalLength);
                float foreZ = startZ + (deck.globalForeExtent * totalLength);

                float deckOffset = GetAbsoluteDeckHeight(deck);
                float upperBulwarkHeight = deckOffset + deck.bulwarkHeight;

                // Upper Deck Bulwark Sheer Line over [aftZ, foreZ]
                Vector3? prevUpperPt = null;
                int upperSegs = 20;
                for (int i = 0; i <= upperSegs; i++)
                {
                    float z = Mathf.Lerp(aftZ, foreZ, i / (float)upperSegs);
                    float y = GetMainSheerYAtZ(z) + upperBulwarkHeight + GetGlobalSheerOffsetAtZ(z);
                    Vector3 currPt = new Vector3(0, y, z);
                    if (prevUpperPt.HasValue) Gizmos.DrawLine(prevUpperPt.Value, currPt);
                    prevUpperPt = currPt;
                }

                // Vertical Bulkhead Drop Lines at Fore/Aft deck boundaries
                if (deck.globalForeExtent < 0.999f)
                {
                    float yUpperFore = GetMainSheerYAtZ(foreZ) + upperBulwarkHeight + GetGlobalSheerOffsetAtZ(foreZ);
                    float yLowerFore = GetMainSheerYAtZ(foreZ) + bulwarkHeight + GetGlobalSheerOffsetAtZ(foreZ);
                    if (deck.parentDeckID != 0)
                    {
                        var parent = upperDecks.Find(d => d.deckID == deck.parentDeckID);
                        if (parent != null)
                        {
                            yLowerFore = GetMainSheerYAtZ(foreZ) + GetAbsoluteDeckHeight(parent) + parent.bulwarkHeight + GetGlobalSheerOffsetAtZ(foreZ);
                        }
                    }

                    Vector3 topForePt = new Vector3(0, yUpperFore, foreZ);
                    Vector3 botForePt = new Vector3(0, yLowerFore, foreZ);
                    Gizmos.DrawLine(topForePt, botForePt);
                }

                if (deck.globalAftExtent > 0.001f)
                {
                    float yUpperAft = GetMainSheerYAtZ(aftZ) + upperBulwarkHeight + GetGlobalSheerOffsetAtZ(aftZ);
                    float yLowerAft = GetMainSheerYAtZ(aftZ) + bulwarkHeight + GetGlobalSheerOffsetAtZ(aftZ);
                    if (deck.parentDeckID != 0)
                    {
                        var parent = upperDecks.Find(d => d.deckID == deck.parentDeckID);
                        if (parent != null)
                        {
                            yLowerAft = GetMainSheerYAtZ(aftZ) + GetAbsoluteDeckHeight(parent) + parent.bulwarkHeight + GetGlobalSheerOffsetAtZ(aftZ);
                        }
                    }

                    Vector3 topAftPt = new Vector3(0, yUpperAft, aftZ);
                    Vector3 botAftPt = new Vector3(0, yLowerAft, aftZ);
                    Gizmos.DrawLine(topAftPt, botAftPt);
                }
            }
        }

        // Draw Upper Decks
        if (upperDecks != null)
        {
            float totalLength = endZ - startZ;
            float aftMostZ = GetAftMostZ();
            float fwdMostZ = GetFwdMostZ();

            foreach (var deck in upperDecks)
            {
                float deckOffset = GetAbsoluteDeckHeight(deck);
                float bulwarkOffset = deckOffset + deck.bulwarkHeight;

                // Draw Full Dashed Line for Deck spanning entire length
                DrawDashedDeckCurve(deckOffset, new Color(0.85f, 0.85f, 0.85f, 0.8f), 80, useBaseDeckSheer: true);

                // Draw Full Dashed Line for Bulwark spanning entire length
                DrawDashedDeckCurve(bulwarkOffset, new Color(0.7f, 0.7f, 0.7f, 0.8f), 80, useBaseDeckSheer: false);

                float aftZ = startZ + (deck.globalAftExtent * totalLength);
                float foreZ = startZ + (deck.globalForeExtent * totalLength);
                
                // Clamp to physical hull bounds at this height
                float aftDeckY = GetBaseDeckYAtZ(aftZ) + deckOffset;
                float aftLimit = GetSternProfileZAtY(aftDeckY);
                if (aftZ < aftLimit) aftZ = aftLimit;
                
                float foreDeckY = GetBaseDeckYAtZ(foreZ) + deckOffset;
                float foreLimit = GetStemProfileZAtY(foreDeckY);
                if (foreZ > foreLimit) foreZ = foreLimit;
                
                Gizmos.color = new Color(1f, 1f, 1f, 0.5f); // semi-transparent white
                
                // Draw deck surface
                Vector3? pDeck = null;
                for (int i = 0; i <= 20; i++)
                {
                    float t = i / 20f;
                    float z = Mathf.Lerp(aftZ, foreZ, t);
                    Vector3 cDeck = new Vector3(0, GetBaseDeckYAtZ(z) + deckOffset, z);
                    if (pDeck.HasValue) Gizmos.DrawLine(pDeck.Value, cDeck);
                    pDeck = cDeck;
                }
                
                Vector3 aftPt = new Vector3(0, aftDeckY, aftZ);
                Vector3 forePt = new Vector3(0, foreDeckY, foreZ);

                // Draw vertical boundaries only if they don't hit the absolute ends of the ship
                if (deck.globalAftExtent > 0.001f) Gizmos.DrawLine(aftPt, aftPt - new Vector3(0, deck.height, 0));
                if (deck.globalForeExtent < 0.999f) Gizmos.DrawLine(forePt, forePt - new Vector3(0, deck.height, 0));
            }
        }

        // Draw Lower Decks
        if (lowerDecks != null)
        {
            float totalLength = endZ - startZ;
            foreach (var deck in lowerDecks)
            {
                float aftZ = startZ + (deck.aftExtent * totalLength);
                float foreZ = startZ + (deck.foreExtent * totalLength);
                
                float yDrop = GetLowerDeckYDrop(deck);
                
                // Clamp to physical hull bounds (roughly using Base Deck limits for now)
                float aftLimit = GetSternProfileZAtY(GetMainSheerYAtZ(aftZ) - yDrop);
                if (aftZ < aftLimit) aftZ = aftLimit;
                
                float foreLimit = GetStemProfileZAtY(GetMainSheerYAtZ(foreZ) - yDrop);
                if (foreZ > foreLimit) foreZ = foreLimit;

                Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f); // semi-transparent blue
                
                Vector3? pDeck = null;
                for (int i = 0; i <= 20; i++)
                {
                    float t = i / 20f;
                    float z = Mathf.Lerp(aftZ, foreZ, t);
                    Vector3 cDeck = new Vector3(0, GetMainSheerYAtZ(z) - yDrop, z);
                    if (pDeck.HasValue) Gizmos.DrawLine(pDeck.Value, cDeck);
                    pDeck = cDeck;
                }
                
                Vector3 aftPt = new Vector3(0, GetMainSheerYAtZ(aftZ) - yDrop, aftZ);
                Vector3 forePt = new Vector3(0, GetMainSheerYAtZ(foreZ) - yDrop, foreZ);

                if (deck.aftExtent > 0.001f) Gizmos.DrawLine(aftPt, aftPt + new Vector3(0, deck.height, 0));
                if (deck.foreExtent < 0.999f) Gizmos.DrawLine(forePt, forePt + new Vector3(0, deck.height, 0));
            }
        }

        // Upper Stem
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(cachedStemTopPoint, new Vector3(0, GetTopTimberYAtZ(cachedUpperStemTopPoint.z), cachedUpperStemTopPoint.z));

        // 6. Rising Lines
        DrawQuadraticBezierCurve(cachedMidshipRisingPoint, cachedBowP1Ctrl, cachedBowRisingEnd, new Color(1f, 0.5f, 0f, 1f));
        DrawQuadraticBezierCurve(cachedMidshipRisingPoint, cachedSternP1Ctrl, cachedSternRisingEnd, new Color(0.6f, 0.2f, 0.8f, 1f));

        // Projected Stern Rising Line
        if (showProjectedSternRisingLine && cachedProjectedSternRisingEnd.z < cachedSternRisingEnd.z - 0.001f)
        {
            DrawDashedLine(cachedSternRisingEnd, cachedProjectedSternRisingEnd, Color.gray, 20);
        }

        // Projected Bow Rising Line
        if (showProjectedBowRisingLine && cachedProjectedBowRisingEnd.z > cachedBowRisingEnd.z + 0.001f)
        {
            DrawDashedLine(cachedBowRisingEnd, cachedProjectedBowRisingEnd, Color.gray, 20);
        }

        // Projected Bow & Stern Bilge Lines
        if (showProjectedBowBilgeLine && cachedProjectedBowBilgeEnd.z > GetBilgeBowZ() + 0.001f)
        {
            Vector3 bEnd = new Vector3(0, GetBilgeYAtZ(GetBilgeBowZ()), GetBilgeBowZ());
            DrawDashedLine(bEnd, cachedProjectedBowBilgeEnd, Color.gray, 20);
        }
        if (showProjectedSternBilgeLine && cachedProjectedSternBilgeEnd.z < GetBilgeSternZ() - 0.001f)
        {
            Vector3 sEnd = new Vector3(0, GetBilgeYAtZ(GetBilgeSternZ()), GetBilgeSternZ());
            DrawDashedLine(sEnd, cachedProjectedSternBilgeEnd, Color.gray, 20);
        }

        // Beakhead Bulwark Control Points
        if (enableBeakheadBulwark && curveBeakheadBulwark)
        {
            float bowZ = cachedTopTimberBowIntersect.z;
            float sternZ = cachedTopTimberSternIntersect.z;
            float totalLength = bowZ - sternZ;
            float startDropZ = bowZ - (beakheadBulwarkStartPercent * totalLength);
            
            ModularUpperDeck bowDeck = GetHighestActiveDeckAtZ(bowZ);
            float activeBulwarkHeight = (bowDeck != null) ? bowDeck.bulwarkHeight : bulwarkHeight;
            float baseDeckYStart = GetMainSheerYAtZ(startDropZ) + ((bowDeck != null) ? GetAbsoluteDeckHeight(bowDeck) : 0f);
            float baseDeckYEnd = GetMainSheerYAtZ(bowZ) + ((bowDeck != null) ? GetAbsoluteDeckHeight(bowDeck) : 0f);
            
            Vector3 p0 = new Vector3(0, baseDeckYStart + activeBulwarkHeight + GetGlobalSheerOffsetAtZ(startDropZ), startDropZ);
            Vector3 p3 = new Vector3(0, baseDeckYEnd + beakheadBulwarkHeight + GetGlobalSheerOffsetAtZ(bowZ), bowZ);
            
            float dropLengthZ = bowZ - startDropZ;
            float dropHeightY = activeBulwarkHeight - beakheadBulwarkHeight; // absolute height difference

            float topAngleRad = Mathf.Lerp(0f, -Mathf.PI / 2f, beakheadBulwarkTopCurveAngle);
            float bottomAngleRad = Mathf.Lerp(Mathf.PI, Mathf.PI / 2f, beakheadBulwarkBottomCurveAngle);
            float bbcLength = Mathf.Min(beakheadBulwarkCurveLength, 1f);

            float topNormalizedZ = Mathf.Cos(topAngleRad) * bbcLength * dropLengthZ;
            float topNormalizedY = Mathf.Sin(topAngleRad) * bbcLength * dropHeightY; 
            float bottomNormalizedZ = Mathf.Cos(bottomAngleRad) * bbcLength * dropLengthZ; 
            float bottomNormalizedY = Mathf.Sin(bottomAngleRad) * bbcLength * dropHeightY;

            Vector3 p1 = new Vector3(0, p0.y + topNormalizedY, p0.z + topNormalizedZ);
            Vector3 p2 = new Vector3(0, p3.y + bottomNormalizedY, p3.z + bottomNormalizedZ);

            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(p1, 0.2f);
            Gizmos.DrawSphere(p2, 0.2f);
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p3, p2);
        }

        Gizmos.color = new Color(1f, 0.5f, 0f, 1f); Gizmos.DrawSphere(cachedBowRisingEnd, 0.15f);
        Gizmos.color = new Color(0.6f, 0.2f, 0.8f, 1f); Gizmos.DrawSphere(cachedSternRisingEnd, 0.15f);

#if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.LowerCenter;
            labelStyle.contentOffset = new Vector2(0, -10);

            Handles.Label(cachedKeelBowPoint, "Keel Bow", labelStyle);
            Handles.Label(cachedKeelSternPoint, "Keel Stern", labelStyle);
            Handles.Label(cachedStemTopPoint, "Stem Top", labelStyle);
            Handles.Label(cachedSternPostTopPoint, "Stern Post Top", labelStyle);
            Handles.Label(cachedTransomTopPoint, "Transom Top", labelStyle);
            if (enableCounter) Handles.Label(cachedCounterTopPoint, "Counter Top", labelStyle);
            Handles.Label(cachedBaseDeckMidshipPoint, "Base Deck Midship", labelStyle);
            Handles.Label(cachedHobMidshipPoint, "HoB Midship", labelStyle);
            Handles.Label(cachedBowRisingEnd, "Bow Rising Line End", labelStyle);
            Handles.Label(cachedSternRisingEnd, "Stern Rising Line End", labelStyle);
            if (showProjectedAftKeelLine && cachedKeelSternPoint.z > cachedProjectedAftKeelEnd.z + 0.001f)
            {
                Vector3 labelPos = Vector3.Lerp(cachedKeelSternPoint, cachedProjectedAftKeelEnd, 0.5f);
                Handles.Label(labelPos, "Projected Aft Keel Line", labelStyle);
            }
            if (showProjectedSternRisingLine && cachedProjectedSternRisingEnd.z < cachedSternRisingEnd.z - 0.001f)
            {
                Vector3 labelPos = Vector3.Lerp(cachedSternRisingEnd, cachedProjectedSternRisingEnd, 0.5f);
                Handles.Label(labelPos, "Projected Stern Rising Line", labelStyle);
            }
            if (showProjectedBowRisingLine && cachedProjectedBowRisingEnd.z > cachedBowRisingEnd.z + 0.001f)
            {
                Vector3 labelPos = Vector3.Lerp(cachedBowRisingEnd, cachedProjectedBowRisingEnd, 0.5f);
                Handles.Label(labelPos, "Projected Bow Rising Line", labelStyle);
            }
            if (showProjectedTopLine)
            {
                Vector3 labelPos = new Vector3(0, GetPureSheerYAtZ(0), 0);
                Handles.Label(labelPos, "Top Line", labelStyle);
            }
        }
#endif
    }

    public virtual float GetTopLineYAtZ(float z)
    {
        return GetPureSheerYAtZ(z);
    }

    public virtual float GetPureSheerYAtZ(float z)
    {
        float globalOffset = GetGlobalSheerOffsetAtZ(z);
        if (upperDecks != null && upperDecks.Count > 0)
        {
            return GetMainSheerYAtZ(z) + GetMaxDeckHeight() + globalOffset;
        }
        return GetMainSheerYAtZ(z) + bulwarkHeight + globalOffset;
    }

    protected void DrawDashedDeckCurve(float heightOffset, Color color, int dashCount = 80, bool useBaseDeckSheer = true)
    {
        float startZ = GetAftMostZ();
        float endZ = GetFwdMostZ();
        Gizmos.color = color;
        float startY = useBaseDeckSheer ? GetBaseDeckYAtZ(startZ) : GetMainSheerYAtZ(startZ);
        Vector3 previousPoint = new Vector3(0, startY + heightOffset, startZ);
        for (int i = 1; i <= dashCount; i++)
        {
            float zCurr = Mathf.Lerp(startZ, endZ, i / (float)dashCount);
            float yCurr = useBaseDeckSheer ? GetBaseDeckYAtZ(zCurr) : GetMainSheerYAtZ(zCurr);
            Vector3 currentPoint = new Vector3(0, yCurr + heightOffset, zCurr);
            if (i % 2 != 0)
            {
                Gizmos.DrawLine(previousPoint, currentPoint);
            }
            previousPoint = currentPoint;
        }
    }

    protected void DrawDashedTopLineCurve(float startZ, float endZ, Color color, int dashCount = 80)
    {
        Gizmos.color = color;
        Vector3 previousPoint = new Vector3(0, GetPureSheerYAtZ(startZ), startZ);
        for (int i = 1; i <= dashCount; i++)
        {
            float zCurr = Mathf.Lerp(startZ, endZ, i / (float)dashCount);
            Vector3 currentPoint = new Vector3(0, GetPureSheerYAtZ(zCurr), zCurr);
            if (i % 2 != 0)
            {
                Gizmos.DrawLine(previousPoint, currentPoint);
            }
            previousPoint = currentPoint;
        }
    }

    protected void DrawDashedLine(Vector3 start, Vector3 end, Color color, int dashCount = 20)
    {
        Gizmos.color = color;
        for (int i = 0; i < dashCount; i++)
        {
            if (i % 2 != 0) continue;
            float t1 = (float)i / dashCount;
            float t2 = (float)(i + 1) / dashCount;
            Gizmos.DrawLine(Vector3.Lerp(start, end, t1), Vector3.Lerp(start, end, t2));
        }
    }

    protected void DrawDashedQuadraticBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, float tStart, float tEnd, Color color, int segments = 20)
    {
        Gizmos.color = color;
        Vector3 previousPoint = CalculateQuadraticBezierPoint(tStart, p0, p1, p2);
        for (int i = 1; i <= segments; i++)
        {
            float tPrev = Mathf.Lerp(tStart, tEnd, (i - 1) / (float)segments);
            float tCurr = Mathf.Lerp(tStart, tEnd, i / (float)segments);
            Vector3 currentPoint = CalculateQuadraticBezierPoint(tCurr, p0, p1, p2);
            if (i % 2 != 0)
            {
                Gizmos.DrawLine(previousPoint, currentPoint);
            }
            previousPoint = currentPoint;
        }
    }

    private void DrawQuadraticBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Color color, int segments = 20)
    {
        Gizmos.color = color;
        Vector3 previousPoint = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 currentPoint = CalculateQuadraticBezierPoint(t, p0, p1, p2);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
        
        if (showControlArms)
        {
            Color faintColor = color;
            faintColor.a = 0.4f;
            Gizmos.color = faintColor;
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p2, p1);
            Gizmos.DrawWireSphere(p1, 0.1f);
        }
    }

    private void DrawCubicBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color, int segments = 20)
    {
        Gizmos.color = color;
        Vector3 previousPoint = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 currentPoint = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
        
        if (showControlArms)
        {
            Color faintColor = color;
            faintColor.a = 0.4f;
            Gizmos.color = faintColor;
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p3, p2);
            Gizmos.DrawWireSphere(p1, 0.1f);
            Gizmos.DrawWireSphere(p2, 0.1f);
        }
    }

    public void GetKeelSpineProfiles(out List<Vector3> innerPts, out List<Vector3> outerPts)
    {
        innerPts = new List<Vector3>();
        outerPts = new List<Vector3>();
        List<float> thicknesses = new List<float>();

        // 1. Stern Post (Top to Keel)
        innerPts.Add(cachedSternPostTopPoint);
        thicknesses.Add(sternPostThickness * keelThickness);
        innerPts.Add(cachedKeelSternPoint);
        thicknesses.Add(keelThickness); // Bottom of stern post is full keel thickness

        // 2. Keel (Stern to Bow)
        int keelSegments = 20;
        for (int i = 1; i <= keelSegments; i++)
        {
            float t = (float)i / keelSegments;
            Vector3 pt;
            if (keelMidshipDrop != 0f)
            {
                if (t < 0.5f)
                {
                    float localT = t * 2f;
                    Vector3 cp = new Vector3(0, 0, cachedKeelSternPoint.z / 2f);
                    pt = CalculateQuadraticBezierPoint(localT, cachedKeelSternPoint, cp, cachedMidshipKeel);
                }
                else
                {
                    float localT = (t - 0.5f) * 2f;
                    Vector3 cp = new Vector3(0, 0, cachedKeelBowPoint.z / 2f);
                    pt = CalculateQuadraticBezierPoint(localT, cachedMidshipKeel, cp, cachedKeelBowPoint);
                }
            }
            else
            {
                pt = Vector3.Lerp(cachedKeelSternPoint, cachedKeelBowPoint, t);
            }
            innerPts.Add(pt);
            thicknesses.Add(keelThickness); // Constant along keel
        }

        // 3. Stem (Bow to Top)
        int stemSegments = 20;
        for (int i = 1; i <= stemSegments; i++)
        {
            float t = (float)i / stemSegments;
            Vector3 pt = CalculateCubicBezierPoint(t, cachedKeelBowPoint, cachedStemP1, cachedStemP2, cachedStemTopPoint);
            innerPts.Add(pt);
            thicknesses.Add(keelThickness); // Keel and Stem share the same thickness
        }

        // 4. Upper Stem Vertical Extension (Base Deck to Bulwark Top)
        if (cachedUpperStemTopPoint.y > cachedStemTopPoint.y + 0.001f)
        {
            innerPts.Add(cachedUpperStemTopPoint);
            thicknesses.Add(keelThickness);
        }

        // Calculate normals and determine outer profile
        for (int i = 0; i < innerPts.Count; i++)
        {
            Vector3 outerPt;

            if (i == 0) // The Stern Post Top
            {
                Vector3 tVecSP = (innerPts[1] - innerPts[0]).normalized;
                Vector3 nVecSP = new Vector3(0, -tVecSP.z, tVecSP.y);
                Vector3 p1 = innerPts[0] + nVecSP * thicknesses[0];
                
                // Project backward along the TAPERED outer line until Y matches the inner top point Y (Horizontal cut)
                Vector3 bottomOffset = innerPts[1] + nVecSP * thicknesses[1];
                Vector3 trueTaperTangent = (bottomOffset - p1).normalized;

                if (Mathf.Abs(trueTaperTangent.y) > 0.001f)
                {
                    float u = (innerPts[0].y - p1.y) / trueTaperTangent.y;
                    outerPt = p1 + u * trueTaperTangent;
                }
                else
                {
                    outerPt = p1; // Fallback
                }
                outerPts.Add(outerPt);
            }
            else if (i == 1) // The Heel Corner (SternPost meets Keel)
            {
                Vector3 tVecSP = (innerPts[1] - innerPts[0]).normalized;
                Vector3 nVecSP = new Vector3(0, -tVecSP.z, tVecSP.y);
                Vector3 topOffset = innerPts[0] + nVecSP * thicknesses[0];
                Vector3 p1 = innerPts[1] + nVecSP * thicknesses[1];
                Vector3 trueTaperTangent = (p1 - topOffset).normalized; // Tapered direction
                
                Vector3 tVecKeel = (innerPts[2] - innerPts[1]).normalized;
                Vector3 nVecKeel = new Vector3(0, -tVecKeel.z, tVecKeel.y);
                Vector3 p2 = innerPts[1] + nVecKeel * keelThickness;

                // True Line Intersection in 2D (Y, Z plane)
                float denom = (trueTaperTangent.y * tVecKeel.z) - (trueTaperTangent.z * tVecKeel.y);
                if (Mathf.Abs(denom) > 0.0001f)
                {
                    float u = ((p2.y - p1.y) * tVecKeel.z - (p2.z - p1.z) * tVecKeel.y) / denom;
                    outerPt = p1 + u * trueTaperTangent;
                }
                else
                {
                    // Fallback
                    outerPt = innerPts[1] + (nVecSP * thicknesses[1]) + (nVecKeel * keelThickness);
                }
                
                outerPts.Add(outerPt);
            }
            else
            {
                Vector3 tVec;
                if (i == innerPts.Count - 1)
                {
                    if (stemInheritsSheerAngle)
                    {
                        // The top cut inherits the sheer curve tangent (Top Timber)
                        float topZ = innerPts[i].z;
                        // Find a slightly backward Z to calculate tangent of the sheer
                        float backZ = topZ - 0.05f;
                        float topY = GetTopTimberYAtZ(topZ, true);
                        float backY = GetTopTimberYAtZ(backZ, true);
                        
                        Vector3 sheerTangent = new Vector3(0, topY - backY, topZ - backZ).normalized;
                        // The sheer tangent goes FORWARD. The cap should be cut along this tangent.
                        // So the top outer point should be projected along sheerTangent.
                        
                        // First find the perpendicular normal as a baseline to get physical thickness
                        Vector3 stemTangent = (innerPts[i] - innerPts[i - 1]).normalized;
                        Vector3 stemNormal = new Vector3(0, -stemTangent.z, stemTangent.y);
                        Vector3 perpOuter = innerPts[i] + (stemNormal * thicknesses[i]);
                        
                        // Now we want the line from innerPts[i] to outerPt to be parallel to sheerTangent.
                        // BUT we want to preserve the HORIZONTAL or PERPENDICULAR physical thickness?
                        // If we cut it flush, the actual thickness along the stem normal must be preserved.
                        // So we intersect a ray (innerPts[i] along -sheerTangent) with a plane (parallel to stem, passing through perpOuter)
                        
                        // Let's do a 2D line intersection.
                        // Line 1 (Outer surface): point = perpOuter, dir = stemTangent
                        // Line 2 (Cap cut): point = innerPts[i], dir = sheerTangent
                        
                        float denom = (stemTangent.y * sheerTangent.z) - (stemTangent.z * sheerTangent.y);
                        if (Mathf.Abs(denom) > 0.0001f)
                        {
                            float u = ((innerPts[i].y - perpOuter.y) * sheerTangent.z - (innerPts[i].z - perpOuter.z) * sheerTangent.y) / denom;
                            outerPt = perpOuter + u * stemTangent;
                        }
                        else
                        {
                            outerPt = perpOuter; // Fallback
                        }
                    }
                    else
                    {
                        // Perpendicular cut
                        tVec = (innerPts[i] - innerPts[i - 1]).normalized;
                        Vector3 nVec = new Vector3(0, -tVec.z, tVec.y);
                        outerPt = innerPts[i] + (nVec * thicknesses[i]);
                    }
                }
                else
                {
                    // For the curved stem, we can average the normals of adjacent segments
                    tVec = (innerPts[i + 1] - innerPts[i - 1]).normalized;
                    Vector3 nVec = new Vector3(0, -tVec.z, tVec.y);
                    outerPt = innerPts[i] + (nVec * thicknesses[i]);
                }
                
                outerPts.Add(outerPt);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SheerPlan))]
public class SheerPlanEditor : Editor
{
    private bool hobFoldout = false; // default collapsed
    private Dictionary<int, bool> foldouts = new Dictionary<int, bool>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.name == "hobMidshipHeight" ||
                iterator.name == "hobBowHeight" ||
                iterator.name == "hobSternHeight" ||
                iterator.name == "hobDropAft" ||
                iterator.name == "hobCurveControlLength" ||
                iterator.name == "upperDecks" ||
                iterator.name == "beakheadBulwarkHeight" ||
                iterator.name == "beakheadBulwarkStartPercent" ||
                iterator.name == "curveBeakheadBulwark" ||
                iterator.name == "beakheadBulwarkTopCurveAngle" ||
                iterator.name == "beakheadBulwarkBottomCurveAngle" ||
                iterator.name == "beakheadBulwarkCurveLength")
            {
                continue;
            }

            if (iterator.name == "baseDeckSheerFwd")
            {
                SheerPlan s = (SheerPlan)target;
                float maxVal = Mathf.Max(0.0001f, s.mainSheerFwd);
                SerializedProperty prop = serializedObject.FindProperty("baseDeckSheerFwd");
                prop.floatValue = EditorGUILayout.Slider(new GUIContent("Base Deck Sheer Fwd", iterator.tooltip), prop.floatValue, 0f, maxVal);
                continue;
            }

            if (iterator.name == "baseDeckSheerAft")
            {
                SheerPlan s = (SheerPlan)target;
                float maxVal = Mathf.Max(0.0001f, s.mainSheerAft);
                SerializedProperty prop = serializedObject.FindProperty("baseDeckSheerAft");
                prop.floatValue = EditorGUILayout.Slider(new GUIContent("Base Deck Sheer Aft", iterator.tooltip), prop.floatValue, 0f, maxVal);
                continue;
            }

            if (iterator.name == "m_Script")
            {
                GUI.enabled = false;
                EditorGUILayout.PropertyField(iterator, true);
                GUI.enabled = true;
            }
            else
            {
                EditorGUILayout.PropertyField(iterator, true);
            }

            // Immediately after customizeHob, draw the foldout if checked
            if (iterator.name == "customizeHob")
            {
                SheerPlan s = (SheerPlan)target;
                if (s.customizeHob)
                {
                    EditorGUILayout.Space(5);
                    hobFoldout = EditorGUILayout.Foldout(hobFoldout, "HoB Custom Controls", true, EditorStyles.foldoutHeader);
                    if (hobFoldout)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("hobMidshipHeight"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("hobBowHeight"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("hobSternHeight"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("hobDropAft"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("hobCurveControlLength"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.Space(5);
                }
            }

            if (iterator.name == "enableBeakheadBulwark")
            {
                if (serializedObject.FindProperty("enableBeakheadBulwark").boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("beakheadBulwarkHeight"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("beakheadBulwarkStartPercent"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("curveBeakheadBulwark"));
                    if (serializedObject.FindProperty("curveBeakheadBulwark").boolValue)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("beakheadBulwarkTopCurveAngle"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("beakheadBulwarkBottomCurveAngle"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("beakheadBulwarkCurveLength"));
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(5);
                }
            }
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Upper Decks (Hierarchy)", EditorStyles.boldLabel);

        SheerPlan script = (SheerPlan)target;
        Undo.RecordObject(script, "Modify Upper Decks");

        bool changed = false;

        if (script.upperDecks == null) script.upperDecks = new List<SheerPlan.ModularUpperDeck>();

        // Draw Tier 1 decks (parent 0)
        DrawDeckNodesRecursive(script, 0, 0, ref changed);

        EditorGUILayout.Space();
        if (GUILayout.Button("[+] Add Base Deck Section (Tier 1)", GUILayout.Height(30)))
        {
            int maxID = 0;
            foreach (var d in script.upperDecks) if (d.deckID > maxID) maxID = d.deckID;
            script.upperDecks.Add(new SheerPlan.ModularUpperDeck { height = 2f, parentDeckID = 0, deckID = maxID + 1 });
            changed = true;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lower Decks (Tiers)", EditorStyles.boldLabel);
        
        if (script.lowerDecks == null) script.lowerDecks = new List<SheerPlan.ModularLowerDeck>();

        int maxLowerTier = 0;
        foreach (var ld in script.lowerDecks) if (ld.tierLevel > maxLowerTier) maxLowerTier = ld.tierLevel;

        for (int t = 1; t <= maxLowerTier; t++)
        {
            EditorGUILayout.LabelField($"Tier {t}", EditorStyles.boldLabel);
            for (int i = 0; i < script.lowerDecks.Count; i++)
            {
                if (script.lowerDecks[i].tierLevel == t)
                {
                    var ld = script.lowerDecks[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    ld.name = EditorGUILayout.TextField(ld.name);
                    if (GUILayout.Button("[X] Remove", GUILayout.Width(80)))
                    {
                        script.lowerDecks.RemoveAt(i);
                        changed = true;
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        goto breakTiers; // Break out safely
                    }
                    EditorGUILayout.EndHorizontal();

                    float newHeight = EditorGUILayout.FloatField("Height", ld.height);
                    if (newHeight != ld.height) { ld.height = newHeight; changed = true; }

                    float newFore = EditorGUILayout.Slider("Fore Extent (Relative)", ld.foreExtent, 0f, 1f);
                    if (newFore != ld.foreExtent) { ld.foreExtent = newFore; changed = true; }

                    float newAft = EditorGUILayout.Slider("Aft Extent (Relative)", ld.aftExtent, 0f, 1f);
                    if (newAft != ld.aftExtent) { ld.aftExtent = newAft; changed = true; }

                    EditorGUILayout.EndVertical();
                }
            }
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(15f);
            if (GUILayout.Button($"[+] Add Platform to Tier {t}", GUILayout.Height(20)))
            {
                script.lowerDecks.Add(new SheerPlan.ModularLowerDeck { height = 2f, tierLevel = t, aftExtent = 0f, foreExtent = 1f });
                changed = true;
                EditorGUILayout.EndHorizontal();
                goto breakTiers;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
        
        breakTiers:
        
        EditorGUILayout.Space();
        if (GUILayout.Button($"[+] Add New Tier (Tier {maxLowerTier + 1})", GUILayout.Height(30)))
        {
            script.lowerDecks.Add(new SheerPlan.ModularLowerDeck { height = 2f, tierLevel = maxLowerTier + 1, aftExtent = 0f, foreExtent = 1f });
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(script);
            script.UpdateCachedValues();
        }
    }

    private void DrawDeckNodesRecursive(SheerPlan script, int parentID, int indentLevel, ref bool changed, int recursionDepth = 0)
    {
        if (recursionDepth > 100) return; // Hard prevent infinite recursion in Editor

        for (int i = 0; i < script.upperDecks.Count; i++)
        {
            SheerPlan.ModularUpperDeck deck = script.upperDecks[i];
            
            // Auto-fix corrupt data that causes infinite loops (e.g. deck ID is 0 or points to itself)
            if (deck.deckID == 0 || deck.deckID == deck.parentDeckID)
            {
                int maxID = 0;
                foreach (var d in script.upperDecks) if (d.deckID > maxID) maxID = d.deckID;
                deck.deckID = maxID + 1;
                changed = true;
            }

            if (deck.parentDeckID == parentID)
            {
                EditorGUI.indentLevel = indentLevel;

                if (!foldouts.ContainsKey(deck.deckID)) foldouts[deck.deckID] = true;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.BeginHorizontal();
                foldouts[deck.deckID] = EditorGUILayout.Foldout(foldouts[deck.deckID], deck.name, true);
                if (GUILayout.Button("[X] Remove", GUILayout.Width(80)))
                {
                    RemoveDeckRecursive(script, deck.deckID);
                    changed = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return; // Break out to avoid list mutation errors
                }
                EditorGUILayout.EndHorizontal();

                if (foldouts[deck.deckID])
                {
                    EditorGUI.indentLevel = indentLevel + 1;
                    
                    float newHeight = EditorGUILayout.FloatField("Height", deck.height);
                    if (newHeight != deck.height) { deck.height = newHeight; changed = true; }
                    
                    float newBulwarkHeight = EditorGUILayout.FloatField("Bulwark Height", deck.bulwarkHeight);
                    if (newBulwarkHeight != deck.bulwarkHeight) { deck.bulwarkHeight = newBulwarkHeight; changed = true; }

                    float newFore = EditorGUILayout.Slider("Fore Extent (Relative)", deck.foreExtent, 0f, 1f);
                    if (newFore != deck.foreExtent) { deck.foreExtent = newFore; changed = true; }
                    
                    float newAft = EditorGUILayout.Slider("Aft Extent (Relative)", deck.aftExtent, 0f, 1f);
                    if (newAft != deck.aftExtent) { deck.aftExtent = newAft; changed = true; }

                    EditorGUILayout.Space();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                    if (GUILayout.Button("[+] Add Deck On Top", GUILayout.Height(20)))
                    {
                        int maxID = 0;
                        foreach (var d in script.upperDecks) if (d.deckID > maxID) maxID = d.deckID;
                        script.upperDecks.Add(new SheerPlan.ModularUpperDeck { 
                            height = 2f, 
                            parentDeckID = deck.deckID, 
                            deckID = maxID + 1,
                            aftExtent = 0f,
                            foreExtent = 1f
                        });
                        changed = true;
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        return; // Break to avoid mutation errors
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space();
                }
                
                EditorGUILayout.EndVertical();
                
                // Recurse for children
                DrawDeckNodesRecursive(script, deck.deckID, indentLevel + 1, ref changed, recursionDepth + 1);
            }
        }
    }

    private void RemoveDeckRecursive(SheerPlan script, int targetDeckID, int depth = 0)
    {
        if (depth > 100) return;
        
        // Find all children first and delete them recursively
        for (int i = script.upperDecks.Count - 1; i >= 0; i--)
        {
            if (script.upperDecks[i].parentDeckID == targetDeckID)
            {
                RemoveDeckRecursive(script, script.upperDecks[i].deckID, depth + 1);
            }
        }
        
        // Now delete target deck
        int idx = script.upperDecks.FindIndex(d => d.deckID == targetDeckID);
        if (idx != -1)
        {
            script.upperDecks.RemoveAt(idx);
        }
    }
}
#endif
