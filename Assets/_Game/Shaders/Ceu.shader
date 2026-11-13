// O céu (09/11/2026, a pedido: "queria poder ver um sol, uma lua que não existe hoje"): o skybox do mundo. O degradê do
// alto ao horizonte (as cores do AmbienteDoDia, do dia e da noite, pelas globais), a névoa da neblina perto do horizonte,
// o sol (o disco e o clarão na direção da luz do dia), a lua (o disco com a fase, na direção da luz da noite: a mesma luz
// vira a lua), as estrelas (à noite, piscando) e as nuvens (o ruído numa cúpula, arrastado pelo vento, mais fechado com
// o tempo fechado). As globais vêm do AmbienteDoDia: _ZccCeuTopo, _ZccCeuHorizonte, _ZccCeuNeblina, _ZccSol (xyz a
// direção, w o dia), _ZccSolCor, _ZccLua (xyz a direção, w a noite), _ZccNuvens, _ZccEstrelas.
Shader "ZombieCityCleaner/Ceu"
{
    Properties
    {
        _Nuvens("Nuvens (0 a 1, somado ao tempo)", Range(0, 1)) = 0.35
        _SolTamanho("Tamanho do sol", Range(0.998, 0.9999)) = 0.9995
        _LuaTamanho("Tamanho da lua", Range(0.998, 0.9999)) = 0.9996
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Nuvens;
                float _SolTamanho;
                float _LuaTamanho;
            CBUFFER_END

            float4 _ZccCeuTopo;
            float4 _ZccCeuHorizonte;
            float4 _ZccCeuNeblina;
            float4 _ZccSol;
            float4 _ZccSolCor;
            float4 _ZccLua;
            float _ZccNuvens;
            float _ZccEstrelas;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Hash3(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Ruido(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                return Ruido(p) * 0.5 + Ruido(p * 2.03 + 11.0) * 0.25 + Ruido(p * 4.1 + 23.0) * 0.125 + Ruido(p * 8.3 + 5.0) * 0.0625;
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.dir);
                float y = d.y;
                float dia = _ZccSol.w, noite = _ZccLua.w;

                // o degradê: o alto, o horizonte e, embaixo dele, o horizonte escurecendo
                float t = saturate(y);
                half3 ceu = lerp(_ZccCeuHorizonte.rgb, _ZccCeuTopo.rgb, pow(t, 0.55));
                ceu = lerp(ceu, _ZccCeuHorizonte.rgb * 0.8, saturate(-y * 6.0));

                // as estrelas (só de noite, piscando devagar; somem com as nuvens): um ponto redondo no meio de umas
                // células da direção, com o tamanho e o brilho sorteados
                float3 q = floor(d * 420.0);
                float h = Hash3(q);
                float3 meio = (q + 0.5 + (float3(Hash3(q + 1.3), Hash3(q + 2.7), Hash3(q + 4.1)) - 0.5) * 0.6) / 420.0;
                float longe = length(d - normalize(meio)) * 420.0;
                float estrela = step(0.9955, h) * smoothstep(0.42, 0.12, longe) * (0.5 + 0.5 * Hash3(q + 9.0));
                estrela *= 0.7 + 0.3 * sin(_Time.y * 1.7 + h * 600.0);
                ceu += estrela * _ZccEstrelas * saturate(y * 4.0 + 0.1) * half3(0.85, 0.88, 1.0) * 1.4;

                // as nuvens: o ruído numa cúpula (a direção projetada numa altura), arrastado pelo vento
                float2 p = d.xz / max(y, 0.035) * 1.1 + float2(_Time.y * 0.006, _Time.y * 0.0025);
                float n = Fbm(p * 1.4);
                float n2 = Fbm(p * 3.1 + 7.7);
                float cobertura = saturate(_ZccNuvens + _Nuvens);
                float limiar = 0.72 - cobertura * 0.5;
                float nuvem = smoothstep(limiar, limiar + 0.22, n) * saturate(y * 5.0);
                half3 claro = lerp(_ZccCeuTopo.rgb * 1.35 + 0.08, _ZccSolCor.rgb * 0.9 + 0.1, 0.45 * dia);
                half3 escuro = _ZccCeuHorizonte.rgb * 0.75;
                half3 corDaNuvem = lerp(escuro, claro, saturate(0.35 + n2 * 0.9 - cobertura * 0.3));
                ceu = lerp(ceu, corDaNuvem, nuvem * 0.92);

                // o sol: o disco e o clarão (atrás das nuvens, só o clarão passa)
                float cosS = dot(d, _ZccSol.xyz);
                float disco = smoothstep(_SolTamanho - 0.0002, _SolTamanho + 0.00015, cosS);
                float clarao = pow(saturate(cosS), 180.0) * 0.4 + pow(saturate(cosS), 16.0) * 0.1;
                ceu += _ZccSolCor.rgb * (disco * 1.6 * (1.0 - nuvem) + clarao * (1.0 - nuvem * 0.6)) * dia;

                // a lua: o disco com a sombra da fase (um segundo disco deslocado tira um gomo), e um brilho fraco
                float cosL = dot(d, _ZccLua.xyz);
                float lua = smoothstep(_LuaTamanho - 0.00015, _LuaTamanho + 0.0001, cosL);
                float3 sombraDir = normalize(_ZccLua.xyz + float3(0.014, 0.004, 0.0));
                float sombra = smoothstep(_LuaTamanho - 0.00015, _LuaTamanho + 0.0001, dot(d, sombraDir));
                lua *= 1.0 - 0.9 * sombra;
                float brilhoDaLua = pow(saturate(cosL), 700.0) * 0.2;
                ceu += (half3(0.86, 0.88, 0.95) * lua * 1.1 + half3(0.7, 0.75, 0.9) * brilhoDaLua) * noite * (1.0 - nuvem);

                // a névoa da neblina no horizonte
                float neblina = exp(-max(y, 0.0) * 7.0);
                ceu = lerp(ceu, _ZccCeuNeblina.rgb, neblina * 0.9);
                return half4(ceu, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
