// O retrato do jogador na tela do inventário (Player/RetratoDoJogador): a cor da paleta com uma luz fixa, de cima e
// da frente, e uma luz fraca de contorno do outro lado. Não usa as luzes da cena, a neblina nem o ambiente: o retrato
// fica igual de dia, de noite e no bunker.
Shader "ZombieCityCleaner/Retrato"
{
    Properties
    {
        [MainTexture] _BaseMap ("Paleta", 2D) = "white" {}
        _Ambiente ("Ambiente", Range(0, 1)) = 0.42
        _Luz ("Luz", Range(0, 2)) = 0.72
        _DirecaoDaLuz ("Direção da luz (mundo)", Vector) = (0.45, 0.8, 0.55, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float _Ambiente;
            float _Luz;
            float4 _DirecaoDaLuz;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Retrato"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertice
            #pragma fragment Fragmento

            struct Entrada
            {
                float4 posicao : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Saida
            {
                float4 posicao : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            Saida Vertice(Entrada v)
            {
                Saida o;
                o.posicao = TransformObjectToHClip(v.posicao.xyz);
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 Fragmento(Saida i) : SV_Target
            {
                half3 cor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                float3 n = normalize(i.normal);
                float3 l = normalize(_DirecaoDaLuz.xyz);
                float direta = saturate(dot(n, l));
                float contorno = saturate(dot(n, normalize(float3(-l.x, 0.25, -l.z)))) * 0.2;
                return half4(cor * (_Ambiente + _Luz * direta + contorno), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vertice
            #pragma fragment Fragmento

            float4 Vertice(float4 posicao : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(posicao.xyz);
            }

            half Fragmento() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
