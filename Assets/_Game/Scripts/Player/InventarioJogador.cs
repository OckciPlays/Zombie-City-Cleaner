using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using Random = UnityEngine.Random;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Um item na mochila: o Id (único na mochila), o que é, a célula de cima, à esquerda, que ele ocupa e se está girado
    /// (90°: a largura e a altura trocam). A arma da alça (<see cref="naAlca"/>) não ocupa a grade.
    /// <see cref="qtd"/>: as balas da caixa de munição, ou as do pente da arma (a cópia do anfitrião: o dono manda quando a
    /// arma sai da mão, é largada ou guardada).
    /// </summary>
    public struct ItemNaMochila : INetworkSerializeByMemcpy, IEquatable<ItemNaMochila>
    {
        public ushort id;
        public Item item;
        public byte x, y;
        public bool girado;
        /// <summary>A arma pendurada pela alça, no ombro: fora da grade.</summary>
        public bool naAlca;
        /// <summary>As balas: na caixa de munição, ou no pente da arma.</summary>
        public ushort qtd;

        /// <summary>As células que ele ocupa, com o giro.</summary>
        public Vector2Int Tamanho => InventarioJogador.TamanhoNaGrade(item, girado);

        public bool Equals(ItemNaMochila o) => id == o.id && item == o.item && x == o.x && y == o.y && girado == o.girado && naAlca == o.naAlca && qtd == o.qtd;
    }

    /// <summary>
    /// O inventário do jogador:
    /// - o que ele veste (<see cref="MochilaVestida"/> e <see cref="ComColdre"/>, escritos pelo anfitrião). Sem mochila,
    ///   só os bolsos;
    /// - a grade: os bolsos (4 × 2) ou a mochila vestida (pequena 6 × 4, média 8 × 5, grande 10 × 7;
    ///   <see cref="Itens.EspacoDa"/>), e cada item ocupa as células dele (<see cref="Itens.Tamanho"/>: a sniper 7 × 2, a
    ///   lata 1), deitado ou girado 90° (a tela gira com R). Sem pilhas. O que é pego entra na primeira posição em que
    ///   couber (de cima para baixo, da esquerda para a direita; se não couber deitado, girado); sem espaço, não entra.
    ///   Trocar ou tirar a mochila arruma a grade de novo, e o que não cabe mais cai no chão;
    /// - as armas: a primeira vai pela alça (<see cref="ItemNaMochila.naAlca"/>, de qualquer tamanho, sem gastar a
    ///   grade). As outras vão na grade, e cada uma precisa de um lugar no corpo: a mochila prende uma nas costas (longa ou
    ///   curta) e o coldre, uma curta (a pistola ou a submetralhadora). Sem os dois, só a da alça (<see cref="ArmaCabeNaGrade"/>);
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

        /// <summary>
        /// A mochila (a grade e a arma da alça; escrita pelo anfitrião). O jogador nasce sem mochila, com a pistola na alça,
        /// no primeiro atalho.
        /// </summary>
        public NetworkList<ItemNaMochila> Mochila { get; private set; }
        /// <summary>A mochila que ele veste (Nada: só os bolsos). Escrita pelo anfitrião.</summary>
        public readonly NetworkVariable<Item> MochilaVestida = new(Item.Nada, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>Se ele usa o coldre (leva mais uma arma curta). Escrito pelo anfitrião.</summary>
        public readonly NetworkVariable<bool> ComColdre = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>O tamanho da grade agora: os bolsos ou a mochila vestida.</summary>
        public Vector2Int TamanhoDaGrade => Itens.EspacoDa(MochilaVestida.Value);
        public int Colunas => TamanhoDaGrade.x;
        public int Linhas => TamanhoDaGrade.y;
        /// <summary>Quantas armas ele pode levar: a da alça, mais uma nas costas (com mochila) e uma no coldre.</summary>
        public int VagasDeArma => 1 + (MochilaVestida.Value != Item.Nada ? 1 : 0) + (ComColdre.Value ? 1 : 0);
        /// <summary>Quantas armas ele leva agora (na alça e na grade).</summary>
        public int ArmasLevadas
        {
            get
            {
                int n = 0;
                foreach (var it in Mochila) if (Itens.EhArma(it.item)) n++;
                return n;
            }
        }
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
            ComColdre.OnValueChanged += AoMudarColdre;
            if (IsServer)
            {
                Mochila.Clear();
                MochilaVestida.Value = Item.Nada;
                ComColdre.Value = false;
                Receber(Item.Pistola);
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
            ComColdre.OnValueChanged -= AoMudarColdre;
        }

        void AoMudarEquipamento(Item antes, Item depois) => Mudou?.Invoke();
        void AoMudarColdre(bool antes, bool depois) => Mudou?.Invoke();

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
                if (o.id == ignorar || o.naAlca) continue;
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
        /// A arma pode ir para a grade: cada arma da grade precisa de um lugar no corpo. A mochila prende uma nas costas
        /// (longa ou curta), e o coldre, uma curta. (A da alça não conta.)
        /// </summary>
        static bool ArmaCabeEm(List<ItemNaMochila> lista, bool mochila, bool coldre, Item arma, ushort ignorar)
        {
            int m = mochila ? 1 : 0, c = coldre ? 1 : 0, todas = 0, longas = 0;
            foreach (var o in lista)
            {
                if (o.id == ignorar || o.naAlca || !Itens.EhArma(o.item)) continue;
                todas++;
                if (!Itens.EhCurta(o.item)) longas++;
            }
            if (todas >= m + c) return false;
            return Itens.EhCurta(arma) || longas < m;
        }

        /// <summary>
        /// O item cabe (girado ou não) com o canto de cima, à esquerda, nesta célula, sem contar o item 'ignorar' (o que
        /// se move). A arma da alça não ocupa a grade.
        /// </summary>
        public bool Cabe(Item item, bool girado, int x, int y, ushort ignorar = 0) => CabeEm(Copia(), TamanhoDaGrade, item, girado, x, y, ignorar);

        /// <summary>
        /// A primeira posição em que o item cabe (de cima para baixo, da esquerda para a direita), deitado; se não houver,
        /// girado.
        /// </summary>
        public bool Espaco(Item item, out int x, out int y, out bool girado) => EspacoEm(Copia(), TamanhoDaGrade, item, out x, out y, out girado);

        public bool TemEspaco(Item item) => Espaco(item, out _, out _, out _);

        /// <summary>A alça está livre (sem contar o item 'ignorar').</summary>
        public bool AlcaLivre(ushort ignorar = 0)
        {
            foreach (var it in Mochila)
                if (it.naAlca && it.id != ignorar) return false;
            return true;
        }

        /// <summary>A arma da alça (false, a alça vazia).</summary>
        public bool NaAlca(out ItemNaMochila arma)
        {
            foreach (var it in Mochila)
                if (it.naAlca)
                {
                    arma = it;
                    return true;
                }
            arma = default;
            return false;
        }

        /// <summary>A arma pode ir para a grade (há lugar no corpo para ela: as costas da mochila ou o coldre).</summary>
        public bool ArmaCabeNaGrade(Item arma, ushort ignorar = 0) =>
            ArmaCabeEm(Copia(), MochilaVestida.Value != Item.Nada, ComColdre.Value, arma, ignorar);

        /// <summary>O item pode ir para esta posição da grade (se saiu da alça, a arma precisa de lugar no corpo).</summary>
        public bool PodeMover(ushort id, int x, int y, bool girado)
        {
            if (!Achar(id, out var it) || !Cabe(it.item, girado, x, y, id)) return false;
            return !it.naAlca || !Itens.EhArma(it.item) || ArmaCabeNaGrade(it.item, id);
        }

        /// <summary>O item pode ir para a alça (uma arma, e a alça livre).</summary>
        public bool PodeIrParaAlca(ushort id) => Achar(id, out var it) && !it.naAlca && Itens.EhArma(it.item) && AlcaLivre(id);

        /// <summary>
        /// O jogador pode pegar o item agora? Se não, o motivo (para a dica). A arma vai para a alça, se ela estiver
        /// livre; senão, para a grade, se houver lugar no corpo e espaço. A mochila nova troca a vestida (que cai no chão).
        /// </summary>
        public bool PodeReceber(Item item, out string motivo)
        {
            motivo = null;
            if (item == Item.Nada) return false;
            bool mochila = MochilaVestida.Value != Item.Nada, coldre = ComColdre.Value;
            if (Itens.EhEquipamento(item))
            {
                if (item == Item.Coldre && coldre)
                {
                    motivo = "Você já usa um coldre";
                    return false;
                }
                return true;
            }
            if (Itens.EhArma(item))
            {
                if (AlcaLivre()) return true;
                if (!ArmaCabeNaGrade(item))
                {
                    bool curta = Itens.EhCurta(item);
                    motivo = !mochila && !coldre ? (curta ? "Para levar outra arma, ache uma mochila ou um coldre" : "Para levar outra arma, ache uma mochila")
                        : !curta && !mochila ? "Arma longa vai nas costas: ache uma mochila"
                        : "Sem lugar no corpo para outra arma";
                    return false;
                }
            }
            // a caixa de munição completa primeiro as do mesmo calibre
            if (Itens.EhMunicao(item) && FaltaNasCaixas(Itens.CalibreDa(item)) > 0) return true;
            if (TemEspaco(item)) return true;
            motivo = $"Sem espaço {(mochila ? "na mochila" : "nos bolsos")} para {Itens.NomeComArtigo(item)}";
            return false;
        }

        /// <summary>
        /// A dica de pegar o item ("[E] Pegar a lata de comida", "[E] Pegar a caixa de 9mm (24 balas)"), ou o motivo de não
        /// poder. <paramref name="qtd"/>: as balas da caixa ou do pente da arma (-1, não mostra).
        /// </summary>
        public string DicaDePegar(Item item, int qtd = -1)
        {
            if (!PodeReceber(item, out var motivo)) return motivo;
            if (Itens.EhMochila(item) && MochilaVestida.Value != Item.Nada)
                return $"[E] Trocar a sua {Itens.Nome(MochilaVestida.Value)} pela {Itens.Nome(item)}";
            string balas = qtd < 0 ? "" : Itens.EhMunicao(item) ? $" ({qtd} {(qtd == 1 ? "bala" : "balas")})"
                : Itens.EhArma(item) ? $" ({qtd}/{Itens.QuantidadePadrao(item)})" : "";
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

        // quantas balas ainda cabem nas caixas deste calibre que já estão na mochila
        int FaltaNasCaixas(Calibre calibre)
        {
            var caixa = Itens.CaixaDe(calibre);
            int n = 0;
            foreach (var it in Mochila)
                if (it.item == caixa) n += Itens.MaxPorCaixa - it.qtd;
            return n;
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

        /// <summary>Junta a caixa 'de' na caixa 'para' (do mesmo calibre), até encher a 'para'.</summary>
        public void Juntar(ushort de, ushort para)
        {
            if (PodeJuntar(de, para)) JuntarRpc(de, para);
        }

        /// <summary>As duas são caixas do mesmo calibre, e a 'para' ainda não está cheia.</summary>
        public bool PodeJuntar(ushort de, ushort para) =>
            de != para && Achar(de, out var a) && Achar(para, out var b) && Itens.EhMunicao(a.item) && a.item == b.item && b.qtd < Itens.MaxPorCaixa;

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
        /// Só no anfitrião: o item entra. A arma vai para a alça (livre) ou para a grade (com lugar no corpo); o resto,
        /// para a primeira posição da grade em que cabe (deitado, ou girado). O equipamento é vestido. Sem lugar, devolve false.
        /// </summary>
        public bool Receber(Item item, int qtd = -1)
        {
            if (!IsServer || item == Item.Nada) return false;
            if (Itens.EhEquipamento(item)) return Vestir(item);
            if (qtd < 0) qtd = Itens.QuantidadePadrao(item);
            if (Itens.EhMunicao(item)) return ReceberMunicao(item, qtd) == 0;
            if (Itens.EhArma(item) && AlcaLivre())
            {
                Mochila.Add(new ItemNaMochila { id = NovoId(), item = item, naAlca = true, qtd = (ushort)qtd });
                return true;
            }
            if (Itens.EhArma(item) && !ArmaCabeNaGrade(item)) return false;
            if (!Espaco(item, out int x, out int y, out bool girado)) return false;
            Mochila.Add(new ItemNaMochila { id = NovoId(), item = item, x = (byte)x, y = (byte)y, girado = girado, qtd = (ushort)qtd });
            return true;
        }

        /// <summary>
        /// Só no anfitrião: as balas de uma caixa entram. Primeiro completam as caixas do mesmo calibre que já estão na
        /// mochila; o resto vai numa caixa nova, se houver espaço. Devolve as que sobraram (que ficam no chão).
        /// </summary>
        public int ReceberMunicao(Item caixa, int balas)
        {
            if (!IsServer || !Itens.EhMunicao(caixa)) return balas;
            for (int i = 0; i < Mochila.Count && balas > 0; i++)
            {
                var it = Mochila[i];
                if (it.item != caixa || it.qtd >= Itens.MaxPorCaixa) continue;
                int n = Mathf.Min(balas, Itens.MaxPorCaixa - it.qtd);
                it.qtd = (ushort)(it.qtd + n);
                Mochila[i] = it;
                balas -= n;
            }
            while (balas > 0 && Espaco(caixa, out int x, out int y, out bool girado))
            {
                int n = Mathf.Min(balas, Itens.MaxPorCaixa);
                Mochila.Add(new ItemNaMochila { id = NovoId(), item = caixa, x = (byte)x, y = (byte)y, girado = girado, qtd = (ushort)n });
                balas -= n;
            }
            return balas;
        }

        /// <summary>
        /// Só no anfitrião: veste o equipamento. A mochila nova troca a que ele vestia (essa cai no chão, aos pés dele),
        /// e a grade é arrumada no tamanho novo.
        /// </summary>
        bool Vestir(Item item)
        {
            if (item == Item.Coldre)
            {
                if (ComColdre.Value) return false;
                ComColdre.Value = true;
                return true;
            }
            if (!Itens.EhMochila(item)) return false;
            var antes = MochilaVestida.Value;
            MochilaVestida.Value = item;
            if (antes != Item.Nada) ItensNoMundo.Instancia?.Criar(antes, PontoAosPes(0), transform.eulerAngles.y + 90f, 0);
            Arrumar(transform.position);
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
        /// Só no anfitrião, depois de trocar ou tirar a mochila ou o coldre: o que ainda cabe fica onde está; o resto
        /// procura outro lugar na grade nova (os maiores primeiro), e o que não couber (ou a arma sem lugar no corpo) cai no
        /// chão, em volta do ponto.
        /// </summary>
        void Arrumar(Vector3 onde)
        {
            var espaco = TamanhoDaGrade;
            bool mochila = MochilaVestida.Value != Item.Nada, coldre = ComColdre.Value;
            var fica = new List<ItemNaMochila>();
            var sobra = new List<ItemNaMochila>();
            foreach (var it in Mochila)
            {
                if (it.naAlca)
                {
                    fica.Add(it);
                    continue;
                }
                bool ok = CabeEm(fica, espaco, it.item, it.girado, it.x, it.y, 0)
                          && (!Itens.EhArma(it.item) || ArmaCabeEm(fica, mochila, coldre, it.item, 0));
                (ok ? fica : sobra).Add(it);
            }
            sobra.Sort((a, b) => (b.Tamanho.x * b.Tamanho.y).CompareTo(a.Tamanho.x * a.Tamanho.y));
            var mudou = new Dictionary<ushort, ItemNaMochila>();
            var cai = new HashSet<ushort>();
            foreach (var it in sobra)
            {
                if ((!Itens.EhArma(it.item) || ArmaCabeEm(fica, mochila, coldre, it.item, 0))
                    && EspacoEm(fica, espaco, it.item, out int x, out int y, out bool girado))
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
                    ItensNoMundo.Instancia?.Criar(it.item, PontoAosPes(++caidos, onde), transform.eulerAngles.y + caidos * 70f, it.qtd);
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
        /// tipo delas, com o pente que tinham), e a comida, a água, a munição, a mochila e o coldre ficam no chão, onde ele
        /// estava.
        /// </summary>
        public void Esvaziar()
        {
            int k = 0;
            for (int i = 0; i < Mochila.Count; i++)
            {
                var it = Mochila[i];
                if (Itens.EhArma(it.item)) ParedeDeArmas.Instancia?.Devolver(Itens.Arma(it.item), it.qtd);
                else if (Itens.EhConsumivel(it.item) || Itens.EhMunicao(it.item)) ItensNoMundo.Instancia?.Criar(it.item, PontoAosPes(++k), k * 90f, it.qtd);
            }
            if (MochilaVestida.Value != Item.Nada) ItensNoMundo.Instancia?.Criar(MochilaVestida.Value, PontoAosPes(++k), k * 90f, 0);
            if (ComColdre.Value) ItensNoMundo.Instancia?.Criar(Item.Coldre, PontoAosPes(++k), k * 90f, 0);
        }

        [Rpc(SendTo.Server)]
        void MoverRpc(ushort id, byte x, byte y, bool girado, bool paraAlca, RpcParams parametros = default)
        {
            int k = Indice(id);
            if (parametros.Receive.SenderClientId != OwnerClientId || k < 0) return;
            var it = Mochila[k];
            if (paraAlca)
            {
                if (!PodeIrParaAlca(id)) return;
                it.naAlca = true;
                it.x = it.y = 0;
                it.girado = false;
            }
            else
            {
                if (!it.naAlca && it.x == x && it.y == y && it.girado == girado) return;
                if (!PodeMover(id, x, y, girado)) return;
                it.naAlca = false;
                it.x = x;
                it.y = y;
                it.girado = girado;
            }
            Mochila[k] = it;
        }

        [Rpc(SendTo.Server)]
        void TirarEquipamentoRpc(Item qual, Vector3 ponto, float giro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            if (qual == Item.Coldre)
            {
                if (!ComColdre.Value) return;
                ComColdre.Value = false;
            }
            else
            {
                if (!Itens.EhMochila(qual) || MochilaVestida.Value != qual) return;
                MochilaVestida.Value = Item.Nada;
            }
            if ((ponto - transform.position).sqrMagnitude > 4f * 4f) ponto = transform.position;
            ItensNoMundo.Instancia?.Criar(qual, ponto, giro, 0);
            Arrumar(ponto);
        }

        [Rpc(SendTo.Server)]
        void ConsumirRpc(ushort id, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !Itens.EhConsumivel(Do(id))) return;
            var item = Tirar(id);
            GetComponent<FomeESede>().Consumir(Itens.Consumivel(item));
        }

        [Rpc(SendTo.Server)]
        void LargarRpc(ushort id, Vector3 ponto, float giro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || ItensNoMundo.Instancia == null) return;
            if ((ponto - transform.position).sqrMagnitude > 4f * 4f) ponto = transform.position;
            if (!Achar(id, out var it)) return;
            Tirar(id);
            ItensNoMundo.Instancia.Criar(it.item, ponto, giro, it.qtd);
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
            int n = Mathf.Min(origem.qtd, Itens.MaxPorCaixa - destino.qtd);
            destino.qtd = (ushort)(destino.qtd + n);
            Mochila[b] = destino;
            if (n >= origem.qtd) Mochila.RemoveAt(Indice(de));
            else
            {
                origem.qtd = (ushort)(origem.qtd - n);
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
            if (x >= 0 && y >= 0 && x < Colunas && y < Linhas) MoverRpc(id, (byte)x, (byte)y, girado, false);
        }

        /// <summary>Pendura a arma na alça (o anfitrião confere se ela está livre).</summary>
        public void PorNaAlca(ushort id)
        {
            if (PodeIrParaAlca(id)) MoverRpc(id, 0, 0, false, true);
        }

        /// <summary>Tira a mochila vestida ou o coldre e larga no chão (o que não couber mais na grade cai junto).</summary>
        public void TirarEquipamento(Item qual)
        {
            bool veste = qual == Item.Coldre ? ComColdre.Value : Itens.EhMochila(qual) && MochilaVestida.Value == qual;
            if (!veste || _arma.Usando) return;
            TirarEquipamentoRpc(qual, PontoParaLargar(_rede.cameraMundo.transform), transform.eulerAngles.y + Random.Range(-50f, 50f));
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.5f, false);
        }

        /// <summary>Põe o item num atalho (se ele já estava em outro, sai de lá).</summary>
        public void PorNoAtalho(int lugar, ushort id)
        {
            // a munição não vai na mão (a recarga tira dela direto da mochila)
            if (lugar < 0 || lugar >= Atalhos || Do(id) == Item.Nada || Itens.EhMunicao(Do(id)) || _arma.Usando) return;
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

        /// <summary>Larga o item: no tampo para onde a mira aponta, se estiver perto, senão no chão à frente.</summary>
        public void Largar(ushort id)
        {
            if (Do(id) == Item.Nada || (_arma.Usando && id == IdNaMao)) return;
            if (Itens.EhArma(Do(id))) GuardarPente(id, _arma.MunicaoDe(id, Do(id)));   // a arma leva o pente para o chão
            LargarRpc(id, PontoParaLargar(_rede.cameraMundo.transform), transform.eulerAngles.y + Random.Range(-50f, 50f));
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.5f, false);
        }

        /// <summary>
        /// O dono terminou de comer ou beber o item: ele acaba, e a fome e a sede caem. Depois, a mão volta para o
        /// atalho da última arma (se ela ainda estiver num).
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
                if (e.Type == NetworkListEvent<ItemNaMochila>.EventType.Add && Time.time < _pegouAte && !Itens.EhMunicao(e.Value.item))
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

        void Update()
        {
            if (!IsSpawned || !IsOwner || MenuOnline.Bot || !MenuOnline.Controla) return;
            if (_arma.Usando) return;   // comendo ou bebendo: nem troca nem larga
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
            // dentro do carro, não larga nada
            if (teclado != null && teclado.gKey.wasPressedThisFrame && !(_rede.Passageiro != null && _rede.Passageiro.NoCarro)) Largar(IdNaMao);
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
            int c = Itens.IndiceDaMunicao(item);
            if (c >= 0) return iconesDeMunicao != null && c < iconesDeMunicao.Length ? iconesDeMunicao[c] : null;
            int e = Itens.Equipamento(item);
            if (e >= 0) return iconesDeEquipamento != null && e < iconesDeEquipamento.Length ? iconesDeEquipamento[e] : null;
            int m = Itens.Modelo(item);
            return m >= 0 && icones != null && m < icones.Length ? icones[m] : null;
        }

        public Texture2D FotoDe(Item item)
        {
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
            if (!IsSpawned || !IsOwner || MenuOnline.Aberto || (_tela != null && _tela.Aberta)) return;
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
            // o nome do que está na mão, logo depois de trocar; e, com comida ou água, como usar
            var texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u), alignment = TextAnchor.LowerCenter };
            var naMao = NaMao;
            string linha = null;
            float alfa = 1f;
            if (Itens.EhConsumivel(naMao)) linha = $"{Itens.Nome(naMao)}   ·   clique: {Itens.Consumivel(naMao).verbo}   ·   G: largar";
            else if (Time.time < _nomeAte)
            {
                linha = naMao == Item.Nada ? "mãos vazias   ·   clique: soco" : Itens.Nome(naMao);
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
