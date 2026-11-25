using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O que vai num lote das cidades do Lakewood (Plano_Lakewood, Etapa 6, a parte 3): a planta (a casa, a loja ou o posto
    /// do kit), onde ela fica no lote (o meio e o giro, nas coordenadas do lote) e a pegada. Feito uma vez, pela semente do
    /// lote (toda partida vê a mesma vila). As casas não têm porão (o chão do Lakewood não tem buracos).
    /// </summary>
    public sealed class PlanoDaObra
    {
        public UsoDoLote Uso;
        public Planta Casa;
        public PlantaDeLoja Loja;
        /// <summary>O prédio do kit (23/11/2026, a cidade de Lakewood: os prédios do centro).</summary>
        public PlantaDePredio Predio;
        /// <summary>A variante do posto (a cor da bandeira).</summary>
        public int Posto = -1;
        /// <summary>O meio da obra (a origem dela) nas coordenadas do lote e o giro dela em relação ao lote (graus).</summary>
        public Vector2 Meio;
        public float GiroNoLote;
        /// <summary>A pegada (a construção com a varanda, os degraus e a garagem) nas coordenadas do lote.</summary>
        public Rect Pegada;
        /// <summary>A altura das paredes (para a caixa de longe) e as cores dela.</summary>
        public float Altura;
        public string Parede = "ParedeBege", Telhado = "Telhado";
        /// <summary>O pé da escada da frente e a boca da garagem (nas coordenadas do lote), para os caminhos até a rua.</summary>
        public Vector2? Entrada, EntradaDoCarro;
        public int Semente;
        /// <summary>Nos especiais e nas cabanas: o tipo (<see cref="EspeciaisDeLakewood"/>) e as medidas da casa deles.</summary>
        public string Especial;
        public Medidas? Medidas;
        /// <summary>A largura e o fundo do lote (a praça: os caminhos pelo lote inteiro).</summary>
        public Vector2 TamanhoDoLote;

        public string Nome => Uso switch
        {
            UsoDoLote.Casa => Casa != null ? (Casa.molde == "Terrea" ? "Casa térrea" : "Sobrado") : "Casa",
            UsoDoLote.Loja => Loja != null ? PlantaDeLoja.Nome(Loja.tipo) + (Loja.andares > 1 ? $" de {Loja.andares} andares" : "") : "Loja",
            UsoDoLote.Posto => "Posto",
            UsoDoLote.Predio => Predio != null ? $"Prédio de {Predio.andares} andares" : "Prédio",
            UsoDoLote.Estacionamento => "Estacionamento",
            UsoDoLote.Praca => "Praça",
            UsoDoLote.Galpao => "Galpão",
            UsoDoLote.Especial or UsoDoLote.Cabana => EspeciaisDeLakewood.NomeDe(Especial),
            _ => Uso.ToString(),
        };
    }

    /// <summary>A obra montada (em outra thread): o desenho, a colisão e o resto, nas coordenadas da obra.</summary>
    public sealed class ObraPronta
    {
        public PlanoDaObra Plano;
        public MalhaDeObra Desenho;
        public MalhaDaHera Hera;
        public MalhaDePichacao Pichacoes;
        public MalhasDasCasas.CasaPronta Casa;
        /// <summary>Onde os carros podem ficar (nas coordenadas da obra, já espelhados).</summary>
        public readonly List<(Vector2 pos, float yaw)> Carros = new List<(Vector2, float)>();
        /// <summary>O posto já vem com as caixas de colisão (as colunas, as bombas, o totem).</summary>
        public readonly List<ModelosDaEstrada.Caixa> Caixas = new List<ModelosDaEstrada.Caixa>();
        /// <summary>As marcas do abandono e as pichações de cada parte da loja de armas (as marcas no material delas).</summary>
        public readonly List<MalhaDePichacao> Marcas = new List<MalhaDePichacao>(), MaisPichacoes = new List<MalhaDePichacao>();
        /// <summary>Os móveis da loja de armas (os prefabs da mobília nova, nas coordenadas da obra).</summary>
        public readonly List<MoveisDaLojaDeArmas.Movel> MoveisDaLoja = new List<MoveisDaLojaDeArmas.Movel>();
        /// <summary>O estado da sala segura e o resto da variação da loja de armas.</summary>
        public PlantaDaLojaDeArmas.Variacao Variacao;
        /// <summary>
        /// As pedras da trilha e as faixas de concreto até a rua (no espaço do mapa; quem monta a obra na cena põe: 23/11/2026,
        /// com os 4 mil lotes da cidade de Lakewood, montar todas ao abrir o jogo levava uns 40 s).
        /// </summary>
        public MalhaDeObra Caminhos;
        /// <summary>No prédio: as janelas escuras de cada andar (nas coordenadas da obra, já espelhadas; quem põe devolve).</summary>
        public MalhaDeObra[] Escuros;
    }

    public static class ObrasDeLakewood
    {
        static readonly Medidas M = Medidas.DoJogo;

        /// <summary>Da frente do lote até a frente da casa (os degraus; 6 m desde 12/11/2026, para a trilha da porta ter onde
        /// fazer a curva), e a folga dos fundos e dos lados (m).</summary>
        public const float RecuoDaCasa = 6f, FundosDaCasa = 3f, LadosDaCasa = 1.5f;

        /// <summary>
        /// O plano de cada lote da cidade (os tipos das lojas pela cota da vila: um mercado, uma farmácia, duas 24 horas e
        /// o resto comum, sorteados entre as lojas; o hospital e a loja de armas no terreno deles; os especiais e as cabanas
        /// pelo <see cref="EspeciaisDeLakewood"/>). Os vazios ficam sem plano.
        /// </summary>
        public static Dictionary<int, PlanoDaObra> Planejar(MapaDeLakewood mapa, string cidade)
        {
            var planos = new Dictionary<int, PlanoDaObra>();
            var lojas = new List<int>();
            for (int i = 0; i < mapa.lotes.Count; i++)
                if (mapa.lotes[i].cidade == cidade && mapa.lotes[i].tipo == UsoDoLote.Loja && string.IsNullOrEmpty(mapa.lotes[i].nome)) lojas.Add(i);
            // a cota das lojas, embaralhada pela semente do mapa e o nome da cidade
            var cota = new List<TipoDeLoja> { TipoDeLoja.Mercado, TipoDeLoja.Farmacia, TipoDeLoja.VinteQuatroHoras, TipoDeLoja.VinteQuatroHoras };
            while (cota.Count < lojas.Count) cota.Add(TipoDeLoja.Comum);
            var sorte = new System.Random(unchecked(mapa.semente * 7919 + cidade.Length * 104729 + cidade[0]));
            for (int k = cota.Count - 1; k > 0; k--)
            {
                int j = sorte.Next(k + 1);
                (cota[k], cota[j]) = (cota[j], cota[k]);
            }
            var cid = mapa.cidades.Find(c => c.nome == cidade);
            var centro = cid != null ? cid.centro : Vector2.zero;
            for (int i = 0; i < mapa.lotes.Count; i++)
            {
                var l = mapa.lotes[i];
                if (l.cidade != cidade) continue;
                PlanoDaObra p = null;
                switch (l.tipo)
                {
                    case UsoDoLote.Casa: p = DaCasa(l); break;
                    case UsoDoLote.Loja: p = l.nome == "marina" ? DaLojaDaMarina(l) : DaLoja(l, cota[lojas.IndexOf(i)]); break;
                    case UsoDoLote.Posto: p = DoPosto(l); break;
                    case UsoDoLote.Hospital: p = DoHospital(l); break;
                    case UsoDoLote.LojaDeArmas: p = DaLojaDeArmas(l, mapa.semente); break;
                    case UsoDoLote.Especial: p = DoEspecial(l, l.nome); break;
                    case UsoDoLote.Cabana: p = DoEspecial(l, "cabana"); break;
                    case UsoDoLote.Predio: p = DoPredio(l, centro); break;
                    case UsoDoLote.Estacionamento: p = DoChao(l, UsoDoLote.Estacionamento, 0.08f, "Asfalto"); break;
                    case UsoDoLote.Praca: p = DaPraca(l); break;
                    case UsoDoLote.Galpao: p = DoGalpao(l); break;
                }
                if (p != null) planos[i] = p;
            }
            return planos;
        }

        static PlanoDaObra DaCasa(LoteDoMapa l)
        {
            var r = new System.Random(l.semente);
            float largura = l.largura - 2f * LadosDaCasa, fundo = l.fundo - RecuoDaCasa - FundosDaCasa;
            var p = PlantasDeCasa.ParaOLote(r, M, largura, fundo, porao: false);
            if (p == null) return null;
            if (p.garagem != null) p.garagem.fechada = r.NextDouble() < 0.6;
            var lx = Casa.Limites(p, M);
            var lz = Casa.Profundidade(p, M);
            var meio = new Vector2(-(lx.x + lx.y) * 0.5f, -l.fundo * 0.5f + RecuoDaCasa - lz.x);
            var plano = new PlanoDaObra
            {
                Uso = UsoDoLote.Casa,
                Casa = p,
                Meio = meio,
                Pegada = Rect.MinMaxRect(meio.x + lx.x, meio.y + lz.x, meio.x + lx.y, meio.y + lz.y),
                Altura = Casa.PisoDe(p) + p.andares * (M.altura + M.laje) + M.forro,
                Parede = p.cor,
                Telhado = p.telhado,
                Semente = p.semente,
            };
            float sx = p.espelhar ? -1f : 1f;
            plano.Entrada = meio + new Vector2(sx * p.xPortaFrente, lz.x);
            if (Casa.LugarDoCarro(p, M, out var carro, out _) && p.garagem != null)
                plano.EntradaDoCarro = meio + new Vector2(carro.x, p.garagem.z0 - 0.3f);
            return plano;
        }

        // um especial (a igreja, a escola, os bombeiros, a guarda florestal, o motel) ou uma cabana: a planta desenhada pelo
        // EspeciaisDeLakewood, no meio da largura do lote, a frente (com o que fica na frente dela, a torre da igreja) a
        // 'Recuo' da frente do lote; null se não couber
        static PlanoDaObra DoEspecial(LoteDoMapa l, string tipo)
        {
            var d = EspeciaisDeLakewood.Desenhar(tipo, l.semente);
            if (d == null) return null;
            var p = d.Planta;
            var m = d.Medidas;
            var lx = Casa.Limites(p, m);
            var lz = Casa.Profundidade(p, m);
            float frente = lz.x - d.NaFrente;
            var meio = new Vector2(-(lx.x + lx.y) * 0.5f, -l.fundo * 0.5f + d.Recuo - frente);
            var pegada = Rect.MinMaxRect(meio.x + lx.x, meio.y + frente, meio.x + lx.y, meio.y + lz.y);
            if (pegada.xMin < -l.largura * 0.5f + 0.5f || pegada.xMax > l.largura * 0.5f - 0.5f || pegada.yMax > l.fundo * 0.5f - 0.8f) return null;
            var plano = new PlanoDaObra
            {
                Uso = l.tipo,
                Casa = p,
                Especial = tipo,
                Medidas = m,
                Meio = meio,
                Pegada = pegada,
                Altura = Casa.PisoDe(p) + p.andares * (m.altura + m.laje) + m.forro,
                Parede = p.cor,
                Telhado = p.telhado,
                Semente = p.semente,
            };
            float sx = p.espelhar ? -1f : 1f;
            plano.Entrada = meio + new Vector2(sx * p.xPortaFrente, frente);
            if (Casa.LugarDoCarro(p, m, out var carro, out _) && p.garagem != null)
                plano.EntradaDoCarro = meio + new Vector2(carro.x, p.garagem.z0 - 0.3f);
            else if (tipo == "bombeiros")
                plano.EntradaDoCarro = meio + new Vector2(p.X0 + 5.5f, frente);
            return plano;
        }

        static PlanoDaObra DaLoja(LoteDoMapa l, TipoDeLoja tipo, int andaresPedidos = 0)
        {
            var r = new System.Random(l.semente);
            string[] cores = { "ParedeBege", "Tijolo", "ParedeAzul", "ParedeVerde", "TijoloEscuro" };
            int andares = tipo == TipoDeLoja.Comum && r.NextDouble() < 0.4 ? 2 : 1;
            if (andaresPedidos > 0) andares = andaresPedidos;
            var p = PlantasDeLoja.ParaOLote(r, M, tipo, l.largura - 2f, l.fundo - 4f, andares, cores[r.Next(cores.Length)]);
            if (p == null) return null;
            // a frente da loja 0,6 m atrás da frente do lote (a calçada chega nela)
            var meio = new Vector2(0f, -l.fundo * 0.5f + 0.6f + p.D * 0.5f);
            return new PlanoDaObra
            {
                Uso = UsoDoLote.Loja,
                Loja = p,
                Meio = meio,
                Pegada = Rect.MinMaxRect(meio.x - p.W * 0.5f, meio.y - p.D * 0.5f, meio.x + p.W * 0.5f, meio.y + p.D * 0.5f),
                Altura = p.Cobertura + 0.9f,
                Parede = p.cor,
                Telhado = "Concreto",
                Semente = p.semente,
            };
        }

        static readonly string[] CoresDosPredios = { "Concreto", "ConcretoClaro", "Tijolo", "TijoloEscuro", "ParedeBege", "Concreto" };

        // um prédio do kit (23/11/2026, a cidade de Lakewood: os prédios do centro, como no mapa sorteado): de uns 15 andares
        // perto do meio da cidade a uns 5 na beira do centro, a frente 0,6 m atrás da frente do lote (a calçada chega nela)
        static PlanoDaObra DoPredio(LoteDoMapa l, Vector2 centro)
        {
            var r = new System.Random(l.semente);
            float d = Vector2.Distance(l.meio, centro);
            int andares = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(15f, 5f, Mathf.InverseLerp(60f, 380f, d))) + r.Next(-2, 3), 4, 15);
            var p = PlantasDePredio.ParaOLote(r, M, l.largura - 2f, l.fundo - 4f, andares, CoresDosPredios[r.Next(CoresDosPredios.Length)]);
            if (p == null || p.W > l.largura - 1f || p.D > l.fundo - 1.5f) return null;
            var meio = new Vector2(0f, -l.fundo * 0.5f + 0.6f + p.D * 0.5f);
            return new PlanoDaObra
            {
                Uso = UsoDoLote.Predio,
                Predio = p,
                Meio = meio,
                Pegada = Rect.MinMaxRect(meio.x - p.W * 0.5f, meio.y - p.D * 0.5f, meio.x + p.W * 0.5f, meio.y + p.D * 0.5f),
                Altura = p.AlturaDaCaixa,
                Parede = p.cor,
                Telhado = "Concreto",
                Semente = p.semente,
            };
        }

        // ------------------------------------------------------------------ o estacionamento, a praça e o galpão
        // (23/11/2026, a cidade de Lakewood: os lotes que eram só o chão plano)

        // o estacionamento e a praça: o lote inteiro (menos a beira), no meio dele
        static PlanoDaObra DoChao(LoteDoMapa l, UsoDoLote uso, float altura, string cor)
        {
            float w = l.largura * 0.5f - 0.3f, f = l.fundo * 0.5f - 0.3f;
            return new PlanoDaObra
            {
                Uso = uso,
                Meio = Vector2.zero,
                Pegada = Rect.MinMaxRect(-w, -f, w, f),
                Altura = altura,
                Parede = cor,
                Telhado = cor,
                Semente = l.semente,
            };
        }

        // a praça: a "construção" é o chafariz no meio (o resto é o gramado com os caminhos e as árvores dos canteiros)
        static PlanoDaObra DaPraca(LoteDoMapa l) => new PlanoDaObra
        {
            Uso = UsoDoLote.Praca,
            Meio = Vector2.zero,
            Pegada = Rect.MinMaxRect(-4f, -4f, 4f, 4f),
            Altura = 0.6f,
            Parede = "Pedra",
            Telhado = "Pedra",
            Semente = l.semente,
            TamanhoDoLote = new Vector2(l.largura, l.fundo),
        };

        // o galpão da zona industrial: o corpo de metal (de 6,5 a 9 m), recuado 4 m da frente (o pátio das cargas)
        static PlanoDaObra DoGalpao(LoteDoMapa l)
        {
            var r = new System.Random(l.semente);
            float W = Mathf.Min(l.largura - 4f, 60f), D = Mathf.Min(l.fundo - 6f, 40f);
            if (W < 10f || D < 10f) return null;
            var meio = new Vector2(0f, -l.fundo * 0.5f + 4f + D * 0.5f);
            string[] cores = { "Metal", "ConcretoClaro", "Ferrugem", "Concreto" };
            return new PlanoDaObra
            {
                Uso = UsoDoLote.Galpao,
                Meio = meio,
                Pegada = Rect.MinMaxRect(meio.x - W * 0.5f, meio.y - D * 0.5f, meio.x + W * 0.5f, meio.y + D * 0.5f),
                Altura = 6.5f + (float)r.NextDouble() * 2.5f,
                Parede = cores[r.Next(cores.Length)],
                Telhado = "Metal",
                Semente = l.semente,
            };
        }

        // o estacionamento: o asfalto com as vagas pintadas e os carros largados nelas (os de verdade vêm do PorOResto, sete
        // em cada dez vagas da lista)
        static void MontarEstacionamento(ObraPronta o)
        {
            var pl = o.Plano;
            var r = pl.Pegada;
            var col = new MalhaDeObra();
            var m = o.Desenho;
            m.Caixa(Matrix4x4.Translate(new Vector3(r.center.x, 0.02f, r.center.y)), new Vector3(r.width, 0.08f, r.height), "Asfalto");
            col.Caixa(Matrix4x4.Translate(new Vector3(r.center.x, 0.02f, r.center.y)), new Vector3(r.width, 0.08f, r.height), "Asfalto");
            var sorte = new System.Random(pl.Semente);
            // duas fileiras de vagas de 2,6 × 5 m: uma no fundo (de frente para o fundo) e outra a 6 m dela, de frente para a rua
            int n = Mathf.FloorToInt((r.width - 1f) / 2.6f);
            float x0 = r.center.x - n * 1.3f;
            foreach (var (zMeio, giro) in new[] { (r.yMax - 3f, 0f), (r.yMax - 14f, 180f) })
            {
                if (zMeio - 2.5f < r.yMin + 1f) continue;
                for (int k = 0; k <= n; k++)
                {
                    float x = x0 + k * 2.6f;
                    m.Caixa(Matrix4x4.Translate(new Vector3(x, 0.065f, zMeio)), new Vector3(0.12f, 0.01f, 4.6f), "ConcretoClaro");
                    if (k < n && sorte.NextDouble() < 0.55)
                        o.Carros.Add((new Vector2(x + 1.3f, zMeio), giro + (float)(sorte.NextDouble() * 10 - 5)));
                }
            }
            o.Casa = new MalhasDasCasas.CasaPronta { Nome = pl.Nome, Colisao = col, Semente = pl.Semente };
        }

        // a praça: os caminhos de concreto em cruz e em volta, o chafariz no meio e os bancos (as árvores são do chão:
        // ChaoDeLakewood.QuintalDaPlanta)
        static void MontarPraca(ObraPronta o)
        {
            var pl = o.Plano;
            var lote = pl.TamanhoDoLote;
            var r = Rect.MinMaxRect(-lote.x * 0.5f + 0.3f, -lote.y * 0.5f + 0.3f, lote.x * 0.5f - 0.3f, lote.y * 0.5f - 0.3f);
            var col = new MalhaDeObra();
            var m = o.Desenho;
            float y = 0.04f;
            void Caminho(Vector2 a, Vector2 b, float largura)
            {
                var d = (b - a).normalized;
                var n = new Vector2(-d.y, d.x) * (largura * 0.5f);
                Vector3 P(Vector2 q) => new Vector3(q.x, y, q.y);
                m.Face(new[] { P(a - n), P(b - n), P(b + n), P(a + n) }, Vector3.up, "Calcada");
            }
            // a cruz e a volta, a 3 m da beira
            Caminho(new Vector2(r.xMin, r.center.y), new Vector2(r.xMax, r.center.y), 3f);
            Caminho(new Vector2(r.center.x, r.yMin), new Vector2(r.center.x, r.yMax), 3f);
            Caminho(new Vector2(r.xMin + 3f, r.yMin + 3f), new Vector2(r.xMax - 3f, r.yMin + 3f), 2f);
            Caminho(new Vector2(r.xMin + 3f, r.yMax - 3f), new Vector2(r.xMax - 3f, r.yMax - 3f), 2f);
            Caminho(new Vector2(r.xMin + 3f, r.yMin + 3f), new Vector2(r.xMin + 3f, r.yMax - 3f), 2f);
            Caminho(new Vector2(r.xMax - 3f, r.yMin + 3f), new Vector2(r.xMax - 3f, r.yMax - 3f), 2f);
            // o chafariz: a bacia, a água parada (escura) e a coluna do meio
            var c = new Vector3(r.center.x, 0f, r.center.y);
            m.Cilindro(Matrix4x4.Translate(c), 3.4f, 3.4f, 0.6f, 20, "Pedra");
            col.Cilindro(Matrix4x4.Translate(c), 3.4f, 3.4f, 0.6f, 12, "Pedra");
            m.Cilindro(Matrix4x4.Translate(c + Vector3.up * 0.45f), 3.05f, 3.05f, 0.08f, 20, "Oleo");
            m.Cilindro(Matrix4x4.Translate(c), 0.5f, 0.35f, 2.4f, 10, "Pedra");
            col.Cilindro(Matrix4x4.Translate(c), 0.5f, 0.35f, 2.4f, 8, "Pedra");
            m.Cilindro(Matrix4x4.Translate(c + Vector3.up * 2.4f), 1.1f, 0.4f, 0.35f, 12, "Pedra");
            // os bancos, de frente para o chafariz, nas quatro pontas da cruz
            foreach (var (dir, giro) in new[] { (Vector3.right, -90f), (Vector3.left, 90f), (Vector3.forward, 180f), (Vector3.back, 0f) })
            {
                var pe = c + dir * 6.5f + Quaternion.Euler(0f, giro, 0f) * Vector3.right * 2.2f;
                var M = Matrix4x4.TRS(pe, Quaternion.Euler(0f, giro, 0f), Vector3.one);
                m.Caixa(M * Matrix4x4.Translate(new Vector3(0f, 0.45f, 0f)), new Vector3(1.8f, 0.07f, 0.45f), "MadeiraVelha");
                m.Caixa(M * Matrix4x4.Translate(new Vector3(0f, 0.75f, -0.2f)), new Vector3(1.8f, 0.4f, 0.05f), "MadeiraVelha");
                m.Caixa(M * Matrix4x4.Translate(new Vector3(-0.75f, 0.22f, 0f)), new Vector3(0.06f, 0.44f, 0.4f), "Metal");
                m.Caixa(M * Matrix4x4.Translate(new Vector3(0.75f, 0.22f, 0f)), new Vector3(0.06f, 0.44f, 0.4f), "Metal");
                col.Caixa(M * Matrix4x4.Translate(new Vector3(0f, 0.45f, -0.05f)), new Vector3(1.8f, 0.9f, 0.5f), "Madeira");
            }
            o.Casa = new MalhasDasCasas.CasaPronta { Nome = pl.Nome, Colisao = col, Semente = pl.Semente };
        }

        // o galpão por dentro (23/11/2026, "Siga: marina e galpões"): o piso de concreto, as paredes de metal com os vãos dos
        // portões de enrolar (um aberto, os outros fechados) e da porta, o telhado em duas águas (de dentro também), as
        // fileiras de estantes de aço, as caixas junto das paredes e a mesa do escritório num canto da frente; o saque nos
        // móveis antigos (a unidade do galpão). Nas coordenadas da obra: o meio do galpão no chão, a frente em -z.
        static void MontarGalpao(ObraPronta o)
        {
            var pl = o.Plano;
            float h = pl.Altura, W = pl.Pegada.width, D = pl.Pegada.height;
            const float t = 0.25f, piso = 0.15f;
            var sorte = new System.Random(pl.Semente);
            var col = new MalhaDeObra();
            var m = o.Desenho;
            void Bloco(Vector3 meio, Vector3 tam, string cor, bool colide = true)
            {
                m.Caixa(Matrix4x4.Translate(meio), tam, cor);
                if (colide) col.Caixa(Matrix4x4.Translate(meio), tam, cor);
            }
            // o piso e as paredes do fundo e dos lados
            Bloco(new Vector3(0f, piso * 0.5f - 0.1f, 0f), new Vector3(W, piso + 0.2f, D), "Concreto");
            Bloco(new Vector3(0f, h * 0.5f, D * 0.5f - t * 0.5f), new Vector3(W, h, t), pl.Parede);
            Bloco(new Vector3(-W * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, D - 2f * t), pl.Parede);
            Bloco(new Vector3(W * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, D - 2f * t), pl.Parede);
            // a frente com os vãos: os portões (4 × 4,6 m) e a porta (1 × 2,2 m), da esquerda para a direita
            int portoes = Mathf.Clamp(Mathf.FloorToInt(W / 9f), 1, 4);
            int aberto = sorte.Next(portoes);
            var vaos = new List<(float x0, float x1, float alto, int portao)>();
            for (int k = 0; k < portoes; k++)
            {
                float x = -W * 0.5f + (k + 0.5f) * ((W - 4f) / portoes);
                vaos.Add((x - 2f, x + 2f, 4.6f, k));
            }
            vaos.Add((W * 0.5f - 2.8f, W * 0.5f - 1.8f, 2.2f, -1));
            float zf = -D * 0.5f + t * 0.5f, xa = -W * 0.5f;
            foreach (var (x0, x1, alto, portao) in vaos)
            {
                if (x0 > xa + 0.01f) Bloco(new Vector3((xa + x0) * 0.5f, h * 0.5f, zf), new Vector3(x0 - xa, h, t), pl.Parede);
                Bloco(new Vector3((x0 + x1) * 0.5f, (alto + h) * 0.5f, zf), new Vector3(x1 - x0, h - alto, t), pl.Parede);
                // o portão fechado (a folha de enrolar descida) e a caixa dele em cima, por fora
                if (portao >= 0 && portao != aberto) Bloco(new Vector3((x0 + x1) * 0.5f, alto * 0.5f, zf), new Vector3(x1 - x0, alto, 0.08f), "Metal");
                if (portao >= 0) Bloco(new Vector3((x0 + x1) * 0.5f, alto + 0.25f, zf - 0.3f), new Vector3(x1 - x0 + 0.3f, 0.45f, 0.4f), "Metal", false);
                xa = x1;
            }
            if (W * 0.5f > xa + 0.01f) Bloco(new Vector3((xa + W * 0.5f) * 0.5f, h * 0.5f, zf), new Vector3(W * 0.5f - xa, h, t), pl.Parede);
            // o telhado: duas águas (placas de metal, os dois lados à vista) e os oitões, de fora e de dentro
            float cume = 1.2f, meiaD = D * 0.5f + 0.3f, inclina = Mathf.Atan2(cume, meiaD) * Mathf.Rad2Deg, compr = Mathf.Sqrt(meiaD * meiaD + cume * cume);
            foreach (int lado in new[] { -1, 1 })
            {
                var M = Matrix4x4.TRS(new Vector3(0f, h + cume * 0.5f, lado * meiaD * 0.5f), Quaternion.Euler(lado * inclina, 0f, 0f), Vector3.one);
                m.Caixa(M, new Vector3(W + 0.6f, 0.1f, compr), "Metal");
                col.Caixa(M, new Vector3(W + 0.6f, 0.1f, compr), "Metal");
            }
            foreach (int lado in new[] { -1, 1 })
            {
                float x = lado * (W * 0.5f - 0.01f);
                var tri = new[] { new Vector3(x, h, -D * 0.5f), new Vector3(x, h, D * 0.5f), new Vector3(x, h + cume, 0f) };
                m.Face(tri, new Vector3(lado, 0f, 0f), pl.Parede);
                var tri2 = new[] { new Vector3(x - lado * 0.24f, h, -D * 0.5f), new Vector3(x - lado * 0.24f, h, D * 0.5f), new Vector3(x - lado * 0.24f, h + cume, 0f) };
                m.Face(tri2, new Vector3(-lado, 0f, 0f), pl.Parede);
            }
            // a plataforma das cargas, de fora, ao lado do primeiro portão
            Bloco(new Vector3(-W * 0.25f, 0.6f, -D * 0.5f - 1.5f), new Vector3(Mathf.Min(10f, W * 0.4f), 1.2f, 3f), "Concreto");

            // os móveis (os antigos, com o saque da unidade do galpão)
            var pontos = new List<PontoDeMovel>();
            void Movel(string tipo, Vector3 centro, float giro, Vector3 tam, string comodo, int sala) =>
                pontos.Add(new PontoDeMovel { tipo = tipo, centro = centro, giro = giro, tamanho = tam, saque = true, comodo = comodo, andar = 0, unidade = PontoDeMovel.Galpao, sala = sala });
            // as fileiras de estantes, costas com costas, longe da frente (o pátio de dentro, perto dos portões)
            float zIni = -D * 0.5f + 7f, zFim = D * 0.5f - 1.6f, xIni = -W * 0.5f + 2.2f, xFim = W * 0.5f - 2.2f;
            int estantes = 0;
            for (float z = zIni + 0.6f; z + 0.6f <= zFim && estantes < 48; z += 4.6f)
            {
                int k = 0;
                for (float x = xIni + 0.9f; x + 0.9f <= xFim; x += 1.9f, k++)
                {
                    if (k % 5 == 4) continue;   // (um corredor de través a cada quatro)
                    Movel("estante de aco", new Vector3(x, piso, z - 0.3f), 180f, new Vector3(1.8f, 2.2f, 0.6f), "deposito", 1);
                    Movel("estante de aco", new Vector3(x, piso, z + 0.3f), 0f, new Vector3(1.8f, 2.2f, 0.6f), "deposito", 1);
                    estantes += 2;
                }
            }
            // as caixas no fundo
            for (int k = 0; k < 8; k++)
            {
                float x = Mathf.Lerp(-W * 0.5f + 1f, W * 0.5f - 1f, (float)sorte.NextDouble());
                Movel("caixas", new Vector3(x, piso, D * 0.5f - 0.65f), 0f, new Vector3(0.6f, 0.6f, 0.6f), "deposito", 1);
            }
            // o escritório: a mesa e o arquivo no canto da frente, junto da porta
            Movel("mesa de escritorio", new Vector3(W * 0.5f - 2.5f, piso, -D * 0.5f + 3.2f), 180f, new Vector3(1.4f, 0.75f, 0.7f), "escritorio", 2);
            Movel("arquivo", new Vector3(W * 0.5f - 0.6f, piso, -D * 0.5f + 4.5f), -90f, new Vector3(0.5f, 1.3f, 0.6f), "escritorio", 2);
            Mobilia.Por(pontos, m, col, pl.Semente);

            var pronta = new MalhasDasCasas.CasaPronta
            {
                Nome = pl.Nome,
                Colisao = col,
                Semente = pl.Semente,
                Largura = W,
                Fundo = D,
                CentroDeDentro = new Vector3(0f, h * 0.5f, 0f),
                TamanhoDeDentro = new Vector3(W - 0.6f, h, D - 0.6f),
            };
            pronta.Moveis.AddRange(pontos);
            pronta.Zonas.Add(new MalhasDasCasas.Zona { Centro = new Vector3(0f, piso, 0f), Tamanho = new Vector3(W, 1f, D), Piso = "Concreto" });
            o.Casa = pronta;
        }

        // a loja da marina (23/11/2026): uma comum de um andar, das de esportes (a pesca, as mochilas, as facas: a unidade sai
        // da semente da loja, MoveisDaObra.UnidadeDaLoja)
        static PlanoDaObra DaLojaDaMarina(LoteDoMapa l)
        {
            var p = DaLoja(l, TipoDeLoja.Comum, 1);
            if (p?.Loja != null) p.Loja.semente = (int)(((uint)p.Loja.semente & ~(3u << 5)) | (2u << 5));
            return p;
        }

        static PlanoDaObra DoPosto(LoteDoMapa l)
        {
            // o modelo do posto olha para +z (as bombas e o totem para a estrada): no lote, virado 180° (a frente do lote é -z)
            var meio = new Vector2(0f, -l.fundo * 0.5f + 13f);
            return new PlanoDaObra
            {
                Uso = UsoDoLote.Posto,
                Posto = Mathf.Abs(l.semente) % 3,
                Meio = meio,
                GiroNoLote = 180f,
                Pegada = Rect.MinMaxRect(meio.x - 9.5f, meio.y - 9.5f, meio.x + 9.5f, meio.y + 14.5f),
                Altura = 6f,
                Parede = "ConcretoClaro",
                Telhado = "Metal",
                Semente = l.semente,
            };
        }

        // o Hospital Principal (o HospitalDoMundo monta): o lote no meio do terreno dele (de x -50,5 a 32 e z -30 a 21,5 na
        // obra), a frente da obra (-z) para a rua; a pegada é o terreno inteiro (as calçadas, a praça e os pátios: sem a
        // grama), e o buraco do subsolo é o corpo
        static PlanoDaObra DoHospital(LoteDoMapa l)
        {
            var t = PlantaDoHospital.TerrenoNoMundo;
            var meio = new Vector2(-t.center.x, -t.center.y);
            return new PlanoDaObra
            {
                Uso = UsoDoLote.Hospital,
                Meio = meio,
                Pegada = new Rect(meio.x + t.xMin, meio.y + t.yMin, t.width, t.height),
                Altura = 16f,
                Parede = "TijoloAparente",
                Telhado = "Concreto",
                Semente = l.semente,
            };
        }

        /// <summary>O corpo do hospital no lote (o buraco do subsolo no chão), nas coordenadas do lote.</summary>
        public static Rect CorpoDoHospital(PlanoDaObra p) =>
            Rect.MinMaxRect(p.Meio.x + PlantaDoHospital.X0, p.Meio.y + PlantaDoHospital.Z0, p.Meio.x + PlantaDoHospital.X1, p.Meio.y + PlantaDoHospital.Z1);

        // a loja de armas nova: o lote no meio do terreno dela (de x -16 a 16 e z -29,5 a 20 na obra: o estacionamento na
        // frente), o estado da sala segura pela semente do mapa e a do lote
        static PlanoDaObra DaLojaDeArmas(LoteDoMapa l, int sementeDoMapa)
        {
            var t = PlantaDaLojaDeArmas.Terreno;
            var meio = new Vector2(-t.center.x, -t.center.y);
            return new PlanoDaObra
            {
                Uso = UsoDoLote.LojaDeArmas,
                Meio = meio,
                Pegada = new Rect(meio.x + t.xMin, meio.y + t.yMin, t.width, t.height),
                Altura = 5.9f,
                Parede = "TijoloAparente",
                Telhado = "Concreto",
                Semente = unchecked(l.semente ^ (sementeDoMapa * 92821)) & 0x7fffffff,
            };
        }

        /// <summary>
        /// A obra de verdade (pura: roda em outra thread), nas coordenadas dela: o desenho com a altura sobre o chão na cor
        /// do vértice (a umidade da base), a colisão, as portas e as janelas que abrem, os móveis (os pontos, a mobília nova
        /// quando ela está registrada), o abandono (as janelas, a hera e as pichações) e os pisos.
        /// </summary>
        /// <remarks>
        /// <paramref name="antes"/>: a obra de antes do apocalipse (22/11/2026, a Woodcreek de antes, a etapa 3 do Docs/23):
        /// nova (sem as tábuas, o sangue e o mofo: <see cref="Obra.nova"/>), com todas as janelas inteiras que abrem, sem a
        /// hera, as pichações e as marcas, e a loja de armas intacta. O sorteio da planta é o mesmo: a mesma obra, nova.
        /// </remarks>
        public static ObraPronta Montar(PlanoDaObra plano, bool antes = false)
        {
            var o = new ObraPronta { Plano = plano, Desenho = new MalhaDeObra(), Hera = MalhaDaHera.Emprestar(), Pichacoes = MalhaDePichacao.Emprestar() };
            switch (plano.Uso)
            {
                case UsoDoLote.Casa: case UsoDoLote.Especial: case UsoDoLote.Cabana: MontarCasa(o, antes); break;
                case UsoDoLote.Loja: MontarLoja(o, antes); break;
                case UsoDoLote.Posto: MontarPosto(o, antes); break;
                case UsoDoLote.LojaDeArmas: MontarLojaDeArmas(o, antes); break;
                case UsoDoLote.Predio: MontarPredio(o, antes); break;
                case UsoDoLote.Estacionamento: MontarEstacionamento(o); break;
                case UsoDoLote.Praca: MontarPraca(o); break;
                case UsoDoLote.Galpao: MontarGalpao(o); break;
            }
            return o;
        }

        // a loja de armas (a da cena de teste: a casca e o dentro, as marcas, as pichações, a hera) e o terreno dela (a
        // calçada da frente no nível da porta, com o meio-fio, o estacionamento de asfalto com as vagas e os becos), sem a
        // rua (no Lakewood, a rua é a da rede); o chão de fora 15 cm abaixo da porta, no patamar do lote
        static void MontarLojaDeArmas(ObraPronta o, bool antes)
        {
            var v = antes ? PlantaDaLojaDeArmas.Nova(o.Plano.Semente) : PlantaDaLojaDeArmas.Sortear(o.Plano.Semente);
            o.Variacao = v;
            var col = new MalhaDeObra();
            foreach (var parte in new[] { LojaDeArmas.Parte.Casca, LojaDeArmas.Parte.Dentro })
            {
                var l = new LojaDeArmas(M, parte, v);
                l.Montar();
                if (parte == LojaDeArmas.Parte.Casca && !antes) AbandonoDaObra.Hera(l, AbandonoDaObra.NoMundo, o.Plano.Semente + 5, 6f, o.Hera);
                o.Desenho.Juntar(l.vis, Matrix4x4.identity, false, altura: true);
                col.Juntar(l.col, Matrix4x4.identity, false);
                // (a de antes, sem as marcas e as pichações: elas saem sempre na montagem, e não entram)
                if (!antes)
                {
                    o.Marcas.Add(l.marcas);
                    o.MaisPichacoes.Add(l.pichacoes);
                }
                else
                {
                    MalhaDePichacao.Devolver(l.marcas);
                    MalhaDePichacao.Devolver(l.pichacoes);
                }
                MalhaDeObra.Devolver(l.vis);
                MalhaDeObra.Devolver(l.col);
            }
            const float c = PlantaDaLojaDeArmas.ChaoDeFora, calcada = PlantaDaLojaDeArmas.Calcada;
            float x0 = PlantaDaLojaDeArmas.X0, x1 = PlantaDaLojaDeArmas.X1, z0 = PlantaDaLojaDeArmas.Z0, z1 = PlantaDaLojaDeArmas.Z1;
            float zCalcada = z0 - calcada, zEstacionamento = zCalcada - 12f;
            var chao = new Obra(o.Plano.Semente, M);
            chao.Laje(x0 - 4f, x1 + 4f, zCalcada, z0, 0f, 0.3f, "Calcada", true);
            for (float x = x0 - 2f; x < x1 + 4f; x += 2f)
                chao.Caixa(new Vector3(x, 0.002f, (zCalcada + z0) / 2f), new Vector3(0.03f, 0.004f, calcada), "Concreto");
            chao.Caixa(new Vector3(0f, -0.075f, zCalcada - 0.08f), new Vector3(x1 - x0 + 8f, 0.15f, 0.16f), "MeioFio", colide: true);
            chao.Laje(x0 - 4f, x1 + 4f, zEstacionamento, zCalcada, c + 0.02f, 0.3f, "Asfalto", true);
            for (float x = x0 - 2.8f; x < x1 + 3f; x += 2.8f)
                chao.Caixa(new Vector3(x, c + 0.024f, zCalcada - 2.9f), new Vector3(0.12f, 0.008f, 5.2f), "ListraBranca");
            for (float x = x0 - 1.4f; x < x1 + 2f; x += 2.8f)
                chao.Caixa(new Vector3(x, c + 0.08f, zCalcada - 0.9f), new Vector3(1.6f, 0.12f, 0.2f), "Concreto", colide: true);
            chao.Laje(x0 - 4f, x1 + 4f, z0, z1 + 5f, c + 0.02f, 0.3f, "Concreto", true, new List<Rect> { Rect.MinMaxRect(x0, z0, x1, z1) });
            o.Desenho.Juntar(chao.vis, Matrix4x4.identity, false, altura: true);
            col.Juntar(chao.col, Matrix4x4.identity, false);
            MalhaDeObra.Devolver(chao.vis);
            MalhaDeObra.Devolver(chao.col);
            o.MoveisDaLoja.AddRange(MoveisDaLojaDeArmas.Da(v));
            o.Casa = new MalhasDasCasas.CasaPronta
            {
                Nome = "Loja de armas",
                Colisao = col,
                Semente = o.Plano.Semente,
                CentroDeDentro = new Vector3(0f, 1.9f, 0f),
                TamanhoDeDentro = new Vector3(x1 - x0 - 0.2f, 3.8f, z1 - z0 - 0.2f),
            };
        }

        static void MontarCasa(ObraPronta o, bool antes)
        {
            var p = o.Plano.Casa;
            var M = o.Plano.Medidas ?? ObrasDeLakewood.M;
            var casa = new Casa(p, M) { portasVivas = true, nova = antes };
            casa.Montar();
            if (o.Plano.Especial != null) EspeciaisDeLakewood.Extras(casa, o.Plano.Especial);
            Abandonar(casa, p.semente, 4f, p.espelhar, o, antes);
            var pronta = new MalhasDasCasas.CasaPronta
            {
                Nome = o.Plano.Nome,
                Colisao = Espelhada(casa.col, p.espelhar),
                Largura = p.W,
                Fundo = p.D,
                Triangulos = casa.vis.Triangulos,
                Semente = p.semente,
            };
            o.Desenho.Juntar(casa.vis, Matrix4x4.identity, p.espelhar, altura: true);
            MalhaDeObra.Devolver(casa.vis);
            foreach (var pm in casa.moveis) pronta.Moveis.Add(p.espelhar ? pm.Espelhado() : pm);
            foreach (var pd in casa.portas) pronta.Portas.Add(p.espelhar ? pd.Espelhada() : pd);
            foreach (var jd in casa.janelas) pronta.Janelas.Add(p.espelhar ? jd.Espelhada() : jd);
            pronta.MobiliaNova = MobiliaNova.Pronta ? NovaDe(pronta) : null;
            float sx = p.espelhar ? -1f : 1f;
            void Zona(float xa, float xb, float za, float zb, float y, string piso)
            {
                float a = sx * xa, b = sx * xb;
                pronta.Zonas.Add(new MalhasDasCasas.Zona { Centro = new Vector3((a + b) / 2, y, (za + zb) / 2), Tamanho = new Vector3(Mathf.Abs(b - a), 1f, zb - za), Piso = piso });
            }
            foreach (var k in p.comodos) Zona(k.x0, k.x1, k.z0, k.z1, casa.Base(k.andar), k.piso ?? "Taco");
            var g = p.garagem;
            if (g != null)
            {
                float xi = g.lado > 0 ? p.X1 : p.X0, xo = xi + g.lado * g.largura;
                Zona(Mathf.Min(xi, xo), Mathf.Max(xi, xo), g.z0, g.z1, 0.05f, "Concreto");
            }
            float baixo = casa.Base(0), alto = casa.Base(p.andares - 1) + M.altura + M.forro;
            pronta.CentroDeDentro = new Vector3(0f, (baixo + alto) / 2, 0f);
            pronta.TamanhoDeDentro = new Vector3(p.W - 0.2f, alto - baixo, p.D - 0.2f);
            o.Casa = pronta;
            o.Carros.AddRange(casa.Carros);
        }

        // a casca do prédio (as fachadas de todos os andares, as lajes, o núcleo da escada e do poço, a cobertura); os andares
        // por dentro são do AndaresDoPredio, perto de alguém (como no mapa sorteado: MalhasDasCasas.MontarPredio)
        static void MontarPredio(ObraPronta o, bool antes)
        {
            var p = o.Plano.Predio;
            var predio = new Predio(p, M, -1) { nova = antes };
            predio.MontarCasca((x, z) => 0f, 0f);
            Abandonar(predio, p.semente, 7f, p.espelhar, o, antes);
            float sx = p.espelhar ? -1f : 1f;
            var pronta = new MalhasDasCasas.CasaPronta
            {
                Nome = o.Plano.Nome,
                Colisao = Espelhada(predio.col, p.espelhar),
                Largura = p.W,
                Fundo = p.D,
                Triangulos = predio.vis.Triangulos,
                Semente = p.semente,
            };
            o.Desenho.Juntar(predio.vis, Matrix4x4.identity, p.espelhar, altura: true);
            MalhaDeObra.Devolver(predio.vis);
            o.Escuros = new MalhaDeObra[p.andares];
            for (int f = 0; f < p.andares && f < predio.Escuros.Count; f++) o.Escuros[f] = Espelhada(predio.Escuros[f], p.espelhar);
            foreach (var k in p.terreo)
            {
                float a = sx * k.x0, b = sx * k.x1;
                pronta.Zonas.Add(new MalhasDasCasas.Zona { Centro = new Vector3((a + b) / 2, PlantaDePredio.Piso, (k.z0 + k.z1) / 2), Tamanho = new Vector3(Mathf.Abs(b - a), 1f, k.z1 - k.z0), Piso = p.PisoDe(0, k) });
            }
            float baixo = PlantaDePredio.Piso, alto = p.Cobertura - M.laje;
            pronta.CentroDeDentro = new Vector3(0f, (baixo + alto) / 2, 0f);
            pronta.TamanhoDeDentro = new Vector3(p.W - 0.2f, alto - baixo, p.D - 0.2f);
            o.Casa = pronta;
        }

        // o prédio de longe: o corpo, as faixas de janelas de cada andar nos quatro lados, o vidro do térreo na frente e a casa
        // de máquinas em cima (como no mapa sorteado: MalhasDaCidade)
        static void PredioDeLonge(PlanoDaObra p, MalhaDeObra m)
        {
            var pl = p.Predio;
            var c = p.Meio;
            float topo = pl.Cobertura + 1f, W = pl.W, D = pl.D;
            m.Caixa(Matrix4x4.Translate(new Vector3(c.x, topo * 0.5f, c.y)), new Vector3(W - 0.1f, topo, D - 0.1f), pl.cor);
            for (int f = 1; f < pl.andares; f++)
            {
                float y0 = pl.Base(f) + 0.9f, h = 1.5f, y = y0 + h * 0.5f;
                m.Caixa(Matrix4x4.Translate(new Vector3(c.x, y, c.y - D * 0.5f)), new Vector3(W * 0.9f, h, 0.06f), "Vidro");
                m.Caixa(Matrix4x4.Translate(new Vector3(c.x, y, c.y + D * 0.5f)), new Vector3(W * 0.9f, h, 0.06f), "Vidro");
                m.Caixa(Matrix4x4.Translate(new Vector3(c.x - W * 0.5f, y, c.y)), new Vector3(0.06f, h, D * 0.9f), "Vidro");
                m.Caixa(Matrix4x4.Translate(new Vector3(c.x + W * 0.5f, y, c.y)), new Vector3(0.06f, h, D * 0.9f), "Vidro");
            }
            m.Caixa(Matrix4x4.Translate(new Vector3(c.x, 1.9f, c.y - D * 0.5f)), new Vector3(W * 0.7f, 3f, 0.08f), "Vidro");
            m.Caixa(Matrix4x4.Translate(new Vector3(c.x, topo + 1.4f, c.y)), new Vector3(Mathf.Min(6f, W * 0.3f), 2.8f, Mathf.Min(5f, D * 0.3f)), "Concreto");
        }

        static void MontarLoja(ObraPronta o, bool antes)
        {
            var p = o.Plano.Loja;
            var loja = new Loja(p, M) { nova = antes };
            loja.Montar((x, z) => 0f, 0f);
            // os móveis antigos do salão, dos fundos e do escritório (23/11/2026: o Lakewood não os desenhava, e a loja ficava
            // vazia por dentro), com os produtos do modelo (o Pedro: no apocalipse, eles ficam: "dá imersão de algo deserto");
            // no Dia 0, não (as prateleiras do dia vêm na cena, sem os itens do modelo)
            if (!antes) Mobilia.Por(loja.moveis, loja.vis, loja.col, p.semente);
            Abandonar(loja, p.semente, 5f, p.espelhar, o, antes);
            var pronta = new MalhasDasCasas.CasaPronta
            {
                Nome = o.Plano.Nome,
                Colisao = Espelhada(loja.col, p.espelhar),
                Largura = p.W,
                Fundo = p.D,
                Triangulos = loja.vis.Triangulos,
                Semente = p.semente,
            };
            o.Desenho.Juntar(loja.vis, Matrix4x4.identity, p.espelhar, altura: true);
            MalhaDeObra.Devolver(loja.vis);
            foreach (var pm in loja.moveis) pronta.Moveis.Add(p.espelhar ? pm.Espelhado() : pm);
            pronta.MobiliaNova = MobiliaNova.Pronta ? NovaDe(pronta) : null;
            float sx = p.espelhar ? -1f : 1f;
            foreach (var k in p.comodos)
            {
                float a = sx * k.x0, b = sx * k.x1;
                pronta.Zonas.Add(new MalhasDasCasas.Zona { Centro = new Vector3((a + b) / 2, p.Base(k.andar), (k.z0 + k.z1) / 2), Tamanho = new Vector3(Mathf.Abs(b - a), 1f, k.z1 - k.z0), Piso = k.piso ?? "Piso" });
            }
            float baixo = PlantaDeLoja.Piso, alto = p.Cobertura - M.laje;
            pronta.CentroDeDentro = new Vector3(0f, (baixo + alto) / 2, 0f);
            pronta.TamanhoDeDentro = new Vector3(p.W - 0.2f, alto - baixo, p.D - 0.2f);
            o.Casa = pronta;
        }

        static void MontarPosto(ObraPronta o, bool antes)
        {
            // (o de antes, sem os móveis antigos: as prateleiras do Dia 0 são desenhadas na cena, sem os itens do modelo)
            var m = ModelosDaEstrada.Posto(o.Plano.Posto, semMobilia: antes);
            o.Desenho.Juntar(m.Malha, Matrix4x4.identity, false, altura: true);
            var pronta = new MalhasDasCasas.CasaPronta
            {
                Nome = "Posto",
                Colisao = m.MalhaDeColisao ?? new MalhaDeObra(),
                Semente = o.Plano.Semente,
                CentroDeDentro = m.CentroDeDentro,
                TamanhoDeDentro = m.TamanhoDeDentro,
            };
            pronta.Moveis.AddRange(m.Moveis);
            pronta.Zonas.AddRange(m.Zonas);
            o.Caixas.AddRange(m.Colisao);
            o.Casa = pronta;
        }

        // o abandono da obra (as janelas quebradas e tapadas, a hera e as pichações), como no mundo sorteado
        static void Abandonar(Obra obra, int semente, float alturaDaHera, bool espelhar, ObraPronta o, bool antes)
        {
            // a de antes do apocalipse: só as janelas inteiras que abrem
            if (antes)
            {
                AbandonoDaObra.JanelasNovas(obra);
                return;
            }
            // o Levar diz para onde vai o que entra depois (aqui, só o espelho da obra)
            o.Pichacoes.Levar(Matrix4x4.identity, espelhar);
            o.Hera.Levar(Matrix4x4.identity, espelhar);
            AbandonoDaObra.Janelas(obra, AbandonoDaObra.NoMundo, semente, o.Pichacoes);
            AbandonoDaObra.Hera(obra, AbandonoDaObra.NoMundo, semente, alturaDaHera, o.Hera);
            AbandonoDaObra.Pichacoes(obra, AbandonoDaObra.NoMundo, semente, o.Pichacoes);
        }

        static MalhaDaMobilia NovaDe(MalhasDasCasas.CasaPronta c)
        {
            MobiliaNova.Caixas(c.Moveis, c.Colisao);
            return MobiliaNova.Desenho(c.Moveis, c.Semente);
        }

        static MalhaDeObra Espelhada(MalhaDeObra m, bool espelhar)
        {
            if (espelhar) m.Espelhar();
            return m;
        }

        /// <summary>
        /// A casa de longe (nas coordenadas da obra, sem espelhar): o mesmo corpo, o mesmo telhado (a cumeeira para o mesmo
        /// lado, a mesma altura e o beiral), os oitões, a varanda com o telhadinho e os pilares, a garagem com o telhado de
        /// uma água, a chaminé, a porta da frente e as janelas (escuras, a cada uns 3,6 m de cada parede de cada andar, fora
        /// da porta e da garagem). Uns 300 a 600 triângulos, no lugar dos 5 a 16 mil da de verdade.
        /// </summary>
        public static void CasaDeLonge(Planta pl, Medidas md, MalhaDeObra m)
        {
            float piso = Casa.PisoDe(pl), x0 = pl.X0, x1 = pl.X1, z0 = pl.Z0, z1 = pl.Z1, esp = md.esp;
            float Base(int f) => piso + f * (md.altura + md.laje);
            float forro = Base(pl.andares - 1) + md.altura + md.forro;
            // o corpo (do chão até o forro)
            m.Caixa(Matrix4x4.Translate(new Vector3(0f, forro * 0.5f, 0f)), new Vector3(pl.W + esp, forro, pl.D + esp), pl.cor);
            // o telhado de duas águas, como o Casa.Telhado
            bool noX = pl.cumeeiraNoX;
            float S = noX ? pl.D : pl.W, L = noX ? pl.W : pl.D;
            float ho = S / 2 + 0.45f, C = 0.367f * ho, topo = forro + C, prof = L + 0.9f, hw = S / 2 + esp / 2;
            Vector3 P(float x, float y, float z) => noX ? new Vector3(z, y, -x) : new Vector3(x, y, z);
            Vector3 N(float x, float y, float z) => noX ? new Vector3(z, y, -x) : new Vector3(x, y, z);
            foreach (int sl in new[] { 1, -1 })
            {
                m.Face(new[] { P(sl * ho, forro - 0.05f, -prof / 2), P(sl * ho, forro - 0.05f, prof / 2), P(0f, topo + 0.1f, prof / 2), P(0f, topo + 0.1f, -prof / 2) },
                       N(sl * C, ho, 0f), pl.telhado);
                // o forro do beiral (de baixo)
                m.Face(new[] { P(sl * ho, forro - 0.06f, -prof / 2), P(sl * hw, forro - 0.06f, -prof / 2), P(sl * hw, forro - 0.06f, prof / 2), P(sl * ho, forro - 0.06f, prof / 2) },
                       Vector3.down, "Batente");
            }
            // os oitões
            foreach (int e in new[] { 1, -1 })
            {
                float zc = e * (L / 2 + esp / 2);
                m.Face(new[] { P(-hw, forro, zc), P(hw, forro, zc), P(0f, topo, zc) }, N(0f, 0f, e), pl.cor);
            }
            // a porta da frente e as janelas
            var g = pl.garagem;
            for (int f = 0; f < pl.andares; f++)
            {
                float yb = Base(f);
                for (int lado = 0; lado < 4; lado++)
                {
                    // o lado: 0 a frente (z0), 1 a direita (x1), 2 o fundo (z1), 3 a esquerda (x0); s corre ao longo dele
                    bool emX = lado == 0 || lado == 2;
                    float comp = emX ? pl.W : pl.D, s0 = emX ? x0 : z0;
                    float n = Mathf.Max(0, Mathf.Floor((comp - 1.4f) / 3.6f));
                    for (int k = 0; k < n; k++)
                    {
                        float sc = s0 + (k + 0.5f) * comp / n;
                        if (lado == 0 && f == 0 && Mathf.Abs(sc - pl.xPortaFrente) < 1.6f) continue;
                        if (g != null && g.coberta && f == 0 && (lado == 1 && g.lado > 0 || lado == 3 && g.lado < 0) && sc > g.z0 - 0.8f && sc < g.z1 + 0.8f) continue;
                        if (pl.chamine != 0 && (lado == 1 && pl.chamine > 0 || lado == 3 && pl.chamine < 0) && Mathf.Abs(sc - (z1 - pl.D * 0.525f)) < 1.2f) continue;
                        Janela(m, lado, sc, yb + md.peitoril, yb + md.topoJanela, md.janela, x0, x1, z0, z1, esp);
                    }
                }
            }
            // a porta da frente (a cor dela)
            {
                float zf = z0 - esp / 2 - 0.02f, a = pl.xPortaFrente - md.portaFrente / 2, b = pl.xPortaFrente + md.portaFrente / 2;
                m.Face(new[] { new Vector3(a, piso, zf), new Vector3(a, piso + md.alturaPortaFrente, zf), new Vector3(b, piso + md.alturaPortaFrente, zf), new Vector3(b, piso, zf) },
                       Vector3.back, pl.corDaPorta);
            }
            // a varanda: o deque, o telhadinho e os pilares
            if (pl.varanda)
            {
                float xa, xb;
                if (pl.varandaLarga) { xa = x0 + 0.4f; xb = x1 - 0.4f; }
                else
                {
                    float meia = Mathf.Max(1.7f, md.portaFrente / 2 + 1.15f);
                    xa = Mathf.Max(x0 + 0.3f, pl.xPortaFrente - meia);
                    xb = Mathf.Min(x1 - 0.3f, pl.xPortaFrente + meia);
                }
                float zf = z0 - esp / 2, pr = pl.fundoDaVaranda, xc = (xa + xb) / 2, larg = xb - xa;
                m.Caixa(Matrix4x4.Translate(new Vector3(xc, piso / 2, zf - pr / 2)), new Vector3(larg, piso + 0.02f, pr), "MadeiraVelha");
                float yTeto = piso + md.altura - 0.09f;
                m.Caixa(Matrix4x4.Translate(new Vector3(xc, yTeto, zf - (pr + 0.1f) / 2)), new Vector3(larg + 0.35f, 0.1f, pr + 0.3f), pl.telhado);
                foreach (float xp in new[] { xa + 0.1f, xb - 0.1f })
                    m.Caixa(Matrix4x4.Translate(new Vector3(xp, (piso + yTeto) / 2, zf - pr + 0.1f)), new Vector3(0.12f, yTeto - piso, 0.12f), "Batente");
            }
            // a garagem (o corpo baixo e o telhado de uma água subindo até a parede da casa) ou nada (a vaga é do chão)
            if (g != null && g.coberta)
            {
                float xi = g.lado > 0 ? x1 + esp / 2 : x0 - esp / 2, xo = xi + g.lado * g.largura;
                float xa = Mathf.Min(xi, xo), xb = Mathf.Max(xi, xo), zc = (g.z0 + g.z1) / 2, lz = g.z1 - g.z0;
                const float alto = 2.7f, sobe = 0.5f;
                m.Caixa(Matrix4x4.Translate(new Vector3((xa + xb) / 2, alto / 2, zc)), new Vector3(xb - xa, alto, lz), pl.cor);
                float xBeira = xo + g.lado * 0.25f;
                var nrm = new Vector3(g.lado * sobe, g.largura + 0.25f, 0f);
                m.Face(new[] { new Vector3(xBeira, alto, g.z0 - 0.3f), new Vector3(xi, alto + sobe, g.z0 - 0.3f), new Vector3(xi, alto + sobe, g.z1 + 0.3f), new Vector3(xBeira, alto, g.z1 + 0.3f) },
                       nrm, pl.telhado);
                // o portão (fechado ou o vão escuro)
                float pc = (xa + xb) / 2, pa = pc - 1.35f, pb = pc + 1.35f, zp = g.z0 - 0.02f;
                m.Face(new[] { new Vector3(pa, 0f, zp), new Vector3(pa, 2.3f, zp), new Vector3(pb, 2.3f, zp), new Vector3(pb, 0f, zp) }, Vector3.back, g.fechada ? "Batente" : "Pneu");
            }
            // a chaminé, passando da cumeeira
            if (pl.chamine != 0)
            {
                float x = (pl.chamine > 0 ? x1 : x0) + pl.chamine * (esp / 2 + 0.38f), z = z1 - pl.D * 0.525f, h = topo + 0.6f;
                m.Caixa(Matrix4x4.Translate(new Vector3(x, h / 2, z)), new Vector3(0.7f, h, 0.9f), "Tijolo");
            }
        }

        // uma janela de longe: o vão escuro e a moldura clara em volta, 2 cm para fora da parede
        static void Janela(MalhaDeObra m, int lado, float s, float y0, float y1, float larg, float x0, float x1, float z0, float z1, float esp)
        {
            float a = s - larg / 2, b = s + larg / 2, f = esp / 2 + 0.02f, mo = 0.08f;
            Vector3 Q(float u, float y, float sai) => lado switch
            {
                0 => new Vector3(u, y, z0 - sai),
                2 => new Vector3(-u, y, z1 + sai),
                1 => new Vector3(x1 + sai, y, u),
                _ => new Vector3(x0 - sai, y, -u),
            };
            var fora = lado switch { 0 => Vector3.back, 2 => Vector3.forward, 1 => Vector3.right, _ => Vector3.left };
            // no fundo e na esquerda, o s corre ao contrário (a face fica olhando para fora com a mesma ordem)
            float ua = lado == 2 || lado == 3 ? -b : a, ub = lado == 2 || lado == 3 ? -a : b;
            m.Face(new[] { Q(ua - mo, y0 - mo, f), Q(ua - mo, y1 + mo, f), Q(ub + mo, y1 + mo, f), Q(ub + mo, y0 - mo, f) }, fora, "Batente");
            m.Face(new[] { Q(ua, y0, f + 0.01f), Q(ua, y1, f + 0.01f), Q(ub, y1, f + 0.01f), Q(ub, y0, f + 0.01f) }, fora, "Pneu");
        }

        /// <summary>
        /// A caixa de longe de uma obra (nas coordenadas do lote, no chão dele): as paredes da pegada até a altura e o
        /// telhado (de duas águas nas casas, chato nas lojas e no posto).
        /// </summary>
        public static void CaixaDeLonge(PlanoDaObra p, MalhaDeObra m)
        {
            if (p.Predio != null)
            {
                PredioDeLonge(p, m);
                return;
            }
            if (p.Uso == UsoDoLote.Estacionamento) return;   // (rente ao chão: de longe, nada)
            if (p.Uso == UsoDoLote.Praca)
            {
                m.Cilindro(Matrix4x4.Translate(new Vector3(p.Pegada.center.x, 0f, p.Pegada.center.y)), 3.4f, 3.4f, 0.6f, 10, "Pedra");
                return;
            }
            // as casas, as cabanas e os especiais: a forma da casa de verdade (12/11/2026, a pedido: "consigo ver as casas
            // mudando de formato de longe")
            if (p.Casa != null)
            {
                var tmp = MalhaDeObra.Emprestar();
                CasaDeLonge(p.Casa, p.Medidas ?? M, tmp);
                if (p.Especial == "igreja") EspeciaisDeLakewood.TorreDeLonge(p.Casa, p.Medidas ?? M, tmp);
                m.Juntar(tmp, Matrix4x4.Translate(new Vector3(p.Meio.x, 0f, p.Meio.y)) * Matrix4x4.Rotate(Quaternion.Euler(0f, p.GiroNoLote, 0f)), p.Casa.espelhar, altura: true);
                MalhaDeObra.Devolver(tmp);
                return;
            }
            var r = p.Pegada;
            float h = p.Altura;
            var meio = new Vector3(r.center.x, h * 0.5f, r.center.y);
            m.Caixa(Matrix4x4.Translate(meio), new Vector3(r.width - 0.4f, h, r.height - 0.4f), p.Parede);
            if (p.Casa == null) return;
            // as duas águas ao longo do comprido
            float meiaL = r.width * 0.5f, meiaF = r.height * 0.5f, cume = Mathf.Min(r.width, r.height) * 0.28f;
            bool aoLongoDeX = r.width >= r.height;
            Vector3 P(float a, float b, float y) => aoLongoDeX ? new Vector3(r.center.x + a, y, r.center.y + b) : new Vector3(r.center.x + b, y, r.center.y + a);
            float c = aoLongoDeX ? meiaL : meiaF, e = aoLongoDeX ? meiaF : meiaL;
            m.Face(new[] { P(-c, -e, h), P(c, -e, h), P(c, 0f, h + cume), P(-c, 0f, h + cume) }, aoLongoDeX ? new Vector3(0, 1, -1) : new Vector3(-1, 1, 0), p.Telhado);
            m.Face(new[] { P(-c, e, h), P(c, e, h), P(c, 0f, h + cume), P(-c, 0f, h + cume) }, aoLongoDeX ? new Vector3(0, 1, 1) : new Vector3(1, 1, 0), p.Telhado);
            m.Face(new[] { P(-c, -e, h), P(-c, e, h), P(-c, 0f, h + cume) }, aoLongoDeX ? Vector3.left : Vector3.back, p.Parede);
            m.Face(new[] { P(c, -e, h), P(c, e, h), P(c, 0f, h + cume) }, aoLongoDeX ? Vector3.right : Vector3.forward, p.Parede);
        }
    }
}
