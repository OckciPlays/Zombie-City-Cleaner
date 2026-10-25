using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O porta-malas do carro que se dirige: um gatilho na frente da tampa traseira (para fora da caixa de colisão de
    /// baixo, que tapava o que fica dentro), o que a mira procura. Fechado, E abre a tampa; aberto, E pega o que a mira
    /// aponta lá dentro (o macaco ou o estepe), guarda no lugar vazio o que o jogador leva, ou fecha a tampa.
    /// O macaco e o estepe não têm colisor: a mira é conferida contra a caixa da malha de cada um.
    /// </summary>
    public class PortaMalasDoCarro : Interagivel
    {
        public Carro carro;

        public const int Macaco = 0, Estepe = 1;

        public override string Dica(ArmaJogador quem)
        {
            if (!Pode(quem)) return null;
            if (!carro.PortaMalasAberto) return carro.Velocidade > 1.5f ? null : "[E] Abrir o porta-malas";
            var inv = quem.Inventario;
            int alvo = Alvo(quem);
            if (alvo == Macaco)
            {
                if (carro.TemMacaco) return inv.PodeReceber(Item.Macaco, out var motivo) ? "[E] Pegar o macaco" : motivo;
                if (Achar(inv, Item.Macaco, out _)) return "[E] Guardar o macaco";
            }
            else if (alvo == Estepe)
            {
                int e = carro.Estepe;
                if (e >= 0) return inv.PodeReceber(Item.Pneu, out var motivo) ? $"[E] Pegar o estepe ({e}%)" : motivo;
                if (Achar(inv, Item.Pneu, out var pneu)) return $"[E] Guardar o pneu ({pneu.qtd}%)";
            }
            return "[E] Fechar o porta-malas";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (!Pode(quem)) return;
            if (!carro.PortaMalasAberto)
            {
                carro.PedirPortaMalas(true);
                return;
            }
            var inv = quem.Inventario;
            int alvo = Alvo(quem);
            if (alvo == Macaco)
            {
                if (carro.TemMacaco) { carro.PedirDoPortaMalas(Macaco); return; }
                if (Achar(inv, Item.Macaco, out var m)) { carro.PedirGuardar(Macaco, m.id); return; }
            }
            else if (alvo == Estepe)
            {
                if (carro.Estepe >= 0) { carro.PedirDoPortaMalas(Estepe); return; }
                if (Achar(inv, Item.Pneu, out var p)) { carro.PedirGuardar(Estepe, p.id); return; }
            }
            carro.PedirPortaMalas(false);
        }

        bool Pode(ArmaJogador quem)
        {
            if (carro == null || !carro.IsSpawned || carro.Virado || quem == null || quem.Inventario == null) return false;
            var p = quem.GetComponent<PassageiroDoCarro>();
            return p == null || !p.NoCarro;
        }

        // o que a mira aponta dentro do porta-malas (pela caixa da malha do macaco e do estepe, mesmo sem eles lá), ou -1
        int Alvo(ArmaJogador quem)
        {
            var rede = quem.GetComponent<JogadorRede>();
            if (rede == null || rede.cameraMundo == null) return -1;
            var olho = rede.cameraMundo.transform;
            var raio = new Ray(olho.position, olho.forward);
            int alvo = -1;
            float menor = float.MaxValue;
            if (Na(carro.macacoNoPortaMalas, raio, out float tm) && tm < menor) { menor = tm; alvo = Macaco; }
            if (Na(carro.estepeNoPortaMalas, raio, out float te) && te < menor) { alvo = Estepe; }
            return alvo;
        }

        internal static bool Na(MeshRenderer r, Ray raio, out float t)
        {
            t = 0f;
            if (r == null) return false;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            var tr = r.transform;
            var local = new Ray(tr.InverseTransformPoint(raio.origin), tr.InverseTransformDirection(raio.direction));
            var caixa = mf.sharedMesh.bounds;
            caixa.Expand(0.08f);
            return caixa.IntersectRay(local, out t) && t < 3f;
        }

        /// <summary>A peça desse tipo na mochila (o pneu: o melhor).</summary>
        internal static bool Achar(InventarioJogador inv, Item item, out ItemNaMochila achado)
        {
            achado = default;
            bool tem = false;
            foreach (var it in inv.Mochila)
                if (it.item == item && (!tem || it.qtd > achado.qtd))
                {
                    achado = it;
                    tem = true;
                }
            return tem;
        }
    }
}
