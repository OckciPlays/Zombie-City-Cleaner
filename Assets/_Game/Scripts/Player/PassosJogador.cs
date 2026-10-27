using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os passos de um jogador, em todas as cópias (ninguém manda nada pela rede: cada uma conta os passos pela distância
    /// andada e pela velocidade). Um passo a cada 0,6 m mirando (devagar), 0,75 m andando e 1,15 m correndo. O som é o do
    /// chão embaixo do pé (<see cref="Superficie"/>): grama no gramado; nas casas e no bunker, o piso do cômodo
    /// (<see cref="PisoPorZona"/>: taco, carpete ou o piso frio da cozinha); concreto no resto (ruas, calçadas e o
    /// prédio). No ar, nada; ao cair de um pulo, os dois pés.
    /// O dono ouve os próprios passos baixos, "dentro da cabeça"; os dos outros saem no pé deles, até 25 m.
    /// </summary>
    public class PassosJogador : MonoBehaviour
    {
        public JogadorRede rede;
        [Tooltip("Volume dos próprios passos (andando; correndo, 1,5 vez).")]
        public float volumeProprio = 0.22f;
        [Tooltip("Volume dos passos dos outros (andando; correndo, 1,5 vez).")]
        public float volumeDosOutros = 0.6f;

        // o raio do chão não vê os jogadores, os braços, os zumbis, os corpos e o Ignore Raycast
        const int MascaraDoChao = ~((1 << 2) | (1 << JogadorRede.CamadaBracos) | (1 << 7) | (1 << 8) | (1 << JogadorRede.CamadaJogador));
        // o chão de cada colisor: a superfície fixa, ou o PisoPorZona da construção dele (que depende do ponto)
        static readonly Dictionary<Collider, (Superficie fixa, PisoPorZona zonas)> _cache = new Dictionary<Collider, (Superficie, PisoPorZona)>();

        CharacterController _controle;
        Vector3 _ultima;
        float _andado, _noArDesde = -1f;
        bool _comecou;

        // Play sem recarregar o domínio: os colisores da partida anterior não ficam guardados
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Limpar() => _cache.Clear();

        void Awake() => _controle = GetComponent<CharacterController>();

        void Update()
        {
            if (!rede.IsSpawned) return;
            Vector3 pos = transform.position;
            if (!_comecou) { _ultima = pos; _comecou = true; }
            Vector3 d = pos - _ultima;
            _ultima = pos;
            d.y = 0f;
            float andou = d.magnitude;
            if (andou > 2f) andou = 0f;   // teleporte (nasceu, caiu)

            // o dono sabe se está no chão; ao voltar de um pulo (ou de uma queda), os dois pés
            if (rede.IsOwner)
            {
                if (!_controle.isGrounded)
                {
                    if (_noArDesde < 0f) _noArDesde = Time.time;
                    return;
                }
                if (_noArDesde >= 0f && Time.time - _noArDesde > 0.3f)
                {
                    Pisar(true, 1.4f);
                    _andado = 0f;
                }
                _noArDesde = -1f;
            }

            float vel = rede.Velocidade;
            bool correndo = vel > 3.2f;
            float passo = correndo ? 1.15f : vel < 1.6f ? 0.6f : 0.75f;
            // parado, o próximo passo sai logo que ele andar (meio passo)
            if (vel < 0.4f)
            {
                _andado = Mathf.Min(_andado, passo * 0.5f);
                return;
            }
            _andado += andou;
            if (_andado < passo) return;
            _andado -= passo;
            Pisar(correndo, correndo ? 1.5f : 1f);
        }

        void Pisar(bool correndo, float forca)
        {
            if (Efeitos.Passos == null) return;
            Vector3 pe = transform.position;
            if (!Physics.Raycast(pe + Vector3.up * 0.3f, Vector3.down, out var chao, 0.6f, MascaraDoChao, QueryTriggerInteraction.Ignore)) return;
            var clips = Efeitos.Passos[(int)Do(chao.collider, chao.point)][correndo ? 1 : 0];
            if (rede.IsOwner) Efeitos.Som(clips, chao.point, volumeProprio * forca, false);
            else Efeitos.Som(clips, chao.point, volumeDosOutros * forca, true, 25f);
        }

        /// <summary>
        /// O chão de um colisor naquele ponto: o gramado ("Chão"; no mundo gerado, concreto em cima das estradas asfaltadas
        /// e do acostamento, que não têm colisor próprio), o cômodo de uma construção com <see cref="PisoPorZona"/> (as
        /// casas, o bunker, e os móveis dentro delas) ou concreto.
        /// </summary>
        static Superficie Do(Collider c, Vector3 ponto)
        {
            if (!_cache.TryGetValue(c, out var chao))
            {
                chao = (c.name == "Chão" ? Superficie.Grama : Superficie.Concreto, c.name == "Chão" ? null : c.GetComponentInParent<PisoPorZona>());
                _cache[c] = chao;
            }
            if (chao.zonas != null) return chao.zonas.Em(ponto);
            if (chao.fixa == Superficie.Grama && NaEstrada(ponto)) return Superficie.Concreto;
            return chao.fixa;
        }

        static bool NaEstrada(Vector3 p)
        {
            var mundo = ZombieCityCleaner.World.MundoDaPartida.Instancia;
            var dados = mundo != null ? mundo.Dados : null;
            if (dados == null) return false;
            // as ruas das cidades (o asfalto não tem colisor próprio: o pé fica no terreno, 5 cm abaixo)
            if (dados.OcupadoPelaCidade(new Vector2(p.x, p.z), 0f)) return true;
            var rede = dados.Rede;
            return rede != null && rede.Acostado(p.x, p.z, out bool deTerra) > 0.5f && !deTerra;
        }
    }
}
