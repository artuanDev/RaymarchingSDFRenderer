using UnityEngine;

namespace SdfRenderer
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SDFCustomMaterial : MonoBehaviour
    {
        [SerializeField] private SDFMaterialAsset m_Material;
        public SDFMaterialAsset Material { get => m_Material; set { m_Material = value; MarkDirty(); } }

        private static void MarkDirty() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Shapes | SDFDirtyFlags.Materials);
        private void OnEnable() => MarkDirty();
        private void OnDisable() => MarkDirty();
        private void OnValidate() => MarkDirty();
    }
}
