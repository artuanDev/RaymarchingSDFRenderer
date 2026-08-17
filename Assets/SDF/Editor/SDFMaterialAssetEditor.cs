using UnityEditor;
using UnityEngine;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SdfRenderer.Editor
{
    [CustomEditor(typeof(SDFMaterialAsset))]
    public sealed class SDFMaterialAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty model = serializedObject.FindProperty("m_ShadingModel");
            EditorGUILayout.PropertyField(model);
            SDFShadingModel type = (SDFShadingModel)model.enumValueIndex;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BaseColor"));
            if (type != SDFShadingModel.Custom)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BaseMap"));
            if (type == SDFShadingModel.BlinnPhong)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SpecularColor"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SpecularPower"));
            }
            else if (type == SDFShadingModel.Cel)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_CelBands"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SpecularColor"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SpecularPower"));
            }
            else if (type == SDFShadingModel.PbrLike)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Metallic"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Smoothness"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Occlusion"));
            }
            else if (type == SDFShadingModel.Custom)
            {
                SerializedProperty shaderProperty = serializedObject.FindProperty("m_CustomShader");
                EditorGUILayout.PropertyField(shaderProperty);
                SDFShaderAsset shader = shaderProperty.objectReferenceValue as SDFShaderAsset;
                if (shader != null)
                    DrawDeclaredProperties(shader.Source);
                else
                    EditorGUILayout.HelpBox("Assign an imported .sdfshader module to expose its declared properties.", MessageType.Info);
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Emission"));
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDeclaredProperties(string source)
        {
            bool drewCustom0 = false;
            bool drewCustom1 = false;
            string[] lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                Match texture = Regex.Match(line, "^\\s*_BaseMap\\s*\\(\\s*\\\"([^\\\"]+)\\\"\\s*,\\s*2D", RegexOptions.IgnoreCase);
                if (texture.Success)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BaseMap"), new GUIContent(texture.Groups[1].Value));
                    continue;
                }

                Match match = Regex.Match(line,
                    "^\\s*_Custom(?<block>[01])(?<component>[XYZW]?)\\s*\\(\\s*\\\"(?<label>[^\\\"]+)\\\"\\s*,\\s*(?<kind>Color|Vector|Float|Toggle|Range\\(\\s*(?<min>[-+0-9.eE]+)\\s*,\\s*(?<max>[-+0-9.eE]+)\\s*\\)|Enum\\((?<enum>[^)]*)\\))",
                    RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;
                int block = match.Groups["block"].Value == "0" ? 0 : 1;
                SerializedProperty property = serializedObject.FindProperty(block == 0 ? "m_Custom0" : "m_Custom1");
                string component = match.Groups["component"].Value.ToUpperInvariant();
                string label = match.Groups["label"].Value;
                string propertyType = match.Groups["kind"].Value;
                if (component.Length == 0)
                {
                    if ((block == 0 && drewCustom0) || (block == 1 && drewCustom1))
                        continue;
                    if (propertyType.Equals("Color", StringComparison.OrdinalIgnoreCase))
                    {
                        Vector4 vector = property.vector4Value;
                        Color color = EditorGUILayout.ColorField(label, new Color(vector.x, vector.y, vector.z, vector.w));
                        property.vector4Value = new Vector4(color.r, color.g, color.b, color.a);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(property, new GUIContent(label));
                    }
                    if (block == 0) drewCustom0 = true; else drewCustom1 = true;
                    continue;
                }

                Vector4 value = property.vector4Value;
                int index = "XYZW".IndexOf(component, StringComparison.Ordinal);
                float current = value[index];
                float updated;
                if (propertyType.StartsWith("Range", StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(match.Groups["min"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float minimum) &&
                    float.TryParse(match.Groups["max"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float maximum))
                    updated = EditorGUILayout.Slider(label, current, minimum, maximum);
                else if (propertyType.Equals("Toggle", StringComparison.OrdinalIgnoreCase))
                    updated = EditorGUILayout.Toggle(label, current > 0.5f) ? 1f : 0f;
                else if (propertyType.StartsWith("Enum", StringComparison.OrdinalIgnoreCase))
                {
                    string[] tokens = match.Groups["enum"].Value.Split(',');
                    int optionCount = tokens.Length / 2;
                    string[] names = new string[optionCount];
                    int[] values = new int[optionCount];
                    for (int option = 0; option < optionCount; ++option)
                    {
                        names[option] = tokens[option * 2].Trim();
                        int.TryParse(tokens[option * 2 + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[option]);
                    }
                    updated = optionCount > 0 ? EditorGUILayout.IntPopup(label, Mathf.RoundToInt(current), names, values) : current;
                }
                else
                    updated = EditorGUILayout.FloatField(label, current);
                if (!Mathf.Approximately(updated, current))
                {
                    value[index] = updated;
                    property.vector4Value = value;
                }
                if (block == 0) drewCustom0 = true; else drewCustom1 = true;
            }
            if (!drewCustom0 && !drewCustom1)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Custom0"), new GUIContent("Custom 0"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Custom1"), new GUIContent("Custom 1"));
            }
        }

        public override bool HasPreviewGUI() => true;

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            SDFMaterialAsset material = (SDFMaterialAsset)target;
            Texture2D preview = SDFRaymarchPreview.Render(material, Mathf.Clamp(Mathf.RoundToInt(rect.width), 32, 256), Mathf.Clamp(Mathf.RoundToInt(rect.height), 32, 256)) ??
                                RenderPreview(material, Mathf.Clamp(Mathf.RoundToInt(rect.width), 32, 256), Mathf.Clamp(Mathf.RoundToInt(rect.height), 32, 256));
            GUI.DrawTexture(rect, preview, ScaleMode.StretchToFill, false);
            UnityEngine.Object.DestroyImmediate(preview);
        }

        public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height) =>
            SDFRaymarchPreview.Render((SDFMaterialAsset)target, width, height) ?? RenderPreview((SDFMaterialAsset)target, width, height);

        private static Texture2D RenderPreview(SDFMaterialAsset material, int width, int height)
        {
            width = Mathf.Max(1, width); height = Mathf.Max(1, height);
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            Color[] pixels = new Color[width * height];
            Color baseColor = material.BaseColor;
            Vector3 light = new Vector3(-0.4f, 0.7f, -0.6f).normalized;
            for (int y = 0; y < height; ++y)
            for (int x = 0; x < width; ++x)
            {
                float px = (2f * (x + 0.5f) / width - 1f) * (float)width / height;
                float py = 2f * (y + 0.5f) / height - 1f;
                float radius2 = px * px + py * py;
                Color color = new Color(0.08f, 0.09f, 0.11f, 1f);
                if (radius2 <= 0.72f)
                {
                    Vector3 normal = new Vector3(px, py, -Mathf.Sqrt(0.72f - radius2)).normalized;
                    float diffuse = Mathf.Max(0f, Vector3.Dot(normal, light));
                    if (material.ShadingModel == SDFShadingModel.Cel)
                        diffuse = Mathf.Floor(diffuse * material.CelBands) / Mathf.Max(1, material.CelBands - 1);
                    float specular = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(normal, (light + Vector3.back).normalized)), material.SpecularPower);
                    color = material.ShadingModel == SDFShadingModel.Unlit
                        ? baseColor
                        : baseColor * (0.12f + diffuse * 0.88f) + material.SpecularColor * specular + material.Emission;
                    color.a = 1f;
                }
                pixels[y * width + x] = color;
            }
            result.SetPixels(pixels);
            result.Apply(false, true);
            return result;
        }
    }
}
