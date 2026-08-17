using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SdfRenderer.Editor
{
    public static class SDFSampleSceneBuilder
    {
        private const string Folder = "Assets/SDF/Samples";

        [InitializeOnLoadMethod]
        private static void ScheduleMissingSamples() => EditorApplication.delayCall += BuildMissingSamples;

        private static void BuildMissingSamples()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
                return;
            string[] names = { "SDFPrimitiveGallery", "SDFOperationsGallery", "SDFMaterialGallery", "SDFBenchmark" };
            foreach (string name in names)
            {
                if (!System.IO.File.Exists(Folder + "/" + name + ".unity"))
                {
                    BuildAllSamples();
                    return;
                }
            }
        }

        [MenuItem("Tools/SDF/Build Sample Scenes")]
        public static void BuildAllSamples()
        {
            Scene restoreScene = SceneManager.GetActiveScene();
            UnityEngine.Object restoreSelection = Selection.activeObject;
            BuildPrimitiveGallery();
            BuildOperationsGallery();
            BuildMaterialGallery();
            BuildBenchmark();
            AssetDatabase.SaveAssets();
            if (restoreScene.IsValid() && restoreScene.isLoaded)
                SceneManager.SetActiveScene(restoreScene);
            Selection.activeObject = restoreSelection;
            Debug.Log("Built SDFPrimitiveGallery, SDFOperationsGallery, SDFMaterialGallery, and SDFBenchmark scenes.");
        }

        private static void BuildPrimitiveGallery()
        {
            Scene scene = NewLitScene();
            int index = 0;
            int width = 6;
            foreach (SDFShapeType type in Enum.GetValues(typeof(SDFShapeType)))
            {
                SDFModel model = SDFCreateMenus.CreateModel(null).GetComponent<SDFModel>();
                SDFShape shape = SDFCreateMenus.CreateShape(type, model.gameObject);
                model.name = ObjectNames.NicifyVariableName(type.ToString());
                model.transform.position = new Vector3((index % width - 2.5f) * 3f, 0f, (index / width) * 3f);
                CreateLabel(model.transform, model.name);
                ++index;
            }
            FrameCamera(scene, new Vector3(0f, 14f, -20f), new Vector3(0f, 0f, 8f));
            SaveAndClose(scene, Folder + "/SDFPrimitiveGallery.unity");
        }

        private static void BuildOperationsGallery()
        {
            Scene scene = NewLitScene();
            int index = 0;
            foreach (SDFOperationType operation in Enum.GetValues(typeof(SDFOperationType)))
            {
                SDFModel model = SDFCreateMenus.CreateModel(null).GetComponent<SDFModel>();
                SDFShape first = SDFCreateMenus.CreateShape(SDFShapeType.Sphere, model.gameObject);
                model.name = ObjectNames.NicifyVariableName(operation.ToString());
                model.transform.position = new Vector3((index - 2.5f) * 3f, 0f, 0f);
                CreateLabel(model.transform, model.name);
                SDFShape second = SDFCreateMenus.CreateShape(SDFShapeType.Box, model.gameObject);
                second.transform.localPosition = Vector3.right * 0.45f;
                SerializedObject op = new SerializedObject(second.GetComponent<SDFOperation>());
                op.FindProperty("m_Type").enumValueIndex = (int)operation;
                op.FindProperty("m_Smoothness").floatValue = 0.35f;
                op.ApplyModifiedPropertiesWithoutUndo();
                ++index;
            }
            index = 0;
            foreach (SDFModifierType modifierType in Enum.GetValues(typeof(SDFModifierType)))
            {
                SDFModel model = SDFCreateMenus.CreateModel(null).GetComponent<SDFModel>();
                model.name = ObjectNames.NicifyVariableName(modifierType.ToString());
                model.transform.position = new Vector3((index % 6 - 2.5f) * 3f, 0f, 4f + (index / 6) * 4f);
                SDFShape shape = SDFCreateMenus.CreateShape(SDFShapeType.RoundBox, model.gameObject);
                SDFCreateMenus.AddModifier(shape.gameObject, modifierType);
                CreateLabel(model.transform, model.name);
                ++index;
            }
            FrameCamera(scene, new Vector3(0f, 5f, -15f), Vector3.zero);
            SaveAndClose(scene, Folder + "/SDFOperationsGallery.unity");
        }

        private static void BuildMaterialGallery()
        {
            Scene scene = NewLitScene();
            SDFMaterialAsset[] materials = new SDFMaterialAsset[4];
            foreach (SDFShadingModel modelType in new[] { SDFShadingModel.BlinnPhong, SDFShadingModel.Unlit, SDFShadingModel.Cel, SDFShadingModel.PbrLike })
            {
                string path = Folder + "/" + modelType + ".asset";
                SDFMaterialAsset material = AssetDatabase.LoadAssetAtPath<SDFMaterialAsset>(path);
                if (material == null)
                {
                    material = ScriptableObject.CreateInstance<SDFMaterialAsset>();
                    AssetDatabase.CreateAsset(material, path);
                    SerializedObject serialized = new SerializedObject(material);
                    serialized.FindProperty("m_ShadingModel").enumValueIndex = (int)modelType;
                    serialized.FindProperty("m_BaseColor").colorValue = Color.HSVToRGB((int)modelType * 0.18f, 0.65f, 0.9f);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                materials[(int)modelType] = material;
                SDFModel sdfModel = SDFCreateMenus.CreateModel(null).GetComponent<SDFModel>();
                SDFShape shape = SDFCreateMenus.CreateShape(SDFShapeType.Sphere, sdfModel.gameObject);
                sdfModel.transform.position = new Vector3(((int)modelType - 1.5f) * 3f, 0f, 0f);
                SerializedObject shapeData = new SerializedObject(shape);
                shapeData.FindProperty("m_Material").objectReferenceValue = material;
                shapeData.ApplyModifiedPropertiesWithoutUndo();
                CreateLabel(sdfModel.transform, ObjectNames.NicifyVariableName(modelType.ToString()));
            }
            SDFModel blendModel = SDFCreateMenus.CreateModel(null).GetComponent<SDFModel>();
            blendModel.name = "Cel to PBR Smooth Blend";
            blendModel.transform.position = new Vector3(0f, 0f, 3.5f);
            SDFShape blendA = SDFCreateMenus.CreateShape(SDFShapeType.Sphere, blendModel.gameObject);
            blendA.transform.localPosition = Vector3.left * 0.4f;
            AssignMaterial(blendA, materials[(int)SDFShadingModel.Cel]);
            SDFShape blendB = SDFCreateMenus.CreateShape(SDFShapeType.RoundBox, blendModel.gameObject);
            blendB.transform.localPosition = Vector3.right * 0.4f;
            AssignMaterial(blendB, materials[(int)SDFShadingModel.PbrLike]);
            SerializedObject blendOperation = new SerializedObject(blendB.GetComponent<SDFOperation>());
            blendOperation.FindProperty("m_Type").enumValueIndex = (int)SDFOperationType.SmoothUnion;
            blendOperation.FindProperty("m_Smoothness").floatValue = 0.5f;
            blendOperation.ApplyModifiedPropertiesWithoutUndo();
            CreateLabel(blendModel.transform, blendModel.name);
            string[] customShaderGuids = AssetDatabase.FindAssets("t:SDFShaderAsset", new[] { Folder + "/Shaders" });
            for (int customIndex = 0; customIndex < customShaderGuids.Length; ++customIndex)
            {
                SDFShaderAsset shader = AssetDatabase.LoadAssetAtPath<SDFShaderAsset>(AssetDatabase.GUIDToAssetPath(customShaderGuids[customIndex]));
                if (shader == null) continue;
                string materialPath = Folder + "/Custom_" + shader.name + ".asset";
                SDFMaterialAsset customMaterial = AssetDatabase.LoadAssetAtPath<SDFMaterialAsset>(materialPath);
                if (customMaterial == null)
                {
                    customMaterial = ScriptableObject.CreateInstance<SDFMaterialAsset>();
                    AssetDatabase.CreateAsset(customMaterial, materialPath);
                    SerializedObject materialData = new SerializedObject(customMaterial);
                    materialData.FindProperty("m_ShadingModel").enumValueIndex = (int)SDFShadingModel.Custom;
                    materialData.FindProperty("m_CustomShader").objectReferenceValue = shader;
                    materialData.FindProperty("m_Custom0").vector4Value = new Vector4(0.25f, 0.75f, 1f, 32f);
                    materialData.FindProperty("m_Custom1").vector4Value = new Vector4(1f, 0.65f, 0f, 0f);
                    materialData.ApplyModifiedPropertiesWithoutUndo();
                }
                SDFModel customModel = SDFCreateMenus.CreateModel(null).GetComponent<SDFModel>();
                customModel.name = shader.name;
                customModel.transform.position = new Vector3((customIndex - (customShaderGuids.Length - 1) * 0.5f) * 3f, 0f, 7f);
                SDFShape customShape = SDFCreateMenus.CreateShape(SDFShapeType.Torus, customModel.gameObject);
                AssignMaterial(customShape, customMaterial);
                CreateLabel(customModel.transform, customModel.name);
            }
            FrameCamera(scene, new Vector3(0f, 4f, -11f), Vector3.zero);
            SaveAndClose(scene, Folder + "/SDFMaterialGallery.unity");
        }

        private static void BuildBenchmark()
        {
            Scene scene = NewLitScene();
            GameObject controller = new GameObject("SDF Benchmark Controller");
            controller.AddComponent<SDFBenchmarkController>();
            FrameCamera(scene, new Vector3(0f, 35f, -35f), Vector3.zero);
            SaveAndClose(scene, Folder + "/SDFBenchmark.unity");
        }

        private static Scene NewLitScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.sun = light;
            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.Skybox;
            return scene;
        }

        private static void FrameCamera(Scene scene, Vector3 position, Vector3 target)
        {
            Camera camera = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                camera = root.GetComponentInChildren<Camera>();
                if (camera != null) break;
            }
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);
            camera.farClipPlane = 500f;
        }

        private static void SaveAndClose(Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void AssignMaterial(SDFShape shape, SDFMaterialAsset material)
        {
            SerializedObject data = new SerializedObject(shape);
            data.FindProperty("m_Material").objectReferenceValue = material;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateLabel(Transform parent, string text)
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 32;
            label.characterSize = 0.08f;
            label.color = Color.white;
        }
    }
}
