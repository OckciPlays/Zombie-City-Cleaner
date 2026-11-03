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
    /// O nome de cada jogador vai ao lado da seta dele (03/11/2026, a pedido: "o nome dos jogadores no mapa"), com as marcas,
    /// as anotações e os pings (MapaDoMundo.Marcas.cs); o minimapa (<see cref="MinimapaDoMundo"/>) usa a mesma imagem.
    /// Fica no objeto "Mundo" da cena Mundo.
    /// </summary>
    public partial class MapaDoMundo : MonoBehaviour
    {
        [Tooltip("O lado da imagem do mapa, em pixels (2048: uns 2,5 m por pixel no médio).")]
        public int resolucao = 2048;

        public bool Aberto { get; private set; }

        /// <summary>O mapa da cena (o minimapa lê a imagem dele).</summary>
        public static MapaDoMundo Instancia { get; private set; }
        /// <summary>A imagem do mapa inteiro (null enquanto pinta).</summary>
        public Texture2D Textura => _textura;
        public DadosDoMundo Dados => _dados;

        static Texture2D _setaCompartilhada;
        /// <summary>A seta dos jogadores (a mesma do mapa grande, para o minimapa).</summary>
        public static Texture2D SetaDosJogadores => _setaCompartilhada != null ? _setaCompartilhada : _setaCompartilhada = Seta();

        DadosDoMundo _dados;
        Texture2D _textura;
        Task<Color32[][]> _pintando;
        List<PintorDoMapa.Rotulo> _rotulos;
        float _zoom = 1f;
        Vector2 _centro;
        readonly List<Rect> _ocupados = new List<Rect>();
        GUIStyle[] _nomes;
        GUIStyle _titulo, _texto, _nomeDoJogador;
        readonly List<(Rect caixa, string nome, bool voce)> _nomesDosJogadores = new List<(Rect, string, bool)>();
        float _uDosEstilos;
        Texture2D _seta;

        // a cor de você no mapa: o ciano não aparece em nada do desenho (o amarelo é o da casa da família)
        static readonly Color CorDeVoce = new Color(0.25f, 0.9f, 1f);

        void OnEnable()
        {
            Instancia = this;
            MenuOnline.FecharTelas += Fechar;
            // o minimapa (03/11/2026): no mesmo objeto, posto aqui (sem mexer na cena do Mundo)
            if (GetComponent<MinimapaDoMundo>() == null) gameObject.AddComponent<MinimapaDoMundo>();
        }

        void OnDisable()
        {
            MenuOnline.FecharTelas -= Fechar;
            if (Aberto) Abrir(false);
        }

        void OnDestroy()
        {
            if (_textura != null) Destroy(_textura);
            if (Instancia == this) Instancia = null;
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
                    _pintando = Task.Run(() => ComMipmaps(PintorDoMapa.Pintar(dados, lado), lado));
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
                    // com mipmaps: no mapa inteiro a imagem fica 2 a 3 vezes menor na tela, e as linhas finas tremeriam. Eles
                    // vêm prontos da outra thread (ComMipmaps): aqui, só a cópia e a subida
                    var mips = _pintando.Result;
                    _textura = new Texture2D(resolucao, resolucao, TextureFormat.RGBA32, mips.Length, false)
                        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 1, name = "Mapa do mundo" };
                    for (int k = 0; k < mips.Length; k++) _textura.SetPixelData(mips[k], k);
                    _textura.Apply(false, true);
                }
                _pintando = null;
            }

            var teclado = Keyboard.current;
            if (JogadorRede.Local == null)
            {
                if (Aberto) Abrir(false);
                return;
            }
            if (teclado != null && teclado.mKey.wasPressedThisFrame && !MenuOnline.Aberto && _textura != null && !EscrevendoNasMarcas) Abrir(!Aberto);
            if (!Aberto)
            {
                _menu = MenuDasMarcas.Nenhum;
                return;
            }

            var mouse = Mouse.current;
            float escala = Escala();
            if (AtualizarAsMarcas(mouse, escala)) return;   // o menu das marcas aberto: o mapa fica parado
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

        // a imagem e os mipmaps dela (a média de 2 × 2 de um nível para o próximo), na outra thread (03/11/2026: o
        // SetPixels32 com o Apply que gerava os mipmaps levava ~30 ms num quadro só, já com o jogo na tela)
        static Color32[][] ComMipmaps(Color32[] base0, int lado)
        {
            int niveis = 1;
            for (int n = lado; n > 1; n >>= 1) niveis++;
            var mips = new Color32[niveis][];
            mips[0] = base0;
            int antes = lado;
            for (int k = 1; k < niveis; k++)
            {
                int n = Mathf.Max(1, antes >> 1);
                var a = mips[k - 1];
                var m = new Color32[n * n];
                for (int y = 0; y < n; y++)
                {
                    int y0 = Mathf.Min(2 * y, antes - 1) * antes, y1 = Mathf.Min(2 * y + 1, antes - 1) * antes;
                    for (int x = 0; x < n; x++)
                    {
                        int x0 = Mathf.Min(2 * x, antes - 1), x1 = Mathf.Min(2 * x + 1, antes - 1);
                        Color32 p = a[y0 + x0], q = a[y0 + x1], r = a[y1 + x0], s = a[y1 + x1];
                        m[y * n + x] = new Color32((byte)((p.r + q.r + r.r + s.r + 2) >> 2), (byte)((p.g + q.g + r.g + s.g + 2) >> 2),
                                                   (byte)((p.b + q.b + r.b + s.b + 2) >> 2), (byte)((p.a + q.a + r.a + s.a + 2) >> 2));
                    }
                }
                mips[k] = m;
                antes = n;
            }
            return mips;
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
            // o nome do jogador: à esquerda (começa colado na seta), sem margem (a etiqueta é desenhada em volta)
            _nomeDoJogador = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(13 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow, wordWrap = false, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
            };
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

            // os nomes dos jogadores primeiro (03/11/2026): colados na seta, e os nomes dos lugares desviam deles (antes, o nome
            // ficava centrado numa caixa à direita, longe da seta, e os lugares que aparecem com o zoom passavam por cima)
            _ocupados.Clear();
            _nomesDosJogadores.Clear();
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null) continue;
                bool voce = j == JogadorRede.Local;
                var pos = j.transform.position;
                var p = NaJanela(new Vector2(pos.x, pos.z));
                string nome = j.NomeNoMapa;
                var t = _nomeDoJogador.CalcSize(new GUIContent(nome));
                float ladoDaSeta = (voce ? 28f : 22f) * u;
                var caixa = new Rect(p.x + ladoDaSeta * 0.5f + 6 * u, p.y - t.y / 2f, t.x, t.y);
                _nomesDosJogadores.Add((caixa, nome, voce));
                _ocupados.Add(new Rect(caixa.x - 5 * u, caixa.y - 2 * u, caixa.width + 10 * u, caixa.height + 4 * u));
                _ocupados.Add(new Rect(p.x - ladoDaSeta / 2f, p.y - ladoDaSeta / 2f, ladoDaSeta, ladoDaSeta));
            }

            // os nomes: os lugares a partir de ~1,1 px por 10 m, os bairros a partir de ~3 px por 10 m
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
            // as marcas, as anotações e os pings (embaixo das setas)
            DesenharAsMarcas(NaJanela, u, janela);
            if (_seta == null) _seta = SetaDosJogadores;
            JogadorRede local = null;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null) continue;
                if (j == JogadorRede.Local) { local = j; continue; }
                Jogador(j, NaJanela, 22f * u, new Color(0.95f, 0.95f, 0.95f));
            }
            if (local != null) Jogador(local, NaJanela, 28f * u, CorDeVoce);
            // o nome de cada um numa etiqueta escura ao lado da seta, na cor dele (você em ciano), por cima de tudo
            foreach (var (caixa, nome, voce) in _nomesDosJogadores)
            {
                GUI.color = new Color(0.04f, 0.04f, 0.035f, 0.75f);
                GUI.DrawTexture(new Rect(caixa.x - 4 * u, caixa.y - 1 * u, caixa.width + 8 * u, caixa.height + 2 * u), Texture2D.whiteTexture);
                GUI.color = antes;
                _nomeDoJogador.normal.textColor = voce ? CorDeVoce : new Color(0.97f, 0.97f, 0.95f);
                GUI.Label(caixa, nome, _nomeDoJogador);
            }
            GUI.EndGroup();
            MenuDasMarcasNaTela(u);

            var mundo = MundoDaPartida.Instancia;
            string titulo = $"{mundo?.Nome}  ·  {Tamanhos.NomeComLado(_dados.Tamanho)}  ·  semente {_dados.Semente}";
            GUI.Label(new Rect(0, 8 * u, Screen.width, 26 * u), titulo, _titulo);
            GUI.Label(new Rect(0, Screen.height - 30 * u, Screen.width, 22 * u),
                "M ou ESC fecha  ·  roda do mouse aproxima  ·  arraste para mover  ·  Espaço: você  ·  clique direito: ping, marca ou anotação", _texto);
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
