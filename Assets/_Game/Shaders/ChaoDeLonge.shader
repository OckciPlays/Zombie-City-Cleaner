// O relevo de longe (09/11/2026, a pedido: "a textura de perto não bate com a que se vê ao longe"): o mesmo Chao.shader,
// com os pesos das texturas no vértice (o ChaoDeLakewood.Pesos a cada 25 m), para o longe ter as mesmas texturas do
// perto, sem emenda; e a COPA DA MATA: o UV do vértice guarda (a altura da copa, de 0 a 1 vezes 16 m; a fração de mata),
// a malha sobe para a altura da copa só longe da câmera (de 430 a 650 m, onde as árvores de perto acabam) e ganha a cor
// salpicada das copas (_CopaCor com um ruído na cor e na normal). A neve nos picos vem do Chao.shader.
// O chão do mundo gerado (Plano_Mapa, Parte M2; o TerrainBlend do Guardiões da Floresta). A única textura do cenário:
// as construções e os objetos continuam nas cores da paleta.
// - A COR DO VÉRTICE guarda os pesos (DadosDoMundo.PesosDoChao): R = grama seca, G = terra, B = cascalho, A = folhas mortas.
// - As texturas são projetadas de cima (coordenadas do mundo, _Tiling metros por repetição).
// - A mistura é "por altura": o alfa de cada textura (tufos, pedrinhas, folhas) decide quem aparece na transição, e a
//   borda fica irregular, não um degradê borrado.
// - Uma variação grande de tom (_VariacaoForca) para a repetição não aparecer de longe.
// - A rocha entra sozinha no chão inclinado (as encostas das serras), projetada dos lados (triplanar) para não esticar.
// Iluminação: a PBR da URP (UniversalFragmentPBR), como o URP Lit e o shader dos zumbis: o sol e as sombras, as luzes dos
// postes e a lanterna (Forward+, cookies, camadas de luz), o SSAO e a neblina. Compatível com o SRP Batcher.
// As texturas são feitas por código (Editor/TexturasDoChao, menu Zombie City Cleaner > Mundo > Gerar as texturas do chão).
Shader "ZombieCityCleaner/ChaoDeLonge"
{
    Properties
    {
        _GramaTex("Grama seca", 2D) = "white" {}
        _TerraTex("Terra", 2D) = "white" {}
        _CascalhoTex("Cascalho", 2D) = "white" {}
        _FolhasTex("Folhas mortas", 2D) = "white" {}
        _RochaTex("Rocha (encostas)", 2D) = "gray" {}
        _Tiling("Metros por repetição", Float) = 4
        _Nitidez("Nitidez da mistura", Range(0.05, 1)) = 0.3
        _VariacaoForca("Variação grande de tom", Range(0, 0.5)) = 0.2
        _VariacaoEscala("Tamanho da variação (m)", Float) = 42
        _RochaInclinacao("Rocha: a normal.y de / até", Vector) = (0.8, 0.62, 0, 0)
        _RochaTiling("Rocha: metros por repetição", Float) = 9
        _Smoothness("Brilho", Range(0, 1)) = 0.06
        _NeveAltura("Neve a partir de (m)", Float) = 100000
        _NeveCor("Cor da neve", Color) = (0.88, 0.9, 0.94, 1)
        _CopaCor("Cor das copas de longe", Color) = (0.16, 0.24, 0.13, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _GramaTex_ST;
            float _Tiling;
            half _Nitidez;
            half _VariacaoForca;
            float _VariacaoEscala;
            float4 _RochaInclinacao;
            float _RochaTiling;
            half _Smoothness;
            float _NeveAltura;
            half4 _NeveCor;
            half4 _CopaCor;
        CBUFFER_END

        float HashL(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float RuidoL(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(HashL(i), HashL(i + float2(1, 0)), u.x), lerp(HashL(i + float2(0, 1)), HashL(i + float2(1, 1)), u.x), u.y);
        }

        // a copa da mata sobe longe da câmera (perto, ela furava o terreno e as árvores de verdade)
        float3 LevantarCopa(float3 posWS, float copa)
        {
            float d = distance(posWS, _WorldSpaceCameraPos.xyz);
            posWS.y += copa * 16.0 * smoothstep(430.0, 650.0, d);
            return posWS;
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

            TEXTURE2D(_GramaTex);   SAMPLER(sampler_GramaTex);
            TEXTURE2D(_TerraTex);
            TEXTURE2D(_CascalhoTex);
            TEXTURE2D(_FolhasTex);
            TEXTURE2D(_RochaTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 pesos : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half2 copa : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = LevantarCopa(TransformObjectToWorld(input.positionOS.xyz), input.uv.x);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.pesos = input.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                o.copa = half2(input.uv.y, smoothstep(380.0, 600.0, distance(o.positionWS, _WorldSpaceCameraPos.xyz)));
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.positionWS.xz / max(_Tiling, 0.01);
                half4 grama = SAMPLE_TEXTURE2D(_GramaTex, sampler_GramaTex, uv);
                half4 terra = SAMPLE_TEXTURE2D(_TerraTex, sampler_GramaTex, uv * 0.9 + 0.37);
                half4 cascalho = SAMPLE_TEXTURE2D(_CascalhoTex, sampler_GramaTex, uv * 1.1 + 0.71);
                half4 folhas = SAMPLE_TEXTURE2D(_FolhasTex, sampler_GramaTex, uv * 0.8 + 0.13);

                // a mistura por altura: o alfa (o relevo da textura) desempata a transição
                half4 w = max(input.pesos, 0);
                w /= max(dot(w, 1), 0.0001);
                half4 h = half4(grama.a, terra.a, cascalho.a, folhas.a) * 0.5 + w;
                half topo = max(max(h.x, h.y), max(h.z, h.w)) - _Nitidez;
                half4 b = max(h - topo, 0);
                b /= max(dot(b, 1), 0.0001);
                half3 cor = grama.rgb * b.x + terra.rgb * b.y + cascalho.rgb * b.z + folhas.rgb * b.w;

                // a variação grande de tom (a própria grama, bem ampliada, como uma nuvem de cor)
                half macro = SAMPLE_TEXTURE2D(_GramaTex, sampler_GramaTex, input.positionWS.xz / max(_VariacaoEscala, 1) + 0.5).g;
                cor *= 1.0 + (macro - 0.4) * 2.0 * _VariacaoForca;

                // a rocha no chão inclinado, projetada dos lados conforme a face
                half3 n = NormalizeNormalPerPixel(input.normalWS);
                half pedra = smoothstep(_RochaInclinacao.x, _RochaInclinacao.y, n.y);
                if (pedra > 0.001)
                {
                    float3 pr = input.positionWS / max(_RochaTiling, 0.01);
                    half3 bw = pow(abs(n), 4.0);
                    bw /= max(dot(bw, 1.0), 0.0001);
                    half4 rocha = SAMPLE_TEXTURE2D(_RochaTex, sampler_GramaTex, pr.zy) * bw.x
                                + SAMPLE_TEXTURE2D(_RochaTex, sampler_GramaTex, pr.xz) * bw.y
                                + SAMPLE_TEXTURE2D(_RochaTex, sampler_GramaTex, pr.xy) * bw.z;
                    pedra = saturate((pedra - 0.5 + (rocha.a - 0.5) * 0.5) / 0.35 + 0.5);
                    cor = lerp(cor, rocha.rgb, pedra);
                }
                // a neve nos picos (acima de _NeveAltura, a borda torta pela variação grande), só no chão deitado
                half neve = smoothstep(_NeveAltura, _NeveAltura + 60.0, input.positionWS.y + (macro - 0.4) * 90.0) * saturate((n.y - 0.5) * 2.5);
                cor = lerp(cor, _NeveCor.rgb, neve);

                // a copa da mata: longe, a cor salpicada das copas no lugar do chão, com a normal entortada
                half copa = input.copa.x * input.copa.y;
                if (copa > 0.001)
                {
                    // as manchas da mata (os talhões mais claros e mais escuros, a 90 e 300 m) e o salpicado das copas (a 12 e 5 m)
                    float2 q = input.positionWS.xz;
                    float m1 = RuidoL(q / 300.0 + 41.0), m2 = RuidoL(q / 90.0 + 17.0);
                    float r1 = RuidoL(q / 12.0), r2 = RuidoL(q / 5.0 + 7.3);
                    half3 corDaCopa = _CopaCor.rgb * (1.0 + (m1 - 0.5) * 0.5 + (m2 - 0.5) * 0.35 + (r1 - 0.5) * 0.45 + (r2 - 0.5) * 0.25);
                    cor = lerp(cor, corDaCopa, copa);
                    n = normalize(n + float3(RuidoL(q / 35.0 + 3.1) - 0.5, 0.0, RuidoL(q / 35.0 + 11.7) - 0.5) * 0.9 * copa
                                    + float3(RuidoL(q / 9.0 + 5.3) - 0.5, 0.0, RuidoL(q / 9.0 + 13.9) - 0.5) * 0.5 * copa);
                }

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
                float2 uv : TEXCOORD0;
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
                float3 positionWS = LevantarCopa(TransformObjectToWorld(input.positionOS.xyz), input.uv.x);
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
                float2 uv : TEXCOORD0;
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
                o.positionCS = TransformWorldToHClip(LevantarCopa(TransformObjectToWorld(input.positionOS.xyz), input.uv.x));
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
                float2 uv : TEXCOORD0;
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
                o.positionCS = TransformWorldToHClip(LevantarCopa(TransformObjectToWorld(input.positionOS.xyz), input.uv.x));
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
