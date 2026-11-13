using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// Os moradores no bunker (a parte 6 do Plano_NPCs_Bunker.md da pasta de arte): os lugares tirados dos móveis
    /// (<see cref="LugaresDoBunker"/>) e a agenda (<see cref="AgendaDoMorador"/>).
    /// </summary>
    public class VidaNoBunkerTestes
    {
        [Test]
        public void OsLugaresBastamParaOsMoradores()
        {
            var todos = LugaresDoBunker.Todos;
            int Conta(TipoDoLugar t, int f) => todos.Count(l => l.tipo == t && l.andar == f);
            Assert.GreaterOrEqual(Conta(TipoDoLugar.Banco, 2), 22, "os bancos do refeitório (um lugar por morador)");
            Assert.GreaterOrEqual(Conta(TipoDoLugar.Sofa, 1), 8, "os assentos dos sofás da sala comum");
            Assert.GreaterOrEqual(Conta(TipoDoLugar.Espelho, 3), 4, "os espelhos do banheiro dos dormitórios");
            Assert.GreaterOrEqual(Conta(TipoDoLugar.Pia, 2), 1, "a pia da lavagem");
            Assert.GreaterOrEqual(Conta(TipoDoLugar.Bancada, 0), 1, "a bancada do arsenal");
            // as camas: um morador por móvel, dois na de casal (as 22 camas do plano)
            var camas = todos.Where(l => l.tipo == TipoDoLugar.Cama).ToList();
            int pessoas = camas.GroupBy(l => l.movel).Sum(g => MoveisDoBunker.Todos[g.Key].modelo == "CamaCasal" ? Mathf.Min(2, g.Count()) : 1);
            Assert.GreaterOrEqual(pessoas, 22, "as camas para 22");
            foreach (var nome in new[] { "Quarto 5", "Quarto 6" })
                Assert.AreEqual(2, camas.Count(l => l.nome == nome), $"os dois lados da cama de casal do {nome}");
            Assert.IsFalse(todos.Any(l => l.andar >= 4), "nada no B4 (os moradores não descem lá)");
            // a segunda fatia: cada estante (e bancada que guarda) do B0 ao B3 tem onde o morador parar na frente
            var moveis = MoveisDoBunker.Todos;
            for (int i = 0; i < moveis.Length; i++)
                if (moveis[i].andar < 4 && LugaresDoBunker.GuardaItens(moveis[i].modelo))
                    Assert.IsTrue(todos.Any(l => l.tipo == TipoDoLugar.Estante && l.movel == i), $"{moveis[i].modelo} no {PlantaDeBunker.Codigos[moveis[i].andar]}: sem lugar na frente");
        }

        [Test]
        public void OsLugaresDoLazerVemDepoisDosOutros()
        {
            // a parte 8 (o lazer): o rádio, a mesa de dobrar a roupa, a volta da mesa de sinuca, o suporte dos tacos e a
            // mesinha do fumo, todos depois dos lugares de antes (os índices do save não mudam: a cama e o lugar de cada um)
            var todos = LugaresDoBunker.Todos;
            var moveis = MoveisDoBunker.Todos;
            bool DoLazer(LugarDoMorador l) =>
                l.tipo == TipoDoLugar.Radio || l.tipo == TipoDoLugar.Dobrar || l.tipo == TipoDoLugar.Sinuca
                || (l.movel >= 0 && LugaresDoBunker.GuardaDoLazer(moveis[l.movel].modelo));
            int primeiro = System.Array.FindIndex(todos, DoLazer);
            Assert.GreaterOrEqual(primeiro, 0, "os lugares do lazer");
            for (int i = primeiro; i < todos.Length; i++) Assert.IsTrue(DoLazer(todos[i]), $"o lugar {i} ({todos[i].tipo}, {todos[i].nome}) no meio dos do lazer");
            Assert.AreEqual(1, todos.Count(l => l.tipo == TipoDoLugar.Radio), "a cadeira do rádio (B0)");
            Assert.GreaterOrEqual(todos.Count(l => l.tipo == TipoDoLugar.Dobrar), 2, "a mesa de dobrar a roupa (B3)");
            Assert.GreaterOrEqual(todos.Count(l => l.tipo == TipoDoLugar.Sinuca), 3, "em volta da mesa de sinuca (B1)");
            foreach (var modelo in new[] { "SuporteDeTacos", "MesaDeCanto" })
            {
                int i = System.Array.FindIndex(moveis, x => x.modelo == modelo);
                Assert.GreaterOrEqual(i, 0, modelo);
                Assert.IsTrue(todos.Any(l => l.tipo == TipoDoLugar.Estante && l.movel == i), $"{modelo}: sem lugar na frente");
            }
            // os móveis novos vêm depois dos antigos (o índice de cada móvel é a estante dos itens guardados)
            Assert.AreEqual("SuporteDeTacos", moveis[MoveisDoBunker.Antigos].modelo, "o primeiro móvel do lazer");
            // o canto do fumo: em pé, perto do cinzeiro
            int mesinha = System.Array.FindIndex(moveis, x => x.modelo == "MesaDeCanto");
            Assert.GreaterOrEqual(todos.Count(l => l.tipo == TipoDoLugar.EmPe && l.movel == mesinha), 1, "o canto do fumo");
        }

        [Test]
        public void OsLugaresFicamDentroDoBunkerEFicamNoChao()
        {
            foreach (var l in LugaresDoBunker.Todos)
            {
                Assert.AreEqual(PlantaDeBunker.Base(l.andar), l.raiz.y, 0.001f, $"{l.tipo} {l.nome}: no chão do andar");
                Assert.That(l.raiz.x, Is.InRange(PlantaDeBunker.X0, PlantaDeBunker.X1), $"{l.tipo} {l.nome}: dentro em x");
                Assert.That(l.raiz.z, Is.InRange(PlantaDeBunker.Z0, PlantaDeBunker.Z1), $"{l.tipo} {l.nome}: dentro em z");
                var k = PlantaDeBunker.DoAndar(l.andar).Em(l.raiz.x, l.raiz.z);
                Assert.IsNotNull(k, $"{l.tipo} {l.nome}: num cômodo");
                Assert.IsFalse(PlantaDeBunker.DoNucleo(k), $"{l.tipo} {l.nome}: fora do núcleo");
            }
        }

        [Test]
        public void AAgendaCobreODia()
        {
            var normal = new FichaDoMorador { idade = 30 };
            var velho = new FichaDoMorador { idade = 70, tracos = { "Religioso" } };
            var notivago = new FichaDoMorador { idade = 30, tracos = { "Notivago" } };
            var insone = new FichaDoMorador { idade = 30, condicoes = { "Insonia" } };
            foreach (var f in new[] { normal, velho, notivago, insone })
                for (float h = 0f; h < 24f; h += 0.25f)
                {
                    var a = AgendaDoMorador.Programada(f, h, out float dura);
                    Assert.AreNotEqual(AtividadeDoMorador.Nada, a);
                    Assert.That(dura, Is.InRange(0.01f, 24f), $"{a} às {h}");
                }
            Assert.AreEqual(AtividadeDoMorador.Dormir, AgendaDoMorador.Programada(normal, 23f, out _));
            Assert.AreEqual(AtividadeDoMorador.Comer, AgendaDoMorador.Programada(normal, 12.2f, out _));
            Assert.AreEqual(AtividadeDoMorador.Lazer, AgendaDoMorador.Programada(insone, 23f, out _));
            Assert.AreEqual(AtividadeDoMorador.Dormir, AgendaDoMorador.Programada(notivago, 12f, out _));
            Assert.AreEqual(AtividadeDoMorador.Trabalho, AgendaDoMorador.Programada(notivago, 23f, out _));
            Assert.AreEqual(AtividadeDoMorador.Cochilo, AgendaDoMorador.Programada(velho, 14.5f, out _));
            Assert.AreEqual(AtividadeDoMorador.Rezar, AgendaDoMorador.Programada(velho, 21.7f, out _));
            // dormindo às 23 h, até as 6:30
            AgendaDoMorador.Programada(normal, 23f, out float ateAcordar);
            Assert.AreEqual(7.5f, ateAcordar, 0.001f);
        }

        [Test]
        public void ARapidezVemDaFicha()
        {
            var normal = new FichaDoMorador { idade = 30 };
            var manco = new FichaDoMorador { idade = 70, condicoes = { "JoelhoRuim" } };
            var rapido = new FichaDoMorador { idade = 25, tracos = { "Rapido" } };
            Assert.Less(AgendaDoMorador.Velocidade(manco), AgendaDoMorador.Velocidade(normal) * 0.7f);
            Assert.Greater(AgendaDoMorador.Velocidade(rapido), AgendaDoMorador.Velocidade(normal));
            Assert.IsTrue(AgendaDoMorador.Mancando(manco));
            Assert.IsTrue(AgendaDoMorador.Velho(manco));
        }

        [Test]
        public void OHumorVemDosMotivosEDosTracos()
        {
            var f = new FichaDoMorador { idade = 30 };
            var v = new VidaDoMorador();
            Assert.AreEqual(50f, HumorDoMorador.Alvo(f, v, 12f), 0.001f, "sem nada, 50");
            f.tracos.Add("Alegre");
            Assert.AreEqual(60f, HumorDoMorador.Alvo(f, v, 12f), 0.001f, "o alegre, +10");
            f.tracos.Add("Notivago");
            Assert.AreEqual(64f, HumorDoMorador.Alvo(f, v, 22f), 0.001f, "o notívago, +4 de noite");
            Assert.AreEqual(60f, HumorDoMorador.Alvo(f, v, 12f), 0.001f, "e nada de dia");

            HumorDoMorador.Por(v, "Fome", -15f, 10f);
            HumorDoMorador.Por(v, "Fome", -15f, 30f);
            Assert.AreEqual(1, v.motivos.Count, "o mesmo motivo é renovado, não repetido");
            Assert.AreEqual(30f, v.motivos[0].ate, 0.001f, "com o prazo mais longo");
            Assert.AreEqual(45f, HumorDoMorador.Alvo(f, v, 12f), 0.001f);
            HumorDoMorador.Vencer(v, 31f);
            Assert.AreEqual(0, v.motivos.Count, "o vencido sai");

            HumorDoMorador.Por(v, "Teste", -500f, 99f);
            Assert.AreEqual(0f, HumorDoMorador.Alvo(f, v, 12f), 0.001f, "nunca abaixo de 0");
            HumorDoMorador.Por(v, "Teste", 500f, 99f);
            Assert.AreEqual(100f, HumorDoMorador.Alvo(f, v, 12f), 0.001f, "nem acima de 100");

            Assert.AreEqual(FaixaDoHumor.Triste, HumorDoMorador.Faixa(34f));
            Assert.AreEqual(FaixaDoHumor.Neutro, HumorDoMorador.Faixa(35f));
            Assert.AreEqual(FaixaDoHumor.Feliz, HumorDoMorador.Faixa(66f));
            var chaves = new System.Collections.Generic.HashSet<string>();
            foreach (var m in HumorDoMorador.Tabela)
            {
                Assert.IsTrue(chaves.Add(m.chave), $"a chave {m.chave} repete");
                Assert.IsFalse(string.IsNullOrEmpty(m.nome), $"o motivo {m.chave} sem nome");
            }
        }
    }
}
