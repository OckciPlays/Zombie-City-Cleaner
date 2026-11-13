// As cores do PS1 da pixelação (Configurações > Gráficos > Pixelação, as duas "PS1"; a pedido em 07/11/2026): o passe de
// tela cheia "Pixelacao" do PC_Renderer (Editor/MontagemDaPixelacao), antes do pós-processamento (depois dele, o URP passava a
// ampliar a imagem suavizando, e a pixelação virava borrão), na resolução baixa em que o 3D é desenhado (o renderScale do URP,
// que depois amplia sem suavizar): cada canal reduzido a 5 bits (32 tons, como o PS1) no espaço sRGB, com o pontilhado de
// Bayer 4 × 4 em cada pixel da resolução baixa. Acima de 1 (as lâmpadas, o que acende o bloom) não corta: os degraus seguem.
// Desligado (o recurso inativo) nas outras opções.
Shader "ZombieCityCleaner/Pixelacao"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "Pixelacao"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            static const float Bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 cor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);
                uint2 p = (uint2)input.positionCS.xy & 3;
                float d = Bayer[p.y * 4 + p.x] / 16.0 - 0.5;
                float3 s = LinearToSRGB(max(cor.rgb, 0.0));
                s = floor(s * 31.0 + d + 0.5) / 31.0;
                return half4(SRGBToLinear(max(s, 0.0)), cor.a);
            }
            ENDHLSL
        }
    }
}
