using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;

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
    /// um pouco e encalha onde a água fica rasa na proa.</para>
    /// </summary>
    public class Caiaque : NetworkBehaviour
    {
        public const int Lugares = 2;

        [Tooltip("Os bancos: 0 atrás (quem rema), 1 na frente (quem pesca). O quadril de quem senta.")]
        public Transform[] assentos = new Transform[Lugares];
        [Tooltip("O remo (gira na mão de quem rema; sem ninguém, fica deitado no casco).")]
        public Transform remo;
        public float velocidadeMaxima = 3.2f, re = 1.4f, giroMaximo = 55f;
        [Tooltip("Do comprimento do caiaque, a metade (m): a proa, que encalha na água rasa.")]
        public float meioComprimento = 2.3f;

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

        InputAction _mover;
        float _velocidade, _giro, _fase;
        Vector3 _ultima;
        Vector3 _remoLugar;
        Quaternion _remoGiro;

        void OnEnable() => Todos.Add(this);
        void OnDisable() => Todos.Remove(this);

        void Awake()
        {
            if (remo != null)
            {
                _remoLugar = remo.localPosition;
                _remoGiro = remo.localRotation;
            }
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
            if (IsOwner) Navegar(dt);
            Remo(dt);
        }

        // no dono: quem rema (o lugar 0, nesta máquina) move; sem remo, a água freia; boia no nível da água
        void Navegar(float dt)
        {
            bool remando = NetworkManager != null && Ocupante(0) == NetworkManager.LocalClientId + 1 && MenuOnline.Controla;
            Vector2 m = remando && _mover != null ? _mover.ReadValue<Vector2>() : Vector2.zero;
            float alvo = m.y > 0.05f ? m.y * velocidadeMaxima : m.y < -0.05f ? m.y * re : 0f;
            float acelera = Mathf.Abs(alvo) > Mathf.Abs(_velocidade) ? 1.1f : 0.55f;
            _velocidade = Mathf.MoveTowards(_velocidade, alvo, acelera * dt);
            float virar = m.x * giroMaximo * (0.45f + 0.55f * Mathf.Clamp01(Mathf.Abs(_velocidade) / 1.5f));
            _giro = Mathf.Lerp(_giro, virar, 1f - Mathf.Exp(-3f * dt));
            float yaw = transform.eulerAngles.y + _giro * dt;
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
            float t = Time.time;
            if (Agua.Nivel(nova, out float nivel)) nova.y = nivel + Mathf.Sin(t * 1.3f) * 0.025f;
            transform.SetPositionAndRotation(nova, rumo * Quaternion.Euler(Mathf.Sin(t * 0.9f) * 1.2f, 0f, Mathf.Sin(t * 1.1f) * 1.8f));
        }

        // o remo: com alguém remando, na mão dele, girando pela velocidade; sem ninguém, deitado no casco
        void Remo(float dt)
        {
            Vector3 p = transform.position;
            float v = dt > 0f ? new Vector2(p.x - _ultima.x, p.z - _ultima.z).magnitude / dt : 0f;
            _ultima = p;
            if (remo == null) return;
            if (Ocupante(0) == 0 || assentos == null || assentos[0] == null)
            {
                remo.SetLocalPositionAndRotation(_remoLugar, _remoGiro);
                return;
            }
            _fase += Mathf.Clamp(v, 0f, velocidadeMaxima) * dt * 2.4f;
            float bate = Mathf.Sin(_fase);
            var banco = assentos[0];
            Vector3 maos = banco.localPosition + new Vector3(0f, 0.42f, 0.42f);
            remo.localPosition = maos;
            remo.localRotation = Quaternion.Euler(0f, bate * 12f, bate * 32f);
        }
    }
}
