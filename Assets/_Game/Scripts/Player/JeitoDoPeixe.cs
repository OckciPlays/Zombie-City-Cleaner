using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O jeito de cada peixe da vara (27/11/2026, a pesca nova, a pedido: "isso precisa variar para cada tipo de peixe, por
    /// exemplo um peixe maior, ele belisca mais, é mais difícil de pescar"; "cada peixe possui um padrão de pesca, onde ele é
    /// mais rápido, mais lento, dependendo do seu tamanho e espécie e raridade"). A ficha diz:
    /// <list type="bullet">
    /// <item><b>a espera</b> (o que a boia faz, sem texto nenhum): quantas beliscadas antes da mordida, quanto da boia afunda
    /// em cada uma (de 0 a 1 da altura dela), quanto cada uma dura, se ele arrasta a boia para o lado, e a janela para fisgar
    /// depois que a boia some (o peixe maior dá menos tempo);</item>
    /// <item><b>a briga</b> (o círculo: <see cref="BrigaDoPeixe"/>): a velocidade, os arrancos, o quanto ele muda de rumo, as
    /// paradas, o tamanho do ícone, o ritmo da barra de captura e a cor dele.</item>
    /// </list>
    /// O peixe é sorteado no começo da espera (<c>PescaJogador.SortearPeixe</c>), para as beliscadas já serem dele.
    /// </summary>
    public sealed class JeitoDoPeixe
    {
        // ------------------------------------------------------------------ a espera
        /// <summary>Quantas beliscadas antes da mordida (sorteado entre os dois, com os dois).</summary>
        public int beliscadasMin, beliscadasMax;
        /// <summary>Quanto da boia afunda numa beliscada (0 nada, 1 a boia toda embaixo d'água).</summary>
        public float afundaMin, afundaMax;
        /// <summary>Quanto dura uma beliscada (s): desce, fica e volta.</summary>
        public float duracaoMin, duracaoMax;
        /// <summary>A beliscada arrasta a boia um pouco para o lado (o bagre, no fundo, puxando devagar).</summary>
        public bool arrasta;
        /// <summary>A janela para fisgar depois que a boia some de uma vez (s).</summary>
        public float janela;

        // ------------------------------------------------------------------ a briga (o círculo, raio 1)
        /// <summary>A velocidade de cruzeiro do peixe no círculo (raios por segundo).</summary>
        public float velocidade;
        /// <summary>Os arrancos: quantos por segundo (em média) e quantas vezes mais rápido no arranco.</summary>
        public float arrancosPorSegundo, arranco;
        /// <summary>O quanto o rumo muda (graus por segundo de vaivém) e quantas paradas por segundo.</summary>
        public float giro, paradasPorSegundo;
        /// <summary>O raio do ícone do peixe (do raio do círculo).</summary>
        public float tamanho;
        /// <summary>A barra de captura: quanto enche por segundo com a boia nele e quanto esvazia longe dele.</summary>
        public float enche, esvazia;
        /// <summary>A cor do peixe no círculo.</summary>
        public Color cor;

        // as fichas (os peixes da vara: Loot/Itens.cs; o salmão é da lança). A barra de captura, da metade (30%) até cheia
        // com a boia sempre em cima: a perca-sol em ~5 s, a truta em ~6,5, o black bass e o bagre em ~8, a dourada em ~8 (mas ela foge rápido e muda de lado:
        // num teste com um robô de reação humana, 0,2 s, ela saiu 1 vez em 5; o black bass, 4 em 5; os outros, sempre);
        // longe dele, de 30% a vazia em 1,5 a 3 s
        static readonly JeitoDoPeixe Perca = new JeitoDoPeixe
        {
            beliscadasMin = 0, beliscadasMax = 2, afundaMin = 0.20f, afundaMax = 0.35f, duracaoMin = 0.35f, duracaoMax = 0.55f, janela = 1.2f,
            velocidade = 0.30f, arrancosPorSegundo = 0.12f, arranco = 1.8f, giro = 60f, paradasPorSegundo = 0.25f, tamanho = 0.13f,
            enche = 0.14f, esvazia = 0.10f, cor = new Color(0.78f, 0.74f, 0.32f),
        };
        static readonly JeitoDoPeixe Truta = new JeitoDoPeixe
        {
            beliscadasMin = 1, beliscadasMax = 3, afundaMin = 0.30f, afundaMax = 0.50f, duracaoMin = 0.35f, duracaoMax = 0.6f, janela = 1.0f,
            velocidade = 0.45f, arrancosPorSegundo = 0.25f, arranco = 2.0f, giro = 140f, paradasPorSegundo = 0.15f, tamanho = 0.14f,
            enche = 0.11f, esvazia = 0.13f, cor = new Color(0.85f, 0.55f, 0.62f),
        };
        static readonly JeitoDoPeixe Bass = new JeitoDoPeixe
        {
            beliscadasMin = 2, beliscadasMax = 5, afundaMin = 0.40f, afundaMax = 0.70f, duracaoMin = 0.4f, duracaoMax = 0.7f, janela = 0.8f,
            velocidade = 0.55f, arrancosPorSegundo = 0.45f, arranco = 2.4f, giro = 110f, paradasPorSegundo = 0.12f, tamanho = 0.17f,
            enche = 0.09f, esvazia = 0.16f, cor = new Color(0.34f, 0.52f, 0.30f),
        };
        static readonly JeitoDoPeixe Bagre = new JeitoDoPeixe
        {
            beliscadasMin = 2, beliscadasMax = 4, afundaMin = 0.30f, afundaMax = 0.60f, duracaoMin = 0.8f, duracaoMax = 1.4f, arrasta = true, janela = 0.9f,
            velocidade = 0.28f, arrancosPorSegundo = 0.35f, arranco = 3.0f, giro = 50f, paradasPorSegundo = 0.4f, tamanho = 0.17f,
            enche = 0.09f, esvazia = 0.14f, cor = new Color(0.48f, 0.38f, 0.28f),
        };
        static readonly JeitoDoPeixe Dourada = new JeitoDoPeixe
        {
            beliscadasMin = 3, beliscadasMax = 6, afundaMin = 0.20f, afundaMax = 0.70f, duracaoMin = 0.2f, duracaoMax = 0.45f, janela = 0.6f,
            velocidade = 0.55f, arrancosPorSegundo = 0.5f, arranco = 1.9f, giro = 150f, paradasPorSegundo = 0.08f, tamanho = 0.12f,
            enche = 0.085f, esvazia = 0.15f, cor = new Color(0.98f, 0.76f, 0.22f),
        };

        /// <summary>A ficha do peixe (a perca-sol para o que não tiver uma).</summary>
        public static JeitoDoPeixe De(Item peixe) => peixe switch
        {
            Item.PeixeMedio => Truta,
            Item.PeixeGrande => Bass,
            Item.Bagre => Bagre,
            Item.PeixeDourado => Dourada,
            _ => Perca,
        };
    }
}
