using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// Uma bancada de trabalho (31/10/2026, a pedido: "não precisa colocar para fazer a bancada - a bancada pode existir em
    /// alguns lugares específicos, como no bunker, em algumas áreas de povoados"): as receitas mais difíceis
    /// (<see cref="Receita.bancada"/>) só saem perto de uma. Fica na cena (a montagem do bunker põe a dele); E nela abre a
    /// aba Criar da tela do inventário.
    /// </summary>
    public class Bancada : Interagivel
    {
        /// <summary>Até onde, dos pés do jogador ao meio da bancada, ela vale (m).</summary>
        public const float Alcance = 3f;

        /// <summary>A mesa de oficina (06/11/2026: a do bunker): as receitas maiores; também vale como bancada.</summary>
        public bool oficina;

        static readonly List<Bancada> _todas = new List<Bancada>();

        void OnEnable() => _todas.Add(this);
        void OnDisable() => _todas.Remove(this);

        /// <summary>Há uma bancada perto do ponto (os pés do jogador).</summary>
        public static bool Perto(Vector3 ponto) => Perto(ponto, false);

        /// <summary>Há uma mesa de oficina perto do ponto (os pés do jogador).</summary>
        public static bool PertoDaOficina(Vector3 ponto) => Perto(ponto, true);

        static bool Perto(Vector3 ponto, bool soAOficina)
        {
            foreach (var b in _todas)
            {
                if (b == null || (soAOficina && !b.oficina)) continue;
                var d = b.transform.position - ponto;
                d.y *= 2f;   // a de outro andar não vale
                if (d.sqrMagnitude <= Alcance * Alcance) return true;
            }
            return false;
        }

        public override string Dica(ArmaJogador quem) => oficina ? "[E] Usar a mesa de oficina (criar)" : "[E] Usar a bancada (criar)";

        public override void Usar(ArmaJogador quem)
        {
            var tela = quem.GetComponent<TelaDoInventario>();
            if (tela != null) tela.AbrirNaAbaCriar();
        }
    }
}
