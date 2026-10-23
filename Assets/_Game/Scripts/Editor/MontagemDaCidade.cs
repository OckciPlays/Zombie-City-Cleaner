using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Monta a cidade em blocos (Parte 4): ruas, calçadas, o prédio de 4 andares, as 5 casas (uma é a da família,
    /// com o bunker embaixo), carros abandonados, cercas, postes, o anel de pinheiros e a barreira invisível.
    /// Tudo é caixa com cor sólida; a arte final (Parte 8) troca os blocos pelos modelos.
    /// Menu: Zombie City Cleaner > Montar cidade em blocos. Pode rodar de novo: apaga e refaz a raiz "Cidade".
    /// </summary>
    public static class MontagemDaCidade
    {
        const string PastaMateriais = "Assets/_Game/Art/Materials/Blocos";
        const string PastaMalhas = "Assets/_Game/Art/Meshes";
        const float AlturaParede = 2.8f, Espessura = 0.2f, Porta = 1.1f, AlturaPorta = 2.1f;

        static Dictionary<string, Material> _m;
        static Rect? _buracoNoChao;
        static Mesh _prisma, _cone, _pinheiro;
        static System.Random _rng;

        [MenuItem("Zombie City Cleaner/Montar cidade em blocos")]
        public static void Montar()
        {
            Directory.CreateDirectory(PastaMateriais);
            Directory.CreateDirectory(PastaMalhas);
            _rng = new System.Random(1987);
            Materiais();
            Malhas();

            var cena = EditorSceneManager.GetActiveScene();
            foreach (var nome in new[] { "Cidade", "Teste (temporário)" })
            {
                var velho = cena.GetRootGameObjects().FirstOrDefault(g => g.name == nome);
                if (velho != null) Object.DestroyImmediate(velho);
            }
            var raiz = new GameObject("Cidade").transform;

            _buracoNoChao = null;
            Ruas(raiz);
            Postes(raiz);
            Predio(Grupo(raiz, "Prédio"), new Vector3(20f, 0f, 21f), 0f);
            Casa(Grupo(raiz, "Casa 1"), new Vector3(-27f, 0f, 19f), 0f, 10f, 8f, "Parede_Verde", false);
            Casa(Grupo(raiz, "Casa 2"), new Vector3(-12f, 0f, 23f), 0f, 9f, 8f, "Parede_Bege", false);
            Casa(Grupo(raiz, "Casa da família"), new Vector3(-20f, 0f, -18f), 180f, 12f, 10f, "Parede_Azul", true);
            Casa(Grupo(raiz, "Casa 4"), new Vector3(13f, 0f, -14f), 180f, 9f, 8f, "Parede_Bege", false);
            Casa(Grupo(raiz, "Casa 5"), new Vector3(28f, 0f, -26f), 270f, 10f, 8f, "Parede_Verde", false);
            Praca(Grupo(raiz, "Praça"), new Vector3(24f, 0f, -12f));
            Estacionamento(Grupo(raiz, "Estacionamento"), new Vector3(10f, 0f, 33f));
            Carros(Grupo(raiz, "Carros"));
            Cercas(Grupo(raiz, "Cercas"));
            Arvores(Grupo(raiz, "Árvores"));
            Barricadas(Grupo(raiz, "Barricadas"));
            Pinheiros(Grupo(raiz, "Pinheiros"));
            Barreira(Grupo(raiz, "Barreira invisível"));
            Chao(raiz);   // por último: precisa do buraco da escada do bunker, que a casa da família marca

            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Light>() != null || t.GetComponent<PosteDeLuz>() != null || t.name == "Cone de luz") continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            PontosNaCasaDaFamilia(cena);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            AssetDatabase.SaveAssets();
            Debug.Log("Cidade em blocos montada.");
        }

        // ------------------------------------------------------------------ materiais e malhas

        static void Materiais()
        {
            _m = new Dictionary<string, Material>();
            var cores = new Dictionary<string, string>
            {
                ["Asfalto"] = "#3A3D42", ["Calcada"] = "#8A8A84", ["MeioFio"] = "#B8B29A", ["Grama"] = "#4B5240", ["Terra"] = "#5A4B3C",
                ["Parede_Verde"] = "#6E7B6A", ["Parede_Bege"] = "#8C7B6A", ["Parede_Azul"] = "#5E6B7A", ["Parede_Interna"] = "#A8A49A",
                ["Piso"] = "#6E655A", ["Teto"] = "#B8B5AC", ["Telhado"] = "#4A3B38", ["Telhado_Azul"] = "#3E4A52", ["Tijolo"] = "#7A4A3C",
                ["Concreto"] = "#6A6A64", ["Bunker"] = "#5E5E58", ["Batente"] = "#D8D2C0", ["Madeira"] = "#5A4636", ["Cerca"] = "#8C7B6A",
                ["Pinheiro"] = "#2F3D33", ["Tronco"] = "#4A3A2E", ["Copa"] = "#3E4A38", ["Poste"] = "#2E3134", ["Lampada"] = "#FFB46B",
                ["Carro_Azul"] = "#5E6B7A", ["Carro_Vinho"] = "#6B3434", ["Carro_Cinza"] = "#8C8A80", ["Carro_Verde"] = "#3E4A3A",
                ["Vidro"] = "#1E2A30", ["Pneu"] = "#1A1A1C", ["Barreira"] = "#B8B29A", ["Faixa"] = "#B8A24A",
            };
            foreach (var kv in cores)
            {
                string caminho = $"{PastaMateriais}/{kv.Key}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
                if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, caminho); }
                ColorUtility.TryParseHtmlString(kv.Value, out var c);
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", kv.Key == "Vidro" ? 0.6f : kv.Key == "Asfalto" ? 0.2f : 0.08f);
                m.enableInstancing = true;
                if (kv.Key == "Lampada") { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 2.5f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
                EditorUtility.SetDirty(m);
                _m[kv.Key] = m;
            }
            // cone de luz dos postes: aditivo, dos dois lados, mais forte perto da lâmpada
            string caminhoCone = $"{PastaMateriais}/ConeDeLuz.mat";
            var cone = AssetDatabase.LoadAssetAtPath<Material>(caminhoCone);
            if (cone == null) { cone = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(cone, caminhoCone); }
            cone.SetFloat("_Surface", 1f); cone.SetFloat("_Blend", 2f); cone.SetFloat("_Cull", 0f);
            cone.SetOverrideTag("RenderType", "Transparent"); cone.renderQueue = (int)RenderQueue.Transparent;
            cone.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); cone.SetInt("_DstBlend", (int)BlendMode.One); cone.SetInt("_ZWrite", 0);
            cone.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            cone.SetColor("_BaseColor", new Color(1f, 0.71f, 0.42f, 0.11f));
            cone.SetTexture("_BaseMap", Degrade());
            EditorUtility.SetDirty(cone);
            _m["ConeDeLuz"] = cone;
        }

        static Texture2D Degrade()
        {
            string caminho = $"{PastaMateriais}/DegradeCone.png";
            {
                var t = new Texture2D(4, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                {
                    float v = y / 63f;                       // 1 = ponta do cone (lâmpada)
                    float a = Mathf.Pow(v, 2.4f);
                    for (int x = 0; x < 4; x++) t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                File.WriteAllBytes(caminho, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(caminho);
                var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
                imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }

        static Mesh SalvarMalha(Mesh m, string nome)
        {
            string caminho = $"{PastaMalhas}/{nome}.asset";
            var velha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (velha != null) { EditorUtility.CopySerialized(m, velha); Object.DestroyImmediate(m); return velha; }
            AssetDatabase.CreateAsset(m, caminho);
            return m;
        }

        static void Malhas()
        {
            // prisma do telhado: base 1x1 (x, z) em y = 0, cumeeira no centro em y = 1, ao longo de z
            var p = new Mesh { name = "Prisma" };
            var v = new List<Vector3>(); var tri = new List<int>();
            void Face(params Vector3[] q)
            {
                int b = v.Count; v.AddRange(q);
                for (int i = 1; i + 1 < q.Length; i++) { tri.Add(b); tri.Add(b + i); tri.Add(b + i + 1); }
            }
            Vector3 a0 = new Vector3(-0.5f, 0, -0.5f), a1 = new Vector3(0.5f, 0, -0.5f), b0 = new Vector3(-0.5f, 0, 0.5f), b1 = new Vector3(0.5f, 0, 0.5f);
            Vector3 c0 = new Vector3(0, 1, -0.5f), c1 = new Vector3(0, 1, 0.5f);
            Face(a0, c0, c1, b0); Face(b1, c1, c0, a1); Face(a1, c0, a0); Face(b0, c1, b1); Face(a0, b0, b1, a1);
            p.SetVertices(v); p.SetTriangles(tri, 0); p.RecalculateNormals(); p.RecalculateBounds();
            _prisma = SalvarMalha(p, "Prisma");

            // cone aberto: ponta em y = 0, base de raio 1 em y = -1; v = 1 na ponta
            var c = new Mesh { name = "ConeDeLuz" };
            var cv = new List<Vector3>(); var cuv = new List<Vector2>(); var ct = new List<int>();
            const int lados = 20;
            for (int i = 0; i <= lados; i++)
            {
                float ang = i * Mathf.PI * 2f / lados;
                cv.Add(Vector3.zero); cuv.Add(new Vector2(i / (float)lados, 1f));
                cv.Add(new Vector3(Mathf.Cos(ang), -1f, Mathf.Sin(ang))); cuv.Add(new Vector2(i / (float)lados, 0f));
            }
            for (int i = 0; i < lados; i++) { int b = i * 2; ct.AddRange(new[] { b, b + 3, b + 1 }); }
            c.SetVertices(cv); c.SetUVs(0, cuv); c.SetTriangles(ct, 0); c.RecalculateNormals(); c.RecalculateBounds();
            _cone = SalvarMalha(c, "ConeDeLuz");

            // pinheiro: tronco (submalha 0) e três camadas de copa piramidal de 8 lados (submalha 1)
            var pm = new Mesh { name = "Pinheiro" };
            var pv = new List<Vector3>(); var t0 = new List<int>(); var t1 = new List<int>();
            void Piramide(List<int> alvo, float baseY, float raio, float altura, int n, float giro)
            {
                for (int i = 0; i < n; i++)
                {
                    float a = giro + i * Mathf.PI * 2f / n, b = giro + (i + 1) * Mathf.PI * 2f / n;
                    int k = pv.Count;
                    pv.Add(new Vector3(0, baseY + altura, 0)); pv.Add(new Vector3(Mathf.Cos(b) * raio, baseY, Mathf.Sin(b) * raio)); pv.Add(new Vector3(Mathf.Cos(a) * raio, baseY, Mathf.Sin(a) * raio));
                    alvo.AddRange(new[] { k, k + 1, k + 2 });
                    k = pv.Count;
                    pv.Add(new Vector3(0, baseY, 0)); pv.Add(new Vector3(Mathf.Cos(a) * raio, baseY, Mathf.Sin(a) * raio)); pv.Add(new Vector3(Mathf.Cos(b) * raio, baseY, Mathf.Sin(b) * raio));
                    alvo.AddRange(new[] { k, k + 1, k + 2 });
                }
            }
            Piramide(t0, 0f, 0.25f, 3.5f, 6, 0f);
            Piramide(t1, 1.6f, 2.3f, 3.2f, 8, 0.2f);
            Piramide(t1, 3.4f, 1.8f, 2.8f, 8, 0.6f);
            Piramide(t1, 5.0f, 1.2f, 2.6f, 8, 0.1f);
            pm.subMeshCount = 2; pm.SetVertices(pv); pm.SetTriangles(t0, 0); pm.SetTriangles(t1, 1);
            pm.RecalculateNormals(); pm.RecalculateBounds();
            _pinheiro = SalvarMalha(pm, "Pinheiro");
        }

        // ------------------------------------------------------------------ peças básicas

        static Transform Grupo(Transform pai, string nome)
        {
            var g = new GameObject(nome).transform; g.SetParent(pai, false); return g;
        }

        static GameObject Caixa(Transform pai, string nome, Vector3 centro, Vector3 tamanho, string mat, float yaw = 0f, bool colisor = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nome;
            go.transform.SetParent(pai, false);
            go.transform.localPosition = centro;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = tamanho;
            go.GetComponent<MeshRenderer>().sharedMaterial = _m[mat];
            if (!colisor) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Abertura numa parede: posição do começo ao longo da parede, largura, altura de baixo e de cima.</summary>
        struct Abertura
        {
            public float inicio, largura, baixo, alto;
            public Abertura(float inicio, float largura, float baixo, float alto) { this.inicio = inicio; this.largura = largura; this.baixo = baixo; this.alto = alto; }
            public static Abertura Porta(float centro) => new Abertura(centro - MontagemDaCidade.Porta / 2f, MontagemDaCidade.Porta, 0f, AlturaPorta);
            public static Abertura Janela(float centro, float largura = 1.2f) => new Abertura(centro - largura / 2f, largura, 0.9f, 2.1f);
        }

        /// <summary>Parede reta de a até b (no plano), com portas e janelas. As partes sólidas viram caixas.</summary>
        static void Parede(Transform pai, string nome, Vector2 a, Vector2 b, float baseY, float altura, string mat, params Abertura[] aberturas)
        {
            Vector2 d = b - a; float comprimento = d.magnitude; Vector2 u = d / comprimento;
            float yaw = -Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg;
            void Pedaco(float s0, float s1, float y0, float y1)
            {
                if (s1 - s0 < 0.01f || y1 - y0 < 0.01f) return;
                Vector2 m = a + u * ((s0 + s1) / 2f);
                Caixa(pai, nome, new Vector3(m.x, baseY + (y0 + y1) / 2f, m.y), new Vector3(s1 - s0, y1 - y0, Espessura), mat, yaw);
            }
            float s = 0f;
            foreach (var ab in aberturas.OrderBy(x => x.inicio))
            {
                Pedaco(s, ab.inicio, 0f, altura);
                Pedaco(ab.inicio, ab.inicio + ab.largura, 0f, ab.baixo);
                Pedaco(ab.inicio, ab.inicio + ab.largura, ab.alto, altura);
                if (ab.baixo > 0.01f) Caixa(pai, "Peitoril", new Vector3((a + u * (ab.inicio + ab.largura / 2f)).x, baseY + ab.baixo - 0.03f, (a + u * (ab.inicio + ab.largura / 2f)).y),
                    new Vector3(ab.largura, 0.06f, Espessura + 0.1f), "Batente", yaw, false);
                s = ab.inicio + ab.largura;
            }
            Pedaco(s, comprimento, 0f, altura);
        }

        /// <summary>Laje retangular (x0..x1, z0..z1) com o topo em y e um buraco opcional.</summary>
        static void Laje(Transform pai, string nome, float x0, float x1, float z0, float z1, float topo, float espessura, string mat, Rect? buraco = null)
        {
            if (buraco == null)
            {
                Caixa(pai, nome, new Vector3((x0 + x1) / 2f, topo - espessura / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, espessura, z1 - z0), mat);
                return;
            }
            var h = buraco.Value;
            Laje(pai, nome, x0, x1, z0, h.yMin, topo, espessura, mat);
            Laje(pai, nome, x0, x1, h.yMax, z1, topo, espessura, mat);
            Laje(pai, nome, x0, h.xMin, h.yMin, h.yMax, topo, espessura, mat);
            Laje(pai, nome, h.xMax, x1, h.yMin, h.yMax, topo, espessura, mat);
        }

        /// <summary>Escada reta: degraus maciços saindo de (x, y, z) na direção dir (+1/-1 em z), com largura em x.</summary>
        static void Escada(Transform pai, float xCentro, float largura, float y0, float z0, int degraus, float altura, float piso, int dir, string mat)
        {
            for (int i = 0; i < degraus; i++)
            {
                float topo = y0 + altura * (i + 1);
                float zc = z0 + dir * (piso * i + piso / 2f);
                float alturaBloco = Mathf.Min(topo - y0 + 0.02f, 0.6f);
                Caixa(pai, "Degrau", new Vector3(xCentro, topo - alturaBloco / 2f, zc), new Vector3(largura, alturaBloco, piso), mat);
            }
        }

        static void Telhado(Transform pai, Vector3 centro, float largura, float profundidade, float altura, string mat)
        {
            var go = new GameObject("Telhado");
            go.transform.SetParent(pai, false);
            go.transform.localPosition = centro;
            go.transform.localScale = new Vector3(largura, altura, profundidade);
            go.AddComponent<MeshFilter>().sharedMesh = _prisma;
            go.AddComponent<MeshRenderer>().sharedMaterial = _m[mat];
        }

        // ------------------------------------------------------------------ terreno, ruas e calçadas

        static void Chao(Transform raiz)
        {
            Laje(raiz, "Chão", -110f, 110f, -110f, 110f, -0.02f, 0.1f, "Grama", _buracoNoChao);
        }

        static readonly float[] Linhas = { -40f, 0f, 40f };

        static void Ruas(Transform raiz)
        {
            var g = Grupo(raiz, "Ruas");
            const float meia = 4f, fim = 44f;
            // anel e cruzamento central
            foreach (float x in Linhas) Caixa(g, "Rua", new Vector3(x, -0.05f, 0f), new Vector3(meia * 2f, 0.1f, fim * 2f), "Asfalto");
            foreach (float z in Linhas)
            {
                Caixa(g, "Rua", new Vector3(-22f, -0.049f, z), new Vector3(36f, 0.1f, meia * 2f), "Asfalto");
                Caixa(g, "Rua", new Vector3(22f, -0.049f, z), new Vector3(36f, 0.1f, meia * 2f), "Asfalto");
            }
            // saídas da cidade (bloqueadas mais à frente)
            Caixa(g, "Saída norte", new Vector3(0f, -0.05f, 54f), new Vector3(meia * 2f, 0.1f, 20f), "Asfalto");
            Caixa(g, "Saída sul", new Vector3(0f, -0.05f, -54f), new Vector3(meia * 2f, 0.1f, 20f), "Asfalto");
            Caixa(g, "Saída leste", new Vector3(54f, -0.05f, 0f), new Vector3(20f, 0.1f, meia * 2f), "Asfalto");
            Caixa(g, "Saída oeste", new Vector3(-54f, -0.05f, 0f), new Vector3(20f, 0.1f, meia * 2f), "Asfalto");
            // faixas amarelas tracejadas no meio das ruas principais
            for (float t = -60f; t <= 60f; t += 6f)
            {
                if (Mathf.Abs(t) > 5f) Caixa(g, "Faixa", new Vector3(0f, 0.005f, t), new Vector3(0.15f, 0.01f, 3f), "Faixa", 0f, false);
                if (Mathf.Abs(t) > 5f) Caixa(g, "Faixa", new Vector3(t, 0.005f, 0f), new Vector3(3f, 0.01f, 0.15f), "Faixa", 0f, false);
            }
            // calçadas: um anel em volta de cada quarteirão (2,5 m de largura, 12 cm acima da rua)
            var calcadas = Grupo(raiz, "Calçadas");
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    float x0 = Linhas[i] + meia, x1 = Linhas[i + 1] - meia, z0 = Linhas[j] + meia, z1 = Linhas[j + 1] - meia;
                    AnelDeCalcada(calcadas, x0, x1, z0, z1);
                }
        }

        static void AnelDeCalcada(Transform pai, float x0, float x1, float z0, float z1)
        {
            const float w = 2.5f, y = 0.06f, h = 0.12f;
            Caixa(pai, "Calçada", new Vector3((x0 + x1) / 2f, y, z0 + w / 2f), new Vector3(x1 - x0, h, w), "Calcada");
            Caixa(pai, "Calçada", new Vector3((x0 + x1) / 2f, y, z1 - w / 2f), new Vector3(x1 - x0, h, w), "Calcada");
            Caixa(pai, "Calçada", new Vector3(x0 + w / 2f, y, (z0 + z1) / 2f), new Vector3(w, h, z1 - z0 - 2 * w), "Calcada");
            Caixa(pai, "Calçada", new Vector3(x1 - w / 2f, y, (z0 + z1) / 2f), new Vector3(w, h, z1 - z0 - 2 * w), "Calcada");
        }

        // ------------------------------------------------------------------ postes de luz

        static void Postes(Transform raiz)
        {
            var g = Grupo(raiz, "Postes");
            var lugares = new List<(Vector3 pos, float yaw)>();
            // ao longo das ruas do cruzamento (até as saídas) e do anel, alternando os lados
            int k = 0;
            for (float t = -58f; t <= 58f; t += 14f)
            {
                if (Mathf.Abs(t) < 7f || Mathf.Abs(Mathf.Abs(t) - 40f) < 7f) continue;
                float lado = (k++ % 2 == 0) ? 1f : -1f;
                lugares.Add((new Vector3(5.2f * lado, 0f, t), lado > 0 ? 270f : 90f));
                lugares.Add((new Vector3(t, 0f, -5.2f * lado), lado > 0 ? 0f : 180f));
            }
            foreach (float l in new[] { -40f, 40f })
                for (float t = -30f; t <= 30f; t += 20f)
                {
                    if (Mathf.Abs(t) < 7f) continue;
                    lugares.Add((new Vector3(l - 5.2f * Mathf.Sign(l), 0f, t), l > 0 ? 90f : 270f));
                    lugares.Add((new Vector3(t, 0f, l - 5.2f * Mathf.Sign(l)), l > 0 ? 180f : 0f));
                }
            for (int i = 0; i < lugares.Count; i++)
            {
                var (pos, yaw) = lugares[i];
                var poste = Grupo(g, $"Poste {i + 1}");
                poste.localPosition = pos; poste.localRotation = Quaternion.Euler(0f, yaw, 0f);
                Caixa(poste, "Haste", new Vector3(0f, 3f, 0f), new Vector3(0.16f, 6f, 0.16f), "Poste");
                Caixa(poste, "Braço", new Vector3(0f, 5.9f, 0.7f), new Vector3(0.1f, 0.1f, 1.5f), "Poste", 0f, false);
                var lampada = Caixa(poste, "Lâmpada", new Vector3(0f, 5.8f, 1.35f), new Vector3(0.35f, 0.12f, 0.5f), "Lampada", 0f, false);
                var luzGo = new GameObject("Luz"); luzGo.transform.SetParent(poste, false);
                luzGo.transform.localPosition = new Vector3(0f, 5.65f, 1.35f); luzGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var luz = luzGo.AddComponent<Light>();
                luz.type = LightType.Spot; ColorUtility.TryParseHtmlString("#FFB46B", out var cor); luz.color = cor;
                luz.intensity = 95f; luz.range = 16f; luz.spotAngle = 105f; luz.innerSpotAngle = 35f; luz.shadows = LightShadows.None;
                var cone = new GameObject("Cone de luz"); cone.transform.SetParent(poste, false);
                cone.transform.localPosition = new Vector3(0f, 5.7f, 1.35f); cone.transform.localScale = new Vector3(3.2f, 5.7f, 3.2f);
                cone.AddComponent<MeshFilter>().sharedMesh = _cone;
                var r = cone.AddComponent<MeshRenderer>(); r.sharedMaterial = _m["ConeDeLuz"]; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                var p = poste.gameObject.AddComponent<PosteDeLuz>();
                p.luz = luz; p.cone = r; p.lampada = lampada.GetComponent<MeshRenderer>();
                p.pisca = i % 7 == 3;                                  // alguns postes falham
                p.quebrado = i % 11 == 5;                              // e alguns nem acendem
            }
        }

        // ------------------------------------------------------------------ casas

        /// <summary>Casa térrea com sala na frente e dois cômodos atrás. A frente fica para -Z local (gire com yaw).</summary>
        static void Casa(Transform g, Vector3 centro, float yaw, float W, float D, string parede, bool bunker)
        {
            g.localPosition = centro; g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            const float piso = 0.15f;
            float x0 = -W / 2f, x1 = W / 2f, z0 = -D / 2f, z1 = D / 2f;
            float zDiv = z0 + D * 0.55f;
            // buraco no piso para a escada do bunker: do meio do cômodo de trás (à esquerda) até a borda da laje
            Rect? buraco = bunker ? new Rect(x0 + 0.4f, EscadaDoBunkerInicio(zDiv), 1.2f, z1 + 0.2f - EscadaDoBunkerInicio(zDiv)) : (Rect?)null;
            Laje(g, "Piso", x0 - 0.2f, x1 + 0.2f, z0 - 0.2f, z1 + 0.2f, piso, 0.3f, "Piso", buraco);
            Caixa(g, "Degrau da porta", new Vector3(-W * 0.2f, 0.04f, z0 - 0.6f), new Vector3(1.6f, 0.08f, 0.8f), "Concreto");
            // paredes externas
            Parede(g, "Parede frente", new Vector2(x0, z0), new Vector2(x1, z0), piso, AlturaParede, parede, Abertura.Porta(W * 0.3f), Abertura.Janela(W * 0.72f), Abertura.Janela(W * 0.08f + 0.6f));
            Parede(g, "Parede fundo", new Vector2(x1, z1), new Vector2(x0, z1), piso, AlturaParede, parede, Abertura.Janela(W * 0.25f), Abertura.Porta(W * 0.7f));
            Parede(g, "Parede esquerda", new Vector2(x0, z1), new Vector2(x0, z0), piso, AlturaParede, parede, Abertura.Janela(D * 0.3f), Abertura.Janela(D * 0.75f));
            Parede(g, "Parede direita", new Vector2(x1, z0), new Vector2(x1, z1), piso, AlturaParede, parede, Abertura.Janela(D * 0.25f), Abertura.Janela(D * 0.7f));
            // divisões internas: sala na frente; quarto e cozinha atrás
            Parede(g, "Parede interna", new Vector2(x0 + 0.1f, zDiv), new Vector2(x1 - 0.1f, zDiv), piso, AlturaParede, "Parede_Interna", Abertura.Porta(W * 0.25f), Abertura.Porta(W * 0.75f));
            Parede(g, "Parede interna", new Vector2(0f, zDiv + 0.1f), new Vector2(0f, z1 - 0.1f), piso, AlturaParede, "Parede_Interna");
            Laje(g, "Forro", x0 - 0.1f, x1 + 0.1f, z0 - 0.1f, z1 + 0.1f, piso + AlturaParede + 0.12f, 0.12f, "Teto");
            Telhado(g, new Vector3(0f, piso + AlturaParede + 0.12f, 0f), W + 0.9f, D + 0.9f, 2.0f, parede == "Parede_Azul" ? "Telhado_Azul" : "Telhado");
            // móveis em blocos: dão escala e esconderijos
            Caixa(g, "Sofá", new Vector3(x0 + 1.5f, piso + 0.4f, z0 + 1.0f), new Vector3(2.2f, 0.8f, 0.9f), "Madeira");
            Caixa(g, "Mesa", new Vector3(x1 - 1.6f, piso + 0.38f, z0 + 2.2f), new Vector3(1.4f, 0.76f, 0.9f), "Madeira");
            Caixa(g, "Cama", new Vector3(x1 - 1.4f, piso + 0.25f, z1 - 1.3f), new Vector3(1.6f, 0.5f, 2.0f), "Teto");
            Caixa(g, "Armário", new Vector3(bunker ? -0.6f : x0 + 0.5f, piso + 1.0f, z1 - 0.4f), new Vector3(bunker ? 1.0f : 0.8f, 2.0f, 0.6f), "Madeira");
            var eco = new GameObject("Eco da casa").AddComponent<AudioReverbZone>();
            eco.transform.SetParent(g, false); eco.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            eco.reverbPreset = AudioReverbPreset.Room; eco.minDistance = Mathf.Min(W, D) * 0.4f; eco.maxDistance = Mathf.Max(W, D) * 0.6f;
            if (bunker)
            {
                Bunker(g, x0, zDiv, z1, piso);
                // o chão de grama passa por baixo da casa: abre nele o mesmo buraco da escada
                var b = buraco.Value;
                var cantos = new[] { new Vector3(b.xMin, 0f, b.yMin), new Vector3(b.xMax, 0f, b.yMin), new Vector3(b.xMin, 0f, b.yMax), new Vector3(b.xMax, 0f, b.yMax) }.Select(g.TransformPoint).ToArray();
                float mx = cantos.Min(c => c.x), Mx = cantos.Max(c => c.x), mz = cantos.Min(c => c.z), Mz = cantos.Max(c => c.z);
                _buracoNoChao = new Rect(mx, mz, Mx - mx, Mz - mz);
            }
        }

        /// <summary>
        /// O bunker da família, embaixo do quintal: a escada começa no meio do cômodo de trás, desce para os fundos
        /// (passando por baixo da parede) e chega à porta do bunker, com sala comum, quarto e despensa.
        /// </summary>
        static void Bunker(Transform casa, float x0, float zDiv, float z1, float piso)
        {
            var g = Grupo(casa, "Bunker");
            const float fundo = -3.2f, teto = -0.55f, h = teto - fundo;
            float xEsc = x0 + 1.0f, zTopo = EscadaDoBunkerInicio(zDiv);
            int degraus = 19; float alturaDegrau = (piso - fundo) / degraus, pisoDegrau = 0.26f;
            for (int i = 0; i < degraus; i++)
            {
                float topo = piso - alturaDegrau * (i + 1);
                Caixa(g, "Degrau", new Vector3(xEsc, topo - 0.15f, zTopo + pisoDegrau * (i + 0.5f)), new Vector3(1.2f, 0.3f, pisoDegrau), "Concreto");
            }
            float zBase = zTopo + pisoDegrau * degraus;                       // pé da escada
            float zPorta = zBase + 1.2f;                                       // patamar antes da porta (a cabeça não bate na verga)
            // poço da escada: paredes de concreto dos dois lados e grade em volta do buraco, dentro da casa
            Parede(g, "Poço", new Vector2(xEsc - 0.7f, zTopo), new Vector2(xEsc - 0.7f, zPorta), fundo, piso - fundo, "Bunker");
            Parede(g, "Poço", new Vector2(xEsc + 0.7f, zTopo), new Vector2(xEsc + 0.7f, zPorta), fundo, piso - fundo, "Bunker");
            Parede(g, "Poço", new Vector2(xEsc - 0.7f, zTopo - 0.1f), new Vector2(xEsc + 0.7f, zTopo - 0.1f), fundo, piso - fundo - 0.3f, "Bunker");
            Caixa(g, "Grade", new Vector3(xEsc + 0.65f, piso + 0.5f, (zTopo + z1) / 2f + 0.15f), new Vector3(0.06f, 1.0f, z1 - zTopo - 0.3f), "Poste");
            Caixa(g, "Grade", new Vector3(xEsc - 0.65f, piso + 0.5f, (zTopo + z1) / 2f + 0.15f), new Vector3(0.06f, 1.0f, z1 - zTopo - 0.3f), "Poste");
            // salas: um retângulo depois do pé da escada, embaixo do quintal
            float bx0 = xEsc - 0.7f, bx1 = bx0 + 9.5f, bz0 = zPorta, bz1 = zPorta + 7.5f;
            Laje(g, "Chão do bunker", bx0 - 0.3f, bx1 + 0.3f, zTopo - 0.3f, bz1 + 0.3f, fundo, 0.3f, "Concreto");
            Laje(g, "Teto do bunker", bx0 - 0.3f, bx1 + 0.3f, bz0 - 0.3f, bz1 + 0.3f, teto + 0.3f, 0.3f, "Concreto");
            Parede(g, "Parede", new Vector2(bx0, bz0), new Vector2(bx1, bz0), fundo, h, "Bunker", new Abertura(0.1f, 1.2f, 0f, 2.3f));
            Parede(g, "Parede", new Vector2(bx1, bz0), new Vector2(bx1, bz1), fundo, h, "Bunker");
            Parede(g, "Parede", new Vector2(bx1, bz1), new Vector2(bx0, bz1), fundo, h, "Bunker");
            Parede(g, "Parede", new Vector2(bx0, bz1), new Vector2(bx0, bz0), fundo, h, "Bunker");
            // divisões: sala comum (lado da porta), quarto e despensa (do outro lado)
            float xDiv = bx0 + 5.5f, zMeio = (bz0 + bz1) / 2f;
            Parede(g, "Divisória", new Vector2(xDiv, bz0), new Vector2(xDiv, bz1), fundo, h, "Bunker", Abertura.Porta(1.6f), Abertura.Porta(5.4f));
            Parede(g, "Divisória", new Vector2(xDiv, zMeio), new Vector2(bx1, zMeio), fundo, h, "Bunker");
            // móveis
            Caixa(g, "Mesa", new Vector3(bx0 + 2.8f, fundo + 0.38f, zMeio + 0.5f), new Vector3(1.8f, 0.76f, 1.0f), "Madeira");
            Caixa(g, "Beliche", new Vector3(bx1 - 0.6f, fundo + 0.9f, bz0 + 1.4f), new Vector3(0.9f, 1.8f, 2.0f), "Madeira");
            Caixa(g, "Prateleira", new Vector3(bx1 - 0.35f, fundo + 1.0f, bz1 - 1.8f), new Vector3(0.5f, 2.0f, 2.6f), "Madeira");
            Caixa(g, "Prateleira", new Vector3(xDiv + 1.8f, fundo + 1.0f, bz1 - 0.35f), new Vector3(2.0f, 2.0f, 0.5f), "Madeira");
            var lampada = new GameObject("Lâmpada do bunker").AddComponent<Light>();
            // a luz do dia não chega aqui (AmbienteDoDia apaga o ambiente no subsolo): a lâmpada ilumina a sala sozinha
            lampada.transform.SetParent(g, false); lampada.transform.localPosition = new Vector3(bx0 + 2.7f, teto - 0.45f, zMeio);
            lampada.type = LightType.Point; lampada.color = new Color(1f, 0.85f, 0.6f); lampada.intensity = 5f; lampada.range = 9f;
            var eco = new GameObject("Eco do bunker").AddComponent<AudioReverbZone>();
            eco.transform.SetParent(g, false); eco.transform.localPosition = new Vector3((bx0 + bx1) / 2f, fundo + 1.3f, zMeio);
            eco.reverbPreset = AudioReverbPreset.StoneCorridor; eco.minDistance = 3f; eco.maxDistance = 7f;
        }

        static float EscadaDoBunkerInicio(float zDiv) => zDiv + 0.7f;

        // ------------------------------------------------------------------ prédio de 4 andares

        static void Predio(Transform g, Vector3 centro, float yaw)
        {
            g.localPosition = centro; g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            const float W = 22f, D = 14f, H = 3.0f; const int andares = 4;
            float x0 = -W / 2f, x1 = W / 2f, z0 = -D / 2f, z1 = D / 2f;
            // núcleo: escada em U no fundo, no meio (3,2 m de largura)
            float ex0 = -1.6f, ex1 = 1.6f, ezFrente = z1 - 5.0f, ezFundo = z1 - 0.2f;
            var buraco = new Rect(ex0, ezFrente, ex1 - ex0, ezFundo - ezFrente);
            for (int a = 0; a <= andares; a++)
            {
                float y = 0.15f + a * H;
                bool cobertura = a == andares;
                Laje(g, cobertura ? "Laje da cobertura" : $"Piso {a}", x0 - 0.2f, x1 + 0.2f, z0 - 0.2f, z1 + 0.2f, y, a == 0 ? 0.3f : 0.25f, a == 0 ? "Concreto" : "Piso", (a > 0 && !cobertura) ? buraco : (Rect?)null);
                if (cobertura)
                {
                    foreach (var (a2, b2) in new[] { (new Vector2(x0, z0), new Vector2(x1, z0)), (new Vector2(x1, z0), new Vector2(x1, z1)), (new Vector2(x1, z1), new Vector2(x0, z1)), (new Vector2(x0, z1), new Vector2(x0, z0)) })
                        Parede(g, "Platibanda", a2, b2, y, 0.9f, "Tijolo");
                    break;
                }
                var andar = Grupo(g, a == 0 ? "Térreo" : $"Andar {a}");
                float hParede = H - 0.25f;
                // fachada com janelas em todos os andares; porta de entrada no térreo
                var frente = new List<Abertura>();
                for (float s = 1.5f; s < W - 1f; s += 3.5f)
                    if (!(a == 0 && Mathf.Abs(s - W / 2f) < 2f)) frente.Add(Abertura.Janela(s, 1.4f));
                if (a == 0) frente.Add(new Abertura(W / 2f - 1.0f, 2.0f, 0f, 2.4f));
                Parede(andar, "Fachada", new Vector2(x0, z0), new Vector2(x1, z0), y, hParede, "Tijolo", frente.ToArray());
                Parede(andar, "Fundos", new Vector2(x1, z1), new Vector2(x0, z1), y, hParede, "Tijolo", Abertura.Janela(3f, 1.4f), Abertura.Janela(7f, 1.4f), Abertura.Janela(15f, 1.4f), Abertura.Janela(19f, 1.4f));
                Parede(andar, "Lateral", new Vector2(x0, z1), new Vector2(x0, z0), y, hParede, "Tijolo", Abertura.Janela(4f, 1.4f), Abertura.Janela(10f, 1.4f));
                Parede(andar, "Lateral", new Vector2(x1, z0), new Vector2(x1, z1), y, hParede, "Tijolo", Abertura.Janela(4f, 1.4f), Abertura.Janela(10f, 1.4f));
                // corredor central (3,2 m) que vai da frente até a escada; um apartamento de cada lado
                foreach (float lado in new[] { -1f, 1f })
                {
                    float xp = 1.6f * lado;
                    Parede(andar, "Parede do apartamento", new Vector2(xp, z0 + 0.1f), new Vector2(xp, z1 - 0.1f), y, hParede, "Parede_Interna", Abertura.Porta(3.0f));
                    float xFim = lado < 0 ? x0 + 0.1f : x1 - 0.1f;
                    Parede(andar, "Quarto", new Vector2(xp, 0.5f), new Vector2(xFim, 0.5f), y, hParede, "Parede_Interna", Abertura.Porta(Mathf.Abs(xFim - xp) * 0.5f));
                    float xm = (xp + xFim) / 2f;
                    Caixa(andar, "Cama", new Vector3(xm + 1.2f * lado, y + 0.25f, z1 - 1.5f), new Vector3(1.6f, 0.5f, 2.0f), "Teto");
                    Caixa(andar, "Sofá", new Vector3(xm, y + 0.4f, z0 + 1.0f), new Vector3(2.2f, 0.8f, 0.9f), "Madeira");
                    Caixa(andar, "Mesa", new Vector3(xm - 1.5f * lado, y + 0.38f, -2.0f), new Vector3(1.2f, 0.76f, 0.8f), "Madeira");
                }
                // escada em U até o próximo andar: lance A sobe para o fundo (lado esquerdo), patamar, lance B volta (lado direito)
                if (a < andares - 1)
                {
                    int degraus = 9; float altura = H / 2f / degraus, piso = 0.28f;
                    Escada(andar, -0.85f, 1.4f, y, ezFrente, degraus, altura, piso, +1, "Concreto");
                    float zPatamar = ezFrente + degraus * piso;
                    Caixa(andar, "Patamar", new Vector3(0f, y + H / 2f - 0.1f, (zPatamar + ezFundo) / 2f), new Vector3(3.2f, 0.2f, ezFundo - zPatamar), "Concreto");
                    Escada(andar, 0.85f, 1.4f, y + H / 2f, zPatamar, degraus, altura, piso, -1, "Concreto");
                    Caixa(andar, "Mureta", new Vector3(0f, y + 1.1f, ezFrente + 1.6f), new Vector3(0.12f, 2.2f, 3.4f), "Parede_Interna");
                }
            }
            Caixa(g, "Marquise", new Vector3(0f, 2.75f, z0 - 0.8f), new Vector3(4f, 0.15f, 1.6f), "Concreto", 0f, false);
            Caixa(g, "Degraus da entrada", new Vector3(0f, 0.05f, z0 - 0.7f), new Vector3(3f, 0.1f, 1.2f), "Concreto");
            var eco = new GameObject("Eco do prédio").AddComponent<AudioReverbZone>();
            eco.transform.SetParent(g, false); eco.transform.localPosition = new Vector3(0f, 6f, 0f);
            eco.reverbPreset = AudioReverbPreset.Hallway; eco.minDistance = 6f; eco.maxDistance = 12f;
        }

        // ------------------------------------------------------------------ resto da cidade

        static void Praca(Transform g, Vector3 centro)
        {
            g.localPosition = centro;
            Caixa(g, "Piso da praça", new Vector3(0f, 0.04f, 0f), new Vector3(10f, 0.08f, 10f), "Calcada");
            Caixa(g, "Coreto", new Vector3(0f, 0.3f, 0f), new Vector3(4f, 0.6f, 4f), "Concreto");
            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f;
                Vector3 p = Quaternion.Euler(0f, ang, 0f) * new Vector3(0f, 0f, 4f);
                Caixa(g, "Banco", p + new Vector3(0f, 0.25f, 0f), new Vector3(2f, 0.5f, 0.5f), "Madeira", ang);
            }
            Arvore(g, new Vector3(-3.5f, 0f, 3.5f)); Arvore(g, new Vector3(3.5f, 0f, -3.5f));
        }

        static void Estacionamento(Transform g, Vector3 centro)
        {
            g.localPosition = centro;
            Caixa(g, "Pátio", new Vector3(0f, -0.04f, 0f), new Vector3(10f, 0.1f, 4.5f), "Asfalto");
            Carro(g, new Vector3(-3f, 0f, 0f), 90f, "Carro_Cinza");
            Carro(g, new Vector3(3.2f, 0f, 0.3f), 95f, "Carro_Azul");
        }

        static void Carro(Transform pai, Vector3 pos, float yaw, string cor)
        {
            var g = Grupo(pai, "Carro");
            g.localPosition = pos; g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Caixa(g, "Lataria", new Vector3(0f, 0.65f, 0f), new Vector3(1.8f, 0.7f, 4.2f), cor);
            Caixa(g, "Cabine", new Vector3(0f, 1.25f, -0.2f), new Vector3(1.6f, 0.55f, 2.2f), "Vidro");
            foreach (var (x, z) in new[] { (-0.8f, 1.3f), (0.8f, 1.3f), (-0.8f, -1.3f), (0.8f, -1.3f) })
                Caixa(g, "Pneu", new Vector3(x, 0.32f, z), new Vector3(0.3f, 0.64f, 0.64f), "Pneu", 0f, false);
        }

        static void Carros(Transform g)
        {
            Carro(g, new Vector3(2.2f, 0f, 18f), 8f, "Carro_Vinho");
            Carro(g, new Vector3(-1.5f, 0f, -24f), 175f, "Carro_Verde");
            Carro(g, new Vector3(-26f, 0f, 1.8f), 80f, "Carro_Azul");
            Carro(g, new Vector3(31f, 0f, -2.5f), 260f, "Carro_Cinza");
            Carro(g, new Vector3(38.5f, 0f, 22f), 20f, "Carro_Vinho");
            Carro(g, new Vector3(-41.5f, 0f, -18f), 190f, "Carro_Cinza");
            Carro(g, new Vector3(14f, 0f, 41.5f), 275f, "Carro_Verde");
            Carro(g, new Vector3(-12f, 0f, -38.5f), 95f, "Carro_Azul");
        }

        static void Cerca(Transform pai, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a; float n = d.magnitude; Vector2 u = d / n;
            float yaw = -Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg;
            for (float s = 0f; s <= n + 0.01f; s += 2f)
            {
                Vector2 p = a + u * Mathf.Min(s, n);
                Caixa(pai, "Mourão", new Vector3(p.x, 0.5f, p.y), new Vector3(0.1f, 1.0f, 0.1f), "Cerca", yaw);
            }
            Vector2 m = (a + b) / 2f;
            Caixa(pai, "Travessa", new Vector3(m.x, 0.75f, m.y), new Vector3(n, 0.08f, 0.04f), "Cerca", yaw);
            Caixa(pai, "Travessa", new Vector3(m.x, 0.35f, m.y), new Vector3(n, 0.08f, 0.04f), "Cerca", yaw);
        }

        static void Cercas(Transform g)
        {
            // quintal da casa da família (com portão aberto para a rua) e das casas do noroeste
            Cerca(g, new Vector2(-33f, -31f), new Vector2(-7f, -31f));
            Cerca(g, new Vector2(-33f, -31f), new Vector2(-33f, -7f));
            Cerca(g, new Vector2(-33f, -7f), new Vector2(-23f, -7f));
            Cerca(g, new Vector2(-17f, -7f), new Vector2(-7f, -7f));
            Cerca(g, new Vector2(-33f, 31f), new Vector2(-6f, 31f));
            Cerca(g, new Vector2(-19.5f, 12f), new Vector2(-19.5f, 31f));
            Cerca(g, new Vector2(7f, -31f), new Vector2(20f, -31f));
        }

        static void Arvore(Transform pai, Vector3 pos)
        {
            var g = Grupo(pai, "Árvore"); g.localPosition = pos;
            float yaw = (float)_rng.NextDouble() * 90f;
            Caixa(g, "Tronco", new Vector3(0f, 1.3f, 0f), new Vector3(0.3f, 2.6f, 0.3f), "Tronco", yaw);
            Caixa(g, "Copa", new Vector3(0f, 3.4f, 0f), new Vector3(2.6f, 2.2f, 2.6f), "Copa", yaw + 15f, false);
        }

        static void Arvores(Transform g)
        {
            foreach (var p in new[] { new Vector3(-34f, 0f, 10f), new Vector3(-8f, 0f, 9f), new Vector3(-32f, 0f, -33f), new Vector3(-9f, 0f, -33f),
                                      new Vector3(33f, 0f, 9f), new Vector3(7.5f, 0f, 9f), new Vector3(33f, 0f, -9f), new Vector3(8f, 0f, -33f), new Vector3(-24f, 0f, 33f) })
                Arvore(g, p);
        }

        static void Barricadas(Transform g)
        {
            foreach (var (pos, yaw) in new[] { (new Vector3(0f, 0f, 60f), 0f), (new Vector3(0f, 0f, -60f), 180f), (new Vector3(60f, 0f, 0f), 90f), (new Vector3(-60f, 0f, 0f), 270f) })
            {
                var b = Grupo(g, "Barricada"); b.localPosition = pos; b.localRotation = Quaternion.Euler(0f, yaw, 0f);
                for (int i = -2; i <= 2; i++) Caixa(b, "Barreira", new Vector3(i * 1.9f, 0.45f, 0f), new Vector3(1.8f, 0.9f, 0.5f), "Barreira", (i % 2) * 6f);
                Carro(b, new Vector3(-2.2f, 0f, 3.5f), 70f, "Carro_Vinho");
                Carro(b, new Vector3(2.8f, 0f, 4.2f), 115f, "Carro_Cinza");
            }
        }

        static void Pinheiros(Transform g)
        {
            int n = 0;
            for (int i = 0; i < 2200 && n < 320; i++)
            {
                float ang = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float r = 64f + (float)_rng.NextDouble() * 34f;
                var pos = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (Mathf.Abs(pos.x) > 100f || Mathf.Abs(pos.z) > 100f) continue;
                var go = new GameObject("Pinheiro");
                go.transform.SetParent(g, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * (0.9f + (float)_rng.NextDouble() * 0.7f);
                go.AddComponent<MeshFilter>().sharedMesh = _pinheiro;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { _m["Tronco"], _m["Pinheiro"] };
                n++;
            }
        }

        static void Barreira(Transform g)
        {
            const int lados = 48; const float r = 63f;
            for (int i = 0; i < lados; i++)
            {
                float ang = (i + 0.5f) * Mathf.PI * 2f / lados;
                var go = new GameObject("Parede invisível");
                go.transform.SetParent(g, false);
                go.transform.localPosition = new Vector3(Mathf.Cos(ang) * r, 3f, Mathf.Sin(ang) * r);
                go.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg + 90f, 0f);
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(2f * Mathf.PI * r / lados + 0.6f, 6f, 1f);
            }
        }

        static void PontosNaCasaDaFamilia(UnityEngine.SceneManagement.Scene cena)
        {
            var pontos = cena.GetRootGameObjects().FirstOrDefault(r => r.name == "Pontos de nascimento");
            if (pontos == null) return;
            var lugares = new[] { new Vector3(-22f, 0.05f, -4.6f), new Vector3(-18f, 0.05f, -4.6f), new Vector3(-24f, 0.05f, -4.2f), new Vector3(-16f, 0.05f, -4.2f) };
            for (int i = 0; i < pontos.transform.childCount && i < lugares.Length; i++)
            {
                var p = pontos.transform.GetChild(i);
                p.position = lugares[i];
                p.rotation = Quaternion.Euler(0f, 30f - i * 20f, 0f);
            }
        }
    }
}
