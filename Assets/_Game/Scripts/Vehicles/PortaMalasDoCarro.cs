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
    /// Na caminhonete (15/11/2026), é a caçamba aberta (<see cref="Carro.cacamba"/>): um gatilho em volta dela, sempre
    /// aberta. Mirando o estepe ou o macaco (a caixa da malha, sem folga: "está um pouco difícil de entrar na caçamba, pois
    /// toda vez aparece a opção de pegar o estepe ou o macaco"), pega ou guarda; fora deles, sobe na caçamba pela subida
    /// do lado onde o jogador está (a <see cref="PortaDoCarro"/> da caçamba).
    /// </summary>
    public class PortaMalasDoCarro : Interagivel
    {
        public Carro carro;

        public const int Macaco = 0, Estepe = 1;

        public override string Dica(ArmaJogador quem)
        {
            if (!Pode(quem)) return null;
            if (!carro.PortaMalasAberto) return carro.Velocidade > 1.5f || carro.cacamba ? null : "[E] Abrir o porta-malas";
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
                if (e >= 0) return inv.PodeReceber(Item.Pneu, out var motivo)
                    ? $"[E] Pegar o estepe ({e}%){(inv.PneuVaiParaAsMaos ? " nas mãos (pesado)" : "")}" : motivo;
                if (AcharPneu(inv, out int estado, out _)) return $"[E] Guardar o pneu ({estado}%)";
            }
            // a caçamba (a caminhonete) não fecha: fora do estepe e do macaco, a subida (sem lugar livre, nada, e a mira passa
            // para o que está atrás)
            if (carro.cacamba)
            {
                var subida = Subida(quem);
                return subida != null ? subida.Dica(quem) : null;
            }
            return "[E] Fechar o porta-malas";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (!Pode(quem)) return;
            if (!carro.PortaMalasAberto)
            {
                if (!carro.cacamba) carro.PedirPortaMalas(true);
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
                if (AcharPneu(inv, out _, out ushort id)) { carro.PedirGuardar(Estepe, id); return; }
            }
            if (carro.cacamba)
            {
                var subida = Subida(quem);
                if (subida != null && subida.Dica(quem) != null) subida.Usar(quem);
                return;
            }
            carro.PedirPortaMalas(false);
        }

        // a subida da caçamba do lado onde o jogador está (pelo x dele no espaço do carro)
        PortaDoCarro Subida(ArmaJogador quem)
        {
            if (carro.portas == null) return null;
            float x = carro.transform.InverseTransformPoint(quem.transform.position).x;
            PortaDoCarro achada = null;
            foreach (var p in carro.portas)
                if (p != null && p.cacamba && (achada == null || Mathf.Sign(p.lado) == Mathf.Sign(x))) achada = p;
            return achada;
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
            var olho = rede.Olho;
            var raio = new Ray(olho.position, olho.forward);
            int alvo = -1;
            float menor = float.MaxValue;
            // na caçamba, a caixa da malha sem folga e mais perto (antes, a folga de 8 cm tapava a subida)
            float folga = carro.cacamba ? 0f : 0.08f, alcance = carro.cacamba ? 2.4f : 3f;
            if (Na(carro.macacoNoPortaMalas, raio, out float tm, folga, alcance) && tm < menor) { menor = tm; alvo = Macaco; }
            if (Na(carro.estepeNoPortaMalas, raio, out float te, folga, alcance) && te < menor) { alvo = Estepe; }
            return alvo;
        }

        internal static bool Na(MeshRenderer r, Ray raio, out float t, float folga = 0.08f, float alcance = 3f)
        {
            t = 0f;
            if (r == null) return false;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            var tr = r.transform;
            var local = new Ray(tr.InverseTransformPoint(raio.origin), tr.InverseTransformDirection(raio.direction));
            var caixa = mf.sharedMesh.bounds;
            caixa.Expand(folga);
            return caixa.IntersectRay(local, out t) && t < alcance;
        }

        /// <summary>
        /// O pneu para pôr numa roda ou no porta-malas: primeiro o das mãos (o Id 0), senão o melhor da mochila.
        /// </summary>
        internal static bool AcharPneu(InventarioJogador inv, out int estado, out ushort id)
        {
            id = 0;
            estado = inv.EstadoDoPneuNasMaos;
            if (estado >= 0) return true;
            if (!Achar(inv, Item.Pneu, out var it)) return false;
            id = it.id;
            estado = it.qtd;
            return true;
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
