using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os fachos de luz que afastam os Ocos (veja Zumbi.Luz.cs): a lanterna de cada jogador, ligada, e os faróis acesos de
    /// cada carro. Refeitos a cada quadro, no anfitrião (são poucos: um por jogador e um por carro de farol aceso).
    /// </summary>
    public partial class ZombieManager
    {
        internal struct Facho
        {
            public Vector3 origem, direcao;
            public float alcance, cosMeio, tanMeio;
            public bool carro;
            public JogadorRede dono;   // quem segura a lanterna (no farol, ninguém)
        }

        internal readonly List<Facho> Fachos = new List<Facho>(8);

        void AtualizarFachos()
        {
            Fachos.Clear();
            var a = ajustes;
            float meioL = a.meioAnguloDaLanterna * Mathf.Deg2Rad, meioF = a.meioAnguloDoFarol * Mathf.Deg2Rad;
            for (int k = 0; k < QuantosJogadores; k++)
            {
                ref readonly var p = ref Jogadores[k];
                if (!p.lanterna || p.olhar.sqrMagnitude < 0.01f) continue;
                Fachos.Add(new Facho
                {
                    origem = p.olho, direcao = p.olhar.normalized, alcance = a.alcanceDaLanterna,
                    cosMeio = Mathf.Cos(meioL), tanMeio = Mathf.Tan(meioL), dono = p.jogador,
                });
            }
            foreach (var c in Carro.Todos)
            {
                if (c == null || !c.FaroisLigados) continue;
                // a luz sai do meio dos faróis, para a frente do carro (no plano: o facho do farol desce um pouco)
                Vector3 frente = c.transform.forward;
                frente.y = 0f;
                if (frente.sqrMagnitude < 0.01f) continue;
                frente.Normalize();
                Vector3 origem = Vector3.zero;
                int n = 0;
                if (c.farois != null)
                    foreach (var f in c.farois)
                        if (f != null) { origem += f.transform.position; n++; }
                origem = n > 0 ? origem / n : c.transform.position + frente * 2f + Vector3.up * 0.7f;
                Fachos.Add(new Facho
                {
                    origem = origem, direcao = frente, alcance = a.alcanceDoFarol,
                    cosMeio = Mathf.Cos(meioF), tanMeio = Mathf.Tan(meioF), carro = true,
                });
            }
        }

        /// <summary>
        /// O facho que bate neste ponto (o peito do zumbi): dentro do alcance e do ângulo, sem parede no meio. De dia, a
        /// luz conta em qualquer sombra (debaixo de um teto, na sombra dos prédios, debaixo das copas); no sol, não (o sol
        /// apaga o facho, e o zumbi no sol já está fugindo). Até 29/10/2026 era só debaixo de um teto, e na cidade grande os
        /// zumbis que passam o dia na sombra dos prédios, na rua, não ligavam para a lanterna (o bug que o Pedro viu).
        /// </summary>
        internal bool NoFacho(Vector3 peito, out Facho facho)
        {
            facho = default;
            bool dia = EDia;
            for (int i = 0; i < Fachos.Count; i++)
            {
                var f = Fachos[i];
                Vector3 para = peito - f.origem;
                if (f.carro) para.y = 0f;
                float d = para.magnitude;
                if (d > f.alcance || d < 0.3f) continue;
                if (Vector3.Dot(para / d, f.direcao) < f.cosMeio) continue;
                if (!Enxerga(f.origem, peito)) continue;
                if (dia && Exposto(peito - Vector3.up * 1.2f)) continue;   // no sol (o ponto é o peito; o Exposto quer os pés)
                facho = f;
                return true;
            }
            return false;
        }
    }
}
