using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using Random = UnityEngine.Random;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Largar uma parte da pilha (17/11/2026, a pedido: "dropar quantidade de loot"): o botão direito, "Largar quantos...",
    /// escolhe quantos da pilha (ou quantas balas da caixa de munição) vão para o chão (a escolha na tela:
    /// TelaDoInventario.Quantos.cs). O resto fica na mochila, no mesmo lugar da grade.
    /// </summary>
    public partial class InventarioJogador
    {
        /// <summary>
        /// O dono: larga n da pilha (na caixa de munição, n balas, numa caixa no chão). Com n do tamanho da pilha, larga
        /// ela inteira (o <see cref="Largar"/>).
        /// </summary>
        public void LargarQuantos(ushort id, int n)
        {
            if (n <= 0 || !Achar(id, out var it) || !Itens.EmPilha(it.item) || (_arma.Usando && id == IdNaMao)) return;
            if (n >= Itens.Quantos(it.item, it.qtd))
            {
                Largar(id);
                return;
            }
            LargarQuantosRpc(id, (ushort)Mathf.Min(n, ushort.MaxValue), PontoParaLargar(_rede.Olho), transform.eulerAngles.y + Random.Range(-50f, 50f));
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.5f, false);
        }

        [Rpc(SendTo.Server)]
        void LargarQuantosRpc(ushort id, ushort n, Vector3 ponto, float giro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || ItensNoMundo.Instancia == null || n == 0) return;
            if ((ponto - transform.position).sqrMagnitude > 4f * 4f) ponto = transform.position;
            int k = Indice(id);
            if (k < 0) return;
            var it = Mochila[k];
            if (!Itens.EmPilha(it.item)) return;
            int tem = Itens.Quantos(it.item, it.qtd);
            if (n >= tem)
            {
                // (a pilha mudou no caminho: vai inteira)
                Mochila.RemoveAt(k);
                ItensNoMundo.Instancia.Criar(it.item, ponto, giro, it.qtd, it.desgaste, it.cores);
                return;
            }
            var fica = it;
            fica.qtd = (ushort)(tem - n);
            Mochila[k] = fica;
            ItensNoMundo.Instancia.Criar(it.item, ponto, giro, n, it.desgaste, it.cores);
        }
    }
}
