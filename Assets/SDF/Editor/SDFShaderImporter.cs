using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace SdfRenderer.Editor
{
    [ScriptedImporter(1, "sdfshader")]
    public sealed class SDFShaderImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            string source = File.ReadAllText(context.assetPath);
            SDFShaderAsset asset = ScriptableObject.CreateInstance<SDFShaderAsset>();
            string error = ValidateSource(source);
            asset.SetStableId(AssetDatabase.AssetPathToGUID(context.assetPath));
            asset.SetImportState(source, error);
            asset.name = Path.GetFileNameWithoutExtension(context.assetPath);
            context.AddObjectToAsset("SDFShader", asset);
            context.SetMainObject(asset);
            EditorApplication.delayCall += SDFShaderGenerator.Regenerate;
        }

        public static string ValidateSource(string source)
        {
            if (!source.Contains("HLSLPROGRAM", StringComparison.Ordinal) || !source.Contains("ENDHLSL", StringComparison.Ordinal))
                return "An SDF shader must contain HLSLPROGRAM and ENDHLSL markers.";
            if (!Regex.IsMatch(source, @"\bfloat3\s+SDFSurface\s*\("))
                return "The HLSL block must declare: float3 SDFSurface(SDFSurfaceContext context, SDFMaterialGpu material).";
            Match properties = Regex.Match(source, @"\bProperties\s*\{(?<body>[\s\S]*?)\}\s*HLSLPROGRAM", RegexOptions.IgnoreCase);
            if (!properties.Success)
                return "Add a Properties { ... } block before HLSLPROGRAM.";
            foreach (string rawLine in properties.Groups["body"].Value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (!line.StartsWith("_", StringComparison.Ordinal)) continue;
                bool supported = Regex.IsMatch(line, @"^_BaseMap\s*\([^,]+,\s*2D\s*\)", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(line, @"^_Custom[01][XYZW]?\s*\([^,]+,\s*(Color|Vector|Float|Toggle|Range\s*\(|Enum\s*\()", RegexOptions.IgnoreCase);
                if (!supported)
                    return "Unsupported property declaration: " + line + ". Use _BaseMap (2D) or _Custom0/_Custom1 and their XYZW components.";
            }
            return string.Empty;
        }
    }

    internal static class SDFShaderGenerator
    {
        private const string OutputPath = "Assets/SDF/Generated/SDFCustomMaterials.hlsl";
        private const string RegistryPath = "Assets/SDF/Generated/SDFCustomShaderRegistry.cs";
        private static bool s_Generating;

        [InitializeOnLoadMethod]
        private static void ScheduleOnDomainLoad() => EditorApplication.delayCall += Regenerate;

        internal static void Regenerate()
        {
            if (s_Generating || EditorApplication.isCompiling)
                return;
            s_Generating = true;
            try
            {
                List<SDFShaderAsset> assets = AssetDatabase.FindAssets("t:SDFShaderAsset")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<SDFShaderAsset>)
                    .Where(asset => asset != null && string.IsNullOrEmpty(asset.LastError))
                    .OrderBy(asset => asset.StableId, StringComparer.Ordinal)
                    .ToList();

                StringBuilder output = new StringBuilder(4096);
                output.AppendLine("#ifndef SDF_CUSTOM_MATERIALS_INCLUDED");
                output.AppendLine("#define SDF_CUSTOM_MATERIALS_INCLUDED");
                output.AppendLine("// Auto-generated from .sdfshader modules. Do not edit.");
                for (int i = 0; i < assets.Count; ++i)
                {
                    SDFShaderAsset asset = assets[i];
                    if (asset.GeneratedIndex != i)
                    {
                        asset.SetGeneratedIndex(i);
                        EditorUtility.SetDirty(asset);
                    }
                    string hlsl = ExtractHlsl(asset.Source);
                    string sourcePath = AssetDatabase.GetAssetPath(asset).Replace("\\", "/").Replace("\"", "\\\"");
                    output.AppendLine($"#line 1 \"{sourcePath}\"");
                    output.AppendLine(Regex.Replace(hlsl, @"\bSDFSurface\b", "SDFSurface_" + i));
                    output.AppendLine("#line 1 \"Assets/SDF/Generated/SDFCustomMaterials.hlsl\"");
                }
                output.AppendLine("float3 SDFShadeCustom(uint customId, SDFSurfaceContext context, SDFMaterialGpu material)");
                output.AppendLine("{");
                for (int i = 0; i < assets.Count; ++i)
                    output.AppendLine($"    if (customId == {i}u) return SDFSurface_{i}(context, material);");
                output.AppendLine("    return material.BaseColor.rgb;");
                output.AppendLine("}");
                output.AppendLine("#endif");

                string contents = output.ToString();
                string current = File.Exists(OutputPath) ? File.ReadAllText(OutputPath) : string.Empty;
                if (!string.Equals(current, contents, StringComparison.Ordinal))
                {
                    File.WriteAllText(OutputPath, contents, new UTF8Encoding(false));
                    AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
                }
                StringBuilder registry = new StringBuilder(1024);
                registry.AppendLine("namespace SdfRenderer.Generated");
                registry.AppendLine("{");
                registry.AppendLine("    // Auto-generated from .sdfshader assets. Do not edit.");
                registry.AppendLine("    public static class SDFCustomShaderRegistry");
                registry.AppendLine("    {");
                registry.AppendLine("        public static int Resolve(string stableId)");
                registry.AppendLine("        {");
                registry.AppendLine("            switch (stableId)");
                registry.AppendLine("            {");
                for (int i = 0; i < assets.Count; ++i)
                    registry.AppendLine($"                case \"{assets[i].StableId}\": return {i};");
                registry.AppendLine("                default: return -1;");
                registry.AppendLine("            }");
                registry.AppendLine("        }");
                registry.AppendLine("    }");
                registry.AppendLine("}");
                string registryContents = registry.ToString();
                string existingRegistry = File.Exists(RegistryPath) ? File.ReadAllText(RegistryPath) : string.Empty;
                if (!string.Equals(existingRegistry, registryContents, StringComparison.Ordinal))
                {
                    File.WriteAllText(RegistryPath, registryContents, new UTF8Encoding(false));
                    AssetDatabase.ImportAsset(RegistryPath, ImportAssetOptions.ForceUpdate);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                s_Generating = false;
            }
        }

        private static string ExtractHlsl(string source)
        {
            int start = source.IndexOf("HLSLPROGRAM", StringComparison.Ordinal);
            int end = source.LastIndexOf("ENDHLSL", StringComparison.Ordinal);
            if (start < 0 || end <= start)
                return string.Empty;
            start += "HLSLPROGRAM".Length;
            return source.Substring(start, end - start).Trim();
        }
    }

    public static class SDFShaderCreation
    {
        private const string Template =
            "SDFShader \"New SDF Shader\"\n" +
            "{\n" +
            "    Properties\n" +
            "    {\n" +
            "        _Custom0 (\"Tint\", Color) = (0.2, 0.8, 1.0, 1.0)\n" +
            "        _Custom1X (\"Strength\", Range(0, 2)) = 1\n" +
            "        _Custom1Y (\"Use Rim Light\", Toggle) = 0\n" +
            "        _BaseMap (\"Base Map\", 2D) = \"white\"\n" +
            "    }\n\n" +
            "    HLSLPROGRAM\n" +
            "    // Surface-only module: raymarched SDFs have no mesh vertex stage.\n" +
            "    // Context also supplies ambientOcclusion and selfShadow so custom lighting can follow renderer settings.\n" +
            "    float3 SDFSurface(SDFSurfaceContext context, SDFMaterialGpu material)\n" +
            "    {\n" +
            "        float3 tint = material.Custom0.rgb;\n" +
            "        float strength = material.Custom1.x;\n" +
            "        float ndotl = saturate(dot(context.normalWS, context.lightDirectionWS)) * context.selfShadow;\n" +
            "        float3 textureColor = SampleSDFTexture((uint)(material.CustomShaderTextureIndices.y + 0.5), context.positionOS.xz).rgb;\n" +
            "        return textureColor * tint * (context.ambientColor * context.ambientOcclusion + context.lightColor * ndotl) * strength;\n" +
            "    }\n" +
            "    ENDHLSL\n" +
            "}\n";

        [MenuItem("Assets/Create/SDF Shader", false, 301)]
        public static void Create() => ProjectWindowUtil.CreateAssetWithContent("New SDF Shader.sdfshader", Template);
    }

    [CustomEditor(typeof(SDFShaderAsset))]
    public sealed class SDFShaderAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            SDFShaderAsset asset = (SDFShaderAsset)target;
            if (!string.IsNullOrEmpty(asset.LastError))
                EditorGUILayout.HelpBox(asset.LastError, MessageType.Error);
            EditorGUILayout.LabelField("Generated Dispatch Index", asset.GeneratedIndex.ToString());
            if (GUILayout.Button("Open Source"))
                AssetDatabase.OpenAsset(asset);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextArea(asset.Source, GUILayout.MinHeight(260));
            if (GUILayout.Button("Regenerate SDF Shader Registry"))
                SDFShaderGenerator.Regenerate();
        }
    }
}
