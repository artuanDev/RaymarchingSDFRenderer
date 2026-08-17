using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace SdfRenderer.Editor
{
    public sealed class SDFAuthoringWindow : EditorWindow
    {
        private ScrollView m_Palette;
        private TextField m_Search;
        private VisualElement m_InspectorHost;

        [MenuItem("Window/SDF Authoring")]
        public static void Open() => GetWindow<SDFAuthoringWindow>("SDF Authoring");

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 6;
            rootVisualElement.style.paddingRight = 6;
            Toolbar toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => SDFCreateMenus.CreateModel(null)) { text = "New Model" });
            toolbar.Add(new ToolbarButton(SDFCreateMenus.CreateMaterialAsset) { text = "New Material" });
            toolbar.Add(new ToolbarButton(SDFShaderCreation.Create) { text = "New SDF Shader" });
            toolbar.Add(new ToolbarButton(() => SDFProjectSetup.InstallRendererFeature()) { text = "Install Renderer" });
            rootVisualElement.Add(toolbar);
            m_Search = new TextField("Search");
            m_Search.RegisterValueChangedCallback(_ => RebuildPalette());
            rootVisualElement.Add(m_Search);

            TwoPaneSplitView split = new TwoPaneSplitView(0, 330, TwoPaneSplitViewOrientation.Horizontal);
            m_Palette = new ScrollView();
            m_InspectorHost = new ScrollView();
            split.Add(m_Palette);
            split.Add(m_InspectorHost);
            rootVisualElement.Add(split);
            split.style.flexGrow = 1;
            RebuildPalette();
            Selection.selectionChanged += RefreshSelection;
            RefreshSelection();
        }

        private void OnDisable() => Selection.selectionChanged -= RefreshSelection;

        private void RebuildPalette()
        {
            if (m_Palette == null) return;
            m_Palette.Clear();
            string query = m_Search?.value?.Trim() ?? string.Empty;
            foreach (SDFShapeType type in Enum.GetValues(typeof(SDFShapeType)))
            {
                string label = ObjectNames.NicifyVariableName(type.ToString());
                if (query.Length > 0 && label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Button button = new Button(() => SDFCreateMenus.CreateShape(type));
                button.style.height = 26;
                button.style.flexDirection = FlexDirection.Row;
                Texture icon = PrimitiveIcon(type);
                if (icon != null)
                {
                    Image image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
                    image.style.width = 20;
                    image.style.height = 20;
                    image.style.marginRight = 5;
                    button.Add(image);
                }
                button.Add(new Label(label));
                button.tooltip = "Create " + label + " in the selected SDF Model";
                m_Palette.Add(button);
            }
        }

        private static Texture PrimitiveIcon(SDFShapeType type)
        {
            string iconName;
            switch (type)
            {
                case SDFShapeType.Box:
                case SDFShapeType.RoundBox:
                case SDFShapeType.BoxFrame:
                case SDFShapeType.TriangularPrismBound:
                case SDFShapeType.Pyramid:
                    iconName = "d_PreMatCube";
                    break;
                case SDFShapeType.InfiniteCylinder:
                case SDFShapeType.CappedCylinder:
                case SDFShapeType.ArbitraryCappedCylinder:
                case SDFShapeType.RoundedCylinder:
                case SDFShapeType.Cone:
                case SDFShapeType.CappedCone:
                    iconName = "d_PreMatCylinder";
                    break;
                default:
                    iconName = "d_PreMatSphere";
                    break;
            }
            return EditorGUIUtility.IconContent(iconName).image;
        }

        private void RefreshSelection()
        {
            if (m_InspectorHost == null) return;
            m_InspectorHost.Clear();
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                m_InspectorHost.Add(new HelpBox("Select an SDF model or shape to edit it here.", HelpBoxMessageType.Info));
                return;
            }
            m_InspectorHost.Add(new Label(selected.name) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 15 } });
            m_InspectorHost.Add(new InspectorElement(selected));
            SDFShape shape = selected.GetComponent<SDFShape>();
            if (shape == null) return;
            AddStackValidation(shape);
            AddOperandControls(shape);
            AddModifierControls(shape);
            if (selected.GetComponent<SDFCustomMaterial>() == null)
                m_InspectorHost.Add(new Button(() => { Undo.AddComponent<SDFCustomMaterial>(selected); RefreshSelection(); }) { text = "Add Custom Material Override" });
            m_InspectorHost.Add(new Label("Add Modifier") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            foreach (SDFModifierType type in Enum.GetValues(typeof(SDFModifierType)))
                m_InspectorHost.Add(new Button(() => { SDFCreateMenus.AddModifier(selected, type); RefreshSelection(); }) { text = ObjectNames.NicifyVariableName(type.ToString()) });
        }

        private void AddOperandControls(SDFShape shape)
        {
            m_InspectorHost.Add(new Label("Operand Order") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            VisualElement row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(new Button(() => MoveOperand(shape, -1)) { text = "Earlier", tooltip = "Move this operand earlier in the model's deterministic hierarchy fold." });
            row.Add(new Button(() => MoveOperand(shape, 1)) { text = "Later", tooltip = "Move this operand later in the model's deterministic hierarchy fold." });
            row.Add(new Button(() => { Undo.DestroyObjectImmediate(shape.gameObject); RefreshSelection(); }) { text = "Remove Operand" });
            m_InspectorHost.Add(row);
        }

        private void AddModifierControls(SDFShape shape)
        {
            SDFModifier[] modifiers = shape.GetComponents<SDFModifier>();
            if (modifiers.Length == 0) return;
            m_InspectorHost.Add(new Label("Modifier Order (top to bottom)") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            for (int i = 0; i < modifiers.Length; ++i)
            {
                SDFModifier modifier = modifiers[i];
                VisualElement row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                Label name = new Label((i + 1) + ". " + ObjectNames.NicifyVariableName(modifier.Type.ToString()));
                name.style.flexGrow = 1;
                row.Add(name);
                row.Add(new Button(() => { ComponentUtility.MoveComponentUp(modifier); RefreshSelection(); }) { text = "↑" });
                row.Add(new Button(() => { ComponentUtility.MoveComponentDown(modifier); RefreshSelection(); }) { text = "↓" });
                row.Add(new Button(() => { Undo.DestroyObjectImmediate(modifier); RefreshSelection(); }) { text = "Remove" });
                m_InspectorHost.Add(row);
            }
        }

        private void AddStackValidation(SDFShape shape)
        {
            SDFOperation operation = shape.GetComponent<SDFOperation>();
            if (IsFirstOperand(shape) && operation != null && operation.Type != SDFOperationType.Union && operation.Type != SDFOperationType.SmoothUnion)
                m_InspectorHost.Add(new HelpBox("The first operand seeds the fold, so its operation is ignored. Move it later or use Union.", HelpBoxMessageType.Warning));
            if ((shape.IsUnbounded || HasModifier(shape, SDFModifierType.InfiniteRepeat)) && shape.ClipBounds.size.sqrMagnitude < 0.001f)
                m_InspectorHost.Add(new HelpBox("This stack is analytically unbounded and needs non-zero Clip Bounds.", HelpBoxMessageType.Error));
            if (HasModifier(shape, SDFModifierType.Twist) || HasModifier(shape, SDFModifierType.Bend) || HasModifier(shape, SDFModifierType.Revolution))
                m_InspectorHost.Add(new HelpBox("This modifier stack disables per-operand bounds-distance skipping to preserve correctness.", HelpBoxMessageType.Info));
        }

        private static bool HasModifier(SDFShape shape, SDFModifierType type)
        {
            foreach (SDFModifier modifier in shape.GetComponents<SDFModifier>())
                if (modifier.isActiveAndEnabled && modifier.Type == type) return true;
            return false;
        }

        private static bool IsFirstOperand(SDFShape shape)
        {
            Transform parent = shape.transform.parent;
            if (parent == null) return true;
            for (int i = 0; i < parent.childCount; ++i)
            {
                SDFShape sibling = parent.GetChild(i).GetComponent<SDFShape>();
                if (sibling != null && sibling.isActiveAndEnabled)
                    return sibling == shape;
            }
            return true;
        }

        private void MoveOperand(SDFShape shape, int direction)
        {
            Transform transform = shape.transform;
            int target = Mathf.Clamp(transform.GetSiblingIndex() + direction, 0, transform.parent != null ? transform.parent.childCount - 1 : 0);
            if (target == transform.GetSiblingIndex()) return;
            Undo.RecordObject(transform, "Reorder SDF Operand");
            transform.SetSiblingIndex(target);
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Topology | SDFDirtyFlags.Bounds);
            RefreshSelection();
        }
    }
}
