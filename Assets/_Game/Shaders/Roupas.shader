// As roupas (Plano_Roupas.md da pasta de arte, 30/10/2026): um material só para todas as peças (as roupas, os cabelos e
// os acessórios dos NPCs e do personagem principal).
// - As texturas de cada peça ficam em duas Texture2DArray (montadas por Editor/MontagemDasRoupas a partir das tiras do
//   roupas_exportar.py): _Cores (sRGB) e _Mascaras (linear: R, G e B, as partes 1, 2 e 3 da cor que troca).
// - O UV1.y é a camada da peça nas duas.
// - A conta (a mesma do material do Blender, na luz linear, como a dos zumbis leves):
//     cor = base x ((1 - (R + G + B)) + 2 x (_Cor1 x R + _Cor2 x G + _Cor3 x B))
//   onde a cor troca, a base é cinza (0,5) com o claro e o escuro do desenho; as cores vêm do Vestimenta (por peça).
// - O corpo (o NPC_Corpo, a camada -1 no UV1.y) usa as texturas dele (_CorpoCor e _CorpoMascara, as da textura do
//   personagem principal: R a pele, G a cueca e B as sobrancelhas), com a mesma conta: as cores dele são a pele, a
//   cueca e a cor dos pelos (a do cabelo). Assim o corpo e as peças usam o mesmo material.
// - Os vidros (a lente dos óculos de proteção, a viseira; o lote 6, 31/10/2026): o alfa da textura de cor diz o quanto
//   eles cobrem, desenhado em pontilhado (a matriz de Bayer 4 x 4 na tela, a mesma em todos os passes: a cor, a
//   profundidade e as normais); na sombra, o vidro (alfa abaixo de 0,5) não entra. O resto tem alfa 1.
// Iluminação: a PBR da URP (UniversalFragmentPBR), como a da mobília. No retrato do inventário (_Retrato = 1, a cópia
// do material feita pelo RetratoDoJogador; o jogador vestido, 31/10/2026), a luz fixa do shader do retrato (a de cima e
// da frente e o contorno fraco, sem as luzes da cena, a neblina e o ambiente): o retrato fica igual de dia, de noite e
// no bunker.
Shader "ZombieCityCleaner/Roupas"
{
    Properties
    {
        _Cores("Cores (Texture2DArray)", 2DArray) = "" {}
        _Mascaras("Máscaras (Texture2DArray)", 2DArray) = "" {}
        _CorpoCor("Cor do corpo", 2D) = "gray" {}
        _CorpoMascara("Máscara do corpo", 2D) = "black" {}
        _Cor1("Cor 1", Color) = (0.5, 0.5, 0.5, 1)
        _Cor2("Cor 2", Color) = (0.5, 0.5, 0.5, 1)
        _Cor3("Cor 3", Color) = (0.5, 0.5, 0.5, 1)
        _Suavidade("Suavidade", Range(0, 1)) = 0.12
        [Toggle] _Retrato("Retrato (a luz fixa do inventário)", Float) = 0
        _Ambiente("Ambiente do retrato", Range(0, 1)) = 0.42
        _Luz("Luz do retrato", Range(0, 2)) = 0.72
        _DirecaoDaLuz("Direção da luz do retrato (mundo)", Vector) = (0.45, 0.8, 0.55, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Cor1;
            half4 _Cor2;
            half4 _Cor3;
            half _Suavidade;
            half _Retrato;
            half _Ambiente;
            half _Luz;
            float4 _DirecaoDaLuz;
        CBUFFER_END

        TEXTURE2D_ARRAY(_Cores);      SAMPLER(sampler_Cores);

        // o quanto o pixel cobre (o alfa da cor; o corpo, a camada -1, é sempre cheio)
        half AlfaDaPeca(float3 uv)
        {
            if (uv.z < -0.5) return 1.0;
            return SAMPLE_TEXTURE2D_ARRAY(_Cores, sampler_Cores, uv.xy, uv.z).a;
        }

        // o pontilhado dos vidros: a matriz de Bayer 4 x 4 na posição do pixel na tela
        void Pontilhado(half alfa, float2 pixel)
        {
            if (alfa >= 0.99) return;
            uint2 p = uint2(pixel) & 3u;
            static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            clip(alfa - (bayer[p.y * 4 + p.x] + 0.5) / 16.0);
        }
        ENDHLSL

        // ------------------------------------------------------------------
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
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_ARRAY(_Mascaras);
            TEXTURE2D(_CorpoCor);         SAMPLER(sampler_CorpoCor);
            TEXTURE2D(_CorpoMascara);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 face : TEXCOORD1;    // x: o número da face (o Vestimenta esconde as cobertas); y: a camada
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;      // xy: o UV; z: a camada
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
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
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = float3(input.uv, round(input.face.y));
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 base, m;
                UNITY_BRANCH
                if (input.uv.z < -0.5)
                {
                    base = SAMPLE_TEXTURE2D(_CorpoCor, sampler_CorpoCor, input.uv.xy).rgb;
                    m = SAMPLE_TEXTURE2D(_CorpoMascara, sampler_CorpoCor, input.uv.xy).rgb;
                }
                else
                {
                    half4 cor = SAMPLE_TEXTURE2D_ARRAY(_Cores, sampler_Cores, input.uv.xy, input.uv.z);
                    Pontilhado(cor.a, input.positionCS.xy);
                    base = cor.rgb;
                    m = SAMPLE_TEXTURE2D_ARRAY(_Mascaras, sampler_Cores, input.uv.xy, input.uv.z).rgb;
                }
                half3 tinta = _Cor1.rgb * m.r + _Cor2.rgb * m.g + _Cor3.rgb * m.b;
                half3 albedo = base * ((1.0 - (m.r + m.g + m.b)) + 2.0 * tinta);
                half3 n = NormalizeNormalPerPixel(input.normalWS);

                // o retrato do inventário: a luz fixa (a mesma conta do shader do retrato)
                UNITY_BRANCH
                if (_Retrato > 0.5)
                {
                    float3 l = normalize(_DirecaoDaLuz.xyz);
                    float direta = saturate(dot(n, l));
                    float contorno = saturate(dot(n, normalize(float3(-l.x, 0.25, -l.z)))) * 0.2;
                    return half4(saturate(albedo) * (_Ambiente + _Luz * direta + contorno), 1.0);
                }

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = saturate(albedo);
                surface.alpha = 1.0;
                surface.smoothness = _Suavidade;
                surface.occlusion = 1.0;
                surface.normalTS = half3(0.0, 0.0, 1.0);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI = SampleSH(n);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 face : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.uv = float3(input.uv, round(input.face.y));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(AlfaDaPeca(input.uv) - 0.5);
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
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
                float2 face : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = float3(input.uv, round(input.face.y));
                return o;
            }

            half Frag(Varyings input) : SV_Target
            {
                Pontilhado(AlfaDaPeca(input.uv), input.positionCS.xy);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // as normais para o SSAO
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
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 face : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 uv : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = float3(input.uv, round(input.face.y));
                return o;
            }

            void Frag(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                Pontilhado(AlfaDaPeca(input.uv), input.positionCS.xy);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = saturate(PackNormalOctQuadEncode(normalize(input.normalWS)) * 0.5 + 0.5);
                outNormalWS = half4(PackFloat2To888(oct), 0.0);
            #else
                outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
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
