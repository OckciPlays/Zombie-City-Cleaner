// As poças da chuva forte (World/ClimaNaTela; a pedido em 02/11/2026: "em chuvas fortes, criam poças de água"). Um
// quadrado deitado no chão, recortado: a borda é um ruído (cada poça tem a sua, pela posição), e ela enche e seca com
// _ZccPocas (global, de 0 a 1: o Tempo.Pocas). Opaca como a água do lago (Agua.shader): transparente, ela ficaria limpa
// no meio da neblina de altura. Escura e bem lisa (os postes, a lanterna e o céu refletem), com os anéis das gotas
// batendo enquanto chove (_ZccChuva).
Shader "ZombieCityCleaner/ClimaPoca"
{
    Properties
    {
        _Cor ("Cor", Color) = (0.03, 0.032, 0.03, 1)
        _Brilho ("Brilho", Range(0, 1)) = 0.93
        _ReflexoDoCeu ("Reflexo do céu (o da cena)", Range(0, 1)) = 0.05
        _ReflexoDaNeblina ("Reflexo da neblina (o céu do jogo)", Range(0, 1.5)) = 0.85
        _Aneis ("Força dos anéis", Range(0, 2)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+12" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Cor;
            half _Brilho;
            half _ReflexoDoCeu;
            half _Aneis;
            half _ReflexoDaNeblina;
        CBUFFER_END

        float _ZccPocas;
        float _ZccChuva;
        float _ZccClarao;

        float Hash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float Ruido(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
        }

        // o recorte: positivo dentro da poça (quanto mais cheia, maior); a borda é um ruído próprio de cada poça
        float Dentro(float2 uv, float3 mundo)
        {
            float2 centro = float2(unity_ObjectToWorld._m03, unity_ObjectToWorld._m23);
            float semente = Hash(floor(centro * 3.1));
            float2 c = uv * 2.0 - 1.0;
            float borda = 0.45 + 0.4 * Ruido(mundo.xz * 0.9 + semente * 37.0) + 0.15 * Ruido(mundo.xz * 3.1 - semente * 11.0);
            float r = length(c) / borda;
            return _ZccPocas * 1.08 - r;
        }

        // os anéis das gotas: uma gota por célula de 30 cm, cada uma no seu tempo
        float Aneis(float2 xz)
        {
            float soma = 0.0;
            float2 celula = floor(xz / 0.3);
            [unroll] for (int y = -1; y <= 1; y++)
            [unroll] for (int x = -1; x <= 1; x++)
            {
                float2 cc = celula + float2(x, y);
                float sorte = Hash(cc);
                float2 centro = (cc + 0.2 + 0.6 * float2(sorte, Hash(cc + 7.1))) * 0.3;
                float ciclo = _Time.y * (0.9 + 0.6 * sorte) + sorte * 5.0;
                float fase = frac(ciclo);
                float d = length(xz - centro) - fase * 0.22;
                soma += exp(-d * d * 1800.0) * (1.0 - fase) * step(Hash(cc + floor(ciclo)), _ZccChuva * 0.9);
            }
            return soma;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float dentro = Dentro(input.uv, input.positionWS);
                clip(dentro);
                float2 xz = input.positionWS.xz;
                // a normal entortada pelos anéis
                float e = 0.01;
                float a0 = Aneis(xz);
                float ax = Aneis(xz + float2(e, 0)) - a0, az = Aneis(xz + float2(0, e)) - a0;
                float forca = _Aneis * 0.004 / e;
                half3 normal = normalize(half3(-ax * forca, 1.0, -az * forca));
                // a borda: um pouco mais clara (a lama molhada em volta)
                half borda = saturate(1.0 - dentro * 12.0);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(_Cor.rgb, _Cor.rgb * 3.0, borda);
                surface.alpha = 1.0;
                surface.smoothness = lerp(_Brilho, 0.6, borda);
                surface.occlusion = _ReflexoDoCeu;
                surface.normalTS = half3(0.0, 0.0, 1.0);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI = SampleSH(normal);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                // o céu do jogo é a neblina (o reflexo da cena é o céu azul padrão, que não aparece): a poça reflete a cor
                // dela, mais de lado (Fresnel), e o clarão do raio
                half fresnel = 0.08h + 0.92h * pow(1.0h - saturate(dot(normal, inputData.viewDirectionWS)), 4.0h);
                color.rgb += (unity_FogColor.rgb + _ZccClarao * half3(0.75, 0.8, 1.0)) * fresnel * _ReflexoDaNeblina * (1.0h - borda);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                return o;
            }

            half Frag(Varyings input) : SV_Target
            {
                clip(Dentro(input.uv, input.positionWS));
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                return o;
            }

            void Frag(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                clip(Dentro(input.uv, input.positionWS));
                half3 n = half3(0, 1, 0);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5);
                outNormalWS = half4(PackFloat2To888(oct), 0.0);
            #else
                outNormalWS = half4(n, 0.0);
            #endif
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
