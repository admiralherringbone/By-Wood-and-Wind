using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class TransomTimbers : MonoBehaviour
{
    [Header("Component References")]
    public SheerPlan sheerPlan;
    public BodyPlan bodyPlan;
    public BreadthPlan breadthPlan;
    public FramingModel framingModel;
    public FashionTimberModel fashionTimberModel;
    public SternTimbersModel sternTimbersModel;
    public FramingModelRibbands framingModelRibbands;

    [Header("Transom Timber Settings")]
    [Tooltip("Show transverse transom timbers at the stern.")]
    public bool showTransomTimbers = true;

    [Tooltip("Show key profile transom timbers (HoB line, Counter Base, Counter Top).")]
    public bool showProfileTransomTimbers = true;

    [Tooltip("Show transverse transom timbers at ribband intersections above the HoB line.")]
    public bool showRibbandTransomTimbers = true;

    [Tooltip("Show text labels for transom timbers in the Editor scene view.")]
    public bool showLabels = true;

    [Header("Visualization Colors")]
    public Color profileTransomColor = new Color(1f, 0.8f, 0.2f); // Gold / Yellow
    public Color ribbandTransomColor = new Color(0.2f, 0.9f, 0.9f); // Cyan / Aqua

    public struct TransomTimberLine
    {
        public string label;
        public Vector3 portPoint;
        public Vector3 stbdPoint;
        public float y;
        public float z;
        public bool isProfileLine;
    }

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        FindReferences();
    }

    public void FindReferences()
    {
        if (sheerPlan == null) sheerPlan = GetComponentInParent<SheerPlan>();
        if (sheerPlan == null) sheerPlan = FindFirstObjectByType<SheerPlan>();

        if (bodyPlan == null) bodyPlan = GetComponentInParent<BodyPlan>();
        if (bodyPlan == null) bodyPlan = FindFirstObjectByType<BodyPlan>();

        if (breadthPlan == null) breadthPlan = GetComponentInParent<BreadthPlan>();
        if (breadthPlan == null) breadthPlan = FindFirstObjectByType<BreadthPlan>();

        if (framingModel == null) framingModel = GetComponentInParent<FramingModel>();
        if (framingModel == null) framingModel = FindFirstObjectByType<FramingModel>();

        if (fashionTimberModel == null) fashionTimberModel = GetComponentInParent<FashionTimberModel>();
        if (fashionTimberModel == null) fashionTimberModel = FindFirstObjectByType<FashionTimberModel>();

        if (sternTimbersModel == null) sternTimbersModel = GetComponentInParent<SternTimbersModel>();
        if (sternTimbersModel == null) sternTimbersModel = FindFirstObjectByType<SternTimbersModel>();

        if (framingModelRibbands == null) framingModelRibbands = GetComponentInParent<FramingModelRibbands>();
        if (framingModelRibbands == null) framingModelRibbands = FindFirstObjectByType<FramingModelRibbands>();
    }

    private void OnDrawGizmos()
    {
        FindReferences();
        if (sheerPlan == null || bodyPlan == null || breadthPlan == null || fashionTimberModel == null) return;

        sheerPlan.UpdateCachedValues();
        bodyPlan.UpdateCachedCurves();
        breadthPlan.UpdateCachedCurves();

        if (showTransomTimbers)
        {
            DrawTransomTimbers();
        }
    }

    public void DrawTransomTimbers()
    {
        if (!showTransomTimbers) return;

        List<TransomTimberLine> lines = GetTransomTimberLines();

        foreach (var line in lines)
        {
            if (line.isProfileLine && !showProfileTransomTimbers) continue;
            if (!line.isProfileLine && !showRibbandTransomTimbers) continue;

            Gizmos.color = line.isProfileLine ? profileTransomColor : ribbandTransomColor;
            Gizmos.DrawLine(line.portPoint, line.stbdPoint);

#if UNITY_EDITOR
            if (showLabels)
            {
                if (_labelStyle == null)
                {
                    _labelStyle = new GUIStyle();
                    _labelStyle.alignment = TextAnchor.MiddleLeft;
                }
                _labelStyle.normal.textColor = line.isProfileLine ? profileTransomColor : ribbandTransomColor;

                Vector3 labelPos = line.stbdPoint + new Vector3(0.2f, 0f, 0f);
                Handles.Label(labelPos, line.label, _labelStyle);
            }
#endif
        }
    }

    public List<TransomTimberLine> GetTransomTimberLines()
    {
        List<TransomTimberLine> lines = new List<TransomTimberLine>();
        FindReferences();
        if (sheerPlan == null || fashionTimberModel == null) return lines;

        float hobY = sheerPlan.cachedHobSternIntersect.y;

        // -------------------------------------------------------------
        // 1. KEY STERN PROFILE TRANSOM TIMBERS
        // -------------------------------------------------------------

        // A. HoB Line Transom Timber
        Vector3 hobPt = sheerPlan.cachedHobSternIntersect;
        float xHob = fashionTimberModel.GetNaturalHullXAtYZ(hobPt.y, hobPt.z);
        lines.Add(new TransomTimberLine
        {
            label = "HoB Transom",
            portPoint = new Vector3(-xHob, hobPt.y, hobPt.z),
            stbdPoint = new Vector3(xHob, hobPt.y, hobPt.z),
            y = hobPt.y,
            z = hobPt.z,
            isProfileLine = true
        });

        // B. Counter Base & Top Transom Timbers (if counter enabled)
        if (sheerPlan.enableCounter)
        {
            Vector3 cBasePt = sheerPlan.cachedCounterBasePoint;
            float xBase = fashionTimberModel.GetNaturalHullXAtYZ(cBasePt.y, cBasePt.z);
            if (!IsDuplicateLine(lines, cBasePt.y, cBasePt.z))
            {
                lines.Add(new TransomTimberLine
                {
                    label = "Counter Base Transom",
                    portPoint = new Vector3(-xBase, cBasePt.y, cBasePt.z),
                    stbdPoint = new Vector3(xBase, cBasePt.y, cBasePt.z),
                    y = cBasePt.y,
                    z = cBasePt.z,
                    isProfileLine = true
                });
            }

            Vector3 cTopPt = sheerPlan.cachedCounterTopPoint;
            float xTop = fashionTimberModel.GetNaturalHullXAtYZ(cTopPt.y, cTopPt.z);
            if (!IsDuplicateLine(lines, cTopPt.y, cTopPt.z))
            {
                lines.Add(new TransomTimberLine
                {
                    label = "Counter Top Transom",
                    portPoint = new Vector3(-xTop, cTopPt.y, cTopPt.z),
                    stbdPoint = new Vector3(xTop, cTopPt.y, cTopPt.z),
                    y = cTopPt.y,
                    z = cTopPt.z,
                    isProfileLine = true
                });
            }
        }

        // -------------------------------------------------------------
        // 2. RIBBAND TRANSOM TIMBERS (Above HoB Line)
        // -------------------------------------------------------------

        // A. Primary Deck Line
        AddRibbandTransomLine(lines, z => sheerPlan.GetBaseDeckYAtZ(z) + sheerPlan.GetGlobalSheerOffsetAtZ(z), "Primary Deck Transom");

        // B. Lower Deck Lines
        if (sheerPlan.lowerDecks != null)
        {
            foreach (var deck in sheerPlan.lowerDecks)
            {
                float yDrop = sheerPlan.GetLowerDeckYDrop(deck);
                AddRibbandTransomLine(lines, z => sheerPlan.GetBaseDeckYAtZ(z) - yDrop, $"{deck.name} Transom");
            }
        }

        // C. Upper Deck Lines
        if (sheerPlan.upperDecks != null)
        {
            foreach (var deck in sheerPlan.upperDecks)
            {
                float hOffset = sheerPlan.GetAbsoluteDeckHeight(deck);
                AddRibbandTransomLine(lines, z => sheerPlan.GetBaseDeckYAtZ(z) + hOffset + sheerPlan.GetGlobalSheerOffsetAtZ(z), $"{deck.name} Floor Transom");
            }
        }

        // D. Top Timber / Bulwark Lines
        List<float> sheerHeights = new List<float>();
        sheerHeights.Add(sheerPlan.bulwarkHeight);

        if (sheerPlan.upperDecks != null)
        {
            foreach (var deck in sheerPlan.upperDecks)
            {
                float absH = sheerPlan.GetAbsoluteDeckHeight(deck) + deck.bulwarkHeight;
                if (!sheerHeights.Contains(absH)) sheerHeights.Add(absH);
            }
        }

        int bulwarkIndex = 1;
        foreach (float hOffset in sheerHeights)
        {
            AddRibbandTransomLine(lines, z => sheerPlan.GetBaseDeckYAtZ(z) + hOffset + sheerPlan.GetGlobalSheerOffsetAtZ(z), $"Bulwark Ribband Transom {bulwarkIndex++}");
        }


        return lines;
    }

    private void AddRibbandTransomLine(List<TransomTimberLine> lines, Func<float, float> ribbandYFunc, string label)
    {
        Vector3? intersect = FindSternFashionTimberIntersection(ribbandYFunc);
        if (!intersect.HasValue) return;

        Vector3 stbd = intersect.Value;
        if (IsDuplicateLine(lines, stbd.y, stbd.z)) return;

        lines.Add(new TransomTimberLine
        {
            label = label,
            portPoint = new Vector3(-stbd.x, stbd.y, stbd.z),
            stbdPoint = stbd,
            y = stbd.y,
            z = stbd.z,
            isProfileLine = false
        });
    }

    private Vector3? FindSternFashionTimberIntersection(Func<float, float> ribbandYFunc)
    {
        if (fashionTimberModel == null || sheerPlan == null) return null;

        float lowZ = sheerPlan.GetAftMostZ();
        float highZ = sheerPlan.cachedKeelBowPoint.z;

        if (Mathf.Abs(lowZ - highZ) < 0.001f) return null;

        for (int i = 0; i < 25; i++)
        {
            float midZ = (lowZ + highZ) * 0.5f;
            float sternY = sheerPlan.GetSternProfileYAtZ(midZ);
            float ribbandY = ribbandYFunc(midZ);

            if (ribbandY < sternY)
            {
                lowZ = midZ;
            }
            else
            {
                highZ = midZ;
            }
        }

        float finalZ = (lowZ + highZ) * 0.5f;
        float finalY = sheerPlan.GetSternProfileYAtZ(finalZ);
        float hobY = sheerPlan.cachedHobSternIntersect.y;

        // Ensure intersection is at or above HoB line (with 0.005 tolerance)
        if (finalY < hobY - 0.005f) return null;

        float finalX = fashionTimberModel.GetNaturalHullXAtYZ(finalY, finalZ);
        return new Vector3(finalX, finalY, finalZ);
    }

    private bool IsDuplicateLine(List<TransomTimberLine> lines, float y, float z)
    {
        foreach (var l in lines)
        {
            if (Mathf.Abs(l.y - y) < 0.005f && Mathf.Abs(l.z - z) < 0.005f)
            {
                return true;
            }
        }
        return false;
    }

#if UNITY_EDITOR
    private static GUIStyle _labelStyle;
#endif
}
