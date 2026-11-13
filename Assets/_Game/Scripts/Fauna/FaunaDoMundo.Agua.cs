using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// O lago para os bichos (a etapa 3, 12/11/2026): o mundo gerado tem um lago só (<see cref="DadosDoMundo.Lago"/>, com a
    /// ilha no meio). Aqui, os pontos na água (pela fundura) e na margem, para nascer, pastar, pescar e pousar. No Lakewood
    /// (a parte 2 do "O jogo do mundo sorteado no Lakewood"), a água é o reservatório, os rios e o canal, cada um na sua
    /// altura: os pontos são procurados em volta de quem pede (FaunaDoMundo.Terreno.cs).
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        static DadosDoMundo DadosDoMapa => MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;

        static float Fundura(Vector3 p) => AguaEm(p.x, p.z, out _);

        /// <summary>A fundura da água no ponto (0 em terra).</summary>
        internal static float FunduraEm(Vector3 p) => Fundura(p);

        /// <summary>
        /// Um ponto na água do lago com a fundura entre 'min' e 'max', a uma distância de 'perto' entre 'de' e 'ate' (a altura
        /// é o nível da água). Falso se não achar (sem lago, ou longe demais).
        /// </summary>
        internal static bool PontoNaAgua(Vector3 perto, float de, float ate, float min, float max, out Vector3 p, int tentativas = 40)
        {
            p = default;
            var d = DadosDoMapa;
            if (d == null) return PontoNaAguaEmVolta(perto, de, ate, min, max, out p, tentativas);
            if (d.Lago == null) return false;
            var lago = d.Lago;
            for (int k = 0; k < tentativas; k++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                float q = Random.Range(0.25f, 1.02f);
                var c = new Vector3(lago.Centro.x + dx * lago.RaioNaDirecao(dx, dz) * q, lago.Nivel, lago.Centro.y + dz * lago.RaioNaDirecao(dx, dz) * q);
                float f = d.FunduraDaAgua(c.x, c.z);
                if (f < min || f > max) continue;
                float dist = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(perto.x, perto.z));
                if (dist < de || dist > ate) continue;
                p = c;
                return true;
            }
            return false;
        }

        /// <summary>Um ponto na terra, perto da água (a margem: de 'de' a 'ate' m da água), a até 'raio' m de 'perto'.</summary>
        internal static bool PontoNaMargem(Vector3 perto, float raio, float de, float ate, out Vector3 p)
        {
            p = default;
            var d = DadosDoMapa;
            if (d == null) return PontoNaMargemEmVolta(perto, raio, de, ate, out p);
            if (d.Lago == null) return false;
            var lago = d.Lago;
            for (int k = 0; k < 40; k++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                float r = lago.RaioNaDirecao(dx, dz);
                float fora = Random.Range(de, ate);
                var c = new Vector3(lago.Centro.x + dx * (r + fora), 0f, lago.Centro.y + dz * (r + fora));
                if (d.FunduraDaAgua(c.x, c.z) > 0.02f) continue;
                if (Vector2.Distance(new Vector2(c.x, c.z), new Vector2(perto.x, perto.z)) > raio) continue;
                c.y = d.Altura(c.x, c.z);
                p = c;
                return true;
            }
            return false;
        }

        /// <summary>A margem mais perto do ponto da água, na direção de fora do lago (o ninho dos gansos), a 'fora' m da água.</summary>
        internal static bool MargemNaDirecao(Vector3 p, float fora, out Vector3 m)
        {
            m = default;
            var d = DadosDoMapa;
            if (d == null) return MargemMaisPerto(p, fora, out m);
            if (d.Lago == null) return false;
            var lago = d.Lago;
            var para = new Vector2(p.x - lago.Centro.x, p.z - lago.Centro.y);
            if (para.sqrMagnitude < 0.01f) para = Vector2.right;
            para.Normalize();
            float r = lago.RaioNaDirecao(para.x, para.y) * 0.8f;
            for (float x = r; x < r * 1.6f + 40f; x += 1f)
            {
                var c = new Vector3(lago.Centro.x + para.x * x, 0f, lago.Centro.y + para.y * x);
                if (d.FunduraDaAgua(c.x, c.z) > 0.02f) continue;
                c += new Vector3(para.x, 0f, para.y) * fora;
                c.y = d.Altura(c.x, c.z);
                m = c;
                return true;
            }
            return false;
        }

        /// <summary>O ponto da água mais perto (procurando em volta), com a fundura mínima; para fugir para a água.</summary>
        internal static bool AguaMaisPerto(Vector3 de, float funduraMin, float raio, out Vector3 p)
        {
            p = default;
            if (!TemMapa) return false;
            float melhor = float.MaxValue;
            for (int a = 0; a < 16; a++)
                for (float r = 2f; r <= raio; r += 3f)
                {
                    var c = de + Quaternion.Euler(0f, a * 22.5f, 0f) * Vector3.forward * r;
                    if (AguaEm(c.x, c.z, out float nivel) < funduraMin) continue;
                    if (r < melhor)
                    {
                        melhor = r;
                        p = new Vector3(c.x, nivel, c.z);
                    }
                    break;
                }
            return melhor < float.MaxValue;
        }

        // ------------------------------------------------------------------ a água em volta (o Lakewood)

        // um ponto na água com a fundura entre min e max, no anel de 'de' a 'ate' m em volta de 'perto' (a altura: a da água)
        static bool PontoNaAguaEmVolta(Vector3 perto, float de, float ate, float min, float max, out Vector3 p, int tentativas)
        {
            p = default;
            if (!TemMapa) return false;
            for (int k = 0; k < tentativas; k++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float dist = Mathf.Sqrt(Random.Range(de * de, ate * ate));
                var c = new Vector3(perto.x + Mathf.Cos(ang) * dist, 0f, perto.z + Mathf.Sin(ang) * dist);
                float f = AguaEm(c.x, c.z, out float nivel);
                if (f < min || f > max) continue;
                p = new Vector3(c.x, nivel, c.z);
                return true;
            }
            return false;
        }

        // um ponto em terra a até 'raio' m de 'perto', de 'de' a 'ate' m da água
        static bool PontoNaMargemEmVolta(Vector3 perto, float raio, float de, float ate, out Vector3 p)
        {
            p = default;
            var chao = ChaoDoLakewood;
            if (chao == null) return false;
            for (int k = 0; k < 40; k++)
            {
                Vector2 r = Random.insideUnitCircle * raio;
                float x = perto.x + r.x, z = perto.z + r.y;
                if (chao.Agua(x, z, out _, de) || !chao.Agua(x, z, out _, ate)) continue;
                p = new Vector3(x, chao.Altura(x, z), z);
                return true;
            }
            return false;
        }

        // a terra mais perto de um ponto da água (em 16 rumos, até 120 m), e 'fora' m além dela
        static bool MargemMaisPerto(Vector3 p, float fora, out Vector3 m)
        {
            m = default;
            if (!TemMapa) return false;
            float melhor = float.MaxValue;
            for (int a = 0; a < 16; a++)
            {
                var dir = Quaternion.Euler(0f, a * 22.5f, 0f) * Vector3.forward;
                for (float r = 1f; r <= 120f && r < melhor; r += 1.5f)
                {
                    var c = p + dir * r;
                    if (AguaEm(c.x, c.z, out _) > 0.02f) continue;
                    melhor = r;
                    c += dir * fora;
                    c.y = AlturaDoMapa(c.x, c.z);
                    m = c;
                    break;
                }
            }
            return melhor < float.MaxValue;
        }

        /// <summary>
        /// Onde nasce um grupo das espécies da água, em volta do jogador (de 'de' a 'ate' m): o salmão no raso, os patos e
        /// os gansos boiando, a águia no céu do lago, o castor na toca (FaunaDoMundo.Castores.cs), o alce na margem. Falso:
        /// a espécie não é da água (o povoamento usa o anel normal).
        /// </summary>
        internal bool LugarNaAgua(Especie e, Vector3 pes, float de, float ate, out Vector3 p, out bool achou)
        {
            p = default;
            achou = false;
            switch (e)
            {
                case Especie.Salmao:
                    achou = PontoNaAgua(pes, de, ate, 0.6f, 1.8f, out p);
                    return true;
                case Especie.Pato:
                case Especie.Ganso:
                    achou = PontoNaAgua(pes, de, ate, 0.5f, 99f, out p);
                    return true;
                case Especie.Aguia:
                    achou = PontoNaAgua(pes, de, ate, 1f, 99f, out p);
                    return true;
                case Especie.Castor:
                    achou = TocaParaNascer(pes, de, ate, out p);
                    return true;
                case Especie.Alce:
                    achou = PontoNaMargem(pes, ate, 3f, 30f, out p) && Vector3.Distance(p, pes) >= de;
                    return true;
            }
            return false;
        }

        /// <summary>O pasto novo dos bichos da água (perto do antigo): o cardume no raso, o bando boiando, o alce no raso ou na margem.</summary>
        internal bool PastoNaAgua(Vector3 antes, Especie e, out Vector3 p)
        {
            p = default;
            switch (e)
            {
                case Especie.Salmao: return PontoNaAgua(antes, 4f, 25f, 0.6f, 1.8f, out p) || PontoNaAgua(antes, 0f, 80f, 0.6f, 1.8f, out p);
                case Especie.Pato:
                case Especie.Ganso: return PontoNaAgua(antes, 8f, 40f, 0.5f, 99f, out p) || PontoNaAgua(antes, 0f, 90f, 0.5f, 99f, out p);
                case Especie.Alce:
                    // o alce: metade das vezes come a taboa no raso, de cabeça dentro da água; senão, na margem
                    if (Random.value < 0.5f && PontoNaAgua(antes, 5f, 45f, 0.25f, 0.8f, out p)) return true;
                    return PontoNaMargem(antes, 45f, 2f, 20f, out p);
            }
            return false;
        }
    }
}
