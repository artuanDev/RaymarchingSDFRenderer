using UnityEditor;
using UnityEngine;

namespace SdfRenderer.Editor
{
    [CustomEditor(typeof(SDFRenderSettings))]
    public sealed class SDFRenderSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Shadow and ambient-occlusion controls are global to this renderer. Shadow steps tune SDF casting into the URP main-light atlas. URP screen-space AO adds the SDF depth/normal prepass required by the URP SSAO renderer feature; radius, quality, and sample count remain controlled by that renderer feature.",
                MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Native-resolution quality presets", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Current", ((SDFRenderSettings)target).QualityPreset.ToString());
            EditorGUILayout.HelpBox("Presets change tracing precision only. They never change camera or render-target resolution.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                PresetButton(SDFQualityPreset.Balanced);
                PresetButton(SDFQualityPreset.High);
                PresetButton(SDFQualityPreset.Ultra);
            }
        }

        private void PresetButton(SDFQualityPreset preset)
        {
            if (!GUILayout.Button(ObjectNames.NicifyVariableName(preset.ToString()))) return;
            foreach (Object item in targets)
            {
                SDFRenderSettings settings = (SDFRenderSettings)item;
                Undo.RecordObject(settings, "Apply SDF Quality Preset");
                settings.ApplyPreset(preset);
                EditorUtility.SetDirty(settings);
            }
        }
    }
}
