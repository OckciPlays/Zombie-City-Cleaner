using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A fuga na tela de cada máquina (23/11/2026, as etapas 5 e 6 do Docs/23; o Pedro: "uma missão principal no canto
    /// direito superior, com o objetivo de correr"), posta pelo <see cref="LakewoodNaCena"/>:
    ///  - a missão no canto de cima, à direita: "FUJA PARA O BUNKER", o que fazer e quantos metros faltam até o portão;
    ///  - a marca "Bunker" do mapa (o alfinete com a distância, posta pelo anfitrião) leva até o portão;
    ///  - os gritos pela vila, de vez em quando, e o Gus chamando no portão (a fala embaixo, como no cinema);
    ///  - a cada tentativa nova (<see cref="MundoDeLakewood.Tentativa"/>), o jogador local volta para a sala da casa (também na
    ///    primeira, quando ela não vem da tela dos 30 dias: a fuga que voltou do save, ou quem entrou no meio dela);
    ///  - a tela da fuga que falhou (alguém morreu), até o recomeço;
    ///  - a tela dos 5 anos (todos chegaram ao bunker): quando a época volta para depois e o palco sai, o jogador vai para a
    ///    sala de operações do bunker, e a tela clareia.
    /// </summary>
    public class FugaNaTela : MonoBehaviour
    {
        int _tentativaVista;
        float _proximoGrito, _proximoChamado;
        readonly System.Random _sorte = new System.Random();
        // as "pessoas" dos gritos (cada uma com a sua voz, uma de cada vez)
        readonly Object[] _quemGrita = new Object[4];
        bool _naChegada, _prontoNoBunker, _chegadaFeita;
        float _chegadaDesde, _prontoDesde, _falhaDesde = -1f;
        GUIStyle _titulo, _texto, _pequeno, _grande, _marcador;
        float _u;

        static MundoDeLakewood Mundo => MundoDeLakewood.Instancia;

        void Update()
        {
            var m = Mundo;
            if (m == null || !m.IsSpawned)
            {
                // (26/11/2026, a integração do save do Dia 0) fora da rede, a tela volta ao começo: a cena não recarrega entre
                // uma partida e outra, e o número da tentativa vista, a tela da falha e a dos 5 anos ficavam da partida
                // anterior (sair no meio da fuga e, sem fechar o jogo, abrir um save da fuga, ou entrar na de um amigo)
                _tentativaVista = 0;
                _falhaDesde = -1f;
                _naChegada = _chegadaFeita = _prontoNoBunker = _despertar = false;
                return;
            }
            var j = JogadorRede.Local;
            // a tentativa nova: o jogador local na sala da casa (já com a tela da falha por cima)
            if (m.NaFuga && m.Tentativa != _tentativaVista && j != null)
            {
                // (a primeira vem da tela dos 30 dias: o DiaZeroDaPartida põe na sala. 25/11/2026, o save do Dia 0: menos na fuga
                // que voltou do save e para quem entrou no meio dela, que não passaram pela tela; esses vão para a casa já, com
                // o palco pronto: sem ele, os pontos de nascimento ainda são os do bunker, e a chegada seria na hora)
                bool pelaTela = _tentativaVista == 0 && DiaZeroDaPartida.VindoDoDiaZero;
                var palco = WoodcreekDoDiaZero.Atual;
                if (pelaTela || (palco != null && palco.Pronta && LakewoodNaCena.ComPalcoDoDiaZero))
                {
                    _tentativaVista = m.Tentativa;
                    if (!pelaTela) IrParaACasa(j);
                    _despertar = true;   // (23/11/2026: o despertar, quando a fuga começa a contar)
                }
            }
            // a tela da falha fica até a tentativa nova começar (a volta para a casa fica por baixo dela). (26/11/2026, a
            // integração do save do Dia 0) Só depois de uma falha vista: antes, qualquer tentativa depois da primeira com o
            // relógio ainda negativo era uma falha, e o número não volta a 1 sem fechar o jogo; a fuga que voltou do save
            // esperando o mundo (até 90 s) e a tela dos 30 dias de uma segunda fuga mostravam "A FUGA FALHOU"
            if (m.Falhou || (_falhaDesde >= 0f && m.NaFuga && m.TempoDaFuga < 0f)) { if (_falhaDesde < 0f) _falhaDesde = Time.unscaledTime; }
            else _falhaDesde = -1f;
            // a chegada: a tela preta cobre a troca da época; com o palco fora, a sala de operações
            // (a chegada fica marcada na rede até a próxima fuga: a tela dos 5 anos passa uma vez só)
            if (m.NaFuga && !m.Chegou) _chegadaFeita = false;
            if (m.Chegou && !_naChegada && !_chegadaFeita)
            {
                _naChegada = true;
                _chegadaFeita = true;
                _prontoNoBunker = false;
                _chegadaDesde = Time.unscaledTime;
                // (25/11/2026, a revisão: um craft no meio desligava a cena quando acabava; primeiro ele para, depois a cena liga)
                AnimacaoDoCraft.CancelarNoJogadorLocal();
                MenuOnline.Cinema = true;
                if (j != null) j.GetComponent<MovimentoPrimeiraPessoa>().Voando = true;
            }
            if (_naChegada && !_prontoNoBunker && Time.unscaledTime - _chegadaDesde > 4f && !m.NaFuga && !LakewoodNaCena.NoAntes && !LakewoodNaCena.ComPalcoDoDiaZero)
            {
                _prontoNoBunker = true;
                _prontoDesde = Time.unscaledTime;
                if (j != null)
                {
                    var mov = j.GetComponent<MovimentoPrimeiraPessoa>();
                    var pose = PontosDeNascimento.Ponto(NetworkManager.Singleton.LocalClientId);
                    mov.Voando = false;
                    mov.Teleportar(pose.position, pose.rotation.eulerAngles.y);
                }
                Debug.Log("[Fuga] 5 anos depois: na sala de operações do bunker.");
            }
            if (_naChegada && _prontoNoBunker && Time.unscaledTime - _prontoDesde > 5f)
            {
                _naChegada = false;
                MenuOnline.Cinema = false;
            }
            if (!m.NaFuga && !m.Chegou) _tentativaVista = 0;
            if (m.NaFuga && !m.Falhou && !m.Chegou && m.TempoDaFuga > 0f && j != null) Sons(j);
        }

        // os gritos pela vila (perto o bastante para ouvir, de lugares diferentes) e o Gus chamando no portão
        void Sons(JogadorRede j)
        {
            var agora = Time.time;
            if (_despertar)
            {
                _despertar = false;
                _proximoGrito = agora + 9f;   // (os gritos de sempre esperam o despertar passar)
                StartCoroutine(Despertar(j));
            }
            if (agora >= _proximoGrito)
            {
                _proximoGrito = agora + 3.5f + (float)_sorte.NextDouble() * 5.5f;
                // (23/11/2026: a altura não multiplica mais pela distância; os gritos saíam de 13 a 24 m do chão)
                Grito(j.transform.position + Lado(11f + (float)_sorte.NextDouble() * 9f), _sorte.Next(3) == 0 ? TipoDeVoz.Dor : TipoDeVoz.Susto);
            }
            var w = WoodcreekDoDiaZero.Atual;
            var gus = w != null ? w.ChamadoNoPortao : null;
            if (gus.HasValue && agora >= _proximoChamado && Vector3.Distance(j.transform.position, gus.Value) < 50f)
            {
                _proximoChamado = agora + 4.5f;
                VozDoPersonagem.Tocar(w, 0, TipoDeVoz.Ataque, gus.Value, true);
            }
        }

        // ------------------------------------------------------------------ o despertar (23/11/2026)

        bool _despertar;

        /// <summary>
        /// O despertar no começo da noite e de cada tentativa (23/11/2026, a pedido: "para começar a noite devia ter um som alto,
        /// como se fosse tiros, gritos mesmos para o personagem acordar durante a noite, e sirenes tocando"):
        /// - a rajada de um rifle a uns 35 m, e a câmera dá um pulo e o coração dispara;
        /// - um grito perto; a pistola a uns 55 m; outro grito;
        /// - a submetralhadora longe (a 110 m), mais um grito, a espingarda a uns 70 m.
        /// Os tiros são os gravados das armas, com o eco do lugar. As sirenes da defesa civil já tocam longe a noite toda
        /// (WoodcreekDoDiaZero.PorAsSirenes). Tudo nesta máquina, em volta do jogador daqui.
        /// </summary>
        System.Collections.IEnumerator Despertar(JogadorRede j)
        {
            yield return new WaitForSeconds(0.4f);
            if (j == null) yield break;
            Vector3 eu = j.transform.position;
            var rifle = eu + Lado(35f, 1.5f);
            for (int i = 0; i < 3; i++)
            {
                Tiro(TipoDeArma.Rifle, rifle);
                if (i == 0) Susto(j, 0.55f, 35f);
                yield return new WaitForSeconds(0.16f + (float)_sorte.NextDouble() * 0.12f);
            }
            yield return new WaitForSeconds(0.5f);
            Grito(eu + Lado(16f), TipoDeVoz.Susto);
            yield return new WaitForSeconds(0.9f);
            var pistola = eu + Lado(55f, 1.5f);
            Tiro(TipoDeArma.Pistola, pistola);
            yield return new WaitForSeconds(0.35f);
            Tiro(TipoDeArma.Pistola, pistola);
            yield return new WaitForSeconds(0.8f);
            Grito(eu + Lado(24f), TipoDeVoz.Dor);
            yield return new WaitForSeconds(1.4f);
            var submetralhadora = eu + Lado(110f, 2f);
            for (int i = 0; i < 6; i++)
            {
                Tiro(TipoDeArma.Submetralhadora, submetralhadora);
                yield return new WaitForSeconds(0.09f);
            }
            yield return new WaitForSeconds(1.2f);
            Grito(eu + Lado(30f), TipoDeVoz.Susto);
            yield return new WaitForSeconds(2f);
            var espingarda = eu + Lado(70f, 1.5f);
            Tiro(TipoDeArma.Espingarda, espingarda);
            yield return new WaitForSeconds(0.9f);
            Tiro(TipoDeArma.Espingarda, espingarda);
        }

        // um ponto em volta, a 'dist' m, na altura 'alto'
        Vector3 Lado(float dist, float alto = 1.2f)
        {
            float ang = (float)_sorte.NextDouble() * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(ang) * dist, alto, Mathf.Sin(ang) * dist);
        }

        // o tiro gravado da arma, com o eco do lugar (como o dos sobreviventes)
        static void Tiro(TipoDeArma arma, Vector3 onde)
        {
            var tiros = Efeitos.Tiros;
            if (tiros != null && (int)arma < tiros.Length) Efeitos.Som(tiros[(int)arma], onde, 1f, true, 250f);
            EcoDoTiro.Tocar(arma, onde, 1f, true, 250f);
        }

        // uma das "pessoas" da vila grita (cada uma com a sua voz)
        void Grito(Vector3 onde, TipoDeVoz tipo)
        {
            int pessoa = _sorte.Next(4);
            if (_quemGrita[pessoa] == null) _quemGrita[pessoa] = new GameObject($"Grito {pessoa}");
            VozDoPersonagem.Tocar(_quemGrita[pessoa], pessoa, tipo, onde, true);
        }

        // o pulo de quem acorda com o barulho: a câmera sacode e o coração dispara
        static void Susto(JogadorRede j, float tremor, float bpm)
        {
            var mov = j.GetComponent<MovimentoPrimeiraPessoa>();
            if (mov != null) mov.Tremer(tremor);
            var coracao = j.GetComponent<MonitorCardiaco>();
            if (coracao != null) coracao.Sobressalto(bpm);
        }

        static void IrParaACasa(JogadorRede j)
        {
            var mov = j.GetComponent<MovimentoPrimeiraPessoa>();
            var pose = PontosDeNascimento.Ponto(NetworkManager.Singleton.LocalClientId);
            mov.Voando = false;
            mov.Teleportar(pose.position, pose.rotation.eulerAngles.y);
        }

        void Estilos(float u)
        {
            if (_titulo != null && Mathf.Approximately(u, _u)) return;
            _u = u;
            var fonte = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial" }, 32);
            _titulo = new GUIStyle(GUI.skin.label) { font = fonte, fontSize = Mathf.RoundToInt(20 * u), alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
            _titulo.normal.textColor = new Color(0.98f, 0.86f, 0.5f);
            _texto = new GUIStyle(_titulo) { fontSize = Mathf.RoundToInt(15 * u), fontStyle = FontStyle.Normal, wordWrap = true };
            _texto.normal.textColor = new Color(0.95f, 0.93f, 0.88f);
            _pequeno = new GUIStyle(_texto) { fontSize = Mathf.RoundToInt(13 * u) };
            _grande = new GUIStyle(GUI.skin.label) { font = fonte, fontSize = Mathf.RoundToInt(60 * u), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _grande.normal.textColor = new Color(0.93f, 0.91f, 0.86f);
            _marcador = new GUIStyle(GUI.skin.label) { font = fonte, fontSize = Mathf.RoundToInt(19 * u), alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
            _marcador.normal.textColor = new Color(0.98f, 0.86f, 0.5f);
        }

        void OnGUI()
        {
            var m = Mundo;
            if (m == null || !m.IsSpawned) return;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos(u);
            if (_naChegada)
            {
                GUI.depth = -21;
                DesenharOsCincoAnos(u);
                return;
            }
            if (!m.NaFuga || ModoVideo.Ligado) return;
            if (_falhaDesde >= 0f && !TelaDeMorte.Aberta)
            {
                GUI.depth = -21;
                float a = Mathf.Clamp01((Time.unscaledTime - _falhaDesde) / 0.8f);
                Preto(a * 0.92f);
                var c = GUI.color;
                GUI.color = new Color(0.85f, 0.18f, 0.14f, a);
                GUI.Label(new Rect(0, Screen.height * 0.38f, Screen.width, 80 * u), "A FUGA FALHOU", _grande);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.Label(new Rect(Screen.width * 0.25f, Screen.height * 0.38f + 84 * u, Screen.width * 0.5f, 30 * u),
                          "Alguém não conseguiu. A noite recomeça na casa.", new GUIStyle(_texto) { alignment = TextAnchor.MiddleCenter });
                GUI.color = c;
                return;
            }
            if (MenuOnline.Aberto || MenuOnline.Cinema || m.TempoDaFuga < 0f) return;
            var j = JogadorRede.Local;
            var lw = LakewoodNaCena.Instancia;
            if (j == null || lw == null || !lw.PortaoDoBunker(out var portao)) return;
            GUI.depth = -5;
            // (25/11/2026) a missão, a distância até o portão e a tentativa estão no caderno das missões (UI/CadernoDeMissoes),
            // no canto de cima à esquerda; antes, ficavam por cima do minimapa
            // o Gus chamando no portão (a fala embaixo, sem caixa, como no cinema)
            var wc = WoodcreekDoDiaZero.Atual;
            var gus = wc != null ? wc.ChamadoNoPortao : null;
            if (gus.HasValue && Vector3.Distance(j.transform.position, gus.Value) < 45f)
                Sombra(new Rect(Screen.width * 0.15f, Screen.height * 0.8f, Screen.width * 0.7f, 30 * u),
                       "Gus Rodrigues: Aqui! Rápido, pra dentro! O portão vai fechar!", _marcador);
        }

        // a tela dos 5 anos: escurece, o título; com o jogador no bunker, clareia
        void DesenharOsCincoAnos(float u)
        {
            float t = Time.unscaledTime - _chegadaDesde;
            float alfa = Mathf.Clamp01(t / 1.2f), texto = Mathf.Clamp01((t - 1.4f) / 1.2f);
            if (_prontoNoBunker)
            {
                float d = Time.unscaledTime - _prontoDesde;
                alfa = 1f - Mathf.Clamp01((d - 2.6f) / 2.0f);
                texto = Mathf.Min(texto, 1f - Mathf.Clamp01((d - 2.0f) / 1.0f));
            }
            Preto(alfa);
            var c = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, texto);
            GUI.Label(new Rect(0, Screen.height * 0.36f, Screen.width, 90 * u), "5 ANOS DEPOIS", _grande);
            GUI.Label(new Rect(Screen.width * 0.2f, Screen.height * 0.36f + 96 * u, Screen.width * 0.6f, 50 * u),
                      "O portão do bunker fechou atrás de vocês naquela noite. Lá fora, o mundo mudou.",
                      new GUIStyle(_texto) { alignment = TextAnchor.MiddleCenter });
            GUI.color = c;
        }

        static void Preto(float alfa)
        {
            if (alfa <= 0f) return;
            var c = GUI.color;
            GUI.color = new Color(0.02f, 0.02f, 0.02f, alfa);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = c;
        }

        static void Sombra(Rect r, string texto, GUIStyle estilo)
        {
            var cor = estilo.normal.textColor;
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.75f * cor.a);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), texto, estilo);
            estilo.normal.textColor = cor;
            GUI.Label(r, texto, estilo);
        }
    }
}
