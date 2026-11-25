using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// A figura de um Oco na noite do colapso (24/11/2026, o Pedro: "precisa de maior ambientação, mais npcs (alguns mortos no
    /// chão e sendo devorados)"): só o desenho (o Oco_00_Original, sem a IA, sem colisão), em cada máquina igual:
    ///  - <b>o devorador</b>: de quatro em cima de um corpo (o "RastejarParado") ou curvado mordendo (o "Attack" de novo a
    ///    cada mordida);
    ///  - <b>o perseguidor</b>: correndo (o "Chase") atrás de quem foge, no rastro dele, 1,2 s atrás.
    /// Quando um jogador chega a <see cref="Acorda"/> m, a figura sai e, no anfitrião, nasce um Oco de verdade no lugar
    /// dela (<see cref="LakewoodNaCena.NascerUmOcoNaFuga"/>), virado para o jogador: ele se levanta do corpo e vem.
    /// </summary>
    public class FiguraDeOco : MonoBehaviour
    {
        public const float Acorda = 14f;

        /// <summary>O corpo que ele devora (o devorador).</summary>
        public Vector3 corpo;
        public bool deQuatro;
        /// <summary>Quem ele persegue (o perseguidor).</summary>
        public Transform presa;

        Animator _an;
        float _proximaMordida;
        readonly Queue<(float t, Vector3 p)> _rastro = new Queue<(float, Vector3)>();

        public static FiguraDeOco Criar(Transform pai, Vector3 pos, float giro, string nome)
        {
            var prefab = Resources.Load<GameObject>("Oco_00_Original");
            if (prefab == null) return null;
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, giro, 0f), pai);
            go.name = nome;
            var teste = go.GetComponent<OcoDeTeste>();
            if (teste != null) { teste.enabled = false; Destroy(teste); }
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>())
                if (mb != null && !(mb is FiguraDeOco) && mb.GetType().Namespace != null && mb.GetType().Namespace.StartsWith("ZombieCityCleaner")) mb.enabled = false;
            var f = go.AddComponent<FiguraDeOco>();
            f._an = go.GetComponentInChildren<Animator>();
            if (f._an != null)
            {
                f._an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                f._an.applyRootMotion = false;
            }
            return f;
        }

        void Start()
        {
            if (_an == null) return;
            if (presa != null) _an.Play("Chase", 0, Random.value);
            else if (deQuatro) { _an.Play("RastejarParado", 0, Random.value); _an.speed = 0.8f; }
            else { _an.Play("Attack", 0, Random.value); _an.speed = 0.75f; }
            _proximaMordida = Time.time + Random.Range(1.2f, 2.4f);
        }

        void Update()
        {
            // alguém chegou perto: a figura sai e (no anfitrião) nasce o de verdade
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.Criado) continue;
                var d = j.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > Acorda * Acorda) continue;
                var rede = Unity.Netcode.NetworkManager.Singleton;
                if (rede != null && rede.IsServer && LakewoodNaCena.Instancia != null)
                    LakewoodNaCena.Instancia.NascerUmOcoNaFuga(transform.position, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                Destroy(gameObject);
                return;
            }
            if (presa != null)
            {
                // no rastro de quem foge, 1,2 s atrás (o caminho dele é um laço: o rastro nunca corta)
                if (!presa.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
                _rastro.Enqueue((Time.time, presa.position));
                while (_rastro.Count > 1 && Time.time - _rastro.Peek().t > 1.2f) _rastro.Dequeue();
                var alvo = _rastro.Peek().p;
                var mover = alvo - transform.position;
                mover.y = 0f;
                if (mover.sqrMagnitude > 0.0004f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(mover), Time.deltaTime * 8f);
                transform.position = alvo;
                return;
            }
            // o devorador: a mordida de novo a cada 1,2 a 2,4 s (o de pé), virado para o corpo
            if (!deQuatro && _an != null && Time.time >= _proximaMordida)
            {
                _proximaMordida = Time.time + Random.Range(1.2f, 2.4f);
                _an.CrossFadeInFixedTime("Attack", 0.15f, 0, 0f);
            }
        }
    }

    public partial class WoodcreekDoDiaZero
    {
        readonly List<FiguraDeOco> _figuras = new List<FiguraDeOco>();

        /// <summary>
        /// As figuras dos Ocos da noite (de novo a cada tentativa da fuga, <see cref="LakewoodNaCena"/>): um devorador em cada
        /// corpo (metade de quatro, metade curvada) e um perseguidor atrás de cada terceiro de quem foge. Devolve quantas.
        /// </summary>
        public int RefazerAsFiguras()
        {
            foreach (var f in _figuras) if (f != null) Destroy(f.gameObject);
            _figuras.Clear();
            if (_caos == null) return 0;
            for (int i = 0; i < LugaresDasVitimas.Count; i++)
            {
                var (corpo, giroDoCorpo) = LugaresDasVitimas[i];
                bool deQuatro = i % 2 == 0;
                float ang = (giroDoCorpo + 90f + i * 37f) * Mathf.Deg2Rad;
                var pos = corpo + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (deQuatro ? 0.75f : 0.95f);
                pos.y = corpo.y - 0.1f;
                var para = corpo - pos;
                var f = FiguraDeOco.Criar(_caos, pos, Mathf.Atan2(para.x, para.z) * Mathf.Rad2Deg, $"Devorando o corpo {i + 1}");
                if (f == null) break;
                f.corpo = corpo;
                f.deQuatro = deQuatro;
                _figuras.Add(f);
            }
            var npcs = GetComponent<NpcsDoDiaZero>();
            if (npcs != null)
            {
                int k = 0;
                foreach (var n in npcs.Todos)
                {
                    if (n == null || n.Lugar == null || n.Lugar.rotina != Rotina.Correr) continue;
                    if (k++ % 3 != 1) continue;
                    var f = FiguraDeOco.Criar(_caos, n.transform.position - n.transform.forward * 4f, n.transform.eulerAngles.y, $"Perseguindo {n.name}");
                    if (f == null) break;
                    f.presa = n.transform;
                    _figuras.Add(f);
                }
            }
            return _figuras.Count;
        }
    }
}
