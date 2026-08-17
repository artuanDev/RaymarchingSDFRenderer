using System;
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
        Everything = Positions | Rotations | Scales | Materials
    }

    [ExecuteAlways]
    public sealed class SDFBenchmarkController : MonoBehaviour
    {
        private static readonly ProfilerMarker AnimationMarker = new ProfilerMarker("SDF/Benchmark Animate Transforms");

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
        [SerializeField, Range(1, 32)] private int m_MaterialCount = 8;
        [SerializeField, Min(0f)] private float m_AnimationSpeed = 1f;
        [SerializeField] private bool m_PreviewInEditMode;
        private Transform m_GeneratedRoot;
        private TransformAccessArray m_Transforms;
        private SDFMaterialAsset[] m_Materials;

        private int m_BuiltCount = -1;
        private int m_BuiltMaterialCount = -1;
        private float m_BuiltSpacing = -1f;
        private SDFBenchmarkAnimation m_PreviousAnimation;

        public SDFBenchmarkAnimation Animation { get => m_Animation; set => m_Animation = value; }

        private void OnEnable() => RebuildIfNeeded();
        private void OnDisable() => DestroyGenerated();
        private void OnValidate()
        {
            m_ModelCount = Mathf.Clamp(m_ModelCount, 1, 10000);
            m_MaterialCount = Mathf.Clamp(m_MaterialCount, 1, 32);
            m_AnimationSpeed = Mathf.Max(0f, m_AnimationSpeed);
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
            if (activeAnimation == SDFBenchmarkAnimation.None && disabledTransforms == SDFBenchmarkAnimation.None)
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
                    modelObject.transform.hasChanged = false;
                    m_Transforms.Add(modelObject.transform);
                }
            }
            m_BuiltCount = m_ModelCount;
            m_BuiltMaterialCount = m_MaterialCount;
            m_BuiltSpacing = m_Spacing;
            m_PreviousAnimation = SDFBenchmarkAnimation.None;
        }

        private void RebuildIfNeeded()
        {
            if (m_BuiltCount != m_ModelCount ||
                m_BuiltMaterialCount != m_MaterialCount ||
                !Mathf.Approximately(m_BuiltSpacing, m_Spacing) ||
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
            DestroyMaterials();
            m_BuiltCount = -1;
            m_BuiltMaterialCount = -1;
            m_BuiltSpacing = -1f;
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
    }
}
