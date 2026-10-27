using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As casas de verdade de um bloco de 64 m (Plano_Mapa, Parte M6, primeira fatia), em dados (seguras em outra thread;
    /// os Mesh saem na principal), para quem está perto. Longe, as casas são as caixas de <see cref="MalhasDaCidade"/>.
    ///  - <b>o desenho</b> (<see cref="DoBloco.Desenho"/>): as casas do bloco (o kit das construções, <see cref="Casa"/>)
    ///    numa malha só, já no mapa, mais o caminho de concreto do pé da escada da frente até a calçada e a entrada do
    ///    carro, seguindo o chão;
    ///  - <b>cada casa</b> (<see cref="CasaPronta"/>): onde ela fica, a colisão (nas coordenadas dela, já espelhada), os
    ///    cômodos com o piso de cada um (para os passos), a caixa de dentro (a neblina de dentro) e o tamanho (o eco). Com
    ///    porão, a calçada de concreto em volta dos buracos do terreno (<see cref="GeradorDoMundo"/> tira as células que
    ///    encostam no porão e no fosso), no patamar, entra no desenho e na colisão.
    /// Cada casa é do bloco onde cai o meio do lote dela (<see cref="TracadoDaCidade.Bloco"/>).
    /// O abandono (Parte M9c, o grau "muito" da cena de testes; <see cref="AbandonoDaObra"/>): as janelas quebradas e
    /// tapadas entram no desenho; a hera e as pichações de todas as obras do bloco, em duas malhas à parte
    /// (<see cref="DoBloco.Hera"/>, <see cref="DoBloco.Pichacoes"/>). O desenho guarda a altura de cada vértice sobre o
    /// chão da obra (a umidade na base das paredes, no shader do abandono).
    /// A mobília ("A mobília", primeira fatia; <see cref="Mobilia"/>): os móveis das casas e das lojas do bloco numa
    /// malha à parte (<see cref="DoBloco.Mobilia"/>, que aparece só perto), e uma caixa por móvel na colisão de cada uma;
    /// nos andares dos prédios, no desenho e na colisão do andar (que já carregam só perto).
    /// </summary>
    public static class MalhasDasCasas
    {
        /// <summary>Um cômodo (ou a garagem, ou a varanda): a caixa nas coordenadas da casa (já espelhadas) e o piso (o nome da cor).</summary>
        public struct Zona
        {
            public Vector3 Centro, Tamanho;
            public string Piso;
        }

        public sealed class CasaPronta
        {
            public string Nome;
            /// <summary>No mapa: o meio da casa no chão dela (o patamar) e o giro em y (graus).</summary>
            public Vector3 Posicao;
            public float Giro;
            /// <summary>A colisão, nas coordenadas da casa (já espelhada).</summary>
            public MalhaDeObra Colisao;
            public readonly List<Zona> Zonas = new List<Zona>();
            /// <summary>A caixa de dentro (das lajes ao forro), nas coordenadas da casa.</summary>
            public Vector3 CentroDeDentro, TamanhoDeDentro;
            public float Largura, Fundo;
            public int Triangulos;
            /// <summary>Os pontos dos móveis (nas coordenadas da casa, já espelhados).</summary>
            public readonly List<PontoDeMovel> Moveis = new List<PontoDeMovel>();
        }

        /// <summary>Um prédio (Parte M6, segunda fatia): a casca vai para o desenho do bloco; o resto, aqui.</summary>
        public sealed class PredioPronto
        {
            public string Nome;
            /// <summary>No mapa: o meio do prédio no chão dele e o giro em y (graus).</summary>
            public Vector3 Posicao;
            public float Giro;
            public PlantaDePredio Planta;
            /// <summary>A colisão da casca, nas coordenadas do prédio (já espelhada).</summary>
            public MalhaDeObra Colisao;
            /// <summary>As janelas escuras de cada andar, nas coordenadas do prédio (já espelhadas).</summary>
            public MalhaDeObra[] Escuros;
            /// <summary>Os pisos do térreo (a laje dele é da casca).</summary>
            public readonly List<Zona> Zonas = new List<Zona>();
            /// <summary>A caixa de dentro (do térreo até embaixo da cobertura), nas coordenadas do prédio.</summary>
            public Vector3 CentroDeDentro, TamanhoDeDentro;
            public int Triangulos;
        }

        /// <summary>Um andar de um prédio por dentro (montado só perto de alguém), nas coordenadas do prédio (já espelhado).</summary>
        public sealed class AndarPronto
        {
            public int Andar;
            public MalhaDeObra Desenho, Colisao;
            public readonly List<Zona> Zonas = new List<Zona>();
            /// <summary>Os pontos dos móveis do andar (nas coordenadas do prédio, já espelhados).</summary>
            public readonly List<PontoDeMovel> Moveis = new List<PontoDeMovel>();
        }

        public sealed class DoBloco
        {
            public MalhaDeObra Desenho;
            /// <summary>A hera e as pichações das obras do bloco, já no mapa (do estoque; <see cref="Devolver(DoBloco)"/>).</summary>
            public MalhaDaHera Hera;
            public MalhaDePichacao Pichacoes;
            /// <summary>Os móveis das casas e das lojas do bloco, já no mapa (do estoque; null sem a <see cref="Mobilia"/>).</summary>
            public MalhaDeObra Mobilia;
            public readonly List<CasaPronta> Casas = new List<CasaPronta>();
            public readonly List<PredioPronto> Predios = new List<PredioPronto>();
            public bool Vazio => Casas.Count == 0 && Predios.Count == 0;
        }

        static readonly Medidas M = Medidas.DoJogo;

        /// <summary>O bloco tem casas (sem montar nada).</summary>
        public static bool TemCasas(DadosDoMundo d, int bx, int bz)
        {
            var lista = d.CidadesDoBloco(bx, bz);
            if (lista == null || d.Plano == null) return false;
            foreach (int ti in lista)
            {
                var t = d.Plano.Tracados[ti];
                var doBloco = t.Bloco(bx, bz);
                if (doBloco == null) continue;
                foreach (int k in doBloco.Lotes) if (t.Lotes[k].Casa != null || t.Lotes[k].Predio != null || t.Lotes[k].Loja != null) return true;
            }
            return false;
        }

        public static DoBloco Bloco(DadosDoMundo d, int bx, int bz)
        {
            var r = new DoBloco();
            var lista = d.CidadesDoBloco(bx, bz);
            if (lista == null || d.Plano == null) return r;
            foreach (int ti in lista)
            {
                var t = d.Plano.Tracados[ti];
                var doBloco = t.Bloco(bx, bz);
                if (doBloco == null) continue;
                foreach (int k in doBloco.Lotes)
                {
                    var l = t.Lotes[k];
                    if (l.Casa == null && l.Predio == null && l.Loja == null) continue;
                    r.Desenho ??= MalhaDeObra.Emprestar(TamanhoDaMalha.Grande);
                    r.Hera ??= MalhaDaHera.Emprestar();
                    r.Pichacoes ??= MalhaDePichacao.Emprestar();
                    if (Mobilia.Pronta && l.Predio == null) r.Mobilia ??= MalhaDeObra.Emprestar(TamanhoDaMalha.Media);
                    if (l.Casa != null) r.Casas.Add(Montar(d, t, l, r.Desenho, r.Hera, r.Pichacoes, r.Mobilia));
                    else if (l.Loja != null) r.Casas.Add(MontarLoja(d, t, l, r.Desenho, r.Hera, r.Pichacoes, r.Mobilia));
                    else r.Predios.Add(MontarPredio(d, t, l, r.Desenho, r.Hera, r.Pichacoes));
                }
            }
            return r;
        }

        /// <summary>Devolve as malhas do bloco ao estoque (depois de virarem Mesh, ou se o bloco não for mais usado).</summary>
        public static void Devolver(DoBloco r)
        {
            MalhaDeObra.Devolver(r.Desenho);
            r.Desenho = null;
            MalhaDaHera.Devolver(r.Hera);
            MalhaDePichacao.Devolver(r.Pichacoes);
            MalhaDeObra.Devolver(r.Mobilia);
            r.Hera = null;
            r.Pichacoes = null;
            r.Mobilia = null;
            foreach (var c in r.Casas)
            {
                MalhaDeObra.Devolver(c.Colisao);
                c.Colisao = null;
            }
            foreach (var p in r.Predios)
            {
                MalhaDeObra.Devolver(p.Colisao);
                p.Colisao = null;
                if (p.Escuros == null) continue;
                foreach (var e in p.Escuros) MalhaDeObra.Devolver(e);
                p.Escuros = null;
            }
        }

        public static void Devolver(AndarPronto a)
        {
            MalhaDeObra.Devolver(a.Desenho);
            MalhaDeObra.Devolver(a.Colisao);
            a.Desenho = a.Colisao = null;
        }

        /// <summary>
        /// Um prédio do lote (Parte M6, segunda fatia): a casca dele vai para 'desenho' (no mapa); devolve a colisão dela,
        /// as janelas escuras de cada andar e os pisos do térreo. Os andares por dentro são montados à parte
        /// (<see cref="MontarAndar"/>), só perto de alguém.
        /// </summary>
        public static PredioPronto MontarPredio(DadosDoMundo d, TracadoDaCidade t, Lote l, MalhaDeObra desenho, MalhaDaHera hera = null,
                                                MalhaDePichacao pichacoes = null)
        {
            var p = l.Predio;
            float sx = p.espelhar ? -1f : 1f;
            // o chão (nas coordenadas do prédio) num ponto da planta, para os degraus, e o mais baixo em volta, para a fundação
            float Chao(float x, float z)
            {
                var w = t.Mundo(l.NaCasa(sx * x, z));
                return d.Altura(w.x, w.y) - l.ChaoDaCasa;
            }
            float maisBaixo = 0f;
            for (int i = 0; i <= 8; i++)
            {
                float fx = Mathf.Lerp(p.X0, p.X1, i / 8f), fz = Mathf.Lerp(p.Z0, p.Z1, i / 8f);
                maisBaixo = Mathf.Min(maisBaixo, Mathf.Min(Mathf.Min(Chao(fx, p.Z0), Chao(fx, p.Z1)), Mathf.Min(Chao(p.X0, fz), Chao(p.X1, fz))));
            }
            var predio = new Predio(p, M, -1);
            predio.MontarCasca(Chao, maisBaixo);
            var matriz = t.MatrizDaCasa(l);
            Abandonar(predio, AbandonoDaObra.NoMundo, p.semente, 7f, matriz, p.espelhar, hera, pichacoes);
            var pronto = new PredioPronto
            {
                Nome = $"Prédio de {p.andares} andares",
                Posicao = matriz.MultiplyPoint3x4(Vector3.zero),
                Giro = t.GiroDaCasa(l),
                Planta = p,
                Colisao = Espelhada(predio.col, p.espelhar),
                Escuros = new MalhaDeObra[p.andares],
                Triangulos = predio.vis.Triangulos,
            };
            desenho.Juntar(predio.vis, matriz, p.espelhar, altura: true);
            for (int f = 0; f < p.andares; f++) pronto.Escuros[f] = Espelhada(predio.Escuros[f], p.espelhar);
            MalhaDeObra.Devolver(predio.vis);
            foreach (var k in p.terreo) pronto.Zonas.Add(ZonaDo(k, sx, PlantaDePredio.Piso, p.PisoDe(0, k)));
            float baixo = PlantaDePredio.Piso, alto = p.Cobertura - M.laje;
            pronto.CentroDeDentro = new Vector3(0f, (baixo + alto) / 2, 0f);
            pronto.TamanhoDeDentro = new Vector3(p.W - 0.2f, alto - baixo, p.D - 0.2f);
            return pronto;
        }

        /// <summary>
        /// Uma loja do lote (Parte M6, terceira fatia), inteira como uma casa: o desenho vai para 'desenho' (no mapa);
        /// devolve a colisão, os pisos de cada cômodo, a caixa de dentro e o tamanho (para o eco).
        /// </summary>
        public static CasaPronta MontarLoja(DadosDoMundo d, TracadoDaCidade t, Lote l, MalhaDeObra desenho, MalhaDaHera hera = null,
                                            MalhaDePichacao pichacoes = null, MalhaDeObra mobilia = null)
        {
            var p = l.Loja;
            float sx = p.espelhar ? -1f : 1f;
            float Chao(float x, float z)
            {
                var w = t.Mundo(l.NaCasa(sx * x, z));
                return d.Altura(w.x, w.y) - l.ChaoDaCasa;
            }
            float maisBaixo = 0f;
            for (int i = 0; i <= 6; i++)
            {
                float fx = Mathf.Lerp(p.X0, p.X1, i / 6f), fz = Mathf.Lerp(p.Z0, p.Z1, i / 6f);
                maisBaixo = Mathf.Min(maisBaixo, Mathf.Min(Mathf.Min(Chao(fx, p.Z0), Chao(fx, p.Z1)), Mathf.Min(Chao(p.X0, fz), Chao(p.X1, fz))));
            }
            var loja = new Loja(p, M);
            loja.Montar(Chao, maisBaixo);
            var matriz = t.MatrizDaCasa(l);
            Abandonar(loja, AbandonoDaObra.NoMundo, p.semente, 5f, matriz, p.espelhar, hera, pichacoes);
            if (mobilia != null) Mobiliar(loja.moveis, p.semente, loja.col, mobilia, matriz, p.espelhar);
            var pronta = new CasaPronta
            {
                Nome = PlantaDeLoja.Nome(p.tipo) + (p.andares > 1 ? $" de {p.andares} andares" : ""),
                Posicao = matriz.MultiplyPoint3x4(Vector3.zero),
                Giro = t.GiroDaCasa(l),
                Colisao = Espelhada(loja.col, p.espelhar),
                Largura = p.W,
                Fundo = p.D,
                Triangulos = loja.vis.Triangulos,
            };
            desenho.Juntar(loja.vis, matriz, p.espelhar, altura: true);
            MalhaDeObra.Devolver(loja.vis);
            foreach (var pm in loja.moveis) pronta.Moveis.Add(p.espelhar ? pm.Espelhado() : pm);
            foreach (var k in p.comodos) pronta.Zonas.Add(ZonaDo(k, sx, p.Base(k.andar), k.piso ?? "Piso"));
            float baixo = PlantaDeLoja.Piso, alto = p.Cobertura - M.laje;
            pronta.CentroDeDentro = new Vector3(0f, (baixo + alto) / 2, 0f);
            pronta.TamanhoDeDentro = new Vector3(p.W - 0.2f, alto - baixo, p.D - 0.2f);
            return pronta;
        }

        /// <summary>O andar f do prédio por dentro (em outra thread, quando alguém chega perto dele).</summary>
        public static AndarPronto MontarAndar(PlantaDePredio p, int f)
        {
            var predio = new Predio(p, M, f);
            predio.MontarAndar();
            Mobilia.Por(predio.Moveis, predio.vis, predio.col, p.semente * 31 + f);
            var r = new AndarPronto { Andar = f, Desenho = Espelhada(predio.vis, p.espelhar), Colisao = Espelhada(predio.col, p.espelhar) };
            foreach (var pm in predio.Moveis) r.Moveis.Add(p.espelhar ? pm.Espelhado() : pm);
            float sx = p.espelhar ? -1f : 1f;
            foreach (var k in predio.Comodos) r.Zonas.Add(ZonaDo(k, sx, p.Base(f), k.piso));
            return r;
        }

        // o abandono da obra (Parte M9c), antes de ela ir para o desenho: as janelas nela; a hera e as pichações nas malhas
        // do bloco, levadas para o lugar dela
        static void Abandonar(Obra obra, float k, int semente, float alturaDaHera, Matrix4x4 matriz, bool espelhar, MalhaDaHera hera, MalhaDePichacao pichacoes)
        {
            pichacoes?.Levar(matriz, espelhar);
            AbandonoDaObra.Janelas(obra, k, semente, pichacoes);
            if (hera != null)
            {
                hera.Levar(matriz, espelhar);
                AbandonoDaObra.Hera(obra, k, semente, alturaDaHera, hera);
            }
            if (pichacoes != null) AbandonoDaObra.Pichacoes(obra, k, semente, pichacoes);
        }

        // os móveis da obra (antes de espelhar a colisão dela): o desenho numa malha de rascunho, levado para o lugar dela
        // na malha da mobília do bloco (com a altura sobre o chão da obra, como o desenho das casas), e as caixas na colisão
        static void Mobiliar(List<PontoDeMovel> pontos, int semente, MalhaDeObra colisao, MalhaDeObra mobilia, Matrix4x4 matriz, bool espelhar)
        {
            var desenho = MalhaDeObra.Emprestar(TamanhoDaMalha.Media);
            Mobilia.Por(pontos, desenho, colisao, semente);
            mobilia.Juntar(desenho, matriz, espelhar, altura: true);
            MalhaDeObra.Devolver(desenho);
        }

        // a montagem fica com a malha da obra (espelhada nela mesma, se precisar), em vez de copiá-la para outra do estoque:
        // a cópia da colisão de um prédio (uns 25 mil vértices) pegava uma malha pequena e crescia as listas de novo
        static MalhaDeObra Espelhada(MalhaDeObra m, bool espelhar)
        {
            if (espelhar) m.Espelhar();
            return m;
        }

        static Zona ZonaDo(Comodo k, float sx, float y, string piso)
        {
            float a = sx * k.x0, b = sx * k.x1;
            return new Zona { Centro = new Vector3((a + b) / 2, y, (k.z0 + k.z1) / 2), Tamanho = new Vector3(Mathf.Abs(b - a), 1.0f, k.z1 - k.z0), Piso = piso };
        }

        /// <summary>Uma casa do lote: o desenho dela vai para 'desenho' (no mapa); devolve o resto.</summary>
        public static CasaPronta Montar(DadosDoMundo d, TracadoDaCidade t, Lote l, MalhaDeObra desenho, MalhaDaHera hera = null,
                                        MalhaDePichacao pichacoes = null, MalhaDeObra mobilia = null)
        {
            var p = l.Casa;
            var casa = new Casa(p, M);
            casa.Montar();
            var matriz = t.MatrizDaCasa(l);
            Abandonar(casa, l.Tipo == TipoDeLote.Familia ? AbandonoDaObra.NaFamilia : AbandonoDaObra.NoMundo, p.semente, 4f, matriz, p.espelhar, hera, pichacoes);
            if (mobilia != null) Mobiliar(casa.moveis, p.semente, casa.col, mobilia, matriz, p.espelhar);
            var pronta = new CasaPronta
            {
                Nome = (p.molde == "Terrea" ? "Casa térrea" : "Sobrado") + (p.porao ? " com porão" : "") + (l.Tipo == TipoDeLote.Familia ? " (a da família)" : ""),
                Posicao = matriz.MultiplyPoint3x4(Vector3.zero),
                Giro = t.GiroDaCasa(l),
                Colisao = Espelhada(casa.col, p.espelhar),
                Largura = p.W,
                Fundo = p.D,
            };
            desenho.Juntar(casa.vis, matriz, p.espelhar, altura: true);
            if (p.porao) CalcadaEmVolta(p, desenho, matriz, pronta.Colisao);
            pronta.Triangulos = casa.vis.Triangulos;
            MalhaDeObra.Devolver(casa.vis);
            foreach (var pm in casa.moveis) pronta.Moveis.Add(p.espelhar ? pm.Espelhado() : pm);

            // o caminho do pé da escada da frente até a calçada, e a entrada do carro
            Caminho(d, t, l, casa.Entrada, 1.2f, desenho);
            if (casa.EntradaDoCarro.HasValue) Caminho(d, t, l, casa.EntradaDoCarro.Value, 3.0f, desenho);

            // os cômodos, a garagem e a varanda (o piso de cada um, para os passos)
            float Sx(float x) => p.espelhar ? -x : x;
            void Zona(float xa, float xb, float za, float zb, float y, string piso)
            {
                float a = Sx(xa), b = Sx(xb);
                pronta.Zonas.Add(new Zona
                {
                    Centro = new Vector3((a + b) / 2, y, (za + zb) / 2),
                    Tamanho = new Vector3(Mathf.Abs(b - a), 1.0f, zb - za),
                    Piso = piso,
                });
            }
            foreach (var k in p.comodos) Zona(k.x0, k.x1, k.z0, k.z1, casa.Base(k.andar), k.piso ?? "Taco");
            var g = p.garagem;
            if (g != null)
            {
                float xi = g.lado > 0 ? p.X1 : p.X0, xo = xi + g.lado * g.largura;
                Zona(Mathf.Min(xi, xo), Mathf.Max(xi, xo), g.z0, g.z1, 0.05f, "Concreto");
            }
            if (p.varanda)
            {
                float meia = Mathf.Max(1.7f, M.portaFrente / 2 + 1.15f);
                float va = p.varandaLarga ? p.X0 + 0.4f : Mathf.Max(p.X0 + 0.3f, p.xPortaFrente - meia);
                float vb = p.varandaLarga ? p.X1 - 0.4f : Mathf.Min(p.X1 - 0.3f, p.xPortaFrente + meia);
                Zona(va, vb, p.Z0 - M.esp / 2 - p.fundoDaVaranda, p.Z0, casa.piso, "MadeiraVelha");
            }
            // a caixa de dentro: do chão do porão (ou do térreo) ao forro
            float baixo = casa.Base(p.porao ? -1 : 0), alto = casa.Base(p.andares - 1) + M.altura + M.forro;
            pronta.CentroDeDentro = new Vector3(0f, (baixo + alto) / 2, 0f);
            pronta.TamanhoDeDentro = new Vector3(p.W - 0.2f, alto - baixo, p.D - 0.2f);
            return pronta;
        }

        /// <summary>
        /// A calçada de concreto em volta dos buracos do porão (<see cref="Casa.CalcadaDoPorao"/>, o bastante para tapar
        /// qualquer célula de 2 m do terreno que encoste neles, girada como for), sem passar por cima deles: em faixas de z,
        /// entre as bordas dos retângulos. Nas coordenadas da casa (já espelhadas), 3 cm acima do patamar.
        /// </summary>
        static void CalcadaEmVolta(Planta p, MalhaDeObra desenho, Matrix4x4 matriz, MalhaDeObra colisao)
        {
            const float largura = Casa.CalcadaDoPorao, y = 0.03f;
            var buracos = Casa.BuracosDa(p, M);
            var fora = new List<Rect>();
            foreach (var b in buracos) fora.Add(Rect.MinMaxRect(b.xMin - largura, b.yMin - largura, b.xMax + largura, b.yMax + largura));
            var cortes = new List<float>();
            foreach (var b in buracos) { cortes.Add(b.yMin); cortes.Add(b.yMax); }
            foreach (var f in fora) { cortes.Add(f.yMin); cortes.Add(f.yMax); }
            cortes.Sort();
            var calcada = MalhaDeObra.Emprestar();
            for (int i = 0; i + 1 < cortes.Count; i++)
            {
                float za = cortes[i], zb = cortes[i + 1];
                if (zb - za < 0.01f) continue;
                float zm = (za + zb) / 2;
                // os trechos em x desta faixa: dentro de algum retângulo de fora e fora de todos os buracos
                var trechos = new List<Vector2>();
                foreach (var f in fora) if (zm > f.yMin && zm < f.yMax) trechos.Add(new Vector2(f.xMin, f.xMax));
                trechos = Unir(trechos);
                foreach (var b in buracos)
                    if (zm > b.yMin && zm < b.yMax) trechos = Tirar(trechos, b.xMin, b.xMax);
                foreach (var tr in trechos)
                {
                    if (tr.y - tr.x < 0.01f) continue;
                    var c = new Vector3((tr.x + tr.y) / 2, y - 0.05f, zm);
                    var tam = new Vector3(tr.y - tr.x, 0.1f, zb - za);
                    calcada.Caixa(Matrix4x4.Translate(c), tam, "Concreto");
                    colisao.Caixa(Matrix4x4.Translate(c), tam, "Concreto");
                }
            }
            desenho.Juntar(calcada, matriz);
            MalhaDeObra.Devolver(calcada);
        }

        static List<Vector2> Unir(List<Vector2> trechos)
        {
            trechos.Sort((a, b) => a.x.CompareTo(b.x));
            var r = new List<Vector2>();
            foreach (var t in trechos)
            {
                if (r.Count > 0 && t.x <= r[r.Count - 1].y) r[r.Count - 1] = new Vector2(r[r.Count - 1].x, Mathf.Max(r[r.Count - 1].y, t.y));
                else r.Add(t);
            }
            return r;
        }

        static List<Vector2> Tirar(List<Vector2> trechos, float a, float b)
        {
            var r = new List<Vector2>();
            foreach (var t in trechos)
            {
                if (b <= t.x || a >= t.y) { r.Add(t); continue; }
                if (a > t.x) r.Add(new Vector2(t.x, a));
                if (b < t.y) r.Add(new Vector2(b, t.y));
            }
            return r;
        }

        /// <summary>
        /// Uma faixa de concreto de um ponto da casa (nas coordenadas dela, já espelhadas) até a beira da frente do lote
        /// (a calçada), seguindo o chão, 3 cm acima dele.
        /// </summary>
        static void Caminho(DadosDoMundo d, TracadoDaCidade t, Lote l, Vector2 de, float largura, MalhaDeObra desenho)
        {
            var a = l.NaCasa(de.x, de.y);
            bool aoLongoDeU = l.Frente.y != 0f;
            float beira = aoLongoDeU ? (l.Frente.y < 0f ? l.V0 : l.V1) : (l.Frente.x < 0f ? l.U0 : l.U1);
            float comp = Mathf.Abs((aoLongoDeU ? a.y : a.x) - beira);
            if (comp < 0.2f) return;
            var dir = l.Frente;
            var lado = new Vector2(dir.y, -dir.x) * (largura / 2);
            int n = Mathf.Max(1, Mathf.CeilToInt(comp));
            var pontos = new List<Vector3>(4);
            Vector3 P(Vector2 local)
            {
                var w = t.Mundo(local);
                return new Vector3(w.x, d.Altura(w.x, w.y) + 0.03f, w.y);
            }
            for (int k = 0; k < n; k++)
            {
                Vector2 c0 = a + dir * (comp * k / n), c1 = a + dir * (comp * (k + 1) / n);
                pontos.Clear();
                pontos.Add(P(c0 - lado)); pontos.Add(P(c1 - lado)); pontos.Add(P(c1 + lado)); pontos.Add(P(c0 + lado));
                desenho.Face(pontos, Vector3.up, "Calcada");
            }
        }
    }
}
