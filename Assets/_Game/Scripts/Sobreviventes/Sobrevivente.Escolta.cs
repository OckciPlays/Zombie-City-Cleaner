using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// O aliado (24/11/2026, o Capitão da primeira missão depois dos 5 anos: "retomar a cidade com o npc capitão (junto com
    /// os colegas)"): um sobrevivente de um grupo aliado (<see cref="GrupoDeSobreviventes.aliado"/>), que nunca briga com os
    /// jogadores. No anfitrião, com a <see cref="escolta"/>, segue quem pediu "Venha comigo" (<see cref="lider"/>; caído ou
    /// fora, o jogador de pé mais perto). Desde 25/11/2026, a pedido do Pedro ("ele para de nós seguir durante a missão...
    /// ele está um pouco lento também em relação a corrida do jogador, fica muito para trás e em muitos momentos ele prefere
    /// ficar atirando nos zumbis do que seguir o jogador e acaba se perdendo, o correto seria: Se estamos parado e tem zumbi
    /// por perto, ele atira até uma certa distancia (por exemplo uns 30metros)... a partir dessa distancia, ele começaria a
    /// seguir a gente correndo ao invés de atirar nos zumbis"):
    ///  - <b>até 30 m</b> de quem ele segue, atira no zumbi que ele vê (o atrás da parede não o segura mais) e, se ficou a
    ///    mais de 8 m, vai chegando perto atirando;
    ///  - <b>a mais de 30 m</b>, para de atirar e corre até ele, a 6,5 m/s (o jogador corre a 4,2);
    ///  - sem zumbi, fica a uns 3 a 6 m dele, andando ou correndo para chegar;
    ///  - <b>preso</b> (parado 3,5 s querendo andar, ou sem caminho) <b>ou a mais de 45 m</b>, aparece uns 7 m atrás de quem
    ///    ele segue, num ponto do NavMesh que ninguém vê (antes, só com 70 m e à vista);
    ///  - quem ele segue num carro: espera, e atira nos zumbis perto; na caminhonete da família (<see cref="carona"/>), corre
    ///    até ela e sobe na caçamba (Sobrevivente.Cacamba.cs, pelo LakewoodNaCena);
    ///  - sem a escolta, espera no posto dele (o pátio do bunker), olhando para quem chega; longe do posto (a mais de 60 m,
    ///    depois da missão), volta para ele quando ninguém vê, em vez de andar até o NavMesh acabar.
    /// </summary>
    public partial class Sobrevivente
    {
        /// <summary>No anfitrião: seguindo os jogadores.</summary>
        internal bool escolta;
        /// <summary>No anfitrião: quem pediu "Venha comigo" (25/11/2026: ele segue esse; sem ele, o jogador mais perto).</summary>
        internal JogadorRede lider;
        /// <summary>No anfitrião: o carro em que ele sobe (a caminhonete da família; o LakewoodNaCena põe).</summary>
        internal Carro carona;

        /// <summary>Até onde de quem ele segue o Capitão ainda atira nos zumbis (m); mais longe, corre.</summary>
        public const float DistanciaDeAtirar = 30f;
        /// <summary>A corrida para alcançar (m/s; o jogador corre a 4,2).</summary>
        public const float CorridaDeAlcance = 6.5f;
        /// <summary>A partir daqui (m), ou preso, ele aparece atrás de quem segue, onde ninguém vê.</summary>
        public const float DistanciaDeAparecer = 45f;

        bool Aliado => grupo != null && grupo.aliado;

        /// <summary>
        /// No anfitrião: quem está conversando com ele, e até quando (25/11/2026: parado e olhando para quem conversa, no
        /// máximo 2 min, se a tela fechou sem avisar; antes, a escolta seguia andando e a conversa fechava pela distância).
        /// (25/11/2026, a revisão: o bug 78) Todos os que conversam, e não só o último: com dois na mesma conversa, quem
        /// abriu por último e fechou primeiro soltava o Capitão (ou o Gus), que voltava à escolta ou ao posto no meio da
        /// conversa do outro, e a tela dele fechava sozinha pela distância.
        /// </summary>
        readonly Dictionary<JogadorRede, float> _conversas = new Dictionary<JogadorRede, float>();
        static readonly List<JogadorRede> _conversasParaTirar = new List<JogadorRede>();

        /// <summary>A conversa dura no máximo isto (s), se a tela de alguém fechou sem avisar.</summary>
        const float ConversaMaisLonga = 120f;
        /// <summary>Mais longe que isto (m), quem conversava saiu da conversa (a tela fecha a 4,5 m; isto é a folga, se o aviso não chegou).</summary>
        const float LongeDaConversa = 8f;

        /// <summary>No anfitrião: quem conversa com ele (o mais perto, se são vários), ou null.</summary>
        internal JogadorRede conversandoCom => QuemConversa();

        /// <summary>No anfitrião: a tela de conversa de <paramref name="quem"/> abriu (ele fica parado, olhando para quem fala).</summary>
        internal void ComecarAConversa(JogadorRede quem)
        {
            if (quem != null) _conversas[quem] = Time.time + ConversaMaisLonga;
        }

        /// <summary>No anfitrião: a tela de conversa de <paramref name="quem"/> fechou (ele só volta a andar quando a última fecha).</summary>
        internal void AcabarAConversa(JogadorRede quem)
        {
            if (quem != null) _conversas.Remove(quem);
        }

        // o mais perto dos que ainda conversam; sai da conta quem saiu da partida, se afastou ou passou dos 2 min
        JogadorRede QuemConversa()
        {
            if (_conversas.Count == 0) return null;
            JogadorRede perto = null;
            float melhor = float.MaxValue, agora = Time.time;
            _conversasParaTirar.Clear();
            foreach (var kv in _conversas)
            {
                var j = kv.Key;
                float d = j != null && j.IsSpawned ? Horizontal(j.transform.position - transform.position) : float.MaxValue;
                if (d > LongeDaConversa || agora >= kv.Value)
                {
                    _conversasParaTirar.Add(j);
                    continue;
                }
                if (d < melhor)
                {
                    melhor = d;
                    perto = j;
                }
            }
            foreach (var j in _conversasParaTirar) _conversas.Remove(j);
            _conversasParaTirar.Clear();
            return perto;
        }

        float _presoDesde = -1f, _proximoAparecer, _proximoEsperem;

        void PensarComoAliado(float agora)
        {
            var quem = QuemConversa();
            if (quem != null)
            {
                Parar();
                OlharPara(quem.transform.position);
                return;
            }
            if (escolta) Escoltar(agora);
            else EsperarNoPosto(agora);
        }

        /// <summary>No anfitrião: quem ele segue (o líder de pé; senão, o jogador de pé mais perto; senão, qualquer um).</summary>
        internal JogadorRede QuemEleSegue(out float dist)
        {
            JogadorRede alvo = null;
            if (lider != null && lider.IsSpawned && lider.Criado && lider.AlvoValido) alvo = lider;
            if (alvo == null) alvo = JogadorMaisPerto(true, out _);
            if (alvo == null && lider != null && lider.IsSpawned && lider.Criado) alvo = lider;
            if (alvo == null) alvo = JogadorMaisPerto(false, out _);
            dist = alvo != null ? Horizontal(alvo.transform.position - transform.position) : float.MaxValue;
            return alvo;
        }

        JogadorRede JogadorMaisPerto(bool dePe, out float dist)
        {
            JogadorRede alvo = null;
            float melhor = float.MaxValue;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.IsSpawned || !j.Criado || (dePe && !j.AlvoValido)) continue;
                float d = (j.transform.position - transform.position).sqrMagnitude;
                if (d < melhor) { melhor = d; alvo = j; }
            }
            dist = alvo != null ? Mathf.Sqrt(melhor) : float.MaxValue;
            return alvo;
        }

        static float Horizontal(Vector3 d) => new Vector2(d.x, d.z).magnitude;

        static Carro CarroDe(JogadorRede j) => j != null && j.Passageiro != null && j.Passageiro.NoCarro ? j.Passageiro.Carro : null;

        /// <summary>
        /// Nenhum jogador vê o ponto (para ele aparecer ou sumir sem ninguém ver): todos a mais de <paramref name="perto"/> m e
        /// com ele fora da frente (mais de 80° do olhar) ou atrás de uma parede. (25/11/2026: o lugar de aparecer, atrás de
        /// quem ele segue, fica a uns 7 m: lá, só a frente conta.)
        /// </summary>
        internal static bool NinguemVe(Vector3 ponto, float perto = 14f)
        {
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.IsSpawned || !j.Criado) continue;
                // (o olhar de verdade só do jogador desta máquina; os outros, pelo corpo)
                var o = j.IsOwner ? j.Olho : null;
                Vector3 olho = o != null ? o.position : j.transform.position + Vector3.up * 1.6f;
                Vector3 para = ponto - olho;
                if (para.magnitude < perto) return false;
                Vector3 frente = o != null ? o.forward : j.transform.forward;
                if (Vector3.Angle(frente, para) > 80f) continue;
                if (Physics.Linecast(olho, ponto, MascaraDoCenario, QueryTriggerInteraction.Ignore)) continue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// No anfitrião, antes de tudo (o Sobrevivente.Pensar): o aliado na escolta que ficou a mais de 45 m de quem segue (o
        /// grupo foi de carro, ou correu na frente), ou preso, aparece uns 7 m atrás dele, no NavMesh de lá, onde ninguém vê.
        /// Longe dos jogadores ele fica sem o NavMesh embaixo (o agente desliga) e não chegaria a pensar na escolta.
        /// Verdadeiro se ele foi.
        /// </summary>
        bool AlcancarOsJogadores()
        {
            if (!Aliado || !escolta || agente == null || NaCacamba || Time.time < _proximoAparecer) return false;
            // (25/11/2026, a revisão: o bug 78) com alguém conversando com ele, fica (quem ele segue pode estar a mais de 90 m,
            // e ele sumia no meio da conversa)
            if (QuemConversa() != null) return false;
            var alvo = QuemEleSegue(out float dist);
            if (alvo == null) return false;
            var carro = CarroDe(alvo);
            // num carro (não a caminhonete): espera ele descer; na caminhonete, quem sobe é o LakewoodNaCena
            if (carro != null) return false;
            bool preso = _presoDesde >= 0f && Time.time - _presoDesde > 3.5f;
            if (dist <= DistanciaDeAparecer && !preso) return false;
            _proximoAparecer = Time.time + 0.75f;
            // (de onde ele some: longe de todos, ou fora da vista)
            if (dist < 90f && !NinguemVe(transform.position + Vector3.up * 1.2f)) return false;
            if (!LugarAtrasDe(alvo, out var ponto)) return false;
            if (agente.enabled) agente.Warp(ponto);
            else
            {
                transform.position = ponto;
                LigarOAgente();
            }
            _presoDesde = -1f;
            return true;
        }

        // uns 7 m atrás de quem ele segue (ou dos lados), no NavMesh, onde ninguém vê
        static bool LugarAtrasDe(JogadorRede alvo, out Vector3 ponto)
        {
            var t = alvo.transform;
            float[] giros = { 180f, 145f, 215f, 110f, 250f };
            foreach (var g in giros)
            foreach (var d in new[] { 7f, 10f, 5f })
            {
                var p = t.position + Quaternion.Euler(0f, g, 0f) * t.forward * d;
                if (!NavMesh.SamplePosition(p, out var hit, 2.5f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - t.position.y) > 3f) continue;
                if (!NinguemVe(hit.position + Vector3.up * 1.2f, 3f)) continue;
                ponto = hit.position;
                return true;
            }
            ponto = default;
            return false;
        }

        // um zumbi que ameaça e que ele vê (o atrás da parede não o segura)
        bool VejoOZumbi(out Vector3 mira)
        {
            mira = default;
            if (_zumbi == null || !ZumbiAmeaca(_zumbi)) return false;
            mira = _zumbi.transform.position + Vector3.up * 1.35f;
            return !Physics.Linecast(Olho, mira, MascaraDoCenario, QueryTriggerInteraction.Ignore);
        }

        // segue quem pediu (a regra dos 30 m)
        void Escoltar(float agora)
        {
            var alvo = QuemEleSegue(out float dist);
            bool zumbi = VejoOZumbi(out var mira);
            if (alvo == null)
            {
                Parar();
                if (zumbi) AtirarNo(null, mira, agora);
                return;
            }
            var carro = CarroDe(alvo);
            if (carro != null)
            {
                // na caminhonete da família: corre até ela (sobe na caçamba perto dela: o LakewoodNaCena)
                if (carro == carona && !NaCacamba)
                {
                    Andar(carro.transform.position, CorridaDeAlcance, 2.5f, agora);
                    return;
                }
                // noutro carro: espera ele descer, cuidando dos zumbis
                Parar();
                _presoDesde = -1f;
                if (zumbi) AtirarNo(null, mira, agora);
                else OlharPara(alvo.transform.position);
                return;
            }
            if (dist > DistanciaDeAtirar)
            {
                // longe: corre até ele, sem atirar
                if (agora >= _proximoEsperem && dist > 38f)
                {
                    _proximoEsperem = agora + 12f;
                    Falar(Frase.Esperem);
                }
                Andar(alvo.transform.position, CorridaDeAlcance, 3.5f, agora);
                return;
            }
            if (zumbi)
            {
                // o zumbi colado: recua atirando (como os outros); senão atira e, longe de quem segue, vai chegando
                if ((_zumbi.transform.position - transform.position).sqrMagnitude < 3f * 3f)
                {
                    LutarComZumbi(agora);
                    return;
                }
                AtirarNo(null, mira, agora);
                if (dist > 8f) Andar(alvo.transform.position, 3.2f, 4f, agora);
                else
                {
                    Parar();
                    _presoDesde = -1f;
                }
                return;
            }
            if (dist <= 6f)
            {
                Parar();
                _presoDesde = -1f;
                OlharPara(alvo.transform.position);
                return;
            }
            Andar(alvo.transform.position, dist > 14f ? 5.2f : 2.4f, 3.5f, agora);
        }

        // vai até o ponto e mede se está preso (parado querendo andar, ou sem caminho inteiro)
        void Andar(Vector3 ponto, float velocidade, float parada, float agora)
        {
            Ir(ponto, velocidade, parada);
            bool longe = Horizontal(ponto - transform.position) > parada + 1.5f;
            bool semCaminho = agente.isOnNavMesh && !agente.pathPending && agente.hasPath && agente.pathStatus != NavMeshPathStatus.PathComplete;
            bool parado = agente.velocity.sqrMagnitude < 0.3f * 0.3f && !agente.pathPending;
            if (longe && (parado || semCaminho))
            {
                if (_presoDesde < 0f) _presoDesde = agora;
            }
            else _presoDesde = -1f;
        }

        void EsperarNoPosto(float agora)
        {
            _presoDesde = -1f;
            float longe = Horizontal(posto - transform.position);
            // longe do posto (a missão acabou noutro lugar): volta quando ninguém vê, de onde está e de onde chega
            if (longe > 60f)
            {
                Parar();
                if (agora >= _proximoAparecer)
                {
                    _proximoAparecer = agora + 1f;
                    if (NinguemVe(transform.position + Vector3.up * 1.2f) && NinguemVe(posto + Vector3.up * 1.2f)) VoltarAoPosto();
                }
                return;
            }
            if (longe > 2.5f)
            {
                Ir(posto, longe > 14f ? 4.2f : 2.2f, 1f);
                return;
            }
            Parar();
            JogadorRede perto = null;
            float melhor = 12f * 12f;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.Criado) continue;
                float d = (j.transform.position - transform.position).sqrMagnitude;
                if (d < melhor) { melhor = d; perto = j; }
            }
            if (perto != null) OlharPara(perto.transform.position);
            else transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, giroDoPosto, 0f), Time.deltaTime * 2f);
        }

        /// <summary>No anfitrião: direto para o posto (sem o NavMesh lá, ele fica parado até o NavMesh chegar).</summary>
        internal void VoltarAoPosto()
        {
            if (NaCacamba) _cacamba.Value = 0;
            if (agente != null && agente.enabled)
            {
                if (NavMesh.SamplePosition(posto, out var hit, 3f, NavMesh.AllAreas))
                {
                    agente.Warp(hit.position);
                    transform.rotation = Quaternion.Euler(0f, giroDoPosto, 0f);
                    return;
                }
                agente.enabled = false;
            }
            transform.SetPositionAndRotation(posto, Quaternion.Euler(0f, giroDoPosto, 0f));
            LigarOAgente();
        }

        void OlharPara(Vector3 p)
        {
            var d = p - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 4f);
        }
    }
}
