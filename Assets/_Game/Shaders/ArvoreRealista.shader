// As árvores realistas da floresta nova (o Plano_Floresta.md, "Os pinheiros realistas", 09/11/2026): o tronco com a
// textura da casca e o normal map, e a copa em cartas (quads com a foto dos galhos, recortadas pelo alfa, dos dois lados).
// - O vento é o do Vegetacao.shader: a COR DO VÉRTICE (R = o balanço pela altura, G = a fase, B = o tremor das pontas)
//   e o global _ZccVento (xy a direção, z a força em metros, w a rajada).
// - As cartas recortam pelo alfa (_ALPHATEST_ON) e desenham os dois lados (_Cull = Off), com a normal virada para quem
//   olha; a normal da malha é "esférica" (sai do eixo da árvore, para a copa sombrear como um volume) e o normal map da
//   foto entra por cima, com a força _BumpForca.
// - _Translucencia: a luz do sol atravessando a copa quando ela fica entre o sol e a câmera (as folhas acesas por trás).
// - O cross-fade dos LODs (LOD_FADE_CROSSFADE, o pontilhado do URP): a troca entre os LODs não "pula". A passada de
//   sombra NÃO pontilha: na faixa de troca os dois LODs fazem a sombra inteira (a união das duas formas, quase iguais);
//   pontilhada, a sombra mudava na frente do jogador conforme ele andava (o fade é pela distância, com o GRD).
// - As instâncias de sempre e as do GPU Resident Drawer (DOTS_INSTANCING_ON): a floresta é muita árvore igual.
// - Um material só (_UM_MATERIAL, 09/11/2026: o pinheiro leve): o tronco e as cartas dos galhos na mesma submalha e no
//   mesmo material, para cada árvore ser um lote por passada em vez de dois (os draw calls das árvores pela metade). A
//   cor do vértice A diz o que é cada parte (0 a casca, 1 os galhos): a casca lê a _CascaMap (a textura que se repete)
//   e as cartas o atlas (_BaseMap), cada uma com a sua normal, o seu brilho e a sua variação de tom; só os galhos têm a
//   luz atravessando. O material é o dos galhos (recortado e dos dois lados).
// Iluminação: a PBR da URP (UniversalFragmentPBR), como o shader da vegetação: o sol e as sombras, as luzes (Forward+), o
// SSAO e a neblina. As passadas de sombra e de profundidade balançam e recortam igual.
Shader "ZombieCityCleaner/ArvoreRealista"
{
    Properties
    {
        _BaseMap("Cor (A: o alfa)", 2D) = "white" {}
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _BumpForca("Força da normal", Range(0, 2)) = 1
        _Cor("Tinta", Color) = (1, 1, 1, 1)
        _Cutoff("Corte do alfa", Range(0, 1)) = 0.4
        _Suavidade("Brilho", Range(0, 1)) = 0.15
        _Translucencia("Luz atravessando (as folhas)", Range(0, 2)) = 0
        _Balanco("Balanço (vezes o vento)", Range(0, 3)) = 1
        _Tremor("Tremor das pontas (m)", Range(0, 0.3)) = 0.05
        _Variacao("Variação de tom entre as árvores", Range(0, 1)) = 0.4
        _Cartaz("Cartaz que vira para a câmera", Float) = 0
        [Toggle(_UM_MATERIAL)] _UmMaterial("Tronco e galhos num material só (a casca onde a cor do vértice A = 0)", Float) = 0
        [NoScaleOffset] _CascaMap("Casca (no material só)", 2D) = "white" {}
        [Normal][NoScaleOffset] _CascaNormal("Normal da casca", 2D) = "bump" {}
        _CascaBump("Força da normal da casca", Range(0, 2)) = 1
        _CascaSuavidade("Brilho da casca", Range(0, 1)) = 0.1
        _CascaVariacao("Variação de tom da casca", Range(0, 1)) = 0.3
        [Toggle(_ALPHATEST_ON)] _Recorte("Recortar pelo alfa", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Lados", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half _BumpForca;
            half4 _Cor;
            half _Cutoff;
            half _Suavidade;
            half _Translucencia;
            half _Balanco;
            half _Tremor;
            half _Variacao;
            half _Cartaz;
            half _CascaBump;
            half _CascaSuavidade;
            half _CascaVariacao;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_CascaMap);
        SAMPLER(sampler_CascaMap);
        TEXTURE2D(_CascaNormal);
        SAMPLER(sampler_CascaNormal);

        float4 _ZccVento;

        // o vértice no mundo, com o vento: o balanço da árvore inteira (pela altura, a fase pelo pé) e o tremor das pontas
        float3 Vento(float3 positionOS, half4 cor)
        {
            float4x4 M = GetObjectToWorldMatrix();
            float3 pe = float3(M._m03, M._m13, M._m23);
            float3 p;
            if (_Cartaz > 0.5)
            {
                // o cartaz: a carta vira para a câmera em volta do eixo da árvore (a escala do objeto vale, o giro não)
                float escala = length(float3(M._m01, M._m11, M._m21));
                float2 d = GetCameraPositionWS().xz - pe.xz;
                d /= max(length(d), 0.001);
                float3 direita = float3(-d.y, 0.0, d.x);
                p = pe + direita * (positionOS.x * escala) + float3(0.0, positionOS.y * escala, 0.0);
            }
            else p = mul(M, float4(positionOS, 1.0)).xyz;
            float forca = _ZccVento.z;
            if (forca <= 0.0) return p;
            float t = _Time.y;
            float fase = cor.g * 6.2832 + pe.x * 0.37 + pe.z * 0.53;
            float balanco = sin(t * 0.9 + fase) * 0.6 + sin(t * 2.1 + fase * 1.7) * 0.25 + 0.35 + _ZccVento.w * 0.4;
            float b = cor.r * forca * _Balanco * balanco;
            p.xz += _ZccVento.xy * b;
            p.y -= b * b * 0.5;
            // o tremor: as pontas das cartas, cada uma no seu tempo
            float tremor = cor.b * _Tremor * (0.5 + 0.5 * forca) ;
            float q = t * 7.0 + p.x * 2.1 + p.z * 1.7 + p.y * 1.3;
            p += float3(sin(q), sin(q * 1.3 + 1.0) * 0.5, cos(q * 0.8)) * tremor * (0.6 + 0.4 * _ZccVento.w);
            return p;
        }

        half4 Cor(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
        }

        // cada árvore num tom (pelo pé dela): umas mais amareladas, outras mais azuladas, mais claras ou mais escuras
        half3 Tinta(half variacao)
        {
            float4x4 M = GetObjectToWorldMatrix();
            float2 pe = float2(M._m03, M._m23);
            float h1 = frac(sin(dot(pe, float2(12.9898, 78.233))) * 43758.5453);
            float h2 = frac(sin(dot(pe, float2(39.3467, 11.135))) * 24634.6345);
            half3 tom = lerp(half3(1.04, 1.0, 0.86), half3(0.9, 1.0, 1.1), h1);
            tom *= 0.86 + 0.26 * h2;
            return lerp(half3(1, 1, 1), tom, variacao);
        }

        // no material só (_UM_MATERIAL): 0 a casca do tronco, 1 as cartas dos galhos (a cor do vértice A); sem ele, 1
        half Camada(half4 cor)
        {
        #if defined(_UM_MATERIAL)
            return cor.a;
        #else
            return 1.0h;
        #endif
        }

        half VariacaoDa(half camada)
        {
        #if defined(_UM_MATERIAL)
            return lerp(_CascaVariacao, _Variacao, camada);
        #else
            return _Variacao;
        #endif
        }

        void Recortar(half alfa)
        {
        #if defined(_ALPHATEST_ON)
            clip(alfa - _Cutoff);
        #endif
        }
        ENDHLSL

        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _UM_MATERIAL
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
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
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
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half3 tinta : TEXCOORD5;
                half camada : TEXCOORD6;
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
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.normalWS = n.normalWS;
                o.tangentWS = half4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                o.camada = Camada(input.color);
                o.tinta = Tinta(VariacaoDa(o.camada));
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif
                half camada = 1.0h;
                half suavidade = _Suavidade;
            #if defined(_UM_MATERIAL)
                // o material só: a casca (camada 0, a textura que se repete) ou as cartas (camada 1, o atlas). A camada é a
                // mesma no triângulo inteiro; as derivadas saem antes do if (o mipmap certo nos dois lados)
                camada = input.camada;
                float2 dx = ddx(input.uv), dy = ddy(input.uv);
                half4 cor;
                half4 amostraN;
                half forcaN;
                UNITY_BRANCH
                if (camada > 0.5h)
                {
                    cor = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, input.uv, dx, dy);
                    amostraN = SAMPLE_TEXTURE2D_GRAD(_BumpMap, sampler_BumpMap, input.uv, dx, dy);
                    forcaN = _BumpForca;
                }
                else
                {
                    cor = half4(SAMPLE_TEXTURE2D_GRAD(_CascaMap, sampler_CascaMap, input.uv, dx, dy).rgb, 1.0h);
                    amostraN = SAMPLE_TEXTURE2D_GRAD(_CascaNormal, sampler_CascaNormal, input.uv, dx, dy);
                    forcaN = _CascaBump;
                }
                Recortar(cor.a);
                suavidade = lerp(_CascaSuavidade, _Suavidade, camada);
            #else
                half4 cor = Cor(input.uv);
                Recortar(cor.a);
                half4 amostraN = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half forcaN = _BumpForca;
            #endif

                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // a normal da malha é a mesma dos dois lados da carta (a copa é um volume, não uma folha)
                half3 n = normalize(input.normalWS);
                half3 t = normalize(input.tangentWS.xyz);
                half3 b = cross(n, t) * input.tangentWS.w;
                half3 nTS = UnpackNormalScale(amostraN, forcaN);
                n = normalize(nTS.x * t + nTS.y * b + nTS.z * n);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = cor.rgb * _Cor.rgb * input.tinta;
                surface.alpha = 1.0;
                surface.smoothness = suavidade;
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
                // a luz atravessando a copa, com o sol atrás dela
                if (_Translucencia > 0.0)
                {
                    Light sol = GetMainLight(inputData.shadowCoord);
                    half atras = pow(saturate(dot(viewDir, -sol.direction)), 5.0);
                    color.rgb += surface.albedo * sol.color * sol.shadowAttenuation * sol.distanceAttenuation * atras * _Translucencia * camada;
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _UM_MATERIAL
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
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
                half camada : TEXCOORD1;
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
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.camada = Camada(input.color);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                Recortar(lerp(1.0h, Cor(input.uv).a, input.camada));
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _UM_MATERIAL
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

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
                half camada : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformWorldToHClip(Vento(input.positionOS.xyz, input.color));
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.camada = Camada(input.color);
                return o;
            }

            half Frag(Varyings input) : SV_Target
            {
            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif
                Recortar(lerp(1.0h, Cor(input.uv).a, input.camada));
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _UM_MATERIAL
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

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
                half camada : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformWorldToHClip(Vento(input.positionOS.xyz, input.color));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.camada = Camada(input.color);
                return o;
            }

            void Frag(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif
                Recortar(lerp(1.0h, Cor(input.uv).a, input.camada));
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
