using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Arrombar as portas trancadas (o Plano_Portas.md da pasta de arte, a parte 4): com a porta que se sabe trancada (tremeu)
    /// e a ferramenta na mochila, segurar o E (<see cref="Interagivel.Segurar"/>) arromba:
    ///  - a gazua: <see cref="TempoDaGazua"/> s, sem barulho para os zumbis; uma em cada quatro quebra (some da pilha);
    ///  - o pé de cabra: <see cref="TempoDoPeDeCabra"/> s, com o barulho que chama os zumbis até <see cref="RaioDoBarulho"/> m.
    /// Com as duas, a gazua (o silêncio). O anfitrião confere (a distância, a ferramenta e a tranca), destranca para sempre e
    /// abre; todos ouvem o fim (<see cref="ArrombouRpc"/>). Enquanto o jogador local segura, o som da ferramenta repete na porta.
    /// </summary>
    public partial class PortasDoMundo
    {
        public const float TempoDaGazua = 4f, TempoDoPeDeCabra = 2f, ChanceDeQuebrarAGazua = 0.25f, RaioDoBarulho = 25f;
        /// <summary>O grampo do Chaveiro (06/11/2026): abre sem ferramenta, sem barulho e sem quebrar, em 6 s.</summary>
        public const float TempoDoGrampo = 6f;
        /// <summary>O "grampo" do Chaveiro: não é um item (não se gasta nem se carrega), só a ferramenta da conta.</summary>
        const Item Grampo = (Item)254;

        static JogadorRede DonoDe(InventarioJogador inv) => inv != null ? inv.GetComponent<JogadorRede>() : null;

        static Item FerramentaDe(InventarioJogador inv) =>
            inv == null ? Item.Nada : inv.Quantos(Item.Gazua) > 0 ? Item.Gazua : inv.Quantos(Item.PeDeCabra) > 0 ? Item.PeDeCabra
            : Habilidades.Tem(DonoDe(inv), Habilidade.Chaveiro) ? Grampo : Item.Nada;

        // (as Mãos leves, 06/11/2026: 15% mais rápido por ponto)
        static float TempoDa(Item ferramenta, InventarioJogador inv = null)
        {
            float t = ferramenta == Item.Gazua ? TempoDaGazua : ferramenta == Item.PeDeCabra ? TempoDoPeDeCabra : ferramenta == Grampo ? TempoDoGrampo : 0f;
            t *= 1f - 0.15f * Habilidades.Pontos(DonoDe(inv), Habilidade.MaosLeves);
            return t;
        }

        /// <summary>A dica da porta que o jogador sabe trancada (e o que ele tem para arrombar).</summary>
        internal string DicaDaTrancada(PortaDaCasa p, ArmaJogador quem) => DicaDaTrancada(FerramentaDe(quem.Inventario));

        /// <summary>A dica do que se sabe trancado (a porta, a janela), com a ferramenta que o jogador tem.</summary>
        internal static string DicaDaTrancada(Item ferramenta) => ferramenta switch
        {
            Item.Gazua => "Trancada · segure [E]: arrombar com a gazua (sem barulho)",
            Item.PeDeCabra => "Trancada · segure [E]: arrombar com o pé de cabra (faz barulho)",
            Grampo => "Trancada · segure [E]: abrir com um grampo (o Chaveiro, sem barulho)",
            _ => "Trancada · uma gazua ou um pé de cabra arromba",
        };

        /// <summary>Quantos segundos o E se segura para arrombar (0: não dá, sem a ferramenta ou sem saber que está trancada).</summary>
        internal float TempoDeArrombar(PortaDaCasa p, ArmaJogador quem)
        {
            if (p.Aberta || !Trancada(p) || !SabidaTrancada(p.chave)) return 0f;
            if (p.dados.DeFora && p.DoLadoQueAbre(quem.transform.position)) return 0f;   // por dentro, destranca sem ferramenta
            return TempoDa(FerramentaDe(quem.Inventario), quem.Inventario);
        }

        internal static string TextoDoArrombar(Item ferramenta) =>
            ferramenta == Item.Gazua ? "Arrombando com a gazua" : ferramenta == Item.PeDeCabra ? "Arrombando com o pé de cabra"
            : ferramenta == Grampo ? "Abrindo com um grampo" : null;

        internal string TextoDoArrombar(PortaDaCasa p, ArmaJogador quem) => TextoDoArrombar(FerramentaDe(quem.Inventario));

        /// <summary>O jogador local segurou o E o tempo de arrombar a porta.</summary>
        internal void PedirArrombar(PortaDaCasa p, ArmaJogador quem) => PedirArrombar(p.chave, p.Meio, quem);

        /// <summary>O jogador local segurou o E o tempo de arrombar a janela.</summary>
        internal void PedirArrombar(JanelaDaCasa j, ArmaJogador quem) => PedirArrombar(j.chave, j.Meio, quem);

        void PedirArrombar(ulong chave, Vector3 onde, ArmaJogador quem)
        {
            var f = FerramentaDe(quem.Inventario);
            if (f == Item.Nada) return;
            if (!IsSpawned)
            {
                EscreverChave(chave, true, true);
                Efeitos.Som(f != Item.PeDeCabra ? SomDasPortas.Gazua : SomDasPortas.PeDeCabra, onde, 1f, true, 30f);
                return;
            }
            ArrombarRpc(chave, (byte)f);
        }

        /// <summary>Quem joga → anfitrião: arromba a porta com a ferramenta.</summary>
        [Rpc(SendTo.Server)]
        void ArrombarRpc(ulong chave, byte ferramenta, RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(quem, out var cliente) || cliente.PlayerObject == null) return;
            bool janela = !_registro.TryGetValue(chave, out var r);
            if (janela && !_registroDasJanelas.TryGetValue(chave, out r) || r.c == null) return;
            var f = (Item)ferramenta;
            var inv = cliente.PlayerObject.GetComponent<InventarioJogador>();
            var pes = cliente.PlayerObject.transform.position;
            var jogador = cliente.PlayerObject.GetComponent<JogadorRede>();
            if (inv == null || (f != Item.Gazua && f != Item.PeDeCabra && f != Grampo)) return;
            if (f == Grampo ? !Habilidades.Tem(jogador, Habilidade.Chaveiro) : inv.Quantos(f) <= 0) return;
            bool maosLeves = Habilidades.Pontos(jogador, Habilidade.MaosLeves) >= 2;   // (o 2º ponto: o pé de cabra quieto e a gazua que não quebra)
            var meio = janela ? MeioDaJanela(r.c, r.i) : MeioDa(r.c, r.i);
            if ((meio - (pes + Vector3.up)).sqrMagnitude > distanciaMaxima * distanciaMaxima) return;
            if (janela ? JanelaAbertaAgora(r.c, r.i) || !JanelaTrancadaAgora(r.c, r.i) : AbertaAgora(r.c, r.i) || !TrancadaAgora(r.c, r.i)) return;
            bool quebrou = f == Item.Gazua && !maosLeves && Random.value < ChanceDeQuebrarAGazua;
            if (quebrou)
            {
                inv.GastarItem(Item.Gazua, 1);
                inv.AvisarDono("A gazua quebrou na fechadura");
            }
            EscreverChave(chave, true, true);
            ArrombouRpc(chave, ferramenta);
            // o pé de cabra arrebentando a fechadura chama os zumbis de perto
            // (as Mãos leves, 06/11/2026: em silêncio)
            if (f == Item.PeDeCabra && !maosLeves) Zombies.ZombieManager.Barulho(meio, jogador, RaioDoBarulho);
        }

        /// <summary>Anfitrião → todos: a porta foi arrombada (o som do fim).</summary>
        [Rpc(SendTo.Everyone)]
        void ArrombouRpc(ulong chave, byte ferramenta)
        {
            _sabidas.Remove(chave);
            Vector3? onde = _vivas.TryGetValue(chave, out var p) && p != null ? p.Meio
                          : _janelasVivas.TryGetValue(chave, out var j) && j != null ? j.Meio : (Vector3?)null;
            if (onde.HasValue)
                Efeitos.Som((Item)ferramenta != Item.PeDeCabra ? SomDasPortas.Gazua : SomDasPortas.PeDeCabra, onde.Value, 1f, true, (Item)ferramenta != Item.PeDeCabra ? 12f : 35f);
        }

        // o jogador local segurando o E numa porta trancada: o som da ferramenta repete nela
        Interagivel _arrombando;
        float _proximoSom;

        void AtualizarArrombar()
        {
            var arma = JogadorRede.Local != null ? JogadorRede.Local.GetComponent<ArmaJogador>() : null;
            var alvo = arma != null ? arma.SegurandoAgora : null;
            if (alvo == null || (!(alvo is PortaDaCasa) && !(alvo is JanelaDaCasa)))
            {
                _arrombando = null;
                return;
            }
            var f = FerramentaDe(arma.Inventario);
            if (alvo != _arrombando)
            {
                _arrombando = alvo;
                _proximoSom = 0f;
            }
            if (Time.time < _proximoSom || f == Item.Nada) return;
            Vector3 onde = alvo is PortaDaCasa p ? p.Meio : ((JanelaDaCasa)alvo).Meio;
            // a gazua: os cliques dos pinos; o pé de cabra: a madeira forçando (o estalo do fim vem do anfitrião)
            bool silencio = f != Item.PeDeCabra;
            Efeitos.Som(silencio ? SomDasPortas.Gazua : SomDasPortas.Forcando, onde, silencio ? 0.7f : 0.8f, true, silencio ? 10f : 25f);
            _proximoSom = Time.time + (silencio ? 1.45f : 0.7f);
        }
    }
}
