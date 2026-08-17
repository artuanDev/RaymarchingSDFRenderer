using UnityEngine;

namespace SdfRenderer
{
    [ExecuteAlways]
    public sealed class SDFBenchmarkController : MonoBehaviour
    {
        [SerializeField, Range(1, 10000)] private int m_ModelCount = 256;
        [SerializeField, Min(0.1f)] private float m_Spacing = 2.5f;
        [SerializeField] private bool m_Animate = true;
        [SerializeField] private bool m_PreviewInEditMode;
        private Transform m_GeneratedRoot;

        private int m_BuiltCount = -1;

        private void OnEnable() => RebuildIfNeeded();
        private void OnDisable() => DestroyGenerated();
        private void OnValidate() { m_ModelCount = Mathf.Clamp(m_ModelCount, 1, 10000); m_BuiltCount = -1; }

        private void Update()
        {
            RebuildIfNeeded();
            if (!m_Animate || (!Application.isPlaying && !m_PreviewInEditMode) || m_GeneratedRoot == null)
                return;
            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            using (SDFSceneRegistry.BatchChanges())
            {
                for (int i = 0; i < m_GeneratedRoot.childCount; ++i)
                {
                    Transform child = m_GeneratedRoot.GetChild(i);
                    Vector3 p = child.localPosition;
                    p.y = Mathf.Sin(time * 1.7f + i * 0.37f) * 0.35f;
                    child.localPosition = p;
                    child.hasChanged = false;
                }
                SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Shapes | SDFDirtyFlags.Bounds);
            }
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
            using (SDFSceneRegistry.BatchChanges())
            {
                for (int i = 0; i < m_ModelCount; ++i)
                {
                    GameObject modelObject = new GameObject("Model " + i);
                    modelObject.transform.SetParent(m_GeneratedRoot, false);
                    modelObject.transform.localPosition = new Vector3((i % width - width * 0.5f) * m_Spacing, 0f, (i / width - width * 0.5f) * m_Spacing);
                    modelObject.AddComponent<SDFModel>();
                    SDFShape shape = modelObject.AddComponent<SDFShape>();
                    shape.ShapeType = (i & 1) == 0 ? SDFShapeType.Sphere : SDFShapeType.RoundBox;
                }
            }
            m_BuiltCount = m_ModelCount;
        }

        private void RebuildIfNeeded()
        {
            if (m_BuiltCount != m_ModelCount || m_GeneratedRoot == null)
                Rebuild();
        }

        private void DestroyGenerated()
        {
            if (m_GeneratedRoot == null) return;
            if (Application.isPlaying) Destroy(m_GeneratedRoot.gameObject);
            else DestroyImmediate(m_GeneratedRoot.gameObject);
            m_GeneratedRoot = null;
            m_BuiltCount = -1;
        }
    }
}
