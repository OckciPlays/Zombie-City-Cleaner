using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>De onde vem o lixo: a lata da casa, as sacolas e a caçamba das lojas.</summary>
    public enum TipoDeLixo : byte { Lata, Sacolas, Cacamba }

    /// <summary>
    /// O lixo da cidade no Lakewood (13/11/2026, a pedido: "os guaxinins revirariam os lixos, estariam em sacolas mexendo e
    /// fazendo barulho, em latas de lixo"). Antes a lixeira de cada casa existia só para o guaxinim (não era desenhada);
    /// agora a Woodcreek tem o lixo à vista: a lata na frente de cada casa (na beira do lote, do lado da rua), as sacolas
    /// pretas e verdes do lado de metade delas, a caçamba atrás das lojas, do posto e da loja de armas (com as sacolas em
    /// volta) e uns montes de sacolas nos lotes vazios. Tudo sai do sorteio de cada lote (o mesmo em todas as máquinas: nada
    /// vai pela rede) e só é desenhado perto do jogador desta máquina (até 140 m). Onde o guaxinim revirou (o lixo espalhado,
    /// que vai pela mensagem das casas), a lata e as sacolas somem: o lixo espalhado já mostra a lata deitada e os sacos.
    /// </summary>
    public sealed class LixoDaCidade : MonoBehaviour
    {
        public struct Ponto
        {
            public Vector3 pos;
            public float giro;
            public TipoDeLixo tipo;
            public int quantas;
            public int semente;
        }

        static readonly List<Ponto> _pontos = new List<Ponto>();
        static ChaoDeLakewood _doChao;

        /// <summary>Os pontos de lixo do mapa (vazio fora do Lakewood).</summary>
        public static List<Ponto> Pontos
        {
            get
            {
                var chao = LakewoodNaCena.Instancia != null ? LakewoodNaCena.Instancia.ChaoNoMapa : null;
                if (chao != _doChao)
                {
                    _doChao = chao;
                    Montar(chao);
                }
                return _pontos;
            }
        }

        /// <summary>Onde o lixo espalhado do guaxinim fica, para o ponto (a caçamba fica de pé: o lixo vai do lado).</summary>
        public static Vector3 OndeEspalha(Ponto p) => p.tipo == TipoDeLixo.Cacamba ? p.pos + Quaternion.Euler(0f, p.giro, 0f) * Vector3.right * 1.6f : p.pos;

        /// <summary>
        /// (23/11/2026) O giro da lata, das sacolas ou da caçamba de pé (o do lote, torto até 15° pela semente): o mesmo em
        /// todas as máquinas (o guaxinim dentro da caçamba, a lata que tomba).
        /// </summary>
        public static float GiroDePe(Ponto p) => p.giro + (float)new System.Random(p.semente).NextDouble() * 30f - 15f;

        static void Montar(ChaoDeLakewood chao)
        {
            _pontos.Clear();
            if (chao == null) return;
            foreach (var l in chao.Lotes)
            {
                var lote = l.Lote;
                var r = new System.Random(lote.semente ^ (l.Indice * 7919));
                switch (lote.tipo)
                {
                    case UsoDoLote.Casa:
                    case UsoDoLote.Cabana:
                    {
                        if (l.Plano == null) break;
                        if (lote.tipo == UsoDoLote.Cabana && r.NextDouble() > 0.4) break;
                        // a lata: na beira do lote, do lado da rua (o mesmo lugar de antes, onde o guaxinim já revirava)
                        Vector2 q = lote.NoMapa(lote.largura * 0.35f, -lote.fundo * 0.5f + 0.8f);
                        _pontos.Add(new Ponto { pos = new Vector3(q.x, chao.Altura(q.x, q.y), q.y), giro = lote.giro, tipo = TipoDeLixo.Lata, quantas = 1, semente = r.Next() });
                        // as sacolas do lado de metade delas
                        if (r.NextDouble() < 0.55)
                        {
                            Vector2 s = lote.NoMapa(lote.largura * 0.35f - 1f, -lote.fundo * 0.5f + 0.7f);
                            _pontos.Add(new Ponto { pos = new Vector3(s.x, chao.Altura(s.x, s.y), s.y), giro = lote.giro, tipo = TipoDeLixo.Sacolas, quantas = 1 + r.Next(3), semente = r.Next() });
                        }
                        break;
                    }
                    case UsoDoLote.Loja:
                    case UsoDoLote.Posto:
                    case UsoDoLote.LojaDeArmas:
                    {
                        // a caçamba atrás (ou do lado) da construção, com as sacolas em volta
                        var c = l.Construcao;
                        float z = Mathf.Min(c.yMax + 2.2f, lote.fundo * 0.5f - 1.6f);
                        float x = Mathf.Clamp(c.center.x + c.width * 0.25f, -lote.largura * 0.5f + 2f, lote.largura * 0.5f - 2f);
                        if (z < c.yMax + 1.2f)
                        {
                            // sem lugar atrás: do lado
                            z = c.center.y;
                            x = c.xMax + 2f < lote.largura * 0.5f - 1f ? c.xMax + 1.6f : c.xMin - 1.6f;
                        }
                        var cp = l.NoMundo(x, z);
                        _pontos.Add(new Ponto { pos = cp, giro = lote.giro, tipo = TipoDeLixo.Cacamba, quantas = 1, semente = r.Next() });
                        var sp = l.NoMundo(x - 2.1f, z);
                        _pontos.Add(new Ponto { pos = sp, giro = lote.giro, tipo = TipoDeLixo.Sacolas, quantas = 2 + r.Next(3), semente = r.Next() });
                        break;
                    }
                    case UsoDoLote.Vazio:
                    {
                        // uns montes de sacolas largadas
                        if (r.NextDouble() > 0.3) break;
                        Vector2 q = lote.NoMapa(((float)r.NextDouble() - 0.5f) * lote.largura * 0.5f, ((float)r.NextDouble() - 0.5f) * lote.fundo * 0.5f);
                        _pontos.Add(new Ponto { pos = new Vector3(q.x, chao.Altura(q.x, q.y), q.y), giro = (float)r.NextDouble() * 360f, tipo = TipoDeLixo.Sacolas, quantas = 3 + r.Next(4), semente = r.Next() });
                        break;
                    }
                }
            }
            // (feitos no mapa, os lotes; o lixo fica no mundo do jogo: OrigemDeLakewood, 16/11/2026)
            for (int i = 0; i < _pontos.Count; i++)
            {
                var p = _pontos[i];
                p.pos = OrigemDeLakewood.NoMundo(p.pos);
                _pontos[i] = p;
            }
        }

        // ------------------------------------------------------------------ a tela (em cada máquina)

        const float Perto = 140f, Longe = 170f;
        readonly Dictionary<int, GameObject> _feitos = new Dictionary<int, GameObject>();
        bool _feitosNoAntes;
        float _proxima;
        bool _conferirAgora;
        static LixoDaCidade _instancia;

        void Awake() => _instancia = this;

        /// <summary>
        /// (23/11/2026) Confere o lixo na próxima vez, sem esperar os 0,75 s: o lixo espalhado acabou de aparecer (a lata tombando
        /// começa onde a de pé estava, e a de pé some junto).
        /// </summary>
        public static void ConferirAgora()
        {
            if (_instancia != null) _instancia._conferirAgora = true;
        }

        /// <summary>O lixo mais perto de 'p' (a até 'raio' m), ou -1.</summary>
        public static int IndiceMaisPerto(Vector3 p, float raio)
        {
            var pontos = Pontos;
            int melhor = -1;
            float m = raio * raio;
            for (int i = 0; i < pontos.Count; i++)
            {
                float dx = pontos[i].pos.x - p.x, dz = pontos[i].pos.z - p.z, d = dx * dx + dz * dz;
                if (d < m) { m = d; melhor = i; }
            }
            return melhor;
        }

        void Update()
        {
            Balancar();
            if (Time.unscaledTime < _proxima && !_conferirAgora) return;
            _conferirAgora = false;
            _proxima = Time.unscaledTime + 0.75f;
            var j = JogadorRede.Local;
            var pontos = Pontos;
            if (j == null || pontos.Count == 0)
            {
                if (_feitos.Count > 0) Limpar();
                return;
            }
            Vector3 eu = j.transform.position;
            if (LakewoodNaCena.NoAntes != _feitosNoAntes)
            {
                Limpar();
                _feitosNoAntes = LakewoodNaCena.NoAntes;
            }
            for (int i = 0; i < pontos.Count; i++)
            {
                var p = pontos[i];
                float d = Vector2.Distance(new Vector2(p.pos.x, p.pos.z), new Vector2(eu.x, eu.z));
                _feitos.TryGetValue(i, out var go);
                if (d > Longe)
                {
                    if (go != null) Destroy(go);
                    _feitos.Remove(i);
                    continue;
                }
                if (go == null && d < Perto)
                {
                    go = Fazer(p, i);
                    go.transform.SetParent(transform, true);
                    _feitos[i] = go;
                    _giroDePe[i] = go.transform.rotation;
                }
                // revirado pelo guaxinim: a lata e as sacolas somem (o lixo espalhado as mostra caídas)
                if (go != null && p.tipo != TipoDeLixo.Cacamba)
                {
                    bool revirado = false;
                    foreach (var l in FaunaDoMundo.LixoNaTela) if ((l - p.pos).sqrMagnitude < 2.25f) { revirado = true; break; }
                    if (go.activeSelf == revirado) go.SetActive(!revirado);
                }
            }
        }

        void Limpar()
        {
            foreach (var kv in _feitos) if (kv.Value != null) Destroy(kv.Value);
            _feitos.Clear();
            _giroDePe.Clear();
            _balancando.Clear();
        }

        void OnDestroy()
        {
            Limpar();
            if (_instancia == this) _instancia = null;
        }

        // ------------------------------------------------------------------ o guaxinim mexendo (em cada máquina)

        // (23/11/2026) a lata balança com o guaxinim mexendo do lado (antes de tombar: Bicho.Cacamba.cs), e a caçamba dá um
        // tranco de vez em quando com ele dentro (quem chega perto e presta atenção vê)
        readonly List<Vector3> _guaxinins = new List<Vector3>();
        readonly Dictionary<int, float> _balancando = new Dictionary<int, float>();   // o lixo e até quando (Time.time)
        readonly Dictionary<int, Quaternion> _giroDePe = new Dictionary<int, Quaternion>();
        readonly List<int> _parar = new List<int>();
        float _proximaOlhada;

        void Balancar()
        {
            float agora = Time.time;
            var pontos = Pontos;
            if (agora >= _proximaOlhada)
            {
                _proximaOlhada = agora + 0.2f;
                if (FaunaDoMundo.GuaxininsParados(_guaxinins) > 0)
                    foreach (var kv in _feitos)
                    {
                        if (kv.Value == null || !kv.Value.activeSelf || kv.Key >= pontos.Count) continue;
                        var p = pontos[kv.Key];
                        if (p.tipo == TipoDeLixo.Sacolas) continue;
                        bool cacamba = p.tipo == TipoDeLixo.Cacamba;
                        float raio = cacamba ? 1f : 1.35f, acima = cacamba ? 1f : 1.5f;
                        foreach (var g in _guaxinins)
                        {
                            float dx = g.x - p.pos.x, dz = g.z - p.pos.z;
                            if (dx * dx + dz * dz > raio * raio || g.y < p.pos.y - 0.5f || g.y > p.pos.y + acima) continue;
                            if (!cacamba) _balancando[kv.Key] = agora + 0.35f;
                            else if (!_balancando.ContainsKey(kv.Key) && Random.value < 0.06f) _balancando[kv.Key] = agora + 0.4f;
                            break;
                        }
                    }
            }
            if (_balancando.Count == 0) return;
            _parar.Clear();
            foreach (var kv in _balancando)
            {
                _feitos.TryGetValue(kv.Key, out var go);
                if (go == null || !_giroDePe.TryGetValue(kv.Key, out var giro))
                {
                    _parar.Add(kv.Key);
                    continue;
                }
                if (agora > kv.Value || !go.activeSelf)
                {
                    go.transform.rotation = giro;
                    _parar.Add(kv.Key);
                    continue;
                }
                bool cacamba = kv.Key < pontos.Count && pontos[kv.Key].tipo == TipoDeLixo.Cacamba;
                float a = cacamba ? 1.2f : 4.5f, t = agora * (cacamba ? 31f : 19f) + kv.Key;
                go.transform.rotation = giro * Quaternion.Euler(Mathf.Sin(t) * a, 0f, Mathf.Sin(t * 1.37f + 1f) * a * 0.8f);
            }
            foreach (int k in _parar) _balancando.Remove(k);
        }

        /// <summary>
        /// O material do lixo (23/11/2026): o da cidade, com o abandono (o plástico desbota e racha, o metal enferruja) e na
        /// época (o de antes, limpo). No Lakewood, o do chão das obras (sem a umidade da base: a lata não é uma parede); no
        /// mapa sorteado, o dos objetos de rua sem a altura.
        /// </summary>
        internal static Material MaterialDoLixo
        {
            get
            {
                var fonte = LakewoodNaCena.Instancia != null && LakewoodNaCena.Instancia.materialDaCidade != null ? LakewoodNaCena.Instancia.materialDaCidade : null;
                if (fonte == null && ObjetosDoMundo.Instancia != null && ObjetosDoMundo.Instancia.materialDaCidade != null)
                {
                    if (_semAltura == null || _semAlturaDe != ObjetosDoMundo.Instancia.materialDaCidade)
                    {
                        _semAlturaDe = ObjetosDoMundo.Instancia.materialDaCidade;
                        _semAltura = new Material(_semAlturaDe) { name = _semAlturaDe.name + " (o lixo)" };
                        if (_semAltura.HasProperty("_ModoDaAltura")) _semAltura.SetFloat("_ModoDaAltura", 0f);
                        if (_semAltura.HasProperty("_Chao")) _semAltura.SetFloat("_Chao", -10000f);
                    }
                    fonte = _semAltura;
                }
                return LakewoodNaCena.NaEpoca(fonte);
            }
        }
        static Material _semAltura, _semAlturaDe;

        /// <summary>A lata de uma casa a até 1,5 m de 'p' (a do lixo revirado: a mesma, caída).</summary>
        internal static bool LataPerto(Vector3 p, out Ponto lata)
        {
            lata = default;
            float melhor = 2.25f;
            bool achou = false;
            foreach (var l in Pontos)
            {
                if (l.tipo != TipoDeLixo.Lata) continue;
                float d = (new Vector2(l.pos.x, l.pos.z) - new Vector2(p.x, p.z)).sqrMagnitude;
                if (d < melhor) { melhor = d; lata = l; achou = true; }
            }
            return achou;
        }

        /// <summary>Uma peça do lixo: a malha no material do lixo, filha de 'pai'.</summary>
        internal static GameObject Peca(Transform pai, string nome, Mesh malha, Vector3 pos, Quaternion giro)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            go.transform.SetLocalPositionAndRotation(pos, giro);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialDoLixo;
            return go;
        }

        // (23/11/2026) as malhas do ModelosDoLixo no lugar dos cilindros e das caixas de cor lisa; e o E que revira (LixoNaMira:
        // na colisão da lata e da caçamba; nas sacolas, um gatilho em volta delas)
        static GameObject Fazer(Ponto p, int indice)
        {
            var r = new System.Random(p.semente);
            var go = new GameObject(p.tipo == TipoDeLixo.Lata ? "Lata de lixo" : p.tipo == TipoDeLixo.Sacolas ? "Sacolas de lixo" : "Caçamba");
            go.transform.SetPositionAndRotation(p.pos, Quaternion.Euler(0f, p.giro + (float)r.NextDouble() * 30f - 15f, 0f));
            var t = go.transform;
            switch (p.tipo)
            {
                case TipoDeLixo.Lata:
                {
                    var lata = ModelosDoLixo.QualLata(p.semente);
                    Peca(t, lata.carrinho ? "O carrinho" : "A lata de metal", ModelosDoLixo.DaLata(lata), Vector3.zero, Quaternion.identity);
                    if (lata.carrinho)
                    {
                        var col = go.AddComponent<BoxCollider>();
                        var tam = ModelosDoLixo.TamanhoDoCarrinho;
                        col.center = new Vector3(0f, tam.y * 0.5f, 0.02f);
                        col.size = tam;
                    }
                    else
                    {
                        var col = go.AddComponent<CapsuleCollider>();
                        col.center = new Vector3(0f, ModelosDoLixo.AlturaDaLata * 0.5f, 0f);
                        col.radius = ModelosDoLixo.RaioDaLata;
                        col.height = ModelosDoLixo.AlturaDaLata;
                    }
                    break;
                }
                case TipoDeLixo.Sacolas:
                {
                    for (int i = 0; i < p.quantas; i++)
                    {
                        var pos = new Vector3(((float)r.NextDouble() - 0.5f) * 0.9f, 0f, ((float)r.NextDouble() - 0.5f) * 0.7f);
                        var giro = Quaternion.Euler((float)r.NextDouble() * 12f - 6f, (float)r.NextDouble() * 360f, (float)r.NextDouble() * 12f - 6f);
                        // (as de cima de um monte ficam um pouco mais altas, encostadas nas outras)
                        if (i >= 3) pos.y = 0.12f;
                        Peca(t, "Sacola", ModelosDoLixo.Sacola(r.Next(ModelosDoLixo.FormasDaSacola), r.Next(ModelosDoLixo.CoresDaSacola.Length)), pos, giro);
                    }
                    var gatilho = go.AddComponent<BoxCollider>();
                    gatilho.isTrigger = true;
                    gatilho.center = new Vector3(0f, 0.3f, 0f);
                    gatilho.size = new Vector3(1.4f, 0.6f, 1.2f);
                    break;
                }
                case TipoDeLixo.Cacamba:
                {
                    Peca(t, "A caçamba", ModelosDoLixo.Cacamba(r.Next(ModelosDoLixo.CoresDaCacamba.Length), r.Next(3)), Vector3.zero, Quaternion.identity);
                    var col = go.AddComponent<BoxCollider>();
                    var tam = ModelosDoLixo.TamanhoDaCacamba;
                    col.center = new Vector3(0f, tam.y * 0.5f, 0f);
                    col.size = tam;
                    break;
                }
            }
            var mira = go.AddComponent<LixoNaMira>();
            mira.indice = indice;
            mira.tipo = p.tipo;
            return go;
        }
    }
}
