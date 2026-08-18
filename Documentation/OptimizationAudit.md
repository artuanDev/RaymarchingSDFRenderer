# Previous-project optimization audit

Reference inspected read-only: `D:\Unity\SDF_Rendering`.

## Retained and redesigned

| Technique in previous attempt | New implementation |
|---|---|
| Packed model/shape/modifier structured buffers | Retained and extended with operation, material, custom-shader, and texture indices. |
| Dirty scene version | Retained, split into diagnostic dirty flags, and paired with edit/play transform checks. Transform animation now takes a cached matrix/bounds-only upload path instead of recompiling topology. |
| Batched changes | Retained through `SDFSceneRegistry.BatchChanges()`. |
| Geometric buffer growth | Retained; capacities grow to powers of two and resources are reused. |
| Dynamic buffer uploads | Extended with a three-buffer model/shape ring to avoid overwriting buffers still consumed by preceding GPU frames. |
| Dynamic transform packing | Extended with Burst-compiled `IJobParallelForTransform` shape packing and parallel model-bounds aggregation. The component benchmark also animates real Unity transforms through a Burst transform job. |
| Dynamic CSG and modifiers | Added incremental operation and modifier refreshes, including conservative local/world-bound updates, without rediscovering or recompiling topology. |
| Procedural 36-vertex model bounds | Retained as instanced invisible proxy geometry. |
| Ray/AABB entry and exit | Retained, including inside-camera winding handling. |
| Per-shape AABB skip | Retained only for operations and modifiers for which the bound remains conservative. |
| Nonuniform-scale correction | Retained using minimum absolute scale for safe world stepping. |
| Pixel-aware epsilon | Retained for hit and tetrahedral normal samples. |
| Compute-resident two-shape benchmark | Generalized into a GPU-resident producer that writes this renderer's full production layouts. It uses the existing analytic shader and PBR path rather than the reference project's reduced shape/material shader. The component path remains available for comparison and arbitrary authoring. |
| Profiler markers | Retained for CPU compilation/upload and GPU raymarch pass, with a separate `SDF/CPU Refresh Transforms` marker for dynamic-scene profiling. |

## Current high-count optimization pass

- Scene-view selection now uses a refittable BVH instead of analytically raymarching every registered shape during every IMGUI Layout event.
- Shape parameter edits remain incremental and no longer rebuild scene topology.
- Operation and modifier revisions skip unchanged component packing.
- Modifier-only changes update affected bounds from cached matrices without traversing every Unity transform.
- When a depth/normal prepass exists, the PBR color pass validates and reuses its hit and normal rather than tracing the same model twice.
- Runtime and Editor benchmark sweeps report CPU/GPU timing, allocation, upload, and granular refresh telemetry; the Editor sweep additionally exercises Scene-view orbit, selection churn, and inspector-style edits without substituting a reduced preview.

## Second pass after first-profile miss

The first granular-dirty pass did not materially improve the reported benchmark, demonstrating that topology discovery was not the dominant cost in this workload. The second pass targets the remaining full-frame work:

- Triple-buffered dynamic GPU data is now filled with `GraphicsBuffer.LockBufferForWrite` plus Burst copy jobs rather than full `SetData` driver uploads.
- Combined transform and modifier animation no longer performs two complete world-bounds refits.
- The depth/normal pass writes a stable model-ID attachment. PBR color is resolved once per SDF pixel with a fullscreen triangle instead of rasterizing all model AABBs again and validating each candidate analytically.
- The benchmark CSV now records animation, renderer-refresh, modifier/operation-refresh, and upload marker time separately so the next measured result distinguishes component animation, CPU packing/upload, and GPU cost.

Sparse world-space distance bricks from the reference SDF-engine approach are not used as the universal representation. The benchmark deliberately moves and modifies all 10,000 independent models, which would invalidate most world-space bricks every frame. The renderer retains analytic local-space instances for this workload and leaves sparse brick caching as a future hybrid path for large, spatially coherent edit stacks where measured cache reuse justifies its memory and update cost.

## Third pass: GPU-resident animation

The measured rise to roughly 30 FPS still left the CPU component-animation and full-buffer production path active. Inspection of `D:\Unity\SDF_Rendering` showed that its high-count result came primarily from persistent UAV buffers plus one compute dispatch: transforms, inverse matrices, CSG parameters, modifier parameters, materials, shape bounds, and model bounds never returned to the CPU.

The benchmark now has a **Use GPU Driven Batch** mode enabled by default. One versioned compute pass writes the current renderer's 32-byte models, 176-byte shapes, 48-byte modifiers, and 112-byte PBR materials in place. Animated state sent by the CPU is only time, flags, counts, and spacing; there is no hierarchy traversal, Transform job, managed component loop, or per-frame structured-buffer upload. The same buffers feed native-resolution Scene/Game rendering, model-ID resolve, URP lighting, SSAO, and shadow integration. Multiple cameras reuse the update, and mixed component/GPU sources receive distinct model-ID ranges.

This is a real execution-path change, not resolution scaling, frame skipping, reduced step counts, hidden Editor content, or a replacement lighting model. Disabling **Use GPU Driven Batch** restores the component benchmark for authoring-path comparisons.

## Fourth pass: camera visibility and indirect submission

The renderer no longer submits every model AABB to every camera. A GPU frustum pass reads the current production model bounds, compacts visible model IDs with a shared-memory group scan and only one global atomic per 64 models, and writes procedural indirect arguments without a CPU readback. Depth/normal rendering and the analytic color fallback use that list directly. The model-ID fullscreen PBR resolve remains a single draw and needs no instance culling. Each Scene/Game camera reuses its own geometrically grown buffers, stale camera entries are retired, and camera motion does not touch model, shape, modifier, or material data.

Main-light and screen-space shadow caster paths deliberately keep full conservative submission in this pass. Camera-frustum culling would incorrectly remove an offscreen object whose shadow reaches the visible image; those paths require cascade/light-volume culling in a later pass.

The measured benchmark showed no FPS increase from this pass. That falsifies AABB submission, offscreen vertex work, and CPU-visible-count handling as dominant costs for the current camera composition. The culling path remains useful for sparse and multi-view scenes, but further optimization moved to the fragment depth/normal evaluator rather than adding more visibility machinery.

## Reverted compact-evaluator experiment

An attempted in-shader compact evaluator duplicated the generic evaluator and its fallbacks inside every march, ambient-occlusion, and tetrahedral-normal call site. Unity's fragment compiler expanded the combined call graph until the SDF raymarch, depth/normal, and screen-space-shadow variants timed out. The experiment was removed rather than increasing `UNITY_SHADER_COMPILER_TASK_TIMEOUT_MINUTES`; the original importable generic evaluator and normal path are restored. Future specialization must live in a separately compiled shader/kernel so the generic and fixed-topology call graphs cannot inline into one another.

## Rejected or replaced

The old project replaced the entire render pipeline. That prevented normal URP coexistence and was replaced with a RenderGraph-compatible URP renderer feature. Its `SDFSample` carried only RGB, so all objects shared one Blinn–Phong calculation. The new hit-surface fold evaluates each contributing shading model and blends final radiance with the geometric CSG weight.

Unsafe operand-distance skipping is disabled for twist, bend, revolution, and infinite repetition rather than relying on padded bounds. The renderer also uses URP's active depth attachment instead of clearing and owning the camera target.
