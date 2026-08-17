# SDF coverage

Source of truth: Íñigo Quílez, [3D distance functions](https://iquilezles.org/articles/distfunctions/). The source distinguishes exact signed distance, conservative bounds, and unsigned distances; this implementation preserves those categories.

`Half extents`, `half height`, and similar parameters follow the source equations. Every unbounded primitive exposes `Clip Bounds`, which limits rendering but does not alter the analytic distance.

The CPU evaluator, GPU dispatch, known-surface test case, finite-value test, and conservative corner-bound test are authored for all 33 entries. “Tested” below means a focused analytic assertion is present; Unity-native execution and desktop shader compilation must still be completed by the Test Runner on the imported project.

| Display name | Source function | Kind | Parameters | Extent | Conservative local bounds | Implementation |
|---|---|---|---|---|---|---|
| Sphere | `sdSphere` | Exact | radius > 0 | Finite | radius cube | CPU/GPU implemented; analytic test authored |
| Box | `sdBox` | Exact | half extents > 0 | Finite | half extents | CPU/GPU implemented; analytic test authored |
| Round Box | `sdRoundBox` | Exact | half extents, radius >= 0 | Finite | extents + radius | GPU + CPU |
| Box Frame | `sdBoxFrame` | Exact | half extents, edge thickness > 0 | Finite | extents + thickness | GPU + CPU |
| Torus | `sdTorus` | Exact | major/minor radius > 0 | Finite | analytic torus extents | GPU + CPU |
| Capped Torus | `sdCappedTorus` | Exact | major/minor radius, cap angle | Finite | full torus extents | GPU |
| Link | `sdLink` | Exact | half length, major/minor radius | Finite | analytic link extents | GPU + CPU |
| Infinite Cylinder | `sdCylinder` | Exact | radius > 0, clip bounds | Infinite | explicit clip bounds | GPU + CPU |
| Cone | `sdCone` | Exact | half height, base radius | Finite | enclosing cone box | GPU |
| Infinite Cone | `sdCone` | Exact | angle, clip bounds | Infinite | explicit clip bounds | GPU |
| Plane | `sdPlane` | Exact | normalized normal, offset, clip bounds | Infinite | explicit clip bounds | GPU + CPU |
| Hexagonal Prism | `sdHexPrism` | Exact | radius, half height | Finite | enclosing box | GPU |
| Triangular Prism | `sdTriPrism` | Bound | prism parameters | Finite | enclosing box | GPU + CPU bound |
| Capsule / Line | `sdCapsule` | Exact | endpoints, radius | Finite | segment AABB + radius | GPU + CPU |
| Vertical Capsule | `sdVerticalCapsule` | Exact | height, radius | Finite | analytic capsule AABB | GPU + CPU |
| Capped Cylinder | `sdCappedCylinder` | Exact | half height, radius | Finite | cylinder box | GPU + CPU |
| Arbitrary Capped Cylinder | `sdCappedCylinder` | Exact | endpoints, radius | Finite | segment AABB + radius | GPU |
| Rounded Cylinder | `sdRoundedCylinder` | Exact | half height, body/edge radius | Finite | expanded cylinder box | GPU + CPU |
| Capped Cone | `sdCappedCone` | Exact | half height, endpoint radii | Finite | maximum-radius box | GPU |
| Arbitrary Capped Cone | `sdCappedCone` | Exact | endpoints, endpoint radii | Finite | segment AABB + max radius | GPU |
| Solid Angle | `sdSolidAngle` | Exact | radius, angle | Finite | radius cube | GPU |
| Cut Sphere | `sdCutSphere` | Exact | radius, cut height | Finite | sphere box | GPU |
| Cut Hollow Sphere | `sdCutHollowSphere` | Exact | radius, cut height, thickness | Finite | expanded sphere box | GPU + CPU |
| Death Star | `sdDeathStar` | Exact | radii, center distance | Finite | union envelope | GPU |
| Round Cone | `sdRoundCone` | Exact | endpoints, endpoint radii | Finite | segment AABB + max radius | GPU |
| Revolved Vesica | `sdVesicaSegment` | Exact | endpoints, width | Finite | conservative segment envelope | GPU |
| Ellipsoid | `sdEllipsoid` | Bound | three radii | Finite | radii box | GPU + CPU bound |
| Rhombus | `sdRhombus` | Exact | diagonals, half height, rounding | Finite | enclosing box | GPU |
| Octahedron | `sdOctahedron` | Exact | size | Finite | size cube | GPU + CPU |
| Octahedron (bound) | `sdOctahedron` | Bound | size | Finite | size cube | GPU + CPU bound |
| Pyramid | `sdPyramid` | Exact | height | Finite | base/apex box | GPU |
| Triangle | `udTriangle` | Unsigned | 3 points, thickness | Finite | point AABB + thickness | GPU + CPU |
| Quad | `udQuad` | Unsigned | 4 points, thickness | Finite | point AABB + thickness | GPU + CPU |

## Operations

Union, subtraction, intersection, polynomial smooth union, smooth subtraction, and smooth intersection are implemented as ordered components. Geometry and final shaded radiance share the exact smooth weight.

## Modifiers

Round, onion, elongation, mirror/symmetry, finite repetition, clipped infinite repetition, twist, bend, revolution, and extrusion are component stacks. Domain modifiers retain inspector order; distance modifiers run after primitive evaluation. Twist, bend, revolution, and infinite repetition explicitly disable operand-distance AABB skipping.
