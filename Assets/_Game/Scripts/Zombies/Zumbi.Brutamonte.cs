using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O Brutamonte (01/11/2026, a pedido): o mini-chefe. Um tanque de 500 de vida, cego e surdo, que não liga para a luz nem
    /// para o sol. O casco tira o dano de tudo (×0,3); o único ponto fraco é o olho nas costas, aberto (×3, o mesmo da
    /// cabeça dos outros). Ele nasce brotando da terra (um por mundo, mais de noite: ZombieManager.Especiais.cs).
    /// <para>
    /// O ciclo do olho (as fases vão pela rede, com a vida, num byte):
    /// Procurando (o olho aberto vasculha; ele gira devagar no lugar, e anda para onde viu alguém por último) →
    /// Travando (viu o jogador mais perto por <c>tempoParaTravar</c>: o olho fecha rápido) → Preparando (vira de frente
    /// para o ponto onde viu o jogador e firma os pés: a deixa para sair da frente) → Avançando (em linha reta até aquele
    /// ponto, rápido: quem estiver no caminho leva o dano do avanço; batendo numa parede, fica Tonto, com o olho aberto) →
    /// Fúria (bate no chão com os dois punhos, três vezes: cada golpe dá dano em área, mais forte perto dele, e levanta a
    /// poeira e a terra; 05/11/2026, no lugar das braçadas no ar) → Cansado (o olho abre devagar) → Procurando. Acertar o
    /// olho enquanto ele procura faz ele travar em quem atirou.
    /// </para>
    /// </summary>
    public partial class Zumbi
    {
        /// <summary>A fase do Brutamonte (3 bits na rede).</summary>
        public enum FaseDoBruto : byte { Emergindo, Procurando, Travando, Preparando, Avancando, Furia, Cansado, Tonto }

        static readonly int EmergirId = Animator.StringToHash("Emergir");
        static readonly int FuriaId = Animator.StringToHash("Furia");
        const float TempoDeEmergir = 3.6f;
        const float TempoDeTravar = 0.25f;   // o olho fechando depois de ver

        // O golpe no chão (05/11/2026, a pedido: "Ao invés dele começar a dar uns socos no ar como está hoje, seria
        // interessante ele começar a bater no chão com os dois punhos e dar dano em área"). A animação Furia (do
        // Blender/especiais_animacoes.py, 100 quadros a 30 por segundo, sem laço) tem três impactos, nos quadros 15, 41 e 71:
        // o dano (no anfitrião) e a poeira (em todas as máquinas) saem nesses momentos, contados do começo da fase. Mudando
        // um impacto lá, mude aqui. O terceiro golpe é o maior.
        static readonly float[] GolpesDaFuria = { 15f / 30f, 41f / 30f, 71f / 30f };
        static readonly float[] ForcaDosGolpes = { 0.85f, 1f, 1.35f };
        /// <summary>A fúria inteira (a animação, até ele se levantar): depois vem o Cansado.</summary>
        const float TempoDaFuria = 100f / 30f;
        int _golpeDaFuria, _golpeMostrado;
        Transform _maoE, _maoD;
        bool _achouAsMaos, _rugiuNoTerceiro;

        public FaseDoBruto FaseBruto { get; private set; }
        /// <summary>O olho: 0 fechado, 1 aberto (em todas as máquinas, pela fase).</summary>
        public float AberturaDoOlho { get; private set; }
        bool OlhoExposto => AberturaDoOlho > 0.6f;

        float _faseBrutoDesde, _olhoVendo, _percorrido, _parado, _proximoPassoDoBruto;
        Vector3 _pontoDoAvanco, _direcaoDoAvanco, _fimDoAvanco, _ultimoVistoPeloOlho;
        bool _bateuNaParede, _temUltimoVisto, _emergirVisto;
        IAlvoDeZumbi _travadoEm;
        readonly System.Collections.Generic.List<IAlvoDeZumbi> _atingidosNoAvanco = new System.Collections.Generic.List<IAlvoDeZumbi>(4);
        // o olho e as pálpebras: o repouso e os eixos (no espaço do pai), das bindposes da malha (PoseDoOlho), uma vez
        PoseDoOlho _poseDoOlho;
        bool _temRepousoDoOlho, _avisouDoOlho;
        float _giroDoOlho, _inclinacaoDoOlho, _giroAlvo, _inclinacaoAlvo, _velGiro, _velIncl, _proximaOlhadaDoBruto;

        byte BrutamonteNaRede => (byte)(((int)FaseBruto & 7) | (Mathf.Clamp(Mathf.RoundToInt(FracaoDaVida * 31f), 0, 31) << 3));

        void ReceberBrutamonte(byte valor, bool comEfeito)
        {
            _fracaoRecebida = ((valor >> 3) & 31) / 31f;
            var f = (FaseDoBruto)(valor & 7);
            if (f != FaseBruto) MudarFaseBruto(f, comEfeito);
        }

        void PrepararBrutamonte()
        {
            agente.updateRotation = false;   // ele vira por conta própria (o olho vasculha girando o corpo)
            _temUltimoVisto = false;
            semNinguemDesde = -1f;
            MudarFaseBruto(FaseDoBruto.Emergindo, true);
        }

        void ZerarBrutamonte()
        {
            FaseBruto = FaseDoBruto.Emergindo;
            _faseBrutoDesde = Time.time;
            _acaoNoAnimador = true;   // (a cópia nasce já emergindo: a fase não muda, então a animação entra por GarantirAcaoDoBruto)
            _emergirVisto = false;
            AberturaDoOlho = 0f;
            _olhoVendo = 0f;
            _travadoEm = null;
            _atingidosNoAvanco.Clear();
            if (agente != null) agente.updateRotation = true;
        }

        void MudarFaseBruto(FaseDoBruto nova, bool comEfeito)
        {
            FaseBruto = nova;
            _faseBrutoDesde = Time.time;
            bool acao = nova == FaseDoBruto.Emergindo || nova == FaseDoBruto.Furia;
            _acaoNoAnimador = acao;
            AplicarPernasNoAnimador();
            switch (nova)
            {
                case FaseDoBruto.Emergindo:
                    Tocar(EmergirId, 0f, 0f);
                    if (comEfeito) _m?.EfeitoDeEmergir(this);
                    _emergirVisto = true;
                    break;
                case FaseDoBruto.Furia:
                    Tocar(FuriaId, 0.12f, 0f);
                    _golpeDaFuria = 0;
                    _golpeMostrado = 0;
                    _rugiuNoTerceiro = false;
                    // o rugido curto ao chegar; o urro longo fica para o terceiro golpe (GolpesNoChaoDepoisDaAnimacao)
                    if (comEfeito) _m?.RugidoDoBrutamonte(this, 0.9f);
                    break;
                case FaseDoBruto.Travando:
                    if (comEfeito) _m?.RugidoDoBrutamonte(this, 0.9f);
                    break;
                case FaseDoBruto.Tonto:
                    if (comEfeito) { MostrarReacao(); _m?.BaqueDoBrutamonte(this); }
                    break;
            }
        }

        // ------------------------------------------------------------------ anfitrião: a cada decisão e a cada quadro

        /// <summary>A decisão do Brutamonte (no lugar da do Oco): só anda para onde o olho viu alguém por último.</summary>
        void PensarBrutamonte(float agora)
        {
            if (FaseBruto != FaseDoBruto.Procurando || !NoNavMesh) return;
            if (_temUltimoVisto && (_ultimoVistoPeloOlho - transform.position).sqrMagnitude > 9f)
            {
                if (agora >= _proximoPassoDoBruto)
                {
                    _proximoPassoDoBruto = agora + 1f;
                    Ir(_ultimoVistoPeloOlho, A.brutamonte.velocidade, 1.5f);
                    Atual = Estado.Investigando;
                }
            }
            else if (agente.hasPath && Chegou())
            {
                agente.ResetPath();
                Atual = Estado.Parado;
            }
        }

        void BrutamonteNoQuadro(float agora, float dt)
        {
            var b = A.brutamonte;
            float naFase = agora - _faseBrutoDesde;
            switch (FaseBruto)
            {
                case FaseDoBruto.Emergindo:
                    PararAgente();
                    if (naFase >= TempoDeEmergir) MudarFaseBruto(FaseDoBruto.Procurando, true);
                    break;
                case FaseDoBruto.Procurando:
                {
                    // o corpo gira devagar (o olho nas costas varre em volta); andando, vira para onde anda
                    if (agente.enabled && agente.hasPath && agente.velocity.sqrMagnitude > 0.05f) VirarPara(transform.position + agente.velocity, 120f, dt);
                    else transform.Rotate(0f, 22f * dt, 0f);
                    var visto = JogadorNoOlho(b);
                    if (visto != null)
                    {
                        _olhoVendo += dt;
                        _ultimoVistoPeloOlho = visto.transform.position;
                        _temUltimoVisto = true;
                        if (_olhoVendo >= b.tempoParaTravar) Travar(visto);
                    }
                    else _olhoVendo = Mathf.Max(0f, _olhoVendo - dt * 0.5f);
                    break;
                }
                case FaseDoBruto.Travando:
                    PararAgente();
                    if (naFase >= TempoDeTravar) MudarFaseBruto(FaseDoBruto.Preparando, true);
                    break;
                case FaseDoBruto.Preparando:
                    PararAgente();
                    VirarPara(_pontoDoAvanco, 540f, dt);
                    if (naFase >= b.preparo) ComecarAvanco();
                    break;
                case FaseDoBruto.Avancando:
                    Avancar(agora, dt, b);
                    break;
                case FaseDoBruto.Furia:
                    PararAgente();
                    // os golpes no chão, nos impactos da animação
                    while (_golpeDaFuria < GolpesDaFuria.Length && naFase >= GolpesDaFuria[_golpeDaFuria])
                    {
                        GolpeNoChao(ForcaDosGolpes[_golpeDaFuria], b);
                        _golpeDaFuria++;
                    }
                    if (naFase >= TempoDaFuria) MudarFaseBruto(FaseDoBruto.Cansado, true);
                    break;
                case FaseDoBruto.Cansado:
                    PararAgente();
                    if (naFase >= b.cansado) MudarFaseBruto(FaseDoBruto.Procurando, true);
                    break;
                case FaseDoBruto.Tonto:
                    PararAgente();
                    if (naFase >= b.tonto) MudarFaseBruto(FaseDoBruto.Procurando, true);
                    break;
            }
        }

        void PararAgente()
        {
            if (NoNavMesh && agente.hasPath) agente.ResetPath();
            if (Atual != Estado.Parado && FaseBruto != FaseDoBruto.Avancando) Atual = Estado.Parado;
        }

        void VirarPara(Vector3 ponto, float grausPorSegundo, float dt)
        {
            Vector3 d = ponto - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), grausPorSegundo * dt);
        }

        /// <summary>
        /// O jogador mais perto que o olho vê: até o alcance, com a linha livre do olho até o peito, e do lado das costas (o
        /// olho gira até ~110° de cada lado da nuca).
        /// </summary>
        IAlvoDeZumbi JogadorNoOlho(AjustesDoBrutamonte b)
        {
            if (!OlhoExposto) return null;
            Vector3 deOnde = olho != null ? olho.position : transform.position + Vector3.up * 1.6f - transform.forward * 0.3f;
            Vector3 costas = -transform.forward;
            float menor = b.alcanceDoOlho;
            IAlvoDeZumbi melhor = null;
            for (int i = 0; i < _m.QuantosAlvos; i++)   // os jogadores e os sobreviventes (02/11/2026)
            {
                ref readonly var p = ref _m.AlvoNo(i);
                Vector3 para = p.peito - deOnde;
                float d = para.magnitude;
                if (d > menor) continue;
                Vector3 plano = new Vector3(para.x, 0f, para.z);
                if (plano.sqrMagnitude > 0.01f && Vector3.Angle(costas, plano) > 110f) continue;
                if (!_m.Enxerga(deOnde, p.peito)) continue;
                menor = d;
                melhor = p.quem;
            }
            return melhor;
        }

        /// <summary>
        /// F1 > "Fúria do Brutamonte" (05/11/2026; no anfitrião): a fúria agora, onde ele está, virado para quem pediu (para
        /// testar o golpe no chão e para gravar os vídeos, sem esperar o olho travar). Emergindo, não.
        /// </summary>
        internal void FuriaAgora(Vector3 virarPara)
        {
            if (tipo != TipoDeZumbi.Brutamonte || Morto || FaseBruto == FaseDoBruto.Emergindo) return;
            Vector3 d = virarPara - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
            Velocidade = 0f;
            _engajadoAte = Time.time + 20f;
            MudarFaseBruto(FaseDoBruto.Furia, true);
            PararAgente();
        }

        /// <summary>Viu: o olho fecha, e ele se prepara para avançar no ponto onde o jogador estava.</summary>
        void Travar(IAlvoDeZumbi quem)
        {
            if (quem == null) return;
            _travadoEm = quem;
            _pontoDoAvanco = quem.transform.position;
            _ultimoVistoPeloOlho = _pontoDoAvanco;
            _temUltimoVisto = true;
            _olhoVendo = 0f;
            _engajadoAte = Time.time + 20f;
            MudarFaseBruto(FaseDoBruto.Travando, true);
        }

        void ComecarAvanco()
        {
            Vector3 d = _pontoDoAvanco - transform.position;
            d.y = 0f;
            float dist = Mathf.Min(d.magnitude + 1.5f, A.brutamonte.avancoMaximo);   // passa um pouco do ponto
            _direcaoDoAvanco = d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
            Vector3 fim = transform.position + _direcaoDoAvanco * dist;
            _bateuNaParede = false;
            // a linha reta pelo NavMesh: se ela bate numa borda (uma parede), ele vai até lá e fica tonto. A linha sai
            // do ponto do agente (no NavMesh); o transform pode estar um pouco acima, e aí o raio "bate" logo no começo.
            // Uma batida colada nele fica para o "empacou" do avanço, que mede de verdade.
            Vector3 inicio = NoNavMesh ? agente.nextPosition : transform.position;
            if (NoNavMesh && NavMesh.Raycast(inicio, fim, out var hit, agente.areaMask) && hit.distance > 0.3f)
            {
                fim = hit.position;
                _bateuNaParede = hit.distance < dist - 0.6f;
            }
            _fimDoAvanco = fim;
            _percorrido = 0f;
            _parado = 0f;
            _atingidosNoAvanco.Clear();
            transform.rotation = Quaternion.LookRotation(_direcaoDoAvanco);
            Atual = Estado.Perseguindo;
            MudarFaseBruto(FaseDoBruto.Avancando, true);
        }

        void Avancar(float agora, float dt, AjustesDoBrutamonte b)
        {
            Vector3 falta = _fimDoAvanco - transform.position;
            falta.y = 0f;
            float passo = b.velocidadeDoAvanco * dt;
            bool chegou = falta.magnitude <= passo + 0.05f || _percorrido >= b.avancoMaximo;
            if (!chegou && NoNavMesh)
            {
                // (o transform só acompanha o agente depois; o nextPosition muda na hora)
                Vector3 antes = agente.nextPosition;
                agente.Move(_direcaoDoAvanco * passo);
                float andou = (agente.nextPosition - antes).magnitude;
                _percorrido += andou;
                // empacou (um carro, outro zumbi grande): conta como a parede
                _parado = andou < passo * 0.25f ? _parado + dt : 0f;
                if (_parado > 0.25f)
                {
                    _bateuNaParede = true;
                    chegou = true;
                }
            }
            Velocidade = b.velocidadeDoAvanco;
            // quem está no caminho
            for (int i = 0; i < _m.QuantosAlvos; i++)
            {
                ref readonly var p = ref _m.AlvoNo(i);
                if (p.quem == null || _atingidosNoAvanco.Contains(p.quem)) continue;
                Vector3 d = p.pes - transform.position;
                if (Mathf.Abs(d.y) > 1.6f || new Vector2(d.x, d.z).sqrMagnitude > 1.7f * 1.7f) continue;
                _atingidosNoAvanco.Add(p.quem);
                _m.Golpear(p.quem, b.danoDoAvanco, transform.position);
                _m.BaqueDoBrutamonte(this);
            }
            // as barricadas no caminho caem
            Loot.ConstrucoesNoMundo.Instancia?.QuebrarBarricadasPerto(transform.position + _direcaoDoAvanco * 0.6f, 1.6f, this);   // (e as estacas, 11/11/2026: ele leva o dano delas)
            if (!chegou) return;
            Velocidade = 0f;
            if (_bateuNaParede)
            {
                _m.BaqueDoBrutamonte(this);
                MudarFaseBruto(FaseDoBruto.Tonto, true);
            }
            else MudarFaseBruto(FaseDoBruto.Furia, true);
            Atual = Estado.Parado;
        }

        /// <summary>
        /// Um golpe no chão (no anfitrião): o dano em área em volta dos punhos, inteiro até <c>raioCheioDoGolpe</c> e caindo
        /// até um quarto na borda (<c>raioDoGolpeNoChao</c>), vezes a força do golpe. Quem está bem mais alto ou mais baixo
        /// (um telhado, um andar) não sente, e uma parede no meio segura a onda. As barricadas em volta dos punhos caem.
        /// </summary>
        void GolpeNoChao(float forca, AjustesDoBrutamonte b)
        {
            Golpes++;
            Vector3 ponto = PontoDoGolpe();
            float cheio = Mathf.Max(0f, b.raioCheioDoGolpe), raio = Mathf.Max(cheio + 0.01f, b.raioDoGolpeNoChao);
            for (int i = 0; i < _m.QuantosAlvos; i++)
            {
                ref readonly var p = ref _m.AlvoNo(i);
                if (p.quem == null) continue;
                Vector3 d = p.pes - ponto;
                if (Mathf.Abs(d.y) > 1.6f) continue;
                float dist = new Vector2(d.x, d.z).magnitude;
                if (dist > raio || !_m.Enxerga(ponto + Vector3.up * 0.4f, p.peito)) continue;
                float k = dist <= cheio ? 1f : Mathf.Lerp(1f, 0.25f, (dist - cheio) / (raio - cheio));
                _m.Golpear(p.quem, b.danoDoGolpeNoChao * forca * k, ponto);
            }
            Loot.ConstrucoesNoMundo.Instancia?.QuebrarBarricadasPerto(ponto, 1.4f + 0.6f * forca, this);
        }

        /// <summary>Onde os punhos batem: no chão, entre as duas mãos (ou na frente dele, sem os ossos das mãos).</summary>
        Vector3 PontoDoGolpe()
        {
            if (!_achouAsMaos)
            {
                _achouAsMaos = true;
                if (esqueleto != null)
                    foreach (var o in esqueleto)
                    {
                        if (o == null) continue;
                        if (o.name == "hand.L") _maoE = o;
                        else if (o.name == "hand.R") _maoD = o;
                    }
            }
            Vector3 p = _maoE != null && _maoD != null ? (_maoE.position + _maoD.position) * 0.5f : transform.position + transform.forward * 0.9f;
            p.y = transform.position.y;
            return p;
        }

        /// <summary>
        /// Em todas as máquinas, depois da animação: a poeira, o estrondo e o tremor de cada golpe no chão, na hora do impacto
        /// (contado do começo da fase, como o dano no anfitrião). Um impacto que passou há mais de 0,3 s (a cópia chegou
        /// atrasada, um quadro muito longo) não aparece.
        /// </summary>
        void GolpesNoChaoDepoisDaAnimacao()
        {
            if (FaseBruto != FaseDoBruto.Furia) return;
            float naFase = Time.time - _faseBrutoDesde;
            while (_golpeMostrado < GolpesDaFuria.Length && naFase >= GolpesDaFuria[_golpeMostrado])
            {
                if (naFase - GolpesDaFuria[_golpeMostrado] < 0.3f)
                    _m?.EfeitoDoGolpeNoChao(this, PontoDoGolpe(), ForcaDosGolpes[_golpeMostrado]);
                _golpeMostrado++;
            }
            // o rugido lá no alto, antes do terceiro golpe (a cabeça para trás na animação, quadros 60 a 67)
            if (_golpeMostrado == 2 && !_rugiuNoTerceiro && naFase >= 59f / 30f)
            {
                _rugiuNoTerceiro = true;
                _m?.RugidoDoBrutamonte(this, 1.15f);
            }
        }

        /// <summary>Um acerto no Brutamonte: o casco tira quase tudo; o olho aberto leva o triplo e o faz travar em quem atirou.</summary>
        void LevarTiroNoBrutamonte(RegiaoDoCorpo regiao, float dano, Vector3 ponto, Vector3 direcao, IAlvoDeZumbi autor, float agora)
        {
            var b = A.brutamonte;
            bool noOlho = regiao == RegiaoDoCorpo.Olho && OlhoExposto;
            _danoNoCorpo += dano * (noOlho ? b.danoNoOlho : b.danoNoCasco);
            if (_danoNoCorpo >= b.vida)
            {
                Morrer(agora, false, ponto, direcao, false);
                return;
            }
            _engajadoAte = agora + 20f;
            if (!noOlho) return;
            Reacoes++;
            MostrarReacao();
            if (autor != null && (FaseBruto == FaseDoBruto.Procurando || FaseBruto == FaseDoBruto.Cansado || FaseBruto == FaseDoBruto.Tonto))
                Travar(autor);
        }

        // ------------------------------------------------------------------ em todas as máquinas: o olho

        float AberturaAlvo(float naFase) => FaseBruto switch
        {
            FaseDoBruto.Procurando or FaseDoBruto.Tonto => 1f,
            FaseDoBruto.Cansado => Mathf.Clamp01(naFase / Mathf.Max(0.2f, A != null ? A.brutamonte.cansado : 2f)),
            FaseDoBruto.Emergindo => naFase > TempoDeEmergir - 0.6f ? 1f : 0f,
            _ => 0f,
        };

        /// <summary>
        /// Emergindo e na fúria, o Animator precisa estar no estado da ação (não há transição para ele): se não está (o
        /// Animator acabou de ser ligado, a cópia nasceu já emergindo), entra, no ponto em que a fase está.
        /// </summary>
        void GarantirAcaoDoBruto()
        {
            int estado = FaseBruto == FaseDoBruto.Emergindo ? EmergirId : FaseBruto == FaseDoBruto.Furia ? FuriaId : 0;
            if (estado == 0 || !animador.isActiveAndEnabled) return;
            float naFase = Time.time - _faseBrutoDesde;
            if (!_emergirVisto && FaseBruto == FaseDoBruto.Emergindo)
            {
                _emergirVisto = true;
                if (naFase < 1f) _m?.EfeitoDeEmergir(this);
            }
            if (!_acaoNoAnimador)
            {
                _acaoNoAnimador = true;
                AplicarPernasNoAnimador();
            }
            if (animador.IsInTransition(0)) return;
            if (animador.GetCurrentAnimatorStateInfo(0).shortNameHash == estado) return;
            Tocar(estado, 0f, FaseBruto == FaseDoBruto.Emergindo ? Mathf.Min(naFase, TempoDeEmergir - 0.05f) : 0f);
        }

        void OlhoDepoisDaAnimacao(float dt)
        {
            GarantirAcaoDoBruto();
            GolpesNoChaoDepoisDaAnimacao();
            float naFase = Time.time - _faseBrutoDesde;
            float alvo = AberturaAlvo(naFase);
            // fecha rápido, abre devagar (cansado: segue a fase)
            float vel = alvo < AberturaDoOlho ? 1f / 0.15f : FaseBruto == FaseDoBruto.Cansado ? 99f : 1f / 0.4f;
            AberturaDoOlho = Mathf.MoveTowards(AberturaDoOlho, alvo, vel * dt);
            if (hitboxDoOlho != null && hitboxDoOlho.colisor != null)
                hitboxDoOlho.colisor.enabled = NivelAtual == Nivel.Perto && OlhoExposto && !Morto;
            if (olho == null || palpebraSup == null || palpebraInf == null)
            {
                if (!_avisouDoOlho)
                {
                    _avisouDoOlho = true;
                    Debug.LogError($"{name}: o Brutamonte está sem os ossos do olho (olho, palpebra_sup, palpebra_inf); monte o prefab de novo.", this);
                }
                return;
            }
            if (!_temRepousoDoOlho)
            {
                // o repouso (a bindpose: o olho aberto olhando para trás do Brutamonte), não a pose deste quadro, que já
                // vem animada (o peito curvado) e, com o Animator parado num quadro, com a pálpebra do quadro anterior
                _temRepousoDoOlho = true;
                PoseDoOlho.Calcular(transform, PoseDoOlho.MalhaDe(renderers, olho), olho, palpebraSup, palpebraInf, out _poseDoOlho);
                if (!_poseDoOlho.dasBindposes && !_avisouDoOlho)
                {
                    _avisouDoOlho = true;
                    Debug.LogWarning($"{name}: os ossos do olho não estão nas bindposes da malha; o repouso do olho saiu da pose atual.", this);
                }
            }
            ref readonly var pose = ref _poseDoOlho;
            // olhar: o jogador mais perto daqui (o local), se está atrás dele; senão, olhadas rápidas em volta
            bool segue = false;
            var local = JogadorRede.Local;
            if (local != null && AberturaDoOlho > 0.2f)
            {
                Vector3 d = local.transform.position + Vector3.up * 1.4f - olho.position;
                if (d.sqrMagnitude < 40f * 40f)
                {
                    Vector3 l = Quaternion.Inverse(olho.parent.rotation) * d;
                    float frente = Vector3.Dot(l, pose.frente);
                    if (frente > 0.1f)
                    {
                        _giroAlvo = Mathf.Atan2(Vector3.Dot(l, pose.lado), frente) * Mathf.Rad2Deg;
                        _inclinacaoAlvo = Mathf.Atan2(Vector3.Dot(l, pose.cima), frente) * Mathf.Rad2Deg;
                        segue = true;
                    }
                }
            }
            if (!segue && Time.time >= _proximaOlhadaDoBruto)
            {
                _giroAlvo = Random.Range(-30f, 30f);
                _inclinacaoAlvo = Random.Range(-14f, 14f);
                _proximaOlhadaDoBruto = Time.time + Random.Range(0.35f, 1.1f);
            }
            _giroAlvo = Mathf.Clamp(_giroAlvo, -35f, 35f);
            _inclinacaoAlvo = Mathf.Clamp(_inclinacaoAlvo, -20f, 20f);
            float t = segue ? 0.1f : 0.05f;
            _giroDoOlho = Mathf.SmoothDamp(_giroDoOlho, _giroAlvo, ref _velGiro, t, Mathf.Infinity, dt);
            _inclinacaoDoOlho = Mathf.SmoothDamp(_inclinacaoDoOlho, _inclinacaoAlvo, ref _velIncl, t, Mathf.Infinity, dt);
            float fechar = AnguloDasPalpebras * (1f - AberturaDoOlho * AberturaDoOlho * (3f - 2f * AberturaDoOlho));
            pose.Aplicar(olho, palpebraSup, palpebraInf, fechar, _giroDoOlho, _inclinacaoDoOlho);
        }

        /// <summary>
        /// Quanto cada pálpebra gira para fechar (graus; o sinal acerta o sentido no modelo). As bordas das pálpebras do
        /// Brutamonte se encontram a ~36° (04/11/2026; com os 52° de antes, uma entrava na outra).
        /// </summary>
        internal static float AnguloDasPalpebras = 38f;
    }
}
