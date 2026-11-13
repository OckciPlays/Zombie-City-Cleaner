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
    /// mais devagar de noite). Antes, a boia treme às vezes (as beliscadas): clicar nelas espanta o peixe. Clicar sem
    /// beliscada recolhe a linha (sem perder a isca).</item>
    /// <item><b>A mordida:</b> a boia afunda; o clique em 1,2 s fisga.</item>
    /// <item><b>A puxada:</b> a tensão da linha. Segurando o clique, ele recolhe e a linha estica; o peixe puxa aos
    /// arrancos (mais forte o maior) e cansa. Tensão demais arrebenta a linha; frouxa demais por 2 s, o peixe solta; longe
    /// demais, ele leva a linha. Chegando perto, pegou: o anfitrião confere e põe o peixe na mochila
    /// (<see cref="InventarioJogador.PegouOPeixe"/>), com o XP. O peixe escapando leva a isca.</item>
    /// </list>
    /// Os peixes maiores ficam na água mais funda (o meio do lago, de caiaque). Na margem ou no caiaque (o lugar da frente).
    /// <para>As habilidades (o ramo Pesca): a Paciência (morde 40% mais rápido), a Isca fina (40% de chance de não gastar
    /// a isca), o Peixe grande (mais peixes grandes), a Noite de pesca (a noite e a chuva ajudam mais) e o Mestre pescador
    /// (a truta-dourada, rara, do fundo).</para>
    /// <para>A boia e a linha aparecem para todos: a boia vai pela rede (<see cref="InventarioJogador.BoiaNaRede"/>), e os
    /// outros desenham a linha da vara do corpo (<see cref="PescaVista"/>).</para>
    /// </summary>
    public class PescaJogador : MonoBehaviour
    {
        public static PescaJogador Local { get; private set; }

        public const float AlcanceDoLance = 18f, LanceMinimo = 4f, FundoMinimo = 0.8f;
        /// <summary>A força enche em tanto tempo (s).</summary>
        const float TempoDaForca = 1.1f;
        /// <summary>A janela para fisgar depois da mordida (s).</summary>
        const float JanelaDaMordida = 1.2f;
        /// <summary>A puxada: recolhendo (m/s), quanto o peixe leva solto (m/s por força) e até onde chega para pegar (m).</summary>
        const float Recolhe = 2.0f, Leva = 2.2f, Pegou = 1.3f;

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
        Vector3 _pontoDaBoia, _deOnde, _boiaAgora;
        // as beliscadas da espera: os momentos (s desde o começo da espera) e a que está acontecendo
        readonly float[] _beliscadas = new float[3];
        int _quantasBeliscadas;
        float _beliscandoAte = -1f;
        // a puxada
        float _tensao, _distancia, _distanciaInicial, _puxao, _trocaDoPeixe, _folegoDoPeixe, _folegoTotal, _forca, _frouxa, _proximaCatraca;
        bool _peixePuxando, _segura;
        Vector3 _rumoDoPeixe;
        int _botaoNoQuadro = -1;
        float _proximaRede;
        GUIStyle _estilo, _estiloPequeno;
        float _u;
        static AudioClip _plop, _catraca, _estalo;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _arma = GetComponent<ArmaJogador>();
            _inventario = GetComponent<InventarioJogador>();
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
        /// <summary>Na puxada, segurando o botão: a carretilha recolhe.</summary>
        public bool Recolhendo => _fase == Fase.Puxando && _segura;

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
            }
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
                    IrNosBracos(PuxarId, 0.2f);
                    rapidez = _segura ? 1f : 0.35f;
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
                    if (apertou) Recolher("Recolheu a linha");
                    break;
                case Fase.Esperando:
                case Fase.Beliscando:
                    if (!apertou) break;
                    if (_fase == Fase.Beliscando) Espantar();
                    else Recolher("Recolheu a linha");
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

        void ComecarAEspera(float vezes = 1f)
        {
            _fase = Fase.Esperando;
            _tempo = 0f;
            _espera = SortearEspera() * vezes;
            _quantasBeliscadas = Random.Range(0, 4);
            for (int i = 0; i < _quantasBeliscadas; i++) _beliscadas[i] = Random.Range(2f, Mathf.Max(2.5f, _espera - 1.2f));
            _beliscandoAte = -1f;
            Rede(true);
        }

        void Espantar()
        {
            InventarioJogador.AvisoLocal("Puxou cedo: o peixe se assustou (espere a boia afundar)");
            Plop(_pontoDaBoia, 0.35f);
            AguaNaSuperficie.Onda(_pontoDaBoia, 0.5f);
            ComecarAEspera(1.5f);
        }

        void Fisgar()
        {
            _fase = Fase.Puxando;
            _tempo = 0f;
            _tensao = 0.3f;
            _frouxa = 0f;
            Vector3 eu = new Vector3(transform.position.x, _pontoDaBoia.y, transform.position.z);
            _distancia = _distanciaInicial = Mathf.Max(Pegou + 1f, Vector3.Distance(eu, _pontoDaBoia));
            _rumoDoPeixe = (_pontoDaBoia - eu).normalized;
            // o peixe: a força dos arrancos e quanto tempo de arranco ele aguenta antes de cansar
            (_forca, _folegoTotal) = _peixe switch
            {
                Item.PeixePequeno => (0.35f, 4f),
                Item.PeixeMedio => (0.55f, 7f),
                Item.PeixeGrande => (0.8f, 11f),
                Item.Bagre => (0.7f, 12f),
                _ => (0.95f, 14f),
            };
            _folegoDoPeixe = _folegoTotal;
            _peixePuxando = true;
            _trocaDoPeixe = Random.Range(0.8f, 1.6f);
            Plop(_pontoDaBoia, 0.8f);
            AguaNaSuperficie.Respingo(_pontoDaBoia, 10, 0.6f);
            Rede(true);
        }

        void Escapou(string aviso, bool arrebentou = false)
        {
            bool fina = Random.value < 0.2f * Habilidades.Pontos(_rede, Habilidade.IscaFina);   // (a Isca fina: 20% por ponto)
            _inventario.PegouOPeixe(Item.Nada, !fina);
            InventarioJogador.AvisoLocal(aviso + (fina ? " (a isca ficou no anzol)" : ""));
            if (arrebentou)
            {
                if (_estalo == null) _estalo = GerarEstalo();
                Efeitos.Som(_estalo, _rede.Olho.position, 0.7f, false);
            }
            Recolher();
        }

        void Pegou_()
        {
            bool fina = Random.value < 0.2f * Habilidades.Pontos(_rede, Habilidade.IscaFina);   // (a Isca fina: 20% por ponto)
            _inventario.PegouOPeixe(_peixe, !fina);
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

        void Recolher(string aviso = null)
        {
            if (aviso != null) InventarioJogador.AvisoLocal(aviso);
            _fase = Fase.Parado;
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
                        if (_naTerra)
                        {
                            Recolher("A boia caiu fora da água funda");
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
                    // as beliscadas: a boia treme e afunda um pouco por 0,4 s
                    bool belisca = false;
                    for (int i = 0; i < _quantasBeliscadas; i++)
                        if (_tempo >= _beliscadas[i] && _tempo < _beliscadas[i] + 0.4f) belisca = true;
                    if (belisca && _fase == Fase.Esperando)
                    {
                        _fase = Fase.Beliscando;
                        Plop(_pontoDaBoia, 0.15f);
                        AguaNaSuperficie.Onda(_pontoDaBoia, 0.3f);
                        Rede(true);
                    }
                    else if (!belisca && _fase == Fase.Beliscando)
                    {
                        _fase = Fase.Esperando;
                        Rede(true);
                    }
                    _boiaAgora = _pontoDaBoia + Vector3.up * (Mathf.Sin(Time.time * 2.2f) * 0.02f)
                                 + (belisca ? Vector3.down * (0.025f + 0.015f * Mathf.Sin(_tempo * 40f)) : Vector3.zero);
                    if (_tempo >= _espera)
                    {
                        _fase = Fase.Mordendo;
                        _tempo = 0f;
                        _peixe = SortearPeixe();
                        Plop(_pontoDaBoia, 0.9f);
                        AguaNaSuperficie.Onda(_pontoDaBoia, 0.8f);
                        Rede(true);
                    }
                    break;
                }
                case Fase.Mordendo:
                    // a boia afunda e volta: 1,2 s para fisgar
                    _boiaAgora = _pontoDaBoia + Vector3.down * (0.12f + 0.06f * Mathf.Sin(_tempo * 30f));
                    folga = 0.6f;
                    if (_tempo > JanelaDaMordida)
                    {
                        InventarioJogador.AvisoLocal("O peixe soltou a isca: clique quando a boia afundar");
                        ComecarAEspera(0.6f);
                    }
                    break;
                case Fase.Puxando:
                    if (Puxada(dt)) return;
                    folga = Mathf.Clamp01(1f - _tensao * 1.4f);
                    break;
            }
            _linha.Desenhar(Ponta(), _boiaAgora, folga);
            if (_fase == Fase.Puxando && Time.time >= _proximaRede) Rede(false);
        }

        /// <summary>
        /// A puxada, a cada quadro: o peixe dá arrancos (a força dele, menor quando cansa) e descansa; segurando, a linha
        /// recolhe e estica; solta, ela afrouxa e o peixe leva linha nos arrancos. Devolve se acabou (pegou ou escapou).
        /// </summary>
        bool Puxada(float dt)
        {
            _trocaDoPeixe -= dt;
            if (_trocaDoPeixe <= 0f)
            {
                _peixePuxando = !_peixePuxando;
                _trocaDoPeixe = _peixePuxando ? Random.Range(0.5f, 1.6f) * (_folegoDoPeixe > 0f ? 1f : 0.5f) : Random.Range(0.6f, 1.6f);
            }
            if (_peixePuxando) _folegoDoPeixe = Mathf.Max(0f, _folegoDoPeixe - dt);
            float cansado = Mathf.Lerp(0.45f, 1f, _folegoDoPeixe / Mathf.Max(0.1f, _folegoTotal));
            float alvo = _peixePuxando ? _forca * cansado * (0.85f + 0.15f * Mathf.Sin(_tempo * 9f)) : 0.08f;
            _puxao = Mathf.MoveTowards(_puxao, alvo, dt * 3f);
            if (_segura)
            {
                _distancia -= Recolhe * (1f - 0.65f * _puxao) * dt;
                if (Time.time >= _proximaCatraca)
                {
                    _proximaCatraca = Time.time + 0.075f;
                    if (_catraca == null) _catraca = GerarCatraca();
                    Efeitos.Som(_catraca, _rede.Olho.position, 0.18f, false);
                }
            }
            else if (_puxao > 0.15f) _distancia += _puxao * Leva * dt;
            float tensaoAlvo = _segura ? 0.3f + 0.75f * _puxao : 0.04f + 0.2f * _puxao;
            _tensao = Mathf.MoveTowards(_tensao, tensaoAlvo, (tensaoAlvo > _tensao ? 1.3f : 2.2f) * dt);
            _frouxa = _tensao < 0.1f ? _frouxa + dt : 0f;
            // a boia vai com o peixe: na linha até ele, para os lados nos arrancos
            Vector3 eu = new Vector3(transform.position.x, _pontoDaBoia.y, transform.position.z);
            Vector3 lado = Vector3.Cross(Vector3.up, _rumoDoPeixe);
            _boiaAgora = eu + _rumoDoPeixe * _distancia + lado * (Mathf.Sin(_tempo * 2.7f) * 0.7f * _puxao)
                         + Vector3.down * (0.04f + 0.08f * _puxao);
            if (_tensao >= 1f) { Escapou("A linha arrebentou: o peixe foi embora", true); return true; }
            if (_frouxa > 2.2f) { Escapou("A linha afrouxou: o peixe soltou"); return true; }
            if (_distancia > _distanciaInicial + 7f) { Escapou("O peixe levou a linha longe e escapou"); return true; }
            if (_tempo > 60f) { Escapou("Demorou demais: o peixe escapou"); return true; }
            if (_distancia <= Pegou) { Pegou_(); return true; }
            return false;
        }

        // a ponta da vara: a do corpo, na terceira pessoa; a dos braços, na primeira (onde ela aparece na tela)
        Vector3 Ponta()
        {
            var terceira = _rede.TerceiraPessoa;
            if (terceira != null && terceira.Ativa && LinhaDaPesca.PontaNoCorpo(_rede, out var noCorpo)) return noCorpo;
            return LinhaDaPesca.PontaNaTela(_rede, _arma);
        }

        // a boia para os outros (xyz, o ponto; w, a fase); no puxar, dez vezes por segundo
        void Rede(bool agora)
        {
            if (_inventario == null || !_inventario.IsSpawned) return;
            _proximaRede = Time.time + 0.1f;
            var f = _fase == Fase.Carregando ? Fase.Parado : _fase;
            Vector3 p = _fase == Fase.Puxando ? _boiaAgora : _pontoDaBoia;
            var v = f == Fase.Parado ? Vector4.zero : new Vector4(p.x, p.y, p.z, (float)f);
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

        // a catraca do molinete: um tique seco e curto
        static AudioClip GerarCatraca()
        {
            const int taxa = 22050;
            int n = taxa / 40;
            var dados = new float[n];
            var r = new System.Random(5);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)taxa;
                dados[i] = ((float)(r.NextDouble() * 2 - 1) * 0.5f + Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.5f) * Mathf.Exp(-t * 400f);
            }
            return SomGerado.Clip("Catraca", dados, taxa);
        }

        // a linha arrebentando: um estalo e um zunido que cai
        static AudioClip GerarEstalo()
        {
            const int taxa = 22050;
            int n = taxa / 3;
            var dados = new float[n];
            var r = new System.Random(9);
            float fase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)taxa;
                fase += 2f * Mathf.PI * Mathf.Lerp(1800f, 300f, Mathf.Clamp01(t * 4f)) / taxa;
                dados[i] = (float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-t * 90f) * 0.8f + Mathf.Sin(fase) * Mathf.Exp(-t * 9f) * 0.4f;
            }
            return SomGerado.Clip("Linha arrebentando", dados, taxa);
        }

        // ------------------------------------------------------------------ a tela

        void OnGUI()
        {
            if (_fase == Fase.Parado || _rede == null || !_rede.IsOwner || MenuOnline.Aberto || MenuOnline.Cinema) return;
            if (Event.current.type != EventType.Repaint) return;
            float u = Screen.height / 720f;
            if (_estilo == null || !Mathf.Approximately(u, _u))
            {
                _u = u;
                _estilo = new GUIStyle(EstilosDoHud.Rotulo(Mathf.RoundToInt(15 * u), FontStyle.Bold, TextAnchor.MiddleCenter));
                _estiloPequeno = new GUIStyle(EstilosDoHud.Rotulo(Mathf.RoundToInt(12 * u), FontStyle.Bold, TextAnchor.MiddleCenter));
            }
            float cx = Screen.width * 0.5f, y = Screen.height * 0.5f + 90 * u;
            var claro = new Color(0.95f, 0.93f, 0.86f);
            float w = 260 * u, h = 14 * u, x = cx - w / 2f, yb = y + 28 * u;
            switch (_fase)
            {
                case Fase.Carregando:
                    Texto(new Rect(cx - 300 * u, y, 600 * u, 24 * u), "Solte para lançar", claro, _estilo);
                    Retangulo(new Rect(x, yb, w, h), new Color(0f, 0f, 0f, 0.55f));
                    Retangulo(new Rect(x, yb, w * _carga, h), Color.Lerp(new Color(0.45f, 0.8f, 1f, 0.9f), new Color(1f, 0.85f, 0.3f, 0.95f), _carga));
                    Texto(new Rect(x, yb + h + 2 * u, w, 18 * u), $"{Mathf.Lerp(LanceMinimo, AlcanceDoLance, _carga):0} m", claro, _estiloPequeno);
                    break;
                case Fase.Esperando:
                    Texto(new Rect(cx - 300 * u, y, 600 * u, 24 * u), "Esperando o peixe (clique: recolher a linha)", claro, _estilo);
                    break;
                case Fase.Beliscando:
                    Texto(new Rect(cx - 300 * u, y, 600 * u, 24 * u), "Beliscando... espere a boia afundar", new Color(0.8f, 0.9f, 1f), _estilo);
                    break;
                case Fase.Mordendo:
                    Texto(new Rect(cx - 300 * u, y, 600 * u, 24 * u), "MORDEU! Clique!", new Color(1f, 0.85f, 0.3f), _estilo);
                    break;
                case Fase.Puxando:
                {
                    bool puxando = _puxao > 0.4f;
                    Texto(new Rect(cx - 300 * u, y, 600 * u, 24 * u),
                        puxando ? "O PEIXE ESTÁ PUXANDO: solte!" : "Segure o clique para recolher",
                        puxando ? new Color(1f, 0.55f, 0.25f) : claro, _estilo);
                    // a tensão: frouxa (amarelo), boa (verde), perto de arrebentar (vermelho)
                    Retangulo(new Rect(x, yb, w, h), new Color(0f, 0f, 0f, 0.55f));
                    Retangulo(new Rect(x, yb, w * 0.12f, h), new Color(0.9f, 0.8f, 0.25f, 0.55f));
                    Retangulo(new Rect(x + w * 0.12f, yb, w * 0.73f, h), new Color(0.3f, 0.75f, 0.35f, 0.55f));
                    Retangulo(new Rect(x + w * 0.85f, yb, w * 0.15f, h), new Color(0.9f, 0.25f, 0.2f, 0.7f));
                    float px = x + w * Mathf.Clamp01(_tensao);
                    Retangulo(new Rect(px - 2 * u, yb - 4 * u, 4 * u, h + 8 * u), Color.white);
                    Texto(new Rect(x, yb + h + 2 * u, w, 18 * u), $"TENSÃO DA LINHA · o peixe a {_distancia:0.0} m", claro, _estiloPequeno);
                    break;
                }
            }
        }

        void Texto(Rect r, string t, Color c, GUIStyle estilo)
        {
            if (string.IsNullOrEmpty(t)) return;
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), t, estilo);
            estilo.normal.textColor = c;
            GUI.Label(r, t, estilo);
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
