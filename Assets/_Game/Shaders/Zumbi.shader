// Os zumbis leves (24/10/2026): um modelo só com a aparência variando de zumbi para zumbi por camadas de textura.
// Cada zumbi tem a sua cópia do material (Zombies/Aparencia.cs sorteia as cores e as camadas e troca o rosto pela
// expressão). As texturas vêm do Blender (Modelo 3D/Zumbis/texturas_leves.py e rostos_leves.py, na pasta de arte):
//   _BaseMap    a textura do modelo, com as regiões que mudam de cor (pele, roupa de cima, de baixo e cabelo) em cinza:
//               0,5 (na luz linear) é a cor média da região;
//   _Mascaras   RGBA = pele, roupa de cima, roupa de baixo, cabelo;
//   _Sujeiras   10 camadas (Texture2DArray), RGBA = sangue, sujeira, ferida (carne à mostra), sangue seco. Uma camada
//               dá o sangue (R, B e A) e outra a sujeira (G);
//   _Rosto      a folha de rostos, 8 × 8 quadros (linha 0: olhos; 1: bocas; 2 e 3: marcas), no segundo UV ("Rosto"):
//               as faces da frente da cabeça têm o UV do rosto em 0..1; as outras, (-1, -1).
// A conta (a mesma da prévia do Blender, previa_leves.py):
//   cor = base × ((1 − soma das máscaras) + 2 × (pele·M.r + camiseta·M.g + calça·M.b + cabelo·M.a))
//   a sujeira multiplica pelo tom dela; o sangue seco escurece; o sangue e a ferida pintam por cima;
//   o rosto (olhos, boca e duas marcas) vai por cima de tudo, só na pele. Duas cores da folha são chaves: o verde puro
//   vira a cor dos olhos do zumbi e o magenta puro a cor do cabelo (as sobrancelhas).
// Iluminação: a PBR da URP (UniversalFragmentPBR), como o URP Lit que os zumbis usavam: sol e sombras, as luzes dos
// postes e a lanterna (Forward+, cookies, camadas de luz), SSAO, reflexos e a neblina. Compatível com o SRP Batcher
// (todas as propriedades no UnityPerMaterial).
Shader "ZombieCityCleaner/Zumbi"
{
    Properties
    {
        [MainTexture] _BaseMap("Base (regiões de cor em cinza)", 2D) = "white" {}
        [NoScaleOffset] _Mascaras("Máscaras (pele, cima, baixo, cabelo)", 2D) = "black" {}
        [NoScaleOffset] _Sujeiras("Sujeiras (sangue, sujeira, ferida, seco)", 2DArray) = "black" {}
        [NoScaleOffset] _Rosto("Folha de rostos", 2D) = "black" {}

        [Header(Cores do zumbi)]
        _CorPele("Pele", Color) = (0.62, 0.68, 0.55, 1)
        _CorCima("Roupa de cima", Color) = (0.894, 0.881, 0.875, 1)
        _CorBaixo("Roupa de baixo", Color) = (0.355, 0.404, 0.496, 1)
        _CorCabelo("Cabelo", Color) = (0.449, 0.234, 0.153, 1)
        _CorOlhos("Olhos", Color) = (0.78, 0.70, 0.20, 1)

        [Header(Camadas)]
        _Camadas("Camadas (sangue, força do sangue, sujeira, força da sujeira)", Vector) = (0, 1, 0, 1)
        _Quadros("Quadros do rosto (olhos, boca, marca 1, marca 2; -1 = nada)", Vector) = (0, 8, -1, -1)

        [Header(Tons)]
        _CorSangue("Sangue", Color) = (0.48, 0.05, 0.05, 1)
        _TomSangueSeco("Sangue seco (multiplica)", Color) = (0.58, 0.33, 0.29, 1)
        _CorFerida("Ferida", Color) = (0.62, 0.20, 0.18, 1)
        _CorFeridaFundo("Fundo da ferida", Color) = (0.25, 0.05, 0.05, 1)
        _TomSujeira("Sujeira (multiplica)", Color) = (0.58, 0.53, 0.45, 1)
        _Smoothness("Brilho", Range(0, 1)) = 0.15
        _BrilhoDoSangue("Brilho do sangue", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _CorPele;
            half4 _CorCima;
            half4 _CorBaixo;
            half4 _CorCabelo;
            half4 _CorOlhos;
            float4 _Camadas;
            float4 _Quadros;
            half4 _CorSangue;
            half4 _TomSangueSeco;
            half4 _CorFerida;
            half4 _CorFeridaFundo;
            half4 _TomSujeira;
            half _Smoothness;
            half _BrilhoDoSangue;
        CBUFFER_END
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

            TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Mascaras);           SAMPLER(sampler_Mascaras);
            TEXTURE2D_ARRAY(_Sujeiras);     SAMPLER(sampler_Sujeiras);
            TEXTURE2D(_Rosto);              SAMPLER(sampler_Rosto);

            // a chave da folha de rostos (0,85 na imagem sRGB) na luz linear
            #define CHAVE 0.694

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uvRosto : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;          // xy = UV do corpo, zw = UV do rosto
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
                o.uv = float4(TRANSFORM_TEX(input.uv, _BaseMap), input.uvRosto);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // um quadro da folha de rostos por cima da cor (as chaves trocadas pelas cores do zumbi), só na pele
            half3 CamadaDoRosto(half3 cor, float2 uvRosto, float quadro, half naPele)
            {
                if (quadro < 0.0) return cor;
                float2 uv = clamp(uvRosto, 0.002, 0.998);
                float coluna = fmod(quadro, 8.0), linha = floor(quadro / 8.0);
                half4 c = SAMPLE_TEXTURE2D(_Rosto, sampler_Rosto, float2((coluna + uv.x) / 8.0, (7.0 - linha + uv.y) / 8.0));
                half chaveOlhos = saturate((c.g - max(c.r, c.b)) * 1.5);
                half chaveCabelo = saturate((min(c.r, c.b) - c.g) * 1.5);
                half3 rgb = lerp(c.rgb, _CorOlhos.rgb * (c.g / CHAVE), chaveOlhos);
                rgb = lerp(rgb, _CorCabelo.rgb * (max(c.r, c.b) / CHAVE) * 0.55, chaveCabelo);
                return lerp(cor, rgb, c.a * naPele);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv.xy;

                // as cores do zumbi
                half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half4 m = SAMPLE_TEXTURE2D(_Mascaras, sampler_Mascaras, uv);
                half soma = m.r + m.g + m.b + m.a;
                half3 tinta = _CorPele.rgb * m.r + _CorCima.rgb * m.g + _CorBaixo.rgb * m.b + _CorCabelo.rgb * m.a;
                half3 cor = base * ((1.0 - soma) + 2.0 * tinta);

                // a sujeira, o sangue seco, o sangue e as feridas
                half4 s = SAMPLE_TEXTURE2D_ARRAY(_Sujeiras, sampler_Sujeiras, uv, _Camadas.x);
                half sujeira = SAMPLE_TEXTURE2D_ARRAY(_Sujeiras, sampler_Sujeiras, uv, _Camadas.z).g;
                cor = lerp(cor, cor * _TomSujeira.rgb, saturate(sujeira * _Camadas.w) * 0.9);
                cor = lerp(cor, cor * _TomSangueSeco.rgb, saturate(s.a * _Camadas.y) * 0.85);
                half sangue = saturate(s.r * _Camadas.y * 1.6);
                cor = lerp(cor, _CorSangue.rgb, sangue);
                half ferida = saturate(s.b * saturate(_Camadas.y * 2.0));
                cor = lerp(cor, _CorFeridaFundo.rgb, saturate(ferida * 3.0));
                cor = lerp(cor, _CorFerida.rgb, saturate((ferida - 0.45) * 2.5));

                // o rosto: os olhos, a boca e as duas marcas
                float2 uvRosto = input.uv.zw;
                if (uvRosto.x > -0.5)
                {
                    half naPele = saturate((m.r - 0.3) * 2.0);
                    cor = CamadaDoRosto(cor, uvRosto, _Quadros.x, naPele);
                    cor = CamadaDoRosto(cor, uvRosto, _Quadros.y, naPele);
                    cor = CamadaDoRosto(cor, uvRosto, _Quadros.z, naPele);
                    cor = CamadaDoRosto(cor, uvRosto, _Quadros.w, naPele);
                }

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = cor;
                surface.alpha = 1.0;
                surface.metallic = 0.0;
                surface.specular = half3(0.0, 0.0, 0.0);
                surface.smoothness = lerp(_Smoothness, _BrilhoDoSangue, sangue);
                surface.occlusion = 1.0;
                surface.normalTS = half3(0.0, 0.0, 1.0);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI = SampleSH(inputData.normalWS);
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
        // as normais para o SSAO (como o DepthNormals do URP Lit sem mapa de normais)
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
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
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
