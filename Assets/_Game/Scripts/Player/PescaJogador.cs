using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A pesca (06/11/2026, a pedido: "fazer o sistema de pesca ... dar um sentido à vara de pesca"; refeita em 07/11/2026,
    /// a pedido: "a linha está saindo fora da ponta da vara e não funcionando muito bem"), no dono. Com a vara na mão e uma
    /// isca na mochila:
    /// <list type="number">
    /// <item><b>O arremesso:</b> segurar o clique enche a força (1,1 s); soltando, a boia voa para onde ele olha, de 4 a
    /// 18 m pela força. Na água funda (pelo menos 0,8 m), ela fica; fora dela, volta.</item>
    /// <item><b>A espera:</b> o peixe morde depois de uns 8 a 22 s (mais rápido de manhã cedo, no fim da tarde e com chuva;
    /// mais devagar de noite). Antes, ele belisca a isca: a boia afunda um pouco e volta, com um "plic" e uma onda leve.
    /// Clicar numa beliscada espanta o peixe; clicar sem beliscada recolhe a linha (sem perder a isca).</item>
    /// <item><b>A mordida:</b> a boia some de uma vez, puxada para baixo e para o lado, com o respingo; o clique na janela
    /// fisga, e a vara dá o tranco para trás. Perdendo a janela, a boia volta à tona e a espera recomeça.</item>
    /// <item><b>A briga</b> (desde 27/11/2026, a etapa 3 da pesca nova: <see cref="BrigaDoPeixe"/>, na tela o
    /// <see cref="UI.CirculoDaPesca"/>): o círculo da água embaixo da mira, com o peixe andando pelo jeito dele e a boia
    /// levada pelo mouse (a câmera e o corpo param). A boia em cima do peixe enche a barra e a carretilha recolhe; longe,
    /// ela esvazia. Cheia, pegou: o anfitrião confere e põe o peixe na mochila (<see cref="InventarioJogador.PegouOPeixe"/>),
    /// com o XP. Vazia (ou em 60 s), escapou, e o peixe leva a isca. No lago, a boia acompanha o peixe numa roda em volta de
    /// onde ele fisgou e vem chegando conforme a barra enche.</item>
    /// </list>
    /// Os peixes maiores ficam na água mais funda (o meio do lago, de caiaque). Na margem ou no caiaque (o lugar da frente).
    /// <para>As habilidades (o ramo Pesca): a Paciência (morde 40% mais rápido), a Isca fina (40% de chance de não gastar
    /// a isca), o Peixe grande (mais peixes grandes), a Noite de pesca (a noite e a chuva ajudam mais) e o Mestre pescador
    /// (a truta-dourada, rara, do fundo).</para>
    /// <para>A boia e a linha aparecem para todos: a boia vai pela rede (<see cref="InventarioJogador.BoiaNaRede"/>), e os
    /// outros desenham a linha da vara do corpo (<see cref="PescaVista"/>).</para>
    /// <para>Desde 27/11/2026 (a pesca nova, a pedido: "Eu quero tirar os textos, não quero deixar nada escrito, ao invés
    /// disso pensei em fazer uma boia que funcione real"), nada escrito: a boia conta tudo. O peixe é sorteado já no começo
    /// da espera, e o jeito dele (<see cref="JeitoDoPeixe"/>) diz quantas beliscadas, quanto a boia afunda em cada uma e o
    /// tempo para fisgar: o maior belisca mais e dá menos tempo. Só ficam a barrinha da força (sem os metros), o "Sem isca
    /// na mochila" e o "Pescou ...!" do anfitrião.</para>
    /// </summary>
    public class PescaJogador : MonoBehaviour
    {
        public static PescaJogador Local { get; private set; }

        public const float AlcanceDoLance = 18f, LanceMinimo = 4f, FundoMinimo = 0.8f;
        /// <summary>A força enche em tanto tempo (s).</summary>
        const float TempoDaForca = 1.1f;
        /// <summary>O tranco da fisgada (s): a vara vai para trás, por cima do ombro, e cai no puxar.</summary>
        public const float DuracaoDoTranco = 0.18f;
        /// <summary>A mordida: a boia some em tanto tempo (s), quanto passa da tona (m) e quanto o peixe a arrasta (m).</summary>
        const float Sumindo = 0.12f, AbaixoDaTona = 0.12f, Arrasto = 0.4f;
        /// <summary>O máximo de beliscadas numa espera (a truta-dourada chega a 6).</summary>
        const int MaxBeliscadas = 8;
        /// <summary>
        /// A força na ponta da vara (de 0 a 1, a <see cref="VaraQueEntorta"/>): esperando, o peso da boia; beliscando, mais o
        /// quanto a boia afundou (a beliscada mais funda); na mordida, forte. Na briga, a fisgada leva a 1, e depois vão os
        /// arrancos do peixe e o quanto a boia está longe dele.
        /// </summary>
        public const float ForcaEsperando = 0.05f, ForcaDaBeliscada = 0.45f, ForcaDaMordida = 0.8f;
        /// <summary>A boia no lago na briga: o raio da roda em volta de onde ele fisgou (m) e até onde ela chega do jogador (m).</summary>
        const float RodaNoLago = 3f, PertoDoJogador = 2.5f;

        /// <summary>As fases da boia (também na rede, para os outros desenharem).</summary>
        public enum Fase : byte { Parado, Voando, Esperando, Beliscando, Mordendo, Puxando, Carregando }

        JogadorRede _rede;
        ArmaJogador _arma;
        InventarioJogador _inventario;
        readonly LinhaDaPesca _linha = new LinhaDaPesca();
        Fase _fase;
        float _tempo, _carga, _espera, _voo, _altura;
        bool _naTerra;
        Item _peixe;
        JeitoDoPeixe _jeito;
        Vector3 _pontoDaBoia, _deOnde, _boiaAgora;
        // as beliscadas da espera: quando começa cada uma (s desde o começo da espera), quanto dura e quanto da boia afunda
        readonly float[] _beliscadas = new float[MaxBeliscadas], _duracaoDaBeliscada = new float[MaxBeliscadas], _funduraDaBeliscada = new float[MaxBeliscadas];
        int _quantasBeliscadas;
        // o bagre arrasta a boia na beliscada, e todo peixe na mordida: para este lado (no plano da água)
        Vector3 _arrasto;
        float _trancoAte = -1f;
        // a boia pulando (o peixe espantado) e voltando à tona (a mordida perdida): desde quando e de que fundura (m)
        float _puloEm = -10f, _voltaEm = -10f, _voltaDe;
        // a beliscada de agora: quanto da boia está embaixo (0 a 1), para a vara
        float _afundadaAgora;
        // a vara dos braços que entorta (a etapa 2) e o último rumo da linha nela (para ela assentar com a linha recolhida)
        VaraQueEntorta _dobraDosBracos;
        Vector3 _rumoDosBracos = Vector3.down;
        // a puxada
        // a briga no círculo (a etapa 3): onde ele fisgou e o ponto com água mais perto do jogador, até onde a boia vem
        BrigaDoPeixe _briga;
        Vector3 _ondeFisgou, _pertoNaAgua;
        float _proximaCatraca;
        bool _segura;
        MovimentoPrimeiraPessoa _movimento;
        int _botaoNoQuadro = -1;
        float _proximaRede;
        static AudioClip _plop;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _arma = GetComponent<ArmaJogador>();
            _inventario = GetComponent<InventarioJogador>();
            _movimento = GetComponent<MovimentoPrimeiraPessoa>();
        }

        void OnEnable() => Local = this;

        void OnDisable()
        {
            if (Local == this) Local = null;
            Recolher();
        }

        void OnDestroy() => _linha.Destruir();

        public bool Pescando => _fase != Fase.Parado;
        /// <summary>A fase de agora (com o carregar da força, que não vai pela rede): para os braços e o corpo com a vara.</summary>
        public Fase FaseAgora => _fase;
        /// <summary>A força do lance carregando (0 a 1).</summary>
        public float Carga => _fase == Fase.Carregando ? _carga : 0f;
        /// <summary>Na briga, com a boia em cima do peixe: a carretilha recolhe (a mão gira a manivela rápido).</summary>
        public bool Recolhendo => _fase == Fase.Puxando && _briga != null && _briga.NoPeixe;
        /// <summary>A briga de agora (null fora dela): para os testes e a tela.</summary>
        public BrigaDoPeixe Briga => _fase == Fase.Puxando ? _briga : null;
        /// <summary>A boia como ela está desenhada agora (no mundo), para a vara entortar para o lado dela.</summary>
        public Vector3 BoiaAgora => _boiaAgora;

        /// <summary>A força na ponta da vara agora (0 a 1; veja <see cref="ForcaEsperando"/>).</summary>
        public float ForcaNaVara => _fase switch
        {
            Fase.Voando or Fase.Esperando => ForcaEsperando,
            Fase.Beliscando => ForcaEsperando + ForcaDaBeliscada * _afundadaAgora,
            Fase.Mordendo => ForcaDaMordida,
            Fase.Puxando => Time.time < _trancoAte ? 1f : _briga != null ? Mathf.Clamp01(0.3f + 0.4f * _briga.Arranco + 0.3f * _briga.Longe) : 0.3f,
            _ => 0f,
        };

        // ------------------------------------------------------------------ os braços com a vara (07/11/2026)

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int LancarId = Animator.StringToHash("Lancar");
        static readonly int RecolherId = Animator.StringToHash("Recolher");
        static readonly int PuxarId = Animator.StringToHash("Puxar");
        static readonly int MovimentoId = Animator.StringToHash("Movimento");
        static readonly int SacarId = Animator.StringToHash("Sacar");
        Animator _bracos;
        GameObject _bracosVistos;
        int _estadoDosBracos;
        Fase _faseDosBracos;
        float _recolherAte = -1f;

        /// <summary>
        /// Os braços de primeira pessoa com a vara nova (ArmaJogador.BracosDaVara), a cada quadro, no dono: segurando (a
        /// árvore parada e andando, o "Movimento"), carregando (a vara vai para trás com a força: o Lancar parado no
        /// preparo), lançando (o Lancar segue dali), puxando o peixe (o Puxar; segurando o botão, a mão gira a manivela, e
        /// solto, devagar) e, quando a linha volta, a mão recolhe um pouco (o Recolher).
        /// </summary>
        void LateUpdate()
        {
            if (_rede == null || !_rede.IsOwner) return;
            var b = _arma != null ? _arma.BracosDaVara : null;
            if (b == null)
            {
                _bracosVistos = null;
                return;
            }
            if (b != _bracosVistos)
            {
                _bracosVistos = b;
                _bracos = b.GetComponentInChildren<Animator>(true);
                _estadoDosBracos = SacarId;
                _dobraDosBracos = VaraQueEntorta.De(b.transform);
            }
            EntortarOsBracos();
            if (_bracos == null || !_bracos.isActiveAndEnabled) return;
            _bracos.SetFloat(SpeedId, _rede.Velocidade);
            var fase = _fase;
            if (fase == Fase.Parado && _faseDosBracos != Fase.Parado && _faseDosBracos != Fase.Carregando)
                _recolherAte = Time.time + 0.9f;
            bool soltou = fase == Fase.Voando && _faseDosBracos == Fase.Carregando;
            _faseDosBracos = fase;
            float rapidez = 1f;
            switch (fase)
            {
                case Fase.Carregando:
                    _estadoDosBracos = LancarId;
                    _bracos.Play(LancarId, 0, Mathf.Clamp01(_carga) * (VaraNoCorpo.LancarPreparo / VaraNoCorpo.LancarDuracao));
                    rapidez = 0f;
                    break;
                case Fase.Voando:
                    if (soltou) _bracos.Play(LancarId, 0, VaraNoCorpo.LancarPreparo / VaraNoCorpo.LancarDuracao);
                    else if (_estadoDosBracos != LancarId) IrNosBracos(LancarId, 0.08f);
                    break;
                case Fase.Puxando:
                    if (Time.time < _trancoAte)
                    {
                        // a fisgada: o tranco, a vara para trás por cima do ombro (o começo do Lancar até o alto do preparo,
                        // tocado à mão em 0,18 s), e daí o Puxar
                        float k = 1f - (_trancoAte - Time.time) / DuracaoDoTranco;
                        _estadoDosBracos = LancarId;
                        _bracos.Play(LancarId, 0, Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI * 0.5f) * (VaraNoCorpo.LancarPreparo / VaraNoCorpo.LancarDuracao));
                        rapidez = 0f;
                        break;
                    }
                    IrNosBracos(PuxarId, 0.12f);
                    rapidez = Recolhendo ? 1f : 0.35f;
                    break;
                default:
                {
                    // o lance termina antes de voltar a segurar; o Sacar também
                    var agora = _bracos.GetCurrentAnimatorStateInfo(0);
                    if ((agora.shortNameHash == LancarId || agora.shortNameHash == SacarId) && agora.normalizedTime < 0.98f && !_bracos.IsInTransition(0)) break;
                    IrNosBracos(Time.time < _recolherAte ? RecolherId : MovimentoId, 0.2f);
                    break;
                }
            }
            _bracos.speed = rapidez;
        }

        /// <summary>
        /// A vara dos braços entortando (a etapa 2), depois da animação deles: para o lado da linha, que no mundo vai da ponta
        /// (onde ela aparece na tela) até a boia; os braços são desenhados por outra câmera, perto da origem, e o rumo passa
        /// para lá pelo olhar (o mesmo na tela). Sem a linha, ela assenta de volta, reta.
        /// </summary>
        void EntortarOsBracos()
        {
            if (_dobraDosBracos == null) return;
            if (_fase != Fase.Parado && _fase != Fase.Carregando)
            {
                Vector3 rumo = _boiaAgora - Ponta();
                var mundo = _rede.cameraMundo;
                var bracos = _rede.cameraBracos;
                if (mundo != null && bracos != null) rumo = bracos.transform.TransformDirection(mundo.transform.InverseTransformDirection(rumo));
                if (rumo.sqrMagnitude > 1e-6f) _rumoDosBracos = rumo;
            }
            _dobraDosBracos.Aplicar(ForcaNaVara, _rumoDosBracos, Time.deltaTime);
        }

        void IrNosBracos(int estado, float duracao)
        {
            var s = _bracos.IsInTransition(0) ? _bracos.GetNextAnimatorStateInfo(0) : _bracos.GetCurrentAnimatorStateInfo(0);
            _estadoDosBracos = estado;
            if (s.shortNameHash == estado) return;
            _bracos.CrossFadeInFixedTime(estado, duracao, 0);
        }

        /// <summary>
        /// O botão de atirar com a vara na mão (o ArmaJogador chama a cada quadro): o aperto, o segurar e o soltar. Sem a
        /// chamada num quadro (o menu aberto, a mão em outra coisa), conta como solto.
        /// </summary>
        public void Botao(bool apertou, bool segura, bool soltou)
        {
            _botaoNoQuadro = Time.frameCount;
            _segura = segura;
            switch (_fase)
            {
                case Fase.Parado:
                    if (apertou) ComecarACarregar();
                    break;
                case Fase.Carregando:
                    if (soltou || !segura) Lancar();
                    break;
                case Fase.Voando:
                    if (apertou) Recolher();
                    break;
                case Fase.Esperando:
                case Fase.Beliscando:
                    if (!apertou) break;
                    if (_fase == Fase.Beliscando) Espantar();
                    else Recolher();
                    break;
                case Fase.Mordendo:
                    if (apertou) Fisgar();
                    break;
            }
        }

        /// <summary>O clique de uma vez (o aperto e o soltar no mesmo quadro).</summary>
        public void Clicar() => Botao(true, false, true);

        // ------------------------------------------------------------------ o arremesso

        void ComecarACarregar()
        {
            if (_inventario == null || _inventario.Quantos(Item.Isca) <= 0)
            {
                InventarioJogador.AvisoLocal("Sem isca na mochila (o milho vira isca: CRIAR)");
                return;
            }
            _fase = Fase.Carregando;
            _carga = 0f;
            _tempo = 0f;
        }

        void Lancar()
        {
            var olho = _rede.Olho;
            Vector3 plano = new Vector3(olho.forward.x, 0f, olho.forward.z);
            if (plano.sqrMagnitude < 0.01f) plano = transform.forward;
            plano.Normalize();
            float distancia = Mathf.Lerp(LanceMinimo, AlcanceDoLance, _carga);
            var alvo = olho.position + plano * distancia;
            _naTerra = true;
            // o chão (ou a água) embaixo do alvo, e o que estiver no caminho
            if (Physics.Linecast(olho.position, alvo, out var bateu, Mascara, QueryTriggerInteraction.Ignore))
                alvo = bateu.point - plano * 0.2f;
            if (Agua.Nivel(alvo, out float nivel) && Agua.Fundura(new Vector3(alvo.x, nivel, alvo.z)) >= FundoMinimo)
            {
                alvo.y = nivel;
                _naTerra = false;
            }
            else if (Physics.Raycast(alvo + Vector3.up * 3f, Vector3.down, out var chao, 30f, Mascara, QueryTriggerInteraction.Ignore))
                alvo.y = chao.point.y;
            else if (Agua.Nivel(alvo, out nivel)) alvo.y = nivel;
            _pontoDaBoia = alvo;
            _deOnde = Ponta();
            _voo = 0.35f + distancia * 0.035f;
            _altura = 0.8f + distancia * 0.12f;
            _fase = Fase.Voando;
            _tempo = 0f;
            Efeitos.Som(Efeitos.SocoVento, olho.position, 0.35f + 0.35f * _carga, false);
            Rede(true);
        }

        // o que segura a boia no voo e o chão: tudo menos o jogador, os zumbis, os corpos, a água e os gatilhos
        const int Mascara = ~((1 << 2) | (1 << 4) | (1 << 6) | (1 << 10) | (1 << 11) | (1 << 12));

        // ------------------------------------------------------------------ a espera, a mordida

        /// <summary>
        /// A espera: o peixe já é sorteado aqui (para as beliscadas serem dele), e o jeito dele diz quantas beliscadas vêm, quanto
        /// cada uma afunda a boia e quanto dura. Elas ficam espalhadas pela espera, uma em cada pedaço dela, e a última acaba
        /// pelo menos 1,4 s antes da mordida (a mordida não se mistura com uma beliscada). A espera cresce, se precisar, para
        /// caber as beliscadas do peixe mais nervoso.
        /// </summary>
        void ComecarAEspera(float vezes = 1f)
        {
            _fase = Fase.Esperando;
            _tempo = 0f;
            _peixe = SortearPeixe();
            _jeito = JeitoDoPeixe.De(_peixe);
            _quantasBeliscadas = Mathf.Clamp(Random.Range(_jeito.beliscadasMin, _jeito.beliscadasMax + 1), 0, MaxBeliscadas);
            _espera = Mathf.Max(SortearEspera() * vezes, 3.5f + 1.8f * _quantasBeliscadas);
            float inicio = Mathf.Min(2f, _espera * 0.25f), fim = _espera - 1.1f;
            float pedaco = (fim - inicio) / Mathf.Max(1, _quantasBeliscadas);
            for (int i = 0; i < _quantasBeliscadas; i++)
            {
                _duracaoDaBeliscada[i] = Random.Range(_jeito.duracaoMin, _jeito.duracaoMax);
                _funduraDaBeliscada[i] = Random.Range(_jeito.afundaMin, _jeito.afundaMax);
                _beliscadas[i] = inicio + pedaco * i + Random.Range(0f, Mathf.Max(0f, pedaco - _duracaoDaBeliscada[i] - 0.3f));
            }
            // o lado para onde o peixe leva a boia: mais para longe do jogador que para perto
            Vector3 longe = _pontoDaBoia - transform.position;
            longe.y = 0f;
            longe = longe.sqrMagnitude > 0.01f ? longe.normalized : transform.forward;
            _arrasto = (longe * Random.Range(0.3f, 1f) + Vector3.Cross(Vector3.up, longe) * Random.Range(-1f, 1f)).normalized;
            Rede(true);
        }

        /// <summary>
        /// O clique numa beliscada: o peixe se assusta (a espera recomeça, mais longa), sem texto: a boia dá um pulo, vem um
        /// pouco para perto (a linha puxou) e a água mexe.
        /// </summary>
        void Espantar()
        {
            Vector3 perto = transform.position - _pontoDaBoia;
            perto.y = 0f;
            if (perto.sqrMagnitude > 1f)
            {
                var novo = _pontoDaBoia + perto.normalized * 0.3f;
                if (Agua.Nivel(novo, out float nivel) && Agua.Fundura(new Vector3(novo.x, nivel, novo.z)) >= FundoMinimo * 0.5f)
                    _pontoDaBoia = new Vector3(novo.x, nivel, novo.z);
            }
            _puloEm = Time.time;
            Plop(_pontoDaBoia, 0.3f);
            AguaNaSuperficie.Onda(_pontoDaBoia, 0.5f);
            ComecarAEspera(1.5f);
        }

        void Fisgar()
        {
            _fase = Fase.Puxando;
            _tempo = 0f;
            _trancoAte = Time.time + DuracaoDoTranco;
            // a boia estava embaixo d'água, arrastada pelo peixe: a briga começa dali
            var arrastada = new Vector3(_boiaAgora.x, _pontoDaBoia.y, _boiaAgora.z);
            if (Agua.Fundura(arrastada) >= FundoMinimo * 0.5f) _pontoDaBoia = arrastada;
            // a briga no círculo, pelo jeito do peixe; a câmera e o corpo param até ela acabar
            _briga = new BrigaDoPeixe(_jeito ?? JeitoDoPeixe.De(_peixe));
            _ondeFisgou = _pontoDaBoia;
            _pertoNaAgua = PertoNaAgua();
            if (_movimento != null) _movimento.NaBriga = true;
            Plop(_pontoDaBoia, 0.8f);
            AguaNaSuperficie.Respingo(_pontoDaBoia, 10, 0.6f);
            Rede(true);
        }

        // o peixe escapou (sem texto: ele foge do círculo e a linha volta); leva a isca, menos com a Isca fina
        void Escapou()
        {
            bool fina = Random.value < 0.2f * Habilidades.Pontos(_rede, Habilidade.IscaFina);   // (a Isca fina: 20% por ponto)
            _inventario.PegouOPeixe(Item.Nada, !fina);
            if (_briga != null) CirculoDaPesca.Acabou(_briga);
            AguaNaSuperficie.Onda(_boiaAgora, 0.6f);
            Recolher();
        }

        void Pegou_()
        {
            bool fina = Random.value < 0.2f * Habilidades.Pontos(_rede, Habilidade.IscaFina);   // (a Isca fina: 20% por ponto)
            _inventario.PegouOPeixe(_peixe, !fina);
            if (_briga != null) CirculoDaPesca.Acabou(_briga);   // (o peixe salta do círculo)
            AguaNaSuperficie.Respingo(_boiaAgora, 14, 0.8f);
            Plop(_boiaAgora, 0.9f);
            Recolher();
        }

        // ------------------------------------------------------------------ o peixe: qual e quando

        /// <summary>
        /// O peixe que morde, pela fundura onde a boia está (07/11/2026, a pedido: os maiores no meio do lago): de 1 a 4 m de
        /// fundo, os pequenos ficam mais raros e os grandes mais comuns. O Peixe grande puxa para os grandes; o Mestre
        /// pescador pode pegar a truta-dourada (mais na água funda). Desde 12/11/2026 (a etapa 3 da fauna, os peixes
        /// americanos), o bagre: no fundo, e à noite bem mais (de 3% no raso de dia a ~30% no fundo de noite).
        /// </summary>
        Item SortearPeixe()
        {
            float fundo = Mathf.Clamp01((Agua.Fundura(_pontoDaBoia) - 1f) / 3f);
            int grande = Habilidades.Pontos(_rede, Habilidade.PeixeGrande);
            if (Habilidades.Tem(_rede, Habilidade.MestrePescador) && Random.value < Mathf.Lerp(0.02f, 0.08f, fundo)) return Item.PeixeDourado;
            if (Random.value < Mathf.Lerp(0.03f, 0.17f, fundo) * (World.CicloDoDia.Noite > 0.5f ? 1.8f : 0.7f)) return Item.Bagre;
            float p = 0.55f - 0.30f * fundo, g = 0.12f + 0.25f * fundo;
            p *= 1f - 0.15f * grande;   // (o Peixe grande: com 2 pontos, 0,7 e 1,6)
            g *= 1f + 0.3f * grande;
            float m = Mathf.Max(0.1f, 1f - p - g);
            float r = Random.value * (p + m + g);
            return r < p ? Item.PeixePequeno : r < p + m ? Item.PeixeMedio : Item.PeixeGrande;
        }

        /// <summary>
        /// Quanto o peixe demora (s): de 8 a 22; de manhã cedo (das 5h às 8h30) e no fim da tarde (das 17h às 20h), 40% mais
        /// rápido; de noite, 40% mais devagar (com a Noite de pesca, 20% mais rápido); com chuva, 20% mais rápido (com a Noite
        /// de pesca, 35%); a Paciência, 20% mais rápido por ponto.
        /// </summary>
        float SortearEspera()
        {
            float t = Random.Range(8f, 22f);
            float h = CicloDoDia.Hora;
            bool noitePesca = Habilidades.Tem(_rede, Habilidade.NoiteDePesca);
            if ((h >= 5f && h < 8.5f) || (h >= 17f && h < 20f)) t *= 0.6f;
            else if (CicloDoDia.Noite > 0.5f) t *= noitePesca ? 0.8f : 1.4f;
            if (Tempo.Chuva > 0.3f) t *= noitePesca ? 0.65f : 0.8f;
            t *= 1f - 0.2f * Habilidades.Pontos(_rede, Habilidade.Paciencia);
            return t;
        }

        // ------------------------------------------------------------------ a cada quadro

        void Recolher()
        {
            _fase = Fase.Parado;
            _briga = null;
            if (_movimento != null) _movimento.NaBriga = false;
            _linha.Esconder();
            Rede(true);
        }

        void Update()
        {
            if (_rede == null || !_rede.IsOwner) return;
            if (_fase == Fase.Parado) return;
            // trocou de item, saiu de perto ou caiu: a linha volta
            if (_arma == null || _arma.NaMao != Item.VaraDePesca || !_arma.isActiveAndEnabled
                || (_rede.Vida != null && _rede.Vida.Caido.Value)
                || (_fase != Fase.Carregando && new Vector2(transform.position.x - _pontoDaBoia.x, transform.position.z - _pontoDaBoia.z).magnitude > AlcanceDoLance + 8f))
            {
                Recolher();
                return;
            }
            // sem o botão neste quadro (o menu, a mão ocupada): conta como solto
            if (_botaoNoQuadro < Time.frameCount - 1)
            {
                _segura = false;
                if (_fase == Fase.Carregando)
                {
                    _fase = Fase.Parado;
                    return;
                }
            }
            float dt = Time.deltaTime;
            _tempo += dt;
            float folga = 1f;
            switch (_fase)
            {
                case Fase.Carregando:
                    _carga = Mathf.Clamp01(_tempo / TempoDaForca);
                    return;
                case Fase.Voando:
                {
                    // a boia voa em arco da ponta da vara até onde cai
                    float k = Mathf.Clamp01(_tempo / _voo);
                    _boiaAgora = Vector3.Lerp(_deOnde, _pontoDaBoia, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * _altura);
                    folga = 0.25f;
                    if (k >= 1f)
                    {
                        // fora da água funda, a linha volta (sem texto: a boia bate no chão e vem)
                        if (_naTerra)
                        {
                            Recolher();
                            return;
                        }
                        Plop(_pontoDaBoia, 0.5f);
                        AguaNaSuperficie.Onda(_pontoDaBoia, 0.6f);
                        ComecarAEspera();
                    }
                    break;
                }
                case Fase.Esperando:
                case Fase.Beliscando:
                {
                    // as beliscadas (o jeito do peixe): a boia desce rápido até a fundura da beliscada, fica um instante,
                    // tremendo, e volta com um repique; o bagre a arrasta um pouco para o lado
                    int agora = -1;
                    float s = 0f;
                    for (int i = 0; i < _quantasBeliscadas; i++)
                    {
                        float d = _tempo - _beliscadas[i];
                        if (d >= 0f && d < _duracaoDaBeliscada[i])
                        {
                            agora = i;
                            s = d / _duracaoDaBeliscada[i];
                            break;
                        }
                    }
                    if (agora >= 0 && _fase == Fase.Esperando)
                    {
                        _fase = Fase.Beliscando;
                        Plop(_pontoDaBoia, 0.06f + 0.14f * _funduraDaBeliscada[agora]);
                        AguaNaSuperficie.Onda(_pontoDaBoia, 0.2f + 0.3f * _funduraDaBeliscada[agora]);
                        Rede(true);
                    }
                    else if (agora < 0 && _fase == Fase.Beliscando)
                    {
                        _fase = Fase.Esperando;
                        Rede(true);
                    }
                    _boiaAgora = BoiaNaAgua(_pontoDaBoia);
                    _afundadaAgora = 0f;
                    if (agora >= 0)
                    {
                        float curva = CurvaDaBeliscada(s);
                        _afundadaAgora = _funduraDaBeliscada[agora] * Mathf.Max(0f, curva);
                        _boiaAgora += Vector3.down * (_linha.AcimaDaAgua * _funduraDaBeliscada[agora] * curva * (1f + 0.12f * Mathf.Sin(_tempo * 38f)));
                        if (_jeito.arrasta) _boiaAgora += _arrasto * (0.12f * Mathf.Max(0f, curva));
                        folga = 0.8f;
                    }
                    // o pulo do espanto e a volta à tona da mordida perdida
                    float p = (Time.time - _puloEm) / 0.3f;
                    if (p < 1f) _boiaAgora += Vector3.up * (0.05f * Mathf.Sin(p * Mathf.PI));
                    float v = (Time.time - _voltaEm) / 0.35f;
                    if (v < 1f) _boiaAgora += Vector3.down * (_voltaDe * (1f - Mathf.SmoothStep(0f, 1f, v)));
                    if (_tempo >= _espera)
                    {
                        // a mordida de verdade: a boia some de uma vez
                        _fase = Fase.Mordendo;
                        _tempo = 0f;
                        Plop(_pontoDaBoia, 0.9f);
                        AguaNaSuperficie.Onda(_pontoDaBoia, 0.8f);
                        AguaNaSuperficie.Respingo(_pontoDaBoia, 6, 0.35f);
                        Rede(true);
                    }
                    break;
                }
                case Fase.Mordendo:
                {
                    // a mordida: a boia some em 0,12 s (passa da tona uns 12 cm) e é arrastada uns 40 cm para o lado do peixe,
                    // tremendo embaixo d'água; a janela é a do peixe (o maior dá menos tempo)
                    float k = Mathf.Clamp01(_tempo / Sumindo);
                    float a = Mathf.Clamp01(_tempo / 0.6f);
                    float fundo = (_linha.AcimaDaAgua + AbaixoDaTona) * (1f - (1f - k) * (1f - k));
                    _boiaAgora = _pontoDaBoia + _arrasto * (Arrasto * (1f - (1f - a) * (1f - a)))
                                 + Vector3.down * (fundo + 0.02f * Mathf.Sin(_tempo * 25f) * k);
                    folga = 0.15f;
                    if (_tempo > _jeito.janela)
                    {
                        // perdeu a janela: o peixe soltou, a boia volta à tona de onde ele a deixou, e a espera recomeça
                        var solta = new Vector3(_boiaAgora.x, _pontoDaBoia.y, _boiaAgora.z);
                        if (Agua.Fundura(solta) >= FundoMinimo * 0.5f) _pontoDaBoia = solta;
                        _voltaDe = _pontoDaBoia.y - _boiaAgora.y;
                        _voltaEm = Time.time;
                        Plop(_pontoDaBoia, 0.3f);
                        AguaNaSuperficie.Onda(_pontoDaBoia, 0.45f);
                        ComecarAEspera(0.6f);
                    }
                    break;
                }
                case Fase.Puxando:
                    if (Briga_(dt)) return;
                    folga = 0.08f;
                    break;
            }
            _linha.Desenhar(Ponta(), _boiaAgora, folga);
            if (_fase == Fase.Puxando && Time.time >= _proximaRede) Rede(false);
        }

        /// <summary>
        /// A briga no círculo, a cada quadro (a etapa 3: <see cref="BrigaDoPeixe"/>): depois do tranco da fisgada, o olhar (que
        /// na briga não gira a câmera) leva a boia do círculo. No lago, a boia acompanha o peixe numa roda (o círculo da tela
        /// deitado na água: o x para o lado, o y para longe) em volta de onde ele fisgou, e a roda vem para perto do jogador e
        /// encolhe conforme a barra enche. Com a boia em cima do peixe, a carretilha recolhe (o som do molinete). Devolve se
        /// acabou (pegou ou escapou).
        /// </summary>
        bool Briga_(float dt)
        {
            if (Time.time >= _trancoAte) _briga.Passo(_movimento != null ? _movimento.OlharDaBriga : Vector2.zero, dt);
            if (_briga.NoPeixe && Time.time >= _proximaCatraca)
            {
                _proximaCatraca = Time.time + 0.075f;
                // (20/11/2026) o molinete recolhendo: grãos da fita rebobinando (o sfxjar)
                Efeitos.Som(SonsGravados.Um("Pesca", "molinete_"), _rede.Olho.position, 0.3f, false);
            }
            float perto = Mathf.Clamp01((_briga.Captura - BrigaDoPeixe.Comeco) / (1f - BrigaDoPeixe.Comeco));
            Vector3 centro = Vector3.Lerp(_ondeFisgou, _pertoNaAgua, perto);
            Vector3 longe = centro - transform.position;
            longe.y = 0f;
            longe = longe.sqrMagnitude > 0.01f ? longe.normalized : transform.forward;
            Vector3 lado = Vector3.Cross(Vector3.up, longe);
            float roda = RodaNoLago * Mathf.Lerp(1f, 0.45f, perto);
            Vector3 alvo = centro + (lado * _briga.Peixe.x + longe * _briga.Peixe.y) * roda + Vector3.down * (0.05f + 0.08f * _briga.Arranco);
            _boiaAgora = Vector3.Lerp(_boiaAgora, alvo, 1f - Mathf.Exp(-8f * dt));
            if (_briga.Pegou) { Pegou_(); return true; }
            if (_briga.Escapou) { Escapou(); return true; }
            return false;
        }

        // o ponto mais perto do jogador, na reta até onde ele fisgou, que ainda tem água (a boia não chega na terra)
        Vector3 PertoNaAgua()
        {
            Vector3 eu = new Vector3(transform.position.x, _ondeFisgou.y, transform.position.z);
            Vector3 rumo = eu - _ondeFisgou;
            float total = rumo.magnitude;
            if (total < 0.01f) return _ondeFisgou;
            rumo /= total;
            Vector3 melhor = _ondeFisgou;
            for (float d = 0.5f; d <= total - PertoDoJogador; d += 0.5f)
            {
                var p = _ondeFisgou + rumo * d;
                if (!Agua.Nivel(p, out float nivel) || Agua.Fundura(new Vector3(p.x, nivel, p.z)) < 0.3f) break;
                melhor = new Vector3(p.x, _ondeFisgou.y, p.z);
            }
            return melhor;
        }

        // ------------------------------------------------------------------ a boia

        /// <summary>A boia parada na água: sobe e desce devagar com as ondinhas (pouco, para a beliscada aparecer).</summary>
        public static Vector3 BoiaNaAgua(Vector3 ponto) => ponto + Vector3.up * (Mathf.Sin(Time.time * 2.2f) * 0.012f);

        /// <summary>
        /// Uma beliscada, de 0 (o começo) a 1 (o fim): quanto da fundura dela a boia está embaixo. Desce rápido (o primeiro
        /// quinto), fica, e volta com um repique (passa um pouco da tona, para cima, e assenta).
        /// </summary>
        public static float CurvaDaBeliscada(float s)
        {
            if (s <= 0f || s >= 1f) return 0f;
            if (s < 0.22f) return Mathf.SmoothStep(0f, 1f, s / 0.22f);
            if (s < 0.45f) return 1f;
            float k = (s - 0.45f) / 0.55f;
            return Mathf.Pow(1f - k, 1.5f) * Mathf.Cos(k * Mathf.PI * 1.5f);
        }

        // a ponta da vara: a do corpo, na terceira pessoa; a dos braços, na primeira (onde ela aparece na tela). No vídeo, com a
        // câmera do filme olhando o corpo (27/11/2026, o vídeo 36: o ModoVideo.Olho), também a do corpo: a dos braços saía no
        // ar, na frente do rosto, longe da ponta da vara que a câmera vê
        Vector3 Ponta()
        {
            var terceira = _rede.TerceiraPessoa;
            bool corpoNaTela = (terceira != null && terceira.Ativa) || (Video.ModoVideo.Ligado && Video.ModoVideo.Olho.HasValue);
            if (corpoNaTela && LinhaDaPesca.PontaNoCorpo(_rede, out var noCorpo)) return noCorpo;
            return LinhaDaPesca.PontaNaTela(_rede, _arma);
        }

        // a boia para os outros (xyz, o ponto; w, a fase, e na puxada a força na vara na fração); no puxar, dez vezes por segundo
        void Rede(bool agora)
        {
            if (_inventario == null || !_inventario.IsSpawned) return;
            _proximaRede = Time.time + 0.1f;
            var f = _fase == Fase.Carregando ? Fase.Parado : _fase;
            Vector3 p = _fase == Fase.Puxando ? _boiaAgora : _pontoDaBoia;
            // (27/11/2026, a vara que entorta) na puxada, a força na vara vai junto, na fração do w (quem lê usa o FloorToInt)
            float w = (float)f + (f == Fase.Puxando ? Mathf.Clamp(ForcaNaVara, 0f, 0.99f) : 0f);
            var v = f == Fase.Parado ? Vector4.zero : new Vector4(p.x, p.y, p.z, w);
            if (agora || (v - _inventario.BoiaNaRede.Value).sqrMagnitude > 0.0025f) _inventario.BoiaNaRede.Value = v;
        }

        void Plop(Vector3 onde, float volume)
        {
            if (_plop == null) _plop = GerarPlop();
            Efeitos.Som(_plop, onde, volume, true, 20f);
        }

        /// <summary>Um "plop" na água: um estalo grave que cai de tom, com chiado (também para quem olha).</summary>
        public static AudioClip GerarPlop()
        {
            const int taxa = 22050;
            int n = taxa / 4;
            var dados = new float[n];
            var r = new System.Random(11);
            float filtro = 0f, fase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)taxa;
                fase += 2f * Mathf.PI * Mathf.Lerp(520f, 140f, Mathf.Clamp01(t * 6f)) / taxa;
                filtro = Mathf.Lerp(filtro, (float)(r.NextDouble() * 2 - 1), 0.3f);
                dados[i] = (Mathf.Sin(fase) * 0.7f + filtro * 0.35f) * Mathf.Exp(-t * 18f);
            }
            return SomGerado.Clip("Plop", dados, taxa);
        }

        // ------------------------------------------------------------------ a tela

        /// <summary>
        /// A tela da pesca, sem nada escrito (27/11/2026): carregando, só a barrinha fina da força, embaixo da mira; na briga,
        /// o círculo (<see cref="CirculoDaPesca"/>), e depois dela o fim (o peixe saltando ou fugindo). Esperando, beliscando e
        /// na mordida, nada: a boia conta.
        /// </summary>
        void OnGUI()
        {
            // (gravando um vídeo, nada de HUD: 27/11/2026, o vídeo 36, como as outras telas do jogo)
            if (_rede == null || !_rede.IsOwner || MenuOnline.Aberto || MenuOnline.Cinema || Video.ModoVideo.Ligado) return;
            if (Event.current.type != EventType.Repaint) return;
            if (_fase == Fase.Puxando && _briga != null)
            {
                CirculoDaPesca.Desenhar(_briga);
                return;
            }
            if (CirculoDaPesca.Mostrando) CirculoDaPesca.DesenharFim();
            if (_fase != Fase.Carregando) return;
            float u = Screen.height / 720f;
            float cx = Screen.width * 0.5f, yb = Screen.height * 0.5f + 60 * u;
            float w = 120 * u, h = 4 * u, x = cx - w / 2f;
            Retangulo(new Rect(x - u, yb - u, w + 2 * u, h + 2 * u), new Color(0f, 0f, 0f, 0.45f));
            Retangulo(new Rect(x, yb, w * _carga, h), Color.Lerp(new Color(0.75f, 0.9f, 1f, 0.85f), new Color(1f, 0.85f, 0.35f, 0.95f), _carga));
        }

        static void Retangulo(Rect r, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }
}
