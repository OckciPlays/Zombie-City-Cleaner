using System;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>Os 4 lugares da proteção de sucata (11/11/2026, a etapa 3), na ordem do grupo PROTEÇÃO da tela.</summary>
    public enum LugarDaProtecao : byte { Capacete, Colete, Bracadeiras, Caneleiras }

    /// <summary>
    /// A proteção vestida: a peça de cada lugar (Nada: vazio) e o desgaste dela (0, nova; 100, quebrada: não protege). Vai
    /// pela rede inteira (8 bytes), escrita pelo anfitrião (<see cref="InventarioJogador.Protecao"/>).
    /// </summary>
    public struct ProtecaoVestida : INetworkSerializeByMemcpy, IEquatable<ProtecaoVestida>
    {
        public Item capacete, colete, bracadeiras, caneleiras;
        public byte gastoCapacete, gastoColete, gastoBracadeiras, gastoCaneleiras;

        /// <summary>A peça vestida no lugar (Nada: vazio).</summary>
        public Item Peca(LugarDaProtecao l) => l switch
        {
            LugarDaProtecao.Capacete => capacete, LugarDaProtecao.Colete => colete,
            LugarDaProtecao.Bracadeiras => bracadeiras, _ => caneleiras,
        };

        /// <summary>O desgaste da peça vestida no lugar (0, nova; 100, quebrada).</summary>
        public byte Desgaste(LugarDaProtecao l) => l switch
        {
            LugarDaProtecao.Capacete => gastoCapacete, LugarDaProtecao.Colete => gastoColete,
            LugarDaProtecao.Bracadeiras => gastoBracadeiras, _ => gastoCaneleiras,
        };

        /// <summary>Veste (ou tira, com Nada) a peça no lugar, com o desgaste dela.</summary>
        public void Por(LugarDaProtecao l, Item item, int desgaste)
        {
            byte d = (byte)Mathf.Clamp(item == Item.Nada ? 0 : desgaste, 0, 100);
            switch (l)
            {
                case LugarDaProtecao.Capacete: capacete = item; gastoCapacete = d; break;
                case LugarDaProtecao.Colete: colete = item; gastoColete = d; break;
                case LugarDaProtecao.Bracadeiras: bracadeiras = item; gastoBracadeiras = d; break;
                default: caneleiras = item; gastoCaneleiras = d; break;
            }
        }

        /// <summary>A peça do lugar está vestida e inteira (a quebrada não protege).</summary>
        public bool Protege(LugarDaProtecao l) => Peca(l) != Item.Nada && Desgaste(l) < 100;

        /// <summary>Alguma peça vestida.</summary>
        public bool Alguma => capacete != Item.Nada || colete != Item.Nada || bracadeiras != Item.Nada || caneleiras != Item.Nada;

        public bool Equals(ProtecaoVestida o) =>
            capacete == o.capacete && colete == o.colete && bracadeiras == o.bracadeiras && caneleiras == o.caneleiras
            && gastoCapacete == o.gastoCapacete && gastoColete == o.gastoColete && gastoBracadeiras == o.gastoBracadeiras && gastoCaneleiras == o.gastoCaneleiras;

        public override bool Equals(object o) => o is ProtecaoVestida p && Equals(p);
        public override int GetHashCode() => HashCode.Combine(capacete, colete, bracadeiras, caneleiras, gastoCapacete, gastoColete, gastoBracadeiras, gastoCaneleiras);
    }

    /// <summary>
    /// A proteção de sucata (11/11/2026, a etapa 3 do ramo Proteção de sucata da Resistência; o plano aprovado pelo Pedro):
    /// o capacete de panela (<c>p1</c>), as braçadeiras e as caneleiras (<c>p2</c>) e o colete de chapa (<c>p3</c>), feitos
    /// na bancada e na mesa de oficina (<see cref="Receitas"/>) e vestidos no grupo PROTEÇÃO da tela do inventário. Cada peça
    /// corta uma parte do golpe que pega na parte do corpo que ela cobre (<see cref="Corte"/>) e se gasta com ele, até
    /// quebrar (<see cref="Aguenta"/>; quebrada, não protege, e o kit de reparo conserta). O Remendo de chapa (<c>p4</c>)
    /// gasta menos; a Couraça (<c>p5</c>): a mordida numa parte coberta não infecciona. O colete pesa (anda mais devagar) e
    /// faz barulho, e as caneleiras também (os passos se ouvem mais longe). Os números ficam aqui, para o Pedro ajustar.
    /// </summary>
    public static class Protecao
    {
        // ------------------------------------------------------------------ os números

        /// <summary>Quanto do golpe cada peça corta (o capacete na cabeça, o colete no tronco, as braçadeiras nos braços e as caneleiras nas pernas).</summary>
        public const float CorteDoCapacete = 0.35f, CorteDoColete = 0.30f, CorteDasBracadeiras = 0.30f, CorteDasCaneleiras = 0.30f;

        /// <summary>
        /// Quanto golpe (na escala da vida: a mordida do Oco tira 12) cada peça segura até quebrar: cada golpe gasta dano ×
        /// 100 ÷ isto (pelo menos 1%). O capacete aguenta umas 12 mordidas; o colete, umas 20; as braçadeiras e as caneleiras
        /// (o par, um desgaste só), umas 12.
        /// </summary>
        public const float AguentaOCapacete = 150f, AguentaOColete = 250f, AguentamAsBracadeiras = 150f, AguentamAsCaneleiras = 150f;

        /// <summary>O Remendo de chapa (<c>p4</c>): as peças se gastam isto menos por nível (2 níveis).</summary>
        public const float RemendoPorNivel = 0.25f;

        /// <summary>
        /// O conserto com o kit de reparo pede o Remendo de chapa? (11/11/2026: não; qualquer um conserta, e o Remendo só gasta
        /// menos. Mude para true se o conserto for só de quem tem o nó.)
        /// </summary>
        public const bool ConsertoPedeORemendo = false;

        /// <summary>O colete deixa o andar (e o correr) 4% mais lento.</summary>
        public const float LentidaoDoColete = 0.04f;

        /// <summary>Quanto mais longe os passos se ouvem (e mais alto o som deles): com o colete e com as caneleiras.</summary>
        public const float PassosDoColete = 0.15f, PassosDasCaneleiras = 0.10f;

        public const int Quantos = 4;

        // ------------------------------------------------------------------ as peças

        /// <summary>Uma peça da proteção de sucata (o capacete, o colete, as braçadeiras, as caneleiras).</summary>
        public static bool EhPeca(Item i) => i >= Item.Capacete && i <= Item.Caneleiras;
        public static LugarDaProtecao LugarDe(Item i) => (LugarDaProtecao)(i - Item.Capacete);
        public static Item ItemDo(LugarDaProtecao l) => (Item)((int)Item.Capacete + (int)l);

        public static float Corte(LugarDaProtecao l) => l switch
        {
            LugarDaProtecao.Capacete => CorteDoCapacete, LugarDaProtecao.Colete => CorteDoColete,
            LugarDaProtecao.Bracadeiras => CorteDasBracadeiras, _ => CorteDasCaneleiras,
        };

        public static float Aguenta(LugarDaProtecao l) => l switch
        {
            LugarDaProtecao.Capacete => AguentaOCapacete, LugarDaProtecao.Colete => AguentaOColete,
            LugarDaProtecao.Bracadeiras => AguentamAsBracadeiras, _ => AguentamAsCaneleiras,
        };

        /// <summary>O tamanho na mochila (o capacete 2 × 2, o colete 2 × 3, as braçadeiras 2 × 1, as caneleiras 2 × 2).</summary>
        public static Vector2Int Tamanho(Item i) => LugarDe(i) switch
        {
            LugarDaProtecao.Capacete => new Vector2Int(2, 2), LugarDaProtecao.Colete => new Vector2Int(2, 3),
            LugarDaProtecao.Bracadeiras => new Vector2Int(2, 1), _ => new Vector2Int(2, 2),
        };

        /// <summary>O nome do lugar, embaixo dele na tela.</summary>
        public static string NomeDoLugar(LugarDaProtecao l) => l switch
        {
            LugarDaProtecao.Capacete => "Capacete", LugarDaProtecao.Colete => "Colete",
            LugarDaProtecao.Bracadeiras => "Braçadeiras", _ => "Caneleiras",
        };

        /// <summary>O que o lugar cobre ("a cabeça", "o tronco", "os braços", "as pernas").</summary>
        public static string QueCobre(LugarDaProtecao l) => l switch
        {
            LugarDaProtecao.Capacete => "a cabeça", LugarDaProtecao.Colete => "o tronco",
            LugarDaProtecao.Bracadeiras => "os braços", _ => "as pernas",
        };

        /// <summary>A peça que cobre a parte do corpo (a cabeça, o capacete; o tronco, o colete; os braços e as pernas, o par).</summary>
        public static LugarDaProtecao LugarQueCobre(ParteDoCorpo p) => p switch
        {
            ParteDoCorpo.Cabeca => LugarDaProtecao.Capacete, ParteDoCorpo.Tronco => LugarDaProtecao.Colete,
            ParteDoCorpo.BracoE or ParteDoCorpo.BracoD => LugarDaProtecao.Bracadeiras, _ => LugarDaProtecao.Caneleiras,
        };

        /// <summary>
        /// Os golpes que a proteção segura: os de quem bate (a mordida, o corte, a pancada, o tiro e o esmagamento). A queda,
        /// o tornado e o raio passam inteiros.
        /// </summary>
        public static bool Segura(TipoDeGolpe t) =>
            t == TipoDeGolpe.Mordida || t == TipoDeGolpe.Corte || t == TipoDeGolpe.Pancada || t == TipoDeGolpe.Tiro || t == TipoDeGolpe.Esmagamento;

        /// <summary>Quanto um golpe de 'dano' gasta da peça (%), com o Remendo de chapa ('remendo': os pontos dele); pelo menos 1.</summary>
        public static int GastoDoGolpe(LugarDaProtecao l, float dano, int remendo) =>
            Mathf.Max(1, Mathf.RoundToInt(dano * 100f / Aguenta(l) * Mathf.Max(0f, 1f - RemendoPorNivel * remendo)));

        // ------------------------------------------------------------------ o peso e o barulho

        /// <summary>O fator da velocidade de quem veste (o colete, 0,96; o resto, 1).</summary>
        public static float FatorDaVelocidade(InventarioJogador inv) =>
            inv != null && inv.IsSpawned && inv.Protecao.Value.colete != Item.Nada ? 1f - LentidaoDoColete : 1f;

        /// <summary>O fator dos passos de quem veste (o alcance do barulho e o volume do som): o colete e as caneleiras somam.</summary>
        public static float FatorDosPassos(InventarioJogador inv)
        {
            if (inv == null || !inv.IsSpawned) return 1f;
            var p = inv.Protecao.Value;
            return 1f + (p.colete != Item.Nada ? PassosDoColete : 0f) + (p.caneleiras != Item.Nada ? PassosDasCaneleiras : 0f);
        }

        public static float FatorDosPassos(JogadorRede j) => j != null ? FatorDosPassos(j.Inventario) : 1f;

        // ------------------------------------------------------------------ os textos (o gênero e o número de cada peça)

        static bool Plural(Item i) => i == Item.Bracadeiras || i == Item.Caneleiras;

        /// <summary>"O capacete de panela quebrou", "As braçadeiras de sucata quebraram".</summary>
        public static string Quebrou(Item i) =>
            $"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(i))} {(Plural(i) ? "quebraram" : "quebrou")}: não {(Plural(i) ? "protegem" : "protege")} mais (conserte com um kit de reparo)";

        /// <summary>"O capacete de panela foi consertado", "As braçadeiras de sucata foram consertadas".</summary>
        public static string Consertada(Item i) =>
            $"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(i))} {(Plural(i) ? "foram consertadas" : "foi consertado")}";

        /// <summary>O estado da peça: "100%", "35%", "quebrado" (ou "quebradas", nas do par).</summary>
        public static string Estado(Item i, int desgaste) => desgaste >= 100 ? (Plural(i) ? "quebradas" : "quebrado") : $"{100 - desgaste}%";
    }
}
