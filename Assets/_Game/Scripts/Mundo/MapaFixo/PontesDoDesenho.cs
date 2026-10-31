using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>Uma ponte achada no desenho: onde a via cruza o rio, o tamanho e o que o desenho diz dela.</summary>
    public struct PonteCalculada
    {
        public string Nome;
        public Vector2 Ponto;
        /// <summary>A direção da via na ponte.</summary>
        public Vector2 Direcao;
        /// <summary>De uma cabeceira à outra (m): a largura do rio mais as margens.</summary>
        public float Comprimento;
        public float LarguraDoRio;
        /// <summary>A via (o nome da estrada, da avenida, ou "ferrovia") e o tipo dela (null na ferrovia).</summary>
        public string Via;
        public TipoDeVia? TipoDaVia;
        public bool DaCidade;
        public string Rio;
        public TipoDePonte Tipo;
        public EstadoDaPonte Estado;
    }

    /// <summary>
    /// As pontes do desenho (Plano_Mapa_Fixo, Parte F1; as malhas delas são da F5): cada cruzamento de uma via (as estradas,
    /// as avenidas das cidades e a ferrovia) com um rio vira uma ponte. O tipo sai da via e da largura do rio (a de terra é
    /// de madeira, a da ferrovia é a treliça, a do rio estreito é a pequena, a perto da represa é a dela; o resto, de vigas),
    /// e o que o desenho diz (<see cref="DesenhoDoMapa.pontes"/>: o nome, o tipo e o estado) vale para o cruzamento mais
    /// perto do ponto dado, até 250 m. Código puro: pode rodar em outra thread.
    /// </summary>
    public static class PontesDoDesenho
    {
        public const float AlcanceDoDesenho = 250f;

        public static List<PonteCalculada> Calcular(DesenhoDoMapa d) => Calcular(d, out _);

        /// <summary>As pontes, e as do desenho que não acharam cruzamento (os índices em <see cref="DesenhoDoMapa.pontes"/>).</summary>
        public static List<PonteCalculada> Calcular(DesenhoDoMapa d, out List<int> semCruzamento)
        {
            var lista = new List<PonteCalculada>();
            var rios = new List<(RioDoDesenho rio, List<Vector2> linha)>();
            foreach (var r in d.rios)
                if (r.pontos.Count >= 2) rios.Add((r, DesenhoDoMapa.Linha(r.pontos)));
            var represas = new List<Vector2>();
            foreach (var l in d.lugares)
                if (l.tipo == TipoDeLugarFixo.Represa) represas.Add(l.ponto);

            void Cruzar(List<Vector2> via, string nome, TipoDeVia? tipo, bool daCidade)
            {
                foreach (var (rio, linha) in rios)
                {
                    float total = Mathf.Max(Linhas.Comprimento(linha), 1f), acum = 0f;
                    for (int s = 1; s < linha.Count; s++)
                    {
                        var a = linha[s - 1];
                        var b = linha[s];
                        float l = Vector2.Distance(a, b);
                        for (int v = 1; v < via.Count; v++)
                        {
                            if (!Linhas.Cruzam(via[v - 1], via[v], a, b, out var p, out _, out float tr)) continue;
                            // o mesmo cruzamento achado em dois segmentos seguidos (a ponta de um é o começo do outro)
                            bool repetido = false;
                            foreach (var j in lista) repetido |= j.Via == nome && j.Rio == rio.nome && Vector2.Distance(j.Ponto, p) < 40f;
                            if (repetido) continue;
                            float fr = (acum + l * tr) / total;
                            float largura = DesenhoDoMapa.LarguraDoRio(rio, fr);
                            var dir = (via[v] - via[v - 1]).normalized;
                            var dirRio = (b - a).normalized;
                            // a via torta em relação ao rio atravessa mais água
                            float seno = Mathf.Max(0.35f, Mathf.Abs(dir.x * dirRio.y - dir.y * dirRio.x));
                            var tipoDaPonte = tipo == TipoDeVia.Terra ? TipoDePonte.Madeira
                                            : tipo == null ? TipoDePonte.Trelica
                                            : largura < 22f ? TipoDePonte.Pequena : TipoDePonte.Vigas;
                            foreach (var rp in represas)
                                if (Vector2.Distance(rp, p) < 200f) tipoDaPonte = TipoDePonte.Represa;
                            lista.Add(new PonteCalculada
                            {
                                Nome = "",
                                Ponto = p,
                                Direcao = dir,
                                LarguraDoRio = largura,
                                Comprimento = largura / seno + 30f,
                                Via = nome,
                                TipoDaVia = tipo,
                                DaCidade = daCidade,
                                Rio = rio.nome,
                                Tipo = tipoDaPonte,
                                Estado = EstadoDaPonte.Inteira,
                            });
                        }
                        acum += l;
                    }
                }
            }

            foreach (var e in d.estradas)
                if (e.pontos.Count >= 2) Cruzar(DesenhoDoMapa.Linha(e.pontos), e.nome, e.tipo, false);
            foreach (var c in d.cidades)
            foreach (var a in c.avenidas)
                if (a.pontos.Count >= 2) Cruzar(DesenhoDoMapa.Linha(a.pontos), a.nome, TipoDeVia.Condado, true);
            if (d.ferrovia.Count >= 2) Cruzar(DesenhoDoMapa.Linha(d.ferrovia), "ferrovia", null, false);

            // o que o desenho diz de cada uma
            semCruzamento = new List<int>();
            for (int k = 0; k < d.pontes.Count; k++)
            {
                var pd = d.pontes[k];
                int melhor = -1;
                float perto = AlcanceDoDesenho;
                for (int i = 0; i < lista.Count; i++)
                {
                    float dist = Vector2.Distance(lista[i].Ponto, pd.perto);
                    if (dist < perto)
                    {
                        perto = dist;
                        melhor = i;
                    }
                }
                if (melhor < 0)
                {
                    semCruzamento.Add(k);
                    continue;
                }
                var p = lista[melhor];
                if (!string.IsNullOrEmpty(pd.nome)) p.Nome = pd.nome;
                if (pd.tipo != TipoDePonte.Automatica) p.Tipo = pd.tipo;
                p.Estado = pd.estado;
                lista[melhor] = p;
            }
            return lista;
        }
    }
}
