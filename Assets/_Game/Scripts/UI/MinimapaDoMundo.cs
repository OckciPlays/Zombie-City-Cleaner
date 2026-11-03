using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O minimapa (03/11/2026, a pedido: "um minimapa com os jogadores"), posto pelo <see cref="MapaDoMundo"/> no objeto
    /// "Mundo", desenhado com a mesma imagem do mapa:
    ///  - no canto de cima, à direita; quadrado, com o norte para cima; mostra uns 250 m em volta (o grande, 400 m);
    ///  - as setas dos jogadores (você em ciano, no meio), os pings e as marcas (sem os nomes: eles ficam no mapa grande);
    ///  - a tecla N muda o tamanho: pequeno, grande ou escondido (lembrado no computador);
    ///  - some nas telas cheias (o inventário, o mapa, o menu, o cinema, a tela de morte) e no Dia 0 (sem mapa ainda).
    /// Aqui também (no dono): o ping no mundo, no botão do meio do mouse (o ponto para onde se olha, até 300 m), e o
    /// losango de cada ping por cima de tudo, com o nome de quem marcou e a distância; o bipe quando chega um.
    /// </summary>
    public class MinimapaDoMundo : MonoBehaviour
    {
        const string Chave = "minimapa.tamanho";
        static readonly float[] Lados = { 170f, 270f };
        static readonly float[] Metros = { 250f, 400f };

        /// <summary>0 pequeno, 1 grande, 2 escondido.</summary>
        public static int Tamanho { get; private set; }

        MapaDoMundo _mapa;
        MundoDaPartida _mundoOuvido;
        float _proximoPing;
        GUIStyle _rotulo, _norte;

        void Awake()
        {
            _mapa = GetComponent<MapaDoMundo>();
            try { Tamanho = Mathf.Clamp(PlayerPrefs.GetInt(Chave, 0), 0, 2); }
            catch { Tamanho = 0; }
        }

        void OnDisable() => Ouvir(null);

        // o bipe dos pings que chegam (da partida de agora)
        void Ouvir(MundoDaPartida m)
        {
            if (_mundoOuvido == m) return;
            if (_mundoOuvido != null) _mundoOuvido.MarcaNova -= AoChegarMarca;
            _mundoOuvido = m;
            if (m != null) m.MarcaNova += AoChegarMarca;
        }

        void AoChegarMarca(MarcaNoMapa m)
        {
            if (m.tipo != TipoDeMarca.Ping) return;
            var cam = Camera.main;
            Efeitos.Som(MarcasDoMapa.Bipe, cam != null ? cam.transform.position : Vector3.zero, 0.55f, false);
        }

        void Update()
        {
            var mundo = MundoDaPartida.Instancia;
            Ouvir(mundo != null && mundo.IsSpawned ? mundo : null);
            if (mundo != null && mundo.IsSpawned && mundo.IsServer) mundo.LimparOsPings();
            var local = JogadorRede.Local;
            if (local == null || mundo == null || mundo.Dados == null || !MenuOnline.Controla) return;
            var teclado = Keyboard.current;
            if (teclado != null && teclado.nKey.wasPressedThisFrame)
            {
                Tamanho = (Tamanho + 1) % 3;
                try { PlayerPrefs.SetInt(Chave, Tamanho); }
                catch { }
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.middleButton.wasPressedThisFrame && Time.unscaledTime >= _proximoPing && OndeOlha(local, mundo.Dados, out var ponto))
            {
                _proximoPing = Time.unscaledTime + 0.4f;
                local.PedirPing(ponto);
            }
        }

        // o ponto para onde o jogador olha: o que o raio pega (sem o próprio corpo) até 300 m; sem nada (o chão longe ainda
        // sem colisor), o relevo dos dados, andando pelo raio
        static bool OndeOlha(JogadorRede j, Mundo.DadosDoMundo d, out Vector3 ponto)
        {
            var olho = j.Olho;
            Vector3 o = olho.position, dir = olho.forward;
            int mascara = ~((1 << 2) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos) | (1 << JogadorRede.CamadaRetrato));
            if (Physics.Raycast(o, dir, out var hit, MarcasDoMapa.AlcanceDoPing, mascara, QueryTriggerInteraction.Ignore))
            {
                ponto = hit.point;
                return true;
            }
            for (float t = 2f; t <= MarcasDoMapa.AlcanceDoPing; t += 2f)
            {
                var q = o + dir * t;
                if (q.y > d.Altura(q.x, q.z)) continue;
                ponto = new Vector3(q.x, d.Altura(q.x, q.z), q.z);
                return true;
            }
            ponto = default;
            return false;
        }

        bool TelaCheia => MenuOnline.Aberto || MenuOnline.ComTela || MenuOnline.Cinema || ModoVideo.Ligado || TelaDeMorte.Aberta
                          || (_mapa != null && _mapa.Aberto);

        void Estilos(float u)
        {
            int t = Mathf.RoundToInt(12 * u);
            if (_rotulo != null && _rotulo.fontSize == t) return;
            _rotulo = new GUIStyle(GUI.skin.label) { fontSize = t, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, clipping = TextClipping.Overflow };
            _rotulo.normal.textColor = Color.white;
            _norte = new GUIStyle(_rotulo) { fontSize = Mathf.RoundToInt(13 * u) };
        }

        void OnGUI()
        {
            var local = JogadorRede.Local;
            var mundo = MundoDaPartida.Instancia;
            if (local == null || mundo == null || !mundo.IsSpawned || mundo.Dados == null || TelaCheia) return;
            float u = Screen.height / 720f;
            Estilos(u);
            PingsNoMundo(local, mundo, u);
            if (Tamanho < 2 && _mapa != null && _mapa.Textura != null) Minimapa(local, mundo, u);
        }

        // o losango de cada ping, por cima de tudo, com quem marcou e a distância (some nos 3 s finais)
        void PingsNoMundo(JogadorRede local, MundoDaPartida mundo, float u)
        {
            var lista = mundo.Marcas;
            var cam = local.cameraMundo != null ? local.cameraMundo : Camera.main;
            if (lista == null || cam == null) return;
            double agora = mundo.Agora;
            var antes = GUI.color;
            foreach (var m in lista)
            {
                if (m.tipo != TipoDeMarca.Ping) continue;
                var tela = cam.WorldToScreenPoint(m.pos + Vector3.up * 0.6f);
                if (tela.z <= 0.1f) continue;
                float x = Mathf.Clamp(tela.x, 20 * u, Screen.width - 20 * u);
                float y = Mathf.Clamp(Screen.height - tela.y, 20 * u, Screen.height - 40 * u);
                float alfa = Mathf.Clamp01((float)(m.ate - agora) / 3f);
                float s = 22f * u;
                var cor = MarcasDoMapa.CorDoPing(m.dono);
                GUI.color = new Color(cor.r, cor.g, cor.b, alfa);
                GUI.DrawTexture(new Rect(x - s / 2f, y - s / 2f, s, s), MarcasDoMapa.Losango);
                float distancia = Vector3.Distance(local.transform.position, m.pos);
                GUI.color = new Color(0f, 0f, 0f, 0.75f * alfa);
                string texto = $"{m.autor} · {distancia:0} m";
                GUI.Label(new Rect(x - 100 * u + 1, y + s / 2f + 1, 200 * u, 18 * u), texto, _rotulo);
                GUI.color = new Color(1f, 1f, 1f, alfa);
                GUI.Label(new Rect(x - 100 * u, y + s / 2f, 200 * u, 18 * u), texto, _rotulo);
            }
            GUI.color = antes;
        }

        void Minimapa(JogadorRede local, MundoDaPartida mundo, float u)
        {
            var d = mundo.Dados;
            float lado = Lados[Tamanho] * u, metros = Metros[Tamanho];
            var area = new Rect(Screen.width - lado - 14 * u, 30 * u, lado, lado);
            var antes = GUI.color;
            // a moldura e a imagem (o pedaço em volta do jogador; o norte, o +z, para cima)
            GUI.color = new Color(0.05f, 0.05f, 0.045f, 0.85f);
            GUI.DrawTexture(new Rect(area.x - 3 * u, area.y - 3 * u, area.width + 6 * u, area.height + 6 * u), Texture2D.whiteTexture);
            GUI.color = antes;
            var p = local.transform.position;
            float L = d.Lado, M = d.Metade, w = metros / L;
            GUI.DrawTextureWithTexCoords(area, _mapa.Textura, new Rect((p.x + M) / L - w / 2f, (p.z + M) / L - w / 2f, w, w));
            float escala = lado / metros;
            GUI.BeginGroup(area);
            var meio = new Vector2(lado / 2f, lado / 2f);
            Vector2 NoMini(Vector3 mundoPos) => meio + new Vector2(mundoPos.x - p.x, -(mundoPos.z - p.z)) * escala;
            // as marcas e os pings
            var lista = mundo.Marcas;
            if (lista != null)
            {
                double agora = mundo.Agora;
                foreach (var m in lista)
                {
                    var q = NoMini(m.pos);
                    if (q.x < -10 || q.y < -10 || q.x > lado + 10 || q.y > lado + 10) continue;
                    var cor = MarcasDoMapa.CorDa(m);
                    if (m.tipo == TipoDeMarca.Ping)
                    {
                        float s = 14f * u * (1f + 0.15f * Mathf.Sin(Time.unscaledTime * 8f));
                        GUI.color = new Color(cor.r, cor.g, cor.b, Mathf.Clamp01((float)(m.ate - agora) / 3f));
                        GUI.DrawTexture(new Rect(q.x - s / 2f, q.y - s / 2f, s, s), MarcasDoMapa.Losango);
                    }
                    else
                    {
                        float s = 16f * u;
                        GUI.color = cor;
                        GUI.DrawTexture(new Rect(q.x - s / 2f, q.y - s * 0.95f, s, s), MarcasDoMapa.Alfinete);
                    }
                }
                GUI.color = antes;
            }
            // os outros jogadores e você (no meio)
            var seta = MapaDoMundo.SetaDosJogadores;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || j == local) continue;
                var q = NoMini(j.transform.position);
                if (q.x < 0 || q.y < 0 || q.x > lado || q.y > lado) continue;
                Seta(seta, q, j.transform.forward, 14f * u, new Color(0.95f, 0.95f, 0.95f));
            }
            Seta(seta, meio, local.transform.forward, 18f * u, new Color(0.25f, 0.9f, 1f));
            GUI.EndGroup();
            // o norte
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(area.x + 1, area.y + 2 * u + 1, area.width, 18 * u), "N", _norte);
            GUI.color = antes;
            GUI.Label(new Rect(area.x, area.y + 2 * u, area.width, 18 * u), "N", _norte);
        }

        static void Seta(Texture2D seta, Vector2 p, Vector3 frente, float lado, Color cor)
        {
            float giro = Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg;
            var matriz = GUI.matrix;
            var antes = GUI.color;
            GUIUtility.RotateAroundPivot(giro, p);
            GUI.color = cor;
            GUI.DrawTexture(new Rect(p.x - lado / 2f, p.y - lado / 2f, lado, lado), seta);
            GUI.color = antes;
            GUI.matrix = matriz;
        }
    }
}
