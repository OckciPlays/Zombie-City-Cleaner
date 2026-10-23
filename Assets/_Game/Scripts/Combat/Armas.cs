using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>As armas do jogo, na ordem dos modelos (o número vai pela rede).</summary>
    public enum TipoDeArma : byte { Pistola, Rifle, Submetralhadora, Espingarda }

    /// <summary>Os números de uma arma. A vida dos zumbis é contada em tiros de pistola (dano 1).</summary>
    public class DadosDaArma
    {
        public string nome, nomeComArtigo;
        public int capacidade;
        /// <summary>Tempo entre um tiro e o próximo (s). Na espingarda, inclui a bomba.</summary>
        public float intervalo;
        /// <summary>Atira enquanto o gatilho estiver apertado.</summary>
        public bool automatica;
        /// <summary>A recarga inteira (s); na espingarda, um cartucho.</summary>
        public float tempoRecarga;
        public bool cartuchoACartucho;
        /// <summary>Quando o carregador sai e quando o novo entra, desde o começo da recarga (s), para o som.</summary>
        public float carregadorSai, carregadorEntra;
        public int chumbos = 1;
        /// <summary>Dano de cada chumbo (ou do tiro).</summary>
        public float dano;
        /// <summary>Dispersão em graus parado e o acréscimo andando.</summary>
        public float dispersao, dispersaoAndando;
        public float coiceGraus;
        public float alcance;
        public SomDeTiro.Perfil som;
        /// <summary>A boca do cano na malha da arma avulsa (origem no cabo, cano em +Z).</summary>
        public Vector3 boca;
    }

    public static class Armas
    {
        public const int Quantas = 4;

        public static readonly DadosDaArma[] Todas =
        {
            new DadosDaArma
            {
                nome = "pistola", nomeComArtigo = "a pistola", capacidade = 12, intervalo = 0.18f, tempoRecarga = 40 / 24f,
                carregadorSai = 5 / 24f, carregadorEntra = 27 / 24f, dano = 1f, dispersao = 0.3f, dispersaoAndando = 1.2f,
                coiceGraus = 1.6f, alcance = 80f, som = SomDeTiro.Pistola, boca = new Vector3(0f, 0.093f, 0.188f),
            },
            new DadosDaArma
            {
                nome = "rifle", nomeComArtigo = "o rifle", capacidade = 30, intervalo = 0.1f, automatica = true,
                tempoRecarga = 46 / 24f, carregadorSai = 11 / 24f, carregadorEntra = 36 / 24f, dano = 1.4f, dispersao = 0.5f,
                dispersaoAndando = 1.8f, coiceGraus = 0.9f, alcance = 120f, som = SomDeTiro.Rifle, boca = new Vector3(0f, 0.105f, 0.732f),
            },
            new DadosDaArma
            {
                nome = "submetralhadora", nomeComArtigo = "a submetralhadora", capacidade = 25, intervalo = 0.075f,
                automatica = true, tempoRecarga = 40 / 24f, carregadorSai = 10 / 24f, carregadorEntra = 31 / 24f, dano = 0.8f,
                dispersao = 0.9f, dispersaoAndando = 1.6f, coiceGraus = 0.6f, alcance = 60f, som = SomDeTiro.Submetralhadora,
                boca = new Vector3(0f, 0.105f, 0.291f),
            },
            new DadosDaArma
            {
                nome = "espingarda", nomeComArtigo = "a espingarda", capacidade = 6, intervalo = 22 / 24f, tempoRecarga = 13 / 24f,
                cartuchoACartucho = true, chumbos = 8, dano = 0.5f, dispersao = 3.5f, dispersaoAndando = 1f, coiceGraus = 4f,
                alcance = 35f, som = SomDeTiro.Espingarda, boca = new Vector3(0f, 0.056f, 0.892f),
            },
        };

        /// <summary>Os nomes dos modelos (Jogador_FP_&lt;nome&gt;.fbx, Arma_&lt;nome&gt;, TP_&lt;clipe&gt;_&lt;nome&gt;).</summary>
        public static readonly string[] Modelos = { "Pistola", "Rifle", "Submetralhadora", "Espingarda" };

        public static DadosDaArma Dados(TipoDeArma t) => Todas[Mathf.Clamp((int)t, 0, Quantas - 1)];
    }
}
