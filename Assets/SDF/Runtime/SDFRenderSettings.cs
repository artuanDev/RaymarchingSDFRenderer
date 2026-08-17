using UnityEngine;

namespace SdfRenderer
{
    [CreateAssetMenu(menuName = "SDF/Render Settings", fileName = "SDFRenderSettings")]
    public sealed class SDFRenderSettings : ScriptableObject
    {
        [SerializeField, HideInInspector] private SDFQualityPreset m_QualityPreset = SDFQualityPreset.High;

        [Header("Sphere tracing")]
        [SerializeField, Range(16, 1024)] private int m_MaxSteps = 192;
        [SerializeField, Min(0.001f)] private float m_MaxDistance = 1000f;
        [SerializeField, Range(0.05f, 1f)] private float m_StepSafety = 0.8f;
        [SerializeField, Min(0.000001f)] private float m_SurfaceEpsilon = 0.0005f;
        [SerializeField, Min(0.000001f)] private float m_NormalEpsilon = 0.001f;
        [SerializeField, Range(0f, 2f)] private float m_PixelTolerance = 0.5f;

        [Header("Lighting fallback")]
        [SerializeField] private Color m_AmbientColor = new Color(0.08f, 0.09f, 0.12f, 1f);
        [SerializeField] private Vector3 m_LightDirection = new Vector3(0.35f, 0.8f, 0.25f);
        [SerializeField] private Color m_LightColor = Color.white;
        [SerializeField, Min(0f)] private float m_LightIntensity = 1f;

        public int MaxSteps => m_MaxSteps;
        public float MaxDistance => m_MaxDistance;
        public float StepSafety => m_StepSafety;
        public float SurfaceEpsilon => m_SurfaceEpsilon;
        public float NormalEpsilon => m_NormalEpsilon;
        public float PixelTolerance => m_PixelTolerance;
        public Color AmbientColor => m_AmbientColor;
        public Vector3 LightDirection => m_LightDirection.sqrMagnitude > 0f ? m_LightDirection.normalized : Vector3.up;
        public Color LightColor => m_LightColor;
        public float LightIntensity => m_LightIntensity;
        public SDFQualityPreset QualityPreset => m_QualityPreset;

        public void ApplyPreset(SDFQualityPreset preset)
        {
            m_QualityPreset = preset;
            switch (preset)
            {
                case SDFQualityPreset.Balanced:
                    m_MaxSteps = 128; m_StepSafety = 0.75f; m_SurfaceEpsilon = 0.00075f; m_NormalEpsilon = 0.0015f; m_PixelTolerance = 0.7f;
                    break;
                case SDFQualityPreset.High:
                    m_MaxSteps = 192; m_StepSafety = 0.8f; m_SurfaceEpsilon = 0.0005f; m_NormalEpsilon = 0.001f; m_PixelTolerance = 0.5f;
                    break;
                case SDFQualityPreset.Ultra:
                    m_MaxSteps = 384; m_StepSafety = 0.7f; m_SurfaceEpsilon = 0.0002f; m_NormalEpsilon = 0.0004f; m_PixelTolerance = 0.25f;
                    break;
            }
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Settings);
        }

        private void OnValidate()
        {
            m_MaxSteps = Mathf.Clamp(m_MaxSteps, 16, 1024);
            m_MaxDistance = Mathf.Max(0.001f, m_MaxDistance);
            m_StepSafety = Mathf.Clamp(m_StepSafety, 0.05f, 1f);
            m_SurfaceEpsilon = Mathf.Max(0.000001f, m_SurfaceEpsilon);
            m_NormalEpsilon = Mathf.Max(0.000001f, m_NormalEpsilon);
            if (m_QualityPreset != SDFQualityPreset.Custom &&
                !MatchesPreset(m_QualityPreset))
                m_QualityPreset = SDFQualityPreset.Custom;
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Settings);
        }

        private bool MatchesPreset(SDFQualityPreset preset)
        {
            switch (preset)
            {
                case SDFQualityPreset.Balanced: return m_MaxSteps == 128 && Mathf.Approximately(m_StepSafety, 0.75f) && Mathf.Approximately(m_SurfaceEpsilon, 0.00075f) && Mathf.Approximately(m_NormalEpsilon, 0.0015f) && Mathf.Approximately(m_PixelTolerance, 0.7f);
                case SDFQualityPreset.High: return m_MaxSteps == 192 && Mathf.Approximately(m_StepSafety, 0.8f) && Mathf.Approximately(m_SurfaceEpsilon, 0.0005f) && Mathf.Approximately(m_NormalEpsilon, 0.001f) && Mathf.Approximately(m_PixelTolerance, 0.5f);
                case SDFQualityPreset.Ultra: return m_MaxSteps == 384 && Mathf.Approximately(m_StepSafety, 0.7f) && Mathf.Approximately(m_SurfaceEpsilon, 0.0002f) && Mathf.Approximately(m_NormalEpsilon, 0.0004f) && Mathf.Approximately(m_PixelTolerance, 0.25f);
                default: return true;
            }
        }
    }
}
