// Neblina de altura em tela cheia (Full Screen Pass Renderer Feature, antes do pós-processamento).
// Mais densa perto do chão e rala no alto, com um ruído arrastado pelo vento para ela parecer viva.
// Os valores vêm do AmbienteDoDia (globais _NeblinaCor, _NeblinaParams e _NeblinaVento).
Shader "ZombieCityCleaner/NeblinaDeAltura"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "NeblinaDeAltura"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _NeblinaCor;     // rgb = cor
            float4 _NeblinaParams;  // x = densidade no chão, y = altura da base (m), z = queda por metro, w = distância máxima (m)
            float4 _NeblinaVento;   // xy = deslocamento do ruído (m), z = escala do ruído, w = força do ruído (0 a 1)

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 cor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float profundidade = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool ceu = profundidade <= 0.00001;
                #else
                    profundidade = lerp(UNITY_NEAR_CLIP_VALUE, 1, profundidade);
                    bool ceu = profundidade >= 0.99999;
                #endif
                float3 ponto = ComputeWorldSpacePosition(uv, profundidade, UNITY_MATRIX_I_VP);
                float3 camera = _WorldSpaceCameraPos;
                float3 raio = ponto - camera;
                float distancia = length(raio);
                float3 direcao = raio / max(distancia, 1e-4);
                distancia = ceu ? _NeblinaParams.w : min(distancia, _NeblinaParams.w);

                // densidade d(y) = d0 * exp(-k (y - base)); integral exata ao longo do raio.
                // Abaixo da base (bunker, porões) a conta usa a base: senão a neblina engrossaria debaixo da terra.
                float k = _NeblinaParams.z;
                float noOlho = _NeblinaParams.x * exp(-k * (max(camera.y, _NeblinaParams.y) - _NeblinaParams.y));
                float subida = k * direcao.y;
                float integral = abs(subida) > 1e-4
                    ? noOlho * (1.0 - exp(-subida * distancia)) / subida
                    : noOlho * distancia;

                // ruído: manchas de neblina mais grossa e mais rala, arrastadas pelo vento
                float2 amostra = (camera.xz + direcao.xz * min(distancia, 30.0) + _NeblinaVento.xy) * _NeblinaVento.z;
                float n = Ruido(amostra) * 0.65 + Ruido(amostra * 2.7 + 13.1) * 0.35;
                integral *= lerp(1.0 - _NeblinaVento.w, 1.0 + _NeblinaVento.w, n);

                float quanto = 1.0 - exp(-max(integral, 0.0));
                return half4(lerp(cor.rgb, _NeblinaCor.rgb, saturate(quanto)), cor.a);
            }
            ENDHLSL
        }
    }
}
