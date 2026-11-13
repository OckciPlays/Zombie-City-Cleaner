using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O abandono do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 5): as marcas e as placas do forro que
    /// caíram, pela semente e por receitas por tipo de cômodo (nada solto ao acaso).
    ///  - <see cref="marcas"/>: os quadros do atlas das marcas (<see cref="AtlasDasMarcas"/>, o T_Marcas do hospital_marcas.py),
    ///    numa malha à parte (a do shader das marcas: transparente, a cor do vértice tingindo o tom do atlas). O sangue onde a
    ///    história pede (<see cref="SangueDe"/>: muito na emergência, na sala vermelha, no recebimento e no isolamento; um pouco
    ///    nas enfermarias, nos corredores do térreo e do 2º e na espera; nada na administração e no serviço): as poças, os
    ///    arrastos, as pegadas, os respingos e o rastro das macas no chão, as mãos e os respingos com os escorridos nas paredes;
    ///    os furos de tiro onde houve a última resistência (o térreo da entrada e da emergência, o isolamento); em todo cômodo,
    ///    a sujeira junto das paredes, umas manchas de água, as infiltrações do teto (com a água embaixo; mais no último andar,
    ///    embaixo da cobertura) e das paredes de fora, o mofo nos úmidos e no subsolo; o encardido nos corredores; os papéis
    ///    na administração, na recepção e no posto; os cacos de vidro nos de frascos.
    ///    No chão, nada no meio de um móvel (o centro da marca fora da pegada dele); nas paredes, fora das portas e das janelas
    ///    (os furos do acabamento) e a 25 cm das quinas; tudo dentro do cômodo (a marca não atravessa a parede).
    ///  - <see cref="PlacasQueCairam"/>: as placas do forro modular que caíram (nos andares de baixo da cobertura). A laje de
    ///    cima tem o furo só no desenho (a colisão fica inteira), com o vão escuro do entreforro dentro (<see cref="VaoDoForro"/>,
    ///    na casca) e uns fios pendurados; no chão, a placa caída (inteira ou quebrada) ou pendurada pela grade
    ///    (<see cref="PlacasNoChao"/>, no andar).
    /// </summary>
    public sealed partial class Hospital
    {
        /// <summary>As marcas do andar (só no MontarAndar): os quadros, com o UV do atlas e a cor de cada uma.</summary>
        public readonly MalhaDePichacao marcas = new MalhaDePichacao();

        System.Random _rm;
        int _ordem;
        readonly List<Rect> _pegadas = new List<Rect>();
        readonly List<(int lado, float lo, float hi, bool fora, List<Rect> furos)> _corridas = new List<(int, float, float, bool, List<Rect>)>();
        readonly List<(int lado, Rect livre)> _vagas = new List<(int, Rect)>();

        /// <summary>
        /// A cor de cada categoria (no sRGB; no vértice vai a linear, e o shader multiplica pelo tom do atlas, uns 0,55): o
        /// sangue, o sangue velho, a água, o mofo, a sujeira e o reboco dos tiros. Com o tom, o sangue seco fica vinho escuro.
        /// </summary>
        static readonly Color[] Tintas =
        {
            new Color(0.52f, 0.07f, 0.05f), new Color(0.40f, 0.11f, 0.07f), new Color(0.55f, 0.45f, 0.30f),
            new Color(0.33f, 0.39f, 0.25f), new Color(0.47f, 0.41f, 0.33f), new Color(0.86f, 0.85f, 0.81f), Color.white, Color.white,
        };

        static readonly string[] FortesNoChao = { "PocaGrande_A", "PocaGrande_B", "Arrasto", "Pegadas", "Respingos", "Rodinhas", "PocaPequena", "Arrasto" };
        static readonly string[] FracosNoChao = { "PocaPequena", "Pegadas", "Respingos", "Rodinhas", "PocaGrande_B", "PocaPequena" };
        static readonly string[] SangueNaParede = { "Maos", "MaoArrastada", "RespingoNaParede", "Maos" };

        float Sorte() => (float)_rm.NextDouble();
        string Sorteia(string[] l) => l[_rm.Next(l.Length)];

        /// <summary>O quanto de sangue o cômodo tem (0 nenhum, 1 pouco, 2 médio, 3 muito): onde a história pede.</summary>
        static int SangueDe(string tipo, int f)
        {
            switch (tipo)
            {
                case "emergencia": case "vermelha": case "recebimento": case "quarto": case "isolamento": return 3;
                case "triagem": case "observacao": case "uti": case "cirurgia": case "autopsia": case "necroterio": return 2;
                case "enfermaria": case "posto": case "preparo": case "medicacao": case "consultorio": case "camaras": case "garagem":
                case "recepcao": case "espera": case "entrada": case "hall": return 1;
                case "corredor": return f == Terreo ? 2 : f == 2 ? 1 : 0;
                case "banheiro": case "laboratorio": case "raiox": case "coleta": case "seguranca": return f == Terreo ? 1 : 0;
                default: return 0;
            }
        }

        // onde houve tiro: a última resistência no térreo (a entrada, a recepção, a espera, a emergência e os corredores) e no isolamento
        static bool Tiroteio(string tipo, int f) =>
            (f == Terreo && tipo is "recepcao" or "espera" or "entrada" or "emergencia" or "vermelha" or "recebimento" or "seguranca" or "corredor")
            || tipo is "isolamento" or "quarto" or "acesso";

        static bool DePapel(string tipo) =>
            tipo is "escritorio" or "secretaria" or "rh" or "arquivo" or "planejamento" or "recepcao" or "posto" or "diretor" or "seguranca"
                or "reuniao" or "consultorio" or "triagem" or "medicacao";

        static bool Umido(string tipo, int f) => f == 0 || tipo is "banheiro" or "lavanderia" or "limpeza" or "copa" or "residuos";

        // ------------------------------------------------------------ as marcas do andar

        void MarcasDoAndar(Andar an)
        {
            int f = _parte;
            _rm = new System.Random(_semente * 7907 + f * 613 + 11);
            _ordem = 0;
            _pegadas.Clear();
            foreach (var mv in MoveisDoHospital.DoAndar(f))
                if (MedidasDaMobilia.Tem(mv.modelo)) _pegadas.Add(MoveisDoHospital.Pegada(mv.modelo, mv.lugar, mv.giro, mv.escala));
            foreach (var k in an.comodos)
            {
                if (DoNucleo(k) || k.tipo == "escada" || an.fechados.Contains(k)) continue;
                MarcasDoComodo(k, f);
            }
        }

        void MarcasDoComodo(Comodo k, int f)
        {
            ParedesDe(k);
            float area = (k.x1 - k.x0) * (k.z1 - k.z0);
            int s = SangueDe(k.tipo, f);
            bool corredor = k.tipo == "corredor" || k.tipo == "isolamento";
            float teto = (!Anexo(k) ? Teto(f) : (k.tipo == "garagem" ? TopoDaGaragem : TopoDaEntrada) - LajeDoAnexo) - Base(f);

            // o sangue no chão (os compridos ao comprido do cômodo)
            if (s > 0)
            {
                float por = s == 3 ? 11f : s == 2 ? 22f : 50f;
                int n = Mathf.Clamp(Mathf.RoundToInt(area / por * (0.6f + Sorte())), s >= 2 ? 2 : 0, s == 3 ? 11 : 6);
                for (int i = 0; i < n; i++)
                {
                    string nome = Sorteia(s == 3 ? FortesNoChao : FracosNoChao);
                    bool comprido = nome is "Arrasto" or "Pegadas" or "Rodinhas";
                    if (!NoChao(k, nome, comprido || corredor ? AoComprido(k) : Sorte() * 360f, 0.8f + 0.2f * Sorte()))
                        NoChao(k, "PocaPequena", Sorte() * 360f, 0.9f);
                }
            }
            // o sangue nas paredes
            int np = s >= 2 ? s + _rm.Next(s + 1) : s == 1 && Sorte() < 0.45f ? 1 : 0;
            for (int i = 0; i < np; i++)
            {
                string nome = Sorteia(SangueNaParede);
                if (nome == "Maos") NaParede(k, nome, 0.5f, 2.1f, 0.9f);
                else if (nome == "MaoArrastada") NaParede(k, nome, 0.9f, 1.8f, 0.9f);
                else NaParede(k, nome, 0.25f, 2.2f, 0.9f);
            }
            // os tiros
            if (Tiroteio(k.tipo, f))
                for (int i = 0, n = 1 + _rm.Next(corredor ? 3 : 2); i < n; i++) NaParede(k, "Tiros", 0.9f, 1.9f, 0.95f);
            // a sujeira junto das paredes
            for (int i = 0, n = 1 + Mathf.Min(3, (int)(area / 30f)); i < n; i++) NoCanto(k, "SujeiraDoCanto", 0.7f + 0.2f * Sorte());
            // a água no chão
            if (Sorte() < (f == 0 ? 0.7f : 0.35f)) NoChao(k, "Agua", Sorte() * 360f, 0.75f);
            // o teto: a infiltração (com a água pingando embaixo dela) e o mofo nos úmidos
            float pInfiltra = f == Andares - 1 ? 0.75f : f == 0 ? 0.5f : 0.3f;
            if (Sorte() < pInfiltra && NoTeto(k, "InfiltracaoNoTeto", teto, 1f, out var onde)) NoChao(k, "Agua", Sorte() * 360f, 0.7f, onde);
            if (Umido(k.tipo, f) && Sorte() < 0.5f) NoTeto(k, "MofoNoTeto", teto, 0.85f, out _);
            // as paredes: a infiltração descendo (nas de fora; no subsolo, em qualquer uma) e o mofo subindo do chão
            if (Sorte() < (f == Andares - 1 ? 0.6f : 0.3f)) NaParede(k, "InfiltracaoNaParede", teto - 1.8f, teto, 1f, f != 0);
            if (Umido(k.tipo, f) ? Sorte() < 0.8f : Sorte() < 0.15f) NaParede(k, "MofoNaBase", 0f, 0.8f, 0.85f);
            // o encardido onde se passa muito
            if (corredor || k.tipo is "recepcao" or "espera" or "posto" or "hall")
                for (int i = 0, n = 1 + _rm.Next(2); i < n; i++) NaParede(k, "Encardido", 0.7f, 1.5f, 0.6f);
            // os papéis e os cacos
            if (DePapel(k.tipo))
                for (int i = 0, n = 1 + _rm.Next(area > 60f ? 3 : 2); i < n; i++) NoChao(k, "Papeis", Sorte() * 360f, 1f);
            else if (Sorte() < 0.15f) NoChao(k, "Papeis", Sorte() * 360f, 1f);
            if (k.tipo is "laboratorio" or "medicacao" or "coleta" or "consultorio" or "uti" or "vermelha" or "emergencia" or "preparo" && Sorte() < 0.5f)
                NoChao(k, "Cacos", Sorte() * 360f, 1f);
        }

        // ao comprido do cômodo (para um lado ou para o outro), um pouco torto
        float AoComprido(Comodo k) =>
            (k.x1 - k.x0 >= k.z1 - k.z0 ? 0f : 90f) + (Sorte() < 0.5f ? 180f : 0f) + (Sorte() - 0.5f) * 30f;

        Color Tinta(AtlasDasMarcas.Item it, float alfa)
        {
            // o sangue seco é opaco (meio transparente, por cima da parede clara, ele ficava rosado)
            if (it.categoria == AtlasDasMarcas.Categoria.Sangue || it.categoria == AtlasDasMarcas.Categoria.SangueVelho) alfa = Mathf.Max(alfa, 0.97f);
            float v = 0.88f + 0.24f * Sorte();
            Color c = it.Colorida ? new Color(Mathf.Min(1f, v), Mathf.Min(1f, v), Mathf.Min(1f, v)) : Tintas[(int)it.categoria] * v;
            c = c.linear;
            c.a = Mathf.Clamp01(alfa);
            return c;
        }

        // um pouco acima do piso (6 mm) e uma acima da outra (no mesmo plano, piscariam)
        float AlturaNoChao() => Base(_parte) + 0.009f + 0.00025f * (_ordem++ % 16);

        bool DebaixoDeMovel(float x, float z)
        {
            foreach (var r in _pegadas)
                if (x > r.xMin + 0.1f && x < r.xMax - 0.1f && z > r.yMin + 0.1f && z < r.yMax - 0.1f) return true;
            return false;
        }

        /// <summary>Uma marca deitada no chão do cômodo (inteira dentro dele), girada 'giro' graus; em 'onde', o mais perto dali que couber.</summary>
        bool NoChao(Comodo k, string nome, float giro, float alfa, Vector2? onde = null)
        {
            int i = AtlasDasMarcas.Indice(nome);
            if (i < 0) return false;
            var it = AtlasDasMarcas.Itens[i];
            float a = giro * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            float w = it.tamanho.x, h = it.tamanho.y;
            float ex = (Mathf.Abs(c) * w + Mathf.Abs(sn) * h) / 2f, ez = (Mathf.Abs(sn) * w + Mathf.Abs(c) * h) / 2f;
            float folga = m.esp / 2f + 0.06f;
            float xa = k.x0 + folga + ex, xb = k.x1 - folga - ex, za = k.z0 + folga + ez, zb = k.z1 - folga - ez;
            if (xa > xb || za > zb) return false;
            for (int t = 0; t < 16; t++)
            {
                float x = onde.HasValue ? Mathf.Clamp(onde.Value.x, xa, xb) : Mathf.Lerp(xa, xb, Sorte());
                float z = onde.HasValue ? Mathf.Clamp(onde.Value.y, za, zb) : Mathf.Lerp(za, zb, Sorte());
                if (!onde.HasValue && t < 12 && DebaixoDeMovel(x, z)) continue;
                marcas.Quadro(new Vector3(x, AlturaNoChao(), z), new Vector3(c, 0f, sn), new Vector3(-sn, 0f, c), Vector3.up, it.tamanho, it.uv, Tinta(it, alfa));
                return true;
            }
            return false;
        }

        /// <summary>Uma marca no teto do cômodo ('teto' acima do chão), virada para baixo; devolve onde ela ficou.</summary>
        bool NoTeto(Comodo k, string nome, float teto, float alfa, out Vector2 onde)
        {
            onde = default;
            int i = AtlasDasMarcas.Indice(nome);
            if (i < 0) return false;
            var it = AtlasDasMarcas.Itens[i];
            float a = Sorte() * Mathf.PI * 2f, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            float w = it.tamanho.x, h = it.tamanho.y;
            float ex = (Mathf.Abs(c) * w + Mathf.Abs(sn) * h) / 2f, ez = (Mathf.Abs(sn) * w + Mathf.Abs(c) * h) / 2f;
            float folga = m.esp / 2f + 0.06f;
            float xa = k.x0 + folga + ex, xb = k.x1 - folga - ex, za = k.z0 + folga + ez, zb = k.z1 - folga - ez;
            if (xa > xb || za > zb) return false;
            float x = Mathf.Lerp(xa, xb, Sorte()), z = Mathf.Lerp(za, zb, Sorte());
            marcas.Quadro(new Vector3(x, Base(_parte) + teto - 0.006f - 0.0002f * (_ordem++ % 8), z), new Vector3(c, 0f, sn), new Vector3(-sn, 0f, c), Vector3.down, it.tamanho, it.uv, Tinta(it, alfa));
            onde = new Vector2(x, z);
            return true;
        }

        // as paredes do cômodo, em corridas (os trechos seguidos de cada lado juntos), com os furos das portas e das janelas
        void ParedesDe(Comodo k)
        {
            _corridas.Clear();
            for (int lado = 0; lado < 4; lado++)
            {
                if (!_trechos.TryGetValue((k, lado), out var lista)) continue;
                lista.Sort((x, y) => x.lo.CompareTo(y.lo));
                int i = 0;
                while (i < lista.Count)
                {
                    int j = i;
                    while (j + 1 < lista.Count && Mathf.Abs(lista[j + 1].lo - lista[j].hi) < 0.01f) j++;
                    var furos = new List<Rect>();
                    bool fora = true;
                    for (int t = i; t <= j; t++)
                    {
                        if (lista[t].outro != null) fora = false;
                        foreach (var v in lista[t].vaos) furos.Add(FuroDe(v));
                    }
                    _corridas.Add((lado, lista[i].lo, lista[j].hi, fora, furos));
                    i = j + 1;
                }
            }
        }

        // os pedaços livres das paredes (a 'margem' das pontas, entre as alturas y0 e y1) onde cabe uma marca de w × h
        void VagasNaParede(float w, float h, float y0, float y1, float margem, bool soFora, bool noChao)
        {
            _vagas.Clear();
            foreach (var c in _corridas)
            {
                if (soFora && !c.fora) continue;
                _furos.Clear();
                _furos.AddRange(c.furos);
                Cobrir(c.lo + margem, c.hi - margem, y0, y1, _livres);
                foreach (var r in _livres)
                    if (r.width >= w + 0.02f && r.height >= h - 0.001f && (!noChao || r.yMin < 0.001f)) _vagas.Add((c.lado, r));
            }
        }

        /// <summary>Uma marca numa parede do cômodo, inteira entre as alturas y0 e y1, fora das portas e das janelas (com 'soFora', só nas de fora).</summary>
        bool NaParede(Comodo k, string nome, float y0, float y1, float alfa, bool soFora = false)
        {
            int i = AtlasDasMarcas.Indice(nome);
            if (i < 0) return false;
            var it = AtlasDasMarcas.Itens[i];
            float w = it.tamanho.x, h = it.tamanho.y;
            VagasNaParede(w, h, y0, y1, 0.25f, soFora, false);
            if (_vagas.Count == 0) return false;
            var (lado, r) = _vagas[_rm.Next(_vagas.Count)];
            float s = Mathf.Lerp(r.xMin + w / 2f, r.xMax - w / 2f, Sorte());
            float y = Mathf.Lerp(r.yMin + h / 2f, r.yMax - h / 2f, Sorte());
            var p = PlanoDe(k, lado);
            // na frente da pintura das barras (3 mm)
            marcas.Quadro(P(p, s, y, 0.006f + 0.0002f * (_ordem++ % 10)), Leitura(p.n), Vector3.up, p.n, it.tamanho, it.uv, Tinta(it, alfa));
            return true;
        }

        /// <summary>Uma marca deitada no chão junto de uma parede (a linha de cima do desenho encostada nela), fora das portas.</summary>
        bool NoCanto(Comodo k, string nome, float alfa)
        {
            int i = AtlasDasMarcas.Indice(nome);
            if (i < 0) return false;
            var it = AtlasDasMarcas.Itens[i];
            float w = it.tamanho.x, h = it.tamanho.y;
            VagasNaParede(w, 0.3f, 0f, 0.3f, 0.12f, false, true);
            if (_vagas.Count == 0) return false;
            var (lado, r) = _vagas[_rm.Next(_vagas.Count)];
            // o comprimento do cômodo do outro lado (a marca não pode passar dele)
            float fundo = lado == 0 || lado == 2 ? k.z1 - k.z0 : k.x1 - k.x0;
            if (fundo < h + 0.4f) return false;
            float s = Mathf.Lerp(r.xMin + w / 2f, r.xMax - w / 2f, Sorte());
            var p = PlanoDe(k, lado);
            var c = P(p, s, 0f, h / 2f - 0.02f);
            c.y = AlturaNoChao();
            marcas.Quadro(c, p.u, -p.n, Vector3.up, it.tamanho, it.uv, Tinta(it, alfa));
            return true;
        }

        // ------------------------------------------------------------ as placas do forro que caíram

        static readonly Dictionary<int, List<(Rect placa, Comodo k)>> _caidas = new Dictionary<int, List<(Rect, Comodo)>>();

        // o quanto o forro de cada tipo de cômodo sofreu (os da emergência e da internação mais; a administração menos)
        static float DanoDoForro(string tipo) => tipo switch
        {
            "emergencia" or "vermelha" or "uti" or "enfermaria" or "observacao" or "triagem" or "corredor" or "posto" or "isolamento" or "quarto" => 1f,
            "espera" or "recepcao" or "laboratorio" or "consultorio" or "medicacao" or "preparo" => 0.7f,
            _ => 0.4f,
        };

        /// <summary>
        /// As placas do forro modular que caíram no andar f (o retângulo, na grade de 0,6 × 1,2 m, e o cômodo): nos cômodos com
        /// o forro, longe das paredes e das luminárias, só onde o andar de cima tem o piso por cima do furo (no 3º, a laje de
        /// cima é a cobertura: nenhuma). A mesma lista na casca (o furo e o vão) e no andar (a placa no chão).
        /// </summary>
        public static List<(Rect placa, Comodo k)> PlacasQueCairam(int f)
        {
            lock (_caidas)
            {
                if (_caidas.TryGetValue(f, out var pronta)) return pronta;
                var lista = new List<(Rect, Comodo)>();
                _caidas[f] = lista;
                if (f < 0 || f >= Andares - 1) return lista;
                var an = DoAndar(f);
                var cima = DoAndar(f + 1);
                var rnd = new System.Random(4271 + f * 97);
                var luzes = new List<Vector2>();
                foreach (var k in an.comodos)
                {
                    if (DoNucleo(k) || Anexo(k) || an.fechados.Contains(k) || !ComForro(an.AcabamentoDe(k), k.tipo)) continue;
                    float area = (k.x1 - k.x0) * (k.z1 - k.z0);
                    int n = Mathf.FloorToInt(area / 28f * DanoDoForro(k.tipo) * (float)rnd.NextDouble() * 2f + (float)rnd.NextDouble() * 0.6f);
                    if (n <= 0) continue;
                    luzes.Clear();
                    PontosDasLuminarias(k, true, luzes);
                    int i0 = Mathf.CeilToInt((k.x0 + 0.25f - X0) / PlacaDoForroX), i1 = Mathf.FloorToInt((k.x1 - 0.25f - X0) / PlacaDoForroX) - 1;
                    int j0 = Mathf.CeilToInt((k.z0 + 0.25f - Z0) / PlacaDoForroZ), j1 = Mathf.FloorToInt((k.z1 - 0.25f - Z0) / PlacaDoForroZ) - 1;
                    if (i1 < i0 || j1 < j0) continue;
                    for (int t = 0, feitas = 0; t < 30 && feitas < n; t++)
                    {
                        int i = rnd.Next(i0, i1 + 1), j = rnd.Next(j0, j1 + 1);
                        var r = new Rect(X0 + i * PlacaDoForroX, Z0 + j * PlacaDoForroZ, PlacaDoForroX, PlacaDoForroZ);
                        bool ok = true;
                        foreach (var l in luzes) if (Mathf.Abs(l.x - r.center.x) < PlacaDoForroX && Mathf.Abs(l.y - r.center.y) < PlacaDoForroZ) ok = false;
                        foreach (var (o, _) in lista) if (o.Overlaps(new Rect(r.x - 0.05f, r.y - 0.05f, r.width + 0.1f, r.height + 0.1f))) ok = false;
                        // em cima, o piso de um cômodo (não o núcleo, nem perto da parede: o piso começa 10 cm antes dela)
                        var kc = cima.Em(r.center.x, r.center.y);
                        if (kc == null || DoNucleo(kc) || kc.tipo == "escada" || r.xMin < kc.x0 + 0.2f || r.xMax > kc.x1 - 0.2f || r.yMin < kc.z0 + 0.2f || r.yMax > kc.z1 - 0.2f) ok = false;
                        if (!ok) continue;
                        lista.Add((r, k));
                        feitas++;
                    }
                }
                return lista;
            }
        }

        /// <summary>Onde ficam as luminárias de um cômodo (no chão, x e z): em linhas ao longo do lado comprido; com o forro, no meio de uma placa dele.</summary>
        static void PontosDasLuminarias(Comodo k, bool forro, List<Vector2> saida)
        {
            float w = k.x1 - k.x0, d = k.z1 - k.z0;
            bool emX = w >= d;
            float comp = emX ? w : d, larg = emX ? d : w;
            int n = Mathf.Max(1, Mathf.RoundToInt(comp / 8f)), linhas = Mathf.Max(1, Mathf.RoundToInt(larg / 9f));
            for (int i = 0; i < n; i++)
                for (int j = 0; j < linhas; j++)
                {
                    float s = (i + 0.5f) / n, t = (j + 0.5f) / linhas;
                    float x = emX ? Mathf.Lerp(k.x0, k.x1, s) : Mathf.Lerp(k.x0, k.x1, t);
                    float z = emX ? Mathf.Lerp(k.z0, k.z1, t) : Mathf.Lerp(k.z0, k.z1, s);
                    if (forro)
                    {
                        // no meio da placa do forro (a grade de 0,6 m em x e 1,2 m em z, a mesma em todos os cômodos)
                        x = Mathf.Clamp(X0 + PlacaDoForroX * (Mathf.Floor((x - X0) / PlacaDoForroX) + 0.5f), k.x0 + 0.4f, k.x1 - 0.4f);
                        z = Mathf.Clamp(Z0 + PlacaDoForroZ * (Mathf.Floor((z - Z0) / PlacaDoForroZ) + 0.5f), k.z0 + 0.7f, k.z1 - 0.7f);
                    }
                    saida.Add(new Vector2(x, z));
                }
        }

        /// <summary>
        /// O vão do entreforro em cima de uma placa que caiu (na casca, com o furo da laje): as quatro paredes e o fundo, de
        /// concreto escuro, a 45 cm do forro, e às vezes um fio pendurado.
        /// </summary>
        void VaoDoForro(Rect r, float teto)
        {
            const float fundo = 0.45f;
            float y0 = teto, y1 = teto + fundo;
            Vector3 A(float x, float y, float z) => new Vector3(x, y, z);
            void Lado(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                var q = new List<Vector3>(4) { a, b, c, d };
                vis.Face(q, n, "Concreto");
            }
            Lado(A(r.xMin, y0, r.yMin), A(r.xMax, y0, r.yMin), A(r.xMax, y1, r.yMin), A(r.xMin, y1, r.yMin), Vector3.forward);
            Lado(A(r.xMin, y0, r.yMax), A(r.xMax, y0, r.yMax), A(r.xMax, y1, r.yMax), A(r.xMin, y1, r.yMax), Vector3.back);
            Lado(A(r.xMin, y0, r.yMin), A(r.xMin, y0, r.yMax), A(r.xMin, y1, r.yMax), A(r.xMin, y1, r.yMin), Vector3.right);
            Lado(A(r.xMax, y0, r.yMin), A(r.xMax, y0, r.yMax), A(r.xMax, y1, r.yMax), A(r.xMax, y1, r.yMin), Vector3.left);
            Lado(A(r.xMin, y1, r.yMin), A(r.xMax, y1, r.yMin), A(r.xMax, y1, r.yMax), A(r.xMin, y1, r.yMax), Vector3.down);
            // o fio (o da luminária que não foi posta, ou o do alarme), pendurado do fundo
            var rnd = new System.Random(Mathf.RoundToInt(r.x * 100f) * 31 + Mathf.RoundToInt(r.y * 100f));
            if (rnd.NextDouble() < 0.55)
            {
                float comp = 0.35f + (float)rnd.NextDouble() * 0.6f;
                var p = new Vector3(r.xMin + 0.15f + (float)rnd.NextDouble() * 0.3f, y1 - comp / 2f, r.yMin + 0.2f + (float)rnd.NextDouble() * 0.8f);
                Caixa(p, new Vector3(0.012f, comp, 0.012f), "Pneu", (float)rnd.NextDouble() * 90f, RZ(((float)rnd.NextDouble() - 0.5f) * 16f));
            }
        }

        /// <summary>
        /// As placas que caíram no andar (o desenho, sem colisão: são finas): umas penduradas pela grade, caídas de um lado;
        /// as outras no chão, perto do furo, inteiras ou quebradas em duas.
        /// </summary>
        void PlacasNoChao(int f)
        {
            float teto = Teto(f), chao = Base(f);
            foreach (var (r, k) in PlacasQueCairam(f))
            {
                var rnd = new System.Random(Mathf.RoundToInt(r.x * 100f) * 17 + Mathf.RoundToInt(r.y * 100f) * 3 + f);
                if (rnd.NextDouble() < 0.3)
                {
                    // pendurada, presa pela beirada de um dos lados curtos (os de 0,6 m), caída de 55 a 75 graus
                    float ang = 55f + (float)rnd.NextDouble() * 20f;
                    bool deTras = rnd.NextDouble() < 0.5;
                    float a = ang * Mathf.Deg2Rad;
                    float z = deTras ? r.yMax : r.yMin, sinal = deTras ? -1f : 1f;
                    var c = new Vector3(r.center.x, teto - 0.004f - Mathf.Sin(a) * (PlacaDoForroZ - 0.03f) / 2f, z + sinal * Mathf.Cos(a) * (PlacaDoForroZ - 0.03f) / 2f);
                    Caixa(c, new Vector3(PlacaDoForroX - 0.03f, 0.012f, PlacaDoForroZ - 0.03f), "Teto", 0f, RX(deTras ? -ang : ang));
                    continue;
                }
                // no chão, perto do furo (dentro do cômodo)
                float folga = m.esp / 2f + 0.5f;
                float x = Mathf.Clamp(r.center.x + ((float)rnd.NextDouble() - 0.5f) * 1.2f, k.x0 + folga, k.x1 - folga);
                float zc = Mathf.Clamp(r.center.y + ((float)rnd.NextDouble() - 0.5f) * 1.4f, k.z0 + folga, k.z1 - folga);
                float giro = (float)rnd.NextDouble() * 360f;
                if (rnd.NextDouble() < 0.45)
                {
                    // quebrada em duas, uma meio por cima da outra
                    Caixa(new Vector3(x, chao + 0.012f, zc), new Vector3(PlacaDoForroX - 0.03f, 0.012f, 0.62f), "Teto", giro);
                    Caixa(new Vector3(x + 0.25f, chao + 0.026f, zc + 0.3f), new Vector3(PlacaDoForroX - 0.05f, 0.012f, 0.5f), "Teto", giro + 25f, RZ(4f));
                }
                else Caixa(new Vector3(x, chao + 0.012f, zc), new Vector3(PlacaDoForroX - 0.03f, 0.012f, PlacaDoForroZ - 0.03f), "Teto", giro, RX(1.5f));
            }
        }
    }
}
