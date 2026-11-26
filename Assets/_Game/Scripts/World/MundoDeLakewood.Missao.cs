using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A campanha na rede (25/11/2026, o caderno das missões; antes, 24/11, só a primeira missão depois dos 5 anos): a fase,
    /// a submissão da vez e três números para o caderno (as compras, os zumbis da rua e os de pedra, as peças que faltam...).
    /// O anfitrião decide (<see cref="LakewoodNaCena"/>, o DiaZero e o Missao) e só aceita a submissão da vez
    /// (<see cref="Concluir"/>): a pedido do Pedro, "o jogo deve impedir o jogador de pular missões". As submissões que
    /// acabam numa conversa vêm do cliente (<see cref="ConcluirPelaConversaRpc"/>). Vai no save (a fase e a submissão,
    /// <c>MundoSalvo.faseDaCampanha</c>); desde 25/11/2026 (o save do Dia 0), também no Dia 0 e na fuga, que voltam já no
    /// spawn (<see cref="TalvezComecarODiaZero"/>).
    /// </summary>
    public partial class MundoDeLakewood
    {
        readonly NetworkVariable<byte> _fase = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> _passo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> _numero1 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> _numero2 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> _numero3 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // (25/11/2026) a campanha começou pelo Dia 0 (as anotações da mochila mostram o Dia 0 e a fuga só então)
        readonly NetworkVariable<bool> _comDiaZero = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // (25/11/2026, a revisão: um save de antes do caderno vai direto para o Capitão, e as anotações mostravam o tutorial
        // do Gus inteiro riscado, sem ele ter acontecido) a fase do bunker começou neste mundo
        readonly NetworkVariable<bool> _comOBunker = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public FaseDaCampanha Fase => IsSpawned ? (FaseDaCampanha)_fase.Value : FaseDaCampanha.Nenhuma;
        public int Passo => IsSpawned ? _passo.Value : 0;
        public int Numero1 => IsSpawned ? _numero1.Value : 0;
        public int Numero2 => IsSpawned ? _numero2.Value : 0;
        public int Numero3 => IsSpawned ? _numero3.Value : 0;
        /// <summary>A campanha deste mundo começou pelo Dia 0 (vai no save: <c>MundoSalvo.campanhaComDiaZero</c>).</summary>
        public bool ComDiaZero => IsSpawned && _comDiaZero.Value;
        /// <summary>
        /// A fase do bunker (o tutorial do Gus) aconteceu neste mundo (vai no save: <c>MundoSalvo.campanhaComOBunker</c>). Falso
        /// num mundo de antes do caderno, que foi direto para o Capitão: as anotações da mochila não mostram essa fase.
        /// </summary>
        public bool ComOBunker => IsSpawned && _comOBunker.Value;

        /// <summary>A submissão da vez é esta.</summary>
        public bool Em(FaseDaCampanha f, int p) => Fase == f && Passo == p;

        /// <summary>
        /// No anfitrião: a primeira missão depois dos 5 anos (retomar a Woodcreek) já foi cumprida. Vai no save
        /// (<c>MundoSalvo.primeiraMissao</c>, de antes do caderno: um save antigo com ela pula para a campanha livre).
        /// </summary>
        public bool PrimeiraMissaoCumprida => Fase == FaseDaCampanha.Livre;

        /// <summary>No anfitrião: a fase começa, na primeira submissão pronta (sem nenhuma, a fase seguinte).</summary>
        public void ComecarAFase(FaseDaCampanha f)
        {
            if (!IsServer) return;
            if (f == FaseDaCampanha.DiaZero) _comDiaZero.Value = true;
            if (f != FaseDaCampanha.Livre && f != FaseDaCampanha.Nenhuma && CampanhaDeLakewood.PrimeiraPronta(f) < 0) f = CampanhaDeLakewood.Seguinte(f);
            // (25/11/2026, a revisão) toda entrada no tutorial do Gus passa por aqui: o jogo novo, o fim do Dia 0 e da fuga, o save
            if (f == FaseDaCampanha.OBunker && !_comOBunker.Value) _comOBunker.Value = true;
            int p = f == FaseDaCampanha.Livre || f == FaseDaCampanha.Nenhuma ? 0 : Mathf.Max(0, CampanhaDeLakewood.PrimeiraPronta(f));
            if (_fase.Value == (byte)f && _passo.Value == p) return;
            _fase.Value = (byte)f;
            _passo.Value = (byte)p;
            Numeros(0, 0, 0);
            Debug.Log($"[Campanha] A fase {f} começou: \"{CampanhaDeLakewood.Texto(f, p, this, false)}\"");
        }

        /// <summary>
        /// No anfitrião: a submissão foi feita. Só conta se ela é a da vez (as feitas fora de hora não pulam nada); a seguinte
        /// pronta vira a da vez, e no fim da fase começa a seguinte.
        /// </summary>
        public bool Concluir(FaseDaCampanha f, int p)
        {
            if (!IsServer || !Em(f, p)) return false;
            int proximo = CampanhaDeLakewood.PrimeiraPronta(f, p + 1);
            Debug.Log($"[Campanha] Feito: \"{CampanhaDeLakewood.Texto(f, p, this, false)}\" ({f}, {p}).");
            if (proximo >= 0)
            {
                _passo.Value = (byte)proximo;
                Numeros(0, 0, 0);
                Debug.Log($"[Campanha] Agora: \"{CampanhaDeLakewood.Texto(f, proximo, this, false)}\".");
            }
            else ComecarAFase(CampanhaDeLakewood.Seguinte(f));
            return true;
        }

        /// <summary>No anfitrião: os números do caderno (o que cada submissão mostra).</summary>
        public void Numeros(int a, int b, int c)
        {
            if (!IsServer) return;
            byte x = (byte)Mathf.Clamp(a, 0, 255), y = (byte)Mathf.Clamp(b, 0, 255), z = (byte)Mathf.Clamp(c, 0, 255);
            if (_numero1.Value != x) _numero1.Value = x;
            if (_numero2.Value != y) _numero2.Value = y;
            if (_numero3.Value != z) _numero3.Value = z;
        }

        /// <summary>
        /// Qualquer um (a conversa com o Gus ou com o Capitão): a submissão da vez que acaba numa conversa
        /// (<see cref="CampanhaDeLakewood.PelaConversa"/>). O anfitrião confere que ela é mesmo a da vez.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ConcluirPelaConversaRpc(byte fase, byte passo)
        {
            var f = (FaseDaCampanha)fase;
            if (!CampanhaDeLakewood.PelaConversa(f, passo)) return;
            Concluir(f, passo);
        }

        /// <summary>No anfitrião (o save): a fase e a submissão de agora.</summary>
        public void CampanhaParaSalvar(out int fase, out int passo)
        {
            fase = _fase.Value;
            passo = _passo.Value;
        }

        /// <summary>
        /// No anfitrião (o save, ao carregar): a fase e a submissão salvas. Um save de antes do caderno (fase -1) volta pela
        /// primeira missão: cumprida, a campanha livre; senão, a conversa com o Capitão. Nos dois, sem o tutorial do Gus
        /// (<see cref="ComOBunker"/> falso).
        /// </summary>
        public void RestaurarACampanha(int fase, int passo, bool primeiraMissao, bool comDiaZero = false, bool comOBunker = false)
        {
            if (!IsServer) return;
            // (25/11/2026, o save do Dia 0) o save do Dia 0 ou da fuga já voltou no spawn, na fase e na submissão dele
            // (TalvezComecarODiaZero): aqui, só a marca do bunker (o tutorial do Gus ainda vem depois da fuga, a menos que um
            // Dia 0 do F1 tenha sido salvo num mundo que já passou por ele)
            if (Retomada)
            {
                _comDiaZero.Value = true;
                _comOBunker.Value = comOBunker;
                Debug.Log($"[Campanha] Do save: {Fase}, submissão {Passo} (o Dia 0 ou a fuga, retomados no spawn).");
                return;
            }
            _comDiaZero.Value = comDiaZero;
            // (25/11/2026, a revisão) a marca do bunker veio no save; num save sem ela (de antes, ou gravado hoje antes da
            // revisão), o Dia 0 diz que houve: depois dele, o jogo sempre passa pelo tutorial (o AcabarODiaZero). O save no
            // meio do tutorial liga a marca no ComecarAFase, logo abaixo. Um save de antes do caderno (fase -1) fica sem ela.
            bool doCaderno = fase >= 0 && fase <= (int)FaseDaCampanha.Livre;
            _comOBunker.Value = doCaderno && (comOBunker || comDiaZero);
            if (!doCaderno)
            {
                ComecarAFase(primeiraMissao ? FaseDaCampanha.Livre : FaseDaCampanha.RetomeAWoodcreek);
                return;
            }
            var f = (FaseDaCampanha)fase;
            // (o Dia 0 e a fuga salvos voltam no spawn, com a Retomada, acima; a fase Nenhuma, e qualquer save que chegue aqui
            // com eles sem ter sido retomado, vão para depois deles, como antes de 25/11/2026)
            if (f == FaseDaCampanha.DiaZero || f == FaseDaCampanha.Fuga || f == FaseDaCampanha.Nenhuma) f = FaseDaCampanha.OBunker;
            ComecarAFase(f);
            if (Fase == f && passo > 0 && passo < CampanhaDeLakewood.Passos(f) && CampanhaDeLakewood.Pronta(f, passo)) _passo.Value = (byte)passo;
            Debug.Log($"[Campanha] Do save: {Fase}, submissão {Passo}.");
        }

        /// <summary>Qualquer um (a conversa com o Capitão): ele vem junto (true) ou espera onde está (false).</summary>
        [Rpc(SendTo.Server)]
        public void EscoltaRpc(bool vem, RpcParams parametros = default)
        {
            var lw = LakewoodNaCena.Instancia;
            Player.JogadorRede quem = null;
            if (NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) && cliente.PlayerObject != null)
                quem = cliente.PlayerObject.GetComponent<Player.JogadorRede>();
            if (lw != null) lw.MudarAEscolta(vem, quem);
        }

        /// <summary>
        /// Quem conversa com o Capitão (25/11/2026, a pedido: "enquanto alguns npcs andam e eu converso com eles, para eles,
        /// eles ainda estão andando"): a tela abriu (true) ou fechou. O anfitrião o segura parado, olhando para quem conversa.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ConversaComOCapitaoRpc(bool comecou, RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(quem, out var cliente) || cliente.PlayerObject == null) return;
            var lw = LakewoodNaCena.Instancia;
            if (lw != null) lw.ConversaComOCapitao(cliente.PlayerObject.GetComponent<Player.JogadorRede>(), comecou);
        }
    }
}
