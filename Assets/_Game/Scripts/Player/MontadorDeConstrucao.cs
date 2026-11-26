using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Montar um kit no mundo (31/10/2026: a fogueira, a cama e a barricada), só no dono: pelo menu do botão direito do
    /// kit na tela do inventário ("Montar no chão"), a tela fecha e a sombra da construção acompanha a mira, no chão até
    /// <see cref="ConstrucoesNoMundo.AlcanceDeMontar"/> m: verde onde cabe, vermelha onde não. R (ou a roda) gira, o clique
    /// esquerdo monta (o anfitrião confere e tira o kit da mochila), o direito, G ou ESC desistem. Enquanto isso, a arma
    /// não atira (<see cref="ArmaJogador.Montando"/>).
    /// </summary>
    public class MontadorDeConstrucao : MonoBehaviour
    {
        JogadorRede _rede;
        InventarioJogador _inventario;
        ArmaJogador _arma;
        ushort _kit;
        Item _tipo;
        float _giro;
        bool _cabe, _noChao;
        Vector3 _ponto;
        GameObject _sombra;
        Material _verde, _vermelho;
        MeshRenderer _render;
        MeshRenderer[] _renders;   // a sombra e as partes dela (o rastro da cerca de fogo, 11/11/2026)

        // (25/11/2026, a pedido do Pedro: "todos os itens que vão ser postos no chão, que são feitos para montar, deverão
        // aparecer o seu fantasma para escolher o melhor lugar, poder girar") o clique que escolhe "Montar no chão" no menu da
        // mochila às vezes chega ao Input System um quadro depois, e a peça era montada na hora, sem o fantasma: o clique de
        // montar só vale depois de o botão ser solto e de passar um instante
        bool _esperarSoltar;
        float _comecouEm;
        const float SemCliqueNoComeco = 0.25f;
        // (25/11/2026, a revisão) o último quadro com uma tela do jogo aberta por cima (a mochila, o menu do F1) ou numa cena
        // (o Cinema): o ESC que fecha a tela não desiste da montagem no mesmo quadro
        int _quadroComTela = -10;

        public bool Montando => _kit != 0;

        // o chão que a mira procura (sem o jogador, os braços, os zumbis e os corpos)
        static readonly int MascaraDoChao = ~((1 << 2) | (1 << 16) | (1 << JogadorRede.CamadaBracos) | (1 << 7) | (1 << 8) | (1 << JogadorRede.CamadaJogador));

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _inventario = GetComponent<InventarioJogador>();
            _arma = GetComponent<ArmaJogador>();
        }

        /// <summary>Começa a montar o kit da mochila (o Id dele).</summary>
        public void Comecar(ushort kit)
        {
            var tipo = _inventario.Do(kit);
            var mundo = ConstrucoesNoMundo.Instancia;
            if (!Itens.EhConstrucao(tipo) || mundo == null || mundo.MalhaDe(tipo) == null) return;
            Parar();
            _kit = kit;
            _tipo = tipo;
            _esperarSoltar = true;
            _comecouEm = Time.unscaledTime;
            _giro = transform.eulerAngles.y + ConstrucoesNoMundo.GiroDaSombra(tipo);   // virada para o jogador (as estacas e a cerca de fogo, não: 11/11/2026)
            _sombra = new GameObject("Montando: " + Itens.Nome(tipo));
            _sombra.AddComponent<MeshFilter>().sharedMesh = mundo.MalhaDe(tipo);
            _render = _sombra.AddComponent<MeshRenderer>();
            _render.shadowCastingMode = ShadowCastingMode.Off;
            ConstrucoesNoMundo.ExtrasDoFantasma(tipo, _sombra.transform);   // a cerca de fogo: o rastro de 4 m (11/11/2026)
            _renders = _sombra.GetComponentsInChildren<MeshRenderer>();
            if (_verde == null)
            {
                _verde = Transparente(new Color(0.35f, 0.95f, 0.4f, 0.45f));
                _vermelho = Transparente(new Color(1f, 0.3f, 0.25f, 0.45f));
            }
            _arma.Montando = true;
            MontandoABase = Itens.EhDaBase(tipo) || ConstrucoesNoMundo.PrecisaDoNucleo(tipo);
        }

        /// <summary>
        /// (24/11/2026) O jogador desta máquina está montando uma peça da base (o núcleo, o muro, o portão, a barricada): a área
        /// de cada núcleo aparece no chão (ConstrucoesNoMundo.Base.cs).
        /// </summary>
        public static bool MontandoABase { get; private set; }
        // por que não dá para montar aqui (fora da área de um núcleo), ou null
        string _motivo;

        public void Parar()
        {
            _kit = 0;
            if (_sombra != null) Destroy(_sombra);
            _sombra = null;
            if (_arma != null) _arma.Montando = false;
            MontandoABase = false;
        }

        void OnDisable() => Parar();

        void Update()
        {
            if (_kit == 0) return;
            var mundo = ConstrucoesNoMundo.Instancia;
            // o kit saiu da mochila (largado, comido...), o menu (ESC) abriu ou o jogador entrou no carro: desiste
            if (mundo == null || _inventario.Do(_kit) != _tipo || MenuOnline.Aberto || (_rede.Passageiro != null && _rede.Passageiro.NoCarro))
            {
                Parar();
                return;
            }
            // (25/11/2026, a revisão) com a mochila aberta por cima (o TAB para conferir os materiais), o fantasma fica onde
            // estava e nada da montagem responde: o clique dentro da tela (arrastar, escolher um item) montava a peça onde o
            // fantasma tinha ficado, e o botão direito, o R e a roda da tela desistiam ou giravam. Fechada a tela, o clique de
            // montar só volta depois de o botão ser solto e de passar o instante do começo (o clique que fecha não monta)
            // (e numa cena, o MenuOnline.Cinema, na revisão do mesmo dia: o CRIAR da mochila aberta por cima começa a animação
            // do craft, que fecha a mochila e liga o Cinema; o clique que pula a animação montava a peça, e o "Clique: montar"
            // aparecia por cima da câmera de lado. O mesmo na tela preta dos 5 anos e na passagem do Dia 0)
            if (MenuOnline.ComTela || MenuOnline.Cinema)
            {
                // (na cena, o fantasma some: na câmera de lado do craft, ele aparecia no meio do quadro; ele volta sozinho no
                // primeiro quadro depois dela)
                if (MenuOnline.Cinema && _sombra != null && _sombra.activeSelf) _sombra.SetActive(false);
                _esperarSoltar = true;
                _comecouEm = Time.unscaledTime;
                _quadroComTela = Time.frameCount;
                return;
            }
            var teclado = Keyboard.current;
            var mouse = Mouse.current;
            bool telaAcabouDeFechar = Time.frameCount - _quadroComTela <= 1;
            if (!telaAcabouDeFechar && (teclado != null && (teclado.escapeKey.wasPressedThisFrame || teclado.gKey.wasPressedThisFrame) || mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                Parar();
                return;
            }
            if (teclado != null && teclado.rKey.wasPressedThisFrame) _giro += 45f;
            if (mouse != null)
            {
                float roda = mouse.scroll.ReadValue().y;
                if (roda != 0f) _giro += roda > 0f ? 15f : -15f;
            }
            // o ponto: onde a mira bate no chão, até o alcance
            var olho = _rede.Olho;
            _noChao = Physics.Raycast(olho.position, olho.forward, out var acerto, ConstrucoesNoMundo.AlcanceDeMontar, MascaraDoChao, QueryTriggerInteraction.Ignore)
                      && acerto.normal.y > 0.75f;
            if (_noChao) _ponto = acerto.point;
            else
            {
                // olhando para longe ou para a parede: o chão embaixo do ponto mais longe da mira
                var longe = olho.position + olho.forward * Mathf.Min(3f, ConstrucoesNoMundo.AlcanceDeMontar);
                _noChao = Physics.Raycast(longe + Vector3.up * 0.5f, Vector3.down, out var chao, 3f, MascaraDoChao, QueryTriggerInteraction.Ignore) && chao.normal.y > 0.75f;
                if (_noChao) _ponto = chao.point;
            }
            // só no chão em que ele pisa (não em cima da mesa)
            if (_noChao && !ConstrucoesNoMundo.NoChao(transform.position, _ponto)) _noChao = false;
            // (24/11/2026) o muro e o portão encaixam na ponta de outro (em linha ou na quina): o chão do ponto encaixado
            float giro = _giro;
            if (_noChao)
            {
                var encaixe = _ponto;
                if (mundo.Encaixar(_tipo, ref encaixe, ref giro)
                    && Physics.Raycast(encaixe + Vector3.up * 1.5f, Vector3.down, out var chaoDoEncaixe, 3f, MascaraDoChao, QueryTriggerInteraction.Ignore)
                    && ConstrucoesNoMundo.NoChao(transform.position, chaoDoEncaixe.point))
                    _ponto = chaoDoEncaixe.point;
                else giro = _giro;
            }
            // (24/11/2026) a base só dentro da área de um núcleo (e o núcleo, longe de outro)
            _motivo = _noChao ? mundo.PodeMontarAqui(_tipo, _ponto) : null;
            _cabe = _noChao && _motivo == null && mundo.Cabe(_tipo, _ponto, giro);
            // (25/11/2026, a pedido do Pedro: "colocar o fantasma da muralha") fora do chão (a mira na parede, no alto, num
            // degrau), a sombra não some mais: fica vermelha onde a mira chega, para quem monta ver a peça e para onde ela vai
            bool aVista = _noChao || OndeAMiraChega(olho, out _ponto);
            _sombra.SetActive(aVista);
            _sombra.transform.SetPositionAndRotation(_ponto + Vector3.up * 0.01f, Quaternion.Euler(0f, giro, 0f));
            foreach (var r in _renders) r.sharedMaterial = _cabe ? _verde : _vermelho;
            if (_esperarSoltar && (mouse == null || !mouse.leftButton.isPressed) && Time.unscaledTime - _comecouEm >= SemCliqueNoComeco)
                _esperarSoltar = false;
            if (!_esperarSoltar && _cabe && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                mundo.PedirMontar(_kit, _ponto, giro);
                Combat.Efeitos.Som(Combat.Efeitos.Largar, _ponto + Vector3.up * 0.3f, 0.8f, false);
                Parar();
            }
        }

        // fora do chão: onde a mira chega (no que ela bate, até o alcance, ou 3 m à frente), descido até o chão embaixo dele
        static bool OndeAMiraChega(Transform olho, out Vector3 ponto)
        {
            float alcance = ConstrucoesNoMundo.AlcanceDeMontar;
            ponto = Physics.Raycast(olho.position, olho.forward, out var bate, alcance, MascaraDoChao, QueryTriggerInteraction.Ignore)
                ? bate.point - olho.forward * 0.3f
                : olho.position + olho.forward * Mathf.Min(3f, alcance);
            if (Physics.Raycast(ponto + Vector3.up * 0.3f, Vector3.down, out var chao, 12f, MascaraDoChao, QueryTriggerInteraction.Ignore))
                ponto = chao.point;
            return true;
        }

        void OnGUI()
        {
            // (a revisão, 25/11/2026: com a mochila aberta por cima ou numa cena, a montagem espera, e o "Clique: montar" não aparece)
            if (_kit == 0 || Video.GravacaoNoMundo.SemHud || MenuOnline.ComTela || MenuOnline.Cinema) return;
            float u = Screen.height / 720f;
            var estilo = EstilosDoHud.Rotulo(Mathf.RoundToInt(15 * u), FontStyle.Normal, TextAnchor.MiddleCenter);
            string texto = !_noChao ? $"Montando {Itens.NomeComArtigo(_tipo)}: mire no chão"
                : _cabe ? $"Clique: montar {Itens.NomeComArtigo(_tipo)}   ·   R ou roda: girar   ·   botão direito: desistir"
                : $"{_motivo ?? "Não cabe aqui"}   ·   R ou roda: girar   ·   botão direito: desistir";
            var r = new Rect(0f, Screen.height * 0.62f, Screen.width, 28 * u);
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(r.x + 1.5f * u, r.y + 1.5f * u, r.width, r.height), texto, estilo);
            estilo.normal.textColor = _cabe ? new Color(0.92f, 0.9f, 0.82f) : new Color(1f, 0.6f, 0.5f);
            GUI.Label(r, texto, estilo);
        }

        // um material transparente sem luz (a sombra da construção)
        static Material Transparente(Color cor)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = "Montando" };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", cor);
            return m;
        }
    }
}
