#ifndef SDF_CUSTOM_MATERIALS_INCLUDED
#define SDF_CUSTOM_MATERIALS_INCLUDED
// Auto-generated from .sdfshader modules. Do not edit.
#line 1 "Assets/SDF/Samples/Shaders/ExamplePbrLike.sdfshader"
float3 SDFSurface_0(SDFSurfaceContext context, SDFMaterialGpu material)
    {
        float ndotl = saturate(dot(context.normalWS, context.lightDirectionWS));
        float3 halfDirection = normalize(context.lightDirectionWS + context.viewDirectionWS);
        float metallic = saturate(material.Custom0.w);
        float smoothness = saturate(material.Custom1.x);
        float3 f0 = lerp(0.04.xxx, material.Custom0.rgb, metallic);
        float3 diffuse = material.Custom0.rgb * (1.0 - metallic) * (context.ambientColor + context.lightColor * ndotl);
        float specular = pow(saturate(dot(context.normalWS, halfDirection)), lerp(4.0, 256.0, smoothness));
        return diffuse + f0 * context.lightColor * specular;
    }
#line 1 "Assets/SDF/Generated/SDFCustomMaterials.hlsl"
#line 1 "Assets/SDF/Samples/Shaders/ExampleUnlit.sdfshader"
float3 SDFSurface_1(SDFSurfaceContext context, SDFMaterialGpu material)
    {
        float3 tex = SampleSDFTexture((uint)(material.CustomShaderTextureIndices.y + 0.5), context.positionOS.xz).rgb;
        return tex * material.Custom0.rgb;
    }
#line 1 "Assets/SDF/Generated/SDFCustomMaterials.hlsl"
#line 1 "Assets/SDF/Samples/Shaders/ExampleCel.sdfshader"
float3 SDFSurface_2(SDFSurfaceContext context, SDFMaterialGpu material)
    {
        float bands = max(material.Custom0.w, 2.0);
        float lighting = floor(saturate(dot(context.normalWS, context.lightDirectionWS)) * bands) / (bands - 1.0);
        return material.Custom0.rgb * (context.ambientColor + context.lightColor * lighting);
    }
#line 1 "Assets/SDF/Generated/SDFCustomMaterials.hlsl"
#line 1 "Assets/SDF/Samples/Shaders/ExampleBlinnPhong.sdfshader"
float3 SDFSurface_3(SDFSurfaceContext context, SDFMaterialGpu material)
    {
        float ndotl = saturate(dot(context.normalWS, context.lightDirectionWS));
        float3 halfDirection = normalize(context.lightDirectionWS + context.viewDirectionWS);
        float specular = pow(saturate(dot(context.normalWS, halfDirection)), max(material.Custom0.w, 1.0));
        float3 tex = SampleSDFTexture((uint)(material.CustomShaderTextureIndices.y + 0.5), context.positionOS.xz).rgb;
        return tex * material.Custom0.rgb * (context.ambientColor + context.lightColor * ndotl) + context.lightColor * specular;
    }
#line 1 "Assets/SDF/Generated/SDFCustomMaterials.hlsl"
float3 SDFShadeCustom(uint customId, SDFSurfaceContext context, SDFMaterialGpu material)
{
    if (customId == 0u) return SDFSurface_0(context, material);
    if (customId == 1u) return SDFSurface_1(context, material);
    if (customId == 2u) return SDFSurface_2(context, material);
    if (customId == 3u) return SDFSurface_3(context, material);
    return material.BaseColor.rgb;
}
#endif
