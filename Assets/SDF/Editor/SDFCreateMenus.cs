using UnityEditor;
using UnityEngine;

namespace SdfRenderer.Editor
{
    public static class SDFCreateMenus
    {
        [MenuItem("GameObject/SDF/Model", false, 10)]
        public static GameObject CreateModel(MenuCommand command)
        {
            GameObject model = new GameObject("SDF Model");
            Undo.RegisterCreatedObjectUndo(model, "Create SDF Model");
            GameObjectUtility.SetParentAndAlign(model, command?.context as GameObject);
            Undo.AddComponent<SDFModel>(model);
            Selection.activeGameObject = model;
            return model;
        }

        [MenuItem("GameObject/SDF/Sphere", false, 20)] private static void Sphere(MenuCommand c) => CreateShape(SDFShapeType.Sphere, c?.context as GameObject);
        [MenuItem("GameObject/SDF/Box", false, 21)] private static void Box(MenuCommand c) => CreateShape(SDFShapeType.Box, c?.context as GameObject);
        [MenuItem("GameObject/SDF/Round Box", false, 22)] private static void RoundBox(MenuCommand c) => CreateShape(SDFShapeType.RoundBox, c?.context as GameObject);
        [MenuItem("GameObject/SDF/Torus", false, 23)] private static void Torus(MenuCommand c) => CreateShape(SDFShapeType.Torus, c?.context as GameObject);

        public static SDFShape CreateShape(SDFShapeType type, GameObject requestedParent = null)
        {
            SDFModel model = requestedParent != null ? requestedParent.GetComponentInParent<SDFModel>() : null;
            if (model == null && Selection.activeGameObject != null)
                model = Selection.activeGameObject.GetComponentInParent<SDFModel>();
            if (model == null)
                model = CreateModel(null).GetComponent<SDFModel>();

            GameObject child = new GameObject(ObjectNames.NicifyVariableName(type.ToString()));
            Undo.RegisterCreatedObjectUndo(child, "Create SDF Shape");
            Undo.SetTransformParent(child.transform, model.transform, "Parent SDF Shape");
            child.transform.localPosition = Vector3.zero;
            SDFShape shape = Undo.AddComponent<SDFShape>(child);
            shape.ShapeType = type;
            Undo.AddComponent<SDFOperation>(child);
            Selection.activeGameObject = child;
            EditorGUIUtility.PingObject(child);
            return shape;
        }

        public static SDFModifier AddModifier(GameObject target, SDFModifierType type)
        {
            if (target == null || target.GetComponent<SDFShape>() == null)
                return null;
            SDFModifier modifier = Undo.AddComponent<SDFModifier>(target);
            SerializedObject serialized = new SerializedObject(modifier);
            serialized.FindProperty("m_Type").enumValueIndex = (int)type;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return modifier;
        }

        public static void CreateMaterialAsset()
        {
            SDFMaterialAsset material = ScriptableObject.CreateInstance<SDFMaterialAsset>();
            ProjectWindowUtil.CreateAsset(material, "New SDF Material.asset");
        }
    }
}
