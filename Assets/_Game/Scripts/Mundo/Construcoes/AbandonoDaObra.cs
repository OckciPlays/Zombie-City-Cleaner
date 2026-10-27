using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O abandono das construções feitas pelo jogo (Plano_Mapa, Parte M9b; primeiro na cena de testes, para o usuário
    /// aprovar antes de ir para o jogo). Roda depois do Montar de uma casa, loja ou prédio, com a intensidade k (0 a 1):
    ///  - <see cref="Janelas"/>: as janelas de fora que ficaram abertas viram, umas, o vidro sujo quebrado (um buraco torto
    ///    no meio, ou inteiro) e, outras, tapadas: tábuas pregadas (em X ou deitadas) ou a chapa de compensado inteira (às
    ///    vezes pichada), mais quanto maior k;
    ///  - <see cref="Hera"/>: a hera numa malha à parte (com o vento do shader da vegetação): ramos subindo do chão pelas
    ///    paredes de fora (passando para as dos andares de cima), desviando das portas e das janelas, e, com k alto,
    ///    caindo do alto das paredes; folhas pequenas e inclinadas para fora, umas vermelhas do outono;
    ///  - <see cref="Pichacoes"/>: as pichações do atlas (<see cref="AtlasDasPichacoes"/>) numa malha à parte
    ///    (<see cref="MalhaDePichacao"/>, a mesma das chapas), nas paredes de fora do térreo: os recados, as marcas, as
    ///    assinaturas e as tags nos pedaços livres, e o X da busca e os avisos perto das portas.
    /// As paredes de fora são as levantadas e, no prédio, as fachadas da casca (<see cref="Obra.paredesDeFora"/>). Tudo
    /// nas coordenadas da obra, ou, nas malhas da hera e das pichações, levado para o bloco (<see cref="MalhaDaHera.Levar"/>):
    /// no mundo (Parte M9c), as casas, as lojas e os prédios de um bloco dividem uma malha de cada.
    /// </summary>
    public static class AbandonoDaObra
    {
        /// <summary>O grau do abandono no mundo: o "muito" da cena de testes (escolhido pelo usuário em 27/10/2026).</summary>
        public const float NoMundo = 1f;
        /// <summary>A casa da família: a família mora embaixo dela e cuida um pouco.</summary>
        public const float NaFamilia = 0.4f;

        /// <summary>As cores das latas (as mesmas do LATAS_DA_PREVIA do cidade_pichacoes.py): vermelho, preto, branco, laranja e azul.</summary>
        static readonly Color[] Latas =
        {
            new Color(0.62f, 0.07f, 0.06f), new Color(0.09f, 0.09f, 0.09f), new Color(0.86f, 0.86f, 0.83f), new Color(0.9f, 0.42f, 0.08f),
            new Color(0.16f, 0.32f, 0.62f),
        };

        static readonly string[] Madeiras = { "MadeiraVelha", "Madeira", "BancoMadeira" };
        static readonly string[] Verdes = { "Hera", "Hera", "FolhaEscura", "Folha", "FolhaClara" };
        static readonly string[] Outono = { "BordoVermelho", "BordoLaranja", "Hera", "BordoVermelho" };

        // ------------------------------------------------------------------ as janelas

        /// <summary>
        /// As janelas de fora (as que não são portas): umas quebradas, umas tapadas; o resto fica aberta. Com 'pichacoes',
        /// umas chapas de compensado ganham uma pichação.
        /// </summary>
        public static void Janelas(Obra obra, float k, int semente, MalhaDePichacao pichacoes = null)
        {
            if (k <= 0.01f) return;
            var rnd = new System.Random(semente * 13 + 5);
            float tabuas = 0.05f + 0.3f * k, quebradas = 0.15f + 0.35f * k;
            _fora ??= new List<Parede>();
            DeFora(obra, _fora);
            foreach (var w in _fora)
            {
                foreach (var ab in w.aberturas)
                {
                    if (ab.EhPorta || obra.Tapada(w, ab)) continue;
                    double r = rnd.NextDouble();
                    if (r < tabuas) Tabuas(obra, w, ab, rnd, k, pichacoes);
                    else if (r < tabuas + quebradas) Vidro(obra, w, ab, rnd, rnd.NextDouble() < 0.45 + 0.45 * k);
                }
            }
        }

        static float Faixa(System.Random rnd, float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        // as paredes de fora da obra acima do chão: as levantadas e as fachadas de prédio (as listas de rascunho são de
        // cada thread: o kit roda em várias)
        [System.ThreadStatic] static List<Parede> _fora, _coluna;

        static void DeFora(Obra obra, List<Parede> l)
        {
            l.Clear();
            foreach (var w in obra.paredes) if (w.ext && w.baseY > -0.5f) l.Add(w);
            foreach (var w in obra.paredesDeFora) if (w.ext && w.baseY > -0.5f) l.Add(w);
        }

        // as paredes na mesma linha de w (os andares de cima e de baixo), de baixo para cima
        static void Coluna(List<Parede> todas, Parede w, List<Parede> coluna)
        {
            coluna.Clear();
            foreach (var c in todas)
            {
                var d = c.a - w.a;
                if (Vector2.Dot(c.u, w.u) < 0.99f || Mathf.Abs(d.x * w.u.y - d.y * w.u.x) > 0.05f) continue;
                float ini = Vector2.Dot(d, w.u);
                if (ini < w.L - 0.1f && ini + c.L > 0.1f) coluna.Add(c);
            }
            coluna.Sort((x, y) => x.baseY.CompareTo(y.baseY));
        }

        // a parede da coluna na altura y (da obra), ou nenhuma (entre as lajes, ou acima de todas)
        static Parede NaAltura(List<Parede> coluna, float y)
        {
            foreach (var c in coluna) if (y >= c.baseY - 0.01f && y < c.baseY + c.altura + 0.01f) return c;
            return null;
        }

        // tapada por fora: a chapa de compensado inteira (às vezes pichada) ou tábuas pregadas, em X ou de 3 a 4 deitadas,
        // um pouco tortas
        static void Tabuas(Obra obra, Parede w, Abertura ab, System.Random rnd, float k, MalhaDePichacao pichacoes)
        {
            float h = ab.alto - ab.baixo, cs = ab.ini + ab.larg / 2, cy = ab.baixo + h / 2;
            float fora = obra.m.esp / 2 + 0.05f;
            double v = rnd.NextDouble();
            if (v < 0.4)
            {
                const float borda = 0.05f, esp = 0.025f;
                obra.Placa(w, ab.ini - borda, ab.ini + ab.larg + borda, ab.baixo - borda, ab.alto + borda, fora, esp, "Compensado");
                if (pichacoes != null && rnd.NextDouble() < 0.4 + 0.4 * k)
                    NaChapa(pichacoes, w, rnd, ab.ini - borda, ab.ini + ab.larg + borda, ab.baixo - borda, ab.alto + borda, fora + esp / 2 + 0.004f);
                return;
            }
            if (v < 0.7)
            {
                float diag = Mathf.Sqrt(ab.larg * ab.larg + h * h) + 0.12f, ang = Mathf.Atan2(h, ab.larg) * Mathf.Rad2Deg;
                obra.Placa(w, cs - diag / 2, cs + diag / 2, cy - 0.09f, cy + 0.09f, fora, 0.03f, Madeiras[rnd.Next(3)], ang);
                obra.Placa(w, cs - diag / 2, cs + diag / 2, cy - 0.09f, cy + 0.09f, fora + 0.03f, 0.03f, Madeiras[rnd.Next(3)], -ang);
            }
            else
            {
                int n = rnd.Next(3, 5);
                for (int i = 0; i < n; i++)
                {
                    float y = ab.baixo + (i + 0.5f) * h / n;
                    obra.Placa(w, ab.ini - 0.12f, ab.ini + ab.larg + 0.12f, y - 0.08f, y + 0.08f, fora, 0.03f, Madeiras[rnd.Next(3)], Faixa(rnd, -8f, 8f));
                }
            }
        }

        // o vidro sujo no meio da parede: inteiro ou com um buraco torto (o anel entre a borda e a estrela do buraco); as
        // listas de rascunho são de cada thread (o kit roda em várias)
        [System.ThreadStatic] static List<(float ang, Vector2 p)> _borda, _buraco;
        [System.ThreadStatic] static List<Vector3> _tri;

        static void Vidro(Obra obra, Parede w, Abertura ab, System.Random rnd, bool buraco)
        {
            _borda ??= new List<(float, Vector2)>();
            _buraco ??= new List<(float, Vector2)>();
            _tri ??= new List<Vector3>(3);
            float s0 = ab.ini + 0.03f, s1 = ab.ini + ab.larg - 0.03f, y0 = ab.baixo + 0.03f, y1 = ab.alto - 0.03f;
            var fora = new Vector3(w.n.x, 0f, w.n.y);
            if (!buraco)
            {
                var q = new List<Vector3> { w.Ponto(s0, y0, 0f), w.Ponto(s1, y0, 0f), w.Ponto(s1, y1, 0f), w.Ponto(s0, y1, 0f) };
                obra.vis.Face(q, fora, "VidroSujo");
                obra.vis.Face(q, -fora, "VidroSujo");
                return;
            }
            float cs = Mathf.Lerp(s0, s1, Faixa(rnd, 0.3f, 0.7f)), cy = Mathf.Lerp(y0, y1, Faixa(rnd, 0.3f, 0.7f));
            float rx = (s1 - s0) * Faixa(rnd, 0.3f, 0.48f), ry = (y1 - y0) * Faixa(rnd, 0.3f, 0.48f);
            _buraco.Clear();
            // de 9 a 11 pontas de raio sorteado, umas entrando fundo (as lascas que ficaram)
            int pontas = rnd.Next(9, 12);
            for (int i = 0; i < pontas; i++)
            {
                float a = i * Mathf.PI * 2f / pontas + Faixa(rnd, -0.25f, 0.25f);
                float r = rnd.NextDouble() < 0.25 ? Faixa(rnd, 0.3f, 0.5f) : Faixa(rnd, 0.6f, 1.08f);
                var pt = new Vector2(Mathf.Clamp(cs + Mathf.Cos(a) * rx * r, s0 + 0.02f, s1 - 0.02f), Mathf.Clamp(cy + Mathf.Sin(a) * ry * r, y0 + 0.02f, y1 - 0.02f));
                _buraco.Add((Mathf.Atan2(pt.y - cy, pt.x - cs), pt));
            }
            // a borda: os 4 cantos e onde o raio de cada ponto do buraco sai do vidro
            _borda.Clear();
            foreach (var c in new[] { new Vector2(s0, y0), new Vector2(s1, y0), new Vector2(s1, y1), new Vector2(s0, y1) })
                _borda.Add((Mathf.Atan2(c.y - cy, c.x - cs), c));
            foreach (var (a, _) in _buraco)
            {
                float dx = Mathf.Cos(a), dy = Mathf.Sin(a), t = float.MaxValue;
                if (dx > 1e-4f) t = Mathf.Min(t, (s1 - cs) / dx);
                if (dx < -1e-4f) t = Mathf.Min(t, (s0 - cs) / dx);
                if (dy > 1e-4f) t = Mathf.Min(t, (y1 - cy) / dy);
                if (dy < -1e-4f) t = Mathf.Min(t, (y0 - cy) / dy);
                _borda.Add((a, new Vector2(cs + dx * t, cy + dy * t)));
            }
            _borda.Sort((x, y) => x.ang.CompareTo(y.ang));
            _buraco.Sort((x, y) => x.ang.CompareTo(y.ang));
            // as duas voltas andando juntas pelo ângulo: um triângulo de cada vez
            int na = _borda.Count, nb = _buraco.Count, ia = 0, ib = 0;
            const float volta = Mathf.PI * 2f;
            while (ia < na || ib < nb)
            {
                float proxA = ia < na ? _borda[(ia + 1) % na].ang + (ia + 1 >= na ? volta : 0f) : float.MaxValue;
                float proxB = ib < nb ? _buraco[(ib + 1) % nb].ang + (ib + 1 >= nb ? volta : 0f) : float.MaxValue;
                if (proxA <= proxB)
                {
                    Tri(obra, w, fora, _borda[ia % na].p, _borda[(ia + 1) % na].p, _buraco[ib % nb].p);
                    ia++;
                }
                else
                {
                    Tri(obra, w, fora, _buraco[ib % nb].p, _borda[ia % na].p, _buraco[(ib + 1) % nb].p);
                    ib++;
                }
            }
        }

        static void Tri(Obra obra, Parede w, Vector3 fora, Vector2 a, Vector2 b, Vector2 c)
        {
            _tri.Clear();
            _tri.Add(w.Ponto(a.x, a.y, 0f));
            _tri.Add(w.Ponto(b.x, b.y, 0f));
            _tri.Add(w.Ponto(c.x, c.y, 0f));
            obra.vis.Face(_tri, fora, "VidroSujo");
            obra.vis.Face(_tri, -fora, "VidroSujo");
        }

        // ------------------------------------------------------------------ as pichações

        // os itens do atlas de cada tipo
        static readonly int[][] PorTipo = ItensPorTipo();

        static int[][] ItensPorTipo()
        {
            var l = new List<int>[5];
            for (int i = 0; i < l.Length; i++) l[i] = new List<int>();
            for (int i = 0; i < AtlasDasPichacoes.Itens.Length; i++) l[(int)AtlasDasPichacoes.Itens[i].tipo].Add(i);
            var r = new int[l.Length][];
            for (int i = 0; i < l.Length; i++) r[i] = l[i].ToArray();
            return r;
        }

        [System.ThreadStatic] static List<Rect> _feitas;

        /// <summary>
        /// As pichações das paredes de fora do térreo (ao alcance de quem está na calçada), mais quanto maior k: perto das
        /// portas, o X da busca ou um aviso; nos pedaços livres (longe das janelas, das portas e umas das outras), os
        /// recados, as marcas, as assinaturas e as tags. Somam na malha 'p' (a mesma das chapas das janelas).
        /// </summary>
        public static void Pichacoes(Obra obra, float k, int semente, MalhaDePichacao p)
        {
            if (k <= 0.01f) return;
            _fora ??= new List<Parede>();
            _feitas ??= new List<Rect>();
            DeFora(obra, _fora);
            var rnd = new System.Random(semente * 11 + 7);
            float baixo = float.MaxValue;
            foreach (var w in _fora) baixo = Mathf.Min(baixo, w.baseY);
            float afast = obra.m.esp / 2 + 0.008f;
            foreach (var w in _fora)
            {
                if (w.baseY > baixo + 0.3f || w.L < 1.5f) continue;
                _feitas.Clear();
                foreach (var ab in w.aberturas)
                    if (ab.EhPorta && rnd.NextDouble() < 0.2 + 0.5 * k)
                        Pichar(p, w, rnd, AtlasDasPichacoes.Tipo.Porta, afast, ab);
                int n = Mathf.RoundToInt(w.L * (0.02f + 0.16f * k) * Faixa(rnd, 0.5f, 1.5f));
                for (int i = 0; i < n; i++)
                {
                    double r = rnd.NextDouble();
                    var tipo = r < 0.4 ? AtlasDasPichacoes.Tipo.Recado : r < 0.55 ? AtlasDasPichacoes.Tipo.Marca
                             : r < 0.77 ? AtlasDasPichacoes.Tipo.Assinatura : AtlasDasPichacoes.Tipo.Tag;
                    Pichar(p, w, rnd, tipo, afast, null);
                }
            }
        }

        // uma pichação do tipo na parede: ao lado da porta (se vier) ou num lugar livre; 8 tentativas, cada uma com uma
        // pichação sorteada (as menores cabem entre as janelas) e as últimas menores
        static void Pichar(MalhaDePichacao p, Parede w, System.Random rnd, AtlasDasPichacoes.Tipo tipo, float afast, Abertura? porta)
        {
            var lista = PorTipo[(int)tipo];
            if (lista.Length == 0) return;
            bool tag = tipo == AtlasDasPichacoes.Tipo.Tag;
            for (int tentativa = 0; tentativa < 8; tentativa++)
            {
                var item = AtlasDasPichacoes.Itens[lista[rnd.Next(lista.Length)]];
                float escala = tag ? Faixa(rnd, 0.9f, 1.2f) : porta != null ? Faixa(rnd, 0.9f, 1.15f) : Faixa(rnd, 1.05f, 1.4f);
                var tam = item.tamanho * escala * (tentativa >= 5 ? 0.8f : 1f);
                float s, y;
                if (porta is Abertura ab)
                {
                    float folga = Faixa(rnd, 0.05f, 0.3f);
                    s = rnd.NextDouble() < 0.5 ? ab.ini - tam.x / 2 - folga : ab.ini + ab.larg + tam.x / 2 + folga;
                    y = Faixa(rnd, 1.0f, 1.5f);
                }
                else
                {
                    float de = tag ? 0.1f : 0.45f, ate = Mathf.Min(w.altura - 0.1f, tag ? 2.3f : 2.8f);
                    if (ate - de < tam.y || w.L - 0.2f < tam.x) continue;
                    s = Faixa(rnd, tam.x / 2 + 0.1f, w.L - tam.x / 2 - 0.1f);
                    y = Faixa(rnd, de + tam.y / 2, ate - tam.y / 2);
                }
                var ret = new Rect(s - tam.x / 2, y - tam.y / 2, tam.x, tam.y);
                if (!Livre(w, ret)) continue;
                _feitas.Add(ret);
                float giro = Faixa(rnd, -1f, 1f) * (tag ? 6f : 3.5f) * Mathf.Deg2Rad;
                var u3 = new Vector3(w.u.x, 0f, w.u.y);
                var lado = u3 * Mathf.Cos(giro) + Vector3.up * Mathf.Sin(giro);
                var cima = Vector3.up * Mathf.Cos(giro) - u3 * Mathf.Sin(giro);
                var lata = item.Colorida ? Color.white
                         : item.nome.StartsWith("Busca") ? Latas[rnd.NextDouble() < 0.6 ? 3 : 0]
                         : Latas[rnd.Next(Latas.Length)];
                var cor = lata.linear;
                cor.a = Faixa(rnd, 0.8f, 1f);
                // cada uma um pouco mais para fora que a anterior (onde a névoa de uma encosta na outra)
                p.Quadro(w.Ponto(s, y, afast + _feitas.Count * 0.0015f), lado, cima, new Vector3(w.n.x, 0f, w.n.y), tam, item.uv, cor);
                return;
            }
        }

        // uma pichação na chapa (de s0 a s1 e de y0 a y1 da parede), inteira dentro dela (a névoa e os escorridos também:
        // fora da chapa, ficariam no ar, na frente do buraco da janela)
        static void NaChapa(MalhaDePichacao p, Parede w, System.Random rnd, float s0, float s1, float y0, float y1, float fora)
        {
            double r = rnd.NextDouble();
            var tipo = r < 0.35 ? AtlasDasPichacoes.Tipo.Porta : r < 0.75 ? AtlasDasPichacoes.Tipo.Recado : r < 0.88 ? AtlasDasPichacoes.Tipo.Marca : AtlasDasPichacoes.Tipo.Tag;
            var lista = PorTipo[(int)tipo];
            if (lista.Length == 0) return;
            var item = AtlasDasPichacoes.Itens[lista[rnd.Next(lista.Length)]];
            float larg = s1 - s0, alt = y1 - y0;
            float escala = Mathf.Min(1.4f, Mathf.Min(larg * 0.94f / item.tamanho.x, alt * 0.94f / item.tamanho.y));
            if (escala < 0.45f) return;
            var tam = item.tamanho * escala;
            float s = (s0 + s1) / 2 + Faixa(rnd, -0.5f, 0.5f) * (larg * 0.94f - tam.x);
            float y = (y0 + y1) / 2 + Faixa(rnd, -0.5f, 0.5f) * (alt * 0.94f - tam.y);
            var lata = item.Colorida ? Color.white : item.nome.StartsWith("Busca") ? Latas[3] : Latas[rnd.Next(Latas.Length)];
            var cor = lata.linear;
            cor.a = Faixa(rnd, 0.85f, 1f);
            p.Quadro(w.Ponto(s, y, fora), new Vector3(w.u.x, 0f, w.u.y), Vector3.up, new Vector3(w.n.x, 0f, w.n.y), tam, item.uv, cor);
        }

        // o retângulo (s, y da parede) longe das aberturas e das outras pichações (essas podem encostar um pouco)
        static bool Livre(Parede w, Rect r)
        {
            if (r.xMin < 0.02f || r.xMax > w.L - 0.02f || r.yMin < 0.02f || r.yMax > w.altura) return false;
            foreach (var ab in w.aberturas)
                if (r.Overlaps(new Rect(ab.ini - 0.08f, ab.baixo - 0.08f, ab.larg + 0.16f, ab.alto - ab.baixo + 0.16f))) return false;
            foreach (var f in _feitas)
                if (r.Overlaps(new Rect(f.x + f.width * 0.1f, f.y + f.height * 0.15f, f.width * 0.8f, f.height * 0.7f))) return false;
            return true;
        }

        // ------------------------------------------------------------------ a hera

        /// <summary>
        /// A hera das paredes de fora: os ramos subindo do chão (das paredes do térreo, passando para as de cima na mesma
        /// linha, até 'alturaMax' acima do chão delas) e, com k acima de 0,45, caindo do alto das paredes de cima. Devolve a
        /// malha (vazia com k perto de 0).
        /// </summary>
        public static MalhaDaHera Hera(Obra obra, float k, int semente, float alturaMax = 7f)
        {
            var h = new MalhaDaHera();
            Hera(obra, k, semente, alturaMax, h);
            return h;
        }

        /// <summary>A hera da obra, somada na malha h (levada para onde ela pedir).</summary>
        public static void Hera(Obra obra, float k, int semente, float alturaMax, MalhaDaHera h)
        {
            if (k <= 0.01f) return;
            _fora ??= new List<Parede>();
            _coluna ??= new List<Parede>();
            DeFora(obra, _fora);
            var rnd = new System.Random(semente * 7 + 3);
            float baixo = float.MaxValue, alto = float.MinValue;
            foreach (var w in _fora)
            {
                baixo = Mathf.Min(baixo, w.baseY);
                alto = Mathf.Max(alto, w.baseY + w.altura);
            }
            float afast = obra.m.esp / 2;
            foreach (var w in _fora)
            {
                if (w.L < 1f) continue;
                if (w.baseY < baixo + 0.3f)
                {
                    Coluna(_fora, w, _coluna);
                    int sobem = Mathf.RoundToInt(w.L * (0.06f + 0.6f * k * k) * Faixa(rnd, 0.5f, 1.5f));
                    for (int i = 0; i < sobem; i++) Ramo(h, _coluna, w, rnd, k, afast, true, alturaMax);
                }
                if (k > 0.45f && w.baseY + w.altura > alto - 0.3f)
                {
                    Coluna(_fora, w, _coluna);
                    int caem = Mathf.RoundToInt(w.L * 0.14f * (k - 0.4f) * Faixa(rnd, 0.5f, 1.5f));
                    for (int i = 0; i < caem; i++) Ramo(h, _coluna, w, rnd, k, afast, false, alturaMax);
                }
            }
        }

        static bool NaAbertura(Parede w, float s, float y, float folga)
        {
            foreach (var ab in w.aberturas)
                if (s > ab.ini - folga && s < ab.ini + ab.larg + folga && y > ab.baixo - folga && y < ab.alto + folga) return true;
            return false;
        }

        // um ramo na parede w (s e y nas coordenadas dela; y passa do alto dela para as de cima da coluna)
        static void Ramo(MalhaDaHera h, List<Parede> coluna, Parede w, System.Random rnd, float k, float afast, bool subindo, float alturaMax)
        {
            float s = Faixa(rnd, 0.25f, w.L - 0.25f);
            if (subindo && NaAbertura(w, s, 0.1f, 0.25f)) return;
            var ultima = coluna[coluna.Count - 1];
            float topo = ultima.baseY + ultima.altura - w.baseY, chao = coluna[0].baseY - w.baseY;
            float alcance = subindo
                ? Mathf.Lerp(1.0f, Mathf.Min(topo + 0.3f, alturaMax), Mathf.Clamp01(Mathf.Pow((float)rnd.NextDouble(), 0.6f) * k + 0.15f))
                : Faixa(rnd, 0.6f, 1.2f + 2.2f * k);
            float largura = Faixa(rnd, 0.25f, 0.6f) * (0.7f + 0.8f * k);
            var cores = rnd.NextDouble() < 0.15 ? Outono : Verdes;
            float fase = (float)rnd.NextDouble();
            var lado = new Vector3(w.u.x, 0f, w.u.y);
            var fora = new Vector3(w.n.x, 0f, w.n.y);
            for (float d = 0f; d < alcance; d += 0.11f)
            {
                float y = subindo ? d : w.altura - d;
                s += Faixa(rnd, -0.045f, 0.045f);
                // desvia das janelas e das portas (da parede da coluna nesta altura) pelo lado mais perto
                var c = NaAltura(coluna, w.baseY + y);
                if (c != null)
                {
                    float off = Vector2.Dot(w.a - c.a, c.u), yc = w.baseY + y - c.baseY;
                    foreach (var ab in c.aberturas)
                    {
                        float sc = s + off;
                        if (sc > ab.ini - 0.06f && sc < ab.ini + ab.larg + 0.06f && yc > ab.baixo - 0.08f && yc < ab.alto + 0.08f)
                            s = (sc - ab.ini < ab.ini + ab.larg - sc ? ab.ini - 0.08f : ab.ini + ab.larg + 0.08f) - off;
                    }
                }
                s = Mathf.Clamp(s, 0.1f, w.L - 0.1f);
                float u = d / alcance;
                float larg = largura * (subindo ? Mathf.Lerp(1f, 0.4f, u) : Mathf.Lerp(0.6f, 0.25f, u));
                int n = 2 + (rnd.NextDouble() < k ? 1 : 0) + (k > 0.6f && rnd.NextDouble() < 0.5 ? 1 : 0);
                for (int i = 0; i < n; i++)
                {
                    float sf = s + Faixa(rnd, -larg / 2, larg / 2), yf = y + Faixa(rnd, -0.05f, 0.05f);
                    float tam = Faixa(rnd, 0.14f, 0.24f), giro = Faixa(rnd, -0.7f, 0.7f), incl = Faixa(rnd, 0.3f, 0.9f);
                    float afasta = afast + Faixa(rnd, 0.02f, 0.07f);
                    string cor = cores[rnd.Next(cores.Length)];
                    if (yf < chao + 0.03f) continue;
                    var cf = NaAltura(coluna, w.baseY + yf);
                    if (cf != null && NaAbertura(cf, sf + Vector2.Dot(w.a - cf.a, cf.u), w.baseY + yf - cf.baseY, 0.02f)) continue;
                    // a folha inclinada para fora (a ponta sai da parede) e girada no plano dela
                    var cima = Vector3.up * Mathf.Cos(giro) + lado * Mathf.Sin(giro);
                    var ladoDaFolha = lado * Mathf.Cos(giro) - Vector3.up * Mathf.Sin(giro);
                    var ponta = cima * Mathf.Cos(incl) + fora * Mathf.Sin(incl);
                    h.Folha(w.Ponto(sf, yf, afasta), ladoDaFolha, ponta, fora, tam, cor, fase);
                }
            }
        }
    }

    /// <summary>
    /// Uma malha de folhas com a cor do vértice (o vento do shader da vegetação: R = 0, sem balanço; G = a fase do ramo;
    /// B = 1, o tremor): a hera. Cada folha é um losango de 2 triângulos, com a cor dela da paleta. No mundo (Parte M9c),
    /// uma por bloco, do estoque (<see cref="Emprestar"/>), com as folhas de cada obra levadas para o lugar dela (<see cref="Levar"/>).
    /// </summary>
    public sealed class MalhaDaHera
    {
        readonly List<Vector3> _v = new List<Vector3>(), _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<Color32> _c = new List<Color32>();
        readonly List<int> _t = new List<int>();
        Matrix4x4 _m = Matrix4x4.identity;
        bool _espelhar;

        static readonly Stack<MalhaDaHera> Estoque = new Stack<MalhaDaHera>();

        public int Folhas => _t.Count / 6;

        /// <summary>Uma malha vazia do estoque (ou nova); quem pega devolve com <see cref="Devolver"/>.</summary>
        public static MalhaDaHera Emprestar()
        {
            lock (Estoque)
                if (Estoque.Count > 0) return Estoque.Pop();
            return new MalhaDaHera();
        }

        public static void Devolver(MalhaDaHera h)
        {
            if (h == null) return;
            h._v.Clear(); h._n.Clear(); h._uv.Clear(); h._c.Clear(); h._t.Clear();
            h.Levar(Matrix4x4.identity, false);
            lock (Estoque)
                if (Estoque.Count < 24) Estoque.Push(h);
        }

        /// <summary>As próximas folhas vão espelhadas em x (se pedir) e levadas por m (a posição e o giro da obra no bloco).</summary>
        public void Levar(Matrix4x4 m, bool espelhar)
        {
            _m = m;
            _espelhar = espelhar;
        }

        /// <summary>Uma folha no centro c: 'lado' e 'ponta' no plano dela (a ponta para cima e para fora), virada para 'fora'.</summary>
        public void Folha(Vector3 c, Vector3 lado, Vector3 ponta, Vector3 fora, float tam, string cor, float fase)
        {
            if (_espelhar) { c.x = -c.x; lado.x = -lado.x; ponta.x = -ponta.x; fora.x = -fora.x; }
            c = _m.MultiplyPoint3x4(c);
            lado = _m.MultiplyVector(lado);
            ponta = _m.MultiplyVector(ponta);
            fora = _m.MultiplyVector(fora);
            Vector3 b = c - ponta * (tam * 0.45f), e = c - lado * (tam * 0.42f), p = c + ponta * (tam * 0.6f), d = c + lado * (tam * 0.42f);
            var n = Vector3.Cross(d - b, p - b).normalized;
            bool inverter = Vector3.Dot(n, fora) < 0f;
            if (inverter) n = -n;
            var uv = Paleta.UV(cor);
            var vento = new Color32(0, (byte)Mathf.RoundToInt(fase * 255f), 255, 255);
            int i0 = _v.Count;
            _v.Add(b); _v.Add(d); _v.Add(p); _v.Add(e);
            for (int i = 0; i < 4; i++) { _n.Add(n); _uv.Add(uv); _c.Add(vento); }
            if (!inverter) { _t.Add(i0); _t.Add(i0 + 1); _t.Add(i0 + 2); _t.Add(i0); _t.Add(i0 + 2); _t.Add(i0 + 3); }
            else { _t.Add(i0); _t.Add(i0 + 2); _t.Add(i0 + 1); _t.Add(i0); _t.Add(i0 + 3); _t.Add(i0 + 2); }
        }

        public Mesh ParaMesh(string nome, bool espelhar = false)
        {
            var m = new Mesh { name = nome };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            if (!espelhar)
            {
                m.SetVertices(_v); m.SetNormals(_n); m.SetUVs(0, _uv); m.SetColors(_c); m.SetTriangles(_t, 0);
            }
            else
            {
                var v = new List<Vector3>(_v.Count);
                var n = new List<Vector3>(_n.Count);
                foreach (var p in _v) v.Add(new Vector3(-p.x, p.y, p.z));
                foreach (var p in _n) n.Add(new Vector3(-p.x, p.y, p.z));
                var t = new List<int>(_t.Count);
                for (int i = 0; i < _t.Count; i += 3) { t.Add(_t[i]); t.Add(_t[i + 2]); t.Add(_t[i + 1]); }
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, _uv); m.SetColors(_c); m.SetTriangles(t, 0);
            }
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>
    /// Uma malha de pichações: um quadro por pichação, com o UV do retângulo dela no atlas e a cor da lata no vértice (o
    /// alfa: o quanto da tinta ainda está lá). Espelhada, o texto é desespelhado (o u volta dentro do retângulo).
    /// </summary>
    public sealed class MalhaDePichacao
    {
        readonly List<Vector3> _v = new List<Vector3>(), _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<float> _somaDoU = new List<float>();
        readonly List<Color> _c = new List<Color>();
        readonly List<int> _t = new List<int>();
        Matrix4x4 _m = Matrix4x4.identity;
        bool _espelhar;

        static readonly Stack<MalhaDePichacao> Estoque = new Stack<MalhaDePichacao>();

        public int Quadros => _v.Count / 4;

        /// <summary>Uma malha vazia do estoque (ou nova); quem pega devolve com <see cref="Devolver"/>.</summary>
        public static MalhaDePichacao Emprestar()
        {
            lock (Estoque)
                if (Estoque.Count > 0) return Estoque.Pop();
            return new MalhaDePichacao();
        }

        public static void Devolver(MalhaDePichacao p)
        {
            if (p == null) return;
            p._v.Clear(); p._n.Clear(); p._uv.Clear(); p._somaDoU.Clear(); p._c.Clear(); p._t.Clear();
            p.Levar(Matrix4x4.identity, false);
            lock (Estoque)
                if (Estoque.Count < 24) Estoque.Push(p);
        }

        /// <summary>
        /// Os próximos quadros vão espelhados em x (se pedir; o texto continua de frente: o lado é desvirado) e levados por m
        /// (a posição e o giro da obra no bloco).
        /// </summary>
        public void Levar(Matrix4x4 m, bool espelhar)
        {
            _m = m;
            _espelhar = espelhar;
        }

        /// <summary>Um quadro no centro c: 'lado' (a direita de quem olha de fora) e 'cima' no plano da parede, virado para 'fora'.</summary>
        public void Quadro(Vector3 c, Vector3 lado, Vector3 cima, Vector3 fora, Vector2 tamanho, Rect uv, Color cor)
        {
            if (_espelhar)
            {
                c.x = -c.x; cima.x = -cima.x; fora.x = -fora.x;
                // espelhado, o lado apontaria para a esquerda de quem olha de fora (o texto sairia de trás para a frente)
                lado = new Vector3(lado.x, -lado.y, -lado.z);
            }
            c = _m.MultiplyPoint3x4(c);
            lado = _m.MultiplyVector(lado);
            cima = _m.MultiplyVector(cima);
            fora = _m.MultiplyVector(fora);
            Vector3 a = c - lado * (tamanho.x / 2) - cima * (tamanho.y / 2), b = a + lado * tamanho.x, d = a + cima * tamanho.y, e = b + cima * tamanho.y;
            int i0 = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(e); _v.Add(d);
            _uv.Add(new Vector2(uv.xMin, uv.yMin)); _uv.Add(new Vector2(uv.xMax, uv.yMin));
            _uv.Add(new Vector2(uv.xMax, uv.yMax)); _uv.Add(new Vector2(uv.xMin, uv.yMax));
            for (int i = 0; i < 4; i++) { _n.Add(fora); _c.Add(cor); _somaDoU.Add(uv.xMin + uv.xMax); }
            if (Vector3.Dot(Vector3.Cross(b - a, e - a), fora) >= 0f) { _t.Add(i0); _t.Add(i0 + 1); _t.Add(i0 + 2); _t.Add(i0); _t.Add(i0 + 2); _t.Add(i0 + 3); }
            else { _t.Add(i0); _t.Add(i0 + 2); _t.Add(i0 + 1); _t.Add(i0); _t.Add(i0 + 3); _t.Add(i0 + 2); }
        }

        public Mesh ParaMesh(string nome, bool espelhar = false)
        {
            var m = new Mesh { name = nome };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            if (!espelhar)
            {
                m.SetVertices(_v); m.SetNormals(_n); m.SetUVs(0, _uv); m.SetColors(_c); m.SetTriangles(_t, 0);
            }
            else
            {
                var v = new List<Vector3>(_v.Count);
                var n = new List<Vector3>(_n.Count);
                var uv = new List<Vector2>(_uv.Count);
                foreach (var p in _v) v.Add(new Vector3(-p.x, p.y, p.z));
                foreach (var p in _n) n.Add(new Vector3(-p.x, p.y, p.z));
                for (int i = 0; i < _uv.Count; i++) uv.Add(new Vector2(_somaDoU[i] - _uv[i].x, _uv[i].y));
                var t = new List<int>(_t.Count);
                for (int i = 0; i < _t.Count; i += 3) { t.Add(_t[i]); t.Add(_t[i + 2]); t.Add(_t[i + 1]); }
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetColors(_c); m.SetTriangles(t, 0);
            }
            m.RecalculateBounds();
            return m;
        }
    }
}
