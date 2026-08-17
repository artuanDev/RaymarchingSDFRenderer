# Custom SDF surface shaders

Choose **Assets > Create > SDF Shader**. Unity creates an editable `.sdfshader` text asset and imports it as `SDFShaderAsset`. A valid source contains a `Properties` description and one HLSL function:

```hlsl
float3 SDFSurface(SDFSurfaceContext context, SDFMaterialGpu material)
```

This is intentionally a surface/fragment contract, not arbitrary ShaderLab. Raymarched surfaces have no mesh vertices, vertex attributes, or conventional vertex stage. Importing a module extracts its HLSL block, assigns a stable generated dispatch index, and regenerates `Assets/SDF/Generated/SDFCustomMaterials.hlsl`. The primary raymarch shader therefore compiles every active module into real HLSL rather than attempting unsupported dynamic shader calls.

## Context

`SDFSurfaceContext` provides:

- `positionWS` and the contributing shape's `positionOS`;
- the composite `normalWS`;
- `viewDirectionWS`;
- procedural `uv` (the current operand's local XZ projection);
- normalized `screenUV` and pixel-valued `screenPosition`;
- `cameraPositionWS` and `cameraForwardWS`;
- the selected directional light, color, and ambient color.

`SDFMaterialGpu` provides base color, specular parameters, emission, shading model data, `Custom0`, `Custom1`, and generated shader/texture indices. Material assets parse labels from the module's `Properties` block. Declare `_Custom0`/`_Custom1` as vectors, or address individual components with names such as `_Custom0X ("Strength", Range(0, 2))`; HLSL reads that value as `material.Custom0.x`.

Use:

```hlsl
SampleSDFTexture((uint)(material.CustomShaderTextureIndices.y + 0.5), uv)
```

to sample the assigned Base Map. Sixteen independent 2D textures can be bound in one scene batch without resizing or atlasing. Additional textures use a white fallback and produce an explicit warning.

Custom modules run only during the final hit-surface fold, never during sphere-tracing or normal-distance samples. Smooth operations blend their final lit outputs using the same interpolation weight as the distance formula.
