using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O mapa do mundo gerado (Plano_Mapa, Parte M3), na tecla M (OnGUI, uma tela do jogo: o mouse solta e o jogador para;
    /// ESC ou M fecha). A imagem é pintada em outra thread assim que o mundo fica pronto (<see cref="PintorDoMapa"/>), e os
    /// nomes são escritos por cima: as cidades sempre; os outros lugares e os bairros aparecem ao aproximar, e o que
    /// encostaria num nome já escrito fica de fora. Os jogadores são setas para onde olham (você em ciano). A roda do
    /// mouse aproxima no ponto do mouse, arrastar move, Espaço volta para você. Por enquanto mostra o mapa inteiro: a
    /// descoberta (só onde o grupo passou, e os mapas de papel) vem depois.
    /// Fica no objeto "Mundo" da cena Mundo.
    /// </summary>
    public class MapaDoMundo : MonoBehaviour
    {
        [Tooltip("O lado da imagem do mapa, em pixels (2048: uns 2,5 m por pixel no médio).")]
        public int resolucao = 2048;

        public bool Aberto { get; private set; }

        DadosDoMundo _dados;
        Texture2D _textura;
        Task<Color32[]> _pintando;
        List<PintorDoMapa.Rotulo> _rotulos;
        float _zoom = 1f;
        Vector2 _centro;
        readonly List<Rect> _ocupados = new List<Rect>();
        GUIStyle[] _nomes;
        GUIStyle _titulo, _texto;
        float _uDosEstilos;
        Texture2D _seta;

        // a cor de você no mapa: o ciano não aparece em nada do desenho (o amarelo é o da casa da família)
        static readonly Color CorDeVoce = new Color(0.25f, 0.9f, 1f);

        void OnEnable() => MenuOnline.FecharTelas += Fechar;

        void OnDisable()
        {
            MenuOnline.FecharTelas -= Fechar;
            if (Aberto) Abrir(false);
        }

        void OnDestroy()
        {
            if (_textura != null) Destroy(_textura);
            if (_seta != null) Destroy(_seta);
        }

        void Fechar() => Abrir(false);

        void Abrir(bool abrir)
        {
            Aberto = abrir;
            MenuOnline.Tela(this, abrir);
            if (!abrir || _dados == null) return;
            // abre mostrando o mapa todo
            _zoom = 1f;
            _centro = Vector2.zero;
        }

        void Update()
        {
            var mundo = MundoDaPartida.Instancia;
            var dados = mundo != null ? mundo.Dados : null;
            if (dados != _dados)
            {
                // outro mundo (ou nenhum): a imagem do anterior sai, e a deste é pintada em outra thread
                if (_textura != null) Destroy(_textura);
                _textura = null;
                _dados = dados;
                _pintando = null;
                _rotulos = null;
                if (Aberto) Abrir(false);
                if (dados != null)
                {
                    int lado = resolucao;
                    _pintando = Task.Run(() => PintorDoMapa.Pintar(dados, lado));
                    _rotulos = PintorDoMapa.Rotulos(dados);
                    // os maiores primeiro: na hora de escrever, o que encostaria num já escrito fica de fora
                    _rotulos.Sort((a, b) => a.Nivel.CompareTo(b.Nivel));
                }
            }
            if (_pintando != null && _pintando.IsCompleted)
            {
                if (_pintando.IsFaulted) Debug.LogException(_pintando.Exception);
                else
                {
                    // com mipmaps: no mapa inteiro a imagem fica 2 a 3 vezes menor na tela, e as linhas finas tremeriam
                    _textura = new Texture2D(resolucao, resolucao, TextureFormat.RGBA32, true)
                        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 1, name = "Mapa do mundo" };
                    _textura.SetPixels32(_pintando.Result);
                    _textura.Apply(true, true);
                }
                _pintando = null;
            }

            var teclado = Keyboard.current;
            if (JogadorRede.Local == null)
            {
                if (Aberto) Abrir(false);
                return;
            }
            if (teclado != null && teclado.mKey.wasPressedThisFrame && !MenuOnline.Aberto && _textura != null) Abrir(!Aberto);
            if (!Aberto) return;

            var mouse = Mouse.current;
            float escala = Escala();
            if (mouse != null)
            {
                float rolagem = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(rolagem) > 0.01f)
                {
                    // aproxima no ponto do mouse: o ponto do mundo embaixo dele fica no lugar
                    var tela = mouse.position.ReadValue();
                    var antes = NoMundo(tela, escala);
                    _zoom = Mathf.Clamp(_zoom * Mathf.Pow(1.18f, Mathf.Sign(rolagem)), 1f, 24f);
                    escala = Escala();
                    _centro += antes - NoMundo(tela, escala);
                }
                if (mouse.leftButton.isPressed)
                {
                    var delta = mouse.delta.ReadValue();
                    _centro -= delta / escala;
                }
            }
            if (teclado != null && teclado.spaceKey.wasPressedThisFrame)
            {
                var p = JogadorRede.Local.transform.position;
                _centro = new Vector2(p.x, p.z);
            }
            float limite = _dados.Metade;
            _centro = new Vector2(Mathf.Clamp(_centro.x, -limite, limite), Mathf.Clamp(_centro.y, -limite, limite));
        }

        // pixels por metro: o mapa inteiro cabe na altura da tela com zoom 1
        float Escala() => LadoNaTela() * _zoom / _dados.Lado;

        static float LadoNaTela() => Mathf.Min(Screen.height - 130f * Mathf.Min(Screen.height / 720f, Screen.width / 1280f), Screen.width - 40f);

        // da tela do Input System (origem embaixo) para o mundo (x, z)
        Vector2 NoMundo(Vector2 tela, float escala)
        {
            var meio = new Vector2(Screen.width / 2f, Screen.height / 2f);
            return _centro + (tela - meio) / escala;
        }

        // do mundo (x, z) para a tela do OnGUI (origem em cima)
        Vector2 NaTela(Vector2 mundo, float escala)
        {
            var meio = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var d = (mundo - _centro) * escala;
            return new Vector2(meio.x + d.x, meio.y - d.y);
        }

        void Estilos(float u)
        {
            if (_nomes != null && Mathf.Approximately(_uDosEstilos, u)) return;
            _uDosEstilos = u;
            _nomes = new GUIStyle[4];
            int[] tamanhos = { 21, 15, 12, 11 };
            for (int n = 0; n < 4; n++)
            {
                _nomes[n] = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(tamanhos[n] * u),
                    fontStyle = n <= 1 ? FontStyle.Bold : n == 3 ? FontStyle.Italic : FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Overflow,
                };
                _nomes[n].normal.textColor = n == 3 ? new Color(0.2f, 0.18f, 0.16f) : new Color(0.07f, 0.07f, 0.07f);
            }
            _titulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _titulo.normal.textColor = new Color(0.9f, 0.88f, 0.82f);
            _texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * u), alignment = TextAnchor.MiddleCenter };
            _texto.normal.textColor = new Color(0.75f, 0.74f, 0.7f);
        }

        void OnGUI()
        {
            if (!Aberto || _textura == null || _dados == null || ModoVideo.Ligado) return;
            GUI.depth = -8;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos(u);
            var antes = GUI.color;
            GUI.color = new Color(0.07f, 0.07f, 0.065f, 0.97f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = antes;

            float escala = Escala();
            float lado = LadoNaTela();
            var janela = new Rect((Screen.width - lado) / 2f, (Screen.height - lado) / 2f, lado, lado);
            GUI.BeginGroup(janela);
            Vector2 NaJanela(Vector2 mundo) => NaTela(mundo, escala) - janela.position;
            var cantoA = NaJanela(new Vector2(-_dados.Metade, _dados.Metade));
            var cantoB = NaJanela(new Vector2(_dados.Metade, -_dados.Metade));
            GUI.DrawTexture(Rect.MinMaxRect(cantoA.x, cantoA.y, cantoB.x, cantoB.y), _textura);

            // os nomes: os lugares a partir de ~1,1 px por 10 m, os bairros a partir de ~3 px por 10 m
            _ocupados.Clear();
            if (_rotulos != null)
                foreach (var r in _rotulos)
                {
                    if (r.Nivel == 2 && escala < 0.11f) continue;
                    if (r.Nivel == 3 && escala < 0.3f) continue;
                    var p = NaJanela(r.Ponto);
                    var estilo = _nomes[r.Nivel];
                    var tamanho = estilo.CalcSize(new GUIContent(r.Texto));
                    var caixa = new Rect(p.x - tamanho.x / 2f, p.y - tamanho.y / 2f, tamanho.x, tamanho.y);
                    if (caixa.xMax < 0 || caixa.yMax < 0 || caixa.x > lado || caixa.y > lado) continue;
                    bool encosta = false;
                    foreach (var o in _ocupados) encosta |= o.Overlaps(caixa);
                    if (encosta) continue;
                    _ocupados.Add(caixa);
                    Contornado(caixa, r.Texto, estilo, new Color(1f, 0.98f, 0.92f, 0.8f));
                }

            // os jogadores: uma seta para onde cada um olha (você em ciano, por cima dos outros)
            if (_seta == null) _seta = Seta();
            JogadorRede local = null;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null) continue;
                if (j == JogadorRede.Local) { local = j; continue; }
                var p = Jogador(j, NaJanela, 22f * u, new Color(0.95f, 0.95f, 0.95f));
                Contornado(new Rect(p.x + 12 * u, p.y - 9 * u, 140 * u, 18 * u), $"jogador {j.OwnerClientId + 1}", _nomes[2], new Color(1f, 0.98f, 0.92f, 0.8f));
            }
            if (local != null) Jogador(local, NaJanela, 28f * u, CorDeVoce);
            GUI.EndGroup();

            var mundo = MundoDaPartida.Instancia;
            string titulo = $"{mundo?.Nome}  ·  {Tamanhos.NomeComLado(_dados.Tamanho)}  ·  semente {_dados.Semente}";
            GUI.Label(new Rect(0, 8 * u, Screen.width, 26 * u), titulo, _titulo);
            GUI.Label(new Rect(0, Screen.height - 30 * u, Screen.width, 22 * u),
                "M ou ESC fecha  ·  roda do mouse aproxima  ·  arraste para mover  ·  Espaço: você", _texto);
        }

        void Contornado(Rect caixa, string texto, GUIStyle estilo, Color contorno)
        {
            var cor = estilo.normal.textColor;
            estilo.normal.textColor = contorno;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0) GUI.Label(new Rect(caixa.x + dx, caixa.y + dy, caixa.width, caixa.height), texto, estilo);
            estilo.normal.textColor = cor;
            GUI.Label(caixa, texto, estilo);
        }

        // a seta do jogador, girada para onde ele olha; devolve onde ela ficou na janela
        Vector2 Jogador(JogadorRede j, System.Func<Vector2, Vector2> naJanela, float lado, Color cor)
        {
            var pos = j.transform.position;
            var p = naJanela(new Vector2(pos.x, pos.z));
            var frente = j.transform.forward;
            // o GUI gira no sentido do relógio a partir de cima: olhando para +z a seta aponta para cima
            float giro = Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg;
            var matriz = GUI.matrix;
            var antes = GUI.color;
            GUIUtility.RotateAroundPivot(giro, p);
            GUI.color = cor;
            GUI.DrawTexture(new Rect(p.x - lado / 2f, p.y - lado / 2f, lado, lado), _seta);
            GUI.color = antes;
            GUI.matrix = matriz;
            return p;
        }

        // a seta em ponta de lança (branca, para tingir com GUI.color) com a borda escura, apontando para cima
        static Texture2D Seta()
        {
            const int n = 64;
            // a ponta, o canto direito, o recorte de trás e o canto esquerdo, de cima para baixo
            var forma = new[] { new Vector2(32f, 5f), new Vector2(55f, 57f), new Vector2(32f, 45f), new Vector2(9f, 57f) };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var q = new Vector2(x + 0.5f, y + 0.5f);
                bool dentro = false;
                float borda = float.MaxValue;
                for (int a = 0, b = forma.Length - 1; a < forma.Length; b = a++)
                {
                    var pa = forma[a];
                    var pb = forma[b];
                    if ((pa.y > q.y) != (pb.y > q.y) && q.x < (pb.x - pa.x) * (q.y - pa.y) / (pb.y - pa.y) + pa.x) dentro = !dentro;
                    var ab = pb - pa;
                    float t = Mathf.Clamp01(Vector2.Dot(q - pa, ab) / ab.sqrMagnitude);
                    borda = Mathf.Min(borda, Vector2.Distance(q, pa + ab * t));
                }
                // a distância com sinal (negativa dentro): o miolo até 3 px da borda, o contorno escuro de 3 px por fora
                float dist = dentro ? -borda : borda;
                float miolo = Mathf.Clamp01(-dist - 2.5f);
                float alfa = Mathf.Clamp01(3.5f - dist);
                byte c = (byte)Mathf.RoundToInt(Mathf.Lerp(20f, 255f, miolo));
                // a textura tem a linha 0 embaixo
                px[(n - 1 - y) * n + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(alfa * 255f));
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Seta do mapa" };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
