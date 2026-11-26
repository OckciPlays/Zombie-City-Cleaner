using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// A mesa de oficina que os jogadores montam (25/11/2026). Primeiro foi a "mesa de ferramentas", de tábuas; no mesmo dia,
    /// a pedido do Pedro ("devemos substituir todas e deixar uma só padrão. No caso deixar a mesa de oficina, eu acho o modelo
    /// dela mais bonito"), ela virou a mesa de oficina do bunker (Resources/MesaDeOficina, do Blender/oficina.py: 2 m, o
    /// painel de furinhos com as ferramentas, a morsa azul, as gavetas). O kit (2 toras e 4 madeiras, feito à mão; no código
    /// ainda o <see cref="Item.MesaDeFerramentas"/>, para o número do item não mudar) monta-se no chão como os outros, em
    /// qualquer lugar, sem o núcleo da base. Montada, ela é uma mesa de oficina (<see cref="Bancada.Perto"/> conta com ela):
    /// o E abre a aba Criar da mochila, com as receitas da mesa, e segurar o E desmonta (o kit volta). A frente (a morsa e as
    /// gavetas) é o +Z, virada para quem monta, como as outras construções.
    /// Desde a revisão de 25/11/2026, ela tem vida (<see cref="VidaDaMesa"/>, no <c>valor</c>): sólida e cortando o NavMesh,
    /// sem vida ela era uma parede de graça, fora das regras da base (uma mesa na frente da porta fechava a casa aos zumbis
    /// para sempre). Os zumbis encostados batem nela como no muro, o Brutamonte e os chefes a derrubam, e quebrada ela deixa
    /// 2 madeiras. Ela continua sem pedir o núcleo da base (o tutorial do Gus a monta no pátio).
    /// </summary>
    public partial class ConstrucoesNoMundo
    {
        /// <summary>
        /// A vida da mesa montada (25/11/2026, a revisão): entre a barricada solta (100) e o portão (380). Com 2,5 de dano por
        /// zumbi encostado por segundo, 3 zumbis levam uns 33 s para derrubá-la, uma horda de 10, uns 10 s.
        /// </summary>
        public const float VidaDaMesa = 250f;

        /// <summary>
        /// Desmontada com pelo menos isto da vida (%), a mesa volta como o kit; abaixo, como as 2 madeiras de quando quebra (a
        /// regra do muro e do portão: sem ela, desmontar e montar de novo consertava a mesa de graça).
        /// </summary>
        public const float MesaVoltaInteiraCom = 50f;

        static Mesh _malhaDaMesa;
        static Material _materialDaMesa;

        /// <summary>
        /// A mesa montada: as três malhas do prefab da mesa de oficina (a mesa e as duas gavetas) numa só, com o meio da
        /// caixa no ponto e os pés no chão. Sem o prefab, a mesa de tábuas de antes.
        /// </summary>
        static Mesh MalhaDaMesa()
        {
            if (_malhaDaMesa != null) return _malhaDaMesa;
            var prefab = Resources.Load<GameObject>("MesaDeOficina");
            if (prefab == null) return _malhaDaMesa = MesaDeTabuas();
            var raiz = prefab.transform.worldToLocalMatrix;
            var partes = new List<CombineInstance>();
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (_materialDaMesa == null && mr != null) _materialDaMesa = mr.sharedMaterial;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                    partes.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = raiz * mf.transform.localToWorldMatrix });
            }
            if (partes.Count == 0)
            {
                // (a malha do modelo sem a leitura ligada na importação: a de tábuas)
                Debug.LogWarning("[Mesa] As malhas da mesa de oficina não têm a leitura ligada (Read/Write): a mesa montada fica a de tábuas.");
                _materialDaMesa = null;
                return _malhaDaMesa = MesaDeTabuas();
            }
            var m = new Mesh { name = "Mesa de oficina (montada)", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(partes.ToArray(), true, true);
            // o meio da caixa no ponto (x e z) e o chão no 0 (o prefab tem a origem nas costas)
            var b = m.bounds;
            var desloca = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++) v[i] += desloca;
            m.vertices = v;
            m.RecalculateBounds();
            return _malhaDaMesa = m;
        }

        /// <summary>O material da mesa montada (o da paleta do modelo da oficina); null: o das construções.</summary>
        static Material MaterialDaMesa
        {
            get
            {
                MalhaDaMesa();
                return _materialDaMesa;
            }
        }

        // a mesa de tábuas (a primeira versão, de 25/11/2026), só para quando o prefab da oficina não está no projeto
        static Mesh MesaDeTabuas()
        {
            var o = new MalhaDeObra(4096);
            o.Caixa(Em(0f, 0.92f, 0f), new Vector3(1.6f, 0.06f, 0.72f), "Madeira");
            o.Caixa(Em(-0.74f, 0.45f, 0.31f), new Vector3(0.08f, 0.9f, 0.08f), "MadeiraVelha");
            o.Caixa(Em(0.74f, 0.45f, 0.31f), new Vector3(0.08f, 0.9f, 0.08f), "MadeiraVelha");
            o.Caixa(Em(-0.74f, 0.87f, -0.31f), new Vector3(0.08f, 1.74f, 0.08f), "MadeiraVelha");
            o.Caixa(Em(0.74f, 0.87f, -0.31f), new Vector3(0.08f, 1.74f, 0.08f), "MadeiraVelha");
            o.Caixa(Em(0f, 0.22f, 0f), new Vector3(1.5f, 0.04f, 0.62f), "MadeiraVelha");
            o.Caixa(Em(0f, 1.34f, -0.355f), new Vector3(1.56f, 0.76f, 0.03f), "Madeira");
            return o.ParaMesh("Mesa de oficina (tábuas)");
        }

        /// <summary>Há uma mesa de oficina montada perto do ponto (os pés do jogador; a mesma conta da <see cref="Bancada"/>).</summary>
        public bool MesaPerto(Vector3 ponto)
        {
            if (!IsSpawned) return false;
            foreach (var c in _lista)
            {
                if (c.tipo != Item.MesaDeFerramentas) continue;
                var d = c.pos - ponto;
                d.y *= 2f;   // (a de outro andar não vale)
                if (d.sqrMagnitude <= Bancada.Alcance * Bancada.Alcance) return true;
            }
            return false;
        }

        /// <summary>A mesa de oficina montada mais perto do ponto (até o alcance da bancada), para a animação do craft; false sem nenhuma.</summary>
        public bool MesaMaisPerto(Vector3 ponto, out Vector3 pos, out float giro)
        {
            pos = default;
            giro = 0f;
            if (!IsSpawned) return false;
            float melhor = Bancada.Alcance * Bancada.Alcance;
            bool achou = false;
            foreach (var c in _lista)
            {
                if (c.tipo != Item.MesaDeFerramentas) continue;
                var d = c.pos - ponto;
                d.y *= 2f;
                if (d.sqrMagnitude > melhor) continue;
                melhor = d.sqrMagnitude;
                pos = c.pos;
                giro = c.giro;
                achou = true;
            }
            return achou;
        }

        /// <summary>Quantas mesas de oficina estão montadas (o tutorial do Gus, 25/11/2026).</summary>
        public int MesasMontadas()
        {
            if (!IsSpawned) return 0;
            int n = 0;
            foreach (var c in _lista)
                if (c.tipo == Item.MesaDeFerramentas) n++;
            return n;
        }

        /// <summary>A altura do tampo da mesa montada (m, do chão): a da mesa de oficina.</summary>
        public const float TampoDaMesa = Bancada.TampoDaOficina;
    }
}
