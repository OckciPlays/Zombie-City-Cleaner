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
        /// <summary>Os nomes das estradas dentro das vilas (nas placas das esquinas).</summary>
        static readonly Dictionary<string, string> ApelidosDasRuas = new Dictionary<string, string>
        {
            { "East Shore Highway", "Main Street" },
            { "North Shore Highway", "Main Street" },
        };

        void MontarAsCidades()
        {
            var pai = new GameObject("Cidades").transform;
            pai.SetParent(_pai, false);
            foreach (var cidade in mapa.cidades)
            {
                if (!mapa.TemLotes(cidade.nome)) continue;
                var vis = new MalhaDeObra();
                var col = new MalhaDeObra();
                string pop = cidade.populacao > 0 ? cidade.populacao.ToString() : "?";
                var resumo = RuasDaCidade.Montar(Chao, cidade.nome, pop, vis, col, ApelidosDasRuas);
                var go = Objeto($"{cidade.nome}: as ruas", vis.ParaMesh($"{cidade.nome} (as ruas)"), materialDaCidade, pai);
                go.AddComponent<MeshCollider>().sharedMesh = col.ParaMesh($"{cidade.nome} (as ruas, colisão)");
                Debug.Log($"[Lakewood] {cidade.nome}: {resumo}; {vis.Triangulos} triângulos.");
            }
            // as obras dos lotes: as caixas de longe e os caminhos (as de verdade vêm perto de alguém)
            MontarAsObras(pai);
        }
    }
}
