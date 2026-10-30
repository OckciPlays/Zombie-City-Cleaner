using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;
using Random = UnityEngine.Random;

namespace ZombieCityCleaner.Video
{
    /// <summary>
    /// A filmagem do vídeo do fundo do menu (a pedido em 29/10/2026: "mostrando a cidade bem devagar, mostrando os Ocos
    /// petrificados, alguns dentro de casa observando você, luzes piscando"). Roda no mundo do jogo (a cena Mundo, já
    /// hospedada), de manhã cedo e com a neblina forte, em cinco tomadas lentas, cada quadro desenhado por uma câmera
    /// própria e gravado em JPG (2560 × 1440 desde a segunda versão, a pedido: "a qualidade está baixa"; era 1280 × 720; 30
    /// por segundo, com o tempo do jogo andando 1/30 s por quadro, por mais que o
    /// quadro demore): a rua com as estátuas, um Oco na janela de uma casa com a lâmpada piscando lá dentro, o carro com o
    /// pisca-alerta, o rosto de uma estátua de perto e a cidade grande vista do alto. O vídeo do menu sai das pastas pelo
    /// Ferramentas/video_do_menu.py (as tomadas emendadas com o escuro, a cor e o laço). Uma ferramenta: o jogo não usa.
    /// Começa por <see cref="Comecar"/> (no Play, com o jogador local já nascido).
    /// </summary>
    public class FilmeDoMenu : MonoBehaviour
    {
        public static string Situacao { get; private set; } = "";
        public static bool Terminou { get; private set; }

        const int Largura = 2560, Altura = 1440, Fps = 30, QualidadeDoJpg = 96;
        const float Hora = 6.8f;   // o sol já petrifica (a CicloDoDia.Noite ~0,33), mas a luz ainda é a do começo do dia

        string _pasta;
        Camera _cam;
        RenderTexture _rt;
        Texture2D _tex;
        JogadorRede _j;
        Zumbi _observador;
        Vector3 _olharDoObservador;
        readonly List<(Light luz, float forca, float semente)> _piscando = new List<(Light, float, float)>();
        readonly List<GameObject> _criados = new List<GameObject>();
        readonly List<Zumbi> _estatuas = new List<Zumbi>();

        /// <summary>Filma tudo, ou só as tomadas que começam com 'so' (o número: "5" refaz só a vista do alto).</summary>
        public static void Comecar(string pasta, string so = null)
        {
            Terminou = false;
            Situacao = "começando";
            var go = new GameObject("Filme do menu");
            var f = go.AddComponent<FilmeDoMenu>();
            f._pasta = pasta;
            f._so = so;
            f.StartCoroutine(f.Rodar());
        }

        string _so;
        bool Filmar(string numero) => string.IsNullOrEmpty(_so) || _so.Contains(numero);

        void LateUpdate()
        {
            // a câmera do jogador fica desligada (o JogadorRede liga quando ele nasce); quem filma é a nossa
            if (_j != null)
            {
                if (_j.cameraMundo != null) _j.cameraMundo.enabled = false;
                if (_j.cameraBracos != null) _j.cameraBracos.enabled = false;
            }
            if (_cam != null)
            {
                ModoVideo.Olho = _cam.transform.position;
                // o fundo é a neblina (o AmbienteDoDia só pinta o das câmeras que desenham na tela, e esta desenha numa textura)
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = AmbienteDoDia.CorDaNeblina;
            }
            // o Oco da janela fica parado, olhando para fora
            if (_observador != null && _observador.agente != null && _observador.agente.isOnNavMesh)
            {
                _observador.agente.isStopped = true;
                _observador.transform.rotation = Quaternion.LookRotation(_olharDoObservador);
            }
            // as luzes falhando: quase sempre acesas, com quedas curtas e, de vez em quando, um apagão mais longo
            float t = Time.time;
            foreach (var (luz, forca, s) in _piscando)
            {
                if (luz == null) continue;
                float queda = Mathf.PerlinNoise(s, t * 7f), apagao = Mathf.PerlinNoise(s + 13.1f, t * 0.6f);
                float k = apagao < 0.28f ? 0.03f : queda < 0.33f ? 0.12f : 0.82f + 0.18f * Mathf.PerlinNoise(s + 5.3f, t * 23f);
                luz.intensity = forca * k;
            }
        }

        IEnumerator Rodar()
        {
            while ((_j = JogadorRede.Local) == null || !_j.IsSpawned || ZombieManager.Instancia == null) yield return null;
            Directory.CreateDirectory(_pasta);
            var zm = ZombieManager.Instancia;
            zm.SemDano = true;
            CicloDoDia.Instancia.SempreDeDia(true);
            CicloDoDia.MudarHora(Hora);
            ModoVideo.Ligado = true;
            ModoVideo.Neblina = 1.5f;
            ModoVideo.AlcanceDosZumbis = 3f;
            MontarCamera();
            Time.captureFramerate = Fps;
            yield return Esperar(4f);
            Vector3 casa = _j.transform.position;
            // cada tomada com o sorteio fixo (as estátuas, as luzes): refazer dá a mesma cena
            if (Filmar("1")) { Random.InitState(7001); yield return Rua(casa); }
            if (Filmar("2")) { Random.InitState(7002); yield return Janela(casa); }
            if (Filmar("3")) { Random.InitState(7003); yield return PiscaAlerta(casa); }
            if (Filmar("4")) { Random.InitState(7004); yield return DePerto(); }
            if (Filmar("5")) { Random.InitState(7005); yield return DoAlto(); }
            Time.captureFramerate = 0;
            Desfazer();
            Situacao = "pronto";
            Terminou = true;
        }

        void MontarCamera()
        {
            var go = new GameObject("Câmera do filme");
            _criados.Add(go);
            _cam = go.AddComponent<Camera>();
            var fonte = _j.cameraMundo;
            if (fonte != null) _cam.CopyFrom(fonte);
            _cam.farClipPlane = 600f;
            _cam.nearClipPlane = 0.1f;
            go.tag = "MainCamera";   // a neblina, as partículas e o resto seguem a Camera.main
            var dados = _cam.GetUniversalAdditionalCameraData();
            dados.renderPostProcessing = true;
            dados.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            dados.antialiasingQuality = AntialiasingQuality.High;
            dados.renderShadows = true;
            if (fonte != null) dados.volumeLayerMask = fonte.GetUniversalAdditionalCameraData().volumeLayerMask;
            _rt = new RenderTexture(Largura, Altura, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            _rt.Create();
            _cam.targetTexture = _rt;
            _tex = new Texture2D(Largura, Altura, TextureFormat.RGB24, false);
        }

        void Desfazer()
        {
            if (_j != null)
            {
                if (_j.cameraMundo != null) _j.cameraMundo.enabled = true;
                if (_j.cameraBracos != null) _j.cameraBracos.enabled = true;
            }
            ModoVideo.Zerar();
            foreach (var go in _criados) if (go != null) Destroy(go);
            if (_rt != null) _rt.Release();
            CicloDoDia.Instancia.SempreDeDia(false);
            if (ZombieManager.Instancia != null) ZombieManager.Instancia.SemDano = false;
        }

        // ------------------------------------------------------------------ a gravação

        static IEnumerator Esperar(float segundos)
        {
            int n = Mathf.CeilToInt(segundos * Fps);
            for (int i = 0; i < n; i++) yield return null;
        }

        // uma tomada: a câmera pela pose de cada momento (0 a 1), um JPG por quadro; antes, 4 quadros parados (as sombras e
        // o anti-serrilhado assentam depois do pulo da câmera)
        IEnumerator Gravar(string nome, float duracao, Func<float, (Vector3 pos, Vector3 alvo, float campo)> pose)
        {
            string pasta = Path.Combine(_pasta, nome);
            Directory.CreateDirectory(pasta);
            foreach (var velho in Directory.GetFiles(pasta, "*.jpg")) File.Delete(velho);
            int n = Mathf.RoundToInt(duracao * Fps);
            for (int i = -4; i < n; i++)
            {
                var (pos, alvo, campo) = pose(Mathf.Clamp01(i / (float)(n - 1)));
                _cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(alvo - pos));
                _cam.fieldOfView = campo;
                Situacao = $"{nome}: {Mathf.Max(i, 0)}/{n}";
                yield return new WaitForEndOfFrame();
                if (i < 0) continue;
                var antes = RenderTexture.active;
                RenderTexture.active = _rt;
                _tex.ReadPixels(new Rect(0, 0, Largura, Altura), 0, 0);
                RenderTexture.active = antes;
                File.WriteAllBytes(Path.Combine(pasta, $"{i:00000}.jpg"), _tex.EncodeToJPG(QualidadeDoJpg));
            }
        }

        static float Suave(float t) => t * t * (3f - 2f * t);

        // ------------------------------------------------------------------ as tomadas

        // 1. a rua: a câmera avança devagar entre as estátuas, na neblina
        IEnumerator Rua(Vector3 casa)
        {
            Situacao = "rua: procurando";
            if (!LugarDeRua(casa, out var alvo, out var dir)) alvo = casa;
            // as estátuas na frente da câmera (até 6 m do alvo; ela anda de 16 a 9 m) e a 4,5 m ou mais para o lado: na
            // primeira versão, uma estátua e o poste passavam a 1-2 m da câmera e atravessavam a borda da imagem
            // deformados ("ocorre uma esticada durante sua passagem")
            PorEstatuas(alvo, dir, 5, 4.5f, 14f, 6f);
            Vector3 lado = Vector3.Cross(Vector3.up, dir);
            Vector3 ini = alvo + dir * 16f + lado * 1.2f, fim = alvo + dir * 9f - lado * 0.6f;
            AfastarOJogador(ini + dir * 20f);
            yield return Esperar(1.5f);
            yield return Gravar("1_rua", 8.5f, t =>
            {
                float s = Suave(t);
                var p = Vector3.Lerp(ini, fim, s);
                p.y = Chao(p) + 1.65f;
                var olho = alvo + Vector3.up * 1.3f - lado * Mathf.Lerp(0.5f, -0.5f, s);
                return (p, olho, 50f);
            });
        }

        // 2. a janela: um Oco parado dentro de casa, olhando para fora, e a lâmpada falhando atrás dele
        IEnumerator Janela(Vector3 casa)
        {
            Situacao = "janela: procurando";
            if (!AcharJanela(casa, out var janela, out var fora, out var dentro))
            {
                Situacao = "janela: nenhuma";
                yield break;
            }
            ZombieManager.Instancia.CriarUm(dentro, Quaternion.LookRotation(fora).eulerAngles.y, false);
            yield return null;
            _observador = MaisPerto(z => !z.Morto && !z.Petrificado, dentro, 1.5f);
            _olharDoObservador = fora;
            var lado = Vector3.Cross(Vector3.up, fora);
            Piscante(dentro - fora * 2.2f + lado * 1.2f + Vector3.up * 1.9f, new Color(1f, 0.72f, 0.42f), 3.2f, 7f);
            float chaoFora = Chao(janela + fora * 6f);
            Vector3 ini = janela + fora * 8.5f + lado * 1.5f, fim = janela + fora * 4.2f + lado * 0.3f;
            AfastarOJogador(janela + fora * 26f);
            yield return Esperar(1.5f);
            yield return Gravar("2_janela", 8.5f, t =>
            {
                float s = Suave(t);
                var p = Vector3.Lerp(ini, fim, s);
                p.y = Mathf.Max(Chao(p), chaoFora) + 1.6f;
                var cabeca = _observador != null ? _observador.transform.position + Vector3.up * 1.55f : janela;
                return (p, Vector3.Lerp(janela, cabeca, 0.6f), Mathf.Lerp(48f, 40f, s));
            });
            _observador = null;
        }

        // 3. o pisca-alerta: um carro abandonado piscando na neblina, com estátuas em volta
        IEnumerator PiscaAlerta(Vector3 casa)
        {
            Situacao = "pisca-alerta: procurando";
            var pecas = PecasDosCarrosDoMundo.Instancia;
            var objetos = ObjetosDoMundo.Instancia;
            int carro = CarroPerto(casa, 90f, out var pos, out var giro, out var caixa);
            if (carro < 0 || pecas == null)
            {
                Situacao = "pisca-alerta: nenhum carro";
                yield break;
            }
            pecas.DispararOMaisPerto(pos);
            Vector3 frente = giro * Vector3.forward, lado = giro * Vector3.right;
            PorEstatuas(pos, -frente, 3, 2.5f, 6f);
            Vector3 meio = pos + giro * caixa.center;
            Vector3 ini = meio + lado * 7.5f + frente * 4f, fim = meio + lado * 7f - frente * 1.5f;
            AfastarOJogador(meio + lado * 30f);
            yield return Esperar(1.5f);
            yield return Gravar("3_pisca", 7.5f, t =>
            {
                float s = Suave(t);
                var p = Vector3.Lerp(ini, fim, s);
                p.y = Chao(p) + 0.9f;
                return (p, meio + Vector3.up * 0.4f, 46f);
            });
        }

        // 4. de perto: o rosto de uma estátua (na pose da agonia), com uma luz fria falhando de lado
        IEnumerator DePerto()
        {
            Situacao = "de perto: procurando";
            var candidatas = new List<Zumbi>(_estatuas);
            foreach (var z in FindObjectsByType<Zumbi>())
                if (z.isActiveAndEnabled && z.Petrificado && !candidatas.Contains(z) && (z.transform.position - _j.transform.position).sqrMagnitude < 150f * 150f)
                    candidatas.Add(z);
            Zumbi estatua = null;
            Vector3 cabeca = default;
            foreach (var z in candidatas)
                if (z != null && z.Petrificado && FrenteLivre(z, out cabeca))
                {
                    estatua = z;
                    break;
                }
            if (estatua == null)
            {
                Situacao = "de perto: nenhuma estátua com a frente livre";
                yield break;
            }
            Vector3 frente = estatua.transform.forward, lado = estatua.transform.right;
            Piscante(cabeca + lado * 1.6f + Vector3.up * 1.2f + frente * 0.8f, new Color(0.72f, 0.82f, 1f), 2.2f, 5f);
            AfastarOJogador(estatua.transform.position - frente * 25f);
            yield return Esperar(1.0f);
            yield return Gravar("4_perto", 7.5f, t =>
            {
                float s = Suave(t);
                var p = cabeca + frente * Mathf.Lerp(2.6f, 1.25f, s) + lado * Mathf.Lerp(0.5f, 0.15f, s) + Vector3.up * Mathf.Lerp(0.05f, -0.02f, s);
                return (p, cabeca - Vector3.up * 0.05f, 38f);
            });
        }

        // 5. do alto: a cidade grande na neblina, a câmera passando devagar por cima dos prédios
        IEnumerator DoAlto()
        {
            Situacao = "do alto: indo para a cidade grande";
            var mundo = MundoDaPartida.Instancia;
            var d = mundo != null ? mundo.Dados : null;
            if (d?.Plano == null) yield break;
            var aqui = new Vector2(_j.transform.position.x, _j.transform.position.z);
            Vector2 melhor = aqui;
            float menor = float.MaxValue;
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                {
                    if (l.Predio == null || l.Predio.andares < 6) continue;
                    var m = t.Mundo(l.MeioDaCasa);
                    float dist = Vector2.Distance(m, aqui);
                    if (dist < menor) { menor = dist; melhor = m; }
                }
            var centro = new Vector3(melhor.x, d.Altura(melhor.x, melhor.y), melhor.y);
            _j.GetComponent<MovimentoPrimeiraPessoa>().Teleportar(centro + new Vector3(0f, 0.5f, -30f), 0f);
            _cam.transform.position = centro + Vector3.up * 60f;
            yield return Esperar(14f);   // os blocos de lá carregam, os prédios ficam de verdade e a rua se povoa
            // janelas acesas falhando nos prédios de perto (a luz por dentro da fachada, na altura de um andar)
            foreach (var predio in AndaresDoPredio.Ativos)
            {
                if (predio == null || predio.Planta == null || (predio.transform.position - centro).sqrMagnitude > 90f * 90f) continue;
                var pl = predio.Planta;
                int andar = Random.Range(1, Mathf.Max(2, pl.andares));
                float x = Random.Range(-pl.W * 0.35f, pl.W * 0.35f);
                Piscante(predio.transform.TransformPoint(new Vector3(x, pl.Base(andar) + 1.6f, pl.Z0 + 1.2f)), new Color(1f, 0.74f, 0.45f), 2.8f, 6f);
            }
            Vector3 ida = new Vector3(0.6f, 0f, 0.8f).normalized;
            Vector3 ini = centro - ida * 62f + Vector3.up * 40f, fim = centro - ida * 44f + Vector3.up * 37f;
            ModoVideo.Neblina = 0.45f;   // do alto, a neblina do chão cobria tudo
            yield return Gravar("5_alto", 9f, t =>
            {
                float s = Suave(t);
                var p = Vector3.Lerp(ini, fim, s);
                return (p, p + ida * 80f + Vector3.down * 34f, 55f);
            });
        }

        // ------------------------------------------------------------------ os lugares

        // a frente da estátua livre para a câmera chegar ao rosto: de 3 m até 1 m do rosto, sem nada no caminho e fora de casa
        static bool FrenteLivre(Zumbi z, out Vector3 cabeca)
        {
            cabeca = z.partes != null && z.partes.Length > CorpoDeZumbi.ParteCabeca && z.partes[CorpoDeZumbi.ParteCabeca] != null
                ? z.partes[CorpoDeZumbi.ParteCabeca].position : z.transform.position + Vector3.up * 1.6f;
            Vector3 frente = z.transform.forward;
            Vector3 longe = cabeca + frente * 3f + z.transform.right * 0.5f;
            if (AreaInterna.Contem(longe)) return false;
            var caminho = (cabeca + frente * 0.9f) - longe;
            if (Physics.SphereCast(longe, 0.35f, caminho.normalized, out _, caminho.magnitude, ~0, QueryTriggerInteraction.Ignore)) return false;
            // e atrás da câmera um pouco de espaço (para não nascer encostada numa parede)
            return !Physics.CheckSphere(longe, 0.5f, ~0, QueryTriggerInteraction.Ignore);
        }

        // um trecho de rua aberto perto da casa: uma estátua com 16 m livres de um lado, ou um ponto da rua mesmo
        bool LugarDeRua(Vector3 casa, out Vector3 alvo, out Vector3 dir)
        {
            alvo = casa;
            dir = Vector3.forward;
            var candidatos = new List<Zumbi>();
            foreach (var z in FindObjectsByType<Zumbi>())
                if (z.isActiveAndEnabled && z.Petrificado && (z.transform.position - casa).sqrMagnitude < 90f * 90f) candidatos.Add(z);
            candidatos.Sort((a, b) => (a.transform.position - casa).sqrMagnitude.CompareTo((b.transform.position - casa).sqrMagnitude));
            foreach (var z in candidatos)
                if (DirecaoLivre(z.transform.position, 17f, out dir) && CaminhoLivre(z.transform.position, dir))
                {
                    alvo = z.transform.position;
                    return true;
                }
            for (int i = 0; i < 60; i++)
            {
                var p = casa + new Vector3(Random.Range(-50f, 50f), 0f, Random.Range(-50f, 50f));
                if (!NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) continue;
                if (ZombieManager.Instancia.Coberto(hit.position)) continue;
                if (DirecaoLivre(hit.position, 17f, out dir) && CaminhoLivre(hit.position, dir))
                {
                    alvo = hit.position;
                    return true;
                }
            }
            return false;
        }

        // o caminho da câmera da rua (de 17 m até 7 m do alvo, de 0,6 m de um lado a 1,2 m do outro) sem nada a menos de
        // ~2,5 m: raios paralelos na altura do olho e na de um carro (um poste, uma árvore, um carro)
        static bool CaminhoLivre(Vector3 alvo, Vector3 dir)
        {
            Vector3 lado = Vector3.Cross(Vector3.up, dir);
            foreach (float altura in new[] { 0.9f, 1.65f })
                for (float de = -3.1f; de <= 3.75f; de += 0.85f)
                {
                    var origem = alvo + dir * 7f + lado * de + Vector3.up * altura;
                    if (Physics.SphereCast(origem, 0.4f, dir, out _, 10f, ~0, QueryTriggerInteraction.Ignore)) return false;
                }
            return true;
        }

        // a direção (no plano) com 'livre' metros sem nada na altura do olho, de 16 tentadas
        static bool DirecaoLivre(Vector3 de, float livre, out Vector3 dir)
        {
            float giro0 = Random.Range(0f, 360f);
            for (int k = 0; k < 16; k++)
            {
                dir = Quaternion.Euler(0f, giro0 + k * 22.5f, 0f) * Vector3.forward;
                var olho = de + Vector3.up * 1.6f;
                if (!Physics.SphereCast(olho, 0.4f, dir, out _, livre, ~0, QueryTriggerInteraction.Ignore)) return true;
            }
            dir = Vector3.forward;
            return false;
        }

        // estátuas em volta do alvo, do lado de onde a câmera vem e atrás dele (fora da linha da câmera); 'ate': no máximo
        // a tantos metros do alvo na direção da câmera (para ela não passar ao lado de nenhuma)
        void PorEstatuas(Vector3 alvo, Vector3 dir, int quantas, float perto, float longe, float ate = float.MaxValue)
        {
            var zm = ZombieManager.Instancia;
            Vector3 lado = Vector3.Cross(Vector3.up, dir);
            for (int i = 0, tentativas = 0; i < quantas && tentativas < quantas * 8; tentativas++)
            {
                float ao = Random.Range(-longe * 0.5f, Mathf.Min(longe, ate)), de = Random.Range(perto, perto + 3f) * (Random.value < 0.5f ? -1f : 1f);
                var p = alvo + dir * ao + lado * de;
                if (!NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) continue;
                if (!zm.CriarUm(hit.position, Random.Range(0f, 360f), true)) continue;
                var z = MaisPerto(x => x.Petrificado, hit.position, 1.5f);
                if (z != null) _estatuas.Add(z);
                i++;
            }
        }

        // uma janela de casa perto: o vão (sem colisão na altura do peito, com parede embaixo), o lado de fora e um ponto
        // do NavMesh do lado de dentro, a 1,3 m do vão
        bool AcharJanela(Vector3 casa, out Vector3 janela, out Vector3 fora, out Vector3 dentro)
        {
            janela = fora = dentro = default;
            var areas = new List<AreaInterna>(FindObjectsByType<AreaInterna>());
            areas.Sort((a, b) => (a.transform.position - casa).sqrMagnitude.CompareTo((b.transform.position - casa).sqrMagnitude));
            foreach (var area in areas)
            {
                if ((area.transform.position - casa).sqrMagnitude > 120f * 120f) break;
                var t = area.transform;
                var tam = area.tamanho;
                if (tam.x < 4f || tam.z < 4f || tam.y > 6f) continue;   // casas (os prédios têm os andares por conta própria)
                float piso = t.position.y - tam.y / 2f;
                var lados = new[] { (t.forward, tam.z / 2f, t.right, tam.x), (-t.forward, tam.z / 2f, t.right, tam.x), (t.right, tam.x / 2f, t.forward, tam.z), (-t.right, tam.x / 2f, t.forward, tam.z) };
                foreach (var (n, meio, ao, largura) in lados)
                    for (float s = -0.38f; s <= 0.38f; s += 0.06f)
                    {
                        Vector3 parede = t.position + n * meio + ao * (s * largura);
                        Vector3 peito = new Vector3(parede.x, piso + 1.45f, parede.z), baixo = new Vector3(parede.x, piso + 0.45f, parede.z);
                        // o vão: de fora para dentro, nada até passar da parede; embaixo, a parede (é janela, não porta)
                        if (Physics.Raycast(peito + n * 2.5f, -n, 3.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (!Physics.Raycast(baixo + n * 2.5f, -n, 3.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        // lá fora, espaço para a câmera (9 m livres)
                        if (Physics.SphereCast(peito + n * 0.6f, 0.3f, n, out _, 9f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (!NavMesh.SamplePosition(peito - n * 1.3f - Vector3.up * 1.45f, out var hit, 0.8f, NavMesh.AllAreas)) continue;
                        if (Mathf.Abs(hit.position.y - piso) > 0.4f) continue;
                        janela = peito;
                        fora = n;
                        dentro = hit.position;
                        return true;
                    }
            }
            return false;
        }

        static int CarroPerto(Vector3 p, float raio, out Vector3 pos, out Quaternion giro, out Bounds caixa)
        {
            pos = default;
            giro = Quaternion.identity;
            caixa = default;
            var d = MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;
            var objetos = ObjetosDoMundo.Instancia;
            if (d?.Objetos == null || objetos == null) return -1;
            var min = d.BlocoDe(p.x - raio, p.z - raio);
            var max = d.BlocoDe(p.x + raio, p.z + raio);
            int melhor = -1;
            float menor = raio * raio;
            for (int bz = min.y; bz <= max.y; bz++)
            for (int bx = min.x; bx <= max.x; bx++)
            {
                var lista = d.Objetos.DoBloco(bx, bz);
                if (lista == null) continue;
                foreach (int i in lista)
                {
                    if (!objetos.CaixaDoCarro(i, out var pc, out var g, out var c)) continue;
                    float dist = (pc - p).sqrMagnitude;
                    if (dist >= menor) continue;
                    // com espaço do lado para a câmera
                    var lado = g * Vector3.right;
                    if (Physics.SphereCast(pc + Vector3.up * 1.0f + lado * (c.extents.x + 0.5f), 0.3f, lado, out _, 7f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    menor = dist;
                    melhor = i;
                    pos = pc;
                    giro = g;
                    caixa = c;
                }
            }
            return melhor;
        }

        static Zumbi MaisPerto(Func<Zumbi, bool> serve, Vector3 p, float raio)
        {
            Zumbi melhor = null;
            float menor = raio * raio;
            foreach (var z in FindObjectsByType<Zumbi>())
            {
                if (!z.isActiveAndEnabled || z.Morto || !serve(z)) continue;
                float d = (z.transform.position - p).sqrMagnitude;
                if (d < menor) { menor = d; melhor = z; }
            }
            return melhor;
        }

        static float Chao(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out var hit, 20f, 1, QueryTriggerInteraction.Ignore) ? hit.point.y : p.y;

        void AfastarOJogador(Vector3 onde)
        {
            if (NavMesh.SamplePosition(onde, out var hit, 8f, NavMesh.AllAreas)) onde = hit.position;
            _j.GetComponent<MovimentoPrimeiraPessoa>().Teleportar(onde + Vector3.up * 0.1f, 0f);
        }

        void Piscante(Vector3 onde, Color cor, float forca, float alcance)
        {
            var go = new GameObject("Luz falhando");
            _criados.Add(go);
            go.transform.position = onde;
            var luz = go.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = cor;
            luz.range = alcance;
            luz.intensity = forca;
            luz.shadows = LightShadows.None;
            _piscando.Add((luz, forca, Random.Range(0f, 100f)));
        }
    }
}
