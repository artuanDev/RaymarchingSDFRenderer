using UnityEditor;

namespace SdfRenderer.Editor
{
    [CustomEditor(typeof(SDFOperation)), CanEditMultipleObjects]
    public sealed class SDFOperationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty type = serializedObject.FindProperty("m_Type");
            EditorGUILayout.PropertyField(type);
            if (type.hasMultipleDifferentValues || SDFOperation.UsesSmoothing((SDFOperationType)type.enumValueIndex))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Smoothness"), new UnityEngine.GUIContent("Blend Radius"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
