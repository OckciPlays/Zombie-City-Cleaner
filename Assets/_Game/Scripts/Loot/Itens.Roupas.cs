using UnityEngine;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// Os lugares das roupas no corpo: os da tela do inventário, em volta do retrato (o Plano_Personagem.md da pasta de
    /// arte, a parte 4; 31/10/2026, a pedido: 5 de cada lado, com as luvas, e o cinto e o coldre no mesmo lugar). Cada
    /// lugar veste uma peça; a jaqueta e o colete dividem o mesmo (não vão juntos).
    /// </summary>
    public enum LugarDaRoupa : byte { Chapeu, Oculos, Mascara, Camiseta, Luvas, PorCima, Cinto, Calca, Sapato }

    /// <summary>Uma roupa como item: a peça do catálogo das roupas (o Roupas.json), os nomes e o lugar dela no corpo.</summary>
    public class DadosDaRoupa
    {
        public string peca, nome, nomeComArtigo;
        public LugarDaRoupa lugar;
    }

    /// <summary>
    /// As roupas como itens (a parte 4 do Plano_Personagem.md, 31/10/2026): uma por peça do catálogo que se veste (as 73
    /// do Roupas_Itens.fbx: tudo menos os cabelos, as barbas, os cílios e as versões de baixo do chapéu), de
    /// <see cref="Item.PrimeiraRoupa"/> em diante, na ordem do catálogo (uma peça nova vai no fim, lá e aqui). Cada uma ocupa
    /// uma célula da mochila e leva as três cores dela (<see cref="Cores(string[])"/>: no <c>cores</c> do item na mochila,
    /// no chão e vestido; 0, as do catálogo).
    /// </summary>
    public static partial class Itens
    {
        public const int QuantosLugaresDeRoupa = 9;

        static DadosDaRoupa R(string peca, string artigo, string nome, LugarDaRoupa lugar) =>
            new DadosDaRoupa { peca = peca, nome = nome, nomeComArtigo = artigo + " " + nome, lugar = lugar };

        // a ordem do catálogo (o roupas_exportar.py: as peças de cada lote, na ordem em que entraram); a montagem das
        // roupas confere (MontagemDasRoupas.ConferirItens)
        static readonly DadosDaRoupa[] _roupas =
        {
            R("Bone", "o", "boné", LugarDaRoupa.Chapeu),
            R("Camiseta", "a", "camiseta", LugarDaRoupa.Camiseta),
            R("Jaqueta_Capuz", "a", "jaqueta com capuz", LugarDaRoupa.PorCima),
            R("Jeans", "a", "calça jeans", LugarDaRoupa.Calca),
            R("Tenis", "o", "tênis", LugarDaRoupa.Sapato),
            R("Oculos", "os", "óculos", LugarDaRoupa.Oculos),
            R("Top", "o", "top", LugarDaRoupa.Camiseta),
            R("Bone_Militar", "o", "boné militar", LugarDaRoupa.Chapeu),
            R("Bone_Policia", "o", "boné da polícia", LugarDaRoupa.Chapeu),
            R("Quepe", "o", "quepe", LugarDaRoupa.Chapeu),
            R("Gorro", "o", "gorro", LugarDaRoupa.Chapeu),
            R("Capacete", "o", "capacete de obra", LugarDaRoupa.Chapeu),
            R("Chapeu_Cauboi", "o", "chapéu de caubói", LugarDaRoupa.Chapeu),
            R("Bandana", "a", "bandana", LugarDaRoupa.Chapeu),
            R("Regata", "a", "regata", LugarDaRoupa.Camiseta),
            R("Regata_Feminina", "a", "regata feminina", LugarDaRoupa.Camiseta),
            R("Camiseta_V", "a", "camiseta gola V", LugarDaRoupa.Camiseta),
            R("Camisa_Uniforme", "a", "camisa de uniforme", LugarDaRoupa.Camiseta),
            R("Camisa_Uniforme_Longa", "a", "camisa de uniforme de manga longa", LugarDaRoupa.Camiseta),
            R("Camisa_Militar", "a", "camisa militar", LugarDaRoupa.Camiseta),
            R("Camisa_Xadrez", "a", "camisa xadrez", LugarDaRoupa.Camiseta),
            R("Camisa_Social", "a", "camisa social", LugarDaRoupa.Camiseta),
            R("Jaqueta_Lona", "a", "jaqueta de lona", LugarDaRoupa.PorCima),
            R("Jaqueta_Couro", "a", "jaqueta de couro", LugarDaRoupa.PorCima),
            R("Jaqueta_Esportiva", "a", "jaqueta esportiva", LugarDaRoupa.PorCima),
            R("Moletom", "o", "moletom", LugarDaRoupa.PorCima),
            R("Moletom_Ziper", "o", "moletom com zíper", LugarDaRoupa.PorCima),
            R("Jaleco", "o", "jaleco", LugarDaRoupa.PorCima),
            R("Cardiga", "o", "cardigã", LugarDaRoupa.PorCima),
            R("Colete_Refletivo", "o", "colete refletivo", LugarDaRoupa.PorCima),
            R("Colete_Tatico", "o", "colete tático", LugarDaRoupa.PorCima),
            R("Jeans_Rasgado", "o", "jeans rasgado", LugarDaRoupa.Calca),
            R("Calca_Cargo", "a", "calça cargo", LugarDaRoupa.Calca),
            R("Calca_Militar", "a", "calça militar", LugarDaRoupa.Calca),
            R("Calca_Uniforme", "a", "calça de uniforme", LugarDaRoupa.Calca),
            R("Calca_Trabalho", "a", "calça de trabalho", LugarDaRoupa.Calca),
            R("Calca_Social", "a", "calça social", LugarDaRoupa.Calca),
            R("Jardineira", "a", "jardineira", LugarDaRoupa.Calca),
            R("Jeans_Suspensorio", "o", "jeans com suspensório", LugarDaRoupa.Calca),
            R("Bota_Trabalho", "a", "bota de trabalho", LugarDaRoupa.Sapato),
            R("Bota_Couro", "a", "bota de couro", LugarDaRoupa.Sapato),
            R("Bota_Cauboi", "a", "bota de caubói", LugarDaRoupa.Sapato),
            R("Coturno", "o", "coturno", LugarDaRoupa.Sapato),
            R("Tenis_Baixo", "o", "tênis baixo", LugarDaRoupa.Sapato),
            R("Tenis_Cano", "o", "tênis de cano", LugarDaRoupa.Sapato),
            R("Sapato_Social", "o", "sapato social", LugarDaRoupa.Sapato),
            R("Luva_Tatica", "as", "luvas táticas", LugarDaRoupa.Luvas),
            R("Luva_Sem_Dedos", "as", "luvas sem dedos", LugarDaRoupa.Luvas),
            R("Cinto_Policia", "o", "cinto da polícia", LugarDaRoupa.Cinto),
            R("Cinto_Ferramentas", "o", "cinto de ferramentas", LugarDaRoupa.Cinto),
            R("Cinto_Tatico", "o", "cinto tático", LugarDaRoupa.Cinto),
            R("Oculos_Grosso", "os", "óculos de aro grosso", LugarDaRoupa.Oculos),
            R("Oculos_Redondo", "os", "óculos redondos", LugarDaRoupa.Oculos),
            R("Oculos_Meio_Aro", "os", "óculos meio-aro", LugarDaRoupa.Oculos),
            R("Oculos_Sol", "os", "óculos de sol", LugarDaRoupa.Oculos),
            R("Oculos_Aviador", "os", "óculos aviador", LugarDaRoupa.Oculos),
            R("Oculos_Sol_Redondo", "os", "óculos de sol redondos", LugarDaRoupa.Oculos),
            R("Oculos_Esportivo", "os", "óculos esportivos", LugarDaRoupa.Oculos),
            R("Oculos_Protecao", "os", "óculos de proteção", LugarDaRoupa.Oculos),
            R("Oculos_Tatico", "os", "óculos táticos", LugarDaRoupa.Oculos),
            R("Tapa_Olho", "o", "tapa-olho", LugarDaRoupa.Oculos),
            R("Visao_Noturna", "a", "visão noturna", LugarDaRoupa.Oculos),
            R("Mascara_Cirurgica", "a", "máscara cirúrgica", LugarDaRoupa.Mascara),
            R("Mascara_Pano", "a", "máscara de pano", LugarDaRoupa.Mascara),
            R("Bandana_Rosto", "a", "bandana no rosto", LugarDaRoupa.Mascara),
            R("Gola_Tubular", "a", "gola tubular", LugarDaRoupa.Mascara),
            R("Gola_Camuflada", "a", "gola camuflada", LugarDaRoupa.Mascara),
            R("Gola_Caveira", "a", "gola de caveira", LugarDaRoupa.Mascara),
            R("Respirador", "o", "respirador", LugarDaRoupa.Mascara),
            R("Mascara_Gas", "a", "máscara de gás", LugarDaRoupa.Mascara),
            R("Balaclava", "a", "balaclava", LugarDaRoupa.Mascara),
            R("Viseira", "a", "viseira", LugarDaRoupa.Mascara),
            R("Capacete_Militar", "o", "capacete militar", LugarDaRoupa.Chapeu),
        };

        // o nome de cada lugar (na tela) e o resumo das roupas dele
        static readonly string[] _lugaresDasRoupas = { "Chapéu", "Óculos", "Máscara", "Camiseta", "Luvas", "Jaqueta", "Cinto", "Calça", "Sapato" };
        static readonly string[] _dicasDosLugares =
        {
            "Chapéu", "Óculos", "Máscara", "Camiseta", "Luvas", "Jaqueta ou colete (um dos dois)", "Cinto ou coldre", "Calça", "Calçado",
        };
        static readonly string[] _resumosDasRoupas =
        {
            "Vai na cabeça.", "Vai nos olhos.", "Cobre o rosto.", "Vai no tronco, por baixo.", "Vai nas mãos.",
            "Vai por cima da camiseta.", "Vai na cintura (só enfeite, por enquanto).", "Vai nas pernas.", "Vai nos pés.",
        };

        public static int QuantasRoupas => _roupas.Length;
        public static bool EhRoupa(Item i) => i >= Item.PrimeiraRoupa && i - Item.PrimeiraRoupa < _roupas.Length;
        public static int IndiceDaRoupa(Item i) => EhRoupa(i) ? i - Item.PrimeiraRoupa : -1;
        public static Item DaRoupa(int k) => (Item)((int)Item.PrimeiraRoupa + k);
        public static DadosDaRoupa Roupa(Item i) => EhRoupa(i) ? _roupas[i - Item.PrimeiraRoupa] : null;
        public static DadosDaRoupa Roupa(int k) => _roupas[k];

        /// <summary>O item da peça do catálogo (Nada: a peça não é um item).</summary>
        public static Item RoupaDaPeca(string peca)
        {
            for (int k = 0; k < _roupas.Length; k++)
                if (_roupas[k].peca == peca) return DaRoupa(k);
            return Item.Nada;
        }

        /// <summary>O nome curto do lugar (embaixo dele, na tela).</summary>
        public static string NomeDoLugar(LugarDaRoupa l) => _lugaresDasRoupas[(int)l];
        /// <summary>O lugar escrito por inteiro (a dica do mouse).</summary>
        public static string DicaDoLugar(LugarDaRoupa l) => _dicasDosLugares[(int)l];

        /// <summary>O lugar de uma peça pelo lugar dela no catálogo (a jaqueta e o colete, o mesmo; o cinto e o coldre, o mesmo).</summary>
        public static bool LugarDoCatalogo(string lugar, out LugarDaRoupa l)
        {
            switch (lugar)
            {
                case "Cabeca": l = LugarDaRoupa.Chapeu; return true;
                case "Oculos": l = LugarDaRoupa.Oculos; return true;
                case "Mascara": l = LugarDaRoupa.Mascara; return true;
                case "Tronco": l = LugarDaRoupa.Camiseta; return true;
                case "Maos": l = LugarDaRoupa.Luvas; return true;
                case "Por_Cima":
                case "Colete": l = LugarDaRoupa.PorCima; return true;
                case "Cintura": l = LugarDaRoupa.Cinto; return true;
                case "Pernas": l = LugarDaRoupa.Calca; return true;
                case "Pes": l = LugarDaRoupa.Sapato; return true;
                default: l = default; return false;
            }
        }

        // ------------------------------------------------------------------ as cores

        // as três cores num ulong: 21 bits cada (7 por canal, de 0 a 127, em sRGB) e o bit de cima ligado (0: as do catálogo)
        const ulong ComCores = 1UL << 63;

        /// <summary>As três cores (hex) num número só (0: nenhuma; as do catálogo).</summary>
        public static ulong Cores(string[] hex)
        {
            if (hex == null || hex.Length == 0) return 0;
            ulong v = ComCores;
            for (int k = 0; k < 3; k++)
            {
                var c = ZombieCityCleaner.Roupas.CatalogoDasRoupas.Cor(hex[Mathf.Min(k, hex.Length - 1)]);
                ulong r = (ulong)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 127f), g = (ulong)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 127f),
                      b = (ulong)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 127f);
                v |= ((r << 14) | (g << 7) | b) << (21 * k);
            }
            return v;
        }

        /// <summary>As três cores (hex) de volta (null: 0, as do catálogo).</summary>
        public static string[] CoresEmHex(ulong cores)
        {
            if ((cores & ComCores) == 0) return null;
            var hex = new string[3];
            for (int k = 0; k < 3; k++)
            {
                ulong c = (cores >> (21 * k)) & 0x1FFFFF;
                var cor = new Color(((c >> 14) & 0x7F) / 127f, ((c >> 7) & 0x7F) / 127f, (c & 0x7F) / 127f);
                hex[k] = "#" + ColorUtility.ToHtmlStringRGB(cor);
            }
            return hex;
        }
    }
}
