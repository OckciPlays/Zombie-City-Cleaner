using UnityEngine;
using UnityEngine.Rendering.Universal;

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

        int _mascara;
        CameraClearFlags _fundo;
        bool _pos, _leve;

        /// <summary>
        /// Leve: enquanto o vídeo do menu cobre a tela (UI/FundoDoMenu), a câmera não desenha nada (nem o céu, nem o
        /// pós-processamento): só limpa a tela de preto. Desligado, volta ao que era.
        /// </summary>
        public static void Leve(bool leve)
        {
            var c = _instancia != null ? _instancia.GetComponent<Camera>() : null;
            if (c == null || leve == _instancia._leve) return;
            _instancia._leve = leve;
            var dados = c.GetUniversalAdditionalCameraData();
            if (leve)
            {
                _instancia._mascara = c.cullingMask;
                _instancia._fundo = c.clearFlags;
                _instancia._pos = dados.renderPostProcessing;
                c.cullingMask = 0;
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = Color.black;
                dados.renderPostProcessing = false;
            }
            else
            {
                c.cullingMask = _instancia._mascara;
                c.clearFlags = _instancia._fundo;
                dados.renderPostProcessing = _instancia._pos;
            }
        }
    }
}
