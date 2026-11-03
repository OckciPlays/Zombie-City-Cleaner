using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// As portas que abrem das casas do mundo (o Plano_Portas.md da pasta de arte), no objeto "Mundo" da cena Mundo (com o
    /// NetworkObject):
    ///  - cada máquina sabe como cada porta começa, pela semente da casa (<see cref="GiroDoComeco"/>: as de fora fechadas
    ///    ou entreabertas, as de dentro abertas, uma em cada cinco fechada; <see cref="TrancadaNoComeco"/>: a da frente 60%,
    ///    as outras de fora 40%, a do porão 30%, a de dentro do quarto e do banheiro, fechada, 30%);
    ///  - a rede leva só o que mudou (<see cref="_mudadas"/>: aberta e destrancada, pela chave da porta; o anfitrião escreve);
    ///  - o E de quem joga vai ao anfitrião (<see cref="UsarRpc"/>), que confere a distância e abre, fecha, destranca por
    ///    dentro (as de fora) ou, trancada, manda todo mundo ver e ouvir a folha tremer (<see cref="TremerRpc"/>);
    ///  - as folhas das casas de verdade entram aos poucos (<see cref="portasPorQuadro"/>), com a malha de cada tamanho e
    ///    cor feita uma vez.
    /// Fora de uma partida (sem a rede), abre e fecha direto.
    /// </summary>
    public partial class PortasDoMundo : NetworkBehaviour
    {
        /// <summary>A camada das folhas: bate no jogador, no tiro e na visão dos zumbis, e fica fora do NavMesh (camadas 0 e 1).</summary>
        public const int Camada = 12;

        [Tooltip("Folhas postas por quadro (as de uma casa entram aos poucos).")]
        [Min(1)] public int portasPorQuadro = 12;
        [Tooltip("Até onde o anfitrião aceita o pedido de abrir (m, dos pés de quem pede ao meio da folha).")]
        public float distanciaMaxima = 3.5f;

        public static PortasDoMundo Instancia { get; private set; }

        public struct Mudanca : INetworkSerializeByMemcpy, IEquatable<Mudanca>
        {
            public ulong chave;
            public byte bits;
            public bool Equals(Mudanca o) => chave == o.chave && bits == o.bits;
        }

        const byte BitAberta = 1, BitDestrancada = 2;

        NetworkList<Mudanca> _mudadas;
        // o espelho da lista (ou, fora da rede, o estado daqui)
        readonly Dictionary<ulong, byte> _estado = new Dictionary<ulong, byte>();
        readonly Dictionary<ulong, (PortasDaConstrucao c, int i)> _registro = new Dictionary<ulong, (PortasDaConstrucao, int)>();
        readonly Dictionary<ulong, PortaDaCasa> _vivas = new Dictionary<ulong, PortaDaCasa>();
        // as que o jogador local sabe trancadas (viu ou ouviu tremer)
        readonly HashSet<ulong> _sabidas = new HashSet<ulong>();
        readonly List<PortasDaConstrucao> _porFazer = new List<PortasDaConstrucao>();
        readonly Dictionary<(int, int, string), Mesh> _malhas = new Dictionary<(int, int, string), Mesh>();

        /// <summary>Quantas portas e quantas janelas estão na cena (o F1 mostra).</summary>
        public int Vivas => _vivas.Count;
        public int JanelasVivas => _janelasVivas.Count;

        void Awake()
        {
            _mudadas = new NetworkList<Mudanca>();
            Instancia = this;
        }

        public override void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            foreach (var m in _malhas.Values) if (m != null) Destroy(m);
            _malhas.Clear();
            foreach (var m in _folhasDeJanela.Values) if (m != null) Destroy(m);
            _folhasDeJanela.Clear();
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _mudadas.Clear();
            _mudadas.OnListChanged += AoMudar;
            Sincronizar(true);
        }

        public override void OnNetworkDespawn()
        {
            _mudadas.OnListChanged -= AoMudar;
            _estado.Clear();
            _sabidas.Clear();
        }

        void AoMudar(NetworkListEvent<Mudanca> e) => Sincronizar(false);

        // as folhas de perto ficam como a lista da rede (com o som, quando não é o começo)
        void Sincronizar(bool ja)
        {
            if (IsSpawned)
            {
                _estado.Clear();
                foreach (var m in _mudadas) _estado[m.chave] = m.bits;
            }
            foreach (var p in _vivas.Values) Aplicar(p, ja);
            foreach (var j in _janelasVivas.Values) AplicarJanela(j, ja);
        }

        void Aplicar(PortaDaCasa p, bool ja)
        {
            if (p == null) return;
            bool antes = p.Aberta;
            bool mudou = _estado.TryGetValue(p.chave, out byte bits);
            bool aberta = mudou ? (bits & BitAberta) != 0 : p.dados.giro > 1f;
            p.Definir(aberta, !mudou, ja);
            if (!ja && p.Aberta != antes) Efeitos.Som(p.Aberta ? SomDasPortas.Abrir : SomDasPortas.Fechar, p.Meio, 0.8f, true, 25f);
        }

        // ------------------------------------------------------------ como começam

        /// <summary>A chave de uma porta na rede: a da casa e o número dela.</summary>
        public static ulong ChaveDaPorta(ulong obra, int i) => Sorteio.Misturar(obra ^ 0x504F525441000000UL ^ (ulong)(uint)i);

        static float Sorte(int semente, int i, int sal) => (Sorteio.Hash(semente, i, 7151, sal) & 0xFFFFFF) / (float)0x1000000;

        /// <summary>
        /// O giro do começo: as de fora (a da frente, as dos fundos e do lado e a do porão) fechadas três em cada quatro, e as
        /// outras entreabertas (de 15° a 40°) ou abertas (o giro do kit, se ele as abriu); a da garagem fechada uma em cada
        /// três; as de dentro abertas (o giro do kit), uma em cada cinco fechada.
        /// </summary>
        public static float GiroDoComeco(PortaDaObra d, int semente, int i)
        {
            float s = Sorte(semente, i, 1);
            switch (d.tipo)
            {
                case PortaDaObra.Tipo.Dentro: return s < 0.2f ? 0f : d.giro;
                case PortaDaObra.Tipo.Garagem: return s < 0.33f ? 0f : d.giro;
                default:
                    if (s < 0.75f) return 0f;
                    return d.giro > 1f ? d.giro : 15f + 25f * Sorte(semente, i, 4);
            }
        }

        /// <summary>Trancada no começo (só as fechadas): a da frente 60%, as outras de fora 40%, a do porão 30%, a de dentro do quarto e do banheiro 30%.</summary>
        public static bool TrancadaNoComeco(PortaDaObra d, int semente, int i)
        {
            if (GiroDoComeco(d, semente, i) > 1f) return false;
            float chance = d.tipo switch
            {
                PortaDaObra.Tipo.Frente => 0.6f,
                PortaDaObra.Tipo.Fora => 0.4f,
                PortaDaObra.Tipo.Porao => 0.3f,
                PortaDaObra.Tipo.Garagem => 0.3f,
                _ => d.comodo == "quarto" || d.comodo == "banheiro" ? 0.3f : 0f,
            };
            return Sorte(semente, i, 2) < chance;
        }

        bool AbertaAgora(PortasDaConstrucao c, int i) =>
            _estado.TryGetValue(c.chaves[i], out byte bits) ? (bits & BitAberta) != 0 : GiroDoComeco(c.portas[i], c.semente, i) > 1f;

        bool TrancadaAgora(PortasDaConstrucao c, int i) =>
            TrancadaNoComeco(c.portas[i], c.semente, i) && !(_estado.TryGetValue(c.chaves[i], out byte bits) && (bits & BitDestrancada) != 0);

        /// <summary>A porta está trancada agora (a do começo, e ninguém destrancou nem arrombou).</summary>
        public bool Trancada(PortaDaCasa p) => p.construcao != null && TrancadaAgora(p.construcao, p.indice);

        /// <summary>O jogador local sabe que ela está trancada (viu ou ouviu a folha tremer).</summary>
        public bool SabidaTrancada(ulong chave) => _sabidas.Contains(chave);

        // ------------------------------------------------------------ o E

        /// <summary>O E numa porta: pelo anfitrião (na rede) ou direto.</summary>
        public void PedirPorta(PortaDaCasa p)
        {
            if (p == null) return;
            if (!IsSpawned)
            {
                Decidir(p.construcao, p.indice, JogadorRede.Local != null ? JogadorRede.Local.transform.position : p.Meio, false);
                return;
            }
            UsarRpc(p.chave);
        }

        /// <summary>Quem joga → anfitrião: abre, fecha, destranca por dentro ou tenta a trancada.</summary>
        [Rpc(SendTo.Server)]
        void UsarRpc(ulong chave, RpcParams parametros = default)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return;
            if (!_registro.TryGetValue(chave, out var r) || r.c == null) return;
            var pes = cliente.PlayerObject.transform.position;
            if ((MeioDa(r.c, r.i) - (pes + Vector3.up)).sqrMagnitude > distanciaMaxima * distanciaMaxima) return;
            Decidir(r.c, r.i, pes, true);
        }

        // no anfitrião (ou fora da rede): o que o E faz nesta porta, para quem está em 'quem'
        void Decidir(PortasDaConstrucao c, int i, Vector3 quem, bool naRede)
        {
            if (c == null) return;
            var d = c.portas[i];
            if (AbertaAgora(c, i)) Escrever(c, i, false, false);
            else if (!TrancadaAgora(c, i)) Escrever(c, i, true, false);
            else if (d.DeFora && DoLadoQueAbre(c, d, quem)) Escrever(c, i, true, true);
            else if (naRede) TremerRpc(c.chaves[i]);
            else Tremeu(c.chaves[i]);
        }

        static Vector3 MeioDa(PortasDaConstrucao c, int i)
        {
            var d = c.portas[i];
            return c.transform.TransformPoint(d.dobradica + new Vector3(d.fechada.x, 0f, d.fechada.y) * (d.largura / 2) + Vector3.up);
        }

        static bool DoLadoQueAbre(PortasDaConstrucao c, PortaDaObra d, Vector3 ponto)
        {
            var q = c.transform.InverseTransformPoint(ponto);
            return (q.x - d.dobradica.x) * d.abre.x + (q.z - d.dobradica.z) * d.abre.y > 0f;
        }

        // no anfitrião: a porta fica aberta ou fechada ('destrancar': e destrancada, para sempre)
        void Escrever(PortasDaConstrucao c, int i, bool aberta, bool destrancar) => EscreverChave(c.chaves[i], aberta, destrancar);

        // no anfitrião: a porta ou a janela da chave
        void EscreverChave(ulong chave, bool aberta, bool destrancar)
        {
            byte bits = _estado.TryGetValue(chave, out byte b) ? b : (byte)0;
            bits = (byte)((bits & ~BitAberta) | (aberta ? BitAberta : 0) | (destrancar ? BitDestrancada : 0));
            if (!IsSpawned)
            {
                _estado[chave] = bits;
                if (_vivas.TryGetValue(chave, out var p)) Aplicar(p, false);
                if (_janelasVivas.TryGetValue(chave, out var j)) AplicarJanela(j, false);
                return;
            }
            for (int k = 0; k < _mudadas.Count; k++)
                if (_mudadas[k].chave == chave)
                {
                    _mudadas[k] = new Mudanca { chave = chave, bits = bits };
                    return;
                }
            _mudadas.Add(new Mudanca { chave = chave, bits = bits });
        }

        /// <summary>Anfitrião → todos: a porta trancada tremeu (a folha e o som; quem está perto fica sabendo que ela está trancada).</summary>
        [Rpc(SendTo.Everyone)]
        void TremerRpc(ulong chave) => Tremeu(chave);

        void Tremeu(ulong chave)
        {
            _sabidas.Add(chave);
            if (_janelasVivas.TryGetValue(chave, out var j) && j != null)
            {
                j.Tremer();
                Efeitos.Som(SomDasPortas.Trancada, j.Meio, 0.6f, true, 15f);
            }
            if (!_vivas.TryGetValue(chave, out var p) || p == null) return;
            p.Tremer();
            Efeitos.Som(SomDasPortas.Trancada, p.Meio, 0.9f, true, 18f);
        }

        // ------------------------------------------------------------ as casas e as folhas

        internal void Registrar(PortasDaConstrucao c)
        {
            for (int i = 0; i < c.chaves.Length; i++) _registro[c.chaves[i]] = (c, i);
            for (int i = 0; i < c.chavesDasJanelas.Length; i++) _registroDasJanelas[c.chavesDasJanelas[i]] = (c, i);
            _porFazer.Add(c);
        }

        internal void Tirar(PortasDaConstrucao c)
        {
            if (c.chaves == null) return;
            foreach (var k in c.chaves)
            {
                if (_registro.TryGetValue(k, out var r) && r.c == c) _registro.Remove(k);
                if (_vivas.TryGetValue(k, out var p) && (p == null || p.construcao == c)) _vivas.Remove(k);
            }
            if (c.chavesDasJanelas != null)
                foreach (var k in c.chavesDasJanelas)
                {
                    if (_registroDasJanelas.TryGetValue(k, out var r) && r.c == c) _registroDasJanelas.Remove(k);
                    if (_janelasVivas.TryGetValue(k, out var j) && (j == null || j.construcao == c)) _janelasVivas.Remove(k);
                }
            _porFazer.Remove(c);
        }

        internal void Esquecer(PortaDaCasa p)
        {
            if (_vivas.TryGetValue(p.chave, out var v) && v == p) _vivas.Remove(p.chave);
        }

        void Update()
        {
            int n = portasPorQuadro;
            while (n > 0 && _porFazer.Count > 0)
            {
                var c = _porFazer[0];
                if (c == null || c.proxima >= c.portas.Length && c.proximaJanela >= c.janelas.Length)
                {
                    _porFazer.RemoveAt(0);
                    continue;
                }
                if (c.proxima < c.portas.Length) Criar(c, c.proxima++);
                else CriarJanela(c, c.proximaJanela++);
                n--;
            }
            AtualizarArrombar();
        }

        void Criar(PortasDaConstrucao c, int i)
        {
            var d = c.portas[i];
            d.giro = GiroDoComeco(d, c.semente, i);
            var go = new GameObject($"Porta {i}") { layer = Camada };
            go.transform.SetParent(c.transform, false);
            go.transform.localPosition = d.dobradica;
            go.AddComponent<MeshFilter>().sharedMesh = Malha(d.largura, d.altura, d.cor);
            go.AddComponent<MeshRenderer>().sharedMaterial = Material;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(d.largura / 2, d.altura / 2, 0f);
            col.size = new Vector3(d.largura, d.altura, 0.05f);
            var p = go.AddComponent<PortaDaCasa>();
            p.construcao = c;
            p.indice = i;
            p.chave = c.chaves[i];
            p.dados = d;
            bool mudou = _estado.TryGetValue(p.chave, out byte bits);
            float giro = mudou ? ((bits & BitAberta) != 0 ? Mathf.Min(d.maximo, 90f) : 0f) : d.giro;
            // o obstáculo do NavMesh só no anfitrião (a IA dos zumbis roda lá)
            p.Preparar(giro, IsSpawned && IsServer);
            _vivas[p.chave] = p;
        }

        Material _material;
        Material Material
        {
            get
            {
                if (_material == null && TerrenoDoMundo.Instancia != null)
                    _material = TerrenoDoMundo.Instancia.materialDasCasas != null ? TerrenoDoMundo.Instancia.materialDasCasas : TerrenoDoMundo.Instancia.materialDaCidade;
                return _material;
            }
        }

        // a folha (a origem na dobradiça, ao longo do +X), com as duas almofadas dos dois lados e a maçaneta atravessada
        Mesh Malha(float largura, float altura, string cor)
        {
            var k = (Mathf.RoundToInt(largura * 1000f), Mathf.RoundToInt(altura * 1000f), cor);
            if (_malhas.TryGetValue(k, out var m) && m != null) return m;
            var o = MalhaDeObra.Emprestar();
            o.Caixa(Matrix4x4.Translate(new Vector3(largura / 2, altura / 2, 0f)), new Vector3(largura, altura, 0.045f), cor);
            float pa = altura * 0.36f, margem = 0.11f;
            o.Caixa(Matrix4x4.Translate(new Vector3(largura / 2, altura - margem - pa / 2, 0f)), new Vector3(largura - 2 * margem, pa, 0.055f), cor);
            o.Caixa(Matrix4x4.Translate(new Vector3(largura / 2, margem + pa / 2, 0f)), new Vector3(largura - 2 * margem, pa, 0.055f), cor);
            o.Caixa(Matrix4x4.Translate(new Vector3(largura - 0.08f, Mathf.Min(1.0f, altura * 0.48f), 0f)), new Vector3(0.04f, 0.04f, 0.16f), "Cromo");
            m = o.ParaMesh($"Porta {largura:0.00} × {altura:0.00} ({cor})");
            MalhaDeObra.Devolver(o);
            _malhas[k] = m;
            return m;
        }
    }
}
