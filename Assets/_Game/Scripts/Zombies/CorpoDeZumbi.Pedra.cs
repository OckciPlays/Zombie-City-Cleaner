using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// A estátua que quebra (07/11/2026, a pedido: na morte de pedra eles ficavam "duros, bonecos estáticos"). Ela tomba
    /// dura, na postura em que estava (<see cref="Estatua"/>), e ao bater no chão (uma parte que freia de uma vez) se parte:
    /// a cabeça quase sempre, cada antebraço às vezes, as canelas menos. Cada parte que sai vira um pedaço de pedra que
    /// rola (o mesmo pool dos membros cortados, ZombieManager.Pedacos.cs, com a malha cortada pelo <see cref="Membros"/>; a
    /// cabeça, os triângulos dela no espaço do osso), e o corpo fica com o toco de pedra. A espingarda de perto e o carro
    /// (o arremesso) a despedaçam na hora, em todas as partes. Poeira e o som da casca em cada quebra. Longe da câmera
    /// (mais de <see cref="DistanciaParaQuebrar"/> m), ela só tomba inteira (os pedaços e a poeira não valem o custo).
    /// </summary>
    public partial class CorpoDeZumbi
    {
        const float DistanciaParaQuebrar = 60f;
        const float FreadaQueQuebra = 2.4f;   // m/s perdidos num passo de física por uma parte: bateu no chão
        const float TempoVigiando = 4f;

        static readonly Dictionary<Mesh, Mesh> _cabecasDePedra = new Dictionary<Mesh, Mesh>();
        bool _vigiando, _quebrou;
        Vector3[] _velocidades;
        byte _cortes;
        bool _cabecaFora;

        /// <summary>Começa a vigiar a queda da estátua (chamado na queda, depois de a física ligar).</summary>
        void VigiarAEstatua(bool arremesso, Vector3 direcao)
        {
            _quebrou = false;
            _vigiando = true;
            _velocidades ??= new Vector3[partes.Length];
            for (int i = 0; i < partes.Length; i++) _velocidades[i] = partes[i].linearVelocity;
            if (arremesso) Despedacar(true, direcao);
        }

        /// <summary>A cada passo de física da estátua caindo: a primeira batida forte a quebra.</summary>
        void PassoDaEstatua()
        {
            if (!_vigiando) return;
            if (_quebrou || Time.time - caiuEm > TempoVigiando)
            {
                _vigiando = false;
                return;
            }
            for (int i = 0; i < partes.Length; i++)
            {
                var p = partes[i];
                if (p.isKinematic) continue;
                Vector3 v = p.linearVelocity;
                if ((_velocidades[i] - v).magnitude > FreadaQueQuebra && _velocidades[i].sqrMagnitude > 1f)
                {
                    Vector3 direcao = _velocidades[i];
                    direcao.y = 0f;
                    Despedacar(false, direcao);
                    return;
                }
                _velocidades[i] = v;
            }
        }

        // um sorteio que muda com o lugar e a parte (o mesmo em todas as máquinas, que veem a estátua no mesmo lugar)
        static float Sorte(Vector3 onde, float semente)
        {
            float x = Mathf.Sin(onde.x * 12.9898f + onde.z * 78.233f + semente * 4.1414f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        /// <summary>
        /// Quebra a estátua: as partes que saem viram pedaços de pedra (tudo: a cabeça e os quatro membros inteiros; senão,
        /// a cabeça quase sempre, os antebraços às vezes, as canelas menos).
        /// </summary>
        internal void Despedacar(bool tudo, Vector3 direcao)
        {
            if (_quebrou || !_estatua) return;
            _quebrou = true;
            var m = ZombieManager.Instancia;
            var cam = Camera.main;
            if (Application.isPlaying && cam != null && (cam.transform.position - partes[0].position).sqrMagnitude > DistanciaParaQuebrar * DistanciaParaQuebrar)
                return;
            if (direcao.sqrMagnitude < 0.01f) direcao = -transform.forward;
            Vector3 onde = partes[0].position;
            byte antes = _cortes, depois = antes;
            var pedra = new[] { materiaisInteira[0], materiaisInteira[0] };
            for (int l = 0; l < Membros.Quantos; l++)
            {
                bool braco = l < 2;
                int novo = tudo ? (braco ? 2 : 1)
                    : Sorte(onde, l + 1) < (braco ? 0.6f : 0.3f) ? 1 : 0;
                int velho = Membros.Estado(antes, l);
                if (novo <= velho) continue;
                int indice = Membros.IndiceDoOsso(esqueleto, Membros.OssoDoCorte(l, novo));
                if (indice < 0) continue;
                var osso = esqueleto[indice];
                var malha = Membros.Pedaco(malhaInteira, esqueleto, Membros.ComEstado(depois, l, velho), Membros.ComEstado(depois, l, novo), indice);
                int parte = 3 + l * 2 + (novo == 1 ? 1 : 0);
                Vector3 vel = parte < partes.Length ? partes[parte].linearVelocity : Vector3.zero;
                m?.SoltarPedaco(malha, pedra, osso, direcao, vel * 0.6f, true);
                depois = Membros.ComEstado(depois, l, novo);
                Efeitos.Impacto(osso.position, Vector3.up);
            }
            bool cabeca = !_cabecaFora && (tudo || Sorte(onde, 7f) < 0.85f);
            if (cabeca)
            {
                var malha = CabecaDePedra();
                var osso = partes[ParteCabeca].transform;
                if (malha != null)
                {
                    m?.SoltarPedaco(malha, new[] { materiaisInteira[0] }, osso, direcao, partes[ParteCabeca].linearVelocity * 0.6f, true);
                    _cabecaFora = true;
                    Efeitos.Impacto(osso.position, Vector3.up);
                }
            }
            if (depois == antes && !cabeca) return;
            _cortes = depois;
            // o toco também é de pedra (a "carne" do corte no material do Oco, que está todo de pedra)
            pele.sharedMesh = Membros.Variante(_cabecaFora ? malhaSemCabeca : malhaInteira, esqueleto, _cortes);
            pele.sharedMaterials = pedra;
            for (int l = 0; l < Membros.Quantos; l++)
            {
                int e = Membros.Estado(_cortes, l);
                int cima = 3 + l * 2, baixo = cima + 1;
                Colidir(baixo, e < 1);
                Colidir(cima, e < 2);
            }
            if (_cabecaFora) Colidir(ParteCabeca, false);
            var sons = m != null ? m.Sons : null;
            Efeitos.Som(sons != null && sons.soltando != null ? sons.soltando : Efeitos.SocoParede, onde + Vector3.up * 0.3f,
                        ZombieManager.VolumeDosZumbis, true, 30f);
        }

        /// <summary>A cabeça como um pedaço: os triângulos presos a ela, no espaço do osso (um por malha, guardado).</summary>
        Mesh CabecaDePedra()
        {
            if (malhaInteira == null) return null;
            if (_cabecasDePedra.TryGetValue(malhaInteira, out var pronta) && pronta != null) return pronta;
            int ih = Membros.IndiceDoOsso(esqueleto, "head");
            if (ih < 0 || !malhaInteira.isReadable) return null;
            var vs = malhaInteira.vertices;
            var ns = malhaInteira.normals;
            var uv = malhaInteira.uv;
            var pesos = malhaInteira.boneWeights;
            var tris = malhaInteira.triangles;
            var bind = malhaInteira.bindposes[ih];
            var novo = new Dictionary<int, int>();
            var v2 = new List<Vector3>();
            var n2 = new List<Vector3>();
            var u2 = new List<Vector2>();
            var t2 = new List<int>();
            int Ponto(int v)
            {
                if (novo.TryGetValue(v, out int i)) return i;
                i = v2.Count;
                novo[v] = i;
                v2.Add(bind.MultiplyPoint3x4(vs[v]));
                n2.Add(ns.Length > v ? bind.MultiplyVector(ns[v]).normalized : Vector3.up);
                u2.Add(uv.Length > v ? uv[v] : Vector2.zero);
                return i;
            }
            for (int i = 0; i < tris.Length; i += 3)
            {
                if (pesos[tris[i]].boneIndex0 != ih || pesos[tris[i + 1]].boneIndex0 != ih || pesos[tris[i + 2]].boneIndex0 != ih) continue;
                t2.Add(Ponto(tris[i])); t2.Add(Ponto(tris[i + 1])); t2.Add(Ponto(tris[i + 2]));
            }
            if (t2.Count == 0) return null;
            var cabeca = new Mesh { name = malhaInteira.name + " (cabeça de pedra)" };
            cabeca.SetVertices(v2);
            cabeca.SetNormals(n2);
            cabeca.SetUVs(0, u2);
            cabeca.SetTriangles(t2, 0);
            cabeca.RecalculateBounds();
            cabeca.UploadMeshData(true);
            _cabecasDePedra[malhaInteira] = cabeca;
            return cabeca;
        }
    }
}
