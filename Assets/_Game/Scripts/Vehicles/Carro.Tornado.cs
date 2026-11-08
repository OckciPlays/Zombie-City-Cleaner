using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O tornado no carro e na moto (07/11/2026, a pedido: "ele vai poder puxar o nosso carro se estivermos mais perto (e
    /// girar em torno dele por um breve período), isso funcionará para o caiaque e moto"). Na máquina que move o carro (o
    /// dono: quem dirige, ou o anfitrião com ele vazio), no fim do FixedUpdate, com a física ligada (o carro parado longe de
    /// todos, cinemático, não sente):
    /// <list type="bullet">
    /// <item><b>O puxão</b> (até <see cref="PuxaOCarro"/> m): uma aceleração para o tornado e para o lado do giro, mais forte
    /// perto, e um pouco para cima (alivia o peso nas rodas: o carro escorrega de lado). De longe dá para fugir acelerando;
    /// perto, não. A moto, mais leve, sente mais (<see cref="MaisNaMoto"/>).</item>
    /// <item><b>O giro</b> (no miolo, <see cref="MioloDoCarro"/> m; a moto, <see cref="MioloDaMoto"/>): ele sobe uns 3 a 5 m e
    /// roda em volta do tornado, girando em volta de si e balançando, por 2 a 3,5 s; depois é jogado para fora e cai (pode
    /// cair virado: o R desvira). Por 6 s depois disso ele não pega de novo, só puxa.</item>
    /// </list>
    /// Quem está sentado vai junto (preso ao banco). O dano em quem está perto continua o do anfitrião (CicloDoDia.Tempo).
    /// </summary>
    public partial class Carro
    {
        const float PuxaOCarro = 45f, ForcaNoCarro = 9f, MioloDoCarro = 11f, MioloDaMoto = 13f, MaisNaMoto = 1.35f;
        float _tornadoGiraAte = -1f, _tornadoPegaDepois, _tornadoChao, _tornadoAltura;

        /// <summary>O tornado está girando este carro agora (na máquina que o move).</summary>
        public bool GirandoNoTornado => _tornadoGiraAte > 0f && Time.time < _tornadoGiraAte;

        void NoTornado()
        {
            if (_corpo == null || _corpo.isKinematic || !Redemoinho.Ativo)
            {
                _tornadoGiraAte = -1f;
                return;
            }
            var pos = _corpo.worldCenterOfMass;
            if (!Redemoinho.Onde(pos, out _, out _, out float d) || d > PuxaOCarro)
            {
                _tornadoGiraAte = -1f;
                return;
            }
            float forca = Tempo.ForcaDoTornado;
            float leve = moto ? MaisNaMoto : 1f;
            if (_tornadoGiraAte > 0f)
            {
                if (Time.time < _tornadoGiraAte && forca > 0.3f)
                {
                    // a velocidade do giro, perseguida com uma aceleração limitada, sem a gravidade
                    var alvo = Redemoinho.Giro(pos, _tornadoChao, moto ? 5.5f : 7f, moto ? 13f : 12f, _tornadoAltura);
                    var acelera = Vector3.ClampMagnitude((alvo - _corpo.linearVelocity) * 3.5f, 35f) - Physics.gravity;
                    _corpo.AddForce(acelera, ForceMode.Acceleration);
                    // rodando em volta de si e balançando
                    float t = Time.time;
                    var gira = Vector3.up * (2.2f - _corpo.angularVelocity.y) * 3f
                               + transform.forward * (Mathf.Sin(t * 2.3f) * 2.5f) + transform.right * (Mathf.Sin(t * 1.7f + 1f) * 2f);
                    _corpo.AddTorque(gira, ForceMode.Acceleration);
                    return;
                }
                // acabou: joga para fora, e por uns segundos só puxa
                _tornadoGiraAte = -1f;
                _tornadoPegaDepois = Time.time + 6f;
                _corpo.AddForce(Redemoinho.Arremesso(pos, 9f, 5f, 4f) * leve, ForceMode.VelocityChange);
                return;
            }
            float k = Mathf.Pow(1f - d / PuxaOCarro, 1.4f) * forca;
            _corpo.AddForce(Redemoinho.Puxao(pos, PuxaOCarro, ForcaNoCarro * leve) + Vector3.up * (5f * k * k * leve), ForceMode.Acceleration);
            if (d < (moto ? MioloDaMoto : MioloDoCarro) && forca > 0.5f && Time.time >= _tornadoPegaDepois)
            {
                _tornadoGiraAte = Time.time + Random.Range(2f, 3.5f);
                _tornadoChao = pos.y;
                _tornadoAltura = Random.Range(3f, 5f) * (moto ? 1.2f : 1f);
            }
        }
    }
}
