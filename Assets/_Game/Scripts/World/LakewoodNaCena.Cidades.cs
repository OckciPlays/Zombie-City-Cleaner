using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// As cidades do Lakewood na cena (Plano_Lakewood, Etapa 6): o que fica na rua (a calçada do comércio, as placas das
    /// esquinas e das entradas e os postes: <see cref="RuasDaCidade"/>), numa malha só por cidade, com a colisão. As ruas
    /// novas são pedaços da rede das estradas (o MontarEstradas), e o chão dos lotes é o do <see cref="ChaoDeLakewood"/>.
    /// </summary>
    public partial class LakewoodNaCena
    {
        /// <summary>Os nomes das estradas dentro de cada cidade (nas placas das esquinas).</summary>
        static readonly Dictionary<string, Dictionary<string, string>> ApelidosDasRuas = new Dictionary<string, Dictionary<string, string>>
        {
            { "Woodcreek", new Dictionary<string, string> { { "East Shore Highway", "Main Street" }, { "North Shore Highway", "Main Street" } } },
            // a cidade de Lakewood (23/11/2026): o boulevard de norte a sul e a avenida do meio
            { "Lakewood", new Dictionary<string, string> { { "Airport Road", "Lakewood Boulevard" }, { "East Shore Highway", "Lakewood Boulevard" }, { "Eastern Highway", "Central Avenue" } } },
        };

        // o que a versão de antes das ruas de cada cidade precisa (montada na primeira vez que a época vira para o antes)
        readonly List<(string cidade, string pop, Transform pai)> _ruasPorFazer = new List<(string, string, Transform)>();

        /// <summary>O lado dos pedaços das ruas de uma cidade (m): cada um com o desenho e a colisão dele.</summary>
        const float PedacoDasRuas = 256f;

        void MontarAsCidades()
        {
            var pai = new GameObject("Cidades").transform;
            pai.SetParent(_pai, false);
            foreach (var cidade in mapa.cidades)
            {
                if (!mapa.TemLotes(cidade.nome)) continue;
                string pop = cidade.populacao > 0 ? cidade.populacao.ToString() : "?";
                // as duas versões (22/11/2026, a Woodcreek de antes): a de depois e a de antes do apocalipse (os postes novos
                // com a lente acesa e a placa sem o risco), uma ligada de cada vez (LakewoodNaCena.Epoca.cs). A de antes só
                // quando a época vira (23/11/2026: com a cidade de Lakewood, as duas no Awake levavam uns 4 s)
                var depois = MontarAsRuas(cidade.nome, pop, pai, false);
                _ruasDasCidades.Add((depois, null));
                _ruasPorFazer.Add((cidade.nome, pop, pai));
            }
            // as obras dos lotes: as caixas de longe e os caminhos (as de verdade vêm perto de alguém)
            MontarAsObras(pai);
            MontarAMarina(pai);
        }

        // a marina da cidade de Lakewood (23/11/2026): o píer, os barcos e a rampa na beira da baía, no fim da Marina Road
        // (a loja e o estacionamento são lotes da cidade)
        void MontarAMarina(Transform pai)
        {
            var lugar = mapa.lugares.Find(l => l.nome == "Lakewood Marina");
            var rua = mapa.ruas?.Find(r => r.nome == "Marina Road");
            if (lugar == null || rua == null || rua.pontos.Count < 2) return;
            var vis = new MalhaDeObra();
            var col = new MalhaDeObra();
            if (!MarinaDeLakewood.Montar(Chao, lugar.ponto, rua.pontos[rua.pontos.Count - 1], mapa.semente, vis, col, out var resumo))
            {
                Debug.LogWarning("[Lakewood] A marina ficou sem o píer: não há água perto do lugar dela.");
                return;
            }
            var go = Objeto("Lakewood Marina: o píer e os barcos", vis.ParaMesh("Marina (o píer e os barcos)"), materialDaCidade, pai);
            go.AddComponent<MeshCollider>().sharedMesh = col.ParaMesh("Marina (colisão)");
            Debug.Log($"[Lakewood] A marina: {resumo}; {vis.Triangulos} triângulos.");
        }

        GameObject MontarAsRuas(string cidade, string pop, Transform pai, bool nova)
        {
            RuasDaCidade.Nova = nova;
            RuasDaCidade.LuzesDosPostes.Clear();
            try
            {
                var vis = new MalhaDeObra();
                var col = new MalhaDeObra();
                var resumo = RuasDaCidade.Montar(Chao, cidade, pop, vis, col, ApelidosDasRuas.TryGetValue(cidade, out var apelidos) ? apelidos : null);
                string nome = nova ? $"{cidade}: as ruas (de antes)" : $"{cidade}: as ruas";
                // em pedaços de 256 m (23/11/2026: a da cidade de Lakewood inteira passava de 2 milhões de triângulos)
                var go = new GameObject(nome);
                go.transform.SetParent(pai, false);
                var pedacosVis = vis.Dividir(PedacoDasRuas);
                var pedacosCol = col.Dividir(PedacoDasRuas);
                var chaves = new HashSet<(int, int)>(pedacosVis.Keys);
                chaves.UnionWith(pedacosCol.Keys);
                foreach (var k in chaves)
                {
                    string n = $"{nome} ({k.Item1}, {k.Item2})";
                    GameObject parte;
                    if (pedacosVis.TryGetValue(k, out var pv)) parte = Objeto(n, pv.ParaMesh(n), materialDaCidade, go.transform);
                    else
                    {
                        parte = new GameObject(n);
                        parte.transform.SetParent(go.transform, false);
                    }
                    if (pedacosCol.TryGetValue(k, out var pc)) parte.AddComponent<MeshCollider>().sharedMesh = pc.ParaMesh($"{n} (colisão)");
                }
                if (!nova) Debug.Log($"[Lakewood] {cidade}: {resumo}; {vis.Triangulos} triângulos ({col.Triangulos} na colisão) em {chaves.Count} pedaços.");
                else _luzesDosPostes.AddRange(RuasDaCidade.LuzesDosPostes);
                return go;
            }
            finally
            {
                RuasDaCidade.Nova = false;
            }
        }
    }
}
