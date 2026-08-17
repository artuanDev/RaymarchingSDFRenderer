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

        public SDFModifierType Type => m_Type;
        public SDFModifierAxes Axes => m_Axes;
        public Vector3 Vector => m_Vector;
        public Vector3Int Count => m_Count;
        public float Amount => m_Amount;

        public bool InvalidatesBoundsDistance => m_Type == SDFModifierType.Twist || m_Type == SDFModifierType.Bend || m_Type == SDFModifierType.Revolution ||
            m_Type == SDFModifierType.InfiniteRepeat;

        internal Vector4 PackA() => new Vector4((float)m_Type, (float)m_Axes, m_Amount, 0f);
        internal Vector4 PackB() => new Vector4(m_Vector.x, m_Vector.y, m_Vector.z, 0f);
        internal Vector4 PackC() => new Vector4(Mathf.Max(0, m_Count.x), Mathf.Max(0, m_Count.y), Mathf.Max(0, m_Count.z), 0f);

        private void OnEnable() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Modifiers);
        private void OnDisable() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Modifiers);
        private void OnDestroy() => SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Modifiers);
        private void OnDidApplyAnimationProperties() => OnValidate();
        private void OnValidate()
        {
            m_Count = new Vector3Int(Mathf.Max(0, m_Count.x), Mathf.Max(0, m_Count.y), Mathf.Max(0, m_Count.z));
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Modifiers);
        }
    }
}
