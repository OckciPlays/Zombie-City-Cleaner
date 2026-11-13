using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Salvar;
using ZombieCityCleaner.World;
using static ZombieCityCleaner.Mundo.PlantaDeBunker;

namespace ZombieCityCleaner.Moradores
{
    /// <summary>
    /// O que o terminal do bunker pede ao anfitrião (a parte 7 do Plano_NPCs_Bunker.md da pasta de arte): o registro (o
    /// diário do bunker, os 100 últimos, no save), a troca da cama de um morador e a meta de um lugar do estoque. O registro
    /// junta o que os jogadores guardam e pegam nas estantes num aviso só por estante (depois de uns segundos parados).
    /// </summary>
    public partial class MoradoresDoBunker
    {
        public const int MaxNoRegistro = 100;
        const float JuntarMexidas = 6f;   // s sem mexer na estante: o aviso sai

        readonly List<string> _registro = new List<string>();
        bool _carregado;

        sealed class Mexida
        {
            public string quem;
            public int estante;
            public bool guardou;
            public float ultima;
            public readonly List<(Item item, int n)> itens = new List<(Item, int)>();
        }
        readonly Dictionary<(ulong, int, bool), Mexida> _mexidas = new Dictionary<(ulong, int, bool), Mexida>();
        readonly List<(ulong, int, bool)> _prontas = new List<(ulong, int, bool)>();

        /// <summary>O registro do bunker (do mais velho ao mais novo), no anfitrião.</summary>
        public IReadOnlyList<string> Registro => _registro;
        /// <summary>O registro e as metas deste mundo já vieram do save (só aí o save grava os dois).</summary>
        public bool Carregado => _carregado;

        /// <summary>Escreve no registro do bunker, com o dia e a hora do jogo ("Dia 12, 07:14: ...").</summary>
        public void Registrar(string texto)
        {
            float h = CicloDoDia.Hora;
            int hh = Mathf.FloorToInt(h), mm = Mathf.Clamp(Mathf.FloorToInt((h - hh) * 60f), 0, 59);
            _registro.Add($"Dia {CicloDoDia.Dia}, {hh:00}:{mm:00}: {texto}");
            if (_registro.Count > MaxNoRegistro) _registro.RemoveRange(0, _registro.Count - MaxNoRegistro);
        }

        // o registro e as metas do mundo salvo (uma vez por mundo, no anfitrião)
        void CarregarOTerminal()
        {
            _carregado = true;
            _registro.Clear();
            _mexidas.Clear();
            var salvo = Salvamento.Atual;
            if (salvo != null && salvo.registroDoBunker != null) _registro.AddRange(salvo.registroDoBunker);
            if (_registro.Count > MaxNoRegistro) _registro.RemoveRange(0, _registro.Count - MaxNoRegistro);
            EstoqueDoBunker.CarregarAsMetas(salvo?.metasDosLugares);
        }

        void EsquecerOTerminal()
        {
            _carregado = false;
            _registro.Clear();
            _mexidas.Clear();
            EstoqueDoBunker.CarregarAsMetas(null);
        }

        // ------------------------------------------------------------------ as estantes

        void AoMexerNaEstante(ulong cliente, int estante, Item item, int n, bool guardou)
        {
            if (!_carregado) return;
            var chave = (cliente, estante, guardou);
            if (!_mexidas.TryGetValue(chave, out var m))
            {
                string quem = "Alguém";
                var rede = NetworkManager.Singleton;
                if (rede != null && rede.ConnectedClients.TryGetValue(cliente, out var c) && c.PlayerObject != null)
                {
                    var j = c.PlayerObject.GetComponent<JogadorRede>();
                    if (j != null) quem = j.NomeNoMapa;
                }
                _mexidas[chave] = m = new Mexida { quem = quem, estante = estante, guardou = guardou };
            }
            m.ultima = Time.unscaledTime;
            int k = m.itens.FindIndex(x => x.item == item);
            if (k >= 0) m.itens[k] = (item, m.itens[k].n + n);
            else m.itens.Add((item, n));
        }

        // os avisos das estantes paradas há uns segundos
        void AvisarAsMexidas()
        {
            if (_mexidas.Count == 0) return;
            _prontas.Clear();
            foreach (var kv in _mexidas) if (Time.unscaledTime - kv.Value.ultima > JuntarMexidas) _prontas.Add(kv.Key);
            foreach (var chave in _prontas)
            {
                var m = _mexidas[chave];
                _mexidas.Remove(chave);
                var partes = m.itens.ConvertAll(x => x.n == 1 ? (Itens.Loot(x.item)?.nomeComArtigo ?? Itens.Nome(x.item)) : Itens.ComNumero(x.item, x.n));
                string lista = partes.Count == 1 ? partes[0] : string.Join(", ", partes.GetRange(0, partes.Count - 1)) + " e " + partes[partes.Count - 1];
                Registrar($"{m.quem} {(m.guardou ? "guardou" : "pegou")} {lista} {NoLugar(m.estante, m.guardou)}.");
            }
        }

        // "na despensa (B2)", "do depósito do B1"
        static string NoLugar(int estante, bool guardou)
        {
            var todos = MoveisDoBunker.Todos;
            string andar = estante >= 0 && estante < todos.Length ? Codigos[todos[estante].andar] : "?";
            return EstoqueDoBunker.TipoDe(estante) switch
            {
                TipoDoEstoque.Despensa => $"{(guardou ? "na" : "da")} despensa ({andar})",
                TipoDoEstoque.Agua => $"{(guardou ? "na" : "da")} bancada da água ({andar})",
                _ => $"{(guardou ? "no" : "do")} depósito do {andar}",
            };
        }

        // ------------------------------------------------------------------ os pedidos do terminal

        /// <summary>
        /// No anfitrião, o terminal: o morador <paramref name="id"/> passa a dormir na cama do móvel <paramref name="movel"/>
        /// (com lugar: a de casal leva dois). Ele fica com o lado livre mais longe das camas dos outros; se está dormindo na
        /// de antes, levanta e vai para a nova.
        /// </summary>
        public bool MudarCama(int id, int movel, string quem)
        {
            var m = _moradores.Find(x => x.ficha.id == id);
            int cama = CamasDoBunker.Todas.FindIndex(c => c.movel == movel);
            if (m == null || cama < 0) return false;
            var c = CamasDoBunker.Todas[cama];
            var todos = LugaresDoBunker.Todos;
            if (m.vida.cama >= 0 && todos[m.vida.cama].movel == movel) return false;
            int donos = 0;
            foreach (var o in _moradores) if (o != m && o.vida.cama >= 0 && todos[o.vida.cama].movel == movel) donos++;
            if (donos >= c.capacidade) return false;
            int melhor = -1;
            float longe = -1f;
            foreach (int lado in c.lados)
            {
                if (_moradores.Exists(o => o != m && o.vida.cama == lado)) continue;
                float perto = float.MaxValue;
                foreach (var o in _moradores)
                    if (o != m && o.vida.cama >= 0 && todos[o.vida.cama].andar == todos[lado].andar)
                        perto = Mathf.Min(perto, Plano(todos[o.vida.cama].raiz - todos[lado].raiz));
                if (perto > longe)
                {
                    longe = perto;
                    melhor = lado;
                }
            }
            if (melhor < 0) return false;
            int antes = m.vida.cama;
            m.vida.cama = melhor;
            // dormindo (ou indo dormir) na de antes: decide de novo, e a agenda manda para a nova
            if (antes >= 0 && m.vida.lugar == antes) m.vida.ate = Mathf.Min(m.vida.ate, Agora);
            Registrar($"{quem} mudou a cama de {m.Nome}: {c.nome}.");
            return true;
        }

        /// <summary>No anfitrião, o terminal: a meta de um lugar do estoque (os que têm meta).</summary>
        public bool MudarMeta(TipoDoEstoque tipo, int meta, string quem)
        {
            if (!EstoqueDoBunker.MudarMeta(tipo, meta)) return false;
            string de = tipo switch
            {
                TipoDoEstoque.Despensa => "da despensa",
                TipoDoEstoque.Agua => "da bancada da água",
                TipoDoEstoque.Suporte => "do suporte dos tacos",
                TipoDoEstoque.Fumo => "da mesinha do fumo",
                TipoDoEstoque.Enfermaria => "do armário da enfermaria",
                _ => "do depósito",
            };
            Registrar($"{quem} mudou a meta {de} para {EstoqueDoBunker.Meta(tipo)}.");
            return true;
        }
    }
}
