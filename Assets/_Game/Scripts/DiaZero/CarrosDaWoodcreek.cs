using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// Os carros que circulam pela Woodcreek no Dia 0 (22/11/2026, a etapa 4 do Docs/23: "os carros circulando pelas ruas, em
    /// trilhos e parando diante do jogador e de outro carro, sem a IA de dirigir, que é outra fase"). No palco do Dia 0 (com a
    /// <see cref="WoodcreekDoDiaZero"/>), só no Play:
    ///  - os trilhos são as estradas e as ruas do chão do Lakewood que passam pela vila (<see cref="ChaoDeLakewood.Estradas"/>),
    ///    cortadas na caixa dela com uma folga; cada carro vai pela faixa da direita até a ponta e volta pela outra;
    ///  - eles param (freando) diante de um jogador, de um veículo de verdade ou de outro carro na mesma faixa, e seguem
    ///    quando a frente fica livre;
    ///  - cada máquina anda os seus (não vão pela rede: os jogadores e os veículos que fazem parar são os da rede, então todos
    ///    veem quase o mesmo). Têm colisão (não se atravessa um carro parado) e o som do motor.
    /// </summary>
    public class CarrosDaWoodcreek : MonoBehaviour
    {
        [Tooltip("Quantos carros circulam pela vila.")]
        public int quantos = 9;
        [Tooltip("A folga em volta da caixa da vila em que os trilhos continuam (m).")]
        public float folga = 90f;

        sealed class Trilho
        {
            public Vector3[] Pontos;   // no mundo, no meio da estrada
            public float[] AoLongo;
            public float Comprimento, MeiaLargura;
        }

        sealed class CarroDoTrilho
        {
            public Transform T;
            public Trilho Trilho;
            public float S, Vel, Querida;
            public int Sentido;
        }

        readonly List<Trilho> _trilhos = new List<Trilho>();
        readonly List<CarroDoTrilho> _carros = new List<CarroDoTrilho>();
        bool _feito;

        const float Freia = 7.5f, ParaEm = 4.2f, Acelera = 2.2f;

        void Update()
        {
            if (!Application.isPlaying) return;
            var rua = GetComponent<RuaDoDiaZero>();
            if (!_feito && rua != null && rua.Pronta)
            {
                _feito = true;
                Criar(rua);
            }
            if (_carros.Count > 0) Andar(Time.deltaTime);
        }

        void OnDestroy()
        {
            foreach (var c in _carros) if (c.T != null) Destroy(c.T.gameObject);
        }

        void Criar(RuaDoDiaZero rua)
        {
            var lw = LakewoodNaCena.Instancia;
            if (lw == null || rua.carros == null || rua.carros.Length == 0) return;
            var chao = lw.ChaoNoMapa;
            // a caixa da vila: os lotes dela, com a folga
            string cidade = WoodcreekDoDiaZero.Atual != null && WoodcreekDoDiaZero.Atual.LoteDaFamilia != null ? WoodcreekDoDiaZero.Atual.LoteDaFamilia.Lote.cidade : "Woodcreek";
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var l in chao.Lotes)
            {
                if (l.Lote.cidade != cidade || l.Lote.tipo == UsoDoLote.Cabana || l.Lote.tipo == UsoDoLote.Acampamento) continue;
                min = Vector2.Min(min, l.Lote.meio);
                max = Vector2.Max(max, l.Lote.meio);
            }
            if (min.x > max.x) return;
            min -= Vector2.one * folga;
            max += Vector2.one * folga;
            bool Dentro(Vector2 p) => p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y;
            // os trilhos: cada trecho seguido de uma estrada dentro da caixa (os de mais de 120 m)
            foreach (var e in chao.Estradas)
            {
                if (e.DaUsina || e.Pontos == null) continue;
                var atual = new List<Vector3>();
                for (int i = 0; i <= e.Pontos.Length; i++)
                {
                    bool dentro = i < e.Pontos.Length && Dentro(e.Pontos[i]) && !(e.Ponte != null && e.Ponte[i]);
                    if (dentro)
                    {
                        var p = e.Pontos[i];
                        atual.Add(OrigemDeLakewood.NoMundo(new Vector3(p.x, e.Alturas != null ? e.Alturas[i] : chao.Altura(p.x, p.y), p.y)));
                        continue;
                    }
                    if (atual.Count >= 2) NovoTrilho(atual, e.MeiaLargura);
                    atual = new List<Vector3>();
                }
            }
            if (_trilhos.Count == 0) return;
            // os carros pelos trilhos, pelo comprimento de cada um (o mesmo sorteio em todas as máquinas)
            float total = 0f;
            foreach (var t in _trilhos) total += t.Comprimento;
            var rnd = new System.Random(4321);
            var pai = new GameObject("Carros da vila").transform;
            pai.SetParent(transform, false);
            for (int i = 0; i < quantos; i++)
            {
                float u = (float)rnd.NextDouble() * total;
                Trilho tr = _trilhos[0];
                foreach (var t in _trilhos)
                {
                    if (u <= t.Comprimento) { tr = t; break; }
                    u -= t.Comprimento;
                }
                var malha = rua.carros[(i * 3 + 1) % rua.carros.Length];
                var go = new GameObject($"Carro da vila {i + 1}");
                go.transform.SetParent(pai, false);
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                go.AddComponent<MeshRenderer>().sharedMaterial = rua.material;
                var corpo = go.AddComponent<Rigidbody>();
                corpo.isKinematic = true;
                corpo.interpolation = RigidbodyInterpolation.None;
                var caixa = go.AddComponent<BoxCollider>();
                caixa.center = malha.bounds.center;
                caixa.size = malha.bounds.size;
                var som = go.AddComponent<AudioSource>();
                som.clip = SonsDoDiaZero.MotorDoCarro;
                som.loop = true;
                som.spatialBlend = 1f;
                som.rolloffMode = AudioRolloffMode.Linear;
                som.minDistance = 3f;
                som.maxDistance = 45f;
                Combat.MixerDeSom.NoCanal(som, Combat.CanalDoSom.Veiculos, 0.5f);
                som.pitch = 0.9f + 0.03f * (i % 5);
                if (som.clip != null)
                {
                    som.time = i * 0.3f % Mathf.Max(0.1f, som.clip.length);
                    som.Play();
                }
                float vel = 8.5f + (float)rnd.NextDouble() * 3f;
                _carros.Add(new CarroDoTrilho { T = go.transform, Trilho = tr, S = u, Vel = vel, Querida = vel, Sentido = rnd.Next(2) == 0 ? 1 : -1 });
            }
            Debug.Log($"[Dia 0] Os carros da vila: {_carros.Count} em {_trilhos.Count} trilhos ({total:F0} m).");
        }

        void NovoTrilho(List<Vector3> pts, float meiaLargura)
        {
            var ao = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) ao[i] = ao[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            if (ao[pts.Count - 1] < 120f) return;
            _trilhos.Add(new Trilho { Pontos = pts.ToArray(), AoLongo = ao, Comprimento = ao[pts.Count - 1], MeiaLargura = Mathf.Max(2.5f, meiaLargura) });
        }

        // o ponto do trilho (no meio da estrada) e a direção, na distância s do começo
        static (Vector3 p, Vector3 dir) No(Trilho t, float s)
        {
            s = Mathf.Clamp(s, 0f, t.Comprimento);
            int i = System.Array.BinarySearch(t.AoLongo, s);
            if (i < 0) i = ~i - 1;
            i = Mathf.Clamp(i, 0, t.Pontos.Length - 2);
            float seg = t.AoLongo[i + 1] - t.AoLongo[i];
            float u = seg > 1e-4f ? (s - t.AoLongo[i]) / seg : 0f;
            var dir = t.Pontos[i + 1] - t.Pontos[i];
            return (Vector3.Lerp(t.Pontos[i], t.Pontos[i + 1], u), dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward);
        }

        // o lugar do carro: na faixa da direita do sentido dele
        Vector3 Lugar(CarroDoTrilho c, out Vector3 frente)
        {
            var (p, dir) = No(c.Trilho, c.S);
            frente = dir * c.Sentido;
            var direita = Vector3.Cross(Vector3.up, frente).normalized;
            return p + direita * (c.Trilho.MeiaLargura * 0.5f) + Vector3.up * 0.03f;
        }

        readonly List<Vector3> _obstaculos = new List<Vector3>();

        void Andar(float dt)
        {
            // o que faz parar: os jogadores, os veículos de verdade e os outros carros
            _obstaculos.Clear();
            foreach (var j in JogadorRede.Todos) if (j != null) _obstaculos.Add(j.transform.position);
            foreach (var v in Carro.Todos) if (v != null) _obstaculos.Add(v.transform.position);
            foreach (var c in _carros)
            {
                var aqui = Lugar(c, out var frente);
                // a frente livre: nada na faixa (2 m para cada lado) a menos de 'Freia' m
                float livre = float.MaxValue;
                foreach (var o in _obstaculos) livre = Mathf.Min(livre, NaFrente(aqui, frente, o));
                foreach (var outro in _carros)
                {
                    if (outro == c || outro.T == null) continue;
                    livre = Mathf.Min(livre, NaFrente(aqui, frente, outro.T.position));
                }
                float quer = livre < ParaEm ? 0f : livre < Freia ? c.Querida * (livre - ParaEm) / (Freia - ParaEm) : c.Querida;
                c.Vel = Mathf.MoveTowards(c.Vel, quer, (quer < c.Vel ? 9f : Acelera) * dt);
                c.S += c.Vel * dt * c.Sentido;
                // a ponta do trilho: volta pela outra faixa
                if (c.S > c.Trilho.Comprimento) { c.S = c.Trilho.Comprimento; c.Sentido = -1; }
                else if (c.S < 0f) { c.S = 0f; c.Sentido = 1; }
                var p = Lugar(c, out frente);
                c.T.SetPositionAndRotation(p, Quaternion.LookRotation(frente, Vector3.up));
            }
        }

        // a distância à frente do carro até o ponto, se ele está na faixa (2 m para cada lado e até 3 m de altura); senão, longe
        static float NaFrente(Vector3 de, Vector3 frente, Vector3 o)
        {
            var d = o - de;
            if (Mathf.Abs(d.y) > 3f) return float.MaxValue;
            d.y = 0f;
            float aoLongo = Vector3.Dot(d, frente);
            if (aoLongo < 0.5f) return float.MaxValue;
            float aoLado = Vector3.Cross(frente, d).magnitude;
            return aoLado < 2.0f ? aoLongo - 2.4f : float.MaxValue;
        }
    }
}
