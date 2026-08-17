using UnityEngine;

namespace SdfRenderer
{
    [ExecuteAlways]
    public sealed class SDFModifier : MonoBehaviour
    {
        [SerializeField] private SDFModifierType m_Type;
        [SerializeField] private SDFModifierAxes m_Axes = SDFModifierAxes.All;
        [SerializeField] private Vector3 m_Vector = Vector3.one;
        [SerializeField] private Vector3Int m_Count = Vector3Int.one;
        [SerializeField] private float m_Amount = 0.1f;

        public SDFModifierType Type { get => m_Type; set { if (m_Type == value) return; m_Type = value; MarkDirty(); } }
        public SDFModifierAxes Axes { get => m_Axes; set { if (m_Axes == value) return; m_Axes = value; MarkDirty(); } }
        public Vector3 Vector { get => m_Vector; set { if (m_Vector == value) return; m_Vector = value; MarkDirty(); } }
        public Vector3Int Count { get => m_Count; set { value = Positive(value); if (m_Count == value) return; m_Count = value; MarkDirty(); } }
        public float Amount { get => m_Amount; set { if (Mathf.Approximately(m_Amount, value)) return; m_Amount = value; MarkDirty(); } }

        public bool InvalidatesBoundsDistance => m_Type == SDFModifierType.Twist || m_Type == SDFModifierType.Bend || m_Type == SDFModifierType.Revolution ||
            m_Type == SDFModifierType.InfiniteRepeat;

        internal Vector4 PackA() => new Vector4((float)m_Type, (float)m_Axes, m_Amount, 0f);
        internal Vector4 PackB() => new Vector4(m_Vector.x, m_Vector.y, m_Vector.z, 0f);
        internal Vector4 PackC() => new Vector4(Mathf.Max(0, m_Count.x), Mathf.Max(0, m_Count.y), Mathf.Max(0, m_Count.z), 0f);

        private void OnEnable() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology | SDFDirtyFlags.Modifiers);
        private void OnDisable() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology | SDFDirtyFlags.Modifiers);
        private void OnDestroy() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology | SDFDirtyFlags.Modifiers);
        private void OnDidApplyAnimationProperties() => OnValidate();
        private void OnValidate()
        {
            m_Count = Positive(m_Count);
            MarkDirty();
        }

        private static Vector3Int Positive(Vector3Int value) => new Vector3Int(Mathf.Max(0, value.x), Mathf.Max(0, value.y), Mathf.Max(0, value.z));
        private static void MarkDirty() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Modifiers);
    }
}
