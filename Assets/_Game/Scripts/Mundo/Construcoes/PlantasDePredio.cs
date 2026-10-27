using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A planta de um prédio (Plano_Mapa, Parte M6, segunda fatia): uma lâmina com o corredor de um lado ao outro, os
    /// apartamentos dos dois lados dele e o núcleo atrás do corredor (a escada em U e o poço do elevador, parado, lado a
    /// lado). No térreo, a portaria na frente, alinhada com o núcleo, e dos lados dela as lojas (ou mais apartamentos).
    /// Os andares de cima são iguais (o andar tipo); o papel e o piso de cada apartamento mudam de um andar para outro.
    /// Em cima, a cobertura com a platibanda, a casinha da escada e a casa de máquinas.
    /// Coordenadas do prédio: o meio no centro, x de -W/2 a W/2, z de -D/2 (a frente, para a rua) a D/2 (o fundo); as
    /// linhas das paredes passam nas bordas dos cômodos (como na Planta das casas). Espelhar espelha a malha em x no fim.
    /// </summary>
    public sealed class PlantaDePredio
    {
        public int semente;
        public float W, D;
        /// <summary>Os andares (o térreo é o 0); a cobertura fica na altura do andar 'andares'.</summary>
        public int andares;
        public bool espelhar;
        /// <summary>O térreo com as lojas na frente, dos lados da portaria (ou com apartamentos).</summary>
        public bool lojasNoTerreo;
        /// <summary>
        /// O andar com as portas do elevador abertas (dá para cair no poço; as do térreo também ficam abertas, para sair
        /// dele, e a cabine fica parada lá em cima), ou -1.
        /// </summary>
        public int andarDoPocoAberto = -1;
        /// <summary>O hospital (Parte M6, terceira fatia): a mesma lâmina, com as salas dele no lugar dos apartamentos.</summary>
        public bool hospital;
        /// <summary>O corredor, de zc0 (o lado da frente) a zc1, de um lado ao outro do prédio.</summary>
        public float zc0, zc1;
        /// <summary>A escada (x de ex0 a ex1, de zc1 até o fundo) e o poço do elevador (px0 a px1, de zc1 a <see cref="FimDoPoco"/>), lado a lado.</summary>
        public float ex0, ex1, px0, px1;
        /// <summary>O meio da porta da frente (a da portaria).</summary>
        public float xPorta;
        /// <summary>As cores: a fachada, as faixas (a borda das lajes e os peitoris), as quinas, o corredor e a portaria.</summary>
        public string cor, faixa, quina, papelDoCorredor, pisoDoCorredor, papelDaPortaria;
        public float laje, peDireito, alturaDoAndar;
        public readonly List<Comodo> terreo = new List<Comodo>(), tipo = new List<Comodo>();
        public readonly List<Ligacao> ligacoesDoTerreo = new List<Ligacao>(), ligacoesDoTipo = new List<Ligacao>();

        /// <summary>O pé-direito do térreo (a portaria e as lojas, mais altos) e a altura do piso dele acima do chão.</summary>
        public const float PeDireitoDoTerreo = 4.0f, Piso = 0.15f;
        /// <summary>O núcleo (entre as linhas das paredes): a escada de dois lances de 1,6 m e o poço do elevador.</summary>
        public const float LarguraDaEscada = 3.6f, LarguraDoPoco = 2.4f, FundoDoPoco = 2.6f;
        /// <summary>O patamar de cada andar na escada (do lado do corredor), a platibanda e a casinha da escada na cobertura.</summary>
        public const float Patamar = 1.5f, Platibanda = 1.1f, CasinhaDaEscada = 3.0f;

        public float X0 => -W / 2; public float X1 => W / 2; public float Z0 => -D / 2; public float Z1 => D / 2;
        public float FimDoPoco => zc1 + FundoDoPoco;
        public float NucleoX0 => Mathf.Min(ex0, px0);
        public float NucleoX1 => Mathf.Max(ex1, px1);

        public float AlturaDoTerreo => PeDireitoDoTerreo + laje;
        /// <summary>A altura do piso do andar f (o topo da laje); a do andar 'andares' é a cobertura.</summary>
        public float Base(int f) => Piso + (f <= 0 ? 0f : AlturaDoTerreo + (f - 1) * alturaDoAndar);
        public float PeDireitoDe(int f) => f == 0 ? PeDireitoDoTerreo : peDireito;
        public float Cobertura => Base(andares);
        /// <summary>A altura da caixa de longe: do piso do térreo ao topo da platibanda.</summary>
        public float AlturaDaCaixa => Cobertura + Platibanda - Piso;

        /// <summary>O andar de uma altura (nas coordenadas do prédio): de 0 a 'andares' (a cobertura).</summary>
        public int AndarEm(float y)
        {
            if (y < Base(1) - 0.05f) return 0;
            return Mathf.Clamp(1 + Mathf.FloorToInt((y - Base(1) + 0.05f) / alturaDoAndar), 1, andares);
        }

        /// <summary>O vão da escada nas lajes de cima (fora o patamar, junto do corredor) e o do poço do elevador.</summary>
        public Rect VaoDaEscada => Rect.MinMaxRect(ex0 + 0.1f, zc1 + 0.1f + Patamar, ex1 - 0.1f, Z1 - 0.1f);
        public Rect VaoDoPoco => Rect.MinMaxRect(px0 + 0.1f, zc1 + 0.1f, px1 - 0.1f, FimDoPoco - 0.1f);

        /// <summary>As portas do elevador do andar f estão abertas.</summary>
        public bool PocoAbertoNo(int f) => andarDoPocoAberto >= 0 && (f == andarDoPocoAberto || f == 0);

        public List<Comodo> ComodosDo(int f) => f == 0 ? terreo : tipo;
        public List<Ligacao> LigacoesDo(int f) => f == 0 ? ligacoesDoTerreo : ligacoesDoTipo;

        static readonly string[] Papeis = { "PapelVerde", "PapelRosa", "ParedeBege", "ParedeAzul", "ParedeVerde", "ParedeInterna", "ParedeBegeEscura" };
        static readonly string[] Pisos = { "Taco", "Carpete", "Taco", "Piso" };

        /// <summary>O papel de um cômodo no andar f: o dos de todos é fixo; o dos apartamentos muda com o andar.</summary>
        public string PapelDe(int f, Comodo k)
        {
            if (k.apto < 0) return k.papel;
            if (k.tipo == "cozinha" || k.tipo == "banheiro" || k.tipo == "lavanderia") return "Azulejo";
            return Papeis[Escolha(f, k, k.tipo == "quarto" ? 1 : 2, Papeis.Length)];
        }

        public string PisoDe(int f, Comodo k)
        {
            if (k.apto < 0) return k.piso;
            if (k.tipo == "banheiro") return "Azulejo";
            if (k.tipo == "cozinha" || k.tipo == "lavanderia") return "Piso";
            return Pisos[Escolha(f, k, 3, Pisos.Length)];
        }

        int Escolha(int f, Comodo k, int sal, int n) => (int)(Sorteio.FracaoDoHash(Sorteio.Hash(semente, f * 64 + k.apto, sal)) * n) % n;

        /// <summary>Os cômodos e as ligações do andar f: cópias dos do térreo ou do tipo, com o andar, o papel e o piso dele.</summary>
        public void Andar(int f, List<Comodo> comodos, List<Ligacao> ligacoes)
        {
            comodos.Clear();
            ligacoes.Clear();
            var modelo = ComodosDo(f);
            var copias = new Dictionary<Comodo, Comodo>(modelo.Count);
            foreach (var k in modelo)
            {
                var c = new Comodo { tipo = k.tipo, andar = f, x0 = k.x0, x1 = k.x1, z0 = k.z0, z1 = k.z1, apto = k.apto, papel = PapelDe(f, k), piso = PisoDe(f, k) };
                copias[k] = c;
                comodos.Add(c);
            }
            foreach (var l in LigacoesDo(f))
                ligacoes.Add(new Ligacao { a = copias[l.a], b = l.b == null ? null : copias[l.b], pos = l.pos, passagem = l.passagem, lado = l.lado });
        }
    }

    /// <summary>
    /// O molde dos prédios: a lâmina do tamanho do lote (até 44 m de frente e 24 m de fundo), com o núcleo atrás do
    /// corredor (no meio, um pouco para um lado; nos prédios estreitos, encostado num lado), os apartamentos de ~7 m (a
    /// cozinha e a sala de um lado, o banheiro e o quarto do outro; abaixo de 6 m, a quitinete) e, onde não cabe
    /// apartamento, um depósito. Atrás do poço do elevador, a lavanderia do apartamento do lado.
    /// </summary>
    public static class PlantasDePredio
    {
        static float F(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        static float Arred(float v) => Mathf.Round(v * 10f) / 10f;
        static T Um<T>(System.Random r, params T[] o) => o[r.Next(o.Length)];

        /// <summary>A largura mínima de um apartamento (a quitinete) e a que se procura.</summary>
        const float MenorApartamento = 4.2f, LarguraDoApartamento = 7.2f;

        /// <summary>O prédio de um lote do centro: até 'largura' de frente e 'fundo' de fundo, com 'andares' andares e a fachada na cor dada.</summary>
        public static PlantaDePredio ParaOLote(System.Random r, Medidas m, float largura, float fundo, int andares, string cor)
        {
            var p = new PlantaDePredio
            {
                semente = r.Next(), andares = Mathf.Max(2, andares), cor = cor,
                laje = m.laje, peDireito = m.altura, alturaDoAndar = m.altura + m.laje,
            };
            p.W = Arred(Mathf.Clamp(largura, 9f, 44f));
            p.D = Arred(Mathf.Clamp(Mathf.Min(fundo, F(r, 18f, 24f)), 15f, 24f));
            p.espelhar = r.NextDouble() < 0.5;
            p.lojasNoTerreo = r.NextDouble() < 0.6;
            if (p.andares >= 3 && r.NextDouble() < 0.5) p.andarDoPocoAberto = r.Next(2, p.andares);
            p.faixa = Um(r, "Concreto", "ConcretoClaro", "ConcretoClaro");
            p.quina = cor == "Tijolo" ? "TijoloEscuro" : cor == "TijoloEscuro" ? "Tijolo" : Um(r, "Concreto", "TijoloEscuro");
            p.papelDoCorredor = Um(r, "ParedeVerdeEscura", "ParedeBegeEscura", "ParedeAzulEscura");
            p.pisoDoCorredor = Um(r, "Piso", "Carpete", "Piso");
            p.papelDaPortaria = Um(r, "ParedeBege", "Tijolo", "ParedeInterna");

            Nucleo(p, r, Arred(F(r, 2.2f, 2.8f)));
            Andar(p, r, true);
            Andar(p, r, false);
            return p;
        }

        // o corredor (de largura c, no meio do fundo) e o núcleo: com lugar para apartamentos (ou salas) dos dois lados
        // dele, no meio (um pouco para um lado); senão, encostado num lado, com o poço do lado de dentro (a lavanderia atrás
        // dele fica com o apartamento do lado)
        static void Nucleo(PlantaDePredio p, System.Random r, float c)
        {
            p.zc0 = Arred(-c / 2);
            p.zc1 = p.zc0 + c;
            const float ew = PlantaDePredio.LarguraDaEscada, pw = PlantaDePredio.LarguraDoPoco, cw = ew + pw;
            float livre = p.W - cw;
            float kx0;
            bool pocoADireita;
            if (livre < 2f * MenorApartamento + 0.6f)
            {
                bool naEsquerda = r.NextDouble() < 0.5;
                kx0 = naEsquerda ? p.X0 : p.X1 - cw;
                pocoADireita = naEsquerda;
            }
            else
            {
                float folga = livre / 2 - MenorApartamento;
                kx0 = Arred(p.X0 + livre / 2 + F(r, -1f, 1f) * Mathf.Min(folga, 3f));
                pocoADireita = r.NextDouble() < 0.5;
            }
            if (pocoADireita) { p.ex0 = kx0; p.ex1 = kx0 + ew; p.px0 = p.ex1; p.px1 = p.px0 + pw; }
            else { p.px0 = kx0; p.px1 = kx0 + pw; p.ex0 = p.px1; p.ex1 = p.ex0 + ew; }
        }

        /// <summary>
        /// O hospital (Parte M6, terceira fatia), no lugar de um prédio do centro e do tamanho dele (W × D): a lâmina com o
        /// corredor largo e o núcleo (a escada e o elevador). No térreo, a recepção na frente (alinhada com o núcleo, com a
        /// porta da frente), dos lados dela a emergência (com a porta das ambulâncias) e a triagem, a espera e os
        /// consultórios; atrás do corredor, o raio X, os consultórios e os banheiros. Em cima, as enfermarias e o posto de
        /// enfermagem na frente, os quartos e os banheiros atrás; atrás do poço, a rouparia. Todas as salas dão no corredor.
        /// </summary>
        public static PlantaDePredio ParaHospital(System.Random r, Medidas m, float W, float D, int andares)
        {
            var p = new PlantaDePredio
            {
                semente = r.Next(), andares = Mathf.Clamp(andares, 3, 6), hospital = true, W = W, D = D,
                laje = m.laje, peDireito = m.altura, alturaDoAndar = m.altura + m.laje,
                cor = Um(r, "ConcretoClaro", "ParedeInterna"), faixa = Um(r, "ParedeAzul", "ConcretoClaro"), quina = "ParedeAzul",
                papelDoCorredor = "ParedeInterna", pisoDoCorredor = "Piso", papelDaPortaria = "ParedeInterna",
            };
            p.espelhar = r.NextDouble() < 0.5;
            if (r.NextDouble() < 0.4) p.andarDoPocoAberto = r.Next(2, p.andares);
            Nucleo(p, r, Arred(F(r, 2.8f, 3.2f)));
            Hospital(p, r, true);
            Hospital(p, r, false);
            return p;
        }

        // um andar do hospital (o térreo ou o tipo)
        static void Hospital(PlantaDePredio p, System.Random r, bool terreo)
        {
            var lista = terreo ? p.terreo : p.tipo;
            var ligs = terreo ? p.ligacoesDoTerreo : p.ligacoesDoTipo;
            int andar = terreo ? 0 : 1;
            var corr = Novo(lista, "corredor", andar, p.X0, p.zc0, p.X1, p.zc1, -1, p.papelDoCorredor, p.pisoDoCorredor);
            // atrás do corredor, dos dois lados do núcleo
            var atras = terreo ? new[] { "consultorio", "raiox", "consultorio", "banheiro" } : new[] { "quarto", "quarto", "banheiro", "quarto", "rouparia" };
            Salas(lista, ligs, corr, andar, p.X0, p.NucleoX0, p.Z1, p.zc1, atras, 3.8f, r);
            Salas(lista, ligs, corr, andar, p.NucleoX1, p.X1, p.Z1, p.zc1, atras, 3.8f, r);
            Lavanderia(p, lista, ligs, andar, "rouparia");
            if (!terreo)
            {
                Salas(lista, ligs, corr, andar, p.X0, p.X1, p.Z0, p.zc0, new[] { "enfermaria", "enfermaria", "posto", "enfermaria" }, 6.5f, r);
                return;
            }
            // na frente do térreo: a recepção, alinhada com o núcleo, e dos lados a emergência (com a porta das ambulâncias)
            // e a triagem, e a espera e os consultórios
            float meio = (p.NucleoX0 + p.NucleoX1) / 2;
            float pa = Mathf.Clamp(Arred(meio - 3.4f), p.X0, Mathf.Max(p.X0, p.X1 - 6.8f)), pb = Mathf.Min(p.X1, pa + 6.8f);
            if (pa - p.X0 < MenorApartamento) pa = p.X0;
            if (p.X1 - pb < MenorApartamento) pb = p.X1;
            var recepcao = Novo(lista, "recepcao", 0, pa, p.Z0, pb, p.zc0, -1, "ParedeInterna", "Piso");
            p.xPorta = Arred((pa + pb) / 2);
            ligs.Add(new Ligacao { a = recepcao, lado = 0, pos = p.xPorta });
            ligs.Add(new Ligacao { a = corr, b = recepcao, pos = p.xPorta, passagem = true });
            bool emergenciaNaEsquerda = r.NextDouble() < 0.5;
            foreach (var (a, b, esquerda) in new[] { (p.X0, pa, true), (pb, p.X1, false) })
            {
                if (b - a < 0.05f) continue;
                if (esquerda == emergenciaNaEsquerda && b - a >= 7f)
                {
                    // a emergência junto da recepção (a porta larga para o corredor e a das ambulâncias para a rua) e a triagem
                    float xt = esquerda ? a + Mathf.Min(3.6f, (b - a) * 0.35f) : b - Mathf.Min(3.6f, (b - a) * 0.35f);
                    float ea = esquerda ? xt : a, eb = esquerda ? b : xt;
                    var emergencia = Novo(lista, "emergencia", 0, ea, p.Z0, eb, p.zc0, -1, "Azulejo", "Piso");
                    ligs.Add(new Ligacao { a = emergencia, lado = 0, pos = Arred((ea + eb) / 2) });
                    ligs.Add(new Ligacao { a = corr, b = emergencia, pos = Arred((ea + eb) / 2), passagem = true });
                    float ta = esquerda ? a : xt, tb = esquerda ? xt : b;
                    var triagem = Novo(lista, "triagem", 0, ta, p.Z0, tb, p.zc0, -1, "ParedeInterna", "Piso");
                    ligs.Add(new Ligacao { a = corr, b = triagem, pos = (ta + tb) / 2 });
                }
                else
                {
                    // a espera junto da recepção (aberta para o corredor) e os consultórios
                    float xe = esquerda ? b - Mathf.Min(7f, b - a) : a + Mathf.Min(7f, b - a);
                    float ea = esquerda ? xe : a, eb = esquerda ? b : xe;
                    var espera = Novo(lista, "espera", 0, ea, p.Z0, eb, p.zc0, -1, "ParedeBege", "Piso");
                    ligs.Add(new Ligacao { a = corr, b = espera, pos = Arred((ea + eb) / 2), passagem = true });
                    float ca = esquerda ? a : xe, cb = esquerda ? xe : b;
                    if (cb - ca > 0.05f) Salas(lista, ligs, corr, 0, ca, cb, p.Z0, p.zc0, new[] { "consultorio" }, 3.8f, r);
                }
            }
        }

        // uma fileira de salas de xa a xb, da fachada (zFachada) ao corredor (zCorredor), de ~'largura' cada, com os tipos
        // em sequência (o posto de enfermagem é aberto para o corredor; as outras têm porta). Uma sobra estreita vira depósito
        static void Salas(List<Comodo> lista, List<Ligacao> ligs, Comodo corr, int andar, float xa, float xb, float zFachada, float zCorredor,
                          string[] tipos, float largura, System.Random r)
        {
            float L = xb - xa;
            if (L < 0.05f) return;
            float za = Mathf.Min(zFachada, zCorredor), zb = Mathf.Max(zFachada, zCorredor);
            if (L < 3f)
            {
                var dep = Novo(lista, "deposito", andar, xa, za, xb, zb, -1, "Concreto", "Concreto");
                ligs.Add(new Ligacao { a = corr, b = dep, pos = (xa + xb) / 2 });
                return;
            }
            int n = Mathf.Max(1, Mathf.RoundToInt(L / largura));
            while (n > 1 && L / n < 3f) n--;
            int ini = r.Next(tipos.Length);
            for (int i = 0; i < n; i++)
            {
                float a = i == 0 ? xa : Arred(xa + L * i / n), b = i == n - 1 ? xb : Arred(xa + L * (i + 1) / n);
                string tipo = tipos[(ini + i) % tipos.Length];
                bool molhado = tipo == "banheiro" || tipo == "rouparia";
                var sala = Novo(lista, tipo, andar, a, za, b, zb, -1, molhado || tipo == "enfermaria" ? "Azulejo" : "ParedeInterna", molhado ? "Azulejo" : "Piso");
                ligs.Add(new Ligacao { a = corr, b = sala, pos = Arred((a + b) / 2), passagem = tipo == "posto" });
            }
        }

        static Comodo Novo(List<Comodo> lista, string tipo, int andar, float x0, float z0, float x1, float z1, int apto, string papel, string piso)
        {
            var c = new Comodo { tipo = tipo, andar = andar, x0 = x0, z0 = z0, x1 = x1, z1 = z1, apto = apto, papel = papel, piso = piso };
            lista.Add(c);
            return c;
        }

        // o térreo ou o andar tipo
        static void Andar(PlantaDePredio p, System.Random r, bool terreo)
        {
            var lista = terreo ? p.terreo : p.tipo;
            var ligs = terreo ? p.ligacoesDoTerreo : p.ligacoesDoTipo;
            int andar = terreo ? 0 : 1, apto = 0;
            var corr = Novo(lista, "corredor", andar, p.X0, p.zc0, p.X1, p.zc1, -1, p.papelDoCorredor, p.pisoDoCorredor);

            // atrás do corredor: os dois lados do núcleo (um lado estreito vira depósito) e a lavanderia atrás do poço
            Fileira(p, r, lista, ligs, corr, andar, p.X0, p.NucleoX0, p.Z1, p.zc1, ref apto);
            Fileira(p, r, lista, ligs, corr, andar, p.NucleoX1, p.X1, p.Z1, p.zc1, ref apto);
            Lavanderia(p, lista, ligs, andar);

            if (!terreo)
            {
                Fileira(p, r, lista, ligs, corr, andar, p.X0, p.X1, p.Z0, p.zc0, ref apto);
                return;
            }
            // na frente do térreo: a portaria, alinhada com o núcleo, e dos lados as lojas ou os apartamentos (um lado
            // estreito demais fica com a portaria)
            float meio = (p.NucleoX0 + p.NucleoX1) / 2;
            float pa = Mathf.Clamp(Arred(meio - 2.7f), p.X0, Mathf.Max(p.X0, p.X1 - 5.4f)), pb = Mathf.Min(p.X1, pa + 5.4f);
            if (pa - p.X0 < MenorApartamento) pa = p.X0;
            if (p.X1 - pb < MenorApartamento) pb = p.X1;
            var portaria = Novo(lista, "portaria", 0, pa, p.Z0, pb, p.zc0, -1, p.papelDaPortaria, "Piso");
            p.xPorta = Arred((pa + pb) / 2);
            ligs.Add(new Ligacao { a = portaria, lado = 0, pos = p.xPorta });
            ligs.Add(new Ligacao { a = corr, b = portaria, pos = p.xPorta, passagem = true });
            foreach (var (a, b) in new[] { (p.X0, pa), (pb, p.X1) })
            {
                if (b - a < 0.05f) continue;
                if (p.lojasNoTerreo) Lojas(p, lista, ligs, a, b);
                else Fileira(p, r, lista, ligs, corr, 0, a, b, p.Z0, p.zc0, ref apto);
            }
        }

        // uma fileira de apartamentos de xa a xb, da fachada (zFachada) ao corredor (zCorredor)
        static void Fileira(PlantaDePredio p, System.Random r, List<Comodo> lista, List<Ligacao> ligs, Comodo corr, int andar,
                            float xa, float xb, float zFachada, float zCorredor, ref int apto)
        {
            float largura = xb - xa;
            if (largura < 0.05f) return;
            if (largura < MenorApartamento)
            {
                // estreito demais para um apartamento: um depósito, com a porta no corredor
                var dep = Novo(lista, "deposito", andar, xa, Mathf.Min(zFachada, zCorredor), xb, Mathf.Max(zFachada, zCorredor), -1, "Concreto", "Concreto");
                ligs.Add(new Ligacao { a = corr, b = dep, pos = (xa + xb) / 2 });
                return;
            }
            int n = Mathf.Max(1, Mathf.RoundToInt(largura / LarguraDoApartamento));
            while (n > 1 && largura / n < MenorApartamento) n--;
            float w = largura / n;
            for (int i = 0; i < n; i++)
            {
                float a = i == 0 ? xa : Arred(xa + w * i), b = i == n - 1 ? xb : Arred(xa + w * (i + 1));
                Apartamento(lista, ligs, corr, andar, a, b, zFachada, zCorredor, apto++, (i & 1) == 1, F(r, 0.52f, 0.6f));
            }
        }

        // um apartamento de xa a xb: a cozinha (a entrada, junto do corredor) e a sala (na fachada) de um lado, o banheiro e
        // o quarto do outro; com menos de 6 m, a quitinete (a cozinha e o banheiro junto do corredor, a sala na fachada)
        static void Apartamento(List<Comodo> lista, List<Ligacao> ligs, Comodo corr, int andar, float xa, float xb,
                                float zFachada, float zCorredor, int apto, bool trocar, float divisa)
        {
            float dir = Mathf.Sign(zFachada - zCorredor);
            float Z(float d) => zCorredor + dir * d;
            Comodo Sala(string tipo, float a, float b, float d0, float d1) =>
                Novo(lista, tipo, andar, a, Mathf.Min(Z(d0), Z(d1)), b, Mathf.Max(Z(d0), Z(d1)), apto, null, null);
            float largura = xb - xa, fundo = Mathf.Abs(zFachada - zCorredor);
            if (largura < 6f)
            {
                const float ban = 2.0f, faixa = 2.6f;
                float xs = trocar ? xa + ban : xb - ban;
                float ca = trocar ? xs : xa, cb = trocar ? xb : xs;
                float ba = trocar ? xa : xs, bb = trocar ? xs : xb;
                var coz = Sala("cozinha", ca, cb, 0f, faixa);
                var banh = Sala("banheiro", ba, bb, 0f, faixa);
                var sala = Sala("sala", xa, xb, faixa, fundo);
                ligs.Add(new Ligacao { a = corr, b = coz, pos = (ca + cb) / 2 });
                ligs.Add(new Ligacao { a = coz, b = sala, pos = (ca + cb) / 2, passagem = true });
                ligs.Add(new Ligacao { a = coz, b = banh, pos = Z(faixa / 2) });
                return;
            }
            const float fundoDaCozinha = 3.0f, fundoDoBanheiro = 2.4f;
            float s = Arred(trocar ? xb - largura * divisa : xa + largura * divisa);
            float la = trocar ? s : xa, lb = trocar ? xb : s;
            float qa = trocar ? xa : s, qb = trocar ? s : xb;
            var cozinha = Sala("cozinha", la, lb, 0f, fundoDaCozinha);
            var salaDeEstar = Sala("sala", la, lb, fundoDaCozinha, fundo);
            var banheiro = Sala("banheiro", qa, qb, 0f, fundoDoBanheiro);
            var quarto = Sala("quarto", qa, qb, fundoDoBanheiro, fundo);
            ligs.Add(new Ligacao { a = corr, b = cozinha, pos = (la + lb) / 2 });
            ligs.Add(new Ligacao { a = cozinha, b = salaDeEstar, pos = (la + lb) / 2, passagem = true });
            ligs.Add(new Ligacao { a = salaDeEstar, b = quarto, pos = Z((fundoDaCozinha + fundo) / 2) });
            ligs.Add(new Ligacao { a = cozinha, b = banheiro, pos = Z(fundoDoBanheiro / 2) });
        }

        // as lojas do térreo, de xa a xb: um salão cada (até ~9 m), com a porta de enrolar para a rua
        static void Lojas(PlantaDePredio p, List<Comodo> lista, List<Ligacao> ligs, float xa, float xb)
        {
            float largura = xb - xa;
            int n = Mathf.Max(1, Mathf.RoundToInt(largura / 9f));
            float w = largura / n;
            for (int i = 0; i < n; i++)
            {
                float a = i == 0 ? xa : Arred(xa + w * i), b = i == n - 1 ? xb : Arred(xa + w * (i + 1));
                var loja = Novo(lista, "loja", 0, a, p.Z0, b, p.zc0, -1, "ParedeInterna", "Piso");
                ligs.Add(new Ligacao { a = loja, lado = 0, pos = Arred((a + b) / 2) });
            }
        }

        // a lavanderia atrás do poço do elevador (no hospital, a rouparia), com a porta para o cômodo do lado (o que encosta
        // mais nela)
        static void Lavanderia(PlantaDePredio p, List<Comodo> lista, List<Ligacao> ligs, int andar, string tipo = "lavanderia")
        {
            bool pocoADireita = p.px0 >= p.ex1 - 0.01f;
            float xLado = pocoADireita ? p.px1 : p.px0;
            Comodo melhor = null;
            float maior = 0f;
            foreach (var k in lista)
            {
                bool encosta = pocoADireita ? Mathf.Abs(k.x0 - xLado) < 0.01f : Mathf.Abs(k.x1 - xLado) < 0.01f;
                if (!encosta) continue;
                float sobre = Mathf.Min(k.z1, p.Z1) - Mathf.Max(k.z0, p.FimDoPoco);
                if (sobre > maior) { maior = sobre; melhor = k; }
            }
            if (melhor == null || maior < 1.6f) return;   // sem vizinho, o fundo do poço fica fechado
            var lav = Novo(lista, tipo, andar, p.px0, p.FimDoPoco, p.px1, p.Z1, melhor.apto, "Azulejo", "Piso");
            ligs.Add(new Ligacao { a = melhor, b = lav, pos = (Mathf.Max(melhor.z0, lav.z0) + Mathf.Min(melhor.z1, lav.z1)) / 2 });
        }
    }
}
