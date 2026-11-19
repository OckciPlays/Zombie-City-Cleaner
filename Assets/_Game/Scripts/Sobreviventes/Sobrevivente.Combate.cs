using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// O combate mais esperto dos NPCs (18/11/2026, a etapa 3, a pedido: "eles pegar abrigos no tiro-teio, inteligência
    /// iguais do GTA 5 ... entender o tipo de arma ... e usar o lugar a seu favor ... melhorar a pontaria"; "sozinho em um
    /// acampamento é quase impossível, igual na vida real"). As armas não mudam (as de sucata dos civis, as reais dos
    /// militares): muda o jeito de usar.
    /// <list type="bullet">
    /// <item>a mira que assenta: quanto mais tempo ele vê o alvo, melhor ele mira (<see cref="_conhece"/>), e isso não
    /// zera quando ele volta para a proteção; o militar assenta mais depressa e às vezes mira na cabeça;</item>
    /// <item>a distância boa da arma (<see cref="DistanciaBoa"/>): a proteção escolhida fica perto dela;</item>
    /// <item>a proteção baixa (agachado atrás dela, de pé para atirar) e o lugar reservado (dois não disputam o mesmo);</item>
    /// <item>o fogo para prender: quem tem munição atira por uns segundos onde o alvo sumiu;</item>
    /// <item>o cerco: com três ou mais no tiroteio e o alvo escondido, um vai pelo lado (<see cref="GrupoDeSobreviventes.flanqueador"/>);</item>
    /// <item>cada um mira no jogador mais perto que ele vê (não só no do grupo).</item>
    /// </list>
    /// </summary>
    public partial class Sobrevivente
    {
        /// <summary>De 0 a 1: o quanto ele já acompanhou o alvo (a mira assentada). Sobe vendo, cai devagar sem ver.</summary>
        float _conhece;
        JogadorRede _alvoProprio;
        float _proximaRajada, _prendendoAte;
        bool _protecaoBaixa;
        bool _flanqueando;

        // os acertos de um tiro (reaproveitados: antes eram dois dicionários novos por tiro)
        static readonly Dictionary<JogadorRede, (float dano, Vector3 ponto)> _danoNoJogador = new Dictionary<JogadorRede, (float, Vector3)>();
        static readonly Dictionary<Zumbi, (Hitbox h, Vector3 p, float dano)> _noZumbi = new Dictionary<Zumbi, (Hitbox, Vector3, float)>();

        bool Militar => Papel == PapelDoSobrevivente.Militar;

        /// <summary>Quanto tempo vendo o alvo até a mira assentar de vez (s).</summary>
        float TempoParaAssentar => Militar ? 1.6f : Arma == TipoDeArma.Sniper ? 2.2f : 2.8f;

        /// <summary>A distância boa da arma (m): de onde ela acerta bem e não fica exposta demais.</summary>
        float DistanciaBoa => Arma switch
        {
            TipoDeArma.Espingarda => 10f,
            TipoDeArma.Sniper => 45f,
            TipoDeArma.Rifle => 28f,
            TipoDeArma.Submetralhadora => 16f,
            _ => Armas.DeSucata(Arma) && DadosDaArma.capacidade <= 3 ? 26f : 15f,
        };

        /// <summary>A mira assenta vendo o alvo e desassenta devagar sem ver (a cada quadro do combate).</summary>
        void AtualizarAMira(bool vejo)
        {
            float dt = Time.deltaTime;
            _conhece = vejo ? Mathf.Min(1f, _conhece + dt / TempoParaAssentar) : Mathf.Max(0f, _conhece - dt / 9f);
        }

        /// <summary>
        /// Onde ele mira no alvo: o peito; o militar com a mira assentada, às vezes a cabeça. Um pouco à frente de quem corre
        /// (ele acompanha o movimento).
        /// </summary>
        Vector3 PontoDaMira(IAlvoDeZumbi quem, Vector3 peito)
        {
            if (quem is JogadorRede j)
            {
                // (18/11/2026) a sniper, pela luneta: metade das vezes na cabeça
                float cabeca = Arma == TipoDeArma.Sniper ? 0.5f : Militar ? 0.15f : 0.04f;
                // (só quando a mira é o peito: o jogador na proteção, com só a cabeça de fora, já é a cabeça)
                if (_conhece > (Arma == TipoDeArma.Sniper ? 0.4f : 0.7f) && Random.value < cabeca && peito.y - quem.transform.position.y < 1.4f)
                    peito += Vector3.up * 0.32f;
            }
            return peito;
        }

        /// <summary>
        /// Durante o combate (do Olhar): o jogador mais perto que ele vê, se for bem mais perto que o do grupo (cada um atira
        /// em quem está na frente dele; com dois jogadores, o grupo não fica só num).
        /// </summary>
        void EscolherOAlvo()
        {
            _alvoProprio = null;
            if (grupo == null || grupo.estado != EstadoDoGrupo.Combate || !grupo.Hostil) return;
            float melhor = AlcanceDaVista * AlcanceDaVista;
            float doGrupo = grupo.inimigo != null && grupo.inimigo.AlvoValido
                ? (grupo.inimigo.transform.position - transform.position).sqrMagnitude : float.MaxValue;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.AlvoValido || !j.Criado || ReferenceEquals(j, grupo.inimigo)) continue;
                Vector3 peito = j.transform.position + Vector3.up * 1.3f;
                float d2 = (peito - Olho).sqrMagnitude;
                if (d2 >= melhor || d2 > doGrupo * 0.5f) continue;
                if (Physics.Linecast(Olho, peito, MascaraDoCenario, QueryTriggerInteraction.Ignore)) continue;
                melhor = d2;
                _alvoProprio = j;
            }
        }

        /// <summary>
        /// O fogo para prender: o alvo acabou de sumir atrás de alguma coisa; ele atira por uns segundos onde o viu (quem
        /// tem pente sobrando), para o alvo não sair dali. Devolve se atirou.
        /// </summary>
        bool PrenderNoLugar(float agora)
        {
            if (agora > _prendendoAte || pente <= 0 || grupo == null) return false;
            var d = DadosDaArma;
            if (!d.automatica && pente < Mathf.Max(2, d.capacidade / 2)) return false;
            Vector3 onde = grupo.ondeViu + Vector3.up * 1.1f + Random.insideUnitSphere * 0.5f;
            if (Physics.Linecast(Olho, onde, MascaraDoCenario, QueryTriggerInteraction.Ignore) && Random.value < 0.5f) onde += Vector3.up * 0.4f;
            AtirarNo(null, onde, agora, true);
            return true;
        }

        /// <summary>
        /// O cerco: com três ou mais vivos no tiroteio e o alvo escondido há uns segundos, um (o que está mais longe da
        /// linha de tiro dos outros) vai para um ponto pelo lado do alvo, de onde dá para vê-lo. Devolve o ponto.
        /// </summary>
        bool PontoDoFlanco(Vector3 inimigo, out Vector3 ponto)
        {
            ponto = Vector3.zero;
            Vector3 deMim = transform.position - inimigo;
            deMim.y = 0f;
            if (deMim.sqrMagnitude < 1f) return false;
            float dist = Mathf.Clamp(deMim.magnitude, 9f, 18f);
            deMim.Normalize();
            float lado = Random.value < 0.5f ? 1f : -1f;
            for (int t = 0; t < 8; t++)
            {
                float ang = lado * Random.Range(65f, 110f) * (t % 2 == 0 ? 1f : -1f);
                var p = inimigo + Quaternion.Euler(0f, ang, 0f) * deMim * dist;
                if (!NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) continue;
                // de lá, a linha até o alvo fica livre (o lado dele que a proteção não tapa)
                if (Physics.Linecast(hit.position + Vector3.up * 1.4f, inimigo + Vector3.up * 1.2f, MascaraDoCenario, QueryTriggerInteraction.Ignore)) continue;
                ponto = hit.position;
                return true;
            }
            return false;
        }

        /// <summary>Um tiro dele acertou ou passou perto: a reação (do Ferir: ele levou o tiro).</summary>
        void ReagirAoTiro(float agora)
        {
            if (_morto.Value || _fugindo) return;
            _conhece *= 0.55f;   // a dor atrapalha a mira
            if (grupo == null || grupo.estado != EstadoDoGrupo.Combate) return;
            if (_temProtecao)
            {
                // espiando: volta para trás da proteção na hora
                if (_fase == 2)
                {
                    _fase = 1;
                    _fimDaFase = agora + Random.Range(1.2f, 2.4f);
                    Ir(_protecao, 3.6f, 0.3f);
                }
            }
            else
            {
                _proximaProtecao = 0f;   // sem proteção: procura uma já
                _protecaoDepoisDe = 0f;
            }
            // muito ferido: a próxima proteção é mais para trás
            if (vida < (Militar ? VidaDoMilitar : VidaMaxima) * 0.35f) _recuar = true;
        }

        bool _recuar;
        float _semVerDesde = -1f;
        int _passosAMais;

        /// <summary>
        /// Espiando pela quina (ou pela janela) sem ver o alvo há 1,2 s: mais um passinho para o lado do vão (20 cm, até 3
        /// vezes). A conta do espiar não bate sempre com o corpo de verdade (o andar de cima, a quina torta); assim ele acha.
        /// 18/11/2026.
        /// </summary>
        void EspiarMaisUmPouco(bool vejo, float agora)
        {
            if (vejo || _ladoDoEspiar == 0 || _passosAMais >= 3)
            {
                _semVerDesde = -1f;
                return;
            }
            if (_semVerDesde < 0f) { _semVerDesde = agora; return; }
            if (agora - _semVerDesde < 1.2f) return;
            _semVerDesde = agora;
            var mais = _espiar + transform.right * (_ladoDoEspiar * 0.2f);
            if (!UnityEngine.AI.NavMesh.SamplePosition(mais, out var h, 0.3f, UnityEngine.AI.NavMesh.AllAreas)) { _passosAMais = 3; return; }
            _espiar = h.position;
            _passosAMais++;
            Ir(_espiar, 1.5f, 0.1f);
        }
        /// <summary>O começo do tiroteio (18/11/2026): se ele já está nele, e até quando atira de onde está antes de ir para a proteção.</summary>
        bool _noTiroteio;
        float _protecaoDepoisDe;
        /// <summary>A roda (18/11/2026): o inimigo colado; a automática solta o pente inteiro (vale por um quadro).</summary>
        bool _soltarOPente;

        // para o F1 e os testes (18/11/2026)
        /// <summary>No anfitrião: -1 fora do tiroteio ou sem proteção; 0 indo para ela; 1 escondido; 2 espiando.</summary>
        public int FaseNaProtecao => grupo != null && grupo.estado == EstadoDoGrupo.Combate && _temProtecao ? _fase : -1;
        /// <summary>No anfitrião: quantas vezes ele atirou às cegas.</summary>
        public int RajadasAsCegas { get; private set; }

        /// <summary>
        /// No anfitrião (o F1, "Armar o acampamento"): troca a arma de fogo dele, com o pente cheio (para ver a espingarda, a
        /// sniper e o rifle no acampamento, que só tem as de sucata).
        /// </summary>
        public void TrocarAArma(TipoDeArma arma)
        {
            if (!IsServer || ComBranca || _morto.Value) return;
            var id = Identidade.Value;
            id.arma = arma;
            Identidade.Value = id;
            _recarregando = false;
            pente = DadosDaArma.capacidade;
        }

        /// <summary>Para que lado ele espia pela quina, inclinando (1: a direita, -1: a esquerda, 0: sem inclinar).</summary>
        int _ladoDoEspiar;
        float _proximoCego, _cegoAte;

        /// <summary>
        /// O tiro às cegas (18/11/2026): escondido, com o alvo visto há pouco, ele põe só a arma para fora da proteção (pela
        /// quina ou por cima do muro baixo) e atira por menos de um segundo na direção de onde o alvo estava, sem olhar (a
        /// abertura de três vezes). Prende o alvo onde está. A sniper e a espingarda não (uma precisa olhar; a outra, chegar
        /// perto); nem atrás da proteção alta sem quina para espiar (a arma bateria na parede).
        /// </summary>
        void AtirarAsCegas(float agora)
        {
            if (grupo == null || pente <= 0 || _recarregando || ComBranca) return;
            if (Arma == TipoDeArma.Sniper || Arma == TipoDeArma.Espingarda) return;
            if (!_protecaoBaixa && _ladoDoEspiar == 0) return;
            if (agora >= _cegoAte)
            {
                if (agora < _proximoCego) return;
                if (agora - grupo.viuEm > 5f) return;
                if (!DadosDaArma.automatica && pente < 2) return;
                _proximoCego = agora + Random.Range(2.5f, 4.5f);
                _cegoAte = agora + Random.Range(0.6f, 1.1f);
                RajadasAsCegas++;
            }
            _querInclinar = _ladoDoEspiar * 30;
            AtirarNo(null, grupo.ondeViu + Vector3.up * 1.1f, agora, true, true);
        }

        float _proximoAvanco;

        /// <summary>
        /// A espingarda (18/11/2026, a pedido: "a sua intenção é chegar mais perto do jogador para dar o máximo de dano"):
        /// vai de proteção em proteção para perto (a nota da proteção pesa a distância boa, 10 m); a menos de 12 m e vendo o
        /// alvo, vai para cima dele atirando; sem proteção por perto, avança em zigue-zague; de mais de 22 m, não atira (o
        /// AtirarNo). Devolve se cuidou do quadro.
        /// </summary>
        bool EspingardaAvanca(IAlvoDeZumbi inimigo, Vector3 alvo, Vector3 peito, bool vejo, float agora)
        {
            Vector3 para = alvo - transform.position;
            para.y = 0f;
            float dist = para.magnitude;
            para /= Mathf.Max(dist, 0.01f);
            bool semProtecaoPerto = !_temProtecao && agora < _proximaProtecao;
            if (vejo && (dist < 12f || semProtecaoPerto))
            {
                _temProtecao = false;
                if (agora >= _proximoCaminho)
                {
                    _proximoCaminho = agora + 0.4f;
                    // perto: para cima dele; longe: em zigue-zague, pelo lado
                    Vector3 lado = dist < 12f ? Vector3.zero : Vector3.Cross(Vector3.up, para) * ((Random.value < 0.5f ? 1f : -1f) * Random.Range(2f, 4f));
                    var perto = alvo - para * Mathf.Min(4f, dist) + lado;
                    if (NavMesh.SamplePosition(perto, out var h, 2f, NavMesh.AllAreas)) Ir(h.position, dist < 12f ? 3.6f : 4.0f, 0.5f);
                }
                AtirarNo(inimigo, peito, agora);
                return true;
            }
            // longe, escondido: troca por uma proteção mais perto (sem gastar tiro daqui)
            if (dist > 14f && _temProtecao && _fase >= 1 && agora >= _proximoAvanco)
            {
                _proximoAvanco = agora + 5f;
                _temProtecao = false;
                _proximaProtecao = 0f;
            }
            return false;
        }
    }
}
