using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// Uma bancada de trabalho (31/10/2026, a pedido: "não precisa colocar para fazer a bancada - a bancada pode existir em
    /// alguns lugares específicos, como no bunker, em algumas áreas de povoados"): as receitas mais difíceis
    /// (<see cref="Receita.bancada"/>) só saem perto de uma. Fica na cena (a montagem do bunker põe a dele); E nela abre a
    /// aba Criar da tela do inventário. Desde 25/11/2026, uma mesa só, padrão: a mesa de oficina (a do bunker, as das casas,
    /// que eram bancadas de madeira e ganharam o modelo dela, e a que o jogador monta, <see cref="ConstrucoesNoMundo.MesaPerto"/>);
    /// a diferença entre a bancada e a oficina acabou (<see cref="oficina"/> ficou só como marca).
    /// </summary>
    public class Bancada : Interagivel
    {
        /// <summary>Até onde, dos pés do jogador ao meio da bancada, ela vale (m).</summary>
        public const float Alcance = 3f;

        /// <summary>A mesa de oficina (06/11/2026: a do bunker): as receitas maiores; também vale como bancada.</summary>
        public bool oficina;

        /// <summary>A altura do tampo da mesa de oficina (m, no espaço dela, do chão; o modelo do Blender/oficina.py).</summary>
        public const float TampoDaOficina = 0.95f;

        /// <summary>A altura do tampo no espaço desta bancada (m; -1: não se sabe, e a animação do craft usa a caixa dela).</summary>
        public float alturaDoTampo = -1f;

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
            // (25/11/2026) a mesa de ferramentas montada pelos jogadores
            var mundo = ConstrucoesNoMundo.Instancia;
            return !soAOficina && mundo != null && mundo.MesaPerto(ponto);
        }

        /// <summary>A bancada da cena mais perto do ponto (a até o alcance; ou só a mesa de oficina), para a animação do craft; null sem nenhuma.</summary>
        public static Bancada MaisPerto(Vector3 ponto, bool soAOficina = false)
        {
            Bancada melhor = null;
            float menor = Alcance * Alcance;
            foreach (var b in _todas)
            {
                if (b == null || (soAOficina && !b.oficina)) continue;
                var d = b.transform.position - ponto;
                d.y *= 2f;
                if (d.sqrMagnitude > menor) continue;
                menor = d.sqrMagnitude;
                melhor = b;
            }
            return melhor;
        }

        public override string Dica(ArmaJogador quem) => "[E] Usar a mesa de oficina (criar)";

        public override void Usar(ArmaJogador quem)
        {
            var tela = quem.GetComponent<TelaDoInventario>();
            if (tela != null) tela.AbrirNaAbaCriar();
        }
    }
}
