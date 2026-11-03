using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Loot
{
    /// <summary>Uma mochila no chão (a de um sobrevivente morto, ou a nossa, onde caímos).</summary>
    public struct MochilaNoChao : INetworkSerializeByMemcpy, IEquatable<MochilaNoChao>
    {
        public ushort id;
        public Vector3 pos;
        public float giro;
        /// <summary>A mochila (Nada: só os pertences, sem mochila).</summary>
        public Item mochila;
        public int dinheiro;
        /// <summary>A grade dela (as colunas e as linhas: a da mochila; mais linhas, se o que ela leva não cabe).</summary>
        public byte colunas, linhas;
        public FixedString32Bytes nome;

        public bool Equals(MochilaNoChao o) => id == o.id && pos == o.pos && giro == o.giro && mochila == o.mochila && dinheiro == o.dinheiro
                                               && colunas == o.colunas && linhas == o.linhas && nome.Equals(o.nome);
    }

    /// <summary>Um item dentro de uma mochila no chão (o Id do item vale dentro da mochila).</summary>
    public struct ItemNoRecipiente : INetworkSerializeByMemcpy, IEquatable<ItemNoRecipiente>
    {
        public ushort recipiente;
        public ItemNaMochila item;

        public bool Equals(ItemNoRecipiente o) => recipiente == o.recipiente && item.Equals(o.item);
    }

    /// <summary>
    /// As mochilas no chão (02/11/2026, a pedido: "a mochila que quando abrimos abre um inventário deles", e "morreu, a
    /// mochila fica onde a gente caiu"), no objeto "Mundo". O anfitrião cria (o sobrevivente que morre; o jogador que cai)
    /// e muda; todos desenham (a malha da mochila, ou a pequena para os pertences sem mochila) e o E abre a tela do saque
    /// (<see cref="TelaDoSaque"/>). Pegar, guardar, pegar tudo, pegar o dinheiro e trocar de mochila (a dele, maior, já
    /// cheia: a nossa fica no lugar, com o que tinha) passam pelo anfitrião. A vazia some. Elas ficam no save.
    /// </summary>
    public class MochilasNoChao : NetworkBehaviour
    {
        public static MochilasNoChao Instancia { get; private set; }
        public const float Alcance = 3.2f;

        NetworkList<MochilaNoChao> _mochilas;
        NetworkList<ItemNoRecipiente> _itens;
        ushort _proximoId = 1, _proximoItem = 1;
        readonly Dictionary<ushort, GameObject> _objetos = new Dictionary<ushort, GameObject>();

        void Awake()
        {
            Instancia = this;
            _mochilas = new NetworkList<MochilaNoChao>();
            _itens = new NetworkList<ItemNoRecipiente>();
        }

        public override void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            Instancia = this;
            if (IsServer)
            {
                _mochilas.Clear();
                _itens.Clear();
            }
            _mochilas.OnListChanged += AoMudar;
            Atualizar();
        }

        public override void OnNetworkDespawn()
        {
            _mochilas.OnListChanged -= AoMudar;
            foreach (var o in _objetos.Values) if (o != null) Destroy(o);
            _objetos.Clear();
        }

        void AoMudar(NetworkListEvent<MochilaNoChao> e) => Atualizar();

        // ------------------------------------------------------------------ o que todos veem

        void Atualizar()
        {
            var vivas = new HashSet<ushort>();
            foreach (var m in _mochilas)
            {
                vivas.Add(m.id);
                if (!_objetos.TryGetValue(m.id, out var go) || go == null) _objetos[m.id] = go = NovoObjeto(m);
                else Desenhar(go, m);
            }
            foreach (var id in new List<ushort>(_objetos.Keys))
                if (!vivas.Contains(id))
                {
                    if (_objetos[id] != null) Destroy(_objetos[id]);
                    _objetos.Remove(id);
                }
        }

        GameObject NovoObjeto(MochilaNoChao m)
        {
            var go = new GameObject("Mochila no chão");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.On;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            go.AddComponent<MochilaNoChaoNaMira>().id = m.id;
            Desenhar(go, m);
            return go;
        }

        static void Desenhar(GameObject go, MochilaNoChao m)
        {
            var itens = ItensNoMundo.Instancia;
            var item = m.mochila != Item.Nada ? m.mochila : Item.MochilaPequena;
            var malha = itens != null ? itens.MalhaDe(item) : null;
            go.GetComponent<MeshFilter>().sharedMesh = malha;
            var r = go.GetComponent<MeshRenderer>();
            if (itens != null) r.sharedMaterial = itens.MaterialDe(item);
            go.transform.SetPositionAndRotation(m.pos, Quaternion.Euler(0f, m.giro, 0f));
            var col = go.GetComponent<BoxCollider>();
            if (malha != null)
            {
                col.center = malha.bounds.center;
                col.size = malha.bounds.size + Vector3.one * 0.15f;
            }
            else col.size = new Vector3(0.5f, 0.5f, 0.5f);
        }

        // ------------------------------------------------------------------ ler

        public bool Achar(ushort id, out MochilaNoChao m)
        {
            foreach (var x in _mochilas)
                if (x.id == id)
                {
                    m = x;
                    return true;
                }
            m = default;
            return false;
        }

        int Indice(ushort id)
        {
            for (int i = 0; i < _mochilas.Count; i++) if (_mochilas[i].id == id) return i;
            return -1;
        }

        /// <summary>Os itens de uma mochila (uma cópia).</summary>
        public List<ItemNaMochila> Itens(ushort recipiente)
        {
            var lista = new List<ItemNaMochila>();
            foreach (var x in _itens) if (x.recipiente == recipiente) lista.Add(x.item);
            return lista;
        }

        public static string NomeDaMochila(MochilaNoChao m)
        {
            string nome = m.nome.ToString();
            return string.IsNullOrEmpty(nome) ? "a mochila" : nome;
        }

        // ------------------------------------------------------------------ criar (no anfitrião)

        /// <summary>No anfitrião: uma mochila no chão com os itens (a grade é a da mochila; mais linhas, se precisar).</summary>
        public ushort Criar(Vector3 pos, float giro, string nome, Item mochila, List<ItemNaMochila> itens, int dinheiro)
        {
            if (!IsServer || !IsSpawned) return 0;
            if ((itens == null || itens.Count == 0) && mochila == Item.Nada && dinheiro <= 0) return 0;
            var espaco = Loot.Itens.EspacoDa(mochila);
            int colunas = Mathf.Max(espaco.x, 6), linhas = Mathf.Max(espaco.y, 2);
            // o chão embaixo (o corpo pode estar caindo)
            if (Physics.Raycast(pos + Vector3.up * 1.2f, Vector3.down, out var chao, 4f, 1 << 0, QueryTriggerInteraction.Ignore)) pos = chao.point;
            ushort id = _proximoId++;
            var postos = new List<ItemNaMochila>();
            if (itens != null)
                foreach (var it in itens)
                {
                    if (!Loot.Itens.NoJogo(it.item)) continue;
                    // (03/11/2026) o que não vai na grade (as mochilas: sem tamanho nela) fica no chão, do lado. Antes a grade
                    // crescia sem fim tentando encaixar a mochila da banca do comerciante de roupas morto, e o jogo travava
                    var tamanho = Loot.Itens.Tamanho(it.item);
                    if (tamanho.x <= 0 || tamanho.y <= 0)
                    {
                        NoChaoDoLado(it, pos, giro);
                        continue;
                    }
                    var novo = it;
                    bool coube = false;
                    for (int tentativa = 0; tentativa < 80 && !(coube = Posicionar(postos, ref novo, colunas, linhas)); tentativa++) linhas++;
                    if (!coube)
                    {
                        Debug.LogWarning($"[Mochilas] {it.item} não coube na mochila de \"{nome}\": fica no chão.");
                        NoChaoDoLado(it, pos, giro);
                        continue;
                    }
                    novo.id = _proximoItem++;
                    postos.Add(novo);
                    _itens.Add(new ItemNoRecipiente { recipiente = id, item = novo });
                }
            _mochilas.Add(new MochilaNoChao
            {
                id = id, pos = pos, giro = giro, mochila = mochila, dinheiro = Mathf.Max(0, dinheiro), colunas = (byte)colunas, linhas = (byte)Mathf.Min(linhas, 40),
                nome = new FixedString32Bytes(Cortar(nome)),
            });
            return id;
        }

        // um item no chão, ao lado da mochila (o que não vai na grade)
        static void NoChaoDoLado(ItemNaMochila it, Vector3 pos, float giro)
        {
            var mundo = ItensNoMundo.Instancia;
            if (mundo == null) return;
            var lado = Quaternion.Euler(0f, giro + UnityEngine.Random.Range(60f, 120f), 0f) * Vector3.forward * UnityEngine.Random.Range(0.5f, 0.9f);
            mundo.Criar(it.item, pos + lado, giro + UnityEngine.Random.Range(0f, 360f), it.qtd, it.desgaste, it.cores);
        }

        internal static string Curto(string s) => Cortar(s);
        // o FixedString32Bytes guarda 29 bytes (as letras com acento ocupam 2)
        static string Cortar(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            while (System.Text.Encoding.UTF8.GetByteCount(s) > 29) s = s.Substring(0, s.Length - 1);
            return s;
        }

        // o item no primeiro lugar em que cabe na grade (deitado, ou girado)
        static bool Posicionar(List<ItemNaMochila> postos, ref ItemNaMochila it, int colunas, int linhas)
        {
            if (!InventarioJogador.EspacoNaGrade(postos, new Vector2Int(colunas, linhas), it.item, out int x, out int y, out bool girado)) return false;
            it.x = (byte)x;
            it.y = (byte)y;
            it.girado = girado;
            return true;
        }

        /// <summary>
        /// No anfitrião: o jogador caiu (a vida acabou). A mochila vestida, com tudo, o pneu das mãos e as roupas vestidas
        /// (03/11/2026, a pedido: "perder também as roupas") ficam no chão, onde ele estava; ele volta na cama sem nada, só
        /// com o corpo da criação e o dinheiro.
        /// </summary>
        public void DaMorte(InventarioJogador inventario)
        {
            if (!IsServer || inventario == null) return;
            var itens = inventario.TirarTudo(out var mochila, out int pneu);
            if (pneu != InventarioJogador.SemPneu) itens.Add(new ItemNaMochila { item = Item.Pneu, qtd = (ushort)pneu });
            var roupas = inventario.Roupas.Value;
            for (int k = 0; k < global::ZombieCityCleaner.Loot.Itens.QuantosLugaresDeRoupa; k++)
            {
                var l = (LugarDaRoupa)k;
                if (roupas.Peca(l) != Item.Nada) itens.Add(new ItemNaMochila { item = roupas.Peca(l), qtd = 1, cores = roupas.Cores(l) });
            }
            inventario.Roupas.Value = default;
            var rede = inventario.GetComponent<JogadorRede>();
            string nome = rede != null && rede.Criado ? $"a mochila de {rede.Aparencia.Value.nome}" : "a sua mochila";
            Criar(inventario.transform.position, inventario.transform.eulerAngles.y, nome, mochila, itens, 0);
        }

        // ------------------------------------------------------------------ o que o jogador pede (passa pelo anfitrião)

        InventarioJogador QuemPede(RpcParams p, ushort recipiente, out int k)
        {
            k = Indice(recipiente);
            if (k < 0 || !NetworkManager.ConnectedClients.TryGetValue(p.Receive.SenderClientId, out var c) || c.PlayerObject == null) return null;
            if ((c.PlayerObject.transform.position - _mochilas[k].pos).sqrMagnitude > (Alcance + 1f) * (Alcance + 1f)) return null;
            return c.PlayerObject.GetComponent<InventarioJogador>();
        }

        public void PedirPegar(ushort recipiente, ushort item) => PegarRpc(recipiente, item);
        public void PedirGuardar(ushort recipiente, ushort idNaMochila) => GuardarRpc(recipiente, idNaMochila);
        public void PedirPegarTudo(ushort recipiente) => PegarTudoRpc(recipiente);
        public void PedirDinheiro(ushort recipiente) => DinheiroRpc(recipiente);
        public void PedirTrocarDeMochila(ushort recipiente) => TrocarRpc(recipiente);

        [Rpc(SendTo.Server)]
        void PegarRpc(ushort recipiente, ushort item, RpcParams parametros = default)
        {
            var inv = QuemPede(parametros, recipiente, out _);
            if (inv == null) return;
            if (!Passar(inv, recipiente, item)) inv.AvisarDono("Não cabe na sua mochila.");
            Limpar(recipiente);
        }

        [Rpc(SendTo.Server)]
        void PegarTudoRpc(ushort recipiente, RpcParams parametros = default)
        {
            var inv = QuemPede(parametros, recipiente, out int k);
            if (inv == null) return;
            bool sobrou = false;
            foreach (var it in Itens(recipiente)) sobrou |= !Passar(inv, recipiente, it.id);
            var m = _mochilas[k];
            if (m.dinheiro > 0)
            {
                inv.Pagar(m.dinheiro);
                m.dinheiro = 0;
                _mochilas[k] = m;
            }
            if (sobrou) inv.AvisarDono("Nem tudo coube na sua mochila.");
            Limpar(recipiente);
        }

        [Rpc(SendTo.Server)]
        void DinheiroRpc(ushort recipiente, RpcParams parametros = default)
        {
            var inv = QuemPede(parametros, recipiente, out int k);
            if (inv == null) return;
            var m = _mochilas[k];
            if (m.dinheiro <= 0) return;
            inv.Pagar(m.dinheiro);
            m.dinheiro = 0;
            _mochilas[k] = m;
            Limpar(recipiente);
        }

        [Rpc(SendTo.Server)]
        void GuardarRpc(ushort recipiente, ushort idNaMochila, RpcParams parametros = default)
        {
            var inv = QuemPede(parametros, recipiente, out int k);
            if (inv == null || !inv.Achar(idNaMochila, out var it)) return;
            var m = _mochilas[k];
            var postos = Itens(recipiente);
            int linhas = m.linhas;
            var novo = it;
            if (!Posicionar(postos, ref novo, m.colunas, linhas))
            {
                inv.AvisarDono("Não cabe aí.");
                return;
            }
            if (Loot.Itens.EhArma(it.item)) novo.qtd = (ushort)Mathf.Max(0, inv.PenteDe(it.id, it.qtd));
            inv.Tirar(idNaMochila);
            novo.id = _proximoItem++;
            _itens.Add(new ItemNoRecipiente { recipiente = recipiente, item = novo });
        }

        /// <summary>
        /// A troca de mochila: a dele (com tudo dentro) vai para o jogador, e a do jogador (com tudo dentro) fica no chão. O
        /// que não couber na grade nova fica no chão também.
        /// </summary>
        [Rpc(SendTo.Server)]
        void TrocarRpc(ushort recipiente, RpcParams parametros = default)
        {
            var inv = QuemPede(parametros, recipiente, out int k);
            if (inv == null) return;
            var m = _mochilas[k];
            if (m.mochila == Item.Nada) return;
            var dele = Itens(recipiente);
            var minhas = inv.TirarTudo(out var minha, out int pneu);
            // o que era dele vai para o jogador (com a mochila dele)
            RemoverItens(recipiente);
            var sobra = inv.VestirComItens(m.mochila, dele);
            // o que era do jogador fica no chão, na mochila dele (com o que não coube)
            var ficam = new List<ItemNaMochila>(minhas);
            ficam.AddRange(sobra);
            if (pneu != InventarioJogador.SemPneu) ficam.Add(new ItemNaMochila { item = Item.Pneu, qtd = (ushort)pneu });
            var espaco = Loot.Itens.EspacoDa(minha);
            int colunas = Mathf.Max(espaco.x, 6), linhas = Mathf.Max(espaco.y, 2);
            var postos = new List<ItemNaMochila>();
            foreach (var it in ficam)
            {
                var novo = it;
                while (!Posicionar(postos, ref novo, colunas, linhas)) linhas++;
                novo.id = _proximoItem++;
                postos.Add(novo);
                _itens.Add(new ItemNoRecipiente { recipiente = recipiente, item = novo });
            }
            m.mochila = minha;
            m.colunas = (byte)colunas;
            m.linhas = (byte)Mathf.Min(linhas, 40);
            _mochilas[k] = m;
            Limpar(recipiente);
        }

        // um item da mochila no chão para o jogador (as pilhas: o que couber); devolve se passou tudo
        bool Passar(InventarioJogador inv, ushort recipiente, ushort item)
        {
            for (int i = 0; i < _itens.Count; i++)
            {
                var x = _itens[i];
                if (x.recipiente != recipiente || x.item.id != item) continue;
                int sobra = inv.ReceberIgual(x.item);
                if (sobra <= 0)
                {
                    _itens.RemoveAt(i);
                    return true;
                }
                if (sobra < Loot.Itens.Quantos(x.item.item, x.item.qtd))
                {
                    x.item.qtd = (ushort)sobra;
                    _itens[i] = x;
                }
                return false;
            }
            return true;
        }

        void RemoverItens(ushort recipiente)
        {
            for (int i = _itens.Count - 1; i >= 0; i--) if (_itens[i].recipiente == recipiente) _itens.RemoveAt(i);
        }

        // a mochila que ficou sem nada (sem itens, sem dinheiro e sem mochila) some
        void Limpar(ushort recipiente)
        {
            int k = Indice(recipiente);
            if (k < 0) return;
            var m = _mochilas[k];
            if (m.mochila != Item.Nada || m.dinheiro > 0) return;
            foreach (var x in _itens) if (x.recipiente == recipiente) return;
            _mochilas.RemoveAt(k);
        }

        // ------------------------------------------------------------------ o save

        public List<(MochilaNoChao m, List<ItemNaMochila> itens)> Copia()
        {
            var lista = new List<(MochilaNoChao, List<ItemNaMochila>)>();
            foreach (var m in _mochilas) lista.Add((m, Itens(m.id)));
            return lista;
        }

        public void Restaurar(List<(MochilaNoChao m, List<ItemNaMochila> itens)> lista)
        {
            if (!IsServer || !IsSpawned) return;
            _mochilas.Clear();
            _itens.Clear();
            _proximoId = 1;
            _proximoItem = 1;
            foreach (var (m, itens) in lista)
            {
                var novo = m;
                novo.id = _proximoId++;
                foreach (var it in itens)
                {
                    var i = it;
                    i.id = _proximoItem++;
                    _itens.Add(new ItemNoRecipiente { recipiente = novo.id, item = i });
                }
                _mochilas.Add(novo);
            }
        }
    }

    /// <summary>A mochila no chão na mira: E abre a tela do saque.</summary>
    public class MochilaNoChaoNaMira : Interagivel
    {
        public ushort id;

        public override string Dica(ArmaJogador quem)
        {
            var m = MochilasNoChao.Instancia;
            if (m == null || !m.Achar(id, out var mochila)) return null;
            return $"[E] Revistar {MochilasNoChao.NomeDaMochila(mochila)}";
        }

        public override void Usar(ArmaJogador quem) => TelaDoSaque.Abrir(id);
    }
}
