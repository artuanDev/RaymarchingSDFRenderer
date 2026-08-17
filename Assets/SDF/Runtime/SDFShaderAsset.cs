using System;
using UnityEngine;

namespace SdfRenderer
{
    public sealed class SDFShaderAsset : ScriptableObject
    {
        [SerializeField, HideInInspector] private string m_StableId;
        [SerializeField, TextArea(12, 40)] private string m_Source;
        [SerializeField, HideInInspector] private string m_LastError;
        [SerializeField, HideInInspector] private int m_GeneratedIndex = -1;

        public string StableId => m_StableId;
        public string Source => m_Source;
        public string LastError => m_LastError;
        public int GeneratedIndex => m_GeneratedIndex;

        internal void Initialize(string source)
        {
            if (string.IsNullOrEmpty(m_StableId))
                m_StableId = Guid.NewGuid().ToString("N");
            m_Source = source;
        }

        public void SetImportState(string source, string error)
        {
            m_Source = source;
            m_LastError = error;
        }

        public void SetGeneratedIndex(int value) => m_GeneratedIndex = value;
        public void SetStableId(string value) => m_StableId = string.IsNullOrEmpty(value) ? Guid.NewGuid().ToString("N") : value;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(m_StableId))
                m_StableId = Guid.NewGuid().ToString("N");
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Materials);
        }
    }
}
