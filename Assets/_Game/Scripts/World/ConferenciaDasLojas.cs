using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    public static partial class ConferenciaDasCasas
    {
        static string VariacaoDa(PlantaDeLoja p) => PlantaDeLoja.Nome(p.tipo) + (p.andares > 1 ? ", com apartamento em cima" : "");

        /// <summary>
        /// A conferência das lojas de verdade (Parte M6, terceira fatia), na loja mais perto de cada variação (o tipo: comum,
        /// mercado, farmácia, 24 horas e armas; de um andar ou com o apartamento em cima: até 9): o zumbi, da calçada na
        /// frente da porta até cada cômodo de cada andar (o NavMesh), e o jogador andando o mesmo caminho até cada um, com
        /// o CharacterController e a gravidade (chega, prende ou cai). O resultado vai para o log e para
        /// `Conferencia_Lojas.md` (no Editor, em `Benchmarks/`; no jogo compilado, na pasta de dados dele).
        /// </summary>
        public static IEnumerator RodarLojas(MovimentoPrimeiraPessoa movimento, DadosDoMundo d, Action<string> avisar)
        {
            if (Rodando || movimento == null || d?.Plano == null) yield break;
            var origem = movimento.transform.position;
            var aqui = new Vector2(origem.x, origem.z);
            float giroAntes = movimento.transform.eulerAngles.y;
            var maisPerto = new Dictionary<string, (TracadoDaCidade t, Lote l, float dist)>();
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                {
                    if (l.Loja == null) continue;
                    float dist = Vector2.Distance(t.Mundo(l.MeioDaCasa), aqui);
                    string chave = VariacaoDa(l.Loja);
                    if (!maisPerto.TryGetValue(chave, out var atual) || dist < atual.dist) maisPerto[chave] = (t, l, dist);
                }
            var lojas = new List<(TracadoDaCidade t, Lote l, float dist)>(maisPerto.Values);
            if (lojas.Count == 0) { avisar?.Invoke("Nenhuma loja no mapa."); yield break; }
            lojas.Sort((a, b) => a.dist.CompareTo(b.dist));

            Rodando = true;
            var controle = movimento.GetComponent<CharacterController>();
            bool voandoAntes = movimento.Voando;
            movimento.Voando = false;
            var relatorio = new StringBuilder();
            int comodos = 0, peloNavMesh = 0, andados = 0, chegaram = 0;
            var falhas = new List<string>();
            var dono = new object();
            var caminho = new NavMeshPath();
            try
            {
                for (int ci = 0; ci < lojas.Count; ci++)
                {
                    var (t, l, _) = lojas[ci];
                    var p = l.Loja;
                    string variacao = VariacaoDa(p);
                    var M = t.MatrizDaCasa(l);
                    float sx = p.espelhar ? -1f : 1f;
                    // o começo: na calçada, na frente da (primeira) porta da frente
                    float xPorta = p.ligacoes.Find(x => x.b == null && x.lado == 0).pos;
                    var frente = l.NaCasa(sx * xPorta, p.Z0);
                    bool aoLongoDeU = l.Frente.y != 0f;
                    float beira = aoLongoDeU ? (l.Frente.y < 0f ? l.V0 : l.V1) : (l.Frente.x < 0f ? l.U0 : l.U1);
                    if (aoLongoDeU) frente.y = beira; else frente.x = beira;
                    var w = t.Mundo(frente + l.Frente * 1.2f);
                    var inicio = new Vector3(w.x, d.Altura(w.x, w.y) + TracadoDaCidade.AlturaDaCalcada + 0.05f, w.y);
                    Status = $"Conferindo as lojas: {ci + 1} de {lojas.Count} ({variacao})";
                    yield return IrPara(movimento, controle, inicio, dono);
                    var linha = new StringBuilder($"- {variacao}, {p.W:0.0} × {p.D:0.0} m, {p.andares} andar{(p.andares > 1 ? "es" : "")} em ({M.m03:F0}, {M.m23:F0}): ");
                    foreach (var k in p.comodos)
                    {
                        comodos++;
                        string onde = k.tipo + (k.andar > 0 ? $" ({k.andar}º andar)" : "");
                        if (!AcharPonto(M, sx, k, p.Base(k.andar), out var alvo))
                        {
                            falhas.Add($"{variacao}: {onde} sem NavMesh");
                            linha.Append($"{onde} sem NavMesh; ");
                            continue;
                        }
                        if (!Caminho(inicio, alvo, caminho))
                        {
                            falhas.Add($"{variacao}: o zumbi não chega no {onde}, {caminho.status}");
                            linha.Append($"{onde}: zumbi NÃO; ");
                            continue;
                        }
                        peloNavMesh++;
                        andados++;
                        Status = $"Conferindo as lojas: {ci + 1} de {lojas.Count}, andando até o {onde}";
                        var resultado = new string[1];
                        yield return Andar(movimento, controle, inicio, caminho.corners, resultado);
                        if (resultado[0] == null) { chegaram++; linha.Append($"{onde}: ok; "); }
                        else
                        {
                            linha.Append($"{onde}: jogador {resultado[0]}; ");
                            falhas.Add($"{variacao}: o jogador {resultado[0]} indo ao {onde}");
                        }
                    }
                    relatorio.AppendLine(linha.ToString());
                    Debug.Log("[Lojas] " + linha);
                }
            }
            finally
            {
                TerrenoDoMundo.Instancia?.Esquecer(dono);
                controle.enabled = false;
                movimento.Teleportar(origem, giroAntes);
                controle.enabled = true;
                movimento.Voando = voandoAntes;
                Rodando = false;
                Status = "";
            }
            if (falhas.Count > 0)
            {
                relatorio.AppendLine();
                relatorio.AppendLine("Falhas:");
                foreach (var f in falhas) relatorio.AppendLine("- " + f);
            }
            string texto = $"## {DateTime.Now:dd/MM/yyyy HH:mm} — {Tamanhos.NomeComLado(d.Tamanho)}, semente {d.Semente}, uma loja de cada variação ({lojas.Count}), " +
                           $"as mais perto de ({origem.x:F0}, {origem.z:F0})\n\nOs cômodos: {comodos}; o zumbi chega em {peloNavMesh}; o jogador andou até {andados} e chegou em {chegaram}.\n\n{relatorio}";
            Debug.Log("[Lojas] " + texto);
            string arquivo = Gravar("Conferencia_Lojas.md", "# A conferência das lojas de verdade (Parte M6, terceira fatia)", texto);
            avisar?.Invoke($"Lojas: {lojas.Count}; zumbi em {peloNavMesh} de {comodos} cômodos; jogador em {chegaram} de {andados}. " +
                           $"{(falhas.Count > 0 ? $"{falhas.Count} falhas" : "Sem falhas")} (em {arquivo}).");
        }
    }
}
