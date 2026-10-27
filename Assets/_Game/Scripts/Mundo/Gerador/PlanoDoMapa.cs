using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    public enum TipoDeLugar : byte
    {
        CidadeGrande, CidadePequena, Vila, Fazenda, BaseMilitar, ComunidadeAliada, ComunidadeInimiga, Laboratorio, Cabana,
        Posto, Bunker, CasaDoLago, CasaDaFamilia, Barreira,
    }

    public enum TipoDeBairro : byte { Centro, Comercio, Residencial }

    public enum TipoDeEstrada : byte { Rodovia, Estrada, Terra }

    /// <summary>Um lugar do mapa: o tipo, o nome, o meio, o raio (m) e o giro (graus; o das ruas, numa cidade).</summary>
    public class Lugar
    {
        public TipoDeLugar Tipo;
        public string Nome;
        public Vector2 Centro;
        public float Raio;
        public float Giro;
        /// <summary>A cabana de preparador, com um bunker escondido embaixo (Plano_Mapa, sugestão 9).</summary>
        public bool Preparador;

        public bool EhCidade => Tipo == TipoDeLugar.CidadeGrande || Tipo == TipoDeLugar.CidadePequena || Tipo == TipoDeLugar.Vila;

        public float Distancia(Vector2 p) => Vector2.Distance(Centro, p);
    }

    /// <summary>
    /// Um bairro de uma cidade: o centro, o anel do comércio ou um setor dos bairros de casas (entre dois ângulos, em graus,
    /// contados do leste para o norte).
    /// </summary>
    public sealed class Bairro
    {
        public string Nome;
        public TipoDeBairro Tipo;
        public float AnguloDe, AnguloAte;
        /// <summary>Onde o nome vai no mapa.</summary>
        public Vector2 Centro;
    }

    /// <summary>
    /// Uma cidade (grande ou pequena) ou vila: o contorno irregular (<see cref="Q"/>: 1 na beira) e as zonas pela distância do
    /// meio (Plano_Mapa, "As cidades"): o centro (prédios), o anel do comércio e os bairros de casas, em setores com nome.
    /// </summary>
    public sealed class Cidade : Lugar
    {
        /// <summary>Até onde (em q) vão o centro e o comércio; 0 = não tem.</summary>
        public float FimDoCentro, FimDoComercio;
        public float Fase;
        public readonly List<Bairro> Bairros = new List<Bairro>();
        internal Ruido Contorno;
        /// <summary>As ruas, os lotes e o chão da cidade (Parte M5).</summary>
        public TracadoDaCidade Tracado;

        /// <summary>A distância relativa até a beira: 0 no meio, 1 na beira, mais que 1 fora.</summary>
        public float Q(float x, float z)
        {
            float dx = x - Centro.x, dz = z - Centro.y;
            float m = Mathf.Sqrt(dx * dx + dz * dz);
            if (m < 1e-3f) return 0f;
            return m / (Raio * (1f + 0.14f * Contorno.Amostra(dx / m * 1.6f + Fase, dz / m * 1.6f - Fase)));
        }

        /// <summary>O bairro no ponto (null fora da cidade).</summary>
        public Bairro BairroEm(float x, float z)
        {
            float q = Q(x, z);
            if (q > 1f || Bairros.Count == 0) return null;
            foreach (var b in Bairros)
            {
                if (b.Tipo == TipoDeBairro.Centro && q < FimDoCentro) return b;
                if (b.Tipo == TipoDeBairro.Comercio && q >= FimDoCentro && q < FimDoComercio) return b;
            }
            float ang = Mathf.Repeat(Mathf.Atan2(z - Centro.y, x - Centro.x) * Mathf.Rad2Deg, 360f);
            foreach (var b in Bairros)
            {
                if (b.Tipo != TipoDeBairro.Residencial) continue;
                float de = b.AnguloDe, ate = b.AnguloAte;
                bool dentro = de <= ate ? ang >= de && ang < ate : ang >= de || ang < ate;
                if (dentro) return b;
            }
            return Bairros[Bairros.Count - 1];
        }

        public TipoDeBairro ZonaEm(float x, float z)
        {
            float q = Q(x, z);
            return q < FimDoCentro ? TipoDeBairro.Centro : q < FimDoComercio ? TipoDeBairro.Comercio : TipoDeBairro.Residencial;
        }
    }

    /// <summary>
    /// Uma estrada: o tipo, os pontos (a cada ~12 m, já suavizados) e o comprimento. De e Para são índices em
    /// <see cref="PlanoDoMapa.Lugares"/> (-1 = a rede ou a borda): os da ligação inteira, que a <see cref="RedeDeEstradas"/>
    /// corta em pedaços sem repetição (cada pedaço guarda os da ligação). A rede também põe a altura do asfalto em cada ponto
    /// e onde vai o guard-rail (por segmento, de cada lado de quem anda do primeiro ponto para o último).
    /// </summary>
    public sealed class Estrada
    {
        public TipoDeEstrada Tipo;
        public readonly List<Vector2> Pontos = new List<Vector2>();
        public float Comprimento;
        public int De = -1, Para = -1;
        public readonly List<float> Alturas = new List<float>();
        public bool[] GuardRailEsquerda, GuardRailDireita;
        /// <summary>A distância de cada ponto (ao longo da estrada) até o nó mais perto dela (m).</summary>
        public float[] AteONo;
        /// <summary>
        /// O caminho por dentro de uma cidade (Parte M5), do meio dela até o portão de uma rodovia, pelas avenidas: entra
        /// no grafo das placas (quem atravessa a cidade vai por ele), mas não tem malha nem mexe no chão (as ruas são da cidade).
        /// </summary>
        public bool Conector;
    }

    /// <summary>Uma linha do metrô: as estações em ordem, com o nome de cada uma (o bairro ou a cidade onde ela fica).</summary>
    public sealed class LinhaDeMetro
    {
        public string Nome;
        public readonly List<Vector2> Estacoes = new List<Vector2>();
        public readonly List<string> NomesDasEstacoes = new List<string>();
    }

    /// <summary>
    /// O desenho do mapa (Plano_Mapa, Parte M3): os lugares pela tabela dos tamanhos, com os nomes; as cidades com as
    /// zonas e os bairros; as estradas (rodovias entre as cidades e saindo do mapa pelas barreiras, estradas para as vilas,
    /// as bases e as comunidades, e as de terra para as fazendas, as cabanas e a beira do lago); os postos na beira das
    /// estradas; os bunkers; e o metrô ligando as cidades. Feito por <see cref="PlanejadorDoMapa"/>.
    /// </summary>
    public sealed class PlanoDoMapa
    {
        public readonly List<Lugar> Lugares = new List<Lugar>();
        public readonly List<Cidade> Cidades = new List<Cidade>();
        public readonly List<Estrada> Estradas = new List<Estrada>();
        public readonly List<LinhaDeMetro> Metro = new List<LinhaDeMetro>();
        /// <summary>O traçado de cada cidade e vila (Parte M5), na ordem de <see cref="Cidades"/>.</summary>
        public readonly List<TracadoDaCidade> Tracados = new List<TracadoDaCidade>();
        public Lugar CasaDaFamilia, CasaDoLago, Laboratorio;
        /// <summary>O lote da casa da família (no traçado da cidade dela).</summary>
        public Lote LoteDaFamilia;
        public Cidade CidadeDaFamilia;
        public Bairro BairroDaFamilia;

        public int Indice(Lugar l) => Lugares.IndexOf(l);

        public IEnumerable<Lugar> DoTipo(TipoDeLugar tipo)
        {
            foreach (var l in Lugares)
                if (l.Tipo == tipo) yield return l;
        }

        public int Quantos(TipoDeLugar tipo)
        {
            int n = 0;
            foreach (var l in Lugares) if (l.Tipo == tipo) n++;
            return n;
        }

        /// <summary>A cidade (ou vila) que contém o ponto, ou null.</summary>
        public Cidade CidadeEm(float x, float z)
        {
            foreach (var c in Cidades)
                if (Mathf.Abs(x - c.Centro.x) < c.Raio * 1.2f && Mathf.Abs(z - c.Centro.y) < c.Raio * 1.2f && c.Q(x, z) <= 1f) return c;
            return null;
        }
    }
}
