using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>As armas do jogo, na ordem dos modelos (o número vai pela rede).</summary>
    public enum TipoDeArma : byte { Pistola, Rifle, Submetralhadora, Espingarda, Sniper }

    /// <summary>
    /// A munição de cada arma: a pistola e a submetralhadora usam 9mm, o rifle 5,56, a espingarda calibre 12 e a sniper
    /// 7,62. As balas vêm em caixas (<see cref="ZombieCityCleaner.Loot.Itens.MaxPorCaixa"/>), que a recarga gasta.
    /// </summary>
    public enum Calibre : byte { Nove, Cinco56, Doze, Sete62 }

    /// <summary>
    /// O dano de uma arma até uma distância (a espingarda). A parte do corpo multiplica no zumbi (AjustesDeZumbi): a
    /// cabeça 3 vezes, os braços e as pernas metade.
    /// </summary>
    public struct FaixaDeDano
    {
        public float ate, dano;
        public FaixaDeDano(float ate, float dano) { this.ate = ate; this.dano = dano; }
    }

    /// <summary>
    /// Os números de uma arma. O dano é na escala de 100 (31/10/2026, a pedido: a vida do Oco é 100; antes era 6, em
    /// tiros de pistola): a pistola tira 15 (7 tiros no corpo, 3 na cabeça, que vale 3 vezes), a submetralhadora 18, o
    /// rifle 25 e a sniper 50 (2 no corpo, 1 na cabeça). Cada braço e cada perna tem 50 de vida (4 tiros de pistola).
    /// </summary>
    public class DadosDaArma
    {
        public string nome, nomeComArtigo;
        public int capacidade;
        /// <summary>A munição que ela usa (a recarga tira as balas das caixas desse calibre).</summary>
        public Calibre calibre;
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
        /// <summary>Dano de cada tiro (as armas que não mudam com a distância), antes do multiplicador da parte do corpo.</summary>
        public float dano;
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
                nome = "pistola", nomeComArtigo = "a pistola", capacidade = 12, calibre = Calibre.Nove, intervalo = 0.18f, tempoRecarga = 40 / 24f,
                carregadorSai = 5 / 24f, carregadorEntra = 27 / 24f, dano = 15f, dispersao = 0.3f, dispersaoAndando = 1.2f,
                coiceGraus = 1.6f, alcance = 80f, som = SomDeTiro.Pistola, boca = new Vector3(0f, 0.093f, 0.188f),
            },
            new DadosDaArma
            {
                nome = "rifle", nomeComArtigo = "o rifle", capacidade = 30, calibre = Calibre.Cinco56, intervalo = 0.1f, automatica = true,
                tempoRecarga = 46 / 24f, carregadorSai = 11 / 24f, carregadorEntra = 36 / 24f, dano = 25f, dispersao = 0.5f,
                dispersaoAndando = 1.8f, coiceGraus = 0.9f, alcance = 120f, som = SomDeTiro.Rifle, boca = new Vector3(0f, 0.105f, 0.732f),
            },
            new DadosDaArma
            {
                nome = "submetralhadora", nomeComArtigo = "a submetralhadora", capacidade = 25, calibre = Calibre.Nove, intervalo = 0.075f,
                automatica = true, tempoRecarga = 40 / 24f, carregadorSai = 10 / 24f, carregadorEntra = 31 / 24f, dano = 18f,
                dispersao = 0.9f, dispersaoAndando = 1.6f, coiceGraus = 0.6f, alcance = 60f, som = SomDeTiro.Submetralhadora,
                boca = new Vector3(0f, 0.105f, 0.291f),
            },
            new DadosDaArma
            {
                nome = "espingarda", nomeComArtigo = "a espingarda", capacidade = 6, calibre = Calibre.Doze, intervalo = 22 / 24f, tempoRecarga = 13 / 24f,
                cartuchoACartucho = true, chumbos = 8, chumbosJuntos = true, arremessoAte = 5f,
                // de perto (até 5 m) mata com um tiro no corpo (115: sobra para um pouco de pedra) e arranca um membro; na
                // média (até 15 m), 4 no corpo e 2 na cabeça (com os chumbos juntos, um chumbo na cabeça era fácil demais
                // para valer um tiro só); de longe, pouco
                faixas = new[] { new FaixaDeDano(5f, 115f), new FaixaDeDano(15f, 30f), new FaixaDeDano(float.MaxValue, 17f) },
                dispersao = 3.5f, dispersaoAndando = 1f, coiceGraus = 4f,
                alcance = 35f, som = SomDeTiro.Espingarda, boca = new Vector3(0f, 0.056f, 0.892f),
            },
            new DadosDaArma
            {
                // de ferrolho, com luneta: um tiro na cabeça mata, no corpo são dois, e um tiro arranca um membro; cinco no
                // pente; o tiro inclui puxar o ferrolho (a animação Fire tem 1,25 s). Sem a luneta, espalha muito.
                nome = "sniper", nomeComArtigo = "a sniper", capacidade = 5, calibre = Calibre.Sete62, intervalo = 30 / 24f, tempoRecarga = 46 / 24f,
                carregadorSai = 11 / 24f, carregadorEntra = 36 / 24f, dano = 50f,
                dispersao = 0f, dispersaoSemLuneta = 4f, dispersaoAndando = 2.5f, coiceGraus = 5f, alcance = 160f,
                fovLuneta = 12f, ferrolho = true, corpoComo = TipoDeArma.Rifle,
                som = SomDeTiro.Sniper, boca = new Vector3(0f, 0.115f, 0.905f),
            },
        };

        /// <summary>Os nomes dos modelos (Jogador_FP_&lt;nome&gt;.fbx, Arma_&lt;nome&gt;, TP_&lt;clipe&gt;_&lt;nome&gt;).</summary>
        public static readonly string[] Modelos = { "Pistola", "Rifle", "Submetralhadora", "Espingarda", "Sniper" };

        public static DadosDaArma Dados(TipoDeArma t) => Todas[Mathf.Clamp((int)t, 0, Quantas - 1)];

        /// <summary>O dano desta arma a esta distância (antes do multiplicador da parte do corpo).</summary>
        public static float Dano(DadosDaArma d, float distancia)
        {
            if (d.faixas != null)
                foreach (var f in d.faixas)
                    if (distancia <= f.ate) return f.dano;
            return d.dano;
        }
    }
}
