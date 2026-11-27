using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A pesca dos outros (07/11/2026, a pedido: "a linha e a boia aparecem para os outros jogadores também"), em cada
    /// máquina, para os jogadores que não são dela: a boia que vem pela rede (<see cref="InventarioJogador.BoiaNaRede"/>)
    /// e a linha da ponta da vara do corpo até ela. A boia voa da vara quando ele lança, boia esperando, afunda um pouco e
    /// volta nas beliscadas, some de uma vez na mordida e corre com o peixe na puxada (a linha esticada). O dono desenha a
    /// dele (<see cref="PescaJogador"/>).
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
            var fase = (PescaJogador.Fase)Mathf.FloorToInt(v.w + 0.001f);   // (a fração do w é a força na vara: VaraQueEntorta)
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
                else if (_fase != PescaJogador.Fase.Parado && (fase == PescaJogador.Fase.Mordendo || fase == PescaJogador.Fase.Beliscando
                         || fase == PescaJogador.Fase.Esperando && _fase == PescaJogador.Fase.Voando))
                {
                    if (_plop == null) _plop = PescaJogador.GerarPlop();
                    float forca = fase == PescaJogador.Fase.Mordendo ? 0.8f : fase == PescaJogador.Fase.Beliscando ? 0.12f : 0.5f;
                    Efeitos.Som(_plop, ponto, forca, true, 20f);
                    AguaNaSuperficie.Onda(ponto, fase == PescaJogador.Fase.Mordendo ? 0.8f : fase == PescaJogador.Fase.Beliscando ? 0.3f : 0.6f);
                    if (fase == PescaJogador.Fase.Mordendo) AguaNaSuperficie.Respingo(ponto, 6, 0.35f);
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
                    // (27/11/2026, a pesca nova) a beliscada: a fundura e o tempo de cada uma só o dono sabe; aqui, uma média
                    // (40% da boia, meio segundo), com a mesma curva
                    boia = PescaJogador.BoiaNaAgua(ponto) + Vector3.down * (_linha.AcimaDaAgua * 0.4f * PescaJogador.CurvaDaBeliscada(t / 0.5f));
                    folga = 0.8f;
                    break;
                case PescaJogador.Fase.Mordendo:
                {
                    // a mordida: a boia some de uma vez, como no dono (sem o arrasto, que não vai pela rede)
                    float k = Mathf.Clamp01(t / 0.12f);
                    boia = ponto + Vector3.down * ((_linha.AcimaDaAgua + 0.12f) * (1f - (1f - k) * (1f - k)) + 0.02f * Mathf.Sin(t * 25f) * k);
                    folga = 0.15f;
                    break;
                }
                case PescaJogador.Fase.Puxando:
                    // a boia vem pela rede dez vezes por segundo: segue suave
                    _suave = Vector3.Lerp(_suave, ponto, 1f - Mathf.Exp(-10f * Time.deltaTime));
                    boia = _suave;
                    folga = 0.1f;
                    break;
                default:
                    boia = PescaJogador.BoiaNaAgua(ponto);
                    break;
            }
            _linha.Desenhar(ponta, boia, folga);
        }
    }
}
