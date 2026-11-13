using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Uma malha low-poly de faces planas montada por código (o Plano_Floresta, 09/11/2026): cada triângulo com os seus três
    /// vértices (o sombreado plano), o UV apontando para a célula da cor na <see cref="Paleta"/> (como as malhas do
    /// cidade_kit.py, para o material da vegetação servir igual) e a cor do vértice com o vento do Vegetacao.shader
    /// (R = o balanço pela altura, 0 no chão e 1 a 16 m; G = a fase da peça; B = o tremor, 1 nas folhas).
    /// </summary>
    public sealed class ConstrutorDeMalha
    {
        const float AlturaDoVento = 16f;

        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<Color32> _c = new List<Color32>();

        public int Triangulos => _v.Count / 3;

        /// <summary>Um triângulo (a frente é a ordem horária olhando de fora, como na Unity), na cor da paleta.</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 cor, float fase, float folha, float balanco = 1f)
        {
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f) return;
            normal.Normalize();
            Vertice(a, normal, cor, fase, folha, balanco);
            Vertice(b, normal, cor, fase, folha, balanco);
            Vertice(c, normal, cor, fase, folha, balanco);
        }

        /// <summary>Um quadrilátero (a, b, c, d em volta), em dois triângulos.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 cor, float fase, float folha, float balanco = 1f)
        {
            Tri(a, b, c, cor, fase, folha, balanco);
            Tri(a, c, d, cor, fase, folha, balanco);
        }

        void Vertice(Vector3 p, Vector3 n, Vector2 cor, float fase, float folha, float balanco)
        {
            _v.Add(p);
            _n.Add(n);
            _uv.Add(cor);
            float r = Mathf.Pow(Mathf.Clamp01(p.y / AlturaDoVento), 1.5f) * balanco;
            _c.Add(new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(r) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(fase) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(folha) * 255f), 255));
        }

        /// <summary>O Mesh (só na thread principal).</summary>
        public Mesh Malha(string nome)
        {
            var m = new Mesh { name = nome };
            m.indexFormat = _v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetColors(_c);
            var tri = new int[_v.Count];
            for (int i = 0; i < tri.Length; i++) tri[i] = i;
            m.SetTriangles(tri, 0, true);
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>
    /// Os modelos de floresta feitos por código (o Plano_Floresta, 09/11/2026, pelo prompt e pela folha de referência do
    /// usuário): low-poly estilizado, com as silhuetas da referência e as variações pela semente. A fase 1 são os
    /// pinheiros, em três tamanhos: o tronco em segmentos, mais grosso na base e um pouco torto; a copa em andares de
    /// galhos (cada andar uma saia de pontas caídas, em estrela, com o fundo escuro e o topo mais claro), mais largos
    /// embaixo e menores em cima, com uns vãos; e a ponta. Os LODs: o 1 com metade dos andares e menos pontas, o 2 com
    /// uns cones simples. As cores vêm da paleta (o pinheiro ou o abeto, pelo tom da variação).
    /// </summary>
    public static class ArvoresProcedurais
    {
        public enum TamanhoDoPinheiro { Alto, Medio, Pequeno }

        /// <summary>Os parâmetros de um pinheiro (sorteados pela semente; a ferramenta deixa mexer).</summary>
        [Serializable]
        public struct Pinheiro
        {
            public TamanhoDoPinheiro tamanho;
            public int semente;
            [Tooltip("A altura total (m).")] public float altura;
            [Tooltip("A largura da copa (vezes o normal do tamanho).")] public float largura;
            [Tooltip("Quantos andares de galhos.")] public int andares;
            [Tooltip("Quanto as pontas dos galhos caem (0 reto, 1 muito).")] public float caida;
            [Tooltip("A inclinação e a torção do tronco (0 reto).")] public float torto;
            [Tooltip("O raio do tronco na base (m).")] public float raioDoTronco;
            [Tooltip("Onde a copa começa (fração da altura).")] public float comecoDaCopa;
            [Tooltip("O tom: abaixo de 0,5 o pinheiro verde, acima o abeto azulado.")] public float tom;
            [Tooltip("Quantas pontas por andar (de perto).")] public int pontas;
        }

        public static readonly string[] NomesDosTamanhos = { "PinheiroAlto", "PinheiroMedio", "PinheiroPequeno" };
        public const int VariacoesPorTamanho = 5;

        /// <summary>O nome da malha de uma variação (1 a 5) de um tamanho, como vai em <see cref="Floresta.Nomes"/>.</summary>
        public static string Nome(TamanhoDoPinheiro t, int variacao) => $"{NomesDosTamanhos[(int)t]}_{variacao}";

        /// <summary>A semente de cada variação (a mesma sempre: o mundo e a cena de teste batem).</summary>
        public static int Semente(TamanhoDoPinheiro t, int variacao) => 1000 + (int)t * 100 + variacao;

        /// <summary>Os parâmetros sorteados pela semente, dentro do que cada tamanho permite.</summary>
        public static Pinheiro Sortear(TamanhoDoPinheiro t, int semente)
        {
            var r = new System.Random(semente);
            float Entre(float a, float b) => a + (float)r.NextDouble() * (b - a);
            var p = new Pinheiro { tamanho = t, semente = semente };
            switch (t)
            {
                case TamanhoDoPinheiro.Alto:
                    p.altura = Entre(13f, 18f);
                    p.andares = r.Next(12, 17);
                    p.raioDoTronco = Entre(0.32f, 0.44f);
                    p.comecoDaCopa = Entre(0.2f, 0.3f);
                    p.pontas = r.Next(7, 10);
                    break;
                case TamanhoDoPinheiro.Medio:
                    p.altura = Entre(8f, 12f);
                    p.andares = r.Next(9, 13);
                    p.raioDoTronco = Entre(0.22f, 0.3f);
                    p.comecoDaCopa = Entre(0.16f, 0.26f);
                    p.pontas = r.Next(6, 9);
                    break;
                default:
                    p.altura = Entre(3f, 6f);
                    p.andares = r.Next(6, 9);
                    p.raioDoTronco = Entre(0.11f, 0.17f);
                    p.comecoDaCopa = Entre(0.1f, 0.18f);
                    p.pontas = r.Next(6, 8);
                    break;
            }
            p.largura = Entre(0.88f, 1.22f);
            p.caida = Entre(0.4f, 0.7f);
            p.torto = Entre(0.1f, 0.9f);
            p.tom = (float)r.NextDouble();
            return p;
        }

        /// <summary>A malha de um pinheiro no LOD pedido (0 de perto, 1 e 2 de longe).</summary>
        public static Mesh MalhaDoPinheiro(Pinheiro p, int lod, string nome)
        {
            var c = new ConstrutorDeMalha();
            MontarPinheiro(c, p, lod);
            return c.Malha(nome);
        }

        /// <summary>Quantos triângulos tem um pinheiro no LOD (sem criar o Mesh).</summary>
        public static int TriangulosDoPinheiro(Pinheiro p, int lod)
        {
            var c = new ConstrutorDeMalha();
            MontarPinheiro(c, p, lod);
            return c.Triangulos;
        }

        // as cores da copa: o pinheiro (verde) ou o abeto (azulado), três tons cada (o fundo, o meio e as pontas)
        static void Cores(float tom, out Vector2 escura, out Vector2 meia, out Vector2 clara)
        {
            if (tom < 0.5f)
            {
                escura = Paleta.UV("PinheiroEscuro");
                meia = Paleta.UV("Pinheiro");
                clara = Paleta.UV("PinheiroClaro");
            }
            else
            {
                escura = Paleta.UV("PinheiroEscuro");
                meia = Paleta.UV("Abeto");
                clara = Paleta.UV("AbetoClaro");
            }
        }

        static void MontarPinheiro(ConstrutorDeMalha c, Pinheiro p, int lod)
        {
            var r = new System.Random(p.semente * 7 + 3);
            float Entre(float a, float b) => a + (float)r.NextDouble() * (b - a);
            Cores(p.tom, out var escura, out var meia, out var clara);
            var casca = p.tom < 0.5f ? Paleta.UV("Tronco") : Paleta.UV("TroncoEscuro");
            var cascaClara = Paleta.UV("Tronco");

            // o eixo do tronco: uma leve inclinação e uma torção (o seno pela altura), mais forte nos tortos
            float inclinaX = Entre(-1f, 1f) * 0.06f * p.torto, inclinaZ = Entre(-1f, 1f) * 0.06f * p.torto;
            float torceX = Entre(-1f, 1f) * 0.25f * p.torto, torceZ = Entre(-1f, 1f) * 0.25f * p.torto;
            float faseTorce = Entre(0f, 6.28f);
            Vector3 Eixo(float y)
            {
                float t = y / p.altura;
                float s = Mathf.Sin(t * 3.1416f + faseTorce) * t;
                return new Vector3(inclinaX * y + torceX * s, y, inclinaZ * y + torceZ * s);
            }

            // ------------------------------------------------ o tronco
            int segmentos = lod == 0 ? 6 : lod == 1 ? 4 : 3;
            int lados = lod == 0 ? 7 : lod == 1 ? 5 : 4;
            float topoDoTronco = p.altura * 0.985f;
            float giroDoTronco = Entre(0f, 6.28f);
            Vector3 Anel(int i, int j, out float raio)
            {
                float t = i / (float)segmentos;
                float y = topoDoTronco * t;
                // a base alarga (as raízes), o resto afina até a ponta
                raio = p.raioDoTronco * Mathf.Lerp(1f, 0.1f, Mathf.Pow(t, 0.8f)) * (t < 0.08f ? 1f + (0.08f - t) / 0.08f * 0.35f : 1f);
                // uma irregularidade em volta (o tronco não é um tubo)
                float ang = giroDoTronco + 6.2831853f * j / lados;
                float irregular = 1f + 0.08f * p.torto * Mathf.Sin(ang * 3f + t * 9f);
                var e = Eixo(y);
                return e + new Vector3(Mathf.Cos(ang) * raio * irregular, 0f, Mathf.Sin(ang) * raio * irregular);
            }
            for (int i = 0; i < segmentos; i++)
            for (int j = 0; j < lados; j++)
            {
                var a = Anel(i, j, out _);
                var b = Anel(i, (j + 1) % lados, out _);
                var d = Anel(i + 1, j, out _);
                var e = Anel(i + 1, (j + 1) % lados, out _);
                // umas faces mais claras (a casca gasta)
                var cor = lod == 0 && r.NextDouble() < 0.18 ? cascaClara : casca;
                c.Quad(a, d, e, b, cor, 0f, 0f, 0.6f);
            }

            // ------------------------------------------------ os andares da copa
            int andares = lod == 0 ? p.andares : lod == 1 ? Mathf.Max(3, p.andares / 2 + 1) : Mathf.Clamp(p.andares / 3 + 1, 3, 4);
            int pontas = lod == 0 ? p.pontas : lod == 1 ? 5 : 5;
            float y0 = p.altura * p.comecoDaCopa, y1 = p.altura * 0.94f;
            float raioNormal = p.altura * (p.tamanho == TamanhoDoPinheiro.Pequeno ? 0.16f : 0.13f) * p.largura;
            float faseDaArvore = Entre(0f, 1f);
            for (int a = 0; a < andares; a++)
            {
                float t = andares == 1 ? 0f : a / (float)(andares - 1);
                // os andares mais juntos em cima; o raio cai para a ponta, com um sobe e desce entre vizinhos
                float y = Mathf.Lerp(y0, y1, Mathf.Pow(t, 0.85f));
                float raio = raioNormal * Mathf.Lerp(1f, 0.2f, Mathf.Pow(t, 0.9f)) * Entre(0.92f, 1.08f);
                // as pontas caem mais nos andares de baixo (os galhos velhos), e cada andar encaixa no de cima
                float caida = p.caida * raio * (1f + 0.5f * (1f - t));
                float espessura = Mathf.Max(0.22f, raio * 0.42f);
                float giro = Entre(0f, 6.28f);
                float fase = Mathf.Repeat(faseDaArvore + a * 0.13f, 1f);
                var centro = Eixo(y);
                var centroBaixo = centro + Vector3.down * (caida * 0.75f + 0.04f);
                var centroTopo = centro + Vector3.up * espessura;
                // as cores do andar: embaixo mais escuro, em cima mais claro
                bool baixo = t < 0.35f;
                if (lod == 2)
                {
                    // de longe: um cone simples com o fundo
                    int n = 6;
                    for (int j = 0; j < n; j++)
                    {
                        float a0 = giro + 6.2831853f * j / n, a1 = giro + 6.2831853f * (j + 1) / n;
                        var l0 = centro + new Vector3(Mathf.Cos(a0) * raio, -caida, Mathf.Sin(a0) * raio);
                        var l1 = centro + new Vector3(Mathf.Cos(a1) * raio, -caida, Mathf.Sin(a1) * raio);
                        c.Tri(l0, centroTopo, l1, j % 2 == 0 ? meia : (baixo ? escura : clara), fase, 1f);
                        c.Tri(l0, l1, centroBaixo, escura, fase, 1f);
                    }
                    continue;
                }
                int k = pontas, n2 = k * 2;
                var ringBaixo = new Vector3[n2];
                var ringTopo = new Vector3[k];
                for (int j = 0; j < n2; j++)
                {
                    bool ponta = j % 2 == 0;
                    float ang = giro + 6.2831853f * j / n2 + Entre(-0.06f, 0.06f);
                    float rr = ponta ? raio * Entre(0.9f, 1.12f) : raio * Entre(0.58f, 0.72f);
                    float dy = ponta ? -caida * Entre(0.8f, 1.25f) : -caida * 0.35f;
                    ringBaixo[j] = centro + new Vector3(Mathf.Cos(ang) * rr, dy, Mathf.Sin(ang) * rr);
                    if (ponta) ringTopo[j / 2] = centro + new Vector3(Mathf.Cos(ang) * raio * 0.4f, espessura * Entre(0.85f, 1.1f), Mathf.Sin(ang) * raio * 0.4f);
                }
                for (int j = 0; j < k; j++)
                {
                    var l0 = ringBaixo[2 * j];
                    var l1 = ringBaixo[2 * j + 1];
                    var l2 = ringBaixo[(2 * j + 2) % n2];
                    var u0 = ringTopo[j];
                    var u1 = ringTopo[(j + 1) % k];
                    // os lados: da ponta ao vão e do vão à próxima ponta, com o topo
                    var corLado = r.NextDouble() < 0.4 ? clara : meia;
                    c.Tri(l0, u0, l1, corLado, fase, 1f);
                    c.Tri(l1, u1, l2, r.NextDouble() < 0.4 ? clara : meia, fase, 1f);
                    c.Tri(l1, u0, u1, meia, fase, 1f);
                    // o fundo escuro
                    c.Tri(l0, l1, centroBaixo, escura, fase, 1f);
                    c.Tri(l1, l2, centroBaixo, escura, fase, 1f);
                    // o topo (visto de cima)
                    c.Tri(u0, centroTopo, u1, baixo ? meia : clara, fase, 1f);
                }
            }

            // ------------------------------------------------ a ponta
            {
                var baseDaPonta = Eixo(y1);
                var topo = Eixo(p.altura) + Vector3.up * 0.02f;
                float raio = raioNormal * 0.16f;
                int n = lod == 0 ? 5 : 4;
                float giro = Entre(0f, 6.28f);
                for (int j = 0; j < n; j++)
                {
                    float a0 = giro + 6.2831853f * j / n, a1 = giro + 6.2831853f * (j + 1) / n;
                    var l0 = baseDaPonta + new Vector3(Mathf.Cos(a0) * raio, -raio * 0.4f, Mathf.Sin(a0) * raio);
                    var l1 = baseDaPonta + new Vector3(Mathf.Cos(a1) * raio, -raio * 0.4f, Mathf.Sin(a1) * raio);
                    c.Tri(l0, topo, l1, j % 2 == 0 ? clara : meia, faseDaArvore, 1f);
                    c.Tri(l0, l1, baseDaPonta + Vector3.down * raio * 0.6f, escura, faseDaArvore, 1f);
                }
            }
        }
    }
}
