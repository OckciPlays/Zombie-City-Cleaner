using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O sem cabeça que ainda anda (07/11/2026, a pedido: no tiro na cabeça, "alguns terem alguns minutos de respostas
    /// motoras, como andar sem rumo, até mesmo podendo dar algum hit (com uma possibilidade mais baixa), e desligando por
    /// completo como se fosse um corte na energia"). No anfitrião, o tiro que estoura a cabeça e mataria, às vezes
    /// (<see cref="AjustesDeZumbi.chanceDeAndarSemCabeca"/>), deixa ele de pé: <see cref="Estado.SemComando"/>, sem a cabeça
    /// (a malha sem ela, a do corpo que cai), sem voz, sem ver nem ouvir, desnorteado (passos curtos para lados
    /// diferentes, paradas girando no lugar) por uns 3 s (<see cref="AjustesDeZumbi.tempoSemCabeca"/>; a pedido em
    /// 07/11/2026, eram 5 a 14 s e "ficam muito tempo vivos"). Quem encostar na frente dele às vezes leva um golpe às cegas
    /// (<see cref="AjustesDeZumbi.chanceDoGolpeSemCabeca"/>). Acabou o tempo, ou levou mais um acerto, ele desliga de uma
    /// vez: o corpo cai mole, sem os músculos (<see cref="CorpoDeZumbi.Morte.Desligou"/>). O XP de quem o abateu vem no tiro
    /// na cabeça (ZombieManager.Acertar). Os outros jogadores veem pelo estado (o 11, nos 4 bits do estado na rede).
    /// O sem comando fica preso nele (<see cref="_semComando"/>) até cair: nada de fora muda o estado dele (o barulho, o
    /// chamado, o tiro que não mata). Antes, o tiro seguinte fazia ele ir investigar o barulho, deixava de ser "sem comando"
    /// e o corpo caía com a cabeça de volta.
    /// O Brutamonte e o Long não andam sem cabeça (o Brutamonte não perde a cabeça; o Long tem a agonia dele); nem quem já
    /// está sem uma perna, caído ou de pedra.
    /// </summary>
    public partial class Zumbi
    {
        float _semComandoAte;
        bool _semComando, _desligou;
        Transform _pescoco;

        /// <summary>Sem a cabeça e ainda de pé (andando sem rumo até desligar).</summary>
        public bool SemComando => _semComando && !Morto;

        /// <summary>Morreu desligando (o sem cabeça que andou): o corpo cai mole.</summary>
        internal bool Desligou => _desligou;

        /// <summary>No anfitrião, no acerto que estoura a cabeça e mataria: ele fica andando sem ela?</summary>
        bool AndarSemCabeca(bool semCabeca, bool arremesso)
        {
            if (!semCabeca || arremesso || _semComando || _m == null) return false;
            if (Pedra >= 0.5f || _boneco != null || Deitado || FaseDoEquilibrio != Fase.Nenhuma || _pernas != 0) return false;
            if (tipo == TipoDeZumbi.Brutamonte || tipo == TipoDeZumbi.Long || !NoNavMesh) return false;
            return Random.value < _m.ajustes.chanceDeAndarSemCabeca;
        }

        void EntrarSemComando(float agora)
        {
            var a = _m.ajustes;
            SemCabeca = true;
            _semComando = true;
            Atual = Estado.SemComando;
            _semComandoAte = agora + Random.Range(a.tempoSemCabeca.x, a.tempoSemCabeca.y);
            _alvo = null;
            _golpeEm = 0f;
            _engajadoAte = 0f;
            _proximoCaminho = 0f;
            _proximoGolpe = agora + 0.8f;
            if (agente.enabled && agente.isOnNavMesh) agente.ResetPath();
            MostrarSemComando(true);
        }

        /// <summary>No anfitrião, a cada quadro (de todos os vivos): o tempo dele acabou, desliga.</summary>
        bool VigiarSemComando(float agora)
        {
            if (!_semComando) return false;
            if (agora >= _semComandoAte) Desligar(agora);
            return true;
        }

        /// <summary>
        /// No anfitrião: desnorteado, como quem não sabe para onde ir: passos curtos, cada um virando bastante para um lado
        /// (até 150°), num ritmo que muda; às vezes para e gira no lugar. E às vezes bate em quem encostar na frente.
        /// </summary>
        void PensarSemComando(float agora, AjustesDeZumbi a)
        {
            if (_golpeEm > 0f && agora >= _golpeEm)
            {
                Acertar(a);
                _alvo = null;
            }
            if (agora >= _proximoCaminho)
            {
                _proximoCaminho = agora + Random.Range(0.35f, 0.8f);
                if (Random.value < 0.25f)
                {
                    // para, perdido, e vira para um lado qualquer
                    if (agente.enabled && agente.isOnNavMesh) agente.ResetPath();
                    Vector2 r = Random.insideUnitCircle.normalized;
                    OlharPara(transform.position + new Vector3(r.x, 0f, r.y));
                }
                else
                    for (int t = 0; t < 3; t++)
                    {
                        Vector3 rumo = Quaternion.Euler(0f, Random.Range(-150f, 150f), 0f) * transform.forward;
                        Vector3 p = transform.position + rumo * Random.Range(0.6f, 1.8f);
                        if (NavMesh.SamplePosition(p, out var hit, 0.8f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - transform.position.y) < 1f)
                        {
                            Ir(hit.position, a.velocidadeVagando * Random.Range(0.4f, 1.05f), 0.1f);
                            break;
                        }
                    }
            }
            if (agora >= _proximoGolpe && _golpeEm <= 0f)
            {
                _proximoGolpe = agora + a.intervaloDoGolpe;
                for (int i = 0; i < _m.QuantosAlvos; i++)
                {
                    ref readonly var p = ref _m.AlvoNo(i);
                    Vector3 d = p.pes - transform.position;
                    if (new Vector2(d.x, d.z).magnitude > a.alcanceDoAtaque || Mathf.Abs(d.y) > 1.2f) continue;
                    if (Random.value >= a.chanceDoGolpeSemCabeca) break;
                    // às cegas: vira para quem esbarrou e bate
                    _alvo = p.quem;
                    OlharPara(p.pes);
                    _golpeEm = agora + a.atrasoDoDano;
                    Golpes++;
                    MostrarGolpe();
                    break;
                }
            }
        }

        /// <summary>O corte na energia: morre ali mesmo, e o corpo cai mole.</summary>
        void Desligar(float agora) => Morrer(agora, true, transform.position + Vector3.up, -transform.forward, false);

        /// <summary>Em todas as máquinas: a malha sem a cabeça, sem voz, sem a hitbox da cabeça; com efeito, ela estoura.</summary>
        void MostrarSemComando(bool comEfeito)
        {
            _semComando = true;
            var pele = renderers.Length > 0 ? renderers[0] as SkinnedMeshRenderer : null;
            var semCabeca = MalhaSemCabecaViva;
            if (pele != null && semCabeca != null)
            {
                GarantirMateriais();
                pele.sharedMesh = Membros.Variante(semCabeca, esqueleto, Cortes);
                pele.sharedMaterials = _peleECarne;
            }
            AtualizarColisores();
            if (voz != null) voz.Stop();
            if (!comEfeito || _m == null) return;
            if (partes != null && partes.Length > CorpoDeZumbi.ParteCabeca) Sangue.Explosao(partes[CorpoDeZumbi.ParteCabeca].position);
            if (_pescoco == null)
                foreach (var t in esqueleto)
                    if (t.name == "neck") { _pescoco = t; break; }
            if (_pescoco != null) Sangue.Jorro(_pescoco, _pescoco.up, 5f);
        }

        /// <summary>A malha sem a cabeça (a do corpo que cai, com a tampa de carne no pescoço), para o sem cabeça de pé.</summary>
        Mesh MalhaSemCabecaViva => _m != null && _m.corpos != null && modelo >= 0 && modelo < _m.corpos.Length && _m.corpos[modelo] != null
            ? _m.corpos[modelo].malhaSemCabeca : null;
    }
}
