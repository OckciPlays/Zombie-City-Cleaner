using System;
using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>As 3 áreas da árvore de habilidades (06/11/2026); cada uma é uma página da aba K.</summary>
    public enum AreaDaHabilidade : byte { Combate, Resistencia, Sobrevivencia }

    /// <summary>
    /// Os 13 ramos da árvore nova (09/11/2026), na ordem das páginas: o Combate (Guerreiro, Armeiro de sucata, Assassino,
    /// Ladrão), a Resistência (Fogo, Saúde, Fome e sede, Recursos, Proteção de sucata) e a Sobrevivência (Craft,
    /// Armadilhas e defesas, Sortudo, Pesca). Os ramos novos (<see cref="Habilidades.RamoNovo"/>) ainda são "em breve", menos
    /// o Armeiro de sucata (as armas, 09/11/2026, e as melhorias, 10/11/2026) e a Proteção de sucata (11/11/2026, a etapa 3).
    /// </summary>
    public enum RamoDaHabilidade : byte
    {
        Guerreiro, ArmeiroDeSucata, Assassino, Ladrao,
        Fogo, Saude, FomeESede, Recursos, ProtecaoDeSucata,
        Craft, Armadilhas, Sortudo, Pesca,
    }

    /// <summary>
    /// Os 74 nós da árvore nova (09/11/2026), na ordem dos ramos. O número é o lugar dos 2 bits do nó no
    /// <see cref="ProgressoDoPersonagem"/> (os pontos dele, de 0 a 3): não mudar a ordem (vai no save e na rede).
    /// </summary>
    public enum Habilidade : byte
    {
        // Combate: o Guerreiro
        TiroForte, RecargaRapida, BracoForte, MaoFirme, MiraDeAco,
        // Combate: o Armeiro de sucata (as três armas valem desde 09/11/2026; as melhorias, em breve)
        PistolaDeSucata, RifleDeSucata, SubmetralhadoraDeSucata, CanoReforcado, MirasCaseiras, CarregadorEstendido,
        LunetaDeCano, MolaNova, SilenciadorCaseiro, MestreArmeiro,
        // Combate: o Assassino
        PassosLeves, PelasCostas, Discreto, TiroAbafado, SangueFrio,
        // Combate: o Ladrão
        Borracheiro, MaosLeves, Chaveiro, AlarmeMudo, LigacaoDireta,
        // Resistência: o Fogo (desde 11/11/2026, a etapa 4)
        TochaDuradoura, Lampiao, CoquetelMolotov, FogueiraPortatil, FogoQuePega, CalorDoFogo, MuralhaDeFogo,
        // Resistência: a Saúde
        MaosDeEnfermeiro, CurativoBemFeito, Imunidade, NervosDeAco, MedicoDeCampo,
        // Resistência: a Fome e sede
        EstomagoForte, Cantil, Folego, BemNutrido, Camelo,
        // Resistência: os Recursos
        Cuidadoso, Economico, Farmaceutico, Reparador, Remendao,
        // Resistência: a Proteção de sucata (desde 11/11/2026, a etapa 3)
        CapaceteDePanela, BracadeirasECaneleiras, ColeteDeChapa, RemendoDeChapa, Couraca,
        // Sobrevivência: o Craft (o Sucateiro vale desde 09/11/2026, a sucata da 2ª leva)
        Sucateiro, FazTudo, Artesao, MaosHabeis, Armeiro,
        // Sobrevivência: as Armadilhas e defesas (desde 11/11/2026, a etapa 5a; a Barricada de tábuas e a Fortaleza, na 5b)
        AlarmeDeLatas, BarricadaDeTabuas, TapeteDePregos, Estacas, Armadilheiro, CercaDeFogo, Fortaleza,
        // Sobrevivência: o Sortudo
        OlhoBom, Garimpeiro, Abutre, PeDeCoelho, Faro,
        // Sobrevivência: a Pesca
        Paciencia, IscaFina, PeixeGrande, NoiteDePesca, MestrePescador,
    }

    /// <summary>
    /// O progresso de um personagem na árvore: o XP somado (o nível sai dele) e os pontos de cada nó (2 bits por
    /// <see cref="Habilidade"/>, de 0 a 3; 32 nós em cada ulong). Vai pela rede (<see cref="InventarioJogador.Progresso"/>)
    /// e no save do personagem.
    /// </summary>
    [Serializable]
    public struct ProgressoDoPersonagem : INetworkSerializeByMemcpy, IEquatable<ProgressoDoPersonagem>
    {
        public int xp;
        /// <summary>
        /// A árvore velha (06/11 a 08/11/2026): um bit por habilidade aprendida. Só serve à migração: num save de antes da
        /// árvore nova vem preenchido, e o anfitrião devolve os pontos (<see cref="Habilidades.Migrar"/>). Na nova, fica 0.
        /// </summary>
        public ulong aprendidas;
        /// <summary>Os pontos de cada nó da árvore nova (09/11/2026): 2 bits por nó, os nós 0 a 31, 32 a 63 e 64 a 73.</summary>
        public ulong niveis0, niveis1, niveis2;
        /// <summary>A árvore mudou e os pontos voltaram: a aba K mostra o aviso uma vez (e o anfitrião apaga).</summary>
        public bool avisoDaArvore;

        /// <summary>Quantos pontos ele tem no nó (0 a 3).</summary>
        public int PontosEm(Habilidade h)
        {
            int i = (int)h;
            if (i < 0 || i >= Habilidades.Quantas) return 0;
            ulong w = i < 32 ? niveis0 : i < 64 ? niveis1 : niveis2;
            return (int)((w >> (2 * (i & 31))) & 3UL);
        }

        /// <summary>Põe os pontos do nó (0 a 3).</summary>
        public void PorPontos(Habilidade h, int n)
        {
            int i = (int)h;
            if (i < 0 || i >= Habilidades.Quantas) return;
            int b = 2 * (i & 31);
            ulong mascara = ~(3UL << b), valor = (ulong)Mathf.Clamp(n, 0, 3) << b;
            if (i < 32) niveis0 = (niveis0 & mascara) | valor;
            else if (i < 64) niveis1 = (niveis1 & mascara) | valor;
            else niveis2 = (niveis2 & mascara) | valor;
        }

        /// <summary>Tem pelo menos 1 ponto no nó? (os efeitos de liga e desliga valem com 1)</summary>
        public bool Tem(Habilidade h) => PontosEm(h) > 0;
        public int Nivel => Habilidades.NivelDe(xp);
        /// <summary>Os pontos gastos na árvore toda.</summary>
        public int Gastos => Somar(niveis0) + Somar(niveis1) + Somar(niveis2);
        /// <summary>Os pontos livres: um por nível (o nível 1 já dá o primeiro), menos os gastos.</summary>
        public int Pontos => Mathf.Max(0, Nivel - Gastos);
        /// <summary>Esquece a árvore toda (os pontos voltam).</summary>
        public void Esquecer() => niveis0 = niveis1 = niveis2 = 0UL;

        // a soma dos campos de 2 bits: os bits baixos contam 1, os altos 2
        static int Somar(ulong w)
        {
            const ulong Baixos = 0x5555555555555555UL;
            return Bits(w & Baixos) + 2 * Bits((w >> 1) & Baixos);
        }

        static int Bits(ulong w)
        {
            int n = 0;
            for (; w != 0; w &= w - 1) n++;
            return n;
        }

        public bool Equals(ProgressoDoPersonagem o) => xp == o.xp && aprendidas == o.aprendidas && niveis0 == o.niveis0
                                                       && niveis1 == o.niveis1 && niveis2 == o.niveis2 && avisoDaArvore == o.avisoDaArvore;
        public override bool Equals(object o) => o is ProgressoDoPersonagem p && Equals(p);
        public override int GetHashCode() => xp ^ aprendidas.GetHashCode() ^ (niveis0.GetHashCode() * 3) ^ (niveis1.GetHashCode() * 7)
                                             ^ (niveis2.GetHashCode() * 13) ^ (avisoDaArvore ? 1 << 30 : 0);
    }

    /// <summary>
    /// A árvore de habilidades (06/11/2026, a pedido: "um sistema de XP que você melhoraria suas skill"; refeita em
    /// 09/11/2026, a etapa 1a da árvore nova). O XP vem das ações (matar, mais um dia vivo, tratar, criar, saquear, a
    /// ligação direta, pescar, cortar árvores), num total só; cada nível dá 1 ponto livre. Na árvore nova, cada nó tem de 1
    /// a 3 níveis (cada ponto é um passo, e o efeito cresce com ele), as áreas têm 5 fileiras (a fileira R pede
    /// <see cref="PontosDaFileira"/> pontos gastos nas de cima da mesma área; a 5 são as pontas), e cada nó pede os
    /// pré-requisitos dele com pelo menos 1 ponto. Os nós dos ramos novos ainda são "em breve" (não dá para aprender), menos
    /// o Armeiro de sucata (09 e 10/11/2026) e a Proteção de sucata (11/11/2026). São
    /// 40 níveis: não dá para pegar tudo. Morrer não tira nada (por enquanto). Quem conta é o anfitrião
    /// (<see cref="InventarioJogador"/>.Habilidades.cs).
    /// </summary>
    public static class Habilidades
    {
        public const int NivelMaximo = 40;
        public const int Quantas = 74;
        public const int Fileiras = 5;

        /// <summary>Os pontos que a fileira pede, gastos nas fileiras de cima da mesma área (o índice é a fileira, 1 a 5).</summary>
        public static readonly int[] PontosDaFileira = { 0, 0, 3, 7, 12, 18 };

        /// <summary>
        /// O XP de um nível para o seguinte: 50 do 1 para o 2 e 10 a mais a cada nível até o 11 (150 do 11 para o 12, como
        /// na árvore de antes); dali, 5 a mais a cada nível (290 do 39 para o 40). O nível 40 pede 7.330 no total (umas 16 h
        /// de jogo; o 30 pede 4.655).
        /// </summary>
        public static int XpDoNivel(int nivel)
        {
            int n = Mathf.Max(1, nivel);
            return n <= 11 ? 40 + 10 * n : 150 + 5 * (n - 11);
        }

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

        // ------------------------------------------------------------------ os nós

        static bool Valida(Habilidade h) => (int)h >= 0 && (int)h < Quantas;
        public static RamoDaHabilidade RamoDe(Habilidade h) => Nos[(int)h].ramo;
        public static AreaDaHabilidade AreaDe(RamoDaHabilidade r) =>
            r <= RamoDaHabilidade.Ladrao ? AreaDaHabilidade.Combate : r <= RamoDaHabilidade.ProtecaoDeSucata ? AreaDaHabilidade.Resistencia : AreaDaHabilidade.Sobrevivencia;
        public static AreaDaHabilidade AreaDe(Habilidade h) => AreaDe(RamoDe(h));
        /// <summary>A fileira do nó (1 a 5; a 5 são as pontas).</summary>
        public static int Fileira(Habilidade h) => Nos[(int)h].fileira;
        /// <summary>A coluna do nó na página da área (0 a 5; o meio, 0,5, fica entre duas).</summary>
        public static float Coluna(Habilidade h) => Nos[(int)h].coluna;
        /// <summary>Quantos níveis o nó tem (1 a 3).</summary>
        public static int Maximo(Habilidade h) => Nos[(int)h].maximo;
        public static bool EhPonta(Habilidade h) => Nos[(int)h].fileira == Fileiras;
        /// <summary>O efeito já existe no jogo? (os outros são "em breve": aparecem, mas ainda não dá para aprender)</summary>
        public static bool Pronta(Habilidade h) => Nos[(int)h].pronta;
        /// <summary>Os nós que este pede (cada um com pelo menos 1 ponto; alguns são de outro ramo).</summary>
        public static Habilidade[] Pede(Habilidade h) => Nos[(int)h].pede;
        public static string Nome(Habilidade h) => Nos[(int)h].nome;
        /// <summary>O efeito com k pontos (1 ao máximo).</summary>
        public static string Efeito(Habilidade h, int k) => Nos[(int)h].efeito(Mathf.Clamp(k, 1, Maximo(h)));
        /// <summary>O efeito no nível máximo.</summary>
        public static string Descricao(Habilidade h) => Efeito(h, Maximo(h));

        /// <summary>Quantas colunas a página da área tem.</summary>
        public static int Colunas(AreaDaHabilidade a) => a == AreaDaHabilidade.Sobrevivencia ? 5 : 6;
        /// <summary>Os ramos da área, na ordem das colunas.</summary>
        public static RamoDaHabilidade[] Ramos(AreaDaHabilidade a) => a == AreaDaHabilidade.Combate ? RamosDoCombate
            : a == AreaDaHabilidade.Resistencia ? RamosDaResistencia : RamosDaSobrevivencia;
        static readonly RamoDaHabilidade[] RamosDoCombate = { RamoDaHabilidade.Guerreiro, RamoDaHabilidade.ArmeiroDeSucata, RamoDaHabilidade.Assassino, RamoDaHabilidade.Ladrao };
        static readonly RamoDaHabilidade[] RamosDaResistencia = { RamoDaHabilidade.Fogo, RamoDaHabilidade.Saude, RamoDaHabilidade.FomeESede, RamoDaHabilidade.Recursos, RamoDaHabilidade.ProtecaoDeSucata };
        static readonly RamoDaHabilidade[] RamosDaSobrevivencia = { RamoDaHabilidade.Craft, RamoDaHabilidade.Armadilhas, RamoDaHabilidade.Sortudo, RamoDaHabilidade.Pesca };

        /// <summary>Os ramos novos da árvore de 09/11/2026 (o selo "novo"; as Armadilhas inteiras desde 11/11/2026, a etapa 5b).</summary>
        public static bool RamoNovo(RamoDaHabilidade r) => r == RamoDaHabilidade.ArmeiroDeSucata || r == RamoDaHabilidade.Fogo
                                                         || r == RamoDaHabilidade.ProtecaoDeSucata || r == RamoDaHabilidade.Armadilhas;

        /// <summary>As colunas que o ramo ocupa (a primeira e a última).</summary>
        public static void ColunasDoRamo(RamoDaHabilidade r, out float de, out float ate)
        {
            de = 99f;
            ate = -1f;
            for (int i = 0; i < Quantas; i++)
                if (Nos[i].ramo == r)
                {
                    de = Mathf.Min(de, Nos[i].coluna);
                    ate = Mathf.Max(ate, Nos[i].coluna);
                }
        }

        /// <summary>O ícone do ramo na aba K: uma letra ou um símbolo simples.</summary>
        public static string Simbolo(RamoDaHabilidade r) => r switch
        {
            RamoDaHabilidade.Guerreiro => "G", RamoDaHabilidade.ArmeiroDeSucata => "A", RamoDaHabilidade.Assassino => "†", RamoDaHabilidade.Ladrao => "L",
            RamoDaHabilidade.Fogo => "▲", RamoDaHabilidade.Saude => "+", RamoDaHabilidade.FomeESede => "F", RamoDaHabilidade.Recursos => "R",
            RamoDaHabilidade.ProtecaoDeSucata => "P", RamoDaHabilidade.Craft => "C", RamoDaHabilidade.Armadilhas => "A", RamoDaHabilidade.Sortudo => "♣",
            _ => "P",
        };

        // ------------------------------------------------------------------ aprender e tirar

        /// <summary>Os pontos gastos na área, nas fileiras de cima da fileira dada (as que contam para abri-la).</summary>
        public static int GastosAcima(in ProgressoDoPersonagem p, AreaDaHabilidade area, int fileira)
        {
            int soma = 0;
            for (int i = 0; i < Quantas; i++)
                if (Nos[i].fileira < fileira && AreaDe(Nos[i].ramo) == area) soma += p.PontosEm((Habilidade)i);
            return soma;
        }

        /// <summary>A fileira já abriu? (os pontos gastos nas de cima da mesma área)</summary>
        public static bool FileiraAberta(in ProgressoDoPersonagem p, AreaDaHabilidade area, int fileira) =>
            GastosAcima(p, area, fileira) >= PontosDaFileira[Mathf.Clamp(fileira, 1, Fileiras)];

        /// <summary>
        /// Pode pôr mais um ponto agora? (não é "em breve", ainda não está no máximo, os pré-requisitos têm ponto, a fileira
        /// abriu e tem ponto livre). O motivo diz tudo o que falta, uma coisa por linha.
        /// </summary>
        public static bool PodeAprender(in ProgressoDoPersonagem p, Habilidade h, out string motivo)
        {
            motivo = null;
            if (!Valida(h)) { motivo = ""; return false; }
            if (!Pronta(h)) { motivo = "Em breve: chega numa próxima etapa."; return false; }
            if (p.PontosEm(h) >= Maximo(h)) { motivo = "Já está no nível máximo."; return false; }
            string falta = null;
            var area = AreaDe(h);
            int fileira = Fileira(h), precisa = PontosDaFileira[fileira], gastos = GastosAcima(p, area, fileira);
            if (gastos < precisa)
                falta = $"A fileira {fileira} pede {precisa} pontos nas de cima ({Nome(area).ToLowerInvariant()}: {gastos} de {precisa}).";
            string pede = null;
            // (um pedido "em breve" não segura, 11/11/2026: as Estacas pedem a Barricada de tábuas, que só vem na etapa 5b; o
            // pedido volta a valer quando ela ficar pronta)
            foreach (var q in Pede(h))
                if (!p.Tem(q) && Pronta(q)) pede = pede == null ? Nome(q) : $"{pede}, {Nome(q)}";
            if (pede != null) falta = (falta == null ? "" : falta + "\n") + $"Primeiro: {pede}.";
            if (p.Pontos <= 0) falta = (falta == null ? "" : falta + "\n") + "Sem pontos livres: suba de nível.";
            motivo = falta;
            return falta == null;
        }

        /// <summary>
        /// Pode tirar um ponto? (tem ponto, e nada aprendido depende dele: nem um nó que o pede, com o último ponto, nem as
        /// fileiras de baixo da mesma área, que precisam dos pontos gastos acima delas)
        /// </summary>
        public static bool PodeTirar(in ProgressoDoPersonagem p, Habilidade h, out string motivo)
        {
            motivo = null;
            if (!Valida(h)) { motivo = ""; return false; }
            int k = p.PontosEm(h);
            if (k <= 0) { motivo = "Nenhum ponto nela."; return false; }
            if (k == 1)
                for (int i = 0; i < Quantas; i++)
                {
                    var outra = (Habilidade)i;
                    if (!p.Tem(outra)) continue;
                    foreach (var q in Pede(outra))
                        if (q == h) { motivo = $"{Nome(outra)} depende dela."; return false; }
                }
            var area = AreaDe(h);
            int fileira = Fileira(h);
            for (int f = fileira + 1; f <= Fileiras; f++)
            {
                bool usada = false;
                for (int i = 0; i < Quantas && !usada; i++)
                    usada = Nos[i].fileira == f && AreaDe(Nos[i].ramo) == area && p.Tem((Habilidade)i);
                if (usada && GastosAcima(p, area, f) - 1 < PontosDaFileira[f])
                {
                    motivo = $"A fileira {f} precisa destes pontos ({PontosDaFileira[f]} nas de cima).";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// A migração da árvore velha (09/11/2026): quem tinha habilidades aprendidas recebe todos os pontos de volta (o XP e
        /// o nível ficam) e vê o aviso uma vez. Devolve se mudou.
        /// </summary>
        public static bool Migrar(ref ProgressoDoPersonagem p)
        {
            if (p.aprendidas == 0UL) return false;
            p.aprendidas = 0UL;
            p.Esquecer();
            p.avisoDaArvore = true;
            return true;
        }

        public const string AvisoDaArvore = "A árvore de habilidades mudou: os seus pontos voltaram. Distribua de novo.";

        // ------------------------------------------------------------------ os textos

        public static string Nome(AreaDaHabilidade a) => a switch
        {
            AreaDaHabilidade.Combate => "COMBATE", AreaDaHabilidade.Resistencia => "RESISTÊNCIA", _ => "SOBREVIVÊNCIA",
        };

        public static string Nome(RamoDaHabilidade r) => r switch
        {
            RamoDaHabilidade.Guerreiro => "Guerreiro", RamoDaHabilidade.ArmeiroDeSucata => "Armeiro de sucata",
            RamoDaHabilidade.Assassino => "Assassino", RamoDaHabilidade.Ladrao => "Ladrão",
            RamoDaHabilidade.Fogo => "Fogo", RamoDaHabilidade.Saude => "Saúde", RamoDaHabilidade.FomeESede => "Fome e sede",
            RamoDaHabilidade.Recursos => "Recursos", RamoDaHabilidade.ProtecaoDeSucata => "Proteção de sucata",
            RamoDaHabilidade.Craft => "Craft", RamoDaHabilidade.Armadilhas => "Armadilhas e defesas", RamoDaHabilidade.Sortudo => "Sortudo",
            _ => "Pesca",
        };

        readonly struct No
        {
            public readonly RamoDaHabilidade ramo;
            public readonly float coluna;
            public readonly byte fileira, maximo;
            public readonly string nome;
            public readonly Func<int, string> efeito;
            public readonly Habilidade[] pede;
            public readonly bool pronta;

            public No(RamoDaHabilidade ramo, float coluna, int fileira, int maximo, string nome, Func<int, string> efeito, bool pronta, Habilidade[] pede)
            {
                this.ramo = ramo;
                this.coluna = coluna;
                this.fileira = (byte)fileira;
                this.maximo = (byte)maximo;
                this.nome = nome;
                this.efeito = efeito;
                this.pronta = pronta;
                this.pede = pede ?? Array.Empty<Habilidade>();
            }
        }

        // um nó que já tem efeito (o texto muda com os pontos) e um "em breve" (o texto é um só)
        static No E(RamoDaHabilidade r, float c, int f, int max, string nome, Func<int, string> efeito, params Habilidade[] pede) =>
            new No(r, c, f, max, nome, efeito, true, pede);
        static No B(RamoDaHabilidade r, float c, int f, int max, string nome, string efeito, params Habilidade[] pede) =>
            new No(r, c, f, max, nome, _ => efeito, false, pede);

        const string DepoisVem = "O efeito vem numa próxima etapa.";
        const RamoDaHabilidade Gue = RamoDaHabilidade.Guerreiro, Arm = RamoDaHabilidade.ArmeiroDeSucata, Ass = RamoDaHabilidade.Assassino,
            Lad = RamoDaHabilidade.Ladrao, Fog = RamoDaHabilidade.Fogo, Sau = RamoDaHabilidade.Saude, Fom = RamoDaHabilidade.FomeESede,
            Rec = RamoDaHabilidade.Recursos, Pro = RamoDaHabilidade.ProtecaoDeSucata, Cra = RamoDaHabilidade.Craft,
            Arma = RamoDaHabilidade.Armadilhas, Sor = RamoDaHabilidade.Sortudo, Pes = RamoDaHabilidade.Pesca;

        // a ordem é a do enum Habilidade: (o ramo, a coluna, a fileira, os níveis, o nome, o efeito, o que pede)
        static readonly No[] Nos =
        {
            // o Guerreiro
            E(Gue, 0, 1, 3, "Tiro forte", k => $"+{4 * k}% de dano com arma de fogo."),
            E(Gue, 0, 2, 2, "Recarga rápida", k => $"Recarrega {8 * k}% mais rápido (a animação dos braços também).", Habilidade.TiroForte),
            E(Gue, 0, 3, 3, "Braço forte", k => $"+{5 * k}% de dano com arma branca.", Habilidade.RecargaRapida),
            E(Gue, 0, 4, 2, "Mão firme", k => $"{12 * k}% menos coice e menos tremor na mira.", Habilidade.BracoForte),
            E(Gue, 0, 5, 1, "Mira de aço", _ => "A mira não treme (nem com dor, pânico, abstinência ou braço machucado).", Habilidade.MaoFirme),
            // o Armeiro de sucata: as três armas (09/11/2026, as receitas no Loot/Receitas.cs) e, desde 10/11/2026 (a etapa 2), as
            // melhorias (os números no Combat/Melhorias.cs; postas na bancada, no painel MELHORIAS da aba Criar)
            // (um nível só desde 10/11/2026: o segundo não fazia nada; o ponto de quem tinha dois volta, no DefinirProgresso)
            E(Arm, 2, 1, 1, "Pistola de sucata", _ => "Destrava a pistola de sucata (um tiro) e a munição de prego."),
            E(Arm, 1, 2, 1, "Rifle de precisão de sucata", _ => "Destrava o rifle de sucata e a munição de parafuso.", Habilidade.PistolaDeSucata),
            E(Arm, 3, 2, 1, "Submetralhadora de sucata", _ => "Destrava a submetralhadora de sucata.", Habilidade.PistolaDeSucata),
            E(Arm, 1, 3, 3, "Cano reforçado", k => k == 1 ? "O cano reforçado I (na bancada): +8% de dano, e a arma gasta 10% mais."
                : $"O cano reforçado {(k == 2 ? "II" : "III")} (na mesa de oficina): +{8 * k}% de dano, e a arma gasta {10 * k}% mais.", Habilidade.RifleDeSucata),
            E(Arm, 2, 3, 2, "Miras caseiras", k => k == 1 ? "A mira de ferro ajustada (na bancada): o tiro espalha 15% menos."
                : "E a mira de ponto caseira (na mesa de oficina): mirando, o olho vai no pontinho vermelho e o tiro espalha 30% menos.", Habilidade.PistolaDeSucata),
            E(Arm, 3, 3, 1, "Carregador estendido", _ => "O carregador estendido (na bancada): a pistola de sucata leva 3 tiros, e a submetralhadora, 32.", Habilidade.SubmetralhadoraDeSucata),
            E(Arm, 1, 4, 1, "Luneta de cano", _ => "A luneta de cano, só no rifle de sucata (na mesa de oficina): mirando, aproxima 2,5 vezes e o tiro espalha 60% menos.", Habilidade.CanoReforcado, Habilidade.MaoFirme),
            E(Arm, 2, 4, 2, "Mola nova", k => $"As armas de sucata emperram {25 * k}% menos.", Habilidade.MirasCaseiras),
            E(Arm, 3, 4, 2, "Silenciador caseiro", k => k == 1 ? "O silenciador de garrafa PET (na bancada): o tiro chega 60% menos longe e tira 5% menos; dura 15 tiros."
                : "E o de filtro de óleo (na mesa de oficina): o tiro chega 50% menos longe e tira 5% menos; dura 80 tiros.", Habilidade.CarregadorEstendido, Habilidade.TiroAbafado),
            E(Arm, 2, 5, 1, "Mestre armeiro", _ => "As armas de sucata batem como as reais, e as receitas do Armeiro de sucata gastam 25% menos sucata.", Habilidade.MolaNova),
            // o Assassino
            E(Ass, 4, 1, 2, "Passos leves", k => $"Os zumbis ouvem os seus passos (correndo e nas poças) {25 * k}% menos longe."),
            E(Ass, 4, 2, 1, "Pelas costas", _ => "O golpe de arma branca pelas costas mata o Oco na hora (se ele não estiver caçando você).", Habilidade.PassosLeves),
            E(Ass, 4, 3, 2, "Discreto", k => $"Os sobreviventes enxergam você {15 * k}% menos longe e ouvem os seus passos {25 * k}% menos longe.", Habilidade.PelasCostas),
            E(Ass, 4, 4, 2, "Tiro abafado", k => $"O barulho do seu tiro chega {15 * k}% menos longe.", Habilidade.Discreto),
            E(Ass, 4, 5, 1, "Sangue frio", _ => "Matar não abala a mente.", Habilidade.TiroAbafado),
            // o Ladrão
            E(Lad, 5, 1, 1, "Borracheiro", _ => "Remenda o pneu furado com o kit de reparo (só quem tem) e anda mais rápido carregando o pneu."),
            E(Lad, 5, 2, 2, "Mãos leves", k => k == 1 ? "Arromba 15% mais rápido."
                : "Arromba 30% mais rápido; o pé de cabra não chama os zumbis e a gazua nunca quebra.", Habilidade.Borracheiro),
            E(Lad, 5, 3, 1, "Chaveiro", _ => "Abre as portas e as janelas trancadas com um grampo (sem ferramenta e sem barulho), em 6 s.", Habilidade.MaosLeves),
            E(Lad, 5, 4, 2, "Alarme mudo", k => $"Abrir o capô e mexer nos pneus disparam o alarme do carro {40 * k}% menos (o tiro ainda dispara).", Habilidade.Chaveiro),
            E(Lad, 5, 5, 1, "Ligação direta", _ => "Liga os carros sem a chave (o mini-game dos fios). Sem ela, o carro só liga com a chave.", Habilidade.AlarmeMudo),
            // o Fogo (11/11/2026, a etapa 4: as receitas no Loot/Receitas.cs, os números no Player/FogoDoJogador.cs)
            E(Fog, 0.5f, 1, 2, "Tocha duradoura", k => $"A tocha dura {FogoDoJogador.TochaDuradouraPorNivel * 100f * k:0}% mais."),
            E(Fog, 0, 2, 1, "Lampião", _ => $"Destrava o lampião (na mesa de oficina): luz quente na mão, os zumbis ficam longe e a chuva não apaga. Gasta combustível ({FogoDoJogador.MinutosDoLampiao:0} min por carga).", Habilidade.TochaDuradoura),
            E(Fog, 1, 2, 1, "Coquetel molotov", _ => $"Destrava o coquetel molotov (na bancada): arremessado, acende o chão por {FogoDoJogador.SegundosDoMolotov:0} s e queima quem estiver ali.", Habilidade.TochaDuradoura),
            E(Fog, 0, 3, 1, "Fogueira portátil", _ => $"Destrava o kit da fogueira portátil (na bancada): queima {FogoDoJogador.MinutosDaFogueiraPortatil:0} min, os zumbis ficam longe, e o E recolhe.", Habilidade.Lampiao),
            E(Fog, 1, 3, 2, "Fogo que pega", k => $"O molotov e a tocha queimam {FogoDoJogador.FogoQuePegaPorNivel * 100f * k:0}% mais tempo, e o fogo do molotov se espalha {FogoDoJogador.AreaQuePegaPorNivel * 100f * k:0}% mais.", Habilidade.CoquetelMolotov),
            E(Fog, 0, 4, 2, "Calor do fogo", k => $"A menos de {FogoDoJogador.DistanciaDoCalor:0} m de um fogo aceso, a mente sobe {FogoDoJogador.CalorPorNivel * k:0} por hora.", Habilidade.FogueiraPortatil, Habilidade.NervosDeAco),
            E(Fog, 0.5f, 5, 1, "Muralha de fogo", _ => $"O seu fogo segura os zumbis {(FogoDoJogador.FatorDaMuralha - 1f) * 100f:0}% mais longe, e eles fogem em pânico.", Habilidade.CalorDoFogo),
            // a Saúde
            E(Sau, 2, 1, 2, "Mãos de enfermeiro", k => $"Os curativos levam {12 * k}% menos tempo."),
            E(Sau, 2, 2, 2, "Curativo bem feito", k => $"A bandagem e a atadura devolvem +{5 * k}% à parte.", Habilidade.MaosDeEnfermeiro),
            E(Sau, 2, 3, 2, "Imunidade", k => $"A ferida infecciona com {20 * k}% menos chance.", Habilidade.CurativoBemFeito),
            E(Sau, 2, 4, 3, "Nervos de aço", k => $"A mente cai {10 * k}% menos.", Habilidade.Imunidade),
            E(Sau, 2, 5, 1, "Médico de campo", _ => "A bandagem comum protege como a antisséptica, e o kit médico leva metade do tempo.", Habilidade.NervosDeAco),
            // a Fome e sede
            E(Fom, 3, 1, 3, "Estômago forte", k => $"A fome cai {5 * k}% mais devagar."),
            E(Fom, 3, 2, 3, "Cantil", k => $"A sede cai {5 * k}% mais devagar.", Habilidade.EstomagoForte),
            E(Fom, 3, 3, 2, "Fôlego", k => $"Correr gasta menos fome e sede ({(k == 1 ? "1,4" : "1,25")} vez, no lugar de 1,6).", Habilidade.Cantil),
            E(Fom, 3, 4, 2, "Bem nutrido", k => $"Comer e beber enchem {12 * k}% mais.", Habilidade.Folego),
            E(Fom, 3, 5, 1, "Camelo", _ => "A fome e a sede caem 30% mais devagar (no lugar do Estômago forte e do Cantil).", Habilidade.BemNutrido),
            // os Recursos
            E(Rec, 4, 1, 2, "Cuidadoso", k => $"As armas se desgastam {10 * k}% menos."),
            E(Rec, 4, 2, 2, "Econômico", k => $"As pilhas da lanterna e a gasolina (de quem dirige) duram {12 * k}% mais.", Habilidade.Cuidadoso),
            E(Rec, 4, 3, 2, "Farmacêutico", k => $"O analgésico e o xarope agem {25 * k}% mais tempo.", Habilidade.Economico),
            E(Rec, 4, 4, 2, "Reparador", k => $"{15 * k}% de chance de o kit de reparo não ser gasto (no conserto da arma e no remendo do pneu).", Habilidade.Farmaceutico),
            E(Rec, 4, 5, 1, "Remendão", _ => "As suas armas nunca emperram (só param quando quebram).", Habilidade.Reparador),
            // a Proteção de sucata (11/11/2026, a etapa 3: as receitas no Loot/Receitas.cs, os números no Player/Protecao.cs; as
            // peças se vestem no grupo PROTEÇÃO da tela)
            E(Pro, 5, 1, 1, "Capacete de panela", _ => $"Destrava o capacete de panela (na bancada): corta {Protecao.CorteDoCapacete * 100f:0}% do golpe na cabeça."),
            E(Pro, 5, 2, 1, "Braçadeiras e caneleiras", _ => $"Destrava as braçadeiras e as caneleiras (na bancada): cortam {Protecao.CorteDasBracadeiras * 100f:0}% do golpe nos braços e nas pernas.", Habilidade.CapaceteDePanela),
            E(Pro, 5, 3, 1, "Colete de chapa", _ => $"Destrava o colete de chapa (na mesa de oficina, com um pneu): corta {Protecao.CorteDoColete * 100f:0}% no tronco, mas pesa e faz barulho.", Habilidade.BracadeirasECaneleiras),
            E(Pro, 5, 4, 2, "Remendo de chapa", k => $"As peças da proteção se gastam {Protecao.RemendoPorNivel * 100f * k:0}% menos (e o kit de reparo conserta).", Habilidade.ColeteDeChapa, Habilidade.Reparador),
            E(Pro, 5, 5, 1, "Couraça", _ => "A mordida numa parte coberta pela proteção não infecciona.", Habilidade.RemendoDeChapa),
            // o Craft (09/11/2026, a sucata da 2ª leva: o Sucateiro vale, e o Faz-tudo pede ele, como na árvore aprovada; um
            // save com o Faz-tudo sem o Sucateiro continua valendo: o pedido só conta para aprender)
            E(Cra, 0, 1, 3, "Sucateiro", k => $"Desmontar (os painéis de metal e os itens) rende +{k} sucata{(k > 1 ? "s" : "")}."),
            E(Cra, 0, 2, 1, "Faz-tudo", _ => "Destrava na bancada: a gazua, a faca e a mochila média.", Habilidade.Sucateiro),
            E(Cra, 0, 3, 1, "Artesão", _ => "Destrava na mesa de oficina (no bunker): o machado, o pé de cabra, a mochila grande e a lanterna.", Habilidade.FazTudo),
            E(Cra, 0, 4, 2, "Mãos hábeis", k => k == 1 ? "Ao criar, 25% de chance de sair em dobro."
                : "Ao criar, 25% de chance de sair em dobro e 20% de devolver um material.", Habilidade.Artesao),
            E(Cra, 0, 5, 1, "Armeiro", _ => "Na mesa de oficina: munição de todos os calibres, com a pólvora.", Habilidade.MaosHabeis),
            // as Armadilhas e defesas (11/11/2026, a etapa 5a: as receitas no Loot/Receitas.cs, os números no
            // Player/ArmadilhasDoJogador.cs; montadas no chão como a barricada). A Barricada de tábuas e a Fortaleza vieram na
            // etapa 5b (11/11/2026: pregada nas portas e nas janelas das casas, Loot/ConstrucoesNoMundo.Barricadas.cs; desde
            // então, as Estacas voltam a pedir a Barricada de tábuas)
            E(Arma, 1.5f, 1, 1, "Alarme de latas", _ => $"Destrava o alarme de latas (na bancada): quem passa em pé pelo barbante faz as latas tocarem; os zumbis a {ArmadilhasDoJogador.BarulhoDoAlarme:0} m vêm ver, e você recebe o aviso a até {ArmadilhasDoJogador.AvisoDoAlarme:0} m. {ArmadilhasDoJogador.ToquesDoAlarme} toques."),
            E(Arma, 1, 2, 2, "Barricada de tábuas", k => k == 1
                ? $"Destrava as tábuas para barricada (na bancada, com a ferramenta: 3 madeiras e 6 pregos). Mirando uma porta ou uma janela de casa, segure E: a barricada fica pregada do seu lado, e ela não abre (dos dois lados). Vida: a porta {ArmadilhasDoJogador.VidaNaPorta:0}, a janela {ArmadilhasDoJogador.VidaNaJanela:0}; os zumbis encostados e o Brutamonte a quebram. Do lado dela, a ferramenta desmonta."
                : $"A barricada de tábuas com {ArmadilhasDoJogador.ReforcoDoNivel2 * 100f:0}% mais vida (a porta {ArmadilhasDoJogador.VidaNaPorta * (1f + ArmadilhasDoJogador.ReforcoDoNivel2):0}, a janela {ArmadilhasDoJogador.VidaNaJanela * (1f + ArmadilhasDoJogador.ReforcoDoNivel2):0}), e o conserto: do lado dela, segure o clique com {ArmadilhasDoJogador.MadeiraDoConserto} madeira, {ArmadilhasDoJogador.PregosDoConserto} pregos e a ferramenta (+{ArmadilhasDoJogador.Conserto * 100f:0}% da vida).", Habilidade.AlarmeDeLatas),
            E(Arma, 2, 2, 1, "Tapete de pregos", _ => $"Destrava o tapete de pregos (na bancada, com a ferramenta): o zumbi que pisa leva {ArmadilhasDoJogador.DanoDoTapete:0} e anda {ArmadilhasDoJogador.LentidaoDosPregos * 100f:0}% mais devagar por {ArmadilhasDoJogador.SegundosLento:0} s; quem pisa sem estar agachado leva {ArmadilhasDoJogador.DanoDoTapeteNoJogador:0} na perna; e o pneu do carro fura. {ArmadilhasDoJogador.UsosDoTapete} usos.", Habilidade.AlarmeDeLatas),
            E(Arma, 1, 3, 1, "Estacas", _ => $"Destrava as estacas (na bancada, com a ferramenta): seguram os zumbis como a barricada, e quem encosta na frente leva {ArmadilhasDoJogador.DanoDasEstacas:0} por segundo. O Brutamonte as quebra, mas leva {ArmadilhasDoJogador.DanoNoBrutamonte:0}.", Habilidade.BarricadaDeTabuas, Habilidade.Artesao),
            E(Arma, 2, 3, 2, "Armadilheiro", k => $"As suas armadilhas têm {ArmadilhasDoJogador.UsosPorNivel * 100f * k:0}% mais usos (e vida) e {ArmadilhasDoJogador.DanoPorNivel * 100f * k:0}% mais dano."
                + (k >= 2 ? " O seu tapete de pregos não fere os jogadores." : ""), Habilidade.TapeteDePregos),
            E(Arma, 1.5f, 4, 1, "Cerca de fogo", _ => $"Destrava a cerca de fogo (na bancada, com 2 combustíveis): um rastro de {ArmadilhasDoJogador.ComprimentoDaCerca:0} m que, aceso (a tocha, o lampião ou outro fogo), queima {ArmadilhasDoJogador.SegundosDaCerca:0} s; os zumbis não atravessam, e quem atravessa queima.", Habilidade.Estacas),
            E(Arma, 1.5f, 5, 1, "Fortaleza", _ => $"As suas defesas (as barricadas e as estacas) aguentam o dobro e se consertam sozinhas: {ArmadilhasDoJogador.ConsertoPorHora * 100f:0}% da vida por hora do jogo, com você a até {ArmadilhasDoJogador.DonoPerto:0} m e nenhum zumbi batendo nelas. O seu alarme de latas avisa de qualquer distância.", Habilidade.CercaDeFogo),
            // o Sortudo
            E(Sor, 3, 1, 2, "Olho bom", k => $"Às vezes ({5 * k}%) acha mais um item comum nas casas, e as pilhas achadas vêm com {(k == 1 ? 12 : 25)}% a mais."),
            E(Sor, 3, 2, 2, "Garimpeiro", k => $"Pegando um item das casas, às vezes ({4 * k}%) acha também um incomum.", Habilidade.OlhoBom),
            E(Sor, 3, 3, 2, "Abutre", k => k == 1 ? "Quem você mata deixa uma ou duas coisas a mais na mochila (comida, remédio)."
                : "Quem você mata deixa mais coisas na mochila (comida, remédio e a munição da arma dele).", Habilidade.Garimpeiro),
            E(Sor, 3, 4, 2, "Pé de coelho", k => $"Pegando um item das casas, às vezes ({2 * k}%) acha também um raro.", Habilidade.Abutre),
            E(Sor, 3, 5, 1, "Faro", _ => "Os itens raros a até 15 m aparecem marcados na tela (só para você), até dentro das gavetas.", Habilidade.PeDeCoelho),
            // a Pesca
            E(Pes, 4, 1, 2, "Paciência", k => $"O peixe morde {20 * k}% mais rápido."),
            E(Pes, 4, 2, 2, "Isca fina", k => $"{20 * k}% de chance de não gastar a isca.", Habilidade.Paciencia),
            E(Pes, 4, 3, 2, "Peixe grande", k => k == 1 ? "Um pouco mais de tucunarés e tilápias, e menos lambaris."
                : "Mais tucunarés e tilápias, e menos lambaris.", Habilidade.IscaFina),
            E(Pes, 4, 4, 1, "Noite de pesca", _ => "A noite e a chuva ajudam a pescar (sem ela, atrapalham).", Habilidade.PeixeGrande),
            E(Pes, 4, 5, 1, "Mestre pescador", _ => "Às vezes (6%) morde o dourado, raro e caro.", Habilidade.NoiteDePesca),
        };

        // ------------------------------------------------------------------ o XP de cada ação (o anfitrião dá)

        public const int XpDoOco = 2, XpDoCorredor = 3, XpDoLong = 5, XpDoBrutamonte = 15, XpDaEstatua = 1;
        public const int XpDoSobrevivente = 10, XpDoMilitar = 20;
        public const int XpDoDia = 25;
        public const int XpDoCurativo = 3, XpDoCurativoNoAmigo = 4, XpDaInfeccaoCurada = 10;
        public const int XpDaLigacaoDireta = 8;
        public const int XpDaPesca = 4, XpDoDourado = 12;
        public const int XpDaArvore = 3;
        /// <summary>Desmontar um painel de metal (09/11/2026, a sucata).</summary>
        public const int XpDoPainel = 1;

        /// <summary>O XP de criar: 1 mais metade do que a receita gasta (até 6).</summary>
        public static int XpDaReceita(Loot.Receita r)
        {
            int gasta = 0;
            if (r?.gasta != null) foreach (var g in r.gasta) gasta += g.qtd;
            return Mathf.Clamp(1 + gasta / 2, 1, 6);
        }

        /// <summary>
        /// Quanto leva o curativo (s): o do item (<see cref="PartesDoCorpo.TempoDe"/>), 12% menos por ponto nas Mãos de
        /// enfermeiro, e o kit na metade com o Médico de campo (de quem trata).
        /// </summary>
        public static float TempoDoCurativo(Loot.Item item, JogadorRede quemTrata)
        {
            float t = PartesDoCorpo.TempoDe(item);
            t *= 1f - 0.12f * Pontos(quemTrata, Habilidade.MaosDeEnfermeiro);
            if (item == Loot.Item.KitMedico && Tem(quemTrata, Habilidade.MedicoDeCampo)) t *= 0.5f;
            return t;
        }

        // ------------------------------------------------------------------ quem tem

        /// <summary>O jogador tem a habilidade (pelo menos 1 ponto)? Em qualquer máquina: o progresso vai pela rede.</summary>
        public static bool Tem(JogadorRede j, Habilidade h) => Pontos(j, h) > 0;

        /// <summary>O jogador local tem a habilidade?</summary>
        public static bool Tem(Habilidade h) => Tem(JogadorRede.Local, h);

        /// <summary>Quantos pontos o jogador tem na habilidade (0 ao máximo dela; o efeito cresce com eles).</summary>
        public static int Pontos(JogadorRede j, Habilidade h) =>
            j != null && j.Inventario != null ? j.Inventario.Progresso.Value.PontosEm(h) : 0;

        /// <summary>Quantos pontos o jogador local tem na habilidade.</summary>
        public static int Pontos(Habilidade h) => Pontos(JogadorRede.Local, h);
    }
}
