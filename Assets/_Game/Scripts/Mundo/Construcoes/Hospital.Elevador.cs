using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O poço do elevador do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 3): em cada andar, os trilhos-
    /// guia dos dois lados; no fundo (o subsolo), os para-choques; e a cabine, parada entre o 2º e o 3º andar desde a
    /// evacuação (as portas dela meio abertas, a luz apagada e os cabos subindo até a casa de máquinas). No 2º andar, as
    /// portas do saguão foram forçadas (a fresta de 35 cm da <see cref="Porta.fresta"/>): por ela se vê a cabine na altura da
    /// cabeça e o poço escuro embaixo. Sem colisão (ninguém entra no poço).
    /// </summary>
    public sealed partial class Hospital
    {
        /// <summary>O chão da cabine parada (acima do chão do 2º andar) e a altura dela.</summary>
        public const float CabineAcimaDo2 = 1.65f, AlturaDaCabine = 2.45f;

        void NoPoco(int f)
        {
            var el = Elevador;
            float xi = el.xMin + m.esp / 2f, xf = el.xMax - m.esp / 2f, zc = el.center.y;
            float b = Base(f), y0 = f == 0 ? b : b - GrossuraDaLaje, y1 = Teto(f);
            foreach (float x in new[] { xi + 0.05f, xf - 0.05f })
                Caixa(new Vector3(x, (y0 + y1) / 2f, zc), new Vector3(0.06f, y1 - y0, 0.1f), "Metal");
            if (f == 0)
                foreach (float x in new[] { -0.6f, 0.6f })
                {
                    Caixa(new Vector3(el.center.x + x, b + 0.05f, zc), new Vector3(0.4f, 0.1f, 0.4f), "Metal");
                    Cilindro(new Vector3(el.center.x + x, b + 0.1f, zc), 0.11f, 0.11f, 0.5f, 8, "Faixa");
                }
            if (f == 2) Cabine();
        }

        void Cabine()
        {
            var el = Elevador;
            float cx = el.center.x, yc = Base(2) + CabineAcimaDo2, h = AlturaDaCabine;
            float x0 = el.xMin + 0.22f, x1 = el.xMax - 0.22f, z0 = el.yMin + 0.27f, z1 = el.yMax - 0.22f, zc = (z0 + z1) / 2f;
            float w = x1 - x0, d = z1 - z0;
            // o inox por dentro (as faces que olham para o meio da cabine), a chapa pintada por fora e o piso de borracha
            var cor = Cor.Por((n, c) => n.y > 0.7f ? "Pneu" : n.y < -0.7f ? "Metal" : (n.x * (cx - c.x) + n.z * (zc - c.z) > 0f ? "Cromo" : "Metal"));
            Caixa(new Vector3(cx, yc - 0.08f, zc), new Vector3(w, 0.16f, d), cor);
            Caixa(new Vector3(cx, yc - 0.56f, z0 + 0.01f), new Vector3(1.4f, 0.8f, 0.02f), "Metal");
            Caixa(new Vector3(cx, yc + h / 2f, z1 - 0.025f), new Vector3(w, h, 0.05f), cor);
            Caixa(new Vector3(x0 + 0.025f, yc + h / 2f, zc), new Vector3(0.05f, h, d - 0.1f), cor);
            Caixa(new Vector3(x1 - 0.025f, yc + h / 2f, zc), new Vector3(0.05f, h, d - 0.1f), cor);
            // a frente, dos dois lados da porta de 1,3 m, e a verga
            float pa = cx - 0.65f, pb = cx + 0.65f;
            Caixa(new Vector3((x0 + pa) / 2f, yc + h / 2f, z0 + 0.025f), new Vector3(pa - x0, h, 0.05f), cor);
            Caixa(new Vector3((pb + x1) / 2f, yc + h / 2f, z0 + 0.025f), new Vector3(x1 - pb, h, 0.05f), cor);
            Caixa(new Vector3(cx, yc + 2.1f + (h - 2.1f) / 2f, z0 + 0.025f), new Vector3(1.3f, h - 2.1f, 0.05f), cor);
            // as portas da cabine: a da esquerda aberta (atrás da frente), a da direita parada no meio
            Caixa(new Vector3(pa - 0.33f + 0.02f, yc + 1.05f, z0 + 0.075f), new Vector3(0.66f, 2.1f, 0.03f), "Cromo");
            Caixa(new Vector3(cx + 0.3f + 0.33f, yc + 1.05f, z0 + 0.075f), new Vector3(0.66f, 2.1f, 0.03f), "Cromo");
            // o teto com a luminária apagada, o corrimão do fundo e o painel dos botões
            Caixa(new Vector3(cx, yc + h + 0.03f, zc), new Vector3(w, 0.06f, d), cor);
            Caixa(new Vector3(cx, yc + h - 0.006f, zc), new Vector3(1.0f, 0.01f, 1.6f), "Farol");
            Caixa(new Vector3(cx, yc + 0.95f, z1 - 0.09f), new Vector3(w - 0.3f, 0.04f, 0.04f), "Cromo");
            Caixa(new Vector3(x1 - 0.2f, yc + 1.2f, z0 + 0.06f), new Vector3(0.22f, 0.8f, 0.02f), "Cromo");
            for (int i = 0; i < 4; i++)
                Caixa(new Vector3(x1 - 0.2f, yc + 1.45f - i * 0.12f, z0 + 0.073f), new Vector3(0.05f, 0.05f, 0.012f), "Pneu");
            // a travessa de cima e os cabos até a casa de máquinas (em cima da laje da cobertura)
            Caixa(new Vector3(cx, yc + h + 0.3f, zc), new Vector3(w + 0.1f, 0.24f, 0.26f), "Metal");
            float topo = TopoDaCobertura - GrossuraDaLaje - 0.01f;
            for (int i = 0; i < 4; i++)
            {
                float x = cx - 0.09f + i * 0.06f;
                Tubo(new Vector3(x, yc + h + 0.42f, zc), new Vector3(x, topo, zc), 0.01f, "Grade");
            }
        }
    }
}
