using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// A construção das bases militares (03/11/2026), em cada máquina, até <see cref="BasesMilitaresDoMundo.raioDaConstrucao"/>
    /// m do jogador local, pela <see cref="PlantaDaBase"/>: os modelos da beira da estrada (<see cref="ModelosDaEstrada"/>:
    /// o contêiner, os sacos de areia, o alambrado, o caminhão do exército e o blindado), as barreiras de concreto do
    /// Blender e as peças feitas aqui (as barracas, a guarita, as caixas de munição, o mastro, os postes e a cancela). Uma
    /// malha só, as caixas de colisão em filhos e, pronta, a área do NavMesh em volta refeita.
    /// </summary>
    public partial class BasesMilitaresDoMundo
    {
        sealed class Construido
        {
            public GameObject Raiz;
            public Bounds Caixa;
            public readonly List<Mesh> Malhas = new List<Mesh>();
        }

        readonly Dictionary<int, Construido> _construidos = new Dictionary<int, Construido>();
        static readonly Dictionary<int, ModelosDaEstrada.Modelo> _modelos = new Dictionary<int, ModelosDaEstrada.Modelo>();
        float _proximaRevisao;

        /// <summary>As bases montadas nesta máquina (para os testes).</summary>
        public int Construidas => _construidos.Count;

        void AtualizarConstrucoes()
        {
            if (Time.unscaledTime < _proximaRevisao) return;
            _proximaRevisao = Time.unscaledTime + 0.5f;
            Vector3 centro;
            var local = JogadorRede.Local;
            if (local != null) centro = local.transform.position;
            else if (Camera.main != null) centro = Camera.main.transform.position;
            else return;
            var c2 = new Vector2(centro.x, centro.z);
            for (int b = 0; b < _bases.Count; b++)
            {
                float dist = Vector2.Distance(c2, _bases[b].Centro);
                bool montada = _construidos.TryGetValue(b, out var c);
                if (!montada && dist < raioDaConstrucao)
                {
                    double inicio = Time.realtimeSinceStartupAsDouble;
                    var p = Planta(b);
                    _construidos[b] = Montar(p);
                    Debug.Log($"[Bases] A {p.Lugar.Nome} foi montada em {(Time.realtimeSinceStartupAsDouble - inicio) * 1000.0:0} ms.");
                    return;   // uma por vez
                }
                if (montada && dist > raioDaConstrucao + 60f)
                {
                    Apagar(c);
                    _construidos.Remove(b);
                }
            }
        }

        void ApagarConstrucoes()
        {
            foreach (var c in _construidos.Values) Apagar(c);
            _construidos.Clear();
        }

        static void Apagar(Construido c)
        {
            if (c.Raiz != null)
            {
                Destroy(c.Raiz);
                NavMeshDoMundo.Mudou(c.Caixa);
            }
            foreach (var m in c.Malhas) if (m != null) Destroy(m);
            c.Malhas.Clear();
        }

        // ------------------------------------------------------------------ montar

        Construido Montar(PlantaDaBase p)
        {
            var c = new Construido();
            var raiz = new GameObject(p.Lugar.Nome ?? "Base militar");
            raiz.transform.SetPositionAndRotation(p.Meio, Quaternion.Euler(0f, p.GiroNoMundo(0f), 0f));
            c.Raiz = raiz;
            float H = PlantaDaBase.Meia;
            c.Caixa = new Bounds(p.Meio + Vector3.up * 5f, new Vector3(2f * H + 30f, 40f, 2f * H + 30f));
            var objetos = ObjetosDoMundo.Instancia;
            var terreno = TerrenoDoMundo.Instancia;
            Material daCidade = objetos != null && objetos.materialDaCidade != null ? objetos.materialDaCidade : terreno != null ? terreno.materialDaCidade : null;

            var desenho = MalhaDeObra.Emprestar(TamanhoDaMalha.Grande);
            var colisoes = new GameObject("Colisões").transform;
            colisoes.SetParent(raiz.transform, false);
            foreach (var peca in p.Pecas)
            {
                float y = p.Chao(peca.pos, peca.giro, peca.comprimento);
                var local = new Vector3(peca.pos.x, y, peca.pos.z);
                var M = Matrix4x4.TRS(local, Quaternion.Euler(0f, peca.giro, 0f), Vector3.one);
                switch (peca.tipo)
                {
                    case PlantaDaBase.Coisa.Barreira:
                    {
                        if (objetos == null || objetos.barreira == null) break;
                        var b = new GameObject("Barreira");
                        b.transform.SetParent(raiz.transform, false);
                        b.transform.localPosition = local;
                        b.transform.localRotation = Quaternion.Euler(0f, peca.giro, 0f);
                        b.AddComponent<MeshFilter>().sharedMesh = objetos.barreira;
                        b.AddComponent<MeshRenderer>().sharedMaterial = daCidade;
                        var bc = b.AddComponent<BoxCollider>();
                        bc.center = objetos.barreira.bounds.center;
                        bc.size = objetos.barreira.bounds.size;
                        break;
                    }
                    case PlantaDaBase.Coisa.Barraca: Barraca(M, peca.variante, desenho, colisoes); break;
                    case PlantaDaBase.Coisa.Guarita: Guarita(M, desenho, colisoes); break;
                    case PlantaDaBase.Coisa.Caixas: Caixas(M, desenho, colisoes); break;
                    case PlantaDaBase.Coisa.Mastro: Mastro(M, desenho, colisoes); break;
                    case PlantaDaBase.Coisa.Poste: Poste(M, desenho, colisoes, raiz.transform); break;
                    case PlantaDaBase.Coisa.Cancela: Cancela(M, desenho, colisoes); break;
                    default:
                    {
                        var modelo = Modelo(peca.tipo, peca.variante);
                        desenho.Juntar(modelo.Malha, M);
                        foreach (var cx in modelo.Colisao) Caixa(colisoes, M, cx.Centro, cx.Tamanho, cx.GiroY);
                        break;
                    }
                }
            }
            var go = new GameObject("Cerca, barracas e veículos");
            go.transform.SetParent(raiz.transform, false);
            var malha = desenho.ParaMesh($"{p.Lugar.Nome}: a base");
            c.Malhas.Add(malha);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = daCidade;
            MalhaDeObra.Devolver(desenho);
            NavMeshDoMundo.Mudou(c.Caixa);
            return c;
        }

        // um modelo da beira da estrada (feito uma vez)
        static ModelosDaEstrada.Modelo Modelo(PlantaDaBase.Coisa tipo, int variante)
        {
            if (tipo == PlantaDaBase.Coisa.Alambrado) variante = variante == 1 ? 0 : variante % 3 == 0 ? 0 : 2;   // sem o rasgado
            int chave = (int)tipo * 16 + variante;
            if (_modelos.TryGetValue(chave, out var m)) return m;
            m = tipo switch
            {
                PlantaDaBase.Coisa.Conteiner => ModelosDaEstrada.Conteiner(variante),
                PlantaDaBase.Coisa.Sacos => ModelosDaEstrada.Sacos(),
                PlantaDaBase.Coisa.Caminhao => ModelosDaEstrada.CaminhaoMilitar(),
                PlantaDaBase.Coisa.Blindado => ModelosDaEstrada.Blindado(),
                _ => ModelosDaEstrada.Alambrado(variante),
            };
            _modelos[chave] = m;
            return m;
        }

        static void Caixa(Transform pai, Matrix4x4 M, Vector3 centro, Vector3 tamanho, float giroY = 0f)
        {
            var go = new GameObject("Caixa");
            go.transform.SetParent(pai, false);
            go.transform.localPosition = M.MultiplyPoint3x4(centro);
            go.transform.localRotation = M.rotation * Quaternion.Euler(0f, giroY, 0f);
            go.AddComponent<BoxCollider>().size = tamanho;
        }

        static void Bloco(MalhaDeObra desenho, Matrix4x4 M, Vector3 centro, Vector3 tam, Cor cor) => desenho.Caixa(M * Matrix4x4.Translate(centro), tam, cor);

        // a barraca do exército: 4 × 6 m, as paredes baixas e o telhado em duas águas (a porta para +z)
        static void Barraca(Matrix4x4 M, int variante, MalhaDeObra desenho, Transform colisoes)
        {
            var perfil = new[] { new Vector2(-2f, 0f), new Vector2(2f, 0f), new Vector2(2f, 1.1f), new Vector2(0f, 2.7f), new Vector2(-2f, 1.1f) };
            var a = new List<Vector3>();
            var b = new List<Vector3>();
            foreach (var v in perfil)
            {
                a.Add(M.MultiplyPoint3x4(new Vector3(v.x, v.y, -3f)));
                b.Add(M.MultiplyPoint3x4(new Vector3(v.x, v.y, 3f)));
            }
            desenho.Solido(a, b, variante % 2 == 0 ? "CarroVerde" : "PinheiroEscuro");
            // a porta (a lona mais escura, aberta) e as estacas
            Bloco(desenho, M, new Vector3(0f, 0.95f, 3.02f), new Vector3(1.3f, 1.9f, 0.04f), "Pneu");
            foreach (float x in new[] { -2.3f, 2.3f })
            foreach (float z in new[] { -2.6f, 2.6f })
                Bloco(desenho, M, new Vector3(x, 0.15f, z), new Vector3(0.06f, 0.3f, 0.06f), "MadeiraVelha");
            Caixa(colisoes, M, new Vector3(0f, 1.0f, 0f), new Vector3(4f, 2f, 6f));
        }

        // a guarita do portão: as paredes, a faixa de vidro em volta e o teto de chapa
        static void Guarita(Matrix4x4 M, MalhaDeObra desenho, Transform colisoes)
        {
            Bloco(desenho, M, new Vector3(0f, 1.25f, 0f), new Vector3(2.2f, 2.5f, 2.2f), "ConcretoClaro");
            Bloco(desenho, M, new Vector3(0f, 1.65f, 0f), new Vector3(2.24f, 0.7f, 2.24f), "Vidro");
            Bloco(desenho, M, new Vector3(0f, 2.6f, 0f), new Vector3(2.7f, 0.15f, 2.7f), "Metal");
            Bloco(desenho, M, new Vector3(0f, 1.0f, 1.12f), new Vector3(0.8f, 2.0f, 0.04f), "Lixeira");
            Caixa(colisoes, M, new Vector3(0f, 1.3f, 0f), new Vector3(2.2f, 2.6f, 2.2f));
        }

        // a pilha de caixas de munição (verdes) em cima de um palete
        static void Caixas(Matrix4x4 M, MalhaDeObra desenho, Transform colisoes)
        {
            Bloco(desenho, M, new Vector3(0f, 0.07f, 0f), new Vector3(2.2f, 0.14f, 1.3f), "Madeira");
            foreach (float x in new[] { -0.52f, 0.52f })
            foreach (float z in new[] { -0.32f, 0.32f })
                Bloco(desenho, M, new Vector3(x, 0.4f, z), new Vector3(1.0f, 0.52f, 0.6f), "CarroVerde");
            Bloco(desenho, M, new Vector3(-0.3f, 0.92f, 0f), new Vector3(1.0f, 0.52f, 0.6f), "CarroVerde");
            Bloco(desenho, M, new Vector3(0.65f, 0.83f, 0.1f), new Vector3(0.6f, 0.34f, 0.4f), "PinheiroEscuro");
            Caixa(colisoes, M, new Vector3(0f, 0.6f, 0f), new Vector3(2.2f, 1.2f, 1.3f));
        }

        // o mastro com a bandeira verde
        static void Mastro(Matrix4x4 M, MalhaDeObra desenho, Transform colisoes)
        {
            Bloco(desenho, M, new Vector3(0f, 0.15f, 0f), new Vector3(0.7f, 0.3f, 0.7f), "Concreto");
            desenho.Cilindro(M, 0.06f, 0.05f, 8f, 6, "Metal");
            Bloco(desenho, M, new Vector3(0.82f, 7.3f, 0f), new Vector3(1.6f, 1.0f, 0.03f), "CarroVerde");
            Bloco(desenho, M, new Vector3(0.82f, 7.3f, 0f), new Vector3(0.5f, 0.3f, 0.04f), "PlacaAmarela");
            Caixa(colisoes, M, new Vector3(0f, 4f, 0f), new Vector3(0.25f, 8f, 0.25f));
        }

        // o poste de luz: o cano, a lâmpada para dentro da base (+z) e a luz, acesa sempre (o gerador da base)
        static void Poste(Matrix4x4 M, MalhaDeObra desenho, Transform colisoes, Transform raiz)
        {
            desenho.Cilindro(M, 0.09f, 0.07f, 6f, 6, "Metal");
            Bloco(desenho, M, new Vector3(0f, 5.95f, 0.45f), new Vector3(0.12f, 0.12f, 0.9f), "Metal");
            Bloco(desenho, M, new Vector3(0f, 5.85f, 0.9f), new Vector3(0.55f, 0.22f, 0.4f), "JanelaAcesa");
            Caixa(colisoes, M, new Vector3(0f, 3f, 0f), new Vector3(0.25f, 6f, 0.25f));
            var luz = new GameObject("Luz da base").AddComponent<Light>();
            luz.transform.SetParent(raiz, false);
            luz.transform.localPosition = M.MultiplyPoint3x4(new Vector3(0f, 5.5f, 1.2f));
            luz.type = LightType.Point;
            luz.color = new Color(0.9f, 0.95f, 1f);
            luz.range = 24f;
            luz.intensity = 2.6f;
            luz.shadows = LightShadows.None;
        }

        // a cancela do portão, levantada: o poste listrado e o braço vermelho e branco para cima
        static void Cancela(Matrix4x4 M, MalhaDeObra desenho, Transform colisoes)
        {
            Bloco(desenho, M, new Vector3(0f, 0.55f, 0f), new Vector3(0.3f, 1.1f, 0.3f), "PlacaAmarela");
            var braco = M * Matrix4x4.TRS(new Vector3(0f, 1.1f, 0f), Quaternion.Euler(0f, 0f, 78f), Vector3.one);
            for (int k = 0; k < 6; k++)
                desenho.Caixa(braco * Matrix4x4.Translate(new Vector3(0.5f + k, 0f, 0f)), new Vector3(1f, 0.1f, 0.1f), k % 2 == 0 ? "PlacaVermelha" : "ListraBranca");
            Caixa(colisoes, M, new Vector3(0f, 0.55f, 0f), new Vector3(0.3f, 1.1f, 0.3f));
        }
    }
}
