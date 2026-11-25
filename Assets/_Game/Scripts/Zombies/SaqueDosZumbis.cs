using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O que os zumbis levavam (24/11/2026, a pedido: "precisamos colocar uma espécie de lotear os zumbis que matamos, onde
    /// eles podem carregar algumas águas, alguns itens, alimentos, roupas e tecidos ... Para quando apertarmos E nele, terá
    /// uma espécie de loading e depois ... aparecer um menu do zumbi (como se fosse um inventário humano) com os itens"):
    /// <list type="bullet">
    /// <item>no anfitrião, quando o zumbi morre (<see cref="Zumbi"/>.Morrer), o corpo vira um recipiente (MochilasNoChao, o
    /// tipo Zumbi): os bolsos (4 × 3) com 0 a 3 coisas sorteadas (água, comida, pano, roupa velha, umas roupas, um remédio, um
    /// maço...), mais o que é do ofício de cada Oco (a enfermeira, o mecânico, o executivo...);</item>
    /// <item>o zumbi de mochila (<see cref="Zumbi.MochilaNasCostas"/>): a grade é a da mochila, com 3 a 6 coisas de quem
    /// sobrevivia (mais água, comida, remédio, umas balas) e a mochila no chão, para trocar pela nossa;</item>
    /// <item>os especiais deixam o troféu que sobe o núcleo da base: o olho do Brutamonte, a garganta do Long, a cabeça do
    /// chefe (os que não viram outro) e, às vezes (3%), a cabeça do Oco que morreu com ela;</item>
    /// <item>segurar o E no corpo revista (o "loading") e abre a tela do saque, com tudo para pegar. O recipiente fica o mesmo
    /// tempo que o corpo no chão (10 min) e não vai no save.</item>
    /// </list>
    /// </summary>
    public static class SaqueDosZumbis
    {
        /// <summary>A chance de a cabeça do Oco (inteira) ficar boa para o núcleo.</summary>
        public const float ChanceDaCabecaDoOco = 0.03f;
        /// <summary>A chance de um corpo comum ter alguma coisa nos bolsos.</summary>
        public const float ChanceDeTerAlgo = 0.6f;

        struct Entrada
        {
            public Item item;
            public string roupa;
            public int peso, min, max;
        }

        static Entrada L(Item item, int peso, int min = 1, int max = 1) => new Entrada { item = item, peso = peso, min = min, max = max };
        static Entrada R(string roupa, int peso) => new Entrada { roupa = roupa, peso = peso, min = 1, max = 1 };

        // os bolsos de qualquer um: a água, um pouco de comida, o pano e a roupa velha, umas roupas, uma coisa ou outra
        static readonly Entrada[] DosBolsos =
        {
            L(Item.Agua, 10), L(Item.RefrigeranteLata, 4), L(Item.Suco, 2),
            L(Item.Salgadinho, 7), L(Item.Chocolate, 5), L(Item.BarraDeEnergia, 3), L(Item.Comida, 4), L(Item.Feijao, 2),
            L(Item.Pano, 9, 1, 2), L(Item.Roupa, 6),
            R("Camiseta", 2), R("Jeans", 2), R("Tenis", 1), R("Moletom", 1), R("Bone", 1), R("Mascara_Pano", 1), R("Gorro", 1),
            L(Item.MacoDeCigarros, 3, 2, 14), L(Item.Atadura, 2), L(Item.Comprimidos, 1), L(Item.Pilhas, 2, 1, 2),
            L(Item.FitaAdesiva, 1), L(Item.Corda, 1), L(Item.Gazua, 1),
        };

        // o de quem sobrevivia (o zumbi de mochila): mais água e comida, remédio, umas balas e o que serve para fabricar
        static readonly Entrada[] DaMochila =
        {
            L(Item.Agua, 12), L(Item.GarrafaTermica, 2), L(Item.Energetico, 2),
            L(Item.Comida, 7), L(Item.Feijao, 5), L(Item.Milho, 3), L(Item.Sopa, 3), L(Item.BarraDeEnergia, 5), L(Item.Chocolate, 3),
            L(Item.Atadura, 5), L(Item.Gaze, 3), L(Item.Comprimidos, 3), L(Item.Desinfetante, 2), L(Item.Antibiotico, 1), L(Item.Calmante, 1),
            L(Item.Pano, 6, 1, 3), L(Item.Roupa, 3), L(Item.Corda, 3), L(Item.FitaAdesiva, 3), L(Item.Fios, 2),
            L(Item.Pilhas, 3, 1, 3), L(Item.Lanterna, 1), L(Item.Isca, 1, 2, 5),
            L(Item.Municao9mm, 3, 4, 12), L(Item.Municao12, 1, 2, 5), L(Item.MunicaoPrego, 2, 4, 10),
            R("Jaqueta_Lona", 1), R("Calca_Cargo", 1), R("Bota_Trabalho", 1), R("Luva_Sem_Dedos", 1), R("Gola_Tubular", 1),
        };

        // o ofício de cada Oco (o nome do modelo dele): o que ele ainda leva, além dos bolsos
        static Entrada[] DoOficio(string modelo)
        {
            if (modelo.Contains("Enfermeira")) return new[] { L(Item.Atadura, 6), L(Item.Gaze, 5), L(Item.Comprimidos, 4), L(Item.Desinfetante, 3), L(Item.Antibiotico, 1), R("Jaleco", 2), R("Mascara_Cirurgica", 4) };
            if (modelo.Contains("Mecanico")) return new[] { L(Item.PecasDeMetal, 5), L(Item.Fios, 4), L(Item.FitaAdesiva, 3), L(Item.Pregos, 3, 3, 10), L(Item.Ferramenta, 1), R("Calca_Trabalho", 2), R("Cinto_Ferramentas", 1) };
            if (modelo.Contains("Estudante")) return new[] { L(Item.Salgadinho, 5), L(Item.Chocolate, 4), L(Item.RefrigeranteLata, 4), L(Item.Energetico, 2), R("Moletom_Ziper", 2), R("Tenis_Cano", 1) };
            if (modelo.Contains("Executivo")) return new[] { L(Item.Comprimidos, 2), L(Item.MacoDeCigarros, 3, 4, 18), R("Camisa_Social", 3), R("Calca_Social", 2), R("Sapato_Social", 2) };
            if (modelo.Contains("Lenhador")) return new[] { L(Item.Madeira, 5, 1, 2), L(Item.Corda, 3), L(Item.Gravetos, 3, 1, 3), R("Camisa_Xadrez", 3), R("Bota_Trabalho", 2), R("Gorro", 1) };
            if (modelo.Contains("Corredora")) return new[] { L(Item.Agua, 6), L(Item.Energetico, 4), L(Item.BarraDeEnergia, 4), R("Top", 2), R("Tenis_Baixo", 2), R("Oculos_Esportivo", 1) };
            if (modelo.Contains("Vigia")) return new[] { L(Item.Lanterna, 3), L(Item.Pilhas, 4, 1, 3), L(Item.Municao9mm, 2, 3, 9), R("Bone_Policia", 2), R("Cinto_Policia", 1) };
            if (modelo.Contains("Turista")) return new[] { L(Item.Agua, 5), L(Item.Suco, 3), L(Item.GarrafaTermica, 2), R("Bone", 2), R("Oculos_Sol", 2), R("Chapeu_Cauboi", 1) };
            return null;
        }

        // o nome do corpo (o título da tela) pelo modelo e pelo tipo
        static string NomeDoCorpo(Zumbi z, string modelo)
        {
            switch (z.tipo)
            {
                case TipoDeZumbi.Brutamonte: return "o corpo do Brutamonte";
                case TipoDeZumbi.Long: return "o corpo do Long";
                case TipoDeZumbi.Corredor: return "o corpo do Corredor";
                case TipoDeZumbi.Chefe: return "o corpo do chefe";
            }
            if (modelo.Contains("Enfermeira")) return "o corpo da enfermeira";
            if (modelo.Contains("Mecanico")) return "o corpo do mecânico";
            if (modelo.Contains("Estudante")) return "o corpo do estudante";
            if (modelo.Contains("Executivo")) return "o corpo do executivo";
            if (modelo.Contains("Lenhador")) return "o corpo do lenhador";
            if (modelo.Contains("Corredora")) return "o corpo da corredora";
            if (modelo.Contains("Vigia")) return "o corpo do vigia";
            if (modelo.Contains("Turista")) return "o corpo do turista";
            return "o corpo do zumbi";
        }

        /// <summary>Quantos recipientes de zumbi foram criados nesta partida (o teste).</summary>
        public static int Criados { get; private set; }

        /// <summary>No anfitrião: o zumbi morreu; o corpo vira o recipiente com o que ele levava.</summary>
        public static void AoMorrer(Zumbi z)
        {
            var mochilas = MochilasNoChao.Instancia;
            if (z == null || mochilas == null || !mochilas.IsServer || !mochilas.IsSpawned || Video.ModoVideo.Ligado) return;
            // só perto de alguém (os zumbis que os sobreviventes matam longe não gastam o teto de recipientes)
            if (!AlguemPerto(z.transform.position)) return;
            var zm = ZombieManager.Instancia;
            string modelo = zm != null && zm.modelos != null && z.modelo >= 0 && z.modelo < zm.modelos.Length && zm.modelos[z.modelo] != null ? zm.modelos[z.modelo].name : "";
            var itens = new List<ItemNaMochila>();
            Item mochila = z.MochilaNasCostas;
            int dinheiro = 0;
            Vector2Int grade = new Vector2Int(4, 3);

            // os troféus dos especiais (o núcleo da base)
            switch (z.tipo)
            {
                case TipoDeZumbi.Brutamonte: Por(itens, Item.OlhoDoBrutamonte, 1); break;
                case TipoDeZumbi.Long: Por(itens, Item.GargantaDoLong, 1); break;
                case TipoDeZumbi.Chefe:
                    // (o Blindado se divide e o Alongado vira o Longo: a cabeça é de quem morre de vez)
                    if (z.qualChefe != QualChefe.Blindado && z.qualChefe != QualChefe.Alongado) Por(itens, Item.CabecaDeChefe, 1);
                    grade = new Vector2Int(5, 4);
                    break;
                case TipoDeZumbi.Oco:
                    if (!z.SemCabeca && Random.value < ChanceDaCabecaDoOco) Por(itens, Item.CabecaDoOco, 1);
                    break;
            }

            if (Itens.EhMochila(mochila))
            {
                // o sobrevivente que virou zumbi: a mochila cheia (a grade dela)
                var espaco = Itens.EspacoDa(mochila);
                if (espaco.x > 0 && espaco.y > 0) grade = espaco;
                int n = Random.Range(3, 7);
                for (int i = 0; i < n; i++) Sortear(itens, DaMochila);
                if (Random.value < 0.35f) dinheiro = Random.Range(5, 61);
            }
            if (z.tipo == TipoDeZumbi.Oco || z.tipo == TipoDeZumbi.Corredor)
            {
                if (Random.value < ChanceDeTerAlgo)
                {
                    var oficio = DoOficio(modelo);
                    int n = Random.Range(1, 4);
                    for (int i = 0; i < n; i++) Sortear(itens, oficio != null && Random.value < 0.4f ? oficio : DosBolsos);
                }
                // o executivo ainda tem a carteira
                if (modelo.Contains("Executivo") && Random.value < 0.6f) dinheiro += Random.Range(10, 81);
                else if (Random.value < 0.12f) dinheiro += Random.Range(1, 16);
            }

            // no chão, onde o corpo cai (um pouco para o lado do empurrão)
            var empurrao = Quaternion.Euler(0f, z.DirecaoDaMorte, 0f) * Vector3.forward;
            Vector3 pos = z.transform.position + empurrao * 0.6f;
            if (Physics.Raycast(pos + Vector3.up * 1.2f, Vector3.down, out var chao, 3f, ~((1 << 2) | (1 << 7) | (1 << 8) | (1 << JogadorRede.CamadaJogador)), QueryTriggerInteraction.Ignore))
                pos = chao.point;
            ushort id = mochilas.CriarRecipiente(TipoDeRecipiente.Zumbi, OrigemDe(z), pos, z.DirecaoDaMorte, NomeDoCorpo(z, modelo), grade, mochila, itens, dinheiro);
            if (id != 0) Criados++;
        }

        /// <summary>A origem do recipiente do corpo: o Id e a geração do zumbi (o Id volta do pool; com a geração, é outro).</summary>
        public static int OrigemDe(Zumbi z) => z.Id | (z.Geracao << 16);

        /// <summary>Até onde de um jogador o corpo vira recipiente (m).</summary>
        public const float AlcanceDoSaque = 150f;

        static bool AlguemPerto(Vector3 p)
        {
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.IsSpawned && (j.transform.position - p).sqrMagnitude < AlcanceDoSaque * AlcanceDoSaque) return true;
            return false;
        }

        static void Por(List<ItemNaMochila> itens, Item item, int n)
        {
            if (!Itens.NoJogo(item)) return;
            itens.Add(new ItemNaMochila { item = item, qtd = (ushort)(Itens.EmPilha(item) ? Itens.Quantos(item, n) : Itens.QuantidadePadrao(item)) });
        }

        static void Sortear(List<ItemNaMochila> itens, Entrada[] tabela)
        {
            int total = 0;
            foreach (var e in tabela) total += e.peso;
            int r = Random.Range(0, total);
            foreach (var e in tabela)
            {
                r -= e.peso;
                if (r >= 0) continue;
                if (e.roupa != null)
                {
                    var roupa = Itens.RoupaDaPeca(e.roupa);
                    if (roupa != Item.Nada && Itens.NoJogo(roupa)) itens.Add(new ItemNaMochila { item = roupa, qtd = 0 });
                }
                else Por(itens, e.item, Random.Range(e.min, e.max + 1));
                return;
            }
        }
    }
}
