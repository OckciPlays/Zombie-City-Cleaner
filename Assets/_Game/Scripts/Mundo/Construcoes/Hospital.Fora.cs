using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O embasamento do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 3): o chão em volta fica 45 cm
    /// abaixo do térreo (<see cref="PlantaDoHospital.Embasamento"/>). Na frente, embaixo da marquise, a varanda da entrada
    /// com os degraus no meio e as duas rampas dos lados (de 1:12, com o patamar em cima) e os guarda-corpos; do lado da
    /// garagem, a rampa das ambulâncias até os portões; e, nas saídas (a do leste, a barricada do oeste e a de serviço no
    /// fundo), o patamar e os degraus. Tudo com a colisão (o NavMesh sobe por eles).
    /// </summary>
    public sealed partial class Hospital
    {
        /// <summary>A varanda da entrada, embaixo da marquise, e a largura das rampas dos lados dela.</summary>
        public static readonly Rect Varanda = Rect.MinMaxRect(-7.5f, -28f, 7.5f, Z0 - 0.1f);
        public const float LarguraDaRampa = 1.6f, LarguraDosDegraus = 6f;
        /// <summary>A rampa das ambulâncias, na frente dos portões da garagem (de 6,4 m: uns 4°).</summary>
        public const float RampaDaGaragem = 6.4f;

        static readonly Cor CorDoChaoDeFora = Cor.PorNormal("Calcada", "Concreto", "Concreto");
        static readonly Cor CorDoDegrau = Cor.PorNormal("ConcretoClaro", "Concreto", "Concreto");

        void EmbasamentoDoHospital()
        {
            var v = Varanda;
            float grossura = Embasamento + 0.05f;
            // a varanda (sem o pedaço da entrada, que já tem o piso dela)
            var furo = Rect.MinMaxRect(Entrada.xMin - 0.1f, Entrada.yMin - 0.1f, Entrada.xMax + 0.1f, Z0);
            Laje(v.xMin, v.xMax, v.yMin, v.yMax, 0f, grossura, CorDoChaoDeFora, true, new[] { furo });
            Degraus(new Vector2(0f, v.yMin), Vector2.down, LarguraDosDegraus);
            const float ac = 0.95f;
            foreach (float s in new[] { -1f, 1f })
            {
                // o guarda-corpo da beira da frente, dos degraus até a quina
                GuardaCorpo(new Vector3(s * (LarguraDosDegraus / 2f + 0.1f), 0f, v.yMin + 0.04f), new Vector3(s * (v.xMax - 0.04f), 0f, v.yMin + 0.04f), ac, CorDoCorrimao);
                // a rampa do lado, subindo da frente para o fundo, e o patamar em cima, que dá na varanda
                float xi = s * v.xMax, xo = s * (v.xMax + LarguraDaRampa);
                float zPe = v.yMin - 0.6f, zTopo = zPe + Embasamento * 12f;
                Rampa(Mathf.Min(xi, xo), Mathf.Max(xi, xo), zPe, zTopo);
                Laje(Mathf.Min(xi, xo), Mathf.Max(xi, xo), zTopo, zTopo + 1.6f, 0f, grossura, CorDoChaoDeFora, true);
                float xr = xo - s * 0.04f, xv = xi - s * 0.04f;
                GuardaCorpo(new Vector3(xr, ChaoDeFora, zPe + 0.05f), new Vector3(xr, 0f, zTopo), ac, CorDoCorrimao);
                GuardaCorpo(new Vector3(xr, 0f, zTopo), new Vector3(xr, 0f, zTopo + 1.56f), ac, CorDoCorrimao);
                GuardaCorpo(new Vector3(xr, 0f, zTopo + 1.56f), new Vector3(xi, 0f, zTopo + 1.56f), ac, CorDoCorrimao);
                // na beira da varanda: ao lado da rampa (mais baixa) e, depois do patamar, até a fachada
                GuardaCorpo(new Vector3(xv, 0f, v.yMin + 0.04f), new Vector3(xv, 0f, zTopo), ac, CorDoCorrimao);
                GuardaCorpo(new Vector3(xv, 0f, zTopo + 1.6f), new Vector3(xv, 0f, v.yMax - 0.05f), ac, CorDoCorrimao);
            }
            // a rampa das ambulâncias, do pátio até a soleira dos portões
            var g = GaragemDasAmbulancias;
            float xTopo = g.xMin - m.esp / 2f, xPe = xTopo - RampaDaGaragem, za = g.yMin - 0.6f, zb = g.yMax + 0.6f, baixo = ChaoDeFora - 0.05f;
            Solido(new List<Vector3> { new Vector3(xPe, baixo, za), new Vector3(xTopo, baixo, za), new Vector3(xTopo, 0f, za), new Vector3(xPe, ChaoDeFora, za) },
                   new List<Vector3> { new Vector3(xPe, baixo, zb), new Vector3(xTopo, baixo, zb), new Vector3(xTopo, 0f, zb), new Vector3(xPe, ChaoDeFora, zb) },
                   CorDoChaoDeFora, colide: true);
            // as saídas: a do leste, a do oeste (barricada) e a de serviço, no fundo
            PatamarDeFora(Rect.MinMaxRect(X1 + 0.1f, -8.2f, X1 + 1.5f, -4.8f));
            Degraus(new Vector2(X1 + 1.5f, -6.5f), Vector2.right, 3.2f);
            PatamarDeFora(Rect.MinMaxRect(X0 - 1.5f, -8.2f, X0 - 0.1f, -5.15f));
            Degraus(new Vector2(X0 - 1.5f, -6.65f), Vector2.left, 2.9f);
            PatamarDeFora(Rect.MinMaxRect(5.4f, Z1 + 0.1f, 10.6f, Z1 + 1.6f));
            Degraus(new Vector2(8f, Z1 + 1.6f), Vector2.up, 5.0f);
        }

        void PatamarDeFora(Rect r) => Laje(r.xMin, r.xMax, r.yMin, r.yMax, 0f, Embasamento + 0.05f, CorDoChaoDeFora, true);

        // uma rampa de x0 a x1, subindo em z do chão de fora (em zPe) até o térreo (em zTopo)
        void Rampa(float x0, float x1, float zPe, float zTopo)
        {
            float baixo = ChaoDeFora - 0.05f;
            Solido(new List<Vector3> { new Vector3(x0, baixo, zPe), new Vector3(x0, baixo, zTopo), new Vector3(x0, 0f, zTopo), new Vector3(x0, ChaoDeFora, zPe) },
                   new List<Vector3> { new Vector3(x1, baixo, zPe), new Vector3(x1, baixo, zTopo), new Vector3(x1, 0f, zTopo), new Vector3(x1, ChaoDeFora, zPe) },
                   CorDoChaoDeFora, colide: true);
        }

        /// <summary>
        /// Os degraus da beira de um patamar do térreo (a 'borda': o meio da beira) até o chão de fora, para 'fora': 3 espelhos
        /// de 15 cm e pisadas de 30 cm, com a faixa amarela na quina de cada um.
        /// </summary>
        void Degraus(Vector2 borda, Vector2 fora, float largura)
        {
            int n = Mathf.CeilToInt(Embasamento / 0.19f - 0.05f);
            float r = Embasamento / n, p = 0.3f, baixo = ChaoDeFora - 0.05f;
            float yaw = -Mathf.Atan2(fora.y, fora.x) * Mathf.Rad2Deg + 90f;
            for (int k = 1; k < n; k++)
            {
                float topo = -k * r;
                Vector2 c = borda + fora * ((k - 1) * p + p / 2f), q = borda + fora * (k * p - 0.03f);
                Caixa(new Vector3(c.x, (topo + baixo) / 2f, c.y), new Vector3(largura, topo - baixo, p), CorDoDegrau, yaw, colide: true);
                Caixa(new Vector3(q.x, topo + 0.002f, q.y), new Vector3(largura - 0.04f, 0.006f, 0.05f), "Faixa", yaw);
            }
            Vector2 b = borda - fora * 0.03f;
            Caixa(new Vector3(b.x, 0.002f, b.y), new Vector3(largura - 0.04f, 0.006f, 0.05f), "Faixa", yaw);
        }
    }
}
