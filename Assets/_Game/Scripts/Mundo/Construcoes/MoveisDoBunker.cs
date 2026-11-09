using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDeBunker;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os móveis do bunker novo (o Plano_Bunker.md da pasta de arte, a parte 4), um por um, escritos à mão (o bunker é um
    /// só): a mobília nova e os objetos do lote do bunker (o <c>mobilia_bunker.py</c>), pelo nome do modelo, nas coordenadas
    /// do esboço (x de 0 a 32, z de 0 a 24, como a <see cref="PlantaDeBunker"/>). Os de parede (<see cref="Enc"/>) ficam com
    /// as costas na face da parede (pela medida de cada modelo, a <see cref="MedidasDaMobilia"/>), os soltos
    /// (<see cref="Solto"/>) com a origem no ponto. O giro é o da Unity: 0 olha para +z (as costas numa parede de z menor),
    /// 180 para -z, 90 para +x e -90 para -x.
    /// Os móveis vão em ×1,30, como no mundo; o que encaixa na construção (o portão, as portas, os canos), em ×1,00.
    /// As caixas de colisão de cada andar entram na malha de colisão dele (<see cref="Caixas"/>), como as da mobília do mundo.
    /// Do B0 ao B4 (as três fatias da parte 4); o refeitório e os quartos 1 a 4 saem de ajudantes
    /// (<see cref="Refeitorio"/>, <see cref="QuartoDoCorredor"/>).
    /// </summary>
    public static class MoveisDoBunker
    {
        public struct Movel
        {
            public int andar;
            public string modelo;
            public Vector3 lugar;   // nas coordenadas da obra (já com o chão do andar)
            public float giro, escala;
            public bool caixa;
        }

        /// <summary>Os modelos em tamanho final (encaixam na construção).</summary>
        public static readonly HashSet<string> TamanhoFinal = new HashSet<string>
        {
            "Portao", "PortaDeAco", "PortaCamaraFria", "CanoReto", "CanoCurva", "CanoT", "Registro", "SuporteDeCano", "Duto",
            "DutoCurva", "Grelha", "TorreDeVentilacao", "Antena",
        };

        /// <summary>Os que não têm caixa de colisão (os da parede, os tapetes e o que fica em cima de outro).</summary>
        static bool SemCaixa(string m) => m.StartsWith("Tapete") || m.StartsWith("Quadro") || m == "MapaDaParede" || m == "Toalheiro" || m == "Computador";

        static float Escala(string m) => TamanhoFinal.Contains(m) ? 1f : MedidasDaMobilia.Escala;

        /// <summary>Um móvel solto: a origem em (x, z) do esboço, a y do chão do andar.</summary>
        static Movel Solto(int f, string m, float x, float z, float giro = 0f, float y = 0f) => new Movel
        {
            andar = f, modelo = m, lugar = new Vector3(x + X0, Base(f) + y, z + Z0), giro = giro, escala = Escala(m), caixa = !SemCaixa(m) && y < 0.3f,
        };

        /// <summary>
        /// Um móvel encostado: as costas na face da parede (x ou z do esboço, conforme o giro) e o meio dele em 's' ao longo
        /// dela, a y do chão do andar.
        /// </summary>
        static Movel Enc(int f, string m, float giro, float face, float s, float y = 0f)
        {
            float e = Escala(m);
            // o fundo da caixa do modelo (as costas ficam em -z dele): o meio menos metade do fundo, na escala
            float costas = MedidasDaMobilia.Tem(m) ? (MedidasDaMobilia.Meio(m).y - MedidasDaMobilia.Real(m).z / 2f) * e : 0f;
            float d = -costas + 0.02f;
            int g = Mathf.RoundToInt(giro);
            float x = g == 90 ? face + d : g == -90 ? face - d : s;
            float z = g == 0 ? face + d : (g == 180 || g == -180) ? face - d : s;
            return Solto(f, m, x, z, giro, y);
        }

        static readonly Movel[] Fixos =
        {
            // ---------------------------------------------------------------- B0: a sala de operações
            Enc(0, "EstacaoDeRadio", 0, 0.2f, 3.0f),
            Solto(0, "CadeiraEscritorio", 3.0f, 2.05f, 180),
            Enc(0, "MapaDaParede", 90, 0.2f, 4.3f, 1.05f),
            Solto(0, "Mesa6", 6.8f, 4.8f),
            Solto(0, "Cadeira", 6.05f, 4.02f), Solto(0, "Cadeira", 7.55f, 4.02f),
            Solto(0, "Cadeira", 6.05f, 5.58f, 180), Solto(0, "Cadeira", 7.55f, 5.58f, 180),
            Solto(0, "Cadeira", 5.44f, 4.8f, 90), Solto(0, "Cadeira", 8.16f, 4.8f, -90),
            Enc(0, "Arquivo", 0, 0.2f, 6.0f), Enc(0, "Arquivo", 0, 0.2f, 6.66f),
            Enc(0, "EstanteAco90", 90, 0.2f, 1.5f), Enc(0, "EstanteAco90", 90, 0.2f, 7.3f),
            // os postos na janela da garagem: a mesa, o computador (em cima dela, na frente do vão da cadeira) e a cadeira
            Enc(0, "MesaEscritorio", -90, 11.8f, 2.6f), Enc(0, "Computador", -90, 11.8f, 2.86f, 0.99f),
            Solto(0, "CadeiraEscritorio", 10.35f, 2.86f, 90),
            Enc(0, "MesaEscritorio", -90, 11.8f, 5.9f), Enc(0, "Computador", -90, 11.8f, 6.16f, 0.99f),
            Solto(0, "CadeiraEscritorio", 10.35f, 6.16f, 90),
            // o arsenal e a fabricação (o painel de armas é à parte, à esquerda da bancada)
            Enc(0, "Bancada", 180, 23.8f, 8.3f),
            Enc(0, "ArmarioDeAco", 90, 0.2f, 17.3f), Enc(0, "ArmarioDeAco", 90, 0.2f, 18.75f),
            Enc(0, "Vestiario", -90, 11.9f, 17.4f),
            Enc(0, "EstanteAco120", -90, 11.9f, 21.4f),
            Solto(0, "Mesa4", 6.4f, 19.6f),
            Solto(0, "Cadeira", 6.4f, 18.75f), Solto(0, "Cadeira", 6.4f, 20.45f, 180),
            // a segunda bancada (a fabricação), na parede da esquerda, depois dos armários
            Enc(0, "Bancada", 90, 0.2f, 21.6f),
            // a garagem, a oficina e o combustível
            Solto(0, "Macaco", 25.6f, 19.6f),
            Solto(0, "Pneus", 26.6f, 22.4f, 20),
            // a mesa de oficina (06/11/2026, a árvore de habilidades: as receitas maiores), no lugar da bancada da garagem
            Enc(0, "MesaDeOficina", -90, 31.8f, 11.0f),
            Enc(0, "EstanteAco90", -90, 31.8f, 8.0f),
            Enc(0, "EstanteAco120", -90, 31.8f, 14.5f), Enc(0, "EstanteAco120", -90, 31.8f, 16.2f),
            Solto(0, "Pneus", 30.0f, 20.6f, 35),
            Solto(0, "Tambores", 30.4f, 4.1f),
            Enc(0, "Galoes", 0, 0.2f, 30.4f),

            // ---------------------------------------------------------------- B1: o depósito e os banheiros
            Enc(1, "EstanteAco120", 90, 0.2f, 1.4f), Enc(1, "EstanteAco120", 90, 0.2f, 3.1f), Enc(1, "EstanteAco120", 90, 0.2f, 4.8f),
            Enc(1, "EstanteAco120", 0, 0.2f, 2.6f), Enc(1, "EstanteAco120", 0, 0.2f, 4.3f), Enc(1, "EstanteAco120", 0, 0.2f, 6.0f),
            Enc(1, "PrateleiraMadeira150", -90, 8.9f, 3.6f),
            // a ilha do meio: duas estantes de costas
            Enc(1, "EstanteAco120", 0, 4.55f, 4.5f), Enc(1, "EstanteAco120", 180, 4.55f, 4.5f),
            Enc(1, "Privada", 180, 23.8f, 1.1f), Enc(1, "Privada", 180, 23.8f, 2.3f), Enc(1, "Privada", 180, 23.8f, 3.5f), Enc(1, "Privada", 180, 23.8f, 4.7f),
            Enc(1, "Box", 90, 0.2f, 16.0f), Enc(1, "Box", 90, 0.2f, 17.3f),
            Enc(1, "PiaDupla", -90, 8.9f, 20.6f),
            Enc(1, "Toalheiro", 90, 0.2f, 19.6f, 1.0f),
            // a sala de estar: a TV entre as estantes baixas, o sofá de canto e as duas poltronas em volta da mesa de centro,
            // o canto de leitura (duas poltronas no tapete redondo) e a mesa de jogo de cartas
            Enc(1, "TVConsole", 0, 0.2f, 15.5f),
            Enc(1, "EstanteBaixa", 0, 0.2f, 13.0f), Enc(1, "EstanteBaixa", 0, 0.2f, 18.0f),
            Solto(1, "TapetePersa", 15.5f, 4.3f),
            Solto(1, "MesaDeCentro", 15.5f, 3.6f),
            Solto(1, "SofaL", 15.5f, 6.2f, 180),
            Solto(1, "Sofa1", 13.2f, 3.6f, 90), Solto(1, "Sofa1", 17.8f, 3.6f, -90),
            Enc(1, "EstanteLivros", -90, 21.9f, 2.0f), Enc(1, "EstanteLivros", -90, 21.9f, 9.6f),
            Solto(1, "AbajurDePe", 13.9f, 8.3f),
            Enc(1, "QuadroPaisagem", 90, 9.1f, 4.5f, 1.4f),
            Solto(1, "VasoPlanta", 20.9f, 0.9f),
            Solto(1, "TapeteRedondo", 11.6f, 7.4f),
            Solto(1, "Sofa1", 10.4f, 7.4f, 90), Solto(1, "Sofa1", 12.8f, 7.4f, -90),
            Solto(1, "MesaDeCanto", 11.6f, 7.4f),
            Solto(1, "Mesa4", 19.6f, 9.3f),
            Solto(1, "CadeiraEstofada", 19.2f, 8.4f), Solto(1, "CadeiraEstofada", 20.0f, 8.4f),
            Solto(1, "CadeiraEstofada", 19.2f, 10.2f, 180), Solto(1, "CadeiraEstofada", 20.0f, 10.2f, 180),
            // a sala de jogos
            Solto(1, "MesaSinuca", 17.0f, 18.3f),
            Solto(1, "MesaRedonda", 12.5f, 21.0f),
            Solto(1, "CadeiraEstofada", 12.5f, 20.08f), Solto(1, "CadeiraEstofada", 12.5f, 21.92f, 180),
            Solto(1, "CadeiraEstofada", 11.58f, 21.0f, 90), Solto(1, "CadeiraEstofada", 13.42f, 21.0f, -90),
            Enc(1, "Sofa3", 180, 23.8f, 22.5f),
            Enc(1, "EstanteBaixa", 180, 23.8f, 19.0f),
            Enc(1, "QuadroBarco", 180, 23.8f, 15.5f, 1.5f),
            Enc(1, "Sofa2", -90, 26.9f, 17.0f),
            Solto(1, "TapeteListras", 25.0f, 17.0f, 90),
            Solto(1, "MesaDeCentro", 24.95f, 17.0f, 90),
            Solto(1, "Sofa1", 23.6f, 17.0f, 90),
            Solto(1, "AbajurDePe", 25.9f, 22.9f),
            Enc(1, "EstanteLivros", 90, 9.1f, 17.6f),
            Solto(1, "Mesa4", 12.6f, 16.2f, 90),
            Solto(1, "Cadeira", 11.75f, 15.8f, 90), Solto(1, "Cadeira", 11.75f, 16.6f, 90),
            Solto(1, "Cadeira", 13.45f, 15.8f, -90), Solto(1, "Cadeira", 13.45f, 16.6f, -90),
            // a enfermaria e o isolamento
            Enc(1, "CamaHospital", -90, 31.8f, 1.6f), Enc(1, "CamaHospital", -90, 31.8f, 5.0f), Enc(1, "CamaHospital", -90, 31.8f, 8.4f),
            Solto(1, "Biombo", 30.2f, 3.3f), Solto(1, "Biombo", 30.2f, 6.7f),
            Enc(1, "MesaEscritorio", 0, 0.2f, 24.6f),
            Solto(1, "CadeiraEscritorio", 24.86f, 1.65f, 180),
            Enc(1, "ArmarioDeAco", 90, 22.1f, 9.8f),
            Enc(1, "PiaColuna", 90, 22.1f, 2.6f),
            Enc(1, "CamaHospital", -90, 31.8f, 15.6f), Enc(1, "CamaHospital", -90, 31.8f, 21.2f),
            Solto(1, "Biombo", 30.0f, 18.4f),

            // ---------------------------------------------------------------- B2: a lavagem e o lixo
            Enc(2, "PiaInox", 90, 0.2f, 1.6f), Enc(2, "BancadaInox", 90, 0.2f, 4.0f),
            Enc(2, "EstanteAco120", 90, 0.2f, 6.8f),
            Solto(2, "Tambores", 4.6f, 1.4f),
            Enc(2, "Galoes", 0, 0.2f, 7.4f),
            Solto(2, "BancadaInox", 4.8f, 5.6f, 90),
            // o refeitório (as mesas e os bancos vêm do Refeitorio): a bancada da água, a geladeira, os galões e o quadro
            Enc(2, "BancadaInox", -90, 31.8f, 4.0f), Enc(2, "GeladeiraGrande", -90, 31.8f, 7.2f), Enc(2, "Galoes", -90, 31.8f, 1.4f),
            Enc(2, "QuadroFlores", 0, 0.2f, 20.0f, 1.5f),
            // a cozinha: os dois fogões com as coifas no fundo, as bancadas, a pia, as geladeiras, a estante e as duas
            // bancadas de preparo no meio
            Enc(2, "FogaoIndustrial", 180, 23.8f, 12.4f), Enc(2, "FogaoIndustrial", 180, 23.8f, 14.3f),
            Enc(2, "Coifa", 180, 23.8f, 12.4f, 1.75f), Enc(2, "Coifa", 180, 23.8f, 14.3f, 1.75f),
            Enc(2, "BancadaInox", 180, 23.8f, 16.9f), Enc(2, "BancadaInox", 180, 23.8f, 19.35f),
            Enc(2, "PiaInox", -90, 20.9f, 14.6f),
            Enc(2, "GeladeiraGrande", 90, 9.1f, 15.3f), Enc(2, "GeladeiraGrande", 90, 9.1f, 16.45f),
            Enc(2, "EstanteAco120", 90, 9.1f, 22.4f),
            Solto(2, "BancadaInox", 13.9f, 19.0f), Solto(2, "BancadaInox", 16.4f, 19.0f, 180),
            // a câmara fria: as estantes em volta e os dois freezers no meio
            Enc(2, "EstanteAco120", 90, 0.2f, 16.4f), Enc(2, "EstanteAco120", 90, 0.2f, 18.1f),
            Enc(2, "EstanteAco120", 90, 0.2f, 19.8f), Enc(2, "EstanteAco120", 90, 0.2f, 21.5f),
            Enc(2, "EstanteAco120", 180, 23.8f, 1.8f), Enc(2, "EstanteAco120", 180, 23.8f, 3.5f),
            Enc(2, "EstanteAco120", 180, 23.8f, 5.2f), Enc(2, "EstanteAco120", 180, 23.8f, 6.9f),
            Enc(2, "EstanteAco120", -90, 8.9f, 16.4f), Enc(2, "EstanteAco120", -90, 8.9f, 22.6f),
            Enc(2, "EstanteAco120", 0, 15.1f, 2.4f), Enc(2, "EstanteAco120", 0, 15.1f, 4.1f),
            Solto(2, "FreezerHorizontal", 3.6f, 19.6f, 90), Solto(2, "FreezerHorizontal", 5.4f, 19.6f, -90),
            // a despensa: as estantes nas paredes, as duas ilhas de estantes de costas, a prateleira e os freezers
            Enc(2, "EstanteAco120", 180, 23.8f, 22.5f), Enc(2, "EstanteAco120", 180, 23.8f, 24.2f),
            Enc(2, "EstanteAco120", 180, 23.8f, 25.9f), Enc(2, "EstanteAco120", 180, 23.8f, 27.6f),
            Enc(2, "EstanteAco120", 180, 23.8f, 29.3f), Enc(2, "EstanteAco120", 180, 23.8f, 31.0f),
            Enc(2, "EstanteAco120", -90, 31.8f, 13.5f), Enc(2, "EstanteAco120", -90, 31.8f, 15.2f),
            Enc(2, "EstanteAco120", -90, 31.8f, 16.9f), Enc(2, "EstanteAco120", -90, 31.8f, 18.6f),
            Enc(2, "EstanteAco120", -90, 31.8f, 20.3f),
            Enc(2, "EstanteAco120", 0, 12.1f, 23.0f), Enc(2, "EstanteAco120", 0, 12.1f, 24.7f),
            Enc(2, "EstanteAco120", 0, 12.1f, 26.4f), Enc(2, "EstanteAco120", 0, 12.1f, 28.1f),
            Enc(2, "EstanteAco120", 0, 12.1f, 29.8f),
            Enc(2, "EstanteAco120", 0, 16.0f, 24.0f), Enc(2, "EstanteAco120", 180, 16.0f, 24.0f),
            Enc(2, "EstanteAco120", 0, 16.0f, 25.7f), Enc(2, "EstanteAco120", 180, 16.0f, 25.7f),
            Enc(2, "EstanteAco120", 0, 16.0f, 27.4f), Enc(2, "EstanteAco120", 180, 16.0f, 27.4f),
            Enc(2, "EstanteAco120", 0, 19.6f, 24.0f), Enc(2, "EstanteAco120", 180, 19.6f, 24.0f),
            Enc(2, "EstanteAco120", 0, 19.6f, 25.7f), Enc(2, "EstanteAco120", 180, 19.6f, 25.7f),
            Enc(2, "EstanteAco120", 0, 19.6f, 27.4f), Enc(2, "EstanteAco120", 180, 19.6f, 27.4f),
            Enc(2, "PrateleiraMadeira150", 90, 21.1f, 14.5f),
            Enc(2, "FreezerHorizontal", 90, 21.1f, 20.6f), Enc(2, "FreezerHorizontal", 90, 21.1f, 22.1f),

            // ---------------------------------------------------------------- B3: os quartos 5 e 6 (de casal, nas pontas do corredor)
            Enc(3, "CamaCasal", 90, 0.2f, 4.3f), Enc(3, "CriadoMudo", 90, 0.2f, 2.65f), Enc(3, "CriadoMudo", 90, 0.2f, 5.95f),
            Enc(3, "GuardaRoupa3", 0, 0.2f, 2.2f), Enc(3, "Comoda3", 180, 8.5f, 2.2f), Solto(3, "TapeteFelpudo", 3.4f, 4.3f, 90),
            Enc(3, "CamaCasal", 90, 0.2f, 19.5f), Enc(3, "CriadoMudo", 90, 0.2f, 17.85f), Enc(3, "CriadoMudo", 90, 0.2f, 21.15f),
            Enc(3, "GuardaRoupa3", 180, 23.8f, 2.2f), Enc(3, "Comoda3", 0, 15.1f, 2.2f), Solto(3, "TapeteFelpudo", 3.4f, 19.5f, 90),
            // o dormitório: 9 beliches com um baú no pé de cada e os guarda-roupas
            Enc(3, "Beliche", 0, 0.2f, 10.6f), Enc(3, "Beliche", 0, 0.2f, 12.6f), Enc(3, "Beliche", 0, 0.2f, 14.6f),
            Enc(3, "Beliche", 0, 0.2f, 16.6f), Enc(3, "Beliche", 0, 0.2f, 18.6f),
            Solto(3, "Bau", 10.6f, 3.25f), Solto(3, "Bau", 12.6f, 3.25f), Solto(3, "Bau", 14.6f, 3.25f),
            Solto(3, "Bau", 16.6f, 3.25f), Solto(3, "Bau", 18.6f, 3.25f),
            Enc(3, "Beliche", 180, 10.9f, 10.6f), Enc(3, "Beliche", 180, 10.9f, 12.6f),
            Enc(3, "Beliche", 180, 10.9f, 16.6f), Enc(3, "Beliche", 180, 10.9f, 18.6f),
            Solto(3, "Bau", 10.6f, 7.85f, 180), Solto(3, "Bau", 12.6f, 7.85f, 180),
            Solto(3, "Bau", 16.6f, 7.85f, 180), Solto(3, "Bau", 18.6f, 7.85f, 180),
            Enc(3, "GuardaRoupa2", -90, 20.9f, 4.5f), Enc(3, "GuardaRoupa2", -90, 20.9f, 6.0f),
            // a lavanderia: as máquinas de lavar e as secadoras na parede da frente, o tanque, as estantes e a mesa de dobrar
            Enc(3, "MaquinaDeLavar", 0, 0.2f, 22.1f), Enc(3, "MaquinaDeLavar", 0, 0.2f, 23.25f),
            Enc(3, "Secadora", 0, 0.2f, 24.4f), Enc(3, "Secadora", 0, 0.2f, 25.55f),
            Enc(3, "PiaInox", 90, 21.1f, 4.6f), Enc(3, "EstanteAco90", 90, 21.1f, 7.6f),
            Enc(3, "EstanteAco120", -90, 26.9f, 4.0f), Enc(3, "EstanteAco120", -90, 26.9f, 6.0f),
            Solto(3, "Mesa4", 24.0f, 6.8f, 90),
            // a rouparia: os guarda-roupas, as estantes e a mesa de dobrar
            Enc(3, "GuardaRoupa3", 90, 27.1f, 1.6f), Enc(3, "GuardaRoupa3", 90, 27.1f, 3.6f), Enc(3, "GuardaRoupa3", 90, 27.1f, 5.6f),
            Enc(3, "EstanteAco120", -90, 31.8f, 1.4f), Enc(3, "EstanteAco120", -90, 31.8f, 3.1f),
            Enc(3, "EstanteAco120", -90, 31.8f, 4.8f), Enc(3, "EstanteAco120", -90, 31.8f, 6.5f),
            Enc(3, "EstanteAco120", -90, 31.8f, 8.2f),
            Solto(3, "Mesa4", 29.4f, 7.4f, 90),
            // o depósito
            Enc(3, "EstanteAco120", 90, 29.1f, 15.2f), Enc(3, "EstanteAco120", 90, 29.1f, 16.9f),
            Enc(3, "EstanteAco120", -90, 31.8f, 15.2f), Enc(3, "EstanteAco120", -90, 31.8f, 16.9f),
            Enc(3, "EstanteAco120", 180, 19.4f, 30.5f),
            // os banheiros e os chuveiros: as privadas e os boxes no fundo, as pias e os toalheiros na frente e a banheira
            Enc(3, "Privada", 180, 23.8f, 11.0f), Enc(3, "Privada", 180, 23.8f, 12.2f), Enc(3, "Privada", 180, 23.8f, 13.4f),
            Enc(3, "Privada", 180, 23.8f, 14.6f), Enc(3, "Privada", 180, 23.8f, 15.8f), Enc(3, "Privada", 180, 23.8f, 17.0f),
            Enc(3, "Box", 180, 23.8f, 20.0f), Enc(3, "Box", 180, 23.8f, 21.3f), Enc(3, "Box", 180, 23.8f, 22.6f),
            Enc(3, "Box", 180, 23.8f, 23.9f), Enc(3, "Box", 180, 23.8f, 25.2f), Enc(3, "Box", 180, 23.8f, 26.5f),
            Enc(3, "PiaDupla", 0, 19.6f, 12.5f), Enc(3, "PiaDupla", 0, 19.6f, 16.0f), Enc(3, "PiaDupla", 0, 19.6f, 19.5f),
            Enc(3, "Toalheiro", 0, 19.6f, 22.6f, 1.0f), Enc(3, "Toalheiro", 0, 19.6f, 24.6f, 1.0f), Enc(3, "Toalheiro", 0, 19.6f, 26.6f, 1.0f),
            Enc(3, "Banheira", -90, 31.8f, 21.75f),

            // ---------------------------------------------------------------- B4: o esgoto (a fossa, as bombas)
            Solto(4, "Cisterna", 2.2f, 4.2f, 90),
            Enc(4, "Bomba", 0, 0.2f, 5.5f), Enc(4, "Bomba", 0, 0.2f, 7.2f),
            // as peças: as estantes, a bancada, os armários, os tambores e os galões
            Enc(4, "EstanteAco120", 90, 0.2f, 16.2f), Enc(4, "EstanteAco120", 90, 0.2f, 17.9f),
            Enc(4, "EstanteAco120", 90, 0.2f, 19.6f), Enc(4, "EstanteAco120", 90, 0.2f, 21.3f),
            Enc(4, "Bancada", 180, 23.8f, 3.0f),
            Enc(4, "ArmarioDeAco", 180, 23.8f, 5.6f), Enc(4, "ArmarioDeAco", 180, 23.8f, 7.0f),
            Enc(4, "EstanteAco90", -90, 8.8f, 19.0f), Enc(4, "Galoes", -90, 8.8f, 21.4f),
            Solto(4, "Tambores", 4.6f, 18.6f),
            // a elétrica: os 3 quadros na parede da frente, os bancos de baterias nos lados e a mesa de controle
            Enc(4, "QuadroEletrico", 0, 0.2f, 11.5f), Enc(4, "QuadroEletrico", 0, 0.2f, 14.2f), Enc(4, "QuadroEletrico", 0, 0.2f, 16.9f),
            Enc(4, "Baterias", 90, 9.1f, 3.0f), Enc(4, "Baterias", 90, 9.1f, 5.2f), Enc(4, "Baterias", 90, 9.1f, 7.4f),
            Enc(4, "Baterias", -90, 19.9f, 2.3f), Enc(4, "Baterias", -90, 19.9f, 8.6f),
            Solto(4, "MesaEscritorio", 14.5f, 4.0f), Solto(4, "Computador", 14.8f, 4.0f, 0, 0.99f),
            Solto(4, "CadeiraEscritorio", 14.76f, 4.85f, 180),
            // o gerador: os dois geradores no meio, o tanque de diesel e os tambores no fundo, a bancada e o armário
            Solto(4, "Gerador", 12.5f, 18.5f), Solto(4, "Gerador", 17.0f, 18.5f),
            Enc(4, "TanqueDiesel", 180, 23.8f, 12.5f), Solto(4, "Tambores", 17.5f, 22.3f),
            Enc(4, "Bancada", 90, 9.1f, 16.5f), Enc(4, "ArmarioDeAco", 90, 9.1f, 19.5f),
            // a água: as duas cisternas, as bombas na frente, os filtros no fundo, a estante e os galões
            Solto(4, "Cisterna", 28.0f, 3.0f), Solto(4, "Cisterna", 28.0f, 8.6f),
            Enc(4, "Bomba", 0, 0.2f, 22.5f), Enc(4, "Bomba", 0, 0.2f, 24.2f),
            Enc(4, "FiltroDeAgua", 180, 11.9f, 22.5f), Enc(4, "FiltroDeAgua", 180, 11.9f, 25.0f),
            Enc(4, "EstanteAco90", 90, 20.1f, 9.5f), Enc(4, "Galoes", 90, 20.1f, 2.5f),
            // o ar: os filtros de ar (NBC) no fundo, os ventiladores na parede da direita, a bancada e a estante
            Enc(4, "FiltroDeAr", 180, 23.8f, 24.0f), Enc(4, "FiltroDeAr", 180, 23.8f, 28.0f),
            Enc(4, "Ventilador", -90, 31.8f, 16.0f), Enc(4, "Ventilador", -90, 31.8f, 19.5f),
            Enc(4, "Bancada", 0, 12.1f, 26.0f), Enc(4, "EstanteAco120", 0, 12.1f, 29.0f),
        };

        public static readonly Movel[] Todos = Fixos
            .Concat(Refeitorio(2, new[] { 13.0f, 17.5f, 22.0f, 26.5f }, new[] { 3.0f, 6.4f }))
            .Concat(QuartoDoCorredor(9f, false)).Concat(QuartoDoCorredor(14f, true))
            .Concat(QuartoDoCorredor(19f, false)).Concat(QuartoDoCorredor(24f, true))
            .ToArray();

        /// <summary>As mesas do refeitório, cada uma com um banco de cada lado (as mesas ao longo de x).</summary>
        static IEnumerable<Movel> Refeitorio(int f, float[] xs, float[] zs)
        {
            foreach (float z in zs)
                foreach (float x in xs)
                {
                    yield return Solto(f, "MesaRefeitorio", x, z);
                    yield return Solto(f, "BancoRefeitorio", x, z - 0.8f);
                    yield return Solto(f, "BancoRefeitorio", x, z + 0.8f, 180);
                }
        }

        /// <summary>
        /// Um dos quartos 1 a 4 do B3 (5 × 6,5 m, de x0 a x0 + 5, a porta no meio da parede do corredor, em z = 13): a cama
        /// de casal e a de solteiro no fundo, com o criado-mudo entre as duas, o guarda-roupa e a cômoda nas paredes dos lados
        /// e o tapete; espelhado, tudo troca de lado. A frente fica livre: a folha da porta abre para dentro, do lado de
        /// x0 + 3,13 (com uma cama numa parede do lado, ela e a ponta da folha fechavam a passagem para o zumbi).
        /// </summary>
        static IEnumerable<Movel> QuartoDoCorredor(float x0, bool espelho)
        {
            float X(float dx) => espelho ? x0 + 5f - dx : x0 + dx;
            yield return Enc(3, "CamaCasal", 180, 19.4f, X(1.5f));
            yield return Enc(3, "CriadoMudo", 180, 19.4f, X(2.95f));
            yield return Enc(3, "CamaSolteiro", 180, 19.4f, X(4.1f));
            yield return espelho ? Enc(3, "GuardaRoupa2", -90, x0 + 4.9f, 14.6f) : Enc(3, "GuardaRoupa2", 90, x0 + 0.1f, 14.6f);
            yield return espelho ? Enc(3, "Comoda3", 90, x0 + 0.1f, 14.8f) : Enc(3, "Comoda3", -90, x0 + 4.9f, 14.8f);
            yield return Solto(3, "TapeteListras", X(2.5f), 15.6f);
        }

        /// <summary>
        /// As portas de dentro do andar que são modelos da mobília (a da câmara fria): o meio do vão no chão e o giro, com a
        /// frente para o cômodo para onde a folha abre (a regra do kit: o que não é corredor; entre dois, o maior).
        /// </summary>
        public static IEnumerable<(string modelo, Vector3 lugar, float giro)> PortasDeModelo(int f)
        {
            foreach (var l in PlantaDeBunker.DoAndar(f).ligacoes)
            {
                if (string.IsNullOrEmpty(l.modelo)) continue;
                Comodo A = l.a, B = l.b;
                bool vertical = Mathf.Abs(A.x1 - B.x0) < 0.01f || Mathf.Abs(B.x1 - A.x0) < 0.01f;
                float linha = vertical ? (Mathf.Abs(A.x1 - B.x0) < 0.01f ? A.x1 : B.x1) : (Mathf.Abs(A.z1 - B.z0) < 0.01f ? A.z1 : B.z1);
                Comodo alvo = A.tipo == "corredor" ? B : B.tipo == "corredor" ? A
                            : (A.x1 - A.x0) * (A.z1 - A.z0) >= (B.x1 - B.x0) * (B.z1 - B.z0) ? A : B;
                float meio = vertical ? (alvo.x0 + alvo.x1) / 2f : (alvo.z0 + alvo.z1) / 2f;
                var lugar = vertical ? new Vector3(linha, Base(f), l.pos) : new Vector3(l.pos, Base(f), linha);
                float giro = vertical ? (meio > linha ? 90f : -90f) : (meio > linha ? 0f : 180f);
                yield return (l.modelo, lugar, giro);
            }
        }

        public static IEnumerable<Movel> DoAndar(int f)
        {
            foreach (var m in Todos) if (m.andar == f) yield return m;
        }

        /// <summary>
        /// As caixas de colisão dos móveis do andar f na malha de colisão (como as da mobília do mundo): o tamanho na escala
        /// menos 15 cm de cada lado em x e z, e os altos em camadas de até 0,85 m (o NavMesh não vê uma caixa oca).
        /// </summary>
        public static void Caixas(int f, MalhaDeObra colisao)
        {
            foreach (var m in DoAndar(f))
            {
                if (!m.caixa || !MedidasDaMobilia.Tem(m.modelo)) continue;
                Vector3 t = MedidasDaMobilia.Real(m.modelo) * m.escala;
                Vector2 meio = MedidasDaMobilia.Meio(m.modelo) * m.escala;
                float baixo = MedidasDaMobilia.Baixo(m.modelo) * m.escala;
                var giro = Quaternion.Euler(0f, m.giro, 0f);
                Vector3 c = m.lugar + giro * new Vector3(meio.x, 0f, meio.y);
                // a mesma caixa das casas (em camadas; nas camas, a cabeceira e o colchão, para o pulo da cama, 08/11/2026)
                MobiliaNova.Caixa(colisao, m.modelo, c + Vector3.up * baixo, m.giro, t);
            }
        }

        // ------------------------------------------------------------ os canos e os dutos no teto

        /// <summary>Uma corrida de peças no teto do andar: de (x, z) até (x, z) do esboço, o modelo (o cano ou o duto) e a cor.</summary>
        public struct Corrida
        {
            public int andar;
            public Vector2 de, ate;
            public string modelo;
            public Color cor;
            public float abaixoDoTeto;
        }

        public static readonly Color Agua = new Color(0.26f, 0.44f, 0.64f), Diesel = new Color(0.62f, 0.16f, 0.12f),
                                     Ventilacao = new Color(0.86f, 0.68f, 0.16f), Esgoto = new Color(0.40f, 0.28f, 0.18f),
                                     Galvanizado = new Color(0.74f, 0.75f, 0.72f), Eletrico = new Color(0.22f, 0.22f, 0.21f);

        static Corrida C(int f, float x0, float z0, float x1, float z1, string modelo, Color cor, float abaixo) => new Corrida
        {
            andar = f, de = new Vector2(x0, z0), ate = new Vector2(x1, z1), modelo = modelo, cor = cor, abaixoDoTeto = abaixo,
        };

        public static readonly Corrida[] Canos =
        {
            // B0: o duto no meio da garagem (entre as duas fileiras de luz) e os canos junto da parede das operações
            C(0, 20.0f, 0.6f, 20.0f, 23.4f, "Duto", Galvanizado, 0.32f),
            C(0, 12.5f, 0.6f, 12.5f, 23.4f, "CanoReto", Agua, 0.22f),
            C(0, 12.85f, 0.6f, 12.85f, 23.4f, "CanoReto", Diesel, 0.22f),
            C(0, 6.6f, 8.8f, 6.6f, 14.8f, "CanoReto", Agua, 0.22f),
            // B1: os canos do hall e o do fundo da sala de jogos
            C(1, 7.1f, 8.7f, 7.1f, 14.9f, "CanoReto", Agua, 0.22f),
            C(1, 7.75f, 8.7f, 7.75f, 14.9f, "CanoReto", Ventilacao, 0.22f),
            C(1, 9.2f, 23.55f, 26.8f, 23.55f, "CanoReto", Agua, 0.22f),
            C(1, 9.2f, 23.2f, 26.8f, 23.2f, "CanoReto", Esgoto, 0.22f),
            // B2: os do hall, o cano de água e o duto do refeitório, o duto da cozinha (em cima das coifas) e o esgoto da lavagem
            C(2, 7.1f, 8.7f, 7.1f, 14.9f, "CanoReto", Agua, 0.22f),
            C(2, 7.75f, 8.7f, 7.75f, 14.9f, "CanoReto", Ventilacao, 0.22f),
            C(2, 9.2f, 0.55f, 31.8f, 0.55f, "CanoReto", Agua, 0.22f),
            C(2, 9.4f, 6.0f, 31.6f, 6.0f, "Duto", Galvanizado, 0.32f),
            C(2, 9.4f, 22.2f, 20.6f, 22.2f, "Duto", Galvanizado, 0.32f),
            C(2, 0.6f, 0.6f, 0.6f, 8.0f, "CanoReto", Esgoto, 0.22f),
            // B3: os dos dois corredores
            C(3, 7.1f, 0.6f, 7.1f, 23.4f, "CanoReto", Agua, 0.22f),
            C(3, 7.75f, 0.6f, 7.75f, 23.4f, "CanoReto", Ventilacao, 0.22f),
            C(3, 9.2f, 11.6f, 31.8f, 11.6f, "CanoReto", Agua, 0.22f),
            C(3, 9.2f, 12.3f, 31.8f, 12.3f, "CanoReto", Esgoto, 0.22f),
            // B4: o hall (a água, a ventilação e o diesel), o esgoto, as peças, a elétrica (os eletrodutos em cima dos quadros),
            // o gerador (o diesel do tanque e o duto), a água (a linha das bombas e, mais baixa, a dos filtros) e o ar (os dutos)
            C(4, 7.1f, 8.7f, 7.1f, 14.9f, "CanoReto", Agua, 0.22f),
            C(4, 7.75f, 8.7f, 7.75f, 14.9f, "CanoReto", Ventilacao, 0.22f),
            C(4, 6.5f, 8.7f, 6.5f, 14.9f, "CanoReto", Diesel, 0.22f),
            C(4, 0.6f, 0.6f, 0.6f, 8.0f, "CanoReto", Esgoto, 0.22f),
            C(4, 1.0f, 1.0f, 8.8f, 1.0f, "CanoReto", Esgoto, 0.5f),
            C(4, 0.2f, 23.4f, 8.8f, 23.4f, "CanoReto", Agua, 0.22f),
            C(4, 9.2f, 1.4f, 19.8f, 1.4f, "CanoReto", Eletrico, 0.22f),
            C(4, 9.2f, 1.75f, 19.8f, 1.75f, "CanoReto", Eletrico, 0.22f),
            C(4, 9.4f, 21.0f, 19.6f, 21.0f, "CanoReto", Diesel, 0.22f),
            C(4, 9.4f, 15.5f, 19.6f, 15.5f, "Duto", Galvanizado, 0.32f),
            C(4, 20.2f, 1.2f, 31.8f, 1.2f, "CanoReto", Agua, 0.22f),
            C(4, 25.8f, 1.6f, 25.8f, 11.8f, "CanoReto", Agua, 0.5f),
            C(4, 20.3f, 21.5f, 31.7f, 21.5f, "Duto", Galvanizado, 0.32f),
            C(4, 20.3f, 15.0f, 31.7f, 15.0f, "Duto", Galvanizado, 0.32f),
        };

        /// <summary>
        /// Uma peça solta no teto (o registro num cano, a grelha de ar): no ponto (x, z do esboço), o giro (o registro corre
        /// em x: 90 num cano em z), pintada com a cor (a = 0: a do modelo) e o quanto fica abaixo do teto (a grelha, 0: a
        /// origem dela é no teto).
        /// </summary>
        public struct Peca
        {
            public int andar;
            public Vector2 ponto;
            public string modelo;
            public float giro;
            public Color cor;
            public float abaixoDoTeto;
        }

        static Peca P(int f, float x, float z, string modelo, float giro, Color cor, float abaixo) => new Peca
        {
            andar = f, ponto = new Vector2(x, z), modelo = modelo, giro = giro, cor = cor, abaixoDoTeto = abaixo,
        };

        public static readonly Peca[] Pecas =
        {
            // B4: os registros nas linhas de água e de diesel e as grelhas de ar
            P(4, 23.0f, 1.2f, "Registro", 0, Agua, 0.22f), P(4, 29.0f, 1.2f, "Registro", 0, Agua, 0.22f),
            P(4, 25.8f, 6.0f, "Registro", 90, Agua, 0.5f),
            P(4, 12.0f, 21.0f, "Registro", 0, Diesel, 0.22f), P(4, 17.0f, 21.0f, "Registro", 0, Diesel, 0.22f),
            P(4, 6.5f, 11.0f, "Registro", 90, Diesel, 0.22f),
            P(4, 22.5f, 18.0f, "Grelha", 0, default, 0f), P(4, 29.5f, 18.0f, "Grelha", 0, default, 0f),
            P(4, 11.0f, 13.0f, "Grelha", 0, default, 0f), P(4, 18.5f, 13.0f, "Grelha", 0, default, 0f),
            P(4, 12.0f, 9.0f, "Grelha", 0, default, 0f), P(4, 17.0f, 9.0f, "Grelha", 0, default, 0f),
            P(4, 4.5f, 6.5f, "Grelha", 0, default, 0f), P(4, 4.5f, 17.0f, "Grelha", 0, default, 0f),
        };
    }
}
