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
    }
}
