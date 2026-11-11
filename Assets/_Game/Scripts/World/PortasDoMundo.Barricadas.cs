using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using A = ZombieCityCleaner.Player.ArmadilhasDoJogador;
using Habilidade = ZombieCityCleaner.Player.Habilidade;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A barricada de tábuas nas portas e nas janelas das casas (11/11/2026, a etapa 5b da árvore nova: o nó t2; a construção
    /// é do <see cref="ConstrucoesNoMundo"/>, no Loot/ConstrucoesNoMundo.Barricadas.cs). Com as tábuas na mochila e o nó,
    /// mirando uma porta (<see cref="PortaDaCasa"/>) ou uma janela (<see cref="JanelaDaCasa"/>), segurar o E prega a barricada
    /// do lado de quem prega (o toque no E continua abrindo e fechando); o anfitrião acha o vão pela chave (o registro das
    /// casas), põe a barricada na face da parede e fecha a porta ou a janela. Enquanto ela existe, o E não abre (dos dois
    /// lados: a folha treme, e a dica diz "Barricada"), ninguém arromba, e o <see cref="PuloDaJanela"/> não acha a abertura
    /// (a janela fica fechada, e as tábuas tapam o vão).
    /// </summary>
    public partial class PortasDoMundo
    {
        /// <summary>A porta ou a janela da chave tem uma barricada de tábuas (em qualquer cópia: a lista das construções).</summary>
        public static bool Barricada(ulong chave) => ConstrucoesNoMundo.Instancia != null && ConstrucoesNoMundo.Instancia.BarricadaDoVao(chave).HasValue;

        // do meio da parede à face (a grossura das paredes das casas, Medidas.DoJogo.esp, é 0,2 m)
        const float MeiaParede = 0.1f;

        /// <summary>O jogador local pode pregar a barricada neste vão (o nó, as tábuas na mochila, ainda sem barricada).</summary>
        internal static bool PodePregar(ulong chave, ArmaJogador quem) =>
            quem != null && quem.Inventario != null && quem.Inventario.Quantos(Item.TabuasBarricada) > 0
            && Habilidades.Tem(quem.GetComponent<JogadorRede>(), Habilidade.BarricadaDeTabuas) && !Barricada(chave);

        // o pedaço da dica para pregar ("", sem as tábuas ou o nó)
        internal static string DicaDePregar(ulong chave, ArmaJogador quem) => PodePregar(chave, quem) ? "   ·   [segure E] Barricar com as tábuas" : "";

        /// <summary>A dica da porta ou da janela barricada (null: sem barricada).</summary>
        internal static string DicaDaBarricada(ulong chave, ArmaJogador quem, bool janela)
        {
            var c = ConstrucoesNoMundo.Instancia != null ? ConstrucoesNoMundo.Instancia.BarricadaDoVao(chave) : null;
            if (!c.HasValue) return null;
            string o = janela ? "Janela barricada" : "Porta barricada";
            float pct = c.Value.valor / ConstrucoesNoMundo.MaximoDe(c.Value) * 100f;
            return ConstrucoesNoMundo.DoLadoDela(c.Value, quem.transform.position)
                ? $"{o} ({Mathf.CeilToInt(pct)}%): não abre"
                : $"{o} do outro lado: não abre ({Mathf.CeilToInt(pct)}%)";
        }

        // o que segurar o E faz na porta: arrombar (a trancada sabida, com a ferramenta) ou, senão, pregar
        bool PregandoNa(PortaDaCasa p, ArmaJogador quem) => TempoDeArrombar(p, quem) <= 0f && PodePregar(p.chave, quem);
        bool PregandoNa(JanelaDaCasa j, ArmaJogador quem) => TempoDeArrombar(j, quem) <= 0f && PodePregar(j.chave, quem);

        internal float TempoDoSegurar(PortaDaCasa p, ArmaJogador quem) => PregandoNa(p, quem) ? A.SegundosDePregar : TempoDeArrombar(p, quem);
        internal float TempoDoSegurar(JanelaDaCasa j, ArmaJogador quem) => PregandoNa(j, quem) ? A.SegundosDePregar : TempoDeArrombar(j, quem);
        internal bool ToqueUsa(PortaDaCasa p, ArmaJogador quem) => PregandoNa(p, quem);
        internal bool ToqueUsa(JanelaDaCasa j, ArmaJogador quem) => PregandoNa(j, quem);
        internal string TextoDoSegurar(PortaDaCasa p, ArmaJogador quem) => PregandoNa(p, quem) ? "Pregando as tábuas" : TextoDoArrombar(p, quem);
        internal string TextoDoSegurar(JanelaDaCasa j, ArmaJogador quem) => PregandoNa(j, quem) ? "Pregando as tábuas" : TextoDoArrombar(j, quem);

        internal void Segurado(PortaDaCasa p, ArmaJogador quem)
        {
            if (PregandoNa(p, quem)) PregarRpc(p.chave);
            else PedirArrombar(p, quem);
        }

        internal void Segurado(JanelaDaCasa j, ArmaJogador quem)
        {
            if (PregandoNa(j, quem)) PregarRpc(j.chave);
            else PedirArrombar(j, quem);
        }

        /// <summary>Quem joga → anfitrião: prega a barricada de tábuas na porta ou na janela da chave, do lado dele.</summary>
        [Rpc(SendTo.Server)]
        void PregarRpc(ulong chave, RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(quem, out var cliente) || cliente.PlayerObject == null) return;
            Pregar(cliente.PlayerObject.GetComponent<InventarioJogador>(), quem, chave, false);
        }

        /// <summary>
        /// Só no anfitrião: prega a barricada de tábuas do jogador no vão da chave (a porta ou a janela), do lado em que ele está
        /// (a 3,5 m, no máximo). <paramref name="deGraca"/>: o teste (sem as tábuas nem o nó). Devolve se pregou.
        /// </summary>
        public bool Pregar(InventarioJogador inventario, ulong cliente, ulong chave, bool deGraca)
        {
            var construcoes = ConstrucoesNoMundo.Instancia;
            if (!IsServer || inventario == null || construcoes == null || Barricada(chave)) return false;
            bool janela = !_registro.TryGetValue(chave, out var r);
            if (janela && !_registroDasJanelas.TryGetValue(chave, out r) || r.c == null) return false;
            var pes = inventario.transform.position;
            var meio = janela ? MeioDaJanela(r.c, r.i) : MeioDa(r.c, r.i);
            if ((meio - (pes + Vector3.up)).sqrMagnitude > distanciaMaxima * distanciaMaxima) return false;
            Vector3 pos;
            float giro, largura, altura;
            if (janela) PoseNaJanela(r.c, r.i, pes, out pos, out giro, out largura, out altura);
            else PoseNaPorta(r.c, r.i, pes, out pos, out giro, out largura, out altura);
            if (!construcoes.PregarNoVao(inventario, cliente, chave, janela, pos, giro, largura, altura, deGraca)) return false;
            // a porta ou a janela fecha na hora (a tranca fica como estava)
            EscreverChave(chave, false, false);
            return true;
        }

        /// <summary>Só no anfitrião: a porta ou a janela da barricada (o save voltando) fica fechada.</summary>
        public void FecharPelaBarricada(ulong chave)
        {
            if (!IsSpawned || !IsServer || chave == 0) return;
            if (_estado.TryGetValue(chave, out byte bits) && (bits & BitAberta) == 0) return;
            EscreverChave(chave, false, false);
        }

        /// <summary>
        /// A pose da barricada de tábuas numa porta: no meio do vão, no chão, na face da parede do lado de <paramref name="quem"/>
        /// (o +Z para ele). Do lado para onde a folha abre, a folha fechada fica na face (metade para fora): as tábuas, 3,5 cm
        /// mais longe. O vão: a folha mais 3 cm de cada lado e em cima (o Obra.FolhaDePorta).
        /// </summary>
        public static void PoseNaPorta(PortasDaConstrucao c, int i, Vector3 quem, out Vector3 pos, out float giro, out float largura, out float altura)
        {
            var d = c.portas[i];
            var abre = new Vector3(d.abre.x, 0f, d.abre.y).normalized;
            var ao = new Vector3(d.fechada.x, 0f, d.fechada.y).normalized;
            // o meio da parede, no meio do vão, no chão (a dobradiça fica na face do lado que abre, 1 cm acima do chão)
            var meio = d.dobradica + ao * (d.largura / 2f) - abre * MeiaParede + Vector3.down * 0.01f;
            var q = c.transform.InverseTransformPoint(quem);
            float lado = Vector3.Dot(q - meio, abre) >= 0f ? 1f : -1f;
            var n = abre * lado;
            pos = c.transform.TransformPoint(meio + n * (MeiaParede + (lado > 0f ? 0.035f : 0.004f)));
            var nm = c.transform.TransformDirection(n);
            giro = Mathf.Atan2(nm.x, nm.z) * Mathf.Rad2Deg;
            largura = d.largura + 0.06f;
            altura = d.altura + 0.06f;
        }

        /// <summary>A pose da barricada de tábuas numa janela: no meio do peitoril, na face da parede do lado de <paramref name="quem"/>.</summary>
        public static void PoseNaJanela(PortasDaConstrucao c, int i, Vector3 quem, out Vector3 pos, out float giro, out float largura, out float altura)
        {
            var j = c.janelas[i];
            var fora = new Vector3(j.fora.x, 0f, j.fora.y).normalized;
            var q = c.transform.InverseTransformPoint(quem);
            float lado = Vector3.Dot(q - j.peitoril, fora) >= 0f ? 1f : -1f;
            var n = fora * lado;
            float meia = j.parede > 0.01f ? j.parede / 2f : MeiaParede;
            pos = c.transform.TransformPoint(j.peitoril + n * (meia + 0.004f));
            var nm = c.transform.TransformDirection(n);
            giro = Mathf.Atan2(nm.x, nm.z) * Mathf.Rad2Deg;
            largura = j.largura;
            altura = j.altura;
        }

        // ------------------------------------------------------------ o teste (o F1 e o MCP)

        /// <summary>
        /// Só no anfitrião (o teste): a porta (ou a janela) viva mais perto do jogador, a até <paramref name="raio"/> m: a chave,
        /// o meio e se é a da frente. Devolve 0 sem nenhuma.
        /// </summary>
        public ulong MaisPerto(Vector3 ponto, float raio, bool janela, out Vector3 meio)
        {
            meio = Vector3.zero;
            ulong melhor = 0;
            float menor = raio * raio;
            if (!janela)
                foreach (var p in _vivas.Values)
                {
                    if (p == null) continue;
                    float d2 = (p.Meio - ponto).sqrMagnitude;
                    if (d2 >= menor) continue;
                    menor = d2;
                    melhor = p.chave;
                    meio = p.Meio;
                }
            else
                foreach (var j in _janelasVivas.Values)
                {
                    if (j == null) continue;
                    float d2 = (j.Meio - ponto).sqrMagnitude;
                    if (d2 >= menor) continue;
                    menor = d2;
                    melhor = j.chave;
                    meio = j.Meio;
                }
            return melhor;
        }

        /// <summary>A folha viva da porta ou da janela da chave (null: longe ou não existe), para o teste.</summary>
        public PortaDaCasa PortaViva(ulong chave) => _vivas.TryGetValue(chave, out var p) ? p : null;
        public JanelaDaCasa JanelaViva(ulong chave) => _janelasVivas.TryGetValue(chave, out var j) ? j : null;

        /// <summary>Só no anfitrião (o teste): a porta ou a janela da chave está aberta agora (o estado da rede).</summary>
        public bool AbertaAgora(ulong chave)
        {
            if (_registro.TryGetValue(chave, out var r) && r.c != null) return AbertaAgora(r.c, r.i);
            if (_registroDasJanelas.TryGetValue(chave, out r) && r.c != null) return JanelaAbertaAgora(r.c, r.i);
            return false;
        }

        /// <summary>Só no anfitrião (o teste): o E de um jogador na porta ou na janela da chave, como se ele apertasse.</summary>
        public void UsarDeTeste(ulong chave, Vector3 pes)
        {
            if (_registro.TryGetValue(chave, out var r) && r.c != null) Decidir(r.c, r.i, pes, true);
            else if (_registroDasJanelas.TryGetValue(chave, out r) && r.c != null) DecidirJanela(r.c, r.i, pes, true);
        }

        // o jogador local segurando o E para pregar: o martelo repete no vão (o som do fim, e o barulho, vêm do anfitrião)
        bool SomDoMartelo(Interagivel alvo, ArmaJogador arma, Vector3 onde)
        {
            bool pregando = alvo is PortaDaCasa p ? PregandoNa(p, arma) : alvo is JanelaDaCasa j && PregandoNa(j, arma);
            if (!pregando) return false;
            if (Time.time < _proximoSom) return true;
            Efeitos.Som(SomDasArmadilhas.Martelo, onde, 0.8f, true, 25f);
            _proximoSom = Time.time + 1.05f;
            return true;
        }
    }
}
