using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A fuga (23/11/2026, as etapas 5 e 6 do Docs/23-Campanha-e-Mapa.md; o Pedro: "Hoje vai ter uma missão principal no
    /// canto direito superior, com o objetivo de correr. Nisso eu já imagino o caos, pessoas sendo devoradas pelos Ocos,
    /// brutamontes na cidade, long também. E seria uma missão onde daria para falhar e morrer, que se acontecer retornaria ao
    /// começo. Até quando chegarmos perto do bunker descendo a rua a pé, com isso vamos ver o bunker, algumas pessoas
    /// entrando e vamos entrar junto"). Aqui, só a marca da rede e as regras do anfitrião:
    ///  - as chaves da casa (o fim do Dia 0) começam a fuga (<see cref="ComecarAFuga"/>): os 30 dias passam numa tela (o
    ///    DiaZeroDaPartida), e a noite do colapso começa na casa da família, ainda na Woodcreek de antes;
    ///  - a cada tentativa (<see cref="Tentativa"/>), cada um volta para a sala da casa, e o caos é montado de novo (os
    ///    zumbis da fuga: LakewoodNaCena.Fuga.cs);
    ///  - alguém morreu: a fuga falhou (<see cref="Falhou"/>), e depois da tela ela recomeça para todos (<see cref="Recomecar"/>);
    ///  - todos os vivos chegaram ao portão do bunker (<see cref="Chegou"/>): a tela dos 5 anos, e a época volta para depois
    ///    (<see cref="AcabarAFuga"/>);
    ///  - (25/11/2026, o save do Dia 0) o jogo grava na fuga (<see cref="PodeSalvarAFuga"/>), e o Continuar volta no começo
    ///    de uma tentativa, como quando ela falha (<see cref="RetomarAFuga"/>).
    /// </summary>
    public partial class MundoDeLakewood
    {
        /// <summary>Quanto dura a tela dos 30 dias antes de a noite do colapso aparecer (s).</summary>
        public const float TelaDosTrintaDias = 11f;
        /// <summary>Quanto a tela da fuga que falhou fica antes de recomeçar (s).</summary>
        const float TelaDaFalha = 4.5f;

        readonly NetworkVariable<bool> _fuga = new NetworkVariable<bool>();
        readonly NetworkVariable<int> _tentativa = new NetworkVariable<int>();
        readonly NetworkVariable<bool> _falhou = new NetworkVariable<bool>();
        readonly NetworkVariable<bool> _chegou = new NetworkVariable<bool>();
        readonly NetworkVariable<double> _fugaDesde = new NetworkVariable<double>();

        float _recomecarEm = -1f, _acabarEm = -1f;
        // (25/11/2026, a revisão) só no anfitrião: a tentativa com que a fuga de agora começou
        int _primeiraTentativa = -1;

        /// <summary>
        /// Só no anfitrião: a primeira tentativa da fuga de agora, a das chaves (as outras são os recomeços). Nela, o que cada
        /// um leva vai para o armazém da garagem (LakewoodNaCena.Fuga.cs). O número da tentativa não volta a 1: numa segunda
        /// fuga na mesma partida (o Dia 0 de novo pelo F1), a primeira é outra.
        /// </summary>
        public int PrimeiraTentativa => _primeiraTentativa;

        /// <summary>A fuga está acontecendo (da tela dos 30 dias até a chegada ao bunker).</summary>
        public bool NaFuga => IsSpawned && _fuga.Value;
        /// <summary>O número da tentativa de agora (muda a cada recomeço: cada máquina põe o seu jogador de volta na casa).</summary>
        public int Tentativa => _tentativa.Value;
        /// <summary>Alguém morreu: a tela da fuga que falhou (até o recomeço).</summary>
        public bool Falhou => IsSpawned && _falhou.Value;
        /// <summary>Todos chegaram ao bunker: a tela dos 5 anos.</summary>
        public bool Chegou => IsSpawned && _chegou.Value;
        /// <summary>Há quanto tempo a tentativa de agora começou (s, no relógio da partida).</summary>
        public float TempoDaFuga => IsSpawned ? (float)(NetworkManager.ServerTime.Time - _fugaDesde.Value) : 0f;

        /// <summary>
        /// (25/11/2026, o save do Dia 0) O jogo pode gravar a fuga agora: na fuga, depois da tela dos 30 dias (com a tentativa
        /// já montada: 1 s depois de ela começar, para o armazém da garagem já ter guardado o que cada um levava), fora da tela
        /// da falha e da chegada, e com a fuga retomada já começada. As regras são do anfitrião (o Salvamento).
        /// </summary>
        public bool PodeSalvarAFuga => IsSpawned && NaFuga && Antes && !_falhou.Value && !_chegou.Value && _recomecarEm < 0f &&
                                       _acabarEm < 0f && !_retomandoAFuga && TempoDaFuga >= 1f;

        /// <summary>A marca "Bunker" do mapa é posta por este autor (o save não a grava: a tentativa põe de novo).</summary>
        public const string AutorDaMarcaDoBunker = "Fuga";

        /// <summary>A tentativa da fuga retomada começa tanto depois de o mundo voltar do save (s), como no recomeço.</summary>
        const float EsperaDaRetomada = 1.5f;
        /// <summary>
        /// Até quanto a fuga retomada espera a casa, o chão e o jogador do anfitrião, depois de o mundo voltar do save (s):
        /// depois, começa mesmo assim.
        /// </summary>
        const float RetomadaNoMaximo = 90f;
        // (25/11/2026) no anfitrião: a fuga retomada ainda espera o mundo voltar do save para a tentativa começar
        bool _retomandoAFuga;
        float _retomandoDesde;

        /// <summary>
        /// No anfitrião (25/11/2026, o save do Dia 0: o Continuar num save da fuga): a noite do colapso de novo, no começo de
        /// uma tentativa, como quando ela falha: a época de antes, a fuga, a noite parada e uma tentativa nova, sem a tela dos 30
        /// dias. A tentativa só começa a contar quando o mundo voltou do save, a casa está pronta, com o chão dela, e o anfitrião
        /// já está nela (<see cref="AcompanharARetomada"/>): antes disso, os zumbis da fuga não nascem. Com o armazém já guardado
        /// ('armazemFeito'), a tentativa não guarda de novo (a <see cref="PrimeiraTentativa"/> fica -1): cada um volta só com
        /// as roupas e a lanterna, e o armazém, a caçamba e o que não coube ficam como foram salvos.
        /// </summary>
        void RetomarAFuga(bool armazemFeito)
        {
            if (!IsServer) return;
            _depoisEm = -1f;
            _recomecarEm = _acabarEm = -1f;
            PorAEpoca(EpocaDoMundo.Antes);
            _diaZero.Value = false;
            _falhou.Value = false;
            _chegou.Value = false;
            _fuga.Value = true;
            _comDiaZero.Value = true;   // (a fuga só vem depois do Dia 0)
            ComecarAFase(FaseDaCampanha.Fuga);
            // a tentativa nova, parada até o mundo voltar (o relógio dela é acertado no AcompanharAFuga)
            NovaTentativa(RetomadaNoMaximo + 3600f);
            _primeiraTentativa = armazemFeito ? -1 : _tentativa.Value;
            _retomandoAFuga = true;
            _retomandoDesde = -1f;
            var ciclo = CicloDoDia.Instancia;
            if (ciclo != null && ciclo.IsSpawned) ciclo.SempreDeNoite(true);   // (de novo quando a tentativa começar: o ciclo nasce andando)
            Retomada = true;
            Debug.Log($"[Fuga] A fuga voltou do save: a tentativa {_tentativa.Value}, na casa da família ({(armazemFeito ? "o armazém já guardado" : "o armazém ainda vai guardar")}).");
        }

        // (25/11/2026) no anfitrião, a cada quadro: a fuga retomada começa a contar quando o mundo voltou do save, a casa da
        // família está pronta, com o chão dela, e o jogador do anfitrião já foi para lá (a FugaNaTela leva); ou, com o mundo de
        // volta, depois do tempo máximo
        void AcompanharARetomada()
        {
            if (!_retomandoAFuga) return;
            if (!NaFuga || _chegou.Value)
            {
                _retomandoAFuga = false;
                return;
            }
            if (!Pronto || !Salvar.Salvamento.JaRestaurou) return;
            // (o anfitrião ainda criando o personagem: a fuga espera por ele, sem contar o tempo máximo)
            var j = Player.JogadorRede.Local;
            if (j == null || !j.Criado) return;
            if (_retomandoDesde < 0f) _retomandoDesde = Time.unscaledTime;
            if (Time.unscaledTime - _retomandoDesde < RetomadaNoMaximo)
            {
                var palco = DiaZero.WoodcreekDoDiaZero.Atual;
                if (palco == null || !palco.Pronta || !LakewoodNaCena.ComPalcoDoDiaZero) return;
                // (com o palco, os nascimentos são a sala da casa)
                var sala = Network.PontosDeNascimento.Ponto(NetworkManager.LocalClientId).position;
                IChaoDosZumbis chao = LakewoodNaCena.Instancia;
                if (chao == null || !chao.Pronto(sala, 12f)) return;
                if ((j.transform.position - sala).sqrMagnitude > 25f * 25f) return;
            }
            _retomandoAFuga = false;
            var ciclo = CicloDoDia.Instancia;
            if (ciclo != null) ciclo.SempreDeNoite(true);
            _fugaDesde.Value = NetworkManager.ServerTime.Time + EsperaDaRetomada;
            Debug.Log($"[Fuga] A fuga retomada começa: a tentativa {_tentativa.Value}, {Time.unscaledTime - _retomandoDesde:F1} s depois de o mundo voltar do save.");
        }

        /// <summary>
        /// No anfitrião (as chaves da casa, DiaZeroDaPartida): o Dia 0 acaba e a fuga começa, na mesma época de antes (a
        /// noite do colapso, 30 dias depois).
        /// </summary>
        public void ComecarAFuga()
        {
            if (!IsServer) return;
            Retomada = false;   // (25/11/2026: a fuga que começa pelas chaves não é a do save)
            _retomandoAFuga = false;
            _depoisEm = -1f;
            _diaZero.Value = false;
            _falhou.Value = false;
            _chegou.Value = false;
            _fuga.Value = true;
            if (Fase != FaseDaCampanha.Fuga) ComecarAFase(FaseDaCampanha.Fuga);   // (25/11/2026: o caderno das missões)
            NovaTentativa(TelaDosTrintaDias);
            _primeiraTentativa = _tentativa.Value;
            Debug.Log("[Fuga] Os 30 dias passaram: a noite do colapso começa na casa da família.");
        }

        // a tentativa nova: o relógio dela começa depois da tela (dos 30 dias, ou da que falhou)
        void NovaTentativa(float depois)
        {
            _tentativa.Value++;
            _fugaDesde.Value = NetworkManager.ServerTime.Time + depois;
        }

        /// <summary>No anfitrião: alguém morreu na fuga. A tela da falha, e ela recomeça para todos.</summary>
        public void FalharAFuga(string quem)
        {
            if (!IsServer || !NaFuga || _falhou.Value || _chegou.Value) return;
            _falhou.Value = true;
            _recomecarEm = Time.unscaledTime + TelaDaFalha;
            Debug.Log($"[Fuga] A fuga falhou ({quem}): ela recomeça na casa da família.");
        }

        /// <summary>No anfitrião: todos os vivos chegaram ao portão do bunker. A tela dos 5 anos, e depois a época de depois.</summary>
        public void ChegarAoBunker()
        {
            if (!IsServer || !NaFuga || _chegou.Value || _falhou.Value) return;
            _chegou.Value = true;
            _acabarEm = Time.unscaledTime + 2.5f;
            Debug.Log("[Fuga] Todos chegaram ao bunker da comunidade.");
            Concluir(FaseDaCampanha.Fuga, CampanhaDeLakewood.FujaParaOBunker);   // (25/11/2026: o caderno das missões)
            // (25/11/2026) a chegada não tira mais nada: cada um saiu de casa só com as roupas e a lanterna (o resto ficou no
            // armazém da garagem, LakewoodNaCena.Armazem.cs); antes, o que levavam ia para a caçamba
        }

        /// <summary>No anfitrião (o F1, ou a chegada): a fuga acaba, e a época volta para depois (os 5 anos).</summary>
        public void AcabarAFuga()
        {
            if (!IsServer) return;
            _retomandoAFuga = false;
            _fuga.Value = false;
            _falhou.Value = false;
            _depoisEm = Time.unscaledTime + EpocaDepoisDaPorta;
            if (Fase == FaseDaCampanha.Fuga) ComecarAFase(CampanhaDeLakewood.Seguinte(FaseDaCampanha.Fuga));   // (o F1 pulou a fuga)
            // 5 anos depois, de manhã (o relógio volta a andar)
            var ciclo = CicloDoDia.Instancia;
            if (ciclo != null)
            {
                ciclo.SempreDeNoite(false);
                CicloDoDia.MudarHora(DiaZero.DiaZeroDaPartida.HoraDaSaida);
                ciclo.HoraRede.Value = DiaZero.DiaZeroDaPartida.HoraDaSaida;
            }
            Debug.Log("[Fuga] A fuga acabou: 5 anos depois, no bunker.");
        }

        /// <summary>O texto da marca do bunker no mapa (a fuga).</summary>
        public const string MarcaDoBunker = "Bunker";

        /// <summary>No anfitrião: a marca "Bunker" no mapa, no portão (uma só: a de antes sai).</summary>
        public void PorAMarcaDoBunker(Vector3 portao)
        {
            if (!IsServer) return;
            TirarAMarcaDoBunker();
            PorMarca(UI.TipoDeMarca.Anotacao, portao + Vector3.up * 1.5f, 2, MarcaDoBunker, ulong.MaxValue, AutorDaMarcaDoBunker);
        }

        /// <summary>
        /// No anfitrião: a marca "Bunker" da fuga sai (o save também não a grava: Salvamento.Salvar). (26/11/2026, a revisão do
        /// save do Dia 0) Só a da fuga, pelo autor: as anotações que voltam do save também vêm sem dono, e a de um jogador com o
        /// texto "Bunker" saía junto a cada tentativa.
        /// </summary>
        public void TirarAMarcaDoBunker()
        {
            if (!IsServer || _marcas == null) return;
            for (int i = _marcas.Count - 1; i >= 0; i--)
                if (_marcas[i].tipo == UI.TipoDeMarca.Anotacao && _marcas[i].dono == ulong.MaxValue && _marcas[i].texto.ToString() == MarcaDoBunker &&
                    _marcas[i].autor.ToString() == AutorDaMarcaDoBunker)
                    _marcas.RemoveAt(i);
        }

        // a cada quadro, no anfitrião (o Update da MundoDeLakewood.Troncos.cs): o recomeço depois da falha e o fim depois da
        // chegada
        void AcompanharAFuga()
        {
            if (!IsServer) return;
            AcompanharARetomada();   // (25/11/2026, o save do Dia 0: a fuga que voltou do save)
            if (_recomecarEm >= 0f && Time.unscaledTime >= _recomecarEm)
            {
                _recomecarEm = -1f;
                Recomecar();
            }
            if (_acabarEm >= 0f && Time.unscaledTime >= _acabarEm)
            {
                _acabarEm = -1f;
                AcabarAFuga();
            }
        }

        // a fuga de novo, do começo (a falha): a tentativa nova (cada um volta para a casa e é curado pela cena)
        void Recomecar()
        {
            if (!NaFuga) return;
            _falhou.Value = false;
            NovaTentativa(1.5f);
            Debug.Log($"[Fuga] A tentativa {_tentativa.Value}.");
        }
    }
}
