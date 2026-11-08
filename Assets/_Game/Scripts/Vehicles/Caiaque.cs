using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O caiaque de dois lugares (06/11/2026, a pedido: "criar um kayak que dê para a gente subir e colocar no lago para
    /// poder pescar"; "dois lugares"): o lugar 0, atrás, rema (W e S para a frente e para trás, A e D viram); o lugar 1, na
    /// frente, fica com as mãos livres (a vara de pesca, a arma). Fica na beira do lago do mundo, do lado do bunker
    /// (<see cref="World.MundoDaPartida.PorOCaiaque"/>), e vai no save do mundo.
    /// <para>A rede: como o carro, o <see cref="NetworkTransform"/> do dono, e o dono é quem rema (sem ninguém remando, o
    /// anfitrião). Os lugares são do anfitrião (<see cref="EntrarRpc"/>, <see cref="SairRpc"/>); cada máquina senta os
    /// jogadores nos bancos (<see cref="PassageiroDoCaiaque"/>). Ele boia no nível da água (<see cref="Agua"/>), balança
    /// e encalha onde a água fica rasa na proa.</para>
    /// <para>
    /// Desde 07/11/2026 (a pedido: "o certo é pescar quando paramos, mesmo se estivermos sozinho"; a remada alternada com
    /// som e rastro; "não pode virar, mas vai ficar balançando"; o compartimento):
    /// <list type="bullet">
    /// <item><b>O remo na mão</b> (<see cref="RemoNaMao"/>, do dono): remando, ele fica na mão direita do corpo de quem
    /// rema (a pegada do <c>Blender/agua_animacoes.py</c>) e o corpo toca o Remar; parado um pouco, quem rema larga o remo
    /// no casco e fica com as mãos livres (pesca, atira); W ou S pega de novo (<see cref="PassageiroDoCaiaque"/>).</item>
    /// <item><b>A remada:</b> cada pá entra na água no seu tempo do Remar (a esquerda no começo, a direita no meio), com o
    /// barulho, o respingo e a proa indo um pouco para o outro lado; atrás, o rastro de ondinhas.</item>
    /// <item><b>O tempo:</b> com vento e chuva ele balança mais (até umas 4 vezes), sem virar.</item>
    /// <item><b>O compartimento:</b> a escotilha de trás (<see cref="CompartimentoDoCaiaque"/>), uma estante de 2 × 2 das
    /// estantes do bunker (<see cref="Compartimento"/>).</item>
    /// </list>
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(120)]
    public class Caiaque : NetworkBehaviour
    {
        public const int Lugares = 2;

        [Tooltip("Os bancos: 0 atrás (quem rema), 1 na frente (quem pesca). O quadril de quem senta.")]
        public Transform[] assentos = new Transform[Lugares];
        [Tooltip("O remo (na mão de quem rema; sem ninguém remando, deitado no casco).")]
        public Transform remo;
        public float velocidadeMaxima = 3.2f, re = 1.4f, giroMaximo = 55f;
        [Tooltip("Do comprimento do caiaque, a metade (m): a proa, que encalha na água rasa.")]
        public float meioComprimento = 2.3f;

        /// <summary>
        /// A pegada do remo na mão direita do corpo (07/11/2026, a do Remar e do Remar_Parado, igual em todos os quadros): o
        /// lugar e o giro da raiz do remo no osso hand.R. O cabo vai pelo +X da mão.
        /// </summary>
        static readonly Vector3 PegadaNaMao = new Vector3(0.30f, 0.072f, -0.03f);
        static readonly Quaternion GiroNaMao = new Quaternion(0f, 0.60877f, 0.79335f, 0f);
        /// <summary>A volta da remada (s), a do clipe Remar; as pás entram na água no começo (a esquerda) e no meio (a direita).</summary>
        public const float VoltaDaRemada = 1.6f;

        public static readonly List<Caiaque> Todos = new List<Caiaque>();

        /// <summary>Quem está em cada lugar (o id do cliente mais 1; 0, ninguém).</summary>
        [Serializable]
        public struct Ocupantes : INetworkSerializeByMemcpy, IEquatable<Ocupantes>
        {
            public ulong a, b;
            public ulong this[int i] => i == 0 ? a : b;
            public bool Equals(Ocupantes o) => a == o.a && b == o.b;
            public override bool Equals(object o) => o is Ocupantes x && Equals(x);
            public override int GetHashCode() => a.GetHashCode() ^ (b.GetHashCode() << 1);
        }

        readonly NetworkVariable<Ocupantes> _ocupantes = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // o remo de quem rema (o dono escreve; 07/11/2026): 0 deitado no casco, 1 na mão (parado), 2 remando
        readonly NetworkVariable<byte> _remo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        InputAction _mover;
        float _velocidade, _giro, _proximaOnda, _cicloAntes;
        Vector3 _ultima;
        Vector3 _remoLugar, _escotilha;
        Quaternion _remoGiro;
        JogadorRede _remadorVisto;
        Animator _animadorVisto;
        Transform _maoDoRemador;
        Renderer[] _desenhoDoRemo;
        bool _remoSoSombra;
        static readonly int RemarId = Animator.StringToHash("Remar");

        /// <summary>A velocidade dele para a frente (m/s; de ré, negativa), medida em cada máquina.</summary>
        public float Velocidade { get; private set; }
        /// <summary>O remo está na mão de quem rema (com alguém no lugar 0).</summary>
        public bool RemoNaMao => IsSpawned && Ocupante(0) != 0 && _remo.Value >= 1;
        /// <summary>Quem rema está remando agora (o W, o S, o A ou o D apertados).</summary>
        public bool Remando => IsSpawned && Ocupante(0) != 0 && _remo.Value == 2;
        /// <summary>Onde a remada está (0 a 1), pelo Remar do corpo de quem rema; -1 sem remada.</summary>
        public float CicloDaRemada { get; private set; } = -1f;
        /// <summary>O número da estante do compartimento dele (as estantes do bunker).</summary>
        public int Compartimento => EstantesDoBunker.BaseDosCaiaques + Mathf.Max(0, Todos.IndexOf(this));
        /// <summary>A escotilha de trás (o compartimento), no mundo.</summary>
        public Vector3 PontoDoCompartimento => transform.TransformPoint(_escotilha);

        /// <summary>O caiaque do compartimento (o número da estante), ou null.</summary>
        public static Caiaque DoCompartimento(int estante)
        {
            int i = estante - EstantesDoBunker.BaseDosCaiaques;
            return i >= 0 && i < Todos.Count ? Todos[i] : null;
        }

        void OnEnable() => Todos.Add(this);
        void OnDisable() => Todos.Remove(this);

        void Awake()
        {
            if (remo != null)
            {
                _remoLugar = remo.localPosition;
                _remoGiro = remo.localRotation;
                _desenhoDoRemo = remo.GetComponentsInChildren<Renderer>(true);
            }
            PorAEscotilha();
        }

        // a escotilha de trás: uma caixa-gatilho em cima do convés, atrás do banco de quem rema (o E no compartimento)
        void PorAEscotilha()
        {
            Vector3 banco = assentos != null && assentos.Length > 0 && assentos[0] != null ? transform.InverseTransformPoint(assentos[0].position) : new Vector3(0f, 0.2f, -0.5f);
            _escotilha = new Vector3(0f, banco.y + 0.16f, Mathf.Max(-meioComprimento + 0.45f, banco.z - 0.85f));
            var go = new GameObject("Compartimento");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = _escotilha;
            var caixa = go.AddComponent<BoxCollider>();
            caixa.isTrigger = true;
            caixa.size = new Vector3(0.55f, 0.35f, 0.5f);
            go.AddComponent<CompartimentoDoCaiaque>().caiaque = this;
        }

        public override void OnNetworkSpawn()
        {
            _mover = InputSystem.actions.FindAction("Player/Move", true);
            _ultima = transform.position;
        }

        public override void OnNetworkDespawn()
        {
            foreach (var j in JogadorRede.Todos)
            {
                var p = j != null ? j.GetComponent<PassageiroDoCaiaque>() : null;
                if (p != null && p.Caiaque == this) p.Levantar();
            }
        }

        /// <summary>Quem está no lugar (o id do cliente mais 1; 0, ninguém).</summary>
        public ulong Ocupante(int lugar) => lugar >= 0 && lugar < Lugares ? _ocupantes.Value[lugar] : 0;

        /// <summary>O lugar do cliente aqui (-1, nenhum).</summary>
        public int LugarDe(ulong cliente)
        {
            for (int i = 0; i < Lugares; i++) if (Ocupante(i) == cliente + 1) return i;
            return -1;
        }

        /// <summary>O lugar livre (o de remar primeiro); -1 com os dois ocupados.</summary>
        public int LugarLivre()
        {
            for (int i = 0; i < Lugares; i++) if (Ocupante(i) == 0) return i;
            return -1;
        }

        /// <summary>No dono (quem rema): o remo na mão (remando ou parado) ou largado no casco (as mãos livres).</summary>
        public void PorORemo(bool naMao, bool remando)
        {
            byte v = (byte)(!naMao ? 0 : remando ? 2 : 1);
            if (IsSpawned && IsOwner && _remo.Value != v) _remo.Value = v;
        }

        // ------------------------------------------------------------------------------------------ entrar e sair

        public void PedirEntrar(int lugar)
        {
            if (IsSpawned) EntrarRpc((byte)lugar);
        }

        [Rpc(SendTo.Server)]
        void EntrarRpc(byte lugar, RpcParams parametros = default)
        {
            ulong c = parametros.Receive.SenderClientId;
            if (lugar >= Lugares || Ocupante(lugar) != 0 || LugarDe(c) >= 0) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(c, out var cliente) || cliente.PlayerObject == null) return;
            var j = cliente.PlayerObject.GetComponent<JogadorRede>();
            if (j == null || (j.Vida != null && j.Vida.Caido.Value) || (j.Passageiro != null && j.Passageiro.NoCarro)) return;
            if (j.Inventario != null && j.Inventario.CarregandoPneu) return;
            foreach (var k in Todos) if (k != this && k.LugarDe(c) >= 0) return;
            if ((j.transform.position - transform.position).sqrMagnitude > 4.5f * 4.5f) return;
            var o = _ocupantes.Value;
            if (lugar == 0) o.a = c + 1;
            else o.b = c + 1;
            _ocupantes.Value = o;
            // quem rema é o dono (move o caiaque na máquina dele, como o motorista)
            if (lugar == 0 && OwnerClientId != c) NetworkObject.ChangeOwnership(c);
        }

        public void PedirSair()
        {
            if (IsSpawned) SairRpc();
        }

        [Rpc(SendTo.Server)]
        void SairRpc(RpcParams parametros = default) => Desocupar(parametros.Receive.SenderClientId);

        /// <summary>No anfitrião: o cliente sai do caiaque (se estava nele).</summary>
        public void Desocupar(ulong cliente)
        {
            if (!IsServer) return;
            var o = _ocupantes.Value;
            bool remava = o.a == cliente + 1;
            if (remava) o.a = 0;
            if (o.b == cliente + 1) o.b = 0;
            if (o.Equals(_ocupantes.Value)) return;
            _ocupantes.Value = o;
            if (remava && OwnerClientId != NetworkManager.ServerClientId) NetworkObject.RemoveOwnership();
        }

        /// <summary>No anfitrião: o cliente sai de qualquer caiaque (caiu, saiu da partida).</summary>
        public static void TirarDeTodos(ulong cliente)
        {
            foreach (var k in Todos) if (k != null && k.IsServer) k.Desocupar(cliente);
        }

        // ------------------------------------------------------------------------------------------ cada quadro

        void Update()
        {
            if (!IsSpawned) return;
            float dt = Time.deltaTime;
            // cada máquina senta (e levanta) os jogadores dos lugares
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null) continue;
                var p = j.GetComponent<PassageiroDoCaiaque>();
                if (p == null) continue;
                int l = LugarDe(j.OwnerClientId);
                if (l >= 0) p.Sentar(this, l);
                else if (p.Caiaque == this) p.Levantar();
            }
            if (IsServer)
                for (int i = 0; i < Lugares; i++)
                {
                    ulong c = Ocupante(i);
                    if (c != 0 && !NetworkManager.ConnectedClients.ContainsKey(c - 1)) Desocupar(c - 1);
                }
            // sem ninguém remando, o remo volta para o casco
            if (IsOwner && Ocupante(0) == 0 && _remo.Value != 0) _remo.Value = 0;
            if (IsOwner) Navegar(dt);
            Medir(dt);
            Remada(dt);
        }

        // no dono: quem rema (o lugar 0, nesta máquina, com o remo na mão) move; sem remo, a água freia; boia no nível da
        // água e balança (mais com vento e chuva); cada remada puxa a proa um pouco para o outro lado
        void Navegar(float dt)
        {
            bool remando = NetworkManager != null && Ocupante(0) == NetworkManager.LocalClientId + 1 && MenuOnline.Controla && _remo.Value >= 1;
            Vector2 m = remando && _mover != null ? _mover.ReadValue<Vector2>() : Vector2.zero;
            float alvo = m.y > 0.05f ? m.y * velocidadeMaxima : m.y < -0.05f ? m.y * re : 0f;
            float acelera = Mathf.Abs(alvo) > Mathf.Abs(_velocidade) ? 1.1f : 0.55f;
            _velocidade = Mathf.MoveTowards(_velocidade, alvo, acelera * dt);
            float virar = m.x * giroMaximo * (0.45f + 0.55f * Mathf.Clamp01(Mathf.Abs(_velocidade) / 1.5f));
            _giro = Mathf.Lerp(_giro, virar, 1f - Mathf.Exp(-3f * dt));
            float vaiVem = CicloDaRemada >= 0f ? Mathf.Sin(CicloDaRemada * 2f * Mathf.PI) * 7f * Mathf.Clamp01(Mathf.Abs(_velocidade) / 1.5f) : 0f;
            float yaw = transform.eulerAngles.y + (_giro + vaiVem) * dt;
            if (GirandoNoTornado) yaw += 150f * dt;   // (girando no tornado, o casco roda também)
            var rumo = Quaternion.Euler(0f, yaw, 0f);
            Vector3 frente = rumo * Vector3.forward;
            Vector3 nova = transform.position + frente * (_velocidade * dt);
            // encalha: a proa (ou a popa, de ré) na água rasa
            Vector3 ponta = nova + frente * (Mathf.Sign(_velocidade == 0f ? 1f : _velocidade) * meioComprimento);
            if (Mathf.Abs(_velocidade) > 0.01f && Agua.Fundura(ponta) < 0.25f)
            {
                _velocidade = 0f;
                nova = transform.position;
            }
            nova = NoTornado(nova, dt);
            float t = Time.time;
            // o tempo (07/11/2026): o vento e a chuva balançam mais, sem virar (até umas 4 vezes)
            float mar = 1f + 2f * Mathf.Clamp01(Tempo.Vento) + 1.2f * Mathf.Clamp01(Tempo.Chuva);
            float rajada = 1f + 0.35f * (Mathf.PerlinNoise(t * 0.4f, 3.1f) - 0.5f) * (mar - 1f);
            if (Agua.Nivel(nova, out float nivel)) nova.y = nivel + Mathf.Sin(t * 1.3f) * 0.025f * mar
                                                       + (GirandoNoTornado ? Mathf.Abs(Mathf.Sin(t * 5f)) * 0.6f : 0f);   // (aos pulos na água)
            transform.SetPositionAndRotation(nova, rumo * Quaternion.Euler(Mathf.Sin(t * 0.9f) * 1.2f * mar * rajada, 0f, Mathf.Sin(t * 1.1f) * 1.8f * mar * rajada));
        }

        // ------------------------------------------------------------------------------------------ o tornado (07/11/2026)

        float _tornadoGiraAte = -1f, _tornadoPegaDepois;
        Vector3 _tornadoEmpurrao;
        /// <summary>No dono: até onde o tornado puxa o caiaque (m), a força no meio (m/s) e o miolo, onde ele pega e gira.</summary>
        const float PuxaOCaiaque = 55f, ForcaNoCaiaque = 4.5f, MioloDoCaiaque = 13f;

        bool GirandoNoTornado => _tornadoGiraAte > 0f && Time.time < _tornadoGiraAte;

        /// <summary>
        /// No dono (07/11/2026, a pedido: o tornado puxa o carro, a moto e o caiaque, e no miolo gira em volta dele por um tempo
        /// curto): o tornado arrasta o caiaque pela água; no miolo, ele roda em volta do tornado (aos pulos, e o casco girando)
        /// por 2 a 3 s e é jogado para fora, deslizando. Ele só anda onde há água funda (não sobe na terra) e não vira.
        /// </summary>
        Vector3 NoTornado(Vector3 nova, float dt)
        {
            Vector3 anda = Vector3.zero;
            if (Redemoinho.Onde(nova, out _, out _, out float d) && d < PuxaOCaiaque)
            {
                if (_tornadoGiraAte > 0f)
                {
                    if (Time.time < _tornadoGiraAte && Tempo.ForcaDoTornado > 0.3f)
                    {
                        anda = Redemoinho.Giro(nova, nova.y, 6f, 9f, 0f);
                        anda.y = 0f;
                    }
                    else
                    {
                        _tornadoGiraAte = -1f;
                        _tornadoPegaDepois = Time.time + 6f;
                        _tornadoEmpurrao = Redemoinho.Arremesso(nova, 7f, 3f, 0f);
                    }
                }
                else
                {
                    anda = Redemoinho.Puxao(nova, PuxaOCaiaque, ForcaNoCaiaque);
                    anda.y = 0f;
                    if (d < MioloDoCaiaque && Tempo.ForcaDoTornado > 0.5f && Time.time >= _tornadoPegaDepois)
                        _tornadoGiraAte = Time.time + UnityEngine.Random.Range(2f, 3.2f);
                }
            }
            else _tornadoGiraAte = -1f;
            anda += _tornadoEmpurrao;
            _tornadoEmpurrao = Vector3.MoveTowards(_tornadoEmpurrao, Vector3.zero, 3f * dt);
            if (anda.sqrMagnitude < 1e-4f) return nova;
            var destino = nova + anda * dt;
            if (Agua.Nivel(destino, out _) && Agua.Fundura(destino) >= 0.25f) return destino;
            _tornadoEmpurrao = Vector3.zero;
            return nova;
        }

        // a velocidade para a frente, em cada máquina (a posição chega pela rede)
        void Medir(float dt)
        {
            Vector3 p = transform.position;
            float v = dt > 0f ? Vector3.Dot(p - _ultima, transform.forward) / dt : 0f;
            _ultima = p;
            Velocidade = Mathf.Lerp(Velocidade, Mathf.Clamp(v, -velocidadeMaxima, velocidadeMaxima), 1f - Mathf.Exp(-6f * dt));
        }

        // a remada, em cada máquina: o tempo dela vem do Remar do corpo de quem rema; cada pá que entra na água faz barulho
        // e respinga; atrás, o rastro
        void Remada(float dt)
        {
            var remador = Remador();
            CicloDaRemada = -1f;
            if (remador != null && remador.animadorCorpo != null && remador.animadorCorpo.isActiveAndEnabled)
            {
                var estado = remador.animadorCorpo.GetCurrentAnimatorStateInfo(0);
                if (estado.shortNameHash == RemarId) CicloDaRemada = Mathf.Repeat(estado.normalizedTime, 1f);
            }
            if (CicloDaRemada >= 0f && _cicloAntes >= 0f)
            {
                // a pá esquerda entra logo no começo, a direita no meio da volta
                if (Passou(_cicloAntes, CicloDaRemada, 0.03f)) Pa(-1f);
                if (Passou(_cicloAntes, CicloDaRemada, 0.53f)) Pa(1f);
            }
            _cicloAntes = CicloDaRemada;
            float v = Mathf.Abs(Velocidade);
            if (v > 0.5f && Time.time >= _proximaOnda && Agua.Nivel(transform.position, out float nivel))
            {
                _proximaOnda = Time.time + Mathf.Lerp(0.6f, 0.3f, v / velocidadeMaxima);
                Vector3 popa = transform.TransformPoint(new Vector3(0f, 0f, -meioComprimento * Mathf.Sign(Velocidade)));
                popa.y = nivel + 0.02f;
                AguaNaSuperficie.Onda(popa, 0.5f + 0.4f * v / velocidadeMaxima);
                Vector3 proa = transform.TransformPoint(new Vector3(0f, 0f, meioComprimento * 0.9f * Mathf.Sign(Velocidade)));
                proa.y = nivel + 0.02f;
                AguaNaSuperficie.Onda(proa, 0.3f);
            }
        }

        static bool Passou(float antes, float agora, float marca) => antes <= agora ? antes < marca && agora >= marca : antes < marca || agora >= marca;

        // uma pá na água: do lado (-1 a esquerda, 1 a direita), na altura da água
        void Pa(float lado)
        {
            if (!Agua.Nivel(transform.position, out float nivel)) return;
            float z = assentos != null && assentos.Length > 0 && assentos[0] != null ? transform.InverseTransformPoint(assentos[0].position).z + 0.6f : 0f;
            Vector3 ponto = transform.TransformPoint(new Vector3(lado * 0.95f, 0f, z));
            ponto.y = nivel + 0.02f;
            Efeitos.Som(SonsDaRemada(), ponto, 0.45f, true, 25f);
            AguaNaSuperficie.Respingo(ponto, 7, 0.55f);
            AguaNaSuperficie.Onda(ponto, 0.45f);
        }

        static AudioClip[] _sonsDaRemada;
        static AudioClip[] SonsDaRemada()
        {
            if (_sonsDaRemada != null) return _sonsDaRemada;
            _sonsDaRemada = new AudioClip[4];
            for (int i = 0; i < 4; i++) _sonsDaRemada[i] = Resources.Load<AudioClip>($"Agua/Agua_Bracada_{i}");
            return _sonsDaRemada;
        }

        // quem rema (o jogador do lugar 0), ou null
        JogadorRede Remador()
        {
            ulong c = Ocupante(0);
            if (c == 0) return null;
            foreach (var j in JogadorRede.Todos) if (j != null && j.OwnerClientId == c - 1) return j;
            return null;
        }

        // o remo: na mão direita de quem rema (com ele na mão); senão, deitado no casco. Depois do corpo sentar e animar.
        void LateUpdate()
        {
            if (!IsSpawned || remo == null) return;
            var remador = RemoNaMao ? Remador() : null;
            var mao = remador != null ? MaoDe(remador) : null;
            if (mao == null)
            {
                remo.SetLocalPositionAndRotation(_remoLugar, _remoGiro);
                SoSombra(false);
                return;
            }
            remo.SetPositionAndRotation(mao.TransformPoint(PegadaNaMao), mao.rotation * GiroNaMao);
            // quem rema vê o remo do corpo (desde 07/11/2026, o corpo sem a cabeça na primeira pessoa: as mãos dele no remo);
            // sem o corpo à vista, o remo só faz sombra, como o corpo
            var terceira = remador.TerceiraPessoa;
            SoSombra(remador.IsOwner && (terceira == null || !terceira.Ativa) && !remador.CorpoSemCabeca);
        }

        Transform MaoDe(JogadorRede j)
        {
            var animador = j.animadorCorpo;
            if (j == _remadorVisto && animador == _animadorVisto && _maoDoRemador != null) return _maoDoRemador;
            _remadorVisto = j;
            _animadorVisto = animador;
            _maoDoRemador = null;
            if (animador != null)
                foreach (var t in animador.GetComponentsInChildren<Transform>(true))
                    if (t.name == "hand.R")
                    {
                        _maoDoRemador = t;
                        break;
                    }
            return _maoDoRemador;
        }

        void SoSombra(bool so)
        {
            if (so == _remoSoSombra || _desenhoDoRemo == null) return;
            _remoSoSombra = so;
            foreach (var r in _desenhoDoRemo)
                if (r != null) r.shadowCastingMode = so ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
