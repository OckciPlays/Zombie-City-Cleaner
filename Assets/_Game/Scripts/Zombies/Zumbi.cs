using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Um zumbi. No anfitrião ele pensa: vaga devagar, percebe os jogadores (visão que depende da luz, contato, passos
    /// correndo e tiros), persegue devagar, ataca de perto e desiste quando o alvo se afasta ou some de vista. Quem chama
    /// Pensar() é o ZombieManager, em fatias e no ritmo do nível de detalhe de cada um.
    /// Nos outros jogadores o zumbi é uma cópia: só segue as posições que chegam pela rede, com interpolação.
    /// Não tem Update próprio (com centenas de zumbis, o ZombieManager faz um laço só).
    /// </summary>
    public class Zumbi : MonoBehaviour
    {
        public enum Estado : byte { Parado, Vagando, Investigando, Procurando, Perseguindo, Atacando }
        public enum Nivel : byte { Perto, Medio, Longe }

        public const int CamadaZumbi = 7;
        // No nível Perto a cápsula só segura o jogador (Ignore Raycast): o tiro passa por ela e acerta as hitboxes.
        const int CamadaSoBloqueio = 2;
        const float DuracaoDoBote = 0.45f;

        [Header("Peças (a montagem preenche)")]
        public Animator animador;
        public NavMeshAgent agente;
        public CapsuleCollider capsula;
        public Hitbox[] hitboxes;
        public Renderer[] renderers;
        public AudioSource voz;
        [Tooltip("Ossos do bote (o golpe), girados por cima da animação.")]
        public Transform ossoPeito, ossoBracoE, ossoBracoD;

        static readonly int SpeedId = Animator.StringToHash("Speed");

        public int Id { get; private set; }
        /// <summary>Muda cada vez que o Id volta do pool: a cópia sabe que é outro zumbi.</summary>
        public byte Geracao { get; private set; }
        public bool Copia { get; private set; }
        public Estado Atual { get; private set; }
        /// <summary>Nível de detalhe aplicado: animação, hitboxes, sombra e (no anfitrião) navegação.</summary>
        public Nivel NivelAtual { get; private set; } = Nivel.Longe;
        /// <summary>Conta os golpes. Vai pela rede: a cópia toca o bote quando o número muda.</summary>
        public byte Golpes { get; private set; }
        public float Velocidade { get; private set; }
        /// <summary>Perseguindo, atacando ou levou tiro há pouco: nunca cai de nível.</summary>
        public bool Engajado => Atual == Estado.Perseguindo || Atual == Estado.Atacando || Time.time < _engajadoAte;
        internal bool TemBote => _bote > 0f;

        ZombieManager _m;
        bool _visivel = true;
        float _velAnimacao, _bote;

        // ------------------------------------------------------------------ comum

        internal void AplicarNivel(Nivel n, bool forcar = false)
        {
            if (n == NivelAtual && !forcar) return;
            NivelAtual = n;
            bool perto = n == Nivel.Perto, longe = n == Nivel.Longe;
            animador.enabled = !longe;
            animador.cullingMode = perto ? AnimatorCullingMode.CullUpdateTransforms : AnimatorCullingMode.CullCompletely;
            foreach (var h in hitboxes) h.colisor.enabled = perto;
            capsula.enabled = !longe;
            gameObject.layer = perto ? CamadaSoBloqueio : CamadaZumbi;
            var sombra = perto ? ShadowCastingMode.On : ShadowCastingMode.Off;
            foreach (var r in renderers) r.shadowCastingMode = sombra;
            AtualizarRenderers();
        }

        /// <summary>Longe demais de quem joga nesta máquina: a neblina esconde, então nem desenha.</summary>
        internal void DefinirVisivel(bool visivel)
        {
            if (visivel == _visivel) return;
            _visivel = visivel;
            AtualizarRenderers();
        }

        void AtualizarRenderers()
        {
            bool ligado = _visivel && NivelAtual != Nivel.Longe;
            foreach (var r in renderers) r.enabled = ligado;
        }

        void Animar(float dt)
        {
            // atacando ele para (a animação de ataque vem na Parte 6; por enquanto o bote mostra o golpe)
            float alvo = Atual == Estado.Atacando ? 0f : Velocidade;
            _velAnimacao = Mathf.Lerp(_velAnimacao, alvo, 1f - Mathf.Exp(-8f * dt));
            if (animador.enabled) animador.SetFloat(SpeedId, _velAnimacao);
            if (_bote > 0f) _bote = Mathf.Max(0f, _bote - dt / DuracaoDoBote);
        }

        void ComecarBote() => _bote = 1f;

        /// <summary>O golpe: o tronco inclina e os braços vão para a frente, por cima da animação (no LateUpdate).</summary>
        internal void AplicarBote()
        {
            // sem a animação reescrevendo os ossos a cada quadro, a rotação se acumularia
            if (!animador.enabled || !renderers[0].isVisible) return;
            float k = Mathf.Sin((1f - _bote) * Mathf.PI);
            Vector3 eixo = transform.right;
            if (ossoPeito != null) ossoPeito.rotation = Quaternion.AngleAxis(25f * k, eixo) * ossoPeito.rotation;
            if (ossoBracoE != null) ossoBracoE.rotation = Quaternion.AngleAxis(-45f * k, eixo) * ossoBracoE.rotation;
            if (ossoBracoD != null) ossoBracoD.rotation = Quaternion.AngleAxis(-45f * k, eixo) * ossoBracoD.rotation;
        }

        internal void Gemer(float tom)
        {
            if (voz == null || voz.isPlaying || _m == null || _m.vozDoZumbi == null || !_m.PodeGemer(transform.position)) return;
            var trecho = _m.TrechoDaVoz();
            voz.clip = _m.vozDoZumbi;
            voz.pitch = tom * Random.Range(0.85f, 1.05f);
            voz.Play();
            voz.time = trecho.x;
            voz.SetScheduledEndTime(AudioSettings.dspTime + trecho.y / voz.pitch);
        }

        /// <summary>Volta ao estado de fábrica, para o pool. (Na Parte 6, aqui também voltam a vida e os membros.)</summary>
        internal void Zerar()
        {
            _alvo = null; _alerta = 0f; _bote = 0f; _girando = false; _engajadoAte = 0f;
            Golpes = 0; Velocidade = 0f; _velAnimacao = 0f; _qtd = 0;
            Atual = Estado.Parado;
            if (agente.enabled)
            {
                if (agente.isOnNavMesh) agente.ResetPath();
                agente.enabled = false;
            }
            if (voz != null) voz.Stop();
            if (animador.isActiveAndEnabled) animador.Rebind();
        }

        // ------------------------------------------------------------------ anfitrião: a IA

        internal float proximaDecisao, ultimaDecisao;
        JogadorRede _alvo;
        Vector3 _casa, _ultimaPosicao, _olharPara;
        float _fimDaPausa, _alerta, _ultimoVisto, _proximoGolpe, _proximoCaminho, _engajadoAte, _preguica, _giro;
        bool _girando;

        bool NoNavMesh => agente.enabled && agente.isOnNavMesh;

        internal void PrepararNoServidor(int id, byte geracao, Vector3 posicao, float yaw, ZombieManager m)
        {
            Id = id; Geracao = geracao; Copia = false; _m = m;
            transform.SetPositionAndRotation(posicao, Quaternion.Euler(0f, yaw, 0f));
            _casa = posicao;
            _preguica = Random.value < 0.33f ? 3f : 1f;
            Atual = Estado.Parado;
            _fimDaPausa = Time.time + Random.Range(0f, 4f);
            proximaDecisao = ultimaDecisao = Time.time;
            _visivel = true;
            AplicarNivel(Nivel.Longe, true);
        }

        /// <summary>Muda o nível de detalhe no anfitrião: além do visual, liga ou desliga a navegação.</summary>
        internal void MudarNivelNoServidor(Nivel n)
        {
            if (n == NivelAtual) return;
            AplicarNivel(n);
            if (n == Nivel.Longe)
            {
                // zumbi virtual: guarda só a posição e o estado, sem agente nem animação
                if (agente.enabled)
                {
                    if (agente.isOnNavMesh) agente.ResetPath();
                    agente.enabled = false;
                }
                if (Atual != Estado.Parado) Pausar(Time.time);
                Velocidade = 0f;
                return;
            }
            if (!agente.enabled)
            {
                if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas)) transform.position = hit.position;
                agente.enabled = true;
            }
            agente.obstacleAvoidanceType = n == Nivel.Perto ? ObstacleAvoidanceType.MedQualityObstacleAvoidance : ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }

        /// <summary>A cada quadro (níveis Perto e Médio): velocidade, giro e animação.</summary>
        internal void QuadroNoServidor(float dt)
        {
            Velocidade = agente.enabled ? agente.velocity.magnitude : 0f;
            if (_girando)
            {
                Vector3 dir = _olharPara - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 300f * dt);
            }
            else if (Atual == Estado.Procurando) transform.Rotate(0f, _giro * dt, 0f);
            Animar(dt);
        }

        /// <summary>Uma decisão. No nível Médio ele só vaga e responde a tiros; a percepção é só no Perto.</summary>
        internal void Pensar(float agora, float dt)
        {
            if (!NoNavMesh) return;
            var a = _m.ajustes;
            if (NivelAtual == Nivel.Perto && Atual != Estado.Perseguindo && Atual != Estado.Atacando) Perceber(agora, dt, a);
            switch (Atual)
            {
                case Estado.Parado:
                    if (agora >= _fimDaPausa) Vagar(agora, a);
                    break;
                case Estado.Vagando:
                    if (Chegou()) Pausar(agora);
                    break;
                case Estado.Investigando:
                    if (Chegou()) Procurar(agora);
                    break;
                case Estado.Procurando:
                    if (agora >= _fimDaPausa)
                    {
                        _casa = transform.position;
                        Vagar(agora, a);
                    }
                    break;
                case Estado.Perseguindo:
                    Perseguir(agora, a);
                    break;
                case Estado.Atacando:
                    Atacar(agora, a);
                    break;
            }
        }

        void Perceber(float agora, float dt, AjustesDeZumbi a)
        {
            Vector3 olho = transform.position + Vector3.up * 1.55f;
            float maior = 0f;
            int quem = -1;
            for (int i = 0; i < _m.QuantosJogadores; i++)
            {
                ref readonly var p = ref _m.Jogadores[i];
                Vector3 para = p.peito - olho;
                float d = para.magnitude;
                float forca;
                if (d < a.distanciaDeContato || (p.correndo && d < a.barulhoDeCorrida)) forca = 100f;   // sente ou ouve: na hora
                else
                {
                    float alcance = _m.AlcanceDaVisao(p);
                    if (d > alcance) continue;
                    bool naFrente = Vector3.Angle(transform.forward, para) < a.meioAnguloDaVisao;
                    // a luz da lanterna batendo nele: nota mesmo de costas
                    bool luzNele = p.lanterna && Vector3.Angle(p.olhar, -para) < 25f;
                    if ((!naFrente && !luzNele) || !_m.Enxerga(olho, p.peito)) continue;
                    forca = Mathf.Lerp(4f, 1.2f, d / alcance);   // quanto mais perto, mais rápido ele percebe
                }
                if (forca > maior) { maior = forca; quem = i; }
            }
            if (quem < 0)
            {
                _alerta = Mathf.Max(0f, _alerta - dt * 0.5f);
                return;
            }
            _alerta += maior * dt;
            ref readonly var visto = ref _m.Jogadores[quem];
            if (_alerta >= 1f)
            {
                ComecarPerseguicao(visto.jogador, agora);
                return;
            }
            // desconfiado: para e vira para o que viu
            if (Atual == Estado.Vagando || Atual == Estado.Parado)
            {
                agente.ResetPath();
                Atual = Estado.Parado;
                _fimDaPausa = agora + 1.5f;
                OlharPara(visto.pes);
            }
        }

        void Vagar(float agora, AjustesDeZumbi a)
        {
            for (int t = 0; t < 4; t++)
            {
                Vector2 r = Random.insideUnitCircle * a.raioDeVagar;
                Vector3 p = _casa + new Vector3(r.x, 0f, r.y);
                if (NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - _casa.y) < 1.2f)
                {
                    Ir(hit.position, a.velocidadeVagando, 0.4f);
                    Atual = Estado.Vagando;
                    return;
                }
            }
            Pausar(agora);
        }

        void Ir(Vector3 destino, float velocidade, float parada)
        {
            agente.speed = velocidade;
            agente.stoppingDistance = parada;
            agente.isStopped = false;
            agente.SetDestination(destino);
            _girando = false;
        }

        void Pausar(float agora)
        {
            if (NoNavMesh) agente.ResetPath();
            Atual = Estado.Parado;
            var a = _m.ajustes;
            _fimDaPausa = agora + Random.Range(a.pausa.x, a.pausa.y) * _preguica;
        }

        void Procurar(float agora)
        {
            if (NoNavMesh) agente.ResetPath();
            Atual = Estado.Procurando;
            _fimDaPausa = agora + Random.Range(3f, 6f);
            _giro = Random.value < 0.5f ? -45f : 45f;
            _girando = false;
        }

        void OlharPara(Vector3 ponto)
        {
            _olharPara = ponto;
            _girando = true;
        }

        bool Chegou() => !agente.pathPending && (!agente.hasPath || agente.remainingDistance <= agente.stoppingDistance + 0.2f);

        /// <summary>Foi ver um barulho: anda até lá, olha em volta e passa a vagar ali.</summary>
        internal void Investigar(Vector3 ponto, float agora)
        {
            if (!NoNavMesh) return;
            _alvo = null;
            if (NavMesh.SamplePosition(ponto, out var hit, 3f, NavMesh.AllAreas)) ponto = hit.position;
            Ir(ponto, _m.ajustes.velocidadeInvestigando, 1f);
            Atual = Estado.Investigando;
        }

        internal void ComecarPerseguicao(JogadorRede jogador, float agora)
        {
            if (jogador == null) return;
            bool novo = Atual != Estado.Perseguindo && Atual != Estado.Atacando;
            _alvo = jogador;
            _alerta = 1f;
            _ultimoVisto = agora;
            _ultimaPosicao = jogador.transform.position;
            _proximoCaminho = 0f;
            Atual = Estado.Perseguindo;
            _m.Engajou(this);   // sobe direto para o nível Perto (liga o agente, se estava desligado)
            if (NoNavMesh) Ir(_ultimaPosicao, _m.ajustes.velocidadePerseguindo, 0.9f);
            if (novo) Gemer(1.05f);
        }

        /// <summary>Levou tiro (Parte 6): vai atrás de quem atirou e fica no nível Perto por um tempo.</summary>
        internal void LevouTiro(JogadorRede autor)
        {
            _engajadoAte = Time.time + 5f;
            if (autor != null) ComecarPerseguicao(autor, Time.time);
        }

        void Perseguir(float agora, AjustesDeZumbi a)
        {
            if (_alvo == null || !_alvo.IsSpawned)
            {
                Desistir(agora);
                return;
            }
            Vector3 pos = _alvo.transform.position;
            Vector3 delta = pos - transform.position;
            float d = delta.magnitude;
            if (d > a.distanciaDeDesistir)
            {
                Desistir(agora);
                return;
            }
            if (d < 3f || _m.Enxerga(transform.position + Vector3.up * 1.55f, pos + Vector3.up * 1.3f))
            {
                _ultimoVisto = agora;
                _ultimaPosicao = pos;
            }
            if (agora - _ultimoVisto > a.tempoSemVer)
            {
                // sumiu de vista: vai até onde o viu por último, olha em volta e desiste
                Investigar(_ultimaPosicao, agora);
                return;
            }
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            if (horizontal < a.alcanceDoAtaque && Mathf.Abs(delta.y) < 1.2f)
            {
                Atual = Estado.Atacando;
                agente.ResetPath();
                _proximoGolpe = Mathf.Max(_proximoGolpe, agora + 0.25f);
                return;
            }
            if (agora >= _proximoCaminho)
            {
                Ir(_ultimaPosicao, a.velocidadePerseguindo, 0.9f);
                _proximoCaminho = agora + 0.25f;
            }
        }

        void Atacar(float agora, AjustesDeZumbi a)
        {
            if (_alvo == null || !_alvo.IsSpawned)
            {
                Desistir(agora);
                return;
            }
            Vector3 pos = _alvo.transform.position;
            Vector3 delta = pos - transform.position;
            if (new Vector2(delta.x, delta.z).magnitude > a.alcanceDoAtaque + 0.4f || Mathf.Abs(delta.y) > 1.4f)
            {
                Atual = Estado.Perseguindo;
                _proximoCaminho = 0f;
                return;
            }
            _ultimoVisto = agora;
            _ultimaPosicao = pos;
            OlharPara(pos);
            if (agora >= _proximoGolpe)
            {
                _proximoGolpe = agora + a.intervaloDoGolpe;
                Golpes++;
                ComecarBote();
                _m.Golpear(_alvo, a.danoDoGolpe, transform.position);
            }
        }

        void Desistir(float agora)
        {
            _alvo = null;
            _alerta = 0f;
            _casa = transform.position;
            Procurar(agora);   // olha em volta um pouco e depois volta a vagar ali mesmo
        }

        // ------------------------------------------------------------------ cópia (nos outros jogadores)

        struct Amostra
        {
            public double tempo;
            public Vector3 posicao;
            public float yaw;
        }

        readonly Amostra[] _amostras = new Amostra[4];
        int _qtd;
        float _intervalo = 0.1f, _atraso = 0.2f;
        internal float ultimoRecebido;

        internal void PrepararCopia(int id, byte geracao, ZombieManager m)
        {
            Id = id; Geracao = geracao; Copia = true; _m = m;
            agente.enabled = false;
            _qtd = 0; _intervalo = 0.1f; _atraso = 0.2f;
            _visivel = true;
            AplicarNivel(Nivel.Medio, true);
        }

        internal void Receber(double tempo, Vector3 posicao, float yaw, Estado estado, byte golpes, float velocidade)
        {
            ultimoRecebido = Time.unscaledTime;
            bool primeira = _qtd == 0;
            if (primeira) transform.SetPositionAndRotation(posicao, Quaternion.Euler(0f, yaw, 0f));
            else
            {
                double ultimo = _amostras[_qtd - 1].tempo;
                if (tempo <= ultimo) return;   // chegou fora de ordem
                _intervalo = Mathf.Lerp(_intervalo, (float)(tempo - ultimo), 0.3f);
            }
            if (_qtd == _amostras.Length)
            {
                for (int i = 1; i < _qtd; i++) _amostras[i - 1] = _amostras[i];
                _qtd--;
            }
            _amostras[_qtd++] = new Amostra { tempo = tempo, posicao = posicao, yaw = yaw };
            if (!primeira && estado == Estado.Perseguindo && Atual != Estado.Perseguindo && Atual != Estado.Atacando) Gemer(1.05f);
            if (!primeira && golpes != (Golpes & 3)) ComecarBote();
            Atual = estado;
            Golpes = golpes;
            Velocidade = velocidade;
        }

        /// <summary>
        /// Mostra a cópia um pouco no passado, entre as duas amostras que cercam esse momento. O atraso acompanha o
        /// ritmo de envio: ~0,2 s para os zumbis perto (10 envios/s) e ~0,5 s para os do nível médio.
        /// </summary>
        internal void QuadroDaCopia(double agoraNoServidor, float dt)
        {
            if (_qtd == 0) return;
            float atrasoAlvo = Mathf.Clamp(_intervalo * 1.6f + 0.05f, 0.12f, 0.7f);
            _atraso = Mathf.MoveTowards(_atraso, atrasoAlvo, dt * 0.25f);
            double t = agoraNoServidor - _atraso;
            Amostra a = _amostras[_qtd - 1], b = a;
            if (t < a.tempo)
            {
                a = b = _amostras[0];
                for (int i = 1; i < _qtd; i++)
                    if (t < _amostras[i].tempo)
                    {
                        a = _amostras[i - 1];
                        b = _amostras[i];
                        break;
                    }
            }
            float k = b.tempo > a.tempo ? Mathf.Clamp01((float)((t - a.tempo) / (b.tempo - a.tempo))) : 1f;
            transform.SetPositionAndRotation(Vector3.Lerp(a.posicao, b.posicao, k), Quaternion.Euler(0f, Mathf.LerpAngle(a.yaw, b.yaw, k), 0f));
            Animar(dt);
        }
    }
}
