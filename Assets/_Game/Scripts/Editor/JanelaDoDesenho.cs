using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A janela do desenho do mapa fixo (Plano_Mapa_Fixo, Parte F1: "a ferramenta do Editor para ver o desenho de cima e
    /// mover os pontos"). Menu Zombie City Cleaner > Mapa fixo > Desenho do mapa. O mapa pintado (<see cref="PintorDoDesenho"/>,
    /// 2048 px, pintado em outra thread a partir de uma cópia do desenho) com os nomes por cima, e a camada escolhida em
    /// linhas: clicar escolhe um elemento, arrastar move o ponto, Shift + clicar põe um ponto no lado mais perto, Ctrl +
    /// clicar tira o ponto, Alt + arrastar move o elemento inteiro (a cidade inteira, com os distritos e as avenidas). O
    /// botão direito (ou o do meio) arrasta o mapa, e a roda aproxima. Tudo vai para o Desfazer. Do lado, os campos do
    /// elemento escolhido, e os botões de pôr, apagar, pintar de novo e gravar a imagem.
    /// </summary>
    public sealed class JanelaDoDesenho : EditorWindow
    {
        enum Camada { Quarentena, Bloqueios, Serras, Picos, Passagens, Planicies, Lagos, Ilhas, Rios, Estradas, Ferrovia, Pontes, Cidades, Avenidas, Lugares, Zonas, Regioes, Familia }

        const int LadoDaPintura = 2048;
        const float LarguraDoPainel = 340f, Perto = 12f;

        DesenhoDoMapa _d;
        SerializedObject _so;
        Texture2D _tex;
        RelevoDoDesenho _relevo;
        List<RotuloDoDesenho> _rotulos = new List<RotuloDoDesenho>();
        Task<(RelevoDoDesenho relevo, Color32[] px, List<RotuloDoDesenho> rotulos)> _tarefa;
        DesenhoDoMapa _copia;
        bool _sujo = true, _relevoSujo = true, _nomes = true;
        double _quando;

        Vector2 _centro;
        float _zoom = -1f;
        Camada _camada = Camada.Cidades;
        int _sel = -1, _sub = -1, _ponto = -1;
        bool _arrastando, _movendoTudo;
        Vector2 _mouseNoMundo, _rolagem;
        GUIStyle _rotulo;

        [MenuItem("Zombie City Cleaner/Mapa fixo/Desenho do mapa")]
        public static void Abrir() => GetWindow<JanelaDoDesenho>("Desenho do mapa");

        void OnEnable()
        {
            wantsMouseMove = true;
            _d = AssetDatabase.LoadAssetAtPath<DesenhoDoMapa>(DesenhoDoMapa.Caminho);
            if (_d != null) _so = new SerializedObject(_d);
            Undo.undoRedoPerformed += Desfeito;
            EditorApplication.update += Atualizar;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= Desfeito;
            EditorApplication.update -= Atualizar;
            if (_tex != null) DestroyImmediate(_tex);
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

        static bool MudaORelevo(Camada c) => c is Camada.Serras or Camada.Picos or Camada.Passagens or Camada.Planicies
                                              or Camada.Lagos or Camada.Ilhas or Camada.Rios;

        // a pintura em outra thread: começa quando o desenho mudou e ninguém está arrastando; acaba aqui, na principal
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
                    if (_tex == null) _tex = new Texture2D(LadoDaPintura, LadoDaPintura, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
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
                    var r = relevoPronto ?? new RelevoDoDesenho(copia);
                    var px = PintorDoDesenho.Pintar(copia, r, LadoDaPintura);
                    return (r, px, PintorDoDesenho.Rotulos(copia, r));
                });
            }
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
                EditorGUILayout.HelpBox($"Não achei o desenho em {DesenhoDoMapa.Caminho}.", MessageType.Error);
                if (GUILayout.Button("Procurar de novo")) OnEnable();
                return;
            }
            var area = AreaDoMapa;
            if (_zoom <= 0f) _zoom = Mathf.Min(area.width, area.height) / DesenhoDoMapa.Lado;
            var e = Event.current;
            if (area.Contains(e.mousePosition)) _mouseNoMundo = MundoDe(e.mousePosition);

            EditorGUI.DrawRect(area, new Color(0.1f, 0.11f, 0.12f));
            if (_tex != null)
            {
                var a = TelaDe(new Vector2(-DesenhoDoMapa.Metade, DesenhoDoMapa.Metade));
                float lado = DesenhoDoMapa.Lado * _zoom;
                GUI.DrawTexture(new Rect(a.x, a.y, lado, lado), _tex);
            }
            if (_nomes) Nomes();
            DesenharAsLinhas();
            Mouse(e, area);
            Painel();
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

        static bool DePonto(Camada c) => c is Camada.Bloqueios or Camada.Picos or Camada.Pontes or Camada.Lugares or Camada.Regioes or Camada.Familia;

        static bool Fechada(Camada c) => c is Camada.Quarentena or Camada.Planicies or Camada.Lagos or Camada.Ilhas or Camada.Cidades or Camada.Zonas;

        /// <summary>Os elementos da camada: (o índice, o sub-índice) — nas cidades, o distrito; nas avenidas, a avenida.</summary>
        IEnumerable<(int el, int sub)> Elementos(Camada c)
        {
            switch (c)
            {
                case Camada.Quarentena: case Camada.Ferrovia: case Camada.Familia: yield return (0, -1); break;
                case Camada.Cidades:
                    for (int i = 0; i < _d.cidades.Count; i++)
                    for (int s = 0; s < _d.cidades[i].distritos.Count; s++) yield return (i, s);
                    break;
                case Camada.Avenidas:
                    for (int i = 0; i < _d.cidades.Count; i++)
                    for (int s = 0; s < _d.cidades[i].avenidas.Count; s++) yield return (i, s);
                    break;
                default:
                    for (int i = 0; i < Lista(c).Count; i++) yield return (i, -1);
                    break;
            }
        }

        System.Collections.IList Lista(Camada c) => c switch
        {
            Camada.Bloqueios => _d.bloqueios,
            Camada.Serras => _d.relevo.serras,
            Camada.Picos => _d.relevo.picos,
            Camada.Passagens => _d.relevo.passagens,
            Camada.Planicies => _d.relevo.planicies,
            Camada.Lagos => _d.lagos,
            Camada.Ilhas => _d.ilhas,
            Camada.Rios => _d.rios,
            Camada.Estradas => _d.estradas,
            Camada.Pontes => _d.pontes,
            Camada.Cidades => _d.cidades,
            Camada.Lugares => _d.lugares,
            Camada.Zonas => _d.zonas,
            Camada.Regioes => _d.regioes,
            _ => null,
        };

        // os pontos de um elemento de linha (a mesma lista do desenho: mexer nela mexe no desenho)
        List<Vector2> Pontos(Camada c, int el, int sub) => c switch
        {
            Camada.Quarentena => _d.quarentena,
            Camada.Ferrovia => _d.ferrovia,
            Camada.Serras => _d.relevo.serras[el].crista,
            Camada.Passagens => _d.relevo.passagens[el].pontos,
            Camada.Planicies => _d.relevo.planicies[el].contorno,
            Camada.Lagos => _d.lagos[el].contorno,
            Camada.Ilhas => _d.ilhas[el].contorno,
            Camada.Rios => _d.rios[el].pontos,
            Camada.Estradas => _d.estradas[el].pontos,
            Camada.Cidades => _d.cidades[el].distritos[sub].contorno,
            Camada.Avenidas => _d.cidades[el].avenidas[sub].pontos,
            Camada.Zonas => _d.zonas[el].contorno,
            _ => null,
        };

        Vector2 Ponto(Camada c, int el) => c switch
        {
            Camada.Bloqueios => _d.bloqueios[el].ponto,
            Camada.Picos => _d.relevo.picos[el].ponto,
            Camada.Pontes => _d.pontes[el].perto,
            Camada.Lugares => _d.lugares[el].ponto,
            Camada.Regioes => _d.regioes[el].ponto,
            _ => _d.casaDaFamilia,
        };

        void PorPonto(Camada c, int el, Vector2 p)
        {
            switch (c)
            {
                case Camada.Bloqueios: _d.bloqueios[el].ponto = p; break;
                case Camada.Picos: _d.relevo.picos[el].ponto = p; break;
                case Camada.Pontes: _d.pontes[el].perto = p; break;
                case Camada.Lugares: _d.lugares[el].ponto = p; break;
                case Camada.Regioes: _d.regioes[el].ponto = p; break;
                case Camada.Familia: _d.casaDaFamilia = p; break;
            }
        }

        // ------------------------------------------------------------------ as linhas da camada

        void DesenharAsLinhas()
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (var (el, sub) in Elementos(_camada))
            {
                bool escolhido = el == _sel && sub == _sub;
                var cor = escolhido ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f, 0.55f);
                if (DePonto(_camada))
                {
                    var p = TelaDe(Ponto(_camada, el));
                    EditorGUI.DrawRect(new Rect(p.x - 5f, p.y - 5f, 10f, 10f), Color.black);
                    EditorGUI.DrawRect(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), cor);
                    continue;
                }
                var pts = Pontos(_camada, el, sub);
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
            bool temEscolhido = _sel >= 0 && Existe(_sel, _sub);
            if (temEscolhido && e.alt)
            {
                Undo.RecordObject(_d, "Mover o elemento do desenho");
                _arrastando = _movendoTudo = true;
                return;
            }
            if (temEscolhido && !DePonto(_camada))
            {
                var pts = Pontos(_camada, _sel, _sub);
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
                        Undo.RecordObject(_d, "Tirar o ponto do desenho");
                        pts.RemoveAt(perto);
                        _ponto = -1;
                        EditorUtility.SetDirty(_d);
                        Sujar(MudaORelevo(_camada));
                    }
                    return;
                }
                if (perto >= 0)
                {
                    Undo.RecordObject(_d, "Mover o ponto do desenho");
                    _ponto = perto;
                    _arrastando = true;
                    return;
                }
                if (e.shift)
                {
                    // o ponto novo no lado mais perto; nas linhas abertas, depois da última ponta (ou antes da primeira)
                    // quando o clique fica além dela
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
                    Undo.RecordObject(_d, "Pôr um ponto no desenho");
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
                Undo.RecordObject(_d, "Mover o ponto do desenho");
                _arrastando = true;
                return;
            }
            Escolher(w, e.mousePosition);
        }

        bool Existe(int el, int sub)
        {
            foreach (var x in Elementos(_camada))
                if (x.el == el && x.sub == sub) return true;
            return false;
        }

        // escolhe o elemento mais perto do clique (nas áreas fechadas, clicar dentro também vale)
        void Escolher(Vector2 w, Vector2 tela)
        {
            float melhor = Perto;
            int sel = -1, sub = -1;
            foreach (var (el, s) in Elementos(_camada))
            {
                float dd;
                if (DePonto(_camada)) dd = Vector2.Distance(TelaDe(Ponto(_camada, el)), tela);
                else
                {
                    var pts = Pontos(_camada, el, s);
                    if (pts == null || pts.Count == 0) continue;
                    dd = Linhas.Distancia(w, pts, Fechada(_camada)) * _zoom;
                    if (Fechada(_camada) && pts.Count >= 3 && Linhas.Dentro(w, pts)) dd = Mathf.Min(dd, Perto * 0.5f + Linhas.Area(pts) * 1e-9f);
                }
                if (dd < melhor)
                {
                    melhor = dd;
                    sel = el;
                    sub = s;
                }
            }
            _sel = sel;
            _sub = sub;
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
                var pts = Pontos(_camada, _sel, _sub);
                if (_ponto < pts.Count) pts[_ponto] = w;
            }
            EditorUtility.SetDirty(_d);
            Repaint();
        }

        // o elemento inteiro (a cidade inteira, se a camada for a das cidades ou das avenidas)
        void Mover(Vector2 delta)
        {
            void Todos(List<Vector2> l)
            {
                for (int i = 0; i < l.Count; i++) l[i] += delta;
            }
            switch (_camada)
            {
                case Camada.Cidades: case Camada.Avenidas:
                    var c = _d.cidades[_sel];
                    c.centro += delta;
                    foreach (var dis in c.distritos)
                    {
                        Todos(dis.contorno);
                        if (dis.rotulo != Vector2.zero) dis.rotulo += delta;
                    }
                    foreach (var a in c.avenidas) Todos(a.pontos);
                    break;
                case Camada.Lagos:
                    Todos(_d.lagos[_sel].contorno);
                    if (_d.lagos[_sel].rotulo != Vector2.zero) _d.lagos[_sel].rotulo += delta;
                    break;
                case Camada.Zonas:
                    Todos(_d.zonas[_sel].contorno);
                    if (_d.zonas[_sel].rotulo != Vector2.zero) _d.zonas[_sel].rotulo += delta;
                    break;
                default:
                    if (DePonto(_camada)) PorPonto(_camada, _sel, Ponto(_camada, _sel) + delta);
                    else Todos(Pontos(_camada, _sel, _sub));
                    break;
            }
        }

        // ------------------------------------------------------------------ o painel

        string Caminho()
        {
            if (_sel < 0) return null;
            return _camada switch
            {
                Camada.Quarentena => "quarentena",
                Camada.Ferrovia => "ferrovia",
                Camada.Familia => "casaDaFamilia",
                Camada.Bloqueios => $"bloqueios.Array.data[{_sel}]",
                Camada.Serras => $"relevo.serras.Array.data[{_sel}]",
                Camada.Picos => $"relevo.picos.Array.data[{_sel}]",
                Camada.Passagens => $"relevo.passagens.Array.data[{_sel}]",
                Camada.Planicies => $"relevo.planicies.Array.data[{_sel}]",
                Camada.Lagos => $"lagos.Array.data[{_sel}]",
                Camada.Ilhas => $"ilhas.Array.data[{_sel}]",
                Camada.Rios => $"rios.Array.data[{_sel}]",
                Camada.Estradas => $"estradas.Array.data[{_sel}]",
                Camada.Pontes => $"pontes.Array.data[{_sel}]",
                Camada.Cidades => $"cidades.Array.data[{_sel}].distritos.Array.data[{_sub}]",
                Camada.Avenidas => $"cidades.Array.data[{_sel}].avenidas.Array.data[{_sub}]",
                Camada.Lugares => $"lugares.Array.data[{_sel}]",
                Camada.Zonas => $"zonas.Array.data[{_sel}]",
                Camada.Regioes => $"regioes.Array.data[{_sel}]",
                _ => null,
            };
        }

        void Painel()
        {
            var r = new Rect(position.width - LarguraDoPainel, 0f, LarguraDoPainel, position.height);
            EditorGUI.DrawRect(r, new Color(0.19f, 0.19f, 0.19f));
            GUILayout.BeginArea(new Rect(r.x + 8f, 8f, r.width - 16f, r.height - 16f));
            _rolagem = GUILayout.BeginScrollView(_rolagem);
            EditorGUILayout.LabelField("Desenho do mapa fixo", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_tarefa != null ? "Pintando..." : $"x {_mouseNoMundo.x:F0}   z {_mouseNoMundo.y:F0}   ({_zoom * 1000f:F1} px por km)", EditorStyles.miniLabel);
            var nova = (Camada)EditorGUILayout.EnumPopup("Camada", _camada);
            if (nova != _camada)
            {
                _camada = nova;
                _sel = _sub = _ponto = -1;
                if (_camada is Camada.Quarentena or Camada.Ferrovia or Camada.Familia) _sel = 0;
            }
            _nomes = EditorGUILayout.Toggle("Mostrar os nomes", _nomes);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = Lista(_camada) != null || _camada == Camada.Avenidas;
                if (GUILayout.Button("Pôr um novo")) PorUmNovo();
                GUI.enabled = _sel >= 0 && (Lista(_camada) != null || _camada == Camada.Avenidas);
                if (GUILayout.Button("Apagar")) Apagar();
                GUI.enabled = true;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pintar de novo")) Sujar(true);
                if (GUILayout.Button("Gravar a imagem")) ImagemDoDesenho.Gravar();
            }
            EditorGUILayout.Space();

            string caminho = Caminho();
            if (caminho != null && _so != null)
            {
                _so.Update();
                if (_camada is Camada.Cidades or Camada.Avenidas)
                {
                    var cidade = _so.FindProperty($"cidades.Array.data[{_sel}]");
                    if (cidade != null)
                    {
                        EditorGUILayout.LabelField("A cidade", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(cidade.FindPropertyRelative("nome"));
                        EditorGUILayout.PropertyField(cidade.FindPropertyRelative("tipo"));
                        EditorGUILayout.PropertyField(cidade.FindPropertyRelative("centro"));
                        EditorGUILayout.PropertyField(cidade.FindPropertyRelative("rotulo"));
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField(_camada == Camada.Cidades ? "O distrito" : "A avenida", EditorStyles.boldLabel);
                    }
                }
                var prop = _so.FindProperty(caminho);
                if (prop != null)
                {
                    EditorGUI.BeginChangeCheck();
                    if (_camada == Camada.Familia)
                    {
                        EditorGUILayout.PropertyField(prop);
                        EditorGUILayout.PropertyField(_so.FindProperty("rotuloDaFamilia"));
                    }
                    else EditorGUILayout.PropertyField(prop, true);
                    if (EditorGUI.EndChangeCheck() || _so.hasModifiedProperties)
                    {
                        _so.ApplyModifiedProperties();
                        Sujar(MudaORelevo(_camada));
                    }
                }
            }
            else EditorGUILayout.HelpBox("Clique num elemento da camada para escolher.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Clicar: escolher. Arrastar um ponto: mover.\n" +
                "Shift + clicar: pôr um ponto no lado mais perto.\n" +
                "Ctrl + clicar num ponto: tirar.\n" +
                "Alt + arrastar: mover o elemento inteiro (nas cidades, a cidade toda).\n" +
                "Botão direito ou do meio: arrastar o mapa. Roda: aproximar.\n" +
                "O ponto azul é o primeiro (nos rios, a nascente).", MessageType.Info);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void PorUmNovo()
        {
            Undo.RecordObject(_d, "Pôr um elemento no desenho");
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
                case Camada.Bloqueios: _d.bloqueios.Add(new BloqueioDoDesenho { nome = "Bloqueio", ponto = c }); break;
                case Camada.Serras: _d.relevo.serras.Add(new SerraDoDesenho { nome = "Serra", crista = Linha() }); break;
                case Camada.Picos: _d.relevo.picos.Add(new PicoDoDesenho { nome = "Pico", ponto = c }); break;
                case Camada.Passagens: _d.relevo.passagens.Add(new PassagemDoDesenho { nome = "Passagem", pontos = Linha() }); break;
                case Camada.Planicies: _d.relevo.planicies.Add(new PlanicieDoDesenho { contorno = Contorno() }); break;
                case Camada.Lagos: _d.lagos.Add(new LagoDoDesenho { nome = "Lago", contorno = Contorno() }); break;
                case Camada.Ilhas: _d.ilhas.Add(new IlhaDoDesenho { nome = "", contorno = Contorno() }); break;
                case Camada.Rios: _d.rios.Add(new RioDoDesenho { nome = "Rio", pontos = Linha() }); break;
                case Camada.Estradas: _d.estradas.Add(new ViaDoDesenho { nome = "Estrada", tipo = TipoDeVia.Condado, pontos = Linha() }); break;
                case Camada.Pontes: _d.pontes.Add(new PonteDoDesenho { nome = "Ponte", perto = c }); break;
                case Camada.Cidades:
                    if (_sel >= 0) _d.cidades[_sel].distritos.Add(new DistritoDoDesenho { nome = "", tipo = TipoDeDistrito.Bairro, contorno = Contorno() });
                    else _d.cidades.Add(new CidadeDoDesenho { nome = "Cidade", tipo = TipoDeCidade.Vila, centro = c, rotulo = new Vector2(0f, 400f),
                                                              distritos = new List<DistritoDoDesenho> { new DistritoDoDesenho { tipo = TipoDeDistrito.Bairro, contorno = Contorno() } } });
                    break;
                case Camada.Avenidas:
                    if (_sel >= 0) _d.cidades[_sel].avenidas.Add(new AvenidaDoDesenho { nome = "Avenida", pontos = Linha() });
                    break;
                case Camada.Lugares: _d.lugares.Add(new LugarDoDesenho { nome = "Lugar", ponto = c }); break;
                case Camada.Zonas: _d.zonas.Add(new ZonaDoDesenho { nome = "", contorno = Contorno() }); break;
                case Camada.Regioes: _d.regioes.Add(new RegiaoDoDesenho { nome = "REGIÃO", ponto = c }); break;
            }
            EditorUtility.SetDirty(_d);
            _so?.Update();
            Sujar(MudaORelevo(_camada));
        }

        void Apagar()
        {
            if (_sel < 0) return;
            Undo.RecordObject(_d, "Apagar o elemento do desenho");
            switch (_camada)
            {
                case Camada.Cidades:
                    var c = _d.cidades[_sel];
                    if (c.distritos.Count > 1) c.distritos.RemoveAt(_sub);
                    else _d.cidades.RemoveAt(_sel);
                    break;
                case Camada.Avenidas:
                    _d.cidades[_sel].avenidas.RemoveAt(_sub);
                    break;
                default:
                    Lista(_camada)?.RemoveAt(_sel);
                    break;
            }
            _sel = _sub = _ponto = -1;
            EditorUtility.SetDirty(_d);
            _so?.Update();
            Sujar(MudaORelevo(_camada));
        }
    }
}
