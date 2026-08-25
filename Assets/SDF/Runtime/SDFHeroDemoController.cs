using UnityEngine;

namespace SdfRenderer
{
    /// <summary>
    /// Drives the seamless, capture-ready motion in the hero sample. The scene remains
    /// fully authorable: this component only animates ordinary transforms and the public
    /// SDF modifier API while the game is running.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SDFHeroDemoController : MonoBehaviour
    {
        [SerializeField] private Transform m_Sculpture;
        [SerializeField] private Transform m_BlendShape;
        [SerializeField] private Transform m_CutterShape;
        [SerializeField] private Transform m_OrbitRoot;
        [SerializeField] private Transform m_FaceSculpture;
        [SerializeField] private Transform m_CreatureSculpture;
        [SerializeField] private Transform m_TotemSculpture;
        [SerializeField] private SDFModifier m_Twist;
        [SerializeField] private Camera m_Camera;
        [SerializeField] private Transform m_CameraTarget;
        [SerializeField, Min(1f)] private float m_LoopDuration = 12f;
        [SerializeField] private bool m_AnimateCamera = true;

        private Quaternion m_SculptureRotation;
        private Vector3 m_BlendPosition;
        private Quaternion m_CutterRotation;
        private Quaternion m_OrbitRotation;
        private Quaternion m_FaceRotation;
        private Vector3 m_CreaturePosition;
        private Quaternion m_CreatureRotation;
        private Quaternion m_TotemRotation;
        private Vector3 m_CameraOffset;
        private float m_InitialTwist;

        private void OnEnable()
        {
            if (m_Sculpture != null) m_SculptureRotation = m_Sculpture.localRotation;
            if (m_BlendShape != null) m_BlendPosition = m_BlendShape.localPosition;
            if (m_CutterShape != null) m_CutterRotation = m_CutterShape.localRotation;
            if (m_OrbitRoot != null) m_OrbitRotation = m_OrbitRoot.localRotation;
            if (m_FaceSculpture != null) m_FaceRotation = m_FaceSculpture.localRotation;
            if (m_CreatureSculpture != null)
            {
                m_CreaturePosition = m_CreatureSculpture.localPosition;
                m_CreatureRotation = m_CreatureSculpture.localRotation;
            }
            if (m_TotemSculpture != null) m_TotemRotation = m_TotemSculpture.localRotation;
            if (m_Twist != null) m_InitialTwist = m_Twist.Amount;
            if (m_Camera != null && m_CameraTarget != null)
                m_CameraOffset = m_Camera.transform.position - m_CameraTarget.position;
        }

        private void Update()
        {
            float phase = Mathf.Repeat(Time.time / Mathf.Max(1f, m_LoopDuration), 1f) * Mathf.PI * 2f;
            float wave = Mathf.Sin(phase);

            using (SDFSceneRegistry.BatchChanges())
            {
                if (m_Sculpture != null)
                    m_Sculpture.localRotation = m_SculptureRotation *
                        Quaternion.Euler(wave * 3f, phase * Mathf.Rad2Deg, Mathf.Cos(phase) * 2f);
                if (m_BlendShape != null)
                    m_BlendShape.localPosition = m_BlendPosition + new Vector3(wave * 0.28f, Mathf.Cos(phase * 2f) * 0.12f, 0f);
                if (m_CutterShape != null)
                    m_CutterShape.localRotation = m_CutterRotation * Quaternion.Euler(phase * Mathf.Rad2Deg * 2f, phase * Mathf.Rad2Deg, 0f);
                if (m_OrbitRoot != null)
                    m_OrbitRoot.localRotation = m_OrbitRotation * Quaternion.Euler(0f, -phase * Mathf.Rad2Deg, wave * 8f);
                if (m_FaceSculpture != null)
                    m_FaceSculpture.localRotation = m_FaceRotation *
                        Quaternion.Euler(Mathf.Cos(phase) * 1.5f, wave * 7f, 0f);
                if (m_CreatureSculpture != null)
                {
                    m_CreatureSculpture.localPosition = m_CreaturePosition + Vector3.up * (wave * 0.09f);
                    m_CreatureSculpture.localRotation = m_CreatureRotation *
                        Quaternion.Euler(0f, wave * -9f, Mathf.Sin(phase * 2f) * 2.5f);
                }
                if (m_TotemSculpture != null)
                    m_TotemSculpture.localRotation = m_TotemRotation *
                        Quaternion.Euler(0f, -phase * Mathf.Rad2Deg, Mathf.Cos(phase) * 1.5f);
                if (m_Twist != null)
                    m_Twist.Amount = m_InitialTwist + wave * 0.18f;
            }

            if (m_AnimateCamera && m_Camera != null && m_CameraTarget != null)
            {
                Quaternion drift = Quaternion.AngleAxis(wave * 7f, Vector3.up);
                Vector3 offset = drift * m_CameraOffset + Vector3.up * (Mathf.Cos(phase) * 0.12f);
                m_Camera.transform.position = m_CameraTarget.position + offset;
                m_Camera.transform.rotation = Quaternion.LookRotation(m_CameraTarget.position - m_Camera.transform.position, Vector3.up);
            }
        }
    }
}
