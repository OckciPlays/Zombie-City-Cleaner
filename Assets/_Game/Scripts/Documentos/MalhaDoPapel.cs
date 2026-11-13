using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using Forma = ZombieCityCleaner.Mundo.DocumentosDoHospital.Forma;

namespace ZombieCityCleaner.Documentos
{
    /// <summary>
    /// O atlas dos papéis (<c>Resources/Documentos/T_Papeis.png</c>, desenhado pelo Editor, o <c>MontagemDosPapeis</c>):
    /// os retângulos de cada desenho, em pixels, com a origem em cima à esquerda (a imagem tem 1024 × 1024). Os de folha
    /// (256 × 330) têm a proporção da página.
    /// </summary>
    public static class AtlasDosPapeis
    {
        public const int Lado = 1024;

        public static readonly RectInt
            Folha = new RectInt(0, 0, 256, 330),        // datilografada, com o timbre
            Aviso = new RectInt(256, 0, 256, 330),      // o aviso impresso, com a faixa vermelha
            Ficha = new RectInt(512, 0, 256, 330),      // o formulário preenchido à mão
            Carta = new RectInt(768, 0, 256, 330),      // a carta à mão no papel timbrado
            Recado = new RectInt(0, 340, 256, 330),     // o recado de pincel atômico
            Liso = new RectInt(256, 340, 256, 330),     // o papel velho sem nada (o fundo da folha na tela)
            CadernoE = new RectInt(512, 340, 256, 330), // a página da esquerda do caderno aberto
            CadernoD = new RectInt(768, 340, 256, 330), // a da direita
            Pasta = new RectInt(0, 680, 256, 330),      // a capa da pasta parda, com a orelha e o carimbo
            Prancheta = new RectInt(256, 680, 256, 330),// a prancheta de eucatex
            Bilhete = new RectInt(512, 680, 256, 190),  // o bilhete pautado, à mão
            Capa = new RectInt(768, 680, 256, 190),     // o pano da capa do caderno
            Clipe = new RectInt(512, 880, 64, 64),      // o metal do prendedor
            Fita = new RectInt(580, 880, 64, 40),       // a fita crepe
            Borda = new RectInt(648, 880, 64, 64),      // a beira do papel
            Manila = new RectInt(716, 880, 64, 64),     // o papelão pardo da pasta
            Madeira = new RectInt(784, 880, 64, 64);    // a beira da prancheta

        /// <summary>O retângulo no UV (a origem embaixo), um pixel e meio para dentro (sem pegar o vizinho no filtro).</summary>
        public static Rect UV(RectInt r)
        {
            const float m = 1.5f;
            return Rect.MinMaxRect((r.x + m) / Lado, 1f - (r.y + r.height - m) / Lado, (r.x + r.width - m) / Lado, 1f - (r.y + m) / Lado);
        }
    }

    /// <summary>
    /// As malhas dos papéis, feitas aqui (uma por forma e desenho, guardadas): no espaço do papel, a largura em x, a altura da
    /// página em z (o alto em +z) e a frente em +y, com a origem no meio da grossura (o <c>DocumentosDoHospital.Tamanho</c>).
    /// A folha, a carta e o aviso (com duas tiras de fita crepe), a prancheta (o eucatex, a folha e o prendedor), a pasta
    /// parda entreaberta com a folha aparecendo, o bilhete dobrado ao meio e o caderno aberto (a capa, as duas páginas e a
    /// lombada).
    /// </summary>
    public static class MalhaDoPapel
    {
        static readonly Dictionary<(Forma, Letra), Mesh> _prontas = new Dictionary<(Forma, Letra), Mesh>();

        public static Mesh De(Forma forma, Letra letra)
        {
            if (_prontas.TryGetValue((forma, letra), out var m) && m != null) return m;
            var c = new Construtor();
            // o tamanho: a largura (x), a altura da página (y, que aqui vai em z) e a grossura (z, que aqui vai em y)
            var t = DocumentosDoHospital.Tamanho(forma);
            float w = t.x, h = t.y, g = t.z;
            switch (forma)
            {
                case Forma.Prancheta:
                    c.Caixa(new Vector3(0f, -0.003f, 0f), new Vector3(w, 0.006f, h), AtlasDosPapeis.Prancheta, AtlasDosPapeis.Madeira, AtlasDosPapeis.Madeira);
                    c.Topo(new Vector3(0f, 0.0006f, -0.02f), 0.23f, 0.30f, letra == Letra.Maquina ? AtlasDosPapeis.Folha : AtlasDosPapeis.Ficha);
                    c.Caixa(new Vector3(0f, 0.0035f, h / 2f - 0.024f), new Vector3(0.11f, 0.007f, 0.036f), AtlasDosPapeis.Clipe, AtlasDosPapeis.Clipe, AtlasDosPapeis.Clipe);
                    c.Caixa(new Vector3(0f, 0.002f, h / 2f - 0.004f), new Vector3(0.034f, 0.004f, 0.012f), AtlasDosPapeis.Clipe, AtlasDosPapeis.Clipe, AtlasDosPapeis.Clipe);
                    break;
                case Forma.Pasta:
                    // a capa de trás, com a orelha, a folha aparecendo e a capa da frente um pouco aberta (a dobra na esquerda)
                    c.Caixa(new Vector3(0f, -0.0055f, 0f), new Vector3(w, 0.003f, h), AtlasDosPapeis.Manila, AtlasDosPapeis.Manila, AtlasDosPapeis.Manila);
                    c.Caixa(new Vector3(0.07f, -0.0055f, h / 2f + 0.011f), new Vector3(0.09f, 0.003f, 0.022f), AtlasDosPapeis.Manila, AtlasDosPapeis.Manila, AtlasDosPapeis.Manila);
                    c.Matriz = Matrix4x4.TRS(new Vector3(0.022f, -0.0036f, -0.008f), Quaternion.Euler(0f, 5f, 0f), Vector3.one);
                    c.Topo(Vector3.zero, 0.25f, 0.32f, AtlasDosPapeis.Folha);
                    c.Matriz = Matrix4x4.TRS(new Vector3(-w / 2f, 0.001f, 0f), Quaternion.Euler(0f, -2f, 4.5f), Vector3.one);
                    c.Caixa(new Vector3(w / 2f - 0.004f, 0.0015f, 0f), new Vector3(w - 0.006f, 0.003f, h - 0.004f), AtlasDosPapeis.Pasta, AtlasDosPapeis.Manila, AtlasDosPapeis.Manila);
                    c.Matriz = Matrix4x4.identity;
                    break;
                case Forma.Bilhete:
                {
                    // dobrado ao meio: a metade da esquerda deitada e a da direita levantada 14° na dobra
                    var uv = AtlasDosPapeis.Bilhete;
                    var esquerda = new RectInt(uv.x, uv.y, uv.width / 2, uv.height);
                    var direita = new RectInt(uv.x + uv.width / 2, uv.y, uv.width / 2, uv.height);
                    c.Topo(new Vector3(-w / 4f, -0.0006f, 0f), w / 2f, h, esquerda);
                    c.Matriz = Matrix4x4.TRS(new Vector3(0f, -0.0006f, 0f), Quaternion.Euler(0f, 0f, 14f), Vector3.one);
                    c.Topo(new Vector3(w / 4f, 0f, 0f), w / 2f, h, direita);
                    c.Embaixo(new Vector3(w / 4f, -0.0002f, 0f), w / 2f, h, AtlasDosPapeis.Borda);
                    c.Matriz = Matrix4x4.identity;
                    break;
                }
                case Forma.Caderno:
                {
                    // a capa aberta, as duas páginas subindo para a lombada e a lombada
                    c.Caixa(new Vector3(0f, -0.009f, 0f), new Vector3(w, 0.006f, h), AtlasDosPapeis.Capa, AtlasDosPapeis.Capa, AtlasDosPapeis.Capa);
                    float pw = w / 2f - 0.012f, ph = h - 0.014f;
                    c.Matriz = Matrix4x4.TRS(new Vector3(-pw / 2f - 0.006f, -0.0005f, 0f), Quaternion.Euler(0f, 0f, 3f), Vector3.one);
                    c.Caixa(Vector3.zero, new Vector3(pw, 0.011f, ph), AtlasDosPapeis.CadernoE, AtlasDosPapeis.Borda, AtlasDosPapeis.Borda);
                    c.Matriz = Matrix4x4.TRS(new Vector3(pw / 2f + 0.006f, -0.0005f, 0f), Quaternion.Euler(0f, 0f, -3f), Vector3.one);
                    c.Caixa(Vector3.zero, new Vector3(pw, 0.011f, ph), AtlasDosPapeis.CadernoD, AtlasDosPapeis.Borda, AtlasDosPapeis.Borda);
                    c.Matriz = Matrix4x4.identity;
                    c.Caixa(new Vector3(0f, -0.004f, 0f), new Vector3(0.014f, 0.012f, h - 0.004f), AtlasDosPapeis.Capa, AtlasDosPapeis.Capa, AtlasDosPapeis.Capa);
                    break;
                }
                default:
                {
                    // a folha solta, a carta ou o aviso (com a fita crepe nos dois cantos de cima)
                    var desenho = forma == Forma.Aviso ? (letra == Letra.Mao ? AtlasDosPapeis.Recado : AtlasDosPapeis.Aviso)
                                : forma == Forma.Carta || letra == Letra.Mao ? AtlasDosPapeis.Carta
                                : letra == Letra.Formulario ? AtlasDosPapeis.Ficha : AtlasDosPapeis.Folha;
                    c.Topo(new Vector3(0f, g / 2f - 0.0002f, 0f), w, h, desenho);
                    if (forma == Forma.Aviso)
                    {
                        for (int lado = -1; lado <= 1; lado += 2)
                        {
                            c.Matriz = Matrix4x4.TRS(new Vector3(lado * (w / 2f - 0.014f), g / 2f + 0.0006f, h / 2f - 0.01f), Quaternion.Euler(0f, lado * 38f, 0f), Vector3.one);
                            c.Topo(Vector3.zero, 0.055f, 0.022f, AtlasDosPapeis.Fita);
                        }
                        c.Matriz = Matrix4x4.identity;
                    }
                    break;
                }
            }
            m = c.Malha($"Papel_{forma}_{letra}");
            _prontas[(forma, letra)] = m;
            return m;
        }

        // ------------------------------------------------------------ o construtor das malhas

        sealed class Construtor
        {
            readonly List<Vector3> _v = new List<Vector3>(), _n = new List<Vector3>();
            readonly List<Vector2> _uv = new List<Vector2>();
            readonly List<int> _t = new List<int>();
            public Matrix4x4 Matriz = Matrix4x4.identity;

            // o quadrilátero o, o+dv, o+du+dv, o+du (a frente para dv × du); du segue o u do desenho e dv o v
            void Face(Vector3 o, Vector3 du, Vector3 dv, RectInt desenho)
            {
                var r = AtlasDosPapeis.UV(desenho);
                Vector3 n = Matriz.MultiplyVector(Vector3.Cross(dv, du)).normalized;
                int b = _v.Count;
                _v.Add(Matriz.MultiplyPoint3x4(o));
                _v.Add(Matriz.MultiplyPoint3x4(o + dv));
                _v.Add(Matriz.MultiplyPoint3x4(o + du + dv));
                _v.Add(Matriz.MultiplyPoint3x4(o + du));
                for (int i = 0; i < 4; i++) _n.Add(n);
                _uv.Add(new Vector2(r.xMin, r.yMin));
                _uv.Add(new Vector2(r.xMin, r.yMax));
                _uv.Add(new Vector2(r.xMax, r.yMax));
                _uv.Add(new Vector2(r.xMax, r.yMin));
                _t.Add(b); _t.Add(b + 1); _t.Add(b + 2);
                _t.Add(b); _t.Add(b + 2); _t.Add(b + 3);
            }

            /// <summary>Uma face deitada, para cima, com o meio em c.</summary>
            public void Topo(Vector3 c, float w, float h, RectInt desenho) =>
                Face(c + new Vector3(-w / 2f, 0f, -h / 2f), new Vector3(w, 0f, 0f), new Vector3(0f, 0f, h), desenho);

            /// <summary>Uma face deitada, para baixo.</summary>
            public void Embaixo(Vector3 c, float w, float h, RectInt desenho) =>
                Face(c + new Vector3(w / 2f, 0f, -h / 2f), new Vector3(-w, 0f, 0f), new Vector3(0f, 0f, h), desenho);

            /// <summary>Uma caixa: o desenho de cima, o dos lados e o de baixo.</summary>
            public void Caixa(Vector3 c, Vector3 s, RectInt cima, RectInt lados, RectInt baixo)
            {
                Vector3 e = s / 2f;
                Face(c + new Vector3(-e.x, e.y, -e.z), new Vector3(s.x, 0f, 0f), new Vector3(0f, 0f, s.z), cima);
                Face(c + new Vector3(e.x, -e.y, -e.z), new Vector3(-s.x, 0f, 0f), new Vector3(0f, 0f, s.z), baixo);
                Face(c + new Vector3(-e.x, -e.y, -e.z), new Vector3(s.x, 0f, 0f), new Vector3(0f, s.y, 0f), lados);
                Face(c + new Vector3(e.x, -e.y, e.z), new Vector3(-s.x, 0f, 0f), new Vector3(0f, s.y, 0f), lados);
                Face(c + new Vector3(-e.x, -e.y, e.z), new Vector3(0f, 0f, -s.z), new Vector3(0f, s.y, 0f), lados);
                Face(c + new Vector3(e.x, -e.y, -e.z), new Vector3(0f, 0f, s.z), new Vector3(0f, s.y, 0f), lados);
            }

            public Mesh Malha(string nome)
            {
                var m = new Mesh { name = nome };
                m.SetVertices(_v);
                m.SetNormals(_n);
                m.SetUVs(0, _uv);
                m.SetTriangles(_t, 0);
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
