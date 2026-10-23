using UnityEngine;

namespace ZombieCityCleaner.UI
{
    /// <summary>A câmera que aparece no menu, antes de o jogador local nascer. Ela se desliga quando a câmera dele assume.</summary>
    public class CameraDoMenu : MonoBehaviour
    {
        static CameraDoMenu _instancia;

        void Awake() => _instancia = this;

        public static void Ligar(bool ligada)
        {
            if (_instancia != null) _instancia.gameObject.SetActive(ligada);
        }
    }
}
