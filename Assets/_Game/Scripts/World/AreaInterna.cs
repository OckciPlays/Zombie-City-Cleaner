using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O volume de dentro de uma casa ou do prédio (uma caixa no espaço local deste objeto). Com a câmera aqui dentro, o
    /// AmbienteDoDia afina a neblina: a de fora entra pelas janelas, mas não enche a sala.
    /// Posto pela montagem da cidade em cada casa e no prédio; o bunker usa a regra do subsolo.
    /// </summary>
    public class AreaInterna : MonoBehaviour
    {
        static readonly List<AreaInterna> Todas = new List<AreaInterna>();

        [Tooltip("Tamanho da caixa (local), centrada neste objeto.")]
        public Vector3 tamanho = new Vector3(10f, 3f, 8f);

        void OnEnable() => Todas.Add(this);
        void OnDisable() => Todas.Remove(this);

        /// <summary>Se o ponto (mundo) está dentro de alguma área interna.</summary>
        public static bool Contem(Vector3 ponto)
        {
            foreach (var a in Todas)
            {
                Vector3 p = a.transform.InverseTransformPoint(ponto);
                Vector3 m = a.tamanho * 0.5f;
                if (Mathf.Abs(p.x) <= m.x && Mathf.Abs(p.y) <= m.y && Mathf.Abs(p.z) <= m.z) return true;
            }
            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, tamanho);
        }
    }
}
