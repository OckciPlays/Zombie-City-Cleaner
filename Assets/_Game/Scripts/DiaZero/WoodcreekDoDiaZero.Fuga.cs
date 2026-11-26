using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Roupas;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// A Woodcreek na noite do colapso (23/11/2026, a fuga: as etapas 5 e 6 do Docs/23; o Pedro: "eu já imagino o caos,
    /// pessoas sendo devoradas pelos Ocos, brutamontes na cidade, long também"). Na mesma vila de antes, o palco troca o dia
    /// normal pelo caos (<see cref="VirarAFuga"/>, em cada máquina, uma vez):
    ///  - as pessoas e os carros do Dia 0 saem; no lugar, quem corre pelas ruas fugindo e os corpos no chão, com o sangue
    ///    (<see cref="LugaresDasVitimas"/>: os Ocos de verdade, do anfitrião, ficam em cima deles);
    ///  - os carros batidos atravessados na rua, uns pegando fogo (a luz que tremula), e as sirenes de longe;
    ///  - na chegada, o portão do bunker aceso, a comunidade correndo para dentro e o Gus Rodrigues no portão, chamando
    ///    (<see cref="ChegadaNoBunker"/>).
    /// O caminho da fuga: da casa, a Spruce Lane, a North Shore Highway pelo comércio, a East Shore Highway e a Sawmill Road,
    /// descendo até o bunker na beira do lago.
    /// </summary>
    /// <summary>
    /// Quem corre para dentro do bunker na chegada da fuga (só o desenho; 24/11/2026, o Pedro: "na porta do bunker, os npc
    /// simplesmente surgem do nada e entram para dentro"): já está no pátio, de pé e olhando para a rua, desde o começo da
    /// noite; quando alguém chega a <see cref="Chama"/> m de onde ele está (a abertura da cerca, a uns 40 m da porta), espera
    /// a vez dele e corre para dentro, uma vez só, e some lá dentro (entrou). Não volta a aparecer.
    /// </summary>
    public class CorrendoParaOBunker : MonoBehaviour
    {
        public const float Chama = 40f;
        public Vector3[] caminho;
        public float espera, velocidade = 3.6f;
        int _ponto = 1;
        float _falta;
        bool _chamado, _dentro;
        Animator _an;
        Renderer[] _desenho;

        void Start()
        {
            _desenho = GetComponentsInChildren<Renderer>(true);
            _an = GetComponentInChildren<Animator>();
            if (caminho != null && caminho.Length > 0) transform.position = caminho[0];
            if (_an != null) _an.Play("Parado", 0, Random.value);
        }

        void Update()
        {
            if (_dentro || caminho == null || caminho.Length < 2) return;
            if (!_chamado)
            {
                // de pé no pátio, olhando para a rua, até alguém chegar perto (descendo a rua, antes da cerca)
                var aqui = caminho[0];
                foreach (var j in JogadorRede.Todos)
                {
                    if (j == null || !j.Criado) continue;
                    var d = j.transform.position - aqui;
                    d.y = 0f;
                    if (d.sqrMagnitude < Chama * Chama)
                    {
                        _chamado = true;
                        _falta = espera;
                        break;
                    }
                }
                return;
            }
            if (_falta > 0f)
            {
                _falta -= Time.deltaTime;
                if (_falta > 0f) return;
                if (_an != null) _an.CrossFadeInFixedTime("Correr", 0.2f);
            }
            var alvo = caminho[_ponto];
            var mover = alvo - transform.position;
            mover.y = 0f;
            if (mover.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(mover);
            transform.position = Vector3.MoveTowards(transform.position, alvo, velocidade * Time.deltaTime);
            if ((transform.position - alvo).sqrMagnitude > 0.04f) return;
            if (++_ponto < caminho.Length) return;
            // entrou: some lá dentro
            _dentro = true;
            foreach (var r in _desenho) if (r != null) r.enabled = false;
        }
    }

    public partial class WoodcreekDoDiaZero
    {
        /// <summary>O caminho da fuga no mapa (da casa ao portão do bunker, pelas ruas da vila).</summary>
        public readonly List<Vector2> CaminhoDaFuga = new List<Vector2>();
        /// <summary>Onde ficam os corpos no chão (no mundo) e o giro de cada um: os Ocos do anfitrião ficam em cima deles.</summary>
        public readonly List<(Vector3 ponto, float giro)> LugaresDasVitimas = new List<(Vector3, float)>();

        bool _naFuga;
        Transform _caos;
        readonly List<(Light luz, float base_, float semente)> _fogos = new List<(Light, float, float)>();
        int _chamasNosCarros;
        // (25/11/2026, o save do Dia 0) o quadro em que a vila virou a noite do colapso, e os NPCs de quem foge
        int _quadroDaFuga;
        NpcsDoDiaZero _npcsDaFuga;

        /// <summary>
        /// A noite do colapso já está montada para as figuras dos Ocos (25/11/2026, o save do Dia 0): os NPCs do dia já saíram (o
        /// Destroy só acontece no fim do quadro) e os de quem foge já estão na rua (eles nascem no Update seguinte). Antes, o
        /// palco que entra já na fuga (a fuga que voltou do save, o cliente que entra no meio dela) fazia tudo no mesmo quadro,
        /// e os perseguidores ficavam sem quem perseguir. Sem os NPCs (sem o modelo deles), libera em uns 2 s.
        /// </summary>
        public bool ProntaParaAsFiguras =>
            _naFuga && Time.frameCount > _quadroDaFuga + 1 &&
            (_npcsDaFuga == null || _npcsDaFuga.npc == null || _npcsDaFuga.Quantos > 0 || Npcs.Count == 0 || Time.frameCount > _quadroDaFuga + 120);

        /// <summary>A vila vira a noite do colapso (nesta máquina, uma vez por fuga).</summary>
        public void VirarAFuga()
        {
            if (_naFuga || !Pronta) return;
            _naFuga = true;
            _quadroDaFuga = Time.frameCount;
            var lw = LakewoodNaCena.Instancia;
            if (lw == null) return;
            // as pessoas e os carros do dia normal saem
            var npcs = GetComponent<NpcsDoDiaZero>();
            GameObject npc = npcs != null ? npcs.npc : null;
            Material particulas = npcs != null ? npcs.particulas : null;
            if (npcs != null) Destroy(npcs);
            var carros = GetComponent<CarrosDaWoodcreek>();
            if (carros != null) Destroy(carros);
            _caos = new GameObject("A noite do colapso").transform;
            _caos.SetParent(transform, false);
            MontarOCaminho(lw);
            // quem corre fugindo pelas ruas: os NPCs do Dia 0 de novo, com as rotinas da fuga (correndo, em laço)
            Npcs.Clear();
            PorQuemFoge(lw);
            var novos = gameObject.AddComponent<NpcsDoDiaZero>();
            novos.npc = npc;
            novos.particulas = particulas;
            novos.carros = 0;
            _npcsDaFuga = novos;
            if (npc != null) PorOsCorpos(lw, npc);
            PorOsCarrosBatidos(lw);
            PorAChegada(lw, npc);
            int sirenes = PorAsSirenes();
            Debug.Log($"[Fuga] A vila na noite do colapso: {Npcs.Count} fugindo, {LugaresDasVitimas.Count} corpos, {_fogos.Count + _chamasNosCarros} fogos, {sirenes} sirenes.");
        }

        /// <summary>
        /// As sirenes da noite do colapso (23/11/2026, a pedido: "sirenes tocando"): duas sirenes de alerta da defesa civil
        /// (SonsDoDiaZero.Sirenes, gravadas), no alto de um poste, uma a uns 130 m da casa e outra perto do lago, fora do
        /// caminho, em laço a noite toda. Cada uma começa num ponto do ciclo, e a segunda é um pouco mais aguda: não soam
        /// juntas. Devolve quantas.
        /// </summary>
        int PorAsSirenes()
        {
            var sirenes = SonsDoDiaZero.Sirenes;
            if (sirenes == null || sirenes.Length == 0 || CaminhoDaFuga.Count < 2) return 0;
            // (a primeira a uns 130 m da casa, para o despertar; a segunda já perto do lago)
            float[] fracoes = { 0.08f, 0.7f }, desvios = { 100f, -120f };
            for (int i = 0; i < fracoes.Length; i++)
            {
                var go = new GameObject($"Sirene {i + 1}");
                go.transform.SetParent(_caos, true);
                go.transform.position = NoCaminhoNoMundo(fracoes[i], desvios[i], 12f);
                var som = go.AddComponent<AudioSource>();
                som.clip = sirenes[i % sirenes.Length];
                som.loop = true;
                som.spatialBlend = 1f;
                som.rolloffMode = AudioRolloffMode.Logarithmic;
                som.minDistance = 80f;
                som.maxDistance = 900f;
                som.dopplerLevel = 0f;
                som.time = Random.value * som.clip.length * 0.9f;
                Combat.MixerDeSom.NoCanal(som, Combat.CanalDoSom.Objetos, 0.8f);
                som.Play();
            }
            return fracoes.Length;
        }

        // o fogo tremulando (no LateUpdate da WoodcreekDoDiaZero)
        void TremularOsFogos()
        {
            for (int i = 0; i < _fogos.Count; i++)
            {
                var (luz, base_, semente) = _fogos[i];
                if (luz == null) continue;
                luz.intensity = base_ * (0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 6f, semente));
            }
        }

        float[] _aoLongo;

        /// <summary>O comprimento do caminho da fuga (m).</summary>
        public float ComprimentoDaFuga => _aoLongo != null && _aoLongo.Length > 0 ? _aoLongo[_aoLongo.Length - 1] : 0f;

        /// <summary>O ponto do caminho da fuga (no mapa) na fração dele (0 a casa, 1 o portão da cerca) e a direção ali.</summary>
        public Vector2 NoCaminho(float fracao, out Vector2 direcao)
        {
            direcao = Vector2.up;
            if (CaminhoDaFuga.Count == 0) return Vector2.zero;
            if (CaminhoDaFuga.Count == 1 || _aoLongo == null) return CaminhoDaFuga[0];
            float s = Mathf.Clamp01(fracao) * ComprimentoDaFuga;
            int i = System.Array.BinarySearch(_aoLongo, s);
            if (i < 0) i = ~i - 1;
            i = Mathf.Clamp(i, 0, CaminhoDaFuga.Count - 2);
            float seg = _aoLongo[i + 1] - _aoLongo[i];
            var a = CaminhoDaFuga[i];
            var b = CaminhoDaFuga[i + 1];
            if ((b - a).sqrMagnitude > 1e-6f) direcao = (b - a).normalized;
            return Vector2.Lerp(a, b, seg > 1e-4f ? (s - _aoLongo[i]) / seg : 0f);
        }

        /// <summary>O ponto do caminho com o desvio para o lado (no mundo, no chão de lá).</summary>
        public Vector3 NoCaminhoNoMundo(float fracao, float desvio, float acima = 0.05f)
        {
            var p = NoCaminho(fracao, out var dir) + new Vector2(-dir.y, dir.x) * desvio;
            var chao = LakewoodNaCena.Instancia.ChaoNoMapa;
            return OrigemDeLakewood.NoMundo(new Vector3(p.x, chao.Altura(p.x, p.y) + acima, p.y));
        }

        // o caminho da fuga pelas ruas de verdade (o menor caminho pela rede das estradas e das ruas da vila, da frente da
        // casa ao portão da cerca do bunker), um ponto a cada 6 m, no mapa
        void MontarOCaminho(LakewoodNaCena lw)
        {
            CaminhoDaFuga.Clear();
            var chao = lw.ChaoNoMapa;
            var inicio = LoteDaFamilia.Lote.MeioDaFrente;
            var fim = chao.Bunker != null ? chao.Bunker.NoMapa(PlantaDeBunker.ChegadaDaEstrada.x, PlantaDeBunker.ChegadaDaEstrada.y - 2f) : inicio;
            var min = Vector2.Min(inicio, fim) - Vector2.one * 250f;
            var max = Vector2.Max(inicio, fim) + Vector2.one * 250f;
            var pts = new List<Vector2>();
            var donos = new List<int>();
            var viz = new List<List<int>>();
            var grade = new Dictionary<Vector2Int, List<int>>();
            Vector2Int Cel(Vector2 q) => new Vector2Int(Mathf.FloorToInt(q.x / 8f), Mathf.FloorToInt(q.y / 8f));
            for (int e = 0; e < chao.Estradas.Count; e++)
            {
                var est = chao.Estradas[e];
                if (est.DaUsina || est.Pontos == null) continue;
                int anterior = -1;
                foreach (var q in est.Pontos)
                {
                    if (q.x < min.x || q.x > max.x || q.y < min.y || q.y > max.y) { anterior = -1; continue; }
                    int k = pts.Count;
                    pts.Add(q);
                    donos.Add(e);
                    viz.Add(new List<int>());
                    if (anterior >= 0) { viz[k].Add(anterior); viz[anterior].Add(k); }
                    anterior = k;
                    var c = Cel(q);
                    if (!grade.TryGetValue(c, out var l)) grade[c] = l = new List<int>();
                    l.Add(k);
                }
            }
            // os cruzamentos: os pontos de estradas diferentes a menos de 6 m
            for (int k = 0; k < pts.Count; k++)
            {
                var c = Cel(pts[k]);
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!grade.TryGetValue(new Vector2Int(c.x + dx, c.y + dz), out var l)) continue;
                    foreach (int j in l)
                        if (j > k && donos[j] != donos[k] && (pts[j] - pts[k]).sqrMagnitude < 36f && !viz[k].Contains(j))
                        {
                            viz[k].Add(j);
                            viz[j].Add(k);
                        }
                }
            }
            int Perto(Vector2 alvo)
            {
                int melhor = -1;
                float d = float.MaxValue;
                for (int k = 0; k < pts.Count; k++)
                {
                    float dk = (pts[k] - alvo).sqrMagnitude;
                    if (dk < d) { d = dk; melhor = k; }
                }
                return melhor;
            }
            int a = Perto(inicio), b = Perto(fim);
            var rota = new List<Vector2>();
            if (a >= 0 && b >= 0)
            {
                // o menor caminho (Dijkstra)
                var dist = new float[pts.Count];
                var de = new int[pts.Count];
                for (int k = 0; k < dist.Length; k++) { dist[k] = float.MaxValue; de[k] = -1; }
                dist[a] = 0f;
                var fila = new SortedSet<(float d, int k)>();
                fila.Add((0f, a));
                while (fila.Count > 0)
                {
                    var (d, k) = fila.Min;
                    fila.Remove(fila.Min);
                    if (k == b) break;
                    if (d > dist[k]) continue;
                    foreach (int j in viz[k])
                    {
                        float nd = d + Vector2.Distance(pts[k], pts[j]);
                        if (nd >= dist[j]) continue;
                        if (dist[j] < float.MaxValue) fila.Remove((dist[j], j));
                        dist[j] = nd;
                        de[j] = k;
                        fila.Add((nd, j));
                    }
                }
                if (de[b] >= 0 || a == b)
                    for (int k = b; k >= 0; k = de[k]) rota.Add(pts[k]);
                rota.Reverse();
            }
            if (rota.Count < 2)
            {
                Debug.LogWarning("[Fuga] Sem o caminho pelas ruas até o bunker: em linha reta.");
                rota.Clear();
                rota.Add(inicio);
                rota.Add(fim);
            }
            // a frente da casa, a rota (um ponto a cada 6 m) e o portão da cerca
            CaminhoDaFuga.Add(inicio);
            var ultimo = inicio;
            foreach (var q in rota)
                if ((q - ultimo).sqrMagnitude >= 36f) { CaminhoDaFuga.Add(q); ultimo = q; }
            CaminhoDaFuga.Add(fim);
            _aoLongo = new float[CaminhoDaFuga.Count];
            for (int k = 1; k < CaminhoDaFuga.Count; k++) _aoLongo[k] = _aoLongo[k - 1] + Vector2.Distance(CaminhoDaFuga[k - 1], CaminhoDaFuga[k]);
            Debug.Log($"[Fuga] O caminho da fuga pelas ruas: {ComprimentoDaFuga:F0} m ({CaminhoDaFuga.Count} pontos, {pts.Count} pontos de estrada na volta).");
        }

        // quem corre fugindo: pelas ruas do caminho, uns indo, outros voltando, alguns em volta do comércio
        void PorQuemFoge(LakewoodNaCena lw)
        {
            var chao = lw.ChaoNoMapa;
            var rnd = new System.Random(semente + 4242);
            Vector3 W(Vector2 m) => OrigemDeLakewood.NoMundo(new Vector3(m.x, chao.Altura(m.x, m.y) + 0.03f, m.y));
            string[] tipos = { "Civil", "Estudante", "Idoso", "Civil", "Trabalhador", "Civil", "Estudante", "Civil" };
            float L = Mathf.Max(1f, ComprimentoDaFuga);
            for (int i = 0; i < 22; i++)
            {
                // um trecho de 50 a 90 m do caminho, com o desvio para o lado (as calçadas), de ida e de volta
                float f0 = 0.04f + (float)rnd.NextDouble() * 0.85f, f1 = Mathf.Min(1f, f0 + (50f + (float)rnd.NextDouble() * 40f) / L);
                float desvio = (float)rnd.NextDouble() * 8f - 4f;
                var trecho = new List<Vector3>();
                for (int k = 0; k <= 8; k++) trecho.Add(NoCaminhoNoMundo(Mathf.Lerp(f0, f1, k / 8f), desvio, 0.03f));
                var volta = Enumerable.Reverse(trecho).Select(v => v + Vector3.up * 0.001f).ToList();
                var caminho = (i % 2 == 0 ? trecho.Concat(volta) : volta.Concat(trecho)).ToArray();
                var l = new LugarDeNpc { rotina = Rotina.Correr, ponto = caminho[0], giro = 0f, tipo = tipos[i % tipos.Length], semente = 5000 + i * 31, caminho = caminho };
                l.doente = i % 5 == 4;
                Npcs.Add(l);
            }
        }

        // os corpos no chão, com o sangue embaixo (na rua e nas calçadas, perto do caminho)
        void PorOsCorpos(LakewoodNaCena lw, GameObject npc)
        {
            LugaresDasVitimas.Clear();
            var chao = lw.ChaoNoMapa;
            var rnd = new System.Random(semente + 777);
            var obra = new Obra(semente + 778, Medidas.DoJogo);
            // (24/11/2026: 12 corpos, a pedido: "alguns mortos no chão e sendo devorados"; os Ocos em cima são as figuras,
            // WoodcreekDoDiaZero.Colapso.cs)
            for (int i = 0; i < 12; i++)
            {
                float lado = (i % 2 == 0 ? 1f : -1f) * (1.5f + (float)rnd.NextDouble() * 3.5f);
                float giro = (float)rnd.NextDouble() * 360f;
                var mundo = NoCaminhoNoMundo(0.06f + i * 0.075f + (float)rnd.NextDouble() * 0.03f, lado, 0.12f);
                LugaresDasVitimas.Add((mundo, giro));
                // deitado de costas (como os doentes da introdução, no chão), vestido pelo sorteio
                var corpo = Instantiate(npc, mundo, Quaternion.Euler(-90f, giro, 0f), _caos);
                corpo.name = $"Corpo {i + 1}";
                var v = corpo.GetComponent<Vestimenta>();
                if (v != null) { v.npc = ""; v.VestirSorteado(i % 3 == 0 ? "Idoso" : "Civil", 900 + i); }
                corpo.AddComponent<NpcDoCinema>().Iniciar("Parado", NpcDoCinema.Pose.BracosAbertos);
                foreach (var c in corpo.GetComponentsInChildren<Collider>()) c.enabled = false;
                // o sangue: umas manchas chatas no chão em volta
                var local = _caos.InverseTransformPoint(mundo);
                for (int k = 0; k < 4; k++)
                {
                    var o = new Vector3((float)rnd.NextDouble() * 1.6f - 0.8f, -0.1f + 0.004f * k, (float)rnd.NextDouble() * 1.6f - 0.8f);
                    obra.Caixa(local + o, new Vector3(0.5f + (float)rnd.NextDouble() * 0.9f, 0.006f, 0.4f + (float)rnd.NextDouble() * 0.8f), "Sangue", (float)rnd.NextDouble() * 180f);
                }
            }
            Desenhar("Sangue", obra);
        }

        // os carros batidos atravessados na rua, uns pegando fogo (a luz laranja que tremula e a fumaça escura em cima)
        void PorOsCarrosBatidos(LakewoodNaCena lw)
        {
            if (carros == null || carros.Length == 0) return;
            var chao = lw.ChaoNoMapa;
            var rnd = new System.Random(semente + 555);
            for (int i = 0; i < 9; i++)
            {
                float f = 0.12f + i * 0.09f + (float)rnd.NextDouble() * 0.03f;
                NoCaminho(f, out var dir);
                var mundo = NoCaminhoNoMundo(f, (float)rnd.NextDouble() * 5f - 2.5f, 0.03f);
                float giro = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 35f + (float)rnd.NextDouble() * 110f;
                var malha = carros[rnd.Next(carros.Length)];
                var go = new GameObject($"Carro batido {i + 1}");
                go.transform.SetParent(_caos, true);
                go.transform.SetPositionAndRotation(mundo, Quaternion.Euler(0f, giro, (float)rnd.NextDouble() * 6f - 3f));
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                var caixa = go.AddComponent<BoxCollider>();
                caixa.center = malha.bounds.center;
                caixa.size = malha.bounds.size;
                // o alarme disparado (em laço), num sim, num não
                if (i % 2 == 1)
                {
                    var alarme = Resources.Load<Vehicles.SonsDoAlarme>("SonsDoAlarme");
                    if (alarme != null && alarme.sirenes != null && alarme.sirenes.Length > 0)
                    {
                        var som = go.AddComponent<AudioSource>();
                        som.clip = alarme.sirenes[i % alarme.sirenes.Length];
                        som.loop = true;
                        som.spatialBlend = 1f;
                        som.rolloffMode = AudioRolloffMode.Linear;
                        som.minDistance = 4f;
                        som.maxDistance = 70f;
                        Combat.MixerDeSom.NoCanal(som, Combat.CanalDoSom.Veiculos, 0.45f);
                        som.Play();
                    }
                    continue;
                }
                // o fogo do jogo (as chamas, a luz tremulando e o som), em cima do carro
                var fogos = Loot.ConstrucoesNoMundo.Instancia;
                if (fogos != null && fogos.materialDoFogo != null)
                {
                    var chama = Loot.ConstrucoesNoMundo.Fogo(go.transform, malha.bounds.center + Vector3.up * (malha.bounds.extents.y + 0.1f), fogos.materialDoFogo, 1.6f, 14f, true, go.layer);
                    var luz = chama.GetComponent<Light>();
                    if (luz != null) chama.AddComponent<Loot.ChamaTremula>().luz = luz;
                    Combat.SomDasChamas.Por(chama, 1.4f);
                    _chamasNosCarros++;
                }
                else
                {
                    var fogo = new GameObject("Fogo").AddComponent<Light>();
                    fogo.transform.SetParent(go.transform, false);
                    fogo.transform.localPosition = malha.bounds.center + Vector3.up * 1.2f;
                    fogo.type = LightType.Point;
                    fogo.range = 16f;
                    fogo.color = new Color(1f, 0.55f, 0.2f);
                    fogo.shadows = LightShadows.None;
                    _fogos.Add((fogo, 6f, i * 1.7f));
                }
            }
        }

        // ------------------------------------------------------------------ a chegada no bunker

        /// <summary>O Gus no portão do bunker (no mundo), para a fala dele na tela (FugaNaTela).</summary>
        public Vector3? ChamadoNoPortao { get; private set; }

        // na chegada (o Pedro: "vamos ver o bunker, algumas pessoas entrando e vamos entrar junto"): a luz no portão aberto, o
        // Gus Rodrigues do lado dele, com os braços abertos chamando, e a comunidade correndo do portão da cerca para dentro
        void PorAChegada(LakewoodNaCena lw, GameObject npc)
        {
            var b = lw.ChaoNoMapa.Bunker;
            if (b == null) return;
            Vector3 L(float x, float z, float y = 0.05f)
            {
                var p = b.NoMapa(x, z);
                return OrigemDeLakewood.NoMundo(new Vector3(p.x, b.Altura + y, p.y));
            }
            float xp = PlantaDeBunker.PortaoX0 + PlantaDeBunker.PortaoLargura * 0.5f, z0 = PlantaDeBunker.Z0;
            var portao = L(xp, z0 - 0.6f);
            var rua = L((PlantaDeBunker.VaoDaCercaX0 + PlantaDeBunker.VaoDaCercaX1) * 0.5f, PlantaDeBunker.Patio.yMin + 2f);
            // a luz amarela em cima do portão
            var luz = new GameObject("Luz do portão").AddComponent<Light>();
            luz.transform.SetParent(_caos, true);
            luz.transform.position = L(xp, z0 - 1.2f, PlantaDeBunker.PortaoAltura + 0.6f);
            luz.type = LightType.Point;
            luz.range = 22f;
            luz.intensity = 4f;
            luz.color = new Color(1f, 0.86f, 0.55f);
            luz.shadows = LightShadows.None;
            if (npc == null) return;
            // o Gus, do lado do portão, de frente para quem desce a rua
            var gus = L(xp + PlantaDeBunker.PortaoLargura * 0.5f + 1.2f, z0 - 1.6f);
            float paraARua = Mathf.Atan2(rua.x - gus.x, rua.z - gus.z) * Mathf.Rad2Deg;
            var g = Instantiate(npc, gus, Quaternion.Euler(0f, paraARua, 0f), _caos);
            g.name = "Gus Rodrigues (o portão)";
            var v = g.GetComponent<Vestimenta>();
            if (v != null) { v.npc = ""; v.VestirSorteado("Trabalhador", 64); }
            g.AddComponent<NpcDoCinema>().Iniciar("Parado", NpcDoCinema.Pose.BracosAbertos);
            ChamadoNoPortao = gus + Vector3.up * 1.7f;
            // a comunidade correndo para dentro (cada um volta para o portão da cerca depois de um tempo)
            string[] tipos = { "Civil", "Idoso", "Estudante", "Civil", "Trabalhador" };
            for (int i = 0; i < 5; i++)
            {
                var de = L(PlantaDeBunker.VaoDaCercaX0 + 1f + i * 1.3f, PlantaDeBunker.Patio.yMin + 1f);
                var meio = L(xp + (i - 2) * 0.6f, z0 - 3f);
                var dentro = L(xp + (i - 2) * 0.8f, z0 + 6f);
                var c = Instantiate(npc, de, Quaternion.identity, _caos);
                c.name = $"Entrando no bunker {i + 1}";
                var vc = c.GetComponent<Vestimenta>();
                if (vc != null) { vc.npc = ""; vc.VestirSorteado(tipos[i], 640 + i); }
                // (24/11/2026: já de pé no pátio, olhando para a rua; correm para dentro, uma vez, com alguém perto)
                c.transform.rotation = Quaternion.Euler(0f, paraARua + (i - 2) * 12f, 0f);
                var corre = c.AddComponent<CorrendoParaOBunker>();
                corre.caminho = new[] { de, meio, dentro };
                corre.espera = 0.4f + i * 0.9f;
            }
        }

        void Desenhar(string nome, Obra o)
        {
            if (o.vis.Triangulos > 0)
            {
                var go = new GameObject(nome);
                go.transform.SetParent(_caos, false);
                go.AddComponent<MeshFilter>().sharedMesh = o.vis.ParaMesh(nome);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            MalhaDeObra.Devolver(o.vis);
            MalhaDeObra.Devolver(o.col);
        }
    }
}
