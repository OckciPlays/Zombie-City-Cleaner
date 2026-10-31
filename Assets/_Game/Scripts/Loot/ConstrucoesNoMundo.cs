using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// Uma construção montada no mundo: o que é (o kit: a fogueira, a cama, a barricada), onde está, para onde está
    /// virada, quem montou e o valor dela (a barricada: a vida, de 0 a 100; a fogueira: até quando o fogo dura, no
    /// relógio do servidor).
    /// </summary>
    public struct Construcao : INetworkSerializeByMemcpy, IEquatable<Construcao>
    {
        public ushort id;
        public Item tipo;
        public Vector3 pos;
        public float giro;
        public float valor;
        public ulong dono;

        public bool Equals(Construcao o) => id == o.id && tipo == o.tipo && pos == o.pos && giro == o.giro && valor == o.valor && dono == o.dono;
    }

    /// <summary>
    /// As construções montadas pelos jogadores (31/10/2026, a pedido: a fogueira, a cama e a barricada da fabricação;
    /// "fogueira, cama e barricada são montadas no mundo e funcionam"). Uma lista só na rede (o anfitrião manda) e cada
    /// cópia monta os objetos dela, como os itens no chão (<see cref="ItensNoMundo"/>):
    /// - a fogueira: acesa, a luz que tremula, o fogo e a vida que volta (1 por segundo, até 3 m); a lenha dura
    ///   <see cref="MinutosPorLenha"/> minutos e E põe mais (1 madeira). Apagada, E acende com 1 madeira;
    /// - a cama: quem montou volta nela quando cai (no lugar do ponto de nascimento); E recolhe (volta o kit);
    /// - a barricada: o colisor e o buraco no NavMesh (os zumbis não passam: dão a volta ou param nela) e a vida, que os
    ///   zumbis perto, perseguindo alguém, tiram (<see cref="DanoPorZumbi"/> por segundo, cada um); acabou, ela cai e
    ///   sobram umas tábuas. E desmonta (volta o kit, se estiver inteira; senão, a madeira).
    /// O jogador monta pelo menu do botão direito do kit, na tela do inventário (<see cref="MontadorDeConstrucao"/>).
    /// </summary>
    public class ConstrucoesNoMundo : NetworkBehaviour
    {
        [Tooltip("As malhas montadas, na ordem do enum (de Fogueira em diante): Construcao_FogueiraMontada, _CamaMontada e _BarricadaMontada.")]
        public Mesh[] malhas;
        public Material material;
        [Tooltip("O material do fogo (aditivo, o do clarão dos tiros).")]
        public Material materialDoFogo;

        public const float MinutosPorLenha = 4f;
        public const float DanoPorZumbi = 2.5f;
        public const float AlcanceDoCalor = 3f;
        /// <summary>Até onde o jogador monta (m, dos pés ao ponto).</summary>
        public const float AlcanceDeMontar = 5f;

        public static ConstrucoesNoMundo Instancia { get; private set; }

        NetworkList<Construcao> _lista;
        ushort _proximoId = 1;
        readonly Dictionary<ushort, GameObject> _objetos = new Dictionary<ushort, GameObject>();
        readonly HashSet<ushort> _vistos = new HashSet<ushort>();
        readonly List<ushort> _sobrando = new List<ushort>();
        float _proximoTique;

        void Awake() => _lista = new NetworkList<Construcao>();
        void OnEnable() => Instancia = this;

        void OnDisable()
        {
            if (Instancia == this) Instancia = null;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _lista.Clear();
            _lista.OnListChanged += AoMudar;
            Atualizar();
        }

        public override void OnNetworkDespawn()
        {
            _lista.OnListChanged -= AoMudar;
            foreach (var o in _objetos.Values) if (o != null) Destroy(o);
            _objetos.Clear();
        }

        void AoMudar(NetworkListEvent<Construcao> e) => Atualizar();

        float Agora => NetworkManager != null ? NetworkManager.ServerTime.TimeAsFloat : Time.time;

        /// <summary>A malha montada do kit (null: não é um, ou ainda não foi montada).</summary>
        public Mesh MalhaDe(Item tipo)
        {
            int k = tipo - Item.Fogueira;
            return Itens.EhConstrucao(tipo) && malhas != null && k >= 0 && k < malhas.Length ? malhas[k] : null;
        }

        /// <summary>
        /// A pose da construção num ponto do chão, virada (graus), e se ela cabe ali (sem bater em parede, móvel ou outra
        /// construção). O mesmo para a sombra do jogador que monta e para o anfitrião.
        /// </summary>
        public bool Cabe(Item tipo, Vector3 ponto, float giro, Collider ignorar = null)
        {
            var malha = MalhaDe(tipo);
            if (malha == null) return false;
            var b = malha.bounds;
            var rot = Quaternion.Euler(0f, giro, 0f);
            var centro = ponto + rot * new Vector3(b.center.x, b.center.y + 0.06f, b.center.z);
            var meia = new Vector3(b.extents.x - 0.02f, Mathf.Max(0.01f, b.extents.y - 0.05f), b.extents.z - 0.02f);
            var batidas = Physics.OverlapBox(centro, meia, rot, MascaraDeMontar, QueryTriggerInteraction.Ignore);
            foreach (var c in batidas)
                if (c != ignorar) return false;
            return true;
        }

        // o que impede montar: o cenário e as outras construções (não o jogador, os braços, os zumbis e os corpos)
        static readonly int MascaraDeMontar = ~((1 << 2) | (1 << JogadorRede.CamadaBracos) | (1 << 7) | (1 << 8) | (1 << JogadorRede.CamadaJogador));

        /// <summary>O ponto é do chão em que o jogador pisa (não o tampo de uma mesa, um balcão ou outro andar).</summary>
        public static bool NoChao(Vector3 pes, Vector3 ponto) => Mathf.Abs(ponto.y - pes.y) < 0.6f;

        /// <summary>A cama do jogador (a última que ele montou): onde ele volta quando cai.</summary>
        public static bool CamaDe(ulong dono, out Pose pose)
        {
            pose = default;
            var m = Instancia;
            if (m == null || !m.IsSpawned) return false;
            for (int i = m._lista.Count - 1; i >= 0; i--)
            {
                var c = m._lista[i];
                if (c.tipo != Item.Cama || c.dono != dono) continue;
                var rot = Quaternion.Euler(0f, c.giro, 0f);
                // ao lado da cama, olhando para a mesma direção
                pose = new Pose(c.pos + rot * new Vector3(0.75f, 0.05f, 0f), rot);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ no dono

        /// <summary>O jogador local monta o kit da mochila (o Id dele) no ponto, virado (o anfitrião confere).</summary>
        public void PedirMontar(ushort idDoKit, Vector3 ponto, float giro) => MontarRpc(idDoKit, ponto, giro);

        public void PedirUsar(ushort id) => UsarRpc(id);

        // ------------------------------------------------------------------ no anfitrião

        static InventarioJogador InventarioDe(NetworkManager rede, ulong cliente) =>
            rede.ConnectedClients.TryGetValue(cliente, out var c) && c.PlayerObject != null ? c.PlayerObject.GetComponent<InventarioJogador>() : null;

        [Rpc(SendTo.Server)]
        void MontarRpc(ushort idDoKit, Vector3 ponto, float giro, RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            var inventario = InventarioDe(NetworkManager, quem);
            if (inventario == null || !inventario.Achar(idDoKit, out var kit) || !Itens.EhConstrucao(kit.item)) return;
            if ((inventario.transform.position - ponto).sqrMagnitude > (AlcanceDeMontar + 1f) * (AlcanceDeMontar + 1f)) return;
            if (!NoChao(inventario.transform.position, ponto)) return;
            if (!Cabe(kit.item, ponto, giro)) return;
            inventario.TirarUm(idDoKit);
            float valor = kit.item == Item.Barricada ? 100f : kit.item == Item.Fogueira ? Agora + MinutosPorLenha * 60f : 0f;
            _lista.Add(new Construcao { id = _proximoId++, tipo = kit.item, pos = ponto, giro = giro, valor = valor, dono = quem });
        }

        [Rpc(SendTo.Server)]
        void UsarRpc(ushort id, RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            var inventario = InventarioDe(NetworkManager, quem);
            int k = Indice(id);
            if (inventario == null || k < 0) return;
            var c = _lista[k];
            if ((inventario.transform.position - c.pos).sqrMagnitude > 4f * 4f) return;
            switch (c.tipo)
            {
                case Item.Fogueira:
                    // mais lenha (ou acender): 1 madeira, mais uns minutos de fogo
                    if (!inventario.GastarItem(Item.Madeira, 1)) return;
                    c.valor = Mathf.Max(c.valor, Agora) + MinutosPorLenha * 60f;
                    _lista[k] = c;
                    break;
                case Item.Cama:
                    _lista.RemoveAt(k);
                    inventario.EntregarItem(Item.Cama, 1);
                    break;
                case Item.Barricada:
                    _lista.RemoveAt(k);
                    if (c.valor >= 60f) inventario.EntregarItem(Item.Barricada, 1);
                    else inventario.EntregarItem(Item.Madeira, 2);
                    break;
            }
        }

        int Indice(ushort id)
        {
            for (int i = 0; i < _lista.Count; i++)
                if (_lista[i].id == id) return i;
            return -1;
        }

        public Construcao? Dados(ushort id)
        {
            int k = Indice(id);
            return k < 0 ? (Construcao?)null : _lista[k];
        }

        /// <summary>A fogueira está acesa (o fogo ainda não acabou, no relógio do servidor).</summary>
        public bool Acesa(Construcao c) => c.tipo == Item.Fogueira && c.valor > Agora;

        void Update()
        {
            // cada cópia: o fogo das fogueiras acende e apaga pelo relógio
            foreach (var c in _lista)
                if (c.tipo == Item.Fogueira && _objetos.TryGetValue(c.id, out var o) && o != null)
                {
                    var fogo = o.transform.Find("Fogo");
                    bool acesa = Acesa(c);
                    if (fogo != null && fogo.gameObject.activeSelf != acesa) fogo.gameObject.SetActive(acesa);
                    if (acesa && fogo != null && fogo.TryGetComponent<Light>(out var luz))
                        luz.intensity = 2.2f + 0.6f * Mathf.PerlinNoise(Time.time * 3.1f, c.id) + 0.3f * Mathf.Sin(Time.time * 17f + c.id);
                }
            if (!IsServer || !IsSpawned || Time.time < _proximoTique) return;
            // o anfitrião, a cada meio segundo: o calor das fogueiras e os zumbis nas barricadas
            const float passo = 0.5f;
            _proximoTique = Time.time + passo;
            for (int k = _lista.Count - 1; k >= 0; k--)
            {
                var c = _lista[k];
                if (c.tipo == Item.Fogueira && Acesa(c))
                {
                    foreach (var cliente in NetworkManager.ConnectedClientsList)
                    {
                        var jogador = cliente.PlayerObject;
                        if (jogador == null || (jogador.transform.position - c.pos).sqrMagnitude > AlcanceDoCalor * AlcanceDoCalor) continue;
                        jogador.GetComponent<VidaJogador>()?.Curar(1f * passo);
                    }
                }
                else if (c.tipo == Item.Barricada)
                {
                    int zumbis = ZumbisNa(c);
                    if (zumbis == 0) continue;
                    c.valor -= DanoPorZumbi * zumbis * passo;
                    if (c.valor <= 0f)
                    {
                        _lista.RemoveAt(k);
                        // sobram umas tábuas no chão
                        ItensNoMundo.Instancia?.Criar(Item.Madeira, c.pos, c.giro, 2);
                    }
                    else _lista[k] = c;
                }
            }
        }

        // os zumbis vivos, perseguindo alguém, encostados na barricada (até 0,9 m da caixa dela)
        int ZumbisNa(Construcao c)
        {
            var zm = ZombieManager.Instancia;
            var malha = MalhaDe(c.tipo);
            if (zm == null || malha == null) return 0;
            var rot = Quaternion.Euler(0f, c.giro, 0f);
            var inv = Quaternion.Inverse(rot);
            var b = malha.bounds;
            int n = 0;
            foreach (var z in zm.Ativos)
            {
                if (z == null || z.Morto || !z.Engajado) continue;
                var local = inv * (z.transform.position - c.pos);
                var d = new Vector3(Mathf.Max(0f, Mathf.Abs(local.x - b.center.x) - b.extents.x), 0f, Mathf.Max(0f, Mathf.Abs(local.z - b.center.z) - b.extents.z));
                if (d.magnitude < 0.9f && Mathf.Abs(local.y) < 1.5f) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ os objetos

        void Atualizar()
        {
            _vistos.Clear();
            foreach (var c in _lista)
            {
                _vistos.Add(c.id);
                if (!_objetos.ContainsKey(c.id)) _objetos[c.id] = Montar(c);
            }
            _sobrando.Clear();
            foreach (var id in _objetos.Keys)
                if (!_vistos.Contains(id)) _sobrando.Add(id);
            foreach (var id in _sobrando)
            {
                if (_objetos[id] != null) Destroy(_objetos[id]);
                _objetos.Remove(id);
            }
        }

        GameObject Montar(Construcao c)
        {
            var malha = MalhaDe(c.tipo);
            var go = new GameObject($"Construção {c.id} ({Itens.Nome(c.tipo)})");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(c.pos, Quaternion.Euler(0f, c.giro, 0f));
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            var b = malha != null ? malha.bounds : new Bounds(Vector3.up * 0.2f, Vector3.one * 0.4f);
            var col = go.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;
            // a fogueira e a cama não seguram ninguém (passa-se por cima); a barricada, sim
            col.isTrigger = c.tipo != Item.Barricada;
            var mira = go.AddComponent<ConstrucaoNaMira>();
            mira.mundo = this;
            mira.id = c.id;
            if (c.tipo == Item.Barricada)
            {
                var obstaculo = go.AddComponent<NavMeshObstacle>();
                obstaculo.shape = NavMeshObstacleShape.Box;
                obstaculo.center = b.center;
                obstaculo.size = b.size + new Vector3(0.1f, 0f, 0.2f);
                obstaculo.carving = true;
                obstaculo.carveOnlyStationary = false;
            }
            if (c.tipo == Item.Fogueira) Fogo(go.transform, new Vector3(0f, 0.12f, 0f), materialDoFogo, 1f, 7f, true, go.layer);
            return go;
        }

        /// <summary>
        /// O fogo (a fogueira e a tocha): as chamas (partículas aditivas, as do clarão dos tiros) e, com
        /// <paramref name="alcance"/> &gt; 0, a luz laranja (a fogueira faz ela tremular). <paramref name="tamanho"/> escala as
        /// chamas; no mundo (<paramref name="noMundo"/>) elas ficam para trás quando o fogo anda.
        /// </summary>
        public static GameObject Fogo(Transform pai, Vector3 onde, Material material, float tamanho, float alcance, bool noMundo, int camada)
        {
            var fogo = new GameObject("Fogo") { layer = camada };
            fogo.transform.SetParent(pai, false);
            fogo.transform.localPosition = onde;
            fogo.transform.rotation = Quaternion.identity;
            if (alcance > 0f)
            {
                var luz = fogo.AddComponent<Light>();
                luz.type = LightType.Point;
                luz.color = new Color(1f, 0.58f, 0.25f);
                luz.range = alcance;
                luz.intensity = 2.4f;
                luz.shadows = LightShadows.None;
            }
            var ps = fogo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f * Mathf.Sqrt(tamanho), 0.9f * Mathf.Sqrt(tamanho));
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * tamanho, 1.1f * tamanho);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f * tamanho, 0.55f * tamanho);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.45f, 0.95f), new Color(1f, 0.62f, 0.25f, 0.85f));
            main.simulationSpace = noMundo ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.maxParticles = 80;
            var emissao = ps.emission;
            emissao.rateOverTime = 48f;
            var forma = ps.shape;
            forma.shapeType = ParticleSystemShapeType.Cone;
            forma.angle = 12f;
            forma.radius = 0.16f * tamanho;
            forma.rotation = new Vector3(-90f, 0f, 0f);
            var aoLongo = ps.sizeOverLifetime;
            aoLongo.enabled = true;
            aoLongo.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
            var cor = ps.colorOverLifetime;
            cor.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.08f), 0.5f), new GradientColorKey(new Color(0.5f, 0.1f, 0.05f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
            cor.color = g;
            var render = fogo.GetComponent<ParticleSystemRenderer>();
            render.sharedMaterial = material;
            render.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return fogo;
        }
    }

    /// <summary>O objeto de uma construção: a mira acha o colisor, e E usa (a lenha na fogueira, recolher a cama, desmontar a barricada).</summary>
    public class ConstrucaoNaMira : Interagivel
    {
        public ConstrucoesNoMundo mundo;
        public ushort id;

        public override string Dica(ArmaJogador quem)
        {
            var d = mundo.Dados(id);
            if (d == null || quem.Inventario == null) return null;
            var c = d.Value;
            int madeira = quem.Inventario.Quantos(Item.Madeira);
            switch (c.tipo)
            {
                case Item.Fogueira:
                    bool acesa = mundo.Acesa(c);
                    if (madeira == 0) return acesa ? "Fogueira acesa (mais lenha: 1 madeira)" : "Fogueira apagada: precisa de 1 madeira";
                    return acesa ? $"[E] Pôr lenha (1 madeira: +{ConstrucoesNoMundo.MinutosPorLenha:0} min)" : "[E] Acender a fogueira (1 madeira)";
                case Item.Cama:
                    bool minha = mundo.NetworkManager != null && c.dono == mundo.NetworkManager.LocalClientId;
                    return minha ? "Sua cama: você volta aqui quando cair   ·   [E] Recolher" : "[E] Recolher a cama";
                case Item.Barricada:
                    return $"Barricada ({Mathf.CeilToInt(c.valor)}%)   ·   [E] Desmontar";
            }
            return null;
        }

        public override void Usar(ArmaJogador quem) => mundo.PedirUsar(id);
    }
}
