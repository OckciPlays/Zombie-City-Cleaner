using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// A toca dos castores (no save do mundo): o lugar, o tamanho que os galhos deram e quando foi feita. A do riacho
    /// (13/11/2026) tem a represa: o meio dela (rx, ry, rz; y é a água), o giro da correnteza, a largura do rio e o quanto
    /// os galhos já fizeram (0 a 1). Os saves antigos vêm sem ela (noRio falso).
    /// </summary>
    [System.Serializable]
    public struct TocaDoCastor
    {
        public float x, z, tamanho;
        public double criadaNaHora;
        public bool noRio;
        public float rx, ry, rz, giro, largura, represa;
    }

    /// <summary>Uma árvore que o castor derrubou (no save do mundo): o toco roído em ponta de lápis.</summary>
    [System.Serializable]
    public struct ArvoreRoida
    {
        public long chave;
        public float x, y, z, raio;
    }

    /// <summary>
    /// Os castores no gerenciador (a etapa 3, 12/11/2026): as tocas (no máximo 2 no lago, no raso perto da margem; crescem
    /// com os dias e com os galhos), as árvores roídas (o tempo de roer, somado entre os castores: ~90 s; ela cai pelo
    /// sistema do machado, MundoDaPartida.DerrubarPeloCastor, e fica o toco em ponta de lápis), o save e a mensagem
    /// "ZCC.FaunaCasas" (a cada 3 s, a quem está perto: as tocas, os tocos roídos e os ninhos dos gansos; as casas são
    /// desenhadas em cada máquina: CasasDosBichos).
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        const string NomeDasCasas = "ZCC.FaunaCasas";
        const int MaxTocas = 2;
        /// <summary>Os segundos de roer (somando os castores) até a árvore cair.</summary>
        public const float TempoParaRoer = 90f;
        /// <summary>O F1: os castores roem depressa (para ver a árvore cair).</summary>
        public static bool CastorRapido;

        /// <summary>O save carregou antes da fauna existir (ou a fauna foi recriada).</summary>
        public static List<TocaDoCastor> TocasGuardadas;
        public static List<ArvoreRoida> RoidasGuardadas;

        readonly List<TocaDoCastor> _tocas = new List<TocaDoCastor>();
        readonly List<ArvoreRoida> _roidas = new List<ArvoreRoida>();
        readonly Dictionary<long, float> _roendo = new Dictionary<long, float>();
        float _proximasCasas;
        CasasDosBichos _casas;

        CasasDosBichos Casas
        {
            get
            {
                if (_casas == null)
                {
                    var go = new GameObject("Casas dos bichos");
                    go.transform.SetParent(transform, false);
                    _casas = go.AddComponent<CasasDosBichos>();
                }
                return _casas;
            }
        }

        // ------------------------------------------------------------------ o save

        /// <summary>As tocas e os tocos roídos (o save do mundo).</summary>
        public static void CastoresParaSalvar(out List<TocaDoCastor> tocas, out List<ArvoreRoida> roidas)
        {
            var f = Instancia;
            if (f == null || !f._servidor)
            {
                tocas = TocasGuardadas != null ? new List<TocaDoCastor>(TocasGuardadas) : new List<TocaDoCastor>();
                roidas = RoidasGuardadas != null ? new List<ArvoreRoida>(RoidasGuardadas) : new List<ArvoreRoida>();
                return;
            }
            tocas = new List<TocaDoCastor>(f._tocas);
            roidas = new List<ArvoreRoida>(f._roidas);
        }

        /// <summary>O save carregou: as tocas e os tocos voltam (agora, ou quando a fauna nascer).</summary>
        public static void RestaurarCastores(List<TocaDoCastor> tocas, List<ArvoreRoida> roidas)
        {
            TocasGuardadas = tocas != null ? new List<TocaDoCastor>(tocas) : null;
            RoidasGuardadas = roidas != null ? new List<ArvoreRoida>(roidas) : null;
            if (Instancia != null && Instancia._servidor) Instancia.RestaurarOsCastores();
        }

        void RestaurarOsCastores()
        {
            _tocas.Clear();
            _roidas.Clear();
            if (TocasGuardadas != null) _tocas.AddRange(TocasGuardadas);
            if (RoidasGuardadas != null) _roidas.AddRange(RoidasGuardadas);
        }

        // ------------------------------------------------------------------ as tocas

        /// <summary>O tamanho da toca agora (0 a 1): o dos galhos, mais um pouco a cada dia.</summary>
        static float TamanhoDaToca(TocaDoCastor t) => Mathf.Clamp01(t.tamanho + (float)(HorasDoJogo - t.criadaNaHora) / 24f * 0.05f);

        /// <summary>
        /// Onde nasce a família de castores, em volta do jogador: a toca mais perto (de 'de' a 'ate' m) ou uma nova (no raso,
        /// de 0,35 a 0,9 m, se o lago ainda tem lugar para tocas).
        /// </summary>
        internal bool TocaParaNascer(Vector3 pes, float de, float ate, out Vector3 p)
        {
            p = default;
            foreach (var t in _tocas)
            {
                var c = new Vector3(t.x, NivelEm(t.x, t.z), t.z);
                float d = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(pes.x, pes.z));
                if (d >= de && d <= ate) { p = c; return true; }
            }
            // (13/11) no Lakewood, os riachos primeiro: a toca com a represa (FaunaDoMundo.Rios.cs)
            if (TemRios && Random.value < 0.75f && TocaNoRio(pes, de, ate, out p)) return true;
            if (_tocas.Count - CountNoRio() >= MaxTocas) return false;
            if (!PontoNaAgua(pes, de, ate, 0.35f, 0.9f, out p)) return false;
            // longe da outra toca
            foreach (var t in _tocas) if (Vector2.Distance(new Vector2(t.x, t.z), new Vector2(p.x, p.z)) < 60f) return false;
            _tocas.Add(new TocaDoCastor { x = p.x, z = p.z, tamanho = 0.35f, criadaNaHora = HorasDoJogo });
            return true;
        }

        /// <summary>O galho chegou à toca mais perto do ponto: ela cresce.</summary>
        internal void CrescerToca(Vector3 perto, float quanto)
        {
            int melhor = -1;
            float m = 12f;
            for (int i = 0; i < _tocas.Count; i++)
            {
                float d = Vector2.Distance(new Vector2(_tocas[i].x, _tocas[i].z), new Vector2(perto.x, perto.z));
                if (d < m) { m = d; melhor = i; }
            }
            if (melhor < 0) return;
            var t = _tocas[melhor];
            t.tamanho = Mathf.Clamp01(t.tamanho + quanto);
            _tocas[melhor] = t;
        }

        // ------------------------------------------------------------------ as árvores roídas

        readonly List<PlantaDaFloresta> _plantas = new List<PlantaDaFloresta>(256);

        /// <summary>A árvore em pé mais perto da toca, na beira (a até ~12 m da água), a até 35 m: a chave, o pé e o raio do tronco.</summary>
        internal bool ArvoreParaRoer(Vector3 toca, out long chave, out Vector3 pe, out float raio)
        {
            chave = 0;
            pe = default;
            raio = 0f;
            var d = DadosDoMapa;
            // o Lakewood (a parte 2 do "O jogo do mundo sorteado no Lakewood"): a beira é a terra a até 12 m da água
            var chao = ChaoDoLakewood;
            if (d == null && chao != null)
                return ArvoresDoMundo.MaisPerto(toca, 35f, out chave, out pe, out raio, q => !chao.Agua(q.x, q.z, out _) && chao.Agua(q.x, q.z, out _, 12f));
            if (d?.Floresta == null || d.Lago == null) return false;
            var a = d.BlocoDe(toca.x - 35f, toca.z - 35f);
            var b = d.BlocoDe(toca.x + 35f, toca.z + 35f);
            float melhor = float.MaxValue;
            for (int bz = a.y; bz <= b.y; bz++)
                for (int bx = a.x; bx <= b.x; bx++)
                {
                    d.Floresta.DoBloco(bx, bz, _plantas);
                    for (int i = 0; i < _plantas.Count; i++)
                    {
                        var pl = _plantas[i];
                        if (!Floresta.EhArvore(pl.Especie) || Floresta.Deitada(pl.Especie) || Floresta.RaioDoTronco[(int)pl.Especie] <= 0f) continue;
                        float dist = Vector2.Distance(new Vector2(pl.Posicao.x, pl.Posicao.z), new Vector2(toca.x, toca.z));
                        if (dist > 35f || dist >= melhor) continue;
                        float q = d.Lago.Q(pl.Posicao.x, pl.Posicao.z);
                        if (q < 1f || q > 1.25f) continue;   // na beira
                        long k = FlorestaDoMundo.Chave(bx, bz, i);
                        if (FlorestaDoMundo.Cortada(k)) continue;
                        melhor = dist;
                        chave = k;
                        pe = pl.Posicao;
                        raio = Floresta.RaioDoTronco[(int)pl.Especie] * pl.Escala;
                    }
                }
            return melhor < float.MaxValue;
        }

        /// <summary>Um castor roeu 'dt' s a árvore: verdadeiro quando ela cai (o toco roído fica).</summary>
        internal bool Roer(long chave, Vector3 pe, float dt, Vector3 deOnde)
        {
            if (FlorestaDoMundo.Cortada(chave)) return true;
            _roendo.TryGetValue(chave, out float t);
            t += dt;
            _roendo[chave] = t;
            if (t < (CastorRapido ? 8f : TempoParaRoer)) return false;
            _roendo.Remove(chave);
            if (!ArvoresDoMundo.DerrubarPeloCastor(chave, deOnde)) return true;
            float raio = 0.25f;
            if (ArvoresDoMundo.Planta(chave, out var planta)) raio = ArvoresDoMundo.RaioDoTronco(planta);
            _roidas.Add(new ArvoreRoida { chave = chave, x = pe.x, y = pe.y, z = pe.z, raio = raio });
            if (_roidas.Count > 200) _roidas.RemoveAt(0);
            return true;
        }

        // ------------------------------------------------------------------ as casas na rede e na tela

        struct Casa
        {
            public byte tipo;   // 0 a toca, 1 o toco roído, 2 o ninho, 3 o lixo espalhado, 4 a represa (13/11)
            public Vector3 pos;
            public float tamanho;   // (no lixo: de onde ele veio, a lata, as sacolas ou a caçamba)
            public float giro, largura;   // (a represa: o giro da correnteza e a largura do rio)
        }

        readonly List<Casa> _casasPerto = new List<Casa>();

        // no anfitrião, a cada 3 s: as casas perto de cada cliente vão pela rede; as dele, para a tela
        void AtualizarCasas()
        {
            if (Time.unscaledTime < _proximasCasas) return;
            _proximasCasas = Time.unscaledTime + 3f;
            var nm = NetworkManager.Singleton;
            foreach (var g in Pessoas)
            {
                _casasPerto.Clear();
                JuntarCasas(g.pes, 320f, _casasPerto);
                if (g.j.OwnerClientId == NetworkManager.ServerClientId || nm == null || !nm.IsListening)
                {
                    if (g.j.IsOwner) Casas.Mostrar(_casasPerto);
                    continue;
                }
                using var w = new FastBufferWriter(2 + _casasPerto.Count * 25, Allocator.Temp, 2 + _casasPerto.Count * 25 + 256);
                w.WriteValueSafe((ushort)_casasPerto.Count);
                foreach (var c in _casasPerto)
                {
                    w.WriteValueSafe(c.tipo);
                    w.WriteValueSafe(c.pos);
                    w.WriteValueSafe(c.tamanho);
                    w.WriteValueSafe(c.giro);
                    w.WriteValueSafe(c.largura);
                }
                nm.CustomMessagingManager.SendNamedMessage(NomeDasCasas, g.j.OwnerClientId, w, NetworkDelivery.Unreliable);
            }
        }

        void JuntarCasas(Vector3 perto, float raio, List<Casa> saida)
        {
            float r2 = raio * raio;
            foreach (var t in _tocas)
            {
                var p = new Vector3(t.x, NivelEm(t.x, t.z), t.z);
                if ((p - perto).sqrMagnitude < r2 && saida.Count < 60) saida.Add(new Casa { tipo = 0, pos = p, tamanho = TamanhoDaToca(t) });
                // (13/11) a represa do riacho
                var rp = new Vector3(t.rx, t.ry, t.rz);
                if (t.noRio && (rp - perto).sqrMagnitude < r2 && saida.Count < 60)
                    saida.Add(new Casa { tipo = 4, pos = rp, tamanho = TamanhoDaRepresa(t), giro = t.giro, largura = t.largura });
            }
            foreach (var r in _roidas)
            {
                var p = new Vector3(r.x, r.y, r.z);
                if ((p - perto).sqrMagnitude < r2 && saida.Count < 60) saida.Add(new Casa { tipo = 1, pos = p, tamanho = r.raio });
            }
            foreach (var g in Grupos)
            {
                if (!g.temNinho || (g.ninho - perto).sqrMagnitude > r2 || saida.Count >= 60) continue;
                saida.Add(new Casa { tipo = 2, pos = g.ninho, tamanho = 1f });
            }
            // (a etapa 4) o lixo que o guaxinim espalhou
            float agora = Time.time;
            foreach (var l in _lixos)
            {
                if (l.ate < agora || (l.pos - perto).sqrMagnitude > r2 || saida.Count >= 60) continue;
                // (23/11/2026) a idade (s) vai na largura: o lixo novo mostra a lata tombando
                saida.Add(new Casa { tipo = 3, pos = l.pos, tamanho = l.tipo, largura = LixoDura - (l.ate - agora) });
            }
        }

        void AoReceberCasas(ulong remetente, FastBufferReader r)
        {
            r.ReadValueSafe(out ushort n);
            _casasPerto.Clear();
            for (int i = 0; i < n && i < 60; i++)
            {
                r.ReadValueSafe(out byte tipo);
                r.ReadValueSafe(out Vector3 pos);
                r.ReadValueSafe(out float tamanho);
                r.ReadValueSafe(out float giro);
                r.ReadValueSafe(out float largura);
                _casasPerto.Add(new Casa { tipo = tipo, pos = pos, tamanho = tamanho, giro = giro, largura = largura });
            }
            Casas.Mostrar(_casasPerto);
        }

        /// <summary>
        /// As casas dos bichos desenhadas nesta máquina (só perto, refeitas quando mudam): a toca (um monte de galhos no raso,
        /// com o colisor), o toco roído em ponta de lápis e o ninho dos gansos (os galhos em roda e os ovos).
        /// </summary>
        sealed class CasasDosBichos : MonoBehaviour
        {
            readonly Dictionary<long, (GameObject go, float tamanho)> _feitas = new Dictionary<long, (GameObject, float)>();
            readonly HashSet<long> _vistas = new HashSet<long>();

            static long Chave(Casa c) => ((long)c.tipo << 56) ^ ((long)Mathf.RoundToInt(c.pos.x * 2f) << 28) ^ (long)(uint)Mathf.RoundToInt(c.pos.z * 2f);

            /// <summary>Onde há lixo espalhado na tela agora (as lixeiras e as sacolas de LixoDaCidade somem ali).</summary>
            public static readonly List<Vector3> LixoNaTela = new List<Vector3>();

            public void Mostrar(List<Casa> casas)
            {
                _vistas.Clear();
                LixoNaTela.Clear();
                foreach (var c in casas) if (c.tipo == 3) LixoNaTela.Add(c.pos);
                foreach (var c in casas)
                {
                    long k = Chave(c);
                    _vistas.Add(k);
                    if (_feitas.TryGetValue(k, out var f) && f.go != null && Mathf.Abs(f.tamanho - c.tamanho) < 0.04f) continue;
                    if (f.go != null) Destroy(f.go);
                    var go = c.tipo == 0 ? Toca(c.pos, c.tamanho) : c.tipo == 1 ? TocoRoido(c.pos, c.tamanho) : c.tipo == 2 ? Ninho(c.pos)
                           : c.tipo == 4 ? Represa(c.pos, c.tamanho, c.giro, c.largura) : Lixo(c.pos, Mathf.RoundToInt(c.tamanho), c.largura);
                    go.transform.SetParent(transform, true);
                    _feitas[k] = (go, c.tamanho);
                }
                var tirar = new List<long>();
                foreach (var kv in _feitas) if (!_vistas.Contains(kv.Key)) tirar.Add(kv.Key);
                foreach (var k in tirar)
                {
                    if (_feitas[k].go != null) Destroy(_feitas[k].go);
                    _feitas.Remove(k);
                }
                LixoDaCidade.ConferirAgora();   // (23/11/2026: a lata de pé some junto com a que começa a tombar)
            }

            static readonly Color Galho = new Color(0.36f, 0.27f, 0.18f), GalhoClaro = new Color(0.52f, 0.41f, 0.28f), Barro = new Color(0.25f, 0.2f, 0.15f);

            // a toca: um monte de galhos (de 1,6 a 4,4 m), metade fora da água
            static GameObject Toca(Vector3 p, float tamanho)
            {
                var go = new GameObject("Toca dos castores");
                go.transform.position = p;
                var r = new System.Random(Mathf.RoundToInt(p.x * 13f + p.z * 7f));
                float raio = 0.8f + 1.4f * tamanho, alto = raio * 0.6f;
                ModelosDosBichos.CaixaSolta(go.transform, new Vector3(0f, alto * 0.3f - 0.3f, 0f), new Vector3(raio * 1.6f, alto * 1.1f, raio * 1.6f), Barro);
                int n = 26 + Mathf.RoundToInt(30 * tamanho);
                for (int i = 0; i < n; i++)
                {
                    float ang = (float)r.NextDouble() * 360f, alt = (float)r.NextDouble();
                    float rr = raio * Mathf.Sqrt(1f - alt * alt) * (0.75f + 0.25f * (float)r.NextDouble());
                    var pos = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * rr + Vector3.up * (alt * alto - 0.25f);
                    float comp = 0.8f + (float)r.NextDouble() * 1.4f;
                    var giro = new Vector3((float)r.NextDouble() * 60f - 30f, ang + 90f + (float)r.NextDouble() * 60f - 30f, (float)r.NextDouble() * 40f - 20f);
                    ModelosDosBichos.CaixaSolta(go.transform, pos, new Vector3(0.06f, 0.06f, comp), r.NextDouble() < 0.5 ? Galho : GalhoClaro, giro);
                }
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, alto * 0.3f - 0.2f, 0f);
                col.size = new Vector3(raio * 1.5f, alto + 0.4f, raio * 1.5f);
                return go;
            }

            // o toco roído: a base com a casca e a ponta de lápis de madeira clara
            static GameObject TocoRoido(Vector3 p, float raio)
            {
                var go = new GameObject("Toco roído");
                go.transform.position = p;
                float r = Mathf.Max(0.12f, raio);
                var casca = new Color(0.3f, 0.23f, 0.17f);
                var madeira = new Color(0.82f, 0.7f, 0.5f);
                ModelosDosBichos.CaixaSolta(go.transform, new Vector3(0f, 0.15f, 0f), new Vector3(r * 1.9f, 0.3f, r * 1.9f), casca);
                ModelosDosBichos.CaixaSolta(go.transform, new Vector3(0f, 0.36f, 0f), new Vector3(r * 1.4f, 0.12f, r * 1.4f), madeira, new Vector3(0f, 25f, 0f));
                ModelosDosBichos.CaixaSolta(go.transform, new Vector3(0f, 0.47f, 0f), new Vector3(r * 0.9f, 0.1f, r * 0.9f), madeira, new Vector3(0f, 50f, 0f));
                ModelosDosBichos.CaixaSolta(go.transform, new Vector3(0f, 0.56f, 0f), new Vector3(r * 0.45f, 0.08f, r * 0.45f), madeira, new Vector3(0f, 75f, 0f));
                ModelosDosBichos.CaixaSolta(go.transform, new Vector3(0f, 0.63f, 0f), new Vector3(r * 0.15f, 0.06f, r * 0.15f), madeira);
                return go;
            }

            // a represa (13/11): o barro e os galhos atravessados no riacho, de margem a margem; cresce de ~0,4 m a ~1,1 m acima
            // da água. Dá para andar por cima (o colisor).
            static GameObject Represa(Vector3 p, float tamanho, float giro, float largura)
            {
                var go = new GameObject("Represa dos castores");
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, giro, 0f));
                var r = new System.Random(Mathf.RoundToInt(p.x * 11f + p.z * 5f));
                float comp = largura + 3f, alto = 0.35f + 0.75f * tamanho, fundo = 1.8f, grosso = 1.2f + 1.2f * tamanho;
                // o corpo de barro: a parede (x atravessa o rio; z é a correnteza)
                ModelosDosBichos.CaixaSolta(go.transform, new Vector3(0f, (alto - fundo) * 0.5f, 0f), new Vector3(comp, alto + fundo, grosso), Barro);
                ModelosDosBichos.CaixaSolta(go.transform, new Vector3(0f, alto - 0.12f, -grosso * 0.15f), new Vector3(comp * 0.96f, 0.25f, grosso * 0.6f), new Color(0.3f, 0.24f, 0.17f));
                // os galhos: por cima e encostados, mais quanto maior
                int n = 30 + Mathf.RoundToInt(70 * tamanho);
                for (int i = 0; i < n; i++)
                {
                    float x = ((float)r.NextDouble() * 2f - 1f) * comp * 0.5f;
                    float y = alto * (0.3f + 0.75f * (float)r.NextDouble());
                    float z = ((float)r.NextDouble() * 2f - 1f) * grosso * 0.55f;
                    float c = 0.8f + (float)r.NextDouble() * 1.8f;
                    var g = new Vector3((float)r.NextDouble() * 50f - 25f, (float)r.NextDouble() * 180f, (float)r.NextDouble() * 30f - 15f);
                    ModelosDosBichos.CaixaSolta(go.transform, new Vector3(x, y, z), new Vector3(0.07f, 0.07f, c), r.NextDouble() < 0.5 ? Galho : GalhoClaro, g);
                }
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, (alto - fundo) * 0.5f, 0f);
                col.size = new Vector3(comp, alto + fundo, grosso);
                return go;
            }

            // o lixo espalhado: a lata deitada (ou só as sacolas rasgadas), os sacos e os papéis em volta (13/11: o tipo é de
            // onde ele veio: 0 a lata, 1 as sacolas, 2 a caçamba). 'idade' (23/11/2026): os segundos desde que ele espalhou (o
            // novo mostra a lata tombando)
            static GameObject Lixo(Vector3 p, int tipo = 0, float idade = 999f)
            {
                var go = new GameObject("Lixo espalhado");
                go.transform.position = p;
                int semente = Mathf.RoundToInt(p.x * 7f + p.z * 3f);
                var r = new System.Random(semente);
                var t = go.transform;
                // (23/11/2026) as malhas do ModelosDoLixo: os restos espalhados, as sacolas rasgadas e, da casa, a mesma lata
                // de pé antes (o carrinho ou a de metal), caída de lado com a boca para o lixo
                var soltos = new List<GameObject>();
                soltos.Add(LixoDaCidade.Peca(t, "Os restos", ModelosDoLixo.Restos(r.Next(ModelosDoLixo.VariantesDosRestos)), Vector3.zero, Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f)));
                for (int i = 0; i < 3; i++)
                {
                    var pos = new Vector3((float)r.NextDouble() * 2f - 1f, 0f, (float)r.NextDouble() * 2f - 1f) * 1.2f;
                    soltos.Add(LixoDaCidade.Peca(t, "Sacola rasgada", ModelosDoLixo.Sacola(r.Next(ModelosDoLixo.FormasDaSacola), r.Next(ModelosDoLixo.CoresDaSacola.Length), i > 0 || tipo != 1),
                        pos, Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f)));
                }
                // (23/11/2026) o E revira o lixo espalhado também (o do lixo de onde ele veio): um gatilho largo, no chão
                int indice = LixoDaCidade.IndiceMaisPerto(p, 2.2f);
                if (indice >= 0)
                {
                    var gatilho = go.AddComponent<BoxCollider>();
                    gatilho.isTrigger = true;
                    gatilho.center = new Vector3(0f, 0.3f, 0f);
                    gatilho.size = new Vector3(2.6f, 0.6f, 2.6f);
                    var mira = go.AddComponent<LixoNaMira>();
                    mira.indice = indice;
                    mira.tipo = LixoDaCidade.Pontos[indice].tipo;
                    mira.espalhado = true;
                }
                if (tipo == 0 && LixoDaCidade.LataPerto(p, out var deCasa))
                {
                    LataTombada(go, deCasa, idade < 4f, soltos);
                    return go;
                }
                if (tipo == 0)
                {
                    var lata = LixoDaCidade.LataPerto(p, out var ponto) ? ModelosDoLixo.QualLata(ponto.semente) : ModelosDoLixo.QualLata(semente);
                    float giro = 20f + (float)r.NextDouble() * 30f;
                    if (lata.carrinho)
                        LixoDaCidade.Peca(t, "O carrinho caído", ModelosDoLixo.Carrinho(lata.cor, 2), new Vector3(0.75f, 0.315f, 0.1f), Quaternion.Euler(0f, giro, 90f));
                    else
                    {
                        LixoDaCidade.Peca(t, "A lata caída", ModelosDoLixo.LataDeMetal(2), new Vector3(0.7f, 0.29f, 0.1f), Quaternion.Euler(0f, giro, 90f));
                        LixoDaCidade.Peca(t, "A tampa da lata", ModelosDoLixo.TampaDaLata(), new Vector3(1.25f, 0.13f, -0.55f), Quaternion.Euler(180f, (float)r.NextDouble() * 360f, 0f));
                    }
                }
                return go;
            }

            // (23/11/2026) a lata da casa caída: a mesma que estava de pé (o carrinho ou a de metal), tombada em volta da beirada
            // do fundo, para o lado que a semente escolhe (o carrinho cai de lado); a de metal perde a tampa, que vai para o
            // lado da boca. 'animar': a queda (FaunaDoMundo.QuedaDaLata), com os restos aparecendo quando ela bate no chão
            static void LataTombada(GameObject go, LixoDaCidade.Ponto ponto, bool animar, List<GameObject> soltos)
            {
                var t = go.transform;
                var lata = ModelosDoLixo.QualLata(ponto.semente);
                var r = new System.Random(ponto.semente ^ 0x5bd1e995);
                var giroDePe = Quaternion.Euler(0f, LixoDaCidade.GiroDePe(ponto), 0f);
                Vector3 posDePe = ponto.pos, d;
                float raio;
                if (lata.carrinho)
                {
                    d = giroDePe * (r.NextDouble() < 0.5 ? Vector3.right : Vector3.left);
                    raio = ModelosDoLixo.TamanhoDoCarrinho.x * 0.5f;
                }
                else
                {
                    d = Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f) * Vector3.forward;
                    raio = ModelosDoLixo.RaioDaLata;
                }
                Vector3 pivo = posDePe + d * raio, eixo = Vector3.Cross(Vector3.up, d);
                var deitada = Quaternion.AngleAxis(90f, eixo);
                var peca = LixoDaCidade.Peca(t, lata.carrinho ? "O carrinho caído" : "A lata caída",
                                             lata.carrinho ? ModelosDoLixo.Carrinho(lata.cor, 2) : ModelosDoLixo.LataDeMetal(2), Vector3.zero, Quaternion.identity);
                peca.transform.SetPositionAndRotation(pivo + deitada * (posDePe - pivo), deitada * giroDePe);
                GameObject tampa = null;
                Vector3 tampaPara = default;
                var tampaGiro = Quaternion.identity;
                if (!lata.carrinho)
                {
                    Vector3 lado = eixo * (r.NextDouble() < 0.5 ? 1f : -1f);
                    tampaPara = pivo + d * (ModelosDoLixo.AlturaDaLata + 0.3f) + lado * 0.45f;
                    tampaPara.y = posDePe.y + 0.13f;
                    tampaGiro = Quaternion.Euler(180f, (float)r.NextDouble() * 360f, 0f);
                    tampa = LixoDaCidade.Peca(t, "A tampa da lata", ModelosDoLixo.TampaDaLata(), Vector3.zero, Quaternion.identity);
                    tampa.transform.SetPositionAndRotation(tampaPara, tampaGiro);
                }
                if (!animar) return;
                foreach (var s in soltos) if (s != null) s.SetActive(false);
                var q = go.AddComponent<QuedaDaLata>();
                q.lata = peca.transform;
                q.tampa = tampa != null ? tampa.transform : null;
                q.pivo = pivo;
                q.eixo = eixo;
                q.posDePe = posDePe;
                q.giroDePe = giroDePe;
                q.tampaDe = posDePe + Vector3.up * ModelosDoLixo.AlturaDaLata;
                q.tampaGiroDe = giroDePe;
                q.tampaPara = tampaPara;
                q.tampaGiroPara = tampaGiro;
                q.aparecem = soltos.ToArray();
                q.desde = Time.time;
                q.Update();   // (já no primeiro quadro, a lata de pé)
            }

            // o ninho: os galhos em roda e três ovos
            static GameObject Ninho(Vector3 p)
            {
                var go = new GameObject("Ninho dos gansos");
                go.transform.position = p;
                for (int i = 0; i < 9; i++)
                {
                    float ang = i * 40f;
                    ModelosDosBichos.CaixaSolta(go.transform, Quaternion.Euler(0f, ang, 0f) * Vector3.forward * 0.32f + Vector3.up * 0.05f,
                        new Vector3(0.05f, 0.08f, 0.32f), i % 2 == 0 ? Galho : GalhoClaro, new Vector3(0f, ang + 90f, 0f));
                }
                for (int i = 0; i < 3; i++)
                    ModelosDosBichos.CaixaSolta(go.transform, new Vector3((i - 1) * 0.09f, 0.06f, (i % 2) * 0.06f), new Vector3(0.06f, 0.07f, 0.085f), new Color(0.93f, 0.91f, 0.84f));
                return go;
            }
        }
    }
}
