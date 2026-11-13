using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A janela do desenho do mapa de Lakewood (Plano_Lakewood: "ferramentas de Editor" para ajustar o mapa sem reescrever
    /// o sistema). Menu Zombie City Cleaner > Lakewood > Desenho do mapa. O mapa pintado (<see cref="PintorDeLakewood"/>,
    /// 2048 px, pintado em outra thread a partir de uma cópia do mapa) com os nomes por cima, a imagem de referência por
    /// baixo (para comparar; a transparência no painel), a grade dos setores e a camada escolhida em linhas: clicar escolhe
    /// um elemento, arrastar move o ponto, Shift + clicar põe um ponto no lado mais perto, Ctrl + clicar tira o ponto,
    /// Alt + arrastar move o elemento inteiro. O botão direito (ou o do meio) arrasta o mapa, e a roda aproxima. Tudo vai
    /// para o Desfazer. Do lado, os campos do elemento escolhido, e os botões de pôr, apagar, pintar de novo e gravar a imagem.
    /// </summary>
    public sealed class JanelaDeLakewood : EditorWindow
    {
        enum Camada { Zonas, Picos, Passagens, Lagos, Ilhas, Rios, Represa, Estradas, Pontes, Cidades, Lugares, Tuneis, Partida }

        const int LarguraDaPintura = 2048;
        const float LarguraDoPainel = 340f, Perto = 12f;

        MapaDeLakewood _d;
        SerializedObject _so;
        Texture2D _tex, _referencia;
        RelevoDeLakewood _relevo;
        List<RotuloDoMapa> _rotulos = new List<RotuloDoMapa>();
        Task<(RelevoDeLakewood relevo, Color32[] px, List<RotuloDoMapa> rotulos)> _tarefa;
        MapaDeLakewood _copia;
        bool _sujo = true, _relevoSujo = true, _nomes = true, _setores = true, _mostrarReferencia;
        float _alfaDaReferencia = 0.6f;
        double _quando;

        Vector2 _centro;
        float _zoom = -1f;
        Camada _camada = Camada.Lagos;
        int _sel = -1, _ponto = -1;
        bool _arrastando, _movendoTudo;
        Vector2 _mouseNoMundo, _rolagem;
        GUIStyle _rotulo;

        [MenuItem("Zombie City Cleaner/Lakewood/Desenho do mapa", false, 20)]
        public static void Abrir() => GetWindow<JanelaDeLakewood>("Mapa de Lakewood");

        void OnEnable()
        {
            wantsMouseMove = true;
            _d = AssetDatabase.LoadAssetAtPath<MapaDeLakewood>(MapaDeLakewood.Caminho);
            if (_d != null)
            {
                _so = new SerializedObject(_d);
                _centro = _d.Meio;
            }
            Undo.undoRedoPerformed += Desfeito;
            EditorApplication.update += Atualizar;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= Desfeito;
            EditorApplication.update -= Atualizar;
            if (_tex != null) DestroyImmediate(_tex);
            if (_referencia != null) DestroyImmediate(_referencia);
            if (_copia != null && _tarefa == null) DestroyImmediate(_copia);
        }

        void Desfeito() => Sujar(true);

        void Sujar(bool relevo)
        {
            _sujo = true;
            _relevoSujo |= relevo;
            _quando = EditorApplication.timeSinceStartup + 0.25;
            Repaint();
        }

        static bool MudaORelevo(Camada c) => c is not (Camada.Pontes or Camada.Tuneis or Camada.Partida);

        // a pintura em outra thread: começa quando o mapa mudou e ninguém está arrastando; acaba aqui, na principal
        void Atualizar()
        {
            if (_tarefa != null && _tarefa.IsCompleted)
            {
                if (_tarefa.IsFaulted) Debug.LogException(_tarefa.Exception);
                else
                {
                    var (relevo, px, rotulos) = _tarefa.Result;
                    _relevo = relevo;
                    _rotulos = rotulos;
                    int altura = PintorDeLakewood.AlturaPara(_d, LarguraDaPintura);
                    if (_tex == null || _tex.height != altura)
                    {
                        if (_tex != null) DestroyImmediate(_tex);
                        _tex = new Texture2D(LarguraDaPintura, altura, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                    }
                    _tex.SetPixels32(px);
                    _tex.Apply();
                }
                DestroyImmediate(_copia);
                _copia = null;
                _tarefa = null;
                Repaint();
            }
            if (_tarefa == null && _sujo && _d != null && !_arrastando && EditorApplication.timeSinceStartup >= _quando)
            {
                _sujo = false;
                _copia = _d.Copia();
                var copia = _copia;
                var relevoPronto = _relevoSujo ? null : _relevo;
                _relevoSujo = false;
                _tarefa = Task.Run(() =>
                {
                    var r = relevoPronto ?? new RelevoDeLakewood(copia);
                    var px = PintorDeLakewood.Pintar(copia, r, LarguraDaPintura);
                    return (r, px, PintorDeLakewood.Rotulos(copia, r));
                });
            }
        }

        // a imagem de referência recortada na moldura (lida uma vez)
        Texture2D Referencia()
        {
            if (_referencia != null) return _referencia;
            if (!File.Exists(LeituraDaReferencia.Imagem)) return null;
            var cheia = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            cheia.LoadImage(File.ReadAllBytes(LeituraDaReferencia.Imagem));
            int w = LeituraDaReferencia.X1 - LeituraDaReferencia.X0, h = LeituraDaReferencia.Y1 - LeituraDaReferencia.Y0;
            var px = cheia.GetPixels(LeituraDaReferencia.X0, cheia.height - LeituraDaReferencia.Y1, w, h);
            _referencia = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            _referencia.SetPixels(px);
            _referencia.Apply();
            DestroyImmediate(cheia);
            return _referencia;
        }

        // ------------------------------------------------------------------ a tela

        Rect AreaDoMapa => new Rect(0f, 0f, position.width - LarguraDoPainel, position.height);

        Vector2 TelaDe(Vector2 w)
        {
            var c = AreaDoMapa.center;
            return new Vector2(c.x + (w.x - _centro.x) * _zoom, c.y - (w.y - _centro.y) * _zoom);
        }

        Vector2 MundoDe(Vector2 s)
        {
            var c = AreaDoMapa.center;
            return new Vector2(_centro.x + (s.x - c.x) / _zoom, _centro.y - (s.y - c.y) / _zoom);
        }

        void OnGUI()
        {
            if (_d == null)
            {
                EditorGUILayout.HelpBox($"Não achei o mapa em {MapaDeLakewood.Caminho}. Rode Zombie City Cleaner > Lakewood > 1 e 2.", MessageType.Error);
                if (GUILayout.Button("Procurar de novo")) OnEnable();
                return;
            }
            var area = AreaDoMapa;
            if (_zoom <= 0f) _zoom = Mathf.Min(area.width / _d.largura, area.height / _d.comprimento) * 0.96f;
            var e = Event.current;
            if (area.Contains(e.mousePosition)) _mouseNoMundo = MundoDe(e.mousePosition);

            EditorGUI.DrawRect(area, new Color(0.1f, 0.11f, 0.12f));
            var canto = TelaDe(new Vector2(0f, _d.comprimento));
            var retangulo = new Rect(canto.x, canto.y, _d.largura * _zoom, _d.comprimento * _zoom);
            if (_tex != null) GUI.DrawTexture(retangulo, _tex);
            if (_mostrarReferencia)
            {
                var referencia = Referencia();
                if (referencia != null)
                {
                    var antes = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, _alfaDaReferencia);
                    GUI.DrawTexture(retangulo, referencia);
                    GUI.color = antes;
                }
            }
            if (_setores) Setores();
            if (_nomes) Nomes();
            DesenharAsLinhas();
            Mouse(e, area);
            Painel();
        }

        void Setores()
        {
            if (Event.current.type != EventType.Repaint) return;
            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            for (int i = 0; i <= _d.setoresX; i++)
            {
                var a = TelaDe(new Vector2(i * _d.LadoDoSetorX, 0f));
                var b = TelaDe(new Vector2(i * _d.LadoDoSetorX, _d.comprimento));
                Handles.DrawLine(a, b);
            }
            for (int j = 0; j <= _d.setoresZ; j++)
            {
                var a = TelaDe(new Vector2(0f, j * _d.LadoDoSetorZ));
                var b = TelaDe(new Vector2(_d.largura, j * _d.LadoDoSetorZ));
                Handles.DrawLine(a, b);
            }
        }

        void Nomes()
        {
            _rotulo ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, richText = false };
            foreach (var r in _rotulos)
            {
                int tam = Mathf.RoundToInt(r.Tamanho * _zoom * 1.1f);
                if (tam < 7) continue;
                _rotulo.fontSize = Mathf.Min(tam, 120);
                _rotulo.fontStyle = r.Estilo;
                var p = TelaDe(r.Ponto);
                var caixa = new Rect(p.x - 600f, p.y - tam, 1200f, tam * 2f);
                var antes = GUI.matrix;
                if (Mathf.Abs(r.Giro) > 0.5f) GUIUtility.RotateAroundPivot(-r.Giro, p);
                _rotulo.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
                GUI.Label(new Rect(caixa.x + 1.5f, caixa.y + 1.5f, caixa.width, caixa.height), r.Texto, _rotulo);
                _rotulo.normal.textColor = r.Cor;
                GUI.Label(caixa, r.Texto, _rotulo);
                GUI.matrix = antes;
            }
        }

        // ------------------------------------------------------------------ os elementos de cada camada

        static bool DePonto(Camada c) => c is Camada.Picos or Camada.Represa or Camada.Pontes or Camada.Lugares or Camada.Partida;

        static bool Fechada(Camada c) => c is Camada.Zonas or Camada.Lagos or Camada.Ilhas or Camada.Cidades;

        /// <summary>Quantos elementos a camada tem (na represa, os dois pontos; na partida, um).</summary>
        int Quantos(Camada c) => c switch
        {
            Camada.Represa => 2,
            Camada.Partida => 1,
            _ => Lista(c)?.Count ?? 0,
        };

        System.Collections.IList Lista(Camada c) => c switch
        {
            Camada.Zonas => _d.relevo.zonas,
            Camada.Picos => _d.relevo.picos,
            Camada.Passagens => _d.relevo.passagens,
            Camada.Lagos => _d.lagos,
            Camada.Ilhas => _d.ilhas,
            Camada.Rios => _d.rios,
            Camada.Estradas => _d.estradas,
            Camada.Pontes => _d.pontes,
            Camada.Cidades => _d.cidades,
            Camada.Lugares => _d.lugares,
            Camada.Tuneis => _d.tuneis,
            _ => null,
        };

        // os pontos de um elemento de linha (a mesma lista do mapa: mexer nela mexe no mapa)
        List<Vector2> Pontos(Camada c, int el) => c switch
        {
            Camada.Zonas => _d.relevo.zonas[el].contorno,
            Camada.Passagens => _d.relevo.passagens[el].pontos,
            Camada.Lagos => _d.lagos[el].contorno,
            Camada.Ilhas => _d.ilhas[el].contorno,
            Camada.Rios => _d.rios[el].pontos,
            Camada.Estradas => _d.estradas[el].pontos,
            Camada.Cidades => _d.cidades[el].contorno,
            Camada.Tuneis => _d.tuneis[el].pontos,
            _ => null,
        };

        Vector2 Ponto(Camada c, int el) => c switch
        {
            Camada.Picos => _d.relevo.picos[el].ponto,
            Camada.Represa => el == 0 ? _d.represa.a : _d.represa.b,
            Camada.Pontes => _d.pontes[el].perto,
            Camada.Lugares => _d.lugares[el].ponto,
            _ => _d.partida,
        };

        void PorPonto(Camada c, int el, Vector2 p)
        {
            switch (c)
            {
                case Camada.Picos: _d.relevo.picos[el].ponto = p; break;
                case Camada.Represa: if (el == 0) _d.represa.a = p; else _d.represa.b = p; break;
                case Camada.Pontes: _d.pontes[el].perto = p; break;
                case Camada.Lugares: _d.lugares[el].ponto = p; break;
                case Camada.Partida: _d.partida = p; break;
            }
        }

        // ------------------------------------------------------------------ as linhas da camada

        void DesenharAsLinhas()
        {
            if (Event.current.type != EventType.Repaint) return;
            int n = Quantos(_camada);
            // a crista da represa, ligando os dois pontos
            if (_camada == Camada.Represa && _d.represa.Tem)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.7f);
                Handles.DrawAAPolyLine(3f, TelaDe(_d.represa.a), TelaDe(_d.represa.b));
                var rec = _d.represa.recorte;
                if (rec.width > 0f)
                {
                    Handles.color = new Color(1f, 0.5f, 0.2f, 0.6f);
                    Handles.DrawAAPolyLine(1.5f, TelaDe(rec.min), TelaDe(new Vector2(rec.xMax, rec.yMin)), TelaDe(rec.max), TelaDe(new Vector2(rec.xMin, rec.yMax)), TelaDe(rec.min));
                }
            }
            for (int el = 0; el < n; el++)
            {
                bool escolhido = el == _sel;
                var cor = escolhido ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f, 0.55f);
                if (DePonto(_camada))
                {
                    var p = TelaDe(Ponto(_camada, el));
                    EditorGUI.DrawRect(new Rect(p.x - 5f, p.y - 5f, 10f, 10f), Color.black);
                    EditorGUI.DrawRect(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), cor);
                    if (_camada == Camada.Lugares)
                    {
                        float r = _d.lugares[el].raio * _zoom;
                        Handles.color = cor;
                        Handles.DrawWireDisc(new Vector3(p.x, p.y, 0f), Vector3.forward, r);
                    }
                    continue;
                }
                var pts = Pontos(_camada, el);
                if (pts == null || pts.Count == 0) continue;
                var tela = new Vector3[pts.Count + (Fechada(_camada) ? 1 : 0)];
                for (int i = 0; i < tela.Length; i++) tela[i] = TelaDe(pts[i % pts.Count]);
                Handles.color = new Color(0f, 0f, 0f, 0.6f);
                Handles.DrawAAPolyLine(escolhido ? 5f : 3.5f, tela);
                Handles.color = cor;
                Handles.DrawAAPolyLine(escolhido ? 3f : 1.8f, tela);
                if (!escolhido) continue;
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = TelaDe(pts[i]);
                    bool este = i == _ponto;
                    EditorGUI.DrawRect(new Rect(p.x - 4.5f, p.y - 4.5f, 9f, 9f), Color.black);
                    EditorGUI.DrawRect(new Rect(p.x - 3.5f, p.y - 3.5f, 7f, 7f), este ? Color.red : i == 0 ? Color.cyan : Color.white);
                }
            }
        }

        // ------------------------------------------------------------------ o mouse

        void Mouse(Event e, Rect area)
        {
            bool dentro = area.Contains(e.mousePosition);
            switch (e.type)
            {
                case EventType.ScrollWheel when dentro:
                {
                    var antes = MundoDe(e.mousePosition);
                    _zoom = Mathf.Clamp(_zoom * Mathf.Pow(1.12f, -e.delta.y), 0.02f, 2f);
                    var depois = MundoDe(e.mousePosition);
                    _centro += antes - depois;
                    e.Use();
                    break;
                }
                case EventType.MouseDrag when e.button == 1 || e.button == 2:
                    _centro += new Vector2(-e.delta.x, e.delta.y) / _zoom;
                    e.Use();
                    break;
                case EventType.MouseDown when dentro && e.button == 0:
                    Clicar(e);
                    e.Use();
                    break;
                case EventType.MouseDrag when e.button == 0 && _arrastando:
                    Arrastar(e);
                    e.Use();
                    break;
                case EventType.MouseUp when e.button == 0 && _arrastando:
                    _arrastando = _movendoTudo = false;
                    Undo.IncrementCurrentGroup();
                    Sujar(MudaORelevo(_camada));
                    e.Use();
                    break;
                case EventType.MouseMove:
                    Repaint();
                    break;
            }
        }

        void Clicar(Event e)
        {
            var w = MundoDe(e.mousePosition);
            bool temEscolhido = _sel >= 0 && _sel < Quantos(_camada);
            if (temEscolhido && e.alt)
            {
                Undo.RecordObject(_d, "Mover o elemento do mapa");
                _arrastando = _movendoTudo = true;
                return;
            }
            if (temEscolhido && !DePonto(_camada))
            {
                var pts = Pontos(_camada, _sel);
                int perto = -1;
                float melhor = Perto;
                for (int i = 0; i < pts.Count; i++)
                {
                    float dd = Vector2.Distance(TelaDe(pts[i]), e.mousePosition);
                    if (dd < melhor)
                    {
                        melhor = dd;
                        perto = i;
                    }
                }
                if (perto >= 0 && (e.control || e.command))
                {
                    if (pts.Count > (Fechada(_camada) ? 3 : 2))
                    {
                        Undo.RecordObject(_d, "Tirar o ponto do mapa");
                        pts.RemoveAt(perto);
                        _ponto = -1;
                        EditorUtility.SetDirty(_d);
                        Sujar(MudaORelevo(_camada));
                    }
                    return;
                }
                if (perto >= 0)
                {
                    Undo.RecordObject(_d, "Mover o ponto do mapa");
                    _ponto = perto;
                    _arrastando = true;
                    return;
                }
                if (e.shift)
                {
                    int lados = Fechada(_camada) ? pts.Count : pts.Count - 1, melhorLado = 0;
                    float dist = float.MaxValue, tLado = 0.5f;
                    for (int i = 0; i < lados; i++)
                    {
                        float dd = Linhas.DistanciaDoSegmento(w, pts[i], pts[(i + 1) % pts.Count], out float tt);
                        if (dd < dist)
                        {
                            dist = dd;
                            melhorLado = i;
                            tLado = tt;
                        }
                    }
                    Undo.RecordObject(_d, "Pôr um ponto no mapa");
                    bool aberta = !Fechada(_camada);
                    if (aberta && melhorLado == lados - 1 && tLado >= 0.999f)
                    {
                        pts.Add(w);
                        _ponto = pts.Count - 1;
                    }
                    else if (aberta && melhorLado == 0 && tLado <= 0.001f)
                    {
                        pts.Insert(0, w);
                        _ponto = 0;
                    }
                    else
                    {
                        pts.Insert(melhorLado + 1, w);
                        _ponto = melhorLado + 1;
                    }
                    _arrastando = true;
                    EditorUtility.SetDirty(_d);
                    return;
                }
            }
            if (temEscolhido && DePonto(_camada) && Vector2.Distance(TelaDe(Ponto(_camada, _sel)), e.mousePosition) < Perto)
            {
                Undo.RecordObject(_d, "Mover o ponto do mapa");
                _arrastando = true;
                return;
            }
            Escolher(w, e.mousePosition);
        }

        // escolhe o elemento mais perto do clique (nas áreas fechadas, clicar dentro também vale)
        void Escolher(Vector2 w, Vector2 tela)
        {
            float melhor = Perto;
            int sel = -1;
            int n = Quantos(_camada);
            for (int el = 0; el < n; el++)
            {
                float dd;
                if (DePonto(_camada)) dd = Vector2.Distance(TelaDe(Ponto(_camada, el)), tela);
                else
                {
                    var pts = Pontos(_camada, el);
                    if (pts == null || pts.Count == 0) continue;
                    dd = Linhas.Distancia(w, pts, Fechada(_camada)) * _zoom;
                    if (Fechada(_camada) && pts.Count >= 3 && Linhas.Dentro(w, pts)) dd = Mathf.Min(dd, Perto * 0.5f + Linhas.Area(pts) * 1e-9f);
                }
                if (dd < melhor)
                {
                    melhor = dd;
                    sel = el;
                }
            }
            _sel = sel;
            _ponto = -1;
            GUI.FocusControl(null);
            Repaint();
        }

        void Arrastar(Event e)
        {
            var w = MundoDe(e.mousePosition);
            if (_movendoTudo)
            {
                var delta = new Vector2(e.delta.x, -e.delta.y) / _zoom;
                Mover(delta);
            }
            else if (DePonto(_camada)) PorPonto(_camada, _sel, w);
            else if (_ponto >= 0)
            {
                var pts = Pontos(_camada, _sel);
                if (_ponto < pts.Count) pts[_ponto] = w;
            }
            EditorUtility.SetDirty(_d);
            Repaint();
        }

        // o elemento inteiro
        void Mover(Vector2 delta)
        {
            void Todos(List<Vector2> l)
            {
                for (int i = 0; i < l.Count; i++) l[i] += delta;
            }
            switch (_camada)
            {
                case Camada.Cidades:
                    var c = _d.cidades[_sel];
                    c.centro += delta;
                    Todos(c.contorno);
                    break;
                case Camada.Lagos:
                    Todos(_d.lagos[_sel].contorno);
                    if (_d.lagos[_sel].rotulo != Vector2.zero) _d.lagos[_sel].rotulo += delta;
                    break;
                case Camada.Represa:
                    _d.represa.a += delta;
                    _d.represa.b += delta;
                    Todos(_d.represa.funilOeste);
                    Todos(_d.represa.funilLeste);
                    _d.represa.recorte.position += delta;
                    break;
                default:
                    if (DePonto(_camada)) PorPonto(_camada, _sel, Ponto(_camada, _sel) + delta);
                    else Todos(Pontos(_camada, _sel));
                    break;
            }
        }

        // ------------------------------------------------------------------ o painel

        string Caminho()
        {
            if (_sel < 0) return null;
            return _camada switch
            {
                Camada.Zonas => $"relevo.zonas.Array.data[{_sel}]",
                Camada.Picos => $"relevo.picos.Array.data[{_sel}]",
                Camada.Passagens => $"relevo.passagens.Array.data[{_sel}]",
                Camada.Lagos => $"lagos.Array.data[{_sel}]",
                Camada.Ilhas => $"ilhas.Array.data[{_sel}]",
                Camada.Rios => $"rios.Array.data[{_sel}]",
                Camada.Represa => "represa",
                Camada.Estradas => $"estradas.Array.data[{_sel}]",
                Camada.Pontes => $"pontes.Array.data[{_sel}]",
                Camada.Cidades => $"cidades.Array.data[{_sel}]",
                Camada.Lugares => $"lugares.Array.data[{_sel}]",
                Camada.Tuneis => $"tuneis.Array.data[{_sel}]",
                Camada.Partida => "partida",
                _ => null,
            };
        }

        void Painel()
        {
            var r = new Rect(position.width - LarguraDoPainel, 0f, LarguraDoPainel, position.height);
            EditorGUI.DrawRect(r, new Color(0.19f, 0.19f, 0.19f));
            GUILayout.BeginArea(new Rect(r.x + 8f, 8f, r.width - 16f, r.height - 16f));
            _rolagem = GUILayout.BeginScrollView(_rolagem);
            EditorGUILayout.LabelField("Mapa de Lakewood", EditorStyles.boldLabel);
            string onde = _d.Dentro(_mouseNoMundo.x, _mouseNoMundo.y) ? $" · setor {_d.NomeDoSetorEm(_mouseNoMundo.x, _mouseNoMundo.y)}" : "";
            string altura = _relevo != null && _d.Dentro(_mouseNoMundo.x, _mouseNoMundo.y) ? $" · {_relevo.Altura(_mouseNoMundo.x, _mouseNoMundo.y):F0} m" : "";
            EditorGUILayout.LabelField(_tarefa != null ? "Pintando..." : $"x {_mouseNoMundo.x:F0}   z {_mouseNoMundo.y:F0}{onde}{altura}", EditorStyles.miniLabel);
            var nova = (Camada)EditorGUILayout.EnumPopup("Camada", _camada);
            if (nova != _camada)
            {
                _camada = nova;
                _sel = _ponto = -1;
                if (_camada is Camada.Partida) _sel = 0;
            }
            _nomes = EditorGUILayout.Toggle("Mostrar os nomes", _nomes);
            _setores = EditorGUILayout.Toggle("Mostrar os setores", _setores);
            _mostrarReferencia = EditorGUILayout.Toggle("Mostrar a referência", _mostrarReferencia);
            if (_mostrarReferencia) _alfaDaReferencia = EditorGUILayout.Slider("Transparência", _alfaDaReferencia, 0.05f, 1f);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = Lista(_camada) != null;
                if (GUILayout.Button("Pôr um novo")) PorUmNovo();
                GUI.enabled = _sel >= 0 && Lista(_camada) != null;
                if (GUILayout.Button("Apagar")) Apagar();
                GUI.enabled = true;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pintar de novo")) Sujar(true);
                if (GUILayout.Button("Gravar a imagem")) ImagemDeLakewood.Gravar();
            }
            EditorGUILayout.Space();

            string caminho = Caminho();
            if (caminho != null && _so != null)
            {
                _so.Update();
                var prop = _so.FindProperty(caminho);
                if (prop != null)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(prop, true);
                    if (EditorGUI.EndChangeCheck() || _so.hasModifiedProperties)
                    {
                        _so.ApplyModifiedProperties();
                        Sujar(MudaORelevo(_camada));
                    }
                }
            }
            else EditorGUILayout.HelpBox("Clique num elemento da camada para escolher.", MessageType.None);

            if (_camada == Camada.Zonas || _camada == Camada.Picos || _camada == Camada.Passagens)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("O relevo geral", EditorStyles.boldLabel);
                _so.Update();
                EditorGUI.BeginChangeCheck();
                var relevo = _so.FindProperty("relevo");
                foreach (var nome in new[] { "altitudeBase", "amplitude", "escala", "alturaDasMontanhas", "asperezaDasMontanhas", "suavidadeDasMontanhas", "aberturaDosVales", "raioDoAplainamento" })
                    EditorGUILayout.PropertyField(relevo.FindPropertyRelative(nome));
                if (EditorGUI.EndChangeCheck() || _so.hasModifiedProperties)
                {
                    _so.ApplyModifiedProperties();
                    Sujar(true);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Clicar: escolher. Arrastar um ponto: mover.\n" +
                "Shift + clicar: pôr um ponto no lado mais perto.\n" +
                "Ctrl + clicar num ponto: tirar.\n" +
                "Alt + arrastar: mover o elemento inteiro.\n" +
                "Botão direito ou do meio: arrastar o mapa. Roda: aproximar.\n" +
                "O ponto azul é o primeiro (nos rios, a nascente). A origem (0, 0) é o canto sudoeste.", MessageType.Info);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void PorUmNovo()
        {
            Undo.RecordObject(_d, "Pôr um elemento no mapa");
            var c = _centro;
            float t = 300f;
            List<Vector2> Linha() => new List<Vector2> { c + new Vector2(-t, 0f), c, c + new Vector2(t, 0f) };
            List<Vector2> Contorno()
            {
                var l = new List<Vector2>();
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    l.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * t);
                }
                return l;
            }
            switch (_camada)
            {
                case Camada.Zonas: _d.relevo.zonas.Add(new ZonaDeRelevo { nome = "Zona", contorno = Contorno() }); break;
                case Camada.Picos: _d.relevo.picos.Add(new PicoDoMapa { nome = "Pico", ponto = c }); break;
                case Camada.Passagens: _d.relevo.passagens.Add(new PassagemDoMapa { nome = "Passagem", pontos = Linha() }); break;
                case Camada.Lagos: _d.lagos.Add(new LagoDoMapa { nome = "Lago", contorno = Contorno(), nivelPeloRelevo = true }); break;
                case Camada.Ilhas: _d.ilhas.Add(new IlhaDoMapa { nome = "Ilhote", contorno = Contorno() }); break;
                case Camada.Rios: _d.rios.Add(new RioDoMapa { nome = "Rio", pontos = Linha() }); break;
                case Camada.Estradas: _d.estradas.Add(new ViaDoMapa { nome = "Estrada", tipo = TipoDeVia.Secundaria, pontos = Linha() }); break;
                case Camada.Pontes: _d.pontes.Add(new PonteDoMapa { nome = "Ponte", perto = c }); break;
                case Camada.Cidades: _d.cidades.Add(new CidadeDoMapa { nome = "Cidade", tipo = TipoDeCidade.Vila, centro = c, contorno = Contorno() }); break;
                case Camada.Lugares: _d.lugares.Add(new LugarDoMapa { nome = "Lugar", ponto = c }); break;
                case Camada.Tuneis: _d.tuneis.Add(new TunelDoMapa { nome = "Túnel", pontos = Linha() }); break;
            }
            EditorUtility.SetDirty(_d);
            _so?.Update();
            Sujar(MudaORelevo(_camada));
        }

        void Apagar()
        {
            if (_sel < 0) return;
            Undo.RecordObject(_d, "Apagar o elemento do mapa");
            Lista(_camada)?.RemoveAt(_sel);
            _sel = _ponto = -1;
            EditorUtility.SetDirty(_d);
            _so?.Update();
            Sujar(MudaORelevo(_camada));
        }
    }
}
