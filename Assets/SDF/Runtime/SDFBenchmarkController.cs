using System;
using UnityEngine;
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
        [SerializeField, Range(1, 10000)] private int m_ModelCount = 256;
        [SerializeField, Min(0.1f)] private float m_Spacing = 2.5f;
        [SerializeField, FormerlySerializedAs("m_Animate")] private SDFBenchmarkAnimation m_Animation = SDFBenchmarkAnimation.Positions;
        [SerializeField, Range(1, 32)] private int m_MaterialCount = 8;
        [SerializeField, Min(0f)] private float m_AnimationSpeed = 1f;
        [SerializeField] private bool m_PreviewInEditMode;
        private Transform m_GeneratedRoot;
        private Transform[] m_Instances;
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
            if (m_GeneratedRoot == null || m_Instances == null)
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
                    for (int i = 0; i < m_Instances.Length; ++i)
                    {
                        Transform child = m_Instances[i];
                        float phase = time + i * 0.173f;
                        Vector3 position = default;
                        Quaternion rotation = default;
                        if (updatePositions)
                        {
                            position = GridPosition(i, width);
                            if (animatePositions)
                                position.y = Mathf.Sin(phase * 1.7f) * 0.35f;
                        }
                        if (updateRotations)
                        {
                            rotation = animateRotations
                                ? Quaternion.Euler(Mathf.Sin(phase * 0.9f) * 35f, phase * 55f, Mathf.Cos(phase * 1.1f) * 20f)
                                : Quaternion.identity;
                        }
                        if (updatePositions && updateRotations)
                            child.SetLocalPositionAndRotation(position, rotation);
                        else if (updatePositions)
                            child.localPosition = position;
                        else if (updateRotations)
                            child.localRotation = rotation;
                        if (updateScales)
                            child.localScale = animateScales
                                ? Vector3.one * (1f + Mathf.Sin(phase * 1.3f) * 0.28f)
                                : Vector3.one;
                        // The benchmark explicitly reports one transform batch, so
                        // the registry does not need to rediscover the same changes.
                        child.hasChanged = false;
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
            m_Instances = new Transform[m_ModelCount];
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
                    m_Instances[i] = modelObject.transform;
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
            if (m_GeneratedRoot != null)
            {
                if (Application.isPlaying) Destroy(m_GeneratedRoot.gameObject);
                else DestroyImmediate(m_GeneratedRoot.gameObject);
            }
            m_GeneratedRoot = null;
            m_Instances = null;
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
