using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    public enum TipoDeRua : byte { Rua, Avenida }

    public enum TipoDeLote : byte { Casa, Loja, Predio, Praca, Estacionamento, Baldio, Laboratorio, Familia }

    /// <summary>
    /// Um lote da cidade, no plano local dela (u, v; <see cref="TracadoDaCidade"/>): o terreno (um retângulo nos eixos da
    /// grade, da divisa com a calçada até o fundo), para onde a frente olha (a rua) e a construção (outro retângulo, dentro
    /// do terreno), com os andares e a altura das paredes. A altura do chão sai na hora de montar a malha.
    /// </summary>
    public sealed class Lote
    {
        public TipoDeLote Tipo;
        public TipoDeBairro Zona;
        /// <summary>O terreno no plano local.</summary>
        public float U0, U1, V0, V1;
        /// <summary>Para onde a frente olha (a rua): (±1, 0) ou (0, ±1), no plano local.</summary>
        public Vector2 Frente;
        /// <summary>A construção no plano local (dentro do terreno); sem construção, a área toda.</summary>
        public float BU0, BU1, BV0, BV1;
        public int Andares;
        /// <summary>A altura das paredes (m), do chão mais alto embaixo dela até o topo (sem o telhado).</summary>
        public float Altura;
        public string Cor, CorDoTelhado;
        public uint Semente;
        /// <summary>O quarteirão (a célula da grade: i × linhas v + j) onde ele fica.</summary>
        public int Celula;

        /// <summary>
        /// A casa de verdade (Parte M6; null nos lotes que não são de casa): a planta, o meio dela no plano local e a altura
        /// do chão dela (o patamar). No plano local, o retângulo que ela ocupa (com a varanda, a garagem, o fosso e os
        /// degraus) e o do patamar (o chão nivelado; ele volta ao chão da cidade em 2,5 m). A caixa da construção
        /// (BU0 a BV1, a de longe) é o corpo da casa.
        /// </summary>
        public Planta Casa;
        public Vector2 MeioDaCasa;
        public float ChaoDaCasa;
        public Rect RetDaCasa, RetDoPatamar;

        /// <summary>
        /// O prédio de verdade (Parte M6, segunda fatia; null nos lotes que não são de prédio). Usa os mesmos campos da
        /// casa: o meio, o giro (a frente para a rua) e o retângulo dele; o chão é o mais alto embaixo dele (o gerador acerta
        /// depois do relevo), sem patamar.
        /// </summary>
        public PlantaDePredio Predio;

        /// <summary>
        /// A loja de verdade (Parte M6, terceira fatia; null nos lotes que não são de loja), como o prédio: o meio, o giro e o
        /// retângulo dela nos campos da casa, e o chão o mais alto embaixo dela.
        /// </summary>
        public PlantaDeLoja Loja;

        /// <summary>O eixo z da casa no plano local: a frente da casa (-z) olha para a rua, a frente do lote.</summary>
        public Vector2 FundoDaCasa => -Frente;
        /// <summary>O eixo x da casa no plano local.</summary>
        public Vector2 LadoDaCasa => new Vector2(-Frente.y, Frente.x);
        /// <summary>Um ponto da casa (x, z, nas coordenadas dela, já espelhadas) no plano local.</summary>
        public Vector2 NaCasa(float x, float z) => MeioDaCasa + LadoDaCasa * x + FundoDaCasa * z;

        public Vector2 Meio => new Vector2((U0 + U1) * 0.5f, (V0 + V1) * 0.5f);
        public bool TemConstrucao => Andares > 0;
    }

    /// <summary>
    /// Por onde uma estrada sai da cidade: a ponta de uma das avenidas do meio (Plano_Mapa, Parte M5). A rodovia começa na
    /// beira do cruzamento da ponta (<see cref="Ponto"/>), segue a avenida até sair do contorno (<see cref="Prefixo"/>, até
    /// <see cref="Saida"/>) e dali vai pelo relevo. Dentro da cidade, quem anda vai pelas ruas: o <see cref="Conector"/> (do
    /// meio da cidade até o ponto) entra na rede de estradas só como caminho (não é desenhado).
    /// </summary>
    public sealed class Portao
    {
        /// <summary>A direção da avenida para fora, no plano local: (±1, 0) ou (0, ±1).</summary>
        public Vector2 DirLocal;
        public int I, J;
        public Vector2 Ponto, Saida, Dir;
        public List<Vector2> Prefixo, Conector;
        public bool Usado;
    }

    /// <summary>
    /// O traçado de uma cidade (ou vila) em blocos (Plano_Mapa, Parte M5): a grade de ruas no giro da cidade, um pouco torta
    /// (as linhas com espaços diferentes e o plano todo deformado por ondas largas; no centro da cidade grande, também por
    /// ondas curtas: as ruas antigas, mais irregulares), as avenidas (as do meio e, na cidade grande, uma a cada 4 linhas),
    /// os cruzamentos, as calçadas, os quarteirões com os lotes por zona (o centro com os prédios de 6 a 15 andares, o
    /// comércio com as lojas de 1 a 3 andares e os bairros com as casas de 1 e 2 andares), as praças, o terreno do
    /// laboratório, os portões das rodovias e o chão aplainado.
    ///
    /// Tudo é pensado no plano local (u, v), onde as ruas são retas e os lotes são retângulos: u no eixo
    /// <see cref="EixoU"/> (o giro da cidade), v no <see cref="EixoV"/>, e o meio da cidade na origem. <see cref="Mundo"/>
    /// leva um ponto local para o mapa (com a deformação) e <see cref="Local"/> faz o contrário. As linhas "u" são as de u
    /// constante (as ruas que correm ao longo de v) e as linhas "v", as de v constante.
    /// </summary>
    public sealed class TracadoDaCidade
    {
        public const float MeiaRua = 4f, CalcadaDaRua = 2.5f, MeiaAvenida = 7f, CalcadaDaAvenida = 4f;
        /// <summary>O asfalto das ruas e a calçada acima do chão (m): o meio-fio tem 15 cm.</summary>
        public const float AlturaDoAsfalto = 0.05f, AlturaDaCalcada = 0.2f;
        /// <summary>A rua é cortada em pedaços de até isto (m) para a malha seguir o chão e a deformação.</summary>
        public const float Pedaco = 12f;
        /// <summary>O fundo máximo de um lote (m): quarteirões mais largos ficam com um miolo vazio.</summary>
        public const float FundoMaximo = 36f;
        public const byte MaisU = 1, MenosU = 2, MaisV = 4, MenosV = 8;

        public static float MeiaPista(TipoDeRua t) => t == TipoDeRua.Avenida ? MeiaAvenida : MeiaRua;
        public static float Calcada(TipoDeRua t) => t == TipoDeRua.Avenida ? CalcadaDaAvenida : CalcadaDaRua;

        public readonly Cidade Cidade;
        public readonly int Indice;
        public readonly Vector2 Centro, EixoU, EixoV;
        public float[] Us, Vs;
        /// <summary>Os índices das linhas do meio (u = 0 e v = 0).</summary>
        public int IC, JC;
        public TipoDeRua[] TipoU, TipoV;
        public string[] NomeU, NomeV;
        /// <summary>O cruzamento (i, j) está dentro da cidade.</summary>
        public bool[,] Dentro;
        /// <summary>A rua da linha u i, de v[j] até v[j + 1].</summary>
        public bool[,] RuaU;
        /// <summary>A rua da linha v j, de u[i] até u[i + 1].</summary>
        public bool[,] RuaV;
        /// <summary>Os braços do cruzamento que continuam como estrada (os portões): não fecham com calçada.</summary>
        public byte[,] Abertos;
        public readonly List<Portao> Portoes = new List<Portao>();
        public readonly List<Lote> Lotes = new List<Lote>();

        /// <summary>Um pedaço de rua: da linha u (corre ao longo de v; De e Ate em v) ou da linha v (ao longo de u).</summary>
        public struct Peca
        {
            public bool DaLinhaU;
            public int Linha;
            public float De, Ate;
        }

        public readonly List<Peca> Pecas = new List<Peca>();
        public readonly List<Vector2Int> Cruzamentos = new List<Vector2Int>();

        /// <summary>
        /// A calçada dos dois lados do pedaço p, sem as esquinas (as dos cruzamentos; a mesma conta da montagem da cidade):
        /// de c0 a c1 ao longo da rua, com a meia pista (a, do eixo ao meio-fio) e a largura da calçada (s). Falso se não
        /// sobra calçada.
        /// </summary>
        public bool CalcadaDaPeca(Peca p, out float c0, out float c1, out float a, out float s)
        {
            var tipo = p.DaLinhaU ? TipoU[p.Linha] : TipoV[p.Linha];
            a = MeiaPista(tipo);
            s = Calcada(tipo);
            var linhas = p.DaLinhaU ? Vs : Us;
            float meio = (p.De + p.Ate) * 0.5f;
            int j = 0;
            while (j + 1 < linhas.Length && !(meio >= linhas[j] && meio <= linhas[j + 1])) j++;
            float esquina0 = p.DaLinhaU ? linhas[j] + AV(j) + SV(j) : linhas[j] + AU(j) + SU(j);
            float esquina1 = p.DaLinhaU ? linhas[j + 1] - AV(j + 1) - SV(j + 1) : linhas[j + 1] - AU(j + 1) - SU(j + 1);
            c0 = Mathf.Max(p.De, esquina0);
            c1 = Mathf.Min(p.Ate, esquina1);
            return c1 > c0;
        }

        /// <summary>O ponto local do pedaço p a 'aoLongo' da rua e 'atravessado' do eixo dela.</summary>
        public Vector2 NaPeca(Peca p, float aoLongo, float atravessado)
        {
            float eixo = p.DaLinhaU ? Us[p.Linha] : Vs[p.Linha];
            return p.DaLinhaU ? new Vector2(eixo + atravessado, aoLongo) : new Vector2(aoLongo, eixo + atravessado);
        }

        /// <summary>O que cada bloco de 64 m tem desta cidade: os pedaços de rua, os cruzamentos e os lotes (pelo meio).</summary>
        public sealed class DoBloco
        {
            public readonly List<int> Pecas = new List<int>(), Cruzamentos = new List<int>(), Lotes = new List<int>();
        }

        readonly Dictionary<int, DoBloco> _porBloco = new Dictionary<int, DoBloco>();
        readonly DadosDoMundo _d;
        readonly Ruido _ondas;
        readonly float _fase, _ampLarga, _ampCentro, _raioCentro;
        readonly Vector2 _desvioNoMeio;
        // o chão aplainado: uma grade de 16 m, borrada e com a subida limitada
        float[] _chao;
        int _nChao;
        Vector2 _origemChao;
        const float PassoDoChao = 16f;

        public bool EhVila => Cidade.Tipo == TipoDeLugar.Vila;

        /// <summary>Até onde (q) vão os cruzamentos: o contorno; na vila, que é pequena, um pouco além (senão sobrava uma cruz).</summary>
        public float LimiteDasRuas => EhVila ? 1.2f : 1f;

        public TracadoDaCidade(DadosDoMundo d, Cidade cidade, int indice, Ruido ondas)
        {
            _d = d;
            Cidade = cidade;
            Indice = indice;
            Centro = cidade.Centro;
            float g = cidade.Giro * Mathf.Deg2Rad;
            EixoU = new Vector2(Mathf.Cos(g), Mathf.Sin(g));
            EixoV = new Vector2(-Mathf.Sin(g), Mathf.Cos(g));
            _ondas = ondas;
            var s = Sorteio.Da(d.Semente, "ruas-" + indice);
            _fase = s.Entre(0f, 200f);
            (_ampLarga, _ampCentro) = cidade.Tipo switch
            {
                TipoDeLugar.CidadeGrande => (6f, 7f),
                TipoDeLugar.CidadePequena => (5f, 3f),
                _ => (2.5f, 0f),
            };
            _raioCentro = cidade.Raio * 0.32f;
            _desvioNoMeio = DeformacaoCrua(0f, 0f);
            Linhas(s);
        }

        // ------------------------------------------------------------------ o plano local e o mapa

        Vector2 DeformacaoCrua(float u, float v)
        {
            const float larga = 1f / 380f, curta = 1f / 130f;
            float x = _ondas.Amostra(u * larga + _fase, v * larga - _fase) * _ampLarga;
            float z = _ondas.Amostra(u * larga - _fase + 41.7f, v * larga + _fase - 13.3f) * _ampLarga;
            if (_ampCentro > 0f)
            {
                float a = _ampCentro * Mathf.Exp(-(u * u + v * v) / (_raioCentro * _raioCentro));
                if (a > 0.01f)
                {
                    x += _ondas.Amostra(u * curta + _fase + 7.7f, v * curta - _fase + 3.1f) * a;
                    z += _ondas.Amostra(u * curta - _fase - 5.5f, v * curta + _fase + 9.9f) * a;
                }
            }
            return new Vector2(x, z);
        }

        /// <summary>O ponto do mapa (x, z) de um ponto local (u, v). O meio da cidade fica exatamente no centro dela.</summary>
        public Vector2 Mundo(float u, float v) => Centro + EixoU * u + EixoV * v + DeformacaoCrua(u, v) - _desvioNoMeio;

        public Vector2 Mundo(Vector2 local) => Mundo(local.x, local.y);

        /// <summary>O ponto local de um ponto do mapa (a deformação desfeita aos poucos: ela é suave e pequena).</summary>
        public Vector2 Local(Vector2 p)
        {
            var d = p - Centro;
            float u = Vector2.Dot(d, EixoU), v = Vector2.Dot(d, EixoV);
            for (int it = 0; it < 4; it++)
            {
                var dd = d - DeformacaoCrua(u, v) + _desvioNoMeio;
                u = Vector2.Dot(dd, EixoU);
                v = Vector2.Dot(dd, EixoV);
            }
            return new Vector2(u, v);
        }

        /// <summary>A distância relativa até a beira da cidade (<see cref="Cidade.Q"/>) de um ponto local.</summary>
        public float Q(float u, float v)
        {
            var p = Mundo(u, v);
            return Cidade.Q(p.x, p.y);
        }

        public float AU(int i) => MeiaPista(TipoU[i]);
        public float SU(int i) => Calcada(TipoU[i]);
        public float AV(int j) => MeiaPista(TipoV[j]);
        public float SV(int j) => Calcada(TipoV[j]);

        /// <summary>Os braços do cruzamento (i, j): <see cref="MaisU"/>, <see cref="MenosU"/>, <see cref="MaisV"/>, <see cref="MenosV"/>.</summary>
        public byte Bracos(int i, int j)
        {
            byte b = 0;
            if (i + 1 < Us.Length && RuaV[i, j]) b |= MaisU;
            if (i > 0 && RuaV[i - 1, j]) b |= MenosU;
            if (j + 1 < Vs.Length && RuaU[i, j]) b |= MaisV;
            if (j > 0 && RuaU[i, j - 1]) b |= MenosV;
            return b;
        }

        // ------------------------------------------------------------------ as linhas e os nomes

        static readonly string[] Avenidas =
        {
            "Avenida Brasil", "Avenida Getúlio Vargas", "Avenida Paulista", "Avenida Presidente Vargas", "Avenida Rio Branco",
            "Avenida Tiradentes", "Avenida Santos Dumont", "Avenida das Nações", "Avenida Independência",
            "Avenida Juscelino Kubitschek", "Avenida Duque de Caxias", "Avenida São João", "Avenida Atlântica", "Avenida Ipiranga",
            "Avenida dos Bandeirantes", "Avenida Marechal Deodoro", "Avenida Afonso Pena", "Avenida Sete de Setembro",
            "Avenida Floriano Peixoto", "Avenida Dom Pedro II",
        };

        static readonly string[] Ruas =
        {
            "Rua XV de Novembro", "Rua Sete de Setembro", "Rua Tiradentes", "Rua Dom Pedro II", "Rua Barão do Rio Branco",
            "Rua São Paulo", "Rua Minas Gerais", "Rua Bahia", "Rua Paraná", "Rua Goiás", "Rua Pernambuco", "Rua Santa Catarina",
            "Rua Rio de Janeiro", "Rua Amazonas", "Rua Ceará", "Rua das Flores", "Rua das Palmeiras", "Rua dos Ipês",
            "Rua das Acácias", "Rua dos Girassóis", "Rua das Margaridas", "Rua das Orquídeas", "Rua dos Jasmins",
            "Rua das Hortênsias", "Rua dos Cravos", "Rua Castro Alves", "Rua José de Alencar", "Rua Machado de Assis",
            "Rua Rui Barbosa", "Rua Carlos Gomes", "Rua Olavo Bilac", "Rua Monteiro Lobato", "Rua Cecília Meireles",
            "Rua Santos Dumont", "Rua Duque de Caxias", "Rua Marechal Rondon", "Rua Benjamin Constant", "Rua Joaquim Nabuco",
            "Rua Visconde de Mauá", "Rua Princesa Isabel", "Rua Anchieta", "Rua Bela Vista", "Rua Boa Esperança",
            "Rua do Comércio", "Rua da Matriz", "Rua do Rosário", "Rua da Paz", "Rua da Liberdade", "Rua São Jorge",
            "Rua São Francisco", "Rua Santo Antônio", "Rua São Benedito", "Rua Nossa Senhora Aparecida", "Rua Primeiro de Maio",
            "Rua Treze de Maio", "Rua Vinte e Um de Abril", "Rua Doze de Outubro", "Rua Padre Anchieta", "Rua Coronel Fagundes",
            "Rua Major Sertório", "Rua Capitão Salomão", "Rua Professor Lima", "Rua Doutor Mário Ribeiro",
            "Rua Manoel da Nóbrega", "Rua Cabral", "Rua Floriano Peixoto", "Rua Getúlio Vargas", "Rua Marechal Deodoro",
            "Rua Espírito Santo", "Rua Alagoas", "Rua Sergipe", "Rua Piauí", "Rua Maranhão", "Rua Pará", "Rua Acre",
            "Rua Tocantins", "Rua Rondônia", "Rua Roraima", "Rua Amapá", "Rua Mato Grosso", "Rua Paraíba",
            "Rua Rio Grande do Sul", "Rua Pedro Álvares Cabral", "Rua Frei Caneca", "Rua da Aurora", "Rua do Sol",
        };

        /// <summary>O nome curto, como vai na placa da esquina ("R. Sete de Setembro", "Av. Brasil").</summary>
        public static string Abreviado(string nome) =>
            nome.StartsWith("Avenida ") ? "Av. " + nome.Substring(8) : nome.StartsWith("Rua ") ? "R. " + nome.Substring(4) : nome;

        void Linhas(Sorteio s)
        {
            float r = Cidade.Raio;
            switch (Cidade.Tipo)
            {
                case TipoDeLugar.CidadeGrande:
                    Us = Posicoes(s, 88f, 112f, 0.1f, r * 1.32f, r);
                    Vs = Posicoes(s, 70f, 76f, 0.06f, r * 1.32f, r);
                    break;
                case TipoDeLugar.CidadePequena:
                    Us = Posicoes(s, 85f, 105f, 0.1f, r * 1.32f, r);
                    Vs = Posicoes(s, 70f, 74f, 0.06f, r * 1.32f, r);
                    break;
                default:
                    // a vila: a rua principal (v = 0), as travessas a cada ~58 m e as paralelas, se couberem (com as casas
                    // do lado de fora delas)
                    Us = Posicoes(s, 58f, 58f, 0.08f, r * 1.3f, r);
                    float paralela = 64f * s.Entre(0.95f, 1.05f);
                    Vs = new[] { -2f * paralela, -paralela, 0f, paralela, 2f * paralela };
                    break;
            }
            IC = Array.IndexOf(Us, 0f);
            JC = Array.IndexOf(Vs, 0f);
            TipoU = new TipoDeRua[Us.Length];
            TipoV = new TipoDeRua[Vs.Length];
            for (int i = 0; i < Us.Length; i++) TipoU[i] = EhAvenida(i - IC) ? TipoDeRua.Avenida : TipoDeRua.Rua;
            for (int j = 0; j < Vs.Length; j++) TipoV[j] = EhAvenida(j - JC) ? TipoDeRua.Avenida : TipoDeRua.Rua;
            // os nomes: sem repetir na cidade (nas outras pode: toda cidade tem a sua Rua Sete de Setembro)
            var avenidas = new List<string>(Avenidas);
            var ruas = new List<string>(Ruas);
            string Nome(TipoDeRua t)
            {
                var lista = t == TipoDeRua.Avenida ? avenidas : ruas;
                if (lista.Count == 0) return t == TipoDeRua.Avenida ? $"Avenida {s.Entre(2, 99)}" : $"Rua {s.Entre(2, 999)}";
                int k = s.Entre(0, lista.Count);
                string n = lista[k];
                lista.RemoveAt(k);
                return n;
            }
            NomeU = new string[Us.Length];
            NomeV = new string[Vs.Length];
            for (int i = 0; i < Us.Length; i++) NomeU[i] = Nome(TipoU[i]);
            for (int j = 0; j < Vs.Length; j++) NomeV[j] = Nome(TipoV[j]);
        }

        bool EhAvenida(int k) => Cidade.Tipo switch
        {
            TipoDeLugar.CidadeGrande => k % 4 == 0,
            TipoDeLugar.CidadePequena => k == 0,
            _ => false,
        };

        // as posições das linhas, do meio para fora: o espaço do centro perto do meio e o de fora longe dele, com um
        // sorteio em volta
        static float[] Posicoes(Sorteio s, float noCentro, float fora, float variacao, float alcance, float raio)
        {
            var lista = new List<float> { 0f };
            foreach (float lado in new[] { 1f, -1f })
            {
                float x = 0f;
                while (true)
                {
                    float espaco = Mathf.Lerp(noCentro, fora, Geo.Suave(raio * 0.12f, raio * 0.42f, Mathf.Abs(x))) * s.Entre(1f - variacao, 1f + variacao);
                    x += lado * espaco;
                    if (Mathf.Abs(x) > alcance) break;
                    lista.Add(Mathf.Round(x * 10f) / 10f);
                }
            }
            lista.Sort();
            return lista.ToArray();
        }

        // ------------------------------------------------------------------ as ruas e os portões

        /// <summary>
        /// Os cruzamentos dentro do contorno, as ruas entre dois deles e os portões (as pontas das avenidas do meio; na vila,
        /// as da rua principal). Antes das estradas.
        /// </summary>
        public void TracarRuas()
        {
            int nu = Us.Length, nv = Vs.Length;
            Dentro = new bool[nu, nv];
            RuaU = new bool[nu, nv];
            RuaV = new bool[nu, nv];
            Abertos = new byte[nu, nv];
            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
                Dentro[i, j] = Q(Us[i], Vs[j]) < LimiteDasRuas;
            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                if (j + 1 < nv) RuaU[i, j] = Dentro[i, j] && Dentro[i, j + 1];
                if (i + 1 < nu) RuaV[i, j] = Dentro[i, j] && Dentro[i + 1, j];
            }
            var dirs = EhVila ? new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0) }
                              : new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            foreach (var dir in dirs) PorPortao(dir);

            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
                if (Bracos(i, j) != 0 || Abertos[i, j] != 0) Cruzamentos.Add(new Vector2Int(i, j));
        }

        void PorPortao(Vector2Int dir)
        {
            int i = IC, j = JC;
            while (true)
            {
                int ni = i + dir.x, nj = j + dir.y;
                if (ni < 0 || nj < 0 || ni >= Us.Length || nj >= Vs.Length) break;
                bool rua = dir.x != 0 ? RuaV[Math.Min(i, ni), j] : RuaU[i, Math.Min(j, nj)];
                if (!rua) break;
                i = ni;
                j = nj;
            }
            var dl = new Vector2(dir.x, dir.y);
            // a beira do cruzamento da ponta: a meia pista da rua que cruza a avenida ali
            float beira = dir.x != 0 ? AU(i) : AV(j);
            var ponto = new Vector2(Us[i], Vs[j]) + dl * beira;
            // até sair do contorno, mais 50 m
            float t = 0f;
            while (t < Cidade.Raio * 2f && Q(ponto.x + dl.x * t, ponto.y + dl.y * t) < (EhVila ? 1.5f : 1f)) t += 4f;
            t += 50f;
            var portao = new Portao { DirLocal = dl, I = i, J = j, Ponto = Mundo(ponto), Saida = Mundo(ponto + dl * t) };
            portao.Dir = (portao.Saida - Mundo(ponto + dl * (t - 6f))).normalized;
            portao.Prefixo = Amostrar(ponto, ponto + dl * t);
            portao.Conector = Amostrar(Vector2.zero, ponto);
            Abertos[i, j] |= dir.x > 0 ? MaisU : dir.x < 0 ? MenosU : dir.y > 0 ? MaisV : MenosV;
            Portoes.Add(portao);
        }

        // os pontos de a até b (locais), a cada ~12 m, no mapa
        List<Vector2> Amostrar(Vector2 a, Vector2 b)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 12f));
            var lista = new List<Vector2>(n + 1);
            for (int k = 0; k <= n; k++) lista.Add(Mundo(Vector2.Lerp(a, b, k / (float)n)));
            return lista;
        }

        /// <summary>O portão que melhor olha para o ponto (pela direção para fora), entre os que ainda servem.</summary>
        public Portao PortaoPara(Vector2 alvo, Portao menos = null)
        {
            Portao melhor = null;
            float maior = float.MinValue;
            var dir = (alvo - Centro).normalized;
            foreach (var p in Portoes)
            {
                if (p == menos) continue;
                float nota = Vector2.Dot(p.Dir, dir);
                if (nota > maior)
                {
                    maior = nota;
                    melhor = p;
                }
            }
            return melhor;
        }

        // ------------------------------------------------------------------ os lotes

        static readonly string[] CoresDasLojas = { "ParedeBege", "Concreto", "ConcretoClaro", "Tijolo", "ParedeVerde", "ParedeAzul", "ParedeBegeEscura" };
        static readonly string[] CoresDosPredios = { "Concreto", "ConcretoClaro", "Tijolo", "TijoloEscuro", "ParedeBege", "Concreto" };

        /// <summary>
        /// Os lotes de cada quarteirão (depois das estradas: nenhum fica no caminho delas). Em cada quarteirão, as fileiras
        /// de lotes de frente para as ruas dos lados compridos (num quarteirão inteiro, as duas fileiras dividem o fundo);
        /// na beira da cidade, só para as ruas que existem. O quarteirão do nordeste do meio é a praça (nas cidades), e
        /// alguns outros de casas também; os do terreno do laboratório ficam murados. Cada lote sorteia a construção pela
        /// zona.
        /// </summary>
        public void Lotear(PlanoDoMapa plano)
        {
            var s = Sorteio.Da(_d.Semente, "lotes-" + Indice);
            var corredores = Corredores(plano);
            var lab = plano.Laboratorio;
            int nu = Us.Length, nv = Vs.Length;
            // o quarteirão do laboratório mais perto do meio dele leva o prédio
            int labI = -1, labJ = -1;
            if (lab != null && Cidade.Q(lab.Centro.x, lab.Centro.y) < 1f)
            {
                var l = Local(lab.Centro);
                float menor = float.MaxValue;
                for (int i = 0; i + 1 < nu; i++)
                for (int j = 0; j + 1 < nv; j++)
                {
                    float dist = Vector2.Distance(new Vector2((Us[i] + Us[i + 1]) * 0.5f, (Vs[j] + Vs[j + 1]) * 0.5f), l);
                    if (dist < menor)
                    {
                        menor = dist;
                        labI = i;
                        labJ = j;
                    }
                }
            }
            for (int i = 0; i + 1 < nu; i++)
            for (int j = 0; j + 1 < nv; j++)
            {
                bool oeste = RuaU[i, j], leste = RuaU[i + 1, j], sul = RuaV[i, j], norte = RuaV[i, j + 1];
                if (!(oeste || leste || sul || norte)) continue;
                _celulaAtual = i * nv + j;
                float pu0 = Us[i] + AU(i) + SU(i), pu1 = Us[i + 1] - AU(i + 1) - SU(i + 1);
                float pv0 = Vs[j] + AV(j) + SV(j), pv1 = Vs[j + 1] - AV(j + 1) - SV(j + 1);
                if (pu1 - pu0 < 12f || pv1 - pv0 < 12f) continue;
                bool inteiro = oeste && leste && sul && norte;
                var meio = new Vector2((pu0 + pu1) * 0.5f, (pv0 + pv1) * 0.5f);
                var meioNoMapa = Mundo(meio);
                var zona = EhVila ? TipoDeBairro.Residencial : Cidade.ZonaEm(meioNoMapa.x, meioNoMapa.y);

                // o laboratório: os quarteirões dentro do raio dele, murados; o prédio no mais perto do meio
                if (lab != null && labI >= 0 && Vector2.Distance(meioNoMapa, lab.Centro) < lab.Raio)
                {
                    var l = Terreno(TipoDeLote.Laboratorio, zona, pu0, pu1, pv0, pv1, new Vector2(0f, -1f), s);
                    if (i == labI && j == labJ)
                    {
                        float mu = (pu1 - pu0) * 0.18f, mv = (pv1 - pv0) * 0.18f;
                        (l.BU0, l.BU1, l.BV0, l.BV1) = (pu0 + mu, pu1 - mu, pv0 + mv, pv1 - mv);
                        l.Andares = 5;
                        l.Altura = 4.5f + 4 * 3.6f + 1f;
                        l.Cor = "ConcretoClaro";
                    }
                    if (!NoCorredor(l, corredores)) Lotes.Add(l);
                    continue;
                }
                // as praças: a do meio da cidade e algumas nos bairros de casas
                bool praca = inteiro && !EhVila && ((i == IC && j == JC) ||
                             (zona == TipoDeBairro.Residencial && Sorteio.FracaoDoHash(Sorteio.Hash(_d.Semente, Indice * 1000 + i, j, 77)) < 0.035f));
                if (praca)
                {
                    var l = Terreno(TipoDeLote.Praca, zona, pu0, pu1, pv0, pv1, new Vector2(0f, -1f), s);
                    if (!NoCorredor(l, corredores)) Lotes.Add(l);
                    continue;
                }

                // as fileiras: de frente para as ruas dos lados compridos (ou dos que existem)
                int ladosV = (sul ? 1 : 0) + (norte ? 1 : 0), ladosU = (oeste ? 1 : 0) + (leste ? 1 : 0);
                bool compridoEmU = pu1 - pu0 >= pv1 - pv0;
                bool deFrenteParaV = EhVila ? ladosV > 0 : ladosV > 0 && ladosU > 0 ? (ladosV != ladosU ? ladosV > ladosU : compridoEmU) : ladosV > 0;
                if (deFrenteParaV)
                {
                    float largura = pv1 - pv0;
                    float fundo = sul && norte ? largura * 0.5f : Mathf.Min(largura, FundoMaximo);
                    fundo = Mathf.Min(fundo, FundoMaximo);
                    if (sul) Fileira(pu0, pu1, pv0, fundo, new Vector2(0f, -1f), s, corredores);
                    if (norte) Fileira(pu0, pu1, pv1, fundo, new Vector2(0f, 1f), s, corredores);
                }
                else
                {
                    float largura = pu1 - pu0;
                    float fundo = oeste && leste ? largura * 0.5f : Mathf.Min(largura, FundoMaximo);
                    fundo = Mathf.Min(fundo, FundoMaximo);
                    if (oeste) Fileira(pv0, pv1, pu0, fundo, new Vector2(-1f, 0f), s, corredores);
                    if (leste) Fileira(pv0, pv1, pu1, fundo, new Vector2(1f, 0f), s, corredores);
                }
            }
            Especiais();
        }

        /// <summary>
        /// As construções especiais da cidade (Parte M6, terceira fatia), depois dos lotes: o tipo de cada loja do comércio
        /// (as cotas de mercados, farmácias, lojas 24 horas e a loja de armas, sorteadas entre os lotes; o mercado só nos
        /// largos e fundos) e a planta dela; na cidade grande, o hospital no lugar de um prédio largo do centro.
        /// </summary>
        void Especiais()
        {
            var s = Sorteio.Da(_d.Semente, "lojas-" + Indice);
            var lojas = Lotes.FindAll(l => l.Tipo == TipoDeLote.Loja && l.TemConstrucao);
            for (int i = lojas.Count - 1; i > 0; i--)
            {
                int j = s.Entre(0, i + 1);
                (lojas[i], lojas[j]) = (lojas[j], lojas[i]);
            }
            bool grande = Cidade.Tipo == TipoDeLugar.CidadeGrande;
            int mercados = grande ? 3 : 1, farmacias = grande ? 4 : 2, vinteQuatro = grande ? 4 : 1, armas = grande ? 1 : 0;
            var tipos = new TipoDeLoja[lojas.Count];
            var feito = new bool[lojas.Count];
            // o mercado primeiro, nos lotes largos e fundos
            for (int i = 0; i < lojas.Count && mercados > 0; i++)
                if (FrenteDaConstrucao(lojas[i]) >= 16f && FundoDaConstrucao(lojas[i]) >= 15f)
                {
                    tipos[i] = TipoDeLoja.Mercado;
                    feito[i] = true;
                    mercados--;
                }
            for (int i = 0; i < lojas.Count; i++)
            {
                if (feito[i]) continue;
                if (armas > 0) { tipos[i] = TipoDeLoja.Armas; armas--; }
                else if (farmacias > 0) { tipos[i] = TipoDeLoja.Farmacia; farmacias--; }
                else if (vinteQuatro > 0) { tipos[i] = TipoDeLoja.VinteQuatroHoras; vinteQuatro--; }
                else tipos[i] = TipoDeLoja.Comum;
            }
            for (int i = 0; i < lojas.Count; i++) PorLoja(lojas[i], tipos[i]);
            if (grande) Hospital(s);
        }

        static float FrenteDaConstrucao(Lote l) => l.Frente.y != 0f ? l.BU1 - l.BU0 : l.BV1 - l.BV0;
        static float FundoDaConstrucao(Lote l) => l.Frente.y != 0f ? l.BV1 - l.BV0 : l.BU1 - l.BU0;

        /// <summary>
        /// A loja de verdade do lote (Parte M6, terceira fatia): a planta do tipo, do tamanho da construção (colada na
        /// calçada e nos vizinhos), de frente para a rua. A caixa da construção (a de longe) fica no corpo da loja, do piso
        /// até o topo da platibanda.
        /// </summary>
        void PorLoja(Lote l, TipoDeLoja tipo)
        {
            bool aoLongoDeU = l.Frente.y != 0f;
            var p = PlantasDeLoja.ParaOLote(new System.Random((int)(l.Semente & 0x7fffffff)), Medida, tipo, FrenteDaConstrucao(l), FundoDaConstrucao(l), l.Andares, l.Cor);
            l.Loja = p;
            var frente = aoLongoDeU
                ? new Vector2((l.BU0 + l.BU1) * 0.5f, l.Frente.y < 0f ? l.BV0 : l.BV1)
                : new Vector2(l.Frente.x < 0f ? l.BU0 : l.BU1, (l.BV0 + l.BV1) * 0.5f);
            l.MeioDaCasa = frente + l.FundoDaCasa * (p.D * 0.5f);
            var corpo = NoLocal(l, p.X0, p.X1, p.Z0, p.Z1);
            (l.BU0, l.BU1, l.BV0, l.BV1) = (corpo.xMin, corpo.xMax, corpo.yMin, corpo.yMax);
            l.RetDaCasa = corpo;
            l.Andares = p.andares;
            l.Altura = p.AlturaDaCaixa;
        }

        /// <summary>
        /// O hospital da cidade grande (Parte M6, terceira fatia): no lugar do prédio de verdade que tem lugar para ele (ao
        /// menos 24 × 18 m) mais perto da beira do centro (a 70% do raio dele), com o mesmo tamanho; de 4 a 6 andares.
        /// </summary>
        void Hospital(Sorteio s)
        {
            Lote melhor = null;
            float menor = float.MaxValue, alvo = Cidade.Raio * Cidade.FimDoCentro * 0.7f;
            foreach (var l in Lotes)
            {
                if (l.Predio == null || l.Predio.W < 24f || l.Predio.D < 18f) continue;
                float dist = Mathf.Abs(Vector2.Distance(Mundo(l.MeioDaCasa), Centro) - alvo);
                if (dist < menor)
                {
                    menor = dist;
                    melhor = l;
                }
            }
            if (melhor == null) return;
            var antes = melhor.Predio;
            var p = PlantasDePredio.ParaHospital(new System.Random((int)(melhor.Semente & 0x7fffffff) ^ 0x4851), Medida, antes.W, antes.D, s.Entre(4, 7));
            melhor.Predio = p;
            melhor.Andares = p.andares;
            melhor.Altura = p.AlturaDaCaixa;
            melhor.Cor = p.cor;
        }

        // uma fileira de lotes ao longo de "de" até "ate" (no eixo da rua), com a frente na linha "frente" (no outro eixo),
        // olhando para "frenteDir"
        void Fileira(float de, float ate, float frente, float fundo, Vector2 frenteDir, Sorteio s, List<(Vector2 a, Vector2 b, float meia)> corredores)
        {
            bool aoLongoDeU = frenteDir.y != 0f;
            float x = de;
            while (ate - x > 8f)
            {
                // a zona pelo meio do próximo lote
                var proximo = aoLongoDeU ? new Vector2(x + 10f, frente - frenteDir.y * fundo * 0.5f) : new Vector2(frente - frenteDir.x * fundo * 0.5f, x + 10f);
                var noMapa = Mundo(proximo);
                if (Cidade.Q(noMapa.x, noMapa.y) > (EhVila ? 1.5f : 1.25f))
                {
                    x += 10f;
                    continue;
                }
                var zona = EhVila ? TipoDeBairro.Residencial : Cidade.ZonaEm(noMapa.x, noMapa.y);
                if (zona == TipoDeBairro.Residencial)
                {
                    x = CasaNaFileira(x, ate, frente, fundo, frenteDir, s, corredores);
                    continue;
                }
                (float min, float max) = zona == TipoDeBairro.Centro ? (24f, 40f) : (11f, 24f);
                float largura = s.Entre(min, max);
                if (ate - x - largura < min) largura = ate - x;
                float a = x, b = x + largura;
                x = b;
                float f0 = frente, f1 = frente - (aoLongoDeU ? frenteDir.y : frenteDir.x) * fundo;
                var l = aoLongoDeU
                    ? Terreno(TipoDeLote.Casa, zona, a, b, Mathf.Min(f0, f1), Mathf.Max(f0, f1), frenteDir, s)
                    : Terreno(TipoDeLote.Casa, zona, Mathf.Min(f0, f1), Mathf.Max(f0, f1), a, b, frenteDir, s);
                Construcao(l, s);
                if (!NoCorredor(l, corredores)) Lotes.Add(l);
            }
        }

        int _celulaAtual;

        /// <summary>A escala das construções do jogo (Parte M0).</summary>
        static readonly Medidas Medida = Medidas.DoJogo;

        /// <summary>
        /// Um lote de casa na fileira (Parte M6), do tamanho da casa sorteada para ele (a planta que cabe no fundo da
        /// fileira, com a garagem ou a vaga), mais um vão de cada lado; o resto da fileira, se for pouco para outra casa, fica
        /// com ele. Uns lotes ficam baldios. Devolve onde ele termina.
        /// </summary>
        float CasaNaFileira(float x, float ate, float frente, float fundo, Vector2 frenteDir, Sorteio s, List<(Vector2 a, Vector2 b, float meia)> corredores)
        {
            bool aoLongoDeU = frenteDir.y != 0f;
            float f0 = frente, f1 = frente - (aoLongoDeU ? frenteDir.y : frenteDir.x) * fundo;
            Lote Novo(float a, float b) => aoLongoDeU
                ? Terreno(TipoDeLote.Casa, TipoDeBairro.Residencial, a, b, Mathf.Min(f0, f1), Mathf.Max(f0, f1), frenteDir, s)
                : Terreno(TipoDeLote.Casa, TipoDeBairro.Residencial, Mathf.Min(f0, f1), Mathf.Max(f0, f1), a, b, frenteDir, s);
            // o fim do lote ao longo da rua (e da construção dele, que ainda é o lote inteiro)
            void Ate(Lote l, float b)
            {
                if (aoLongoDeU) l.U1 = l.BU1 = b;
                else l.V1 = l.BV1 = b;
            }
            const float MenorCasa = 17f;
            var lote = Novo(x, ate);
            if (s.Fracao() < 0.05f)
            {
                float largura = Mathf.Min(s.Entre(14f, 20f), ate - x);
                if (ate - x - largura < MenorCasa) largura = ate - x;
                Ate(lote, x + largura);
                lote.Tipo = TipoDeLote.Baldio;
                if (!NoCorredor(lote, corredores)) Lotes.Add(lote);
                return x + largura;
            }
            // o vão de cada lado (o recuo lateral): com menos de 1,7 m, as células de 2 m do terreno em volta de uma casa
            // pegavam a altura da vizinha, e o chão subia ou descia junto da parede
            float vao = s.Entre(1.7f, 2.4f), recuo = s.Entre(3f, 5f);
            var planta = PlantasDeCasa.ParaOLote(new System.Random((int)(lote.Semente & 0x7fffffff)), Medida, ate - x - 2f * vao, fundo - recuo - 2.5f);
            if (planta == null) return ate;   // nem a casa mais estreita cabe: o resto da fileira fica vazio
            var lim = Casa.Limites(planta, Medida);
            float larg = lim.y - lim.x + 2f * vao;
            if (ate - x - larg < MenorCasa) larg = ate - x;
            Ate(lote, x + larg);
            PorCasa(lote, planta, recuo, vao);
            if (!NoCorredor(lote, corredores)) Lotes.Add(lote);
            return x + larg;
        }

        /// <summary>
        /// Põe a casa da planta no lote (Parte M6): de frente para a rua, com a frente dos degraus a 'recuo' m da beira da
        /// frente do lote (a calçada) e o lado dela a 'vao' m do início do lote ao longo da rua. Guarda o meio, o retângulo
        /// dela, o do patamar e a altura do chão dela, e acerta a caixa da construção (a de longe) no corpo da casa.
        /// </summary>
        public void PorCasa(Lote l, Planta planta, float recuo, float vao)
        {
            var lim = Casa.Limites(planta, Medida);
            var prof = Casa.Profundidade(planta, Medida);
            Vector2 X = l.LadoDaCasa, Z = l.FundoDaCasa;
            bool aoLongoDeU = l.Frente.y != 0f;
            float sx = aoLongoDeU ? X.x : X.y, sz = aoLongoDeU ? Z.y : Z.x;
            float ao = (aoLongoDeU ? l.U0 : l.V0) + vao - Mathf.Min(sx * lim.x, sx * lim.y);
            float beira = aoLongoDeU ? (l.Frente.y < 0f ? l.V0 : l.V1) : (l.Frente.x < 0f ? l.U0 : l.U1);
            float noFundo = beira + sz * (recuo - prof.x);
            l.Casa = planta;
            l.MeioDaCasa = aoLongoDeU ? new Vector2(ao, noFundo) : new Vector2(noFundo, ao);
            // a casa (com porão, também a calçada em volta dos buracos) e o patamar, 2 m além dela
            l.RetDaCasa = NoLocal(l, lim.x, lim.y, prof.x, prof.y);
            l.RetDoPatamar = Rect.MinMaxRect(l.RetDaCasa.xMin - 2f, l.RetDaCasa.yMin - 2f, l.RetDaCasa.xMax + 2f, l.RetDaCasa.yMax + 2f);
            var corpo = NoLocal(l, planta.X0, planta.X1, planta.Z0, planta.Z1);
            (l.BU0, l.BU1, l.BV0, l.BV1) = (corpo.xMin, corpo.xMax, corpo.yMin, corpo.yMax);
            var meio = Mundo(l.MeioDaCasa);
            l.ChaoDaCasa = GeradorDoMundo.AlturaDoChao(_d, meio.x, meio.y);
            l.Tipo = l.Tipo == TipoDeLote.Familia ? TipoDeLote.Familia : TipoDeLote.Casa;
            l.Andares = planta.andares;
            // a caixa de longe: do piso até o forro (MalhasDaCidade soma o piso da casa)
            l.Altura = planta.andares * (Medida.altura + Medida.laje) - Medida.laje + Medida.forro;
            l.Cor = planta.cor;
            l.CorDoTelhado = planta.telhado;
        }

        // o retângulo (no plano local) de um retângulo da casa (xa a xb, za a zb, nas coordenadas dela)
        static Rect NoLocal(Lote l, float xa, float xb, float za, float zb)
        {
            Vector2 a = l.NaCasa(xa, za), b = l.NaCasa(xb, zb);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>
        /// O giro da casa do lote no mapa (graus, em y): o eixo z dela (para o fundo) no ponto do meio dela, com a deformação
        /// do plano local. A casa é rígida: perto do meio dela, a deformação é só um giro.
        /// </summary>
        public float GiroDaCasa(Lote l)
        {
            var dir = Mundo(l.MeioDaCasa + l.FundoDaCasa) - Mundo(l.MeioDaCasa);
            return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        }

        /// <summary>Onde fica a casa do lote no mapa: o meio dela, na altura do chão dela, e o giro (para a matriz da Unity).</summary>
        public Matrix4x4 MatrizDaCasa(Lote l)
        {
            var meio = Mundo(l.MeioDaCasa);
            return Matrix4x4.TRS(new Vector3(meio.x, l.ChaoDaCasa, meio.y), Quaternion.Euler(0f, GiroDaCasa(l), 0f), Vector3.one);
        }

        Lote Terreno(TipoDeLote tipo, TipoDeBairro zona, float u0, float u1, float v0, float v1, Vector2 frente, Sorteio s) => new Lote
        {
            Tipo = tipo, Zona = zona, U0 = u0, U1 = u1, V0 = v0, V1 = v1, Frente = frente, BU0 = u0, BU1 = u1, BV0 = v0, BV1 = v1,
            Semente = (uint)(s.Proximo() & 0xffffffff), Celula = _celulaAtual,
        };

        // a construção do lote, pela zona: a loja colada na calçada e o prédio quase encostando nos vizinhos; uns
        // estacionamentos (as casas são da CasaNaFileira)
        void Construcao(Lote l, Sorteio s)
        {
            float vazio = s.Fracao();
            float frenteDoLote = l.Frente.y != 0f ? l.U1 - l.U0 : l.V1 - l.V0;
            float fundoDoLote = l.Frente.y != 0f ? l.V1 - l.V0 : l.U1 - l.U0;
            float recuo, lado, fundo;
            switch (l.Zona)
            {
                case TipoDeBairro.Comercio:
                    if (vazio < 0.07f) { l.Tipo = TipoDeLote.Estacionamento; return; }
                    l.Tipo = TipoDeLote.Loja;
                    recuo = 0f;
                    lado = 0.15f;
                    fundo = Mathf.Min(s.Entre(14f, 26f), fundoDoLote - 2f);
                    bool centroDaPequena = Cidade.Tipo == TipoDeLugar.CidadePequena;
                    float sorteio = s.Fracao();
                    l.Andares = centroDaPequena ? (sorteio < 0.35f ? 2 : sorteio < 0.75f ? 3 : 4) : (sorteio < 0.4f ? 1 : sorteio < 0.8f ? 2 : 3);
                    l.Altura = 4.5f + (l.Andares - 1) * 3.4f + 0.6f;
                    l.Cor = CoresDasLojas[s.Entre(0, CoresDasLojas.Length)];
                    l.CorDoTelhado = "Concreto";
                    break;
                default:
                    if (vazio < 0.05f) { l.Tipo = TipoDeLote.Estacionamento; return; }
                    l.Tipo = TipoDeLote.Predio;
                    recuo = s.Entre(0f, 3f);
                    lado = s.Entre(1f, 2.5f);
                    fundo = fundoDoLote - recuo - s.Entre(1f, 3f);
                    // mais altos perto do meio: de 15 andares no meio a 6 na beira do centro
                    var noMapa = Mundo(l.Meio);
                    float r = Vector2.Distance(noMapa, Centro) / Mathf.Max(1f, Cidade.Raio * Cidade.FimDoCentro);
                    l.Andares = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(15f, 6f, r) + s.Entre(-2.5f, 2.5f)), 5, 15);
                    l.Altura = 4.5f + (l.Andares - 1) * 3.6f + 1f;
                    l.Cor = CoresDosPredios[s.Entre(0, CoresDosPredios.Length)];
                    l.CorDoTelhado = "Concreto";
                    break;
            }
            // (o prédio de verdade precisa de 9 m de frente e 15 de fundo, com o núcleo e um apartamento; a loja, de 8 × 10 m)
            bool predio = l.Tipo == TipoDeLote.Predio;
            if (fundo < (predio ? 15f : 10f) || frenteDoLote - 2f * lado < (predio ? 9f : 8f))
            {
                l.Tipo = TipoDeLote.Baldio;
                l.Andares = 0;
                return;
            }
            // o retângulo da construção: recuada da frente, afastada dos lados
            if (l.Frente.y != 0f)
            {
                l.BU0 = l.U0 + lado;
                l.BU1 = l.U1 - lado;
                if (l.Frente.y < 0f) { l.BV0 = l.V0 + recuo; l.BV1 = l.BV0 + fundo; }
                else { l.BV1 = l.V1 - recuo; l.BV0 = l.BV1 - fundo; }
            }
            else
            {
                l.BV0 = l.V0 + lado;
                l.BV1 = l.V1 - lado;
                if (l.Frente.x < 0f) { l.BU0 = l.U0 + recuo; l.BU1 = l.BU0 + fundo; }
                else { l.BU1 = l.U1 - recuo; l.BU0 = l.BU1 - fundo; }
            }
            if (predio) PorPredio(l);
        }

        /// <summary>
        /// O prédio de verdade do lote (Parte M6, segunda fatia): a planta que cabe na construção (até 44 m de frente e 24 m
        /// de fundo), na frente dela (recuada como ela) e no meio ao longo da rua, de frente para a rua. A caixa da
        /// construção (a de longe) fica no corpo do prédio, da altura dele até o topo da platibanda.
        /// </summary>
        void PorPredio(Lote l)
        {
            bool aoLongoDeU = l.Frente.y != 0f;
            float largura = aoLongoDeU ? l.BU1 - l.BU0 : l.BV1 - l.BV0, fundo = aoLongoDeU ? l.BV1 - l.BV0 : l.BU1 - l.BU0;
            var p = PlantasDePredio.ParaOLote(new System.Random((int)(l.Semente & 0x7fffffff)), Medida, largura, fundo, l.Andares, l.Cor);
            l.Predio = p;
            var frente = aoLongoDeU
                ? new Vector2((l.BU0 + l.BU1) * 0.5f, l.Frente.y < 0f ? l.BV0 : l.BV1)
                : new Vector2(l.Frente.x < 0f ? l.BU0 : l.BU1, (l.BV0 + l.BV1) * 0.5f);
            l.MeioDaCasa = frente + l.FundoDaCasa * (p.D * 0.5f);
            var corpo = NoLocal(l, p.X0, p.X1, p.Z0, p.Z1);
            (l.BU0, l.BU1, l.BV0, l.BV1) = (corpo.xMin, corpo.xMax, corpo.yMin, corpo.yMax);
            l.RetDaCasa = corpo;
            l.Andares = p.andares;
            l.Altura = p.AlturaDaCaixa;
        }

        // os trechos das estradas (fora os caminhos por dentro das cidades) que passam perto da cidade, com a meia largura
        // de cada um mais o acostamento
        List<(Vector2 a, Vector2 b, float meia)> Corredores(PlanoDoMapa plano)
        {
            var lista = new List<(Vector2, Vector2, float)>();
            float alcance = Cidade.Raio * 1.5f + 60f;
            foreach (var e in plano.Estradas)
            {
                if (e.Conector) continue;
                float meia = RedeDeEstradas.MeiaLargura(e.Tipo) + RedeDeEstradas.Acostamento + 1.5f;
                for (int i = 0; i + 1 < e.Pontos.Count; i++)
                {
                    Vector2 a = e.Pontos[i], b = e.Pontos[i + 1];
                    if (Mathf.Abs(a.x - Centro.x) > alcance && Mathf.Abs(b.x - Centro.x) > alcance) continue;
                    if (Mathf.Abs(a.y - Centro.y) > alcance && Mathf.Abs(b.y - Centro.y) > alcance) continue;
                    lista.Add((a, b, meia));
                }
            }
            // e os postos (o pátio deles)
            foreach (var l in plano.Lugares)
                if (l.Tipo == TipoDeLugar.Posto && Vector2.Distance(l.Centro, Centro) < alcance + 40f)
                    lista.Add((l.Centro, l.Centro, l.Raio + 8f));
            return lista;
        }

        // o lote encosta numa estrada (os cantos, o meio das bordas e o meio)
        bool NoCorredor(Lote l, List<(Vector2 a, Vector2 b, float meia)> corredores)
        {
            if (corredores.Count == 0) return false;
            for (int k = 0; k < 9; k++)
            {
                float fu = (k % 3) * 0.5f, fv = (k / 3) * 0.5f;
                var p = Mundo(Mathf.Lerp(l.U0, l.U1, fu), Mathf.Lerp(l.V0, l.V1, fv));
                foreach (var (a, b, meia) in corredores)
                    if (RedeDeEstradas.DistanciaAoSegmento(p, a, b, out _) < meia) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ os blocos de 64 m

        /// <summary>Corta as ruas em pedaços e guarda, por bloco de 64 m, os pedaços, os cruzamentos e os lotes (pelo meio de cada um).</summary>
        public void Indexar()
        {
            Pecas.Clear();
            _porBloco.Clear();
            int nu = Us.Length, nv = Vs.Length;
            void Cortar(bool daLinhaU, int linha, float de, float ate)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt((ate - de) / Pedaco));
                for (int k = 0; k < n; k++)
                {
                    var peca = new Peca { DaLinhaU = daLinhaU, Linha = linha, De = Mathf.Lerp(de, ate, k / (float)n), Ate = Mathf.Lerp(de, ate, (k + 1) / (float)n) };
                    float meio = (peca.De + peca.Ate) * 0.5f;
                    var p = daLinhaU ? Mundo(Us[linha], meio) : Mundo(meio, Vs[linha]);
                    Do(p).Pecas.Add(Pecas.Count);
                    Pecas.Add(peca);
                }
            }
            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                if (j + 1 < nv && RuaU[i, j]) Cortar(true, i, Vs[j] + AV(j), Vs[j + 1] - AV(j + 1));
                if (i + 1 < nu && RuaV[i, j]) Cortar(false, j, Us[i] + AU(i), Us[i + 1] - AU(i + 1));
            }
            for (int k = 0; k < Cruzamentos.Count; k++)
            {
                var c = Cruzamentos[k];
                Do(Mundo(Us[c.x], Vs[c.y])).Cruzamentos.Add(k);
            }
            _lotesDaCelula = new List<int>[nu * nv];
            for (int k = 0; k < Lotes.Count; k++)
            {
                Do(Mundo(Lotes[k].Meio)).Lotes.Add(k);
                (_lotesDaCelula[Lotes[k].Celula] ??= new List<int>()).Add(k);
            }
        }

        // os lotes de cada quarteirão (cada lote fica dentro do quarteirão dele)
        List<int>[] _lotesDaCelula;

        /// <summary>Os lotes do quarteirão que contém o ponto local (null = nenhum).</summary>
        List<int> LotesEm(Vector2 local)
        {
            if (_lotesDaCelula == null) return null;
            int i = Faixa(Us, local.x), j = Faixa(Vs, local.y);
            return i < 0 || j < 0 ? null : _lotesDaCelula[i * Vs.Length + j];
        }

        DoBloco Do(Vector2 p)
        {
            var b = _d.BlocoDe(p.x, p.y);
            int chave = b.y * _d.BlocosPorLado + b.x;
            if (!_porBloco.TryGetValue(chave, out var lista)) _porBloco[chave] = lista = new DoBloco();
            return lista;
        }

        /// <summary>O que o bloco (bx, bz) tem desta cidade (null = nada).</summary>
        public DoBloco Bloco(int bx, int bz) => _porBloco.TryGetValue(bz * _d.BlocosPorLado + bx, out var l) ? l : null;

        static int LinhaMaisPerto(float[] linhas, float x)
        {
            int melhor = 0;
            for (int k = 1; k < linhas.Length; k++)
                if (Mathf.Abs(linhas[k] - x) < Mathf.Abs(linhas[melhor] - x)) melhor = k;
            return melhor;
        }

        static int Faixa(float[] linhas, float x)
        {
            for (int k = 0; k + 1 < linhas.Length; k++)
                if (x >= linhas[k] && x <= linhas[k + 1]) return k;
            return -1;
        }

        /// <summary>O que o mapa pintado mostra num ponto: nada da cidade, o asfalto, a calçada, uma construção, a praça ou o estacionamento.</summary>
        public enum NoMapa : byte { Nada, Asfalto, Calcada, Construcao, Praca, Estacionamento }

        /// <summary>O que há no ponto do mapa (para o mapa pintado).</summary>
        public NoMapa OQueTem(Vector2 p)
        {
            var l = Local(p);
            int i = LinhaMaisPerto(Us, l.x);
            float du = Mathf.Abs(l.x - Us[i]);
            if (du < AU(i) + SU(i))
            {
                int j = Faixa(Vs, l.y);
                int jj = LinhaMaisPerto(Vs, l.y);
                bool cruzamento = Mathf.Abs(l.y - Vs[jj]) < AV(jj) + SV(jj) && (Bracos(i, jj) != 0 || Abertos[i, jj] != 0);
                if ((j >= 0 && RuaU[i, j]) || cruzamento) return du < AU(i) ? NoMapa.Asfalto : NoMapa.Calcada;
            }
            int jv = LinhaMaisPerto(Vs, l.y);
            float dv = Mathf.Abs(l.y - Vs[jv]);
            if (dv < AV(jv) + SV(jv))
            {
                int ii = Faixa(Us, l.x);
                if (ii >= 0 && RuaV[ii, jv]) return dv < AV(jv) ? NoMapa.Asfalto : NoMapa.Calcada;
            }
            var lotes = LotesEm(l);
            if (lotes != null)
                foreach (int k in lotes)
                {
                    var lote = Lotes[k];
                    if (l.x < lote.U0 || l.x > lote.U1 || l.y < lote.V0 || l.y > lote.V1) continue;
                    if (lote.Tipo == TipoDeLote.Praca) return NoMapa.Praca;
                    if (lote.Tipo == TipoDeLote.Estacionamento) return NoMapa.Estacionamento;
                    if (lote.TemConstrucao && l.x >= lote.BU0 && l.x <= lote.BU1 && l.y >= lote.BV0 && l.y <= lote.BV1) return NoMapa.Construcao;
                }
            return NoMapa.Nada;
        }

        /// <summary>O lote que contém o ponto do mapa, ou null (fora dos quarteirões).</summary>
        public Lote LoteEm(Vector2 p)
        {
            var l = Local(p);
            var lotes = LotesEm(l);
            if (lotes == null) return null;
            foreach (int k in lotes)
            {
                var lote = Lotes[k];
                if (l.x >= lote.U0 && l.x <= lote.U1 && l.y >= lote.V0 && l.y <= lote.V1) return lote;
            }
            return null;
        }

        /// <summary>
        /// O ponto do mapa cai numa rua ou calçada da cidade (com os cruzamentos), numa construção ou num lote sem casa que
        /// não é chão livre (a praça, o estacionamento, o lote da família, o laboratório), com a folga (m).
        /// </summary>
        public bool Ocupado(Vector2 p, float folga = 0.5f)
        {
            var l = Local(p);
            // as ruas das linhas u (correm ao longo de v) e os cruzamentos delas
            int i = LinhaMaisPerto(Us, l.x);
            if (Mathf.Abs(l.x - Us[i]) < AU(i) + SU(i) + folga)
            {
                int j = Faixa(Vs, l.y);
                if (j >= 0 && RuaU[i, j]) return true;
                int jj = LinhaMaisPerto(Vs, l.y);
                if (Mathf.Abs(l.y - Vs[jj]) < AV(jj) + SV(jj) + folga && (Bracos(i, jj) != 0 || Abertos[i, jj] != 0)) return true;
            }
            int jv = LinhaMaisPerto(Vs, l.y);
            if (Mathf.Abs(l.y - Vs[jv]) < AV(jv) + SV(jv) + folga)
            {
                int ii = Faixa(Us, l.x);
                if (ii >= 0 && RuaV[ii, jv]) return true;
            }
            // as construções e os lotes que não são chão livre (com a folga, um ponto na beira do quarteirão vizinho
            // fica de fora; a calçada no meio dá mais folga que isso)
            var lotes = LotesEm(l);
            if (lotes != null)
                foreach (int k in lotes)
                {
                    var lote = Lotes[k];
                    bool todo = lote.Tipo == TipoDeLote.Praca || lote.Tipo == TipoDeLote.Estacionamento || lote.Tipo == TipoDeLote.Familia || lote.Tipo == TipoDeLote.Laboratorio;
                    if (todo && l.x > lote.U0 - folga && l.x < lote.U1 + folga && l.y > lote.V0 - folga && l.y < lote.V1 + folga) return true;
                    if (lote.TemConstrucao && l.x > lote.BU0 - folga && l.x < lote.BU1 + folga && l.y > lote.BV0 - folga && l.y < lote.BV1 + folga) return true;
                }
            return false;
        }

        // ------------------------------------------------------------------ o chão

        /// <summary>
        /// O chão aplainado da cidade (Plano_Mapa, "As cidades num chão quase plano"): o relevo numa grade de 16 m, borrado
        /// (uns 64 m) e com a subida limitada a 4,5% (a diferença para a média encolhe), lido com a interpolação de
        /// Catmull-Rom (sem quinas). Do contorno (q = 1) para fora, volta ao relevo até <see cref="FimDoChao"/>.
        /// </summary>
        public void MedirOChao()
        {
            float meia = Alcance + 48f;
            _nChao = Mathf.CeilToInt(2f * meia / PassoDoChao) + 1;
            _origemChao = Centro - Vector2.one * meia;
            int n = _nChao;
            var h = new float[n * n];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                h[j * n + i] = GeradorDoMundo.AlturaComLago(_d, _origemChao.x + i * PassoDoChao, _origemChao.y + j * PassoDoChao);
            // o borrão (gaussiano, separável)
            const int raio = 8;
            var pesos = new float[raio * 2 + 1];
            float soma = 0f;
            for (int k = -raio; k <= raio; k++) soma += pesos[k + raio] = Mathf.Exp(-k * k / (2f * 4f * 4f));
            for (int k = 0; k < pesos.Length; k++) pesos[k] /= soma;
            var aux = new float[n * n];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float v = 0f;
                for (int k = -raio; k <= raio; k++) v += h[j * n + Mathf.Clamp(i + k, 0, n - 1)] * pesos[k + raio];
                aux[j * n + i] = v;
            }
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float v = 0f;
                for (int k = -raio; k <= raio; k++) v += aux[Mathf.Clamp(j + k, 0, n - 1) * n + i] * pesos[k + raio];
                h[j * n + i] = v;
            }
            // a subida: a média e a maior inclinação dentro do contorno (com folga); se passar de 4,5%, tudo encolhe
            double media = 0;
            int dentro = 0;
            float inclinacao = 0f;
            for (int j = 0; j + 1 < n; j++)
            for (int i = 0; i + 1 < n; i++)
            {
                float x = _origemChao.x + i * PassoDoChao, z = _origemChao.y + j * PassoDoChao;
                if (Cidade.Q(x, z) > 1.15f) continue;
                media += h[j * n + i];
                dentro++;
                inclinacao = Mathf.Max(inclinacao, Mathf.Abs(h[j * n + i + 1] - h[j * n + i]) / PassoDoChao, Mathf.Abs(h[(j + 1) * n + i] - h[j * n + i]) / PassoDoChao);
            }
            float m = dentro > 0 ? (float)(media / dentro) : h[(n / 2) * n + n / 2];
            float fator = inclinacao > 0.045f ? 0.045f / inclinacao : 1f;
            for (int k = 0; k < h.Length; k++) h[k] = m + (h[k] - m) * fator;
            _chao = h;
        }

        /// <summary>Até onde (q) o chão aplainado vai voltando ao relevo.</summary>
        public float FimDoChao => EhVila ? 1.55f : 1.35f;

        /// <summary>Até onde (m do meio, em x ou z) a cidade mexe no chão: o fim do chão aplainado no contorno mais largo (+14%).</summary>
        public float Alcance => Cidade.Raio * FimDoChao * 1.17f;

        /// <summary>O quanto o chão da cidade manda no ponto (1 dentro do contorno, 0 a partir de <see cref="FimDoChao"/>).</summary>
        public float PesoDoChao(float x, float z)
        {
            if (Mathf.Abs(x - Centro.x) > Alcance || Mathf.Abs(z - Centro.y) > Alcance) return 0f;
            return 1f - Geo.Suave(1f, FimDoChao, Cidade.Q(x, z));
        }

        /// <summary>
        /// O chão das casas (Parte M6): no patamar de cada casa (<see cref="Lote.RetDoPatamar"/>, 2 m além dela), o chão fica
        /// na altura dela, e de lá volta à altura h em 2,5 m, sem chegar na calçada (some no último 1,2 m antes da beira do
        /// quarteirão; só o patamar de uma casa de esquina passa um pouco). Vale a casa mais perto (pelo retângulo dela, que
        /// nunca encosta no de outra). Entre duas casas vizinhas, de alturas diferentes, fica um degrauzinho de terra (a rua
        /// sobe no máximo 4,5%: uns 0,5 m de uma casa para a outra).
        /// </summary>
        public float NoPatamar(Vector2 p, float h)
        {
            if (_lotesDaCelula == null) return h;
            var l = Local(p);
            int i = Faixa(Us, l.x), j = Faixa(Vs, l.y);
            if (i < 0 || j < 0) return h;
            var lotes = _lotesDaCelula[i * Vs.Length + j];
            if (lotes == null) return h;
            float pu0 = Us[i] + AU(i) + SU(i), pu1 = Us[i + 1] - AU(i + 1) - SU(i + 1);
            float pv0 = Vs[j] + AV(j) + SV(j), pv1 = Vs[j + 1] - AV(j + 1) - SV(j + 1);
            float dentro = Mathf.Min(Mathf.Min(l.x - pu0, pu1 - l.x), Mathf.Min(l.y - pv0, pv1 - l.y));
            if (dentro <= -2f) return h;
            Lote perto = null;
            float menor = float.MaxValue;
            foreach (int k in lotes)
            {
                var lote = Lotes[k];
                if (lote.Casa == null) continue;
                float dist = Distancia(lote.RetDaCasa, l);
                if (dist < menor)
                {
                    menor = dist;
                    perto = lote;
                }
            }
            if (perto == null) return h;
            float fora = Distancia(perto.RetDoPatamar, l);
            if (fora >= 2.5f || (fora > 0f && dentro <= 0f)) return h;
            // no patamar, sempre a altura da casa; na volta, sem chegar na calçada
            float peso = fora <= 0f ? 1f : (1f - Geo.Suave(0f, 2.5f, fora)) * Geo.Suave(0f, 1.2f, dentro);
            return Mathf.Lerp(h, perto.ChaoDaCasa, peso);
        }

        // a distância de um ponto ao retângulo (0 dentro)
        static float Distancia(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(0f, Mathf.Max(r.xMin - p.x, p.x - r.xMax)), dy = Mathf.Max(0f, Mathf.Max(r.yMin - p.y, p.y - r.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>A altura do chão aplainado no ponto.</summary>
        public float Chao(float x, float z)
        {
            float fx = (x - _origemChao.x) / PassoDoChao, fz = (z - _origemChao.y) / PassoDoChao;
            int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz);
            float tx = fx - i, tz = fz - j;
            float l0 = Linha(j - 1, i, tx), l1 = Linha(j, i, tx), l2 = Linha(j + 1, i, tx), l3 = Linha(j + 2, i, tx);
            return CatmullRom(l0, l1, l2, l3, tz);
        }

        float Linha(int j, int i, float t) => CatmullRom(H(i - 1, j), H(i, j), H(i + 1, j), H(i + 2, j), t);

        float H(int i, int j) => _chao[Mathf.Clamp(j, 0, _nChao - 1) * _nChao + Mathf.Clamp(i, 0, _nChao - 1)];

        static float CatmullRom(float a, float b, float c, float d, float t) =>
            b + 0.5f * t * (c - a + t * (2f * a - 5f * b + 4f * c - d + t * (3f * (b - c) + d - a)));
    }
}
