using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// As obras dos lotes das cidades do Lakewood (Plano_Lakewood, Etapa 6, a parte 3): de longe, a caixa de cada uma (as
    /// paredes e o telhado, numa malha por lote, sempre); perto de alguém (a câmera ou um jogador, a menos de
    /// <see cref="raioDasObras"/>), a obra de verdade do kit (a casa, a loja ou o posto: <see cref="ObrasDeLakewood"/>),
    /// montada em outra thread, com a colisão cozida em outra thread, a hera, as pichações, os carros parados na garagem, os
    /// móveis (os antigos e a mobília nova, com o saque, quando a cena tem os sistemas deles), as portas que abrem, os
    /// pisos dos passos, a caixa de dentro e o eco. A obra sai 3 s depois de ninguém estar perto, e a caixa volta.
    /// Os caminhos de concreto da porta e da garagem até a rua são uma malha só por cidade, feita no começo.
    /// </summary>
    public partial class LakewoodNaCena
    {
        [Header("As obras das cidades (Etapa 6)")]
        [Tooltip("O material das casas de verdade (AbandonoDasCasas: a altura sobre o chão na cor do vértice). Sem ele, o da cidade.")]
        public Material materialDasCasas;
        [Tooltip("A hera (o material da vegetação) e as pichações.")]
        public Material materialDaHera, materialDasPichacoes;
        [Tooltip("As marcas do abandono (Marcas.mat: a loja de armas).")]
        public Material materialDasMarcas;
        [Tooltip("Até onde as obras de verdade aparecem em volta de alguém (m).")]
        public float raioDasObras = 95f;
        [Tooltip("Sem a neblina, até onde as obras de verdade ficam montadas (m): a troca da caixa de longe ficava à vista.")]
        public float raioDasObrasSemNeblina = 170f;
        [Tooltip("Quantas obras montam ao mesmo tempo (em outras threads).")]
        public int obrasAoMesmoTempo = 2;

        sealed class ObraNaCena
        {
            public ChaoDeLakewood.LoteNoChao Lote;
            public PlanoDaObra Plano;
            public MeshRenderer Longe;
            public GameObject Perto;
            public Task<ObraPronta> Montando;
            public ObraPronta Pronta;
            public readonly List<Mesh> Malhas = new List<Mesh>();
            public readonly List<(MeshCollider c, Mesh m, Task t)> Cozinhando = new List<(MeshCollider, Mesh, Task)>();
            public float QueridaEm = -100f;
            public bool Querida, Falhou;
        }

        readonly List<ObraNaCena> _obras = new List<ObraNaCena>();
        Transform _paiDasObras;
        float _proximaObra;

        /// <summary>Quantas obras de verdade estão na cena (para o F1).</summary>
        public int ObrasPerto { get; private set; }

        // as caixas de longe de todas as obras e os caminhos de cada cidade (uma vez, no Awake)
        void MontarAsObras(Transform pai)
        {
            _paiDasObras = pai;
            var caminhos = new Dictionary<string, MalhaDeObra>();
            foreach (var l in Chao.Lotes)
            {
                if (l.Plano == null) continue;
                // o hospital é o do mundo sorteado (o HospitalDoMundo), no lugar do lote
                if (l.Plano.Uso == UsoDoLote.Hospital)
                {
                    FixarOHospital(l);
                    continue;
                }
                var longe = new MalhaDeObra();
                ObrasDeLakewood.CaixaDeLonge(l.Plano, longe);
                // com o material das casas (o abandono), como a obra de verdade: a troca não muda a cor
                var go = Objeto($"{l.Lote.cidade}: {l.Plano.Nome} (de longe)", longe.ParaMesh($"Obra de longe {l.Indice}"), materialDasCasas != null ? materialDasCasas : materialDaCidade, pai);
                go.transform.SetPositionAndRotation(l.NoMundo(0f, 0f), l.Rotacao);
                go.isStatic = false;
                _obras.Add(new ObraNaCena { Lote = l, Plano = l.Plano, Longe = go.GetComponent<MeshRenderer>() });
                if (!caminhos.TryGetValue(l.Lote.cidade, out var m)) caminhos[l.Lote.cidade] = m = new MalhaDeObra();
                if (l.Trilha != null) PedrasDaTrilha(l, m);
                else if (l.Plano.Entrada.HasValue) Caminho(l, l.Plano.Entrada.Value, 1.2f, m);
                if (l.Plano.EntradaDoCarro.HasValue) Caminho(l, l.Plano.EntradaDoCarro.Value, 3f, m);
            }
            foreach (var kv in caminhos)
                if (kv.Value.Triangulos > 0) Objeto($"{kv.Key}: os caminhos", kv.Value.ParaMesh($"{kv.Key} (caminhos)"), materialDaCidade, pai);
        }

        // o Hospital Principal no lote da cidade (Plano_Lakewood, Etapa 6, a parte 4): o HospitalDoMundo da cena, com o
        // meio do corpo no patamar (o embasamento por cima, como no mundo sorteado), o nome do condado na torre e os papéis
        // que citam o condado
        void FixarOHospital(ChaoDeLakewood.LoteNoChao l)
        {
            var h = HospitalDoMundo.Instancia != null ? HospitalDoMundo.Instancia : FindAnyObjectByType<HospitalDoMundo>();
            if (h == null)
            {
                Debug.LogWarning("[Lakewood] A cena não tem o HospitalDoMundo (Lakewood > Etapa 6: as cidades na cena).");
                return;
            }
            h.nomeNaTorre = new[] { "LAKEWOOD", "COUNTY", "HOSPITAL" };
            h.arquivoDosPapeis = "Documentos/Hospital_Lakewood";
            var meio = l.Plano.Meio;
            h.Fixar(l.NoMundo(meio.x, meio.y, 0.05f + PlantaDoHospital.Embasamento), l.Lote.giro, mapa.semente);
            Debug.Log($"[Lakewood] O hospital ({l.Lote.cidade}): em ({l.Lote.meio.x:F0}; {l.Lote.meio.y:F0}), de frente para a {l.Lote.rua}.");
        }

        // uma faixa de concreto de um ponto do lote (a entrada do carro, a porta das lojas) até a beira do asfalto da rua da
        // frente, 3 cm acima da malha do terreno (pela altura exata, ela afundava entre os vértices no talude dos lotes)
        void Caminho(ChaoDeLakewood.LoteNoChao l, Vector2 de, float largura, MalhaDeObra m)
        {
            var a = l.Lote.NoMapa(de.x, de.y);
            var dir = l.Lote.Frente;
            var lado = l.Lote.Lado * (largura * 0.5f);
            // até onde: o ponto da frente em que o asfalto começa (no máximo 30 m)
            float comp = 0f;
            for (float s = 0f; s < 30f; s += 0.25f)
            {
                var p = a + dir * s;
                comp = s;
                if (Chao.EstradaPerto(p.x, p.y, 0.3f, out float dist, out _, out _, out float meia) && dist <= meia + 0.15f) break;
            }
            if (comp < 0.5f) return;
            int n = Mathf.CeilToInt(comp / 0.5f);
            Vector3 P(Vector2 q) => new Vector3(q.x, Chao.AlturaDaMalha(q.x, q.y) + 0.03f, q.y);
            for (int k = 0; k < n; k++)
            {
                Vector2 c0 = a + dir * (comp * k / n), c1 = a + dir * (comp * (k + 1) / n);
                m.Face(new[] { P(c0 - lado), P(c1 - lado), P(c1 + lado), P(c0 + lado) }, Vector3.up, "Calcada");
            }
        }

        // as pedras da trilha da porta até a rua (12/11/2026, a pedido: "um trilho em vez de ser reto"): lajes chatas e
        // tortas a cada 0,7 m da curva, um pouco para cada lado, por cima da terra batida que o chão pinta (o
        // ChaoDeLakewood.Lotes.cs), cada uma 3 cm acima da malha do terreno no ponto mais alto dela
        void PedrasDaTrilha(ChaoDeLakewood.LoteNoChao l, MalhaDeObra m)
        {
            var tr = l.Trilha;
            var sorte = new System.Random(unchecked(l.Lote.semente * 17 + 3));
            float Entre(float a, float b) => a + (float)sorte.NextDouble() * (b - a);
            string[] cores = { "Pedra", "Pedra", "PedraEscura", "Concreto" };
            float andado = 0f, proxima = 0.35f;
            var A = new List<Vector3>(7);
            var B = new List<Vector3>(7);
            int lado = 1;
            for (int k = 0; k + 1 < tr.Count; k++)
            {
                Vector2 a = tr[k], b = tr[k + 1];
                float seg = Vector2.Distance(a, b);
                while (proxima <= andado + seg && seg > 1e-4f)
                {
                    float u = (proxima - andado) / seg;
                    var c = Vector2.Lerp(a, b, u);
                    var dir = (b - a) / seg;
                    var nrm = new Vector2(-dir.y, dir.x);
                    c += nrm * (lado * Entre(0.05f, 0.18f));
                    lado = -lado;
                    proxima += Entre(0.62f, 0.78f);
                    if (sorte.NextDouble() < 0.06) continue;   // uma que afundou ou levaram
                    // a laje: um polígono de 6 lados torto, de 0,42 a 0,6 m, girado pela trilha e mais um pouco
                    float r1 = Entre(0.2f, 0.3f), r2 = Entre(0.16f, 0.22f), giro = Mathf.Atan2(dir.y, dir.x) + Entre(-0.35f, 0.35f);
                    A.Clear();
                    B.Clear();
                    float alto = float.MinValue;
                    var pts = new Vector2[6];
                    for (int q = 0; q < 6; q++)
                    {
                        float ang = q * Mathf.PI / 3f + Entre(-0.2f, 0.2f), raio = Entre(0.85f, 1.1f);
                        var local = new Vector2(Mathf.Cos(ang) * r1 * raio, Mathf.Sin(ang) * r2 * raio);
                        pts[q] = c + new Vector2(local.x * Mathf.Cos(giro) - local.y * Mathf.Sin(giro), local.x * Mathf.Sin(giro) + local.y * Mathf.Cos(giro));
                        alto = Mathf.Max(alto, Chao.AlturaDaMalha(pts[q].x, pts[q].y));
                    }
                    alto = Mathf.Max(alto, Chao.AlturaDaMalha(c.x, c.y)) + 0.03f;
                    foreach (var q in pts)
                    {
                        A.Add(new Vector3(q.x, alto - 0.06f, q.y));
                        B.Add(new Vector3(q.x, alto, q.y));
                    }
                    m.Solido(A, B, cores[sorte.Next(cores.Length)]);
                }
                andado += seg;
            }
        }

        // quem está perto: a câmera e os jogadores (a colisão deles precisa das obras)
        readonly List<Vector3> _centros = new List<Vector3>();

        void RevisarAsObras()
        {
            if (_obras.Count == 0) return;
            _centros.Clear();
            var cam = Camera.main;
            if (cam != null) _centros.Add(cam.transform.position);
            foreach (var j in JogadorRede.Todos) if (j != null) _centros.Add(j.transform.position);
            float raio = _semNeblina ? Mathf.Max(raioDasObras, raioDasObrasSemNeblina) : raioDasObras;
            float r2 = raio * raio;
            int montando = 0;
            foreach (var o in _obras) if (o.Montando != null) montando++;
            int perto = 0;
            foreach (var o in _obras)
            {
                var meio = o.Lote.Lote.meio;
                bool quer = false;
                foreach (var c in _centros)
                {
                    float dx = c.x - meio.x, dz = c.z - meio.y;
                    if (dx * dx + dz * dz < r2) { quer = true; break; }
                }
                if (quer) o.QueridaEm = Time.time;
                o.Querida = quer || Time.time - o.QueridaEm < 3f;
                if (o.Querida && !o.Falhou && o.Perto == null && o.Montando == null && o.Pronta == null && montando < obrasAoMesmoTempo)
                {
                    var plano = o.Plano;
                    o.Montando = Task.Run(() => ObrasDeLakewood.Montar(plano));
                    montando++;
                }
                if (!o.Querida && o.Perto != null) TirarAObra(o);
                if (o.Perto != null) perto++;
            }
            ObrasPerto = perto;
        }

        // a cada quadro: no máximo uma obra pronta vira objeto, e as colisões cozidas entram
        void ReceberAsObras()
        {
            if (Time.time >= _proximaObra)
            {
                _proximaObra = Time.time + 0.25f;
                RevisarAsObras();
            }
            bool fez = false;
            foreach (var o in _obras)
            {
                for (int k = o.Cozinhando.Count - 1; k >= 0; k--)
                {
                    var (c, m, t) = o.Cozinhando[k];
                    if (!t.IsCompleted) continue;
                    if (c != null) { c.sharedMesh = m; c.enabled = true; }
                    o.Cozinhando.RemoveAt(k);
                }
                if (o.Montando != null && o.Montando.IsCompleted)
                {
                    if (o.Montando.IsFaulted)
                    {
                        Debug.LogWarning($"[Lakewood] A obra {o.Plano.Nome} do lote {o.Lote.Indice} não montou: {o.Montando.Exception?.GetBaseException().Message}");
                        o.Montando = null;
                        o.Falhou = true;   // não tenta de novo nesta partida
                        continue;
                    }
                    o.Pronta = o.Montando.Result;
                    o.Montando = null;
                }
                // a pronta espera a vez dela (uma por quadro)
                if (o.Pronta == null) continue;
                if (!o.Querida) { Devolver(o.Pronta); o.Pronta = null; continue; }
                if (fez) continue;
                PorAObra(o);
                fez = true;
            }
        }

        static void Devolver(ObraPronta p)
        {
            if (p == null) return;
            if (p.Hera != null) MalhaDaHera.Devolver(p.Hera);
            if (p.Pichacoes != null) MalhaDePichacao.Devolver(p.Pichacoes);
            p.Hera = null;
            p.Pichacoes = null;
            foreach (var m in p.Marcas) if (m != null) MalhaDePichacao.Devolver(m);
            foreach (var m in p.MaisPichacoes) if (m != null) MalhaDePichacao.Devolver(m);
            p.Marcas.Clear();
            p.MaisPichacoes.Clear();
        }

        // a obra pronta na cena (como o PorCasas do mundo sorteado)
        void PorAObra(ObraNaCena o)
        {
            var p = o.Pronta;
            var c = p.Casa;
            var l = o.Lote;
            var go = new GameObject($"{l.Lote.cidade}: {c.Nome} (lote {l.Indice})");
            go.transform.SetParent(_paiDasObras, false);
            // a loja de armas tem o chão de fora 15 cm abaixo da porta (a origem dela)
            float acima = o.Plano.Uso == UsoDoLote.LojaDeArmas ? 0.03f - PlantaDaLojaDeArmas.ChaoDeFora : 0f;
            go.transform.SetPositionAndRotation(l.NoMundo(o.Plano.Meio.x, o.Plano.Meio.y, acima), Quaternion.Euler(0f, l.Lote.giro + o.Plano.GiroNoLote, 0f));
            o.Perto = go;
            var desenho = p.Desenho.ParaMesh(c.Nome);
            o.Malhas.Add(desenho);
            go.AddComponent<MeshFilter>().sharedMesh = desenho;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = materialDasCasas != null ? materialDasCasas : materialDaCidade;
            if (p.Hera != null && p.Hera.Folhas > 0 && materialDaHera != null)
            {
                var h = new GameObject("Hera");
                h.transform.SetParent(go.transform, false);
                var mh = p.Hera.ParaMesh($"{c.Nome} (hera)");
                o.Malhas.Add(mh);
                h.AddComponent<MeshFilter>().sharedMesh = mh;
                var rh = h.AddComponent<MeshRenderer>();
                rh.sharedMaterial = materialDaHera;
                rh.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (p.Pichacoes != null && p.Pichacoes.Quadros > 0 && materialDasPichacoes != null)
            {
                var pi = new GameObject("Pichações");
                pi.transform.SetParent(go.transform, false);
                var mp = p.Pichacoes.ParaMesh($"{c.Nome} (pichações)");
                o.Malhas.Add(mp);
                pi.AddComponent<MeshFilter>().sharedMesh = mp;
                var rp = pi.AddComponent<MeshRenderer>();
                rp.sharedMaterial = materialDasPichacoes;
                rp.shadowCastingMode = ShadowCastingMode.Off;
            }
            // as marcas e as pichações das partes da loja de armas
            for (int k = 0; k < p.Marcas.Count; k++) Junto(go, o, p.Marcas[k], materialDasMarcas, $"{c.Nome} (marcas {k})");
            for (int k = 0; k < p.MaisPichacoes.Count; k++) Junto(go, o, p.MaisPichacoes[k], materialDasPichacoes, $"{c.Nome} (pichações {k})");
            // os móveis da loja de armas (os prefabs da mobília nova, sem a colisão do corpo: a dela está na da obra)
            if (p.MoveisDaLoja.Count > 0) MoveisDaLojaDeArmasNaCena(go, p);
            Devolver(p);
            // os móveis: os antigos (as lojas, o posto) e a mobília nova (as casas; com o saque), e as portas que abrem
            MoveisDaConstrucao.Por(go, c.Moveis);
            if (MobiliaNovaDoMundo.Instancia != null && c.MobiliaNova != null) MobiliaDaConstrucao.Por(go, c.Moveis, c.MobiliaNova, c.Semente, -100);
            c.MobiliaNova = null;
            if (PortasDoMundo.Instancia != null) PortasDaConstrucao.Por(go, c.Portas, c.Janelas, c.Semente);
            // a colisão cozida em outra thread
            var col = c.Colisao.ParaMesh($"{c.Nome} (colisão)");
            o.Malhas.Add(col);
            var colisor = go.AddComponent<MeshCollider>();
            colisor.enabled = false;
            var id = col.GetEntityId();
            o.Cozinhando.Add((colisor, col, Task.Run(() => Physics.BakeMesh(id, false))));
            foreach (var cx in p.Caixas)
            {
                var b = new GameObject("Colisão");
                b.transform.SetParent(go.transform, false);
                b.transform.SetLocalPositionAndRotation(cx.Centro, Quaternion.Euler(0f, cx.GiroY, 0f));
                b.AddComponent<BoxCollider>().size = cx.Tamanho;
            }
            // os pisos (os passos), a caixa de dentro (a neblina de dentro) e o eco
            var pisos = go.AddComponent<PisoPorZona>();
            pisos.zonas = new Bounds[c.Zonas.Count];
            pisos.superficies = new Superficie[c.Zonas.Count];
            for (int i = 0; i < c.Zonas.Count; i++)
            {
                pisos.zonas[i] = new Bounds(c.Zonas[i].Centro, c.Zonas[i].Tamanho);
                pisos.superficies[i] = TerrenoDoMundo.SomDoPiso(c.Zonas[i].Piso);
            }
            pisos.resto = Superficie.Concreto;
            if (c.TamanhoDeDentro.sqrMagnitude > 0.01f)
            {
                var dentro = new GameObject("Dentro");
                dentro.transform.SetParent(go.transform, false);
                dentro.transform.localPosition = c.CentroDeDentro;
                dentro.AddComponent<AreaInterna>().tamanho = c.TamanhoDeDentro;
                EcoSoDentro.Criar(go.transform, new Bounds(c.CentroDeDentro, c.TamanhoDeDentro));
            }
            // os carros parados na garagem ou na vaga (sete em cada dez casas)
            if (carrosParados != null && carrosParados.Length > 0 && p.Carros.Count > 0)
            {
                var rnd = new System.Random(c.Semente * 31 + 7);
                var material = materialDaUsina != null ? materialDaUsina : materialDaCidade;
                foreach (var (pos, yaw) in p.Carros)
                {
                    if (rnd.NextDouble() > 0.7) continue;
                    var malha = carrosParados[rnd.Next(carrosParados.Length)];
                    if (malha == null) continue;
                    var carro = new GameObject("Carro parado");
                    carro.transform.SetParent(go.transform, false);
                    carro.transform.SetLocalPositionAndRotation(new Vector3(pos.x, 0.05f, pos.y), Quaternion.Euler(0f, yaw, 0f));
                    carro.AddComponent<MeshFilter>().sharedMesh = malha;
                    carro.AddComponent<MeshRenderer>().sharedMaterial = material;
                    var caixa = carro.AddComponent<BoxCollider>();
                    caixa.center = malha.bounds.center;
                    caixa.size = malha.bounds.size;
                }
            }
            o.Pronta = null;
            if (o.Longe != null) o.Longe.enabled = false;
        }

        void Junto(GameObject go, ObraNaCena o, MalhaDePichacao m, Material material, string nome)
        {
            if (m == null || m.Quadros == 0 || material == null) return;
            var j = new GameObject(nome);
            j.transform.SetParent(go.transform, false);
            var malha = m.ParaMesh(nome);
            o.Malhas.Add(malha);
            j.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = j.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        Dictionary<string, GameObject> _prefabsDaMobilia;

        void MoveisDaLojaDeArmasNaCena(GameObject go, ObraPronta p)
        {
            if (_prefabsDaMobilia == null)
            {
                _prefabsDaMobilia = new Dictionary<string, GameObject>();
                var nova = MobiliaNovaDoMundo.Instancia;
                if (nova != null && nova.prefabs != null)
                    foreach (var pf in nova.prefabs)
                        if (pf != null) _prefabsDaMobilia[pf.name] = pf;
            }
            var pai = new GameObject("Móveis").transform;
            pai.SetParent(go.transform, false);
            foreach (var m in p.MoveisDaLoja)
            {
                if (!_prefabsDaMobilia.TryGetValue(m.modelo, out var pf)) continue;
                var o = Instantiate(pf, pai);
                o.name = m.modelo;
                o.transform.SetLocalPositionAndRotation(m.lugar, Quaternion.Euler(0f, m.giro, 0f));
                o.transform.localScale = Vector3.one * m.escala;
                var corpo = o.GetComponent<MeshCollider>();
                if (corpo != null) Destroy(corpo);
                if (m.porta >= 0) PortaDoCofre(o, m.porta);
            }
        }

        // a porta de cofre: aberta a 85° (arrombada), entreaberta a 35° ou fechada e trancada (sem a parte que abre)
        static void PortaDoCofre(GameObject o, int estado)
        {
            var parte = o.GetComponentInChildren<ParteQueAbre>(true);
            if (parte == null) return;
            parte.rotulo = "a porta de cofre";
            parte.duracao = 1.4f;
            if (estado == 2)
            {
                parte.Pousar(false);
                Destroy(parte);
                return;
            }
            parte.quanto = Mathf.Sign(parte.quanto) * (estado == 0 ? 85f : 35f);
            parte.Pousar(true);
        }

        void TirarAObra(ObraNaCena o)
        {
            if (o.Perto != null) Destroy(o.Perto);
            o.Perto = null;
            foreach (var m in o.Malhas) if (m != null) Destroy(m);
            o.Malhas.Clear();
            o.Cozinhando.Clear();
            if (o.Longe != null) o.Longe.enabled = true;
        }
    }
}
