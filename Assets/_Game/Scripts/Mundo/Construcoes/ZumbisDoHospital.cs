using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os zumbis de dentro do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 7, a decisão 9: "poucos e
    /// espalhados, pelos pontos e pelas zonas de cada setor, uns isolados, uns grupos pequenos, salas fechadas com zumbis,
    /// ativados por andar; nada de horda dentro"). Aqui só o plano, pela semente do mundo (o mesmo em todas as máquinas e em
    /// qualquer thread): os grupos de cada andar, cada um num cômodo, com quantos. Quem põe os zumbis é o anfitrião
    /// (Zombies/ZombieManager.Hospital.cs), quando o andar carrega.
    ///  - <b>a sala fechada</b>: o cômodo com todas as portas fechadas no começo (<see cref="Hospital.ComecoDaPorta"/>)
    ///    tem, quatro vezes em dez, um grupo de 2 a 3 lá dentro (quem abre a porta acha);
    ///  - <b>os setores</b>: a área médica (a emergência, as enfermarias, a UTI, o isolamento, a sala vermelha) com a chance
    ///    maior (35%, um ou dois), a circulação com um isolado a cada uns 140 m², o serviço (20%) e o resto (10%) com um;
    ///  - <b>o teto de cada andar</b> (<see cref="MaximoNoAndar"/>): o subsolo 10, o térreo 14, o 2º 12, o 3º 8.
    /// De fora: os cômodos lacrados (a A-01, as salas técnicas dela, o quarto 4), as escadas, o elevador e os pequenos.
    /// </summary>
    public static class ZumbisDoHospital
    {
        /// <summary>Um grupo: o cômodo (o retângulo nas coordenadas da obra), o andar, quantos e se fica numa sala fechada.</summary>
        public struct Grupo
        {
            public Rect area;
            public int andar, quantos;
            public string comodo;
            public bool fechado;
        }

        public static readonly int[] MaximoNoAndar = { 10, 14, 12, 8 };

        static readonly HashSet<string> Medicos = new HashSet<string>
        {
            "emergencia", "enfermaria", "uti", "vermelha", "triagem", "observacao", "isolamento", "quarto", "cirurgia", "preparo", "recebimento",
            "necroterio", "autopsia",
        };

        static readonly HashSet<string> DeFora = new HashSet<string> { "escada", "elevador" };

        /// <summary>Os grupos do andar f (pela semente do mundo).</summary>
        public static List<Grupo> DoAndar(int semente, int f)
        {
            var an = PlantaDoHospital.DoAndar(f);
            var r = new Sorteio(Sorteio.Misturar(((ulong)(uint)semente << 8) ^ (ulong)(uint)f ^ 0x5A4D4249484F5350UL));
            var grupos = new List<Grupo>();
            foreach (var k in an.comodos)
            {
                if (an.fechados.Contains(k) || DeFora.Contains(k.tipo)) continue;
                float area = (k.x1 - k.x0) * (k.z1 - k.z0);
                if (area < 6f || k.x1 - k.x0 < 1.6f || k.z1 - k.z0 < 1.6f) continue;
                var zona = an.zonas.TryGetValue(k, out var z) ? z : Zona.Circulacao;
                int quantos = 0;
                bool fechado = SalaFechada(semente, an, k);
                if (fechado && r.Chance(0.4f)) quantos = r.Entre(2, 4);
                else if (zona == Zona.Circulacao)
                {
                    // um isolado a cada uns 140 m² de corredor e de espera
                    float n = area / 140f;
                    quantos = Mathf.FloorToInt(n) + (r.Fracao() < n - Mathf.Floor(n) ? 1 : 0);
                }
                else if (Medicos.Contains(k.tipo)) quantos = r.Chance(0.35f) ? r.Entre(1, 3) : 0;
                else if (zona == Zona.Servico) quantos = r.Chance(0.2f) ? 1 : 0;
                else quantos = r.Chance(0.1f) ? 1 : 0;
                if (quantos <= 0) continue;
                grupos.Add(new Grupo
                {
                    area = Rect.MinMaxRect(k.x0, k.z0, k.x1, k.z1), andar = f, quantos = quantos, comodo = k.tipo, fechado = fechado,
                });
            }
            // o teto do andar: tira um de cada vez dos grupos de fora das salas fechadas (os maiores primeiro), e depois delas
            int total = 0;
            foreach (var g in grupos) total += g.quantos;
            int maximo = MaximoNoAndar[Mathf.Clamp(f, 0, MaximoNoAndar.Length - 1)];
            for (int passo = 0; total > maximo; passo++)
            {
                bool tirou = false;
                for (int i = grupos.Count - 1; i >= 0 && total > maximo; i--)
                {
                    var g = grupos[i];
                    if (passo < 3 && g.fechado) continue;
                    g.quantos--;
                    total--;
                    tirou = true;
                    if (g.quantos <= 0) grupos.RemoveAt(i);
                    else grupos[i] = g;
                }
                if (!tirou && passo >= 3) break;
            }
            return grupos;
        }

        /// <summary>
        /// O cômodo começa fechado: tem porta que abre e todas começam fechadas (sem passagem, sem vão aberto e sem porta
        /// parada aberta para ele).
        /// </summary>
        public static bool SalaFechada(int semente, Andar an, Comodo k)
        {
            bool temPorta = false;
            foreach (var p in an.portas)
            {
                if (p.a != k && p.b != k) continue;
                if (p.aberto || p.folhas == 0) return false;
                if (Hospital.QueAbre(an, p))
                {
                    Hospital.ComecoDaPorta(semente, an, p, out bool fechada, out _);
                    if (!fechada) return false;
                    temPorta = true;
                }
                else if (p.estado == EstadoDaPorta.Aberta) return false;
            }
            return temPorta;
        }
    }
}
