using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// A horda que cerca (a pedido em 28/10/2026: antes todos iam para o mesmo ponto, o jogador, e o NavMesh os punha em
    /// fila, um atrás do outro, no mesmo caminho). O ZombieManager (ZombieManager.Cerco.cs) dá a cada perseguidor um lugar
    /// em volta do alvo (um ângulo); ele vai para o lugar dele: de longe, um ponto ao lado do jogador, do lado do lugar
    /// (eles se abrem em leque); de perto (7 m), o círculo de ataque. Cada um tem o seu ritmo (a velocidade varia um pouco)
    /// e a sua prioridade de desvio, para não andarem em bloco.
    /// Também a ameaça (a pedido em 28/10/2026): de dia e ao amanhecer, ele prioriza se esconder; só sai atrás do jogador
    /// no sol se foi atacado (tiro, soco, a batida do carro) ou com um tiro bem perto, por 20 s.
    /// </summary>
    public partial class Zumbi
    {
        // o círculo em volta do alvo (o golpe alcança 1,3 m). A 0,9 m, parando 0,4 m antes, eles encostavam na cápsula do
        // jogador e o empurravam (17 m no teste): agora 1,1 m, parando a 15 cm
        const float RaioDoCerco = 1.1f;
        const float ParadaNoCerco = 0.15f;
        // dando a volta para chegar ao lugar do outro lado: um anel por fora (sem atravessar o jogador), 70° por vez
        const float RaioDaVolta = 1.8f;
        const float PassoDaVolta = 70f * Mathf.Deg2Rad;
        const float ComecaACercar = 7f;        // mais perto que isto, cada um vai para o seu lugar no círculo
        const float TempoAmeacado = 20f;

        float _anguloNoCerco, _cercoAte, _ameacadoAte, _feridoAte, _ritmo = 1f, _noAlcanceDesde = -1f, _presoDesde = -1f, _semDesvioAte;
        bool _temCerco;

        /// <summary>Quem ele persegue (para o cerco).</summary>
        internal JogadorRede Alvo => _alvo;

        /// <summary>Foi atacado (ou ouviu um tiro bem perto) há pouco: sai atrás do jogador mesmo no sol.</summary>
        internal bool Ameacado => Time.time < _ameacadoAte;

        internal void Ameacar(float agora) => _ameacadoAte = agora + TempoAmeacado;

        /// <summary>Levou tiro, soco ou a batida do carro há pouco: continua atrás do jogador mesmo virando pedra no sol.</summary>
        internal bool Ferido => Time.time < _feridoAte;

        internal void Ferir(float agora)
        {
            _feridoAte = agora + TempoAmeacado;
            Ameacar(agora);
        }

        /// <summary>
        /// Preso no caminho (parado, com caminho, longe do destino) por mais de 1,2 s: passa 2,5 s sem desviar dos outros
        /// zumbis (atravessa). Dois na mesma porta, cada um desviando do outro, travavam ali (a pedido em 28/10/2026).
        /// </summary>
        void Destravar(float agora)
        {
            if (_semDesvioAte > 0f && agora >= _semDesvioAte)
            {
                _semDesvioAte = 0f;
                agente.obstacleAvoidanceType = NivelAtual == Nivel.Perto ? ObstacleAvoidanceType.MedQualityObstacleAvoidance : ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            }
            bool indo = agente.hasPath && !agente.pathPending && agente.remainingDistance > agente.stoppingDistance + 0.4f;
            if (!indo || agente.velocity.sqrMagnitude > 0.04f)
            {
                _presoDesde = -1f;
                return;
            }
            if (_presoDesde < 0f) _presoDesde = agora;
            else if (agora - _presoDesde > 1.2f && _semDesvioAte <= 0f)
            {
                _semDesvioAte = agora + 2.5f;
                agente.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                _presoDesde = -1f;
            }
        }

        /// <summary>De dia ou amanhecendo, sem ter sido atacado, não vai atrás de quem está onde o sol bate (ou vai bater).</summary>
        bool DesisteDoSol(Vector3 alvo) => _m.ProtegerDoSol && !Ameacado && _m.Exposto(alvo);

        /// <summary>O lugar dele em volta do alvo (um ângulo, no plano), dado pelo ZombieManager (vale 1 s; renovado a cada 0,3 s).</summary>
        internal void DefinirCerco(float angulo)
        {
            _anguloNoCerco = angulo;
            _temCerco = true;
            _cercoAte = Time.time + 1f;
        }

        /// <summary>O mesmo lugar por mais 1 s (o grupo não mudou).</summary>
        internal void RenovarCerco() => _cercoAte = Time.time + 1f;

        internal float AnguloNoCerco => _anguloNoCerco;

        // cada um no seu ritmo (a velocidade de 88% a 112%) e com a sua prioridade de desvio
        void SortearRitmo()
        {
            _ritmo = Random.Range(0.88f, 1.12f);
            agente.avoidancePriority = Random.Range(35, 66);
        }

        /// <summary>Já está no lugar dele do círculo (a menos de 0,8 m), ou não tem lugar: pode atacar.</summary>
        bool NoLugarDoCerco(Vector3 alvo)
        {
            if (!_temCerco || Time.time > _cercoAte) return true;
            Vector3 lugar = alvo + new Vector3(Mathf.Sin(_anguloNoCerco), 0f, Mathf.Cos(_anguloNoCerco)) * RaioDoCerco;
            Vector3 d = lugar - transform.position;
            d.y = 0f;
            return d.sqrMagnitude < 0.6f * 0.6f;
        }

        /// <summary>
        /// Para onde ele vai, perseguindo: o lugar dele no cerco (de longe, um ponto ao lado do alvo, do lado do lugar; de
        /// perto, o círculo em volta). Sem lugar, ou com o lugar fora do NavMesh ou atrás de uma parede (em outro cômodo),
        /// direto para o alvo.
        /// </summary>
        Vector3 DestinoNoCerco(Vector3 alvo, float distancia)
        {
            if (!_temCerco || Time.time > _cercoAte) return alvo;
            float angulo = _anguloNoCerco, raio;
            if (distancia > ComecaACercar) raio = Mathf.Clamp(distancia * 0.5f, 2.5f, 6f);
            else
            {
                // perto: se o lugar está do outro lado, dá a volta por fora (no máximo 70° por vez, num anel de 1,8 m), sem
                // atravessar o jogador; chegando ao lado do lugar, fecha no círculo
                Vector3 daqui = transform.position - alvo;
                float rumo = Mathf.Atan2(daqui.x, daqui.z);
                float falta = Mathf.DeltaAngle(rumo * Mathf.Rad2Deg, _anguloNoCerco * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                if (Mathf.Abs(falta) > 0.35f)
                {
                    angulo = rumo + Mathf.Clamp(falta, -PassoDaVolta, PassoDaVolta);
                    raio = RaioDaVolta;
                }
                else raio = RaioDoCerco;
            }
            Vector3 lugar = alvo + new Vector3(Mathf.Sin(angulo), 0f, Mathf.Cos(angulo)) * raio;
            if (!NavMesh.SamplePosition(lugar, out var hit, 1.2f, NavMesh.AllAreas)) return alvo;
            if (NavMesh.SamplePosition(alvo, out var noAlvo, 1.5f, NavMesh.AllAreas)
                && NavMesh.Raycast(noAlvo.position, hit.position, out _, NavMesh.AllAreas)) return alvo;
            return hit.position;
        }
    }
}
