using UnityEditor;
using UnityEngine;

namespace SdfRenderer.Editor
{
    [CustomEditor(typeof(SDFShape)), CanEditMultipleObjects]
    public sealed class SDFShapeEditor : UnityEditor.Editor
    {
        private SerializedProperty m_Type;

        private void OnEnable() => m_Type = serializedObject.FindProperty("m_ShapeType");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_Type);
            if (!m_Type.hasMultipleDifferentValues)
                DrawShapeProperties((SDFShapeType)m_Type.enumValueIndex);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Material"));
            if (IsUnbounded((SDFShapeType)m_Type.enumValueIndex) || HasInfiniteRepeat())
            {
                EditorGUILayout.HelpBox("This primitive is infinite analytically. Clip Bounds is an explicit render region, not part of its distance function.", MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ClipBounds"));
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawShapeProperties(SDFShapeType type)
        {
            void Field(string name, string label = null) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), label == null ? null : new GUIContent(label));
            switch (type)
            {
                case SDFShapeType.Sphere: Field("m_Radius"); break;
                case SDFShapeType.Box: Field("m_Size", "Half Extents"); break;
                case SDFShapeType.RoundBox: Field("m_Size", "Half Extents"); Field("m_Roundness"); break;
                case SDFShapeType.BoxFrame: Field("m_Size", "Half Extents"); Field("m_Thickness"); break;
                case SDFShapeType.Torus: Field("m_RadiusA", "Major Radius"); Field("m_RadiusB", "Minor Radius"); break;
                case SDFShapeType.CappedTorus: Field("m_RadiusA", "Major Radius"); Field("m_RadiusB", "Minor Radius"); Field("m_Angle", "Cap Angle"); break;
                case SDFShapeType.Link: Field("m_Height", "Half Length"); Field("m_RadiusA", "Major Radius"); Field("m_RadiusB", "Minor Radius"); break;
                case SDFShapeType.InfiniteCylinder: Field("m_Radius"); break;
                case SDFShapeType.Cone: Field("m_Height", "Half Height"); Field("m_RadiusA", "Base Radius"); break;
                case SDFShapeType.InfiniteCone: Field("m_Angle"); break;
                case SDFShapeType.Plane: Field("m_Normal"); Field("m_Offset"); break;
                case SDFShapeType.HexagonalPrism: Field("m_RadiusA", "Radius"); Field("m_Height", "Half Height"); break;
                case SDFShapeType.TriangularPrismBound: Field("m_Size", "Bound Parameters"); break;
                case SDFShapeType.Capsule: Points(2); Field("m_Radius"); break;
                case SDFShapeType.VerticalCapsule: Field("m_Height"); Field("m_Radius"); break;
                case SDFShapeType.CappedCylinder: Field("m_Height", "Half Height"); Field("m_Radius"); break;
                case SDFShapeType.ArbitraryCappedCylinder: Points(2); Field("m_Radius"); break;
                case SDFShapeType.RoundedCylinder: Field("m_Height", "Half Height"); Field("m_RadiusA", "Body Radius"); Field("m_RadiusB", "Edge Radius"); break;
                case SDFShapeType.CappedCone: Field("m_Height", "Half Height"); Field("m_RadiusA", "Lower Radius"); Field("m_RadiusB", "Upper Radius"); break;
                case SDFShapeType.ArbitraryCappedCone:
                case SDFShapeType.RoundCone: Points(2); Field("m_RadiusA", "Start Radius"); Field("m_RadiusB", "End Radius"); break;
                case SDFShapeType.SolidAngle: Field("m_Radius"); Field("m_Angle"); break;
                case SDFShapeType.CutSphere: Field("m_Radius"); Field("m_Offset", "Cut Height"); break;
                case SDFShapeType.CutHollowSphere: Field("m_Radius"); Field("m_Offset", "Cut Height"); Field("m_Thickness"); break;
                case SDFShapeType.DeathStar: Field("m_RadiusA", "Outer Radius"); Field("m_RadiusB", "Cut Radius"); Field("m_Height", "Center Distance"); break;
                case SDFShapeType.RevolvedVesica: Points(2); Field("m_Radius", "Width"); break;
                case SDFShapeType.EllipsoidBound: Field("m_Size", "Radii"); break;
                case SDFShapeType.Rhombus: Field("m_RadiusA", "Diagonal A"); Field("m_RadiusB", "Diagonal B"); Field("m_Height", "Half Height"); Field("m_Roundness"); break;
                case SDFShapeType.Octahedron:
                case SDFShapeType.OctahedronBound: Field("m_Radius", "Size"); break;
                case SDFShapeType.Pyramid: Field("m_Height"); break;
                case SDFShapeType.TriangleUnsigned: Points(3); Field("m_Thickness"); break;
                case SDFShapeType.QuadUnsigned: Points(4); Field("m_Thickness"); break;
            }
        }

        private void Points(int count)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_PointA"), new GUIContent("Point A"));
            if (count > 1) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_PointB"), new GUIContent("Point B"));
            if (count > 2) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_PointC"), new GUIContent("Point C"));
            if (count > 3) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_PointD"), new GUIContent("Point D"));
        }

        private static bool IsUnbounded(SDFShapeType type) => type == SDFShapeType.Plane || type == SDFShapeType.InfiniteCylinder || type == SDFShapeType.InfiniteCone;

        private bool HasInfiniteRepeat()
        {
            foreach (UnityEngine.Object item in targets)
            {
                SDFModifier[] modifiers = ((SDFShape)item).GetComponents<SDFModifier>();
                for (int i = 0; i < modifiers.Length; ++i)
                    if (modifiers[i].isActiveAndEnabled && modifiers[i].Type == SDFModifierType.InfiniteRepeat)
                        return true;
            }
            return false;
        }

        private void OnSceneGUI()
        {
            SDFShape shape = (SDFShape)target;
            using (new Handles.DrawingScope(new Color(0.2f, 0.85f, 1f, 0.8f), shape.transform.localToWorldMatrix))
            {
                Bounds bounds = shape.GetLocalBounds();
                Handles.DrawWireCube(bounds.center, bounds.size);
            }
            if (shape.ShapeType != SDFShapeType.Sphere)
                return;
            EditorGUI.BeginChangeCheck();
            float radius = Handles.RadiusHandle(shape.transform.rotation, shape.transform.position, shape.Radius);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(shape, "Resize SDF Sphere");
                shape.Radius = radius;
                EditorUtility.SetDirty(shape);
            }
        }
    }
}
