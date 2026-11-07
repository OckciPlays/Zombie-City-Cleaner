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
    /// O saque nos móveis de perto (o Plano_Saque.md da pasta de arte, a parte 4):
    ///  - cada máquina sorteia o mesmo saque de cada construção (<see cref="MobiliaDaConstrucao.Saque"/>), e os itens entram
    ///    com os móveis de perto (<see cref="PorOSaque"/>): a malha do item como ele fica no chão, no lugar dele, em cima ou
    ///    dentro do móvel; o da gaveta, da prateleira da porta e da tampa vai com ela;
    ///  - a rede leva só o que foi pego (<see cref="_pegos"/>, as chaves; o anfitrião escreve): o E vai ao anfitrião
    ///    (<see cref="PegarRpc"/>), que acha o item pelos dados do mundo (o cliente pode estar longe dele), confere a
    ///    distância e se cabe na mochila, e põe a chave na lista. O que foi pego não volta.
    /// </summary>
    public partial class MobiliaNovaDoMundo
    {
        /// <summary>A parte das chaves do saque (<see cref="MobiliaNova.ChaveDaParte"/> com ela mais o lugar): as partes que abrem são de 0 a umas 20, e a mesa de sinuca, 1000.</summary>
        const int ParteDoSaque = 900;
        /// <summary>Até onde o anfitrião aceita o pedido de pegar (m, dos pés do jogador ao lugar do item com o móvel fechado: a gaveta aberta sai até uns 60 cm).</summary>
        const float DistanciaDePegar = 4.5f;

        NetworkList<ulong> _pegos;
        // os pegos desta máquina (o espelho da lista da rede) e os itens de perto, pela chave
        readonly HashSet<ulong> _pegosAqui = new HashSet<ulong>();
        readonly Dictionary<ulong, SaqueNaMira> _saqueVivo = new Dictionary<ulong, SaqueNaMira>();
        static readonly List<ItemDoSaque> _doMovel = new List<ItemDoSaque>();

        /// <summary>A chave de um item do saque na lista dos pegos (a obra, o ponto do móvel, a peça e o lugar).</summary>
        public static ulong ChaveDoSaque(ulong obra, int ponto, int peca, int lugar) => MobiliaNova.ChaveDaParte(obra, ponto, peca, ParteDoSaque + lugar);

        /// <summary>O item já foi pego (por alguém, na partida).</summary>
        public static bool Pego(ulong chave) => Instancia != null && Instancia._pegosAqui.Contains(chave);

        /// <summary>Quantos itens do saque já foram pegos na partida (o F1 mostra).</summary>
        public static int QuantosPegos => Instancia != null ? Instancia._pegosAqui.Count : 0;

        void AoPegar(NetworkListEvent<ulong> e) => SincronizarOSaque();

        /// <summary>Só no anfitrião: as chaves do saque já pego (o save, Salvar/Salvamento).</summary>
        public List<ulong> CopiaDoSaque()
        {
            var lista = new List<ulong>(_pegos.Count);
            foreach (var k in _pegos) lista.Add(k);
            return lista;
        }

        /// <summary>Só no anfitrião: o saque já pego do mundo salvo (o que foi pego não volta).</summary>
        public void RestaurarOSaque(List<ulong> pegos)
        {
            if (!IsServer || !IsSpawned) return;
            _pegos.Clear();
            foreach (var k in pegos) _pegos.Add(k);
        }

        // os itens de perto somem quando são pegos
        void SincronizarOSaque()
        {
            if (!IsSpawned) return;
            _pegosAqui.Clear();
            foreach (var k in _pegos) _pegosAqui.Add(k);
            foreach (var (k, s) in _saqueVivo)
                if (s != null && _pegosAqui.Contains(k)) s.gameObject.SetActive(false);
        }

        /// <summary>
        /// Os itens do saque da construção que vão neste móvel de perto (o ponto e a peça dele), em 'saida'. Com as partes
        /// ainda fechadas (antes de elas ficarem como estão na rede): o lugar de cada item é o do móvel fechado.
        /// 'caixaDoPonto': a caixa do móvel nas coordenadas dele (o móvel antigo da farmácia e do hospital, 05/11/2026, que não tem malha: o
        /// objeto vazio no ponto, com a caixa do ponto); sem ela, a da malha do móvel.
        /// </summary>
        internal void PorOSaque(MobiliaDaConstrucao m, GameObject movel, string modelo, ulong obra, int ponto, int peca, List<SaqueNaMira> saida, Bounds? caixaDoPonto = null)
        {
            var itens = ItensNoMundo.Instancia;
            if (itens == null) return;
            _doMovel.Clear();
            foreach (var it in m.Saque)
                if (it.ponto == ponto && it.peca == peca) _doMovel.Add(it);
            if (_doMovel.Count == 0) return;
            var raiz = movel.transform;
            var corpo = movel.GetComponent<MeshFilter>();
            var caixa = caixaDoPonto ?? (corpo != null && corpo.sharedMesh != null ? corpo.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.zero));
            var obraT = m.transform;
            foreach (var it in _doMovel)
            {
                ulong chave = ChaveDoSaque(obra, ponto, peca, it.lugar);
                if (_pegosAqui.Contains(chave)) continue;
                var item = (Item)it.item;
                var malha = itens.MalhaDe(item);
                if (malha == null) continue;
                var parte = it.parte != null ? raiz.Find($"{modelo}.{it.parte}") : null;

                var go = new GameObject($"Saque: {Itens.Nome(item)}") { layer = CamadaDasPartes };
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = itens.MaterialDe(item);
                r.shadowCastingMode = UI.Graficos.SombraDosMoveis ? ShadowCastingMode.On : ShadowCastingMode.Off;
                // a roupa com as cores do catálogo
                var roupas = Itens.EhRoupa(item) ? Roupas.ItensDasRoupas.Carregar() : null;
                if (roupas != null) roupas.Pintar(r, item, 0);

                // como no chão (ItensNoMundo): as armas deitadas de lado, o meio de baixo da malha girada no apoio
                var giro = Quaternion.Euler(0f, it.giro, 0f) * (Itens.EhArma(item) || Itens.EhBranca(item) ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity);
                var b = malha.bounds;
                float baixo = float.MaxValue;
                Vector3 meio = Vector3.zero;
                for (int c = 0; c < 8; c++)
                {
                    var p = giro * new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                    baixo = Mathf.Min(baixo, p.y);
                    meio += p / 8f;
                }
                go.transform.SetPositionAndRotation(obraT.TransformPoint(it.apoio + new Vector3(-meio.x, -baixo + 0.002f, -meio.z)), obraT.rotation * giro);
                // o da gaveta (e da prateleira da porta, e da tampa) vai com ela; o resto, com o móvel
                go.transform.SetParent(it.naParte && parte != null ? parte : raiz, true);

                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = b.center;
                col.size = Vector3.Max(b.size + Vector3.one * 0.03f, Vector3.one * 0.10f);

                var s = go.AddComponent<SaqueNaMira>();
                s.chave = chave;
                s.obra = obra;
                s.ponto = ponto;
                s.peca = peca;
                s.lugar = it.lugar;
                s.item = item;
                s.raridade = it.raridade;
                s.qtd = Itens.EhArma(item) ? Mathf.RoundToInt(it.pente * Itens.QuantidadePadrao(item))
                      : Itens.EmPilha(item) ? Itens.Quantos(item, it.qtd) : -1;
                s.porta = parte != null ? parte.GetComponent<ParteQueAbre>() : null;
                s.movel = raiz;
                s.caixaDoMovel = caixa;
                _saqueVivo[chave] = s;
                saida.Add(s);
            }
        }

        /// <summary>Um item de perto saiu (os móveis de perto da construção saíram).</summary>
        internal void EsquecerOSaque(SaqueNaMira s)
        {
            if (s != null && _saqueVivo.TryGetValue(s.chave, out var v) && v == s) _saqueVivo.Remove(s.chave);
        }

        /// <summary>O jogador local pede para pegar o item (fora de uma partida, nada).</summary>
        public void PedirPegar(SaqueNaMira s, InventarioJogador quem)
        {
            if (!IsSpawned || s == null || quem == null) return;
            quem.AvisarQuePegou();
            PegarRpc(s.obra, s.ponto, s.peca, s.lugar);
        }

        /// <summary>
        /// Quem joga → anfitrião: pega o item do saque. O anfitrião acha o item pelos dados do mundo (as construções em volta
        /// de todos os jogadores estão carregadas nele), confere se ainda está lá e a distância, e põe na mochila; sem lugar,
        /// ele fica onde está (a dica avisa). A pilha que só cabe em parte: o resto cai aos pés dele.
        /// </summary>
        // o Sortudo (06/11/2026): pegando um item das casas, às vezes ele acha mais um (o Olho bom, um normal em 10%; o
        // Garimpeiro, um incomum em 8%; o Pé de coelho, um raro em 4%), do catálogo do saque (sem as armas e as roupas)
        static void Sorte(InventarioJogador inventario, Vector3 pes, float giro)
        {
            var j = inventario.GetComponent<JogadorRede>();
            if (Habilidades.Tem(j, Player.Habilidade.PeDeCoelho) && Random.value < 0.04f && AchouMais(inventario, Raridade.Raro, pes, giro)) return;
            if (Habilidades.Tem(j, Player.Habilidade.Garimpeiro) && Random.value < 0.08f && AchouMais(inventario, Raridade.Incomum, pes, giro)) return;
            if (Habilidades.Tem(j, Player.Habilidade.OlhoBom) && Random.value < 0.10f) AchouMais(inventario, Raridade.Normal, pes, giro);
        }

        static bool AchouMais(InventarioJogador inventario, Raridade r, Vector3 pes, float giro)
        {
            var lista = CatalogoDoSaque.DaRaridade(r);
            if (lista == null || lista.Count == 0) return false;
            for (int t = 0; t < 8; t++)
            {
                var c = lista[Random.Range(0, lista.Count)];
                var item = (Item)c.numero;
                if (item == Item.Nada || Itens.EhArma(item) || Itens.EhRoupa(item)) continue;
                int n = Itens.Quantos(item, Random.Range(c.min, c.max + 1));
                bool cabe = Itens.EmPilha(item) ? inventario.ReceberEmPilha(item, n) < n : inventario.Receber(item, -1, 0);
                if (!cabe) ItensNoMundo.Instancia?.Criar(item, pes, giro + 40f, Itens.EmPilha(item) ? n : 1);
                inventario.AvisarDono($"Sorte: achou também {Itens.ComNumero(item, n)}");
                return true;
            }
            return false;
        }

        [Rpc(SendTo.Server)]
        void PegarRpc(ulong obra, int ponto, int peca, int lugar, RpcParams parametros = default)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return;
            var inventario = cliente.PlayerObject.GetComponent<InventarioJogador>();
            ulong chave = ChaveDoSaque(obra, ponto, peca, lugar);
            if (inventario == null || _pegosAqui.Contains(chave)) return;
            var m = MobiliaDaConstrucao.DaObra(obra);
            if (m == null || !m.AcharNoSaque(ponto, peca, lugar, out var it)) return;
            var onde = m.transform.TransformPoint(it.apoio);
            var pes = cliente.PlayerObject.transform.position;
            if ((pes - onde).sqrMagnitude > DistanciaDePegar * DistanciaDePegar) return;
            var item = (Item)it.item;
            bool pegou;
            if (Itens.EmPilha(item))
            {
                int n = Itens.Quantos(item, it.qtd);
                // o Olho bom (06/11/2026): a pilha vem com 25% a mais (pelo menos um)
                if (Habilidades.Tem(inventario.GetComponent<JogadorRede>(), Player.Habilidade.OlhoBom)) n += Mathf.Max(1, Mathf.CeilToInt(n * 0.25f));
                int sobra = inventario.ReceberEmPilha(item, n);
                pegou = sobra < n;
                if (pegou && sobra > 0) ItensNoMundo.Instancia?.Criar(item, pes, cliente.PlayerObject.transform.eulerAngles.y + 40f, sobra);
            }
            else if (Itens.EhArma(item)) pegou = inventario.Receber(item, Mathf.RoundToInt(it.pente * Itens.QuantidadePadrao(item)), it.desgaste);
            else pegou = inventario.Receber(item, -1, it.desgaste);
            if (pegou)
            {
                _pegos.Add(chave);
                _pegosAqui.Add(chave);
                inventario.GanharXp(Mathf.Clamp((int)it.raridade + 1, 1, 4), "saque");   // (06/11/2026, a árvore de habilidades)
                Sorte(inventario, pes, cliente.PlayerObject.transform.eulerAngles.y);
            }
        }
    }
}
