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

        public string Nome => Uso switch
        {
            UsoDoLote.Casa => Casa != null ? (Casa.molde == "Terrea" ? "Casa térrea" : "Sobrado") : "Casa",
            UsoDoLote.Loja => Loja != null ? PlantaDeLoja.Nome(Loja.tipo) + (Loja.andares > 1 ? $" de {Loja.andares} andares" : "") : "Loja",
            UsoDoLote.Posto => "Posto",
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
                if (mapa.lotes[i].cidade == cidade && mapa.lotes[i].tipo == UsoDoLote.Loja) lojas.Add(i);
            // a cota das lojas, embaralhada pela semente do mapa e o nome da cidade
            var cota = new List<TipoDeLoja> { TipoDeLoja.Mercado, TipoDeLoja.Farmacia, TipoDeLoja.VinteQuatroHoras, TipoDeLoja.VinteQuatroHoras };
            while (cota.Count < lojas.Count) cota.Add(TipoDeLoja.Comum);
            var sorte = new System.Random(unchecked(mapa.semente * 7919 + cidade.Length * 104729 + cidade[0]));
            for (int k = cota.Count - 1; k > 0; k--)
            {
                int j = sorte.Next(k + 1);
                (cota[k], cota[j]) = (cota[j], cota[k]);
            }
            for (int i = 0; i < mapa.lotes.Count; i++)
            {
                var l = mapa.lotes[i];
                if (l.cidade != cidade) continue;
                PlanoDaObra p = null;
                switch (l.tipo)
                {
                    case UsoDoLote.Casa: p = DaCasa(l); break;
                    case UsoDoLote.Loja: p = DaLoja(l, cota[lojas.IndexOf(i)]); break;
                    case UsoDoLote.Posto: p = DoPosto(l); break;
                    case UsoDoLote.Hospital: p = DoHospital(l); break;
                    case UsoDoLote.LojaDeArmas: p = DaLojaDeArmas(l, mapa.semente); break;
                    case UsoDoLote.Especial: p = DoEspecial(l, l.nome); break;
                    case UsoDoLote.Cabana: p = DoEspecial(l, "cabana"); break;
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

        static PlanoDaObra DaLoja(LoteDoMapa l, TipoDeLoja tipo)
        {
            var r = new System.Random(l.semente);
            string[] cores = { "ParedeBege", "Tijolo", "ParedeAzul", "ParedeVerde", "TijoloEscuro" };
            int andares = tipo == TipoDeLoja.Comum && r.NextDouble() < 0.4 ? 2 : 1;
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
        public static ObraPronta Montar(PlanoDaObra plano)
        {
            var o = new ObraPronta { Plano = plano, Desenho = new MalhaDeObra(), Hera = MalhaDaHera.Emprestar(), Pichacoes = MalhaDePichacao.Emprestar() };
            switch (plano.Uso)
            {
                case UsoDoLote.Casa: case UsoDoLote.Especial: case UsoDoLote.Cabana: MontarCasa(o); break;
                case UsoDoLote.Loja: MontarLoja(o); break;
                case UsoDoLote.Posto: MontarPosto(o); break;
                case UsoDoLote.LojaDeArmas: MontarLojaDeArmas(o); break;
            }
            return o;
        }

        // a loja de armas (a da cena de teste: a casca e o dentro, as marcas, as pichações, a hera) e o terreno dela (a
        // calçada da frente no nível da porta, com o meio-fio, o estacionamento de asfalto com as vagas e os becos), sem a
        // rua (no Lakewood, a rua é a da rede); o chão de fora 15 cm abaixo da porta, no patamar do lote
        static void MontarLojaDeArmas(ObraPronta o)
        {
            var v = PlantaDaLojaDeArmas.Sortear(o.Plano.Semente);
            o.Variacao = v;
            var col = new MalhaDeObra();
            foreach (var parte in new[] { LojaDeArmas.Parte.Casca, LojaDeArmas.Parte.Dentro })
            {
                var l = new LojaDeArmas(M, parte, v);
                l.Montar();
                if (parte == LojaDeArmas.Parte.Casca) AbandonoDaObra.Hera(l, AbandonoDaObra.NoMundo, o.Plano.Semente + 5, 6f, o.Hera);
                o.Desenho.Juntar(l.vis, Matrix4x4.identity, false, altura: true);
                col.Juntar(l.col, Matrix4x4.identity, false);
                o.Marcas.Add(l.marcas);
                o.MaisPichacoes.Add(l.pichacoes);
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

        static void MontarCasa(ObraPronta o)
        {
            var p = o.Plano.Casa;
            var M = o.Plano.Medidas ?? ObrasDeLakewood.M;
            var casa = new Casa(p, M) { portasVivas = true };
            casa.Montar();
            if (o.Plano.Especial != null) EspeciaisDeLakewood.Extras(casa, o.Plano.Especial);
            Abandonar(casa, p.semente, 4f, p.espelhar, o);
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

        static void MontarLoja(ObraPronta o)
        {
            var p = o.Plano.Loja;
            var loja = new Loja(p, M);
            loja.Montar((x, z) => 0f, 0f);
            Abandonar(loja, p.semente, 5f, p.espelhar, o);
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

        static void MontarPosto(ObraPronta o)
        {
            var m = ModelosDaEstrada.Posto(o.Plano.Posto);
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
        static void Abandonar(Obra obra, int semente, float alturaDaHera, bool espelhar, ObraPronta o)
        {
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
