using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A planta do bunker, a mesma do `cidade_bunker.py` (pasta de arte, `Modelo 3D/Cidade`), nas coordenadas locais da
    /// casa da família (12 × 10 m, piso em 0,15): x à direita, y para cima e z para os fundos.
    /// A escada começa no meio da cozinha e desce para +z em dois lances de 15 degraus, com um patamar no meio, até o
    /// vestíbulo e a porta blindada. Depois dela, o salão (10 × 14 m, 5,3 m de pé-direito), com dois quartos de cada
    /// lado e o depósito (à esquerda de quem entra, perto da porta), no fim de corredores curtos.
    /// Ao mudar a planta lá, mude aqui (e rode a montagem da cidade).
    /// </summary>
    static class PlantaDoBunker
    {
        public const float Piso = 0.15f;
        /// <summary>O meio da escada (e da porta e do salão) e onde ela começa, na cozinha.</summary>
        public const float XEsc = -5f, ZTopo = 1.2f;
        public const float Teto = -0.55f, PeSalao = 5.3f, PeComodo = 3f, PeCorredor = 2.4f;
        /// <summary>O chão do bunker.</summary>
        public const float Fundo = Teto - PeSalao;
        public const int DegrausPorLance = 15;
        public const float PisoDoDegrau = 0.26f, Patamar = 1.2f;
        public const float AlturaDoDegrau = (Piso - Fundo) / (2 * (DegrausPorLance + 1));
        public const float YPatamar = Piso - AlturaDoDegrau * (DegrausPorLance + 1);
        public const float ZPatamar = ZTopo + PisoDoDegrau * DegrausPorLance;
        public const float ZLance2 = ZPatamar + Patamar;
        public const float ZBase = ZLance2 + PisoDoDegrau * DegrausPorLance;
        public const float ZPorta = ZBase + 1.6f;
        /// <summary>O salão: x de SX0 a SX1, z de SZ0 (a parede da porta) a SZ1.</summary>
        public const float SX0 = XEsc - 5f, SX1 = XEsc + 5f, SZ0 = ZPorta, SZ1 = ZPorta + 14f;
        public const float Corredor = 1.2f;
        /// <summary>A espessura das paredes.</summary>
        public const float Esp = 0.2f;
        /// <summary>O eixo das dobradiças da porta, no chão. Fechada, a folha vai dele para -x; o salão fica em +z.</summary>
        public static readonly Vector3 Dobradica = new Vector3(XEsc + 0.68f, Fundo, ZPorta + 0.16f);

        public readonly struct Comodo
        {
            public readonly string nome, titulo;
            /// <summary>-1 = à esquerda de quem entra no salão (-x), +1 = à direita.</summary>
            public readonly int lado;
            public readonly float v0, v1, largura;

            public Comodo(string nome, string titulo, int lado, float v0, float v1, float largura)
            {
                this.nome = nome; this.titulo = titulo; this.lado = lado; this.v0 = v0; this.v1 = v1; this.largura = largura;
            }

            public float X0 => lado < 0 ? SX0 - Corredor - largura : SX1 + Corredor;
            public float X1 => X0 + largura;
            public float Z0 => SZ0 + v0;
            public float Z1 => SZ0 + v1;
            /// <summary>O meio do corredor e da entrada.</summary>
            public float ZMeio => (Z0 + Z1) / 2f;
            /// <summary>A parede do lado do salão (a da entrada) e para onde ela olha, de dentro do cômodo (±1 em x).</summary>
            public float XEntrada => lado < 0 ? X1 : X0;
            public float ParaDentro => lado < 0 ? -1f : 1f;
        }

        /// <summary>Os cômodos, na ordem dos grupos de luz (1 a 5).</summary>
        public static readonly Comodo[] Comodos =
        {
            new Comodo("quarto1", "quarto 1", -1, 10.2f, 13.8f, 4f), new Comodo("quarto2", "quarto 2", -1, 5.6f, 9.2f, 4f),
            new Comodo("quarto3", "quarto 3", 1, 10.2f, 13.8f, 4f), new Comodo("quarto4", "quarto 4", 1, 5.6f, 9.2f, 4f),
            new Comodo("deposito", "depósito", -1, 0.2f, 4.6f, 4.5f),
        };

        /// <summary>
        /// As lâmpadas: o grupo (0 salão, 1 a 4 quartos, 5 depósito, 6 escada) e a posição. Repete o luzes_do_bunker()
        /// do Blender, que faz as luminárias em volta delas.
        /// </summary>
        public static IEnumerable<(int grupo, Vector3 pos)> Luzes()
        {
            foreach (float v in new[] { 1.75f, 6.85f, 12.1f }) yield return (0, new Vector3(XEsc, Fundo + 3.6f, SZ0 + v));
            for (int g = 0; g < Comodos.Length; g++)
            {
                var c = Comodos[g];
                yield return (g + 1, new Vector3((c.X0 + c.X1) / 2f, Fundo + PeComodo - 0.3f, c.ZMeio));
            }
            yield return (6, new Vector3(XEsc + 0.47f, YPatamar + 2f, (ZPatamar + ZLance2) / 2f));
            yield return (6, new Vector3(XEsc + 0.47f, Fundo + 2.15f, ZBase + 0.8f));
        }

        /// <summary>
        /// Os interruptores: onde (na parede ou no poste da grade), para onde a frente olha (yaw), o grupo de luzes e o
        /// que ele acende (para a dica). O do salão fica do lado da porta; o de cada cômodo, por dentro, à direita de quem
        /// entra; os da escada, no vestíbulo e na grade da boca da escada, na cozinha.
        /// </summary>
        public static IEnumerable<(Vector3 pos, float yaw, int grupo, string oQue)> Interruptores()
        {
            const float altura = 1.3f;
            yield return (new Vector3(XEsc - 1f, Fundo + altura, SZ0 + Esp / 2f), 0f, 0, "a luz do salão");
            for (int g = 0; g < Comodos.Length; g++)
            {
                var c = Comodos[g];
                float x = c.XEntrada + c.ParaDentro * Esp / 2f;
                float z = c.ZMeio + (c.lado < 0 ? 0.9f : -0.9f);
                yield return (new Vector3(x, Fundo + altura, z), c.lado < 0 ? 270f : 90f, g + 1, "a luz do " + c.titulo);
            }
            yield return (new Vector3(XEsc - 0.6f, Fundo + altura, ZPorta - 0.5f), 90f, 6, "a luz da escada");
            yield return (new Vector3(XEsc - 0.65f, Piso + 0.75f, ZTopo + 0.27f), 180f, 6, "a luz da escada");
        }
    }
}
