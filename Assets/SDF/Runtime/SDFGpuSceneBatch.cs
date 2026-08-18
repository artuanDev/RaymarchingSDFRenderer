using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SdfRenderer
{
    /// <summary>
    /// A scene-data producer whose final render buffers remain resident on the GPU.
    /// Producers may update them with compute work without rebuilding the component scene.
    /// </summary>
    internal interface ISDFGpuSceneBatch
    {
        int BatchId { get; }
        int ModelCount { get; }
        GraphicsBuffer ModelBuffer { get; }
        GraphicsBuffer ShapeBuffer { get; }
        GraphicsBuffer ModifierBuffer { get; }
        GraphicsBuffer MaterialBuffer { get; }
        IReadOnlyList<Texture> Textures { get; }
        void RecordUpdate(RenderGraph renderGraph);
        void PrepareCompatibility(CommandBuffer commandBuffer);
    }

    internal static class SDFGpuSceneBatchRegistry
    {
        private static readonly List<ISDFGpuSceneBatch> Batches = new List<ISDFGpuSceneBatch>(4);
        private static int s_NextBatchId = 1;

        internal static IReadOnlyList<ISDFGpuSceneBatch> Active => Batches;
        internal static int AllocateId() => s_NextBatchId++;

        internal static void Register(ISDFGpuSceneBatch batch)
        {
            if (batch != null && !Batches.Contains(batch))
                Batches.Add(batch);
        }

        internal static void Unregister(ISDFGpuSceneBatch batch)
        {
            if (batch != null)
                Batches.Remove(batch);
        }
    }

    internal sealed class SDFBenchmarkGpuBatch : ISDFGpuSceneBatch, IDisposable
    {
        private const int ThreadGroupSize = 64;
        private const int ModelStride = 32;
        private const int ShapeStride = 176;
        private const int ModifierStride = 48;
        private const int MaterialStride = 112;

        private static readonly int ModelCountId = Shader.PropertyToID("_ModelCount");
        private static readonly int MaterialCountId = Shader.PropertyToID("_MaterialCount");
        private static readonly int GridWidthId = Shader.PropertyToID("_GridWidth");
        private static readonly int GridSpacingId = Shader.PropertyToID("_GridSpacing");
        private static readonly int AnimationTimeId = Shader.PropertyToID("_AnimationTime");
        private static readonly int AnimationFlagsId = Shader.PropertyToID("_AnimationFlags");
        private static readonly int IncludeOperationsId = Shader.PropertyToID("_IncludeOperations");
        private static readonly int IncludeModifiersId = Shader.PropertyToID("_IncludeModifiers");
        private static readonly int LinearColorSpaceId = Shader.PropertyToID("_LinearColorSpace");

        private sealed class ComputePassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public BufferHandle Models;
            public BufferHandle Shapes;
            public BufferHandle Modifiers;
            public BufferHandle Materials;
            public int ModelCount;
            public int MaterialCount;
            public int GridWidth;
            public float GridSpacing;
            public float AnimationTime;
            public int AnimationFlags;
            public int IncludeOperations;
            public int IncludeModifiers;
        }

        private static readonly IReadOnlyList<Texture> NoTextures = Array.Empty<Texture>();
        private readonly ComputeShader m_Shader;
        private readonly int m_Kernel;
        private readonly int m_GridWidth;
        private float m_GridSpacing;
        private int m_MaterialCount;
        private bool m_IncludeOperations;
        private bool m_IncludeModifiers;
        private float m_AnimationTime;
        private int m_AnimationFlags;
        private uint m_StateVersion = 1;
        private uint m_PreparedVersion;
        private bool m_Disposed;

        public int BatchId { get; }
        public int ModelCount { get; }
        public GraphicsBuffer ModelBuffer { get; }
        public GraphicsBuffer ShapeBuffer { get; }
        public GraphicsBuffer ModifierBuffer { get; }
        public GraphicsBuffer MaterialBuffer { get; }
        public IReadOnlyList<Texture> Textures => NoTextures;

        internal SDFBenchmarkGpuBatch(int modelCount, int materialCount, float spacing,
            bool includeOperations, bool includeModifiers, ComputeShader source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            BatchId = SDFGpuSceneBatchRegistry.AllocateId();
            ModelCount = Mathf.Max(1, modelCount);
            m_MaterialCount = Mathf.Clamp(materialCount, 1, 32);
            m_GridWidth = Mathf.CeilToInt(Mathf.Sqrt(ModelCount));
            m_GridSpacing = Mathf.Max(0.1f, spacing);
            m_IncludeOperations = includeOperations;
            m_IncludeModifiers = includeModifiers;
            m_Shader = UnityEngine.Object.Instantiate(source);
            m_Shader.name = source.name + " (SDF GPU Batch)";
            m_Kernel = m_Shader.FindKernel("UpdateBenchmark");
            ModelBuffer = NewBuffer(ModelCount, ModelStride, "SDF GPU Batch Models");
            ShapeBuffer = NewBuffer(ModelCount * 2, ShapeStride, "SDF GPU Batch Shapes");
            ModifierBuffer = NewBuffer(ModelCount, ModifierStride, "SDF GPU Batch Modifiers");
            MaterialBuffer = NewBuffer(m_MaterialCount, MaterialStride, "SDF GPU Batch Materials");
        }

        internal void SetState(float time, SDFBenchmarkAnimation animation, float spacing,
            int materialCount, bool includeOperations, bool includeModifiers)
        {
            int flags = (int)animation;
            float nextSpacing = Mathf.Max(0.1f, spacing);
            int nextMaterialCount = Mathf.Clamp(materialCount, 1, m_MaterialCount);
            if (Mathf.Approximately(m_AnimationTime, time) && m_AnimationFlags == flags &&
                Mathf.Approximately(m_GridSpacing, nextSpacing) && m_MaterialCount == nextMaterialCount &&
                m_IncludeOperations == includeOperations && m_IncludeModifiers == includeModifiers)
                return;
            m_AnimationTime = time;
            m_AnimationFlags = flags;
            m_GridSpacing = nextSpacing;
            m_MaterialCount = nextMaterialCount;
            m_IncludeOperations = includeOperations;
            m_IncludeModifiers = includeModifiers;
            unchecked { ++m_StateVersion; }
        }

        public void RecordUpdate(RenderGraph renderGraph)
        {
            if (m_Disposed || m_PreparedVersion == m_StateVersion)
                return;
            m_PreparedVersion = m_StateVersion;
            using IComputeRenderGraphBuilder builder = renderGraph.AddComputePass<ComputePassData>(
                "SDF GPU-Resident Scene Update", out ComputePassData data,
                new ProfilingSampler("SDF/GPU Resident Scene Update"));
            FillPassData(renderGraph, data);
            builder.UseBuffer(data.Models, AccessFlags.Write);
            builder.UseBuffer(data.Shapes, AccessFlags.Write);
            builder.UseBuffer(data.Modifiers, AccessFlags.Write);
            builder.UseBuffer(data.Materials, AccessFlags.Write);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (ComputePassData pass, ComputeGraphContext context) => Dispatch(context.cmd, pass));
        }

        public void PrepareCompatibility(CommandBuffer commandBuffer)
        {
            if (m_Disposed || m_PreparedVersion == m_StateVersion)
                return;
            m_PreparedVersion = m_StateVersion;
            commandBuffer.SetComputeBufferParam(m_Shader, m_Kernel, SDFShaderIds.Models, ModelBuffer);
            commandBuffer.SetComputeBufferParam(m_Shader, m_Kernel, SDFShaderIds.Shapes, ShapeBuffer);
            commandBuffer.SetComputeBufferParam(m_Shader, m_Kernel, SDFShaderIds.Modifiers, ModifierBuffer);
            commandBuffer.SetComputeBufferParam(m_Shader, m_Kernel, SDFShaderIds.Materials, MaterialBuffer);
            SetParameters(commandBuffer, m_Shader, ModelCount, m_MaterialCount, m_GridWidth, m_GridSpacing,
                m_AnimationTime, m_AnimationFlags, m_IncludeOperations ? 1 : 0, m_IncludeModifiers ? 1 : 0);
            commandBuffer.DispatchCompute(m_Shader, m_Kernel,
                (Mathf.Max(ModelCount, m_MaterialCount) + ThreadGroupSize - 1) / ThreadGroupSize, 1, 1);
        }

        private void FillPassData(RenderGraph renderGraph, ComputePassData data)
        {
            data.Shader = m_Shader;
            data.Kernel = m_Kernel;
            data.Models = renderGraph.ImportBuffer(ModelBuffer);
            data.Shapes = renderGraph.ImportBuffer(ShapeBuffer);
            data.Modifiers = renderGraph.ImportBuffer(ModifierBuffer);
            data.Materials = renderGraph.ImportBuffer(MaterialBuffer);
            data.ModelCount = ModelCount;
            data.MaterialCount = m_MaterialCount;
            data.GridWidth = m_GridWidth;
            data.GridSpacing = m_GridSpacing;
            data.AnimationTime = m_AnimationTime;
            data.AnimationFlags = m_AnimationFlags;
            data.IncludeOperations = m_IncludeOperations ? 1 : 0;
            data.IncludeModifiers = m_IncludeModifiers ? 1 : 0;
        }

        private static void Dispatch(ComputeCommandBuffer commandBuffer, ComputePassData data)
        {
            commandBuffer.SetComputeBufferParam(data.Shader, data.Kernel, SDFShaderIds.Models, data.Models);
            commandBuffer.SetComputeBufferParam(data.Shader, data.Kernel, SDFShaderIds.Shapes, data.Shapes);
            commandBuffer.SetComputeBufferParam(data.Shader, data.Kernel, SDFShaderIds.Modifiers, data.Modifiers);
            commandBuffer.SetComputeBufferParam(data.Shader, data.Kernel, SDFShaderIds.Materials, data.Materials);
            SetParameters(commandBuffer, data.Shader, data.ModelCount, data.MaterialCount, data.GridWidth,
                data.GridSpacing, data.AnimationTime, data.AnimationFlags, data.IncludeOperations, data.IncludeModifiers);
            commandBuffer.DispatchCompute(data.Shader, data.Kernel,
                (Mathf.Max(data.ModelCount, data.MaterialCount) + ThreadGroupSize - 1) / ThreadGroupSize, 1, 1);
        }

        private static void SetParameters(IComputeCommandBuffer commandBuffer, ComputeShader shader,
            int modelCount, int materialCount, int gridWidth, float spacing, float time, int flags,
            int includeOperations, int includeModifiers)
        {
            commandBuffer.SetComputeIntParam(shader, ModelCountId, modelCount);
            commandBuffer.SetComputeIntParam(shader, MaterialCountId, materialCount);
            commandBuffer.SetComputeIntParam(shader, GridWidthId, gridWidth);
            commandBuffer.SetComputeFloatParam(shader, GridSpacingId, spacing);
            commandBuffer.SetComputeFloatParam(shader, AnimationTimeId, time);
            commandBuffer.SetComputeIntParam(shader, AnimationFlagsId, flags);
            commandBuffer.SetComputeIntParam(shader, IncludeOperationsId, includeOperations);
            commandBuffer.SetComputeIntParam(shader, IncludeModifiersId, includeModifiers);
            commandBuffer.SetComputeIntParam(shader, LinearColorSpaceId,
                QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 : 0);
        }

        private static void SetParameters(CommandBuffer commandBuffer, ComputeShader shader,
            int modelCount, int materialCount, int gridWidth, float spacing, float time, int flags,
            int includeOperations, int includeModifiers)
        {
            commandBuffer.SetComputeIntParam(shader, ModelCountId, modelCount);
            commandBuffer.SetComputeIntParam(shader, MaterialCountId, materialCount);
            commandBuffer.SetComputeIntParam(shader, GridWidthId, gridWidth);
            commandBuffer.SetComputeFloatParam(shader, GridSpacingId, spacing);
            commandBuffer.SetComputeFloatParam(shader, AnimationTimeId, time);
            commandBuffer.SetComputeIntParam(shader, AnimationFlagsId, flags);
            commandBuffer.SetComputeIntParam(shader, IncludeOperationsId, includeOperations);
            commandBuffer.SetComputeIntParam(shader, IncludeModifiersId, includeModifiers);
            commandBuffer.SetComputeIntParam(shader, LinearColorSpaceId,
                QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 : 0);
        }

        private static GraphicsBuffer NewBuffer(int count, int stride, string name) =>
            new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, count), stride) { name = name };

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            ModelBuffer.Release();
            ShapeBuffer.Release();
            ModifierBuffer.Release();
            MaterialBuffer.Release();
            CoreUtils.Destroy(m_Shader);
        }
    }
}
