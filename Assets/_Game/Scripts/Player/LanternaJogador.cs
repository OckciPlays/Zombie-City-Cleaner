using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Lanterna presa no peito, no estilo Silent Hill; liga e desliga com F (também dentro do carro).
    /// Fica abaixo e à frente dos olhos, longe da arma (colada na câmera, ela estourava a arma de luz).
    /// O facho converge no ponto da mira: um raio sai do olho, e a luz aponta do peito para onde ele bateu. Assim o centro
    /// da luz fica sempre na mira, perto ou longe (antes ela apontava 9° abaixo do olhar e com atraso, e o centro caía
    /// longe da mira).
    /// O dono usa a câmera dele direto, sem atraso. Os outros usam o <see cref="JogadorRede.Pitch"/>, que chega em degraus
    /// de 0,5°, então é suavizado.
    /// O facho tem um cookie gerado por código: o centro forte, o anel do refletor e a borda mais fraca e quente.
    /// Ela não ilumina os braços de primeira pessoa do dono: de tão perto, deixava a arma e as mangas brancas. Só durante o
    /// desenho da câmera dos braços ela fica desligada.
    /// No carro, ela sai do olho (do peito, ela ficava a 25 cm do volante e estourava o painel de branco), mais fraca
    /// (<see cref="fatorNoCarro"/>), com a sombra começando perto (o painel e o volante fazem sombra) e a mira passando
    /// pela lataria do carro.
    /// </summary>
    public class LanternaJogador : NetworkBehaviour
    {
        public JogadorRede rede;
        public Light luz;
        [Tooltip("Posição em relação aos pés do jogador (x direita, y altura, z frente).")]
        public Vector3 posicaoNoPeito = new Vector3(0.08f, 1.28f, 0.22f);
        [Tooltip("Suavização do olhar dos outros jogadores (o Pitch chega em degraus de 0,5°).")]
        public float atraso = 14f;
        [Tooltip("Até onde o raio da mira procura (m). Sem acerto, a luz converge nessa distância.")]
        public float alcanceDaMira = 30f;
        [Tooltip("Mais perto que isso, a luz não tenta convergir (o peito fica muito abaixo do olho).")]
        public float minimoDaMira = 1.2f;

        [Header("Facho")]
        public float intensidade = 34f;
        public float abertura = 62f;
        public float aberturaInterna = 52f;
        [Tooltip("A força da luz dentro do carro (o painel fica perto).")]
        public float fatorNoCarro = 0.3f;

        [Header("Ao ligar")]
        [Tooltip("Por quanto tempo a luz pisca quando liga (s).")]
        public float piscadaAoLigar = 0.22f;

        public readonly NetworkVariable<bool> Ligada = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        const float AlturaDoOlho = 1.62f;
        // o raio da mira não vê os jogadores, os braços, os corpos e a cápsula do zumbi de perto (Ignore Raycast)
        const int MascaraDaMira = ~((1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos) | (1 << 8) | (1 << 2));
        const int MascaraNoCarro = MascaraDaMira & ~(1 << Vehicles.Carro.CamadaVeiculo);

        static Texture2D _cookie;

        InputAction _alternar;
        float _pitch, _distancia;
        float _proximaTrocaBot, _ligouEm = -10f, _sombraPerto;
        bool _desligadaNosBracos, _estavaLigada;

        void Awake()
        {
            _alternar = InputSystem.actions.FindAction("Player/Flashlight", true);
            _distancia = alcanceDaMira;
            luz.cookie = Cookie();
            luz.intensity = intensidade;
            luz.spotAngle = abertura;
            luz.innerSpotAngle = aberturaInterna;
            _sombraPerto = luz.shadowNearPlane;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += AntesDaCamera;
            RenderPipelineManager.endCameraRendering += DepoisDaCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= AntesDaCamera;
            RenderPipelineManager.endCameraRendering -= DepoisDaCamera;
        }

        public override void OnNetworkSpawn()
        {
            _pitch = rede.Pitch.Value;
            _estavaLigada = Ligada.Value;
            // a lanterna do dono não faz sombra do corpo dele (invisível na primeira pessoa: as mãos e os braços faziam
            // sombra no facho, sobretudo mirando e dentro do carro); a dos outros, nesta máquina, faz sombra de tudo menos
            // da arma da mão deles (colada na lanterna, o cano das longas fazia sombra no facho; ela também não a ilumina,
            // porque a camada da arma não está na luz dela: veja JogadorRede.MascaraDaArmaDosOutros)
            var dados = luz.GetUniversalAdditionalLightData();
            dados.customShadowLayers = true;
            dados.shadowRenderingLayers = IsOwner ? ~JogadorRede.MascaraDoDono : ~JogadorRede.MascaraDaArmaDosOutros;
        }

        void AntesDaCamera(ScriptableRenderContext contexto, Camera camera)
        {
            if (camera != rede.cameraBracos || !luz.enabled) return;
            luz.enabled = false;
            _desligadaNosBracos = true;
        }

        void DepoisDaCamera(ScriptableRenderContext contexto, Camera camera)
        {
            if (!_desligadaNosBracos) return;
            luz.enabled = true;
            _desligadaNosBracos = false;
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            // desde 01/11/2026, a lanterna é um item: sem ela (ou sem pilhas), não acende; as pilhas acabando, ela falha antes
            // de apagar (a gosma do Long esvazia as pilhas de uma vez)
            int carga = Video.ModoVideo.Ligado || rede.Inventario == null ? 100 : rede.Inventario.CargaDaLanterna;   // a cena de vídeo tem a lanterna de sempre
            if (IsOwner)
            {
                bool controla = MenuOnline.Controla;
                bool trocar = MenuOnline.Bot ? Time.time >= _proximaTrocaBot : controla && _alternar.WasPressedThisFrame();
                if (MenuOnline.Bot && trocar) _proximaTrocaBot = Time.time + 5f;
                if (trocar)
                {
                    if (!Ligada.Value && carga < 0) InventarioJogador.AvisoLocal("Você não tem uma lanterna");
                    else if (!Ligada.Value && carga == 0) InventarioJogador.AvisoLocal("A lanterna está sem pilhas");
                    else Ligada.Value = !Ligada.Value;
                    Efeitos.Som(Efeitos.Lanterna, luz.transform.position, 0.6f, false);
                }
                // sem lanterna: apaga na hora; sem pilhas: falha um pouco e apaga
                if (Ligada.Value && carga < 0) Ligada.Value = false;
                if (Ligada.Value && carga == 0)
                {
                    if (_semCargaDesde < 0f) _semCargaDesde = Time.time;
                    else if (Time.time - _semCargaDesde > 1.3f) Ligada.Value = false;
                }
                else _semCargaDesde = -1f;
            }

            bool ligada = Ligada.Value;
            if (ligada && !_estavaLigada) _ligouEm = Time.time;
            _estavaLigada = ligada;
            luz.enabled = ligada;
            if (!ligada) return;
            bool noCarro = rede.Passageiro != null && rede.Passageiro.NoCarro;
            luz.intensity = intensidade * Piscada(Time.time - _ligouEm) * Falha(carga) * (noCarro ? fatorNoCarro : 1f);
            luz.shadowNearPlane = noCarro ? 0.1f : _sombraPerto;

            // de onde o jogador olha, e para onde
            Vector3 olho;
            Quaternion olhar;
            if (IsOwner)
            {
                // no carro, a cabeça (com a câmera de trás, a câmera fica longe dela); a pé, o olho (na terceira pessoa,
                // a cabeça olhando para o meio da tela)
                var cam = noCarro ? rede.cameraMundo.transform.parent : rede.Olho;
                olho = cam.position;
                olhar = cam.rotation;
            }
            else
            {
                _pitch = Mathf.LerpAngle(_pitch, rede.Pitch.Value, 1f - Mathf.Exp(-atraso * Time.deltaTime));
                olho = transform.TransformPoint(0f, AlturaDoOlho, 0f);
                olhar = transform.rotation * Quaternion.Euler(_pitch, 0f, 0f);
            }
            Vector3 frente = olhar * Vector3.forward;

            // até onde a mira vai; a distância é suavizada (rápido) para o facho não pular ao passar por uma quina
            float alvo = Physics.Raycast(olho, frente, out var acerto, alcanceDaMira, noCarro ? MascaraNoCarro : MascaraDaMira, QueryTriggerInteraction.Ignore)
                ? Mathf.Max(acerto.distance, minimoDaMira) : alcanceDaMira;
            _distancia = Mathf.Lerp(_distancia, alvo, 1f - Mathf.Exp(-18f * Time.deltaTime));

            Vector3 peito = noCarro ? olho + olhar * new Vector3(0f, -0.06f, 0.1f) : transform.TransformPoint(posicaoNoPeito);
            Vector3 ponto = olho + frente * _distancia;
            luz.transform.SetPositionAndRotation(peito, Quaternion.LookRotation(ponto - peito, olhar * Vector3.up));
        }

        float _semCargaDesde = -1f;

        /// <summary>As pilhas no fim (abaixo de 8%) ou acabando de vez: a luz enfraquece e falha.</summary>
        float Falha(int carga)
        {
            if (carga > 8) return 1f;
            float ruido = Mathf.PerlinNoise(Time.time * (carga <= 0 ? 14f : 6f), 3.7f);
            if (carga <= 0) return ruido > 0.55f ? 0.6f : 0.05f;
            return Mathf.Lerp(0.35f, 1f, carga / 8f) * (ruido > 0.85f ? 0.3f : 1f);
        }

        /// <summary>Ao ligar, a lâmpada falha duas vezes antes de firmar.</summary>
        float Piscada(float t)
        {
            if (t >= piscadaAoLigar) return 1f;
            float k = t / piscadaAoLigar;
            if (k < 0.18f) return 0.55f;
            if (k < 0.32f) return 0.08f;
            if (k < 0.55f) return 0.85f;
            if (k < 0.68f) return 0.2f;
            return 1f;
        }

        /// <summary>
        /// O desenho do facho, projetado pelo cone: um centro forte (~1/3 do cone), o anel um pouco mais claro do refletor
        /// em volta dele, a luz de fora (mais fraca e um pouco mais quente) e umas manchas leves de lente suja.
        /// (Anéis finos em toda a luz, como num refletor barato, viravam um alvo no chão: saíram.)
        /// </summary>
        static Texture2D Cookie()
        {
            if (_cookie != null) return _cookie;
            const int n = 128;
            var px = new Color32[n * n];
            var aleatorio = new System.Random(7);
            // 5 manchas fracas na lente
            var manchas = new Vector3[5];
            for (int i = 0; i < manchas.Length; i++)
                manchas[i] = new Vector3((float)aleatorio.NextDouble() * 1.2f - 0.6f, (float)aleatorio.NextDouble() * 1.2f - 0.6f, 0.08f + (float)aleatorio.NextDouble() * 0.12f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float centro = 1f - Mathf.SmoothStep(0.22f, 0.36f, r);
                float anel = Mathf.Exp(-Mathf.Pow((r - 0.36f) / 0.06f, 2f)) * 0.1f;
                float fora = 0.4f * (1f - Mathf.SmoothStep(0.55f, 1f, r));
                float b = Mathf.Clamp01(Mathf.Max(centro, fora) + anel);
                foreach (var m in manchas)
                {
                    float d = new Vector2(u - m.x, v - m.y).magnitude / m.z;
                    if (d < 1f) b *= 1f - 0.12f * (1f - d * d);
                }
                // fora do centro a luz é um pouco mais quente (a borda do refletor)
                float quente = Mathf.SmoothStep(0.25f, 0.6f, r);
                px[y * n + x] = new Color(b, b * (1f - 0.05f * quente), b * (1f - 0.14f * quente), b);
            }
            _cookie = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Cookie da lanterna", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _cookie.SetPixels32(px);
            _cookie.Apply(true, true);
            return _cookie;
        }
    }
}
