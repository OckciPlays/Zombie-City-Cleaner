using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// As lojas da Woodcreek no Dia 0 (22/11/2026, a etapa 4 do Docs/23: "o mercado, a farmácia, a loja de armas, a de roupas e
    /// o posto"). Qual loja vende o quê sai do tipo da planta de cada lote (<see cref="LojaDoLote"/>): o mercado e a farmácia
    /// são eles mesmos; as duas 24 horas vendem como a conveniência do posto; das lojas comuns, a primeira é a de roupas e a
    /// segunda a de ferragens e autopeças, e as outras ficam fechadas; o posto é o posto; a loja de armas nova é a de armas (a
    /// pistola a $1.000). Quando a obra de uma loja que vende entra na cena (perto de alguém), o <see cref="LakewoodNaCena"/>
    /// chama <see cref="AbrirALoja"/>: as prateleiras do Dia 0 (<see cref="PrateleirasDoDia"/>, sem os itens que vêm nos
    /// modelos), o caixa e os produtos (<see cref="LojaDoDiaZero"/>). A obra que sai leva a loja junto; ela abre de novo quando
    /// volta.
    /// </summary>
    public partial class WoodcreekDoDiaZero
    {
        // as lojas que vendem, pelo número do lote
        readonly Dictionary<int, LojaDoDia> _abertas = new Dictionary<int, LojaDoDia>();
        // as plantas do mapa que ganharam o nome na fachada (e o nome de antes, devolvido quando o palco sai)
        readonly List<(PlantaDeLoja planta, string antes)> _nomesNasPlantas = new List<(PlantaDeLoja, string)>();
        readonly HashSet<PlantaDeLoja> _comPlaca = new HashSet<PlantaDeLoja>();

        /// <summary>A loja do Dia 0 do lote (o que ela vende), ou null: fechada (ou não é loja).</summary>
        public LojaDoDia? LojaDoLote(int lote) => _abertas.TryGetValue(lote, out var l) ? l : (LojaDoDia?)null;

        /// <summary>As lojas que vendem: o nome, a rua na frente de cada uma (no mundo) e o giro de quem olha para ela (o F1).</summary>
        public IEnumerable<(string nome, Vector3 ponto, float giro)> FrentesDasLojas()
        {
            var lw = LakewoodNaCena.Instancia;
            if (lw == null) yield break;
            foreach (var l in lw.ChaoNoMapa.Lotes)
            {
                if (!_abertas.TryGetValue(l.Indice, out var tipo)) continue;
                var f = l.Lote.MeioDaFrente + l.Lote.Frente * 4f;
                var ponto = OrigemDeLakewood.NoMundo(new Vector3(f.x, lw.ChaoNoMapa.Altura(f.x, f.y) + 0.3f, f.y));
                var olhar = -l.Lote.Frente;
                yield return (PrecosDoDiaZero.Nome(tipo), ponto, Mathf.Atan2(olhar.x, olhar.y) * Mathf.Rad2Deg);
            }
        }

        /// <summary>
        /// Cada loja que vende e o meio da frente dela no mundo (24/11/2026, para a conversa dos NPCs: "onde ficam as lojas?").
        /// Vale também para as lojas ainda não montadas (longe de todos).
        /// </summary>
        public IEnumerable<(LojaDoDia tipo, Vector3 ponto)> LojasNoMapa()
        {
            var lw = LakewoodNaCena.Instancia;
            if (lw == null) yield break;
            foreach (var l in lw.ChaoNoMapa.Lotes)
            {
                if (!_abertas.TryGetValue(l.Indice, out var tipo)) continue;
                var f = l.Lote.MeioDaFrente;
                yield return (tipo, OrigemDeLakewood.NoMundo(new Vector3(f.x, lw.ChaoNoMapa.Altura(f.x, f.y), f.y)));
            }
        }

        // qual loja do Dia 0 cada lote de loja é (os lotes em ordem: o mesmo em todas as máquinas)
        void SortearAsLojas(ChaoDeLakewood chao)
        {
            _abertas.Clear();
            int comuns = 0;
            foreach (var l in chao.Lotes)
            {
                var plano = l.Plano;
                if (plano == null) continue;
                switch (plano.Uso)
                {
                    case UsoDoLote.Posto: _abertas[l.Indice] = LojaDoDia.Posto; break;
                    case UsoDoLote.LojaDeArmas: _abertas[l.Indice] = LojaDoDia.Armas; break;
                    case UsoDoLote.Loja when plano.Loja != null:
                        switch (plano.Loja.tipo)
                        {
                            case TipoDeLoja.Mercado: _abertas[l.Indice] = LojaDoDia.Mercado; break;
                            case TipoDeLoja.Farmacia: _abertas[l.Indice] = LojaDoDia.Farmacia; break;
                            case TipoDeLoja.Armas: _abertas[l.Indice] = LojaDoDia.Armas; break;
                            case TipoDeLoja.VinteQuatroHoras:
                            case TipoDeLoja.Conveniencia: _abertas[l.Indice] = LojaDoDia.Posto; break;
                            default:
                                if (comuns == 0) _abertas[l.Indice] = LojaDoDia.Roupas;
                                else if (comuns == 1) _abertas[l.Indice] = LojaDoDia.Ferragens;
                                comuns++;
                                break;
                        }
                        if (_abertas.TryGetValue(l.Indice, out var aberta)) DarONome(plano.Loja, aberta, l.Lote.semente);
                        break;
                }
            }
        }

        // (24/11/2026, o Pedro: "sem nome na sua fachada") o nome da loja na planta, antes de a obra ser montada: com ele, o
        // kit deixa a placa lisa (sem as letras de bloco) e o AbrirALoja escreve o letreiro de verdade por cima. Só nas que vêm
        // sem nome; o nome de antes volta quando o palco sai (a planta é a do mapa, a mesma da loja abandonada de depois).
        void DarONome(PlantaDeLoja p, LojaDoDia tipo, int semente)
        {
            if (p == null || !string.IsNullOrWhiteSpace(p.nome)) return;
            _nomesNasPlantas.Add((p, p.nome));
            p.nome = NomeDaFachada(tipo, semente);
            _comPlaca.Add(p);
        }

        protected override void OnDestroy()
        {
            foreach (var (planta, antes) in _nomesNasPlantas)
                if (planta != null) planta.nome = antes;
            _nomesNasPlantas.Clear();
            _comPlaca.Clear();
            base.OnDestroy();
        }

        /// <summary>
        /// A obra de uma loja entrou na cena (LakewoodNaCena.PorAObra, a etapa dos móveis): se ela vende no Dia 0, ganha as
        /// prateleiras, o caixa e os produtos (no objeto da obra).
        /// </summary>
        public void AbrirALoja(GameObject go, ChaoDeLakewood.LoteNoChao lote, PlanoDaObra plano, ObraPronta pronta, MalhasDasCasas.CasaPronta c)
        {
            if (!Pronta || go == null || lote == null || !_abertas.TryGetValue(lote.Indice, out var tipo)) return;
            var loja = go.AddComponent<LojaDoDiaZero>();
            switch (plano.Uso)
            {
                case UsoDoLote.Loja:
                {
                    var p = plano.Loja;
                    // os pontos na planta (os da obra vêm espelhados), o caixa ao lado da porta nas que não têm (desenhado aqui)
                    var naPlanta = new List<PontoDeMovel>();
                    foreach (var pm in c.Moveis) naPlanta.Add(p.espelhar ? pm.Espelhado() : pm);
                    var balcoes = new List<Rect>(p.balcoes);
                    var novos = new List<Rect>();
                    if (balcoes.Count == 0)
                    {
                        var r = BalcaoPertoDaPorta(p);
                        if (r.HasValue)
                        {
                            balcoes.Add(r.Value);
                            novos.Add(p.espelhar ? Rect.MinMaxRect(-r.Value.xMax, r.Value.yMin, -r.Value.xMin, r.Value.yMax) : r.Value);
                        }
                    }
                    // as gôndolas do meio do salão (na planta; espelhadas para o desenho, como os pontos da obra)
                    var doMeio = GondolasDoMeio(p, naPlanta, balcoes, tipo == LojaDoDia.Roupas);
                    naPlanta.AddRange(doMeio);
                    var desenhar = new List<PontoDeMovel>(c.Moveis);
                    foreach (var g in doMeio) desenhar.Add(p.espelhar ? g.Espelhado() : g);
                    Prateleiras(go, PrateleirasDoDia.Desenhar(desenhar, novos, PlantaDeLoja.Piso, p.semente + 31));
                    loja.Montar(this, tipo, p, naPlanta, Matrix4x4.identity, p.espelhar, new Bounds(new Vector3(0f, 2f, 0f), new Vector3(p.W, 4f, p.D)), balcoes);
                    // o nome na placa da fachada (24/11/2026, o Pedro: "sem nome na sua fachada"; a planta ganhou o nome no
                    // SortearAsLojas, antes da obra: a placa dela ficou lisa, sem as letras de bloco)
                    if (_comPlaca.Contains(p)) Placa(go.transform, p);
                    break;
                }
                case UsoDoLote.Posto:
                {
                    // a planta da loja do posto (a mesma do ModelosDaEstrada.Posto), na matriz dele; os pontos voltam para ela
                    var planta = PlantasDeLoja.DoPosto(new System.Random(plano.Posto + 11), Medidas.DoJogo);
                    var paraOPosto = PlantaDoPosto;
                    var naPlanta = new List<PontoDeMovel>();
                    foreach (var pm in c.Moveis)
                    {
                        var q = pm;
                        q.centro = paraOPosto.inverse.MultiplyPoint3x4(q.centro);
                        q.giro -= 180f;
                        if (planta.espelhar) q = q.Espelhado();
                        naPlanta.Add(q);
                    }
                    Prateleiras(go, PrateleirasDoDia.Desenhar(c.Moveis, null, 0f, planta.semente + 31));
                    var area = new Bounds(paraOPosto.MultiplyPoint3x4(new Vector3(0f, 2f, 0f)), new Vector3(planta.W, 4f, planta.D));
                    loja.Montar(this, LojaDoDia.Posto, planta, naPlanta, paraOPosto, planta.espelhar, area);
                    break;
                }
                case UsoDoLote.LojaDeArmas:
                {
                    const float x0 = PlantaDaLojaDeArmas.X0, x1 = PlantaDaLojaDeArmas.X1, z0 = PlantaDaLojaDeArmas.Z0, z1 = PlantaDaLojaDeArmas.Z1;
                    var area = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(x1 - x0, 4f, z1 - z0));
                    var salao = Rect.MinMaxRect(PlantaDaLojaDeArmas.X(0.14f), PlantaDaLojaDeArmas.Z(0.14f), PlantaDaLojaDeArmas.X(15.46f), PlantaDaLojaDeArmas.Z(21.86f));
                    loja.MontarNaLojaDeArmas(this, pronta.MoveisDaLoja, area, salao, 3.2f);
                    PlacaDaLojaDeArmas(go.transform);
                    break;
                }
                default:
                    Destroy(loja);
                    return;
            }
            Lojas.Add(loja);
            Debug.Log($"[Dia 0] {loja.Nome} aberta no lote {lote.Indice} ({plano.Nome}): {loja.Mostrados} produtos à mostra{(loja.SemLugar > 0 ? $", {loja.SemLugar} sem lugar" : "")}.");
        }

        /// <summary>
        /// O nome da loja na fachada (24/11/2026, o Pedro: "Hoje acho que estão muito simples, sem nome na sua faixada"): um
        /// nome de comércio de vila, sorteado pela semente do lote, entre os do tipo.
        /// </summary>
        public static string NomeDaFachada(LojaDoDia tipo, int semente)
        {
            string[] nomes = tipo switch
            {
                LojaDoDia.Mercado => new[] { "MERCADO WOODCREEK", "MERCADO DO LAGO", "MERCADINHO PINE" },
                LojaDoDia.Farmacia => new[] { "FARMÁCIA WOODCREEK", "FARMÁCIA DO LAGO" },
                LojaDoDia.Ferragens => new[] { "FERRAGENS E AUTOPEÇAS", "FERRAGENS DO HANK" },
                LojaDoDia.Roupas => new[] { "ROUPAS & CIA", "LOJA DA ANN" },
                LojaDoDia.Armas => new[] { "ARMAS E MUNIÇÃO" },
                _ => new[] { "CONVENIÊNCIA 24H" },
            };
            return nomes[(int)((uint)semente % (uint)nomes.Length)];
        }

        // a placa da loja de armas (a planta dela não tem a placa do kit): o quadro vermelho em cima da porta, com o nome
        void PlacaDaLojaDeArmas(Transform obra)
        {
            var placa = new GameObject("Placa da fachada").transform;
            placa.SetParent(obra, false);
            placa.localPosition = new Vector3(0f, (PlantaDaLojaDeArmas.PeDireito + PlantaDaLojaDeArmas.AltoDaFachada) * 0.5f, PlantaDaLojaDeArmas.Z0 - 0.32f);
            placa.localRotation = Quaternion.Euler(0f, 180f, 0f);   // (o texto olha para o +z: a frente da loja é o -z dela)
            var o = new Obra(31, Medidas.DoJogo);
            o.Caixa(new Vector3(0f, 0f, -0.06f), new Vector3(7.2f, 1.05f, 0.1f), "PlacaVermelha");
            o.Caixa(new Vector3(0f, 0f, -0.115f), new Vector3(7.45f, 1.3f, 0.02f), "Metal");
            var malha = new GameObject("Quadro");
            malha.transform.SetParent(placa, false);
            malha.AddComponent<MeshFilter>().sharedMesh = o.vis.ParaMesh("Placa da loja de armas");
            malha.AddComponent<MeshRenderer>().sharedMaterial = material;
            MalhaDeObra.Devolver(o.vis);
            MalhaDeObra.Devolver(o.col);
            Escrever(placa, new Vector3(0f, -0.02f, 0.005f), NomeDaFachada(LojaDoDia.Armas, 0), 0.48f, ModelosDaEstrada.LetraClara);
        }

        /// <summary>A loja do posto no espaço do posto (o ModelosDaEstrada.Posto: a frente dela para as bombas).</summary>
        static Matrix4x4 PlantaDoPosto => Matrix4x4.TRS(new Vector3(0f, 0f, -10f), Quaternion.Euler(0f, 180f, 0f), Vector3.one);

        // as prateleiras desenhadas (e a colisão delas) no objeto da obra
        void Prateleiras(GameObject go, (MalhaDeObra vis, MalhaDeObra col, int moveis) d)
        {
            if (d.vis.Triangulos > 0)
            {
                var p = new GameObject("Prateleiras do Dia 0");
                p.transform.SetParent(go.transform, false);
                var malha = d.vis.ParaMesh("Prateleiras do Dia 0");
                p.AddComponent<MeshFilter>().sharedMesh = malha;
                p.AddComponent<MeshRenderer>().sharedMaterial = material;
                if (d.col.Triangulos > 0) p.AddComponent<MeshCollider>().sharedMesh = d.col.ParaMesh("Prateleiras do Dia 0 (colisão)");
                p.AddComponent<ApagarAsMalhas>();
            }
            MalhaDeObra.Devolver(d.vis);
            MalhaDeObra.Devolver(d.col);
        }

        /// <summary>
        /// As gôndolas do meio do salão (22/11/2026: as lojas do kit da Woodcreek só têm os móveis das paredes, e o salão
        /// ficava vazio): colunas de gôndolas ao comprido do salão (da frente para o fundo), a 2,3 m uma da outra, com o
        /// corredor da porta livre, longe dos móveis das paredes e dos caixas, na planta.
        /// </summary>
        static List<PontoDeMovel> GondolasDoMeio(PlantaDeLoja p, List<PontoDeMovel> pontos, List<Rect> balcoes, bool mesaDasMochilas)
        {
            var lista = new List<PontoDeMovel>();
            var s = p.Salao;
            float y = PlantaDeLoja.Piso;
            var ocupado = new List<Rect>();
            foreach (var pm in pontos)
            {
                if (pm.comodo != "salao" || pm.andar != 0) continue;
                y = pm.centro.y;
                bool deitado = Mathf.Abs(Mathf.Sin(pm.giro * Mathf.Deg2Rad)) > 0.7f;
                float mx = (deitado ? pm.tamanho.z : pm.tamanho.x) / 2f + 0.9f, mz = (deitado ? pm.tamanho.x : pm.tamanho.z) / 2f + 0.9f;
                ocupado.Add(Rect.MinMaxRect(pm.centro.x - mx, pm.centro.z - mz, pm.centro.x + mx, pm.centro.z + mz));
            }
            foreach (var b in balcoes) ocupado.Add(Rect.MinMaxRect(b.xMin - 1.3f, b.yMin - 1.3f, b.xMax + 1.3f, b.yMax + 1.3f));
            // o corredor da porta da frente, até 4 m para dentro
            var porta = p.ligacoes.Find(l => l.b == null && l.lado == 0);
            float xp = porta != null ? porta.pos : 0f;
            ocupado.Add(Rect.MinMaxRect(xp - 1.4f, s.z0 - 1f, xp + 1.4f, s.z0 + 4f));
            // na loja de roupas, o meio do salão fica para a mesa das mochilas (a LojaDoDiaZero.Mochilas procura ali)
            if (mesaDasMochilas) ocupado.Add(Rect.MinMaxRect(s.Cx - 2.4f, s.Cz - 1.6f, s.Cx + 2.4f, s.Cz + 1.6f));
            const float Largura = 0.9f, Comprida = 2.7f, Entre = 2.3f, Vao = 1.3f;
            float x0 = s.x0 + 1.7f, x1 = s.x1 - 1.7f, z0 = s.z0 + 3.2f, z1 = s.z1 - 1.7f;
            if (x1 - x0 < Largura || z1 - z0 < 1.8f) return lista;
            int colunas = Mathf.FloorToInt((x1 - x0 - Largura) / Entre) + 1;
            float sobra = (x1 - x0 - Largura) - (colunas - 1) * Entre;
            for (int i = 0; i < colunas; i++)
            {
                float x = x0 + sobra / 2f + Largura / 2f + i * Entre;
                for (float z = z0; z + 1.8f <= z1; z += Comprida + Vao)
                {
                    float comp = Mathf.Min(Comprida, z1 - z);
                    var r = Rect.MinMaxRect(x - Largura / 2f, z, x + Largura / 2f, z + comp);
                    if (ocupado.Exists(o => o.Overlaps(r))) continue;
                    lista.Add(new PontoDeMovel { tipo = "gondola", comodo = "salao", andar = 0, centro = new Vector3(x, y, z + comp / 2f), tamanho = new Vector3(Largura, 1.6f, comp) });
                }
            }
            return lista;
        }

        /// <summary>O caixa da loja comum: como o da 24 horas, ao lado da porta da frente, para dentro (na planta); null sem lugar.</summary>
        static Rect? BalcaoPertoDaPorta(PlantaDeLoja p)
        {
            var porta = p.ligacoes.Find(l => l.b == null && l.lado == 0);
            float xp = porta != null ? porta.pos : 0f;
            float lado = xp + 2.2f + 0.7f < p.X1 - 0.6f ? 1f : -1f;
            float a = xp + lado * 1.9f, b = a + lado * 0.7f;
            float z0 = p.Z0 + 1.0f, z1 = Mathf.Min(p.Z0 + 3.4f, p.zs - 1.6f);
            if (z1 - z0 <= 1f) return null;
            return Rect.MinMaxRect(Mathf.Min(a, b), z0, Mathf.Max(a, b), z1);
        }

        // ------------------------------------------------------------------ os atendentes (para os NPCs, sem a obra montada)

        /// <summary>
        /// Onde ficam os atendentes da loja do lote (no mundo) e para onde olham: a mesma conta da <see cref="LojaDoDiaZero"/>,
        /// feita na planta, para os NPCs já nascerem no lugar (a obra só entra perto de alguém).
        /// </summary>
        List<(Vector3 ponto, float giro)> AtendentesDe(LakewoodNaCena lw, ChaoDeLakewood.LoteNoChao lote, LojaDoDia tipo)
        {
            var lista = new List<(Vector3, float)>();
            var plano = lote.Plano;
            var pose = lw.PoseDaObra(lote);
            var obra = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);
            void Por(Matrix4x4 plantaParaObra, bool espelhar, Vector3 q, float giro)
            {
                var l = plantaParaObra.MultiplyPoint3x4(espelhar ? new Vector3(-q.x, q.y, q.z) : q);
                var g = plantaParaObra.rotation * Quaternion.Euler(0f, espelhar ? -giro : giro, 0f);
                lista.Add((obra.MultiplyPoint3x4(l), (pose.rotation * g).eulerAngles.y));
            }
            if (plano.Uso == UsoDoLote.LojaDeArmas)
            {
                foreach (var m in MoveisDaLojaDeArmas.Da(PlantaDaLojaDeArmas.Nova(plano.Semente)))
                {
                    if (m.modelo != "BalcaoCaixa" || !MedidasDaMobilia.Tem(m.modelo)) continue;
                    var real = MedidasDaMobilia.Real(m.modelo) * m.escala;
                    var meio = MedidasDaMobilia.Meio(m.modelo) * m.escala;
                    var giro = Quaternion.Euler(0f, m.giro, 0f);
                    var atras = m.lugar + giro * new Vector3(meio.x, 0f, meio.y + real.z / 2f + 0.5f);
                    Por(Matrix4x4.identity, false, atras, m.giro + 180f);
                }
                return lista;
            }
            PlantaDeLoja p;
            Matrix4x4 M;
            if (plano.Uso == UsoDoLote.Posto)
            {
                p = PlantasDeLoja.DoPosto(new System.Random(plano.Posto + 11), Medidas.DoJogo);
                M = PlantaDoPosto;
            }
            else
            {
                p = plano.Loja;
                M = Matrix4x4.identity;
            }
            if (p == null) return lista;
            var balcoes = new List<Rect>(p.balcoes);
            if (balcoes.Count == 0 && plano.Uso == UsoDoLote.Loja)
            {
                var r = BalcaoPertoDaPorta(p);
                if (r.HasValue) balcoes.Add(r.Value);
            }
            var porta = p.ligacoes.Find(l => l.b == null && l.lado == 0);
            float xp = porta != null ? porta.pos : 0f;
            float piso = PlantaDeLoja.Piso;
            for (int i = 0; i < balcoes.Count; i++)
            {
                var r = balcoes[i];
                if (tipo == LojaDoDia.Mercado)
                {
                    if (i >= 2) continue;
                    Por(M, p.espelhar, new Vector3(r.xMin - 0.55f, piso, r.center.y), 90f);
                }
                else if (r.width > r.height) Por(M, p.espelhar, new Vector3(r.center.x, piso, r.yMax + 0.6f), 180f);
                else
                {
                    bool direita = r.center.x > xp;
                    Por(M, p.espelhar, new Vector3(direita ? r.xMax + 0.5f : r.xMin - 0.5f, piso, r.center.y), direita ? 270f : 90f);
                }
            }
            return lista;
        }
    }

    /// <summary>Apaga as malhas feitas na hora do objeto quando ele sai (as prateleiras do Dia 0 saem com a obra).</summary>
    public class ApagarAsMalhas : MonoBehaviour
    {
        void OnDestroy()
        {
            var f = GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
            var c = GetComponent<MeshCollider>();
            if (c != null && c.sharedMesh != null) Destroy(c.sharedMesh);
        }
    }
}
