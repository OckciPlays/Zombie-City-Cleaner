using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    public sealed partial class UsinaDeLakewood
    {
        // ------------------------------------------------------------------ a subestação

        /// <summary>O u dos três transformadores e o v deles.</summary>
        static readonly float[] Transformadores = { 650f, 680f, 710f };
        const float VDosTransformadores = 166f;

        // a subestação: os três transformadores nas bases, os barramentos, os disjuntores, os dois pórticos (um para cada
        // linha) e a casinha de comando; o alambrado é à parte
        void MontarASubestacao(List<PecaDaUsina> saida, System.Random sorte, Func<float, float, float> H)
        {
            var s = Subestacao;
            float h = AlturaDoPatio("Subestação");
            var d = new Desenho(L(s.center.x, h, s.center.y));
            foreach (float u in Transformadores)
            {
                float v = VDosTransformadores;
                // a base com a bacia de óleo, o corpo, os radiadores, o conservador e as buchas
                d.Caixa(L(u, h + 0.25f, v), new Vector3(8f, 0.5f, 7f), "Concreto", true);
                d.Quad(L(u - 3.6f, h + 0.51f, v - 3.1f), L(u + 3.6f, h + 0.51f, v - 3.1f), L(u + 3.6f, h + 0.51f, v + 3.1f), L(u - 3.6f, h + 0.51f, v + 3.1f), Vector3.up, "Oleo");
                d.Caixa(L(u, h + 0.5f + 2.1f, v), new Vector3(4.6f, 4.2f, 3.4f), Cor.PorNormal("Metal", "CarroCinza"), true);
                foreach (float lado in new[] { -1f, 1f })
                    for (int k = 0; k < 6; k++)
                        d.Caixa(L(u + lado * 3.2f, h + 2.5f, v - 1.5f + k * 0.6f), new Vector3(1.8f, 3.2f, 0.06f), "CarroCinza");
                d.CilindroDeitado(L(u, h + 5.6f, v + 1.0f), 0.55f, 3.4f, 8, "CarroCinza");
                foreach (float du in new[] { -1.2f, 1.2f }) d.Caixa(L(u + du, h + 4.95f, v + 1.0f), new Vector3(0.15f, 0.7f, 0.6f), "Metal");
                foreach (float du in new[] { -1.2f, 0f, 1.2f })
                {
                    d.Cilindro(L(u + du, h + 4.7f, v - 0.6f), 0.22f, 0.12f, 2.4f, 6, "ListraBranca");
                    d.Caixa(L(u + du, h + 7.2f, v - 0.6f), new Vector3(0.3f, 0.2f, 0.3f), "Cromo");
                    // a descida da bucha até o barramento
                    d.Tubo(L(u + du, h + 7.3f, v - 0.6f), L(u + du * 1.4f, h + 9.6f, 192f + (du + 1.2f) * 1.7f), 0.04f, 4, "Cromo");
                }
                // a ferrugem escorrendo pelo corpo
                for (int k = 0; k < 3; k++)
                {
                    float du = -1.8f + k * 1.6f + (float)sorte.NextDouble() * 0.6f;
                    d.Quad(L(u + du - 0.2f, h + 4.4f, v + 1.72f), L(u + du + 0.2f, h + 4.4f, v + 1.72f), L(u + du + 0.12f, h + 1.4f, v + 1.72f), L(u + du - 0.12f, h + 1.4f, v + 1.72f), Vector3.back, "Ferrugem");
                }
                // o disjuntor de cada transformador (o tanque nas pernas, com as buchas)
                var pd = L(u, h, 205f);
                foreach (float du in new[] { -1f, 1f }) foreach (float dv in new[] { -0.5f, 0.5f })
                    d.Caixa(pd + new Vector3(du, 0.6f, -dv), new Vector3(0.12f, 1.2f, 0.12f), "Metal");
                d.Caixa(pd + Vector3.up * 2.1f, new Vector3(2.4f, 1.8f, 1.4f), "Metal", true);
                foreach (float du in new[] { -0.8f, 0f, 0.8f })
                    d.Cilindro(pd + new Vector3(du, 3f, 0f), 0.14f, 0.09f, 1.6f, 6, "ListraBranca");
            }
            // o barramento: os três tubos das fases nos suportes, do primeiro transformador até o pórtico da linha leste
            float yb = h + 9.6f;
            foreach (float v in new[] { 192f, 194f, 196f })
                d.Tubo(L(636f, yb, v), L(744f, yb, v), 0.07f, 5, "Cromo");
            for (float u = 640f; u <= 742f; u += 34f)
            {
                d.Caixa(L(u, h + 4.6f, 194f), new Vector3(0.5f, 9.2f, 0.5f), "Metal", true);
                d.Caixa(L(u, h + 9.2f, 194f), new Vector3(0.3f, 0.3f, 5.4f), "Metal");
                foreach (float v in new[] { 192f, 194f, 196f })
                    d.Cilindro(L(u, h + 9.35f, v), 0.12f, 0.08f, 0.3f, 6, "ListraBranca");
            }
            // os pórticos treliçados: o da linha leste (ao longo do v) e o da linha sul (ao longo do u)
            PorticoDaSubestacao(d, h, new Vector2(744f, 180f), new Vector2(744f, 216f));
            PorticoDaSubestacao(d, h, new Vector2(660f, 232f), new Vector2(720f, 232f));
            // a descida do barramento até o pórtico da linha sul
            foreach (float k in new[] { 0f, 1f, 2f })
                d.Tubo(L(690f + (k - 1f) * 2f, yb, 196f), L(668f + k * 8.8f, h + 14f, 232f), 0.05f, 4, "Cromo");
            // a casinha de comando
            var r = Rect.MinMaxRect(738f, 150f, 746f, 157f);
            Caixote(d, r, h, h + 3.2f, "ParedeBege", "Concreto", 0.4f);
            Porta(d, r, Lado.Oeste, 153.5f, h, 1.0f, 2.2f, "Metal");
            Janela(d, r, Lado.Montante, 740f, 742.5f, h + 1.3f, h + 2.4f, 2, 1, sorte);
            d.Caixa(NoLado(r, Lado.Jusante, 744f, h + 1.4f, 0.4f), new Vector3(1f, 0.9f, 0.7f), "Metal");
            Hera(d, L(r.xMax + 0.02f, h, r.center.y), Vector3.right, Vector3.forward, 4f, 3.2f, 17);
            saida.Add(Peca("Subestação", d));
        }

        /// <summary>Os pontos dos cabos de um pórtico (na moldura da barragem): 6, ao longo da viga, embaixo das cadeias de isoladores.</summary>
        static IEnumerable<Vector3> CabosDoPortico(float h, Vector2 a, Vector2 b)
        {
            for (int k = 0; k < 6; k++)
            {
                var p = Vector2.Lerp(a, b, 0.11f + k * 0.156f);
                yield return L(p.x, h + 13.8f, p.y);
            }
        }

        // um pórtico treliçado de a até b (u, v): três colunas de quatro cantoneiras, a viga em treliça a 16 m e as cadeias
        // de isoladores penduradas nela
        void PorticoDaSubestacao(Desenho d, float h, Vector2 a, Vector2 b)
        {
            const float altura = 16f, meia = 0.45f;
            var dir = (b - a).normalized;
            var lado = new Vector3(-dir.y, 0f, -dir.x);
            var ao = new Vector3(dir.x, 0f, -dir.y);
            foreach (float t in new[] { 0f, 0.5f, 1f })
            {
                var p = Vector2.Lerp(a, b, t);
                var pe = L(p.x, h, p.y);
                Vector3 Canto(int k, float y) => pe + Vector3.up * y + ao * (k % 2 == 0 ? -meia : meia) + lado * (k < 2 ? -meia : meia);
                for (int k = 0; k < 4; k++) d.Entre(Canto(k, 0f), Canto(k, altura), 0.12f, 0.12f, "Metal");
                for (float y = 2f; y < altura; y += 2.5f)
                {
                    d.Entre(Canto(0, y), Canto(1, y), 0.07f, 0.07f, "Metal");
                    d.Entre(Canto(2, y), Canto(3, y), 0.07f, 0.07f, "Metal");
                    d.Entre(Canto(0, y), Canto(2, y), 0.07f, 0.07f, "Metal");
                    d.Entre(Canto(1, y), Canto(3, y), 0.07f, 0.07f, "Metal");
                    d.Entre(Canto(0, y), Canto(3, y + 2.5f), 0.06f, 0.06f, "Metal");
                }
                d.Caixa(pe + Vector3.up * 0.25f, new Vector3(1.6f, 0.5f, 1.6f), "Concreto", true);
                d.Colide(pe + Vector3.up * (altura * 0.5f), new Vector3(1f, altura, 1f));
            }
            // a viga: os quatro banzos e as diagonais
            var p0 = L(a.x, h + altura, a.y);
            var p1 = L(b.x, h + altura, b.y);
            foreach (float s in new[] { -1f, 1f })
            foreach (float y in new[] { 0f, 1.2f })
                d.Entre(p0 + lado * (s * 0.5f) + Vector3.up * y, p1 + lado * (s * 0.5f) + Vector3.up * y, 0.09f, 0.09f, "Metal");
            float comp = Vector3.Distance(p0, p1);
            int n = Mathf.Max(1, Mathf.RoundToInt(comp / 1.8f));
            for (int k = 0; k < n; k++)
            {
                var q0 = Vector3.Lerp(p0, p1, k / (float)n);
                var q1 = Vector3.Lerp(p0, p1, (k + 1) / (float)n);
                foreach (float s in new[] { -1f, 1f })
                    d.Entre(q0 + lado * (s * 0.5f), q1 + lado * (s * 0.5f) + Vector3.up * 1.2f, 0.05f, 0.05f, "Metal");
            }
            // as cadeias de isoladores
            foreach (var c in CabosDoPortico(h, a, b))
            {
                d.Cilindro(c, 0.14f, 0.14f, 2.2f, 6, "ListraBranca");
                d.Caixa(c, new Vector3(0.22f, 0.15f, 0.22f), "Cromo");
            }
        }

        // ------------------------------------------------------------------ as torres e os cabos

        /// <summary>Os pontos dos cabos de uma torre (x de través na linha, y para cima): os seis condutores, embaixo dos isoladores.</summary>
        public static readonly Vector3[] CabosDaTorre =
        {
            new Vector3(-7f, 18.4f, 0f), new Vector3(7f, 18.4f, 0f), new Vector3(-8.5f, 24.4f, 0f),
            new Vector3(8.5f, 24.4f, 0f), new Vector3(-7f, 30.4f, 0f), new Vector3(7f, 30.4f, 0f),
        };
        /// <summary>O cabo de terra, na ponta da torre.</summary>
        public static readonly Vector3 TerraDaTorre = new Vector3(0f, 36.2f, 0f);
        const float AlturaDaTorre = 36f;

        MalhaDeObra _malhaDaTorre, _colisaoDaTorre;

        // a meia largura da torre na altura y (a base de 7,2 m, a cintura de 2 m aos 21 m, a gaiola reta e a ponta)
        static float MeiaDaTorre(float y) => y <= 21f ? Mathf.Lerp(3.6f, 1f, y / 21f) : y <= 33f ? 1f : Mathf.Lerp(1f, 0.3f, (y - 33f) / 3f);

        // a torre treliçada de circuito duplo, nas coordenadas dela (a base no meio, os braços em x): uma malha só para todas
        MalhaDeObra MalhaDaTorre()
        {
            if (_malhaDaTorre != null) return _malhaDaTorre;
            var d = new Desenho(Vector3.zero);
            Vector3 Canto(int sx, int sz, float y) => new Vector3(sx * MeiaDaTorre(y), y, sz * MeiaDaTorre(y));
            float[] niveis = { 0f, 7f, 14f, 21f, 27f, 33f, AlturaDaTorre };
            var cantos = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) };
            for (int i = 0; i + 1 < niveis.Length; i++)
            {
                float y0 = niveis[i], y1 = niveis[i + 1];
                // as pernas
                foreach (var (sx, sz) in cantos)
                    d.Entre(Canto(sx, sz, y0), Canto(sx, sz, y1), 0.2f, 0.2f, y1 <= 7f ? "Ferrugem" : "Metal");
                if (i + 1 == niveis.Length - 1) continue;
                // as travessas e as diagonais de cada face (em X embaixo, em zigue-zague em cima)
                for (int f = 0; f < 4; f++)
                {
                    var (ax, az) = cantos[f];
                    var (bx, bz) = cantos[(f + 1) % 4];
                    d.Entre(Canto(ax, az, y1), Canto(bx, bz, y1), 0.12f, 0.12f, "Metal");
                    d.Entre(Canto(ax, az, y0), Canto(bx, bz, y1), 0.09f, 0.09f, "Metal");
                    if (y1 <= 21f) d.Entre(Canto(bx, bz, y0), Canto(ax, az, y1), 0.09f, 0.09f, "Metal");
                }
            }
            // os braços: os banzos de baixo, o de cima e as diagonais, e os isoladores pendurados na ponta
            foreach (var (y, ponta) in new[] { (21f, 7f), (27f, 8.5f), (33f, 7f) })
                foreach (int s in new[] { -1, 1 })
                {
                    var tip = new Vector3(s * ponta, y, 0f);
                    d.Entre(new Vector3(s * 1f, y, -1f), tip + Vector3.back * 0.25f, 0.12f, 0.12f, "Metal");
                    d.Entre(new Vector3(s * 1f, y, 1f), tip + Vector3.forward * 0.25f, 0.12f, 0.12f, "Metal");
                    d.Entre(new Vector3(s * 1f, y + 2.4f, 0f), tip + Vector3.up * 0.35f, 0.12f, 0.12f, "Metal");
                    d.Entre(new Vector3(s * (1f + (ponta - 1f) * 0.5f), y, 0f), new Vector3(s * 1f, y + 2.4f, 0f), 0.08f, 0.08f, "Metal");
                    d.Entre(new Vector3(s * (1f + (ponta - 1f) * 0.5f), y, 0f), tip + Vector3.up * 0.35f, 0.08f, 0.08f, "Metal");
                    d.Cilindro(tip + Vector3.down * 2.6f, 0.15f, 0.15f, 2.6f, 6, "ListraBranca");
                    d.Caixa(tip + Vector3.down * 2.6f, new Vector3(0.25f, 0.15f, 0.25f), "Cromo");
                }
            // o braço do cabo de terra, as sapatas e a placa de perigo
            d.Entre(new Vector3(-1.4f, AlturaDaTorre, 0f), new Vector3(1.4f, AlturaDaTorre, 0f), 0.1f, 0.1f, "Metal");
            foreach (var (sx, sz) in cantos)
                d.Caixa(new Vector3(sx * 3.6f, 0.1f, sz * 3.6f), new Vector3(1f, 1.2f, 1f), "Concreto");
            d.Caixa(new Vector3(0f, 3.2f, -MeiaDaTorre(3.2f) - 0.05f), new Vector3(0.6f, 0.45f, 0.03f), "PlacaAmarela");
            _malhaDaTorre = d.Vis;
            // a colisão: as quatro pernas perto do chão
            _colisaoDaTorre = new MalhaDeObra(256);
            foreach (var (sx, sz) in cantos)
                _colisaoDaTorre.Caixa(Matrix4x4.Translate(new Vector3(sx * 3.2f, 3f, sz * 3.2f)), new Vector3(0.9f, 6f, 0.9f), "Concreto");
            return _malhaDaTorre;
        }

        // as torres no chão e os cabos de vão em vão (com a flecha da catenária, mais rasa onde o chão sobe), mais os vãos
        // dos pórticos da subestação até a primeira torre de cada linha
        void MontarAsLinhas(List<PecaDaUsina> saida, Func<float, float, float> altura)
        {
            var malha = MalhaDaTorre();
            var bases = new Vector3[Torres.Count];
            var giros = new Quaternion[Torres.Count];
            for (int i = 0; i < Torres.Count; i++)
            {
                var t = Torres[i];
                var giro = Quaternion.Euler(0f, t.Giro, 0f);
                float h = float.MaxValue;
                foreach (var c in new[] { new Vector3(-3.6f, 0f, -3.6f), new Vector3(3.6f, 0f, -3.6f), new Vector3(3.6f, 0f, 3.6f), new Vector3(-3.6f, 0f, 3.6f) })
                {
                    var q = giro * c;
                    h = Mathf.Min(h, altura(t.Ponto.x + q.x, t.Ponto.y + q.z));
                }
                bases[i] = new Vector3(t.Ponto.x, h - 0.4f, t.Ponto.y);
                giros[i] = giro;
                saida.Add(new PecaDaUsina { Nome = $"Torre {t.Linha + 1}.{i}", Tipo = TipoDaPecaDaUsina.Obra, Posicao = bases[i], Giro = giro, Desenho = malha, Colisao = _colisaoDaTorre });
            }
            var pontos = new List<(Vector3 a, Vector3 b)>(7);
            for (int i = 0; i + 1 < Torres.Count; i++)
            {
                if (Torres[i].Linha != Torres[i + 1].Linha) continue;
                pontos.Clear();
                foreach (var c in CabosDaTorre) pontos.Add((bases[i] + giros[i] * c, bases[i + 1] + giros[i + 1] * c));
                pontos.Add((bases[i] + giros[i] * TerraDaTorre, bases[i + 1] + giros[i + 1] * TerraDaTorre));
                saida.Add(CabosDoVao($"Cabos {Torres[i].Linha + 1}.{i}", pontos, altura));
            }
            // dos pórticos até a primeira torre de cada linha (os pontos do pórtico ligados aos da torre pela ordem de través)
            float hs = AlturaDoPatio("Subestação");
            var porticos = new[] { (new Vector2(744f, 180f), new Vector2(744f, 216f)), (new Vector2(660f, 232f), new Vector2(720f, 232f)) };
            for (int l = 0; l < LinhasDeTransmissao.Count && l < porticos.Length; l++)
            {
                int primeira = Torres.FindIndex(t => t.Linha == l);
                if (primeira < 0) continue;
                var doPortico = new List<Vector3>();
                foreach (var c in CabosDoPortico(hs, porticos[l].Item1, porticos[l].Item2)) doPortico.Add(NoMundo(c));
                var daTorre = new List<Vector3>();
                foreach (var c in CabosDaTorre) daTorre.Add(bases[primeira] + giros[primeira] * c);
                var lado = giros[primeira] * Vector3.right;
                doPortico.Sort((p, q) => Vector3.Dot(p, lado).CompareTo(Vector3.Dot(q, lado)));
                daTorre.Sort((p, q) => Vector3.Dot(p, lado).CompareTo(Vector3.Dot(q, lado)));
                pontos.Clear();
                for (int k = 0; k < 6; k++) pontos.Add((doPortico[k], daTorre[k]));
                saida.Add(CabosDoVao($"Cabos da subestação {l + 1}", pontos, altura));
            }
        }

        // os cabos de um vão: cada um em 12 pedaços, com a flecha de 3% do vão (menos se ficar a menos de 8 m do chão)
        PecaDaUsina CabosDoVao(string nome, List<(Vector3 a, Vector3 b)> cabos, Func<float, float, float> altura)
        {
            var origem = cabos[0].a;
            var d = new Desenho(origem);
            const int pedacos = 12;
            foreach (var (a, b) in cabos)
            {
                float vao = Vector3.Distance(a, b);
                float flecha = 0.03f * vao;
                for (int tenta = 0; tenta < 6; tenta++)
                {
                    float folga = float.MaxValue;
                    for (int k = 1; k < pedacos; k++)
                    {
                        float t = k / (float)pedacos;
                        var p = Vector3.Lerp(a, b, t) + Vector3.down * (4f * flecha * t * (1f - t));
                        folga = Mathf.Min(folga, p.y - altura(p.x, p.z));
                    }
                    if (folga >= 8f) break;
                    flecha *= 0.55f;
                }
                var ant = a;
                for (int k = 1; k <= pedacos; k++)
                {
                    float t = k / (float)pedacos;
                    var p = Vector3.Lerp(a, b, t) + Vector3.down * (4f * flecha * t * (1f - t));
                    d.Entre(ant, p, 0.07f, 0.07f, "Grade");
                    ant = p;
                }
            }
            return new PecaDaUsina { Nome = nome, Tipo = TipoDaPecaDaUsina.Obra, Posicao = origem, Giro = Quaternion.identity, Desenho = d.Vis };
        }
    }
}
