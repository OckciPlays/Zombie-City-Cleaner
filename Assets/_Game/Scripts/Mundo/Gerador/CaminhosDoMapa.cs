using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O traçado das estradas pelo relevo (Plano_Mapa, "As estradas e as placas"; as trilhas A* do Guardiões da Floresta):
    /// uma busca A* numa grade de 24 m sobre o mapa jogável. Custa mais subir (acima da inclinação do tipo: rodovia 8%,
    /// estrada 12%, terra 15%, o custo dispara), passar por dentro de fazendas, bases e comunidades e pela serra (só as
    /// saídas do mapa passam por ela); não passa pelo lago. Andar por uma estrada que já existe sai mais barato (as
    /// estradas se juntam), e dentro das cidades também (o chão delas vai ser aplainado). O caminho achado é arredondado
    /// (Chaikin) e guardado a cada ~12 m. O mesmo resultado em todas as máquinas: nada de sorteio, desempate pelo índice.
    /// Desde a M4, a busca lembra a direção de chegada em cada célula e só vira 45° por passo (as curvas abrem: um
    /// retorno da serra leva uns 100 m de estrada, e a faixa de 10 m não dobra por cima dela mesma). Sem caminho assim
    /// (um lugar apertado), tenta de novo aceitando 90°.
    /// </summary>
    public sealed class CaminhosDoMapa
    {
        const float Passo = 24f;
        // as 8 direções em volta, em ordem (do leste para o norte): virar 45° é ir para a vizinha na lista
        static readonly int[] Dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
        static readonly int[] Dz = { 0, 1, 1, 1, 0, -1, -1, -1 };

        readonly DadosDoMundo _d;
        readonly PlanoDoMapa _plano;
        readonly int _n;
        readonly float _origem;
        readonly float[] _altura;      // NaN = ainda não medida
        readonly byte[] _chao;         // 0 livre, 1 lago, 2 serra, 3 fora do mapa
        readonly bool[] _naEstrada;
        readonly bool[] _alemDaCerca;
        // dentro de uma cidade (Parte M5): as estradas não passam (entram e saem pelos portões, nas pontas das avenidas)
        readonly bool[] _naCidade;
        readonly int[] _dono;

        public DadosDoMundo Dados => _d;

        /// <summary>As saídas do mapa passam da cerca da quarentena (para a barreira ficar em cima dela); as outras, não.</summary>
        public bool PassarDaCerca;
        // a busca (reaproveitada entre as buscas, marcada pela vez), por estado: célula × 8 + a direção de chegada
        readonly float[] _g;
        readonly int[] _pai, _vez;
        readonly bool[] _fechada;
        int _vezAtual;

        public CaminhosDoMapa(DadosDoMundo d, PlanoDoMapa plano)
        {
            _d = d;
            _plano = plano;
            _origem = -d.Metade;
            _n = Mathf.CeilToInt(d.Lado / Passo);
            int total = _n * _n;
            _altura = new float[total];
            _chao = new byte[total];
            _naEstrada = new bool[total];
            _alemDaCerca = new bool[total];
            _naCidade = new bool[total];
            _dono = new int[total];
            _g = new float[total * 8];
            _pai = new int[total * 8];
            _vez = new int[total * 8];
            _fechada = new bool[total * 8];
            for (int k = 0; k < total; k++)
            {
                _altura[k] = float.NaN;
                var p = Centro(k);
                _alemDaCerca[k] = !d.DentroDaCerca(p.x, p.y, 10f);
                // fora: além da cerca da quarentena (as saídas passam dela uns metros, para a barreira ficar em cima)
                if (!d.DentroDaCerca(p.x, p.y, -28f) || !d.Dentro(p.x, p.y, 20f)) _chao[k] = 3;
                else if (d.Lago != null && d.Lago.Q(p.x, p.y) < 1.18f) _chao[k] = 1;
                else if (d.Borda(p.x, p.y) > 0.3f) _chao[k] = 2;
            }
            MarcarLugares();
        }

        Vector2 Centro(int k) => new Vector2(_origem + (k % _n + 0.5f) * Passo, _origem + (k / _n + 0.5f) * Passo);

        int Celula(Vector2 p)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt((p.x - _origem) / Passo), 0, _n - 1);
            int j = Mathf.Clamp(Mathf.FloorToInt((p.y - _origem) / Passo), 0, _n - 1);
            return j * _n + i;
        }

        float Altura(int k)
        {
            float h = _altura[k];
            if (float.IsNaN(h))
            {
                var p = Centro(k);
                _altura[k] = h = GeradorDoMundo.AlturaComLago(_d, p.x, p.y);
            }
            return h;
        }

        static float Limite(TipoDeEstrada tipo) => tipo == TipoDeEstrada.Rodovia ? 0.08f : tipo == TipoDeEstrada.Estrada ? 0.12f : 0.15f;

        // o multiplicador de entrar na célula v (além da distância); infinito = não passa
        float Custo(int u, int v, float distancia, TipoDeEstrada tipo, bool pelaSerra, Lugar de, Lugar para, int fim)
        {
            byte chao = _chao[v];
            if (v != fim)
            {
                if (chao == 1 || chao == 3 || _naCidade[v]) return float.PositiveInfinity;
                if (_alemDaCerca[v] && !PassarDaCerca) return float.PositiveInfinity;
                // na serra, só pela estrada que já sobe (a saída): o ponto mais perto da rede pode ser nela
                if (chao == 2 && !pelaSerra && !_naEstrada[v]) return float.PositiveInfinity;
            }
            float inclinacao = Mathf.Abs(Altura(v) - Altura(u)) / distancia;
            float limite = Limite(tipo);
            float fator = 1f + 6f * inclinacao + (inclinacao > limite ? 120f * (inclinacao - limite) : 0f);
            if (chao == 2) fator *= 2f;
            if (_naEstrada[v]) fator *= 0.6f;
            int dono = _dono[v];
            if (dono >= 0)
            {
                var l = _plano.Lugares[dono];
                if (l != de && l != para) fator *= 3f;
            }
            return fator;
        }

        /// <summary>Até onde (q) a área de uma cidade fica fechada para as estradas (a vila tem as ruas um pouco além do contorno).</summary>
        static float LimiteDaArea(Cidade c) => c.Tipo == TipoDeLugar.Vila ? 1.28f : 1.04f;

        static bool NaAreaDeUmaCidade(PlanoDoMapa plano, Vector2 p)
        {
            foreach (var c in plano.Cidades)
                if (Mathf.Abs(p.x - c.Centro.x) < c.Raio * 1.5f && Mathf.Abs(p.y - c.Centro.y) < c.Raio * 1.5f && c.Q(p.x, p.y) < LimiteDaArea(c) + 0.02f)
                    return true;
            return false;
        }

        // de quem é cada célula (o índice do lugar; -1 nenhum): o que tem cerca (fazenda, base, comunidade, laboratório).
        // Os pequenos (cabana, posto, bunker) não contam. As cidades ficam fechadas (até um pouco fora do contorno: os
        // lotes da beira). Feito uma vez, antes das buscas
        void MarcarLugares()
        {
            for (int k = 0; k < _dono.Length; k++) _dono[k] = -1;
            foreach (var c in _plano.Cidades)
            {
                float alcance = c.Raio * 1.5f;
                int i0 = Mathf.Max(0, Mathf.FloorToInt((c.Centro.x - alcance - _origem) / Passo));
                int i1 = Mathf.Min(_n - 1, Mathf.FloorToInt((c.Centro.x + alcance - _origem) / Passo));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((c.Centro.y - alcance - _origem) / Passo));
                int j1 = Mathf.Min(_n - 1, Mathf.FloorToInt((c.Centro.y + alcance - _origem) / Passo));
                float limite = LimiteDaArea(c);
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    var p = Centro(j * _n + i);
                    if (c.Q(p.x, p.y) < limite) _naCidade[j * _n + i] = true;
                }
            }
            for (int li = 0; li < _plano.Lugares.Count; li++)
            {
                var l = _plano.Lugares[li];
                if (l.EhCidade || l.Tipo == TipoDeLugar.Bunker || l.Tipo == TipoDeLugar.Cabana || l.Tipo == TipoDeLugar.Posto
                    || l.Tipo == TipoDeLugar.Barreira || l.Tipo == TipoDeLugar.CasaDaFamilia) continue;
                int i0 = Mathf.Max(0, Mathf.FloorToInt((l.Centro.x - l.Raio - _origem) / Passo));
                int i1 = Mathf.Min(_n - 1, Mathf.FloorToInt((l.Centro.x + l.Raio - _origem) / Passo));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((l.Centro.y - l.Raio - _origem) / Passo));
                int j1 = Mathf.Min(_n - 1, Mathf.FloorToInt((l.Centro.y + l.Raio - _origem) / Passo));
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int k = j * _n + i;
                    if ((Centro(k) - l.Centro).sqrMagnitude > l.Raio * l.Raio) continue;
                    if (_dono[k] < 0) _dono[k] = li;
                }
            }
        }

        /// <summary>
        /// O caminho de a até b (null se não houver). "de" e "para" são os lugares das pontas (passar por dentro deles não
        /// custa a mais); pelaSerra deixa passar pela serra (as saídas do mapa). "saindo" e "chegando" (opcionais) são as
        /// direções do primeiro e do último passo (até 45° de diferença): a estrada continua a avenida do portão da cidade.
        /// </summary>
        public Estrada Tracar(Vector2 a, Vector2 b, TipoDeEstrada tipo, bool pelaSerra, Lugar de, Lugar para, Vector2? saindo = null, Vector2? chegando = null)
        {
            // primeiro saindo e chegando bem na direção pedida (a vizinha mais perto dela), depois até 45° dela, depois de qualquer jeito
            _limiteDaDirecao = 0.92f;
            int fimEstado = Buscar(a, b, tipo, pelaSerra, de, para, 1, saindo, chegando);
            if (fimEstado < 0) fimEstado = Buscar(a, b, tipo, pelaSerra, de, para, 2, saindo, chegando);
            _limiteDaDirecao = 0.7f;
            if (fimEstado < 0 && (saindo != null || chegando != null)) fimEstado = Buscar(a, b, tipo, pelaSerra, de, para, 2, saindo, chegando);
            if (fimEstado < 0 && (saindo != null || chegando != null)) fimEstado = Buscar(a, b, tipo, pelaSerra, de, para, 2, null, null);
            if (fimEstado < 0) return null;

            var celulas = new List<Vector2>();
            for (int s = fimEstado; s >= 0; s = _pai[s]) celulas.Add(Centro(s >> 3));
            celulas.Reverse();
            celulas[0] = a;
            celulas[celulas.Count - 1] = b;
            var suave = Chaikin(Chaikin(Chaikin(celulas)));
            var estrada = new Estrada { Tipo = tipo };
            Reamostrar(suave, 12f, estrada.Pontos);
            for (int i = 1; i < estrada.Pontos.Count; i++) estrada.Comprimento += Vector2.Distance(estrada.Pontos[i - 1], estrada.Pontos[i]);
            foreach (var p in estrada.Pontos) _naEstrada[Celula(p)] = true;
            return estrada;
        }

        // a busca A* com a direção no estado (célula × 8 + direção de chegada); "virada" é quantos passos de 45° a estrada
        // pode virar de uma célula para a outra. Devolve o estado em que chegou (ou -1)
        int Buscar(Vector2 a, Vector2 b, TipoDeEstrada tipo, bool pelaSerra, Lugar de, Lugar para, int virada, Vector2? saindo, Vector2? chegando)
        {
            int inicio = Celula(a), fim = Celula(b);
            _vezAtual++;
            var heap = new HeapMinimo();
            var alvo = Centro(fim);
            // a partida: qualquer direção serve (ou só as perto da direção pedida)
            for (int dir = 0; dir < 8; dir++)
            {
                if (saindo.HasValue && Vector2.Dot(Direcao(dir), saindo.Value) < _limiteDaDirecao) continue;
                Abrir(inicio * 8 + dir, 0f, -1);
                heap.Por(Vector2.Distance(Centro(inicio), alvo) * 0.6f, inicio * 8 + dir);
            }
            while (heap.Quantos > 0)
            {
                int s = heap.Tirar();
                if (_fechada[s]) continue;
                int u = s >> 3, du = s & 7;
                if (u == fim && (!chegando.HasValue || Vector2.Dot(Direcao(du), chegando.Value) >= _limiteDaDirecao)) return s;
                _fechada[s] = true;
                int ui = u % _n, uj = u / _n;
                for (int giro = -virada; giro <= virada; giro++)
                {
                    int k = (du + giro + 8) & 7;
                    int vi = ui + Dx[k], vj = uj + Dz[k];
                    if (vi < 0 || vj < 0 || vi >= _n || vj >= _n) continue;
                    int v = vj * _n + vi;
                    int sv = v * 8 + k;
                    if (_vez[sv] == _vezAtual && _fechada[sv]) continue;
                    float distancia = (k & 1) == 0 ? Passo : Passo * 1.41421356f;
                    float fator = Custo(u, v, distancia, tipo, pelaSerra, de, para, fim);
                    if (float.IsInfinity(fator)) continue;
                    float g = _g[s] + distancia * fator;
                    if (_vez[sv] == _vezAtual && g >= _g[sv]) continue;
                    Abrir(sv, g, s);
                    // a heurística com o menor fator possível (0,6, na estrada que já existe): nunca superestima
                    heap.Por(g + Vector2.Distance(Centro(v), alvo) * 0.6f, sv);
                }
            }
            return -1;
        }

        static Vector2 Direcao(int dir) => new Vector2(Dx[dir], Dz[dir]).normalized;

        // o quanto o primeiro e o último passo precisam seguir a direção pedida (o cosseno)
        float _limiteDaDirecao = 0.7f;

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

        /// <summary>Arredonda a linha (Chaikin), mantendo o primeiro e o último ponto.</summary>
        public static List<Vector2> Chaikin(List<Vector2> linha)
        {
            if (linha.Count < 3) return new List<Vector2>(linha);
            var nova = new List<Vector2>(linha.Count * 2) { linha[0] };
            for (int i = 0; i < linha.Count - 1; i++)
            {
                nova.Add(Vector2.Lerp(linha[i], linha[i + 1], 0.25f));
                nova.Add(Vector2.Lerp(linha[i], linha[i + 1], 0.75f));
            }
            nova.Add(linha[linha.Count - 1]);
            return nova;
        }

        /// <summary>Pontos a cada "passo" m ao longo da linha (o primeiro e o último ficam).</summary>
        public static void Reamostrar(List<Vector2> linha, float passo, List<Vector2> saida)
        {
            saida.Clear();
            saida.Add(linha[0]);
            float sobra = 0f;
            for (int i = 1; i < linha.Count; i++)
            {
                var a = linha[i - 1];
                var b = linha[i];
                float trecho = Vector2.Distance(a, b);
                float t = passo - sobra;
                while (t <= trecho)
                {
                    saida.Add(Vector2.Lerp(a, b, t / trecho));
                    t += passo;
                }
                sobra = trecho - (t - passo);
            }
            if (Vector2.Distance(saida[saida.Count - 1], linha[linha.Count - 1]) > passo * 0.3f) saida.Add(linha[linha.Count - 1]);
            else saida[saida.Count - 1] = linha[linha.Count - 1];
        }

        /// <summary>
        /// O ponto de estrada mais perto (de qualquer estrada já traçada), ou o próprio ponto se não houver. Com o mundo,
        /// só os pontos bem dentro da cerca da quarentena (ninguém se liga na ponta da saída, depois da barreira).
        /// </summary>
        public static Vector2 MaisPerto(PlanoDoMapa plano, Vector2 p, out float distancia, DadosDoMundo d = null)
        {
            var melhor = p;
            distancia = float.MaxValue;
            foreach (var e in plano.Estradas)
                foreach (var q in e.Pontos)
                {
                    // (nem nos caminhos por dentro das cidades, nem nos pontos da área fechada delas: lá as ruas são da
                    // cidade, e a busca não chegaria)
                    if (e.Conector || NaAreaDeUmaCidade(plano, q)) continue;
                    if (d != null && !d.DentroDaCerca(q.x, q.y, 25f)) continue;
                    float d2 = (q - p).sqrMagnitude;
                    if (d2 < distancia)
                    {
                        distancia = d2;
                        melhor = q;
                    }
                }
            distancia = Mathf.Sqrt(distancia);
            return melhor;
        }

        // um heap mínimo simples (custo, célula); empata pela célula, para dar o mesmo em todas as máquinas
        sealed class HeapMinimo
        {
            readonly List<(float f, int k)> _itens = new List<(float, int)>(1024);
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
    }
}
