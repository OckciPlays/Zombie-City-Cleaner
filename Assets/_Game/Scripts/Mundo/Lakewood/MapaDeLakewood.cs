using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>O tipo de uma via do mapa (Plano_Lakewood, Etapa 5).</summary>
    public enum TipoDeVia { Rodovia, Secundaria, Terra, Rua }

    public enum TipoDeCidade { Principal, Pequena, Vila, Rural }

    /// <summary>Os pontos de interesse do condado (Plano_Lakewood, Etapas 4, 6 e 8).</summary>
    public enum TipoDeLugarDoMapa
    {
        Represa, Usina, Laboratorio, Aeroporto, BaseMilitar, Parque, CentroDeVisitantes, EstacaoDeTratamento,
        Acampamento, Antena, Marina, CasasDeLago, Serraria, Pedreira, Mirante, Ponte, EstacaoAmbiental
    }

    /// <summary>As cinco ilhas do reservatório (Plano_Lakewood, Etapa 3) e os ilhotes.</summary>
    public enum TipoDeIlha { Central, Acampamento, Antena, Residencial, Pesquisa, Ilhote }

    public enum EstadoDaPonte { Inteira, Bloqueada }

    /// <summary>O que cobre cada célula da grade lida da imagem de referência (a ordem é o valor gravado no asset).</summary>
    public enum Cobertura : byte { Mata, Campo, Lavoura, Rocha, Agua, Urbano }

    /// <summary>
    /// Uma região do relevo (a planície do oeste, a serra do norte, as colinas do leste...): dentro do contorno, o chão
    /// vale a altitude dela mais as colinas do ruído dela; na beira, a passagem suave para o que está fora.
    /// </summary>
    [Serializable]
    public class ZonaDeRelevo
    {
        public string nome;
        public List<Vector2> contorno = new List<Vector2>();
        [Tooltip("A altitude média do chão na zona (m).")] public float altitude = 200f;
        [Tooltip("O quanto as colinas sobem e descem (m).")] public float amplitude = 40f;
        [Tooltip("O tamanho das colinas (m).")] public float escala = 1600f;
        [Tooltip("O quanto as cristas do ruído somam por cima (m): as zonas rochosas.")] public float aspereza;
        [Tooltip("O quanto as montanhas da imagem valem aqui (1: inteiras; 0,35: as colinas baixas do oeste).")] public float fatorDasMontanhas = 1f;
        [Tooltip("A largura da passagem na beira da zona (m), metade para cada lado.")] public float borda = 700f;
    }

    [Serializable]
    public class PicoDoMapa
    {
        public string nome;
        public Vector2 ponto;
        [Tooltip("O quanto o pico sobe por cima do relevo (m).")] public float subida = 60f;
        public float raio = 900f;
        [Tooltip("O rótulo, a partir do ponto (m).")] public Vector2 rotulo = new Vector2(0f, -350f);
    }

    /// <summary>Um vale aberto na serra (por onde uma estrada ou um rio passa): perto da linha, o relevo desce até a altura dela.</summary>
    [Serializable]
    public class PassagemDoMapa
    {
        public string nome;
        public List<Vector2> pontos = new List<Vector2>();
        [Tooltip("A distância da linha em que a passagem vale 37% (m).")] public float largura = 500f;
        [Tooltip("A altura do fundo dela (m).")] public float altura = 250f;
    }

    [Serializable]
    public class ParametrosDoRelevo
    {
        [Tooltip("A altitude do chão fora de todas as zonas (m).")] public float altitudeBase = 185f;
        [Tooltip("O quanto as colinas sobem e descem fora das zonas (m).")] public float amplitude = 30f;
        [Tooltip("O tamanho das colinas (m).")] public float escala = 2200f;
        [Tooltip("A altura das montanhas da imagem (m, onde a grade da montanha vale 1).")] public float alturaDasMontanhas = 330f;
        [Tooltip("O quanto as cristas do ruído somam nas montanhas (m).")] public float asperezaDasMontanhas = 55f;
        [Tooltip("O raio (m) do amaciamento da grade das montanhas: a borda da mancha da imagem vira a encosta.")] public float suavidadeDasMontanhas = 220f;
        [Tooltip("O quanto o vale de cada rio abre para os lados (m).")] public float aberturaDosVales = 300f;
        [Tooltip("O raio (m) da média que aplaina o chão das cidades e das instalações.")] public float raioDoAplainamento = 250f;
        [Tooltip("Pintadas em ordem: a de depois fica por cima.")] public List<ZonaDeRelevo> zonas = new List<ZonaDeRelevo>();
        public List<PicoDoMapa> picos = new List<PicoDoMapa>();
        public List<PassagemDoMapa> passagens = new List<PassagemDoMapa>();
    }

    [Serializable]
    public class LagoDoMapa
    {
        public string nome;
        public List<Vector2> contorno = new List<Vector2>();
        [Tooltip("A altura da água (m). No reservatório, a cota fixa do plano: 150 m.")] public float nivel = 150f;
        [Tooltip("A fundura longe da margem (m).")] public float fundura = 32f;
        [Tooltip("A água na altura do chão em volta (o relevo calcula); senão, a do nível.")] public bool nivelPeloRelevo;
        [Tooltip("A distância (m) em que a margem oeste sobe do nível da água até o relevo: comprida, a margem rasa e suave.")] public float rampaDaMargemOeste = 900f;
        [Tooltip("A mesma distância na margem leste: curta, as encostas rochosas.")] public float rampaDaMargemLeste = 350f;
        [Tooltip("Onde vai o nome (zero: no meio do lago).")] public Vector2 rotulo;
        public float giroDoRotulo;
    }

    [Serializable]
    public class IlhaDoMapa
    {
        public string nome;
        public TipoDeIlha tipo = TipoDeIlha.Ilhote;
        public List<Vector2> contorno = new List<Vector2>();
        [Tooltip("A altura no meio da ilha, acima da água (m).")] public float altura = 22f;
    }

    [Serializable]
    public class RioDoMapa
    {
        public string nome;
        [Tooltip("Da nascente à foz.")] public List<Vector2> pontos = new List<Vector2>();
        public float larguraNoComeco = 8f, larguraNoFim = 30f;
        [Tooltip("A altura da água na nascente e na foz (m), quando não é pelo relevo.")] public float nivelNoComeco = 350f, nivelNoFim = 150f;
        [Tooltip("Como a água desce: 1 por igual; 3 rápida no começo (a serra) e quase parada no fim.")] public float curva = 3f;
        [Tooltip("A água pelo chão (2 m abaixo dele, sempre descendo), chegando na do lago ou do rio onde ele cai.")] public bool nivelPeloRelevo = true;
        [Range(0f, 1f), Tooltip("Onde vai o nome, na fração do comprimento.")] public float posicaoDoRotulo = 0.5f;
    }

    /// <summary>
    /// A barragem (Plano_Lakewood, Etapa 4): a linha da crista de uma encosta (a) à outra (b), que fecha o reservatório.
    /// O contorno do lago passa por a e b; do lado de fora (a jusante) fica o vale baixo do rio.
    /// </summary>
    [Serializable]
    public class RepresaDoMapa
    {
        public string nome = "Lakewood Dam";
        [Tooltip("As duas cabeceiras da crista, nas encostas.")] public Vector2 a, b;
        [Tooltip("A altura da crista (m): acima da água do reservatório.")] public float alturaDaCrista = 158f;
        public float larguraDaCrista = 10f;
        [Tooltip("O chão no pé da barragem, do lado de fora (m).")] public float alturaDoPe = 108f;
        [Tooltip("Os pontos da margem que afunilam o lago até a cabeceira a (do lago para a) e os de b (de b para o lago).")]
        public List<Vector2> funilOeste = new List<Vector2>(), funilLeste = new List<Vector2>();
        [Tooltip("A caixa (x, z, largura, comprimento) onde o contorno lido da imagem é trocado pelo funil e pela crista.")] public Rect recorte;

        public bool Tem => a != b;
        public Vector2 Meio => (a + b) * 0.5f;
        public float Comprimento => Vector2.Distance(a, b);
        public Vector2 Direcao => (b - a).normalized;
    }

    [Serializable]
    public class ViaDoMapa
    {
        public string nome;
        [Tooltip("O número do escudo (US-12, 41); vazio, sem escudo.")] public string numero;
        public TipoDeVia tipo;
        public List<Vector2> pontos = new List<Vector2>();
        [Tooltip("A altura presa do asfalto no começo e no fim (m; 0: pelo chão): as estradas que saem da crista da barragem.")]
        public float alturaNoComeco, alturaNoFim;
        [Tooltip("A rampa máxima (0: a do tipo, 6% na rodovia e 9% nas outras): as de serra.")]
        public float rampaMaxima;
        [Tooltip("Da altura presa do começo, só desce (a estrada corta o morro em vez de subir nele): a que sai da crista para o pé.")]
        public bool soDesce;
        [Tooltip("A largura do asfalto (m; 0: a do tipo). As ruas das cidades (Etapa 6) usam a delas.")]
        public float largura;
    }

    [Serializable]
    public class PonteDoMapa
    {
        public string nome;
        public Vector2 perto;
        public EstadoDaPonte estado;
    }

    /// <summary>Uma cidade ou comunidade: a área reservada (o contorno), que o relevo aplaina (Plano_Lakewood, Etapas 2 e 6).</summary>
    [Serializable]
    public class CidadeDoMapa
    {
        public string nome;
        [Tooltip("O que ela é (para o mapa e o plano).")] public string descricao;
        public TipoDeCidade tipo;
        public Vector2 centro;
        public List<Vector2> contorno = new List<Vector2>();
        [Tooltip("O giro da grade de ruas (graus).")] public float giro;
        [Tooltip("A cota do platô (m); zero: a média do relevo em volta.")] public float altitude;
        [Range(0f, 1f), Tooltip("O quanto o chão é aplainado dentro do contorno.")] public float aplainar = 0.85f;
        [Tooltip("A largura da passagem na beira (m).")] public float borda = 300f;
        [Tooltip("O nome, a partir do centro (m).")] public Vector2 rotulo = new Vector2(0f, 420f);
        [Tooltip("A população de antes do surto (a placa da entrada, riscada).")] public int populacao;
    }

    [Serializable]
    public class LugarDoMapa
    {
        public string nome;
        public TipoDeLugarDoMapa tipo;
        public Vector2 ponto;
        public float raio = 150f;
        public float giro;
        [Range(0f, 1f), Tooltip("O quanto o chão é aplainado no raio (as instalações: o aeroporto, a estação).")] public float aplainar;
        [Tooltip("O nome, a partir do ponto (m).")] public Vector2 rotulo = new Vector2(0f, -260f);
    }

    /// <summary>
    /// A casa da partida (10/11/2026, a pedido: "coloque para o jogador nascer perto de alguma estrada... uma casa perto da
    /// estrada... sem mobília, só com mesas e armas", com os carros e as motos ao lado, todos arrumados): uma térrea do
    /// kit, de frente para a estrada, no terreiro plano onde os veículos ficam e o jogador nasce. O lugar é achado no
    /// Editor (Zombie City Cleaner > Lakewood > A casa da partida) e guardado aqui; o resto sai do ChaoDeLakewood.
    /// </summary>
    [Serializable]
    public class CasaDaPartida
    {
        public bool tem;
        [Tooltip("O meio da estrada na frente da casa.")] public Vector2 naEstrada;
        [Tooltip("A direção da estrada para a casa (unitária).")] public Vector2 paraACasa = Vector2.up;
        [Tooltip("A estrada (o nome), para o relatório.")] public string estrada;
        [Tooltip("O sorteio da casa (a térrea do kit).")] public int semente = 11;

        /// <summary>O meio da casa, mais ou menos (a parede da frente fica a 24 m da linha do meio da estrada).</summary>
        public Vector2 MeioDaCasa => naEstrada + paraACasa.normalized * 30f;

        /// <summary>
        /// O lote de cidade que a casa da partida toma (12/11/2026: ela foi para o lote de casa da Woodcreek perto do lago):
        /// esse lote fica vazio, sem a casa da cidade e sem o patamar (o terreiro da partida manda ali).
        /// </summary>
        public bool TomaOLote(LoteDoMapa l) => tem && l != null && (l.tipo == UsoDoLote.Casa || l.tipo == UsoDoLote.Vazio)
                                                && Vector2.Distance(l.meio, MeioDaCasa) < 25f;
    }

    /// <summary>Um túnel da rede subterrânea (Plano_Lakewood, Etapa 8): por enquanto, só a linha reservada no mapa.</summary>
    [Serializable]
    public class TunelDoMapa
    {
        public string nome;
        public List<Vector2> pontos = new List<Vector2>();
    }

    /// <summary>
    /// O mapa fixo do Condado de Lakewood (Plano_Lakewood, 08/11/2026): a fonte de tudo o que só uma pessoa decide, em
    /// metros no mundo. O x cresce para o leste e o y do Vector2 (o z do mundo) para o norte; a origem (0, 0) é o canto
    /// sudoeste do mapa, que tem <see cref="largura"/> por <see cref="comprimento"/> (12 × 10 km), em setores de 3 × 2,5
    /// km (4 × 4). Os pontos das linhas e dos contornos são os de controle: quem usa suaviza com <see cref="Linha"/> e
    /// <see cref="Contorno"/> (Chaikin, 3 passadas, iguais para todos). O desenho inicial vem do esboço do código
    /// (`EsbocoDeLakewood`) e da leitura da imagem de referência (`LeituraDaReferencia`: o lago, as ilhas e a cobertura);
    /// daí em diante, o asset é a fonte, editado na janela Zombie City Cleaner > Lakewood > Desenho do mapa.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie City Cleaner/Mapa de Lakewood", fileName = "MapaDeLakewood")]
    public sealed class MapaDeLakewood : ScriptableObject
    {
        /// <summary>Onde o mapa fica no projeto.</summary>
        public const string Caminho = "Assets/_Game/Settings/Lakewood/MapaDeLakewood.asset";

        public string nome = "Lakewood County";
        [Tooltip("O sorteio do que o desenho não diz (o ruído do relevo, as plantas).")] public int semente = 1;

        [Header("O tamanho (m) e os setores")]
        [Tooltip("De oeste a leste (o x).")] public float largura = 12000f;
        [Tooltip("De sul a norte (o z).")] public float comprimento = 10000f;
        public int setoresX = 4, setoresZ = 4;

        [Header("O relevo e a água")]
        public ParametrosDoRelevo relevo = new ParametrosDoRelevo();
        public List<LagoDoMapa> lagos = new List<LagoDoMapa>();
        public List<IlhaDoMapa> ilhas = new List<IlhaDoMapa>();
        public List<RioDoMapa> rios = new List<RioDoMapa>();
        public RepresaDoMapa represa = new RepresaDoMapa();
        [Tooltip("O vale afogado do reservatório (do braço norte até a barragem): o fundo é mais fundo ao longo dele, crescendo para a barragem.")]
        public List<Vector2> canalDoReservatorio = new List<Vector2>();
        [Tooltip("A distância do meio do canal em que a fundura dele cai para 37% (m).")] public float larguraDoCanal = 450f;
        [Tooltip("A fundura do canal na barragem, por baixo da fundura do lago (m).")] public float funduraDoCanal = 26f;

        [Header("As vias (Etapa 5)")]
        public List<ViaDoMapa> estradas = new List<ViaDoMapa>();
        public List<PonteDoMapa> pontes = new List<PonteDoMapa>();

        [Header("As cidades, os lugares e a rede subterrânea")]
        public List<CidadeDoMapa> cidades = new List<CidadeDoMapa>();
        public List<LugarDoMapa> lugares = new List<LugarDoMapa>();
        public List<TunelDoMapa> tuneis = new List<TunelDoMapa>();

        [Header("As ruas e os lotes das cidades (Etapa 6)")]
        [Tooltip("As ruas novas das cidades (as estradas da Etapa 5 que passam por elas ficam em estradas).")]
        public List<RuaDaCidade> ruas = new List<RuaDaCidade>();
        [Tooltip("Os lotes das cidades: o lugar, o giro, o tamanho e o que vai em cada um.")]
        public List<LoteDoMapa> lotes = new List<LoteDoMapa>();

        [Header("A partida")]
        [Tooltip("Onde a partida fica por enquanto (a cena acha o chão seco mais perto).")] public Vector2 partida = new Vector2(8550f, 3600f);
        [Tooltip("A casa da partida, com as mesas das armas e os veículos arrumados no terreiro (com ela, a partida é o terreiro).")]
        public CasaDaPartida casaDaPartida = new CasaDaPartida();

        [Header("A cobertura (lida da imagem de referência)")]
        [Tooltip("O lado de cada célula das grades (m).")] public float celulaDaGrade = 50f;
        public int gradeX, gradeZ;
        [Tooltip("O que cobre cada célula (Cobertura: 0 mata, 1 campo, 2 lavoura, 3 rocha, 4 água, 5 urbano), da linha do sul para a do norte.")] public byte[] cobertura;
        [Tooltip("O quanto cada célula é montanha (0 a 255), da linha do sul para a do norte.")] public byte[] montanha;

        public bool TemGrade => gradeX > 1 && gradeZ > 1 && cobertura != null && montanha != null && cobertura.Length == gradeX * gradeZ && montanha.Length == gradeX * gradeZ;

        public Vector2 Meio => new Vector2(largura * 0.5f, comprimento * 0.5f);

        public bool Dentro(float x, float z) => x >= 0f && z >= 0f && x <= largura && z <= comprimento;

        // ------------------------------------------------------------------ os setores

        public float LadoDoSetorX => largura / Mathf.Max(1, setoresX);
        public float LadoDoSetorZ => comprimento / Mathf.Max(1, setoresZ);

        /// <summary>O setor de um ponto (i de oeste a leste, j de sul a norte; fora do mapa, o da beira).</summary>
        public void SetorDe(float x, float z, out int i, out int j)
        {
            i = Mathf.Clamp(Mathf.FloorToInt(x / LadoDoSetorX), 0, Mathf.Max(1, setoresX) - 1);
            j = Mathf.Clamp(Mathf.FloorToInt(z / LadoDoSetorZ), 0, Mathf.Max(1, setoresZ) - 1);
        }

        /// <summary>O nome do setor: a coluna em letra (A no oeste) e a linha em número (1 no sul): "A1", "D4".</summary>
        public static string NomeDoSetor(int i, int j) => $"{(char)('A' + i)}{j + 1}";

        public string NomeDoSetorEm(float x, float z)
        {
            SetorDe(x, z, out int i, out int j);
            return NomeDoSetor(i, j);
        }

        /// <summary>A caixa do setor (x, z, largura, comprimento), em metros.</summary>
        public Rect LimitesDoSetor(int i, int j) => new Rect(i * LadoDoSetorX, j * LadoDoSetorZ, LadoDoSetorX, LadoDoSetorZ);

        // ------------------------------------------------------------------ as grades da imagem

        // a posição na grade (o meio da célula 0 fica meia célula dentro da borda)
        void NaGrade(float x, float z, out int i, out int j, out float tx, out float tz)
        {
            float fx = Mathf.Clamp(x / celulaDaGrade - 0.5f, 0f, gradeX - 1.001f), fz = Mathf.Clamp(z / celulaDaGrade - 0.5f, 0f, gradeZ - 1.001f);
            i = (int)fx;
            j = (int)fz;
            tx = fx - i;
            tz = fz - j;
        }

        /// <summary>O quanto o ponto é montanha (de 0 a 1, entre as 4 células em volta).</summary>
        public float Montanha(float x, float z)
        {
            if (!TemGrade) return 0f;
            NaGrade(x, z, out int i, out int j, out float tx, out float tz);
            int k = j * gradeX + i;
            float a = Mathf.Lerp(montanha[k], montanha[k + 1], tx), b = Mathf.Lerp(montanha[k + gradeX], montanha[k + gradeX + 1], tx);
            return Mathf.Lerp(a, b, tz) / 255f;
        }

        /// <summary>O que cobre a célula do ponto (sem a grade, a mata).</summary>
        public Cobertura CoberturaEm(float x, float z)
        {
            if (!TemGrade) return Cobertura.Mata;
            int i = Mathf.Clamp(Mathf.FloorToInt(x / celulaDaGrade), 0, gradeX - 1), j = Mathf.Clamp(Mathf.FloorToInt(z / celulaDaGrade), 0, gradeZ - 1);
            return (Cobertura)cobertura[j * gradeX + i];
        }

        /// <summary>Quantas coberturas há (os valores de <see cref="Cobertura"/>).</summary>
        public const int Coberturas = 6;

        /// <summary>
        /// As frações de cada cobertura no ponto (entre as 4 células em volta; somam 1), em "saida", na ordem de
        /// <see cref="Cobertura"/>: a mistura suave entre as células, para a beira delas não sair em degraus.
        /// </summary>
        public void Fracoes(float x, float z, Span<float> saida)
        {
            for (int k = 0; k < saida.Length; k++) saida[k] = 0f;
            if (!TemGrade)
            {
                saida[(int)Cobertura.Mata] = 1f;
                return;
            }
            NaGrade(x, z, out int i, out int j, out float tx, out float tz);
            int k0 = j * gradeX + i;
            saida[cobertura[k0]] += (1f - tx) * (1f - tz);
            saida[cobertura[k0 + 1]] += tx * (1f - tz);
            saida[cobertura[k0 + gradeX]] += (1f - tx) * tz;
            saida[cobertura[k0 + gradeX + 1]] += tx * tz;
        }

        /// <summary>
        /// O ponto deslocado pelo ruído (uns 36 m, em manchas de 230 m), para quem lê a grade: a beira das células não sai
        /// reta, e a mata, o campo e a rocha ficam com os contornos tortos de uma paisagem.
        /// </summary>
        public static Vector2 Deformar(Ruido r, float x, float z, float amplitude = 36f, float escala = 230f) =>
            new Vector2(x + amplitude * r.Fbm(x / escala, z / escala, 2), z + amplitude * r.Fbm(x / escala + 51.7f, z / escala - 19.3f, 2));

        /// <summary>Uma cobertura sorteada pelas frações (u de 0 a 1): na beira da mata, umas árvores soltas no campo.</summary>
        public static Cobertura Sortear(ReadOnlySpan<float> fracoes, float u)
        {
            float acum = 0f;
            for (int k = 0; k < fracoes.Length; k++)
            {
                acum += fracoes[k];
                if (u < acum) return (Cobertura)k;
            }
            return Cobertura.Mata;
        }

        /// <summary>O quanto o ponto é de um tipo de cobertura (de 0 a 1, entre as 4 células em volta: as beiradas saem suaves).</summary>
        public float Fracao(Cobertura tipo, float x, float z)
        {
            if (!TemGrade) return tipo == Cobertura.Mata ? 1f : 0f;
            NaGrade(x, z, out int i, out int j, out float tx, out float tz);
            int k = j * gradeX + i;
            byte t = (byte)tipo;
            float a = Mathf.Lerp(cobertura[k] == t ? 1f : 0f, cobertura[k + 1] == t ? 1f : 0f, tx);
            float b = Mathf.Lerp(cobertura[k + gradeX] == t ? 1f : 0f, cobertura[k + gradeX + 1] == t ? 1f : 0f, tx);
            return Mathf.Lerp(a, b, tz);
        }

        // ------------------------------------------------------------------ as linhas

        /// <summary>Uma linha aberta do desenho (os rios, as vias, os túneis), suavizada.</summary>
        public static List<Vector2> Linha(IList<Vector2> pontos) => Linhas.Suavizar(pontos, 3, false);

        /// <summary>Um contorno fechado do desenho (os lagos, as ilhas, as zonas, as cidades), suavizado.</summary>
        public static List<Vector2> Contorno(IList<Vector2> pontos) => Linhas.Suavizar(pontos, 3, true);

        /// <summary>Uma cópia solta (para pintar em outra thread enquanto este é editado). Só na thread principal.</summary>
        public MapaDeLakewood Copia()
        {
            var c = CreateInstance<MapaDeLakewood>();
            c.hideFlags = HideFlags.HideAndDontSave;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), c);
            return c;
        }

        /// <summary>A largura do rio na fração do comprimento (m).</summary>
        public static float LarguraDoRio(RioDoMapa r, float fracao) => Mathf.Lerp(r.larguraNoComeco, r.larguraNoFim, fracao);

        /// <summary>A altura do barranco de um rio acima da água, pela largura dele (m): de 0,6 num córrego a 2 m num rio largo.</summary>
        public static float MargemDoRio(float largura) => Mathf.Clamp(0.35f + 0.045f * largura, 0.6f, 2.0f);

        /// <summary>A fundura do leito no meio do rio, pela largura (m).</summary>
        public static float FunduraDoRio(float largura) => 0.5f + 0.08f * largura;

        /// <summary>
        /// A linha de um rio no mundo: a do desenho suavizada e reamostrada de 6 em 6 m, com os meandros (as curvas de
        /// um rio de verdade: o comprimento de onda de umas 10 larguras e a amplitude de 1,6 largura, menores na serra,
        /// sumindo na nascente e na foz para elas ficarem onde o desenho diz) e as irregularidades miúdas das margens.
        /// "montanha" (0 a 1) diz o quanto o ponto é serra (o vale estreito deixa pouco espaço para a curva). Código puro.
        /// </summary>
        public static List<Vector2> LinhaDoRio(RioDoMapa rio, int semente, int indice, Func<Vector2, float> montanha = null)
        {
            if (rio.pontos.Count < 2) return null;
            var basica = Linhas.Reamostrar(Linha(rio.pontos), 6f, false);
            int n = basica.Count;
            if (n < 3) return basica;
            var ruido = new Ruido(semente, "rios-de-lakewood");
            float total = Mathf.Max(Linhas.Comprimento(basica), 1e-3f), s = 0f;
            float fase = 6.2831853f * Sorteio.FracaoDoHash(Sorteio.Hash(semente, indice, 1, 2));
            var saida = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                if (i > 0) s += Vector2.Distance(basica[i - 1], basica[i]);
                float t = s / total;
                var dir = (basica[Mathf.Min(i + 1, n - 1)] - basica[Mathf.Max(i - 1, 0)]).normalized;
                var lado = new Vector2(-dir.y, dir.x);
                float larg = LarguraDoRio(rio, t);
                float onda = Mathf.Clamp(10f * larg, 70f, 420f);
                float amp = Mathf.Clamp(1.6f * larg, 8f, 45f);
                float serra = montanha != null ? Mathf.Clamp01(montanha(basica[i])) : 0f;
                amp *= 1f - 0.7f * serra;
                float pontas = Geo.Suave(0f, 0.07f, t) * Geo.Suave(1f, 0.94f, t);
                float u = s / onda;
                float m = Mathf.Sin(6.2831853f * u + fase) * 0.7f + 0.8f * ruido.Amostra(u * 1.7f + indice * 13.7f, 3.1f);
                float miudo = 0.12f * larg * ruido.Amostra(s / (2.5f * Mathf.Max(larg, 4f)) + indice * 7.3f, 9.7f);
                saida.Add(basica[i] + lado * (pontas * (amp * m + miudo)));
            }
            // uma passada de suavização tira as dobras, e a reamostragem deixa os pontos de novo a 6 m
            return Linhas.Reamostrar(Linhas.Suavizar(saida, 1, false), 6f, false);
        }

        /// <summary>A altura da água do rio na fração do comprimento (m), quando ela não é pelo relevo.</summary>
        public static float NivelDoRio(RioDoMapa r, float fracao) =>
            r.nivelNoFim + (r.nivelNoComeco - r.nivelNoFim) * Mathf.Pow(1f - Mathf.Clamp01(fracao), Mathf.Max(0.1f, r.curva));

        /// <summary>O lago maior (o reservatório), ou -1 sem lagos.</summary>
        public int LagoPrincipal()
        {
            int melhor = -1;
            float maior = 0f;
            for (int l = 0; l < lagos.Count; l++)
            {
                if (lagos[l].contorno.Count < 3) continue;
                float a = Linhas.Area(lagos[l].contorno);
                if (a > maior)
                {
                    maior = a;
                    melhor = l;
                }
            }
            return melhor;
        }

        /// <summary>Se a cidade já tem os lotes da Etapa 6 (o mapa pinta os de verdade, sem o sinal das ruas).</summary>
        public bool TemLotes(string cidade) => lotes != null && lotes.Exists(l => l.cidade == cidade);

        /// <summary>O centro de um contorno (o meio de uma ilha, de uma cidade).</summary>
        public static Vector2 CentroDe(List<Vector2> contorno) => contorno.Count >= 3 ? Linhas.Centro(Contorno(contorno)) : Vector2.zero;
    }
}
