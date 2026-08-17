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
| Procedural 36-vertex model bounds | Retained as instanced invisible proxy geometry. |
| Ray/AABB entry and exit | Retained, including inside-camera winding handling. |
| Per-shape AABB skip | Retained only for operations and modifiers for which the bound remains conservative. |
| Nonuniform-scale correction | Retained using minimum absolute scale for safe world stepping. |
| Pixel-aware epsilon | Retained for hit and tetrahedral normal samples. |
| Compute-resident two-shape benchmark | Not copied. Its fixed two-shape layout could not represent the complete primitive, modifier, CSG, and custom-material system. The replacement benchmark exercises the production component-to-buffer path at up to 10,000 models. |
| Profiler markers | Retained for CPU compilation/upload and GPU raymarch pass, with a separate `SDF/CPU Refresh Transforms` marker for dynamic-scene profiling. |

## Rejected or replaced

The old project replaced the entire render pipeline. That prevented normal URP coexistence and was replaced with a RenderGraph-compatible URP renderer feature. Its `SDFSample` carried only RGB, so all objects shared one Blinn–Phong calculation. The new hit-surface fold evaluates each contributing shading model and blends final radiance with the geometric CSG weight.

Unsafe operand-distance skipping is disabled for twist, bend, revolution, and infinite repetition rather than relying on padded bounds. The renderer also uses URP's active depth attachment instead of clearing and owning the camera target.
