using System;
using System.Collections;
using System.Text;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Serialization;

namespace SdfRenderer
{
    [Flags]
    public enum SDFBenchmarkAnimation
    {
        None = 0,
        Positions = 1 << 0,
        Rotations = 1 << 1,
        Scales = 1 << 2,
        Materials = 1 << 3,
        Operations = 1 << 4,
        Modifiers = 1 << 5,
        Everything = Positions | Rotations | Scales | Materials | Operations | Modifiers
    }

    [ExecuteAlways]
    public sealed class SDFBenchmarkController : MonoBehaviour
    {
        private static readonly ProfilerMarker AnimationMarker = new ProfilerMarker("SDF/Benchmark Animate Transforms");
        private static readonly ProfilerMarker OperationAnimationMarker = new ProfilerMarker("SDF/Benchmark Animate Operations");
        private static readonly ProfilerMarker ModifierAnimationMarker = new ProfilerMarker("SDF/Benchmark Animate Modifiers");

        [BurstCompile]
        private struct AnimateTransformsJob : IJobParallelForTransform
        {
            public float Time;
            public float Spacing;
            public int Width;
            public int AnimatePositions;
            public int AnimateRotations;
            public int AnimateScales;
            public int UpdatePositions;
            public int UpdateRotations;
            public int UpdateScales;

            public void Execute(int index, TransformAccess transform)
            {
                float phase = Time + index * 0.173f;
                Vector3 position = default;
                Quaternion rotation = default;
                if (UpdatePositions != 0)
                {
                    position = new Vector3(
                        (index % Width - Width * 0.5f) * Spacing,
                        AnimatePositions != 0 ? math.sin(phase * 1.7f) * 0.35f : 0f,
                        (index / Width - Width * 0.5f) * Spacing);
                }
                if (UpdateRotations != 0)
                {
                    if (AnimateRotations != 0)
                    {
                        float3 degrees = new float3(math.sin(phase * 0.9f) * 35f, phase * 55f, math.cos(phase * 1.1f) * 20f);
                        quaternion value = quaternion.EulerZXY(math.radians(degrees));
                        rotation = new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
                    }
                    else
                    {
                        rotation = Quaternion.identity;
                    }
                }

                if (UpdatePositions != 0 && UpdateRotations != 0)
                    transform.SetLocalPositionAndRotation(position, rotation);
                else if (UpdatePositions != 0)
                    transform.localPosition = position;
                else if (UpdateRotations != 0)
                    transform.localRotation = rotation;
                if (UpdateScales != 0)
                {
                    float scale = AnimateScales != 0 ? 1f + math.sin(phase * 1.3f) * 0.28f : 1f;
                    transform.localScale = Vector3.one * scale;
                }
            }
        }

        [SerializeField, Range(1, 10000)] private int m_ModelCount = 256;
        [SerializeField, Min(0.1f)] private float m_Spacing = 2.5f;
        [SerializeField, FormerlySerializedAs("m_Animate")] private SDFBenchmarkAnimation m_Animation = SDFBenchmarkAnimation.Positions;
        [SerializeField] private bool m_IncludeOperations = true;
        [SerializeField] private bool m_IncludeModifiers = true;
        [SerializeField, Range(1, 32)] private int m_MaterialCount = 8;
        [SerializeField, Min(0f)] private float m_AnimationSpeed = 1f;
        [SerializeField] private bool m_PreviewInEditMode;
        [Header("Automated measurement")]
        [SerializeField] private bool m_RunSweepOnStart;
        [SerializeField, Min(1)] private int m_SweepWarmupFrames = 60;
        [SerializeField, Min(1)] private int m_SweepSampleFrames = 180;
        private Transform m_GeneratedRoot;
        private TransformAccessArray m_Transforms;
        private SDFMaterialAsset[] m_Materials;
        private SDFOperation[] m_Operations;
        private SDFModifier[] m_Modifiers;
        private bool m_SweepRunning;

        private int m_BuiltCount = -1;
        private int m_BuiltMaterialCount = -1;
        private float m_BuiltSpacing = -1f;
        private bool m_BuiltOperations;
        private bool m_BuiltModifiers;
        private SDFBenchmarkAnimation m_PreviousAnimation;

        public SDFBenchmarkAnimation Animation { get => m_Animation; set => m_Animation = value; }
        public bool PreviewInEditMode { get => m_PreviewInEditMode; set => m_PreviewInEditMode = value; }

        private void OnEnable() => RebuildIfNeeded();
        private void Start()
        {
            if (Application.isPlaying && m_RunSweepOnStart)
                StartCoroutine(RunBenchmarkSweep());
        }
        private void OnDisable()
        {
            m_SweepRunning = false;
            DestroyGenerated();
        }
        private void OnValidate()
        {
            m_ModelCount = Mathf.Clamp(m_ModelCount, 1, 10000);
            m_MaterialCount = Mathf.Clamp(m_MaterialCount, 1, 32);
            m_AnimationSpeed = Mathf.Max(0f, m_AnimationSpeed);
            m_SweepWarmupFrames = Mathf.Max(1, m_SweepWarmupFrames);
            m_SweepSampleFrames = Mathf.Max(1, m_SweepSampleFrames);
        }

        [ContextMenu("Run Benchmark Sweep")]
        public void BeginBenchmarkSweep()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before running the SDF benchmark sweep.", this);
                return;
            }
            if (m_SweepRunning)
                return;
            StartCoroutine(RunBenchmarkSweep());
        }

        private IEnumerator RunBenchmarkSweep()
        {
            if (m_SweepRunning)
                yield break;
            m_SweepRunning = true;
            SDFBenchmarkAnimation original = m_Animation;
            var modes = new[]
            {
                SDFBenchmarkAnimation.None,
                SDFBenchmarkAnimation.Positions,
                SDFBenchmarkAnimation.Rotations,
                SDFBenchmarkAnimation.Scales,
                SDFBenchmarkAnimation.Positions | SDFBenchmarkAnimation.Rotations | SDFBenchmarkAnimation.Scales,
                SDFBenchmarkAnimation.Materials,
                SDFBenchmarkAnimation.Operations,
                SDFBenchmarkAnimation.Modifiers,
                SDFBenchmarkAnimation.Everything
            };
            var labels = new[]
            {
                "Static", "Positions", "Rotations", "Scales", "Transforms", "Materials",
                "Operations", "Modifiers", "Everything"
            };
            var timings = new FrameTiming[1];
            var report = new StringBuilder(1024);
            report.AppendLine($"SDF benchmark sweep: models={m_ModelCount}, operations={m_IncludeOperations}, " +
                $"modifiers={m_IncludeModifiers}, resolution={Screen.width}x{Screen.height}");
            report.AppendLine("Mode,FPS,CPU ms,GPU ms,Upload KiB/frame,Shapes/frame,Models/frame,Operations/frame,Modifiers/frame,Bounds/frame,Frames");

            for (int modeIndex = 0; modeIndex < modes.Length; ++modeIndex)
            {
                m_Animation = modes[modeIndex];
                for (int frame = 0; frame < m_SweepWarmupFrames; ++frame)
                    yield return null;

                double start = Time.realtimeSinceStartupAsDouble;
                double cpuTotal = 0.0;
                double gpuTotal = 0.0;
                int cpuSamples = 0;
                int gpuSamples = 0;
                long uploadTotal = 0L;
                long shapeTotal = 0L;
                long modelTotal = 0L;
                long operationTotal = 0L;
                long modifierTotal = 0L;
                long boundsTotal = 0L;
                for (int frame = 0; frame < m_SweepSampleFrames; ++frame)
                {
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    uint timingCount = FrameTimingManager.GetLatestTimings(1, timings);
                    if (timingCount > 0 && timings[0].cpuFrameTime > 0.0)
                    {
                        cpuTotal += timings[0].cpuFrameTime;
                        ++cpuSamples;
                    }
                    if (timingCount > 0 && timings[0].gpuFrameTime > 0.0)
                    {
                        gpuTotal += timings[0].gpuFrameTime;
                        ++gpuSamples;
                    }
                    SDFPerformanceSnapshot snapshot = SDFPerformanceMetrics.CurrentFrame;
                    uploadTotal += snapshot.UploadBytes;
                    shapeTotal += snapshot.ShapesRefreshed;
                    modelTotal += snapshot.ModelsRefreshed;
                    operationTotal += snapshot.OperationsRefreshed;
                    modifierTotal += snapshot.ModifiersRefreshed;
                    boundsTotal += snapshot.BoundsRefreshed;
                }
                double elapsed = Math.Max(0.000001, Time.realtimeSinceStartupAsDouble - start);
                double fps = m_SweepSampleFrames / elapsed;
                double cpu = cpuSamples > 0 ? cpuTotal / cpuSamples : 0.0;
                double gpu = gpuSamples > 0 ? gpuTotal / gpuSamples : 0.0;
                double samples = m_SweepSampleFrames;
                report.AppendLine($"{labels[modeIndex]},{fps:F2},{cpu:F3},{gpu:F3}," +
                    $"{uploadTotal / samples / 1024.0:F2},{shapeTotal / samples:F2},{modelTotal / samples:F2}," +
                    $"{operationTotal / samples:F2},{modifierTotal / samples:F2},{boundsTotal / samples:F2}," +
                    $"{m_SweepSampleFrames}");
            }

            m_Animation = original;
            m_SweepRunning = false;
            Debug.Log(report.ToString(), this);
        }

        private void Update()
        {
            RebuildIfNeeded();
            if (m_GeneratedRoot == null || !m_Transforms.isCreated)
                return;
            bool canAnimate = Application.isPlaying || m_PreviewInEditMode;
            SDFBenchmarkAnimation activeAnimation = canAnimate ? m_Animation : SDFBenchmarkAnimation.None;
            const SDFBenchmarkAnimation transformMask = SDFBenchmarkAnimation.Positions |
                SDFBenchmarkAnimation.Rotations | SDFBenchmarkAnimation.Scales;
            SDFBenchmarkAnimation activeTransforms = activeAnimation & transformMask;
            SDFBenchmarkAnimation disabledTransforms = (m_PreviousAnimation & transformMask) & ~activeTransforms;
            bool animateOperations = (activeAnimation & SDFBenchmarkAnimation.Operations) != 0;
            bool animateModifiers = (activeAnimation & SDFBenchmarkAnimation.Modifiers) != 0;
            bool resetOperations = (m_PreviousAnimation & SDFBenchmarkAnimation.Operations) != 0 && !animateOperations;
            bool resetModifiers = (m_PreviousAnimation & SDFBenchmarkAnimation.Modifiers) != 0 && !animateModifiers;
            if (activeAnimation == SDFBenchmarkAnimation.None && disabledTransforms == SDFBenchmarkAnimation.None &&
                !resetOperations && !resetModifiers)
            {
                m_PreviousAnimation = activeAnimation;
                return;
            }
            float time = (Application.isPlaying ? Time.time : Time.realtimeSinceStartup) * m_AnimationSpeed;
            bool animatePositions = (activeAnimation & SDFBenchmarkAnimation.Positions) != 0;
            bool animateRotations = (activeAnimation & SDFBenchmarkAnimation.Rotations) != 0;
            bool animateScales = (activeAnimation & SDFBenchmarkAnimation.Scales) != 0;
            bool animateMaterials = (activeAnimation & SDFBenchmarkAnimation.Materials) != 0;
            bool updatePositions = animatePositions || (disabledTransforms & SDFBenchmarkAnimation.Positions) != 0;
            bool updateRotations = animateRotations || (disabledTransforms & SDFBenchmarkAnimation.Rotations) != 0;
            bool updateScales = animateScales || (disabledTransforms & SDFBenchmarkAnimation.Scales) != 0;
            using (SDFSceneRegistry.BatchChanges())
            {
                if (updatePositions || updateRotations || updateScales)
                {
                    int width = Mathf.CeilToInt(Mathf.Sqrt(m_ModelCount));
                    AnimateTransformsJob job = new AnimateTransformsJob
                    {
                        Time = time,
                        Spacing = m_Spacing,
                        Width = width,
                        AnimatePositions = animatePositions ? 1 : 0,
                        AnimateRotations = animateRotations ? 1 : 0,
                        AnimateScales = animateScales ? 1 : 0,
                        UpdatePositions = updatePositions ? 1 : 0,
                        UpdateRotations = updateRotations ? 1 : 0,
                        UpdateScales = updateScales ? 1 : 0
                    };
                    using (AnimationMarker.Auto())
                    {
                        JobHandle animationHandle = job.Schedule(m_Transforms);
                        animationHandle.Complete();
                    }
                    SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Transforms);
                }

                if (animateMaterials && m_Materials != null)
                {
                    for (int i = 0; i < m_Materials.Length; ++i)
                    {
                        float hue = Mathf.Repeat(i / (float)m_Materials.Length + time * 0.08f, 1f);
                        Color color = Color.HSVToRGB(hue, 0.72f, 0.9f);
                        m_Materials[i].BaseColor = color;
                        m_Materials[i].Emission = color * (0.03f + 0.03f * Mathf.Sin(time * 1.4f + i));
                        m_Materials[i].Smoothness = 0.5f + 0.4f * Mathf.Sin(time * 0.7f + i * 0.8f);
                    }
                }

                if ((animateOperations || resetOperations) && m_Operations != null)
                {
                    using (OperationAnimationMarker.Auto())
                    {
                        int operationOffset = animateOperations ? Mathf.FloorToInt(time * 0.35f) : 0;
                        for (int i = 0; i < m_Operations.Length; ++i)
                        {
                            SDFOperation operation = m_Operations[i];
                            if (operation == null) continue;
                            operation.SetParameters(
                                (SDFOperationType)((i + operationOffset) % 6),
                                animateOperations
                                    ? 0.08f + (Mathf.Sin(time * 1.1f + i * 0.31f) * 0.5f + 0.5f) * 0.24f
                                    : 0.18f);
                        }
                    }
                }

                if ((animateModifiers || resetModifiers) && m_Modifiers != null)
                {
                    using (ModifierAnimationMarker.Auto())
                    {
                        for (int i = 0; i < m_Modifiers.Length; ++i)
                        {
                            SDFModifier modifier = m_Modifiers[i];
                            if (modifier == null) continue;
                            AnimateModifier(modifier, i, animateModifiers ? time : 0f, animateModifiers);
                        }
                    }
                }
            }
            m_PreviousAnimation = activeAnimation;
        }

        [ContextMenu("Rebuild Benchmark")]
        public void Rebuild()
        {
            DestroyGenerated();
            GameObject root = new GameObject("Generated SDF Benchmark");
            root.hideFlags = HideFlags.DontSave;
            root.transform.SetParent(transform, false);
            m_GeneratedRoot = root.transform;
            int width = Mathf.CeilToInt(Mathf.Sqrt(m_ModelCount));
            m_Transforms = new TransformAccessArray(m_ModelCount);
            m_Operations = m_IncludeOperations ? new SDFOperation[m_ModelCount] : null;
            m_Modifiers = m_IncludeModifiers ? new SDFModifier[m_ModelCount] : null;
            using (SDFSceneRegistry.BatchChanges())
            {
                CreateMaterials();
                for (int i = 0; i < m_ModelCount; ++i)
                {
                    GameObject modelObject = new GameObject("Model " + i);
                    modelObject.transform.SetParent(m_GeneratedRoot, false);
                    modelObject.transform.localPosition = GridPosition(i, width);
                    modelObject.AddComponent<SDFModel>();
                    SDFShape shape = modelObject.AddComponent<SDFShape>();
                    shape.ShapeType = (i & 1) == 0 ? SDFShapeType.Sphere : SDFShapeType.RoundBox;
                    shape.Material = m_Materials[i % m_Materials.Length];
                    if (m_IncludeModifiers)
                    {
                        SDFModifier modifier = modelObject.AddComponent<SDFModifier>();
                        ConfigureModifier(modifier, shape, i);
                        m_Modifiers[i] = modifier;
                    }
                    if (m_IncludeOperations)
                    {
                        GameObject operandObject = new GameObject("CSG Operand");
                        operandObject.transform.SetParent(modelObject.transform, false);
                        operandObject.transform.localPosition = new Vector3(0.38f, 0.08f, 0f);
                        SDFShape operand = operandObject.AddComponent<SDFShape>();
                        operand.ShapeType = (i % 3) == 0 ? SDFShapeType.Sphere : (i % 3) == 1 ? SDFShapeType.RoundBox : SDFShapeType.Torus;
                        operand.Material = m_Materials[(i + 1) % m_Materials.Length];
                        SDFOperation operation = operandObject.AddComponent<SDFOperation>();
                        operation.Type = (SDFOperationType)(i % 6);
                        operation.Smoothness = 0.18f;
                        m_Operations[i] = operation;
                    }
                    modelObject.transform.hasChanged = false;
                    m_Transforms.Add(modelObject.transform);
                }
            }
            m_BuiltCount = m_ModelCount;
            m_BuiltMaterialCount = m_MaterialCount;
            m_BuiltSpacing = m_Spacing;
            m_BuiltOperations = m_IncludeOperations;
            m_BuiltModifiers = m_IncludeModifiers;
            m_PreviousAnimation = SDFBenchmarkAnimation.None;
        }

        private void RebuildIfNeeded()
        {
            if (m_BuiltCount != m_ModelCount ||
                m_BuiltMaterialCount != m_MaterialCount ||
                !Mathf.Approximately(m_BuiltSpacing, m_Spacing) ||
                m_BuiltOperations != m_IncludeOperations ||
                m_BuiltModifiers != m_IncludeModifiers ||
                m_GeneratedRoot == null)
                Rebuild();
        }

        private void DestroyGenerated()
        {
            if (m_Transforms.isCreated) m_Transforms.Dispose();
            if (m_GeneratedRoot != null)
            {
                if (Application.isPlaying) Destroy(m_GeneratedRoot.gameObject);
                else DestroyImmediate(m_GeneratedRoot.gameObject);
            }
            m_GeneratedRoot = null;
            m_Operations = null;
            m_Modifiers = null;
            DestroyMaterials();
            m_BuiltCount = -1;
            m_BuiltMaterialCount = -1;
            m_BuiltSpacing = -1f;
            m_BuiltOperations = false;
            m_BuiltModifiers = false;
            m_PreviousAnimation = SDFBenchmarkAnimation.None;
        }

        private Vector3 GridPosition(int index, int width) => new Vector3(
            (index % width - width * 0.5f) * m_Spacing,
            0f,
            (index / width - width * 0.5f) * m_Spacing);

        private void CreateMaterials()
        {
            DestroyMaterials();
            m_Materials = new SDFMaterialAsset[m_MaterialCount];
            for (int i = 0; i < m_Materials.Length; ++i)
            {
                SDFMaterialAsset material = ScriptableObject.CreateInstance<SDFMaterialAsset>();
                material.name = "Benchmark Material " + i;
                material.hideFlags = HideFlags.HideAndDontSave;
                material.BaseColor = Color.HSVToRGB(i / (float)m_Materials.Length, 0.65f, 0.9f);
                m_Materials[i] = material;
            }
        }

        private void DestroyMaterials()
        {
            if (m_Materials == null)
                return;
            for (int i = 0; i < m_Materials.Length; ++i)
            {
                if (m_Materials[i] == null) continue;
                if (Application.isPlaying) Destroy(m_Materials[i]);
                else DestroyImmediate(m_Materials[i]);
            }
            m_Materials = null;
        }

        private static void ConfigureModifier(SDFModifier modifier, SDFShape shape, int index)
        {
            modifier.Type = (SDFModifierType)(index % 10);
            modifier.Axes = modifier.Type == SDFModifierType.Twist || modifier.Type == SDFModifierType.Bend
                ? SDFModifierAxes.Y : SDFModifierAxes.X;
            modifier.Count = new Vector3Int(1, 0, 0);
            modifier.Amount = 0.12f;
            modifier.Vector = modifier.Type == SDFModifierType.FiniteRepeat || modifier.Type == SDFModifierType.InfiniteRepeat
                ? new Vector3(1.25f, 1f, 1f) : new Vector3(0.18f, 0f, 0f);
            if (modifier.Type == SDFModifierType.InfiniteRepeat)
                shape.ClipBounds = new Bounds(Vector3.zero, Vector3.one * 1.8f);
        }

        private static void AnimateModifier(SDFModifier modifier, int index, float time, bool animate)
        {
            float wave = animate ? Mathf.Sin(time * 1.25f + index * 0.23f) : 0f;
            switch (modifier.Type)
            {
                case SDFModifierType.Elongate:
                case SDFModifierType.Mirror:
                    modifier.Vector = new Vector3(0.18f + wave * 0.1f, 0f, 0f);
                    break;
                case SDFModifierType.FiniteRepeat:
                case SDFModifierType.InfiniteRepeat:
                    modifier.Vector = new Vector3(1.25f + wave * 0.18f, 1f, 1f);
                    break;
                default:
                    modifier.Amount = 0.12f + wave * 0.08f;
                    break;
            }
        }
    }
}
