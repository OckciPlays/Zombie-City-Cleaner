using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A proteção do jogador (18/11/2026, a etapa 4, a pedido: "encostar perto de uma casa, a gente aperta o botão Ctrl e
    /// pegamos uma proteção no local, apoiamos as costas na parede, podemos atirar sem mirar, apenas com a arma para fora e
    /// podemos mirar colocando apenas a região da cabeça e parte da arma para fora ... isso também deve ocorrer com carros,
    /// pequenos muros, algumas barricadas baixas (quando apertamos ctrl ele abaixaria e colocaria a arma por cima)"; "atirar
    /// com o botão esquerdo é atirar sem mirar, então somente a arma atira, em rajadas aleatórias. quando miramos com o botão
    /// direito, a mira fica precisa, porém é mais risco, mais exposição"). Só no dono; a pose vai pela rede no JogadorRede
    /// (JogadorRede.Protecao.cs).
    /// <list type="bullet">
    /// <item>o Ctrl, a pé, encosta na parede, no carro, no muro ou na barricada mais perto (a até 1,6 m; a grade não serve,
    /// não protege); o Ctrl de novo, correr, pular ou andar para longe dela, sai;</item>
    /// <item>a alta (tapa em pé) e a baixa (tapa agachado: carro, mureta, sacos de areia): na baixa, ele fica agachado;</item>
    /// <item>encostado, a câmera vai para a terceira pessoa (atrás do ombro) e o andar é só ao longo dela, parando na quina;</item>
    /// <item>o botão direito: na baixa, levanta por cima dela; na alta, na quina, inclina o tronco (a câmera da primeira
    /// pessoa sai junto); a mira precisa, a cabeça e a arma de fora;</item>
    /// <item>o botão esquerdo sem mirar: o tiro às cegas, da beira da proteção (por cima da baixa, pela quina da alta),
    /// bem aberto, e a automática em rajadas soltas (o ArmaJogador).</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(101)]
    public class ProtecaoDoJogador : MonoBehaviour
    {
        /// <summary>O jogador daqui está encostado numa proteção.</summary>
        public static bool Encostado { get; private set; }
        /// <summary>Os testes: um Ctrl no próximo quadro.</summary>
        public static bool CtrlNoTeste;
        /// <summary>Os testes: encostado numa proteção baixa (agachado atrás dela).</summary>
        public bool NaBaixa => _encostado && _baixa;
        /// <summary>Os testes: a quina livre (1 a direita, -1 a esquerda, 0 nenhuma).</summary>
        public int Quina => _lado;

        const int MascaraDaProtecao = (1 << 0) | (1 << 11);   // o cenário e os carros (a grade, a 15, não)
        const float AlcanceDoEncostar = 1.6f, Folga = 0.42f;
        // (a baixa vai até 1 m pelo menos: atrás dela, agachado, a cápsula desce a 1,2 m e a cabeça fica atrás)
        const float AlturaAlta = 1.55f, AlturaBaixa = 1.0f, CapsulaNaBaixa = 1.2f;
        const float SaidaDoOlho = 0.42f;

        JogadorRede _rede;
        MovimentoPrimeiraPessoa _mov;
        ArmaJogador _arma;
        CharacterController _cc;

        bool _encostado, _baixa, _agachouAqui;
        Vector3 _normal, _parede;     // a normal da proteção (para o lado do jogador, no plano) e um ponto nela
        float _topo;                  // a altura do topo da baixa (mundo)
        int _lado;                    // a quina livre: 1 a direita (de quem olha para a proteção), -1 a esquerda, 0 nenhuma
        Vector3 _encostarEm;
        float _encostandoAte, _longeDesde = -1f, _semProtecaoDesde = -1f;
        Vector3 _olhoInclinado;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _mov = GetComponent<MovimentoPrimeiraPessoa>();
            _arma = GetComponent<ArmaJogador>();
            _cc = GetComponent<CharacterController>();
        }

        void OnDisable()
        {
            if (_encostado) Sair();
        }

        bool PodeFicar =>
            _rede != null && _rede.IsSpawned && _rede.IsOwner && _mov != null && _mov.enabled && !_mov.Voando && !_mov.Nadando
            && (_rede.Passageiro == null || !_rede.Passageiro.NoCarro) && (_rede.Vida == null || _rede.Vida.Vida.Value > 0f);

        void Update()
        {
            if (_rede == null || !_rede.IsOwner) return;
            if (!PodeFicar)
            {
                if (_encostado) Sair();
                return;
            }
            var teclado = Keyboard.current;
            bool ctrl = MenuOnline.Controla && teclado != null && !SentarNoFogo.Sentado && (teclado.leftCtrlKey.wasPressedThisFrame || teclado.rightCtrlKey.wasPressedThisFrame);
            ctrl |= CtrlNoTeste;   // (os testes pelo MCP)
            CtrlNoTeste = false;
            if (ctrl)
            {
                if (_encostado) Sair();
                else Encostar();
            }
            if (!_encostado) return;
            if (teclado != null && MenuOnline.Controla && (teclado.spaceKey.wasPressedThisFrame || (teclado.leftShiftKey.isPressed && _mov.Correndo)))
            {
                Sair();
                return;
            }
            // chegando nela (meio passo, até 0,25 s): ainda não confere (18/11/2026: a proteção achada a mais de 0,92 m
            // falhava a conferência no mesmo quadro, e ele saía na hora; "parece que quer entrar, porém já volta em pé")
            if (Time.time < _encostandoAte)
            {
                var falta = _encostarEm - transform.position;
                falta.y = 0f;
                _cc.Move(falta * Mathf.Min(1f, Time.deltaTime * 14f));
            }
            // ainda encostado? (a proteção pode ter ido embora: o carro andou, a barricada quebrou). Só sai depois de 0,2 s
            // sem ela: nas quinas e nos carros a linha reta passa ao lado por um quadro
            else if (!AindaNaProtecao(out var bate))
            {
                if (_semProtecaoDesde < 0f) _semProtecaoDesde = Time.time;
                else if (Time.time - _semProtecaoDesde > 0.2f)
                {
                    Sair();
                    return;
                }
            }
            else
            {
                _semProtecaoDesde = -1f;
                _parede = bate.point;
            }
            _lado = QuinaLivre();
            bool mirando = _rede.Mirando.Value;
            // a pose: a baixa agachado (de pé mirando, por cima dela); a alta, inclinando pela quina mirando
            if (_baixa)
            {
                if (mirando && _mov.Agachado) _mov.PedirAbaixar(false);
                else if (!mirando && !_mov.Agachado) _mov.PedirAbaixar(true);
                // agachado atrás dela, bem baixo: a cápsula (o tiro dos NPCs) desce a 1,2 m
                if (!mirando && _mov.Agachado && _cc.height > CapsulaNaBaixa + 0.01f)
                {
                    _cc.height = CapsulaNaBaixa;
                    _cc.center = new Vector3(_cc.center.x, CapsulaNaBaixa * 0.5f, _cc.center.z);
                }
            }
            int inclinar = !_baixa && mirando ? _lado * 100 : !_baixa && _arma != null && Time.time - _arma.UltimoTiroAsCegas < 0.7f ? _lado * 35 : 0;
            if (_rede.Inclinacao.Value != inclinar) _rede.Inclinacao.Value = (sbyte)inclinar;
            // a câmera: atrás do ombro encostado; mirando, a primeira pessoa
            CameraTerceiraPessoa.NaProtecao = !mirando;
            // o tiro às cegas sai da beira da proteção (sem mirar)
            if (_arma != null) _arma.OrigemAsCegas = mirando ? (Vector3?)null : OrigemAsCegas();
        }

        // depois da câmera (100): mirando na quina da alta, o olho sai pela quina
        void LateUpdate()
        {
            if (_rede == null || !_rede.IsOwner || _rede.cameraMundo == null) return;
            var cam = _rede.cameraMundo.transform;
            bool inclinando = _encostado && !_baixa && _lado != 0 && _rede.Mirando.Value && !_rede.EmTerceiraPessoa;
            Vector3 alvo = inclinando ? new Vector3(_lado * SaidaDoOlho, -0.1f, 0f) : Vector3.zero;
            _olhoInclinado = Vector3.MoveTowards(_olhoInclinado, alvo, Time.deltaTime * 2.5f);
            if (!_rede.EmTerceiraPessoa && (inclinando || _olhoInclinado.sqrMagnitude > 1e-6f)) cam.localPosition = _olhoInclinado;
        }

        // ------------------------------------------------------------------ encostar e sair

        void Encostar()
        {
            Vector3 pe = transform.position;
            float melhor = float.MaxValue;
            RaycastHit achada = default;
            bool achou = false;
            for (int k = 0; k < 16; k++)
            {
                float ang = k * Mathf.PI * 2f / 16f;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                if (!Physics.Raycast(pe + Vector3.up * AlturaBaixa, dir, out var h, AlcanceDoEncostar, MascaraDaProtecao, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(h.normal.y) > 0.35f || h.distance >= melhor) continue;
                melhor = h.distance;
                achada = h;
                achou = true;
            }
            if (!achou)
            {
                Avisar("Nada para encostar aqui.");
                return;
            }
            _normal = achada.normal;
            _normal.y = 0f;
            _normal.Normalize();
            _parede = achada.point;
            // alta (tapa em pé) ou baixa (só agachado)
            _baixa = !Physics.Raycast(pe + Vector3.up * AlturaAlta, -_normal, melhor + 0.6f, MascaraDaProtecao, QueryTriggerInteraction.Ignore);
            _topo = pe.y + AlturaBaixa;
            if (_baixa && Physics.Raycast(_parede - _normal * 0.08f + Vector3.up * 2.5f, Vector3.down, out var topo, 3f, MascaraDaProtecao, QueryTriggerInteraction.Ignore))
                _topo = topo.point.y;
            _encostarEm = _parede + _normal * Folga;
            _encostarEm.y = pe.y;
            _encostandoAte = Time.time + 0.25f;
            _semProtecaoDesde = -1f;
            _encostado = Encostado = true;
            _agachouAqui = false;
            if (_baixa && !_mov.Agachado) _agachouAqui = _mov.PedirAbaixar(true);
            _mov.FiltroDoPasso = AoLongoDaProtecao;
            // virado para ela (para o lado de onde vem o perigo)
            float yaw = Mathf.Atan2(-_normal.x, -_normal.z) * Mathf.Rad2Deg;
            _mov.Olhar(yaw, 0f);
            _rede.NaProtecao.Value = (byte)(_baixa ? 2 : 1);
            _lado = QuinaLivre();
        }

        /// <summary>Sai da proteção, se estiver nela (sentando no fogo, 18/11/2026).</summary>
        public void SairDaProtecao()
        {
            if (_encostado) Sair();
        }

        void Sair()
        {
            _encostado = Encostado = false;
            CameraTerceiraPessoa.NaProtecao = false;
            if (_mov != null)
            {
                if (_mov.FiltroDoPasso == (System.Func<Vector3, Vector3>)AoLongoDaProtecao) _mov.FiltroDoPasso = null;
                if (_baixa && _mov.Agachado) _mov.PedirAbaixar(false);
            }
            if (_arma != null) _arma.OrigemAsCegas = null;
            if (_rede != null && _rede.IsSpawned && _rede.IsOwner)
            {
                if (_rede.NaProtecao.Value != 0) _rede.NaProtecao.Value = 0;
                if (_rede.Inclinacao.Value != 0) _rede.Inclinacao.Value = 0;
            }
            _longeDesde = -1f;
        }

        // a proteção ainda está ali: pela normal dela ou na direção do ponto onde ele encostou (perto: até 1,2 m)
        bool AindaNaProtecao(out RaycastHit bate)
        {
            Vector3 olho = transform.position + Vector3.up * AlturaBaixa;
            if (Physics.Raycast(olho, -_normal, out bate, Folga + 0.8f, MascaraDaProtecao, QueryTriggerInteraction.Ignore)) return true;
            Vector3 para = _parede - olho;
            para.y = 0f;
            float d = para.magnitude;
            return d > 0.01f && d < Folga + 0.8f
                && Physics.Raycast(olho, para / d, out bate, d + 0.3f, MascaraDaProtecao, QueryTriggerInteraction.Ignore);
        }

        // o andar encostado: só ao longo dela (parando na quina); andar para longe dela por 0,3 s sai
        Vector3 AoLongoDaProtecao(Vector3 desejada)
        {
            if (!_encostado) return desejada;
            Vector3 aoLongo = Vector3.Cross(Vector3.up, _normal);
            float ao = Vector3.Dot(desejada, aoLongo);
            float fora = Vector3.Dot(desejada, _normal);
            if (fora > 0.6f && fora > Mathf.Abs(ao))
            {
                if (_longeDesde < 0f) _longeDesde = Time.time;
                else if (Time.time - _longeDesde > 0.3f) Sair();
                return Vector3.zero;
            }
            _longeDesde = -1f;
            // na quina: não passa dela (sem a proteção atrás daquele lado, para)
            if (Mathf.Abs(ao) > 0.01f)
            {
                var adiante = transform.position + aoLongo * (Mathf.Sign(ao) * 0.3f) + Vector3.up * AlturaBaixa;
                if (!Physics.Raycast(adiante, -_normal, Folga + 0.5f, MascaraDaProtecao, QueryTriggerInteraction.Ignore)) ao = 0f;
            }
            return aoLongo * ao * (_baixa ? 0.9f : 1f);
        }

        // a quina livre perto (a 0,45 m para o lado, a proteção acaba): de quem olha para ela, 1 a direita, -1 a esquerda
        int QuinaLivre()
        {
            Vector3 aoLongo = Vector3.Cross(Vector3.up, _normal);   // (de quem olha para a proteção, a esquerda)
            float altura = _baixa ? AlturaBaixa : 1.3f;
            bool esquerda = !Physics.Raycast(transform.position + aoLongo * 0.45f + Vector3.up * altura, -_normal, Folga + 0.5f, MascaraDaProtecao, QueryTriggerInteraction.Ignore);
            bool direita = !Physics.Raycast(transform.position - aoLongo * 0.45f + Vector3.up * altura, -_normal, Folga + 0.5f, MascaraDaProtecao, QueryTriggerInteraction.Ignore);
            return direita ? 1 : esquerda ? -1 : 0;
        }

        // de onde sai o tiro às cegas: por cima da baixa, ou pela quina livre da alta (null: no meio da alta, não sai)
        Vector3? OrigemAsCegas()
        {
            if (_baixa) return new Vector3(transform.position.x, _topo + 0.14f, transform.position.z) - _normal * 0.3f;
            if (_lado == 0) return null;
            Vector3 direita = -Vector3.Cross(Vector3.up, _normal);
            return transform.position + direita * (_lado * 0.55f) + Vector3.up * 1.3f - _normal * 0.25f;
        }

        static void Avisar(string texto) => InventarioJogador.AvisoLocal(texto);
    }
}
