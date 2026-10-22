using UnityEngine;
using UnityEngine.UI;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Renderiza a câmera numa textura de baixa resolução (estilo PS1) e mostra ela esticada na tela,
    /// com filtro Point para os pixels ficarem quadrados.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PS1Screen : MonoBehaviour
    {
        [Tooltip("Altura da imagem em pixels. O PS1 rodava a 240.")]
        [Range(120, 720)] public int verticalResolution = 240;

        Camera _camera;
        RenderTexture _target;
        RawImage _image;
        GameObject _output;

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
            BuildOutput();
            Resize();
        }

        void OnDisable()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_output != null) Destroy(_output);
            if (_target != null) { _target.Release(); Destroy(_target); }
        }

        void LateUpdate()
        {
            int width = Mathf.Max(1, Mathf.RoundToInt(verticalResolution * (float)Screen.width / Mathf.Max(1, Screen.height)));
            if (_target == null || _target.width != width || _target.height != verticalResolution) Resize();
        }

        void BuildOutput()
        {
            // Uma câmera vazia desenha na tela (evita o aviso "No cameras rendering"); o Canvas mostra a textura.
            _output = new GameObject("PS1 Output");
            var outputCamera = _output.AddComponent<Camera>();
            outputCamera.cullingMask = 0;
            outputCamera.clearFlags = CameraClearFlags.SolidColor;
            outputCamera.backgroundColor = Color.black;
            outputCamera.depth = _camera.depth + 1;

            var canvas = _output.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;

            var imageObject = new GameObject("PS1 Image", typeof(RectTransform));
            imageObject.transform.SetParent(_output.transform, false);
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _image = imageObject.AddComponent<RawImage>();
            _image.raycastTarget = false;
        }

        void Resize()
        {
            int width = Mathf.Max(1, Mathf.RoundToInt(verticalResolution * (float)Screen.width / Mathf.Max(1, Screen.height)));
            if (_target != null)
            {
                _camera.targetTexture = null;
                _target.Release();
                Destroy(_target);
            }
            _target = new RenderTexture(width, verticalResolution, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
                name = "PS1 Screen"
            };
            _target.Create();
            _camera.targetTexture = _target;
            _image.texture = _target;
        }
    }
}
