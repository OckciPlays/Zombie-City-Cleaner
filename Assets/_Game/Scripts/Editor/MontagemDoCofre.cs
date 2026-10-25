using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O cofre do motor em 3D (o que se vê olhando dentro do capô, pela <see cref="Player.TelaDoCapo"/>), parte da
    /// montagem dos veículos:
    /// - <see cref="MontarCofre"/>: o motor e a frente do <c>Cofre.fbx</c> (feito pelo <c>Blender/cofre.py</c>), as 3
    ///   peças nos lugares delas (as malhas <c>Peca_*</c> da montagem das armas), a peça apagada de cada lugar, o gatilho
    ///   que o mouse acha, de onde se olha e a luz de quem olha (<see cref="CofreDoMotor"/>).
    /// - <see cref="AbrirCarroDaCidade"/>: os carros da cidade (do Cidade_Rua.fbx) não têm o capô à parte. O capô deles é
    ///   um quadrado só, de lado a lado, em cima da frente: ele é recortado da malha (<see cref="Recortar"/>), e o
    ///   "Capô aberto" guarda a carroceria sem ele, o capô à parte (a origem na dobradiça) e o cofre por dentro (o fundo,
    ///   as paredes e as caixas das rodas, feitos aqui, com a cor do carro).
    /// </summary>
    public static partial class MontagemDosVeiculos
    {
        const string FbxCofre = "Assets/_Game/Art/Veiculos/Cofre.fbx";
        const string PastaDasPecas = "Assets/_Game/Art/Characters/Jogador/Malhas";

        // o cofre do carro da família: o meio do fundo (o Y_COFRE do carro.py, mais o fundo) e o comprimento (do Z_CAPO
        // até a parede da frente, em 2,03)
        const float FrenteDoCofre = 2.03f;
        static readonly Vector3 FundoDoCofre = new Vector3(0f, 0.491f, (ZCapo + FrenteDoCofre) / 2f);
        // os lugares das peças, os do cofre.py (LUGARES): a vela no 2º poço, a bateria na bandeja e o radiador de pé,
        // contado da parede da frente
        static readonly Vector3 LugarDaVela = new Vector3(-0.09f, 0.265f, -0.08f);
        static readonly Vector3 LugarDaBateria = new Vector3(0.40f, 0.02f, 0.10f);
        static readonly Vector3 LugarDoRadiador = new Vector3(0f, 0.175f, -0.12f);
        static readonly string[] NomesDasPecas = { "Vela", "Bateria", "Radiador" };

        // as cores de dentro do cofre (a Paleta.png: linha, coluna), as do carro.py
        static Vector2 CorDaPaleta(int linha, int coluna) => new Vector2((coluna + 0.5f) / 16f, 1f - (linha + 0.5f) / 16f);
        static readonly Vector2 CorDoFundo = CorDaPaleta(10, 14);     // "grade"
        static readonly Vector2 CorDeDentro = CorDaPaleta(10, 0);     // "interior"

        static Material Fantasma()
        {
            // a peça apagada no lugar vazio: Unlit, transparente, sem sombra (a cor e o pulso vêm do CofreDoMotor)
            var f = Material("Fantasma", "Universal Render Pipeline/Unlit");
            f.shaderKeywords = new string[0];
            f.SetFloat("_Surface", 1f);
            f.SetFloat("_Blend", 0f);
            f.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            f.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            f.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            f.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            f.SetFloat("_ZWrite", 0f);
            f.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            f.SetOverrideTag("RenderType", "Transparent");
            f.renderQueue = (int)RenderQueue.Transparent;
            f.SetColor("_BaseColor", new Color(1f, 0.86f, 0.42f, 0.3f));
            f.SetShaderPassEnabled("ShadowCaster", false);
            f.SetShaderPassEnabled("DepthOnly", false);
            f.SetShaderPassEnabled("DepthNormals", false);
            EditorUtility.SetDirty(f);
            return f;
        }

        static void ImportarCofre()
        {
            var imp = AssetImporter.GetAtPath(FbxCofre) as ModelImporter;
            if (imp == null) throw new FileNotFoundException("Cofre.fbx não encontrado (rode o Blender/cofre.py).", FbxCofre);
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.isReadable = false;
            imp.generateSecondaryUV = false;
            imp.bakeAxisConversion = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.SaveAndReimport();
        }

        static Mesh MalhaDoCofre(string nome)
        {
            var m = AssetDatabase.LoadAllAssetsAtPath(FbxCofre).OfType<Mesh>().FirstOrDefault(x => x.name == nome);
            if (m == null) throw new System.Exception($"A peça {nome} não está no {FbxCofre}.");
            return m;
        }

        static MeshRenderer Malha(Transform pai, string nome, Mesh malha, Material mat, Vector3 pos, Quaternion rot, int camada)
        {
            var go = new GameObject(nome) { layer = camada };
            go.transform.SetParent(pai, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return r;
        }

        /// <summary>
        /// O cofre em 3D, com a origem no meio do fundo (<paramref name="fundo"/>, no espaço do pai), o comprimento (da
        /// parede de trás à da frente) e a altura do capô na frente (acima do fundo), que diz de onde se olha.
        /// </summary>
        static CofreDoMotor MontarCofre(Transform pai, Vector3 fundo, float comprimento, float alturaNaFrente, Mats m, MonoBehaviour dono, int camada)
        {
            var raiz = new GameObject("Cofre do motor") { layer = camada };
            raiz.transform.SetParent(pai, false);
            raiz.transform.localPosition = fundo;
            var cofre = raiz.AddComponent<CofreDoMotor>();
            cofre.dono = dono;
            Malha(raiz.transform, "Motor", MalhaDoCofre("Cofre_Motor"), m.cidade, Vector3.zero, Quaternion.identity, camada);
            var frente = Malha(raiz.transform, "Frente", MalhaDoCofre("Cofre_Frente"), m.cidade, new Vector3(0f, 0f, comprimento / 2f), Quaternion.identity, camada).transform;

            // as 3 peças: a malha do item, a apagada (escondida) e o gatilho do mouse (Ignore Raycast: o tiro passa)
            cofre.pecas = new MeshRenderer[3];
            cofre.fantasmas = new MeshRenderer[3];
            cofre.lugares = new Collider[3];
            var pos = new[] { LugarDaVela, LugarDaBateria, frente.localPosition + LugarDoRadiador };
            // o radiador fica de pé, as aletas para a frente (a malha dele está deitada)
            var rot = new[] { Quaternion.identity, Quaternion.identity, Quaternion.Euler(90f, 0f, 0f) };
            for (int k = 0; k < 3; k++)
            {
                var malha = AssetDatabase.LoadAssetAtPath<Mesh>($"{PastaDasPecas}/Peca_{NomesDasPecas[k]}.asset");
                if (malha == null) throw new FileNotFoundException("Rode a montagem das armas antes (as malhas das peças).", $"Peca_{NomesDasPecas[k]}");
                var peca = Malha(raiz.transform, NomesDasPecas[k], malha, m.cidade, pos[k], rot[k], camada);
                cofre.pecas[k] = peca;
                var f = Malha(peca.transform, "Apagada", malha, m.fantasma, Vector3.zero, Quaternion.identity, camada);
                f.shadowCastingMode = ShadowCastingMode.Off;
                f.receiveShadows = false;
                f.enabled = false;
                cofre.fantasmas[k] = f;
                var g = new GameObject("Gatilho") { layer = 2 };
                g.transform.SetParent(peca.transform, false);
                var bc = g.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.center = malha.bounds.center;
                bc.size = Vector3.Max(malha.bounds.size + Vector3.one * 0.03f, Vector3.one * 0.1f);
                cofre.lugares[k] = bc;
            }

            // de onde se olha: em cima da frente, a cabeça embaixo do capô aberto, olhando o motor de cima
            var olhar = new GameObject("Olhar").transform;
            olhar.SetParent(raiz.transform, false);
            var de = new Vector3(0f, alturaNaFrente + 0.46f, comprimento / 2f + 0.24f);
            olhar.SetLocalPositionAndRotation(de, Quaternion.LookRotation(new Vector3(0f, 0.10f, -0.06f) - de));
            cofre.olhar = olhar;
            // a luz de quem olha (a lanterna do celular): perto da cabeça, sem sombra, só na máquina de quem olha
            var luz = new GameObject("Luz de quem olha").AddComponent<Light>();
            luz.transform.SetParent(raiz.transform, false);
            luz.transform.localPosition = de + new Vector3(0f, 0.06f, -0.05f);
            luz.type = LightType.Point;
            luz.range = 2.2f;
            luz.intensity = 0.25f;   // mais forte, estoura o cofre numa cor só
            luz.color = new Color(1f, 0.94f, 0.84f);
            luz.shadows = LightShadows.None;
            luz.enabled = false;
            cofre.luz = luz;
            return cofre;
        }

        /// <summary>O cofre do carro da família, dentro da carroceria (o fundo e as paredes são do carro.py).</summary>
        static CofreDoMotor CofreDaFamilia(Transform raiz, Carro carro, Mats m) =>
            MontarCofre(raiz, FundoDoCofre, FrenteDoCofre - ZCapo, AlturaDoCapo(FrenteDoCofre) - FundoDoCofre.y, m, carro, Carro.CamadaVeiculo);

        // ------------------------------------------------------------------------------------------ os carros da cidade

        class Recorte
        {
            public Mesh semCapo, tampa, caixa;
            public Vector3 dobradica;
            public float zTras, zFrente, yFrente, piso;
        }

        static readonly Dictionary<Mesh, Recorte> _recortes = new Dictionary<Mesh, Recorte>();

        /// <summary>
        /// O "Capô aberto" de um carro da cidade (escondido; o <see cref="CapoDaCidade"/> mostra no lugar do carro):
        /// a carroceria sem o capô, o capô (a origem na dobradiça) e o cofre por dentro. False se a malha não tem capô.
        /// </summary>
        static bool AbrirCarroDaCidade(MeshFilter carro, CapoDaCidade capo, Mats m)
        {
            if (!_recortes.TryGetValue(carro.sharedMesh, out var r)) _recortes[carro.sharedMesh] = r = Recortar(carro.sharedMesh);
            if (r == null) return false;
            var mr = carro.GetComponent<MeshRenderer>();
            var aberto = new GameObject("Capô aberto");
            aberto.transform.SetParent(carro.transform, false);
            var corpo = Malha(aberto.transform, "Carroceria", r.semCapo, m.cidade, Vector3.zero, Quaternion.identity, 0);
            corpo.shadowCastingMode = mr.shadowCastingMode;
            var tampa = Malha(aberto.transform, "Tampa do capô", r.tampa, m.cidade, r.dobradica, Quaternion.identity, 0);
            Malha(aberto.transform, "Paredes do cofre", r.caixa, m.cidade, Vector3.zero, Quaternion.identity, 0);
            var fundo = new Vector3(0f, r.piso, (r.zTras + r.zFrente) / 2f);
            capo.cofre = MontarCofre(aberto.transform, fundo, r.zFrente - r.zTras, r.yFrente - r.piso, m, capo, 0);
            capo.carroFechado = mr;
            capo.aberto = aberto;
            capo.tampa = tampa.transform;
            aberto.SetActive(false);
            return true;
        }

        /// <summary>
        /// Recorta o capô da malha: os triângulos grandes e planos virados para cima na frente (o capô deles é um quadrado só, de
        /// lado a lado). Grava em Art/Veiculos/Malhas a carroceria sem ele, ele à parte (com a origem na dobradiça, atrás,
        /// e as duas faces) e as paredes do cofre (o fundo 40 cm abaixo do capô, as paredes dos lados com a cor do carro,
        /// a de trás, a da frente e as caixas das rodas da frente).
        /// </summary>
        static Recorte Recortar(Mesh malha)
        {
            var v = malha.vertices;
            var uv = malha.uv;
            var t = malha.triangles;
            var capo = new List<int>();
            var resto = new List<int>();
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                var cruz = Vector3.Cross(b - a, c - a);
                var centro = (a + b + c) / 3f;
                bool doCapo = cruz.normalized.y > 0.97f && centro.z > 0.3f && cruz.magnitude / 2f > 0.1f;
                (doCapo ? capo : resto).AddRange(new[] { t[i], t[i + 1], t[i + 2] });
            }
            if (capo.Count == 0)
            {
                Debug.LogWarning($"Não achei o capô na malha {malha.name}.");
                return null;
            }
            float x0 = float.MaxValue, x1 = float.MinValue, zT = float.MaxValue, zF = float.MinValue;
            foreach (int i in capo)
            {
                x0 = Mathf.Min(x0, v[i].x);
                x1 = Mathf.Max(x1, v[i].x);
                zT = Mathf.Min(zT, v[i].z);
                zF = Mathf.Max(zF, v[i].z);
            }
            float yT = float.MinValue, yF = float.MinValue;
            foreach (int i in capo)
            {
                if (Mathf.Abs(v[i].z - zT) < 0.01f) yT = Mathf.Max(yT, v[i].y);
                if (Mathf.Abs(v[i].z - zF) < 0.01f) yF = Mathf.Max(yF, v[i].y);
            }
            var corDoCarro = uv[capo[0]];
            var r = new Recorte { zTras = zT, zFrente = zF, yFrente = yF, piso = yF - 0.40f, dobradica = new Vector3((x0 + x1) / 2f, yT, zT) };

            // a carroceria sem o capô (uma cópia: os outros canais, como o UV do lightmap, vão junto)
            var sem = Object.Instantiate(malha);
            sem.name = malha.name + "_SemCapo";
            sem.SetTriangles(resto, 0);
            r.semCapo = PainelDoCarro.Salvar(sem, $"{PastaMalhas}/{sem.name}.asset");

            // o capô: as faces de cima e as de baixo (as mesmas, ao contrário), em volta da dobradiça
            var mt = new Construtor();
            for (int i = 0; i < capo.Count; i += 3)
            {
                Vector3 a = v[capo[i]] - r.dobradica, b = v[capo[i + 1]] - r.dobradica, c = v[capo[i + 2]] - r.dobradica;
                mt.Triangulo(a, b, c, corDoCarro);
                mt.Triangulo(a - Vector3.up * 0.012f, c - Vector3.up * 0.012f, b - Vector3.up * 0.012f, CorDeDentro);
            }
            r.tampa = PainelDoCarro.Salvar(mt.Malha(malha.name + "_Capo"), $"{PastaMalhas}/{malha.name}_Capo.asset");

            // as paredes do cofre, viradas para dentro
            const float e = 0.008f;
            float piso = r.piso, xa = x0 + e, xb = x1 - e, za = zT + e, zb = zF - e;
            var cx = new Construtor();
            cx.Quad(new Vector3(xa, piso, za), new Vector3(xb, piso, za), new Vector3(xb, piso, zb), new Vector3(xa, piso, zb), Vector3.up, CorDoFundo);
            cx.Quad(new Vector3(xa, piso, za), new Vector3(xa, yT, za), new Vector3(xa, yF, zb), new Vector3(xa, piso, zb), Vector3.right, corDoCarro);
            cx.Quad(new Vector3(xb, piso, za), new Vector3(xb, yT, za), new Vector3(xb, yF, zb), new Vector3(xb, piso, zb), Vector3.left, corDoCarro);
            cx.Quad(new Vector3(xa, piso, za), new Vector3(xb, piso, za), new Vector3(xb, yT, za), new Vector3(xa, yT, za), Vector3.forward, CorDeDentro);
            cx.Quad(new Vector3(xa, piso, zb), new Vector3(xb, piso, zb), new Vector3(xb, yF, zb), new Vector3(xa, yF, zb), Vector3.back, CorDeDentro);
            // as caixas das rodas da frente: o arco da roda (as faces inclinadas, viradas para cima, perto do chão) diz
            // onde e até que altura
            float r0 = float.MaxValue, r1 = float.MinValue, topo = float.MinValue;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                var n = Vector3.Cross(b - a, c - a).normalized;
                var centro = (a + b + c) / 3f;
                if (n.y < 0.5f || n.y > 0.97f || centro.z < 0.3f || centro.y > 0.8f || Mathf.Abs(centro.x) < 0.4f) continue;
                r0 = Mathf.Min(r0, Mathf.Min(a.z, Mathf.Min(b.z, c.z)));
                r1 = Mathf.Max(r1, Mathf.Max(a.z, Mathf.Max(b.z, c.z)));
                topo = Mathf.Max(topo, Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
            }
            if (r1 > r0 && topo > piso)
            {
                r0 = Mathf.Max(r0 - 0.03f, za);
                r1 = Mathf.Min(r1 + 0.03f, zb);
                float y = topo + 0.03f, largura = 0.27f;
                foreach (float s in new[] { -1f, 1f })
                {
                    float fora = s < 0 ? xa : xb, dentro = fora - s * largura;
                    float xm = Mathf.Min(fora, dentro), xM = Mathf.Max(fora, dentro);
                    cx.Quad(new Vector3(xm, y, r0), new Vector3(xM, y, r0), new Vector3(xM, y, r1), new Vector3(xm, y, r1), Vector3.up, CorDeDentro);
                    cx.Quad(new Vector3(dentro, piso, r0), new Vector3(dentro, y, r0), new Vector3(dentro, y, r1), new Vector3(dentro, piso, r1), Vector3.right * -s, CorDeDentro);
                    cx.Quad(new Vector3(xm, piso, r0), new Vector3(xM, piso, r0), new Vector3(xM, y, r0), new Vector3(xm, y, r0), Vector3.back, CorDeDentro);
                    cx.Quad(new Vector3(xm, piso, r1), new Vector3(xM, piso, r1), new Vector3(xM, y, r1), new Vector3(xm, y, r1), Vector3.forward, CorDeDentro);
                }
            }
            r.caixa = PainelDoCarro.Salvar(cx.Malha(malha.name + "_Cofre"), $"{PastaMalhas}/{malha.name}_Cofre.asset");
            return r;
        }

        /// <summary>Uma malha de faces planas, cada uma com uma cor da paleta (o UV), virada para o lado pedido.</summary>
        class Construtor
        {
            readonly List<Vector3> _v = new List<Vector3>();
            readonly List<Vector2> _uv = new List<Vector2>();
            readonly List<int> _t = new List<int>();

            public void Triangulo(Vector3 a, Vector3 b, Vector3 c, Vector2 cor)
            {
                int i = _v.Count;
                _v.AddRange(new[] { a, b, c });
                _uv.AddRange(new[] { cor, cor, cor });
                _t.AddRange(new[] { i, i + 1, i + 2 });
            }

            // a frente de um triângulo na Unity é a do Cross(b - a, c - a)
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 cor)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f)
                {
                    (b, d) = (d, b);
                }
                Triangulo(a, b, c, cor);
                Triangulo(a, c, d, cor);
            }

            public Mesh Malha(string nome)
            {
                var m = new Mesh { name = nome };
                m.SetVertices(_v);
                m.SetUVs(0, _uv);
                m.SetTriangles(_t, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            }
        }
    }
}
