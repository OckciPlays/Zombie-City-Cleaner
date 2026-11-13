using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O reflexo do cenário na água do lago (08/11/2026, a água nova; o PlanarReflections do BoatAttack da Unity): uma
    /// câmera espelhada na altura da água desenha a cena, invertida, numa textura de meia resolução antes de cada quadro
    /// da câmera do jogador (um pedido de render dentro do beginCameraRendering), sem as sombras, sem o pós-processamento
    /// e sem a camada da água; o Agua.shader lê a textura (_ZccReflexoTex) no mesmo pixel da tela, entortada pelas
    /// ondinhas, só na altura do plano (_ZccReflexoPlano: os rios, em outras alturas, refletem só o céu). Só na Ultra
    /// (<see cref="Ativo"/>, pelo Graficos; na Alta também até 09/11/2026). Um por cena, posto pelo mundo
    /// (<see cref="Instalar"/>).
    /// </summary>
    public class ReflexoDaAgua : MonoBehaviour
    {
        /// <summary>Ligado (a qualidade Ultra); desligado, a água reflete só o céu.</summary>
        public static bool Ativo = true;
        /// <summary>Até onde o reflexo desenha (m): a cena de floresta limita, para não desenhar a mata inteira de novo.</summary>
        public static float Alcance = float.PositiveInfinity;

        [Tooltip("A altura do plano da água (o lago).")] public float nivel;
        [Tooltip("A resolução da textura, em fração da tela.")] [Range(0.25f, 1f)] public float escala = 0.5f;

        static readonly int TexId = Shader.PropertyToID("_ZccReflexoTex");
        static readonly int PlanoId = Shader.PropertyToID("_ZccReflexoPlano");
        const float FolgaDoPlano = 0.07f;
        const int CamadaDaAgua = 4;

        static ReflexoDaAgua _instancia;
        Camera _cam;
        RenderTexture _rt;

        /// <summary>Põe (ou acerta) o reflexo da cena na altura da água do lago.</summary>
        public static void Instalar(float nivel)
        {
            if (_instancia == null) _instancia = new GameObject("Reflexo da água").AddComponent<ReflexoDaAgua>();
            _instancia.nivel = nivel;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Antes;
            Shader.SetGlobalFloat(PlanoId, -1e9f);
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Antes;
            Shader.SetGlobalFloat(PlanoId, -1e9f);
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
            if (_cam != null) { Destroy(_cam.gameObject); _cam = null; }
            if (_instancia == this) _instancia = null;
        }

        void Antes(ScriptableRenderContext contexto, Camera cam)
        {
            if (cam == _cam) return;
            if (cam.cameraType != CameraType.Game || cam != Camera.main) return;
            // por baixo da água (ou sem a qualidade), nada de reflexo
            if (!Ativo || cam.transform.position.y < nivel + 0.2f)
            {
                Shader.SetGlobalFloat(PlanoId, -1e9f);
                return;
            }
            Preparar(cam);
            Espelhar(cam);
            GL.invertCulling = true;
            var pedido = new UniversalRenderPipeline.SingleCameraRequest { destination = _rt };
            if (RenderPipeline.SupportsRenderRequest(_cam, pedido)) RenderPipeline.SubmitRenderRequest(_cam, pedido);
            GL.invertCulling = false;
            Shader.SetGlobalTexture(TexId, _rt);
            Shader.SetGlobalFloat(PlanoId, nivel);
        }

        // a câmera do reflexo (uma cópia da do jogador, sem as sombras, sem o pós e sem a água) e a textura dela
        void Preparar(Camera cam)
        {
            if (_cam == null)
            {
                var go = new GameObject("Câmera do reflexo") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, false);
                _cam = go.AddComponent<Camera>();
                _cam.enabled = false;
            }
            _cam.CopyFrom(cam);
            _cam.enabled = false;
            _cam.useOcclusionCulling = false;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            var fundo = RenderSettings.fogColor;
            fundo.a = 0f;
            _cam.backgroundColor = fundo;
            _cam.cullingMask = cam.cullingMask & ~(1 << CamadaDaAgua);
            _cam.depthTextureMode = DepthTextureMode.None;
            _cam.farClipPlane = Mathf.Min(cam.farClipPlane, Alcance);
            var dados = _cam.GetUniversalAdditionalCameraData();
            dados.renderShadows = false;
            dados.renderPostProcessing = false;
            dados.requiresColorOption = CameraOverrideOption.Off;
            dados.requiresDepthOption = CameraOverrideOption.Off;
            dados.antialiasing = AntialiasingMode.None;
            dados.renderType = CameraRenderType.Base;
            dados.cameraStack?.Clear();
            int w = Mathf.Max(64, Mathf.RoundToInt(cam.pixelWidth * escala)), h = Mathf.Max(64, Mathf.RoundToInt(cam.pixelHeight * escala));
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt != null) { _rt.Release(); Destroy(_rt); }
                _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "Reflexo da água", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                _rt.Create();
            }
            _cam.targetTexture = _rt;
        }

        // a câmera espelhada no plano da água, com o plano de corte oblíquo (nada de baixo da água entra no reflexo)
        void Espelhar(Camera cam)
        {
            var pos = new Vector3(0f, nivel, 0f);
            var normal = Vector3.up;
            float d = -Vector3.Dot(normal, pos) - FolgaDoPlano;
            var plano = new Vector4(normal.x, normal.y, normal.z, d);
            var reflexao = Matrix4x4.identity;
            MatrizDoEspelho(ref reflexao, plano);
            var antiga = cam.transform.position - new Vector3(0f, nivel * 2f, 0f);
            var nova = new Vector3(antiga.x, -antiga.y, antiga.z);
            _cam.transform.forward = Vector3.Scale(cam.transform.forward, new Vector3(1f, -1f, 1f));
            _cam.worldToCameraMatrix = cam.worldToCameraMatrix * reflexao;
            var corte = PlanoNaCamera(_cam, pos - Vector3.up * 0.1f, normal, 1f);
            _cam.projectionMatrix = cam.CalculateObliqueMatrix(corte);
            _cam.transform.position = nova;
        }

        static void MatrizDoEspelho(ref Matrix4x4 m, Vector4 plano)
        {
            m.m00 = 1f - 2f * plano[0] * plano[0];
            m.m01 = -2f * plano[0] * plano[1];
            m.m02 = -2f * plano[0] * plano[2];
            m.m03 = -2f * plano[3] * plano[0];
            m.m10 = -2f * plano[1] * plano[0];
            m.m11 = 1f - 2f * plano[1] * plano[1];
            m.m12 = -2f * plano[1] * plano[2];
            m.m13 = -2f * plano[3] * plano[1];
            m.m20 = -2f * plano[2] * plano[0];
            m.m21 = -2f * plano[2] * plano[1];
            m.m22 = 1f - 2f * plano[2] * plano[2];
            m.m23 = -2f * plano[3] * plano[2];
            m.m30 = 0f;
            m.m31 = 0f;
            m.m32 = 0f;
            m.m33 = 1f;
        }

        static Vector4 PlanoNaCamera(Camera cam, Vector3 pos, Vector3 normal, float lado)
        {
            var deslocado = pos + normal * FolgaDoPlano;
            var m = cam.worldToCameraMatrix;
            var p = m.MultiplyPoint(deslocado);
            var n = m.MultiplyVector(normal).normalized * lado;
            return new Vector4(n.x, n.y, n.z, -Vector3.Dot(p, n));
        }
    }
}
