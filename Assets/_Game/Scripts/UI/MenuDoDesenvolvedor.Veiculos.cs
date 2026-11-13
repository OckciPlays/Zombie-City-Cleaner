using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// F3 > Veículos (10/11/2026, a pedido: "uma opção no f3 para conseguir dar spawn em carros já arrumados 100% e com
    /// gasolina e chave"): a perua, o cupê e as duas motos, criados pelo anfitrião 6 m à frente de quem pediu (de lado, no
    /// chão), com as peças do motor, os pneus e o estepe a 100%, o macaco no porta-malas e o tanque cheio; a chave do veículo
    /// vai para a mochila (sem espaço, ou com "No chão", fica no chão à frente). Os prefabs vêm da lista da rede (os quatro já
    /// estão nela, para o anfitrião poder criar). Eles não entram no save: o save guarda só a perua e o cupê da partida.
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        static readonly (string nome, string prefab)[] VeiculosDoF3 =
        {
            ("Perua", "Carro"), ("Cupê", "Cupe"), ("Moto cross", "Moto_Cross"), ("Moto Fat Boy", "Moto_FatBoy"),
        };

        void BotoesDosVeiculos(float x, float y, float w, float bw, float u)
        {
            GUI.Label(new Rect(x, y, w, 40 * u), "Arrumado (100%) e com o tanque cheio, 6 m à frente. A chave vai para a mochila (ou para o chão).", _texto);
            y += 46 * u;
            for (int i = 0; i < VeiculosDoF3.Length; i++)
            {
                var r = new Rect(x + (i % 2) * (bw + 8 * u), y + (i / 2) * 32 * u, bw, 26 * u);
                if (GUI.Button(r, TelaDoInventario.Maiuscula(VeiculosDoF3[i].nome), _botao)) CriarVeiculoRpc((byte)i, _destino == 1);
            }
        }

        [Rpc(SendTo.Server)]
        void CriarVeiculoRpc(byte qual, bool chaveNoChao, RpcParams parametros = default)
        {
            if (!Aceita(parametros)) return;
            string feito = CriarVeiculoNoAnfitriao(qual, chaveNoChao, out _);
            if (!string.IsNullOrEmpty(feito)) AvisoDoCarroRpc(feito);
        }

        /// <summary>
        /// No anfitrião: cria o veículo "qual" (o índice de <see cref="VeiculosDoF3"/>) arrumado, à frente deste jogador, e dá
        /// a chave a ele. Devolve o aviso (e o carro criado; null se não deu).
        /// </summary>
        public string CriarVeiculoNoAnfitriao(int qual, bool chaveNoChao, out Carro criado)
        {
            criado = null;
            if (!IsServer || qual < 0 || qual >= VeiculosDoF3.Length) return null;
            var (nome, nomeDoPrefab) = VeiculosDoF3[qual];
            var prefab = PrefabDoVeiculo(nomeDoPrefab);
            if (prefab == null) return $"{nome}: o prefab não está na lista da rede";
            // o lugar: 6 m à frente de quem pediu, de lado para ele (a porta virada para ele), no chão; com outro carro ali, mais
            // à frente ou do lado (o primeiro lugar livre)
            var frente = transform.forward;
            frente.y = 0f;
            frente = frente.sqrMagnitude > 1e-4f ? frente.normalized : Vector3.forward;
            var lado = Vector3.Cross(Vector3.up, frente);
            var giro = Quaternion.LookRotation(lado);
            int mascara = ~((1 << 2) | (1 << JogadorRede.CamadaJogador));
            Vector3 ponto = transform.position + frente * 6f;
            bool achou = false;
            foreach (float ate in new[] { 6f, 10f, 14f })
            {
                foreach (float desvio in new[] { 0f, -6f, 6f })
                {
                    var q = ChaoSemCarro(transform.position + frente * ate + lado * desvio, mascara);
                    if (TemVeiculo(q + Vector3.up * 1.2f, giro)) continue;
                    ponto = q;
                    achou = true;
                    break;
                }
                if (achou) break;
            }
            var go = Instantiate(prefab, ponto + Vector3.up * 0.6f, giro);
            go.name = prefab.name;
            // o estado de fábrica de agora (o carro aplica ao nascer na rede): tudo a 100% e o tanque cheio
            var carro = go.GetComponent<Carro>();
            carro.ArrumadoDeFabrica();
            go.GetComponent<NetworkObject>().Spawn(true);
            criado = carro;
            // a chave
            string chave = "";
            if (carro.chaveDoVeiculo != Item.Nada)
                chave = DarItem(carro.chaveDoVeiculo, chaveNoChao, 0) ? " A chave está na mochila." : " A chave está no chão, à frente.";
            return $"{nome}: arrumado, com o tanque cheio.{chave}";
        }

        // o chão embaixo do ponto (o primeiro que não é um veículo: o raio batia no teto do carro de antes, e o novo nascia em cima dele)
        static Vector3 ChaoSemCarro(Vector3 p, int mascara)
        {
            var acertos = Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 80f, mascara, QueryTriggerInteraction.Ignore);
            System.Array.Sort(acertos, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in acertos)
                if (h.collider.GetComponentInParent<Carro>() == null) return h.point;
            return p;
        }

        // um veículo (um corpo com física) na caixa de um carro neste lugar
        static bool TemVeiculo(Vector3 centro, Quaternion giro)
        {
            foreach (var col in Physics.OverlapBox(centro, new Vector3(1.4f, 0.9f, 2.8f), giro, ~0, QueryTriggerInteraction.Ignore))
                if (col.attachedRigidbody != null && col.attachedRigidbody.GetComponentInParent<Carro>() != null) return true;
            return false;
        }

        /// <summary>O prefab do veículo na lista da rede, pelo nome (null sem ele).</summary>
        static GameObject PrefabDoVeiculo(string nome)
        {
            var rede = NetworkManager.Singleton;
            if (rede == null || rede.NetworkConfig?.Prefabs?.Prefabs == null) return null;
            foreach (var p in rede.NetworkConfig.Prefabs.Prefabs)
                if (p.Prefab != null && p.Prefab.name == nome && p.Prefab.GetComponent<Carro>() != null) return p.Prefab;
            return null;
        }
    }
}
