using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// As letras das placas (Plano_Mapa, Parte M4): uma malha de quadradinhos, um por letra, com o UV no atlas de uma fonte
    /// dinâmica (a Arial do sistema, parecida com a das placas brasileiras; sem ela, a fonte embutida da Unity), desenhada
    /// pelo shader do letreiro (o alfa do atlas recorta a letra). Os textos vêm de <see cref="ModelosDaEstrada.Texto"/>: no
    /// plano da frente da placa (+z), com u para a direita de quem lê (o -x da placa) e v para cima.
    /// Se a Unity refizer o atlas (outra letra ou outro tamanho pedidos), as UVs antigas ficam erradas: <see cref="Versao"/>
    /// muda, e quem tem letreiros refaz as malhas. Só na thread principal.
    /// </summary>
    public static class Letreiro
    {
        /// <summary>O tamanho pedido à fonte (px por em): as letras ficam nítidas até ~1 m na tela.</summary>
        public const int Tamanho = 64;
        /// <summary>A altura das maiúsculas da Arial, em frações do em.</summary>
        const float AlturaDaMaiuscula = 0.716f;
        /// <summary>
        /// A cedilha maior (10/11/2026, a pedido: no "CAÇA E FERRAGENS" da loja do Dia 0 ela quase não aparecia de longe): o Ç
        /// sai como o C, com a cedilha da fonte em negrito (o ¸) por baixo, esta vez maior. O ç minúsculo fica o da fonte
        /// (ele desce como o g e o p, e as placas das estradas já contam com isso). A da Arial tem um
        /// terço da altura da letra e o traço mais fino que o do C, e o recorte do alfa (o shader corta em 0,5) a apagava
        /// quando a placa ficava pequena na tela. Desce até <see cref="FundoDaCedilha"/> da altura das maiúsculas.
        /// </summary>
        public const float CedilhaMaior = 1.45f;
        const char Cedilha = '\u00B8';
        /// <summary>Até onde a cedilha maior desce abaixo da linha de base, em frações da altura das maiúsculas (para caber na placa).</summary>
        public const float FundoDaCedilha = 0.5f;

        /// <summary>O texto tem o Ç, com a cedilha maior (que desce até <see cref="FundoDaCedilha"/> abaixo da linha de base).</summary>
        public static bool TemCedilha(string texto) => texto != null && texto.IndexOf('Ç') >= 0;

        // o Ç sai como o C (a cedilha vem à parte, maior)
        static char SemCedilha(char c) => c == 'Ç' ? 'C' : c;

        static Font _fonte;

        /// <summary>Muda quando o atlas da fonte é refeito (as malhas antigas precisam ser refeitas).</summary>
        public static int Versao { get; private set; }

        public static Font Fonte
        {
            get
            {
                if (_fonte != null) return _fonte;
                // uma fonte só nossa: a do OnGUI (a embutida) é refeita toda hora, e as UVs das placas iriam junto
                _fonte = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Liberation Sans", "DejaVu Sans" }, Tamanho);
                if (_fonte == null) _fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Font.textureRebuilt += f =>
                {
                    if (f == _fonte) Versao++;
                };
                return _fonte;
            }
        }

        public static Texture Atlas => Fonte.material.mainTexture;

        static readonly List<Vector3> _v = new List<Vector3>();
        static readonly List<Vector2> _uv = new List<Vector2>();
        static readonly List<int> _t = new List<int>();
        static readonly List<Vector3> _n = new List<Vector3>();

        /// <summary>A malha dos textos de uma cor (<see cref="ModelosDaEstrada.LetraClara"/>...); null se não houver nenhum. Reaproveita "malha" se vier.</summary>
        public static Mesh Malha(List<ModelosDaEstrada.Texto> textos, int cor, Mesh malha = null)
        {
            var f = Fonte;
            bool algum = false;
            foreach (var t in textos)
                if (t.Cor == cor && !string.IsNullOrEmpty(t.Conteudo))
                {
                    f.RequestCharactersInTexture(t.Conteudo, Tamanho);
                    if (TemCedilha(t.Conteudo))
                    {
                        f.RequestCharactersInTexture("C", Tamanho);
                        f.RequestCharactersInTexture(Cedilha.ToString(), Tamanho, FontStyle.Bold);
                    }
                    algum = true;
                }
            if (!algum) return null;
            _v.Clear();
            _uv.Clear();
            _t.Clear();
            _n.Clear();
            foreach (var t in textos)
            {
                if (t.Cor != cor || string.IsNullOrEmpty(t.Conteudo)) continue;
                var giro = Quaternion.Euler(0f, t.Giro, 0f);
                var normal = giro * Vector3.forward;
                int primeiro = _v.Count;
                float escala = t.Altura / (Tamanho * AlturaDaMaiuscula);
                float largura = 0f;
                foreach (char c in t.Conteudo)
                    if (f.GetCharacterInfo(SemCedilha(c), out var ci, Tamanho)) largura += ci.advance;
                largura *= escala;
                float u0 = t.Alinhamento < 0 ? t.U : t.Alinhamento == 0 ? t.U - largura * 0.5f : t.U - largura;
                float linhaDeBase = t.V - t.Altura * 0.5f;
                float cursor = 0f;
                foreach (char c in t.Conteudo)
                {
                    char letra = SemCedilha(c);
                    if (!f.GetCharacterInfo(letra, out var ci, Tamanho)) continue;
                    float ua = u0 + (cursor + ci.minX) * escala, ub = u0 + (cursor + ci.maxX) * escala;
                    float va = linhaDeBase + ci.minY * escala, vb = linhaDeBase + ci.maxY * escala;
                    if (letra != c && f.GetCharacterInfo(Cedilha, out var ce, Tamanho, FontStyle.Bold))
                    {
                        // a cedilha: no meio da letra, com o alto dela (que entra um pouco no traço de baixo do C) na linha
                        // de base, CedilhaMaior vezes maior e sem passar de FundoDaCedilha abaixo dela
                        float k = Mathf.Min(CedilhaMaior, (FundoDaCedilha * Tamanho * AlturaDaMaiuscula + ce.maxY) / Mathf.Max(1, ce.maxY - ce.minY)) * escala;
                        float meio = u0 + (cursor + (ci.minX + ci.maxX) * 0.5f) * escala, alto = linhaDeBase + ce.maxY * escala;
                        float ca = meio - (ce.maxX - ce.minX) * 0.5f * k, cb = meio + (ce.maxX - ce.minX) * 0.5f * k;
                        Quadro(ca, cb, alto - (ce.maxY - ce.minY) * k, alto, ce, t.Z);
                    }
                    cursor += ci.advance;
                    if (ub - ua < 1e-4f || vb - va < 1e-4f) continue;
                    Quadro(ua, ub, va, vb, ci, t.Z);
                }
                // o giro do texto (o verso da placa, os lados da placa de rua)
                for (int i = primeiro; i < _v.Count; i++)
                {
                    if (t.Giro != 0f) _v[i] = giro * _v[i];
                    _n.Add(normal);
                }
            }
            if (_v.Count == 0) return null;
            if (malha == null) malha = new Mesh { name = "Letreiro" };
            malha.Clear();
            malha.SetVertices(_v);
            malha.SetUVs(0, _uv);
            malha.SetNormals(_n);
            malha.SetTriangles(_t, 0);
            malha.RecalculateBounds();
            return malha;
        }

        // o quadradinho de uma letra (de ua a ub para a direita de quem lê, de va a vb para cima), com o UV dela no atlas
        static void Quadro(float ua, float ub, float va, float vb, CharacterInfo ci, float z)
        {
            // u cresce para a direita de quem lê, que olha para -z: no espaço da placa, -x
            int b = _v.Count;
            _v.Add(new Vector3(-ua, va, z)); _uv.Add(ci.uvBottomLeft);
            _v.Add(new Vector3(-ua, vb, z)); _uv.Add(ci.uvTopLeft);
            _v.Add(new Vector3(-ub, vb, z)); _uv.Add(ci.uvTopRight);
            _v.Add(new Vector3(-ub, va, z)); _uv.Add(ci.uvBottomRight);
            // a frente (+z): (BL, TL, BR) e (BR, TL, TR)
            _t.Add(b); _t.Add(b + 1); _t.Add(b + 3);
            _t.Add(b + 3); _t.Add(b + 1); _t.Add(b + 2);
        }
    }
}
