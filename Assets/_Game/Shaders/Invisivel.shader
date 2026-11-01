// O que não se desenha (31/10/2026, a parte 5 do Plano_Personagem.md da pasta de arte): a pele dos braços de primeira
// pessoa, no lugar da qual entram os braços do corpo vestido (Player/RoupasNosBracos). A malha dos braços de alguns itens
// (a lata, a garrafa) não deixa tirar a submalha da pele, então ela ganha este material: o vértice cai fora da tela e
// nada é desenhado (nem a sombra, nem a profundidade). Vai no jogo pelo ItensDasRoupas (Resources).
Shader "ZombieCityCleaner/Invisivel"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Nada"
            Tags { "LightMode" = "UniversalForward" }
            ColorMask 0
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Entrada { float4 posicao : POSITION; };

            // fora do volume da tela (x, y e z além de w): o triângulo é descartado antes de chegar aos pixels
            float4 Vert(Entrada i) : SV_POSITION { return float4(2.0, 2.0, 2.0, 1.0); }

            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
