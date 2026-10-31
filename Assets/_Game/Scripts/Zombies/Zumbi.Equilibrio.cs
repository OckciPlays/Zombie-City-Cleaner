using UnityEngine;
using UnityEngine.AI;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O equilíbrio sem uma perna (31/10/2026, a pedido: "Quando ele perder uma das pernas, ele vai começar a tombar pros
    /// lados, cair no chão, pode começar a se arrastejar mesmo com uma perna, pode tentar se levantar e cair novamente, pode
    /// tentar levantar, manter o equilibrio e conseguir andar com uma perna pulando"). Só com uma perna: sem as duas, o
    /// desmembramento de sempre (cai e rasteja).
    /// <para>
    /// Quem decide é o anfitrião (as fases, a inclinação e os sorteios, com os números em <see cref="AjustesDeZumbi"/>); as
    /// cópias recebem a fase e a inclinação num byte. Perdendo a perna, ele cai na hora ou cambaleia: o corpo inteiro
    /// tomba para os lados como um pêndulo apoiado no pé que sobrou (o "Modelo" girado em volta dele, por cima da
    /// animação), e ele pula de lado tentando se segurar; termina caindo ou se firmando (Firme: pula numa perna, como
    /// antes, e um tiro pode derrubá-lo de novo). A queda é com física: nesta máquina, o corpo do pool do modelo (o mesmo
    /// da morte, vivo) cai para o lado da inclinação; acabada a queda, o zumbi volta deitado de bruços (parado no primeiro
    /// quadro do levantar) e a pose sai da do boneco aos poucos. Deitado, ele decide: tenta levantar ou rasteja numa perna
    /// (e de tempos em tempos tenta de novo, até desistir).
    /// </para>
    /// <para>
    /// O levantar (desde 31/10/2026, a pedido: "Ele está levantando de repente, sem utilizar o apoio dos braços [...] algo
    /// mais cinematografico"): os clipes LevantarE e LevantarD, feitos pose a pose no Blender (Blender/equilibrio_oco.py),
    /// ~4,5 s: as mãos vêm para baixo dos ombros, ele empurra o chão (os braços falham uma vez), põe o joelho embaixo do
    /// corpo, quase tomba para o lado do coto, arrasta o pé até ficar agachado numa perna e sobe empurrando o chão. Quando
    /// a tentativa falha, ele cai num desses três momentos fracos (no do tombo, para o lado do coto).
    /// </para>
    /// <para>
    /// A física só acontece onde o zumbi está perto e é desenhado; mais longe, a queda é a animação Cair. Nos níveis de
    /// longe, a fase termina na hora (deitado vira rastejando; de pé, firme).
    /// </para>
    /// </summary>
    public partial class Zumbi
    {
        /// <summary>A fase do equilíbrio (vai pela rede em 3 bits).</summary>
        public enum Fase : byte { Nenhuma, Cambaleando, Caindo, Deitado, Levantando, Rastejando, Firme }

        static readonly int LevantarId = Animator.StringToHash("Levantar");
        static readonly int LevantandoE = Animator.StringToHash("LevantandoE");
        static readonly int LevantandoD = Animator.StringToHash("LevantandoD");
        const float MisturaDoBoneco = 0.7f;   // s para a pose sair da do boneco e chegar na animação
        // os momentos fracos do levantar (a fração do clipe): os braços falham, o tombo para o lado do coto e o empurrão
        // final do chão. Uma tentativa que falha cai num deles
        static readonly float[] MomentosFracos = { 0.22f, 0.43f, 0.67f };
        const int MomentoDoTombo = 1;
        const float GrausPorPasso = 3f;      // a inclinação na rede: 5 bits com sinal, de 3 em 3 graus

        public Fase FaseDoEquilibrio { get; private set; }
        /// <summary>A inclinação de lado, em graus (+: tombando para a direita do zumbi).</summary>
        public float Inclinacao { get; private set; }
        /// <summary>A fase e a inclinação num byte, para a rede.</summary>
        internal byte EquilibrioNaRede =>
            (byte)(((int)FaseDoEquilibrio & 7) | ((Mathf.Clamp(Mathf.RoundToInt(Inclinacao / GrausPorPasso), -15, 15) + 16) << 3));

        bool Deitado => FaseDoEquilibrio == Fase.Caindo || FaseDoEquilibrio == Fase.Deitado || FaseDoEquilibrio == Fase.Levantando;

        // anfitrião: o tempo da fase, o rumo da queda e as tentativas
        float _faseDesde, _faseAte, _ladoDaQueda, _amplitude0, _frequencia, _fase0, _falhaEm, _ladoDaFalha;
        bool _vaiCair;
        int _tentativas;
        // nesta máquina: o boneco da queda, a inclinação mostrada e a volta do boneco para a animação
        CorpoDeZumbi _boneco;
        float _inclinacaoVista, _misturaAte, _tempoDoLevantar = -1f;
        bool _inclinado;
        Quaternion[] _poseDoBoneco, _poseAnimada;
        Vector3 _quadrilDoBoneco;
        Vector3 _raizPos0;
        Quaternion _raizRot0;
        bool _temRaiz0;
        int _iQuadril = -1, _iCabeca = -1;
        Transform _peE, _peD;

        // ------------------------------------------------------------------ as fases (anfitrião e cópias)

        /// <summary>As pernas pelos membros perdidos: 0 as duas, 1 sem a esquerda, 2 sem a direita, 4 sem as duas.</summary>
        int PernasPelosCortes()
        {
            bool e = Membros.SemPerna(Cortes, 0), d = Membros.SemPerna(Cortes, 1);
            return e && d ? 4 : e ? 1 : d ? 2 : 0;
        }

        /// <summary>O jeito de andar na fase: no chão (caindo, deitado, levantando) é 3 (parado, sem golpe); rastejando, 4.</summary>
        int PernasDaFase(int pelosCortes) => FaseDoEquilibrio switch
        {
            Fase.Caindo or Fase.Deitado or Fase.Levantando => 3,
            Fase.Rastejando => 4,
            _ => pelosCortes,
        };

        /// <summary>
        /// O valor de "Pernas" no Animator: deitado e levantando (5, sem transição) são o levantar, tocado pelo código
        /// (deitado, parado no primeiro quadro); rastejando, o rastejar (4).
        /// </summary>
        int PernasNoAnimador => FaseDoEquilibrio switch
        {
            Fase.Rastejando => 4,
            Fase.Deitado or Fase.Levantando => 5,
            _ => _pernas,
        };

        /// <summary>O estado do levantar pela perna que falta (sem a direita, o espelho).</summary>
        int EstadoDoLevantar => PernasPelosCortes() == 2 ? LevantandoD : LevantandoE;

        /// <summary>
        /// Com "Pernas" em 5, o Animator precisa estar no levantar (não há transição para ele): se não está (acabou de
        /// cair pela animação, saiu do rastejar, o Animator foi religado), entra, do primeiro quadro.
        /// </summary>
        void GarantirLevantar()
        {
            int estado = EstadoDoLevantar;
            if (!animador.HasState(0, estado)) return;
            if (FaseDoEquilibrio == Fase.Deitado) animador.SetFloat(LevantarId, 0f);
            if (animador.GetCurrentAnimatorStateInfo(0).shortNameHash == estado) return;
            if (animador.IsInTransition(0) && animador.GetNextAnimatorStateInfo(0).shortNameHash == estado) return;
            animador.CrossFadeInFixedTime(estado, 0.35f, 0, 0f);
        }

        /// <summary>
        /// Entra numa fase, em qualquer máquina (no anfitrião pela decisão, nas cópias pela rede). Com efeito, a queda é
        /// com física (o boneco) e o fim dela volta para a animação a partir da pose dele.
        /// </summary>
        void AplicarFase(Fase nova, bool comEfeito)
        {
            var antes = FaseDoEquilibrio;
            if (nova == antes) return;
            FaseDoEquilibrio = nova;
            if (antes == Fase.Caindo) DevolverBoneco(comEfeito);
            DefinirPernas(PernasDaFase(PernasPelosCortes()));
            if (nova == Fase.Caindo && comEfeito) ComecarBoneco();
            if (nova == Fase.Levantando) TocarLevantar();
            AtualizarColisores();
            AtualizarRenderers();
        }

        // levantando: o clipe do levantar andando (deitado, ele já estava parado no primeiro quadro), para durar o
        // tempoParaLevantar dos ajustes
        void TocarLevantar()
        {
            if (!animador.isActiveAndEnabled || _m == null) return;
            if (_tempoDoLevantar < 0f)
            {
                _tempoDoLevantar = 4.47f;
                foreach (var c in animador.runtimeAnimatorController.animationClips)
                    if (c != null && c.name == "LevantarE") { _tempoDoLevantar = c.length; break; }
            }
            animador.SetFloat(LevantarId, _tempoDoLevantar / Mathf.Max(0.5f, _m.ajustes.tempoParaLevantar));
            GarantirLevantar();
        }

        /// <summary>Nas cópias: a fase e a inclinação que chegaram pela rede.</summary>
        void ReceberEquilibrio(byte valor, bool comEfeito)
        {
            Inclinacao = (((valor >> 3) & 31) - 16) * GrausPorPasso;
            var fase = (Fase)Mathf.Min(valor & 7, (int)Fase.Firme);
            if (fase != FaseDoEquilibrio) AplicarFase(fase, comEfeito);
        }

        /// <summary>De volta ao pool: inteiro e de pé, sem boneco nem inclinação.</summary>
        void ZerarEquilibrio()
        {
            DevolverBoneco(false);
            FaseDoEquilibrio = Fase.Nenhuma;
            Inclinacao = _inclinacaoVista = 0f;
            _tentativas = 0;
            _misturaAte = 0f;
            Endireitar();
        }

        // ------------------------------------------------------------------ anfitrião: as decisões

        /// <summary>Perdeu uma perna (só uma): cai na hora ou cambaleia, tombando mais para o lado da perna que caiu.</summary>
        void ComecarEquilibrio(Vector3 direcaoDoTiro)
        {
            if (Copia || _m == null || Morto || Petrificado) return;
            var a = _m.ajustes;
            float ladoDaPerna = PernasPelosCortes() == 1 ? -1f : 1f;
            float ladoDoTiro = Vector3.Dot(direcaoDoTiro, transform.right) >= 0f ? 1f : -1f;
            float lado = Random.value < 0.75f ? ladoDaPerna : ladoDoTiro;
            if (Random.value < a.chanceDeCairNaHora) CairNoServidor(lado);
            else Cambalear(lado, Random.value < a.chanceDeCairCambaleando);
        }

        void MudarFase(Fase nova, float duracao)
        {
            float agora = Time.time;
            _faseDesde = agora;
            _faseAte = agora + duracao;
            bool parado = nova == Fase.Caindo || nova == Fase.Deitado || nova == Fase.Levantando;
            if (NoNavMesh)
            {
                if (parado) agente.ResetPath();
                agente.isStopped = parado;
            }
            AplicarFase(nova, true);
        }

        void Cambalear(float lado, bool vaiCair)
        {
            var a = _m.ajustes;
            _ladoDaQueda = lado;
            _vaiCair = vaiCair;
            _amplitude0 = Random.Range(10f, 18f);
            _frequencia = Random.Range(0.9f, 1.4f);
            _fase0 = lado > 0f ? 0f : Mathf.PI;   // o primeiro balanço vai para o lado da queda
            MudarFase(Fase.Cambaleando, Random.Range(a.tempoCambaleando.x, a.tempoCambaleando.y));
        }

        void CairNoServidor(float lado)
        {
            _ladoDaQueda = lado;
            Inclinacao = lado * _m.ajustes.inclinacaoDeQueda;   // a cópia sabe para que lado o boneco cai
            MudarFase(Fase.Caindo, _m.ajustes.tempoDaQueda);
        }

        // a queda acabou: deitado no lugar onde o boneco parou (sem boneco aqui, um pouco para o lado da queda)
        void Deitar()
        {
            if (_boneco == null)
            {
                Vector3 novo = transform.position + transform.right * (_ladoDaQueda * 0.6f);
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + 90f * _ladoDaQueda, 0f);
                Levar(novo);
            }
            Inclinacao = 0f;
            var a = _m.ajustes;
            MudarFase(Fase.Deitado, Random.Range(a.tempoDeitado.x, a.tempoDeitado.y));
        }

        void Levar(Vector3 ponto)
        {
            if (NavMesh.SamplePosition(ponto, out var hit, 1.5f, NavMesh.AllAreas)) ponto = hit.position;
            if (NoNavMesh) agente.Warp(ponto);
            else transform.position = ponto;
        }

        void Levantar()
        {
            var a = _m.ajustes;
            _tentativas++;
            float duracao = Mathf.Max(0.5f, a.tempoParaLevantar);
            _falhaEm = 0f;
            if (Random.value >= a.chanceDeLevantar)
            {
                // falha num momento fraco do clipe; no do tombo, cai para o lado do coto
                int m = Random.Range(0, MomentosFracos.Length);
                _falhaEm = Time.time + duracao * (MomentosFracos[m] + Random.Range(-0.02f, 0.03f));
                _ladoDaFalha = m == MomentoDoTombo ? (PernasPelosCortes() == 1 ? -1f : 1f) : Random.value < 0.5f ? -1f : 1f;
            }
            MudarFase(Fase.Levantando, duracao);
        }

        void Rastejar()
        {
            var a = _m.ajustes;
            MudarFase(Fase.Rastejando, Random.Range(a.rastejandoAteTentar.x, a.rastejandoAteTentar.y));
        }

        /// <summary>A cada quadro, no anfitrião (níveis Perto e Médio): o balanço, a queda e as decisões no chão.</summary>
        void EquilibrioNoServidor(float agora, float dt)
        {
            if (FaseDoEquilibrio == Fase.Nenhuma || Morto) return;
            var a = _m.ajustes;
            switch (FaseDoEquilibrio)
            {
                case Fase.Cambaleando:
                {
                    if (Petrificado) return;
                    float t = agora - _faseDesde, duracao = Mathf.Max(0.1f, _faseAte - _faseDesde), k = Mathf.Clamp01(t / duracao);
                    float limite = a.inclinacaoDeQueda;
                    // o balanço: cada vez maior até cair, ou cada vez menor até se firmar; um pouco tombado para o lado da queda
                    float amplitude = _vaiCair ? Mathf.Lerp(_amplitude0, limite * 1.05f, k * k) : _amplitude0 * Mathf.Pow(1f - k, 1.5f) + 1.5f * k;
                    float vies = _ladoDaQueda * 6f * (_vaiCair ? 1f : 1f - k);
                    Inclinacao = Mathf.Clamp(vies + amplitude * Mathf.Sin(2f * Mathf.PI * _frequencia * t + _fase0), -limite * 1.1f, limite * 1.1f);
                    // pula de lado para pôr o pé embaixo do corpo
                    if (NoNavMesh) agente.Move(transform.right * (Inclinacao / limite * 0.7f * dt));
                    if (_vaiCair && (Mathf.Abs(Inclinacao) >= limite * 0.92f && k > 0.45f || agora >= _faseAte))
                        CairNoServidor(Inclinacao >= 0f ? 1f : -1f);
                    else if (!_vaiCair && agora >= _faseAte)
                    {
                        Inclinacao = 0f;
                        MudarFase(Fase.Firme, 0f);
                    }
                    break;
                }
                case Fase.Caindo:
                    if (agora >= _faseAte) Deitar();
                    break;
                case Fase.Deitado:
                    if (Petrificado || agora < _faseAte) return;
                    if (_tentativas < a.tentativasDeLevantar && Random.value < a.chanceDeTentarLevantar) Levantar();
                    else Rastejar();
                    break;
                case Fase.Levantando:
                    if (_falhaEm > 0f && agora >= _falhaEm) CairNoServidor(_ladoDaFalha);
                    else if (agora >= _faseAte)
                        // de pé: ainda balança um pouco (às vezes cai de novo) e se firma
                        Cambalear(PernasPelosCortes() == 1 ? -1f : 1f, Random.value < a.chanceDeCairCambaleando * 0.5f);
                    break;
                case Fase.Rastejando:
                    if (Petrificado || agora < _faseAte) return;
                    if (_tentativas < a.tentativasDeLevantar && Random.value < a.chanceDeTentarLevantar) Levantar();
                    else _faseAte = agora + Random.Range(a.rastejandoAteTentar.x, a.rastejandoAteTentar.y);
                    break;
                case Fase.Firme:
                    // de pé numa perna: um balanço pequeno, maior andando
                    Inclinacao = (Velocidade > 0.2f ? 5f : 2.5f) * Mathf.Sin(agora * 2.3f + Id);
                    break;
            }
        }

        /// <summary>Um acerto que não matou (no anfitrião): de pé numa perna, pode desequilibrar; levantando, pode cair.</summary>
        void EquilibrioComTiro(Vector3 direcao)
        {
            if (Copia || _m == null || Morto || Petrificado) return;
            var a = _m.ajustes;
            float lado = Vector3.Dot(direcao, transform.right) >= 0f ? 1f : -1f;
            switch (FaseDoEquilibrio)
            {
                case Fase.Firme:
                    if (Random.value < a.chanceDeCambalearComTiro) Cambalear(lado, Random.value < a.chanceDeCairCambaleando);
                    break;
                case Fase.Cambaleando:
                    if (!_vaiCair && Random.value < 0.5f)
                    {
                        _vaiCair = true;
                        _ladoDaQueda = lado;
                    }
                    break;
                case Fase.Levantando:
                    if (Random.value < 0.6f) CairNoServidor(lado);
                    break;
            }
        }

        /// <summary>Ficou longe de todos (o zumbi virtual não tem física nem animação): a fase termina na hora.</summary>
        void EquilibrioDeLonge()
        {
            if (FaseDoEquilibrio == Fase.Nenhuma || Copia) return;
            if (Deitado) MudarFase(Fase.Rastejando, 999f);
            else if (FaseDoEquilibrio == Fase.Cambaleando) MudarFase(Fase.Firme, 0f);
            Inclinacao = 0f;
        }

        /// <summary>Perdeu as duas pernas: o desmembramento de sempre (cai e rasteja) toma o lugar do equilíbrio.</summary>
        void EncerrarEquilibrio()
        {
            if (FaseDoEquilibrio == Fase.Nenhuma) return;
            DevolverBoneco(false);
            FaseDoEquilibrio = Fase.Nenhuma;
            Inclinacao = 0f;
            if (!Copia && NoNavMesh) agente.isStopped = false;
            AtualizarColisores();
            AtualizarRenderers();
        }

        // ------------------------------------------------------------------ nesta máquina: o boneco e a inclinação

        void AcharOssos()
        {
            if (_iQuadril >= 0) return;
            _iQuadril = System.Array.IndexOf(esqueleto, partes[0]);
            _iCabeca = System.Array.IndexOf(esqueleto, partes[CorpoDeZumbi.ParteCabeca]);
            foreach (var o in esqueleto)
            {
                if (o.name == "foot.L") _peE = o;
                else if (o.name == "foot.R") _peD = o;
            }
            _peE ??= partes[8];
            _peD ??= partes[10];
        }

        // a queda: o corpo do pool cai daqui, com a pose de agora, empurrado para o lado da inclinação
        void ComecarBoneco()
        {
            if (_m == null || NivelAtual != Nivel.Perto || !_visivel || Morto || Petrificado) return;
            AcharOssos();
            // (o boneco sai inclinado como o zumbi estava; o "Modelo" endireita no próximo quadro, escondido)
            float lado = Inclinacao >= 0f ? 1f : -1f;
            Vector3 velocidade = Copia ? transform.forward * Velocidade : agente.enabled ? agente.velocity : Vector3.zero;
            _boneco = _m.CairVivo(this, velocidade, (transform.right * lado + Vector3.up * 0.1f) * 16f);
        }

        /// <summary>
        /// A queda acabou: o boneco volta ao pool e o zumbi aparece deitado (o levantar parado), virado como o boneco ficou;
        /// a pose sai da do boneco aos poucos (<see cref="DepoisDaAnimacao"/>). No anfitrião, o zumbi vai para onde o
        /// boneco parou.
        /// </summary>
        void DevolverBoneco(bool voltarParaAnimacao)
        {
            if (_boneco == null) return;
            var b = _boneco;
            _boneco = null;
            if (!voltarParaAnimacao || !animador.isActiveAndEnabled || Morto)
            {
                _m.DevolverBoneco(b);
                AtualizarRenderers();
                AtualizarColisores();
                return;
            }
            AcharOssos();
            int n = esqueleto.Length;
            if (_poseDoBoneco == null || _poseDoBoneco.Length != n)
            {
                _poseDoBoneco = new Quaternion[n];
                _poseAnimada = new Quaternion[n];
            }
            for (int i = 0; i < n && i < b.esqueleto.Length; i++) _poseDoBoneco[i] = b.esqueleto[i].rotation;
            _quadrilDoBoneco = b.esqueleto[_iQuadril].position;
            Vector3 rumo = b.esqueleto[_iCabeca].position - _quadrilDoBoneco;
            _m.DevolverBoneco(b);
            rumo.y = 0f;
            if (!Copia && rumo.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(rumo);
            // deitado já, nesta mesma chamada (a animação não espera o próximo quadro): o primeiro quadro do levantar, parado
            animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animador.SetFloat(SpeedId, 0f);
            int estado = EstadoDoLevantar;
            if (animador.HasState(0, estado))
            {
                animador.SetInteger(PernasId, 5);
                animador.SetFloat(LevantarId, 0f);
                animador.Play(estado, 0, 0f);
            }
            else
            {
                animador.SetInteger(PernasId, 4);
                animador.Play("Rastejando", 0, 0f);
            }
            animador.Update(0f);
            if (!Copia)
            {
                // o quadril da animação em cima do quadril do boneco
                Vector3 desvio = _quadrilDoBoneco - esqueleto[_iQuadril].position;
                desvio.y = 0f;
                if (desvio.sqrMagnitude < 9f) Levar(transform.position + desvio);
            }
            _misturaAte = Time.time + MisturaDoBoneco;
            AtualizarRenderers();
            AtualizarColisores();
        }

        /// <summary>Morreu durante a queda: o boneco já está caindo; ele fica como o corpo (com o rosto de morto).</summary>
        bool MorrerNoBoneco(Vector3 direcao)
        {
            if (_boneco == null) return false;
            var b = _boneco;
            _boneco = null;
            _m.MorrerNoBoneco(b, this, SemCabeca, _parteDaMorte, direcao);
            return true;
        }

        void Endireitar()
        {
            if (!_temRaiz0 || !_inclinado) return;
            raizDoModelo.SetLocalPositionAndRotation(_raizPos0, _raizRot0);
            _inclinado = false;
        }

        /// <summary>
        /// Depois da animação, a cada quadro (o ZombieManager chama, no LateUpdate): o corpo inclinado em volta do pé que
        /// sobrou (cambaleando, firme) e a pose saindo da do boneco depois da queda.
        /// </summary>
        internal void DepoisDaAnimacao(float dt)
        {
            float agora = Time.time;
            bool misturando = agora < _misturaAte;
            if (FaseDoEquilibrio == Fase.Nenhuma && !_inclinado && !misturando) return;
            bool animado = animador.enabled && _visivel && NivelAtual != Nivel.Longe && !Morto;
            // a inclinação: no anfitrião, a decidida; nas cópias, a que chega (suavizada)
            float alvo = animado && (FaseDoEquilibrio == Fase.Cambaleando || FaseDoEquilibrio == Fase.Firme) ? Inclinacao : 0f;
            _inclinacaoVista = Copia ? Mathf.Lerp(_inclinacaoVista, alvo, 1f - Mathf.Exp(-10f * dt)) : alvo;
            if (Mathf.Abs(_inclinacaoVista) > 0.05f && animado)
            {
                AcharOssos();
                if (!_temRaiz0)
                {
                    _raizPos0 = raizDoModelo.localPosition;
                    _raizRot0 = raizDoModelo.localRotation;
                    _temRaiz0 = true;
                }
                // o pivô: o pé que sobrou, no chão (sem a esquerda, apoia na direita)
                var pe = _pernas == 1 ? _peD : _peE;
                Vector3 noModelo = raizDoModelo.InverseTransformPoint(pe.position);
                Vector3 pivo = _raizPos0 + _raizRot0 * Vector3.Scale(noModelo, raizDoModelo.localScale);
                pivo.y = 0f;
                var giro = Quaternion.AngleAxis(-_inclinacaoVista, Vector3.forward);
                raizDoModelo.SetLocalPositionAndRotation(pivo + giro * (_raizPos0 - pivo), giro * _raizRot0);
                _inclinado = true;
            }
            else Endireitar();
            if (!misturando) return;
            if (_poseDoBoneco == null || !animado)
            {
                _misturaAte = 0f;
                return;
            }
            // a pose: do boneco para a animação (o quadril também na posição); o pai antes do filho no esqueleto. Só do quadril
            // para baixo: o osso "root" (acima dele) não é animado em todos os modelos, e o giro escrito nele ficava para
            // sempre (o zumbi andava torto depois da primeira queda)
            float k = 1f - (_misturaAte - agora) / MisturaDoBoneco;
            k = k * k * (3f - 2f * k);
            int n = esqueleto.Length;
            for (int i = _iQuadril; i < n; i++) _poseAnimada[i] = esqueleto[i].rotation;
            Vector3 quadrilAnimado = esqueleto[_iQuadril].position;
            for (int i = _iQuadril; i < n; i++) esqueleto[i].rotation = Quaternion.Slerp(_poseDoBoneco[i], _poseAnimada[i], k);
            esqueleto[_iQuadril].position = Vector3.Lerp(_quadrilDoBoneco, quadrilAnimado, k);
            if (agora + dt >= _misturaAte)
            {
                _misturaAte = 0f;
                animador.cullingMode = NivelAtual == Nivel.Perto ? AnimatorCullingMode.CullUpdateTransforms : AnimatorCullingMode.CullCompletely;
            }
        }
    }
}
