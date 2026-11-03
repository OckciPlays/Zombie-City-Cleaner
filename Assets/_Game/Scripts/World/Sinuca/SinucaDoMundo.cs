using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Uma mesa de sinuca na rede: a chave dela (a mesma em todas as máquinas), onde ela fica (para a distância dos
    /// pedidos), se tem as bolas, quem joga, as bolas que caíram e a rodada (cada jogo arrumado).
    /// </summary>
    public struct MesaNaRede : INetworkSerializeByMemcpy, IEquatable<MesaNaRede>
    {
        public ulong chave, jogador;
        public Vector3 centro;
        public int rodada;
        public ushort fora;
        public bool comBolas;

        public bool Equals(MesaNaRede o) => chave == o.chave && jogador == o.jogador && centro == o.centro && rodada == o.rodada && fora == o.fora && comBolas == o.comBolas;
    }

    /// <summary>
    /// As mesas de sinuca que se jogam (01/11/2026, a pedido: "colocar o sistema de sinuca já no bunker e no mapa também,
    /// nas mesas de sinuca que existirem"), no "Mundo" da cena (e no "Sinuca" da cena de teste). Cada mesa é o móvel da
    /// mobília nova (<c>MesaSinuca</c>) com a <see cref="MesaDeSinuca"/> (<see cref="Ligar"/>: as casas, nos móveis de perto,
    /// e o bunker da família, nos andares): ela aparece e some com os móveis, e o jogo dela fica aqui, pela chave.
    /// - **A rede** (o anfitrião manda): uma lista das mesas já usadas (<see cref="MesaNaRede"/>) e a das bolas (16 por
    ///   mesa, na ordem da lista). A mesa entra na lista no primeiro pedido (as bolas arrumadas, sem estar na mesa). Cada
    ///   pedido confere o jogador perto da mesa (<see cref="distanciaMaxima"/>) e o item certo.
    /// - **A tacada:** quem joga simula na máquina dele e manda os quadros pelos outros (<see cref="MandarQuadro"/>); quando
    ///   as bolas param, manda o fim, e o anfitrião grava.
    /// </summary>
    public class SinucaDoMundo : NetworkBehaviour
    {
        [Tooltip("As malhas dos 4 tacos (as do chão, na ordem do enum Item), para o taco que mira.")]
        public Mesh[] tacos;
        [Tooltip("O material do taco que mira (o da paleta) e o das linhas da mira (sem luz).")]
        public Material materialDoTaco, materialDaLinha;
        [Tooltip("Até onde o anfitrião aceita os pedidos (m, dos pés do jogador ao meio da mesa).")]
        public float distanciaMaxima = 3.5f;

        public const ulong Ninguem = ulong.MaxValue;
        /// <summary>A parte das chaves das mesas (a da obra e do ponto, como as partes que abrem).</summary>
        public const int ParteDaMesa = 1000;

        public static SinucaDoMundo Instancia { get; private set; }

        NetworkList<MesaNaRede> _mesas;
        NetworkList<Vector2> _bolas;
        // as mesas desta máquina (as que estão de perto agora), pela chave
        readonly Dictionary<ulong, MesaDeSinuca> _naCena = new Dictionary<ulong, MesaDeSinuca>();

        void Awake()
        {
            _mesas = new NetworkList<MesaNaRede>();
            _bolas = new NetworkList<Vector2>();
        }

        void OnEnable() => Instancia = this;

        void OnDisable()
        {
            if (Instancia == this) Instancia = null;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _mesas.Clear();
                _bolas.Clear();
            }
            _mesas.OnListChanged += AoMudarMesas;
            _bolas.OnListChanged += AoMudarBolas;
            foreach (var m in _naCena.Values) m.Confirmado();
        }

        public override void OnNetworkDespawn()
        {
            _mesas.OnListChanged -= AoMudarMesas;
            _bolas.OnListChanged -= AoMudarBolas;
        }

        void AoMudarMesas(NetworkListEvent<MesaNaRede> e)
        {
            if (e.Index >= 0 && e.Index < _mesas.Count) Avisar(_mesas[e.Index].chave);
            else foreach (var m in _naCena.Values) m.Confirmado();
        }

        void AoMudarBolas(NetworkListEvent<Vector2> e)
        {
            int k = e.Index / FisicaDaSinuca.Bolas;
            if (e.Index >= 0 && k < _mesas.Count) Avisar(_mesas[k].chave);
        }

        void Avisar(ulong chave)
        {
            if (_naCena.TryGetValue(chave, out var m) && m != null) m.Confirmado();
        }

        // ------------------------------------------------------------------ as mesas da cena

        /// <summary>
        /// Faz do móvel da mesa de sinuca (o prefab <c>MesaSinuca</c> da mobília nova, já posto e na escala dele) uma mesa
        /// que se joga: a <see cref="MesaDeSinuca"/> com a chave, as 16 bolas (as filhas <c>.Bola.n</c>, sem colisor, escondidas
        /// até as bolas irem para a mesa) e o gatilho da mira em volta (um pouco maior que a mesa, fora do NavMesh).
        /// </summary>
        public static MesaDeSinuca Ligar(GameObject movel, ulong chave)
        {
            var mesa = movel.AddComponent<MesaDeSinuca>();
            mesa.chave = chave;
            mesa.mesa = movel.transform;
            mesa.bolas = new Transform[FisicaDaSinuca.Bolas];
            foreach (Transform filho in movel.transform)
            {
                int k = filho.name.IndexOf(".Bola.", StringComparison.Ordinal);
                if (k < 0 || !int.TryParse(filho.name.Substring(k + 6), out int n) || n < 0 || n >= FisicaDaSinuca.Bolas) continue;
                mesa.bolas[n] = filho;
                foreach (var c in filho.GetComponents<Collider>()) Destroy(c);
                filho.gameObject.SetActive(false);
            }
            var mira = new GameObject("Mira") { layer = 2 };
            mira.transform.SetParent(movel.transform, false);
            var gatilho = mira.AddComponent<BoxCollider>();
            gatilho.isTrigger = true;
            gatilho.center = new Vector3(0f, 0.46f, 0f);
            gatilho.size = new Vector3(2.4f, 0.96f, 1.42f);
            mira.AddComponent<MesaDeSinucaNaMira>().mesa = mesa;
            var s = Instancia;
            if (s != null)
            {
                mesa.tacos = s.tacos;
                mesa.materialDoTaco = s.materialDoTaco;
                mesa.materialDaLinha = s.materialDaLinha;
            }
            return mesa;
        }

        public void Registrar(MesaDeSinuca mesa)
        {
            if (mesa != null) _naCena[mesa.chave] = mesa;
        }

        public void Esquecer(MesaDeSinuca mesa)
        {
            if (mesa != null && _naCena.TryGetValue(mesa.chave, out var m) && m == mesa) _naCena.Remove(mesa.chave);
        }

        // ------------------------------------------------------------------ o que a mesa lê

        int Indice(ulong chave)
        {
            for (int i = 0; i < _mesas.Count; i++)
                if (_mesas[i].chave == chave) return i;
            return -1;
        }

        /// <summary>O estado da mesa na rede (false: ninguém usou ela ainda, sem bolas e livre).</summary>
        public bool Estado(ulong chave, out MesaNaRede mesa)
        {
            int i = IsSpawned ? Indice(chave) : -1;
            mesa = i >= 0 ? _mesas[i] : new MesaNaRede { chave = chave, jogador = Ninguem };
            return i >= 0;
        }

        /// <summary>As posições das 16 bolas da mesa (as do triângulo, se ninguém usou ela ainda).</summary>
        public void Posicoes(ulong chave, Vector2[] pos)
        {
            int i = IsSpawned ? Indice(chave) : -1;
            if (i < 0 || _bolas.Count < (i + 1) * FisicaDaSinuca.Bolas)
            {
                var arrumadas = FisicaDaSinuca.Arrumadas(1);
                for (int b = 0; b < pos.Length; b++) pos[b] = arrumadas[b];
                return;
            }
            for (int b = 0; b < pos.Length && b < FisicaDaSinuca.Bolas; b++) pos[b] = _bolas[i * FisicaDaSinuca.Bolas + b];
        }

        // ------------------------------------------------------------------ os pedidos (do jogador local)

        public void PorBolas(MesaDeSinuca mesa) => PorBolasRpc(mesa.chave, mesa.Centro);
        public void RecolherBolas(MesaDeSinuca mesa) => RecolherBolasRpc(mesa.chave, mesa.Centro);
        public void Jogar(MesaDeSinuca mesa) => JogarRpc(mesa.chave, mesa.Centro);
        public void Sair(MesaDeSinuca mesa) => SairRpc(mesa.chave);
        public void Fim(MesaDeSinuca mesa, QuadroDaSinuca final, bool recomecar) => FimRpc(mesa.chave, final, recomecar);
        public void MandarQuadro(MesaDeSinuca mesa, QuadroDaSinuca quadro) => QuadroRpc(mesa.chave, quadro);

        // ------------------------------------------------------------------ no anfitrião

        // a mesa da chave (entra na lista no primeiro pedido, perto do jogador que pediu) e o inventário dele
        bool Quem(RpcParams parametros, ulong chave, Vector3 centro, out InventarioJogador inventario, out int i)
        {
            inventario = null;
            i = -1;
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return false;
            i = Indice(chave);
            var onde = i >= 0 ? _mesas[i].centro : centro;
            var d = cliente.PlayerObject.transform.position - onde;
            if (Mathf.Abs(d.y) > 2.5f) return false;
            d.y = 0f;
            if (d.magnitude > distanciaMaxima) return false;
            inventario = cliente.PlayerObject.GetComponent<InventarioJogador>();
            if (inventario == null) return false;
            if (i < 0)
            {
                _mesas.Add(new MesaNaRede { chave = chave, centro = centro, jogador = Ninguem });
                foreach (var p in FisicaDaSinuca.Arrumadas(UnityEngine.Random.Range(0, int.MaxValue))) _bolas.Add(p);
                i = _mesas.Count - 1;
            }
            return true;
        }

        // o jogo arrumado: o triângulo e a branca na marca, nenhuma caída
        void Arrumar(int i)
        {
            var pos = FisicaDaSinuca.Arrumadas(UnityEngine.Random.Range(0, int.MaxValue));
            for (int b = 0; b < pos.Length; b++) _bolas[i * FisicaDaSinuca.Bolas + b] = pos[b];
            var m = _mesas[i];
            m.fora = 0;
            m.rodada++;
            _mesas[i] = m;
        }

        [Rpc(SendTo.Server)]
        void PorBolasRpc(ulong chave, Vector3 centro, RpcParams parametros = default)
        {
            if (!Quem(parametros, chave, centro, out var inventario, out int i) || _mesas[i].comBolas) return;
            ushort caixa = inventario.Primeiro(Item.CaixaDeBolas);
            if (caixa == 0 || !inventario.TrocarItem(caixa, Item.CaixaDeBolasVazia)) return;
            Arrumar(i);
            var m = _mesas[i];
            m.comBolas = true;
            _mesas[i] = m;
        }

        [Rpc(SendTo.Server)]
        void RecolherBolasRpc(ulong chave, Vector3 centro, RpcParams parametros = default)
        {
            if (!Quem(parametros, chave, centro, out var inventario, out int i)) return;
            var m = _mesas[i];
            if (!m.comBolas || m.jogador != Ninguem) return;
            ushort caixa = inventario.Primeiro(Item.CaixaDeBolasVazia);
            if (caixa == 0 || !inventario.TrocarItem(caixa, Item.CaixaDeBolas)) return;
            m.comBolas = false;
            _mesas[i] = m;
        }

        [Rpc(SendTo.Server)]
        void JogarRpc(ulong chave, Vector3 centro, RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            if (!Quem(parametros, chave, centro, out var inventario, out int i)) return;
            var m = _mesas[i];
            if (!m.comBolas || (m.jogador != Ninguem && m.jogador != quem) || !Itens.EhTaco(inventario.NaMao)) return;
            // um jogador numa mesa só
            for (int k = 0; k < _mesas.Count; k++)
                if (k != i && _mesas[k].jogador == quem) Liberar(k);
            m.jogador = quem;
            _mesas[i] = m;
        }

        [Rpc(SendTo.Server)]
        void SairRpc(ulong chave, RpcParams parametros = default)
        {
            int i = Indice(chave);
            if (i >= 0 && _mesas[i].jogador == parametros.Receive.SenderClientId) Liberar(i, false);
        }

        // a mesa fica livre (com 'voltar', as bolas voltam para o que o anfitrião gravou: quem jogava saiu no meio da tacada)
        void Liberar(int i, bool voltar = true)
        {
            var m = _mesas[i];
            m.jogador = Ninguem;
            if (voltar) m.rodada++;
            _mesas[i] = m;
        }

        [Rpc(SendTo.Server)]
        void FimRpc(ulong chave, QuadroDaSinuca final, bool recomecar, RpcParams parametros = default)
        {
            int i = Indice(chave);
            if (i < 0 || _mesas[i].jogador != parametros.Receive.SenderClientId || final.pos == null || !_mesas[i].comBolas) return;
            if (recomecar)
            {
                Arrumar(i);
                return;
            }
            for (int b = 0; b < FisicaDaSinuca.Bolas; b++)
                if (_bolas[i * FisicaDaSinuca.Bolas + b] != final.pos[b]) _bolas[i * FisicaDaSinuca.Bolas + b] = final.pos[b];
            var m = _mesas[i];
            m.fora = final.fora;
            _mesas[i] = m;
        }

        /// <summary>Quem joga manda um quadro da tacada para os outros (a mesa de cada um, se estiver de perto, mostra ele).</summary>
        [Rpc(SendTo.NotMe)]
        void QuadroRpc(ulong chave, QuadroDaSinuca quadro, RpcParams parametros = default)
        {
            if (quadro.pos == null || !_naCena.TryGetValue(chave, out var mesa) || mesa == null) return;
            if (!Estado(chave, out var m) || m.jogador != parametros.Receive.SenderClientId) return;
            mesa.ReceberQuadro(quadro);
        }

        void Update()
        {
            // quem jogava saiu da partida: a mesa fica livre, e as bolas voltam para onde o anfitrião gravou
            if (!IsSpawned || !IsServer) return;
            for (int i = 0; i < _mesas.Count; i++)
                if (_mesas[i].jogador != Ninguem && !NetworkManager.ConnectedClients.ContainsKey(_mesas[i].jogador)) Liberar(i);
        }
    }
}
