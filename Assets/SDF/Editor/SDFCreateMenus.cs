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

        [MenuItem("GameObject/SDF/Sphere", false, 20)] private static void Sphere(MenuCommand command) => CreateFromMenu(SDFShapeType.Sphere, command);
        [MenuItem("GameObject/SDF/Box", false, 21)] private static void Box(MenuCommand command) => CreateFromMenu(SDFShapeType.Box, command);
        [MenuItem("GameObject/SDF/Round Box", false, 22)] private static void RoundBox(MenuCommand command) => CreateFromMenu(SDFShapeType.RoundBox, command);
        [MenuItem("GameObject/SDF/Box Frame", false, 23)] private static void BoxFrame(MenuCommand command) => CreateFromMenu(SDFShapeType.BoxFrame, command);
        [MenuItem("GameObject/SDF/Torus", false, 24)] private static void Torus(MenuCommand command) => CreateFromMenu(SDFShapeType.Torus, command);
        [MenuItem("GameObject/SDF/Capped Torus", false, 25)] private static void CappedTorus(MenuCommand command) => CreateFromMenu(SDFShapeType.CappedTorus, command);
        [MenuItem("GameObject/SDF/Link", false, 26)] private static void Link(MenuCommand command) => CreateFromMenu(SDFShapeType.Link, command);
        [MenuItem("GameObject/SDF/Infinite Cylinder", false, 27)] private static void InfiniteCylinder(MenuCommand command) => CreateFromMenu(SDFShapeType.InfiniteCylinder, command);
        [MenuItem("GameObject/SDF/Cone", false, 28)] private static void Cone(MenuCommand command) => CreateFromMenu(SDFShapeType.Cone, command);
        [MenuItem("GameObject/SDF/Infinite Cone", false, 29)] private static void InfiniteCone(MenuCommand command) => CreateFromMenu(SDFShapeType.InfiniteCone, command);
        [MenuItem("GameObject/SDF/Plane", false, 30)] private static void Plane(MenuCommand command) => CreateFromMenu(SDFShapeType.Plane, command);
        [MenuItem("GameObject/SDF/Hexagonal Prism", false, 31)] private static void HexagonalPrism(MenuCommand command) => CreateFromMenu(SDFShapeType.HexagonalPrism, command);
        [MenuItem("GameObject/SDF/Triangular Prism (Bound)", false, 32)] private static void TriangularPrismBound(MenuCommand command) => CreateFromMenu(SDFShapeType.TriangularPrismBound, command);
        [MenuItem("GameObject/SDF/Capsule", false, 33)] private static void Capsule(MenuCommand command) => CreateFromMenu(SDFShapeType.Capsule, command);
        [MenuItem("GameObject/SDF/Vertical Capsule", false, 34)] private static void VerticalCapsule(MenuCommand command) => CreateFromMenu(SDFShapeType.VerticalCapsule, command);
        [MenuItem("GameObject/SDF/Capped Cylinder", false, 35)] private static void CappedCylinder(MenuCommand command) => CreateFromMenu(SDFShapeType.CappedCylinder, command);
        [MenuItem("GameObject/SDF/Arbitrary Capped Cylinder", false, 36)] private static void ArbitraryCappedCylinder(MenuCommand command) => CreateFromMenu(SDFShapeType.ArbitraryCappedCylinder, command);
        [MenuItem("GameObject/SDF/Rounded Cylinder", false, 37)] private static void RoundedCylinder(MenuCommand command) => CreateFromMenu(SDFShapeType.RoundedCylinder, command);
        [MenuItem("GameObject/SDF/Capped Cone", false, 38)] private static void CappedCone(MenuCommand command) => CreateFromMenu(SDFShapeType.CappedCone, command);
        [MenuItem("GameObject/SDF/Arbitrary Capped Cone", false, 39)] private static void ArbitraryCappedCone(MenuCommand command) => CreateFromMenu(SDFShapeType.ArbitraryCappedCone, command);
        [MenuItem("GameObject/SDF/Solid Angle", false, 40)] private static void SolidAngle(MenuCommand command) => CreateFromMenu(SDFShapeType.SolidAngle, command);
        [MenuItem("GameObject/SDF/Cut Sphere", false, 41)] private static void CutSphere(MenuCommand command) => CreateFromMenu(SDFShapeType.CutSphere, command);
        [MenuItem("GameObject/SDF/Cut Hollow Sphere", false, 42)] private static void CutHollowSphere(MenuCommand command) => CreateFromMenu(SDFShapeType.CutHollowSphere, command);
        [MenuItem("GameObject/SDF/Death Star", false, 43)] private static void DeathStar(MenuCommand command) => CreateFromMenu(SDFShapeType.DeathStar, command);
        [MenuItem("GameObject/SDF/Round Cone", false, 44)] private static void RoundCone(MenuCommand command) => CreateFromMenu(SDFShapeType.RoundCone, command);
        [MenuItem("GameObject/SDF/Revolved Vesica", false, 45)] private static void RevolvedVesica(MenuCommand command) => CreateFromMenu(SDFShapeType.RevolvedVesica, command);
        [MenuItem("GameObject/SDF/Ellipsoid (Bound)", false, 46)] private static void EllipsoidBound(MenuCommand command) => CreateFromMenu(SDFShapeType.EllipsoidBound, command);
        [MenuItem("GameObject/SDF/Rhombus", false, 47)] private static void Rhombus(MenuCommand command) => CreateFromMenu(SDFShapeType.Rhombus, command);
        [MenuItem("GameObject/SDF/Octahedron", false, 48)] private static void Octahedron(MenuCommand command) => CreateFromMenu(SDFShapeType.Octahedron, command);
        [MenuItem("GameObject/SDF/Octahedron (Bound)", false, 49)] private static void OctahedronBound(MenuCommand command) => CreateFromMenu(SDFShapeType.OctahedronBound, command);
        [MenuItem("GameObject/SDF/Pyramid", false, 50)] private static void Pyramid(MenuCommand command) => CreateFromMenu(SDFShapeType.Pyramid, command);
        [MenuItem("GameObject/SDF/Triangle (Unsigned)", false, 51)] private static void TriangleUnsigned(MenuCommand command) => CreateFromMenu(SDFShapeType.TriangleUnsigned, command);
        [MenuItem("GameObject/SDF/Quad (Unsigned)", false, 52)] private static void QuadUnsigned(MenuCommand command) => CreateFromMenu(SDFShapeType.QuadUnsigned, command);

        private static void CreateFromMenu(SDFShapeType type, MenuCommand command) =>
            CreateShape(type, command?.context as GameObject);

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
