using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Um quadro do jogo: onde estão as 16 bolas (no plano da mesa), as que já caíram e o que soou desde o quadro
    /// anterior (a batida mais forte entre bolas e na tabela, de 0 a 255, as caçapas e se teve tacada).
    /// </summary>
    public struct QuadroDaSinuca : INetworkSerializable
    {
        public Vector2[] pos;
        public ushort fora;
        public byte bola, tabela, cacapas, tacada;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            if (s.IsReader) pos = new Vector2[FisicaDaSinuca.Bolas];
            for (int i = 0; i < FisicaDaSinuca.Bolas; i++) s.SerializeValue(ref pos[i]);
            s.SerializeValue(ref fora);
            s.SerializeValue(ref bola);
            s.SerializeValue(ref tabela);
            s.SerializeValue(ref cacapas);
            s.SerializeValue(ref tacada);
        }
    }

    /// <summary>
    /// A mesa de sinuca que se joga (por enquanto só na cena de teste da sinuca; a pedido em 29/10/2026). A mesa é o
    /// modelo da mobília nova (<c>MesaSinuca</c>, na escala dos móveis), e as 16 bolas são as filhas dele.
    /// - **As bolas** vêm numa caixa (o item <see cref="Item.CaixaDeBolas"/>): com ela na mochila, E na mesa põe as bolas
    ///   arrumadas no triângulo, e a caixa fica vazia na mochila; com a vazia, E recolhe as bolas (ninguém jogando).
    /// - **Jogar:** com um taco na mão (<see cref="Itens.EhTaco"/>), E na mesa entra no jogo: a câmera vai para cima da
    ///   mesa, o mouse mira a branca e o clique segurado dá a força (veja <c>MesaDeSinuca.Jogo.cs</c>). Um jogador por vez;
    ///   o corpo dele fica parado onde estava, e os outros só veem as bolas andando.
    /// - **A rede:** o anfitrião guarda as posições (<see cref="_posicoes"/>, as que caíram e quem joga). Quem joga simula
    ///   a tacada (<see cref="FisicaDaSinuca"/>) e manda um quadro a cada 0,05 s para os outros; quando as bolas param,
    ///   manda o fim para o anfitrião, que grava. A preta na caçapa, ou todas as 15, recomeça o jogo (o triângulo de novo).
    /// </summary>
    [DefaultExecutionOrder(200)]   // a câmera de quem joga é posta depois de tudo o que mexe nela
    public partial class MesaDeSinuca : NetworkBehaviour
    {
        [Tooltip("A mesa (o modelo da mobília nova, na escala dos móveis): o pano é o plano dela; as bolas são filhas dela.")]
        public Transform mesa;
        [Tooltip("As 16 bolas (MesaSinuca.Bola.0 a .15), filhas da mesa.")]
        public Transform[] bolas;
        [Tooltip("As malhas dos 4 tacos (as do chão, na ordem do enum Item), para o taco que mira; o material é o da paleta.")]
        public Mesh[] tacos;
        public Material materialDoTaco;
        [Tooltip("O material das linhas da mira (sem luz).")]
        public Material materialDaLinha;
        [Tooltip("A altura do pano, no tamanho real da mesa (sem a escala).")]
        public float alturaDoPano = 0.78f;
        [Tooltip("Até onde o anfitrião aceita os pedidos (m, dos pés do jogador ao meio da mesa).")]
        public float distanciaMaxima = 3.5f;

        const ulong Ninguem = ulong.MaxValue;

        public readonly NetworkVariable<bool> ComBolas = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<ulong> _jogador = new(Ninguem, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<ushort> _fora = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // a cada jogo arrumado (as bolas postas, ou a preta na caçapa), a vista pula para o triângulo sem deslizar
        readonly NetworkVariable<int> _rodada = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        NetworkList<Vector2> _posicoes;

        // a vista desta máquina: onde cada bola aparece, para onde ela vai e as que estão escondidas
        readonly Vector2[] _vista = new Vector2[FisicaDaSinuca.Bolas];
        readonly Vector2[] _alvo = new Vector2[FisicaDaSinuca.Bolas];
        ushort _foraDoAlvo;
        bool _pular = true;
        // o último quadro de quem joga (os outros mostram ele até o fim da tacada chegar no anfitrião)
        float _quadroAte;

        /// <summary>Alguém está jogando (o anfitrião aceitou).</summary>
        public bool Ocupada => _jogador.Value != Ninguem;
        public bool EuJogo => IsSpawned && _jogador.Value == NetworkManager.LocalClientId;

        void Awake() => _posicoes = new NetworkList<Vector2>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _jogador.Value = Ninguem;
                if (_posicoes.Count != FisicaDaSinuca.Bolas) Arrumar();
            }
            _posicoes.OnListChanged += AoMudarLista;
            _fora.OnValueChanged += AoMudarFora;
            _rodada.OnValueChanged += AoMudarRodada;
            _pular = true;
            LerAlvo();
        }

        public override void OnNetworkDespawn()
        {
            _posicoes.OnListChanged -= AoMudarLista;
            _fora.OnValueChanged -= AoMudarFora;
            _rodada.OnValueChanged -= AoMudarRodada;
            SairJa();
        }

        void AoMudarLista(NetworkListEvent<Vector2> e) => Confirmado();
        void AoMudarFora(ushort antes, ushort depois) => Confirmado();

        void AoMudarRodada(int antes, int depois)
        {
            _pular = true;
            Confirmado();
        }

        // o anfitrião gravou: a vista vai para o que está na rede
        void Confirmado()
        {
            _quadroAte = 0f;
            _esperandoAnfitriao = false;
            LerAlvo();
        }

        void LerAlvo()
        {
            for (int i = 0; i < FisicaDaSinuca.Bolas; i++) _alvo[i] = i < _posicoes.Count ? _posicoes[i] : Vector2.zero;
            _foraDoAlvo = _fora.Value;
        }

        // ------------------------------------------------------------------ no anfitrião

        // o jogo arrumado: o triângulo e a branca na marca, nenhuma caída
        void Arrumar()
        {
            var pos = FisicaDaSinuca.Arrumadas(Random.Range(0, int.MaxValue));
            if (_posicoes.Count != FisicaDaSinuca.Bolas)
            {
                _posicoes.Clear();
                foreach (var p in pos) _posicoes.Add(p);
            }
            else
                for (int i = 0; i < pos.Length; i++) _posicoes[i] = pos[i];
            _fora.Value = 0;
            _rodada.Value++;
        }

        // o jogador que mandou o pedido, se ele está perto da mesa (o inventário e a arma dele)
        bool Quem(RpcParams parametros, out InventarioJogador inventario)
        {
            inventario = null;
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return false;
            var d = cliente.PlayerObject.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude > distanciaMaxima) return false;
            inventario = cliente.PlayerObject.GetComponent<InventarioJogador>();
            return inventario != null;
        }

        [Rpc(SendTo.Server)]
        void PorBolasRpc(RpcParams parametros = default)
        {
            if (ComBolas.Value || !Quem(parametros, out var inventario)) return;
            ushort caixa = inventario.Primeiro(Item.CaixaDeBolas);
            if (caixa == 0 || !inventario.TrocarItem(caixa, Item.CaixaDeBolasVazia)) return;
            Arrumar();
            ComBolas.Value = true;
        }

        [Rpc(SendTo.Server)]
        void RecolherBolasRpc(RpcParams parametros = default)
        {
            if (!ComBolas.Value || Ocupada || !Quem(parametros, out var inventario)) return;
            ushort caixa = inventario.Primeiro(Item.CaixaDeBolasVazia);
            if (caixa == 0 || !inventario.TrocarItem(caixa, Item.CaixaDeBolas)) return;
            ComBolas.Value = false;
        }

        [Rpc(SendTo.Server)]
        void JogarRpc(RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            if (!ComBolas.Value || (Ocupada && _jogador.Value != quem) || !Quem(parametros, out var inventario)) return;
            if (!Itens.EhTaco(inventario.NaMao)) return;
            _jogador.Value = quem;
        }

        [Rpc(SendTo.Server)]
        void SairRpc(RpcParams parametros = default)
        {
            if (_jogador.Value == parametros.Receive.SenderClientId) _jogador.Value = Ninguem;
        }

        /// <summary>O fim de uma tacada (as bolas pararam): o anfitrião grava, ou arruma o jogo de novo.</summary>
        [Rpc(SendTo.Server)]
        void FimRpc(QuadroDaSinuca final, bool recomecar, RpcParams parametros = default)
        {
            if (_jogador.Value != parametros.Receive.SenderClientId || final.pos == null || !ComBolas.Value) return;
            if (recomecar)
            {
                Arrumar();
                return;
            }
            if (_posicoes.Count != FisicaDaSinuca.Bolas) Arrumar();
            for (int i = 0; i < FisicaDaSinuca.Bolas; i++)
                if (_posicoes[i] != final.pos[i]) _posicoes[i] = final.pos[i];
            _fora.Value = final.fora;
        }

        /// <summary>Quem joga manda um quadro da tacada para os outros.</summary>
        [Rpc(SendTo.NotMe)]
        void QuadroRpc(QuadroDaSinuca quadro, RpcParams parametros = default)
        {
            if (quadro.pos == null || parametros.Receive.SenderClientId != _jogador.Value) return;
            for (int i = 0; i < FisicaDaSinuca.Bolas; i++) _alvo[i] = quadro.pos[i];
            _foraDoAlvo = quadro.fora;
            _quadroAte = Time.time + 1.5f;
            // os sons, no meio da mesa
            var meio = mesa != null ? mesa.position + Vector3.up * alturaDoPano * mesa.lossyScale.y : transform.position;
            if (quadro.tacada != 0) Efeitos.Som(SomDaSinuca.Tacada, meio, 0.7f, true, 25f);
            if (quadro.bola > 4) Efeitos.Som(SomDaSinuca.Bolas, meio, quadro.bola / 255f, true, 25f);
            if (quadro.tabela > 4) Efeitos.Som(SomDaSinuca.Tabela, meio, quadro.tabela / 255f * 0.8f, true, 25f);
            if (quadro.cacapas > 0) Efeitos.Som(SomDaSinuca.Cacapa, meio, 0.8f, true, 25f);
        }

        void Update()
        {
            if (!IsSpawned) return;
            // quem jogava saiu da partida: a mesa fica livre, e as bolas voltam para onde o anfitrião gravou (se ele saiu
            // no meio de uma tacada, os outros viam as bolas andando)
            if (IsServer && Ocupada && !NetworkManager.ConnectedClients.ContainsKey(_jogador.Value))
            {
                _jogador.Value = Ninguem;
                _rodada.Value++;
            }
            AtualizarJogo();
            AtualizarVista();
        }

        // ------------------------------------------------------------------ a vista (todas as máquinas)

        void AtualizarVista()
        {
            if (bolas == null || mesa == null) return;
            ushort fora;
            if (_simulando)
            {
                // quem joga vê a própria simulação
                for (int i = 0; i < FisicaDaSinuca.Bolas; i++) _alvo[i] = _sim.Pos[i];
                fora = _sim.Fora;
                _pular = false;
                Mostrar(true, fora);
                return;
            }
            if (_esperandoAnfitriao && Time.time > _esperarAte) Confirmado();
            if (!_esperandoAnfitriao && Time.time > _quadroAte && _quadroAte > 0f) Confirmado();
            fora = _foraDoAlvo;
            Mostrar(false, fora);
        }

        void Mostrar(bool direto, ushort fora)
        {
            bool com = ComBolas.Value;
            float k = 1f - Mathf.Exp(-22f * Time.deltaTime);
            float y = alturaDoPano + FisicaDaSinuca.Raio;
            for (int i = 0; i < FisicaDaSinuca.Bolas && i < bolas.Length; i++)
            {
                var b = bolas[i];
                if (b == null) continue;
                bool dentro = com && (fora & (1 << i)) == 0;
                if (b.gameObject.activeSelf != dentro) b.gameObject.SetActive(dentro);
                var antes = _vista[i];
                var alvo = _alvo[i];
                _vista[i] = direto || _pular || (alvo - antes).sqrMagnitude > 0.25f ? alvo : Vector2.Lerp(antes, alvo, k);
                if (!dentro) continue;
                b.localPosition = new Vector3(_vista[i].x, y, _vista[i].y);
                // a bola rola: gira em volta do eixo de lado do caminho, o quanto andou sobre o raio
                var anda = _vista[i] - antes;
                float d = anda.magnitude;
                if (d > 1e-5f && d < 0.3f)
                {
                    var eixo = Vector3.Cross(Vector3.up, new Vector3(anda.x, 0f, anda.y) / d);
                    b.localRotation = Quaternion.AngleAxis(d / FisicaDaSinuca.Raio * Mathf.Rad2Deg, eixo) * b.localRotation;
                }
            }
            _pular = false;
        }

        // ------------------------------------------------------------------ o E na mesa

        /// <summary>A dica embaixo da mira, para o jogador local.</summary>
        public string Dica(ArmaJogador quem)
        {
            var inventario = quem != null ? quem.Inventario : null;
            if (!IsSpawned || inventario == null || _jogando) return null;
            if (Ocupada) return "Alguém está jogando sinuca";
            bool taco = Itens.EhTaco(inventario.NaMao);
            if (!ComBolas.Value)
                return inventario.Primeiro(Item.CaixaDeBolas) != 0 ? "[E] Pôr as bolas na mesa"
                    : inventario.Primeiro(Item.CaixaDeBolasVazia) != 0 ? "A sua caixa de bolas está vazia" : "Mesa sem as bolas: ache uma caixa de bolas";
            if (taco) return "[E] Jogar sinuca";
            if (inventario.Primeiro(Item.CaixaDeBolasVazia) != 0) return "[E] Recolher as bolas   ·   segure um taco para jogar";
            return "Segure um taco para jogar";
        }

        /// <summary>O jogador local apertou E na mesa.</summary>
        public void Usar(ArmaJogador quem)
        {
            var inventario = quem != null ? quem.Inventario : null;
            if (!IsSpawned || inventario == null || _jogando || Ocupada) return;
            if (!ComBolas.Value)
            {
                if (inventario.Primeiro(Item.CaixaDeBolas) != 0)
                {
                    PorBolasRpc();
                    Efeitos.Som(SomDaSinuca.Cacapa, transform.position + Vector3.up, 0.5f, true, 20f);
                }
                return;
            }
            if (Itens.EhTaco(inventario.NaMao))
            {
                _armaQuePediu = quem;
                _pediuEm = Time.time;
                JogarRpc();
                return;
            }
            if (inventario.Primeiro(Item.CaixaDeBolasVazia) != 0)
            {
                RecolherBolasRpc();
                Efeitos.Som(SomDaSinuca.Cacapa, transform.position + Vector3.up, 0.5f, true, 20f);
            }
        }
    }
}
