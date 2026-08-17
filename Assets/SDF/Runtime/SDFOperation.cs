using UnityEngine;

namespace SdfRenderer
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SDFOperation : MonoBehaviour
    {
        [SerializeField] private SDFOperationType m_Type = SDFOperationType.Union;
        [SerializeField, Min(0f)] private float m_Smoothness = 0.25f;

        public SDFOperationType Type => m_Type;
        public float Smoothness => UsesSmoothing(m_Type) ? Mathf.Max(0f, m_Smoothness) : 0f;

        public static bool UsesSmoothing(SDFOperationType type) =>
            type == SDFOperationType.SmoothUnion || type == SDFOperationType.SmoothSubtraction ||
            type == SDFOperationType.SmoothIntersection;

        private void OnEnable() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology);
        private void OnDisable() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology);
        private void OnDidApplyAnimationProperties() => OnValidate();
        private void OnValidate()
        {
            m_Smoothness = Mathf.Max(0f, m_Smoothness);
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology);
        }
    }
}
