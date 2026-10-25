using System;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// As 3 peças do motor de um carro (o que se vê e se troca com o capô aberto): a vela (o motor), a bateria e o
    /// radiador. Cada uma é um byte: <see cref="Vazio"/> sem a peça, ou o estado dela, de 0 a 100%. Vai pela rede.
    /// <para>
    /// O que cada uma faz no carro que se dirige (<see cref="Carro"/>):
    /// - a vela: abaixo de <see cref="VelaMinima"/> (ou sem ela), o motor não pega (o motor de arranque gira e ele só
    ///   tosse). Acima, pega, e quanto melhor a vela, maiores as marcas do mini-game da partida e mais força o motor tem;
    /// - a bateria (a carga): sem ela (ou abaixo de <see cref="BateriaMorta"/>), nada acende; abaixo de
    ///   <see cref="BateriaArranque"/>, o painel acende fraco e o motor de arranque não gira (só o estalo). A partida gasta
    ///   a carga, e o motor ligado carrega de volta;
    /// - o radiador: quanto pior, mais quente o motor fica ligado (<see cref="TemperaturaAlvo"/>). Fervendo, o motor
    ///   apaga e só pega de novo quando esfriar.
    /// </para>
    /// </summary>
    public struct PecasDoMotor : INetworkSerializeByMemcpy, IEquatable<PecasDoMotor>
    {
        public const int Quantas = 3, Vela = 0, Bateria = 1, Radiador = 2;
        /// <summary>Sem a peça.</summary>
        public const byte Vazio = 255;
        /// <summary>A vela mínima para o motor pegar (%).</summary>
        public const int VelaMinima = 10;
        /// <summary>Abaixo disso a bateria não acende nada; abaixo de <see cref="BateriaArranque"/>, não gira o arranque.</summary>
        public const int BateriaMorta = 3, BateriaArranque = 12;

        public byte vela, bateria, radiador;

        public byte this[int k]
        {
            get => k == Vela ? vela : k == Bateria ? bateria : radiador;
            set
            {
                if (k == Vela) vela = value;
                else if (k == Bateria) bateria = value;
                else radiador = value;
            }
        }

        public bool Tem(int k) => this[k] != Vazio;
        /// <summary>O estado da peça (0 a 100); sem ela, 0.</summary>
        public int Estado(int k) => Tem(k) ? this[k] : 0;

        public bool Equals(PecasDoMotor o) => vela == o.vela && bateria == o.bateria && radiador == o.radiador;

        public static PecasDoMotor Com(int vela, int bateria, int radiador) => new PecasDoMotor
        {
            vela = Byte(vela), bateria = Byte(bateria), radiador = Byte(radiador),
        };

        static byte Byte(int v) => v < 0 ? Vazio : (byte)Mathf.Clamp(v, 0, 100);

        // ------------------------------------------------------------------ as regras

        /// <summary>O lugar de cada peça no capô (e no menu).</summary>
        public static readonly string[] Lugares = { "Motor", "Bateria", "Radiador" };

        /// <summary>O motor pega com esta vela.</summary>
        public bool VelaBoa => Estado(Vela) >= VelaMinima;
        /// <summary>A bateria acende o painel.</summary>
        public bool BateriaAcende => Estado(Bateria) >= BateriaMorta;
        /// <summary>A bateria gira o motor de arranque.</summary>
        public bool BateriaGira => Estado(Bateria) >= BateriaArranque;

        /// <summary>
        /// Até onde a temperatura do motor ligado sobe (0 frio, ~0,45 normal, 1 fervendo): o radiador bom segura em 0,45;
        /// abaixo de uns 35%, ela passa de 1 e o motor ferve.
        /// </summary>
        public float TemperaturaAlvo => 0.45f + (1f - Estado(Radiador) / 100f) * 0.85f;

        /// <summary>O tamanho das marcas do mini-game da partida, pela vela (gasta: menores).</summary>
        public float FatorDasMarcas => Mathf.Lerp(0.55f, 1.15f, Estado(Vela) / 100f);

        /// <summary>A força do motor, pela vela (gasta: o motor falha e anda menos).</summary>
        public float FatorDeForca => Mathf.Lerp(0.7f, 1f, Estado(Vela) / 100f);

        /// <summary>O que a peça faz neste estado (uma frase curta, para o menu do capô e o inventário).</summary>
        public static string Efeito(int k, int estado)
        {
            if (k == Vela) return estado < VelaMinima ? "o motor não pega" : estado < 40 ? "pega, com falhas" : "o motor pega bem";
            if (k == Bateria) return estado < BateriaMorta ? "morta: nada acende" : estado < BateriaArranque ? "fraca: não gira o arranque" : estado < 35 ? "pouca carga" : "boa carga";
            return estado < 35 ? "o motor ferve" : estado < 65 ? "o motor esquenta" : "esfria bem";
        }

        /// <summary>O efeito de uma peça que falta.</summary>
        public static string Faltando(int k) => k == Vela ? "sem vela: o motor não pega" : k == Bateria ? "sem bateria: nada acende" : "sem radiador: o motor ferve";

        /// <summary>A cor do estado: vermelho, amarelo ou verde.</summary>
        public static Color CorDoEstado(int estado) =>
            estado < 25 ? new Color(0.95f, 0.38f, 0.3f) : estado < 60 ? new Color(0.98f, 0.8f, 0.35f) : new Color(0.5f, 0.9f, 0.45f);

        // ------------------------------------------------------------------ no anfitrião

        /// <summary>
        /// Só no anfitrião: tira a peça do carro e põe na mochila do jogador (com o estado dela). Sem espaço, ela fica no
        /// carro e devolve false.
        /// </summary>
        public bool Tirar(int k, InventarioJogador inventario)
        {
            if (k < 0 || k >= Quantas || !Tem(k) || inventario == null) return false;
            if (!inventario.Receber(Itens.DaPeca(k), this[k])) return false;
            this[k] = Vazio;
            return true;
        }

        /// <summary>
        /// Só no anfitrião: a peça da mochila (pelo Id) vai para o carro. A que estava no lugar vai para a mochila (ou,
        /// sem espaço, para o chão aos pés do jogador).
        /// </summary>
        public bool Por(int k, InventarioJogador inventario, ushort id)
        {
            if (k < 0 || k >= Quantas || inventario == null || !inventario.Achar(id, out var it) || it.item != Itens.DaPeca(k)) return false;
            inventario.Tirar(id);
            byte velha = this[k];
            this[k] = (byte)Mathf.Clamp(it.qtd, 0, 100);
            if (velha != Vazio && !inventario.Receber(Itens.DaPeca(k), velha))
                ItensNoMundo.Instancia?.Criar(Itens.DaPeca(k), inventario.transform.position + inventario.transform.right * 0.4f, inventario.transform.eulerAngles.y, velha);
            return true;
        }
    }

    /// <summary>
    /// Um capô que se abre (o do carro que se dirige ou o de um carro da cidade): o que se vê olhando o motor
    /// (<see cref="Player.TelaDoCapo"/>) e o que se pede. Os pedidos vão para o anfitrião, que confere a distância.
    /// </summary>
    public interface ICapo
    {
        /// <summary>O nome que aparece no alto da tela ("Sua perua", "Sedã cinza").</summary>
        string NomeDoCarro { get; }
        /// <summary>O cofre do motor em 3D, o que se olha (a câmera vai até o <see cref="CofreDoMotor.olhar"/>).</summary>
        CofreDoMotor Cofre { get; }
        PecasDoMotor Pecas { get; }
        /// <summary>O meio do capô (a tela fecha longe dele).</summary>
        Vector3 Ponto { get; }
        /// <summary>Ainda dá para mexer (o carro existe, está parado e de pé).</summary>
        bool Aberto { get; }
        /// <summary>Por que não dá para trocar as peças agora (o motor ligado), ou null.</summary>
        string Impedimento { get; }
        /// <summary>Uma linha a mais no alto da tela (a carga e a temperatura do motor), ou null.</summary>
        string Situacao { get; }
        void PedirTirar(int peca);
        void PedirPor(int peca, ushort idNaMochila);
        /// <summary>O jogador local começou ou parou de olhar o motor (o capô sobe e desce para todos).</summary>
        void Olhando(bool olhando);
    }
}
