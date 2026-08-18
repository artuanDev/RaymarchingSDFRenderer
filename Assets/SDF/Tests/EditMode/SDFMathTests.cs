using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace SdfRenderer.Tests
{
    public sealed class SDFMathTests
    {
        [Test]
        public void SphereHasExpectedSignAndDistance()
        {
            Vector4 parameters = new Vector4(1f, 0f, 0f, 0f);
            Assert.That(SDFMath.EvaluatePrimitive(Vector3.zero, SDFShapeType.Sphere, parameters, default, default, default), Is.EqualTo(-1f).Within(1e-6f));
            Assert.That(SDFMath.EvaluatePrimitive(Vector3.right, SDFShapeType.Sphere, parameters, default, default, default), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(SDFMath.EvaluatePrimitive(Vector3.right * 3f, SDFShapeType.Sphere, parameters, default, default, default), Is.EqualTo(2f).Within(1e-6f));
        }

        [Test]
        public void BoxHasExpectedSurfaceDistance()
        {
            Vector4 size = new Vector4(1f, 2f, 3f, 0f);
            Assert.That(SDFMath.EvaluatePrimitive(Vector3.zero, SDFShapeType.Box, default, size, default, default), Is.EqualTo(-1f).Within(1e-6f));
            Assert.That(SDFMath.EvaluatePrimitive(new Vector3(1f, 0f, 0f), SDFShapeType.Box, default, size, default, default), Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void SmoothUnionReturnsMatchingGeometryWeight()
        {
            float distance = SDFMath.SmoothUnion(-0.1f, 0.1f, 0.4f, out float currentWeight);
            Assert.That(currentWeight, Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(distance, Is.EqualTo(-0.125f).Within(1e-6f));
        }

        [Test]
        public void EveryPrimitiveProducesFiniteValueForNonDegenerateDefaults()
        {
            GameObject gameObject = new GameObject("SDF Test Shape");
            try
            {
                SDFShape shape = gameObject.AddComponent<SDFShape>();
                foreach (SDFShapeType type in System.Enum.GetValues(typeof(SDFShapeType)))
                {
                    shape.ShapeType = type;
                    float value = shape.EvaluateLocal(new Vector3(0.13f, 0.27f, -0.19f));
                    Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False, type.ToString());
                    Bounds bounds = shape.GetLocalBounds();
                    Assert.That(bounds.size.x > 0f && bounds.size.y > 0f && bounds.size.z > 0f, type.ToString());
                }
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static IEnumerable<TestCaseData> DefaultSurfaceSamples()
        {
            const float r = 0.5f, h = 1f, ra = 0.75f, rb = 0.2f, thickness = 0.1f;
            float angle = 45f * Mathf.Deg2Rad;
            Vector3 pointA = new Vector3(0f, -0.5f, 0f), pointB = new Vector3(0f, 0.5f, 0f);
            yield return Surface(SDFShapeType.Sphere, new Vector3(r, 0f, 0f));
            yield return Surface(SDFShapeType.Box, new Vector3(0.5f, 0f, 0f));
            yield return Surface(SDFShapeType.RoundBox, new Vector3(0.6f, 0f, 0f));
            yield return Surface(SDFShapeType.BoxFrame, new Vector3(0.5f, 0.5f, 0f));
            yield return Surface(SDFShapeType.Torus, new Vector3(ra + rb, 0f, 0f));
            yield return Surface(SDFShapeType.CappedTorus, new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * (ra + rb));
            yield return Surface(SDFShapeType.Link, new Vector3(ra + rb, h, 0f));
            yield return Surface(SDFShapeType.InfiniteCylinder, new Vector3(r, 0f, 0f));
            yield return Surface(SDFShapeType.Cone, new Vector3(0f, h, 0f));
            yield return Surface(SDFShapeType.InfiniteCone, Vector3.zero);
            yield return Surface(SDFShapeType.Plane, Vector3.zero);
            yield return Surface(SDFShapeType.HexagonalPrism, new Vector3(0f, ra, 0f));
            yield return Surface(SDFShapeType.TriangularPrismBound, new Vector3(0f, 0f, 0.5f));
            yield return Surface(SDFShapeType.Capsule, new Vector3(r, 0f, 0f));
            yield return Surface(SDFShapeType.VerticalCapsule, new Vector3(r, h * 0.5f, 0f));
            yield return Surface(SDFShapeType.CappedCylinder, new Vector3(r, 0f, 0f));
            yield return Surface(SDFShapeType.ArbitraryCappedCylinder, new Vector3(r, 0f, 0f));
            yield return Surface(SDFShapeType.RoundedCylinder, new Vector3(ra, 0f, 0f));
            yield return Surface(SDFShapeType.CappedCone, new Vector3(ra, -h, 0f));
            yield return Surface(SDFShapeType.ArbitraryCappedCone, pointA + Vector3.right * ra);
            yield return Surface(SDFShapeType.SolidAngle, new Vector3(0f, r, 0f));
            yield return Surface(SDFShapeType.CutSphere, new Vector3(r, 0f, 0f));
            yield return Surface(SDFShapeType.CutHollowSphere, new Vector3(r + thickness, 0f, 0f));
            yield return Surface(SDFShapeType.DeathStar, new Vector3(-ra, 0f, 0f));
            yield return Surface(SDFShapeType.RoundCone, pointA + Vector3.down * ra);
            yield return Surface(SDFShapeType.RevolvedVesica, pointB);
            yield return Surface(SDFShapeType.EllipsoidBound, new Vector3(0.5f, 0f, 0f));
            yield return Surface(SDFShapeType.Rhombus, new Vector3(0f, h, 0f));
            yield return Surface(SDFShapeType.Octahedron, new Vector3(r, 0f, 0f));
            yield return Surface(SDFShapeType.OctahedronBound, new Vector3(r, 0f, 0f));
            yield return Surface(SDFShapeType.Pyramid, new Vector3(0f, h, 0f));
            yield return Surface(SDFShapeType.TriangleUnsigned, new Vector3(0.1f, 0f, thickness));
            yield return Surface(SDFShapeType.QuadUnsigned, new Vector3(0f, 0f, thickness));
        }

        private static TestCaseData Surface(SDFShapeType type, Vector3 point) =>
            new TestCaseData(type, point).SetName(type + "_DefaultKnownSurface");

        [TestCaseSource(nameof(DefaultSurfaceSamples))]
        public void EveryPrimitiveHasAKnownDefaultSurfaceSample(SDFShapeType type, Vector3 point)
        {
            GameObject gameObject = new GameObject(type.ToString());
            try
            {
                SDFShape shape = gameObject.AddComponent<SDFShape>();
                shape.ShapeType = type;
                Assert.That(shape.EvaluateLocal(point), Is.EqualTo(0f).Within(2e-4f), type.ToString());
            }
            finally { Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void EveryFinitePrimitiveHasConservativeCornerBounds()
        {
            GameObject gameObject = new GameObject("SDF Bounds Test");
            try
            {
                SDFShape shape = gameObject.AddComponent<SDFShape>();
                foreach (SDFShapeType type in System.Enum.GetValues(typeof(SDFShapeType)))
                {
                    shape.ShapeType = type;
                    if (shape.IsUnbounded) continue;
                    Bounds bounds = shape.GetLocalBounds();
                    for (int corner = 0; corner < 8; ++corner)
                    {
                        Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                        Assert.That(shape.EvaluateLocal(point), Is.GreaterThanOrEqualTo(-2e-4f), type + " corner " + corner);
                    }
                }
            }
            finally { Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void HardSubtractionSelectsCutterWhenCutSurfaceWins()
        {
            float result = SDFMath.Combine(-0.6f, -0.2f, SDFOperationType.Subtraction, 0f, out float currentWeight);
            Assert.That(result, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(currentWeight, Is.Zero);
        }

        [Test]
        public void NestedSmoothBlendPreservesAllPreviousContributors()
        {
            SDFMath.Combine(-0.1f, 0.1f, SDFOperationType.SmoothUnion, 0.4f, out float firstCurrentWeight);
            SDFMath.Combine(-0.125f, -0.05f, SDFOperationType.SmoothIntersection, 0.3f, out float accumulatedWeight);
            float firstMaterialWeight = accumulatedWeight * firstCurrentWeight;
            float secondMaterialWeight = accumulatedWeight * (1f - firstCurrentWeight);
            float thirdMaterialWeight = 1f - accumulatedWeight;
            Assert.That(firstMaterialWeight + secondMaterialWeight + thirdMaterialWeight, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(firstMaterialWeight, Is.GreaterThan(0f));
            Assert.That(secondMaterialWeight, Is.GreaterThan(0f));
            Assert.That(thirdMaterialWeight, Is.GreaterThan(0f));
        }

        [Test]
        public void BatchedDirtyChangesProduceOneVersionWithCombinedUploadFlags()
        {
            uint before = SDFSceneRegistry.Version;
            using (SDFSceneRegistry.BatchChanges())
            {
                SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Materials);
                SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Settings);
                Assert.That(SDFSceneRegistry.Version, Is.EqualTo(before));
            }
            Assert.That(SDFSceneRegistry.Version, Is.EqualTo(unchecked(before + 1)));
            Assert.That(SDFSceneRegistry.GetDirtyFlagsSince(before), Is.EqualTo(SDFDirtyFlags.Materials | SDFDirtyFlags.Settings));
        }

        [Test]
        public void RuntimeOperationAndModifierParametersUseIncrementalDirtyFlags()
        {
            GameObject gameObject = new GameObject("Dynamic SDF Components");
            try
            {
                SDFOperation operation = gameObject.AddComponent<SDFOperation>();
                SDFModifier modifier = gameObject.AddComponent<SDFModifier>();
                uint beforeOperation = SDFSceneRegistry.Version;
                operation.Type = SDFOperationType.SmoothSubtraction;
                operation.Smoothness = 0.37f;
                Assert.That(SDFSceneRegistry.GetDirtyFlagsSince(beforeOperation), Is.EqualTo(SDFDirtyFlags.Operations));

                uint beforeModifier = SDFSceneRegistry.Version;
                modifier.Type = SDFModifierType.Twist;
                modifier.Amount = 0.42f;
                Assert.That(SDFSceneRegistry.GetDirtyFlagsSince(beforeModifier), Is.EqualTo(SDFDirtyFlags.Modifiers));
            }
            finally { Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void BenchmarkEverythingIncludesOperationsAndModifiers()
        {
            Assert.That((SDFBenchmarkAnimation.Everything & SDFBenchmarkAnimation.Operations) != 0, Is.True);
            Assert.That((SDFBenchmarkAnimation.Everything & SDFBenchmarkAnimation.Modifiers) != 0, Is.True);
        }

        [Test]
        public void CpuScenePickerRayHitsAnalyticSphereSurface()
        {
            GameObject gameObject = new GameObject("Pickable SDF Sphere");
            try
            {
                SDFShape shape = gameObject.AddComponent<SDFShape>();
                Ray ray = new Ray(new Vector3(0f, 0f, -3f), Vector3.forward);
                Assert.That(SDFCpuEvaluator.Raycast(shape, ray, out float distance), Is.True);
                Assert.That(distance, Is.EqualTo(2.5f).Within(0.002f));
            }
            finally { Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void CachedPickerLocalBoundsRefitMatchesFullWorldBoundsEvaluation()
        {
            GameObject gameObject = new GameObject("Animated Pick Bounds");
            try
            {
                SDFShape shape = gameObject.AddComponent<SDFShape>();
                SDFModifier modifier = gameObject.AddComponent<SDFModifier>();
                modifier.Type = SDFModifierType.Elongate;
                modifier.Vector = new Vector3(0.4f, 0.1f, 0.2f);
                Bounds localBounds = SDFCpuEvaluator.GetLocalBounds(shape);

                gameObject.transform.SetPositionAndRotation(new Vector3(3f, -2f, 5f), Quaternion.Euler(17f, 31f, 9f));
                gameObject.transform.localScale = new Vector3(1.5f, 0.75f, 2f);
                Bounds cachedRefit = SDFCpuEvaluator.GetWorldBounds(shape, localBounds);
                Bounds fullEvaluation = SDFCpuEvaluator.GetWorldBounds(shape);

                Assert.That(cachedRefit.center, Is.EqualTo(fullEvaluation.center));
                Assert.That(cachedRefit.extents, Is.EqualTo(fullEvaluation.extents));
            }
            finally { Object.DestroyImmediate(gameObject); }
        }
    }
}
