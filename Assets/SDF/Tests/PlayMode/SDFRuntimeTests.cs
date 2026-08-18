using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SdfRenderer.Tests
{
    public sealed class SDFRuntimeTests
    {
        [UnityTest]
        public IEnumerator RuntimePropertyChangeMarksSceneDirty()
        {
            GameObject gameObject = new GameObject("Runtime SDF");
            gameObject.AddComponent<SDFModel>();
            SDFShape shape = gameObject.AddComponent<SDFShape>();
            yield return null;
            uint before = SDFSceneRegistry.Version;
            shape.Radius = 2f;
            Assert.That(SDFSceneRegistry.Version, Is.GreaterThan(before));
            Assert.That(SDFSceneRegistry.GetDirtyFlagsSince(before),
                Is.EqualTo(SDFDirtyFlags.Shapes | SDFDirtyFlags.Bounds));
            Object.Destroy(gameObject);
        }

        [Test]
        public void AssigningUnchangedShapeValuesDoesNotInvalidateTheScene()
        {
            GameObject gameObject = new GameObject("Stable Runtime SDF");
            try
            {
                SDFShape shape = gameObject.AddComponent<SDFShape>();
                uint before = SDFSceneRegistry.Version;
                shape.ShapeType = shape.ShapeType;
                shape.Radius = shape.Radius;
                shape.Size = shape.Size;
                shape.Roundness = shape.Roundness;
                shape.Height = shape.Height;
                shape.ClipBounds = shape.ClipBounds;
                Assert.That(SDFSceneRegistry.Version, Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void RuntimeMaterialAnimationIsBatchedAndMarksOnlyMaterialsDirty()
        {
            SDFMaterialAsset material = ScriptableObject.CreateInstance<SDFMaterialAsset>();
            try
            {
                uint before = SDFSceneRegistry.Version;
                using (SDFSceneRegistry.BatchChanges())
                {
                    material.BaseColor = Color.red;
                    material.Emission = Color.red * 0.1f;
                    material.Smoothness = 0.8f;
                }
                Assert.That(SDFSceneRegistry.Version, Is.EqualTo(unchecked(before + 1)));
                Assert.That(SDFSceneRegistry.GetDirtyFlagsSince(before), Is.EqualTo(SDFDirtyFlags.Materials));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void NewMaterialsAndLightingSettingsUseTheUrpLitPathByDefault()
        {
            SDFMaterialAsset material = ScriptableObject.CreateInstance<SDFMaterialAsset>();
            SDFRenderSettings settings = ScriptableObject.CreateInstance<SDFRenderSettings>();
            try
            {
                Assert.That(material.ShadingModel, Is.EqualTo(SDFShadingModel.PbrLike));
                Assert.That(material.Occlusion, Is.EqualTo(1f));
                Assert.That(settings.ReceiveUrpShadows, Is.True);
                Assert.That(settings.CastMainLightShadows, Is.True);
                Assert.That(settings.UseUrpScreenSpaceAo, Is.True);
                Assert.That(settings.SdfAmbientOcclusion, Is.True);
                Assert.That(settings.ReuseDepthNormalPrepass, Is.True);
                Assert.That(settings.AmbientOcclusionStrength, Is.GreaterThan(0f));
                Assert.That(settings.AmbientOcclusionRadius, Is.GreaterThan(0f));
                Assert.That(settings.AmbientOcclusionSamples, Is.InRange(2, 6));
                Assert.That(settings.ShadowMaxSteps, Is.GreaterThan(0));
                Assert.That(settings.ShadowSoftness, Is.GreaterThan(0f));
                Assert.That(settings.AmbientColor.maxColorComponent, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(settings);
            }
        }
    }
}
