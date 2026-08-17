using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SdfRenderer.Tests
{
    public sealed class SDFEditorTests
    {
        [Test]
        public void ShaderImporterReportsMissingEntryPointAndUnsupportedProperties()
        {
            string missingEntry = "SDFShader \"Bad\" { Properties { } HLSLPROGRAM float3 Wrong() { return 0; } ENDHLSL }";
            Assert.That(SdfRenderer.Editor.SDFShaderImporter.ValidateSource(missingEntry), Does.Contain("SDFSurface"));
            string unsupported = "SDFShader \"Bad\" { Properties { _Gloss (\"Gloss\", Float) = 1 } HLSLPROGRAM float3 SDFSurface(SDFSurfaceContext c, SDFMaterialGpu m) { return 0; } ENDHLSL }";
            Assert.That(SdfRenderer.Editor.SDFShaderImporter.ValidateSource(unsupported), Does.Contain("Unsupported property"));
        }

        [Test]
        public void CreationCommandBuildsSerializableOrderedOperand()
        {
            GameObject model = SdfRenderer.Editor.SDFCreateMenus.CreateModel(null);
            try
            {
                SDFShape shape = SdfRenderer.Editor.SDFCreateMenus.CreateShape(SDFShapeType.CappedCone, model);
                Assert.That(shape.ShapeType, Is.EqualTo(SDFShapeType.CappedCone));
                Assert.That(shape.transform.parent, Is.EqualTo(model.transform));
                Assert.That(shape.GetComponent<SDFOperation>(), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(model); }
        }

        [Test]
        public void EveryPrimitiveHasAHierarchyCreationMenu()
        {
            foreach (SDFShapeType type in System.Enum.GetValues(typeof(SDFShapeType)))
            {
                MethodInfo method = typeof(SdfRenderer.Editor.SDFCreateMenus).GetMethod(
                    type.ToString(), BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null, $"Missing creation command for {type}.");
                MenuItem menu = method.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>().SingleOrDefault();
                Assert.That(menu, Is.Not.Null, $"Missing GameObject menu registration for {type}.");
                Assert.That(menu.menuItem, Does.StartWith("GameObject/SDF/"));
            }
        }

        [Test]
        public void PrimaryDesktopRaymarchShaderHasNoCompilerErrors()
        {
            Shader shader = Shader.Find("Hidden/SDF/URPVolumeRaymarch");
            Assert.That(shader, Is.Not.Null);
            var errors = ShaderUtil.GetShaderMessages(shader)
                .Where(message => message.severity.ToString() == "Error")
                .ToArray();
            Assert.That(errors, Is.Empty, string.Join("\n", errors.Select(error => error.message)));
        }
    }
}
