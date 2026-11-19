using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// Atirar da janela (18/11/2026, a etapa 2 da proteção, a pedido: "atrás das janelas é algo que já devemos começar a
    /// fazer ... quero que eles comecem a atirar de dentro da janela"). No tiroteio, as janelas das casas perto (as que
    /// abrem, viradas para o inimigo) entram na procura da proteção como lugares bons: do lado de dentro, encostado na
    /// parede ao lado do vão. Dali ele espia inclinando pelo vão (a parede tapa o corpo) e se esconde atrás da parede; a
    /// janela fechada ele abre por dentro na hora de espiar (destrancando, se for o caso). A janela com a barricada de
    /// tábuas não abre e fica de fora.
    /// </summary>
    public partial class Sobrevivente
    {
        /// <summary>
        /// O que tapa na procura da proteção: o cenário e os carros. A porta e a janela não contam (ele as abre); a visão e o
        /// tiro de verdade contam com elas fechadas (<see cref="MascaraDoCenario"/>).
        /// </summary>
        const int MascaraDaProtecao = (1 << 0) | (1 << 11);

        /// <summary>Até onde ele procura uma janela (m, dele até ela).</summary>
        const float AlcanceDasJanelas = 30f;

        /// <summary>O quanto a janela vale a mais na nota da proteção (m de caminho: é um bom lugar).</summary>
        const float BonusDaJanela = 14f;

        static readonly List<JanelaDaCasa> _janelasPerto = new List<JanelaDaCasa>(16);
        static readonly List<JanelaDaCasa> _janelaDoCandidato = new List<JanelaDaCasa>(32);

        /// <summary>A janela da proteção de agora (null: a proteção não é uma janela).</summary>
        JanelaDaCasa _janelaDoPosto;
        float _proximaJanela;

        // (o diagnóstico da última procura: os lugares nas janelas, com chão, que tapam, com caminho, e com o espiar)
        static readonly int[] _janelaNasEtapas = new int[5];

        /// <summary>No anfitrião (os testes): na última procura de qualquer um, quantos lugares de janela passaram de cada etapa.</summary>
        public static string DiagnosticoDasJanelas =>
            $"{_janelaNasEtapas[0]} lugares, {_janelaNasEtapas[1]} com chão, {_janelaNasEtapas[2]} tapam, {_janelaNasEtapas[3]} com caminho, {_janelaNasEtapas[4]} com o espiar";

        /// <summary>No anfitrião (o F1 e os testes): a janela de onde ele atira agora (null: a proteção não é uma janela).</summary>
        public JanelaDaCasa JanelaDoPosto => _temProtecao ? _janelaDoPosto : null;

        /// <summary>
        /// Os lugares nas janelas (no começo de <see cref="_candidatos"/>, cada um com a sua em <see cref="_janelaDoCandidato"/>):
        /// para cada janela perto, virada para o inimigo, os dois lados do vão por dentro, a meio metro da parede. Devolve
        /// quantos.
        /// </summary>
        int CandidatosDasJanelas(Vector3 inimigo)
        {
            _janelaDoCandidato.Clear();
            var portas = PortasDoMundo.Instancia;
            if (portas == null) return 0;
            portas.JanelasPerto(transform.position, AlcanceDasJanelas, _janelasPerto);
            foreach (var j in _janelasPerto)
            {
                if (j == null || !PortasDoMundo.PodeAbrirPorDentro(j)) continue;
                Vector3 fora = j.transform.forward;
                fora.y = 0f;
                Vector3 paraOInimigo = inimigo - j.transform.position;
                paraOInimigo.y = 0f;
                if (fora.sqrMagnitude < 0.01f || paraOInimigo.sqrMagnitude < 9f) continue;
                fora.Normalize();
                if (Vector3.Dot(fora, paraOInimigo.normalized) < 0.3f) continue;   // virada para o outro lado
                Vector3 aoLongo = Vector3.Cross(Vector3.up, fora);
                float meia = j.dados.largura * 0.5f;
                foreach (float s in new[] { 1f, -1f })
                {
                    // o peitoril fica a uns 90 cm do chão: o ponto de busca, a uns 20 cm do chão de dentro
                    // (a 32 cm da borda do vão: o ombro fica atrás da parede; para espiar, ele dá meio passo e inclina)
                    var p = j.transform.position - fora * 0.5f + aoLongo * (s * (meia + 0.32f)) - Vector3.up * 0.7f;
                    if (!j.DoLadoDeDentro(p)) continue;
                    _candidatos.Add(p);
                    _janelaDoCandidato.Add(j);
                }
                if (_janelaDoCandidato.Count >= 16) break;
            }
            return _janelaDoCandidato.Count;
        }

        /// <summary>
        /// Na hora de espiar (a fase 2): a janela do posto, fechada, ele abre por dentro (de meio em meio segundo, se o
        /// jogador a fechar de novo).
        /// </summary>
        void AbrirAJanelaDoPosto(float agora)
        {
            if (_janelaDoPosto == null || _janelaDoPosto.Aberta || agora < _proximaJanela) return;
            _proximaJanela = agora + 0.5f;
            if ((transform.position - _protecao).sqrMagnitude > 1.5f * 1.5f) return;
            PortasDoMundo.Instancia?.AbrirPorDentro(_janelaDoPosto);
        }
    }
}
