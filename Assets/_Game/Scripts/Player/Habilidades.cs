using System;
using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>As 3 áreas da árvore de habilidades (06/11/2026).</summary>
    public enum AreaDaHabilidade : byte { Combate, Resistencia, Sobrevivencia }

    /// <summary>Os 9 ramos: 3 em cada área, cada um com 4 habilidades e a ponta.</summary>
    public enum RamoDaHabilidade : byte { Guerreiro, Assassino, Ladrao, Saude, FomeESede, Recursos, Craft, Sortudo, Pesca }

    /// <summary>
    /// As 45 habilidades, na ordem dos ramos (5 em cada: as 4 em sequência e a ponta). O número é o bit no
    /// <see cref="ProgressoDoPersonagem.aprendidas"/>: não mudar a ordem (vai no save).
    /// </summary>
    public enum Habilidade : byte
    {
        // Combate: o Guerreiro
        TiroForte, RecargaRapida, BracoForte, MaoFirme, MiraDeAco,
        // Combate: o Assassino
        PassosLeves, PelasCostas, Discreto, TiroAbafado, SangueFrio,
        // Combate: o Ladrão
        Borracheiro, MaosLeves, Chaveiro, AlarmeMudo, LigacaoDireta,
        // Resistência: a Saúde
        MaosDeEnfermeiro, CurativoBemFeito, Imunidade, NervosDeAco, MedicoDeCampo,
        // Resistência: a Fome e sede
        EstomagoForte, Cantil, Folego, BemNutrido, Camelo,
        // Resistência: os Recursos
        Cuidadoso, Economico, Reparador, Farmaceutico, Remendao,
        // Sobrevivência: o Craft
        FazTudo, MaosHabeis, Artesao, Aproveitador, Armeiro,
        // Sobrevivência: o Sortudo
        OlhoBom, Garimpeiro, Abutre, PeDeCoelho, Faro,
        // Sobrevivência: a Pesca
        Paciencia, IscaFina, PeixeGrande, NoiteDePesca, MestrePescador,
    }

    /// <summary>
    /// O progresso de um personagem na árvore: o XP somado (o nível sai dele) e as habilidades aprendidas (um bit por
    /// <see cref="Habilidade"/>). Vai pela rede (<see cref="InventarioJogador.Progresso"/>) e no save do personagem.
    /// </summary>
    [Serializable]
    public struct ProgressoDoPersonagem : INetworkSerializeByMemcpy, IEquatable<ProgressoDoPersonagem>
    {
        public int xp;
        public ulong aprendidas;

        public bool Tem(Habilidade h) => (aprendidas & (1UL << (int)h)) != 0;
        public int Nivel => Habilidades.NivelDe(xp);
        /// <summary>Quantas habilidades ele já aprendeu.</summary>
        public int Aprendidas
        {
            get
            {
                int n = 0;
                for (ulong b = aprendidas; b != 0; b &= b - 1) n++;
                return n;
            }
        }
        /// <summary>Os pontos livres: um por nível (o nível 1 já dá o primeiro), menos os gastos.</summary>
        public int Pontos => Mathf.Max(0, Nivel - Aprendidas);

        public bool Equals(ProgressoDoPersonagem o) => xp == o.xp && aprendidas == o.aprendidas;
        public override bool Equals(object o) => o is ProgressoDoPersonagem p && Equals(p);
        public override int GetHashCode() => xp ^ aprendidas.GetHashCode();
    }

    /// <summary>
    /// A árvore de habilidades (06/11/2026, a pedido: "um sistema de XP que você melhoraria suas skill"). O XP vem das
    /// ações (matar, mais um dia vivo, tratar, criar, saquear, a ligação direta), num total só; cada nível dá 1 ponto livre
    /// ("pontos livres a cada nível e utilizamos onde acharmos melhor"). São 45 habilidades e 30 níveis: não dá para pegar
    /// tudo ("vai ficar a critério do jogador"). O nível 30 chega em umas 12 horas de jogo, com números pequenos (cada
    /// ação dá de 1 a 20). Morrer não tira nada (por enquanto). Quem conta é o anfitrião
    /// (<see cref="InventarioJogador"/>.Habilidades.cs).
    /// </summary>
    public static class Habilidades
    {
        public const int NivelMaximo = 30;
        public const int Quantas = 45;
        public const int PorRamo = 5;

        /// <summary>O XP de um nível para o seguinte: 50 do 1 para o 2, e 10 a mais a cada nível (330 do 29 para o 30).</summary>
        public static int XpDoNivel(int nivel) => 40 + 10 * Mathf.Max(1, nivel);

        /// <summary>O XP somado para chegar ao nível (o 1 é 0).</summary>
        public static int XpAte(int nivel)
        {
            int soma = 0;
            for (int n = 1; n < nivel; n++) soma += XpDoNivel(n);
            return soma;
        }

        public static int NivelDe(int xp)
        {
            int nivel = 1;
            while (nivel < NivelMaximo && xp >= XpAte(nivel + 1)) nivel++;
            return nivel;
        }

        public static RamoDaHabilidade RamoDe(Habilidade h) => (RamoDaHabilidade)((int)h / PorRamo);
        public static AreaDaHabilidade AreaDe(RamoDaHabilidade r) => (AreaDaHabilidade)((int)r / 3);
        /// <summary>A posição no ramo: 0 a 3, e 4 a ponta.</summary>
        public static int OrdemNoRamo(Habilidade h) => (int)h % PorRamo;
        public static bool EhPonta(Habilidade h) => OrdemNoRamo(h) == PorRamo - 1;
        public static Habilidade Primeira(RamoDaHabilidade r) => (Habilidade)((int)r * PorRamo);

        /// <summary>Pode aprender agora? (tem ponto, ainda não tem, e já tem a anterior do ramo)</summary>
        public static bool PodeAprender(in ProgressoDoPersonagem p, Habilidade h, out string motivo)
        {
            if ((int)h >= Quantas) { motivo = ""; return false; }
            if (p.Tem(h)) { motivo = "Já aprendida."; return false; }
            int ordem = OrdemNoRamo(h);
            if (ordem > 0 && !p.Tem((Habilidade)((int)h - 1)))
            {
                motivo = $"Primeiro: {Nome((Habilidade)((int)h - 1))}.";
                return false;
            }
            if (p.Pontos <= 0) { motivo = "Sem pontos: suba de nível."; return false; }
            motivo = null;
            return true;
        }

        // ------------------------------------------------------------------ os textos

        public static string Nome(AreaDaHabilidade a) => a switch
        {
            AreaDaHabilidade.Combate => "COMBATE", AreaDaHabilidade.Resistencia => "RESISTÊNCIA", _ => "SOBREVIVÊNCIA",
        };

        public static string Nome(RamoDaHabilidade r) => r switch
        {
            RamoDaHabilidade.Guerreiro => "Guerreiro", RamoDaHabilidade.Assassino => "Assassino", RamoDaHabilidade.Ladrao => "Ladrão",
            RamoDaHabilidade.Saude => "Saúde", RamoDaHabilidade.FomeESede => "Fome e sede", RamoDaHabilidade.Recursos => "Recursos",
            RamoDaHabilidade.Craft => "Craft", RamoDaHabilidade.Sortudo => "Sortudo", _ => "Pesca",
        };

        public static string Nome(Habilidade h) => Dados[(int)h].nome;
        public static string Descricao(Habilidade h) => Dados[(int)h].descricao;
        /// <summary>O efeito já existe no jogo? (as outras aparecem como "em breve": dá para aprender, o efeito vem depois)</summary>
        public static bool Pronta(Habilidade h) => Dados[(int)h].pronta;

        readonly struct Dado
        {
            public readonly string nome, descricao;
            public readonly bool pronta;
            public Dado(string nome, string descricao, bool pronta = true)
            {
                this.nome = nome;
                this.descricao = descricao;
                this.pronta = pronta;
            }
        }

        static readonly Dado[] Dados =
        {
            // o Guerreiro
            new("Tiro forte", "+10% de dano com arma de fogo."),
            new("Recarga rápida", "Recarrega 15% mais rápido."),
            new("Braço forte", "+15% de dano com arma branca."),
            new("Mão firme", "25% menos coice e menos tremor na mira."),
            new("Mira de aço", "A mira não treme (nem com dor, pânico, abstinência ou braço machucado)."),
            // o Assassino
            new("Passos leves", "Os zumbis ouvem você correndo (e pisando nas poças) pela metade da distância."),
            new("Pelas costas", "O golpe de arma branca pelas costas mata o Oco na hora (se ele não estiver caçando você)."),
            new("Discreto", "Os sobreviventes enxergam você 30% menos longe e ouvem os seus passos pela metade."),
            new("Tiro abafado", "O barulho do seu tiro chega 30% menos longe."),
            new("Sangue frio", "Matar não abala a mente."),
            // o Ladrão
            new("Borracheiro", "Remenda o pneu furado com o kit de reparo (só quem tem) e anda mais rápido carregando o pneu."),
            new("Mãos leves", "Arromba 30% mais rápido; o pé de cabra não chama os zumbis e a gazua nunca quebra."),
            new("Chaveiro", "Abre as portas e as janelas trancadas com um grampo (sem ferramenta e sem barulho), em 6 s."),
            new("Alarme mudo", "Abrir o capô e mexer nos pneus quase nunca dispara o alarme do carro."),
            new("Ligação direta", "Liga os carros sem a chave (o mini-game dos fios). Sem ela, o carro só liga com a chave."),
            // a Saúde
            new("Mãos de enfermeiro", "Os curativos levam 25% menos tempo."),
            new("Curativo bem feito", "A bandagem e a atadura devolvem +10% à parte."),
            new("Imunidade", "A ferida infecciona com 40% menos chance."),
            new("Nervos de aço", "A mente cai 30% menos."),
            new("Médico de campo", "A bandagem comum protege como a antisséptica, e o kit médico leva metade do tempo."),
            // a Fome e sede
            new("Estômago forte", "A fome cai 15% mais devagar."),
            new("Cantil", "A sede cai 15% mais devagar."),
            new("Fôlego", "Correr gasta menos fome e sede."),
            new("Bem nutrido", "Comer e beber enchem 25% mais."),
            new("Camelo", "A fome e a sede caem 30% mais devagar (no lugar dos 15%)."),
            // os Recursos
            new("Cuidadoso", "As armas se desgastam 20% menos."),
            new("Econômico", "As pilhas da lanterna e a gasolina duram 25% mais."),
            new("Reparador", "30% de chance de o kit de reparo não ser gasto (no conserto da arma e no remendo do pneu)."),
            new("Farmacêutico", "O analgésico e o xarope agem 50% mais tempo."),
            new("Remendão", "As suas armas nunca emperram (só param quando quebram)."),
            // o Craft
            new("Faz-tudo", "Destrava na bancada: a gazua, a faca e a mochila média."),
            new("Mãos hábeis", "Ao criar, 25% de chance de sair em dobro."),
            new("Artesão", "Destrava na mesa de oficina (no bunker): o machado, o pé de cabra, a mochila grande e a lanterna."),
            new("Aproveitador", "Ao criar, 20% de chance de devolver um material."),
            new("Armeiro", "Na mesa de oficina: munição de todos os calibres, com a pólvora."),
            // o Sortudo
            new("Olho bom", "As pilhas achadas nas casas vêm com 25% a mais, e às vezes (10%) acha mais um item comum."),
            new("Garimpeiro", "Pegando um item das casas, às vezes (8%) acha também um incomum."),
            new("Abutre", "Quem você mata deixa mais coisas na mochila (comida, remédio, munição)."),
            new("Pé de coelho", "Pegando um item das casas, às vezes (4%) acha também um raro."),
            new("Faro", "Os itens raros a até 15 m aparecem marcados na tela (só para você), até dentro das gavetas."),
            // a Pesca
            new("Paciência", "O peixe morde 40% mais rápido."),
            new("Isca fina", "40% de chance de não gastar a isca."),
            new("Peixe grande", "Mais tucunarés e tilápias, menos lambaris."),
            new("Noite de pesca", "A noite e a chuva ajudam a pescar (sem ela, atrapalham)."),
            new("Mestre pescador", "Às vezes (6%) morde o dourado, raro e caro."),
        };

        // ------------------------------------------------------------------ o XP de cada ação (o anfitrião dá)

        public const int XpDoOco = 2, XpDoCorredor = 3, XpDoLong = 5, XpDoBrutamonte = 15, XpDaEstatua = 1;
        public const int XpDoSobrevivente = 10, XpDoMilitar = 20;
        public const int XpDoDia = 25;
        public const int XpDoCurativo = 3, XpDoCurativoNoAmigo = 4, XpDaInfeccaoCurada = 10;
        public const int XpDaLigacaoDireta = 8;
        public const int XpDaPesca = 4, XpDoDourado = 12;
        public const int XpDaArvore = 3;

        /// <summary>O XP de criar: 1 mais metade do que a receita gasta (até 6).</summary>
        public static int XpDaReceita(Loot.Receita r)
        {
            int gasta = 0;
            if (r?.gasta != null) foreach (var g in r.gasta) gasta += g.qtd;
            return Mathf.Clamp(1 + gasta / 2, 1, 6);
        }

        /// <summary>
        /// Quanto leva o curativo (s): o do item (<see cref="PartesDoCorpo.TempoDe"/>), 25% menos com as Mãos de
        /// enfermeiro, e o kit na metade com o Médico de campo (de quem trata).
        /// </summary>
        public static float TempoDoCurativo(Loot.Item item, JogadorRede quemTrata)
        {
            float t = PartesDoCorpo.TempoDe(item);
            if (Tem(quemTrata, Habilidade.MaosDeEnfermeiro)) t *= 0.75f;
            if (item == Loot.Item.KitMedico && Tem(quemTrata, Habilidade.MedicoDeCampo)) t *= 0.5f;
            return t;
        }

        // ------------------------------------------------------------------ quem tem

        /// <summary>O jogador tem a habilidade? (em qualquer máquina: o progresso vai pela rede)</summary>
        public static bool Tem(JogadorRede j, Habilidade h) => j != null && j.Inventario != null && j.Inventario.Progresso.Value.Tem(h);

        /// <summary>O jogador local tem a habilidade?</summary>
        public static bool Tem(Habilidade h) => Tem(JogadorRede.Local, h);
    }
}
