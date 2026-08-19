using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace SdfRenderer
{
    public sealed class SDFRendererFeature : ScriptableRendererFeature
    {
        private sealed class SDFFrameResources : ContextItem
        {
            public TextureHandle ModelId = TextureHandle.nullHandle;
            public override void Reset() => ModelId = TextureHandle.nullHandle;
        }

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
            private const int SortBucketCount = 256;

            private static readonly int CullModelsId = Shader.PropertyToID("_SDFCullModels");
            private static readonly int IndirectArgumentsId = Shader.PropertyToID("_SDFIndirectArguments");
            private static readonly int CullModelCountId = Shader.PropertyToID("_SDFCullModelCount");
            private static readonly int CullSceneViewId = Shader.PropertyToID("_SDFCullSceneView");
            private static readonly int ModelBucketsId = Shader.PropertyToID("_SDFModelBuckets");
            private static readonly int BucketCountsId = Shader.PropertyToID("_SDFBucketCounts");
            private static readonly int BucketCursorsId = Shader.PropertyToID("_SDFBucketCursors");
            private static readonly int CullCameraPositionId = Shader.PropertyToID("_SDFCullCameraPosition");
            private static readonly int CullSortDistanceId = Shader.PropertyToID("_SDFCullSortDistance");
            private static readonly int CullBoundsExtrusionId = Shader.PropertyToID("_SDFCullBoundsExtrusion");

            // Distinguishes visible lists that cover the same camera and source but cull
            // against different bounds, so the shadow pass does not reuse the camera list.
            private const int CameraVisibilityVariant = 0;
            private const int ShadowVisibilityVariant = 1;
            private static readonly int[] FrustumPlaneIds =
            {
                Shader.PropertyToID("_SDFFrustumPlane0"), Shader.PropertyToID("_SDFFrustumPlane1"),
                Shader.PropertyToID("_SDFFrustumPlane2"), Shader.PropertyToID("_SDFFrustumPlane3"),
                Shader.PropertyToID("_SDFFrustumPlane4"), Shader.PropertyToID("_SDFFrustumPlane5")
            };

            private sealed class PassData
            {
                public Material Material;
                public MaterialPropertyBlock Properties;
                public int InstanceCount;
                public int VertexCount;
                public int ShaderPass;
                public BufferHandle Models;
                public BufferHandle Shapes;
                public BufferHandle Modifiers;
                public BufferHandle Materials;
                public BufferHandle VisibleIndicesHandle;
                public BufferHandle IndirectArgsHandle;
                public GraphicsBuffer IndirectArgs;
                public bool UseIndirect;
            }

            private sealed class VisibilityPassData
            {
                public ComputeShader Shader;
                public int ClearKernel;
                public int CullKernel;
                public int PrefixSumKernel;
                public int ScatterKernel;
                public int ModelCount;
                public int SceneView;
                public Vector4 CameraPosition;
                public float SortDistance;
                public Vector4 BoundsExtrusion;
                public Vector4 Plane0;
                public Vector4 Plane1;
                public Vector4 Plane2;
                public Vector4 Plane3;
                public Vector4 Plane4;
                public Vector4 Plane5;
                public BufferHandle ModelsHandle;
                public BufferHandle VisibleHandle;
                public BufferHandle ArgsHandle;
                public BufferHandle BucketsHandle;
                public BufferHandle CountsHandle;
                public BufferHandle CursorsHandle;
                public GraphicsBuffer Models;
                public GraphicsBuffer Visible;
                public GraphicsBuffer Args;
                public GraphicsBuffer Buckets;
                public GraphicsBuffer Counts;
                public GraphicsBuffer Cursors;
            }

            private sealed class VisibilityEntry
            {
                public readonly Plane[] Planes = new Plane[6];
                public GraphicsBuffer Visible;
                public GraphicsBuffer Args;
                public GraphicsBuffer Buckets;
                public GraphicsBuffer Counts;
                public GraphicsBuffer Cursors;
                public int Capacity;
                public int LastCullFrame = int.MinValue;
                public int LastUsedFrame = int.MinValue;
                public int LastModelCount;
                public Matrix4x4 LastViewProjection;
                public Vector3 LastExtrusion;

                public void EnsureCapacity(int count)
                {
                    if (Counts == null)
                    {
                        Counts = new GraphicsBuffer(GraphicsBuffer.Target.Structured, SortBucketCount, sizeof(uint))
                        {
                            name = "SDF Sort Bucket Counts"
                        };
                        Cursors = new GraphicsBuffer(GraphicsBuffer.Target.Structured, SortBucketCount, sizeof(uint))
                        {
                            name = "SDF Sort Bucket Cursors"
                        };
                    }
                    int capacity = Mathf.NextPowerOfTwo(Mathf.Max(1, count));
                    if (capacity <= Capacity && Visible != null && Args != null && Buckets != null)
                        return;
                    Visible?.Release();
                    Args?.Release();
                    Buckets?.Release();
                    Visible = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, sizeof(uint))
                    {
                        name = "SDF Visible Model Indices"
                    };
                    Args = new GraphicsBuffer(GraphicsBuffer.Target.Structured |
                        GraphicsBuffer.Target.IndirectArguments, 4, sizeof(uint))
                    {
                        name = "SDF Indirect Draw Arguments"
                    };
                    Buckets = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, sizeof(uint))
                    {
                        name = "SDF Model Sort Buckets"
                    };
                    Capacity = capacity;
                    LastCullFrame = int.MinValue;
                }

                public void Dispose()
                {
                    Visible?.Release();
                    Args?.Release();
                    Buckets?.Release();
                    Counts?.Release();
                    Cursors?.Release();
                    Visible = null;
                    Args = null;
                    Buckets = null;
                    Counts = null;
                    Cursors = null;
                    Capacity = 0;
                }
            }

            private readonly struct VisibilityResult
            {
                public readonly GraphicsBuffer Visible;
                public readonly GraphicsBuffer Args;

                public VisibilityResult(GraphicsBuffer visible, GraphicsBuffer args)
                {
                    Visible = visible;
                    Args = args;
                }

                public bool IsValid => Visible != null && Args != null;
            }

            private readonly struct RenderSource
            {
                public readonly int Id;
                public readonly int ModelCount;
                public readonly GraphicsBuffer Models;
                public readonly GraphicsBuffer Shapes;
                public readonly GraphicsBuffer Modifiers;
                public readonly GraphicsBuffer Materials;
                public readonly IReadOnlyList<Texture> Textures;

                public RenderSource(int id, int modelCount, GraphicsBuffer models, GraphicsBuffer shapes,
                    GraphicsBuffer modifiers, GraphicsBuffer materials, IReadOnlyList<Texture> textures)
                {
                    Id = id;
                    ModelCount = modelCount;
                    Models = models;
                    Shapes = shapes;
                    Modifiers = modifiers;
                    Materials = materials;
                    Textures = textures;
                }
            }

            private readonly SDFRenderSettings m_Settings;
            private readonly SDFSceneData m_SceneData = new SDFSceneData();
            private readonly List<RenderSource> m_Sources = new List<RenderSource>(4);
            private readonly Dictionary<long, MaterialPropertyBlock> m_CameraProperties = new Dictionary<long, MaterialPropertyBlock>(8);
            private readonly Dictionary<long, VisibilityEntry> m_Visibility = new Dictionary<long, VisibilityEntry>(8);
            private readonly List<long> m_StaleVisibilityKeys = new List<long>(8);
            private readonly Material m_Material;
            private readonly ComputeShader m_VisibilityShader;
            private readonly int m_ClearVisibilityKernel = -1;
            private readonly int m_CullVisibilityKernel = -1;
            private readonly int m_PrefixSumVisibilityKernel = -1;
            private readonly int m_ScatterVisibilityKernel = -1;
            private readonly ProfilingSampler m_DepthNormalsSampler = new ProfilingSampler("SDF/Depth Normals");
            private readonly ProfilingSampler m_MainLightShadowSampler = new ProfilingSampler("SDF/Main Light Shadow Caster");
            private readonly ProfilingSampler m_ScreenSpaceShadowSampler = new ProfilingSampler("SDF/Screen Space Shadows");
            private readonly ProfilingSampler m_VisibilitySampler = new ProfilingSampler("SDF/GPU Model Visibility");
            private CameraState m_CompatibilityCamera;

            internal bool IsValid => m_Material != null;

            private struct CameraState
            {
                public Matrix4x4 ViewProjection;
                public Matrix4x4 CullingMatrix;
                public Vector3 Position;
                public Vector3 Forward;
                public float Near;
                public float Far;
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
                m_VisibilityShader = Resources.Load<ComputeShader>("SDFModelCull");
                if (m_VisibilityShader != null)
                {
                    m_ClearVisibilityKernel = m_VisibilityShader.FindKernel("ClearCounts");
                    m_CullVisibilityKernel = m_VisibilityShader.FindKernel("CullAndCount");
                    m_PrefixSumVisibilityKernel = m_VisibilityShader.FindKernel("PrefixSumBuckets");
                    m_ScatterVisibilityKernel = m_VisibilityShader.FindKernel("ScatterModels");
                }
                profilingSampler = new ProfilingSampler("SDF/Full Resolution Raymarch");
            }

            internal void Dispose()
            {
                m_SceneData.Dispose();
                m_CameraProperties.Clear();
                foreach (VisibilityEntry entry in m_Visibility.Values) entry.Dispose();
                m_Visibility.Clear();
                m_StaleVisibilityKeys.Clear();
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
                GatherSources(renderGraph);
                if (m_Sources.Count == 0 || m_Material == null)
                    return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), !resources.isActiveTargetBackBuffer);
                CameraState camera = BuildCameraState(cameraData.camera, gpuProjection * cameraData.GetViewMatrix(), lightData);

                bool reuseDepthNormalPrepass = m_Settings.ReuseDepthNormalPrepass &&
                    (m_Settings.UseUrpScreenSpaceAo || m_Settings.CastMainLightShadows) &&
                    resources.cameraDepthTexture.IsValid() && resources.cameraNormalsTexture.IsValid();
                SDFFrameResources sdfResources = frameData.Contains<SDFFrameResources>()
                    ? frameData.Get<SDFFrameResources>() : null;
                bool resolveFromModelIds = reuseDepthNormalPrepass &&
                    sdfResources != null && sdfResources.ModelId.IsValid();
                int modelIdBase = 0;
                for (int sourceIndex = 0; sourceIndex < m_Sources.Count; ++sourceIndex)
                {
                    RenderSource source = m_Sources[sourceIndex];
                    VisibilityResult visibility = resolveFromModelIds
                        ? default : RecordVisibility(renderGraph, camera, source);
                    using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                        "SDF Full Resolution Raymarch", out PassData passData, profilingSampler);
                    SetupDrawPass(renderGraph, builder, passData, source, visibility);
                    passData.Material = m_Material;
                    passData.InstanceCount = resolveFromModelIds ? 1 : source.ModelCount;
                    passData.VertexCount = resolveFromModelIds ? 3 : 36;
                    passData.ShaderPass = resolveFromModelIds ? 4 : 0;
                    passData.Properties = BuildProperties(camera, source, modelIdBase, 0, 0,
                        reuseDepthNormalPrepass, visibility);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                    if (reuseDepthNormalPrepass)
                    {
                        builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                        builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                    }
                    if (resolveFromModelIds)
                        builder.UseTexture(sdfResources.ModelId, AccessFlags.Read);
                    if (resources.mainShadowsTexture.IsValid()) builder.UseTexture(resources.mainShadowsTexture, AccessFlags.Read);
                    if (resources.additionalShadowsTexture.IsValid()) builder.UseTexture(resources.additionalShadowsTexture, AccessFlags.Read);
                    if (resources.ssaoTexture.IsValid()) builder.UseTexture(resources.ssaoTexture, AccessFlags.Read);
                    SetDrawFunction(builder);
                    modelIdBase += source.ModelCount;
                }
            }

#pragma warning disable CS0672, CS0618
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                GatherSources(null);
                if (m_Sources.Count == 0 || m_Material == null)
                    return;
                CommandBuffer cmd = CommandBufferPool.Get("SDF Full Resolution Raymarch");
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    int modelIdBase = 0;
                    IReadOnlyList<ISDFGpuSceneBatch> batches = SDFGpuSceneBatchRegistry.Active;
                    for (int i = 0; i < batches.Count; ++i) batches[i].PrepareCompatibility(cmd);
                    for (int i = 0; i < m_Sources.Count; ++i)
                    {
                        RenderSource source = m_Sources[i];
                        cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 36,
                            source.ModelCount, BuildProperties(m_CompatibilityCamera, source, modelIdBase, 0));
                        modelIdBase += source.ModelCount;
                    }
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore CS0672, CS0618

            internal void RecordDepthNormals(RenderGraph renderGraph, ContextContainer frameData)
            {
                GatherSources(renderGraph);
                if (m_Sources.Count == 0 || m_Material == null)
                    return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (!resources.cameraNormalsTexture.IsValid() || !resources.cameraDepthTexture.IsValid())
                    return;
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true);
                CameraState camera = BuildCameraState(cameraData.camera, gpuProjection * cameraData.GetViewMatrix(), lightData);

                bool supportsModelIdTarget = SystemInfo.supportedRenderTargetCount >= 2 &&
                    SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, GraphicsFormatUsage.Render);
                TextureHandle modelId = TextureHandle.nullHandle;
                if (supportsModelIdTarget)
                {
                    TextureDesc descriptor = resources.cameraNormalsTexture.GetDescriptor(renderGraph);
                    descriptor.name = "SDF Model IDs";
                    descriptor.format = GraphicsFormat.R32_SFloat;
                    descriptor.clearBuffer = true;
                    descriptor.clearColor = Color.clear;
                    descriptor.filterMode = FilterMode.Point;
                    modelId = renderGraph.CreateTexture(descriptor);
                    frameData.GetOrCreate<SDFFrameResources>().ModelId = modelId;
                }
                int modelIdBase = 0;
                for (int sourceIndex = 0; sourceIndex < m_Sources.Count; ++sourceIndex)
                {
                    RenderSource source = m_Sources[sourceIndex];
                    VisibilityResult visibility = RecordVisibility(renderGraph, camera, source);
                    using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                        "SDF Depth Normals", out PassData passData, m_DepthNormalsSampler);
                    SetupDrawPass(renderGraph, builder, passData, source, visibility);
                    passData.Material = m_Material;
                    passData.InstanceCount = source.ModelCount;
                    passData.VertexCount = 36;
                    passData.ShaderPass = 1;
                    passData.Properties = BuildProperties(camera, source, modelIdBase, 1, 0, false, visibility);
                    builder.SetRenderAttachment(resources.cameraNormalsTexture, 0, AccessFlags.ReadWrite);
                    if (supportsModelIdTarget)
                    {
                        builder.SetRenderAttachment(modelId, 1, AccessFlags.ReadWrite);
                        builder.SetGlobalTextureAfterPass(modelId, SDFShaderIds.ModelIdTexture);
                    }
                    builder.SetRenderAttachmentDepth(resources.cameraDepthTexture, AccessFlags.ReadWrite);
                    SetDrawFunction(builder);
                    modelIdBase += source.ModelCount;
                }
            }

            internal void RecordMainLightShadows(RenderGraph renderGraph, ContextContainer frameData)
            {
                GatherSources(renderGraph);
                if (m_Sources.Count == 0 || m_Material == null)
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

                int modelIdBase = 0;
                for (int sourceIndex = 0; sourceIndex < m_Sources.Count; ++sourceIndex)
                {
                    RenderSource source = m_Sources[sourceIndex];
                    using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                        "SDF Main Light Shadow Caster", out PassData passData, m_MainLightShadowSampler);
                    SetupDrawPass(renderGraph, builder, passData, source);
                    passData.Material = m_Material;
                    passData.InstanceCount = source.ModelCount * cascadeCount;
                    passData.VertexCount = 36;
                    passData.ShaderPass = 2;
                    passData.Properties = BuildProperties(camera, source, modelIdBase, 2, cascadeCount);
                    builder.SetRenderAttachmentDepth(resources.mainShadowsTexture, AccessFlags.ReadWrite);
                    SetDrawFunction(builder);
                    modelIdBase += source.ModelCount;
                }
            }

            internal void RecordScreenSpaceShadows(RenderGraph renderGraph, ContextContainer frameData)
            {
                GatherSources(renderGraph);
                if (m_Sources.Count == 0 || m_Material == null)
                    return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (!resources.cameraDepthTexture.IsValid() || !resources.cameraNormalsTexture.IsValid())
                    return;
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), !resources.isActiveTargetBackBuffer);
                CameraState camera = BuildCameraState(cameraData.camera, gpuProjection * cameraData.GetViewMatrix(), lightData);

                // Vert extrudes each caster along the light by ShadowMaxDistance, so the
                // cull has to see that same swept volume: a caster behind the camera can
                // still drop a shadow onto a receiver in view. Until now this pass drew
                // every model in the source with no culling at all.
                Vector3 shadowExtrusion = -camera.LightDirection * m_Settings.ShadowMaxDistance;
                int modelIdBase = 0;
                for (int sourceIndex = 0; sourceIndex < m_Sources.Count; ++sourceIndex)
                {
                    RenderSource source = m_Sources[sourceIndex];
                    VisibilityResult visibility = RecordVisibility(renderGraph, camera, source,
                        shadowExtrusion, ShadowVisibilityVariant);
                    using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                        "SDF Screen Space Shadows", out PassData passData, m_ScreenSpaceShadowSampler);
                    SetupDrawPass(renderGraph, builder, passData, source, visibility);
                    passData.Material = m_Material;
                    passData.InstanceCount = source.ModelCount;
                    passData.VertexCount = 36;
                    passData.ShaderPass = 3;
                    passData.Properties = BuildProperties(camera, source, modelIdBase, 3, 0, false, visibility);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    // Bound read-only purely so the pass can depth-test. The pass writes no
                    // depth; without an attachment the only legal state is ZTest Always,
                    // which is what made every fragment of every extruded caster volume
                    // reach the fragment shader.
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                    SetDrawFunction(builder);
                    modelIdBase += source.ModelCount;
                }
            }

            private void GatherSources(RenderGraph renderGraph)
            {
                m_SceneData.UpdateIfNeeded();
                m_Sources.Clear();
                if (m_SceneData.ModelCount > 0 && m_SceneData.ModelBuffer != null &&
                    m_SceneData.ShapeBuffer != null && m_SceneData.ModifierBuffer != null && m_SceneData.MaterialBuffer != null)
                {
                    m_Sources.Add(new RenderSource(0, m_SceneData.ModelCount, m_SceneData.ModelBuffer,
                        m_SceneData.ShapeBuffer, m_SceneData.ModifierBuffer, m_SceneData.MaterialBuffer,
                        m_SceneData.Textures));
                }
                IReadOnlyList<ISDFGpuSceneBatch> batches = SDFGpuSceneBatchRegistry.Active;
                for (int i = 0; i < batches.Count; ++i)
                {
                    ISDFGpuSceneBatch batch = batches[i];
                    if (batch == null || batch.ModelCount <= 0 || batch.ModelBuffer == null ||
                        batch.ShapeBuffer == null || batch.ModifierBuffer == null || batch.MaterialBuffer == null)
                        continue;
                    if (renderGraph != null)
                        batch.RecordUpdate(renderGraph);
                    m_Sources.Add(new RenderSource(batch.BatchId, batch.ModelCount, batch.ModelBuffer,
                        batch.ShapeBuffer, batch.ModifierBuffer, batch.MaterialBuffer, batch.Textures));
                }
            }

            private VisibilityResult RecordVisibility(RenderGraph renderGraph, CameraState camera, RenderSource source,
                Vector3 boundsExtrusion = default, int variant = CameraVisibilityVariant)
            {
                if (renderGraph == null || m_VisibilityShader == null || !SystemInfo.supportsComputeShaders ||
                    source.ModelCount < 64)
                    return default;

                long key = ((long)camera.CameraId << 32) ^ ((long)source.Id << 4) ^ (uint)variant;
                if (!m_Visibility.TryGetValue(key, out VisibilityEntry entry))
                {
                    entry = new VisibilityEntry();
                    m_Visibility.Add(key, entry);
                }
                entry.EnsureCapacity(source.ModelCount);
                int frame = Time.frameCount;
                entry.LastUsedFrame = frame;
                bool alreadyRecorded = entry.LastCullFrame == frame && entry.LastModelCount == source.ModelCount &&
                    entry.LastViewProjection == camera.CullingMatrix && entry.LastExtrusion == boundsExtrusion;
                if (!alreadyRecorded)
                {
                    entry.LastCullFrame = frame;
                    entry.LastModelCount = source.ModelCount;
                    entry.LastViewProjection = camera.CullingMatrix;
                    entry.LastExtrusion = boundsExtrusion;
                    GeometryUtility.CalculateFrustumPlanes(camera.CullingMatrix, entry.Planes);

                    using IComputeRenderGraphBuilder builder = renderGraph.AddComputePass<VisibilityPassData>(
                        "SDF GPU Model Visibility", out VisibilityPassData data,
                        m_VisibilitySampler);
                    data.Shader = m_VisibilityShader;
                    data.ClearKernel = m_ClearVisibilityKernel;
                    data.CullKernel = m_CullVisibilityKernel;
                    data.PrefixSumKernel = m_PrefixSumVisibilityKernel;
                    data.ScatterKernel = m_ScatterVisibilityKernel;
                    data.ModelCount = source.ModelCount;
                    data.SceneView = camera.SceneView > 0.5f ? 1 : 0;
                    data.CameraPosition = camera.Position;
                    data.SortDistance = camera.Far;
                    data.BoundsExtrusion = boundsExtrusion;
                    data.Plane0 = PlaneVector(entry.Planes[0]);
                    data.Plane1 = PlaneVector(entry.Planes[1]);
                    data.Plane2 = PlaneVector(entry.Planes[2]);
                    data.Plane3 = PlaneVector(entry.Planes[3]);
                    data.Plane4 = PlaneVector(entry.Planes[4]);
                    data.Plane5 = PlaneVector(entry.Planes[5]);
                    data.Models = source.Models;
                    data.Visible = entry.Visible;
                    data.Args = entry.Args;
                    data.Buckets = entry.Buckets;
                    data.Counts = entry.Counts;
                    data.Cursors = entry.Cursors;
                    data.ModelsHandle = renderGraph.ImportBuffer(source.Models);
                    data.VisibleHandle = renderGraph.ImportBuffer(entry.Visible);
                    data.ArgsHandle = renderGraph.ImportBuffer(entry.Args);
                    data.BucketsHandle = renderGraph.ImportBuffer(entry.Buckets);
                    data.CountsHandle = renderGraph.ImportBuffer(entry.Counts);
                    data.CursorsHandle = renderGraph.ImportBuffer(entry.Cursors);
                    builder.UseBuffer(data.ModelsHandle, AccessFlags.Read);
                    builder.UseBuffer(data.VisibleHandle, AccessFlags.Write);
                    builder.UseBuffer(data.ArgsHandle, AccessFlags.Write);
                    builder.UseBuffer(data.BucketsHandle, AccessFlags.ReadWrite);
                    builder.UseBuffer(data.CountsHandle, AccessFlags.ReadWrite);
                    builder.UseBuffer(data.CursorsHandle, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (VisibilityPassData pass, ComputeGraphContext context) =>
                        DispatchVisibility(pass, context.cmd));
                }
                PruneVisibility(frame);
                return new VisibilityResult(entry.Visible, entry.Args);
            }

            private static Vector4 PlaneVector(Plane plane) =>
                new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);

            private static void DispatchVisibility(VisibilityPassData data, ComputeCommandBuffer commandBuffer)
            {
                int modelGroups = (data.ModelCount + 63) / 64;

                commandBuffer.SetComputeBufferParam(data.Shader, data.ClearKernel, IndirectArgumentsId, data.Args);
                commandBuffer.SetComputeBufferParam(data.Shader, data.ClearKernel, BucketCountsId, data.Counts);
                commandBuffer.DispatchCompute(data.Shader, data.ClearKernel, 1, 1, 1);

                commandBuffer.SetComputeIntParam(data.Shader, CullModelCountId, data.ModelCount);
                commandBuffer.SetComputeIntParam(data.Shader, CullSceneViewId, data.SceneView);
                commandBuffer.SetComputeVectorParam(data.Shader, CullCameraPositionId, data.CameraPosition);
                commandBuffer.SetComputeFloatParam(data.Shader, CullSortDistanceId, data.SortDistance);
                commandBuffer.SetComputeVectorParam(data.Shader, CullBoundsExtrusionId, data.BoundsExtrusion);
                commandBuffer.SetComputeVectorParam(data.Shader, FrustumPlaneIds[0], data.Plane0);
                commandBuffer.SetComputeVectorParam(data.Shader, FrustumPlaneIds[1], data.Plane1);
                commandBuffer.SetComputeVectorParam(data.Shader, FrustumPlaneIds[2], data.Plane2);
                commandBuffer.SetComputeVectorParam(data.Shader, FrustumPlaneIds[3], data.Plane3);
                commandBuffer.SetComputeVectorParam(data.Shader, FrustumPlaneIds[4], data.Plane4);
                commandBuffer.SetComputeVectorParam(data.Shader, FrustumPlaneIds[5], data.Plane5);
                commandBuffer.SetComputeBufferParam(data.Shader, data.CullKernel, CullModelsId, data.Models);
                commandBuffer.SetComputeBufferParam(data.Shader, data.CullKernel, IndirectArgumentsId, data.Args);
                commandBuffer.SetComputeBufferParam(data.Shader, data.CullKernel, ModelBucketsId, data.Buckets);
                commandBuffer.SetComputeBufferParam(data.Shader, data.CullKernel, BucketCountsId, data.Counts);
                commandBuffer.DispatchCompute(data.Shader, data.CullKernel, modelGroups, 1, 1);

                commandBuffer.SetComputeBufferParam(data.Shader, data.PrefixSumKernel, BucketCountsId, data.Counts);
                commandBuffer.SetComputeBufferParam(data.Shader, data.PrefixSumKernel, BucketCursorsId, data.Cursors);
                commandBuffer.DispatchCompute(data.Shader, data.PrefixSumKernel, 1, 1, 1);

                commandBuffer.SetComputeIntParam(data.Shader, CullModelCountId, data.ModelCount);
                commandBuffer.SetComputeBufferParam(data.Shader, data.ScatterKernel, ModelBucketsId, data.Buckets);
                commandBuffer.SetComputeBufferParam(data.Shader, data.ScatterKernel, BucketCursorsId, data.Cursors);
                commandBuffer.SetComputeBufferParam(data.Shader, data.ScatterKernel,
                    SDFShaderIds.VisibleModelIndices, data.Visible);
                commandBuffer.DispatchCompute(data.Shader, data.ScatterKernel, modelGroups, 1, 1);
            }

            private void PruneVisibility(int frame)
            {
                if ((frame & 127) != 0 || m_Visibility.Count <= 8)
                    return;
                m_StaleVisibilityKeys.Clear();
                foreach (KeyValuePair<long, VisibilityEntry> pair in m_Visibility)
                {
                    if (frame - pair.Value.LastUsedFrame > 256)
                        m_StaleVisibilityKeys.Add(pair.Key);
                }
                for (int i = 0; i < m_StaleVisibilityKeys.Count; ++i)
                {
                    long key = m_StaleVisibilityKeys[i];
                    m_Visibility[key].Dispose();
                    m_Visibility.Remove(key);
                }
                m_StaleVisibilityKeys.Clear();
            }

            private static void SetupDrawPass(RenderGraph renderGraph, IRasterRenderGraphBuilder builder,
                PassData passData, RenderSource source, VisibilityResult visibility = default)
            {
                passData.Models = renderGraph.ImportBuffer(source.Models);
                passData.Shapes = renderGraph.ImportBuffer(source.Shapes);
                passData.Modifiers = renderGraph.ImportBuffer(source.Modifiers);
                passData.Materials = renderGraph.ImportBuffer(source.Materials);
                builder.UseBuffer(passData.Models, AccessFlags.Read);
                builder.UseBuffer(passData.Shapes, AccessFlags.Read);
                builder.UseBuffer(passData.Modifiers, AccessFlags.Read);
                builder.UseBuffer(passData.Materials, AccessFlags.Read);
                passData.UseIndirect = visibility.IsValid;
                if (visibility.IsValid)
                {
                    passData.VisibleIndicesHandle = renderGraph.ImportBuffer(visibility.Visible);
                    passData.IndirectArgsHandle = renderGraph.ImportBuffer(visibility.Args);
                    passData.IndirectArgs = visibility.Args;
                    builder.UseBuffer(passData.VisibleIndicesHandle, AccessFlags.Read);
                    builder.UseBuffer(passData.IndirectArgsHandle, AccessFlags.Read);
                }
            }

            private static void SetDrawFunction(IRasterRenderGraphBuilder builder)
            {
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    if (data.UseIndirect)
                        context.cmd.DrawProceduralIndirect(Matrix4x4.identity, data.Material, data.ShaderPass,
                            MeshTopology.Triangles, data.IndirectArgs, 0, data.Properties);
                    else
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, data.ShaderPass,
                            MeshTopology.Triangles, data.VertexCount, data.InstanceCount, data.Properties);
                });
            }

            private MaterialPropertyBlock BuildProperties(CameraState camera, RenderSource source, int modelIdBase,
                int passMode, int shadowCascadeCount = 0,
                bool reuseDepthNormalPrepass = false, VisibilityResult visibility = default)
            {
                long key = ((long)camera.CameraId << 32) ^ ((long)source.Id << 8) ^ (uint)passMode;
                if (!m_CameraProperties.TryGetValue(key, out MaterialPropertyBlock properties))
                {
                    properties = new MaterialPropertyBlock();
                    m_CameraProperties.Add(key, properties);
                }
                properties.Clear();
                properties.SetBuffer(SDFShaderIds.Models, source.Models);
                properties.SetBuffer(SDFShaderIds.Shapes, source.Shapes);
                properties.SetBuffer(SDFShaderIds.Modifiers, source.Modifiers);
                properties.SetBuffer(SDFShaderIds.Materials, source.Materials);
                properties.SetInteger(SDFShaderIds.UseVisibleModelIndices, visibility.IsValid ? 1 : 0);
                if (visibility.IsValid)
                    properties.SetBuffer(SDFShaderIds.VisibleModelIndices, visibility.Visible);
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
                properties.SetInteger(SDFShaderIds.ModelCount, source.ModelCount);
                properties.SetInteger(SDFShaderIds.ModelIdBase, modelIdBase);
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
                    Texture texture = i < source.Textures.Count ? source.Textures[i] : Texture2D.whiteTexture;
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
                    CullingMatrix = camera.cullingMatrix,
                    Position = camera.transform.position,
                    Forward = camera.transform.forward,
                    Near = camera.nearClipPlane,
                    Far = camera.farClipPlane,
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
