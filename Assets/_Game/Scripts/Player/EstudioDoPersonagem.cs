using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Roupas;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O estúdio da tela de criar o personagem (<see cref="TelaDeCriarPersonagem"/>; o Plano_Personagem.md da pasta de
    /// arte, a parte 2): uma cópia do corpo do jogador (o do prefab, sem nada na mão), longe de tudo (600 m embaixo do
    /// mundo), na camada do retrato, parada (o parado do mostruário das roupas), vestida com a aparência escolhida e com as
    /// roupas na luz fixa do retrato. Uma câmera desenha o corpo inteiro numa RenderTexture de fundo transparente, que a
    /// tela mostra no meio, girando (<see cref="Giro"/>); outra faz as miniaturas das opções (o cabelo e a barba na
    /// cabeça, a camiseta e a calça sozinhas).
    /// </summary>
    public class EstudioDoPersonagem
    {
        static readonly Vector3 Lugar = new Vector3(0f, -600f, 0f);
        public const int TamanhoDaMiniatura = 128;

        readonly GameObject _raiz;
        readonly Transform _modelo;
        readonly Animator _animador;
        readonly Vestimenta _vestimenta;
        readonly Renderer _corpo;
        readonly Camera _camera, _cameraDasMiniaturas;
        readonly RenderTexture _imagem, _miniatura;
        readonly Dictionary<Material, Material> _materiais = new Dictionary<Material, Material>();

        /// <summary>O giro do corpo, em graus (0: de frente; o corpo olha para a câmera, em +Z).</summary>
        public float Giro = -18f;
        public RenderTexture Imagem => _imagem;
        public CatalogoDasRoupas Catalogo => _vestimenta != null ? _vestimenta.Catalogo : null;

        public EstudioDoPersonagem(JogadorRede rede, RuntimeAnimatorController parado, int largura, int altura)
        {
            _raiz = new GameObject("Estúdio do personagem");
            _raiz.transform.position = Lugar;
            var prefab = NetworkManager.Singleton != null ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab : null;
            var referencia = prefab != null ? prefab.transform.Find(rede.animadorCorpo.name) : null;
            var copia = Object.Instantiate(referencia != null ? referencia.gameObject : rede.animadorCorpo.gameObject, _raiz.transform, false);
            copia.name = "Corpo";
            copia.SetActive(true);
            copia.transform.localPosition = Vector3.zero;
            copia.transform.localRotation = Quaternion.identity;
            _modelo = copia.transform;
            // nada na mão
            var naMao = new HashSet<string>();
            foreach (var a in rede.naMaoCorpo) if (a != null) naMao.Add(a.name);
            foreach (var t in copia.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = JogadorRede.CamadaRetrato;
                if (naMao.Contains(t.name) || t.name.StartsWith("P_")) t.gameObject.SetActive(false);
            }
            _animador = copia.GetComponent<Animator>();
            if (_animador != null)
            {
                if (parado != null) _animador.runtimeAnimatorController = parado;
                _animador.applyRootMotion = false;
                _animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            _vestimenta = copia.GetComponent<Vestimenta>();
            foreach (var r in copia.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = true;
                if (r is SkinnedMeshRenderer pele) pele.updateWhenOffscreen = true;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = NoRetrato(mats[i]);
                r.sharedMaterials = mats;
                if (r.name == Vestimenta.Corpo) _corpo = r;
            }

            _camera = NovaCamera("Câmera do estúdio", 22f);
            _camera.transform.localPosition = new Vector3(0f, 1.0f, 5.6f);
            _camera.transform.LookAt(Lugar + new Vector3(0f, 0.93f, 0f));
            _imagem = new RenderTexture(Mathf.Max(16, largura * 2), Mathf.Max(16, altura * 2), 24, RenderTextureFormat.ARGB32) { name = "Estúdio do personagem" };
            _imagem.Create();
            _camera.targetTexture = _imagem;

            _cameraDasMiniaturas = NovaCamera("Câmera das miniaturas", 24f);
            _cameraDasMiniaturas.enabled = false;   // só desenha quando pedida (Render)
            _miniatura = new RenderTexture(TamanhoDaMiniatura * 2, TamanhoDaMiniatura * 2, 24, RenderTextureFormat.ARGB32) { name = "Miniatura" };
            _miniatura.Create();
            _cameraDasMiniaturas.targetTexture = _miniatura;
        }

        Camera NovaCamera(string nome, float campo)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(_raiz.transform, false);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << JogadorRede.CamadaRetrato;
            cam.fieldOfView = campo;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 12f;
            var dados = go.AddComponent<UniversalAdditionalCameraData>();
            dados.renderPostProcessing = false;
            dados.renderShadows = false;
            dados.antialiasing = AntialiasingMode.None;
            // o renderizador 1 (o dos braços) não tem a neblina de altura
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.rendererDataList.Length > 1) dados.SetRenderer(1);
            return cam;
        }

        // o material das roupas na luz fixa do retrato (uma cópia por material)
        Material NoRetrato(Material de)
        {
            if (de == null || de.shader == null || de.shader.name != "ZombieCityCleaner/Roupas") return de;
            if (_materiais.TryGetValue(de, out var m)) return m;
            m = new Material(de);
            m.SetFloat("_Retrato", 1f);
            m.SetFloat("_Ambiente", 0.36f);
            m.SetFloat("_Luz", 0.86f);
            m.SetVector("_DirecaoDaLuz", new Vector4(0.3f, 0.85f, 0.65f, 0f));
            _materiais[de] = m;
            return m;
        }

        /// <summary>Veste o corpo do estúdio com a aparência.</summary>
        public void Vestir(AparenciaDoJogador a)
        {
            if (_vestimenta == null) return;
            var catalogo = _vestimenta.Catalogo;
            _vestimenta.corpo = a.Corpo;
            _vestimenta.pele = a.Pele(catalogo);
            _vestimenta.Vestir(a.Pecas(catalogo));
            if (_corpo != null) _corpo.enabled = true;
        }

        /// <summary>A cada quadro, com a tela aberta: o giro, o parado sem a camada dos braços, e a câmera desenha.</summary>
        public void Atualizar()
        {
            _modelo.localRotation = Quaternion.Euler(0f, Giro, 0f);
            if (_animador != null && _animador.isActiveAndEnabled && _animador.layerCount > 1) _animador.SetLayerWeight(1, 0f);
        }

        /// <summary>O enquadramento de uma miniatura: o ponto olhado (no corpo) e de onde a câmera olha.</summary>
        public enum Parte { Cabeca, Tronco, Pernas }

        /// <summary>
        /// Uma miniatura (fundo transparente): o corpo vestido com as peças pedidas, a parte dele enquadrada de três quartos.
        /// Na cabeça (o cabelo e a barba), com o corpo; no tronco e nas pernas (a camiseta e a calça), só a peça. Depois, a
        /// aparência volta (chame <see cref="Vestir"/>).
        /// </summary>
        public Texture2D Miniatura(AparenciaDoJogador a, List<CatalogoDasRoupas.PecaVestida> pecas, Parte parte, Texture2D destino)
        {
            if (_vestimenta == null) return destino;
            var catalogo = _vestimenta.Catalogo;
            _vestimenta.corpo = a.Corpo;
            _vestimenta.pele = a.Pele(catalogo);
            _vestimenta.Vestir(pecas);
            if (_corpo != null) _corpo.enabled = parte == Parte.Cabeca;
            var giro = _modelo.localRotation;
            _modelo.localRotation = Quaternion.identity;
            Vector3 alvo, de;
            switch (parte)
            {
                case Parte.Cabeca: alvo = new Vector3(0f, 1.67f, 0f); de = new Vector3(0.42f, 1.72f, 1.05f); _cameraDasMiniaturas.fieldOfView = 24f; break;
                case Parte.Tronco: alvo = new Vector3(0f, 1.24f, 0f); de = new Vector3(0.5f, 1.32f, 2.3f); _cameraDasMiniaturas.fieldOfView = 25f; break;
                default: alvo = new Vector3(0f, 0.56f, 0f); de = new Vector3(0.6f, 0.75f, 2.9f); _cameraDasMiniaturas.fieldOfView = 26f; break;
            }
            _cameraDasMiniaturas.transform.position = Lugar + de;
            _cameraDasMiniaturas.transform.LookAt(Lugar + alvo);
            _cameraDasMiniaturas.Render();
            var antes = RenderTexture.active;
            var meio = RenderTexture.GetTemporary(TamanhoDaMiniatura, TamanhoDaMiniatura, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(_miniatura, meio);   // metade do tamanho: sem serrilhado
            RenderTexture.active = meio;
            if (destino == null) destino = new Texture2D(TamanhoDaMiniatura, TamanhoDaMiniatura, TextureFormat.RGBA32, false) { name = "Miniatura" };
            destino.ReadPixels(new Rect(0, 0, TamanhoDaMiniatura, TamanhoDaMiniatura), 0, 0);
            destino.Apply();
            RenderTexture.active = antes;
            RenderTexture.ReleaseTemporary(meio);
            _modelo.localRotation = giro;
            if (_corpo != null) _corpo.enabled = true;
            return destino;
        }

        public void Destruir()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_cameraDasMiniaturas != null) _cameraDasMiniaturas.targetTexture = null;
            if (_imagem != null) _imagem.Release();
            if (_miniatura != null) _miniatura.Release();
            foreach (var m in _materiais.Values) Object.Destroy(m);
            if (_raiz != null) Object.Destroy(_raiz);
        }
    }
}
