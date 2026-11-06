using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// A construção dos acampamentos (02/11/2026), em cada máquina, até <see cref="AcampamentosDoMundo.raioDaConstrucao"/> m
    /// do jogador local (nada vai pela rede: sai da <see cref="PlantaDoAcampamento"/>, igual em todas):
    ///  - a casa (o kit das construções, sem porão), com as janelas todas tapadas com tábuas (<see cref="AbandonoDaObra.Fortificar"/>),
    ///    a mobília, os pisos, a caixa de dentro e o eco;
    ///  - a cerca: os pedaços de alambrado, os muros de sacos de areia e os contêineres (<see cref="ModelosDaEstrada"/>) e as
    ///    barreiras de concreto do portão (o modelo do Blender), com as caixas de colisão;
    ///  - as bancas: o balcão, as caixas, os postes, a lona, a placa escrita (o <see cref="Letreiro"/>) e uma lâmpada;
    ///  - as barracas dos moradores e as fogueiras (o fogo das construções, com a luz e um estalo de vez em quando).
    /// Pronta, a área do NavMesh em volta é refeita (<see cref="NavMeshDoMundo.Mudou"/>).
    /// </summary>
    public partial class AcampamentosDoMundo
    {
        sealed class Construido
        {
            public GameObject Raiz;
            public Bounds Caixa;
            public readonly List<Mesh> Malhas = new List<Mesh>();
            public readonly List<(MeshFilter filtro, List<ModelosDaEstrada.Texto> textos)> Letras = new List<(MeshFilter, List<ModelosDaEstrada.Texto>)>();
            public readonly List<(Vector3 pos, Light luz)> Fogueiras = new List<(Vector3, Light)>();
        }

        readonly Dictionary<int, Construido> _construidos = new Dictionary<int, Construido>();
        static readonly Dictionary<int, ModelosDaEstrada.Modelo> _pecas = new Dictionary<int, ModelosDaEstrada.Modelo>();
        static readonly Dictionary<int, Mesh> _malhasDasPecas = new Dictionary<int, Mesh>();
        [System.NonSerialized] Material _letras;
        int _versaoDasLetras = -1;
        float _proximaRevisao, _proximoEstalo;
        static AudioClip[] _estalos;

        static readonly string[] Lonas = { "CarroAzul", "CarroVerde", "Laranja", "CarroVinho", "Ferrugem" };

        /// <summary>Os acampamentos montados nesta máquina (para os testes).</summary>
        public int Construidos => _construidos.Count;

        void AtualizarConstrucoes()
        {
            var d = Dados;
            if (d?.Plano == null)
            {
                if (_construidos.Count > 0) ApagarConstrucoes();
                return;
            }
            if (Time.unscaledTime >= _proximaRevisao)
            {
                _proximaRevisao = Time.unscaledTime + 0.5f;
                Revisar();
            }
            ChuvaNasFogueiras();   // a conta da chuva anda sempre (longe do acampamento também)
            if (_construidos.Count == 0) return;
            if (_versaoDasLetras != Letreiro.Versao) RefazerAsLetras();
            Fogueiras();
        }

        void Revisar()
        {
            Vector3 centro;
            var local = JogadorRede.Local;
            if (local != null) centro = local.transform.position;
            else if (Camera.main != null) centro = Camera.main.transform.position;
            else return;
            var c2 = new Vector2(centro.x, centro.z);
            for (int a = 0; a < Quantos; a++)
            {
                var p = Planta(a);
                float dist = Vector2.Distance(c2, p.Lugar.Centro);
                bool montado = _construidos.TryGetValue(a, out var c);
                if (!montado && dist < raioDaConstrucao)
                {
                    _construidos[a] = Construir(p);
                    return;   // um por vez (a casa leva alguns ms)
                }
                if (montado && dist > raioDaConstrucao + 60f)
                {
                    Apagar(c);
                    _construidos.Remove(a);
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

        Construido Construir(PlantaDoAcampamento p)
        {
            double inicio = Time.realtimeSinceStartupAsDouble;
            var c = Montar(p);
            Debug.Log($"[Acampamentos] O {p.Lugar.Nome} foi montado em {(Time.realtimeSinceStartupAsDouble - inicio) * 1000.0:0} ms.");
            return c;
        }

        Construido Montar(PlantaDoAcampamento p)
        {
            var c = new Construido();
            var raiz = new GameObject(p.Lugar.Nome);
            raiz.transform.SetPositionAndRotation(p.Mundo(Vector3.zero), Quaternion.Euler(0f, p.GiroNoMundo(0f), 0f));
            c.Raiz = raiz;
            float H = p.Meia;
            c.Caixa = new Bounds(p.Mundo(Vector3.zero) + Vector3.up * 5f, new Vector3(2f * H + 24f, 30f, 2f * H + 24f));
            var objetos = ObjetosDoMundo.Instancia;
            var terreno = TerrenoDoMundo.Instancia;
            Material daCidade = objetos != null && objetos.materialDaCidade != null ? objetos.materialDaCidade : terreno != null ? terreno.materialDaCidade : null;
            Material dasCasas = terreno != null && terreno.materialDasCasas != null ? terreno.materialDasCasas : daCidade;

            MontarACasa(p, raiz.transform, dasCasas, c);

            // a cerca, as bancas e as barracas: uma malha só, e as caixas de colisão em filhos
            var desenho = MalhaDeObra.Emprestar(TamanhoDaMalha.Grande);
            var colisoes = new GameObject("Colisões").transform;
            colisoes.SetParent(raiz.transform, false);
            // o chão de terra batida dentro da cerca (o chão do mundo ali é plano: Acampamento.Altura), por cima das folhas
            desenho.Caixa(Matrix4x4.Translate(new Vector3(0f, 0.015f, 0f)), new Vector3(2f * H + 1f, 0.05f, 2f * H + 1f), "Terra");
            foreach (var (tipo, pos, giro) in p.Coisas) Coisa(tipo, pos, giro, desenho, colisoes);
            foreach (var peca in p.Cerca)
            {
                var M = Matrix4x4.TRS(peca.pos, Quaternion.Euler(0f, peca.giro, 0f), Vector3.one);
                if (peca.tipo == PecaDaCerca.Barreira)
                {
                    if (objetos == null || objetos.barreira == null) continue;
                    var b = new GameObject("Barreira");
                    b.transform.SetParent(raiz.transform, false);
                    b.transform.localPosition = peca.pos;
                    b.transform.localRotation = Quaternion.Euler(0f, peca.giro, 0f);
                    b.AddComponent<MeshFilter>().sharedMesh = objetos.barreira;
                    b.AddComponent<MeshRenderer>().sharedMaterial = daCidade;
                    var bc = b.AddComponent<BoxCollider>();
                    bc.center = objetos.barreira.bounds.center;
                    bc.size = objetos.barreira.bounds.size;
                    continue;
                }
                var modelo = Peca(peca.tipo, peca.variante);
                desenho.Juntar(modelo.Malha, M);
                foreach (var cx in modelo.Colisao) Caixa(colisoes, M, cx.Centro, cx.Tamanho, cx.GiroY);
            }
            for (int k = 0; k < p.Bancas.Count; k++) Banca(p.Bancas[k], k, desenho, colisoes, raiz.transform, c);
            foreach (var (pos, giro) in p.Barracas) Barraca(pos, giro, desenho, colisoes);
            var go = new GameObject("Cerca, bancas e barracas");
            go.transform.SetParent(raiz.transform, false);
            var malha = desenho.ParaMesh($"{p.Lugar.Nome}: cerca e bancas");
            c.Malhas.Add(malha);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = daCidade;
            MalhaDeObra.Devolver(desenho);

            foreach (var f in p.Fogueiras) Fogueira(p, f, raiz.transform, c);
            NavMeshDoMundo.Mudou(c.Caixa);
            return c;
        }

        // um pedaço de cerca dos modelos da estrada (feito uma vez)
        static ModelosDaEstrada.Modelo Peca(PecaDaCerca tipo, int variante)
        {
            if (tipo == PecaDaCerca.Alambrado) variante = variante == 1 ? 0 : variante % 3 == 0 ? 0 : 2;   // sem o rasgado
            int chave = (int)tipo * 16 + variante;
            if (_pecas.TryGetValue(chave, out var m)) return m;
            m = tipo switch
            {
                PecaDaCerca.Sacos => ModelosDaEstrada.Sacos(),
                PecaDaCerca.Conteiner => ModelosDaEstrada.Conteiner(variante),
                _ => ModelosDaEstrada.Alambrado(variante),
            };
            _pecas[chave] = m;
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

        // a banca: o balcão de frente para o corredor, as caixas atrás, os postes e a lona por cima, a placa e a lâmpada
        void Banca(PlantaDoAcampamento.Banca b, int k, MalhaDeObra desenho, Transform colisoes, Transform raiz, Construido c)
        {
            var M = Matrix4x4.TRS(b.balcao, Quaternion.Euler(0f, b.giro, 0f), Vector3.one);
            void Bloco(Vector3 centro, Vector3 tam, Cor cor) => desenho.Caixa(M * Matrix4x4.Translate(centro), tam, cor);
            Bloco(new Vector3(0f, 0.5f, 0f), new Vector3(2.6f, 1.0f, 0.75f), "MadeiraVelha");
            Bloco(new Vector3(0f, 1.02f, 0.02f), new Vector3(2.7f, 0.05f, 0.85f), "Madeira");
            Caixa(colisoes, M, new Vector3(0f, 0.5f, 0f), new Vector3(2.6f, 1.0f, 0.75f));
            Bloco(new Vector3(-0.85f, 0.3f, -1.75f), new Vector3(0.6f, 0.6f, 0.6f), "Papelao");
            Bloco(new Vector3(-0.85f, 0.85f, -1.75f), new Vector3(0.5f, 0.5f, 0.5f), "Papelao");
            Bloco(new Vector3(0.8f, 0.25f, -1.8f), new Vector3(0.8f, 0.5f, 0.55f), "Madeira");
            Caixa(colisoes, M, new Vector3(0f, 0.45f, -1.78f), new Vector3(2.4f, 0.9f, 0.6f));
            foreach (float x in new[] { -1.45f, 1.45f })
            foreach (float z in new[] { 0.45f, -2.1f })
                Bloco(new Vector3(x, 1.35f, z), new Vector3(0.1f, 2.7f, 0.1f), "MadeiraVelha");
            Bloco(new Vector3(0f, 2.72f, -0.82f), new Vector3(3.2f, 0.06f, 2.9f), Lonas[k % Lonas.Length]);
            // a placa, presa nos postes da frente, e o que ela diz
            Bloco(new Vector3(0f, 2.3f, 0.48f), new Vector3(2.2f, 0.42f, 0.05f), "PlacaBranca");
            var placa = new GameObject("Placa");
            placa.transform.SetParent(raiz, false);
            placa.transform.localPosition = M.MultiplyPoint3x4(new Vector3(0f, 2.3f, 0.48f));
            placa.transform.localRotation = M.rotation;
            var textos = new List<ModelosDaEstrada.Texto>
            {
                new ModelosDaEstrada.Texto { Conteudo = PrecosDosSobreviventes.Placa(b.categoria), U = 0f, V = 0f, Z = 0.03f, Altura = 0.24f, Alinhamento = 0, Cor = ModelosDaEstrada.LetraEscura },
            };
            var letras = Letreiro.Malha(textos, ModelosDaEstrada.LetraEscura);
            if (letras != null && MaterialDasLetras() != null)
            {
                c.Malhas.Add(letras);
                var f = placa.AddComponent<MeshFilter>();
                f.sharedMesh = letras;
                var r = placa.AddComponent<MeshRenderer>();
                r.sharedMaterial = _letras;
                r.shadowCastingMode = ShadowCastingMode.Off;
                c.Letras.Add((f, textos));
            }
            // a lâmpada embaixo da lona
            Bloco(new Vector3(0f, 2.55f, -0.4f), new Vector3(0.14f, 0.18f, 0.14f), "JanelaAcesa");
            var luz = new GameObject("Lâmpada").AddComponent<Light>();
            luz.transform.SetParent(raiz, false);
            luz.transform.localPosition = M.MultiplyPoint3x4(new Vector3(0f, 2.4f, -0.3f));
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.78f, 0.5f);
            luz.range = 9f;
            luz.intensity = 3f;
            luz.shadows = LightShadows.None;
        }

        // as coisas soltas: o toco (um banco em volta da fogueira), o barril e a pilha de caixas
        static void Coisa(PlantaDoAcampamento.CoisaSolta tipo, Vector3 pos, float giro, MalhaDeObra desenho, Transform colisoes)
        {
            var M = Matrix4x4.TRS(pos, Quaternion.Euler(0f, giro, 0f), Vector3.one);
            switch (tipo)
            {
                case PlantaDoAcampamento.CoisaSolta.Toco:
                    desenho.Caixa(M * Matrix4x4.Translate(new Vector3(0f, 0.2f, 0f)), new Vector3(1.3f, 0.32f, 0.34f), "TroncoEscuro");
                    break;
                case PlantaDoAcampamento.CoisaSolta.Barril:
                    desenho.Cilindro(M, 0.3f, 0.3f, 0.9f, 10, "Ferrugem");
                    desenho.Cilindro(M * Matrix4x4.Translate(new Vector3(0.75f, 0f, 0.1f)), 0.3f, 0.3f, 0.9f, 10, "Cacamba");
                    Caixa(colisoes, M, new Vector3(0.37f, 0.45f, 0.05f), new Vector3(1.4f, 0.9f, 0.7f));
                    break;
                default:
                    desenho.Caixa(M * Matrix4x4.Translate(new Vector3(0f, 0.3f, 0f)), new Vector3(0.6f, 0.6f, 0.6f), "Papelao");
                    desenho.Caixa(M * Matrix4x4.Translate(new Vector3(0.65f, 0.25f, 0.05f)), new Vector3(0.6f, 0.5f, 0.55f), "Madeira");
                    desenho.Caixa(M * Matrix4x4.TRS(new Vector3(0.2f, 0.85f, 0f), Quaternion.Euler(0f, 18f, 0f), Vector3.one), new Vector3(0.55f, 0.5f, 0.5f), "Papelao");
                    Caixa(colisoes, M, new Vector3(0.32f, 0.55f, 0f), new Vector3(1.3f, 1.1f, 0.65f));
                    break;
            }
        }

        // a barraca: a lona em A, de 2,6 × 3,2 m
        static void Barraca(Vector3 pos, float giro, MalhaDeObra desenho, Transform colisoes)
        {
            var R = Quaternion.Euler(0f, giro, 0f);
            var a = new List<Vector3>();
            var b = new List<Vector3>();
            foreach (var v in new[] { new Vector3(-1.3f, 0f, 0f), new Vector3(1.3f, 0f, 0f), new Vector3(0f, 1.5f, 0f) })
            {
                a.Add(pos + R * (v + new Vector3(0f, 0f, -1.6f)));
                b.Add(pos + R * (v + new Vector3(0f, 0f, 1.6f)));
            }
            desenho.Solido(a, b, "CarroVerde");
            Caixa(colisoes, Matrix4x4.TRS(pos, R, Vector3.one), new Vector3(0f, 0.6f, 0f), new Vector3(2.4f, 1.2f, 3.2f));
        }

        // a fogueira: a malha da fogueira montada e o fogo (as chamas e a luz)
        void Fogueira(PlantaDoAcampamento p, Vector3 local, Transform raiz, Construido c)
        {
            var construcoes = ConstrucoesNoMundo.Instancia;
            if (construcoes == null) return;
            var go = new GameObject("Fogueira");
            go.transform.SetParent(raiz, false);
            go.transform.localPosition = local;
            var malha = construcoes.MalhaDe(Item.Fogueira);
            if (malha != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                go.AddComponent<MeshRenderer>().sharedMaterial = construcoes.material;
            }
            var fogo = ConstrucoesNoMundo.Fogo(go.transform, new Vector3(0f, 0.12f, 0f), construcoes.materialDoFogo, 1.15f, 14f, true, go.layer);
            c.Fogueiras.Add((p.Mundo(local) + Vector3.up * 0.3f, fogo.GetComponent<Light>()));
        }

        // a casa do acampamento: a planta da PlantaDoAcampamento, fortificada, com a mobília (como as casas novas do Dia 0)
        static void MontarACasa(PlantaDoAcampamento p, Transform raiz, Material material, Construido c)
        {
            var M = Medidas.DoJogo;
            var planta = p.Casa;
            var casa = new Casa(planta, M);
            casa.Montar();
            AbandonoDaObra.Fortificar(casa, planta.semente);
            Mobilia.Por(casa.moveis, casa.vis, casa.col, planta.semente);
            if (planta.espelhar) casa.col.Espelhar();
            var pontos = new List<PontoDeMovel>();
            foreach (var pm in casa.moveis) pontos.Add(planta.espelhar ? pm.Espelhado() : pm);
            MalhaDaMobilia nova = null;
            if (MobiliaNova.Pronta)
            {
                MobiliaNova.Caixas(pontos, casa.col);
                nova = MobiliaNova.Desenho(pontos, planta.semente);
            }
            var go = new GameObject("Casa do acampamento");
            go.transform.SetParent(raiz, false);
            go.transform.localPosition = p.PosicaoDaCasa;
            go.transform.localRotation = Quaternion.Euler(0f, PlantaDoAcampamento.GiroDaCasa, 0f);
            var desenho = MalhaDeObra.Emprestar(TamanhoDaMalha.Grande);
            desenho.Juntar(casa.vis, Matrix4x4.identity, planta.espelhar, altura: true);
            var malha = desenho.ParaMesh("Casa do acampamento");
            MalhaDeObra.Devolver(desenho);
            c.Malhas.Add(malha);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var colisao = casa.col.ParaMesh("Casa do acampamento (colisão)");
            c.Malhas.Add(colisao);
            go.AddComponent<MeshCollider>().sharedMesh = colisao;
            if (nova != null) MobiliaDaConstrucao.Por(go, pontos, nova, planta.semente, -100);

            // os pisos (os passos), a caixa de dentro (a neblina de dentro) e o eco
            var zonas = new List<Bounds>();
            var pisos = new List<Superficie>();
            float Sx(float v) => planta.espelhar ? -v : v;
            foreach (var k in planta.comodos)
            {
                float xa = Sx(k.x0), xb = Sx(k.x1);
                zonas.Add(new Bounds(new Vector3((xa + xb) / 2, casa.Base(k.andar), (k.z0 + k.z1) / 2), new Vector3(Mathf.Abs(xb - xa), 1f, k.z1 - k.z0)));
                pisos.Add(TerrenoDoMundo.SomDoPiso(k.piso ?? "Taco"));
            }
            var pz = go.AddComponent<PisoPorZona>();
            pz.zonas = zonas.ToArray();
            pz.superficies = pisos.ToArray();
            pz.resto = Superficie.Concreto;
            float baixo = casa.Base(0), alto = casa.Base(planta.andares - 1) + M.altura + M.forro;
            var centro = new Vector3(0f, (baixo + alto) / 2, 0f);
            var tamanho = new Vector3(planta.W - 0.2f, alto - baixo, planta.D - 0.2f);
            var dentro = new GameObject("Dentro");
            dentro.transform.SetParent(go.transform, false);
            dentro.transform.localPosition = centro;
            dentro.AddComponent<AreaInterna>().tamanho = tamanho;
            EcoSoDentro.Criar(go.transform, new Bounds(centro, tamanho));
            MalhaDeObra.Devolver(casa.vis);
            MalhaDeObra.Devolver(casa.col);
        }

        // ------------------------------------------------------------------ as letras e o fogo

        Material MaterialDasLetras()
        {
            var objetos = ObjetosDoMundo.Instancia;
            if (_letras == null && objetos != null && objetos.materialDasLetras != null)
            {
                _letras = new Material(objetos.materialDasLetras) { name = "Letras dos acampamentos" };
                _letras.SetColor("_Color", new Color(0.12f, 0.11f, 0.1f));
            }
            if (_letras != null && _letras.mainTexture != Letreiro.Atlas) _letras.SetTexture("_MainTex", Letreiro.Atlas);
            return _letras;
        }

        void RefazerAsLetras()
        {
            _versaoDasLetras = Letreiro.Versao;
            foreach (var c in _construidos.Values)
            {
                c.Letras.RemoveAll(l => l.filtro == null);
                foreach (var (filtro, textos) in c.Letras) Letreiro.Malha(textos, ModelosDaEstrada.LetraEscura, filtro.sharedMesh);
            }
            MaterialDasLetras();
        }

        // a chuva apaga as fogueiras dos acampamentos também (05/11/2026; só o que se vê, em cada máquina: os zumbis não
        // entram no acampamento de todo jeito): elas molham como as dos jogadores e os moradores acendem de novo um minuto
        // depois de a chuva passar
        float _molhadaDosAcampamentos, _semChuvaDesde = -1f;
        bool _fogueirasApagadas;
        const float ReacenderDepoisDaChuva = 60f;

        void ChuvaNasFogueiras()
        {
            float agora = Time.time, chuva = Tempo.Chuva;
            if (chuva > ConstrucoesNoMundo.ChuvaQueMolha)
            {
                _semChuvaDesde = -1f;
                _molhadaDosAcampamentos = Mathf.Min(1f, _molhadaDosAcampamentos + Time.deltaTime * chuva / ConstrucoesNoMundo.SegundosParaApagarAFogueira);
                if (_molhadaDosAcampamentos >= 1f) _fogueirasApagadas = true;
            }
            else
            {
                if (_semChuvaDesde < 0f) _semChuvaDesde = agora;
                _molhadaDosAcampamentos = Mathf.Max(0f, _molhadaDosAcampamentos - Time.deltaTime / ConstrucoesNoMundo.SegundosParaSecar);
                if (_fogueirasApagadas && agora - _semChuvaDesde > ReacenderDepoisDaChuva) _fogueirasApagadas = false;
            }
        }

        // o fogo tremula, e a mais perto estala de vez em quando (até 20 m de quem ouve)
        void Fogueiras()
        {
            var cam = Camera.main;
            Vector3 ouvido = cam != null ? cam.transform.position : Vector3.zero;
            float agora = Time.time, menor = float.MaxValue;
            Vector3 maisPerto = default;
            foreach (var c in _construidos.Values)
                foreach (var (pos, luz) in c.Fogueiras)
                {
                    if (luz != null && luz.gameObject.activeSelf == _fogueirasApagadas) luz.gameObject.SetActive(!_fogueirasApagadas);
                    if (_fogueirasApagadas) continue;
                    if (luz != null) luz.intensity = 2.6f + 0.9f * Mathf.PerlinNoise(agora * 3.1f, pos.x) + 0.4f * Mathf.Sin(agora * 17f + pos.z);
                    float d = (pos - ouvido).sqrMagnitude;
                    if (d < menor)
                    {
                        menor = d;
                        maisPerto = pos;
                    }
                }
            if (cam == null || menor > 20f * 20f || agora < _proximoEstalo) return;
            _proximoEstalo = agora + Random.Range(0.07f, 0.4f);
            Efeitos.Som(Estalos(), maisPerto, Random.Range(0.18f, 0.45f), true, 20f);
        }

        // os estalos da lenha: estouros curtos de ruído (gerados uma vez)
        static AudioClip[] Estalos()
        {
            if (_estalos != null) return _estalos;
            const int taxa = 44100;
            var rnd = new System.Random(77);
            _estalos = new AudioClip[10];
            for (int k = 0; k < _estalos.Length; k++)
            {
                int n = (int)(taxa * (0.02f + 0.05f * (float)rnd.NextDouble()));
                var dados = new float[n];
                float queda = 60f + 140f * (float)rnd.NextDouble();
                float anterior = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)taxa;
                    float ruido = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    anterior = Mathf.Lerp(anterior, ruido, 0.55f);   // um pouco mais grave que o ruído branco
                    dados[i] = anterior * Mathf.Exp(-t * queda) * 0.9f;
                }
                _estalos[k] = AudioClip.Create($"Estalo {k}", n, 1, taxa, false);
                _estalos[k].SetData(dados, 0);
            }
            return _estalos;
        }
    }
}
