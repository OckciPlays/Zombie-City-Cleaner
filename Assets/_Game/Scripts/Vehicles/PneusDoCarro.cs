using System;
using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// As 4 rodas de um carro (na ordem dos WheelColliders: dianteira esquerda, dianteira direita, traseira esquerda e
    /// traseira direita). Cada uma é um byte: <see cref="Vazio"/> sem a roda (o carro fica no cubo), ou o estado do
    /// pneu, de 0 a 100%. Vai pela rede.
    /// <para>
    /// O que cada uma faz no carro (<see cref="Carro"/>):
    /// - abaixo de <see cref="Minimo"/>, o pneu está furado: a roda baixa (o aro quase no chão), segura menos e arrasta,
    ///   e o carro puxa para aquele lado e anda menos;
    /// - acima, quanto mais gasto, menos ele segura (o carro escorrega mais nas curvas e na freada);
    /// - sem a roda, o cubo arrasta no chão: o carro quase não anda.
    /// </para>
    /// </summary>
    public struct PneusDoCarro : INetworkSerializeByMemcpy, IEquatable<PneusDoCarro>
    {
        public const int Quantas = 4;
        /// <summary>Sem a roda.</summary>
        public const byte Vazio = 255;
        /// <summary>Abaixo disso (%) o pneu está furado.</summary>
        public const int Minimo = 10;
        /// <summary>O raio da roda inteira, o do pneu furado (o aro e a borracha amassada) e o do cubo (sem a roda), em m.</summary>
        public const float RaioBom = 0.30f, RaioFurado = 0.215f, RaioDoCubo = 0.07f;

        public byte de, dd, te, td;

        public byte this[int i]
        {
            get => i == 0 ? de : i == 1 ? dd : i == 2 ? te : td;
            set
            {
                if (i == 0) de = value;
                else if (i == 1) dd = value;
                else if (i == 2) te = value;
                else td = value;
            }
        }

        public bool Tem(int i) => this[i] != Vazio;
        /// <summary>O estado do pneu (0 a 100); sem a roda, 0.</summary>
        public int Estado(int i) => Tem(i) ? this[i] : 0;
        public bool Furado(int i) => Tem(i) && this[i] < Minimo;

        public bool Equals(PneusDoCarro o) => de == o.de && dd == o.dd && te == o.te && td == o.td;

        /// <summary>Os 4 pneus (%; -1, sem a roda).</summary>
        public static PneusDoCarro Com(int[] estados)
        {
            var p = new PneusDoCarro();
            for (int i = 0; i < Quantas; i++)
            {
                int v = estados != null && i < estados.Length ? estados[i] : 100;
                p[i] = v < 0 ? Vazio : (byte)Mathf.Clamp(v, 0, 100);
            }
            return p;
        }

        // ------------------------------------------------------------------ as regras

        /// <summary>O nome de cada roda ("a dianteira esquerda").</summary>
        public static readonly string[] Nomes = { "dianteira esquerda", "dianteira direita", "traseira esquerda", "traseira direita" };

        /// <summary>O raio do WheelCollider desta roda (furada, ela baixa; sem ela, o cubo).</summary>
        public float Raio(int i) => !Tem(i) ? RaioDoCubo : Furado(i) ? RaioFurado : RaioBom;

        /// <summary>O quanto a roda segura (multiplica a rigidez do atrito): o pneu gasto segura menos.</summary>
        public float Aderencia(int i) => !Tem(i) ? 0.22f : Furado(i) ? 0.42f : Mathf.Lerp(0.72f, 1f, Estado(i) / 100f);

        /// <summary>A força (N) com que a roda arrasta no chão, contra o movimento: o pneu furado e o cubo sem a roda.</summary>
        public float Arrasto(int i) => !Tem(i) ? 3200f : Furado(i) ? 1400f : 0f;

        /// <summary>Quantas rodas estão ruins (furadas ou faltando).</summary>
        public int Ruins
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Quantas; i++) if (!Tem(i) || Furado(i)) n++;
                return n;
            }
        }

        /// <summary>O modelo da roda (o pneu cheio): o raio e a metade da largura, para o tiro achar a roda.</summary>
        public const float RaioDoModelo = 0.30f, MeiaLargura = 0.105f;

        /// <summary>O que o pneu faz neste estado (uma frase curta, para o inventário).</summary>
        public static string Efeito(int estado) =>
            estado < Minimo ? "furado: o carro puxa e anda mal" : estado < 40 ? "gasto: escorrega nas curvas" : "segura bem";
    }
}
