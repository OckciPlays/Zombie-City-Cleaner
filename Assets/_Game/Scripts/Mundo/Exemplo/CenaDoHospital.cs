using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A cena de teste do Hospital Principal (o Plano_Hospital.md da pasta de arte: a parte 2, o blockout, e a parte 3, a
    /// arquitetura final), para andar por dentro e conferir antes da mobília e do abandono: o <see cref="Hospital"/> (a casca
    /// e os 4 andares) no terreno, 45 cm abaixo do térreo (o embasamento), com a praça e a varanda da marquise na frente, o
    /// estacionamento, a rua, o acesso das ambulâncias até a rampa da garagem e o pátio de serviço no fundo. Desde as
    /// referências do usuário (07/11/2026): o material do abandono (o shader desenha o tijolo aparente da fachada e as
    /// manchas, os escorridos, a umidade, o mofo e as poças), a hera subindo pelas paredes de fora, os arbustos na frente,
    /// os frades amarelos, os postes do estacionamento, a placa da vaga especial e a lixeira da entrada.
    /// As paradas (as setas do AndarNoExemplo) são a frente, o saguão de cada andar (de frente para o elevador) e a garagem;
    /// o P mostra a planta do andar em que o jogador está (↑ ↓ os outros). O hospital não tem energia (07/11/2026, a pedido):
    /// nenhuma luz acende dentro; só o dia pelas janelas e a lanterna, que o L liga e desliga (a do jogo, presa na câmera).
    /// </summary>
    [ExecuteAlways]
    public class CenaDoHospital : CenaDeExemplo
    {
        /// <summary>
        /// A luz de fora que sobra dentro (as janelas deixam entrar o dia) e no subsolo: as do jogo (o AmbienteDoDia: dentro
        /// das construções a luz não muda, e no subsolo sobram 15%). Com as lâmpadas, eram 0,3 e 0,06; sem energia, o
        /// hospital é iluminado como as casas.
        /// </summary>
        [Range(0f, 1f)] public float luzDentro = 1f, luzNoSubsolo = 0.15f;
        /// <summary>A neblina de altura de fora (a densidade no chão): a do jogo é bem mais grossa; aqui, para ver o prédio.</summary>
        [Range(0f, 0.1f)] public float neblinaDeFora = 0.012f;
        /// <summary>O grau do abandono (o _Intensidade do material, por objeto).</summary>
        [Range(0f, 1f)] public float abandono = 0.9f;
        [Tooltip("O material da vegetação (a hera e os arbustos).")]
        public Material vegetacao;
        public Mesh arbustoA, arbustoB, arbustoC, lixeira;
        [Tooltip("Os prefabs da mobília nova (Prefabs/MobiliaNova: a mobília, o lote do bunker e o do hospital).")]
        public GameObject[] mobilia;
        [Tooltip("O material das marcas do abandono (Marcas.mat: o atlas T_Marcas e o shader das marcas).")]
        public Material materialDasMarcas;

        // a lanterna (a do jogo: a força, a abertura e o alcance do LanternaJogador), presa na câmera
        const float ForcaDaLanterna = 34f, AberturaDaLanterna = 62f, AberturaInternaDaLanterna = 52f, AlcanceDaLanterna = 28f;
        static readonly Vector3 LanternaNaCamera = new Vector3(0.1f, -0.3f, 0.15f);
        Light _lanterna;
        bool _lanternaLigada = true;
        int _marcas;

        protected override void Montar()
        {
            var M = Medidas.DoJogo;
            _marcas = 0;
            var go = Novo("Hospital", raiz).transform;
            var casca = new Hospital(M, -1, semente);
            casca.MontarCasca();
            var tris = new List<string> { $"casca {casca.vis.Triangulos:N0}" };
            Parte(casca, "Hospital (casca)", go);
            Hera(casca, go);
            for (int f = 0; f < Andares; f++)
            {
                var andar = new Hospital(M, f, semente);
                andar.MontarAndar();
                tris.Add($"{Codigos[f]} {andar.vis.Triangulos:N0}");
                Parte(andar, $"Hospital {Codigos[f]} ({NomesDosAndares[f]})", go);
            }
            int moveis = Moveis(go);
            Terreno();
            Ambulancias();
            Objetos();
            Objeto("Chão (o terreno, a praça, o estacionamento e a rua)", raiz, Malha(chao.vis, "Chão"), Malha(chao.col, "Chão (colisão)"), Vector3.zero, 0f);
            Carros();
            Arvores();
            Arbustos();
            Abandono();
            Paradas.Clear();
            ParadasDoHospital();
            _zona = -1;
            LuzDeFora();
            resumo.AppendLine($"Hospital Principal (a arquitetura e a mobília): 56 × 32 m por andar, 4 andares, a garagem das ambulâncias e a entrada; " +
                              $"sem energia; {moveis} móveis; {_marcas} marcas; triângulos: {string.Join(", ", tris)} (total {triangulos:N0})");
            if (_faltam.Count > 0) resumo.AppendLine($"Faltam os modelos (monte a mobília nova): {string.Join(", ", _faltam)}");
        }

        // ------------------------------------------------------------ os móveis (a parte 4: MoveisDoHospital)

        Dictionary<string, GameObject> _prefabs;
        readonly HashSet<string> _faltam = new HashSet<string>();

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

        // os móveis de cada andar, sem a colisão do corpo (a dele está na colisão do andar: MoveisDoHospital.Caixas); as
        // partes que abrem ficam com a delas
        int Moveis(Transform go)
        {
            _prefabs = null;
            _faltam.Clear();
            int n = 0;
            for (int f = 0; f < Andares; f++)
            {
                var pai = Novo($"Móveis {Codigos[f]}", go).transform;
                foreach (var m in MoveisDoHospital.DoAndar(f))
                {
                    var pf = Prefab(m.modelo);
                    if (pf == null)
                    {
                        _faltam.Add(m.modelo);
                        continue;
                    }
                    var o = Instantiate(pf, pai);
                    o.name = m.modelo;
                    o.hideFlags = HideFlags.DontSave;
                    o.transform.SetLocalPositionAndRotation(m.lugar, Quaternion.Euler(0f, m.giro, 0f));
                    o.transform.localScale = Vector3.one * m.escala;
                    var corpo = o.GetComponent<MeshCollider>();
                    if (corpo != null) DestroyImmediate(corpo);
                    n++;
                }
            }
            foreach (var x in MoveisDoHospital.Faltam) _faltam.Add(x);
            return n;
        }

        // uma parte do hospital como objeto (com a colisão) e as marcas dela (a parte 5); as malhas voltam para o estoque
        void Parte(Hospital h, string nome, Transform pai)
        {
            Objeto(nome, pai, Malha(h.vis, nome), Malha(h.col, nome + " (colisão)"), Vector3.zero, 0f);
            triangulos += h.vis.Triangulos;
            if (h.marcas.Quadros > 0 && materialDasMarcas != null)
            {
                var go = Novo(nome + " (marcas)", pai);
                go.AddComponent<MeshFilter>().sharedMesh = h.marcas.ParaMesh(nome + " (marcas)");
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = materialDasMarcas;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                _marcas += h.marcas.Quadros;
            }
            MalhaDeObra.Devolver(h.vis);
            MalhaDeObra.Devolver(h.col);
        }

        // ------------------------------------------------------------ o terreno em volta

        static Rect Pegada => Rect.MinMaxRect(X0 - 0.1f, Z0 - 0.1f, X1 + 0.1f, Z1 + 0.1f);
        static Rect PegadaDaGaragem => Rect.MinMaxRect(GaragemDasAmbulancias.xMin - 0.1f, GaragemDasAmbulancias.yMin - 0.1f, X0, GaragemDasAmbulancias.yMax + 0.1f);
        static Rect PegadaDaEntrada => Rect.MinMaxRect(Entrada.xMin - 0.1f, Entrada.yMin - 0.1f, Entrada.xMax + 0.1f, Z0);
        /// <summary>A praça da frente, o estacionamento, o pátio das ambulâncias e o acesso dele, o pátio de serviço e a rua.</summary>
        static readonly Rect Praca = Rect.MinMaxRect(-12f, -39.5f, 12f, -16.1f), Estacionamento = Rect.MinMaxRect(14f, -39.5f, 56f, -20f),
                             PatioDasAmbulancias = Rect.MinMaxRect(-62f, -7f, -43.1f, 10f), Acesso = Rect.MinMaxRect(-62f, -39.5f, -54f, -7f),
                             PatioDeServico = Rect.MinMaxRect(2f, 16.1f, 16f, 24f), SaidaLeste = Rect.MinMaxRect(28.1f, -9f, 31f, -4f),
                             Rua = Rect.MinMaxRect(-160f, -52f, 160f, -39.5f);

        // o chão: a grama em volta (com os buracos do prédio e do que é calçado), a praça com a marquise, o estacionamento com
        // as vagas, o pátio e o acesso das ambulâncias, o pátio de serviço, a saída de emergência do leste e a rua com as calçadas
        void Terreno()
        {
            var buracos = new List<Rect> { Pegada, PegadaDaGaragem, PegadaDaEntrada, Praca, Estacionamento, PatioDasAmbulancias, Acesso, PatioDeServico, SaidaLeste, Rua };
            const float c = ChaoDeFora;
            chao.Laje(-160f, 160f, -160f, 160f, c, 0.3f, "Grama", true, buracos);
            chao.Laje(Praca.xMin, Praca.xMax, Praca.yMin, Praca.yMax, c, 0.3f, "Calcada", true, new[] { PegadaDaEntrada });
            for (float x = Praca.xMin + 2f; x < Praca.xMax; x += 2f)
                chao.Caixa(new Vector3(x, c + 0.002f, (Praca.yMin + Z0) / 2f), new Vector3(0.03f, 0.004f, Z0 - Praca.yMin), "Concreto");
            chao.Laje(Estacionamento.xMin, Estacionamento.xMax, Estacionamento.yMin, Estacionamento.yMax, c, 0.3f, "Asfalto", true);
            for (float x = Estacionamento.xMin + 1.5f; x < Estacionamento.xMax - 1f; x += 2.8f)
                foreach (float z in new[] { -23.5f, -33.5f })
                    chao.Caixa(new Vector3(x, c + 0.004f, z), new Vector3(0.12f, 0.008f, 5f), "ListraBranca");
            chao.Laje(PatioDasAmbulancias.xMin, PatioDasAmbulancias.xMax, PatioDasAmbulancias.yMin, PatioDasAmbulancias.yMax, c, 0.3f, "Asfalto", true);
            chao.Laje(Acesso.xMin, Acesso.xMax, Acesso.yMin, Acesso.yMax, c, 0.3f, "Asfalto", true);
            chao.Laje(PatioDeServico.xMin, PatioDeServico.xMax, PatioDeServico.yMin, PatioDeServico.yMax, c, 0.3f, "Concreto", true);
            chao.Laje(SaidaLeste.xMin, SaidaLeste.xMax, SaidaLeste.yMin, SaidaLeste.yMax, c, 0.3f, "Calcada", true);
            // a rua (8 m de asfalto) e as calçadas dos dois lados, com a faixa
            chao.Laje(Rua.xMin, Rua.xMax, -49.5f, -41.5f, c, 0.3f, "Asfalto", true);
            chao.Laje(Rua.xMin, Rua.xMax, -41.5f, Rua.yMax, c, 0.3f, "Calcada", true);
            chao.Laje(Rua.xMin, Rua.xMax, Rua.yMin, -49.5f, c, 0.3f, "Calcada", true);
            for (float x = Rua.xMin; x < Rua.xMax; x += 6f) chao.Caixa(new Vector3(x, c + 0.004f, -45.5f), new Vector3(3f, 0.008f, 0.15f), "Faixa");
            // a caçamba e os contêineres do lixo no pátio de serviço
            chao.Caixa(new Vector3(12.5f, c + 0.9f, 21f), new Vector3(3.6f, 1.8f, 2f), "Cacamba", colide: true);
            chao.Caixa(new Vector3(4.5f, c + 0.6f, 22.4f), new Vector3(1.4f, 1.2f, 1.1f), "Lixeira", colide: true);
            chao.Caixa(new Vector3(6.2f, c + 0.6f, 22.4f), new Vector3(1.4f, 1.2f, 1.1f), "Lixeira", colide: true);
        }

        // as ambulâncias (caixas do tamanho de uma, por enquanto sem o modelo): duas nas vagas 2 e 3 da garagem, de frente para
        // os portões, e uma no pátio, largada atravessada
        void Ambulancias()
        {
            void Uma(Vector3 meio, float giro)
            {
                var M = Matrix4x4.TRS(meio, Quaternion.Euler(0f, giro, 0f), Vector3.one);
                chao.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 1.55f, 0.7f)), new Vector3(2.3f, 2.5f, 4.1f), "CarroBranco", colide: true);
                chao.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 1.15f, -2.05f)), new Vector3(2.2f, 1.7f, 1.6f), "CarroBranco", colide: true);
                chao.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 1.55f, 0.7f)), new Vector3(2.32f, 0.3f, 4.12f), "PlacaVermelha");
                chao.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 2.05f, -2.0f)), new Vector3(1.9f, 0.6f, 0.9f), "VidroCarro");
                foreach (float x in new[] { -1.0f, 1.0f })
                    foreach (float z in new[] { -2.1f, 1.6f })
                        chao.CaixaM(M * Matrix4x4.Translate(new Vector3(x, 0.42f, z)), new Vector3(0.32f, 0.84f, 0.84f), "Pneu");
            }
            // (o comprimento ao longo do z local, a cabine em -z; giro 90: a cabine para -x, os portões)
            Uma(new Vector3(GaragemDasAmbulancias.xMin + 5.2f, 0f, Vagas[1] + Z0), 90f);
            Uma(new Vector3(GaragemDasAmbulancias.xMin + 5.2f, 0f, Vagas[2] + Z0), 90f);
            Uma(new Vector3(-56f, ChaoDeFora, 2f), 220f);
        }

        void Carros()
        {
            if (carros == null || carros.Length == 0) return;
            var rnd = new System.Random(semente);
            for (int i = 0; i < 9; i++)
            {
                if (rnd.NextDouble() < 0.4) continue;
                float x = Estacionamento.xMin + 2.9f + i * 2.8f * 1.5f;
                if (x > Estacionamento.xMax - 2f) break;
                Objeto("Carro no estacionamento", raiz, carros[rnd.Next(carros.Length)], null, new Vector3(x, ChaoDeFora + 0.05f, i % 2 == 0 ? -23.5f : -33.5f), i % 2 == 0 ? 180f : 0f);
            }
            Objeto("Carro na rua", raiz, carros[rnd.Next(carros.Length)], null, new Vector3(-20f, ChaoDeFora, -47.5f), 90f);
            Objeto("Carro na rua", raiz, carros[rnd.Next(carros.Length)], null, new Vector3(36f, ChaoDeFora, -43.5f), 260f);
        }

        // a mata em volta, longe do prédio, do que é calçado e da rua
        void Arvores()
        {
            if (arvores == null || arvores.Length == 0) return;
            var rnd = new System.Random(semente);
            for (int i = 0; i < 260; i++)
            {
                float x = Mathf.Lerp(-130f, 130f, (float)rnd.NextDouble()), z = Mathf.Lerp(-130f, 130f, (float)rnd.NextDouble());
                var p = new Vector2(x, z);
                bool longe = true;
                foreach (var r in new[] { Pegada, PegadaDaGaragem, Praca, Estacionamento, PatioDasAmbulancias, Acesso, PatioDeServico, Rua })
                    if (Rect.MinMaxRect(r.xMin - 5f, r.yMin - 5f, r.xMax + 5f, r.yMax + 5f).Contains(p)) longe = false;
                if (!longe) continue;
                var a = Objeto("Árvore", raiz, arvores[rnd.Next(arvores.Length)], null, new Vector3(x, ChaoDeFora, z), (float)rnd.NextDouble() * 360f);
                a.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.6f, (float)rnd.NextDouble());
            }
        }

        // ------------------------------------------------------------ o abandono, a hera e os objetos de fora

        // a hera nas paredes de fora da casca (até o alto do 3º andar), com o material da vegetação (o vento)
        void Hera(Obra obra, Transform pai)
        {
            if (vegetacao == null) return;
            var hera = AbandonoDaObra.Hera(obra, abandono, semente + 5, 11f);
            if (hera.Folhas == 0) return;
            var h = Novo("Hera", pai);
            var m = hera.ParaMesh("Hera", false);
            m.hideFlags = HideFlags.DontSave;
            h.AddComponent<MeshFilter>().sharedMesh = m;
            var r = h.AddComponent<MeshRenderer>();
            r.sharedMaterial = vegetacao;
            // a normal das folhas um pouco para cima (como a grama): de perto, as do lado da sombra saíam pretas
            var bloco = new MaterialPropertyBlock();
            bloco.SetFloat("_Cima", 0.6f);
            r.SetPropertyBlock(bloco);
            triangulos += hera.Folhas * 2;
        }

        // o grau do abandono e a altura do chão de fora em todos os objetos com o material do abandono; por dentro (os
        // andares), 60% do de fora: com o grau todo, a pintura verde descascava em manchas que pareciam camuflagem
        void Abandono()
        {
            var fora = new MaterialPropertyBlock();
            fora.SetFloat("_Intensidade", abandono);
            fora.SetFloat("_Chao", ChaoDeFora);
            var dentro = new MaterialPropertyBlock();
            dentro.SetFloat("_Intensidade", abandono * 0.6f);
            dentro.SetFloat("_Chao", ChaoDeFora);
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.sharedMaterial != material) continue;
                string n = r.gameObject.name;
                r.SetPropertyBlock(n.StartsWith("Hospital ") && !n.Contains("casca") ? dentro : fora);
            }
        }

        // os arbustos nos canteiros da frente (dos lados da varanda) e do lado leste, com o material da vegetação
        void Arbustos()
        {
            var malhas = new List<Mesh>();
            foreach (var m in new[] { arbustoA, arbustoB, arbustoC }) if (m != null) malhas.Add(m);
            if (malhas.Count == 0 || vegetacao == null) return;
            var rnd = new System.Random(semente + 11);
            void Um(float x, float z)
            {
                var a = Objeto("Arbusto", raiz, malhas[rnd.Next(malhas.Count)], null, new Vector3(x, ChaoDeFora, z), (float)rnd.NextDouble() * 360f);
                a.GetComponent<MeshRenderer>().sharedMaterial = vegetacao;
                a.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.35f, (float)rnd.NextDouble());
            }
            foreach (float s in new[] { -1f, 1f })
                for (float x = 10f; x < 27f; x += 2.2f)
                    if (rnd.NextDouble() < 0.8) Um(s * (x + (float)rnd.NextDouble() * 0.8f), Z0 - 1.1f - (float)rnd.NextDouble() * 0.9f);
            for (float z = -2f; z < 15f; z += 2.6f)
                if (rnd.NextDouble() < 0.6) Um(X1 + 1.2f + (float)rnd.NextDouble() * 0.8f, z);
        }

        // os frades amarelos (na frente dos degraus e dos lados da rampa das ambulâncias), os postes do estacionamento
        // (apagados: não há luz), a placa da vaga especial ao pé da rampa da varanda e a lixeira da entrada
        void Objetos()
        {
            void Frade(float x, float z)
            {
                chao.Cilindro(new Vector3(x, ChaoDeFora, z), 0.1f, 0.1f, 0.9f, 8, "PlacaAmarela");
                chao.Cilindro(new Vector3(x, ChaoDeFora + 0.9f, z), 0.1f, 0.06f, 0.06f, 8, "PlacaAmarela");
                chao.Caixa(new Vector3(x, ChaoDeFora + 0.45f, z), new Vector3(0.2f, 0.9f, 0.2f), "Concreto", colide: true, visual: false);
            }
            foreach (float x in new[] { -3.3f, -1.1f, 1.1f, 3.3f }) Frade(x, Hospital.Varanda.yMin - 1.4f);
            var g = GaragemDasAmbulancias;
            foreach (float z in new[] { g.yMin - 0.9f, g.yMax + 0.9f })
                foreach (float x in new[] { g.xMin - 1.2f, g.xMin - 3.4f, g.xMin - 5.6f }) Frade(x, z);
            if (poste != null)
                foreach (float x in new[] { 20f, 34f, 48f })
                    Objeto("Poste", raiz, poste, null, new Vector3(x, ChaoDeFora, Estacionamento.yMax - 0.6f), 180f);
            // a placa da vaga especial: o poste e a placa azul
            var pv = new Vector3(Hospital.Varanda.xMax + Hospital.LarguraDaRampa + 1.0f, ChaoDeFora, Hospital.Varanda.yMin - 1.0f);
            chao.Cilindro(pv, 0.03f, 0.03f, 2.2f, 6, "Metal");
            chao.Caixa(pv + new Vector3(0f, 1.95f, -0.035f), new Vector3(0.32f, 0.42f, 0.02f), "CarroAzul");
            chao.Caixa(pv + new Vector3(0f, 1.97f, -0.047f), new Vector3(0.12f, 0.16f, 0.006f), "PlacaBranca");
            if (lixeira != null) Objeto("Lixeira", raiz, lixeira, null, new Vector3(Entrada.xMax + 1.4f, 0f, Entrada.yMin - 0.7f), 0f);
        }

        // ------------------------------------------------------------ as paradas e a planta

        // as paradas: a frente, o saguão de cada andar (de frente para a porta do elevador) e a garagem; todas com os mesmos
        // pisos e cômodos, para o P abrir no andar em que o jogador está
        void ParadasDoHospital()
        {
            var limites = new Bounds(new Vector3((GaragemDasAmbulancias.xMin + X1) / 2f, 2f, (Marquise.yMin + Z1) / 2f),
                                     new Vector3(X1 - GaragemDasAmbulancias.xMin + 1f, 18f, Z1 - Marquise.yMin + 1f));
            void Por(string onde, Vector3 chegada, float giro)
            {
                var p = new Parada { Ficha = $"Hospital Principal · {onde}\n56 × 32 m por andar, 4 andares (a arquitetura)", Chegada = chegada, Giro = giro, Limites = limites };
                for (int f = 0; f < Andares; f++)
                {
                    p.Pisos.Add(Base(f));
                    p.NomesDosPisos.Add($"{Codigos[f]} · {NomesDosAndares[f]}");
                    foreach (var (centro, rotulo) in Rotulos(f)) p.Comodos.Add((centro, f, rotulo));
                }
                Paradas.Add(p);
            }
            Por("a frente", new Vector3(0f, ChaoDeFora + 0.1f, -36f), 0f);
            for (int f = 0; f < Andares; f++)
                Por($"{Codigos[f]} ({NomesDosAndares[f]})", new Vector3(Elevador.center.x, Base(f) + 0.1f, Elevador.yMin - 1.0f), 0f);
            Por("a garagem das ambulâncias", new Vector3(GaragemDasAmbulancias.xMin - 8f, ChaoDeFora + 0.1f, Vagas[0] + Z0), 90f);
        }

        /// <summary>O meio (no chão) e o rótulo de cada cômodo do andar f que vai na planta: o nome e o tamanho.</summary>
        public static IEnumerable<(Vector3 centro, string rotulo)> Rotulos(int f)
        {
            var an = AndarDaPlanta(f);
            foreach (var k in an.comodos)
            {
                if (an.semRotulo.Contains(k) || k.tipo == "elevador") continue;
                string nome = an.nomes[k];
                bool passa = k.tipo == "corredor" || k.tipo == "escada";
                yield return (new Vector3(k.Cx, Base(f), k.Cz), passa ? nome : $"{nome}\n{(k.x1 - k.x0):0.#} × {(k.z1 - k.z0):0.#} m");
            }
        }

        // os andares da planta, para a tela (os nomes dos cômodos), feitos uma vez
        static Andar[] _planta;
        public static Andar AndarDaPlanta(int f)
        {
            if (_planta == null)
            {
                _planta = new Andar[Andares];
                for (int i = 0; i < Andares; i++) _planta[i] = DoAndar(i);
            }
            return _planta[f];
        }

        /// <summary>O cômodo em que fica um ponto (e o andar dele), ou null fora do hospital.</summary>
        public static Comodo Onde(Vector3 p, out int f)
        {
            f = AndarEm(p.y + 0.2f);
            if (p.y < TopoDaCobertura && p.y > -GrossuraDaLaje)
                foreach (var k in AndarDaPlanta(Terreo).comodos)
                    if (Anexo(k) && k.Contem(p.x, p.z) && p.y < (k.tipo == "garagem" ? TopoDaGaragem : TopoDaEntrada)) { f = Terreo; return k; }
            if (p.x < X0 || p.x > X1 || p.z < Z0 || p.z > Z1 || p.y > TopoDaCobertura || p.y < Base(0) - 1f) return null;
            var c = AndarDaPlanta(f).Em(p.x, p.z);
            return c != null && Anexo(c) ? null : c;
        }

        // ------------------------------------------------------------ a tela e a luz

        public override string Texto(Vector3 pos)
        {
            var k = Onde(pos, out int f);
            string onde = k == null ? "Fora do hospital"
                        : $"{Codigos[f]} · {AndarDaPlanta(f).nomes[k]} ({(k.x1 - k.x0):0.#} × {(k.z1 - k.z0):0.#} m)";
            return onde + "\n" + Resumo;
        }

        public override string AjudaDasTeclas => "L liga e desliga a lanterna (o hospital não tem energia) · J toca um som do hospital";

        public override void Teclas(Keyboard k)
        {
            if (k.lKey.wasPressedThisFrame) _lanternaLigada = !_lanternaLigada;
            // os sons (a parte 6): o AmbienteSonoro fica no objeto "Sons do hospital" (do assembly do jogo: daqui, pelo nome)
            if (k.jKey.wasPressedThisFrame) GameObject.Find("Sons do hospital")?.SendMessage("TocarUmSom", SendMessageOptions.DontRequireReceiver);
        }

        // um facho como o da lanterna do jogo (sem o desenho do refletor)
        static Light Facho(GameObject go)
        {
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.intensity = ForcaDaLanterna;
            l.spotAngle = AberturaDaLanterna;
            l.innerSpotAngle = AberturaInternaDaLanterna;
            l.range = AlcanceDaLanterna;
            l.color = new Color(1f, 0.96f, 0.88f);
            l.shadows = LightShadows.Soft;
            l.shadowNearPlane = 0.2f;
            return l;
        }

        /// <summary>A lanterna das fotos: em 'pos' (um pouco abaixo e à direita do olho), apontada para 'alvo'. Quem pede destrói.</summary>
        public static Light LanternaDaFoto(Vector3 pos, Vector3 alvo)
        {
            var go = new GameObject("Lanterna da foto") { hideFlags = HideFlags.HideAndDontSave };
            var giro = Quaternion.LookRotation(alvo - pos);
            go.transform.SetPositionAndRotation(pos + giro * LanternaNaCamera, giro);
            return Facho(go);
        }

        /// <summary>Dentro do hospital (no corpo, até a cobertura, ou num anexo).</summary>
        public static bool Dentro(Vector3 p) => Onde(p, out _) != null ||
            (p.x > X0 - 0.1f && p.x < X1 + 0.1f && p.z > Z0 - 0.1f && p.z < Z1 + 0.1f && p.y < TopoDaCobertura && p.y > Base(0) - 1f);

        // a luz de fora guardada (a cena volta a ela fora do hospital)
        bool _guardada;
        Color _ceu, _equador, _chao;
        float _sol, _reflexo = 1f;
        bool _neblina;
        Light _luzDoSol;
        int _zona = -1;

        /// <summary>
        /// A luz de onde a câmera está: fora, a da cena, com a neblina de altura rala; dentro, a luz ambiente cai para
        /// <see cref="luzDentro"/> (o sol fica: as janelas deixam entrar o dia) e, no subsolo, a ambiente e o sol caem para
        /// <see cref="luzNoSubsolo"/>; sem neblina dentro. Lâmpada nenhuma: o hospital não tem energia.
        /// </summary>
        public void Iluminar(Vector3 camera)
        {
            bool dentro = Dentro(camera);
            int zona = !dentro ? 0 : camera.y > -GrossuraDaLaje ? 1 : 2;
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
            float k = zona == 0 ? 1f : zona == 1 ? luzDentro : luzNoSubsolo;
            RenderSettings.ambientSkyColor = _ceu * k;
            RenderSettings.ambientEquatorColor = _equador * k;
            RenderSettings.ambientGroundColor = _chao * k;
            // o reflexo do céu (o do dia, fixo) junto: no escuro, o azulejo ficava com um brilho cinza
            RenderSettings.reflectionIntensity = _reflexo * k;
            RenderSettings.fog = _neblina && zona == 0;
            if (_luzDoSol != null) _luzDoSol.intensity = zona == 2 ? _sol * luzNoSubsolo : _sol;
        }

        /// <summary>Volta a luz de fora (depois das fotos).</summary>
        public void LuzDeFora() => Iluminar(new Vector3(0f, 100f, -200f));

        void OnApplicationQuit() => LuzDeFora();

        void Update()
        {
            if (!Application.isPlaying) return;
            var cam = Camera.main;
            if (cam == null) return;
            Iluminar(cam.transform.position);
            if (_lanterna == null)
            {
                var go = new GameObject("Lanterna") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(cam.transform, false);
                go.transform.localPosition = LanternaNaCamera;
                _lanterna = Facho(go);
            }
            _lanterna.enabled = _lanternaLigada;
        }
    }
}
