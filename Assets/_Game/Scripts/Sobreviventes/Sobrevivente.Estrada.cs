using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// Os dois do carro quebrado na estrada (02/11/2026, a etapa 4; <see cref="CarrosQuebradosDoMundo"/>):
    ///  - <b>ajoelhado na roda</b> (em todas as máquinas, pelo <c>_ajoelhado</c> da rede): o "Sentado_Motorista" do
    ///    controlador da arma troca pelo clipe "Agachado" (um override por arma), o "Lugar" = 1 leva até ele, a camada dos
    ///    braços sai, a arma some da mão, e os braços vão para a roda por cima da animação (batendo, na troca);
    ///  - <b>a cabeça na estrada</b> (no anfitrião): quem trabalha (o mecânico; o vigia, se o mecânico morreu) fica
    ///    ajoelhado na roda; o vigia fica de pé, de olho no jogador mais perto, e levanta o rifle (sem atirar) para quem
    ///    chega armado. Hostis, os dois lutam como os grupos (Sobrevivente.Cabeca.cs).
    /// </summary>
    public partial class Sobrevivente
    {
        static readonly int LugarId = Animator.StringToHash("Lugar");
        // o controlador com o "Agachado" no lugar do "Sentado_Motorista", por controlador de arma
        static readonly Dictionary<RuntimeAnimatorController, AnimatorOverrideController> _comAgachado = new Dictionary<RuntimeAnimatorController, AnimatorOverrideController>();
        static bool _avisouSemAgachado;

        bool _querAjoelhar;
        float _pesoDaPose, _pesoDosBracos = 1f;
        Transform _bracoD, _anteD, _maoD, _bracoE, _anteE, _maoE;

        /// <summary>O mecânico e o vigia do carro quebrado (em qualquer máquina, pela identidade).</summary>
        public bool DaEstrada => Identidade.Value.papel == PapelDoSobrevivente.Mecanico || Identidade.Value.papel == PapelDoSobrevivente.Vigia;

        /// <summary>O carro quebrado deste (em qualquer máquina, pela identidade; -1 se não é da estrada).</summary>
        public int CenaNaRede => DaEstrada ? Identidade.Value.grupo - CarrosQuebradosDoMundo.GrupoBase : -1;

        public bool Ajoelhado => IsSpawned && _ajoelhado.Value;

        /// <summary>No anfitrião, antes do Spawn: já nasce ajoelhado na roda (de longe, antes do NavMesh ficar pronto).</summary>
        internal void NascerAjoelhado() => _ajoelhado.Value = true;

        void AoMudarAjoelhado(bool antes, bool depois) => MostrarArma();

        RuntimeAnimatorController ControladorAjoelhado(RuntimeAnimatorController daArma)
        {
            if (daArma == null) return null;
            if (agachado == null)
            {
                if (!_avisouSemAgachado) Debug.LogWarning("[Sobreviventes] O prefab não tem o clipe \"Agachado\" (Montar os sobreviventes): os do carro quebrado ficam de pé.");
                _avisouSemAgachado = true;
                return daArma;
            }
            if (_comAgachado.TryGetValue(daArma, out var pronto) && pronto != null) return pronto;
            var original = daArma is AnimatorOverrideController o ? o.runtimeAnimatorController : daArma;
            var novo = new AnimatorOverrideController(original) { name = daArma.name + " (ajoelhado)" };
            var trocas = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            if (daArma is AnimatorOverrideController o2) o2.GetOverrides(trocas);
            else novo.GetOverrides(trocas);
            bool achou = false;
            for (int i = 0; i < trocas.Count; i++)
                if (trocas[i].Key != null && trocas[i].Key.name == "Sentado_Motorista")
                {
                    trocas[i] = new KeyValuePair<AnimationClip, AnimationClip>(trocas[i].Key, agachado);
                    achou = true;
                }
            novo.ApplyOverrides(trocas);
            if (!achou) Debug.LogWarning("[Sobreviventes] O controlador não tem o \"Sentado_Motorista\": os do carro quebrado não ajoelham.");
            _comAgachado[daArma] = novo;
            return novo;
        }

        // a cada quadro, em todas as máquinas: o "Lugar" e a camada dos braços (some ajoelhado)
        void Ajoelhar()
        {
            if (!DaEstrada) return;
            bool ajoelhado = _ajoelhado.Value && !_caido;
            corpo.animador.SetInteger(LugarId, ajoelhado ? 1 : 0);
            _pesoDosBracos = Mathf.MoveTowards(_pesoDosBracos, ajoelhado ? 0f : 1f, Time.deltaTime * 4f);
            if (corpo.animador.layerCount > 1) corpo.animador.SetLayerWeight(1, _pesoDosBracos);
        }

        // os braços na roda, por cima da animação (batendo, durante a troca)
        void LateUpdate()
        {
            if (!IsSpawned || corpo == null || !DaEstrada) return;
            _pesoDaPose = Mathf.MoveTowards(_pesoDaPose, _ajoelhado.Value && !_caido ? 1f : 0f, Time.deltaTime * 3f);
            if (_pesoDaPose <= 0f) return;
            if (_maoD == null) AcharOsBracos();
            var f = transform.forward;
            var d = transform.right;
            var c = Vector3.up;
            var m = CarrosQuebradosDoMundo.Instancia;
            bool trocando = m != null && m.Trocando(CenaNaRede);
            float bate = trocando ? Mathf.Sin(Time.time * 8f + (Identidade.Value.semente & 15)) : 0f;
            // (a roda fica na frente e embaixo: o meio dela a uns 35 cm do chão, os ombros dele a 1 m)
            DiaZero.NpcDoDiaZero.Apontar(_bracoD, _anteD, f * 0.55f - c * 0.8f + d * 0.12f, _pesoDaPose);
            DiaZero.NpcDoDiaZero.Apontar(_anteD, _maoD, f * 0.85f - c * (0.35f - 0.5f * bate) - d * 0.2f, _pesoDaPose);
            DiaZero.NpcDoDiaZero.Apontar(_bracoE, _anteE, f * 0.55f - c * 0.8f - d * 0.12f, _pesoDaPose);
            DiaZero.NpcDoDiaZero.Apontar(_anteE, _maoE, f * 0.85f - c * 0.4f + d * 0.25f, _pesoDaPose);
        }

        void AcharOsBracos()
        {
            foreach (var t in corpo.GetComponentsInChildren<Transform>(true))
                switch (t.name)
                {
                    case "upper_arm.R": _bracoD = t; break;
                    case "forearm.R": _anteD = t; break;
                    case "hand.R": _maoD = t; break;
                    case "upper_arm.L": _bracoE = t; break;
                    case "forearm.L": _anteE = t; break;
                    case "hand.L": _maoE = t; break;
                }
        }

        // ------------------------------------------------------------------ no anfitrião: na estrada

        // quem trabalha fica ajoelhado na roda; o outro fica no posto, de olho em quem chega (vira para o jogador mais perto,
        // até 30 m; quem chega armado leva a mira, sem tiro)
        void FicarNaEstrada(float agora)
        {
            _temProtecao = false;
            var m = CarrosQuebradosDoMundo.Instancia;
            if (m == null) return;
            bool trabalha = m.Trabalhador(cena) == this;
            Vector3 alvo = posto;
            float giro = giroDoPosto;
            if (trabalha) m.LugarDoTrabalho(cena, out alvo, out giro);
            if ((transform.position - alvo).sqrMagnitude > 0.45f * 0.45f)
            {
                if (agora >= _proximoCaminho)
                {
                    _proximoCaminho = agora + 0.6f;
                    Ir(alvo, 1.6f, 0.15f);
                }
                return;
            }
            if (agente.hasPath) agente.ResetPath();
            if (trabalha)
            {
                _querAjoelhar = true;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, giro, 0f), 180f * Time.deltaTime);
                return;
            }
            var j = m.QuemVigiar(cena, transform.position, out bool apontar);
            if (j != null)
            {
                if (apontar)
                {
                    _mirandoEm = j.transform.position + Vector3.up * 1.3f;   // o Mirar vira o corpo; sem tiro
                    return;
                }
                Vector3 para = j.transform.position - transform.position;
                para.y = 0f;
                if (para.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(para), 150f * Time.deltaTime);
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, giro, 0f), 120f * Time.deltaTime);
        }
    }

    /// <summary>
    /// Os dois do carro quebrado na mira (de perto): E fala com eles; com um pneu bom na mochila (30% ou mais), E dá o pneu.
    /// Quem chega com a arma na mão ouve que é para guardar.
    /// </summary>
    public class CarroQuebradoNaMira : Interagivel
    {
        public Sobrevivente sobrevivente;

        public override string Dica(ArmaJogador quem)
        {
            var s = sobrevivente;
            var m = CarrosQuebradosDoMundo.Instancia;
            if (s == null || s.Morto || m == null || quem == null || quem.Inventario == null) return null;
            int q = s.CenaNaRede;
            switch (m.Estado(q))
            {
                case EstadoDoCarroQuebrado.Esperando:
                    var j = quem.GetComponent<JogadorRede>();
                    if (j != null && CarrosQuebradosDoMundo.Armado(j)) return "Guarde a arma para falar com eles";
                    if (Vehicles.PortaMalasDoCarro.AcharPneu(quem.Inventario, out int estado, out _) && estado >= CarrosQuebradosDoMundo.PneuMinimo)
                        return $"[E] Dar o pneu ({estado}%)";
                    return "[E] Falar";
                case EstadoDoCarroQuebrado.Trocando:
                    return $"Trocando o pneu... ({m.Falta(q)} s)";
                case EstadoDoCarroQuebrado.Ajudados:
                    return "[E] Falar";
                default:
                    return null;
            }
        }

        public override void Usar(ArmaJogador quem)
        {
            var s = sobrevivente;
            var m = CarrosQuebradosDoMundo.Instancia;
            if (s == null || s.Morto || m == null || quem == null || quem.Inventario == null) return;
            int q = s.CenaNaRede;
            var estadoDaCena = m.Estado(q);
            if (estadoDaCena == EstadoDoCarroQuebrado.Esperando
                && Vehicles.PortaMalasDoCarro.AcharPneu(quem.Inventario, out int estado, out ushort id) && estado >= CarrosQuebradosDoMundo.PneuMinimo)
                m.PedirDarPneu(q, id);
            else if (estadoDaCena == EstadoDoCarroQuebrado.Esperando || estadoDaCena == EstadoDoCarroQuebrado.Ajudados)
                m.PedirConversa(q);
        }
    }
}
