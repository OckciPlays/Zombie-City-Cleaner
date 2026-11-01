using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// Um item no chão (ou num móvel): o que é, onde está apoiado, para onde está virado e as balas (da caixa de munição, ou
    /// do pente da arma).
    /// </summary>
    public struct ItemNoChao : INetworkSerializeByMemcpy, IEquatable<ItemNoChao>
    {
        public ushort id;
        public Item item;
        /// <summary>O ponto de apoio (o chão ou o tampo, embaixo do meio do item).</summary>
        public Vector3 apoio;
        public float giro;
        public ushort qtd;
        /// <summary>O desgaste da arma (0, nova).</summary>
        public byte desgaste;

        public bool Equals(ItemNoChao o) => id == o.id && item == o.item && apoio == o.apoio && giro == o.giro && qtd == o.qtd && desgaste == o.desgaste;
    }

    /// <summary>
    /// Os itens soltos pelo mundo: a comida, a água e as caixas de munição espalhadas pelas casas e pelo prédio, e o que os
    /// jogadores largam (armas também, com o pente que tinham). Uma lista só na rede (o anfitrião manda), e cada cópia monta os objetos dela: a malha do item, em
    /// pé (a lata e a garrafa) ou deitada de lado (as armas), e o colisor que a mira procura (<see cref="ItemNoChaoNaMira"/>).
    /// Pegar (E) vai para o anfitrião, que confere a distância e põe o item na mochila (sem espaço, ele fica no chão).
    /// Os lugares do começo vêm da montagem da cidade (os tampos das mesas, balcões, cômodas e criados-mudos, e o chão),
    /// cada um já com o item dele.
    /// </summary>
    public class ItensNoMundo : NetworkBehaviour
    {
        [Tooltip("A malha de cada modelo, na ordem de Itens.Modelos (origem onde a mão segura; topo em +Y).")]
        public Mesh[] malhas;
        [Tooltip("A malha de cada equipamento (as 3 mochilas e o coldre), na ordem do enum Item (topo em +Y).")]
        public Mesh[] malhasDeEquipamento;
        [Tooltip("A malha de cada caixa de munição, na ordem de Calibre (topo em +Y).")]
        public Mesh[] malhasDeMunicao;
        [Tooltip("A malha de cada peça do motor (a vela, a bateria e o radiador), na ordem do enum Item (topo em +Y).")]
        public Mesh[] malhasDePeca;
        [Tooltip("A malha de cada coisa da sinuca (as 2 caixas de bolas e os 4 tacos, deitados), na ordem do enum Item (topo em +Y).")]
        public Mesh[] malhasDaSinuca;
        [Tooltip("A malha de cada item do saque (o loot.py), na ordem de Itens.DoLoot (topo em +Y).")]
        public Mesh[] malhasDeLoot;
        public Material material;
        [Tooltip("Os itens do começo da partida: o ponto de apoio de cada um.")]
        public Vector3[] lugares;
        public Item[] itensDosLugares;
        public float[] girosDosLugares;
        [Tooltip("Até onde o anfitrião aceita o pedido de pegar (m, dos pés do jogador ao item).")]
        public float distanciaMaxima = 3.5f;

        public static ItensNoMundo Instancia { get; private set; }

        NetworkList<ItemNoChao> _itens;
        ushort _proximoId = 1;
        readonly Dictionary<ushort, ItemNoChaoNaMira> _objetos = new Dictionary<ushort, ItemNoChaoNaMira>();
        readonly Stack<ItemNoChaoNaMira> _guardados = new Stack<ItemNoChaoNaMira>();
        readonly HashSet<ushort> _vistos = new HashSet<ushort>();
        readonly List<ushort> _sobrando = new List<ushort>();

        void Awake() => _itens = new NetworkList<ItemNoChao>();

        void OnEnable() => Instancia = this;

        void OnDisable()
        {
            if (Instancia == this) Instancia = null;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _itens.Clear();
                for (int i = 0; lugares != null && i < lugares.Length; i++)
                {
                    var item = itensDosLugares[i];
                    // a caixa achada nunca vem cheia: o anfitrião sorteia as balas a cada partida
                    int qtd = Itens.EhMunicao(item) ? UnityEngine.Random.Range(Itens.Municao(Itens.CalibreDa(item)).achadaMin, Itens.Municao(Itens.CalibreDa(item)).achadaMax + 1) : Itens.EmPilha(item) ? 1 : 0;
                    _itens.Add(new ItemNoChao { id = _proximoId++, item = item, apoio = lugares[i], giro = girosDosLugares[i], qtd = (ushort)qtd });
                }
            }
            _itens.OnListChanged += AoMudar;
            Atualizar();
        }

        public override void OnNetworkDespawn()
        {
            _itens.OnListChanged -= AoMudar;
            foreach (var o in _objetos.Values) Guardar(o);
            _objetos.Clear();
        }

        void AoMudar(NetworkListEvent<ItemNoChao> e) => Atualizar();

        /// <summary>
        /// Só no anfitrião: um item novo no mundo, apoiado no ponto. <paramref name="qtd"/>: as balas (da caixa, ou do pente
        /// da arma); -1, cheio.
        /// </summary>
        public void Criar(Item item, Vector3 apoio, float giro, int qtd = -1, int desgaste = 0)
        {
            if (!IsServer || !IsSpawned || item == Item.Nada) return;
            if (qtd < 0) qtd = Itens.QuantidadePadrao(item);
            if (Itens.EhMunicao(item) && qtd <= 0) return;   // a caixa vazia não fica no chão
            if (Itens.EmPilha(item)) qtd = Mathf.Max(1, qtd);
            _itens.Add(new ItemNoChao { id = _proximoId++, item = item, apoio = apoio, giro = giro, qtd = (ushort)qtd, desgaste = (byte)Mathf.Clamp(desgaste, 0, 100) });
        }

        /// <summary>O jogador local pede para pegar o item.</summary>
        public void PedirPegar(ushort id, InventarioJogador quem)
        {
            if (!IsSpawned) return;
            quem.AvisarQuePegou();
            PegarRpc(id);
        }

        [Rpc(SendTo.Server)]
        void PegarRpc(ushort id, RpcParams parametros = default)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return;
            var inventario = cliente.PlayerObject.GetComponent<InventarioJogador>();
            int k = Indice(id);
            if (inventario == null || k < 0) return;
            var it = _itens[k];
            if ((cliente.PlayerObject.transform.position - it.apoio).sqrMagnitude > distanciaMaxima * distanciaMaxima) return;
            // a pilha (a munição, o saque) completa as que ele já tem, e o que não couber fica no chão
            if (Itens.EmPilha(it.item))
            {
                int sobra = inventario.ReceberEmPilha(it.item, Itens.Quantos(it.item, it.qtd));
                if (sobra <= 0) _itens.RemoveAt(k);
                else if (sobra != it.qtd)
                {
                    it.qtd = (ushort)sobra;
                    _itens[k] = it;
                }
                return;
            }
            // sem espaço na mochila, ele fica no chão (a dica avisa)
            if (inventario.Receber(it.item, it.qtd, it.desgaste)) _itens.RemoveAt(k);
        }

        int Indice(ushort id)
        {
            for (int i = 0; i < _itens.Count; i++)
                if (_itens[i].id == id) return i;
            return -1;
        }

        /// <summary>O item do objeto da mira (ou Nada, se já foi pego).</summary>
        public Item Do(ushort id)
        {
            int k = Indice(id);
            return k < 0 ? Item.Nada : _itens[k].item;
        }

        /// <summary>As balas do item (da caixa, ou do pente da arma).</summary>
        public int Quantidade(ushort id)
        {
            int k = Indice(id);
            return k < 0 ? 0 : _itens[k].qtd;
        }

        // cada cópia monta os objetos da lista: os novos aparecem, os pegos somem, os trocados mudam de malha
        void Atualizar()
        {
            _vistos.Clear();
            foreach (var it in _itens)
            {
                _vistos.Add(it.id);
                if (!_objetos.TryGetValue(it.id, out var o))
                {
                    o = _guardados.Count > 0 ? _guardados.Pop() : NovoObjeto();
                    o.gameObject.SetActive(true);
                    o.id = it.id;
                    o.item = Item.Nada;
                    _objetos[it.id] = o;
                }
                if (o.item != it.item) Montar(o, it);
            }
            _sobrando.Clear();
            foreach (var id in _objetos.Keys)
                if (!_vistos.Contains(id)) _sobrando.Add(id);
            foreach (var id in _sobrando)
            {
                Guardar(_objetos[id]);
                _objetos.Remove(id);
            }
        }

        ItemNoChaoNaMira NovoObjeto()
        {
            var go = new GameObject("Item");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var o = go.AddComponent<ItemNoChaoNaMira>();
            o.mundo = this;
            return o;
        }

        void Guardar(ItemNoChaoNaMira o)
        {
            if (o == null) return;
            o.gameObject.SetActive(false);
            _guardados.Push(o);
        }

        // a lata, a garrafa, as mochilas e o coldre em pé; as armas deitadas de lado. O ponto de apoio fica embaixo do
        // meio da malha
        /// <summary>A malha do item do saque (null: não é um, ou ainda não foi montada).</summary>
        public Mesh MalhaDoLoot(Item item)
        {
            int k = Itens.IndiceDoLoot(item);
            return malhasDeLoot != null && k >= 0 && k < malhasDeLoot.Length ? malhasDeLoot[k] : null;
        }

        /// <summary>A malha do item como ele fica no chão (null: sem modelo).</summary>
        public Mesh MalhaDe(Item item)
        {
            int m = Itens.Modelo(item), e = Itens.Equipamento(item), mun = Itens.IndiceDaMunicao(item), pc = Itens.IndiceDaPeca(item);
            int sn = Itens.IndiceDaSinuca(item);
            return MalhaDoLoot(item) is Mesh l ? l
                : e >= 0 ? (malhasDeEquipamento != null && e < malhasDeEquipamento.Length ? malhasDeEquipamento[e] : null)
                : mun >= 0 ? (malhasDeMunicao != null && mun < malhasDeMunicao.Length ? malhasDeMunicao[mun] : null)
                : pc >= 0 ? (malhasDePeca != null && pc < malhasDePeca.Length ? malhasDePeca[pc] : null)
                : sn >= 0 ? (malhasDaSinuca != null && sn < malhasDaSinuca.Length ? malhasDaSinuca[sn] : null)
                : malhas != null && m >= 0 && m < malhas.Length ? malhas[m] : null;
        }

        /// <summary>O material do item: a paleta, ou o da textura própria do modelo (a pistola nova).</summary>
        public Material MaterialDe(Item item)
        {
            int m = Itens.Modelo(item), e = Itens.Equipamento(item), mun = Itens.IndiceDaMunicao(item), pc = Itens.IndiceDaPeca(item);
            var proprio = m >= 0 && e < 0 && mun < 0 && pc < 0 ? Itens.MaterialProprio(m) : null;
            return proprio != null ? proprio : material;
        }

        void Montar(ItemNoChaoNaMira o, ItemNoChao it)
        {
            o.item = it.item;
            var malha = MalhaDe(it.item);
            o.GetComponent<MeshFilter>().sharedMesh = malha;
            o.GetComponent<MeshRenderer>().sharedMaterial = MaterialDe(it.item);
            o.name = $"Item {it.id} ({Itens.Nome(it.item)})";
            if (malha == null) return;
            var b = malha.bounds;
            var rot = Quaternion.Euler(0f, it.giro, 0f) * (Itens.EhArma(it.item) || Itens.EhBranca(it.item) ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity);
            float baixo = float.MaxValue;
            Vector3 meio = Vector3.zero;
            for (int c = 0; c < 8; c++)
            {
                var p = rot * new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                baixo = Mathf.Min(baixo, p.y);
                meio += p / 8f;
            }
            o.transform.SetPositionAndRotation(it.apoio + new Vector3(-meio.x, -baixo + 0.002f, -meio.z), rot);
            // o colisor da mira: a caixa da malha, um pouco maior (as coisas pequenas ficam fáceis de mirar)
            var col = o.GetComponent<BoxCollider>();
            col.center = b.center;
            col.size = Vector3.Max(b.size + Vector3.one * 0.04f, Vector3.one * 0.14f);
        }
    }

    /// <summary>O objeto de um item do <see cref="ItensNoMundo"/>: a mira acha o colisor, e E pega.</summary>
    public class ItemNoChaoNaMira : Interagivel
    {
        public ItensNoMundo mundo;
        public ushort id;
        public Item item;

        public override string Dica(ArmaJogador quem)
        {
            var inventario = quem.Inventario;
            if (inventario == null || mundo.Do(id) == Item.Nada) return null;
            return inventario.DicaDePegar(item, Itens.EmPilha(item) || Itens.EhArma(item) || Itens.ComEstado(item) ? mundo.Quantidade(id) : -1);
        }

        public override void Usar(ArmaJogador quem) => mundo.PedirPegar(id, quem.Inventario);
    }
}
