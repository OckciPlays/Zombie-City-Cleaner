using System.Collections.Generic;
using UnityEngine;
using Andar = ZombieCityCleaner.Mundo.PlantaDoHospital.Andar;
using Porta = ZombieCityCleaner.Mundo.PlantaDoHospital.Porta;
using Divisa = ZombieCityCleaner.Mundo.PlantaDoHospital.Divisa;
using Borda = ZombieCityCleaner.Mundo.PlantaDoHospital.Borda;
using TipoDePorta = ZombieCityCleaner.Mundo.PlantaDoHospital.TipoDePorta;
using Zona = ZombieCityCleaner.Mundo.PlantaDoHospital.Zona;
using static ZombieCityCleaner.Mundo.PlantaDaLojaDeArmas;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O abandono da loja de armas (o Plano_LojaDeArmas.md, a decisão 8): as marcas do atlas do hospital
    /// (<see cref="AtlasDasMarcas"/>) por receitas por cômodo e pela semente, contando a história (o dono resistiu atrás do
    /// caixa: a poça, o arrasto até o estoque, a mão na parede; os tiros na parede do fundo e na entrada; no estado C, o
    /// sangue velho dentro da sala segura, onde ele se trancou; os rastros do carrinho no estoque; os tiros nas paredes do
    /// estande; a sujeira, a água, as infiltrações, o mofo do banheiro, os papéis do caixa e do escritório, os cacos dos
    /// balcões e das vitrines) e as pichações do atlas delas ("LOOTERS WILL BE SHOT" e "NO AMMO" na fachada, o X da busca,
    /// "STAY OUT" perto do estande, a contagem dos dias no acampamento e o recado na porta de cofre).
    /// </summary>
    public sealed partial class LojaDeArmas
    {
        System.Random _rm;
        int _ordem;
        readonly List<Rect> _pegadas = new List<Rect>();

        /// <summary>A cor de cada categoria das marcas (as do hospital: o sangue, o velho, a água, o mofo, a sujeira, o reboco dos tiros, os papéis e o vidro).</summary>
        static readonly Color[] Tintas =
        {
            new Color(0.52f, 0.07f, 0.05f), new Color(0.40f, 0.11f, 0.07f), new Color(0.55f, 0.45f, 0.30f),
            new Color(0.33f, 0.39f, 0.25f), new Color(0.47f, 0.41f, 0.33f), new Color(0.86f, 0.85f, 0.81f), Color.white, Color.white,
        };
        static readonly Color Sujeira = new Color(0.42f, 0.37f, 0.30f);

        /// <summary>As cores das latas de spray (as do AbandonoDaObra: o vermelho, o preto, o branco, o laranja e o azul).</summary>
        static readonly Color[] Latas =
        {
            new Color(0.62f, 0.07f, 0.06f), new Color(0.07f, 0.07f, 0.07f), new Color(0.88f, 0.88f, 0.86f), new Color(0.86f, 0.43f, 0.10f),
            new Color(0.15f, 0.25f, 0.55f),
        };

        float Sorte() => (float)_rm.NextDouble();

        // ------------------------------------------------------------ as marcas de dentro

        void MarcasDeDentro()
        {
            _rm = new System.Random(v.semente * 7907 + 17);
            _ordem = 0;
            _pegadas.Clear();
            foreach (var mv in MoveisDaLojaDeArmas.Da(v))
                if (MedidasDaMobilia.Tem(mv.modelo) && mv.lugar.y < 0.3f) _pegadas.Add(MoveisDoHospital.Pegada(mv.modelo, mv.lugar, mv.giro, mv.escala));
            foreach (var k in an.comodos) MarcasDoComodo(k);
        }

        void MarcasDoComodo(Comodo k)
        {
            float area = (k.x1 - k.x0) * (k.z1 - k.z0);
            float teto = ComForro(k.tipo) ? Forro : PeDireito;
            int sujeira = 1 + Mathf.Min(3, (int)(area / 40f));
            for (int i = 0; i < sujeira; i++) NoCanto(k, 0.7f + 0.2f * Sorte());
            if (Sorte() < 0.5f) NoChao(k, "Agua", Sorte() * 360f, 0.7f);
            if (Sorte() < 0.45f && NoTeto(k, "InfiltracaoNoTeto", teto, 1f, out var pinga)) NoChao(k, "Agua", Sorte() * 360f, 0.7f, pinga);
            switch (k.tipo)
            {
                case "vendas":
                    {
                        float s = v.sangue;
                        // o dono resistiu atrás do caixa: a poça, o arrasto até a porta do estoque, as pegadas saindo pela loja
                        NoChao(k, "PocaGrande_A", Sorte() * 360f, 0.95f, new Vector2(X(12.3f), Z(20.7f)));
                        NoChao(k, "Arrasto", 180f + (Sorte() - 0.5f) * 10f, 0.85f, new Vector2(X(10.3f), Z(20.8f)));
                        if (s > 0.55f) NoChao(k, "Pegadas", -80f + (Sorte() - 0.5f) * 20f, 0.6f * s, new Vector2(X(13.8f), Z(15.5f)));
                        NoChao(k, "Respingos", Sorte() * 360f, 0.8f, new Vector2(X(11.2f), Z(18.0f)));
                        NaParede(k, 2, "MaoArrastada", 0.9f, 1.6f, 0.9f, X(10.4f));
                        if (s > 0.7f) NaParede(k, 2, "RespingoNaParede", 1.2f, 2.2f, 0.9f, X(12.4f));
                        // os tiros: na parede do fundo (em cima dos balcões), em volta da entrada e na parede do estande
                        for (int i = 0; i < 3; i++) NaParede(k, 2, "Tiros", 1.6f, 2.8f, 0.95f);
                        NaParede(k, 0, "Tiros", 2.9f, 3.45f, 0.95f, X(9.0f));
                        NaParede(k, 1, "Tiros", 1.2f, 2.4f, 0.95f);
                        for (int i = 0; i < 2; i++) NaParede(k, Sorte() < 0.5f ? 1 : 3, "Encardido", 0.7f, 1.5f, 0.6f);
                        // os papéis (os recibos do caixa, os formulários) e os cacos (das vitrines e dos balcões quebrados)
                        NoChao(k, "Papeis", Sorte() * 360f, 1f, new Vector2(X(13.2f), Z(17.6f)));
                        NoChao(k, "Papeis", Sorte() * 360f, 1f);
                        NoChao(k, "Papeis", Sorte() * 360f, 1f);
                        NoChao(k, "Cacos", Sorte() * 360f, 1f, new Vector2(X(2.6f), Z(1.0f)));
                        NoChao(k, "Cacos", Sorte() * 360f, 1f, new Vector2(X(12.8f), Z(1.0f)));
                        NoChao(k, "Cacos", Sorte() * 360f, 1f, new Vector2(X(4.0f), Z(17.9f)));
                        NoChao(k, "Cacos", Sorte() * 360f, 1f, new Vector2(X(3.3f), Z(11.5f)));
                        break;
                    }
                case "escritorio":
                    for (int i = 0; i < 3; i++) NoChao(k, "Papeis", Sorte() * 360f, 1f);
                    break;
                case "estoque":
                    // os rastros do carrinho de carga, da porta dos fundos até a porta de cofre
                    NoChao(k, "Rodinhas", 90f + (Sorte() - 0.5f) * 14f, 0.55f, new Vector2(X(8.6f), Z(27.4f)), Sujeira);
                    NoChao(k, "Rodinhas", 60f, 0.45f, new Vector2(X(8.0f), Z(25.0f)), Sujeira);
                    if (v.sala != EstadoDaSala.Intacta) NoChao(k, "Papeis", Sorte() * 360f, 1f);
                    break;
                case "segura":
                    if (v.sala == EstadoDaSala.Intacta)
                    {
                        // o dono, que se trancou mordido: o sangue velho no canto e a mão arrastada do lado de dentro da porta
                        NoChao(k, "PocaGrande_B", Sorte() * 360f, 0.95f, new Vector2(X(1.3f), Z(23.4f)));
                        NoChao(k, "Arrasto", 200f, 0.75f, new Vector2(X(3.6f), Z(24.6f)));
                        NaParede(k, 1, "MaoArrastada", 0.8f, 1.6f, 0.9f, PortaDoCofre.z - 1.2f);
                    }
                    else
                    {
                        NoChao(k, "Pegadas", Sorte() * 360f, 0.5f, null, Sujeira);
                        for (int i = 0; i < (v.sala == EstadoDaSala.Saqueada ? 3 : 1); i++) NoChao(k, "Papeis", Sorte() * 360f, 1f);
                    }
                    break;
                case "antessala":
                    NoChao(k, "Pegadas", Sorte() * 360f, 0.5f, null, Sujeira);
                    NaParede(k, 3, "Tiros", 1.3f, 2.2f, 0.9f);
                    break;
                case "banheiro":
                    NaParede(k, Sorte() < 0.5f ? 1 : 2, "MofoNaBase", 0f, 0.8f, 0.85f);
                    NaParede(k, 0, "MofoNaBase", 0f, 0.8f, 0.85f);
                    NoTeto(k, "MofoNoTeto", teto, 0.85f, out _);
                    NaParede(k, 1, "InfiltracaoNaParede", teto - 1.8f, teto, 1f);
                    break;
                case "estande":
                    // os tiros nas paredes dos lados (da linha até o anteparo) e o encardido da área de espera
                    for (int i = 0; i < 7; i++)
                        NaParede(k, i % 2 == 0 ? 1 : 3, "Tiros", 0.9f, 2.6f, 0.95f, Mathf.Lerp(LinhaDeTiro + 1.5f, ComecoDoAnteparo - 0.6f, Sorte()));
                    NaParede(k, 0, "Encardido", 0.7f, 1.5f, 0.6f);
                    NoTeto(k, "InfiltracaoNoTeto", teto, 1f, out var p2);
                    NoChao(k, "Agua", Sorte() * 360f, 0.7f, p2);
                    if (v.acampamento) NoChao(k, "Papeis", Sorte() * 360f, 1f, new Vector2(X(17.6f), Z(17.5f)));
                    break;
            }
        }

        Color Tinta(AtlasDasMarcas.Item it, float alfa, Color? cor = null)
        {
            if (it.categoria == AtlasDasMarcas.Categoria.Sangue || it.categoria == AtlasDasMarcas.Categoria.SangueVelho) alfa = Mathf.Max(alfa, 0.97f);
            float val = 0.88f + 0.24f * Sorte();
            Color c = cor.HasValue ? cor.Value * val : it.Colorida ? new Color(Mathf.Min(1f, val), Mathf.Min(1f, val), Mathf.Min(1f, val)) : Tintas[(int)it.categoria] * val;
            c = c.linear;
            c.a = Mathf.Clamp01(alfa);
            return c;
        }

        float AlturaNoChao() => 0.009f + 0.00025f * (_ordem++ % 16);

        bool DebaixoDeMovel(float x, float z)
        {
            foreach (var r in _pegadas)
                if (x > r.xMin + 0.1f && x < r.xMax - 0.1f && z > r.yMin + 0.1f && z < r.yMax - 0.1f) return true;
            return false;
        }

        /// <summary>Uma marca deitada no chão do cômodo (inteira dentro dele); em 'onde', o mais perto dali que couber.</summary>
        bool NoChao(Comodo k, string nome, float giro, float alfa, Vector2? onde = null, Color? cor = null)
        {
            int i = AtlasDasMarcas.Indice(nome);
            if (i < 0) return false;
            var it = AtlasDasMarcas.Itens[i];
            float a = giro * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            float w = it.tamanho.x, h = it.tamanho.y;
            float ex = (Mathf.Abs(c) * w + Mathf.Abs(sn) * h) / 2f, ez = (Mathf.Abs(sn) * w + Mathf.Abs(c) * h) / 2f;
            float folga = m.esp / 2f + 0.06f;
            float xa = k.x0 + folga + ex, xb = k.x1 - folga - ex, za = k.z0 + folga + ez, zb = k.z1 - folga - ez;
            if (xa > xb || za > zb) return false;
            for (int t = 0; t < 16; t++)
            {
                float x = onde.HasValue ? Mathf.Clamp(onde.Value.x, xa, xb) : Mathf.Lerp(xa, xb, Sorte());
                float z = onde.HasValue ? Mathf.Clamp(onde.Value.y, za, zb) : Mathf.Lerp(za, zb, Sorte());
                if (!onde.HasValue && t < 12 && DebaixoDeMovel(x, z)) continue;
                marcas.Quadro(new Vector3(x, AlturaNoChao(), z), new Vector3(c, 0f, sn), new Vector3(-sn, 0f, c), Vector3.up, it.tamanho, it.uv, Tinta(it, alfa, cor));
                return true;
            }
            return false;
        }

        // a sujeira junto de uma parede (a marca comprida encostada nela, de um lado sorteado)
        void NoCanto(Comodo k, float alfa)
        {
            int i = AtlasDasMarcas.Indice("SujeiraDoCanto");
            if (i < 0) return;
            var it = AtlasDasMarcas.Itens[i];
            int lado = _rm.Next(4);
            bool emX = lado == 0 || lado == 2;
            float folga = m.esp / 2f + 0.06f + it.tamanho.y / 2f;
            float comp = emX ? k.x1 - k.x0 : k.z1 - k.z0;
            if (comp < it.tamanho.x + 0.6f) return;
            float s = Mathf.Lerp((emX ? k.x0 : k.z0) + it.tamanho.x / 2f + 0.3f, (emX ? k.x1 : k.z1) - it.tamanho.x / 2f - 0.3f, Sorte());
            Vector3 p = lado switch
            {
                0 => new Vector3(s, 0f, k.z0 + folga),
                1 => new Vector3(k.x1 - folga, 0f, s),
                2 => new Vector3(s, 0f, k.z1 - folga),
                _ => new Vector3(k.x0 + folga, 0f, s),
            };
            // o alto do quadro (o lado de dentro da faixa, a parte sem sujeira) para longe da parede
            Vector3 dentro = lado switch { 0 => Vector3.forward, 1 => Vector3.left, 2 => Vector3.back, _ => Vector3.right };
            Vector3 aoLongo = emX ? Vector3.right : Vector3.forward;
            p.y = AlturaNoChao();
            marcas.Quadro(p, aoLongo, dentro, Vector3.up, it.tamanho, it.uv, Tinta(it, alfa));
        }

        /// <summary>Uma marca no teto ('teto' acima do chão), virada para baixo; devolve onde ela ficou.</summary>
        bool NoTeto(Comodo k, string nome, float teto, float alfa, out Vector2 onde)
        {
            onde = default;
            int i = AtlasDasMarcas.Indice(nome);
            if (i < 0) return false;
            var it = AtlasDasMarcas.Itens[i];
            float folga = m.esp / 2f + 0.1f;
            float xa = k.x0 + folga + it.tamanho.x / 2f, xb = k.x1 - folga - it.tamanho.x / 2f, za = k.z0 + folga + it.tamanho.y / 2f, zb = k.z1 - folga - it.tamanho.y / 2f;
            if (xa > xb || za > zb) return false;
            float x = Mathf.Lerp(xa, xb, Sorte()), z = Mathf.Lerp(za, zb, Sorte());
            onde = new Vector2(x, z);
            marcas.Quadro(new Vector3(x, teto - 0.004f, z), Vector3.right, Vector3.back, Vector3.down, it.tamanho, it.uv, Tinta(it, alfa));
            return true;
        }

        // o quanto o acabamento sai da face da parede em cada cômodo (a marca vai na frente dele)
        static float Saida(string tipo) => tipo switch
        {
            "vendas" or "antessala" or "escritorio" => 0.036f,
            "estande" => 0.056f,
            "segura" => 0.156f,
            _ => 0.022f,
        };

        /// <summary>
        /// Os vãos de um lado do cômodo (0 a frente, 1 o leste, 2 o fundo, 3 o oeste), ao longo dele (x ou z): as portas (de
        /// dentro e de fora) e as janelas de fora.
        /// </summary>
        List<(float de, float ate, float baixo, float alto)> FurosDoLado(Comodo k, int lado)
        {
            var l = new List<(float, float, float, float)>();
            float linha = lado == 0 ? k.z0 : lado == 1 ? k.x1 : lado == 2 ? k.z1 : k.x0;
            foreach (var p in an.portas)
            {
                float larg = Largura(p) / 2f + 0.1f;
                if (p.b == null)
                {
                    if (p.a == k && p.lado == lado) l.Add((p.pos - larg, p.pos + larg, 0f, Alto(p) + 0.3f));
                    continue;
                }
                if (p.a != k && p.b != k) continue;
                var o = p.a == k ? p.b : p.a;
                bool nesse = lado switch
                {
                    0 => Mathf.Abs(o.z1 - linha) < 0.01f,
                    1 => Mathf.Abs(o.x0 - linha) < 0.01f,
                    2 => Mathf.Abs(o.z0 - linha) < 0.01f,
                    _ => Mathf.Abs(o.x1 - linha) < 0.01f,
                };
                if (nesse) l.Add((p.pos - larg, p.pos + larg, 0f, Alto(p) + 0.3f));
            }
            foreach (var j in Janelas)
                if (j.lado == lado && Mathf.Abs((lado == 0 ? Z0 : lado == 1 ? X1 : lado == 2 ? Z1 : X0) - linha) < 0.01f)
                    l.Add((j.de - 0.1f, j.ate + 0.1f, j.baixo - 0.1f, j.alto + 0.1f));
            return l;
        }

        /// <summary>
        /// Uma marca numa parede do cômodo (o lado 'lado'), de y0 a y1 (o meio dela), longe das portas, das janelas e das
        /// quinas, na frente do acabamento; em 's', o mais perto dali (ao longo da parede).
        /// </summary>
        bool NaParede(Comodo k, int lado, string nome, float y0, float y1, float alfa, float? s = null)
        {
            int i = AtlasDasMarcas.Indice(nome);
            if (i < 0) return false;
            var it = AtlasDasMarcas.Itens[i];
            bool emX = lado == 0 || lado == 2;
            float lo = (emX ? k.x0 : k.z0) + m.esp / 2f + 0.25f + it.tamanho.x / 2f, hi = (emX ? k.x1 : k.z1) - m.esp / 2f - 0.25f - it.tamanho.x / 2f;
            if (lo > hi) return false;
            var furos = FurosDoLado(k, lado);
            float linha = lado == 0 ? k.z0 : lado == 1 ? k.x1 : lado == 2 ? k.z1 : k.x0;
            float sai = m.esp / 2f + Saida(k.tipo) + 0.006f;
            Vector3 dentro = lado switch { 0 => Vector3.forward, 1 => Vector3.left, 2 => Vector3.back, _ => Vector3.right };
            for (int t = 0; t < 14; t++)
            {
                float c = s.HasValue ? Mathf.Clamp(s.Value + (t == 0 ? 0f : (Sorte() - 0.5f) * t * 0.4f), lo, hi) : Mathf.Lerp(lo, hi, Sorte());
                float y = Mathf.Lerp(y0, y1, Sorte());
                y = Mathf.Max(y, it.tamanho.y / 2f + 0.02f);
                bool livre = true;
                foreach (var (de, ate, baixo, alto) in furos)
                    if (c + it.tamanho.x / 2f > de && c - it.tamanho.x / 2f < ate && y + it.tamanho.y / 2f > baixo && y - it.tamanho.y / 2f < alto) livre = false;
                if (!livre) continue;
                Vector3 p = emX ? new Vector3(c, y, linha) : new Vector3(linha, y, c);
                p += dentro * sai;
                // a direita de quem olha a parede de dentro do cômodo
                Vector3 direita = Vector3.Cross(Vector3.up, -dentro);
                marcas.Quadro(p, direita, Vector3.up, dentro, it.tamanho, it.uv, Tinta(it, alfa));
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ as pichações

        /// <summary>Uma pichação do atlas pelo nome: o meio em 'c', na face virada para 'fora', com a lata, um pouco torta.</summary>
        void Pichar(string nome, Vector3 c, Vector3 fora, float escala = 1f, int lata = -1)
        {
            int idx = -1;
            for (int i = 0; i < AtlasDasPichacoes.Itens.Length; i++) if (AtlasDasPichacoes.Itens[i].nome == nome) { idx = i; break; }
            if (idx < 0) return;
            var item = AtlasDasPichacoes.Itens[idx];
            var u = Vector3.Cross(Vector3.up, -fora).normalized;
            float giro = Faixa(-3f, 3f) * Mathf.Deg2Rad;
            var lado = u * Mathf.Cos(giro) + Vector3.up * Mathf.Sin(giro);
            var cima = Vector3.up * Mathf.Cos(giro) - u * Mathf.Sin(giro);
            var cor = item.Colorida ? Color.white : Latas[lata >= 0 ? lata : rnd.Next(Latas.Length)];
            cor = cor.linear;
            cor.a = Faixa(0.85f, 1f);
            pichacoes.Quadro(c + fora * (0.008f + pichacoes.Quadros * 0.0015f), lado, cima, fora, item.tamanho * escala, item.uv, cor);
        }

        void PichacoesDeFora()
        {
            float zf = Z0 - m.esp / 2f;
            // a fachada: o aviso dos donos em cima da primeira vitrine, o X da equipe de busca no compensado em cima da porta,
            // "NO AMMO" em cima da última vitrine e "STAY OUT" em cima da janela da antessala
            Pichar("Looters", new Vector3(X(2.1f), 3.24f, zf), Vector3.back, 0.95f, 0);
            Pichar("BuscaA", new Vector3(MeioDaEntrada, AltoDaEntrada + 0.35f, zf - 0.03f), Vector3.back, 0.55f, 3);
            Pichar("NoAmmo", new Vector3(X(13.5f), 3.2f, zf), Vector3.back, 1f, 1);
            Pichar("StayOut", new Vector3(X(18.1f), 3.18f, zf), Vector3.back, 0.9f, 0);
            // os lados e os fundos: as tags, as assinaturas e a caveira perto da porta dos fundos
            float xo = X0 - m.esp / 2f, xl = X1 + m.esp / 2f, zt = Z1 + m.esp / 2f;
            Pichar("TagKaos", new Vector3(xo, 1.1f, Z(9f)), Vector3.left);
            Pichar("AssDuke", new Vector3(xo, 1.5f, Z(17.5f)), Vector3.left, 1f, 1);
            Pichar("TagVex", new Vector3(xl, 1.0f, Z(20f)), Vector3.right);
            Pichar("AssK2", new Vector3(xl, 1.2f, Z(14f)), Vector3.right, 1f, 2);
            Pichar("TagSkul", new Vector3(X(15.5f), 1.1f, zt), Vector3.forward);
            Pichar("Caveira", new Vector3(X(6.9f), 1.5f, zt), Vector3.forward, 1f, 2);
            Pichar("KeepOut", new Vector3(X(12.0f), 1.6f, zt), Vector3.forward, 1f, 0);
        }

        void PichacoesDeDentro()
        {
            float e = m.esp / 2f;
            // "STAY OUT" ao lado da porta do estande, na loja (os sobreviventes acampados lá dentro), e "NO AMMO" em cima das vitrines
            Pichar("StayOut", new Vector3(X(15.6f) - e - Saida("vendas"), 1.75f, Z(1.9f)), Vector3.left, 1f, 0);
            Pichar("NoAmmo", new Vector3(X(2.1f), 3.18f, Z0 + e + Saida("vendas")), Vector3.forward, 0.95f, 1);
            // a porta de cofre, do lado do estoque: "DEAD INSIDE" na intacta (o dono lá dentro), o X da busca na parcial e o
            // "CHECKED" na saqueada
            var lado = new Vector3(SalaSegura.xMax + e + 0.022f, 1.55f, PortaDoCofre.z - VaoDoCofre / 2f - 0.75f);
            if (v.sala == EstadoDaSala.Intacta) Pichar("DeadInside", lado, Vector3.right, 1f, 0);
            else if (v.sala == EstadoDaSala.Parcial) Pichar("BuscaB", lado, Vector3.right, 0.8f, 3);
            else Pichar("Checked", lado, Vector3.right, 1f, 1);
            // a contagem dos dias perto do acampamento do estande
            if (v.acampamento) Pichar("Dias23", new Vector3(Estande.xMin + e + Saida("estande"), 1.4f, Z(15.5f)), Vector3.right, 1f, 2);
        }
    }
}
