using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O veneno da cascavel (12/11/2026, a etapa 4 da fauna; o plano aprovado: sem item novo). Uma em quatro picadas é seca
    /// (sem veneno, como na vida real). Nas outras, o membro picado perde vida aos poucos por <see cref="SegundosDoVeneno"/> s
    /// (de 25 a 40 no total), com a dor na mente e o enjoo; o curativo não cura (passa sozinho), mas pode matar quem já
    /// está fraco. O símbolo "Envenenado" (EstadosDoJogador) fica até passar. No anfitrião; o tempo que falta vai pela rede
    /// (o símbolo do dono). Quem cai ou renasce fica limpo.
    /// </summary>
    public partial class VidaJogador
    {
        public const float SegundosDoVeneno = 240f, ChanceDaPicadaSeca = 0.25f;

        /// <summary>Os segundos de veneno que faltam (0: limpo).</summary>
        public readonly NetworkVariable<float> Veneno = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        ParteDoCorpo _parteDoVeneno = ParteDoCorpo.PernaE;
        float _venenoPorSegundo, _venenoJunto, _proximaDorDoVeneno;

        /// <summary>
        /// Só no anfitrião: a picada da cascavel na parte (o golpe já tirou a vida dele). Verdadeiro se veio o veneno (a
        /// picada seca não envenena).
        /// </summary>
        public bool Picada(ParteDoCorpo parte)
        {
            if (!IsServer || !IsSpawned || Caido.Value) return false;
            if (Random.value < ChanceDaPicadaSeca)
            {
                AvisarAoDono("A cascavel picou, mas foi uma picada seca: sem veneno.");
                return false;
            }
            _parteDoVeneno = parte;
            _venenoPorSegundo = Random.Range(25f, 40f) / SegundosDoVeneno;
            Veneno.Value = SegundosDoVeneno;
            _proximaDorDoVeneno = Time.time + 20f;
            AbalarAgora(6f, MotivoDaMente.Dor);
            Enjoar("A picada da cascavel: o veneno está agindo (a dor e o enjoo; o curativo não cura, passa sozinho)", "Veneno de cascavel.");
            return true;
        }

        /// <summary>No anfitrião, a cada quadro (a fauna chama): o veneno tira a vida do membro picado, de 1 em 1.</summary>
        public void CorrerVeneno(float dt)
        {
            if (!IsServer || !IsSpawned || Veneno.Value <= 0f) return;
            if (Caido.Value)
            {
                Veneno.Value = 0f;
                return;
            }
            Veneno.Value = Mathf.Max(0f, Veneno.Value - dt);
            _venenoJunto += _venenoPorSegundo * dt;
            if (_venenoJunto >= 1f)
            {
                float d = Mathf.Floor(_venenoJunto);
                _venenoJunto -= d;
                PorNaParte(_parteDoVeneno, EmPorcento(d));
                ConferirAVida("Veneno de cascavel.");
            }
            if (Time.time > _proximaDorDoVeneno)
            {
                _proximaDorDoVeneno = Time.time + 30f;
                AbalarAgora(1.5f, MotivoDaMente.Dor);
            }
            if (Veneno.Value <= 0f) AvisarAoDono("O veneno da cascavel passou.");
        }
    }
}
