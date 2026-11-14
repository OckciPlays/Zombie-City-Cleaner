using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// Os rios do Lakewood para os bichos (13/11/2026, a pedido: "deveria ter os castores spawnando e fazendo as barragens
    /// nesses rios"). Antes os bichos da água sorteavam qualquer água em volta (o reservatório quase sempre ganhava); agora
    /// procuram os rios de propósito: o salmão sobe os riachos (3 em 4 vezes), os patos boiam nos rios também (2 em 5) e os
    /// castores fazem a família nos riachos estreitos (de 4 a 16 m), com a toca no remanso e a represa atravessada no rio,
    /// uns 10 m abaixo dela. A represa cresce com os dias e com os galhos (no save, junto da toca). O mundo sorteado não tem
    /// rios: lá, tudo como antes.
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        /// <summary>As tocas nos rios (as do lago continuam no máximo 2).</summary>
        const int MaxTocasNoRio = 4;

        struct PontoDoRio
        {
            public int rio, i;
        }

        static readonly List<PontoDoRio> _pontosDoRio = new List<PontoDoRio>(512);

        /// <summary>O mapa tem rios (o Lakewood).</summary>
        internal static bool TemRios
        {
            get
            {
                var chao = ChaoDoLakewood;
                return chao != null && chao.Relevo.LinhasDosRios != null && chao.Relevo.LinhasDosRios.Length > 0;
            }
        }

        /// <summary>
        /// Um ponto no meio de um rio, de 'de' a 'ate' m do ponto dado, onde o rio tem de 'larguraMin' a 'larguraMax' m (fora
        /// dos lagos e longe das pontes). Devolve o ponto (y na água), a direção da correnteza (no chão) e a largura ali.
        /// </summary>
        internal static bool PontoNoRio(Vector3 perto, float de, float ate, float larguraMin, float larguraMax, out Vector3 p, out Vector3 correnteza, out float largura)
        {
            p = default;
            correnteza = Vector3.forward;
            largura = 0f;
            var chao = ChaoDoLakewood;
            if (chao == null) return false;
            var rel = chao.Relevo;
            var linhas = rel.LinhasDosRios;
            if (linhas == null) return false;
            _pontosDoRio.Clear();
            float de2 = de * de, ate2 = ate * ate;
            for (int r = 0; r < linhas.Length && r < chao.Mapa.rios.Count; r++)
            {
                var L = linhas[r];
                if (L == null || L.Count < 5) continue;
                var fr = rel.FracoesDosRios[r];
                for (int i = 2; i < L.Count - 2; i += 3)
                {
                    float dx = L[i].x - perto.x, dz = L[i].y - perto.z, d2 = dx * dx + dz * dz;
                    if (d2 < de2 || d2 > ate2) continue;
                    float w = MapaDeLakewood.LarguraDoRio(chao.Mapa.rios[r], fr[i]);
                    if (w < larguraMin || w > larguraMax) continue;
                    _pontosDoRio.Add(new PontoDoRio { rio = r, i = i });
                    if (_pontosDoRio.Count >= 400) break;
                }
            }
            // o sorteio: uns pontos ao acaso (no lago e embaixo das pontes, não)
            for (int t = 0; t < 12 && _pontosDoRio.Count > 0; t++)
            {
                int k = Random.Range(0, _pontosDoRio.Count);
                var pr = _pontosDoRio[k];
                _pontosDoRio.RemoveAt(k);
                var L = linhas[pr.rio];
                Vector2 c = L[pr.i];
                if (chao.NoLago(c.x, c.y)) continue;
                if (chao.EstradaPerto(c.x, c.y, 30f, out _, out _, out _, out _)) continue;   // (13/11: longe das pontes e das estradas)
                float nivel = rel.NiveisDosRios[pr.rio][pr.i];
                if (AguaEm(c.x, c.y, out float nivelAqui) > 0.2f) nivel = nivelAqui;
                else continue;
                Vector2 dir = L[pr.i + 1] - L[pr.i - 1];
                if (dir.sqrMagnitude < 0.0001f) continue;
                dir.Normalize();
                p = new Vector3(c.x, nivel, c.y);
                correnteza = new Vector3(dir.x, 0f, dir.y);
                largura = MapaDeLakewood.LarguraDoRio(chao.Mapa.rios[pr.rio], rel.FracoesDosRios[pr.rio][pr.i]);
                return true;
            }
            return false;
        }

        /// <summary>Um ponto na água do rio perto do meio dado (até 'fora' da largura para os lados), com a fundura pedida.</summary>
        static bool NaAguaDoRio(Vector3 meio, Vector3 correnteza, float largura, float fora, float fundMin, float fundMax, out Vector3 p)
        {
            Vector3 lado = Vector3.Cross(Vector3.up, correnteza);
            for (int t = 0; t < 8; t++)
            {
                Vector3 q = meio + lado * Random.Range(-fora, fora) * largura * 0.5f + correnteza * Random.Range(-4f, 4f);
                float f = AguaEm(q.x, q.z, out float nivel);
                if (f < fundMin || f > fundMax) continue;
                p = new Vector3(q.x, nivel, q.z);
                return true;
            }
            p = meio;
            return AguaEm(meio.x, meio.z, out _) >= fundMin;
        }

        // ------------------------------------------------------------------ o castor no riacho

        /// <summary>
        /// Uma família nova de castores num riacho (de 4 a 16 m), de 'de' a 'ate' m do jogador: a represa atravessada no rio e a
        /// toca uns 10 m acima dela (no remanso, perto de uma margem). Falso: sem riacho no anel, ou já há tocas demais.
        /// </summary>
        bool TocaNoRio(Vector3 pes, float de, float ate, out Vector3 p)
        {
            p = default;
            int noRio = 0;
            foreach (var t in _tocas) if (t.noRio) noRio++;
            if (noRio >= MaxTocasNoRio) return false;
            if (!PontoNoRio(pes, de, ate, 4f, 16f, out var represa, out var correnteza, out float largura)) return false;
            foreach (var t in _tocas) if (Vector2.Distance(new Vector2(t.x, t.z), new Vector2(represa.x, represa.z)) < 150f) return false;
            // a toca: acima da represa (contra a correnteza), mais para uma margem, onde ainda é fundo (de 0,3 a 1,6 m)
            Vector3 lado = Vector3.Cross(Vector3.up, correnteza) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 toca = represa - correnteza * Random.Range(8f, 12f);
            Vector3 q = toca + lado * largura * 0.25f;
            float f = AguaEm(q.x, q.z, out float nivel);
            if (f >= 0.3f && f <= 1.6f) toca = new Vector3(q.x, nivel, q.z);
            else if (AguaEm(toca.x, toca.z, out nivel) >= 0.3f) toca.y = nivel;
            else return false;
            _tocas.Add(new TocaDoCastor
            {
                x = toca.x, z = toca.z, tamanho = 0.35f, criadaNaHora = HorasDoJogo,
                noRio = true, rx = represa.x, ry = represa.y, rz = represa.z,
                giro = Quaternion.LookRotation(correnteza).eulerAngles.y, largura = largura, represa = 0.25f,
            });
            p = toca;
            return true;
        }

        /// <summary>A represa agora (0 a 1): a dos galhos, mais um pouco a cada dia (8% por dia).</summary>
        static float TamanhoDaRepresa(TocaDoCastor t) => Mathf.Clamp01(t.represa + (float)(HorasDoJogo - t.criadaNaHora) / 24f * 0.08f);

        /// <summary>O galho (ou o barro) chegou à represa mais perto do ponto: ela cresce.</summary>
        internal void CrescerRepresa(Vector3 perto, float quanto)
        {
            int melhor = -1;
            float m = 20f;
            for (int i = 0; i < _tocas.Count; i++)
            {
                if (!_tocas[i].noRio) continue;
                float d = Vector2.Distance(new Vector2(_tocas[i].rx, _tocas[i].rz), new Vector2(perto.x, perto.z));
                if (d < m) { m = d; melhor = i; }
            }
            if (melhor < 0) return;
            var t = _tocas[melhor];
            t.represa = Mathf.Clamp01(t.represa + quanto);
            _tocas[melhor] = t;
        }

        /// <summary>A família que nasce na toca do ponto fica sabendo da represa dela (o grupo leva os galhos até lá).</summary>
        void RepresaDoGrupo(Grupo g)
        {
            foreach (var t in _tocas)
            {
                if (!t.noRio || Vector2.Distance(new Vector2(t.x, t.z), new Vector2(g.toca.x, g.toca.z)) > 3f) continue;
                g.temRepresa = true;
                g.represa = new Vector3(t.rx, t.ry, t.rz);
                g.represaGiro = t.giro;
                g.represaLargura = t.largura;
                return;
            }
        }

        // ------------------------------------------------------------------ o F1

        /// <summary>O F1, em cada máquina: o riacho mais perto (de 4 a 16 m de largura, a até 3 km): a margem, de frente para ele.</summary>
        public static bool LugarNoRiacho(JogadorRede j, out Vector3 margem, out float giro)
        {
            margem = default;
            giro = 0f;
            if (j == null) return false;
            Vector3 pes = j.transform.position;
            for (float ate = 300f; ate <= 3000f; ate *= 2f)
            {
                if (!PontoNoRio(pes, 0f, ate, 4f, 16f, out var meio, out var correnteza, out float largura)) continue;
                Vector3 lado = Vector3.Cross(Vector3.up, correnteza);
                for (float fora = largura * 0.5f + 2f; fora < largura * 0.5f + 14f; fora += 1.5f)
                {
                    Vector3 q = meio + lado * fora;
                    if (AguaEm(q.x, q.z, out _) > 0.02f) continue;
                    margem = new Vector3(q.x, AlturaDoMapa(q.x, q.z), q.z);
                    giro = Quaternion.LookRotation(-lado).eulerAngles.y;
                    return true;
                }
            }
            return false;
        }

        /// <summary>O F1 (no anfitrião): uma família de castores no riacho mais perto, com a toca e a represa.</summary>
        public string CastoresNoRiacho(JogadorRede j)
        {
            if (!_servidor || j == null) return "Só o anfitrião.";
            if (!TemRios) return "Este mapa não tem rios (só o Lakewood).";
            Vector3 pes = j.transform.position;
            Vector3 toca = default;
            bool achou = false;
            // a toca de rio mais perto, se já houver uma; senão, uma nova (perto primeiro)
            float m = 400f;
            foreach (var t in _tocas)
            {
                if (!t.noRio) continue;
                float d = Vector2.Distance(new Vector2(t.x, t.z), new Vector2(pes.x, pes.z));
                if (d < m) { m = d; toca = new Vector3(t.x, NivelEm(t.x, t.z), t.z); achou = true; }
            }
            for (float ate = 120f; !achou && ate <= 3000f; ate *= 2f)
            {
                if (_tocas.Count > 0 && CountNoRio() >= MaxTocasNoRio) _tocas.RemoveAll(t => t.noRio);   // (o teste abre lugar)
                achou = TocaNoRio(pes, 0f, ate, out toca);
            }
            if (!achou) return "Sem riacho por perto (de 4 a 16 m de largura).";
            int n = NascerGrupo(Especie.Castor, toca, true);
            return n > 0 ? $"{n} castores na toca do riacho a {Vector3.Distance(pes, toca):0} m (a represa fica uns 10 m abaixo dela)." : "Não deu para pôr os castores.";
        }

        int CountNoRio()
        {
            int n = 0;
            foreach (var t in _tocas) if (t.noRio) n++;
            return n;
        }
    }
}
