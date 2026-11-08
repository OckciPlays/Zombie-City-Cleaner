using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A pesca dos outros (07/11/2026, a pedido: "a linha e a boia aparecem para os outros jogadores também"), em cada
    /// máquina, para os jogadores que não são dela: a boia que vem pela rede (<see cref="InventarioJogador.BoiaNaRede"/>)
    /// e a linha da ponta da vara do corpo até ela. A boia voa da vara quando ele lança, boia esperando, treme nas
    /// beliscadas, afunda na mordida e corre com o peixe na puxada (a linha esticada). O dono desenha a dele
    /// (<see cref="PescaJogador"/>).
    /// </summary>
    public class PescaVista : MonoBehaviour
    {
        JogadorRede _rede;
        InventarioJogador _inventario;
        readonly LinhaDaPesca _linha = new LinhaDaPesca();
        PescaJogador.Fase _fase;
        float _desde;
        Vector3 _ponto, _deOnde, _suave;
        static AudioClip _plop;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _inventario = GetComponent<InventarioJogador>();
            // a vara nova no corpo (07/11/2026), em todas as cópias do jogador, como esta
            if (GetComponent<VaraNoCorpo>() == null) gameObject.AddComponent<VaraNoCorpo>();
        }

        void OnDestroy() => _linha.Destruir();

        void Update()
        {
            if (_rede == null || _inventario == null || !_inventario.IsSpawned || _rede.IsOwner)
            {
                if (_linha.Visivel) _linha.Esconder();
                return;
            }
            var v = _inventario.BoiaNaRede.Value;
            var fase = (PescaJogador.Fase)Mathf.RoundToInt(v.w);
            bool comVara = _inventario.NaMao == Item.VaraDePesca;
            if (fase == PescaJogador.Fase.Parado || !comVara || !LinhaDaPesca.PontaNoCorpo(_rede, out var ponta))
            {
                _fase = PescaJogador.Fase.Parado;
                if (_linha.Visivel) _linha.Esconder();
                return;
            }
            var ponto = new Vector3(v.x, v.y, v.z);
            if (fase != _fase)
            {
                // o lance: a boia sai da vara; as outras trocas, o "plop" de onde ela está
                if (fase == PescaJogador.Fase.Voando) _deOnde = ponta;
                else if (_fase != PescaJogador.Fase.Parado && (fase == PescaJogador.Fase.Mordendo || fase == PescaJogador.Fase.Esperando && _fase == PescaJogador.Fase.Voando))
                {
                    if (_plop == null) _plop = PescaJogador.GerarPlop();
                    Efeitos.Som(_plop, ponto, fase == PescaJogador.Fase.Mordendo ? 0.8f : 0.5f, true, 20f);
                    AguaNaSuperficie.Onda(ponto, fase == PescaJogador.Fase.Mordendo ? 0.8f : 0.6f);
                }
                _fase = fase;
                _desde = Time.time;
                _suave = ponto;
            }
            float t = Time.time - _desde;
            Vector3 boia;
            float folga = 1f;
            switch (fase)
            {
                case PescaJogador.Fase.Voando:
                {
                    float d = Vector3.Distance(_deOnde, ponto);
                    float k = Mathf.Clamp01(t / (0.35f + d * 0.035f));
                    boia = Vector3.Lerp(_deOnde, ponto, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * (0.8f + d * 0.12f));
                    folga = 0.25f;
                    break;
                }
                case PescaJogador.Fase.Beliscando:
                    boia = ponto + Vector3.down * (0.025f + 0.015f * Mathf.Sin(t * 40f));
                    break;
                case PescaJogador.Fase.Mordendo:
                    boia = ponto + Vector3.down * (0.12f + 0.06f * Mathf.Sin(t * 30f));
                    folga = 0.6f;
                    break;
                case PescaJogador.Fase.Puxando:
                    // a boia vem pela rede dez vezes por segundo: segue suave
                    _suave = Vector3.Lerp(_suave, ponto, 1f - Mathf.Exp(-10f * Time.deltaTime));
                    boia = _suave;
                    folga = 0.1f;
                    break;
                default:
                    boia = ponto + Vector3.up * (Mathf.Sin(Time.time * 2.2f) * 0.02f);
                    break;
            }
            _linha.Desenhar(ponta, boia, folga);
        }
    }
}
