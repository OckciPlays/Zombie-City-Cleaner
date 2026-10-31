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

        public bool Montando => _kit != 0;

        // o chão que a mira procura (sem o jogador, os braços, os zumbis e os corpos)
        static readonly int MascaraDoChao = ~((1 << 2) | (1 << JogadorRede.CamadaBracos) | (1 << 7) | (1 << 8) | (1 << JogadorRede.CamadaJogador));

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
            _giro = transform.eulerAngles.y + 180f;   // virada para o jogador
            _sombra = new GameObject("Montando: " + Itens.Nome(tipo));
            _sombra.AddComponent<MeshFilter>().sharedMesh = mundo.MalhaDe(tipo);
            _render = _sombra.AddComponent<MeshRenderer>();
            _render.shadowCastingMode = ShadowCastingMode.Off;
            if (_verde == null)
            {
                _verde = Transparente(new Color(0.35f, 0.95f, 0.4f, 0.45f));
                _vermelho = Transparente(new Color(1f, 0.3f, 0.25f, 0.45f));
            }
            _arma.Montando = true;
        }

        public void Parar()
        {
            _kit = 0;
            if (_sombra != null) Destroy(_sombra);
            _sombra = null;
            if (_arma != null) _arma.Montando = false;
        }

        void OnDisable() => Parar();

        void Update()
        {
            if (_kit == 0) return;
            var mundo = ConstrucoesNoMundo.Instancia;
            // o kit saiu da mochila (largado, comido...), a tela abriu ou o jogador entrou no carro: desiste
            if (mundo == null || _inventario.Do(_kit) != _tipo || MenuOnline.Aberto || (_rede.Passageiro != null && _rede.Passageiro.NoCarro))
            {
                Parar();
                return;
            }
            var teclado = Keyboard.current;
            var mouse = Mouse.current;
            if (teclado != null && (teclado.escapeKey.wasPressedThisFrame || teclado.gKey.wasPressedThisFrame) || mouse != null && mouse.rightButton.wasPressedThisFrame)
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
            _cabe = _noChao && mundo.Cabe(_tipo, _ponto, _giro);
            _sombra.SetActive(_noChao);
            _sombra.transform.SetPositionAndRotation(_ponto + Vector3.up * 0.01f, Quaternion.Euler(0f, _giro, 0f));
            _render.sharedMaterial = _cabe ? _verde : _vermelho;
            if (_cabe && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                mundo.PedirMontar(_kit, _ponto, _giro);
                Combat.Efeitos.Som(Combat.Efeitos.Largar, _ponto + Vector3.up * 0.3f, 0.8f, false);
                Parar();
            }
        }

        void OnGUI()
        {
            if (_kit == 0) return;
            float u = Screen.height / 720f;
            var estilo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u), alignment = TextAnchor.MiddleCenter };
            string texto = !_noChao ? $"Montando {Itens.NomeComArtigo(_tipo)}: mire no chão"
                : _cabe ? $"Clique: montar {Itens.NomeComArtigo(_tipo)}   ·   R ou roda: girar   ·   botão direito: desistir"
                : "Não cabe aqui   ·   R ou roda: girar   ·   botão direito: desistir";
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
