using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// As prateleiras das lojas do Dia 0 na Woodcreek (22/11/2026, a etapa 4 do Docs/23; o Pedro: "existem no seu formato base
    /// uns itens que não deveriam estar ali e que atrapalham os reais itens do game (isso acontecia no d0 principalmente)"). As
    /// lojas do Lakewood não desenham os móveis antigos do salão (os modelos do Cidade_Mobilia.fbx trazem os produtos colados
    /// neles), e no Dia 0 elas ganham estas: feitas de caixas, vazias, com as tábuas nas alturas exatas das fileiras da
    /// <see cref="LojaDoDiaZero"/> (os produtos à venda assentam nelas). Os tipos são os dos pontos do salão: a prateleira, a
    /// gôndola (dos dois lados), o expositor refrigerado (com a porta de vidro), o expositor de armas, a vitrine, o freezer e
    /// os balcões dos caixas. A colisão é uma caixa por móvel, recuada como a dos móveis antigos (<see cref="Mobilia.Recuo"/>:
    /// a mira passa dela para os produtos da frente).
    /// </summary>
    public static class PrateleirasDoDia
    {
        /// <summary>
        /// Os móveis do salão (os pontos nas coordenadas do objeto da loja: já espelhados, como os da obra) e os balcões
        /// (retângulos no chão, nas mesmas coordenadas), num desenho e numa colisão. <paramref name="piso"/>: a altura do chão
        /// do salão.
        /// </summary>
        public static (MalhaDeObra vis, MalhaDeObra col, int moveis) Desenhar(IEnumerable<PontoDeMovel> pontos, IEnumerable<Rect> balcoes, float piso, int semente)
        {
            var o = new Obra(semente, Medidas.DoJogo);
            int n = 0;
            foreach (var pm in pontos)
            {
                if (pm.comodo != "salao" || pm.andar != 0) continue;
                if (Movel(o, pm)) n++;
            }
            if (balcoes != null)
                foreach (var r in balcoes)
                {
                    var m = Matrix4x4.Translate(new Vector3(r.center.x, piso, r.center.y));
                    o.CaixaM(m * Matrix4x4.Translate(new Vector3(0f, 0.46f, 0f)), new Vector3(r.width, 0.92f, r.height), "Madeira", colide: true);
                    o.CaixaM(m * Matrix4x4.Translate(new Vector3(0f, 0.94f, 0f)), new Vector3(r.width + 0.04f, 0.04f, r.height + 0.04f), "PlacaBranca");
                    n++;
                }
            return (o.vis, o.col, n);
        }

        // um móvel no ponto dele (a frente para o +z do móvel); false se o tipo não é de loja
        static bool Movel(Obra o, PontoDeMovel pm)
        {
            float sx = pm.tamanho.x, sy = pm.tamanho.y, sz = pm.tamanho.z;
            var m = Matrix4x4.TRS(pm.centro, Quaternion.Euler(0f, pm.giro, 0f), Vector3.one);
            void C(Vector3 c, Vector3 t, string mat, bool colide = false) => o.CaixaM(m * Matrix4x4.Translate(c), t, mat, colide);
            // a caixa de colisão do móvel inteiro, recuada (a mira chega nos produtos da frente)
            // (só na colisão: desenhada, ela escondia os produtos de dentro do móvel)
            void Corpo() => o.CaixaM(m * Matrix4x4.Translate(new Vector3(0f, sy / 2f, 0f)), new Vector3(Mathf.Max(0.1f, sx - 2f * Mobilia.Recuo), sy, Mathf.Max(0.1f, sz - 2f * Mobilia.Recuo)), "Concreto", true, false);
            const float T = 0.025f;   // a espessura das tábuas
            switch (pm.tipo)
            {
                case "prateleira":
                {
                    // o fundo, os lados, o chapéu e as tábuas (a de cima de cada fileira: y * sy / 1.9)
                    C(new Vector3(0f, sy / 2f, -sz / 2f + 0.015f), new Vector3(sx, sy, 0.03f), "Metal");
                    foreach (float lado in new[] { -1f, 1f }) C(new Vector3(lado * (sx / 2f - 0.015f), sy / 2f, 0f), new Vector3(0.03f, sy, sz), "Metal");
                    C(new Vector3(0f, sy - 0.015f, 0f), new Vector3(sx, 0.03f, sz), "Metal");
                    foreach (float y in new[] { 0.12f, 0.56f, 0.98f, 1.40f })
                        C(new Vector3(0f, y * sy / 1.9f - T / 2f, 0.01f), new Vector3(sx - 0.06f, T, sz - 0.04f), "PlacaBranca");
                    Corpo();
                    return true;
                }
                case "gondola":
                {
                    // a divisória no meio do comprido e as tábuas dos dois lados (y * sy / 1.6), com as cabeceiras
                    bool emZ = sz >= sx;
                    float comprido = emZ ? sz : sx, largo = emZ ? sx : sz;
                    Vector3 V(float aoLado, float y, float aoLongo) => emZ ? new Vector3(aoLado, y, aoLongo) : new Vector3(aoLongo, y, aoLado);
                    Vector3 Tam(float aoLado, float y, float aoLongo) => emZ ? new Vector3(aoLado, y, aoLongo) : new Vector3(aoLongo, y, aoLado);
                    C(V(0f, sy / 2f, 0f), Tam(0.05f, sy, comprido), "Metal");
                    C(V(0f, 0.06f, 0f), Tam(largo - 0.06f, 0.12f, comprido), "Metal");
                    foreach (float ponta in new[] { -1f, 1f }) C(V(0f, sy / 2f, ponta * (comprido / 2f - 0.015f)), Tam(largo, sy, 0.03f), "Metal");
                    foreach (float y in new[] { 0.12f, 0.61f, 1.06f })
                        foreach (float lado in new[] { -1f, 1f })
                            C(V(lado * largo / 4f, y * sy / 1.6f - T / 2f, 0f), Tam(largo / 2f - 0.04f, T, comprido - 0.04f), "PlacaBranca");
                    Corpo();
                    return true;
                }
                case "expositor refrigerado":
                {
                    // a geladeira aberta na frente (o vidro do atlas é opaco: esconderia as bebidas): o corpo branco e as tábuas (y * sy / 2)
                    C(new Vector3(0f, sy / 2f, -sz / 2f + 0.03f), new Vector3(sx, sy, 0.06f), "PlacaBranca");
                    foreach (float lado in new[] { -1f, 1f }) C(new Vector3(lado * (sx / 2f - 0.03f), sy / 2f, 0f), new Vector3(0.06f, sy, sz), "PlacaBranca");
                    C(new Vector3(0f, sy - 0.06f, 0f), new Vector3(sx, 0.12f, sz), "PlacaBranca");
                    C(new Vector3(0f, 0.04f, 0f), new Vector3(sx, 0.08f, sz), "Metal");
                    foreach (float y in new[] { 0.16f, 0.56f, 0.96f, 1.36f })
                        C(new Vector3(0f, y * sy / 2f - T / 2f, 0f), new Vector3(sx - 0.12f, T, sz - 0.1f), "Grade");
                    Corpo();
                    return true;
                }
                case "expositor de armas":
                {
                    // o painel do fundo e a prateleirinha (0,90 * sy / 2)
                    C(new Vector3(0f, sy / 2f, -sz / 2f + 0.03f), new Vector3(sx, sy, 0.06f), "Madeira");
                    C(new Vector3(0f, 0.9f * sy / 2f - T / 2f, sz * 0.05f), new Vector3(sx - 0.1f, T, sz * 0.7f), "Madeira");
                    Corpo();
                    return true;
                }
                case "vitrine":
                {
                    // o balcão: a base até o tampo de dentro (0,62 * sy), com a moldura de metal do fundo (sem o vidro: o do atlas é
                    // opaco e escondia o que está à venda)
                    C(new Vector3(0f, 0.62f * sy / 2f, 0f), new Vector3(sx, 0.62f * sy, sz), "Madeira");
                    C(new Vector3(0f, (0.62f * sy + sy) / 2f, -sz / 2f + 0.02f), new Vector3(sx, sy * 0.38f, 0.04f), "Metal");
                    Corpo();
                    return true;
                }
                case "freezer":
                    C(new Vector3(0f, sy / 2f, 0f), new Vector3(sx, sy, sz), "PlacaBranca");
                    Corpo();
                    return true;
            }
            return false;
        }
    }
}
