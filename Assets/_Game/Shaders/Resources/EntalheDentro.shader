// A madeira de dentro do entalhe do machado (18/11/2026, World/CorteNaMadeira.cs): as duas faces do corte em cunha (o
// fundo reto, com os anéis, e o alto inclinado, com as fibras e os degraus dos golpes), desenhadas sem o teste de
// profundidade só onde a boca do mesmo corte marcou o estêncil (EntalheBoca.shader): por dentro da árvore, como se a casca
// tivesse sido aberta. A luz é a PBR da URP (como o Chao.shader): o sol e as sombras, a lanterna e as outras luzes, a névoa.
// A cor vem dos vértices (o escuro perto do fundo do corte); o alfa diz se a face mostra os anéis (1) ou as fibras (0). O
// espaço do objeto tem o eixo do tronco no Y, na origem: os anéis e as fibras saem da distância e do ângulo em volta dele.
Shader "ZombieCityCleaner/EntalheDentro"
{
    Properties
    {
        _Ref ("Número do corte no estêncil", Float) = 32
        _Smoothness ("Smoothness", Range(0, 1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest+21" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _Smoothness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest Always
            Cull Back
            Stencil
            {
                Ref [_Ref]
                ReadMask 224
                WriteMask 224
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
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
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 foraXZ : TEXCOORD1;
                float2 foraY : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 cor : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                float3 foraWS : TEXCOORD5;
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
                o.positionOS = input.positionOS.xyz;
                o.foraWS = TransformObjectToWorld(float3(input.foraXZ.x, input.foraY.x, input.foraXZ.y));
                o.cor = input.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p = input.positionOS;
                float r = length(p.xz);
                float ang = atan2(p.x, p.z);
                // os anéis (no fundo reto, a ponta das fibras): uns 2,5 cm cada, um pouco tortos
                half aneis = 0.5 + 0.5 * sin(r * 250.0 + sin(ang * 3.0 + p.y * 7.0) * 1.8);
                aneis = smoothstep(0.15, 0.95, aneis);
                // as fibras (no alto inclinado, ao longo do tronco): riscos finos que tremem um pouco
                float fio = ang * max(r, 0.02) * 520.0 + sin(p.y * 55.0 + ang * 9.0) * 0.9;
                half fibras = 0.5 + 0.5 * sin(fio) * sin(fio * 0.37 + 1.3);
                half desenho = lerp(fibras, aneis, input.cor.a);
                half3 cor = input.cor.rgb * (0.80 + 0.20 * desenho);

                half3 n = NormalizeNormalPerPixel(input.normalWS);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = cor;
                surface.alpha = 1.0;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1.0;
                surface.normalTS = half3(0.0, 0.0, 1.0);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.foraWS);   // (a sombra por fora da casca)
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI = SampleSH(n);
                inputData.shadowMask = half4(1, 1, 1, 1);

                surface.emission = cor * 0.06;
                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
