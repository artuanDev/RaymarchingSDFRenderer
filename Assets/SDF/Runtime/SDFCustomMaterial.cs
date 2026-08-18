using UnityEngine;

namespace SdfRenderer
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SDFCustomMaterial : MonoBehaviour
    {
        [SerializeField] private SDFMaterialAsset m_Material;
        [System.NonSerialized] private uint m_DataVersion;
        internal uint DataVersion => m_DataVersion;
        public SDFMaterialAsset Material { get => m_Material; set { if (m_Material == value) return; m_Material = value; MarkDirty(); } }

        private void MarkDirty()
        {
            unchecked { ++m_DataVersion; }
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Shapes | SDFDirtyFlags.Materials);
        }
        private void OnEnable() => MarkTopologyDirty();
        private void OnDisable() => MarkDirty();
        private void OnDestroy() => MarkTopologyDirty();
        private void OnValidate() => MarkDirty();

        private void MarkTopologyDirty()
        {
            unchecked { ++m_DataVersion; }
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology | SDFDirtyFlags.Shapes | SDFDirtyFlags.Materials);
        }
    }
}
