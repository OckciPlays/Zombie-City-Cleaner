using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O jogo de quem está na mesa (só na máquina dele; entra com E e um taco na mão, sai com E ou ESC, guardando o taco,
    /// abrindo o inventário ou se a mesa ficar sem as bolas):
    /// - a câmera do jogador sai do olho e vai para cima da mesa (como a do capô), olhando para baixo, com a mesa inteira na
    ///   tela; o mouse fica solto (<see cref="MenuOnline.Tela"/>) e o corpo fica parado onde estava;
    /// - o mouse mira: a branca vai na direção do ponto do pano embaixo dele. O taco da mão aparece atrás da branca, e a
    ///   linha da mira vai até a primeira bola (ou tabela), com a bola-fantasma onde a branca encosta e o rumo da bola
    ///   acertada;
    /// - segurar o clique enche a força (vai e volta, em <see cref="tempoDaForca"/>), e o taco recua junto; soltar bate.
    ///   O botão direito desiste;
    /// - a tacada é simulada aqui (<see cref="FisicaDaSinuca"/>), com os sons, e vai para os outros em quadros; quando as
    ///   bolas param, o fim vai para o anfitrião. A branca que cai volta para a marca; a preta, ou todas, recomeçam o jogo.
    /// </summary>
    public partial class MesaDeSinuca
    {
        [Header("O jogo (quem está na mesa)")]
        [Tooltip("O campo de visão de cima da mesa (graus, na vertical).")]
        public float campoDeVisao = 50f;
        [Tooltip("Quanto tempo (s) a câmera leva para ir para cima da mesa e para voltar ao olho.")]
        public float tempoEntrar = 0.6f, tempoSair = 0.45f;
        [Tooltip("Quanto tempo (s), segurando o clique, a força leva da mínima à máxima (e depois volta).")]
        public float tempoDaForca = 1.1f;

        static readonly Color CorDoTexto = new Color(0.94f, 0.92f, 0.86f);
        static readonly Color TextoApagado = new Color(0.8f, 0.78f, 0.72f, 0.9f);
        static readonly Color Destaque = new Color(0.98f, 0.82f, 0.36f);
        static readonly Color FundoEscuro = new Color(0.07f, 0.07f, 0.065f, 0.86f);

        // o pedido de jogar (o anfitrião responde pondo o jogador local na mesa)
        ArmaJogador _armaQuePediu;
        float _pediuEm = -10f;

        JogadorRede _rede;
        ArmaJogador _arma;
        TelaDoInventario _telaDoInventario;
        bool _jogando, _saindo;
        float _k, _fovAntes;
        int _entrouNoQuadro;

        readonly FisicaDaSinuca _sim = new FisicaDaSinuca();
        bool _simulando, _esperandoAnfitriao;
        float _esperarAte, _proximoQuadro;
        Vector2 _mira = Vector2.right, _brancaNaTacada;
        bool _carregando;
        float _carga, _forca, _batidaEm = -10f, _forcaDaBatida;
        ushort _foraAntes;
        // o que soou desde o último quadro mandado aos outros
        float _somBola, _somTabela;
        int _somCacapas;
        bool _somTacada;
        string _aviso;
        float _avisoAte;

        // o taco que mira (a malha do taco da mão) e as linhas da mira
        GameObject _taco;
        MeshFilter _malhaDoTaco;
        int _tacoMostrado = -1;
        float _sinalDaPonta = 1f;
        Vector3 _pontaNaMalha;
        LineRenderer _linha, _fantasma, _rumo;
        float _u = 1f, _uDosEstilos;
        GUIStyle _titulo, _texto, _centro;

        void OnDisable() => SairJa();

        // chamado no Update da mesa
        void AtualizarJogo()
        {
            // o anfitrião aceitou o pedido: vai para cima da mesa
            if (!_jogando && EuJogo && _armaQuePediu != null && Time.time - _pediuEm < 3f) Entrar(_armaQuePediu);
            if (!_jogando || _saindo) return;
            var inventario = _arma != null ? _arma.Inventario : null;
            if (_rede == null || !_rede.IsSpawned || inventario == null || !Itens.EhTaco(inventario.NaMao) || !ComBolas.Value || !EuJogo
                || (_telaDoInventario != null && _telaDoInventario.Aberta))
            {
                Sair();
                return;
            }
            var teclado = Keyboard.current;
            if (teclado != null && Time.frameCount > _entrouNoQuadro + 1 && !MenuOnline.Aberto && teclado.eKey.wasPressedThisFrame)
            {
                Sair();
                return;
            }
            if (_simulando)
            {
                Simular();
                return;
            }
            if (_k < 0.95f || _esperandoAnfitriao) return;
            Mirar();
            Tacada();
        }

        void Entrar(ArmaJogador quem)
        {
            _armaQuePediu = null;
            if (quem == null || quem.rede == null || quem.rede.cameraMundo == null) return;
            _arma = quem;
            _rede = quem.rede;
            _telaDoInventario = quem.GetComponent<TelaDoInventario>();
            _jogando = true;
            _saindo = false;
            _k = 0f;
            _fovAntes = _rede.cameraMundo.fieldOfView;
            _entrouNoQuadro = Time.frameCount;
            _carregando = false;
            _forca = 0f;
            MenuOnline.Tela(this, true);
            MenuOnline.FecharTelas += Sair;
            Avisar("Mire com o mouse e segure o clique para a força");
        }

        /// <summary>Sai da mesa: a tacada que ainda andava termina na hora, e a câmera volta para o olho.</summary>
        public void Sair()
        {
            if (!_jogando || _saindo) return;
            if (_simulando)
            {
                _sim.AteParar();
                TerminarTacada();
            }
            _saindo = true;
            _carregando = false;
            MostrarMira(false);
            if (IsSpawned && EuJogo) SairRpc();
        }

        // a câmera voltou ao olho: tudo como antes
        void Terminar()
        {
            if (!_jogando) return;
            var cam = _rede != null ? _rede.cameraMundo : null;
            if (cam != null)
            {
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;
                cam.fieldOfView = _fovAntes;
                if (_rede.cameraBracos != null) _rede.cameraBracos.enabled = _arma == null || !_arma.NaLuneta;
            }
            MenuOnline.Tela(this, false);
            MenuOnline.FecharTelas -= Sair;
            _jogando = false;
            _saindo = false;
            _rede = null;
            _arma = null;
            MostrarMira(false);
        }

        // a mesa sumiu (ou a cena fechou) com alguém nela
        void SairJa()
        {
            _simulando = false;
            if (_jogando) Terminar();
        }

        // ------------------------------------------------------------------ a câmera

        void LateUpdate()
        {
            if (!_jogando) return;
            if (_rede == null || _rede.cameraMundo == null || mesa == null)
            {
                Terminar();
                return;
            }
            _k = Mathf.MoveTowards(_k, _saindo ? 0f : 1f, Time.deltaTime / (_saindo ? tempoSair : tempoEntrar));
            if (_saindo && _k <= 0f)
            {
                Terminar();
                return;
            }
            var cam = _rede.cameraMundo;
            var olho = cam.transform.parent;
            float s = Mathf.SmoothStep(0f, 1f, _k);
            // em cima do meio da mesa, olhando para baixo, com a mesa comprida deitada na tela e inteira nela
            var up = mesa.up;
            var centro = mesa.TransformPoint(new Vector3(0f, alturaDoPano, 0f));
            float escala = mesa.lossyScale.x;
            float meioX = (FisicaDaSinuca.MeioComprimento + 0.24f) * escala, meioZ = (FisicaDaSinuca.MeioLargura + 0.22f) * escala;
            float tanV = Mathf.Tan(campoDeVisao * 0.5f * Mathf.Deg2Rad);
            float altura = Mathf.Max(meioZ / tanV, meioX / (tanV * Mathf.Max(0.5f, cam.aspect))) * 1.04f;
            var pos = centro + up * altura;
            var giro = Quaternion.LookRotation(-up, mesa.forward);
            var de = olho != null ? olho.position : cam.transform.position;
            var deGiro = olho != null ? olho.rotation : cam.transform.rotation;
            cam.transform.SetPositionAndRotation(Vector3.Lerp(de, pos, s), Quaternion.Slerp(deGiro, giro, s));
            cam.fieldOfView = Mathf.Lerp(_fovAntes, campoDeVisao, s);
            if (_rede.cameraBracos != null && _rede.cameraBracos.enabled) _rede.cameraBracos.enabled = false;
            AtualizarMira();
        }

        // ------------------------------------------------------------------ a mira e a tacada

        Vector2 Branca => _alvo[FisicaDaSinuca.Branca];

        // o mouse aponta um ponto do pano: a branca vai para lá
        void Mirar()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var raio = _rede.cameraMundo.ScreenPointToRay(mouse.position.ReadValue());
            var plano = new Plane(mesa.up, mesa.TransformPoint(new Vector3(0f, alturaDoPano + FisicaDaSinuca.Raio, 0f)));
            if (!plano.Raycast(raio, out float t)) return;
            var local = mesa.InverseTransformPoint(raio.GetPoint(t));
            var d = new Vector2(local.x, local.z) - Branca;
            if (d.sqrMagnitude > 1e-6f) _mira = d.normalized;
        }

        void Tacada()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var p = mouse.position.ReadValue();
            var gui = new Vector2(p.x, Screen.height - p.y);
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _carregando = false;
                _forca = 0f;
            }
            if (mouse.leftButton.wasPressedThisFrame && !MenuDoDesenvolvedor.Area.Contains(gui))
            {
                _carregando = true;
                _carga = 0f;
                _forca = 0f;
            }
            if (!_carregando) return;
            if (mouse.leftButton.isPressed)
            {
                _carga += Time.deltaTime;
                _forca = Mathf.PingPong(_carga / tempoDaForca, 1f);
                return;
            }
            _carregando = false;
            if (_forca >= 0.03f) Bater(_forca);
            _forca = 0f;
        }

        /// <summary>Uma tacada na direção (no plano da mesa) com a força (0 a 1), como a do mouse: para os testes.</summary>
        public void Tacar(Vector2 direcao, float forca)
        {
            if (!_jogando || _saindo || _simulando || _esperandoAnfitriao || direcao.sqrMagnitude < 1e-8f) return;
            _mira = direcao.normalized;
            Bater(Mathf.Clamp01(forca));
        }

        void Bater(float forca)
        {
            var pos = new Vector2[FisicaDaSinuca.Bolas];
            for (int i = 0; i < pos.Length; i++) pos[i] = i < _posicoes.Count ? _posicoes[i] : Vector2.zero;
            _sim.Carregar(pos, _fora.Value);
            if (!_sim.Dentro(FisicaDaSinuca.Branca)) _sim.VoltarABranca();
            _foraAntes = _sim.Fora;
            _brancaNaTacada = _sim.Pos[FisicaDaSinuca.Branca];
            _sim.Vel[FisicaDaSinuca.Branca] = _mira * Mathf.Max(0.12f, FisicaDaSinuca.VelocidadeMaxima * Mathf.Pow(forca, 1.3f));
            _simulando = true;
            _proximoQuadro = 0f;
            _batidaEm = Time.time;
            _forcaDaBatida = forca;
            _somTacada = true;
            Efeitos.Som(SomDaSinuca.Tacada, PontoDaBola(_brancaNaTacada), 0.35f + 0.5f * forca, false);
        }

        void Simular()
        {
            _sim.Simular(Mathf.Min(Time.deltaTime, 0.05f));
            // os sons de quem joga (perto da mesa, sem posição)
            if (_sim.BatidaDeBola > 0.01f) Efeitos.Som(SomDaSinuca.Bolas, transform.position, Mathf.Clamp01(_sim.BatidaDeBola * 1.4f) * 0.9f, false);
            if (_sim.BatidaNaTabela > 0.01f) Efeitos.Som(SomDaSinuca.Tabela, transform.position, Mathf.Clamp01(_sim.BatidaNaTabela * 1.4f) * 0.7f, false);
            if (_sim.Cacapas > 0) Efeitos.Som(SomDaSinuca.Cacapa, transform.position, 0.75f, false);
            _somBola = Mathf.Max(_somBola, _sim.BatidaDeBola);
            _somTabela = Mathf.Max(_somTabela, _sim.BatidaNaTabela);
            _somCacapas += _sim.Cacapas;
            _sim.Limpar();
            if (Time.time >= _proximoQuadro)
            {
                _proximoQuadro = Time.time + 0.05f;
                MandarQuadro();
            }
            if (_sim.Parada) TerminarTacada();
        }

        QuadroDaSinuca Quadro() => new QuadroDaSinuca { pos = (Vector2[])_sim.Pos.Clone(), fora = _sim.Fora };

        void MandarQuadro()
        {
            var q = Quadro();
            q.bola = (byte)Mathf.RoundToInt(Mathf.Clamp01(_somBola) * 255f);
            q.tabela = (byte)Mathf.RoundToInt(Mathf.Clamp01(_somTabela) * 255f);
            q.cacapas = (byte)Mathf.Min(255, _somCacapas);
            q.tacada = (byte)(_somTacada ? 1 : 0);
            _somBola = _somTabela = 0f;
            _somCacapas = 0;
            _somTacada = false;
            if (IsSpawned) QuadroRpc(q);
        }

        // as bolas pararam: o que caiu, a branca de volta, o jogo de novo (a preta, ou todas) e o fim para o anfitrião
        void TerminarTacada()
        {
            _simulando = false;
            ushort caidas = (ushort)(_sim.Fora & ~_foraAntes);
            bool preta = (caidas & (1 << FisicaDaSinuca.Preta)) != 0;
            int foraDoJogo = 0;
            for (int i = 1; i < FisicaDaSinuca.Bolas; i++)
                if (!_sim.Dentro(i)) foraDoJogo++;
            bool todas = foraDoJogo >= FisicaDaSinuca.Bolas - 1;
            bool branca = (caidas & 1) != 0;
            string nomes = Nomes((ushort)(caidas & ~1));
            if (preta) Avisar("A preta caiu: o jogo recomeça");
            else if (todas) Avisar("Todas as bolas caíram: o jogo recomeça");
            else
            {
                if (branca) _sim.VoltarABranca();
                if (branca) Avisar(nomes != null ? $"Encaçapou {nomes}, mas a branca caiu: ela volta para a marca" : "A branca caiu: ela volta para a marca");
                else if (nomes != null) Avisar("Encaçapou " + nomes);
            }
            // os outros recebem as posições do fim (com a branca de volta), e esta máquina mostra elas até o anfitrião gravar
            MandarQuadro();
            for (int i = 0; i < FisicaDaSinuca.Bolas; i++) _alvo[i] = _sim.Pos[i];
            _foraDoAlvo = _sim.Fora;
            _esperandoAnfitriao = true;
            _esperarAte = Time.time + 2f;
            if (IsSpawned) FimRpc(Quadro(), preta || todas);
        }

        // "a 3", "a 3 e a 11", "a 3, a 5 e a 11" (null, nenhuma)
        static string Nomes(ushort mascara)
        {
            var lista = new System.Collections.Generic.List<string>();
            for (int i = 1; i < FisicaDaSinuca.Bolas; i++)
                if ((mascara & (1 << i)) != 0) lista.Add("a " + i);
            if (lista.Count == 0) return null;
            if (lista.Count == 1) return lista[0];
            return string.Join(", ", lista.GetRange(0, lista.Count - 1)) + " e " + lista[lista.Count - 1];
        }

        void Avisar(string texto)
        {
            _aviso = texto;
            _avisoAte = Time.time + 3f;
        }

        Vector3 PontoDaBola(Vector2 p) => mesa.TransformPoint(new Vector3(p.x, alturaDoPano + FisicaDaSinuca.Raio, p.y));

        // ------------------------------------------------------------------ o taco e as linhas

        void AtualizarMira()
        {
            bool mostrar = !_saindo && _k > 0.9f && !_esperandoAnfitriao && (!_simulando || Time.time - _batidaEm < 0.12f)
                           && _arma != null && _arma.Inventario != null && Itens.EhTaco(_arma.Inventario.NaMao);
            MostrarMira(mostrar);
            if (!mostrar) return;
            Taco(Itens.IndiceDoTaco(_arma.Inventario.NaMao));
            var b = _simulando ? _brancaNaTacada : Branca;
            float recuo;
            if (_simulando)
            {
                // a tacada: o taco vai para a frente num instante e passa um pouco do lugar da branca
                float t = Mathf.Clamp01((Time.time - _batidaEm) / 0.06f);
                recuo = Mathf.Lerp(0.012f + _forcaDaBatida * 0.22f, -0.01f, t);
            }
            else recuo = 0.012f + (_carregando ? _forca * 0.22f : 0f);
            PorOTaco(b, _mira, recuo);
            if (_simulando)
            {
                _linha.enabled = _fantasma.enabled = _rumo.enabled = false;
                return;
            }
            Linhas(b, _mira);
        }

        void MostrarMira(bool mostrar)
        {
            if (_taco != null && _taco.activeSelf != mostrar) _taco.SetActive(mostrar);
            foreach (var l in new[] { _linha, _fantasma, _rumo })
                if (l != null && l.enabled != mostrar) l.enabled = mostrar;
        }

        // o taco da mão (a malha dele, do chão), filho da mesa (no tamanho dela), e para que lado está a ponta
        void Taco(int qual)
        {
            if (_taco == null)
            {
                _taco = new GameObject("Taco que mira");
                _taco.transform.SetParent(mesa, false);
                _malhaDoTaco = _taco.AddComponent<MeshFilter>();
                var r = _taco.AddComponent<MeshRenderer>();
                r.sharedMaterial = materialDoTaco;
                r.shadowCastingMode = ShadowCastingMode.On;
                _linha = NovaLinha("Linha da mira", 2, false);
                _fantasma = NovaLinha("Bola-fantasma", 24, true);
                _rumo = NovaLinha("Rumo da bola", 2, false);
            }
            if (qual == _tacoMostrado || tacos == null || qual < 0 || qual >= tacos.Length || tacos[qual] == null) return;
            _tacoMostrado = qual;
            var malha = tacos[qual];
            _malhaDoTaco.sharedMesh = malha;
            // a ponta é a ponta mais fina (o taco está deitado ao longo do X da malha)
            var b = malha.bounds;
            var vs = malha.vertices;
            float raioMin = 0f, raioMax = 0f;
            int nMin = 0, nMax = 0;
            foreach (var v in vs)
            {
                float r = new Vector2(v.y - b.center.y, v.z - b.center.z).magnitude;
                if (v.x < b.min.x + 0.03f) { raioMin += r; nMin++; }
                if (v.x > b.max.x - 0.03f) { raioMax += r; nMax++; }
            }
            bool pontaNoMax = nMin == 0 || (nMax > 0 && raioMax / nMax < raioMin / nMin);
            _sinalDaPonta = pontaNoMax ? 1f : -1f;
            _pontaNaMalha = new Vector3(pontaNoMax ? b.max.x : b.min.x, b.center.y, b.center.z);
        }

        // a ponta do taco atrás da branca, na direção da mira, com o cabo um pouco levantado (passa por cima da tabela)
        void PorOTaco(Vector2 branca, Vector2 d, float recuo)
        {
            float inclinacao = 6f * Mathf.Deg2Rad;
            var eixo = (new Vector3(d.x, 0f, d.y) * Mathf.Cos(inclinacao) - Vector3.up * Mathf.Sin(inclinacao)).normalized;
            var ponta = new Vector3(branca.x, alturaDoPano + FisicaDaSinuca.Raio, branca.y) - eixo * (FisicaDaSinuca.Raio + recuo);
            var giro = Quaternion.FromToRotation(new Vector3(_sinalDaPonta, 0f, 0f), eixo);
            _taco.transform.localRotation = giro;
            _taco.transform.localPosition = ponta - giro * _pontaNaMalha;
        }

        LineRenderer NovaLinha(string nome, int pontos, bool fechada)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<LineRenderer>();
            l.useWorldSpace = true;
            l.sharedMaterial = materialDaLinha;
            l.widthMultiplier = 0.005f * mesa.lossyScale.x;
            l.positionCount = pontos;
            l.loop = fechada;
            l.numCapVertices = 2;
            l.shadowCastingMode = ShadowCastingMode.Off;
            l.receiveShadows = false;
            l.enabled = false;
            return l;
        }

        // a linha da branca até onde ela encosta (na primeira bola ou na tabela), a bola-fantasma ali e o rumo da bola
        // acertada (ao longo da linha dos centros)
        void Linhas(Vector2 p0, Vector2 d)
        {
            float r = FisicaDaSinuca.Raio;
            float lx = FisicaDaSinuca.MeioComprimento - r, ly = FisicaDaSinuca.MeioLargura - r;
            float t = float.MaxValue;
            if (d.x > 1e-6f) t = Mathf.Min(t, (lx - p0.x) / d.x);
            else if (d.x < -1e-6f) t = Mathf.Min(t, (-lx - p0.x) / d.x);
            if (d.y > 1e-6f) t = Mathf.Min(t, (ly - p0.y) / d.y);
            else if (d.y < -1e-6f) t = Mathf.Min(t, (-ly - p0.y) / d.y);
            t = Mathf.Max(0f, t);
            int acertada = -1;
            for (int j = 1; j < FisicaDaSinuca.Bolas; j++)
            {
                if ((_foraDoAlvo & (1 << j)) != 0) continue;
                var m = p0 - _alvo[j];
                float b = Vector2.Dot(d, m), c = m.sqrMagnitude - 4f * r * r;
                float disc = b * b - c;
                if (disc < 0f) continue;
                float tj = -b - Mathf.Sqrt(disc);
                if (tj > 0f && tj < t)
                {
                    t = tj;
                    acertada = j;
                }
            }
            var contato = p0 + d * t;
            float y = alturaDoPano + r * 0.15f;
            Vector3 Ponto(Vector2 q) => mesa.TransformPoint(new Vector3(q.x, y, q.y));
            _linha.SetPosition(0, Ponto(p0 + d * r));
            _linha.SetPosition(1, Ponto(contato - d * r));
            for (int i = 0; i < _fantasma.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / _fantasma.positionCount;
                _fantasma.SetPosition(i, Ponto(contato + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r));
            }
            if (acertada >= 0)
            {
                var c = _alvo[acertada];
                var n = (c - contato).normalized;
                _rumo.enabled = true;
                _rumo.SetPosition(0, Ponto(c + n * r));
                _rumo.SetPosition(1, Ponto(c + n * (r + 0.3f)));
            }
            else _rumo.enabled = false;
        }

        // ------------------------------------------------------------------ a tela

        void OnGUI()
        {
            if (!_jogando || _saindo || MenuOnline.Aberto) return;
            float a = Mathf.Clamp01((_k - 0.6f) / 0.4f);
            if (a <= 0f) return;
            _u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos();
            float u = _u;
            var antes = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);

            // em cima, à esquerda: o que ainda está na mesa
            ushort fora = _simulando ? _sim.Fora : _foraDoAlvo;
            int lisas = 0, listradas = 0;
            for (int i = 1; i < FisicaDaSinuca.Bolas; i++)
            {
                if ((fora & (1 << i)) != 0 || i == FisicaDaSinuca.Preta) continue;
                if (i < FisicaDaSinuca.Preta) lisas++;
                else listradas++;
            }
            // (abaixo da linha da partida, que fica no canto)
            var r = new Rect(24 * u, 58 * u, 380 * u, 72 * u);
            Ret(r, FundoEscuro);
            GUI.Label(new Rect(r.x + 14 * u, r.y + 8 * u, r.width - 28 * u, 24 * u), "SINUCA", _titulo);
            GUI.Label(new Rect(r.x + 14 * u, r.y + 38 * u, r.width - 28 * u, 24 * u),
                $"Na mesa: {lisas} lisas, {listradas} listradas e a preta", _texto);

            // embaixo: a força (segurando o clique), o aviso e o que fazer (acima da barra de atalhos e do nome do que
            // está na mão)
            float yBase = Screen.height - 150 * u;
            if (_carregando)
            {
                var barra = new Rect(Screen.width / 2f - 160 * u, yBase - 58 * u, 320 * u, 16 * u);
                Ret(new Rect(barra.x - 3 * u, barra.y - 3 * u, barra.width + 6 * u, barra.height + 6 * u), FundoEscuro);
                Ret(new Rect(barra.x, barra.y, barra.width * _forca, barra.height), Color.Lerp(new Color(0.55f, 0.75f, 0.45f), new Color(0.95f, 0.45f, 0.3f), _forca));
                GUI.Label(new Rect(barra.x, barra.y - 22 * u, barra.width, 20 * u), "FORÇA", _centro);
            }
            if (Time.time < _avisoAte && _aviso != null)
            {
                var estilo = new GUIStyle(_centro);
                estilo.normal.textColor = Destaque;
                Faixa(_aviso, yBase - 26 * u, estilo);
            }
            Faixa(_simulando ? "As bolas estão andando..." : _esperandoAnfitriao ? "..."
                : "Mouse: mirar   ·   segure o clique: força, solte: bater   ·   botão direito: desistir   ·   E ou ESC: sair", yBase + 8 * u, _centro);
            GUI.color = antes;
        }

        void Faixa(string texto, float y, GUIStyle estilo)
        {
            float u = _u;
            var tamanho = estilo.CalcSize(new GUIContent(texto));
            var r = new Rect((Screen.width - tamanho.x) / 2f - 16 * u, y, tamanho.x + 32 * u, 30 * u);
            Ret(r, FundoEscuro);
            GUI.Label(r, texto, estilo);
        }

        static void Ret(Rect r, Color c)
        {
            var antes = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * antes.a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
        }

        void Estilos()
        {
            if (_titulo != null && Mathf.Approximately(_uDosEstilos, _u)) return;
            _uDosEstilos = _u;
            float u = _u;
            GUIStyle Estilo(int tamanho, FontStyle fonte, TextAnchor ancora, Color cor)
            {
                var s = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(tamanho * u), fontStyle = fonte, alignment = ancora, wordWrap = false };
                s.normal.textColor = cor;
                s.padding = new RectOffset(0, 0, 0, 0);
                return s;
            }
            _titulo = Estilo(16, FontStyle.Bold, TextAnchor.MiddleLeft, CorDoTexto);
            _texto = Estilo(14, FontStyle.Normal, TextAnchor.MiddleLeft, TextoApagado);
            _centro = Estilo(14, FontStyle.Bold, TextAnchor.MiddleCenter, CorDoTexto);
        }
    }
}
