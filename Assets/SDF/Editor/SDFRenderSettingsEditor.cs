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
                "Shadow and ambient-occlusion controls are global to this renderer. Shadow Softness adds a penumbra estimate inside the existing shadow trace, without extra rays. SDF Ambient Occlusion samples the local distance field; Samples controls its bounded per-hit cost. URP Screen Space AO adds contact between SDFs and regular geometry through the shared depth/normal prepass. Scene probes provide PBR ambient/reflection lighting, with Ambient Color used as a minimum fallback.",
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
