using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// O Capitão na caçamba (25/11/2026, a pedido do Pedro: "quando pegarmos a caminhonete, ele deveria subir em cima dela,
    /// como se fosse um novo passageiro") e o Capitão que não morre ("da para matar ele batendo nele (acho que não
    /// deveríamos dar para matar ele)").
    ///  - <b>A caçamba</b>: quando quem ele segue entra na caminhonete da família, o anfitrião (LakewoodNaCena) o põe num
    ///    lugar só dele, entre o estepe e o macaco, sem ocupar os dois lugares dos jogadores lá atrás. Agachado (a pose da
    ///    proteção baixa, com a arma na mão), ele olha para trás e atira nos zumbis a até 30 m. Em todas as máquinas ele fica
    ///    preso ao chão da caçamba no LateUpdate (pela <see cref="_cacamba"/>, o carro na rede), andando com ela; o agente
    ///    fica desligado e a animação parada. Quando quem ele segue desce, ele desce do lado da caçamba.
    ///  - <b>Imortal</b> (<see cref="Imortal"/>): o tiro, o golpe, o carro, os zumbis e o fogo não tiram vida nem fazem
    ///    sangue; o tiro e o golpe de um jogador o fazem reclamar ("Ei! Sou eu!").
    /// </summary>
    public partial class Sobrevivente
    {
        /// <summary>O carro em cuja caçamba ele vai (o NetworkObjectId; 0, no chão).</summary>
        readonly NetworkVariable<ulong> _cacamba = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>O lugar dele na caçamba (no carro: x e z; a altura é o piso da caçamba): entre o estepe e o macaco.</summary>
        static readonly Vector2 LugarNaCacamba = new Vector2(0.40f, -1.68f);

        Carro _carroDaCacamba;
        ulong _idDaCacamba;
        float _proximaReclamacao;

        /// <summary>Não morre (o Capitão da comunidade, pela identidade: em todas as máquinas).</summary>
        public bool Imortal => IsSpawned && (Identidade.Value.grupo == LakewoodNaCena.GrupoDoCapitao
                                             || Identidade.Value.grupo == LakewoodNaCena.GrupoDoGus);   // (e o Gus do tutorial, 25/11/2026)

        /// <summary>Vai na caçamba de um carro (em todas as máquinas).</summary>
        public bool NaCacamba => IsSpawned && _cacamba.Value != 0;

        /// <summary>O carro em cuja caçamba ele vai, ou null.</summary>
        public Carro CarroDaCacamba
        {
            get
            {
                ulong id = IsSpawned ? _cacamba.Value : 0;
                if (id == 0) return null;
                if (id != _idDaCacamba || _carroDaCacamba == null)
                {
                    _idDaCacamba = id;
                    _carroDaCacamba = NetworkManager != null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var no) ? no.GetComponent<Carro>() : null;
                }
                return _carroDaCacamba;
            }
        }

        /// <summary>No anfitrião: sobe na caçamba do carro (o agente desliga).</summary>
        internal void SubirNaCacamba(Carro c)
        {
            if (!IsServer || c == null || !c.IsSpawned) return;
            if (agente != null && agente.enabled)
            {
                if (agente.isOnNavMesh) agente.ResetPath();
                agente.enabled = false;
            }
            _presoDesde = -1f;
            _cacamba.Value = c.NetworkObjectId;
            transform.SetPositionAndRotation(PontoNaCacamba(c), c.transform.rotation * Quaternion.Euler(0f, 180f, 0f));
            Falar(Frase.NaCacamba, true);
        }

        /// <summary>No anfitrião: desce da caçamba no ponto (no chão, do lado dela).</summary>
        internal void DescerDaCacamba(Vector3 ponto)
        {
            if (!IsServer || !NaCacamba) return;
            _cacamba.Value = 0;
            transform.position = ponto;
            LigarOAgente();
        }

        static Vector3 PontoNaCacamba(Carro c) => c.transform.TransformPoint(new Vector3(LugarNaCacamba.x, c.pisoDaCacamba, LugarNaCacamba.y));

        /// <summary>Em todas as máquinas, no LateUpdate: preso ao chão da caçamba (depois do NetworkTransform e do carro).</summary>
        void PresoNaCacamba()
        {
            var c = CarroDaCacamba;
            if (c == null) return;
            transform.position = PontoNaCacamba(c);
            // (fora do anfitrião, o giro vem do NetworkTransform; sem mirar, para trás da caminhonete)
        }

        // no anfitrião, na caçamba: olha, atira nos zumbis a até 30 m, recarrega; agachado, virado para trás
        void PensarNaCacamba(float agora)
        {
            if (_recarregando && agora >= _recarregaAte) TerminarRecarga();
            if (agora >= _proximaOlhada)
            {
                _proximaOlhada = agora + Random.Range(0.22f, 0.32f);
                Olhar(agora);
            }
            bool luz = CicloDoDia.Noite > 0.5f;
            if (_luz.Value != luz) _luz.Value = luz;
            _mirandoEm = null;
            var c = CarroDaCacamba;
            if (_zumbi != null && ZumbiAmeaca(_zumbi))
            {
                Vector3 alvo = _zumbi.transform.position + Vector3.up * 1.35f;
                if (!Physics.Linecast(OlhoAgora, alvo, 1 << 0, QueryTriggerInteraction.Ignore)) AtirarNo(null, alvo, agora);
            }
            else if (c != null)
            {
                var giro = c.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, giro, 240f * Time.deltaTime);
            }
            Mirar();
            _querInclinar = 0;
            _querAgachar = 100;
            AplicarAPose();
        }

        // no anfitrião: um jogador acertou o Capitão (o tiro ou o golpe): ele reclama, sem perder vida
        void Reclamar()
        {
            if (Time.time < _proximaReclamacao) return;
            _proximaReclamacao = Time.time + 2.5f;
            Falar(Frase.Amigo, true);
        }
    }
}
