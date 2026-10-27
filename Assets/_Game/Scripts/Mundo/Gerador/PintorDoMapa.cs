using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Pinta o mapa como imagem (Plano_Mapa, Parte M3; o WorldMapPainter do Guardiões da Floresta), com cara de mapa de
    /// estrada: o relevo sombreado de noroeste, a serra cinza, o lago, as cidades com as zonas (o centro mais escuro, o
    /// comércio, os bairros de casas) e um sinal das ruas no giro delas, as fazendas em faixas de plantação, as bases e as
    /// comunidades cercadas (verde as aliadas, vermelho as inimigas), o laboratório, as estradas (a rodovia amarela, a estrada
    /// branca e a de terra marrom tracejada), o metrô (tracejado vermelho com as estações) e os ícones (postos, bunkers,
    /// cabanas, barreiras, a casa do lago e a da família). Os nomes não vão na imagem: <see cref="Rotulos"/> diz onde cada
    /// um fica, e quem mostra o mapa escreve por cima. Código puro: pode rodar em outra thread.
    /// A imagem cobre o mapa jogável (de -Metade a +Metade), com a linha 0 no sul (como uma Texture2D).
    /// </summary>
    public static class PintorDoMapa
    {
        static readonly Color Terra = new Color(0.66f, 0.64f, 0.52f);
        static readonly Color Serra = new Color(0.56f, 0.54f, 0.51f);
        static readonly Color Agua = new Color(0.36f, 0.45f, 0.5f);
        static readonly Color AguaFunda = new Color(0.24f, 0.32f, 0.38f);
        static readonly Color CentroCor = new Color(0.43f, 0.42f, 0.41f);
        static readonly Color ComercioCor = new Color(0.56f, 0.54f, 0.51f);
        static readonly Color CasasCor = new Color(0.74f, 0.7f, 0.62f);
        static readonly Color Rua = new Color(0.86f, 0.84f, 0.79f);
        static readonly Color Contorno = new Color(0.25f, 0.24f, 0.22f);
        static readonly Color Rodovia = new Color(0.93f, 0.72f, 0.28f);
        static readonly Color EstradaCor = new Color(0.95f, 0.94f, 0.9f);
        static readonly Color TerraCor = new Color(0.47f, 0.36f, 0.23f);
        static readonly Color Metro = new Color(0.72f, 0.12f, 0.12f);

        /// <summary>Os nomes do mapa e onde ficam: o nível diz o tamanho (0 a cidade grande, 1 a pequena, 2 os lugares, 3 os bairros).</summary>
        public struct Rotulo
        {
            public Vector2 Ponto;
            public string Texto;
            public int Nivel;
        }

        public static List<Rotulo> Rotulos(DadosDoMundo d)
        {
            var lista = new List<Rotulo>();
            var plano = d.Plano;
            if (plano == null) return lista;
            foreach (var l in plano.Lugares)
            {
                int nivel;
                switch (l.Tipo)
                {
                    case TipoDeLugar.CidadeGrande: nivel = 0; break;
                    case TipoDeLugar.CidadePequena: nivel = 1; break;
                    case TipoDeLugar.Vila: case TipoDeLugar.BaseMilitar: case TipoDeLugar.ComunidadeAliada: case TipoDeLugar.ComunidadeInimiga:
                    case TipoDeLugar.Laboratorio: case TipoDeLugar.Fazenda: case TipoDeLugar.CasaDoLago: case TipoDeLugar.CasaDaFamilia:
                        nivel = 2; break;
                    default: continue;
                }
                var ponto = l.Centro;
                // o nome da cidade fica em cima dela; o dos outros, embaixo do desenho
                if (l.EhCidade && l.Tipo != TipoDeLugar.Vila) ponto += Vector2.up * l.Raio * 0.15f;
                else if (!l.EhCidade) ponto += Vector2.down * (l.Raio + 30f);
                lista.Add(new Rotulo { Ponto = ponto, Texto = l.Nome, Nivel = nivel });
            }
            foreach (var c in plano.Cidades)
            {
                if (c.Tipo == TipoDeLugar.Vila) continue;
                foreach (var b in c.Bairros)
                    if (b.Tipo != TipoDeBairro.Centro && !(c.Tipo == TipoDeLugar.CidadePequena && b.Tipo == TipoDeBairro.Comercio))
                    {
                        // a casa da família fica no meio do bairro dela: o nome sobe, para o ícone não cobrir
                        var ponto = b.Centro;
                        var casa = plano.CasaDaFamilia;
                        if (casa != null && Vector2.Distance(ponto, casa.Centro) < 60f) ponto = casa.Centro + Vector2.up * 75f;
                        lista.Add(new Rotulo { Ponto = ponto, Texto = b.Nome, Nivel = 3 });
                    }
            }
            return lista;
        }

        public static Color32[] Pintar(DadosDoMundo d, int lado)
        {
            float mpp = d.Lado / lado;   // metros por pixel
            // as larguras e os ícones são medidos em pixels de uma imagem de 1024: numa maior, crescem junto (o mesmo
            // desenho, mais nítido de perto)
            float e = lado / 1024f;
            var px = new Color[lado * lado];
            var h = new float[lado * lado];
            // as duas passadas por pixel vão em paralelo, uma linha por vez (no grande em 2048, de 6 s para 1,1)
            Parallel.For(0, lado, j =>
            {
                for (int i = 0; i < lado; i++)
                    h[j * lado + i] = d.Altura(X(d, i, mpp), X(d, j, mpp));
            });

            // o chão: o relevo sombreado, a serra e o lago
            var luz = new Vector3(-0.55f, 0.7f, 0.45f).normalized;
            Parallel.For(0, lado, j =>
            {
            for (int i = 0; i < lado; i++)
            {
                int k = j * lado + i;
                float x = X(d, i, mpp), z = X(d, j, mpp);
                float dx = (h[j * lado + Mathf.Min(i + 1, lado - 1)] - h[j * lado + Mathf.Max(i - 1, 0)]) / (2f * mpp);
                float dz = (h[Mathf.Min(j + 1, lado - 1) * lado + i] - h[Mathf.Max(j - 1, 0) * lado + i]) / (2f * mpp);
                var n = new Vector3(-dx * 2.5f, 1f, -dz * 2.5f).normalized;
                float sombra = Mathf.Lerp(0.72f, 1.12f, Mathf.Clamp01(Vector3.Dot(n, luz) * 1.1f));
                var c = Color.Lerp(Terra, Serra, Geo.Suave(0.15f, 0.6f, d.Borda(x, z)));
                // as manchas de terra e de folhas, bem de leve
                var pesos = d.PesosDoChao(Mathf.Clamp(Mathf.RoundToInt((x + d.MetadeDaGrade) / DadosDoMundo.Celula), 0, d.Celulas),
                                          Mathf.Clamp(Mathf.RoundToInt((z + d.MetadeDaGrade) / DadosDoMundo.Celula), 0, d.Celulas));
                c = Color.Lerp(c, new Color(0.56f, 0.5f, 0.4f), pesos.g / 255f * 0.35f);
                c = Color.Lerp(c, new Color(0.58f, 0.55f, 0.42f), pesos.a / 255f * 0.25f);
                c *= sombra;
                if (d.Lago != null)
                {
                    float fundura = d.Lago.Nivel - h[k];
                    if (fundura > 0f && d.Lago.Q(x, z) < 1.35f)
                    {
                        c = Color.Lerp(Agua, AguaFunda, Mathf.Clamp01(fundura / 4f));
                        if (fundura < 0.4f) c = Color.Lerp(c, Contorno, 0.4f);
                    }
                }
                c.a = 1f;
                px[k] = c;
            }
            });

            var plano = d.Plano;
            if (plano != null)
            {
                foreach (var l in plano.Lugares) PintarArea(d, px, lado, mpp, e, l);
                // as estradas: os contornos de todas primeiro e o miolo por cima (os cruzamentos ficam limpos)
                foreach (var es in plano.Estradas) Linha(px, lado, d, mpp, es.Pontos, (Largura(es.Tipo) + 2.2f) * e, Contorno, 0.85f, 0f);
                foreach (var es in plano.Estradas)
                {
                    var cor = es.Tipo == TipoDeEstrada.Rodovia ? Rodovia : es.Tipo == TipoDeEstrada.Estrada ? EstradaCor : TerraCor;
                    Linha(px, lado, d, mpp, es.Pontos, Largura(es.Tipo) * e, cor, 1f, es.Tipo == TipoDeEstrada.Terra ? 7f * e : 0f);
                }
                foreach (var linha in plano.Metro)
                {
                    Linha(px, lado, d, mpp, linha.Estacoes, 1.6f * e, Metro, 0.9f, 5f * e);
                    foreach (var est in linha.Estacoes)
                    {
                        Disco(px, lado, d, mpp, est, 3.2f * e, Metro);
                        Disco(px, lado, d, mpp, est, 1.8f * e, Color.white);
                    }
                }
                // a cerca da quarentena: o quadrado tracejado em vermelho-escuro
                float c = d.Cerca;
                var cerca = new List<Vector2> { new Vector2(-c, -c), new Vector2(c, -c), new Vector2(c, c), new Vector2(-c, c), new Vector2(-c, -c) };
                Linha(px, lado, d, mpp, cerca, 2f * e, new Color(0.55f, 0.12f, 0.1f), 0.9f, 7f * e);
                foreach (var l in plano.Lugares) PintarIcone(px, lado, d, mpp, e, l);
            }

            var saida = new Color32[px.Length];
            for (int k = 0; k < px.Length; k++) saida[k] = px[k];
            return saida;
        }

        static float X(DadosDoMundo d, int i, float mpp) => -d.Metade + (i + 0.5f) * mpp;

        static float Largura(TipoDeEstrada t) => t == TipoDeEstrada.Rodovia ? 3.2f : t == TipoDeEstrada.Estrada ? 2.2f : 1.5f;

        // ------------------------------------------------------------------ as áreas

        static void PintarArea(DadosDoMundo d, Color[] px, int lado, float mpp, float e, Lugar l)
        {
            bool area = l.EhCidade || l.Tipo == TipoDeLugar.Fazenda || l.Tipo == TipoDeLugar.BaseMilitar || l.Tipo == TipoDeLugar.ComunidadeAliada
                        || l.Tipo == TipoDeLugar.ComunidadeInimiga || l.Tipo == TipoDeLugar.Laboratorio;
            if (!area) return;
            // (as cidades com os lotes da beira, que passam um pouco do contorno)
            float alcance = l.Raio * (l.EhCidade ? 1.45f : 1.2f);
            int i0 = Mathf.Max(0, Mathf.FloorToInt((l.Centro.x - alcance + d.Metade) / mpp));
            int i1 = Mathf.Min(lado - 1, Mathf.CeilToInt((l.Centro.x + alcance + d.Metade) / mpp));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((l.Centro.y - alcance + d.Metade) / mpp));
            int j1 = Mathf.Min(lado - 1, Mathf.CeilToInt((l.Centro.y + alcance + d.Metade) / mpp));
            float giro = l.Giro * Mathf.Deg2Rad, cos = Mathf.Cos(giro), sen = Mathf.Sin(giro);
            var cidade = l as Cidade;
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                float x = X(d, i, mpp), z = X(d, j, mpp);
                float rx = x - l.Centro.x, rz = z - l.Centro.y;
                // as coordenadas no giro do lugar (as ruas, as faixas, o quadrado da base)
                float u = rx * cos + rz * sen, v = -rx * sen + rz * cos;
                int k = j * lado + i;
                var antes = px[k];
                Color c;
                if (cidade != null)
                {
                    float q = cidade.Q(x, z);
                    if (q > 1.25f) continue;
                    var zona = cidade.ZonaEm(x, z);
                    var fundo = zona == TipoDeBairro.Centro ? CentroCor : zona == TipoDeBairro.Comercio ? ComercioCor : CasasCor;
                    var t = cidade.Tracado;
                    if (t == null)
                    {
                        if (q > 1f) continue;
                        c = fundo;
                    }
                    else
                    {
                        // as ruas, as construções e as praças de verdade (Parte M5); fora do contorno, só os lotes da beira
                        var oque = t.OQueTem(new Vector2(x, z));
                        if (q > 1f && oque == TracadoDaCidade.NoMapa.Nada) continue;
                        c = oque switch
                        {
                            TracadoDaCidade.NoMapa.Asfalto => Rua,
                            TracadoDaCidade.NoMapa.Calcada => Color.Lerp(Rua, fundo, 0.35f),
                            TracadoDaCidade.NoMapa.Construcao => Color.Lerp(fundo, Contorno, 0.5f),
                            TracadoDaCidade.NoMapa.Praca => new Color(0.42f, 0.5f, 0.32f),
                            TracadoDaCidade.NoMapa.Estacionamento => Color.Lerp(Rua, fundo, 0.55f),
                            _ => q > 1f ? Color.Lerp(antes, fundo, 0.4f) : fundo,
                        };
                    }
                    c = Color.Lerp(antes, c, 0.9f);
                }
                else if (l.Tipo == TipoDeLugar.Fazenda)
                {
                    if (rx * rx + rz * rz > l.Raio * l.Raio) continue;
                    bool faixa = Mathf.Repeat(u, 26f) < 13f;
                    c = Color.Lerp(antes, faixa ? new Color(0.62f, 0.62f, 0.4f) : new Color(0.53f, 0.5f, 0.33f), 0.6f);
                    if (Mathf.Abs(u) < 14f && Mathf.Abs(v) < 10f) c = new Color(0.45f, 0.33f, 0.24f);   // a casa e o celeiro
                }
                else
                {
                    // a base, o laboratório (quadrados) e as comunidades (redondas), com a cerca
                    bool quadrado = l.Tipo == TipoDeLugar.BaseMilitar || l.Tipo == TipoDeLugar.Laboratorio;
                    float meio = l.Raio * (quadrado ? 0.8f : 1f);
                    float dist = quadrado ? Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) : Mathf.Sqrt(rx * rx + rz * rz);
                    if (dist > meio) continue;
                    var miolo = l.Tipo == TipoDeLugar.BaseMilitar ? new Color(0.45f, 0.48f, 0.36f)
                              : l.Tipo == TipoDeLugar.Laboratorio ? new Color(0.5f, 0.46f, 0.56f) : new Color(0.62f, 0.58f, 0.48f);
                    var cerca = l.Tipo == TipoDeLugar.ComunidadeAliada ? new Color(0.22f, 0.5f, 0.26f)
                              : l.Tipo == TipoDeLugar.ComunidadeInimiga ? new Color(0.68f, 0.17f, 0.14f)
                              : l.Tipo == TipoDeLugar.Laboratorio ? new Color(0.3f, 0.16f, 0.4f) : Contorno;
                    c = dist > meio - mpp * 1.8f * e ? cerca : Color.Lerp(antes, miolo, 0.85f);
                }
                c.a = 1f;
                px[k] = c;
            }
        }

        // ------------------------------------------------------------------ os ícones

        static void PintarIcone(Color[] px, int lado, DadosDoMundo d, float mpp, float e, Lugar l)
        {
            var p = l.Centro;
            switch (l.Tipo)
            {
                case TipoDeLugar.Posto:
                    Quadrado(px, lado, d, mpp, p, 3.2f * e, Contorno);
                    Quadrado(px, lado, d, mpp, p, 2.2f * e, new Color(0.95f, 0.5f, 0.12f));
                    break;
                case TipoDeLugar.Bunker:
                    Quadrado(px, lado, d, mpp, p, 2.6f * e, new Color(0.12f, 0.12f, 0.12f));
                    Quadrado(px, lado, d, mpp, p, 1.1f * e, new Color(0.98f, 0.82f, 0.2f));
                    break;
                case TipoDeLugar.Cabana:
                    Triangulo(px, lado, d, mpp, p, 3.4f * e, Contorno);
                    Triangulo(px, lado, d, mpp, p, 2.3f * e, new Color(0.55f, 0.38f, 0.22f));
                    break;
                case TipoDeLugar.Barreira:
                    Xis(px, lado, d, mpp, p, 4.5f * e, new Color(0.8f, 0.1f, 0.1f));
                    break;
                case TipoDeLugar.CasaDoLago:
                    Quadrado(px, lado, d, mpp, p, 3f * e, Contorno);
                    Quadrado(px, lado, d, mpp, p, 2f * e, new Color(0.85f, 0.8f, 0.7f));
                    break;
                case TipoDeLugar.CasaDaFamilia:
                    Disco(px, lado, d, mpp, p, 5.5f * e, Contorno);
                    Disco(px, lado, d, mpp, p, 4.2f * e, new Color(1f, 0.85f, 0.15f));
                    Disco(px, lado, d, mpp, p, 1.6f * e, Contorno);
                    break;
            }
        }

        // as formas em pixels, com o tamanho em pixels (a meia largura)
        static void Forma(Color[] px, int lado, DadosDoMundo d, float mpp, Vector2 p, float raio, Color cor, System.Func<float, float, float> dentro)
        {
            float ci = (p.x + d.Metade) / mpp - 0.5f, cj = (p.y + d.Metade) / mpp - 0.5f;
            for (int j = Mathf.FloorToInt(cj - raio - 1); j <= Mathf.CeilToInt(cj + raio + 1); j++)
            for (int i = Mathf.FloorToInt(ci - raio - 1); i <= Mathf.CeilToInt(ci + raio + 1); i++)
            {
                if (i < 0 || j < 0 || i >= lado || j >= lado) continue;
                float a = Mathf.Clamp01(dentro(i - ci, j - cj) + 0.5f);
                if (a <= 0f) continue;
                int k = j * lado + i;
                var c = Color.Lerp(px[k], cor, a * cor.a);
                c.a = 1f;
                px[k] = c;
            }
        }

        static void Disco(Color[] px, int lado, DadosDoMundo d, float mpp, Vector2 p, float raio, Color cor) =>
            Forma(px, lado, d, mpp, p, raio, cor, (x, y) => raio - Mathf.Sqrt(x * x + y * y));

        static void Quadrado(Color[] px, int lado, DadosDoMundo d, float mpp, Vector2 p, float raio, Color cor) =>
            Forma(px, lado, d, mpp, p, raio, cor, (x, y) => raio - Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)));

        static void Triangulo(Color[] px, int lado, DadosDoMundo d, float mpp, Vector2 p, float raio, Color cor) =>
            Forma(px, lado, d, mpp, p, raio, cor, (x, y) => Mathf.Min(raio * 0.8f + y, raio * 0.9f - y * 0.5f - Mathf.Abs(x) * 0.9f));

        static void Xis(Color[] px, int lado, DadosDoMundo d, float mpp, Vector2 p, float raio, Color cor) =>
            Forma(px, lado, d, mpp, p, raio, cor, (x, y) => Mathf.Min(raio - Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)),
                raio * 0.27f - Mathf.Min(Mathf.Abs(x - y), Mathf.Abs(x + y)) * 0.7071f));

        // uma linha grossa (largura em pixels), com o traço opcional (o comprimento do traço em pixels; 0 = cheia)
        static void Linha(Color[] px, int lado, DadosDoMundo d, float mpp, List<Vector2> pontos, float largura, Color cor, float forca, float traco)
        {
            float percorrido = 0f;
            for (int s = 1; s < pontos.Count; s++)
            {
                var a = (pontos[s - 1] + Vector2.one * d.Metade) / mpp - Vector2.one * 0.5f;
                var b = (pontos[s] + Vector2.one * d.Metade) / mpp - Vector2.one * 0.5f;
                float comprimento = Vector2.Distance(a, b);
                float meia = largura * 0.5f;
                int i0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - meia - 1)), i1 = Mathf.Min(lado - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + meia + 1));
                int j0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - meia - 1)), j1 = Mathf.Min(lado - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + meia + 1));
                var ab = b - a;
                float ab2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    var p = new Vector2(i, j);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab2);
                    if (traco > 0f && Mathf.Repeat(percorrido + t * comprimento, traco * 2f) > traco) continue;
                    float dist = Vector2.Distance(p, a + ab * t);
                    float alfa = Mathf.Clamp01(meia - dist + 0.5f) * forca;
                    if (alfa <= 0f) continue;
                    int k = j * lado + i;
                    var c = Color.Lerp(px[k], cor, alfa);
                    c.a = 1f;
                    px[k] = c;
                }
                percorrido += comprimento;
            }
        }
    }
}
