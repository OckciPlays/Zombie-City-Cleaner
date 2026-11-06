using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O sorteio dos NPCs (a parte 2 do Plano_NPCs_Bunker.md da pasta de arte, as decisões 1 a 6, e a tela das cartas, de
    /// 04/11/2026): a ficha de cada um (o nome, a idade, a história em duas partes, as habilidades com a paixão, os traços,
    /// as condições, o que ele não faz e a aparência para o sorteio das roupas), os 52 do mundo em grupos, com os laços, e as
    /// cartas da tela (o baralho e as mãos). A mesma semente dá o mesmo mundo. A ordem de uma ficha: o sexo, a idade, o nome,
    /// as condições, as histórias, o que ele não faz, as habilidades, as paixões, os traços, a aparência e o apelido.
    /// </summary>
    public static class SorteioDosMoradores
    {
        public const int AnosDoApocalipse = 5;
        public const int IdadeMinima = 16, IdadeMaxima = 80;
        /// <summary>Os NPCs de cada mundo, as cartas de cada rodada e quantos começam no bunker (as rodadas).</summary>
        public const int NpcsNoMundo = 52, CartasPorRodada = 5, MoradoresNoComeco = 5;
        /// <summary>A chance de um novo de um grupo nascer com um laço com um dos de antes do grupo.</summary>
        public const float ChanceDeLacoNoGrupo = 0.6f;
        /// <summary>A chance de o primeiro de um grupo ter um laço com alguém de outro (o irmão que está noutra comunidade).</summary>
        public const float ChanceDeLacoDeFora = 0.12f;
        /// <summary>A chance de ser fumante (a decisão 4).</summary>
        public const float ChanceDeFumante = 0.4f;
        /// <summary>A história dos anos do apocalipse de quem era da família e ficou de fora do bunker (nunca sorteada).</summary>
        public const string SeparadoDaFamilia = "SeparadoDaFamilia";

        // os tamanhos dos grupos do mapa (as famílias, os casais, os bandos e os sozinhos), com o peso
        static readonly (int tamanho, float peso)[] TamanhosDosGrupos = { (1, 18f), (2, 24f), (3, 24f), (4, 18f), (5, 10f), (6, 6f) };
        // quantos vêm com o parente da família (o outro do casal, um irmão dele, um amigo)
        static readonly (int extras, float peso)[] ComOParente = { (0, 45f), (1, 40f), (2, 15f) };
        // os laços de fora do grupo (sem o casal: o casal fica no mesmo grupo)
        static readonly string[] LacosDeFora = { LacoDoMorador.Irmao, LacoDoMorador.Primo, LacoDoMorador.Amigo, LacoDoMorador.Ex };

        // ------------------------------------------------------------------------------------------- os do mundo

        /// <summary>
        /// Os NPCs de um mundo (<paramref name="total"/>, 52): o parente da família primeiro, às vezes com quem veio com ele (o
        /// grupo 0), e os grupos do mapa, de 1 a 6, com os laços dentro deles (e uns poucos com alguém de outro grupo).
        /// </summary>
        public static NpcsDoMundo SortearMundo(int semente, int total = NpcsNoMundo)
        {
            var r = new Random(Misturar(semente, 6));
            var m = new NpcsDoMundo { semente = semente };
            var parente = Gerar(r, PedidoDoParente(r, Usados(m)), m.proximoId++);
            m.fichas.Add(parente);
            var familia = new List<FichaDoMorador> { parente };
            int extras = Pesado(r, ComOParente, x => x.peso).extras;
            for (int k = 0; k < extras && m.fichas.Count < total; k++)
            {
                var n = NoGrupo(r, m, familia, 1f);
                familia.Add(n);
                m.fichas.Add(n);
            }
            Grupos(r, m, total);
            AcertarOsParentes(m);
            return m;
        }

        /// <summary>
        /// Completa os NPCs de um mundo até o <paramref name="total"/> (um mundo salvo antes das cartas, com os moradores da
        /// tela de antes): os grupos do mapa, com os ids a partir do <see cref="NpcsDoMundo.proximoId"/>.
        /// </summary>
        public static void CompletarMundo(NpcsDoMundo m, int semente, int total = NpcsNoMundo)
        {
            var r = new Random(Misturar(semente, 8));
            Grupos(r, m, total);
        }

        static void Grupos(Random r, NpcsDoMundo m, int total)
        {
            int grupo = m.fichas.Count == 0 ? 0 : m.fichas.Max(f => f.grupo) + 1;
            while (m.fichas.Count < total)
            {
                int tamanho = Math.Min(Pesado(r, TamanhosDosGrupos, t => t.peso).tamanho, total - m.fichas.Count);
                var membros = new List<FichaDoMorador>();
                for (int i = 0; i < tamanho; i++)
                {
                    FichaDoMorador n;
                    if (i > 0) n = NoGrupo(r, m, membros, ChanceDeLacoNoGrupo);
                    else if (m.fichas.Count > 0 && r.NextDouble() < ChanceDeLacoDeFora) n = NoGrupo(r, m, m.fichas, 1f, LacosDeFora);
                    else n = Gerar(r, new Pedido { usados = Usados(m) }, m.proximoId++);
                    n.grupo = grupo;
                    // quem herdou o "entrou no bunker com a família" de um laço (o irmão do pai) está pelo mapa: se perdeu dela
                    if (n.depois == ListasDosMoradores.ComAFamilia) n.depois = SeparadoDaFamilia;
                    membros.Add(n);
                    m.fichas.Add(n);
                }
                grupo++;
            }
        }

        // ------------------------------------------------------------------------------------------------ as cartas

        /// <summary>
        /// O baralho do mundo: os ids na ordem em que as cartas saem, <see cref="CartasPorRodada"/> por rodada. O parente da
        /// família está sempre na primeira mão (num lugar sorteado); se não for escolhido, fica no mapa.
        /// </summary>
        public static List<int> Baralho(NpcsDoMundo m)
        {
            var r = new Random(Misturar(m.semente, 7));
            var parente = m.Parente;
            var outros = Embaralhar(r, m.fichas.Where(f => f != parente).Select(f => f.id).ToList());
            if (parente == null) return outros;
            var b = outros.Take(CartasPorRodada - 1).ToList();
            b.Insert(r.Next(b.Count + 1), parente.id);
            b.AddRange(outros.Skip(CartasPorRodada - 1));
            return b;
        }

        /// <summary>As cartas da <paramref name="rodada"/> (de 0 a 4): as do baralho, sem repetir as de antes.</summary>
        public static List<FichaDoMorador> Mao(NpcsDoMundo m, int rodada) =>
            Baralho(m).Skip(rodada * CartasPorRodada).Take(CartasPorRodada).Select(m.DoId).Where(f => f != null).ToList();

        /// <summary>
        /// Separa os do bunker (os <paramref name="escolhidos"/>, na ordem) dos do mapa (o resto). Quem entrou no bunker com a
        /// família e ficou de fora se perdeu dela no Dia 0 (a história <see cref="SeparadoDaFamilia"/>).
        /// </summary>
        public static (List<FichaDoMorador> bunker, List<FichaDoMorador> mapa) Separar(NpcsDoMundo m, IList<int> escolhidos)
        {
            var bunker = escolhidos.Select(m.DoId).Where(f => f != null).ToList();
            var mapa = m.fichas.Where(f => !escolhidos.Contains(f.id)).ToList();
            foreach (var f in mapa)
                if (f.depois == ListasDosMoradores.ComAFamilia) f.depois = SeparadoDaFamilia;
            return (bunker, mapa);
        }

        /// <summary>Um morador sozinho, sem laço com a família (quem pede abrigo no portão, o sobrevivente perdido).</summary>
        public static FichaDoMorador SortearUm(int semente, int id, IEnumerable<string> nomesUsados = null)
        {
            var r = new Random(Misturar(semente, 5));
            var usados = new HashSet<string>(nomesUsados ?? Enumerable.Empty<string>());
            var f = Gerar(r, new Pedido { usados = usados }, id);
            if (f.depois == ListasDosMoradores.ComAFamilia) f.depois = "SozinhoNoMato";
            return f;
        }

        /// <summary>O laço inverso: se o outro é o pai deste, este é o filho do outro.</summary>
        public static string Inverso(string tipo) => tipo switch
        {
            LacoDoMorador.Pai => LacoDoMorador.Filho,
            LacoDoMorador.Filho => LacoDoMorador.Pai,
            LacoDoMorador.Avo => LacoDoMorador.Neto,
            LacoDoMorador.Neto => LacoDoMorador.Avo,
            _ => tipo,
        };

        /// <summary>
        /// A opinião de <paramref name="a"/> sobre <paramref name="b"/> no começo (a decisão 6), de −100 a 100: a base do
        /// laço, os traços e um pouco de acaso, que muda de um par para o outro e de um lado para o outro.
        /// </summary>
        public static int OpiniaoDoComeco(FichaDoMorador a, FichaDoMorador b)
        {
            int v = 0;
            var l = a.LacoCom(b.id);
            if (l != null)
                v += l.tipo switch
                {
                    LacoDoMorador.Casal => 40,
                    LacoDoMorador.Pai or LacoDoMorador.Filho or LacoDoMorador.Irmao or LacoDoMorador.Avo or LacoDoMorador.Neto => 25,
                    LacoDoMorador.Primo => 15,
                    LacoDoMorador.Amigo => 20,
                    LacoDoMorador.Ex => -20,
                    _ => 0,
                };
            if (b.Tem("Gentil")) v += 10;
            if (b.Tem("Grosso")) v -= 10;
            if (a.Tem(ListasDosMoradores.Fumante) && b.Tem(ListasDosMoradores.Fumante)) v += 5;
            if (a.Tem("Pessimista")) v -= 5;
            if (a.Tem("Alegre")) v += 5;
            v += Misturar(a.id * 7919 + b.id, (a.nome?.Length ?? 0) + 11) % 21 - 10;
            return Math.Max(-100, Math.Min(100, v));
        }

        // -------------------------------------------------------------------------------------------- os textos

        /// <summary>"o pai da família", "a avó da família" (vazio se não é parente).</summary>
        public static string TextoDoParente(FichaDoMorador f) => f.parente switch
        {
            ParenteDaFamilia.Pai => f.mulher ? "a mãe da família" : "o pai da família",
            ParenteDaFamilia.Avo => f.mulher ? "a avó da família" : "o avô da família",
            ParenteDaFamilia.Tio => f.mulher ? "a tia da família" : "o tio da família",
            _ => "",
        };

        /// <summary>"72 anos · homem · o pai da família".</summary>
        public static string TextoDaIdade(FichaDoMorador f)
        {
            string t = $"{f.idade} anos · {(f.mulher ? "mulher" : "homem")}";
            string p = TextoDoParente(f);
            return p.Length > 0 ? $"{t} · {p}" : t;
        }

        public static string FraseDeAntes(FichaDoMorador f) => Frase(f, ListasDosMoradores.DeAntes(f.antes));
        public static string FraseDeDepois(FichaDoMorador f) => Frase(f, ListasDosMoradores.DeDepois(f.depois));
        public static string NomeDeAntes(FichaDoMorador f) => ListasDosMoradores.DeAntes(f.antes)?.Nome(f.mulher) ?? f.antes;
        public static string NomeDeDepois(FichaDoMorador f) => ListasDosMoradores.DeDepois(f.depois)?.Nome(f.mulher) ?? f.depois;

        static string Frase(FichaDoMorador f, HistoriaDoMorador h)
        {
            if (h == null) return "";
            // os anos no ofício, até a aposentadoria (uns 40)
            int anos = Math.Max(1, Math.Min(40, f.idade - AnosDoApocalipse - h.inicio));
            return h.frase.Replace("{N}", f.nome).Replace("{o}", f.mulher ? "a" : "o")
                          .Replace("{anos}", anos == 1 ? "1 ano" : $"{anos} anos")
                          .Replace("{escola}", f.idade - AnosDoApocalipse < 18 ? "no colégio" : "na faculdade");
        }

        public static string TextoDosTracos(FichaDoMorador f) =>
            f.tracos.Count == 0 ? "—" : string.Join(" · ", f.tracos.Select(t => ListasDosMoradores.Traco(t)?.Nome(f.mulher) ?? t));

        public static string TextoDasCondicoes(FichaDoMorador f) =>
            f.condicoes.Count == 0 ? "—" : string.Join(" · ", f.condicoes.Select(c => ListasDosMoradores.Condicao(c)?.nome ?? c));

        public static string TextoDoNaoFaz(FichaDoMorador f)
        {
            var nomes = ListasDosMoradores.NomesDoNaoFaz.Where(n => f.NaoFaz(n.i)).Select(n => n.nome).ToList();
            return nomes.Count == 0 ? "—" : string.Join(" · ", nomes);
        }

        /// <summary>Os laços vistos deste ("irmão da Martha (+31)"), com a opinião dele sobre o outro; o parente, primeiro.</summary>
        public static List<string> TextosDosLacos(FichaDoMorador f, NpcsDoMundo g)
        {
            var lista = new List<string>();
            if (f.parente == ParenteDaFamilia.Pai) lista.Add(f.mulher ? "a mãe dos jogadores" : "o pai dos jogadores");
            else if (f.parente == ParenteDaFamilia.Avo) lista.Add(f.mulher ? "a avó dos jogadores" : "o avô dos jogadores");
            else if (f.parente == ParenteDaFamilia.Tio) lista.Add(f.mulher ? "a tia dos jogadores" : "o tio dos jogadores");
            foreach (var l in f.lacos)
            {
                var o = g?.DoId(l.outro);
                if (o == null) continue;
                int op = OpiniaoDoComeco(f, o);
                lista.Add($"{PalavraDoLaco(l.tipo, f.mulher)} {(o.mulher ? "da" : "do")} {o.nome} ({(op >= 0 ? "+" : "")}{op})");
            }
            return lista;
        }

        /// <summary>O que este é para o outro, pelo laço (o que o outro é para este) e pelo sexo deste.</summary>
        public static string PalavraDoLaco(string tipo, bool mulher) => tipo switch
        {
            LacoDoMorador.Casal => mulher ? "esposa" : "marido",
            LacoDoMorador.Pai => mulher ? "filha" : "filho",
            LacoDoMorador.Filho => mulher ? "mãe" : "pai",
            LacoDoMorador.Irmao => mulher ? "irmã" : "irmão",
            LacoDoMorador.Avo => mulher ? "neta" : "neto",
            LacoDoMorador.Neto => mulher ? "avó" : "avô",
            LacoDoMorador.Primo => mulher ? "prima" : "primo",
            LacoDoMorador.Amigo => mulher ? "amiga" : "amigo",
            LacoDoMorador.Ex => mulher ? "ex-mulher" : "ex-marido",
            _ => tipo,
        };

        /// <summary>A ficha inteira em texto (o log do teste e a folha dos sorteados).</summary>
        public static string Descrever(FichaDoMorador f, NpcsDoMundo g)
        {
            var sb = new StringBuilder();
            sb.AppendLine(f.NomeCompleto.ToUpperInvariant());
            sb.AppendLine(TextoDaIdade(f));
            sb.AppendLine($"ANTES: {NomeDeAntes(f)}. {FraseDeAntes(f)}");
            sb.AppendLine($"NOS ANOS DO APOCALIPSE: {NomeDeDepois(f)}. {FraseDeDepois(f)}");
            for (int h = 0; h < ListasDosMoradores.Habilidades; h++)
            {
                string valor = f.Incapaz((Habilidade)h) ? " —" : $"{f.habilidades[h],2}";
                string paixao = f.paixoes[h] == Paixao.Apaixonado ? " ♦♦" : f.paixoes[h] == Paixao.Interessado ? " ♦" : "";
                sb.AppendLine($"  {ListasDosMoradores.NomesDasHabilidades[h],-11}{valor}{paixao}");
            }
            sb.AppendLine($"TRAÇOS: {TextoDosTracos(f)}");
            sb.AppendLine($"CONDIÇÕES: {TextoDasCondicoes(f)}");
            sb.AppendLine($"NÃO FAZ: {TextoDoNaoFaz(f)}");
            var lacos = TextosDosLacos(f, g);
            sb.AppendLine($"LAÇOS: {(lacos.Count == 0 ? "—" : string.Join("; ", lacos))}");
            sb.AppendLine($"ROUPA: {f.aparencia.tipoDaRoupa}, pele {f.aparencia.pele}, cabelo {f.aparencia.cabelo}{(f.aparencia.grisalho ? " (grisalho)" : "")}");
            return sb.ToString();
        }

        // ----------------------------------------------------------------------------------------- o pedido de uma ficha

        sealed class Pedido
        {
            public bool? mulher;
            public int idadeMin = -1, idadeMax = -1;
            public string sobrenome;
            public int? pele, cabelo;
            public ParenteDaFamilia parente;
            public string depois;
            public HashSet<string> usados = new HashSet<string>();
        }

        // os nomes e os sobrenomes que já estão no mundo (os sobrenomes com o "#" na frente): quem não é parente não repete
        // nenhum dos dois (sem isso, a Tessa Gomez saía sem laço com o Jimmy Gomez)
        static HashSet<string> Usados(NpcsDoMundo g)
        {
            var s = new HashSet<string>();
            foreach (var f in g.fichas)
            {
                s.Add(f.nome);
                s.Add("#" + f.sobrenome);
                if (!string.IsNullOrEmpty(f.apelido)) s.Add("@" + f.apelido);
            }
            return s;
        }

        static Pedido PedidoDoParente(Random r, HashSet<string> usados)
        {
            var p = new Pedido { usados = usados };
            double x = r.NextDouble();
            if (x < 0.55) { p.parente = ParenteDaFamilia.Pai; p.idadeMin = 55; p.idadeMax = 78; }
            else if (x < 0.8) { p.parente = ParenteDaFamilia.Avo; p.idadeMin = 68; p.idadeMax = 80; }
            else { p.parente = ParenteDaFamilia.Tio; p.idadeMin = 55; p.idadeMax = 75; }
            p.depois = ListasDosMoradores.ComAFamilia;
            return p;
        }

        // os laços possíveis de um novo (N) com um de antes (E), como "o que N é para E", com o peso
        static readonly (string tipo, float peso)[] PesosDosLacos =
        {
            (LacoDoMorador.Casal, 25f), (LacoDoMorador.Filho, 11f), (LacoDoMorador.Pai, 9f), (LacoDoMorador.Irmao, 18f),
            (LacoDoMorador.Avo, 3f), (LacoDoMorador.Neto, 3f), (LacoDoMorador.Primo, 8f), (LacoDoMorador.Amigo, 18f),
            (LacoDoMorador.Ex, 6f),
        };

        static int Contar(FichaDoMorador f, string tipo) => f.lacos.Count(l => l.tipo == tipo);

        // se o novo pode ser "tipo" de E (o que N é para E)
        static bool Pode(FichaDoMorador e, string tipo)
        {
            bool parente = e.parente != ParenteDaFamilia.Nenhum;
            switch (tipo)
            {
                case LacoDoMorador.Casal: return e.idade >= 18 && Contar(e, LacoDoMorador.Casal) == 0;
                case LacoDoMorador.Filho: return !parente && e.idade - 18 >= IdadeMinima;
                case LacoDoMorador.Pai: return !parente && e.idade + 18 <= 78 && Contar(e, LacoDoMorador.Pai) < 2;
                case LacoDoMorador.Irmao: return true;
                case LacoDoMorador.Avo: return !parente && e.idade + 40 <= IdadeMaxima && Contar(e, LacoDoMorador.Avo) < 4;
                case LacoDoMorador.Neto: return !parente && e.idade - 40 >= IdadeMinima;
                case LacoDoMorador.Primo: return true;
                case LacoDoMorador.Amigo: return true;
                case LacoDoMorador.Ex: return e.idade >= 18;
                default: return false;
            }
        }

        static Pedido PedidoDoLaco(Random r, FichaDoMorador e, string tipo, HashSet<string> usados)
        {
            var p = new Pedido { usados = usados };
            int lo, hi;
            void Parente(double sobrenome)
            {
                if (r.NextDouble() < sobrenome) p.sobrenome = e.sobrenome;
                p.pele = Math.Max(0, Math.Min(5, e.aparencia.pele + r.Next(-1, 2)));
                p.cabelo = Math.Max(0, Math.Min(5, e.aparencia.cabelo + r.Next(-1, 2)));
            }
            switch (tipo)
            {
                case LacoDoMorador.Casal:
                    p.mulher = !e.mulher;
                    lo = e.idade - 8; hi = e.idade + 8;
                    if (r.NextDouble() < 0.8) p.sobrenome = e.sobrenome;
                    if (e.parente != ParenteDaFamilia.Nenhum) { p.parente = e.parente; p.depois = ListasDosMoradores.ComAFamilia; lo = Math.Max(lo, 50); }
                    else if (r.NextDouble() < 0.6) p.depois = e.depois;
                    lo = Math.Max(lo, 18);
                    break;
                case LacoDoMorador.Filho:
                    lo = e.idade - 45; hi = e.idade - 18;
                    Parente(0.85);
                    if (r.NextDouble() < 0.7) p.depois = e.depois;
                    break;
                case LacoDoMorador.Pai:
                    lo = e.idade + 18; hi = Math.Min(78, e.idade + 45);
                    Parente(0.85);   // com um pai já no grupo, o sexo deste é o outro (no NoGrupo)
                    if (r.NextDouble() < 0.7) p.depois = e.depois;
                    break;
                case LacoDoMorador.Irmao:
                    lo = e.idade - 12; hi = e.idade + 12;
                    Parente(0.85);
                    if (r.NextDouble() < 0.6) p.depois = e.depois;
                    break;
                case LacoDoMorador.Avo:
                    lo = e.idade + 40; hi = e.idade + 60;
                    Parente(0.6);
                    break;
                case LacoDoMorador.Neto:
                    lo = e.idade - 60; hi = e.idade - 40;
                    Parente(0.6);
                    break;
                case LacoDoMorador.Primo:
                    lo = e.idade - 15; hi = e.idade + 15;
                    Parente(0.4);
                    break;
                case LacoDoMorador.Amigo:
                    // o amigo de um velho é quase sempre mais novo que ele (senão o grupo fica todo de velhos)
                    lo = e.idade - 25; hi = e.idade + (e.idade >= 55 ? 5 : 25);
                    if (r.NextDouble() < 0.5) p.depois = e.depois;
                    break;
                default: // o ex
                    p.mulher = !e.mulher;
                    lo = Math.Max(18, e.idade - 10); hi = e.idade + 10;
                    break;
            }
            if (p.depois == ListasDosMoradores.ComAFamilia && p.parente == ParenteDaFamilia.Nenhum && r.NextDouble() < 0.5) p.depois = null;
            p.idadeMin = Math.Max(IdadeMinima, lo);
            p.idadeMax = Math.Min(e.parente != ParenteDaFamilia.Nenhum ? IdadeMaxima : 78, hi);
            if (p.idadeMax < p.idadeMin) p.idadeMax = p.idadeMin;
            return p;
        }

        // um novo de um grupo: com a chance, com um laço com um dos outros (dos tipos pedidos; null, todos) e os laços que
        // vêm junto
        static FichaDoMorador NoGrupo(Random r, NpcsDoMundo g, List<FichaDoMorador> outros, float chance, string[] tipos = null)
        {
            var usados = Usados(g);
            if (outros.Count > 0 && r.NextDouble() < chance)
            {
                for (int tentativa = 0; tentativa < 6; tentativa++)
                {
                    var e = outros[r.Next(outros.Count)];
                    var servem = PesosDosLacos.Where(t => Pode(e, t.tipo) && (tipos == null || tipos.Contains(t.tipo))).ToList();
                    if (servem.Count == 0) continue;
                    var tipo = Pesado(r, servem, t => t.peso).tipo;
                    var pedido = PedidoDoLaco(r, e, tipo, usados);
                    if (tipo == LacoDoMorador.Pai)
                    {
                        var outroPai = e.lacos.Where(l => l.tipo == LacoDoMorador.Pai).Select(l => g.DoId(l.outro)).FirstOrDefault(x => x != null);
                        if (outroPai != null) pedido.mulher = !outroPai.mulher;
                    }
                    var n = Gerar(r, pedido, g.proximoId++);
                    var todos = new List<FichaDoMorador>(outros) { n };
                    Ligar(e, n, tipo);
                    Propagar(r, todos, e, n, tipo);
                    return n;
                }
            }
            return Gerar(r, new Pedido { usados = usados }, g.proximoId++);
        }

        // n é "tipo" de e (o que n é para e)
        static void Ligar(FichaDoMorador e, FichaDoMorador n, string tipo)
        {
            if (e == n || e.LacoCom(n.id) != null || n.LacoCom(e.id) != null) return;
            e.lacos.Add(new LacoDoMorador { outro = n.id, tipo = tipo });
            n.lacos.Add(new LacoDoMorador { outro = e.id, tipo = Inverso(tipo) });
        }

        static IEnumerable<FichaDoMorador> Com(List<FichaDoMorador> todos, FichaDoMorador f, string tipo) =>
            f.lacos.Where(l => l.tipo == tipo).Select(l => todos.Find(x => x.id == l.outro)).Where(x => x != null).ToList();

        // a família que vem junto: o filho de um é filho do outro do casal e irmão dos outros filhos; o irmão de um tem os
        // mesmos pais e irmãos; o pai de um é pai dos irmãos dele e, às vezes, casal do outro pai
        static void Propagar(Random r, List<FichaDoMorador> todos, FichaDoMorador e, FichaDoMorador n, string tipo)
        {
            // os irmãos por tabela (o irmão do irmão, o outro filho do mesmo pai) com no máximo 27 anos de diferença (com os
            // 52 do mundo, o mais velho de um e o mais novo do outro passavam disso)
            bool PodeSerIrmao(FichaDoMorador a, FichaDoMorador b) => Math.Abs(a.idade - b.idade) <= 27;
            bool PodeSerPai(FichaDoMorador pai, FichaDoMorador filho) =>
                pai.idade - filho.idade >= 18 && pai.idade - filho.idade <= 45 && Contar(filho, LacoDoMorador.Pai) < 2 &&
                pai.LacoCom(filho.id) == null && (Com(todos, filho, LacoDoMorador.Pai).All(p => p.mulher != pai.mulher));

            switch (tipo)
            {
                case LacoDoMorador.Filho: // n é filho de e
                    foreach (var s in Com(todos, e, LacoDoMorador.Casal))
                        if (r.NextDouble() < 0.75 && PodeSerPai(s, n)) Ligar(n, s, LacoDoMorador.Pai);
                    foreach (var pai in Com(todos, n, LacoDoMorador.Pai))
                        foreach (var c in Com(todos, pai, LacoDoMorador.Filho))
                            if (c != n && PodeSerIrmao(c, n)) Ligar(c, n, LacoDoMorador.Irmao);
                    break;
                case LacoDoMorador.Pai: // n é pai de e
                    foreach (var x in Com(todos, e, LacoDoMorador.Irmao))
                        if (PodeSerPai(n, x)) Ligar(x, n, LacoDoMorador.Pai);
                    foreach (var p in Com(todos, e, LacoDoMorador.Pai))
                        if (p != n && p.mulher != n.mulher && Contar(p, LacoDoMorador.Casal) == 0 && Contar(n, LacoDoMorador.Casal) == 0 &&
                            Math.Abs(p.idade - n.idade) <= 12 && r.NextDouble() < 0.6)
                            Ligar(p, n, LacoDoMorador.Casal);
                    break;
                case LacoDoMorador.Irmao: // n é irmão de e
                    foreach (var p in Com(todos, e, LacoDoMorador.Pai))
                        if (PodeSerPai(p, n)) Ligar(n, p, LacoDoMorador.Pai);
                    foreach (var x in Com(todos, e, LacoDoMorador.Irmao))
                        if (x != n && PodeSerIrmao(x, n)) Ligar(x, n, LacoDoMorador.Irmao);
                    break;
                case LacoDoMorador.Casal: // n é o outro do casal de e
                    foreach (var c in Com(todos, e, LacoDoMorador.Filho))
                        if (r.NextDouble() < 0.6 && PodeSerPai(n, c)) Ligar(c, n, LacoDoMorador.Pai);
                    break;
            }
        }

        // o parente da família é o primeiro e o outro do casal dele; os outros não são
        static void AcertarOsParentes(NpcsDoMundo g)
        {
            if (g.fichas.Count == 0) return;
            var primeiro = g.fichas[0];
            for (int i = 1; i < g.fichas.Count; i++)
            {
                var f = g.fichas[i];
                var l = f.LacoCom(primeiro.id);
                f.parente = l != null && l.tipo == LacoDoMorador.Casal ? primeiro.parente : ParenteDaFamilia.Nenhum;
            }
        }

        // ------------------------------------------------------------------------------------------------- a ficha

        static readonly (int de, int ate, float peso)[] FaixasDeIdade =
            { (16, 24, 20f), (25, 34, 26f), (35, 44, 22f), (45, 54, 15f), (55, 64, 10f), (65, 78, 7f) };

        static FichaDoMorador Gerar(Random r, Pedido p, int id)
        {
            var f = new FichaDoMorador { id = id, parente = p.parente };

            // o sexo e a idade
            f.mulher = p.mulher ?? r.NextDouble() < 0.5;
            if (p.idadeMin >= 0) f.idade = r.Next(p.idadeMin, p.idadeMax + 1);
            else
            {
                var faixa = Pesado(r, FaixasDeIdade, x => x.peso);
                f.idade = r.Next(faixa.de, faixa.ate + 1);
            }

            // o nome (nomes de velho acima de 58, às vezes desde os 50) e o sobrenome
            bool velho = f.idade >= 58 || (f.idade >= 50 && r.NextDouble() < 0.4);
            var nomes = f.mulher ? (velho ? ListasDosMoradores.MulheresVelhas : ListasDosMoradores.MulheresNovas)
                                 : (velho ? ListasDosMoradores.HomensVelhos : ListasDosMoradores.HomensNovos);
            f.nome = nomes[r.Next(nomes.Length)];
            for (int t = 0; t < 60 && p.usados.Contains(f.nome); t++) f.nome = nomes[r.Next(nomes.Length)];
            p.usados.Add(f.nome);
            f.sobrenome = p.sobrenome;
            if (f.sobrenome == null)
            {
                var sobrenomes = ListasDosMoradores.Sobrenomes;
                f.sobrenome = sobrenomes[r.Next(sobrenomes.Length)];
                for (int t = 0; t < 60 && p.usados.Contains("#" + f.sobrenome); t++) f.sobrenome = sobrenomes[r.Next(sobrenomes.Length)];
            }
            p.usados.Add("#" + f.sobrenome);

            // as condições (o parente velho quase sempre tem uma)
            var (uma, segunda) = ListasDosMoradores.ChanceDeCondicao(f.idade);
            if (p.parente != ParenteDaFamilia.Nenhum) uma = Math.Max(uma, 0.9f);
            int quantas = r.NextDouble() < uma ? (r.NextDouble() < segunda ? 2 : 1) : 0;
            for (int k = 0; k < quantas; k++)
            {
                var servem = ListasDosMoradores.Condicoes.Where(c => !f.condicoes.Contains(c.chave) && c.peso(f.idade) > 0f).ToList();
                if (servem.Count == 0) break;
                f.condicoes.Add(Pesado(r, servem, c => c.peso(f.idade)).chave);
            }

            // as histórias (abaixo de 22, só o estudante; acima de 66, a de antes é mais vezes a do aposentado)
            var antes = ListasDosMoradores.Antes.Where(h => f.idade >= h.idadeMin && f.idade <= h.idadeMax).ToList();
            var historia = Pesado(r, antes, h => h.peso * (h.chave == "Estudante" && f.idade >= 22 ? 0.4f
                                                         : h.chave == "Aposentado" && f.idade >= 66 ? 5f : 1f)) ?? ListasDosMoradores.Antes[0];
            f.antes = historia.chave;
            // o "entrou no bunker com a família" só vem pedido (o parente e quem veio com ele): os outros estão pelo mapa
            var depois = p.depois != null ? ListasDosMoradores.DeDepois(p.depois)
                                          : Pesado(r, ListasDosMoradores.Depois.Where(h => h.chave != ListasDosMoradores.ComAFamilia).ToList(), h => h.peso);
            f.depois = depois.chave;

            // o que ele não faz: a história, o coração fraco e, às vezes, o acaso
            if (historia.naoFaz != Incapacidade.Nenhuma && r.NextDouble() < historia.chanceDoNaoFaz) f.naoFaz |= historia.naoFaz;
            if (f.TemCondicao("CoracaoFraco")) f.naoFaz |= Incapacidade.NadaDeTrabalhoPesado;
            double acaso = r.NextDouble();
            bool cuida = historia.paixao == Habilidade.Medicina, luta = historia.paixao == Habilidade.Combate,
                 cozinha = historia.paixao == Habilidade.Cozinha;
            if (acaso < 0.04 && !cozinha) f.naoFaz |= Incapacidade.NaoCozinha;
            else if (acaso < 0.07 && !luta) f.naoFaz |= Incapacidade.NaoLuta;
            else if (acaso < 0.10 && !cuida) f.naoFaz |= Incapacidade.NaoCuidaDeDoentes;

            // as habilidades: um pouco de sorte, a idade e as histórias
            float idadeF = Math.Max(0f, Math.Min(1f, (f.idade - IdadeMinima) / 40f));
            for (int h = 0; h < ListasDosMoradores.Habilidades; h++)
                f.habilidades[h] = r.Next(0, 4) + (int)Math.Round(idadeF * r.NextDouble() * 6.0);
            foreach (var (h, pts) in historia.bonus) f.habilidades[(int)h] += pts;
            foreach (var (h, pts) in depois.bonus) f.habilidades[(int)h] += pts;
            if (historia.paixao.HasValue) f.habilidades[(int)historia.paixao.Value] += (int)Math.Round(idadeF * 3f);
            if (historia.chave == "Estudante")
                foreach (int h in Embaralhar(r, Enumerable.Range(0, ListasDosMoradores.Habilidades).ToList()).Take(3)) f.habilidades[h] += 2;
            if (historia.chave == "Aposentado")
            {
                var oficios = Embaralhar(r, new List<int> { (int)Habilidade.Mecanica, (int)Habilidade.Construcao, (int)Habilidade.Plantio,
                                                             (int)Habilidade.Cozinha, (int)Habilidade.Oficio });
                f.habilidades[oficios[0]] += 3;
                f.habilidades[oficios[1]] += 2;
            }
            for (int h = 0; h < ListasDosMoradores.Habilidades; h++)
            {
                f.habilidades[h] = Math.Max(0, Math.Min(20, f.habilidades[h]));
                if (f.Incapaz((Habilidade)h)) f.habilidades[h] = 0;
            }

            // as paixões: de 1 a 3, a primeira mais vezes na do ofício
            int paixoes = 1 + (r.NextDouble() < 0.55 ? 1 : 0) + (r.NextDouble() < 0.2 ? 1 : 0);
            for (int k = 0; k < paixoes; k++)
            {
                int h;
                if (k == 0 && historia.paixao.HasValue && r.NextDouble() < 0.65) h = (int)historia.paixao.Value;
                else
                {
                    var livres = Enumerable.Range(0, ListasDosMoradores.Habilidades)
                                           .Where(x => f.paixoes[x] == Paixao.Nenhuma && !f.Incapaz((Habilidade)x)).ToList();
                    if (livres.Count == 0) break;
                    h = Pesado(r, livres, x => 1f + f.habilidades[x]);
                }
                if (f.Incapaz((Habilidade)h) || f.paixoes[h] != Paixao.Nenhuma) continue;
                f.paixoes[h] = r.NextDouble() < (f.habilidades[h] >= 6 ? 0.4 : 0.3) ? Paixao.Apaixonado : Paixao.Interessado;
            }

            // os traços: de 1 a 3, o fumante numa das vagas, os que as histórias puxam e os outros pelo peso
            int vagas = Pesado(r, new[] { (1, 30f), (2, 50f), (3, 20f) }, x => x.Item2).Item1;
            if (r.NextDouble() < ChanceDeFumante) f.tracos.Add(ListasDosMoradores.Fumante);
            foreach (var puxa in new[] { historia.puxa, depois.puxa })
                if (f.tracos.Count < vagas && puxa != null && r.NextDouble() < 0.5 && Serve(f, puxa)) f.tracos.Add(puxa);
            while (f.tracos.Count < vagas)
            {
                var servem = ListasDosMoradores.Tracos.Where(t => t.Peso(f.idade) > 0f && Serve(f, t.chave)).ToList();
                if (servem.Count == 0) break;
                f.tracos.Add(Pesado(r, servem, t => t.Peso(f.idade)).chave);
            }

            // a aparência: a roupa da história (acima de 65, a do idoso; os que viveram no mato às vezes a do sobrevivente)
            var a = f.aparencia;
            a.mulher = f.mulher;
            a.tipoDaRoupa = historia.roupa ?? "Civil";
            if (f.idade > 65) a.tipoDaRoupa = "Idoso";
            else if ((f.depois == "SozinhoNoMato" || f.depois == "Saqueador" || f.depois == "CacouZumbis" || f.depois == "Estrada") &&
                     r.NextDouble() < 0.35) a.tipoDaRoupa = "Sobrevivente";
            a.semente = r.Next(1, int.MaxValue);
            a.pele = p.pele ?? r.Next(0, 6);
            a.cabelo = p.cabelo ?? r.Next(0, 6);
            a.grisalho = f.idade >= 62 || (f.idade >= 50 && r.NextDouble() < (f.idade - 50) / 12.0);
            a.semBarba = f.idade < 19;

            // o apelido: o do nome, o do ofício ou o "Red" dos ruivos
            double ap = r.NextDouble();
            if (ap < 0.35 && ListasDosMoradores.ApelidosDoNome.TryGetValue(f.nome, out var doNome)) f.apelido = doNome;
            else if (ap < 0.6 && ListasDosMoradores.ApelidosDoOficio.TryGetValue(f.antes, out var doOficio)) f.apelido = doOficio;
            else if (ap < 0.7 && a.cabelo == 4 && !a.grisalho) f.apelido = "Red";
            else f.apelido = "";
            // o apelido não se repete no mundo (dois "Sarge" entre os 52)
            if (f.apelido.Length > 0 && !p.usados.Add("@" + f.apelido)) f.apelido = "";
            return f;
        }

        // se o traço pode entrar: não repetido, sem o do par e sem brigar com as condições
        static bool Serve(FichaDoMorador f, string chave)
        {
            if (f.tracos.Contains(chave)) return false;
            var t = ListasDosMoradores.Traco(chave);
            if (t == null) return false;
            if (t.oposto != null && f.tracos.Contains(t.oposto)) return false;
            if (chave == "Rapido" && f.TemCondicao("JoelhoRuim")) return false;
            if (chave == "SaudeDeFerro" && f.condicoes.Count >= 2) return false;
            return true;
        }

        // --------------------------------------------------------------------------------------------- o acaso

        static List<T> Embaralhar<T>(Random r, List<T> lista)
        {
            for (int i = lista.Count - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                (lista[i], lista[j]) = (lista[j], lista[i]);
            }
            return lista;
        }

        static T Pesado<T>(Random r, IList<T> lista, Func<T, float> peso)
        {
            float total = 0f;
            foreach (var x in lista) total += Math.Max(0f, peso(x));
            if (total <= 0f || lista.Count == 0) return default;
            double sorteio = r.NextDouble() * total;
            foreach (var x in lista)
            {
                sorteio -= Math.Max(0f, peso(x));
                if (sorteio < 0) return x;
            }
            return lista[lista.Count - 1];
        }

        /// <summary>A semente embaralhada (o final do SplitMix), como no sorteio das roupas: as vizinhas dão grupos diferentes.</summary>
        static int Misturar(int semente, int canal)
        {
            ulong z = ((ulong)(uint)semente * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)canal * 0xD1B54A32D192ED03UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)(z & 0x7FFFFFFF);
        }
    }
}
