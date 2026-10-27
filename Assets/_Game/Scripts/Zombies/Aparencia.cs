using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// A aparência dos zumbis leves (24/10/2026, shader ZombieCityCleaner/Zumbi): cada zumbi tem a sua cópia do material,
    /// com as cores da pele, das roupas, do cabelo e dos olhos, a camada de sangue e a de sujeira (das 10 do modelo) e as
    /// marcas do rosto (machucados, olho faltando), sorteadas pelo Id e pela geração. O sorteio é o mesmo em todas as
    /// máquinas: nada vai pela rede.
    /// O rosto troca de expressão pelo estado do zumbi (vagando, alerta, perseguindo, atacando, levando tiro, morto).
    /// As listas de cores e os quadros da folha de rostos são os mesmos da prévia do Blender (previa_leves.py e
    /// rostos_leves.py, na pasta de arte).
    /// </summary>
    public static class Aparencia
    {
        public enum Expressao : byte { Vagando, Parado, Alerta, Furia, Ataque, Dor, Morto }

        public static readonly int CorPeleId = Shader.PropertyToID("_CorPele");
        public static readonly int CorCimaId = Shader.PropertyToID("_CorCima");
        public static readonly int CorBaixoId = Shader.PropertyToID("_CorBaixo");
        public static readonly int CorCabeloId = Shader.PropertyToID("_CorCabelo");
        public static readonly int CorOlhosId = Shader.PropertyToID("_CorOlhos");
        public static readonly int CamadasId = Shader.PropertyToID("_Camadas");
        public static readonly int QuadrosId = Shader.PropertyToID("_Quadros");

        // as cores (sRGB), as mesmas do previa_leves.py. A pele é sempre verde (a pedido, sem roxo, azul nem verde muito
        // claro): do verde acinzentado ao escuro
        static readonly Color[] Peles =
        {
            C(0.52f, 0.60f, 0.45f), C(0.46f, 0.56f, 0.40f), C(0.50f, 0.55f, 0.38f), C(0.42f, 0.50f, 0.38f),
            C(0.38f, 0.47f, 0.33f), C(0.48f, 0.54f, 0.46f),
        };
        static readonly Color[] RoupasDeCima =
        {
            C(0.86f, 0.85f, 0.82f), C(0.50f, 0.50f, 0.50f), C(0.12f, 0.12f, 0.13f), C(0.55f, 0.10f, 0.10f),
            C(0.12f, 0.17f, 0.32f), C(0.33f, 0.36f, 0.20f), C(0.72f, 0.58f, 0.20f), C(0.48f, 0.62f, 0.75f),
            C(0.40f, 0.28f, 0.18f), C(0.18f, 0.40f, 0.25f),
        };
        static readonly Color[] RoupasDeBaixo =
        {
            C(0.355f, 0.404f, 0.496f), C(0.15f, 0.19f, 0.30f), C(0.10f, 0.10f, 0.11f), C(0.62f, 0.55f, 0.38f),
            C(0.38f, 0.38f, 0.40f), C(0.35f, 0.25f, 0.17f), C(0.30f, 0.32f, 0.20f),
        };
        static readonly Color[] Cabelos =
        {
            C(0.449f, 0.234f, 0.153f), C(0.08f, 0.07f, 0.07f), C(0.75f, 0.60f, 0.32f), C(0.62f, 0.27f, 0.10f),
            C(0.55f, 0.55f, 0.55f), C(0.22f, 0.13f, 0.08f),
        };
        static readonly Color[] Olhos =
        {
            C(0.85f, 0.85f, 0.78f), C(0.78f, 0.70f, 0.20f), C(0.70f, 0.10f, 0.08f), C(0.55f, 0.70f, 0.80f),
            C(0.35f, 0.55f, 0.25f), C(0.35f, 0.20f, 0.10f), C(0.10f, 0.09f, 0.09f),
        };

        // a folha de rostos (rostos_leves.py): 8 quadros por linha; linha 0 os olhos, 1 as bocas, 2 e 3 as marcas
        const int CamadasDeSujeira = 10;
        const int PrimeiraMarca = 16, Marcas = 13, SemOlhoDireito = 29, SemOlhoEsquerdo = 30;
        // expressão → (olhos, boca)
        static readonly Vector2[] Quadros =
        {
            new Vector2(0, 8),    // Vagando: pálpebras pesadas, boca fechada
            new Vector2(6, 8),    // Parado (os que "dormem" em pé): olhos fechados
            new Vector2(1, 9),    // Alerta (investigando, procurando): olhos abertos, gemendo
            new Vector2(2, 10),   // Fúria (perseguindo): sobrancelhas em V, rosnando
            new Vector2(3, 11),   // Ataque: arregalado, mordendo
            new Vector2(4, 12),   // Dor (levou tiro): olhos apertados, dentes cerrados
            new Vector2(5, 13),   // Morto: olhos virados, boca caída
        };

        /// <summary>O que foi sorteado para um zumbi.</summary>
        public struct Variacao
        {
            public Color pele, cima, baixo, cabelo, olhos;
            public Vector4 camadas;   // sangue, força do sangue, sujeira, força da sujeira
            public int marca1, marca2;
            /// <summary>Parado, ele fecha os olhos ("dorme" em pé).</summary>
            public bool dorme;
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>Um gerador pequeno e igual em todas as máquinas (xorshift), pelo Id e pela geração do zumbi.</summary>
        struct Sorteio
        {
            uint _s;
            public Sorteio(int id, byte geracao) { _s = (uint)id * 2654435761u ^ (uint)geracao * 40503u ^ 0x9E3779B9u; if (_s == 0) _s = 1; Proximo(); }
            public uint Proximo() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
            public float Valor() => (Proximo() & 0xFFFFFF) / 16777216f;
            public float Entre(float a, float b) => a + (b - a) * Valor();
            public int Ate(int n) => (int)(Valor() * n) % n;
            public T Um<T>(T[] lista) => lista[Ate(lista.Length)];
        }

        // um pouco mais claro ou mais escuro, para as cores das listas não se repetirem iguais
        static Color Tom(ref Sorteio r, Color c, float quanto)
        {
            float k = r.Entre(1f - quanto, 1f + quanto * 0.6f);
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
        }

        public static Variacao Sortear(int id, byte geracao)
        {
            var r = new Sorteio(id, geracao);
            var v = new Variacao
            {
                pele = Tom(ref r, r.Um(Peles), 0.08f),
                cima = Tom(ref r, r.Um(RoupasDeCima), 0.15f),
                baixo = Tom(ref r, r.Um(RoupasDeBaixo), 0.15f),
                cabelo = Tom(ref r, r.Um(Cabelos), 0.12f),
                olhos = r.Um(Olhos),
            };
            // o sangue: quase todos sujos, alguns quase limpos
            float sangue = r.Valor() < 0.12f ? r.Entre(0.15f, 0.35f) : r.Entre(0.55f, 1f);
            v.camadas = new Vector4(r.Ate(CamadasDeSujeira), sangue, r.Ate(CamadasDeSujeira), r.Entre(0.35f, 1f));
            v.marca1 = r.Valor() < 0.6f ? PrimeiraMarca + r.Ate(Marcas) : -1;
            float olho = r.Valor();
            v.marca2 = olho < 0.08f ? SemOlhoDireito : olho < 0.16f ? SemOlhoEsquerdo : -1;
            v.dorme = r.Valor() < 0.3f;
            return v;
        }

        /// <summary>As cores e as camadas no material do zumbi (a cópia dele).</summary>
        public static void Aplicar(Material m, in Variacao v, Expressao e)
        {
            m.SetColor(CorPeleId, v.pele);
            m.SetColor(CorCimaId, v.cima);
            m.SetColor(CorBaixoId, v.baixo);
            m.SetColor(CorCabeloId, v.cabelo);
            m.SetColor(CorOlhosId, v.olhos);
            m.SetVector(CamadasId, v.camadas);
            m.SetVector(QuadrosId, QuadrosDe(e, v));
        }

        public static Vector4 QuadrosDe(Expressao e, in Variacao v)
        {
            var q = Quadros[(int)e];
            return new Vector4(q.x, q.y, v.marca1, v.marca2);
        }

        /// <summary>O rosto de morto com as mesmas marcas (o corpo que cai copia os quadros do zumbi).</summary>
        public static Vector4 Morto(Vector4 quadros)
        {
            var q = Quadros[(int)Expressao.Morto];
            return new Vector4(q.x, q.y, quadros.z, quadros.w);
        }

        /// <summary>O material é do shader dos zumbis leves (os modelos antigos usam o URP Lit e não mudam).</summary>
        public static bool Leve(Material m) => m != null && m.HasProperty(QuadrosId);
    }
}
