using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O bunker da comunidade no Lakewood (22/11/2026, a etapa 2 do Docs/23-Campanha-e-Mapa.md): o
    /// <see cref="BunkerDoMundo"/> da cena fixado no lugar do mapa (<see cref="LugarDoBunker"/>, cavado na encosta perto da
    /// casa da partida). Com ele, o jogo novo começa na sala de operações, olhando para a mesa de reunião (como no mundo
    /// sorteado), e o anfitrião põe a perua, o cupê e as motos nas vagas da garagem (só com ela pronta: antes disso, o chão
    /// da garagem é o buraco do terreno) e as chaves no chão da sala. A casa da partida continua no mapa, vazia: as armas e
    /// os veículos dela ficam para o teste (o F1, painel do Lakewood: "A casa de teste").
    /// Quem está dentro do bunker não volta para a superfície (<see cref="Segurar"/>), e quem está num andar ainda sem a
    /// colisão espera parado (<see cref="EsperarOBunker"/>).
    /// </summary>
    public partial class LakewoodNaCena
    {
        BunkerDoMundo _bunker;
        Vector3[] _nascimentosNoBunker;
        float[] _girosNoBunker;
        bool _esperandoOBunker;

        /// <summary>O bunker da comunidade da cena (null sem ele).</summary>
        public BunkerDoMundo Bunker => _bunker;

        // no Awake, depois da casa da partida: o bunker no lugar dele e a partida na sala de operações
        void FixarOBunker()
        {
            var lugar = Chao.Bunker;
            if (lugar == null) return;
            var b = BunkerDoMundo.Instancia != null ? BunkerDoMundo.Instancia : FindAnyObjectByType<BunkerDoMundo>();
            if (b == null)
            {
                Debug.LogWarning("[Lakewood] O mapa tem o bunker, mas a cena não tem o BunkerDoMundo (Lakewood > Etapa 6: as cidades na cena).");
                return;
            }
            _bunker = b;
            // (o bunker fica fora da raiz: no mundo; 5 cm acima do pátio, como no mundo sorteado)
            b.Fixar(OrigemDeLakewood.NoMundo(new Vector3(lugar.Meio.x, lugar.Altura + 0.05f, lugar.Meio.y)), lugar.Giro, mapa.semente);
            // os nascimentos na sala de operações, olhando para a mesa de reunião (o GeradorDoMundo.PartidaNoBunker)
            int n = PlantaDeBunker.Nascimentos.Length;
            _nascimentosNoBunker = new Vector3[n];
            _girosNoBunker = new float[n];
            for (int i = 0; i < n; i++)
            {
                _nascimentosNoBunker[i] = OrigemDeLakewood.NoMundo(lugar.DoEsboco(PlantaDeBunker.Nascimentos[i], 0.05f));
                _girosNoBunker[i] = lugar.GiroDe(PlantaDeBunker.Nascimentos[i], PlantaDeBunker.MesaDeReuniao);
            }
            // a partida (no mapa): o chão e os pinheiros em volta montam já no Awake
            partida = lugar.DoEsboco(PlantaDeBunker.MeioDasOperacoes);
            Debug.Log($"[Lakewood] O bunker da comunidade: em ({lugar.Meio.x:F0}; {lugar.Meio.y:F0}) do mapa, setor {mapa.NomeDoSetorEm(lugar.Meio.x, lugar.Meio.y)}, o giro {lugar.Giro:F0}°, o pátio a {lugar.Altura:F1} m; a partida na sala de operações.");
        }

        void Start()
        {
            // os pontos de nascimento da cena (os da casa da partida) passam para a sala de operações
            if (_nascimentosNoBunker != null) PontosDeNascimento.Definir(_nascimentosNoBunker, _girosNoBunker);
        }

        // ------------------------------------------------------------------ o jogador dentro do bunker

        /// <summary>
        /// O ponto (mundo) fica dentro de uma obra com andares abaixo do chão do terreno: o bunker (a caixa, a casinha e o
        /// morro) ou o Hospital Principal (o subsolo). Ali, a volta para cima não vale (como no MundoDaPartida.ManterNoChao):
        /// quem cai abaixo do último andar sai da caixa e volta a valer.
        /// </summary>
        static bool EmObraFunda(Vector3 p) => BunkerDoMundo.Dentro(p) || HospitalDoMundo.Dentro(p);

        // o jogador local num lugar do bunker ainda sem a colisão (a casca, ou o andar dele, montando em outra thread) fica
        // parado até ela chegar: sem isso, quem nasce na sala de operações caía pelo buraco do terreno embaixo da caixa
        void EsperarOBunker()
        {
            var b = _bunker;
            var rede = NetworkManager.Singleton;
            var meu = rede != null && rede.IsListening ? rede.LocalClient?.PlayerObject : null;
            var mov = meu != null ? meu.GetComponent<MovimentoPrimeiraPessoa>() : null;
            if (b == null || mov == null)
            {
                _esperandoOBunker = false;
                return;
            }
            var p = meu.transform.position;
            int andar = BunkerDoMundo.AndarDe(p);
            bool falta = !mov.Voando && BunkerDoMundo.Dentro(p) && (!b.CascaPronta || (andar >= 0 && !b.AndarPronto(andar)));
            if (falta)
            {
                if (!mov.Esperando)
                {
                    mov.Esperando = true;
                    _esperandoOBunker = true;
                }
            }
            else if (_esperandoOBunker)
            {
                _esperandoOBunker = false;
                mov.Esperando = false;
            }
        }

        // ------------------------------------------------------------------ os veículos na garagem (no anfitrião)

        // a perua e o cupê de ré nas vagas da garagem (a frente para o portão), as motos na vaga de antes da perua (lado a
        // lado) e as chaves no chão da sala de operações, entre os dois primeiros que nascem (como o MundoDaPartida)
        void PorOsVeiculosNoBunker(ItensNoMundo itens)
        {
            var lugar = Chao.Bunker;
            float giro = lugar.Giro + 180f;
            var rot = Quaternion.Euler(0f, giro, 0f);
            var perua = OrigemDeLakewood.NoMundo(lugar.DoEsboco(PlantaDeBunker.VagaDaPerua));
            var cupe = OrigemDeLakewood.NoMundo(lugar.DoEsboco(PlantaDeBunker.VagaDoCupe));
            var passo = cupe - perua;
            passo.y = 0f;
            var ao = passo.sqrMagnitude > 0.01f ? passo.normalized : Vector3.right;
            var vagaDasMotos = perua - passo;
            var meio = Vector3.Lerp(_nascimentosNoBunker[0], _nascimentosNoBunker[1], 0.5f) + Vector3.down * 0.05f;
            var lado = _nascimentosNoBunker[0] - _nascimentosNoBunker[1];
            lado.y = 0f;
            lado = lado.sqrMagnitude > 0.01f ? lado.normalized : Vector3.right;
            int chaves = 0, postos = 0;
            for (int i = 0; i < veiculos.Length; i++)
            {
                if (veiculos[i] == null) continue;
                var c0 = veiculos[i].GetComponent<Carro>();
                bool moto = c0 != null && c0.moto;
                Vector3 pos;
                if (!moto) pos = (i == 0 ? perua : cupe) + Vector3.up * 0.45f;
                else pos = vagaDasMotos + ao * (postos % 2 == 0 ? -0.85f : 0.85f) + Vector3.up * 0.3f;
                if (moto) postos++;
                var go = Instantiate(veiculos[i], pos, rot);
                go.name = veiculos[i].name;
                var carro = go.GetComponent<Carro>();
                if (carro != null) carro.ArrumadoDeFabrica();
                go.GetComponent<NetworkObject>().Spawn(true);
                if (itens != null && carro != null && carro.chaveDoVeiculo != Item.Nada)
                {
                    itens.Criar(carro.chaveDoVeiculo, meio + lado * (0.3f * chaves - 0.45f), lugar.Giro + 35f - 25f * chaves);
                    chaves++;
                }
            }
            Debug.Log($"[Lakewood] O bunker: {veiculos.Length} veículos na garagem e {chaves} chaves na sala de operações.");
        }

        // ------------------------------------------------------------------ a casa de teste (o F1)

        bool _casaDeTestePosta;

        /// <summary>
        /// No anfitrião (o F1, painel do Lakewood): os quatro veículos arrumados no terreiro da casa da partida e as armas, a
        /// munição e as chaves nas mesas dela, como era a partida antes do bunker. Uma vez por partida.
        /// </summary>
        public string PorACasaDeTeste()
        {
            var rede = NetworkManager.Singleton;
            if (rede == null || !rede.IsServer) return "Só o anfitrião.";
            if (Chao.Partida == null) return "O mapa não tem a casa da partida.";
            if (_casaDeTestePosta) return "A casa de teste já está pronta (uma vez por partida).";
            var itens = ItensNoMundo.Instancia;
            if (itens == null || !itens.IsSpawned) return "Os itens do mundo ainda não estão prontos.";
            _casaDeTestePosta = true;
            PorOsVeiculosNoTerreiro();
            PorAsArmas(itens);
            return "A casa de teste: os 4 veículos no terreiro e as armas nas mesas.";
        }
    }
}
