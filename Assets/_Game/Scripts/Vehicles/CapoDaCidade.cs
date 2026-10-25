using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O capô de um carro da cidade (um carro parado, abandonado): um gatilho em volta da frente, que a mira procura. E
    /// leva a cabeça para dentro do capô (<see cref="TelaDoCapo"/>), com as peças que sobraram nele (guardadas no
    /// <see cref="PecasDosCarros"/>, pelo <see cref="indice"/>): dá para tirar as que servem e levar.
    /// O modelo do carro (do Cidade_Rua.fbx) não abre: a montagem dos veículos recorta o capô da malha e guarda, no
    /// "Capô aberto", a carroceria sem o capô, o capô à parte (<see cref="tampa"/>, a origem na dobradiça) e o cofre do
    /// motor (<see cref="cofre"/>). Enquanto alguém olha o motor, o carro da cidade some e o "Capô aberto" aparece no
    /// lugar dele, com o capô subindo.
    /// </summary>
    public class CapoDaCidade : Interagivel, ICapo
    {
        public PecasDosCarros pecas;
        public int indice;
        [Tooltip("O nome do carro na tela (\"Sedã cinza\").")]
        public string nome = "Carro";

        [Header("O capô aberto")]
        [Tooltip("O carro da cidade (a malha inteira), que some com o capô aberto.")]
        public MeshRenderer carroFechado;
        [Tooltip("A carroceria sem o capô, o capô e o cofre: aparece no lugar do carro com o capô aberto.")]
        public GameObject aberto;
        [Tooltip("O capô recortado da malha (a origem na dobradiça, atrás): abre girando em X.")]
        public Transform tampa;
        public CofreDoMotor cofre;
        public float abertura = 62f;

        float _visto;

        public string NomeDoCarro => nome;
        public CofreDoMotor Cofre => cofre;
        public PecasDoMotor Pecas => pecas != null ? pecas.Do(indice) : default;
        public Vector3 Ponto => transform.position;
        public bool Aberto => pecas != null && pecas.IsSpawned;
        public string Impedimento => null;
        public string Situacao => "Abandonado. O que sobrou no motor:";

        public void PedirTirar(int peca) => pecas.PedirTirar(indice, peca);
        public void PedirPor(int peca, ushort idNaMochila) => pecas.PedirPor(indice, peca, idNaMochila);
        public void Olhando(bool olhando) => pecas?.Olhar(indice, olhando);

        void OnDisable() => Mostrar(false);

        void Update()
        {
            // alguém olha o motor (pela rede), ou o jogador desta máquina (sem esperar a rede)
            bool abrir = (pecas != null && pecas.EstaAberto(indice)) || (cofre != null && cofre.Inspecionando);
            if (!abrir && _visto == 0f) return;
            Mostrar(true);
            CofreDoMotor.AnimarCapo(tampa, Quaternion.identity, abertura, ref _visto, abrir, Time.deltaTime);
            if (_visto == 0f) Mostrar(false);
        }

        // o carro da cidade ou o do capô aberto (o mesmo carro, com o capô à parte)
        void Mostrar(bool comCapoAberto)
        {
            if (aberto == null || carroFechado == null) return;
            if (aberto.activeSelf != comCapoAberto) aberto.SetActive(comCapoAberto);
            if (carroFechado.enabled == comCapoAberto) carroFechado.enabled = !comCapoAberto;
        }

        public override string Dica(ArmaJogador quem)
        {
            if (!Aberto) return null;
            var p = Pecas;
            bool vazio = true;
            for (int k = 0; k < PecasDoMotor.Quantas; k++) vazio &= !p.Tem(k);
            return vazio ? "[E] Abrir o capô (já vasculhado)" : "[E] Abrir o capô";
        }

        public override void Usar(ArmaJogador quem)
        {
            var tela = quem.GetComponent<TelaDoCapo>();
            if (tela != null) tela.Abrir(this);
        }
    }
}
