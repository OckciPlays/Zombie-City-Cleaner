// O funil do tornado (World/ClimaNaTela; a pedido em 02/11/2026): umas cascas de cone, uma dentro da outra, girando em
// velocidades diferentes. Leve: sem textura, um ruído 3D enrolado no cone (emenda sem costura) faz as faixas de poeira e
// nuvem subindo em espiral.
// - O UV: x dá a volta no cone, y sobe (0 no chão, 1 no alto, na nuvem). O alfa do vértice esfuma a base e o topo.
// - _Desce: o funil descendo da nuvem (0 nada, 1 inteiro até o chão) e se desfazendo.
// - A neblina só em parte (_Neblina): de longe, ele aparece como uma sombra escura atrás da chuva.
// - Ele dança: o meio entorta devagar e o pé anda para os lados.
// - O clarão dos raios (_ZccClarao, global) acende o funil por um instante.
Shader "ZombieCityCleaner/ClimaFunil"
{
    Properties
    {
        _Cor ("Cor", Color) = (0.2, 0.19, 0.18, 0.85)
        _Giro ("Giro (voltas por segundo no pé)", Float) = 0.35
        _Subida ("Subida das faixas", Float) = 0.6
        _Desce ("Descida (0 a 1)", Range(0, 1)) = 1
        _Danca ("Dança (m)", Float) = 4
        _Neblina ("Neblina", Range(0, 1)) = 0.45
        _Semente ("Semente", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Funil"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertice
            #pragma fragment Fragmento
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Cor;
                float _Giro;
                float _Subida;
                half _Desce;
                float _Danca;
                half _Neblina;
                float _Semente;
            CBUFFER_END

            float _ZccClarao;

            struct Entrada
            {
                float4 posicao : POSITION;
                float3 normal : NORMAL;
                half4 cor : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Saida
            {
                float4 posicao : SV_POSITION;
                half4 cor : COLOR;
                float2 uv : TEXCOORD0;
                float3 mundo : TEXCOORD1;
                float3 normal : TEXCOORD2;
                half neblina : TEXCOORD3;
            };

            float Hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Ruido3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash3(i + float3(0, 0, 0)), Hash3(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            Saida Vertice(Entrada v)
            {
                Saida o;
                float h = v.uv.y;
                float t = _Time.y + _Semente * 7.0;
                float3 mundo = TransformObjectToWorld(v.posicao.xyz);
                // o pé anda para os lados, o meio entorta devagar (o topo fica preso na nuvem)
                float pe = 1.0 - h;
                float2 danca = float2(sin(t * 0.37 + h * 2.1), cos(t * 0.29 + h * 1.7)) * _Danca * (0.4 + 2.2 * pe * h)
                             + float2(sin(t * 1.3 + h * 9.0), cos(t * 1.1 + h * 7.0)) * 0.6 * pe;
                mundo.xz += danca;
                o.mundo = mundo;
                o.posicao = TransformWorldToHClip(mundo);
                // a câmera do jogador só desenha até 150 m: o funil (de 150 a 650 m) era cortado olhando de frente e
                // aparecia olhando de lado (de lado, a profundidade é menor). O que passa do fundo fica preso nele, atrás
                // de tudo o que está mais perto.
            #if UNITY_REVERSED_Z
                o.posicao.z = max(o.posicao.z, o.posicao.w * 1e-6);
            #else
                o.posicao.z = min(o.posicao.z, o.posicao.w * (1.0 - 1e-6));
            #endif
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.cor = v.cor;
                o.uv = v.uv;
                // a neblina pela distância de verdade (pela profundidade, ela mudava com o lado para onde se olha)
                o.neblina = ComputeFogFactorZ0ToFar(length(mundo - GetCameraPositionWS()));
                return o;
            }

            half4 Fragmento(Saida i) : SV_Target
            {
                float t = _Time.y + _Semente * 7.0;
                float h = i.uv.y;
                // gira mais rápido embaixo (o funil fino) e as faixas sobem em espiral
                float angulo = i.uv.x * 6.2831853 + t * _Giro * 6.2831853 * (1.6 - h);
                float3 p = float3(cos(angulo) * 2.2, sin(angulo) * 2.2, h * 9.0 - t * _Subida + angulo * 0.25);
                float n = Ruido3(p + _Semente) * 0.6 + Ruido3(p * 2.3 + 11.0) * 0.3 + Ruido3(p * 5.1 - 7.0) * 0.1;
                // a borda do cone, de lado, fica rala (senão ele parece um cano)
                float3 olhar = normalize(GetWorldSpaceViewDir(i.mundo));
                half lado = smoothstep(0.0, 0.85, abs(dot(normalize(i.normal), olhar)));
                // descendo da nuvem: o pé só aparece com a descida
                half desce = saturate((h - (1.0 - _Desce) * 0.95) * 10.0) * saturate(_Desce * 3.0);
                half a = _Cor.a * i.cor.a * saturate(-0.05 + n * 1.45) * lado * desce;
                if (a < 0.003) discard;
                half3 luz = max(half3(0, 0, 0), SampleSH(half3(0, 1, 0))) * 1.3h + GetMainLight().color * 0.15h + _ZccClarao * half3(1.3, 1.4, 1.6);
                half3 rgb = _Cor.rgb * (0.55 + 0.9 * n) * luz;
                rgb = lerp(rgb, MixFog(rgb, i.neblina), _Neblina);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
