using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O bunker da comunidade no chão do Lakewood (22/11/2026, a etapa 2 do Docs/23-Campanha-e-Mapa.md): a pegada do
    /// bunker do mundo sorteado (<see cref="PlantaDeBunker.Pegada"/>: o pátio, a caixa e os taludes do morro) plana na altura
    /// da estrada do portão, com um talude largo até o chão de fora (ele fica cavado na encosta), e o buraco no terreno
    /// embaixo da caixa inteira (como o <c>GeradorDoMundo.AbrirOBunker</c>: a laje da garagem cobre, e os andares de baixo
    /// ficam livres). O morro, o pátio de concreto e a cerca são da obra (<see cref="Bunker.MontarMorro"/>), postos pela
    /// cena. As coordenadas são as do mapa; o x e o z locais são os do bunker (o portão olha para o -z).
    /// </summary>
    public sealed class LugarDoBunker
    {
        /// <summary>A passagem da pegada plana para o chão de fora (m): largo, porque a encosta sobe atrás do morro.</summary>
        public const float Talude = 16f;

        /// <summary>O meio da caixa (no mapa) e o chão da garagem e do pátio.</summary>
        public readonly Vector2 Meio;
        public readonly float Giro, Altura;
        // os eixos do bunker no mapa: o x e o z dele
        readonly Vector2 _x, _z;

        LugarDoBunker(Vector2 meio, float giro, float altura)
        {
            Meio = meio;
            Giro = giro;
            Altura = altura;
            var r = Quaternion.Euler(0f, giro, 0f);
            var vx = r * Vector3.right;
            var vz = r * Vector3.forward;
            _x = new Vector2(vx.x, vx.z);
            _z = new Vector2(vz.x, vz.z);
        }

        /// <summary>O bunker do mapa (null sem ele: <see cref="MapaDeLakewood.bunker"/>).</summary>
        public static LugarDoBunker De(MapaDeLakewood mapa)
        {
            var b = mapa.bunker;
            return b != null && b.tem ? new LugarDoBunker(b.meio, b.giro, b.altura) : null;
        }

        /// <summary>Um ponto do bunker (x e z locais) no mapa.</summary>
        public Vector2 NoMapa(float x, float z) => Meio + _x * x + _z * z;

        /// <summary>Um ponto do mapa no espaço do bunker (x e z locais).</summary>
        public Vector2 Local(float x, float z)
        {
            var p = new Vector2(x - Meio.x, z - Meio.y);
            return new Vector2(Vector2.Dot(p, _x), Vector2.Dot(p, _z));
        }

        /// <summary>A distância do ponto (no mapa) até a pegada (0 dentro).</summary>
        public float DistanciaDaPegada(float x, float z)
        {
            var p = Local(x, z);
            var r = PlantaDeBunker.Pegada;
            float dx = Mathf.Max(Mathf.Max(r.xMin - p.x, 0f), p.x - r.xMax), dz = Mathf.Max(Mathf.Max(r.yMin - p.y, 0f), p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>O ponto (no mapa) fica no pátio de concreto (<see cref="PlantaDeBunker.Patio"/>, com meio metro de folga).</summary>
        public bool NoPatio(float x, float z)
        {
            var p = Local(x, z);
            var r = PlantaDeBunker.Patio;
            return p.x > r.xMin - 0.5f && p.x < r.xMax + 0.5f && p.y > r.yMin - 0.5f && p.y < r.yMax + 0.5f;
        }

        /// <summary>O chão com o bunker: plano na pegada e o talude até o chão de fora ("h").</summary>
        public float Chao(float x, float z, float h)
        {
            float d = DistanciaDaPegada(x, z);
            if (d >= Talude) return h;
            return d <= 0f ? Altura : Mathf.Lerp(Altura, h, Geo.Suave(0f, Talude, d));
        }

        /// <summary>O meio da pegada no mapa e o raio que pega tudo (o talude incluído).</summary>
        public Vector2 MeioDaPegada => NoMapa(PlantaDeBunker.Pegada.center.x, PlantaDeBunker.Pegada.center.y);
        public float Raio => PlantaDeBunker.Pegada.size.magnitude * 0.5f + Talude;

        /// <summary>O buraco no terreno: a caixa inteira, com a parede de fora e uma folga (o meio, os eixos e as meias medidas).</summary>
        public (Vector2 centro, Vector2 eixoX, Vector2 eixoZ, Vector2 meia) Buraco
        {
            get
            {
                float fx = PlantaDeBunker.X1 + PlantaDeBunker.ParedeDeFora * 0.5f + 0.3f, fz = PlantaDeBunker.Z1 + PlantaDeBunker.ParedeDeFora * 0.5f + 0.3f;
                return (Meio, _x, _z, new Vector2(fx, fz));
            }
        }

        /// <summary>Um ponto do esboço da planta (x de 0 a 32, z de 0 a 24) no mapa, com a altura (acima do chão da garagem).</summary>
        public Vector3 DoEsboco(Vector2 esboco, float y = 0f)
        {
            var p = NoMapa(esboco.x + PlantaDeBunker.X0, esboco.y + PlantaDeBunker.Z0);
            return new Vector3(p.x, Altura + y, p.y);
        }

        /// <summary>O giro (graus) de quem olha do ponto do esboço "de" para o "para".</summary>
        public float GiroDe(Vector2 de, Vector2 para)
        {
            var a = NoMapa(de.x + PlantaDeBunker.X0, de.y + PlantaDeBunker.Z0);
            var b = NoMapa(para.x + PlantaDeBunker.X0, para.y + PlantaDeBunker.Z0);
            return Mathf.Repeat(Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg, 360f);
        }
    }

    public sealed partial class ChaoDeLakewood
    {
        /// <summary>O bunker da comunidade (null sem ele; <see cref="MapaDeLakewood.bunker"/>).</summary>
        public LugarDoBunker Bunker { get; private set; }

        // o bunker no chão: o lugar e o buraco embaixo da caixa (no construtor, depois dos lotes)
        void PorOBunker()
        {
            Bunker = LugarDoBunker.De(Mapa);
            if (Bunker != null) Buracos.Add(Bunker.Buraco);
        }

        // ------------------------------------------------------------------ o relevo de longe embaixo do bunker

        /// <summary>A folga (m) em volta da caixa do bunker nas células do relevo de longe que saem dos setores.</summary>
        const float FolgaDoLongeNoBunker = 3f;

        /// <summary>
        /// (25/11/2026, a pedido do Pedro: "um pedaço de terra nos andares inferiores, na cozinha e mais embaixo") a célula do
        /// relevo de longe (o canto e as medidas, no mapa) encosta na caixa do bunker? O relevo de longe fica 6 m abaixo do
        /// chão: embaixo do pátio, isso é o B2 (a cozinha), e onde o relevo desce, o B3 e o B4. Essas células saem da malha
        /// dos setores (<see cref="Longe"/>) e vão para a <see cref="LongeDoBunker"/>, que a cena só mostra de longe (perto,
        /// os blocos de perto cobrem o lugar, com o buraco).
        /// </summary>
        public bool CelulaDeLongeNoBunker(float x0, float z0, float lx, float lz)
        {
            if (Bunker == null) return false;
            var (c, _, _, meia) = Bunker.Buraco;
            float r = meia.magnitude + FolgaDoLongeNoBunker;
            float dx = Mathf.Max(Mathf.Max(x0 - c.x, 0f), c.x - (x0 + lx));
            float dz = Mathf.Max(Mathf.Max(z0 - c.y, 0f), c.y - (z0 + lz));
            return dx * dx + dz * dz < r * r;
        }

        /// <summary>
        /// As células do relevo de longe que encostam na caixa do bunker (<see cref="CelulaDeLongeNoBunker"/>), numa malha só
        /// delas, com os mesmos vértices da grade do mapa todo (sem emenda com os setores). Null sem o bunker.
        /// </summary>
        public MalhaDoBloco LongeDoBunker(float passo = PassoDeLonge)
        {
            if (Bunker == null) return null;
            var (c, _, _, meia) = Bunker.Buraco;
            float r = meia.magnitude + FolgaDoLongeNoBunker;
            int i0 = Mathf.FloorToInt((c.x - r) / passo), i1 = Mathf.FloorToInt((c.x + r) / passo);
            int j0 = Mathf.FloorToInt((c.y - r) / passo), j1 = Mathf.FloorToInt((c.y + r) / passo);
            var v = new List<Vector3>();
            var cor = new List<Color32>();
            var uv = new List<Vector2>();
            System.Span<float> f = stackalloc float[MapaDeLakewood.Coberturas];
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                if (!CelulaDeLongeNoBunker(i * passo, j * passo, passo, passo)) continue;
                // os cantos na ordem da malha dos setores: (i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j)
                var p = new Vector3[4];
                var k = new Color32[4];
                var u = new Vector2[4];
                for (int q = 0; q < 4; q++)
                {
                    int a = i + (q >= 2 ? 1 : 0), b = j + (q == 1 || q == 2 ? 1 : 0);
                    float x = a * passo, z = b * passo;
                    p[q] = new Vector3(x, VerticeDeLonge(passo, a, b), z);
                    CorECopaDeLonge(x, z, Relevo.Altura(x, z), f, out k[q], out u[q]);
                }
                v.Add(p[0]); v.Add(p[1]); v.Add(p[2]);
                cor.Add(k[0]); cor.Add(k[1]); cor.Add(k[2]);
                uv.Add(u[0]); uv.Add(u[1]); uv.Add(u[2]);
                v.Add(p[0]); v.Add(p[2]); v.Add(p[3]);
                cor.Add(k[0]); cor.Add(k[2]); cor.Add(k[3]);
                uv.Add(u[0]); uv.Add(u[2]); uv.Add(u[3]);
            }
            return v.Count > 0 ? Fechar(v, cor, uv) : null;
        }
    }
}
