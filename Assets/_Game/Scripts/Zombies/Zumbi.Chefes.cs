using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os chefes (15/11/2026, a pedido: "IA e habilidades"): o tipo <see cref="TipoDeZumbi.Chefe"/>, um por
    /// <see cref="QualChefe"/>. São zumbis como os outros (o pool, a rede, as hitboxes, o tiro), mas com o cérebro deles:
    /// <list type="bullet">
    /// <item>o Blindado (~8 m): agarra com a mão enorme, aperta duas vezes e arremessa; bate no chão com os dois punhos;
    /// firma os pés e dá a investida (batendo numa parede, fica tonto); levando muito dano de uma vez, agacha e absorve (o
    /// casco de calos tira quase tudo, menos na cabeça). Ao morrer, rasga-se nos Agregados A e B;</item>
    /// <item>os Agregados (~3,5 m): a mão de carne estica, agarra de longe e puxa a vítima até ele; de perto, o golpe do
    /// outro braço;</item>
    /// <item>o Alongado (~9,4 m): o braço comprido alcança longe, agarra e, segurando, arremessa por cima da cabeça; de
    /// perto, varre o chão. Ao morrer, o que sobra dele é o Longo;</item>
    /// <item>o Longo (agachado, ~2,8 m): rápido, cospe o ácido (a gosma do Long, que prende quem leva em cheio) e dá o bote;
    /// o papo é o ponto fraco;</item>
    /// <item>o Condutor (~8 m): o grito chama a horda (os de perto vêm atrás do alvo dele, chegam outros de fora da vista e
    /// os que estão por perto entram em frenesi, mais rápidos); a clava de corpos varre; com metade da vida (e de novo com
    /// um quinto), o frenesi dele: mais rápido e com os golpes mais seguidos. O rosto no peito é o ponto fraco.</item>
    /// </list>
    /// A ação de agora vai pela rede no byte do especial (5 bits, com um contador de 3 para a mesma ação seguida), a vida no
    /// byte da pedra (o chefe não vira pedra) e o frenesi no do equilíbrio (ele não manca). Cada máquina toca a animação da
    /// ação (o estado do controlador do chefe, com o nome do clipe) e os efeitos nos tempos do clipe (do Blender,
    /// chefe_anim.py: mudando um tempo lá, mude aqui); o dano, o agarrão e o arremesso são só do anfitrião. Quem é agarrado
    /// vai com a mão do chefe na máquina dele (MovimentoPrimeiraPessoa.Chefes.cs) até o chefe soltar ou arremessar.
    /// </summary>
    public partial class Zumbi
    {
        /// <summary>A ação do chefe (5 bits na rede).</summary>
        public enum AcaoDoChefe : byte
        {
            Nenhuma, Surgir, Golpe, Agarrar, Preparo, Investida, Tonto, Absorver, Esticar, Alcancar, Arremessar, Varrer, Cuspir, Bote,
            Grito, Varredura, Frenesi,
        }
        const int QuantasAcoes = 17;

        [Header("Chefe (a montagem preenche: Chefes > Montar os chefes no jogo)")]
        [Tooltip("Qual dos chefes (o tipo é Chefe).")]
        public QualChefe qualChefe;
        [Tooltip("A altura dele em pé (m): o acerto tolerado, o olho e o afundar do corpo.")]
        public float alturaDoChefe = 8f;
        [Tooltip("A mão que agarra (o Blindado, os Agregados, o Alongado): quem é agarrado vai com ela.")]
        public Transform maoQueAgarra;
        [Tooltip("A mão (ou a clava) dos golpes; a outra é a segunda mão do golpe no chão do Blindado.")]
        public Transform maoDoGolpe, outraMao;
        [Tooltip("A boca (o ácido do Longo) e a cabeça (o bote do Longo).")]
        public Transform bocaDoChefe, cabecaDoChefe;

        public bool EhChefe => tipo == TipoDeZumbi.Chefe;
        /// <summary>O que o chefe está fazendo agora (em todas as máquinas).</summary>
        public AcaoDoChefe AcaoAtual { get; private set; }
        /// <summary>O frenesi do Condutor (depois de perder metade da vida): mais rápido e com os golpes mais seguidos.</summary>
        public bool Enfurecido { get; private set; }
        /// <summary>O Blindado agachado com os braços na frente: o tiro tira quase nada (e faz faísca, ArmaJogador.DePedra).</summary>
        public bool Absorvendo => EhChefe && AcaoAtual == AcaoDoChefe.Absorver && Time.time - _acaoDesde > 0.2f && Time.time - _acaoDesde < 2.35f;
        /// <summary>Saindo (o Agregado do Blindado, o Longo do Alongado, o que chegou): a mente de quem está perto sente.</summary>
        internal bool ChefeSurgindo => EhChefe && !Morto && AcaoAtual == AcaoDoChefe.Surgir;

        /// <summary>O nome dele (a barra do chefe, o XP, a tela de morte).</summary>
        public string NomeDoChefe => qualChefe switch
        {
            QualChefe.Blindado => "O Blindado",
            QualChefe.AgregadoA or QualChefe.AgregadoB => "O Agregado",
            QualChefe.Alongado => "O Alongado",
            QualChefe.Longo => "O Longo",
            _ => "O Condutor",
        };

        internal int XpDoChefe => qualChefe switch
        {
            QualChefe.Blindado => 40, QualChefe.AgregadoA or QualChefe.AgregadoB => 15, QualChefe.Alongado => 40, QualChefe.Longo => 20, _ => 50,
        };

        static readonly int VelocidadeDoChefeId = Animator.StringToHash("Velocidade");
        static readonly int InvestidaId = Animator.StringToHash("Investida");
        static readonly int FrenesiId = Animator.StringToHash("Frenesi");
        static readonly int EstadoDanoId = Animator.StringToHash("Dano");
        static readonly int EstadoMorteId = Animator.StringToHash("Morte");
        static readonly int EstadoDividirId = Animator.StringToHash("Dividir");
        // o estado do controlador de cada ação (o nome do clipe); 0: nenhum (o Parado e o Andar, pela Velocidade)
        static readonly int[] EstadoDaAcao = MontarEstadosDasAcoes();

        static int[] MontarEstadosDasAcoes()
        {
            var e = new int[QuantasAcoes];
            for (int i = 0; i < QuantasAcoes; i++)
            {
                var a = (AcaoDoChefe)i;
                e[i] = a switch
                {
                    AcaoDoChefe.Nenhuma or AcaoDoChefe.Preparo => 0,
                    AcaoDoChefe.Surgir or AcaoDoChefe.Tonto => Animator.StringToHash("Dano"),
                    _ => Animator.StringToHash(a.ToString()),
                };
            }
            return e;
        }

        float _acaoDesde, _proximaAcao, _proximoAlvo, _ultimoVistoChefe, _proximoCaminhoChefe, _reacaoChefeAte, _paradoAte;
        float _proximoPasseio, _interesseAte, _duracaoDoTonto, _janelaDeDano, _janelaDesde, _danoSegurando, _boteAndado;
        float _altoDesde = -1f, _distanciaDoAlto, _recuandoAte, _ultimoGritoEm = -999f;
        // o alvo que ele não alcança (no alto: um telhado, uma sacada): o relógio de quanto tempo ele está empacado embaixo, e
        // o que ele deixou de lado por um tempo (Recuar)
        IAlvoDeZumbi _alvoDoAlto;
        readonly Dictionary<IAlvoDeZumbi, float> _ignorados = new Dictionary<IAlvoDeZumbi, float>();
        float _morteVistaEm = -1f;
        readonly float[] _prontoEm = new float[QuantasAcoes];
        byte _contadorDaAcao;
        int _eventosFeitos, _efeitosFeitos, _limiaresDoFrenesi;
        bool _frenesiPendente, _temInteresse, _temMaoAntes, _afundou, _temRaizDoChefe;
        IAlvoDeZumbi _alvoDoChefe, _ultimoAutor;
        JogadorRede _segurado;
        Vector3 _interesse, _maoAntes, _raizDoChefe0, _direcaoDoBote;
        readonly List<IAlvoDeZumbi> _atingidosPeloChefe = new List<IAlvoDeZumbi>(4);

        bool AlvoDoChefeValido => _alvoDoChefe != null && _alvoDoChefe.AlvoValido;
        Vector3 OlhoDoChefe => transform.position + Vector3.up * Mathf.Max(1.4f, alturaDoChefe * 0.8f);
        /// <summary>O jogador que ele segura agora (no anfitrião).</summary>
        internal JogadorRede SeguradoPeloChefe => _segurado;

        AjustesDosChefes C => A.chefes;

        float VidaDoChefe => qualChefe switch
        {
            QualChefe.Blindado => C.vidaDoBlindado,
            QualChefe.AgregadoA or QualChefe.AgregadoB => C.vidaDoAgregado,
            QualChefe.Alongado => C.vidaDoAlongado,
            QualChefe.Longo => C.vidaDoLongo,
            _ => C.vidaDoCondutor,
        };

        float VelocidadeBaseDoChefe => qualChefe switch
        {
            QualChefe.Blindado => C.velocidadeDoBlindado,
            QualChefe.AgregadoA or QualChefe.AgregadoB => C.velocidadeDoAgregado,
            QualChefe.Alongado => C.velocidadeDoAlongado,
            QualChefe.Longo => C.velocidadeDoLongo,
            _ => C.velocidadeDoCondutor,
        };

        float VelocidadeDoChefe => VelocidadeBaseDoChefe * (Enfurecido ? 1.4f : 1f);

        // a distância (horizontal, do centro dele) em que ele para de andar e fica de frente
        float ParadaDoChefe => qualChefe switch
        {
            QualChefe.Blindado => 3.6f, QualChefe.AgregadoA or QualChefe.AgregadoB => 2.2f, QualChefe.Alongado => 4.5f, QualChefe.Longo => 1.6f, _ => 4f,
        };

        float RaioDoAgenteDoChefe => qualChefe switch
        {
            QualChefe.Blindado => 1.2f, QualChefe.AgregadoA or QualChefe.AgregadoB => 0.7f, QualChefe.Alongado => 1f, QualChefe.Longo => 0.8f, _ => 1.3f,
        };

        float GiroDoChefe => qualChefe == QualChefe.Longo ? 300f : qualChefe == QualChefe.AgregadoA || qualChefe == QualChefe.AgregadoB ? 200f : 110f;

        // ------------------------------------------------------------------ os tempos dos clipes (chefe_anim.py)

        /// <summary>Quanto dura a ação (s): o clipe inteiro (o anfitrião espera o fim antes de andar de novo).</summary>
        float DuracaoDaAcao(AcaoDoChefe a) => a switch
        {
            AcaoDoChefe.Surgir => 1.6f,
            AcaoDoChefe.Golpe => qualChefe == QualChefe.Blindado ? 2.4f : 1.6f,
            AcaoDoChefe.Agarrar => 3f,
            AcaoDoChefe.Preparo => 0.75f,
            AcaoDoChefe.Investida => 5f,
            AcaoDoChefe.Tonto => _duracaoDoTonto > 0f ? _duracaoDoTonto : 2f,
            AcaoDoChefe.Absorver => 2.6f,
            AcaoDoChefe.Esticar => 2.6f,
            AcaoDoChefe.Alcancar => 2.2f,
            AcaoDoChefe.Arremessar => 2.3f,
            AcaoDoChefe.Varrer => 2.2f,
            AcaoDoChefe.Cuspir => 1.9f,
            AcaoDoChefe.Bote => 1.9f,
            AcaoDoChefe.Grito => 3f,
            AcaoDoChefe.Varredura => 2.3f,
            AcaoDoChefe.Frenesi => 3.5f,
            _ => 0f,
        };

        /// <summary>O clipe de cada ação (o Dano tem 0,8 s; os loops, a ação inteira): para entrar nele de novo, se preciso.</summary>
        float DuracaoDoClipe(AcaoDoChefe a) => a == AcaoDoChefe.Surgir || a == AcaoDoChefe.Tonto ? 0.8f : DuracaoDaAcao(a);

        /// <summary>Até quando ele ainda vira para o alvo durante a ação (o momento do golpe; depois, vai reto).</summary>
        float ViraAte(AcaoDoChefe a) => a switch
        {
            AcaoDoChefe.Golpe => qualChefe == QualChefe.Blindado ? 0.9f : 0.5f,
            AcaoDoChefe.Agarrar => 0.85f,
            AcaoDoChefe.Preparo => 0.75f,
            AcaoDoChefe.Esticar => 0.6f,
            AcaoDoChefe.Alcancar => 0.7f,
            AcaoDoChefe.Arremessar => 0.5f,
            AcaoDoChefe.Varrer => 0.45f,
            AcaoDoChefe.Cuspir => 0.8f,
            AcaoDoChefe.Bote => 0.5f,
            AcaoDoChefe.Grito => 0.6f,
            AcaoDoChefe.Varredura => 0.5f,
            _ => 0f,
        };

        float DuracaoDaMorte => qualChefe switch
        {
            QualChefe.Blindado => 3.3f, QualChefe.Alongado or QualChefe.Condutor => 3f, _ => 2.6f,
        };

        // ------------------------------------------------------------------ a rede

        byte ChefeNaRede => (byte)(((int)AcaoAtual & 31) | ((_contadorDaAcao & 7) << 5));
        /// <summary>O byte da pedra: no chefe, a vida (0 a 255; ele não vira pedra).</summary>
        internal byte PedraNaRede => EhChefe ? (byte)Mathf.Clamp(Mathf.RoundToInt(FracaoDaVida * 255f), 0, 255) : (byte)Mathf.RoundToInt(Pedra * 255f);
        /// <summary>O byte do equilíbrio: no chefe, o frenesi (ele não manca).</summary>
        internal byte ExtraNaRede => EhChefe ? (byte)(Enfurecido ? 1 : 0) : EquilibrioNaRede;

        void ReceberChefe(byte valor, bool comEfeito)
        {
            int a = valor & 31;
            byte n = (byte)((valor >> 5) & 7);
            if (a >= QuantasAcoes) return;
            if ((AcaoDoChefe)a == AcaoAtual && n == _contadorDaAcao) return;
            _contadorDaAcao = n;
            MudarAcaoDoChefe((AcaoDoChefe)a, comEfeito);
        }

        void ReceberExtraDoChefe(byte extra) => Enfurecido = (extra & 1) != 0;

        // ------------------------------------------------------------------ preparar, zerar e a ação (em todas as máquinas)

        void PrepararChefe()
        {
            float agora = Time.time;
            agente.radius = RaioDoAgenteDoChefe;
            agente.height = Mathf.Max(1.8f, alturaDoChefe);
            agente.acceleration = qualChefe == QualChefe.Longo ? 14f : 6f;
            agente.angularSpeed = 140f;
            agente.updateRotation = false;   // ele vira por conta própria (de frente para o alvo nos golpes)
            agente.avoidancePriority = 5;    // os outros saem da frente
            for (int i = 0; i < QuantasAcoes; i++) _prontoEm[i] = agora + 1.2f;
            _prontoEm[(int)AcaoDoChefe.Grito] = agora + 2.5f;
            _prontoEm[(int)AcaoDoChefe.Investida] = agora + 4f;
            _ultimoVistoChefe = agora;
            _casa = transform.position;
            _duracaoDoTonto = 0f;
            semNinguemDesde = -1f;
            MudarAcaoDoChefe(AcaoDoChefe.Surgir, true);
        }

        void ZerarChefe()
        {
            if (_segurado != null) SoltarSegurado(Vector3.zero);
            AcaoAtual = AcaoDoChefe.Nenhuma;
            _contadorDaAcao = 0;
            _acaoDesde = Time.time;
            _eventosFeitos = _efeitosFeitos = 0;
            for (int i = 0; i < QuantasAcoes; i++) _prontoEm[i] = 0f;
            _alvoDoChefe = _ultimoAutor = null;
            _temInteresse = false;
            Enfurecido = false;
            _frenesiPendente = false;
            _limiaresDoFrenesi = 0;
            _janelaDeDano = 0f;
            _proximaAcao = _paradoAte = _reacaoChefeAte = 0f;
            _altoDesde = -1f;
            _recuandoAte = 0f;
            _alvoDoAlto = null;
            _ignorados.Clear();
            _ultimoGritoEm = -999f;
            _morteVistaEm = -1f;
            _afundou = false;
            if (_temRaizDoChefe && raizDoModelo != null) raizDoModelo.localPosition = _raizDoChefe0;
            _atingidosPeloChefe.Clear();
            _atingidosNoAvanco.Clear();
            if (agente != null) agente.updateRotation = true;
        }

        /// <summary>Uma ação nova (no anfitrião pela decisão, nas cópias pela rede): a animação dela, do começo.</summary>
        void MudarAcaoDoChefe(AcaoDoChefe nova, bool comEfeito)
        {
            if (Morto) return;
            AcaoAtual = nova;
            _acaoDesde = Time.time;
            _eventosFeitos = 0;
            // (a cópia que nasceu no meio da ação não toca os sons de antes; o rugido de quem surge, sim: ele acabou de nascer)
            _efeitosFeitos = comEfeito || nova == AcaoDoChefe.Surgir ? 0 : ~0;
            _temMaoAntes = false;
            _atingidosPeloChefe.Clear();
            if (!Copia) _contadorDaAcao = (byte)((_contadorDaAcao + 1) & 7);
            if (!animador.isActiveAndEnabled) return;
            if (qualChefe == QualChefe.Blindado) animador.SetBool(InvestidaId, nova == AcaoDoChefe.Investida);
            if (qualChefe == QualChefe.Condutor) animador.SetBool(FrenesiId, nova == AcaoDoChefe.Frenesi);
            int estado = EstadoDaAcao[(int)nova];
            if (estado != 0) Tocar(estado, 0.12f);
        }

        void AnimarChefe(float dt)
        {
            bool parado = Morto || (AcaoAtual != AcaoDoChefe.Nenhuma && AcaoAtual != AcaoDoChefe.Investida);
            _velAnimacao = Mathf.Lerp(_velAnimacao, parado ? 0f : Velocidade, 1f - Mathf.Exp(-8f * dt));
            if (animador.isActiveAndEnabled) animador.SetFloat(VelocidadeDoChefeId, _velAnimacao);
        }

        /// <summary>Andando, a animação acompanha a velocidade (o clipe foi feito para a de perseguir).</summary>
        float RitmoDoChefe => AcaoAtual != AcaoDoChefe.Nenhuma || _velAnimacao < 0.15f || A == null ? 1f
            : Mathf.Clamp(_velAnimacao / Mathf.Max(0.3f, VelocidadeBaseDoChefe), 0.65f, 1.6f);

        void ReacaoDoChefe()
        {
            if (AcaoAtual != AcaoDoChefe.Nenhuma || Morto) return;
            Tocar(EstadoDanoId, 0.1f);
        }

        // ------------------------------------------------------------------ anfitrião: a decisão

        /// <summary>A decisão do chefe (no lugar da do Oco): escolhe o alvo, vai atrás e escolhe o golpe.</summary>
        void PensarChefe(float agora)
        {
            var c = C;
            if (AcaoAtual != AcaoDoChefe.Nenhuma)
            {
                Atual = Estado.Atacando;
                _engajadoAte = agora + 10f;
                return;
            }
            if (agora >= _proximoAlvo)
            {
                _proximoAlvo = agora + 0.4f;
                EscolherAlvoDoChefe(agora, c);
            }
            if (!AlvoDoChefeValido)
            {
                _alvoDoChefe = null;
                PasseioDoChefe(agora);
                return;
            }
            Vector3 pos = _alvoDoChefe.transform.position;
            Vector3 delta = pos - transform.position;
            float d = new Vector2(delta.x, delta.z).magnitude;
            _engajadoAte = agora + 10f;
            if (agora < _paradoAte)
            {
                if (agente.hasPath) agente.ResetPath();
                return;
            }
            if (!ReferenceEquals(_alvoDoAlto, _alvoDoChefe))
            {
                _alvoDoAlto = _alvoDoChefe;   // (outro alvo: o relógio do empacado começa de novo)
                _altoDesde = -1f;
            }
            // (os golpes que não pegam não zeram o relógio do empacado abaixo: só um acerto no alvo, AcertouOAlvo)
            if (agora >= _proximaAcao && EscolherAcaoDoChefe(agora, d, delta.y, c)) return;
            // não alcança quem está no alto (um telhado, uma sacada, um andar de cima): o alvo bem mais alto e ele sem chegar 1,5 m
            // mais perto em 7 s, sem acertar nada; aí ele sai da vista e deixa de lado quem está lá em cima. O Longo não: o ácido
            // chega no telhado. (No teste, encostado na parede da casa, ele andava aos trancos: a velocidade nunca ficava parada o
            // bastante para contar, e ele ficava 70 s embaixo do jogador)
            // Indo por um caminho inteiro até lá (a escada de um prédio, a rampa de um estacionamento), não conta: ele vai subir.
            bool alto = Mathf.Abs(delta.y) > 3f;
            if (alto && qualChefe != QualChefe.Longo)
            {
                // (o caminho tem de terminar lá em cima: um que termina no chão embaixo do alvo não é chegar, e com o alvo andando
                // de um lado para o outro numa sacada o fim dele mudava sempre e o relógio nunca enchia)
                bool chega = agente.hasPath && !agente.pathPending && agente.pathStatus == NavMeshPathStatus.PathComplete
                             && Mathf.Abs(agente.pathEndPosition.y - pos.y) < 3f
                             && agente.remainingDistance > agente.stoppingDistance + 0.6f;
                float falta = d + Mathf.Abs(delta.y);   // (subir também é chegar mais perto)
                if (chega || _altoDesde < 0f || falta < _distanciaDoAlto - 1.5f)
                {
                    _altoDesde = agora;   // (começou, está chegando, ou chegou mais perto: o relógio recomeça)
                    _distanciaDoAlto = falta;
                }
                else if (agora - _altoDesde > 7f && Recuar(pos, agora)) return;
            }
            else _altoDesde = -1f;
            Atual = Estado.Perseguindo;
            if (agora < _proximoCaminhoChefe) return;
            _proximoCaminhoChefe = agora + 0.3f;
            float parada = ParadaDoChefe;
            if (d > parada || alto) Ir(pos, VelocidadeDoChefe, parada);   // (no alto, segue o caminho até onde ele chega: uma rampa, uma escada)
            else if (agente.hasPath) agente.ResetPath();
        }

        void EscolherAlvoDoChefe(float agora, AjustesDosChefes c)
        {
            Vector3 olho = OlhoDoChefe;
            if (_alvoDoChefe != null)
            {
                if (!_alvoDoChefe.AlvoValido) _alvoDoChefe = null;
                else
                {
                    Vector3 p = _alvoDoChefe.transform.position;
                    float d = Vector3.Distance(p, transform.position);
                    // (dentro do bunker fechado, nem sentido de perto: ele desiste como o Oco, e não fica na porta)
                    bool isolado = BunkerRede.Isolado(p + Vector3.up * 1.3f, olho);
                    if (!isolado && (d < c.sentePerto || (d < c.alcanceDaVisao * 1.3f && _m.Enxerga(olho, p + Vector3.up * 1.3f)))) _ultimoVistoChefe = agora;
                    if (d > c.desisteA || agora - _ultimoVistoChefe > c.tempoSemVer || ZombieManager.NaZonaSegura(p))
                    {
                        // perdeu: vai até onde viu por último
                        _interesse = p;
                        _temInteresse = true;
                        _interesseAte = agora + 25f;
                        _alvoDoChefe = null;
                    }
                }
            }
            IAlvoDeZumbi melhor = null;
            float menor = float.MaxValue;
            for (int i = 0; i < _m.QuantosAlvos; i++)
            {
                ref readonly var p = ref _m.AlvoNo(i);
                if (p.quem == null || !p.quem.AlvoValido || Ignorado(p.quem, agora)) continue;
                if (BunkerRede.Isolado(p.peito, olho)) continue;
                if (ZombieManager.ZonasSeguras.Count > 0 && ZombieManager.NaZonaSegura(p.pes)) continue;
                float d = Vector3.Distance(p.pes, transform.position);
                if (d >= menor) continue;
                bool sente = d < c.sentePerto || (p.correndo && d < c.sentePerto * 1.6f);
                if (!sente && (d > c.alcanceDaVisao || !_m.Enxerga(olho, p.peito))) continue;
                menor = d;
                melhor = p.quem;
            }
            if (melhor == null) return;
            if (_alvoDoChefe == null)
            {
                _alvoDoChefe = melhor;
                _ultimoVistoChefe = agora;
                _temInteresse = false;
                // achou: o Condutor grita logo
                // (só se o último grito já passou do intervalo: trocar de alvo ou voltar à vista não grita de novo na hora)
                if (qualChefe == QualChefe.Condutor)
                    _prontoEm[(int)AcaoDoChefe.Grito] = Mathf.Min(_prontoEm[(int)AcaoDoChefe.Grito], Mathf.Max(agora + 0.8f, _ultimoGritoEm + c.intervaloDoGrito));
            }
            else if (!ReferenceEquals(melhor, _alvoDoChefe) && menor < Vector3.Distance(_alvoDoChefe.transform.position, transform.position) * 0.6f)
            {
                _alvoDoChefe = melhor;   // outro bem mais perto
                _ultimoVistoChefe = agora;
            }
        }

        /// <summary>Sem alvo: vai ver o barulho (ou onde viu alguém por último), ou anda devagar em volta.</summary>
        void PasseioDoChefe(float agora)
        {
            if (_temInteresse)
            {
                Atual = Estado.Investigando;
                Vector3 falta = _interesse - transform.position;
                falta.y = 0f;   // (o barulho de um telhado: chega embaixo)
                if (falta.sqrMagnitude < 9f || agora > _interesseAte)
                {
                    _temInteresse = false;
                    _casa = transform.position;
                    if (agente.hasPath) agente.ResetPath();
                    Atual = Estado.Parado;
                }
                else if (agora >= _proximoCaminhoChefe)
                {
                    _proximoCaminhoChefe = agora + 1f;
                    Ir(_interesse, VelocidadeDoChefe * 0.8f, 2f);
                }
                return;
            }
            if (agente.hasPath && Chegou())
            {
                agente.ResetPath();
                Atual = Estado.Parado;
            }
            if (agora < _proximoPasseio) return;
            _proximoPasseio = agora + Random.Range(6f, 12f);
            Vector2 r = Random.insideUnitCircle * 14f;
            if (NavMesh.SamplePosition(_casa + new Vector3(r.x, 0f, r.y), out var hit, 3f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - _casa.y) < 2f)
            {
                Ir(hit.position, VelocidadeDoChefe * 0.55f, 1.5f);
                Atual = Estado.Vagando;
            }
        }

        /// <summary>Ouviu (um tiro, um grito, o alarme): sem alvo, vai ver.</summary>
        void OuvirChefe(Vector3 ponto, float agora)
        {
            if (_alvoDoChefe != null || agora < _recuandoAte) return;   // (recuando do telhado, o tiro de lá não o chama de volta)
            _interesse = ponto;
            _temInteresse = true;
            _interesseAte = agora + 30f;
            _proximoCaminhoChefe = 0f;
        }

        void PegarAlvoDoChefe(IAlvoDeZumbi quem, float agora)
        {
            _engajadoAte = agora + 10f;
            if (_alvoDoChefe != null || quem == null || !quem.AlvoValido || Ignorado(quem, agora)) return;
            _alvoDoChefe = quem;
            _ultimoVistoChefe = agora;
            _temInteresse = false;
        }

        bool Pronto(AcaoDoChefe a, float agora) => agora >= _prontoEm[(int)a];

        bool VeOAlvo => AlvoDoChefeValido && _m.Enxerga(OlhoDoChefe, _alvoDoChefe.transform.position + Vector3.up * 1.3f);

        bool AlvoNaFrente(float graus)
        {
            if (!AlvoDoChefeValido) return false;
            Vector3 d = _alvoDoChefe.transform.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude < 0.25f || Vector3.Angle(transform.forward, d) < graus;
        }

        /// <summary>O golpe para a distância e o que está pronto; true: começou uma ação.</summary>
        bool EscolherAcaoDoChefe(float agora, float d, float dy, AjustesDosChefes c)
        {
            bool alto = Mathf.Abs(dy) > Mathf.Max(3f, alturaDoChefe * 0.5f);   // num telhado, num andar: só o que vai longe
            bool jogador = _alvoDoChefe is JogadorRede;
            switch (qualChefe)
            {
                case QualChefe.Blindado:
                    // (o agarrão pega também na beira de um telhado baixo: a mão sobe até o peito dele)
                    if (Mathf.Abs(dy) < AlturaDoAgarrao(AcaoDoChefe.Agarrar) && d < 5.5f && jogador && Pronto(AcaoDoChefe.Agarrar, agora) && AlvoNaFrente(70f))
                        return ComecarAcao(AcaoDoChefe.Agarrar, 9f, agora);
                    if (Mathf.Abs(dy) <= 2.5f && d < 6f && Pronto(AcaoDoChefe.Golpe, agora)) return ComecarAcao(AcaoDoChefe.Golpe, 4f, agora);
                    if (Mathf.Abs(dy) <= 2.5f && d > 9f && d < 26f && Pronto(AcaoDoChefe.Investida, agora) && VeOAlvo)
                        return ComecarAcao(AcaoDoChefe.Preparo, 0f, agora, AcaoDoChefe.Investida, 10f);
                    break;
                case QualChefe.AgregadoA:
                case QualChefe.AgregadoB:
                    if (!alto && d < 3.4f && Pronto(AcaoDoChefe.Golpe, agora)) return ComecarAcao(AcaoDoChefe.Golpe, 1.8f, agora);
                    if ((d > 3f || alto) && d < c.alcanceDaMao && Mathf.Abs(dy) <= AlturaDoAgarrao(AcaoDoChefe.Esticar) && jogador && Pronto(AcaoDoChefe.Esticar, agora) && VeOAlvo)
                        return ComecarAcao(AcaoDoChefe.Esticar, 7f, agora);
                    break;
                case QualChefe.Alongado:
                    if (!alto && d < 7.5f && Pronto(AcaoDoChefe.Varrer, agora)) return ComecarAcao(AcaoDoChefe.Varrer, 4f, agora);
                    if (d > 4f && d < c.alcanceDoAlongado && Mathf.Abs(dy) <= AlturaDoAgarrao(AcaoDoChefe.Alcancar) && jogador && Pronto(AcaoDoChefe.Alcancar, agora) && VeOAlvo)
                        return ComecarAcao(AcaoDoChefe.Alcancar, 8f, agora);
                    break;
                case QualChefe.Longo:
                    // (o bote também colado no alvo: a mordida; no teste, a menos de 1,5 m ele ficava parado sem golpe nenhum)
                    if (!alto && d < 7f && Pronto(AcaoDoChefe.Bote, agora)) return ComecarAcao(AcaoDoChefe.Bote, d < 2f ? 2.2f : 3.5f, agora);
                    if (d > (alto ? 1f : 5f) && d < 26f && Pronto(AcaoDoChefe.Cuspir, agora) && VeOAlvo)   // (no alto, cospe também de perto)
                        return ComecarAcao(AcaoDoChefe.Cuspir, Random.Range(c.intervaloDoCuspe.x, c.intervaloDoCuspe.y), agora);
                    break;
                case QualChefe.Condutor:
                    if (_frenesiPendente)
                    {
                        _frenesiPendente = false;
                        return ComecarAcao(AcaoDoChefe.Frenesi, 0f, agora);
                    }
                    if (d < 50f && Pronto(AcaoDoChefe.Grito, agora) && VeOAlvo) return ComecarAcao(AcaoDoChefe.Grito, c.intervaloDoGrito, agora);
                    if (!alto && d < 7f && Pronto(AcaoDoChefe.Varredura, agora)) return ComecarAcao(AcaoDoChefe.Varredura, 3.5f, agora);
                    break;
            }
            return false;
        }

        /// <summary>Começa uma ação (no anfitrião): para de andar e marca quando ela fica pronta de novo.</summary>
        bool ComecarAcao(AcaoDoChefe a, float espera, float agora, AcaoDoChefe marcar = AcaoDoChefe.Nenhuma, float esperaDaMarcada = 0f)
        {
            if (Morto) return false;   // (as estacas podem matá-lo no meio do quadro: nada tira ele de Morto)
            if (a == AcaoDoChefe.Grito) _ultimoGritoEm = agora;
            float k = Enfurecido ? 0.6f : 1f;
            _prontoEm[(int)a] = agora + espera * k;
            if (marcar != AcaoDoChefe.Nenhuma) _prontoEm[(int)marcar] = agora + esperaDaMarcada * k;
            if (NoNavMesh && agente.hasPath) agente.ResetPath();
            Atual = Estado.Atacando;
            _engajadoAte = agora + 10f;
            if (a == AcaoDoChefe.Bote)
            {
                _boteAndado = 0f;
                _direcaoDoBote = transform.forward;
            }
            MudarAcaoDoChefe(a, true);
            return true;
        }

        void TerminarAcao(float agora)
        {
            if (Morto) return;
            if (_segurado != null) SoltarSegurado(Vector3.zero);
            MudarAcaoDoChefe(AcaoDoChefe.Nenhuma, true);
            _proximaAcao = agora + (Enfurecido ? 0.2f : 0.45f);
            _proximoCaminhoChefe = 0f;
            Velocidade = 0f;
            Atual = Estado.Parado;
        }

        // ------------------------------------------------------------------ anfitrião: a cada quadro

        void ChefeNoQuadro(float agora, float dt)
        {
            var c = C;
            // (caiu, desmaiou, dormiu, entrou no carro ou no caiaque, saiu do jogo: solta)
            if (_segurado != null && !_segurado.LivreParaAMao) SoltarSegurado(Vector3.zero);
            Vector3 alvo = AlvoDoChefeValido ? _alvoDoChefe.transform.position : transform.position + transform.forward;
            if (AcaoAtual == AcaoDoChefe.Nenhuma)
            {
                if (agente.enabled && agente.velocity.sqrMagnitude > 0.04f) VirarPara(transform.position + agente.velocity, GiroDoChefe, dt);
                else if (AlvoDoChefeValido) VirarPara(alvo, GiroDoChefe * 0.8f, dt);
                return;
            }
            float naAcao = agora - _acaoDesde;
            var acao = AcaoAtual;
            if (acao != AcaoDoChefe.Investida && acao != AcaoDoChefe.Bote && NoNavMesh && agente.hasPath) agente.ResetPath();
            if (naAcao < ViraAte(acao)) VirarPara(alvo, acao == AcaoDoChefe.Preparo ? 360f : GiroDoChefe * 1.3f, dt);
            switch (acao)
            {
                case AcaoDoChefe.Golpe:
                    if (qualChefe == QualChefe.Blindado)
                    {
                        if (Evento(0, 1.2f, naAcao)) GolpeNoChaoDoBlindado(c);
                    }
                    else if (naAcao > 0.5f && naAcao < 0.95f)
                        GolpeDaMao(maoDoGolpe, 1.6f, c.danoDoGolpeDoAgregado, "Rasgado pelo Agregado.", TipoDeGolpe.Corte, PartesDoCorpo.Pesos.Briga, 0f, 4f, 2f, Vector3.zero);
                    break;
                case AcaoDoChefe.Agarrar:
                    if (Evento(0, 0.95f, naAcao)) TentarAgarrar(0f, 6.5f, AlturaDoAgarrao(AcaoDoChefe.Agarrar), 55f, 2.3f, 0f, null, TipoDeGolpe.Nenhum);
                    if (_segurado != null)
                    {
                        if (Evento(1, 1.7f, naAcao)) Apertar(c);
                        if (Evento(2, 2.25f, naAcao)) Apertar(c);
                        if (Evento(3, 2.6f, naAcao)) SoltarSegurado(transform.forward * c.arremessoDoBlindado + Vector3.up * 6f);
                    }
                    break;
                case AcaoDoChefe.Preparo:
                    if (naAcao >= DuracaoDaAcao(acao))
                    {
                        ComecarInvestida(c);
                        return;
                    }
                    break;
                case AcaoDoChefe.Investida:
                    InvestidaDoChefe(agora, dt, c);
                    return;
                case AcaoDoChefe.Esticar:
                    if (Evento(0, 0.72f, naAcao))
                        TentarAgarrar(2f, c.alcanceDaMao + 1f, AlturaDoAgarrao(AcaoDoChefe.Esticar), 32f, 1.5f, c.danoDaMao, "Agarrado pelo Agregado.", TipoDeGolpe.Corte);
                    if (_segurado != null && Evento(1, 1.85f, naAcao))
                    {
                        SoltarSegurado(transform.forward * 1.5f + Vector3.up * 1f);
                        _prontoEm[(int)AcaoDoChefe.Golpe] = agora;   // puxou para perto: o golpe vem logo
                    }
                    break;
                case AcaoDoChefe.Alcancar:
                    if (Evento(0, 0.8f, naAcao))
                        TentarAgarrar(3f, c.alcanceDoAlongado + 1f, AlturaDoAgarrao(AcaoDoChefe.Alcancar), 30f, 3.6f, 6f, "Agarrado pelo Alongado.", TipoDeGolpe.Pancada);
                    if (_segurado != null && Evento(1, 1.45f, naAcao))
                    {
                        MudarAcaoDoChefe(AcaoDoChefe.Arremessar, true);   // segurando: joga por cima da cabeça (a vítima vai junto)
                        return;
                    }
                    break;
                case AcaoDoChefe.Arremessar:
                    if (_segurado != null && Evento(0, 1.15f, naAcao))
                    {
                        var j = _segurado;
                        SoltarSegurado(transform.forward * c.arremessoDoAlongado + Vector3.up * 8f);
                        _m.GolpearPeloChefe(this, j, c.danoDoArremesso, "Arremessado pelo Alongado.", TipoDeGolpe.Pancada, PartesDoCorpo.Pesos.Briga, 0.25f, Vector3.zero, 0.8f);
                    }
                    break;
                case AcaoDoChefe.Varrer:
                    if (naAcao > 0.5f && naAcao < 1.6f)
                        GolpeDaMao(maoDoGolpe, 2f, c.danoDaVarrida, "Varrido pelo Alongado.", TipoDeGolpe.Pancada, PartesDoCorpo.Pesos.Briga, 0.15f, 10f, 3.5f, -transform.right);
                    break;
                case AcaoDoChefe.Cuspir:
                    if (Evento(0, 0.85f, naAcao)) CuspirNoAlvo();
                    break;
                case AcaoDoChefe.Absorver:
                    // agachado, ele se recompõe: a vida volta aos poucos enquanto o casco segura (15/11/2026, a pedido)
                    if (naAcao > 0.25f && naAcao < 2.35f)
                        _danoNoCorpo = Mathf.Max(0f, _danoNoCorpo - c.curaAbsorvendo * VidaDoCorpo * dt / 2.1f);
                    break;
                case AcaoDoChefe.Bote:
                    BoteDoLongo(agora, dt, naAcao, c);
                    break;
                case AcaoDoChefe.Grito:
                    if (Evento(0, 0.7f, naAcao)) _m.GritoDoCondutor(this, _alvoDoChefe);
                    break;
                case AcaoDoChefe.Varredura:
                    if (naAcao > 0.55f && naAcao < 1.35f)
                        GolpeDaMao(maoDoGolpe, 2.3f, c.danoDaVarredura, "Esmagado pelo Condutor.", TipoDeGolpe.Pancada, PartesDoCorpo.Pesos.Briga, 0.2f, 9f, 4f, transform.right);
                    break;
                case AcaoDoChefe.Frenesi:
                    if (Evento(0, 0.3f, naAcao))
                    {
                        Enfurecido = true;
                        _m.FrenesiNaHorda(this, C.tempoDoFrenesi);
                    }
                    break;
            }
            if (Morto) return;   // (morreu num evento da ação: as estacas)
            if (naAcao >= DuracaoDaAcao(acao)) TerminarAcao(agora);
        }

        // um evento da ação (um bit por evento): true na primeira vez que o tempo passa dele
        bool Evento(int bit, float t, float naAcao)
        {
            if (naAcao < t || (_eventosFeitos & (1 << bit)) != 0) return false;
            _eventosFeitos |= 1 << bit;
            return true;
        }

        /// <summary>
        /// O golpe de uma mão (ou da clava, ou da cabeça no bote): a cada quadro da janela do golpe, quem está perto do caminho
        /// que a mão fez desde o quadro anterior leva o golpe (cada um uma vez por ação). A mão é o osso de verdade (o Animator
        /// do chefe anima sempre, AplicarNivel): o dano sai onde a animação passa.
        /// </summary>
        void GolpeDaMao(Transform mao, float raio, float dano, string causa, TipoDeGolpe tipoDoGolpe, PartesDoCorpo.Pesos pesos, float fratura,
                        float empurrao, float sobe, Vector3 rumo)
        {
            Vector3 agora = mao != null ? mao.position : transform.position + transform.forward * Mathf.Max(1.5f, alturaDoChefe * 0.4f) + Vector3.up;
            Vector3 antes = _temMaoAntes ? _maoAntes : agora;
            _maoAntes = agora;
            _temMaoAntes = true;
            for (int i = 0; i < _m.QuantosAlvos; i++)
            {
                ref readonly var p = ref _m.AlvoNo(i);
                if (p.quem == null || !p.quem.AlvoValido || _atingidosPeloChefe.Contains(p.quem)) continue;
                float dist = Mathf.Min(DistanciaAoSegmento(p.peito, antes, agora), DistanciaAoSegmento(p.pes + Vector3.up * 0.4f, antes, agora));
                if (dist > raio) continue;
                _atingidosPeloChefe.Add(p.quem);
                Vector3 fora = p.pes - transform.position;
                fora.y = 0f;
                fora = fora.sqrMagnitude > 0.01f ? fora.normalized : transform.forward;
                Vector3 dir = rumo.sqrMagnitude > 0.01f ? (rumo.normalized * 0.6f + fora * 0.4f).normalized : fora;
                _m.GolpearPeloChefe(this, p.quem, dano, causa, tipoDoGolpe, pesos, fratura, dir * empurrao + Vector3.up * sobe, 0.6f);
            }
        }

        static float DistanciaAoSegmento(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>O golpe no chão do Blindado: dano em área em volta dos punhos, inteiro perto e um terço na borda.</summary>
        void GolpeNoChaoDoBlindado(AjustesDosChefes c)
        {
            Vector3 ponto = PontoDoGolpeDoChefe();
            float raio = Mathf.Max(1f, c.raioDoGolpe), cheio = raio * 0.4f;
            for (int i = 0; i < _m.QuantosAlvos; i++)
            {
                ref readonly var p = ref _m.AlvoNo(i);
                if (p.quem == null || !p.quem.AlvoValido) continue;
                Vector3 d = p.pes - ponto;
                if (Mathf.Abs(d.y) > 2.5f) continue;
                float dist = new Vector2(d.x, d.z).magnitude;
                if (dist > raio || !_m.Enxerga(ponto + Vector3.up * 0.5f, p.peito)) continue;
                float k = dist <= cheio ? 1f : Mathf.Lerp(1f, 0.33f, (dist - cheio) / (raio - cheio));
                Vector3 fora = new Vector3(d.x, 0f, d.z);
                fora = fora.sqrMagnitude > 0.01f ? fora.normalized : transform.forward;
                _m.GolpearPeloChefe(this, p.quem, c.danoDoGolpe * k, "Esmagado pelo Blindado.", TipoDeGolpe.Esmagamento, PartesDoCorpo.Pesos.Chao,
                                    k > 0.7f ? 0.5f : 0.2f, fora * 6f * k + Vector3.up * 5f * k, k);
            }
            Loot.ConstrucoesNoMundo.Instancia?.QuebrarBarricadasPerto(ponto, 3f, this);
        }

        /// <summary>Onde os punhos do Blindado batem: no chão, entre as duas mãos.</summary>
        Vector3 PontoDoGolpeDoChefe()
        {
            Vector3 p = maoQueAgarra != null && outraMao != null ? (maoQueAgarra.position + outraMao.position) * 0.5f
                : transform.position + transform.forward * Mathf.Max(1.5f, alturaDoChefe * 0.35f);
            p.y = transform.position.y;
            return p;
        }

        // ------------------------------------------------------------------ o agarrão

        /// <summary>
        /// A mão fechou: agarra quem está na frente, dentro do alcance e à vista (o alvo primeiro). Só jogadores são levados
        /// pela mão (o sobrevivente só leva o dano). O agarrado vai com a mão na máquina dele até ser solto.
        /// </summary>
        void TentarAgarrar(float de, float ate, float alcanceDeAltura, float cone, float tempoMaximo, float dano, string causa, TipoDeGolpe tipoDoGolpe)
        {
            IAlvoDeZumbi pego = null;
            float menor = float.MaxValue;
            for (int i = 0; i < _m.QuantosAlvos; i++)
            {
                ref readonly var p = ref _m.AlvoNo(i);
                if (p.quem == null || !p.quem.AlvoValido) continue;
                Vector3 d = p.pes - transform.position;
                float h = new Vector2(d.x, d.z).magnitude;
                if (d.magnitude < de - 0.5f || h > ate || Mathf.Abs(d.y) > alcanceDeAltura) continue;
                if (h > 1f && Vector3.Angle(transform.forward, new Vector3(d.x, 0f, d.z)) > cone) continue;
                if (!_m.Enxerga(transform.position + Vector3.up * Mathf.Min(2f, alturaDoChefe * 0.5f), p.peito)) continue;
                float prioridade = ReferenceEquals(p.quem, _alvoDoChefe) ? h - 100f : h;
                if (prioridade < menor)
                {
                    menor = prioridade;
                    pego = p.quem;
                }
            }
            if (pego == null) return;
            AcertouOAlvo(pego);
            if (dano > 0f) _m.GolpearPeloChefe(this, pego, dano, causa, tipoDoGolpe, PartesDoCorpo.Pesos.Briga, 0f, Vector3.zero, 0.4f);
            if (pego is JogadorRede j && j.PodeSerAgarrado && maoQueAgarra != null)
            {
                _segurado = j;
                _danoSegurando = 0f;
                j.AgarrarPeloChefe(this, tempoMaximo);
            }
        }

        void Apertar(AjustesDosChefes c)
        {
            if (_segurado == null) return;
            _m.GolpearPeloChefe(this, _segurado, c.danoDoAperto, "Esmagado na mão do Blindado.", TipoDeGolpe.Esmagamento, PartesDoCorpo.Pesos.Avanco,
                                0.35f, Vector3.zero, 0.7f);
        }

        /// <summary>Solta quem ele segura (no anfitrião), com esta velocidade (o arremesso; zero: só larga).</summary>
        void SoltarSegurado(Vector3 velocidade)
        {
            var j = _segurado;
            _segurado = null;
            if (j != null) j.SoltarDoChefe(this, velocidade);
        }

        /// <summary>No anfitrião: o dono não pôde ir para a mão (o pânico, o pulo da janela): larga sem apertar nem arremessar.</summary>
        internal void LargarSemArremesso(JogadorRede j)
        {
            if (_segurado != null && ReferenceEquals(_segurado, j)) _segurado = null;
        }

        /// <summary>Até que altura (m, acima ou abaixo dos pés dele) cada agarrão pega.</summary>
        float AlturaDoAgarrao(AcaoDoChefe a) => a switch
        {
            AcaoDoChefe.Agarrar => alturaDoChefe * 0.75f,          // o Blindado: a mão sobe até o peito
            AcaoDoChefe.Esticar => Mathf.Max(3f, alturaDoChefe * 1.3f),   // a mão de carne sobe esticando
            AcaoDoChefe.Alcancar => alturaDoChefe * 0.8f,          // o braço comprido
            _ => Mathf.Max(3f, alturaDoChefe * 0.45f),
        };

        /// <summary>No anfitrião: um golpe dele pegou em alguém; no alvo, o relógio do empacado embaixo do telhado zera.</summary>
        internal void AcertouOAlvo(IAlvoDeZumbi quem)
        {
            if (ReferenceEquals(quem, _alvoDoChefe)) _altoDesde = -1f;
        }

        /// <summary>
        /// Deixado de lado há pouco (Recuar) e ainda lá no alto: não volta a ser o alvo nem pela vista, nem pelo tiro, nem pelo
        /// barulho. Desceu ao nível dele, vale de novo na hora.
        /// </summary>
        bool Ignorado(IAlvoDeZumbi quem, float agora)
        {
            if (quem == null || !quem.AlvoValido) return false;
            return _ignorados.TryGetValue(quem, out float ate) && agora < ate && Mathf.Abs(quem.transform.position.y - transform.position.y) > 3f;
        }

        /// <summary>Deixa de lado, por 25 s, todos os que estão no alto aqui perto (dois amigos no mesmo telhado), e o alvo.</summary>
        void IgnorarOsDoAlto(float agora)
        {
            // (um por alvo: com quatro vagas, o quinto no alto tirava o alvo da lista e ele voltava para baixo do telhado)
            void Ignorar(IAlvoDeZumbi quem) => _ignorados[quem] = agora + 25f;
            if (_ignorados.Count > 32) _ignorados.Clear();   // (os vencidos e os que saíram do jogo não se acumulam)
            if (_alvoDoChefe != null) Ignorar(_alvoDoChefe);
            for (int i = 0; i < _m.QuantosAlvos; i++)
            {
                ref readonly var p = ref _m.AlvoNo(i);
                if (p.quem == null || !p.quem.AlvoValido || ReferenceEquals(p.quem, _alvoDoChefe)) continue;
                if (Mathf.Abs(p.pes.y - transform.position.y) > 3f && (p.pes - transform.position).sqrMagnitude < 45f * 45f) Ignorar(p.quem);
            }
        }

        static NavMeshPath _caminhoDoRecuo;

        /// <summary>
        /// Empacado embaixo de quem está no alto: deixa de lado (25 s, enquanto estiverem lá em cima) o alvo e os outros que
        /// estão no alto ali perto, e vai para um lugar no chão dele (no mesmo nível, com o caminho inteiro), de 25 a 35 m do
        /// alvo, de preferência fora da vista dele (sem achar, em qualquer direção, de 15 a 45 m; sem nada, só vaga ali). O
        /// tempo do recuo sai do comprimento do caminho, e o barulho não o chama de volta nesse tempo. Depois, se vir alguém de
        /// novo, volta. False só sem o NavMesh (tenta de novo daqui a 7 s).
        /// </summary>
        bool Recuar(Vector3 alvo, float agora)
        {
            if (!NoNavMesh)
            {
                _altoDesde = agora;
                return false;
            }
            if (_caminhoDoRecuo == null) _caminhoDoRecuo = new NavMeshPath();
            Vector3 fora = transform.position - alvo;
            fora.y = 0f;
            fora = fora.sqrMagnitude > 0.01f ? fora.normalized : -transform.forward;
            Vector3 olhoDoAlvo = alvo + Vector3.up * 1.6f, melhor = default;
            bool achou = false;
            float caminho = 0f;
            for (int t = 0; t < 20; t++)
            {
                bool largo = t >= 10;   // (a segunda volta: qualquer direção, mais perto ou mais longe, num desnível maior)
                float giro = largo ? Random.Range(0f, 360f) : (t % 2 == 0 ? 1f : -1f) * 20f * ((t + 1) / 2);
                Vector3 rumo = Quaternion.Euler(0f, giro, 0f) * fora;
                float r = largo ? Random.Range(15f, 45f) : Random.Range(25f, 35f);
                var tentativa = new Vector3(alvo.x + rumo.x * r, transform.position.y, alvo.z + rumo.z * r);   // (no nível dele, não no do telhado)
                if (!NavMesh.SamplePosition(tentativa, out var hit, 4f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - transform.position.y) > (largo ? 4f : 2.5f)) continue;
                if (!agente.CalculatePath(hit.position, _caminhoDoRecuo) || _caminhoDoRecuo.status != NavMeshPathStatus.PathComplete) continue;
                bool visto = _m.Enxerga(olhoDoAlvo, hit.position + Vector3.up * Mathf.Clamp(alturaDoChefe * 0.6f, 1f, 5f));
                if (!achou || !visto)
                {
                    melhor = hit.position;
                    achou = true;
                    caminho = Comprimento(_caminhoDoRecuo);
                    if (!visto) break;
                }
            }
            IgnorarOsDoAlto(agora);
            _alvoDoChefe = null;
            _altoDesde = -1f;
            _proximoCaminhoChefe = 0f;
            if (achou)
            {
                _interesse = melhor;   // (o PasseioDoChefe leva até lá, a 0,8 da velocidade)
                _temInteresse = true;
                _interesseAte = agora + caminho / Mathf.Max(0.3f, VelocidadeDoChefe * 0.8f) + 6f;
                _recuandoAte = _interesseAte;
            }
            else
            {
                _temInteresse = false;
                _casa = transform.position;
                _recuandoAte = agora + 10f;
            }
            return true;
        }

        static float Comprimento(NavMeshPath caminho)
        {
            var cantos = caminho.corners;
            float soma = 0f;
            for (int i = 1; i < cantos.Length; i++) soma += Vector3.Distance(cantos[i - 1], cantos[i]);
            return soma;
        }

        // ------------------------------------------------------------------ a investida, o bote e o ácido

        void ComecarInvestida(AjustesDosChefes c)
        {
            Vector3 alvo = AlvoDoChefeValido ? _alvoDoChefe.transform.position : transform.position + transform.forward * 10f;
            Vector3 d = alvo - transform.position;
            d.y = 0f;
            float dist = Mathf.Min(d.magnitude + 3f, c.investidaMaxima);
            _direcaoDoAvanco = d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
            Vector3 fim = transform.position + _direcaoDoAvanco * dist;
            _bateuNaParede = false;
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
            MudarAcaoDoChefe(AcaoDoChefe.Investida, true);
        }

        void InvestidaDoChefe(float agora, float dt, AjustesDosChefes c)
        {
            Vector3 falta = _fimDoAvanco - transform.position;
            falta.y = 0f;
            float passo = c.velocidadeDaInvestida * dt;
            bool chegou = falta.magnitude <= passo + 0.05f || _percorrido >= c.investidaMaxima || agora - _acaoDesde > DuracaoDaAcao(AcaoDoChefe.Investida);
            if (!chegou && NoNavMesh)
            {
                Vector3 antes = agente.nextPosition;
                agente.Move(_direcaoDoAvanco * passo);
                float andou = (agente.nextPosition - antes).magnitude;
                _percorrido += andou;
                _parado = andou < passo * 0.25f ? _parado + dt : 0f;
                if (_parado > 0.25f)
                {
                    _bateuNaParede = true;
                    chegou = true;
                }
            }
            Velocidade = c.velocidadeDaInvestida;
            for (int i = 0; i < _m.QuantosAlvos; i++)
            {
                ref readonly var p = ref _m.AlvoNo(i);
                if (p.quem == null || !p.quem.AlvoValido || _atingidosNoAvanco.Contains(p.quem)) continue;
                Vector3 d = p.pes - transform.position;
                if (Mathf.Abs(d.y) > 2.5f || new Vector2(d.x, d.z).sqrMagnitude > 2.8f * 2.8f) continue;
                _atingidosNoAvanco.Add(p.quem);
                // para o lado (para fora da linha da investida) e para cima
                Vector3 lado = Vector3.Cross(Vector3.up, _direcaoDoAvanco);
                if (Vector3.Dot(lado, d) < 0f) lado = -lado;
                _m.GolpearPeloChefe(this, p.quem, c.danoDaInvestida, "Atropelado pelo Blindado.", TipoDeGolpe.Esmagamento, PartesDoCorpo.Pesos.Avanco,
                                    0.35f, (lado * 0.7f + _direcaoDoAvanco * 0.3f).normalized * 9f + Vector3.up * 4.5f, 1f);
            }
            Loot.ConstrucoesNoMundo.Instancia?.QuebrarBarricadasPerto(transform.position + _direcaoDoAvanco * 1.2f, 2.5f, this);
            if (Morto) return;   // (as estacas o mataram agora)
            if (!chegou) return;
            Velocidade = 0f;
            if (_bateuNaParede)
            {
                _duracaoDoTonto = c.tontoDoBlindado;
                MudarAcaoDoChefe(AcaoDoChefe.Tonto, true);
                return;
            }
            // chegou perto do alvo: o golpe no chão, já
            if (AlvoDoChefeValido && Vector3.Distance(_alvoDoChefe.transform.position, transform.position) < 6.5f)
            {
                ComecarAcao(AcaoDoChefe.Golpe, 4f, agora);
                return;
            }
            TerminarAcao(agora);
        }

        void BoteDoLongo(float agora, float dt, float naAcao, AjustesDosChefes c)
        {
            if (naAcao < 0.5f && AlvoDoChefeValido)
            {
                Vector3 d = _alvoDoChefe.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f) _direcaoDoBote = d.normalized;
            }
            if (naAcao >= 0.5f && naAcao < 0.85f && NoNavMesh && _boteAndado < c.boteMaximo)
            {
                bool colado = AlvoDoChefeValido && new Vector2(_alvoDoChefe.transform.position.x - transform.position.x,
                                                               _alvoDoChefe.transform.position.z - transform.position.z).magnitude < 1.3f;
                if (!colado)
                {
                    float passo = 12f * dt;
                    agente.Move(_direcaoDoBote * passo);
                    _boteAndado += passo;
                    Velocidade = 12f;
                }
            }
            else Velocidade = 0f;
            if (naAcao > 0.55f && naAcao < 1f)
                GolpeDaMao(cabecaDoChefe != null ? cabecaDoChefe : bocaDoChefe, 1.5f, c.danoDoBote, "Mordido pelo Longo.", TipoDeGolpe.Mordida,
                           PartesDoCorpo.Pesos.Corredor, 0f, 5f, 2f, _direcaoDoBote);
        }

        /// <summary>
        /// O ácido do Longo: a gosma do Long (prende quem leva em cheio, a poça apaga a lanterna), que também corrói (o dano em
        /// cheio e a queima na poça) e envenena; da boca dele até onde o alvo vai estar (um pouco à frente).
        /// </summary>
        void CuspirNoAlvo()
        {
            if (!AlvoDoChefeValido) return;
            Vector3 de = bocaDoChefe != null ? bocaDoChefe.position : transform.position + Vector3.up * 1.8f + transform.forward;
            Vector3 para = _alvoDoChefe.transform.position;
            if (_alvoDoChefe is JogadorRede j && j.TryGetComponent<CharacterController>(out var cc))
            {
                Vector3 v = cc.velocity;
                v.y = 0f;
                para += v * Mathf.Clamp(Vector3.Distance(de, para) / Mathf.Max(4f, A.cuspidor.velocidadeDaGosma), 0.2f, 1.2f) * 0.6f;
            }
            if (NavMesh.SamplePosition(para, out var hit, 2f, NavMesh.AllAreas)) para = hit.position;
            _m.LancarAcido(de, para, this);   // (a gosma do Long que também corrói e envenena: ZombieManager.Gosma.cs)
        }

        // ------------------------------------------------------------------ os tiros nele (anfitrião)

        /// <summary>
        /// Um acerto no chefe: a cabeça e o ponto fraco tiram mais, os braços e as pernas menos; o casco do Blindado tira quase
        /// tudo, e absorvendo, mais ainda. Segurando alguém, muito dano o faz soltar. Muito dano de uma vez faz o Blindado
        /// absorver; o Condutor entra em frenesi com metade e com um quinto da vida.
        /// </summary>
        void LevarTiroNoChefe(RegiaoDoCorpo regiao, float dano, Vector3 ponto, Vector3 direcao, IAlvoDeZumbi autor, float agora)
        {
            var c = C;
            dano = Mathf.Clamp(dano, 0f, c.maiorAcerto);
            float fator = regiao switch
            {
                RegiaoDoCorpo.Cabeca => c.multiplicadorNaCabeca,
                RegiaoDoCorpo.Papo => c.multiplicadorNoPontoFraco,
                RegiaoDoCorpo.Braco or RegiaoDoCorpo.Perna => c.danoNosMembros,
                _ => 1f,
            };
            if (qualChefe == QualChefe.Blindado)
            {
                if (Absorvendo) fator *= c.danoAbsorvendo;
                else if (regiao != RegiaoDoCorpo.Cabeca) fator *= c.cascoDoBlindado;
            }
            if (AcaoAtual == AcaoDoChefe.Surgir) fator *= 0.5f;
            float tirado = dano * fator;
            _danoNoCorpo += tirado;
            if (autor != null) _ultimoAutor = autor;
            if (_danoNoCorpo >= VidaDoCorpo)
            {
                Morrer(agora, false, ponto, direcao, false);
                return;
            }
            _engajadoAte = agora + 15f;
            // os amigos atirando nele: solta quem ele segura
            if (_segurado != null)
            {
                _danoSegurando += tirado;
                if (_danoSegurando >= c.danoParaSoltar)
                {
                    SoltarSegurado(transform.forward * 3f + Vector3.up * 2f);
                    _duracaoDoTonto = 1.6f;
                    MudarAcaoDoChefe(AcaoDoChefe.Tonto, true);
                    _proximaAcao = agora + 1.8f;
                }
            }
            // o Blindado: muito dano em 3 s, ele agacha e absorve
            if (agora - _janelaDesde > 3f)
            {
                _janelaDesde = agora;
                _janelaDeDano = 0f;
            }
            _janelaDeDano += dano;
            if (qualChefe == QualChefe.Blindado && AcaoAtual == AcaoDoChefe.Nenhuma && _janelaDeDano >= c.danoParaAbsorver && Pronto(AcaoDoChefe.Absorver, agora))
            {
                _janelaDeDano = 0f;
                ComecarAcao(AcaoDoChefe.Absorver, 12f, agora);
            }
            // o Condutor: o frenesi com metade e com um quinto da vida (entra quando a ação de agora acaba)
            if (qualChefe == QualChefe.Condutor)
            {
                float f = FracaoDaVida;
                if ((_limiaresDoFrenesi == 0 && f < 0.5f) || (_limiaresDoFrenesi == 1 && f < 0.2f))
                {
                    _limiaresDoFrenesi = f < 0.2f ? 2 : 1;
                    _frenesiPendente = true;
                    _proximaAcao = Mathf.Min(_proximaAcao, agora);
                }
            }
            // a reação: só parado entre as ações, no máximo a cada 1,6 s, e com um acerto que conta
            if (AcaoAtual == AcaoDoChefe.Nenhuma && agora >= _reacaoChefeAte && tirado >= VidaDoCorpo * 0.02f)
            {
                _reacaoChefeAte = agora + 1.6f;
                _paradoAte = agora + 0.5f;
                Reacoes++;
                MostrarReacao();
            }
            // quem atirou: vira o alvo (sem alvo, sempre; com outro, às vezes)
            if (autor != null && autor.AlvoValido && !Ignorado(autor, agora) && (_alvoDoChefe == null || (!ReferenceEquals(autor, _alvoDoChefe) && Random.value < 0.25f)))
            {
                _alvoDoChefe = autor;
                _ultimoVistoChefe = agora;
                _temInteresse = false;
            }
        }

        /// <summary>Morreu (no anfitrião, antes da morte na tela): solta quem segura e larga a ação (a morte toca por cima).</summary>
        void AntesDaMorteDoChefe()
        {
            if (_segurado != null) SoltarSegurado(Vector3.zero);
            if (AcaoAtual != AcaoDoChefe.Nenhuma) MudarAcaoDoChefe(AcaoDoChefe.Nenhuma, false);
        }

        /// <summary>Morreu (no anfitrião): o Blindado se divide e o Alongado vira o Longo (ZombieManager.Chefes.cs).</summary>
        void DepoisDaMorteDoChefe() => _m.ChefeMorreu(this, _ultimoAutor ?? _alvoDoChefe);

        // ------------------------------------------------------------------ em todas as máquinas: a morte e os efeitos

        void MostrarMorteDoChefe(bool comEfeito)
        {
            _morteVistaEm = comEfeito ? Time.time : Time.time - 999f;   // (a cópia que já chegou morta: o corpo só some)
            _afundou = !comEfeito;
            AtualizarColisores();
            AtualizarRenderers();
            if (!comEfeito || !animador.isActiveAndEnabled) return;
            if (qualChefe == QualChefe.Blindado) animador.SetBool(InvestidaId, false);
            if (qualChefe == QualChefe.Condutor) animador.SetBool(FrenesiId, false);
            Tocar(qualChefe == QualChefe.Blindado ? EstadoDividirId : EstadoMorteId, 0.15f);
            if (_m != null)
            {
                bool pequeno = qualChefe == QualChefe.Longo || qualChefe == QualChefe.AgregadoA || qualChefe == QualChefe.AgregadoB;
                _m.SomDoChefe(this, pequeno ? "Long_Agonia" : "Brutamonte_Furia", 1f, 100f);
            }
            Sangue.Mancha(transform.position + transform.forward * 0.8f, Mathf.Clamp(alturaDoChefe * 0.3f, 1.2f, 3f));
        }

        /// <summary>
        /// Em todas as máquinas, depois da animação: se o Animator perdeu a ação (ligado agora, a cópia chegou no meio), entra
        /// nela de novo no ponto certo; os sons e o pó nos tempos do clipe; morto, o corpo deita e afunda no chão.
        /// </summary>
        void ChefeDepoisDaAnimacao(float dt)
        {
            if (Morto)
            {
                AfundarOChefe();
                return;
            }
            if (!animador.isActiveAndEnabled) return;
            float naAcao = Time.time - _acaoDesde;
            int estado = EstadoDaAcao[(int)AcaoAtual];
            if (estado != 0 && naAcao > 0.2f && naAcao < DuracaoDoClipe(AcaoAtual) - 0.25f && !animador.IsInTransition(0)
                && animador.GetCurrentAnimatorStateInfo(0).shortNameHash != estado)
                Tocar(estado, 0.1f, naAcao);
            EfeitosDaAcao(naAcao);
        }

        void EfeitosDaAcao(float naAcao)
        {
            if (_m == null) return;
            bool grande = qualChefe == QualChefe.Blindado || qualChefe == QualChefe.Alongado || qualChefe == QualChefe.Condutor;
            switch (AcaoAtual)
            {
                case AcaoDoChefe.Surgir:
                    if (Efeito(0, 0.05f, naAcao)) _m.SomDoChefe(this, grande ? "Brutamonte_Rugido" : "Long_Grito", grande ? 1f : 0.8f, 90f);
                    break;
                case AcaoDoChefe.Golpe:
                    if (qualChefe == QualChefe.Blindado)
                    {
                        if (Efeito(0, 0.3f, naAcao)) _m.SomDoChefe(this, "Brutamonte_Rugido", 0.8f, 60f);
                        if (Efeito(1, 1.2f, naAcao)) _m.EfeitoDoGolpeNoChao(this, PontoDoGolpeDoChefe(), 1.45f);
                    }
                    break;
                case AcaoDoChefe.Agarrar:
                    if (Efeito(0, 0.55f, naAcao)) _m.SomDoChefe(this, "Brutamonte_Baque", 0.8f, 45f);
                    break;
                case AcaoDoChefe.Preparo:
                    if (Efeito(0, 0f, naAcao)) _m.SomDoChefe(this, "Brutamonte_Rugido", 1f, 80f);
                    break;
                case AcaoDoChefe.Investida:
                {
                    // os passos pesados: um baque e o chão tremendo a cada meio segundo
                    int passo = Mathf.Min(30, Mathf.FloorToInt(naAcao / 0.45f));
                    if (Efeito(passo, passo * 0.45f, naAcao))
                    {
                        _m.SomDoChefe(this, "Brutamonte_Baque", 0.7f, 45f);
                        _m.TremerPerto(transform.position, 14f, 0.35f);
                    }
                    break;
                }
                case AcaoDoChefe.Tonto:
                    if (Efeito(0, 0f, naAcao)) _m.SomDoChefe(this, "Brutamonte_Baque", 1f, 50f);
                    break;
                case AcaoDoChefe.Arremessar:
                    if (Efeito(0, 1.05f, naAcao)) _m.SomDoChefe(this, "Brutamonte_Rugido", 0.9f, 70f);
                    break;
                case AcaoDoChefe.Varrer:
                case AcaoDoChefe.Varredura:
                    if (Efeito(0, 0.4f, naAcao)) _m.SomDoChefe(this, "Brutamonte_Rugido", 0.7f, 60f);
                    if (Efeito(1, AcaoAtual == AcaoDoChefe.Varrer ? 0.9f : 0.85f, naAcao)) _m.SomDoChefe(this, "Brutamonte_Baque", 0.8f, 45f);
                    break;
                case AcaoDoChefe.Esticar:
                case AcaoDoChefe.Alcancar:
                    if (Efeito(0, 0.35f, naAcao)) _m.SomDoChefe(this, "Long_Cuspe", 0.6f, 40f);
                    break;
                case AcaoDoChefe.Cuspir:
                    if (Efeito(0, 0.6f, naAcao)) _m.SomDoChefe(this, "Long_Cuspe", 1f, 45f);
                    break;
                case AcaoDoChefe.Bote:
                    if (Efeito(0, 0.45f, naAcao)) _m.SomDoChefe(this, "Long_Grito", 0.55f, 50f);
                    break;
                case AcaoDoChefe.Grito:
                    if (Efeito(0, 0.65f, naAcao))
                    {
                        _m.SomDoChefe(this, "Long_Grito", 1f, 140f);
                        _m.SomDoChefe(this, "Brutamonte_Furia", 0.8f, 120f);
                        _m.TremerPerto(transform.position, 30f, 0.55f);
                    }
                    break;
                case AcaoDoChefe.Frenesi:
                    if (Efeito(0, 0.1f, naAcao)) _m.SomDoChefe(this, "Brutamonte_Furia", 1f, 110f);
                    break;
            }
        }

        // um efeito da ação (um bit por efeito), em cada máquina
        bool Efeito(int bit, float t, float naAcao)
        {
            if (naAcao < t || (_efeitosFeitos & (1 << bit)) != 0) return false;
            _efeitosFeitos |= 1 << bit;
            return true;
        }

        /// <summary>O corpo do chefe morto: deita (a animação da morte) e, uns segundos depois, afunda no chão e some.</summary>
        void AfundarOChefe()
        {
            if (_morteVistaEm < 0f || _afundou || raizDoModelo == null) return;
            float t = Time.time - _morteVistaEm - (DuracaoDaMorte + 4f);
            if (t <= 0f) return;
            if (!_temRaizDoChefe)
            {
                _raizDoChefe0 = raizDoModelo.localPosition;
                _temRaizDoChefe = true;
            }
            float k = Mathf.Clamp01(t / 6f);
            raizDoModelo.localPosition = _raizDoChefe0 + Vector3.down * (k * k * (alturaDoChefe * 0.35f + 1.5f));
            if (k < 1f) return;
            _afundou = true;
            AtualizarRenderers();
        }

        // ------------------------------------------------------------------ o frenesi da horda (todos os zumbis)

        float _frenesiAte, _fatorDoFrenesi = 1f;

        /// <summary>No anfitrião: o grito (ou o frenesi) do Condutor deixa este zumbi mais rápido até <paramref name="ate"/>.</summary>
        internal void Enfurecer(float ate, float fator)
        {
            _frenesiAte = Mathf.Max(_frenesiAte, ate);
            _fatorDoFrenesi = Mathf.Max(1f, fator);
        }

        /// <summary>A velocidade a mais do frenesi (entra no <see cref="FatorDaPedra"/>, por onde passam todas as contas da velocidade).</summary>
        float FatorDoFrenesi => Time.time < _frenesiAte ? _fatorDoFrenesi : 1f;

        float _chamadoAte;

        /// <summary>
        /// No anfitrião: chamado pelo grito do Condutor, ele vai atrás do alvo de longe até <paramref name="ate"/>: não desiste
        /// pela distância (o Oco desiste a 20 m, e a horda nasce a mais de 20 m de todos) nem por não ver, e fica engajado
        /// (no nível Perto, sem voltar para o pool). Ferido: vem mesmo de dia, virando pedra.
        /// </summary>
        internal void ChamarPeloGrito(IAlvoDeZumbi alvo, float ate)
        {
            if (Morto || alvo == null || !alvo.AlvoValido) return;
            float agora = Time.time;
            _chamadoAte = Mathf.Max(_chamadoAte, ate);
            _engajadoAte = Mathf.Max(_engajadoAte, ate);
            Ferir(agora);   // (o Ferido e o Ameaçado: o sol não o tira da caça)
            ComecarPerseguicao(alvo, agora);
        }

        /// <summary>Ainda no chamado do grito (Zumbi.cs, Perseguir: não desiste de longe nem por não ver).</summary>
        bool ChamadoPeloGrito => Time.time < _chamadoAte;

        // ------------------------------------------------------------------ o F1

        /// <summary>F1 > Chefes (no anfitrião): a próxima habilidade dele agora, virado para quem pediu.</summary>
        internal string HabilidadeAgora(Vector3 virarPara, IAlvoDeZumbi quem)
        {
            if (!EhChefe || Morto) return null;
            if (AcaoAtual != AcaoDoChefe.Nenhuma) return $"{NomeDoChefe} já está numa ação ({AcaoAtual}).";
            Vector3 d = virarPara - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
            if (quem != null) PegarAlvoDoChefe(quem, Time.time);
            float agora = Time.time;
            var lista = qualChefe switch
            {
                QualChefe.Blindado => new[] { AcaoDoChefe.Golpe, AcaoDoChefe.Agarrar, AcaoDoChefe.Preparo, AcaoDoChefe.Absorver },
                QualChefe.AgregadoA or QualChefe.AgregadoB => new[] { AcaoDoChefe.Esticar, AcaoDoChefe.Golpe },
                QualChefe.Alongado => new[] { AcaoDoChefe.Alcancar, AcaoDoChefe.Varrer },
                QualChefe.Longo => new[] { AcaoDoChefe.Cuspir, AcaoDoChefe.Bote },
                _ => new[] { AcaoDoChefe.Grito, AcaoDoChefe.Varredura, AcaoDoChefe.Frenesi },
            };
            var a = lista[_proximaDoF1++ % lista.Length];
            ComecarAcao(a, 0f, agora);
            return $"{NomeDoChefe}: {a}.";
        }
        int _proximaDoF1;

        /// <summary>F1 > Chefes (no anfitrião): tira esta fração da vida inteira (1: morre, e o que vem depois dele nasce).</summary>
        internal void TirarVidaDoChefe(float fracao, IAlvoDeZumbi autor)
        {
            if (!EhChefe || Morto || _m == null) return;
            float agora = Time.time;
            if (autor != null) _ultimoAutor = autor;
            _danoNoCorpo = Mathf.Min(VidaDoCorpo, _danoNoCorpo + VidaDoCorpo * fracao);
            if (_danoNoCorpo >= VidaDoCorpo - 0.01f)
            {
                Morrer(agora, false, transform.position + Vector3.up * (alturaDoChefe * 0.5f), -transform.forward, false);
                return;
            }
            // (um acerto mínimo: o frenesi do Condutor, o alvo)
            LevarTiroNoChefe(RegiaoDoCorpo.Tronco, 0.001f, transform.position + Vector3.up, -transform.forward, autor, agora);
        }
    }
}
