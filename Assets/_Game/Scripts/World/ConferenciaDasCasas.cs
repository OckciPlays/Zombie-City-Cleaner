using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A conferência das casas de verdade (Plano_Mapa, Parte M6: "andar por dentro de todas as variações sem cair, sem
    /// prender e sem buraco, e o zumbi chega em todos os cômodos"), no anfitrião, pelo F1 do mundo:
    ///  - <b>o zumbi:</b> para cada casa perto (até o raio), o caminho pelo NavMesh (<see cref="NavMeshDoMundo"/>) da calçada
    ///    na frente dela até o meio de cada cômodo (no andar dele: o porão, o térreo e o de cima);
    ///  - <b>o jogador:</b> numa casa de cada variação (térrea ou sobrado, com porão ou não, garagem, garagem com porta
    ///    para dentro ou vaga, espelhada ou não: até 24) e na da família, o próprio jogador anda o mesmo caminho até cada cômodo,
    ///    com o CharacterController dele e a gravidade (o movimento dele fica desligado nesse tempo): chega, prende (não
    ///    sai do lugar por 2 s) ou cai (fica 1,5 m abaixo do chão do caminho).
    /// Ou, em vez das casas por perto, a mais perto de cada variação, no mapa todo (as 24, se o mapa tiver todas).
    /// O resultado vai para o log e para `Conferencia_Casas.md` (no Editor, em `Benchmarks/`; no jogo compilado, na pasta de
    /// dados dele). No fim o jogador volta para onde estava.
    /// </summary>
    public static partial class ConferenciaDasCasas
    {
        public static bool Rodando { get; private set; }
        public static string Status { get; private set; } = "";

        const float Velocidade = 5f, Gravidade = -20f;

        static float BaseDoAndar(Planta p, Medidas m, int andar) =>
            andar >= 0 ? Casa.PisoDe(p) + andar * (m.altura + m.laje) : Casa.PisoDe(p) - m.laje - m.alturaPorao;

        static string Variacao(Planta p) =>
            (p.molde == "Terrea" ? "térrea" : "sobrado") + (p.porao ? " com porão" : "") +
            (p.garagem == null ? "" : !p.garagem.coberta ? ", vaga" : p.garagem.portaParaDentro ? ", garagem com porta" : ", garagem") +
            (p.espelhar ? ", espelhada" : "");

        /// <summary>
        /// Confere as casas até 'raio' do jogador; com 'umaDeCadaVariacao', a casa mais perto de cada variação, no mapa todo
        /// (o raio não conta).
        /// </summary>
        public static IEnumerator Rodar(MovimentoPrimeiraPessoa movimento, DadosDoMundo d, float raio, Action<string> avisar, bool umaDeCadaVariacao = false)
        {
            if (Rodando || movimento == null || d?.Plano == null) yield break;
            var m = Medidas.DoJogo;
            var origem = movimento.transform.position;
            var aqui = new Vector2(origem.x, origem.z);
            float giroAntes = movimento.transform.eulerAngles.y;
            var casas = new List<(TracadoDaCidade t, Lote l)>();
            var maisPerto = new Dictionary<string, (TracadoDaCidade t, Lote l, float dist)>();
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                {
                    if (l.Casa == null) continue;
                    float dist = Vector2.Distance(t.Mundo(l.MeioDaCasa), aqui);
                    if (!umaDeCadaVariacao)
                    {
                        if (dist < raio) casas.Add((t, l));
                        continue;
                    }
                    string chave = Variacao(l.Casa);
                    if (!maisPerto.TryGetValue(chave, out var atual) || dist < atual.dist) maisPerto[chave] = (t, l, dist);
                }
            foreach (var c in maisPerto.Values) casas.Add((c.t, c.l));
            if (casas.Count == 0) { avisar?.Invoke("Nenhuma casa perto."); yield break; }
            casas.Sort((a, b) => Vector2.Distance(a.t.Mundo(a.l.MeioDaCasa), aqui).CompareTo(Vector2.Distance(b.t.Mundo(b.l.MeioDaCasa), aqui)));

            Rodando = true;
            var controle = movimento.GetComponent<CharacterController>();
            bool voandoAntes = movimento.Voando;
            movimento.Voando = false;
            var relatorio = new StringBuilder();
            string cabecalho = $"{DateTime.Now:dd/MM/yyyy HH:mm} — {Tamanhos.NomeComLado(d.Tamanho)}, semente {d.Semente}, " +
                               (umaDeCadaVariacao ? $"uma casa de cada variação ({casas.Count}), as mais perto de ({origem.x:F0}, {origem.z:F0})"
                                                  : $"{casas.Count} casas até {raio:F0} m de ({origem.x:F0}, {origem.z:F0})");
            int comodos = 0, peloNavMesh = 0, andados = 0, chegaram = 0;
            var falhas = new List<string>();
            var variacoesAndadas = new HashSet<string>();
            var dono = new object();
            var caminho = new NavMeshPath();
            try
            {
                for (int ci = 0; ci < casas.Count; ci++)
                {
                    var (t, l) = casas[ci];
                    var p = l.Casa;
                    string variacao = Variacao(p) + (l.Tipo == TipoDeLote.Familia ? " (a da família)" : "");
                    var M = t.MatrizDaCasa(l);
                    float sx = p.espelhar ? -1f : 1f;
                    // o começo: na calçada, na frente da porta da frente
                    var prof = Casa.Profundidade(p, m);
                    var porta = l.NaCasa(sx * p.xPortaFrente, prof.x);
                    bool aoLongoDeU = l.Frente.y != 0f;
                    float beira = aoLongoDeU ? (l.Frente.y < 0f ? l.V0 : l.V1) : (l.Frente.x < 0f ? l.U0 : l.U1);
                    if (aoLongoDeU) porta.y = beira; else porta.x = beira;
                    var w = t.Mundo(porta + l.Frente * 1.2f);
                    var inicio = new Vector3(w.x, d.Altura(w.x, w.y) + TracadoDaCidade.AlturaDaCalcada + 0.05f, w.y);

                    // o jogador vai até lá (o chão, as casas e o NavMesh em volta montam) e espera
                    Status = $"Conferindo as casas: {ci + 1} de {casas.Count} ({variacao})";
                    controle.enabled = false;
                    movimento.Teleportar(inicio, 0f);
                    controle.enabled = true;
                    TerrenoDoMundo.Instancia?.Pedir(dono, inicio, 50f, true);
                    float limite = Time.realtimeSinceStartup + 25f;
                    yield return null;
                    while (Time.realtimeSinceStartup < limite &&
                           (TerrenoDoMundo.Instancia == null || !TerrenoDoMundo.Instancia.Pronto(inicio, 40f) ||
                            NavMeshDoMundo.Instancia == null || !NavMeshDoMundo.Instancia.Pronto || !NavMeshDoMundo.TemEm(inicio)))
                    {
                        movimento.Teleportar(inicio, 0f);
                        yield return null;
                    }
                    bool andar = variacoesAndadas.Add(variacao);
                    var linha = new StringBuilder($"- {variacao}, {p.W:0.0} × {p.D:0.0} m em ({M.m03:F0}, {M.m23:F0}): ");
                    foreach (var k in p.comodos)
                    {
                        comodos++;
                        float y = BaseDoAndar(p, m, k.andar);
                        if (!AcharPonto(M, sx, k, y, out var alvo))
                        {
                            falhas.Add($"{variacao}: {k.tipo} (andar {k.andar}) sem NavMesh");
                            linha.Append($"{k.tipo} sem NavMesh; ");
                            continue;
                        }
                        bool ok = NavMesh.CalculatePath(inicio, alvo, NavMesh.AllAreas, caminho) && caminho.status == NavMeshPathStatus.PathComplete &&
                                  Vector3.Distance(caminho.corners[caminho.corners.Length - 1], alvo) < 0.6f;
                        if (ok) peloNavMesh++;
                        else falhas.Add($"{variacao}: o zumbi não chega no {k.tipo} (andar {k.andar}), {caminho.status}");
                        string feito = ok ? "zumbi ok" : "zumbi NÃO";
                        if (ok && andar)
                        {
                            andados++;
                            var cantos = caminho.corners;
                            Status = $"Conferindo as casas: {ci + 1} de {casas.Count}, andando até o {k.tipo}";
                            var resultado = new string[1];
                            yield return Andar(movimento, controle, inicio, cantos, resultado);
                            if (resultado[0] == null) { chegaram++; feito += ", jogador ok"; }
                            else
                            {
                                feito += ", jogador " + resultado[0];
                                falhas.Add($"{variacao}: o jogador {resultado[0]} indo ao {k.tipo} (andar {k.andar})");
                            }
                        }
                        linha.Append($"{k.tipo}{(k.andar != 0 ? $" ({(k.andar < 0 ? "porão" : "em cima")})" : "")}: {feito}; ");
                    }
                    relatorio.AppendLine(linha.ToString());
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
            string texto = $"## {cabecalho}\n\nOs cômodos: {comodos}; o zumbi chega em {peloNavMesh}; o jogador andou até {andados} " +
                           $"(numa casa de cada uma das {variacoesAndadas.Count} variações) e chegou em {chegaram}.\n\n{relatorio}";
            Debug.Log("[Casas] " + texto);
            string arquivo = Gravar("Conferencia_Casas.md", "# A conferência das casas de verdade (Parte M6)", texto);
            avisar?.Invoke($"Casas: {casas.Count}; zumbi em {peloNavMesh} de {comodos} cômodos; jogador em {chegaram} de {andados}. " +
                           $"{(falhas.Count > 0 ? $"{falhas.Count} falhas" : "Sem falhas")} (em {arquivo}).");
        }

        static string VariacaoDo(PlantaDePredio p) =>
            (p.hospital ? "hospital" : p.lojasNoTerreo ? "térreo com lojas" : "térreo com apartamentos") + (p.andarDoPocoAberto >= 0 ? ", poço aberto" : "") +
            (p.espelhar ? ", espelhado" : "");

        /// <summary>
        /// A conferência dos prédios de verdade (Parte M6, segunda fatia), no prédio mais perto de cada variação (o térreo
        /// com lojas ou com apartamentos, o poço do elevador aberto ou não, espelhado ou não: até 8):
        ///  - <b>o zumbi:</b> o caminho pelo NavMesh da calçada na frente da portaria até o patamar da escada de cada andar
        ///    (de patamar em patamar), dele até cada cômodo do andar (os andares carregam de 5 em 5, pedidos ao prédio,
        ///    <see cref="AndaresDoPredio.Pedir"/>) e até a cobertura;
        ///  - <b>o jogador:</b> anda da calçada até cada cômodo do térreo; do patamar da escada até cada cômodo do primeiro
        ///    andar (os de cima são iguais a ele); e da calçada até a cobertura, pela escada inteira.
        /// Com 'quantos', só os prédios mais perto. O resultado vai para o log e para `Conferencia_Predios.md` (no Editor, em
        /// `Benchmarks/`; no jogo compilado, na pasta de dados dele).
        /// </summary>
        public static IEnumerator RodarPredios(MovimentoPrimeiraPessoa movimento, DadosDoMundo d, Action<string> avisar, int quantos = int.MaxValue)
        {
            if (Rodando || movimento == null || d?.Plano == null) yield break;
            var origem = movimento.transform.position;
            var aqui = new Vector2(origem.x, origem.z);
            float giroAntes = movimento.transform.eulerAngles.y;
            var maisPerto = new Dictionary<string, (TracadoDaCidade t, Lote l, float dist)>();
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                {
                    if (l.Predio == null) continue;
                    float dist = Vector2.Distance(t.Mundo(l.MeioDaCasa), aqui);
                    string chave = VariacaoDo(l.Predio);
                    if (!maisPerto.TryGetValue(chave, out var atual) || dist < atual.dist) maisPerto[chave] = (t, l, dist);
                }
            var predios = new List<(TracadoDaCidade t, Lote l, float dist)>(maisPerto.Values);
            if (predios.Count == 0) { avisar?.Invoke("Nenhum prédio no mapa."); yield break; }
            predios.Sort((a, b) => a.dist.CompareTo(b.dist));
            if (predios.Count > quantos) predios.RemoveRange(quantos, predios.Count - quantos);

            Rodando = true;
            var controle = movimento.GetComponent<CharacterController>();
            bool voandoAntes = movimento.Voando;
            movimento.Voando = false;
            var relatorio = new StringBuilder();
            int comodos = 0, peloNavMesh = 0, andados = 0, chegaram = 0, subidas = 0, estouradas = 0;
            var falhas = new List<string>();
            var dono = new object();
            var caminho = new NavMeshPath();
            var lista = new List<Comodo>();
            var ligs = new List<Ligacao>();
            var cantos = new List<Vector3[]>();
            try
            {
                for (int ci = 0; ci < predios.Count; ci++)
                {
                    var (t, l, _) = predios[ci];
                    var p = l.Predio;
                    string variacao = $"{p.andares} andares, {VariacaoDo(p)}";
                    var M = t.MatrizDaCasa(l);
                    float sx = p.espelhar ? -1f : 1f;
                    // o começo: na calçada, na frente da porta da portaria
                    var frente = l.NaCasa(sx * p.xPorta, p.Z0);
                    bool aoLongoDeU = l.Frente.y != 0f;
                    float beira = aoLongoDeU ? (l.Frente.y < 0f ? l.V0 : l.V1) : (l.Frente.x < 0f ? l.U0 : l.U1);
                    if (aoLongoDeU) frente.y = beira; else frente.x = beira;
                    var w = t.Mundo(frente + l.Frente * 1.2f);
                    var inicio = new Vector3(w.x, d.Altura(w.x, w.y) + TracadoDaCidade.AlturaDaCalcada + 0.05f, w.y);
                    Status = $"Conferindo os prédios: {ci + 1} de {predios.Count} ({variacao})";
                    yield return IrPara(movimento, controle, inicio, dono);
                    var linha = new StringBuilder($"- {variacao}, {p.W:0.0} × {p.D:0.0} m em ({M.m03:F0}, {M.m23:F0}): ");
                    if (AndaresDe(p) == null)
                    {
                        falhas.Add($"{variacao}: o prédio não montou");
                        relatorio.AppendLine(linha + "não montou");
                        continue;
                    }

                    // o patamar da escada de cada andar (o caminho da rua até lá em cima é medido de patamar em patamar, e
                    // dele até o cômodo: com o prédio inteiro, a busca do NavMesh passa do limite de nós e devolve um caminho
                    // parcial, mesmo com tudo ligado)
                    Vector3 Patamar(int f) => M.MultiplyPoint3x4(new Vector3(sx * (p.ex0 + p.ex1) / 2, p.Base(f) + 0.05f, p.zc1 + 0.1f + PlantaDePredio.Patamar / 2));
                    var esperou = new string[1];

                    // o zumbi: os andares de 5 em 5
                    int doPredio = 0, chegouNoPredio = 0;
                    Vector3 ultimoPatamar = inicio;
                    bool pelaEscada = true;
                    for (int k = 0; k < p.andares; k += 5)
                    {
                        int ate = Mathf.Min(p.andares - 1, k + 4);
                        Status = $"Conferindo os prédios: {ci + 1} de {predios.Count}, andares {k} a {ate}";
                        yield return EsperarAndares(p, k, ate, dono, movimento, inicio, esperou);
                        if (esperou[0] != null) { estouradas++; falhas.Add($"{variacao}: os andares {esperou[0]}"); }
                        for (int f = k; f <= ate; f++)
                        {
                            p.Andar(f, lista, ligs);
                            var hp = new NavMeshHit();
                            if (f > 0 && pelaEscada)
                            {
                                pelaEscada = NavMesh.SamplePosition(Patamar(f), out hp, 0.8f, NavMesh.AllAreas) && Caminho(ultimoPatamar, hp.position, caminho);
                                if (pelaEscada) ultimoPatamar = hp.position;
                                else falhas.Add($"{variacao}: o zumbi não chega no patamar do andar {f} (do de baixo{(hp.hit ? ", " + caminho.status : ", sem NavMesh")})");
                            }
                            bool ateOPatamar = f == 0 || pelaEscada;
                            foreach (var c in lista)
                            {
                                comodos++;
                                doPredio++;
                                bool achou = AcharPonto(M, sx, c, p.Base(f), out var alvo);
                                bool ok = achou && ateOPatamar && Caminho(f == 0 ? inicio : hp.position, alvo, caminho);
                                if (ok) { peloNavMesh++; chegouNoPredio++; }
                                else if (ateOPatamar) falhas.Add($"{variacao}: o zumbi não chega no {c.tipo} do andar {f} ({(achou ? caminho.status.ToString() : "sem NavMesh")})");
                            }
                        }
                        AndaresDe(p)?.Esquecer(dono);
                    }
                    var cobertura = M.MultiplyPoint3x4(new Vector3(sx * (p.ex0 + p.ex1) / 2, p.Cobertura + 0.05f, p.zc1 - 1.2f));
                    var hc = new NavMeshHit();
                    var hpc = new NavMeshHit();
                    bool naCobertura = pelaEscada && NavMesh.SamplePosition(cobertura, out hc, 0.8f, NavMesh.AllAreas) &&
                                       NavMesh.SamplePosition(Patamar(p.andares), out hpc, 0.8f, NavMesh.AllAreas) &&
                                       Caminho(ultimoPatamar, hpc.position, caminho) && Caminho(hpc.position, hc.position, caminho);
                    if (!naCobertura) falhas.Add($"{variacao}: o zumbi não chega na cobertura ({(pelaEscada ? caminho.status.ToString() : "nem num andar de baixo")})");
                    linha.Append($"zumbi em {chegouNoPredio} de {doPredio} cômodos{(naCobertura ? " e na cobertura" : ", NÃO na cobertura")}; ");

                    // o jogador: o térreo (da calçada) e o primeiro andar (do patamar da escada; os de cima são iguais a ele)
                    foreach (int f in new[] { 0, 1 })
                    {
                        Vector3 de = inicio;
                        if (f == 0) yield return EsperarAndares(p, 0, 1, null, movimento, inicio, esperou);
                        else
                        {
                            var patamar = Patamar(f);
                            yield return IrPara(movimento, controle, patamar, dono);
                            yield return EsperarAndares(p, 0, Mathf.Min(p.andares - 1, 2), null, movimento, patamar, esperou);
                            de = NavMesh.SamplePosition(patamar, out var hp, 1f, NavMesh.AllAreas) ? hp.position : patamar;
                        }
                        if (esperou[0] != null) { estouradas++; falhas.Add($"{variacao}: com o jogador no andar {f}, os andares {esperou[0]}"); }
                        p.Andar(f, lista, ligs);
                        cantos.Clear();
                        foreach (var c in lista)
                            if (AcharPonto(M, sx, c, p.Base(f), out var alvo) && Caminho(de, alvo, caminho)) cantos.Add(caminho.corners);
                        int foram = 0, chegaramAqui = 0;
                        foreach (var cs in cantos)
                        {
                            Status = $"Conferindo os prédios: {ci + 1} de {predios.Count}, andando no andar {f}";
                            var resultado = new string[1];
                            andados++;
                            foram++;
                            yield return Andar(movimento, controle, de, cs, resultado);
                            if (resultado[0] == null) { chegaram++; chegaramAqui++; }
                            else falhas.Add($"{variacao}: o jogador {resultado[0]} no andar {f}");
                        }
                        linha.Append($"jogador no andar {f}: {chegaramAqui} de {foram}; ");
                    }
                    // a subida inteira, da calçada à cobertura (o caminho de patamar em patamar)
                    if (naCobertura)
                    {
                        yield return IrPara(movimento, controle, inicio, dono);
                        var subida = new List<Vector3>();
                        Vector3 ultimo = inicio;
                        bool inteira = true;
                        for (int f = 1; f <= p.andares + 1 && inteira; f++)
                        {
                            Vector3 proximo = hc.position;
                            if (f <= p.andares)
                            {
                                inteira = NavMesh.SamplePosition(Patamar(f), out var hs, 0.8f, NavMesh.AllAreas);
                                proximo = hs.position;
                            }
                            inteira = inteira && Caminho(ultimo, proximo, caminho);
                            if (!inteira) break;
                            foreach (var c in caminho.corners)
                                if (subida.Count == 0 || (c - subida[subida.Count - 1]).sqrMagnitude > 0.0001f) subida.Add(c);
                            ultimo = proximo;
                        }
                        var resultado = new string[1];
                        Status = $"Conferindo os prédios: {ci + 1} de {predios.Count}, subindo até a cobertura";
                        if (inteira) yield return Andar(movimento, controle, inicio, subida.ToArray(), resultado);
                        else resultado[0] = "sem o caminho da escada";
                        if (resultado[0] == null) { subidas++; linha.Append("subiu até a cobertura"); }
                        else { linha.Append($"subindo, {resultado[0]}"); falhas.Add($"{variacao}: o jogador {resultado[0]} subindo até a cobertura"); }
                    }
                    relatorio.AppendLine(linha.ToString());
                    // a memória depois de cada prédio (no Editor, uma rodada inteira já passou dos 9 GB e acabou a memória)
                    Debug.Log($"[Prédios] {linha} (a memória: {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576} MB da Unity, " +
                              $"{UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576} MB do Mono)");
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
            string cabecalho = $"{DateTime.Now:dd/MM/yyyy HH:mm} — {Tamanhos.NomeComLado(d.Tamanho)}, semente {d.Semente}, um prédio de cada variação ({predios.Count}), os mais perto de ({origem.x:F0}, {origem.z:F0})";
            string texto = $"## {cabecalho}\n\nOs cômodos: {comodos}; o zumbi chega em {peloNavMesh}; o jogador andou até {andados} (o térreo e o primeiro andar de cada prédio) " +
                           $"e chegou em {chegaram}; subiu da calçada à cobertura em {subidas} de {predios.Count}" +
                           (estouradas > 0 ? $"; {estouradas} esperas pelos andares e pelo NavMesh passaram de 60 s" : "") + $".\n\n{relatorio}";
            Debug.Log("[Prédios] " + texto);
            string arquivo = Gravar("Conferencia_Predios.md", "# A conferência dos prédios de verdade (Parte M6, segunda fatia)", texto);
            avisar?.Invoke($"Prédios: {predios.Count}; zumbi em {peloNavMesh} de {comodos} cômodos; jogador em {chegaram} de {andados}; subiu em {subidas}. " +
                           $"{(falhas.Count > 0 ? $"{falhas.Count} falhas" : "Sem falhas")} (em {arquivo}).");
        }

        // o relatório: no Editor, em Benchmarks/ do projeto; no jogo compilado, na pasta de dados dele (persistentDataPath,
        // como o benchmark da cidade). Devolve o caminho
        static string Gravar(string nome, string titulo, string texto)
        {
            string arquivo = Path.GetFullPath(Application.isEditor
                ? Path.Combine(Application.dataPath, "..", "Benchmarks", nome)
                : Path.Combine(Application.persistentDataPath, nome));
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo));
            if (!File.Exists(arquivo)) File.WriteAllText(arquivo, titulo + "\n\n");
            File.AppendAllText(arquivo, texto + "\n");
            return arquivo;
        }

        static readonly List<AndaresDoPredio> _naCena = new List<AndaresDoPredio>();

        // o prédio na cena agora (o bloco pode tirar e refazer as casas dele, e o objeto de antes some)
        static AndaresDoPredio AndaresDe(PlantaDePredio p)
        {
            _naCena.Clear();
            TerrenoDoMundo.Instancia?.JuntarPredios(_naCena);
            return _naCena.Find(a => a != null && a.Planta == p);
        }

        /// <summary>
        /// Espera os andares de 'de' a 'ate' do prédio carregarem (pedidos em nome de 'dono', se não for null) e um NavMesh
        /// montado depois disso (até 60 s, com o jogador parado em 'onde'): com os andares prontos, a área em volta é marcada
        /// para refazer, e vale a montagem seguinte (sem isso, os caminhos saíam de um NavMesh velho, ainda sem os andares, ou
        /// com o térreo aberto, sem as paredes). Em 'motivo[0]', null se deu certo; senão, o que faltou.
        /// </summary>
        static IEnumerator EsperarAndares(PlantaDePredio p, int de, int ate, object dono, MovimentoPrimeiraPessoa movimento, Vector3 onde, string[] motivo)
        {
            float limite = Time.realtimeSinceStartup + 60f;
            int montagens = -1, refeito = 0;
            AndaresDoPredio antes = null;
            motivo[0] = null;
            while (Time.realtimeSinceStartup < limite)
            {
                movimento.Teleportar(onde, 0f);
                ManterVivo(movimento);
                var nav = NavMeshDoMundo.Instancia;
                var andares = AndaresDe(p);
                if (andares != antes)
                {
                    // o pedido vai para o prédio de agora
                    if (antes != null) refeito++;
                    antes = andares;
                    montagens = -1;
                }
                if (andares != null && dono != null) andares.Pedir(dono, de, ate);
                if (nav != null && andares != null && andares.CarregadosDe(de, ate))
                {
                    if (montagens < 0)
                    {
                        montagens = nav.Montagens;
                        NavMeshDoMundo.Mudou(andares.CaixaDoAndar(de));
                    }
                    else if (nav.Montagens > montagens && nav.Pronto) yield break;
                }
                yield return null;
            }
            var agora = AndaresDe(p);
            var estado = new StringBuilder();
            if (agora != null) for (int f = 0; f < p.andares; f++) estado.Append(agora.CarregadosDe(f, f) ? '1' : '0');
            motivo[0] = $"de {de} a {ate}: a espera passou de 60 s ({(agora == null ? "o prédio não está na cena" : $"carregados {estado}")}" +
                        $"{(refeito > 0 ? $", refeito {refeito} vezes" : "")}{(montagens < 0 ? "" : $", {NavMeshDoMundo.Instancia.Montagens - montagens} montagens do NavMesh depois")}; " +
                        $"o NavMesh: {NavMeshDoMundo.Instancia?.Estado}; o terreno em volta: {TerrenoDoMundo.Instancia?.QueFalta(onde, 84f) ?? "pronto"})";
            Debug.LogWarning("[Prédios] " + motivo[0]);
        }

        // a conferência leva mais de meia hora: sem comer e beber, o jogador caía (de sede) no meio dela e voltava para casa
        static void ManterVivo(MovimentoPrimeiraPessoa movimento)
        {
            var vida = movimento.GetComponent<VidaJogador>();
            if (vida != null && vida.IsServer && vida.IsSpawned) vida.Vida.Value = vida.vidaMaxima;
            var fomeESede = movimento.GetComponent<FomeESede>();
            if (fomeESede != null && fomeESede.IsServer && fomeESede.IsSpawned)
            {
                fomeESede.Fome.Value = 100f;
                fomeESede.Sede.Value = 100f;
            }
        }

        // o caminho completo pelo NavMesh até perto do alvo
        static bool Caminho(Vector3 de, Vector3 ate, NavMeshPath caminho) =>
            NavMesh.CalculatePath(de, ate, NavMesh.AllAreas, caminho) && caminho.status == NavMeshPathStatus.PathComplete &&
            Vector3.Distance(caminho.corners[caminho.corners.Length - 1], ate) < 0.6f;

        // leva o jogador até o ponto e espera o chão, as construções e o NavMesh em volta (até 30 s)
        static IEnumerator IrPara(MovimentoPrimeiraPessoa movimento, CharacterController controle, Vector3 ponto, object dono)
        {
            controle.enabled = false;
            movimento.Teleportar(ponto, 0f);
            controle.enabled = true;
            TerrenoDoMundo.Instancia?.Pedir(dono, ponto, 50f, true);
            float limite = Time.realtimeSinceStartup + 30f;
            yield return null;
            while (Time.realtimeSinceStartup < limite &&
                   (TerrenoDoMundo.Instancia == null || !TerrenoDoMundo.Instancia.Pronto(ponto, 40f) ||
                    NavMeshDoMundo.Instancia == null || !NavMeshDoMundo.Instancia.Pronto || !NavMeshDoMundo.TemEm(ponto)))
            {
                movimento.Teleportar(ponto, 0f);
                ManterVivo(movimento);
                yield return null;
            }
        }

        // um ponto do NavMesh no chão do cômodo: o meio, ou perto dos cantos (o meio pode cair no vão da escada)
        static bool AcharPonto(Matrix4x4 M, float sx, Comodo k, float y, out Vector3 ponto)
        {
            float mx = Mathf.Min(0.9f, (k.x1 - k.x0) * 0.3f), mz = Mathf.Min(0.9f, (k.z1 - k.z0) * 0.3f);
            var tentativas = new[]
            {
                new Vector2(k.Cx, k.Cz), new Vector2(k.x0 + mx, k.z0 + mz), new Vector2(k.x1 - mx, k.z0 + mz),
                new Vector2(k.x1 - mx, k.z1 - mz), new Vector2(k.x0 + mx, k.z1 - mz),
            };
            foreach (var q in tentativas)
            {
                var alvo = M.MultiplyPoint3x4(new Vector3(sx * q.x, y + 0.05f, q.y));
                if (NavMesh.SamplePosition(alvo, out var hit, 0.8f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - alvo.y) < 0.35f)
                {
                    ponto = hit.position;
                    return true;
                }
            }
            ponto = default;
            return false;
        }

        // o jogador anda pelos cantos do caminho, com o CharacterController e a gravidade: null se chegou, senão o que houve
        static IEnumerator Andar(MovimentoPrimeiraPessoa movimento, CharacterController controle, Vector3 inicio, Vector3[] cantos, string[] resultado)
        {
            controle.enabled = false;
            movimento.Teleportar(inicio, 0f);
            controle.enabled = true;
            movimento.enabled = false;
            try
            {
                float vy = 0f, parado = 0f;
                Vector3 ultimo = controle.transform.position;
                for (int i = 1; i < cantos.Length; i++)
                {
                    var alvo = cantos[i];
                    float limite = Time.realtimeSinceStartup + 20f;
                    while (true)
                    {
                        ManterVivo(movimento);
                        var pos = controle.transform.position;
                        var falta = new Vector3(alvo.x - pos.x, 0f, alvo.z - pos.z);
                        if (falta.magnitude < 0.25f && Mathf.Abs(alvo.y - pos.y) < 1.2f) break;
                        // caiu: bem abaixo do caminho
                        if (pos.y < Mathf.Min(alvo.y, cantos[i - 1].y) - 1.5f) { resultado[0] = $"caiu em ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})"; yield break; }
                        float dt = Mathf.Min(Time.deltaTime, 0.05f);
                        vy = controle.isGrounded ? -2f : vy + Gravidade * dt;
                        var passo = falta.normalized * Mathf.Min(Velocidade * dt, falta.magnitude);
                        controle.Move(passo + Vector3.up * vy * dt);
                        if (falta.sqrMagnitude > 0.01f) movimento.Olhar(Mathf.Atan2(falta.x, falta.z) * Mathf.Rad2Deg, 10f);
                        // preso: não sai do lugar por 2 s
                        if ((controle.transform.position - ultimo).sqrMagnitude > 0.04f) { ultimo = controle.transform.position; parado = 0f; }
                        else if ((parado += dt) > 2f || Time.realtimeSinceStartup > limite) { resultado[0] = $"prendeu em ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})"; yield break; }
                        yield return null;
                    }
                }
            }
            finally
            {
                movimento.enabled = true;
            }
        }
    }
}
