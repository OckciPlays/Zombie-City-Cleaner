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
    /// A emboscada nas cidades grandes (18/11/2026, a etapa 3 da proteção, a pedido: "alguns npcs na cidade, já armar
    /// emboscada para a gente ... em cima dos prédios, dentro dos apartamentos ... quando chegarmos perto em um momento
    /// aleatório, mas isso precisa ser algo raro, se não vai ficar chato e enjoativo"). Só no anfitrião, posto pelo
    /// <see cref="SobreviventesDoMundo"/> no mesmo objeto.
    /// <list type="bullet">
    /// <item>rara: ao entrar numa cidade grande, 1 chance em 5, e no máximo uma por cidade a cada dia de jogo; quando sai, ela
    /// acontece num momento sorteado (de 20 a 90 s depois), se o jogador ainda estiver na cidade;</item>
    /// <item>os postos, em volta dele (de 18 a 75 m): as janelas que abrem das casas e das lojas viradas para ele (por dentro,
    /// ao lado do vão, o andar de cima também; no máximo 2 por casa) e, no mundo sorteado, o telhado dos prédios (atrás da
    /// mureta, do lado dele). Com menos de 2 postos, não há emboscada (o Lakewood tem poucas casas por rua);</item>
    /// <item>de 4 a 7 civis (as armas de sucata: o rifle de precisão no telhado), um por quadro, quietos e agachados nos postos;
    /// com um jogador a menos de 38 m de um deles (ou atirando num), todos abrem fogo juntos, cada um do seu posto
    /// (Sobrevivente.Emboscada.cs);</item>
    /// <item>somem quando todos os jogadores estão longe (140 m antes do tiroteio; 120 m depois dele, já sem lutar).</item>
    /// </list>
    /// Os apartamentos dos prédios por dentro ficam para depois (os andares só carregam com alguém dentro); hoje, as janelas
    /// são as das casas e das lojas, e o telhado, o dos prédios.
    /// </summary>
    public class EmboscadasDoMundo : MonoBehaviour
    {
        public static EmboscadasDoMundo Instancia { get; private set; }

        public const int GrupoBase = 4000;
        /// <summary>A chance de uma emboscada ao entrar numa cidade grande.</summary>
        public float chance = 0.2f;
        /// <summary>A que distância de um deles os jogadores disparam a emboscada (m).</summary>
        public float disparo = 38f;

        sealed class Emboscada
        {
            public GrupoDeSobreviventes grupo;
            public int cidade;
            public Vector3 centro;
            public bool disparada;
            public float ultimoCombate;
        }

        Emboscada _atual;
        readonly Dictionary<int, int> _diaDaUltima = new Dictionary<int, int>();
        readonly Dictionary<JogadorRede, int> _cidadeDe = new Dictionary<JogadorRede, int>();
        readonly List<(Emboscada e, System.Action criar)> _aAparecer = new List<(Emboscada, System.Action)>();
        JogadorRede _quem;
        int _cidadeMarcada = -1;
        float _armarEm = -1f, _proximaOlhada, _proximoPensar;
        int _contador;

        static readonly List<JanelaDaCasa> _janelas = new List<JanelaDaCasa>(64);

        void Awake() => Instancia = this;

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        static bool Servidor => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        void Update()
        {
            if (!Servidor || MundoDoJogo.Pronto == null) return;
            if (_aAparecer.Count > 0)
            {
                var (e, criar) = _aAparecer[0];
                _aAparecer.RemoveAt(0);
                if (e.grupo != null) e.grupo.nascendo = Mathf.Max(0, e.grupo.nascendo - 1);
                criar();
            }
            float agora = Time.time;
            if (_atual != null && agora >= _proximoPensar)
            {
                _proximoPensar = agora + 0.2f;
                _atual.grupo?.Pensar();
                Disparar(agora);
            }
            if (agora < _proximaOlhada) return;
            _proximaOlhada = agora + 1f;
            if (_atual != null) Conferir(agora);
            else
            {
                OlharAsEntradas();
                if (_armarEm > 0f && agora >= _armarEm) TentarArmar();
            }
        }

        // ------------------------------------------------------------------ quando

        // quem entrou numa cidade grande: o sorteio (uma por cidade por dia) e o momento
        void OlharAsEntradas()
        {
            var mundo = MundoDoJogo.Pronto;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.AlvoValido || !j.Criado) continue;
                var p = j.transform.position;
                int c = mundo.CidadeEm(p.x, p.z);
                _cidadeDe.TryGetValue(j, out int antes);
                _cidadeDe[j] = c;
                // (no mundo sorteado, a cidade grande; no Lakewood, que não tem prédios e só a Woodcreek tem casas, qualquer uma)
                bool vale = mundo.CidadeGrande(c) || MundoDaPartida.Instancia == null || MundoDaPartida.Instancia.Dados == null;
                if (c < 0 || c == antes || !vale || _armarEm > 0f) continue;
                if (_diaDaUltima.TryGetValue(c, out int dia) && dia == CicloDoDia.Dia) continue;
                _diaDaUltima[c] = CicloDoDia.Dia;   // (o sorteio é um só por cidade por dia, saia ou não)
                if (Random.value >= chance) continue;
                _quem = j;
                _cidadeMarcada = c;
                _armarEm = Time.time + Random.Range(20f, 90f);
                Debug.Log($"[Emboscadas] Vai ter uma emboscada na cidade {c} daqui a {_armarEm - Time.time:F0} s.");
            }
        }

        void TentarArmar()
        {
            _armarEm = -1f;
            if (_quem == null || !_quem.AlvoValido) return;
            var p = _quem.transform.position;
            if (MundoDoJogo.Pronto.CidadeEm(p.x, p.z) != _cidadeMarcada) return;   // (já saiu da cidade)
            Armar(_quem, out _);
        }

        /// <summary>O F1: uma emboscada agora, em volta do jogador (a cidade e o sorteio não contam).</summary>
        public string ArmarAgora(JogadorRede j)
        {
            if (j == null) return "Sem jogador.";
            if (_atual != null) Recolher();
            return Armar(j, out string aviso) ? aviso : "Sem emboscada: " + aviso;
        }

        // ------------------------------------------------------------------ onde

        struct Posto
        {
            public Vector3 pos;
            public float giro;
            public JanelaDaCasa janela;
            public bool telhado;
        }

        bool Armar(JogadorRede j, out string aviso)
        {
            var prefab = SobreviventesDoMundo.Instancia != null ? SobreviventesDoMundo.Instancia.prefab : null;
            if (prefab == null) { aviso = "sem o prefab dos sobreviventes."; return false; }
            var p = j.transform.position;
            var postos = new List<Posto>();
            string porque = PostosNasJanelas(p, postos);
            int nasJanelas = postos.Count;
            PostosNosTelhados(p, postos);
            if (postos.Count < 2)
            {
                aviso = $"só {postos.Count} lugares bons em volta ({nasJanelas} em janelas: {porque}).";
                Debug.Log("[Emboscadas] " + aviso);
                return false;
            }
            // os postos em ordem sorteada; de 4 a 7 deles (os que houver)
            for (int i = postos.Count - 1; i > 0; i--)
            {
                int k = Random.Range(0, i + 1);
                (postos[i], postos[k]) = (postos[k], postos[i]);
            }
            int n = Mathf.Min(postos.Count, Random.Range(4, 8));
            var e = new Emboscada { cidade = MundoDoJogo.Pronto.CidadeEm(p.x, p.z), centro = p };
            var g = new GrupoDeSobreviventes { cidade = -1, quantos = n, emboscada = true };
            e.grupo = g;
            int id = GrupoBase + (_contador++ % 900);
            for (int i = 0; i < n; i++)
            {
                var posto = postos[i];
                // as armas: as de sucata dos civis (o rifle de precisão no telhado)
                var real = posto.telhado ? TipoDeArma.Rifle : Random.value < 0.45f ? TipoDeArma.Rifle : Random.value < 0.65f ? TipoDeArma.Pistola : TipoDeArma.Submetralhadora;
                var arma = SobreviventesDoMundo.ArmaDoCivil(real);
                int semente = Random.Range(int.MinValue, int.MaxValue);
                int indice = i;
                g.nascendo++;
                _aAparecer.Add((e, () =>
                {
                    if (_atual != e) return;
                    var go = Instantiate(prefab, posto.pos, Quaternion.Euler(0f, posto.giro, 0f));
                    var sob = go.GetComponent<Sobrevivente>();
                    sob.Identidade.Value = new IdentidadeDoSobrevivente
                    {
                        semente = semente, arma = arma, roupa = (byte)(Random.value < 0.5f ? 0 : 3), grupo = (ushort)id, papel = PapelDoSobrevivente.Busca, indice = (byte)indice,
                    };
                    sob.grupo = g;
                    sob.naEmboscada = true;
                    sob.posto = posto.pos;
                    sob.ronda = posto.pos;
                    sob.giroDoPosto = posto.giro;
                    if (posto.janela != null) sob.JanelaDaEmboscada(posto.janela);
                    SobreviventesDoMundo.Encher(sob, arma);
                    go.GetComponent<NetworkObject>().Spawn(true);
                    g.membros.Add(sob);
                }));
            }
            _atual = e;
            aviso = $"uma emboscada de {n} em volta, a {Mathf.RoundToInt(postos.Count > 0 ? Vector3.Distance(p, postos[0].pos) : 0)} m ({nasJanelas} lugares em janelas, {postos.Count - nasJanelas} em telhados).";
            Debug.Log("[Emboscadas] " + aviso);
            return true;
        }

        // as janelas que abrem (casas e lojas) viradas para o jogador, de 22 a 70 m: por dentro, ao lado do vão
        static string PostosNasJanelas(Vector3 p, List<Posto> saida)
        {
            var portas = PortasDoMundo.Instancia;
            if (portas == null) return "sem as portas";
            portas.JanelasPerto(p, 75f, _janelas);
            int longe = 0, virada = 0, cheia = 0, semChao = 0, tapada = 0;
            var porCasa = new Dictionary<PortasDaConstrucao, int>();
            foreach (var j in _janelas)
            {
                if (saida.Count >= 10) break;
                if (j == null || !PortasDoMundo.PodeAbrirPorDentro(j)) continue;
                Vector3 paraEle = p - j.transform.position;
                paraEle.y = 0f;
                float d = paraEle.magnitude;
                if (d < 18f || d > 75f) { longe++; continue; }
                Vector3 fora = j.transform.forward;
                fora.y = 0f;
                if (fora.sqrMagnitude < 0.01f) continue;
                fora.Normalize();
                if (Vector3.Dot(fora, paraEle / d) < 0.35f) { virada++; continue; }
                // (de lá se vê onde ele está: as árvores, os carros e as outras casas no meio não servem)
                if (Physics.Linecast(j.Meio + fora * 0.4f, p + Vector3.up * 1.4f, (1 << 0) | (1 << 11), QueryTriggerInteraction.Ignore)) { tapada++; continue; }
                porCasa.TryGetValue(j.construcao, out int ja);
                if (ja >= 2) { cheia++; continue; }
                int antes = saida.Count;
                Vector3 aoLongo = Vector3.Cross(Vector3.up, fora);
                float meia = j.dados.largura * 0.5f;
                float s = Random.value < 0.5f ? 1f : -1f;
                for (int t = 0; t < 2; t++, s = -s)
                {
                    var q = j.transform.position - fora * 0.5f + aoLongo * (s * (meia + 0.32f)) - Vector3.up * 0.7f;
                    if (!NavMesh.SamplePosition(q, out var hit, 0.9f, NavMesh.AllAreas) || !j.DoLadoDeDentro(hit.position)) continue;
                    if (Mathf.Abs(hit.position.y - (j.transform.position.y - 0.9f)) > 0.7f) continue;   // (o chão do mesmo andar)
                    saida.Add(new Posto { pos = hit.position, giro = Quaternion.LookRotation(fora).eulerAngles.y, janela = j });
                    porCasa[j.construcao] = ja + 1;
                    break;
                }
                if (saida.Count == antes) semChao++;
            }
            return $"{_janelas.Count} janelas perto; longe ou colada {longe}, virada para o outro lado {virada}, sem ver onde ele está {tapada}, casa cheia {cheia}, sem o chão por dentro {semChao}";
        }

        // o telhado dos prédios (o mundo sorteado), de 30 a 90 m: atrás da mureta, do lado do jogador
        static void PostosNosTelhados(Vector3 p, List<Posto> saida)
        {
            var d = MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;
            if (d == null || d.Plano == null) return;
            var t = d.CidadeEm(p.x, p.z);
            if (t == null) return;
            int predios = 0;
            foreach (var l in t.Lotes)
            {
                if (predios >= 3) break;
                var pl = l.Predio;
                if (pl == null || pl.hospital) continue;
                var M = t.MatrizDaCasa(l);
                Vector3 meio = M.MultiplyPoint3x4(Vector3.zero);
                float dist = Vector2.Distance(new Vector2(meio.x, meio.z), new Vector2(p.x, p.z));
                if (dist < 30f || dist > 90f) continue;
                float sx = pl.espelhar ? -1f : 1f;
                // o lado do prédio virado para o jogador (no espaço do prédio, sem o espelho)
                Vector3 local = M.inverse.MultiplyPoint3x4(p);
                local.x *= sx;
                float hw = pl.W * 0.5f, hd = pl.D * 0.5f;
                bool frenteOuFundo = Mathf.Abs(local.z) / hd > Mathf.Abs(local.x) / hw;
                int achados = 0;
                for (int tentativa = 0; tentativa < 6 && achados < 2; tentativa++)
                {
                    Vector3 q;
                    if (frenteOuFundo) q = new Vector3(Random.Range(-hw + 1.5f, hw - 1.5f), pl.Cobertura, Mathf.Sign(local.z) * (hd - 0.6f));
                    else q = new Vector3(Mathf.Sign(local.x) * (hw - 0.6f), pl.Cobertura, Random.Range(-hd + 1.5f, hd - 1.5f));
                    var w = M.MultiplyPoint3x4(new Vector3(sx * q.x, q.y, q.z));
                    // a laje embaixo e nada em volta (a casinha da escada, a casa de máquinas)
                    if (!Physics.Raycast(w + Vector3.up * 1f, Vector3.down, out var hit, 2f, 1, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y - w.y) > 0.35f) continue;
                    w = hit.point;
                    if (Physics.CheckCapsule(w + Vector3.up * 0.45f, w + Vector3.up * 1.6f, 0.3f, 1, QueryTriggerInteraction.Ignore)) continue;
                    Vector3 paraEle = p - w;
                    paraEle.y = 0f;
                    // (de pé, por cima da mureta, ele vê onde o jogador está)
                    if (Physics.Linecast(w + Vector3.up * 1.6f, p + Vector3.up * 1.4f, (1 << 0) | (1 << 11), QueryTriggerInteraction.Ignore)) continue;
                    saida.Add(new Posto { pos = w, giro = Quaternion.LookRotation(paraEle).eulerAngles.y, telhado = true });
                    achados++;
                }
                if (achados > 0) predios++;
            }
        }

        // ------------------------------------------------------------------ o tiroteio e o fim

        // com um jogador perto de um deles, todos abrem fogo
        void Disparar(float agora)
        {
            var g = _atual.grupo;
            if (g == null) return;
            if (g.estado == EstadoDoGrupo.Combate)
            {
                _atual.disparada = true;
                _atual.ultimoCombate = agora;
                return;
            }
            if (_atual.disparada || g.nascendo > 0) return;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.AlvoValido || !j.Criado) continue;
                foreach (var m in g.membros)
                {
                    if (m == null || m.Morto) continue;
                    if ((m.transform.position - j.transform.position).sqrMagnitude > disparo * disparo) continue;
                    g.Ver(j, j.transform.position, m);
                    _atual.disparada = true;
                    _atual.ultimoCombate = agora;
                    Debug.Log("[Emboscadas] A emboscada começou.");
                    return;
                }
            }
        }

        void Conferir(float agora)
        {
            var g = _atual.grupo;
            if (g == null) { _atual = null; return; }
            if (g.nascendo == 0 && g.Vivos == 0) { Recolher(); return; }
            float perto = GrupoDeSobreviventes.PertoDeUmJogador(_atual.centro);
            bool lutando = g.estado == EstadoDoGrupo.Combate || agora - _atual.ultimoCombate < 20f;
            if (!_atual.disparada ? perto > 140f : !lutando && perto > 120f) Recolher();
        }

        void Recolher()
        {
            var e = _atual;
            if (e == null) return;
            _aAparecer.RemoveAll(x => x.e == e);
            foreach (var m in e.grupo.membros.ToArray())
            {
                if (m == null || m.Morto) continue;
                var no = m.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned) no.Despawn(true);
            }
            e.grupo.membros.Clear();
            _atual = null;
            Debug.Log("[Emboscadas] A emboscada saiu.");
        }

        /// <summary>No anfitrião (o F1): a emboscada de agora sai (os mortos ficam).</summary>
        public void TirarAsPessoas() => Recolher();

        /// <summary>Para o F1 e os testes: a emboscada de agora (null: nenhuma) e quantos vivos.</summary>
        public int VivosNaEmboscada => _atual?.grupo != null ? _atual.grupo.Vivos : -1;
        public bool EmboscadaDisparada => _atual != null && _atual.disparada;
    }
}
