using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// As pessoas da Woodcreek no Dia 0 (22/11/2026, a etapa 4 do Docs/23: "os NPCs do Dia 0 com as rotinas pela vila"): os
    /// mesmos NPCs da rua do Dia 0 (<see cref="NpcsDoDiaZero"/> e <see cref="NpcDoDiaZero"/>: as rotinas pelo relógio da
    /// partida, iguais em todas as máquinas), nos lugares da vila:
    ///  - os atendentes das lojas que vendem, atrás dos caixas (a conta da planta: a obra só entra perto de alguém);
    ///  - nos quintais da frente das casas perto do comércio e da nossa casa, um sim, um não: regando, cortando a grama ou
    ///    conversando perto da rua;
    ///  - nas calçadas do comércio (as frentes das lojas, dos dois lados da rodovia), quem passeia e quem corre;
    ///  - na beira da Creek Street, quem passeia até o comércio.
    /// Cinco ficam doentes (os olhos vermelhos e a tosse), como na rua do Dia 0.
    /// </summary>
    public partial class WoodcreekDoDiaZero
    {
        /// <summary>O meio do comércio da Woodcreek (a North Shore Highway com as lojas), no mapa.</summary>
        static readonly Vector2 Comercio = new Vector2(9975f, 6760f);

        void PorNpcsDaWoodcreek(LakewoodNaCena lw)
        {
            var chao = lw.ChaoNoMapa;
            var rnd = new System.Random(semente + 909);
            int n = 0;
            LugarDeNpc Lugar(Rotina r, Vector3 ponto, float giro, string tipo)
            {
                var l = new LugarDeNpc { rotina = r, ponto = ponto, giro = giro, tipo = tipo, semente = 1000 + n * 37 };
                n++;
                Npcs.Add(l);
                return l;
            }
            // um ponto do mapa no mundo, no chão de lá
            Vector3 W(Vector2 m, float acima = 0.03f) => OrigemDeLakewood.NoMundo(new Vector3(m.x, chao.Altura(m.x, m.y) + acima, m.y));
            float Giro(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

            // os atendentes
            var doMercado = new List<LugarDeNpc>();
            var doPosto = new List<LugarDeNpc>();
            foreach (var l in chao.Lotes)
            {
                if (!_abertas.TryGetValue(l.Indice, out var tipo)) continue;
                foreach (var (p, g) in AtendentesDe(lw, l, tipo))
                {
                    var a = Lugar(Rotina.Atendente, p, g, tipo == LojaDoDia.Farmacia ? "Médico" : "Civil");
                    if (tipo == LojaDoDia.Mercado) doMercado.Add(a);
                    if (tipo == LojaDoDia.Posto && l.Plano.Uso == UsoDoLote.Posto) doPosto.Add(a);
                }
            }

            // os quintais da frente: as casas perto do comércio e da nossa (a nossa e a do lado ficam livres), um sim, um não
            var casa = LoteDaFamilia.Lote.meio;
            var casas = chao.Lotes
                .Where(l => l.Plano != null && l.Plano.Uso == UsoDoLote.Casa && l != LoteDaFamilia && l.Lote.cidade == LoteDaFamilia.Lote.cidade &&
                            Vector2.Distance(l.Lote.meio, casa) > 30f && (Vector2.Distance(l.Lote.meio, Comercio) < 260f || Vector2.Distance(l.Lote.meio, casa) < 160f))
                .OrderBy(l => Vector2.Distance(l.Lote.meio, casa))
                .ToList();
            int quintais = 0;
            var regando = new List<LugarDeNpc>();
            var conversando = new List<LugarDeNpc>();
            for (int i = 0; i < casas.Count && quintais < 12; i += 2)
            {
                var l = casas[i];
                var lote = l.Lote;
                // o quintal da frente, no espaço do lote: da beira da frente até a frente da casa, longe dos lados
                float za = -lote.fundo * 0.5f + 1.0f, zb = l.Construcao.yMin - 0.9f;
                float xa = -lote.largura * 0.5f + 1.2f, xb = lote.largura * 0.5f - 1.2f;
                if (zb - za < 1.6f || xb - xa < 3f) continue;
                switch (quintais % 3)
                {
                    case 0:
                    {
                        var p = lote.NoMapa(Mathf.Lerp(xa, xb, 0.3f), Mathf.Lerp(za, zb, 0.55f));
                        regando.Add(Lugar(Rotina.Regar, W(p), Giro(lote.Lado) + 30f, rnd.NextDouble() < 0.5 ? "Idoso" : "Civil"));
                        break;
                    }
                    case 1:
                    {
                        // a grama cortada em faixas, de um lado ao outro do quintal, e de volta pela outra ordem (o laço)
                        var caminho = new List<Vector3>();
                        const int faixas = 4;
                        for (int f = 0; f < faixas; f++)
                        {
                            float z = Mathf.Lerp(za + 0.3f, zb - 0.3f, f / (float)(faixas - 1));
                            float x0 = f % 2 == 0 ? xa : xb, x1 = f % 2 == 0 ? xb : xa;
                            caminho.Add(W(lote.NoMapa(x0, z)));
                            caminho.Add(W(lote.NoMapa(x1, z)));
                        }
                        for (int f = faixas - 1; f >= 0; f--) caminho.Add(caminho[f * 2 + 1]);
                        var c = Lugar(Rotina.CortarGrama, caminho[0], Giro(lote.Lado), rnd.NextDouble() < 0.5 ? "Trabalhador" : "Civil");
                        c.caminho = caminho.ToArray();
                        break;
                    }
                    default:
                    {
                        // dois vizinhos conversando perto da rua
                        var meio = lote.NoMapa(Mathf.Lerp(xa, xb, 0.6f), za + 0.5f);
                        var a = Lugar(Rotina.Conversar, W(meio + lote.Lado * 0.55f), Giro(-lote.Lado), "Civil");
                        var b = Lugar(Rotina.Conversar, W(meio - lote.Lado * 0.55f), Giro(lote.Lado), rnd.NextDouble() < 0.5 ? "Idoso" : "Estudante");
                        a.par = Npcs.IndexOf(b);
                        b.par = Npcs.IndexOf(a);
                        conversando.Add(a);
                        break;
                    }
                }
                quintais++;
            }

            // as calçadas do comércio: as frentes das lojas de cada lado da rodovia, de uma ponta à outra e de volta
            var passeando = new List<LugarDeNpc>();
            var lojas = chao.Lotes.Where(l => l.Plano != null && l.Lote.cidade == LoteDaFamilia.Lote.cidade &&
                                              (l.Plano.Uso == UsoDoLote.Loja || l.Plano.Uso == UsoDoLote.LojaDeArmas || l.Plano.Uso == UsoDoLote.Posto) &&
                                              Vector2.Distance(l.Lote.meio, Comercio) < 300f).ToList();
            string[] tipos = { "Civil", "Estudante", "Idoso", "Civil", "Trabalhador", "Civil" };
            int quem = 0;
            foreach (int lado in new[] { 1, -1 })
            {
                var frentes = lojas.Where(l => Mathf.Sign(l.Lote.Frente.x) == lado).OrderBy(l => l.Lote.meio.y)
                                   .Select(l => l.Lote.MeioDaFrente + l.Lote.Frente * 1.8f).ToList();
                if (frentes.Count < 2) continue;
                for (int k = 0; k < 3; k++, quem++)
                {
                    float desvio = (k - 1) * 0.45f;
                    var ida = frentes.Select(f => W(f + Vector2.right * lado * desvio)).ToList();
                    var volta = frentes.Select(f => W(f + Vector2.right * lado * (desvio + 0.5f))).Reverse().ToList();
                    if (k == 1) { ida.Reverse(); volta.Reverse(); }
                    var caminho = ida.Concat(volta).ToArray();
                    var r = quem == 2 ? Rotina.Correr : Rotina.Passear;
                    var l = Lugar(r, caminho[0], 0f, tipos[quem % tipos.Length]);
                    l.caminho = caminho;
                    if (r == Rotina.Passear) passeando.Add(l);
                }
            }

            // a beira da Creek Street: quem desce até o comércio e volta
            var creek = lw.mapa.ruas.FirstOrDefault(r => r.nome == "Creek Street" && r.cidade == LoteDaFamilia.Lote.cidade);
            if (creek != null && creek.pontos.Count >= 2)
                for (int k = 0; k < 2; k++)
                {
                    float off = (creek.MeiaLargura + 1.3f) * (k == 0 ? 1f : -1f);
                    var ida = new List<Vector3>();
                    for (int i = 0; i < creek.pontos.Count; i++)
                    {
                        var a = creek.pontos[Mathf.Max(0, i - 1)];
                        var b = creek.pontos[Mathf.Min(creek.pontos.Count - 1, i + 1)];
                        var d = (b - a).normalized;
                        ida.Add(W(creek.pontos[i] + new Vector2(-d.y, d.x) * off));
                    }
                    var caminho = ida.Concat(Enumerable.Reverse(ida).Select(v => v + Vector3.up * 0.001f)).ToArray();
                    var l = Lugar(Rotina.Passear, caminho[k == 0 ? 0 : caminho.Length / 2], 0f, k == 0 ? "Idoso" : "Civil");
                    l.caminho = caminho;
                    passeando.Add(l);
                }

            // os doentes (5): um atendente do mercado, o do posto, quem passeia, quem rega e quem conversa
            var doentes = new List<LugarDeNpc>();
            void Doente(IEnumerable<LugarDeNpc> de)
            {
                var l = de.FirstOrDefault(x => !x.doente);
                if (l == null) return;
                l.doente = true;
                doentes.Add(l);
            }
            Doente(doMercado);
            Doente(doPosto);
            Doente(passeando);
            Doente(regando);
            Doente(conversando);
            while (doentes.Count < 5)
            {
                int antes = doentes.Count;
                Doente(Npcs.Where(x => x.rotina == Rotina.Passear || x.rotina == Rotina.Conversar || x.rotina == Rotina.Atendente));
                if (doentes.Count == antes) break;
            }
        }
    }
}
