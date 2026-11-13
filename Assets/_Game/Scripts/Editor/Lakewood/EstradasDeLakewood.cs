using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A rede de estradas do Lakewood (Plano_Lakewood, Etapa 5, 10/11/2026, a pedido: "só as estradas normais pelo mapa...
    /// estradas que se conectem certinho... algumas estradas vão subir essas montanhas"): o plano das ligações e o menu que
    /// traça cada uma pelo relevo (<see cref="TracadoDeLakewood"/>) e grava os pontos no asset (no lugar das estradas de
    /// antes). Sem as estradas de terra (a pedido) e sem as placas (por enquanto).
    ///  - <b>O anel da rodovia</b> (a SR-27, 6% no máximo): da Lakewood pela margem leste até a Woodcreek, pela margem norte
    ///    (a ponte do estreitamento, a North Crossing) até a Riverside, pela margem oeste até a Fairview e até a estrada
    ///    oeste da barragem; a crista é a travessia; da cabeceira leste, pela margem sul, de volta à Lakewood. Passa pelo
    ///    meio das cidades (as ruas delas, na Etapa 6, se ligam nela).
    ///  - <b>As saídas do mapa:</b> a rodovia do leste (a SR-14, da Lakewood), a estrada do rio (o vale abaixo da barragem,
    ///    para o sul) e a do oeste (da Fairview).
    ///  - <b>As secundárias</b> (9%; as de serra, 10%): o aeroporto, a estação de tratamento, o parque de Pine Ridge, a
    ///    estrada da serra (da Woodcreek até a base, com o ramal da pedreira), e os ramais que saem do anel até o lugar (o
    ///    mirante, a fazenda, as casas de lago, a marina, a serraria e o centro de visitantes).
    /// A saída da garganta da barragem (a South Shore Highway) sobe a 7%. A ponte do norte é a reta mais curta sobre a
    /// baía (<see cref="TracadoDeLakewood.Travessia"/>), e o resto do anel chega e sai nela na direção dela.
    /// </summary>
    public static class EstradasDeLakewood
    {
        /// <summary>Uma ligação do plano: as paradas (a primeira e a última, as pontas), ou um ramal até a estrada "ate".</summary>
        sealed class Ligacao
        {
            public string nome, numero;
            public TipoDeVia tipo = TipoDeVia.Secundaria;
            public float rampa;
            public bool saida;
            public Vector2[] paradas;
            public string ate;
            public Vector2? perto;
            public string lugar;
            /// <summary>A saída do mapa: o trecho da beira (duas pontas) onde ela pode chegar, de frente.</summary>
            public Vector2[] borda;
            /// <summary>
            /// A ponte sobre o lago no caminho (perto deste ponto): a reta mais curta de terra a terra, de leste para oeste; a
            /// estrada vai da primeira parada até uma cabeceira, atravessa e segue da outra até a última, na direção da ponte.
            /// </summary>
            public Vector2? ponte;
        }

        const string Anel = "SR-27";

        static Vector2 Cidade(MapaDeLakewood m, string nome) => m.cidades.Find(c => c.nome == nome).centro;
        static Vector2 Lugar(MapaDeLakewood m, string nome) => m.lugares.Find(l => l.nome == nome).ponto;

        static List<Ligacao> Plano(MapaDeLakewood m, UsinaDeLakewood usina)
        {
            var lakewood = Cidade(m, "Lakewood");
            var woodcreek = Cidade(m, "Woodcreek");
            var riverside = Cidade(m, "Riverside");
            var fairview = Cidade(m, "Fairview");
            var ponteDoNorte = Lugar(m, "North Crossing");
            var damRoad = usina.Vias.Find(v => v.nome == "Lakewood Dam Road");
            var westRoad = usina.Vias.Find(v => v.nome == "Dam West Road");
            var linhaDaDamRoad = MapaDeLakewood.Linha(damRoad.pontos);
            // a cabeceira leste: a rodovia sai da estrada da barragem logo depois da crista (ainda no corte, na altura dela)
            var cabeceiraLeste = TracadoDeLakewood.PontoMaisPerto(usina.Mundo(930f, 6f), linhaDaDamRoad);
            var fimDaDamRoad = linhaDaDamRoad[linhaDaDamRoad.Count - 1];
            var linhaDaWestRoad = MapaDeLakewood.Linha(westRoad.pontos);
            var fimDaWestRoad = linhaDaWestRoad[linhaDaWestRoad.Count - 1];
            return new List<Ligacao>
            {
                // o anel
                new Ligacao { nome = "East Shore Highway", numero = Anel, tipo = TipoDeVia.Rodovia, paradas = new[] { lakewood, woodcreek } },
                new Ligacao { nome = "North Shore Highway", numero = Anel, tipo = TipoDeVia.Rodovia, paradas = new[] { woodcreek, riverside }, ponte = ponteDoNorte },
                new Ligacao { nome = "Fairview Highway", numero = Anel, tipo = TipoDeVia.Rodovia, paradas = new[] { riverside, fairview } },
                new Ligacao { nome = "West Shore Highway", numero = Anel, tipo = TipoDeVia.Rodovia, paradas = new[] { fairview, fimDaWestRoad } },
                // a saída da garganta da barragem sobe a 7% (a rampa de serra: a cabeceira é uma encosta em pé)
                new Ligacao { nome = "South Shore Highway", numero = Anel, tipo = TipoDeVia.Rodovia, rampa = 0.07f, paradas = new[] { cabeceiraLeste, lakewood } },
                // as saídas do mapa: chegam de frente num trecho da beira
                new Ligacao { nome = "Eastern Highway", numero = "SR-14", tipo = TipoDeVia.Rodovia, saida = true, paradas = new[] { lakewood },
                              borda = new[] { new Vector2(m.largura - 6f, 2300f), new Vector2(m.largura - 6f, 4700f) } },
                new Ligacao { nome = "River Valley Road", saida = true, paradas = new[] { fimDaDamRoad }, borda = new[] { new Vector2(3900f, 6f), new Vector2(5600f, 6f) } },
                new Ligacao { nome = "Fairview West Road", saida = true, paradas = new[] { fairview }, borda = new[] { new Vector2(6f, 5300f), new Vector2(6f, 7300f) } },
                // as secundárias até os lugares
                new Ligacao { nome = "Airport Road", paradas = new[] { lakewood, Lugar(m, "Lakewood Regional Airport") }, lugar = "Lakewood Regional Airport" },
                new Ligacao { nome = "Treatment Plant Road", paradas = new[] { fimDaDamRoad, Lugar(m, "Water Treatment Plant") }, lugar = "Water Treatment Plant" },
                new Ligacao { nome = "Pine Ridge Road", rampa = 0.1f, paradas = new[] { riverside, Lugar(m, "Pine Ridge State Park") }, lugar = "Pine Ridge State Park" },
                // a estrada da serra: da Woodcreek pelo vale até a base, e o ramal da pedreira saindo dela
                new Ligacao { nome = "Crowley Road", rampa = 0.1f, paradas = new[] { woodcreek, Lugar(m, "Fort Crowley") }, lugar = "Fort Crowley" },
                new Ligacao { nome = "Quarry Road", rampa = 0.1f, paradas = new[] { Lugar(m, "Crowley Quarry") }, ate = "Crowley Road", lugar = "Crowley Quarry" },
                // os ramais: do lugar até o anel
                new Ligacao { nome = "Lookout Road", rampa = 0.1f, paradas = new[] { Lugar(m, "East Lookout") }, ate = "East Shore Highway", lugar = "East Lookout" },
                new Ligacao { nome = "Sullivan Farms Road", paradas = new[] { Cidade(m, "Sullivan Farms") }, ate = "West Shore Highway" },
                new Ligacao { nome = "Westshore Road", paradas = new[] { Lugar(m, "Westshore Cottages") }, ate = "West Shore Highway", lugar = "Westshore Cottages" },
                new Ligacao { nome = "Marina Road", paradas = new[] { Lugar(m, "North Arm Marina") }, ate = "North Shore Highway", lugar = "North Arm Marina" },
                new Ligacao { nome = "Sawmill Road", paradas = new[] { Lugar(m, "Woodcreek Sawmill") }, ate = "East Shore Highway", lugar = "Woodcreek Sawmill" },
                new Ligacao { nome = "Visitor Center Road", paradas = new[] { Lugar(m, "Visitor Center") }, ate = "North Shore Highway", lugar = "Visitor Center" },
            };
        }

        [MenuItem("Zombie City Cleaner/Lakewood/Etapa 5: traçar as estradas", false, 5)]
        public static void TracarEGravar()
        {
            var m = AssetDatabase.LoadAssetAtPath<MapaDeLakewood>(MapaDeLakewood.Caminho);
            if (m == null)
            {
                Debug.LogError("[Lakewood] Sem o asset do mapa.");
                return;
            }
            var t0 = Time.realtimeSinceStartup;
            var relatorio = new StringBuilder();
            var vias = Tracar(m, relatorio);
            if (vias == null) return;
            Undo.RecordObject(m, "Traçar as estradas do Lakewood");
            m.estradas = vias;
            // o nome da ponte do lago (o mapa pintado põe o nome na ponte mais perto do ponto)
            if (!m.pontes.Exists(p => p.nome == "North Crossing"))
                m.pontes.Add(new PonteDoMapa { nome = "North Crossing", perto = Lugar(m, "North Crossing"), estado = EstadoDaPonte.Inteira });
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Lakewood] Estradas traçadas em {Time.realtimeSinceStartup - t0:F1} s: {vias.Count} estradas.\n{relatorio}");
        }

        /// <summary>Traça o plano todo (sem gravar) e devolve as vias, ou null se faltar um caminho.</summary>
        public static List<ViaDoMapa> Tracar(MapaDeLakewood m, StringBuilder relatorio)
        {
            // o chão sem as estradas de antes (as da usina ficam: elas são do código)
            var copia = m.Copia();
            copia.estradas = new List<ViaDoMapa>();
            var relevo = new RelevoDeLakewood(copia);
            var chao = new ChaoDeLakewood(copia, relevo);
            // (a ponte do norte é uma reta à parte: a busca não atravessa o lago em lugar nenhum)
            var tracado = new TracadoDeLakewood(chao, new List<TracadoDeLakewood.CorredorDePonte>());
            foreach (var v in chao.Usina.Vias) tracado.Marcar(MapaDeLakewood.Linha(v.pontos));
            var vias = new List<ViaDoMapa>();
            foreach (var lg in Plano(copia, chao.Usina))
            {
                var t = Time.realtimeSinceStartup;
                var pedido = new TracadoDeLakewood.Pedido
                {
                    Rampa = lg.rampa > 0f ? lg.rampa : lg.tipo == TipoDeVia.Rodovia ? 0.06f : 0.09f,
                    Saida = lg.saida,
                    LugarDe = -1,
                    LugarPara = string.IsNullOrEmpty(lg.lugar) ? -1 : copia.lugares.FindIndex(l => l.nome == lg.lugar),
                };
                List<Vector2> pontos;
                var paradas = new List<Vector2>(lg.paradas);
                // a ponta no lugar fica em terra seca (a marina é na beira da água); as outras (as cidades, as estradas da
                // usina, as saídas) ficam onde estão
                if (lg.lugar != null)
                {
                    int k = lg.ate != null ? 0 : paradas.Count - 1;
                    paradas[k] = Seco(chao, paradas[k]);
                }
                if (lg.ate != null)
                {
                    var host = vias.Find(v => v.nome == lg.ate);
                    if (host == null)
                    {
                        Debug.LogError($"[Lakewood] {lg.nome}: a estrada {lg.ate} não foi traçada antes.");
                        return null;
                    }
                    pontos = tracado.TracarAte(paradas[0], MapaDeLakewood.Linha(host.pontos), pedido, lg.perto);
                }
                else if (lg.borda != null) pontos = tracado.TracarAte(paradas[0], lg.borda, pedido, null, 600f, true);
                else if (lg.ponte.HasValue)
                {
                    // as cabeceiras: a reta mais curta sobre a baía (de pelo menos 90 m), de leste para oeste
                    if (!TracadoDeLakewood.Travessia(chao, lg.ponte.Value, 320f, Vector2.left, 90f, out var leste, out var oeste))
                    {
                        Debug.LogError($"[Lakewood] {lg.nome}: sem travessia perto de ({lg.ponte.Value.x:F0}; {lg.ponte.Value.y:F0}).");
                        return null;
                    }
                    var dir = (oeste - leste).normalized;
                    var ate = tracado.Tracar(new[] { paradas[0], leste }, pedido, null, dir);
                    var depois = tracado.Tracar(new[] { oeste, paradas[paradas.Count - 1] }, pedido, dir, null);
                    pontos = null;
                    if (ate != null && depois != null)
                    {
                        pontos = new List<Vector2>(ate);
                        float vao = Vector2.Distance(leste, oeste);
                        int partes = Mathf.Max(1, Mathf.CeilToInt(vao / TracadoDeLakewood.EntrePontos));
                        for (int k = 1; k <= partes; k++) pontos.Add(Vector2.Lerp(leste, oeste, k / (float)partes));
                        for (int k = 1; k < depois.Count; k++) pontos.Add(depois[k]);
                        relatorio.AppendLine($"  a ponte de {lg.nome}: de ({leste.x:F0}; {leste.y:F0}) a ({oeste.x:F0}; {oeste.y:F0}), {vao:F0} m de cabeceira a cabeceira.");
                    }
                }
                else pontos = tracado.Tracar(paradas, pedido);
                if (pontos == null)
                {
                    Debug.LogError($"[Lakewood] {lg.nome}: sem caminho, de ({paradas[0].x:F0}; {paradas[0].y:F0}).\n{tracado.Diagnostico(paradas[0], 8)}");
                    return null;
                }
                var via = new ViaDoMapa { nome = lg.nome, numero = lg.numero ?? "", tipo = lg.tipo, pontos = pontos, rampaMaxima = lg.rampa };
                vias.Add(via);
                tracado.Marcar(MapaDeLakewood.Linha(pontos));
                relatorio.AppendLine($"{lg.nome}: {Linhas.Comprimento(MapaDeLakewood.Linha(pontos)) / 1000f:F2} km, {pontos.Count} pontos, {tracado.Abertos} estados, {Time.realtimeSinceStartup - t:F1} s.");
            }
            return vias;
        }

        // o ponto seco mais perto (fora do lago e dos rios), até 300 m
        static Vector2 Seco(ChaoDeLakewood chao, Vector2 p)
        {
            bool Molhado(Vector2 q) => chao.AguaDoLagoEm(q.x, q.y, out _, 18f) || (chao.RioMaisPerto(q.x, q.y, 12f, out var r) && r.Dist < r.Meia + 12f);
            if (!Molhado(p)) return p;
            for (float raio = 12f; raio <= 300f; raio += 12f)
                for (int k = 0; k < 24; k++)
                {
                    float a = k * Mathf.PI * 2f / 24f;
                    var q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * raio;
                    if (!Molhado(q)) return q;
                }
            return p;
        }
    }
}
