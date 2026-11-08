using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A medida dos troncos (07/11/2026, para os entalhes do machado: <see cref="FlorestaDoMundo.MarcarEntalhe"/>). Os troncos
    /// da floresta são de blocos (quadrados, com raízes largas embaixo) e bem mais finos que a colisão; as malhas não são
    /// legíveis no jogo. Aqui, no editor, cada árvore (a malha mais detalhada da espécie, na escala 1) é medida por raios de
    /// fora para o eixo: o raio da superfície do tronco em <see cref="Alturas"/> alturas e <see cref="Angulos"/> ângulos (o
    /// galho de fora, longe do tronco, não conta). Vai para <c>Resources/TroncosDasArvores.bytes</c>, que o jogo lê.
    /// Menu Zombie City Cleaner > Mundo > Medir os troncos (os entalhes do machado), com a cena do mundo aberta.
    /// </summary>
    public static class MedidaDosTroncos
    {
        public const int Alturas = 7, Angulos = 48;
        public const float AlturaMinima = 0.3f, Passo = 0.2f;
        const string Arquivo = "Assets/_Game/Resources/TroncosDasArvores.bytes";

        [MenuItem("Zombie City Cleaner/Mundo/Medir os troncos (os entalhes do machado)")]
        public static void Medir()
        {
            var f = Object.FindAnyObjectByType<FlorestaDoMundo>(FindObjectsInactive.Include);
            if (f == null || f.malhas == null)
            {
                Debug.LogWarning("Troncos: abra a cena do mundo (a FlorestaDoMundo tem as malhas das árvores).");
                return;
            }
            int especies = (int)Especie.MortaB + 1;
            var dados = new float[especies * Alturas * Angulos];
            var linhas = new List<string>();
            for (int e = 0; e < especies; e++)
            {
                int k = e * 3;
                var malha = k < f.malhas.Length ? f.malhas[k] : null;
                float colisao = Floresta.RaioDoTronco[e];
                if (malha == null || colisao <= 0f) continue;
                var tris = Triangulos(malha);
                float limite = colisao * 1.5f + 0.05f;
                float soma = 0f;
                int n = 0;
                for (int h = 0; h < Alturas; h++)
                {
                    float y = AlturaMinima + h * Passo;
                    var faixa = new float[Angulos];
                    var validos = new List<float>();
                    for (int a = 0; a < Angulos; a++)
                    {
                        float ang = a * 2f * Mathf.PI / Angulos;
                        var dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                        faixa[a] = Superficie(tris, dir * 3f + Vector3.up * y, -dir, limite);
                        if (faixa[a] > 0f) validos.Add(faixa[a]);
                    }
                    // sem tronco nalgum ângulo (o raio passou entre as raízes): a mediana da altura; sem nada, uma estimativa
                    validos.Sort();
                    float mediana = validos.Count > 0 ? validos[validos.Count / 2] : colisao * 0.75f;
                    for (int a = 0; a < Angulos; a++)
                    {
                        float r = faixa[a] > 0f ? faixa[a] : mediana;
                        dados[(e * Alturas + h) * Angulos + a] = r;
                        soma += r;
                        n++;
                    }
                }
                linhas.Add($"{(Especie)e}: {soma / Mathf.Max(1, n):0.00} m em média (a colisão: {colisao:0.00})");
            }
            using (var w = new BinaryWriter(File.Create(Arquivo)))
            {
                w.Write(especies);
                w.Write(Alturas);
                w.Write(Angulos);
                w.Write(AlturaMinima);
                w.Write(Passo);
                foreach (var v in dados) w.Write(v);
            }
            AssetDatabase.ImportAsset(Arquivo);
            Debug.Log("Troncos medidos (" + Arquivo + "):\n" + string.Join("\n", linhas));
        }

        // os triângulos da malha (no editor, as malhas não legíveis também se leem pelos dados)
        static List<(Vector3 a, Vector3 b, Vector3 c)> Triangulos(Mesh malha)
        {
            var lista = new List<(Vector3, Vector3, Vector3)>();
            using (var dados = Mesh.AcquireReadOnlyMeshData(malha))
            {
                var d = dados[0];
                var vs = new NativeArray<Vector3>(d.vertexCount, Allocator.Temp);
                d.GetVertices(vs);
                for (int s = 0; s < d.subMeshCount; s++)
                {
                    var sub = d.GetSubMesh(s);
                    var idx = new NativeArray<int>(sub.indexCount, Allocator.Temp);
                    d.GetIndices(idx, s);
                    for (int i = 0; i + 2 < idx.Length; i += 3) lista.Add((vs[idx[i]], vs[idx[i + 1]], vs[idx[i + 2]]));
                    idx.Dispose();
                }
                vs.Dispose();
            }
            return lista;
        }

        // a superfície mais de fora que o raio encontra, até 'limite' do eixo (os galhos de fora não contam); -1 sem nada
        static float Superficie(List<(Vector3 a, Vector3 b, Vector3 c)> tris, Vector3 de, Vector3 dir, float limite)
        {
            float melhor = -1f;
            foreach (var (a, b, c) in tris)
            {
                if (!Cruza(de, dir, a, b, c, out float t)) continue;
                float r = 3f - t;
                if (r <= limite && r > melhor) melhor = r;
            }
            return melhor;
        }

        // Möller–Trumbore, dos dois lados
        static bool Cruza(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0f;
            Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-8f) return false;
            float inv = 1f / det;
            Vector3 s = o - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(d, q) * inv;
            if (v < 0f || u + v > 1f) return false;
            t = Vector3.Dot(e2, q) * inv;
            return t > 0f;
        }
    }
}
