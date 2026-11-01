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
    /// Fúria (bate os braços para todos os lados: quem estiver perto leva o dano de cada braçada) → Cansado (o olho abre
    /// devagar) → Procurando. Acertar o olho enquanto ele procura faz ele travar em quem atirou.
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

        public FaseDoBruto FaseBruto { get; private set; }
        /// <summary>O olho: 0 fechado, 1 aberto (em todas as máquinas, pela fase).</summary>
        public float AberturaDoOlho { get; private set; }
        bool OlhoExposto => AberturaDoOlho > 0.6f;

        float _faseBrutoDesde, _olhoVendo, _proximaBracada, _percorrido, _parado, _proximoPassoDoBruto;
        Vector3 _pontoDoAvanco, _direcaoDoAvanco, _fimDoAvanco, _ultimoVistoPeloOlho;
        bool _bateuNaParede, _temUltimoVisto, _emergirVisto;
        JogadorRede _travadoEm;
        readonly System.Collections.Generic.List<JogadorRede> _atingidosNoAvanco = new System.Collections.Generic.List<JogadorRede>(4);
        // o olho e as pálpebras (no espaço do pai, na pose de repouso)
        Quaternion _olhoRepouso, _supRepouso, _infRepouso;
        Vector3 _olhoLado, _olhoCima, _olhoFrente;
        bool _temRepousoDoOlho;
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
                    if (comEfeito) _m?.RugidoDoBrutamonte(this, 1.15f);
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
                    if (agora >= _proximaBracada)
                    {
                        _proximaBracada = agora + b.intervaloDaFuria;
                        BaterEmVolta(b.raioDaFuria, b.danoDaFuria);
                    }
                    if (naFase >= b.tempoDaFuria) MudarFaseBruto(FaseDoBruto.Cansado, true);
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
        JogadorRede JogadorNoOlho(AjustesDoBrutamonte b)
        {
            if (!OlhoExposto) return null;
            Vector3 deOnde = olho != null ? olho.position : transform.position + Vector3.up * 1.6f - transform.forward * 0.3f;
            Vector3 costas = -transform.forward;
            float menor = b.alcanceDoOlho;
            JogadorRede melhor = null;
            for (int i = 0; i < _m.QuantosJogadores; i++)
            {
                ref readonly var p = ref _m.Jogadores[i];
                Vector3 para = p.peito - deOnde;
                float d = para.magnitude;
                if (d > menor) continue;
                Vector3 plano = new Vector3(para.x, 0f, para.z);
                if (plano.sqrMagnitude > 0.01f && Vector3.Angle(costas, plano) > 110f) continue;
                if (!_m.Enxerga(deOnde, p.peito)) continue;
                menor = d;
                melhor = p.jogador;
            }
            return melhor;
        }

        /// <summary>Viu: o olho fecha, e ele se prepara para avançar no ponto onde o jogador estava.</summary>
        void Travar(JogadorRede quem)
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
            for (int i = 0; i < _m.QuantosJogadores; i++)
            {
                ref readonly var p = ref _m.Jogadores[i];
                if (p.jogador == null || _atingidosNoAvanco.Contains(p.jogador)) continue;
                Vector3 d = p.pes - transform.position;
                if (Mathf.Abs(d.y) > 1.6f || new Vector2(d.x, d.z).sqrMagnitude > 1.7f * 1.7f) continue;
                _atingidosNoAvanco.Add(p.jogador);
                _m.Golpear(p.jogador, b.danoDoAvanco, transform.position);
                _m.BaqueDoBrutamonte(this);
            }
            // as barricadas no caminho caem
            Loot.ConstrucoesNoMundo.Instancia?.QuebrarBarricadasPerto(transform.position + _direcaoDoAvanco * 0.6f, 1.6f);
            if (!chegou) return;
            Velocidade = 0f;
            if (_bateuNaParede)
            {
                _m.BaqueDoBrutamonte(this);
                MudarFaseBruto(FaseDoBruto.Tonto, true);
            }
            else
            {
                _proximaBracada = agora + 0.25f;
                MudarFaseBruto(FaseDoBruto.Furia, true);
            }
            Atual = Estado.Parado;
        }

        void BaterEmVolta(float raio, float dano)
        {
            Golpes++;
            bool acertou = false;
            for (int i = 0; i < _m.QuantosJogadores; i++)
            {
                ref readonly var p = ref _m.Jogadores[i];
                Vector3 d = p.pes - transform.position;
                if (Mathf.Abs(d.y) > 1.8f || new Vector2(d.x, d.z).sqrMagnitude > raio * raio) continue;
                _m.Golpear(p.jogador, dano, transform.position);
                acertou = true;
            }
            if (acertou) _m.BaqueDoBrutamonte(this);
        }

        /// <summary>Um acerto no Brutamonte: o casco tira quase tudo; o olho aberto leva o triplo e o faz travar em quem atirou.</summary>
        void LevarTiroNoBrutamonte(RegiaoDoCorpo regiao, float dano, Vector3 ponto, Vector3 direcao, JogadorRede autor, float agora)
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
            float naFase = Time.time - _faseBrutoDesde;
            float alvo = AberturaAlvo(naFase);
            // fecha rápido, abre devagar (cansado: segue a fase)
            float vel = alvo < AberturaDoOlho ? 1f / 0.15f : FaseBruto == FaseDoBruto.Cansado ? 99f : 1f / 0.4f;
            AberturaDoOlho = Mathf.MoveTowards(AberturaDoOlho, alvo, vel * dt);
            if (hitboxDoOlho != null && hitboxDoOlho.colisor != null)
                hitboxDoOlho.colisor.enabled = NivelAtual == Nivel.Perto && OlhoExposto && !Morto;
            if (olho == null || palpebraSup == null || palpebraInf == null) return;
            if (!_temRepousoDoOlho)
            {
                // o repouso (a pose do prefab): o olho olha para trás do Brutamonte
                _olhoRepouso = olho.localRotation;
                _supRepouso = palpebraSup.localRotation;
                _infRepouso = palpebraInf.localRotation;
                var inv = Quaternion.Inverse(olho.parent.rotation);
                _olhoFrente = (inv * -transform.forward).normalized;
                _olhoCima = (inv * transform.up).normalized;
                _olhoLado = Vector3.Cross(_olhoCima, _olhoFrente).normalized;
                _temRepousoDoOlho = true;
            }
            // olhar: o jogador mais perto daqui (o local), se está atrás dele; senão, olhadas rápidas em volta
            bool segue = false;
            var local = JogadorRede.Local;
            if (local != null && AberturaDoOlho > 0.2f)
            {
                Vector3 d = local.transform.position + Vector3.up * 1.4f - olho.position;
                if (d.sqrMagnitude < 40f * 40f)
                {
                    Vector3 l = Quaternion.Inverse(olho.parent.rotation) * d;
                    float frente = Vector3.Dot(l, _olhoFrente);
                    if (frente > 0.1f)
                    {
                        _giroAlvo = Mathf.Atan2(Vector3.Dot(l, _olhoLado), frente) * Mathf.Rad2Deg;
                        _inclinacaoAlvo = Mathf.Atan2(Vector3.Dot(l, _olhoCima), frente) * Mathf.Rad2Deg;
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
            palpebraSup.localRotation = Quaternion.AngleAxis(fechar, _olhoLado) * _supRepouso;
            palpebraInf.localRotation = Quaternion.AngleAxis(-fechar, _olhoLado) * _infRepouso;
            olho.localRotation = Quaternion.AngleAxis(_giroDoOlho, _olhoCima) * Quaternion.AngleAxis(-_inclinacaoDoOlho, _olhoLado) * _olhoRepouso;
        }

        /// <summary>Quanto cada pálpebra gira para fechar (graus; o sinal acerta o sentido no modelo).</summary>
        internal static float AnguloDasPalpebras = 52f;
    }
}
