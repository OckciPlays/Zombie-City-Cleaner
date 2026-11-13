using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A casa da partida no chão (10/11/2026, a pedido: "coloque para o jogador nascer perto de alguma estrada... uma casa
    /// perto da estrada... sem mobília, só com mesas e armas", com os carros e as motos ao lado, todos arrumados): o
    /// terreiro plano entre a estrada e a casa (na altura do asfalto, com o talude até o chão de fora), a térrea do kit de
    /// frente para a estrada, as vagas dos veículos (de frente para a estrada, prontos para sair) e os pontos de nascimento.
    /// Coordenadas do terreiro: a ao longo da estrada (o x da casa), d da linha do meio da estrada para a casa.
    /// </summary>
    public sealed class LugarDaPartida
    {
        /// <summary>Da linha do meio da estrada até a parede da frente da casa.</summary>
        public const float FrenteDaCasa = 24f;
        /// <summary>O meio dos veículos (o carro de 4,8 m vai de 12 a 17 m da linha do meio; o acostamento acaba em 9,5).</summary>
        public const float LinhaDosVeiculos = 14.5f;
        /// <summary>Onde o jogador nasce, entre os veículos e a casa.</summary>
        public const float LinhaDaPartida = 19.5f;
        /// <summary>O quintal atrás da casa e a folga dos lados.</summary>
        const float Fundos = 4f, Lados = 2.5f;
        /// <summary>A passagem do terreiro para o chão de fora.</summary>
        public const float Talude = 10f;

        public readonly Vector2 Origem, Fora, Lado;
        /// <summary>O chão do terreiro (o asfalto na frente da casa, menos os 8 cm do chão da pista).</summary>
        public readonly float Altura;
        public readonly Planta Planta;
        /// <summary>O meio da casa (o centro da planta) no terreiro.</summary>
        public readonly Vector2 CasaEm;
        /// <summary>O terreiro (a, d) e a casa (com a garagem, a varanda e os degraus de trás).</summary>
        public readonly Rect Terreiro, Casa;
        /// <summary>O giro da casa (o z dela para fora da estrada) e o dos veículos (de frente para a estrada).</summary>
        public readonly float GiroDaCasa, GiroDosVeiculos;

        /// <summary>As vagas: a perua, o cupê, a moto cross e a Fat Boy (a ordem dos prefabs da cena), à esquerda da porta.</summary>
        public static readonly float[] VagasEmA = { -11f, -7.4f, -4.3f, -2.4f };
        /// <summary>Os pontos de nascimento, à direita da porta, de frente para a casa.</summary>
        public static readonly float[] PartidasEmA = { 1.5f, 3f, 4.5f, 6f };

        LugarDaPartida(Vector2 origem, Vector2 fora, float altura, Planta planta, Medidas m, float bordaDaPista)
        {
            Origem = origem;
            Fora = fora.normalized;
            // o x da casa no mapa, com o z dela para fora: o giro de Unity leva o x para (cos, -sen) = (z.y, -z.x)
            Lado = new Vector2(Fora.y, -Fora.x);
            Altura = altura;
            Planta = planta;
            GiroDaCasa = Mathf.Atan2(Fora.x, Fora.y) * Mathf.Rad2Deg;
            GiroDosVeiculos = GiroDaCasa + 180f;
            var lx = global::ZombieCityCleaner.Mundo.Casa.Limites(planta, m);
            var lz = global::ZombieCityCleaner.Mundo.Casa.Profundidade(planta, m);
            // a casa no meio do terreiro em a; a parede da frente na FrenteDaCasa
            CasaEm = new Vector2(-(lx.x + lx.y) * 0.5f, FrenteDaCasa + planta.D * 0.5f);
            Casa = Rect.MinMaxRect(CasaEm.x + lx.x, CasaEm.y + lz.x, CasaEm.x + lx.y, CasaEm.y + lz.y);
            float meia = Mathf.Max(Casa.width * 0.5f + Lados, -VagasEmA[0] + 3f, PartidasEmA[PartidasEmA.Length - 1] + 3f);
            Terreiro = Rect.MinMaxRect(-meia, bordaDaPista - 0.5f, meia, Casa.yMax + Fundos);
        }

        /// <summary>A casa da partida do mapa (null sem ela): o chão do terreiro é o da estrada na frente da casa.</summary>
        public static LugarDaPartida De(ChaoDeLakewood chao)
        {
            var c = chao.Mapa.casaDaPartida;
            if (c == null || !c.tem || c.paraACasa.sqrMagnitude < 0.01f) return null;
            if (!chao.EstradaPerto(c.naEstrada.x, c.naEstrada.y, 10f, out _, out float hr, out _, out float meia)) return null;
            return new LugarDaPartida(c.naEstrada, c.paraACasa, hr - 0.08f, PlantaDa(c.semente), Medidas.DoJogo, meia + 3f);
        }

        /// <summary>A térrea da casa da partida: sem porão, o portão da garagem aberto e sem espelhar.</summary>
        public static Planta PlantaDa(int semente)
        {
            var r = new System.Random(semente);
            var p = PlantasDeCasa.Terrea(r, Medidas.DoJogo, false);
            PlantasDeCasa.Estilo(r, p);
            p.espelhar = false;
            p.portaDaFrenteAberta = true;
            if (p.garagem != null) p.garagem.fechada = false;
            return p;
        }

        public Vector2 NoMapa(float a, float d) => Origem + Lado * a + Fora * d;

        public Vector2 Local(float x, float z)
        {
            var p = new Vector2(x - Origem.x, z - Origem.y);
            return new Vector2(Vector2.Dot(p, Lado), Vector2.Dot(p, Fora));
        }

        /// <summary>Um ponto do terreiro no mundo (y acima do chão dele).</summary>
        public Vector3 NoMundo(float a, float d, float y = 0f)
        {
            var p = NoMapa(a, d);
            return new Vector3(p.x, Altura + y, p.y);
        }

        /// <summary>A casa: o meio dela no chão do terreiro e o giro (o z dela para fora da estrada).</summary>
        public Vector3 PosicaoDaCasa => NoMundo(CasaEm.x, CasaEm.y);
        public Quaternion RotacaoDaCasa => Quaternion.Euler(0f, GiroDaCasa, 0f);

        /// <summary>A distância do ponto até o terreiro (0 dentro).</summary>
        public float DistanciaDoTerreiro(float x, float z)
        {
            var p = Local(x, z);
            float da = Mathf.Max(Terreiro.xMin - p.x, 0f, p.x - Terreiro.xMax), dd = Mathf.Max(Terreiro.yMin - p.y, 0f, p.y - Terreiro.yMax);
            return Mathf.Sqrt(da * da + dd * dd);
        }

        /// <summary>O chão com o terreiro: plano dentro dele e o talude até o chão de fora ("h").</summary>
        public float Chao(float x, float z, float h)
        {
            float d = DistanciaDoTerreiro(x, z);
            if (d >= Talude) return h;
            return d <= 0f ? Altura : Mathf.Lerp(Altura, h, Geo.Suave(0f, Talude, d));
        }

        /// <summary>O meio do terreiro no mapa e o raio que pega tudo (o talude incluído).</summary>
        public Vector2 Meio => NoMapa(Terreiro.center.x, Terreiro.center.y);
        public float Raio => Terreiro.size.magnitude * 0.5f + Talude;
    }

    public sealed partial class ChaoDeLakewood
    {
        /// <summary>A casa da partida (null sem ela; <see cref="MapaDeLakewood.casaDaPartida"/>).</summary>
        public LugarDaPartida Partida { get; private set; }
    }
}
