using System;
using UnityEngine;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// Os ícones do corpo do jogador (05/11/2026, o sistema de vida por membro): a gota (sangrando), a faixa (enfaixado), a
    /// tala, o osso partido (a fratura; a exposta leva a gota junto), o anel (a entorse) e a cruz (o kit médico). Gerados por
    /// código uma vez, de 64 px, pela distância com sinal, como as marcas do mapa (<see cref="MarcasDoMapa"/>): o miolo
    /// branco, para tingir com o <c>GUI.color</c>, e o contorno escuro de 3 px, que se lê em cima de qualquer cor. Também as
    /// riscas do membro inutilizado (a que se repete e a redonda, da cabeça) e a vinheta das bordas da tela (a cabeça a 0).
    /// Na etapa 2: o termômetro (a febre), o escudo (protegida até o amanhecer), a gotinha (limpa pelo spray: falta
    /// enfaixar) e o micróbio (a ferida infeccionada).
    /// O HUD (<see cref="Player.CorpoNoHud"/>) e a aba CORPO da tela do inventário usam.
    /// </summary>
    public static class IconesDoCorpo
    {
        static Texture2D _gota, _faixa, _tala, _osso, _entorse, _cruz, _riscas, _riscasRedondas, _vinheta;
        static Texture2D _termometro, _escudo, _gotinha, _infeccao;

        /// <summary>A gota de sangue (a ponta em cima): a ferida que sangra.</summary>
        public static Texture2D Gota => _gota != null ? _gota : _gota = Desenhar("Gota (corpo)", (x, y) =>
        {
            float bola = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 41f)) - 16f;
            // a ponta: do alto da bola até em cima, abrindo como a gota
            float t = Mathf.InverseLerp(6f, 41f, y);
            float meia = 15.5f * Mathf.Sin(t * Mathf.PI * 0.5f);
            float ponta = y < 6f || y > 41f ? 99f : Mathf.Abs(x - 32f) - meia;
            return Mathf.Min(bola, ponta);
        });

        /// <summary>A faixa enrolada: uma tira larga com as voltas do pano marcadas (o enfaixado).</summary>
        public static Texture2D Faixa => _faixa != null ? _faixa : _faixa = Desenhar("Faixa (corpo)", (x, y) =>
        {
            float tira = Retangulo(x, y, 32f, 32f, 26f, 13f, 4f);
            // as voltas: duas linhas inclinadas (escuras como o contorno)
            float volta = Mathf.Min(Mathf.Abs((x - 24f) - (y - 32f) * 0.45f), Mathf.Abs((x - 40f) - (y - 32f) * 0.45f));
            return Mathf.Max(tira, -2f - volta);
        });

        /// <summary>A tala: duas ripas em pé presas por duas tiras.</summary>
        public static Texture2D Tala => _tala != null ? _tala : _tala = Desenhar("Tala (corpo)", (x, y) =>
        {
            float ripas = Mathf.Min(Retangulo(x, y, 22f, 32f, 5f, 27f, 2f), Retangulo(x, y, 42f, 32f, 5f, 27f, 2f));
            float tiras = Mathf.Min(Retangulo(x, y, 32f, 20f, 17f, 3.5f, 1.5f), Retangulo(x, y, 32f, 44f, 17f, 3.5f, 1.5f));
            return Mathf.Min(ripas, tiras);
        });

        /// <summary>O osso partido no meio, em diagonal (a fratura).</summary>
        public static Texture2D Osso => _osso != null ? _osso : _osso = Desenhar("Osso partido (corpo)", (x, y) =>
        {
            var a = new Vector2(15f, 49f);
            var b = new Vector2(49f, 15f);
            var p = new Vector2(x, y);
            float haste = Segmento(p, a, b) - 5f;
            // as cabeças do osso: duas bolinhas em cada ponta, de lado
            var eixo = (b - a).normalized;
            var lado = new Vector2(-eixo.y, eixo.x);
            float cabecas = Mathf.Min(
                Mathf.Min(Vector2.Distance(p, a + lado * 5.5f) - 6.5f, Vector2.Distance(p, a - lado * 5.5f) - 6.5f),
                Mathf.Min(Vector2.Distance(p, b + lado * 5.5f) - 6.5f, Vector2.Distance(p, b - lado * 5.5f) - 6.5f));
            float osso = Mathf.Min(haste, cabecas);
            // a quebra: um vão em zigue-zague atravessando o meio (transparente)
            var meio = (a + b) * 0.5f;
            float ao = Vector2.Dot(p - meio, eixo);
            float de = Vector2.Dot(p - meio, lado);
            float zigue = Mathf.Abs(Mathf.Repeat(de + 2.5f, 5f) - 2.5f) * 1.2f - 1.5f;
            float vao = 6f - Mathf.Abs(ao - zigue);
            return Mathf.Max(osso, vao);
        });

        /// <summary>O anel (o tornozelo ou o pulso torcido: a entorse).</summary>
        public static Texture2D Entorse => _entorse != null ? _entorse : _entorse = Desenhar("Entorse (corpo)", (x, y) =>
            Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f)) - 19f) - 6f);

        /// <summary>A cruz do kit médico.</summary>
        public static Texture2D Cruz => _cruz != null ? _cruz : _cruz = Desenhar("Cruz (corpo)", (x, y) =>
            Mathf.Min(Retangulo(x, y, 32f, 32f, 8f, 24f, 2f), Retangulo(x, y, 32f, 32f, 24f, 8f, 2f)));

        // ---- a etapa 2 (05/11/2026): a limpeza e a infecção ----

        /// <summary>O termômetro (a febre): o tubo em pé com as marcas e o bulbo embaixo.</summary>
        public static Texture2D Termometro => _termometro != null ? _termometro : _termometro = Desenhar("Termômetro (corpo)", (x, y) =>
        {
            float tubo = Retangulo(x, y, 32f, 27f, 6.5f, 21f, 6.5f);
            float bulbo = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 49f)) - 10.5f;
            float forma = Mathf.Min(tubo, bulbo);
            // as marcas: três riscos escuros do lado direito do tubo
            var p = new Vector2(x, y);
            float marcas = Mathf.Min(Segmento(p, new Vector2(33f, 14f), new Vector2(37f, 14f)),
                           Mathf.Min(Segmento(p, new Vector2(33f, 22f), new Vector2(37f, 22f)), Segmento(p, new Vector2(33f, 30f), new Vector2(37f, 30f)))) - 0.6f;
            return Mathf.Max(forma, -2f - marcas);
        });

        /// <summary>O escudo (a ferida protegida da infecção até o amanhecer: o curativo antisséptico).</summary>
        public static Texture2D Escudo => _escudo != null ? _escudo : _escudo = Desenhar("Escudo (corpo)", (x, y) =>
        {
            float topo = Retangulo(x, y, 32f, 21f, 18f, 13f, 4f);
            // a ponta de baixo: estreitando até o bico, como a gota ao contrário
            float t = Mathf.InverseLerp(28f, 58f, y);
            float meia = 18f * Mathf.Sqrt(Mathf.Clamp01(1f - t));
            float ponta = y < 28f || y > 58f ? 99f : Mathf.Abs(x - 32f) - meia;
            float forma = Mathf.Min(topo, ponta);
            // a faixa do meio, escura (o brasão)
            float faixa = Mathf.Abs(x - 32f) - 1.2f;
            return y > 13f && y < 48f ? Mathf.Max(forma, -2f - faixa) : forma;
        });

        /// <summary>A gotinha com o brilho (a ferida limpa pelo spray: falta enfaixar). Tinja de azul.</summary>
        public static Texture2D Gotinha => _gotinha != null ? _gotinha : _gotinha = Desenhar("Gotinha (corpo)", (x, y) =>
        {
            // a gota um pouco menor, embaixo à esquerda
            float bola = Vector2.Distance(new Vector2(x, y), new Vector2(28f, 44f)) - 13f;
            float t = Mathf.InverseLerp(14f, 44f, y);
            float meia = 12.5f * Mathf.Sin(t * Mathf.PI * 0.5f);
            float ponta = y < 14f || y > 44f ? 99f : Mathf.Abs(x - 28f) - meia;
            float gota = Mathf.Min(bola, ponta);
            // o brilho: a estrelinha de 4 pontas em cima à direita
            float brilho = Mathf.Min(Retangulo(x, y, 49f, 14f, 1.8f, 8f, 1.8f), Retangulo(x, y, 49f, 14f, 8f, 1.8f, 1.8f));
            return Mathf.Min(gota, brilho);
        });

        /// <summary>O micróbio (a ferida infeccionada): a bola com as antenas e as pintas escuras. Tinja de verde-amarelado.</summary>
        public static Texture2D Infeccao => _infeccao != null ? _infeccao : _infeccao = Desenhar("Infecção (corpo)", (x, y) =>
        {
            var p = new Vector2(x, y);
            var c = new Vector2(32f, 32f);
            float forma = Vector2.Distance(p, c) - 13f;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f + 0.39f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float antena = Segmento(p, c + d * 12f, c + d * 21f) - 2f;
                float ponta = Vector2.Distance(p, c + d * 23f) - 4f;
                forma = Mathf.Min(forma, Mathf.Min(antena, ponta));
            }
            // as pintas escuras dentro da bola
            float pintas = Mathf.Min(Vector2.Distance(p, new Vector2(27f, 28f)) - 2.8f,
                           Mathf.Min(Vector2.Distance(p, new Vector2(37f, 33f)) - 2.4f, Vector2.Distance(p, new Vector2(29f, 38f)) - 2f));
            return Mathf.Max(forma, -2f - pintas);
        });

        /// <summary>
        /// As riscas do membro inutilizado: listras escuras em diagonal num fundo transparente, para repetir (o
        /// <c>wrapMode</c> é Repeat: desenhe com <c>GUI.DrawTextureWithTexCoords</c>) por cima do cinza.
        /// </summary>
        public static Texture2D Riscas => _riscas != null ? _riscas : _riscas = DesenharRiscas("Riscas (corpo)", false);

        /// <summary>As riscas num círculo (a cabeça inutilizada), para pôr por cima do círculo cinza.</summary>
        public static Texture2D RiscasRedondas => _riscasRedondas != null ? _riscasRedondas : _riscasRedondas = DesenharRiscas("Riscas redondas (corpo)", true);

        /// <summary>A vinheta: o meio transparente e as bordas pretas (a tela inteira, com a cabeça a 0).</summary>
        public static Texture2D Vinheta
        {
            get
            {
                if (_vinheta != null) return _vinheta;
                const int n = 128;
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // a distância do meio, de 0 (meio) a ~1,41 (o canto)
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01((r - 0.55f) / 0.8f), 1.6f);
                    px[y * n + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(a * 255f));
                }
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Vinheta (corpo)" };
                tex.SetPixels32(px);
                tex.Apply(false, true);
                return _vinheta = tex;
            }
        }

        // ------------------------------------------------------------------ as formas

        // a distância de um retângulo de cantos redondos (o meio, as meias medidas e o raio do canto)
        static float Retangulo(float x, float y, float cx, float cy, float meiaW, float meiaH, float raio)
        {
            float qx = Mathf.Abs(x - cx) - (meiaW - raio), qy = Mathf.Abs(y - cy) - (meiaH - raio);
            float fora = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return fora + Mathf.Min(Mathf.Max(qx, qy), 0f) - raio;
        }

        // a distância do ponto ao segmento de a até b
        static float Segmento(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        // uma forma de 64 px pela distância com sinal (negativa dentro; y de cima para baixo): o miolo branco, o contorno
        // escuro de 3 px (a cópia do MarcasDoMapa.Desenhar, que é privado)
        static Texture2D Desenhar(string nome, Func<float, float, float> distancia)
        {
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = distancia(x + 0.5f, y + 0.5f);
                float miolo = Mathf.Clamp01(-d - 2.5f);
                float alfa = Mathf.Clamp01(3.5f - d);
                byte c = (byte)Mathf.RoundToInt(Mathf.Lerp(20f, 255f, miolo));
                px[(n - 1 - y) * n + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(alfa * 255f));
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = nome };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        // as listras escuras em diagonal (de 8 em 8 px, que se repetem sem emenda em 32); redondas: só dentro do círculo
        static Texture2D DesenharRiscas(string nome, bool redondas)
        {
            int n = redondas ? 64 : 32;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float s = Mathf.Repeat(x + y + 1f, 8f);
                float listra = Mathf.Clamp01(1.6f - Mathf.Abs(s - 4f));
                if (redondas)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    listra *= Mathf.Clamp01(n / 2f - 3f - r);
                }
                px[y * n + x] = new Color32(10, 10, 10, (byte)Mathf.RoundToInt(listra * 0.75f * 255f));
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = redondas ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = nome,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
