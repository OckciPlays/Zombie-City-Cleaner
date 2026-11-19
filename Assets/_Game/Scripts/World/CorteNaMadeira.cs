using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O corte do machado na madeira (18/11/2026, a pedido: "ao invés de ficar esse risco preto/branco ... realmente fazer um
    /// desgaste na árvore, inclusive com algumas enroscadas que o machado dá com os golpes"): o entalhe em cunha, como numa
    /// árvore cortada de verdade, no pinheiro em pé e no tronco caído (LakewoodNaCena.Arvores.cs e LakewoodNaCena.Troncos.cs).
    /// <list type="bullet">
    /// <item>a boca: a abertura do corte na casca (EntalheBoca.shader), que só marca no estêncil onde ela aparece;</item>
    /// <item>a madeira de dentro (EntalheDentro.shader): o fundo reto, com os anéis, e o alto inclinado, com as fibras e os
    /// degraus dos golpes, desenhados "dentro" da árvore só onde a boca marcou. A árvore não muda (o shader dela é o mesmo);</item>
    /// <item>as lascas presas na borda, meio arrancadas (as "enroscadas"), e os cavacos que caem no chão a cada golpe.</item>
    /// </list>
    /// A malha fica no espaço do pé da árvore, com o eixo do tronco no Y: o lado (de 0 a 3, de 90 em 90° a partir do norte), a
    /// altura do corte, o raio da casca e a fundura (de 0 a 1). Cada corte visível usa um número no estêncil (de 0 a 6: os
    /// quatro lados do pinheiro em pé e os três cortes do tronco caído), para a madeira de um não aparecer na boca do outro.
    /// </summary>
    public static class CorteNaMadeira
    {
        static Shader _boca, _dentro;
        static readonly Material[] _bocas = new Material[7], _dentros = new Material[7];
        static Material _lascas;
        static bool _avisou;

        /// <summary>Os materiais do corte de número 'numero' (de 0 a 6): a boca, a madeira de dentro e as lascas.</summary>
        public static Material[] Materiais(int numero)
        {
            numero = Mathf.Clamp(numero, 0, 6);
            if (_boca == null)
            {
                _boca = Resources.Load<Shader>("EntalheBoca");
                _dentro = Resources.Load<Shader>("EntalheDentro");
            }
            if (_lascas == null)
            {
                _lascas = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Lascas da madeira" };
                _lascas.SetColor("_BaseColor", new Color(0.80f, 0.66f, 0.46f));
                _lascas.SetFloat("_Smoothness", 0.05f);
                _lascas.SetFloat("_Cull", 0f);   // as lascas e os cavacos, dos dois lados
                _lascas.doubleSidedGI = true;
            }
            if (_boca == null || _dentro == null)
            {
                if (!_avisou) Debug.LogWarning("[Corte] Sem os shaders EntalheBoca e EntalheDentro (Shaders/Resources): o corte fica só com as lascas.");
                _avisou = true;
                return new[] { _lascas, _lascas, _lascas };
            }
            if (_bocas[numero] == null)
            {
                _bocas[numero] = new Material(_boca) { name = $"Entalhe (boca {numero})" };
                _bocas[numero].SetFloat("_Ref", (numero + 1) * 32);
                _dentros[numero] = new Material(_dentro) { name = $"Entalhe (madeira {numero})" };
                _dentros[numero].SetFloat("_Ref", (numero + 1) * 32);
            }
            return new[] { _bocas[numero], _dentros[numero], _lascas };
        }

        // a cor da madeira nova (o pinheiro por dentro: um creme amarelado), o fundo do corte mais escuro (no espaço linear:
        // a cor dos vértices vai direto para o shader; sem a conversão, ficava quase branca)
        static readonly Color Clara = new Color(0.86f, 0.70f, 0.48f).linear, Fibra = new Color(0.80f, 0.63f, 0.42f).linear;

        /// <summary>
        /// O entalhe em cunha no lado 'lado' do tronco, com o fundo reto na 'altura' (m, do pé, ao longo do tronco), na casca de
        /// raio 'raio' e com a fundura de 0 a 1 (1: uns três quartos do raio). Três partes: a boca, a madeira de dentro e as
        /// lascas. A semente deixa os degraus e as lascas iguais em todas as máquinas e a cada golpe (o corte só cresce).
        /// </summary>
        /// <param name="raioNoAngulo">O raio da casca na altura do corte em cada ângulo (radianos, como o atan2(x, z) no
        /// espaço do corte), para a boca e a borda acompanharem as faces do tronco; null ou menor que 1 cm, o 'raio'.</param>
        public static Mesh Malha(int lado, float fundura, float altura, float raio, int semente, System.Func<float, float> raioNoAngulo = null)
        {
            fundura = Mathf.Clamp01(fundura);
            float R = Mathf.Max(0.05f, raio);
            float D = Mathf.Max(0.04f, fundura) * 0.75f * R;
            float H = Mathf.Clamp(R * 0.9f, 0.10f, 0.32f) * Mathf.Lerp(0.35f, 1f, fundura);
            float tmax = Mathf.Acos(Mathf.Clamp((R - D) / R, -1f, 1f));
            float fi = (lado & 3) * 90f * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Sin(fi), 0f, Mathf.Cos(fi));
            var e = new Vector3(Mathf.Cos(fi), 0f, -Mathf.Sin(fi));
            Vector3 Fora(float t) => d * Mathf.Cos(t) + e * Mathf.Sin(t);
            // o raio da casca no ângulo t do corte (o ângulo do mundo é o do lado mais t)
            float Casca(float t)
            {
                float medido = raioNoAngulo != null ? raioNoAngulo(fi + t) : -1f;
                return medido > 0.01f ? medido : R;
            }
            var sombra = new List<Vector2>();   // (o y fica no uv2.y; o uv2.x e o uv3 levam o ponto de fora, para a sombra)
            var sombraXZ = new List<Vector2>();
            float Alto(float s) => H * (s - (R - D)) / D;   // a altura do corte inclinado na distância s, ao longo de d
            var rnd = new System.Random(semente);
            float Sorte() => (float)rnd.NextDouble();
            void Sombra(float t, float y)
            {
                var f = Fora(t) * (Casca(t) + 0.06f);
                sombraXZ.Add(new Vector2(f.x, f.z));
                sombra.Add(new Vector2(y, 0f));
            }

            var v = new List<Vector3>();
            var c = new List<Color>();
            var boca = new List<int>();
            var dentro = new List<int>();
            var lascas = new List<int>();
            const int N = 14, J = 4;
            Vector3 cima = Vector3.up;

            // o triângulo virado para 'n' (a frente da face, para o culling)
            void Tri(List<int> lista, int a, int b, int k, Vector3 n)
            {
                if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[k] - v[a]), n) >= 0f) lista.AddRange(new[] { a, b, k });
                else lista.AddRange(new[] { a, k, b });
            }

            // 1. o fundo reto: entre a linha do fundo (a "dobradiça") e a casca, na altura do corte (os anéis)
            int fundo0 = v.Count;
            for (int k = 0; k <= N; k++)
            {
                float t = Mathf.Lerp(-tmax, tmax, k / (float)N);
                var casca = Fora(t) * Casca(t);
                var dobra = d * (R - D) + e * (R * Mathf.Sin(t));
                for (int j = 0; j <= 2; j++)
                {
                    float u = j / 2f;
                    var p = Vector3.Lerp(dobra, casca, u);
                    float pulo = j == 1 ? (Sorte() - 0.5f) * 0.006f : 0f;
                    v.Add(p + cima * (altura + pulo));
                    Sombra(t, altura + 0.02f);
                    float claro = Mathf.Lerp(0.62f, 1f, u);
                    c.Add(new Color(Clara.r * claro, Clara.g * claro, Clara.b * claro, 1f));
                }
            }
            for (int k = 0; k < N; k++)
                for (int j = 0; j < 2; j++)
                {
                    int a = fundo0 + k * 3 + j, b = a + 1, a2 = a + 3, b2 = a2 + 1;
                    Tri(dentro, a, b, b2, cima);
                    Tri(dentro, a, b2, a2, cima);
                }

            // 2. o alto inclinado: da dobradiça até a casca, subindo (as fibras e os degraus de cada golpe)
            var normalInclinada = (d * (H / D) - cima).normalized;
            int degraus = 2 + Mathf.RoundToInt(fundura * 5f);
            int alto0 = v.Count;
            for (int k = 0; k <= N; k++)
            {
                float t = Mathf.Lerp(-tmax, tmax, k / (float)N);
                float rc = Casca(t);
                float sCasca = rc * Mathf.Cos(t);
                for (int j = 0; j <= J; j++)
                {
                    float u = j / (float)J;
                    float s = Mathf.Lerp(R - D, sCasca, u);
                    float lat = Mathf.Lerp(R * Mathf.Sin(t), rc * Mathf.Sin(t), u);
                    float alto = Mathf.Max(0f, Alto(Mathf.Lerp(R - D, R * Mathf.Cos(t), u)));
                    var p = d * s + e * lat + cima * (altura + alto);
                    Sombra(t, altura + alto);
                    // os degraus dos golpes (cada golpe tira uma lasca e deixa um degrau) e um pouco de aspereza
                    float degrau = Mathf.Repeat(u * degraus, 1f);
                    float bate = (degrau - 0.5f) * 0.008f * Mathf.Sin(u * Mathf.PI);
                    float aspero = (j > 0 && j < J && k > 0 && k < N) ? (Sorte() - 0.5f) * 0.007f : 0f;
                    v.Add(p + normalInclinada * (bate + aspero));
                    float claro = Mathf.Lerp(0.55f, 0.95f, u);
                    c.Add(new Color(Fibra.r * claro, Fibra.g * claro, Fibra.b * claro, 0f));
                }
            }
            for (int k = 0; k < N; k++)
                for (int j = 0; j < J; j++)
                {
                    int a = alto0 + k * (J + 1) + j, b = a + 1, a2 = a + J + 1, b2 = a2 + 1;
                    Tri(dentro, a, b, b2, normalInclinada);
                    Tri(dentro, a, b2, a2, normalInclinada);
                }

            // 3. a boca: a casca entre o fundo e o alto, um pouco por fora (o estêncil)
            int boca0 = v.Count;
            for (int k = 0; k <= N; k++)
            {
                float t = Mathf.Lerp(-tmax, tmax, k / (float)N);
                float topo = Mathf.Max(0f, Alto(R * Mathf.Cos(t)));
                float rb = Casca(t) + 0.012f;   // (a casca passa do calculado até uns 3 mm nas quinas)
                v.Add(Fora(t) * rb + cima * (altura - 0.008f));
                v.Add(Fora(t) * rb + cima * (altura + topo + 0.008f));
                c.Add(Color.white);
                c.Add(Color.white);
                Sombra(t, altura);
                Sombra(t, altura + topo);
            }
            for (int k = 0; k < N; k++)
            {
                int a = boca0 + k * 2;
                boca.AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 });
            }

            // 4. as lascas meio arrancadas na borda: no alto (as fibras levantadas pelo machado) e no fundo
            int quantas = 3 + Mathf.RoundToInt(fundura * 8f);
            var normaisDasLascas = new List<(int, Vector3)>();
            for (int i = 0; i < quantas; i++)
            {
                float t = Mathf.Lerp(-tmax * 0.85f, tmax * 0.85f, Sorte());
                bool noAlto = Sorte() < 0.65f;
                var fora = Fora(t);
                var ao = Vector3.Cross(cima, fora).normalized;   // ao longo da borda
                float topo = Mathf.Max(0f, Alto(R * Mathf.Cos(t)));
                var base0 = fora * (Casca(t) + 0.004f) + cima * (altura + (noAlto ? topo : 0f));
                float comprido = Mathf.Lerp(0.025f, 0.07f, Sorte()) * Mathf.Lerp(0.6f, 1f, fundura);
                float largo = Mathf.Lerp(0.010f, 0.024f, Sorte());
                // a lasca sai da borda para fora e se curva: do alto, para baixo (ela pende); do fundo, para cima
                var ponta = base0 + fora * comprido * 0.75f + cima * (noAlto ? -comprido * 0.55f : comprido * 0.35f)
                            + ao * (Sorte() - 0.5f) * 0.02f;
                int a = v.Count;
                v.Add(base0 - ao * largo * 0.5f);
                v.Add(base0 + ao * largo * 0.5f);
                v.Add(ponta);
                c.Add(Clara); c.Add(Clara); c.Add(Fibra);
                Sombra(t, altura); Sombra(t, altura); Sombra(t, altura);
                var nl = (fora + cima * 0.8f).normalized;
                for (int q = 0; q < 3; q++) normaisDasLascas.Add((a + q, nl));
                lascas.AddRange(new[] { a, a + 1, a + 2 });   // (o material desenha os dois lados)
            }

            var malha = new Mesh { name = "Corte do machado" };
            malha.SetVertices(v);
            malha.SetColors(c);
            // o ponto de fora de cada vértice (uns 6 cm além da casca, na mesma altura), no uv2 (x, z) e no uv3 (y): a madeira
            // de dentro pergunta a sombra do sol ali (no próprio ponto, dentro da árvore, a casca sempre fazia sombra)
            malha.SetUVs(1, sombraXZ);
            malha.SetUVs(2, sombra);
            malha.subMeshCount = 3;
            malha.SetTriangles(boca, 0);
            malha.SetTriangles(dentro, 1);
            malha.SetTriangles(lascas, 2);
            malha.RecalculateNormals();
            var normais = malha.normals;
            foreach (var (i, nn) in normaisDasLascas) normais[i] = nn;
            malha.normals = normais;
            malha.RecalculateBounds();
            return malha;
        }

        // ------------------------------------------------------------------ os cavacos no chão

        const int MaxCavacos = 60;
        static readonly Queue<GameObject> _cavacos = new Queue<GameObject>();
        static Mesh _malhaDoCavaco;

        /// <summary>Uns cavacos (os pedaços que o machado arranca) caem no chão perto do golpe, para o lado de quem bateu.</summary>
        public static void Cavacos(Vector3 ponto, Vector3 para, System.Func<float, float, float> chao, int quantos = 2)
        {
            if (chao == null) return;
            if (_malhaDoCavaco == null) _malhaDoCavaco = MalhaDoCavaco();
            Materiais(0);
            para.y = 0f;
            para = para.sqrMagnitude > 0.001f ? para.normalized : Vector3.forward;
            for (int i = 0; i < quantos; i++)
            {
                GameObject go;
                while (_cavacos.Count > 0 && _cavacos.Peek() == null) _cavacos.Dequeue();
                if (_cavacos.Count >= MaxCavacos) go = _cavacos.Dequeue();
                else
                {
                    go = new GameObject("Cavaco");
                    go.AddComponent<MeshFilter>().sharedMesh = _malhaDoCavaco;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = _lascas;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                var lado = Vector3.Cross(Vector3.up, para);
                var p = ponto + para * Random.Range(0.3f, 1.2f) + lado * Random.Range(-0.6f, 0.6f);
                p.y = chao(p.x, p.z) + 0.006f;
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f)));
                go.transform.localScale = Vector3.one * Random.Range(0.7f, 1.3f);
                go.SetActive(true);
                _cavacos.Enqueue(go);
            }
        }

        // um cavaco: uma lasca chata e torta, de uns 6 x 3 cm
        static Mesh MalhaDoCavaco()
        {
            var v = new[]
            {
                new Vector3(-0.030f, 0f, -0.012f), new Vector3(0.028f, 0f, -0.016f), new Vector3(0.034f, 0f, 0.010f), new Vector3(-0.024f, 0f, 0.016f),
                new Vector3(-0.030f, 0.007f, -0.010f), new Vector3(0.026f, 0.005f, -0.014f), new Vector3(0.032f, 0.006f, 0.008f), new Vector3(-0.022f, 0.008f, 0.014f),
            };
            var t = new[]
            {
                4, 6, 5, 4, 7, 6,   // em cima
                0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7,
            };
            var m = new Mesh { name = "Cavaco" };
            m.vertices = v;
            m.triangles = t;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
