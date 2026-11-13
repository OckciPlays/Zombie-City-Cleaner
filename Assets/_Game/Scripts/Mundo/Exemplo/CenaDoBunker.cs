using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static ZombieCityCleaner.Mundo.PlantaDeBunker;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A cena de teste do bunker novo (o Plano_Bunker.md da pasta de arte, a parte 2: a casca), para andar por dentro e
    /// sentir o tamanho: o <see cref="Bunker"/> (a casca e os 5 andares) no chão, debaixo do morro de terra, com o corte na
    /// frente do portão (as abas de concreto), o pátio cercado e a estrada de terra; o portão de duas folhas e a portinhola
    /// abrem com o E (o portão devagar, a portinhola depressa), como as portas de aço do B4 e da casinha; uns carros nas
    /// vagas; as lâmpadas de cada cômodo acesas. Sem neblina dentro: com a câmera dentro da caixa, a luz de fora cai (na
    /// garagem e na casinha um pouco, embaixo quase toda) e a neblina some lá embaixo.
    /// As paradas (as setas do AndarNoExemplo) são o pátio, o hall de cada andar e o alto do morro; o P mostra a planta do
    /// andar em que o jogador está (↑ ↓ os outros).
    /// </summary>
    [ExecuteAlways]
    public class CenaDoBunker : CenaDeExemplo
    {
        /// <summary>A luz de fora que sobra dentro: na garagem e na casinha (o portão deixa entrar o dia) e embaixo.</summary>
        [Range(0f, 1f)] public float luzNaGaragem = 0.35f, luzEmbaixo = 0.08f;

        [Tooltip("Os prefabs da mobília nova (com o lote do bunker): Mobília nova > Montar.")]
        public GameObject[] mobilia;
        [Tooltip("A malha do painel de armas do bunker antigo (Painel_Armas, do Cidade_Moveis.fbx).")]
        public Mesh painelDeArmas;
        [Tooltip("O material da parede dos fundadores (Art/Materials/Fundadores.mat).")]
        public Material fundadores;

        readonly List<Light> _lampadas = new List<Light>();
        readonly List<ParteQueAbre> _portas = new List<ParteQueAbre>();

        protected override void Montar()
        {
            var M = Medidas.DoJogo;
            _lampadas.Clear();
            _portas.Clear();
            _faltam.Clear();
            _pecas = 0;
            _prefabs = null;
            var go = Novo("Bunker", raiz).transform;

            var casca = new Bunker(M, -1, semente);
            casca.MontarCasca();
            var tris = new List<string> { $"casca {casca.vis.Triangulos:N0}" };
            Parte(casca, "Bunker (casca)", go);
            for (int f = 0; f < Andares; f++)
            {
                var andar = new Bunker(M, f, semente);
                andar.MontarAndar();
                tris.Add($"{Codigos[f]} {andar.vis.Triangulos:N0}");
                Parte(andar, $"Bunker {Codigos[f]} ({NomesDosAndares[f]})", go);
            }
            PecasQueAbrem(go);
            Moveis(go);
            Fundadores(go);
            MontarMorro();
            CarrosNasVagas();
            Arvores();
            Objeto("Chão (o morro, o pátio e a estrada)", raiz, Malha(chao.vis, "Chão"), Malha(chao.col, "Chão (colisão)"), Vector3.zero, 0f);
            Paradas.Clear();
            ParadasDoBunker();
            _zona = -1;
            LuzDeFora();
            resumo.AppendLine($"Bunker: 32 × 24 m, 5 andares, {_lampadas.Count} lâmpadas, {_pecas} peças da mobília; triângulos: {string.Join(", ", tris)} (total {triangulos:N0})");
            if (_faltam.Count > 0) resumo.AppendLine("Faltam os prefabs: " + string.Join(", ", _faltam) + " (Mobília nova > Montar)");
        }

        // uma parte do bunker como objeto (com a colisão) e as lâmpadas dela; as malhas voltam para o estoque
        void Parte(Bunker b, string nome, Transform pai)
        {
            Objeto(nome, pai, Malha(b.vis, nome), Malha(b.col, nome + " (colisão)"), Vector3.zero, 0f);
            triangulos += b.vis.Triangulos;
            foreach (var (pos, alcance, forca) in b.Luzes)
            {
                var l = Novo("Lâmpada", pai).AddComponent<Light>();
                l.transform.localPosition = pos;
                l.type = LightType.Point;
                l.range = alcance;
                l.intensity = forca;
                l.color = new Color(1f, 0.92f, 0.78f);
                l.shadows = LightShadows.None;
                _lampadas.Add(l);
            }
            MalhaDeObra.Devolver(b.vis);
            MalhaDeObra.Devolver(b.col);
        }

        // ------------------------------------------------------------ os modelos do Blender (a mobília nova e o lote do bunker)

        Dictionary<string, GameObject> _prefabs;

        GameObject Prefab(string modelo)
        {
            if (_prefabs == null)
            {
                _prefabs = new Dictionary<string, GameObject>();
                if (mobilia != null)
                    foreach (var p in mobilia)
                        if (p != null) _prefabs[p.name] = p;
            }
            return _prefabs.TryGetValue(modelo, out var pf) ? pf : null;
        }

        /// <summary>
        /// Um modelo da mobília nova (ou do lote do bunker) no lugar, sem a colisão do corpo (a dele está na colisão do andar,
        /// <see cref="MoveisDoBunker.Caixas"/>); as partes que abrem ficam com a delas. O C troca a cor (os do catálogo).
        /// </summary>
        GameObject Por(string modelo, Transform pai, Vector3 lugar, float giro, float escala)
        {
            var pf = Prefab(modelo);
            if (pf == null)
            {
                _faltam.Add(modelo);
                return null;
            }
            var go = Instantiate(pf, pai);
            go.name = modelo;
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetLocalPositionAndRotation(lugar, Quaternion.Euler(0f, giro, 0f));
            go.transform.localScale = Vector3.one * escala;
            var corpo = go.GetComponent<MeshCollider>();
            if (corpo != null) DestroyImmediate(corpo);
            go.AddComponent<MovelDoMostruario>().Guardar(modelo);
            _pecas++;
            return go;
        }

        readonly HashSet<string> _faltam = new HashSet<string>();
        int _pecas;

        // uma parte do modelo que abre como porta, para dentro (o lado de -z dele: no portão, o de dentro do bunker)
        ParteQueAbre ParaDentro(Transform parte, float duracao, string rotulo)
        {
            float cx = parte.GetComponent<MeshFilter>().sharedMesh.bounds.center.x;
            var p = parte.gameObject.AddComponent<ParteQueAbre>();
            p.tipo = ParteQueAbre.Tipo.Porta;
            // estendida para -x, o giro negativo leva a ponta para -z
            p.quanto = cx < 0f ? -90f : 90f;
            p.duracao = duracao;
            p.rotulo = rotulo;
            _portas.Add(p);
            return p;
        }

        // o portão (o modelo, de frente para fora: as folhas abrem juntas, para dentro, devagar), a portinhola presa na folha
        // da esquerda e as portas de aço do B4 (na saída da escada) e da casinha (para o morro), abrindo para o lado da porta
        void PecasQueAbrem(Transform pai)
        {
            var portao = Por("Portao", pai, new Vector3(PortaoX0 + PortaoLargura / 2, 0f, Z0), 180f, 1f);
            if (portao != null)
            {
                Transform f1 = portao.transform.Find("Portao.Folha.1"), f2 = portao.transform.Find("Portao.Folha.2"),
                          portinhola = portao.transform.Find("Portao.Portinhola.1");
                if (f1 != null && f2 != null)
                {
                    var esq = ParaDentro(f1, 6f, "o portão");
                    var dir = ParaDentro(f2, 6f, "o portão");
                    esq.gameObject.AddComponent<FolhasJuntas>().outra = dir;
                    dir.gameObject.AddComponent<FolhasJuntas>().outra = esq;
                    if (portinhola != null)
                    {
                        portinhola.SetParent(f1, true);
                        ParaDentro(portinhola, 0.6f, "a portinhola");
                    }
                }
            }
            foreach (var (y, rotulo) in new[] { (Base(Andares - 1), "a porta de aço (B4, restrito)"), (Morro, "a porta da casinha") })
            {
                var porta = Por("PortaDeAco", pai, new Vector3(Escada.xMax, y, PortaDaEscadaZ), 90f, 1f);
                var folha = porta != null ? porta.GetComponentInChildren<ParteQueAbre>() : null;
                if (folha != null) folha.rotulo = rotulo;
            }
            // as portas de dentro que são modelos (a da câmara fria): o kit deixou o vão sem a folha
            for (int f = 0; f < Andares; f++)
                foreach (var (modelo, lugar, giro) in MoveisDoBunker.PortasDeModelo(f))
                {
                    var porta = Por(modelo, pai, lugar, giro, 1f);
                    var folha = porta != null ? porta.GetComponentInChildren<ParteQueAbre>() : null;
                    if (folha != null) folha.rotulo = modelo == "PortaCamaraFria" ? "a porta da câmara fria" : "a porta";
                }
        }

        // os móveis dos andares (MoveisDoBunker) e os canos e os dutos no teto, peça por peça (os canos pintados pela cor da
        // corrida: o cano é a parte principal do modelo)
        void Moveis(Transform pai)
        {
            var moveis = Novo("Móveis", pai).transform;
            foreach (var m in MoveisDoBunker.Todos) Por(m.modelo, moveis, m.lugar, m.giro, m.escala);
            var canos = Novo("Canos e dutos", pai).transform;
            var pintados = new Dictionary<(string, Color), Mesh>();
            foreach (var c in MoveisDoBunker.Canos)
            {
                Vector3 a = new Vector3(c.de.x + X0, Teto(c.andar) - c.abaixoDoTeto, c.de.y + Z0);
                Vector3 b = new Vector3(c.ate.x + X0, Teto(c.andar) - c.abaixoDoTeto, c.ate.y + Z0);
                Vector3 d = b - a;
                int n = Mathf.Max(1, Mathf.FloorToInt(d.magnitude));
                float giro = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? 0f : 90f;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = a + d.normalized * (i + 0.5f);
                    var peca = Por(c.modelo, canos, p, giro, 1f);
                    if (peca != null) Pintar(peca, c.modelo, c.cor, pintados);
                    if (c.modelo == "CanoReto" && i % 2 == 1) Por("SuporteDeCano", canos, p + d.normalized * 0.5f, giro, 1f);
                }
            }
            foreach (var pc in MoveisDoBunker.Pecas)
            {
                var lugar = new Vector3(pc.ponto.x + X0, Teto(pc.andar) - pc.abaixoDoTeto, pc.ponto.y + Z0);
                var peca = Por(pc.modelo, canos, lugar, pc.giro, 1f);
                if (peca != null && pc.cor.a > 0f) Pintar(peca, pc.modelo, pc.cor, pintados);
            }
        }

        // pinta o modelo com a cor (a parte principal, no esmalte), uma malha por modelo e cor
        void Pintar(GameObject go, string modelo, Color cor, Dictionary<(string, Color), Mesh> pintados)
        {
            var filtro = go.GetComponent<MeshFilter>();
            if (filtro == null) return;
            if (!pintados.TryGetValue((modelo, cor), out var malha))
            {
                var tex = CatalogoDaMobilia.Textura(modelo == "Duto" ? "MetalEscovado" : "Esmalte");
                malha = CatalogoDaMobilia.Pintar(filtro.sharedMesh, new CatalogoDaMobilia.Variacao("", new CatalogoDaMobilia.Acabamento(tex, cor)));
                malha.hideFlags = HideFlags.DontSave;
                pintados[(modelo, cor)] = malha;
            }
            filtro.sharedMesh = malha;
        }

        // a parede dos fundadores (o quadro com o material deles) no fundo da garagem, em cima das vagas, e o painel de armas
        // (o móvel "Painel_Armas", com o nome que a montagem das armas procura para pôr a "Parede de armas") no arsenal
        void Fundadores(Transform pai)
        {
            if (fundadores != null)
            {
                // o quadro de 1 × 1 (como o Quad da Unity: a frente para -z), feito aqui (a limpeza da cena apaga as malhas
                // DontSave, e a do Quad é da Unity)
                var malha = new Mesh { name = "Quadro dos fundadores", hideFlags = HideFlags.DontSave };
                malha.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) });
                malha.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
                malha.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
                malha.SetTriangles(new[] { 0, 3, 1, 3, 0, 2 }, 0);
                malha.RecalculateBounds();
                var q = Novo("Parede dos fundadores", pai);
                q.transform.localPosition = new Vector3(20f + X0, 3.25f, Z1 - ParedeDeFora / 2 - 0.01f);
                q.transform.localScale = new Vector3(4.2f, 3.15f, 1f);
                q.AddComponent<MeshFilter>().sharedMesh = malha;
                var r = q.AddComponent<MeshRenderer>();
                r.sharedMaterial = fundadores;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (painelDeArmas != null)
            {
                var painel = Objeto("Painel_Armas", pai, painelDeArmas, null, new Vector3(3.4f + X0, 0f, Z1 - ParedeDeFora / 2), 180f);
                painel.isStatic = true;
            }
        }

        // o morro, o pátio e a cerca (os do kit, Bunker.MontarMorro), o chão em volta (sem a caixa e o pátio) e a estrada
        // de terra, da cerca para longe
        void MontarMorro()
        {
            Bunker.MontarMorro(chao);
            var caixa = Rect.MinMaxRect(X0 - ParedeDeFora / 2, Z0 - ParedeDeFora / 2, X1 + ParedeDeFora / 2, Z1 + ParedeDeFora / 2);
            chao.Laje(-160f, 160f, -160f, 160f, 0f, 0.3f, "Grama", true, new[] { Patio, caixa });
            chao.Laje(VaoDaCercaX0, VaoDaCercaX1, -160f, Patio.yMin, 0.025f, 0.1f, "Terra");
        }

        // um sedã e a caminhonete nas vagas 1 e 3, de frente para o fundo
        void CarrosNasVagas()
        {
            if (carros == null || carros.Length == 0) return;
            for (int i = 0; i < 4; i += 2)
            {
                var r = R(12.4f + i * 3.8f, 17.6f, 12.4f + (i + 1) * 3.8f, 23.6f);
                Objeto("Carro na vaga", raiz, carros[Mathf.Min(i, carros.Length - 1)], null, new Vector3(r.center.x, 0.05f, r.center.y + 0.3f), 0f);
            }
        }

        // a mata em volta, longe do morro, do pátio e da estrada
        void Arvores()
        {
            if (arvores == null || arvores.Length == 0) return;
            var rnd = new System.Random(semente);
            float pe = Morro * Talude;
            for (int i = 0; i < 220; i++)
            {
                float x = Mathf.Lerp(-110f, 110f, (float)rnd.NextDouble()), z = Mathf.Lerp(-110f, 110f, (float)rnd.NextDouble());
                bool noMorro = x > X0 - pe - 3f && x < X1 + pe + 3f && z > Z0 - pe - 3f && z < Z1 + pe + 3f;
                bool noPatio = x > -18f && x < 26f && z > -46f && z < Z0;
                bool naEstrada = x > -2f && x < 12f;
                if (noMorro || noPatio || naEstrada) continue;
                var a = Objeto("Árvore", raiz, arvores[rnd.Next(arvores.Length)], null, new Vector3(x, 0f, z), (float)rnd.NextDouble() * 360f);
                a.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.6f, (float)rnd.NextDouble());
            }
        }

        // as paradas: o pátio, o hall de cada andar (na frente da porta da escada) e o alto do morro, todas olhando para o
        // fundo (+z) e com os mesmos pisos e cômodos, para o P abrir no andar em que o jogador está (com a frente embaixo)
        void ParadasDoBunker()
        {
            var limites = new Bounds(new Vector3(0f, -5.5f, 0f), new Vector3(X1 - X0 + ParedeDeFora, 23f, Z1 - Z0 + ParedeDeFora));
            void Por(string onde, Vector3 chegada)
            {
                var p = new Parada { Ficha = $"Bunker · {onde}\n32 × 24 m por andar, 5 andares, 17 m de fundo", Chegada = chegada, Giro = 0f, Limites = limites };
                for (int f = Andares - 1; f >= 0; f--)
                {
                    p.Pisos.Add(Base(f));
                    p.NomesDosPisos.Add($"{Codigos[f]} · {NomesDosAndares[f]}");
                    var an = AndarDaPlanta(f);
                    foreach (var k in an.comodos)
                    {
                        if (k.tipo == "duto" || k.tipo == "poco") continue;
                        string nome = an.nomes[k];
                        bool passa = k.tipo == "corredor" || k.tipo == "escada";
                        p.Comodos.Add((new Vector3(k.Cx, Base(f), k.Cz), Andares - 1 - f, passa ? nome : $"{nome}\n{(k.x1 - k.x0):0.#} × {(k.z1 - k.z0):0.#} m"));
                    }
                }
                p.Pisos.Add(Morro);
                p.NomesDosPisos.Add("A casinha (o alto do morro)");
                Paradas.Add(p);
            }
            Por("o pátio", new Vector3(PortaoX0 + PortaoLargura / 2, 0.1f, -32f));
            for (int f = 0; f < Andares; f++)
                Por($"{Codigos[f]} ({NomesDosAndares[f]})", new Vector3(Escada.xMax + 1.4f, Base(f) + 0.1f, PortaDaEscadaZ));
            Por("o alto do morro", new Vector3(Escada.xMax + 3f, Morro + 0.1f, PortaDaEscadaZ - 3f));
        }

        // os andares da planta, para a tela (os nomes dos cômodos), feitos uma vez
        static Andar[] _planta;
        static Andar AndarDaPlanta(int f)
        {
            if (_planta == null)
            {
                _planta = new Andar[Andares];
                for (int i = 0; i < Andares; i++) _planta[i] = DoAndar(i);
            }
            return _planta[f];
        }

        // ------------------------------------------------------------ a tela e a luz

        public override string Texto(Vector3 pos)
        {
            string onde = "Fora do bunker";
            bool dentro = Dentro(pos);
            if (dentro && pos.y > TopoDaCaixa - 0.1f) onde = "A casinha da saída de cima";
            else if (dentro)
            {
                int f = AndarEm(pos.y + 0.2f);
                var an = AndarDaPlanta(f);
                var k = an.Em(pos.x, pos.z);
                onde = $"{Codigos[f]} · {NomesDosAndares[f]}" + (k != null ? $" · {an.nomes[k]} ({(k.x1 - k.x0):0.#} × {(k.z1 - k.z0):0.#} m)" : "");
            }
            return onde + "\n" + Resumo;
        }

        public override string AjudaDasTeclas => "L acende e apaga as lâmpadas";

        public override void Teclas(Keyboard k)
        {
            if (k.lKey.wasPressedThisFrame)
            {
                bool acesas = _lampadas.Count > 0 && !_lampadas[0].enabled;
                foreach (var l in _lampadas) if (l != null) l.enabled = acesas;
            }
        }

        /// <summary>Dentro da caixa do bunker (até a laje de cima) ou da casinha.</summary>
        public static bool Dentro(Vector3 p)
        {
            float e = ParedeDeFora / 2;
            if (p.y > TopoDaCaixa) return p.y < Morro + AlturaDaCasinha && Escada.Contains(new Vector2(p.x, p.z));
            return p.x > X0 - e && p.x < X1 + e && p.z > Z0 - e && p.z < Z1 + e && p.y > Base(Andares - 1) - 1f;
        }

        // a luz de fora guardada (a cena volta a ela fora do bunker)
        bool _guardada;
        Color _ceu, _equador, _chao;
        float _sol, _reflexo = 1f;
        bool _neblina;
        Light _luzDoSol;
        int _zona = -1;

        /// <summary>A neblina de altura de fora (a densidade no chão): a do jogo é bem mais grossa; aqui, para ver o bunker.</summary>
        [Range(0f, 0.1f)] public float neblinaDeFora = 0.012f;

        /// <summary>
        /// A luz de onde a câmera está: fora, a da cena, com a neblina de altura rala (<see cref="neblinaDeFora"/>; a cena
        /// não tem o AmbienteDoDia do jogo, que a põe); na garagem e na casinha, a luz ambiente cai para
        /// <see cref="luzNaGaragem"/> (o sol fica: o dia entra pelo portão); embaixo, a ambiente e o sol caem para
        /// <see cref="luzEmbaixo"/>. Dentro, sem neblina nenhuma (como o subsolo do jogo).
        /// </summary>
        public void Iluminar(Vector3 camera)
        {
            int zona = !Dentro(camera) ? 0 : camera.y > -GrossuraDaLaje ? 1 : 2;
            // os globais da neblina de altura (o efeito de tela do renderizador do jogo)
            Shader.SetGlobalColor("_NeblinaCor", new Color(0.63f, 0.65f, 0.67f, 1f));
            Shader.SetGlobalVector("_NeblinaParams", new Vector4(zona == 0 ? neblinaDeFora : 0f, 0.2f, 0.32f, 140f));
            if (zona == _zona) return;
            if (!_guardada)
            {
                _ceu = RenderSettings.ambientSkyColor; _equador = RenderSettings.ambientEquatorColor; _chao = RenderSettings.ambientGroundColor;
                _neblina = RenderSettings.fog;
                _reflexo = RenderSettings.reflectionIntensity;
                foreach (var l in FindObjectsByType<Light>())
                    if (l.type == LightType.Directional) { _luzDoSol = l; _sol = l.intensity; break; }
                _guardada = true;
            }
            _zona = zona;
            float k = zona == 0 ? 1f : zona == 1 ? luzNaGaragem : luzEmbaixo;
            RenderSettings.ambientSkyColor = _ceu * k;
            RenderSettings.ambientEquatorColor = _equador * k;
            RenderSettings.ambientGroundColor = _chao * k;
            // o reflexo do céu (o do dia, fixo) junto: no escuro, o azulejo ficava com um brilho cinza
            RenderSettings.reflectionIntensity = _reflexo * k;
            RenderSettings.fog = _neblina && zona == 0;
            if (_luzDoSol != null) _luzDoSol.intensity = zona == 2 ? _sol * luzEmbaixo : _sol;
        }

        /// <summary>Volta a luz de fora (depois das fotos).</summary>
        public void LuzDeFora() => Iluminar(new Vector3(0f, 100f, -200f));

        void OnApplicationQuit() => LuzDeFora();

        void Update()
        {
            if (!Application.isPlaying) return;
            var cam = Camera.main;
            if (cam != null) Iluminar(cam.transform.position);
        }
    }

    /// <summary>As duas folhas do portão abrem e fecham juntas: quando uma muda, a outra vai junto.</summary>
    public class FolhasJuntas : MonoBehaviour
    {
        public ParteQueAbre outra;
        ParteQueAbre _esta;
        bool _antes;

        void Start()
        {
            _esta = GetComponent<ParteQueAbre>();
            _antes = _esta != null && _esta.Aberta;
        }

        void Update()
        {
            if (_esta == null || outra == null || _esta.Aberta == _antes) return;
            _antes = _esta.Aberta;
            outra.Definir(_esta.Aberta);
            // a outra não repete a mudança de volta
            var dela = outra.GetComponent<FolhasJuntas>();
            if (dela != null) dela._antes = _esta.Aberta;
        }
    }
}
