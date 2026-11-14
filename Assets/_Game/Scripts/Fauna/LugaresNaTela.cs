using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// Os lugares dos bichos na tela (a etapa 5, 13/11/2026; o sorteio em FaunaDoMundo.Lugares.cs), em cada máquina, só
    /// perto de quem joga nela (até 320 m), feitos de caixas provisórias como os bichos:
    /// - a caverna: um morro de pedras facetadas com a boca, o túnel (3 m de largura, 3 m de altura) e o salão (7 x 9 m,
    ///   4 m de altura), o piso de pedra acima do chão (com a rampa na boca, se precisar); tudo com colisor (dá para entrar e
    ///   para subir nas pedras de fora). Lá dentro é escuro (o teto faz sombra);
    /// - a toca dos coiotes: o monte de terra com o buraco escuro, a terra revirada na frente e uns ossos;
    /// - o brejo: as poças de água parada (com as folhas de nenúfar), o barro, as moitas de taboa, as árvores mortas (de pé e
    ///   caídas). As poças não molham nem afundam (são só o desenho: o terreno do Gustavo não muda).
    /// </summary>
    public sealed class LugaresNaTela : MonoBehaviour
    {
        const float Perto = 320f, Longe = 380f;
        readonly Dictionary<int, GameObject> _feitos = new Dictionary<int, GameObject>();
        float _proxima;

        void Update()
        {
            if (Time.unscaledTime < _proxima) return;
            _proxima = Time.unscaledTime + 1f;
            var j = JogadorRede.Local;
            var lugares = FaunaDoMundo.Lugares;
            if (j == null || lugares.Count == 0)
            {
                if (_feitos.Count > 0 && lugares.Count == 0) Limpar();
                return;
            }
            Vector3 eu = j.transform.position;
            for (int i = 0; i < lugares.Count; i++)
            {
                var l = lugares[i];
                float d = Vector2.Distance(new Vector2(l.pos.x, l.pos.z), new Vector2(eu.x, eu.z)) - l.raio;
                _feitos.TryGetValue(i, out var go);
                if (d > Longe)
                {
                    if (go != null) Destroy(go);
                    _feitos.Remove(i);
                }
                else if (go == null && d < Perto)
                {
                    go = l.tipo == TipoDeLugarDoBicho.Caverna ? Caverna(l) : l.tipo == TipoDeLugarDoBicho.Covil ? Covil(l) : Brejo(l);
                    go.transform.SetParent(transform, true);
                    _feitos[i] = go;
                }
            }
        }

        void Limpar()
        {
            foreach (var kv in _feitos) if (kv.Value != null) Destroy(kv.Value);
            _feitos.Clear();
        }

        void OnDestroy() => Limpar();

        static readonly Color Pedra = new Color(0.36f, 0.35f, 0.33f), PedraEscura = new Color(0.25f, 0.24f, 0.23f), PedraClara = new Color(0.45f, 0.44f, 0.41f);

        static Transform Bloco(Transform pai, Vector3 meio, Vector3 tamanho, Color cor, Vector3 giro = default, bool colisor = true)
        {
            var t = ModelosDosBichos.CaixaSolta(pai, meio, tamanho, cor, giro);
            if (colisor) t.gameObject.AddComponent<BoxCollider>();
            return t;
        }

        // ------------------------------------------------------------------ a caverna

        static GameObject Caverna(LugarDosBichos l)
        {
            var go = new GameObject("Caverna dos ursos");
            go.transform.SetPositionAndRotation(l.pos, Quaternion.Euler(0f, l.giro, 0f));
            var t = go.transform;
            var r = new System.Random(l.semente);
            float R() => (float)r.NextDouble();

            // o piso: a laje grossa (desce até dentro do chão, para não ficar vão por baixo)
            Bloco(t, new Vector3(0f, -2.3f, 6.5f), new Vector3(10f, 4.6f, 18f), PedraEscura);
            // a rampa da boca: do chão de fora até o piso
            float fora = FaunaDoMundo.AlturaDoMapa(l.NoMundo(new Vector3(0f, 0f, -3f)).x, l.NoMundo(new Vector3(0f, 0f, -3f)).z, l.pos.y);
            float degrau = l.pos.y - fora;
            if (degrau > 0.15f)
            {
                float comp = Mathf.Max(3f, degrau * 2.2f);
                float ang = Mathf.Atan2(degrau, comp) * Mathf.Rad2Deg;
                Bloco(t, new Vector3(0f, -degrau * 0.5f - 0.2f, -comp * 0.5f + 0.2f), new Vector3(3.6f, 0.4f, comp + 0.4f), PedraEscura, new Vector3(-ang, 0f, 0f));
            }
            // o túnel (z de 0 a 5): as paredes e o teto
            Bloco(t, new Vector3(-2.15f, 1.6f, 2.5f), new Vector3(1.3f, 3.2f, 5f), Pedra);
            Bloco(t, new Vector3(2.15f, 1.6f, 2.5f), new Vector3(1.3f, 3.2f, 5f), Pedra);
            Bloco(t, new Vector3(0f, 3.75f, 2.5f), new Vector3(5.6f, 1.1f, 5.2f), PedraEscura);
            // o salão (x de -3,5 a 3,5; z de 5 a 14): as paredes, o fundo, a frente dos lados do túnel e o teto
            Bloco(t, new Vector3(-4.3f, 2.1f, 9.6f), new Vector3(1.6f, 4.2f, 10f), Pedra);
            Bloco(t, new Vector3(4.3f, 2.1f, 9.6f), new Vector3(1.6f, 4.2f, 10f), Pedra);
            Bloco(t, new Vector3(0f, 2.1f, 14.8f), new Vector3(10.2f, 4.2f, 1.6f), Pedra);
            Bloco(t, new Vector3(-2.9f, 2.1f, 5.35f), new Vector3(2.8f, 4.2f, 0.7f), Pedra);
            Bloco(t, new Vector3(2.9f, 2.1f, 5.35f), new Vector3(2.8f, 4.2f, 0.7f), Pedra);
            Bloco(t, new Vector3(0f, 4.85f, 9.6f), new Vector3(10.2f, 1.3f, 11f), PedraEscura);
            // lá dentro: umas pedras soltas, a cama de folhas e os ossos
            for (int i = 0; i < 6; i++)
                Bloco(t, new Vector3(R() * 5f - 2.5f, 0.12f, 6f + R() * 7.5f), new Vector3(0.3f + R() * 0.5f, 0.25f + R() * 0.3f, 0.3f + R() * 0.5f), PedraClara, new Vector3(R() * 30f, R() * 360f, R() * 30f), false);
            Bloco(t, new Vector3(0.8f, 0.04f, 11.5f), new Vector3(2.6f, 0.08f, 2.2f), new Color(0.35f, 0.27f, 0.15f), new Vector3(0f, R() * 360f, 0f), false);
            for (int i = 0; i < 4; i++)
                Bloco(t, new Vector3(R() * 4f - 2f, 0.04f, 7f + R() * 6f), new Vector3(0.05f, 0.05f, 0.35f + R() * 0.2f), new Color(0.88f, 0.86f, 0.78f), new Vector3(0f, R() * 360f, 0f), false);
            // por fora: o morro de pedras (cobre a casca; dá para subir)
            for (int i = 0; i < 14; i++)
            {
                float ang = i / 14f * Mathf.PI * 2f + R() * 0.3f;
                float rx = 6.2f + R() * 1.4f, rz = 8.6f + R() * 1.6f;
                var p = new Vector3(Mathf.Cos(ang) * rx, 0.8f + R() * 2.6f, 9.6f + Mathf.Sin(ang) * rz);
                // a boca fica livre
                if (p.z < 3f && Mathf.Abs(p.x) < 4.2f) p.x = Mathf.Sign(p.x == 0f ? 1f : p.x) * (4.2f + R() * 1.5f);
                float tam = 3.2f + R() * 3.2f;
                Bloco(t, p, new Vector3(tam, tam * (0.6f + R() * 0.5f), tam * (0.8f + R() * 0.4f)), i % 3 == 0 ? PedraClara : Pedra, new Vector3(R() * 30f - 15f, R() * 360f, R() * 30f - 15f));
            }
            for (int i = 0; i < 4; i++)
                Bloco(t, new Vector3(R() * 6f - 3f, 5.2f + R() * 1.2f, 6f + R() * 8f), new Vector3(4f + R() * 3f, 1.6f + R() * 1.4f, 4f + R() * 3f), i % 2 == 0 ? Pedra : PedraClara, new Vector3(R() * 20f - 10f, R() * 360f, R() * 20f - 10f));
            // a testa da boca (por cima do túnel)
            Bloco(t, new Vector3(0f, 4.3f, -0.3f), new Vector3(6.4f, 1.6f, 1.6f), PedraClara, new Vector3(-8f, 0f, 0f));
            return go;
        }

        // ------------------------------------------------------------------ a toca dos coiotes

        static GameObject Covil(LugarDosBichos l)
        {
            var go = new GameObject("Toca dos coiotes");
            go.transform.SetPositionAndRotation(l.pos, Quaternion.Euler(0f, l.giro, 0f));
            var t = go.transform;
            var r = new System.Random(l.semente);
            float R() => (float)r.NextDouble();
            var terra = new Color(0.4f, 0.31f, 0.21f);
            var terraEscura = new Color(0.3f, 0.23f, 0.16f);
            // o monte de terra (com o colisor: dá para subir) e o buraco escuro na frente
            Bloco(t, new Vector3(0f, 0.55f, 1.2f), new Vector3(4.2f, 1.5f, 3.4f), terra, new Vector3(-6f, R() * 10f - 5f, 0f));
            Bloco(t, new Vector3(0f, 1.05f, 1.8f), new Vector3(2.8f, 1.1f, 2.4f), terraEscura, new Vector3(-10f, R() * 20f - 10f, 0f));
            Bloco(t, new Vector3(0f, 0.35f, -0.48f), new Vector3(0.75f, 0.55f, 0.12f), new Color(0.04f, 0.035f, 0.03f), default, false);
            Bloco(t, new Vector3(0f, 0.74f, -0.42f), new Vector3(1.2f, 0.28f, 0.5f), terraEscura, new Vector3(-12f, 0f, 0f));
            // a terra revirada na frente e uns ossos
            for (int i = 0; i < 6; i++)
                Bloco(t, new Vector3(R() * 3f - 1.5f, 0.03f, -1.2f - R() * 2.2f), new Vector3(0.5f + R() * 0.7f, 0.08f, 0.4f + R() * 0.6f), terraEscura, new Vector3(0f, R() * 360f, 0f), false);
            for (int i = 0; i < 5; i++)
                Bloco(t, new Vector3(R() * 4f - 2f, 0.03f, -1.5f - R() * 3f), new Vector3(0.04f, 0.04f, 0.25f + R() * 0.25f), new Color(0.88f, 0.86f, 0.78f), new Vector3(0f, R() * 360f, 0f), false);
            return go;
        }

        // ------------------------------------------------------------------ o brejo

        /// <summary>As poças do brejo (no espaço do mundo: o meio e o raio), o mesmo sorteio do desenho.</summary>
        static List<(Vector2 meio, float raio)> Pocas(LugarDosBichos b)
        {
            var r = new System.Random(b.semente);
            var pocas = new List<(Vector2, float)>();
            int n = 7 + r.Next(5);
            for (int i = 0; i < n; i++)
            {
                float ang = (float)r.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)r.NextDouble()) * b.raio * 0.75f;
                pocas.Add((new Vector2(b.pos.x + Mathf.Cos(ang) * d, b.pos.z + Mathf.Sin(ang) * d), 1.8f + (float)r.NextDouble() * 3.5f));
            }
            return pocas;
        }

        /// <summary>O ponto cai numa poça do brejo (os bichos não nascem nela).</summary>
        public static bool NaPoca(LugarDosBichos b, Vector3 p)
        {
            foreach (var (meio, raio) in Pocas(b)) if ((meio - new Vector2(p.x, p.z)).sqrMagnitude < raio * raio) return true;
            return false;
        }

        static GameObject Brejo(LugarDosBichos b)
        {
            var go = new GameObject("Brejo");
            go.transform.position = b.pos;
            var t = go.transform;
            var r = new System.Random(b.semente ^ 0x77);
            float R() => (float)r.NextDouble();
            var agua = new Color(0.16f, 0.2f, 0.13f);
            var barro = new Color(0.22f, 0.18f, 0.12f);
            var folha = new Color(0.22f, 0.36f, 0.16f);
            var taboa = new Color(0.32f, 0.42f, 0.2f);
            var cabeca = new Color(0.33f, 0.2f, 0.11f);
            var morta = new Color(0.42f, 0.4f, 0.36f);
            Vector3 Local(Vector2 mundo) { var q = new Vector3(mundo.x, FaunaDoMundo.AlturaDoMapa(mundo.x, mundo.y, b.pos.y), mundo.y); return q - b.pos; }

            // as poças: a água parada (escura), a borda de barro e as folhas de nenúfar
            foreach (var (meio, raio) in Pocas(b))
            {
                if (FaunaDoMundo.AguaEm(meio.x, meio.y, out _) > 0.02f) continue;   // (no rio ou no lago, não)
                var c = Local(meio);
                ModelosDosBichos.CilindroSolto(t, c + Vector3.up * 0.03f, raio + 0.6f, 0.04f, barro);
                ModelosDosBichos.CilindroSolto(t, c + Vector3.up * 0.06f, raio, 0.04f, agua);
                int folhas = 2 + r.Next(5);
                for (int k = 0; k < folhas; k++)
                {
                    float ang = R() * Mathf.PI * 2f, d = R() * raio * 0.8f;
                    ModelosDosBichos.CilindroSolto(t, c + new Vector3(Mathf.Cos(ang) * d, 0.09f, Mathf.Sin(ang) * d), 0.18f + R() * 0.15f, 0.01f, folha);
                }
            }
            // o barro solto
            for (int i = 0; i < 10; i++)
            {
                float ang = R() * Mathf.PI * 2f, d = Mathf.Sqrt(R()) * b.raio;
                var m2 = new Vector2(b.pos.x + Mathf.Cos(ang) * d, b.pos.z + Mathf.Sin(ang) * d);
                float tx = 1.5f + R() * 2.5f, tz = 1.2f + R() * 2f, giroDoBarro = R() * 360f;
                if (FaunaDoMundo.AguaEm(m2.x, m2.y, out _) > 0.02f) continue;
                var c = Local(m2);
                ModelosDosBichos.CaixaSolta(t, c + Vector3.up * 0.02f, new Vector3(tx, 0.04f, tz), barro, new Vector3(0f, giroDoBarro, 0f));
            }
            // as moitas de taboa (as hastes e as cabeças marrons)
            int moitas = 14 + r.Next(8);
            for (int i = 0; i < moitas; i++)
            {
                float ang = R() * Mathf.PI * 2f, d = Mathf.Sqrt(R()) * b.raio * 0.95f;
                var m2 = new Vector2(b.pos.x + Mathf.Cos(ang) * d, b.pos.z + Mathf.Sin(ang) * d);
                int hastes = 6 + r.Next(8);
                if (FaunaDoMundo.AguaEm(m2.x, m2.y, out _) > 0.35f) { for (int k = 0; k < hastes * 7; k++) R(); continue; }   // (só na terra ou no raso)
                var c = Local(m2);
                for (int k = 0; k < hastes; k++)
                {
                    var q = c + new Vector3(R() * 1.2f - 0.6f, 0f, R() * 1.2f - 0.6f);
                    float alto = 1.1f + R() * 0.8f;
                    var g = new Vector3(R() * 10f - 5f, R() * 360f, R() * 10f - 5f);
                    ModelosDosBichos.CaixaSolta(t, q + Vector3.up * alto * 0.5f, new Vector3(0.025f, alto, 0.025f), taboa, g);
                    if (R() < 0.6f) ModelosDosBichos.CaixaSolta(t, q + Vector3.up * (alto - 0.15f), new Vector3(0.06f, 0.22f, 0.06f), cabeca, g);
                }
            }
            // as árvores mortas: de pé (o tronco cinza e uns galhos pelados) e caídas (com colisor)
            int arvores = 4 + r.Next(4);
            for (int i = 0; i < arvores; i++)
            {
                float ang = R() * Mathf.PI * 2f, d = Mathf.Sqrt(R()) * b.raio * 0.9f;
                var m2 = new Vector2(b.pos.x + Mathf.Cos(ang) * d, b.pos.z + Mathf.Sin(ang) * d);
                if (FaunaDoMundo.AguaEm(m2.x, m2.y, out _) > 0.02f) continue;
                var c = Local(m2);
                if (R() < 0.6f)
                {
                    float alto = 4f + R() * 5f, grosso = 0.22f + R() * 0.2f;
                    var tronco = Bloco(t, c + Vector3.up * alto * 0.5f, new Vector3(grosso, alto, grosso), morta, new Vector3(R() * 12f - 6f, R() * 360f, R() * 12f - 6f));
                    int galhos = 2 + r.Next(3);
                    for (int k = 0; k < galhos; k++)
                        ModelosDosBichos.CaixaSolta(tronco, new Vector3(0f, 0.1f + R() * 0.35f, 0f), new Vector3(0.35f, 0.25f + R() * 0.15f, 0.35f), morta, new Vector3(40f + R() * 30f, R() * 360f, 0f));
                }
                else
                {
                    float comp = 3f + R() * 4f, grosso = 0.25f + R() * 0.2f;
                    Bloco(t, c + Vector3.up * grosso * 0.4f, new Vector3(grosso, grosso, comp), morta, new Vector3(R() * 6f, R() * 360f, R() * 20f));
                }
            }
            return go;
        }
    }
}
