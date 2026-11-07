using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;
using Random = UnityEngine.Random;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A limpeza e a infecção (05/11/2026, a etapa 2 do sistema de vida, a pedido: "Nos primeiros dias a importância da
    /// bandagem ser anti-séptica é muito grande ... Se não for tratada com anti-séptico ... uns 80% de já no próximo dia
    /// evoluir com infecção da ferida e precisar de antibióticos ... e depois essa taxa ir caindo até chegar a quase 0").
    /// <para>A cada amanhecer (as 6 h do mundo; com o relógio parado, a cada 26 h vividas), cada ferida aberta com 12 h ou
    /// mais é sorteada, nos seus 3 primeiros dias: o 1º pela rapidez da limpeza (3% limpa logo, até 80% com 6 h suja), o 2º
    /// e o 3º pela troca do curativo antisséptico no dia (40% → 2% protegida; 15% → 0%); a fratura exposta, bem mais. O
    /// sorteio é fixo (a semente do personagem, o número do amanhecer, a parte, o dia): sair e voltar não muda nada.</para>
    /// <para>A infecção é uma só, do personagem: a ferida infeccionada (24 min reais: o membro fica lento, a perna manca) →
    /// a febre (12 min: o tremor, a visão turva; o xarope alivia) → a sepse (12 min: a vida cai até a morte). O antibiótico
    /// (uma dose a cada 6 h do jogo) cura com 90% na ferida infeccionada, 70% na febre e 40% na sepse (a dose que falha na
    /// sepse tira 10 de vida); quando cura, as feridas abertas param de sortear.</para>
    /// </summary>
    public partial class VidaJogador
    {
        /// <summary>Quanto dura cada fase da infecção (minutos do jogo: 1 minuto do jogo = 1 s real).</summary>
        public static float MinutosDaFase(int fase) => fase switch { 1 => 24 * 60f, 2 => 12 * 60f, _ => 12 * 60f };

        /// <summary>A chance do antibiótico curar em cada fase.</summary>
        public static float ChanceDoAntibiotico(int fase) => fase switch { 1 => 0.9f, 2 => 0.7f, _ => 0.4f };

        /// <summary>
        /// O risco de a ferida infeccionar no sorteio do dia <paramref name="dia"/> (1 a 3), pelas horas que ela ficou suja
        /// desde o último sorteio. Dia 1: 3% + 77% × (horas suja / 6); dia 2: 2% + 38% × (horas / 12); dia 3: 15% × (horas /
        /// 12). A fratura exposta: 15% + 80%, 8% + 52%, 3% + 27% (o osso para fora).
        /// </summary>
        public static float RiscoDeInfeccao(int dia, float horasSujas, bool exposta)
        {
            float d1 = Mathf.Clamp01(horasSujas / 6f), d2 = Mathf.Clamp01(horasSujas / 12f);
            if (exposta)
                return dia == 1 ? 0.15f + 0.80f * d1 : dia == 2 ? 0.08f + 0.52f * d2 : 0.03f + 0.27f * d2;
            return dia == 1 ? 0.03f + 0.77f * d1 : dia == 2 ? 0.02f + 0.38f * d2 : 0.15f * d2;
        }

        const float SepseTiraPorMinuto = 100f / (12 * 60f);   // a sepse: a vida cheia acaba em 12 min reais

        // no anfitrião, no AndarOCorpo: a fase da infecção anda, a sepse tira vida, a dose e o xarope passam
        void AndarAInfeccao(float min)
        {
            if (_doseFalta > 0f) _doseFalta = Mathf.Max(0f, _doseFalta - min);
            if (_xarope > 0f)
            {
                _xarope -= min;
                if (_xarope <= 0f)
                {
                    _xarope = 0f;
                    _corpoMudou = true;
                    if (_infeccao >= 2) AvisarAoDono("O xarope passou: a febre voltou");
                }
            }
            if (_infeccao <= 0) return;
            if (_infeccao == 3)
            {
                _sepse += min * SepseTiraPorMinuto;
                if (_sepse >= 1f)
                {
                    float d = Mathf.Floor(_sepse);
                    _sepse -= d;
                    Perder(d, "Morreu de infecção.");
                    if (Caido.Value) return;
                }
            }
            _infeccaoFalta -= min;
            if (_infeccaoFalta > 0f) return;
            if (_infeccao >= 3)
            {
                Perder(Vida.Value + 1f, "Morreu de infecção.");
                return;
            }
            _infeccao++;
            _infeccaoFalta = MinutosDaFase(_infeccao);
            _corpoMudou = true;
            AvisarAoDono(_infeccao == 2 ? "Você está com febre: a infecção piorou. Tome antibiótico"
                                        : "Sepse: a infecção tomou o corpo. Antibiótico, agora");
        }

        // o amanhecer no corpo: o sorteio de cada ferida, e a proteção do curativo acaba (troque de novo)
        void Amanhecer(bool forcar)
        {
            _desdeOAmanhecer = 0f;
            _amanheceres++;
            _inventario?.GanharXp(Habilidades.XpDoDia, "mais um dia vivo");   // (06/11/2026, a árvore de habilidades)
            if (_semente == 0) _semente = (uint)Random.Range(1, int.MaxValue);
            bool tinhaProtecao = false;
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                int b = 1 << p;
                bool aberta = _feridaFalta[p] > 0f || (_sangra & b) != 0;
                if (!aberta) continue;
                tinhaProtecao |= (_protegida & b) != 0;
                if ((_coberta & b) != 0 || _dias[p] >= 3 || (!forcar && _idade[p] < 12f)) continue;
                int dia = ++_dias[p];
                if (_infeccao == 0)
                {
                    float risco = RiscoDeInfeccao(dia, _sujas[p], _lesao[p] == Lesao.Exposta);
                    if (Habilidades.Tem(_rede, Habilidade.Imunidade)) risco *= 0.6f;   // (a Imunidade, 06/11/2026)
                    float r = Sorteio.FracaoDoHash(Sorteio.Hash((int)_semente, _amanheceres, p, dia));
                    if (r < risco) ComecarInfeccao((ParteDoCorpo)p);
                }
                _sujas[p] = 0f;
            }
            _protegida = 0;
            _limpa = 0;   // a limpeza de ontem não vale para a bandagem de hoje
            _corpoMudou = true;
            if (tinhaProtecao && _infeccao == 0) AvisarAoDono("Amanheceu: troque os curativos (a proteção do antisséptico acabou)");
        }

        void ComecarInfeccao(ParteDoCorpo parte)
        {
            _infeccao = 1;
            _origem = (int)parte + 1;
            _infeccaoFalta = MinutosDaFase(1);
            _sepse = 0f;
            _corpoMudou = true;
            AvisarAoDono($"A ferida {PartesDoCorpo.NaParte(parte)} infeccionou: tome antibiótico");
        }

        // no anfitrião, no Tratar: a dose do antibiótico
        string TomarAntibiotico()
        {
            if (_infeccao <= 0) return null;
            if (Random.value < ChanceDoAntibiotico(_infeccao))
            {
                _infeccao = 0;
                _origem = 0;
                _infeccaoFalta = _sepse = _doseFalta = 0f;
                // o antibiótico cobre as feridas abertas: elas não infeccionam mais
                for (int p = 0; p < PartesDoCorpo.Quantas; p++)
                    if (_feridaFalta[p] > 0f || (_sangra & (1 << p)) != 0) _coberta |= (byte)(1 << p);
                return "O antibiótico fez efeito: a infecção vai passar";
            }
            _doseFalta = 6 * 60f;
            if (_infeccao == 3) Perder(10f, "Morreu de infecção.");
            return "O antibiótico não fez efeito: outra dose daqui a 6 h";
        }

        // ------------------------------------------------ os testes (F1) ------------------------------------------------

        /// <summary>No anfitrião (o F1): amanhece agora no corpo (o sorteio de toda ferida aberta, mesmo nova).</summary>
        public void AmanhecerAgora()
        {
            if (!IsServer || Caido.Value) return;
            Amanhecer(true);
            GravarCorpo();
        }

        /// <summary>No anfitrião (o F1): a infecção na fase (1 a 3), vinda da parte; 0 cura.</summary>
        public void InfeccaoDeTeste(int fase, ParteDoCorpo origem)
        {
            if (!IsServer || Caido.Value) return;
            if (fase <= 0)
            {
                _infeccao = 0;
                _origem = 0;
                _infeccaoFalta = _sepse = 0f;
            }
            else
            {
                if (_infeccao == 0) ComecarInfeccao(PartesDoCorpo.Valida(origem) ? origem : ParteDoCorpo.Tronco);
                _infeccao = Mathf.Clamp(fase, 1, 3);
                _infeccaoFalta = MinutosDaFase(_infeccao);
            }
            _doseFalta = 0f;
            GravarCorpo();
        }

        /// <summary>A infecção e a limpeza em texto (o F1 e o log dos testes).</summary>
        string ResumoDaInfeccao(in EstadoDoCorpo e)
        {
            var s = new System.Text.StringBuilder();
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                var pp = (ParteDoCorpo)p;
                if (!e.Ferida(pp)) continue;
                s.Append($" | ferida {PartesDoCorpo.NomeCurto(pp)}: {e.Idade(pp)} h, suja {e.Sujas(pp)} h, dia {e.Dias(pp)}");
                if (e.Protegida(pp)) s.Append(", protegida");
                if (e.Limpa(pp)) s.Append(", limpa");
                if (e.Coberta(pp)) s.Append(", coberta");
            }
            if (e.Infectado)
                s.Append($" | INFECÇÃO fase {e.infeccao} ({(e.infeccao == 1 ? "ferida" : e.infeccao == 2 ? "febre" : "sepse")}) de {PartesDoCorpo.NomeCurto(e.OrigemDaInfeccao)}, falta {e.infeccaoFalta} min");
            if (e.doseFalta > 0) s.Append($" | dose em {e.doseFalta} min");
            if (e.xarope > 0) s.Append($" | xarope {e.xarope} min");
            s.Append($" | amanheceres {e.amanheceres}, {e.desdeOAmanhecer} min desde o último");
            return s.ToString();
        }
    }
}
