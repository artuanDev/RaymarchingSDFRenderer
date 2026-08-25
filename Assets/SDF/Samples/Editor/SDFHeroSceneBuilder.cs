using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SdfRenderer.Editor
{
    public static class SDFHeroSceneBuilder
    {
        private const string RootFolder = "Assets/SDF/Samples/Hero";
        private const string ScenePath = "Assets/SDF/Samples/SDFHeroDemo.unity";

        [MenuItem("Tools/SDF/Build Hero Demo Scene")]
        public static void BuildHeroDemo()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EnsureFolder(RootFolder);
            HeroAssets assets = BuildAssets();
            Scene restoreScene = SceneManager.GetActiveScene();
            UnityEngine.Object restoreSelection = Selection.activeObject;
            bool canRestoreScene = restoreScene.IsValid() && restoreScene.isLoaded && !string.IsNullOrEmpty(restoreScene.path);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                canRestoreScene ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            ConfigureEnvironment(assets, out Camera camera, out Transform cameraTarget, out Light keyLight);
            BuildMeshStage(assets);
            BuildSdfSculpture(assets, out Transform sculpture, out Transform blendShape,
                out Transform cutterShape, out Transform orbitRoot, out SDFModifier twist);
            Transform faceSculpture = BuildStylizedFace(assets);
            Transform creatureSculpture = BuildCreature(assets);
            Transform totemSculpture = BuildGeometricTotem(assets);

            GameObject directorObject = new GameObject("Hero Demo Director");
            SDFHeroDemoController director = directorObject.AddComponent<SDFHeroDemoController>();
            SerializedObject directorData = new SerializedObject(director);
            directorData.FindProperty("m_Sculpture").objectReferenceValue = sculpture;
            directorData.FindProperty("m_BlendShape").objectReferenceValue = blendShape;
            directorData.FindProperty("m_CutterShape").objectReferenceValue = cutterShape;
            directorData.FindProperty("m_OrbitRoot").objectReferenceValue = orbitRoot;
            directorData.FindProperty("m_FaceSculpture").objectReferenceValue = faceSculpture;
            directorData.FindProperty("m_CreatureSculpture").objectReferenceValue = creatureSculpture;
            directorData.FindProperty("m_TotemSculpture").objectReferenceValue = totemSculpture;
            directorData.FindProperty("m_Twist").objectReferenceValue = twist;
            directorData.FindProperty("m_Camera").objectReferenceValue = camera;
            directorData.FindProperty("m_CameraTarget").objectReferenceValue = cameraTarget;
            directorData.FindProperty("m_LoopDuration").floatValue = 12f;
            directorData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (canRestoreScene)
                EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (canRestoreScene && restoreScene.IsValid() && restoreScene.isLoaded)
                SceneManager.SetActiveScene(restoreScene);
            Selection.activeObject = restoreSelection;
            Debug.Log($"Built capture-ready SDF hero demo at {ScenePath}. Enter Play Mode for its seamless 12-second animation loop.");
        }

        [MenuItem("Tools/SDF/Capture Hero Preview")]
        public static void CaptureHeroPreview()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!File.Exists(Path.GetFullPath(ScenePath)))
                BuildHeroDemo();
            Scene restoreScene = SceneManager.GetActiveScene();
            UnityEngine.Object restoreSelection = Selection.activeObject;
            bool canRestoreScene = restoreScene.IsValid() && restoreScene.isLoaded && !string.IsNullOrEmpty(restoreScene.path);
            Scene heroScene = EditorSceneManager.OpenScene(ScenePath,
                canRestoreScene ? OpenSceneMode.Additive : OpenSceneMode.Single);
            SceneManager.SetActiveScene(heroScene);
            Camera camera = FindInScene<Camera>(heroScene, "Hero Camera");
            if (camera == null)
                throw new InvalidOperationException("The hero scene does not contain its Hero Camera.");

            const int width = 1920;
            const int height = 1080;
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default) { name = "SDF Hero Preview" };
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                result.Apply(false, false);
                string previewPath = RootFolder + "/HeroPreview.png";
                File.WriteAllBytes(Path.GetFullPath(previewPath), result.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(result);
                AssetDatabase.ImportAsset(previewPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Debug.Log($"Captured SDF hero preview at {previewPath}.");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                if (canRestoreScene)
                {
                    EditorSceneManager.CloseScene(heroScene, true);
                    if (restoreScene.IsValid() && restoreScene.isLoaded)
                        SceneManager.SetActiveScene(restoreScene);
                    Selection.activeObject = restoreSelection;
                }
            }
        }

        private static T FindInScene<T>(Scene scene, string objectName) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T[] components = root.GetComponentsInChildren<T>(true);
                foreach (T component in components)
                {
                    if (component.gameObject.name == objectName)
                        return component;
                }
            }
            return null;
        }

        private static HeroAssets BuildAssets()
        {
            const int textureSize = 512;
            Texture2D baseMap = WriteTexture(RootFolder + "/HeroSurface_Base.png", textureSize, false, false,
                (x, y) =>
                {
                    float u = (x + 0.5f) / textureSize;
                    float v = (y + 0.5f) / textureSize;
                    float ribbons = 0.5f + 0.5f * Mathf.Sin((u * 7f + v * 3f) * Mathf.PI * 2f + Mathf.Sin(v * Mathf.PI * 4f));
                    float fine = 0.5f + 0.5f * Mathf.Sin((u - v) * Mathf.PI * 48f);
                    float seam = Mathf.SmoothStep(0.82f, 0.98f, ribbons) * (0.65f + fine * 0.35f);
                    Color teal = Color.Lerp(new Color(0.015f, 0.055f, 0.07f), new Color(0.04f, 0.34f, 0.4f), ribbons);
                    return Color.Lerp(teal, new Color(0.88f, 0.47f, 0.12f), seam);
                });
            Texture2D normalMap = WriteTexture(RootFolder + "/HeroSurface_Normal.png", textureSize, true, true,
                (x, y) =>
                {
                    float Height(int px, int py)
                    {
                        float u = (px + 0.5f) / textureSize;
                        float v = (py + 0.5f) / textureSize;
                        return Mathf.Sin((u * 7f + v * 3f) * Mathf.PI * 2f + Mathf.Sin(v * Mathf.PI * 4f)) * 0.7f +
                               Mathf.Sin((u - v) * Mathf.PI * 48f) * 0.08f;
                    }
                    float dx = Height(x - 1, y) - Height(x + 1, y);
                    float dy = Height(x, y - 1) - Height(x, y + 1);
                    Vector3 normal = new Vector3(dx * 1.8f, dy * 1.8f, 1f).normalized;
                    return new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
                });
            Texture2D metallicMap = WriteTexture(RootFolder + "/HeroSurface_Metallic.png", textureSize, false, true,
                (x, y) =>
                {
                    float u = (x + 0.5f) / textureSize;
                    float v = (y + 0.5f) / textureSize;
                    float ribbon = 0.5f + 0.5f * Mathf.Sin((u * 7f + v * 3f) * Mathf.PI * 2f + Mathf.Sin(v * Mathf.PI * 4f));
                    float value = Mathf.Lerp(0.28f, 0.98f, Mathf.SmoothStep(0.25f, 0.9f, ribbon));
                    return new Color(value, value, value, 1f);
                });
            Texture2D roughnessMap = WriteTexture(RootFolder + "/HeroSurface_Roughness.png", textureSize, false, true,
                (x, y) =>
                {
                    float u = (x + 0.5f) / textureSize;
                    float v = (y + 0.5f) / textureSize;
                    float brushed = 0.5f + 0.5f * Mathf.Sin((u * 63f + Mathf.Sin(v * 11f) * 0.2f) * Mathf.PI * 2f);
                    float broad = 0.5f + 0.5f * Mathf.Sin((u * 7f + v * 3f) * Mathf.PI * 2f);
                    float value = Mathf.Lerp(0.12f, 0.58f, broad) + brushed * 0.08f;
                    return new Color(value, value, value, 1f);
                });

            SDFMaterialAsset mapped = CreateSdfMaterial("Hero Mapped Metal", Color.white, baseMap, normalMap,
                metallicMap, roughnessMap, 0.75f, 0.95f, new Color(0.0f, 0.018f, 0.025f));
            SDFMaterialAsset pearl = CreateSdfMaterial("Hero Pearl", new Color(0.15f, 0.66f, 0.78f), null, null,
                null, null, 0.18f, 0.9f, new Color(0.0f, 0.025f, 0.035f));
            SDFMaterialAsset ember = CreateSdfMaterial("Hero Ember", new Color(0.13f, 0.018f, 0.006f), null, null,
                null, null, 0.55f, 0.72f, new Color(1.3f, 0.12f, 0.018f));
            SDFMaterialAsset satellite = CreateSdfMaterial("Hero Satellite", new Color(0.09f, 0.12f, 0.16f), null, null,
                null, null, 0.8f, 0.82f, new Color(0.02f, 0.28f, 0.48f));
            SDFMaterialAsset porcelain = CreateSdfMaterial("Portrait Porcelain", new Color(0.82f, 0.56f, 0.47f), null, null,
                null, null, 0.03f, 0.66f, new Color(0.012f, 0.004f, 0.002f));
            SDFMaterialAsset portraitInk = CreateSdfMaterial("Portrait Ink", new Color(0.025f, 0.018f, 0.03f), null, null,
                null, null, 0.05f, 0.34f, new Color(0.002f, 0f, 0.004f));
            SDFMaterialAsset creatureCel = CreateSdfMaterial("Creature Cel", new Color(0.16f, 0.78f, 0.58f), null, null,
                null, null, 0f, 0.28f, new Color(0.004f, 0.018f, 0.012f), SDFShadingModel.Cel, 3);
            SDFMaterialAsset creatureAccent = CreateSdfMaterial("Creature Accent", new Color(0.96f, 0.38f, 0.24f), null, null,
                null, null, 0f, 0.42f, new Color(0.025f, 0.002f, 0f), SDFShadingModel.Cel, 2);
            SDFMaterialAsset creatureEye = CreateSdfMaterial("Creature Eyes", new Color(0.03f, 0.035f, 0.04f), null, null,
                null, null, 0f, 0.7f, new Color(0.2f, 1.25f, 0.82f), SDFShadingModel.Unlit);
            SDFMaterialAsset obsidian = CreateSdfMaterial("Totem Obsidian", new Color(0.025f, 0.035f, 0.052f), null, null,
                null, null, 0.92f, 0.88f, new Color(0.002f, 0.012f, 0.025f));

            return new HeroAssets
            {
                Mapped = mapped,
                Pearl = pearl,
                Ember = ember,
                Satellite = satellite,
                Porcelain = porcelain,
                PortraitInk = portraitInk,
                CreatureCel = creatureCel,
                CreatureAccent = creatureAccent,
                CreatureEye = creatureEye,
                Obsidian = obsidian,
                Ground = CreateUrpMaterial("Hero Ground", new Color(0.012f, 0.017f, 0.024f), 0.2f, 0.22f),
                Plinth = CreateUrpMaterial("Hero Plinth", new Color(0.055f, 0.07f, 0.085f), 0.82f, 0.72f),
                Backdrop = CreateUrpMaterial("Hero Backdrop", new Color(0.018f, 0.027f, 0.04f), 0.35f, 0.42f),
                VolumeProfile = CreateVolumeProfile()
            };
        }

        private static void ConfigureEnvironment(HeroAssets assets, out Camera camera, out Transform cameraTarget, out Light keyLight)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.09f, 0.14f, 0.2f);
            RenderSettings.ambientEquatorColor = new Color(0.025f, 0.045f, 0.07f);
            RenderSettings.ambientGroundColor = new Color(0.008f, 0.01f, 0.016f);
            RenderSettings.reflectionIntensity = 0.75f;

            GameObject cameraObject = new GameObject("Hero Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.006f, 0.009f, 0.016f);
            camera.fieldOfView = 43f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.allowHDR = true;
            cameraObject.transform.position = new Vector3(0f, 4.35f, -15.2f);
            UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;

            GameObject targetObject = new GameObject("Camera Target");
            targetObject.transform.position = new Vector3(0f, 1.4f, 1.05f);
            cameraTarget = targetObject.transform;
            cameraObject.transform.rotation = Quaternion.LookRotation(cameraTarget.position - cameraObject.transform.position, Vector3.up);

            GameObject keyObject = new GameObject("Key Sun");
            keyLight = keyObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.76f, 0.57f);
            keyLight.intensity = 2.2f;
            keyLight.shadows = LightShadows.Soft;
            keyObject.transform.rotation = Quaternion.Euler(46f, -38f, 0f);
            RenderSettings.sun = keyLight;

            CreatePointLight("Cyan Rim", new Vector3(-4.5f, 4.2f, -1.2f), new Color(0.05f, 0.55f, 1f), 8f, 11f);
            CreatePointLight("Amber Rim", new Vector3(4.2f, 2.8f, 2.4f), new Color(1f, 0.18f, 0.035f), 6f, 9f);
            CreatePointLight("Portrait Fill", new Vector3(-5.2f, 2.7f, -2f), new Color(1f, 0.58f, 0.42f), 4.5f, 7f);

            GameObject volumeObject = new GameObject("Hero Post Processing");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = assets.VolumeProfile;
        }

        private static void BuildMeshStage(HeroAssets assets)
        {
            CreatePrimitive("Backdrop Wall", PrimitiveType.Cube, new Vector3(0f, 4.25f, 6.35f),
                new Vector3(100f, 14f, 0.35f), assets.Backdrop);
            CreatePrimitive("Shadow Floor", PrimitiveType.Cube, new Vector3(0f, -0.82f, 1f),
                new Vector3(60f, 0.35f, 60f), assets.Ground);
            CreatePrimitive("Lower Plinth", PrimitiveType.Cylinder, new Vector3(0f, -0.38f, 0f),
                new Vector3(3.2f, 0.32f, 3.2f), assets.Plinth);
            CreatePrimitive("Upper Plinth", PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0f),
                new Vector3(2.45f, 0.12f, 2.45f), assets.Backdrop);
            CreateSculpturePlinth("Portrait", new Vector3(-5.25f, 0f, 1.05f), 1.35f, assets);
            CreateSculpturePlinth("Creature", new Vector3(3.95f, 0f, 1.05f), 1.45f, assets);
            CreateSculpturePlinth("Totem", new Vector3(-2.6f, 0f, 1.9f), 1.25f, assets);

            GameObject backdropRoot = new GameObject("Conventional Geometry Backdrop");
            for (int index = -4; index <= 4; ++index)
            {
                float height = 2.2f + (4 - Mathf.Abs(index)) * 0.42f;
                GameObject fin = CreatePrimitive($"Backdrop Fin {index + 5:00}", PrimitiveType.Cube,
                    new Vector3(index * 1.55f, height * 0.5f - 0.65f, 5.4f + Mathf.Abs(index) * 0.15f),
                    new Vector3(0.16f, height, 0.75f), assets.Backdrop);
                fin.transform.SetParent(backdropRoot.transform, true);
            }
        }

        private static void BuildSdfSculpture(HeroAssets assets, out Transform sculpture, out Transform blendShape,
            out Transform cutterShape, out Transform orbitRoot, out SDFModifier twist)
        {
            GameObject sculptureObject = new GameObject("Hero SDF Sculpture");
            sculptureObject.transform.position = new Vector3(0f, 1.65f, 0f);
            sculptureObject.AddComponent<SDFModel>();
            sculpture = sculptureObject.transform;

            SDFShape ribbon = CreateShape(sculptureObject.transform, "Mapped Twisted Ribbon", SDFShapeType.RoundBox, assets.Mapped);
            ribbon.Size = new Vector3(1.65f, 0.34f, 0.62f);
            ribbon.Roundness = 0.24f;
            ribbon.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            twist = ribbon.gameObject.AddComponent<SDFModifier>();
            twist.Type = SDFModifierType.Twist;
            twist.Amount = 0.52f;

            SDFShape ring = CreateShape(sculptureObject.transform, "Mapped Portal Ring", SDFShapeType.Torus, assets.Mapped);
            SetShapeFields(ring, radiusA: 1.58f, radiusB: 0.19f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.23f);

            SDFShape pearl = CreateShape(sculptureObject.transform, "Pearl Blend", SDFShapeType.Sphere, assets.Pearl);
            pearl.Radius = 0.92f;
            pearl.transform.localPosition = new Vector3(0.72f, 0.05f, 0.02f);
            pearl.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.58f);
            blendShape = pearl.transform;

            SDFShape cutter = CreateShape(sculptureObject.transform, "Emissive Cut Surface", SDFShapeType.Box, assets.Ember);
            cutter.Size = new Vector3(0.46f, 1.25f, 0.46f);
            cutter.transform.localPosition = new Vector3(-0.28f, 0.02f, -0.05f);
            cutter.transform.localRotation = Quaternion.Euler(18f, 22f, 36f);
            cutter.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothSubtraction, 0.24f);
            cutterShape = cutter.transform;

            GameObject orbitObject = new GameObject("Orbiting SDF Models");
            orbitObject.transform.position = sculptureObject.transform.position;
            orbitRoot = orbitObject.transform;
            for (int index = 0; index < 3; ++index)
            {
                float angle = index * Mathf.PI * 2f / 3f;
                GameObject satelliteModel = new GameObject($"Satellite {index + 1}");
                satelliteModel.transform.SetParent(orbitRoot, false);
                satelliteModel.transform.localPosition = new Vector3(Mathf.Cos(angle) * 2.72f,
                    0.25f + Mathf.Sin(angle * 2f) * 0.42f, Mathf.Sin(angle) * 2.72f);
                satelliteModel.AddComponent<SDFModel>();
                SDFShape satelliteShape = CreateShape(satelliteModel.transform, "Luminous Orb", SDFShapeType.Sphere,
                    index == 1 ? assets.Ember : assets.Satellite);
                satelliteShape.Radius = index == 1 ? 0.2f : 0.26f;
                SDFModifier onion = satelliteShape.gameObject.AddComponent<SDFModifier>();
                onion.Type = SDFModifierType.Onion;
                onion.Amount = 0.055f;
            }
        }

        private static Transform BuildStylizedFace(HeroAssets assets)
        {
            GameObject modelObject = new GameObject("Porcelain Portrait SDF");
            modelObject.transform.position = new Vector3(-5.25f, 1.18f, 1.05f);
            modelObject.transform.rotation = Quaternion.Euler(0f, -3f, 0f);
            modelObject.AddComponent<SDFModel>();

            SDFShape head = CreateShape(modelObject.transform, "Porcelain Head", SDFShapeType.EllipsoidBound, assets.Porcelain);
            head.Size = new Vector3(0.72f, 0.92f, 0.58f);

            SDFShape leftCheek = CreateShape(modelObject.transform, "Left Cheek", SDFShapeType.EllipsoidBound, assets.Porcelain);
            leftCheek.Size = new Vector3(0.48f, 0.46f, 0.46f);
            leftCheek.transform.localPosition = new Vector3(-0.29f, -0.12f, -0.24f);
            leftCheek.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.24f);

            SDFShape rightCheek = CreateShape(modelObject.transform, "Right Cheek", SDFShapeType.EllipsoidBound, assets.Porcelain);
            rightCheek.Size = new Vector3(0.48f, 0.46f, 0.46f);
            rightCheek.transform.localPosition = new Vector3(0.29f, -0.12f, -0.24f);
            rightCheek.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.24f);

            SDFShape jaw = CreateShape(modelObject.transform, "Soft Jaw", SDFShapeType.EllipsoidBound, assets.Porcelain);
            jaw.Size = new Vector3(0.51f, 0.43f, 0.43f);
            jaw.transform.localPosition = new Vector3(0f, -0.56f, -0.08f);
            jaw.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.28f);

            SDFShape neck = CreateShape(modelObject.transform, "Portrait Neck", SDFShapeType.CappedCylinder, assets.Porcelain);
            neck.Radius = 0.31f;
            neck.Height = 0.58f;
            neck.transform.localPosition = new Vector3(0f, -1.02f, 0.08f);
            neck.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.2f);

            AddFaceEar(modelObject.transform, "Left Ear", -0.73f, assets.Porcelain);
            AddFaceEar(modelObject.transform, "Right Ear", 0.73f, assets.Porcelain);

            SDFShape nose = CreateShape(modelObject.transform, "Sculpted Nose", SDFShapeType.RoundCone, assets.Porcelain);
            SetSegmentFields(nose, new Vector3(0f, 0.09f, -0.38f), new Vector3(0f, 0.02f, -0.91f), 0.22f, 0.075f);
            nose.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.13f);

            SDFShape hairCap = CreateShape(modelObject.transform, "Graphic Hair Cap", SDFShapeType.EllipsoidBound, assets.PortraitInk);
            hairCap.Size = new Vector3(0.72f, 0.31f, 0.56f);
            hairCap.transform.localPosition = new Vector3(0f, 0.76f, 0.08f);
            hairCap.transform.localRotation = Quaternion.Euler(0f, 0f, -5f);
            hairCap.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.14f);

            AddFaceCut(modelObject.transform, "Left Eye Socket", new Vector3(-0.29f, 0.22f, -0.55f), -5f, assets.PortraitInk);
            AddFaceCut(modelObject.transform, "Right Eye Socket", new Vector3(0.29f, 0.22f, -0.55f), 5f, assets.PortraitInk);

            SDFShape mouth = CreateShape(modelObject.transform, "Carved Mouth", SDFShapeType.Capsule, assets.PortraitInk);
            SetSegmentFields(mouth, new Vector3(-0.24f, -0.39f, -0.57f), new Vector3(0.24f, -0.39f, -0.57f), 0.052f, 0.052f);
            mouth.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothSubtraction, 0.055f);

            return modelObject.transform;
        }

        private static void AddFaceEar(Transform parent, string name, float x, SDFMaterialAsset material)
        {
            SDFShape ear = CreateShape(parent, name, SDFShapeType.EllipsoidBound, material);
            ear.Size = new Vector3(0.19f, 0.28f, 0.16f);
            ear.transform.localPosition = new Vector3(x, -0.02f, 0.01f);
            ear.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.12f);
        }

        private static void AddFaceCut(Transform parent, string name, Vector3 position, float roll, SDFMaterialAsset material)
        {
            SDFShape socket = CreateShape(parent, name, SDFShapeType.RoundBox, material);
            socket.Size = new Vector3(0.2f, 0.095f, 0.2f);
            socket.Roundness = 0.095f;
            socket.transform.localPosition = position;
            socket.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            socket.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothSubtraction, 0.07f);
        }

        private static Transform BuildCreature(HeroAssets assets)
        {
            GameObject modelObject = new GameObject("Cel Mooncalf SDF");
            modelObject.transform.position = new Vector3(3.95f, 0.92f, 1.05f);
            modelObject.transform.rotation = Quaternion.Euler(0f, 3f, 0f);
            modelObject.AddComponent<SDFModel>();

            SDFShape body = CreateShape(modelObject.transform, "Cel Body", SDFShapeType.EllipsoidBound, assets.CreatureCel);
            body.Size = new Vector3(0.93f, 0.52f, 0.68f);

            SDFShape head = CreateShape(modelObject.transform, "Big Round Head", SDFShapeType.Sphere, assets.CreatureCel);
            head.Radius = 0.63f;
            head.transform.localPosition = new Vector3(0f, 0.28f, -0.62f);
            head.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.33f);

            SDFShape muzzle = CreateShape(modelObject.transform, "Coral Muzzle", SDFShapeType.EllipsoidBound, assets.CreatureAccent);
            muzzle.Size = new Vector3(0.4f, 0.23f, 0.3f);
            muzzle.transform.localPosition = new Vector3(0f, 0.08f, -1.02f);
            muzzle.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.16f);

            AddCreatureLeg(modelObject.transform, "Front Left Leg", new Vector3(-0.55f, -0.48f, -0.28f), assets.CreatureCel);
            AddCreatureLeg(modelObject.transform, "Front Right Leg", new Vector3(0.55f, -0.48f, -0.28f), assets.CreatureCel);
            AddCreatureLeg(modelObject.transform, "Back Left Leg", new Vector3(-0.55f, -0.48f, 0.3f), assets.CreatureCel);
            AddCreatureLeg(modelObject.transform, "Back Right Leg", new Vector3(0.55f, -0.48f, 0.3f), assets.CreatureCel);

            SDFShape tail = CreateShape(modelObject.transform, "Tapered Tail", SDFShapeType.RoundCone, assets.CreatureCel);
            SetSegmentFields(tail, new Vector3(0f, 0.08f, 0.38f), new Vector3(0f, 0.25f, 1.48f), 0.32f, 0.055f);
            tail.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.2f);

            AddCreatureAntenna(modelObject.transform, "Left Antenna", -1f, assets.CreatureAccent);
            AddCreatureAntenna(modelObject.transform, "Right Antenna", 1f, assets.CreatureAccent);
            AddCreatureEye(modelObject.transform, "Left Eye", -0.25f, assets);
            AddCreatureEye(modelObject.transform, "Right Eye", 0.25f, assets);

            SDFShape mouth = CreateShape(modelObject.transform, "Tiny Smile", SDFShapeType.Capsule, assets.PortraitInk);
            SetSegmentFields(mouth, new Vector3(-0.15f, -0.06f, -1.28f), new Vector3(0.15f, -0.06f, -1.28f), 0.035f, 0.035f);
            mouth.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothSubtraction, 0.04f);

            return modelObject.transform;
        }

        private static void AddCreatureLeg(Transform parent, string name, Vector3 position, SDFMaterialAsset material)
        {
            SDFShape leg = CreateShape(parent, name, SDFShapeType.VerticalCapsule, material);
            leg.Radius = 0.15f;
            leg.Height = 0.4f;
            leg.transform.localPosition = position;
            leg.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.14f);
        }

        private static void AddCreatureAntenna(Transform parent, string name, float side, SDFMaterialAsset material)
        {
            SDFShape antenna = CreateShape(parent, name, SDFShapeType.Capsule, material);
            SetSegmentFields(antenna, new Vector3(side * 0.3f, 0.65f, -0.61f),
                new Vector3(side * 0.58f, 1.03f, -0.55f), 0.07f, 0.07f);
            antenna.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.11f);

            SDFShape tip = CreateShape(parent, name + " Tip", SDFShapeType.Sphere, material);
            tip.Radius = 0.12f;
            tip.transform.localPosition = new Vector3(side * 0.58f, 1.03f, -0.55f);
            tip.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.09f);
        }

        private static void AddCreatureEye(Transform parent, string name, float x, HeroAssets assets)
        {
            SDFShape eye = CreateShape(parent, name, SDFShapeType.Sphere, assets.CreatureEye);
            eye.Radius = 0.135f;
            eye.transform.localPosition = new Vector3(x, 0.43f, -1.11f);
            eye.GetComponent<SDFOperation>().SetParameters(SDFOperationType.Union, 0f);

            SDFShape pupil = CreateShape(parent, name + " Pupil", SDFShapeType.Sphere, assets.PortraitInk);
            pupil.Radius = 0.052f;
            pupil.transform.localPosition = new Vector3(x, 0.43f, -1.22f);
            pupil.GetComponent<SDFOperation>().SetParameters(SDFOperationType.Union, 0f);
        }

        private static Transform BuildGeometricTotem(HeroAssets assets)
        {
            GameObject modelObject = new GameObject("Obsidian Signal Totem SDF");
            modelObject.transform.position = new Vector3(-2.6f, 1.57f, 1.9f);
            modelObject.AddComponent<SDFModel>();

            SDFShape frame = CreateShape(modelObject.transform, "Mapped Monolith Frame", SDFShapeType.BoxFrame, assets.Mapped);
            frame.Size = new Vector3(0.72f, 1.42f, 0.72f);
            SetThickness(frame, 0.12f);

            SDFShape diamond = CreateShape(modelObject.transform, "Obsidian Diamond", SDFShapeType.Octahedron, assets.Obsidian);
            diamond.Radius = 0.93f;
            diamond.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothUnion, 0.12f);

            SDFShape cavity = CreateShape(modelObject.transform, "Emissive Spherical Cavity", SDFShapeType.Sphere, assets.Ember);
            cavity.Radius = 0.5f;
            cavity.GetComponent<SDFOperation>().SetParameters(SDFOperationType.SmoothSubtraction, 0.09f);

            AddTotemRing(modelObject.transform, "Upper Signal Ring", 0.84f, assets.Ember);
            AddTotemRing(modelObject.transform, "Lower Signal Ring", -0.84f, assets.Ember);

            SDFShape core = CreateShape(modelObject.transform, "Floating Ember Core", SDFShapeType.Sphere, assets.Ember);
            core.Radius = 0.31f;
            core.GetComponent<SDFOperation>().SetParameters(SDFOperationType.Union, 0f);

            SDFShape crown = CreateShape(modelObject.transform, "Pearl Crown", SDFShapeType.Octahedron, assets.Pearl);
            crown.Radius = 0.39f;
            crown.transform.localPosition = new Vector3(0f, 1.82f, 0f);
            crown.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            crown.GetComponent<SDFOperation>().SetParameters(SDFOperationType.Union, 0f);

            return modelObject.transform;
        }

        private static void AddTotemRing(Transform parent, string name, float y, SDFMaterialAsset material)
        {
            SDFShape ring = CreateShape(parent, name, SDFShapeType.Torus, material);
            SetShapeFields(ring, radiusA: 0.79f, radiusB: 0.075f);
            ring.transform.localPosition = new Vector3(0f, y, 0f);
            ring.GetComponent<SDFOperation>().SetParameters(SDFOperationType.Union, 0f);
        }

        private static SDFShape CreateShape(Transform parent, string name, SDFShapeType type, SDFMaterialAsset material)
        {
            GameObject shapeObject = new GameObject(name);
            shapeObject.transform.SetParent(parent, false);
            SDFShape shape = shapeObject.AddComponent<SDFShape>();
            shape.ShapeType = type;
            shape.Material = material;
            shapeObject.AddComponent<SDFOperation>();
            return shape;
        }

        private static void SetShapeFields(SDFShape shape, float radiusA, float radiusB)
        {
            SerializedObject serialized = new SerializedObject(shape);
            serialized.FindProperty("m_RadiusA").floatValue = radiusA;
            serialized.FindProperty("m_RadiusB").floatValue = radiusB;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSegmentFields(SDFShape shape, Vector3 pointA, Vector3 pointB, float radiusA, float radiusB)
        {
            SerializedObject serialized = new SerializedObject(shape);
            serialized.FindProperty("m_PointA").vector3Value = pointA;
            serialized.FindProperty("m_PointB").vector3Value = pointB;
            serialized.FindProperty("m_Radius").floatValue = radiusA;
            serialized.FindProperty("m_RadiusA").floatValue = radiusA;
            serialized.FindProperty("m_RadiusB").floatValue = radiusB;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetThickness(SDFShape shape, float thickness)
        {
            SerializedObject serialized = new SerializedObject(shape);
            serialized.FindProperty("m_Thickness").floatValue = thickness;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateSculpturePlinth(string name, Vector3 position, float radius, HeroAssets assets)
        {
            CreatePrimitive(name + " Lower Plinth", PrimitiveType.Cylinder, position + new Vector3(0f, -0.43f, 0f),
                new Vector3(radius, 0.27f, radius), assets.Plinth);
            CreatePrimitive(name + " Upper Plinth", PrimitiveType.Cylinder, position + new Vector3(0f, -0.11f, 0f),
                new Vector3(radius * 0.82f, 0.08f, radius * 0.82f), assets.Backdrop);
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject result = GameObject.CreatePrimitive(type);
            result.name = name;
            result.transform.position = position;
            result.transform.localScale = scale;
            Collider collider = result.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            Renderer renderer = result.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return result;
        }

        private static void CreatePointLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.position = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static SDFMaterialAsset CreateSdfMaterial(string name, Color baseColor, Texture2D baseMap, Texture2D normalMap,
            Texture2D metallicMap, Texture2D roughnessMap, float metallic, float smoothness, Color emission,
            SDFShadingModel shadingModel = SDFShadingModel.PbrLike, int celBands = 3)
        {
            string path = RootFolder + "/" + name.Replace(" ", string.Empty) + ".asset";
            SDFMaterialAsset material = AssetDatabase.LoadAssetAtPath<SDFMaterialAsset>(path);
            if (material == null)
            {
                material = ScriptableObject.CreateInstance<SDFMaterialAsset>();
                AssetDatabase.CreateAsset(material, path);
            }
            SerializedObject serialized = new SerializedObject(material);
            serialized.FindProperty("m_ShadingModel").enumValueIndex = (int)shadingModel;
            serialized.FindProperty("m_BaseColor").colorValue = baseColor;
            serialized.FindProperty("m_BaseMap").objectReferenceValue = baseMap;
            serialized.FindProperty("m_NormalMap").objectReferenceValue = normalMap;
            serialized.FindProperty("m_NormalScale").floatValue = 0.58f;
            serialized.FindProperty("m_Metallic").floatValue = metallic;
            serialized.FindProperty("m_MetallicMap").objectReferenceValue = metallicMap;
            serialized.FindProperty("m_Smoothness").floatValue = smoothness;
            serialized.FindProperty("m_RoughnessMap").objectReferenceValue = roughnessMap;
            serialized.FindProperty("m_Occlusion").floatValue = 1f;
            serialized.FindProperty("m_Emission").colorValue = emission;
            serialized.FindProperty("m_CelBands").intValue = celBands;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateUrpMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = RootFolder + "/" + name.Replace(" ", string.Empty) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader was not found.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static VolumeProfile CreateVolumeProfile()
        {
            string path = RootFolder + "/HeroVolumeProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.intensity.Override(0.32f);
            bloom.threshold.Override(0.85f);
            bloom.scatter.Override(0.68f);
            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);
            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.contrast.Override(12f);
            color.saturation.Override(-4f);
            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.38f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            return profile.Add<T>(true);
        }

        private static Texture2D WriteTexture(string path, int size, bool normalMap, bool linear,
            Func<int, int, Color> pixel)
        {
            Texture2D source = new Texture2D(size, size, TextureFormat.RGBA32, false, linear);
            Color[] colors = new Color[size * size];
            for (int y = 0; y < size; ++y)
            for (int x = 0; x < size; ++x)
                colors[y * size + x] = pixel(x, y);
            source.SetPixels(colors);
            source.Apply(false, false);
            File.WriteAllBytes(Path.GetFullPath(path), source.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap && !linear;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = size;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; ++index)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private sealed class HeroAssets
        {
            public SDFMaterialAsset Mapped;
            public SDFMaterialAsset Pearl;
            public SDFMaterialAsset Ember;
            public SDFMaterialAsset Satellite;
            public SDFMaterialAsset Porcelain;
            public SDFMaterialAsset PortraitInk;
            public SDFMaterialAsset CreatureCel;
            public SDFMaterialAsset CreatureAccent;
            public SDFMaterialAsset CreatureEye;
            public SDFMaterialAsset Obsidian;
            public Material Ground;
            public Material Plinth;
            public Material Backdrop;
            public VolumeProfile VolumeProfile;
        }
    }
}
