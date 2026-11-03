using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Salvar
{
    /// <summary>Um item da mochila guardada (o <see cref="ItemNaMochila"/> sem o Id, que o anfitrião dá de novo).</summary>
    [Serializable]
    public class ItemSalvo
    {
        public int item, x, y, qtd, desgaste;
        public bool girado;
        /// <summary>As cores da roupa (0: as do catálogo).</summary>
        public long cores;
    }

    /// <summary>
    /// O personagem guardado no computador do jogador (02/11/2026, a pedido: "cada um leva o seu personagem"): o nome, a
    /// aparência, o dinheiro, a vida, a fome, a sede, a mochila vestida e o que está nela, os atalhos e o pneu nas mãos.
    /// Vai para qualquer mundo: quem entra manda isso ao anfitrião (<see cref="InventarioJogador"/>, a parte do personagem).
    /// Fica em <c>Application.persistentDataPath/Personagens/&lt;id&gt;.json</c>; a posição dele em cada mundo fica no
    /// mundo (<see cref="MundoSalvo"/>).
    /// </summary>
    [Serializable]
    public class PersonagemSalvo
    {
        public int versao = 1;
        public string id;
        public string nome;
        public int mulher, pele, cabelo, corDosPelos, barba, camiseta, calca;
        public int cabeca, oculos, mascara, tronco, porCima, colete, cintura, pernas, pes, maos;
        public int dinheiro;
        public float vida = 100f, fome = 100f, sede = 100f;
        public int mochila;
        public int pneu = InventarioJogador.SemPneu;
        public List<ItemSalvo> itens = new List<ItemSalvo>();
        /// <summary>Os 3 atalhos: o índice do item em <see cref="itens"/> (-1, vazio).</summary>
        public int[] atalhos = { -1, -1, -1 };
        public int escolhido;
        /// <summary>
        /// As roupas vestidas, uma por lugar (na ordem do <see cref="LugarDaRoupa"/>; 0, vazio) e as cores de cada uma. Num
        /// save de antes delas, vazias: ele entra com as do começo.
        /// </summary>
        public int[] roupas;
        public long[] coresDasRoupas;
        /// <summary>Já entrou num mundo (tem mochila e dinheiro): pula o Dia 0. Falso só no personagem que acabou de ser criado.</summary>
        public bool jogou;
        public string criadoEm, salvoEm;
        public string ultimoMundo;

        /// <summary>A chave dele na rede e nos mundos (um número tirado do id; nunca 0).</summary>
        public ulong Chave => ChaveDe(id);

        public static ulong ChaveDe(string id) => string.IsNullOrEmpty(id) ? 0UL : Sorteio.Fnv(id) | 1UL;

        public static PersonagemSalvo Novo() => new PersonagemSalvo
        {
            id = Guid.NewGuid().ToString("N"),
            criadoEm = DateTime.Now.ToString("s"),
        };

        public AparenciaDoJogador Aparencia() => new AparenciaDoJogador
        {
            nome = new FixedString32Bytes(AparenciaDoJogador.Cortar(nome ?? "")),
            mulher = (byte)mulher, pele = (byte)pele, cabelo = (byte)cabelo, corDosPelos = (byte)corDosPelos, barba = (byte)barba,
            camiseta = (byte)camiseta, calca = (byte)calca, definida = 1,
            cabeca = (byte)cabeca, oculos = (byte)oculos, mascara = (byte)mascara, tronco = (byte)tronco, porCima = (byte)porCima,
            colete = (byte)colete, cintura = (byte)cintura, pernas = (byte)pernas, pes = (byte)pes, maos = (byte)maos,
        };

        public void Vestir(AparenciaDoJogador a)
        {
            nome = a.nome.ToString();
            mulher = a.mulher; pele = a.pele; cabelo = a.cabelo; corDosPelos = a.corDosPelos; barba = a.barba;
            camiseta = a.camiseta; calca = a.calca;
            cabeca = a.cabeca; oculos = a.oculos; mascara = a.mascara; tronco = a.tronco; porCima = a.porCima;
            colete = a.colete; cintura = a.cintura; pernas = a.pernas; pes = a.pes; maos = a.maos;
        }

        /// <summary>O resumo da lista do menu: o dinheiro e o que ele leva.</summary>
        public string Resumo()
        {
            string mochilaNome = mochila != 0 ? Itens.Nome((Item)mochila) : "sem mochila";
            return $"{DiaZero.PrecosDoDiaZero.Dinheiro(dinheiro)} · {mochilaNome} · {itens.Count} itens";
        }

        // ------------------------------------------------------------------ os arquivos

        public static string Pasta => Path.Combine(Application.persistentDataPath, "Personagens");
        string Arquivo => Path.Combine(Pasta, id + ".json");

        /// <summary>Grava no computador (primeiro num arquivo ao lado, depois troca: um jogo que fecha no meio não estraga o anterior).</summary>
        public void Gravar()
        {
            if (string.IsNullOrEmpty(id)) return;
            salvoEm = DateTime.Now.ToString("s");
            try
            {
                Directory.CreateDirectory(Pasta);
                Arquivos.Gravar(Arquivo, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Não deu para gravar o personagem {nome}: {e.Message}");
            }
        }

        /// <summary>Todos os personagens deste computador, os jogados por último primeiro.</summary>
        public static List<PersonagemSalvo> Todos()
        {
            var lista = new List<PersonagemSalvo>();
            if (!Directory.Exists(Pasta)) return lista;
            foreach (var arquivo in Directory.GetFiles(Pasta, "*.json"))
            {
                try
                {
                    var p = JsonUtility.FromJson<PersonagemSalvo>(File.ReadAllText(arquivo));
                    if (p != null && !string.IsNullOrEmpty(p.id) && !string.IsNullOrEmpty(p.nome)) lista.Add(p);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Save] O personagem em {Path.GetFileName(arquivo)} não abriu: {e.Message}");
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
            catch (Exception e) { Debug.LogWarning($"[Save] Não deu para apagar o personagem: {e.Message}"); }
        }
    }

    /// <summary>
    /// O personagem desta máquina para a próxima partida, escolhido no menu antes de criar, continuar ou entrar num mundo.
    /// Um novo é criado na tela de criar (<see cref="TelaDeCriarPersonagem"/>); um que já existe entra direto, com as coisas
    /// dele, e pula o Dia 0.
    /// </summary>
    public static class EscolhaDoPersonagem
    {
        /// <summary>O personagem escolhido (null: nenhum ainda; o menu põe um novo).</summary>
        public static PersonagemSalvo Atual { get; private set; }
        /// <summary>O escolhido ainda não foi criado (a tela de criar abre ao entrar).</summary>
        public static bool Novo => Atual == null || !Atual.jogou;

        public static void Escolher(PersonagemSalvo p) => Atual = p;

        // sem o Domain Reload, o escolhido de um Play ficava para o próximo (e um teste gravaria por cima dele)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Zerar() => Atual = null;

        /// <summary>Um personagem novo (a tela de criar abre ao entrar no mundo).</summary>
        public static void NovoPersonagem() => Atual = PersonagemSalvo.Novo();

        /// <summary>Garante um escolhido (o robô de teste, a linha de comando, o Editor): um novo.</summary>
        public static PersonagemSalvo Garantir()
        {
            if (Atual == null) NovoPersonagem();
            return Atual;
        }
    }

    /// <summary>Gravar um arquivo sem estragar o anterior se o jogo fechar no meio.</summary>
    public static class Arquivos
    {
        public static void Gravar(string caminho, string texto)
        {
            string tmp = caminho + ".tmp";
            File.WriteAllText(tmp, texto);
            if (File.Exists(caminho)) File.Replace(tmp, caminho, null);
            else File.Move(tmp, caminho);
        }
    }
}
