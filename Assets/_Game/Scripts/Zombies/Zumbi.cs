using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Um zumbi. No anfitrião ele pensa: vaga devagar, percebe os jogadores (visão que depende da luz, contato, passos
    /// correndo e tiros), persegue devagar, ataca de perto e desiste quando o alvo se afasta ou some de vista. Quem chama
    /// Pensar() é o ZombieManager, em fatias e no ritmo do nível de detalhe de cada um.
    /// Também leva tiro (só no anfitrião): cada membro tem vida própria e cai quando ela acaba; a cabeça explode com um
    /// tiro; sem perna, ele cai e rasteja.
    /// Nos outros jogadores o zumbi é uma cópia: segue as posições que chegam pela rede, com interpolação, e mostra o
    /// estado recebido (membros, morte, golpes e reações).
    /// Não tem Update próprio (com centenas de zumbis, o ZombieManager faz um laço só).
    /// </summary>
    public class Zumbi : MonoBehaviour
    {
        public enum Estado : byte { Parado, Vagando, Investigando, Procurando, Perseguindo, Atacando, Morto }
        public enum Nivel : byte { Perto, Medio, Longe }

        /// <summary>Um membro que pode cair: o osso que ele segue, a peça solta e a pose dela em relação ao osso.</summary>
        [System.Serializable]
        public struct Pedaco
        {
            public Membros membro;
            public Transform osso;
            [Tooltip("O osso seguinte, para saber para onde o toco aponta.")]
            public Transform ossoFilho;
            [Tooltip("A peça que cai (vazia na cabeça, que explode).")]
            public Mesh malha;
            public Material[] materiais;
            public Vector3 posicao;
            public Quaternion rotacao;
        }

        public const int CamadaZumbi = 7;
        // No nível Perto a cápsula só segura o jogador (Ignore Raycast): o tiro passa por ela e acerta as hitboxes.
        const int CamadaSoBloqueio = 2;
        const float DuracaoDoBote = 0.45f;
        const int CamadaDeAcoes = 1;

        [Header("Peças (a montagem preenche)")]
        public Animator animador;
        public NavMeshAgent agente;
        public CapsuleCollider capsula;
        public Hitbox[] hitboxes;
        public Renderer[] renderers;
        public AudioSource voz;
        [Tooltip("Ossos do bote (o golpe rastejando), girados por cima da animação.")]
        public Transform ossoPeito, ossoBracoE, ossoBracoD;
        public Pedaco[] pedacos;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int RastejandoId = Animator.StringToHash("Rastejando");
        static readonly int MortoId = Animator.StringToHash("Morto");
        static readonly int AtacarId = Animator.StringToHash("Atacar");
        static readonly int TiroId = Animator.StringToHash("Tiro");

        public int Id { get; private set; }
        /// <summary>Muda cada vez que o Id volta do pool: a cópia sabe que é outro zumbi.</summary>
        public byte Geracao { get; private set; }
        public bool Copia { get; private set; }
        public Estado Atual { get; private set; }
        /// <summary>Nível de detalhe aplicado: animação, hitboxes, sombra e (no anfitrião) navegação.</summary>
        public Nivel NivelAtual { get; private set; } = Nivel.Longe;
        /// <summary>Conta os golpes. Vai pela rede: a cópia toca o ataque quando o número muda.</summary>
        public byte Golpes { get; private set; }
        /// <summary>Conta os tiros que ele aguentou. Vai pela rede: a cópia toca a reação quando muda.</summary>
        public byte Reacoes { get; private set; }
        public Membros Membros { get; private set; }
        public float Velocidade { get; private set; }
        public bool Morto => Atual == Estado.Morto;
        public bool Rastejando => (Membros & Membros.Pernas) != 0;
        /// <summary>Perseguindo, atacando ou levou tiro há pouco: nunca cai de nível.</summary>
        public bool Engajado => Atual == Estado.Perseguindo || Atual == Estado.Atacando || Time.time < _engajadoAte;
        internal bool TemBote => _bote > 0f;
        internal float mortoEm;

        ZombieManager _m;
        bool _visivel = true;
        float _velAnimacao, _bote;

        // ------------------------------------------------------------------ comum

        internal void AplicarNivel(Nivel n, bool forcar = false)
        {
            if (n == NivelAtual && !forcar) return;
            NivelAtual = n;
            bool perto = n == Nivel.Perto, longe = n == Nivel.Longe;
            bool estavaDesligado = !animador.enabled;
            animador.enabled = !longe;
            animador.cullingMode = perto ? AnimatorCullingMode.CullUpdateTransforms : AnimatorCullingMode.CullCompletely;
            if (estavaDesligado && animador.enabled) ParametrosDoAnimador();
            capsula.gameObject.layer = perto ? CamadaSoBloqueio : CamadaZumbi;
            AtualizarColisores();
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

        // hitboxes só no nível Perto e só dos membros que ainda estão lá; morto, nada leva tiro
        void AtualizarColisores()
        {
            bool perto = NivelAtual == Nivel.Perto;
            foreach (var h in hitboxes)
                h.colisor.enabled = perto && !Morto && !MembrosDoZumbi.Perdeu(Membros, MembrosDoZumbi.DaRegiao(h.regiao));
            capsula.enabled = NivelAtual != Nivel.Longe && !Morto;
        }

        // o Animator desligado (nível Longe) esquece os parâmetros: ao religar, eles voltam
        void ParametrosDoAnimador()
        {
            if (!animador.isActiveAndEnabled) return;
            animador.SetBool(RastejandoId, Rastejando);
            animador.SetBool(MortoId, Morto);
            animador.SetLayerWeight(CamadaDeAcoes, Rastejando || Morto ? 0f : 1f);
        }

        void Animar(float dt)
        {
            float alvo = Atual == Estado.Atacando || Morto ? 0f : Velocidade;
            _velAnimacao = Mathf.Lerp(_velAnimacao, alvo, 1f - Mathf.Exp(-8f * dt));
            if (animador.enabled) animador.SetFloat(SpeedId, _velAnimacao);
            if (_bote > 0f) _bote = Mathf.Max(0f, _bote - dt / DuracaoDoBote);
        }

        // o golpe: em pé, a animação de ataque (só o tronco); rastejando, o bote do peito
        void MostrarGolpe()
        {
            if (Rastejando) _bote = 1f;
            else if (animador.isActiveAndEnabled) animador.SetTrigger(AtacarId);
        }

        void MostrarReacao()
        {
            if (!Rastejando && animador.isActiveAndEnabled) animador.SetTrigger(TiroId);
        }

        void MostrarMorte(bool comEfeito)
        {
            ParametrosDoAnimador();
            AtualizarColisores();
            if (voz != null) voz.Stop();
            if (comEfeito) Sangue.Mancha(transform.position + transform.forward * 0.5f, 1.1f);
        }

        /// <summary>O bote rastejando: o peito avança por cima da animação (no LateUpdate).</summary>
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

        /// <summary>
        /// Troca o que falta no corpo: a malha (sem as regiões perdidas e com as tampas de carne), as hitboxes, a
        /// cápsula e a animação (sem perna, rasteja). Com efeito: os pedaços caem, o toco jorra e a cabeça explode.
        /// </summary>
        internal void AplicarMembros(Membros novo, bool comEfeito)
        {
            var antes = Membros;
            Membros = novo;
            var smr = (SkinnedMeshRenderer)renderers[0];
            if (novo == Membros.Nenhum)
            {
                smr.sharedMesh = _m.malhaInteira;
                smr.sharedMaterials = _m.MateriaisInteiro;
            }
            else
            {
                smr.sharedMesh = MembrosDoZumbi.Variante(_m.malhaCompleta, _m.mapa, novo);
                smr.sharedMaterials = _m.MateriaisFerido;
            }
            if (Rastejando)
            {
                // deitado: a cápsula vai ao longo do corpo, da cabeça (à frente) aos pés (atrás)
                capsula.direction = 2; capsula.center = new Vector3(0f, 0.22f, -0.2f); capsula.height = 1.6f; capsula.radius = 0.22f;
            }
            else
            {
                capsula.direction = 1; capsula.center = new Vector3(0f, 0.875f, 0f); capsula.height = 1.75f; capsula.radius = 0.28f;
            }
            AtualizarColisores();
            ParametrosDoAnimador();
            if (!comEfeito) return;
            var perdidos = novo & ~antes;
            foreach (var p in pedacos)
            {
                if ((perdidos & p.membro) == 0) continue;
                // o braço (ou a perna) inteiro já traz o antebraço (ou a canela): uma peça só
                if ((p.membro == Membros.AntebracoE && (perdidos & Membros.BracoE) != 0) || (p.membro == Membros.AntebracoD && (perdidos & Membros.BracoD) != 0) ||
                    (p.membro == Membros.CanelaE && (perdidos & Membros.CoxaE) != 0) || (p.membro == Membros.CanelaD && (perdidos & Membros.CoxaD) != 0)) continue;
                EfeitoDoMembro(p, antes);
            }
        }

        void EfeitoDoMembro(in Pedaco p, Membros antes)
        {
            if (p.osso == null) return;
            Vector3 toco = p.ossoFilho != null ? (p.ossoFilho.position - p.osso.position).normalized : Vector3.up;
            if (p.membro == Membros.Cabeca)
            {
                Sangue.Explosao(p.osso.position + toco * 0.12f);
                Sangue.Jorro(p.osso, toco, 4f);
                return;
            }
            // sem o antebraço (ou a canela) que já tinha caído, sobra só o toco: não há peça para soltar
            bool soToco = (p.membro == Membros.BracoE && (antes & Membros.AntebracoE) != 0) || (p.membro == Membros.BracoD && (antes & Membros.AntebracoD) != 0) ||
                          (p.membro == Membros.CoxaE && (antes & Membros.CanelaE) != 0) || (p.membro == Membros.CoxaD && (antes & Membros.CanelaD) != 0);
            if (!soToco && p.malha != null)
            {
                // um empurrão curto para fora do toco: o braço esticado aponta para quem ele queria agarrar, e um empurrão
                // forte jogava o pedaço na cara do jogador
                Vector3 empurrao = (toco * 0.5f + Vector3.up * 0.3f + Random.insideUnitSphere * 0.5f).normalized * Random.Range(0.8f, 1.6f);
                Sangue.Pedaco(p.malha, p.materiais, p.osso.TransformPoint(p.posicao), p.osso.rotation * p.rotacao, empurrao);
            }
            Sangue.Respingo(p.osso.position, toco, 30);
            Sangue.Jorro(p.osso, toco);
            Sangue.Mancha(p.osso.position, 0.7f);
        }

        internal void Gemer(float tom)
        {
            if (Morto || voz == null || voz.isPlaying || _m == null || _m.vozDoZumbi == null || !_m.PodeGemer(transform.position)) return;
            var trecho = _m.TrechoDaVoz();
            voz.clip = _m.vozDoZumbi;
            voz.pitch = tom * Random.Range(0.85f, 1.05f);
            voz.Play();
            voz.time = trecho.x;
            voz.SetScheduledEndTime(AudioSettings.dspTime + trecho.y / voz.pitch);
        }

        /// <summary>Volta ao estado de fábrica, para o pool: inteiro, vivo, de pé, sem alvo.</summary>
        internal void Zerar()
        {
            _alvo = null; _alerta = 0f; _bote = 0f; _girando = false; _engajadoAte = 0f; _caindoAte = 0f; _golpeEm = 0f;
            _danoNoCorpo = 0f;
            System.Array.Clear(_danoNoMembro, 0, _danoNoMembro.Length);
            Golpes = 0; Reacoes = 0; Velocidade = 0f; _velAnimacao = 0f; _qtd = 0;
            Atual = Estado.Parado;
            if (_m != null && Membros != Membros.Nenhum) AplicarMembros(Membros.Nenhum, false);
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
        float _fimDaPausa, _alerta, _ultimoVisto, _proximoGolpe, _proximoCaminho, _engajadoAte, _preguica, _giro, _caindoAte, _golpeEm;
        float _danoNoCorpo;
        readonly float[] _danoNoMembro = new float[9];
        bool _girando;

        bool NoNavMesh => agente.enabled && agente.isOnNavMesh;
        float AlturaDoOlho => Rastejando ? 0.45f : 1.55f;

        // rastejando tudo fica mais lento; sem perna e sem mão, quase parado
        float Vel(float v)
        {
            if (!Rastejando) return v;
            return v * _m.ajustes.fatorRastejando * (Maos == 0 ? 0.3f : 1f);
        }

        int Maos => (MembrosDoZumbi.Perdeu(Membros, Membros.AntebracoE) ? 0 : 1) + (MembrosDoZumbi.Perdeu(Membros, Membros.AntebracoD) ? 0 : 1);

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
            if (Morto) return;   // o corpo não anda mais
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
            if (_girando && !Morto)
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
            if (Morto || !NoNavMesh) return;
            if (agora < _caindoAte) return;   // caindo no chão depois de perder a perna
            var a = _m.ajustes;
            if (_golpeEm > 0f && agora >= _golpeEm) Acertar(a);
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
            Vector3 olho = transform.position + Vector3.up * AlturaDoOlho;
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
                    Ir(hit.position, Vel(a.velocidadeVagando), 0.4f);
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
            if (!NoNavMesh || Morto) return;
            _alvo = null;
            if (NavMesh.SamplePosition(ponto, out var hit, 3f, NavMesh.AllAreas)) ponto = hit.position;
            Ir(ponto, Vel(_m.ajustes.velocidadeInvestigando), 1f);
            Atual = Estado.Investigando;
        }

        internal void ComecarPerseguicao(JogadorRede jogador, float agora)
        {
            if (jogador == null || Morto) return;
            bool novo = Atual != Estado.Perseguindo && Atual != Estado.Atacando;
            _alvo = jogador;
            _alerta = 1f;
            _ultimoVisto = agora;
            _ultimaPosicao = jogador.transform.position;
            _proximoCaminho = 0f;
            Atual = Estado.Perseguindo;
            _m.Engajou(this);   // sobe direto para o nível Perto (liga o agente, se estava desligado)
            if (NoNavMesh && agora >= _caindoAte) Ir(_ultimaPosicao, Vel(_m.ajustes.velocidadePerseguindo), 0.9f);
            if (novo) Gemer(1.05f);
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
            if (d < 3f || _m.Enxerga(transform.position + Vector3.up * AlturaDoOlho, pos + Vector3.up * 1.3f))
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
                Ir(_ultimaPosicao, Vel(a.velocidadePerseguindo), 0.9f);
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
                _golpeEm = agora + a.atrasoDoDano;   // o dano sai quando a animação agarra
                Golpes++;
                MostrarGolpe();
            }
        }

        // o momento do golpe: se o alvo ainda está ao alcance, tira vida (menos sem as mãos)
        void Acertar(AjustesDeZumbi a)
        {
            _golpeEm = 0f;
            if (_alvo == null || !_alvo.IsSpawned) return;
            Vector3 delta = _alvo.transform.position - transform.position;
            if (new Vector2(delta.x, delta.z).magnitude > a.alcanceDoAtaque + 0.5f || Mathf.Abs(delta.y) > 1.4f) return;
            float maos = Maos == 2 ? 1f : Maos == 1 ? 0.65f : 0.3f;
            _m.Golpear(_alvo, a.danoDoGolpe * maos, transform.position);
        }

        void Desistir(float agora)
        {
            _alvo = null;
            _alerta = 0f;
            _casa = transform.position;
            Procurar(agora);   // olha em volta um pouco e depois volta a vagar ali mesmo
        }

        /// <summary>
        /// Um tiro confirmado pelo anfitrião. O membro atingido perde vida e cai quando ela acaba (com uma parte do
        /// dano indo para o corpo); a cabeça explode com um tiro; o corpo sem vida cai morto.
        /// </summary>
        internal void LevarTiro(RegiaoDoCorpo regiao, float dano, JogadorRede autor, float agora)
        {
            if (Morto) return;
            var a = _m.ajustes;
            var membro = MembrosDoZumbi.DaRegiao(regiao);
            // a hitbox de um membro que já caiu (a cópia de quem atirou estava atrasada): conta como raspão no corpo
            if (membro != Membros.Nenhum && MembrosDoZumbi.Perdeu(Membros, membro))
            {
                membro = Membros.Nenhum;
                dano *= 0.5f;
            }
            if (membro == Membros.Cabeca)
            {
                Perder(Membros.Cabeca, agora);
                Morrer(agora);
                return;
            }
            if (membro != Membros.Nenhum)
            {
                int i = Indice(membro);
                _danoNoMembro[i] += dano;
                _danoNoCorpo += dano * a.membroNoCorpo;
                if (_danoNoMembro[i] >= VidaDoMembro(membro, a)) Perder(membro, agora);
            }
            else _danoNoCorpo += dano;
            if (_danoNoCorpo >= a.vidaDoCorpo)
            {
                Morrer(agora);
                return;
            }
            Reacoes++;
            MostrarReacao();
            _engajadoAte = agora + 5f;
            if (autor != null) ComecarPerseguicao(autor, agora);
        }

        static int Indice(Membros m)
        {
            int i = 0;
            for (ushort v = (ushort)m; v > 1; v >>= 1) i++;
            return i;
        }

        static float VidaDoMembro(Membros m, AjustesDeZumbi a)
        {
            switch (m)
            {
                case Membros.Cabeca: return a.vidaDaCabeca;
                case Membros.BracoE: case Membros.BracoD: return a.vidaDoBraco;
                case Membros.AntebracoE: case Membros.AntebracoD: return a.vidaDoAntebraco;
                case Membros.CoxaE: case Membros.CoxaD: return a.vidaDaCoxa;
                default: return a.vidaDaCanela;
            }
        }

        void Perder(Membros membro, float agora)
        {
            bool estavaDePe = !Rastejando;
            AplicarMembros(MembrosDoZumbi.ComOQueVemJunto(Membros | membro), true);
            if (estavaDePe && Rastejando)
            {
                // perdeu a perna: cai no chão (animação de queda) e depois segue rastejando
                _caindoAte = agora + 0.85f;
                if (NoNavMesh) agente.ResetPath();
                if (Atual == Estado.Atacando) Atual = Estado.Perseguindo;
            }
        }

        void Morrer(float agora)
        {
            if (Morto) return;
            Atual = Estado.Morto;
            mortoEm = agora;
            _alvo = null;
            _golpeEm = 0f;
            if (agente.enabled)
            {
                if (agente.isOnNavMesh) agente.ResetPath();
                agente.enabled = false;
            }
            Velocidade = 0f;
            _m.ContarMorte();
            MostrarMorte(true);
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

        internal void Receber(double tempo, Vector3 posicao, float yaw, Estado estado, byte golpes, byte reacoes, Membros membros, float velocidade)
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
            // o que mudou desde o último pacote vira efeito; na primeira vez, o zumbi só aparece como já está
            if (membros != Membros) AplicarMembros(membros, !primeira);
            if (!primeira && estado == Estado.Perseguindo && Atual != Estado.Perseguindo && Atual != Estado.Atacando) Gemer(1.05f);
            if (!primeira && golpes != (Golpes & 3)) MostrarGolpe();
            if (!primeira && reacoes != (Reacoes & 3)) MostrarReacao();
            bool morreu = estado == Estado.Morto && Atual != Estado.Morto;
            Atual = estado;
            Golpes = golpes;
            Reacoes = reacoes;
            Velocidade = velocidade;
            if (morreu) MostrarMorte(!primeira);
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
