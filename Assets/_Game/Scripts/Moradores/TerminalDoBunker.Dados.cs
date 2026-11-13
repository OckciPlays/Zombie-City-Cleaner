using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Salvar;
using ZombieCityCleaner.World;
using static ZombieCityCleaner.Mundo.PlantaDeBunker;

namespace ZombieCityCleaner.Moradores
{
    /// <summary>As abas do terminal do bunker (a decisão 20 do Plano_NPCs_Bunker.md da pasta de arte), na ordem da tela.</summary>
    public enum AbaDoTerminal : byte { Membros, Estoque, Lugares, Registro, Mapa, Radio, Cartas }

    /// <summary>Um morador no terminal (o anfitrião manda; as outras máquinas não têm a vida deles).</summary>
    [Serializable]
    public class MembroNoTerminal
    {
        public int id;
        public string nome, completo, idade, agora, carrega, saude;
        public int andar, cama = -1;
        public float fome, sede, sono, fumo = -1f;
        /// <summary>O humor (de 0 a 100), a faixa ("feliz"), a crise ("" sem) e os motivos ("+8 se divertiu").</summary>
        public int humor;
        public string faixa, crise;
        public List<string> motivos = new List<string>();
    }

    /// <summary>Uma carta no terminal: os NPCs do mundo (os 52), o nome só dos que os jogadores conhecem.</summary>
    [Serializable]
    public class CartaNoTerminal
    {
        public int id, grupo;
        public bool conhecido, noBunker;
        public string nome, idade;
    }

    /// <summary>
    /// O que o anfitrião manda para a tela do terminal, a cada segundo, da aba aberta: os moradores (todas as abas usam), as
    /// metas dos lugares e, na aba de cada um, o registro e as cartas. O estoque e as estantes cada máquina já tem (a lista
    /// das estantes vai pela rede), e o mapa e os jogadores também.
    /// </summary>
    [Serializable]
    public class DadosDoTerminal
    {
        public int aba;
        public bool comMoradores;
        public List<MembroNoTerminal> membros = new List<MembroNoTerminal>();
        public List<int> metas = new List<int>();
        public List<string> registro = new List<string>();
        public List<CartaNoTerminal> cartas = new List<CartaNoTerminal>();
    }

    /// <summary>
    /// As camas do bunker (o móvel: a cama de casal tem dois donos, as outras um), tiradas dos lugares de deitar
    /// (<see cref="LugaresDoBunker"/>), iguais em todas as máquinas.
    /// </summary>
    public static class CamasDoBunker
    {
        public struct Cama
        {
            public int movel, andar, capacidade;
            public string nome;
            public List<int> lados;
        }

        static List<Cama> _todas;

        public static List<Cama> Todas
        {
            get
            {
                if (_todas != null) return _todas;
                _todas = new List<Cama>();
                var lugares = LugaresDoBunker.Todos;
                var moveis = MoveisDoBunker.Todos;
                var porMovel = new Dictionary<int, List<int>>();
                var ordem = new List<int>();
                foreach (int i in LugaresDoBunker.DoTipo(TipoDoLugar.Cama))
                {
                    int m = lugares[i].movel;
                    if (!porMovel.TryGetValue(m, out var l))
                    {
                        porMovel[m] = l = new List<int>();
                        ordem.Add(m);
                    }
                    l.Add(i);
                }
                // o número de cada móvel no cômodo dele ("beliche 3")
                var contagem = new Dictionary<string, int>();
                var total = new Dictionary<string, int>();
                foreach (int m in ordem)
                {
                    string chave = lugares[porMovel[m][0]].nome + "|" + moveis[m].modelo;
                    total[chave] = total.TryGetValue(chave, out int t) ? t + 1 : 1;
                }
                foreach (int m in ordem)
                {
                    var l0 = lugares[porMovel[m][0]];
                    string modelo = moveis[m].modelo;
                    string chave = l0.nome + "|" + modelo;
                    int n = contagem[chave] = contagem.TryGetValue(chave, out int c) ? c + 1 : 1;
                    string palavra = modelo switch
                    {
                        "CamaCasal" => "cama de casal",
                        "Beliche" => "beliche",
                        "CamaSolteiro" => "cama de solteiro",
                        _ => modelo,
                    };
                    _todas.Add(new Cama
                    {
                        movel = m, andar = l0.andar, lados = porMovel[m], capacidade = modelo == "CamaCasal" ? 2 : 1,
                        nome = $"{Codigos[l0.andar]} {l0.nome} · {palavra}{(total[chave] > 1 ? $" {n}" : "")}",
                    });
                }
                return _todas;
            }
        }

        /// <summary>A cama do lugar de deitar (-1: nenhuma).</summary>
        public static int Indice(int lugar)
        {
            if (lugar < 0 || lugar >= LugaresDoBunker.Todos.Length) return -1;
            int movel = LugaresDoBunker.Todos[lugar].movel;
            var todas = Todas;
            for (int i = 0; i < todas.Count; i++) if (todas[i].movel == movel) return i;
            return -1;
        }

        public static string Nome(int lugar)
        {
            int i = Indice(lugar);
            return i < 0 ? "nenhuma" : Todas[i].nome;
        }
    }

    /// <summary>Os nomes dos lugares do bunker para o terminal ("B2 despensa", "B1 · estante de aço").</summary>
    public static class NomesNoTerminal
    {
        /// <summary>O nome do cômodo no ponto (as coordenadas da obra), ou o do andar.</summary>
        public static string Comodo(int andar, float x, float z)
        {
            if (andar < 0 || andar >= Andares) return "?";
            var a = DoAndar(andar);
            var k = a.Em(x, z);
            return k != null && a.nomes.TryGetValue(k, out var n) ? n : NomesDosAndares[andar];
        }

        /// <summary>Onde fica a estante: "B2 Despensa · estante de aço".</summary>
        public static string Estante(int estante)
        {
            var todos = MoveisDoBunker.Todos;
            if (estante < 0 || estante >= todos.Length) return "?";
            var m = todos[estante];
            var forma = EstantesDoBunker.FormaDa(estante);
            return $"{Codigos[m.andar]} {Comodo(m.andar, m.lugar.x, m.lugar.z)} · {(forma != null ? forma.nome : m.modelo)}";
        }

        /// <summary>O que o corpo faz, em palavras ("lendo", "lavando a louça").</summary>
        public static string Agora(VidaDoMorador v)
        {
            if (v.Atividade == AtividadeDoMorador.Sinuca) return "jogando sinuca";
            if (!v.noLugar && v.lugar >= 0)
                return v.Atividade switch
                {
                    AtividadeDoMorador.Dormir => "indo dormir",
                    AtividadeDoMorador.Banheiro => "indo ao banheiro",
                    AtividadeDoMorador.Comer => "indo comer",
                    AtividadeDoMorador.Trabalho => "indo aos afazeres",
                    AtividadeDoMorador.Lazer => "indo descansar",
                    AtividadeDoMorador.Cochilo => "indo cochilar",
                    AtividadeDoMorador.Rezar => "indo rezar",
                    AtividadeDoMorador.Buscar => "indo ao estoque",
                    AtividadeDoMorador.Guardar => "guardando o taco",
                    _ => "andando",
                };
            return AcoesDoCorpo.Acao(v.acao) switch
            {
                AcaoDoCorpo.Dormindo => v.Atividade == AtividadeDoMorador.Cochilo ? "cochilando" : "dormindo",
                AcaoDoCorpo.Sofa => v.Atividade == AtividadeDoMorador.Cochilo ? "cochilando no sofá" : MoradoresDoBunker.VeATV(v.lugar) ? "vendo TV no sofá" : "sentado no sofá",
                AcaoDoCorpo.Cadeira => v.lugar >= 0 && LugaresDoBunker.Todos[v.lugar].tipo == TipoDoLugar.Radio ? "ouvindo o rádio" : "sentado",
                AcaoDoCorpo.DobrandoRoupa => "dobrando a roupa",
                AcaoDoCorpo.Lendo => "lendo",
                AcaoDoCorpo.Rezando => "rezando",
                AcaoDoCorpo.Triste => "triste, sentado",
                AcaoDoCorpo.Banco => "sentado no refeitório",
                AcaoDoCorpo.Comendo => "comendo",
                AcaoDoCorpo.LavandoALouca => "lavando a louça",
                AcaoDoCorpo.Consertando => "consertando na bancada",
                AcaoDoCorpo.Cozinhando => "cozinhando",
                AcaoDoCorpo.Espelho => "no banheiro",
                AcaoDoCorpo.EstanteAlto or AcaoDoCorpo.EstanteMeio or AcaoDoCorpo.EstanteBaixo or AcaoDoCorpo.EstanteChao =>
                    v.Atividade == AtividadeDoMorador.Guardar ? "guardando no suporte" : "pegando no estoque",
                _ => AgendaDoMorador.Nome(v.Atividade),
            };
        }
    }

    /// <summary>
    /// No anfitrião: os dados do terminal (<see cref="DadosDoTerminal"/>) e a ficha de um NPC em texto, para a tela do
    /// terminal de qualquer jogador (a <c>UI/TerminalDoBunker</c>, pelo <c>JogadorRede.Terminal</c>).
    /// </summary>
    public static class TerminalNoAnfitriao
    {
        public static DadosDoTerminal Montar(AbaDoTerminal aba)
        {
            var d = new DadosDoTerminal { aba = (int)aba };
            var mb = MoradoresDoBunker.Instancia;
            d.comMoradores = mb != null && mb.Todos.Count > 0;
            if (mb != null)
                foreach (var m in mb.Todos) d.membros.Add(Membro(mb, m));
            d.metas.AddRange(EstoqueDoBunker.Metas);
            if (aba == AbaDoTerminal.Registro && mb != null)
                for (int i = mb.Registro.Count - 1; i >= 0; i--) d.registro.Add(mb.Registro[i]);
            if (aba == AbaDoTerminal.Cartas) Cartas(d, mb);
            return d;
        }

        static MembroNoTerminal Membro(MoradoresDoBunker mb, MoradoresDoBunker.Morador m)
        {
            var v = m.vida;
            var f = m.ficha;
            string onde = v.lugar >= 0 && v.noLugar ? $"{Codigos[LugaresDoBunker.Todos[v.lugar].andar]}, {LugaresDoBunker.Todos[v.lugar].nome}"
                        : v.lugar >= 0 ? $"para {Codigos[LugaresDoBunker.Todos[v.lugar].andar]}, {LugaresDoBunker.Todos[v.lugar].nome}"
                        : $"{Codigos[Mathf.Clamp(v.andar, 0, Andares - 1)]}, {NomesNoTerminal.Comodo(v.andar, v.pos.x, v.pos.z)}";
            var saude = new List<string>();
            if (v.fome < AgendaDoMorador.Urgente) saude.Add("com fome");
            if (v.sede < AgendaDoMorador.Urgente) saude.Add("com sede");
            if (v.sono < AgendaDoMorador.SonoUrgente) saude.Add(f.mulher ? "exausta" : "exausto");
            if (saude.Count == 0) saude.Add("bem");
            foreach (var c in f.condicoes) saude.Add((ListasDosMoradores.Condicao(c)?.nome ?? c).ToLowerInvariant());
            var carrega = new List<string>();
            foreach (var g in v.vagas)
                if (g.qtd > 0) carrega.Add(g.qtd > 1 ? Loot.Itens.ComNumero((Loot.Item)g.item, g.qtd) : Loot.Itens.Nome((Loot.Item)g.item));
            string idade = SorteioDosMoradores.TextoDaIdade(f);
            return new MembroNoTerminal
            {
                id = f.id, nome = m.Nome, completo = f.NomeCompleto, idade = idade,
                agora = $"{NomesNoTerminal.Agora(v)}{(m.fumo != MoradoresDoBunker.EstadoDoFumo.Nada ? ", fumando" : "")} ({onde})",
                carrega = carrega.Count == 0 ? "nada" : string.Join(", ", carrega),
                saude = string.Join(" · ", saude), andar = v.andar, cama = v.cama,
                fome = v.fome, sede = v.sede, sono = v.sono, fumo = f.Tem(ListasDosMoradores.Fumante) ? v.fumo : -1f,
                humor = Mathf.RoundToInt(v.humor), faixa = HumorDoMorador.NomeDaFaixa(HumorDoMorador.Faixa(v.humor), f.mulher),
                crise = v.crise != 0 ? HumorDoMorador.NomeDaCrise(v.Crise, f.mulher) : "", motivos = mb.TextosDosMotivos(m),
            };
        }

        // os NPCs do mundo: os do bunker e os do mapa (do mundo salvo), o nome só dos conhecidos
        static void Cartas(DadosDoTerminal d, MoradoresDoBunker mb)
        {
            var salvo = Salvamento.Atual;
            var noBunker = new HashSet<int>();
            if (mb != null) foreach (var m in mb.Todos) noBunker.Add(m.ficha.id);
            var todas = new List<FichaDoMorador>();
            if (salvo != null)
            {
                if (salvo.moradores != null) todas.AddRange(salvo.moradores);
                if (salvo.npcsDoMapa != null) todas.AddRange(salvo.npcsDoMapa);
            }
            else if (mb != null) foreach (var m in mb.Todos) todas.Add(m.ficha);
            todas.Sort((a, b) => a.id.CompareTo(b.id));
            foreach (var f in todas)
            {
                bool conhecido = Conhecido(f.id, noBunker, salvo);
                d.cartas.Add(new CartaNoTerminal
                {
                    id = f.id, grupo = f.grupo, conhecido = conhecido, noBunker = noBunker.Contains(f.id),
                    nome = conhecido ? f.NomeCompleto : "", idade = conhecido ? $"{f.idade} anos" : "",
                });
            }
        }

        static bool Conhecido(int id, HashSet<int> noBunker, MundoSalvo salvo) =>
            noBunker.Contains(id) || (salvo != null && ((salvo.npcsDescobertos != null && salvo.npcsDescobertos.Contains(id))
                                                        || (salvo.moradores != null && salvo.moradores.Exists(f => f.id == id))));

        /// <summary>A ficha inteira de um NPC conhecido, em linhas (null: não existe ou ninguém conhece).</summary>
        public static string Ficha(int id)
        {
            var mb = MoradoresDoBunker.Instancia;
            var salvo = Salvamento.Atual;
            var noBunker = new HashSet<int>();
            var doBunker = new List<FichaDoMorador>();
            if (mb != null)
                foreach (var m in mb.Todos)
                {
                    noBunker.Add(m.ficha.id);
                    doBunker.Add(m.ficha);
                }
            var f = doBunker.Find(x => x.id == id) ?? salvo?.NpcDoId(id);
            if (f == null || !Conhecido(id, noBunker, salvo)) return null;
            var g = new NpcsDoMundo();
            if (salvo != null)
            {
                if (salvo.moradores != null) g.fichas.AddRange(salvo.moradores);
                if (salvo.npcsDoMapa != null) g.fichas.AddRange(salvo.npcsDoMapa);
            }
            foreach (var x in doBunker) if (g.DoId(x.id) == null) g.fichas.Add(x);

            var sb = new StringBuilder();
            sb.Append(f.NomeCompleto.ToUpperInvariant()).Append('\n');
            sb.Append(SorteioDosMoradores.TextoDaIdade(f)).Append(noBunker.Contains(id) ? " · no bunker" : " · lá fora").Append('\n');
            sb.Append('\n');
            sb.Append("ANTES: ").Append(SorteioDosMoradores.NomeDeAntes(f)).Append(". ").Append(SorteioDosMoradores.FraseDeAntes(f)).Append('\n');
            sb.Append("NOS ANOS DO APOCALIPSE: ").Append(SorteioDosMoradores.NomeDeDepois(f)).Append(". ").Append(SorteioDosMoradores.FraseDeDepois(f)).Append('\n');
            sb.Append('\n');
            sb.Append("HABILIDADES").Append('\n');
            for (int h = 0; h < ListasDosMoradores.Habilidades; h++)
            {
                string valor = f.Incapaz((Habilidade)h) ? " —" : $"{f.habilidades[h],2}";
                string paixao = f.paixoes[h] == Paixao.Apaixonado ? " ♦♦" : f.paixoes[h] == Paixao.Interessado ? " ♦" : "";
                sb.Append("  ").Append(ListasDosMoradores.NomesDasHabilidades[h].PadRight(12)).Append(valor).Append(paixao).Append('\n');
            }
            sb.Append('\n');
            sb.Append("TRAÇOS: ").Append(SorteioDosMoradores.TextoDosTracos(f)).Append('\n');
            foreach (var t in f.tracos)
            {
                var traco = ListasDosMoradores.Traco(t);
                if (traco != null && !string.IsNullOrEmpty(traco.efeito)) sb.Append("  · ").Append(traco.Nome(f.mulher)).Append(": ").Append(traco.efeito).Append('\n');
            }
            sb.Append("CONDIÇÕES: ").Append(SorteioDosMoradores.TextoDasCondicoes(f)).Append('\n');
            foreach (var c in f.condicoes)
            {
                var cond = ListasDosMoradores.Condicao(c);
                if (cond != null && !string.IsNullOrEmpty(cond.efeito)) sb.Append("  · ").Append(cond.nome).Append(": ").Append(cond.efeito).Append('\n');
            }
            sb.Append("NÃO FAZ: ").Append(SorteioDosMoradores.TextoDoNaoFaz(f)).Append('\n');
            var lacos = SorteioDosMoradores.TextosDosLacos(f, g);
            sb.Append("LAÇOS: ").Append(lacos.Count == 0 ? "—" : string.Join("; ", lacos)).Append('\n');
            if (noBunker.Contains(id) && doBunker.Count > 1)
            {
                var ops = new List<string>();
                foreach (var o in doBunker)
                {
                    if (o.id == id) continue;
                    int op = SorteioDosMoradores.OpiniaoDoComeco(f, o);
                    string nome = mb.Todos[doBunker.IndexOf(o)].Nome;
                    ops.Add($"{nome} {(op >= 0 ? "+" : "−")}{Mathf.Abs(op)}");
                }
                sb.Append("OPINIÃO: ").Append(string.Join(" · ", ops)).Append('\n');
            }
            return sb.ToString();
        }
    }
}
