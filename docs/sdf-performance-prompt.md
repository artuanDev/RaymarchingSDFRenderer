# SDF Renderer Performance Prompt

Feed this file back to Claude Code verbatim to resume the optimization work.

---

## Objective

Make the SDF raymarching renderer fast in both Play mode and the Editor Scene view **while
every feature is simultaneously active**: SDF operations (union / subtract / intersect and
their smooth variants), domain and distance modifiers, per-shape materials being animated
at runtime, SDF self-shadows, screen-space shadows, and ambient occlusion.

Current state: **~30 fps with everything on.**

**Benchmark target: 10,000 SDF shapes, all modifiers and operations active, evaluated on the
GPU, at full quality, above 300 fps.** The 10k system already exists
([SDFBenchmarkController.cs](../Assets/SDF/Runtime/SDFBenchmarkController.cs),
[SDFGpuSceneBatch.cs](../Assets/SDF/Runtime/SDFGpuSceneBatch.cs),
[SDFBenchmarkGpuUpdate.compute](../Assets/SDF/Resources/SDFBenchmarkGpuUpdate.compute)).
Both scene paths must improve: the authored-scene path
([SDFSceneData.cs](../Assets/SDF/Runtime/SDFSceneData.cs)) and the GPU batch path.

---

## Prohibited — read this before proposing anything

These are hard constraints. A change that violates any of them is a failed change, even if
the frame counter goes up.

1. **No downscaling the render.** No render-scale below 1.0, no half-res or quarter-res
   raymarch buffer, no checkerboard rendering, no upscaling of any kind (bilinear, FSR,
   TAAU, DLSS), no variable-rate shading, no rendering into a smaller RT and blitting up.
   Every SDF pixel is traced at native resolution, every frame.

2. **No artificial framerate wins. No shortcuts.** Specifically prohibited:
   - Lowering `MaxSteps`, raising `SurfaceEpsilon` / `NormalEpsilon` / `PixelTolerance`,
     or lowering `StepSafety` quality below the current High preset
     ([SDFRenderSettings.cs](../Assets/SDF/Runtime/SDFRenderSettings.cs)).
   - Reducing `ShadowMaxSteps`, `ShadowMaxDistance`, or `AmbientOcclusionSamples`.
   - Distance-based LOD that drops shapes, simplifies operations, or disables modifiers.
   - Temporal reprojection / frame reuse / amortizing work across frames, caching a hit
     from a previous frame, or any "it's animating so nobody will notice" reasoning.
   - Disabling or weakening any feature to hit the number (shadows, AO, smooth blends,
     per-shape materials, custom material shaders, texture sampling).
   - Freezing or throttling material updates. **Materials change at realtime and must keep
     changing at realtime.**
   - Culling that removes anything actually visible. Frustum, depth and occlusion culling of
     genuinely invisible work is legitimate; anything that pops or omits visible surfaces
     is not.
   - Changing the benchmark (fewer shapes, smaller screen, smaller shapes on screen,
     camera pointed away, fewer modifiers) to make the number easier.
   - Measuring anything other than full-quality end-to-end frame time.

3. **Output must be visually identical.** Before/after frames at the same camera, same
   frame index, same settings must match. Anti-aliasing-level differences from a legitimately
   reordered floating-point expression are acceptable; a visibly different silhouette,
   shading, shadow, or blend is a regression, not an optimization. Verify with actual
   screenshots, not by asserting it.

If the target genuinely cannot be reached without breaking one of these, say so plainly with
the measurement that proves it — do not quietly relax a constraint.

---

## Verified facts about the current renderer

These were established by reading the code and by offline fxc compilation. Earlier drafts of
this document guessed wrong on three of them; the corrections are marked.

### Benchmark topology — **correction**

The 10k benchmark is **10,000 models with 2 shapes each**, not one model with 10,000 shapes.
`SDFBenchmarkGpuBatch` allocates `ShapeBuffer = ModelCount * 2` and `ModifierBuffer =
ModelCount`; the compute kernel writes a base shape at `index*2` and one operand at
`index*2+1`, in a 100x100 grid at 2.5 spacing.

**Consequence: the shape loop inside `EvaluateModel` runs 2 iterations.** A BVH or spatial
acceleration structure over shapes-within-a-model does nothing for this benchmark. (It would
still matter for an authored scene that puts many shapes under one `SDFModel`.) Do not start
there.

### Which passes actually run — **correction**

- Pass 1 `SDFDepthNormals` — **runs** (enqueued when `UseUrpScreenSpaceAo || CastMainLightShadows`, both default true).
- Pass 4 `SDFColorResolve` — **runs**. `resolveFromModelIds` is satisfied by default, so
  colour shading is one fullscreen triangle, already once-per-pixel. Pass 0 is the fallback.
- Pass 3 `SDFScreenSpaceShadows` — **runs**.
- Pass 2 `SDFMainLightShadowCaster` — **never runs.** `m_ShadowPass` is constructed in
  `Create()` but `EnqueuePass` is never called on it
  ([SDFRendererFeature.cs:58-72](../Assets/SDF/Runtime/SDFRendererFeature.cs#L58-L72)).
  The `_SDFPassMode==2` branches in `Vert` and all of `FragShadow` are dead at runtime.
  Decide whether that is intentional before optimizing anything in it.

### The two real bottlenecks are overdraw, not the evaluator

Both live box-rasterizing passes draw one bounding cube instance per model — up to 10,000
instances — and neither can reject a fragment before running its march.

**Pass 1, `SDFDepthNormals`:** `FragDepthNormals` writes `float depth : SV_Depth`. Writing
arbitrary depth **disables early-Z and hierarchical-Z entirely**, so every fragment of every
box runs a full sphere march plus a normal evaluation regardless of what is already in front
of it. The visible-model list from
[SDFModelCull.compute](../Assets/SDF/Resources/SDFModelCull.compute) is frustum-culled but
**not sorted front-to-back**, so even restoring early-Z would not help until draw order is
depth-sorted.

**Pass 3, `SDFScreenSpaceShadows`:** `ZTest Always`, `ZWrite Off`, and `Vert` extrudes the
caster bounds by the full `_SDFShadowMaxDistance` (default 20 world units) along the light
direction. Against ~1-unit shapes at 2.5 spacing that turns each of 10,000 instances into a
long box covering a large screen area, with **zero depth rejection**. This is the most likely
single largest cost in the frame.

### Measured: evaluator code size (offline fxc, `ps_5_0`)

The evaluator was extracted into a standalone harness with no URP dependency and compiled
with `fxc.exe` from the Windows 10 SDK. `#pragma skip_optimizations d3d11` corresponds to
`/Od`, which is what passes 0 and 4 actually ship.

| evaluator variant | `/O3` slots | `/Od` slots | `/Od` temps | `/Od` compile |
|---|---|---|---|---|
| original | 9,996 | 18,664 | 24 | 6.7 s |
| looped `EvaluateNormal` | 3,991 | 7,490 | 25 | 2.5 s |

Two conclusions. `skip_optimizations` costs about 1.87x instruction slots. And
`EvaluateNormal`'s four inlined `EvaluateModel` calls were the dominant code-size driver —
collapsing them to a loop cuts the shipping path 2.49x **at unchanged register pressure**.

Note this is *static* code size. A loop does the same dynamic ALU work as the unrolled form;
the win is instruction-cache behaviour and, more importantly, compile tractability. Do not
report it as a 2.49x runtime win without a GPU measurement.

---

## Already applied

### Step 1 — evaluator size and shadow-pass early-out

1. `EvaluateNormal` accumulates its four tetrahedron taps in a `[loop]` instead of four
   inlined calls in one expression. Tap order and accumulation order unchanged. Verified to
   compile from the edited file: 7,490 slots at `/Od`, exit 0.
2. `FragScreenSpaceShadow` performs its cheap AABB rejection **before** the full
   `EvaluateModel(receiverPosition)` call rather than after. Both are side-effect-free
   discards, so the surviving fragment set is identical; previously every fragment covered by
   an extruded caster volume paid a full field evaluation before being rejected on bounds.

Measured by the user after step 1: **60 fps with no animation, 40 fps with everything
animated**, 10k models, operations and modifiers on. `vSyncCount: 0`, so those are real
numbers rather than a refresh ceiling. Rotations were reported as the most expensive
animation mode, which is consistent with the overdraw diagnosis — rotating a box inflates
its world-space AABB by up to ~1.73x per axis, which inflates both the rasterized coverage
and the marched ray interval.

### Step 2 — early-Z on the depth-normals pass

Targets the overdraw directly. Two coupled edits, since neither works alone:

3. `DepthNormalsOutput` declares `SV_DepthLessEqual` under `UNITY_REVERSED_Z` and
   `SV_DepthGreaterEqual` otherwise, restoring early-Z rejection. `Vert` pins
   `positionCS.z` to `UNITY_NEAR_CLIP_VALUE * w` for `cameraInside` instances so the
   winding flip cannot break the conservative-depth promise.
4. [SDFModelCull.compute](../Assets/SDF/Resources/SDFModelCull.compute) now emits the
   visible model list ordered front to back, via a 256-bucket counting sort over distance
   to the model's bounds (`ClearCounts`, `CullAndCount`, `PrefixSumBuckets`,
   `ScatterModels`). Without an ordering, early-Z rejection depends on where the camera
   happens to point relative to grid index order.

All four kernels compile clean under `fxc /T cs_5_0`.

Enabling `SV_DepthLessEqual` first failed to compile with D3D11 error X8000: a pixel shader
outputting `oDepthLE`/`oDepthGE` must declare its `SV_Position` **input** as
`linear_noperspective_centroid` or `linear_noperspective_sample`. Fixed with a dedicated
`DepthNormalsVaryings` struct carrying `noperspective centroid` on `positionCS`, used only by
`FragDepthNormals`. It is deliberately not on the shared `Varyings`, because
`FragColorResolved` uses `positionCS.xy` as a direct texel coordinate and the other programs
derive `screenUV` from it, which centroid sampling would perturb at MSAA edges.

### Step 3 — the screen-space shadow pass

Attribution, measured by the user: setting `m_CastMainLightShadows` to 0 (which leaves the
depth-normals pass running, since `m_UseUrpScreenSpaceAo` is 1, and removes only pass 3)
took the editor sweep from **70 fps to 200 fps**. That is ~9.3 ms of a 14.3 ms frame, about
**65% of frame time in one pass**.

Two causes, both fixed:

5. **It was never frustum culled.** `RecordScreenSpaceShadows` called `SetupDrawPass` with no
   `VisibilityResult`, so it drew `InstanceCount = ModelCount` — all 10,000 models, always.
   It now builds its own visible list. It cannot share the camera's, because a caster outside
   the frustum can still shadow an on-screen receiver, so the cull compute takes a
   `_SDFCullBoundsExtrusion` uniform and sweeps each model's bounds along the light before the
   frustum test, mirroring `Vert`. Visible lists are keyed by variant so both coexist.
6. **It had no depth buffer bound, so `ZTest Always` was the only legal state** and every
   fragment of every 20-unit extruded volume reached the fragment shader. The pass now binds
   `activeDepthTexture` as a read-only depth attachment and uses `ZTest LEqual`.

   Why that is conservative: the extruded volume is exactly the set of receivers this caster
   can shadow. `LEqual` rejects a fragment only when the volume's near face is strictly
   farther than the receiver, which means the receiver lies in front of the volume and
   therefore outside it — and the shader's own `IntersectAabb` would have discarded it
   anyway. Since the extruded AABB is a superset of the true swept volume, there are no false
   negatives. Fragments that survive but sit behind the volume are still discarded in the
   shader. Output is unchanged; the depth unit (and HiZ) just reaches the answer first.

   This depends on step 2's near-plane pin. `cameraInside` in `Vert` is computed *after* the
   extrusion, so a volume containing the camera rasterizes at the near plane and always
   passes `LEqual` rather than being wrongly rejected. Do not remove one without the other.

**Not yet measured on a GPU.**

One caveat worth knowing: models within the same distance bucket are now scattered by
atomic, so their relative draw order can vary between frames. For opaque surfaces at
distinct depths this is invisible; it would only matter for two models at bit-identical
depth in the same pixel, where `LEqual` lets the last one drawn win.

---

## Next, in priority order

1. **Second-sided depth rejection for the shadow pass.** Step 3 rejects receivers in *front*
   of a caster volume, using front faces and `LEqual`. The mirror case — receivers entirely
   *behind* the volume — still reaches the shader and is discarded there. Catching both sides
   needs either a stencil two-pass over front and back faces (the classic deferred light
   volume setup) or a depth-bounds test. Measure after step 3 before deciding whether the
   remaining cost justifies it.

2. **Tighten bounds against rotation.** This is the measured "rotations cost most" effect.
   `WorldBounds` in the batch compute and `PackShapesJob` in
   [SDFSceneData.cs](../Assets/SDF/Runtime/SDFSceneData.cs) both build a world AABB as
   `abs(axisX)*e.x + abs(axisY)*e.y + abs(axisZ)*e.z`, which inflates by up to ~1.73x per
   axis under rotation — and does so even for a sphere, whose true AABB is rotation
   invariant. Two independent wins: transform the ray into local space and intersect the
   local box (a tight OBB test) for the march interval, and rasterize a tighter hull so
   rotation stops inflating screen coverage.

3. **Re-evaluate `skip_optimizations d3d11`.** With `EvaluateNormal` no longer inlined 4x,
   the compiler's job is 2.49x smaller. Try removing the pragma, or `#pragma use_dxc`, and
   confirm the shader still compiles in acceptable time. Worth ~1.87x instruction slots.

4. **Only then** consider evaluator micro-work: hot/cold split of `SDFShapeGpu` so the
   176-byte load is not paid by shapes the AABB test rejects, reducing divergence in the
   32-branch `EvaluatePrimitive`, and the double walk of the modifier list in `EvaluateShape`
   / `ApplyDistanceModifiers`.

5. **CPU, for the authored-scene path.** `RefreshMaterials`
   ([SDFSceneData.cs:734](../Assets/SDF/Runtime/SDFSceneData.cs#L734)) rebuilds and re-uploads
   the *entire* material buffer when any single material changes, which is the user's live
   scenario. The upload paths call `.Complete()` on the main thread (lines 905, 964).
   `BuildProperties` refills a MaterialPropertyBlock with ~60 setters plus 16 textures per
   source per pass per frame.

---

## How to work

- **Profile before and after every change.** Unity Profiler GPU module, RenderDoc or Nsight
  for pass-level timing, plus
  [SDFEditorPerformanceWindow.cs](../Assets/SDF/Editor/SDFEditorPerformanceWindow.cs) and the
  `Run Benchmark Sweep` context menu on `SDFBenchmarkController`, which already reports
  CPU/GPU ms per animation mode via `FrameTimingManager`. Report **GPU ms per pass**, not
  just fps — fps alone hides which pass moved.
- The offline fxc harness is a fast way to check evaluator code size without Unity: extract
  the struct block and the `IntersectAabb`..`EvaluateNormal` range into a standalone HLSL
  file, add a trivial `PSMain`, and compile with `/T ps_5_0 /Od`. Use PowerShell, not Git
  Bash, or MSYS mangles the `/T` flags into paths; and do not pipe fxc through `2>&1` in
  PowerShell 5.1 or every stderr line comes back wrapped as a NativeCommandError.
- **Land one change at a time** and record its measured delta.
- Run the existing tests ([Assets/SDF/Tests/](../Assets/SDF/Tests/)) after each change.
- Capture reference screenshots before starting and diff against them after each change.

## Definition of done

- 10,000 shapes, all modifiers and operations active, all lighting features on, materials
  animating, native resolution, High quality preset: **> 300 fps**, measured and reported.
- The interactive authored scene at full feature load is meaningfully above 30 fps, with the
  measured number stated.
- Editor Scene view is interactive at full quality.
- Screenshot comparison shows no visible difference from the pre-optimization renderer.
- Every prohibition above still holds, and the report states explicitly that none were relaxed.
