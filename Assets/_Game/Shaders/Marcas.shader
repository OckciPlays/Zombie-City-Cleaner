// As marcas do abandono do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 5): os quadros colados no chão,
// nas paredes e no teto (Hospital.Marcas: o sangue seco, a água, o mofo, a sujeira, os furos de tiro, os papéis e os cacos),
// com o atlas T_Marcas (desenhado em código pelo hospital_marcas.py da pasta de arte). É o shader das pichações com o plano
// do chão e do teto: a cor do vértice tinge o tom do atlas (os papéis e os cacos vêm coloridos, com a cor branca) e o alfa
// dele é o quanto da marca ficou; por cima, o tempo apagando em manchas (_Desgaste).
// Transparente, com a luz da URP, sem escrever a profundidade e puxada um pouco para a frente (Offset); não faz sombra.
Shader "ZombieCityCleaner/Marcas"
{
    Properties
    {
        _BaseMap("Atlas das marcas", 2D) = "white" {}
        _Ruido("Ruído do abandono", 2D) = "gray" {}
        _Desgaste("Desgaste", Range(0, 1)) = 0.25
        _Suavidade("Suavidade", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-50" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -1, -1

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Desgaste, _Suavidade;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Ruido); SAMPLER(sampler_Ruido);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 R(float2 q) { return SAMPLE_TEXTURE2D(_Ruido, sampler_Ruido, q); }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 n = NormalizeNormalPerPixel(input.normalWS);
                float3 p = input.positionWS;
                half3 cor = t.rgb * input.color.rgb;
                half a = t.a * input.color.a;
                // o plano da marca: no chão e no teto, o x e o z; na parede, o eixo dela e a altura
                float2 q = abs(n.y) > 0.7 ? p.xz : (abs(n.x) > abs(n.z) ? p.zy : p.xy);
                // a marca apagando em manchas (pequenas e grandes) e a sujeira por cima
                half apaga = R(q * 0.9).g * 0.6 + R(q * 2.7 + 0.3).r * 0.4;
                a *= 1.0 - _Desgaste * 0.6 * smoothstep(0.56, 0.74, apaga);
                half esc = smoothstep(0.58, 0.82, R(float2(q.x * 0.14, q.y * 0.05)).a);
                cor *= 1.0 - esc * 0.35 * _Desgaste;
                cor = lerp(cor, dot(cor, half3(0.33, 0.34, 0.33)), 0.25 * _Desgaste);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = cor;
                surface.alpha = a;
                surface.smoothness = _Suavidade;
                surface.occlusion = 1.0;
                surface.normalTS = half3(0.0, 0.0, 1.0);

                InputData inputData = (InputData)0;
                inputData.positionWS = p;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(p);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(p);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.fogCoord = InitializeInputDataFog(float4(p, 1.0), input.fogFactor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI = SampleSH(n);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
