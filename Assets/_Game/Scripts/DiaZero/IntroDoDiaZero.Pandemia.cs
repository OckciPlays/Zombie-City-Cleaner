using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Roupas;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// O jornal da pandemia (22/11/2026, a etapa 4 do Docs/23, o D0 na Woodcreek: "a introdução nova, com o governo anunciando
    /// a possível pandemia de uma doença desconhecida, tratada como emergência sanitária, como na COVID-19. A de hoje já conta
    /// os ataques, que agora ficam para o D+30"). Com <see cref="pandemia"/> (o palco da Woodcreek), a introdução troca o
    /// roteiro, na mesma trilha e nos mesmos tempos (91 s):
    ///  - a sala e a TV ligando (o estúdio, como antes);
    ///  - o quarto do hospital no Brasil, os primeiros casos (como antes);
    ///  - o saguão do aeroporto: as máscaras, a medição da febre, o painel dos voos cancelados;
    ///  - a entrevista do governo: o púlpito, as bandeiras, a faixa da emergência, os repórteres e as câmeras;
    ///  - o mercado lotado da cidade grande: as prateleiras quase vazias, as pessoas com os carrinhos;
    ///  - de volta à TV: os primeiros casos nos Estados Unidos, no nosso estado; o fim, com as lojas da vila abertas.
    /// Sem o homem de pedra, a praça das estátuas e o ataque no corredor: o que vem depois fica para o D+30.
    /// </summary>
    public partial class IntroDoDiaZero
    {
        [Tooltip("O jornal da pandemia (o Dia 0 na Woodcreek, 22/11/2026): o anúncio do governo, sem os ataques.")]
        public bool pandemia;

        static readonly Fala[] FalasDaPandemia =
        {
            new Fala { de = 1.5f, ate = 9.5f, texto = "Era para ser só mais um sábado em casa." },
            new Fala { de = 10.5f, ate = 17f, texto = "No Brasil, os primeiros casos de uma doença que ninguém conhece: febre, olhos vermelhos e uma coceira pelo corpo todo." },
            new Fala { de = 17.2f, ate = 23.6f, texto = "Os médicos ainda não sabem a causa. Os hospitais começaram a encher." },
            new Fala { de = 24.5f, ate = 31.6f, texto = "Em poucas semanas, a doença chegou à Europa e à Ásia pelos aeroportos." },
            new Fala { de = 31.8f, ate = 39.6f, texto = "A Organização Mundial da Saúde fala em uma possível pandemia." },
            new Fala { de = 40.5f, ate = 47.6f, texto = "Hoje, o governo declarou emergência de saúde pública em todo o país." },
            new Fala { de = 47.8f, ate = 55.6f, texto = "Pediu calma, e que cada família tenha comida, água e remédios para trinta dias em casa." },
            new Fala { de = 56.5f, ate = 63.8f, texto = "Nas cidades grandes, os mercados já estão lotados." },
            new Fala { de = 64f, ate = 71.6f, texto = "Ninguém sabe ainda como a doença passa de uma pessoa para outra." },
            new Fala { de = 73f, ate = 78.6f, texto = "E hoje, os primeiros casos nos Estados Unidos." },
            new Fala { de = 78.8f, ate = 83.8f, texto = "No nosso estado." },
            new Fala { de = 85f, ate = 90.5f, texto = "As lojas da Woodcreek ainda estão abertas. Cada um tem mil dólares." },
        };

        static string FaixaDaPandemia(float t) =>
            t < 24f ? "PLANTÃO  ·  DOENÇA DESCONHECIDA SE ESPALHA PELO BRASIL"
            : t < 40f ? "OMS  ·  ALERTA DE POSSÍVEL PANDEMIA"
            : t < 56f ? "URGENTE  ·  GOVERNO DECLARA EMERGÊNCIA DE SAÚDE PÚBLICA"
            : t < 72f ? "CORRIDA AOS MERCADOS  ·  FALTAM ÁLCOOL, MÁSCARAS E ÁGUA"
            : "URGENTE  ·  PRIMEIROS CASOS NOS EUA  ·  ESTADO DE WASHINGTON";

        // o texto da faixa e as falas do roteiro de agora
        string Faixa(float t) => pandemia ? FaixaDaPandemia(t) : FaixaDoJornal(t);
        Fala[] FalasAgora => pandemia ? FalasDaPandemia : Falas;

        Transform _aeroporto, _coletiva, _mercado;

        // as cenas da pandemia (nos tempos da trilha)
        void FilmarAPandemia(float t)
        {
            AmbienteDoDia.Escurecer = 0f;
            bool naTv = t < 10f || (t >= 72f && t < 84f);
            if (_estudio != null) _estudio.enabled = naTv;
            AtualizarAFaixa(t);
            if (t < 10f) NaSala(t / 10f, false);
            else if (t < 24f) NoQuarto((t - 10f) / 14f);
            else if (t < 40f) NoAeroporto((t - 24f) / 16f);
            else if (t < 56f) NaColetiva((t - 40f) / 16f);
            else if (t < 72f) NoMercado((t - 56f) / 16f);
            else if (t < 84f) NaSala((t - 72f) / 12f, true);
            else Final(t - 84f);
        }

        void NoAeroporto(float u)
        {
            var p = _aeroporto.TransformPoint(new Vector3(Mathf.Lerp(-9f, 5f, u), 2.4f, -7.5f));
            var olhar = _aeroporto.TransformPoint(new Vector3(Mathf.Lerp(-5f, 6f, u), 1.6f, 4f));
            _cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(olhar - p));
            _cam.fieldOfView = 58f;
        }

        void NaColetiva(float u)
        {
            // do meio dos repórteres para o púlpito, chegando perto
            var p = _coletiva.TransformPoint(new Vector3(Mathf.Lerp(-1.2f, -0.4f, u), 2.05f, Mathf.Lerp(-6.5f, -3.4f, Mathf.SmoothStep(0f, 1f, u))));
            var olhar = _coletiva.TransformPoint(new Vector3(0f, 1.55f, 2.2f));
            _cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(olhar - p));
            _cam.fieldOfView = 48f;
        }

        void NoMercado(float u)
        {
            var p = _mercado.TransformPoint(new Vector3(0.2f, 1.7f, Mathf.Lerp(-9f, -1.5f, u)));
            var olhar = _mercado.TransformPoint(new Vector3(Mathf.Lerp(-0.6f, 0.6f, u), 1.1f, 6f));
            _cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(olhar - p));
            _cam.fieldOfView = 60f;
        }

        // ------------------------------------------------------------------ os cenários da pandemia

        // um NPC dos cenários: vestido pelo sorteio do tipo, com a máscara cirúrgica, se pedida, e a pose
        GameObject Pessoa(GameObject npc, Transform pai, Vector3 local, float giro, string tipo, int semente, NpcDoCinema.Pose pose,
                          bool mascara = false, string animacao = "Parado")
        {
            var a = Instantiate(npc, pai.TransformPoint(local), pai.rotation * Quaternion.Euler(0f, giro, 0f), pai);
            var v = a.GetComponent<Vestimenta>();
            if (v != null)
            {
                v.npc = "";
                v.VestirSorteado(tipo, semente);
                if (mascara)
                {
                    var pecas = v.Vestidas.Where(p => !p.nome.StartsWith("Mascara") && !p.nome.StartsWith("Bandana_Rosto")).ToList();
                    pecas.Add(new CatalogoDasRoupas.PecaVestida { nome = "Mascara_Cirurgica", cores = new[] { "#CFE3EA", "#CFE3EA", "#CFE3EA" } });
                    v.Vestir(pecas);
                }
            }
            a.AddComponent<NpcDoCinema>().Iniciar(animacao, pose);
            return a;
        }

        void MontarAPandemia(Medidas M, Material mat, GameObject npc)
        {
            // o saguão do aeroporto: o piso claro, o vidro do fundo com o céu, as colunas, o painel dos voos e o posto da febre
            {
                _aeroporto = Filho("Saguão do aeroporto", new Vector3(60f, 0f, 0f));
                var o = new Obra(61, M);
                o.Caixa(new Vector3(0f, -0.05f, 0f), new Vector3(34f, 0.1f, 22f), "Piso");
                o.Caixa(new Vector3(0f, 6.05f, 0f), new Vector3(34f, 0.1f, 22f), "ParedeInterna");
                o.Caixa(new Vector3(0f, 3f, 11f), new Vector3(34f, 6f, 0.1f), "Vidro");
                o.Caixa(new Vector3(0f, 3f, -11f), new Vector3(34f, 6f, 0.1f), "ConcretoClaro");
                o.Caixa(new Vector3(-17f, 3f, 0f), new Vector3(0.1f, 6f, 22f), "ConcretoClaro");
                o.Caixa(new Vector3(17f, 3f, 0f), new Vector3(0.1f, 6f, 22f), "ConcretoClaro");
                for (int i = -2; i <= 2; i++) o.Caixa(new Vector3(i * 6.5f, 3f, 4f), new Vector3(0.7f, 6f, 0.7f), "Concreto");
                // o painel dos voos (as linhas, umas vermelhas: cancelados)
                o.Caixa(new Vector3(-3.25f, 3.6f, 10.8f), new Vector3(5.2f, 2.4f, 0.12f), "Grade");
                for (int k = 0; k < 6; k++) o.Caixa(new Vector3(-3.25f, 4.4f - k * 0.32f, 10.72f), new Vector3(4.7f, 0.16f, 0.02f), k % 2 == 0 ? "PlacaVermelha" : "PlacaAmarela");
                // o posto da febre: a mesa e a tenda branca
                o.Caixa(new Vector3(-3f, 0.45f, -1.2f), new Vector3(1.6f, 0.9f, 0.7f), "PlacaBranca");
                o.Caixa(new Vector3(-3f, 2.3f, -1.2f), new Vector3(3.2f, 0.06f, 2.4f), "PlacaBranca");
                // as malas
                var r = new System.Random(61);
                for (int i = 0; i < 9; i++)
                    o.Caixa(new Vector3(-12f + i * 2.9f + (float)r.NextDouble(), 0.33f, -3.4f + (float)r.NextDouble() * 4f), new Vector3(0.45f, 0.66f, 0.26f), i % 3 == 0 ? "CarroAzul" : i % 3 == 1 ? "Grade" : "PlacaVermelha");
                Peca("Saguão", o, _aeroporto, mat);
                Letras(_aeroporto, new Vector3(-3.25f, 5.15f, 10.7f), new List<ModelosDaEstrada.Texto>
                {
                    new ModelosDaEstrada.Texto { Conteudo = "PARTIDAS  ·  CANCELADO", U = 0f, V = 0f, Z = 0f, Altura = 0.26f, Alinhamento = 0, Cor = ModelosDaEstrada.LetraClara },
                });
                if (npc != null)
                {
                    // a agente de saúde medindo a febre de quem chega, e a fila; os outros andando de máscara
                    Pessoa(npc, _aeroporto, new Vector3(-3f, 0f, -0.4f), 180f, "Médico", 91, NpcDoCinema.Pose.Prancheta, true);
                    for (int i = 0; i < 4; i++)
                        Pessoa(npc, _aeroporto, new Vector3(-3.1f + (i % 2) * 0.2f, 0f, -2.2f - i * 0.85f), 0f, i % 2 == 0 ? "Civil" : "Estudante", 92 + i, NpcDoCinema.Pose.Nenhuma, i != 2);
                    // (23/11/2026: quem anda, anda de verdade, de uma ponta à outra do saguão em duas faixas, uma para cada lado,
                    // longe das colunas e de quem está parado; antes andavam no mesmo lugar)
                    for (int i = 0; i < 7; i++)
                    {
                        bool anda = i % 2 == 0;
                        int faixa = (i / 2) % 2;
                        float z = anda ? (faixa == 0 ? 1.3f : 3.1f) : 2.2f, x = -10f + i * 3.4f;
                        var a = Pessoa(npc, _aeroporto, new Vector3(x, 0f, z), anda ? (faixa == 0 ? 90f : -90f) : -90f, i % 3 == 0 ? "Idoso" : "Civil", 100 + i,
                                       NpcDoCinema.Pose.Nenhuma, i % 4 != 1);
                        if (!anda) continue;
                        float sentido = faixa == 0 ? 1f : -1f;
                        a.GetComponent<NpcDoCinema>().Percorrer(_aeroporto.TransformPoint(new Vector3(-15f * sentido, 0f, z)), _aeroporto.TransformPoint(new Vector3(15f * sentido, 0f, z)),
                                                                1.25f + i * 0.04f, (x * sentido + 15f) / 30f, false);
                    }
                }
                Luzes(_aeroporto, new[] { new Vector3(-8f, 5.5f, 0f), new Vector3(0f, 5.5f, 0f), new Vector3(8f, 5.5f, 0f) }, 12f, 2.2f, new Color(1f, 0.98f, 0.92f));
            }

            // a entrevista do governo: a parede azul, a faixa da emergência, o púlpito, as bandeiras, os repórteres e as câmeras
            {
                _coletiva = Filho("Entrevista do governo", new Vector3(100f, 0f, 0f));
                var o = new Obra(71, M);
                o.Caixa(new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 14f), "Piso");
                o.Caixa(new Vector3(0f, 3.55f, 0f), new Vector3(12f, 0.1f, 14f), "ParedeInterna");
                o.Caixa(new Vector3(0f, 1.75f, 3f), new Vector3(12f, 3.5f, 0.1f), "CarroAzul");
                o.Caixa(new Vector3(0f, 1.75f, -7f), new Vector3(12f, 3.5f, 0.1f), "ParedeBege");
                o.Caixa(new Vector3(-6f, 1.75f, -2f), new Vector3(0.1f, 3.5f, 10f), "ParedeBege");
                o.Caixa(new Vector3(6f, 1.75f, -2f), new Vector3(0.1f, 3.5f, 10f), "ParedeBege");
                // o palco baixo, a faixa e o púlpito com o selo
                o.Caixa(new Vector3(0f, 0.1f, 2f), new Vector3(7f, 0.2f, 2f), "Madeira");
                o.Caixa(new Vector3(0f, 2.75f, 2.93f), new Vector3(8.6f, 0.56f, 0.04f), "PlacaVermelha");
                o.Caixa(new Vector3(0f, 0.75f, 1.6f), new Vector3(0.9f, 1.1f, 0.5f), "Madeira");
                o.Caixa(new Vector3(0f, 0.85f, 1.34f), new Vector3(0.36f, 0.36f, 0.02f), "PlacaAmarela");
                o.Caixa(new Vector3(0f, 1.36f, 1.5f), new Vector3(0.04f, 0.3f, 0.04f), "Grade");
                // as duas bandeiras nos mastros, uma de cada lado
                foreach (float x in new[] { -2.6f, 2.6f })
                {
                    o.Caixa(new Vector3(x, 1.3f, 2.5f), new Vector3(0.05f, 2.6f, 0.05f), "Metal");
                    for (int k = 0; k < 7; k++) o.Caixa(new Vector3(x + 0.55f, 2.4f - k * 0.1f, 2.5f), new Vector3(1.0f, 0.1f, 0.02f), k % 2 == 0 ? "PlacaVermelha" : "PlacaBranca");
                    o.Caixa(new Vector3(x + 0.27f, 2.29f, 2.49f), new Vector3(0.44f, 0.36f, 0.025f), "CarroAzul");
                }
                // as câmeras dos jornais nos tripés, no fundo
                for (int i = 0; i < 3; i++)
                {
                    float x = -2.4f + i * 2.4f;
                    o.Caixa(new Vector3(x, 0.75f, -5.4f), new Vector3(0.06f, 1.5f, 0.06f), "Metal");
                    o.Caixa(new Vector3(x, 1.6f, -5.35f), new Vector3(0.3f, 0.26f, 0.5f), "Grade");
                }
                Peca("Sala da entrevista", o, _coletiva, mat);
                Letras(_coletiva, new Vector3(0f, 2.75f, 2.9f), new List<ModelosDaEstrada.Texto>
                {
                    new ModelosDaEstrada.Texto { Conteudo = "EMERGÊNCIA DE SAÚDE PÚBLICA", U = 0f, V = 0f, Z = 0f, Altura = 0.24f, Alinhamento = 0, Cor = ModelosDaEstrada.LetraClara },
                });
                if (npc != null)
                {
                    // quem fala, atrás do púlpito, e os dois do governo atrás; os repórteres em pé, de frente para ele
                    Pessoa(npc, _coletiva, new Vector3(0f, 0.2f, 2.05f), 180f, "Civil", 201, NpcDoCinema.Pose.Nenhuma);
                    Pessoa(npc, _coletiva, new Vector3(-1.5f, 0.2f, 2.5f), 180f, "Médico", 202, NpcDoCinema.Pose.Prancheta);
                    Pessoa(npc, _coletiva, new Vector3(1.5f, 0.2f, 2.5f), 180f, "Policial", 203, NpcDoCinema.Pose.Nenhuma);
                    for (int i = 0; i < 9; i++)
                    {
                        float x = -2.8f + (i % 5) * 1.4f + (i >= 5 ? 0.7f : 0f), z = i < 5 ? -1.6f : -2.9f;
                        Pessoa(npc, _coletiva, new Vector3(x, 0f, z), 0f, i % 3 == 0 ? "Estudante" : "Civil", 210 + i, i % 2 == 0 ? NpcDoCinema.Pose.Prancheta : NpcDoCinema.Pose.Nenhuma, i % 3 == 1);
                    }
                }
                Luzes(_coletiva, new[] { new Vector3(-2f, 3.2f, -1f), new Vector3(2f, 3.2f, -1f), new Vector3(0f, 3.2f, 1.2f) }, 8f, 2.4f, new Color(1f, 0.97f, 0.9f));
            }

            // o mercado lotado: o corredor entre as gôndolas quase vazias, as pessoas e os carrinhos
            {
                _mercado = Filho("Mercado lotado", new Vector3(140f, 0f, 0f));
                var o = new Obra(81, M);
                o.Caixa(new Vector3(0f, -0.05f, 0f), new Vector3(16f, 0.1f, 24f), "Piso");
                o.Caixa(new Vector3(0f, 4.05f, 0f), new Vector3(16f, 0.1f, 24f), "ParedeInterna");
                o.Caixa(new Vector3(0f, 2f, 12f), new Vector3(16f, 4f, 0.1f), "ParedeBege");
                o.Caixa(new Vector3(-8f, 2f, 0f), new Vector3(0.1f, 4f, 24f), "ParedeBege");
                o.Caixa(new Vector3(8f, 2f, 0f), new Vector3(0.1f, 4f, 24f), "ParedeBege");
                Peca("Mercado", o, _mercado, mat);
                // as gôndolas (as do Dia 0), dos dois lados do corredor, com poucos produtos que sobraram
                var gondolas = new List<PontoDeMovel>();
                foreach (float x in new[] { -2.2f, 2.2f, -6f, 6f })
                    foreach (float z in new[] { -6f, 0f, 6f })
                        gondolas.Add(new PontoDeMovel { tipo = "gondola", comodo = "salao", centro = new Vector3(x, 0f, z), tamanho = new Vector3(1.0f, 1.6f, 5.4f) });
                var (vis, col, _) = PrateleirasDoDia.Desenhar(gondolas, null, 0f, 82);
                var desenho = new GameObject("Gôndolas");
                desenho.transform.SetParent(_mercado, false);
                desenho.AddComponent<MeshFilter>().sharedMesh = vis.ParaMesh("Gôndolas do mercado lotado");
                desenho.AddComponent<MeshRenderer>().sharedMaterial = mat;
                MalhaDeObra.Devolver(vis);
                MalhaDeObra.Devolver(col);
                var itens = ItensNoMundo.Instancia;
                if (itens != null)
                {
                    var r = new System.Random(83);
                    Item[] sobraram = { Item.Salgadinho, Item.Chocolate, Item.Suco, Item.RefrigeranteLata, Item.Sopa };
                    foreach (var g in gondolas)
                        for (int n = 0; n < 7; n++)
                        {
                            var item = sobraram[r.Next(sobraram.Length)];
                            var malha = itens.MalhaDe(item);
                            if (malha == null) continue;
                            float lado = r.Next(2) == 0 ? -1f : 1f, y = new[] { 0.12f, 0.61f, 1.06f }[r.Next(3)];
                            var p = g.centro + new Vector3(lado * 0.3f, y - malha.bounds.min.y + 0.005f, -2.4f + (float)r.NextDouble() * 4.8f);
                            var go = new GameObject("Produto");
                            go.transform.SetParent(_mercado, false);
                            go.transform.localPosition = p;
                            go.transform.localRotation = Quaternion.Euler(0f, lado > 0 ? 90f : -90f, 0f);
                            go.AddComponent<MeshFilter>().sharedMesh = malha;
                            go.AddComponent<MeshRenderer>().sharedMaterial = itens.MaterialDe(item);
                        }
                }
                if (npc != null)
                {
                    // as pessoas no corredor, umas correndo, outras pegando o que sobrou; os carrinhos cheios
                    // (23/11/2026: quem corre, corre de verdade, pelo corredor na direção da câmera, entre os carrinhos e as
                    // gôndolas, e volta ao fundo depois de passar por ela; antes corriam no mesmo lugar)
                    for (int i = 0; i < 8; i++)
                    {
                        float z = -6f + i * 1.7f, x = (i % 2 == 0 ? -0.5f : 0.6f);
                        bool corre = i == 2 || i == 5;
                        if (corre) x = i == 2 ? -1.45f : 1.45f;
                        var a = Pessoa(npc, _mercado, new Vector3(x, 0f, z), corre ? 180f : (i % 2 == 0 ? -90f : 90f), i % 3 == 0 ? "Idoso" : "Civil", 300 + i,
                                       NpcDoCinema.Pose.Nenhuma, i % 3 != 0);
                        if (corre)
                            a.GetComponent<NpcDoCinema>().Percorrer(_mercado.TransformPoint(new Vector3(x, 0f, 11f)), _mercado.TransformPoint(new Vector3(x, 0f, -11f)),
                                                                    i == 2 ? 3.1f : 2.7f, (11f - z) / 22f, true);
                    }
                    var c = new Obra(84, M);
                    for (int i = 0; i < 4; i++)
                    {
                        var p = new Vector3(i % 2 == 0 ? 0.9f : -0.9f, 0f, -4f + i * 2.6f);
                        c.Caixa(p + new Vector3(0f, 0.6f, 0f), new Vector3(0.55f, 0.5f, 0.85f), "Grade");
                        c.Caixa(p + new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 0.25f, 0.8f), i % 2 == 0 ? "PlacaVermelha" : "PlacaAmarela");
                    }
                    Peca("Carrinhos", c, _mercado, mat);
                }
                Luzes(_mercado, new[] { new Vector3(0f, 3.7f, -6f), new Vector3(0f, 3.7f, 0f), new Vector3(0f, 3.7f, 6f) }, 9f, 2.0f, new Color(1f, 0.99f, 0.95f));
            }
        }

        void Luzes(Transform pai, Vector3[] onde, float alcance, float forca, Color cor)
        {
            foreach (var p in onde)
            {
                var l = new GameObject("Luz").AddComponent<Light>();
                l.transform.SetParent(pai, false);
                l.transform.localPosition = p;
                l.type = LightType.Point;
                l.range = alcance;
                l.intensity = forca;
                l.color = cor;
            }
        }
    }
}
