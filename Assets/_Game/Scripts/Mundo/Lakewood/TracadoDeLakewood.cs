using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O traçado das estradas do Lakewood pelo relevo (Plano_Lakewood, Etapa 5; a ideia é a do traçado do mundo sorteado,
    /// <see cref="CaminhosDoMapa"/>): uma busca A* numa grade de <see cref="Passo"/> m sobre o mapa, com a direção de
    /// chegada no estado (a estrada só vira 45° por passo: as curvas abrem, e a subida da serra vira zigue-zague em vez de
    /// subir reto). O custo de cada passo é a distância vezes:
    ///  - a subida (acima da rampa da estrada, o custo dispara: ela dá a volta pelo vale ou pela encosta mais deitada);
    ///  - a encosta de lado (no paredão, o corte e o aterro ficam enormes);
    ///  - o rio (uma ponte: ela atravessa no lugar mais estreito, mais ou menos de frente); o lago não se atravessa (a
    ///    ponte do norte é uma reta à parte, <see cref="Travessia"/>), e a beira dele custa mais (a estrada longe da praia);
    ///  - as outras estradas (cruzar pode, andar junto não), as cidades (mais barato: o chão delas é aplainado e as
    ///    estradas entram pelo meio) e os lugares cercados (o aeroporto, a estação, a base, a pedreira: só quem vai até eles).
    /// Não passa pela hidrelétrica (os pátios e as estradas dela) nem pela beira do mapa (só perto das pontas: as saídas).
    /// O caminho das células é puxado para retas em qualquer ângulo onde a reta não custa mais (<see cref="Puxar"/>), depois
    /// arredondado (Chaikin) e devolvido a cada ~<see cref="EntrePontos"/> m, com as pontas exatas.
    /// O mesmo resultado sempre (sem sorteio). Código puro: usado no Editor (o menu da Etapa 5), que grava as estradas no
    /// asset; o jogo só lê os pontos.
    /// </summary>
    public sealed class TracadoDeLakewood
    {
        public const float Passo = 24f;
        /// <summary>A distância entre os pontos de controle gravados (m): o jogo arredonda de novo (<see cref="MapaDeLakewood.Linha"/>).</summary>
        public const float EntrePontos = 30f;

        // as 8 direções, em ordem (do leste para o norte): virar 45° é ir para a vizinha na lista
        static readonly int[] Dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
        static readonly int[] Dz = { 0, 1, 1, 1, 0, -1, -1, -1 };

        enum Chao : byte { Livre, Lago, Rio, Borda, Fechado, Ponte }

        readonly ChaoDeLakewood _c;
        readonly MapaDeLakewood _m;
        public readonly int Nx, Nz;
        readonly float[] _h, _encosta, _fator;
        readonly byte[] _chao;
        readonly short[] _lugar;
        readonly bool[] _naEstrada;
        readonly float[] _g;
        readonly int[] _pai, _vez;
        readonly bool[] _fechada;
        readonly int[] _alvo;
        int _vezAtual;

        /// <summary>Quantos estados a última busca abriu (para o relatório).</summary>
        public int Abertos { get; private set; }

        /// <summary>Uma ponte sobre o lago que pode ser atravessada: o meio e o raio do corredor (m).</summary>
        public readonly struct CorredorDePonte
        {
            public readonly Vector2 Meio;
            public readonly float Raio;
            public CorredorDePonte(Vector2 meio, float raio) { Meio = meio; Raio = raio; }
        }

        public TracadoDeLakewood(ChaoDeLakewood chao, IList<CorredorDePonte> pontes)
        {
            _c = chao;
            _m = chao.Mapa;
            Nx = Mathf.CeilToInt(_m.largura / Passo);
            Nz = Mathf.CeilToInt(_m.comprimento / Passo);
            int total = Nx * Nz;
            _h = new float[total];
            _encosta = new float[total];
            _fator = new float[total];
            _chao = new byte[total];
            _lugar = new short[total];
            _naEstrada = new bool[total];
            _g = new float[total * 8];
            _pai = new int[total * 8];
            _vez = new int[total * 8];
            _fechada = new bool[total * 8];
            _alvo = new int[total];
            var cidades = new List<List<Vector2>>();
            foreach (var cd in _m.cidades) cidades.Add(cd.contorno.Count >= 3 ? MapaDeLakewood.Contorno(cd.contorno) : null);
            var usina = chao.Usina;
            // as estradas da usina: ninguém anda junto delas (só chega nas pontas ou num ponto pedido)
            var viasDaUsina = new List<List<Vector2>>();
            if (usina != null && usina.Tem)
                foreach (var v in usina.Vias)
                    if (v.pontos.Count >= 2) viasDaUsina.Add(MapaDeLakewood.Linha(v.pontos));
            for (int k = 0; k < total; k++)
            {
                var p = Centro(k);
                float x = p.x, z = p.y;
                _lugar[k] = -1;
                _fator[k] = 1f;
                _h[k] = chao.Relevo.Altura(x, z);
                _encosta[k] = chao.Relevo.Inclinacao(x, z);
                if (x < 30f || z < 30f || x > _m.largura - 30f || z > _m.comprimento - 30f) { _chao[k] = (byte)Chao.Borda; continue; }
                // a água do lago (o meio da célula molhado) só se atravessa nos corredores das pontes; a beira seca (até
                // ~25 m da água) passa, mas custa mais (a estrada fica longe da praia, menos nas pontas de terra estreitas)
                if (chao.AguaDoLagoEm(x, z, out float nivel) && _h[k] < nivel + 0.5f)
                {
                    bool corredor = false;
                    foreach (var pt in pontes) corredor |= (p - pt.Meio).sqrMagnitude < pt.Raio * pt.Raio;
                    _chao[k] = (byte)(corredor ? Chao.Ponte : Chao.Lago);
                    _h[k] = Mathf.Max(_h[k], nivel + 6f);
                    continue;
                }
                if (chao.AguaDoLagoEm(x, z, out _, 14f)) _fator[k] = 1.8f;
                if (chao.RioMaisPerto(x, z, 10f, out var rio) && rio.Dist < rio.Meia + 10f)
                {
                    _chao[k] = (byte)Chao.Rio;
                    _h[k] = Mathf.Max(_h[k], rio.Nivel + 3f);
                }
                if (usina != null && usina.Tem)
                {
                    var l = usina.Local(x, z);
                    bool fechado = false;
                    foreach (var pt in usina.Patios)
                    {
                        var a = pt.Area;
                        if (l.x > a.xMin - 14f && l.x < a.xMax + 14f && l.y > a.yMin - 14f && l.y < a.yMax + 14f) { fechado = true; break; }
                    }
                    if (!fechado)
                        foreach (var linha in viasDaUsina)
                            if (Linhas.Distancia(p, linha) < 16f) { fechado = true; break; }
                    if (fechado) { _chao[k] = (byte)Chao.Fechado; continue; }
                }
                for (int ci = 0; ci < cidades.Count; ci++)
                    if (cidades[ci] != null && Linhas.Dentro(p, cidades[ci])) { _fator[k] *= 0.85f; break; }
                for (int li = 0; li < _m.lugares.Count; li++)
                {
                    var lg = _m.lugares[li];
                    if (lg.aplainar < 0.45f) continue;
                    if ((p - lg.ponto).sqrMagnitude < lg.raio * lg.raio * 0.8f) { _lugar[k] = (short)li; break; }
                }
            }
        }

        Vector2 Centro(int k) => new Vector2((k % Nx + 0.5f) * Passo, (k / Nx + 0.5f) * Passo);

        int Celula(Vector2 p)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(p.x / Passo), 0, Nx - 1);
            int j = Mathf.Clamp(Mathf.FloorToInt(p.y / Passo), 0, Nz - 1);
            return j * Nx + i;
        }

        static Vector2 Direcao(int d) => new Vector2(Dx[d], Dz[d]).normalized;

        /// <summary>O chão das células em volta de p (para o relatório: . livre, ~ lago, r rio, # beira, F fechado, P ponte, e marcado).</summary>
        public string Diagnostico(Vector2 p, int raio = 6)
        {
            var sb = new System.Text.StringBuilder();
            int c = Celula(p), ci = c % Nx, cj = c / Nx;
            for (int j = cj + raio; j >= cj - raio; j--)
            {
                for (int i = ci - raio; i <= ci + raio; i++)
                {
                    if (i < 0 || j < 0 || i >= Nx || j >= Nz) { sb.Append(' '); continue; }
                    int k = j * Nx + i;
                    char ch = (Chao)_chao[k] switch { Chao.Lago => '~', Chao.Rio => 'r', Chao.Borda => '#', Chao.Fechado => 'F', Chao.Ponte => 'P', _ => _naEstrada[k] ? 'e' : '.' };
                    if (k == c) ch = 'X';
                    sb.Append(ch);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>Marca a estrada traçada (as que vêm depois podem cruzar, mas não andar junto dela).</summary>
        public void Marcar(IList<Vector2> linha)
        {
            var pts = Linhas.Reamostrar(linha, Passo * 0.5f, false);
            foreach (var p in pts)
                for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    var q = p + new Vector2(di, dj) * (Passo * 0.6f);
                    if (q.x < 0f || q.y < 0f || q.x >= _m.largura || q.y >= _m.comprimento) continue;
                    _naEstrada[Celula(q)] = true;
                }
        }

        /// <summary>O que uma busca pode e quanto custa.</summary>
        public struct Pedido
        {
            /// <summary>A rampa da estrada (6% na rodovia, 9% nas secundárias, 10% nas de serra).</summary>
            public float Rampa;
            /// <summary>Uma saída do mapa (só para o relatório: a beira vale perto das pontas, e a saída chega nela pelo <see cref="TracarAte"/>).</summary>
            public bool Saida;
            /// <summary>Os lugares das pontas (índices em lugares; -1 nenhum): passar por dentro deles não custa a mais.</summary>
            public int LugarDe, LugarPara;
        }

        // as pontas da busca de agora: perto delas, o fechado e a beira não contam (a ponta de uma estrada da usina, a saída)
        Vector2 _pontaA, _pontaB;
        bool _temPontaB;
        const float PertoDaPonta = 50f;

        bool PertoDeUmaPonta(int v)
        {
            var c = Centro(v);
            return (c - _pontaA).sqrMagnitude < PertoDaPonta * PertoDaPonta || (_temPontaB && (c - _pontaB).sqrMagnitude < PertoDaPonta * PertoDaPonta);
        }

        // o multiplicador da distância de entrar em v a partir de u (infinito: não passa)
        float Custo(int u, int v, float distancia, in Pedido p, bool ehPonta)
        {
            var chao = (Chao)_chao[v];
            if (!ehPonta && !PertoDeUmaPonta(v) && (chao == Chao.Lago || chao == Chao.Fechado || chao == Chao.Borda)) return float.PositiveInfinity;
            return Fator(v, Mathf.Abs(_h[v] - _h[u]) / distancia, p);
        }

        // o multiplicador de andar na célula v com a subida dada
        float Fator(int v, float subida, in Pedido p)
        {
            var chao = (Chao)_chao[v];
            float f = 1f + 3f * subida + (subida > p.Rampa ? 800f * (subida - p.Rampa) : 0f);
            f += 2.5f * Mathf.Max(0f, _encosta[v] - 0.15f);
            if (chao == Chao.Rio) f += 4f;
            else if (chao == Chao.Ponte) f += 2.5f;
            if (_naEstrada[v]) f *= 3f;
            int lg = _lugar[v];
            if (lg >= 0 && lg != p.LugarDe && lg != p.LugarPara) f *= 2.5f;
            return f * _fator[v];
        }

        // ------------------------------------------------------------------ o atalho (as retas em qualquer ângulo)

        // a altura para medir a subida num ponto qualquer: o relevo (na água, a do tabuleiro, como nas células)
        float AlturaEm(Vector2 q)
        {
            int c = Celula(q);
            var chao = (Chao)_chao[c];
            return chao == Chao.Lago || chao == Chao.Ponte || chao == Chao.Rio ? _h[c] : _c.Relevo.Altura(q.x, q.y);
        }

        // o custo de andar em linha reta de a até b, medido a cada ~Passo m (infinito se passa por onde não pode)
        float CustoDaReta(Vector2 a, Vector2 b, in Pedido p)
        {
            float l = Vector2.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(l / Passo));
            float custo = 0f, hAntes = AlturaEm(a), passo = l / n;
            for (int k = 1; k <= n; k++)
            {
                var q = Vector2.Lerp(a, b, k / (float)n);
                int c = Celula(q);
                var chao = (Chao)_chao[c];
                if ((chao == Chao.Lago || chao == Chao.Fechado || chao == Chao.Borda) && !PertoDeUmaPonta(c) && _alvo[c] != _vezesDoAlvo) return float.PositiveInfinity;
                float h = AlturaEm(q);
                custo += passo * Fator(c, Mathf.Abs(h - hAntes) / Mathf.Max(passo, 1e-3f), p);
                hAntes = h;
            }
            return custo;
        }

        // puxa o caminho das células (a busca anda em 8 direções) para retas em qualquer ângulo, onde a reta não custa mais
        // que o caminho (a mesma subida, sem água nem fechado), com no máximo "salto" células por reta
        List<Vector2> Puxar(List<Vector2> celulas, in Pedido p, int salto)
        {
            if (celulas.Count < 3 || salto <= 1) return new List<Vector2>(celulas);
            var acumulado = new float[celulas.Count];
            for (int i = 1; i < celulas.Count; i++) acumulado[i] = acumulado[i - 1] + CustoDaReta(celulas[i - 1], celulas[i], p);
            var saida = new List<Vector2> { celulas[0] };
            int atual = 0;
            while (atual < celulas.Count - 1)
            {
                int melhor = atual + 1;
                for (int j = Mathf.Min(celulas.Count - 1, atual + salto); j > atual + 1; j--)
                {
                    float reta = CustoDaReta(celulas[atual], celulas[j], p);
                    if (reta <= acumulado[j] - acumulado[atual] + 0.5f) { melhor = j; break; }
                }
                saida.Add(celulas[melhor]);
                atual = melhor;
            }
            return saida;
        }

        // a linha arredondada passa só por onde pode (o Chaikin corta os cantos para dentro)?
        bool Valida(List<Vector2> linha)
        {
            var pts = Linhas.Reamostrar(MapaDeLakewood.Linha(linha), 8f, false);
            foreach (var q in pts)
            {
                int c = Celula(q);
                var chao = (Chao)_chao[c];
                if ((chao == Chao.Lago || chao == Chao.Fechado || chao == Chao.Borda) && !PertoDeUmaPonta(c) && _alvo[c] != _vezesDoAlvo) return false;
            }
            return true;
        }

        // o melhor atalho que continua válido: retas de até 6 células, senão 3, senão o caminho das células
        List<Vector2> PuxarValido(List<Vector2> celulas, in Pedido p)
        {
            foreach (int salto in new[] { 6, 3 })
            {
                var puxado = Puxar(celulas, p, salto);
                if (Valida(Arredondar(puxado, new[] { puxado[0], puxado[puxado.Count - 1] }))) return puxado;
            }
            return celulas;
        }

        // a busca: de "inicio" (com a direção de saída opcional) até qualquer célula marcada como alvo nesta vez (com a
        // condição da direção de chegada); a heurística vai para "mira" (só com um alvo: com vários, nada)
        int Buscar(int inicio, Vector2? saindo, Vector2? mira, in Pedido pedido, Func<int, int, bool> chegou, int vezDoAlvo)
        {
            _vezAtual++;
            Abertos = 0;
            var heap = new HeapMinimo();
            for (int d = 0; d < 8; d++)
            {
                if (saindo.HasValue && Vector2.Dot(Direcao(d), saindo.Value) < 0.7f) continue;
                Abrir(inicio * 8 + d, 0f, -1);
                heap.Por(mira.HasValue ? Vector2.Distance(Centro(inicio), mira.Value) * 0.85f : 0f, inicio * 8 + d);
            }
            while (heap.Quantos > 0)
            {
                int s = heap.Tirar();
                if (_fechada[s]) continue;
                int u = s >> 3, du = s & 7;
                if (_alvo[u] == vezDoAlvo && chegou(u, du)) return s;
                _fechada[s] = true;
                Abertos++;
                int ui = u % Nx, uj = u / Nx;
                for (int giro = -1; giro <= 1; giro++)
                {
                    int k = (du + giro + 8) & 7;
                    int vi = ui + Dx[k], vj = uj + Dz[k];
                    if (vi < 0 || vj < 0 || vi >= Nx || vj >= Nz) continue;
                    int v = vj * Nx + vi;
                    int sv = v * 8 + k;
                    if (_vez[sv] == _vezAtual && _fechada[sv]) continue;
                    float distancia = (k & 1) == 0 ? Passo : Passo * 1.41421356f;
                    float fator = Custo(u, v, distancia, pedido, _alvo[v] == vezDoAlvo);
                    if (float.IsInfinity(fator)) continue;
                    float g = _g[s] + distancia * fator + (giro != 0 ? Passo * 0.15f : 0f);
                    if (_vez[sv] == _vezAtual && g >= _g[sv]) continue;
                    Abrir(sv, g, s);
                    heap.Por(g + (mira.HasValue ? Vector2.Distance(Centro(v), mira.Value) * 0.85f : 0f), sv);
                }
            }
            return -1;
        }

        void Abrir(int k, float g, int pai)
        {
            if (_vez[k] != _vezAtual)
            {
                _vez[k] = _vezAtual;
                _fechada[k] = false;
            }
            _g[k] = g;
            _pai[k] = pai;
        }

        int _vezesDoAlvo;

        List<Vector2> Caminho(int fim, out int direcaoDeChegada)
        {
            var celulas = new List<Vector2>();
            direcaoDeChegada = fim & 7;
            for (int s = fim; s >= 0; s = _pai[s]) celulas.Add(Centro(s >> 3));
            celulas.Reverse();
            return celulas;
        }

        /// <summary>
        /// Uma estrada pelas paradas, em ordem (a primeira e a última são as pontas exatas; as do meio, pontos por onde
        /// ela passa, como a ponte do norte), com a direção contínua de um trecho para o outro. Devolve os pontos de
        /// controle (a cada <see cref="EntrePontos"/> m), ou null sem caminho.
        /// </summary>
        public List<Vector2> Tracar(IList<Vector2> paradas, Pedido pedido, Vector2? saindoDoComeco = null, Vector2? chegandoNoFim = null)
        {
            var celulas = new List<Vector2>();
            Vector2? saindo = saindoDoComeco;
            for (int t = 1; t < paradas.Count; t++)
            {
                int inicio = Celula(paradas[t - 1]), fim = Celula(paradas[t]);
                _pontaA = paradas[t - 1];
                _pontaB = paradas[t];
                _temPontaB = true;
                int vez = ++_vezesDoAlvo;
                _alvo[fim] = vez;
                // no último trecho, chegando na direção pedida (a cabeceira de uma ponte)
                var chegando = t == paradas.Count - 1 ? chegandoNoFim : null;
                Func<int, int, bool> chegou = (u, d) => !chegando.HasValue || Vector2.Dot(Direcao(d), chegando.Value) >= 0.7f;
                int achado = Buscar(inicio, saindo, Centro(fim), pedido, chegou, vez);
                if (achado < 0) achado = Buscar(inicio, null, Centro(fim), pedido, (u, d) => true, vez);
                if (achado < 0) return null;
                var trecho = Caminho(achado, out int chegada);
                saindo = Direcao(chegada);
                trecho[0] = paradas[t - 1];
                trecho[trecho.Count - 1] = paradas[t];
                trecho = PuxarValido(trecho, pedido);
                if (celulas.Count > 0) trecho.RemoveAt(0);
                celulas.AddRange(trecho);
            }
            return Arredondar(celulas, paradas);
        }

        /// <summary>
        /// Um ramal: de "de" (a ponta exata) até a estrada "linha" (a linha arredondada dela, como o jogo a vê), chegando
        /// nela num ponto a pelo menos 40 m das pontas e a 45° ou mais dela; com "perto", só até "alcance" m desse ponto.
        /// A ponta do ramal fica exatamente na linha. Devolve os pontos de controle, ou null.
        /// </summary>
        public List<Vector2> TracarAte(Vector2 de, IList<Vector2> linha, Pedido pedido, Vector2? perto = null, float alcance = 600f, bool naBorda = false)
        {
            int vez = ++_vezesDoAlvo;
            float total = Linhas.Comprimento(linha);
            var direcoes = new Dictionary<int, Vector2>();
            var pts = Linhas.Reamostrar(linha, 6f, false);
            float s = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) s += Vector2.Distance(pts[i - 1], pts[i]);
                if (s < 40f || s > total - 40f) continue;
                if (perto.HasValue && Vector2.Distance(pts[i], perto.Value) > alcance) continue;
                int k = Celula(pts[i]);
                if ((Centro(k) - pts[i]).sqrMagnitude > 14f * 14f) continue;
                // só em terra firme, longe da água (o ramal não chega em cima de uma ponte); a saída, na beira do mapa
                var chaoDoAlvo = (Chao)_chao[k];
                if (naBorda ? chaoDoAlvo != Chao.Borda : chaoDoAlvo != Chao.Livre || _fator[k] > 1.5f) continue;
                _alvo[k] = vez;
                direcoes[k] = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
            }
            if (direcoes.Count == 0) return null;
            _pontaA = de;
            _temPontaB = false;
            int achado = Buscar(Celula(de), null, perto, pedido, (u, d) => direcoes.TryGetValue(u, out var dir) && Mathf.Abs(Vector2.Dot(Direcao(d), dir)) < 0.75f, vez);
            if (achado < 0) achado = Buscar(Celula(de), null, perto, pedido, (u, d) => true, vez);
            if (achado < 0) return null;
            var celulas = Caminho(achado, out _);
            celulas[0] = de;
            // a ponta na linha: o ponto dela mais perto do fim do caminho
            var fim = PontoMaisPerto(celulas[celulas.Count - 1], linha);
            celulas[celulas.Count - 1] = fim;
            _pontaB = fim;
            _temPontaB = true;
            return Arredondar(PuxarValido(celulas, pedido), new[] { de, fim });
        }

        /// <summary>
        /// A travessia de uma ponte sobre o lago perto de "perto" (até "raio" m): a reta mais curta de terra a terra, com o vão
        /// de pelo menos "vaoMinimo" m (na ponta da baía, a água estreita até sumir), a até 40° de "direcao". "a" e "b" são as
        /// cabeceiras, 15 m dentro da terra (de "a" para "b" é a "direcao").
        /// </summary>
        public static bool Travessia(ChaoDeLakewood c, Vector2 perto, float raio, Vector2 direcao, float vaoMinimo, out Vector2 a, out Vector2 b)
        {
            bool Agua(Vector2 q) => c.AguaDoLagoEm(q.x, q.y, out float n) && c.Relevo.Altura(q.x, q.y) < n;
            float melhor = float.MaxValue, maisPerto = float.MaxValue;
            a = b = perto;
            direcao = direcao.normalized;
            for (float dz = -raio; dz <= raio; dz += 20f)
            for (float dx = -raio; dx <= raio; dx += 20f)
            {
                var m = perto + new Vector2(dx, dz);
                if (dx * dx + dz * dz > raio * raio || !Agua(m)) continue;
                for (int g = -40; g <= 40; g += 5)
                {
                    float r = g * Mathf.Deg2Rad;
                    var dir = new Vector2(direcao.x * Mathf.Cos(r) - direcao.y * Mathf.Sin(r), direcao.x * Mathf.Sin(r) + direcao.y * Mathf.Cos(r));
                    float s1 = 0f, s0 = 0f;
                    while (s1 < 600f && Agua(m + dir * s1)) s1 += 2f;
                    while (s0 < 600f && Agua(m - dir * s0)) s0 += 2f;
                    float vao = s0 + s1;
                    if (s0 >= 600f || s1 >= 600f || vao < vaoMinimo) continue;
                    float d = (m - perto).sqrMagnitude;
                    if (vao < melhor - 4f || (vao < melhor + 4f && d < maisPerto))
                    {
                        melhor = Mathf.Min(melhor, vao);
                        maisPerto = d;
                        a = m - dir * (s0 + 15f);
                        b = m + dir * (s1 + 15f);
                    }
                }
            }
            return melhor < float.MaxValue;
        }

        /// <summary>O ponto da linha mais perto de p (exato, num dos segmentos dela).</summary>
        public static Vector2 PontoMaisPerto(Vector2 p, IList<Vector2> linha)
        {
            float melhor = float.MaxValue;
            var ponto = linha[0];
            for (int i = 1; i < linha.Count; i++)
            {
                var a = linha[i - 1];
                var ab = linha[i] - a;
                float l2 = ab.sqrMagnitude;
                float t = l2 < 1e-9f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                var q = a + ab * t;
                float d = (p - q).sqrMagnitude;
                if (d < melhor)
                {
                    melhor = d;
                    ponto = q;
                }
            }
            return ponto;
        }

        // um heap mínimo simples (custo, estado); empata pelo estado, para dar sempre o mesmo
        sealed class HeapMinimo
        {
            readonly List<(float f, int k)> _itens = new List<(float, int)>(4096);
            public int Quantos => _itens.Count;

            static bool Menor((float f, int k) a, (float f, int k) b) => a.f < b.f || (a.f == b.f && a.k < b.k);

            public void Por(float f, int k)
            {
                _itens.Add((f, k));
                int i = _itens.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (!Menor(_itens[i], _itens[p])) break;
                    (_itens[i], _itens[p]) = (_itens[p], _itens[i]);
                    i = p;
                }
            }

            public int Tirar()
            {
                int topo = _itens[0].k;
                int ultimo = _itens.Count - 1;
                _itens[0] = _itens[ultimo];
                _itens.RemoveAt(ultimo);
                int i = 0;
                while (true)
                {
                    int e = 2 * i + 1, d = e + 1, menor = i;
                    if (e < _itens.Count && Menor(_itens[e], _itens[menor])) menor = e;
                    if (d < _itens.Count && Menor(_itens[d], _itens[menor])) menor = d;
                    if (menor == i) break;
                    (_itens[i], _itens[menor]) = (_itens[menor], _itens[i]);
                    i = menor;
                }
                return topo;
            }
        }

        // o caminho de células arredondado (3 passadas de Chaikin, com as paradas presas) e os pontos de controle
        static List<Vector2> Arredondar(List<Vector2> celulas, IList<Vector2> paradas)
        {
            var suave = CaminhosDoMapa.Chaikin(CaminhosDoMapa.Chaikin(CaminhosDoMapa.Chaikin(celulas)));
            var saida = new List<Vector2>();
            CaminhosDoMapa.Reamostrar(suave, EntrePontos, saida);
            saida[0] = paradas[0];
            saida[saida.Count - 1] = paradas[paradas.Count - 1];
            return saida;
        }
    }
}
