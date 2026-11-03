using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>O que foi marcado no mapa: o ping (some sozinho), a marca (o alfinete colorido) e a anotação (o alfinete com um texto).</summary>
    public enum TipoDeMarca : byte { Ping, Marca, Anotacao }

    /// <summary>
    /// Uma marca no mapa (03/11/2026, a pedido: "pings, marcações e anotações no mapa"), na lista da rede do
    /// <see cref="MundoDaPartida"/>: quem marcou, onde (no mundo), a cor, o texto (a anotação) e, no ping, quando some (o
    /// tempo do anfitrião).
    /// </summary>
    public struct MarcaNoMapa : INetworkSerializeByMemcpy, IEquatable<MarcaNoMapa>
    {
        public ushort id;
        public TipoDeMarca tipo;
        public byte cor;
        public ulong dono;
        public Vector3 pos;
        public double ate;
        public FixedString32Bytes autor;
        public FixedString128Bytes texto;

        public bool Equals(MarcaNoMapa o) => id == o.id && tipo == o.tipo && cor == o.cor && dono == o.dono && pos == o.pos && ate == o.ate
                                             && autor.Equals(o.autor) && texto.Equals(o.texto);
    }

    /// <summary>
    /// O que as marcas do mapa têm em comum (03/11/2026): as cores, os números (o ping dura 20 s, até 3 por jogador; a
    /// anotação, até 40 letras), os desenhos (o losango do ping e o alfinete) e o bipe do ping.
    /// </summary>
    public static class MarcasDoMapa
    {
        public const float DuracaoDoPing = 20f;
        public const int PingsPorJogador = 3;
        public const int LetrasDaAnotacao = 40;
        public const int MarcasNoMundo = 200;
        public const float AlcanceDoPing = 300f;

        public static readonly Color[] Cores =
        {
            new Color(0.92f, 0.22f, 0.18f),   // vermelho
            new Color(0.98f, 0.78f, 0.15f),   // amarelo
            new Color(0.3f, 0.82f, 0.32f),    // verde
            new Color(0.25f, 0.6f, 1f),       // azul
            new Color(0.82f, 0.4f, 0.95f),    // roxo
        };
        public static readonly string[] NomesDasCores = { "Vermelho", "Amarelo", "Verde", "Azul", "Roxo" };

        /// <summary>A cor do ping: o seu em ciano (como a sua seta), o dos outros em laranja.</summary>
        public static Color CorDoPing(ulong dono) =>
            Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.LocalClientId == dono
                ? new Color(0.25f, 0.9f, 1f) : new Color(1f, 0.6f, 0.15f);

        public static Color CorDa(MarcaNoMapa m) => m.tipo == TipoDeMarca.Ping ? CorDoPing(m.dono) : Cores[Mathf.Min(m.cor, (byte)(Cores.Length - 1))];

        /// <summary>A lista da partida (null fora dela).</summary>
        public static NetworkList<MarcaNoMapa> Lista
        {
            get
            {
                var m = MundoDaPartida.Instancia;
                return m != null && m.IsSpawned ? m.Marcas : null;
            }
        }

        /// <summary>O texto cortado no tamanho da anotação (e sem as quebras de linha).</summary>
        public static string Cortar(string texto, int letras = LetrasDaAnotacao)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            texto = texto.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return texto.Length > letras ? texto.Substring(0, letras) : texto;
        }

        static Texture2D _losango, _alfinete;
        static AudioClip _bipe;

        /// <summary>O losango do ping (branco, para tingir), com a borda escura.</summary>
        public static Texture2D Losango => _losango != null ? _losango : _losango = Desenhar("Losango do ping", (x, y) => Mathf.Abs(x - 32f) + Mathf.Abs(y - 32f) - 26f);

        /// <summary>O alfinete da marca: a cabeça redonda em cima e a ponta embaixo (o ponto marcado é a ponta).</summary>
        public static Texture2D Alfinete => _alfinete != null ? _alfinete : _alfinete = Desenhar("Alfinete do mapa", (x, y) =>
        {
            float cabeca = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 22f)) - 17f;
            // a ponta: um triângulo do meio da cabeça até embaixo
            float t = Mathf.InverseLerp(22f, 60f, y);
            float meia = Mathf.Lerp(11f, 0.5f, t);
            float ponta = y < 22f || y > 60f ? 99f : Mathf.Abs(x - 32f) - meia;
            return Mathf.Min(cabeca, ponta);
        });

        // uma forma de 64 px pela distância com sinal (negativa dentro): o miolo branco, o contorno escuro de 3 px
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

        /// <summary>O bipe do ping: duas notas curtas e limpas (gerado uma vez).</summary>
        public static AudioClip Bipe
        {
            get
            {
                if (_bipe != null) return _bipe;
                const int taxa = 44100;
                int n = (int)(taxa * 0.32f);
                var dados = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)taxa;
                    float f = t < 0.12f ? 1320f : 1760f;
                    float local = t < 0.12f ? t : t - 0.14f;
                    float env = local < 0f ? 0f : Mathf.Min(1f, local * 400f) * Mathf.Exp(-local * 18f);
                    dados[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.35f;
                }
                return _bipe = SomGerado.Clip("Bipe do ping", dados, taxa);
            }
        }
    }
}
