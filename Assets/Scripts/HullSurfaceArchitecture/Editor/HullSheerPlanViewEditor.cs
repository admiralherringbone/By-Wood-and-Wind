#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HullSurfaceArchitecture.Editor
{
    /// <summary>
    /// Custom Editor for HullSheerPlanView providing interactive 3D Scene View handles for sheer rise editing.
    /// </summary>
    [CustomEditor(typeof(HullSheerPlanView))]
    public class HullSheerPlanViewEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            HullSheerPlanView sheerView = (HullSheerPlanView)target;
            if (sheerView == null || sheerView.surfaceGrid == null) return;

            MasterHullSurfaceController master = sheerView.masterController;
            float halfLength = (master != null) ? (master.lengthBetweenPerpendiculars / 2f) : 15f;
            Transform targetTransform = sheerView.transform;

            // 1. Midships Sheer Height Handle (Z = 0, Y = sheerHeightMidship)
            Vector3 midPosWorld = targetTransform.TransformPoint(new Vector3(0, sheerView.sheerHeightMidship, 0));
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.magenta;
            Vector3 newMidWorld = Handles.FreeMoveHandle(midPosWorld, 0.15f, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sheerView, "Change Sheer Height Midship");
                Vector3 localPos = targetTransform.InverseTransformPoint(newMidWorld);
                sheerView.sheerHeightMidship = Mathf.Max(0.5f, localPos.y);
                sheerView.TriggerMasterPipeline();
            }

            // 2. Bow Sheer Rise Handle (Z = +L/2, Y = sheerHeightMidship + bowSheerRise)
            float bowSheerTotalY = sheerView.sheerHeightMidship + sheerView.bowSheerRise;
            Vector3 bowPosWorld = targetTransform.TransformPoint(new Vector3(0, bowSheerTotalY, halfLength));
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.magenta;
            Vector3 newBowWorld = Handles.FreeMoveHandle(bowPosWorld, 0.15f, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sheerView, "Change Bow Sheer Rise");
                Vector3 localPos = targetTransform.InverseTransformPoint(newBowWorld);
                sheerView.bowSheerRise = Mathf.Max(0f, localPos.y - sheerView.sheerHeightMidship);
                sheerView.TriggerMasterPipeline();
            }

            // 3. Stern Sheer Rise Handle (Z = -L/2, Y = sheerHeightMidship + sternSheerRise)
            float sternSheerTotalY = sheerView.sheerHeightMidship + sheerView.sternSheerRise;
            Vector3 sternPosWorld = targetTransform.TransformPoint(new Vector3(0, sternSheerTotalY, -halfLength));
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.magenta;
            Vector3 newSternWorld = Handles.FreeMoveHandle(sternPosWorld, 0.15f, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sheerView, "Change Stern Sheer Rise");
                Vector3 localPos = targetTransform.InverseTransformPoint(newSternWorld);
                sheerView.sternSheerRise = Mathf.Max(0f, localPos.y - sheerView.sheerHeightMidship);
                sheerView.TriggerMasterPipeline();
            }

            // Labels
            Handles.Label(midPosWorld + Vector3.up * 0.3f, $"Sheer Height: {sheerView.sheerHeightMidship:F2}m");
            Handles.Label(bowPosWorld + Vector3.up * 0.3f, $"Bow Sheer Rise: +{sheerView.bowSheerRise:F2}m");
            Handles.Label(sternPosWorld + Vector3.up * 0.3f, $"Stern Sheer Rise: +{sheerView.sternSheerRise:F2}m");
        }
    }
}
#endif
