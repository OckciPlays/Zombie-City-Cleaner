using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os pedidos do dono para as marcas do mapa (03/11/2026, World/MundoDaPartida.Marcas.cs): o ping (o botão do meio do
    /// mouse no mundo, ou o clique direito no mapa), a marca, a anotação, editar e apagar. O anfitrião confere quem pede e
    /// escreve a lista.
    /// </summary>
    public partial class JogadorRede
    {
        /// <summary>O nome do jogador no mapa (o do personagem; sem ele, "jogador N").</summary>
        public string NomeNoMapa
        {
            get
            {
                string nome = Aparencia.Value.nome.ToString();
                return string.IsNullOrWhiteSpace(nome) ? $"jogador {OwnerClientId + 1}" : nome;
            }
        }

        public void PedirPing(Vector3 ponto) => MarcaRpc((byte)TipoDeMarca.Ping, ponto, 0, "");

        public void PedirMarca(Vector3 ponto, byte cor, string texto)
        {
            texto = MarcasDoMapa.Cortar(texto);
            MarcaRpc((byte)(texto.Length > 0 ? TipoDeMarca.Anotacao : TipoDeMarca.Marca), ponto, cor, texto);
        }

        public void PedirApagarMarca(ushort id) => ApagarMarcaRpc(id);

        public void PedirEditarMarca(ushort id, byte cor, string texto) => EditarMarcaRpc(id, cor, MarcasDoMapa.Cortar(texto));

        [Rpc(SendTo.Server)]
        void MarcaRpc(byte tipo, Vector3 ponto, byte cor, string texto, RpcParams parametros = default)
        {
            // (12/11/2026: as marcas do mundo da cena, o sorteado ou o Lakewood)
            var m = MarcasDoMundo.Atual;
            var mundo = MundoDoJogo.Atual;
            if (parametros.Receive.SenderClientId != OwnerClientId || m == null || mundo == null) return;
            // dentro do mapa
            if (!mundo.Area.Contains(new Vector2(ponto.x, ponto.z))) return;
            m.PorMarca((TipoDeMarca)Mathf.Min(tipo, (byte)2), ponto, cor, texto, OwnerClientId, NomeNoMapa);
        }

        [Rpc(SendTo.Server)]
        void ApagarMarcaRpc(ushort id, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            MarcasDoMundo.Atual?.ApagarMarca(id);
        }

        [Rpc(SendTo.Server)]
        void EditarMarcaRpc(ushort id, byte cor, string texto, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            MarcasDoMundo.Atual?.EditarMarca(id, cor, texto);
        }
    }
}
