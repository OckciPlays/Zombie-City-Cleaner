using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDeBunker;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>Os tipos de lugar onde o morador faz alguma coisa (a parte 6 do Plano_NPCs_Bunker.md da pasta de arte).</summary>
    /// <remarks>Os do lazer (a parte 8, a segunda fatia): o rádio (a cadeira na frente da estação de rádio, no B0), dobrar a
    /// roupa (a mesa da lavanderia e da rouparia, no B3) e em pé em volta da mesa de sinuca (no B1).</remarks>
    public enum TipoDoLugar : byte { Cama, Sofa, Cadeira, Banco, Pia, Bancada, Fogao, Espelho, EmPe, Estante, Radio, Dobrar, Sinuca }

    /// <summary>
    /// Um lugar do bunker onde o morador faz alguma coisa: onde a raiz dele fica (nas coordenadas da obra, no chão do andar) e
    /// para onde ela olha (o giro da Unity), com o móvel em volta como as animações da parte 5 pedem (a frente do assento
    /// 12 cm atrás da raiz, a beira da cama 12 cm atrás, a bancada 25 cm na frente...).
    /// </summary>
    public struct LugarDoMorador
    {
        public TipoDoLugar tipo;
        public int andar;
        public Vector3 raiz;
        public float giro;
        /// <summary>O móvel (o índice em <see cref="MoveisDoBunker.Todos"/>; -1 nos de pé).</summary>
        public int movel;
        /// <summary>A cama do outro lado: o corpo vai espelhado (a animação deita com a cabeça para a esquerda da raiz).</summary>
        public bool espelho;
        /// <summary>A cama alta (a de casal e a de solteiro: o colchão 12 cm acima do do beliche, o das animações).</summary>
        public bool alta;
        /// <summary>Na estante (e na bancada que guarda): onde a raiz fica ao longo dela (o x do móvel, na escala do jogo).</summary>
        public float lado;
        /// <summary>O tipo do cômodo ("quarto", "dormitorio", "sala"...) e o nome ("Quarto 5").</summary>
        public string comodo, nome;

        /// <summary>A ação de sempre no lugar, com o espelho (o byte da rede).</summary>
        public byte Acao => (byte)((byte)AcoesDoCorpo.DoLugar(tipo) | (espelho ? AcoesDoCorpo.Espelho : 0) | (alta ? AcoesDoCorpo.Alta : 0));
    }

    /// <summary>
    /// Os lugares do bunker para os moradores, tirados dos móveis (<see cref="MoveisDoBunker"/>, com as medidas da
    /// <see cref="MedidasDaMobilia"/> em ×1,30) pelas medidas das animações da parte 5 (o <c>bloco_morador.py</c> da pasta de
    /// arte): os lugares dos sofás e das poltronas, as cadeiras, os bancos do refeitório (atrás deles, olhando para a mesa),
    /// as camas (dos dois lados; o de lá com o corpo espelhado), as pias e as bancadas, os espelhos dos banheiros e uns
    /// lugares em pé no meio dos cômodos. Um lugar só fica se a raiz (um círculo de <see cref="Raio"/>) cabe no cômodo e
    /// não entra em outro móvel (a cadeira empurrada para baixo da mesa fica de fora). Os do B4 ficam de fora (os moradores
    /// não descem lá).
    /// </summary>
    public static class LugaresDoBunker
    {
        /// <summary>O raio da raiz em pé (o chão que ela precisa livre).</summary>
        public const float Raio = 0.22f;
        const float AFrente = 0.12f, AtrasDoBanco = 0.24f, DaCama = 0.12f, DaBancada = 0.25f, DaPia = 0.21f, DoFogao = 0.21f,
                    DoEspelho = 0.32f, DaParede = 0.12f, DaEstante = 0.30f;

        /// <summary>Os móveis que guardam itens (as estantes do <c>EstantesDoBunker</c>), onde os moradores pegam.</summary>
        public static bool GuardaItens(string modelo) =>
            modelo == "EstanteAco90" || modelo == "EstanteAco120" || modelo == "PrateleiraMadeira90" || modelo == "PrateleiraMadeira150" || modelo == "BancadaInox";
        /// <summary>Os que guardam desde o lazer (a parte 8): o suporte dos tacos e a mesinha do fumo (a mesa de canto do bunker).</summary>
        public static bool GuardaDoLazer(string modelo) => modelo == "SuporteDeTacos" || modelo == "MesaDeCanto";
        /// <summary>Ao longo da cama, a raiz a partir da cabeceira: deitado, o quadril fica 5 cm para o lado dela, e a cabeça uns 85 cm além.</summary>
        const float DaCabeceira = 1.0f;

        static readonly List<string> _rejeitados = new List<string>();
        public static readonly LugarDoMorador[] Todos = Calcular();

        /// <summary>Os lugares que ficaram de fora (o móvel e por quê), para o teste.</summary>
        public static IReadOnlyList<string> Rejeitados => _rejeitados;

        /// <summary>Os índices dos lugares de um tipo.</summary>
        public static IEnumerable<int> DoTipo(TipoDoLugar t)
        {
            for (int i = 0; i < Todos.Length; i++) if (Todos[i].tipo == t) yield return i;
        }

        /// <summary>O ponto na frente da porta da escada (do lado do hall), no chão do andar: onde quem vai a outro andar some.</summary>
        public static Vector3 NaEscada(int f) => new Vector3(PlantaDeBunker.Escada.xMax + 0.7f, Base(f), PortaDaEscadaZ);

        /// <summary>O meio do patamar do andar, dentro da caixa da escada, na frente da porta (o fim do caminho do corpo até
        /// um andar que não está montado: a escada é da casca).</summary>
        public static Vector3 NoPatamar(int f) => new Vector3(PlantaDeBunker.EscadaXf - PlantaDeBunker.Patamar * 0.5f, Base(f), PortaDaEscadaZ);

        // ------------------------------------------------------------ a conta

        struct Caixa
        {
            public Vector2 centro, meio;   // o meio-tamanho, no giro do móvel
            public float giro;
            public int movel;
        }

        static LugarDoMorador[] Calcular()
        {
            var lista = new List<LugarDoMorador>();
            var todos = MoveisDoBunker.Todos;
            var plantas = new PlantaDeBunker.Andar[Andares];
            // as caixas dos móveis: as dos antigos (para os lugares de antes ficarem como eram) e as de todos (os do lazer)
            var caixas = new List<Caixa>[Andares];
            var todasAsCaixas = new List<Caixa>[Andares];
            for (int f = 0; f < Andares; f++)
            {
                plantas[f] = DoAndar(f);
                caixas[f] = new List<Caixa>();
                todasAsCaixas[f] = new List<Caixa>();
            }
            for (int i = 0; i < todos.Length; i++)
            {
                var m = todos[i];
                if (!m.caixa || !MedidasDaMobilia.Tem(m.modelo)) continue;
                Vector3 t = MedidasDaMobilia.Real(m.modelo) * m.escala;
                Vector2 meio = MedidasDaMobilia.Meio(m.modelo) * m.escala;
                var c = Quaternion.Euler(0f, m.giro, 0f) * new Vector3(meio.x, 0f, meio.y);
                var caixa = new Caixa { centro = new Vector2(m.lugar.x + c.x, m.lugar.z + c.z), meio = new Vector2(t.x, t.z) / 2f, giro = m.giro, movel = i };
                if (i < MoveisDoBunker.Antigos) caixas[m.andar].Add(caixa);
                todasAsCaixas[m.andar].Add(caixa);
            }

            for (int i = 0; i < todos.Length; i++)
            {
                var m = todos[i];
                if (m.andar >= Andares - 1 || !MedidasDaMobilia.Tem(m.modelo)) continue;
                float e = m.escala;
                Vector3 t = MedidasDaMobilia.Real(m.modelo) * e;
                Vector2 meio = MedidasDaMobilia.Meio(m.modelo) * e;
                float frente = meio.y + t.z / 2f, costas = meio.y - t.z / 2f;

                void Por(TipoDoLugar tipo, float lx, float lz, float giroLocal, bool espelho = false) =>
                    Tentar(lista, plantas, caixas, tipo, i, m, lx, lz, giroLocal, espelho);

                // na frente das estantes e das bancadas que guardam: de 2 a 4 lugares ao longo delas (o braço alcança uns 30 cm
                // para cada lado), a frente 30 cm na frente da raiz (as animações da estante)
                if (GuardaItens(m.modelo))
                {
                    int n = Mathf.Clamp(Mathf.RoundToInt(t.x / 0.5f), 2, 4);
                    for (int k = 0; k < n; k++)
                    {
                        float lx = meio.x + (k + 0.5f - n / 2f) * (t.x / n);
                        int antes = lista.Count;
                        Por(TipoDoLugar.Estante, lx, frente + DaEstante, 180f);
                        if (lista.Count > antes)
                        {
                            var l = lista[antes];
                            l.lado = lx;
                            lista[antes] = l;
                        }
                    }
                    if (m.modelo != "BancadaInox") continue;
                }

                switch (m.modelo)
                {
                    case "Sofa1": case "Sofa2": case "Sofa3": case "SofaL":
                    {
                        // os lugares do assento (o sofa() do mobilia_nova.py: os braços de 20 cm; no de canto, a chaise
                        // fica do lado -x e o lugar dela não conta), a frente do assento a 45 cm do meio
                        bool canto = m.modelo == "SofaL";
                        int n = canto ? 2 : m.modelo[4] - '0';
                        float W = canto ? 2.30f : n == 1 ? 0.95f : n == 2 ? 1.55f : 2.10f;
                        float util = canto ? W - 0.85f - 0.2f : W - 0.4f, wi = util / n;
                        for (int k = 0; k < n; k++)
                        {
                            float xb = -W / 2f + 0.2f + wi * (k + 0.5f);
                            Por(TipoDoLugar.Sofa, -xb * e, 0.45f * e + AFrente, 0f);
                        }
                        break;
                    }
                    case "Cadeira": case "CadeiraEstofada":
                        Por(TipoDoLugar.Cadeira, 0f, frente + AFrente, 0f);
                        break;
                    case "BancoRefeitorio":
                        // atrás do banco, olhando para a mesa (o banco olha para ela), um lugar a cada 70 cm
                        for (int k = -2; k < 2; k++) Por(TipoDoLugar.Banco, (k + 0.5f) * 0.7f, costas - AtrasDoBanco, 0f);
                        break;
                    case "CamaCasal": case "CamaSolteiro": case "Beliche":
                    {
                        // ao lado da cama, de costas para ela, a cabeceira (as costas do móvel) à esquerda da raiz; do
                        // outro lado, a mesma conta espelhada
                        float z = costas + DaCabeceira;
                        Por(TipoDoLugar.Cama, meio.x - t.x / 2f - DaCama, z, -90f);
                        Por(TipoDoLugar.Cama, meio.x + t.x / 2f + DaCama, z, 90f, true);
                        break;
                    }
                    case "PiaInox":
                        Por(TipoDoLugar.Pia, 0f, frente + DaPia, 180f);
                        break;
                    case "Bancada":
                        Por(TipoDoLugar.Bancada, 0f, frente + DaBancada, 180f);
                        break;
                    case "FogaoIndustrial":
                        Por(TipoDoLugar.Fogao, 0f, frente + DoFogao, 180f);
                        break;
                    case "PiaDupla":
                        // as duas cubas (a 0,355 m do meio), na frente do espelho
                        for (int s = -1; s <= 1; s += 2) Por(TipoDoLugar.Espelho, s * 0.355f * e, frente + DoEspelho, 180f);
                        break;
                }
            }

            // os lugares em pé: perto do meio dos cômodos onde se fica à toa (o primeiro ponto livre numa espiral)
            foreach (var (f, tipo) in new[] { (1, "sala"), (1, "jogos"), (2, "refeitorio"), (3, "dormitorio"), (0, "operacoes") })
                foreach (var k in plantas[f].comodos)
                {
                    if (k.tipo != tipo) continue;
                    for (int passo = 0; passo < 40; passo++)
                    {
                        float ang = passo * 2.4f, r = 0.35f * Mathf.Sqrt(passo);
                        var p = new Vector3(k.Cx + Mathf.Cos(ang) * r, Base(f), k.Cz + Mathf.Sin(ang) * r);
                        if (!Livre(plantas[f], caixas[f], p, -1, k)) continue;
                        lista.Add(new LugarDoMorador
                        {
                            tipo = TipoDoLugar.EmPe, andar = f, raiz = p, giro = (passo * 47) % 360, movel = -1, comodo = k.tipo, nome = plantas[f].nomes[k],
                        });
                        break;
                    }
                }

            // e mais em pé em todos os cômodos (a parte 8: o choro no quarto e o andar sem rumo), depois dos de antes (os
            // índices dos lugares do save não mudam): de 1 a 3 por cômodo pelo tamanho, a 1,4 m um do outro
            for (int f = 0; f < Andares - 1; f++)
                foreach (var k in plantas[f].comodos)
                {
                    if (DoNucleo(k)) continue;
                    var postos = new List<Vector3>();
                    foreach (var l in lista) if (l.tipo == TipoDoLugar.EmPe && l.andar == f && k.Contem(l.raiz.x, l.raiz.z)) postos.Add(l.raiz);
                    int quer = Mathf.Clamp(Mathf.FloorToInt((k.x1 - k.x0) * (k.z1 - k.z0) / 14f), 1, 3);
                    for (int passo = 0; passo < 90 && postos.Count < quer; passo++)
                    {
                        float ang = passo * 2.4f, r = 0.35f * Mathf.Sqrt(passo);
                        var p = new Vector3(k.Cx + Mathf.Cos(ang) * r, Base(f), k.Cz + Mathf.Sin(ang) * r);
                        if (!k.Contem(p.x, p.z) || !Livre(plantas[f], caixas[f], p, -1, k)) continue;
                        bool perto = false;
                        foreach (var o in postos) perto |= new Vector2(o.x - p.x, o.z - p.z).sqrMagnitude < 1.4f * 1.4f;
                        if (perto) continue;
                        postos.Add(p);
                        lista.Add(new LugarDoMorador
                        {
                            tipo = TipoDoLugar.EmPe, andar = f, raiz = p, giro = (passo * 47) % 360, movel = -1, comodo = k.tipo, nome = plantas[f].nomes[k],
                        });
                    }
                }

            // os do lazer (a parte 8, a segunda fatia), depois de todos: o suporte dos tacos e a mesinha do fumo (como as
            // estantes), a cadeira do rádio, a mesa de dobrar a roupa e em volta da mesa de sinuca
            for (int i = 0; i < todos.Length; i++)
            {
                var m = todos[i];
                if (m.andar >= Andares - 1 || !MedidasDaMobilia.Tem(m.modelo)) continue;
                float e = m.escala;
                Vector3 t = MedidasDaMobilia.Real(m.modelo) * e;
                Vector2 meio = MedidasDaMobilia.Meio(m.modelo) * e;
                float frente = meio.y + t.z / 2f, costas = meio.y - t.z / 2f;

                void Por(TipoDoLugar tipo, float lx, float lz, float giroLocal) =>
                    Tentar(lista, plantas, todasAsCaixas, tipo, i, m, lx, lz, giroLocal, false);

                if (GuardaDoLazer(m.modelo))
                {
                    // na frente: dois (o braço alcança uns 30 cm para cada lado)
                    for (int k = 0; k < 2; k++)
                    {
                        float lx = meio.x + (k - 0.5f) * Mathf.Min(0.36f, t.x / 2f);
                        int antes = lista.Count;
                        Por(TipoDoLugar.Estante, lx, frente + DaEstante, 180f);
                        if (lista.Count > antes)
                        {
                            var l = lista[antes];
                            l.lado = lx;
                            lista[antes] = l;
                        }
                    }
                    // o canto do fumo: dois em pé dos lados da mesinha, olhando para ela (as duas poltronas do canto de leitura
                    // ficam de fora: a mesinha está perto demais delas, e as pernas entravam nela)
                    if (m.modelo == "MesaDeCanto")
                        foreach (float s in new[] { -1f, 1f })
                            Por(TipoDoLugar.EmPe, meio.x + s * 0.34f, frente + 0.62f, 180f);
                    continue;
                }
                switch (m.modelo)
                {
                    case "CadeiraEscritorio":
                        // a cadeira na frente da estação de rádio (a sala de operações): ouvir o rádio
                        if (NaFrenteDoRadio(m)) Por(TipoDoLugar.Radio, 0f, frente + AFrente, 0f);
                        break;
                    case "Mesa4":
                        // a mesa de dobrar a roupa (a lavanderia e a rouparia do B3), dos dois lados compridos, olhando para ela
                        if (m.andar != 3) break;
                        Por(TipoDoLugar.Dobrar, meio.x, frente + DaBancada, 180f);
                        Por(TipoDoLugar.Dobrar, meio.x, costas - DaBancada, 0f);
                        break;
                    case "MesaSinuca":
                        // em pé em volta da mesa (onde o jogador espera a vez dele), a uns 50 cm da beira, olhando para ela
                        foreach (float s in new[] { -1f, 1f })
                        {
                            Por(TipoDoLugar.Sinuca, meio.x - 0.3f * t.x, meio.y + s * (t.z / 2f + DaSinuca), s > 0f ? 180f : 0f);
                            Por(TipoDoLugar.Sinuca, meio.x + 0.3f * t.x, meio.y + s * (t.z / 2f + DaSinuca), s > 0f ? 180f : 0f);
                            Por(TipoDoLugar.Sinuca, meio.x + s * (t.x / 2f + DaSinuca), meio.y, s > 0f ? -90f : 90f);
                        }
                        break;
                }
            }
            return lista.ToArray();
        }

        /// <summary>Em pé, a raiz de quem espera a vez na sinuca fica a isso da beira da mesa (m).</summary>
        const float DaSinuca = 0.5f;

        // a cadeira de escritório tem a estação de rádio na frente dela (a até 2 m)
        static bool NaFrenteDoRadio(MoveisDoBunker.Movel cadeira)
        {
            foreach (var r in MoveisDoBunker.Todos)
                if (r.modelo == "EstacaoDeRadio" && r.andar == cadeira.andar && Vector3.Distance(r.lugar, cadeira.lugar) < 2f) return true;
            return false;
        }

        /// <summary>Os cômodos onde se fica à toa (o lazer em pé): as salas, o refeitório, o dormitório e as operações.</summary>
        public static bool DeFicar(LugarDoMorador l) =>
            l.comodo == "sala" || l.comodo == "jogos" || l.comodo == "refeitorio" || l.comodo == "dormitorio" || l.comodo == "operacoes";

        static void Tentar(List<LugarDoMorador> lista, PlantaDeBunker.Andar[] plantas, List<Caixa>[] caixas, TipoDoLugar tipo, int i,
                           MoveisDoBunker.Movel m, float lx, float lz, float giroLocal, bool espelho)
        {
            var giro = Quaternion.Euler(0f, m.giro, 0f);
            var p = m.lugar + giro * new Vector3(lx, 0f, lz);
            p.y = Base(m.andar);
            var planta = plantas[m.andar];
            var doMovel = planta.Em(m.lugar.x, m.lugar.z);
            if (doMovel == null || !Livre(planta, caixas[m.andar], p, i, doMovel))
            {
                _rejeitados.Add($"{Codigos[m.andar]} {m.modelo} ({m.lugar.x - X0:F1}, {m.lugar.z - Z0:F1}) {tipo}{(espelho ? " (o outro lado)" : "")}");
                return;
            }
            lista.Add(new LugarDoMorador
            {
                tipo = tipo, andar = m.andar, raiz = p, giro = Mathf.Repeat(m.giro + giroLocal, 360f), movel = i, espelho = espelho,
                alta = tipo == TipoDoLugar.Cama && (m.modelo == "CamaCasal" || m.modelo == "CamaSolteiro"),
                comodo = doMovel.tipo, nome = planta.nomes[doMovel],
            });
        }

        // a raiz cabe: no cômodo do móvel (ou num ligado a ele por uma passagem larga), longe das paredes, e fora dos outros móveis
        static bool Livre(PlantaDeBunker.Andar planta, List<Caixa> caixas, Vector3 p, int movel, Comodo doMovel)
        {
            var k = planta.Em(p.x, p.z);
            if (k == null || DoNucleo(k)) return false;
            if (k != doMovel)
            {
                bool ligado = false;
                foreach (var l in planta.ligacoes)
                    if (l.passagem && l.largura > 2f && ((l.a == k && l.b == doMovel) || (l.b == k && l.a == doMovel))) ligado = true;
                if (!ligado) return false;
            }
            float r = Raio + DaParede;
            bool dentroEmX = p.x - r >= k.x0 && p.x + r <= k.x1, dentroEmZ = p.z - r >= k.z0 && p.z + r <= k.z1;
            // (nas passagens largas, o lado aberto não conta como parede)
            if (!dentroEmX || !dentroEmZ)
            {
                bool aberto = false;
                foreach (var l in planta.ligacoes)
                {
                    if (!l.passagem || l.largura <= 2f || (l.a != k && l.b != k)) continue;
                    var o = l.a == k ? l.b : l.a;
                    if (o.Contem(p.x, p.z) || (Mathf.Abs(p.x - o.x0) < r || Mathf.Abs(p.x - o.x1) < r || Mathf.Abs(p.z - o.z0) < r || Mathf.Abs(p.z - o.z1) < r)) aberto = true;
                }
                if (!aberto) return false;
            }
            foreach (var c in caixas)
            {
                if (c.movel == movel) continue;
                // o ponto no giro da caixa, o mais perto dela, e a distância
                var d = new Vector2(p.x, p.z) - c.centro;
                float a = -c.giro * Mathf.Deg2Rad;
                var q = new Vector2(d.x * Mathf.Cos(a) + d.y * Mathf.Sin(a), -d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a));
                var perto = new Vector2(Mathf.Clamp(q.x, -c.meio.x, c.meio.x), Mathf.Clamp(q.y, -c.meio.y, c.meio.y));
                if ((q - perto).sqrMagnitude < Raio * Raio) return false;
            }
            return true;
        }
    }
}
