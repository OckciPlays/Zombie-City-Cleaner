using Unity.Netcode.Components;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O carro parado (04/11/2026, o carro que sumia do bunker). O chão de um lugar só tem colisão perto de alguém: o
    /// bunker (a casca, a garagem e os andares) é montado só perto dos jogadores, e longe de todos o piso some. O carro
    /// deixado lá caía pelo mundo para sempre (no teste, a 400 m: 330 m abaixo do chão em 12 s), e quem voltava não o
    /// achava mais. No anfitrião, o carro vazio:
    ///  - fica <b>parado</b> (cinemático, sem a física) quando ninguém está a <see cref="distanciaParaParar"/> m, e também ao
    ///    nascer (o do save e o do mundo novo), até alguém chegar a <see cref="distanciaParaParar"/> − <see cref="Folga"/> m
    ///    e o chão embaixo dele ter colisão (os raios das rodas para baixo);
    ///  - solto e quieto, se o chão some debaixo dele (o andar do bunker de um jogador que desceu), volta ao último lugar
    ///    apoiado e fica parado até o chão voltar;
    ///  - caído do mundo (mais de <see cref="FundoDoMundo"/> m abaixo do terreno: um save de antes do conserto), volta à
    ///    vaga dele (<see cref="World.MundoDaPartida.VagaDoCarro"/>).
    /// O macaco (que também deixa o corpo cinemático) e quem senta mandam no carro: o parado só vale vazio e sem o macaco.
    /// O terreno não pede o chão em volta do carro parado (<see cref="World.TerrenoDoMundo"/>).
    /// </summary>
    public partial class Carro
    {
        [Header("Parado longe de todos")]
        [Tooltip("Vazio e sem ninguém a esta distância (m), o carro fica parado (sem a física): longe de todos, o chão de lá perde a colisão, e o carro caía pelo mundo.")]
        public float distanciaParaParar = 60f;

        /// <summary>O carro parado volta a andar com alguém a <see cref="distanciaParaParar"/> menos isto (m).</summary>
        const float Folga = 10f;
        /// <summary>De quanto em quanto tempo o anfitrião olha quem está perto (s).</summary>
        const float IntervaloDoParado = 0.25f;
        /// <summary>Abaixo do terreno mais que isto (m), o carro caiu do mundo (o bunker é bem mais raso).</summary>
        const float FundoDoMundo = 60f;
        /// <summary>Até onde, abaixo das rodas, o raio procura o chão (m).</summary>
        const float AlcanceDoChao = 0.9f;

        bool _parado;
        float _proximaOlhada;
        bool _temApoio;
        Vector3 _apoio;
        Quaternion _giroDoApoio;
        static readonly RaycastHit[] _toquesDoChao = new RaycastHit[8];

        /// <summary>No anfitrião: o carro vazio está parado (sem a física), longe de todos ou sem chão embaixo.</summary>
        public bool Parado => _parado;

        /// <summary>No anfitrião, ao nascer: parado até alguém chegar e o chão de lá ter colisão.</summary>
        void PararAoNascer()
        {
            _parado = true;
            _temApoio = false;
            _proximaOlhada = 0f;
        }

        /// <summary>
        /// No começo do FixedUpdate (no anfitrião dono do carro): cuida do parado. Devolve true com o carro parado (o resto
        /// da física não roda).
        /// </summary>
        bool CuidarDoParado()
        {
            if (!IsSpawned || !IsServer || _corpo == null) return false;
            // o motorista num cliente simula o carro: o parado de antes não vale mais (senão, quando ele saísse andando, a
            // posse voltava ao anfitrião com o carro ainda "parado", e ele freava de uma vez)
            if (!IsOwner)
            {
                _parado = false;
                _temApoio = false;
                return false;
            }
            // o macaco cuida do corpo (cinemático enquanto apoiado); ao sair, o carro fica solto e no chão
            if (MacacoNaRoda >= 0)
            {
                _parado = false;
                _temApoio = false;
                return false;
            }
            if (!Vazio)
            {
                // o apoio de antes não vale depois de alguém dirigir (senão o carro, sem chão logo depois de o motorista
                // sair, voltaria para onde estava parado antes)
                _temApoio = false;
                if (_parado) Soltar();
                return false;
            }
            float agora = Time.time;
            if (_parado)
            {
                // a rede (o NetworkRigidbody, ao voltar a posse para o anfitrião) solta o corpo: prende de novo
                if (!_corpo.isKinematic) Prender();
                if (agora < _proximaOlhada) return true;
                _proximaOlhada = agora + IntervaloDoParado;
                if (CaiuDoMundo()) VoltarParaAVaga();
                else if (AlguemA(distanciaParaParar - Folga) && TemChaoEmbaixo() && ChaoDoBunkerPronto())
                {
                    Soltar();
                    return false;
                }
                return true;
            }

            // solto: o chão some debaixo do carro quieto (um andar do bunker desmontado com alguém perto)
            bool chao = TemChaoEmbaixo();
            float velocidade = _corpo.linearVelocity.magnitude;
            if (!chao && _temApoio)
            {
                Prender();
                PorEm(_apoio, _giroDoApoio);
                _parado = true;
                _proximaOlhada = agora + IntervaloDoParado;
                return true;
            }
            if (velocidade > 1f) _temApoio = false;
            else if (chao && velocidade < 0.3f)
            {
                _temApoio = true;
                _apoio = _corpo.position;
                _giroDoApoio = _corpo.rotation;
            }
            if (agora < _proximaOlhada) return false;
            _proximaOlhada = agora + IntervaloDoParado;
            if (CaiuDoMundo())
            {
                VoltarParaAVaga();
                return true;
            }
            if (!AlguemA(distanciaParaParar))
            {
                Prender();
                _parado = true;
                return true;
            }
            return false;
        }

        void Prender()
        {
            if (!_corpo.isKinematic)
            {
                _corpo.linearVelocity = Vector3.zero;
                _corpo.angularVelocity = Vector3.zero;
            }
            _corpo.isKinematic = true;
        }

        void Soltar()
        {
            _parado = false;
            _temApoio = false;
            if (_corpo == null || MacacoNaRoda >= 0) return;
            if (IsSpawned && !IsOwner) return;   // o corpo é da rede (o motorista dirige)
            _corpo.isKinematic = false;
            _corpo.WakeUp();
        }

        /// <summary>Algum jogador a menos de <paramref name="raio"/> m do carro.</summary>
        bool AlguemA(float raio)
        {
            var p = transform.position;
            foreach (var j in JogadorRede.Todos)
                if (j != null && (j.transform.position - p).sqrMagnitude < raio * raio) return true;
            return false;
        }

        /// <summary>
        /// Uma roda encostada no chão (com a física), ou o chão a menos de <see cref="AlcanceDoChao"/> m abaixo do pé de uma
        /// roda ou do meio do carro (sem contar o próprio carro).
        /// </summary>
        bool TemChaoEmbaixo()
        {
            if (rodas == null) return false;
            if (!_corpo.isKinematic)
                foreach (var r in rodas)
                    if (r != null && r.isGrounded) return true;
            foreach (var r in rodas)
            {
                if (r == null) continue;
                var centro = r.transform.position;
                if (ChaoAbaixo(centro + Vector3.up * 0.3f, r.radius + r.suspensionDistance + 0.3f + AlcanceDoChao)) return true;
            }
            return ChaoAbaixo(_corpo.worldCenterOfMass, 1.6f + AlcanceDoChao);
        }

        /// <summary>
        /// Na área do bunker o chão é da casca dele (a laje da garagem, o morro, o pátio): só solta com ela montada (no morro,
        /// o raio podia achar o terreno embaixo dele antes da casca, e o carro ficava dentro do morro).
        /// </summary>
        bool ChaoDoBunkerPronto()
        {
            var b = World.BunkerDoMundo.Instancia;
            return b == null || !World.BunkerDoMundo.NaArea(transform.position) || b.CascaPronta;
        }

        bool ChaoAbaixo(Vector3 origem, float alcance)
        {
            // só o cenário (a camada 0), como os raios de chão das rodas: um jogador ou um zumbi embaixo não é chão
            int n = Physics.RaycastNonAlloc(origem, Vector3.down, _toquesDoChao, alcance, 1, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _toquesDoChao[i].collider;
                if (c == null || c.attachedRigidbody == _corpo || c.transform.IsChildOf(transform)) continue;
                return true;
            }
            return false;
        }

        bool CaiuDoMundo()
        {
            var dados = World.MundoDaPartida.Instancia != null ? World.MundoDaPartida.Instancia.Dados : null;
            if (dados == null) return false;
            var p = transform.position;
            return p.y < dados.Altura(p.x, p.z) - FundoDoMundo;
        }

        /// <summary>No anfitrião: o carro caído do mundo volta à vaga dele, parado (solta quando alguém chegar).</summary>
        void VoltarParaAVaga()
        {
            var mundo = World.MundoDaPartida.Instancia;
            if (mundo == null || !mundo.VagaDoCarro(this, out var ponto, out var giro)) return;
            // a vaga ocupada (o outro carro estacionado nela): ao lado
            var lado = giro * Vector3.right;
            foreach (float x in new[] { 0f, 3.8f, -3.8f, 7.6f, -7.6f })
            {
                var p = ponto + lado * x;
                if (Physics.CheckBox(p + Vector3.up * 0.75f, new Vector3(1.0f, 0.45f, 2.45f), giro, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                ponto = p;
                break;
            }
            Debug.Log($"[Carro] {name} caiu do mundo (em {transform.position}): voltou para a vaga ({ponto})");
            Prender();
            PorEm(ponto, giro);
            _parado = true;
            _temApoio = false;
        }

        /// <summary>Põe o carro (cinemático) num lugar, sem os outros verem ele voar até lá.</summary>
        void PorEm(Vector3 ponto, Quaternion giro)
        {
            _corpo.position = ponto;
            _corpo.rotation = giro;
            transform.SetPositionAndRotation(ponto, giro);
            var rede = GetComponent<NetworkTransform>();
            if (rede != null && rede.IsSpawned && rede.CanCommitToTransform) rede.Teleport(ponto, giro, transform.localScale);
        }
    }
}
