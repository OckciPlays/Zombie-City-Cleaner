// A vegetação do mundo gerado (Plano_Mapa, Parte M9a; o Vegetation.shader do Guardiões da Floresta): as árvores, os
// arbustos, o capim e as pedras do Cidade_Arvores.fbx (cidade_arvores.py), desenhados em instâncias (FlorestaDoMundo).
// - A cor é a da Paleta.png pelo UV, lida no vértice (cada face aponta para o quadradinho da cor dela) e sem filtro; o
//   alfa da paleta é a suavidade, como no material da cidade.
// - O vento vem da COR DO VÉRTICE: R = o balanço grande (pela altura: 0 no chão, 1 a 16 m), G = a fase da peça,
//   B = o tremor das folhas. A fase do balanço sai também do pé da árvore (cada uma balança no seu tempo). O tremor é
//   pela posição do vértice: os cubos vizinhos da copa dividem os cantos e andam juntos, sem abrir frestas.
// - O vento do mundo é global (_ZccVento: xy a direção no chão, z a força em metros, w a rajada de -1 a 1), posto pelo
//   FlorestaDoMundo; sem ele, nada balança.
// - _Some (x, y): a planta encolhe para o pé dela entre x e y metros da câmera (no chão), em vez de sumir de uma vez no
//   fim do alcance; o FlorestaDoMundo põe por espécie (a grama, o capim, os arbustos), num MaterialPropertyBlock. y = 0:
//   nunca some.
// - _Cima (0 a 1): a normal vira para cima (a grama e o capim recebem a luz como o chão; com a normal das folhas finas,
//   de lado, eles saíam quase pretos). Também por espécie, no mesmo bloco.
// Iluminação: a PBR da URP (UniversalFragmentPBR), como o shader do chão: o sol e as sombras, as luzes (Forward+), o SSAO
// e a neblina. As passadas de sombra e de profundidade balançam igual. Compatível com o SRP Batcher e com instâncias.
Shader "ZombieCityCleaner/Vegetacao"
{
    Properties
    {
        _BaseMap("Paleta", 2D) = "white" {}
        _Balanco("Balanço (vezes o vento)", Range(0, 3)) = 1
        _Tremor("Tremor das folhas (m)", Range(0, 0.2)) = 0.035
        _Suavidade("Suavidade (vezes o alfa da paleta)", Range(0, 1)) = 0.6
        _Some("Some de (m), até (m)", Vector) = (0, 0, 0, 0)
        _Cima("Normal para cima (a grama)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half _Balanco;
            half _Tremor;
            half _Suavidade;
            float4 _Some;
            half _Cima;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_vegetacao_point_clamp);

        float4 _ZccVento;

        // o vértice no mundo, encolhido no fim do alcance e com o vento
        float3 Vento(float3 positionOS, half4 cor)
        {
            float3 p = TransformObjectToWorld(positionOS);
            float3 pe = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
            if (_Some.y > 0.0)
            {
                float d = distance(pe.xz, _WorldSpaceCameraPos.xz);
                p = pe + (p - pe) * saturate((_Some.y - d) / max(_Some.y - _Some.x, 0.01));
            }
            float forca = _ZccVento.z;
            if (forca <= 0.0) return p;
            float fase = dot(pe.xz, float2(0.071, 0.053)) + cor.g * 6.2831853;
            float t = _Time.y;
            float balanco = sin(t * 0.9 + fase) * 0.6 + sin(t * 2.1 + fase * 1.7) * 0.25 + 0.35 + _ZccVento.w * 0.4;
            float b = balanco * forca * _Balanco * cor.r;
            p.xz += _ZccVento.xy * b;
            p.y -= abs(b) * 0.15;
            float tremor = sin(t * 4.3 + dot(p, float3(1.7, 1.3, 1.9)) + fase) * _Tremor * cor.b * (0.4 + forca * 2.0);
            p += float3(0.6, 0.35, 0.5) * tremor;
            return p;
        }

        // a normal no mundo, virada para cima pelo _Cima
        float3 Normal(float3 normalOS)
        {
            return normalize(lerp(TransformObjectToWorldNormal(normalOS), float3(0.0, 1.0, 0.0), _Cima));
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 cor : TEXCOORD0;
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
                o.positionWS = Vento(input.positionOS.xyz, input.color);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = Normal(input.normalOS);
                o.cor = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_vegetacao_point_clamp, input.uv, 0);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n = NormalizeNormalPerPixel(input.normalWS);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = input.cor.rgb;
                surface.alpha = 1.0;
                surface.smoothness = input.cor.a * _Suavidade;
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
                half4 color : COLOR;
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
                float3 positionWS = Vento(input.positionOS.xyz, input.color);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                return o;
            }

            half4 Frag(Varyings input) : SV_Target { return 0; }
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
                half4 color : COLOR;
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
                o.positionCS = TransformWorldToHClip(Vento(input.positionOS.xyz, input.color));
                return o;
            }

            half Frag(Varyings input) : SV_Target { return input.positionCS.z; }
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
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformWorldToHClip(Vento(input.positionOS.xyz, input.color));
                o.normalWS = Normal(input.normalOS);
                return o;
            }

            void Frag(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
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
