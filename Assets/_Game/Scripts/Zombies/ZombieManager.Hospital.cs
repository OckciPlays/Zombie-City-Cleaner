using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os zumbis de dentro do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 7, 08/11/2026). O povoamento
    /// das células não nasce no corpo dele (<see cref="HospitalDoMundo.Dentro"/>): cada andar ganha os seus quando carrega
    /// perto de alguém (no anfitrião), pelos grupos do <see cref="ZumbisDoHospital"/> (uns isolados, uns grupos pequenos e
    /// as salas fechadas com zumbis), em pontos do NavMesh dentro do cômodo de cada um, onde nenhum jogador está vendo.
    /// O andar lembra os seus: quando ele sai (o jogador subiu ou desceu), os que não estão atrás de ninguém voltam ao pool
    /// e o lugar de cada um fica guardado, para eles voltarem ali quando o andar carregar de novo (os mortos não voltam).
    /// Longe de todos, o hospital se esquece (e se povoa de novo na volta, como os andares dos prédios).
    /// </summary>
    public partial class ZombieManager
    {
        sealed class AndarDoHospital
        {
            // os de agora (o zumbi, o Id e a geração dele: o pool reaproveita o objeto) e os guardados (o andar saiu)
            public readonly List<(Zumbi z, int id, byte geracao)> vivos = new List<(Zumbi, int, byte)>();
            public List<Vector3> guardados;
            public bool povoado;
        }

        readonly AndarDoHospital[] _hospital = new AndarDoHospital[PlantaDoHospital.Andares];

        /// <summary>Para conferir (o F1 e o teste): quantos zumbis do hospital estão de pé agora, e em quantos andares.</summary>
        public int ZumbisNoHospital
        {
            get
            {
                int n = 0;
                foreach (var a in _hospital)
                    if (a != null)
                        foreach (var v in a.vivos) if (Vale(v)) n++;
                return n;
            }
        }

        bool Vale((Zumbi z, int id, byte geracao) v) => v.z != null && _porId[v.id] == v.z && _geracoes[v.id] == v.geracao && !v.z.Morto;

        // a cada segundo (com o povoamento), no anfitrião: um andar por vez
        void PovoarOHospital(IChaoDosZumbis dados)
        {
            var h = HospitalDoMundo.Instancia;
            if (h == null || h.Raiz == null || dados == null || !dados.TemHospital) return;
            // longe de todos: esquece (o pool já recolheu os de longe)
            float longe = distanciaDeRecolher + 40f;
            if (!PertoDeAlguem(h.Raiz.position, longe * longe))
            {
                for (int f = 0; f < _hospital.Length; f++) _hospital[f] = null;
                return;
            }
            for (int f = 0; f < _hospital.Length; f++)
            {
                if (!h.AndarPronto(f)) continue;
                var a = _hospital[f] ??= new AndarDoHospital();
                if (a.povoado) continue;
                if (!AndarDoHospitalNoNavMesh(h, f)) continue;   // o NavMesh do andar ainda não veio: no próximo segundo
                a.povoado = true;
                if (a.guardados != null)
                {
                    foreach (var p in a.guardados)
                        if (NavMesh.SamplePosition(p, out var hit, 1f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - p.y) < 0.6f)
                            Guardar(a, Nascer(hit.position));
                    a.guardados = null;
                }
                else PovoarAndarDoHospital(h, dados, f, a);
                return;
            }
        }

        void Guardar(AndarDoHospital a, Zumbi z)
        {
            if (z == null) return;
            a.vivos.Add((z, z.Id, z.Geracao));
            PovoamentoPos++;
        }

        // o NavMesh já cobre o andar? (o meio do corredor de cima, na altura do piso dele)
        static bool AndarDoHospitalNoNavMesh(HospitalDoMundo h, int f)
        {
            var meio = h.Raiz.TransformPoint(new Vector3(28f + PlantaDoHospital.X0, PlantaDoHospital.Base(f) + 0.05f, 22.5f + PlantaDoHospital.Z0));
            return NavMesh.SamplePosition(meio, out var hit, 1.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - meio.y) < 0.6f;
        }

        void PovoarAndarDoHospital(HospitalDoMundo h, IChaoDosZumbis dados, int f, AndarDoHospital a)
        {
            var grupos = ZumbisDoHospital.DoAndar(dados.Semente, f);
            var sorte = new System.Random(Misturar(dados.Semente, 9091 + f, 4421));
            float chao = PlantaDoHospital.Base(f);
            foreach (var g in grupos)
            {
                PovoamentoQuis += g.quantos;
                for (int i = 0; i < g.quantos; i++)
                    for (int t = 0; t < 10; t++)
                    {
                        float x = Mathf.Lerp(g.area.xMin + 0.5f, g.area.xMax - 0.5f, (float)sorte.NextDouble());
                        float z = Mathf.Lerp(g.area.yMin + 0.5f, g.area.yMax - 0.5f, (float)sorte.NextDouble());
                        var alvo = h.Raiz.TransformPoint(new Vector3(x, chao + 0.05f, z));
                        if (!NavMesh.SamplePosition(alvo, out var hit, 0.8f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - alvo.y) > 0.6f) continue;
                        // dentro do cômodo (o ponto achado não pode ter escorregado para o vizinho, do outro lado da parede)
                        var q = h.Raiz.InverseTransformPoint(hit.position);
                        if (!g.area.Contains(new Vector2(q.x, q.z))) continue;
                        var p = hit.position;
                        if (ProtegerDoSol && Exposto(p)) continue;   // o sol entrando pela janela
                        if (AlguemVe(p)) continue;
                        Guardar(a, Nascer(p));
                        break;
                    }
            }
        }

        /// <summary>
        /// No anfitrião: o andar f do hospital saiu (ninguém perto dele). Os zumbis dele que não estão atrás de ninguém voltam
        /// ao pool, e o lugar de cada um fica guardado (eles voltam ali quando o andar carregar de novo).
        /// </summary>
        public void SaiuOAndarDoHospital(int f, Transform raiz)
        {
            if (!IsServer || f < 0 || f >= _hospital.Length) return;
            var a = _hospital[f];
            if (a == null || !a.povoado) return;
            var guardados = new List<Vector3>();
            for (int i = a.vivos.Count - 1; i >= 0; i--)
            {
                var v = a.vivos[i];
                if (!Vale(v)) continue;
                if (v.z.Engajado) continue;   // atrás de alguém: segue (e deixa de ser do andar)
                // só os que ainda estão no andar (o que desceu a escada atrás de um barulho fica onde está)
                var q = raiz.InverseTransformPoint(v.z.transform.position);
                if (Mathf.Abs(q.y - PlantaDoHospital.Base(f)) > 1.5f) continue;
                guardados.Add(v.z.transform.position);
                Devolver(v.z);
            }
            a.vivos.Clear();
            a.guardados = guardados;
            a.povoado = false;
        }

        /// <summary>Teste (F1): o hospital se esquece (os andares carregados se povoam de novo, com o plano do mundo).</summary>
        public void EsquecerOHospital()
        {
            for (int f = 0; f < _hospital.Length; f++)
            {
                var a = _hospital[f];
                if (a == null) continue;
                foreach (var v in a.vivos) if (Vale(v) && !v.z.Engajado) Devolver(v.z);
                _hospital[f] = null;
            }
        }
    }
}
