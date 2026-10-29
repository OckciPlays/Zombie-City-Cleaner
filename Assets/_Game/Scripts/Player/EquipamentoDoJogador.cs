using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O que o jogador leva, no corpo dele (28/10/2026, a pedido: a mochila não aparecia nas costas dos jogadores; era a
    /// fatia 2 das mochilas): a mochila, o coldre e as armas penduradas, as mesmas do retrato do inventário
    /// (<see cref="EquipamentoNoCorpo.OQueVeste"/>), em todas as máquinas, pelo inventário que vem pela rede, presas aos
    /// ossos do corpo (acompanham a animação). No dono, como o corpo: só a sombra na primeira pessoa (fora da sombra da
    /// lanterna dele) e inteiras na terceira (<see cref="JogadorRede.CorpoVisivelNoDono"/>). Somem no carro.
    /// </summary>
    [DefaultExecutionOrder(110)]   // depois da câmera de terceira pessoa, que mostra e esconde o corpo do dono
    public class EquipamentoDoJogador : MonoBehaviour
    {
        JogadorRede _rede;
        InventarioJogador _inventario;
        EquipamentoNoCorpo _equipamento;
        Transform _referencia;
        long _vestido = -1;
        bool _aplicado, _visivel, _noCarro;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _inventario = GetComponent<InventarioJogador>();
            _rede.CorpoMudou += AoMudarCorpo;
        }

        void OnDestroy()
        {
            if (_rede != null) _rede.CorpoMudou -= AoMudarCorpo;
        }

        // o personagem chegou pela rede (outro corpo): veste de novo
        void AoMudarCorpo()
        {
            if (_equipamento != null) _equipamento.Limpar();
            _equipamento = null;
            _vestido = -1;
        }

        void LateUpdate()
        {
            if (!_rede.IsSpawned || !_inventario.IsSpawned || _rede.animadorCorpo == null || ItensNoMundo.Instancia == null) return;
            if (_equipamento == null)
            {
                var corpo = _rede.animadorCorpo.gameObject;
                _equipamento = corpo.GetComponent<EquipamentoNoCorpo>();
                if (_equipamento == null) _equipamento = corpo.AddComponent<EquipamentoNoCorpo>();
                // o corpo em repouso, de onde saem as posições (o do mesmo personagem no prefab)
                var prefab = NetworkManager.Singleton != null ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab : null;
                _referencia = prefab != null ? prefab.transform.Find(corpo.name) : null;
            }
            if (_referencia == null) return;

            long chave = EquipamentoNoCorpo.OQueVeste(_inventario, out var mochila, out bool coldre, out var alca, out var costas, out var cintura);
            if (chave != _vestido)
            {
                _vestido = chave;
                _equipamento.Vestir(_referencia, mochila, coldre, alca, costas, cintura);
                _aplicado = false;
            }

            bool noCarro = _rede.Passageiro != null && _rede.Passageiro.NoCarro;
            bool visivel = !_rede.IsOwner || _rede.CorpoVisivelNoDono;
            if (_aplicado && noCarro == _noCarro && visivel == _visivel) return;
            _aplicado = true;
            _noCarro = noCarro;
            _visivel = visivel;
            foreach (var peca in _equipamento.Pecas)
            {
                if (peca == null) continue;
                peca.SetActive(!noCarro);
                foreach (var r in peca.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = visivel ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
                    r.renderingLayerMask = visivel ? 1u : JogadorRede.MascaraDoDono;
                }
            }
        }
    }
}
