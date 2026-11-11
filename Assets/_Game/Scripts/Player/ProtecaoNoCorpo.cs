using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A proteção de sucata no corpo do jogador (11/11/2026, a etapa 3), em todas as máquinas, pela
    /// <see cref="InventarioJogador.Protecao"/> que vem pela rede: as peças do <see cref="PecasDaProtecao"/> presas aos ossos
    /// do corpo (o capacete na cabeça, o colete no peito, as braçadeiras nos antebraços e as caneleiras nas canelas), na pose
    /// do osso (a peça já está no espaço dele), com a forma do corpo (a "Mulher" a 100 no corpo da mulher). Como os
    /// curativos (<see cref="CurativosNoCorpo"/>): no dono, só a sombra na primeira pessoa e inteiras na terceira; refeitas
    /// quando as peças vestidas, a forma ou o personagem mudam. O retrato do inventário põe uma cópia dele
    /// (<see cref="NoRetrato"/>), com o material do retrato. As braçadeiras nos braços de primeira pessoa são do
    /// <see cref="RoupasNosBracos"/>.
    /// </summary>
    [DefaultExecutionOrder(110)]   // depois da câmera de terceira pessoa, que mostra e esconde o corpo do dono
    public class ProtecaoNoCorpo : MonoBehaviour
    {
        /// <summary>O começo do nome de cada peça: a cópia do corpo (o retrato do inventário) acha e tira as que vieram junto.</summary>
        public const string Prefixo = "Proteção: ";

        InventarioJogador _inventario;
        JogadorRede _rede;
        Transform _corpo;
        ZombieCityCleaner.Roupas.Vestimenta _vestimenta;
        Material _material;
        bool _retrato, _aplicado, _visivel;
        int _chave = -1;
        readonly Dictionary<string, Transform> _ossos = new Dictionary<string, Transform>();
        readonly List<SkinnedMeshRenderer> _pecas = new List<SkinnedMeshRenderer>();

        void Awake()
        {
            _inventario = GetComponent<InventarioJogador>();
            _rede = GetComponent<JogadorRede>();
            if (_rede != null) _rede.CorpoMudou += AoMudarCorpo;
        }

        void OnDestroy()
        {
            if (_rede != null) _rede.CorpoMudou -= AoMudarCorpo;
            Limpar();
        }

        /// <summary>
        /// No retrato do inventário (a cópia do corpo, onde este componente é posto): lê a proteção do jogador
        /// (<paramref name="inventario"/>) e usa o material do retrato. Sem sombra.
        /// </summary>
        public void NoRetrato(InventarioJogador inventario, Material material)
        {
            if (_rede != null) _rede.CorpoMudou -= AoMudarCorpo;
            _rede = null;
            _retrato = true;
            _inventario = inventario;
            _material = material;
            _corpo = transform;
            _vestimenta = GetComponent<ZombieCityCleaner.Roupas.Vestimenta>();
            LerOssos();
            Limpar();
            _chave = -1;
        }

        /// <summary>Tira as peças que vieram numa cópia do corpo (Instantiate leva os filhos dos ossos, não a lista).</summary>
        public static void TirarCopiadas(Transform raiz)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || !t.name.StartsWith(Prefixo, System.StringComparison.Ordinal)) continue;
                t.gameObject.SetActive(false);   // (o Destroy só vale no fim do quadro)
                Destroy(t.gameObject);
            }
        }

        // o personagem chegou pela rede (outro corpo): as peças são feitas de novo nele
        void AoMudarCorpo()
        {
            Limpar();
            _corpo = null;
            _vestimenta = null;
            _ossos.Clear();
            _chave = -1;
        }

        bool AcharCorpo()
        {
            if (_corpo != null) return true;
            if (_rede == null || _rede.animadorCorpo == null) return false;
            _corpo = _rede.animadorCorpo.transform;
            _vestimenta = _corpo.GetComponent<ZombieCityCleaner.Roupas.Vestimenta>();
            LerOssos();
            _chave = -1;
            return true;
        }

        void LerOssos()
        {
            _ossos.Clear();
            if (_corpo == null) return;
            foreach (var t in _corpo.GetComponentsInChildren<Transform>(true))
                if (!_ossos.ContainsKey(t.name)) _ossos[t.name] = t;
        }

        void LateUpdate()
        {
            if (_inventario == null || !_inventario.IsSpawned) return;
            if (!_retrato && !AcharCorpo()) return;
            if (_corpo == null) return;
            var material = _retrato ? _material : PecasDaProtecao.Dados != null ? PecasDaProtecao.Dados.material : null;
            if (material == null) return;
            // a chave: as peças vestidas (4 bits) e a forma do corpo
            var p = _inventario.Protecao.Value;
            int chave = 0;
            for (int k = 0; k < Protecao.Quantos; k++)
                if (p.Peca((LugarDaProtecao)k) != Loot.Item.Nada) chave |= 1 << k;
            string corpo = _vestimenta != null ? _vestimenta.corpo : null;
            chave |= (corpo != null ? (corpo.GetHashCode() & 0xFFFFFF) : 0) << 4;
            if (chave != _chave)
            {
                _chave = chave;
                Refazer(p, material, corpo);
                _aplicado = false;
            }
            Aplicar();
        }

        void Refazer(in ProtecaoVestida p, Material material, string corpo)
        {
            Limpar();
            for (int k = 0; k < PecasDaProtecao.Pecas.Length; k++)
            {
                var (objeto, osso, lugar) = PecasDaProtecao.Pecas[k];
                if (p.Peca(lugar) == Loot.Item.Nada) continue;
                var malha = PecasDaProtecao.Peca(k);
                if (malha == null || !_ossos.TryGetValue(osso, out var t)) continue;
                var r = PecasDaProtecao.Por(t, malha, material, Prefixo + objeto, _corpo.gameObject.layer, corpo);
                r.updateWhenOffscreen = _retrato;
                if (_retrato)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                _pecas.Add(r);
            }
        }

        // no dono, como o corpo: só a sombra na primeira pessoa (na camada do dono) e inteiras na terceira
        void Aplicar()
        {
            bool visivel = _retrato || _rede == null || !_rede.IsOwner || _rede.CorpoVisivelNoDono;
            if (_aplicado && visivel == _visivel) return;
            _aplicado = true;
            _visivel = visivel;
            if (_retrato) return;
            foreach (var r in _pecas)
            {
                if (r == null) continue;
                r.shadowCastingMode = visivel ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
                r.renderingLayerMask = visivel ? 1u : JogadorRede.MascaraDoDono;
            }
        }

        void Limpar()
        {
            foreach (var r in _pecas)
            {
                if (r == null) continue;
                r.gameObject.SetActive(false);   // (o Destroy só vale no fim do quadro)
                Destroy(r.gameObject);
            }
            _pecas.Clear();
        }
    }
}
