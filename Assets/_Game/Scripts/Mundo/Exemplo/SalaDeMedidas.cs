using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A sala de medidas (Plano_Mapa, Parte M0): a mesma casa em 3 escalas, lado a lado, para escolher a proporção das
    /// construções andando dentro delas. A térrea (com porão, entrada por fora) fica do lado norte e o sobrado (com
    /// porão, escada por baixo da escada) do lado sul, cada escala numa coluna, com a placa do número na frente.
    /// Em cada casa, um zumbi parado no corredor e o boneco do jogador (1,80 m) na sala, para comparar com as portas e
    /// o pé-direito. As escalas são as de Medidas.Escalada (1 = as casas de hoje).
    /// Montada por Editor/MontagemDoExemploDasCasas (menu Zombie City Cleaner > Exemplos > Sala de medidas).
    /// </summary>
    [ExecuteAlways]
    public class SalaDeMedidas : CenaDeExemplo
    {
        public float[] escalas = { 1f, 1.15f, 1.3f };
        [Tooltip("O prefab do zumbi: a cópia usa só o filho \"Modelo\" (o visual com o Animator).")]
        public GameObject zumbi;
        [Tooltip("O prefab do jogador: a cópia usa só o filho \"Corpo\".")]
        public GameObject jogador;

        struct Versao { public Bounds caixa; public string texto; }
        readonly List<Versao> _versoes = new List<Versao>();

        protected override string Chave => semente + "|" + string.Join(",", escalas);

        protected override void Montar()
        {
            _versoes.Clear();
            float x = -30f;
            foreach (float k in escalas)
            {
                var m = Medidas.Escalada(k);
                // a mesma planta e o mesmo estilo nas três escalas (a semente é a mesma; só as medidas mudam)
                var terrea = PlantasDeCasa.Terrea(new System.Random(semente), m, porao: true);
                PlantasDeCasa.Estilo(new System.Random(semente + 1), terrea);
                var sobrado = PlantasDeCasa.Sobrado(new System.Random(semente + 2), m, porao: true);
                PlantasDeCasa.Estilo(new System.Random(semente + 3), sobrado);
                var ct = new Casa(terrea, m); ct.Montar();
                var cs = new Casa(sobrado, m); cs.Montar();
                // cada escala é uma coluna: as duas casas no meio dela, uma de frente para a outra
                float coluna = Mathf.Max(Largura(ct), Largura(cs)) + 3f;
                string n = k.ToString("0.00");
                PorCasa(ct, $"Térrea ×{n}", x + (coluna - 3f - Largura(ct)) / 2, 1, out var gt);
                PorCasa(cs, $"Sobrado ×{n}", x + (coluna - 3f - Largura(cs)) / 2, -1, out var gs);
                AnotarVersao(gt, ct, m, "Térrea");
                AnotarVersao(gs, cs, m, "Sobrado");
                // perto da calçada, num canto do lote (no meio, ela ficava na frente da escada da varanda)
                Placa(new Vector3(x + 1.8f, 0f, Calcada + 1.2f), 1, n);
                Placa(new Vector3(x + coluna - 1.8f, 0f, -(Calcada + 1.2f)), -1, n);
                Figuras(gt, terrea, m);
                Figuras(gs, sobrado, m);
                x += coluna;
            }
            Rua(-40f, x + 10f, 2);
            resumo.AppendLine($"Semente {semente}. R gera outra planta; a mesma nas {escalas.Length} escalas.");
        }

        /// <summary>Guarda a caixa da casa e as medidas dela, para a tela mostrar quando o jogador estiver dentro.</summary>
        void AnotarVersao(GameObject go, Casa casa, Medidas m, string molde)
        {
            var b = go.GetComponent<MeshRenderer>().bounds;
            float corredor = casa.p.comodos.Where(c => c.tipo == "corredor" && c.andar >= 0).Select(c => Mathf.Min(c.x1 - c.x0, c.z1 - c.z0)).DefaultIfEmpty(0f).Min();
            var quartos = casa.p.comodos.Where(c => c.tipo == "quarto").Select(c => $"{c.x1 - c.x0:0.0} × {c.z1 - c.z0:0.0}");
            var sala = casa.p.comodos.First(c => c.tipo == "sala");
            string texto = $"{molde} ×{m.escala:0.00}: pé-direito {m.altura:0.00} m · porta {m.porta:0.00} × {m.alturaPorta:0.00} m" +
                           $" (a da frente {m.portaFrente:0.00} × {m.alturaPortaFrente:0.00}) · janela {m.janela:0.00} × {m.topoJanela - m.peitoril:0.00} m" +
                           $"\ncorredor {corredor:0.00} m · escada {m.larguraEscada:0.00} m · sala {sala.x1 - sala.x0:0.0} × {sala.z1 - sala.z0:0.0} m" +
                           $" · quartos {string.Join(" e ", quartos)} m · casa {casa.p.W:0.0} × {casa.p.D:0.0} m";
            _versoes.Add(new Versao { caixa = b, texto = texto });
        }

        public override string Texto(Vector3 posicao)
        {
            foreach (var v in _versoes)
                if (v.caixa.Contains(posicao + Vector3.up * 0.5f)) return "Dentro da " + v.texto;
            return Resumo + "\nEntre numa casa para ver as medidas dela.";
        }

        /// <summary>O zumbi parado no corredor (ou no hall) e o boneco do jogador na sala.</summary>
        void Figuras(GameObject casa, Planta p, Medidas m)
        {
            float piso = p.porao ? 0.75f : 0.15f;
            var sala = p.comodos.First(c => c.tipo == "sala");
            var corredor = p.comodos.First(c => c.tipo == "corredor" && c.andar == 0);
            Vector3 posZumbi, posBoneco;
            if (p.molde == "Terrea")
            {
                // na porta da sala para o corredor, do lado do corredor; o boneco no meio da sala, olhando para ele
                var porta = p.ligacoes.First(l => l.a == sala && l.b == corredor);
                posZumbi = NaCasa(casa, p, porta.pos, piso, corredor.Cz);
                posBoneco = NaCasa(casa, p, porta.pos + 0.6f, piso, sala.z0 + (sala.z1 - sala.z0) * 0.45f);
            }
            else
            {
                // no hall, logo antes da escada; o boneco na sala, perto da passagem
                var lance = p.lances[0];
                posZumbi = NaCasa(casa, p, lance.x0 - 0.7f, piso, lance.zIni + 0.2f);
                posBoneco = NaCasa(casa, p, sala.x1 - 1.4f, piso, sala.z0 + 1.6f);
            }
            if (zumbi != null) Figura(zumbi, "Modelo", "Idle", posZumbi, posBoneco - posZumbi);
            if (jogador != null) Figura(jogador, "Corpo_HomemBranco", "Idle_Maos", posBoneco, posZumbi - posBoneco);   // o primeiro personagem
        }

        /// <summary>
        /// Uma cópia do visual de um prefab (o filho com o Animator e a malha), sem os scripts do jogo, parada na pose do
        /// começo do clipe (sem Play o Animator não roda; com Play, ele toca o que o controlador mandar).
        /// </summary>
        void Figura(GameObject prefab, string filho, string clipe, Vector3 pos, Vector3 frente)
        {
            var origem = prefab.transform.Find(filho);
            if (origem == null) return;
            var go = Instantiate(origem.gameObject, raiz);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
            // só o visual: as hitboxes presas aos ossos (com colisor e corpo físico) trancavam a porta em que ele fica
            foreach (var j in go.GetComponentsInChildren<Joint>(true)) DestroyImmediate(j);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(r);
            foreach (var s in go.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(s);
            frente.y = 0f;
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(frente.sqrMagnitude > 0.01f ? frente : Vector3.forward));
            var an = go.GetComponent<Animator>();
            if (an != null && an.runtimeAnimatorController != null)
            {
                var clipes = an.runtimeAnimatorController.animationClips;
                var c = clipes.FirstOrDefault(a => a.name == clipe || a.name.EndsWith("|" + clipe)) ?? clipes.FirstOrDefault(a => a.name.Contains("Idle"));
                if (c != null) c.SampleAnimation(go, Mathf.Min(0.4f, c.length * 0.25f));
            }
        }

        /// <summary>Uma placa na frente do lote, de frente para a rua, com o número da escala em algarismos de caixas.</summary>
        void Placa(Vector3 pe, int lado, string texto)
        {
            // lado 1: o lado norte, a placa olha para -z; lado -1: olha para +z
            Vector3 fora = new Vector3(0f, 0f, -lado), direita = new Vector3(lado, 0f, 0f);
            float yaw = lado > 0 ? 0f : 180f;
            chao.Caixa(pe + Vector3.up * 0.75f, new Vector3(0.1f, 1.5f, 0.1f), "Poste", yaw, colide: true);
            Vector3 meio = pe + Vector3.up * 1.85f + fora * 0.07f;
            chao.Caixa(meio, new Vector3(1.9f, 0.85f, 0.05f), "PlacaBranca", yaw);
            Algarismos(meio + fora * 0.03f, direita, 0.55f, texto, yaw);
        }

        static readonly Dictionary<char, string> Segmentos = new Dictionary<char, string>
        {
            ['0'] = "abcdef", ['1'] = "bc", ['2'] = "abged", ['3'] = "abgcd", ['4'] = "fgbc",
            ['5'] = "afgcd", ['6'] = "afgedc", ['7'] = "abc", ['8'] = "abcdefg", ['9'] = "abcdfg",
        };

        /// <summary>Algarismos de 7 segmentos (caixas finas) centrados em 'meio', escritos na direção 'direita'.</summary>
        void Algarismos(Vector3 meio, Vector3 direita, float h, string texto, float yaw)
        {
            float w = h * 0.5f, t = h * 0.14f, passo = w + h * 0.22f, virgula = t * 2.2f;
            float total = 0f;
            foreach (char ch in texto) total += ch == ',' || ch == '.' ? virgula : passo;
            float s = -total / 2;
            foreach (char ch in texto)
            {
                if (ch == ',' || ch == '.')
                {
                    Vector3 v = meio + direita * (s + virgula / 2 - h * 0.11f) + Vector3.up * (-h / 2 + t / 2);
                    chao.Caixa(v, new Vector3(t, t, 0.02f), "Poste", yaw);
                    chao.Caixa(v + Vector3.down * t * 0.9f - direita * t * 0.3f, new Vector3(t * 0.6f, t, 0.02f), "Poste", yaw);
                    s += virgula;
                    continue;
                }
                if (!Segmentos.TryGetValue(ch, out var segs)) { s += passo; continue; }
                Vector3 c = meio + direita * (s + w / 2);
                foreach (char sg in segs)
                {
                    Vector3 o; Vector3 tam;
                    switch (sg)
                    {
                        case 'a': o = new Vector3(0, h / 2 - t / 2, 0); tam = new Vector3(w, t, 0.02f); break;
                        case 'g': o = Vector3.zero; tam = new Vector3(w, t, 0.02f); break;
                        case 'd': o = new Vector3(0, -h / 2 + t / 2, 0); tam = new Vector3(w, t, 0.02f); break;
                        case 'f': o = new Vector3(-w / 2 + t / 2, h / 4, 0); tam = new Vector3(t, h / 2, 0.02f); break;
                        case 'b': o = new Vector3(w / 2 - t / 2, h / 4, 0); tam = new Vector3(t, h / 2, 0.02f); break;
                        case 'e': o = new Vector3(-w / 2 + t / 2, -h / 4, 0); tam = new Vector3(t, h / 2, 0.02f); break;
                        default: o = new Vector3(w / 2 - t / 2, -h / 4, 0); tam = new Vector3(t, h / 2, 0.02f); break;
                    }
                    chao.Caixa(c + direita * o.x + Vector3.up * o.y, tam, "Poste", yaw);
                }
                s += passo;
            }
        }
    }
}
