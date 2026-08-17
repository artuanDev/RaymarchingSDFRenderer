# SDF settings

On first Unity import, the editor setup creates `SDFRenderSettings.asset` here and assigns it to the installed URP renderer feature. The asset exposes tracing precision, native-resolution quality presets, main-light shadow tracing (including softness), local SDF AO, URP screen-space AO integration, and fallback directional/ambient lighting.

**SDF Ambient Occlusion** traces the local distance field at visible hits; radius and the bounded 2–6 sample count are configured here. For cross-object AO, add URP's **Screen Space Ambient Occlusion** renderer feature to the desktop renderer and keep it after **SDF Full Resolution Renderer** in the renderer-feature list. The SDF depth/normal pass is scheduled before SSAO regardless of the final color-pass injection point.
