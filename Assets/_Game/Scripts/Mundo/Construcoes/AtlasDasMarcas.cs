// Gerado pelo hospital_marcas.py (pasta de arte, Modelo 3D/Cidade) junto com o atlas Marcas.png: não mude à mão.
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As marcas do abandono do Hospital Principal (o atlas T_Marcas, desenhado em código pelo hospital_marcas.py; o
    /// Plano_Hospital.md, a parte 5): o retângulo de cada uma no atlas, o lugar (o chão, a parede ou o teto), a categoria
    /// (a cor que a tinge) e o tamanho em metros.
    /// </summary>
    public static class AtlasDasMarcas
    {
        public enum Lugar : byte { Chao, Parede, Teto }

        /// <summary>A cor do vértice: o sangue (o mais novo e o velho), a água, o mofo, a sujeira, o reboco dos tiros; os
        /// papéis e os cacos já vêm coloridos (branco).</summary>
        public enum Categoria : byte { Sangue, SangueVelho, Agua, Mofo, Sujeira, Tiro, Papel, Vidro }

        public readonly struct Item
        {
            public readonly string nome;
            public readonly Lugar lugar;
            public readonly Categoria categoria;
            /// <summary>No atlas (0 a 1, v de baixo para cima).</summary>
            public readonly Rect uv;
            /// <summary>Em metros (no chão e no teto, a largura em x do quadro e a altura em z; na parede, a altura em y).</summary>
            public readonly Vector2 tamanho;

            public Item(string nome, Lugar lugar, Categoria categoria, float u, float v, float du, float dv, float largura, float altura)
            {
                this.nome = nome;
                this.lugar = lugar;
                this.categoria = categoria;
                uv = new Rect(u, v, du, dv);
                tamanho = new Vector2(largura, altura);
            }

            public bool Colorida => categoria == Categoria.Papel || categoria == Categoria.Vidro;
        }

        public static readonly Item[] Itens =
        {
            new Item("PocaGrande_A", Lugar.Chao, Categoria.Sangue, 0.500000f, 0.625000f, 0.250000f, 0.250000f, 1.500f, 1.500f),
            new Item("PocaGrande_B", Lugar.Chao, Categoria.SangueVelho, 0.750000f, 0.625000f, 0.250000f, 0.250000f, 1.400f, 1.400f),
            new Item("PocaPequena", Lugar.Chao, Categoria.Sangue, 0.500000f, 0.125000f, 0.125000f, 0.125000f, 0.700f, 0.700f),
            new Item("Arrasto", Lugar.Chao, Categoria.Sangue, 0.000000f, 0.875000f, 0.500000f, 0.125000f, 2.800f, 0.700f),
            new Item("Pegadas", Lugar.Chao, Categoria.Sangue, 0.500000f, 0.875000f, 0.500000f, 0.125000f, 2.800f, 0.700f),
            new Item("Respingos", Lugar.Chao, Categoria.Sangue, 0.000000f, 0.500000f, 0.250000f, 0.250000f, 1.300f, 1.300f),
            new Item("Rodinhas", Lugar.Chao, Categoria.SangueVelho, 0.000000f, 0.750000f, 0.500000f, 0.125000f, 2.800f, 0.700f),
            new Item("Agua", Lugar.Chao, Categoria.Agua, 0.250000f, 0.500000f, 0.250000f, 0.250000f, 1.800f, 1.800f),
            new Item("SujeiraDoCanto", Lugar.Chao, Categoria.Sujeira, 0.500000f, 0.250000f, 0.250000f, 0.125000f, 1.800f, 0.900f),
            new Item("Papeis", Lugar.Chao, Categoria.Papel, 0.500000f, 0.375000f, 0.250000f, 0.250000f, 1.200f, 1.200f),
            new Item("Cacos", Lugar.Chao, Categoria.Vidro, 0.625000f, 0.125000f, 0.125000f, 0.125000f, 0.600f, 0.600f),
            new Item("Maos", Lugar.Parede, Categoria.Sangue, 0.250000f, 0.000000f, 0.125000f, 0.250000f, 0.550f, 1.100f),
            new Item("MaoArrastada", Lugar.Parede, Categoria.Sangue, 0.750000f, 0.250000f, 0.250000f, 0.125000f, 1.200f, 0.600f),
            new Item("RespingoNaParede", Lugar.Parede, Categoria.Sangue, 0.750000f, 0.375000f, 0.250000f, 0.250000f, 1.300f, 1.300f),
            new Item("InfiltracaoNaParede", Lugar.Parede, Categoria.Agua, 0.375000f, 0.000000f, 0.125000f, 0.250000f, 0.900f, 1.800f),
            new Item("MofoNaBase", Lugar.Parede, Categoria.Mofo, 0.000000f, 0.125000f, 0.250000f, 0.125000f, 1.600f, 0.800f),
            new Item("Tiros", Lugar.Parede, Categoria.Tiro, 0.750000f, 0.125000f, 0.125000f, 0.125000f, 0.600f, 0.600f),
            new Item("Encardido", Lugar.Parede, Categoria.Sujeira, 0.875000f, 0.125000f, 0.125000f, 0.125000f, 0.600f, 0.600f),
            new Item("InfiltracaoNoTeto", Lugar.Teto, Categoria.Agua, 0.000000f, 0.250000f, 0.250000f, 0.250000f, 1.800f, 1.800f),
            new Item("MofoNoTeto", Lugar.Teto, Categoria.Mofo, 0.250000f, 0.250000f, 0.250000f, 0.250000f, 1.500f, 1.500f),
        };

        /// <summary>O índice da marca pelo nome (-1 se não tem).</summary>
        public static int Indice(string nome)
        {
            for (int i = 0; i < Itens.Length; i++) if (Itens[i].nome == nome) return i;
            return -1;
        }
    }
}
