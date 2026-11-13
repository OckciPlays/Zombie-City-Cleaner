using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os carros abandonados do Lakewood (12/11/2026, o Plano_Lakewood, "O jogo do mundo sorteado no Lakewood", a parte 4), em
    /// dados, iguais em todas as máquinas (a semente do mapa): os mesmos do mundo sorteado (<see cref="ObjetosDaEstrada"/>:
    /// o modelo, o capô que abre, as rodas, o macaco e o alarme, pelo número de cada um), postos pelas estradas e pelas ruas
    /// do Lakewood:
    ///  - nas rodovias e nas secundárias, de 250 m a 800 m entre eles: encostados no acostamento, parados na pista (meio
    ///    tortos) ou batidos fora dela, no barranco;
    ///  - nas ruas das cidades, de 30 m a 80 m: parados junto da calçada, de um lado e do outro;
    ///  - nunca nas pontes, nos cruzamentos, perto da casa da partida, na água nem em cima de outro;
    ///  - e os quebrados (os sobreviventes, a parte 5): uns do acostamento das rodovias, com o macaco na roda de fora.
    /// Quem desenha é o ObjetosDoMundo (a beira das estradas do sorteado), pela fonte do Lakewood.
    /// </summary>
    public static class BeiraDeLakewood
    {
        /// <summary>Quantos carros quebrados (com os dois sobreviventes trocando o pneu) o Lakewood tem.</summary>
        public const int Quebrados = 3;

        public static ObjetosDaEstrada Planejar(ChaoDeLakewood chao)
        {
            var objetos = new List<ObjetoDaEstrada>();
            var quebrados = new List<CarroQuebrado>();
            var ocupados = new List<Vector2>();
            var s = Sorteio.Da(chao.Mapa.semente, "beira-do-lakewood");
            int variantes = ModelosDaEstrada.NomesDosCarros.Length;
            Vector2 partida = chao.Mapa.partida;
            // os do acostamento das rodovias (onde pode cair um quebrado)
            var noAcostamento = new List<(int indice, Vector2 c, Vector2 frente, Vector2 fora)>();

            bool Livre(Vector2 p, float raio)
            {
                foreach (var o in ocupados) if ((o - p).sqrMagnitude < raio * raio) return false;
                if (Vector2.Distance(p, partida) < 45f) return false;
                if (chao.Partida != null && chao.Partida.DistanciaDoTerreiro(p.x, p.y) < 12f) return false;
                return !chao.Agua(p.x, p.y, out _, 2f);
            }

            for (int e = 0; e < chao.Estradas.Count; e++)
            {
                var est = chao.Estradas[e];
                if (est.DaUsina || est.Via == null || est.Via.tipo == TipoDeVia.Terra || est.Pontos.Length < 4) continue;
                bool rua = est.Via.tipo == TipoDeVia.Rua;
                float minimo = rua ? 30f : est.Via.tipo == TipoDeVia.Rodovia ? 250f : 320f;
                float maximo = rua ? 80f : est.Via.tipo == TipoDeVia.Rodovia ? 650f : 800f;
                float total = est.AoLongo[est.AoLongo.Length - 1];
                float proximo = s.Entre(minimo * 0.3f, maximo);
                int i = 1;
                while (proximo < total - 10f)
                {
                    while (i < est.AoLongo.Length - 1 && est.AoLongo[i] < proximo) i++;
                    float aqui = proximo;
                    proximo += s.Entre(minimo, maximo);
                    if (est.Ponte[i] || est.Ponte[i - 1] || est.AteONo[i] < (rua ? 16f : 35f)) continue;
                    var a = est.Pontos[i - 1];
                    var b = est.Pontos[i];
                    var dir = (b - a).normalized;
                    if (dir.sqrMagnitude < 0.5f) continue;
                    float t = Mathf.InverseLerp(est.AoLongo[i - 1], est.AoLongo[i], aqui);
                    var meio = Vector2.Lerp(a, b, t);
                    // o lado da via (a direita de quem anda no sentido dos pontos, ou a do outro sentido)
                    bool ida = s.Fracao() < 0.5f;
                    var frente = ida ? dir : -dir;
                    var direita = new Vector2(frente.y, -frente.x);
                    float meia = est.MeiaLargura;
                    var o = new ObjetoDaEstrada { Tipo = TipoDeObjeto.Carro, Variante = s.Entre(0, variantes) };
                    float giro = ObjetosDaEstrada.GiroDe(frente);
                    Vector2 p;
                    bool acostamento = false;
                    if (rua)
                    {
                        // parado junto da calçada
                        p = meio + direita * (meia - 1.15f);
                        o.NaEstrada = true;
                        o.Giro = giro + s.Entre(-4f, 4f);
                    }
                    else
                    {
                        float r = s.Fracao();
                        if (r < 0.45f)
                        {
                            p = meio + direita * Mathf.Max(meia - 1.05f, 1.2f);
                            o.NaEstrada = true;
                            o.Giro = giro + s.Entre(-6f, 6f);
                            acostamento = true;
                        }
                        else if (r < 0.8f)
                        {
                            p = meio + direita * (meia * s.Entre(0.1f, 0.5f));
                            o.NaEstrada = true;
                            o.Giro = giro + s.Entre(-25f, 25f);
                        }
                        else
                        {
                            // batido fora da pista, no barranco
                            p = meio + direita * (meia + s.Entre(3.5f, 7f));
                            o.NoTalude = true;
                            o.Giro = giro + s.Entre(-70f, 70f);
                            if (chao.EstradaPerto(p.x, p.y, 2.5f, out _, out _, out _, out _) || chao.LoteEm(p.x, p.y, 3f) != null
                                || chao.Relevo.Inclinacao(p.x, p.y) > 0.6f)
                                continue;
                        }
                    }
                    if (!Livre(p, rua ? 7f : 10f)) continue;
                    // o carro na pista fica em cima da estrada dele (não da outra, perto de um nó)
                    if (o.NaEstrada && (!chao.EstradaPerto(p.x, p.y, 0f, out _, out _, out bool ponte, out _) || ponte)) continue;
                    o.Posicao = p;
                    o.Giro = Mathf.Repeat(o.Giro, 360f);
                    if (acostamento && est.Via.tipo == TipoDeVia.Rodovia) noAcostamento.Add((objetos.Count, p, frente, direita));
                    objetos.Add(o);
                    ocupados.Add(p);
                }
            }

            // os quebrados: uns do acostamento das rodovias, espalhados (longe uns dos outros e da casa da partida)
            var sq = Sorteio.Da(chao.Mapa.semente, "quebrados-do-lakewood");
            for (int k = noAcostamento.Count - 1; k > 0; k--)
            {
                int j = sq.Entre(0, k + 1);
                (noAcostamento[k], noAcostamento[j]) = (noAcostamento[j], noAcostamento[k]);
            }
            foreach (var c in noAcostamento)
            {
                if (quebrados.Count >= Quebrados) break;
                if (Vector2.Distance(c.c, partida) < 400f) continue;
                bool longe = true;
                foreach (var q in quebrados) if (Vector2.Distance(q.Posicao, c.c) < 1500f) longe = false;
                if (!longe) continue;
                objetos[c.indice].Quebrado = true;
                quebrados.Add(new CarroQuebrado { Indice = c.indice, Roda = sq.Fracao() < 0.5f ? 1 : 3, Posicao = c.c, Frente = c.frente, Fora = c.fora });
            }

            return ObjetosDaEstrada.DeUmMapa(objetos, quebrados,
                (x, z) => new Vector2Int(Mathf.FloorToInt(x / ChaoDeLakewood.TamanhoDoBloco), Mathf.FloorToInt(z / ChaoDeLakewood.TamanhoDoBloco)),
                chao.BlocosX, chao.BlocosZ);
        }
    }
}
