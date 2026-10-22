using UnityEngine;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>Define a luz ambiente do shader de PS1 (também no editor, para a Scene view ficar igual ao jogo).</summary>
    [ExecuteAlways]
    public class PS1Atmosphere : MonoBehaviour
    {
        public Color ambientColor = new Color(0.035f, 0.035f, 0.045f);

        static readonly int AmbientId = Shader.PropertyToID("_PS1Ambient");

        void OnEnable() => Apply();
        void OnValidate() => Apply();
        void Start() => Apply();

        void Apply() => Shader.SetGlobalColor(AmbientId, ambientColor);
    }
}
