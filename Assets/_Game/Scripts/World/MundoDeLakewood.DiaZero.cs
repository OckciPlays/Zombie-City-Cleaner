using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O Dia 0 no Lakewood (22/11/2026, a etapa 4 do Docs/23-Campanha-e-Mapa.md): o jogo novo começa na casa da família, na
    /// Woodcreek de antes (<see cref="EpocaDoMundo.Antes"/>), com o jornal na TV e as compras; a porta de casa fecha o dia e o
    /// jogo pula para os 5 anos depois, no bunker da comunidade. Aqui, só a marca da rede (o anfitrião escreve); a casa, as
    /// lojas, as pessoas e os carros são do palco (<see cref="LakewoodNaCena"/>, o DiaZero.WoodcreekDoDiaZero), e o lobby,
    /// a carteira, o caixa e a passagem, do DiaZero.DiaZeroDaPartida (o mesmo do mundo sorteado).
    /// <para>
    /// Desde 25/11/2026 (o save do Dia 0, a pedido do Pedro: "Sim, salvar o Dia 0"), o Dia 0 e a fuga gravam, e o Continuar
    /// volta neles já no spawn (<see cref="TalvezComecarODiaZero"/>): a época de antes sai da fase salva da campanha (o Dia 0
    /// ou a fuga), e o palco entra antes de o jogador nascer, como num jogo novo.
    /// </para>
    /// </summary>
    public partial class MundoDeLakewood
    {
        /// <summary>
        /// O jogo novo começa pelo Dia 0 (a escolha do menu do novo jogo; o anfitrião lê ao abrir a partida). Um jogo salvo
        /// volta pela fase dele: no Dia 0 ou na fuga, se foi salvo neles (25/11/2026); senão, depois deles.
        /// </summary>
        public static bool ComecarPeloDiaZero = true;

        /// <summary>
        /// Só no anfitrião (25/11/2026, o save do Dia 0): o Dia 0 ou a fuga de agora voltou de um save (o Continuar). O
        /// DiaZeroDaPartida e o LakewoodNaCena devolvem então o que é só do dia (as carteiras, as compras, a chave, o que não
        /// coube no armazém) pelo <see cref="Salvar.Salvamento.Atual"/>. Volta a falso quando o Dia 0 ou a fuga começam de
        /// novo nesta partida (o F1, ou as chaves da casa num Dia 0 retomado) e a cada partida.
        /// </summary>
        public bool Retomada { get; private set; }

        /// <summary>Quanto depois da porta a época muda (s): a tela dos 5 anos já cobriu tudo (ela escurece em 1,2 s).</summary>
        const float EpocaDepoisDaPorta = 1.8f;

        readonly NetworkVariable<bool> _diaZero = new NetworkVariable<bool>();
        float _depoisEm = -1f;

        /// <summary>O Dia 0 está acontecendo no Lakewood.</summary>
        public bool NoDiaZero => IsSpawned && _diaZero.Value;

        // no anfitrião, ao abrir a partida: um jogo novo (sem o estado salvo) com o Dia 0 pedido começa nele. Um save do Dia 0
        // ou da fuga (25/11/2026, o save do Dia 0) volta neles já aqui, antes de o jogador nascer (o palco entra logo abaixo,
        // no OnNetworkSpawn, e põe os nascimentos na sala da casa); os outros saves, pelo Salvamento.Restaurar, como antes
        void TalvezComecarODiaZero()
        {
            Retomada = false;
            ZerarAPartidaDeAntes();
            var salvo = Salvar.Salvamento.Atual;
            bool novo = !Salvar.Salvamento.VeioDoSave;
            if (!novo)
            {
                if (salvo.NoDiaZero) RetomarODiaZero(salvo.passoDaCampanha);
                else if (salvo.NaFuga) RetomarAFuga(salvo.fugaGuardouOArmazem);
                return;
            }
            if (ComecarPeloDiaZero) ComecarODiaZero();
            // (25/11/2026, o caderno das missões) o jogo novo sem o Dia 0 começa no bunker, 5 anos depois
            else ComecarAFase(FaseDaCampanha.OBunker);
        }

        // (26/11/2026, a revisão do save do Dia 0) no anfitrião, ao abrir a partida, antes de o jogo novo ou o save decidirem:
        // o "Mundo (Lakewood)" é um objeto da cena, a cena não recarrega entre uma partida e outra, e o Netcode não zera as
        // variáveis da rede dele no fim da partida (ficam com o último valor). Quem saía no Dia 0 ou na fuga e, sem fechar o
        // jogo, abria outro save pelo Continuar (ou um jogo novo) entrava com o Dia 0, a fuga e a época de antes ainda ligados:
        // um save do bunker abria no Dia 0; um save do Dia 0 de outro mundo abria com a fuga junto (os zumbis, e o armazém
        // guardando tudo de cada um); um jogo novo ficava na submissão do Dia 0 de antes (o ComecarODiaZero não faz nada com o
        // Dia 0 já ligado), e a rua saía liberada sem a TV. Aqui tudo volta ao começo, como num jogo aberto agora. (26/11/2026,
        // a integração do save do Dia 0) O número da tentativa também: a tela da fuga de cada máquina (FugaNaTela) zera a
        // tentativa vista fora da rede, e o caderno mostrava "tentativa 4" na primeira fuga de uma partida nova
        void ZerarAPartidaDeAntes()
        {
            if (!IsServer) return;
            _diaZero.Value = false;
            _fuga.Value = false;
            _falhou.Value = false;
            _chegou.Value = false;
            _tentativa.Value = 0;
            _depoisEm = _recomecarEm = _acabarEm = -1f;
            _retomandoAFuga = false;
            _primeiraTentativa = -1;
            PorAEpoca(EpocaDoMundo.Depois);
            _fase.Value = (byte)FaseDaCampanha.Nenhuma;
            _passo.Value = 0;
            Numeros(0, 0, 0);
            _comDiaZero.Value = false;
            _comOBunker.Value = false;
        }

        /// <summary>
        /// No anfitrião (25/11/2026, o save do Dia 0: o Continuar num save do Dia 0): a Woodcreek de antes e o Dia 0 de novo, na
        /// submissão salva (o <see cref="ComecarAFase"/> não pode zerá-la). O DiaZeroDaPartida começa o dia como num jogo novo
        /// e, com a <see cref="Retomada"/>, devolve as carteiras e as compras de cada um; o lobby pula a TV se o jornal já
        /// tinha acabado.
        /// </summary>
        void RetomarODiaZero(int passo)
        {
            if (!IsServer) return;
            _depoisEm = -1f;
            PorAEpoca(EpocaDoMundo.Antes);
            _diaZero.Value = true;
            ComecarAFase(FaseDaCampanha.DiaZero);   // (liga o _comDiaZero; a submissão salva vem logo abaixo)
            int p = Mathf.Clamp(passo, 0, CampanhaDeLakewood.Passos(FaseDaCampanha.DiaZero) - 1);
            if (Fase == FaseDaCampanha.DiaZero && CampanhaDeLakewood.Pronta(FaseDaCampanha.DiaZero, p)) _passo.Value = (byte)p;
            Retomada = true;
            Debug.Log($"[Dia 0] O Dia 0 voltou do save, na submissão {Passo}: \"{CampanhaDeLakewood.Texto(FaseDaCampanha.DiaZero, Passo, this, false)}\".");
        }

        /// <summary>No anfitrião (o jogo novo, ou o F1): a Woodcreek volta para antes e o Dia 0 começa.</summary>
        public void ComecarODiaZero()
        {
            if (!IsServer || _diaZero.Value) return;
            Retomada = false;   // (25/11/2026: um Dia 0 novo não herda o do save)
            _retomandoAFuga = false;
            _depoisEm = -1f;
            PorAEpoca(EpocaDoMundo.Antes);
            _diaZero.Value = true;
            ComecarAFase(FaseDaCampanha.DiaZero);   // (25/11/2026: o caderno das missões, MundoDeLakewood.Missao.cs)
            Debug.Log("[Dia 0] O Dia 0 começou na Woodcreek (a época de antes).");
        }

        /// <summary>
        /// No anfitrião (a porta de casa fechou, DiaZeroDaPartida): o Dia 0 acaba agora, e a época volta para depois logo em
        /// seguida, com a tela dos 5 anos já preta.
        /// </summary>
        public void AcabarODiaZero()
        {
            if (!IsServer || !_diaZero.Value) return;
            _diaZero.Value = false;
            _depoisEm = Time.unscaledTime + EpocaDepoisDaPorta;
            // (o F1 pulou o Dia 0 e a fuga: o caderno vai direto para o bunker)
            if (Fase == FaseDaCampanha.DiaZero || Fase == FaseDaCampanha.Fuga) ComecarAFase(FaseDaCampanha.OBunker);
        }

        // (no Update da MundoDeLakewood.Troncos.cs) a época de depois, um pouco depois da porta
        void AcompanharOFimDoDiaZero()
        {
            if (_depoisEm < 0f || Time.unscaledTime < _depoisEm) return;
            _depoisEm = -1f;
            if (IsServer) PorAEpoca(EpocaDoMundo.Depois);
        }
    }
}
