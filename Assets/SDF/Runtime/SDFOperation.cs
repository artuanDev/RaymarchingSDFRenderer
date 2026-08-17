using UnityEngine;

namespace SdfRenderer
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SDFOperation : MonoBehaviour
    {
        [SerializeField] private SDFOperationType m_Type = SDFOperationType.Union;
        [SerializeField, Min(0f)] private float m_Smoothness = 0.25f;

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

        public static bool UsesSmoothing(SDFOperationType type) =>
            type == SDFOperationType.SmoothUnion || type == SDFOperationType.SmoothSubtraction ||
            type == SDFOperationType.SmoothIntersection;

        private void OnEnable() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology | SDFDirtyFlags.Operations);
        private void OnDisable() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology | SDFDirtyFlags.Operations);
        private void OnDestroy() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology | SDFDirtyFlags.Operations);
        private void OnDidApplyAnimationProperties() => OnValidate();
        private void OnValidate()
        {
            m_Smoothness = Mathf.Max(0f, m_Smoothness);
            MarkDirty();
        }

        private static void MarkDirty() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Operations);
    }
}
