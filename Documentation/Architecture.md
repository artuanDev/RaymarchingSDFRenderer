# Architecture

## Scene representation

An `SDFModel` owns an ordered hierarchy of `SDFShape` operands. Each shape has an `SDFOperation`; the first operand seeds the fold and later operands apply union, subtraction, intersection, or their smooth variants. Multiple `SDFModifier` components form an ordered domain stack. A shape can reference `SDFMaterialAsset`, while `SDFCustomMaterial` provides a component-level override.

`SDFSceneRegistry` is event-driven. Components register on enable and increment a version on serialized, animated, topology, transform, modifier, operation, material, or bounds changes. `SDFSceneData` only rebuilds topology when structural shape data changes. Transform-only changes reuse cached shape/model bindings; Burst-compiled transform and model jobs update world matrices, conservative bounds, and scale corrections in parallel. Operation changes update packed CSG fields and model bounds, while modifier parameter changes update the modifier buffer plus affected conservative bounds. Material-only changes rebuild only the compact material buffer. Buffer capacity grows by powers of two and is reused.

Shape parameter and material-binding changes also use the incremental path; they no longer force hierarchy discovery or a topology rebuild. Shape, custom-material, operation, and modifier bindings carry non-serialized data revisions so unchanged components are skipped. A single inspector edit updates that shape's packed parameters and bounds directly instead of scheduling the full transform-access job. Modifier-only animation likewise refits affected world bounds from cached local-to-world rows. The full transform job remains authoritative whenever Unity transforms change.

## GPU data and rendering

Models, shapes, modifiers, and materials are packed into persistent structured `GraphicsBuffer` objects. Dynamic model/shape data rotates across three buffers so CPU uploads do not overwrite a buffer that a preceding GPU frame may still be consuming. A Unity 6 `ScriptableRendererFeature` records a native RenderGraph raster pass after opaque geometry. The pass binds URP's active camera color and depth attachments directly and draws 36 procedural vertices per model instance. These vertices form a conservative AABB and are never visible as geometry.

The fragment shader intersects the camera ray with the model AABB and sphere-traces only that interval. Existing opaque depth rejects occluded volume fragments, and successful SDF hits write `SV_Depth` so later rendering respects them. Perspective, orthographic, and inside-volume cameras use separate ray setup/winding paths.

When URP requests the SDF depth/normal prepass for SSAO or SDF shadow integration, the color pass can reuse it. It reconstructs the cached world-space hit, verifies that the current model owns that surface, reuses the stored world normal, and proceeds directly to material/PBR evaluation. This removes a duplicate sphere trace and four-sample normal evaluation while retaining the original analytic trace as a fallback when the prepass is unavailable or reuse is disabled.

The Scene-view picker maintains a median-split CPU BVH over conservative shape bounds. Topology changes rebuild it, bounds changes refit it, and transform-only refits reuse cached local bounds without rediscovering modifier components. Only ray-overlapping leaves run the exact CPU SDF raymarch. Scene visibility, picking-disabled objects, conventional-geometry precedence, and Unity's additive/toggle selection behavior are applied after broad-phase traversal.

## Distance safety

World distance uses the minimum absolute lossy-scale component. Per-shape AABB distance is converted with a conservative scale ratio before it can skip a union operand. Twist, bend, and infinite repetition disable this skip because their world AABB distance is not generally a safe lower bound. Hit and normal epsilons grow with projected pixel footprint without changing render resolution.

Exact, bound-only, and unsigned catalogue entries are distinguished in the coverage matrix. Bound-only functions rely on a safety step scale. Infinite functions require an explicit user-visible clip volume.

## Material blending

The march and four-sample normal path evaluate distances only. After a hit, a second ordered fold evaluates surface shaders. Every smooth CSG formula uses the identical interpolation weight for distance and shaded radiance. Since the accumulated radiance is folded at every operation, blends remain correct through arbitrary sequential smooth operations and can cross shading models such as cel and PBR-like.

Hard operations select the shader belonging to the distance winner. Subtraction selects the cutter material on the newly exposed cut surface.

URP lighting, shadow-atlas integration, screen-space ambient occlusion, and ambient/reflection lighting are described in [Lighting.md](Lighting.md).
