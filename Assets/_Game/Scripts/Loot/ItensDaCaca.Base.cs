namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// As caixinhas dos itens da base (24/11/2026, a pedido: o núcleo da base e os troféus dos zumbis especiais que sobem o
    /// nível dele), provisórias como as da caça, até o Pedro trocar os modelos: o olho do Brutamonte, a garganta do Long, a
    /// cabeça de Oco e a de chefe; e os kits do núcleo (o braseiro de sucata), do muro e do portão de toras (o feixe de toras).
    /// As cores são células da paleta já usadas pela caça (CorDaPaleta).
    /// </summary>
    public static partial class ItensDaCaca
    {
        // o olho: o globo branco com a íris vermelha e o nervo
        static Caixa[] Olho() => new[]
        {
            C(0f, 0.06f, 0f, 0.12f, 0.12f, 0.12f, 10, 9, 45f),
            C(0f, 0.06f, 0f, 0.11f, 0.13f, 0.11f, 10, 9),
            C(0f, 0.065f, -0.062f, 0.06f, 0.06f, 0.006f, 6, 11),
            C(0f, 0.065f, -0.066f, 0.026f, 0.026f, 0.004f, 2, 3),
            C(0f, 0.055f, 0.075f, 0.03f, 0.03f, 0.05f, 10, 3),
        };

        // a garganta: o papo rosado de gosma e as pontas cortadas
        static Caixa[] Garganta() => new[]
        {
            C(0f, 0.05f, 0f, 0.08f, 0.1f, 0.22f, 13, 11),
            C(0f, 0.05f, 0f, 0.1f, 0.08f, 0.16f, 13, 11, 45f),
            C(0f, 0.05f, 0.13f, 0.05f, 0.06f, 0.06f, 10, 3),
            C(0f, 0.05f, -0.12f, 0.06f, 0.07f, 0.05f, 10, 3),
        };

        // a cabeça de Oco: cinzenta, os olhos fundos e a boca aberta, o pescoço cortado
        static Caixa[] CabecaDoOco() => new[]
        {
            C(0f, 0.13f, 0f, 0.18f, 0.22f, 0.2f, 3, 10),
            C(0f, 0.23f, 0.01f, 0.16f, 0.04f, 0.17f, 3, 10),
            C(0.045f, 0.16f, -0.101f, 0.035f, 0.025f, 0.006f, 2, 3),
            C(-0.045f, 0.16f, -0.101f, 0.035f, 0.025f, 0.006f, 2, 3),
            C(0f, 0.08f, -0.101f, 0.07f, 0.022f, 0.006f, 10, 3),
            C(0f, 0.01f, 0.01f, 0.09f, 0.03f, 0.09f, 10, 3),
        };

        // a cabeça de chefe: maior, as placas escuras e os olhos vermelhos
        static Caixa[] CabecaDeChefe() => new[]
        {
            C(0f, 0.17f, 0f, 0.28f, 0.32f, 0.3f, 2, 8),
            C(0f, 0.33f, 0.02f, 0.3f, 0.06f, 0.24f, 1, 3),
            C(0.15f, 0.22f, 0.02f, 0.04f, 0.16f, 0.2f, 1, 3),
            C(-0.15f, 0.22f, 0.02f, 0.04f, 0.16f, 0.2f, 1, 3),
            C(0.06f, 0.21f, -0.151f, 0.05f, 0.03f, 0.006f, 6, 11),
            C(-0.06f, 0.21f, -0.151f, 0.05f, 0.03f, 0.006f, 6, 11),
            C(0f, 0.1f, -0.151f, 0.12f, 0.03f, 0.006f, 10, 3),
            C(0f, 0.01f, 0.01f, 0.14f, 0.03f, 0.14f, 10, 3),
        };

        // o kit do núcleo: o braseiro de sucata (a bacia, a borda, as três pernas) com a lenha dentro
        static Caixa[] KitDoNucleo() => new[]
        {
            C(0f, 0.22f, 0f, 0.46f, 0.14f, 0.46f, 4, 3),
            C(0f, 0.22f, 0f, 0.46f, 0.14f, 0.46f, 4, 3, 45f),
            C(0f, 0.3f, 0f, 0.5f, 0.03f, 0.5f, 1, 3),
            C(0.17f, 0.08f, 0.1f, 0.04f, 0.16f, 0.04f, 1, 3),
            C(-0.17f, 0.08f, 0.1f, 0.04f, 0.16f, 0.04f, 1, 3),
            C(0f, 0.08f, -0.19f, 0.04f, 0.16f, 0.04f, 1, 3),
            C(0f, 0.33f, 0f, 0.36f, 0.06f, 0.07f, 7, 3, 20f),
            C(0f, 0.34f, 0f, 0.34f, 0.06f, 0.07f, 8, 3, -40f),
            C(0.06f, 0.3f, -0.02f, 0.12f, 0.05f, 0.12f, 7, 2),
        };

        // o kit do muro (e do portão): o feixe de toras amarradas (o portão leva as dobradiças de metal)
        static Caixa[] KitDoMuro(bool portao)
        {
            var lista = new System.Collections.Generic.List<Caixa>
            {
                C(0f, 0.05f, -0.1f, 0.9f, 0.1f, 0.1f, 7, 3),
                C(0f, 0.05f, 0f, 0.9f, 0.1f, 0.1f, 8, 3),
                C(0f, 0.05f, 0.1f, 0.9f, 0.1f, 0.1f, 7, 3),
                C(0f, 0.14f, -0.05f, 0.88f, 0.09f, 0.09f, 8, 3),
                C(0f, 0.14f, 0.05f, 0.88f, 0.09f, 0.09f, 7, 3),
                C(-0.3f, 0.1f, 0f, 0.03f, 0.2f, 0.32f, 13, 4),
                C(0.3f, 0.1f, 0f, 0.03f, 0.2f, 0.32f, 13, 4),
            };
            if (portao)
            {
                lista.Add(C(-0.15f, 0.19f, 0f, 0.12f, 0.02f, 0.06f, 4, 3));
                lista.Add(C(0.15f, 0.19f, 0f, 0.12f, 0.02f, 0.06f, 4, 3));
            }
            return lista.ToArray();
        }

        // o kit da mesa de ferramentas (25/11/2026): as tábuas empilhadas, amarradas, com o martelo, a serra e a caixinha de
        // pregos por cima
        static Caixa[] KitDaMesa() => new[]
        {
            C(0f, 0.02f, -0.12f, 0.8f, 0.04f, 0.14f, 8, 3),
            C(0f, 0.02f, 0.03f, 0.8f, 0.04f, 0.14f, 7, 3),
            C(0f, 0.06f, -0.05f, 0.78f, 0.04f, 0.14f, 7, 3, 3f),
            C(0f, 0.06f, 0.1f, 0.78f, 0.04f, 0.12f, 8, 3, -2f),
            C(-0.28f, 0.05f, 0f, 0.03f, 0.1f, 0.34f, 13, 4),
            C(0.28f, 0.05f, 0f, 0.03f, 0.1f, 0.34f, 13, 4),
            // o martelo: o cabo e a cabeça
            C(-0.08f, 0.095f, -0.02f, 0.26f, 0.025f, 0.03f, 8, 3, 20f),
            C(0.04f, 0.1f, -0.06f, 0.035f, 0.04f, 0.09f, 1, 3, 20f),
            // a serra: a lâmina e o cabo
            C(0.12f, 0.087f, 0.08f, 0.3f, 0.006f, 0.08f, 2, 3, -10f),
            C(0.3f, 0.092f, 0.11f, 0.07f, 0.02f, 0.06f, 7, 3, -10f),
            // a caixinha de pregos
            C(-0.25f, 0.105f, 0.09f, 0.08f, 0.05f, 0.06f, 4, 3),
        };
    }
}
