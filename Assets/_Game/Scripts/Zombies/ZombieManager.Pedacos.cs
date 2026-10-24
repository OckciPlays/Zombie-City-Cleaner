using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os membros que caem (desmembramento), em todas as máquinas: um pool de pedaços com física, na camada dos corpos
    /// (só batem no cenário e uns nos outros). O pedaço nasce onde o osso do corte está, com a malha do que caiu (feita
    /// na hora pelo <see cref="Membros"/>), sai voando na direção do tiro e, como os corpos, para, afunda e volta ao pool.
    /// </summary>
    public partial class ZombieManager
    {
        [Header("Pedaços (desmembramento)")]
        public int pedacosNoPool = 24;
        [Tooltip("A velocidade do pedaço que cai: na direção do tiro e para cima (m/s).")]
        public Vector2 empurraoDoPedaco = new Vector2(2.2f, 1.6f);

        class Pedaco
        {
            public GameObject go;
            public MeshFilter filtro;
            public MeshRenderer pele;
            public Rigidbody corpo;
            public BoxCollider caixa;
            public float caiuEm;
            public bool emUso, parado;
        }

        readonly List<Pedaco> _pedacos = new List<Pedaco>();
        Transform _pastaDosPedacos;

        Pedaco PegarPedaco()
        {
            Pedaco livre = null;
            foreach (var p in _pedacos)
                if (!p.emUso) { livre = p; break; }
            if (livre == null && _pedacos.Count < pedacosNoPool)
            {
                if (_pastaDosPedacos == null) _pastaDosPedacos = new GameObject("Pedaços").transform;
                var go = new GameObject("Pedaço") { layer = CorpoDeZumbi.Camada };
                go.transform.SetParent(_pastaDosPedacos, false);
                livre = new Pedaco
                {
                    go = go,
                    filtro = go.AddComponent<MeshFilter>(),
                    pele = go.AddComponent<MeshRenderer>(),
                    corpo = go.AddComponent<Rigidbody>(),
                    caixa = go.AddComponent<BoxCollider>(),
                };
                livre.corpo.mass = 3f;
                livre.corpo.linearDamping = 0.1f;
                livre.corpo.angularDamping = 0.4f;
                livre.corpo.interpolation = RigidbodyInterpolation.Interpolate;
                livre.pele.shadowCastingMode = ShadowCastingMode.On;
                go.SetActive(false);
                _pedacos.Add(livre);
            }
            if (livre == null)
            {
                // todos em uso: o mais antigo
                livre = _pedacos[0];
                foreach (var p in _pedacos)
                    if (p.caiuEm < livre.caiuEm) livre = p;
            }
            return livre;
        }

        /// <summary>Um membro cai: o pedaço nasce no osso do corte, com a malha dele, e voa na direção do tiro.</summary>
        internal void SoltarPedaco(Mesh malha, Material[] materiais, Transform osso, Vector3 direcao, Vector3 velocidade)
        {
            if (malha == null || osso == null) return;
            var p = PegarPedaco();
            p.go.SetActive(false);
            p.filtro.sharedMesh = malha;
            p.pele.sharedMaterials = materiais;
            var b = malha.bounds;
            p.caixa.center = b.center;
            p.caixa.size = Vector3.Max(b.size, Vector3.one * 0.05f);
            p.go.transform.SetPositionAndRotation(osso.position, osso.rotation);
            p.go.SetActive(true);
            p.corpo.isKinematic = false;
            p.corpo.detectCollisions = true;
            p.corpo.position = osso.position;
            p.corpo.rotation = osso.rotation;
            direcao.y = 0f;
            p.corpo.linearVelocity = velocidade + direcao.normalized * empurraoDoPedaco.x + Vector3.up * empurraoDoPedaco.y;
            p.corpo.angularVelocity = Random.onUnitSphere * 6f;
            p.caiuEm = Time.time;
            p.emUso = true;
            p.parado = false;
        }

        void AtualizarPedacos(float agora, float dt)
        {
            foreach (var p in _pedacos)
            {
                if (!p.emUso) continue;
                float t = agora - p.caiuEm;
                if (t > tempoDoCorpo + 2.5f)
                {
                    p.emUso = false;
                    p.go.SetActive(false);
                }
                else if (t > tempoDoCorpo) p.go.transform.position += Vector3.down * (0.12f * dt);
                else if (t > tempoAteAssentar && !p.parado)
                {
                    p.parado = true;
                    p.corpo.isKinematic = true;
                    p.corpo.detectCollisions = false;
                }
            }
        }

        /// <summary>
        /// As malhas cortadas mais comuns e os pedaços delas, uma por quadro, no começo da partida: cortar uma malha leva de
        /// 3 a 15 ms e o pedaço de 6 a 20 ms, e na hora do tiro isso virava um tranco. Ficam prontos os cortes de um membro
        /// só (nos dois pontos), as duas pernas juntas e os pedaços de cada corte. As malhas ficam guardadas (são estáticas).
        /// </summary>
        System.Collections.IEnumerator PreaquecerCortes()
        {
            foreach (var modelo in modelos)
            {
                if (modelo == null || modelo.malhaInteira == null) continue;
                var ossos = modelo.esqueleto;
                for (int l = 0; l < Membros.Quantos; l++)
                    for (int e = 1; e <= 2; e++)
                    {
                        byte cortes = Membros.ComEstado(0, l, e);
                        int osso = Membros.IndiceDoOsso(ossos, Membros.OssoDoCorte(l, e));
                        Membros.Variante(modelo.malhaInteira, ossos, cortes);
                        yield return null;
                        Membros.Pedaco(modelo.malhaInteira, ossos, 0, cortes, osso);
                        yield return null;
                        if (e == 2)
                        {
                            Membros.Pedaco(modelo.malhaInteira, ossos, Membros.ComEstado(0, l, 1), cortes, osso);
                            yield return null;
                        }
                    }
                for (int a = 1; a <= 2; a++)
                    for (int b = 1; b <= 2; b++)
                    {
                        Membros.Variante(modelo.malhaInteira, ossos, Membros.ComEstado(Membros.ComEstado(0, Membros.PernaE, a), Membros.PernaD, b));
                        yield return null;
                    }
            }
        }

        void GuardarPedacos()
        {
            foreach (var p in _pedacos)
            {
                p.emUso = false;
                if (p.go != null) p.go.SetActive(false);
            }
        }
    }
}
