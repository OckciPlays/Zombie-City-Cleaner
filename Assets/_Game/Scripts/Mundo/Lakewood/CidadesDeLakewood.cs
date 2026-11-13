using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>O que vai num lote de uma cidade do Lakewood (Plano_Lakewood, Etapa 6).</summary>
    public enum UsoDoLote
    {
        /// <summary>Uma casa do kit das construções (a térrea ou o sobrado).</summary>
        Casa,
        /// <summary>Uma loja do kit (o mercado, a farmácia, a 24 horas...).</summary>
        Loja,
        /// <summary>O lote vazio: a mata tomou conta.</summary>
        Vazio,
        /// <summary>O posto com a loja de conveniência.</summary>
        Posto,
        /// <summary>O Hospital Principal (o LAKEWOOD COUNTY HOSPITAL).</summary>
        Hospital,
        /// <summary>A loja de armas nova (a Gun Store &amp; Shooting Range).</summary>
        LojaDeArmas,
        /// <summary>Um prédio que ainda vai ser feito (o nome diz qual: a igreja, a escola, os bombeiros...).</summary>
        Especial,
        /// <summary>Uma cabana de madeira (a fazer).</summary>
        Cabana,
        /// <summary>
        /// O acampamento dos comerciantes (12/11/2026, a comunidade: a parte 6 do "O jogo do mundo sorteado no Lakewood"):
        /// o chão plano do lote; a cerca, a casa, as barracas e as pessoas são do AcampamentosDoMundo.
        /// </summary>
        Acampamento,
    }

    /// <summary>
    /// Uma rua nova de uma cidade do Lakewood (Plano_Lakewood, Etapa 6): retas entre os pontos do meio dela, sem suavizar
    /// (as ruas de uma vila são retas). As pontas que encostam numa estrada da Etapa 5 ou em outra rua ficam em cima da
    /// linha do meio dela (a ferramenta liga).
    /// </summary>
    [Serializable]
    public class RuaDaCidade
    {
        public string nome;
        [Tooltip("A cidade dela (o nome em MapaDeLakewood.cidades).")] public string cidade;
        [Tooltip("Os pontos do meio, em ordem (retas entre eles).")] public List<Vector2> pontos = new List<Vector2>();
        [Tooltip("A largura do asfalto (m).")] public float largura = 7f;
        [Tooltip("A última ponta volta para a primeira (a alça das cabanas).")] public bool fechada;

        public float MeiaLargura => largura * 0.5f;

        /// <summary>A distância do ponto até a linha do meio (m).</summary>
        public float Distancia(Vector2 p) => pontos.Count >= 2 ? Linhas.Distancia(p, pontos, fechada) : float.MaxValue;
    }

    /// <summary>
    /// Um lote de uma cidade do Lakewood (Plano_Lakewood, Etapa 6): um retângulo de frente para uma rua, com o que vai nele.
    /// Nas coordenadas do lote, o x vai pela frente (o <see cref="Lado"/>) e o z da frente para os fundos (o
    /// <see cref="Fundos"/>), com a origem no meio: a frente fica em z = -fundo / 2. É o espaço de uma obra do kit com o
    /// giro do lote (a frente da obra em -z).
    /// </summary>
    [Serializable]
    public class LoteDoMapa
    {
        [Tooltip("A cidade dele.")] public string cidade;
        public UsoDoLote tipo;
        [Tooltip("Nos especiais, qual é (\"igreja\", \"escola\", \"bombeiros\", \"guarda florestal\", \"motel\"); nos outros, vazio.")] public string nome;
        [Tooltip("A rua ou a estrada da frente.")] public string rua;
        [Tooltip("O meio do lote no mapa.")] public Vector2 meio;
        [Tooltip("O giro (graus, como o y de um objeto da Unity cuja frente é o -z dele): 0 a frente para o sul, 90 para o oeste, 180 para o norte, -90 para o leste.")]
        public float giro;
        [Tooltip("Ao longo da frente (m).")] public float largura;
        [Tooltip("Da frente aos fundos (m).")] public float fundo;
        [Tooltip("O sorteio do que vai nele (a planta, as cores).")] public int semente;

        /// <summary>Para onde a frente olha (a rua fica desse lado).</summary>
        public Vector2 Frente
        {
            get
            {
                float g = giro * Mathf.Deg2Rad;
                return new Vector2(-Mathf.Sin(g), -Mathf.Cos(g));
            }
        }

        public Vector2 Fundos => -Frente;

        /// <summary>O x do lote (o +x da obra girada): à direita de quem está na rua olhando o lote.</summary>
        public Vector2 Lado
        {
            get
            {
                float g = giro * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(g), -Mathf.Sin(g));
            }
        }

        /// <summary>O giro de quem olha para a frente dada (unitária).</summary>
        public static float GiroPara(Vector2 frente) => Mathf.Atan2(-frente.x, -frente.y) * Mathf.Rad2Deg;

        /// <summary>Um ponto do lote (x pela frente, z da frente para os fundos, a partir do meio) no mapa.</summary>
        public Vector2 NoMapa(float x, float z) => meio + Lado * x + Fundos * z;

        /// <summary>O meio da beira da frente, no mapa.</summary>
        public Vector2 MeioDaFrente => meio + Frente * (fundo * 0.5f);

        /// <summary>O ponto do mapa nas coordenadas do lote.</summary>
        public Vector2 NoLote(Vector2 p)
        {
            var q = p - meio;
            return new Vector2(Vector2.Dot(q, Lado), Vector2.Dot(q, Fundos));
        }

        /// <summary>Se o ponto fica dentro do lote (com a folga: positiva, o lote maior).</summary>
        public bool Contem(Vector2 p, float folga = 0f)
        {
            var l = NoLote(p);
            return Mathf.Abs(l.x) <= largura * 0.5f + folga && Mathf.Abs(l.y) <= fundo * 0.5f + folga;
        }

        /// <summary>Os quatro cantos no mapa (a frente primeiro).</summary>
        public Vector2[] Cantos()
        {
            float a = largura * 0.5f, b = fundo * 0.5f;
            return new[] { NoMapa(-a, -b), NoMapa(a, -b), NoMapa(a, b), NoMapa(-a, b) };
        }

        /// <summary>Se dois lotes se encostam (com a folga), pelos eixos separadores dos dois retângulos.</summary>
        public static bool Encostam(LoteDoMapa a, LoteDoMapa b, float folga = 0f)
        {
            var ca = a.Cantos();
            var cb = b.Cantos();
            foreach (var eixo in new[] { a.Lado, a.Frente, b.Lado, b.Frente })
            {
                float a0 = float.MaxValue, a1 = float.MinValue, b0 = float.MaxValue, b1 = float.MinValue;
                foreach (var p in ca) { float d = Vector2.Dot(p, eixo); a0 = Mathf.Min(a0, d); a1 = Mathf.Max(a1, d); }
                foreach (var p in cb) { float d = Vector2.Dot(p, eixo); b0 = Mathf.Min(b0, d); b1 = Mathf.Max(b1, d); }
                if (a1 + folga <= b0 || b1 + folga <= a0) return false;
            }
            return true;
        }

        /// <summary>O nome que aparece no plano e nos relatórios.</summary>
        public string Descricao => tipo switch
        {
            UsoDoLote.Casa => "casa",
            UsoDoLote.Loja => "loja",
            UsoDoLote.Vazio => "lote vazio",
            UsoDoLote.Posto => "posto",
            UsoDoLote.Hospital => "hospital",
            UsoDoLote.LojaDeArmas => "loja de armas",
            UsoDoLote.Cabana => "cabana",
            UsoDoLote.Acampamento => "acampamento dos comerciantes",
            _ => string.IsNullOrEmpty(nome) ? "especial" : nome,
        };
    }

    /// <summary>As contas dos lotes no chão do Lakewood (a ferramenta da Etapa 6 e os testes usam as mesmas).</summary>
    public static class LotesDeLakewood
    {
        /// <summary>O desnível máximo de um lote comum (m): mais que isso, o patamar viraria um muro de arrimo.</summary>
        public const float DesnivelMaximo = 2.5f;

        /// <summary>O desnível máximo dos lotes grandes (o hospital, a escola...): o patamar deles é feito na parte 2.</summary>
        public const float DesnivelMaximoDosGrandes = 6f;

        /// <summary>A distância mínima dos rios (m).</summary>
        public const float LongeDosRios = 14f;

        /// <summary>Se o lote é dos que precisam de pouco desnível (os comuns, enchidos ao longo das ruas).</summary>
        public static bool Comum(LoteDoMapa l) =>
            l.tipo == UsoDoLote.Casa || l.tipo == UsoDoLote.Loja || l.tipo == UsoDoLote.Vazio || l.tipo == UsoDoLote.Cabana;

        /// <summary>
        /// O desnível do relevo no lote (de 5 × 5 pontos: o chão de verdade, sem os patamares dos lotes nem os cortes das
        /// estradas, para o desenho sair igual toda vez), e se ele toca a água ou fica perto de um rio.
        /// </summary>
        public static float Desnivel(ChaoDeLakewood chao, LoteDoMapa l, out bool agua)
        {
            float hmin = float.MaxValue, hmax = float.MinValue;
            agua = false;
            for (int a = 0; a <= 4; a++)
            for (int b = 0; b <= 4; b++)
            {
                var p = l.NoMapa((a / 4f - 0.5f) * l.largura, (b / 4f - 0.5f) * l.fundo);
                float h = chao.Relevo.Altura(p.x, p.y);
                if ((chao.Agua(p.x, p.y, out float nivel, 4f) && nivel > h - 3f) || chao.RioMaisPerto(p.x, p.y, LongeDosRios, out _)) agua = true;
                hmin = Mathf.Min(hmin, h);
                hmax = Mathf.Max(hmax, h);
            }
            return hmax - hmin;
        }
    }
}
