using UnityEngine;

namespace ZombieCityCleaner.Network
{
    /// <summary>Onde cada jogador aparece ao entrar na partida: um ponto por filho, escolhido pelo id do jogador.</summary>
    public class PontosDeNascimento : MonoBehaviour
    {
        static PontosDeNascimento _instancia;

        void OnEnable() => _instancia = this;
        void OnDisable()
        {
            if (_instancia == this) _instancia = null;
        }

        public static Pose Ponto(ulong id)
        {
            if (_instancia == null || _instancia.transform.childCount == 0) return new Pose(Vector3.zero, Quaternion.identity);
            var p = _instancia.transform.GetChild((int)(id % (ulong)_instancia.transform.childCount));
            return new Pose(p.position, p.rotation);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.4f);
            foreach (Transform p in transform)
            {
                Gizmos.DrawWireSphere(p.position + Vector3.up * 0.9f, 0.35f);
                Gizmos.DrawLine(p.position + Vector3.up * 1.6f, p.position + Vector3.up * 1.6f + p.forward * 0.6f);
            }
        }
    }
}
