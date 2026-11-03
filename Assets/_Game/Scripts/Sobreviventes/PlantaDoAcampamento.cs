using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>Uma peça da cerca do acampamento (nas coordenadas dele).</summary>
    public enum PecaDaCerca : byte { Alambrado, Sacos, Conteiner, Barreira }

    /// <summary>
    /// O desenho de um acampamento (02/11/2026, a etapa 3), em dados, igual em todas as máquinas (sai da semente do mundo e
    /// do índice dele). Nas coordenadas do acampamento: o meio no chão aplainado, +z para o portão (a rodovia).
    ///  - a cerca quadrada (a meia largura <see cref="Meia"/>): pedaços de alambrado, muros de sacos de areia e contêineres,
    ///    com o portão no meio da frente, entre duas barreiras de concreto;
    ///  - a casa (uma que "já existia"): a térrea no pequeno e o sobrado no grande, de frente para o portão, no fundo;
    ///  - as bancas dos comerciantes, em duas filas viradas para o meio (3 no pequeno, 7 no grande);
    ///  - as fogueiras na praça (1 ou 2), as barracas dos moradores nos cantos de trás;
    ///  - os lugares das pessoas, na ordem dos índices: os guardas (2 ou 4), os comerciantes (um por banca) e os moradores
    ///    (2 a 5, em volta das fogueiras).
    /// </summary>
    public sealed class PlantaDoAcampamento
    {
        public struct Peca
        {
            public PecaDaCerca tipo;
            public Vector3 pos;
            public float giro;
            public int variante;
        }

        public struct Banca
        {
            /// <summary>O meio do balcão, para onde o balcão olha (o giro) e onde o comerciante fica (atrás dele).</summary>
            public Vector3 balcao, comerciante;
            public float giro;
            public CategoriaDoComercio categoria;
        }

        public struct Posto
        {
            public Vector3 pos, ronda;
            public float giro;
        }

        public readonly Acampamento Lugar;
        public readonly float Meia;
        public readonly Planta Casa;
        /// <summary>O meio da casa (no chão) e o giro dela (180: a frente da planta, o -z, olha para o portão).</summary>
        public readonly Vector3 PosicaoDaCasa;
        public const float GiroDaCasa = 180f;
        public readonly List<Peca> Cerca = new List<Peca>();
        public readonly List<Banca> Bancas = new List<Banca>();
        public readonly List<Posto> Guardas = new List<Posto>();
        public readonly List<Vector3> Fogueiras = new List<Vector3>();
        public readonly List<Vector3> Moradores = new List<Vector3>();
        public readonly List<(Vector3 pos, float giro)> Barracas = new List<(Vector3, float)>();
        /// <summary>Onde os comerciantes se escondem quando o acampamento fica hostil (dentro da casa).</summary>
        public readonly Vector3 Esconderijo;
        /// <summary>
        /// Os esconderijos dentro da casa: o meio de cada cômodo do térreo (03/11/2026: antes, todos iam para o mesmo ponto,
        /// o <see cref="Esconderijo"/>, e se amontoavam).
        /// </summary>
        public readonly List<Vector3> Esconderijos = new List<Vector3>();
        /// <summary>Fora do portão, na frente dele (para o F1 levar o jogador).</summary>
        public readonly Vector3 Portao;

        public int Pessoas => Guardas.Count + Bancas.Count + Moradores.Count;
        public int PrimeiroComerciante => Guardas.Count;
        public int PrimeiroMorador => Guardas.Count + Bancas.Count;

        readonly Matrix4x4 _paraOMundo;
        readonly float _giro;

        // (o tamanho na chave: a mesma semente em outro tamanho é outro mundo)
        static readonly Dictionary<(int, TamanhoDoMundo, int), PlantaDoAcampamento> _cache = new Dictionary<(int, TamanhoDoMundo, int), PlantaDoAcampamento>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Zerar() => _cache.Clear();

        /// <summary>O desenho do acampamento (feito uma vez por mundo).</summary>
        public static PlantaDoAcampamento Da(DadosDoMundo d, Acampamento a)
        {
            var chave = (d.Semente, d.Tamanho, a.Indice);
            if (!_cache.TryGetValue(chave, out var p))
            {
                if (_cache.Count >= 64) _cache.Clear();   // os mundos de antes, numa sessão longa
                p = new PlantaDoAcampamento(d, a);
                _cache[chave] = p;
            }
            return p;
        }

        PlantaDoAcampamento(DadosDoMundo d, Acampamento a)
        {
            Lugar = a;
            var r = new System.Random(unchecked(d.Semente * 6151 + a.Indice * 92821 + 3));
            // (a cerca é bem menor que o lugar: 0,8 do raio deixava 72 m de chão vazio no grande)
            float H = Meia = Mathf.Round(a.Raio * 0.6f);
            // o mundo: +z para a frente (o portão)
            _giro = Mathf.Atan2(a.Frente.x, a.Frente.y) * Mathf.Rad2Deg;
            _paraOMundo = Matrix4x4.TRS(new Vector3(a.Centro.x, a.Altura, a.Centro.y), Quaternion.Euler(0f, _giro, 0f), Vector3.one);

            // a casa: no fundo, de frente para o portão
            var M = Medidas.DoJogo;
            Casa = a.Grande ? PlantasDeCasa.Sobrado(r, M, false) : PlantasDeCasa.Terrea(r, M, false);
            PlantasDeCasa.Estilo(r, Casa);
            Casa.portaDaFrenteAberta = true;
            var prof = global::ZombieCityCleaner.Mundo.Casa.Profundidade(Casa, M);
            var lim = global::ZombieCityCleaner.Mundo.Casa.Limites(Casa, M);
            // girada 180°: a frente (o -z da planta) fica em +z; o fundo dela encosta 3 m antes da cerca de trás
            float zCasa = -H + 3f + prof.y;
            PosicaoDaCasa = new Vector3(0f, 0f, zCasa);
            float frenteDaCasa = zCasa - prof.x;   // o z (no acampamento) dos degraus da frente
            float meiaCasa = Mathf.Max(Mathf.Abs(lim.x), Mathf.Abs(lim.y));
            Esconderijo = new Vector3(0f, global::ZombieCityCleaner.Mundo.Casa.PisoDe(Casa), zCasa);
            foreach (var k in Casa.comodos)
            {
                if (k.andar != 0 || (k.x1 - k.x0) * (k.z1 - k.z0) < 4f) continue;
                float cx = (k.x0 + k.x1) / 2f, cz = (k.z0 + k.z1) / 2f;
                if (Casa.espelhar) cx = -cx;
                // a casa girada 180°: o (x, z) da planta vira (-x, -z)
                Esconderijos.Add(new Vector3(-cx, Esconderijo.y, zCasa - cz));
            }
            if (Esconderijos.Count == 0) Esconderijos.Add(Esconderijo);
            Portao = new Vector3(0f, 0f, H + 5f);

            // a cerca: os quatro lados (o -z das peças, o lado do arame farpado, para fora), com o portão (7 m) no meio da
            // frente: ali, dois trechos que vêm dos cantos e terminam nas barreiras de concreto do portão
            Trecho(r, new Vector3(-H, 0f, H), Vector3.right, H - 3.5f, 180f);
            Trecho(r, new Vector3(H, 0f, H), Vector3.left, H - 3.5f, 180f);
            Trecho(r, new Vector3(-H, 0f, -H), Vector3.right, 2f * H, 0f);
            Trecho(r, new Vector3(-H, 0f, -H), Vector3.forward, 2f * H, 90f);
            Trecho(r, new Vector3(H, 0f, -H), Vector3.forward, 2f * H, 270f);
            Cerca.Add(new Peca { tipo = PecaDaCerca.Barreira, pos = new Vector3(-4.3f, 0f, H), giro = 0f });
            Cerca.Add(new Peca { tipo = PecaDaCerca.Barreira, pos = new Vector3(4.3f, 0f, H), giro = 0f });

            // as bancas: em duas filas (esquerda e direita), viradas para o meio, entre a frente da casa e o portão
            int bancas = a.Grande ? 7 : 3;
            var categorias = new List<CategoriaDoComercio>();
            for (int k = 0; k < PrecosDosSobreviventes.Categorias; k++) categorias.Add((CategoriaDoComercio)k);
            for (int k = categorias.Count - 1; k > 0; k--)
            {
                int j = r.Next(k + 1);
                (categorias[k], categorias[j]) = (categorias[j], categorias[k]);
            }
            float xBanca = Mathf.Max(H * 0.6f, meiaCasa + 2.5f);
            float zPrimeira = Mathf.Max(frenteDaCasa + 1f, -H * 0.3f);
            for (int k = 0; k < bancas; k++)
            {
                float lado = k % 2 == 0 ? -1f : 1f;
                var balcao = new Vector3(lado * xBanca, 0f, zPrimeira + (k / 2) * 5f);
                float giro = lado < 0 ? 90f : 270f;   // o balcão olha para o meio
                Bancas.Add(new Banca
                {
                    balcao = balcao, giro = giro, categoria = categorias[k % categorias.Count],
                    comerciante = balcao + new Vector3(lado * 1.1f, 0f, 0f),
                });
            }

            // as fogueiras na praça e as barracas nos cantos de trás
            float zPraca = Mathf.Min(H - 6f, (zPrimeira + H) * 0.5f);
            if (a.Grande)
            {
                Fogueiras.Add(new Vector3(-H * 0.2f, 0f, zPraca));
                Fogueiras.Add(new Vector3(H * 0.2f, 0f, zPraca - 3f));
            }
            else Fogueiras.Add(new Vector3(0f, 0f, zPraca));
            float xBarraca = Mathf.Min(H - 3f, Mathf.Max(meiaCasa + 3f, H * 0.7f));
            Barracas.Add((new Vector3(-xBarraca, 0f, -H * 0.62f), 90f));
            Barracas.Add((new Vector3(xBarraca, 0f, -H * 0.62f), 270f));
            if (a.Grande) Barracas.Add((new Vector3(-xBarraca, 0f, -H * 0.3f), 90f));

            // os guardas: no portão (dentro, de frente para fora) e, no grande, nos cantos da frente e de trás
            Guardas.Add(new Posto { pos = new Vector3(-3.2f, 0f, H - 2f), ronda = new Vector3(-8f, 0f, H - 2f), giro = 0f });
            Guardas.Add(new Posto { pos = new Vector3(3.2f, 0f, H - 2f), ronda = new Vector3(8f, 0f, H - 2f), giro = 0f });
            if (a.Grande)
            {
                Guardas.Add(new Posto { pos = new Vector3(-H + 3f, 0f, -H + 3f), ronda = new Vector3(-H + 3f, 0f, -H * 0.2f), giro = 225f });
                Guardas.Add(new Posto { pos = new Vector3(H - 3f, 0f, -H + 3f), ronda = new Vector3(H - 3f, 0f, -H * 0.2f), giro = 135f });
            }

            // os moradores: em volta das fogueiras
            int moradores = 2 + r.Next(4);
            for (int k = 0; k < moradores; k++)
            {
                var f = Fogueiras[k % Fogueiras.Count];
                float ang = (k * 137f + (float)r.NextDouble() * 40f) * Mathf.Deg2Rad;
                Moradores.Add(f + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 2.3f);
            }

            // as coisas soltas: os tocos em volta das fogueiras, e os barris e as pilhas de caixas encostados na cerca
            foreach (var f in Fogueiras)
                for (int k = 0; k < 4; k++)
                {
                    float ang = k * 90f + 45f + (float)r.NextDouble() * 20f;
                    var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                    Coisas.Add((CoisaSolta.Toco, f + dir * 1.55f, ang + 90f));
                }
            int soltas = a.Grande ? 10 : 6;
            for (int k = 0; k < soltas; k++)
            {
                // num dos lados (sem a frente, onde fica o portão), a 1,2 m da cerca
                int lado = r.Next(3);
                float ao = ((float)r.NextDouble() * 2f - 1f) * (H - 3f);
                var pos = lado == 0 ? new Vector3(-H + 1.2f, 0f, ao) : lado == 1 ? new Vector3(H - 1.2f, 0f, ao) : new Vector3(ao, 0f, -H + 1.2f);
                bool perto = false;
                foreach (var b in Bancas) if ((b.balcao - pos).sqrMagnitude < 9f) perto = true;
                foreach (var (bp, _) in Barracas) if ((bp - pos).sqrMagnitude < 9f) perto = true;
                if (perto) continue;
                Coisas.Add((r.NextDouble() < 0.55 ? CoisaSolta.Barril : CoisaSolta.Caixas, pos, (float)r.NextDouble() * 360f));
            }
        }

        public enum CoisaSolta : byte { Toco, Barril, Caixas }

        /// <summary>As coisas soltas pelo acampamento (o tipo, o lugar e o giro).</summary>
        public readonly List<(CoisaSolta tipo, Vector3 pos, float giro)> Coisas = new List<(CoisaSolta, Vector3, float)>();

        // um trecho da cerca, do canto 'de' na direção 'dir': alambrado, sacos de areia e contêineres sorteados, um atrás
        // do outro; o último (os sacos) pode passar um pouco do fim, para não sobrar buraco
        void Trecho(System.Random r, Vector3 de, Vector3 dir, float comprimento, float giro)
        {
            float s = 0f;
            while (comprimento - s > 0.6f)
            {
                float resta = comprimento - s;
                double sorte = r.NextDouble();
                var tipo = resta < 6.1f || sorte >= 0.8 ? (sorte < 0.45 && resta >= 2.6f ? PecaDaCerca.Alambrado : PecaDaCerca.Sacos)
                         : sorte < 0.45 ? PecaDaCerca.Alambrado : sorte < 0.62 ? PecaDaCerca.Sacos : PecaDaCerca.Conteiner;
                float tamanho = tipo == PecaDaCerca.Conteiner ? 6.1f : 3f;
                // (o último sacos: encostado no fim)
                float meio = tamanho > resta ? comprimento - tamanho * 0.5f : s + tamanho * 0.5f;
                Cerca.Add(new Peca { tipo = tipo, pos = de + dir * meio, giro = giro, variante = r.Next(4) });
                s += tamanho;
            }
        }

        /// <summary>Um ponto do acampamento no mundo (a altura é a do chão aplainado).</summary>
        public Vector3 Mundo(Vector3 local) => _paraOMundo.MultiplyPoint3x4(local);

        /// <summary>Um giro do acampamento no mundo (graus).</summary>
        public float GiroNoMundo(float local) => local + _giro;

        /// <summary>O acampamento no mundo: a posição do meio e o giro.</summary>
        public Matrix4x4 ParaOMundo => _paraOMundo;

        /// <summary>Um ponto do mundo nas coordenadas do acampamento.</summary>
        public Vector3 Local(Vector3 mundo) => _paraOMundo.inverse.MultiplyPoint3x4(mundo);

        /// <summary>O ponto (no mundo) está dentro da cerca.</summary>
        public bool Dentro(Vector3 mundo, float folga = 0f)
        {
            var l = Local(mundo);
            return Mathf.Abs(l.x) < Meia + folga && Mathf.Abs(l.z) < Meia + folga;
        }

        /// <summary>O que a pessoa de índice i é.</summary>
        public PapelDoSobrevivente PapelDe(int i) => i < Guardas.Count ? PapelDoSobrevivente.Guarda
            : i < PrimeiroMorador ? PapelDoSobrevivente.Comerciante : PapelDoSobrevivente.Morador;

        /// <summary>O lugar da pessoa de índice i (nas coordenadas do acampamento) e para onde ela olha.</summary>
        public (Vector3 pos, float giro) LugarDe(int i)
        {
            if (i < Guardas.Count) return (Guardas[i].pos, Guardas[i].giro);
            if (i < PrimeiroMorador)
            {
                var b = Bancas[i - PrimeiroComerciante];
                return (b.comerciante, b.giro);
            }
            int m = i - PrimeiroMorador;
            if (m < 0 || m >= Moradores.Count) return (Vector3.zero, 0f);
            var p = Moradores[m];
            var f = Fogueiras[m % Fogueiras.Count];
            var para = f - p;
            return (p, Mathf.Atan2(para.x, para.z) * Mathf.Rad2Deg);
        }
    }
}
