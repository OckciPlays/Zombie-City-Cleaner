using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O retrato do jogador na tela do inventário: uma cópia do corpo, longe de tudo (500 m embaixo da cidade), numa
    /// camada só dela (<see cref="JogadorRede.CamadaRetrato"/>), com o material do retrato (a cor da paleta e uma luz
    /// fixa: não muda com a hora, a neblina nem o bunker) e uma câmera própria que desenha numa RenderTexture de fundo
    /// transparente. Ela gira (<see cref="Giro"/>) e respira com o parado do que está na mão, que acompanha o corpo de
    /// verdade (o modelo na mão e o controlador de animação).
    /// A cópia é feita a cada vez que a tela abre (pega a cor e o que está na mão naquela hora) e some quando ela fecha.
    /// </summary>
    public class RetratoDoJogador
    {
        static readonly Vector3 Lugar = new Vector3(0f, -500f, 0f);
        static readonly int SpeedId = Animator.StringToHash("Speed");

        readonly GameObject _raiz;
        readonly Transform _modelo;
        readonly Animator _animador, _original;
        readonly Camera _camera;
        readonly GameObject[] _naMao, _naMaoOriginal;
        readonly List<Material> _materiais = new List<Material>();
        RenderTexture _imagem;

        /// <summary>O giro do modelo, em graus (0 = de frente).</summary>
        public float Giro;
        public RenderTexture Imagem => _imagem;

        public RetratoDoJogador(JogadorRede rede, Material material, int largura, int altura)
        {
            _original = rede.animadorCorpo;
            _raiz = new GameObject("Retrato do jogador");
            _raiz.transform.position = Lugar;
            var copia = Object.Instantiate(_original.gameObject, _raiz.transform, false);
            copia.name = "Corpo";
            copia.transform.localPosition = Vector3.zero;
            copia.transform.localRotation = Quaternion.identity;
            _modelo = copia.transform;
            _animador = copia.GetComponent<Animator>();
            _animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var t in copia.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = JogadorRede.CamadaRetrato;
            // o material do retrato com a textura (e o deslocamento da variação da cor) de cada material do corpo
            foreach (var r in copia.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = true;   // no dono, o que está na mão do corpo fica desligado
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = new Material(material);
                    if (mats[i] != null && mats[i].mainTexture != null)
                    {
                        m.mainTexture = mats[i].mainTexture;
                        m.mainTextureOffset = mats[i].mainTextureOffset;
                        m.mainTextureScale = mats[i].mainTextureScale;
                    }
                    mats[i] = m;
                    _materiais.Add(m);
                }
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer pele) pele.updateWhenOffscreen = true;
            }
            // o que está na mão: as mesmas peças na cópia, pelo nome
            _naMaoOriginal = rede.naMaoCorpo;
            _naMao = new GameObject[_naMaoOriginal.Length];
            for (int i = 0; i < _naMao.Length; i++) _naMao[i] = Buscar(copia.transform, _naMaoOriginal[i].name);

            var camGo = new GameObject("Câmera do retrato");
            camGo.transform.SetParent(_raiz.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.02f, 4.7f);
            camGo.transform.LookAt(Lugar + new Vector3(0f, 0.9f, 0f));
            _camera = camGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = 1 << JogadorRede.CamadaRetrato;
            _camera.fieldOfView = 25f;
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 10f;
            var dados = camGo.AddComponent<UniversalAdditionalCameraData>();
            dados.renderPostProcessing = false;
            dados.renderShadows = false;
            dados.antialiasing = AntialiasingMode.None;
            // o renderizador 1 (o dos braços) não tem a neblina de altura
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.rendererDataList.Length > 1) dados.SetRenderer(1);
            // o dobro da resolução da tela (desenhado menor, fica sem serrilhado)
            _imagem = new RenderTexture(Mathf.Max(16, largura * 2), Mathf.Max(16, altura * 2), 24, RenderTextureFormat.ARGB32) { name = "Retrato do jogador" };
            _imagem.Create();
            _camera.targetTexture = _imagem;
            Atualizar();
        }

        static GameObject Buscar(Transform raiz, string nome)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t.gameObject;
            return null;
        }

        /// <summary>A cada quadro, com a tela aberta: o giro, o que está na mão e o parado.</summary>
        public void Atualizar()
        {
            _modelo.localRotation = Quaternion.Euler(0f, Giro, 0f);   // o corpo olha para +Z, para a câmera
            for (int i = 0; i < _naMao.Length; i++)
                if (_naMao[i] != null && _naMao[i].activeSelf != _naMaoOriginal[i].activeSelf) _naMao[i].SetActive(_naMaoOriginal[i].activeSelf);
            if (_animador.runtimeAnimatorController != _original.runtimeAnimatorController)
                _animador.runtimeAnimatorController = _original.runtimeAnimatorController;
            _animador.SetFloat(SpeedId, 0f);
        }

        /// <summary>Onde os pés ficam na imagem (de 0 a 1, com o y de cima para baixo), para a sombra no chão.</summary>
        public Vector2 Pes
        {
            get
            {
                var v = _camera.WorldToViewportPoint(Lugar);
                return new Vector2(v.x, 1f - v.y);
            }
        }

        public void Destruir()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_imagem != null)
            {
                _imagem.Release();
                Object.Destroy(_imagem);
            }
            foreach (var m in _materiais) Object.Destroy(m);
            Object.Destroy(_raiz);
        }
    }
}
