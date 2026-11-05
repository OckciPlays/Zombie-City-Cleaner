using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// O saque pelo mapa (o Plano_Saque.md da pasta de arte, a parte 3): o catálogo (todos os itens existem no jogo, cada
    /// raridade tem itens, todo móvel com lugares tem a regra dele) e o sorteio no mapa inteiro, sem o Play: cada casa, loja
    /// (os apartamentos de cima) e andar de prédio do Médio 7 é montado como no jogo, e o saque de cada moradia é sorteado
    /// com a semente dela. Confere: nenhuma moradia com lugar fica vazia; nenhuma passa do máximo; cada item cabe no lugar
    /// dele e é do que o lugar aceita; o mesmo sorteio duas vezes dá o mesmo; as raridades perto das chances. O log dá os
    /// números (quantos por moradia, por raridade, por cômodo e os itens mais achados).
    /// </summary>
    public class SaqueTestes
    {
        static readonly Medidas M = Medidas.DoJogo;

        [Test]
        public void OCatalogoEstaCompleto()
        {
            Assert.IsEmpty(CatalogoDoSaque.Faltando, "itens do catálogo que não estão na tabela dos itens (Itens > Escrever os itens do saque): " +
                                                      string.Join(", ", CatalogoDoSaque.Faltando));
            Assert.GreaterOrEqual(CatalogoDoSaque.Itens.Count(i => i.nome.StartsWith("Roupa.")), 70, "as roupas não entraram no catálogo");
            for (int r = 0; r < 5; r++) Assert.IsNotEmpty(CatalogoDoSaque.DaRaridade((Raridade)r), $"nenhum item {CatalogoDoSaque.NomesDasRaridades[r]}");
            foreach (var m in CatalogoDoSaque.ModelosComRegra) Assert.IsTrue(LugaresDoSaque.Tem(m), $"{m} tem a regra do saque, mas não tem lugares");
            foreach (var m in LugaresDoSaque.Nomes) Assert.IsNotNull(CatalogoDoSaque.DoModelo(m), $"{m} tem lugares, mas não tem a regra do saque");
            foreach (var it in CatalogoDoSaque.Itens) Assert.Greater(it.tamanho.x * it.tamanho.z, 0f, $"{it.nome} sem tamanho");
        }

        sealed class Conta
        {
            public int moradias, semLugar, itens;
            public readonly int[] porRaridade = new int[5];
            public readonly Dictionary<string, (int moradias, int itens)> porTipo = new Dictionary<string, (int, int)>();
            public readonly Dictionary<string, int> porComodo = new Dictionary<string, int>(), porItem = new Dictionary<string, int>();
            public readonly List<string> falhas = new List<string>();
            public int maxPorCasa, abaixoDoMinimo;
        }

        // uns 2 minutos (todas as construções do Médio, montadas como no jogo)
        [Test, Timeout(900000)]
        public void OSaqueDoMapaInteiro()
        {
            var d = GeradorDoMundo.Gerar(7, TamanhoDoMundo.Medio);
            var conta = new Conta();
            var relogio = System.Diagnostics.Stopwatch.StartNew();
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                {
                    if (l.Casa != null)
                    {
                        var casa = new Casa(l.Casa, M) { portasVivas = true };
                        casa.Montar();
                        Conferir(casa.moveis, l.Casa.semente, $"casa em {t.Cidade.Nome}", conta);
                        MalhaDeObra.Devolver(casa.vis);
                        MalhaDeObra.Devolver(casa.col);
                    }
                    else if (l.Loja != null)
                    {
                        var loja = new Loja(l.Loja, M);
                        loja.Montar((x, z) => 0f, 0f);
                        Conferir(loja.moveis, l.Loja.semente, $"{PlantaDeLoja.Nome(l.Loja.tipo)} em {t.Cidade.Nome}", conta);
                        MalhaDeObra.Devolver(loja.vis);
                        MalhaDeObra.Devolver(loja.col);
                    }
                    else if (l.Predio != null && !l.Predio.hospital)
                        for (int f = 0; f < l.Predio.andares; f++)
                        {
                            var andar = new Predio(l.Predio, M, f);
                            andar.MontarAndar();
                            Conferir(andar.Moveis, l.Predio.semente * 31 + f, $"prédio em {t.Cidade.Nome}, andar {f}", conta);
                            MalhaDeObra.Devolver(andar.vis);
                            MalhaDeObra.Devolver(andar.col);
                            foreach (var e in andar.Escuros) MalhaDeObra.Devolver(e);
                        }
                }

            var sb = new StringBuilder();
            sb.AppendLine($"[Saque] Médio 7, em {relogio.Elapsed.TotalSeconds:0} s: {conta.moradias} moradias ({conta.semLugar} sem nenhum lugar), {conta.itens} itens " +
                          $"({(float)conta.itens / Mathf.Max(1, conta.moradias - conta.semLugar):0.00} por moradia com lugar; no máximo {conta.maxPorCasa}; " +
                          $"{conta.abaixoDoMinimo} abaixo do mínimo, sem lugar para todos)");
            foreach (var kv in conta.porTipo.OrderBy(k => k.Key))
                sb.AppendLine($"  {kv.Key}: {kv.Value.moradias} moradias, {kv.Value.itens} itens ({(float)kv.Value.itens / Mathf.Max(1, kv.Value.moradias):0.00} cada)");
            sb.Append("  Raridades:");
            for (int r = 0; r < 5; r++) sb.Append($" {CatalogoDoSaque.NomesDasRaridades[r]} {conta.porRaridade[r]} ({100f * conta.porRaridade[r] / Mathf.Max(1, conta.itens):0.0}%)");
            sb.AppendLine();
            sb.AppendLine("  Cômodos: " + string.Join(", ", conta.porComodo.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}")));
            sb.AppendLine("  Os mais achados: " + string.Join(", ", conta.porItem.OrderByDescending(k => k.Value).Take(25).Select(k => $"{k.Key} {k.Value}")));
            sb.AppendLine("  Os raros e acima: " + string.Join(", ", conta.porItem.Where(k => RaridadeDo(k.Key) >= Raridade.Raro)
                .OrderByDescending(k => (int)RaridadeDo(k.Key)).ThenByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}")));
            Debug.Log(sb.ToString());

            Assert.Greater(conta.moradias, 1500, "poucas moradias conferidas");
            Assert.IsEmpty(conta.falhas, $"{conta.falhas.Count} falhas:\n" + string.Join("\n", conta.falhas.Take(30)));
            float normal = (float)conta.porRaridade[0] / conta.itens, lendario = (float)conta.porRaridade[4] / conta.itens;
            // (sem lugar para a raridade na moradia, desce: os normais podem passar um pouco da chance)
            Assert.That(normal, Is.InRange(CatalogoDoSaque.Chances[0] - 0.05f, CatalogoDoSaque.Chances[0] + 0.15f), "os normais longe da chance");
            Assert.That(lendario, Is.LessThanOrEqualTo(CatalogoDoSaque.Chances[4] + 0.01f), "lendários demais");
        }

        static Raridade RaridadeDo(string nome) => CatalogoDoSaque.Itens.First(i => i.nome == nome).raridade;

        static readonly List<ItemDoSaque> _itens = new List<ItemDoSaque>(), _deNovo = new List<ItemDoSaque>();
        static readonly List<MobiliaNova.Peca> _pecas = new List<MobiliaNova.Peca>();

        static void Conferir(List<PontoDeMovel> pontos, int semente, string nome, Conta conta)
        {
            _itens.Clear();
            SaqueDaObra.Sortear(pontos, semente, _itens);
            _deNovo.Clear();
            SaqueDaObra.Sortear(pontos, semente, _deNovo);
            if (_deNovo.Count != _itens.Count || _itens.Where((x, i) => x.nome != _deNovo[i].nome || x.apoio != _deNovo[i].apoio).Any())
                conta.falhas.Add($"{nome}: o mesmo sorteio deu outro saque");

            // (05/11/2026) a farmácia e o hospital (as unidades de loja, nos móveis antigos) têm as regras deles e não entram nas
            // contas das moradias
            foreach (int u in pontos.Where(p => p.unidade > 0 && p.unidade < PontoDeMovel.Farmacia).Select(p => p.unidade).Distinct())
            {
                conta.moradias++;
                bool apto = pontos.Any(p => p.unidade == u && p.apartamento);
                bool sobrado = !apto && pontos.Any(p => p.unidade == u && p.andar >= 1);
                bool porao = !apto && pontos.Any(p => p.unidade == u && p.andar < 0);
                string tipo = apto ? "apartamento" : (sobrado ? "sobrado" : "térrea") + (porao ? " com porão" : "");
                int lugares = SaqueDaObra.Lugares(pontos, semente, u);
                var daMorada = _itens.Where(x => x.unidade == u).ToList();
                conta.porTipo.TryGetValue(tipo, out var pt);
                conta.porTipo[tipo] = (pt.moradias + 1, pt.itens + daMorada.Count);
                if (lugares == 0) { conta.semLugar++; continue; }
                var (min, max) = CatalogoDoSaque.Quantos(apto, sobrado);
                if (porao) max += CatalogoDoSaque.TentativasNoPorao;
                if (daMorada.Count < min) conta.abaixoDoMinimo++;
                if (daMorada.Count == 0) conta.falhas.Add($"{nome}: a moradia {u} ({tipo}) ficou vazia, com {lugares} lugares");
                if (daMorada.Count > max) conta.falhas.Add($"{nome}: a moradia {u} ({tipo}) com {daMorada.Count} itens (o máximo é {max})");
                conta.maxPorCasa = Mathf.Max(conta.maxPorCasa, daMorada.Count);
            }

            foreach (var it in _itens)
            {
                if (it.unidade >= PontoDeMovel.Farmacia) continue;   // o saque das lojas (veja acima)
                conta.itens++;
                conta.porRaridade[(int)it.raridade]++;
                conta.porComodo[it.comodo] = conta.porComodo.TryGetValue(it.comodo, out int c) ? c + 1 : 1;
                conta.porItem[it.nome] = conta.porItem.TryGetValue(it.nome, out int n) ? n + 1 : 1;
                // o lugar do item: a peça do conjunto do ponto, como o sorteio
                _pecas.Clear();
                var p = pontos[it.ponto];
                MobiliaNova.Pecas(p, MobiliaNova.Sorte(p, semente, it.ponto), _pecas);
                var peca = _pecas.FirstOrDefault(q => q.indice == it.peca);
                var lugares = LugaresDoSaque.Do(peca.modelo);
                if (it.lugar >= lugares.Length) { conta.falhas.Add($"{nome}: {it.nome} num lugar que não existe ({peca.modelo})"); continue; }
                var l = lugares[it.lugar];
                if (!SaqueDaObra.Cabe(it.tamanho, l.tamanho * MedidasDaMobilia.Escala, out _))
                    conta.falhas.Add($"{nome}: {it.nome} ({it.tamanho}) não cabe no lugar {it.lugar} de {peca.modelo} ({l.tamanho * MedidasDaMobilia.Escala})");
                var c2 = CatalogoDoSaque.Itens.First(i => i.nome == it.nome);
                if ((c2.especie & CatalogoDoSaque.Aceita(CatalogoDoSaque.DoModelo(peca.modelo), l)) == 0)
                    conta.falhas.Add($"{nome}: {it.nome} ({c2.especie}) no lugar {it.lugar} de {peca.modelo}, que não aceita");
            }
        }
    }
}
