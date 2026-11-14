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
                var chao = LakewoodNaCena.Instancia != null ? LakewoodNaCena.Instancia.Chao : null;
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
        }

        // ------------------------------------------------------------------ a tela (em cada máquina)

        const float Perto = 140f, Longe = 170f;
        readonly Dictionary<int, GameObject> _feitos = new Dictionary<int, GameObject>();
        float _proxima;

        void Update()
        {
            if (Time.unscaledTime < _proxima) return;
            _proxima = Time.unscaledTime + 0.75f;
            var j = JogadorRede.Local;
            var pontos = Pontos;
            if (j == null || pontos.Count == 0)
            {
                if (_feitos.Count > 0) Limpar();
                return;
            }
            Vector3 eu = j.transform.position;
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
                    go = Fazer(p);
                    go.transform.SetParent(transform, true);
                    _feitos[i] = go;
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
        }

        void OnDestroy() => Limpar();

        static readonly Color[] CorDaLata = { new Color(0.22f, 0.3f, 0.24f), new Color(0.12f, 0.13f, 0.14f), new Color(0.2f, 0.27f, 0.42f), new Color(0.45f, 0.46f, 0.44f) };
        static readonly Color[] CorDaSacola = { new Color(0.06f, 0.06f, 0.07f), new Color(0.09f, 0.1f, 0.09f), new Color(0.16f, 0.24f, 0.15f), new Color(0.5f, 0.5f, 0.48f) };

        static GameObject Fazer(Ponto p)
        {
            var r = new System.Random(p.semente);
            var go = new GameObject(p.tipo == TipoDeLixo.Lata ? "Lata de lixo" : p.tipo == TipoDeLixo.Sacolas ? "Sacolas de lixo" : "Caçamba");
            go.transform.SetPositionAndRotation(p.pos, Quaternion.Euler(0f, p.giro + (float)r.NextDouble() * 30f - 15f, 0f));
            var t = go.transform;
            switch (p.tipo)
            {
                case TipoDeLixo.Lata:
                {
                    var cor = CorDaLata[r.Next(CorDaLata.Length)];
                    ModelosDosBichos.CilindroSolto(t, new Vector3(0f, 0.45f, 0f), 0.29f, 0.9f, cor);
                    ModelosDosBichos.CilindroSolto(t, new Vector3(0f, 0.92f, 0f), 0.31f, 0.06f, cor * 0.85f);
                    ModelosDosBichos.CaixaSolta(t, new Vector3(0f, 0.97f, 0f), new Vector3(0.16f, 0.04f, 0.04f), cor * 0.7f);
                    // às vezes a sacola aparece pela tampa
                    if (r.NextDouble() < 0.4) ModelosDosBichos.CaixaSolta(t, new Vector3(0.08f, 0.96f, 0.05f), new Vector3(0.22f, 0.1f, 0.2f), CorDaSacola[0], new Vector3(10f, 30f, 8f));
                    var col = go.AddComponent<CapsuleCollider>();
                    col.center = new Vector3(0f, 0.47f, 0f);
                    col.radius = 0.3f;
                    col.height = 0.95f;
                    break;
                }
                case TipoDeLixo.Sacolas:
                {
                    for (int i = 0; i < p.quantas; i++)
                    {
                        var cor = CorDaSacola[r.Next(CorDaSacola.Length)];
                        float tam = 0.38f + (float)r.NextDouble() * 0.18f;
                        var pos = new Vector3(((float)r.NextDouble() - 0.5f) * 0.9f, tam * 0.42f, ((float)r.NextDouble() - 0.5f) * 0.7f);
                        var giro = new Vector3((float)r.NextDouble() * 16f - 8f, (float)r.NextDouble() * 360f, (float)r.NextDouble() * 16f - 8f);
                        var s = ModelosDosBichos.CaixaSolta(t, pos, new Vector3(tam, tam * 0.85f, tam * 0.9f), cor, giro);
                        // o nó em cima
                        ModelosDosBichos.CaixaSolta(s, new Vector3(0f, 0.6f, 0f), new Vector3(0.25f, 0.3f, 0.25f), cor);
                    }
                    break;
                }
                case TipoDeLixo.Cacamba:
                {
                    var verde = new Color(0.16f, 0.3f, 0.2f);
                    ModelosDosBichos.CaixaSolta(t, new Vector3(0f, 0.62f, 0f), new Vector3(1.9f, 1.05f, 1.15f), verde);
                    ModelosDosBichos.CaixaSolta(t, new Vector3(-0.48f, 1.17f, -0.05f), new Vector3(0.92f, 0.05f, 1.25f), verde * 0.75f, new Vector3(-6f, 0f, 0f));
                    ModelosDosBichos.CaixaSolta(t, new Vector3(0.48f, 1.2f, 0.2f), new Vector3(0.92f, 0.05f, 1.25f), verde * 0.75f, new Vector3(-28f, 0f, 0f));
                    for (int i = 0; i < 4; i++)
                        ModelosDosBichos.CaixaSolta(t, new Vector3(i < 2 ? -0.8f : 0.8f, 0.06f, i % 2 == 0 ? -0.45f : 0.45f), new Vector3(0.1f, 0.12f, 0.1f), new Color(0.1f, 0.1f, 0.1f));
                    // a sacola saindo pela tampa aberta
                    ModelosDosBichos.CaixaSolta(t, new Vector3(0.45f, 1.2f, -0.1f), new Vector3(0.4f, 0.32f, 0.35f), CorDaSacola[0], new Vector3(0f, 25f, 10f));
                    var col = go.AddComponent<BoxCollider>();
                    col.center = new Vector3(0f, 0.62f, 0f);
                    col.size = new Vector3(1.9f, 1.15f, 1.15f);
                    break;
                }
            }
            return go;
        }
    }
}
