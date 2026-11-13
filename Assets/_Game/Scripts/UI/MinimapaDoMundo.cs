using System.Collections.Generic;
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
    ///  - no canto de cima, à direita (embaixo do contador de FPS, se ele estiver ligado); quadrado, com o norte para cima;
    ///    o pequeno mostra 600 m de lado (300 m para cada lado de você) e o grande, 1200 m (08/11/2026, a pedido: "hoje está
    ///    com um zoom muito próximo e um pouco bugado": antes eram 250 e 400 m, e a imagem de 2048 px do mapa Grande, a
    ///    3,9 m por pixel, ficava esticada e borrada); perto da beira do mapa, o que fica fora dele é escuro;
    ///  - as setas dos jogadores (você em ciano, no meio), os pings e as marcas (sem os nomes: eles ficam no mapa grande);
    ///  - a tecla N muda o tamanho: pequeno, grande ou escondido (lembrado no computador);
    ///  - some nas telas cheias (o inventário, o mapa, o menu, o cinema, a tela de morte), na gravação sem HUD e no Dia 0
    ///    (sem mapa ainda).
    /// Aqui também (no dono): o ping no mundo, no botão do meio do mouse (o ponto para onde se olha, até 300 m), e o
    /// losango de cada ping por cima de tudo, com o nome de quem marcou e a distância; o bipe quando chega um. E as marcas
    /// e as anotações do mapa no mundo, como o ping mas sem sumir (08/11/2026, <see cref="AlfinetesNoMundo"/>).
    /// </summary>
    public class MinimapaDoMundo : MonoBehaviour
    {
        const string Chave = "minimapa.tamanho";
        // o lado na tela (px a 720p) e o lado em metros, do pequeno e do grande
        static readonly float[] Lados = { 190f, 290f };
        static readonly float[] Metros = { 600f, 1200f };

        /// <summary>0 pequeno, 1 grande, 2 escondido.</summary>
        public static int Tamanho { get; private set; }

        MapaDoMundo _mapa;
        IMarcasDoMundo _mundoOuvido;
        float _proximoPing;
        GUIStyle _rotulo, _norte, _nomeDaMarca, _distancia;

        // as marcas do mapa que estão na frente da câmera neste quadro (a lista é reaproveitada)
        struct AlfineteNaTela
        {
            public int indice;
            public float x, y, distancia, lado, alfa;
        }
        readonly List<AlfineteNaTela> _alfinetes = new List<AlfineteNaTela>();
        // os textos da distância ("340 m", "1,4 km"), guardados: sem um texto novo por marca a cada quadro
        readonly Dictionary<int, string> _textosDaDistancia = new Dictionary<int, string>();

        void Awake()
        {
            _mapa = GetComponent<MapaDoMundo>();
            try { Tamanho = Mathf.Clamp(PlayerPrefs.GetInt(Chave, 0), 0, 2); }
            catch { Tamanho = 0; }
        }

        void OnDisable() => Ouvir(null);

        // o bipe dos pings que chegam (da partida de agora)
        void Ouvir(IMarcasDoMundo m)
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
            // (12/11/2026: as marcas e o chão do mundo da cena, o sorteado ou o Lakewood)
            var mundo = MarcasDoMundo.Atual;
            Ouvir(mundo != null && mundo.IsSpawned ? mundo : null);
            if (mundo != null && mundo.IsSpawned && mundo.IsServer) mundo.LimparOsPings();
            var local = JogadorRede.Local;
            var chao = MundoDoJogo.Atual;
            if (local == null || mundo == null || chao == null || !MenuOnline.Controla) return;
            var teclado = Keyboard.current;
            if (teclado != null && teclado.nKey.wasPressedThisFrame)
            {
                Tamanho = (Tamanho + 1) % 3;
                try { PlayerPrefs.SetInt(Chave, Tamanho); }
                catch { }
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.middleButton.wasPressedThisFrame && Time.unscaledTime >= _proximoPing && OndeOlha(local, chao, out var ponto))
            {
                _proximoPing = Time.unscaledTime + 0.4f;
                local.PedirPing(ponto);
            }
        }

        // o ponto para onde o jogador olha: o que o raio pega (sem o próprio corpo) até 300 m; sem nada (o chão longe ainda
        // sem colisor), o relevo dos dados, andando pelo raio
        static bool OndeOlha(JogadorRede j, IMundoDoJogo d, out Vector3 ponto)
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

        // (08/11/2026: também na gravação sem HUD, GravacaoNoMundo.SemHud, como o resto do HUD; antes o minimapa e os pings
        // ficavam no vídeo)
        bool TelaCheia => MenuOnline.Aberto || MenuOnline.ComTela || MenuOnline.Cinema || ModoVideo.Ligado || GravacaoNoMundo.SemHud
                          || TelaDeMorte.Aberta || (_mapa != null && _mapa.Aberto);

        void Estilos(float u)
        {
            int t = Mathf.RoundToInt(12 * u);
            if (_rotulo != null && _rotulo.fontSize == t) return;
            _rotulo = new GUIStyle(GUI.skin.label) { fontSize = t, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, clipping = TextClipping.Overflow };
            _rotulo.normal.textColor = Color.white;
            _norte = new GUIStyle(_rotulo) { fontSize = Mathf.RoundToInt(13 * u) };
            // as marcas no mundo: numa linha só (a anotação tem até 40 letras), e a distância menor, embaixo
            _nomeDaMarca = new GUIStyle(_rotulo) { wordWrap = false };
            _distancia = new GUIStyle(_rotulo) { fontSize = Mathf.RoundToInt(11 * u), fontStyle = FontStyle.Normal, wordWrap = false };
        }

        void OnGUI()
        {
            // só desenha (nada aqui recebe clique): os outros eventos do OnGUI passam direto
            if (Event.current.type != EventType.Repaint) return;
            var local = JogadorRede.Local;
            var mundo = MarcasDoMundo.Atual;
            if (local == null || mundo == null || !mundo.IsSpawned || MundoDoJogo.Atual == null || TelaCheia) return;
            float u = Screen.height / 720f;
            Estilos(u);
            // as marcas por baixo dos pings (o ping é o recado de agora)
            AlfinetesNoMundo(local, mundo, u);
            PingsNoMundo(local, mundo, u);
            if (Tamanho < 2 && _mapa != null && _mapa.Textura != null) Minimapa(local, mundo, u);
        }

        /// <summary>
        /// As marcas e as anotações do mapa no mundo (08/11/2026, a pedido: "quando criarmos uma marcação no mapa com o botão
        /// direito, ele deveria aparecer como um ping para se localizar mais fácil, sem precisar ficar abrindo o mapa"): o
        /// alfinete na cor dela, 2 m acima do relevo (o y da marca feita no mapa já é o do relevo), e embaixo a distância.
        /// Para não poluir a tela, diferente do ping: não gruda na borda (só as que estão na frente da câmera), vai até 3 km,
        /// fica menor e mais transparente de longe, e o texto da anotação (ou, na marca, o nome de quem marcou) só aparece a
        /// menos de 150 m ou na marca mais perto do meio da tela (onde se mira; uma só, para os textos não se encavalarem).
        /// Não some com o tempo: sai quando é apagada no mapa (sai da lista).
        /// </summary>
        void AlfinetesNoMundo(JogadorRede local, IMarcasDoMundo mundo, float u)
        {
            var lista = mundo.Marcas;
            var cam = local.cameraMundo != null ? local.cameraMundo : Camera.main;
            if (lista == null || cam == null) return;
            var voce = local.transform.position;
            var meioDaTela = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float margem = 24f * u, raioDaMira = 90f * u;
            int naMira = -1;
            float melhor = raioDaMira * raioDaMira;
            _alfinetes.Clear();
            for (int i = 0; i < lista.Count; i++)
            {
                var m = lista[i];
                if (m.tipo == TipoDeMarca.Ping) continue;
                var ponto = new Vector3(m.pos.x, m.pos.y + MarcasDoMapa.AcimaDoChao, m.pos.z);
                float distancia = Vector3.Distance(voce, ponto);
                if (distancia > MarcasDoMapa.AlcanceNoMundo) continue;
                var tela = cam.WorldToScreenPoint(ponto);
                if (tela.z <= 0.3f) continue;   // atrás da câmera (ou colada nela)
                float x = tela.x, y = Screen.height - tela.y;
                if (x < -margem || x > Screen.width + margem || y < -margem || y > Screen.height + margem) continue;
                // de 20 px perto a 11 px a 3 km, e mais transparente; chegando nela (a menos de ~10 m), clareia, para o
                // alfinete não ficar na frente da vista de quem já está lá
                float longe = Mathf.InverseLerp(MarcasDoMapa.TextoNoMundo, MarcasDoMapa.AlcanceNoMundo, distancia);
                float lado = Mathf.Lerp(20f, 11f, longe) * u;
                float alfa = Mathf.Lerp(0.9f, 0.4f, longe) * Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(3f, 10f, distancia));
                // a da mira: a cabeça do alfinete mais perto do meio da tela
                float naTela = (new Vector2(x, y - lado * 0.6f) - meioDaTela).sqrMagnitude;
                if (naTela < melhor)
                {
                    melhor = naTela;
                    naMira = _alfinetes.Count;
                }
                _alfinetes.Add(new AlfineteNaTela { indice = i, x = x, y = y, distancia = distancia, lado = lado, alfa = alfa });
            }
            if (_alfinetes.Count == 0) return;
            if (naMira >= 0)
            {
                // a da mira um pouco mais forte (é a que se está procurando); daqui em diante, naMira é o índice na lista
                var mirada = _alfinetes[naMira];
                mirada.alfa = Mathf.Max(mirada.alfa, 0.85f);
                _alfinetes[naMira] = mirada;
                naMira = mirada.indice;
            }
            // as de longe primeiro: as de perto ficam por cima
            _alfinetes.Sort((p, q) => q.distancia.CompareTo(p.distancia));
            var antes = GUI.color;
            foreach (var a in _alfinetes)
            {
                var m = lista[a.indice];
                var cor = MarcasDoMapa.CorDa(m);
                // o alfinete com a ponta no ponto marcado (como no mapa)
                GUI.color = new Color(cor.r, cor.g, cor.b, a.alfa);
                GUI.DrawTexture(new Rect(a.x - a.lado / 2f, a.y - a.lado * 0.95f, a.lado, a.lado), MarcasDoMapa.Alfinete);
                float y = a.y + 2f * u;
                if (a.distancia < MarcasDoMapa.TextoNoMundo || a.indice == naMira)
                {
                    string texto = m.tipo == TipoDeMarca.Anotacao && m.texto.Length > 0 ? m.texto.ToString() : m.autor.ToString();
                    Sombreado(new Rect(a.x - 200 * u, y, 400 * u, 18 * u), texto, _nomeDaMarca, a.alfa);
                    y += 15f * u;
                }
                Sombreado(new Rect(a.x - 60 * u, y, 120 * u, 16 * u), TextoDaDistancia(a.distancia), _distancia, a.alfa);
            }
            GUI.color = antes;
        }

        // o texto com a sombra escura (1 px para baixo e para a direita), como o do ping; muda o GUI.color
        static void Sombreado(Rect r, string texto, GUIStyle estilo, float alfa)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.75f * alfa);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), texto, estilo);
            GUI.color = new Color(1f, 1f, 1f, alfa);
            GUI.Label(r, texto, estilo);
        }

        // até 1 km, de metro em metro ("340 m"); depois, de 100 em 100 m ("1,4 km")
        string TextoDaDistancia(float metros)
        {
            int decimos = Mathf.RoundToInt(metros / 100f);
            int chave = metros < 1000f ? Mathf.RoundToInt(metros) : 100000 + decimos;
            if (_textosDaDistancia.TryGetValue(chave, out var texto)) return texto;
            texto = metros < 1000f ? $"{chave} m" : $"{decimos / 10},{decimos % 10} km";
            _textosDaDistancia[chave] = texto;
            return texto;
        }

        // o losango de cada ping, por cima de tudo, com quem marcou e a distância (some nos 3 s finais)
        void PingsNoMundo(JogadorRede local, IMarcasDoMundo mundo, float u)
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

        void Minimapa(JogadorRede local, IMarcasDoMundo mundo, float u)
        {
            // o retângulo do mapa (12/11/2026: no sorteado, o quadrado em volta da origem; no Lakewood, 12 × 10 km a partir dela)
            var r = _mapa.Area;
            float lado = Lados[Tamanho] * u, metros = Metros[Tamanho];
            // embaixo do contador de FPS quando ele está ligado (08/11/2026: o contador, de 10 a 34 px, cobria o alto do
            // minimapa, que começava nos 30 px)
            float topo = (Opcoes.MostrarFps ? 44f : 30f) * u;
            var area = new Rect(Screen.width - lado - 14 * u, topo, lado, lado);
            var antes = GUI.color;
            // a moldura e, dentro dela, o fundo escuro (o que fica fora do mapa, perto da beira)
            GUI.color = new Color(0.05f, 0.05f, 0.045f, 0.85f);
            GUI.DrawTexture(new Rect(area.x - 3 * u, area.y - 3 * u, area.width + 6 * u, area.height + 6 * u), Texture2D.whiteTexture);
            GUI.color = new Color(0.1f, 0.1f, 0.095f, 0.9f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = antes;
            // a imagem: o pedaço em volta do jogador (o norte, o +z, para cima), só a parte do recorte que está dentro do mapa,
            // no lugar dela no quadrado (08/11/2026: antes o recorte inteiro ia para a textura, e perto da beira o Clamp dela
            // esticava a última fileira de pixels por cima do que é fora do mapa)
            var p = local.transform.position;
            float meia = metros / 2f, escala = lado / metros;
            float x0 = Mathf.Max(p.x - meia, r.xMin), x1 = Mathf.Min(p.x + meia, r.xMax);
            float z0 = Mathf.Max(p.z - meia, r.yMin), z1 = Mathf.Min(p.z + meia, r.yMax);
            if (x1 > x0 && z1 > z0)
            {
                // na tela, o y cresce para baixo: o alto do pedaço é o z maior
                var pedaco = new Rect(area.x + (x0 - (p.x - meia)) * escala, area.y + (p.z + meia - z1) * escala, (x1 - x0) * escala, (z1 - z0) * escala);
                GUI.DrawTextureWithTexCoords(pedaco, _mapa.Textura, new Rect((x0 - r.xMin) / r.width, (z0 - r.yMin) / r.height, (x1 - x0) / r.width, (z1 - z0) / r.height));
            }
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
