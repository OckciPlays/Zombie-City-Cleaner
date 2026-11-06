// Escrito pela Zombie City Cleaner > Mobília nova > Escrever os lugares dos móveis antigos (MontagemDosLugaresDoSaque): não mude à mão.
using System.Collections.Generic;

namespace ZombieCityCleaner.Mundo
{
    public static partial class LugaresDoSaque
    {
        /// <summary>
        /// Os lugares de um móvel antigo (o do Cidade_Mobilia.fbx, no tamanho em que ele foi feito: Mobilia.Feitos), só à
        /// vista (os antigos não abrem); nenhum, se ele não está na tabela. Só a farmácia e o hospital usam (SaqueDaObra).
        /// </summary>
        public static LugarDoSaque[] DoAntigo(string modelo) => modelo != null && _antigos.TryGetValue(modelo, out var l) ? l : Nenhum;

        public static IEnumerable<string> NomesDosAntigos => _antigos.Keys;

        // o modelo antigo: os lugares (o meio, a largura, a altura livre e o fundo), no tamanho em que ele foi feito
        static readonly Dictionary<string, LugarDoSaque[]> _antigos = new Dictionary<string, LugarDoSaque[]>
        {
            ["Balcao"] = new[]
            {
                L(0.36f, 1.1f, 0.18f, 1.12f, 0.6f, 0.32f),
                L(-0.74f, 1.1f, 0.02f, 0.36f, 0.6f, 0.64f),
                L(0.19f, 1.1f, -0.24f, 1.46f, 0.6f, 0.12f),
                L(0.07f, 1.1f, -0.08f, 0.54f, 0.6f, 0.16f),
                L(-0.41f, 1.116f, -0.03f, 0.28f, 0.6f, 0.2f),
                L(-0.27f, 1.108f, 0.11f, 0.1f, 0.6f, 0.08f),
            },
            ["Caixas_A"] = new[]
            {
                L(-0.022f, 0.6f, -0.021f, 0.32f, 0.6f, 0.3f),
                L(0.23f, 0.33f, 0f, 0.1f, 0.243f, 0.52f),
            },
            ["Caixas_B"] = new[]
            {
                L(0.23f, 0.33f, 0f, 0.1f, 0.6f, 0.52f),
                L(-0.134f, 0.6f, 0.015f, 0.14f, 0.6f, 0.36f),
                L(0.086f, 0.6f, 0.025f, 0.1f, 0.6f, 0.18f),
                L(0.1f, 0.33f, -0.19f, 0.12f, 0.267f, 0.14f),
            },
            ["CriadoMudo"] = new[]
            {
                L(0.1f, 0.55f, 0f, 0.22f, 0.198f, 0.42f),
                L(-0.12f, 0.55f, 0.11f, 0.18f, 0.198f, 0.2f),
                L(-0.12f, 0.55f, -0.16f, 0.18f, 0.198f, 0.1f),
            },
            ["EstanteDeAco"] = new[]
            {
                L(-0.48f, 0.22f, 0f, 0.7f, 0.6f, 0.48f),
                L(0.126f, 0.44f, -0.005f, 0.48f, 0.558f, 0.4f),
                L(-0.193f, 1.368f, -0.027f, 0.48f, 0.43f, 0.4f),
                L(0.21f, 1.02f, 0f, 0.32f, 0.6f, 0.48f),
                L(0.585f, 1.401f, 0.034f, 0.36f, 0.397f, 0.38f),
                L(0.593f, 0.455f, -0.037f, 0.34f, 0.543f, 0.36f),
                L(-0.635f, 1.293f, -0.017f, 0.3f, 0.505f, 0.4f),
            },
            ["Gondola"] = new[]
            {
                L(0.23f, 0.61f, -0.36f, 0.4f, 0.428f, 1.26f),
                L(-0.23f, 0.61f, -0.76f, 0.4f, 0.428f, 1.1f),
                L(-0.23f, 0.61f, 0.49f, 0.4f, 0.428f, 0.92f),
                L(-0.23f, 1.06f, -0.85f, 0.4f, 0.6f, 0.92f),
                L(0.23f, 1.06f, 0.93f, 0.4f, 0.6f, 0.76f),
                L(0.23f, 1.06f, -0.54f, 0.4f, 0.6f, 0.74f),
                L(-0.24f, 0.12f, 0.54f, 0.42f, 0.468f, 0.7f),
                L(0.24f, 0.12f, 0.76f, 0.42f, 0.468f, 0.62f),
            },
            ["MesaDeEscritorio"] = new[]
            {
                L(0.58f, 0.75f, 0f, 0.22f, 0.6f, 0.68f),
                L(-0.52f, 0.75f, -0.14f, 0.34f, 0.6f, 0.4f),
                L(0.15f, 0.75f, 0f, 0.16f, 0.6f, 0.68f),
                L(-0.15f, 0.5f, 0.48f, 0.36f, 0.216f, 0.28f),
                L(-0.53f, 0.75f, 0.24f, 0.32f, 0.6f, 0.2f),
                L(0.35f, 0.758f, 0.05f, 0.2f, 0.6f, 0.28f),
            },
            ["PiaDeBanheiro"] = new[]
            {
                L(0f, 0.85f, 0.01f, 0.54f, 0.131f, 0.36f),
            },
            ["Prateleira"] = new[]
            {
                L(-0.36f, 0.113f, 0f, 0.4f, 0.523f, 0.44f),
                L(0.18f, 0.663f, 0f, 0.36f, 0.523f, 0.44f),
                L(-0.4f, 0.663f, 0f, 0.32f, 0.523f, 0.44f),
                L(0.44f, 1.213f, 0f, 0.24f, 0.523f, 0.44f),
                L(-0.45f, 1.213f, 0f, 0.22f, 0.523f, 0.44f),
                L(-0.13f, 1.213f, 0f, 0.18f, 0.523f, 0.44f),
                L(-0.044f, 0.313f, 0.02f, 0.2f, 0.323f, 0.32f),
                L(0.46f, 0.958f, 0.02f, 0.18f, 0.228f, 0.34f),
            },
            ["Prateleira_Farmacia"] = new[]
            {
                L(-0.08f, 0.12f, 0.02f, 0.96f, 0.413f, 0.36f),
                L(0.3f, 0.555f, 0.01f, 0.52f, 0.403f, 0.34f),
                L(-0.36f, 0.555f, 0.01f, 0.4f, 0.403f, 0.34f),
                L(0.13f, 0.98f, 0.01f, 0.38f, 0.403f, 0.34f),
                L(-0.37f, 1.405f, 0.01f, 0.38f, 0.403f, 0.34f),
                L(-0.42f, 0.98f, 0.01f, 0.28f, 0.403f, 0.34f),
                L(0.22f, 1.405f, 0.01f, 0.28f, 0.403f, 0.34f),
                L(0.442f, 1.106f, 0.02f, 0.22f, 0.277f, 0.3f),
            },
            ["Prateleira_Rouparia"] = new[]
            {
                L(0.17f, 0.98f, 0.01f, 0.42f, 0.403f, 0.38f),
                L(-0.37f, 0.98f, 0.01f, 0.38f, 0.403f, 0.38f),
                L(-0.12f, 0.12f, 0.02f, 0.36f, 0.413f, 0.4f),
                L(0.33f, 0.12f, 0.02f, 0.22f, 0.413f, 0.4f),
                L(0.46f, 0.555f, 0.01f, 0.2f, 0.403f, 0.38f),
                L(-0.03f, 0.555f, 0.01f, 0.18f, 0.403f, 0.38f),
                L(-0.314f, 1.516f, 0.02f, 0.2f, 0.292f, 0.34f),
                L(0.157f, 0.668f, 0.02f, 0.16f, 0.29f, 0.36f),
            },
        };
    }
}
