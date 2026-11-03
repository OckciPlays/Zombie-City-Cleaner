// As partículas e as linhas do clima (World/ClimaNaTela; a pedido em 02/11/2026): as gotas da chuva, os respingos, a
// poeira e os destroços do tornado, as faíscas e o risco do raio. Sem textura: a forma sai da conta (o ponto macio, que
// esticado vira o risco da gota; o anel do respingo; a linha do raio, com o miolo forte).
// - A luz: o ambiente, um pouco do sol e as luzes de perto (a lanterna, os postes): a chuva brilha no facho da lanterna.
// - O clarão do raio (_ZccClarao, global) acende tudo por um instante.
// - A neblina pela metade ou inteira (_Neblina); nas que somam luz (o raio, as faíscas), ela apaga para o preto.
// Fica numa pasta Resources para ir no build (o ClimaNaTela cria os materiais na hora).
Shader "ZombieCityCleaner/ClimaParticula"
{
    Properties
    {
        [HDR] _Cor ("Cor", Color) = (1, 1, 1, 1)
        [Enum(Ponto,0,Liso,1,Anel,2,Linha,3)] _Forma ("Forma", Float) = 0
        _Luz ("Luz da cena (0: a cor pura)", Range(0, 1)) = 1
        _Neblina ("Neblina", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Origem ("Mistura (origem)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _Destino ("Mistura (destino)", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Faces", Float) = 0
        [Toggle] _Teto ("Some debaixo dos tetos e no carro (a chuva)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Clima"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_Origem] [_Destino]
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertice
            #pragma fragment Fragmento
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Cor;
                float _Forma;
                half _Luz;
                half _Neblina;
                float _Origem;
                float _Destino;
                float _Cull;
                float _Teto;
            CBUFFER_END

            float _ZccClarao;
            // os tetos em volta da câmera (World/ClimaNaTela: a altura do que cobre cada célula de 0,75 m, achada por raios de
            // cima; xy: o canto, z: 1/tamanho, w: ligado) e a caixa do carro em que se está (do mundo para dentro dela; meio
            // tamanho em xyz, w: ligado). A gota e o respingo debaixo de um teto ou dentro do carro somem.
            TEXTURE2D(_ZccTeto);
            SAMPLER(sampler_point_clamp);
            float4 _ZccTetoArea;
            float4x4 _ZccCarro;
            float4 _ZccCarroMeio;

            bool Coberto(float3 p)
            {
                bool coberto = false;
                if (_ZccTetoArea.w > 0.5)
                {
                    float2 uv = (p.xz - _ZccTetoArea.xy) * _ZccTetoArea.z;
                    float teto = SAMPLE_TEXTURE2D_LOD(_ZccTeto, sampler_point_clamp, saturate(uv), 0).r;
                    coberto = all(uv > 0.0) && all(uv < 1.0) && p.y < teto - 0.25;
                }
                if (_ZccCarroMeio.w > 0.5)
                {
                    float3 l = mul(_ZccCarro, float4(p, 1.0)).xyz;
                    coberto = coberto || all(abs(l) < _ZccCarroMeio.xyz);
                }
                return coberto;
            }

            struct Entrada
            {
                float4 posicao : POSITION;
                half4 cor : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Saida
            {
                float4 posicao : SV_POSITION;
                half4 cor : COLOR;
                float2 uv : TEXCOORD0;
                float3 mundo : TEXCOORD1;
                half neblina : TEXCOORD2;
            };

            Saida Vertice(Entrada v)
            {
                Saida o;
                o.mundo = TransformObjectToWorld(v.posicao.xyz);
                o.posicao = TransformWorldToHClip(o.mundo);
                // a câmera só desenha até 150 m: o que passa disso (o raio longe) fica preso no fundo, atrás de tudo
            #if UNITY_REVERSED_Z
                o.posicao.z = max(o.posicao.z, o.posicao.w * 1e-6);
            #else
                o.posicao.z = min(o.posicao.z, o.posicao.w * (1.0 - 1e-6));
            #endif
                o.cor = v.cor;
                if (_Teto > 0.5 && Coberto(o.mundo)) o.cor.a = 0;
                o.uv = v.uv;
                // a neblina pela distância de verdade (pela profundidade, ela mudava com o lado para onde se olha)
                o.neblina = ComputeFogFactorZ0ToFar(length(o.mundo - GetCameraPositionWS()));
                return o;
            }

            half3 LuzDaCena(float3 mundo, float4 posicao)
            {
                half3 luz = max(half3(0, 0, 0), SampleSH(half3(0, 1, 0))) * 2.0h;
                Light sol = GetMainLight();
                luz += sol.color * 0.45h;
            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = mundo;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(posicao);
                uint total = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(total)
                    Light l = GetAdditionalLight(lightIndex, mundo, half4(1, 1, 1, 1));
                    luz += l.color * (l.distanceAttenuation * 1.2h);
                LIGHT_LOOP_END
            #endif
                return luz + _ZccClarao * half3(1.6, 1.7, 2.0);
            }

            half4 Fragmento(Saida i) : SV_Target
            {
                float2 c = i.uv * 2.0 - 1.0;
                float r = length(c);
                half a;
                if (_Forma < 0.5) { a = saturate(1.0 - r); a *= a; }
                else if (_Forma < 1.5) a = 1.0;
                else if (_Forma < 2.5) { float d = (r - 0.72) * 6.0; a = exp(-d * d) * step(r, 1.0); }
                else { float d = saturate(1.0 - abs(c.y)); a = d * d * d + 0.35 * d; }
                half4 cor = _Cor * i.cor;
                cor.a *= a;
                if (cor.a < 0.002) discard;
                half3 rgb = cor.rgb * lerp(half3(1, 1, 1), LuzDaCena(i.mundo, i.posicao), _Luz);
                // somando luz (o raio): a neblina apaga para o preto; misturando, para a cor da neblina
                bool soma = _Destino < 1.5 && _Destino > 0.5;
                half3 comNeblina = soma ? MixFogColor(rgb, half3(0, 0, 0), i.neblina) : MixFog(rgb, i.neblina);
                rgb = lerp(rgb, comNeblina, _Neblina);
                return half4(rgb, cor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
