using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace SdfRenderer
{
    public sealed class SDFRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private SDFRenderSettings m_Settings;
        [SerializeField] private Shader m_RaymarchShader;
        [SerializeField] private RenderPassEvent m_InjectionPoint = RenderPassEvent.AfterRenderingOpaques;

        private SDFRenderPass m_Pass;
        private SDFMainLightShadowPass m_ShadowPass;
        private SDFScreenSpaceShadowPass m_ScreenSpaceShadowPass;
        private SDFDepthNormalsPass m_DepthNormalsPass;
        private SDFRenderSettings m_RuntimeSettings;

        public override void Create()
        {
            if (m_RaymarchShader == null)
                m_RaymarchShader = Shader.Find("Hidden/SDF/URPVolumeRaymarch");
            CoreUtils.Destroy(m_RuntimeSettings);
            m_RuntimeSettings = null;
            if (m_Settings == null)
            {
                m_RuntimeSettings = ScriptableObject.CreateInstance<SDFRenderSettings>();
                m_RuntimeSettings.hideFlags = HideFlags.HideAndDontSave;
            }
            m_Pass?.Dispose();
            m_Pass = new SDFRenderPass(m_Settings != null ? m_Settings : m_RuntimeSettings, m_RaymarchShader)
            {
                renderPassEvent = m_InjectionPoint
            };
            m_DepthNormalsPass = new SDFDepthNormalsPass(m_Pass)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPrePasses
            };
            m_ShadowPass = new SDFMainLightShadowPass(m_Pass)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingShadows
            };
            m_ScreenSpaceShadowPass = new SDFScreenSpaceShadowPass(m_Pass)
            {
                renderPassEvent = (RenderPassEvent)((int)m_InjectionPoint + 1)
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Pass == null || !m_Pass.IsValid)
                return;
            Camera camera = renderingData.cameraData.camera;
            if (camera == null || camera.cameraType == CameraType.Reflection)
                return;
            m_Pass.SetupCompatibilityCamera(ref renderingData);
            SDFRenderSettings activeSettings = m_Settings != null ? m_Settings : m_RuntimeSettings;
            if (activeSettings.UseUrpScreenSpaceAo || activeSettings.CastMainLightShadows)
                renderer.EnqueuePass(m_DepthNormalsPass);
            renderer.EnqueuePass(m_Pass);
            if (activeSettings.CastMainLightShadows)
                renderer.EnqueuePass(m_ScreenSpaceShadowPass);
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
            m_Pass = null;
            m_ShadowPass = null;
            m_ScreenSpaceShadowPass = null;
            m_DepthNormalsPass = null;
            CoreUtils.Destroy(m_RuntimeSettings);
            m_RuntimeSettings = null;
        }

        private sealed class SDFRenderPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                public Material Material;
                public MaterialPropertyBlock Properties;
                public int ModelCount;
                public int InstanceCount;
                public int ShaderPass;
            }

            private readonly SDFRenderSettings m_Settings;
            private readonly SDFSceneData m_SceneData = new SDFSceneData();
            private readonly Dictionary<long, MaterialPropertyBlock> m_CameraProperties = new Dictionary<long, MaterialPropertyBlock>(8);
            private readonly Material m_Material;
            private readonly ProfilingSampler m_DepthNormalsSampler = new ProfilingSampler("SDF/Depth Normals");
            private readonly ProfilingSampler m_MainLightShadowSampler = new ProfilingSampler("SDF/Main Light Shadow Caster");
            private readonly ProfilingSampler m_ScreenSpaceShadowSampler = new ProfilingSampler("SDF/Screen Space Shadows");
            private CameraState m_CompatibilityCamera;

            internal bool IsValid => m_Material != null;

            private struct CameraState
            {
                public Matrix4x4 ViewProjection;
                public Vector3 Position;
                public Vector3 Forward;
                public float Near;
                public float Orthographic;
                public float SceneView;
                public float PixelWorldScale;
                public Vector3 LightDirection;
                public Color LightColor;
                public int CameraId;
            }

            internal SDFRenderPass(SDFRenderSettings settings, Shader shader)
            {
                m_Settings = settings;
                if (shader != null)
                    m_Material = CoreUtils.CreateEngineMaterial(shader);
                profilingSampler = new ProfilingSampler("SDF/Full Resolution Raymarch");
            }

            internal void Dispose()
            {
                m_SceneData.Dispose();
                m_CameraProperties.Clear();
                CoreUtils.Destroy(m_Material);
            }

            internal void SetupCompatibilityCamera(ref RenderingData renderingData)
            {
                Camera camera = renderingData.cameraData.camera;
                Matrix4x4 viewProjection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;
                int mainLightIndex = renderingData.lightData.mainLightIndex;
                bool hasMainLight = mainLightIndex >= 0 && mainLightIndex < renderingData.lightData.visibleLights.Length;
                VisibleLight mainLight = hasMainLight ? renderingData.lightData.visibleLights[mainLightIndex] : default;
                m_CompatibilityCamera = BuildCameraState(camera, viewProjection, hasMainLight, mainLight);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                m_SceneData.UpdateIfNeeded();
                if (m_SceneData.ModelCount <= 0 || m_Material == null)
                    return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), !resources.isActiveTargetBackBuffer);
                CameraState camera = BuildCameraState(cameraData.camera, gpuProjection * cameraData.GetViewMatrix(), lightData);

                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("SDF Full Resolution Raymarch", out PassData passData, profilingSampler);
                passData.Material = m_Material;
                passData.ModelCount = m_SceneData.ModelCount;
                passData.InstanceCount = m_SceneData.ModelCount;
                passData.ShaderPass = 0;
                bool reuseDepthNormalPrepass = m_Settings.ReuseDepthNormalPrepass &&
                    (m_Settings.UseUrpScreenSpaceAo || m_Settings.CastMainLightShadows) &&
                    resources.cameraDepthTexture.IsValid() && resources.cameraNormalsTexture.IsValid();
                passData.Properties = BuildProperties(camera, 0, 0, reuseDepthNormalPrepass);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                if (reuseDepthNormalPrepass)
                {
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                }
                // These textures are sampled through URP globals in Lighting.hlsl.
                // RenderGraph still needs explicit read declarations to preserve their
                // lifetime and order this procedural pass after their producers.
                if (resources.mainShadowsTexture.IsValid())
                    builder.UseTexture(resources.mainShadowsTexture, AccessFlags.Read);
                if (resources.additionalShadowsTexture.IsValid())
                    builder.UseTexture(resources.additionalShadowsTexture, AccessFlags.Read);
                if (resources.ssaoTexture.IsValid())
                    builder.UseTexture(resources.ssaoTexture, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, data.ShaderPass, MeshTopology.Triangles, 36, data.InstanceCount, data.Properties);
                });
            }

#pragma warning disable CS0672, CS0618
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                m_SceneData.UpdateIfNeeded();
                if (m_SceneData.ModelCount <= 0 || m_Material == null)
                    return;
                CommandBuffer cmd = CommandBufferPool.Get("SDF Full Resolution Raymarch");
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 36, m_SceneData.ModelCount, BuildProperties(m_CompatibilityCamera, 0));
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore CS0672, CS0618

            internal void RecordDepthNormals(RenderGraph renderGraph, ContextContainer frameData)
            {
                m_SceneData.UpdateIfNeeded();
                if (m_SceneData.ModelCount <= 0 || m_Material == null)
                    return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (!resources.cameraNormalsTexture.IsValid() || !resources.cameraDepthTexture.IsValid())
                    return;
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true);
                CameraState camera = BuildCameraState(cameraData.camera, gpuProjection * cameraData.GetViewMatrix(), lightData);

                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("SDF Depth Normals", out PassData passData, m_DepthNormalsSampler);
                passData.Material = m_Material;
                passData.ModelCount = m_SceneData.ModelCount;
                passData.InstanceCount = m_SceneData.ModelCount;
                passData.ShaderPass = 1;
                passData.Properties = BuildProperties(camera, 1);
                builder.SetRenderAttachment(resources.cameraNormalsTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(resources.cameraDepthTexture, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, data.ShaderPass, MeshTopology.Triangles, 36, data.InstanceCount, data.Properties);
                });
            }

            internal void RecordMainLightShadows(RenderGraph renderGraph, ContextContainer frameData)
            {
                m_SceneData.UpdateIfNeeded();
                if (m_SceneData.ModelCount <= 0 || m_Material == null)
                    return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalShadowData shadowData = frameData.Get<UniversalShadowData>();
                if (!shadowData.supportsMainLightShadows || shadowData.mainLightShadowCascadesCount <= 0 ||
                    !resources.mainShadowsTexture.IsValid())
                    return;
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true);
                CameraState camera = BuildCameraState(cameraData.camera, gpuProjection * cameraData.GetViewMatrix(), lightData);
                int cascadeCount = Mathf.Clamp(shadowData.mainLightShadowCascadesCount, 1, 4);

                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("SDF Main Light Shadow Caster", out PassData passData, m_MainLightShadowSampler);
                passData.Material = m_Material;
                passData.ModelCount = m_SceneData.ModelCount;
                passData.InstanceCount = m_SceneData.ModelCount * cascadeCount;
                passData.ShaderPass = 2;
                passData.Properties = BuildProperties(camera, 2, cascadeCount);
                builder.SetRenderAttachmentDepth(resources.mainShadowsTexture, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, data.ShaderPass, MeshTopology.Triangles, 36, data.InstanceCount, data.Properties);
                });
            }

            internal void RecordScreenSpaceShadows(RenderGraph renderGraph, ContextContainer frameData)
            {
                m_SceneData.UpdateIfNeeded();
                if (m_SceneData.ModelCount <= 0 || m_Material == null)
                    return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (!resources.cameraDepthTexture.IsValid() || !resources.cameraNormalsTexture.IsValid())
                    return;
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), !resources.isActiveTargetBackBuffer);
                CameraState camera = BuildCameraState(cameraData.camera, gpuProjection * cameraData.GetViewMatrix(), lightData);

                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("SDF Screen Space Shadows", out PassData passData, m_ScreenSpaceShadowSampler);
                passData.Material = m_Material;
                passData.ModelCount = m_SceneData.ModelCount;
                passData.InstanceCount = m_SceneData.ModelCount;
                passData.ShaderPass = 3;
                passData.Properties = BuildProperties(camera, 3);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, data.ShaderPass, MeshTopology.Triangles, 36, data.InstanceCount, data.Properties);
                });
            }

            private MaterialPropertyBlock BuildProperties(CameraState camera, int passMode, int shadowCascadeCount = 0,
                bool reuseDepthNormalPrepass = false)
            {
                long key = ((long)camera.CameraId << 32) | (uint)passMode;
                if (!m_CameraProperties.TryGetValue(key, out MaterialPropertyBlock properties))
                {
                    properties = new MaterialPropertyBlock();
                    m_CameraProperties.Add(key, properties);
                }
                properties.Clear();
                properties.SetBuffer(SDFShaderIds.Models, m_SceneData.ModelBuffer);
                properties.SetBuffer(SDFShaderIds.Shapes, m_SceneData.ShapeBuffer);
                properties.SetBuffer(SDFShaderIds.Modifiers, m_SceneData.ModifierBuffer);
                properties.SetBuffer(SDFShaderIds.Materials, m_SceneData.MaterialBuffer);
                properties.SetMatrix(SDFShaderIds.ViewProjection, camera.ViewProjection);
                properties.SetVector(SDFShaderIds.CameraPosition, camera.Position);
                properties.SetVector(SDFShaderIds.CameraForward, camera.Forward);
                properties.SetFloat(SDFShaderIds.CameraNear, camera.Near);
                properties.SetFloat(SDFShaderIds.Orthographic, camera.Orthographic);
                properties.SetFloat(SDFShaderIds.SceneView, camera.SceneView);
                properties.SetFloat(SDFShaderIds.PixelWorldScale, camera.PixelWorldScale);
                properties.SetInteger(SDFShaderIds.MaxSteps, m_Settings.MaxSteps);
                properties.SetFloat(SDFShaderIds.MaxDistance, m_Settings.MaxDistance);
                properties.SetFloat(SDFShaderIds.StepSafety, m_Settings.StepSafety);
                properties.SetFloat(SDFShaderIds.SurfaceEpsilon, m_Settings.SurfaceEpsilon);
                properties.SetFloat(SDFShaderIds.NormalEpsilon, m_Settings.NormalEpsilon);
                properties.SetFloat(SDFShaderIds.PixelTolerance, m_Settings.PixelTolerance);
                properties.SetInteger(SDFShaderIds.ReuseDepthNormalPrepass, reuseDepthNormalPrepass ? 1 : 0);
                properties.SetInteger(SDFShaderIds.PassMode, passMode);
                properties.SetInteger(SDFShaderIds.PreviewMode, 0);
                properties.SetInteger(SDFShaderIds.ModelCount, m_SceneData.ModelCount);
                properties.SetInteger(SDFShaderIds.ShadowCascadeCount, shadowCascadeCount);
                properties.SetInteger(SDFShaderIds.ReceiveUrpShadows, m_Settings.ReceiveUrpShadows ? 1 : 0);
                properties.SetInteger(SDFShaderIds.SelfShadows, m_Settings.SdfSelfShadows ? 1 : 0);
                properties.SetInteger(SDFShaderIds.ShadowMaxSteps, m_Settings.ShadowMaxSteps);
                properties.SetFloat(SDFShaderIds.ShadowMaxDistance, m_Settings.ShadowMaxDistance);
                properties.SetFloat(SDFShaderIds.ShadowStepSafety, m_Settings.ShadowStepSafety);
                properties.SetFloat(SDFShaderIds.ShadowBias, m_Settings.ShadowBias);
                properties.SetFloat(SDFShaderIds.ShadowStrength, m_Settings.ShadowStrength);
                properties.SetFloat(SDFShaderIds.ShadowSoftness, m_Settings.ShadowSoftness);
                properties.SetInteger(SDFShaderIds.UseUrpScreenSpaceAo, m_Settings.UseUrpScreenSpaceAo ? 1 : 0);
                properties.SetInteger(SDFShaderIds.AmbientOcclusionEnabled, m_Settings.SdfAmbientOcclusion ? 1 : 0);
                properties.SetFloat(SDFShaderIds.AmbientOcclusionStrength, m_Settings.AmbientOcclusionStrength);
                properties.SetFloat(SDFShaderIds.AmbientOcclusionRadius, m_Settings.AmbientOcclusionRadius);
                properties.SetInteger(SDFShaderIds.AmbientOcclusionSamples, m_Settings.AmbientOcclusionSamples);
                Color ambient = QualitySettings.activeColorSpace == ColorSpace.Linear ? m_Settings.AmbientColor.linear : m_Settings.AmbientColor;
                properties.SetColor(SDFShaderIds.AmbientColor, ambient);
                properties.SetVector(SDFShaderIds.LightDirection, camera.LightDirection);
                properties.SetColor(SDFShaderIds.LightColor, camera.LightColor);
                // Procedural draws have no Renderer, so Unity does not populate the
                // per-renderer ambient-probe and reflection-probe built-ins for us.
                // Bind the scene defaults explicitly to make SampleSH and URP's
                // environment BRDF match a regular MeshRenderer.
                SHCoefficients sphericalHarmonics = new SHCoefficients(RenderSettings.ambientProbe);
                properties.SetVector(SDFShaderIds.UnityShAr, sphericalHarmonics.SHAr);
                properties.SetVector(SDFShaderIds.UnityShAg, sphericalHarmonics.SHAg);
                properties.SetVector(SDFShaderIds.UnityShAb, sphericalHarmonics.SHAb);
                properties.SetVector(SDFShaderIds.UnityShBr, sphericalHarmonics.SHBr);
                properties.SetVector(SDFShaderIds.UnityShBg, sphericalHarmonics.SHBg);
                properties.SetVector(SDFShaderIds.UnityShBb, sphericalHarmonics.SHBb);
                properties.SetVector(SDFShaderIds.UnityShC, sphericalHarmonics.SHC);
                properties.SetVector(SDFShaderIds.ShadowShAr, sphericalHarmonics.SHAr);
                properties.SetVector(SDFShaderIds.ShadowShAg, sphericalHarmonics.SHAg);
                properties.SetVector(SDFShaderIds.ShadowShAb, sphericalHarmonics.SHAb);
                properties.SetVector(SDFShaderIds.ShadowShBr, sphericalHarmonics.SHBr);
                properties.SetVector(SDFShaderIds.ShadowShBg, sphericalHarmonics.SHBg);
                properties.SetVector(SDFShaderIds.ShadowShBb, sphericalHarmonics.SHBb);
                properties.SetVector(SDFShaderIds.ShadowShC, sphericalHarmonics.SHC);
                properties.SetVector(SDFShaderIds.UnityProbesOcclusion, Vector4.one);
                Texture defaultReflection = ReflectionProbe.defaultTexture;
                if (defaultReflection != null)
                    properties.SetTexture(SDFShaderIds.UnitySpecCube0, defaultReflection);
                properties.SetVector(SDFShaderIds.UnitySpecCube0Hdr, ReflectionProbe.defaultTextureHDRDecodeValues);
                properties.SetVector(SDFShaderIds.UnitySpecCube0BoxMax, Vector4.zero);
                properties.SetVector(SDFShaderIds.UnitySpecCube0BoxMin, Vector4.zero);
                properties.SetVector(SDFShaderIds.UnitySpecCube0ProbePosition, Vector4.zero);
                properties.SetVector(SDFShaderIds.UnitySpecCube0Rotation, new Vector4(0f, 0f, 0f, 1f));
                for (int i = 0; i < SDFShaderIds.Textures.Length; ++i)
                {
                    Texture texture = i < m_SceneData.Textures.Count ? m_SceneData.Textures[i] : Texture2D.whiteTexture;
                    properties.SetTexture(SDFShaderIds.Textures[i], texture);
                }
                return properties;
            }

            private CameraState BuildCameraState(Camera camera, Matrix4x4 viewProjection, UniversalLightData lightData)
            {
                int mainLightIndex = lightData.mainLightIndex;
                bool hasMainLight = mainLightIndex >= 0 && mainLightIndex < lightData.visibleLights.Length;
                VisibleLight mainLight = hasMainLight ? lightData.visibleLights[mainLightIndex] : default;
                return BuildCameraState(camera, viewProjection, hasMainLight, mainLight);
            }

            private CameraState BuildCameraState(Camera camera, Matrix4x4 viewProjection, bool hasMainLight, VisibleLight mainLight)
            {
                float pixelHeight = Mathf.Max(1f, camera.pixelHeight);
                float pixelScale = camera.orthographic
                    ? 2f * camera.orthographicSize / pixelHeight
                    : 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / pixelHeight;
                Vector3 direction = m_Settings.LightDirection;
                Color sourceColor = m_Settings.LightColor;
                float intensity = m_Settings.LightIntensity;
                bool useUrpMainLight = hasMainLight && mainLight.lightType == LightType.Directional;
                if (useUrpMainLight)
                {
                    direction = -(Vector3)mainLight.localToWorldMatrix.GetColumn(2);
                    sourceColor = mainLight.finalColor;
                    intensity = 1f;
                }
                else
                {
                    // Match the original SDF pipeline's fallback order. URP normally
                    // exposes this light through UniversalLightData; RenderSettings.sun
                    // also keeps previews and compatibility cameras consistent when it
                    // is temporarily absent from the camera's visible-light list.
                    Light sun = RenderSettings.sun;
                    if (sun != null && sun.isActiveAndEnabled && sun.type == LightType.Directional)
                    {
                        direction = -sun.transform.forward;
                        sourceColor = sun.color;
                        intensity = sun.intensity;
                    }
                }
                Color color = useUrpMainLight
                    ? sourceColor * intensity
                    : (QualitySettings.activeColorSpace == ColorSpace.Linear ? sourceColor.linear * intensity : sourceColor * intensity);
                return new CameraState
                {
                    ViewProjection = viewProjection,
                    Position = camera.transform.position,
                    Forward = camera.transform.forward,
                    Near = camera.nearClipPlane,
                    Orthographic = camera.orthographic ? 1f : 0f,
                    SceneView = camera.cameraType == CameraType.SceneView ? 1f : 0f,
                    PixelWorldScale = pixelScale,
                    LightDirection = direction.normalized,
                    LightColor = color,
                    CameraId = camera.GetInstanceID()
                };
            }
        }

        private sealed class SDFMainLightShadowPass : ScriptableRenderPass
        {
            private readonly SDFRenderPass m_Owner;

            internal SDFMainLightShadowPass(SDFRenderPass owner)
            {
                m_Owner = owner;
                profilingSampler = new ProfilingSampler("SDF/Main Light Shadow Caster");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) =>
                m_Owner.RecordMainLightShadows(renderGraph, frameData);

#pragma warning disable CS0672, CS0618
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }
#pragma warning restore CS0672, CS0618
        }

        private sealed class SDFDepthNormalsPass : ScriptableRenderPass
        {
            private readonly SDFRenderPass m_Owner;

            internal SDFDepthNormalsPass(SDFRenderPass owner)
            {
                m_Owner = owner;
                profilingSampler = new ProfilingSampler("SDF/Depth Normals");
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) =>
                m_Owner.RecordDepthNormals(renderGraph, frameData);

#pragma warning disable CS0672, CS0618
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }
#pragma warning restore CS0672, CS0618
        }

        private sealed class SDFScreenSpaceShadowPass : ScriptableRenderPass
        {
            private readonly SDFRenderPass m_Owner;

            internal SDFScreenSpaceShadowPass(SDFRenderPass owner)
            {
                m_Owner = owner;
                profilingSampler = new ProfilingSampler("SDF/Screen Space Shadows");
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) =>
                m_Owner.RecordScreenSpaceShadows(renderGraph, frameData);

#pragma warning disable CS0672, CS0618
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }
#pragma warning restore CS0672, CS0618
        }
    }
}
