// Gerado pelo cidade_pichacoes.py (pasta de arte, Modelo 3D/Cidade) junto com o atlas Pichacoes.png: não mude à mão.
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As pichações do atlas (T_Pichacoes, desenhado em código pelo cidade_pichacoes.py; Plano_Mapa, Parte M9b): o
    /// retângulo de cada uma no atlas (com a margem da névoa e dos escorridos), o tamanho em metros e o tipo.
    /// </summary>
    public static class AtlasDasPichacoes
    {
        /// <summary>
        /// Recado (as frases), Porta (as que vão perto das portas: o X da busca, "DEAD INSIDE"), Marca (as contagens, as
        /// setas e os desenhos), Tag (já colorida) e Assinatura.
        /// </summary>
        public enum Tipo : byte { Recado, Porta, Marca, Tag, Assinatura }

        public readonly struct Item
        {
            public readonly string nome;
            public readonly Tipo tipo;
            /// <summary>No atlas (0 a 1, v de baixo para cima).</summary>
            public readonly Rect uv;
            /// <summary>Em metros, com as margens.</summary>
            public readonly Vector2 tamanho;

            public Item(string nome, Tipo tipo, float u, float v, float du, float dv, float largura, float altura)
            {
                this.nome = nome;
                this.tipo = tipo;
                uv = new Rect(u, v, du, dv);
                tamanho = new Vector2(largura, altura);
            }

            /// <summary>Já vem colorida (não leva a cor da lata).</summary>
            public bool Colorida => tipo == Tipo.Tag;
        }

        public static readonly Item[] Itens =
        {
            new Item("Help", Tipo.Recado, 0.00000f, 0.30273f, 0.25293f, 0.12695f, 1.363f, 0.684f),
            new Item("StayOut", Tipo.Recado, 0.43506f, 0.83838f, 0.29004f, 0.06641f, 1.563f, 0.358f),
            new Item("DeadInside", Tipo.Porta, 0.00000f, 0.16309f, 0.19287f, 0.13770f, 1.039f, 0.742f),
            new Item("DontOpen", Tipo.Porta, 0.25488f, 0.75830f, 0.26123f, 0.07422f, 1.408f, 0.400f),
            new Item("NoFood", Tipo.Recado, 0.51807f, 0.75830f, 0.23291f, 0.07422f, 1.255f, 0.400f),
            new Item("Looters", Tipo.Recado, 0.00000f, 0.55762f, 0.22900f, 0.10889f, 1.234f, 0.587f),
            new Item("TheyHearYou", Tipo.Recado, 0.00000f, 0.91211f, 0.30420f, 0.06348f, 1.639f, 0.342f),
            new Item("AimForTheHead", Tipo.Recado, 0.32373f, 0.43164f, 0.20508f, 0.12158f, 1.105f, 0.655f),
            new Item("GodHelpUs", Tipo.Recado, 0.48145f, 0.66846f, 0.28467f, 0.07861f, 1.534f, 0.424f),
            new Item("WentNorth", Tipo.Recado, 0.49316f, 0.55762f, 0.33789f, 0.09375f, 1.821f, 0.505f),
            new Item("Quarantine", Tipo.Recado, 0.00000f, 0.43164f, 0.32178f, 0.12402f, 1.734f, 0.668f),
            new Item("Evac", Tipo.Recado, 0.25293f, 0.66846f, 0.22656f, 0.08252f, 1.221f, 0.445f),
            new Item("AllDead", Tipo.Recado, 0.18311f, 0.83838f, 0.25000f, 0.06689f, 1.347f, 0.361f),
            new Item("NotSafe", Tipo.Recado, 0.00000f, 0.75830f, 0.25293f, 0.07812f, 1.363f, 0.421f),
            new Item("BurnTheBodies", Tipo.Recado, 0.64062f, 0.43164f, 0.19873f, 0.11230f, 1.071f, 0.605f),
            new Item("KeepOut", Tipo.Recado, 0.00000f, 0.66846f, 0.25098f, 0.08789f, 1.353f, 0.474f),
            new Item("Water", Tipo.Recado, 0.75293f, 0.75830f, 0.21387f, 0.07324f, 1.153f, 0.395f),
            new Item("Checked", Tipo.Porta, 0.30615f, 0.91211f, 0.14551f, 0.05664f, 0.784f, 0.305f),
            new Item("NoAmmo", Tipo.Recado, 0.45361f, 0.91211f, 0.19336f, 0.05664f, 1.042f, 0.305f),
            new Item("OneBitten", Tipo.Porta, 0.00000f, 0.83838f, 0.18115f, 0.07178f, 0.976f, 0.387f),
            new Item("AssDuke", Tipo.Assinatura, 0.64893f, 0.91211f, 0.12158f, 0.04785f, 0.655f, 0.258f),
            new Item("AssRizo", Tipo.Assinatura, 0.87646f, 0.91211f, 0.09424f, 0.04443f, 0.508f, 0.239f),
            new Item("AssK2", Tipo.Assinatura, 0.72705f, 0.83838f, 0.07471f, 0.06445f, 0.403f, 0.347f),
            new Item("AssMoss", Tipo.Assinatura, 0.77246f, 0.91211f, 0.10205f, 0.04541f, 0.550f, 0.245f),
            new Item("BuscaA", Tipo.Porta, 0.39600f, 0.00000f, 0.15332f, 0.15332f, 0.826f, 0.826f),
            new Item("BuscaB", Tipo.Porta, 0.24121f, 0.00000f, 0.15283f, 0.15625f, 0.824f, 0.842f),
            new Item("BuscaC", Tipo.Porta, 0.55127f, 0.00000f, 0.15381f, 0.15283f, 0.829f, 0.824f),
            new Item("Dias7", Tipo.Marca, 0.83301f, 0.55762f, 0.09277f, 0.09082f, 0.500f, 0.489f),
            new Item("Dias23", Tipo.Marca, 0.23096f, 0.55762f, 0.26025f, 0.10156f, 1.403f, 0.547f),
            new Item("Seta", Tipo.Marca, 0.65381f, 0.16309f, 0.19727f, 0.12744f, 1.063f, 0.687f),
            new Item("Caveira", Tipo.Marca, 0.54004f, 0.16309f, 0.11182f, 0.12988f, 0.603f, 0.700f),
            new Item("CarinhaMorta", Tipo.Marca, 0.53076f, 0.43164f, 0.10791f, 0.11426f, 0.582f, 0.616f),
            new Item("TagKaos", Tipo.Tag, 0.70703f, 0.00000f, 0.28809f, 0.14600f, 1.553f, 0.787f),
            new Item("TagVex", Tipo.Tag, 0.00000f, 0.00000f, 0.23926f, 0.16113f, 1.289f, 0.868f),
            new Item("TagR4t", Tipo.Tag, 0.19482f, 0.16309f, 0.20264f, 0.13281f, 1.092f, 0.716f),
            new Item("TagDrone", Tipo.Tag, 0.49121f, 0.30273f, 0.29102f, 0.12598f, 1.568f, 0.679f),
            new Item("TagZk", Tipo.Tag, 0.39941f, 0.16309f, 0.13867f, 0.13184f, 0.747f, 0.711f),
            new Item("TagSkul", Tipo.Tag, 0.25488f, 0.30273f, 0.23438f, 0.12695f, 1.263f, 0.684f),
        };
    }
}
