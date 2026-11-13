// O vidro das roupas (07/11/2026): a lente dos óculos de proteção e a viseira, transparentes de verdade. É o segundo
// material das peças com vidro (a mesma malha desenhada de novo, pela Unity, com o material a mais; quem põe é o
// ItensDasRoupas.PorOVidro e o .Materiais): o das roupas desenha a peça sem o vidro (o alfa da cor abaixo da divisa
// sai), e este, só o vidro, misturado com o que está atrás, com o brilho da luz e mais opaco de lado (o Fresnel).
// Antes, o vidro era um pontilhado no material das roupas, e de perto se viam as bolinhas brancas.
// As texturas são as das roupas (o _Cores, a Texture2DArray; o UV1.y é a camada). Sem sombra e sem profundidade.
// No retrato do inventário (_Retrato = 1), a luz fixa do shader das roupas.
Shader "ZombieCityCleaner/RoupasVidro"
{
    Properties
    {
        _Cores("Cores (Texture2DArray)", 2DArray) = "" {}
        _Suavidade("Suavidade", Range(0, 1)) = 0.9
        _Lado("Opaco de lado (Fresnel)", Range(0, 1)) = 0.55
        [Toggle] _Retrato("Retrato (a luz fixa do inventário)", Float) = 0
        _Ambiente("Ambiente do retrato", Range(0, 1)) = 0.42
        _Luz("Luz do retrato", Range(0, 2)) = 0.72
        _DirecaoDaLuz("Direção da luz do retrato (mundo)", Vector) = (0.45, 0.8, 0.55, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Suavidade;
                half _Lado;
                half _Retrato;
                half _Ambiente;
                half _Luz;
                float4 _DirecaoDaLuz;
            CBUFFER_END

            TEXTURE2D_ARRAY(_Cores);      SAMPLER(sampler_Cores);

            // a mesma divisa do shader das roupas (lá o que fica abaixo dela sai)
            #define DIVISA_DO_VIDRO 0.6

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 face : TEXCOORD1;    // y: a camada
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;
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
                // o corpo (a camada -1) e o que não é vidro saem
                clip(input.uv.z + 0.5);
                half4 cor = SAMPLE_TEXTURE2D_ARRAY(_Cores, sampler_Cores, input.uv.xy, input.uv.z);
                clip(DIVISA_DO_VIDRO - cor.a);
                half3 n = NormalizeNormalPerPixel(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // mais opaco de lado (o Fresnel)
                half alfa = lerp(cor.a, max(cor.a, _Lado), pow(1.0 - saturate(abs(dot(n, v))), 4.0));

                UNITY_BRANCH
                if (_Retrato > 0.5)
                {
                    float3 l = normalize(_DirecaoDaLuz.xyz);
                    float direta = saturate(dot(n, l));
                    float brilho = pow(saturate(dot(n, normalize(l + v))), 48.0);
                    return half4(saturate(cor.rgb) * (_Ambiente + _Luz * direta) + brilho, saturate(alfa + brilho));
                }

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = saturate(cor.rgb);
                surface.alpha = alfa;
                surface.smoothness = _Suavidade;
                surface.specular = half3(0.04, 0.04, 0.04);
                surface.occlusion = 1.0;
                surface.normalTS = half3(0.0, 0.0, 1.0);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = v;
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
                return half4(color.rgb, saturate(alfa));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
