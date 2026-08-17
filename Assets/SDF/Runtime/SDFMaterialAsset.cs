using UnityEngine;

namespace SdfRenderer
{
    [CreateAssetMenu(menuName = "SDF/Material", fileName = "SDFMaterial")]
    public sealed class SDFMaterialAsset : ScriptableObject
    {
        [SerializeField, HideInInspector] private int m_SerializationVersion;
        [SerializeField] private SDFShadingModel m_ShadingModel = SDFShadingModel.PbrLike;
        [SerializeField] private Color m_BaseColor = new Color(0.7f, 0.75f, 0.8f, 1f);
        [SerializeField] private Texture2D m_BaseMap;
        [SerializeField] private Color m_SpecularColor = Color.white;
        [SerializeField, Range(1f, 256f)] private float m_SpecularPower = 48f;
        [SerializeField, Range(0f, 1f)] private float m_Metallic;
        [SerializeField, Range(0f, 1f)] private float m_Smoothness = 0.5f;
        [SerializeField, Range(0f, 1f)] private float m_Occlusion = 1f;
        [SerializeField] private Color m_Emission = Color.black;
        [SerializeField, Range(1, 8)] private int m_CelBands = 3;
        [SerializeField] private SDFShaderAsset m_CustomShader;
        [SerializeField] private Vector4 m_Custom0;
        [SerializeField] private Vector4 m_Custom1;

        public SDFShadingModel ShadingModel => m_ShadingModel;
        public Color BaseColor { get => m_BaseColor; set { if (m_BaseColor == value) return; m_BaseColor = value; MarkDirty(); } }
        public Texture2D BaseMap => m_BaseMap;
        public Color SpecularColor => m_SpecularColor;
        public float SpecularPower => m_SpecularPower;
        public float Metallic => m_Metallic;
        public float Smoothness { get => m_Smoothness; set { value = Mathf.Clamp01(value); if (Mathf.Approximately(m_Smoothness, value)) return; m_Smoothness = value; MarkDirty(); } }
        public float Occlusion => m_Occlusion;
        public Color Emission { get => m_Emission; set { if (m_Emission == value) return; m_Emission = value; MarkDirty(); } }
        public int CelBands => m_CelBands;
        public SDFShaderAsset CustomShader => m_CustomShader;
        public Vector4 Custom0 => m_Custom0;
        public Vector4 Custom1 => m_Custom1;

        private void OnEnable()
        {
            // Occlusion was added after the original material format. Explicitly
            // migrate old assets so a missing serialized field cannot suppress all
            // environment lighting and leave only the colored main light.
            if (m_SerializationVersion >= 1) return;
            m_Occlusion = 1f;
            m_SerializationVersion = 1;
        }

        private void OnValidate()
        {
            m_SpecularPower = Mathf.Clamp(m_SpecularPower, 1f, 256f);
            m_Occlusion = Mathf.Clamp01(m_Occlusion);
            m_SerializationVersion = 1;
            m_CelBands = Mathf.Clamp(m_CelBands, 1, 8);
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Materials);
        }

        private static void MarkDirty() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Materials);
    }
}
