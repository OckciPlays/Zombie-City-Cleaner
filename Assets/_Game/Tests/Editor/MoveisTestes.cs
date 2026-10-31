using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// Os pontos dos móveis (Plano_Mapa, Parte M6, o fim dela): nas casas, nas lojas (e na do posto) e nos andares dos
    /// prédios (o térreo, o primeiro e o último), cada ponto fica dentro do cômodo dele, sem encostar em outro, fora do que
    /// ocupa o chão (o pé da escada, os buracos, as folhas das portas, os balcões) e da frente das portas, e nada alto fica
    /// na frente de uma janela; as portas de cada cômodo continuam ligadas e a frente de cada móvel alcançável (por uma
    /// grade própria, um pouco mais folgada que a do distribuidor); os cômodos têm os móveis principais; e os pontos saem
    /// nas malhas das casas (espelhados com elas). Com a mobília nova (nas casas e nos apartamentos), os pendurados e os
    /// tapetes ficam fora do que ocupa o chão (<see cref="PontoDeMovel.OcupaOChao"/>), os móveis novos podem passar 0,3 m do
    /// peitoril, e a frente do sofá e da cama pode ficar tapada pela mesa de centro e pelo baú.
    /// </summary>
    public class MoveisTestes
    {
        static readonly Medidas M = Medidas.DoJogo;

        struct Furo { public Vector2 a, b; public float chao, peitoril; public bool porta; }

        sealed class Construcao
        {
            public string nome;
            public readonly List<(Comodo k, float chao, string contexto)> comodos = new List<(Comodo, float, string)>();
            public readonly List<PontoDeMovel> pontos = new List<PontoDeMovel>();
            public readonly List<(Rect area, float chao)> ocupado = new List<(Rect, float)>();
            public readonly List<Furo> furos = new List<Furo>();
        }

        static List<Construcao> _construcoes;

        // as construções do Médio (semente 7), montadas uma vez (só o que os testes usam fica guardado)
        static List<Construcao> Construcoes()
        {
            if (_construcoes != null) return _construcoes;
            _construcoes = new List<Construcao>();
            var d = GeradorDoMundo.Gerar(7, TamanhoDoMundo.Medio);
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                {
                    if (l.Casa != null)
                    {
                        var c = new Casa(l.Casa, M);
                        c.Montar();
                        var r = Guardar(c, $"casa em {t.Cidade.Nome} ({l.Casa.molde})", c.moveis);
                        foreach (var k in l.Casa.comodos) r.comodos.Add((k, c.Base(k.andar), "casa"));
                    }
                    else if (l.Loja != null)
                    {
                        var lj = new Loja(l.Loja, M);
                        lj.Montar((x, z) => 0f, 0f);
                        var r = Guardar(lj, $"{PlantaDeLoja.Nome(l.Loja.tipo)} em {t.Cidade.Nome}", lj.moveis);
                        foreach (var k in l.Loja.comodos) r.comodos.Add((k, l.Loja.Base(k.andar), k.andar == 0 ? l.Loja.tipo.ToString() : "casa"));
                    }
                    else if (l.Predio != null)
                        foreach (int f in new[] { 0, 1, l.Predio.andares - 1 })
                        {
                            var p = new Predio(l.Predio, M, f);
                            p.MontarAndar();
                            var r = Guardar(p, $"{(l.Predio.hospital ? "hospital" : "prédio")} em {t.Cidade.Nome}, andar {f}", p.Moveis);
                            // (os apartamentos dos prédios à parte: "apartamento"; para o distribuidor, "casa")
                            foreach (var k in p.Comodos) r.comodos.Add((k, l.Predio.Base(f), l.Predio.hospital ? "hospital" : k.apto >= 0 ? "apartamento" : "predio"));
                        }
                }
            var posto = PlantasDeLoja.DoPosto(new System.Random(11), M);
            var lojaDoPosto = new Loja(posto, M);
            lojaDoPosto.Montar((x, z) => 0f, 0f);
            var rp = Guardar(lojaDoPosto, "loja do posto", lojaDoPosto.moveis);
            foreach (var k in posto.comodos) rp.comodos.Add((k, posto.Base(k.andar), posto.tipo.ToString()));
            return _construcoes;
        }

        static Construcao Guardar(Obra obra, string nome, List<PontoDeMovel> pontos)
        {
            var r = new Construcao { nome = nome };
            r.pontos.AddRange(pontos);
            r.ocupado.AddRange(obra.ocupado);
            void Ver(Parede w)
            {
                foreach (var ab in w.aberturas)
                    r.furos.Add(new Furo { a = w.a + w.u * ab.ini, b = w.a + w.u * (ab.ini + ab.larg), chao = w.baseY, porta = ab.EhPorta, peitoril = ab.baixo });
            }
            foreach (var w in obra.paredes) Ver(w);
            foreach (var w in obra.paredesDeFora) Ver(w);
            _construcoes.Add(r);
            MalhaDeObra.Devolver(obra.vis);
            MalhaDeObra.Devolver(obra.col);
            if (obra is Predio pr) foreach (var e in pr.Escuros) MalhaDeObra.Devolver(e);
            return r;
        }

        // o cômodo de um ponto: o do tipo e do andar dele, com o centro dentro
        static (Comodo k, float chao, string contexto) ComodoDe(Construcao c, PontoDeMovel p)
        {
            foreach (var x in c.comodos)
                if (x.k.tipo == p.comodo && x.k.andar == p.andar && Mathf.Abs(x.chao - p.centro.y) < 0.01f && x.k.Contem(p.centro.x, p.centro.z)) return x;
            return (null, 0f, null);
        }

        static Rect Interior(Comodo k) => Rect.MinMaxRect(k.x0 + M.esp / 2, k.z0 + M.esp / 2, k.x1 - M.esp / 2, k.z1 - M.esp / 2);

        static Rect Encolher(Rect r, float e) => Rect.MinMaxRect(r.xMin + e, r.yMin + e, r.xMax - e, r.yMax - e);

        [Test]
        public void OsMoveisFicamNosComodos()
        {
            int n = 0;
            foreach (var c in Construcoes())
                foreach (var p in c.pontos)
                {
                    n++;
                    var (k, _, _) = ComodoDe(c, p);
                    Assert.IsNotNull(k, $"{c.nome}: {p.tipo} sem cômodo ({p.comodo}, andar {p.andar}) em {p.centro}");
                    var r = Interior(k);
                    var pe = Encolher(p.Pe, 0.01f);
                    Assert.IsTrue(pe.xMin >= r.xMin && pe.xMax <= r.xMax && pe.yMin >= r.yMin && pe.yMax <= r.yMax, $"{c.nome}: {p.tipo} sai do {k.tipo} ({pe} fora de {r})");
                    Assert.Greater(p.tamanho.x, 0f);
                    Assert.Greater(p.tamanho.y, 0f);
                    Assert.Greater(p.tamanho.z, 0f);
                }
            Assert.Greater(n, 50000, "pontos de menos no Médio");
        }

        [Test]
        public void OsMoveisNaoSeEncostam()
        {
            foreach (var c in Construcoes())
                for (int i = 0; i < c.pontos.Count; i++)
                    for (int j = i + 1; j < c.pontos.Count; j++)
                    {
                        var a = c.pontos[i];
                        var b = c.pontos[j];
                        if (Mathf.Abs(a.centro.y - b.centro.y) > 0.01f || !a.OcupaOChao || !b.OcupaOChao) continue;
                        Assert.IsFalse(Encolher(a.Pe, 0.01f).Overlaps(Encolher(b.Pe, 0.01f)), $"{c.nome}: {a.tipo} em cima de {b.tipo} ({a.Pe} e {b.Pe})");
                    }
        }

        [Test]
        public void OsMoveisNaoTapamPortasNemEscadas()
        {
            foreach (var c in Construcoes())
                foreach (var p in c.pontos)
                {
                    if (!p.OcupaOChao) continue;
                    var pe = Encolher(p.Pe, 0.01f);
                    foreach (var (area, chao) in c.ocupado)
                        if (Mathf.Abs(chao - p.centro.y) < 0.2f)
                            Assert.IsFalse(pe.Overlaps(area), $"{c.nome}: {p.tipo} no {p.comodo} em cima do que ocupa o chão ({area})");
                    // na frente das portas (0,9 m para cada lado da parede, ao longo da largura dela; no vão largo sem porta, do
                    // meio dele, da largura de uma porta), nada
                    foreach (var f in c.furos)
                    {
                        if (!f.porta || Mathf.Abs(f.chao - p.centro.y) > 0.2f) continue;
                        bool emX = Mathf.Abs(f.a.y - f.b.y) < 0.01f;
                        float a0 = emX ? Mathf.Min(f.a.x, f.b.x) : Mathf.Min(f.a.y, f.b.y), b0 = emX ? Mathf.Max(f.a.x, f.b.x) : Mathf.Max(f.a.y, f.b.y);
                        if (b0 - a0 > MoveisDaObra.VaoLargo)
                        {
                            float meio = (a0 + b0) / 2;
                            a0 = meio - M.porta / 2;
                            b0 = meio + M.porta / 2;
                        }
                        var frente = emX
                            ? Rect.MinMaxRect(a0, f.a.y - 0.9f, b0, f.a.y + 0.9f)
                            : Rect.MinMaxRect(f.a.x - 0.9f, a0, f.a.x + 0.9f, b0);
                        Assert.IsFalse(pe.Overlaps(frente), $"{c.nome}: {p.tipo} no {p.comodo} na frente de uma porta ({frente})");
                    }
                }
        }

        [Test]
        public void NadaAltoNaFrenteDasJanelas()
        {
            foreach (var c in Construcoes())
                foreach (var p in c.pontos)
                    foreach (var f in c.furos)
                    {
                        if (f.porta || !p.OcupaOChao || Mathf.Abs(f.chao - p.centro.y) > 0.2f || p.tamanho.y <= f.peitoril + (MobiliaNova.EhNovo(p.tipo) ? 0.3f : 0f)) continue;
                        bool emX = Mathf.Abs(f.a.y - f.b.y) < 0.01f;
                        // a faixa de 0,3 m dos dois lados da parede, ao longo da janela
                        var janela = emX
                            ? Rect.MinMaxRect(Mathf.Min(f.a.x, f.b.x) + 0.02f, f.a.y - 0.3f, Mathf.Max(f.a.x, f.b.x) - 0.02f, f.a.y + 0.3f)
                            : Rect.MinMaxRect(f.a.x - 0.3f, Mathf.Min(f.a.y, f.b.y) + 0.02f, f.a.x + 0.3f, Mathf.Max(f.a.y, f.b.y) - 0.02f);
                        Assert.IsFalse(p.Pe.Overlaps(janela), $"{c.nome}: {p.tipo} ({p.tamanho.y:F2} m) na frente de uma janela de peitoril {f.peitoril:F2}");
                    }
        }

        [Test]
        public void AsPortasContinuamLigadas()
        {
            const float celula = 0.1f, folga = 0.25f;
            int comodos = 0;
            foreach (var c in Construcoes())
                foreach (var (k, chao, _) in c.comodos)
                {
                    var r = Interior(k);
                    var meus = c.pontos.FindAll(p => p.OcupaOChao && p.comodo == k.tipo && p.andar == k.andar && Mathf.Abs(p.centro.y - chao) < 0.01f && k.Contem(p.centro.x, p.centro.z));
                    if (meus.Count == 0) continue;
                    comodos++;
                    int nx = Mathf.FloorToInt(r.width / celula), nz = Mathf.FloorToInt(r.height / celula);
                    var livre = new bool[nx * nz];
                    var bocas = new List<Rect>();
                    foreach (var f in c.furos)
                    {
                        if (!f.porta || Mathf.Abs(f.chao - chao) > 0.2f) continue;
                        bool emX = Mathf.Abs(f.a.y - f.b.y) < 0.01f;
                        float linha = emX ? f.a.y : f.a.x;
                        float a = emX ? Mathf.Min(f.a.x, f.b.x) : Mathf.Min(f.a.y, f.b.y), b = emX ? Mathf.Max(f.a.x, f.b.x) : Mathf.Max(f.a.y, f.b.y);
                        Rect boca;
                        if (emX && Mathf.Abs(linha - k.z0) < 0.12f) boca = Rect.MinMaxRect(a + 0.1f, r.yMin, b - 0.1f, r.yMin + 0.4f);
                        else if (emX && Mathf.Abs(linha - k.z1) < 0.12f) boca = Rect.MinMaxRect(a + 0.1f, r.yMax - 0.4f, b - 0.1f, r.yMax);
                        else if (!emX && Mathf.Abs(linha - k.x0) < 0.12f) boca = Rect.MinMaxRect(r.xMin, a + 0.1f, r.xMin + 0.4f, b - 0.1f);
                        else if (!emX && Mathf.Abs(linha - k.x1) < 0.12f) boca = Rect.MinMaxRect(r.xMax - 0.4f, a + 0.1f, r.xMax, b - 0.1f);
                        else continue;
                        if ((emX && (b < k.x0 || a > k.x1)) || (!emX && (b < k.z0 || a > k.z1))) continue;
                        bocas.Add(boca);
                    }
                    bool NaBoca(float x, float z) { foreach (var bo in bocas) if (bo.Contains(new Vector2(x, z))) return true; return false; }
                    for (int j = 0; j < nz; j++)
                        for (int i = 0; i < nx; i++)
                        {
                            float x = r.xMin + (i + 0.5f) * celula, z = r.yMin + (j + 0.5f) * celula;
                            bool l = (x - r.xMin >= folga && r.xMax - x >= folga && z - r.yMin >= folga && r.yMax - z >= folga) || NaBoca(x, z);
                            foreach (var p in meus)
                            {
                                var pe = p.Pe;
                                if (x > pe.xMin - folga && x < pe.xMax + folga && z > pe.yMin - folga && z < pe.yMax + folga) { l = false; break; }
                            }
                            livre[j * nx + i] = l;
                        }
                    if (bocas.Count == 0) continue;
                    // a busca pela grade, da boca da primeira porta
                    var visto = new bool[nx * nz];
                    var fila = new Queue<int>();
                    for (int j = 0; j < nz; j++)
                        for (int i = 0; i < nx; i++)
                        {
                            float x = r.xMin + (i + 0.5f) * celula, z = r.yMin + (j + 0.5f) * celula;
                            if (livre[j * nx + i] && bocas[0].Contains(new Vector2(x, z))) { visto[j * nx + i] = true; fila.Enqueue(j * nx + i); }
                        }
                    Assert.Greater(fila.Count, 0, $"{c.nome}: a porta do {k.tipo} (andar {k.andar}) tapada");
                    while (fila.Count > 0)
                    {
                        int cel = fila.Dequeue(), i = cel % nx, j = cel / nx;
                        foreach (int v in new[] { i > 0 ? cel - 1 : -1, i < nx - 1 ? cel + 1 : -1, j > 0 ? cel - nx : -1, j < nz - 1 ? cel + nx : -1 })
                            if (v >= 0 && livre[v] && !visto[v]) { visto[v] = true; fila.Enqueue(v); }
                    }
                    bool Alcanca(Rect alvo)
                    {
                        for (int j = 0; j < nz; j++)
                            for (int i = 0; i < nx; i++)
                            {
                                float x = r.xMin + (i + 0.5f) * celula, z = r.yMin + (j + 0.5f) * celula;
                                if (visto[j * nx + i] && alvo.Overlaps(new Rect(x - celula / 2, z - celula / 2, celula, celula))) return true;
                            }
                        return false;
                    }
                    for (int b = 1; b < bocas.Count; b++) Assert.IsTrue(Alcanca(bocas[b]), $"{c.nome}: no {k.tipo} (andar {k.andar}), os móveis separam as portas");
                    foreach (var p in meus)
                    {
                        // a frente do móvel: quem anda chega a uns 0,3 m dela (o quadrado de 0,6 m logo na frente do meio)
                        var dir = new Vector2(Mathf.Sin(p.giro * Mathf.Deg2Rad), Mathf.Cos(p.giro * Mathf.Deg2Rad));
                        var meio = new Vector2(p.centro.x, p.centro.z) + dir * (p.tamanho.z / 2 + 0.3f);
                        var quadrado = new Rect(meio.x - 0.3f, meio.y - 0.3f, 0.6f, 0.6f);
                        bool tapada = meus.Exists(o => (o.tipo == "MesaDeCentro" || o.tipo == "Bau") && o.Pe.Overlaps(quadrado));
                        // (na cama nova, também pelos lados: a de lado, encostada pelo comprido, chega-se por um deles)
                        bool peloLado = false;
                        if (p.tipo.StartsWith("Cama") || p.tipo == "Beliche" || p.tipo == "ColchaoNoChao")
                            foreach (float s in new[] { -1f, 1f })
                            {
                                var lado = new Vector2(dir.y, -dir.x) * s;
                                var m = new Vector2(p.centro.x, p.centro.z) + lado * (p.tamanho.x / 2 + 0.3f);
                                peloLado |= Alcanca(new Rect(m.x - 0.3f, m.y - 0.3f, 0.6f, 0.6f));
                            }
                        Assert.IsTrue(Alcanca(quadrado) || p.tipo == "gondola" || tapada || peloLado, $"{c.nome}: a frente do {p.tipo} no {k.tipo} não se alcança");
                    }
                }
            Assert.Greater(comodos, 10000);
        }

        [Test]
        public void OsComodosTemOsMoveisPrincipais()
        {
            // o que cada cômodo precisa ter (um dos tipos), e em quantos por cento dos cômodos, no mínimo
            var regras = new (string contexto, string comodo, string[] tipos, float minimo)[]
            {
                // (as casas e os apartamentos: a mobília nova, a etapa 4 do Plano_Mobilia.md da pasta de arte)
                ("casa", "quarto", new[] { "CamaCasal", "CamaSolteiro", "Beliche", "ColchaoNoChao" }, 0.97f),
                // (com as folhas das portas bem abertas sólidas para quem anda, a mobília: 92% e 94%; eram 95%. Nas casas
                // e nos apartamentos das lojas, todas; nos apartamentos dos prédios, 85% e 88%: a porta de entrada abre
                // para dentro da cozinha pequena, e a folha toma o lugar da geladeira ou do fogão)
                ("casa", "cozinha", new[] { "Geladeira", "GeladeiraGrande" }, 0.91f),
                ("casa", "cozinha", new[] { "Fogao" }, 0.93f),
                ("casa", "banheiro", new[] { "Privada" }, 0.97f),
                ("casa", "sala", new[] { "Sofa1", "Sofa2", "Sofa3", "SofaL" }, 0.95f),
                // os apartamentos dos prédios: a cozinha (3 × 3,6 m, com uma porta ou um vão em cada parede) só tem parede
                // para dois dos móveis de 1 m de fundo da mobília nova em ×1,30: o fogão e a pia quase sempre, a geladeira
                // em uns 18% (com a mobília antiga, 85%); no banheiro de 2 × 2,6 m, a folha da porta atravessa quase o cômodo,
                // e a privada (0,8 m de fundo) fica de fora em uns 11%; o quarto estreito leva a cama de solteiro de lado
                ("apartamento", "quarto", new[] { "CamaCasal", "CamaSolteiro", "Beliche", "ColchaoNoChao" }, 0.97f),
                ("apartamento", "cozinha", new[] { "Fogao" }, 0.9f),
                ("apartamento", "cozinha", new[] { "Geladeira", "GeladeiraGrande" }, 0.15f),
                ("apartamento", "banheiro", new[] { "Privada" }, 0.85f),
                ("apartamento", "sala", new[] { "Sofa1", "Sofa2", "Sofa3", "SofaL" }, 0.9f),
                ("hospital", "enfermaria", new[] { "cama de hospital" }, 0.95f),
                ("Mercado", "salao", new[] { "prateleira", "gondola" }, 1f),
                ("Farmacia", "salao", new[] { "prateleira" }, 1f),
                ("Armas", "salao", new[] { "expositor de armas" }, 1f),
                ("Armas", "deposito", new[] { "armario de armas" }, 1f),
            };
            foreach (var (contexto, comodo, tipos, minimo) in regras)
            {
                int total = 0, com = 0;
                foreach (var c in Construcoes())
                    foreach (var (k, chao, ctx) in c.comodos)
                    {
                        if (ctx != contexto || k.tipo != comodo) continue;
                        total++;
                        foreach (var p in c.pontos)
                            if (p.comodo == k.tipo && p.andar == k.andar && Mathf.Abs(p.centro.y - chao) < 0.01f && k.Contem(p.centro.x, p.centro.z) && System.Array.IndexOf(tipos, p.tipo) >= 0) { com++; break; }
                    }
                Assert.Greater(total, 0, $"nenhum {comodo} ({contexto})");
                Assert.GreaterOrEqual(com, Mathf.CeilToInt(total * minimo - 0.001f), $"{comodo} ({contexto}): só {com} de {total} com {string.Join(" ou ", tipos)}");
            }
            int saque = 0;
            foreach (var c in Construcoes()) foreach (var p in c.pontos) if (p.saque) saque++;
            Assert.Greater(saque, 30000, "pontos de saque de menos");
        }

        [Test]
        public void OsPontosSaemNasMalhasDasCasas()
        {
            var d = GeradorDoMundo.Gerar(7, TamanhoDoMundo.Medio);
            int casas = 0, espelhadas = 0, andares = 0;
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                {
                    if (l.Casa != null && casas < 60)
                    {
                        var desenho = MalhaDeObra.Emprestar(TamanhoDaMalha.Grande);
                        var c = MalhasDasCasas.Montar(d, t, l, desenho);
                        Assert.Greater(c.Moveis.Count, 5, $"{c.Nome}: poucos pontos");
                        float meiaW = l.Casa.W / 2 + 0.05f, meiaD = l.Casa.D / 2 + 0.05f;
                        foreach (var p in c.Moveis)
                        {
                            Assert.LessOrEqual(Mathf.Abs(p.centro.x), meiaW + (l.Casa.garagem != null ? l.Casa.garagem.largura : 0f), $"{c.Nome}: {p.tipo} fora da casa");
                            Assert.LessOrEqual(Mathf.Abs(p.centro.z), meiaD, $"{c.Nome}: {p.tipo} fora da casa");
                        }
                        if (l.Casa.espelhar)
                        {
                            espelhadas++;
                            var obra = new Casa(l.Casa, M);
                            obra.Montar();
                            Assert.AreEqual(obra.moveis.Count, c.Moveis.Count);
                            for (int i = 0; i < c.Moveis.Count; i++) Assert.AreEqual(-obra.moveis[i].centro.x, c.Moveis[i].centro.x, 1e-4f, "o espelho dos pontos");
                            MalhaDeObra.Devolver(obra.vis);
                            MalhaDeObra.Devolver(obra.col);
                        }
                        MalhaDeObra.Devolver(desenho);
                        MalhaDeObra.Devolver(c.Colisao);
                        casas++;
                    }
                    if (l.Predio != null && andares < 20)
                    {
                        var a = MalhasDasCasas.MontarAndar(l.Predio, 1);
                        Assert.Greater(a.Moveis.Count, 0, "andar de prédio sem pontos");
                        MalhasDasCasas.Devolver(a);
                        andares++;
                    }
                }
            Assert.Greater(espelhadas, 0);
            var posto = ModelosDaEstrada.Posto(3);
            Assert.Greater(posto.Moveis.Count, 3, "a loja do posto sem pontos");
        }
    }
}
