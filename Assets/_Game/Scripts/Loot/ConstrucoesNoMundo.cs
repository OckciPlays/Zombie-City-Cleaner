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
    /// virada, quem montou e o valor dela (a barricada: a vida, de 0 até a vida máxima, 100 sem o Armadilheiro e a
    /// Fortaleza; a fogueira: até quando o fogo dura, no relógio do servidor).
    /// </summary>
    public struct Construcao : INetworkSerializeByMemcpy, IEquatable<Construcao>
    {
        public ushort id;
        public Item tipo;
        public Vector3 pos;
        public float giro;
        public float valor;
        public ulong dono;
        /// <summary>
        /// As armadilhas (11/11/2026, a etapa 5a): o Armadilheiro do dono quando montou (0 a 2: os usos, a vida e o dano dela;
        /// Player/ArmadilhasDoJogador.cs). Desde a 5b, também nas barricadas (a solta e a de tábuas: a vida). Nas outras, 0.
        /// </summary>
        public byte nivel;
        /// <summary>
        /// A barricada de tábuas (11/11/2026, a etapa 5b; o tipo é o <see cref="Item.TabuasBarricada"/>): a chave da porta ou
        /// da janela da casa onde ela está pregada (<c>PortasDoMundo.ChaveDaPorta</c> ou <c>ChaveDaJanela</c>, a mesma em
        /// todas as máquinas e ao recarregar: as casas vêm da semente). Nas outras, 0.
        /// </summary>
        public ulong vao;
        /// <summary>O vão da barricada de tábuas (m: a largura e a altura; a malha se ajusta a ele). Nas outras, 0.</summary>
        public float largura, altura;
        /// <summary>
        /// Os bits da 5b: <see cref="ConstrucoesNoMundo.BitJanela"/> (a de tábuas numa janela),
        /// <see cref="ConstrucoesNoMundo.BitReforcada"/> (o nível 2 da Barricada de tábuas de quem pregou) e
        /// <see cref="ConstrucoesNoMundo.BitFortaleza"/> (o dono tinha a Fortaleza: a vida em dobro e o conserto sozinho).
        /// </summary>
        public byte extra;

        public bool Equals(Construcao o) => id == o.id && tipo == o.tipo && pos == o.pos && giro == o.giro && valor == o.valor && dono == o.dono && nivel == o.nivel
                                            && vao == o.vao && largura == o.largura && altura == o.altura && extra == o.extra;
    }

    /// <summary>
    /// As construções montadas pelos jogadores (31/10/2026, a pedido: a fogueira, a cama e a barricada da fabricação;
    /// "fogueira, cama e barricada são montadas no mundo e funcionam"). Uma lista só na rede (o anfitrião manda) e cada
    /// cópia monta os objetos dela, como os itens no chão (<see cref="ItensNoMundo"/>):
    /// - a fogueira: acesa, a luz que tremula e o fogo (não cura desde 05/11/2026: só o tratamento cura); a lenha dura
    ///   <see cref="MinutosPorLenha"/> minutos e E põe mais (1 madeira). Apagada, E acende com 1 madeira. A chuva a apaga
    ///   fora de um teto, e com a tocha apagada na mão, E reacende a tocha nela (05/11/2026, ConstrucoesNoMundo.Chuva.cs);
    ///   os zumbis ficam longe dela (ZombieManager.Luz.cs);
    /// - a cama: quem montou volta nela quando cai (no lugar do ponto de nascimento); E dorme e o clique recolhe (volta o
    ///   kit), só para quem montou (10/11/2026: o anfitrião confere o dono; para os outros, a dica diz de quem é);
    /// - a barricada: o colisor e o buraco no NavMesh (os zumbis não passam: dão a volta ou param nela) e a vida, que os
    ///   zumbis perto, perseguindo alguém, tiram (<see cref="DanoPorZumbi"/> por segundo, cada um); acabou, ela cai e
    ///   sobram umas tábuas. E desmonta (volta o kit, se estiver inteira; senão, a madeira).
    /// O jogador monta pelo menu do botão direito do kit, na tela do inventário (<see cref="MontadorDeConstrucao"/>).
    /// </summary>
    public partial class ConstrucoesNoMundo : NetworkBehaviour
    {
        [Tooltip("As malhas montadas, na ordem do enum (de Fogueira em diante): Construcao_FogueiraMontada, _CamaMontada e _BarricadaMontada.")]
        public Mesh[] malhas;
        public Material material;
        [Tooltip("O material do fogo (aditivo, o do clarão dos tiros).")]
        public Material materialDoFogo;

        public const float MinutosPorLenha = 4f;
        public const float DanoPorZumbi = 2.5f;
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
            EsquecerAChuva();
            EsquecerOFogo();
            EsquecerAsArmadilhas();
            _barricadasDosVaos.Clear();   // (as portas e as janelas barricadas, 11/11/2026, a etapa 5b)
            _batidaEm.Clear();
        }

        void AoMudar(NetworkListEvent<Construcao> e) => Atualizar();

        float Agora => NetworkManager != null ? NetworkManager.ServerTime.TimeAsFloat : Time.time;

        /// <summary>
        /// As construções agora (o save: Salvar/Salvamento). Na fogueira, o valor vira os segundos de fogo que faltam (ele é
        /// o fim do fogo no relógio da partida, que recomeça em cada partida).
        /// </summary>
        public List<Construcao> Copia()
        {
            var lista = new List<Construcao>(_lista.Count);
            foreach (var c in _lista)
            {
                var d = c;
                if (Itens.EhFogueira(d.tipo)) d.valor = Mathf.Max(0f, d.valor - Agora);
                // a cerca de fogo acesa (11/11/2026, a etapa 5a) não vai: o combustível queimou (ela volta gasta)
                if (d.tipo == Item.CercaFogo && d.valor > 0f) continue;
                lista.Add(d);
            }
            return lista;
        }

        /// <summary>Só no anfitrião: as construções do mundo salvo (a fogueira, com os segundos que faltavam).</summary>
        public void Restaurar(List<Construcao> lista)
        {
            if (!IsServer || !IsSpawned) return;
            _lista.Clear();
            _proximoId = 1;
            EsquecerAChuva();
            foreach (var c in lista)
            {
                if (!Itens.EhConstrucao(c.tipo) && c.tipo != Item.TabuasBarricada) continue;
                if (c.tipo == Item.CercaFogo && c.valor > 0f) continue;   // (a acesa não volta: veja a Copia)
                // a barricada de tábuas (11/11/2026, a etapa 5b) sem a porta ou a janela (um save estragado) não volta
                if (c.tipo == Item.TabuasBarricada && (c.vao == 0 || BarricadaDoVao(c.vao).HasValue)) continue;
                var d = c;
                d.id = _proximoId++;
                if (Itens.EhFogueira(d.tipo)) d.valor = d.valor > 0f ? Agora + d.valor : 0f;
                _lista.Add(d);
                if (d.tipo == Item.TabuasBarricada) _barricadasDosVaos[d.vao] = d.id;
                // (a porta ou a janela dela fica fechada: a lista das portas do save já a tem fechada, e o anfitrião confere)
                if (d.tipo == Item.TabuasBarricada) World.PortasDoMundo.Instancia?.FecharPelaBarricada(d.vao);
            }
        }

        /// <summary>A malha montada do kit (null: não é um, ou ainda não foi montada).</summary>
        public Mesh MalhaDe(Item tipo)
        {
            if (tipo == Item.KitFogueira) return ItensDoFogo.Fogueira;   // a portátil (11/11/2026): do Resources/ItensDoFogo
            if (Itens.EhArmadilha(tipo)) return ItensDasArmadilhas.Montada(tipo);   // as armadilhas (11/11/2026): do Resources/ItensDasArmadilhas
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
            // as armadilhas (11/11/2026) ficam no chão da rua, que sobe e desce: a caixa começa mais alto (uns 10° de ladeira
            // na ponta mais longe do meio), para o chão inclinado não contar como uma batida (as estacas compridas não
            // cabiam numa ladeira de 13%)
            float sobe = Itens.EhArmadilha(tipo) ? Mathf.Min(0.2f, 0.18f * Mathf.Max(b.extents.x, b.extents.z)) : 0f;
            var centro = ponto + rot * new Vector3(b.center.x, b.center.y + 0.06f + sobe * 0.5f, b.center.z);
            var meia = new Vector3(b.extents.x - 0.02f, Mathf.Max(0.01f, b.extents.y - 0.05f - sobe * 0.5f), b.extents.z - 0.02f);
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
        public void PedirMontar(ushort idDoKit, Vector3 ponto, float giro) => MontarRpc(idDoKit, ponto, giro, World.Tempo.SobTeto(ponto));

        public void PedirUsar(ushort id) => UsarRpc(id);

        // ------------------------------------------------------------------ no anfitrião

        static InventarioJogador InventarioDe(NetworkManager rede, ulong cliente) =>
            rede.ConnectedClients.TryGetValue(cliente, out var c) && c.PlayerObject != null ? c.PlayerObject.GetComponent<InventarioJogador>() : null;

        /// <summary>
        /// A chave do dono de uma construção, para o jogador que monta ou usa: a do personagem (a que vale em qualquer
        /// partida: 02/11/2026, o save) ou, sem ela, a conexão.
        /// </summary>
        static ulong ChaveDe(InventarioJogador inventario, ulong cliente)
        {
            var rede = inventario != null ? inventario.GetComponent<JogadorRede>() : null;
            return rede != null && rede.Chave.Value != 0 ? rede.Chave.Value : cliente;
        }

        /// <summary>
        /// Quem pode recolher (ou desmontar) a construção: a cama, só quem montou (10/11/2026, a pedido: o E de outro jogador
        /// recolhia a cama dele); a fogueira e a barricada, qualquer um.
        /// </summary>
        public static bool PodeRecolher(Construcao c, ulong chaveDeQuem) => c.tipo != Item.Cama || c.dono == chaveDeQuem;

        [Rpc(SendTo.Server)]
        void MontarRpc(ushort idDoKit, Vector3 ponto, float giro, bool cobertoParaQuemMonta, RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            var inventario = InventarioDe(NetworkManager, quem);
            if (inventario == null || !inventario.Achar(idDoKit, out var kit) || !Itens.EhConstrucao(kit.item)) return;
            if ((inventario.transform.position - ponto).sqrMagnitude > (AlcanceDeMontar + 1f) * (AlcanceDeMontar + 1f)) return;
            if (!NoChao(inventario.transform.position, ponto)) return;
            if (!Cabe(kit.item, ponto, giro)) return;
            // a portátil (11/11/2026, a etapa 4) não é montada na chuva fora de um teto: o kit fica na mochila
            if (kit.item == Item.KitFogueira && ChoveNoFogo && !cobertoParaQuemMonta && !World.Tempo.SobTeto(ponto))
            {
                inventario.AvisarDono("A chuva não deixa o fogo pegar: monte a fogueira portátil debaixo de um teto");
                return;
            }
            inventario.TirarUm(idDoKit);
            // as armadilhas (11/11/2026, a etapa 5a): o Armadilheiro de quem monta fica nela (os usos, a vida e o dano); desde a
            // 5b, também na barricada (a vida), e a Fortaleza de quem monta (um bit: a vida em dobro e o conserto sozinho) na
            // barricada e nas estacas
            var montador = inventario.GetComponent<JogadorRede>();
            int nivel = Itens.EhArmadilha(kit.item) || kit.item == Item.Barricada ? ArmadilhasDoJogador.NivelDe(montador) : 0;
            bool fortaleza = (kit.item == Item.Barricada || kit.item == Item.Estacas) && ArmadilhasDoJogador.FortalezaDe(montador);
            float valor = kit.item == Item.Barricada ? ArmadilhasDoJogador.VidaDaBarricadaSolta(nivel, fortaleza) : kit.item == Item.Fogueira ? Agora + MinutosPorLenha * 60f
                : kit.item == Item.KitFogueira ? Agora + SegundosDoKit(kit.desgaste)
                : Itens.EhArmadilha(kit.item) ? ValorDaArmadilha(kit.item, kit.desgaste, nivel) : 0f;
            if (fortaleza && kit.item == Item.Estacas) valor *= ArmadilhasDoJogador.FatorDaFortaleza;
            // na chuva, fora de um teto (o de quem monta ou o do anfitrião), a fogueira fica montada, apagada, e a lenha do kit
            // volta como 1 madeira (a que acende depois, debaixo de um teto ou sem chuva; 05/11/2026)
            bool apagadaNaChuva = kit.item == Item.Fogueira && ChoveNoFogo && !cobertoParaQuemMonta && !World.Tempo.SobTeto(ponto);
            if (apagadaNaChuva) valor = 0f;
            // o dono é o personagem (a chave dele, que vale em qualquer partida: 02/11/2026, o save), não a conexão
            ulong dono = ChaveDe(inventario, quem);
            ushort id = _proximoId++;
            _lista.Add(new Construcao { id = id, tipo = kit.item, pos = ponto, giro = giro, valor = valor, dono = dono, nivel = (byte)nivel, extra = fortaleza ? BitFortaleza : (byte)0 });
            if (Itens.EhFogueira(kit.item) && cobertoParaQuemMonta) _tetosVistos.Add(id);
            if (apagadaNaChuva)
            {
                inventario.EntregarItem(Item.Madeira, 1);
                inventario.AvisarDono("A chuva não deixa o fogo pegar: a fogueira ficou apagada (1 madeira de volta)");
            }
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
                    // a tocha apagada pela chuva reacende no fogo (05/11/2026): E na fogueira acesa, com ela na mão
                    if (Acesa(c) && inventario.TochaDaMaoApagada)
                    {
                        inventario.AcenderTochaNaMao();
                        return;
                    }
                    // na chuva, fora de um teto, o fogo não pega (nem a lenha nova): a madeira não se perde
                    if (ChoveNoFogo && Coberta(c) != true)
                    {
                        inventario.AvisarDono("A chuva não deixa o fogo pegar: só debaixo de um teto");
                        return;
                    }
                    // mais lenha (ou acender): 1 madeira, mais uns minutos de fogo
                    if (!inventario.GastarItem(Item.Madeira, 1)) return;
                    c.valor = Mathf.Max(c.valor, Agora) + MinutosPorLenha * 60f;
                    _lista[k] = c;
                    break;
                case Item.KitFogueira:
                    RecolherAPortatil(k, c, inventario);   // (11/11/2026, a etapa 4: ConstrucoesNoMundo.Molotov.cs)
                    break;
                case Item.Cama:
                    if (!PodeRecolher(c, ChaveDe(inventario, quem))) return;   // (a cama de outro jogador fica)
                    _lista.RemoveAt(k);
                    inventario.EntregarItem(Item.Cama, 1);
                    break;
                case Item.Barricada:
                    _lista.RemoveAt(k);
                    // (a vida em % da máxima desde 11/11/2026, a etapa 5b: o Armadilheiro e a Fortaleza aumentam a máxima)
                    if (c.valor / MaximoDe(c) * 100f >= ArmadilhasDoJogador.TabuasVoltamCom) inventario.EntregarItem(Item.Barricada, 1);
                    else inventario.EntregarItem(Item.Madeira, 2);
                    break;
                case Item.AlarmeLatas:
                case Item.TapetePregos:
                case Item.Estacas:
                case Item.CercaFogo:
                    UsarArmadilha(k, c, inventario);   // (11/11/2026, a etapa 5a: ConstrucoesNoMundo.Armadilhas.cs)
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
        public bool Acesa(Construcao c) => Itens.EhFogueira(c.tipo) && c.valor > Agora;

        // ------------------------------------------------------------------ os zumbis especiais (01/11/2026)

        /// <summary>As fogueiras acesas até a distância (o Long cospe nelas: odeia a luz).</summary>
        public void FogueirasAcesas(Vector3 centro, float alcance, List<Vector3> saida)
        {
            foreach (var c in _lista)
                if (Acesa(c) && (c.pos - centro).sqrMagnitude <= alcance * alcance) saida.Add(c.pos);
        }

        /// <summary>Só no anfitrião: a gosma apaga as fogueiras até o raio (a lenha que sobrava se perde; E acende de novo).</summary>
        public int ApagarFogueiras(Vector3 ponto, float raio)
        {
            if (!IsServer) return 0;
            int n = 0;
            for (int k = 0; k < _lista.Count; k++)
            {
                var c = _lista[k];
                if (!Acesa(c) || (c.pos - ponto).sqrMagnitude > raio * raio) continue;
                c.valor = Agora;
                _lista[k] = c;
                n++;
            }
            return n;
        }

        /// <summary>
        /// Só no anfitrião: o Brutamonte passa por cima das barricadas (elas caem e sobram tábuas) e, desde 11/11/2026 (a
        /// etapa 5a), das estacas: elas quebram (sobram 2 madeiras), mas ele (<paramref name="quem"/>) leva o dano delas.
        /// </summary>
        public void QuebrarBarricadasPerto(Vector3 ponto, float raio, Zumbi quem = null)
        {
            if (!IsServer) return;
            for (int k = _lista.Count - 1; k >= 0; k--)
            {
                var c = _lista[k];
                // (e as barricadas de tábuas das portas e das janelas, 11/11/2026, a etapa 5b: a porta volta a abrir, fechada)
                if (c.tipo != Item.Barricada && c.tipo != Item.Estacas && c.tipo != Item.TabuasBarricada) continue;
                Vector3 d = c.pos - ponto;
                d.y = 0f;
                if (d.sqrMagnitude > raio * raio) continue;
                if (c.tipo == Item.TabuasBarricada)
                {
                    BarricadaDeTabuasQuebrou(k, c);
                    continue;
                }
                _lista.RemoveAt(k);
                ItensNoMundo.Instancia?.Criar(Item.Madeira, c.pos, c.giro, 2);
                if (c.tipo == Item.Estacas) EstacasQuebradas(c, quem);
            }
        }

        void Update()
        {
            // cada cópia: o fogo das fogueiras acende e apaga pelo relógio
            GarrafasAgora();   // o molotov (11/11/2026, a etapa 4: ConstrucoesNoMundo.Molotov.cs): as garrafas voando e as áreas de fogo
            if (IsServer && IsSpawned) FogoNoServidor();
            ArmadilhasAgora();   // as armadilhas (11/11/2026, a etapa 5a: ConstrucoesNoMundo.Armadilhas.cs): o anfitrião decide, e cada cópia vê
            BarricadasAgora();   // as de tábuas (11/11/2026, a etapa 5b: ConstrucoesNoMundo.Barricadas.cs): a malha danificada e o som de quem mexe
            foreach (var c in _lista)
                if (Itens.EhFogueira(c.tipo) && _objetos.TryGetValue(c.id, out var o) && o != null)
                {
                    var fogo = o.transform.Find("Fogo");
                    bool acesa = Acesa(c);
                    if (fogo != null && fogo.gameObject.activeSelf != acesa) fogo.gameObject.SetActive(acesa);
                    if (acesa && fogo != null && fogo.TryGetComponent<Light>(out var luz))
                        luz.intensity = 3.6f + 0.9f * Mathf.PerlinNoise(Time.time * 3.1f, c.id) + 0.4f * Mathf.Sin(Time.time * 17f + c.id);
                }
            InformarTetos();
            if (!IsServer || !IsSpawned || Time.time < _proximoTique) return;
            // o anfitrião, a cada meio segundo: a chuva no fogo e os zumbis nas barricadas
            const float passo = 0.5f;
            _proximoTique = Time.time + passo;
            MolharNaChuva(passo);
            // (a fogueira não cura mais desde 05/11/2026, a pedido: "a vida não se regenera mais sozinha"; só o tratamento
            // cura. O fogo continua com a luz, os zumbis na beira e a chuva)
            // os zumbis nas barricadas (a solta e, desde 11/11/2026, a etapa 5b, as de tábuas) e o conserto da Fortaleza
            BarricadasNoServidor(passo);   // (ConstrucoesNoMundo.Barricadas.cs)
        }

        // os zumbis vivos, perseguindo alguém, encostados na barricada (até 0,9 m da caixa dela)
        int ZumbisNa(Construcao c)
        {
            var zm = ZombieManager.Instancia;
            if (zm == null || !TemCaixa(c)) return 0;
            var rot = Quaternion.Euler(0f, c.giro, 0f);
            var inv = Quaternion.Inverse(rot);
            var b = CaixaDe(c);   // (a de tábuas, 11/11/2026: a malha da porta ou da janela, no tamanho do vão)
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
            _barricadasDosVaos.Clear();   // (as portas e as janelas barricadas, 11/11/2026, a etapa 5b)
            foreach (var c in _lista)
            {
                _vistos.Add(c.id);
                if (!_objetos.ContainsKey(c.id)) _objetos[c.id] = Montar(c);
                if (c.tipo == Item.TabuasBarricada && c.vao != 0) _barricadasDosVaos[c.vao] = c.id;
            }
            _sobrando.Clear();
            foreach (var id in _objetos.Keys)
                if (!_vistos.Contains(id)) _sobrando.Add(id);
            foreach (var id in _sobrando)
            {
                if (_objetos[id] != null)
                {
                    AntesDeSumir(_objetos[id]);   // (a linha de fogo da cerca apaga devagar, 11/11/2026)
                    Destroy(_objetos[id]);
                }
                _objetos.Remove(id);
                EsquecerAArmadilha(id);
                EsquecerABarricada(id);
            }
        }

        GameObject Montar(Construcao c)
        {
            // a barricada de tábuas (11/11/2026, a etapa 5b): a malha da porta ou da janela, esticada para o vão
            bool tabuas = c.tipo == Item.TabuasBarricada;
            var malha = tabuas ? MalhaDaBarricada(c) : MalhaDe(c.tipo);
            var go = new GameObject($"Construção {c.id} ({Itens.Nome(c.tipo)})");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(c.pos, Quaternion.Euler(0f, c.giro, 0f));
            if (tabuas) go.transform.localScale = EscalaDoVao(c);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            // (a de tábuas: o colisor é o da inteira, mesmo danificada)
            var inteira = tabuas ? ItensDasArmadilhas.Barricada(Janela(c), false) : malha;
            var b = inteira != null ? inteira.bounds : new Bounds(Vector3.up * 0.2f, Vector3.one * 0.4f);
            var col = go.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;
            // a fogueira e a cama não seguram ninguém (passa-se por cima); a barricada, sim (e as estacas, 11/11/2026, e a
            // de tábuas, na porta e na janela)
            col.isTrigger = c.tipo != Item.Barricada && c.tipo != Item.Estacas && !tabuas;
            var mira = go.AddComponent<ConstrucaoNaMira>();
            mira.mundo = this;
            mira.id = c.id;
            // (a de tábuas na porta também corta o NavMesh: os zumbis que querem entrar param nela; na janela, não precisa,
            // o peitoril já fica fora do NavMesh)
            if (c.tipo == Item.Barricada || c.tipo == Item.Estacas || (tabuas && !Janela(c)))
            {
                var obstaculo = go.AddComponent<NavMeshObstacle>();
                obstaculo.shape = NavMeshObstacleShape.Box;
                obstaculo.center = b.center;
                obstaculo.size = b.size + new Vector3(0.1f, 0f, 0.2f);
                // (a de tábuas começa a 17 cm do chão: o buraco desce até o chão)
                if (tabuas) obstaculo.center = new Vector3(b.center.x, b.max.y / 2f, b.center.z);
                if (tabuas) obstaculo.size = new Vector3(b.size.x + 0.1f, b.max.y, b.size.z + 0.2f);
                obstaculo.carving = true;
                obstaculo.carveOnlyStationary = false;
            }
            if (c.tipo == Item.Fogueira) Fogo(go.transform, new Vector3(0f, 0.12f, 0f), materialDoFogo, 1f, 16f, true, go.layer);
            if (c.tipo == Item.KitFogueira) Fogo(go.transform, ItensDoFogo.FogoDaFogueira, materialDoFogo, 0.9f, 16f, true, go.layer);   // a portátil (11/11/2026)   // a luz maior (01/11/2026, a pedido: era 7 m)
            if (Itens.EhArmadilha(c.tipo)) MontarArmadilha(go, c);   // as latas soltas e o rastro da cerca (11/11/2026, a etapa 5a)
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
                    if (acesa && quem.Inventario.TochaDaMaoApagada) return "[E] Reacender a tocha no fogo";
                    if (ConstrucoesNoMundo.ChuvaEm(c.pos))
                        return acesa ? "Fogueira acesa: a chuva está apagando (debaixo de um teto ela fica)" : "Fogueira apagada: na chuva o fogo não pega (só debaixo de um teto)";
                    if (madeira == 0) return acesa ? "Fogueira acesa (mais lenha: 1 madeira)" : "Fogueira apagada: precisa de 1 madeira";
                    return acesa ? $"[E] Pôr lenha (1 madeira: +{ConstrucoesNoMundo.MinutosPorLenha:0} min)" : "[E] Acender a fogueira (1 madeira)";
                case Item.KitFogueira:
                    return mundo.DicaDaPortatil(c, quem);   // (11/11/2026, a etapa 4)
                case Item.Cama:
                    return Minha(c) ? "Sua cama: você volta aqui quando cair   ·   [E] Dormir e salvar   ·   [clique] Recolher" : $"A cama de {NomeDoDono(c.dono)}";
                case Item.Barricada:
                    // (a vida em % da máxima desde 11/11/2026, a etapa 5b: o Armadilheiro e a Fortaleza aumentam a máxima)
                    float pct = c.valor / ConstrucoesNoMundo.MaximoDe(c) * 100f;
                    return $"Barricada ({Mathf.CeilToInt(pct)}%)   ·   [E] Desmontar" + (pct >= ArmadilhasDoJogador.TabuasVoltamCom ? "" : " (sobram 2 madeiras)");
                case Item.TabuasBarricada:
                    return mundo.DicaDaBarricadaDeTabuas(c, quem);   // (11/11/2026, a etapa 5b: ConstrucoesNoMundo.Barricadas.cs)
                case Item.AlarmeLatas:
                case Item.TapetePregos:
                case Item.Estacas:
                case Item.CercaFogo:
                    return mundo.DicaDaArmadilha(c, quem);   // (11/11/2026, a etapa 5a)
            }
            return null;
        }

        // a cama do personagem deste jogador (pela chave dele)
        bool Minha(Construcao c) => c.tipo == Item.Cama && JogadorRede.Local != null && c.dono == JogadorRede.Local.Chave.Value;

        /// <summary>O nome do personagem dono da construção (pela chave dele), se ele estiver na partida; senão, "outro jogador".</summary>
        public static string NomeDoDono(ulong chave)
        {
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.IsSpawned && j.Chave.Value == chave)
                {
                    string nome = j.Aparencia.Value.nome.ToString();
                    if (!string.IsNullOrWhiteSpace(nome)) return nome;
                }
            return "outro jogador";
        }

        public override void Usar(ArmaJogador quem)
        {
            var d = mundo.Dados(id);
            if (d == null) return;
            // na própria cama, E dorme e salva (02/11/2026, o save); o clique recolhe
            if (Minha(d.Value))
            {
                Salvar.Salvamento.PedirDormir();
                JogadorRede.Local?.Vida?.PedirDormir();   // a mente (06/11/2026): dormir de verdade (o cansaço, a noite passando)
            }
            // a cama de outro jogador, só a dica de quem é (10/11/2026: só quem montou recolhe; o anfitrião confere também)
            // (a barricada de tábuas, 11/11/2026: só segurando o E ou o clique)
            else if (d.Value.tipo != Item.Cama && d.Value.tipo != Item.TabuasBarricada) mundo.PedirUsar(id);
        }

        public override bool UsaClique(ArmaJogador quem)
        {
            var d = mundo.Dados(id);
            if (d != null && d.Value.tipo == Item.TabuasBarricada) return mundo.PodeConsertar(d.Value, quem);   // (o conserto: segurar o clique)
            return d != null && Minha(d.Value);
        }

        public override void Clicar(ArmaJogador quem)
        {
            var d = mundo.Dados(id);
            if (d != null && d.Value.tipo == Item.TabuasBarricada)
            {
                // a barricada de tábuas (11/11/2026, a etapa 5b): segurar o clique conserta
                _segurandoOClique = true;
                quem.SegurarOClique(this, ArmadilhasDoJogador.SegundosDeConsertar);
                return;
            }
            mundo.PedirUsar(id);
        }

        // a barricada de tábuas (11/11/2026, a etapa 5b): segurar o E desmonta (do lado em que foi pregada, com a
        // ferramenta); segurar o clique conserta (o nível 2)
        bool _segurandoOClique;

        public override float Segurar(ArmaJogador quem)
        {
            var d = mundo.Dados(id);
            _segurandoOClique = false;
            return d != null && d.Value.tipo == Item.TabuasBarricada && mundo.PodeDesmontar(d.Value, quem) ? ArmadilhasDoJogador.SegundosDeDesmontar : 0f;
        }

        public override void Segurado(ArmaJogador quem)
        {
            if (_segurandoOClique) mundo.PedirConsertar(id);
            else mundo.PedirDesmontar(id);
            _segurandoOClique = false;
        }

        public override string Segurando(ArmaJogador quem) => _segurandoOClique ? "Pregando as tábuas soltas" : "Arrancando as tábuas";
    }
}
