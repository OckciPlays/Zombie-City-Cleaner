using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Um item guardado numa estante do bunker: a estante (o índice do móvel em <see cref="MoveisDoBunker.Todos"/>), o Id
    /// (único entre as estantes), o que é, a célula de cima, à esquerda, se está girado e o que vai com ele (as balas, o
    /// desgaste e as cores da roupa), como na mochila.
    /// </summary>
    public struct ItemNaEstante : INetworkSerializeByMemcpy, IEquatable<ItemNaEstante>
    {
        public ushort estante, id;
        public Item item;
        public byte x, y;
        public bool girado;
        public ushort qtd;
        public byte desgaste;
        public ulong cores;

        public Vector2Int Tamanho => EstantesDoBunker.TamanhoNaEstante(item, girado);

        /// <summary>Como um item da mochila (o desenho da tela; o Id fica 0, para não confundir com os da mochila).</summary>
        public ItemNaMochila ComoDaMochila => new ItemNaMochila { item = item, x = x, y = y, girado = girado, qtd = qtd, desgaste = desgaste, cores = cores };

        public bool Equals(ItemNaEstante o) => estante == o.estante && id == o.id && item == o.item && x == o.x && y == o.y && girado == o.girado
                                              && qtd == o.qtd && desgaste == o.desgaste && cores == o.cores;
    }

    /// <summary>
    /// As estantes de aço e as prateleiras de madeira do bunker da família guardam itens (01/11/2026, a pedido: "ao apertar
    /// E nelas, vai abrir um sistema de colocar itens dentro, mostrando os slots que tem nela e o inventário do jogador,
    /// para eu só arrastar os itens, e aí dependendo da quantidade de itens colocados vai aparecendo na estante ou
    /// prateleira os itens até preencher"). No "Mundo" da cena (com o NetworkObject).
    /// - **A grade de cada uma** (<see cref="Forma"/>): cada tábua é uma faixa de 2 linhas (a de trás e a da frente) com as
    ///   colunas pela largura dela (uns 17 cm por célula, como a mochila); as tábuas vêm de cima para baixo. Um item fica
    ///   numa tábua só (até 2 linhas), com as células dele (<see cref="Itens.Tamanho"/>), deitado ou girado.
    /// - **A rede** (o anfitrião manda): a lista de todos os guardados (<see cref="ItemNaEstante"/>); guardar, pegar e mover
    ///   passam por ele, que confere o jogador perto da estante (<see cref="distanciaMaxima"/>) e o lugar. Por enquanto não é
    ///   salvo no disco (o mundo salvo, a M10).
    /// - **Na estante:** cada item guardado aparece na tábua dele, no lugar das células (<see cref="ItensNaEstante"/>), com a
    ///   malha e o material do chão (<see cref="ItensNoMundo.MalhaDe"/>).
    /// - **A tela** é a do inventário (TAB) com a estante no lugar dos detalhes (<c>TelaDoInventario.Estante.cs</c>): E na
    ///   estante abre (<see cref="EstanteNaMira"/>).
    /// - **O compartimento do caiaque** (07/11/2026, a pedido: "um compartimento pequeno no caiaque") é uma estante a mais,
    ///   de 2 × 2, com o número <see cref="BaseDosCaiaques"/> + o do caiaque: a mesma lista, a mesma tela e o mesmo save; o
    ///   "perto" é o da escotilha de trás dele (<see cref="Vehicles.Caiaque.PontoDoCompartimento"/>). Nada aparece por fora.
    /// </summary>
    public class EstantesDoBunker : NetworkBehaviour
    {
        [Tooltip("Até onde o anfitrião aceita os pedidos (m, dos pés do jogador ao pé da estante, no chão).")]
        public float distanciaMaxima = 3.5f;

        public static EstantesDoBunker Instancia { get; private set; }

        /// <summary>As linhas de cada tábua (a de trás e a da frente).</summary>
        public const int LinhasPorTabua = 2;

        /// <summary>
        /// A forma de uma estante (no tamanho real do modelo, sem a escala dos móveis): o nome, as colunas de cada tábua, a
        /// altura do tampo de cada tábua (de baixo para cima), a largura e o fundo em que os itens ficam, e o vão entre
        /// as tábuas (para os altos não passarem da de cima). As medidas são as do mobilia_servico.py da pasta de arte.
        /// </summary>
        public sealed class Forma
        {
            public string nome;
            /// <summary>O artigo do nome ("a estante de aço", "o suporte dos tacos").</summary>
            public string artigo = "a";
            public int colunas;
            public float[] alturas;
            public float largura, fundo, vao;
            /// <summary>
            /// As linhas de cada tábua (25/11/2026): 2 nas estantes; a caçamba da caminhonete é uma grade só, de 8 linhas (o
            /// pneu, de 3 × 3, cabe nela).
            /// </summary>
            public int linhas = LinhasPorTabua;
            /// <summary>
            /// O suporte dos tacos e a mesinha do fumo (a parte 8 do Plano_NPCs_Bunker.md, a segunda fatia) não têm tábuas: têm
            /// vagas, uma por item (as colunas), cada uma com o que aceita e o lugar dela no móvel (<see cref="vagas"/>).
            /// </summary>
            public EspecialDaEstante especial;
            /// <summary>Nas especiais: onde cada vaga fica no móvel (no tamanho real, sem a escala dos móveis): o meio no chão
            /// dela (x e z) e a altura em que o item assenta (y).</summary>
            public Vector3[] vagas;
            public int Tabuas => alturas.Length;
            public Vector2Int Grade => new Vector2Int(colunas, Tabuas * linhas);
            public bool Especial => especial != EspecialDaEstante.Nenhuma;

            /// <summary>Nas especiais: a vaga aceita o item (os tacos nas 4 do suporte e a caixa de bolas na quinta; os maços na mesinha).</summary>
            public bool Aceita(int vaga, Item item) => especial switch
            {
                EspecialDaEstante.Suporte => vaga < 4 ? Itens.EhTaco(item) : vaga == 4 && Itens.EhCaixaDeBolas(item),
                EspecialDaEstante.Mesinha => vaga >= 0 && vaga < colunas && Itens.EhFumo(item),
                _ => true,
            };
        }

        static readonly float[] TabuasDeAco = { 0.08f + 0.0015f, 0.50f + 0.0015f, 0.92f + 0.0015f, 1.34f + 0.0015f, 1.82f + 0.0015f };
        static readonly float[] TabuasDeMadeira = { 0.10f + 0.009f, 0.62f + 0.009f, 1.14f + 0.009f, 1.66f + 0.009f };

        static readonly Dictionary<string, Forma> Formas = new Dictionary<string, Forma>
        {
            ["EstanteAco90"] = new Forma { nome = "estante de aço", colunas = 6, alturas = TabuasDeAco, largura = 0.82f, fundo = 0.37f, vao = 0.42f },
            ["EstanteAco120"] = new Forma { nome = "estante de aço", colunas = 8, alturas = TabuasDeAco, largura = 1.12f, fundo = 0.37f, vao = 0.42f },
            ["PrateleiraMadeira90"] = new Forma { nome = "prateleira de madeira", colunas = 6, alturas = TabuasDeMadeira, largura = 0.79f, fundo = 0.37f, vao = 0.52f },
            ["PrateleiraMadeira150"] = new Forma { nome = "prateleira de madeira", colunas = 10, alturas = TabuasDeMadeira, largura = 1.39f, fundo = 0.37f, vao = 0.52f },
            // a bancada de inox (06/11/2026, a parte 6 do Plano_NPCs_Bunker.md: a do refeitório é a bancada da água): o tampo,
            // a 0,90 m (o bancada_inox do mobilia_bunker.py)
            ["BancadaInox"] = new Forma { nome = "bancada de inox", colunas = 10, alturas = new[] { 0.90f + 0.002f }, largura = 1.70f, fundo = 0.60f, vao = 0.6f },
            // o suporte dos tacos (a parte 8 do Plano_NPCs_Bunker.md, no tamanho final): as 4 vagas dos tacos em pé (o pé de
            // cada um no copo) e a prateleirinha da caixa de bolas, a quinta. As medidas do mobilia_moradores.py da pasta de
            // arte (VAGAS_DOS_TACOS, TACO_Y, TACO_Z e CAIXA_NO_SUPORTE: o x do Blender é o -x daqui, e o -y, o z)
            ["SuporteDeTacos"] = new Forma
            {
                nome = "suporte dos tacos", artigo = "o", especial = EspecialDaEstante.Suporte, colunas = 5, alturas = new[] { 0.045f },
                largura = 0.64f, fundo = 0.20f, vao = 1.6f,
                vagas = new[]
                {
                    new Vector3(0.195f, 0.045f, 0.02f), new Vector3(0.065f, 0.045f, 0.02f), new Vector3(-0.065f, 0.045f, 0.02f),
                    new Vector3(-0.195f, 0.045f, 0.02f), new Vector3(0f, 1.555f, 0.02f),
                },
            },
            // a mesinha do fumo (a mesa de canto do canto de leitura da sala de estar): até 4 maços no tampo, em volta do
            // cinzeiro (no canto de +x e +z: o MoveisDoBunker.DoLazer)
            ["MesaDeCanto"] = new Forma
            {
                nome = "mesinha do fumo", especial = EspecialDaEstante.Mesinha, colunas = 4, alturas = new[] { 0.58f },
                largura = 0.5f, fundo = 0.5f, vao = 0.4f,
                vagas = new[]
                {
                    new Vector3(-0.15f, 0.58f, -0.12f), new Vector3(-0.15f, 0.58f, 0.03f), new Vector3(-0.02f, 0.58f, -0.13f),
                    new Vector3(0.11f, 0.58f, -0.12f),
                },
            },
        };

        /// <summary>O número da estante do compartimento do primeiro caiaque (os outros, os seguintes).</summary>
        public const int BaseDosCaiaques = 60000;
        const string Compartimento = "CompartimentoDoCaiaque";
        static readonly Forma FormaDoCompartimento = new Forma { nome = "compartimento do caiaque", colunas = 2, alturas = new[] { 0f }, largura = 0.36f, fundo = 0.36f, vao = 0.3f };

        /// <summary>É o compartimento de um caiaque (e não um móvel do bunker)?</summary>
        public static bool EhDoCaiaque(int estante) => estante >= BaseDosCaiaques && estante < BaseDosCaiaques + 16;

        /// <summary>
        /// A caçamba da caminhonete (22/11/2026, o Dia 0 na Woodcreek: o Pedro, "Caçamba da caminhonete": as compras vão para
        /// ela), com o número <see cref="BaseDasCacambas"/> (uma caminhonete só). O "perto" é o da tampa de trás
        /// (<see cref="Vehicles.Carro.PontoDoPortaMalas"/>); quem está na caçamba também alcança. Desde 25/11/2026 (a pedido:
        /// "começa a aparecer alguns itens em cima da caçamba que fica flutuando... aumentar um pouco o tamanho do inventário
        /// dela") é uma grade só de 12 × 8 (antes, 12 × 6 em três faixas de 2 linhas, e o pneu de 3 × 3 nunca cabia), e o que
        /// está nela aparece deitado no chão da caçamba, andando com ela (<c>Carro.PorOsItensDaCacamba</c>).
        /// </summary>
        public const int BaseDasCacambas = 61000;
        const string Cacamba = "CacambaDaCaminhonete";
        static readonly Forma FormaDaCacamba = new Forma { nome = "caçamba da caminhonete", colunas = 12, linhas = 8, alturas = new[] { 0f }, largura = 1.5f, fundo = 1.6f, vao = 0.7f };

        /// <summary>
        /// O armazém da casa da família (25/11/2026, a pedido: "Voltar para casa... Guardar itens em armazém"): duas estantes de
        /// aço no fundo da garagem dela, na Woodcreek (LakewoodNaCena.Armazem.cs), as de número <see cref="BaseDosArmazens"/> e a
        /// seguinte, de 8 × 10 cada (16 × 10 ao todo). As mesmas nas duas épocas: o que fica nelas no Dia 0 espera os 5 anos.
        /// </summary>
        public const int BaseDosArmazens = 62000, EstantesDoArmazem = 2;
        const string Armazem = "EstanteAco120";
        // a de aço de 120 do bunker, com o nome do armazém (o cabeçalho da tela)
        static readonly Forma FormaDoArmazem = new Forma { nome = "armazém da garagem", artigo = "o", colunas = 8, alturas = TabuasDeAco, largura = 1.12f, fundo = 0.37f, vao = 0.42f };

        /// <summary>É uma estante do armazém da casa da família?</summary>
        public static bool EhDoArmazem(int estante) => estante >= BaseDosArmazens && estante < BaseDosArmazens + EstantesDoArmazem;

        /// <summary>As estantes do armazém, na ordem em que se enchem.</summary>
        public static readonly int[] OArmazem = { BaseDosArmazens, BaseDosArmazens + 1 };

        /// <summary>Onde fica cada estante do armazém (o pé dela, no mundo: em cada máquina, quando ela é posta).</summary>
        public static readonly Dictionary<int, Transform> LugarDoArmazem = new Dictionary<int, Transform>();

        /// <summary>
        /// As células que o item ocupa na estante: as da mochila e, para as mochilas compradas no Dia 0 (que vão para a caçamba e
        /// para o armazém, vazias; 25/11/2026), deitadas: a pequena 2 × 2, a média 3 × 2 e a grande 4 × 2 (2 linhas, para
        /// caberem numa tábua das estantes; o 1º teste, com 3 linhas, deixou a mochila presa na caçamba).
        /// </summary>
        public static Vector2Int TamanhoNaEstante(Item item, bool girado)
        {
            if (!Itens.EhMochila(item)) return InventarioJogador.TamanhoNaGrade(item, girado);
            var t = item == Item.MochilaPequena ? new Vector2Int(2, 2) : item == Item.MochilaMedia ? new Vector2Int(3, 2) : new Vector2Int(4, 2);
            return girado ? new Vector2Int(t.y, t.x) : t;
        }

        /// <summary>
        /// Só no anfitrião: nada do que está na estante <paramref name="de"/> cabe mais nas <paramref name="para"/> (o armazém
        /// cheio: a submissão "Guarde as compras" acaba assim mesmo, para as chaves não travarem).
        /// </summary>
        public bool NadaMaisCabe(int de, IList<int> para)
        {
            foreach (var o in _itens)
                if (o.estante == de && CabemNasEstantes(para, new[] { (o.item, (int)o.qtd) })) return false;
            return true;
        }

        /// <summary>A estante aceita mochilas (a caçamba e o armazém).</summary>
        public static bool AceitaMochila(int estante) => EhDaCacamba(estante) || EhDoArmazem(estante);

        /// <summary>É a caçamba de uma caminhonete?</summary>
        public static bool EhDaCacamba(int estante) => estante >= BaseDasCacambas && estante < BaseDasCacambas + 16;

        /// <summary>
        /// Durante a fuga, o armazém da casa da família e a caçamba da caminhonete dela não abrem (25/11/2026, a revisão: a fuga
        /// é "só com as roupas"; antes, dava para voltar à garagem, abrir o armazém e pegar tudo de volta). Em todas as
        /// máquinas: a tela não abre e a dica diz por quê (<see cref="MotivoNaFuga"/>), e o anfitrião recusa guardar, pegar,
        /// mover e descarregar (<see cref="Quem"/>). O anfitrião, ele mesmo, continua guardando nelas (a fuga começa guardando
        /// o que cada um leva, LakewoodNaCena.Armazem.cs).
        /// </summary>
        public static bool FechadaNaFuga(int estante)
        {
            var m = MundoDeLakewood.Instancia;
            return m != null && m.NaFuga && (EhDoArmazem(estante) || estante == LakewoodNaCena.CacambaDaFamilia);
        }

        /// <summary>O aviso de quem tenta abrir o armazém ou a caçamba durante a fuga.</summary>
        public static string MotivoNaFuga(int estante) =>
            EhDaCacamba(estante) ? "Não dá tempo: a caçamba fica para trás." : "Não dá tempo: o armazém fica para trás.";

        public static bool EhEstante(string modelo) => modelo != null && Formas.ContainsKey(modelo);
        public static Forma FormaDe(string modelo) => modelo != null && Formas.TryGetValue(modelo, out var f) ? f : null;

        /// <summary>O modelo da estante (o móvel do bunker; null se o índice não é de uma estante).</summary>
        public static string ModeloDa(int estante)
        {
            if (EhDoCaiaque(estante)) return Compartimento;   // (mesmo antes de o caiaque nascer: o save dele carrega)
            if (EhDaCacamba(estante)) return Cacamba;
            if (EhDoArmazem(estante)) return Armazem;
            var todos = MoveisDoBunker.Todos;
            return estante >= 0 && estante < todos.Length && EhEstante(todos[estante].modelo) ? todos[estante].modelo : null;
        }

        public static Forma FormaDa(int estante) => EhDoCaiaque(estante) ? FormaDoCompartimento : EhDaCacamba(estante) ? FormaDaCacamba
            : EhDoArmazem(estante) ? FormaDoArmazem : FormaDe(ModeloDa(estante));

        NetworkList<ItemNaEstante> _itens;
        ushort _proximoId = 1;

        /// <summary>Todos os itens guardados (escrita pelo anfitrião).</summary>
        public NetworkList<ItemNaEstante> Guardados => _itens;
        /// <summary>Os guardados mudaram (a estante, ou -1: todas).</summary>
        public event Action<int> Mudou;

        void Awake() => _itens = new NetworkList<ItemNaEstante>();

        void OnEnable() => Instancia = this;

        void OnDisable()
        {
            if (Instancia == this) Instancia = null;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _itens.Clear();
            _itens.OnListChanged += AoMudar;
            Mudou?.Invoke(-1);
        }

        public override void OnNetworkDespawn()
        {
            _itens.OnListChanged -= AoMudar;
            Mudou?.Invoke(-1);
        }

        void AoMudar(NetworkListEvent<ItemNaEstante> e)
        {
            // a remoção não diz de qual estante era: todas conferem
            if (e.Type == NetworkListEvent<ItemNaEstante>.EventType.Add || e.Type == NetworkListEvent<ItemNaEstante>.EventType.Value)
            {
                Mudou?.Invoke(e.Value.estante);
                if (e.Type == NetworkListEvent<ItemNaEstante>.EventType.Value && e.PreviousValue.estante != e.Value.estante) Mudou?.Invoke(e.PreviousValue.estante);
            }
            else Mudou?.Invoke(-1);
        }

        /// <summary>Só no anfitrião: os itens guardados (o save, Salvar/Salvamento).</summary>
        public List<ItemNaEstante> Copia()
        {
            var lista = new List<ItemNaEstante>(_itens.Count);
            foreach (var it in _itens) lista.Add(it);
            return lista;
        }

        /// <summary>Só no anfitrião: os itens guardados do mundo salvo no lugar dos de agora (os Ids são dados de novo).</summary>
        public void Restaurar(List<ItemNaEstante> itens)
        {
            if (!IsServer || !IsSpawned) return;
            _itens.Clear();
            _proximoId = 1;
            foreach (var it in itens)
            {
                if (it.item == Item.Nada || !Itens.NoJogo(it.item) || FormaDa(it.estante) == null) continue;
                var novo = it;
                novo.id = NovoId();
                _itens.Add(novo);
            }
        }

        // ------------------------------------------------------------------ a grade

        /// <summary>Quantos itens a estante tem.</summary>
        public int Quantos(int estante)
        {
            int n = 0;
            if (IsSpawned) foreach (var it in _itens) if (it.estante == estante) n++;
            return n;
        }

        public bool Achar(int estante, ushort id, out ItemNaEstante item)
        {
            int k = Indice(estante, id);
            item = k >= 0 ? _itens[k] : default;
            return k >= 0;
        }

        int Indice(int estante, ushort id)
        {
            for (int i = 0; i < _itens.Count; i++)
                if (_itens[i].id == id && _itens[i].estante == estante) return i;
            return -1;
        }

        /// <summary>
        /// O item cabe na estante com o canto de cima, à esquerda, nesta célula (numa tábua só), sem contar o item
        /// <paramref name="ignorar"/> (o que se move).
        /// </summary>
        public bool Cabe(int estante, Item item, bool girado, int x, int y, ushort ignorar = 0)
        {
            var f = FormaDa(estante);
            // nas especiais (o suporte e a mesinha), um item por vaga, do que ela aceita
            if (f != null && f.Especial)
            {
                if (x < 0 || x >= f.colunas || y != 0 || !f.Aceita(x, item)) return false;
                foreach (var o in _itens) if (o.estante == estante && o.id != ignorar && o.x == x) return false;
                return true;
            }
            if (f == null || (Itens.EhMochila(item) && !AceitaMochila(estante)) || !CabeNaGrade(f, item, girado, x, y)) return false;
            foreach (var o in _itens) if (o.estante == estante && o.id != ignorar && !CabeNoLugar(item, girado, x, y, o)) return false;
            return true;
        }

        // (25/11/2026) as contas do Cabe, para a lista de verdade e para a de mentira do caixa do Dia 0 (CabemNaEstante)
        static bool CabeNaGrade(Forma f, Item item, bool girado, int x, int y)
        {
            var t = TamanhoNaEstante(item, girado);
            if (t.x <= 0 || x < 0 || y < 0 || x + t.x > f.colunas || y + t.y > f.Grade.y) return false;
            return y % f.linhas + t.y <= f.linhas;
        }

        static bool CabeNoLugar(Item item, bool girado, int x, int y, ItemNaEstante o)
        {
            var t = TamanhoNaEstante(item, girado);
            var s = o.Tamanho;
            return !(x < o.x + s.x && o.x < x + t.x && y < o.y + s.y && o.y < y + t.y);
        }

        /// <summary>
        /// O primeiro lugar em que o item cabe, deitado (senão, girado): as tábuas pela altura, da mais perto do peito
        /// (<see cref="AlturaBoa"/>, com a escala dos móveis) para as de longe; em cada uma, da linha de trás para a da frente e
        /// da esquerda para a direita. <paramref name="ignorar"/>: um item que conta como já fora (25/11/2026, a revisão: a
        /// mochila que sai, na troca pela vestida).
        /// </summary>
        public bool Espaco(int estante, Item item, out int x, out int y, out bool girado, ushort ignorar = 0)
        {
            var f = FormaDa(estante);
            var t = TamanhoNaEstante(item, false);
            girado = false;
            if (f != null && f.Especial)
            {
                for (x = 0, y = 0; x < f.colunas; x++)
                    if (Cabe(estante, item, false, x, 0, ignorar)) return true;
                x = y = -1;
                return false;
            }
            if (f != null)
            {
                var tabuas = new List<int>();
                for (int k = 0; k < f.Tabuas; k++) tabuas.Add(k);
                tabuas.Sort((a, b) => Mathf.Abs(f.alturas[a] * MedidasDaMobilia.Escala - AlturaBoa).CompareTo(Mathf.Abs(f.alturas[b] * MedidasDaMobilia.Escala - AlturaBoa)));
                for (int g = 0; g < (t.x != t.y ? 2 : 1); g++)
                {
                    girado = g == 1;
                    foreach (int tabua in tabuas)
                    {
                        int faixa = f.Tabuas - 1 - tabua;
                        for (int linha = 0; linha < f.linhas; linha++)
                            for (x = 0; x < f.colunas; x++)
                            {
                                y = faixa * f.linhas + linha;
                                if (Cabe(estante, item, girado, x, y, ignorar)) return true;
                            }
                    }
                }
            }
            x = y = -1;
            girado = false;
            return false;
        }

        /// <summary>A altura (m do chão) da tábua que se enche primeiro: a do peito.</summary>
        const float AlturaBoa = 1.1f;

        /// <summary>
        /// As estantes dos depósitos do bunker, na ordem em que se enchem: as do depósito do B1 e, de sobra, as do B3 (os
        /// índices em <see cref="MoveisDoBunker.Todos"/>).
        /// </summary>
        public static List<int> EstantesDosDepositos()
        {
            var lista = new List<int>();
            var todos = MoveisDoBunker.Todos;
            foreach (int andar in new[] { 1, 3 })
            {
                var planta = PlantaDeBunker.DoAndar(andar);
                for (int i = 0; i < todos.Length; i++)
                {
                    var m = todos[i];
                    if (m.andar != andar || !EhEstante(m.modelo)) continue;
                    var k = planta.Em(m.lugar.x, m.lugar.z);
                    if (k != null && k.tipo == "deposito") lista.Add(i);
                }
            }
            return lista;
        }

        /// <summary>
        /// Só no anfitrião, sem passar por um jogador (as compras do Dia 0): guarda o item na primeira das estantes em que
        /// couber (as pilhas completam antes as do mesmo item que já estão nelas). <paramref name="qtd"/>: as balas, o pente
        /// ou quantos há na pilha (-1, o de sempre). Devolve false se ele (ou o resto da pilha) não coube.
        /// </summary>
        public bool GuardarNasEstantes(IList<int> estantes, Item item, int qtd, int desgaste, ulong cores) =>
            GuardarNasEstantes(estantes, item, qtd, desgaste, cores, out _);

        /// <summary>
        /// O mesmo, dizendo o que não coube (25/11/2026, a revisão: a pilha pode entrar só em parte, completando as que já
        /// estão nas estantes; quem guarda o que sobrou precisa do resto certo, e não da pilha inteira):
        /// <paramref name="sobrou"/> é o resto da pilha (no qtd dela: quantos, ou as balas), ou o qtd do item inteiro, ou 0
        /// (coube tudo).
        /// </summary>
        public bool GuardarNasEstantes(IList<int> estantes, Item item, int qtd, int desgaste, ulong cores, out int sobrou)
        {
            sobrou = qtd;
            if (!IsServer || item == Item.Nada || !Itens.NoJogo(item)) return false;
            // (25/11/2026) a mochila comprada vai vazia para a caçamba (e para o armazém); nas outras estantes, não
            if (Itens.EhEquipamento(item))
            {
                bool aceita = false;
                foreach (int e in estantes) aceita |= AceitaMochila(e);
                if (!aceita) return false;
            }
            if (qtd < 0) qtd = Itens.QuantidadePadrao(item);
            sobrou = qtd;
            if (Itens.EmPilha(item))
            {
                int max = Itens.Pilha(item), resta = Mathf.Max(1, Itens.Quantos(item, qtd));
                for (int i = 0; i < _itens.Count && resta > 0; i++)
                {
                    var o = _itens[i];
                    int tem = Itens.Quantos(o.item, o.qtd);
                    if (o.item != item || tem >= max || !estantes.Contains(o.estante)) continue;
                    int n = Mathf.Min(resta, max - tem);
                    o.qtd = (ushort)(tem + n);
                    _itens[i] = o;
                    resta -= n;
                }
                while (resta > 0)
                {
                    int n = Mathf.Min(resta, max);
                    if (!PorNaPrimeira(estantes, item, n, desgaste, cores))
                    {
                        sobrou = resta;
                        return false;
                    }
                    resta -= n;
                }
                sobrou = 0;
                return true;
            }
            if (!PorNaPrimeira(estantes, item, qtd, desgaste, cores)) return false;
            sobrou = 0;
            return true;
        }

        bool PorNaPrimeira(IList<int> estantes, Item item, int qtd, int desgaste, ulong cores)
        {
            foreach (int estante in estantes)
            {
                if (!Espaco(estante, item, out int x, out int y, out bool girado)) continue;
                _itens.Add(new ItemNaEstante { estante = (ushort)estante, id = NovoId(), item = item, x = (byte)x, y = (byte)y, girado = girado,
                                               qtd = (ushort)Mathf.Clamp(qtd, 0, ushort.MaxValue), desgaste = (byte)Mathf.Clamp(desgaste, 0, 100), cores = cores });
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ o caixa do Dia 0 e o armazém (25/11/2026)

        /// <summary>
        /// Só no anfitrião: todos os itens (cada um com o de sempre: a pilha, as balas) cabem nas estantes, guardados como o
        /// <see cref="GuardarNasEstantes"/> guardaria (as pilhas completam as do mesmo item; o resto, no primeiro lugar livre)?
        /// Conta numa cópia, sem mexer na lista da rede: o caixa do Dia 0 só vende o que cabe na caçamba (a pedido: "Quando
        /// compramos item demais e lotamos o seu inventário, ela simplesmente começa a aparecer alguns itens em cima da caçamba
        /// que fica flutuando").
        /// </summary>
        public bool CabemNasEstantes(IList<int> estantes, IList<(Item item, int qtd)> itens)
        {
            var listas = new Dictionary<int, List<ItemNaEstante>>();
            foreach (int e in estantes)
            {
                if (FormaDa(e) == null || listas.ContainsKey(e)) continue;
                var l = new List<ItemNaEstante>();
                foreach (var o in _itens) if (o.estante == e) l.Add(o);
                listas[e] = l;
            }
            foreach (var (item, q) in itens)
            {
                if (item == Item.Nada || !Itens.NoJogo(item)) continue;
                int qtd = q < 0 ? Itens.QuantidadePadrao(item) : q;
                if (Itens.EmPilha(item))
                {
                    int max = Itens.Pilha(item), resta = Mathf.Max(1, Itens.Quantos(item, qtd));
                    foreach (var l in listas.Values)
                        for (int i = 0; i < l.Count && resta > 0; i++)
                        {
                            var o = l[i];
                            int tem = Itens.Quantos(o.item, o.qtd);
                            if (o.item != item || tem >= max) continue;
                            int n = Mathf.Min(resta, max - tem);
                            o.qtd = (ushort)(tem + n);
                            l[i] = o;
                            resta -= n;
                        }
                    while (resta > 0)
                    {
                        int n = Mathf.Min(resta, max);
                        if (!PorNaCopia(listas, estantes, item, n)) return false;
                        resta -= n;
                    }
                }
                else if (!PorNaCopia(listas, estantes, item, qtd)) return false;
            }
            return true;
        }

        // na cópia: no primeiro lugar livre (a mesma ordem do Espaco: deitado, depois girado; as tábuas da mais perto do peito)
        bool PorNaCopia(Dictionary<int, List<ItemNaEstante>> listas, IList<int> estantes, Item item, int qtd)
        {
            foreach (int e in estantes)
            {
                if (!listas.TryGetValue(e, out var l)) continue;
                var f = FormaDa(e);
                if (f.Especial || (Itens.EhMochila(item) && !AceitaMochila(e))) continue;
                var t = TamanhoNaEstante(item, false);
                var tabuas = new List<int>();
                for (int k = 0; k < f.Tabuas; k++) tabuas.Add(k);
                tabuas.Sort((a, b) => Mathf.Abs(f.alturas[a] * MedidasDaMobilia.Escala - AlturaBoa).CompareTo(Mathf.Abs(f.alturas[b] * MedidasDaMobilia.Escala - AlturaBoa)));
                for (int g = 0; g < (t.x != t.y ? 2 : 1); g++)
                    foreach (int tabua in tabuas)
                        for (int linha = 0; linha < f.linhas; linha++)
                            for (int x = 0; x < f.colunas; x++)
                            {
                                int y = (f.Tabuas - 1 - tabua) * f.linhas + linha;
                                bool girado = g == 1;
                                if (!CabeNaGrade(f, item, girado, x, y)) continue;
                                bool livre = true;
                                foreach (var o in l)
                                    if (!CabeNoLugar(item, girado, x, y, o)) { livre = false; break; }
                                if (!livre) continue;
                                l.Add(new ItemNaEstante { estante = (ushort)e, item = item, x = (byte)x, y = (byte)y, girado = girado, qtd = (ushort)Mathf.Clamp(qtd, 0, ushort.MaxValue) });
                                return true;
                            }
            }
            return false;
        }

        /// <summary>
        /// Só no anfitrião: passa o que couber da estante <paramref name="de"/> para as <paramref name="para"/> (a caçamba para o
        /// armazém; as coisas de quem foge para o armazém), um item de cada vez, inteiro (o que não cabe fica onde estava).
        /// </summary>
        public void Passar(int de, IList<int> para, out int foram, out int ficaram)
        {
            foram = ficaram = 0;
            if (!IsServer) return;
            var deLa = new List<ItemNaEstante>();
            foreach (var o in _itens) if (o.estante == de) deLa.Add(o);
            foreach (var o in deLa)
            {
                if (!CabemNasEstantes(para, new[] { (o.item, (int)o.qtd) }))
                {
                    ficaram++;
                    continue;
                }
                int k = Indice(de, o.id);
                if (k < 0) continue;
                _itens.RemoveAt(k);
                if (GuardarNasEstantes(para, o.item, o.qtd, o.desgaste, o.cores)) foram++;
                else
                {
                    _itens.Add(o);   // (não devia acontecer: a conta acima disse que cabia)
                    ficaram++;
                }
            }
        }

        /// <summary>
        /// "Descarregar no armazém" (a tela da caçamba, 25/11/2026): com a caminhonete perto do armazém da casa da família (a
        /// tampa de trás a até <see cref="CacambaPertoDoArmazem"/> m de uma estante dele), o que couber da caçamba vai para o
        /// armazém.
        /// </summary>
        public void PedirDescarregar(int cacamba)
        {
            if (IsSpawned && EhDaCacamba(cacamba)) DescarregarRpc((ushort)cacamba);
        }

        /// <summary>Até onde do armazém a caminhonete descarrega (m, da tampa de trás ao pé da estante: a entrada da garagem).</summary>
        public const float CacambaPertoDoArmazem = 22f;

        /// <summary>A caçamba (a tampa de trás da caminhonete) está perto do armazém da casa da família (em qualquer máquina).</summary>
        public static bool CacambaPertoDoArmazemDaFamilia(int cacamba)
        {
            foreach (var c in Vehicles.Carro.Todos)
            {
                if (c == null || !c.cacamba || c.CacambaDeGuardar != cacamba) continue;
                foreach (var kv in LugarDoArmazem)
                    if (kv.Value != null && (kv.Value.position - c.PontoDoPortaMalas).sqrMagnitude < CacambaPertoDoArmazem * CacambaPertoDoArmazem) return true;
            }
            return false;
        }

        [Rpc(SendTo.Server)]
        void DescarregarRpc(ushort cacamba, RpcParams parametros = default)
        {
            if (!Quem(parametros, cacamba, out var inventario)) return;
            if (!CacambaPertoDoArmazemDaFamilia(cacamba))
            {
                inventario.AvisarODono("O armazém fica na garagem da casa da família: leve a caminhonete até a entrada dela.");
                return;
            }
            Passar(cacamba, OArmazem, out int foram, out int ficaram);
            Debug.Log($"[Armazém] A caçamba descarregada: {foram} no armazém, {ficaram} na caçamba.");
            inventario.AvisarODono(foram == 0 && ficaram == 0 ? "A caçamba já está vazia."
                : ficaram == 0 ? $"Descarregado: {(foram == 1 ? "1 coisa foi" : $"{foram} coisas foram")} para o armazém da garagem."
                : $"O armazém encheu: {(foram == 1 ? "1 foi" : $"{foram} foram")}, {(ficaram == 1 ? "1 ficou" : $"{ficaram} ficaram")} na caçamba.");
        }

        // ------------------------------------------------------------------ os moradores (a parte 6 do Plano_NPCs_Bunker.md)

        /// <summary>A escala do móvel da estante no bunker (a dos móveis, ×1,30).</summary>
        public static float EscalaDa(int estante)
        {
            var todos = MoveisDoBunker.Todos;
            return estante >= 0 && estante < todos.Length ? todos[estante].escala : MedidasDaMobilia.Escala;
        }

        /// <summary>A altura da tábua do item (m do chão do andar, na escala do jogo).</summary>
        public static float AlturaDe(ItemNaEstante it)
        {
            var f = FormaDa(it.estante);
            if (f == null) return 0f;
            // no suporte, a mão pega o taco no meio dele (e a caixa na prateleirinha); na mesinha, no tampo
            if (f.especial == EspecialDaEstante.Suporte) return it.x < 4 ? 1.2f : 1.62f;
            if (f.Especial && it.x >= 0 && it.x < f.vagas.Length) return f.vagas[it.x].y * EscalaDa(it.estante);
            int tabua = Mathf.Clamp(f.Tabuas - 1 - it.y / f.linhas, 0, f.Tabuas - 1);
            return f.alturas[tabua] * EscalaDa(it.estante);
        }

        /// <summary>Onde o item fica ao longo da estante (o x do móvel, na escala do jogo; a coluna 0 fica no +X).</summary>
        public static float LadoDe(ItemNaEstante it)
        {
            var f = FormaDa(it.estante);
            if (f == null) return 0f;
            if (f.Especial && it.x >= 0 && it.x < f.vagas.Length) return f.vagas[it.x].x * EscalaDa(it.estante);
            float cw = f.largura / f.colunas;
            return (f.largura / 2f - (it.x + it.Tamanho.x / 2f) * cw) * EscalaDa(it.estante);
        }

        /// <summary>
        /// Só no anfitrião (um morador pegando): tira um do item da estante (da pilha, um; senão, o item) e diz qual era.
        /// Falso se ele não está mais lá.
        /// </summary>
        public bool TirarUm(int estante, ushort id, out Item item)
        {
            item = Item.Nada;
            int k = Indice(estante, id);
            if (!IsServer || k < 0) return false;
            var it = _itens[k];
            item = it.item;
            int tem = Itens.Quantos(it.item, it.qtd);
            if (Itens.EmPilha(it.item) && !Itens.EhMunicao(it.item) && tem > 1)
            {
                it.qtd = (ushort)(tem - 1);
                _itens[k] = it;
            }
            else _itens.RemoveAt(k);
            return true;
        }

        /// <summary>
        /// Só no anfitrião (um morador devolvendo o taco, a caixa de bolas ou o maço): guarda o item na estante, no primeiro
        /// lugar em que couber. Falso se não coube.
        /// </summary>
        public bool GuardarDoMorador(int estante, Item item, int qtd) => IsServer && item != Item.Nada && PorNaPrimeira(new[] { estante }, item, qtd, 0, 0);

        /// <summary>Só no anfitrião (um morador pegando um maço): tira o item inteiro (a pilha toda) e diz qual era e quantos.</summary>
        public bool TirarOItem(int estante, ushort id, out Item item, out int qtd)
        {
            item = Item.Nada;
            qtd = 0;
            int k = Indice(estante, id);
            if (!IsServer || k < 0) return false;
            item = _itens[k].item;
            qtd = Itens.Quantos(item, _itens[k].qtd);
            _itens.RemoveAt(k);
            return true;
        }

        /// <summary>O ponto está perto da estante (a do bunker, no chão do andar dela).</summary>
        public static bool Perto(int estante, Vector3 ponto, float distancia)
        {
            if (EhDoCaiaque(estante))
            {
                // o compartimento: a escotilha de trás do caiaque (quem está sentado nele também alcança)
                var k = Vehicles.Caiaque.DoCompartimento(estante);
                if (k == null) return false;
                var dc = ponto - k.PontoDoCompartimento;
                if (dc.y < -2.5f || dc.y > 2.5f) return false;
                dc.y = 0f;
                return dc.magnitude <= distancia;
            }
            if (EhDaCacamba(estante))
            {
                // a tampa de trás da caminhonete (quem está na caçamba também alcança)
                foreach (var c in Vehicles.Carro.Todos)
                {
                    if (c == null || !c.cacamba || c.CacambaDeGuardar != estante) continue;
                    var dc = ponto - c.PontoDoPortaMalas;
                    if (dc.y < -2.5f || dc.y > 3f) continue;
                    dc.y = 0f;
                    if (dc.magnitude <= distancia + 1.5f) return true;
                }
                return false;
            }
            if (EhDoArmazem(estante))
            {
                // o pé da estante no fundo da garagem (a frente dela é o +Z)
                if (!LugarDoArmazem.TryGetValue(estante, out var t) || t == null) return false;
                var da = ponto - (t.position + t.forward * 0.4f);
                if (da.y < -1f || da.y > 2.5f) return false;
                da.y = 0f;
                return da.magnitude <= distancia + 0.5f;
            }
            var bunker = BunkerDoMundo.Instancia;
            if (bunker == null || !bunker.PontoDoMovel(estante, out var pe)) return false;
            var d = ponto - pe;
            if (d.y < -1f || d.y > 2.5f) return false;
            d.y = 0f;
            return d.magnitude <= distancia;
        }

        // ------------------------------------------------------------------ os pedidos (do jogador local)

        /// <summary>
        /// Guarda o item da mochila na estante, nesta célula (x negativo: no primeiro lugar livre, completando antes as
        /// pilhas do mesmo item que já estão nela). A arma leva o pente.
        /// </summary>
        public void Guardar(int estante, InventarioJogador inventario, ArmaJogador arma, ushort id, int x, int y, bool girado)
        {
            var item = inventario.Do(id);
            if (!IsSpawned || item == Item.Nada || (arma != null && arma.Usando && id == inventario.IdNaMao)) return;
            if (Itens.EhArma(item) && arma != null) inventario.GuardarPente(id, arma.MunicaoDe(id, item));
            GuardarRpc((ushort)estante, id, Byte(x), Byte(y), girado);
        }

        /// <summary>Pega o item da estante para a mochila, nesta célula (x negativo: no primeiro lugar livre).</summary>
        public void Pegar(int estante, ushort id, int x, int y, bool girado)
        {
            if (IsSpawned) PegarRpc((ushort)estante, id, Byte(x), Byte(y), girado);
        }

        /// <summary>Move o item para outra célula da mesma estante.</summary>
        public void Mover(int estante, ushort id, int x, int y, bool girado)
        {
            if (IsSpawned && x >= 0 && y >= 0) MoverRpc((ushort)estante, id, (byte)x, (byte)y, girado);
        }

        const byte Livre = 255;
        static byte Byte(int v) => v < 0 || v > 254 ? Livre : (byte)v;

        // ------------------------------------------------------------------ no anfitrião

        /// <summary>
        /// No anfitrião: um jogador guardou (true) ou pegou itens de uma estante: o cliente, a estante, o item e quantos (o
        /// registro do bunker, no terminal).
        /// </summary>
        public static event System.Action<ulong, int, Item, int, bool> Mexeu;

        static void Avisar(RpcParams parametros, int estante, Item item, int n, bool guardou)
        {
            if (n > 0) Mexeu?.Invoke(parametros.Receive.SenderClientId, estante, item, n, guardou);
        }

        bool Quem(RpcParams parametros, int estante, out InventarioJogador inventario)
        {
            inventario = null;
            if (FormaDa(estante) == null) return false;
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return false;
            if (!Perto(estante, cliente.PlayerObject.transform.position, distanciaMaxima)) return false;
            inventario = cliente.PlayerObject.GetComponent<InventarioJogador>();
            if (inventario == null) return false;
            // (25/11/2026, a revisão) na fuga, o armazém e a caçamba da família ficam para trás: nada de guardar, pegar,
            // mover nem descarregar (a tela de quem já estava com ela aberta também fecha, TelaDoInventario.Estante.cs)
            if (FechadaNaFuga(estante))
            {
                inventario.AvisarODono(MotivoNaFuga(estante));
                inventario = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// A mochila guardada (na caçamba ou no armazém) pode ser vestida por este jogador agora? (25/11/2026, a revisão: antes,
        /// pegar a mochila trocava a vestida sem conferir: a vestida caía no chão, e o que não cabia na nova caía solto.) Sem
        /// mochila, sim. Com outra vestida, é uma troca (<paramref name="troca"/>): tudo o que ela leva precisa caber na nova
        /// (a mesma conta do InventarioJogador.Arrumar), e a vestida, vazia, precisa caber na estante, contando o lugar da que
        /// sai (no armazém, também na outra estante dele). Em qualquer máquina: a tela mostra o motivo antes de soltar, e o
        /// anfitrião confere de novo no PegarRpc.
        /// </summary>
        public bool PodeVestir(int estante, ItemNaEstante nova, InventarioJogador inventario, out bool troca, out string motivo)
        {
            troca = false;
            motivo = null;
            if (inventario == null || !inventario.IsSpawned || !Itens.EhMochila(nova.item)) return false;
            var vestida = inventario.MochilaVestida.Value;
            if (vestida == Item.Nada) return true;
            if (vestida == nova.item)
            {
                motivo = $"Você já veste {Itens.NomeComArtigo(vestida)}.";
                return false;
            }
            troca = true;
            if (!CabeTudoNaGrade(inventario.Mochila, Itens.EspacoDa(nova.item)))
            {
                motivo = $"O que está na sua {Itens.Nome(vestida)} não cabe na {Itens.Nome(nova.item)}: tire umas coisas antes.";
                return false;
            }
            if (!LugarDaVestida(estante, vestida, nova.id, out _, out _, out _, out _))
            {
                var f = FormaDa(estante);
                motivo = $"Não há lugar {(f != null && f.artigo == "o" ? "no" : "na")} {f?.nome} para a sua {Itens.Nome(vestida)}.";
                return false;
            }
            return true;
        }

        // onde a mochila vestida fica, na troca: na estante da que sai (contando o lugar dela) e, no armazém, senão na outra
        // estante dele
        bool LugarDaVestida(int estante, Item vestida, ushort sai, out int destino, out int x, out int y, out bool girado)
        {
            destino = estante;
            if (Espaco(estante, vestida, out x, out y, out girado, sai)) return true;
            if (EhDoArmazem(estante))
                foreach (int outra in OArmazem)
                {
                    if (outra == estante || !Espaco(outra, vestida, out x, out y, out girado)) continue;
                    destino = outra;
                    return true;
                }
            return false;
        }

        // tudo o que está na mochila cabe numa grade deste tamanho? A conta do InventarioJogador.Arrumar, numa cópia: o que
        // cabe onde está fica; o resto procura lugar, os maiores primeiro
        static bool CabeTudoNaGrade(NetworkList<ItemNaMochila> mochila, Vector2Int espaco)
        {
            var fica = new List<ItemNaMochila>();
            var sobra = new List<ItemNaMochila>();
            foreach (var it in mochila) (CabeNaGradeOndeEsta(fica, espaco, it) ? fica : sobra).Add(it);
            sobra.Sort((a, b) => (b.Tamanho.x * b.Tamanho.y).CompareTo(a.Tamanho.x * a.Tamanho.y));
            foreach (var it in sobra)
            {
                if (!InventarioJogador.EspacoNaGrade(fica, espaco, it.item, out int x, out int y, out bool girado)) return false;
                var novo = it;
                novo.x = (byte)x;
                novo.y = (byte)y;
                novo.girado = girado;
                fica.Add(novo);
            }
            return true;
        }

        static bool CabeNaGradeOndeEsta(List<ItemNaMochila> postos, Vector2Int espaco, ItemNaMochila it)
        {
            var t = it.Tamanho;
            if (t.x <= 0 || it.x + t.x > espaco.x || it.y + t.y > espaco.y) return false;
            foreach (var o in postos)
            {
                var s = o.Tamanho;
                if (it.x < o.x + s.x && o.x < it.x + t.x && it.y < o.y + s.y && o.y < it.y + t.y) return false;
            }
            return true;
        }

        ushort NovoId()
        {
            ushort id = _proximoId;
            _proximoId = (ushort)(_proximoId == ushort.MaxValue ? 1 : _proximoId + 1);
            return id;
        }

        [Rpc(SendTo.Server)]
        void GuardarRpc(ushort estante, ushort id, byte x, byte y, bool girado, RpcParams parametros = default)
        {
            if (!Quem(parametros, estante, out var inventario) || !inventario.Achar(id, out var it) || !Itens.NoJogo(it.item)) return;
            int qtd = it.qtd;
            if (x == Livre)
            {
                // as pilhas completam primeiro as do mesmo item que já estão na estante
                if (Itens.EmPilha(it.item))
                {
                    int max = Itens.Pilha(it.item), resta = Itens.Quantos(it.item, it.qtd);
                    for (int i = 0; i < _itens.Count && resta > 0; i++)
                    {
                        var o = _itens[i];
                        int tem = Itens.Quantos(o.item, o.qtd);
                        if (o.estante != estante || o.item != it.item || tem >= max) continue;
                        int n = Mathf.Min(resta, max - tem);
                        o.qtd = (ushort)(tem + n);
                        _itens[i] = o;
                        resta -= n;
                    }
                    if (resta <= 0)
                    {
                        inventario.Tirar(id);
                        Avisar(parametros, estante, it.item, Itens.Quantos(it.item, it.qtd), true);
                        return;
                    }
                    qtd = resta;
                }
                if (!Espaco(estante, it.item, out int lx, out int ly, out bool lg))
                {
                    // não coube inteira: o que entrou nas pilhas sai da mochila, e o resto fica nela
                    if (qtd != it.qtd)
                    {
                        inventario.AcertarQuantidade(id, qtd);
                        Avisar(parametros, estante, it.item, Itens.Quantos(it.item, it.qtd) - Itens.Quantos(it.item, qtd), true);
                    }
                    return;
                }
                x = (byte)lx;
                y = (byte)ly;
                girado = lg;
            }
            else if (!Cabe(estante, it.item, girado, x, y)) return;
            inventario.Tirar(id);
            _itens.Add(new ItemNaEstante { estante = estante, id = NovoId(), item = it.item, x = x, y = y, girado = girado, qtd = (ushort)qtd,
                                           desgaste = it.desgaste, cores = it.cores });
            Avisar(parametros, estante, it.item, Itens.Quantos(it.item, it.qtd), true);
        }

        [Rpc(SendTo.Server)]
        void PegarRpc(ushort estante, ushort id, byte x, byte y, bool girado, RpcParams parametros = default)
        {
            int k = Indice(estante, id);
            if (k < 0 || !Quem(parametros, estante, out var inventario)) return;
            var it = _itens[k];
            // (25/11/2026) a mochila da caçamba ou do armazém: quem pega veste. (25/11/2026, a revisão) Com outra vestida, é
            // uma troca conferida (PodeVestir): o que ela leva passa para a nova, e ela fica na estante, vazia, no lugar da
            // que saiu; se não couber, nada muda e o dono é avisado (antes, a vestida caía no chão e o resto caía solto)
            if (Itens.EhMochila(it.item))
            {
                if (!PodeVestir(estante, it, inventario, out bool troca, out string motivo))
                {
                    if (motivo != null) inventario.AvisarODono(motivo);
                    return;
                }
                var vestida = inventario.MochilaVestida.Value;
                int destino = estante, vx = 0, vy = 0;
                bool vg = false;
                if (troca && !LugarDaVestida(estante, vestida, it.id, out destino, out vx, out vy, out vg)) return;   // (o PodeVestir já conferiu)
                _itens.RemoveAt(k);
                // a vestida sai antes, para o Vestir não largar ela no chão; o Arrumar do Vestir passa o que ela levava para a
                // grade nova (cabe tudo: o PodeVestir conferiu)
                if (troca) inventario.MochilaVestida.Value = Item.Nada;
                if (!inventario.Receber(it.item, it.qtd, it.desgaste, it.cores))
                {
                    // (não acontece: a mochila sempre é vestida) tudo volta como estava
                    if (troca) inventario.MochilaVestida.Value = vestida;
                    _itens.Add(it);
                    return;
                }
                Avisar(parametros, estante, it.item, 1, false);
                if (troca)
                {
                    _itens.Add(new ItemNaEstante { estante = (ushort)destino, id = NovoId(), item = vestida, x = (byte)vx, y = (byte)vy, girado = vg });
                    Avisar(parametros, destino, vestida, 1, true);
                    var f = FormaDa(estante);
                    inventario.AvisarODono($"Você vestiu {Itens.NomeComArtigo(it.item)}; {Itens.NomeComArtigo(vestida)} ficou {(f.artigo == "o" ? "no" : "na")} {f.nome}.");
                }
                return;
            }
            if (x == Livre)
            {
                if (Itens.EmPilha(it.item))
                {
                    int sobra = inventario.ReceberEmPilha(it.item, Itens.Quantos(it.item, it.qtd));
                    Avisar(parametros, estante, it.item, Itens.Quantos(it.item, it.qtd) - Mathf.Max(0, sobra), false);
                    if (sobra <= 0) _itens.RemoveAt(k);
                    else if (sobra != Itens.Quantos(it.item, it.qtd))
                    {
                        it.qtd = (ushort)sobra;
                        _itens[k] = it;
                    }
                    return;
                }
                if (inventario.Receber(it.item, it.qtd, it.desgaste, it.cores))
                {
                    _itens.RemoveAt(k);
                    Avisar(parametros, estante, it.item, Itens.Quantos(it.item, it.qtd), false);
                }
                return;
            }
            if (inventario.PorNaGrade(it.item, x, y, girado, it.qtd, it.desgaste, it.cores))
            {
                _itens.RemoveAt(k);
                Avisar(parametros, estante, it.item, Itens.Quantos(it.item, it.qtd), false);
            }
        }

        [Rpc(SendTo.Server)]
        void MoverRpc(ushort estante, ushort id, byte x, byte y, bool girado, RpcParams parametros = default)
        {
            int k = Indice(estante, id);
            if (k < 0 || !Quem(parametros, estante, out _)) return;
            var it = _itens[k];
            if (!Cabe(estante, it.item, girado, x, y, id)) return;
            it.x = x;
            it.y = y;
            it.girado = girado;
            _itens[k] = it;
        }

        // ------------------------------------------------------------------ as estantes da cena

        /// <summary>
        /// Faz do móvel da estante (o prefab da mobília nova, já posto no andar, na escala dele) uma estante que guarda: o
        /// gatilho da mira em volta (E abre a tela) e os itens guardados nas tábuas (<see cref="ItensNaEstante"/>).
        /// </summary>
        public static void Ligar(GameObject movel, int estante, string modelo)
        {
            var forma = FormaDe(modelo);
            if (forma == null) return;
            var filtro = movel.GetComponent<MeshFilter>();
            var b = filtro != null && filtro.sharedMesh != null ? filtro.sharedMesh.bounds : new Bounds(new Vector3(0f, 0.95f, 0f), new Vector3(forma.largura, 1.9f, 0.4f));
            var mira = new GameObject("Mira") { layer = 2 };
            mira.transform.SetParent(movel.transform, false);
            var gatilho = mira.AddComponent<BoxCollider>();
            gatilho.isTrigger = true;
            gatilho.center = b.center;
            gatilho.size = b.size + new Vector3(0.04f, 0.04f, 0.06f);
            var naMira = mira.AddComponent<EstanteNaMira>();
            naMira.estante = estante;
            var itens = movel.AddComponent<ItensNaEstante>();
            itens.estante = estante;
            itens.forma = forma;
        }
    }

    /// <summary>As estantes que não têm tábuas, só vagas (a parte 8 do Plano_NPCs_Bunker.md): o suporte dos tacos e a mesinha do fumo.</summary>
    public enum EspecialDaEstante : byte { Nenhuma, Suporte, Mesinha }

    /// <summary>
    /// Os itens guardados numa estante, na tábua deles (em todas as máquinas): a malha do chão de cada um, no meio das
    /// células dele (as colunas da esquerda para a direita de quem olha de frente, a linha de trás e a da frente), deitado
    /// ou em pé como no chão, virado ao longo do lado mais comprido e, se for maior que o lugar, um pouco menor.
    /// </summary>
    public class ItensNaEstante : MonoBehaviour
    {
        public int estante;
        public EstantesDoBunker.Forma forma;

        EstantesDoBunker _ligada;
        Transform _guardados;
        bool _refazer = true;

        void OnEnable() => _refazer = true;

        void OnDisable() => Desligar();

        void OnDestroy() => Desligar();

        void Desligar()
        {
            if (_ligada != null) _ligada.Mudou -= AoMudar;
            _ligada = null;
        }

        void AoMudar(int qual)
        {
            if (qual < 0 || qual == estante) _refazer = true;
        }

        void Update()
        {
            var e = EstantesDoBunker.Instancia;
            if (e != _ligada)
            {
                Desligar();
                _ligada = e;
                if (e != null) e.Mudou += AoMudar;
                _refazer = true;
            }
            if (!_refazer || forma == null) return;
            _refazer = false;
            Refazer();
        }

        void Refazer()
        {
            if (_guardados != null) Destroy(_guardados.gameObject);
            _guardados = null;
            var e = _ligada;
            var chao = ItensNoMundo.Instancia;
            if (e == null || !e.IsSpawned || chao == null) return;
            float s = transform.localScale.x;
            foreach (var it in e.Guardados)
            {
                if (it.estante != estante) continue;
                if (_guardados == null)
                {
                    // os itens no tamanho deles: o pai deles desfaz a escala dos móveis
                    _guardados = new GameObject("Itens guardados").transform;
                    _guardados.SetParent(transform, false);
                    _guardados.localScale = Vector3.one / Mathf.Max(0.01f, s);
                }
                if (forma.Especial) PorNaVaga(it, chao, s);
                else Por(it, chao, s);
            }
        }

        // no suporte e na mesinha: o taco em pé no copo da vaga (o pé dele, a ponta grossa, para baixo), a caixa de bolas na
        // prateleirinha e os maços deitados no tampo, cada um um pouco virado
        void PorNaVaga(ItemNaEstante it, ItensNoMundo chao, float s)
        {
            var malha = chao.MalhaDe(it.item);
            if (malha == null || it.x >= forma.vagas.Length) return;
            var v = forma.vagas[it.x] * s;
            var b = malha.bounds;
            Quaternion giro;
            Vector3 ponto;
            if (Itens.EhTaco(it.item))
            {
                // o taco da malha está deitado ao longo do X: a ponta é o lado mais fino (o pé fica embaixo)
                float raioMin = 0f, raioMax = 0f;
                int nMin = 0, nMax = 0;
                foreach (var p in malha.vertices)
                {
                    float r = new Vector2(p.y - b.center.y, p.z - b.center.z).magnitude;
                    if (p.x < b.min.x + 0.03f) { raioMin += r; nMin++; }
                    if (p.x > b.max.x - 0.03f) { raioMax += r; nMax++; }
                }
                bool pontaNoMax = nMin == 0 || (nMax > 0 && raioMax / nMax < raioMin / nMin);
                giro = Quaternion.Euler(0f, 30f * it.x, 0f) * Quaternion.FromToRotation(pontaNoMax ? Vector3.right : Vector3.left, Vector3.up);
                ponto = new Vector3(pontaNoMax ? b.min.x : b.max.x, b.center.y, b.center.z);
            }
            else
            {
                giro = Itens.EhFumo(it.item) ? Quaternion.Euler(0f, 18f * (it.x - 1.5f), 0f) : Quaternion.identity;
                var tam = Medir(giro, b, out float baixo, out var meio);
                if (!Itens.EhFumo(it.item) && tam.z > tam.x)
                {
                    giro = Quaternion.Euler(0f, 90f, 0f) * giro;
                    Medir(giro, b, out baixo, out meio);
                }
                ponto = Quaternion.Inverse(giro) * new Vector3(meio.x, baixo, meio.z);
            }
            var go = new GameObject(Itens.Nome(it.item));
            go.transform.SetParent(_guardados, false);
            go.transform.localRotation = giro;
            go.transform.localPosition = v - giro * ponto + Vector3.up * 0.002f;
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = chao.MateriaisDe(it.item);
            mr.shadowCastingMode = UI.Graficos.SombraDosMoveis ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        static readonly Vector3[] _cantos = new Vector3[8];

        void Por(ItemNaEstante it, ItensNoMundo chao, float s)
        {
            var malha = chao.MalhaDe(it.item);
            if (malha == null) return;
            var f = forma;
            var t = it.Tamanho;
            int tabua = f.Tabuas - 1 - it.y / f.linhas, linha = it.y % f.linhas;
            float cw = f.largura / f.colunas, rd = f.fundo / f.linhas;
            // a coluna 0 fica à esquerda de quem olha de frente (a frente é o +Z do móvel, e a esquerda dele, o +X); a linha
            // 0 é a de trás
            float cx = (f.largura / 2f - (it.x + t.x / 2f) * cw) * s, cz = (-f.fundo / 2f + (linha + t.y / 2f) * rd) * s;
            float lx = t.x * cw * s, lz = t.y * rd * s;
            float topo = tabua + 1 < f.Tabuas ? (f.vao - 0.03f) * s : 0.7f;
            // deitada de lado (as armas) ou em pé (o resto), e virada ao longo do lado mais comprido do lugar
            var deitada = Itens.EhArma(it.item) || Itens.EhBranca(it.item) ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            var b = malha.bounds;
            var tam = Medir(deitada, b, out _, out _);
            bool largo = lx >= lz;
            var giro = (largo && tam.z > tam.x) || (!largo && tam.x > tam.z) ? Quaternion.Euler(0f, 90f, 0f) * deitada : deitada;
            tam = Medir(giro, b, out float baixo, out var meio);
            float k = Mathf.Min(1f, lx * 1.1f / Mathf.Max(0.01f, tam.x), lz * 1.15f / Mathf.Max(0.01f, tam.z), topo / Mathf.Max(0.01f, tam.y));
            k = Mathf.Max(0.35f, k);
            var go = new GameObject(Itens.Nome(it.item));
            go.transform.SetParent(_guardados, false);
            go.transform.localRotation = giro;
            go.transform.localScale = Vector3.one * k;
            go.transform.localPosition = new Vector3(cx - meio.x * k, f.alturas[tabua] * s - baixo * k + 0.002f, cz - meio.z * k);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = chao.MateriaisDe(it.item);
            r.shadowCastingMode = UI.Graficos.SombraDosMoveis ? ShadowCastingMode.On : ShadowCastingMode.Off;
            var roupas = Itens.EhRoupa(it.item) ? Roupas.ItensDasRoupas.Carregar() : null;
            if (roupas != null) roupas.Pintar(r, it.item, it.cores);
            // a arma de sucata com as melhorias postas (10/11/2026): as peças por cima, como no chão
            else if (Itens.DeSucata(it.item)) Combat.PecasDasMelhorias.NoChao(go.transform, go.GetComponent<MeshFilter>(), it.item, it.cores, malha, r.sharedMaterial);
        }

        // o tamanho da malha girada (em x, y e z), o ponto mais baixo e o meio dela
        static Vector3 Medir(Quaternion giro, Bounds b, out float baixo, out Vector3 meio)
        {
            var min = Vector3.one * float.MaxValue;
            var max = Vector3.one * float.MinValue;
            for (int c = 0; c < 8; c++)
            {
                var p = giro * new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            baixo = min.y;
            meio = (min + max) / 2f;
            return max - min;
        }
    }

    /// <summary>O gatilho em volta da estante: a mira acha ele, e E abre a tela da estante (a do inventário, com ela ao lado).</summary>
    public class EstanteNaMira : Interagivel
    {
        public int estante;

        public override string Dica(ArmaJogador quem)
        {
            var e = EstantesDoBunker.Instancia;
            var f = EstantesDoBunker.FormaDa(estante);
            if (e == null || !e.IsSpawned || f == null || quem == null) return null;
            // (25/11/2026, a revisão) na fuga, o armazém fica para trás: a dica diz por quê, e o E não abre (AbrirEstante)
            if (EstantesDoBunker.FechadaNaFuga(estante)) return EstantesDoBunker.MotivoNaFuga(estante);
            int n = e.Quantos(estante);
            return $"[E] Abrir {f.artigo} {f.nome}" + (n == 0 ? (f.artigo == "o" ? " (vazio)" : " (vazia)") : n == 1 ? " (1 item)" : $" ({n} itens)");
        }

        public override void Usar(ArmaJogador quem)
        {
            var tela = quem != null ? quem.GetComponent<TelaDoInventario>() : null;
            if (tela != null) tela.AbrirEstante(estante);
        }
    }
}
