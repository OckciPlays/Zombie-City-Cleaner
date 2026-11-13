using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O computador da sala de operações do bunker (B0; a parte 7 do Plano_NPCs_Bunker.md da pasta de arte): E abre o
    /// terminal (<see cref="TerminalDoBunker"/>). O gatilho fica em volta do monitor e do teclado (o computador não tem a
    /// caixa de colisão dos móveis).
    /// </summary>
    public class ComputadorNaMira : Interagivel
    {
        public override string Dica(ArmaJogador quem) => quem == null ? null : "[E] Usar o terminal do bunker";

        public override void Usar(ArmaJogador quem)
        {
            if (quem != null) TerminalDoBunker.Abrir();
        }

        /// <summary>Faz do móvel do computador (o prefab da mobília nova, já posto no andar) um terminal.</summary>
        public static void Ligar(GameObject movel)
        {
            var filtros = movel.GetComponentsInChildren<MeshFilter>(true);
            var caixa = new Bounds(new Vector3(0f, 0.25f, 0f), new Vector3(0.6f, 0.5f, 0.5f));
            bool primeira = true;
            var paraOMovel = movel.transform.worldToLocalMatrix;
            foreach (var f in filtros)
            {
                if (f.sharedMesh == null) continue;
                var b = f.sharedMesh.bounds;
                var m = paraOMovel * f.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var p = m.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z));
                    if (primeira)
                    {
                        caixa = new Bounds(p, Vector3.zero);
                        primeira = false;
                    }
                    else caixa.Encapsulate(p);
                }
            }
            var mira = new GameObject("Mira do terminal") { layer = 2 };
            mira.transform.SetParent(movel.transform, false);
            var gatilho = mira.AddComponent<BoxCollider>();
            gatilho.isTrigger = true;
            gatilho.center = caixa.center;
            gatilho.size = caixa.size + new Vector3(0.06f, 0.06f, 0.06f);
            mira.AddComponent<ComputadorNaMira>();
        }
    }
}
