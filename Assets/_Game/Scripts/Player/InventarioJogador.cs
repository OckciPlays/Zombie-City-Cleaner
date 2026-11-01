using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;
using Random = UnityEngine.Random;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Um item na mochila: o Id (único na mochila), o que é, a célula de cima, à esquerda, que ele ocupa e se está girado
    /// (90°: a largura e a altura trocam). Tudo vai na grade (31/10/2026: a alça saiu).
    /// <see cref="qtd"/>: as balas da caixa de munição, ou as do pente da arma (a cópia do anfitrião: o dono manda quando a
    /// arma sai da mão, é largada ou guardada).
    /// </summary>
    public struct ItemNaMochila : INetworkSerializeByMemcpy, IEquatable<ItemNaMochila>
    {
        public ushort id;
        public Item item;
        public byte x, y;
        public bool girado;
        /// <summary>As balas: na caixa de munição, ou no pente da arma.</summary>
        public ushort qtd;
        /// <summary>O desgaste das armas (0, nova; 100, quebrada: <see cref="Itens.ComDesgaste"/>).</summary>
        public byte desgaste;

        /// <summary>As células que ele ocupa, com o giro.</summary>
        public Vector2Int Tamanho => InventarioJogador.TamanhoNaGrade(item, girado);

        public bool Equals(ItemNaMochila o) => id == o.id && item == o.item && x == o.x && y == o.y && girado == o.girado && qtd == o.qtd && desgaste == o.desgaste;
    }

    /// <summary>
    /// O inventário do jogador:
    /// - o que ele veste (<see cref="MochilaVestida"/>, escrita pelo anfitrião). Sem mochila, só os bolsos;
    /// - a grade: os bolsos (4 × 2) ou a mochila vestida (pequena 6 × 4, média 8 × 5, grande 10 × 7;
    ///   <see cref="Itens.EspacoDa"/>), e cada item ocupa as células dele (<see cref="Itens.Tamanho"/>: a sniper 7 × 2, a
    ///   lata 1), deitado ou girado 90° (a tela gira com R). O que é pego entra na primeira posição em que couber (de cima
    ///   para baixo, da esquerda para a direita; se não couber deitado, girado); sem espaço, não entra. A munição e o saque
    ///   vão em pilhas (<see cref="Itens.Pilha"/>: o qtd é quantos há nela): o que é pego completa primeiro as pilhas dele
    ///   que já estão na mochila, e arrastar uma pilha em cima de outra do mesmo item junta as duas;
    /// - a fabricação e a desmontagem (<see cref="Receitas"/>; a aba Criar e o menu do botão direito da tela): o anfitrião
    ///   confere os ingredientes, a ferramenta e a bancada, tira o que gasta e põe o que sai (o que não couber cai aos pés);
    ///   Trocar ou tirar a mochila arruma a grade de novo, e o que não cabe mais cai no chão;
    /// - as armas: quantas couberem na grade (31/10/2026, a pedido: "vamos deixar liberal, carrega quantas armas conseguir
    ///   no inventário"; a alça, o coldre e o limite de armas saíram). As dos atalhos aparecem penduradas no corpo
    ///   (<see cref="EquipamentoNoCorpo.OQueVeste"/>);
    /// - a barra de atalhos: 3 lugares, cada um apontando para um item da mochila (o item continua nela). O do lugar
    ///   escolhido fica na mão; um lugar vazio deixa a mão vazia (os punhos). A roda passa pelos 3, vazios ou não, 1, 2 e
    ///   3 escolhem, G larga o da mão.
    /// - a munição: caixas de até 60 balas (1 × 1), uma espécie por calibre (<see cref="Calibre"/>). A recarga tira as
    ///   balas delas (<see cref="BalasDe"/>, <see cref="Gastar"/>); a caixa vazia some. Pegar uma caixa completa primeiro as
    ///   do mesmo calibre que já estão na mochila; arrastar uma caixa em cima de outra do mesmo calibre junta as duas.
    ///   As caixas não vão nos atalhos (nem na mão);
    /// - o pente de cada arma vai junto com ela (<see cref="ItemNaMochila.qtd"/>): largada no chão, guardada no painel ou
    ///   pega de novo, ela volta com as balas que tinha.
    /// A arma pega vai para um atalho livre e para a mão; o que é pego com a mão vazia vai para a mão.
    /// A mochila é do anfitrião (pegar, largar, comer, mover e o painel de armas passam por ele); os atalhos e o lugar
    /// escolhido são do dono. A tela da mochila (TAB) é a <see cref="TelaDoInventario"/>; aqui fica a barra do HUD.
    /// </summary>
    public class InventarioJogador : NetworkBehaviour
    {
        public const int Atalhos = 3;

        [Tooltip("Os ícones coloridos, na ordem de Itens.Modelos (as armas, a lata e a garrafa).")]
        public Texture2D[] icones;
        [Tooltip("A foto de cada modelo, na mesma ordem (os detalhes da tela do inventário).")]
        public Texture2D[] fotos;
        [Tooltip("Os ícones e as fotos dos equipamentos (as 3 mochilas e o coldre), na ordem do enum Item.")]
        public Texture2D[] iconesDeEquipamento, fotosDeEquipamento;
        [Tooltip("Os ícones e as fotos das caixas de munição, na ordem de Calibre.")]
        public Texture2D[] iconesDeMunicao, fotosDeMunicao;
        [Tooltip("Os ícones e as fotos das peças do motor (a vela, a bateria e o radiador), na ordem do enum Item.")]
        public Texture2D[] iconesDePeca, fotosDePeca;
        [Tooltip("Os ícones e as fotos da sinuca (as 2 caixas de bolas e os 4 tacos), na ordem do enum Item.")]
        public Texture2D[] iconesDaSinuca, fotosDaSinuca;
        [Tooltip("Os ícones e as fotos do saque (o loot.py), na ordem de Itens.DoLoot.")]
        public Texture2D[] iconesDeLoot, fotosDeLoot;

        /// <summary>
        /// A mochila (a grade; escrita pelo anfitrião). O jogador nasce sem mochila, com a pistola nos bolsos, no primeiro
        /// atalho.
        /// </summary>
        public NetworkList<ItemNaMochila> Mochila { get; private set; }
        /// <summary>A mochila que ele veste (Nada: só os bolsos). Escrita pelo anfitrião.</summary>
        public readonly NetworkVariable<Item> MochilaVestida = new(Item.Nada, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>
        /// O pneu que ele carrega nas mãos (o estado; <see cref="SemPneu"/>, nenhum), quando ele não coube na mochila.
        /// Escrito pelo anfitrião. Com ele, as mãos ficam ocupadas (sem arma) e o jogador anda devagar, sem correr nem pular.
        /// </summary>
        public readonly NetworkVariable<byte> PneuNasMaos = new(SemPneu, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public const byte SemPneu = 255;
        /// <summary>Carregando um pneu nas mãos, o jogador anda nesta fração da velocidade (e não corre nem pula).</summary>
        public const float LentidaoDoPneu = 0.55f;
        public bool CarregandoPneu => IsSpawned && PneuNasMaos.Value != SemPneu;
        /// <summary>O estado do pneu das mãos (%), ou -1 sem ele.</summary>
        public int EstadoDoPneuNasMaos => CarregandoPneu ? PneuNasMaos.Value : -1;

        /// <summary>O tamanho da grade agora: os bolsos ou a mochila vestida.</summary>
        public Vector2Int TamanhoDaGrade => Itens.EspacoDa(MochilaVestida.Value);
        public int Colunas => TamanhoDaGrade.x;
        public int Linhas => TamanhoDaGrade.y;
        // os 3 atalhos, 16 bits cada (o Id do item na mochila; 0 = vazio), escritos pelo dono
        readonly NetworkVariable<long> _atalhos = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>O atalho da mão (0 a 2).</summary>
        public readonly NetworkVariable<byte> Escolhido = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>O Id do item de um atalho (0, vazio; pode apontar para um item que acabou de sair da mochila).</summary>
        public ushort Atalho(int lugar) => Ler(_atalhos.Value, lugar);
        /// <summary>O item de um atalho (Nada, vazio).</summary>
        public Item this[int lugar] => Do(Atalho(lugar));
        public Item NaMao => Do(Atalho(Escolhido.Value));
        /// <summary>O Id do item da mão (0, a mão vazia).</summary>
        public ushort IdNaMao => NaMao == Item.Nada ? (ushort)0 : Atalho(Escolhido.Value);

        /// <summary>A mochila, os atalhos ou o atalho da mão mudaram (em todas as cópias).</summary>
        public event Action Mudou;
        /// <summary>No dono: uma receita saiu, ou um item foi desmontado (o texto do aviso).</summary>
        public event Action<string> Feito;

        // o raio de largar não vê o próprio jogador, os braços, os zumbis, os corpos e o Ignore Raycast
        const int MascaraDoCenario = ~((1 << 2) | (1 << JogadorRede.CamadaBracos) | (1 << 7) | (1 << 8) | (1 << JogadorRede.CamadaJogador));

        ArmaJogador _arma;
        JogadorRede _rede;
        TelaDoInventario _tela;
        float _pegouAte, _nomeAte;
        ushort _proximoId = 1, _consumindo, _voltarPara;
        // as balas que o dono já gastou e o anfitrião ainda não tirou das caixas (até a mochila mudar, ou 1,5 s)
        readonly int[] _gastoPendente = new int[4];
        float _pendenteAte;

        void Awake() => Mochila = new NetworkList<ItemNaMochila>();

        public override void OnNetworkSpawn()
        {
            _arma = GetComponent<ArmaJogador>();
            _rede = GetComponent<JogadorRede>();
            _tela = GetComponent<TelaDoInventario>();
            Mochila.OnListChanged += AoMudarMochila;
            _atalhos.OnValueChanged += AoMudarAtalhos;
            Escolhido.OnValueChanged += AoMudarEscolhido;
            MochilaVestida.OnValueChanged += AoMudarEquipamento;
            PneuNasMaos.OnValueChanged += AoMudarPneu;
            if (IsServer)
            {
                Mochila.Clear();
                MochilaVestida.Value = Item.Nada;
                PneuNasMaos.Value = SemPneu;
                // na cena de vídeo, de mãos vazias; no Dia 0 também (a vida normal: o que tiver, foi comprado)
                var mundo = World.MundoDaPartida.Instancia;
                bool diaZero = mundo != null && mundo.NoDiaZero;
                if (!ModoVideo.SemArma && !diaZero) Receber(Item.Pistola);
            }
            // a pistola do começo no primeiro atalho
            if (IsOwner && _atalhos.Value == 0 && Mochila.Count > 0) _atalhos.Value = Mochila[0].id;
            Mudou?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            Mochila.OnListChanged -= AoMudarMochila;
            _atalhos.OnValueChanged -= AoMudarAtalhos;
            Escolhido.OnValueChanged -= AoMudarEscolhido;
            MochilaVestida.OnValueChanged -= AoMudarEquipamento;
            PneuNasMaos.OnValueChanged -= AoMudarPneu;
            if (_pneuNoCorpo != null) Destroy(_pneuNoCorpo);
            if (_pneuNoOlho != null) Destroy(_pneuNoOlho);
        }

        void AoMudarEquipamento(Item antes, Item depois) => Mudou?.Invoke();
        void AoMudarPneu(byte antes, byte depois) => Mudou?.Invoke();

        static ushort Ler(long atalhos, int lugar) => (ushort)((atalhos >> (16 * lugar)) & 0xFFFF);
        static long Gravar(long atalhos, int lugar, ushort id) => (atalhos & ~(0xFFFFL << (16 * lugar))) | ((long)id << (16 * lugar));

        /// <summary>O item da mochila com este Id (Nada, se não está nela).</summary>
        public Item Do(ushort id) => id != 0 && Achar(id, out var it) ? it.item : Item.Nada;

        public bool Achar(ushort id, out ItemNaMochila item)
        {
            int k = Indice(id);
            item = k >= 0 ? Mochila[k] : default;
            return k >= 0;
        }

        int Indice(ushort id)
        {
            for (int i = 0; i < Mochila.Count; i++)
                if (Mochila[i].id == id) return i;
            return -1;
        }

        /// <summary>O atalho que aponta para o item (-1, nenhum).</summary>
        public int LugarDoAtalho(ushort id)
        {
            for (int i = 0; id != 0 && i < Atalhos; i++)
                if (Atalho(i) == id) return i;
            return -1;
        }

        // ------------------------------------------------------------------ a grade

        /// <summary>As células que o item ocupa na grade: as dele, ou trocadas, girado.</summary>
        public static Vector2Int TamanhoNaGrade(Item item, bool girado)
        {
            var t = Itens.Tamanho(item);
            return girado ? new Vector2Int(t.y, t.x) : t;
        }

        // uma cópia da lista: as contas da grade (e a arrumação ao trocar de mochila) não mexem na lista da rede
        readonly List<ItemNaMochila> _copia = new List<ItemNaMochila>();

        List<ItemNaMochila> Copia()
        {
            _copia.Clear();
            foreach (var it in Mochila) _copia.Add(it);
            return _copia;
        }

        static bool CabeEm(List<ItemNaMochila> lista, Vector2Int espaco, Item item, bool girado, int x, int y, ushort ignorar)
        {
            var t = TamanhoNaGrade(item, girado);
            if (t.x <= 0 || x < 0 || y < 0 || x + t.x > espaco.x || y + t.y > espaco.y) return false;
            foreach (var o in lista)
            {
                if (o.id == ignorar) continue;
                var s = o.Tamanho;
                if (x < o.x + s.x && o.x < x + t.x && y < o.y + s.y && o.y < y + t.y) return false;
            }
            return true;
        }

        static bool EspacoEm(List<ItemNaMochila> lista, Vector2Int espaco, Item item, out int x, out int y, out bool girado)
        {
            var t = Itens.Tamanho(item);
            for (int g = 0; g < (t.x != t.y ? 2 : 1); g++)
            {
                girado = g == 1;
                for (y = 0; y < espaco.y; y++)
                    for (x = 0; x < espaco.x; x++)
                        if (CabeEm(lista, espaco, item, girado, x, y, 0)) return true;
            }
            x = y = -1;
            girado = false;
            return false;
        }

        /// <summary>
        /// O item cabe (girado ou não) com o canto de cima, à esquerda, nesta célula, sem contar o item 'ignorar' (o que
        /// se move).
        /// </summary>
        public bool Cabe(Item item, bool girado, int x, int y, ushort ignorar = 0) => CabeEm(Copia(), TamanhoDaGrade, item, girado, x, y, ignorar);

        /// <summary>
        /// A primeira posição em que o item cabe (de cima para baixo, da esquerda para a direita), deitado; se não houver,
        /// girado.
        /// </summary>
        public bool Espaco(Item item, out int x, out int y, out bool girado) => EspacoEm(Copia(), TamanhoDaGrade, item, out x, out y, out girado);

        public bool TemEspaco(Item item) => Espaco(item, out _, out _, out _);

        /// <summary>O item pode ir para esta posição da grade.</summary>
        public bool PodeMover(ushort id, int x, int y, bool girado) => Achar(id, out var it) && Cabe(it.item, girado, x, y, id);

        /// <summary>
        /// O jogador pode pegar o item agora? Se não, o motivo (para a dica). Tudo vai para a grade, se houver espaço
        /// (as armas também: sem limite de armas). A mochila nova troca a vestida (que cai no chão).
        /// </summary>
        public bool PodeReceber(Item item, out string motivo)
        {
            motivo = null;
            if (item == Item.Nada) return false;
            bool mochila = MochilaVestida.Value != Item.Nada;
            if (Itens.EhEquipamento(item)) return true;
            if (!Itens.NoJogo(item))
            {
                motivo = $"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(item))} saiu do jogo";
                return false;
            }
            // a pilha completa primeiro as do mesmo item (a caixa de munição, as do mesmo calibre)
            if (Itens.EmPilha(item) && FaltaNasPilhas(item) > 0) return true;
            if (TemEspaco(item)) return true;
            // o pneu que não cabe vai para as mãos (pesado)
            if (item == Item.Pneu && !CarregandoPneu) return true;
            motivo = item == Item.Pneu ? "Você já carrega um pneu nas mãos"
                : $"Sem espaço {(mochila ? "na mochila" : "nos bolsos")} para {Itens.NomeComArtigo(item)}";
            return false;
        }

        /// <summary>O pneu vai para as mãos (não cabe na mochila e as mãos estão livres).</summary>
        public bool PneuVaiParaAsMaos => !TemEspaco(Item.Pneu) && !CarregandoPneu;

        /// <summary>
        /// A dica de pegar o item ("[E] Pegar a lata de comida", "[E] Pegar a caixa de 9mm (24 balas)"), ou o motivo de não
        /// poder. <paramref name="qtd"/>: as balas da caixa ou do pente da arma (-1, não mostra).
        /// </summary>
        public string DicaDePegar(Item item, int qtd = -1)
        {
            if (!PodeReceber(item, out var motivo)) return motivo;
            if (Itens.EhMochila(item) && MochilaVestida.Value != Item.Nada)
                return $"[E] Trocar a sua {Itens.Nome(MochilaVestida.Value)} pela {Itens.Nome(item)}";
            if (item == Item.Pneu && PneuVaiParaAsMaos) return $"[E] Carregar o pneu{(qtd >= 0 ? $" ({qtd}%)" : "")} nas mãos (pesado)";
            string balas = qtd < 0 ? "" : Itens.EhMunicao(item) ? $" ({qtd} {(qtd == 1 ? "bala" : "balas")})"
                : Itens.EmPilha(item) ? (qtd > 1 ? $" ({qtd})" : "")
                : Itens.EhArma(item) ? $" ({qtd}/{Itens.QuantidadePadrao(item)})" : Itens.ComEstado(item) ? $" ({qtd}%)" : "";
            return $"[E] {(Itens.EhEquipamento(item) ? "Vestir" : "Pegar")} {Itens.NomeComArtigo(item)}{balas}";
        }

        // ------------------------------------------------------------------ a munição

        /// <summary>
        /// As balas deste calibre nas caixas da mochila, menos as que o dono já gastou e o anfitrião ainda não tirou.
        /// </summary>
        public int BalasDe(Calibre calibre)
        {
            var caixa = Itens.CaixaDe(calibre);
            int n = 0;
            foreach (var it in Mochila)
                if (it.item == caixa) n += it.qtd;
            if (Time.time < _pendenteAte) n -= _gastoPendente[(int)calibre];
            return Mathf.Max(0, n);
        }

        // quantos ainda cabem nas pilhas deste item que já estão na mochila (as balas nas caixas do calibre)
        int FaltaNasPilhas(Item item)
        {
            int max = Itens.Pilha(item), n = 0;
            foreach (var it in Mochila)
                if (it.item == item) n += Mathf.Max(0, max - Itens.Quantos(item, it.qtd));
            return n;
        }

        /// <summary>Quantos deste item há na mochila (somando as pilhas).</summary>
        public int Quantos(Item item)
        {
            int n = 0;
            foreach (var it in Mochila)
                if (it.item == item) n += Itens.Quantos(item, it.qtd);
            return n;
        }

        /// <summary>
        /// A receita pode sair agora? Se não, o que falta (para a tela): os ingredientes, a ferramenta (que não gasta) e,
        /// nas da bancada, estar perto de uma.
        /// </summary>
        public bool PodeCriar(Receita r, out string falta)
        {
            falta = null;
            if (r.bancada && !Bancada.Perto(transform.position)) falta = "Precisa de uma bancada perto";
            else if (r.ferramenta != Item.Nada && Quantos(r.ferramenta) == 0) falta = $"Precisa ter {Itens.Nome(r.ferramenta)} na mochila";
            else
                foreach (var g in r.gasta)
                    if (Quantos(g.item) < g.qtd)
                    {
                        falta = $"Falta {Itens.ComNumero(g.item, g.qtd - Quantos(g.item))}";
                        break;
                    }
            return falta == null;
        }

        /// <summary>O item da mochila pode ser desmontado agora? Se não, o motivo (a ferramenta que falta).</summary>
        public bool PodeDesmontar(ushort id, out Desmontagem d, out string falta)
        {
            falta = null;
            d = Achar(id, out var it) ? Receitas.DesmontagemDe(it.item) : null;
            if (d == null) return false;
            if (d.ferramenta != Item.Nada && Quantos(d.ferramenta) == 0) falta = $"precisa ter {Itens.Nome(d.ferramenta)}";
            return falta == null;
        }

        /// <summary>
        /// O dono gastou balas (a recarga): o anfitrião tira das caixas deste calibre (das mais vazias primeiro; a que
        /// esvazia some). Até ele responder, <see cref="BalasDe"/> já desconta.
        /// </summary>
        public void Gastar(Calibre calibre, int balas)
        {
            if (balas <= 0) return;
            if (Time.time >= _pendenteAte) Array.Clear(_gastoPendente, 0, _gastoPendente.Length);
            _gastoPendente[(int)calibre] += balas;
            _pendenteAte = Time.time + 1.5f;
            GastarRpc(calibre, (ushort)balas);
        }

        /// <summary>Junta a pilha 'de' na pilha 'para' (do mesmo item; as caixas, do mesmo calibre), até encher a 'para'.</summary>
        public void Juntar(ushort de, ushort para)
        {
            if (PodeJuntar(de, para)) JuntarRpc(de, para);
        }

        /// <summary>As duas são pilhas do mesmo item (as caixas, do mesmo calibre), e a 'para' ainda não está cheia.</summary>
        public bool PodeJuntar(ushort de, ushort para) =>
            de != para && Achar(de, out var a) && Achar(para, out var b) && Itens.EmPilha(a.item) && a.item == b.item && Itens.Quantos(b.item, b.qtd) < Itens.Pilha(b.item);

        /// <summary>O dono guarda no anfitrião as balas do pente de uma arma (quando ela sai da mão, é largada ou guardada).</summary>
        public void GuardarPente(ushort id, int balas)
        {
            if (id != 0 && Achar(id, out var it) && Itens.EhArma(it.item) && it.qtd != balas) GuardarPenteRpc(id, (ushort)Mathf.Max(0, balas));
        }

        /// <summary>O pente da arma da mão vai para o anfitrião (antes de guardá-la no painel, por exemplo).</summary>
        public void GuardarPenteDaMao()
        {
            if (_arma != null && _arma.ComArma) GuardarPente(IdNaMao, _arma.Municao);
        }

        // ------------------------------------------------------------------ no anfitrião

        ushort NovoId()
        {
            ushort id = _proximoId;
            _proximoId = (ushort)(_proximoId == ushort.MaxValue ? 1 : _proximoId + 1);
            return id;
        }

        /// <summary>
        /// Só no anfitrião: o item entra na primeira posição da grade em que cabe (deitado, ou girado). O equipamento é
        /// vestido. Sem lugar, devolve false.
        /// </summary>
        public bool Receber(Item item, int qtd = -1, int desgaste = 0)
        {
            if (!IsServer || item == Item.Nada) return false;
            if (Itens.EhEquipamento(item)) return Vestir(item);
            if (qtd < 0) qtd = Itens.QuantidadePadrao(item);
            if (Itens.EmPilha(item)) return ReceberEmPilha(item, Itens.Quantos(item, qtd)) == 0;
            if (!Itens.NoJogo(item)) return false;
            if (!Espaco(item, out int x, out int y, out bool girado))
            {
                // o pneu que não cabe vai para as mãos, se elas estão livres
                if (item != Item.Pneu || PneuNasMaos.Value != SemPneu) return false;
                PneuNasMaos.Value = (byte)Mathf.Clamp(qtd, 0, 100);
                return true;
            }
            Mochila.Add(new ItemNaMochila { id = NovoId(), item = item, x = (byte)x, y = (byte)y, girado = girado, qtd = (ushort)qtd,
                                            desgaste = (byte)Mathf.Clamp(desgaste, 0, 100) });
            return true;
        }

        /// <summary>
        /// Só no anfitrião: os de uma pilha entram (as balas de uma caixa de munição, os do saque). Primeiro completam as
        /// pilhas do mesmo item que já estão na mochila; o resto vai em pilhas novas, enquanto houver espaço. Devolve
        /// quantos sobraram (que ficam no chão).
        /// </summary>
        public int ReceberEmPilha(Item item, int qtd)
        {
            if (!IsServer || !Itens.EmPilha(item) || !Itens.NoJogo(item)) return qtd;
            int max = Itens.Pilha(item);
            for (int i = 0; i < Mochila.Count && qtd > 0; i++)
            {
                var it = Mochila[i];
                int tem = Itens.Quantos(item, it.qtd);
                if (it.item != item || tem >= max) continue;
                int n = Mathf.Min(qtd, max - tem);
                it.qtd = (ushort)(tem + n);
                Mochila[i] = it;
                qtd -= n;
            }
            while (qtd > 0 && Espaco(item, out int x, out int y, out bool girado))
            {
                int n = Mathf.Min(qtd, max);
                Mochila.Add(new ItemNaMochila { id = NovoId(), item = item, x = (byte)x, y = (byte)y, girado = girado, qtd = (ushort)n });
                qtd -= n;
            }
            return qtd;
        }

        /// <summary>Só no anfitrião: as balas de uma caixa entram (<see cref="ReceberEmPilha"/>). Devolve as que sobraram.</summary>
        public int ReceberMunicao(Item caixa, int balas) => Itens.EhMunicao(caixa) ? ReceberEmPilha(caixa, balas) : balas;

        /// <summary>
        /// Só no anfitrião: tira um da pilha (ou o item, se ele não vai em pilha ou é o último). Devolve o que ele era
        /// (Nada, se não estava).
        /// </summary>
        public Item TirarUm(ushort id)
        {
            int k = IsServer ? Indice(id) : -1;
            if (k < 0) return Item.Nada;
            var it = Mochila[k];
            int tem = Itens.Quantos(it.item, it.qtd);
            if (!Itens.EmPilha(it.item) || tem <= 1) Mochila.RemoveAt(k);
            else
            {
                it.qtd = (ushort)(tem - 1);
                Mochila[k] = it;
            }
            return it.item;
        }

        /// <summary>Só no anfitrião: gasta n do item (das pilhas), se ele tiver; devolve se gastou.</summary>
        public bool GastarItem(Item item, int n)
        {
            if (!IsServer || Quantos(item) < n) return false;
            TirarQuantos(item, n);
            return true;
        }

        /// <summary>Só no anfitrião: n do item vão para a mochila; o que não couber cai aos pés.</summary>
        public void EntregarItem(Item item, int n)
        {
            if (IsServer) Entregar(item, n);
        }

        // só no anfitrião: tira n do item, das pilhas menores primeiro (sobram as cheias)
        void TirarQuantos(Item item, int n)
        {
            while (n > 0)
            {
                int k = -1;
                for (int i = 0; i < Mochila.Count; i++)
                    if (Mochila[i].item == item && (k < 0 || Itens.Quantos(item, Mochila[i].qtd) < Itens.Quantos(item, Mochila[k].qtd))) k = i;
                if (k < 0) return;
                var it = Mochila[k];
                int tem = Itens.Quantos(item, it.qtd), tira = Mathf.Min(n, tem);
                n -= tira;
                if (tira >= tem) Mochila.RemoveAt(k);
                else
                {
                    it.qtd = (ushort)(tem - tira);
                    Mochila[k] = it;
                }
            }
        }

        // só no anfitrião: o que saiu (de uma receita ou de uma desmontagem) vai para a mochila; o que não couber cai aos
        // pés. A mochila feita é vestida, se ele estiver sem nenhuma (senão, fica no chão)
        void Entregar(Item item, int qtd)
        {
            int caidos = 0;
            if (Itens.EhMochila(item))
            {
                if (MochilaVestida.Value == Item.Nada) Vestir(item);
                else ItensNoMundo.Instancia?.Criar(item, PontoAosPes(++caidos), transform.eulerAngles.y + 90f, 0);
                return;
            }
            if (Itens.EmPilha(item))
            {
                int sobra = ReceberEmPilha(item, qtd);
                if (sobra > 0) ItensNoMundo.Instancia?.Criar(item, PontoAosPes(++caidos), transform.eulerAngles.y + 40f, sobra);
                return;
            }
            for (int i = 0; i < qtd; i++)
                if (!Receber(item)) ItensNoMundo.Instancia?.Criar(item, PontoAosPes(++caidos), transform.eulerAngles.y + caidos * 70f, -1);
        }

        /// <summary>
        /// Só no anfitrião: veste o equipamento. A mochila nova troca a que ele vestia (essa cai no chão, aos pés dele),
        /// e a grade é arrumada no tamanho novo.
        /// </summary>
        bool Vestir(Item item)
        {
            if (!Itens.EhMochila(item)) return false;
            var antes = MochilaVestida.Value;
            MochilaVestida.Value = item;
            if (antes != Item.Nada) ItensNoMundo.Instancia?.Criar(antes, PontoAosPes(0), transform.eulerAngles.y + 90f, 0);
            Arrumar(transform.position);
            return true;
        }

        /// <summary>Só no anfitrião: o pneu sai das mãos (para uma roda ou o porta-malas). Devolve o estado dele.</summary>
        public bool TirarPneuDasMaos(out int estado)
        {
            estado = PneuNasMaos.Value;
            if (!IsServer || PneuNasMaos.Value == SemPneu) return false;
            PneuNasMaos.Value = SemPneu;
            return true;
        }

        /// <summary>Só no anfitrião: tira o item da mochila e devolve o que ele era (Nada, se não estava).</summary>
        public Item Tirar(ushort id)
        {
            int k = IsServer ? Indice(id) : -1;
            if (k < 0) return Item.Nada;
            var item = Mochila[k].item;
            Mochila.RemoveAt(k);
            return item;
        }

        /// <summary>
        /// Só no anfitrião: o item vira outro do mesmo tamanho, no mesmo lugar da grade (a caixa de bolas cheia vira a
        /// vazia, e a vazia, a cheia). Falso se ele não está na mochila ou se os tamanhos são diferentes.
        /// </summary>
        public bool TrocarItem(ushort id, Item novo)
        {
            int k = IsServer ? Indice(id) : -1;
            if (k < 0) return false;
            var it = Mochila[k];
            if (Itens.Tamanho(it.item) != Itens.Tamanho(novo)) return false;
            it.item = novo;
            it.qtd = 0;
            Mochila[k] = it;
            return true;
        }

        /// <summary>O primeiro item deste tipo na mochila (o Id; 0, nenhum).</summary>
        public ushort Primeiro(Item item)
        {
            foreach (var it in Mochila)
                if (it.item == item) return it.id;
            return 0;
        }

        /// <summary>
        /// Só no anfitrião, depois de trocar ou tirar a mochila: o que ainda cabe fica onde está; o resto procura outro
        /// lugar na grade nova (os maiores primeiro), e o que não couber cai no chão, em volta do ponto.
        /// </summary>
        void Arrumar(Vector3 onde)
        {
            var espaco = TamanhoDaGrade;
            var fica = new List<ItemNaMochila>();
            var sobra = new List<ItemNaMochila>();
            foreach (var it in Mochila)
                (CabeEm(fica, espaco, it.item, it.girado, it.x, it.y, 0) ? fica : sobra).Add(it);
            sobra.Sort((a, b) => (b.Tamanho.x * b.Tamanho.y).CompareTo(a.Tamanho.x * a.Tamanho.y));
            var mudou = new Dictionary<ushort, ItemNaMochila>();
            var cai = new HashSet<ushort>();
            foreach (var it in sobra)
            {
                if (EspacoEm(fica, espaco, it.item, out int x, out int y, out bool girado))
                {
                    var novo = it;
                    novo.x = (byte)x;
                    novo.y = (byte)y;
                    novo.girado = girado;
                    fica.Add(novo);
                    mudou[it.id] = novo;
                }
                else cai.Add(it.id);
            }
            int caidos = 0;
            for (int i = Mochila.Count - 1; i >= 0; i--)
            {
                var it = Mochila[i];
                if (cai.Contains(it.id))
                {
                    Mochila.RemoveAt(i);
                    ItensNoMundo.Instancia?.Criar(it.item, PontoAosPes(++caidos, onde), transform.eulerAngles.y + caidos * 70f, it.qtd, it.desgaste);
                }
                else if (mudou.TryGetValue(it.id, out var novo)) Mochila[i] = novo;
            }
        }

        // um ponto no chão em volta dos pés (ou do ponto dado), um pouco para o lado a cada item, para não ficarem um em cima do outro
        Vector3 PontoAosPes(int k, Vector3? centro = null)
        {
            var c = centro ?? transform.position;
            return k == 0 ? c : c + Quaternion.Euler(0f, k * 75f, 0f) * Vector3.forward * (0.3f + 0.08f * k);
        }

        /// <summary>
        /// Só no anfitrião, quando o jogador sai da partida: as armas voltam para o painel do bunker (se houver vaga do
        /// tipo delas, com o pente que tinham), e o resto (a comida, a água, a munição, os materiais, a mochila...) fica no
        /// chão, onde ele estava.
        /// </summary>
        public void Esvaziar()
        {
            int k = 0;
            for (int i = 0; i < Mochila.Count; i++)
            {
                var it = Mochila[i];
                if (Itens.EhArma(it.item)) ParedeDeArmas.Instancia?.Devolver(Itens.Arma(it.item), it.qtd);
                else ItensNoMundo.Instancia?.Criar(it.item, PontoAosPes(++k), k * 90f, it.qtd, it.desgaste);
            }
            if (MochilaVestida.Value != Item.Nada) ItensNoMundo.Instancia?.Criar(MochilaVestida.Value, PontoAosPes(++k), k * 90f, 0);
            if (TirarPneuDasMaos(out int pneu)) ItensNoMundo.Instancia?.Criar(Item.Pneu, PontoAosPes(++k), k * 90f, pneu);
        }

        [Rpc(SendTo.Server)]
        void MoverRpc(ushort id, byte x, byte y, bool girado, RpcParams parametros = default)
        {
            int k = Indice(id);
            if (parametros.Receive.SenderClientId != OwnerClientId || k < 0) return;
            var it = Mochila[k];
            if (it.x == x && it.y == y && it.girado == girado) return;
            if (!PodeMover(id, x, y, girado)) return;
            it.x = x;
            it.y = y;
            it.girado = girado;
            Mochila[k] = it;
        }

        [Rpc(SendTo.Server)]
        void TirarEquipamentoRpc(Item qual, Vector3 ponto, float giro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            if (!Itens.EhMochila(qual) || MochilaVestida.Value != qual) return;
            MochilaVestida.Value = Item.Nada;
            if ((ponto - transform.position).sqrMagnitude > 4f * 4f) ponto = transform.position;
            ItensNoMundo.Instancia?.Criar(qual, ponto, giro, 0);
            Arrumar(ponto);
        }

        [Rpc(SendTo.Server)]
        void ConsumirRpc(ushort id, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !Itens.EhConsumivel(Do(id))) return;
            var item = TirarUm(id);
            var d = Itens.Consumivel(item);
            GetComponent<FomeESede>().Consumir(d);
            if (d.vida > 0f) GetComponent<VidaJogador>().Curar(d.vida);
        }

        [Rpc(SendTo.Server)]
        void DesgastarRpc(ushort id, byte n, RpcParams parametros = default)
        {
            int k = Indice(id);
            if (parametros.Receive.SenderClientId != OwnerClientId || k < 0) return;
            var it = Mochila[k];
            if (!Itens.ComDesgaste(it.item)) return;
            int novo = Mathf.Min(100, it.desgaste + n);
            // a branca gasta até quebrar (some); a de fogo fica, quebrada, esperando o kit de reparo
            if (novo >= 100 && Itens.EhBranca(it.item))
            {
                Mochila.RemoveAt(k);
                AvisoRpc(it.item == Item.Tocha ? "A tocha se apagou" : $"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(it.item))} quebrou");
                return;
            }
            if (novo >= 100 && it.desgaste < 100) AvisoRpc($"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(it.item))} quebrou: conserte com um kit de reparo");
            it.desgaste = (byte)novo;
            Mochila[k] = it;
        }

        [Rpc(SendTo.Server)]
        void RepararRpc(ushort id, RpcParams parametros = default)
        {
            int k = Indice(id);
            if (parametros.Receive.SenderClientId != OwnerClientId || k < 0 || !PodeReparar(id)) return;
            if (!GastarItem(Item.KitDeReparo, 1)) return;
            k = Indice(id);
            var it = Mochila[k];
            it.desgaste = 0;
            Mochila[k] = it;
            AvisoRpc($"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(it.item))} consertada");
        }

        [Rpc(SendTo.Owner)]
        void AvisoRpc(string texto)
        {
            Feito?.Invoke(texto);
            _avisoNoHud = texto;
            _avisoNoHudAte = Time.time + 2.8f;
        }

        [Rpc(SendTo.Server)]
        void LargarUmRpc(ushort id, Vector3 ponto, float giro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || ItensNoMundo.Instancia == null) return;
            if ((ponto - transform.position).sqrMagnitude > 4f * 4f) ponto = transform.position;
            if (!Achar(id, out var it) || !Itens.EmPilha(it.item)) return;
            TirarUm(id);
            ItensNoMundo.Instancia.Criar(it.item, ponto, giro, 1);
        }

        [Rpc(SendTo.Server)]
        void CriarRpc(byte k, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || k >= Receitas.Todas.Length) return;
            var r = Receitas.Todas[k];
            if (!PodeCriar(r, out _)) return;
            foreach (var g in r.gasta) TirarQuantos(g.item, g.qtd);
            Entregar(r.resultado, r.qtd);
            CriouRpc(k);
        }

        [Rpc(SendTo.Owner)]
        void CriouRpc(byte k) => Feito?.Invoke($"Feito: {Itens.ComNumero(Receitas.Todas[k].resultado, Receitas.Todas[k].qtd)}");

        [Rpc(SendTo.Server)]
        void DesmontarRpc(ushort id, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !PodeDesmontar(id, out var d, out _)) return;
            Achar(id, out var it);
            // a arma desmontada devolve as balas do pente numa caixa
            if (Itens.EhArma(it.item) && it.qtd > 0) Entregar(Itens.CaixaDe(Itens.CalibreDaArma(it.item)), it.qtd);
            TirarUm(id);
            foreach (var v in d.vira) Entregar(v.item, v.qtd);
            DesmontouRpc(d.vira.Length > 0 ? (byte)d.item : (byte)0);
        }

        [Rpc(SendTo.Owner)]
        void DesmontouRpc(byte item)
        {
            var d = Receitas.DesmontagemDe((Item)item);
            if (d != null) Feito?.Invoke($"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(d.item))} virou {Receitas.Lista(d.vira)}");
        }

        [Rpc(SendTo.Server)]
        void LargarRpc(ushort id, Vector3 ponto, float giro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || ItensNoMundo.Instancia == null) return;
            if ((ponto - transform.position).sqrMagnitude > 4f * 4f) ponto = transform.position;
            if (!Achar(id, out var it)) return;
            Tirar(id);
            ItensNoMundo.Instancia.Criar(it.item, ponto, giro, it.qtd, it.desgaste);
        }

        [Rpc(SendTo.Server)]
        void LargarPneuRpc(Vector3 ponto, float giro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || ItensNoMundo.Instancia == null) return;
            if ((ponto - transform.position).sqrMagnitude > 4f * 4f) ponto = transform.position;
            if (TirarPneuDasMaos(out int estado)) ItensNoMundo.Instancia.Criar(Item.Pneu, ponto, giro, estado);
        }

        [Rpc(SendTo.Server)]
        void GastarRpc(Calibre calibre, ushort balas, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            var caixa = Itens.CaixaDe(calibre);
            int falta = balas;
            // das caixas mais vazias primeiro (sobram as cheias)
            while (falta > 0)
            {
                int k = -1;
                for (int i = 0; i < Mochila.Count; i++)
                    if (Mochila[i].item == caixa && (k < 0 || Mochila[i].qtd < Mochila[k].qtd)) k = i;
                if (k < 0) break;
                var it = Mochila[k];
                int n = Mathf.Min(falta, it.qtd);
                falta -= n;
                if (n >= it.qtd) Mochila.RemoveAt(k);
                else
                {
                    it.qtd = (ushort)(it.qtd - n);
                    Mochila[k] = it;
                }
            }
        }

        [Rpc(SendTo.Server)]
        void JuntarRpc(ushort de, ushort para, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !PodeJuntar(de, para)) return;
            var origem = Mochila[Indice(de)];
            int b = Indice(para);
            var destino = Mochila[b];
            int tem = Itens.Quantos(origem.item, origem.qtd), la = Itens.Quantos(destino.item, destino.qtd);
            int n = Mathf.Min(tem, Itens.Pilha(destino.item) - la);
            destino.qtd = (ushort)(la + n);
            Mochila[b] = destino;
            if (n >= tem) Mochila.RemoveAt(Indice(de));
            else
            {
                origem.qtd = (ushort)(tem - n);
                Mochila[Indice(de)] = origem;
            }
        }

        [Rpc(SendTo.Server)]
        void GuardarPenteRpc(ushort id, ushort balas, RpcParams parametros = default)
        {
            int k = Indice(id);
            if (parametros.Receive.SenderClientId != OwnerClientId || k < 0 || !Itens.EhArma(Mochila[k].item)) return;
            var it = Mochila[k];
            it.qtd = (ushort)Mathf.Min(balas, Itens.QuantidadePadrao(it.item));
            Mochila[k] = it;
        }

        // ------------------------------------------------------------------ no dono

        /// <summary>O dono vai pedir para pegar algo: o que chegar logo depois pode ir para a mão.</summary>
        public void AvisarQuePegou() => _pegouAte = Time.time + 2f;

        /// <summary>Move o item para outra posição da grade, girado ou não (o anfitrião confere se cabe).</summary>
        public void Mover(ushort id, int x, int y, bool girado)
        {
            if (x >= 0 && y >= 0 && x < Colunas && y < Linhas) MoverRpc(id, (byte)x, (byte)y, girado);
        }

        /// <summary>Tira a mochila vestida e larga no chão (o que não couber mais na grade cai junto).</summary>
        public void TirarEquipamento(Item qual)
        {
            bool veste = Itens.EhMochila(qual) && MochilaVestida.Value == qual;
            if (!veste || _arma.Usando) return;
            TirarEquipamentoRpc(qual, PontoParaLargar(_rede.Olho), transform.eulerAngles.y + Random.Range(-50f, 50f));
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.5f, false);
        }

        /// <summary>Põe o item num atalho (se ele já estava em outro, sai de lá).</summary>
        public void PorNoAtalho(int lugar, ushort id)
        {
            // a munição e as peças do motor não vão na mão (a recarga tira a munição direto da mochila; as peças vão no capô)
            if (lugar < 0 || lugar >= Atalhos || !Itens.VaiNaMao(Do(id)) || _arma.Usando) return;
            long a = _atalhos.Value;
            for (int i = 0; i < Atalhos; i++)
                if (Ler(a, i) == id) a = Gravar(a, i, 0);
            _atalhos.Value = Gravar(a, lugar, id);
        }

        public void TirarDoAtalho(int lugar)
        {
            if (lugar >= 0 && lugar < Atalhos && !_arma.Usando) _atalhos.Value = Gravar(_atalhos.Value, lugar, 0);
        }

        public void TrocarAtalhos(int de, int para)
        {
            if (de == para || de < 0 || para < 0 || de >= Atalhos || para >= Atalhos || _arma.Usando) return;
            long a = _atalhos.Value;
            ushort x = Ler(a, de), y = Ler(a, para);
            _atalhos.Value = Gravar(Gravar(a, de, y), para, x);
        }

        /// <summary>Larga o pneu das mãos no chão à frente (G).</summary>
        public void LargarPneu()
        {
            if (!CarregandoPneu) return;
            LargarPneuRpc(PontoParaLargar(_rede.Olho), transform.eulerAngles.y + Random.Range(-50f, 50f));
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.7f, false);
        }

        /// <summary>Larga o item: no tampo para onde a mira aponta, se estiver perto, senão no chão à frente.</summary>
        public void Largar(ushort id)
        {
            if (Do(id) == Item.Nada || (_arma.Usando && id == IdNaMao)) return;
            if (Itens.EhArma(Do(id))) GuardarPente(id, _arma.MunicaoDe(id, Do(id)));   // a arma leva o pente para o chão
            LargarRpc(id, PontoParaLargar(_rede.Olho), transform.eulerAngles.y + Random.Range(-50f, 50f));
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.5f, false);
        }

        readonly Dictionary<ushort, float> _desgastePendente = new Dictionary<ushort, float>();
        string _avisoNoHud;
        float _avisoNoHudAte;

        /// <summary>
        /// O dono usou a arma (um tiro, um golpe que pegou): o desgaste sobe. O anfitrião recebe de 1 em 1 (o resto fica
        /// guardado aqui).
        /// </summary>
        public void Desgastar(ushort id, float quanto)
        {
            if (id == 0 || quanto <= 0f || !Itens.ComDesgaste(Do(id))) return;
            _desgastePendente.TryGetValue(id, out float p);
            p += quanto;
            if (p >= 1f)
            {
                int n = Mathf.Min(255, (int)p);
                p -= n;
                DesgastarRpc(id, (byte)n);
            }
            _desgastePendente[id] = p;
        }

        /// <summary>O desgaste do item da mochila (0, novo; 100, quebrado).</summary>
        public int DesgasteDe(ushort id) => Achar(id, out var it) ? it.desgaste : 0;

        /// <summary>A arma pode ser consertada agora: gasta e há um kit de reparo na mochila.</summary>
        public bool PodeReparar(ushort id) => Achar(id, out var it) && Itens.ComDesgaste(it.item) && it.desgaste > 0 && Quantos(Item.KitDeReparo) > 0;

        /// <summary>Conserta a arma com um kit de reparo (o anfitrião confere e gasta o kit).</summary>
        public void Reparar(ushort id)
        {
            if (!PodeReparar(id)) return;
            _desgastePendente.Remove(id);
            RepararRpc(id);
            Efeitos.Som(Efeitos.Recarga, transform.position + Vector3.up * 1.2f, 0.6f, false);
        }

        /// <summary>Larga um só da pilha, no mesmo lugar do <see cref="Largar"/>.</summary>
        public void LargarUm(ushort id)
        {
            if (!Itens.EmPilha(Do(id)) || (_arma.Usando && id == IdNaMao)) return;
            LargarUmRpc(id, PontoParaLargar(_rede.Olho), transform.eulerAngles.y + Random.Range(-50f, 50f));
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.5f, false);
        }

        /// <summary>
        /// Come, bebe ou usa um da pilha direto da mochila (o menu do botão direito da tela): na hora, com o som de abrir e
        /// de uma mordida ou de um gole (pela mão, o clique faz o clipe inteiro).
        /// </summary>
        public void UsarDaMochila(ushort id)
        {
            var item = Do(id);
            if (!Itens.EhConsumivel(item) || _arma.Usando) return;
            bool bebe = Itens.Consumivel(item).tipo == TipoDeLoot.Bebida;
            var ouvido = _rede.cameraMundo != null ? _rede.cameraMundo.transform.position : transform.position + Vector3.up * 1.6f;
            Efeitos.Som(bebe || Itens.EhRemedio(item) ? Efeitos.AbrirTampa : Efeitos.AbrirLata, ouvido, 0.45f, false);
            Efeitos.Som(bebe ? Efeitos.Gole : Efeitos.Mastigar, ouvido, 0.5f, false);
            ConsumirRpc(id);
        }

        /// <summary>Faz a receita (o índice em <see cref="Receitas.Todas"/>; o anfitrião confere).</summary>
        public void Criar(int receita)
        {
            if (receita < 0 || receita >= Receitas.Todas.Length || !PodeCriar(Receitas.Todas[receita], out _)) return;
            CriarRpc((byte)receita);
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.6f, false);
        }

        /// <summary>Desmonta um item da mochila (um da pilha; o anfitrião confere a ferramenta).</summary>
        public void Desmontar(ushort id)
        {
            if (!PodeDesmontar(id, out _, out _) || (_arma.Usando && id == IdNaMao)) return;
            if (Itens.EhArma(Do(id))) GuardarPente(id, _arma.MunicaoDe(id, Do(id)));   // as balas do pente voltam numa caixa
            DesmontarRpc(id);
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.6f, false);
        }

        /// <summary>
        /// O dono terminou de comer ou beber o item: um da pilha acaba, e a fome e a sede caem. Se era o último, a mão
        /// volta para o atalho da última arma (se ela ainda estiver num).
        /// </summary>
        public void Consumir(ushort id, ushort voltarPara)
        {
            _consumindo = id;
            _voltarPara = voltarPara;
            ConsumirRpc(id);
        }

        void AoMudarMochila(NetworkListEvent<ItemNaMochila> e)
        {
            // o anfitrião tirou as balas gastas das caixas: a conta já não precisa descontar
            if (Itens.EhMunicao(e.Value.item) && e.Type != NetworkListEvent<ItemNaMochila>.EventType.Add) _pendenteAte = 0f;
            if (IsOwner)
            {
                long a = _atalhos.Value;
                int escolher = -1;
                // o atalho de um item que saiu da mochila fica vazio; se era o que acabou de ser comido, a mão volta
                // para a última arma
                for (int i = 0; i < Atalhos; i++)
                {
                    ushort id = Ler(a, i);
                    if (id == 0 || Do(id) != Item.Nada) continue;
                    a = Gravar(a, i, 0);
                    if (id == _consumindo && i == Escolhido.Value)
                    {
                        for (int k = 0; k < Atalhos; k++)
                            if (_voltarPara != 0 && Ler(a, k) == _voltarPara) escolher = k;
                        _consumindo = 0;
                    }
                }
                // o que o jogador acabou de pegar: com a mão vazia, vai para a mão; uma arma vai para um atalho livre
                // (e para a mão)
                if (e.Type == NetworkListEvent<ItemNaMochila>.EventType.Add && Time.time < _pegouAte && Itens.VaiNaMao(e.Value.item))
                {
                    _pegouAte = 0f;
                    int lugar = -1;
                    if (Ler(a, Escolhido.Value) == 0) lugar = Escolhido.Value;
                    else if (Itens.EhArma(e.Value.item))
                        for (int i = Atalhos - 1; i >= 0; i--)
                            if (Ler(a, i) == 0) lugar = i;
                    if (lugar >= 0)
                    {
                        a = Gravar(a, lugar, e.Value.id);
                        escolher = lugar;
                    }
                }
                if (a != _atalhos.Value) _atalhos.Value = a;
                if (escolher >= 0 && escolher != Escolhido.Value) Escolhido.Value = (byte)escolher;
            }
            Mudou?.Invoke();
        }

        void AoMudarAtalhos(long antes, long depois) => Mudou?.Invoke();

        void AoMudarEscolhido(byte antes, byte depois)
        {
            _nomeAte = Time.time + 1.6f;
            Mudou?.Invoke();
        }

        // ------------------------------------------------------------------ a lanterna e as pilhas (01/11/2026)

        /// <summary>Uma lanterna com pilhas novas dura isto aceso (s): mais ou menos uma noite.</summary>
        public const float DuracaoDasPilhas = 480f;
        float _gastoDaLanterna;
        bool _lanternaDoComeco;

        /// <summary>A melhor lanterna da mochila: a carga dela (0 a 100; sem lanterna, -1).</summary>
        public int CargaDaLanterna
        {
            get
            {
                int melhor = -1;
                if (Mochila == null) return melhor;
                foreach (var it in Mochila)
                    if (it.item == Item.Lanterna) melhor = Mathf.Max(melhor, 100 - it.desgaste);
                return melhor;
            }
        }

        int IndiceDaLanternaEmUso()
        {
            int k = -1, melhor = -1;
            for (int i = 0; i < Mochila.Count; i++)
                if (Mochila[i].item == Item.Lanterna && 100 - Mochila[i].desgaste > melhor)
                {
                    melhor = 100 - Mochila[i].desgaste;
                    k = i;
                }
            return k;
        }

        /// <summary>Só no anfitrião: a gosma do Long queimou as pilhas (todas as lanternas da mochila).</summary>
        public void EsvaziarLanterna()
        {
            if (!IsServer) return;
            bool havia = false;
            for (int i = 0; i < Mochila.Count; i++)
            {
                var it = Mochila[i];
                if (it.item != Item.Lanterna || it.desgaste >= 100) continue;
                it.desgaste = 100;
                Mochila[i] = it;
                havia = true;
            }
            if (havia) AvisoRpc("A gosma apagou a sua lanterna: troque as pilhas");
        }

        /// <summary>Só no anfitrião: a gosma apagou a tocha da mão (ela se perde).</summary>
        public void ApagarTochaNaMao()
        {
            if (!IsServer || NaMao != Item.Tocha) return;
            int k = Indice(IdNaMao);
            if (k < 0) return;
            Mochila.RemoveAt(k);
            AvisoRpc("A gosma apagou a tocha");
        }

        /// <summary>A lanterna pode ganhar pilhas novas (tem um par na mochila e ela não está cheia).</summary>
        public bool PodeTrocarPilhas(ushort id) => Achar(id, out var it) && it.item == Item.Lanterna && it.desgaste > 0 && Quantos(Item.Pilhas) > 0;

        public void TrocarPilhas(ushort id)
        {
            if (!PodeTrocarPilhas(id)) return;
            TrocarPilhasRpc(id);
            Efeitos.Som(Efeitos.Lanterna, transform.position + Vector3.up * 1.2f, 0.7f, false);
        }

        [Rpc(SendTo.Server)]
        void TrocarPilhasRpc(ushort id, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !PodeTrocarPilhas(id)) return;
            if (!GastarItem(Item.Pilhas, 1)) return;
            int k = Indice(id);
            if (k < 0) return;
            var it = Mochila[k];
            it.desgaste = 0;
            Mochila[k] = it;
            AvisoRpc("Pilhas novas na lanterna");
        }

        /// <summary>No anfitrião: a lanterna acesa gasta as pilhas (a de mais carga da mochila).</summary>
        void GastarPilhas(float dt)
        {
            var lanterna = _rede != null ? _rede.Lanterna : null;
            if (lanterna == null || !lanterna.Ligada.Value) return;
            _gastoDaLanterna += dt * 100f / DuracaoDasPilhas;
            if (_gastoDaLanterna < 1f) return;
            int n = Mathf.FloorToInt(_gastoDaLanterna);
            _gastoDaLanterna -= n;
            int k = IndiceDaLanternaEmUso();
            if (k < 0) return;
            var it = Mochila[k];
            if (it.desgaste >= 100) return;
            it.desgaste = (byte)Mathf.Min(100, it.desgaste + n);
            Mochila[k] = it;
            if (it.desgaste >= 100) AvisoRpc("As pilhas da lanterna acabaram");
        }

        /// <summary>
        /// No anfitrião, no começo da partida: uma lanterna e um par de pilhas no chão, perto de onde o jogador nasceu (por
        /// enquanto o saque não está nos móveis: o Gustavo põe depois).
        /// </summary>
        System.Collections.IEnumerator LanternaDoComeco()
        {
            float ate = Time.time + 60f;
            while (Time.time < ate && (Loot.ItensNoMundo.Instancia == null || !Loot.ItensNoMundo.Instancia.IsSpawned)) yield return null;
            yield return new WaitForSeconds(4f);
            var mundo = Loot.ItensNoMundo.Instancia;
            if (mundo == null) yield break;
            Vector3 frente = transform.forward;
            frente.y = 0f;
            frente = frente.sqrMagnitude > 0.01f ? frente.normalized : Vector3.forward;
            Vector3 lado = Vector3.Cross(Vector3.up, frente);
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = transform.position + frente * 1.1f + lado * (i == 0 ? -0.25f : 0.25f) + Vector3.up * 1f;
                if (Physics.Raycast(p, Vector3.down, out var hit, 3f, ~((1 << JogadorRede.CamadaJogador) | (1 << 2)), QueryTriggerInteraction.Ignore)) p = hit.point;
                else p -= Vector3.up;
                mundo.Criar(i == 0 ? Item.Lanterna : Item.Pilhas, p, transform.eulerAngles.y + 90f, i == 0 ? 1 : 2);
            }
        }

        // ------------------------------------------------------------------ o galão e o remendo do pneu (01/11/2026)

        /// <summary>A gasolina de um galão (litros; o desgaste é o quanto ele está vazio: 0 cheio, 100 vazio).</summary>
        public static float LitrosDe(ItemNaMochila it) =>
            it.item == Item.Galao ? Itens.LitrosDoGalao * (100 - Mathf.Clamp(it.desgaste, 0, 100)) / 100f : 0f;

        public float LitrosNoGalao(ushort id) => Achar(id, out var it) ? LitrosDe(it) : 0f;

        /// <summary>No anfitrião: tira gasolina do galão (o carro abastecendo, Carro.Desgaste.cs). O galão vazio fica.</summary>
        internal void TirarDoGalao(ushort id, float litros)
        {
            if (!IsServer) return;
            int k = Indice(id);
            if (k < 0 || Mochila[k].item != Item.Galao) return;
            var it = Mochila[k];
            float sobra = Mathf.Max(0f, LitrosDe(it) - litros);
            it.desgaste = (byte)Mathf.Clamp(Mathf.RoundToInt(100f - sobra / Itens.LitrosDoGalao * 100f), 0, 100);
            Mochila[k] = it;
            AvisoRpc(sobra < 0.05f ? $"Abasteceu {litros:0.#} L: o galão ficou vazio" : $"Abasteceu {litros:0.#} L: sobrou {sobra:0.#} L no galão");
        }

        /// <summary>
        /// O pneu furado (de 1 a 9%, fora do carro) tem remendo, com um kit de reparo; o rasgado (0%), não. O Id 0 é o pneu
        /// das mãos (sem mochila, o pneu vai para as mãos: R remenda).
        /// </summary>
        public bool PodeRemendar(ushort id)
        {
            if (Quantos(Item.KitDeReparo) <= 0) return false;
            if (id == 0) return CarregandoPneu && PneuNasMaos.Value > 0 && PneuNasMaos.Value < Vehicles.PneusDoCarro.Minimo;
            return Achar(id, out var it) && it.item == Item.Pneu && it.qtd > 0 && it.qtd < Vehicles.PneusDoCarro.Minimo;
        }

        /// <summary>O pneu das mãos está furado (com ou sem kit de reparo): o HUD mostra o R.</summary>
        public bool PneuDasMaosFurado => CarregandoPneu && PneuNasMaos.Value > 0 && PneuNasMaos.Value < Vehicles.PneusDoCarro.Minimo;

        public void Remendar(ushort id)
        {
            if (!PodeRemendar(id)) return;
            RemendarRpc(id);
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 1.1f, 0.6f, false);
        }

        [Rpc(SendTo.Server)]
        void RemendarRpc(ushort id, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !PodeRemendar(id)) return;
            if (!GastarItem(Item.KitDeReparo, 1)) return;
            if (id == 0)
            {
                PneuNasMaos.Value = Vehicles.PneusDoCarro.Remendo;
                AvisoRpc($"Pneu remendado: {Vehicles.PneusDoCarro.Remendo}%");
                return;
            }
            int k = Indice(id);
            if (k < 0) return;
            var it = Mochila[k];
            it.qtd = Vehicles.PneusDoCarro.Remendo;
            Mochila[k] = it;
            AvisoRpc($"Pneu remendado: {Vehicles.PneusDoCarro.Remendo}%");
        }

        /// <summary>Um aviso no HUD de quem joga nesta máquina.</summary>
        public static void AvisoLocal(string texto)
        {
            var inv = JogadorRede.Local != null ? JogadorRede.Local.Inventario : null;
            if (inv == null) return;
            inv.Feito?.Invoke(texto);
            inv._avisoNoHud = texto;
            inv._avisoNoHudAte = Time.time + 2.8f;
        }

        void Update()
        {
            if (IsSpawned && IsServer)
            {
                GastarPilhas(Time.deltaTime);
                if (!_lanternaDoComeco && !ModoVideo.SemArma)
                {
                    _lanternaDoComeco = true;
                    StartCoroutine(LanternaDoComeco());
                }
            }
            if (IsSpawned) PneuQueSeVe();
            if (!IsSpawned || !IsOwner || MenuOnline.Bot || !MenuOnline.Controla) return;
            if (_arma.Usando || _arma.Montando) return;   // comendo, bebendo ou montando: nem troca nem larga
            var teclado = Keyboard.current;
            var mouse = Mouse.current;
            int escolher = -1;
            if (teclado != null)
            {
                if (teclado.digit1Key.wasPressedThisFrame) escolher = 0;
                else if (teclado.digit2Key.wasPressedThisFrame) escolher = 1;
                else if (teclado.digit3Key.wasPressedThisFrame) escolher = 2;
            }
            if (mouse != null)
            {
                float roda = mouse.scroll.ReadValue().y;
                if (roda != 0f) escolher = Proximo(roda < 0f ? 1 : -1);
            }
            if (escolher >= 0 && escolher != Escolhido.Value) Escolhido.Value = (byte)escolher;
            // dentro do carro, não larga nada; com o pneu nas mãos, G larga o pneu
            if (teclado != null && teclado.gKey.wasPressedThisFrame && !(_rede.Passageiro != null && _rede.Passageiro.NoCarro))
            {
                if (CarregandoPneu) LargarPneu();
                else Largar(IdNaMao);
            }
            // com o pneu furado nas mãos, R remenda (com um kit de reparo; a arma está guardada)
            if (teclado != null && teclado.rKey.wasPressedThisFrame && PneuDasMaosFurado)
            {
                if (PodeRemendar(0)) Remendar(0);
                else AvisoLocal("Para remendar o pneu, precisa de um kit de reparo");
            }
        }

        // ------------------------------------------------------------------ o pneu nas mãos, o que se vê

        GameObject _pneuNoCorpo, _pneuNoOlho;

        /// <summary>O pneu nas mãos do corpo (o dono vê como o corpo: só a sombra na primeira pessoa, inteiro na terceira).</summary>
        public Renderer PneuNoCorpo => _pneuNoCorpo != null ? _pneuNoCorpo.GetComponent<MeshRenderer>() : null;

        // em todas as cópias: o pneu em pé na frente do peito (o dono vê só a sombra dele), e, só no dono, o mesmo pneu
        // na frente da câmera, embaixo, na camada dos braços. O que estava na mão do corpo some
        void PneuQueSeVe()
        {
            bool carrega = CarregandoPneu && !(_rede.Passageiro != null && _rede.Passageiro.NoCarro);
            if (carrega && _pneuNoCorpo == null && !CriarPneus()) return;
            if (_pneuNoCorpo == null) return;
            if (_pneuNoCorpo.activeSelf != carrega)
            {
                _pneuNoCorpo.SetActive(carrega);
                if (_rede.Passageiro == null || !_rede.Passageiro.NoCarro) _rede.EsconderNaMao(carrega);
            }
            if (_pneuNoOlho != null && _pneuNoOlho.activeSelf != carrega) _pneuNoOlho.SetActive(carrega);
        }

        bool CriarPneus()
        {
            var mundo = ItensNoMundo.Instancia;
            int pc = Itens.IndiceDaPeca(Item.Pneu);
            if (mundo == null || mundo.malhasDePeca == null || pc < 0 || pc >= mundo.malhasDePeca.Length || mundo.malhasDePeca[pc] == null) return false;
            var malha = mundo.malhasDePeca[pc];
            // a malha é o pneu deitado (o eixo em +Y, a origem embaixo, no meio): de pé, com o eixo para a frente
            var emPe = Quaternion.Euler(90f, 0f, 0f);
            _pneuNoCorpo = Peca("Pneu nas mãos", transform, new Vector3(0f, 0.98f, 0.30f), emPe, malha, mundo.material, gameObject.layer);
            var r = _pneuNoCorpo.GetComponent<MeshRenderer>();
            if (IsOwner && !_rede.CorpoVisivelNoDono)
            {
                // como o corpo do dono: só a sombra, fora da sombra da lanterna dele
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                r.renderingLayerMask = JogadorRede.MascaraDoDono;
            }
            if (IsOwner)
            {
                if (_rede.cameraMundo != null)
                {
                    _pneuNoOlho = Peca("Pneu nas mãos (primeira pessoa)", _rede.cameraMundo.transform, new Vector3(0f, -0.50f, 0.48f),
                        Quaternion.Euler(72f, 0f, 0f), malha, mundo.material, JogadorRede.CamadaBracos);
                    _pneuNoOlho.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            return true;
        }

        static GameObject Peca(string nome, Transform pai, Vector3 pos, Quaternion giro, Mesh malha, Material material, int camada)
        {
            var go = new GameObject(nome) { layer = camada };
            go.transform.SetParent(pai, false);
            go.transform.SetLocalPositionAndRotation(pos, giro);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.SetActive(false);
            return go;
        }

        // o próximo atalho naquele sentido, vazio ou não (um vazio deixa a mão vazia)
        int Proximo(int sentido) => ((Escolhido.Value + sentido) % Atalhos + Atalhos) % Atalhos;

        // olhando para um tampo perto (uma mesa, um balcão, o chão logo ali): ali mesmo. Senão, no chão à frente, sem
        // atravessar a parede
        Vector3 PontoParaLargar(Transform cam)
        {
            if (Physics.Raycast(cam.position, cam.forward, out var acerto, 2.2f, MascaraDoCenario, QueryTriggerInteraction.Ignore) && acerto.normal.y > 0.7f)
                return acerto.point;
            Vector3 peito = transform.position + Vector3.up;
            float d = Physics.Raycast(peito, transform.forward, out var parede, 0.85f, MascaraDoCenario, QueryTriggerInteraction.Ignore)
                ? Mathf.Max(0f, parede.distance - 0.25f) : 0.6f;
            if (Physics.Raycast(peito + transform.forward * d, Vector3.down, out var chao, 3f, MascaraDoCenario, QueryTriggerInteraction.Ignore))
                return chao.point;
            return transform.position;
        }

        /// <summary>O ícone colorido do item (null, nada).</summary>
        public Texture2D IconeDe(Item item)
        {
            int s = Itens.IndiceDaSinuca(item);
            if (s >= 0) return iconesDaSinuca != null && s < iconesDaSinuca.Length ? iconesDaSinuca[s] : null;
            int l = Itens.IndiceDoLoot(item);
            if (l >= 0) return iconesDeLoot != null && l < iconesDeLoot.Length ? iconesDeLoot[l] : null;
            int p = Itens.IndiceDaPeca(item);
            if (p >= 0) return iconesDePeca != null && p < iconesDePeca.Length ? iconesDePeca[p] : null;
            int c = Itens.IndiceDaMunicao(item);
            if (c >= 0) return iconesDeMunicao != null && c < iconesDeMunicao.Length ? iconesDeMunicao[c] : null;
            int e = Itens.Equipamento(item);
            if (e >= 0) return iconesDeEquipamento != null && e < iconesDeEquipamento.Length ? iconesDeEquipamento[e] : null;
            int m = Itens.Modelo(item);
            return m >= 0 && icones != null && m < icones.Length ? icones[m] : null;
        }

        public Texture2D FotoDe(Item item)
        {
            int s = Itens.IndiceDaSinuca(item);
            if (s >= 0) return fotosDaSinuca != null && s < fotosDaSinuca.Length ? fotosDaSinuca[s] : null;
            int l = Itens.IndiceDoLoot(item);
            if (l >= 0) return fotosDeLoot != null && l < fotosDeLoot.Length ? fotosDeLoot[l] : null;
            int p = Itens.IndiceDaPeca(item);
            if (p >= 0) return fotosDePeca != null && p < fotosDePeca.Length ? fotosDePeca[p] : null;
            int c = Itens.IndiceDaMunicao(item);
            if (c >= 0) return fotosDeMunicao != null && c < fotosDeMunicao.Length ? fotosDeMunicao[c] : null;
            int e = Itens.Equipamento(item);
            if (e >= 0) return fotosDeEquipamento != null && e < fotosDeEquipamento.Length ? fotosDeEquipamento[e] : null;
            int m = Itens.Modelo(item);
            return m >= 0 && fotos != null && m < fotos.Length ? fotos[m] : null;
        }

        // a barra de atalhos, embaixo, no meio (com a tela do inventário aberta, ela fica lá dentro)
        void OnGUI()
        {
            if (!IsSpawned || !IsOwner || MenuOnline.Aberto || MenuOnline.Cinema || ModoVideo.Ligado || (_tela != null && _tela.Aberta)) return;
            float u = Screen.height / 720f;
            float w = 112 * u, h = 56 * u, vao = 8 * u;
            float x0 = (Screen.width - (w * Atalhos + vao * (Atalhos - 1))) / 2f, y = Screen.height - h - 16 * u;
            var numero = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * u), fontStyle = FontStyle.Bold };
            for (int i = 0; i < Atalhos; i++)
            {
                float x = x0 + i * (w + vao);
                bool escolhido = i == Escolhido.Value;
                // o fundo claro: as armas do ícone são escuras
                Retangulo(x, y, w, h, escolhido ? new Color(0.5f, 0.5f, 0.47f, 0.82f) : new Color(0.36f, 0.36f, 0.34f, 0.6f));
                if (escolhido)
                {
                    float b = Mathf.Max(1f, Mathf.Round(2 * u));
                    var cor = new Color(0.9f, 0.86f, 0.72f, 0.9f);
                    Retangulo(x, y, w, b, cor); Retangulo(x, y + h - b, w, b, cor);
                    Retangulo(x, y, b, h, cor); Retangulo(x + w - b, y, b, h, cor);
                }
                var icone = IconeDe(this[i]);
                if (icone != null)
                {
                    var antes = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, escolhido ? 1f : 0.8f);
                    GUI.DrawTexture(new Rect(x + 6 * u, y + 5 * u, w - 12 * u, h - 10 * u), icone, ScaleMode.ScaleToFit);
                    GUI.color = antes;
                }
                numero.normal.textColor = escolhido ? new Color(0.95f, 0.92f, 0.8f) : new Color(0.7f, 0.68f, 0.62f, 0.8f);
                GUI.Label(new Rect(x + 5 * u, y + 2 * u, 20 * u, 18 * u), (i + 1).ToString(), numero);
            }
            // o aviso (a arma quebrou, foi consertada), em cima da barra
            if (_avisoNoHud != null && Time.time < _avisoNoHudAte)
            {
                var aviso = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16 * u), alignment = TextAnchor.LowerCenter, fontStyle = FontStyle.Bold };
                var ra = new Rect(0f, y - 60 * u, Screen.width, 26 * u);
                aviso.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
                GUI.Label(new Rect(ra.x + 1.5f * u, ra.y + 1.5f * u, ra.width, ra.height), _avisoNoHud, aviso);
                aviso.normal.textColor = new Color(1f, 0.78f, 0.45f);
                GUI.Label(ra, _avisoNoHud, aviso);
            }
            // o nome do que está na mão, logo depois de trocar; e, com comida ou água, como usar
            var texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u), alignment = TextAnchor.LowerCenter };
            var naMao = NaMao;
            string linha = null;
            float alfa = 1f;
            if (Itens.EhConsumivel(naMao))
            {
                int n = Achar(IdNaMao, out var pilha) ? Itens.Quantos(naMao, pilha.qtd) : 1;
                linha = $"{Itens.Nome(naMao)}{(n > 1 ? $" ({n})" : "")}   ·   clique: {Itens.Consumivel(naMao).verbo}   ·   G: largar";
            }
            else if (Itens.EhTaco(naMao)) linha = $"{Itens.Nome(naMao)}   ·   E na mesa de sinuca: jogar   ·   G: largar";
            else if (Time.time < _nomeAte)
            {
                linha = naMao == Item.Nada ? "mãos vazias   ·   clique: soco"
                    : Itens.EhBranca(naMao) ? $"{Itens.Nome(naMao)}   ·   clique: golpear   ·   G: largar" : Itens.Nome(naMao);
                alfa = Mathf.Clamp01((_nomeAte - Time.time) / 0.4f);
            }
            if (linha != null)
            {
                var r = new Rect(0f, y - 30 * u, Screen.width, 26 * u);
                texto.normal.textColor = new Color(0f, 0f, 0f, 0.7f * alfa);
                GUI.Label(new Rect(r.x + 1.5f * u, r.y + 1.5f * u, r.width, r.height), linha, texto);
                texto.normal.textColor = new Color(0.92f, 0.9f, 0.82f, alfa);
                GUI.Label(r, linha, texto);
            }
        }

        static void Retangulo(float x, float y, float w, float h, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }
}
