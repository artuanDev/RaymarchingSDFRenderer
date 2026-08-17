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
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Pass == null || !m_Pass.IsValid)
                return;
            Camera camera = renderingData.cameraData.camera;
            if (camera == null || camera.cameraType == CameraType.Reflection)
                return;
            m_Pass.SetupCompatibilityCamera(ref renderingData);
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
            m_Pass = null;
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
            }

            private readonly SDFRenderSettings m_Settings;
            private readonly SDFSceneData m_SceneData = new SDFSceneData();
            private readonly Dictionary<int, MaterialPropertyBlock> m_CameraProperties = new Dictionary<int, MaterialPropertyBlock>(4);
            private readonly Material m_Material;
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
                m_CompatibilityCamera = BuildCameraState(camera, viewProjection);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                m_SceneData.UpdateIfNeeded();
                if (m_SceneData.ModelCount <= 0 || m_Material == null)
                    return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), !resources.isActiveTargetBackBuffer);
                CameraState camera = BuildCameraState(cameraData.camera, gpuProjection * cameraData.GetViewMatrix());

                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("SDF Full Resolution Raymarch", out PassData passData, profilingSampler);
                passData.Material = m_Material;
                passData.ModelCount = m_SceneData.ModelCount;
                passData.Properties = BuildProperties(camera);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 36, data.ModelCount, data.Properties);
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
                    cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 36, m_SceneData.ModelCount, BuildProperties(m_CompatibilityCamera));
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore CS0672, CS0618

            private MaterialPropertyBlock BuildProperties(CameraState camera)
            {
                if (!m_CameraProperties.TryGetValue(camera.CameraId, out MaterialPropertyBlock properties))
                {
                    properties = new MaterialPropertyBlock();
                    m_CameraProperties.Add(camera.CameraId, properties);
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
                Color ambient = QualitySettings.activeColorSpace == ColorSpace.Linear ? m_Settings.AmbientColor.linear : m_Settings.AmbientColor;
                properties.SetColor(SDFShaderIds.AmbientColor, ambient);
                properties.SetVector(SDFShaderIds.LightDirection, camera.LightDirection);
                properties.SetColor(SDFShaderIds.LightColor, camera.LightColor);
                for (int i = 0; i < SDFShaderIds.Textures.Length; ++i)
                {
                    Texture texture = i < m_SceneData.Textures.Count ? m_SceneData.Textures[i] : Texture2D.whiteTexture;
                    properties.SetTexture(SDFShaderIds.Textures[i], texture);
                }
                return properties;
            }

            private CameraState BuildCameraState(Camera camera, Matrix4x4 viewProjection)
            {
                float pixelHeight = Mathf.Max(1f, camera.pixelHeight);
                float pixelScale = camera.orthographic
                    ? 2f * camera.orthographicSize / pixelHeight
                    : 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / pixelHeight;
                Vector3 direction = m_Settings.LightDirection;
                Color sourceColor = m_Settings.LightColor;
                float intensity = m_Settings.LightIntensity;
                Light sun = RenderSettings.sun;
                if (sun != null && sun.isActiveAndEnabled && sun.type == LightType.Directional)
                {
                    direction = -sun.transform.forward;
                    sourceColor = sun.color;
                    intensity = sun.intensity;
                }
                Color color = QualitySettings.activeColorSpace == ColorSpace.Linear ? sourceColor.linear * intensity : sourceColor * intensity;
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
    }
}
