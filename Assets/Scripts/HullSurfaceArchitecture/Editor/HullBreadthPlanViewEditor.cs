#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HullSurfaceArchitecture.Editor
{
    /// <summary>
    /// Custom Editor for HullBreadthPlanView providing interactive 3D Scene View handles.
    /// Allows the user to click and drag spline control arms directly in the Scene View to sculpt hull breadth!
    /// </summary>
    [CustomEditor(typeof(HullBreadthPlanView))]
    public class HullBreadthPlanViewEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            HullBreadthPlanView planView = (HullBreadthPlanView)target;
            if (planView == null || planView.surfaceGrid == null) return;

            MasterHullSurfaceController master = planView.masterController;
            float halfLength = (master != null) ? (master.lengthBetweenPerpendiculars / 2f) : 15f;
            float maxB = planView.maxBeam;
            float hw = planView.surfaceGrid.keelHalfWidth;

            Transform targetTransform = planView.transform;

            Vector3 p0 = targetTransform.TransformPoint(planView.cachedFwdP0);
            Vector3 p1 = targetTransform.TransformPoint(planView.cachedFwdP1);
            Vector3 p2 = targetTransform.TransformPoint(planView.cachedFwdP2);
            Vector3 p3 = targetTransform.TransformPoint(planView.cachedFwdP3);

            Vector3 aftP1 = targetTransform.TransformPoint(planView.cachedAftP1);
            Vector3 aftP2 = targetTransform.TransformPoint(planView.cachedAftP2);
            Vector3 aftP3 = targetTransform.TransformPoint(planView.cachedAftP3);

            // 1. Midships Peak Handle (P3: Z = 0, X = maxBeam)
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.yellow;
            Vector3 newP3World = Handles.FreeMoveHandle(p3, planView.markerGizmoRadius * 1.5f, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(planView, "Change Max Beam");
                Vector3 localPos = targetTransform.InverseTransformPoint(newP3World);
                planView.maxBeam = Mathf.Max(hw + 0.1f, localPos.x);
                planView.TriggerMasterPipeline();
            }

            // 2. Forward Midship Control Arm Handle (P2)
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.green;
            Vector3 newP2World = Handles.FreeMoveHandle(p2, planView.markerGizmoRadius, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(planView, "Change Forward Midship Control Arm");
                Vector3 localPos = targetTransform.InverseTransformPoint(newP2World);
                planView.midshipFwdArmLength = Mathf.Clamp01(localPos.z / halfLength);
                planView.TriggerMasterPipeline();
            }

            // 3. Bow Control Arm Handle (P1)
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.green;
            Vector3 newP1World = Handles.FreeMoveHandle(p1, planView.markerGizmoRadius, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(planView, "Change Bow Control Arm");
                Vector3 localPos = targetTransform.InverseTransformPoint(newP1World);
                float distZ = Mathf.Abs(halfLength - planView.cachedFwdP2.z);
                planView.bowArmLength = Mathf.Clamp01(Mathf.Abs(localPos.z - halfLength) / (distZ > 0.001f ? distZ : 1f));
                planView.TriggerMasterPipeline();
            }

            // 4. Aft Midship Control Arm Handle (Aft P1)
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.cyan;
            Vector3 newAftP1World = Handles.FreeMoveHandle(aftP1, planView.markerGizmoRadius, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(planView, "Change Aft Midship Control Arm");
                Vector3 localPos = targetTransform.InverseTransformPoint(newAftP1World);
                planView.midshipAftArmLength = Mathf.Clamp01(-localPos.z / halfLength);
                planView.TriggerMasterPipeline();
            }

            // Draw Tangent Control Arm Lines
            Handles.color = new Color(1f, 0.5f, 0f, 0.8f);
            Handles.DrawDottedLine(p0, p1, 4f);
            Handles.DrawDottedLine(p3, p2, 4f);
            Handles.DrawDottedLine(p3, aftP1, 4f);
            if (planView.cachedIsTransom) Handles.DrawDottedLine(aftP1, aftP2, 4f);
            else Handles.DrawDottedLine(aftP3, aftP2, 4f);

            // Draw Labels in Scene View
            Handles.Label(p3 + Vector3.right * 0.3f, $"Max Beam: {planView.maxBeam:F2}m");
            Handles.Label(p2 + Vector3.right * 0.3f, $"Fwd Midship Arm: {planView.midshipFwdArmLength:F2}");
            Handles.Label(p1 + Vector3.right * 0.3f, $"Bow Arm: {planView.bowArmLength:F2}");
            Handles.Label(aftP1 + Vector3.right * 0.3f, $"Aft Midship Arm: {planView.midshipAftArmLength:F2}");
        }
    }
}
#endif
