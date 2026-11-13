using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// O Hospital Principal no jogo (o Plano_Hospital.md da pasta de arte, a parte 7), sem o Play:
    ///  - <b>as portas que abrem</b>: com <see cref="Obra.portasVivas"/>, cada folha das portas que abrem vira um ponto (com a
    ///    malha dela, a dupla com a outra folha), e são todas as de <see cref="Hospital.QueAbre"/>; as fechadas e as trancadas
    ///    do começo saem da semente do mundo, iguais toda vez;
    ///  - <b>os zumbis de dentro</b>: os grupos de cada andar ficam nos cômodos de verdade (nunca nos lacrados nem nas
    ///    escadas), abaixo do teto do andar, e há salas fechadas com zumbis;
    ///  - <b>o saque</b>: em cada andar, de min a max (ou menos, sem lugar), só até o raro, cada item num lugar de um móvel do
    ///    andar e dentro do cômodo dele, o mesmo toda vez; e a medicação e os depósitos guardam os remédios.
    /// </summary>
    public class HospitalNoJogoTestes
    {
        static readonly Medidas M = Medidas.DoJogo;

        [Test]
        public void AsPortasQueAbremViramPontos()
        {
            var relato = new StringBuilder();
            var erros = new List<string>();
            int esperadas = 0, achadas = 0, fechadas = 0, trancadas = 0, duplas = 0;
            for (int f = 0; f < Andares; f++)
            {
                var an = DoAndar(f);
                foreach (var p in an.portas) if (Hospital.QueAbre(an, p)) esperadas += p.folhas;
            }
            var obras = new List<(string nome, Hospital h)>();
            var casca = new Hospital(M, -1, 7) { portasVivas = true, sementeDasPortas = 7 };
            casca.MontarCasca();
            obras.Add(("casca", casca));
            for (int f = 0; f < Andares; f++)
            {
                var h = new Hospital(M, f, 7) { portasVivas = true, sementeDasPortas = 7 };
                h.MontarAndar();
                obras.Add((Codigos[f], h));
            }
            foreach (var (nome, h) in obras)
            {
                var ps = h.portas;
                achadas += ps.Count;
                for (int i = 0; i < ps.Count; i++)
                {
                    var d = ps[i];
                    string qual = $"{nome}, a porta {i}";
                    if (!d.doKit) erros.Add($"{qual}: sem o começo da obra");
                    if (d.folha == null || d.folha.Triangulos == 0) erros.Add($"{qual}: sem a malha da folha");
                    if (d.largura < 0.4f || d.altura < 1.9f) erros.Add($"{qual}: a folha de {d.largura:0.00} × {d.altura:0.00} m");
                    if (d.abertaEm < 60f) erros.Add($"{qual}: aberta só a {d.abertaEm:0}°");
                    if (d.trancada && d.giro > 1f) erros.Add($"{qual}: trancada e aberta");
                    if (d.giro < 1f) fechadas++;
                    if (d.trancada) trancadas++;
                    if (d.Par >= 0)
                    {
                        duplas++;
                        if (d.Par >= ps.Count || ps[d.Par].Par != i) erros.Add($"{qual}: a outra folha ({d.Par}) não aponta de volta");
                        else if ((ps[d.Par].giro < 1f) != (d.giro < 1f) || ps[d.Par].trancada != d.trancada) erros.Add($"{qual}: as duas folhas começam diferentes");
                    }
                }
                MalhaDeObra.Devolver(h.vis);
                MalhaDeObra.Devolver(h.col);
            }
            relato.AppendLine($"{achadas} folhas que abrem ({esperadas} esperadas; {duplas / 2} duplas): {fechadas} fechadas, {trancadas} trancadas");
            if (achadas != esperadas) erros.Add($"{achadas} folhas, mas a planta tem {esperadas} das que abrem");
            if (fechadas == 0 || trancadas == 0) erros.Add("nenhuma fechada ou nenhuma trancada");
            // o mesmo começo toda vez, e outro mundo, outro começo
            var an1 = DoAndar(Terreo);
            int iguais = 0, diferentes = 0;
            foreach (var p in an1.portas)
            {
                if (!Hospital.QueAbre(an1, p)) continue;
                Hospital.ComecoDaPorta(7, an1, p, out bool f1, out bool t1);
                Hospital.ComecoDaPorta(7, an1, p, out bool f2, out bool t2);
                Hospital.ComecoDaPorta(8, an1, p, out bool f3, out _);
                if (f1 != f2 || t1 != t2) erros.Add("o começo de uma porta mudou com a mesma semente");
                if (f1 == f3) iguais++; else diferentes++;
            }
            if (diferentes == 0) erros.Add("a semente do mundo não muda o começo das portas");
            Debug.Log("[Hospital] " + relato);
            Assert.IsEmpty(erros, relato + string.Join("\n", erros));
        }

        [Test]
        public void OsZumbisFicamNosComodos()
        {
            var erros = new List<string>();
            var relato = new StringBuilder();
            int salasFechadas = 0;
            for (int semente = 1; semente <= 10; semente++)
            {
                var linha = new List<string>();
                for (int f = 0; f < Andares; f++)
                {
                    var an = DoAndar(f);
                    var grupos = ZumbisDoHospital.DoAndar(semente, f);
                    var de = ZumbisDoHospital.DoAndar(semente, f);
                    if (grupos.Count != de.Count || grupos.Sum(g => g.quantos) != de.Sum(g => g.quantos)) erros.Add($"{semente}, {Codigos[f]}: mudou com a mesma semente");
                    int total = grupos.Sum(g => g.quantos);
                    if (total > ZumbisDoHospital.MaximoNoAndar[f]) erros.Add($"{semente}, {Codigos[f]}: {total} zumbis, acima do teto");
                    foreach (var g in grupos)
                    {
                        var k = an.Em(g.area.center.x, g.area.center.y);
                        if (k == null) { erros.Add($"{semente}, {Codigos[f]}: um grupo fora dos cômodos"); continue; }
                        if (an.fechados.Contains(k) || k.tipo == "escada" || k.tipo == "elevador") erros.Add($"{semente}, {Codigos[f]}: um grupo em {an.nomes[k]}");
                        if (g.fechado) salasFechadas++;
                    }
                    linha.Add($"{Codigos[f]} {total}");
                }
                relato.AppendLine($"semente {semente}: {string.Join(", ", linha)}");
            }
            relato.AppendLine($"{salasFechadas} grupos em salas fechadas nas 10 sementes");
            if (salasFechadas == 0) erros.Add("nenhuma sala fechada com zumbis");
            Debug.Log("[Hospital] Zumbis:\n" + relato);
            Assert.IsEmpty(erros, relato + string.Join("\n", erros));
        }

        [Test]
        public void OSaqueFicaNosMoveisDoAndar()
        {
            var erros = new List<string>();
            var relato = new StringBuilder();
            var porRaridade = new int[5];
            var porComodo = new Dictionary<string, int>();
            var porItem = new Dictionary<string, int>();
            int total = 0, remediosNoEstoque = 0;
            for (int semente = 1; semente <= 20; semente++)
                for (int f = 0; f < Andares; f++)
                {
                    var an = DoAndar(f);
                    var moveis = MoveisDoHospital.DoAndar(f);
                    var saque = SaqueDoHospital.DoAndar(semente, f);
                    var de = SaqueDoHospital.DoAndar(semente, f);
                    if (saque.Count != de.Count) erros.Add($"{semente}, {Codigos[f]}: mudou com a mesma semente");
                    var (min, max) = SaqueDoHospital.QuantosNoAndar[f];
                    if (saque.Count > max || saque.Count < min - 2) erros.Add($"{semente}, {Codigos[f]}: {saque.Count} itens (de {min} a {max})");
                    var usados = new HashSet<(int, int)>();
                    foreach (var it in saque)
                    {
                        total++;
                        porRaridade[(int)it.raridade]++;
                        porComodo[it.comodo] = porComodo.TryGetValue(it.comodo, out int c) ? c + 1 : 1;
                        porItem[it.nome] = porItem.TryGetValue(it.nome, out int n) ? n + 1 : 1;
                        if (it.raridade > Raridade.Raro) erros.Add($"{semente}, {Codigos[f]}: {it.nome} {it.raridade}");
                        if (it.ponto < 0 || it.ponto >= moveis.Count) { erros.Add($"{semente}, {Codigos[f]}: o ponto {it.ponto} fora da lista"); continue; }
                        var m = moveis[it.ponto];
                        var lugares = LugaresDoSaque.Do(m.modelo);
                        if (it.lugar < 0 || it.lugar >= lugares.Length) erros.Add($"{semente}, {Codigos[f]}: o lugar {it.lugar} de {m.modelo}");
                        if (!usados.Add((it.ponto, it.lugar))) erros.Add($"{semente}, {Codigos[f]}: dois itens no mesmo lugar de {m.modelo}");
                        var k = an.Em(it.apoio.x, it.apoio.z);
                        if (k == null || an.fechados.Contains(k)) erros.Add($"{semente}, {Codigos[f]}: {it.nome} fora de um cômodo aberto");
                        if (Mathf.Abs(it.apoio.y - Base(f)) > 2.2f) erros.Add($"{semente}, {Codigos[f]}: {it.nome} a {it.apoio.y - Base(f):0.00} m do chão");
                        if ((it.comodo == "medicacao" || it.comodo == "deposito") && (CatalogoDoSaque.Itens.First(c2 => c2.nome == it.nome).especie & EspecieDoSaque.Farmacia) != 0)
                            remediosNoEstoque++;
                    }
                }
            relato.AppendLine($"{total} itens em 20 sementes ({total / 20f:0.0} por hospital)");
            relato.AppendLine("raridades: " + string.Join(", ", porRaridade.Select((n, i) => $"{CatalogoDoSaque.NomesDasRaridades[i]} {100f * n / Mathf.Max(1, total):0.0}%")));
            relato.AppendLine("cômodos: " + string.Join(", ", porComodo.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
            relato.AppendLine("itens: " + string.Join(", ", porItem.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
            if (remediosNoEstoque == 0) erros.Add("nenhum remédio na medicação nem nos depósitos");
            Debug.Log("[Hospital] Saque:\n" + relato);
            Assert.IsEmpty(erros, relato + string.Join("\n", erros.Take(40)));
        }
    }
}
