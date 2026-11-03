using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// As bases militares provisórias (03/11/2026, a pedido: "os militares, mais resistentes, com colete e capacete, rifle,
    /// sniper, espingarda e munição pesada, só nas bases militares"; a base de verdade fica para a próxima fase). No objeto
    /// "Mundo" (o <see cref="SobreviventesDoMundo"/> põe na hora, sem mexer na cena), uma por lugar
    /// <see cref="TipoDeLugar.BaseMilitar"/> do mapa:
    ///  - <b>a construção</b> (em cada máquina, até <see cref="raioDaConstrucao"/> m do jogador local; nada vai pela rede: sai
    ///    da <see cref="PlantaDaBase"/>, igual em todas): a cerca, o portão, a guarita, as barracas, os veículos, os ninhos,
    ///    as caixas, o mastro e os postes de luz;
    ///  - <b>os militares</b> (no anfitrião): de 6 a 8, o mesmo <see cref="Sobrevivente"/> dos grupos, com o papel
    ///    <see cref="PapelDoSobrevivente.Militar"/>, sempre hostis; aparecem com alguém a menos de <see cref="chegarPerto"/> m
    ///    do meio e saem com todos a mais de <see cref="sairLonge"/> m. Rifle (50%), espingarda (25%), sniper (15%) ou a
    ///    submetralhadora (10%); a roupa "Militar" do sorteio com o colete tático e o capacete; vida 150 (o colete corta pela
    ///    metade o tiro no tronco; com o capacete, o tiro na cabeça vale 2,5 vezes, sem matar de uma vez);
    ///  - <b>os mortos</b> continuam mortos; <see cref="DiasParaOReforco"/> dias de jogo depois da última morte, chega outro
    ///    grupo (com ninguém perto);
    ///  - <b>o save</b>: os mortos, a geração (o grupo novo tem outras caras) e o dia do reforço.
    /// </summary>
    public partial class BasesMilitaresDoMundo : MonoBehaviour
    {
        public static BasesMilitaresDoMundo Instancia { get; private set; }

        [Tooltip("Até onde a construção da base aparece em volta do jogador local (m, do meio dela).")]
        public float raioDaConstrucao = 320f;
        [Tooltip("Os militares aparecem com alguém a menos disto do meio da base (m).")]
        public float chegarPerto = 170f;
        [Tooltip("E saem com todos a mais disto (m).")]
        public float sairLonge = 260f;

        /// <summary>O grupo dos militares de uma base na rede é este mais o índice dela.</summary>
        public const int GrupoBase = 3000;
        public const int DiasParaOReforco = 3;

        // no anfitrião
        sealed class Estado
        {
            public int mortos, geracao, diaDoReforco = -1;
            public GrupoDeSobreviventes grupo;
        }

        readonly List<Estado> _estados = new List<Estado>();
        readonly List<Lugar> _bases = new List<Lugar>();
        DadosDoMundo _dados;
        float _proximaConferencia, _proximoPensar;

        void Awake() => Instancia = this;

        void OnDestroy()
        {
            ApagarConstrucoes();
            if (Instancia == this) Instancia = null;
        }

        static bool Servidor => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer;

        static DadosDoMundo Dados
        {
            get
            {
                var m = MundoDaPartida.Instancia;
                return m != null && m.Pronto && !m.NoDiaZero ? m.Dados : null;
            }
        }

        /// <summary>Quantas bases o mundo tem.</summary>
        public int Quantas => _bases.Count;

        /// <summary>O desenho da base (null fora do mundo).</summary>
        public PlantaDaBase Planta(int b)
        {
            Garantir();
            return _dados != null && b >= 0 && b < _bases.Count ? PlantaDaBase.Da(_dados, _bases[b], b) : null;
        }

        // o mundo mudou (outra partida, ou saiu dela): as bases e os estados de novo
        void Garantir()
        {
            var d = Dados;
            if (d == _dados) return;
            _dados = d;
            PlantaDaBase.Esquecer();   // (o mundo anterior não fica preso no cache das plantas)
            ApagarConstrucoes();
            _estados.Clear();
            _bases.Clear();
            if (d != null) _bases.AddRange(PlantaDaBase.Bases(d));
            while (_estados.Count < _bases.Count) _estados.Add(new Estado());
            if (d != null) Debug.Log($"[Bases] {_bases.Count} bases militares no mundo.");
        }

        /// <summary>A base mais perto do ponto e a distância até o meio dela (-1 sem nenhuma).</summary>
        public int MaisPerto(Vector3 ponto, out float distancia)
        {
            Garantir();
            distancia = float.MaxValue;
            int melhor = -1;
            for (int b = 0; b < _bases.Count; b++)
            {
                float d = Vector2.Distance(new Vector2(ponto.x, ponto.z), _bases[b].Centro);
                if (d < distancia)
                {
                    distancia = d;
                    melhor = b;
                }
            }
            return melhor;
        }

        void Update()
        {
            Garantir();
            if (_dados == null) return;
            AtualizarConstrucoes();
            if (!Servidor) return;
            float agora = Time.time;
            if (agora >= _proximoPensar)
            {
                _proximoPensar = agora + 0.2f;
                foreach (var e in _estados) e.grupo?.Pensar();
            }
            if (agora < _proximaConferencia) return;
            _proximaConferencia = agora + 1f;
            ConferirOsMilitares();
        }

        // ------------------------------------------------------------------ os militares (no anfitrião)

        void ConferirOsMilitares()
        {
            var prefab = SobreviventesDoMundo.Instancia != null ? SobreviventesDoMundo.Instancia.prefab : null;
            if (prefab == null) return;
            for (int b = 0; b < _estados.Count; b++)
            {
                var e = _estados[b];
                var p = Planta(b);
                if (p == null) continue;
                // o reforço: dias depois da última morte, com ninguém perto (os mortos voltam a ser outros)
                if (e.grupo == null && e.mortos != 0 && e.diaDoReforco >= 0 && CicloDoDia.Dia >= e.diaDoReforco)
                {
                    e.mortos = 0;
                    e.geracao++;
                    e.diaDoReforco = -1;
                    Debug.Log($"[Bases] Chegou o reforço na {p.Lugar.Nome}.");
                }
                float perto = GrupoDeSobreviventes.PertoDeUmJogador(p.Meio);
                if (e.grupo == null && perto < chegarPerto) Povoar(b, p, e, prefab);
                else if (e.grupo != null && perto > sairLonge) Recolher(e);
            }
        }

        void Povoar(int b, PlantaDaBase p, Estado e, GameObject prefab)
        {
            var g = new GrupoDeSobreviventes { cidade = -1, baseMilitar = b, quantos = p.Militares };
            var s = new System.Random(unchecked(_dados.Semente * 3911 + b * 60013 + e.geracao * 7919 + 5));
            for (int i = 0; i < p.Militares; i++)
            {
                int r = s.Next(100);
                var arma = r < 50 ? TipoDeArma.Rifle : r < 75 ? TipoDeArma.Espingarda : r < 90 ? TipoDeArma.Sniper : TipoDeArma.Submetralhadora;
                int sementeDaRoupa = s.Next();
                if ((e.mortos & (1 << i)) != 0) continue;   // este morreu
                var posto = p.Postos[i];
                var pos = NoChao(p.Mundo(posto.pos));
                var go = Instantiate(prefab, pos, Quaternion.Euler(0f, p.GiroNoMundo(posto.giro), 0f));
                var sob = go.GetComponent<Sobrevivente>();
                sob.Identidade.Value = new IdentidadeDoSobrevivente
                {
                    semente = sementeDaRoupa, arma = arma, roupa = 1, grupo = (ushort)(GrupoBase + b), papel = PapelDoSobrevivente.Militar, indice = (byte)i,
                };
                sob.grupo = g;
                sob.baseMilitar = b;
                sob.nome = "Militar";
                sob.posto = pos;
                sob.ronda = NoChao(p.Mundo(posto.ronda));
                sob.giroDoPosto = p.GiroNoMundo(posto.giro);
                SobreviventesDoMundo.Encher(sob, arma, true);
                go.GetComponent<NetworkObject>().Spawn(true);
                g.membros.Add(sob);
            }
            e.grupo = g;
            Debug.Log($"[Bases] Os militares da {p.Lugar.Nome} apareceram: {g.membros.Count} de {p.Militares}.");
        }

        // o ponto do NavMesh no chão perto do lugar (sem o NavMesh ainda, longe dos jogadores: o próprio lugar, no chão)
        static Vector3 NoChao(Vector3 pos) =>
            NavMesh.SamplePosition(pos, out var hit, 1f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - pos.y) < 0.6f ? hit.position : pos;

        void Recolher(Estado e)
        {
            foreach (var m in e.grupo.membros.ToArray())
            {
                if (m == null) continue;
                var no = m.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned) no.Despawn(true);
            }
            e.grupo.membros.Clear();
            e.grupo = null;
        }

        /// <summary>No anfitrião: um militar morreu (a máscara dos mortos, o dia do reforço e a mochila no chão).</summary>
        public void AoMorrer(Sobrevivente s)
        {
            if (!Servidor || s == null) return;
            Garantir();
            int b = s.baseMilitar;
            if (b >= 0 && b < _estados.Count)
            {
                var e = _estados[b];
                e.mortos |= 1 << s.Identidade.Value.indice;
                e.diaDoReforco = CicloDoDia.Dia + DiasParaOReforco;
            }
            SobreviventesDoMundo.LargarAMochila(s, null);
            Debug.Log($"[Bases] Morreu um militar da {Planta(b)?.Lugar.Nome}.");
        }

        /// <summary>No anfitrião (o F1): os militares de todas as bases saem (voltam quando alguém chegar perto).</summary>
        public void TirarAsPessoas()
        {
            foreach (var e in _estados) if (e.grupo != null) Recolher(e);
        }

        /// <summary>No anfitrião (o F1): a base volta ao começo, com todos vivos (aparecem de novo com alguém perto).</summary>
        public void Recomecar(int b)
        {
            Garantir();
            if (b < 0 || b >= _estados.Count) return;
            var e = _estados[b];
            if (e.grupo != null) Recolher(e);
            e.mortos = 0;
            e.diaDoReforco = -1;
            _proximaConferencia = 0f;
        }

        /// <summary>Os militares que estão na base agora (no anfitrião).</summary>
        public GrupoDeSobreviventes Grupo(int b) => b >= 0 && b < _estados.Count ? _estados[b].grupo : null;

        /// <summary>Quantos da base morreram (no anfitrião).</summary>
        public int Mortos(int b)
        {
            if (b < 0 || b >= _estados.Count) return 0;
            int n = 0;
            for (int k = 0; k < 16; k++) if ((_estados[b].mortos & (1 << k)) != 0) n++;
            return n;
        }

        /// <summary>O dia do jogo em que chega o reforço (-1: ninguém morreu).</summary>
        public int DiaDoReforco(int b) => b >= 0 && b < _estados.Count ? _estados[b].diaDoReforco : -1;

        // ------------------------------------------------------------------ o save

        /// <summary>O estado de cada base (o save): o índice, os mortos (um bit por posto), a geração e o dia do reforço.</summary>
        public List<(int indice, int mortos, int geracao, int dia)> Copia()
        {
            Garantir();
            var lista = new List<(int, int, int, int)>();
            for (int b = 0; b < _estados.Count; b++) lista.Add((b, _estados[b].mortos, _estados[b].geracao, _estados[b].diaDoReforco));
            return lista;
        }

        public void Restaurar(IEnumerable<(int indice, int mortos, int geracao, int dia)> lista)
        {
            Garantir();
            foreach (var (b, mortos, geracao, dia) in lista)
            {
                if (b < 0 || b >= _estados.Count) continue;
                var e = _estados[b];
                if (e.grupo != null) Recolher(e);
                e.mortos = mortos;
                e.geracao = geracao;
                e.diaDoReforco = dia;
            }
        }
    }
}
