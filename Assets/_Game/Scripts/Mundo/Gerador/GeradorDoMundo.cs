using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Gera o mundo em dados a partir da semente e do tamanho (Plano_Mapa, Partes M1 a M4). Pode rodar fora da thread
    /// principal: não usa nada da cena nem o UnityEngine.Random. Cada etapa tem o seu sorteio (<see cref="Sorteio.Da"/>).
    ///  1. as medidas do mapa, da grade de alturas e dos blocos;
    ///  2. o lago (num vale, com a água abaixo de toda a margem);
    ///  3. o desenho do mapa (<see cref="PlanejadorDoMapa"/>): as cidades, os lugares, as estradas, o metrô e os nomes; a
    ///     partida é a casa da família, num bairro de casas de uma cidade pequena;
    ///  4. a rede de estradas (<see cref="RedeDeEstradas"/>): sem estrada repetida, os nós, a altura do asfalto e os postos;
    ///  5. o relevo, em várias threads: as colinas, os vales largos, o detalhe, as serras das bordas, a bacia do lago com
    ///     a ilha, o chão acomodado às estradas e o platô da partida;
    ///  6. o que fica na beira das estradas (<see cref="ObjetosDaEstrada"/>): as placas, os carros, as blitz e os postos;
    ///  7. a floresta (<see cref="Floresta"/>): os plantios sorteados; as plantas de cada bloco saem na hora;
    ///  8. o resumo (checksum), que o anfitrião compara com o de cada jogador.
    /// </summary>
    public static class GeradorDoMundo
    {
        /// <summary>
        /// A versão do gerador. Mude quando a mesma semente passar a gerar outro mundo: quem entra com outra versão não
        /// joga, e os mundos salvos com outra versão não abrem. 1 = a fundação, o mundo plano (Parte M1, 26/10/2026).
        /// 2 = o relevo, as serras e o lago (Parte M2, 26/10/2026). 3 = o desenho do mapa e a partida na casa da família
        /// (Parte M3, 26/10/2026). 4 = as estradas no chão, as placas e a beira das estradas (Parte M4, 26/10/2026).
        /// 5 = as cidades em blocos: as ruas, os lotes, as construções em caixas e o chão aplainado (Parte M5, 26/10/2026).
        /// 6 = as casas de verdade: os lotes do tamanho da casa, o patamar de cada uma, os buracos dos porões e a partida na
        /// casa da família (Parte M6, primeira fatia, 26/10/2026). 7 = os prédios de verdade: a planta de cada um, do
        /// tamanho do lote, e o chão dele (Parte M6, segunda fatia, 26/10/2026). 8 = as lojas de verdade (o tipo e a planta
        /// de cada uma) e o hospital (Parte M6, terceira fatia, 27/10/2026). 9 = a floresta: as manchas, os plantios e as
        /// plantas de cada bloco, e as folhas no chão embaixo dela (Parte M9a, 27/10/2026).
        /// </summary>
        public const int Versao = 9;

        /// <summary>As etapas para a tela de carregamento (de 0 a 1, e o texto). Pode ser chamado de outras threads.</summary>
        public delegate void Progresso(float fracao, string etapa);

        public static DadosDoMundo Gerar(int semente, TamanhoDoMundo tamanho, Progresso progresso = null)
        {
            progresso?.Invoke(0.02f, "Medindo o mapa");
            var d = new DadosDoMundo(semente, tamanho);
            progresso?.Invoke(0.05f, "Escolhendo o lugar do lago");
            EscolherOLago(d);
            progresso?.Invoke(0.07f, "Desenhando o mapa: cidades, estradas e nomes");
            d.Plano = PlanejadorDoMapa.Planejar(d);
            progresso?.Invoke(0.3f, "Escolhendo onde a família começa");
            EscolherAPartida(d);
            progresso?.Invoke(0.31f, "Assentando as estradas");
            d.Rede = RedeDeEstradas.Montar(d);
            AbrirOsPoroes(d);
            progresso?.Invoke(0.32f, "Levantando as colinas e as serras");
            int feitas = 0, linhas = d.Vertices;
            Parallel.For(0, linhas, j =>
            {
                for (int i = 0; i < d.Vertices; i++)
                {
                    var p = d.PosicaoDoVertice(i, j);
                    d.Alturas[j * d.Vertices + i] = AlturaFinal(d, p.x, p.y);
                }
                int n = Interlocked.Increment(ref feitas);
                if (progresso != null && (n & 127) == 0) progresso(0.32f + 0.6f * n / linhas, "Levantando as colinas e as serras");
            });
            AssentarOsPredios(d);
            progresso?.Invoke(0.93f, "Pondo as placas e os carros na beira das estradas");
            d.Objetos = ObjetosDaEstrada.Planejar(d);
            progresso?.Invoke(0.94f, "Plantando a floresta");
            d.Floresta = new Floresta(d);
            progresso?.Invoke(0.95f, "Conferindo");
            d.Resumo = Resumir(d);
            progresso?.Invoke(1f, "Pronto");
            return d;
        }

        /// <summary>Uma semente nova, de 1 a 999.999 (fácil de digitar).</summary>
        public static int NovaSemente() => new System.Random(Environment.TickCount ^ Guid.NewGuid().GetHashCode()).Next(1, 1000000);

        // ------------------------------------------------------------------ o relevo

        /// <summary>A base do relevo (m): com ela, o fundo do lago mais fundo fica bem acima dos -20 m em que o jogador volta à partida.</summary>
        public const float Base = 12f;

        /// <summary>
        /// O relevo sem o lago e sem o platô: colinas suaves (ondas de ~700 m, até ~30 m), vales largos onde um ruído passa
        /// perto de zero, o detalhe (~170 m, ±4 m), a aspereza (±0,5 m) e as serras das bordas (cristas de um ruído
        /// "dobrado", até <see cref="DadosDoMundo.AlturaDaSerra"/>), tudo em cima da <see cref="Base"/>.
        /// </summary>
        public static float AlturaNatural(DadosDoMundo d, float x, float z)
        {
            float c = d.Colinas.Fbm(x * 0.0014f, z * 0.0014f, 3);
            float colinas = Mathf.Pow(Mathf.Clamp01(c * 0.85f + 0.5f), 1.4f) * 30f;
            float v = Mathf.Abs(d.Vales.Fbm(x * 0.0008f, z * 0.0008f, 2));
            float vale = (1f - Geo.Suave(0.03f, 0.2f, v)) * 9f;
            float detalhe = d.Detalhe.Fbm(x * 0.006f, z * 0.006f, 2) * 4f;
            float aspero = d.Detalhe.Amostra(x * 0.035f + 17.3f, z * 0.035f - 9.1f) * 0.5f;
            float h = Base + colinas - vale + detalhe + aspero;
            float borda = d.Borda(x, z);
            if (borda > 0f)
            {
                float crista = 1f - Mathf.Abs(d.Serra.Amostra(x * 0.0042f, z * 0.0042f));
                float miudo = d.Serra.Amostra01(x * 0.013f + 7.1f, z * 0.013f - 3.3f);
                h += borda * d.AlturaDaSerra * (0.45f + 0.4f * crista + 0.15f * miudo);
            }
            return h;
        }

        /// <summary>Até onde (q) o lago mexe no relevo.</summary>
        public const float MaiorQDoLago = 3f;

        /// <summary>O relevo com a bacia do lago (a rampa em volta e a ilha), sem o platô da partida.</summary>
        public static float AlturaComLago(DadosDoMundo d, float x, float z)
        {
            float h = AlturaNatural(d, x, z);
            var lago = d.Lago;
            if (lago != null)
            {
                float q = lago.Q(x, z);
                if (q < MaiorQDoLago)
                {
                    // a bacia: funda no meio, a margem em q = 1. Fora dela, uma rampa de 8% sobe da água até o relevo (onde
                    // o morro em volta é alto, o lago fica num vale largo, e não numa cratera); a rampa some até q = 3
                    float bacia = lago.Nivel - lago.Fundura * (1f - Mathf.Min(1f, q * q));
                    float rampa = lago.Nivel + 0.08f * Mathf.Max(0f, q - 1f) * lago.Raio;
                    float fora = Mathf.Lerp(Mathf.Min(h, rampa), h, Geo.Suave(2.6f, MaiorQDoLago, q));
                    h = Mathf.Lerp(bacia, fora, Geo.Suave(0.85f, 1.2f, q));
                    // a ilha do meio, para a casa do lago
                    float dx = x - lago.Centro.x, dz = z - lago.Centro.y;
                    float qi = Mathf.Sqrt(dx * dx + dz * dz) / lago.RaioDaIlha;
                    if (qi < 1.4f)
                    {
                        float topo = lago.Nivel + 1.4f + d.Detalhe.Amostra(x * 0.08f, z * 0.08f) * 0.25f;
                        h = Mathf.Max(h, Mathf.Lerp(topo, h, Geo.Suave(0.55f, 1.35f, qi)));
                    }
                }
            }
            return h;
        }

        /// <summary>
        /// O relevo com o lago e o chão aplainado das cidades (Parte M5): dentro do contorno, o chão da cidade
        /// (<see cref="TracadoDaCidade.Chao"/>); fora dele, voltando ao relevo. É o chão das ruas e dos lotes, e a base da
        /// altura das estradas.
        /// </summary>
        public static float AlturaDoChao(DadosDoMundo d, float x, float z)
        {
            float h = AlturaComLago(d, x, z);
            var cidade = d.CidadeNoChao(x, z, out float peso);
            // (perto do lago, a bacia e a margem mandam: a volta do chão da cidade, fora do contorno, não levanta o fundo da
            // água. No contorno o peso continua 1 e vai cedendo ao lago sem degrau)
            if (cidade != null && peso > 0f && peso < 1f && d.Lago != null)
            {
                float lago = Geo.Suave(1.3f, 2.2f, d.Lago.Q(x, z));
                float p4 = peso * peso * peso * peso;
                peso *= lago + (1f - lago) * p4;
            }
            if (cidade != null && peso > 0f) h = Mathf.Lerp(h, cidade.Chao(x, z), peso);
            return h;
        }

        /// <summary>
        /// O relevo final: com o lago, o chão das cidades, o chão acomodado às estradas, o patamar das casas (Parte M6) e o
        /// platô da partida (fora das cidades).
        /// </summary>
        public static float AlturaFinal(DadosDoMundo d, float x, float z)
        {
            float h = AlturaDoChao(d, x, z);
            if (d.Rede != null) h = d.Rede.Acomodar(x, z, h);
            var cidade = d.CidadeNoChao(x, z, out _);
            if (cidade != null) h = cidade.NoPatamar(new Vector2(x, z), h);
            if (d.ComPlato)
            {
                float px = x - d.Partida.x, pz = z - d.Partida.z;
                float dp = Mathf.Sqrt(px * px + pz * pz);
                if (dp < DadosDoMundo.FimDoPlato) h = Mathf.Lerp(d.Partida.y, h, Geo.Suave(DadosDoMundo.RaioDoPlato, DadosDoMundo.FimDoPlato, dp));
            }
            return h;
        }

        /// <summary>
        /// Os buracos dos porões das casas (Parte M6): as células do terreno que encostam no porão ou no fosso da entrada
        /// dele saem (a casa tem as paredes do porão até embaixo, e a calçada de concreto em volta tapa o resto delas, no
        /// patamar).
        /// </summary>
        static void AbrirOsPoroes(DadosDoMundo d)
        {
            if (d.Plano == null) return;
            var m = Medidas.DoJogo;
            var cantos = new Vector2[4];
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                {
                    if (l.Casa == null || !l.Casa.porao) continue;
                    foreach (var b in Casa.BuracosDa(l.Casa, m))
                    {
                        cantos[0] = t.Mundo(l.NaCasa(b.xMin, b.yMin));
                        cantos[1] = t.Mundo(l.NaCasa(b.xMax, b.yMin));
                        cantos[2] = t.Mundo(l.NaCasa(b.xMax, b.yMax));
                        cantos[3] = t.Mundo(l.NaCasa(b.xMin, b.yMax));
                        d.MarcarBuracoEm(cantos);
                    }
                }
        }

        /// <summary>
        /// O chão dos prédios (Parte M6, segunda fatia) e das lojas (terceira fatia): o mais alto embaixo de cada um, no
        /// terreno já pronto (a cada metro), para o piso do térreo nunca ficar abaixo do chão. Onde o chão é mais baixo, a
        /// fundação aparece e as portas ganham degraus (o chão embaixo de um prédio varia no máximo ~1,5 m).
        /// </summary>
        static void AssentarOsPredios(DadosDoMundo d)
        {
            if (d.Plano == null) return;
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                {
                    float W, D;
                    if (l.Predio != null) { W = l.Predio.W; D = l.Predio.D; }
                    else if (l.Loja != null) { W = l.Loja.W; D = l.Loja.D; }
                    else continue;
                    int nx = Mathf.CeilToInt(W), nz = Mathf.CeilToInt(D);
                    float maior = float.MinValue;
                    for (int i = 0; i <= nx; i++)
                        for (int j = 0; j <= nz; j++)
                        {
                            var w = t.Mundo(l.NaCasa(Mathf.Lerp(-W / 2, W / 2, i / (float)nx), Mathf.Lerp(-D / 2, D / 2, j / (float)nz)));
                            maior = Mathf.Max(maior, d.Altura(w.x, w.y));
                        }
                    l.ChaoDaCasa = maior;
                }
        }

        /// <summary>
        /// O lago: o raio pelo tamanho do mapa, num dos 12 lugares sorteados no meio do mapa, o mais baixo (num vale). A água
        /// fica 0,6 m abaixo do ponto mais baixo da faixa da margem (q de 1 a 1,7): a água nunca passa da margem.
        /// </summary>
        static void EscolherOLago(DadosDoMundo d)
        {
            var s = Sorteio.Da(d.Semente, "lago");
            float raio = d.Tamanho switch { TamanhoDoMundo.Pequeno => 105f, TamanhoDoMundo.Medio => 135f, _ => 165f } * s.Entre(0.85f, 1.15f);
            float area = d.Metade * 0.55f;
            Vector2 melhor = Vector2.zero;
            float maisBaixo = float.MaxValue;
            for (int k = 0; k < 12; k++)
            {
                var c = new Vector2(s.Entre(-area, area), s.Entre(-area, area));
                float h = AlturaNatural(d, c.x, c.y);
                if (h < maisBaixo)
                {
                    maisBaixo = h;
                    melhor = c;
                }
            }
            var lago = new Lago
            {
                Centro = new Vector2(Mathf.Round(melhor.x), Mathf.Round(melhor.y)),
                Raio = raio,
                Fundura = s.Entre(4.5f, 6.5f),
                RaioDaIlha = s.Entre(18f, 26f),
                Fase = s.Entre(0f, 100f),
                Contorno = new Ruido(d.Semente, "margem-do-lago"),
            };
            float minimo = float.MaxValue;
            for (int a = 0; a < 64; a++)
            {
                float ang = a * Mathf.PI * 2f / 64f;
                float cx = Mathf.Cos(ang), cz = Mathf.Sin(ang);
                float r = lago.RaioNaDirecao(cx, cz);
                foreach (float q in new[] { 1f, 1.15f, 1.3f, 1.5f, 1.7f })
                    minimo = Mathf.Min(minimo, AlturaNatural(d, lago.Centro.x + cx * r * q, lago.Centro.y + cz * r * q));
            }
            lago.Nivel = minimo - 0.6f;
            d.Lago = lago;
        }

        /// <summary>
        /// A partida: a casa da família do desenho do mapa (num bairro de casas de uma cidade pequena). Desde a Parte M6, os
        /// 4 jogadores nascem na sala dela, olhando para a porta da frente, e o carro fica na garagem (ou na vaga) dela. Sem
        /// a casa (um mapa sem cidades), o ponto mais plano de 8 sorteados no meio do mapa, longe do lago, com o chão
        /// nivelado em volta (o platô), e os 4 ficam no terreno de concreto, em duas filas, olhando para a frente dele.
        /// </summary>
        static void EscolherAPartida(DadosDoMundo d)
        {
            var casa = d.Plano?.CasaDaFamilia;
            var lote = d.Plano?.LoteDaFamilia;
            var tracado = d.Plano?.CidadeDaFamilia?.Tracado;
            Vector2 escolhido;
            if (lote != null && tracado != null && lote.Casa != null)
            {
                var planta = lote.Casa;
                var m = Medidas.DoJogo;
                var M = tracado.MatrizDaCasa(lote);
                var sala = planta.comodos.Find(k => k.tipo == "sala" && k.andar == 0) ?? planta.comodos[0];
                float sx = planta.espelhar ? -sala.Cx : sala.Cx, piso = Casa.PisoDe(planta);
                float giro = Mathf.Repeat(tracado.GiroDaCasa(lote) + 180f, 360f);
                var lugares = new[] { new Vector2(-1.2f, -0.9f), new Vector2(1.2f, -0.9f), new Vector2(-1.2f, 0.9f), new Vector2(1.2f, 0.9f) };
                for (int i = 0; i < DadosDoMundo.Jogadores; i++)
                {
                    d.Nascimentos[i] = M.MultiplyPoint3x4(new Vector3(sx + lugares[i].x, piso + 0.05f, sala.Cz + lugares[i].y));
                    d.GirosDosNascimentos[i] = giro;
                }
                d.Partida = M.MultiplyPoint3x4(Vector3.zero);
                d.GiroDaPartida = giro;
                d.ComPlato = false;
                d.FundoDaPartida = d.Partida.y;
                if (Casa.LugarDoCarro(planta, m, out var carro, out float giroDoCarro))
                {
                    d.LugarDoCarro = M.MultiplyPoint3x4(new Vector3(carro.x, 0f, carro.y));
                    // de ré na vaga: a frente para a rua, para sair dirigindo
                    d.GiroDoCarro = Mathf.Repeat(tracado.GiroDaCasa(lote) + giroDoCarro + 180f, 360f);
                }
                if (casa != null) casa.Centro = new Vector2(d.Partida.x, d.Partida.z);
                return;
            }
            if (lote != null && tracado != null)
            {
                // no lote da casa da família (Parte M5): a laje 3 m atrás da calçada, olhando para a rua; sem platô (o chão
                // da cidade já é quase plano, e a laje desce até ele)
                float l = DadosDoMundo.LadoDaPartida;
                var meio = lote.Meio;
                var frente = lote.Frente;
                float fundoDoLote = frente.y != 0f ? lote.V1 - lote.V0 : lote.U1 - lote.U0;
                var local = meio + frente * (fundoDoLote * 0.5f - 3f - l * 0.5f);
                escolhido = tracado.Mundo(local);
                var dir = (tracado.Mundo(local + frente) - escolhido).normalized;
                d.GiroDaPartida = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                var giroDaLaje = Quaternion.Euler(0f, d.GiroDaPartida, 0f);
                float alto = float.MinValue, baixo = float.MaxValue;
                for (int k = 0; k < 9; k++)
                {
                    var c = giroDaLaje * new Vector3((k % 3 - 1) * l * 0.5f, 0f, (k / 3 - 1) * l * 0.5f);
                    float h = AlturaDoChao(d, escolhido.x + c.x, escolhido.y + c.z);
                    alto = Mathf.Max(alto, h);
                    baixo = Mathf.Min(baixo, h);
                }
                d.ComPlato = false;
                d.Partida = new Vector3(escolhido.x, alto, escolhido.y);
                d.FundoDaPartida = baixo;
                if (casa != null) casa.Centro = escolhido;
                PorOsNascimentos(d);
                return;
            }
            if (casa != null)
            {
                escolhido = casa.Centro;
                d.GiroDaPartida = casa.Giro;
            }
            else
            {
                var s = Sorteio.Da(d.Semente, "partida");
                float r = d.Metade * 0.35f;
                escolhido = Vector2.zero;
                float menor = float.MaxValue;
                int validos = 0;
                for (int k = 0; k < 60 && validos < 8; k++)
                {
                    var c = new Vector2(Mathf.Round(s.Entre(-r, r) * 2f) / 2f, Mathf.Round(s.Entre(-r, r) * 2f) / 2f);
                    if (d.Lago != null && (c - d.Lago.Centro).magnitude < d.Lago.Raio * 1.8f + 120f) continue;
                    validos++;
                    float inclinacao = 0f;
                    float h0 = AlturaComLago(d, c.x, c.y);
                    for (int a = 0; a < 6; a++)
                    {
                        float ang = a * Mathf.PI / 3f;
                        inclinacao = Mathf.Max(inclinacao, Mathf.Abs(AlturaComLago(d, c.x + Mathf.Cos(ang) * 30f, c.y + Mathf.Sin(ang) * 30f) - h0));
                    }
                    if (inclinacao < menor)
                    {
                        menor = inclinacao;
                        escolhido = c;
                    }
                }
                d.GiroDaPartida = Mathf.Round(s.Entre(0f, 360f));
            }
            d.Partida = new Vector3(escolhido.x, AlturaDoChao(d, escolhido.x, escolhido.y), escolhido.y);
            d.FundoDaPartida = d.Partida.y - 0.18f;
            PorOsNascimentos(d);
        }

        // os 4 lugares na laje, em duas filas, olhando para a frente dela
        static void PorOsNascimentos(DadosDoMundo d)
        {
            var giro = Quaternion.Euler(0f, d.GiroDaPartida, 0f);
            var lugares = new[] { new Vector3(-1.4f, 0f, 1f), new Vector3(1.4f, 0f, 1f), new Vector3(-1.4f, 0f, -1.2f), new Vector3(1.4f, 0f, -1.2f) };
            for (int i = 0; i < DadosDoMundo.Jogadores; i++)
            {
                d.Nascimentos[i] = d.Partida + giro * lugares[i] + Vector3.up * (DadosDoMundo.AlturaDaPartida + 0.05f);
                d.GirosDosNascimentos[i] = d.GiroDaPartida;
            }
        }

        /// <summary>
        /// O resumo (checksum, FNV-1a) da versão, da semente, do tamanho, do lago, da partida, das alturas a cada 32 m, dos
        /// pesos do chão a cada 256 m, dos buracos, do desenho do mapa, da rede de estradas e da beira das estradas. Os números com vírgula entram arredondados ao milímetro.
        /// </summary>
        static ulong Resumir(DadosDoMundo d)
        {
            ulong h = 1469598103934665603UL;
            void Somar(long v)
            {
                h ^= (ulong)v;
                h *= 1099511628211UL;
            }
            void SomarNumero(float f) => Somar((long)Math.Round(f * 1000.0));
            void SomarPonto(Vector3 p)
            {
                SomarNumero(p.x);
                SomarNumero(p.y);
                SomarNumero(p.z);
            }
            Somar(d.Versao);
            Somar(d.Semente);
            Somar((long)d.Tamanho);
            Somar(d.BlocosPorLado);
            if (d.Lago != null)
            {
                SomarNumero(d.Lago.Centro.x);
                SomarNumero(d.Lago.Centro.y);
                SomarNumero(d.Lago.Raio);
                SomarNumero(d.Lago.Nivel);
                SomarNumero(d.Lago.Fundura);
                SomarNumero(d.Lago.RaioDaIlha);
            }
            SomarPonto(d.Partida);
            SomarNumero(d.GiroDaPartida);
            for (int i = 0; i < DadosDoMundo.Jogadores; i++) SomarPonto(d.Nascimentos[i]);
            for (int j = 0; j < d.Vertices; j += 16)
            for (int i = 0; i < d.Vertices; i += 16)
                SomarNumero(d.AlturaDoVertice(i, j));
            for (int j = 0; j < d.Vertices; j += 128)
            for (int i = 0; i < d.Vertices; i += 128)
            {
                var c = d.PesosDoChao(i, j);
                Somar(c.r | c.g << 8 | c.b << 16 | c.a << 24);
            }
            Somar(d.QuantosBuracos);
            // a floresta: os plantios e as plantas de uns blocos espalhados
            if (d.Floresta != null)
            {
                Somar(d.Floresta.Plantios);
                var plantas = new List<PlantaDaFloresta>();
                for (int k = 1; k <= 9; k++)
                {
                    d.Floresta.DoBloco(d.BlocosPorLado * k / 10, d.BlocosPorLado * (10 - k) / 10, plantas);
                    Somar(plantas.Count);
                    foreach (var p in plantas)
                    {
                        Somar((long)p.Especie);
                        SomarPonto(p.Posicao);
                    }
                }
            }
            // o desenho do mapa: cada lugar (o tipo, o meio, o raio e o nome) e cada estrada (os pontos e o comprimento)
            if (d.Plano != null)
            {
                foreach (var l in d.Plano.Lugares)
                {
                    Somar((long)l.Tipo);
                    SomarNumero(l.Centro.x);
                    SomarNumero(l.Centro.y);
                    SomarNumero(l.Raio);
                    if (l.Nome != null) Somar((long)Sorteio.Fnv(l.Nome));
                }
                foreach (var e in d.Plano.Estradas)
                {
                    Somar((long)e.Tipo);
                    Somar(e.Pontos.Count);
                    SomarNumero(e.Comprimento);
                    for (int i = 0; i < e.Alturas.Count; i += 8) SomarNumero(e.Alturas[i]);
                }
                foreach (var linha in d.Plano.Metro) Somar(linha.Estacoes.Count);
                // as cidades: as linhas das ruas e cada lote (o tipo, o terreno e a construção)
                foreach (var t in d.Plano.Tracados)
                {
                    Somar(t.Us.Length);
                    Somar(t.Vs.Length);
                    Somar(t.Pecas.Count);
                    foreach (var l in t.Lotes)
                    {
                        Somar((long)l.Tipo);
                        SomarNumero(l.U0);
                        SomarNumero(l.V0);
                        SomarNumero(l.BU1);
                        SomarNumero(l.BV1);
                        Somar(l.Andares);
                        if (l.Predio != null)
                        {
                            // o prédio de verdade (Parte M6, segunda fatia): a planta, onde ele fica e o chão dele
                            var pr = l.Predio;
                            Somar(pr.semente);
                            SomarNumero(pr.W);
                            SomarNumero(pr.D);
                            Somar(pr.andares * 16 + (pr.lojasNoTerreo ? 1 : 0) + (pr.espelhar ? 2 : 0) + (pr.andarDoPocoAberto >= 0 ? 4 : 0) + (pr.hospital ? 8 : 0));
                            SomarNumero(pr.ex0);
                            SomarNumero(l.MeioDaCasa.x);
                            SomarNumero(l.MeioDaCasa.y);
                            SomarNumero(l.ChaoDaCasa);
                        }
                        if (l.Loja != null)
                        {
                            // a loja de verdade (Parte M6, terceira fatia): o tipo, a planta, onde ela fica e o chão dela
                            var lj = l.Loja;
                            Somar(lj.semente);
                            Somar((long)lj.tipo * 16 + lj.andares * 2 + (lj.espelhar ? 1 : 0));
                            SomarNumero(lj.W);
                            SomarNumero(lj.D);
                            SomarNumero(lj.zs);
                            SomarNumero(l.MeioDaCasa.x);
                            SomarNumero(l.MeioDaCasa.y);
                            SomarNumero(l.ChaoDaCasa);
                        }
                        if (l.Casa == null) continue;
                        // a casa de verdade (Parte M6): a planta, onde ela fica e o chão dela
                        Somar(l.Casa.semente);
                        SomarNumero(l.Casa.W);
                        SomarNumero(l.Casa.D);
                        Somar((l.Casa.porao ? 1 : 0) + (l.Casa.espelhar ? 2 : 0) + (l.Casa.garagem != null && l.Casa.garagem.coberta ? 4 : 0));
                        SomarNumero(l.MeioDaCasa.x);
                        SomarNumero(l.MeioDaCasa.y);
                        SomarNumero(l.ChaoDaCasa);
                    }
                }
            }
            // a rede (os nós, os trechos e os pátios dos postos) e a beira das estradas
            if (d.Rede != null)
            {
                Somar(d.Rede.Nos.Count);
                Somar(d.Rede.Trechos.Count);
                foreach (var pl in d.Rede.Plataformas) SomarNumero(pl.Altura);
            }
            if (d.Objetos != null)
                foreach (var o in d.Objetos.Todos)
                {
                    Somar((long)o.Tipo);
                    SomarNumero(o.Posicao.x);
                    SomarNumero(o.Posicao.y);
                    SomarNumero(o.Giro);
                }
            return h;
        }
    }
}
