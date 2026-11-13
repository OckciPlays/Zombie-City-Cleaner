using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Roupas;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O estúdio da tela das cartas (<see cref="TelaDosMoradores"/>; o Plano_NPCs_Bunker.md da pasta de arte): o NPC vestível
    /// das roupas, longe de tudo (600 m embaixo do mundo, ao lado do estúdio do personagem), na camada do retrato, parado,
    /// vestido pela ficha (<see cref="RoupaDoMorador"/>) e com as roupas na luz fixa do retrato. Uma câmera tira o retrato
    /// dele (da cintura para cima) numa RenderTexture de fundo transparente: uma por carta, um retrato por quadro
    /// (<see cref="Retratar"/>; o desenho é no fim do quadro), e a carta escolhida pode ficar girando (o mesmo, a cada quadro).
    /// </summary>
    public class EstudioDoMorador
    {
        static readonly Vector3 Lugar = new Vector3(40f, -600f, 0f);
        /// <summary>O tamanho dos retratos das cartas (a proporção do lugar do retrato na carta).</summary>
        public const int LarguraDoRetrato = 352, AlturaDoRetrato = 336;

        readonly GameObject _raiz;
        readonly Transform _modelo;
        readonly Animator _animador;
        readonly Vestimenta _vestimenta;
        readonly Camera _camera;
        readonly Dictionary<Material, Material> _materiais = new Dictionary<Material, Material>();
        AparenciaDoMorador _vestido;

        /// <summary>O giro do corpo, em graus (0: de frente; o corpo olha para a câmera, em +Z).</summary>
        public float Giro = -18f;
        public bool Pronto => _vestimenta != null;

        public EstudioDoMorador(GameObject npc)
        {
            _raiz = new GameObject("Estúdio dos moradores");
            _raiz.transform.position = Lugar;
            if (npc != null)
            {
                var copia = Object.Instantiate(npc, _raiz.transform, false);
                copia.name = "Morador";
                copia.transform.localPosition = Vector3.zero;
                copia.transform.localRotation = Quaternion.identity;
                _modelo = copia.transform;
                int camada = Player.JogadorRede.CamadaRetrato;
                foreach (var t in copia.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = camada;
                _animador = copia.GetComponent<Animator>();
                if (_animador != null)
                {
                    _animador.applyRootMotion = false;
                    _animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                _vestimenta = copia.GetComponent<Vestimenta>();
                foreach (var r in copia.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    if (r is SkinnedMeshRenderer pele) pele.updateWhenOffscreen = true;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = NoRetrato(mats[i]);
                    r.sharedMaterials = mats;
                }
            }

            var go = new GameObject("Câmera do estúdio dos moradores");
            go.transform.SetParent(_raiz.transform, false);
            _camera = go.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = 1 << Player.JogadorRede.CamadaRetrato;
            // da cintura para cima: 1,1 m de alto, de 0,82 a 1,96 m
            _camera.fieldOfView = 18f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 12f;
            var dados = go.AddComponent<UniversalAdditionalCameraData>();
            dados.renderPostProcessing = false;
            dados.renderShadows = false;
            dados.antialiasing = AntialiasingMode.None;
            // o renderizador 1 (o dos braços) não tem a neblina de altura
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.rendererDataList.Length > 1) dados.SetRenderer(1);
            _camera.transform.localPosition = new Vector3(0f, 1.42f, 3.6f);
            _camera.transform.LookAt(Lugar + new Vector3(0f, 1.38f, 0f));
            _camera.enabled = false;
        }

        /// <summary>Uma imagem para o retrato de uma carta (de fundo transparente).</summary>
        public static RenderTexture NovaImagem()
        {
            var rt = new RenderTexture(LarguraDoRetrato, AlturaDoRetrato, 24, RenderTextureFormat.ARGB32) { name = "Retrato da carta", antiAliasing = 4 };
            rt.Create();
            return rt;
        }

        // o material das roupas na luz fixa do retrato (uma cópia por material), como no estúdio do personagem
        Material NoRetrato(Material de)
        {
            if (de == null || de.shader == null || !de.shader.name.StartsWith("ZombieCityCleaner/Roupas")) return de;
            if (_materiais.TryGetValue(de, out var m)) return m;
            m = new Material(de);
            m.SetFloat("_Retrato", 1f);
            m.SetFloat("_Ambiente", 0.36f);
            m.SetFloat("_Luz", 0.86f);
            m.SetVector("_DirecaoDaLuz", new Vector4(0.3f, 0.85f, 0.65f, 0f));
            _materiais[de] = m;
            return m;
        }

        /// <summary>
        /// Veste o corpo pela aparência (se mudou), gira e manda a câmera tirar o retrato na <paramref name="destino"/> no fim
        /// do quadro (a câmera continua ligada até <see cref="Parar"/>).
        /// </summary>
        public void Retratar(AparenciaDoMorador a, RenderTexture destino, float giro)
        {
            if (_vestimenta != null && a != null && !ReferenceEquals(a, _vestido))
            {
                RoupaDoMorador.Vestir(_vestimenta, a);
                _vestido = a;
            }
            Giro = giro;
            if (_modelo != null) _modelo.localRotation = Quaternion.Euler(0f, Giro, 0f);
            if (_animador != null && _animador.isActiveAndEnabled && _animador.layerCount > 1) _animador.SetLayerWeight(1, 0f);
            if (_camera.targetTexture != destino)
            {
                _camera.targetTexture = destino;
                _camera.ResetAspect();
            }
            _camera.enabled = destino != null;
        }

        /// <summary>Desliga a câmera (nenhum retrato a tirar).</summary>
        public void Parar() => _camera.enabled = false;

        public void Destruir()
        {
            if (_camera != null)
            {
                _camera.enabled = false;
                _camera.targetTexture = null;
            }
            foreach (var m in _materiais.Values) Object.Destroy(m);
            if (_raiz != null) Object.Destroy(_raiz);
        }
    }
}
