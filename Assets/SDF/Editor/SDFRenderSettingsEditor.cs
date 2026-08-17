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
                "Shadow and ambient-occlusion controls are global to this renderer. Shadow steps tune the instanced screen-space SDF shadow-volume trace; regular URP shadow maps are still received. URP screen-space AO uses the shared SDF depth/normal prepass; add the URP SSAO renderer feature after this feature to control radius, quality, and sample count. Scene probes provide PBR ambient/reflection lighting, with Ambient Color used as a minimum fallback.",
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
