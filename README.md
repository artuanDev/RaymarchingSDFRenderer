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

URP depth/normal prepasses are reused by the PBR color pass when available, avoiding a second full trace while retaining native resolution and an analytic fallback. Scene-view selection uses a refittable conservative-bounds BVH, so high-count animated scenes do not raymarch every shape merely to determine what is under the mouse.

The `SDFBenchmarkController` supports up to 10,000 generated models. Its workload toggles add representative CSG operands and the complete modifier catalogue. The **Animation** flags can independently enable **Positions**, **Rotations**, **Scales**, **Materials**, **Operations**, **Modifiers**, any combination, or **Everything**. Transform animation and renderer matrix/bounds packing use Burst-compiled jobs; operation and modifier parameters use incremental buffer/bounds refresh paths rather than rebuilding topology.

In Play Mode, use the controller's **Run Benchmark Sweep** context command (or enable **Run Sweep On Start**) to measure Static, individual animation categories, combined transforms, and Everything under identical conditions. The CSV-formatted Console report includes wall-clock FPS, Unity CPU/GPU frame timings, GPU-buffer upload volume, and per-frame shape/model/operation/modifier/bounds refresh counts, then restores the original animation mode.

For a separate Editor measurement, open **Tools > SDF > Editor Performance Benchmark**, assign the benchmark controller, and run the complete Scene-view sweep outside Play Mode. It measures the native-resolution scene while orbiting, animating all data categories, changing selection, and applying inspector-style parameter edits. The report also records open Scene/Game views, Scene repaint rate, GC, upload volume, and granular refresh counts. Open a Game view or second Scene view before starting to include those simultaneous-view configurations.

On first successful Unity import, the setup generates the settings asset, installs the renderer feature, compiles the `.sdfshader` registry, and creates the four sample scenes under `Assets/SDF/Samples`. Run the `SDF.EditModeTests` and `SDF.PlayModeTests` assemblies from **Window > General > Test Runner** after the import finishes.

See [Architecture](Documentation/Architecture.md), [coverage](Documentation/SDFCoverage.md), [custom shaders](Documentation/CustomSDFShaders.md), and the [optimization audit](Documentation/OptimizationAudit.md).
