using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// O que cabe num lugar do inventário: uma arma (o tipo + 1) ou a comida e a água. Vai pela rede num byte.
    /// </summary>
    public enum Item : byte { Nada = 0, Pistola, Rifle, Submetralhadora, Espingarda, Sniper, Comida = 16, Agua }

    /// <summary>Os números da comida e da água: quanto cada uma mata da fome e da sede, e o tempo de comer ou beber.</summary>
    public class DadosDoConsumivel
    {
        public string nome, nomeComArtigo, verbo;
        /// <summary>Quanto sobe a barra da fome e a da sede (de 0 a 100).</summary>
        public float fome, sede;
        /// <summary>O clipe Usar inteiro (s): o item só acaba no fim, e o efeito vem junto.</summary>
        public float tempoDeUso;
    }

    public static class Itens
    {
        /// <summary>
        /// Os modelos de tudo o que vai na mão: as armas (na ordem de <see cref="TipoDeArma"/>) e depois a lata e a
        /// garrafa (Jogador_FP_&lt;nome&gt;.fbx, Item_&lt;nome&gt;, Icone_&lt;nome&gt;).
        /// </summary>
        public static readonly string[] Modelos = { "Pistola", "Rifle", "Submetralhadora", "Espingarda", "Sniper", "Lata", "Garrafa" };
        public const int QuantosModelos = Armas.Quantas + 2;

        /// <summary>
        /// Na primeira pessoa há mais um modelo, depois dos de <see cref="Modelos"/>: os punhos da mão vazia
        /// (Jogador_FP_Maos.fbx, para o soco).
        /// </summary>
        public const int Punhos = QuantosModelos;
        public const int QuantosModelosFP = QuantosModelos + 1;
        public static string ModeloFP(int modelo) => modelo == Punhos ? "Maos" : Modelos[modelo];
        /// <summary>O modelo de primeira pessoa do que está na mão (os punhos, com a mão vazia).</summary>
        public static int ModeloNaMao(Item i) => i == Item.Nada ? Punhos : Modelo(i);

        static readonly DadosDoConsumivel[] _consumiveis =
        {
            // a conserva mata a fome e um pouco da sede (o caldo); a água, só a sede. Os tempos são os do clipe Usar
            // (itens.py: 40 e 44 quadros a 24 fps)
            new DadosDoConsumivel { nome = "lata de comida", nomeComArtigo = "a lata de comida", verbo = "comer", fome = 40f, sede = 5f, tempoDeUso = 40 / 24f },
            new DadosDoConsumivel { nome = "garrafa de água", nomeComArtigo = "a garrafa de água", verbo = "beber", fome = 0f, sede = 45f, tempoDeUso = 44 / 24f },
        };

        public static bool EhArma(Item i) => i >= Item.Pistola && i <= Item.Sniper;
        public static bool EhConsumivel(Item i) => i == Item.Comida || i == Item.Agua;
        public static TipoDeArma Arma(Item i) => (TipoDeArma)((int)i - 1);
        public static Item DaArma(TipoDeArma t) => (Item)((int)t + 1);
        public static DadosDoConsumivel Consumivel(Item i) => _consumiveis[i == Item.Agua ? 1 : 0];

        /// <summary>O índice do modelo do item (na mão, no chão e no ícone), ou -1 (nada).</summary>
        public static int Modelo(Item i) => EhArma(i) ? (int)Arma(i) : i == Item.Comida ? Armas.Quantas : i == Item.Agua ? Armas.Quantas + 1 : -1;

        public static string Nome(Item i) => EhArma(i) ? Armas.Dados(Arma(i)).nome : EhConsumivel(i) ? Consumivel(i).nome : "";
        public static string NomeComArtigo(Item i) => EhArma(i) ? Armas.Dados(Arma(i)).nomeComArtigo : EhConsumivel(i) ? Consumivel(i).nomeComArtigo : "";

        /// <summary>"por" com o artigo: "pelo rifle", "pela lata de comida".</summary>
        public static string Pelo(Item i)
        {
            string nome = NomeComArtigo(i);
            if (nome.StartsWith("a ")) return "pela " + nome.Substring(2);
            if (nome.StartsWith("o ")) return "pelo " + nome.Substring(2);
            return "por " + nome;
        }
    }
}
