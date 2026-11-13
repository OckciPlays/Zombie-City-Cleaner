using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>O que uma peça da usina é na cena (o material e a colisão).</summary>
    public enum TipoDaPecaDaUsina { Obra, Agua, Carro, Barreira }

    /// <summary>
    /// Uma peça da hidrelétrica pronta para a cena (<see cref="UsinaDeLakewood.Montar"/>): a origem no mundo (no chão
    /// dela: o material do abandono acha a altura sobre o chão pelo y do objeto), o giro, o desenho e a colisão (nas
    /// coordenadas da peça). Na água, a malha da água (no mundo); no carro e na barreira, o modelo do Cidade_Rua.
    /// </summary>
    public sealed class PecaDaUsina
    {
        public string Nome;
        public TipoDaPecaDaUsina Tipo;
        public Vector3 Posicao;
        public Quaternion Giro = Quaternion.identity;
        public MalhaDeObra Desenho, Colisao;
        public MalhaDoBloco Agua;
        /// <summary>O carro (o índice em <see cref="ModelosDaEstrada.NomesDosCarros"/>).</summary>
        public int Carro = -1;
    }

    /// <summary>
    /// A hidrelétrica de Lakewood (Plano_Lakewood, Etapa 4, 10/11/2026, a pedido: "pode seguir com a etapa 4"; o prompt
    /// mestre, "7. Hidrelétrica Lakewood"): a barragem de gravidade de verdade e o que fica em volta dela, cinco anos depois
    /// do apocalipse, sem energia. Tudo sai da linha da crista do mapa (<see cref="RepresaDoMapa"/>), num sistema de
    /// coordenadas da barragem: o u ao longo da crista (0 na cabeceira oeste, a; o comprimento dela na leste, b) e o v para
    /// jusante (0 no meio da crista; positivo rio abaixo). Nas malhas, a "moldura da barragem" é o x = u, o y = a altura de
    /// verdade e o z = -v (o +z olha para o lago): o objeto gira <see cref="Giro"/> em y.
    ///  - <b>A barragem</b>: os blocos de gravidade (a face de montante em pé, a de jusante em talude de 0,8), com as juntas,
    ///    as manchas e o nome; a crista de 12 m com a estrada de duas pistas, as calçadas, o parapeito de montante, o
    ///    guarda-corpo de jusante e os postes apagados; o vertedouro no meio (sobre o rio de antes: 6 vãos de 12 m com as
    ///    comportas de segmento fechadas, os pilares, a ponte da estrada e a calha até a bacia de dissipação); as tomadas
    ///    d'água da casa de força na face de montante, com as grades, e o pórtico rolante em cima delas; a torre do elevador;
    ///    as galerias de inspeção no pé (fechadas: a ligação com os túneis é da Etapa 8).
    ///  - <b>A casa de força</b> no pé, a leste do vertedouro, de frente para o canal de fuga, e o canal de fuga (a bacia e o
    ///    poço, com a água parada a 106 m) que vira o Lakewood River.
    ///  - <b>No pátio</b> (a margem leste, entre a barragem, o rio e a encosta): a subestação, o prédio da administração, o
    ///    estacionamento, a oficina, os dois galpões, o prédio da galeria de serviço (a entrada dos túneis, na Etapa 8), o
    ///    tanque de diesel, a guarita e o portão, o alambrado e os carros abandonados.
    ///  - <b>As torres de transmissão</b>: duas linhas saindo da subestação (para a cidade de Lakewood, a leste, e para o
    ///    sul, até a borda do mapa), com os cabos e a faixa sem árvores embaixo.
    ///  - <b>As estradas de acesso</b> (<see cref="Vias"/>): a da crista (na própria barragem), a Lakewood Dam Road (da
    ///    cabeceira leste, descendo até o pátio, passando pelo portão e seguindo para o sul pelo vale) e a Dam West Road (da
    ///    cabeceira oeste pelo colo da Dam West Saddle até a planície). A Etapa 5 liga as pontas delas à rede.
    ///  - <b>O chão</b>: os pátios aplainam o terreno (<see cref="Chao"/>), a água da bacia e do poço conta como água
    ///    (<see cref="Agua"/>), e os pátios, a barragem e a faixa das linhas ficam sem árvores (<see cref="SemArvores"/>),
    ///    com a grama rala ou nenhuma (<see cref="FatorDaGrama"/>) e o cascalho (<see cref="Cascalho"/>).
    /// Código puro: o plano é feito no construtor (precisa só do relevo), e as malhas em <see cref="Montar"/>.
    /// </summary>
    public sealed partial class UsinaDeLakewood
    {
        public readonly MapaDeLakewood Mapa;
        public readonly RelevoDeLakewood Relevo;
        /// <summary>O mapa tem a barragem.</summary>
        public readonly bool Tem;
        /// <summary>A origem (a cabeceira de onde o u começa), a direção da crista e a de jusante, no plano do mapa.</summary>
        public readonly Vector2 A, D, J;
        /// <summary>O giro da moldura da barragem em y (graus): o x vai para <see cref="D"/> e o z para -<see cref="J"/>.</summary>
        public readonly float Giro;
        public readonly Quaternion Rotacao;
        /// <summary>O comprimento da crista entre as cabeceiras (m) e a água do reservatório.</summary>
        public readonly float Comprimento, Nivel;

        /// <summary>O quanto a barragem entra nas encostas, além das cabeceiras (m).</summary>
        public const float Embutido = 45f;
        /// <summary>A água parada do canal de fuga (a bacia e o poço; o rio começa nela) e o fundo de concreto (m).</summary>
        public const float NivelDaFuga = 106f, FundoDaFuga = 100f;

        public float Crista => Mapa.represa.alturaDaCrista;
        public float MeiaCrista => Mapa.represa.larguraDaCrista * 0.5f;
        public float AlturaDoPe => Mapa.represa.alturaDoPe;
        /// <summary>O u das duas pontas da barragem (embutidas nas encostas).</summary>
        public float U0 => -Embutido;
        public float U1 => Comprimento + Embutido;

        // ------------------------------------------------------------------ o vertedouro (sobre o rio de antes)

        /// <summary>O meio do vertedouro (o u onde o Lakewood River começa), os vãos e os pilares.</summary>
        public const float MeioDoVertedouro = 524f, Vao = 12f, Pilar = 3.5f;
        public const int Vaos = 6;
        public const float SoleiraDoVertedouro = 140.3f, TopoDasComportas = 151.5f;
        public static float LarguraDoVertedouro => Vaos * Vao + (Vaos + 1) * Pilar;
        public static float InicioDoVertedouro => MeioDoVertedouro - LarguraDoVertedouro * 0.5f;
        public static float FimDoVertedouro => MeioDoVertedouro + LarguraDoVertedouro * 0.5f;
        /// <summary>A grossura dos muros da calha e do canal de fuga.</summary>
        public const float Muro = 2f;

        // ------------------------------------------------------------------ o que fica onde (u, v: o x e o y do Rect)

        /// <summary>A casa de força, no pé da barragem (a parede do fundo entra na face de jusante).</summary>
        public static readonly Rect CasaDeForca = Rect.MinMaxRect(596f, 24f, 734f, 80f);
        /// <summary>A bacia de dissipação (a calha do vertedouro desce até ela) e o poço do canal de fuga (a água por dentro dos muros).</summary>
        public static Rect Bacia => Rect.MinMaxRect(InicioDoVertedouro, 33f, FimDoVertedouro, 80f);
        public static Rect Poco => Rect.MinMaxRect(InicioDoVertedouro, 80f, CasaDeForca.xMax, 138f);
        /// <summary>A abertura do poço para o rio (a largura do Lakewood River no começo).</summary>
        public static readonly Vector2 SaidaDoRio = new Vector2(504f, 544f);
        public static readonly Rect Subestacao = Rect.MinMaxRect(626f, 146f, 754f, 239f);
        public static readonly Rect Administracao = Rect.MinMaxRect(724f, 268f, 744f, 316f);
        public static readonly Rect Estacionamento = Rect.MinMaxRect(786f, 266f, 836f, 322f);
        public static readonly Rect Galeria = Rect.MinMaxRect(699f, 329f, 717f, 345f);
        public static readonly Rect Oficina = Rect.MinMaxRect(798f, 334f, 826f, 374f);
        public static readonly Rect Galpao1 = Rect.MinMaxRect(800f, 384f, 828f, 414f);
        public static readonly Rect Galpao2 = Rect.MinMaxRect(800f, 424f, 828f, 454f);
        public static readonly Rect Tanque = Rect.MinMaxRect(700f, 368f, 728f, 388f);
        public static readonly Rect GuaritaDoPortao = Rect.MinMaxRect(776f, 564f, 784f, 572f);
        public static readonly Rect GuaritaLeste = Rect.MinMaxRect(906f, 9f, 914f, 17f);
        public static readonly Rect GuaritaOeste = Rect.MinMaxRect(-36f, -21f, -28f, -13f);
        /// <summary>O pátio (o chão de cascalho e terra, sem árvores): da barragem ao alambrado, do rio à encosta.</summary>
        public static readonly Rect Patio = Rect.MinMaxRect(578f, 80f, 975f, 580f);
        /// <summary>O alambrado do pátio (v) e as pontas dele (u), com o portão na estrada.</summary>
        public const float LinhaDoAlambrado = 578f, AlambradoDe = 642f, AlambradoAte = 965f;
        public static readonly Vector2 Portao = new Vector2(786f, 806f);

        // ------------------------------------------------------------------ o plano

        /// <summary>O que um pátio faz no chão.</summary>
        public enum TipoDoPatio
        {
            /// <summary>Embaixo de um prédio: o chão plano na altura dele, sem grama nem árvores.</summary>
            Edificio,
            /// <summary>O chão de cascalho, plano (a subestação, o tanque).</summary>
            Cascalho,
            /// <summary>Embaixo do asfalto (o estacionamento): plano, sem grama.</summary>
            Asfalto,
            /// <summary>Só corta o chão até a altura (a crista, a bacia, o poço): o que está abaixo fica.</summary>
            Corte,
            /// <summary>Não mexe na altura: só tira as árvores e mistura o cascalho (o pátio em volta).</summary>
            Superficie,
        }

        public struct PatioDoChao
        {
            public string Nome;
            public TipoDoPatio Tipo;
            /// <summary>Em u, v.</summary>
            public Rect Area;
            public float Altura, Talude;
            /// <summary>A grama (0 a 1) e o cascalho (0 a 1) dentro.</summary>
            public float Grama, Cascalho;
        }

        public readonly List<PatioDoChao> Patios = new List<PatioDoChao>();
        /// <summary>As estradas de acesso (Secundária), com a altura presa na crista; o chão as junta às do mapa.</summary>
        public readonly List<ViaDoMapa> Vias = new List<ViaDoMapa>();
        /// <summary>As linhas de transmissão (no plano do mapa), da subestação para fora.</summary>
        public readonly List<List<Vector2>> LinhasDeTransmissao = new List<List<Vector2>>();
        /// <summary>O colo da cabeceira oeste (a passagem do relevo; o esboço a grava no mapa).</summary>
        public static readonly Vector2[] ColoOeste = { new Vector2(4107f, 3142f), new Vector2(3880f, 3110f), new Vector2(3640f, 3020f), new Vector2(3450f, 2900f) };

        // a caixa de tudo (u, v), para as consultas saírem rápido longe da usina
        Rect _caixa;
        // as linhas de transmissão, para a faixa sem árvores
        readonly List<(Vector2 a, Vector2 b)> _vaosDasLinhas = new List<(Vector2, Vector2)>();
        const float FaixaDaLinha = 16f;

        public UsinaDeLakewood(MapaDeLakewood mapa, RelevoDeLakewood relevo)
        {
            Mapa = mapa;
            Relevo = relevo;
            var r = mapa.represa;
            Tem = r.Tem && relevo != null;
            if (!Tem) return;
            A = r.a;
            D = r.Direcao;
            J = relevo.Jusante;
            Comprimento = r.Comprimento;
            // a moldura precisa ser um giro (o x para D e o z para -J): se a jusante ficar do outro lado, o u começa em b
            if (Vector2.Dot(J, new Vector2(D.y, -D.x)) < 0f)
            {
                A = r.b;
                D = -D;
            }
            Giro = Mathf.Atan2(-D.y, D.x) * Mathf.Rad2Deg;
            Rotacao = Quaternion.Euler(0f, Giro, 0f);
            int lp = mapa.LagoPrincipal();
            Nivel = lp >= 0 && relevo.NivelDosLagos != null && lp < relevo.NivelDosLagos.Length ? relevo.NivelDosLagos[lp] : 150f;
            Planejar();
        }

        // ------------------------------------------------------------------ as coordenadas

        /// <summary>O ponto (u, v) no plano do mapa.</summary>
        public Vector2 Mundo(float u, float v) => A + D * u + J * v;

        /// <summary>Se o ponto do mapa fica na caixa de tudo o que a usina mexe no chão (os pátios, a crista e os taludes).</summary>
        public bool NaCaixa(float x, float z) => Tem && _caixa.Contains(Local(x, z));

        /// <summary>A caixa de tudo o que a usina mexe no chão, no plano do mapa (alinhada aos eixos x e z).</summary>
        public Rect CaixaNoMapa
        {
            get
            {
                if (!Tem) return default;
                Vector2 a = Mundo(_caixa.xMin, _caixa.yMin), b = Mundo(_caixa.xMax, _caixa.yMin), c = Mundo(_caixa.xMin, _caixa.yMax), d = Mundo(_caixa.xMax, _caixa.yMax);
                return Rect.MinMaxRect(Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)), Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)),
                                       Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)), Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y)));
            }
        }

        /// <summary>O (u, v) de um ponto do mapa.</summary>
        public Vector2 Local(float x, float z)
        {
            var d = new Vector2(x - A.x, z - A.y);
            return new Vector2(Vector2.Dot(d, D), Vector2.Dot(d, J));
        }

        /// <summary>Um ponto da moldura da barragem (x = u, y, z = -v) no mundo.</summary>
        public Vector3 NoMundo(Vector3 p)
        {
            var w = Mundo(p.x, -p.z);
            return new Vector3(w.x, p.y, w.y);
        }

        /// <summary>Um ponto na moldura da barragem: (u, altura, v).</summary>
        public static Vector3 L(float u, float y, float v) => new Vector3(u, y, -v);

        // ------------------------------------------------------------------ o plano (os pátios, as vias, as linhas)

        void Planejar()
        {
            float crista = Crista;
            // a crista: o chão cortado até a estrada (nas pontas, a barragem entra na rocha) e o corpo da barragem sem nada
            Patios.Add(new PatioDoChao { Nome = "Crista", Tipo = TipoDoPatio.Corte, Area = Rect.MinMaxRect(U0, -MeiaCrista - 1.5f, U1, MeiaCrista + 1.5f), Altura = crista - 0.1f, Talude = 10f });
            Patios.Add(new PatioDoChao { Nome = "Barragem", Tipo = TipoDoPatio.Superficie, Area = Rect.MinMaxRect(U0 - 15f, -16f, U1 + 15f, 56f), Grama = 0.15f, Cascalho = 0.6f });
            // o canal de fuga: o chão embaixo do fundo de concreto
            Patios.Add(new PatioDoChao { Nome = "Bacia", Tipo = TipoDoPatio.Corte, Area = Rect.MinMaxRect(InicioDoVertedouro - Muro, 30f, FimDoVertedouro + Muro, 80f), Altura = FundoDaFuga - 1.2f, Talude = 0.6f });
            Patios.Add(new PatioDoChao { Nome = "Poço", Tipo = TipoDoPatio.Corte, Area = Rect.MinMaxRect(InicioDoVertedouro - Muro, 78f, CasaDeForca.xMax + Muro, 138f + Muro), Altura = FundoDaFuga - 1.2f, Talude = 0.6f });
            Patios.Add(new PatioDoChao { Nome = "Casa de força", Tipo = TipoDoPatio.Edificio, Area = Expandir(CasaDeForca, 0f, 0f), Altura = AlturaDoPe, Talude = 8f });
            Patios.Add(Plano("Subestação", TipoDoPatio.Cascalho, Subestacao, 10f, 0.3f, 0.85f));
            Patios.Add(Plano("Administração", TipoDoPatio.Edificio, Expandir(Administracao, 3f, 3f), 8f));
            Patios.Add(Plano("Estacionamento", TipoDoPatio.Asfalto, Estacionamento, 8f));
            Patios.Add(Plano("Galeria de serviço", TipoDoPatio.Edificio, Expandir(Galeria, 2f, 2f), 6f));
            Patios.Add(Plano("Oficina", TipoDoPatio.Edificio, Expandir(Oficina, 3f, 3f), 8f));
            Patios.Add(Plano("Galpão 1", TipoDoPatio.Edificio, Expandir(Galpao1, 2f, 2f), 8f));
            Patios.Add(Plano("Galpão 2", TipoDoPatio.Edificio, Expandir(Galpao2, 2f, 2f), 8f));
            Patios.Add(Plano("Tanque", TipoDoPatio.Cascalho, Tanque, 6f, 0.25f, 0.8f));
            Patios.Add(Plano("Guarita do portão", TipoDoPatio.Edificio, Expandir(GuaritaDoPortao, 1.5f, 1.5f), 5f));
            Patios.Add(new PatioDoChao { Nome = "Guarita leste", Tipo = TipoDoPatio.Edificio, Area = Expandir(GuaritaLeste, 1.5f, 1.5f), Altura = crista + 0.05f, Talude = 6f });
            Patios.Add(new PatioDoChao { Nome = "Guarita oeste", Tipo = TipoDoPatio.Edificio, Area = Expandir(GuaritaOeste, 1.5f, 1.5f), Altura = crista + 0.05f, Talude = 6f });
            Patios.Add(new PatioDoChao { Nome = "Pátio", Tipo = TipoDoPatio.Superficie, Area = Patio, Grama = 0.75f, Cascalho = 0.45f });
            // o pé da casa de força e do vertedouro (entre os muros, onde nasciam pinheiros)
            Patios.Add(new PatioDoChao { Nome = "Pé da casa de força", Tipo = TipoDoPatio.Superficie, Area = Rect.MinMaxRect(InicioDoVertedouro - 12f, 20f, CasaDeForca.xMax + 24f, 145f), Grama = 0.35f, Cascalho = 0.6f });

            // as estradas de acesso (os pontos em u, v; a altura presa na crista)
            ViaDoMapa Via(string nome, float alturaNoComeco, float rampa, params float[] uv)
            {
                var via = new ViaDoMapa { nome = nome, tipo = TipoDeVia.Secundaria, alturaNoComeco = alturaNoComeco, rampaMaxima = rampa };
                for (int k = 0; k + 1 < uv.Length; k += 2) via.pontos.Add(Mundo(uv[k], uv[k + 1]));
                return via;
            }
            // a da barragem desce 51 m pela encosta leste até o portão (rente a ela, mais longa: pelo caminho curto, com os 9%
            // das outras, ela ficava acima do chão e virava ponte), só descendo a partir da crista, e segue pelo vale
            var barragem = Via("Lakewood Dam Road", crista, 0.12f,
                               900f, 0f, 922f, 3f, 940f, 16f, 950f, 45f, 955f, 110f, 960f, 200f, 962f, 300f, 958f, 400f, 945f, 490f,
                               900f, 525f, 845f, 545f, 810f, 556f, 798f, 566f, 796f, 580f, 800f, 640f, 804f, 700f, 828f, 850f, 850f, 1000f);
            barragem.soDesce = true;
            Vias.Add(barragem);
            Vias.Add(Via("Powerhouse Road", AlturaDoPe + 0.1f, 0f,
                         746f, 52f, 756f, 120f, 764f, 200f, 771f, 300f, 778f, 400f, 786f, 470f, 792f, 525f, 797f, 556f));
            var oeste = new ViaDoMapa { nome = "Dam West Road", tipo = TipoDeVia.Secundaria, alturaNoComeco = crista, rampaMaxima = 0.1f };
            oeste.pontos.Add(Mundo(-20f, 0f));
            oeste.pontos.Add(Mundo(-60f, 2f));
            for (int k = 1; k < ColoOeste.Length; k++) oeste.pontos.Add(ColoOeste[k]);
            Vias.Add(oeste);

            // as linhas de transmissão (a primeira torre de cada uma logo fora da subestação)
            LinhasDeTransmissao.Add(new List<Vector2> { Mundo(812f, 205f), new Vector2(5350f, 2600f), new Vector2(6100f, 2450f), new Vector2(6900f, 2550f), new Vector2(7700f, 2750f), new Vector2(8350f, 2950f) });
            LinhasDeTransmissao.Add(new List<Vector2> { Mundo(690f, 262f), new Vector2(4705f, 2300f), new Vector2(4725f, 1700f), new Vector2(4800f, 1100f), new Vector2(4850f, 500f), new Vector2(4880f, 40f) });

            // a caixa de tudo o que mexe no chão
            _caixa = Rect.MinMaxRect(U0 - 60f, -60f, U1 + 60f, 640f);
            foreach (var p in Patios) _caixa = Rect.MinMaxRect(Mathf.Min(_caixa.xMin, p.Area.xMin - p.Talude), Mathf.Min(_caixa.yMin, p.Area.yMin - p.Talude),
                                                              Mathf.Max(_caixa.xMax, p.Area.xMax + p.Talude), Mathf.Max(_caixa.yMax, p.Area.yMax + p.Talude));
            PlanejarAsTorres();
        }

        static Rect Expandir(Rect r, float du, float dv) => Rect.MinMaxRect(r.xMin - du, r.yMin - dv, r.xMax + du, r.yMax + dv);

        // um pátio plano na altura média do relevo embaixo dele (arredondada em 10 cm)
        PatioDoChao Plano(string nome, TipoDoPatio tipo, Rect area, float talude, float grama = 0f, float cascalho = 0.8f)
        {
            float soma = 0f;
            int n = 0;
            for (int j = 0; j <= 4; j++)
            for (int i = 0; i <= 4; i++)
            {
                var p = Mundo(Mathf.Lerp(area.xMin, area.xMax, i / 4f), Mathf.Lerp(area.yMin, area.yMax, j / 4f));
                soma += Relevo.Altura(p.x, p.y);
                n++;
            }
            float h = Mathf.Round(soma / n * 10f) / 10f;
            return new PatioDoChao { Nome = nome, Tipo = tipo, Area = area, Altura = h, Talude = talude, Grama = grama, Cascalho = cascalho };
        }

        /// <summary>A altura de um pátio pelo nome (para as peças que ficam em cima dele).</summary>
        public float AlturaDoPatio(string nome)
        {
            foreach (var p in Patios) if (p.Nome == nome) return p.Altura;
            return AlturaDoPe;
        }

        // ------------------------------------------------------------------ as consultas do chão (seguras em várias threads)

        // a distância (u, v) de um ponto até o retângulo (0 dentro)
        static float DistanciaDoRetangulo(Rect r, Vector2 p)
        {
            float du = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dv = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(du * du + dv * dv);
        }

        /// <summary>
        /// O chão com os pátios da usina: dentro de cada um, a altura dele (nos de corte, só onde o chão está acima); na
        /// beira, a passagem até o chão de fora no talude. "h" é a altura sem a usina.
        /// </summary>
        public float Chao(float x, float z, float h)
        {
            if (!Tem) return h;
            var p = Local(x, z);
            if (!_caixa.Contains(p)) return h;
            for (int k = 0; k < Patios.Count; k++)
            {
                var pt = Patios[k];
                if (pt.Tipo == TipoDoPatio.Superficie) continue;
                float d = DistanciaDoRetangulo(pt.Area, p);
                if (d > pt.Talude) continue;
                float alvo = d <= 0f ? pt.Altura : Mathf.Lerp(pt.Altura, h, Geo.Suave(0f, pt.Talude, d));
                h = pt.Tipo == TipoDoPatio.Corte ? Mathf.Min(h, alvo) : alvo;
            }
            return h;
        }

        /// <summary>A água da usina (a bacia e o poço do canal de fuga) no ponto, e a altura dela.</summary>
        public bool Agua(float x, float z, out float nivel, float folga = 0f)
        {
            nivel = NivelDaFuga;
            if (!Tem) return false;
            var p = Local(x, z);
            if (!_caixa.Contains(p)) return false;
            return Expandir(Bacia, folga, folga).Contains(p) || Expandir(Poco, folga, folga).Contains(p);
        }

        /// <summary>Sem árvores: os pátios (com a beira), o corpo da barragem e a faixa embaixo das linhas.</summary>
        public bool SemArvores(float x, float z)
        {
            if (!Tem) return false;
            var p = Local(x, z);
            if (_caixa.Contains(p))
                foreach (var pt in Patios)
                    if (DistanciaDoRetangulo(pt.Area, p) < 4f) return true;
            var q = new Vector2(x, z);
            foreach (var (a, b) in _vaosDasLinhas)
                if (Linhas.DistanciaDoSegmento(q, a, b, out _) < FaixaDaLinha) return true;
            return false;
        }

        /// <summary>O quanto da grama fica (0 a 1): nada embaixo dos prédios, do asfalto e na água; rala no cascalho.</summary>
        public float FatorDaGrama(float x, float z)
        {
            if (!Tem) return 1f;
            var p = Local(x, z);
            if (!_caixa.Contains(p)) return 1f;
            if (Bacia.Contains(p) || Poco.Contains(p)) return 0f;
            // embaixo da crista (o chão fica 10 cm abaixo do tabuleiro, e os tufos furavam o asfalto)
            if (p.x >= U0 - 1f && p.x <= U1 + 1f && Mathf.Abs(p.y) <= MeiaCrista + 2f) return 0f;
            float f = 1f;
            foreach (var pt in Patios)
            {
                if (pt.Tipo == TipoDoPatio.Corte) continue;
                float d = DistanciaDoRetangulo(pt.Area, p);
                if (d > 2f) continue;
                f = Mathf.Min(f, Mathf.Lerp(pt.Grama, 1f, Geo.Suave(0f, 2f, d)));
            }
            return f;
        }

        /// <summary>O quanto o chão é cascalho (0 a 1), para os pesos das texturas.</summary>
        public float Cascalho(float x, float z)
        {
            if (!Tem) return 0f;
            var p = Local(x, z);
            if (!_caixa.Contains(p)) return 0f;
            float c = 0f;
            foreach (var pt in Patios)
            {
                float d = DistanciaDoRetangulo(pt.Area, p);
                float alcance = pt.Tipo == TipoDoPatio.Superficie ? 6f : Mathf.Max(3f, pt.Talude);
                if (d > alcance) continue;
                float w = pt.Tipo == TipoDoPatio.Corte ? 0.7f : pt.Cascalho;
                c = Mathf.Max(c, w * (1f - Geo.Suave(0f, alcance, d)));
            }
            return c;
        }

        // ------------------------------------------------------------------ as torres de transmissão

        /// <summary>Uma torre: a base no plano do mapa e o giro dela (graus; os braços ficam de través na linha).</summary>
        public struct Torre
        {
            public Vector2 Ponto;
            public float Giro;
            public int Linha;
        }

        public readonly List<Torre> Torres = new List<Torre>();
        /// <summary>O vão máximo entre duas torres (m).</summary>
        public const float VaoDasTorres = 330f;

        void PlanejarAsTorres()
        {
            for (int l = 0; l < LinhasDeTransmissao.Count; l++)
            {
                var linha = LinhasDeTransmissao[l];
                var pontos = new List<Vector2>();
                for (int s = 1; s < linha.Count; s++)
                {
                    var a = linha[s - 1];
                    var b = linha[s];
                    int partes = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / VaoDasTorres));
                    for (int q = 0; q < partes; q++) pontos.Add(Vector2.Lerp(a, b, q / (float)partes));
                }
                pontos.Add(linha[linha.Count - 1]);
                for (int i = 0; i < pontos.Count; i++)
                {
                    // a torre não fica na água, no rio, nos pátios nem em cima de uma estrada: escorrega ao longo da linha
                    var dir = (pontos[Mathf.Min(i + 1, pontos.Count - 1)] - pontos[Mathf.Max(i - 1, 0)]).normalized;
                    var p = pontos[i];
                    for (int tenta = 0; tenta < 12 && !LugarDaTorre(p); tenta++)
                        p = pontos[i] + dir * (((tenta + 2) / 2) * 14f * (tenta % 2 == 0 ? -1f : 1f));
                    pontos[i] = p;
                    Torres.Add(new Torre { Ponto = p, Giro = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, Linha = l });
                }
                for (int i = 1; i < pontos.Count; i++) _vaosDasLinhas.Add((pontos[i - 1], pontos[i]));
            }
        }

        List<List<Vector2>> _linhasDoMapa;

        bool LugarDaTorre(Vector2 p)
        {
            if (!Mapa.Dentro(p.x, p.y)) return false;
            float h = Relevo.Altura(p.x, p.y);
            int lp = Mapa.LagoPrincipal();
            if (h < Nivel + 2f && lp >= 0) return false;
            if (Relevo.Inclinacao(p.x, p.y) > 0.6f) return false;
            var q = Local(p.x, p.y);
            foreach (var pt in Patios)
                if (pt.Tipo != TipoDoPatio.Superficie && DistanciaDoRetangulo(pt.Area, q) < 8f) return false;
            foreach (var via in Vias)
                for (int s = 1; s < via.pontos.Count; s++)
                    if (Linhas.DistanciaDoSegmento(p, via.pontos[s - 1], via.pontos[s], out _) < 14f) return false;
            // as estradas do mapa (Etapa 5), pela linha arredondada delas
            if (_linhasDoMapa == null)
            {
                _linhasDoMapa = new List<List<Vector2>>();
                foreach (var via in Mapa.estradas)
                    if (via.pontos.Count >= 2) _linhasDoMapa.Add(MapaDeLakewood.Linha(via.pontos));
            }
            foreach (var linha in _linhasDoMapa)
                if (Linhas.Distancia(p, linha) < 18f) return false;
            // o rio de saída
            int ri = Mapa.rios.FindIndex(r => r.nome == "Lakewood River");
            if (ri >= 0 && Relevo.LinhasDosRios != null && Relevo.LinhasDosRios[ri] != null)
            {
                var linha = Relevo.LinhasDosRios[ri];
                for (int s = 1; s < linha.Count; s++)
                    if (Linhas.DistanciaDoSegmento(p, linha[s - 1], linha[s], out _) < 40f) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ o desenho de uma peça

        /// <summary>
        /// O desenho de uma peça, com os pontos na moldura da barragem (<see cref="L"/>): a malha fica nas coordenadas da
        /// peça (a moldura menos a origem). A colisão é à parte (o que tem 'colide').
        /// </summary>
        sealed class Desenho
        {
            public readonly MalhaDeObra Vis = new MalhaDeObra(2048), Col = new MalhaDeObra(512);
            public readonly Vector3 Origem;
            readonly List<Vector3> _p = new List<Vector3>(8), _q = new List<Vector3>(8);

            public Desenho(Vector3 origem) { Origem = origem; }

            Matrix4x4 M(Vector3 c, Quaternion giro) => Matrix4x4.TRS(c - Origem, giro, Vector3.one);

            public void Caixa(Vector3 c, Vector3 tam, Cor cor, bool colide = false, float giroY = 0f, float giroX = 0f, float giroZ = 0f)
            {
                var m = M(c, Quaternion.Euler(giroX, giroY, giroZ));
                Vis.Caixa(m, tam, cor);
                if (colide) Col.Caixa(m, tam, "Concreto");
            }

            /// <summary>Só a colisão (a caixa invisível).</summary>
            public void Colide(Vector3 c, Vector3 tam, float giroY = 0f) => Col.Caixa(M(c, Quaternion.Euler(0f, giroY, 0f)), tam, "Concreto");

            /// <summary>Uma caixa de a até b (a linha do meio), com a seção largura × altura.</summary>
            public void Entre(Vector3 a, Vector3 b, float largura, float altura, Cor cor, bool colide = false)
            {
                var d = b - a;
                if (d.sqrMagnitude < 1e-6f) return;
                var cima = Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
                var m = M((a + b) * 0.5f, Quaternion.LookRotation(d, cima));
                var tam = new Vector3(largura, altura, d.magnitude);
                Vis.Caixa(m, tam, cor);
                if (colide) Col.Caixa(m, tam, "Concreto");
            }

            public void Face(IList<Vector3> p, Vector3 fora, Cor cor, bool colide = false)
            {
                _p.Clear();
                foreach (var q in p) _p.Add(q - Origem);
                Vis.Face(_p, fora, cor);
                if (colide) Col.Face(_p, fora, "Concreto");
            }

            /// <summary>Um quadrilátero (a, b, c, d em volta).</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 fora, Cor cor, bool colide = false)
            {
                _q.Clear();
                _q.Add(a); _q.Add(b); _q.Add(c); _q.Add(d);
                Face(_q, fora, cor, colide);
            }

            /// <summary>Um quadrilátero dos dois lados (os arames, as chapas finas).</summary>
            public void QuadDuplo(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 fora, Cor cor)
            {
                Quad(a, b, c, d, fora, cor);
                Quad(a, b, c, d, -fora, cor);
            }

            public void Solido(IList<Vector3> a, IList<Vector3> b, Cor cor, bool colide = false)
            {
                var A2 = new List<Vector3>(a.Count);
                var B2 = new List<Vector3>(b.Count);
                foreach (var q in a) A2.Add(q - Origem);
                foreach (var q in b) B2.Add(q - Origem);
                Vis.Solido(A2, B2, cor);
                if (colide) Col.Solido(A2, B2, "Concreto");
            }

            /// <summary>Só a colisão de um sólido (dois polígonos convexos paralelos).</summary>
            public void ColideSolido(IList<Vector3> a, IList<Vector3> b)
            {
                var A2 = new List<Vector3>(a.Count);
                var B2 = new List<Vector3>(b.Count);
                foreach (var q in a) A2.Add(q - Origem);
                foreach (var q in b) B2.Add(q - Origem);
                Col.Solido(A2, B2, "Concreto");
            }

            public void Cilindro(Vector3 baseC, float r0, float r1, float h, int lados, Cor cor, bool tampas = true) =>
                Vis.Cilindro(Matrix4x4.Translate(baseC - Origem), r0, r1, h, lados, cor, tampas);

            /// <summary>Um cilindro deitado ao longo de x, com o meio em c.</summary>
            public void CilindroDeitado(Vector3 c, float r, float comprimento, int lados, Cor cor) =>
                Vis.Cilindro(Matrix4x4.TRS(c - Origem + Vector3.left * (comprimento * 0.5f), Quaternion.Euler(0f, 0f, -90f), Vector3.one), r, r, comprimento, lados, cor);

            public void Tubo(Vector3 a, Vector3 b, float r, int lados, Cor cor) => Vis.Tubo(a - Origem, b - Origem, r, lados, cor);

            /// <summary>Uma malha pronta (um modelo da estrada), levada por M (nas coordenadas da moldura).</summary>
            public void Juntar(MalhaDeObra outra, Matrix4x4 M) => Vis.Juntar(outra, Matrix4x4.Translate(-Origem) * M);

            /// <summary>
            /// Um letreiro chapado na fonte das construções (<see cref="Letras"/>), centrado em 'meio' (no pé das letras),
            /// correndo em 'direita' e subindo em 'cima', saindo 'sai' da face (para 'fora'). 'pular' é a letra que caiu.
            /// </summary>
            public void Letreiro(string texto, Vector3 meio, Vector3 direita, Vector3 cima, Vector3 fora, float h, Cor cor, float sai = 0.03f, int pular = -1)
            {
                float total = Letras.Largura(texto, h);
                var inicio = meio - direita * (total * 0.5f);
                float x = 0f;
                for (int i = 0; i < texto.Length; i++)
                {
                    var g = Letras.De(texto[i]);
                    if (g == null) { x += Letras.Espaco + Letras.Entre; continue; }
                    if (i != pular)
                        foreach (var parte in g.partes)
                        {
                            _p.Clear();
                            foreach (var q in parte) _p.Add(inicio + direita * ((x + q.x) * h) + cima * (q.y * h) + fora * sai - Origem);
                            Vis.Face(_p, fora, cor);
                        }
                    x += g.largura + Letras.Entre;
                }
            }
        }

        // a peça pronta de um desenho
        PecaDaUsina Peca(string nome, Desenho d, bool colide = true) => new PecaDaUsina
        {
            Nome = nome,
            Tipo = TipoDaPecaDaUsina.Obra,
            Posicao = NoMundo(d.Origem),
            Giro = Rotacao,
            Desenho = d.Vis,
            Colisao = colide && d.Col.Triangulos > 0 ? d.Col : null,
        };

        // ------------------------------------------------------------------ as malhas

        /// <summary>
        /// Todas as peças da usina (as malhas feitas aqui; os carros e as barreiras só com o lugar). "altura" é o chão de
        /// perto (o <see cref="ChaoDeLakewood.Altura"/>, com os pátios e as estradas): o que fica no chão acompanha ele.
        /// </summary>
        public List<PecaDaUsina> Montar(Func<float, float, float> altura)
        {
            var saida = new List<PecaDaUsina>();
            if (!Tem) return saida;
            var sorte = new System.Random(unchecked((int)Sorteio.Hash(Mapa.semente, 4, 1, 77)));
            // a altura do chão num ponto (u, v)
            float H(float u, float v)
            {
                var w = Mundo(u, v);
                return altura(w.x, w.y);
            }
            MontarABarragem(saida, sorte, H);
            MontarOCanalDeFuga(saida);
            MontarOsEdificios(saida, sorte, H);
            MontarASubestacao(saida, sorte, H);
            MontarAsLinhas(saida, altura);
            return saida;
        }
    }
}
