# Full-resolution SDF Renderer for Unity 6 URP

This project contains an analytic, GPU-buffer-driven signed-distance-field renderer for Unity 6000.3 and URP 17.3. SDF models render directly into URP's active native-resolution color and depth targets in both Scene and Game cameras.

## Quick start

1. Open the project and allow scripts/shaders to compile.
2. If necessary, choose **Tools > SDF > Install Renderer Feature**. The setup also runs once automatically and installs the feature into each `UniversalRendererData` asset.
3. Open **Window > SDF Authoring**.
4. Click **New Model**, then choose primitives from the searchable palette.
5. Click rendered SDF surfaces directly in the Scene view to select their shape components. Shift-click adds to the selection and Ctrl/Cmd-click toggles it; the behavior can be disabled under **Tools > SDF > Enable Scene Picking**.
6. Add `SDFModifier` components or change the `SDFOperation` on each operand.
7. Create a material with **Assets > Create > SDF > Material** and assign it to a shape.
8. Create a custom surface module with **Assets > Create > SDF Shader**.

The renderer never creates a reduced-resolution SDF target. Invisible instanced AABB geometry restricts fragment work, while the shader analytically sphere-traces inside each model volume and writes the hit's device depth.

The `SDFBenchmarkController` supports up to 10,000 generated models. Its **Animation** flags can independently enable **Positions**, **Rotations**, **Scales**, **Materials**, any combination, or **Everything**. Transform animation and renderer matrix/bounds packing use Burst-compiled transform jobs across worker threads; they do not rebuild shape topology, modifiers, or materials each frame.

On first successful Unity import, the setup generates the settings asset, installs the renderer feature, compiles the `.sdfshader` registry, and creates the four sample scenes under `Assets/SDF/Samples`. Run the `SDF.EditModeTests` and `SDF.PlayModeTests` assemblies from **Window > General > Test Runner** after the import finishes.

See [Architecture](Documentation/Architecture.md), [coverage](Documentation/SDFCoverage.md), [custom shaders](Documentation/CustomSDFShaders.md), and the [optimization audit](Documentation/OptimizationAudit.md).
