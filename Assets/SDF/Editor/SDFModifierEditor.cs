using UnityEditor;
using UnityEngine;

namespace SdfRenderer.Editor
{
    [CustomEditor(typeof(SDFModifier)), CanEditMultipleObjects]
    public sealed class SDFModifierEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty type = serializedObject.FindProperty("m_Type");
            EditorGUILayout.PropertyField(type);
            if (!type.hasMultipleDifferentValues)
            {
                SDFModifierType value = (SDFModifierType)type.enumValueIndex;
                if (value == SDFModifierType.Elongate || value == SDFModifierType.Mirror || value == SDFModifierType.FiniteRepeat || value == SDFModifierType.InfiniteRepeat || value == SDFModifierType.Extrusion)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Axes"));
                if (value == SDFModifierType.Elongate || value == SDFModifierType.Mirror || value == SDFModifierType.FiniteRepeat || value == SDFModifierType.InfiniteRepeat)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Vector"), new GUIContent(value.ToString() + " Parameters"));
                if (value == SDFModifierType.FiniteRepeat)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Count"));
                if (value == SDFModifierType.Round || value == SDFModifierType.Onion || value == SDFModifierType.Twist || value == SDFModifierType.Bend || value == SDFModifierType.Revolution || value == SDFModifierType.Extrusion)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Amount"));
                if (value == SDFModifierType.InfiniteRepeat || value == SDFModifierType.Twist || value == SDFModifierType.Bend || value == SDFModifierType.Revolution)
                    EditorGUILayout.HelpBox("This modifier disables conservative operand-distance skipping. Infinite repetition also requires explicit Clip Bounds on the shape.", MessageType.Info);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
