using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;
using SdfRenderer.Generated;

namespace SdfRenderer
{
    internal sealed class SDFSceneData : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct ModelGpu
        {
            public Vector4 BoundsMinAndShapeStart;
            public Vector4 BoundsMaxAndShapeCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ShapeGpu
        {
            public Vector4 WorldToLocal0;
            public Vector4 WorldToLocal1;
            public Vector4 WorldToLocal2;
            public Vector4 Parameters0;
            public Vector4 Parameters1;
            public Vector4 Parameters2;
            public Vector4 Parameters3;
            public Vector4 TypeOperationSmoothModifierStart;
            public Vector4 MaterialModifierCountDistanceScaleBoundsScale;
            public Vector4 BoundsMin;
            public Vector4 BoundsMax;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ModifierGpu
        {
            public Vector4 TypeAxesAmount;
            public Vector4 Vector;
            public Vector4 Count;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MaterialGpu
        {
            public Vector4 BaseColor;
            public Vector4 SpecularAndPower;
            public Vector4 EmissionAndCelBands;
            public Vector4 ModelMetallicSmoothness;
            public Vector4 Custom0;
            public Vector4 Custom1;
            public Vector4 CustomShaderTextureIndices;
        }

        private static readonly ProfilerMarker CompileMarker = new ProfilerMarker("SDF/CPU Compile Scene");
        private static readonly ProfilerMarker TransformMarker = new ProfilerMarker("SDF/CPU Refresh Transforms");
        private static readonly ProfilerMarker OperationMarker = new ProfilerMarker("SDF/CPU Refresh Operations");
        private static readonly ProfilerMarker ModifierMarker = new ProfilerMarker("SDF/CPU Refresh Modifiers");
        private static readonly ProfilerMarker UploadMarker = new ProfilerMarker("SDF/CPU Upload Buffers");

        private struct ShapeBinding
        {
            public SDFShape Shape;
            public SDFOperation Operation;
            public Bounds BaseLocalBounds;
            public Bounds LocalBounds;
            public SDFOperationType OperationType;
            public float Smoothness;
            public bool CanUseBoundsDistance;
        }

        private struct ModelBinding
        {
            public int ShapeStart;
            public int ShapeCount;
            public float Padding;
            public bool RenderInSceneView;
        }

        private struct ShapeTransformInput
        {
            public Vector3 BaseLocalCenter;
            public Vector3 BaseLocalExtents;
            public Vector3 LocalCenter;
            public Vector3 LocalExtents;
            public Vector3 ClipCenter;
            public Vector3 ClipExtents;
            public float Smoothness;
            public int CanUseBoundsDistance;
        }

        private struct ModelTransformInput
        {
            public int ShapeStart;
            public int ShapeCount;
            public float Padding;
            public int RenderInSceneView;
        }

        [BurstCompile]
        private struct PackShapesJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<ShapeTransformInput> Inputs;
            public NativeArray<ShapeGpu> Shapes;

            public void Execute(int index, TransformAccess transform)
            {
                ShapeTransformInput input = Inputs[index];
                ShapeGpu shape = Shapes[index];
                Matrix4x4 localToWorld = transform.localToWorldMatrix;
                Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

                shape.WorldToLocal0 = new Vector4(worldToLocal.m00, worldToLocal.m01, worldToLocal.m02, worldToLocal.m03);
                shape.WorldToLocal1 = new Vector4(worldToLocal.m10, worldToLocal.m11, worldToLocal.m12, worldToLocal.m13);
                shape.WorldToLocal2 = new Vector4(worldToLocal.m20, worldToLocal.m21, worldToLocal.m22, worldToLocal.m23);

                Vector3 center = input.LocalCenter;
                Vector3 extents = input.LocalExtents;
                Vector3 worldCenter = new Vector3(
                    localToWorld.m00 * center.x + localToWorld.m01 * center.y + localToWorld.m02 * center.z + localToWorld.m03,
                    localToWorld.m10 * center.x + localToWorld.m11 * center.y + localToWorld.m12 * center.z + localToWorld.m13,
                    localToWorld.m20 * center.x + localToWorld.m21 * center.y + localToWorld.m22 * center.z + localToWorld.m23);
                Vector3 worldExtents = new Vector3(
                    math.abs(localToWorld.m00) * extents.x + math.abs(localToWorld.m01) * extents.y + math.abs(localToWorld.m02) * extents.z,
                    math.abs(localToWorld.m10) * extents.x + math.abs(localToWorld.m11) * extents.y + math.abs(localToWorld.m12) * extents.z,
                    math.abs(localToWorld.m20) * extents.x + math.abs(localToWorld.m21) * extents.y + math.abs(localToWorld.m22) * extents.z);
                worldExtents += Vector3.one * 0.002f;

                GetScaleRange(localToWorld, worldToLocal, out float distanceScale, out float maximumScale);

                shape.TypeOperationSmoothModifierStart.z = input.Smoothness * distanceScale;
                shape.MaterialModifierCountDistanceScaleBoundsScale.z = distanceScale;
                shape.MaterialModifierCountDistanceScaleBoundsScale.w = input.CanUseBoundsDistance != 0
                    ? math.saturate(distanceScale / maximumScale) : 0f;
                shape.BoundsMin = worldCenter - worldExtents;
                shape.BoundsMax = worldCenter + worldExtents;
                Shapes[index] = shape;
            }

            private static void GetScaleRange(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, out float minimum, out float maximum)
            {
                float3 x = new float3(localToWorld.m00, localToWorld.m10, localToWorld.m20);
                float3 y = new float3(localToWorld.m01, localToWorld.m11, localToWorld.m21);
                float3 z = new float3(localToWorld.m02, localToWorld.m12, localToWorld.m22);
                float scaleX = math.length(x);
                float scaleY = math.length(y);
                float scaleZ = math.length(z);
                float orthogonality = math.max(
                    math.abs(math.dot(x, y)) / math.max(0.000001f, scaleX * scaleY),
                    math.max(
                        math.abs(math.dot(x, z)) / math.max(0.000001f, scaleX * scaleZ),
                        math.abs(math.dot(y, z)) / math.max(0.000001f, scaleY * scaleZ)));
                if (orthogonality < 0.0001f)
                {
                    minimum = math.max(0.000001f, math.min(scaleX, math.min(scaleY, scaleZ)));
                    maximum = math.max(0.000001f, math.max(scaleX, math.max(scaleY, scaleZ)));
                    return;
                }

                // For a sheared hierarchy, column lengths are not singular values.
                // Matrix norm bounds keep distance stepping conservative without an
                // expensive singular-value decomposition in every transform job.
                float worldNormOne = math.max(
                    math.abs(localToWorld.m00) + math.abs(localToWorld.m10) + math.abs(localToWorld.m20),
                    math.max(
                        math.abs(localToWorld.m01) + math.abs(localToWorld.m11) + math.abs(localToWorld.m21),
                        math.abs(localToWorld.m02) + math.abs(localToWorld.m12) + math.abs(localToWorld.m22)));
                float worldNormInfinity = math.max(
                    math.abs(localToWorld.m00) + math.abs(localToWorld.m01) + math.abs(localToWorld.m02),
                    math.max(
                        math.abs(localToWorld.m10) + math.abs(localToWorld.m11) + math.abs(localToWorld.m12),
                        math.abs(localToWorld.m20) + math.abs(localToWorld.m21) + math.abs(localToWorld.m22)));
                float inverseNormOne = math.max(
                    math.abs(worldToLocal.m00) + math.abs(worldToLocal.m10) + math.abs(worldToLocal.m20),
                    math.max(
                        math.abs(worldToLocal.m01) + math.abs(worldToLocal.m11) + math.abs(worldToLocal.m21),
                        math.abs(worldToLocal.m02) + math.abs(worldToLocal.m12) + math.abs(worldToLocal.m22)));
                float inverseNormInfinity = math.max(
                    math.abs(worldToLocal.m00) + math.abs(worldToLocal.m01) + math.abs(worldToLocal.m02),
                    math.max(
                        math.abs(worldToLocal.m10) + math.abs(worldToLocal.m11) + math.abs(worldToLocal.m12),
                        math.abs(worldToLocal.m20) + math.abs(worldToLocal.m21) + math.abs(worldToLocal.m22)));
                minimum = math.max(0.000001f, math.rsqrt(math.max(0.000001f, inverseNormOne * inverseNormInfinity)));
                maximum = math.max(0.000001f, math.sqrt(math.max(0.000001f, worldNormOne * worldNormInfinity)));
            }
        }

        [BurstCompile]
        private struct PackModifierBoundsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> ModifiedShapeIndices;
            [ReadOnly] public NativeArray<ModifierGpu> Modifiers;
            [ReadOnly] public NativeArray<ShapeGpu> Shapes;
            [NativeDisableParallelForRestriction] public NativeArray<ShapeTransformInput> Inputs;

            public void Execute(int index)
            {
                int shapeIndex = ModifiedShapeIndices[index];
                ShapeGpu shape = Shapes[shapeIndex];
                ShapeTransformInput input = Inputs[shapeIndex];
                float3 center = input.BaseLocalCenter;
                float3 extents = input.BaseLocalExtents;
                int modifierStart = (int)shape.TypeOperationSmoothModifierStart.w;
                int modifierCount = (int)shape.MaterialModifierCountDistanceScaleBoundsScale.y;
                bool canUseBoundsDistance = true;
                bool infiniteRepeat = false;

                for (int modifierIndex = modifierStart; modifierIndex < modifierStart + modifierCount; ++modifierIndex)
                {
                    ModifierGpu modifier = Modifiers[modifierIndex];
                    int type = (int)modifier.TypeAxesAmount.x;
                    int axes = (int)modifier.TypeAxesAmount.y;
                    float amount = math.abs(modifier.TypeAxesAmount.z);
                    float3 axisMask = new float3((axes & 1) != 0 ? 1f : 0f, (axes & 2) != 0 ? 1f : 0f, (axes & 4) != 0 ? 1f : 0f);
                    canUseBoundsDistance &= type != 5 && type != 6 && type != 7 && type != 8;
                    infiniteRepeat |= type == 5;

                    if (type == 0 || type == 1)
                        extents += amount;
                    else if (type == 2)
                        extents += math.abs(new float3(modifier.Vector.x, modifier.Vector.y, modifier.Vector.z)) * axisMask;
                    else if (type == 3)
                    {
                        float3 offset = math.abs(new float3(modifier.Vector.x, modifier.Vector.y, modifier.Vector.z)) * axisMask;
                        if ((axes & 1) != 0) { extents.x += math.abs(center.x) + offset.x; center.x = 0f; }
                        if ((axes & 2) != 0) { extents.y += math.abs(center.y) + offset.y; center.y = 0f; }
                        if ((axes & 4) != 0) { extents.z += math.abs(center.z) + offset.z; center.z = 0f; }
                    }
                    else if (type == 4)
                        extents += math.abs(new float3(modifier.Vector.x, modifier.Vector.y, modifier.Vector.z)) *
                            new float3(modifier.Count.x, modifier.Count.y, modifier.Count.z) * axisMask;
                    else if (type == 6 || type == 7)
                    {
                        float radius = math.length(center) + math.length(extents);
                        center = float3.zero;
                        extents = radius;
                    }
                    else if (type == 8)
                    {
                        float radius = amount + math.abs(center.x) + extents.x;
                        center = new float3(0f, center.y, 0f);
                        extents = new float3(radius, extents.y, radius);
                    }
                    else if (type == 9)
                        extents += amount * axisMask;
                }

                if (infiniteRepeat)
                {
                    center = input.ClipCenter;
                    extents = input.ClipExtents;
                }
                input.LocalCenter = center;
                input.LocalExtents = extents;
                input.CanUseBoundsDistance = canUseBoundsDistance ? 1 : 0;
                Inputs[shapeIndex] = input;
            }
        }

        [BurstCompile]
        private struct PackModelsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ModelTransformInput> Inputs;
            [ReadOnly] public NativeArray<ShapeGpu> Shapes;
            public NativeArray<ModelGpu> Models;

            public void Execute(int index)
            {
                ModelTransformInput input = Inputs[index];
                Vector3 minimum = default;
                Vector3 maximum = default;
                bool hasBounds = false;
                int end = input.ShapeStart + input.ShapeCount;
                for (int shapeIndex = input.ShapeStart; shapeIndex < end; ++shapeIndex)
                {
                    ShapeGpu shape = Shapes[shapeIndex];
                    Vector3 shapeMinimum = shape.BoundsMin;
                    Vector3 shapeMaximum = shape.BoundsMax;
                    SDFOperationType operation = (SDFOperationType)(int)shape.TypeOperationSmoothModifierStart.y;
                    if (!hasBounds)
                    {
                        minimum = shapeMinimum;
                        maximum = shapeMaximum;
                        hasBounds = true;
                    }
                    else if (operation == SDFOperationType.Union || operation == SDFOperationType.SmoothUnion)
                    {
                        minimum = new Vector3(math.min(minimum.x, shapeMinimum.x), math.min(minimum.y, shapeMinimum.y), math.min(minimum.z, shapeMinimum.z));
                        maximum = new Vector3(math.max(maximum.x, shapeMaximum.x), math.max(maximum.y, shapeMaximum.y), math.max(maximum.z, shapeMaximum.z));
                        if (operation == SDFOperationType.SmoothUnion)
                        {
                            float smoothness = shape.TypeOperationSmoothModifierStart.z;
                            minimum -= Vector3.one * smoothness;
                            maximum += Vector3.one * smoothness;
                        }
                    }
                }

                float padding = math.max(0f, input.Padding);
                minimum -= Vector3.one * padding;
                maximum += Vector3.one * padding;
                Models[index] = new ModelGpu
                {
                    BoundsMinAndShapeStart = new Vector4(minimum.x, minimum.y, minimum.z, input.ShapeStart),
                    BoundsMaxAndShapeCount = new Vector4(maximum.x, maximum.y, maximum.z,
                        input.RenderInSceneView != 0 ? input.ShapeCount : -input.ShapeCount)
                };
            }
        }

        private readonly List<ModelGpu> m_Models = new List<ModelGpu>(128);
        private readonly List<ShapeGpu> m_Shapes = new List<ShapeGpu>(256);
        private readonly List<ModifierGpu> m_Modifiers = new List<ModifierGpu>(128);
        private readonly List<MaterialGpu> m_Materials = new List<MaterialGpu>(64);
        private readonly List<SDFModel> m_ModelScratch = new List<SDFModel>(128);
        private readonly List<SDFShape> m_ShapeScratch = new List<SDFShape>(32);
        private readonly List<SDFModifier> m_ModifierScratch = new List<SDFModifier>(8);
        private readonly List<SDFModifier> m_ModifierBindings = new List<SDFModifier>(128);
        private readonly List<ShapeBinding> m_ShapeBindings = new List<ShapeBinding>(256);
        private readonly List<ModelBinding> m_ModelBindings = new List<ModelBinding>(128);
        private readonly List<int> m_OperationShapeIndices = new List<int>(128);
        private readonly List<int> m_ModifiedShapeIndices = new List<int>(128);
        private readonly Dictionary<SDFMaterialAsset, int> m_MaterialLookup = new Dictionary<SDFMaterialAsset, int>();
        private readonly List<Texture2D> m_Textures = new List<Texture2D>(16);
        private readonly Dictionary<Texture2D, int> m_TextureLookup = new Dictionary<Texture2D, int>();
        private TransformAccessArray m_ShapeTransforms;
        private NativeArray<ShapeTransformInput> m_ShapeTransformInputs;
        private NativeArray<ModelTransformInput> m_ModelTransformInputs;
        private NativeArray<ShapeGpu> m_DynamicShapes;
        private NativeArray<ModelGpu> m_DynamicModels;
        private NativeArray<ModifierGpu> m_DynamicModifiers;
        private NativeArray<int> m_DynamicModifiedShapeIndices;

        private const int DynamicBufferCount = 3;
        private readonly GraphicsBuffer[] m_ModelBuffers = new GraphicsBuffer[DynamicBufferCount];
        private readonly GraphicsBuffer[] m_ShapeBuffers = new GraphicsBuffer[DynamicBufferCount];
        private readonly GraphicsBuffer[] m_ModifierBuffers = new GraphicsBuffer[DynamicBufferCount];
        private GraphicsBuffer m_MaterialBuffer;
        private int m_ModelCapacity;
        private int m_ShapeCapacity;
        private int m_ModifierCapacity;
        private int m_MaterialCapacity;
        private int m_DynamicBufferIndex;
        private int m_ModifierBufferIndex;
        private uint m_CompiledVersion;
        private int m_LastTransformFrame = int.MinValue;

        internal int ModelCount => m_Models.Count;
        internal GraphicsBuffer ModelBuffer => m_ModelBuffers[m_DynamicBufferIndex];
        internal GraphicsBuffer ShapeBuffer => m_ShapeBuffers[m_DynamicBufferIndex];
        internal GraphicsBuffer ModifierBuffer => m_ModifierBuffers[m_ModifierBufferIndex];
        internal GraphicsBuffer MaterialBuffer => m_MaterialBuffer;
        internal IReadOnlyList<Texture2D> Textures => m_Textures;

        internal void UpdateIfNeeded()
        {
            if (Time.frameCount != m_LastTransformFrame)
            {
                m_LastTransformFrame = Time.frameCount;
                SDFDirtyFlags pending = SDFSceneRegistry.GetDirtyFlagsSince(m_CompiledVersion);
                if ((pending & SDFDirtyFlags.Transforms) == 0)
                    SDFSceneRegistry.CheckForTransformChanges();
            }
            if (m_CompiledVersion == SDFSceneRegistry.Version)
                return;
            SDFDirtyFlags dirty = SDFSceneRegistry.GetDirtyFlagsSince(m_CompiledVersion);
            if (ModelBuffer == null || ShapeBuffer == null || ModifierBuffer == null || m_MaterialBuffer == null)
            {
                using (CompileMarker.Auto())
                    Compile();
            }
            else if ((dirty & ~(SDFDirtyFlags.Materials | SDFDirtyFlags.Settings | SDFDirtyFlags.Transforms |
                SDFDirtyFlags.Operations | SDFDirtyFlags.Modifiers)) == 0)
            {
                if ((dirty & SDFDirtyFlags.Materials) != 0 && m_Materials.Count > 0)
                    RefreshMaterials();
                bool modifiersChanged = (dirty & SDFDirtyFlags.Modifiers) != 0;
                bool operationsChanged = (dirty & SDFDirtyFlags.Operations) != 0;
                JobHandle modifierBoundsHandle = default;
                if (operationsChanged)
                {
                    using (OperationMarker.Auto())
                        RefreshOperations();
                }
                if (modifiersChanged)
                {
                    using (ModifierMarker.Auto())
                        modifierBoundsHandle = RefreshModifiers();
                }
                if (((dirty & SDFDirtyFlags.Transforms) != 0 || modifiersChanged || operationsChanged) && m_Shapes.Count > 0)
                {
                    using (TransformMarker.Auto())
                        RefreshDynamicData((dirty & SDFDirtyFlags.Transforms) != 0 || modifiersChanged, modifierBoundsHandle);
                }
            }
            else
            {
                using (CompileMarker.Auto())
                    Compile();
            }
            m_CompiledVersion = SDFSceneRegistry.Version;
        }

        public void Dispose()
        {
            DisposeTransformJobs();
            Release(m_ModelBuffers);
            Release(m_ShapeBuffers);
            Release(m_ModifierBuffers);
            Release(ref m_MaterialBuffer);
            m_ModelCapacity = m_ShapeCapacity = m_ModifierCapacity = m_MaterialCapacity = 0;
        }

        private void Compile()
        {
            m_Models.Clear();
            m_Shapes.Clear();
            m_Modifiers.Clear();
            m_Materials.Clear();
            m_MaterialLookup.Clear();
            m_Textures.Clear();
            m_TextureLookup.Clear();
            m_ModelScratch.Clear();
            m_ShapeBindings.Clear();
            m_ModelBindings.Clear();
            m_ModifierBindings.Clear();
            m_OperationShapeIndices.Clear();
            m_ModifiedShapeIndices.Clear();

            AddDefaultMaterial();
            m_Textures.Add(Texture2D.whiteTexture);
            foreach (SDFModel model in SDFSceneRegistry.RegisteredModels)
            {
                if (model != null && model.isActiveAndEnabled && model.gameObject.activeInHierarchy)
                    m_ModelScratch.Add(model);
            }
            m_ModelScratch.Sort((left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));

            for (int modelIndex = 0; modelIndex < m_ModelScratch.Count; ++modelIndex)
                AppendModel(m_ModelScratch[modelIndex]);

            // Shapes without an SDFModel remain useful during incremental authoring.
            foreach (SDFShape shape in SDFSceneRegistry.RegisteredShapes)
            {
                if (!IsRenderable(shape) || shape.GetComponentInParent<SDFModel>() != null)
                    continue;
                int start = m_Shapes.Count;
                Bounds bounds = default;
                bool hasBounds = false;
                AppendShape(shape, ref bounds, ref hasBounds);
                if (hasBounds)
                {
                    AppendModelGpu(bounds, start, 1, 0.02f, true);
                    m_ModelBindings.Add(new ModelBinding { ShapeStart = start, ShapeCount = 1, Padding = 0.02f, RenderInSceneView = true });
                }
            }

            PrepareTransformJobs();

            using (UploadMarker.Auto())
            {
                EnsureBuffers(m_ModelBuffers, ref m_ModelCapacity, m_Models.Count, Marshal.SizeOf<ModelGpu>(), "SDF Models");
                EnsureBuffers(m_ShapeBuffers, ref m_ShapeCapacity, m_Shapes.Count, Marshal.SizeOf<ShapeGpu>(), "SDF Shapes");
                EnsureBuffers(m_ModifierBuffers, ref m_ModifierCapacity, m_Modifiers.Count, Marshal.SizeOf<ModifierGpu>(), "SDF Modifiers");
                EnsureBuffer(ref m_MaterialBuffer, ref m_MaterialCapacity, m_Materials.Count, Marshal.SizeOf<MaterialGpu>(), "SDF Materials");
                if (m_Models.Count > 0) ModelBuffer.SetData(m_Models);
                if (m_Shapes.Count > 0) ShapeBuffer.SetData(m_Shapes);
                if (m_Modifiers.Count > 0) ModifierBuffer.SetData(m_Modifiers);
                if (m_Materials.Count > 0) m_MaterialBuffer.SetData(m_Materials);
            }
        }

        private void AppendModel(SDFModel model)
        {
            m_ShapeScratch.Clear();
            model.GetComponentsInChildren(false, m_ShapeScratch);
            int start = m_Shapes.Count;
            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < m_ShapeScratch.Count; ++i)
            {
                SDFShape shape = m_ShapeScratch[i];
                if (IsRenderable(shape) && shape.GetComponentInParent<SDFModel>() == model)
                    AppendShape(shape, ref bounds, ref hasBounds);
            }
            if (hasBounds)
            {
                AppendModelGpu(bounds, start, m_Shapes.Count - start, model.BoundsPadding, model.RenderInSceneView);
                m_ModelBindings.Add(new ModelBinding
                {
                    ShapeStart = start,
                    ShapeCount = m_Shapes.Count - start,
                    Padding = model.BoundsPadding,
                    RenderInSceneView = model.RenderInSceneView
                });
            }
        }

        private void AppendShape(SDFShape shape, ref Bounds modelBounds, ref bool hasModelBounds)
        {
            int shapeIndex = m_Shapes.Count;
            m_ModifierScratch.Clear();
            shape.GetComponents(m_ModifierScratch);
            int modifierStart = m_Modifiers.Count;
            bool canUseBoundsDistance = true;
            bool infiniteRepeat = false;
            Bounds baseLocalBounds = shape.GetLocalBounds();
            Bounds localBounds = baseLocalBounds;

            for (int i = 0; i < m_ModifierScratch.Count; ++i)
            {
                SDFModifier modifier = m_ModifierScratch[i];
                if (modifier == null || !modifier.isActiveAndEnabled)
                    continue;
                canUseBoundsDistance &= !modifier.InvalidatesBoundsDistance;
                infiniteRepeat |= modifier.Type == SDFModifierType.InfiniteRepeat;
                ExpandBoundsForModifier(ref localBounds, modifier);
                m_Modifiers.Add(new ModifierGpu { TypeAxesAmount = modifier.PackA(), Vector = modifier.PackB(), Count = modifier.PackC() });
                m_ModifierBindings.Add(modifier);
            }
            if (infiniteRepeat)
                localBounds = shape.ClipBounds;

            Bounds worldBounds = TransformBounds(shape.transform.localToWorldMatrix, localBounds);
            worldBounds.Expand(0.004f);
            shape.GetScaleRange(out float distanceScale, out float maximumScale);
            float boundsScale = canUseBoundsDistance ? Mathf.Clamp01(distanceScale / maximumScale) : 0f;
            SDFOperation operation = shape.GetComponent<SDFOperation>();
            SDFOperationType operationType = operation != null && operation.isActiveAndEnabled ? operation.Type : SDFOperationType.Union;
            float smoothness = operation != null ? operation.Smoothness * distanceScale : 0f;
            SDFCustomMaterial customMaterial = shape.GetComponent<SDFCustomMaterial>();
            SDFMaterialAsset material = customMaterial != null && customMaterial.isActiveAndEnabled && customMaterial.Material != null
                ? customMaterial.Material : shape.Material;
            int materialIndex = GetMaterialIndex(material);
            Matrix4x4 worldToLocal = shape.transform.worldToLocalMatrix;
            shape.GetPackedParameters(out Vector4 p0, out Vector4 p1, out Vector4 p2, out Vector4 p3);
            m_Shapes.Add(new ShapeGpu
            {
                WorldToLocal0 = new Vector4(worldToLocal.m00, worldToLocal.m01, worldToLocal.m02, worldToLocal.m03),
                WorldToLocal1 = new Vector4(worldToLocal.m10, worldToLocal.m11, worldToLocal.m12, worldToLocal.m13),
                WorldToLocal2 = new Vector4(worldToLocal.m20, worldToLocal.m21, worldToLocal.m22, worldToLocal.m23),
                Parameters0 = p0,
                Parameters1 = p1,
                Parameters2 = p2,
                Parameters3 = p3,
                TypeOperationSmoothModifierStart = new Vector4((float)shape.ShapeType, (float)operationType, smoothness, modifierStart),
                MaterialModifierCountDistanceScaleBoundsScale = new Vector4(materialIndex, m_Modifiers.Count - modifierStart, distanceScale, boundsScale),
                BoundsMin = worldBounds.min,
                BoundsMax = worldBounds.max
            });
            m_ShapeBindings.Add(new ShapeBinding
            {
                Shape = shape,
                Operation = operation,
                BaseLocalBounds = baseLocalBounds,
                LocalBounds = localBounds,
                OperationType = operationType,
                Smoothness = operation != null ? operation.Smoothness : 0f,
                CanUseBoundsDistance = canUseBoundsDistance
            });
            if (operation != null) m_OperationShapeIndices.Add(shapeIndex);
            if (m_Modifiers.Count > modifierStart) m_ModifiedShapeIndices.Add(shapeIndex);
            shape.transform.hasChanged = false;

            if (!hasModelBounds)
            {
                modelBounds = worldBounds;
                hasModelBounds = true;
            }
            else if (operationType == SDFOperationType.Union || operationType == SDFOperationType.SmoothUnion)
            {
                modelBounds.Encapsulate(worldBounds);
                if (operationType == SDFOperationType.SmoothUnion)
                    modelBounds.Expand(smoothness * 2f);
            }
        }

        private void AppendModelGpu(Bounds bounds, int start, int count, float padding, bool renderInSceneView)
        {
            bounds.Expand(Mathf.Max(0f, padding) * 2f);
            m_Models.Add(new ModelGpu
            {
                BoundsMinAndShapeStart = new Vector4(bounds.min.x, bounds.min.y, bounds.min.z, start),
                BoundsMaxAndShapeCount = new Vector4(bounds.max.x, bounds.max.y, bounds.max.z, renderInSceneView ? count : -count)
            });
        }

        private int GetMaterialIndex(SDFMaterialAsset material)
        {
            if (material == null)
                return 0;
            if (m_MaterialLookup.TryGetValue(material, out int index))
                return index;
            index = m_Materials.Count;
            m_MaterialLookup.Add(material, index);
            m_Materials.Add(CreateMaterialGpu(material));
            return index;
        }

        private void AddDefaultMaterial()
        {
            m_Materials.Add(DefaultMaterialGpu());
        }

        private static MaterialGpu DefaultMaterialGpu() => new MaterialGpu
        {
            BaseColor = new Vector4(0.65f, 0.72f, 0.82f, 1f),
            SpecularAndPower = new Vector4(1f, 1f, 1f, 48f),
            EmissionAndCelBands = new Vector4(0f, 0f, 0f, 3f),
            ModelMetallicSmoothness = new Vector4((float)SDFShadingModel.PbrLike, 0f, 0.5f, 1f),
            Custom0 = Vector4.zero,
            Custom1 = Vector4.zero,
            CustomShaderTextureIndices = new Vector4(-1f, 0f, 0f, 0f)
        };

        private MaterialGpu CreateMaterialGpu(SDFMaterialAsset material)
        {
            if (material == null)
                return DefaultMaterialGpu();
            Color baseColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? material.BaseColor.linear : material.BaseColor;
            Color specular = QualitySettings.activeColorSpace == ColorSpace.Linear ? material.SpecularColor.linear : material.SpecularColor;
            Color emission = QualitySettings.activeColorSpace == ColorSpace.Linear ? material.Emission.linear : material.Emission;
            return new MaterialGpu
            {
                BaseColor = baseColor,
                SpecularAndPower = new Vector4(specular.r, specular.g, specular.b, material.SpecularPower),
                EmissionAndCelBands = new Vector4(emission.r, emission.g, emission.b, material.CelBands),
                ModelMetallicSmoothness = new Vector4((float)material.ShadingModel, material.Metallic, material.Smoothness, material.Occlusion),
                Custom0 = material.Custom0,
                Custom1 = material.Custom1,
                CustomShaderTextureIndices = new Vector4(material.CustomShader != null ? SDFCustomShaderRegistry.Resolve(material.CustomShader.StableId) : -1, GetTextureIndex(material.BaseMap), 0f, 0f)
            };
        }

        private void RefreshMaterials()
        {
            using (CompileMarker.Auto())
            {
                m_Textures.Clear();
                m_TextureLookup.Clear();
                m_Textures.Add(Texture2D.whiteTexture);
                m_Materials.Clear();
                AddDefaultMaterial();
                for (int i = 0; i < m_MaterialLookup.Count; ++i)
                    m_Materials.Add(DefaultMaterialGpu());
                foreach (KeyValuePair<SDFMaterialAsset, int> pair in m_MaterialLookup)
                    if (pair.Value > 0 && pair.Value < m_Materials.Count)
                        m_Materials[pair.Value] = CreateMaterialGpu(pair.Key);
            }
            using (UploadMarker.Auto())
            {
                EnsureBuffer(ref m_MaterialBuffer, ref m_MaterialCapacity, m_Materials.Count, Marshal.SizeOf<MaterialGpu>(), "SDF Materials");
                m_MaterialBuffer.SetData(m_Materials);
            }
        }

        private void RefreshOperations()
        {
            for (int operationIndex = 0; operationIndex < m_OperationShapeIndices.Count; ++operationIndex)
            {
                int i = m_OperationShapeIndices[operationIndex];
                ShapeBinding binding = m_ShapeBindings[i];
                SDFOperation operation = binding.Operation;
                bool active = operation != null && operation.isActiveAndEnabled;
                binding.OperationType = active ? operation.Type : SDFOperationType.Union;
                binding.Smoothness = active ? operation.Smoothness : 0f;
                m_ShapeBindings[i] = binding;

                ShapeTransformInput input = m_ShapeTransformInputs[i];
                input.Smoothness = binding.Smoothness;
                m_ShapeTransformInputs[i] = input;

                ShapeGpu shape = m_DynamicShapes[i];
                shape.TypeOperationSmoothModifierStart.y = (float)binding.OperationType;
                shape.TypeOperationSmoothModifierStart.z = binding.Smoothness * shape.MaterialModifierCountDistanceScaleBoundsScale.z;
                m_DynamicShapes[i] = shape;
            }
        }

        private JobHandle RefreshModifiers()
        {
            for (int i = 0; i < m_ModifierBindings.Count; ++i)
            {
                SDFModifier modifier = m_ModifierBindings[i];
                if (modifier == null)
                    continue;
                m_Modifiers[i] = new ModifierGpu
                {
                    TypeAxesAmount = modifier.PackA(),
                    Vector = modifier.PackB(),
                    Count = modifier.PackC()
                };
                m_DynamicModifiers[i] = m_Modifiers[i];
            }

            using (UploadMarker.Auto())
            {
                m_ModifierBufferIndex = (m_ModifierBufferIndex + 1) % DynamicBufferCount;
                if (m_Modifiers.Count > 0) ModifierBuffer.SetData(m_Modifiers);
            }
            if (m_DynamicModifiedShapeIndices.Length == 0)
                return default;
            PackModifierBoundsJob boundsJob = new PackModifierBoundsJob
            {
                ModifiedShapeIndices = m_DynamicModifiedShapeIndices,
                Modifiers = m_DynamicModifiers,
                Shapes = m_DynamicShapes,
                Inputs = m_ShapeTransformInputs
            };
            return boundsJob.Schedule(m_DynamicModifiedShapeIndices.Length, 64);
        }

        private void RefreshDynamicData(bool packShapes, JobHandle dependency)
        {
            JobHandle shapesHandle = dependency;
            if (packShapes)
            {
                PackShapesJob shapesJob = new PackShapesJob
                {
                    Inputs = m_ShapeTransformInputs,
                    Shapes = m_DynamicShapes
                };
                shapesHandle = shapesJob.ScheduleReadOnly(m_ShapeTransforms, 64, dependency);
            }
            PackModelsJob modelsJob = new PackModelsJob
            {
                Inputs = m_ModelTransformInputs,
                Shapes = m_DynamicShapes,
                Models = m_DynamicModels
            };
            JobHandle modelsHandle = modelsJob.Schedule(m_DynamicModels.Length, 64, shapesHandle);
            modelsHandle.Complete();

            using (UploadMarker.Auto())
            {
                m_DynamicBufferIndex = (m_DynamicBufferIndex + 1) % DynamicBufferCount;
                ShapeBuffer.SetData(m_DynamicShapes);
                ModelBuffer.SetData(m_DynamicModels);
            }
        }

        private void PrepareTransformJobs()
        {
            DisposeTransformJobs();
            if (m_ShapeBindings.Count == 0 || m_ModelBindings.Count == 0)
                return;

            m_ShapeTransforms = new TransformAccessArray(m_ShapeBindings.Count);
            m_ShapeTransformInputs = new NativeArray<ShapeTransformInput>(m_ShapeBindings.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_ModelTransformInputs = new NativeArray<ModelTransformInput>(m_ModelBindings.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_DynamicShapes = new NativeArray<ShapeGpu>(m_Shapes.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_DynamicModels = new NativeArray<ModelGpu>(m_Models.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_DynamicModifiers = new NativeArray<ModifierGpu>(m_Modifiers.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_DynamicModifiedShapeIndices = new NativeArray<int>(m_ModifiedShapeIndices.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < m_ShapeBindings.Count; ++i)
            {
                ShapeBinding binding = m_ShapeBindings[i];
                m_ShapeTransforms.Add(binding.Shape.transform);
                m_ShapeTransformInputs[i] = new ShapeTransformInput
                {
                    BaseLocalCenter = binding.BaseLocalBounds.center,
                    BaseLocalExtents = binding.BaseLocalBounds.extents,
                    LocalCenter = binding.LocalBounds.center,
                    LocalExtents = binding.LocalBounds.extents,
                    ClipCenter = binding.Shape.ClipBounds.center,
                    ClipExtents = binding.Shape.ClipBounds.extents,
                    Smoothness = binding.Smoothness,
                    CanUseBoundsDistance = binding.CanUseBoundsDistance ? 1 : 0
                };
                m_DynamicShapes[i] = m_Shapes[i];
            }

            for (int i = 0; i < m_ModelBindings.Count; ++i)
            {
                ModelBinding binding = m_ModelBindings[i];
                m_ModelTransformInputs[i] = new ModelTransformInput
                {
                    ShapeStart = binding.ShapeStart,
                    ShapeCount = binding.ShapeCount,
                    Padding = binding.Padding,
                    RenderInSceneView = binding.RenderInSceneView ? 1 : 0
                };
                m_DynamicModels[i] = m_Models[i];
            }
            for (int i = 0; i < m_Modifiers.Count; ++i)
                m_DynamicModifiers[i] = m_Modifiers[i];
            for (int i = 0; i < m_ModifiedShapeIndices.Count; ++i)
                m_DynamicModifiedShapeIndices[i] = m_ModifiedShapeIndices[i];
        }

        private void DisposeTransformJobs()
        {
            if (m_ShapeTransforms.isCreated) m_ShapeTransforms.Dispose();
            if (m_ShapeTransformInputs.IsCreated) m_ShapeTransformInputs.Dispose();
            if (m_ModelTransformInputs.IsCreated) m_ModelTransformInputs.Dispose();
            if (m_DynamicShapes.IsCreated) m_DynamicShapes.Dispose();
            if (m_DynamicModels.IsCreated) m_DynamicModels.Dispose();
            if (m_DynamicModifiers.IsCreated) m_DynamicModifiers.Dispose();
            if (m_DynamicModifiedShapeIndices.IsCreated) m_DynamicModifiedShapeIndices.Dispose();
        }

        private int GetTextureIndex(Texture2D texture)
        {
            if (texture == null)
                return 0;
            if (m_TextureLookup.TryGetValue(texture, out int existing))
                return existing;
            if (m_Textures.Count >= SDFShaderIds.Textures.Length)
            {
                Debug.LogWarning($"SDF supports {SDFShaderIds.Textures.Length} simultaneously bound 2D textures per scene batch. '{texture.name}' uses white fallback.", texture);
                return 0;
            }
            int index = m_Textures.Count;
            m_Textures.Add(texture);
            m_TextureLookup.Add(texture, index);
            return index;
        }

        private static bool IsRenderable(SDFShape shape) => shape != null && shape.isActiveAndEnabled && shape.gameObject.activeInHierarchy;

        private static void ExpandBoundsForModifier(ref Bounds bounds, SDFModifier modifier)
        {
            switch (modifier.Type)
            {
                case SDFModifierType.Round:
                case SDFModifierType.Onion:
                    bounds.Expand(Mathf.Abs(modifier.Amount) * 2f);
                    break;
                case SDFModifierType.Elongate:
                    bounds.Expand(Vector3.Scale(Abs(modifier.Vector), AxesMask(modifier.Axes)) * 2f);
                    break;
                case SDFModifierType.Mirror:
                    Vector3 mirrorOffset = Vector3.Scale(Abs(modifier.Vector), AxesMask(modifier.Axes));
                    Vector3 mirrorCenter = bounds.center;
                    Vector3 mirrorExtents = bounds.extents;
                    if ((modifier.Axes & SDFModifierAxes.X) != 0) { mirrorExtents.x += Mathf.Abs(mirrorCenter.x) + mirrorOffset.x; mirrorCenter.x = 0f; }
                    if ((modifier.Axes & SDFModifierAxes.Y) != 0) { mirrorExtents.y += Mathf.Abs(mirrorCenter.y) + mirrorOffset.y; mirrorCenter.y = 0f; }
                    if ((modifier.Axes & SDFModifierAxes.Z) != 0) { mirrorExtents.z += Mathf.Abs(mirrorCenter.z) + mirrorOffset.z; mirrorCenter.z = 0f; }
                    bounds.center = mirrorCenter;
                    bounds.extents = mirrorExtents;
                    break;
                case SDFModifierType.FiniteRepeat:
                    Vector3 repetition = Vector3.Scale(Abs(modifier.Vector), new Vector3(modifier.Count.x, modifier.Count.y, modifier.Count.z));
                    bounds.Expand(Vector3.Scale(repetition, AxesMask(modifier.Axes)) * 2f);
                    break;
                case SDFModifierType.Twist:
                case SDFModifierType.Bend:
                    float radius = bounds.center.magnitude + bounds.extents.magnitude;
                    bounds.center = Vector3.zero;
                    bounds.extents = Vector3.one * radius;
                    break;
                case SDFModifierType.Revolution:
                    float revolutionRadius = Mathf.Abs(modifier.Amount) + Mathf.Abs(bounds.center.x) + bounds.extents.x;
                    bounds.center = new Vector3(0f, bounds.center.y, 0f);
                    bounds.extents = new Vector3(revolutionRadius, bounds.extents.y, revolutionRadius);
                    break;
                case SDFModifierType.Extrusion:
                    bounds.Expand(Vector3.Scale(Vector3.one * Mathf.Abs(modifier.Amount), AxesMask(modifier.Axes)) * 2f);
                    break;
            }
        }

        private static Vector3 AxesMask(SDFModifierAxes axes) => new Vector3((axes & SDFModifierAxes.X) != 0 ? 1f : 0f, (axes & SDFModifierAxes.Y) != 0 ? 1f : 0f, (axes & SDFModifierAxes.Z) != 0 ? 1f : 0f);
        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds local)
        {
            Vector3 center = matrix.MultiplyPoint3x4(local.center);
            Vector3 e = local.extents;
            Vector3 x = matrix.MultiplyVector(new Vector3(e.x, 0f, 0f));
            Vector3 y = matrix.MultiplyVector(new Vector3(0f, e.y, 0f));
            Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, e.z));
            Vector3 worldExtents = Abs(x) + Abs(y) + Abs(z);
            return new Bounds(center, worldExtents * 2f);
        }

        private static void EnsureBuffer(ref GraphicsBuffer buffer, ref int capacity, int count, int stride, string name)
        {
            int required = Mathf.Max(1, count);
            if (buffer != null && capacity >= required)
                return;
            Release(ref buffer);
            capacity = Mathf.NextPowerOfTwo(required);
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, stride) { name = name };
        }

        private static void EnsureBuffers(GraphicsBuffer[] buffers, ref int capacity, int count, int stride, string name)
        {
            int required = Mathf.Max(1, count);
            if (buffers[0] != null && capacity >= required)
                return;
            Release(buffers);
            capacity = Mathf.NextPowerOfTwo(required);
            for (int i = 0; i < buffers.Length; ++i)
                buffers[i] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, stride) { name = name + " " + i };
        }

        private static void Release(ref GraphicsBuffer buffer)
        {
            buffer?.Release();
            buffer = null;
        }

        private static void Release(GraphicsBuffer[] buffers)
        {
            for (int i = 0; i < buffers.Length; ++i)
            {
                buffers[i]?.Release();
                buffers[i] = null;
            }
        }
    }
}
