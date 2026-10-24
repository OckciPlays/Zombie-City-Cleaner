using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>As armas do jogo, na ordem dos modelos (o número vai pela rede).</summary>
    public enum TipoDeArma : byte { Pistola, Rifle, Submetralhadora, Espingarda, Sniper }

    /// <summary>
    /// O dano de uma arma até uma distância: no tronco (a vida do corpo é 6) e num braço ou numa perna (a vida de cada
    /// membro é 3, e o membro cai quando ela acaba).
    /// </summary>
    public struct FaixaDeDano
    {
        public float ate, corpo, membro;
        public FaixaDeDano(float ate, float corpo, float membro) { this.ate = ate; this.corpo = corpo; this.membro = membro; }
    }

    /// <summary>
    /// Os números de uma arma. A vida dos zumbis é contada em tiros de pistola: o corpo aguenta 6 e cada membro, 3. Na
    /// cabeça, qualquer acerto explode a cabeça.
    /// </summary>
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
        /// <summary>Dano de cada tiro no tronco e num membro (as armas que não mudam com a distância).</summary>
        public float dano, danoNoMembro;
        /// <summary>
        /// Se tiver, o dano muda com a distância: vale a primeira faixa que alcança o alvo (a espingarda).
        /// </summary>
        public FaixaDeDano[] faixas;
        /// <summary>
        /// Os chumbos contam juntos: cada zumbi atingido leva um acerto só por disparo, na parte que mais levou chumbo (a
        /// cabeça, se algum pegou nela). Assim o dano da espingarda não depende de quantos chumbos pegaram.
        /// </summary>
        public bool chumbosJuntos;
        /// <summary>Matando até esta distância, o corpo é jogado para trás (a espingarda de perto). 0 = nunca.</summary>
        public float arremessoAte;
        /// <summary>
        /// A luneta (a sniper): mirando, a câmera fecha até este campo de visão (graus), os braços somem e a mira vira a
        /// da luneta. 0 = sem luneta.
        /// </summary>
        public float fovLuneta;
        /// <summary>A dispersão atirando sem a luneta (a sniper "de quadril"), em graus.</summary>
        public float dispersaoSemLuneta;
        /// <summary>Depois do tiro, a mão puxa o ferrolho (a sniper): o som dele sai junto da animação.</summary>
        public bool ferrolho;
        /// <summary>O corpo (o que os outros veem) usa as animações desta arma; null = as dela mesma.</summary>
        public TipoDeArma? corpoComo;
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
        public const int Quantas = 5;

        public static readonly DadosDaArma[] Todas =
        {
            new DadosDaArma
            {
                nome = "pistola", nomeComArtigo = "a pistola", capacidade = 12, intervalo = 0.18f, tempoRecarga = 40 / 24f,
                carregadorSai = 5 / 24f, carregadorEntra = 27 / 24f, dano = 1f, danoNoMembro = 1f, dispersao = 0.3f, dispersaoAndando = 1.2f,
                coiceGraus = 1.6f, alcance = 80f, som = SomDeTiro.Pistola, boca = new Vector3(0f, 0.093f, 0.188f),
            },
            new DadosDaArma
            {
                nome = "rifle", nomeComArtigo = "o rifle", capacidade = 30, intervalo = 0.1f, automatica = true,
                tempoRecarga = 46 / 24f, carregadorSai = 11 / 24f, carregadorEntra = 36 / 24f, dano = 1.5f, danoNoMembro = 1.5f, dispersao = 0.5f,
                dispersaoAndando = 1.8f, coiceGraus = 0.9f, alcance = 120f, som = SomDeTiro.Rifle, boca = new Vector3(0f, 0.105f, 0.732f),
            },
            new DadosDaArma
            {
                nome = "submetralhadora", nomeComArtigo = "a submetralhadora", capacidade = 25, intervalo = 0.075f,
                automatica = true, tempoRecarga = 40 / 24f, carregadorSai = 10 / 24f, carregadorEntra = 31 / 24f, dano = 1f, danoNoMembro = 1f,
                dispersao = 0.9f, dispersaoAndando = 1.6f, coiceGraus = 0.6f, alcance = 60f, som = SomDeTiro.Submetralhadora,
                boca = new Vector3(0f, 0.105f, 0.291f),
            },
            new DadosDaArma
            {
                nome = "espingarda", nomeComArtigo = "a espingarda", capacidade = 6, intervalo = 22 / 24f, tempoRecarga = 13 / 24f,
                cartuchoACartucho = true, chumbos = 8, chumbosJuntos = true, arremessoAte = 5f,
                // de perto (até 5 m) mata com um tiro no corpo e arranca um membro; na média (até 15 m), dois; de longe, pouco
                faixas = new[] { new FaixaDeDano(5f, 7f, 3f), new FaixaDeDano(15f, 3f, 1.5f), new FaixaDeDano(float.MaxValue, 1f, 0.5f) },
                dispersao = 3.5f, dispersaoAndando = 1f, coiceGraus = 4f,
                alcance = 35f, som = SomDeTiro.Espingarda, boca = new Vector3(0f, 0.056f, 0.892f),
            },
            new DadosDaArma
            {
                // de ferrolho, com luneta: um tiro no corpo mata e arranca um membro; cinco no pente; o tiro inclui puxar o
                // ferrolho (a animação Fire tem 1,25 s). Sem a luneta, espalha muito.
                nome = "sniper", nomeComArtigo = "a sniper", capacidade = 5, intervalo = 30 / 24f, tempoRecarga = 46 / 24f,
                carregadorSai = 11 / 24f, carregadorEntra = 36 / 24f, dano = 8f, danoNoMembro = 8f,
                dispersao = 0f, dispersaoSemLuneta = 4f, dispersaoAndando = 2.5f, coiceGraus = 5f, alcance = 160f,
                fovLuneta = 12f, ferrolho = true, corpoComo = TipoDeArma.Rifle,
                som = SomDeTiro.Sniper, boca = new Vector3(0f, 0.115f, 0.905f),
            },
        };

        /// <summary>Os nomes dos modelos (Jogador_FP_&lt;nome&gt;.fbx, Arma_&lt;nome&gt;, TP_&lt;clipe&gt;_&lt;nome&gt;).</summary>
        public static readonly string[] Modelos = { "Pistola", "Rifle", "Submetralhadora", "Espingarda", "Sniper" };

        public static DadosDaArma Dados(TipoDeArma t) => Todas[Mathf.Clamp((int)t, 0, Quantas - 1)];

        /// <summary>O dano desta arma a esta distância, no tronco e num membro.</summary>
        public static FaixaDeDano Dano(DadosDaArma d, float distancia)
        {
            if (d.faixas != null)
                foreach (var f in d.faixas)
                    if (distancia <= f.ate) return f;
            return new FaixaDeDano(float.MaxValue, d.dano, d.danoNoMembro);
        }
    }
}
