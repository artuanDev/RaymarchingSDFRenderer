using UnityEngine;

namespace SdfRenderer
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SDFModel : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float m_BoundsPadding = 0.02f;
        [SerializeField] private bool m_RenderInSceneView = true;

        public float BoundsPadding => Mathf.Max(0f, m_BoundsPadding);
        public bool RenderInSceneView => m_RenderInSceneView;

        private void OnEnable() => SDFSceneRegistry.Register(this);
        private void OnDisable() => SDFSceneRegistry.Unregister(this);
        private void OnDestroy() => SDFSceneRegistry.Unregister(this);
        private void OnTransformChildrenChanged() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology);
        private void OnValidate()
        {
            m_BoundsPadding = Mathf.Max(0f, m_BoundsPadding);
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Bounds | SDFDirtyFlags.Topology);
        }
    }
}
