// A grama densa da floresta nova (o Plano_Floresta.md, "A grama densa", 09/11/2026): os tufos de folhas recortadas pelo
// alfa (o atlas Grama_Folhas.png, 4 colunas de tufos; cada tufo pega uma coluna pelo pé dele), desenhados em instâncias
// (Graphics.RenderMeshInstanced, pelo GramaDensa.cs) em volta do jogador.
// - O vento é o do ArvoreRealista.shader (a cor do vértice: R o balanço pela altura, G a fase, B o tremor; o global
//   _ZccVento) e a dobra com quem passa é a do Vegetacao.shader (_Dobra e os globais _ZccDobradores/_ZccDobradoresN:
//   o pé e o raio de cada um; quem passa empurra o tufo para longe e para baixo, mais em cima).
// - _Some (x, y): o tufo encolhe para o pé entre x e y metros da câmera, em vez de sumir de uma vez no fim do alcance;
//   _Aparece (x, y): o tufo cresce do pé entre x e y metros (o anel de longe, que começa dentro do de perto).
// - _Desbota (x, y): de x a y metros da câmera o tufo vai para a cor média do chão (_CorDoChao, a textura de grama do
//   Chao.shader), para o fim da grama não aparecer como uma borda (10/11/2026).
// - O vento (10/11/2026): a onda que corre pelo campo vai numa direção fixa (a média do vento do jogo). Com a direção do
//   vento, que gira devagar, o dot(pé, direção) a 9 km da origem (o Lakewood) andava dezenas de radianos por segundo, e a
//   grama tremia rápido sempre que o vento virava. A força tem um teto suave (a tempestade deita a grama firme, sem chicotear).
// - _Cima: a normal virada para cima (a grama ilumina como o chão, sem cada folha ter a sua sombra), e _CorDaBase
//   escurece o pé do tufo (a oclusão entre as folhas). _Translucencia: o sol atravessando as folhas quando ele está atrás.
// Iluminação: a PBR da URP, como as árvores. As passadas de profundidade e de normais dobram e recortam igual; a de
// sombra existe, mas a grama é desenhada sem sombra (ShadowCastingMode.Off).
Shader "ZombieCityCleaner/GramaDensa"
{
    Properties
    {
        _BaseMap("Folhas (A: o alfa)", 2D) = "white" {}
        _Cor("Tinta", Color) = (1, 1, 1, 1)
        _CorDaBase("Tinta do pé do tufo", Color) = (0.45, 0.5, 0.4, 1)
        _Cutoff("Corte do alfa", Range(0, 1)) = 0.4
        _Suavidade("Brilho", Range(0, 1)) = 0.05
        _Translucencia("Luz atravessando (as folhas)", Range(0, 2)) = 0.35
        _Balanco("Balanço (vezes o vento)", Range(0, 3)) = 1
        _Tremor("Tremor das pontas (m)", Range(0, 0.3)) = 0.03
        _Variacao("Variação de tom entre os tufos", Range(0, 1)) = 0.5
        _Dobra("Dobra com quem passa", Range(0, 1)) = 1
        _Cima("Normal para cima", Range(0, 1)) = 0.85
        _Colunas("Colunas do atlas", Float) = 4
        _Some("Some de (m), até (m)", Vector) = (0, 0, 0, 0)
        _Aparece("Cresce de (m), até (m)", Vector) = (0, 0, 0, 0)
        _Desbota("Vai para a cor do chão de (m), até (m)", Vector) = (0, 0, 0, 0)
        _CorDoChao("Cor média do chão (a grama de longe vai para ela)", Color) = (0.353, 0.433, 0.208, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _Cor;
            half4 _CorDaBase;
            half _Cutoff;
            half _Suavidade;
            half _Translucencia;
            half _Balanco;
            half _Tremor;
            half _Variacao;
            half _Dobra;
            half _Cima;
            half _Colunas;
            float4 _Some;
            float4 _Aparece;
            float4 _Desbota;
            half4 _CorDoChao;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        float4 _ZccVento;
        float4 _ZccDobradores[24];
        float _ZccDobradoresN;

        float3 Pe()
        {
            float4x4 M = GetObjectToWorldMatrix();
            return float3(M._m03, M._m13, M._m23);
        }

        float2 Hashes(float2 pe)
        {
            return float2(frac(sin(dot(pe, float2(12.9898, 78.233))) * 43758.5453), frac(sin(dot(pe, float2(39.3467, 11.135))) * 24634.6345));
        }

        // quem passa empurra o tufo para longe e para baixo, mais em cima (o Vegetacao.shader)
        float3 Dobrar(float3 p, float3 pe)
        {
            if (_Dobra <= 0.0) return p;
            float h = saturate((p.y - pe.y) * 1.6);
            int n = (int)_ZccDobradoresN;
            [loop] for (int i = 0; i < n; i++)
            {
                float4 b = _ZccDobradores[i];
                float2 longe = p.xz - b.xz;
                float d = length(longe);
                float empurra = saturate(1.0 - d / b.w) * saturate(2.5 - abs(p.y - b.y)) * _Dobra * h;
                p.xz += longe / max(d, 0.001) * empurra * 0.5;
                p.y -= empurra * 0.35;
            }
            return p;
        }

        // o vértice no mundo: encolhido no fim do alcance, com o vento e com quem passa
        float3 Vento(float3 positionOS, half4 cor, float3 pe)
        {
            float3 p = mul(GetObjectToWorldMatrix(), float4(positionOS, 1.0)).xyz;
            if (_Some.y > 0.0 || _Aparece.y > 0.0)
            {
                float d = distance(pe.xz, GetCameraPositionWS().xz);
                float tamanho = 1.0;
                if (_Some.y > 0.0) tamanho = saturate((_Some.y - d) / max(_Some.y - _Some.x, 0.01));
                if (_Aparece.y > 0.0) tamanho *= saturate((d - _Aparece.x) / max(_Aparece.y - _Aparece.x, 0.01));
                p = pe + (p - pe) * tamanho;
            }
            // a força com um teto suave (de 0,22 a uns 0,38 m): a calma fica igual, e a tempestade (até 0,95 no jogo)
            // não faz a ponta do tufo ir quase 1 m de um lado ao outro
            float forca = 0.42 * tanh(max(_ZccVento.z, 0.0) / 0.42);
            if (forca <= 0.0) return Dobrar(p, pe);
            float firme = saturate((forca - 0.2) / 0.18);
            float t = _Time.y;
            float fase = cor.g * 6.2832 + pe.x * 0.61 + pe.z * 0.47;
            // a onda que passa pelo campo (numa direção fixa: veja o cabeçalho) e o balanço de cada tufo; com o vento forte,
            // o tufo deita para o lado do vento e balança menos
            float onda = sin(t * 1.6 - dot(pe.xz, float2(0.92, 0.38)) * 0.35 + fase * 0.3);
            float balanco = (onda * 0.5 + sin(t * 2.3 + fase) * 0.3) * (1.0 - 0.45 * firme) + 0.35 + 0.35 * firme + _ZccVento.w * 0.3;
            float b = cor.r * forca * _Balanco * balanco * 0.6;
            p.xz += _ZccVento.xy * b;
            p.y -= abs(b) * 0.25;
            float tremor = cor.b * _Tremor * (0.5 + 0.5 * saturate(forca / 0.38));
            float q = t * 6.0 + pe.x * 3.1 + pe.z * 2.7;
            p += float3(sin(q), sin(q * 1.3 + 1.0) * 0.4, cos(q * 0.8)) * tremor;
            return Dobrar(p, pe);
        }

        // cada tufo pega uma coluna do atlas pelo pé dele
        float2 UV(float2 uv, float3 pe)
        {
            float colunas = max(_Colunas, 1.0);
            float coluna = floor(Hashes(pe.xz).x * colunas);
            return float2(frac(uv.x + coluna / colunas), uv.y);
        }

        // cada tufo num tom (pelo pé): uns mais amarelados, outros mais azulados, mais claros ou mais escuros
        half3 Tinta(float3 pe)
        {
            float2 h = Hashes(pe.xz + 7.0);
            half3 tom = lerp(half3(1.08, 1.0, 0.8), half3(0.88, 1.0, 1.08), h.x);
            tom *= 0.8 + 0.35 * h.y;
            return lerp(half3(1, 1, 1), tom, _Variacao);
        }

        half4 Cor(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
        }

        void Recortar(half alfa)
        {
            clip(alfa - _Cutoff);
        }
        ENDHLSL

        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
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
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half3 tinta : TEXCOORD4;
                half altura : TEXCOORD5;
                half desbota : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 pe = Pe();
                o.positionWS = Vento(input.positionOS.xyz, input.color, pe);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = normalize(lerp(TransformObjectToWorldNormal(input.normalOS), float3(0.0, 1.0, 0.0), _Cima));
                o.uv = UV(TRANSFORM_TEX(input.uv, _BaseMap), pe);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                o.tinta = Tinta(pe);
                o.altura = input.uv.y;
                o.desbota = _Desbota.y > 0.0 ? saturate((distance(pe.xz, GetCameraPositionWS().xz) - _Desbota.x) / max(_Desbota.y - _Desbota.x, 0.01)) : 0.0;
                return o;
            }

            half4 Frag(Varyings input, bool frente : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 cor = Cor(input.uv);
                Recortar(cor.a);

                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 n = normalize(input.normalWS);
                // a parte da normal que ainda é da folha vira para quem olha (os dois lados iluminam igual)
                if (!frente && _Cima < 1.0) n = normalize(lerp(-n, n, _Cima));

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = cor.rgb * _Cor.rgb * lerp(_CorDaBase.rgb, half3(1, 1, 1), saturate(input.altura * 1.6)) * input.tinta;
                // longe, a cor média do chão (o fim da grama some no chão de grama)
                surface.albedo = lerp(surface.albedo, _CorDoChao.rgb, input.desbota * 0.85);
                surface.alpha = 1.0;
                surface.smoothness = _Suavidade;
                surface.occlusion = 1.0;
                surface.normalTS = half3(0.0, 0.0, 1.0);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = viewDir;
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
                if (_Translucencia > 0.0)
                {
                    Light sol = GetMainLight(inputData.shadowCoord);
                    half atras = pow(saturate(dot(viewDir, -sol.direction)), 4.0);
                    color.rgb += surface.albedo * sol.color * sol.shadowAttenuation * sol.distanceAttenuation * atras * _Translucencia;
                }
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
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
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
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pe = Pe();
                float3 positionWS = Vento(input.positionOS.xyz, input.color, pe);
                float3 normalWS = float3(0.0, 1.0, 0.0);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                o.uv = UV(TRANSFORM_TEX(input.uv, _BaseMap), pe);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                Recortar(Cor(input.uv).a);
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
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pe = Pe();
                o.positionCS = TransformWorldToHClip(Vento(input.positionOS.xyz, input.color, pe));
                o.uv = UV(TRANSFORM_TEX(input.uv, _BaseMap), pe);
                return o;
            }

            half Frag(Varyings input) : SV_Target
            {
                Recortar(Cor(input.uv).a);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
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
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pe = Pe();
                o.positionCS = TransformWorldToHClip(Vento(input.positionOS.xyz, input.color, pe));
                o.normalWS = normalize(lerp(TransformObjectToWorldNormal(input.normalOS), float3(0.0, 1.0, 0.0), _Cima));
                o.uv = UV(TRANSFORM_TEX(input.uv, _BaseMap), pe);
                return o;
            }

            void Frag(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                Recortar(Cor(input.uv).a);
                float3 n = normalize(input.normalWS);
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
