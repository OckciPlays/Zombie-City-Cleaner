using UnityEngine;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O monitor cardíaco (06/11/2026, a pedido: "ao invés de ser uma barra comum, ser um ritmo cardíaco real ... com onda P,
    /// QRS e onda T; quando está taquicárdico, diminuição do RR, ondas regulares e mais curtas"), só no dono, no lugar da
    /// barra da vida (embaixo, à esquerda). A cor é a vida: verde acima de 60, amarelo de 30 a 60, vermelho abaixo de 30.
    /// <para>O traçado é desenhado como no monitor de verdade: a varredura anda da esquerda para a direita (o monitor inteiro em
    /// <see cref="SegundosNaTela"/>, como os 25 mm/s do papel) e apaga um vão à frente. Cada batimento soma as ondas do ECG
    /// (gaussianas no tempo, em mV): a P (0,13), o Q, o R (1,0), o S e a T (0,30). O PR encurta um pouco com a frequência, e o
    /// QT vai pela fórmula de Bazett (0,40 × √RR): com o coração acelerado, o RR diminui, as ondas ficam mais curtas e a T
    /// chega perto da P seguinte, regulares. Com a vida abaixo de 15, às vezes uma extrassístole (o QRS largo sem a P, a T
    /// invertida e a pausa que compensa).</para>
    /// <para>A frequência (o bpm, ao lado) sobe com o esforço (correr, até +62), com a mente abalada (abaixo de 70), com a
    /// perda de sangue (a vida abaixo de 60: o coração compensa), com a febre e logo depois de um golpe; e volta aos poucos.
    /// A etapa 2 (a crise de pânico) soma o <see cref="Extra"/>.</para>
    /// </summary>
    public class MonitorCardiaco : MonoBehaviour
    {
        const int L = 240, A = 60;                     // a textura do traçado (px)
        public const float SegundosNaTela = 4.2f;      // a varredura atravessa o monitor nisso
        const int Vao = 12;                            // px apagados à frente da varredura
        const float MenorMv = -0.6f, MaiorMv = 1.3f;   // o que cabe na altura
        const int Sub = 4;                             // amostras por coluna (o R não some entre duas colunas)

        struct Batida
        {
            public float r, rr;   // quando é o pico do R (no relógio da onda) e o intervalo que veio antes
            public bool extra;    // a extrassístole
        }

        /// <summary>O do jogador desta máquina.</summary>
        public static MonitorCardiaco Local { get; private set; }

        /// <summary>A frequência agora (batimentos por minuto).</summary>
        public float Bpm => _bpm;
        /// <summary>A etapa 2 (a crise de pânico) soma batimentos aqui.</summary>
        public float Extra { get; set; }
        /// <summary>Quando foi o último R (Time.time): o cérebro pulsa junto.</summary>
        public float UltimaBatida { get; private set; } = -99f;

        VidaJogador _vida;
        JogadorRede _rede;
        Texture2D _tex;
        Color32[] _px;
        bool _mudou;
        float _x, _relogio, _yAntes = -1f, _bpm = 72f, _esforco, _proximaColuna;
        Batida _b0, _b1, _prox;
        Color _cor = CorBoa;
        readonly System.Random _sorte = new System.Random(1905);

        static readonly Color CorBoa = new Color(0.36f, 0.95f, 0.52f), CorAtencao = new Color(0.98f, 0.83f, 0.3f), CorPerigo = new Color(1f, 0.32f, 0.26f);

        void Awake()
        {
            _vida = GetComponent<VidaJogador>();
            _rede = GetComponent<JogadorRede>();
            _tex = new Texture2D(L, A, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Monitor cardíaco" };
            _px = new Color32[L * A];
            for (int i = 0; i < _px.Length; i++) _px[i] = new Color32(255, 255, 255, 0);
            _tex.SetPixels32(_px);
            _tex.Apply(false);
            // os primeiros batimentos (o relógio começa em 0)
            float rr = 60f / _bpm;
            _b0 = new Batida { r = -2f * rr, rr = rr };
            _b1 = new Batida { r = -rr, rr = rr };
            _prox = new Batida { r = 0.3f, rr = rr };
        }

        void OnEnable() => Local = this;

        void OnDisable()
        {
            if (Local == this) Local = null;
        }

        void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }

        void Update()
        {
            if (_vida == null || !_vida.IsSpawned) return;
            float dt = Time.deltaTime;
            AtualizarFrequencia(dt);
            // a cor pela vida (verde, amarelo, vermelho)
            float vida = _vida.Vida.Value;
            var alvo = vida > 60f ? CorBoa : vida >= 30f ? CorAtencao : CorPerigo;
            _cor = Color.Lerp(_cor, alvo, Mathf.Clamp01(dt * 5f));
            // a varredura: as colunas que passaram neste quadro (no máximo meio monitor, se o quadro demorou)
            _proximaColuna += dt * L / SegundosNaTela;
            int colunas = Mathf.Min((int)_proximaColuna, L / 2);
            _proximaColuna -= (int)_proximaColuna;
            for (int c = 0; c < colunas; c++) Coluna();
            if (_mudou)
            {
                _tex.SetPixels32(_px);
                _tex.Apply(false);
                _mudou = false;
            }
        }

        // ------------------------------------------------ o coração ------------------------------------------------

        void AtualizarFrequencia(float dt)
        {
            float alvo = 70f + 2.5f * Mathf.Sin(Time.time * 0.9f);   // a respiração mexe um pouco no ritmo
            bool correndo = _rede != null && _rede.Velocidade > 3.2f;
            _esforco = Mathf.MoveTowards(_esforco, correndo ? 1f : 0f, dt * (correndo ? 1f / 8f : 1f / 25f));
            alvo += 62f * _esforco;
            float mente = _vida.Sanidade.Value;
            if (mente < 70f) alvo += (70f - mente) * 0.55f;
            float vida = _vida.Vida.Value;
            if (vida < 60f) alvo += (60f - vida) * 0.8f;
            var e = _vida.Corpo.Value;
            bool febre = e.ComFebre || e.Sepse;
            if (febre) alvo += 22f;   // (06/11/2026: a febre vence o sono: dormindo com febre, o coração continua acelerado)
            float g = Time.time - _vida.UltimoGolpeNoDono;
            if (g < 8f) alvo += 18f * (1f - g / 8f);
            if (_vida.Dormindo.Value && !febre && vida >= 60f) alvo -= 12f;   // (dormindo, o coração desce, se nada o acelera)
            alvo += Extra;
            alvo = Mathf.Clamp(alvo, 48f, 178f);
            _bpm = Mathf.MoveTowards(_bpm, alvo, dt * 6f);
        }

        // o próximo batimento depois deste: o RR pela frequência de agora (com um pouco de variação); com a vida muito baixa,
        // às vezes uma extrassístole antes da hora, e depois dela a pausa que compensa
        Batida Proxima(Batida depois)
        {
            float normal = 60f / Mathf.Max(30f, _bpm);
            float varia = 1f + ((float)_sorte.NextDouble() - 0.5f) * 0.03f;
            if (depois.extra)
            {
                float pausa = Mathf.Max(normal * 0.5f, 2f * normal - depois.rr);
                return new Batida { r = depois.r + pausa, rr = pausa };
            }
            if (_vida.Vida.Value < 15f && _sorte.NextDouble() < 0.18)
            {
                float cedo = normal * 0.62f;
                return new Batida { r = depois.r + cedo, rr = cedo, extra = true };
            }
            float rr = normal * varia;
            return new Batida { r = depois.r + rr, rr = rr };
        }

        // o ECG no relógio t (mV): a soma das ondas dos batimentos em volta
        float Ecg(float t) => Onda(t - _b0.r, _b0) + Onda(t - _b1.r, _b1) + Onda(t - _prox.r, _prox);

        static float Onda(float d, Batida b)
        {
            if (d < -0.36f || d > 0.62f) return 0f;
            if (b.extra)   // o QRS largo, sem a P; a T ao contrário
                return G(d, 0f, 0.028f, 1.15f) + G(d, 0.07f, 0.03f, -0.45f) + G2(d, 0.3f, 0.08f, 0.06f, -0.32f);
            float rr = Mathf.Max(0.3f, b.rr);
            float hr = 60f / rr;
            float pr = Mathf.Lerp(0.17f, 0.12f, Mathf.InverseLerp(60f, 170f, hr));
            float qt = Mathf.Clamp(0.4f * Mathf.Sqrt(rr), 0.24f, 0.46f);   // Bazett
            float sT = Mathf.Clamp(0.055f * Mathf.Sqrt(rr), 0.03f, 0.065f);
            float picoT = -0.03f + qt * 0.7f;
            return G(d, -pr + 0.05f, 0.022f, 0.13f)        // P
                 + G(d, -0.028f, 0.008f, -0.12f)           // Q
                 + G(d, 0f, 0.0095f, 1f)                   // R
                 + G(d, 0.027f, 0.0095f, -0.26f)           // S
                 + G2(d, picoT, sT * 1.25f, sT * 0.8f, 0.3f);   // T (sobe devagar, desce mais rápido)
        }

        static float G(float d, float m, float s, float a)
        {
            float z = (d - m) / s;
            return a * Mathf.Exp(-0.5f * z * z);
        }

        static float G2(float d, float m, float sAntes, float sDepois, float a) => G(d, m, d < m ? sAntes : sDepois, a);

        // ------------------------------------------------ o traçado ------------------------------------------------

        void Coluna()
        {
            float passo = SegundosNaTela / L;
            float menor = float.MaxValue, maior = float.MinValue;
            for (int k = 1; k <= Sub; k++)
            {
                float t = _relogio + passo * k / Sub;
                while (t >= _prox.r)   // passou o R: este vira o anterior, e vem o próximo
                {
                    _b0 = _b1;
                    _b1 = _prox;
                    _prox = Proxima(_b1);
                    UltimaBatida = Time.time;
                }
                float y = Linha(Ecg(t) + ((float)_sorte.NextDouble() - 0.5f) * 0.012f);
                menor = Mathf.Min(menor, y);
                maior = Mathf.Max(maior, y);
            }
            _relogio += passo;
            // o relógio dá a volta (com horas de jogo, o float perderia a precisão do R)
            if (_relogio > 1000f)
            {
                _relogio -= 1000f;
                _b0.r -= 1000f;
                _b1.r -= 1000f;
                _prox.r -= 1000f;
            }
            if (_yAntes >= 0f)
            {
                menor = Mathf.Min(menor, _yAntes);
                maior = Mathf.Max(maior, _yAntes);
            }
            int x = (int)_x;
            _yAntes = Linha(Ecg(_relogio));
            // apaga o vão à frente e desenha a coluna
            for (int k = 0; k <= Vao; k++) Limpar((x + k) % L);
            for (int y = 0; y < A; y++)
            {
                float d = Mathf.Max(0f, Mathf.Max(menor - y, y - maior));
                float a = Mathf.Max(Mathf.Clamp01(1.3f - d), 0.22f * Mathf.Exp(-d * 0.55f));
                if (a > 0.004f) _px[y * L + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            _x = (_x + 1f) % L;
            _mudou = true;
        }

        void Limpar(int x)
        {
            for (int y = 0; y < A; y++) _px[y * L + x] = new Color32(255, 255, 255, 0);
        }

        // mV → a linha da textura (a de baixo é a 0)
        static float Linha(float mv) => Mathf.Lerp(5f, A - 6f, Mathf.InverseLerp(MenorMv, MaiorMv, mv));

        // ------------------------------------------------ o HUD ------------------------------------------------

        /// <summary>Onde o monitor fica na tela (no lugar da barra da vida).</summary>
        public static Rect Area(float u) => new Rect(20f * u, Screen.height - 68f * u, 150f * u, 38f * u);

        void OnGUI()
        {
            if (_vida == null || !_vida.IsSpawned || !_vida.IsOwner || _vida.Caido.Value) return;
            if (MenuOnline.Aberto || MenuOnline.Cinema || ModoVideo.Ligado || GravacaoNoMundo.SemHud) return;
            float u = Screen.height / 720f;
            var r = Area(u);
            var antes = GUI.color;
            // o fundo do monitor, com a grade fraquinha
            GUI.color = new Color(0f, 0.02f, 0.01f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(_cor.r, _cor.g, _cor.b, 0.07f);
            for (int i = 1; i < 6; i++) GUI.DrawTexture(new Rect(r.x + r.width * i / 6f, r.y, 1f, r.height), Texture2D.whiteTexture);
            for (int i = 1; i < 3; i++) GUI.DrawTexture(new Rect(r.x, r.y + r.height * i / 3f, r.width, 1f), Texture2D.whiteTexture);
            // o traçado e a ponta da varredura
            GUI.color = _cor;
            GUI.DrawTexture(r, _tex);
            float cx = r.x + r.width * (_x / L), cy = r.y + r.height * (1f - (_yAntes < 0f ? A / 2f : _yAntes) / A);
            GUI.color = Color.Lerp(_cor, Color.white, 0.6f);
            GUI.DrawTexture(new Rect(cx - 2f * u, cy - 2f * u, 4f * u, 4f * u), Texture2D.whiteTexture);
            // o bpm ao lado, que pisca a cada batida
            float pisca = Mathf.Clamp01(1f - (Time.time - UltimaBatida) / 0.25f);
            var grande = EstilosDoHud.Rotulo(Mathf.RoundToInt(19 * u), FontStyle.Bold);
            var pequeno = EstilosDoHud.Rotulo(Mathf.RoundToInt(10 * u), FontStyle.Bold);
            var corDoNumero = Color.Lerp(_cor * 0.85f, Color.white, pisca * 0.5f);
            corDoNumero.a = 1f;
            Sombreado(new Rect(r.xMax + 6f * u, r.y + 2f * u, 60f * u, 24f * u), Mathf.RoundToInt(_bpm).ToString(), grande, corDoNumero);
            Sombreado(new Rect(r.xMax + 7f * u, r.y + 23f * u, 60f * u, 14f * u), "bpm", pequeno, new Color(_cor.r, _cor.g, _cor.b, 0.8f));
            GUI.color = antes;
        }

        static void Sombreado(Rect r, string texto, GUIStyle estilo, Color cor)
        {
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), texto, estilo);
            estilo.normal.textColor = cor;
            GUI.Label(r, texto, estilo);
        }
    }
}
