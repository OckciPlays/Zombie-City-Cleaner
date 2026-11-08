using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O fôlego e o afogamento (06/11/2026, a pedido: "conseguir nadar ... afogamento, fôlego também"). O anfitrião conta,
    /// pela posição do jogador (a que vem pela rede): com os olhos embaixo da água (<see cref="Agua.Submerso"/>), o fôlego
    /// cai de 100 a 0 em <see cref="SegundosDeFolego"/> s; fora dela, volta em 4 s. Sem fôlego, ele se afoga: a vida cai
    /// <see cref="AfogandoPorSegundo"/> por segundo (do tronco, os pulmões) até subir ou morrer ("Afogou-se."). Sentado no
    /// carro ou no caiaque, nada conta. O dono vê a barra e a água na tela (<see cref="AguaNaTela"/>).
    /// <para>
    /// O nado rápido (07/11/2026): na água funda, na tona e mais rápido que o nado normal (o Shift), o fôlego cai de 100 a 0
    /// em <see cref="SegundosDeNadoRapido"/> s e não volta; o dono para de nadar rápido quando ele acaba (sem se afogar).
    /// </para>
    /// </summary>
    public partial class VidaJogador
    {
        public const float SegundosDeFolego = 30f, AfogandoPorSegundo = 8f, SegundosDeNadoRapido = 45f;
        // acima disto (m/s) na água funda, é o nado rápido (o normal vai até 1,9; o rápido, 2,8)
        const float VelocidadeDoNadoRapido = 2.3f;

        /// <summary>O fôlego (0 a 100), escrito pelo anfitrião.</summary>
        public readonly NetworkVariable<byte> Folego = new(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        float _folego = 100f, _afogando;
        PassageiroDoCarro _passageiroNaAgua;

        // no anfitrião, a cada quadro
        void AtualizarFolego(float dt)
        {
            if (_passageiroNaAgua == null) _passageiroNaAgua = GetComponent<PassageiroDoCarro>();
            bool sentado = (_passageiroNaAgua != null && _passageiroNaAgua.NoCarro) || PassageiroDoCaiaque.Sentado(this);
            bool embaixo = !Caido.Value && !sentado && Agua.Submerso(transform.position);
            bool rapido = !embaixo && !Caido.Value && !sentado && _rede != null && _rede.Velocidade > VelocidadeDoNadoRapido
                          && Agua.Nivel(transform.position, out float nivel) && nivel - transform.position.y > 1.05f;
            if (embaixo) _folego = Mathf.Max(0f, _folego - 100f / SegundosDeFolego * dt);
            else if (rapido) _folego = Mathf.Max(0f, _folego - 100f / SegundosDeNadoRapido * dt);
            else _folego = Mathf.Min(100f, _folego + 25f * dt);
            if (embaixo && _folego <= 0f)
            {
                _afogando += AfogandoPorSegundo * dt;
                if (_afogando >= 1f)
                {
                    float d = Mathf.Floor(_afogando);
                    _afogando -= d;
                    Perder(d, "Afogou-se.");
                }
            }
            else _afogando = 0f;
            byte b = (byte)Mathf.CeilToInt(_folego);
            if (b != Folego.Value) Folego.Value = b;
        }
    }
}
