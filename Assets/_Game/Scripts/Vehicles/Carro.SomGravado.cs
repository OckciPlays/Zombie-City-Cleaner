using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O som gravado do carro (19/11/2026, a segunda leva da repaginada; Vehicle - Essentials em Resources/Carro):
    ///  - o motor dos carros (não das motos, que têm os laços delas) passa a tocar em camadas como o da moto
    ///    (<see cref="SomEmCamadas"/>): a marcha lenta, 1.000 e 2.000 rpm, misturados pelo giro. Desde 20/11/2026 (o
    ///    Pedro: "todos os carros possuem o mesmo som"), cada modelo tem o seu conjunto (a perua, o cupê, mais agudo, e a
    ///    caminhonete, grave), e cada carro sai um pouco mais grave ou agudo que outro do mesmo modelo (até 6%, pelo
    ///    NetworkObjectId, o mesmo em todas as máquinas);
    ///  - a batida (no dono, que simula a física; todos ouvem): o metal amassando, mais forte e grave quanto maior a
    ///    mudança de velocidade.
    /// </summary>
    public partial class Carro
    {
        bool _motorGravadoVisto;
        // o tom do motor (vezes o pitch de todas as camadas; a moto fica em 1): menor que 1, mais grave
        float _tomDoMotor = 1f;

        // os laços gravados no lugar do laço único feito em código (uma vez por carro; a moto e quem já tem os seus ficam)
        void UsarMotorGravado()
        {
            _motorGravadoVisto = true;
            if (moto || (lacosDoMotor != null && lacosDoMotor.Length >= 2)) return;
            char conjunto = chaveDoVeiculo == Loot.Item.ChaveDaCaminhonete ? 'c' : chaveDoVeiculo == Loot.Item.ChaveDoCupe ? 'b' : 'a';
            var lacos = SomDoCarro.LacosDoMotor(conjunto);
            if (lacos == null || lacos.Length < 3) return;
            lacosDoMotor = lacos;
            giroDosLacos = new[] { 1f, 1.3f, 2.6f };
            // o tom (só o pitch; a mistura das camadas pelo giro continua a mesma): a caminhonete mais grave, o cupê mais
            // agudo, e cada carro até 6% para cima ou para baixo
            float tom = conjunto == 'c' ? 0.87f : conjunto == 'b' ? 1.1f : 1f;
            uint semente = IsSpawned ? (uint)NetworkObjectId * 2654435761u : 0u;
            _tomDoMotor = tom * (0.94f + (semente >> 24) / 255f * 0.12f);
            giroNoMaximo = 3.4f;
        }

        // no dono: a batida forte o bastante para se ouvir (a partir de 2,5 m/s de mudança)
        void SomDaBatida(Vector3 ponto, float mudanca)
        {
            if (mudanca < 2.5f || SomDoCarro.Batida(mudanca) == null) return;
            SomDaBatidaRpc(ponto, mudanca);
        }

        [Rpc(SendTo.Everyone)]
        void SomDaBatidaRpc(Vector3 ponto, float mudanca)
        {
            var clip = SomDoCarro.Batida(mudanca);
            if (clip == null) return;
            Efeitos.Som(clip, ponto, Mathf.Clamp(0.35f + mudanca * 0.07f, 0.35f, 1f), true, Mathf.Clamp(40f + mudanca * 8f, 40f, 140f));
        }
    }
}
