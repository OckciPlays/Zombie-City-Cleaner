using UnityEngine;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O alarme dos carros abandonados (a pedido em 29/10/2026), em Audio/Resources/SonsDoAlarme.asset (o
    /// <see cref="PecasDosCarrosDoMundo"/> carrega pelo nome): as sirenes (Audio/Carro/Alarme_*.wav, do
    /// Ferramentas/alarmes.py) e o material das lanternas do pisca-alerta. Refeito pelo menu Zombie City Cleaner >
    /// Veículos > Montar o alarme dos carros.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie City Cleaner/Sons do alarme", fileName = "SonsDoAlarme")]
    public class SonsDoAlarme : ScriptableObject
    {
        [Tooltip("As sirenes (cada carro tem sempre a mesma), tocadas em laço.")]
        public AudioClip[] sirenes;
        [Tooltip("As lanternas do pisca-alerta, acesas (o PiscaAlerta.mat, que brilha).")]
        public Material luz;
    }
}
