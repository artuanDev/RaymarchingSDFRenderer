using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SdfRenderer.Editor
{
    public static class SDFProjectSetup
    {
        private const string SettingsPath = "Assets/SDF/Settings/SDFRenderSettings.asset";

        [InitializeOnLoadMethod]
        private static void ScheduleSetup() => EditorApplication.delayCall += EnsureInstalledOnce;

        private static void EnsureInstalledOnce()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            InstallRendererFeature(false);
        }

        [MenuItem("Tools/SDF/Install Renderer Feature")]
        public static void InstallRendererFeature() => InstallRendererFeature(true);

        private static void InstallRendererFeature(bool logResult)
        {
            if (!AssetDatabase.IsValidFolder("Assets/SDF/Settings"))
                AssetDatabase.CreateFolder("Assets/SDF", "Settings");
            SDFRenderSettings settings = AssetDatabase.LoadAssetAtPath<SDFRenderSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SDFRenderSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            int installed = 0;
            string[] rendererGuids = AssetDatabase.FindAssets("t:UniversalRendererData");
            foreach (string guid in rendererGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (rendererData == null || rendererData.rendererFeatures.Any(feature => feature is SDFRendererFeature))
                    continue;
                SDFRendererFeature feature = ScriptableObject.CreateInstance<SDFRendererFeature>();
                feature.name = "SDF Full Resolution Renderer";
                SerializedObject serialized = new SerializedObject(feature);
                serialized.FindProperty("m_Settings").objectReferenceValue = settings;
                serialized.FindProperty("m_RaymarchShader").objectReferenceValue = Shader.Find("Hidden/SDF/URPVolumeRaymarch");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
                rendererData.SetDirty();
                EditorUtility.SetDirty(rendererData);
                ++installed;
            }
            if (installed > 0)
                AssetDatabase.SaveAssets();
            if (logResult)
                Debug.Log(installed > 0 ? $"Installed SDF renderer feature into {installed} URP renderer asset(s)." : "SDF renderer feature is already installed.");
        }
    }
}
