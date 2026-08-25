# Known limitations

- Opaque SDF surfaces are the supported production path. Transparent composition is deliberately not presented as working because correct sorting and depth integration require a separate renderer path.
- One Base Map plus two arbitrary `float4` parameter blocks are available per custom material. The inspector parses Float, Range, Color, Vector, Toggle, and Enum declarations mapped to `_Custom0`, `_Custom1`, or their named components; runtime HLSL accesses the stable `Custom0`, `Custom1`, and Base Map fields.
- URP Lit materials can bind Base, Normal, Metallic, and Roughness maps. Custom materials expose one Base Map. Fifteen user-assigned 2D textures plus the built-in white fallback can be bound per rendered scene batch; maps shared by several materials consume one slot. Textures are used at their imported resolution and format and are not resized or atlased.
- The current renderer uses the primary directional light or its configured fallback. Full URP additional-light and shadow-atlas integration is a separate extension.
- XR single-pass instancing has not yet been validated. Perspective, orthographic, Scene, Game, and preview cameras use the non-XR path.
- Unsigned triangle and quad distances become thin renderable surfaces by subtracting the exposed thickness parameter.
