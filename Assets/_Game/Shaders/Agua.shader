// A água dos lagos e dos rios (08/11/2026, a pedido: "uma água mais realista"; antes, a água opaca e parada da Parte M2).
// Transparente, desenhada depois do chão, com a profundidade da tela:
// - A FUNDURA vem da profundidade da cena (_CameraDepthTexture): o caminho da luz dentro da água decide quanto do fundo
//   aparece (a absorção por canal, como a água de verdade: o vermelho some primeiro, e a água fica verde-azulada) e a
//   espuma na beira, onde a água tem menos de uns 30 cm.
// - A REFRAÇÃO: com a cópia da tela (_CameraOpaqueTexture, ligada pelo Graficos na Alta e na Ultra), o fundo é lido
//   entortado pelas ondinhas; sem ela, a água mistura por alfa (a rasa transparente, a funda opaca).
// - As ONDINHAS são um normal map (T_Agua_Normal, feito por código em TexturasDaAgua), em duas escalas: nos lagos
//   arrastadas pelo vento do mundo (_ZccVento), nos rios pela correnteza, ao longo do UV da faixa (o G da cor do vértice
//   diz que a água corre).
// - O REFLEXO: o céu (a luz ambiente na direção refletida) pelo Fresnel e, no lago, a câmera espelhada (ReflexoDaAgua:
//   _ZccReflexoTex, na altura _ZccReflexoPlano) com as ondinhas entortando; o brilho do sol e das luzes (a lanterna) pela
//   PBR da URP, só o especular.
// - A NEBLINA de altura é feita aqui de novo (as mesmas globais do NeblinaDeAltura.shader): o passe de tela cheia dela
//   vem antes dos transparentes, e a água ficaria limpa no meio do chão enevoado.
// - De baixo da água (a câmera submersa), a superfície mostra o que está em cima, pela refração, meio escurecida.
Shader "ZombieCityCleaner/Agua"
{
    Properties
    {
        _CorRasa("Tinta da água rasa (sobre o fundo)", Color) = (0.5, 0.76, 0.74, 1)
        _CorFunda("Cor da água funda", Color) = (0.08, 0.24, 0.28, 1)
        _Absorcao("Absorção por metro (RGB)", Vector) = (0.55, 0.2, 0.15, 0)
        _Turvo("Turvação (0 clara, 1 lamacenta)", Range(0, 1)) = 0.35
        [NoScaleOffset] _NormalTex("Ondinhas (normal)", 2D) = "bump" {}
        _OndasEscala("Metros por repetição das ondinhas", Float) = 6
        _OndasForca("Força das ondinhas", Range(0, 1)) = 0.35
        _OndasVelocidade("Velocidade das ondinhas (m/s)", Float) = 0.15
        _Fluxo("Correnteza dos rios (m/s)", Float) = 0.9
        _Refracao("Refração", Range(0, 0.1)) = 0.025
        _Fresnel("Reflexo de frente (F0)", Range(0, 0.2)) = 0.04
        _Reflexo("Reflexo do céu", Range(0, 1.5)) = 1
        _ReflexoPlanar("Reflexo do cenário no lago", Range(0, 1)) = 0.85
        _Suavidade("Brilho", Range(0.5, 1)) = 0.93
        _Espuma("Espuma na beira (m de fundura)", Range(0, 1)) = 0.28
        _CorEspuma("Cor da espuma", Color) = (0.75, 0.78, 0.74, 1)
        _TintaDoReflexo("Tinta do reflexo (a água tinge o que reflete)", Color) = (0.64, 0.9, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CorRasa;
                half4 _CorFunda;
                float4 _Absorcao;
                half _Turvo;
                float _OndasEscala;
                half _OndasForca;
                float _OndasVelocidade;
                float _Fluxo;
                half _Refracao;
                half _Fresnel;
                half _Reflexo;
                half _ReflexoPlanar;
                half _Suavidade;
                half _Espuma;
                half4 _CorEspuma;
                half4 _TintaDoReflexo;
            CBUFFER_END

            TEXTURE2D(_NormalTex);      SAMPLER(sampler_NormalTex);
            TEXTURE2D(_ZccReflexoTex);  SAMPLER(sampler_ZccReflexoTex);

            // as globais: a refração ligada (o Graficos), o plano do reflexo (o ReflexoDaAgua; abaixo de -1e8, nenhum),
            // o vento do mundo (o FlorestaDoMundo) e a neblina de altura (o AmbienteDoDia)
            float _ZccAguaRefracao;
            float _ZccReflexoPlano;
            float4 _ZccVento;
            float4 _NeblinaCor;
            float4 _NeblinaParams;
            float4 _NeblinaVento;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half rio : TEXCOORD4;
            };

            // ruído de valor 2D suave (sem textura): a espuma
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

            // a neblina de altura no ponto (a mesma conta do NeblinaDeAltura.shader, ao longo do raio até a água)
            float HashN(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float RuidoN(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(HashN(i), HashN(i + float2(1, 0)), f.x), lerp(HashN(i + float2(0, 1)), HashN(i + float2(1, 1)), f.x), f.y);
            }

            half3 NeblinaDeAltura(half3 cor, float3 ponto)
            {
                float3 camera = _WorldSpaceCameraPos;
                float3 raio = ponto - camera;
                float distancia = length(raio);
                float3 direcao = raio / max(distancia, 1e-4);
                distancia = min(distancia, _NeblinaParams.w);
                float k = _NeblinaParams.z;
                float noOlho = _NeblinaParams.x * exp(-k * (max(camera.y, _NeblinaParams.y) - _NeblinaParams.y));
                float subida = k * direcao.y;
                float integral = abs(subida) > 1e-4 ? noOlho * (1.0 - exp(-subida * distancia)) / subida : noOlho * distancia;
                float2 amostra = (camera.xz + direcao.xz * min(distancia, 30.0) + _NeblinaVento.xy) * _NeblinaVento.z;
                float n = RuidoN(amostra) * 0.65 + RuidoN(amostra * 2.7 + 13.1) * 0.35;
                integral *= lerp(1.0 - _NeblinaVento.w, 1.0 + _NeblinaVento.w, n);
                float quanto = 1.0 - exp(-max(integral, 0.0));
                return lerp(cor, _NeblinaCor.rgb, saturate(quanto));
            }

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                o.rio = input.color.g;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 posWS = input.positionWS;
                float3 cam = _WorldSpaceCameraPos;
                float3 V = normalize(cam - posWS);
                float dist = length(cam - posWS);
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-4);
                bool porBaixo = cam.y < posWS.y;
                bool refracao = _ZccAguaRefracao > 0.5;
                float t = _Time.y;

                // as ondinhas: nos rios pelo UV (arrastado rio abaixo), nos lagos pelo mundo (arrastadas pelo vento)
                half rio = input.rio;
                // em direções fixas (a média do vento do jogo e outra a uns 65° dela): com a direção do vento, que gira
                // devagar, o vento × tempo crescia com o tempo de jogo, e as ondinhas escorregavam de lado cada vez mais rápido
                // (10/11/2026)
                const float2 D1 = float2(0.92, 0.38), D2 = float2(0.05, 0.99);
                float2 pLago = posWS.xz + D1 * (t * _OndasVelocidade);
                float2 pLago2 = posWS.xz - D2 * (t * _OndasVelocidade * 0.6) + 37.0;
                float2 pRio = float2(input.uv.x, input.uv.y - t * _Fluxo);
                float2 pRio2 = float2(input.uv.x * 1.37 + 11.0, input.uv.y * 0.9 - t * _Fluxo * 0.55);
                float2 p1 = lerp(pLago, pRio, rio);
                float2 p2 = lerp(pLago2, pRio2, rio);
                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, p1 / _OndasEscala));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, p2 / (_OndasEscala * 2.7)));
                // de longe, as ondinhas somem (senão viram chuvisco)
                half forca = _OndasForca * (1.0 - 0.7 * saturate(dist / 260.0));
                float2 nxy = (n1.xy + n2.xy * 0.6) * forca;
                float3 N = normalize(float3(nxy.x, 1.0, nxy.y));

                // a fundura pela profundidade da cena: o caminho da luz dentro da água, ao longo do raio
                float zAgua = input.screenPos.w;
                float zCena = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float espessura = max(0.0, zCena - zAgua);
                float2 uvR = screenUV + N.xz * _Refracao * saturate(espessura * 0.6 + 0.25);
                float zR = LinearEyeDepth(SampleSceneDepth(uvR), _ZBufferParams);
                if (zR < zAgua) { uvR = screenUV; zR = zCena; }
                float espR = max(0.0, zR - zAgua);
                float caminho = espR * dist / max(zAgua, 0.01);

                // a luz na água: o ambiente e o sol (com a sombra)
                float4 shadowCoord = TransformWorldToShadowCoord(posWS);
                Light luz = GetMainLight(shadowCoord);
                half3 ambiente = SampleSH(half3(0, 1, 0));
                half3 luzAgua = ambiente + luz.color * luz.shadowAttenuation * saturate(luz.direction.y) * 0.9;
                half3 corFunda = _CorFunda.rgb * luzAgua;
                float3 absor = _Absorcao.rgb * lerp(0.45, 2.2, _Turvo);
                half3 trans = exp(-absor * caminho);

                half3 cor;
                half alfa;
                if (porBaixo)
                {
                    // submerso: o que está em cima da água, pela refração, um pouco escurecido
                    N = -N;
                    if (refracao) { cor = lerp(SampleSceneColor(uvR), corFunda, 0.35); alfa = 1.0; }
                    else { cor = corFunda; alfa = 0.45; }
                }
                else
                {
                    if (refracao)
                    {
                        half3 fundo = SampleSceneColor(uvR) * _CorRasa.rgb * 1.6;
                        cor = lerp(corFunda, fundo, trans);
                        alfa = 1.0;
                    }
                    else
                    {
                        cor = corFunda;
                        alfa = 1.0 - dot(trans, half3(0.333, 0.333, 0.334));
                    }

                    // a espuma na beira (a água rasa), recortada pelo ruído, mexendo devagar
                    float ruidoE = Ruido(posWS.xz * 1.3 + float2(t * 0.2, -t * 0.15)) * 0.65 + Ruido(posWS.xz * 3.1 - t * 0.3) * 0.35;
                    float beira = saturate(1.0 - espR / max(_Espuma, 0.01));
                    half espuma = smoothstep(0.42, 0.72, beira * (0.45 + 0.8 * ruidoE));
                    cor = lerp(cor, _CorEspuma.rgb * luzAgua, espuma * 0.65);
                    alfa = max(alfa, espuma * 0.7);

                    // o reflexo: o céu (o ambiente na direção refletida) e, no lago, o cenário espelhado
                    float3 R = reflect(-V, N);
                    R.y = abs(R.y) + 0.02;
                    half3 reflexo = SampleSH(R) * _Reflexo;
                    if (_ZccReflexoPlano > -1e8 && abs(posWS.y - _ZccReflexoPlano) < 0.6)
                    {
                        float2 uvP = screenUV + N.xz * 0.035;
                        half4 pl = SAMPLE_TEXTURE2D(_ZccReflexoTex, sampler_ZccReflexoTex, uvP);
                        reflexo = lerp(reflexo, pl.rgb, pl.a * _ReflexoPlanar);
                    }
                    half F = _Fresnel + (1.0 - _Fresnel) * pow(1.0 - saturate(dot(N, V)), 5.0);
                    F = lerp(F, 0.0, espuma);
                    // a água tinge o reflexo (10/11/2026: o céu nublado, cinza, deixava o lago morto de longe)
                    reflexo *= _TintaDoReflexo.rgb;
                    cor = lerp(cor, reflexo, F);
                    alfa = lerp(alfa, 1.0, F);
                }

                // o brilho do sol e das luzes (a PBR da URP só com o especular: sem cor própria nem o reflexo do ambiente)
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = 0;
                surface.alpha = 1.0;
                surface.smoothness = _Suavidade;
                surface.occlusion = 0;
                surface.normalTS = half3(0, 0, 1);
                InputData inputData = (InputData)0;
                inputData.positionWS = posWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = N;
                inputData.viewDirectionWS = V;
                inputData.shadowCoord = shadowCoord;
                inputData.fogCoord = 0;
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.bakedGI = 0;
                inputData.shadowMask = half4(1, 1, 1, 1);
                half3 brilho = UniversalFragmentPBR(inputData, surface).rgb;
                cor += brilho;

                cor = MixFog(cor, InitializeInputDataFog(float4(posWS, 1.0), input.fogFactor));
                cor = NeblinaDeAltura(cor, posWS);
                return half4(cor, alfa);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
