using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O Lakewood na rede (12/11/2026, o Plano_Lakewood, "O jogo do mundo sorteado no Lakewood", a parte 1): o objeto da cena
    /// "Mundo (Lakewood)", com o NetworkObject, como o "Mundo" do mundo sorteado. Guarda o que a partida divide entre todos: a
    /// semente da partida (o anfitrião sorteia ao abrir: os bichos, os grupos e o que os comerciantes vendem saem diferentes
    /// a cada partida) e, nas partes seguintes, as listas da rede (as árvores cortadas, os painéis desmontados, as marcas do
    /// mapa). Os sistemas do mundo que ficam neste objeto (os sobreviventes, os acampamentos, as peças dos carros, o save)
    /// leem o mundo pelo <see cref="MundoDoJogo"/>. Posto na cena pelo Editor (Lakewood > O jogo do mundo sorteado na cena).
    /// </summary>
    public partial class MundoDeLakewood : NetworkBehaviour
    {
        public static MundoDeLakewood Instancia { get; private set; }

        readonly NetworkVariable<int> _semente = new NetworkVariable<int>(0);

        /// <summary>A semente da partida (0 antes de o anfitrião sortear).</summary>
        public int Semente => _semente.Value;

        /// <summary>A partida começou no Lakewood: o objeto na rede, a semente sorteada e o chão montado.</summary>
        public bool Pronto => IsSpawned && _semente.Value != 0 && LakewoodNaCena.Instancia != null && LakewoodNaCena.Instancia.ChaoNoMapa != null;

        void Awake()
        {
            Instancia = this;
            IniciarAsListas();
        }

        public override void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            // a semente da partida: nova a cada partida; a do save, se a partida veio dele (a parte 8). (26/11/2026, a revisão
            // do save do Dia 0) Sempre, e não só com ela ainda 0: o objeto é da cena, que não recarrega entre as partidas, e a
            // variável ficava com a da partida anterior; o Continuar de outro mundo, sem fechar o jogo, abria com a semente do
            // primeiro, e o save seguinte a gravava no arquivo dele
            if (IsServer)
            {
                var salvo = Salvar.Salvamento.Atual;
                _semente.Value = salvo != null && salvo.lakewood && salvo.temEstado && salvo.semente != 0 ? salvo.semente : SementeNova();
            }
            // o Dia 0 (22/11/2026, a etapa 4 do Docs/23): o jogo novo começa nele; a casa da família entra já aqui, em cada
            // máquina, para o jogador nascer na sala dela (ele nasce logo depois deste objeto). O save do Dia 0 ou da fuga
            // (25/11/2026) volta neles também aqui, pelo mesmo caminho
            if (IsServer) TalvezComecarODiaZero();
            if (LakewoodNaCena.Instancia != null) LakewoodNaCena.Instancia.AcompanharODiaZero();
            AoNascerAsArvores();
            PaineisNoSpawn();
        }

        public override void OnNetworkDespawn()
        {
            Retomada = false;   // (25/11/2026, o save do Dia 0: a retomada é desta partida)
            _retomandoAFuga = false;
            AoSumirAsArvores();
            PaineisDoMundo.DefinirDesmontados(null);
            base.OnNetworkDespawn();
        }

        /// <summary>Uma semente nova (nunca 0).</summary>
        public static int SementeNova()
        {
            int s = (int)(System.DateTime.UtcNow.Ticks & 0x7fffffff) ^ Random.Range(1, int.MaxValue);
            return s == 0 ? 1 : s & 0x7fffffff;
        }

        /// <summary>No anfitrião: a semente de uma partida salva (o save).</summary>
        public void PorASemente(int semente)
        {
            if (IsServer && semente != 0) _semente.Value = semente;
        }

        // as listas da rede das partes seguintes (as árvores cortadas, os painéis, as marcas): criadas no Awake, antes do
        // spawn, como o Netcode pede
        partial void IniciarAsListas();
    }
}
