# SDF lighting

The renderer shades SDF hits inside URP rather than maintaining a separate lighting pipeline. The original `D:\Unity\SDF_Rendering` project remains useful as the baseline for selecting `RenderSettings.sun`, converting fallback colors to linear space, and applying a constant ambient term. That project does not implement PBR, ambient occlusion, or shadows; those integrations are provided here through URP 17.3.

## Shadows

SDF models cast directional-light shadows with an instanced screen-space shadow-volume pass after opaque SDF shading. Each caster extrudes its conservative AABB away from the main light, rasterizes only the covered screen region, reconstructs the receiver from URP's camera depth, and traces only the light-ray segment crossing that caster's original AABB. It does not run a full-screen loop over every SDF model.

The resulting pass provides:

- SDFs cast onto other SDFs and opaque URP geometry.
- SDFs receive regular URP main/additional-light shadows through the normal URP lighting functions.
- A caster's own finished PBR color is not multiplied, so indirect probe/ambient lighting remains visible on surfaces facing away from the sun.

`Cast Main Light Shadows` controls the extra shadow-volume pass. Shadow steps, distance, safety, bias, and softness affect only that cheaper trace and do not change camera raymarch quality. Softness estimates a penumbra during the existing trace and adds no extra shadow rays. Receiver-normal and pixel-scaled biasing keep the trace stable as camera distance changes. The shadow floor is tinted with the same scene ambient probe used by geometry instead of removing all indirect light. Additional-light shadow casting by SDFs is not currently generated; SDF URP Lit materials can still receive additional-light shadows.

## Ambient occlusion

Local SDF ambient occlusion samples the current model's distance field along the surface normal. It is evaluated once per visible hit, shared by smooth material blends, and affects indirect PBR lighting. Radius controls its reach and the bounded 2–6 sample setting controls cost (three by default).

URP's screen-space AO works alongside local AO to add contact between independent SDF models and regular geometry without introducing a global scene-distance loop. Before SSAO executes, a depth/normal pass raymarches the visible SDF surface into URP's camera depth and normal textures. URP then computes one shared AO texture for the complete scene.

The color pass samples that AO texture once per visible SDF hit. Its direct factor attenuates direct lighting according to the SSAO feature's `Direct Lighting Strength`; its indirect factor is combined with the material occlusion value and attenuates ambient/reflection lighting. Smooth CSG material blends reuse the same lookup.

`Use URP Screen Space AO` enables the SDF depth/normal contribution. The renderer's AO strength blends the sampled result toward neutral white; radius, sample count, downsampling, and blur remain settings of URP's `Screen Space Ambient Occlusion` renderer feature.

## PBR

The `URP Lit` SDF shading model packs albedo, metallic, smoothness, material occlusion, and emission into the existing material buffer. At a hit, it builds URP `SurfaceData`, `InputData`, and `BRDFData`, then uses URP's physically based lighting functions for:

- the main directional light and its shadow attenuation;
- Forward+ additional lights and their receive-side shadows;
- dielectric/metallic energy conservation and roughness response;
- reflection probes, glossy environment reflections, and emission.

Distance evaluation and normal estimation remain material-free. Materials are evaluated only after a hit, and the existing ordered CSG fold shades only operands that contribute to the final surface.

## Ambient lighting

URP spherical harmonics provide direction-dependent ambient diffuse lighting, while `GlobalIllumination` adds reflection-probe/environment specular. Procedural draws have no `Renderer`, so this renderer explicitly binds `RenderSettings.ambientProbe`, Unity's default reflection cubemap, HDR decode values, and neutral probe occlusion to every SDF draw. The configured SDF ambient color is converted to linear space and used only as a minimum floor when the scene probe is unavailable.

The preview path has no live URP probe context, so it uses the explicit ambient color directly.
