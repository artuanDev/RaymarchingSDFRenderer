using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SdfRenderer.Editor
{
    [InitializeOnLoad]
    public static class SDFScenePicker
    {
        private const string PreferenceKey = "SDF.ScenePicking.Enabled";
        private const string MenuPath = "Tools/SDF/Enable Scene Picking";
        private static readonly int PickerControlHint = "SDFScenePicker".GetHashCode();
        private static readonly List<SDFShape> Shapes = new List<SDFShape>(256);
        private static readonly List<Object> SelectionScratch = new List<Object>(16);

        static SDFScenePicker()
        {
            SceneView.duringSceneGui += DuringSceneGui;
            Menu.SetChecked(MenuPath, IsEnabled);
        }

        private static bool IsEnabled => EditorPrefs.GetBool(PreferenceKey, true);

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool enabled = !IsEnabled;
            EditorPrefs.SetBool(PreferenceKey, enabled);
            Menu.SetChecked(MenuPath, enabled);
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidation()
        {
            Menu.SetChecked(MenuPath, IsEnabled);
            return true;
        }

        private static void DuringSceneGui(SceneView sceneView)
        {
            Event current = Event.current;
            if (!IsEnabled || current == null)
                return;

            int pickerControl = GUIUtility.GetControlID(PickerControlHint, FocusType.Passive);
            if (current.type == EventType.Layout)
            {
                // Register only over a real, visible SDF surface. Unlike a default
                // control this does not steal ordinary geometry or empty-space clicks.
                if (TryRaycastSdf(sceneView, current.mousePosition, out _, out _, out _))
                    HandleUtility.AddControl(pickerControl, 0f);
                return;
            }

            if (current.type != EventType.MouseDown ||
                current.button != 0 ||
                current.alt ||
                GUIUtility.hotControl != 0 ||
                HandleUtility.nearestControl != pickerControl)
                return;

            if (!TryRaycastSdf(sceneView, current.mousePosition, out SDFShape bestShape,
                    out float bestDistance, out Ray ray))
                return;

            GameObject target = bestShape.gameObject;
            // PickGameObject performs an internal rendered pick, so it must never run
            // during Layout. On the actual click it lets regular geometry in front of
            // an SDF win while the conditional SDF control remains responsible for the
            // event itself.
            GameObject conventionalPick = HandleUtility.PickGameObject(current.mousePosition, false);
            if (conventionalPick != null && conventionalPick.GetComponentInParent<SDFShape>() == null &&
                (!TryGetConventionalDistance(conventionalPick, ray, out float conventionalDistance) ||
                 conventionalDistance + 0.001f < bestDistance))
                target = conventionalPick;

            // Match Unity's normal selection modifiers: Shift adds to the current
            // selection and Ctrl/Cmd toggles the clicked item.
            ApplySelection(target, current.shift, EditorGUI.actionKey);
            current.Use();
            SceneView.RepaintAll();
        }

        private static bool TryRaycastSdf(SceneView sceneView, Vector2 mousePosition,
            out SDFShape bestShape, out float bestDistance, out Ray ray)
        {
            ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            float maximumDistance = sceneView.camera != null ? sceneView.camera.farClipPlane : 100000f;
            SDFSceneRegistry.GetRegisteredShapes(Shapes);
            bestShape = null;
            bestDistance = maximumDistance;
            for (int i = 0; i < Shapes.Count; ++i)
            {
                SDFShape shape = Shapes[i];
                if (shape == null ||
                    (shape.hideFlags & HideFlags.NotEditable) != 0 ||
                    SceneVisibilityManager.instance.IsHidden(shape.gameObject) ||
                    SceneVisibilityManager.instance.IsPickingDisabled(shape.gameObject))
                    continue;
                if (SDFCpuEvaluator.Raycast(shape, ray, out float distance, bestDistance) && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestShape = shape;
                }
            }
            return bestShape != null;
        }

        private static bool TryGetConventionalDistance(GameObject gameObject, Ray ray, out float distance)
        {
            distance = float.PositiveInfinity;
            bool found = false;
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null && collider.Raycast(ray, out RaycastHit hit, float.PositiveInfinity))
            {
                distance = hit.distance;
                found = true;
            }
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null && renderer.bounds.IntersectRay(ray, out float rendererDistance))
            {
                distance = Mathf.Min(distance, rendererDistance);
                found = true;
            }
            return found;
        }

        private static void ApplySelection(GameObject target, bool additive, bool toggle)
        {
            if (!additive && !toggle)
            {
                Selection.activeGameObject = target;
                return;
            }

            SelectionScratch.Clear();
            SelectionScratch.AddRange(Selection.objects);
            int index = SelectionScratch.IndexOf(target);
            if (toggle && index >= 0)
                SelectionScratch.RemoveAt(index);
            else if (index < 0)
                SelectionScratch.Add(target);
            Selection.objects = SelectionScratch.ToArray();
        }
    }
}
