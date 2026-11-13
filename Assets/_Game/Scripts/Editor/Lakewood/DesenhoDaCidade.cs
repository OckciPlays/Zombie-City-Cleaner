using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O desenho das ruas e dos lotes de uma cidade do Lakewood (Plano_Lakewood, Etapa 6), no Editor: as ruas novas (retas
    /// entre os pontos, com as pontas ligadas na estrada ou na rua mais perto), os lotes especiais (um lugar escolhido, de
    /// frente para uma rua) e os lotes enchidos ao longo de uma rua (dos dois lados, um depois do outro, onde cabem: fora
    /// das ruas e dos outros lotes, dentro da cidade, com pouco desnível e longe da água e dos rios). O que sai vai para
    /// <see cref="MapaDeLakewood.ruas"/> e <see cref="MapaDeLakewood.lotes"/> (<see cref="Gravar"/>).
    /// </summary>
    public sealed class DesenhoDaCidade
    {
        sealed class Linha
        {
            public string Nome;
            public List<Vector2> Pontos;
            public float Meia;
            public bool Fechada;
            public RuaDaCidade Rua;   // as novas
        }

        readonly MapaDeLakewood _mapa;
        readonly ChaoDeLakewood _chao;
        readonly string _cidade;
        readonly List<Vector2> _contorno;
        readonly List<Linha> _linhas = new List<Linha>();
        public readonly List<RuaDaCidade> Ruas = new List<RuaDaCidade>();
        public readonly List<LoteDoMapa> Lotes = new List<LoteDoMapa>();
        readonly StringBuilder _relatorio = new StringBuilder();

        /// <summary>O que aconteceu com os especiais (o lugar e o desnível de cada um) e os avisos.</summary>
        public string Relatorio => _relatorio.ToString();

        /// <summary>
        /// O chão do mapa sem as ruas e os lotes das cidades (as ruas novas entram na rede e mudam os pontos das estradas, e
        /// os patamares mudam o chão: o desenho tem de sair igual toda vez).
        /// </summary>
        public static ChaoDeLakewood ChaoSemAsCidades(MapaDeLakewood mapa)
        {
            var copia = mapa.Copia();
            copia.ruas = new List<RuaDaCidade>();
            copia.lotes = new List<LoteDoMapa>();
            return new ChaoDeLakewood(copia, new RelevoDeLakewood(copia));
        }

        public DesenhoDaCidade(MapaDeLakewood mapa, ChaoDeLakewood chao, string cidade)
        {
            _mapa = mapa;
            _chao = chao;
            _cidade = cidade;
            var c = mapa.cidades.Find(x => x.nome == cidade);
            if (c == null) throw new System.ArgumentException($"a cidade {cidade} não está no mapa");
            _contorno = MapaDeLakewood.Contorno(c.contorno);
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var p in _contorno)
            {
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                z0 = Mathf.Min(z0, p.y); z1 = Mathf.Max(z1, p.y);
            }
            var caixa = Rect.MinMaxRect(x0 - 200f, z0 - 200f, x1 + 200f, z1 + 200f);
            // as estradas da Etapa 5 perto da cidade, em trechos seguidos (uma que sai e volta não liga as pontas)
            foreach (var e in chao.Estradas)
            {
                // as ruas das cidades que já estão no mapa saem (o desenho faz as dele)
                if (e.Via.tipo == TipoDeVia.Rua) continue;
                List<Vector2> trecho = null;
                for (int i = 0; i < e.Pontos.Length; i++)
                {
                    if (caixa.Contains(e.Pontos[i]))
                    {
                        trecho ??= new List<Vector2>();
                        trecho.Add(e.Pontos[i]);
                        continue;
                    }
                    if (trecho != null && trecho.Count > 1) _linhas.Add(new Linha { Nome = e.Via.nome, Pontos = trecho, Meia = e.MeiaLargura });
                    trecho = null;
                }
                if (trecho != null && trecho.Count > 1) _linhas.Add(new Linha { Nome = e.Via.nome, Pontos = trecho, Meia = e.MeiaLargura });
            }
        }

        // ------------------------------------------------------------------ as ruas

        /// <summary>Uma rua nova (os pontos em pares x, z).</summary>
        public RuaDaCidade Rua(string nome, float largura, params float[] xz) => NovaRua(nome, largura, false, xz);

        /// <summary>Uma rua em alça (a última ponta volta para a primeira).</summary>
        public RuaDaCidade Alca(string nome, float largura, params float[] xz) => NovaRua(nome, largura, true, xz);

        RuaDaCidade NovaRua(string nome, float largura, bool fechada, float[] xz)
        {
            var r = new RuaDaCidade { nome = nome, cidade = _cidade, largura = largura, fechada = fechada };
            for (int i = 0; i + 1 < xz.Length; i += 2) r.pontos.Add(new Vector2(xz[i], xz[i + 1]));
            Ruas.Add(r);
            _linhas.Add(new Linha { Nome = nome, Rua = r, Meia = r.MeiaLargura, Fechada = fechada, Pontos = Densa(r.pontos, fechada) });
            return r;
        }

        // os pontos de 4 em 4 m (para encher os lotes e medir a distância)
        static List<Vector2> Densa(List<Vector2> pontos, bool fechada)
        {
            var d = new List<Vector2>();
            int n = pontos.Count, lados = fechada ? n : n - 1;
            for (int i = 0; i < lados; i++)
            {
                var a = pontos[i];
                var b = pontos[(i + 1) % n];
                int passos = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 4f));
                for (int k = 0; k < passos; k++) d.Add(Vector2.Lerp(a, b, k / (float)passos));
            }
            d.Add(fechada ? pontos[0] : pontos[n - 1]);
            return d;
        }

        /// <summary>
        /// As pontas das ruas novas (não as alças) que ficam a menos de "alcance" de outra linha (uma estrada ou outra rua)
        /// vão para cima da linha do meio dela: o cruzamento fica de verdade.
        /// </summary>
        public void LigarAsPontas(float alcance = 15f)
        {
            foreach (var l in _linhas)
            {
                if (l.Rua == null || l.Fechada) continue;
                var pts = l.Rua.pontos;
                for (int ponta = 0; ponta < 2; ponta++)
                {
                    int k = ponta == 0 ? 0 : pts.Count - 1;
                    float melhor = alcance;
                    Vector2 alvo = pts[k];
                    string em = null;
                    foreach (var o in _linhas)
                    {
                        if (o == l) continue;
                        var op = o.Rua != null ? o.Rua.pontos : o.Pontos;
                        int lados = o.Fechada ? op.Count : op.Count - 1;
                        for (int s = 0; s < lados; s++)
                        {
                            var a = op[s];
                            var b = op[(s + 1) % op.Count];
                            float d = Linhas.DistanciaDoSegmento(pts[k], a, b, out float t);
                            if (d >= melhor) continue;
                            melhor = d;
                            alvo = Vector2.Lerp(a, b, t);
                            em = o.Nome;
                        }
                    }
                    if (em != null && melhor > 0.01f) pts[k] = alvo;
                }
                l.Pontos = Densa(pts, false);
            }
        }

        // as linhas com o nome (uma estrada que sai da caixa e volta vem em mais de um trecho)
        List<Linha> LinhasDe(string nome)
        {
            var l = _linhas.FindAll(x => x.Nome == nome);
            if (l.Count == 0) throw new System.ArgumentException($"a rua {nome} não existe perto da {_cidade}");
            return l;
        }

        /// <summary>A distância do ponto até a beira da rua ou da estrada mais perto (negativa: em cima dela).</summary>
        float DistanciaDasRuas(Vector2 p)
        {
            float melhor = float.MaxValue;
            foreach (var l in _linhas)
            {
                int lados = l.Pontos.Count - 1;
                for (int i = 0; i < lados; i++)
                    melhor = Mathf.Min(melhor, Linhas.DistanciaDoSegmento(p, l.Pontos[i], l.Pontos[i + 1], out _) - l.Meia);
            }
            return melhor;
        }

        // ------------------------------------------------------------------ os lotes

        // se o lote (com a largura a mais, a folga entre um e outro) fica fora das ruas, dos outros lotes e dentro da cidade
        bool Cabe(LoteDoMapa l, float aMais, float folgaDaRua)
        {
            foreach (var canto in l.Cantos())
                if (!Linhas.Dentro(canto, _contorno)) return false;
            float largura = l.largura + aMais;
            for (float a = -0.5f; a <= 0.501f; a += 0.125f)
            for (float b = -0.5f; b <= 0.501f; b += 0.125f)
                if (DistanciaDasRuas(l.NoMapa(a * largura, b * l.fundo)) < folgaDaRua) return false;
            var largo = new LoteDoMapa { meio = l.meio, giro = l.giro, largura = largura, fundo = l.fundo };
            foreach (var o in Lotes)
                if (LoteDoMapa.Encostam(largo, o, 1f)) return false;
            return true;
        }

        // o lote de frente para a linha, no ponto i dela, do lado s (1 à esquerda de quem anda do primeiro ponto para o último)
        static LoteDoMapa NoPonto(Linha r, int i, int s, float largura, float fundo, float recuo)
        {
            int a = Mathf.Max(0, i - 1), b = Mathf.Min(r.Pontos.Count - 1, i + 1);
            var t = (r.Pontos[b] - r.Pontos[a]).normalized;
            var n = new Vector2(-t.y, t.x) * s;
            return new LoteDoMapa
            {
                meio = r.Pontos[i] + n * (r.Meia + recuo + fundo * 0.5f),
                giro = LoteDoMapa.GiroPara(-n),
                largura = largura,
                fundo = fundo,
                rua = r.Nome,
            };
        }

        /// <summary>
        /// Um lote especial: na rua dada, no ponto dela mais perto de (x, z), do lado dado (1 à esquerda de quem anda do
        /// primeiro ponto para o último, -1 à direita), com a frente a "recuo" da beira da rua. Ele fica mesmo se não couber
        /// (o relatório avisa).
        /// </summary>
        public LoteDoMapa Especial(UsoDoLote tipo, string nome, string rua, float x, float z, int lado, float largura, float fundo, float recuo)
        {
            Linha r = null;
            int melhorI = 1;
            float melhor = float.MaxValue;
            foreach (var candidata in LinhasDe(rua))
                for (int i = 1; i < candidata.Pontos.Count - 1; i++)
                {
                    float d = Vector2.Distance(candidata.Pontos[i], new Vector2(x, z));
                    if (d >= melhor) continue;
                    melhor = d;
                    melhorI = i;
                    r = candidata;
                }
            var l = NoPonto(r, melhorI, lado, largura, fundo, recuo);
            l.tipo = tipo;
            l.nome = nome;
            l.cidade = _cidade;
            bool cabe = Cabe(l, 0f, 1.5f);
            float desnivel = LotesDeLakewood.Desnivel(_chao, l, out bool agua);
            Lotes.Add(l);
            _relatorio.Append($"\n  {l.Descricao}: ({l.meio.x:F0}; {l.meio.y:F0}), {largura:F0} × {fundo:F0} m, de frente para a {rua}, desnível de {desnivel:F1} m");
            if (!cabe) _relatorio.Append(" — NÃO COUBE (em cima de uma rua, de outro lote ou fora da cidade)");
            if (agua) _relatorio.Append(" — NA ÁGUA");
            return l;
        }

        /// <summary>
        /// Os lotes ao longo de uma rua: em cada ponto dela (dentro da área, se houver), dos lados pedidos (1 à direita de
        /// quem anda do primeiro ponto para o último, 2 à esquerda, 3 os dois), o lote onde cabe (com "pulo" de folga entre um
        /// e outro), com o chão quase plano e longe da água. Uma fração "vazio" deles (pelo sorteio) fica vazia, com a mata.
        /// </summary>
        public int Encher(string rua, UsoDoLote tipo, float largura, float fundo, float recuo, float pulo, float vazio, System.Random sorte,
                          int lados = 3, Rect? area = null)
        {
            int postos = 0;
            foreach (var r in LinhasDe(rua))
            for (int i = 1; i < r.Pontos.Count - 1; i++)
            {
                if (area.HasValue && !area.Value.Contains(r.Pontos[i])) continue;
                for (int s = -1; s <= 1; s += 2)
                {
                    if ((lados & (s < 0 ? 1 : 2)) == 0) continue;
                    var l = NoPonto(r, i, s, largura, fundo, recuo);
                    if (!Cabe(l, pulo, 2f)) continue;
                    if (LotesDeLakewood.Desnivel(_chao, l, out bool agua) > LotesDeLakewood.DesnivelMaximo || agua) continue;
                    l.cidade = _cidade;
                    l.tipo = vazio > 0f && sorte.NextDouble() < vazio ? UsoDoLote.Vazio : tipo;
                    Lotes.Add(l);
                    postos++;
                }
            }
            return postos;
        }

        /// <summary>A média dos meios de um tipo de lote (para os nomes do plano).</summary>
        public static Vector2 MeioDos(IEnumerable<LoteDoMapa> lotes)
        {
            Vector2 soma = Vector2.zero;
            int n = 0;
            foreach (var l in lotes)
            {
                soma += l.meio;
                n++;
            }
            return n > 0 ? soma / n : Vector2.zero;
        }

        /// <summary>
        /// Troca as ruas e os lotes da cidade no mapa pelos do desenho, com a semente de cada lote (pelo lugar: o mesmo lote
        /// sai com a mesma casa se o desenho for refeito).
        /// </summary>
        public void Gravar()
        {
            _mapa.ruas ??= new List<RuaDaCidade>();
            _mapa.lotes ??= new List<LoteDoMapa>();
            _mapa.ruas.RemoveAll(r => r.cidade == _cidade);
            _mapa.lotes.RemoveAll(l => l.cidade == _cidade);
            foreach (var l in Lotes)
            {
                l.semente = (int)(Sorteio.Hash(_mapa.semente, Mathf.RoundToInt(l.meio.x), Mathf.RoundToInt(l.meio.y), 61) & 0x7fffffff);
                // o lote que a casa da partida toma fica vazio (12/11/2026: a casa da partida perto do lago, na Woodcreek)
                if (_mapa.casaDaPartida != null && _mapa.casaDaPartida.TomaOLote(l)) l.tipo = UsoDoLote.Vazio;
            }
            _mapa.ruas.AddRange(Ruas);
            _mapa.lotes.AddRange(Lotes);
        }
    }
}
