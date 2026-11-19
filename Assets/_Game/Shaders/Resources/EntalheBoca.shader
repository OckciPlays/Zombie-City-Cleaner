// A boca do entalhe do machado (18/11/2026, World/CorteNaMadeira.cs): a abertura do corte, colada por fora da casca. Não
// desenha nada (ColorMask 0): só marca no estêncil (os bits 5 a 7, o número do corte em _Ref) os pixels onde a boca aparece
// (o teste de profundidade normal: quem está na frente da árvore tapa). Depois, o EntalheDentro desenha a madeira de
// dentro do corte só nesses pixels, sem o teste de profundidade: a casca da árvore fica "aberta" ali, sem mexer no shader
// das árvores. Na fila depois das árvores (as folhas são AlphaTest).
Shader "ZombieCityCleaner/EntalheBoca"
{
    Properties
    {
        _Ref ("Número do corte no estêncil", Float) = 32
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest+20" }
        Pass
        {
            Name "Boca"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            Cull Off
            Stencil
            {
                Ref [_Ref]
                ReadMask 224
                WriteMask 224
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
