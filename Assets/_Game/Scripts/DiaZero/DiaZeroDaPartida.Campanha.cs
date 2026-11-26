using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.World;
using static ZombieCityCleaner.World.CampanhaDeLakewood;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// O Dia 0 no caderno das missões (25/11/2026, a pedido do Pedro: "D0 - comprar recursos (depois de comprar algumas
    /// coisas) - Voltar para casa (quando chegar em casa) - Guardar itens em armazém"), só no Lakewood: o jornal na TV, as
    /// compras do grupo (<see cref="MinimoDeCompras"/>), todos de volta em casa, o armazém e as chaves. As chaves só trancam
    /// a casa na vez delas: antes, o aviso diz o que falta (o F1 continua pulando tudo).
    /// </summary>
    public partial class DiaZeroDaPartida
    {
        float _proximaContaDaCampanha;
        int _cacambaVista = -1;

        bool NaCampanha => NoLakewood && _lakewood != null && _lakewood.IsSpawned && _lakewood.Fase == FaseDaCampanha.DiaZero;

        // no anfitrião: uma submissão do Dia 0 foi feita (só conta na vez dela)
        void ConcluirNoDiaZero(int passo)
        {
            if (IsServer && NaCampanha) _lakewood.Concluir(FaseDaCampanha.DiaZero, passo);
        }

        // no anfitrião, a cada meio segundo na rua: as compras do grupo (o número do caderno) e a volta para casa
        void CampanhaNaRua()
        {
            // (26/11/2026, a integração do save do Dia 0) o Dia 0 que voltou do save conta só depois do Salvamento.Restaurar:
            // antes dele, a caçamba e o armazém ainda estão vazios (as estantes do save entram nele), e o "Guarde no armazém"
            // saía feito sozinho quando a rua ficava pronta primeiro (o Continuar sem fechar o jogo). O jogo novo conta já
            if (!IsServer || !NaCampanha || !Salvar.Salvamento.JaRestaurou || Time.unscaledTime < _proximaContaDaCampanha) return;
            _proximaContaDaCampanha = Time.unscaledTime + 0.5f;
            if (_lakewood.Em(FaseDaCampanha.DiaZero, CompreRecursos))
            {
                // (25/11/2026, a revisão: contava as entradas da lista, e a compra em pilha entra na do mesmo item; 5 latas e 3
                // águas davam "4 de 5") as coisas compradas, como a linha da carteira do caderno
                int compras = ComprasDoGrupo();
                _lakewood.Numeros(Mathf.Min(compras, MinimoDeCompras), 0, 0);
                if (compras >= MinimoDeCompras) ConcluirNoDiaZero(CompreRecursos);
                return;
            }
            if (_lakewood.Em(FaseDaCampanha.DiaZero, VolteParaCasa))
            {
                // todos os jogadores em casa (o grupo volta junto)
                var rua = RuaDoDiaZero.Instancia;
                if (rua == null) return;
                int jogadores = 0;
                foreach (var kv in NetworkManager.ConnectedClients)
                {
                    var po = kv.Value.PlayerObject;
                    if (po == null) continue;
                    jogadores++;
                    if (!rua.DentroDaCasa(po.transform.position)) return;
                }
                if (jogadores > 0) ConcluirNoDiaZero(VolteParaCasa);
                return;
            }
            if (_lakewood.Em(FaseDaCampanha.DiaZero, GuardeNoArmazem))
            {
                // (25/11/2026) a caçamba vazia: tudo foi para o armazém da garagem (a tela da caçamba, "Descarregar"); com o
                // armazém cheio (nada do que sobrou cabe mais nele), acaba assim mesmo, para as chaves não travarem
                int naCacamba = LakewoodNaCena.CacambaComAsCompras;
                _lakewood.Numeros(naCacamba, 0, 0);
                var estantes = EstantesDoBunker.Instancia;
                if (naCacamba == 0 || (naCacamba != _cacambaVista && estantes != null && estantes.NadaMaisCabe(LakewoodNaCena.CacambaDaFamilia, EstantesDoBunker.OArmazem)))
                    ConcluirNoDiaZero(GuardeNoArmazem);
                _cacambaVista = naCacamba;
            }
        }

        /// <summary>
        /// Quantas coisas o grupo comprou no Dia 0 (25/11/2026, a revisão): a mesma conta do <see cref="Compras(ulong)"/>, de
        /// todos (a pilha conta pelo número dentro dela; a caixa de munição, por uma). (25/11/2026, o save do Dia 0) No
        /// anfitrião, contam também as compras do save de quem ainda não voltou para a partida (elas estão na caçamba ou no
        /// armazém, como as dos outros).
        /// </summary>
        public int ComprasDoGrupo()
        {
            int n = 0;
            foreach (var c in _compras) n += Contar(c);
            foreach (var lista in _comprasDoSave.Values)
                foreach (var c in lista) n += Contar(c);
            return n;
        }

        /// <summary>Em cada máquina: as chaves trancam a casa agora? Senão, o que falta fazer antes (para o aviso).</summary>
        public bool PodeTrancar(out string falta)
        {
            falta = null;
            if (!NaCampanha || _lakewood.Em(FaseDaCampanha.DiaZero, FecheACasa)) return true;
            falta = $"Ainda não. Antes: {Texto(FaseDaCampanha.DiaZero, _lakewood.Passo, _lakewood, true).ToLowerInvariant()}";
            return false;
        }
    }
}
