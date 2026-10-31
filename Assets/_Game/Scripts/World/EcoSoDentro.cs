using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O eco de um lugar fechado (um andar de prédio, uma casa) só vale para quem está dentro dele (a pedido em 29/10/2026).
    /// A zona de eco do Unity é uma esfera e vazava pelas paredes, pelo teto e pelo telhado: em cima de um prédio, o eco de
    /// sala dos andares de baixo pegava tudo o que se ouvia, e os zumbis da rua soavam como se estivessem ali. A caixa de
    /// dentro (<see cref="dentro"/>) é no espaço do pai.
    /// </summary>
    [RequireComponent(typeof(AudioReverbZone))]
    public class EcoSoDentro : MonoBehaviour
    {
        public Bounds dentro;
        AudioReverbZone _eco;
        int _vez;

        /// <summary>Põe a zona de eco de sala neste objeto, que só vale com o ouvido dentro da caixa (no espaço de 'pai').</summary>
        public static AudioReverbZone Criar(Transform pai, Bounds dentro)
        {
            var go = new GameObject("Eco");
            go.transform.SetParent(pai, false);
            go.transform.localPosition = dentro.center;
            var eco = go.AddComponent<AudioReverbZone>();
            eco.reverbPreset = AudioReverbPreset.Room;
            eco.minDistance = Mathf.Min(dentro.size.x, dentro.size.z) * 0.4f;
            eco.maxDistance = Mathf.Max(dentro.size.x, dentro.size.z) * 0.6f;
            go.AddComponent<EcoSoDentro>().dentro = dentro;
            return eco;
        }

        void Awake()
        {
            _eco = GetComponent<AudioReverbZone>();
            _eco.enabled = false;
            _vez = Random.Range(0, 4);
        }

        void Update()
        {
            if (((Time.frameCount + _vez) & 3) != 0) return;   // um quadro em quatro basta
            var cam = Camera.main;
            if (cam == null || transform.parent == null) return;
            _eco.enabled = dentro.Contains(transform.parent.InverseTransformPoint(cam.transform.position));
        }
    }
}
