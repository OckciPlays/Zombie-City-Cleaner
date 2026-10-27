using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O que fica nas ruas das cidades e das vilas (Plano_Mapa, Parte M5), em dados, junto com a beira das estradas
    /// (<see cref="ObjetoDaEstrada"/>, mostrados pelos mesmos blocos):
    ///  - <b>os postes</b> na calçada, junto do meio-fio, com o braço sobre a rua: nas avenidas, dos dois lados (a cada
    ///    ~26 m, desencontrados); nas ruas, alternando os lados (a cada ~32 m; nas vilas, ~40 m). Alguns quebrados e alguns
    ///    piscando (a variante: <see cref="PosteQuebrado"/>, <see cref="PostePisca"/>);
    ///  - <b>a placa de rua</b> em cada cruzamento, numa esquina, com o nome das duas ruas (abreviado: "R.", "Av.");
    ///  - <b>os carros parados</b> junto do meio-fio, na mão de quem anda (mais no centro, menos nos bairros), e uns
    ///    largados tortos no meio da rua;
    ///  - <b>as árvores</b> nas calçadas dos bairros de casas e nas praças (40% secas);
    ///  - <b>os hidrantes</b> perto das esquinas e <b>as lixeiras</b> nas calçadas do comércio e do centro (algumas caídas).
    /// O plano local de cada cidade é o do <see cref="TracadoDaCidade"/>; aqui tudo sai já no mapa.
    /// </summary>
    public static class ObjetosDaCidade
    {
        public const int PosteQuebrado = 1, PostePisca = 2;

        public static void Por(DadosDoMundo d, List<ObjetoDaEstrada> todos)
        {
            if (d.Plano == null) return;
            foreach (var t in d.Plano.Tracados)
            {
                var s = Sorteio.Da(d.Semente, "objetos-da-cidade-" + t.Indice);
                var postes = new List<Vector2>();
                int nu = t.Us.Length, nv = t.Vs.Length;
                for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    if (j + 1 < nv && t.RuaU[i, j]) Rua(t, s, todos, postes, true, i, t.Vs[j] + t.AV(j) + t.SV(j), t.Vs[j + 1] - t.AV(j + 1) - t.SV(j + 1));
                    if (i + 1 < nu && t.RuaV[i, j]) Rua(t, s, todos, postes, false, j, t.Us[i] + t.AU(i) + t.SU(i), t.Us[i + 1] - t.AU(i + 1) - t.SU(i + 1));
                }
                foreach (var c in t.Cruzamentos) Esquina(t, s, todos, c.x, c.y);
                foreach (var l in t.Lotes)
                {
                    if (l.Tipo == TipoDeLote.Praca) Praca(t, s, todos, l);
                    if (l.Casa != null && l.Tipo != TipoDeLote.Familia) CarroDaCasa(t, s, todos, l);
                }
            }
        }

        /// <summary>
        /// O carro na garagem (ou na vaga) de 70% das casas (Parte M6), de frente para o fundo, como quem entrou; o da casa
        /// da família é o carro que se dirige (fica com a partida).
        /// </summary>
        static void CarroDaCasa(TracadoDaCidade t, Sorteio s, List<ObjetoDaEstrada> todos, Lote l)
        {
            if (!s.Chance(0.7f) || !Casa.LugarDoCarro(l.Casa, Medidas.DoJogo, out var pos, out float giro)) return;
            var o = Por(todos, TipoDeObjeto.Carro, t.Mundo(l.NaCasa(pos.x, pos.y)), t.GiroDaCasa(l) + giro + s.Entre(-2f, 2f), s.Entre(0, ModelosDaEstrada.Carros));
            o.NaGaragem = true;
        }

        static ObjetoDaEstrada Por(List<ObjetoDaEstrada> todos, TipoDeObjeto tipo, Vector2 p, float giro, int variante = 0)
        {
            var o = new ObjetoDaEstrada { Tipo = tipo, Posicao = p, Giro = Mathf.Repeat(giro, 360f), Variante = variante };
            todos.Add(o);
            return o;
        }

        // o giro em y de quem olha na direção local "dir" a partir do ponto local "p"
        static float GiroLocal(TracadoDaCidade t, Vector2 p, Vector2 dir)
        {
            var w = t.Mundo(p + dir) - t.Mundo(p);
            return Mathf.Atan2(w.x, w.y) * Mathf.Rad2Deg;
        }

        // uma rua entre duas esquinas (de "de" até "ate" ao longo dela, sem as calçadas das ruas que cruzam): da linha u
        // (corre ao longo de v) ou da linha v (ao longo de u)
        static void Rua(TracadoDaCidade t, Sorteio s, List<ObjetoDaEstrada> todos, List<Vector2> postes, bool daLinhaU, int linha, float de, float ate)
        {
            if (ate - de < 8f) return;
            var tipo = daLinhaU ? t.TipoU[linha] : t.TipoV[linha];
            float pista = TracadoDaCidade.MeiaPista(tipo), calcada = TracadoDaCidade.Calcada(tipo);
            float eixo = daLinhaU ? t.Us[linha] : t.Vs[linha];
            var aoLongo = daLinhaU ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
            var lado = daLinhaU ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            Vector2 Ponto(float aoLongoDe, float atravessado) => daLinhaU ? new Vector2(eixo + atravessado, aoLongoDe) : new Vector2(aoLongoDe, eixo + atravessado);
            float meio = (de + ate) * 0.5f;
            var noMeio = t.Mundo(Ponto(meio, 0f));
            var zona = t.EhVila ? TipoDeBairro.Residencial : t.Cidade.ZonaEm(noMeio.x, noMeio.y);

            // os postes: com o braço por cima da rua (a frente do modelo olha para o meio dela)
            bool avenida = tipo == TipoDeRua.Avenida;
            float espaco = avenida ? 26f : t.EhVila ? 40f : 32f;
            int k = 0;
            for (float x = de + 6f + s.Entre(0f, 4f); x < ate - 4f; x += avenida ? espaco * 0.5f : espaco, k++)
            {
                float sinal = avenida ? (k % 2 == 0 ? 1f : -1f) : (k % 2 == 0 ? 1f : -1f);
                var local = Ponto(x, sinal * (pista + 0.55f));
                float sorteio = s.Fracao();
                int variante = sorteio < 0.07f ? PosteQuebrado : sorteio < 0.13f ? PostePisca : 0;
                var o = Por(todos, TipoDeObjeto.Poste, t.Mundo(local), GiroLocal(t, local, -lado * sinal), variante);
                o.NaCalcada = true;
                postes.Add(o.Posicao);
            }

            // os carros parados junto do meio-fio, na mão de quem anda por aquele lado (mão inglesa não: a da direita)
            float chance = zona == TipoDeBairro.Centro ? 0.32f : zona == TipoDeBairro.Comercio ? 0.26f : t.EhVila ? 0.08f : 0.13f;
            foreach (float sinal in new[] { 1f, -1f })
            {
                // indo ao longo de +aoLongo, a direita é +lado (v à esquerda de u); quem para do lado +lado olha para +aoLongo
                var frente = (daLinhaU ? sinal : -sinal) > 0f ? aoLongo : -aoLongo;
                for (float x = de + 7f; x < ate - 7f; x += 6.4f)
                {
                    if (!s.Chance(chance)) continue;
                    var local = Ponto(x + s.Entre(-0.4f, 0.4f), sinal * (pista - 1.15f + s.Entre(-0.15f, 0.1f)));
                    var o = Por(todos, TipoDeObjeto.Carro, t.Mundo(local), GiroLocal(t, local, frente) + s.Entre(-4f, 4f), s.Entre(0, ModelosDaEstrada.Carros));
                    o.NaEstrada = true;
                    x += 1.5f;
                }
            }
            // um largado torto no meio da rua, de vez em quando
            if (ate - de > 30f && s.Chance(avenida ? 0.12f : 0.06f))
            {
                var local = Ponto(s.Entre(de + 10f, ate - 10f), s.Entre(-pista + 2.2f, pista - 2.2f));
                var o = Por(todos, TipoDeObjeto.Carro, t.Mundo(local), GiroLocal(t, local, s.Chance(0.5f) ? aoLongo : -aoLongo) + s.Entre(-40f, 40f), s.Entre(0, ModelosDaEstrada.Carros));
                o.NaEstrada = true;
            }

            // as árvores nas calçadas dos bairros de casas (perto da divisa dos lotes, longe dos postes)
            if (zona == TipoDeBairro.Residencial)
                foreach (float sinal in new[] { 1f, -1f })
                    for (float x = de + 5f; x < ate - 5f; x += 13f)
                    {
                        if (!s.Chance(0.3f)) continue;
                        var local = Ponto(x + s.Entre(-2f, 2f), sinal * (pista + calcada - 0.9f));
                        var p = t.Mundo(local);
                        if (Perto(postes, p, 3.5f)) continue;
                        Por(todos, TipoDeObjeto.Arvore, p, s.Entre(0f, 360f), s.Chance(0.4f) ? 1 : 0).NaCalcada = true;
                    }
            // as lixeiras no comércio e no centro
            else
                foreach (float sinal in new[] { 1f, -1f })
                    for (float x = de + 8f; x < ate - 8f; x += 40f)
                    {
                        if (!s.Chance(0.45f)) continue;
                        var local = Ponto(x + s.Entre(-4f, 4f), sinal * (pista + 0.6f));
                        var p = t.Mundo(local);
                        if (Perto(postes, p, 2f)) continue;
                        var o = Por(todos, TipoDeObjeto.Lixeira, p, GiroLocal(t, local, -lado * sinal) + s.Entre(-15f, 15f));
                        o.NaCalcada = true;
                        if (s.Chance(0.15f)) o.Tombo = 90f;
                    }
        }

        static bool Perto(List<Vector2> pontos, Vector2 p, float raio)
        {
            for (int k = pontos.Count - 1; k >= 0 && k >= pontos.Count - 40; k--)
                if ((pontos[k] - p).sqrMagnitude < raio * raio) return true;
            return false;
        }

        // a placa de rua (numa esquina entre um braço de cada eixo) e, às vezes, um hidrante noutra esquina
        static void Esquina(TracadoDaCidade t, Sorteio s, List<ObjetoDaEstrada> todos, int i, int j)
        {
            byte b = t.Bracos(i, j);
            bool algumU = (b & (TracadoDaCidade.MaisU | TracadoDaCidade.MenosU)) != 0, algumV = (b & (TracadoDaCidade.MaisV | TracadoDaCidade.MenosV)) != 0;
            if (!algumU || !algumV) return;
            // as esquinas entre um braço u e um braço v que existem
            var esquinas = new List<Vector2>(4);
            foreach (float su in new[] { 1f, -1f })
            foreach (float sv in new[] { 1f, -1f })
            {
                bool u = (b & (su > 0f ? TracadoDaCidade.MaisU : TracadoDaCidade.MenosU)) != 0;
                bool v = (b & (sv > 0f ? TracadoDaCidade.MaisV : TracadoDaCidade.MenosV)) != 0;
                if (u && v) esquinas.Add(new Vector2(su, sv));
            }
            if (esquinas.Count == 0) return;
            var e = esquinas[s.Entre(0, esquinas.Count)];
            var local = new Vector2(t.Us[i] + e.x * (t.AU(i) + 0.7f), t.Vs[j] + e.y * (t.AV(j) + 0.7f));
            var placa = Por(todos, TipoDeObjeto.PlacaDeRua, t.Mundo(local), GiroLocal(t, local, new Vector2(1f, 0f)));
            placa.NaCalcada = true;
            placa.Linhas = new List<LinhaDePlaca>
            {
                new LinhaDePlaca { Texto = TracadoDaCidade.Abreviado(t.NomeU[i]), Km = -1f, Tamanho = 1f },
                new LinhaDePlaca { Texto = TracadoDaCidade.Abreviado(t.NomeV[j]), Km = -1f, Tamanho = 1f },
            };
            if (esquinas.Count > 1 && s.Chance(0.3f))
            {
                var outra = esquinas[(esquinas.IndexOf(e) + 1) % esquinas.Count];
                var h = new Vector2(t.Us[i] + outra.x * (t.AU(i) + 0.6f), t.Vs[j] + outra.y * (t.AV(j) + 1.6f));
                Por(todos, TipoDeObjeto.Hidrante, t.Mundo(h), GiroLocal(t, h, new Vector2(-outra.x, 0f))).NaCalcada = true;
            }
        }

        // as árvores da praça, numa grade solta, longe da borda (o miolo fica para o coreto, depois)
        static void Praca(TracadoDaCidade t, Sorteio s, List<ObjetoDaEstrada> todos, Lote l)
        {
            var meio = l.Meio;
            float raioDoMiolo = Mathf.Min(l.U1 - l.U0, l.V1 - l.V0) * 0.18f;
            for (float u = l.U0 + 4f; u < l.U1 - 3f; u += 10f)
            for (float v = l.V0 + 4f; v < l.V1 - 3f; v += 10f)
            {
                var p = new Vector2(u + s.Entre(-2.5f, 2.5f), v + s.Entre(-2.5f, 2.5f));
                if (Vector2.Distance(p, meio) < raioDoMiolo || !s.Chance(0.62f)) continue;
                Por(todos, TipoDeObjeto.Arvore, t.Mundo(p), s.Entre(0f, 360f), s.Chance(0.4f) ? 1 : 0);
            }
        }
    }
}
