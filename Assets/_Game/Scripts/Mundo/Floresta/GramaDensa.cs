using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O chão de onde a <see cref="GramaDensa"/> tira os tufos: a cena de floresta (<see cref="FlorestaDeTeste"/>) e o
    /// Lakewood (<see cref="ChaoDeLakewood"/>, desde 09/11/2026).
    /// </summary>
    public interface IChaoDaGrama
    {
        /// <summary>A semente do sorteio dos tufos.</summary>
        int Semente { get; }
        /// <summary>Onde há chão (x e z): fora dele, nenhum tufo.</summary>
        Rect Limites { get; }
        /// <summary>Se as funções podem ser chamadas fora da thread principal (as células são montadas em outra thread).</summary>
        bool SeguroEmThreads { get; }
        /// <summary>A densidade (0 a 1) num ponto: a chance de cada candidato virar um tufo.</summary>
        float Grama(float x, float z);
        /// <summary>A clareira (0 mata fechada, 1 aberta): os tufos são um pouco maiores no aberto.</summary>
        float Clareira(float x, float z);
        /// <summary>A altura do chão.</summary>
        float Altura(float x, float z);
    }

    /// <summary>
    /// A grama densa da cena de floresta (o Plano_Floresta.md, "A grama densa", 09/11/2026): os tufos (a malha
    /// GramaTufo, três cartas cruzadas com as folhas do atlas) desenhados em instâncias (Graphics.RenderMeshInstanced, sem
    /// um objeto por tufo) até <see cref="alcance"/> m da câmera, em células de 8 m sorteadas pela semente quando a
    /// câmera chega perto (e esquecidas quando se afasta). A densidade vem da <see cref="FlorestaDeTeste.Grama"/> (cheia
    /// nas clareiras, rala sob a mata, nada no riacho), e a altura dos tufos é maior nas clareiras. No fim do alcance os
    /// tufos encolhem para o pé (o _Some do shader), e quem passa dobra a grama (o _ZccDobradores do Vegetacao.shader: o
    /// pé do jogador e o rastro dele, que volta aos poucos). Sem sombra (como a grama do jogo). O chão é o
    /// <see cref="IChaoDaGrama"/> do objeto (a FlorestaDeTeste) ou o posto em <see cref="Chao"/> (o Lakewood, que a
    /// põe na hora); com um chão seguro em várias threads, as células novas são montadas fora da principal.
    /// O anel de longe (10/11/2026, para a grama parecer cobrir tudo sem pesar): depois do <see cref="alcance"/>, até
    /// <see cref="alcanceLonge"/>, uma fração dos tufos, um pouco maiores, só onde o chão tem a grama cheia (o campo e as
    /// clareiras; sob a mata, as árvores tapam a borda), crescendo dentro do fim do de perto enquanto ele encolhe. E os
    /// tufos vão para a cor média do chão (o _Desbota do shader) da metade do alcance até o fim: a borda some no chão.
    /// </summary>
    [ExecuteAlways]
    public class GramaDensa : MonoBehaviour
    {
        public Mesh malha;
        public Material material;
        [Tooltip("Até onde a grama aparece (m).")] public float alcance = 45f;
        [Tooltip("Tufos por m² nas clareiras.")] public float densidade = 4f;
        [Tooltip("A escala dos tufos (de, até), sorteada por tufo; nas clareiras um pouco maior.")] public Vector2 escala = new Vector2(0.7f, 1.25f);
        [Tooltip("O raio em volta do pé do jogador em que a grama dobra (m).")] public float raioDoJogador = 0.8f;
        [Tooltip("Por quanto tempo a grama fica dobrada atrás de quem passou (s).")] public float rastro = 1.2f;
        [Tooltip("Mostra os tufos e as células no canto da tela (no Play).")] public bool resumoNaTela = true;
        [Header("O anel de longe (0 desliga)")]
        [Tooltip("Até onde vão os tufos ralos depois do alcance (m); 0 desliga.")] public float alcanceLonge = 0f;
        [Tooltip("A fração dos tufos no anel de longe.")] [Range(0f, 1f)] public float fracaoLonge = 0.2f;
        [Tooltip("Só onde a densidade do chão chega a isto (o campo e as clareiras).")] [Range(0f, 1f)] public float minimoLonge = 0.45f;
        [Tooltip("A escala dos tufos de longe (vezes a de perto).")] public float escalaLonge = 1.25f;

        const float Celula = 8f;
        const int MaxDobradores = 24, PontosDoRastro = 4, MaxMontando = 12;

        sealed class Cel
        {
            public List<Matrix4x4> Matrizes;
            public Bounds Caixa;
        }

        // uma faixa da grama: as células montadas, as que estão sendo montadas, a lista do quadro e o bloco do desenho
        sealed class Faixa
        {
            public readonly Dictionary<long, Cel> Celulas = new Dictionary<long, Cel>();
            public readonly Dictionary<long, Task<Cel>> Montando = new Dictionary<long, Task<Cel>>();
            public readonly List<Matrix4x4> Lista = new List<Matrix4x4>(16384);
            public MaterialPropertyBlock Bloco;
            public bool Longe;
            // as células da faixa com tufos (revistas quando a câmera anda 2 m, quando chega uma célula nova ou a cada meio
            // segundo: percorrer as ~600 células do anel de longe a cada quadro custava uns 0,5 ms de CPU)
            public readonly List<Cel> Ativas = new List<Cel>();
            public Vector3 Onde = new Vector3(1e9f, 0f, 0f);
            public float Quando = -1f;
            public bool Mudou = true;

            public void Limpar()
            {
                Celulas.Clear();
                Montando.Clear();
                Lista.Clear();
                Ativas.Clear();
                Mudou = true;
            }
        }

        readonly Faixa _perto = new Faixa(), _longe = new Faixa { Longe = true };
        readonly List<long> _prontas = new List<long>();
        readonly List<long> _sobra = new List<long>();
        readonly Vector4[] _dobradores = new Vector4[MaxDobradores];
        readonly Vector3[] _pontos = new Vector3[PontosDoRastro];
        readonly float[] _quando = new float[PontosDoRastro];
        int _proximoPonto;
        float _ultimoPonto;
        IChaoDaGrama _chao;
        readonly Plane[] _planos = new Plane[6];
        [System.NonSerialized] GUIStyle _estilo;
        static readonly int SomeId = Shader.PropertyToID("_Some");
        static readonly int ApareceId = Shader.PropertyToID("_Aparece");
        static readonly int DesbotaId = Shader.PropertyToID("_Desbota");
        static readonly int DobradoresId = Shader.PropertyToID("_ZccDobradores");
        static readonly int QuantosDobradoresId = Shader.PropertyToID("_ZccDobradoresN");

        /// <summary>Quantos tufos foram desenhados no último quadro.</summary>
        public int Tufos { get; private set; }
        public int Celulas => _perto.Celulas.Count + _longe.Celulas.Count;
        /// <summary>Quantos tufos do anel de longe foram desenhados no último quadro.</summary>
        public int TufosLonge { get; private set; }

        /// <summary>O chão dos tufos (sem pôr, o do próprio objeto).</summary>
        public IChaoDaGrama Chao
        {
            get => _chao;
            set
            {
                _chao = value;
                _perto.Limpar();
                _longe.Limpar();
            }
        }

        void OnEnable()
        {
            if (_chao == null) _chao = GetComponent<IChaoDaGrama>();
            _perto.Limpar();
            _longe.Limpar();
        }

        void OnDisable()
        {
            _perto.Limpar();
            _longe.Limpar();
            if (Application.isPlaying) Shader.SetGlobalFloat(QuantosDobradoresId, 0f);
        }

        static long Chave(int cx, int cz) => ((long)(cx + 1000000) << 32) | (uint)(cz + 1000000);

        void Update()
        {
            if (malha == null || material == null || _chao == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            // (no jogo, o alcance cai na Média e na Baixa, UI.Graficos.Grama, e o anel de longe só vem na Alta e na Ultra)
            bool comLonge = alcanceLonge > alcance + Celula;
            float fim = comLonge ? alcanceLonge : alcance;
            GeometryUtility.CalculateFrustumPlanes(cam, _planos);
            var limites = _chao.Limites;
            Juntar(_perto, c, 0f, alcance, limites);
            if (comLonge) Juntar(_longe, c, alcance * 0.7f, alcanceLonge, limites);
            else _longe.Limpar();
            Dobradores(cam);
            Tufos = _perto.Lista.Count + _longe.Lista.Count;
            TufosLonge = _longe.Lista.Count;
            // o de perto encolhe no último quarto; o de longe cresce dentro desse fim e encolhe no fim dele; os dois vão
            // para a cor do chão da metade do alcance até perto do fim de tudo
            var desbota = new Vector4(alcance * 0.45f, fim * 0.92f, 0f, 0f);
            Desenhar(_perto, cam, c, fim, new Vector4(alcance * 0.72f, alcance, 0f, 0f), Vector4.zero, desbota);
            if (comLonge) Desenhar(_longe, cam, c, fim, new Vector4(alcanceLonge * 0.75f, alcanceLonge, 0f, 0f), new Vector4(alcance * 0.7f, alcance * 0.95f, 0f, 0f), desbota);
        }

        // as células de uma faixa (de "de" a "ate" metros da câmera) na lista do quadro; as prontas em outra thread
        // entram, e as que ficaram fora da faixa saem
        void Juntar(Faixa f, Vector3 c, float de, float ate, Rect limites)
        {
            f.Lista.Clear();
            if (f.Montando.Count > 0)
            {
                _prontas.Clear();
                foreach (var par in f.Montando) if (par.Value.IsCompleted) _prontas.Add(par.Key);
                foreach (var k in _prontas)
                {
                    var t = f.Montando[k];
                    f.Montando.Remove(k);
                    if (t.IsFaulted) Debug.LogException(t.Exception);
                    else f.Celulas[k] = t.Result;
                    f.Mudou = true;
                }
            }
            float agora = Time.realtimeSinceStartup;
            if (f.Mudou || (c - f.Onde).sqrMagnitude > 4f || agora - f.Quando > 0.5f)
            {
                f.Mudou = false;
                f.Onde = c;
                f.Quando = agora;
                Rever(f, c, de, ate, limites);
            }
            // por quadro: só o teste da vista nas células com tufos
            foreach (var cel in f.Ativas)
                if (GeometryUtility.TestPlanesAABB(_planos, cel.Caixa)) f.Lista.AddRange(cel.Matrizes);
        }

        // as células da faixa: as que faltam começam a ser montadas, as com tufos vão para as ativas, e as que ficaram fora
        // da faixa saem
        void Rever(Faixa f, Vector3 c, float de, float ate, Rect limites)
        {
            f.Ativas.Clear();
            float raio = ate + Celula;
            int cx0 = Mathf.FloorToInt((c.x - raio) / Celula), cx1 = Mathf.FloorToInt((c.x + raio) / Celula);
            int cz0 = Mathf.FloorToInt((c.z - raio) / Celula), cz1 = Mathf.FloorToInt((c.z + raio) / Celula);
            for (int cz = cz0; cz <= cz1; cz++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                float x0 = cx * Celula, z0 = cz * Celula;
                if (x0 + Celula < limites.xMin || x0 > limites.xMax || z0 + Celula < limites.yMin || z0 > limites.yMax) continue;
                // a distância da câmera à célula (no plano): a mais perto e a mais longe
                float dx = Mathf.Max(x0 - c.x, 0f, c.x - x0 - Celula), dz = Mathf.Max(z0 - c.z, 0f, c.z - z0 - Celula);
                if (dx * dx + dz * dz > ate * ate) continue;
                if (de > 0f)
                {
                    float fx = Mathf.Max(Mathf.Abs(x0 - c.x), Mathf.Abs(x0 + Celula - c.x)), fz = Mathf.Max(Mathf.Abs(z0 - c.z), Mathf.Abs(z0 + Celula - c.z));
                    if (fx * fx + fz * fz < de * de) continue;   // a célula inteira no miolo (a faixa de perto)
                }
                long chave = Chave(cx, cz);
                if (!f.Celulas.TryGetValue(chave, out var cel))
                {
                    bool longe = f.Longe;
                    if (!_chao.SeguroEmThreads) f.Celulas[chave] = cel = Montar(_chao, cx, cz, longe);
                    else
                    {
                        // em outra thread (até 12 ao mesmo tempo por faixa); a célula aparece quando ficar pronta
                        if (!f.Montando.ContainsKey(chave) && f.Montando.Count < MaxMontando)
                        {
                            var chao = _chao;
                            int a = cx, b = cz;
                            f.Montando[chave] = Task.Run(() => Montar(chao, a, b, longe));
                        }
                        continue;
                    }
                }
                if (cel.Matrizes.Count > 0) f.Ativas.Add(cel);
            }
            // as células que ficaram longe (ou, no anel, dentro do miolo) saem
            float sai = raio + 2f * Celula, dentro = de - 2f * Celula;
            _sobra.Clear();
            foreach (var par in f.Celulas)
            {
                var caixa = par.Value.Caixa;
                float dx = Mathf.Max(caixa.min.x - c.x, 0f, c.x - caixa.max.x), dz = Mathf.Max(caixa.min.z - c.z, 0f, c.z - caixa.max.z);
                if (dx * dx + dz * dz > sai * sai) { _sobra.Add(par.Key); continue; }
                if (dentro > 0f)
                {
                    float fx = Mathf.Max(Mathf.Abs(caixa.min.x - c.x), Mathf.Abs(caixa.max.x - c.x)), fz = Mathf.Max(Mathf.Abs(caixa.min.z - c.z), Mathf.Abs(caixa.max.z - c.z));
                    if (fx * fx + fz * fz < dentro * dentro) _sobra.Add(par.Key);
                }
            }
            foreach (var k in _sobra) f.Celulas.Remove(k);
        }

        void Desenhar(Faixa f, Camera cam, Vector3 c, float fim, Vector4 some, Vector4 aparece, Vector4 desbota)
        {
            int n = f.Lista.Count;
            if (n == 0) return;
            if (f.Bloco == null) f.Bloco = new MaterialPropertyBlock();
            f.Bloco.SetVector(SomeId, some);
            f.Bloco.SetVector(ApareceId, aparece);
            f.Bloco.SetVector(DesbotaId, desbota);
            var rp = new RenderParams(material)
            {
                layer = 0,
                receiveShadows = true,
                shadowCastingMode = ShadowCastingMode.Off,
                worldBounds = new Bounds(c, Vector3.one * (fim * 2f + 20f)),
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                matProps = f.Bloco,
                // só na câmera do jogador: no reflexo da água ela quase não aparece e custava o dobro das instâncias (no
                // Editor parado, em todas: a vista da cena também mostra a grama)
                camera = Application.isPlaying ? cam : null,
            };
            for (int ini = 0; ini < n; ini += 1023)
                Graphics.RenderMeshInstanced(rp, malha, 0, f.Lista, Mathf.Min(1023, n - ini), ini);
        }

        // os tufos de uma célula, sorteados pela semente e pela célula (sempre os mesmos)
        // (no anel de longe: outro sorteio, a fração dos candidatos, só onde a grama é cheia e os tufos maiores)
        Cel Montar(IChaoDaGrama chao, int cx, int cz, bool longe = false)
        {
            var cel = new Cel { Matrizes = new List<Matrix4x4>() };
            var rnd = new System.Random(chao.Semente * 7919 + cx * 1301 + cz * 977 + (longe ? 54321 : 12345));
            float Entre(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            float x0 = cx * Celula, z0 = cz * Celula;
            int candidatos = Mathf.RoundToInt(Celula * Celula * Mathf.Max(densidade, 0f) * (longe ? fracaoLonge : 1f));
            float minimo = longe ? minimoLonge : 0f, maior = longe ? escalaLonge : 1f;
            var limites = chao.Limites;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < candidatos; i++)
            {
                float x = x0 + Entre(0f, Celula), z = z0 + Entre(0f, Celula);
                if (!limites.Contains(new Vector2(x, z))) continue;
                float grama = chao.Grama(x, z);
                if (rnd.NextDouble() > grama || grama < minimo) continue;
                float clareira = chao.Clareira(x, z);
                float s = Entre(escala.x, escala.y) * Mathf.Lerp(0.8f, 1.1f, clareira) * maior;
                var pos = new Vector3(x, chao.Altura(x, z) - 0.03f, z);
                var rot = Quaternion.Euler(Entre(-7f, 7f), Entre(0f, 360f), Entre(-7f, 7f));
                cel.Matrizes.Add(Matrix4x4.TRS(pos, rot, new Vector3(s, s * Entre(0.9f, 1.1f), s)));
                min = Vector3.Min(min, pos);
                max = Vector3.Max(max, pos);
            }
            if (cel.Matrizes.Count > 0)
            {
                min -= new Vector3(0.6f, 0.1f, 0.6f);
                max += new Vector3(0.6f, 1.0f, 0.6f);
                cel.Caixa = new Bounds((min + max) * 0.5f, max - min);
            }
            else cel.Caixa = new Bounds(new Vector3(x0 + Celula * 0.5f, 0f, z0 + Celula * 0.5f), new Vector3(Celula, 1f, Celula));
            return cel;
        }

        // quem dobra a grama: o pé do jogador (a raiz da câmera) e o rastro dele (um ponto a cada 0,2 s, o raio
        // diminuindo até o fim do rastro), nas globais do shader. No jogo, o FlorestaDoMundo faz isso.
        void Dobradores(Camera cam)
        {
            if (!Application.isPlaying) return;
            var pe = cam.transform.root.position;
            float agora = Time.time;
            if (agora - _ultimoPonto > 0.2f)
            {
                _pontos[_proximoPonto] = pe;
                _quando[_proximoPonto] = agora;
                _proximoPonto = (_proximoPonto + 1) % PontosDoRastro;
                _ultimoPonto = agora;
            }
            int n = 0;
            _dobradores[n++] = new Vector4(pe.x, pe.y, pe.z, raioDoJogador);
            for (int k = 0; k < PontosDoRastro; k++)
            {
                float idade = agora - _quando[k];
                if (_quando[k] <= 0f || idade >= rastro) continue;
                var q = _pontos[k];
                _dobradores[n++] = new Vector4(q.x, q.y, q.z, raioDoJogador * (1f - idade / rastro));
            }
            Shader.SetGlobalVectorArray(DobradoresId, _dobradores);
            Shader.SetGlobalFloat(QuantosDobradoresId, n);
        }

        void OnGUI()
        {
            if (!Application.isPlaying || !resumoNaTela) return;
            if (_estilo == null) _estilo = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.UpperRight };
            _estilo.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width - 620, 48, 610, 20), $"Grama densa: {Tufos} tufos na vista ({TufosLonge} no anel de longe), {Celulas} células, até {alcance:0} m" + (alcanceLonge > alcance ? $" e os ralos até {alcanceLonge:0} m" : ""), _estilo);
        }
    }
}
