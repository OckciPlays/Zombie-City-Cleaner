using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A base das cenas de exemplo das construções feitas pelo jogo (a rua de exemplo e a sala de medidas): gerar e
    /// apagar, pôr uma casa no lote de frente para a rua, o caminho até a calçada, a rua com o chão de grama em volta (com
    /// os buracos dos porões), os postes, os carros e as árvores.
    ///
    /// Nada do que ela gera é salvo na cena (HideFlags.DontSave): ao abrir a cena, ao mudar algo no Inspector ou com R
    /// (no Play, pelo AndarNoExemplo), ela gera de novo. As subclasses dizem o que vai na rua (Montar).
    /// </summary>
    [ExecuteAlways]
    public abstract class CenaDeExemplo : MonoBehaviour
    {
        public int semente = 7;
        public Material material;
        public Mesh poste;
        public Mesh[] carros, arvores;

        const string NomeDaRaiz = "Gerado (não é salvo)";
        /// <summary>A frente das casas fica a 12,5 m do meio da rua; a calçada vai até 6,5 m.</summary>
        protected const float Frente = 12.5f, Calcada = 6.5f;

        /// <summary>O que foi gerado por último (para a tela do AndarNoExemplo e para as fotos).</summary>
        public string Resumo { get; private set; } = "";
        public readonly List<GameObject> Construcoes = new List<GameObject>();

        protected Transform raiz;
        /// <summary>O chão: a grama, a rua, as calçadas e os caminhos, num objeto só.</summary>
        protected Obra chao;
        protected readonly List<Rect> buracos = new List<Rect>();
        protected StringBuilder resumo;
        protected int triangulos;
        string _geradoCom;

        /// <summary>O que, mudando no Inspector, faz gerar de novo.</summary>
        protected virtual string Chave => semente.ToString();

        /// <summary>O que vai na rua: as casas (PorCasa) e a rua (Rua). Roda dentro do Gerar.</summary>
        protected abstract void Montar();

        /// <summary>O texto da tela do AndarNoExemplo, de onde o jogador está.</summary>
        public virtual string Texto(Vector3 posicao) => Resumo;

        void OnEnable()
        {
            if (transform.Find(NomeDaRaiz) == null) Gerar();
        }

        void OnDisable() => Apagar();

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled && _geradoCom != Chave) Gerar();
            };
        }
#endif

        protected static int Misturar(int a, int b, int c)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                return (int)(h & 0x7fffffff);
            }
        }

        protected GameObject Novo(string nome, Transform pai)
        {
            var go = new GameObject(nome) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(pai, false);
            return go;
        }

        protected GameObject Objeto(string nome, Transform pai, Mesh visual, Mesh colisao, Vector3 pos, float yaw)
        {
            var go = Novo(nome, pai);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = visual;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (colisao != null) go.AddComponent<MeshCollider>().sharedMesh = colisao;
            return go;
        }

        protected Mesh Malha(MalhaDeObra m, string nome, bool espelhar = false)
        {
            var malha = m.ParaMesh(nome, espelhar);
            malha.hideFlags = HideFlags.DontSave;
            return malha;
        }

        public void Apagar()
        {
            Construcoes.Clear();
            Transform velho;
            while ((velho = transform.Find(NomeDaRaiz)) != null)
            {
                foreach (var f in velho.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null && (f.sharedMesh.hideFlags & HideFlags.DontSave) != 0) DestroyImmediate(f.sharedMesh);
                foreach (var c in velho.GetComponentsInChildren<MeshCollider>(true))
                    if (c.sharedMesh != null && (c.sharedMesh.hideFlags & HideFlags.DontSave) != 0) DestroyImmediate(c.sharedMesh);
                DestroyImmediate(velho.gameObject);
            }
        }

        /// <summary>Gera tudo de novo, com a semente de agora.</summary>
        public void Gerar()
        {
            Apagar();
            _geradoCom = Chave;
            raiz = Novo(NomeDaRaiz, transform).transform;
            chao = new Obra(semente, Medidas.DoJogo);
            buracos.Clear();
            resumo = new StringBuilder();
            triangulos = 0;
            Montar();
            Resumo = resumo.ToString();
        }

        /// <summary>A largura do lote de uma casa já montada (com a chaminé, o ar e o fosso do porão).</summary>
        protected static float Largura(Casa casa) => casa.LimitesEmX.y - casa.LimitesEmX.x;

        /// <summary>
        /// Põe a casa (já montada) de frente para a rua, com o lote começando em x (lado 1: o lado norte, a frente em -z;
        /// lado -1: o sul, girada 180°), abre o buraco do porão no chão e faz o caminho até a calçada. Devolve onde o
        /// lote termina.
        /// </summary>
        protected float PorCasa(Casa casa, string nome, float x, int lado, out GameObject go)
        {
            var planta = casa.p;
            Vector2 lim = casa.LimitesEmX;
            float sx = lado > 0 ? 1f : -1f;
            float cx = lado > 0 ? x - lim.x + 1.5f : x + lim.y + 1.5f;
            float cz = lado * (Frente + planta.D / 2);
            Vector2 Mundo(Vector2 q) => new Vector2(cx + sx * q.x, cz + sx * q.y);
            go = Objeto(nome, raiz, Malha(casa.vis, nome, planta.espelhar), Malha(casa.col, nome + " (colisão)", planta.espelhar),
                        new Vector3(cx, 0f, cz), lado > 0 ? 0f : 180f);
            Construcoes.Add(go);
            foreach (var b in casa.BuracosNoChao)
            {
                Vector2 a = Mundo(b.min), c = Mundo(b.max);
                buracos.Add(Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y)));
            }
            Caminho(Mundo(casa.Entrada), lado * Calcada);
            // a entrada de carro até a rua e o carro na garagem (ou na vaga), em 70% das casas
            if (casa.EntradaDoCarro.HasValue) Caminho(Mundo(casa.EntradaDoCarro.Value), lado * Calcada, 3.0f, -0.135f);
            var rnd = new System.Random(Misturar(semente, Mathf.RoundToInt(cx * 10f), lado));
            string lugar = planta.garagem == null ? "" : planta.garagem.coberta
                ? $", garagem {(planta.garagem.fechada ? "fechada" : "aberta")}{(planta.garagem.portaParaDentro ? " com porta para dentro" : "")}"
                : ", vaga ao lado";
            if (carros != null && carros.Length > 0)
                foreach (var (pos, yaw) in casa.Carros)
                {
                    if (rnd.NextDouble() > 0.7) { lugar += " (sem carro)"; continue; }
                    Vector2 c = Mundo(pos);
                    Objeto("Carro da casa", raiz, carros[rnd.Next(carros.Length)], null, new Vector3(c.x, 0.05f, c.y), (lado > 0 ? 0f : 180f) + yaw);
                }
            int t = casa.vis.Triangulos;
            triangulos += t;
            resumo.AppendLine($"{nome}: {planta.W:0.0} × {planta.D:0.0} m{lugar}, {t:N0} triângulos (colisão {casa.col.Triangulos:N0})");
            return x + (lim.y - lim.x) + 3f;
        }

        /// <summary>Um ponto da planta da casa (x, y, z nas coordenadas da Planta) no mundo, com o espelho e o giro dela.</summary>
        protected static Vector3 NaCasa(GameObject casa, Planta p, float x, float y, float z) =>
            casa.transform.TransformPoint(new Vector3(p.espelhar ? -x : x, y, z));

        /// <summary>O caminho de concreto do pé da escada da casa (ou da garagem) até a calçada.</summary>
        protected void Caminho(Vector2 de, float zCalcada, float largura = 1.2f, float meioY = -0.12f)
        {
            float comp = Mathf.Abs(de.y - zCalcada);
            if (comp < 0.1f) return;
            chao.Caixa(new Vector3(de.x, meioY, (de.y + zCalcada) / 2), new Vector3(largura, 0.3f, comp), "Calcada", colide: true);
        }

        /// <summary>
        /// A rua de xa a xb (em x, no meio z = 0): asfalto, faixa amarela, meio-fio e calçada com as juntas, o chão de
        /// grama em volta (com os buracos), os postes, uns carros parados e as árvores atrás dos lotes. Fecha o objeto do
        /// chão; chame depois de pôr as casas.
        /// </summary>
        protected void Rua(float xa, float xb, int quantosCarros = 4)
        {
            var rnd = new System.Random(semente);
            buracos.Add(Rect.MinMaxRect(xa - 30f, -Calcada, xb + 30f, Calcada));
            chao.Laje(xa - 30f, xb + 30f, -70f, 70f, 0f, 0.3f, "Grama", true, buracos);
            float meio = (xa + xb) / 2, comp = xb - xa + 60f;
            chao.Caixa(new Vector3(meio, -0.15f, 0f), new Vector3(comp, 0.3f, 8f), "Asfalto", colide: true);
            for (float x = xa; x < xb; x += 6f) chao.Caixa(new Vector3(x, 0.003f, 0f), new Vector3(3f, 0.006f, 0.15f), "Faixa");
            foreach (int lado in new[] { 1, -1 })
            {
                chao.Caixa(new Vector3(meio, 0f, lado * 4.075f), new Vector3(comp, 0.3f, 0.15f), "MeioFio", colide: true);
                chao.Caixa(new Vector3(meio, -0.09f, lado * 5.325f), new Vector3(comp, 0.3f, 2.35f), "Calcada", colide: true);
                for (float x = xa - 30f; x < xb + 30f; x += 2f) chao.Caixa(new Vector3(x, 0.064f, lado * 5.325f), new Vector3(0.02f, 0.12f, 2.35f), "Concreto");
            }
            Objeto("Chão (rua, calçadas e grama)", raiz, Malha(chao.vis, "Chão"), Malha(chao.col, "Chão (colisão)"), Vector3.zero, 0f);

            if (poste != null)
                for (float x = xa + 8f, k = 0; x < xb; x += 22f, k++)
                {
                    int lado = ((int)k % 2 == 0) ? 1 : -1;
                    Objeto("Poste", raiz, poste, null, new Vector3(x, 0.12f, lado * 5.9f), lado > 0 ? 180f : 0f);
                }
            if (carros != null && carros.Length > 0)
                for (int i = 0; i < quantosCarros; i++)
                {
                    int lado = i % 2 == 0 ? 1 : -1;
                    float x = Mathf.Lerp(xa + 20f, xb - 10f, (float)rnd.NextDouble());
                    Objeto("Carro parado", raiz, carros[rnd.Next(carros.Length)], null, new Vector3(x, 0f, lado * 2.9f), lado > 0 ? 90f : 270f);
                }
            if (arvores != null && arvores.Length > 0)
                for (int i = 0; i < 40; i++)
                {
                    int lado = i % 2 == 0 ? 1 : -1;
                    float x = Mathf.Lerp(xa - 20f, xb + 20f, (float)rnd.NextDouble());
                    float z = lado * Mathf.Lerp(36f, 62f, (float)rnd.NextDouble());
                    var go = Objeto("Árvore", raiz, arvores[rnd.Next(arvores.Length)], null, new Vector3(x, 0f, z), (float)rnd.NextDouble() * 360f);
                    go.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.5f, (float)rnd.NextDouble());
                }
        }
    }
}
