using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// De onde saem os sons do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 6), tirados da planta (nas
    /// coordenadas da obra), para o AmbienteSonoro (que fica no assembly do jogo; aqui, só os lugares). Sem música (a pedido):
    /// <list type="bullet">
    /// <item>Porta: as portas de folha entre dois cômodos (a meia altura, no vão);</item>
    /// <item>Vento: as paredes de fora dos cômodos com janela (as fendas e os vidros quebrados), a entrada e a recepção;</item>
    /// <item>Goteira: embaixo das placas do forro que caíram, nos úmidos (os banheiros, a copa, a limpeza, a lavanderia, os
    /// resíduos), no subsolo inteiro e no 3º (embaixo da cobertura);</item>
    /// <item>Metal e Cano: as salas de serviço e os corredores do subsolo (os canos no teto);</item>
    /// <item>Estrutura: os corredores e os cômodos grandes (a madeira e o forro rangendo);</item>
    /// <item>Papeis: os escritórios, o arquivo, a recepção e o posto, junto das janelas;</item>
    /// <item>Pancada: o meio dos cômodos (longe);</item>
    /// <item>Maca: as macas, as camas e as cadeiras de rodas dos móveis (MoveisDoHospital);</item>
    /// <item>Zumbi: os lacrados (a A-01, as salas técnicas, o quarto 4 do isolamento) e as câmaras frias.</item>
    /// </list>
    /// </summary>
    public static class SonsDoHospital
    {
        /// <summary>Os tipos, na ordem do AmbienteSonoro do hospital (o nome do arquivo de som é o mesmo: Porta_01...).</summary>
        public static readonly string[] Tipos = { "Porta", "Vento", "Goteira", "Metal", "Cano", "Estrutura", "Papeis", "Pancada", "Maca", "Zumbi" };

        /// <summary>A faixa de altura de cada andar (as áreas do som), do chão até o chão de cima.</summary>
        public static Vector2[] Areas()
        {
            var a = new Vector2[Andares];
            for (int f = 0; f < Andares; f++) a[f] = new Vector2(Base(f) - 0.3f, f == Andares - 1 ? TopoDaCobertura + 1f : Base(f + 1) - 0.3f);
            return a;
        }

        /// <summary>O hospital inteiro (o corpo, os anexos e a cobertura), onde os sons rodam.</summary>
        public static Bounds Limites()
        {
            var b = new Bounds();
            b.SetMinMax(new Vector3(GaragemDasAmbulancias.xMin - 1f, Base(0) - 1f, Mathf.Min(Z0, Entrada.yMin) - 1f), new Vector3(X1 + 1f, TopoDaCobertura + 1f, Z1 + 1f));
            return b;
        }

        /// <summary>Os pontos de som: o lugar (nas coordenadas da obra), o tipo (o índice em <see cref="Tipos"/>) e o andar.</summary>
        public static List<(Vector3 lugar, int tipo, int andar)> Pontos()
        {
            var l = new List<(Vector3, int, int)>();
            int T(string nome) => System.Array.IndexOf(Tipos, nome);
            for (int f = 0; f < Andares; f++)
            {
                var an = DoAndar(f);
                float b = Base(f);
                // as portas de folha entre dois cômodos
                foreach (var p in an.portas)
                {
                    if (p.aberto || p.b == null || p.folhas == 0 || p.tipo == TipoDePorta.Elevador) continue;
                    if (LugarDaPorta(p, out var xz)) l.Add((new Vector3(xz.x, b + 1.1f, xz.y), T("Porta"), f));
                }
                foreach (var k in an.comodos)
                {
                    if (DoNucleo(k)) continue;
                    var c = new Vector3((k.x0 + k.x1) / 2f, b + 1.2f, (k.z0 + k.z1) / 2f);
                    float area = (k.x1 - k.x0) * (k.z1 - k.z0);
                    bool fechado = an.fechados.Contains(k);
                    // os lacrados e as câmaras frias: alguém (alguma coisa) lá dentro
                    if (fechado || k.tipo == "camaras") l.Add((c, T("Zumbi"), f));
                    if (fechado) continue;
                    // o vento nas paredes de fora (acima do chão: o subsolo não tem janela), a entrada e a recepção
                    if (f > 0)
                    {
                        if (Mathf.Abs(k.z0 - Z0) < 0.01f) l.Add((new Vector3(c.x, b + 1.7f, k.z0 + 0.6f), T("Vento"), f));
                        if (Mathf.Abs(k.z1 - Z1) < 0.01f) l.Add((new Vector3(c.x, b + 1.7f, k.z1 - 0.6f), T("Vento"), f));
                        if (Mathf.Abs(k.x0 - X0) < 0.01f) l.Add((new Vector3(k.x0 + 0.6f, b + 1.7f, c.z), T("Vento"), f));
                        if (Mathf.Abs(k.x1 - X1) < 0.01f) l.Add((new Vector3(k.x1 - 0.6f, b + 1.7f, c.z), T("Vento"), f));
                        if (k.tipo is "entrada" or "recepcao") l.Add((c, T("Vento"), f));
                    }
                    // as goteiras: os úmidos, o subsolo inteiro, o 3º (a cobertura)
                    if (f == 0 || f == Andares - 1 || k.tipo is "banheiro" or "copa" or "limpeza" or "lavanderia" or "residuos")
                        l.Add((new Vector3(c.x, b + 0.3f, c.z), T("Goteira"), f));
                    // o metal e os canos: o serviço do subsolo e os corredores dele
                    if (f == 0 && k.tipo is "geradores" or "eletrica" or "bombas" or "manutencao" or "oficina" or "lavanderia" or "residuos" or "deposito")
                    {
                        l.Add((new Vector3(c.x, b + 2.6f, c.z), T("Metal"), f));
                        l.Add((new Vector3(c.x, b + 3.0f, c.z), T("Cano"), f));
                    }
                    if (k.tipo == "corredor")
                    {
                        // ao comprido do corredor, um ponto a cada 8 m
                        bool emX = k.x1 - k.x0 >= k.z1 - k.z0;
                        float comp = emX ? k.x1 - k.x0 : k.z1 - k.z0;
                        for (float s = 4f; s < comp - 2f; s += 8f)
                        {
                            var q = emX ? new Vector3(k.x0 + s, 0f, c.z) : new Vector3(c.x, 0f, k.z0 + s);
                            if (f == 0) { l.Add((new Vector3(q.x, b + 3.0f, q.z), T("Cano"), f)); l.Add((new Vector3(q.x, b + 2.8f, q.z), T("Metal"), f)); }
                            l.Add((new Vector3(q.x, b + 3.1f, q.z), T("Estrutura"), f));
                        }
                    }
                    else if (area > 60f) l.Add((new Vector3(c.x, b + 3.1f, c.z), T("Estrutura"), f));
                    // os papéis: os escritórios, o arquivo, a recepção e o posto
                    if (k.tipo is "escritorio" or "arquivo" or "secretaria" or "rh" or "planejamento" or "diretor" or "recepcao" or "posto" or "reuniao")
                        l.Add((new Vector3(c.x, b + 0.2f, c.z), T("Papeis"), f));
                    // a pancada longe: o meio dos cômodos grandes
                    if (area > 40f) l.Add((c, T("Pancada"), f));
                }
                // as goteiras embaixo das placas do forro que caíram
                foreach (var (placa, _) in Hospital.PlacasQueCairam(f))
                    l.Add((new Vector3(placa.center.x, b + 0.3f, placa.center.y), T("Goteira"), f));
                // as macas, as camas e as cadeiras de rodas
                foreach (var m in MoveisDoHospital.DoAndar(f))
                    if (m.modelo is "MacaEmergencia" or "CamaHospital" or "CadeiraDeRodas" or "MacaNecroterio" or "MacaExame")
                        l.Add((m.lugar + Vector3.up * 0.4f, T("Maca"), f));
            }
            return l;
        }

        // o meio de uma porta entre dois cômodos (na linha da parede que eles dividem), em x e z
        static bool LugarDaPorta(Porta p, out Vector2 xz)
        {
            var a = p.a;
            var c = p.b;
            xz = default;
            if (Mathf.Abs(a.x1 - c.x0) < 0.01f) { xz = new Vector2(a.x1, p.pos); return true; }
            if (Mathf.Abs(a.x0 - c.x1) < 0.01f) { xz = new Vector2(a.x0, p.pos); return true; }
            if (Mathf.Abs(a.z1 - c.z0) < 0.01f) { xz = new Vector2(p.pos, a.z1); return true; }
            if (Mathf.Abs(a.z0 - c.z1) < 0.01f) { xz = new Vector2(p.pos, a.z0); return true; }
            return false;
        }
    }
}
