using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O sol dos Ocos (veja Zumbi.Sol.cs): de dia, quem está no sol vira pedra. "No sol" é um raio do ponto (na altura do
    /// peito) até o sol sem nada no caminho: o cenário (o chão, as construções e as casas, camadas Default e TransparentFX)
    /// e os carros fazem sombra; pelas janelas e portas (a colisão das casas e dos prédios tem os vãos abertos) o sol
    /// entra; dentro do bunker (as áreas internas) nunca é sol; debaixo da copa de uma árvore viva é sombra
    /// (FlorestaDoMundo.SobAsCopas: as copas não têm colisão). O sol do jogo fica parado no céu de dia (AmbienteDoDia: 55° de
    /// altura, a mesma direção o dia todo), então a sombra de cada coisa fica no mesmo lugar, e a conta vale de madrugada
    /// para prever (o abrigo ao amanhecer).
    /// </summary>
    public partial class ZombieManager
    {
        // o cenário (Default e TransparentFX, onde ficam as construções) e os carros
        const int MascaraDaSombra = (1 << 0) | (1 << 1) | (1 << Vehicles.Carro.CamadaVeiculo);
        const float AlturaDoTesteDoSol = 1.2f;

        AmbienteDoDia _ambiente;

        /// <summary>Dia claro o bastante para o sol petrificar (do meio do amanhecer ao meio do anoitecer).</summary>
        public bool EDia => CicloDoDia.Noite < 0.5f;

        /// <summary>
        /// O amanhecer, antes de o sol petrificar (das 5h até o dia claro): os zumbis procuram abrigo (Zumbi.Sol.cs) e quem
        /// não acha vira estátua quando o sol vem.
        /// </summary>
        public bool Amanhecendo => !EDia && CicloDoDia.Hora >= 5f && CicloDoDia.Hora < 8f;

        /// <summary>De dia ou amanhecendo: os zumbis só vagam e param onde o sol do dia não bate.</summary>
        public bool ProtegerDoSol => EDia || Amanhecendo;

        /// <summary>
        /// A direção do ponto para o sol do dia (a altura e o giro do sol de dia do AmbienteDoDia: o sol fica parado no céu
        /// de dia, então vale também de madrugada, para prever onde ele vai bater), ou zero sem o ambiente.
        /// </summary>
        Vector3 ParaOSol()
        {
            if (_ambiente == null)
            {
                _ambiente = FindAnyObjectByType<AmbienteDoDia>();
                if (_ambiente == null) return Vector3.zero;
            }
            return -(Quaternion.Euler(_ambiente.dia.alturaLuz, -30f, 0f) * Vector3.forward);
        }

        /// <summary>
        /// Este ponto (os pés) fica no sol do dia? Um raio do peito até o sol sem nada no caminho (as construções, os carros;
        /// pelas janelas e portas abertas o sol entra), fora da copa das árvores e fora do bunker. Não olha a hora.
        /// </summary>
        public bool Exposto(Vector3 pes)
        {
            if (AreaInterna.Contem(pes)) return false;
            Vector3 paraOSol = ParaOSol();
            if (paraOSol.y < 0.05f) return false;   // sem sol
            if (Physics.Raycast(pes + Vector3.up * AlturaDoTesteDoSol, paraOSol, 200f, MascaraDaSombra, QueryTriggerInteraction.Ignore)) return false;
            var floresta = FlorestaDoMundo.Instancia;
            return floresta == null || !floresta.SobAsCopas(pes);
        }

        /// <summary>De dia, este ponto (os pés) está no sol?</summary>
        public bool NoSol(Vector3 pes) => EDia && Exposto(pes);

        /// <summary>Tem teto em cima (até 30 m): dentro de uma casa, de um prédio, debaixo de um viaduto. O abrigo preferido.</summary>
        public bool Coberto(Vector3 pes) =>
            Physics.Raycast(pes + Vector3.up * AlturaDoTesteDoSol, Vector3.up, 30f, MascaraDaSombra, QueryTriggerInteraction.Ignore);

        // a cada quadro, no anfitrião: a pedra de cada zumbi vivo
        void SolNosZumbis(float agora, float dt)
        {
            for (int i = 0; i < _ativos.Count; i++)
            {
                var z = _ativos[i];
                if (!z.Morto) z.SolNoServidor(agora, dt);
            }
        }

        // ------------------------------------------------------------------ o menu de teste (F1)

        /// <summary>Teste: todos os vivos até este raio viram estátua agora.</summary>
        public int PetrificarPerto(Vector3 centro, float raio)
        {
            if (!IsServer || !IsSpawned) return 0;
            int n = 0;
            foreach (var z in _ativos)
                if (!z.Morto && !z.Petrificado && (z.transform.position - centro).sqrMagnitude < raio * raio)
                {
                    z.PetrificarJa();
                    n++;
                }
            return n;
        }

        /// <summary>Teste: um zumbi no ponto (no NavMesh), olhando para a direção; já de pedra, se pedido.</summary>
        public bool CriarUm(Vector3 ponto, float yaw, bool dePedra)
        {
            if (!IsServer || !IsSpawned || !NavMesh.SamplePosition(ponto, out var hit, 2f, NavMesh.AllAreas)) return false;
            var z = Nascer(hit.position);
            if (z == null) return false;
            z.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (dePedra) z.PetrificarJa();
            return true;
        }

        /// <summary>Quantos vivos estão de pedra (no anfitrião; nos clientes, das cópias recebidas).</summary>
        public int DePedra
        {
            get
            {
                int n = 0;
                foreach (var z in IsServer ? _ativos : _copias)
                    if (z.Petrificado) n++;
                return n;
            }
        }

        /// <summary>Teste: todas as estátuas se soltam agora (de dia, no sol, a pedra volta a subir).</summary>
        public int SoltarTodos()
        {
            if (!IsServer || !IsSpawned) return 0;
            int n = 0;
            float agora = Time.time;
            foreach (var z in _ativos)
                if (z.Petrificado)
                {
                    z.SoltarJa(agora);
                    n++;
                }
            return n;
        }
    }
}
