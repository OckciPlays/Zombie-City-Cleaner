using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O exemplo das construções feitas pelo jogo (Plano_Mapa, "As construções: Blender ou jogo"): uma rua com casas
    /// sorteadas pela semente e levantadas na hora pelo kit (Casa, Obra). No começo do lado norte, para comparar:
    /// a Casa_1 do Blender, a mesma casa refeita pelo jogo com as medidas de hoje e a mesma com a escala nova.
    /// Montado por Editor/MontagemDoExemploDasCasas (menu Zombie City Cleaner > Exemplos > Casas geradas pelo jogo).
    /// </summary>
    [ExecuteAlways]
    public class RuaDeExemplo : CenaDeExemplo
    {
        [Range(1, 8)] public int casasPorLado = 5;
        [Tooltip("As casas sorteadas com as medidas de hoje (as da demo), em vez da escala nova.")]
        public bool escalaDeHoje;
        public Mesh casaDoBlender, casaDoBlenderColisao;

        protected override string Chave => $"{semente}|{casasPorLado}|{escalaDeHoje}";

        protected override void Montar()
        {
            var medidas = escalaDeHoje ? Medidas.Hoje : Medidas.DoJogo;

            Casa Levantar(Planta planta, Medidas med)
            {
                var casa = new Casa(planta, med);
                casa.Montar();
                return casa;
            }

            // o lado norte: as três para comparar e as sorteadas
            float xn = -60f;
            if (casaDoBlender != null)
            {
                var go = Objeto("Casa 1 (o modelo do Blender)", raiz, casaDoBlender, casaDoBlenderColisao, new Vector3(xn + 7.5f, 0f, Frente + 4f), 0f);
                Construcoes.Add(go);
                Caminho(new Vector2(xn + 7.5f - 2f, Frente + 4f - 6.3f), Calcada);
                resumo.AppendLine($"Casa 1 (o modelo do Blender): 10,0 × 8,0 m, {casaDoBlender.triangles.Length / 3:N0} triângulos");
                xn += 15f;
            }
            xn = PorCasa(Levantar(PlantasDeCasa.Demo(), Medidas.Hoje), "Casa 1 refeita pelo jogo (medidas de hoje)", xn, 1, out _);
            xn = PorCasa(Levantar(PlantasDeCasa.Demo(), Medidas.DoJogo), "Casa 1 refeita pelo jogo (escala nova)", xn, 1, out _);
            xn += 4f;
            float xs = -60f;
            for (int i = 0; i < casasPorLado * 2; i++)
            {
                int lado = i % 2 == 0 ? 1 : -1;
                int s = Misturar(semente, i, 977);
                var planta = PlantasDeCasa.Sortear(new System.Random(s), medidas);
                string tipo = planta.molde == "Terrea" ? "térrea" : "sobrado";
                string nome = $"Casa {i + 1}: {tipo}{(planta.porao ? " com porão" : "")} (semente {s})";
                var casa = Levantar(planta, medidas);
                if (lado > 0) xn = PorCasa(casa, nome, xn, 1, out _);
                else xs = PorCasa(casa, nome, xs, -1, out _);
            }
            Rua(-70f, Mathf.Max(xn, xs) + 15f);
            resumo.AppendLine($"Casas geradas: {triangulos:N0} triângulos ao todo. Semente {semente}, escala {(escalaDeHoje ? "de hoje" : "nova")}.");
        }
    }
}
