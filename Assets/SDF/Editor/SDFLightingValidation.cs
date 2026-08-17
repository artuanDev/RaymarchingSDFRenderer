using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SdfRenderer.Editor
{
    /// <summary>
    /// Deterministic command-line capture used to validate the SDF/URP comparison
    /// scene without depending on the Scene view or a manually positioned editor.
    /// </summary>
    public static class SDFLightingValidation
    {
        private const string ScenePath = "Assets/SDF/Samples/SDFStandardComparative.unity";
        private const string SettingsPath = "Assets/SDF/Settings/SDFRenderSettings.asset";
        private const int Width = 1280;
        private const int Height = 720;

        [MenuItem("Tools/SDF/Capture Lighting Validation")]
        public static void CaptureFromMenu() => Capture(false);

        public static void CaptureFromCommandLine()
        {
            try
            {
                Capture(true);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Capture(bool commandLine)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null)
                throw new InvalidOperationException($"No camera exists in {ScenePath}.");

            SDFRenderSettings settings = AssetDatabase.LoadAssetAtPath<SDFRenderSettings>(SettingsPath);
            if (settings == null)
                throw new InvalidOperationException($"Could not load {SettingsPath}.");

            SerializedObject serializedSettings = new SerializedObject(settings);
            SerializedProperty aoEnabled = serializedSettings.FindProperty("m_SdfAmbientOcclusion");
            SerializedProperty aoStrength = serializedSettings.FindProperty("m_AmbientOcclusionStrength");
            bool originalAoEnabled = aoEnabled.boolValue;
            float originalAoStrength = aoStrength.floatValue;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not resolve the Unity project root.");

            try
            {
                aoEnabled.boolValue = true;
                aoStrength.floatValue = Mathf.Max(0.8f, originalAoStrength);
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Settings);
                CaptureCamera(camera, Path.Combine(projectRoot, "SDFLightingValidation_AO.png"));

                aoStrength.floatValue = 0f;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Settings);
                CaptureCamera(camera, Path.Combine(projectRoot, "SDFLightingValidation_NoAO.png"));
            }
            finally
            {
                aoEnabled.boolValue = originalAoEnabled;
                aoStrength.floatValue = originalAoStrength;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            Debug.Log($"SDF_LIGHTING_VALIDATION_COMPLETE root={projectRoot} commandLine={commandLine}");
        }

        private static void CaptureCamera(Camera camera, string outputPath)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                Width,
                Height,
                32,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            renderTexture.name = "SDF Lighting Validation";
            renderTexture.antiAliasing = 1;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Texture2D image = null;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                image = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0, false);
                image.Apply(false, false);
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (image != null)
                    UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }
    }
}
