using UnityEngine;

namespace SdfRenderer
{
    [CreateAssetMenu(menuName = "SDF/Material", fileName = "SDFMaterial")]
    public sealed class SDFMaterialAsset : ScriptableObject
    {
        [SerializeField] private SDFShadingModel m_ShadingModel = SDFShadingModel.BlinnPhong;
        [SerializeField] private Color m_BaseColor = new Color(0.7f, 0.75f, 0.8f, 1f);
        [SerializeField] private Texture2D m_BaseMap;
        [SerializeField] private Color m_SpecularColor = Color.white;
        [SerializeField, Range(1f, 256f)] private float m_SpecularPower = 48f;
        [SerializeField, Range(0f, 1f)] private float m_Metallic;
        [SerializeField, Range(0f, 1f)] private float m_Smoothness = 0.5f;
        [SerializeField] private Color m_Emission = Color.black;
        [SerializeField, Range(1, 8)] private int m_CelBands = 3;
        [SerializeField] private SDFShaderAsset m_CustomShader;
        [SerializeField] private Vector4 m_Custom0;
        [SerializeField] private Vector4 m_Custom1;

        public SDFShadingModel ShadingModel => m_ShadingModel;
        public Color BaseColor => m_BaseColor;
        public Texture2D BaseMap => m_BaseMap;
        public Color SpecularColor => m_SpecularColor;
        public float SpecularPower => m_SpecularPower;
        public float Metallic => m_Metallic;
        public float Smoothness => m_Smoothness;
        public Color Emission => m_Emission;
        public int CelBands => m_CelBands;
        public SDFShaderAsset CustomShader => m_CustomShader;
        public Vector4 Custom0 => m_Custom0;
        public Vector4 Custom1 => m_Custom1;

        private void OnValidate()
        {
            m_SpecularPower = Mathf.Clamp(m_SpecularPower, 1f, 256f);
            m_CelBands = Mathf.Clamp(m_CelBands, 1, 8);
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Materials);
        }
    }
}
