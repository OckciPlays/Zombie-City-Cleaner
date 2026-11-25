using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// O aliado (24/11/2026, o Capitão da primeira missão depois dos 5 anos: "retomar a cidade com o npc capitão (junto com
    /// os colegas)"): um sobrevivente de um grupo aliado (<see cref="GrupoDeSobreviventes.aliado"/>), que nunca briga com os
    /// jogadores. No anfitrião: com a <see cref="escolta"/>, segue o jogador mais perto (anda até uns 4 m dele, corre se
    /// ficou para trás e, se o grupo foi de carro e ele ficou a mais de 70 m, aparece perto de quem está na frente, no
    /// NavMesh de lá: <see cref="AlcancarOsJogadores"/>); sem ela, espera no posto dele (o pátio do bunker), olhando para
    /// quem chega. Os zumbis perto, ele combate como os outros (o LutarComZumbi, antes disto).
    /// </summary>
    public partial class Sobrevivente
    {
        /// <summary>No anfitrião: seguindo os jogadores.</summary>
        internal bool escolta;

        bool Aliado => grupo != null && grupo.aliado;

        void PensarComoAliado(float agora)
        {
            if (escolta) Escoltar(agora);
            else EsperarNoPosto();
        }

        JogadorRede JogadorMaisPerto(out float dist)
        {
            JogadorRede alvo = null;
            float melhor = float.MaxValue;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.Criado) continue;
                float d = (j.transform.position - transform.position).sqrMagnitude;
                if (d < melhor) { melhor = d; alvo = j; }
            }
            dist = alvo != null ? Mathf.Sqrt(melhor) : float.MaxValue;
            return alvo;
        }

        /// <summary>
        /// No anfitrião, antes de tudo (o Sobrevivente.Pensar): o aliado na escolta que ficou a mais de 70 m de todos (o grupo
        /// foi de carro, ou correu na frente) aparece 5 m atrás do jogador mais perto, no NavMesh de lá. Longe dos jogadores
        /// ele fica sem o NavMesh embaixo (o agente desliga) e não chegaria a pensar na escolta. Verdadeiro se ele foi.
        /// </summary>
        bool AlcancarOsJogadores()
        {
            if (!Aliado || !escolta || agente == null) return false;
            var alvo = JogadorMaisPerto(out float dist);
            if (alvo == null || dist <= 70f) return false;
            var atras = alvo.transform.position - alvo.transform.forward * 5f;
            if (!NavMesh.SamplePosition(atras, out var hit, 6f, NavMesh.AllAreas)) return false;   // (lá ainda sem o NavMesh)
            if (agente.enabled) agente.Warp(hit.position);
            else
            {
                transform.position = hit.position;
                LigarOAgente();
            }
            return true;
        }

        // segue o jogador mais perto (o vivo e no chão)
        void Escoltar(float agora)
        {
            var alvo = JogadorMaisPerto(out float dist);
            if (alvo == null)
            {
                Parar();
                return;
            }
            if (dist < 4f)
            {
                Parar();
                OlharPara(alvo.transform.position);
                return;
            }
            Ir(alvo.transform.position, dist > 12f ? 5.2f : 2.2f, 3f);
        }

        void EsperarNoPosto()
        {
            if ((transform.position - posto).sqrMagnitude > 2.5f * 2.5f)
            {
                Ir(posto, 2.2f, 1f);
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

        void OlharPara(Vector3 p)
        {
            var d = p - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 4f);
        }
    }
}
