using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using SdfRenderer.Generated;

namespace SdfRenderer.Editor
{
    internal static class SDFRaymarchPreview
    {
        [StructLayout(LayoutKind.Sequential)] private struct ModelGpu { public Vector4 MinStart, MaxCount; }
        [StructLayout(LayoutKind.Sequential)] private struct ShapeGpu
        {
            public Vector4 W0, W1, W2, P0, P1, P2, P3, TypeOperation, MaterialMeta, Min, Max;
        }
        [StructLayout(LayoutKind.Sequential)] private struct ModifierGpu { public Vector4 A, B, C; }
        [StructLayout(LayoutKind.Sequential)] private struct MaterialGpu
        {
            public Vector4 BaseColor, SpecularPower, EmissionBands, ModelMetalSmooth, Custom0, Custom1, CustomTexture, PbrTextures;
        }

        internal static Texture2D Render(SDFMaterialAsset source, int width, int height)
        {
            Shader shader = Shader.Find("Hidden/SDF/URPVolumeRaymarch");
            if (shader == null || !shader.isSupported)
                return null;
            width = Mathf.Clamp(width, 16, 512); height = Mathf.Clamp(height, 16, 512);
            Material material = null;
            GraphicsBuffer models = null, shapes = null, modifiers = null, materials = null;
            RenderTexture target = null;
            GameObject cameraObject = null;
            RenderTexture previous = RenderTexture.active;
            Vector4 previousScaledScreen = Shader.GetGlobalVector("_ScaledScreenParams");
            try
            {
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                models = Buffer(new[] { new ModelGpu { MinStart = new Vector4(-1.05f, -1.05f, -1.05f, 0f), MaxCount = new Vector4(1.05f, 1.05f, 1.05f, 1f) } });
                shapes = Buffer(new[] { new ShapeGpu
                {
                    W0 = new Vector4(1f,0f,0f,0f), W1 = new Vector4(0f,1f,0f,0f), W2 = new Vector4(0f,0f,1f,0f),
                    P0 = new Vector4(1f,1f,0.75f,0.2f), P1 = new Vector4(0.5f,0.5f,0.5f,0.1f),
                    TypeOperation = Vector4.zero, MaterialMeta = new Vector4(0f,0f,1f,1f),
                    Min = new Vector4(-1f,-1f,-1f,0f), Max = new Vector4(1f,1f,1f,0f)
                }});
                modifiers = Buffer(new[] { new ModifierGpu() });
                Color baseColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? source.BaseColor.linear : source.BaseColor;
                Color specular = QualitySettings.activeColorSpace == ColorSpace.Linear ? source.SpecularColor.linear : source.SpecularColor;
                Color emission = QualitySettings.activeColorSpace == ColorSpace.Linear ? source.Emission.linear : source.Emission;
                materials = Buffer(new[] { new MaterialGpu
                {
                    BaseColor = baseColor,
                    SpecularPower = new Vector4(specular.r,specular.g,specular.b,source.SpecularPower),
                    EmissionBands = new Vector4(emission.r,emission.g,emission.b,source.CelBands),
                    ModelMetalSmooth = new Vector4((float)source.ShadingModel,source.Metallic,source.Smoothness,source.Occlusion),
                    Custom0 = source.Custom0, Custom1 = source.Custom1,
                    CustomTexture = new Vector4(source.CustomShader != null ? SDFCustomShaderRegistry.Resolve(source.CustomShader.StableId) : -1, source.BaseMap != null ? 1f : 0f,0f,0f),
                    PbrTextures = new Vector4(source.NormalMap != null ? 2f : 0f, source.MetallicMap != null ? 3f : 0f,
                        source.RoughnessMap != null ? 4f : 0f, source.NormalScale)
                }});

                material.SetBuffer("_SDFModels", models); material.SetBuffer("_SDFShapes", shapes);
                material.SetBuffer("_SDFModifiers", modifiers); material.SetBuffer("_SDFMaterials", materials);
                material.SetTexture("_SDFTexture0", Texture2D.whiteTexture);
                material.SetTexture("_SDFTexture1", source.BaseMap != null ? source.BaseMap : Texture2D.whiteTexture);
                material.SetTexture("_SDFTexture2", source.NormalMap != null ? source.NormalMap : Texture2D.whiteTexture);
                material.SetTexture("_SDFTexture3", source.MetallicMap != null ? source.MetallicMap : Texture2D.whiteTexture);
                material.SetTexture("_SDFTexture4", source.RoughnessMap != null ? source.RoughnessMap : Texture2D.whiteTexture);
                cameraObject = new GameObject("SDF Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -3.2f);
                camera.transform.LookAt(Vector3.zero);
                camera.fieldOfView = 35f; camera.nearClipPlane = 0.01f; camera.farClipPlane = 10f;
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(Matrix4x4.Perspective(camera.fieldOfView, (float)width / height, camera.nearClipPlane, camera.farClipPlane), true);
                material.SetMatrix("_SDFViewProjection", projection * camera.worldToCameraMatrix);
                material.SetVector("_SDFCameraPosition", camera.transform.position);
                material.SetVector("_SDFCameraForward", camera.transform.forward);
                material.SetFloat("_SDFCameraNear", camera.nearClipPlane); material.SetFloat("_SDFOrthographic", 0f);
                material.SetFloat("_SDFSceneView", 0f);
                material.SetFloat("_SDFPixelWorldScale", 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / height);
                material.SetInt("_SDFMaxSteps", 192); material.SetFloat("_SDFMaxDistance", 10f); material.SetFloat("_SDFStepSafety", 0.8f);
                material.SetFloat("_SDFSurfaceEpsilon", 0.0005f); material.SetFloat("_SDFNormalEpsilon", 0.001f); material.SetFloat("_SDFPixelTolerance", 0.5f);
                material.SetInt("_SDFPassMode", 0); material.SetInt("_SDFPreviewMode", 1);
                material.SetInt("_SDFSelfShadows", 0); material.SetInt("_SDFAmbientOcclusionEnabled", 0);
                material.SetVector("_SDFAmbientColor", new Vector3(0.1f,0.11f,0.14f));
                material.SetVector("_SDFLightDirection", new Vector3(-0.4f,0.7f,-0.6f).normalized);
                material.SetVector("_SDFLightColor", Vector3.one);

                target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                RenderTexture.active = target;
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(width, height, 1f + 1f / width, 1f + 1f / height));
                GL.Clear(true, true, new Color(0.06f,0.07f,0.09f,1f));
                GL.Viewport(new Rect(0f,0f,width,height));
                if (!material.SetPass(0)) return null;
                Graphics.DrawProceduralNow(MeshTopology.Triangles, 36, 1);
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                result.ReadPixels(new Rect(0f,0f,width,height), 0, 0, false);
                result.Apply(false, true);
                return result;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                Shader.SetGlobalVector("_ScaledScreenParams", previousScaledScreen);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                models?.Release(); shapes?.Release(); modifiers?.Release(); materials?.Release();
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static GraphicsBuffer Buffer<T>(T[] data) where T : struct
        {
            GraphicsBuffer buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, data.Length, Marshal.SizeOf<T>());
            buffer.SetData(data);
            return buffer;
        }
    }
}
