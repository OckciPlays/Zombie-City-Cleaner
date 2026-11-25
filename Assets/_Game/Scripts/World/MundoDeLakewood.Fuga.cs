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
    ///    (<see cref="AcabarAFuga"/>).
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
        /// No anfitrião (as chaves da casa, DiaZeroDaPartida): o Dia 0 acaba e a fuga começa, na mesma época de antes (a
        /// noite do colapso, 30 dias depois).
        /// </summary>
        public void ComecarAFuga()
        {
            if (!IsServer) return;
            _depoisEm = -1f;
            _diaZero.Value = false;
            _falhou.Value = false;
            _chegou.Value = false;
            _fuga.Value = true;
            NovaTentativa(TelaDosTrintaDias);
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
            // (24/11/2026, o Pedro: "Perdemos todos os itens na loucura do D0"): o que cada um levava vai para a caçamba
            LakewoodNaCena.Instancia?.PerderTudoNaFuga();
        }

        /// <summary>No anfitrião (o F1, ou a chegada): a fuga acaba, e a época volta para depois (os 5 anos).</summary>
        public void AcabarAFuga()
        {
            if (!IsServer) return;
            _fuga.Value = false;
            _falhou.Value = false;
            _depoisEm = Time.unscaledTime + EpocaDepoisDaPorta;
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
            PorMarca(UI.TipoDeMarca.Anotacao, portao + Vector3.up * 1.5f, 2, MarcaDoBunker, ulong.MaxValue, "Fuga");
        }

        /// <summary>No anfitrião: a marca "Bunker" da fuga sai (ela iria para o save).</summary>
        public void TirarAMarcaDoBunker()
        {
            if (!IsServer || _marcas == null) return;
            for (int i = _marcas.Count - 1; i >= 0; i--)
                if (_marcas[i].tipo == UI.TipoDeMarca.Anotacao && _marcas[i].dono == ulong.MaxValue && _marcas[i].texto.ToString() == MarcaDoBunker)
                    _marcas.RemoveAt(i);
        }

        // a cada quadro, no anfitrião (o Update da MundoDeLakewood.Troncos.cs): o recomeço depois da falha e o fim depois da
        // chegada
        void AcompanharAFuga()
        {
            if (!IsServer) return;
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
