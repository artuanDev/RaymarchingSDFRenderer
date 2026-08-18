using UnityEngine;

namespace SdfRenderer
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SDFOperation : MonoBehaviour
    {
        [SerializeField] private SDFOperationType m_Type = SDFOperationType.Union;
        [SerializeField, Min(0f)] private float m_Smoothness = 0.25f;
        [System.NonSerialized] private uint m_DataVersion;

        internal uint DataVersion => m_DataVersion;

        public SDFOperationType Type { get => m_Type; set { if (m_Type == value) return; m_Type = value; MarkDirty(); } }
        public float Smoothness
        {
            get => UsesSmoothing(m_Type) ? Mathf.Max(0f, m_Smoothness) : 0f;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(m_Smoothness, value)) return;
                m_Smoothness = value;
                MarkDirty();
            }
        }
        internal float RawSmoothness => Mathf.Max(0f, m_Smoothness);

        public void SetParameters(SDFOperationType type, float smoothness)
        {
            smoothness = Mathf.Max(0f, smoothness);
            if (m_Type == type && Mathf.Approximately(m_Smoothness, smoothness))
                return;
            m_Type = type;
            m_Smoothness = smoothness;
            MarkDirty();
        }

        public static bool UsesSmoothing(SDFOperationType type) =>
            type == SDFOperationType.SmoothUnion || type == SDFOperationType.SmoothSubtraction ||
            type == SDFOperationType.SmoothIntersection;

        private void OnEnable() => MarkTopologyDirty();
        private void OnDisable() => MarkTopologyDirty();
        private void OnDestroy() => MarkTopologyDirty();
        private void OnDidApplyAnimationProperties() => OnValidate();
        private void OnValidate()
        {
            m_Smoothness = Mathf.Max(0f, m_Smoothness);
            MarkDirty();
        }

        private void MarkDirty()
        {
            unchecked { ++m_DataVersion; }
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Operations);
        }

        private void MarkTopologyDirty()
        {
            unchecked { ++m_DataVersion; }
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology | SDFDirtyFlags.Operations);
        }
    }
}
