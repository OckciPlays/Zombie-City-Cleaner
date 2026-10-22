// Visual de PS1: vértices presos a uma grade de pixels (tremida), textura com mapeamento afim
// (distorção), cor de 15 bits com dithering e neblina. A iluminação é por pixel, para a lanterna
// (spot light) funcionar em paredes e pisos grandes.
Shader "ZombieCityCleaner/PS1 Lit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Textura", 2D) = "white" {}
        [MainColor] _BaseColor ("Cor", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emissão", Color) = (0, 0, 0, 0)
        _VertexSnap ("Tremida dos vértices (pixels)", Range(0, 4)) = 1
        _Affine ("Distorção afim da textura", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            float _VertexSnap;
            float _Affine;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Luz ambiente global, definida pelo PrototypeGame (não depende de bake de iluminação).
        half4 _PS1Ambient;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        // Prende a posição na tela a uma grade de N pixels do render target (que é de baixa resolução).
        float4 PS1Snap(float4 positionCS)
        {
            if (_VertexSnap <= 0.0) return positionCS;
            float2 grid = _ScreenParams.xy / (2.0 * _VertexSnap);
            float4 p = positionCS;
            p.xy = round(p.xy / p.w * grid) / grid * p.w;
            return p;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 uvw : TEXCOORD1;      // uv * w e w: dividindo no fragment dá a interpolação afim
                float3 positionWS : TEXCOORD2;
                half3 normalWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            static const float kBayer4x4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionWS = pos.positionWS;
                o.positionCS = PS1Snap(pos.positionCS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.uvw = float3(o.uv * o.positionCS.w, o.positionCS.w);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = lerp(i.uv, i.uvw.xy / i.uvw.z, _Affine);
                half4 sampled = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half4 texel = sampled * _BaseColor;
                clip(texel.a - 0.5);   // recorte simples (clarão do tiro, manchas de sangue)
                half3 albedo = texel.rgb;
                half3 n = normalize(i.normalWS);

                half3 lighting = _PS1Ambient.rgb;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                lighting += mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * saturate(dot(n, mainLight.direction)));

            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    lighting += light.color * (light.distanceAttenuation * light.shadowAttenuation * saturate(dot(n, light.direction)));
                LIGHT_LOOP_END
            #endif

                // A emissão usa a textura (sem a cor base), para objetos emissivos manterem o desenho.
                half3 color = albedo * lighting + sampled.rgb * _EmissionColor.rgb;
                color = MixFog(color, i.fogFactor);

                // Cor de 15 bits (5 por canal) com dithering ordenado 4x4, em espaço sRGB.
                uint2 px = (uint2)i.positionCS.xy % 4u;
                float dither = (kBayer4x4[px.y * 4u + px.x] + 0.5) / 16.0 - 0.5;
                float3 srgb = LinearToSRGB(saturate(color));
                srgb = floor(srgb * 31.0 + dither + 0.5) / 31.0;
                return half4(SRGBToLinear(saturate(srgb)), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection)));
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(Attributes v) : SV_POSITION
            {
                return PS1Snap(TransformObjectToHClip(v.positionOS.xyz));
            }

            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
