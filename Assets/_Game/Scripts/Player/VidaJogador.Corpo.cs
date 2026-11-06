using System;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.World;
using Random = UnityEngine.Random;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O corpo do jogador (05/11/2026, a pedido: "a vida não se regenera mais sozinha ... cada membro do jogador terá uma
    /// vida"; a etapa 1 do sistema de vida, o Docs/16): as 6 partes (<see cref="ParteDoCorpo"/>), as feridas que sangram
    /// (20% na mordida do Oco e do Corredor; o tiro sempre; o corte, a pancada e o tornado às vezes; só a bandagem ou o
    /// kit estancam), as lesões dos membros (a entorse, a fratura e a fratura exposta: das quedas e do Brutamonte, que
    /// nunca faz sangrar) e os tratamentos (<see cref="Tratar"/>). Só o anfitrião conta (com floats); a rede recebe o
    /// <see cref="EstadoDoCorpo"/> uma vez por segundo, e na hora de cada golpe e tratamento. O tempo das feridas e das
    /// lesões anda em minutos do jogo (<see cref="CicloDoDia.HorasPorSegundo"/>: 1 minuto do jogo = 1 s real), só com o
    /// mundo pronto e fora do Dia 0, e vai no save do personagem (passa de um mundo a outro).
    /// </summary>
    public partial class VidaJogador
    {
        /// <summary>O corpo, como os outros veem (a faixa, a tala) e como vai no save (escrito pelo anfitrião).</summary>
        public readonly NetworkVariable<EstadoDoCorpo> Corpo = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>
        /// O que ele está tratando agora (a fase 3, 05/11/2026: a pose do curativo que os outros veem, PoseDoCurativo),
        /// escrito pelo dono: 0 nada; 1 a 6, a parte + 1 do próprio corpo; <see cref="TratandoOAmigo"/>;
        /// <see cref="TratandoPelaBoca"/> (o comprimido, o xarope, o antibiótico).
        /// </summary>
        public readonly NetworkVariable<byte> Tratando = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public const byte TratandoOAmigo = 7, TratandoPelaBoca = 8;

        /// <summary>O corpo mudou (em todas as máquinas): a faixa e a tala no corpo, o HUD.</summary>
        public event Action<EstadoDoCorpo> CorpoMudou;

        /// <summary>
        /// No dono, a cada golpe que pegou numa parte: a parte, o jeito, a lesão nova (Nenhuma: nada novo) e se abriu uma
        /// ferida. O HUD pisca a parte; o curativo que estava sendo feito para.
        /// </summary>
        public event Action<ParteDoCorpo, TipoDeGolpe, Lesao, bool> Golpeado;

        /// <summary>Quanto a ferida leva para fechar, enfaixada (minutos do jogo: 3 dias). Sem curativo, ela não fecha.</summary>
        public const float MinutosDaFerida = 3 * 24 * 60;

        /// <summary>Quanto a lesão leva para sarar com a tala e o analgésico (minutos do jogo; sem o analgésico, o dobro).</summary>
        public static float MinutosParaSarar(Lesao l) => l switch
        {
            Lesao.Entorse => 6 * 60, Lesao.Fratura => 18 * 60, Lesao.Exposta => 36 * 60, _ => 0f,
        };

        /// <summary>A chance de o golpe abrir uma ferida que sangra.</summary>
        public static float ChanceDeFerida(TipoDeGolpe t) => t switch
        {
            TipoDeGolpe.Mordida => 0.2f, TipoDeGolpe.Corte => 0.7f, TipoDeGolpe.Pancada => 0.35f, TipoDeGolpe.Tiro => 1f,
            TipoDeGolpe.Tornado => 0.3f, _ => 0f,
        };

        /// <summary>Quanto a ferida tira por segundo (da vida geral), pelo jeito do golpe que a abriu.</summary>
        public static float Sangramento(TipoDeGolpe t) => t switch
        {
            TipoDeGolpe.Mordida => 0.10f, TipoDeGolpe.Corte => 0.15f, TipoDeGolpe.Pancada => 0.08f, TipoDeGolpe.Tiro => 0.25f,
            TipoDeGolpe.Tornado => 0.10f, _ => 0.12f,
        };

        /// <summary>A fratura exposta sangra mais que qualquer corte.</summary>
        public const float SangramentoDaExposta = 0.30f;
        const float SangramentoMaximo = 0.6f;

        /// <summary>O que a lesão tira do membro além do golpe (a perna quebrada fica bem machucada).</summary>
        static float DanoNoMembro(Lesao l) => l switch { Lesao.Entorse => 15f, Lesao.Fratura => 40f, Lesao.Exposta => 60f, _ => 0f };

        // só no anfitrião: o corpo com as frações (a rede leva os números redondos)
        readonly float[] _danoDaParte = new float[PartesDoCorpo.Quantas];
        readonly float[] _feridaFalta = new float[PartesDoCorpo.Quantas];
        readonly float[] _sangramento = new float[PartesDoCorpo.Quantas];
        readonly float[] _sararFalta = new float[PartesDoCorpo.Quantas];
        readonly Lesao[] _lesao = new Lesao[PartesDoCorpo.Quantas];
        byte _sangra, _faixa, _talas;
        float _analgesicoFalta, _sangue, _regenAte, _regenPorSegundo, _regenSobra, _proximaGravacaoDoCorpo, _ultimaQueda = -10f;
        bool _corpoMudou;
        // a etapa 2 (a limpeza e a infecção), só no anfitrião: a idade e as horas suja de cada ferida (horas do jogo), os
        // dias sorteados, a proteção até o amanhecer, a limpeza do spray, a cobertura do antibiótico, a infecção e os tempos
        readonly float[] _idade = new float[PartesDoCorpo.Quantas];
        readonly float[] _sujas = new float[PartesDoCorpo.Quantas];
        readonly int[] _dias = new int[PartesDoCorpo.Quantas];
        byte _protegida, _limpa, _coberta;
        int _infeccao, _origem, _amanheceres, _amanhecerVisto;
        uint _semente;
        float _infeccaoFalta, _doseFalta, _xarope, _desdeOAmanhecer, _sepse;
        InventarioJogador _inventario;

        static int Bit(ParteDoCorpo p) => 1 << (int)p;

        /// <summary>
        /// O corpo anda (o sangramento, o fechar das feridas, o sarar das lesões): com o mundo pronto e fora do Dia 0. Sem o
        /// mundo da partida (as cenas de exemplo), sempre.
        /// </summary>
        public static bool CorpoAnda
        {
            get
            {
                var mundo = MundoDaPartida.Instancia;
                return mundo == null || (mundo.Pronto && !mundo.NoDiaZero);
            }
        }

        void NascerCorpo()
        {
            _inventario = GetComponent<InventarioJogador>();
            Corpo.OnValueChanged += AoMudarCorpo;
            if (IsServer) SararTudo();
        }

        void SumirCorpo() => Corpo.OnValueChanged -= AoMudarCorpo;

        void AoMudarCorpo(EstadoDoCorpo antes, EstadoDoCorpo depois) => CorpoMudou?.Invoke(depois);

        /// <summary>No anfitrião: o corpo inteiro de novo (nascer, renascer, os 5 anos do Dia 0, os testes).</summary>
        public void SararTudo()
        {
            if (!IsServer) return;
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                _danoDaParte[p] = _feridaFalta[p] = _sangramento[p] = _sararFalta[p] = 0f;
                _lesao[p] = Lesao.Nenhuma;
            }
            _sangra = _faixa = _talas = 0;
            _analgesicoFalta = _sangue = _regenSobra = 0f;
            _regenAte = 0f;
            // a etapa 2: as feridas e a infecção somem (a semente fica: é do personagem)
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                _idade[p] = _sujas[p] = 0f;
                _dias[p] = 0;
            }
            _protegida = _limpa = _coberta = 0;
            _infeccao = _origem = 0;
            _infeccaoFalta = _doseFalta = _xarope = _sepse = 0f;
            _amanhecerVisto = CicloDoDia.Amanheceres;
            GravarCorpo();
        }

        /// <summary>No anfitrião: o corpo de um personagem salvo (02/11/2026, o save; o corpo, 05/11/2026).</summary>
        public void DefinirCorpo(EstadoDoCorpo e)
        {
            if (!IsServer) return;
            e = e.Conferido();
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                var pp = (ParteDoCorpo)p;
                _danoDaParte[p] = e.Dano(pp);
                _feridaFalta[p] = e.FeridaFalta(pp);
                // (o save não guarda o sangue de cada ferida: a exposta volta com o dela, o resto com o comum)
                _sangramento[p] = !e.Sangra(pp) ? 0f : e.LesaoDe(pp) == Lesao.Exposta ? SangramentoDaExposta : Sangramento(TipoDeGolpe.Nenhum);
                _lesao[p] = e.LesaoDe(pp);
                _sararFalta[p] = e.SararFalta(pp);
            }
            _sangra = e.sangra;
            _faixa = e.faixa;
            _talas = e.talas;
            _analgesicoFalta = e.analgesico;
            _sangue = 0f;
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                var pp = (ParteDoCorpo)p;
                _idade[p] = e.Idade(pp);
                _sujas[p] = e.Sujas(pp);
                _dias[p] = e.Dias(pp);
            }
            _protegida = e.protegida;
            _limpa = e.limpa;
            _coberta = e.coberta;
            _infeccao = e.infeccao;
            _origem = e.origem;
            _infeccaoFalta = e.infeccaoFalta;
            _doseFalta = e.doseFalta;
            _xarope = e.xarope;
            _sepse = 0f;
            _semente = e.semente;
            _amanheceres = e.amanheceres;
            _desdeOAmanhecer = e.desdeOAmanhecer;
            // quem entra no meio do dia não sorteia na hora: o próximo amanhecer do mundo é o primeiro
            _amanhecerVisto = CicloDoDia.Amanheceres;
            GravarCorpo();
        }

        /// <summary>O corpo como está agora no anfitrião (com os números redondos), sem esperar a gravação.</summary>
        EstadoDoCorpo EstadoAgora()
        {
            var e = new EstadoDoCorpo();
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                var pp = (ParteDoCorpo)p;
                // arredonda para cima: um arranhão de 0,4 ainda aparece (e conta para o curativo)
                e.PorDano(pp, Mathf.CeilToInt(_danoDaParte[p] - 0.01f));
                e.PorFerida(pp, Mathf.CeilToInt(_feridaFalta[p]));
                e.PorLesao(pp, _lesao[p]);
                e.PorSarar(pp, Mathf.CeilToInt(_sararFalta[p]));
            }
            e.sangra = _sangra;
            e.faixa = _faixa;
            e.talas = _talas;
            e.analgesico = (ushort)Mathf.Clamp(Mathf.CeilToInt(_analgesicoFalta), 0, ushort.MaxValue);
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                var pp = (ParteDoCorpo)p;
                e.PorIdade(pp, Mathf.RoundToInt(_idade[p]));
                e.PorSujas(pp, Mathf.RoundToInt(_sujas[p]));
                e.PorDias(pp, _dias[p]);
            }
            e.protegida = _protegida;
            e.limpa = _limpa;
            e.coberta = _coberta;
            e.infeccao = (byte)_infeccao;
            e.origem = (byte)_origem;
            e.infeccaoFalta = Minutos(_infeccaoFalta);
            e.doseFalta = Minutos(_doseFalta);
            e.xarope = Minutos(_xarope);
            e.semente = _semente;
            e.amanheceres = (ushort)Mathf.Clamp(_amanheceres, 0, ushort.MaxValue);
            e.desdeOAmanhecer = Minutos(_desdeOAmanhecer);
            return e;
        }

        static ushort Minutos(float m) => (ushort)Mathf.Clamp(Mathf.CeilToInt(m - 0.001f), 0, ushort.MaxValue);

        void GravarCorpo()
        {
            _proximaGravacaoDoCorpo = Time.time + 1f;
            _corpoMudou = false;
            var e = EstadoAgora();
            if (!e.Equals(Corpo.Value)) Corpo.Value = e;
        }

        void AvisarAoDono(string texto)
        {
            if (_inventario != null && !string.IsNullOrEmpty(texto)) _inventario.AvisarDono(texto);
        }

        // ------------------------------------------------ os golpes ------------------------------------------------

        // no anfitrião, dentro do Dano: a parte atingida perde vida, e o golpe pode abrir uma ferida e quebrar ou torcer o
        // membro. Devolve a parte (sorteada, se não veio) e o que mudou.
        void Ferir(float valor, Vector3 de, TipoDeGolpe tipo, ref ParteDoCorpo parte, Lesao lesao, out bool abriu, out Lesao nova)
        {
            abriu = false;
            nova = Lesao.Nenhuma;
            if (!PartesDoCorpo.Valida(parte)) parte = PartesDoCorpo.SortearParte(PartesDoCorpo.PesosDo(tipo), transform, de);
            if (!PartesDoCorpo.EhMembro(parte)) lesao = Lesao.Nenhuma;   // a cabeça e o tronco não quebram (por enquanto)
            int p = (int)parte;
            _danoDaParte[p] = Mathf.Min(100f, _danoDaParte[p] + Mathf.Max(0f, valor) + DanoNoMembro(lesao));
            // a ferida: a fratura exposta sempre sangra; o resto, pela chance do jeito do golpe
            if (lesao == Lesao.Exposta && _lesao[p] != Lesao.Exposta)
            {
                AbrirFerida(parte, SangramentoDaExposta);
                abriu = true;
            }
            else if (Random.value < ChanceDeFerida(tipo))
            {
                AbrirFerida(parte, Sangramento(tipo));
                abriu = true;
            }
            // a lesão: só a pior fica (a fratura por cima da entorse); a tala de antes solta
            if (lesao > _lesao[p])
            {
                _lesao[p] = lesao;
                _sararFalta[p] = MinutosParaSarar(lesao);
                _talas &= (byte)~Bit(parte);
                nova = lesao;
            }
            GravarCorpo();
        }

        void AbrirFerida(ParteDoCorpo parte, float taxa)
        {
            int p = (int)parte, b = Bit(parte);
            // sangrando de novo na mesma parte: o sangue aumenta um pouco (até um limite)
            _sangramento[p] = (_sangra & b) != 0 ? Mathf.Min(SangramentoMaximo, _sangramento[p] + taxa * 0.5f) : taxa;
            // uma ferida nova começa os dias do zero; um corte por cima da que já estava aberta rasga o curativo, mas não
            // adia os sorteios dela (a etapa 2)
            bool nova = (_sangra & b) == 0 && _feridaFalta[p] <= 0f;
            if (nova)
            {
                _idade[p] = _sujas[p] = 0f;
                _dias[p] = 0;
                _coberta &= (byte)~b;
            }
            _protegida &= (byte)~b;
            _limpa &= (byte)~b;
            _sangra |= (byte)b;
            _faixa &= (byte)~b;   // o curativo rasgou: precisa de outro
            _feridaFalta[p] = MinutosDaFerida;
        }

        // ------------------------------------------------ o tempo ------------------------------------------------

        // no anfitrião, a cada quadro (no lugar da recuperação sozinha de antes): a regeneração dos tratamentos, o
        // sangramento, as feridas fechando, o analgésico passando e as lesões sarando
        void AtualizarCorpo(float dt)
        {
            if (!IsServer || Caido.Value) return;
            // a regeneração do curativo e do kit (segundos reais)
            if (_regenAte > Time.time) _regenSobra += _regenPorSegundo * dt;
            if (_regenSobra >= 0.5f || (_regenAte <= Time.time && _regenSobra > 0f))
            {
                Curar(_regenSobra);
                _regenSobra = 0f;
            }
            if (CorpoAnda)
            {
                AndarOCorpo(dt, dt * CicloDoDia.HorasPorSegundo * 60f);
                // o amanhecer do mundo (as 6 h; o Page Up conta); com o relógio parado (o F1), a reserva das 26 h vividas
                if (CicloDoDia.Amanheceres != _amanhecerVisto)
                {
                    _amanhecerVisto = CicloDoDia.Amanheceres;
                    if (_desdeOAmanhecer >= 12 * 60f) Amanhecer(false);
                }
                // (com o relógio andando, o amanhecer pulado pelas 12 h espera o próximo do mundo: não descola das 6 h)
                else if ((CicloDoDia.Parado || CicloDoDia.Instancia == null) && _desdeOAmanhecer >= 26 * 60f) Amanhecer(false);
            }
            if (_corpoMudou || Time.time >= _proximaGravacaoDoCorpo) GravarCorpo();
        }

        // o corpo andando: dt em segundos reais (o sangramento) e min em minutos do jogo (o resto)
        void AndarOCorpo(float dt, float min)
        {
            // o sangramento: tira da vida geral, de 1 em 1, como a fome
            float taxa = 0f;
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
                if ((_sangra & (1 << p)) != 0) taxa += _sangramento[p] > 0f ? _sangramento[p] : Sangramento(TipoDeGolpe.Nenhum);
            if (taxa > 0f)
            {
                _sangue += taxa * dt;
                if (_sangue >= 1f)
                {
                    float d = Mathf.Floor(_sangue);
                    _sangue -= d;
                    Perder(d, "Sangrou até morrer.");
                    if (Caido.Value) return;
                }
            }
            else _sangue = 0f;
            if (min <= 0f) return;
            _desdeOAmanhecer += min;
            // a etapa 2: a idade de cada ferida aberta, e as horas suja (sem a proteção, sem o spray, sem o antibiótico)
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                int b = 1 << p;
                if (_feridaFalta[p] <= 0f && (_sangra & b) == 0) continue;
                _idade[p] += min / 60f;
                // (o spray sozinho não protege: só prepara a bandagem que vem por cima)
                if ((_protegida & b) == 0 && (_coberta & b) == 0) _sujas[p] += min / 60f;
            }
            AndarAInfeccao(min);
            if (Caido.Value) return;
            // as feridas enfaixadas fecham (a que sangra, não)
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                if (_feridaFalta[p] <= 0f || (_sangra & (1 << p)) != 0) continue;
                _feridaFalta[p] -= min;
                if (_feridaFalta[p] > 0f) continue;
                _feridaFalta[p] = 0f;
                _faixa &= (byte)~(1 << p);
                _protegida &= (byte)~(1 << p);
                _limpa &= (byte)~(1 << p);
                _coberta &= (byte)~(1 << p);
                _idade[p] = _sujas[p] = 0f;
                _dias[p] = 0;
                _corpoMudou = true;
                AvisarAoDono($"A ferida {PartesDoCorpo.NaParte((ParteDoCorpo)p)} fechou");
            }
            // quanto as lesões andam: no tempo cheio enquanto o analgésico age, pela metade depois (conta antes de ele
            // passar, para um passo grande, o "+6 h" do F1, dar certo)
            float comRemedio = Mathf.Min(min, Mathf.Max(0f, _analgesicoFalta));
            float progresso = comRemedio + (min - comRemedio) * 0.5f;
            // o analgésico passando
            bool comLesao = false;
            for (int m = (int)ParteDoCorpo.BracoE; m <= (int)ParteDoCorpo.PernaD; m++) comLesao |= _lesao[m] != Lesao.Nenhuma;
            if (_analgesicoFalta > 0f)
            {
                _analgesicoFalta -= min;
                if (_analgesicoFalta <= 0f)
                {
                    _analgesicoFalta = 0f;
                    _corpoMudou = true;
                    if (comLesao) AvisarAoDono("O analgésico passou: a dor voltou");
                }
            }
            // as lesões com a tala saram (no tempo cheio com o analgésico; sem ele, pela metade)
            for (int m = (int)ParteDoCorpo.BracoE; m <= (int)ParteDoCorpo.PernaD; m++)
            {
                if (_lesao[m] == Lesao.Nenhuma || (_talas & (1 << m)) == 0) continue;
                _sararFalta[m] -= progresso;
                if (_sararFalta[m] > 0f) continue;
                var antes = _lesao[m];
                _lesao[m] = Lesao.Nenhuma;
                _sararFalta[m] = 0f;
                _talas &= (byte)~(1 << m);
                _corpoMudou = true;
                var parte = (ParteDoCorpo)m;
                AvisarAoDono(antes == Lesao.Entorse
                    ? $"O tornozelo {(parte == ParteDoCorpo.PernaE ? "esquerdo" : "direito")} sarou: a tala saiu"
                    : $"O osso {(PartesDoCorpo.EhBraco(parte) ? "do" : "da")} {PartesDoCorpo.Nome(parte)} colou: a tala saiu");
            }
        }

        // ------------------------------------------------ a queda ------------------------------------------------

        /// <summary>
        /// No dono (o <see cref="QuedaDoJogador"/>): caiu de <paramref name="altura"/> metros (a altura que valeu, já com a
        /// água). O anfitrião confere e decide a lesão.
        /// </summary>
        public void PedirQueda(float altura)
        {
            if (!IsOwner || Caido.Value) return;
            if (IsServer) ReceberQueda(altura);
            else QuedaRpc(altura);
        }

        [Rpc(SendTo.Server)]
        void QuedaRpc(float altura, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            ReceberQueda(altura);
        }

        void ReceberQueda(float altura)
        {
            if (float.IsNaN(altura) || float.IsInfinity(altura) || Caido.Value) return;
            if (Time.time - _ultimaQueda < 0.75f) return;
            var passageiro = GetComponent<PassageiroDoCarro>();
            if (passageiro != null && passageiro.NoCarro) return;
            _ultimaQueda = Time.time;
            Cair(Mathf.Clamp(altura, 0f, 100f));
        }

        /// <summary>
        /// No anfitrião: a queda de <paramref name="h"/> metros (05/11/2026, a pedido: "entorse de tornozelo, fratura e
        /// fratura exposta, a depender da altura"). Abaixo de 3 m, nada (o pulo, o teto do carro); de 3 a 4,5 m, a entorse
        /// às vezes (o contêiner, o tornado); de 4,5 a 7 m, a fratura (às vezes no braço que apoiou) ou a entorse (a janela
        /// de cima do sobrado, o 1º andar do prédio); de 7 a 11 m, a fratura exposta ou a fratura (o morro do bunker, o poço
        /// do elevador); de 11 a 16 m, a exposta e mais uma fratura; de 16 m para cima, a morte. Pedro gosta das curas e das
        /// lesões por chance: as faixas se misturam nas bordas.
        /// </summary>
        public void Cair(float h)
        {
            if (!IsServer || !IsSpawned || Caido.Value || !CorpoAnda || h < 3f) return;
            const string causa = "Caiu de muito alto.";
            var perna = Random.value < 0.5f ? ParteDoCorpo.PernaE : ParteDoCorpo.PernaD;
            var outra = perna == ParteDoCorpo.PernaE ? ParteDoCorpo.PernaD : ParteDoCorpo.PernaE;
            var embaixo = transform.position + Vector3.down * 0.5f;
            if (h >= 16f)
            {
                Dano(1000f, embaixo, causa, TipoDeGolpe.Queda, perna, Lesao.Exposta);
                return;
            }
            var parte = perna;
            var lesao = Lesao.Nenhuma;
            float dano;
            bool mais = false;
            if (h < 4.5f)
            {
                float t = (h - 3f) / 1.5f;
                dano = Mathf.Lerp(3f, 8f, t);
                if (Random.value < Mathf.Lerp(0.25f, 0.75f, t)) lesao = Lesao.Entorse;
            }
            else if (h < 7f)
            {
                float t = (h - 4.5f) / 2.5f;
                dano = Mathf.Lerp(10f, 25f, t);
                lesao = Random.value < Mathf.Lerp(0.3f, 0.8f, t) ? Lesao.Fratura : Lesao.Entorse;
                // às vezes ele cai com a mão: quebra o braço
                if (lesao == Lesao.Fratura && Random.value < 0.15f) parte = Random.value < 0.5f ? ParteDoCorpo.BracoE : ParteDoCorpo.BracoD;
            }
            else if (h < 11f)
            {
                float t = (h - 7f) / 4f;
                dano = Mathf.Lerp(25f, 50f, t);
                lesao = Random.value < Mathf.Lerp(0.3f, 0.8f, t) ? Lesao.Exposta : Lesao.Fratura;
            }
            else
            {
                float t = (h - 11f) / 5f;
                dano = Mathf.Lerp(50f, 90f, t);
                lesao = Lesao.Exposta;
                mais = true;
            }
            // a segunda fratura (de 11 m para cima) vai junto, sem outro golpe na tela: o aviso fica o da exposta
            if (mais)
            {
                var p2 = Random.value < 0.6f ? outra : (Random.value < 0.5f ? ParteDoCorpo.BracoE : ParteDoCorpo.BracoD);
                Ferir(5f, embaixo, TipoDeGolpe.Queda, ref p2, Lesao.Fratura, out _, out _);
                dano += 5f;
            }
            Dano(dano, embaixo, causa, TipoDeGolpe.Queda, parte, lesao);
        }

        // ------------------------------------------------ os tratamentos ------------------------------------------------

        /// <summary>No anfitrião: o item pode tratar o corpo agora? (escolhe a parte, se vier <see cref="PartesDoCorpo.Sortear"/>)</summary>
        public bool PodeTratar(Item item, ref ParteDoCorpo parte, out string motivo)
        {
            var e = IsServer ? EstadoAgora() : Corpo.Value;
            if (Caido.Value)
            {
                motivo = "Caído, não dá";
                return false;
            }
            return PartesDoCorpo.PodeTratar(e, Vida.Value, vidaMaxima, item, ref parte, out motivo);
        }

        /// <summary>
        /// No anfitrião (o <see cref="InventarioJogador"/>.TratarRpc, depois de conferir e gastar o item): o tratamento faz
        /// efeito. A atadura estanca a ferida da parte, devolve 30% ao membro e 8 à vida geral, e mais 1,5 por segundo por 5
        /// s; a bandagem de pano, 20%, 3 e 1 por segundo por 5 s; o kit (05/11/2026: "o item superior ... trata o corpo
        /// todo") estanca todas as feridas, devolve 50% a cada membro e 40 à vida geral, e mais 2 por segundo por 10 s; a
        /// tala imobiliza o membro quebrado ou torcido (ele começa a sarar); os comprimidos (6 h do jogo) e o xarope (4 h)
        /// tiram a dor. Devolve o aviso para o dono.
        /// </summary>
        public string Tratar(Item item, ParteDoCorpo parte)
        {
            if (!IsServer || Caido.Value) return null;
            string texto = null;
            switch (item)
            {
                case Item.Atadura:
                case Item.Bandagem:
                case Item.BandagemAntisseptica:
                {
                    if (!PartesDoCorpo.Valida(parte)) return null;
                    int p = (int)parte, b = Bit(parte);
                    bool atadura = item == Item.Atadura, antisseptica = item == Item.BandagemAntisseptica;
                    bool estancou = Estancar(parte);
                    // a proteção até o amanhecer: a antisséptica, ou a comum por cima do spray (só numa ferida)
                    bool ferida = _feridaFalta[p] > 0f;
                    bool protege = ferida && (antisseptica || (_limpa & b) != 0);
                    if (ferida) _faixa |= (byte)b;
                    if (protege) _protegida |= (byte)b;
                    _limpa &= (byte)~b;
                    Melhorar(parte, atadura ? 30f : antisseptica ? 25f : 20f);
                    Curar(atadura ? 8f : antisseptica ? 5f : 3f);
                    Regenerar(atadura ? 1.5f : antisseptica ? 1.2f : 1f, 5f);
                    var nome = PartesDoCorpo.NomeComArtigo(parte);
                    string fim = nome[0] == 'a' ? "a" : "o";
                    texto = $"{Maiuscula(nome)} enfaixad{fim}" + (estancou ? ": parou de sangrar" : "")
                          + (protege ? (estancou ? ", e protegid" : ": protegid") + fim + " até o amanhecer"
                                     : ferida ? " (sem antisséptico: pode infeccionar)" : "");
                    break;
                }
                case Item.SprayAntisseptico:
                {
                    if (!PartesDoCorpo.Valida(parte)) return null;
                    _limpa |= (byte)Bit(parte);
                    var nome = PartesDoCorpo.NomeComArtigo(parte);
                    texto = $"{Maiuscula(nome)} limp{(nome[0] == 'a' ? "a" : "o")}: enfaixe para proteger";
                    break;
                }
                case Item.Antibiotico:
                    texto = TomarAntibiotico();
                    break;
                case Item.KitMedico:
                {
                    bool estancou = false;
                    for (int p = 0; p < PartesDoCorpo.Quantas; p++)
                    {
                        estancou |= Estancar((ParteDoCorpo)p);
                        Melhorar((ParteDoCorpo)p, 50f);
                        // o kit limpa e protege toda ferida (conta como a troca do dia)
                        if (_feridaFalta[p] > 0f)
                        {
                            _faixa |= (byte)(1 << p);
                            _protegida |= (byte)(1 << p);
                            _limpa &= (byte)~(1 << p);
                        }
                    }
                    Curar(40f);
                    Regenerar(2f, 10f);
                    texto = estancou ? "Kit médico: o sangue parou e o corpo foi tratado" : "Kit médico: o corpo foi tratado";
                    break;
                }
                case Item.Tala:
                {
                    if (!PartesDoCorpo.EhMembro(parte)) return null;
                    int m = (int)parte;
                    _talas |= (byte)Bit(parte);
                    texto = _lesao[m] == Lesao.Entorse
                        ? $"Tala {PartesDoCorpo.NaParte(parte)}: o tornozelo fica firme"
                        : $"Tala {PartesDoCorpo.NaParte(parte)}: o osso começa a colar";
                    break;
                }
                case Item.Comprimidos:
                {
                    _analgesicoFalta = Mathf.Min(12 * 60f, _analgesicoFalta + 6 * 60f);
                    // a dor passa, mas o braço quebrado ainda treme a mira (o Pedro tremeu sem saber por quê)
                    var depois = EstadoAgora();
                    var treme = PartesDoCorpo.PorQueTreme(depois);
                    texto = treme != null ? $"O analgésico começou a agir: a dor passou, mas a mira ainda treme ({treme.ToLowerInvariant()})"
                                          : "O analgésico começou a agir: a dor passou";
                    break;
                }
                case Item.Remedio:
                {
                    // o xarope (05/11/2026, a pedido: "tira a febre"): os sintomas somem por 4 h; não cura (o antibiótico cura)
                    _xarope = 4 * 60f;
                    texto = "O xarope baixou a febre por umas 4 h (só o antibiótico cura a infecção)";
                    break;
                }
            }
            GravarCorpo();
            return texto;
        }

        // estanca a ferida que sangra (fica enfaixada); devolve se sangrava
        bool Estancar(ParteDoCorpo parte)
        {
            int p = (int)parte, b = Bit(parte);
            if ((_sangra & b) == 0) return false;
            _sangra &= (byte)~b;
            _faixa |= (byte)b;
            _sangramento[p] = 0f;
            if (_feridaFalta[p] <= 0f) _feridaFalta[p] = MinutosDaFerida;
            return true;
        }

        void Melhorar(ParteDoCorpo parte, float quanto) => _danoDaParte[(int)parte] = Mathf.Max(0f, _danoDaParte[(int)parte] - quanto);

        void Regenerar(float porSegundo, float segundos)
        {
            // dois tratamentos seguidos: fica o mais forte, pelo mais longo
            if (_regenAte <= Time.time) _regenPorSegundo = porSegundo;
            else _regenPorSegundo = Mathf.Max(_regenPorSegundo, porSegundo);
            _regenAte = Mathf.Max(_regenAte, Time.time + segundos);
        }

        static string Maiuscula(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        // ------------------------------------------------ no dono ------------------------------------------------

        // no dono, no GolpeRpc: o aviso do que o golpe fez (a lesão nova primeiro, que é a pior notícia)
        void AvisarDoGolpe(ParteDoCorpo parte, TipoDeGolpe tipo, Lesao nova, bool abriu)
        {
            if (!PartesDoCorpo.Valida(parte)) return;
            Golpeado?.Invoke(parte, tipo, nova, abriu);
            if (nova >= Lesao.Fratura) Efeitos.Som(SomDosCurativos.Estalo, transform.position + Vector3.up * 1f, 0.9f, false);
            string texto = null;
            var nome = PartesDoCorpo.NomeComArtigo(parte);
            if (nova == Lesao.Entorse) texto = $"Torceu o tornozelo {(parte == ParteDoCorpo.PernaE ? "esquerdo" : "direito")}: ponha uma tala";
            else if (nova == Lesao.Fratura) texto = $"Quebrou {nome}: ponha uma tala";
            else if (nova == Lesao.Exposta) texto = $"Fratura exposta {PartesDoCorpo.NaParte(parte)}: estanque o sangue e ponha a tala";
            else if (abriu) texto = $"{Maiuscula(nome)} está sangrando: só a bandagem estanca";
            if (texto != null) InventarioJogador.AvisoLocal(texto);
        }

        // ------------------------------------------------ os testes (F1) ------------------------------------------------

        /// <summary>No anfitrião (o F1): abre uma ferida que sangra na parte, sem tirar vida.</summary>
        public void AbrirFeridaDeTeste(ParteDoCorpo parte, TipoDeGolpe tipo = TipoDeGolpe.Mordida)
        {
            if (!IsServer || Caido.Value || !PartesDoCorpo.Valida(parte)) return;
            AbrirFerida(parte, Sangramento(tipo));
            GravarCorpo();
            GolpeRpc(transform.position, (byte)parte, (byte)tipo, (byte)Lesao.Nenhuma, true);
        }

        /// <summary>No anfitrião (o F1): a lesão no membro (a exposta abre a ferida), sem tirar vida geral.</summary>
        public void LesaoDeTeste(ParteDoCorpo parte, Lesao lesao)
        {
            if (!IsServer || Caido.Value || !PartesDoCorpo.EhMembro(parte)) return;
            bool abriu;
            Lesao nova;
            Ferir(0f, transform.position, TipoDeGolpe.Esmagamento, ref parte, lesao, out abriu, out nova);
            GolpeRpc(transform.position, (byte)parte, (byte)TipoDeGolpe.Esmagamento, (byte)nova, abriu);
        }

        /// <summary>No anfitrião (o F1): a parte a 0 (inutilizada).</summary>
        public void InutilizarDeTeste(ParteDoCorpo parte)
        {
            if (!IsServer || Caido.Value || !PartesDoCorpo.Valida(parte)) return;
            _danoDaParte[(int)parte] = 100f;
            GravarCorpo();
        }

        /// <summary>No anfitrião (o F1): passa horas do jogo no corpo (as feridas fecham, as lesões saram, o analgésico passa).</summary>
        public void PassarHorasNoCorpo(float horas)
        {
            if (!IsServer || Caido.Value) return;
            AndarOCorpo(0f, horas * 60f);
            GravarCorpo();
        }

        /// <summary>O corpo em texto (o F1 e o log dos testes).</summary>
        public string ResumoDoCorpo()
        {
            var e = IsServer ? EstadoAgora() : Corpo.Value;
            var s = new StringBuilder();
            s.Append($"vida {Vida.Value:0.#}");
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                var pp = (ParteDoCorpo)p;
                s.Append($" | {PartesDoCorpo.NomeCurto(pp)} {e.Vida(pp)}");
                if (e.Sangra(pp)) s.Append(IsServer ? $" sangra {_sangramento[p]:0.00}/s" : " sangra");
                if (e.Faixa(pp)) s.Append($" faixa {e.FeridaFalta(pp)}min");
                if (e.LesaoDe(pp) != Lesao.Nenhuma) s.Append($" {PartesDoCorpo.NomeDaLesao(e.LesaoDe(pp))}{(e.Tala(pp) ? " com tala" : "")} {e.SararFalta(pp)}min");
            }
            if (e.analgesico > 0) s.Append($" | analgésico {e.analgesico}min");
            s.Append(ResumoDaInfeccao(e));
            return s.ToString();
        }
    }
}
