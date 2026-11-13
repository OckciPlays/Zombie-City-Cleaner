using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O chão do terreno do Hospital Principal no mundo sorteado (o Plano_Hospital.md da pasta de arte, a parte 7,
    /// 08/11/2026), nas coordenadas da obra, no chão de fora (o embasamento: 45 cm abaixo do térreo). O terreno do
    /// quarteirão fica plano 5 cm abaixo (DadosDoMundo.AcimaDoQuarteirao) e aparece onde não há calçada (sem as plantas: a
    /// floresta não planta no lote do hospital). Cabe no <see cref="PlantaDoHospital.TerrenoNoMundo"/>:
    ///  - as calçadas de 2,5 m em volta do corpo (elas tapam a beira do buraco do terreno embaixo do subsolo);
    ///  - a praça da frente, da varanda até a calçada da rua, e o estacionamento do lado leste dela, com as vagas;
    ///  - o acesso das ambulâncias, da rua da frente até o pátio na frente dos portões da garagem (o pé da rampa);
    ///  - o pátio de serviço no fundo, com a caçamba e os contêineres;
    ///  - duas ambulâncias na garagem (caixas do tamanho de uma, como na cena de teste) e os frades na frente dos degraus.
    /// Sem a rua, as árvores e os carros da cena de teste: no mundo, a rua e as calçadas em volta são da cidade.
    /// </summary>
    public static class TerrenoDoHospital
    {
        /// <summary>A praça, o estacionamento, o acesso e o pátio das ambulâncias e o pátio de serviço (nas coordenadas da obra).</summary>
        public static readonly Rect Praca = Rect.MinMaxRect(-14f, -30f, 14f, -16.05f),
                                    Estacionamento = Rect.MinMaxRect(14f, -30f, 31.5f, -18.5f),
                                    PatioDasAmbulancias = Rect.MinMaxRect(-50.5f, -6.5f, -43.05f, 9.5f),
                                    Acesso = Rect.MinMaxRect(-50.5f, -30f, -44f, -6.5f),
                                    PatioDeServico = Rect.MinMaxRect(-2f, 18.5f, 18f, 21.5f);

        /// <summary>A largura das calçadas em volta do corpo.</summary>
        public const float Calcada = 2.5f;

        public static void Montar(Obra o)
        {
            const float c = ChaoDeFora, g = 0.3f;
            float x0 = X0 - Calcada, x1 = X1 + Calcada, z0 = Z0 - Calcada, z1 = Z1 + Calcada;
            // as calçadas em volta do corpo (a da frente dos dois lados da praça; o estacionamento fica na frente dela)
            o.Laje(x0, Praca.xMin, z0, Z0 - 0.05f, c, g, "Calcada", true);
            o.Laje(Praca.xMax, x1, z0, Z0 - 0.05f, c, g, "Calcada", true);
            o.Laje(x0, x1, Z1 + 0.05f, z1, c, g, "Calcada", true);
            o.Laje(x0, X0 - 0.05f, Z0 - 0.05f, Z1 + 0.05f, c, g, "Calcada", true);
            o.Laje(X1 + 0.05f, x1, Z0 - 0.05f, Z1 + 0.05f, c, g, "Calcada", true);
            // a praça, com as juntas a cada 2 m
            o.Laje(Praca.xMin, Praca.xMax, Praca.yMin, Praca.yMax, c, g, "Calcada", true);
            for (float x = Praca.xMin + 2f; x < Praca.xMax - 0.5f; x += 2f)
                o.Caixa(new Vector3(x, c + 0.002f, (Praca.yMin + Praca.yMax) / 2f), new Vector3(0.03f, 0.004f, Praca.height), "Concreto");
            // o estacionamento, com as vagas
            o.Laje(Estacionamento.xMin, Estacionamento.xMax, Estacionamento.yMin, Estacionamento.yMax, c, g, "Asfalto", true);
            for (float x = Estacionamento.xMin + 1.4f; x < Estacionamento.xMax - 1f; x += 2.8f)
                o.Caixa(new Vector3(x, c + 0.004f, Estacionamento.yMin + 3.2f), new Vector3(0.12f, 0.008f, 5f), "ListraBranca");
            // o acesso e o pátio das ambulâncias (a rampa dos portões é da casca)
            o.Laje(Acesso.xMin, Acesso.xMax, Acesso.yMin, Acesso.yMax, c, g, "Asfalto", true);
            o.Laje(PatioDasAmbulancias.xMin, PatioDasAmbulancias.xMax, PatioDasAmbulancias.yMin, PatioDasAmbulancias.yMax, c, g, "Asfalto", true);
            // o pátio de serviço, com a caçamba e os contêineres do lixo
            o.Laje(PatioDeServico.xMin, PatioDeServico.xMax, PatioDeServico.yMin, PatioDeServico.yMax, c, g, "Concreto", true);
            o.Caixa(new Vector3(13.5f, c + 0.9f, 20.2f), new Vector3(3.6f, 1.8f, 2f), "Cacamba", colide: true);
            o.Caixa(new Vector3(4.5f, c + 0.6f, 20.6f), new Vector3(1.4f, 1.2f, 1.1f), "Lixeira", colide: true);
            o.Caixa(new Vector3(6.2f, c + 0.6f, 20.6f), new Vector3(1.4f, 1.2f, 1.1f), "Lixeira", colide: true);
            // os frades amarelos na frente dos degraus da varanda
            foreach (float x in new[] { -3.3f, -1.1f, 1.1f, 3.3f })
            {
                var p = new Vector3(x, c, Hospital.Varanda.yMin - 1.4f);
                o.Cilindro(p, 0.1f, 0.1f, 0.9f, 8, "PlacaAmarela");
                o.Cilindro(p + Vector3.up * 0.9f, 0.1f, 0.06f, 0.06f, 8, "PlacaAmarela");
                o.Caixa(p + Vector3.up * 0.45f, new Vector3(0.2f, 0.9f, 0.2f), "Concreto", colide: true, visual: false);
            }
            // as duas ambulâncias nas vagas 2 e 3 da garagem, de frente para os portões
            foreach (float vaga in new[] { Vagas[1], Vagas[2] })
                Ambulancia(o, new Vector3(GaragemDasAmbulancias.xMin + 5.2f, 0f, vaga + Z0), 90f);
        }

        // uma ambulância em caixas (o comprimento ao longo do z local, a cabine em -z), como as da cena de teste
        static void Ambulancia(Obra o, Vector3 meio, float giro)
        {
            var M = Matrix4x4.TRS(meio, Quaternion.Euler(0f, giro, 0f), Vector3.one);
            o.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 1.55f, 0.7f)), new Vector3(2.3f, 2.5f, 4.1f), "CarroBranco", colide: true);
            o.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 1.15f, -2.05f)), new Vector3(2.2f, 1.7f, 1.6f), "CarroBranco", colide: true);
            o.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 1.55f, 0.7f)), new Vector3(2.32f, 0.3f, 4.12f), "PlacaVermelha");
            o.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 2.05f, -2.0f)), new Vector3(1.9f, 0.6f, 0.9f), "VidroCarro");
            foreach (float x in new[] { -1.0f, 1.0f })
                foreach (float z in new[] { -2.1f, 1.6f })
                    o.CaixaM(M * Matrix4x4.Translate(new Vector3(x, 0.42f, z)), new Vector3(0.32f, 0.84f, 0.84f), "Pneu");
        }
    }
}
