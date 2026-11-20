using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// Os instrumentos da fogueira, mais detalhados (19/11/2026, a pedido, para o vídeo da fogueira: "fazer ficar bem legal e
    /// dar uma melhorada nos detalhes"). As peças redondas são caixinhas giradas em volta do eixo vertical (três quadrados de
    /// 30 em 30 graus fazem um disco de 12 lados; 12 tábuas fazem um aro). Os eixos de cada um são os de antes (a pose de
    /// quem toca, PassageiroDoCarro.NoChao.cs, conta com eles):
    /// - o violão deitado: o comprimento em X (a cabeça para +X), o tampo para +Y (o fundo em y = 0) e a largura em Z (os
    ///   bordões para +Z);
    /// - o pandeiro deitado: a pele para +Y (em y = 0,06), o aro em volta do meio (raio de fora de 13 cm);
    /// - a gaita: o comprimento em X, a altura em Y e os furos para -Z;
    /// - a flauta doce deitada: o bico em +X (a ponta em x = 0,26), os furos para +Y e o do polegar embaixo.
    /// </summary>
    public static partial class ItensDaCaca
    {
        // as casas do violão: a pestana em x = 0,552 e o rastilho em x = -0,279 (a escala de 0,831)
        const float Pestana = 0.552f, Escala = 0.831f;

        /// <summary>Onde fica a casa (o traste) n do violão, no comprimento dele (x).</summary>
        public static float TrasteDoViolao(int n) => Pestana - Escala * (1f - Mathf.Pow(2f, -n / 12f));

        // o violão: o bojo de baixo e o de cima redondos (a cintura sai do encontro deles), as laterais e o fundo de madeira
        // escura, o tampo claro com a beirada escura, a boca com a roseta, o escudo, o cavalete com o rastilho, o braço com a
        // escala, os trastes e a pestana, a cabeça com as tarraxas e as seis cordas (os bordões mais grossos)
        static Caixa[] Violao()
        {
            var l = new List<Caixa>();
            l.AddRange(Disco(-0.24f, 0.05f, 0f, 0.19f, 0.098f, 3, 2));
            l.AddRange(Disco(0f, 0.05f, 0f, 0.135f, 0.098f, 3, 2));
            l.AddRange(Disco(-0.24f, 0.0995f, 0f, 0.18f, 0.002f, 13, 4));
            l.AddRange(Disco(0f, 0.0995f, 0f, 0.125f, 0.002f, 13, 4));
            l.AddRange(Disco(-0.025f, 0.101f, 0f, 0.052f, 0.001f, 2, 0));
            l.AddRange(Disco(-0.025f, 0.102f, 0f, 0.042f, 0.001f, 6, 3));
            l.Add(C(-0.092f, 0.101f, -0.088f, 0.075f, 0.001f, 0.045f, 5, 14, 25f));
            l.Add(C(-0.285f, 0.104f, 0f, 0.028f, 0.007f, 0.15f, 2, 0));
            l.Add(C(-0.279f, 0.109f, 0f, 0.004f, 0.004f, 0.075f, 10, 9));
            l.Add(C(0.335f, 0.07f, 0f, 0.44f, 0.04f, 0.05f, 1, 14));
            l.Add(C(0.34f, 0.093f, 0f, 0.43f, 0.006f, 0.052f, 2, 0));
            for (int n = 1; n <= 11; n++) l.Add(C(TrasteDoViolao(n), 0.0965f, 0f, 0.0035f, 0.003f, 0.052f, 14, 3));
            l.Add(C(Pestana, 0.097f, 0f, 0.006f, 0.008f, 0.052f, 10, 9));
            l.Add(C(0.622f, 0.07f, 0f, 0.135f, 0.03f, 0.085f, 1, 14));
            l.Add(C(0.625f, 0.0855f, 0f, 0.12f, 0.001f, 0.07f, 2, 0));
            foreach (float x in new[] { 0.59f, 0.625f, 0.66f })
            {
                l.Add(C(x, 0.07f, 0.0525f, 0.012f, 0.012f, 0.022f, 14, 3));
                l.Add(C(x, 0.07f, -0.0525f, 0.012f, 0.012f, 0.022f, 14, 3));
            }
            float[] zs = { 0.021f, 0.0125f, 0.004f, -0.004f, -0.0125f, -0.021f };
            float[] grossura = { 0.0024f, 0.0021f, 0.0019f, 0.0016f, 0.0015f, 0.0014f };
            for (int k = 0; k < 6; k++) l.Add(C((Pestana - 0.283f) * 0.5f, 0.105f, zs[k], Pestana + 0.283f, grossura[k], grossura[k], 14, 3));
            return l.ToArray();
        }

        // o pandeiro: o aro de madeira de 12 tábuas, a pele clara um pouco abaixo da beira e as cinco platinelas (os pares de
        // discos de metal, por dentro e por fora do aro), com um vão embaixo à esquerda para o polegar de quem segura
        static Caixa[] Pandeiro()
        {
            var l = new List<Caixa>();
            l.AddRange(Aro(0.031f, 0.124f, 0.012f, 0.062f, 12, 4));
            l.AddRange(Disco(0f, 0.0585f, 0f, 0.119f, 0.003f, 10, 9));
            for (int k = 0; k < 5; k++)
            {
                float graus = 257f + k * 72f, a = graus * Mathf.Deg2Rad;
                foreach (float r in new[] { 0.111f, 0.137f })
                    l.Add(C(r * Mathf.Cos(a), 0.031f, r * Mathf.Sin(a), 0.004f, 0.032f, 0.036f, 14, 3, -graus));
            }
            return l.ToArray();
        }

        // a gaita: as tampas de metal, o pente de madeira e os dez furos do lado da boca
        static Caixa[] Gaita()
        {
            var l = new List<Caixa>
            {
                C(0f, 0.0125f, 0f, 0.1f, 0.017f, 0.026f, 12, 4),
                C(0f, 0.0235f, 0f, 0.104f, 0.005f, 0.028f, 14, 3),
                C(0f, 0.0025f, 0f, 0.104f, 0.005f, 0.028f, 14, 3),
            };
            for (int k = 0; k < 10; k++) l.Add(C(-0.045f + 0.01f * k, 0.0125f, -0.0133f, 0.0055f, 0.008f, 0.001f, 6, 3));
            return l.ToArray();
        }

        // a flauta doce: o corpo de madeira clara, a cabeça, o bico e o pé mais escuros, a janela, os sete furos em cima e o
        // do polegar embaixo
        static Caixa[] Flauta()
        {
            var l = new List<Caixa>
            {
                C(0f, 0.012f, 0f, 0.38f, 0.024f, 0.024f, 13, 4),
                C(0.21f, 0.014f, 0f, 0.06f, 0.028f, 0.028f, 12, 4),
                C(0.25f, 0.016f, 0f, 0.022f, 0.016f, 0.02f, 12, 4),
                C(-0.205f, 0.013f, 0f, 0.04f, 0.026f, 0.026f, 12, 4),
                C(0.195f, 0.0285f, 0f, 0.012f, 0.002f, 0.01f, 2, 0),
                C(0.1f, -0.0005f, 0f, 0.008f, 0.002f, 0.008f, 2, 0),
            };
            foreach (float x in new[] { 0.08f, 0.04f, 0f, -0.04f, -0.08f, -0.12f })
                l.Add(C(x, 0.0245f, 0f, 0.008f, 0.002f, 0.008f, 2, 0));
            l.Add(C(-0.155f, 0.0245f, 0f, 0.006f, 0.002f, 0.006f, 2, 0));
            return l.ToArray();
        }
    }
}
