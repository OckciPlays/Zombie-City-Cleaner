using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDaLojaDeArmas;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os pontos dos sons da loja de armas (o Plano_LojaDeArmas.md, a decisão 10): os sons do hospital (o
    /// <c>Ferramentas/hospital_sons.py</c>, nos mesmos tipos do <see cref="SonsDoHospital.Tipos"/>), raros e de pontos da loja,
    /// para o silêncio ser o principal. Uma área só (a loja é de um pavimento).
    /// <list type="bullet">
    /// <item>Vento: as vitrines e as janelas de fora (as quebradas assobiam).</item>
    /// <item>Porta: as portas de dentro (a folha mexendo com o vento).</item>
    /// <item>Metal: os defletores e os trilhos do estande, as estantes do estoque e a porta de cofre.</item>
    /// <item>Estrutura: o meio da área de vendas e do estande (a cobertura de metal estalando).</item>
    /// <item>Papeis: o caixa e o escritório (os papéis mexendo com o vento).</item>
    /// <item>Goteira: o banheiro, o estoque e o fundo do estande.</item>
    /// <item>Pancada: longe, no meio da loja.</item>
    /// <item>Zumbi: no estado C, dentro da sala segura (o dono).</item>
    /// </list>
    /// </summary>
    public static class SonsDaLojaDeArmas
    {
        public static Vector2[] Areas() => new[] { new Vector2(-1f, TopoDaCobertura + 2f) };

        public static Bounds Limites()
        {
            var b = new Bounds();
            b.SetMinMax(new Vector3(X0 - 1f, -1f, Z0 - 3f), new Vector3(X1 + 1f, TopoDaCobertura + 2f, Z1 + 1f));
            return b;
        }

        /// <summary>Os pontos (o lugar na obra, o tipo no <see cref="SonsDoHospital.Tipos"/> e a área, sempre 0).</summary>
        public static List<(Vector3 lugar, int tipo, int area)> Pontos(Variacao v)
        {
            var l = new List<(Vector3, int, int)>();
            int T(string nome) => System.Array.IndexOf(SonsDoHospital.Tipos, nome);
            foreach (var j in Janelas)
            {
                float meio = (j.de + j.ate) / 2f;
                Vector3 p = j.lado switch
                {
                    0 => new Vector3(meio, 1.8f, Z0 + 0.6f),
                    1 => new Vector3(X1 - 0.6f, 1.8f, meio),
                    2 => new Vector3(meio, 1.8f, Z1 - 0.6f),
                    _ => new Vector3(X0 + 0.6f, 3.0f, meio),
                };
                l.Add((p, T("Vento"), 0));
            }
            l.Add((new Vector3(MeioDaEntrada, 1.4f, Z0 + 0.8f), T("Vento"), 0));
            foreach (var p in PlantaPronta.portas)
            {
                if (p.b == null || EhACofre(p)) continue;
                var a = p.a; var c = p.b;
                Vector2 xz = Mathf.Abs(a.x1 - c.x0) < 0.01f ? new Vector2(a.x1, p.pos) : Mathf.Abs(a.x0 - c.x1) < 0.01f ? new Vector2(a.x0, p.pos)
                           : Mathf.Abs(a.z1 - c.z0) < 0.01f ? new Vector2(p.pos, a.z1) : new Vector2(p.pos, a.z0);
                l.Add((new Vector3(xz.x, 1.1f, xz.y), T("Porta"), 0));
            }
            // o metal: os defletores do estande, o anteparo, as estantes do estoque e a porta de cofre
            for (float z = LinhaDeTiro + 3f; z < ComecoDoAnteparo; z += 6f) l.Add((new Vector3(Estande.center.x, 3.3f, z), T("Metal"), 0));
            l.Add((new Vector3(Estande.center.x, 1.6f, ComecoDoAnteparo + 0.8f), T("Metal"), 0));
            l.Add((new Vector3(Estoque.xMax - 0.8f, 1.6f, Estoque.center.y), T("Metal"), 0));
            l.Add((PortaDoCofre + new Vector3(0.6f, 1.2f, 0f), T("Metal"), 0));
            // a estrutura: o meio da loja e o do estande, perto da cobertura
            l.Add((new Vector3(Vendas.center.x, 3.4f, Vendas.center.y), T("Estrutura"), 0));
            l.Add((new Vector3(Estande.center.x, 3.5f, Estande.center.y), T("Estrutura"), 0));
            // os papéis: o caixa e o escritório
            l.Add((new Vector3(X(11.2f), 0.3f, Z(20.3f)), T("Papeis"), 0));
            l.Add((new Vector3(Escritorio.center.x, 0.3f, Escritorio.center.y), T("Papeis"), 0));
            // as goteiras
            l.Add((new Vector3(Banheiro.center.x, 0.3f, Banheiro.center.y), T("Goteira"), 0));
            l.Add((new Vector3(Estoque.center.x, 0.3f, Estoque.yMax - 1.5f), T("Goteira"), 0));
            l.Add((new Vector3(Estande.center.x, 0.3f, ComecoDoAnteparo - 2f), T("Goteira"), 0));
            // a pancada longe e, no estado C, o dono batendo dentro do cofre
            l.Add((new Vector3(Vendas.center.x, 1.2f, Vendas.center.y), T("Pancada"), 0));
            if (v.sala == EstadoDaSala.Intacta) l.Add((new Vector3(SalaSegura.center.x, 1.2f, SalaSegura.center.y), T("Zumbi"), 0));
            return l;
        }

        /// <summary>Os pesos de cada tipo na loja (na ordem do <see cref="SonsDoHospital.Tipos"/>): sem goteira demais, sem cano e sem maca.</summary>
        public static readonly float[] Pesos = { 1.5f, 3f, 1f, 2f, 0f, 1.5f, 1f, 0.6f, 0f, 1f };
    }
}
