using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// As armas do jogo, na ordem dos modelos (o número vai pela rede). As de sucata (09/11/2026, o Armeiro de sucata) vêm
    /// depois das reais: a pistola (um tiro), o rifle de precisão (de ferrolho, sem luneta) e a submetralhadora.
    /// </summary>
    public enum TipoDeArma : byte { Pistola, Rifle, Submetralhadora, Espingarda, Sniper, PistolaSucata, RifleSucata, SubmetralhadoraSucata }

    /// <summary>
    /// A munição de cada arma: a pistola e a submetralhadora usam 9mm, o rifle 5,56, a espingarda calibre 12 e a sniper
    /// 7,62. As balas vêm em caixas (<see cref="ZombieCityCleaner.Loot.Itens.MaxPorCaixa"/>), que a recarga gasta. As de
    /// sucata (09/11/2026) têm a munição só delas: a de prego (a pistola e a submetralhadora) e a de parafuso (o rifle); a
    /// munição real não serve nelas, nem a delas nas reais.
    /// </summary>
    public enum Calibre : byte { Nove, Cinco56, Doze, Sete62, Prego, Parafuso }

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
        /// <summary>O nome curto do canto do HUD (null: o nome; o rifle de precisão de sucata, "rifle de sucata", 09/11/2026: o inteiro ficava cortado).</summary>
        public string nomeNoHud;
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
        /// <summary>
        /// A arma real de que esta é a de sucata (09/11/2026): os braços de primeira pessoa, as animações e o lugar no corpo
        /// são os dela, com a malha da de sucata no lugar. null: uma arma real.
        /// </summary>
        public TipoDeArma? equivalente;
        /// <summary>O raio do barulho do tiro (o que chama os zumbis), vezes o do jogo.</summary>
        public float barulho = 1f;
        /// <summary>A chance de emperrar (a arma gasta, de <see cref="ZombieCityCleaner.Loot.Itens.DesgasteQueEmperra"/> em diante), vezes a do jogo.</summary>
        public float emperra = 1f;
        /// <summary>O desgaste de cada tiro, vezes o do jogo.</summary>
        public float desgaste = 1f;

        /// <summary>Uma cópia dos números (a de sucata parte da real).</summary>
        public DadosDaArma Copia() => (DadosDaArma)MemberwiseClone();
    }

    public static class Armas
    {
        public const int Quantas = 8;

        public static readonly DadosDaArma[] Todas = ComAsDeSucata(new[]
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
        });

        /// <summary>
        /// Os nomes dos modelos (Jogador_FP_&lt;nome&gt;.fbx, Arma_&lt;nome&gt;, TP_&lt;clipe&gt;_&lt;nome&gt;). As de sucata usam
        /// os braços e as animações da real (<see cref="Real"/>); o nome delas é o do FBX (Art/Armas/Modelos/Sucata_*.fbx).
        /// </summary>
        public static readonly string[] Modelos = { "Pistola", "Rifle", "Submetralhadora", "Espingarda", "Sniper", "Sucata_Pistola", "Sucata_Rifle", "Sucata_Submetralhadora" };

        public static DadosDaArma Dados(TipoDeArma t) => Todas[Mathf.Clamp((int)t, 0, Quantas - 1)];

        /// <summary>A arma real de uma de sucata (os braços, as animações e o corpo); as reais, ela mesma.</summary>
        public static TipoDeArma Real(TipoDeArma t) => Dados(t).equivalente ?? t;
        /// <summary>Uma arma de sucata (09/11/2026).</summary>
        public static bool DeSucata(TipoDeArma t) => Dados(t).equivalente != null;

        /// <summary>
        /// O rifle de sucata com os braços de primeira pessoa próprios (11/11/2026): o Jogador_FP_Sucata_Rifle.fbx, com as
        /// animações da sniper e a pegada no punho e na tábua dele (o Blender/Armas/pegada_fogo.py; o punho dele fica 11 cm
        /// abaixo do da Mosin). false volta aos braços da sniper (rodar Zombie City Cleaner > Armas > Montar os braços do
        /// rifle de sucata depois de mudar).
        /// </summary>
        public static readonly bool RifleDeSucataComBracosProprios = true;
        /// <summary>
        /// O nome dos braços de primeira pessoa de uma arma (Jogador_FP_&lt;nome&gt;.fbx e .overrideController): os da real,
        /// menos o rifle de sucata com os braços próprios (<see cref="RifleDeSucataComBracosProprios"/>). Os objetos dentro do
        /// FBX dele têm os nomes da sniper (FP_Bracos_Sniper, Boca_FP_Sniper).
        /// </summary>
        public static string BracosFP(TipoDeArma t) =>
            t == TipoDeArma.RifleSucata && RifleDeSucataComBracosProprios ? Modelos[(int)t] : Modelos[(int)Real(t)];

        // ------------------------------------------------------------------ as armas de sucata (09/11/2026)

        // Os números das de sucata a partir da real equivalente (a pedido: o dano ~75%, as primeiras do jogo, fabricadas):
        // mude aqui. O resto (a cadência, o coice, a recarga, o alcance e o som) é o da real.
        /// <summary>O dano, vezes o da real.</summary>
        public const float DanoDaSucata = 0.75f;
        /// <summary>A chance de emperrar, vezes a da real.</summary>
        public const float EmperraDaSucata = 2f;
        /// <summary>O desgaste de cada tiro, vezes o da real.</summary>
        public const float DesgasteDaSucata = 1.5f;
        /// <summary>O raio do barulho do tiro, vezes o da real.</summary>
        public const float BarulhoDaSucata = 1.25f;
        /// <summary>O espalhamento do tiro, vezes o da real (a pistola e a submetralhadora).</summary>
        public const float EspalhaDaSucata = 1.3f;
        /// <summary>O espalhamento do rifle de precisão, vezes o da sniper sem a luneta.</summary>
        public const float EspalhaDoRifleDeSucata = 1.15f;
        /// <summary>
        /// O rifle de sucata não tem luneta: mirando pela mira de ferro, espalha isto (graus; a sniper, na luneta, espalha 0,
        /// e 15% de nada é nada); sem mirar, o da sniper sem a luneta mais 15%.
        /// </summary>
        public const float MiraDeFerroDoRifle = 0.3f;
        /// <summary>Quantas balas cabem: a pistola de um tiro (recarrega a cada tiro), a submetralhadora e o rifle.</summary>
        public const int PenteDaPistolaDeSucata = 1, PenteDaSubmetralhadoraDeSucata = 20, PenteDoRifleDeSucata = 3;

        // as reais, e depois as de sucata (na ordem de TipoDeArma), feitas a partir delas
        static DadosDaArma[] ComAsDeSucata(DadosDaArma[] reais)
        {
            DadosDaArma Copia(TipoDeArma real, string nome, string artigo, Calibre calibre, int capacidade, Vector3 boca)
            {
                var r = reais[(int)real];
                var d = r.Copia();
                d.nome = nome;
                d.nomeComArtigo = artigo;
                d.calibre = calibre;
                d.capacidade = capacidade;
                d.dano = r.dano * DanoDaSucata;
                d.dispersao = r.dispersao * EspalhaDaSucata;
                d.dispersaoAndando = r.dispersaoAndando * EspalhaDaSucata;
                d.emperra = r.emperra * EmperraDaSucata;
                d.desgaste = r.desgaste * DesgasteDaSucata;
                d.barulho = r.barulho * BarulhoDaSucata;
                d.boca = boca;
                d.equivalente = real;
                d.corpoComo = r.corpoComo ?? real;
                return d;
            }
            var pistola = Copia(TipoDeArma.Pistola, "pistola de sucata", "a pistola de sucata", Calibre.Prego, PenteDaPistolaDeSucata,
                                new Vector3(0f, 0.088f, 0.197f));
            // o rifle de precisão: o de ferrolho, como a sniper, mas sem a luneta (a mira de ferro)
            var rifle = Copia(TipoDeArma.Sniper, "rifle de precisão de sucata", "o rifle de precisão de sucata", Calibre.Parafuso,
                              PenteDoRifleDeSucata, new Vector3(0f, 0.105f, 0.913f));
            rifle.fovLuneta = 0f;
            rifle.nomeNoHud = "rifle de sucata";   // (no HUD; o inteiro fica no inventário)
            rifle.dispersao = MiraDeFerroDoRifle;
            rifle.dispersaoSemLuneta = reais[(int)TipoDeArma.Sniper].dispersaoSemLuneta * EspalhaDoRifleDeSucata;
            rifle.dispersaoAndando = reais[(int)TipoDeArma.Sniper].dispersaoAndando * EspalhaDoRifleDeSucata;
            var submetralhadora = Copia(TipoDeArma.Submetralhadora, "submetralhadora de sucata", "a submetralhadora de sucata", Calibre.Prego,
                                        PenteDaSubmetralhadoraDeSucata, new Vector3(0f, 0.100f, 0.285f));
            var todas = new DadosDaArma[reais.Length + 3];
            reais.CopyTo(todas, 0);
            todas[(int)TipoDeArma.PistolaSucata] = pistola;
            todas[(int)TipoDeArma.RifleSucata] = rifle;
            todas[(int)TipoDeArma.SubmetralhadoraSucata] = submetralhadora;
            return todas;
        }

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
