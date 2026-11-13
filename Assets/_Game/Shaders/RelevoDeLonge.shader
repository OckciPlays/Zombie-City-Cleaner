// O relevo de longe do mapa fixo de Lakewood (Plano_Lakewood): a cor vem do vértice (a mistura da mata, do campo, da
// lavoura, da rocha e do urbano, já em linear), interpolada pela face, com a luz do sol, o ambiente e a neblina do URP.
// Sem sombras: ele é visto de quilômetros, sem a neblina. O alfa do vértice é a altura da copa da mata (até 16 m): a
// malha fica 6 m abaixo do chão, e a copa só sobe longe da câmera (de 430 a 650 m), onde as árvores de perto acabam (480 m).
// Na mata, a cor é salpicada por um ruído (as copas vistas de longe), para a copa não parecer um cobertor liso.
Shader "ZombieCityCleaner/RelevoDeLonge"
{
    Properties
    {
        _Brilho("Clareza", Range(0.5, 1.5)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Brilho;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                half4 color : COLOR;
                float fogFactor : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

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

            Varyings Vert(Attributes i)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
                float d = distance(posWS, _WorldSpaceCameraPos.xyz);
                posWS.y += i.color.a * 16.0 * smoothstep(430.0, 650.0, d);
                o.positionCS = TransformWorldToHClip(posWS);
                o.positionWS = posWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                // as copas: o ruído salpica a cor e entorta a normal onde a mata é alta (o alfa da cor)
                half mata = saturate(i.color.a * 4.0);
                float2 q = i.positionWS.xz;
                float r1 = Ruido(q / 12.0), r2 = Ruido(q / 5.0 + 7.3);
                float salpico = (r1 - 0.5) * 0.5 + (r2 - 0.5) * 0.3;
                n = normalize(n + float3(Ruido(q / 9.0 + 3.1) - 0.5, 0.0, Ruido(q / 9.0 + 11.7) - 0.5) * 0.7 * mata);
                Light luz = GetMainLight();
                half3 difusa = luz.color * saturate(dot(n, luz.direction));
                half3 ambiente = SampleSH(n);
                half3 cor = i.color.rgb * (1.0 + salpico * mata) * (difusa + ambiente) * _Brilho;
                cor = MixFog(cor, i.fogFactor);
                return half4(cor, 1);
            }
            ENDHLSL
        }
    }
}
