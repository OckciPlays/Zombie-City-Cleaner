// A água do lago (Plano_Mapa, Parte M2; a do Guardiões da Floresta, mais turva): escura e parada, OPACA. Transparente,
// ela seria desenhada depois da neblina de altura (que vem antes dos transparentes) e ficaria limpa no meio do chão
// enevoado; e água turva não mostra o fundo mesmo.
// - A cor do vértice (R) é a fundura (0 na margem, 1 a partir de 3 m, de MalhasDoTerreno.Agua): mais clara no raso e a
//   espuma suja numa linha na margem, recortada por ruído.
// - As ondinhas: um ruído lento entorta a normal (o reflexo das luzes treme) e desenha umas faixas.
// - Iluminação: a PBR da URP, bem lisa (os postes, a lanterna e o sol refletem), com a neblina.
Shader "ZombieCityCleaner/Agua"
{
    Properties
    {
        _CorFundo("Cor no fundo", Color) = (0.035, 0.045, 0.045, 1)
        _CorRaso("Cor no raso", Color) = (0.13, 0.13, 0.11, 1)
        _CorEspuma("Espuma da margem", Color) = (0.36, 0.35, 0.31, 1)
        _Espuma("Largura da espuma (fundura relativa)", Range(0, 0.3)) = 0.07
        _Brilho("Brilho", Range(0, 1)) = 0.86
        _ReflexoDoCeu("Reflexo do céu e luz ambiente", Range(0, 1)) = 0.3
        _OndasEscala("Escala das ondinhas", Float) = 0.45
        _OndasVelocidade("Velocidade das ondinhas", Float) = 0.08
        _OndasForca("Força das ondinhas na luz", Range(0, 0.5)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _CorFundo;
            half4 _CorRaso;
            half4 _CorEspuma;
            half _Espuma;
            half _Brilho;
            half _ReflexoDoCeu;
            float _OndasEscala;
            float _OndasVelocidade;
            half _OndasForca;
        CBUFFER_END

        // ruído de valor 2D suave (sem textura)
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

        // as duas camadas de ondinhas, andando devagar em direções diferentes
        float Ondas(float2 xz)
        {
            float t = _Time.y * _OndasVelocidade;
            float2 p = xz * _OndasEscala;
            return Ruido(p + float2(t, t * 0.6)) * 0.6 + Ruido(p * 2.3 - float2(t * 0.8, -t * 1.1)) * 0.4;
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
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fundura : TEXCOORD1;
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
                o.fundura = input.color.r;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 xz = input.positionWS.xz;
                float n = Ondas(xz);
                // a normal entortada pela inclinação do ruído
                float e = 0.35;
                float nx = Ondas(xz + float2(e, 0)) - n, nz = Ondas(xz + float2(0, e)) - n;
                half3 normal = normalize(half3(-nx * _OndasForca / e, 1.0, -nz * _OndasForca / e));

                half raso = 1.0 - saturate(input.fundura);
                half3 cor = lerp(_CorFundo.rgb, _CorRaso.rgb, raso * raso);
                cor *= 0.9 + 0.2 * step(0.62, n);   // as faixas das ondinhas
                // a espuma suja na margem, recortada pelo ruído
                half espuma = step(1.0 - _Espuma * (0.6 + 0.8 * Ruido(xz * 1.7 + _Time.y * 0.05)), raso);
                cor = lerp(cor, _CorEspuma.rgb, espuma);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = cor;
                surface.alpha = 1.0;
                surface.smoothness = lerp(_Brilho, 0.2, espuma);
                // o reflexo do céu quase apagado (a água turva fica escura); as luzes de verdade continuam refletindo
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
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1.0);
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half Frag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        // ------------------------------------------------------------------
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            void Frag(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
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
