using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os restos dos corpos (12/11/2026, a etapa 4 da fauna; a decisão do Pedro no plano: "interessante pode ser"): quando o
    /// corpo de um zumbi ia afundar (o ZombieManager.Corpos.cs, 15 s depois de cair), ele vira um resto parado, a malha
    /// congelada na pose em que ficou, sem física nem colisão, que pesa pouco. Ficam os últimos <see cref="Maximo"/>, por
    /// <see cref="SegundosQueFicam"/> s (o relógio do jogo corre 1 hora por minuto: os minutos aqui são reais), e então
    /// afundam devagar e somem. Cada máquina tem os seus (os corpos são só visuais); no anfitrião, a lista diz aos urubus
    /// onde descer (Fauna/FaunaDoMundo.Noite.cs).
    /// </summary>
    public static class RestosDosCorpos
    {
        public const int Maximo = 20;
        public const float SegundosQueFicam = 600f;

        public struct Resto
        {
            public Vector3 pos;
            public float desde;
            public GameObject go;
            public Mesh malha;
        }

        static readonly List<Resto> _restos = new List<Resto>();
        static Transform _pasta;

        /// <summary>Os restos de agora (o ponto e quando ficou).</summary>
        public static IReadOnlyList<Resto> Lista => _restos;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Zerar()
        {
            _restos.Clear();
            _pasta = null;
        }

        /// <summary>Há um resto a até 'raio' m do ponto?</summary>
        public static bool Existe(Vector3 p, float raio)
        {
            float r2 = raio * raio;
            foreach (var r in _restos) if (r.go != null && (r.pos - p).sqrMagnitude < r2) return true;
            return false;
        }

        /// <summary>O corpo vira resto: a malha da pose de agora (BakeMesh), com os materiais dele, no mesmo lugar.</summary>
        public static void Criar(SkinnedMeshRenderer pele, Vector3 onde)
        {
            if (pele == null || !pele.gameObject.activeInHierarchy || pele.sharedMesh == null) return;
            if (_pasta == null) _pasta = new GameObject("Restos dos corpos").transform;
            var malha = new Mesh { name = "Resto do corpo" };
            pele.BakeMesh(malha, true);
            var go = new GameObject("Resto do corpo");
            go.transform.SetParent(_pasta, false);
            go.transform.SetPositionAndRotation(pele.transform.position, pele.transform.rotation);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = pele.sharedMaterials;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _restos.Add(new Resto { pos = onde, desde = Time.time, go = go, malha = malha });
            while (_restos.Count > Maximo) Tirar(0);
        }

        /// <summary>A cada quadro (o ZombieManager, em todas as máquinas): os velhos afundam e somem.</summary>
        public static void Atualizar(float agora, float dt)
        {
            for (int i = _restos.Count - 1; i >= 0; i--)
            {
                var r = _restos[i];
                if (r.go == null) { _restos.RemoveAt(i); continue; }
                float t = agora - r.desde;
                if (t > SegundosQueFicam + 4f) Tirar(i);
                else if (t > SegundosQueFicam) r.go.transform.position += Vector3.down * (0.25f * dt);
            }
        }

        /// <summary>Todos somem (a partida acabou).</summary>
        public static void Limpar()
        {
            for (int i = _restos.Count - 1; i >= 0; i--) Tirar(i);
        }

        static void Tirar(int i)
        {
            var r = _restos[i];
            if (r.go != null) Object.Destroy(r.go);
            if (r.malha != null) Object.Destroy(r.malha);
            _restos.RemoveAt(i);
        }
    }
}
