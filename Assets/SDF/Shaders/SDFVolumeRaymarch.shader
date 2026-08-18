Shader "Hidden/SDF/URPVolumeRaymarch"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Opaque" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            struct SDFModelGpu
            {
                float4 BoundsMinAndShapeStart;
                float4 BoundsMaxAndShapeCount;
            };

            struct SDFShapeGpu
            {
                float4 WorldToLocal0;
                float4 WorldToLocal1;
                float4 WorldToLocal2;
                float4 Parameters0;
                float4 Parameters1;
                float4 Parameters2;
                float4 Parameters3;
                float4 TypeOperationSmoothModifierStart;
                float4 MaterialModifierCountDistanceScaleBoundsScale;
                float4 BoundsMin;
                float4 BoundsMax;
            };

            struct SDFModifierGpu
            {
                float4 TypeAxesAmount;
                float4 Vector;
                float4 Count;
            };

            struct SDFMaterialGpu
            {
                float4 BaseColor;
                float4 SpecularAndPower;
                float4 EmissionAndCelBands;
                float4 ModelMetallicSmoothness;
                float4 Custom0;
                float4 Custom1;
                float4 CustomShaderTextureIndices;
            };

            StructuredBuffer<SDFModelGpu> _SDFModels;
            StructuredBuffer<SDFShapeGpu> _SDFShapes;
            StructuredBuffer<SDFModifierGpu> _SDFModifiers;
            StructuredBuffer<SDFMaterialGpu> _SDFMaterials;

            float4x4 _SDFViewProjection;
            float3 _SDFCameraPosition;
            float3 _SDFCameraForward;
            float _SDFCameraNear;
            float _SDFOrthographic;
            float _SDFSceneView;
            float _SDFPixelWorldScale;
            int _SDFMaxSteps;
            float _SDFMaxDistance;
            float _SDFStepSafety;
            float _SDFSurfaceEpsilon;
            float _SDFNormalEpsilon;
            float _SDFPixelTolerance;
            int _SDFReuseDepthNormalPrepass;
            int _SDFPassMode;
            int _SDFPreviewMode;
            int _SDFModelCount;
            int _SDFShadowCascadeCount;
            int _SDFReceiveUrpShadows;
            int _SDFSelfShadows;
            int _SDFShadowMaxSteps;
            float _SDFShadowMaxDistance;
            float _SDFShadowStepSafety;
            float _SDFShadowBias;
            float _SDFShadowStrength;
            float _SDFShadowSoftness;
            int _SDFUseUrpScreenSpaceAO;
            int _SDFAmbientOcclusionEnabled;
            float _SDFAmbientOcclusionStrength;
            float _SDFAmbientOcclusionRadius;
            int _SDFAmbientOcclusionSamples;
            float3 _SDFAmbientColor;
            float3 _SDFLightDirection;
            float3 _SDFLightColor;
            half4 _SDFShadowSHAr;
            half4 _SDFShadowSHAg;
            half4 _SDFShadowSHAb;
            half4 _SDFShadowSHBr;
            half4 _SDFShadowSHBg;
            half4 _SDFShadowSHBb;
            half4 _SDFShadowSHC;
            // D3D11 exposes only sixteen pixel-shader sampler registers and the maximal
            // URP Forward+ lighting variant already uses all of them. SDF slots therefore
            // reuse URP's global linear-clamp sampler and wrap their UVs explicitly. The
            // sixteen texture resources remain independent and cost no extra samplers.
            TEXTURE2D(_SDFTexture0);
            TEXTURE2D(_SDFTexture1);
            TEXTURE2D(_SDFTexture2);
            TEXTURE2D(_SDFTexture3);
            TEXTURE2D(_SDFTexture4);
            TEXTURE2D(_SDFTexture5);
            TEXTURE2D(_SDFTexture6);
            TEXTURE2D(_SDFTexture7);
            TEXTURE2D(_SDFTexture8);
            TEXTURE2D(_SDFTexture9);
            TEXTURE2D(_SDFTexture10);
            TEXTURE2D(_SDFTexture11);
            TEXTURE2D(_SDFTexture12);
            TEXTURE2D(_SDFTexture13);
            TEXTURE2D(_SDFTexture14);
            TEXTURE2D(_SDFTexture15);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                nointerpolation uint modelIndex : TEXCOORD1;
                nointerpolation uint shadowCascade : TEXCOORD2;
            };

            struct FragmentOutput
            {
                float4 color : SV_Target;
                float depth : SV_Depth;
            };

            struct SDFSurfaceContext
            {
                float3 positionWS;
                float3 positionOS;
                float3 normalWS;
                float3 viewDirectionWS;
                float2 uv;
                float2 screenUV;
                float4 screenPosition;
                float3 cameraPositionWS;
                float3 cameraForwardWS;
                float3 lightDirectionWS;
                float3 lightColor;
                float3 ambientColor;
                float directAmbientOcclusion;
                float ambientOcclusion;
                float selfShadow;
            };

            float4 SampleSDFTexture(uint index, float2 uv);
            #include "Assets/SDF/Generated/SDFCustomMaterials.hlsl"

            float4 SampleSDFTexture(uint index, float2 uv)
            {
                uv=frac(uv);
                if(index==1u) return SAMPLE_TEXTURE2D(_SDFTexture1,sampler_LinearClamp,uv);
                if(index==2u) return SAMPLE_TEXTURE2D(_SDFTexture2,sampler_LinearClamp,uv);
                if(index==3u) return SAMPLE_TEXTURE2D(_SDFTexture3,sampler_LinearClamp,uv);
                if(index==4u) return SAMPLE_TEXTURE2D(_SDFTexture4,sampler_LinearClamp,uv);
                if(index==5u) return SAMPLE_TEXTURE2D(_SDFTexture5,sampler_LinearClamp,uv);
                if(index==6u) return SAMPLE_TEXTURE2D(_SDFTexture6,sampler_LinearClamp,uv);
                if(index==7u) return SAMPLE_TEXTURE2D(_SDFTexture7,sampler_LinearClamp,uv);
                if(index==8u) return SAMPLE_TEXTURE2D(_SDFTexture8,sampler_LinearClamp,uv);
                if(index==9u) return SAMPLE_TEXTURE2D(_SDFTexture9,sampler_LinearClamp,uv);
                if(index==10u) return SAMPLE_TEXTURE2D(_SDFTexture10,sampler_LinearClamp,uv);
                if(index==11u) return SAMPLE_TEXTURE2D(_SDFTexture11,sampler_LinearClamp,uv);
                if(index==12u) return SAMPLE_TEXTURE2D(_SDFTexture12,sampler_LinearClamp,uv);
                if(index==13u) return SAMPLE_TEXTURE2D(_SDFTexture13,sampler_LinearClamp,uv);
                if(index==14u) return SAMPLE_TEXTURE2D(_SDFTexture14,sampler_LinearClamp,uv);
                if(index==15u) return SAMPLE_TEXTURE2D(_SDFTexture15,sampler_LinearClamp,uv);
                return SAMPLE_TEXTURE2D(_SDFTexture0,sampler_LinearClamp,uv);
            }

            static const uint kCubeIndices[36] =
            {
                0, 2, 3, 0, 3, 1, 5, 7, 6, 5, 6, 4,
                4, 6, 2, 4, 2, 0, 1, 3, 7, 1, 7, 5,
                2, 6, 7, 2, 7, 3, 4, 0, 1, 4, 1, 5
            };

            float3 CubeCorner(uint index)
            {
                return float3((index & 1u) ? 1.0 : 0.0, (index & 2u) ? 1.0 : 0.0, (index & 4u) ? 1.0 : 0.0);
            }

            Varyings Vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
            {
                uint modelIndex=_SDFPassMode==2?instanceId%(uint)max(_SDFModelCount,1):instanceId;
                uint shadowCascade=_SDFPassMode==2?instanceId/(uint)max(_SDFModelCount,1):0u;
                SDFModelGpu model = _SDFModels[modelIndex];
                if (_SDFSceneView > 0.5 && model.BoundsMaxAndShapeCount.w < 0.0)
                {
                    Varyings hidden;
                    hidden.positionWS = 0.0.xxx;
                    hidden.positionCS = float4(2.0, 2.0, 2.0, 1.0);
                    hidden.modelIndex = modelIndex;
                    hidden.shadowCascade = shadowCascade;
                    return hidden;
                }
                float3 boundsMinimum=model.BoundsMinAndShapeStart.xyz;
                float3 boundsMaximum=model.BoundsMaxAndShapeCount.xyz;
                if(_SDFPassMode==3)
                {
                    float3 shadowExtrusion=-normalize(_SDFLightDirection)*_SDFShadowMaxDistance;
                    boundsMinimum=min(boundsMinimum,boundsMinimum+shadowExtrusion);
                    boundsMaximum=max(boundsMaximum,boundsMaximum+shadowExtrusion);
                }
                float3 outside = max(max(boundsMinimum - _SDFCameraPosition,
                                         _SDFCameraPosition - boundsMaximum), 0.0.xxx);
                bool cameraInside = _SDFPassMode!=2&&dot(outside, outside) < _SDFCameraNear * _SDFCameraNear * 4.0;
                uint windingId = vertexId;
                uint triangleVertex = vertexId % 3u;
                if (cameraInside && triangleVertex == 1u) windingId = vertexId + 1u;
                if (cameraInside && triangleVertex == 2u) windingId = vertexId - 1u;
                float3 corner = CubeCorner(kCubeIndices[windingId]);
                Varyings output;
                output.positionWS = lerp(boundsMinimum,boundsMaximum,corner);
                if(_SDFPassMode==2)
                {
                    float4 shadowPosition=mul(_MainLightWorldToShadow[min(shadowCascade,3u)],float4(output.positionWS,1.0));
                    float2 clipXY=shadowPosition.xy*2.0-shadowPosition.ww;
                    // _MainLightWorldToShadow already contains URP's render-target
                    // orientation and cascade-atlas transform. Flipping Y here again
                    // moves the caster into a different atlas tile on D3D.
                    output.positionCS=float4(clipXY,shadowPosition.z,shadowPosition.w);
                }
                else
                    output.positionCS = mul(_SDFViewProjection, float4(output.positionWS, 1.0));
                output.modelIndex = modelIndex;
                output.shadowCascade = shadowCascade;
                return output;
            }

            float2 IntersectAabb(float3 ro, float3 rd, float3 minimum, float3 maximum)
            {
                float3 directionSign = lerp(-1.0.xxx, 1.0.xxx, step(0.0.xxx, rd));
                float3 inverseDirection = directionSign / max(abs(rd), 1e-8.xxx);
                float3 t0 = (minimum - ro) * inverseDirection;
                float3 t1 = (maximum - ro) * inverseDirection;
                float3 nearValues = min(t0, t1);
                float3 farValues = max(t0, t1);
                return float2(max(nearValues.x, max(nearValues.y, nearValues.z)), min(farValues.x, min(farValues.y, farValues.z)));
            }

            float SquaredDistanceToAabb(float3 p, float3 minimum, float3 maximum)
            {
                float3 delta = max(max(minimum - p, p - maximum), 0.0.xxx);
                return dot(delta, delta);
            }

            float3 ToLocal(float3 positionWS, SDFShapeGpu shape)
            {
                float4 p = float4(positionWS, 1.0);
                return float3(dot(shape.WorldToLocal0, p), dot(shape.WorldToLocal1, p), dot(shape.WorldToLocal2, p));
            }

            bool HasAxis(uint axes, uint bit) { return (axes & bit) != 0u; }

            void ApplyDomainModifier(inout float3 p, inout float correction, inout float extrusionDistance, SDFModifierGpu modifier)
            {
                uint type = (uint)(modifier.TypeAxesAmount.x + 0.5);
                uint axes = (uint)(modifier.TypeAxesAmount.y + 0.5);
                float amount = modifier.TypeAxesAmount.z;
                if (type == 2u)
                {
                    float3 h = abs(modifier.Vector.xyz) * float3(HasAxis(axes, 1u) ? 1.0 : 0.0, HasAxis(axes, 2u) ? 1.0 : 0.0, HasAxis(axes, 4u) ? 1.0 : 0.0);
                    p -= clamp(p, -h, h);
                }
                else if (type == 3u)
                {
                    if (HasAxis(axes, 1u)) p.x = abs(p.x) - modifier.Vector.x;
                    if (HasAxis(axes, 2u)) p.y = abs(p.y) - modifier.Vector.y;
                    if (HasAxis(axes, 4u)) p.z = abs(p.z) - modifier.Vector.z;
                }
                else if (type == 4u || type == 5u)
                {
                    float3 spacing = max(abs(modifier.Vector.xyz), 1e-5.xxx);
                    float3 cell = round(p / spacing);
                    if (type == 4u) cell = clamp(cell, -modifier.Count.xyz, modifier.Count.xyz);
                    if (!HasAxis(axes, 1u)) cell.x = 0.0;
                    if (!HasAxis(axes, 2u)) cell.y = 0.0;
                    if (!HasAxis(axes, 4u)) cell.z = 0.0;
                    p -= cell * spacing;
                }
                else if (type == 6u)
                {
                    float angle = amount * p.y;
                    float sine, cosine;
                    sincos(angle, sine, cosine);
                    p.xz = float2(cosine * p.x - sine * p.z, sine * p.x + cosine * p.z);
                    correction /= 1.0 + abs(amount) * length(p.xz);
                }
                else if (type == 7u)
                {
                    float angle = amount * p.x;
                    float sine, cosine;
                    sincos(angle, sine, cosine);
                    p.xy = float2(cosine * p.x - sine * p.y, sine * p.x + cosine * p.y);
                    correction /= 1.0 + abs(amount) * length(p.xy);
                }
                else if (type == 8u)
                {
                    p = float3(length(p.xz) - amount, p.y, 0.0);
                }
                else if (type == 9u)
                {
                    if (HasAxis(axes, 1u)) { extrusionDistance = max(extrusionDistance, abs(p.x) - abs(amount)); p.x = 0.0; }
                    if (HasAxis(axes, 2u)) { extrusionDistance = max(extrusionDistance, abs(p.y) - abs(amount)); p.y = 0.0; }
                    if (HasAxis(axes, 4u)) { extrusionDistance = max(extrusionDistance, abs(p.z) - abs(amount)); p.z = 0.0; }
                }
            }

            float ApplyDistanceModifiers(float distance, uint start, uint count, float distanceScale)
            {
                [loop] for (uint i = 0u; i < count; ++i)
                {
                    SDFModifierGpu modifier = _SDFModifiers[start + i];
                    uint type = (uint)(modifier.TypeAxesAmount.x + 0.5);
                    float amount = abs(modifier.TypeAxesAmount.z);
                    if (type == 0u) distance -= amount * distanceScale;
                    else if (type == 1u) distance = abs(distance) - amount * distanceScale;
                }
                return distance;
            }

            float SdBox(float3 p, float3 b)
            {
                float3 q = abs(p) - b;
                return length(max(q, 0.0.xxx)) + min(max(q.x, max(q.y, q.z)), 0.0);
            }

            float SdBoxFrame(float3 p, float3 b, float e)
            {
                p = abs(p) - b;
                float3 q = abs(p + e) - e;
                float3 q1 = float3(p.x, q.y, q.z);
                float3 q2 = float3(q.x, p.y, q.z);
                float3 q3 = float3(q.x, q.y, p.z);
                return min(length(max(q1,0.0))+min(max(q1.x,max(q1.y,q1.z)),0.0),
                       min(length(max(q2,0.0))+min(max(q2.x,max(q2.y,q2.z)),0.0),
                           length(max(q3,0.0))+min(max(q3.x,max(q3.y,q3.z)),0.0)));
            }

            float SdCappedCylinder(float3 p, float h, float r)
            {
                float2 d = abs(float2(length(p.xz), p.y)) - float2(r, h);
                return min(max(d.x, d.y), 0.0) + length(max(d, 0.0.xx));
            }

            float SdCapsule(float3 p, float3 a, float3 b, float r)
            {
                float3 pa = p-a, ba = b-a;
                float h = saturate(dot(pa,ba) / max(dot(ba,ba), 1e-8));
                return length(pa-ba*h)-r;
            }

            float SdCappedCone(float3 p, float h, float r1, float r2)
            {
                float2 q = float2(length(p.xz), p.y);
                float2 k1 = float2(r2,h), k2 = float2(r2-r1,2.0*h);
                float2 ca = float2(q.x-min(q.x,(q.y<0.0)?r1:r2), abs(q.y)-h);
                float2 cb = q-k1+k2*saturate(dot(k1-q,k2)/max(dot(k2,k2),1e-8));
                float signValue = (cb.x<0.0 && ca.y<0.0) ? -1.0 : 1.0;
                return signValue*sqrt(min(dot(ca,ca),dot(cb,cb)));
            }

            float SdArbitraryCappedCylinder(float3 p, float3 a, float3 b, float r)
            {
                float3 ba=b-a, pa=p-a;
                float baba=max(dot(ba,ba),1e-8), paba=dot(pa,ba);
                float x=length(pa*baba-ba*paba)-r*baba;
                float y=abs(paba-baba*0.5)-baba*0.5;
                float d=(max(x,y)<0.0)?-min(x*x,y*y*baba):max(x,0.0)*max(x,0.0)+max(y,0.0)*max(y,0.0)*baba;
                return sign(d)*sqrt(abs(d))/baba;
            }

            float SdArbitraryCappedCone(float3 p, float3 a, float3 b, float ra, float rb)
            {
                float3 ba=b-a, pa=p-a;
                float rba=rb-ra, baba=max(dot(ba,ba),1e-8), papa=dot(pa,pa), paba=dot(pa,ba)/baba;
                float x=sqrt(max(papa-paba*paba*baba,0.0));
                float cax=max(0.0,x-((paba<0.5)?ra:rb)), cay=abs(paba-0.5)-0.5;
                float k=rba*rba+baba, f=saturate((rba*(x-ra)+paba*baba)/k);
                float cbx=x-ra-f*rba, cby=paba-f;
                float s=(cbx<0.0 && cay<0.0)?-1.0:1.0;
                return s*sqrt(min(cax*cax+cay*cay*baba,cbx*cbx+cby*cby*baba));
            }

            float SdRoundCone(float3 p, float3 a, float3 b, float r1, float r2)
            {
                float3 ba=b-a, pa=p-a;
                float l2=max(dot(ba,ba),1e-8), rr=r1-r2, a2=max(l2-rr*rr,1e-8), il2=1.0/l2;
                float y=dot(pa,ba), z=y-l2;
                float3 xVector=pa*l2-ba*y;
                float x2=dot(xVector,xVector), y2=y*y*l2, z2=z*z*l2;
                float k=sign(rr)*rr*rr*x2;
                if(sign(z)*a2*z2>k) return sqrt(x2+z2)*il2-r2;
                if(sign(y)*a2*y2<k) return sqrt(x2+y2)*il2-r1;
                return (sqrt(x2*a2*il2)+y*rr)*il2-r1;
            }

            float SdOctahedron(float3 p, float s)
            {
                p=abs(p); float m=p.x+p.y+p.z-s; float3 q;
                if(3.0*p.x<m) q=p.xyz; else if(3.0*p.y<m) q=p.yzx; else if(3.0*p.z<m) q=p.zxy; else return m*0.57735027;
                float k=clamp(0.5*(q.z-q.y+s),0.0,s);
                return length(float3(q.x,q.y-s+k,q.z-k));
            }

            float Dot2(float3 v) { return dot(v,v); }
            float UdTriangle(float3 p, float3 a, float3 b, float3 c)
            {
                float3 ba=b-a, pa=p-a, cb=c-b, pb=p-b, ac=a-c, pc=p-c, nor=cross(ba,ac);
                return sqrt((sign(dot(cross(ba,nor),pa))+sign(dot(cross(cb,nor),pb))+sign(dot(cross(ac,nor),pc))<2.0)
                    ? min(min(Dot2(ba*saturate(dot(ba,pa)/max(Dot2(ba),1e-8))-pa),Dot2(cb*saturate(dot(cb,pb)/max(Dot2(cb),1e-8))-pb)),Dot2(ac*saturate(dot(ac,pc)/max(Dot2(ac),1e-8))-pc))
                    : dot(nor,pa)*dot(nor,pa)/max(Dot2(nor),1e-8));
            }

            float EvaluatePrimitive(float3 p, uint type, float4 a, float4 b, float4 c, float4 d)
            {
                if(type==0u) return length(p)-a.x;
                if(type==1u) return SdBox(p,b.xyz);
                if(type==2u) return SdBox(p,b.xyz)-b.w;
                if(type==3u) return SdBoxFrame(p,b.xyz,c.w);
                if(type==4u) return length(float2(length(p.xz)-a.z,p.y))-a.w;
                if(type==5u) { p.x=abs(p.x); float2 sc=float2(sin(d.w),cos(d.w)); float k=(sc.y*p.x>sc.x*p.y)?dot(p.xy,sc):length(p.xy); return sqrt(dot(p,p)+a.z*a.z-2.0*a.z*k)-a.w; }
                if(type==6u) { float3 q=float3(p.x,max(abs(p.y)-a.y,0.0),p.z); return length(float2(length(q.xy)-a.z,q.z))-a.w; }
                if(type==7u) return length(p.xz)-a.x;
                if(type==8u) return SdCappedCone(p,a.y,a.z,0.0);
                if(type==9u) { float2 sc=float2(sin(d.w),cos(d.w)); float2 q=float2(length(p.xz),-p.y); float value=length(q-sc*max(dot(q,sc),0.0)); return value*((q.x*sc.y-q.y*sc.x<0.0)?-1.0:1.0); }
                if(type==10u) return dot(p,a.xyz)+a.w;
                if(type==11u) { const float3 k=float3(-0.8660254,0.5,0.57735); p=abs(p); p.xy-=2.0*min(dot(k.xy,p.xy),0.0)*k.xy; float2 q=float2(length(p.xy-float2(clamp(p.x,-k.z*a.z,k.z*a.z),a.z))*sign(p.y-a.z),p.z-a.y); return min(max(q.x,q.y),0.0)+length(max(q,0.0)); }
                if(type==12u) { float3 q=abs(p); return max(q.z-b.z,max(q.x*0.866025+p.y*0.5,-p.y)-b.x*0.5); }
                if(type==13u) return SdCapsule(p,a.xyz,b.xyz,c.x);
                if(type==14u) { p.y-=clamp(p.y,0.0,a.y); return length(p)-a.x; }
                if(type==15u) return SdCappedCylinder(p,a.y,a.x);
                if(type==16u) return SdArbitraryCappedCylinder(p,a.xyz,b.xyz,c.x);
                if(type==17u) { float2 q=float2(length(p.xz)-a.z+a.w,abs(p.y)-a.y); return min(max(q.x,q.y),0.0)+length(max(q,0.0))-a.w; }
                if(type==18u) return SdCappedCone(p,a.y,a.z,a.w);
                if(type==19u) return SdArbitraryCappedCone(p,a.xyz,b.xyz,a.w,b.w);
                if(type==20u) { float2 sc=float2(sin(d.w),cos(d.w)); float2 q=float2(length(p.xz),p.y); float l=length(q)-a.x; float m=length(q-sc*clamp(dot(q,sc),0.0,a.x)); return max(l,m*sign(sc.y*q.x-sc.x*q.y)); }
                if(type==21u) { float w=sqrt(max(a.x*a.x-a.y*a.y,0.0)); float2 q=float2(length(p.xz),p.y); float s=max((a.y-a.x)*q.x*q.x+w*w*(a.y+a.x-2.0*q.y),a.y*q.x-w*q.y); return (s<0.0)?length(q)-a.x:(q.x<w)?a.y-q.y:length(q-float2(w,a.y)); }
                if(type==22u) { float w=sqrt(max(a.x*a.x-a.y*a.y,0.0)); float2 q=float2(length(p.xz),p.y); return ((a.y*q.x<w*q.y)?length(q-float2(w,a.y)):abs(length(q)-a.x))-c.w; }
                if(type==23u) { float aa=(a.z*a.z-a.w*a.w+a.y*a.y)/(2.0*max(a.y,1e-6)); float bb=sqrt(max(a.z*a.z-aa*aa,0.0)); float2 q=float2(p.x,length(p.yz)); return (q.x*bb-q.y*aa>a.y*max(bb-q.y,0.0))?length(q-float2(aa,bb)):max(length(q)-a.z,-(length(q-float2(a.y,0.0))-a.w)); }
                if(type==24u) return SdRoundCone(p,a.xyz,b.xyz,a.w,b.w);
                if(type==25u) { float3 center=(a.xyz+b.xyz)*0.5, segment=b.xyz-a.xyz; float l=max(length(segment),1e-6); float3 v=segment/l; float y=dot(p-center,v); float2 q=float2(length(p-center-y*v),abs(y)); float r=0.5*l, width=max(c.x,1e-5), offset=0.5*(r*r-width*width)/width; float3 h=(r*q.x<offset*(q.y-r))?float3(0.0,r,0.0):float3(-offset,0.0,offset+width); return length(q-h.xy)-h.z; }
                if(type==26u) { float3 r=max(b.xyz,1e-5); float k0=length(p/r), k1=length(p/(r*r)); return k0*(k0-1.0)/max(k1,1e-6); }
                if(type==27u) { p=abs(p); float2 bb=float2(a.z,a.w); float f=clamp((bb.x*(bb.x-2.0*p.x)-bb.y*(bb.y-2.0*p.z))/dot(bb,bb),-1.0,1.0); float2 q=float2(length(p.xz-0.5*bb*float2(1.0-f,1.0+f))*sign(p.x*bb.y+p.z*bb.x-bb.x*bb.y)-b.w,p.y-a.y); return min(max(q.x,q.y),0.0)+length(max(q,0.0)); }
                if(type==28u) return SdOctahedron(p,a.x);
                if(type==29u) { p=abs(p); return (p.x+p.y+p.z-a.x)*0.57735027; }
                if(type==30u) { float h=a.y, m2=h*h+0.25; p.xz=abs(p.xz); p.xz=(p.z>p.x)?p.zx:p.xz; p.xz-=0.5; float3 q=float3(p.z,h*p.y-0.5*p.x,h*p.x+0.5*p.y); float s=max(-q.x,0.0),t=clamp((q.y-0.5*p.z)/(m2+0.25),0.0,1.0); float aa=m2*(q.x+s)*(q.x+s)+q.y*q.y,bb=m2*(q.x+0.5*t)*(q.x+0.5*t)+(q.y-m2*t)*(q.y-m2*t); float d2=min(q.y,-q.x*m2-q.y*0.5)>0.0?0.0:min(aa,bb); return sqrt((d2+q.z*q.z)/m2)*sign(max(q.z,-p.y)); }
                if(type==31u) return UdTriangle(p,a.xyz,b.xyz,c.xyz)-a.w;
                return min(UdTriangle(p,a.xyz,b.xyz,c.xyz),UdTriangle(p,a.xyz,c.xyz,d.xyz))-a.w;
            }

            float EvaluateShape(float3 positionWS, SDFShapeGpu shape)
            {
                float3 p=ToLocal(positionWS,shape);
                float correction=1.0;
                float extrusionDistance=-1e20;
                uint start=(uint)(shape.TypeOperationSmoothModifierStart.w+0.5);
                uint count=(uint)(shape.MaterialModifierCountDistanceScaleBoundsScale.y+0.5);
                [loop] for(uint i=0u;i<count;++i) ApplyDomainModifier(p,correction,extrusionDistance,_SDFModifiers[start+i]);
                uint type=(uint)(shape.TypeOperationSmoothModifierStart.x+0.5);
                float distance=EvaluatePrimitive(p,type,shape.Parameters0,shape.Parameters1,shape.Parameters2,shape.Parameters3);
                if(extrusionDistance>-1e19)
                {
                    float2 extrusion=float2(distance,extrusionDistance);
                    distance=min(max(extrusion.x,extrusion.y),0.0)+length(max(extrusion,0.0));
                }
                distance*=shape.MaterialModifierCountDistanceScaleBoundsScale.z*correction;
                return ApplyDistanceModifiers(distance,start,count,shape.MaterialModifierCountDistanceScaleBoundsScale.z*correction);
            }

            bool CanSkipUnion(float3 p,SDFShapeGpu shape,float current,float smoothing)
            {
                float boundsScale=shape.MaterialModifierCountDistanceScaleBoundsScale.w;
                if(boundsScale<=0.0) return false;
                float squared=SquaredDistanceToAabb(p,shape.BoundsMin.xyz,shape.BoundsMax.xyz)*boundsScale*boundsScale;
                float threshold=max(current,0.0)+smoothing;
                return squared>=threshold*threshold;
            }

            bool CanSkipSubtraction(float3 p,SDFShapeGpu shape,float current,float smoothing)
            {
                float boundsScale=shape.MaterialModifierCountDistanceScaleBoundsScale.w;
                if(boundsScale<=0.0) return false;
                float squared=SquaredDistanceToAabb(p,shape.BoundsMin.xyz,shape.BoundsMax.xyz)*boundsScale*boundsScale;
                // A zero AABB distance means the point is inside the conservative
                // operand volume, where a lower distance bound cannot reject it.
                if(squared<=0.0) return false;
                float threshold=max(smoothing-current,0.0);
                return squared>=threshold*threshold;
            }

            float EvaluateModel(float3 p,uint modelIndex)
            {
                SDFModelGpu model=_SDFModels[modelIndex];
                uint start=(uint)(model.BoundsMinAndShapeStart.w+0.5), count=(uint)(abs(model.BoundsMaxAndShapeCount.w)+0.5);
                float current=EvaluateShape(p,_SDFShapes[start]);
                [loop] for(uint i=1u;i<count;++i)
                {
                    SDFShapeGpu shape=_SDFShapes[start+i];
                    uint operation=(uint)(shape.TypeOperationSmoothModifierStart.y+0.5);
                    float k=shape.TypeOperationSmoothModifierStart.z;
                    if((operation==0u||operation==3u)&&CanSkipUnion(p,shape,current,operation==3u?k:0.0)) continue;
                    if((operation==1u||operation==4u)&&CanSkipSubtraction(p,shape,current,operation==4u?k:0.0)) continue;
                    float operand=EvaluateShape(p,shape);
                    if(operation==0u) current=min(current,operand);
                    else if(operation==1u) current=max(current,-operand);
                    else if(operation==2u) current=max(current,operand);
                    else if(operation==3u&&k>1e-7) { float h=saturate(0.5+0.5*(operand-current)/k); current=lerp(operand,current,h)-k*h*(1.0-h); }
                    else if(operation==4u&&k>1e-7) { float h=saturate(0.5-0.5*(current+operand)/k); current=lerp(current,-operand,h)+k*h*(1.0-h); }
                    else if(operation==5u&&k>1e-7) { float h=saturate(0.5-0.5*(operand-current)/k); current=lerp(operand,current,h)+k*h*(1.0-h); }
                    else if(operation==3u) current=min(current,operand);
                    else current=max(current,operation==4u?-operand:operand);
                }
                return current;
            }

            float3 EvaluateNormal(float3 p,uint modelIndex,float epsilon)
            {
                const float3 e1=float3(1,-1,-1),e2=float3(-1,-1,1),e3=float3(-1,1,-1),e4=float3(1,1,1);
                return normalize(e1*EvaluateModel(p+e1*epsilon,modelIndex)+e2*EvaluateModel(p+e2*epsilon,modelIndex)+e3*EvaluateModel(p+e3*epsilon,modelIndex)+e4*EvaluateModel(p+e4*epsilon,modelIndex));
            }

            float2 EvaluateSdfAmbientOcclusion(float2 screenUV)
            {
                if(_SDFUseUrpScreenSpaceAO==0||_SDFAmbientOcclusionStrength<=0.0) return 1.0.xx;
                AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(screenUV);
                return lerp(1.0.xx,float2(ao.directAmbientOcclusion,ao.indirectAmbientOcclusion),
                    saturate(_SDFAmbientOcclusionStrength));
            }

            float EvaluateSdfLocalAmbientOcclusion(float3 p,float3 normalWS,uint modelIndex,float pixelWorldSize)
            {
                if(_SDFAmbientOcclusionEnabled==0||_SDFAmbientOcclusionStrength<=0.0) return 1.0;
                float radius=max(_SDFAmbientOcclusionRadius,pixelWorldSize*2.0);
                float occlusion=0.0;
                float normalization=0.0;
                float weight=1.0;
                [loop] for(int sampleIndex=0;sampleIndex<_SDFAmbientOcclusionSamples;++sampleIndex)
                {
                    float h=radius*(sampleIndex+1.0)/_SDFAmbientOcclusionSamples;
                    float distanceToSurface=EvaluateModel(p+normalWS*h,modelIndex);
                    occlusion+=max(h-distanceToSurface,0.0)*weight;
                    normalization+=h*weight;
                    weight*=0.6;
                }
                return saturate(1.0-_SDFAmbientOcclusionStrength*occlusion/max(normalization,1e-5));
            }

            half3 SampleSdfShadowAmbient(half3 normalWS)
            {
                half4 normalAndConstant=half4(normalWS,1.0h);
                half3 l0l1=half3(dot(_SDFShadowSHAr,normalAndConstant),
                    dot(_SDFShadowSHAg,normalAndConstant),dot(_SDFShadowSHAb,normalAndConstant));
                half4 quadratic=normalWS.xyzz*normalWS.yzzx;
                half3 l2=half3(dot(_SDFShadowSHBr,quadratic),
                    dot(_SDFShadowSHBg,quadratic),dot(_SDFShadowSHBb,quadratic));
                l2+=_SDFShadowSHC.rgb*(normalWS.x*normalWS.x-normalWS.y*normalWS.y);
                return max(l0l1+l2,0.0h);
            }

            float EvaluateSdfSelfShadow(float3 p)
            {
                if(_SDFSelfShadows==0||_SDFShadowStrength<=0.0) return 1.0;
                Light mainLight=GetMainLight(TransformWorldToShadowCoord(p));
                return lerp(1.0,mainLight.shadowAttenuation,saturate(_SDFShadowStrength));
            }

            half4 ShadeUrpLit(SDFMaterialGpu material,float3 baseColor,SDFSurfaceContext context)
            {
                SurfaceData surfaceData=(SurfaceData)0;
                surfaceData.albedo=baseColor;
                surfaceData.specular=half3(0.0,0.0,0.0);
                surfaceData.metallic=saturate(material.ModelMetallicSmoothness.y);
                surfaceData.smoothness=saturate(material.ModelMetallicSmoothness.z);
                surfaceData.normalTS=half3(0.0,0.0,1.0);
                surfaceData.emission=material.EmissionAndCelBands.rgb;
                surfaceData.occlusion=saturate(material.ModelMetallicSmoothness.w);
                surfaceData.alpha=material.BaseColor.a;
                surfaceData.clearCoatMask=0.0;
                surfaceData.clearCoatSmoothness=0.0;

                InputData inputData=(InputData)0;
                inputData.positionWS=context.positionWS;
                inputData.positionCS=mul(_SDFViewProjection,float4(context.positionWS,1.0));
                inputData.normalWS=NormalizeNormalPerPixel(context.normalWS);
                inputData.viewDirectionWS=SafeNormalize(context.viewDirectionWS);
                inputData.shadowCoord=TransformWorldToShadowCoord(context.positionWS);
                inputData.fogCoord=0.0;
                inputData.vertexLighting=half3(0.0,0.0,0.0);
                // URP supplies directional ambient light through spherical harmonics
                // and reflection probes. Retain the explicit ambient term inherited
                // from the original renderer as a floor when the scene has no useful
                // ambient probe instead of adding it twice in normally lit scenes.
                inputData.bakedGI=max(SampleSH(inputData.normalWS),(half3)_SDFAmbientColor);
                inputData.normalizedScreenSpaceUV=context.screenUV;
                inputData.shadowMask=half4(1.0,1.0,1.0,1.0);

                BRDFData brdfData;
                InitializeBRDFData(surfaceData,brdfData);
                BRDFData clearCoatData=CreateClearCoatBRDFData(surfaceData,brdfData);
                if(_SDFPreviewMode!=0)
                {
                    Light previewLight=(Light)0;
                    previewLight.direction=SafeNormalize(context.lightDirectionWS);
                    previewLight.color=context.lightColor;
                    previewLight.distanceAttenuation=1.0;
                    previewLight.shadowAttenuation=1.0;
                    half3 previewColor=brdfData.diffuse*context.ambientColor*surfaceData.occlusion;
                    previewColor+=LightingPhysicallyBased(brdfData,clearCoatData,previewLight,inputData.normalWS,
                        inputData.viewDirectionWS,surfaceData.clearCoatMask,false);
                    previewColor+=surfaceData.emission;
                    return half4(previewColor,surfaceData.alpha);
                }
                half4 shadowMask=CalculateShadowMask(inputData);
                AmbientOcclusionFactor aoFactor;
                // EvaluateSurface fetches SSAO once per visible hit. Reusing it here
                // avoids repeating the same texture lookup for every material touched
                // by a smooth CSG blend.
                aoFactor.directAmbientOcclusion=context.directAmbientOcclusion;
                aoFactor.indirectAmbientOcclusion=min(surfaceData.occlusion,context.ambientOcclusion);

                Light mainLight=GetMainLight(inputData,shadowMask,aoFactor);
                if(_SDFReceiveUrpShadows==0) mainLight.shadowAttenuation=1.0;
                else mainLight.shadowAttenuation=lerp(1.0,mainLight.shadowAttenuation,saturate(_SDFShadowStrength));
                MixRealtimeAndBakedGI(mainLight,inputData.normalWS,inputData.bakedGI);

                LightingData lightingData=CreateLightingData(inputData,surfaceData);
                lightingData.giColor=GlobalIllumination(brdfData,clearCoatData,surfaceData.clearCoatMask,
                    inputData.bakedGI,aoFactor.indirectAmbientOcclusion,inputData.positionWS,inputData.normalWS,
                    inputData.viewDirectionWS,inputData.normalizedScreenSpaceUV);
                // Procedural draws do not have a Renderer from which Unity can bind a
                // per-object reflection probe. Preserve URP's probe result when one is
                // available, but provide the configured ambient floor to the specular
                // lobe as well as diffuse GI so smooth metals never collapse to black.
                half NoV=saturate(dot(inputData.normalWS,inputData.viewDirectionWS));
                half fresnelTerm=Pow4(1.0h-NoV);
                half3 ambientFloor=(half3)_SDFAmbientColor*aoFactor.indirectAmbientOcclusion;
                half3 reflectVector=reflect(-inputData.viewDirectionWS,inputData.normalWS);
                half reflectionMip=PerceptualRoughnessToMipmapLevel(brdfData.perceptualRoughness);
                half4 encodedFallbackReflection=half4(SAMPLE_TEXTURECUBE_LOD(
                    _GlossyEnvironmentCubeMap,sampler_GlossyEnvironmentCubeMap,reflectVector,reflectionMip));
                half3 fallbackReflection=DecodeHDREnvironment(encodedFallbackReflection,
                    _GlossyEnvironmentCubeMap_HDR)*aoFactor.indirectAmbientOcclusion;
                half3 fallbackGi=EnvironmentBRDF(brdfData,ambientFloor,
                    max(fallbackReflection,ambientFloor),fresnelTerm);
                lightingData.giColor=max(lightingData.giColor,fallbackGi);
                lightingData.mainLightColor=LightingPhysicallyBased(brdfData,clearCoatData,mainLight,
                    inputData.normalWS,inputData.viewDirectionWS,surfaceData.clearCoatMask,false);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint pixelLightCount=GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for(uint lightIndex=0u;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);++lightIndex)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light=GetAdditionalLight(lightIndex,inputData,shadowMask,aoFactor);
                            if(_SDFReceiveUrpShadows==0) light.shadowAttenuation=1.0;
                            lightingData.additionalLightsColor+=LightingPhysicallyBased(brdfData,clearCoatData,light,
                                inputData.normalWS,inputData.viewDirectionWS,surfaceData.clearCoatMask,false);
                        }
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light=GetAdditionalLight(lightIndex,inputData,shadowMask,aoFactor);
                        if(_SDFReceiveUrpShadows==0) light.shadowAttenuation=1.0;
                        lightingData.additionalLightsColor+=LightingPhysicallyBased(brdfData,clearCoatData,light,
                            inputData.normalWS,inputData.viewDirectionWS,surfaceData.clearCoatMask,false);
                    LIGHT_LOOP_END
                #endif

                return CalculateFinalColor(lightingData,surfaceData.alpha);
            }

            float3 ShadeMaterial(uint materialIndex,SDFSurfaceContext context)
            {
                SDFMaterialGpu material=_SDFMaterials[materialIndex];
                uint model=(uint)(material.ModelMetallicSmoothness.x+0.5);
                uint textureIndex=(uint)(material.CustomShaderTextureIndices.y+0.5);
                float3 baseColor=material.BaseColor.rgb;
                if(textureIndex>0u) baseColor*=SampleSDFTexture(textureIndex,context.positionOS.xz).rgb;
                if(model==1u) return baseColor+material.EmissionAndCelBands.rgb;
                if(model==4u) return SDFShadeCustom((uint)(material.CustomShaderTextureIndices.x+0.5),context,material)+material.EmissionAndCelBands.rgb;
                if(model==3u) return ShadeUrpLit(material,baseColor,context).rgb;
                float directVisibility=context.selfShadow;
                float ndotl=saturate(dot(context.normalWS,context.lightDirectionWS))*directVisibility;
                if(model==2u) { float bands=max(material.EmissionAndCelBands.w,1.0); ndotl=floor(ndotl*bands+1e-4)/max(bands-1.0,1.0); }
                float3 halfDirection=normalize(context.lightDirectionWS+context.viewDirectionWS);
                float specular=ndotl>0.0?pow(saturate(dot(context.normalWS,halfDirection)),material.SpecularAndPower.w)*directVisibility:0.0;
                float3 diffuse=baseColor*(context.ambientColor*context.ambientOcclusion+context.lightColor*ndotl);
                return diffuse+material.SpecularAndPower.rgb*context.lightColor*specular+material.EmissionAndCelBands.rgb;
            }

            float3 EvaluateSurface(float3 p,uint modelIndex,float3 normalWS,float3 viewDirection,float2 screenUV)
            {
                SDFModelGpu model=_SDFModels[modelIndex];
                uint start=(uint)(model.BoundsMinAndShapeStart.w+0.5),count=(uint)(abs(model.BoundsMaxAndShapeCount.w)+0.5);
                SDFShapeGpu first=_SDFShapes[start];
                float currentDistance=EvaluateShape(p,first);
                SDFSurfaceContext context;
                context.positionWS=p; context.positionOS=ToLocal(p,first); context.normalWS=normalWS; context.viewDirectionWS=viewDirection;
                context.uv=context.positionOS.xz; context.screenUV=screenUV; context.screenPosition=float4(screenUV,screenUV*_ScaledScreenParams.xy);
                context.cameraPositionWS=_SDFCameraPosition; context.cameraForwardWS=_SDFCameraForward;
                context.lightDirectionWS=_SDFLightDirection; context.lightColor=_SDFLightColor; context.ambientColor=_SDFAmbientColor;
                float2 ambientOcclusion=EvaluateSdfAmbientOcclusion(screenUV);
                context.directAmbientOcclusion=ambientOcclusion.x;
                float cameraDistance=max(distance(p,_SDFCameraPosition),_SDFCameraNear);
                float pixelWorldSize=_SDFOrthographic>0.5?_SDFPixelWorldScale:cameraDistance*_SDFPixelWorldScale;
                context.ambientOcclusion=ambientOcclusion.y*EvaluateSdfLocalAmbientOcclusion(p,normalWS,modelIndex,pixelWorldSize);
                context.selfShadow=EvaluateSdfSelfShadow(p);
                float3 currentColor=ShadeMaterial((uint)(first.MaterialModifierCountDistanceScaleBoundsScale.x+0.5),context);
                [loop] for(uint i=1u;i<count;++i)
                {
                    SDFShapeGpu shape=_SDFShapes[start+i];
                    uint operation=(uint)(shape.TypeOperationSmoothModifierStart.y+0.5); float k=shape.TypeOperationSmoothModifierStart.z;
                    if((operation==0u||operation==3u)&&CanSkipUnion(p,shape,currentDistance,operation==3u?k:0.0)) continue;
                    if((operation==1u||operation==4u)&&CanSkipSubtraction(p,shape,currentDistance,operation==4u?k:0.0)) continue;
                    float operandDistance=EvaluateShape(p,shape);
                    bool shadeOperand=false;
                    float blendWeight=0.0;
                    if(operation==0u) { if(operandDistance<currentDistance){currentDistance=operandDistance;shadeOperand=true;} }
                    else if(operation==1u) { float cut=-operandDistance; if(cut>currentDistance){currentDistance=cut;shadeOperand=true;} }
                    else if(operation==2u) { if(operandDistance>currentDistance){currentDistance=operandDistance;shadeOperand=true;} }
                    else if(operation==3u&&k>1e-7)
                    {
                        float h=saturate(0.5+0.5*(operandDistance-currentDistance)/k);
                        currentDistance=lerp(operandDistance,currentDistance,h)-k*h*(1-h);
                        shadeOperand=h<1.0; blendWeight=h;
                    }
                    else if(operation==4u&&k>1e-7)
                    {
                        float h=saturate(0.5-0.5*(currentDistance+operandDistance)/k);
                        currentDistance=lerp(currentDistance,-operandDistance,h)+k*h*(1-h);
                        shadeOperand=h>0.0; blendWeight=1.0-h;
                    }
                    else if(operation==5u&&k>1e-7)
                    {
                        float h=saturate(0.5-0.5*(operandDistance-currentDistance)/k);
                        currentDistance=lerp(operandDistance,currentDistance,h)+k*h*(1-h);
                        shadeOperand=h<1.0; blendWeight=h;
                    }
                    else if(operation==3u) { if(operandDistance<currentDistance){currentDistance=operandDistance;shadeOperand=true;} }
                    else if(operation==4u) { float cut=-operandDistance; if(cut>currentDistance){currentDistance=cut;shadeOperand=true;} }
                    else if(operation==5u) { if(operandDistance>currentDistance){currentDistance=operandDistance;shadeOperand=true;} }

                    if(shadeOperand)
                    {
                        context.positionOS=ToLocal(p,shape); context.uv=context.positionOS.xz;
                        float3 operandColor=ShadeMaterial((uint)(shape.MaterialModifierCountDistanceScaleBoundsScale.x+0.5),context);
                        if(operation==3u||operation==4u||operation==5u)
                            currentColor=lerp(operandColor,currentColor,blendWeight);
                        else
                            currentColor=operandColor;
                    }
                }
                return currentColor;
            }

            float DeviceDepth(float3 p)
            {
                float4 clip=mul(_SDFViewProjection,float4(p,1.0));
                float depth=clip.z/clip.w;
                #if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
                    depth=depth*0.5+0.5;
                #endif
                return depth;
            }

            struct ShadowFragmentOutput
            {
                float depth : SV_Depth;
            };

            ShadowFragmentOutput FragShadow(Varyings input)
            {
                SDFModelGpu model=_SDFModels[input.modelIndex];
                // The cascade matrix used by Vert already clips this instance to the
                // correct atlas tile. A fragment-space tile test is both redundant and
                // wrong on APIs whose SV_Position Y convention differs from shadow UVs.
                float3 rayDirection=-normalize(_SDFLightDirection);
                float3 rayOrigin=input.positionWS-rayDirection*_SDFSurfaceEpsilon*2.0;
                float2 boxHit=IntersectAabb(rayOrigin,rayDirection,model.BoundsMinAndShapeStart.xyz,model.BoundsMaxAndShapeCount.xyz);
                float rayDistance=max(boxHit.x,0.0);
                float rayEnd=min(boxHit.y,rayDistance+_SDFShadowMaxDistance);
                if(rayEnd<=rayDistance) discard;
                bool hit=false;
                [loop] for(int stepIndex=0;stepIndex<_SDFShadowMaxSteps;++stepIndex)
                {
                    float sampleDistance=EvaluateModel(rayOrigin+rayDirection*rayDistance,input.modelIndex);
                    if(abs(sampleDistance)<=_SDFSurfaceEpsilon){hit=true;break;}
                    rayDistance+=max(abs(sampleDistance)*_SDFShadowStepSafety,_SDFSurfaceEpsilon*0.5);
                    if(rayDistance>rayEnd) break;
                }
                if(!hit) discard;
                float3 hitPosition=rayOrigin+rayDirection*rayDistance-rayDirection*_SDFShadowBias;
                float4 shadowPosition=mul(_MainLightWorldToShadow[min(input.shadowCascade,3u)],float4(hitPosition,1.0));
                ShadowFragmentOutput output;
                output.depth=shadowPosition.z/shadowPosition.w;
                return output;
            }

            half4 FragScreenSpaceShadow(Varyings input) : SV_Target
            {
                float2 screenUV=input.positionCS.xy/_ScaledScreenParams.xy;
                float deviceDepth=SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                    if(deviceDepth<=1e-6) discard;
                #else
                    if(deviceDepth>=1.0-1e-6) discard;
                #endif

                float3 receiverPosition=ComputeWorldSpacePosition(screenUV,deviceDepth,UNITY_MATRIX_I_VP);
                float3 receiverNormal=normalize(SampleSceneNormals(screenUV));
                float receiverPixelSize=_SDFOrthographic>0.5?_SDFPixelWorldScale:
                    max(distance(receiverPosition,_SDFCameraPosition),_SDFCameraNear)*_SDFPixelWorldScale;
                float traceBias=max(_SDFShadowBias,receiverPixelSize*max(_SDFPixelTolerance,0.25)*2.0);
                // Do not multiply a caster's own already-PBR-shaded surface. Its
                // N.L term already removes direct light on the back side; multiplying
                // the finished color here would incorrectly crush ambient and probe GI.
                float receiverDistance=EvaluateModel(receiverPosition,input.modelIndex);
                if(abs(receiverDistance)<=max(traceBias*1.5,_SDFSurfaceEpsilon*4.0)) discard;
                float3 rayDirection=normalize(_SDFLightDirection);
                float3 rayOrigin=receiverPosition+receiverNormal*traceBias+rayDirection*traceBias;
                SDFModelGpu model=_SDFModels[input.modelIndex];
                float2 boxHit=IntersectAabb(rayOrigin,rayDirection,
                    model.BoundsMinAndShapeStart.xyz,model.BoundsMaxAndShapeCount.xyz);
                float rayDistance=max(boxHit.x,0.0);
                float rayEnd=min(boxHit.y,_SDFShadowMaxDistance);
                if(rayEnd<=rayDistance) discard;

                float visibility=1.0;
                [loop] for(int stepIndex=0;stepIndex<_SDFShadowMaxSteps;++stepIndex)
                {
                    float sampleDistance=EvaluateModel(rayOrigin+rayDirection*rayDistance,input.modelIndex);
                    if(_SDFShadowSoftness>0.0)
                        visibility=min(visibility,saturate(max(sampleDistance,0.0)/
                            max(_SDFShadowSoftness*max(rayDistance,traceBias),1e-5)));
                    if(abs(sampleDistance)<=max(_SDFSurfaceEpsilon,receiverPixelSize*0.25)){visibility=0.0;break;}
                    rayDistance+=max(abs(sampleDistance)*_SDFShadowStepSafety,_SDFSurfaceEpsilon*0.5);
                    if(rayDistance>rayEnd) break;
                }
                // Tiny soft-shadow estimates become very visible when several caster
                // volumes overlap. Reject that low-confidence tail and remap the useful
                // penumbra range; hard hits remain exactly zero visibility.
                float shadowAmount=saturate((0.97-visibility)/0.97);
                if(shadowAmount<=1e-4) discard;

                // A multiplicative screen-space shadow cannot reconstruct the receiver's
                // BRDF, but a luminance-normalized SH tint retains the scene's ambient
                // colour while preserving the established shadow density.
                half3 ambientLight=max(SampleSdfShadowAmbient((half3)receiverNormal),(half3)_SDFAmbientColor);
                half ambientLuminance=max(dot(ambientLight,half3(0.2126h,0.7152h,0.0722h)),0.05h);
                half3 ambientShadowFloor=saturate(ambientLight*(0.35h/ambientLuminance));
                half blend=saturate((half)(_SDFShadowStrength*shadowAmount));
                return half4(lerp(1.0h.xxx,ambientShadowFloor,blend),1.0h);
            }

            struct CameraTraceOutput
            {
                float3 hitPosition;
                float3 normal;
                float3 viewDirection;
                float2 screenUV;
                float depth;
            };

            CameraTraceOutput ReuseDepthNormalPrepass(Varyings input)
            {
                float2 screenUV=input.positionCS.xy/_ScaledScreenParams.xy;
                float deviceDepth=SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                    if(deviceDepth<=1e-7) discard;
                #else
                    if(deviceDepth>=0.9999999) discard;
                #endif

                float3 hitPosition=ComputeWorldSpacePosition(screenUV,deviceDepth,UNITY_MATRIX_I_VP);
                float3 viewDirection;
                float cameraDistance;
                if(_SDFOrthographic>0.5)
                {
                    viewDirection=-normalize(_SDFCameraForward);
                    cameraDistance=max(abs(dot(hitPosition-_SDFCameraPosition,_SDFCameraForward)),_SDFCameraNear);
                }
                else
                {
                    float3 toCamera=_SDFCameraPosition-hitPosition;
                    cameraDistance=max(length(toCamera),_SDFCameraNear);
                    viewDirection=toCamera/cameraDistance;
                }
                float pixelSize=(_SDFOrthographic>0.5)?_SDFPixelWorldScale:cameraDistance*_SDFPixelWorldScale;
                float epsilon=max(_SDFSurfaceEpsilon,pixelSize*_SDFPixelTolerance);
                // The shared depth texture also contains regular opaque geometry and
                // nearer SDF models. Accept the cached hit only when this model's
                // field confirms the same surface at depth-buffer precision.
                if(abs(EvaluateModel(hitPosition,input.modelIndex))>max(epsilon*2.0,_SDFSurfaceEpsilon*4.0)) discard;

                float3 normal=SampleSceneNormals(screenUV);
                if(dot(normal,normal)<0.25)
                    normal=EvaluateNormal(hitPosition,input.modelIndex,max(_SDFNormalEpsilon,epsilon));
                CameraTraceOutput output;
                output.hitPosition=hitPosition;
                output.normal=normalize(normal);
                output.viewDirection=viewDirection;
                output.screenUV=screenUV;
                output.depth=deviceDepth;
                return output;
            }

            CameraTraceOutput TraceCamera(Varyings input)
            {
                if(_SDFPassMode==0&&_SDFReuseDepthNormalPrepass!=0)
                    return ReuseDepthNormalPrepass(input);
                SDFModelGpu model=_SDFModels[input.modelIndex];
                float3 ro,rd;
                if(_SDFOrthographic>0.5){rd=normalize(_SDFCameraForward);ro=input.positionWS-rd*dot(input.positionWS-_SDFCameraPosition,rd);}
                else{ro=_SDFCameraPosition;rd=normalize(input.positionWS-ro);}
                float2 boxHit=IntersectAabb(ro,rd,model.BoundsMinAndShapeStart.xyz,model.BoundsMaxAndShapeCount.xyz);
                float rayDistance=max(boxHit.x,_SDFCameraNear),rayEnd=min(boxHit.y,_SDFMaxDistance);
                if(rayEnd<=rayDistance) discard;
                bool hit=false; float sampleDistance=0.0;
                [loop] for(int stepIndex=0;stepIndex<_SDFMaxSteps;++stepIndex)
                {
                    sampleDistance=EvaluateModel(ro+rd*rayDistance,input.modelIndex);
                    float pixelSize=(_SDFOrthographic>0.5)?_SDFPixelWorldScale:max(rayDistance,_SDFCameraNear)*_SDFPixelWorldScale;
                    float epsilon=max(_SDFSurfaceEpsilon,pixelSize*_SDFPixelTolerance);
                    if(abs(sampleDistance)<=epsilon){hit=true;break;}
                    rayDistance+=max(abs(sampleDistance)*_SDFStepSafety,epsilon*0.5);
                    if(rayDistance>rayEnd) break;
                }
                if(!hit) discard;
                float3 hitPosition=ro+rd*rayDistance;
                float pixelSize=(_SDFOrthographic>0.5)?_SDFPixelWorldScale:max(rayDistance,_SDFCameraNear)*_SDFPixelWorldScale;
                float3 normal=EvaluateNormal(hitPosition,input.modelIndex,max(_SDFNormalEpsilon,pixelSize*_SDFPixelTolerance));
                CameraTraceOutput output;
                output.hitPosition=hitPosition;
                output.normal=normal;
                output.viewDirection=-rd;
                output.screenUV=input.positionCS.xy/_ScaledScreenParams.xy;
                output.depth=DeviceDepth(hitPosition);
                return output;
            }

            FragmentOutput FragColor(Varyings input)
            {
                CameraTraceOutput trace=TraceCamera(input);
                FragmentOutput output;
                output.color=float4(EvaluateSurface(trace.hitPosition,input.modelIndex,trace.normal,trace.viewDirection,trace.screenUV),1.0);
                output.depth=trace.depth;
                return output;
            }

            FragmentOutput FragDepthNormals(Varyings input)
            {
                CameraTraceOutput trace=TraceCamera(input);
                FragmentOutput output;
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal=PackNormalOctQuadEncode(trace.normal);
                    output.color=float4(PackFloat2To888(saturate(octNormal*0.5+0.5)),0.0);
                #else
                    output.color=float4(trace.normal,0.0);
                #endif
                output.depth=trace.depth;
                return output;
            }
        ENDHLSL

        Pass
        {
            Name "SDFRaymarch"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            // FXC's optimizer becomes super-linear when the complete dynamic SDF
            // evaluator is combined with URP's PBR/reflection code. Keep this scoped
            // to the D3D11 color program; other backends and the lean auxiliary passes
            // still use their normal optimization pipeline.
            #pragma skip_optimizations d3d11
            #pragma vertex Vert
            #pragma fragment FragColor
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            ENDHLSL
        }

        Pass
        {
            Name "SDFDepthNormals"
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragDepthNormals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }

        Pass
        {
            Name "SDFMainLightShadowCaster"
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragShadow
            ENDHLSL
        }

        Pass
        {
            Name "SDFScreenSpaceShadows"
            Cull Back
            ZWrite Off
            ZTest Always
            Blend DstColor Zero
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragScreenSpaceShadow
            ENDHLSL
        }
    }
    Fallback Off
}
