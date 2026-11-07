using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Salvar
{
    /// <summary>
    /// Quando o mundo salva (02/11/2026, a pedido: "as 3 opções são boas e deveriam estar no jogo"; escolhido ao criar o
    /// mundo): sozinho a cada 5 minutos e ao sair, só ao sair da partida, ou só dormindo (na cama, na casa da família).
    /// </summary>
    public enum ModoDeSalvar : byte { Automatico, AoSair, Dormindo }

    /// <summary>
    /// O mundo salvo, no computador de quem hospeda (<c>Application.persistentDataPath/Mundos/&lt;id&gt;.json</c>): o pedido
    /// (a semente, o tamanho, o nome e a versão do gerador), o modo de salvar, o dia e a hora, o clima, o que está no chão,
    /// os carros, as construções, a mobília aberta, os sustos já dados e onde cada personagem estava. O mapa sai da semente
    /// (é gerado de novo); o resto vem daqui (<see cref="Salvamento"/>). As mochilas são de cada jogador
    /// (<see cref="PersonagemSalvo"/>).
    /// </summary>
    [Serializable]
    public class MundoSalvo
    {
        public int versao = 1;
        public string id, nome;
        public int semente, tamanho, versaoDoGerador;
        public int modo;
        /// <summary>O fogo amigo, escolhido ao criar o mundo (03/11/2026).</summary>
        public bool fogoAmigo;
        /// <summary>
        /// A chave do cupê já foi posta no mundo (04/11/2026; marcada quando ela aparece: no mundo novo, com os carros, ou ao
        /// carregar). Falso num save de antes dela: ao carregar, ela aparece uma vez no chão da sala do bunker.
        /// </summary>
        public bool chaveDoCupe;
        /// <summary>
        /// As motos e as chaves delas já foram postas no mundo (06/11/2026). Falso num save de antes delas: ao carregar, elas
        /// aparecem uma vez perto da partida (e as chaves, no chão da sala).
        /// </summary>
        public bool motos;
        /// <summary>O caiaque já foi posto no lago (06/11/2026). Falso num save de antes dele: ao carregar, ele aparece uma vez.</summary>
        public bool caiaque;
        /// <summary>Onde os caiaques ficaram (06/11/2026).</summary>
        public List<CaiaqueSalvo> caiaques = new List<CaiaqueSalvo>();
        public string criadoEm, salvoEm;
        public int dia = 1;
        public float hora = 8f;
        /// <summary>Já foi salvo uma vez (tem o estado). Falso no mundo que acabou de ser criado.</summary>
        public bool temEstado;
        public TempoSalvo tempo = new TempoSalvo();
        public List<ItemNoChaoSalvo> itens = new List<ItemNoChaoSalvo>();
        public List<ConstrucaoSalva> construcoes = new List<ConstrucaoSalva>();
        public List<CarroSalvo> carros = new List<CarroSalvo>();
        public List<CarroDoMundoSalvo> carrosDoMundo = new List<CarroDoMundoSalvo>();
        public List<long> mobiliaAberta = new List<long>();
        /// <summary>As árvores cortadas pelo machado (06/11/2026; a chave da FlorestaDoMundo).</summary>
        public List<long> arvoresCortadas = new List<long>();
        public List<int> sustos = new List<int>();
        public long armasNaParede, pentesNaParede;
        public List<PosicaoSalva> posicoes = new List<PosicaoSalva>();
        /// <summary>Os grupos de sobreviventes de cada cidade (quem morreu continua morto).</summary>
        public List<GrupoSalvo> sobreviventes = new List<GrupoSalvo>();
        /// <summary>As mochilas no chão (dos sobreviventes mortos e dos jogadores que caíram).</summary>
        public List<MochilaSalva> mochilas = new List<MochilaSalva>();
        /// <summary>Os acampamentos: hostil, quem morreu, o estoque dos comerciantes e o dia da última reposição.</summary>
        public List<AcampamentoSalvo> acampamentos = new List<AcampamentoSalvo>();
        /// <summary>Os carros quebrados, na ordem do gerador: o estado de cada cena, quem morreu, o pneu dado e o que falta da troca.</summary>
        public List<CarroQuebradoSalvo> carrosQuebrados = new List<CarroQuebradoSalvo>();
        /// <summary>As bases militares (03/11/2026): quem morreu, a geração dos militares e o dia do reforço.</summary>
        public List<BaseMilitarSalva> basesMilitares = new List<BaseMilitarSalva>();
        /// <summary>As marcas e as anotações do mapa (03/11/2026; os pings não ficam).</summary>
        public List<MarcaSalva> marcas = new List<MarcaSalva>();
        /// <summary>O saque dos móveis já pego (as chaves: o que foi pego não volta; o Plano_Saque.md da pasta de arte).</summary>
        public List<long> saquePego = new List<long>();
        /// <summary>As portas e as janelas das casas mexidas (abertas, fechadas, destrancadas ou arrombadas; o Plano_Portas.md).</summary>
        public List<PortaSalva> portas = new List<PortaSalva>();
        /// <summary>Os itens guardados nas estantes e nas prateleiras do bunker da família.</summary>
        public List<ItemNaEstanteSalvo> estantes = new List<ItemNaEstanteSalvo>();
        /// <summary>
        /// Os moradores do bunker (o Plano_NPCs_Bunker.md da pasta de arte): as fichas, escolhidas nas cartas antes do mundo
        /// (<c>UI/TelaDosMoradores</c>). Vazia num mundo de antes deles: o Continuar abre a tela na primeira vez.
        /// </summary>
        public List<FichaDoMorador> moradores = new List<FichaDoMorador>();
        /// <summary>
        /// Os outros NPCs do mundo (a tela das cartas, 04/11/2026): os 52 sorteados com o mundo, menos os do bunker. Eles vão
        /// estar pelo mapa (as comunidades, as bases, os que esperam resgate; a parte do mapa vem depois). Os laços dos
        /// moradores podem apontar para eles (o irmão que ficou lá fora).
        /// </summary>
        public List<FichaDoMorador> npcsDoMapa = new List<FichaDoMorador>();
        /// <summary>Os ids dos NPCs que os jogadores já conhecem (os do bunker no começo); a lista do computador mostra os outros com "?".</summary>
        public List<int> npcsDescobertos = new List<int>();
        /// <summary>O id do próximo NPC (quem for sorteado depois).</summary>
        public int proximoMorador = 1;

        /// <summary>O NPC do mundo com o id (no bunker ou no mapa), ou null.</summary>
        public FichaDoMorador NpcDoId(int id) => moradores?.Find(f => f.id == id) ?? npcsDoMapa?.Find(f => f.id == id);

        public ModoDeSalvar Modo => (ModoDeSalvar)Mathf.Clamp(modo, 0, 2);
        public PedidoDoMundo Pedido => new PedidoDoMundo
        {
            semente = semente, tamanho = (TamanhoDoMundo)Mathf.Clamp(tamanho, 0, 2), nome = nome, diaZero = false, fogoAmigo = fogoAmigo,
        };

        public static MundoSalvo Novo(PedidoDoMundo pedido, ModoDeSalvar modo) => new MundoSalvo
        {
            id = Guid.NewGuid().ToString("N"),
            nome = string.IsNullOrWhiteSpace(pedido.nome) ? "Mundo novo" : pedido.nome.Trim(),
            semente = pedido.semente,
            tamanho = (int)pedido.tamanho,
            modo = (int)modo,
            fogoAmigo = pedido.fogoAmigo,
            criadoEm = DateTime.Now.ToString("s"),
        };

        public static string NomeDoModo(ModoDeSalvar m) => m switch
        {
            ModoDeSalvar.Automatico => "Automático",
            ModoDeSalvar.AoSair => "Ao sair",
            _ => "Só dormindo",
        };

        /// <summary>A linha da lista do Continuar.</summary>
        public string Resumo()
        {
            string quando = DateTime.TryParse(salvoEm, out var d) ? d.ToString("dd/MM/yyyy HH:mm") : "";
            return $"dia {dia} · {Mathf.FloorToInt(hora):00}:{Mathf.FloorToInt(hora % 1f * 60f):00} · {NomeDoModo(Modo)} · {quando}";
        }

        public PosicaoSalva PosicaoDe(ulong chave)
        {
            long c = unchecked((long)chave);
            foreach (var p in posicoes) if (p.chave == c) return p;
            return null;
        }

        public void LembrarPosicao(ulong chave, Vector3 pos, float giro)
        {
            if (chave == 0) return;
            var p = PosicaoDe(chave);
            if (p == null) posicoes.Add(p = new PosicaoSalva { chave = unchecked((long)chave) });
            p.pos = pos;
            p.giro = giro;
        }

        // ------------------------------------------------------------------ os arquivos

        public static string Pasta => Path.Combine(Application.persistentDataPath, "Mundos");
        string Arquivo => Path.Combine(Pasta, id + ".json");

        public void Gravar()
        {
            if (string.IsNullOrEmpty(id)) return;
            salvoEm = DateTime.Now.ToString("s");
            Directory.CreateDirectory(Pasta);
            Arquivos.Gravar(Arquivo, JsonUtility.ToJson(this));
        }

        public static MundoSalvo Ler(string id)
        {
            var arquivo = Path.Combine(Pasta, id + ".json");
            if (!File.Exists(arquivo)) return null;
            try { return JsonUtility.FromJson<MundoSalvo>(File.ReadAllText(arquivo)); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] O mundo {id} não abriu: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Os mundos salvos deste computador (os jogados por último primeiro), só o cabeçalho de cada um: lê o arquivo todo,
        /// mas guarda só o que a lista mostra.
        /// </summary>
        public static List<MundoSalvo> Todos()
        {
            var lista = new List<MundoSalvo>();
            if (!Directory.Exists(Pasta)) return lista;
            foreach (var arquivo in Directory.GetFiles(Pasta, "*.json"))
            {
                try
                {
                    var m = JsonUtility.FromJson<MundoSalvo>(File.ReadAllText(arquivo));
                    if (m == null || string.IsNullOrEmpty(m.id) || !m.temEstado) continue;
                    lista.Add(new MundoSalvo
                    {
                        id = m.id, nome = m.nome, semente = m.semente, tamanho = m.tamanho, versaoDoGerador = m.versaoDoGerador,
                        modo = m.modo, fogoAmigo = m.fogoAmigo, criadoEm = m.criadoEm, salvoEm = m.salvoEm, dia = m.dia, hora = m.hora, temEstado = true,
                    });
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Save] O mundo em {Path.GetFileName(arquivo)} não abriu: {e.Message}");
                }
            }
            lista.Sort((a, b) => string.CompareOrdinal(b.salvoEm ?? "", a.salvoEm ?? ""));
            return lista;
        }

        public static void Apagar(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var arquivo = Path.Combine(Pasta, id + ".json");
            try { if (File.Exists(arquivo)) File.Delete(arquivo); }
            catch (Exception e) { Debug.LogWarning($"[Save] Não deu para apagar o mundo: {e.Message}"); }
        }
    }

    [Serializable]
    public class TempoSalvo
    {
        /// <summary>As horas do clima desde o começo (o relógio dele) e os eventos que ainda vêm.</summary>
        public double horas;
        public List<EventoSalvo> eventos = new List<EventoSalvo>();
    }

    [Serializable]
    public class EventoSalvo
    {
        public double inicio, fim;
        public int nivel;
        public bool tornado;
    }

    [Serializable]
    public class ItemNoChaoSalvo
    {
        public int item;
        public Vector3 apoio;
        public float giro;
        public int qtd, desgaste;
        /// <summary>As cores da roupa (0: as do catálogo).</summary>
        public long cores;
    }

    [Serializable]
    public class ConstrucaoSalva
    {
        public int tipo;
        public Vector3 pos;
        public float giro;
        /// <summary>A barricada: a vida (%). A fogueira: os segundos de fogo que faltavam.</summary>
        public float valor;
        /// <summary>A chave do personagem que montou (a cama dele).</summary>
        public long dono;
    }

    /// <summary>Um caiaque do mundo (06/11/2026): onde ficou.</summary>
    [Serializable]
    public class CaiaqueSalvo
    {
        public Vector3 pos;
        public Quaternion giro;
    }

    [Serializable]
    public class CarroSalvo
    {
        /// <summary>0, a perua (o carro da família); 1, o cupê; 2 em diante, as motos (MundoDaPartida.motos: 2 a Fat Boy, 3 a cross).</summary>
        public int qual;
        public Vector3 pos;
        public Quaternion giro;
        public int vela, bateria, radiador;
        public int[] pneus = new int[4];
        public bool macaco;
        public int estepe;
        public float litros, carga, temperatura, carencia;
        public float[] resistenciaDoPneu = new float[4], gastoDoPneu = new float[4];
        public float resistenciaDaVela, resistenciaDoRadiador, gastoDaVela, gastoDoRadiador;
    }

    [Serializable]
    public class CarroDoMundoSalvo
    {
        public int id;
        public int vela, bateria, radiador;
        public int[] pneus = new int[4];
        public int macaco;
    }

    [Serializable]
    public class PosicaoSalva
    {
        public long chave;
        public Vector3 pos;
        public float giro;
    }

    /// <summary>Um grupo de busca: a cidade, a parte dela (a cidade grande tem 6; um save antigo, 0), a geração e quem vive.</summary>
    [Serializable]
    public class GrupoSalvo
    {
        public int cidade, parte, geracao, vivos, quantos, diaDaMorte;
    }

    /// <summary>
    /// A cena de um carro quebrado (a etapa 4): o estado (0 esperando, 1 trocando, 2 ajudados, 3 hostis, 4 mortos), quem
    /// morreu (bit 0 o mecânico, bit 1 o vigia), o pneu dado (%) e os segundos que faltavam da troca.
    /// </summary>
    [Serializable]
    public class CarroQuebradoSalvo
    {
        public int estado, mortos, pneu, falta;
    }

    /// <summary>Uma marca do mapa: o tipo (1 marca, 2 anotação), o ponto no mundo, a cor, o texto e quem marcou.</summary>
    [Serializable]
    public class MarcaSalva
    {
        public int tipo, cor;
        public Vector3 pos;
        public string texto, autor;
    }

    /// <summary>Uma base militar: o índice dela, um bit por militar morto (na ordem dos postos), a geração e o dia do reforço.</summary>
    [Serializable]
    public class BaseMilitarSalva
    {
        public int indice, mortos, geracao;
        public int diaDoReforco = -1;
    }

    [Serializable]
    public class AcampamentoSalvo
    {
        public int indice;
        public bool hostil;
        /// <summary>Um bit por pessoa morta (na ordem dos lugares: os guardas, os comerciantes, os moradores).</summary>
        public int mortos;
        public int diaDaReposicao = -1;
        /// <summary>O estoque, um número por produto: a banca (bits 16+), o produto (8 a 15) e a quantidade (0 a 7).</summary>
        public List<int> estoque = new List<int>();
    }

    /// <summary>Uma porta ou uma janela mexida: a chave dela e os bits (1 aberta, 2 destrancada).</summary>
    [Serializable]
    public class PortaSalva
    {
        public long chave;
        public int bits;
    }

    /// <summary>Um item numa estante do bunker: a estante (o índice do móvel no MoveisDoBunker), a célula e o que vai com ele.</summary>
    [Serializable]
    public class ItemNaEstanteSalvo
    {
        public int estante, item, x, y, qtd, desgaste;
        public bool girado;
        public long cores;
    }

    [Serializable]
    public class MochilaSalva
    {
        public string nome;
        public Vector3 pos;
        public float giro;
        public int mochila, dinheiro, colunas, linhas;
        public List<ItemSalvo> itens = new List<ItemSalvo>();
    }
}
