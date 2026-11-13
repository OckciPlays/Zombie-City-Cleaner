using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// As portas que abrem de uma casa do mundo (o Plano_Portas.md da pasta de arte), no objeto dela: os pontos das portas
    /// (<see cref="PortaDaObra"/>, nas coordenadas da casa) e a semente. A <see cref="PortasDoMundo"/> registra as chaves
    /// delas (o anfitrião confere os pedidos por elas) e põe as folhas aos poucos (<see cref="PortaDaCasa"/>, filhas da
    /// casa: saem com ela).
    /// </summary>
    public class PortasDaConstrucao : MonoBehaviour
    {
        internal PortaDaObra[] portas;
        internal JanelaDaObra[] janelas;
        internal int semente;
        /// <summary>A próxima porta e a próxima janela a pôr (as folhas entram aos poucos).</summary>
        internal int proxima, proximaJanela;
        internal ulong[] chaves, chavesDasJanelas;

        /// <summary>
        /// Põe as portas e as janelas no objeto da casa (já no lugar). Nada, sem elas ou sem o <see cref="PortasDoMundo"/>.
        /// 'andar' entra na chave (as partes do Hospital Principal, a casca e os andares, ficam no mesmo lugar).
        /// </summary>
        public static void Por(GameObject go, List<PortaDaObra> portas, List<JanelaDaObra> janelas, int semente, int andar = -100)
        {
            if (PortasDoMundo.Instancia == null || (portas == null || portas.Count == 0) && (janelas == null || janelas.Count == 0)) return;
            var c = go.AddComponent<PortasDaConstrucao>();
            c.portas = portas != null ? portas.ToArray() : new PortaDaObra[0];
            c.janelas = janelas != null ? janelas.ToArray() : new JanelaDaObra[0];
            c.semente = semente;
            // a chave da casa (a mesma em todas as máquinas: o lugar dela, como a dos móveis novos)
            ulong obra = MobiliaNova.ChaveDaObra(go.transform.position, andar);
            c.chaves = new ulong[c.portas.Length];
            for (int i = 0; i < c.portas.Length; i++) c.chaves[i] = PortasDoMundo.ChaveDaPorta(obra, i);
            c.chavesDasJanelas = new ulong[c.janelas.Length];
            for (int i = 0; i < c.janelas.Length; i++) c.chavesDasJanelas[i] = PortasDoMundo.ChaveDaJanela(obra, i);
            PortasDoMundo.Instancia.Registrar(c);
        }

        void OnDestroy() => PortasDoMundo.Instancia?.Tirar(this);
    }
}
