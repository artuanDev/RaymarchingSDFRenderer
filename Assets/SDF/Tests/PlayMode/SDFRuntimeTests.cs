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
            Object.Destroy(gameObject);
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
    }
}
