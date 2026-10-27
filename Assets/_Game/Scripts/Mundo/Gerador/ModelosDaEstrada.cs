using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os modelos da beira da estrada feitos por código (Plano_Mapa, Parte M4), no estilo do kit da cidade (caixas e
    /// cilindros nas cores da <see cref="Paleta"/>, sombreado plano): o posto por fora, o caminhão do exército, o blindado,
    /// o contêiner, o muro de sacos de areia, o alambrado e as placas (a de direção, verde com as letras brancas, e a de
    /// entrada da cidade). Os carros, as barreiras de concreto, o cavalete e o cone vêm do Blender (Cidade_Rua.fbx).
    /// Cada modelo traz a malha, as caixas de colisão e, nas placas, os textos (quem escreve é a cena). Nas coordenadas do
    /// modelo: a frente em +z, o chão em y = 0. Não usa nada da cena: dá para montar em outra thread.
    /// </summary>
    public static class ModelosDaEstrada
    {
        /// <summary>Os carros do Cidade_Rua.fbx, na ordem das variantes.</summary>
        public static readonly string[] NomesDosCarros =
        {
            "Carro_Sedan_Azul", "Carro_Sedan_Vinho", "Carro_Sedan_Cinza", "Carro_Sedan_Verde", "Carro_Pickup_Bege", "Carro_Pickup_Branco", "Carro_Pickup_Azul",
        };
        public const int Carros = 7;

        /// <summary>Uma caixa de colisão no espaço do modelo.</summary>
        public struct Caixa
        {
            public Vector3 Centro, Tamanho;
            public float GiroY;
        }

        /// <summary>As cores das letras: o branco das placas verdes, o verde-escuro da placa de entrada e o vermelho do aviso.</summary>
        public const int LetraClara = 0, LetraEscura = 1, LetraVermelha = 2;

        /// <summary>
        /// Um texto na placa: no plano da frente dela, com u para a direita de quem lê (o -x do modelo) e v para cima; a
        /// altura das letras (m), o alinhamento (-1 começa em u, 0 centrado, 1 termina em u) e a cor (<see cref="LetraClara"/>...).
        /// </summary>
        public struct Texto
        {
            public string Conteudo;
            public float U, V, Z, Altura;
            public int Alinhamento;
            public int Cor;
            /// <summary>O giro do texto em volta do eixo y do modelo (graus): 180 escreve no verso, 90 no lado +x.</summary>
            public float Giro;
        }

        public sealed class Modelo
        {
            public readonly MalhaDeObra Malha = new MalhaDeObra();
            public readonly List<Caixa> Colisao = new List<Caixa>();
            public readonly List<Texto> Textos = new List<Texto>();
            /// <summary>A colisão em malha, além das caixas (a loja de conveniência do posto); null nos outros.</summary>
            public MalhaDeObra MalhaDeColisao;
            /// <summary>Num modelo com o interior (o posto): os pisos dos cômodos (para os passos) e a caixa de dentro (a neblina e o eco).</summary>
            public readonly List<MalhasDasCasas.Zona> Zonas = new List<MalhasDasCasas.Zona>();
            public Vector3 CentroDeDentro, TamanhoDeDentro;
            /// <summary>Num modelo com o interior (o posto): os pontos dos móveis da loja, nas coordenadas do modelo.</summary>
            public readonly List<PontoDeMovel> Moveis = new List<PontoDeMovel>();

            public void Bloco(Vector3 centro, Vector3 tam, Cor cor, float giroY = 0f, float giroZ = 0f) =>
                Malha.Caixa(Matrix4x4.TRS(centro, Quaternion.Euler(0f, giroY, giroZ), Vector3.one), tam, cor);

            public void Colide(Vector3 centro, Vector3 tam, float giroY = 0f) => Colisao.Add(new Caixa { Centro = centro, Tamanho = tam, GiroY = giroY });

            /// <summary>Uma roda: o cilindro deitado no eixo x, centrado em p.</summary>
            public void Roda(Vector3 p, float raio, float largura)
            {
                var M = Matrix4x4.TRS(p + Vector3.right * (largura * 0.5f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
                Malha.Cilindro(M, raio, raio, largura, 10, "Pneu");
            }
        }

        // ------------------------------------------------------------------ o posto

        static readonly string[] CoresDoPosto = { "PlacaVermelha", "PlacaVerde", "Laranja" };

        /// <summary>
        /// O posto por fora (a frente olha para a estrada): a loja de conveniência no fundo (a de verdade, Parte M6,
        /// terceira fatia: a <see cref="Loja"/> da planta do posto, virada para as bombas, com a placa na cor da bandeira, a
        /// colisão em malha, os pisos e a caixa de dentro), a cobertura com as duas ilhas de bombas na frente dela e o totem
        /// com a placa perto da estrada. A cor da bandeira muda com a variante.
        /// </summary>
        public static Modelo Posto(int variante)
        {
            var m = new Modelo();
            string marca = CoresDoPosto[Mathf.Abs(variante) % CoresDoPosto.Length];
            // o piso de concreto da cobertura e da frente da loja
            m.Bloco(new Vector3(0f, 0.05f, 3f), new Vector3(19f, 0.14f, 13f), "Concreto");
            m.Bloco(new Vector3(0f, 0.05f, -4.3f), new Vector3(16f, 0.14f, 2f), "Calcada");
            // a loja: a frente dela (o -z da planta) para as bombas (o +z do posto)
            var medidas = Medidas.DoJogo;
            var planta = PlantasDeLoja.DoPosto(new System.Random(variante + 11), medidas);
            planta.placa = marca;
            var loja = new Loja(planta, medidas);
            loja.Montar((x, z) => 0f, 0f);
            Mobilia.Por(loja.moveis, loja.vis, loja.col, planta.semente);   // a mobília (sem ela registrada, nada)
            var M = Matrix4x4.TRS(new Vector3(0f, 0f, -10f), Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            m.Malha.Juntar(loja.vis, M, planta.espelhar);
            m.MalhaDeColisao = new MalhaDeObra();
            m.MalhaDeColisao.Juntar(loja.col, M, planta.espelhar);
            MalhaDeObra.Devolver(loja.vis);
            MalhaDeObra.Devolver(loja.col);
            float sx = planta.espelhar ? -1f : 1f;
            foreach (var pm in loja.moveis)
            {
                var q = planta.espelhar ? pm.Espelhado() : pm;
                q.centro = M.MultiplyPoint3x4(q.centro);
                q.giro += 180f;
                m.Moveis.Add(q);
            }
            foreach (var k in planta.comodos)
                m.Zonas.Add(new MalhasDasCasas.Zona
                {
                    Centro = M.MultiplyPoint3x4(new Vector3(sx * k.Cx, planta.Base(k.andar), k.Cz)),
                    Tamanho = new Vector3(k.x1 - k.x0, 1f, k.z1 - k.z0),
                    Piso = k.piso ?? "Piso",
                });
            float baixo = PlantaDeLoja.Piso, alto = planta.Cobertura - medidas.laje;
            m.CentroDeDentro = M.MultiplyPoint3x4(new Vector3(0f, (baixo + alto) / 2, 0f));
            m.TamanhoDeDentro = new Vector3(planta.W - 0.2f, alto - baixo, planta.D - 0.2f);
            // a cobertura: 4 colunas e o teto com a faixa da bandeira
            foreach (float x in new[] { -5.5f, 5.5f })
            foreach (float z in new[] { 0.2f, 5.8f })
            {
                m.Bloco(new Vector3(x, 2.6f, z), new Vector3(0.45f, 5.2f, 0.45f), "ConcretoClaro");
                m.Colide(new Vector3(x, 2.6f, z), new Vector3(0.45f, 5.2f, 0.45f));
            }
            m.Bloco(new Vector3(0f, 5.55f, 3f), new Vector3(17f, 0.8f, 11f), Cor.Por((n, c) => n.y > 0.7f ? "Metal" : n.y < -0.7f ? "ConcretoClaro" : marca));
            m.Bloco(new Vector3(0f, 5.95f, 3f), new Vector3(17.04f, 0.16f, 11.04f), "ListraBranca");
            // as ilhas com as bombas
            foreach (float x in new[] { -2.7f, 2.7f })
            {
                m.Bloco(new Vector3(x, 0.15f, 3f), new Vector3(1.4f, 0.3f, 5.4f), Cor.PorNormal("ConcretoClaro", "PlacaAmarela"));
                foreach (float z in new[] { 1.6f, 4.4f })
                {
                    m.Bloco(new Vector3(x, 1.15f, z), new Vector3(0.9f, 1.7f, 0.6f), Cor.Por((n, c) => Mathf.Abs(n.x) > 0.7f ? marca : "CarroBranco"));
                    m.Bloco(new Vector3(x + (x < 0 ? -0.46f : 0.46f), 1.45f, z), new Vector3(0.04f, 0.5f, 0.4f), "Vidro");
                    m.Bloco(new Vector3(x + (x < 0 ? -0.5f : 0.5f), 1.0f, z + 0.2f), new Vector3(0.08f, 0.35f, 0.08f), "Pneu");
                }
                m.Colide(new Vector3(x, 1f, 3f), new Vector3(1.4f, 2f, 5.4f));
            }
            // o totem perto da estrada, com o preço apagado
            m.Bloco(new Vector3(11.5f, 3.5f, 11f), new Vector3(0.4f, 7f, 0.4f), "Metal");
            m.Bloco(new Vector3(11.5f, 7.6f, 11f), new Vector3(2.6f, 2.6f, 0.45f), marca);
            m.Bloco(new Vector3(11.5f, 8.2f, 11f), new Vector3(2.64f, 0.5f, 0.49f), "ListraBranca");
            m.Bloco(new Vector3(11.5f, 6.85f, 11f), new Vector3(2.0f, 0.55f, 0.5f), "Grade");
            m.Colide(new Vector3(11.5f, 3.5f, 11f), new Vector3(0.4f, 7f, 0.4f));
            return m;
        }

        // ------------------------------------------------------------------ o exército

        /// <summary>O caminhão do exército (a frente em +z): a cabine, o capô e a carroceria com a lona.</summary>
        public static Modelo CaminhaoMilitar()
        {
            var m = new Modelo();
            m.Bloco(new Vector3(0f, 0.95f, 0f), new Vector3(2.1f, 0.35f, 7.4f), "Grade");
            m.Bloco(new Vector3(0f, 1.55f, 3.25f), new Vector3(2.2f, 0.9f, 1.1f), "CarroVerde");
            m.Bloco(new Vector3(0f, 2.05f, 1.85f), new Vector3(2.4f, 1.9f, 1.8f), "CarroVerde");
            m.Bloco(new Vector3(0f, 2.45f, 2.76f), new Vector3(2.0f, 0.7f, 0.05f), "VidroCarro");
            m.Bloco(new Vector3(0f, 1.25f, 3.82f), new Vector3(2.0f, 0.4f, 0.08f), "Grade");
            m.Bloco(new Vector3(0f, 1.3f, -1.55f), new Vector3(2.45f, 0.35f, 4.7f), "CarroVerde");
            m.Bloco(new Vector3(0f, 2.3f, -1.55f), new Vector3(2.45f, 1.7f, 4.7f), "PinheiroEscuro");
            m.Bloco(new Vector3(0f, 3.2f, -1.55f), new Vector3(1.9f, 0.2f, 4.7f), "PinheiroEscuro");
            foreach (float z in new[] { 2.9f, -0.8f, -2.6f })
            foreach (float x in new[] { -1.05f, 1.05f })
                m.Roda(new Vector3(x + (x < 0 ? -0.2f : 0.2f), 0.55f, z), 0.55f, 0.42f);
            m.Colide(new Vector3(0f, 1.6f, 0f), new Vector3(2.5f, 3.2f, 7.6f));
            return m;
        }

        /// <summary>O blindado parado (a frente em +z): o casco com a frente inclinada, a torre com o cano e 8 rodas.</summary>
        public static Modelo Blindado()
        {
            var m = new Modelo();
            var perfil = new[] { new Vector2(-3.1f, 0.55f), new Vector2(3.1f, 0.55f), new Vector2(3.1f, 1.3f), new Vector2(1.7f, 2.35f), new Vector2(-3.1f, 2.35f) };
            var a = new List<Vector3>();
            var b = new List<Vector3>();
            foreach (var p in perfil)
            {
                a.Add(new Vector3(-1.3f, p.y, p.x));
                b.Add(new Vector3(1.3f, p.y, p.x));
            }
            m.Malha.Solido(a, b, "Lixeira");
            m.Malha.Cilindro(Matrix4x4.TRS(new Vector3(0f, 2.35f, -0.6f), Quaternion.identity, Vector3.one), 0.75f, 0.6f, 0.55f, 8, "Lixeira");
            m.Malha.Tubo(new Vector3(0f, 2.65f, 0f), new Vector3(0f, 2.7f, 2.8f), 0.07f, 6, "Grade");
            m.Bloco(new Vector3(0f, 1.0f, 3.12f), new Vector3(2.2f, 0.25f, 0.1f), "Grade");
            foreach (float z in new[] { 2.2f, 0.75f, -0.75f, -2.2f })
            foreach (float x in new[] { -1.3f, 1.3f })
                m.Roda(new Vector3(x + (x < 0 ? -0.2f : 0.2f), 0.55f, z), 0.55f, 0.4f);
            m.Colide(new Vector3(0f, 1.35f, 0f), new Vector3(2.8f, 2.7f, 6.3f));
            return m;
        }

        static readonly string[] CoresDoConteiner = { "Cacamba", "Ferrugem", "CarroVinho", "Lixeira" };

        /// <summary>O contêiner de 20 pés: o comprido no eixo x (6 m), com os vincos dos lados e as barras da porta (no +x).</summary>
        public static Modelo Conteiner(int variante)
        {
            var m = new Modelo();
            string cor = CoresDoConteiner[Mathf.Abs(variante) % CoresDoConteiner.Length];
            m.Bloco(new Vector3(0f, 1.3f, 0f), new Vector3(6.06f, 2.59f, 2.44f), cor);
            for (float x = -2.75f; x <= 2.76f; x += 0.5f)
                foreach (float z in new[] { -1.24f, 1.24f })
                    m.Bloco(new Vector3(x, 1.3f, z), new Vector3(0.12f, 2.35f, 0.05f), cor);
            foreach (float z in new[] { -0.9f, -0.4f, 0.4f, 0.9f })
                m.Bloco(new Vector3(3.06f, 1.3f, z), new Vector3(0.06f, 2.4f, 0.06f), "Metal");
            m.Colide(new Vector3(0f, 1.3f, 0f), new Vector3(6.06f, 2.59f, 2.44f));
            return m;
        }

        /// <summary>O muro de sacos de areia: 3 m no eixo x, três fileiras desencontradas.</summary>
        public static Modelo Sacos()
        {
            var m = new Modelo();
            for (int fila = 0; fila < 3; fila++)
            {
                float desloca = fila % 2 == 0 ? 0f : 0.29f;
                for (float x = -1.2f + desloca; x <= 1.21f; x += 0.58f)
                {
                    float y = 0.12f + fila * 0.23f;
                    float giro = Mathf.Sin(x * 13.1f + fila * 2.3f) * 6f;
                    m.Bloco(new Vector3(x, y, Mathf.Sin(x * 7.7f + fila) * 0.04f), new Vector3(0.56f, 0.22f, 0.36f), "CarroBege", giro);
                }
            }
            m.Colide(new Vector3(0f, 0.36f, 0f), new Vector3(2.9f, 0.72f, 0.42f));
            return m;
        }

        /// <summary>Um pedaço de 3 m de alambrado no eixo x (os postes, o tubo de cima, os arames e o arame farpado); a variante 1 está rasgada.</summary>
        public static Modelo Alambrado(int variante)
        {
            var m = new Modelo();
            foreach (float x in new[] { -1.5f, 1.5f })
            {
                m.Malha.Cilindro(Matrix4x4.TRS(new Vector3(x, 0f, 0f), Quaternion.identity, Vector3.one), 0.045f, 0.045f, 2.7f, 6, "Metal");
                m.Bloco(new Vector3(x, 2.85f, 0.12f), new Vector3(0.05f, 0.5f, 0.05f), "Metal", 0f, 0f);
            }
            m.Malha.Tubo(new Vector3(-1.5f, 2.35f, 0f), new Vector3(1.5f, 2.35f, 0f), 0.025f, 5, "Metal");
            bool rasgado = variante == 1;
            for (float x = -1.42f; x <= 1.42f; x += 0.14f)
            {
                if (rasgado && x > -0.5f && x < 0.6f) continue;
                m.Bloco(new Vector3(x, 1.2f, 0f), new Vector3(0.012f, 2.3f, 0.012f), "Grade");
            }
            foreach (float y in new[] { 0.1f, 1.2f, 2.3f })
                m.Bloco(new Vector3(0f, y, 0f), new Vector3(3f, 0.012f, 0.012f), "Grade");
            // o arame farpado em cima, inclinado para fora (o -z)
            foreach (float y in new[] { 2.62f, 2.82f, 3.02f })
                m.Bloco(new Vector3(0f, y, 0.12f + (y - 2.62f) * 0.3f), new Vector3(3f, 0.015f, 0.015f), "Grade");
            m.Colide(new Vector3(0f, 1.4f, 0f), new Vector3(3f, 2.8f, 0.12f));
            return m;
        }

        // ------------------------------------------------------------------ as placas

        public const float AlturaDaLinha = 0.42f;
        const float Letra = 0.26f;

        // a largura de um texto em m, pela altura das maiúsculas (na Arial, cada letra anda em média ~0,75 dela)
        static float Largura(string texto, float altura) => texto.Length * altura * 0.75f;

        /// <summary>
        /// A placa de direção: verde com a borda e as letras brancas, em dois postes, a borda de baixo a 2,1 m. Uma linha
        /// por destino: a seta da saída (só na primeira linha de cada saída), o nome e a distância ("2,4 km").
        /// </summary>
        public static Modelo PlacaDeDirecao(List<LinhaDePlaca> linhas)
        {
            var m = new Modelo();
            float largura = LarguraDaPlacaDeDirecao(linhas);
            float altura = linhas.Count * AlturaDaLinha + 0.3f;
            float baixo = 2.1f, meio = baixo + altura * 0.5f;
            Placa(m, largura, altura, meio, "PlacaVerde", "PlacaBranca");
            for (int i = 0; i < linhas.Count; i++)
            {
                var l = linhas[i];
                float v = baixo + altura - 0.15f - AlturaDaLinha * (i + 0.5f);
                if (l.Seta.HasValue) Seta(m, -largura * 0.5f + 0.36f, v, l.Seta.Value);
                m.Textos.Add(new Texto { Conteudo = l.Texto, U = -largura * 0.5f + 0.72f, V = v, Z = 0.045f, Altura = Letra * l.Tamanho, Alinhamento = -1 });
                if (l.Km >= 0f)
                    m.Textos.Add(new Texto { Conteudo = Distancia(l.Km), U = largura * 0.5f - 0.18f, V = v, Z = 0.045f, Altura = Letra * 0.85f, Alinhamento = 1 });
            }
            return m;
        }

        /// <summary>A largura da placa de direção (m): o nome mais comprido, a seta e a distância.</summary>
        public static float LarguraDaPlacaDeDirecao(List<LinhaDePlaca> linhas)
        {
            float maior = 0f;
            foreach (var l in linhas) maior = Mathf.Max(maior, Largura(l.Texto, Letra * l.Tamanho));
            return Mathf.Clamp(maior + 0.75f + 1.25f + 0.3f, 2.6f, 8f);
        }

        /// <summary>"2,4 km"; abaixo de 1 km, "800 m".</summary>
        public static string Distancia(float km)
        {
            if (km < 0.95f) return Mathf.Max(100, Mathf.RoundToInt(km * 10f) * 100) + " m";
            return (Mathf.Round(km * 10f) / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " km";
        }

        /// <summary>
        /// A placa de entrada da cidade: branca com a borda verde, as letras verde-escuras e centradas ("Bem-vindo a", o nome e
        /// a população), em dois postes. A população vem riscada à mão (uma pincelada escura torta por cima). A variante 1
        /// (vila) é menor.
        /// </summary>
        public static Modelo PlacaDeEntrada(List<LinhaDePlaca> linhas, int variante)
        {
            var m = new Modelo();
            float escala = variante == 1 ? 0.8f : 1f;
            float total = 0f;
            foreach (var l in linhas) total += Letra * 1.6f * l.Tamanho * escala * EntreLinhas;
            float largura = LarguraDaPlacaDeEntrada(linhas, variante), altura = total + 0.3f;
            float baixo = 1.6f, meio = baixo + altura * 0.5f;
            Placa(m, largura, altura, meio, "PlacaBranca", "PlacaVerde");
            float v = baixo + altura - 0.15f;
            foreach (var l in linhas)
            {
                float a = Letra * 1.6f * l.Tamanho * escala;
                v -= a * EntreLinhas * 0.5f;
                m.Textos.Add(new Texto { Conteudo = l.Texto, U = 0f, V = v, Z = 0.045f, Altura = a, Alinhamento = 0, Cor = LetraEscura });
                if (l.Riscada)
                {
                    // o risco à mão: uma faixa escura, torta e um pouco mais comprida que o texto
                    float w = Largura(l.Texto, a) * 1.05f;
                    var M = Matrix4x4.TRS(new Vector3(0f, v, 0.055f), Quaternion.Euler(0f, 0f, 7f), Vector3.one);
                    m.Malha.Caixa(M, new Vector3(w, a * 0.22f, 0.01f), "Grade");
                    var M2 = Matrix4x4.TRS(new Vector3(-0.05f, v - a * 0.08f, 0.056f), Quaternion.Euler(0f, 0f, 4f), Vector3.one);
                    m.Malha.Caixa(M2, new Vector3(w * 0.9f, a * 0.12f, 0.01f), "Grade");
                }
                v -= a * EntreLinhas * 0.5f;
            }
            return m;
        }

        // cada linha da placa de entrada ocupa 1,7 vez a altura das maiúsculas (os acentos em cima e o ç e o g embaixo)
        const float EntreLinhas = 1.7f;

        /// <summary>A largura da placa de entrada (m).</summary>
        public static float LarguraDaPlacaDeEntrada(List<LinhaDePlaca> linhas, int variante)
        {
            float escala = variante == 1 ? 0.8f : 1f, maior = 0f;
            foreach (var l in linhas) maior = Mathf.Max(maior, Largura(l.Texto, Letra * 1.6f * l.Tamanho * escala));
            return Mathf.Clamp(maior + 1f, 2.4f, 7f);
        }

        /// <summary>
        /// A placa de aviso presa na cerca da quarentena (no lado de dentro, +z): branca com a borda e as letras vermelhas,
        /// sem postes (a cerca segura), de 1,1 a 2,1 m do chão.
        /// </summary>
        public static Modelo PlacaDeAviso(List<LinhaDePlaca> linhas)
        {
            var m = new Modelo();
            // a largura pela linha mais comprida (as maiúsculas da Arial são mais largas: ~0,95 da altura cada)
            float largura = 2.2f, altura = 1f, meio = 1.6f;
            foreach (var l in linhas)
            {
                float w = 0f;
                foreach (char c in l.Texto) w += char.IsUpper(c) ? 0.95f : c == ' ' ? 0.4f : 0.75f;
                largura = Mathf.Max(largura, w * 0.16f * l.Tamanho + 0.4f);
            }
            m.Bloco(new Vector3(0f, meio, 0f), new Vector3(largura, altura, 0.03f), Cor.Por((n, c) => n.z > 0.7f ? "PlacaBranca" : "Metal"));
            float z = 0.02f;
            m.Bloco(new Vector3(0f, meio + altura * 0.5f - 0.05f, z), new Vector3(largura - 0.06f, 0.04f, 0.01f), "PlacaVermelha");
            m.Bloco(new Vector3(0f, meio - altura * 0.5f + 0.05f, z), new Vector3(largura - 0.06f, 0.04f, 0.01f), "PlacaVermelha");
            m.Bloco(new Vector3(largura * 0.5f - 0.05f, meio, z), new Vector3(0.04f, altura - 0.06f, 0.01f), "PlacaVermelha");
            m.Bloco(new Vector3(-largura * 0.5f + 0.05f, meio, z), new Vector3(0.04f, altura - 0.06f, 0.01f), "PlacaVermelha");
            float v = meio + 0.2f;
            foreach (var l in linhas)
            {
                float a = 0.16f * l.Tamanho;
                m.Textos.Add(new Texto { Conteudo = l.Texto, U = 0f, V = v, Z = 0.03f, Altura = a, Alinhamento = 0, Cor = LetraVermelha });
                v -= 0.36f;
            }
            return m;
        }

        /// <summary>
        /// A placa de rua da esquina (Parte M5): o poste e duas plaquinhas azuis com as letras brancas, uma para cada rua,
        /// paralela a ela, com o nome dos dois lados. A primeira linha é a rua que corre ao longo do x do modelo (a placa de
        /// cima, que se lê de +z e de -z); a segunda, a que corre ao longo do z (a de baixo, lida de +x e de -x).
        /// </summary>
        public static Modelo PlacaDeRua(List<LinhaDePlaca> linhas)
        {
            var m = new Modelo();
            const float letra = 0.12f, alto = 0.24f, grosso = 0.03f;
            m.Malha.Cilindro(Matrix4x4.identity, 0.035f, 0.035f, 3.05f, 6, "Metal");
            m.Colide(new Vector3(0f, 1.5f, 0f), new Vector3(0.12f, 3f, 0.12f));
            for (int k = 0; k < linhas.Count && k < 2; k++)
            {
                string nome = linhas[k].Texto;
                float largura = LarguraDaPlacaDeRua(nome);
                float y = k == 0 ? 2.92f : 2.62f;
                if (k == 0) m.Bloco(new Vector3(0f, y, 0f), new Vector3(largura, alto, grosso), "CarroAzul");
                else m.Bloco(new Vector3(0f, y, 0f), new Vector3(grosso, alto, largura), "CarroAzul");
                float giro = k == 0 ? 0f : 90f;
                foreach (float lado in new[] { 0f, 180f })
                    m.Textos.Add(new Texto { Conteudo = nome, U = 0f, V = y, Z = grosso * 0.5f + 0.004f, Altura = letra, Alinhamento = 0, Cor = LetraClara, Giro = giro + lado });
            }
            return m;
        }

        /// <summary>A largura da plaquinha da placa de rua (m), pelo nome.</summary>
        public static float LarguraDaPlacaDeRua(string nome) => Mathf.Clamp(Largura(nome, 0.12f) * 1.05f + 0.25f, 0.9f, 3.2f);

        // a chapa (com a frente em +z), a borda de outra cor, os dois postes atrás e a colisão
        static void Placa(Modelo m, float largura, float altura, float meio, string fundo, string borda)
        {
            m.Bloco(new Vector3(0f, meio, 0f), new Vector3(largura, altura, 0.06f), Cor.Por((n, c) => n.z > 0.7f ? fundo : "Metal"));
            float z = 0.035f;
            m.Bloco(new Vector3(0f, meio + altura * 0.5f - 0.06f, z), new Vector3(largura - 0.08f, 0.045f, 0.012f), borda);
            m.Bloco(new Vector3(0f, meio - altura * 0.5f + 0.06f, z), new Vector3(largura - 0.08f, 0.045f, 0.012f), borda);
            m.Bloco(new Vector3(largura * 0.5f - 0.06f, meio, z), new Vector3(0.045f, altura - 0.08f, 0.012f), borda);
            m.Bloco(new Vector3(-largura * 0.5f + 0.06f, meio, z), new Vector3(0.045f, altura - 0.08f, 0.012f), borda);
            float topo = meio + altura * 0.5f - 0.1f;
            foreach (float x in new[] { -largura * 0.3f, largura * 0.3f })
            {
                m.Bloco(new Vector3(x, topo * 0.5f, -0.09f), new Vector3(0.1f, topo, 0.1f), "Metal");
                m.Colide(new Vector3(x, topo * 0.5f, -0.09f), new Vector3(0.12f, topo, 0.12f));
            }
            m.Colide(new Vector3(0f, meio, 0f), new Vector3(largura, altura, 0.08f));
        }

        // a seta branca no plano da placa (u para a direita de quem lê = -x), girando "graus" para a direita a partir de "para cima"
        static void Seta(Modelo m, float u, float v, float graus)
        {
            float a = graus * Mathf.Deg2Rad;
            var frente = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            var lado = new Vector2(frente.y, -frente.x);
            Vector3 P(Vector2 q) => new Vector3(-(u + q.x), v + q.y, 0.045f);
            float haste = 0.13f, cabeca = 0.11f, larg = 0.035f, abre = 0.11f;
            var corpo = new List<Vector3>
            {
                P(-frente * haste + lado * larg), P(frente * (haste - cabeca * 0.5f) + lado * larg),
                P(frente * (haste - cabeca * 0.5f) - lado * larg), P(-frente * haste - lado * larg),
            };
            m.Malha.Face(corpo, Vector3.forward, "PlacaBranca");
            var ponta = new List<Vector3>
            {
                P(frente * (haste + cabeca * 0.6f)), P(frente * (haste - cabeca * 0.6f) + lado * abre), P(frente * (haste - cabeca * 0.6f) - lado * abre),
            };
            m.Malha.Face(ponta, Vector3.forward, "PlacaBranca");
        }
    }
}
