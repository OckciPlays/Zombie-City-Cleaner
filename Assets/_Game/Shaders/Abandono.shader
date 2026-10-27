// A cidade abandonada (Plano_Mapa, Parte M9b; a cena de testes, para o usuário aprovar antes de ir para o jogo): o
// material da paleta (as construções feitas pelo jogo, as ruas, os carros e os objetos), com o abandono por cima.
// - A cor é a da Paleta.png pelo UV (sem filtro), e o tipo da superfície também: T_Abandono_Categorias tem, para cada
//   quadradinho da paleta, se é a pintura de fora (1), a alvenaria e as calçadas (2), o asfalto (3), o telhado (4), o
//   metal (5), a madeira (6), o vidro (7) ou o que é de dentro (8). Nada muda nas malhas.
// - As marcas saem do ruído (T_Abandono_Ruido) projetado no mundo: no plano da parede (as faces de pé) ou no chão (as
//   deitadas), em duas escalas; a escala grande (uns 9 m) também varia o quanto cada trecho está abandonado.
// - _Intensidade (0 a 1): na cena de testes, de cada objeto (um MaterialPropertyBlock); no mundo (Parte M9c), 1 no
//   material. As forças de cada marca (_Escorrido, _Umidade...) são do material, para a escolha do usuário.
// - A altura sobre o chão (a umidade na base das paredes, o descascado e a ferrugem mais embaixo), pelo _ModoDaAltura:
//   0, o y menos _Chao (por objeto na cena de testes; no mundo, -10000: sem base); 1, a cor do vértice (as casas de um
//   bloco numa malha só: os centímetros nos dois primeiros bytes, MalhaDeObra.Juntar); 2, o y do objeto (os objetos de
//   rua, com a origem no chão).
// O que cada tipo ganha:
//   pintura: desbota, escorre (as faixas escuras de cima para baixo), descasca (em grupos de manchas, mais embaixo: o
//            reboco e, no muito, um pouco do tijolo, com a borda escura), a umidade e o limo na base, as rachaduras;
//   alvenaria de pé: escorre, o salitre (as manchas brancas), umidade e limo, rachaduras; deitada (a calçada, o
//            concreto): rachaduras com limo, manchas de água, terra e folhas, poças;
//   asfalto: rachaduras com limo, remendos, manchas de óleo, terra e folhas, poças (escuras e brilhando);
//   telhado: musgo em manchas e o escorrido; metal: ferrugem em manchas; madeira: acinzentada e escorrida;
//   vidro: encardido (mais claro e fosco); de dentro: o mofo em manchas.
// Iluminação: a PBR da URP (UniversalFragmentPBR), como o shader do chão: o sol e as sombras, as luzes, o SSAO e a neblina.
// Com a variante das instâncias DOTS (DOTS.hlsl da URP) em todos os passes: sem ela, o GPU Resident Drawer não junta os
// objetos com este material, e no centro da cidade grande eram uns 290 draw calls a mais que com o Lit (Parte M9c).
Shader "ZombieCityCleaner/Abandono"
{
    Properties
    {
        _BaseMap("Paleta", 2D) = "white" {}
        _Categorias("Categorias da paleta", 2D) = "black" {}
        _Ruido("Ruído do abandono", 2D) = "gray" {}
        _Intensidade("Intensidade (por objeto)", Range(0, 1)) = 0.6
        _Chao("Altura do chão (m, por objeto)", Float) = -10000
        _ModoDaAltura("A altura sobre o chão: 0 = y - _Chao, 1 = a cor do vértice, 2 = o y do objeto", Float) = 0
        _Escorrido("Escorridos", Range(0, 1)) = 1
        _Desbotado("Pintura desbotada", Range(0, 1)) = 1
        _Descascado("Pintura descascando", Range(0, 1)) = 1
        _Umidade("Umidade e limo na base", Range(0, 1)) = 1
        _Rachadura("Rachaduras", Range(0, 1)) = 1
        _MusgoNoTelhado("Musgo nos telhados", Range(0, 1)) = 1
        _Ferrugem("Ferrugem", Range(0, 1)) = 1
        _Rua("Ruas: remendos, manchas, terra e folhas", Range(0, 1)) = 1
        _Pocas("Poças", Range(0, 1)) = 1
        _Mofo("Mofo de dentro", Range(0, 1)) = 1
        _Suavidade("Suavidade (vezes o alfa da paleta)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half _Intensidade;
            float _Chao;
            half _ModoDaAltura;
            half _Escorrido, _Desbotado, _Descascado, _Umidade, _Rachadura, _MusgoNoTelhado, _Ferrugem, _Rua, _Pocas, _Mofo;
            half _Suavidade;
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            TEXTURE2D(_Categorias);
            SAMPLER(sampler_abandono_point_clamp);
            TEXTURE2D(_Ruido); SAMPLER(sampler_Ruido);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float altura : TEXCOORD4;
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
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                // a altura sobre o chão (linear na face nos três modos: a interpolação continua certa)
                o.altura = _ModoDaAltura > 1.5 ? input.positionOS.y
                         : _ModoDaAltura > 0.5 ? input.color.r * 652.8 + input.color.g * 2.55
                         : o.positionWS.y - _Chao;
                return o;
            }

            half4 R(float2 q) { return SAMPLE_TEXTURE2D(_Ruido, sampler_Ruido, q); }

            // as marcas do chão (o asfalto e a calçada deitados): rachaduras com limo, remendos, manchas, terra e folhas, poças
            void Chao(inout half3 cor, inout half suave, float3 p, half k, half grande, bool asfalto)
            {
                float2 q = p.xz;
                half r1 = R(q * 0.09).b, r2 = R(q * 0.23 + 0.31).b;
                half rach = max(r1, r2 * smoothstep(0.45, 0.7, grande)) * smoothstep(0.62 - 0.45 * k, 0.9, grande + 0.25) * _Rachadura;
                cor = lerp(cor * (1.0 - rach * 0.6), half3(0.2, 0.25, 0.13), rach * 0.4 * k);
                if (asfalto)
                {
                    // remendos: retângulos de 2,5 m, uns mais escuros
                    half remendo = step(0.8 - 0.25 * k, R(floor(q / 2.5) * 0.173 + 0.05).r);
                    cor = lerp(cor, cor * 0.75, remendo * 0.7 * _Rua);
                    half oleo = smoothstep(0.68, 0.76, R(q * 0.21 + 0.6).g) * k;
                    cor = lerp(cor, cor * 0.55, oleo * 0.6 * _Rua);
                }
                else
                {
                    // as manchas de água no concreto (escuras, de borda mole)
                    half agua = smoothstep(0.6 - 0.2 * k, 0.72, R(q * 0.13 + 0.2).g * 0.7 + grande * 0.3) * k * _Rua;
                    cor *= 1.0 - agua * 0.35;
                }
                // a terra e as folhas acumuladas (no asfalto, menos e mais fraca: em manchas grandes, parecia uma camada
                // por cima da rua)
                half4 m = R(q * 0.27 + 0.13);
                half limSujo = asfalto ? 0.64 - 0.18 * k : 0.56 - 0.3 * k;
                half sujo = smoothstep(limSujo, limSujo + 0.14, m.r * 0.6 + grande * 0.4) * _Rua * k;
                half3 terra = lerp(half3(0.31, 0.26, 0.2), half3(0.41, 0.29, 0.17), m.g);
                cor = lerp(cor, terra, sujo * (asfalto ? 0.55 : 0.85));
                // as poças: escuras e brilhando, onde a escala grande é baixa
                half poca = (1.0 - smoothstep(0.17, 0.24, grande + (1.0 - k) * 0.12)) * _Pocas;
                cor = lerp(cor, cor * 0.5, poca);
                suave = lerp(suave, 0.92, poca);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 pal = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_abandono_point_clamp, input.uv, 0);
                int cat = (int)round(SAMPLE_TEXTURE2D_LOD(_Categorias, sampler_abandono_point_clamp, input.uv, 0).r * 255.0 / 25.0);
                half3 n = NormalizeNormalPerPixel(input.normalWS);
                float3 p = input.positionWS;
                half3 cor = pal.rgb;
                half suave = pal.a * _Suavidade;

                bool dePe = abs(n.y) < 0.6;
                // o plano da face: a parede (o eixo dela e a altura) ou o chão
                float2 q = dePe ? (abs(n.x) > abs(n.z) ? p.zy : p.xy) : p.xz;
                half4 perto = R(q * 0.31);
                half4 longe = R(q * 0.11 + 0.37);
                half grande = longe.r;
                // o quanto este trecho está abandonado: a intensidade do objeto, variando em manchas de uns 9 m
                half k = saturate(_Intensidade * (0.65 + 0.7 * longe.g));
                float altura = input.altura;

                if ((cat == 1 || cat == 2 || cat == 6) && dePe)
                {
                    if (cat == 1)
                    {
                        // desbotada (o sol de anos)
                        half cinza = dot(cor, half3(0.33, 0.34, 0.33));
                        cor = lerp(cor, cinza * 1.06 + 0.02, k * 0.4 * _Desbotado);
                    }
                    if (cat == 6) cor = lerp(cor, dot(cor, half3(0.33, 0.34, 0.33)) * 1.1 + 0.03, k * 0.55);
                    // os escorridos: o ruído fino na horizontal, esticado na vertical (uma coluna a cada uns 25 cm), só em
                    // uns trechos
                    half esc = R(float2(q.x * 0.14, q.y * 0.05)).a;
                    half trecho = smoothstep(0.42, 0.62, R(q * float2(0.06, 0.1) + 0.9).r);
                    half e = smoothstep(0.58 - 0.2 * k, 0.82, esc) * trecho * _Escorrido * k;
                    cor = lerp(cor, cor * half3(0.55, 0.52, 0.46), e * 0.7);
                    // a pintura descascando: manchas tortas (duas escalas, a grande entortada pela pequena), juntas em
                    // grupos (uma terceira, de uns 20 m) e mais embaixo; o reboco (puxado para a cor da parede) e, no muito,
                    // um pouco do tijolo nos miolos; a borda escura
                    if (cat == 1)
                    {
                        half b = R(q * 0.19 + (perto.rg - 0.5) * 0.35).g * 0.7 + R(q * 0.83 + 0.4).r * 0.3;
                        half grupo = R(q * 0.045 + 0.71).g;
                        b += (grupo - 0.5) * 0.35 + (1.0 - saturate(altura / 2.5)) * 0.07;
                        half lim = 1.0 - 0.36 * k * _Descascado;
                        half casca = smoothstep(lim, lim + 0.02, b);
                        half borda = smoothstep(lim - 0.03, lim, b) * (1.0 - casca);
                        half3 reboco = lerp(cor, half3(0.56, 0.54, 0.5), 0.65) * (0.92 + 0.16 * perto.r);
                        half3 fundo = lerp(reboco, half3(0.42, 0.29, 0.24), smoothstep(lim + 0.12, lim + 0.13, b) * step(0.75, k));
                        cor = lerp(cor * (1.0 - borda * 0.4), fundo, casca);
                    }
                    // o salitre na alvenaria: manchas brancas em uns trechos, mais embaixo
                    if (cat == 2)
                    {
                        half sal = R(q * 0.21 + 0.15).r * 0.6 + perto.g * 0.4 + (1.0 - saturate(altura / 2.0)) * 0.1;
                        sal = smoothstep(0.7 - 0.16 * k, 0.8, sal) * k * _Escorrido;
                        cor = lerp(cor, half3(0.6, 0.58, 0.53), sal * 0.45);
                    }
                    // a umidade e o limo na base, com a beirada torta
                    half h0 = (0.3 + 1.2 * k) * (0.65 + 0.7 * perto.r);
                    half base = (1.0 - smoothstep(h0 * 0.5, h0, altura)) * step(-0.3, altura) * _Umidade;
                    cor = lerp(cor, cor * 0.5 + half3(0.08, 0.12, 0.05), base * (0.3 + 0.5 * k));
                    // as rachaduras, em uns trechos
                    half rach = R(q * 0.16 + 0.5).b * smoothstep(0.72 - 0.45 * k, 0.95, grande) * _Rachadura;
                    cor *= 1.0 - rach * 0.75;
                    suave *= 1.0 - k * 0.6;
                }
                else if ((cat == 2 || cat == 3) && n.y > 0.6)
                {
                    Chao(cor, suave, p, k, grande, cat == 3);
                }
                else if (cat == 4)
                {
                    // o musgo no telhado e o escorrido nas faces de pé (oitões)
                    half musgo = smoothstep(0.6 - 0.32 * k, 0.7, perto.g) * _MusgoNoTelhado * k;
                    cor = lerp(cor, half3(0.25, 0.31, 0.17) * (0.8 + 0.4 * perto.r), musgo * 0.85);
                    half e = smoothstep(0.6 - 0.2 * k, 0.84, R(float2(q.x * 0.14, q.y * 0.05)).a) * _Escorrido * k;
                    cor *= 1.0 - e * 0.35;
                    suave *= 1.0 - k * 0.5;
                }
                else if (cat == 5)
                {
                    // a ferrugem em manchas (pequenas e grandes), mais embaixo (as bordas da lataria, o pé dos postes)
                    half f = R(q * 0.9).g * 0.6 + R(q * 2.3 + 0.2).r * 0.4;
                    f += (1.0 - saturate(altura / 1.2)) * 0.08;
                    half lim = 1.0 - 0.38 * k * _Ferrugem;
                    half fer = smoothstep(lim, lim + 0.04, f);
                    cor = lerp(cor, lerp(half3(0.33, 0.19, 0.12), half3(0.52, 0.29, 0.17), perto.r), fer);
                    suave *= 1.0 - fer;
                }
                else if (cat == 7)
                {
                    // o vidro encardido
                    cor = lerp(cor, cor * 0.8 + half3(0.12, 0.12, 0.11), k * 0.6);
                    suave *= 1.0 - k * 0.6;
                }
                else if (cat == 8)
                {
                    half mofo = smoothstep(0.7 - 0.25 * k, 0.8, perto.g) * _Mofo * k;
                    cor = lerp(cor, cor * 0.55 + half3(0.05, 0.07, 0.04), mofo * 0.7);
                }
                else
                {
                    cor *= 1.0 - k * 0.1;
                }

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = cor;
                surface.alpha = 1.0;
                surface.smoothness = suave;
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
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
