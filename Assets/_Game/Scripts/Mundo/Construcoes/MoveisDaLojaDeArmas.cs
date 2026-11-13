using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDaLojaDeArmas;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os móveis da loja de armas (o Plano_LojaDeArmas.md), um por um (a loja é uma só, como o bunker), pelo nome do modelo
    /// da mobília nova (o lote da loja de armas, o <c>mobilia_loja_armas.py</c>, e os de antes: o escritório, o banheiro, as
    /// estantes, as caixas, o acampamento), nas coordenadas do esboço (x de 0 a 24, z de 0 a 30), com as variações da semente
    /// (<see cref="PlantaDaLojaDeArmas.Variacao"/>): quais balcões de vidro estão quebrados, a gôndola tombada, a barricada da
    /// entrada, a baia quebrada, o acampamento no estande e os três estados da sala segura (A: a porta arrombada e aberta, os
    /// cofres arrombados, as caixas vazias jogadas; B: a porta entreaberta, um cofre e duas caixas fechados; C: a porta
    /// trancada, tudo fechado e no lugar). O giro é o da Unity: 0 olha para +z, 180 para -z, 90 para +x e -90 para -x. Os
    /// móveis vão em ×1,30; o que encaixa na construção (a porta de cofre, as baias, os carros dos alvos), em ×1,00.
    /// </summary>
    public static class MoveisDaLojaDeArmas
    {
        public struct Movel
        {
            public string modelo;
            /// <summary>Nas coordenadas da obra (o chão em y = 0; os pendurados, a origem no teto).</summary>
            public Vector3 lugar;
            public float giro, escala;
            public bool caixa;
            /// <summary>A porta de cofre: 0 aberta (arrombada), 1 entreaberta, 2 fechada e trancada; -1 nos outros.</summary>
            public int porta;
        }

        /// <summary>Os modelos no tamanho final (encaixam na construção).</summary>
        public static readonly HashSet<string> TamanhoFinal = new HashSet<string>
        {
            "PortaDoCofre", "PortaDoCofreArrombada", "BaiaDeTiro", "BaiaDeTiroQuebrada", "CarroDeAlvo", "CarroDeAlvoRasgado",
        };

        /// <summary>Os que não têm caixa de colisão (os pendurados, os de parede, os do chão rasos e a porta de cofre).</summary>
        static bool SemCaixa(string m) => m.StartsWith("CarroDeAlvo") || m == "AlvosNoChao" || m == "CabecaDeCervo" || m == "SuporteDeAbafadores"
                                          || m == "DispenserDePapel" || m == "EspelhoArmario" || m == "QuadroDeAvisos" || m == "Computador"
                                          || m.StartsWith("PortaDoCofre") || m.StartsWith("Tapete");

        /// <summary>
        /// As caixas de colisão próprias (no espaço do modelo, no tamanho final): a baia de tiro tem a divisória e a bancada da
        /// linha (com a chapa embaixo), e o atirador fica no vão entre elas (a caixa inteira fechava a baia).
        /// </summary>
        public static readonly Dictionary<string, (Vector3 centro, Vector3 tamanho)[]> CaixasProprias = new Dictionary<string, (Vector3, Vector3)[]>
        {
            ["BaiaDeTiro"] = new[] { (new Vector3(-0.66f, 1.175f, 0.525f), new Vector3(0.08f, 2.35f, 1.45f)), (new Vector3(0.04f, 0.54f, 0.02f), new Vector3(1.28f, 1.08f, 0.46f)) },
            ["BaiaDeTiroQuebrada"] = new[] { (new Vector3(-0.66f, 0.65f, 0.525f), new Vector3(0.08f, 1.3f, 1.45f)), (new Vector3(0.04f, 0.54f, 0.02f), new Vector3(1.28f, 1.08f, 0.46f)) },
        };

        static float Escala(string m) => TamanhoFinal.Contains(m) ? 1f : MedidasDaMobilia.Escala;

        /// <summary>Os modelos pedidos que faltam na tabela das medidas (monte a mobília nova).</summary>
        public static readonly HashSet<string> Faltam = new HashSet<string>();

        static Movel Solto(string m, float x, float z, float giro = 0f, float y = 0f) => new Movel
        {
            modelo = m, lugar = new Vector3(X(x), y, Z(z)), giro = giro, escala = Escala(m), caixa = !SemCaixa(m) && y < 0.3f, porta = -1,
        };

        /// <summary>Um móvel encostado: as costas na face da parede ('face', x ou z do esboço, conforme o giro) e o meio em 's'.</summary>
        static Movel Enc(string m, float giro, float face, float s, float y = 0f)
        {
            float e = Escala(m);
            float costas = MedidasDaMobilia.Tem(m) ? (MedidasDaMobilia.Meio(m).y - MedidasDaMobilia.Real(m).z / 2f) * e : 0f;
            float d = -costas + 0.02f;
            int g = Mathf.RoundToInt(giro);
            float x = g == 90 ? face + d : g == -90 ? face - d : s;
            float z = g == 0 ? face + d : (g == 180 || g == -180) ? face - d : s;
            return Solto(m, x, z, giro, y);
        }

        // as faces das paredes de cada cômodo (a linha, meia parede e o acabamento: o lambri na loja, os painéis no estande,
        // o concreto grosso na sala segura)
        const float VendasO = 0.14f, VendasL = 15.46f, VendasF = 0.14f, VendasT = 21.86f;
        const float EscO = 11.54f, EscL = 15.46f, EscT = 29.86f;
        const float EstO = 6.52f, EstL = 11.28f, EstF = 22.12f, EstT = 29.88f;
        const float SegO = 0.26f, SegL = 6.14f, SegF = 22.26f, SegT = 29.74f;
        const float AntO = 15.74f, AntL = 20.46f, AntF = 0.14f, AntT = 6.86f;
        const float BanL = 23.88f, BanT = 3.48f;
        const float DepL = 23.88f;
        const float TirO = 15.76f, TirL = 23.84f, TirF = 7.16f;

        /// <summary>Os móveis da loja desta variação (na ordem de pôr), feitos de novo a cada chamada.</summary>
        public static List<Movel> Da(Variacao v)
        {
            var l = new List<Movel>();
            var r = new System.Random(v.semente * 104729 + 7);
            bool Chance(double p) => r.NextDouble() < p;
            float Sorte(float a, float b) => a + (float)r.NextDouble() * (b - a);

            // ---------------------------------------------------------------- a área de vendas
            // o balcão de vidro em L: o braço do oeste (o vendedor do lado da parede, giro -90) e o do fundo (o vendedor atrás,
            // giro 0), com o caixa na ponta, perto do escritório; quais quebrados, pela semente
            var lugares = new List<(float x, float z, float g)>
            {
                (2.4f, 8.2f, -90), (2.4f, 10.25f, -90), (2.4f, 12.3f, -90), (2.4f, 14.35f, -90), (2.4f, 16.4f, -90),
                (3.05f, 19.0f, 0), (5.09f, 19.0f, 0), (7.13f, 19.0f, 0), (9.17f, 19.0f, 0),
            };
            var quebrados = new HashSet<int>();
            while (quebrados.Count < Mathf.Min(v.vitrinesQuebradas, lugares.Count)) quebrados.Add(r.Next(lugares.Count));
            for (int i = 0; i < lugares.Count; i++)
                l.Add(Solto(quebrados.Contains(i) ? "BalcaoVitrineQuebrado" : "BalcaoVitrine", lugares[i].x, lugares[i].z, lugares[i].g));
            l.Add(Solto("BalcaoCaixa", 11.25f, 19.05f, 0));
            l.Add(Solto("CadeiraEscritorio", 11.2f, 20.45f, 180 + Sorte(-30f, 30f)));
            l.Add(Solto("LixeiraPequena", 12.55f, 20.2f));
            l.Add(Solto("CaixaDePapelao", 6.1f, 20.95f, Sorte(-20f, 20f)));
            l.Add(Solto("CaixaDePapelao", 1.2f, 6.4f, Sorte(-30f, 30f)));
            // as armas longas atrás do balcão (a parede oeste e a do fundo) e o painel entre as portas do fundo
            foreach (float z in new[] { 9.4f, 12.75f, 16.1f, 19.5f }) l.Add(Enc("ExpositorArmasLongas", 90, VendasO, z));
            foreach (float x in new[] { 1.95f, 5.3f }) l.Add(Enc("ExpositorArmasLongas", 180, VendasT, x));
            l.Add(Enc("PainelPerfurado", 180, VendasT, 11.15f));
            // a munição na parede oeste, perto da frente, e uma estante na parede do estande
            foreach (float z in new[] { 1.15f, 2.75f, 4.35f }) l.Add(Enc("EstanteMunicao", 90, VendasO, z));
            l.Add(Enc("EstanteMunicao", -90, VendasL, 1.6f));
            // os acessórios na parede do estande e o troféu
            foreach (float z in new[] { 9.4f, 12.1f, 14.8f }) l.Add(Enc("PainelPerfurado", -90, VendasL, z));
            l.Add(Enc("CabecaDeCervo", -90, VendasL, 18.6f, 1.95f));
            // as três gôndolas do meio (a do meio derrubada, às vezes)
            l.Add(Solto("GondolaLoja", 8.8f, 7.6f, 0));
            l.Add(v.gondolaTombada ? Solto("GondolaTombada", 8.6f, 10.6f, Chance(0.5) ? 0 : 180) : Solto("GondolaLoja", 8.8f, 10.4f, 180));
            l.Add(Solto("GondolaLoja", 8.8f, 13.2f, 0));
            // a barricada velha perto da entrada (dos primeiros dias), sem fechar a porta
            if (v.barricada) l.Add(Solto("Barricada", 4.3f, 2.7f, Sorte(-16f, -6f)));

            // ---------------------------------------------------------------- o escritório
            l.Add(Enc("MesaEscritorio", 180, EscT, 13.5f));
            l.Add(Enc("Computador", 180, EscT, 13.24f, 0.99f));
            l.Add(Solto("CadeiraEscritorio", 13.24f, 28.35f, Sorte(-25f, 25f)));
            l.Add(Enc("Arquivo", 90, EscO, 25.7f));
            l.Add(Enc("Arquivo", 90, EscO, 26.35f));
            l.Add(Enc("EstanteArquivo", -90, EscL, 25.6f));
            l.Add(Enc("CofrePequeno", -90, EscL, 28.95f));
            l.Add(Enc("QuadroDeAvisos", 90, EscO, 28.3f, 1.2f));
            l.Add(Solto("LixeiraPequena", 12.0f, 29.35f));

            // ---------------------------------------------------------------- o estoque
            foreach (float z in new[] { 23.4f, 25.1f, 26.8f }) l.Add(Enc("EstanteAco120", -90, EstL, z));
            l.Add(Enc("EstanteAco90", 90, EstO, 23.4f));
            l.Add(Solto("Palete", 7.35f, 29.05f, 0));
            l.Add(Solto("CaixaDePapelao", 7.1f, 29.0f, Sorte(-15f, 15f), 0.188f));
            l.Add(Solto("CaixaDePapelao", 7.7f, 29.1f, Sorte(-15f, 15f), 0.188f));
            l.Add(Solto("CarrinhoDeCarga", 10.2f, 28.7f, 200f));
            l.Add(Solto("CaixaDePapelao", 9.8f, 23.6f, Sorte(0f, 90f)));
            if (v.sala == EstadoDaSala.Saqueada) l.Add(Solto("CaixaMilitarVazia", 8.6f, 25.9f, Sorte(10f, 60f)));

            // ---------------------------------------------------------------- a sala segura (pelo estado)
            foreach (float x in new[] { 1.1f, 2.75f, 4.4f }) l.Add(Enc("EstanteAco120", 180, SegT, x));
            l.Add(Enc("RackDeFuzis", 90, SegO, 27.9f));
            l.Add(Enc("Bancada", 0, SegF, 2.2f));
            var porta = Solto(v.sala == EstadoDaSala.Saqueada ? "PortaDoCofreArrombada" : "PortaDoCofre", 6.4f, PortaDoCofre.z - Z0, 90);
            porta.lugar = PortaDoCofre;
            porta.porta = (int)v.sala;
            l.Add(porta);
            switch (v.sala)
            {
                case EstadoDaSala.Saqueada:
                    l.Add(Enc("CofreDeArmasArrombado", 90, SegO, 23.5f));
                    l.Add(Enc("CofreDeArmasArrombado", 90, SegO, 25.9f));
                    l.Add(Solto("Palete", 3.9f, 26.6f, 75));
                    l.Add(Solto("CaixaMilitarVazia", 2.9f, 24.6f, Sorte(100f, 140f)));
                    l.Add(Solto("CaixaMilitarVazia", 4.7f, 28.4f, Sorte(-40f, -10f)));
                    l.Add(Solto("CaixaDePapelao", 5.2f, 23.4f, Sorte(0f, 90f)));
                    break;
                case EstadoDaSala.Parcial:
                    l.Add(Enc("CofreDeArmas", 90, SegO, 23.5f));
                    l.Add(Enc("CofreDeArmasArrombado", 90, SegO, 25.9f));
                    l.Add(Solto("Palete", 3.7f, 26.4f, 90));
                    l.Add(Solto("CaixaMilitar", 3.7f, 25.95f, 90, 0.188f));
                    l.Add(Solto("CaixaMilitar", 3.7f, 26.85f, 90, 0.188f));
                    l.Add(Solto("CaixaMilitarVazia", 4.9f, 23.9f, Sorte(20f, 60f)));
                    l.Add(Solto("LatasDeMunicao", 5.4f, 28.5f, Sorte(0f, 40f)));
                    break;
                default:
                    l.Add(Enc("CofreDeArmas", 90, SegO, 23.5f));
                    l.Add(Enc("CofreDeArmas", 90, SegO, 24.65f));
                    l.Add(Solto("Palete", 3.7f, 26.4f, 90));
                    l.Add(Solto("CaixaMilitar", 3.7f, 25.95f, 90, 0.188f));
                    l.Add(Solto("CaixaMilitar", 3.7f, 26.85f, 90, 0.188f));
                    l.Add(Solto("CaixaMilitar", 3.7f, 26.4f, 0, 0.578f));
                    l.Add(Solto("LatasDeMunicao", 5.3f, 23.6f, 0));
                    l.Add(Solto("LatasDeMunicao", 5.3f, 28.5f, 15));
                    break;
            }

            // ---------------------------------------------------------------- a antessala, o banheiro e o depósito
            l.Add(Enc("MesaEscritorio", -90, AntL, 3.55f));
            l.Add(Solto("CadeiraEscritorio", 19.25f, 3.55f, 90 + Sorte(-20f, 20f)));
            l.Add(Enc("SuporteDeAbafadores", 90, AntO, 1.6f, 1.25f));
            l.Add(Enc("BancoDeEspera3", 180, AntT, 16.85f));
            l.Add(Enc("Privada", -90, BanL, 1.0f));
            l.Add(Enc("PiaColuna", 180, BanT, 22.9f));
            l.Add(Enc("EspelhoArmario", 180, BanT, 22.9f, 1.25f));
            l.Add(Enc("DispenserDePapel", 180, BanT, 23.6f, 1.3f));
            l.Add(Solto("LixeiraPequena", 22.0f, 3.0f));
            l.Add(Enc("EstanteAco90", -90, DepL, 5.3f));
            l.Add(Solto("CaixaDePapelao", 22.6f, 4.25f, Sorte(0f, 40f)));
            l.Add(Solto("AlvosNoChao", 22.6f, 6.35f, Sorte(0f, 90f)));

            // ---------------------------------------------------------------- o estande
            // as baias (uma quebrada, às vezes), os carros dos alvos entre os defletores (um corredor sem) e os alvos caídos
            for (int i = 0; i < Baias; i++)
            {
                float x = MeioDaBaia(i) - X0;
                l.Add(Solto(i == v.baiaQuebrada ? "BaiaDeTiroQuebrada" : "BaiaDeTiro", x, LinhaDeTiro - Z0, 180));
            }
            float[] paradas = { 11.8f, 14.65f, 17.95f, 21.25f, 24.6f };
            int semAlvo = r.Next(Baias);
            for (int i = 0; i < Baias; i++)
            {
                if (i == semAlvo) continue;
                string m = Chance(0.4) ? "CarroDeAlvoRasgado" : "CarroDeAlvo";
                l.Add(Solto(m, MeioDaBaia(i) - X0, paradas[r.Next(paradas.Length)], 180, PeDireito));
            }
            l.Add(Solto("AlvosNoChao", 20.6f, 22.0f, Sorte(0f, 180f)));
            l.Add(Solto("AlvosNoChao", 22.4f, 15.2f, Sorte(0f, 180f)));
            // a área de espera: a mesa do instrutor, os abafadores e o banco (ou a barricada dos sobreviventes, que fecharam a
            // saída de emergência)
            l.Add(Enc("MesaEscritorio", 0, TirF, 16.6f));
            l.Add(Solto("CadeiraEscritorio", 16.6f, 8.45f, 180 + Sorte(-30f, 30f)));
            l.Add(Enc("SuporteDeAbafadores", 90, TirO, 8.3f, 1.25f));
            if (!v.acampamento) l.Add(Enc("BancoDeEspera3", 0, TirF, 21.2f));
            else
            {
                l.Add(Solto("Barricada", 22.3f, 7.85f, 0));
                // o acampamento no fundo dos corredores 1 e 2 (perto da passagem): o colchonete, o saco de dormir, as latas, o
                // fogareiro, as garrafas e as caixas empilhadas
                l.Add(Solto("Colchonete", 17.4f, 16.6f, 90 + Sorte(-8f, 8f)));
                l.Add(Solto("SacoDeDormir", 18.9f, 18.6f, 80 + Sorte(-10f, 10f)));
                l.Add(Solto("Latas", 18.6f, 15.8f, Sorte(0f, 90f)));
                l.Add(Solto("Fogareiro", 19.4f, 17.0f, Sorte(0f, 90f)));
                l.Add(Solto("Garrafas", 16.5f, 19.7f, Sorte(0f, 90f)));
                l.Add(Solto("CaixaDePapelao", 16.4f, 14.6f, 5));
                l.Add(Solto("CaixaDePapelao", 16.45f, 14.6f, -8, 0.39f));
                l.Add(Solto("CaixaMilitarVazia", 20.0f, 19.4f, Sorte(0f, 90f)));
            }

            // ---------------------------------------------------------------- por fora: as caixas militares na calçada e os paletes no beco
            l.Add(Solto("CaixaMilitar", 13.9f, -1.0f, 4));
            l.Add(Solto("LatasDeMunicao", 15.0f, -0.9f, 30));
            l.Add(Solto("CaixaDePapelao", 13.0f, -0.8f, 25));
            l.Add(Solto("Palete", 3.4f, 31.3f, 10, ChaoDeFora));
            l.Add(Solto("Palete", 3.5f, 31.3f, 4, ChaoDeFora + 0.144f));
            l.Add(Solto("Palete", 15.9f, 31.6f, 80, ChaoDeFora));

            Faltam.Clear();
            foreach (var m in l) if (!MedidasDaMobilia.Tem(m.modelo)) Faltam.Add(m.modelo);
            return l;
        }

        /// <summary>
        /// As caixas de colisão dos móveis na malha de colisão (como as do bunker e do hospital): o tamanho menos o recuo de
        /// cada lado, em camadas nos altos; as próprias (as baias) do jeito delas.
        /// </summary>
        public static void Caixas(Variacao v, MalhaDeObra colisao)
        {
            foreach (var m in Da(v))
            {
                if (!m.caixa || !MedidasDaMobilia.Tem(m.modelo)) continue;
                var giro = Quaternion.Euler(0f, m.giro, 0f);
                if (CaixasProprias.TryGetValue(m.modelo, out var proprias))
                {
                    foreach (var (centro, tamanho) in proprias)
                        colisao.Caixa(Matrix4x4.TRS(m.lugar + giro * (centro * m.escala), giro, Vector3.one), tamanho * m.escala, "Concreto");
                    continue;
                }
                Vector3 t = MedidasDaMobilia.Real(m.modelo) * m.escala;
                Vector2 meio = MedidasDaMobilia.Meio(m.modelo) * m.escala;
                float baixo = MedidasDaMobilia.Baixo(m.modelo) * m.escala;
                Vector3 c = m.lugar + giro * new Vector3(meio.x, 0f, meio.y);
                var tam = new Vector3(Mathf.Max(0.1f, t.x - 2f * Mobilia.Recuo), t.y, Mathf.Max(0.1f, t.z - 2f * Mobilia.Recuo));
                int camadas = t.y > 1.7f ? Mathf.CeilToInt(t.y / 0.85f) : 1;
                float h = t.y / camadas;
                for (int k = 0; k < camadas; k++)
                    colisao.Caixa(Matrix4x4.TRS(c + Vector3.up * (baixo + h * (k + 0.5f)), giro, Vector3.one), new Vector3(tam.x, h, tam.z), "Concreto");
            }
        }
    }
}
