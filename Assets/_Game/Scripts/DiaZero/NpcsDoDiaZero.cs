using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Roupas;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// As pessoas e os carros da rua do Dia 0 (a pedido: "pessoas normais, irrigando o quintal com a mangueira, passeando de
    /// carro, cortando a grama"; "acenando, conversando uns entre os outros"; uns 5 doentes, com os olhos vermelhos e a
    /// tosse). No objeto da rua (com a <see cref="RuaDoDiaZero"/>), só no Play: com a rua pronta, um NPC (o
    /// Prefabs/Roupas/NPC, vestido pelo sorteio do tipo, <see cref="Vestimenta.VestirSorteado"/>) em cada lugar da rua
    /// (<see cref="RuaDoDiaZero.Npcs"/>), cada um com a rotina dele (<see cref="NpcDoDiaZero"/>).
    /// Nada vai pela rede: quem anda está onde o relógio da partida diz (<see cref="Agora"/>), igual em todas as máquinas.
    /// </summary>
    public class NpcsDoDiaZero : MonoBehaviour
    {
        [Tooltip("O NPC vestível (Prefabs/Roupas/NPC).")]
        public GameObject npc;
        [Tooltip("O material das partículas (Art/Materials/Particulas): a água da mangueira e os olhos vermelhos.")]
        public Material particulas;
        [Tooltip("Quantos carros passam na rua.")]
        public int carros = 3;

        readonly List<NpcDoDiaZero> _npcs = new List<NpcDoDiaZero>();
        readonly List<Transform> _carros = new List<Transform>();
        RuaDoDiaZero _rua;
        bool _feito;

        /// <summary>O relógio da partida (o mesmo em todas as máquinas), ou o do jogo fora da rede.</summary>
        public static double Agora
        {
            get
            {
                var r = NetworkManager.Singleton;
                return r != null && r.IsListening ? r.ServerTime.Time : Time.timeAsDouble;
            }
        }

        public int Quantos => _npcs.Count;
        public IReadOnlyList<NpcDoDiaZero> Todos => _npcs;

        void Update()
        {
            if (!Application.isPlaying) return;
            if (_rua == null) _rua = GetComponent<RuaDoDiaZero>();
            if (!_feito && _rua != null && _rua.Pronta)
            {
                _feito = true;
                Criar();
            }
            if (_feito) Carros();
        }

        void OnDestroy()
        {
            foreach (var n in _npcs) if (n != null) Destroy(n.gameObject);
            foreach (var c in _carros) if (c != null) Destroy(c.gameObject);
        }

        void Criar()
        {
            if (npc == null) return;
            var pai = new GameObject("NPCs").transform;
            pai.SetParent(transform, false);
            for (int i = 0; i < _rua.Npcs.Count; i++)
            {
                var l = _rua.Npcs[i];
                var go = Instantiate(npc, l.ponto, Quaternion.Euler(0f, l.giro, 0f), pai);
                go.name = $"NPC {i + 1} ({l.rotina}{(l.doente ? ", doente" : "")})";
                var n = go.AddComponent<NpcDoDiaZero>();
                n.Iniciar(_rua, l, i, particulas);
                _npcs.Add(n);
            }
            for (int i = 0; i < _npcs.Count; i++)
            {
                var l = _rua.Npcs[i];
                if (l.par >= 0 && l.par < _npcs.Count) _npcs[i].Par = _npcs[l.par].transform;
            }
            // os carros que passam (os modelos dos carros da rua)
            if (_rua.carros != null && _rua.carros.Length > 0)
                for (int i = 0; i < carros; i++)
                {
                    var c = new GameObject($"Carro passando {i + 1}");
                    c.transform.SetParent(pai, false);
                    c.AddComponent<MeshFilter>().sharedMesh = _rua.carros[(i * 3 + 1) % _rua.carros.Length];
                    c.AddComponent<MeshRenderer>().sharedMaterial = _rua.material;
                    var som = c.AddComponent<AudioSource>();
                    som.clip = SonsDoDiaZero.MotorDoCarro;
                    som.loop = true;
                    som.spatialBlend = 1f;
                    som.rolloffMode = AudioRolloffMode.Linear;
                    som.minDistance = 3f;
                    som.maxDistance = 45f;
                    som.volume = 0.55f;
                    som.pitch = 0.9f + 0.1f * i;
                    som.time = i * 0.3f % Mathf.Max(0.1f, som.clip.length);
                    som.Play();
                    _carros.Add(c.transform);
                }
        }

        // os carros: cada um numa faixa (para leste na de baixo, para oeste na de cima, a mão de direção daqui), da neblina
        // de uma ponta à da outra, e de novo
        void Carros()
        {
            if (_carros.Count == 0) return;
            float a = _rua.Inicio - 70f, b = _rua.Fim + 70f, comprimento = b - a;
            double t = Agora;
            for (int i = 0; i < _carros.Count; i++)
            {
                float vel = 9.5f + i * 1.3f;
                bool leste = i % 2 == 0;
                float s = (float)((t * vel + i * 97.0) % comprimento);
                float x = leste ? a + s : b - s;
                _carros[i].SetPositionAndRotation(new Vector3(x, 0.02f, leste ? -2.0f : 2.0f), Quaternion.Euler(0f, leste ? 90f : 270f, 0f));
            }
        }
    }
}
