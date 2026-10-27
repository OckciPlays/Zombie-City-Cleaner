using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel do mundo grande (a cena Mundo, Plano_Mapa Partes M1 e M2), ao lado do menu do desenvolvedor: o nome, a
    /// semente, o tamanho, a versão e o resumo do mundo; onde você está (a altura do chão e o bloco) e quantos blocos de
    /// terreno estão montados; voar (F2); ir para um ponto (as coordenadas, a partida, o lago, o meio e as bordas); trazer o
    /// carro; o teste de travessia (voar baixo pelo mapa a 160 km/h, medindo os quadros); o benchmark da cidade (M5); e a
    /// conferência das casas por perto (M6).
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        string _irX = "0", _irZ = "0";
        float _esquecerDestinoEm;
        readonly object _pedidoDoDestino = new object();

        // o teste de travessia
        bool _atravessando;
        Vector2 _travessiaDe, _travessiaDirecao;
        float _travessiaComecou, _travessiaPercorrido, _travessiaVelocidade;
        int _semChao;
        readonly List<float> _quadros = new List<float>();
        const float VelocidadeDaTravessia = 44.4f, AlturaDaTravessia = 2.5f, DuracaoDaTravessia = 60f;

        MovimentoPrimeiraPessoa Movimento => GetComponent<MovimentoPrimeiraPessoa>();

        static DadosDoMundo Mapa => MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;

        void AtalhosDoMundo(Keyboard teclado)
        {
            if (Mapa == null) return;
            if (_esquecerDestinoEm > 0f && Time.time > _esquecerDestinoEm)
            {
                _esquecerDestinoEm = 0f;
                if (TerrenoDoMundo.Instancia != null) TerrenoDoMundo.Instancia.Esquecer(_pedidoDoDestino);
            }
            if (_atravessando) Atravessar();
            if (teclado == null || MenuOnline.Aberto) return;
            if (teclado.f2Key.wasPressedThisFrame) Voar(!Movimento.Voando);
        }

        void Voar(bool voar)
        {
            Movimento.Voando = voar;
            Avisar(voar ? "Voando: Espaço sobe, Ctrl desce, Shift acelera. F2 desliga." : "Andando de novo.");
        }

        void IrPara(float x, float z)
        {
            var d = Mapa;
            float limite = d.Cerca - 5f;
            x = Mathf.Clamp(x, -limite, limite);
            z = Mathf.Clamp(z, -limite, limite);
            float chao = d.Altura(x, z) + 0.2f;
            // o chão do destino começa a carregar já; se o jogador chegar antes da colisão, o MundoDaPartida o devolve à superfície
            TerrenoDoMundo.Instancia?.Pedir(_pedidoDoDestino, new Vector3(x, chao, z), 40f, true);
            _esquecerDestinoEm = Time.time + 10f;
            GetComponent<PassageiroDoCarro>().Soltar();
            Movimento.Teleportar(new Vector3(x, Movimento.Voando ? Mathf.Max(transform.position.y, chao) : chao, z), transform.eulerAngles.y);
            Avisar($"Em x {x:F0}, z {z:F0}.");
        }

        /// <summary>
        /// O teste de travessia: voa a 2,5 m do chão, a 160 km/h (o carro no máximo), do ponto onde está para o lado mais
        /// longe do mapa, por 60 s ou até a borda, medindo cada quadro. No fim, o log diz a média, o 1% pior e o pior quadro.
        /// </summary>
        public void ComecarTravessia() => ComecarTravessia(VelocidadeDaTravessia);

        /// <summary>A travessia noutra velocidade (m/s; correndo, 4,2), para medir o andar pela cidade.</summary>
        public void ComecarTravessia(float velocidade)
        {
            var d = Mapa;
            if (d == null || _atravessando) return;
            var p = new Vector2(transform.position.x, transform.position.z);
            // para o canto oposto, passando pelo meio do mapa
            var alvo = new Vector2(-Mathf.Sign(p.x) * (d.Cerca - 150f), -Mathf.Sign(p.y) * (d.Cerca - 150f));
            _travessiaDirecao = (alvo - p).normalized;
            _travessiaDe = p;
            _travessiaComecou = Time.unscaledTime;
            _travessiaPercorrido = 0f;
            _travessiaVelocidade = velocidade;
            _quadros.Clear();
            _semChao = 0;
            TerrenoDoMundo.MaiorQuadroMs = 0f;
            GetComponent<PassageiroDoCarro>().Soltar();
            Movimento.Voando = true;
            _atravessando = true;
            Avisar($"Teste de travessia: 60 s voando baixo a {velocidade * 3.6f:F0} km/h.");
        }

        void Atravessar()
        {
            var d = Mapa;
            float dt = Time.unscaledDeltaTime;
            if (Time.unscaledTime > _travessiaComecou + 0.5f)   // o primeiro meio segundo não conta
            {
                _quadros.Add(dt * 1000f);
                // um carro ali cairia? (a colisão do chão em volta ainda não estava pronta)
                if (TerrenoDoMundo.Instancia != null && !TerrenoDoMundo.Instancia.Pronto(transform.position, 6f)) _semChao++;
            }
            _travessiaPercorrido += _travessiaVelocidade * dt;
            var p = _travessiaDe + _travessiaDirecao * _travessiaPercorrido;
            transform.position = new Vector3(p.x, d.Altura(p.x, p.y) + AlturaDaTravessia, p.y);
            transform.rotation = Quaternion.LookRotation(new Vector3(_travessiaDirecao.x, 0f, _travessiaDirecao.y));
            if (Time.unscaledTime - _travessiaComecou < DuracaoDaTravessia && d.Dentro(p.x, p.y, 250f)) return;

            _atravessando = false;
            Movimento.Voando = false;
            Movimento.Teleportar(new Vector3(p.x, d.Altura(p.x, p.y) + 0.3f, p.y), transform.eulerAngles.y);
            _quadros.Sort();
            int n = _quadros.Count;
            float soma = 0f;
            foreach (float q in _quadros) soma += q;
            float media = n > 0 ? soma / n : 0f;
            float pior1 = n > 0 ? _quadros[Mathf.Min(n - 1, (int)(n * 0.99f))] : 0f;
            float pior = n > 0 ? _quadros[n - 1] : 0f;
            int acima33 = 0;
            foreach (float q in _quadros) if (q > 33.4f) acima33++;
            string texto = $"Travessia a {_travessiaVelocidade * 3.6f:F0} km/h: {_travessiaPercorrido:F0} m em {Time.unscaledTime - _travessiaComecou:F0} s, {n} quadros; média {media:F1} ms " +
                           $"({(media > 0f ? 1000f / media : 0f):F0} FPS), 1% pior {pior1:F1} ms, pior {pior:F1} ms, {acima33} acima de 33 ms; " +
                           $"o terreno levou no máximo {TerrenoDoMundo.MaiorQuadroMs:F1} ms num quadro; {_semChao} quadros sem a colisão do chão embaixo.";
            Debug.Log("[Mundo] " + texto);
            Avisar(texto);
        }

        /// <summary>
        /// O benchmark da cidade (<see cref="BenchmarkDaCidade"/>), só no anfitrião (ele manda na hora): o menu fecha, para
        /// não entrar na medida, e o aviso diz onde a tabela ficou.
        /// </summary>
        public void ComecarBenchmarkDaCidade()
        {
            if (Mapa == null || BenchmarkDaCidade.Rodando || _atravessando) return;
            if (!IsServer)
            {
                Avisar("O benchmark da cidade roda no anfitrião.");
                return;
            }
            GetComponent<PassageiroDoCarro>().Soltar();
            if (_aberto) Abrir(false);
            StartCoroutine(BenchmarkDaCidade.Rodar(Movimento, Mapa, Avisar));
        }

        /// <summary>
        /// A conferência das casas de verdade (<see cref="ConferenciaDasCasas"/>, Parte M6): as por perto (60 m) ou uma de
        /// cada variação no mapa todo. Só no anfitrião (o NavMesh é dele); o menu fecha.
        /// </summary>
        public void ComecarConferenciaDasCasas(bool umaDeCadaVariacao)
        {
            if (Mapa == null || ConferenciaDasCasas.Rodando || BenchmarkDaCidade.Rodando || _atravessando) return;
            if (!IsServer)
            {
                Avisar("A conferência das casas roda no anfitrião.");
                return;
            }
            GetComponent<PassageiroDoCarro>().Soltar();
            if (_aberto) Abrir(false);
            StartCoroutine(ConferenciaDasCasas.Rodar(Movimento, Mapa, 60f, Avisar, umaDeCadaVariacao));
        }

        /// <summary>A conferência das lojas de verdade (Parte M6, terceira fatia): uma de cada tipo. Só no anfitrião.</summary>
        public void ComecarConferenciaDasLojas()
        {
            if (Mapa == null || ConferenciaDasCasas.Rodando || BenchmarkDaCidade.Rodando || _atravessando) return;
            if (!IsServer)
            {
                Avisar("A conferência das lojas roda no anfitrião.");
                return;
            }
            GetComponent<PassageiroDoCarro>().Soltar();
            if (_aberto) Abrir(false);
            StartCoroutine(ConferenciaDasCasas.RodarLojas(Movimento, Mapa, Avisar));
        }

        /// <summary>A conferência dos prédios de verdade (Parte M6, segunda fatia): um de cada variação. Só no anfitrião.</summary>
        public void ComecarConferenciaDosPredios()
        {
            if (Mapa == null || ConferenciaDasCasas.Rodando || BenchmarkDaCidade.Rodando || _atravessando) return;
            if (!IsServer)
            {
                Avisar("A conferência dos prédios roda no anfitrião.");
                return;
            }
            GetComponent<PassageiroDoCarro>().Soltar();
            if (_aberto) Abrir(false);
            StartCoroutine(ConferenciaDasCasas.RodarPredios(Movimento, Mapa, Avisar));
        }

        /// <summary>Pede ao anfitrião o carro livre mais perto, 4 m à frente.</summary>
        [Rpc(SendTo.Server)]
        void TrazerOCarroRpc(RpcParams parametros = default)
        {
            if (!Aceita(parametros) || Mapa == null) return;
            Carro escolhido = null;
            float menor = float.MaxValue;
            foreach (var c in Carro.Todos)
            {
                if (c == null || !c.IsSpawned || c.Ocupado(0) || c.Ocupado(1) || c.Ocupado(2) || c.Ocupado(3)) continue;
                float d2 = (c.transform.position - transform.position).sqrMagnitude;
                if (d2 < menor) { menor = d2; escolhido = c; }
            }
            if (escolhido == null) return;
            var frente = transform.forward;
            frente.y = 0f;
            var ponto = transform.position + frente.normalized * 4.5f;
            ponto.y = Mapa.Altura(ponto.x, ponto.z) + 0.5f;
            var corpo = escolhido.GetComponent<Rigidbody>();
            corpo.linearVelocity = Vector3.zero;
            corpo.angularVelocity = Vector3.zero;
            corpo.position = ponto;
            corpo.rotation = Quaternion.LookRotation(Vector3.Cross(frente, Vector3.up));
            escolhido.transform.SetPositionAndRotation(ponto, corpo.rotation);
        }

        /// <summary>O painel, à direita do menu (nada fora da cena Mundo). Devolve onde ele ficou.</summary>
        Rect PainelDoMundo(float u, Rect menu)
        {
            var mundo = MundoDaPartida.Instancia;
            var d = Mapa;
            if (d == null) return default;
            var area = new Rect(menu.xMax + 8 * u, menu.y, 280 * u, 658 * u);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "MUNDO", _titulo);
            y += 26 * u;
            GUI.Label(new Rect(x, y, w, 54 * u),
                $"{mundo.Nome}\n{Tamanhos.NomeComLado(d.Tamanho)} · semente {d.Semente} · gerador {d.Versao}\nresumo {d.Resumo:X16}", _texto);
            y += 58 * u;
            var p = transform.position;
            var bloco = d.BlocoDe(p.x, p.z);
            var terreno = TerrenoDoMundo.Instancia;
            string blocos = terreno != null ? $"{terreno.Blocos} blocos, {terreno.BlocosComColisao} com colisão, {terreno.CasasDeVerdade} casas, {terreno.PrediosDeVerdade} prédios ({terreno.AndaresCarregados} andares)" : "";
            // (a linha dos blocos quebra em duas)
            GUI.Label(new Rect(x, y, w, 72 * u),
                $"Você: x {p.x:F0}, z {p.z:F0}, altura {p.y:F1} (chão {d.Altura(p.x, p.z):F1})\nbloco {bloco.x}, {bloco.y} de {d.BlocosPorLado} × {d.BlocosPorLado}\n{blocos}", _texto);
            y += 76 * u;
            if (GUI.Button(new Rect(x, y, w, 28 * u), Movimento.Voando ? "Parar de voar (F2)" : "Voar (F2)", _botao)) Voar(!Movimento.Voando);
            y += 36 * u;

            GUI.Label(new Rect(x, y + 4 * u, 50 * u, 22 * u), "Ir para", _texto);
            GUI.Label(new Rect(x + 52 * u, y + 4 * u, 14 * u, 22 * u), "x", _texto);
            _irX = GUI.TextField(new Rect(x + 64 * u, y, 62 * u, 26 * u), _irX, 6, _campo);
            GUI.Label(new Rect(x + 132 * u, y + 4 * u, 14 * u, 22 * u), "z", _texto);
            _irZ = GUI.TextField(new Rect(x + 144 * u, y, 62 * u, 26 * u), _irZ, 6, _campo);
            if (GUI.Button(new Rect(x + 212 * u, y, w - 212 * u, 26 * u), "Ir", _botao))
            {
                if (float.TryParse(_irX, out float ix) && float.TryParse(_irZ, out float iz)) IrPara(ix, iz);
                else Avisar("Escreva os dois números (em metros, o meio do mapa é 0, 0).");
            }
            y += 34 * u;

            float bw = (w - 8 * u) / 2f, borda = d.Cerca - 30f;
            var margem = d.Lago != null ? d.Lago.Centro + new Vector2(d.Lago.Raio * 1.25f, 0f) : Vector2.zero;
            var lugares = new (string nome, Vector2 ponto)[]
            {
                ("A partida", new Vector2(d.Partida.x, d.Partida.z)), ("A margem do lago", margem),
                ("O meio do mapa", Vector2.zero), ("A borda norte", new Vector2(p.x, borda)),
                ("A borda sul", new Vector2(p.x, -borda)), ("A borda leste", new Vector2(borda, p.z)),
                ("A borda oeste", new Vector2(-borda, p.z)),
            };
            for (int i = 0; i < lugares.Length; i++)
            {
                var r = new Rect(x + (i % 2) * (bw + 8 * u), y + (i / 2) * 32 * u, bw, 26 * u);
                if (!GUI.Button(r, lugares[i].nome, _botao)) continue;
                if (i == 0)
                {
                    // no lugar de nascimento deste jogador, na partida
                    var ponto = PontosDeNascimento.Ponto(OwnerClientId);
                    GetComponent<PassageiroDoCarro>().Soltar();
                    Movimento.Teleportar(ponto.position, ponto.rotation.eulerAngles.y);
                    Avisar("Na partida.");
                }
                else IrPara(lugares[i].ponto.x, lugares[i].ponto.y);
            }
            // a última célula da grade de lugares: trazer o carro
            if (GUI.Button(new Rect(x + bw + 8 * u, y + 3 * 32 * u, bw, 26 * u), "Trazer o carro", _botao))
            {
                TrazerOCarroRpc();
                Avisar("O carro livre mais perto vem para a sua frente.");
            }
            y += 4 * 32 * u + 4 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), _atravessando ? "Atravessando..." : "Teste de travessia (60 s a 160 km/h)", _botao) && !_atravessando)
                ComecarTravessia();
            y += 32 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), BenchmarkDaCidade.Rodando ? "Benchmark da cidade rodando..." : "Benchmark da cidade (dia e noite)", _botao))
                ComecarBenchmarkDaCidade();
            y += 32 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), ConferenciaDasCasas.Rodando ? ConferenciaDasCasas.Status : "Conferir as casas por perto (60 m)", _botao))
                ComecarConferenciaDasCasas(false);
            y += 32 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Conferir uma casa de cada variação", _botao))
                ComecarConferenciaDasCasas(true);
            y += 32 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Conferir um prédio de cada variação", _botao))
                ComecarConferenciaDosPredios();
            y += 32 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Conferir uma loja de cada tipo", _botao))
                ComecarConferenciaDasLojas();
            y += 32 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), MoveisDaConstrucao.Mostrar ? $"Esconder os pontos dos móveis ({MoveisDaConstrucao.Pontos})" : "Mostrar os pontos dos móveis", _botao))
                MoveisDaConstrucao.Mostrar = !MoveisDaConstrucao.Mostrar;
            y += 32 * u;
            if (GUI.Button(new Rect(x, y, w, 26 * u), "Copiar a semente", _botao))
            {
                GUIUtility.systemCopyBuffer = d.Semente.ToString();
                Avisar($"Semente {d.Semente} copiada.");
            }
            return area;
        }
    }
}
