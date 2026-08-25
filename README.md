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

The built-in **URP Lit** material supports a Base Map, tangent-space Normal Map with strength,
Metallic Map, and a separate linear Roughness Map. SDFs use their procedural local XZ coordinates
as UVs and derive the tangent frame analytically from the contributing shape. Metallic maps use the
red channel; roughness maps use the red channel and are converted to smoothness with the material's
Smoothness value acting as a multiplier.

Open `Assets/SDF/Samples/SDFHeroDemo.unity` for the capture-ready portfolio scene. Enter Play Mode
to run its seamless 12-second camera and CSG animation loop, or rebuild all generated hero assets with
**Tools > SDF > Build Hero Demo Scene**.

The renderer never creates a reduced-resolution SDF target. Invisible instanced AABB geometry restricts fragment work, while the shader analytically sphere-traces inside each model volume and writes the hit's device depth.

URP depth/normal prepasses write the winning model ID and are resolved by one fullscreen PBR pass when supported, avoiding a second 10,000-instance AABB draw and full trace while retaining native resolution and an analytic fallback. Scene-view selection uses a refittable conservative-bounds BVH, so high-count animated scenes do not raymarch every shape merely to determine what is under the mouse.

Camera-facing AABB passes are GPU-frustum-culled and submitted indirectly. Only compacted visible model IDs reach vertex processing and rasterization; no visibility count is read back to the CPU. Every Game and Scene camera has a reusable visibility list while sharing the same scene buffers.

The `SDFBenchmarkController` supports up to 10,000 models. **Use GPU Driven Batch** is enabled by default: a compute pass updates production-format transforms, inverse matrices, CSG, modifiers, bounds, and full PBR materials directly in persistent GPU buffers, and both Scene and Game views consume them without CPU scene uploads. Disable it to compare the component authoring path. The **Animation** flags independently enable **Positions**, **Rotations**, **Scales**, **Materials**, **Operations**, **Modifiers**, any combination, or **Everything**; **Preview In Edit Mode** drives the same GPU update path in the Scene view.

In Play Mode, use the controller's **Run Benchmark Sweep** context command (or enable **Run Sweep On Start**) to measure Static, individual animation categories, combined transforms, and Everything under identical conditions. The CSV-formatted Console report includes wall-clock FPS, Unity CPU/GPU frame timings, GPU-buffer upload volume, and per-frame shape/model/operation/modifier/bounds refresh counts, then restores the original animation mode.

For a separate Editor measurement, open **Tools > SDF > Editor Performance Benchmark**, assign the benchmark controller, and run the complete Scene-view sweep outside Play Mode. It measures the native-resolution scene while orbiting, animating all data categories, changing selection, and applying inspector-style parameter edits. The report also records open Scene/Game views, Scene repaint rate, GC, upload volume, and granular refresh counts. Open a Game view or second Scene view before starting to include those simultaneous-view configurations.

On first successful Unity import, the setup generates the settings asset, installs the renderer feature, compiles the `.sdfshader` registry, and creates the four sample scenes under `Assets/SDF/Samples`. Run the `SDF.EditModeTests` and `SDF.PlayModeTests` assemblies from **Window > General > Test Runner** after the import finishes.

See [Architecture](Documentation/Architecture.md), [coverage](Documentation/SDFCoverage.md), [custom shaders](Documentation/CustomSDFShaders.md), and the [optimization audit](Documentation/OptimizationAudit.md).
