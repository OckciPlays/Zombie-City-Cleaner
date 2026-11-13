using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os prédios especiais das cidades do Lakewood (Plano_Lakewood, Etapa 6, a Woodcreek, a parte 6), pelo kit das casas:
    /// cada um é uma <see cref="Planta"/> desenhada aqui (os cômodos, as portas e a aparência), levantada pelo
    /// <see cref="Casa"/> como as casas (as paredes, as janelas, o telhado, as portas que abrem, a mobília dos cômodos que
    /// têm receita e o saque nela), e o que a casa não tem vai por cima (<see cref="Extras"/>): os bancos, o altar e a torre
    /// da igreja, as carteiras e os quadros-negros da escola, os armários e as mangueiras dos bombeiros, a placa e a antena da
    /// guarda florestal, o estacionamento e a placa do motel. Os nomes nas placas são em inglês (o jogo se passa nos
    /// Estados Unidos). Os tipos: "igreja", "escola", "bombeiros", "guarda florestal", "motel" e "cabana" (as 26 do Cabin
    /// Loop). Sem espelhar (as letras sairiam ao contrário), menos as cabanas.
    /// </summary>
    public static class EspeciaisDeLakewood
    {
        /// <summary>O desenho de um especial: a planta, as medidas (o pé-direito da igreja e dos bombeiros é mais alto), o
        /// recuo da frente do lote até a frente da construção e o que fica na frente dela (a torre da igreja).</summary>
        public sealed class Desenho
        {
            public Planta Planta;
            public Medidas Medidas;
            public float Recuo, NaFrente;
            public string Nome;
        }

        public static string NomeDe(string tipo) => tipo switch
        {
            "igreja" => "Igreja",
            "escola" => "Escola",
            "bombeiros" => "Bombeiros",
            "guarda florestal" => "Guarda florestal",
            "motel" => "Motel",
            "cabana" => "Cabana",
            _ => "Especial",
        };

        /// <summary>O desenho do especial pelo tipo (null: o tipo não é conhecido).</summary>
        public static Desenho Desenhar(string tipo, int semente)
        {
            var r = new System.Random(semente);
            Desenho d = tipo switch
            {
                "igreja" => Igreja(r),
                "escola" => Escola(r),
                "bombeiros" => Bombeiros(r),
                "guarda florestal" => GuardaFlorestal(r),
                "motel" => Motel(r),
                "cabana" => Cabana(r),
                _ => null,
            };
            if (d == null) return null;
            d.Nome = NomeDe(tipo);
            var p = d.Planta;
            p.molde = "Especial";
            p.semente = r.Next();
            p.portaDaFrenteAberta = true;
            p.sangue = r.Next(0, 4);
            return d;
        }

        static float F(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        static T Um<T>(System.Random r, params T[] o) => o[r.Next(o.Length)];

        static void Pintar(Comodo k, string papel, string piso)
        {
            k.papel = papel;
            k.piso = piso;
        }

        static Ligacao Vao(Planta p, Comodo a, int lado, float pos, float largura)
        {
            var l = new Ligacao { a = a, lado = lado, pos = pos, passagem = true, largura = largura };
            p.ligacoes.Add(l);
            return l;
        }

        // ------------------------------------------------------------ as plantas

        // a igreja: a nave comprida com a cumeeira ao longo dela, as janelas altas dos lados, a sacristia e o banheiro no
        // fundo e a torre na frente (a entrada passa por baixo dela)
        static Desenho Igreja(System.Random r)
        {
            var m = Medidas.DoJogo;
            m.altura = 5.4f;
            m.topoJanela = 4.4f;
            m.peitoril = 1.7f;
            m.janela = 1.05f;
            var p = new Planta { W = 12f, D = 22f };
            float x0 = p.X0, x1 = p.X1, z0 = p.Z0, z1 = p.Z1;
            var nave = p.Comodo("nave", 0, x0, z0, x1, z1 - 4f);
            var sacristia = p.Comodo("escritorio", 0, x0, z1 - 4f, 1f, z1);
            var ban = p.Comodo("banheiro", 0, 1f, z1 - 4f, x1, z1);
            Pintar(nave, "ParedeInterna", "Taco");
            Pintar(sacristia, "PapelVerde", "Taco");
            Pintar(ban, "Azulejo", "Azulejo");
            p.xPortaFrente = 0f;
            p.Fora(nave, 0, 0f);
            p.Porta(nave, sacristia, x0 + 1.6f);
            p.Porta(nave, ban, x1 - 1.6f);
            p.Fora(sacristia, 2, x0 + 2.4f);
            p.cor = "ParedeBege"; p.escura = "ParedeBegeEscura";
            p.telhado = "TelhadoEscuro"; p.telhadoEscuro = "Telhado";
            p.corDaPorta = "Madeira";
            p.cumeeiraNoX = false;
            p.janelas = new[] { 2, 1 };
            return new Desenho { Planta = p, Medidas = m, Recuo = 4f, NaFrente = 3.6f };
        }

        // a escola: o corredor de ponta a ponta, as salas de aula dos dois lados, a entrada com a secretaria, o banheiro e o
        // depósito, e as saídas nas pontas do corredor
        static Desenho Escola(System.Random r)
        {
            var m = Medidas.DoJogo;
            var p = new Planta { W = 34f, D = 19f };
            float x0 = p.X0, x1 = p.X1, z0 = p.Z0, z1 = p.Z1, c0 = -1.6f, c1 = 1.6f;
            var cor = p.Comodo("corredor", 0, x0, c0, x1, c1);
            var a1 = p.Comodo("aula", 0, x0, z0, -9f, c0);
            var a2 = p.Comodo("aula", 0, -9f, z0, -2.5f, c0);
            var hall = p.Comodo("corredor", 0, -2.5f, z0, 2.5f, c0);
            var sec = p.Comodo("escritorio", 0, 2.5f, z0, 8.5f, c0);
            var a3 = p.Comodo("aula", 0, 8.5f, z0, x1, c0);
            var a4 = p.Comodo("aula", 0, x0, c1, -9f, z1);
            var a5 = p.Comodo("aula", 0, -9f, c1, -1f, z1);
            var ban = p.Comodo("banheiro", 0, -1f, c1, 3.5f, z1);
            var dep = p.Comodo("deposito", 0, 3.5f, c1, 8.5f, z1);
            var a6 = p.Comodo("aula", 0, 8.5f, c1, x1, z1);
            foreach (var k in new[] { a1, a2, a3, a4, a5, a6 }) Pintar(k, Um(r, "ParedeInterna", "PapelVerde"), "ConcretoClaro");
            Pintar(cor, "ParedeVerde", "ConcretoClaro");
            Pintar(hall, "ParedeVerde", "ConcretoClaro");
            Pintar(sec, "ParedeInterna", "Carpete");
            Pintar(ban, "Azulejo", "Azulejo");
            Pintar(dep, "Concreto", "Concreto");
            p.xPortaFrente = 0f;
            p.Fora(hall, 0, 0f);
            p.Passagem(hall, cor, 0f);
            p.Porta(a1, cor, -13f);
            p.Porta(a2, cor, -5.75f);
            p.Porta(sec, cor, 5.5f);
            p.Porta(a3, cor, 12.75f);
            p.Porta(cor, a4, -13f);
            p.Porta(cor, a5, -5f);
            p.Porta(cor, ban, 1.25f);
            p.Porta(cor, dep, 6f);
            p.Porta(cor, a6, 12.75f);
            p.Porta(hall, sec, -0.2f);
            p.Fora(cor, 1, 0f);
            p.Fora(cor, 3, 0f);
            p.cor = Um(r, "Tijolo", "ParedeBege"); p.escura = p.cor == "Tijolo" ? "TijoloEscuro" : "ParedeBegeEscura";
            p.telhado = "TelhadoEscuro"; p.telhadoEscuro = "Telhado";
            p.corDaPorta = "Batente";
            p.cumeeiraNoX = true;
            p.janelas = new[] { 2, 2 };
            return new Desenho { Planta = p, Medidas = m, Recuo = 6f };
        }

        // os bombeiros: o galpão das viaturas com os dois portões abertos na frente e, do lado, o escritório, a cozinha, o
        // dormitório e o banheiro
        static Desenho Bombeiros(System.Random r)
        {
            var m = Medidas.DoJogo;
            m.altura = 4.4f;
            var p = new Planta { W = 17f, D = 14f };
            float x0 = p.X0, x1 = p.X1, z0 = p.Z0, z1 = p.Z1, xg = 2.5f;
            var galpao = p.Comodo("garagem", 0, x0, z0, xg, z1);
            var esc = p.Comodo("escritorio", 0, xg, z0, x1, -2.6f);
            var coz = p.Comodo("cozinha", 0, xg, -2.6f, x1, 1.2f);
            var qto = p.Comodo("quarto", 0, xg, 1.2f, x1, 4.6f);
            var ban = p.Comodo("banheiro", 0, xg, 4.6f, x1, z1);
            Pintar(galpao, "Concreto", "ConcretoClaro");
            Pintar(esc, "ParedeInterna", "Carpete");
            Pintar(coz, "Azulejo", "ConcretoClaro");
            Pintar(qto, "ParedeInterna", "Taco");
            Pintar(ban, "Azulejo", "Azulejo");
            float xe = (xg + x1) / 2;
            p.xPortaFrente = xe;
            p.Fora(esc, 0, xe);
            Vao(p, galpao, 0, x0 + 2.75f, 4.2f);
            Vao(p, galpao, 0, x0 + 8.25f, 4.2f);
            p.Fora(galpao, 2, x0 + 3f);
            p.Porta(galpao, esc, -4.8f);
            p.Porta(esc, coz, xe);
            p.Porta(coz, qto, xe);
            p.Porta(qto, ban, xe);
            p.cor = "Tijolo"; p.escura = "TijoloEscuro";
            p.telhado = "TelhadoEscuro"; p.telhadoEscuro = "Telhado";
            p.corDaPorta = "PersianaVinho";
            p.cumeeiraNoX = true;
            p.janelas = new[] { 3, 1 };
            return new Desenho { Planta = p, Medidas = m, Recuo = 3f };
        }

        // a guarda florestal: a recepção com o rádio, a copa, o alojamento, o banheiro e o depósito do equipamento, e a
        // garagem da caminhonete
        static Desenho GuardaFlorestal(System.Random r)
        {
            var m = Medidas.DoJogo;
            var p = new Planta { W = 13f, D = 10f };
            float x0 = p.X0, x1 = p.X1, z0 = p.Z0, z1 = p.Z1;
            var esc = p.Comodo("escritorio", 0, x0, z0, 0f, 0f);
            var sala = p.Comodo("sala", 0, 0f, z0, x1, 0f);
            var qto = p.Comodo("quarto", 0, x0, 0f, -1.5f, z1);
            var ban = p.Comodo("banheiro", 0, -1.5f, 0f, 1.7f, z1);
            var dep = p.Comodo("deposito", 0, 1.7f, 0f, x1, z1);
            Pintar(esc, "ParedeInterna", "Taco");
            Pintar(sala, "PapelVerde", "Carpete");
            Pintar(qto, "ParedeInterna", "Taco");
            Pintar(ban, "Azulejo", "Azulejo");
            Pintar(dep, "Concreto", "Concreto");
            p.xPortaFrente = -3.25f;
            p.Fora(esc, 0, -3.25f);
            p.Porta(esc, sala, -2.5f);
            p.Porta(esc, qto, -4f);
            p.Porta(esc, ban, -0.75f);
            p.Porta(sala, dep, 4f);
            p.Fora(dep, 2, 4.5f);
            p.garagem = new Garagem { lado = 1, z0 = z0 + 0.4f, z1 = z0 + 7f, largura = 3.6f, coberta = true, fechada = r.NextDouble() < 0.5 };
            p.cor = "MadeiraVelha"; p.escura = "ParedeVerdeEscura";
            p.telhado = "TelhadoEscuro"; p.telhadoEscuro = "Telhado";
            p.corDaPorta = "PersianaVerde";
            p.varanda = true;
            p.cumeeiraNoX = true;
            p.chamine = -1;
            return new Desenho { Planta = p, Medidas = m, Recuo = 6f };
        }

        // o motel de beira de estrada: a recepção numa ponta e os 8 quartos em fila, cada um com a porta para a varanda
        // comprida e o banheiro no fundo
        public const int QuartosDoMotel = 8;
        const float LarguraDoQuarto = 4.1f, LarguraDaRecepcao = 4.8f;

        static Desenho Motel(System.Random r)
        {
            var m = Medidas.DoJogo;
            var p = new Planta { W = LarguraDaRecepcao + QuartosDoMotel * LarguraDoQuarto, D = 8.4f };
            float x0 = p.X0, z0 = p.Z0, z1 = p.Z1, zb = z1 - 2.6f;
            var rec = p.Comodo("escritorio", 0, x0, z0, x0 + LarguraDaRecepcao, z1);
            Pintar(rec, "PapelRosa", "Carpete");
            p.xPortaFrente = x0 + LarguraDaRecepcao / 2;
            p.Fora(rec, 0, p.xPortaFrente);
            string papel = Um(r, "PapelRosa", "PapelVerde", "ParedeInterna");
            for (int i = 0; i < QuartosDoMotel; i++)
            {
                float xa = x0 + LarguraDaRecepcao + i * LarguraDoQuarto, xb = xa + LarguraDoQuarto;
                var q = p.Comodo("quarto", 0, xa, z0, xb, zb);
                var b = p.Comodo("banheiro", 0, xa, zb, xb, z1);
                Pintar(q, papel, "Carpete");
                Pintar(b, "Azulejo", "Azulejo");
                p.Fora(q, 0, xa + 0.85f);
                p.Porta(q, b, (xa + xb) / 2);
            }
            p.cor = Um(r, "ParedeBege", "ParedeAzul"); p.escura = p.cor == "ParedeAzul" ? "ParedeAzulEscura" : "ParedeBegeEscura";
            p.telhado = Um(r, "Telhado", "TelhadoAzul"); p.telhadoEscuro = p.telhado == "TelhadoAzul" ? "TelhadoAzulEscuro" : "TelhadoEscuro";
            p.corDaPorta = Um(r, "PersianaVinho", "PersianaVerde", "Batente");
            p.varanda = true;
            p.varandaLarga = true;
            p.cumeeiraNoX = true;
            p.ar = 1;
            return new Desenho { Planta = p, Medidas = m, Recuo = 10f };
        }

        // a cabana da mata (o Cabin Loop): a sala e a cozinha na frente, o quarto e o banheiro atrás, a varanda e a chaminé
        static Desenho Cabana(System.Random r)
        {
            var m = Medidas.DoJogo;
            float W = Mathf.Round(F(r, 7.0f, 7.8f) * 10f) / 10f, D = Mathf.Round(F(r, 6.0f, 6.6f) * 10f) / 10f;
            var p = new Planta { W = W, D = D };
            float x0 = p.X0, x1 = p.X1, z0 = p.Z0, z1 = p.Z1, xs = x0 + Mathf.Round(W * 0.58f * 10f) / 10f, zs = z0 + Mathf.Round(D * 0.52f * 10f) / 10f;
            var sala = p.Comodo("sala", 0, x0, z0, xs, zs);
            var coz = p.Comodo("cozinha", 0, xs, z0, x1, zs);
            var qto = p.Comodo("quarto", 0, x0, zs, xs, z1);
            var ban = p.Comodo("banheiro", 0, xs, zs, x1, z1);
            Pintar(sala, Um(r, "ParedeInterna", "PapelVerde"), "Taco");
            Pintar(coz, "Azulejo", "Taco");
            Pintar(qto, "ParedeInterna", "Taco");
            Pintar(ban, "Azulejo", "Azulejo");
            p.xPortaFrente = (x0 + xs) / 2;
            p.Fora(sala, 0, p.xPortaFrente);
            p.Passagem(sala, coz, (z0 + zs) / 2);
            p.Porta(sala, qto, (x0 + xs) / 2);
            p.Porta(coz, ban, (xs + x1) / 2);
            p.cor = Um(r, "MadeiraVelha", "Madeira"); p.escura = "TroncoEscuro";
            p.telhado = Um(r, "TelhadoEscuro", "Telhado", "TelhadoAzulEscuro"); p.telhadoEscuro = "TelhadoEscuro";
            p.corDaPorta = Um(r, "Madeira", "PersianaVerde", "PersianaVinho");
            p.varanda = true;
            p.varandaLarga = r.NextDouble() < 0.6;
            p.fundoDaVaranda = 1.5f;
            p.cumeeiraNoX = r.NextDouble() < 0.5;
            p.chamine = r.NextDouble() < 0.5 ? 1 : -1;
            p.espelhar = r.NextDouble() < 0.5;
            p.tabuasPorDentro = r.NextDouble() < 0.3;   // alguém se trancou lá dentro
            p.janelas = new[] { 2, 2 };
            return new Desenho { Planta = p, Medidas = m, Recuo = 1.8f };
        }

        // ------------------------------------------------------------ o que vai por cima

        /// <summary>O que o kit das casas não faz, por cima da casa montada (nas coordenadas da planta, sem espelhar).</summary>
        public static void Extras(Casa c, string tipo)
        {
            switch (tipo)
            {
                case "igreja": ExtrasDaIgreja(c); break;
                case "escola": ExtrasDaEscola(c); break;
                case "bombeiros": ExtrasDosBombeiros(c); break;
                case "guarda florestal": ExtrasDaGuarda(c); break;
                case "motel": ExtrasDoMotel(c); break;
            }
        }

        static float Chao(Casa c) => c.Base(0);

        // um banco de madeira (o assento, o encosto e as pontas), com a colisão de uma caixa só; 'frente' é para onde quem
        // senta olha (+1: +z)
        static void Banco(Casa c, float xa, float xb, float z, int frente, System.Random r)
        {
            float y = Chao(c), meio = (xa + xb) / 2, comp = xb - xa;
            float torto = r.NextDouble() < 0.15 ? (float)(r.NextDouble() * 16 - 8) : 0f;
            var M = Matrix4x4.TRS(new Vector3(meio, y, z), Quaternion.Euler(0f, torto, 0f), Vector3.one);
            c.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 0.45f, 0f)), new Vector3(comp, 0.06f, 0.44f), "Madeira");
            c.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 0.78f, -frente * 0.2f)), new Vector3(comp, 0.5f, 0.05f), "Madeira");
            c.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 0.22f, 0f)), new Vector3(comp - 0.2f, 0.04f, 0.3f), "MadeiraVelha");
            foreach (float px in new[] { -comp / 2 + 0.03f, comp / 2 - 0.03f })
                c.CaixaM(M * Matrix4x4.Translate(new Vector3(px, 0.5f, -frente * 0.02f)), new Vector3(0.06f, 1.0f, 0.5f), "MadeiraVelha");
            c.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 0.5f, 0f)), new Vector3(comp, 1.0f, 0.5f), "Madeira", colide: true, visual: false);
        }

        static void ExtrasDaIgreja(Casa c)
        {
            var p = c.p;
            var r = new System.Random(p.semente + 11);
            float y = Chao(c), x0 = p.X0, x1 = p.X1, z0 = p.Z0, zFundo = p.Z1 - 4f, e = c.m.esp / 2;
            // os bancos dos dois lados do corredor do meio, de frente para o altar (+z)
            int fileiras = 0;
            for (float z = z0 + 2.4f; z < zFundo - 5.2f; z += 1.15f)
            {
                fileiras++;
                if (r.NextDouble() < 0.08) continue;   // um que levaram (a barricada de alguém)
                Banco(c, x0 + e + 0.35f, -0.85f, z, 1, r);
                Banco(c, 0.85f, x1 - e - 0.35f, z, 1, r);
            }
            // o presbitério: o estrado com o degrau, o altar com a toalha, o púlpito e a cruz na parede do fundo
            float zp0 = zFundo - 4f, zp1 = zFundo - 1.1f;
            c.Caixa(new Vector3(0f, y + 0.15f, (zp0 + zp1) / 2), new Vector3(x1 - x0 - 2 * e - 0.1f, 0.3f, zp1 - zp0), "Madeira", colide: true);
            c.Caixa(new Vector3(0f, y + 0.075f, zp0 - 0.2f), new Vector3(4.5f, 0.15f, 0.4f), "Madeira", colide: true);
            c.Caixa(new Vector3(0f, y + 0.3f + 0.47f, zp1 - 1.2f), new Vector3(2.2f, 0.94f, 0.9f), "MadeiraVelha", colide: true);
            c.Caixa(new Vector3(0f, y + 0.3f + 0.95f, zp1 - 1.2f), new Vector3(2.3f, 0.02f, 1.0f), "PlacaBranca");
            c.Caixa(new Vector3(-0.6f, y + 0.3f + 0.97f, zp1 - 1.2f), new Vector3(0.08f, 0.02f, 0.5f), "PlacaVermelha");
            c.Caixa(new Vector3(-3.6f, y + 0.3f + 0.55f, zp0 + 0.8f), new Vector3(0.9f, 1.1f, 0.7f), "Madeira", colide: true);
            float zc = zFundo - e - 0.05f;
            c.Caixa(new Vector3(0f, y + 3.2f, zc), new Vector3(0.22f, 2.6f, 0.08f), "Madeira");
            c.Caixa(new Vector3(0f, y + 3.8f, zc), new Vector3(1.4f, 0.22f, 0.08f), "Madeira");
            // a torre na frente: os quatro pilares (a entrada passa por baixo), o corpo, o campanário aberto, o telhado em
            // ponta e a cruz
            float tx = 1.9f, tz0 = z0 - 3.6f, tz1 = z0 - e, tm = (tz0 + tz1) / 2;
            foreach (float px in new[] { -tx + 0.25f, tx - 0.25f })
                foreach (float pz in new[] { tz0 + 0.25f, tz1 - 0.25f })
                    c.Caixa(new Vector3(px, 1.75f, pz), new Vector3(0.5f, 3.5f, 0.5f), p.cor, colide: true);
            c.Caixa(new Vector3(0f, 3.5f + 3.25f, tm), new Vector3(2 * tx, 6.5f, tz1 - tz0), p.cor, colide: true);
            c.Caixa(new Vector3(0f, 3.5f + 0.15f, tm), new Vector3(2 * tx + 0.2f, 0.3f, tz1 - tz0 + 0.2f), p.escura);
            float yb = 10f;
            foreach (float px in new[] { -tx + 0.2f, tx - 0.2f })
                foreach (float pz in new[] { tz0 + 0.2f, tz1 - 0.2f })
                    c.Caixa(new Vector3(px, yb + 1.1f, pz), new Vector3(0.4f, 2.2f, 0.4f), p.cor, colide: true);
            c.Caixa(new Vector3(0f, yb + 0.1f, tm), new Vector3(2 * tx, 0.2f, tz1 - tz0), p.escura, colide: true);
            c.Caixa(new Vector3(0f, yb + 2.3f, tm), new Vector3(2 * tx + 0.3f, 0.2f, tz1 - tz0 + 0.3f), p.escura, colide: true);
            // o sino, rachado
            c.Cilindro(new Vector3(0f, yb + 0.9f, tm), 0.45f, 0.25f, 0.8f, 8, "FerrugemEscura");
            float ya = yb + 2.4f, yt = ya + 5.2f, h = tx + 0.15f, hz = (tz1 - tz0) / 2 + 0.15f;
            var A = new List<Vector3> { new Vector3(-h, ya, tm - hz), new Vector3(h, ya, tm - hz), new Vector3(h, ya, tm + hz), new Vector3(-h, ya, tm + hz) };
            var B = new List<Vector3> { new Vector3(-0.05f, yt, tm - 0.05f), new Vector3(0.05f, yt, tm - 0.05f), new Vector3(0.05f, yt, tm + 0.05f), new Vector3(-0.05f, yt, tm + 0.05f) };
            c.Solido(A, B, p.telhado);
            c.Caixa(new Vector3(0f, yt + 0.6f, tm), new Vector3(0.12f, 1.3f, 0.12f), "Metal");
            c.Caixa(new Vector3(0f, yt + 0.85f, tm), new Vector3(0.7f, 0.12f, 0.12f), "Metal");
            // o nome na torre e o quadro dos avisos no gramado
            c.Letreiro("WOODCREEK", new Vector3(0f, 8.4f, tz0 - 0.01f), Vector3.right, Vector3.back, 0.36f, "TijoloEscuro", sai: 0.02f);
            c.Letreiro("COMMUNITY CHURCH", new Vector3(0f, 7.7f, tz0 - 0.01f), Vector3.right, Vector3.back, 0.26f, "TijoloEscuro", sai: 0.02f);
            Quadro(c, new Vector3(x1 - 1.2f, 0f, z0 - 4.5f), "SUNDAY SERVICE 10AM", "GOD HELP US ALL", "Madeira", "PlacaBranca");
        }

        /// <summary>A torre da igreja de longe (o corpo, o campanário, a ponta e a cruz), para a caixa de longe da obra.</summary>
        public static void TorreDeLonge(Planta p, Medidas md, MalhaDeObra m)
        {
            float tx = 1.9f, tz0 = p.Z0 - 3.6f, tz1 = p.Z0 - md.esp / 2, tm = (tz0 + tz1) / 2, yb = 10f;
            m.Caixa(Matrix4x4.Translate(new Vector3(0f, (yb + 2.4f) / 2, tm)), new Vector3(2 * tx, yb + 2.4f, tz1 - tz0), p.cor);
            float ya = yb + 2.4f, yt = ya + 5.2f, h = tx + 0.15f, hz = (tz1 - tz0) / 2 + 0.15f;
            var A = new List<Vector3> { new Vector3(-h, ya, tm - hz), new Vector3(h, ya, tm - hz), new Vector3(h, ya, tm + hz), new Vector3(-h, ya, tm + hz) };
            var B = new List<Vector3> { new Vector3(-0.05f, yt, tm - 0.05f), new Vector3(0.05f, yt, tm - 0.05f), new Vector3(0.05f, yt, tm + 0.05f), new Vector3(-0.05f, yt, tm + 0.05f) };
            m.Solido(A, B, p.telhado);
            m.Caixa(Matrix4x4.Translate(new Vector3(0f, yt + 0.6f, tm)), new Vector3(0.12f, 1.3f, 0.12f), "Metal");
            m.Caixa(Matrix4x4.Translate(new Vector3(0f, yt + 0.85f, tm)), new Vector3(0.7f, 0.12f, 0.12f), "Metal");
        }

        // um quadro de avisos de dois postes no gramado, com duas linhas (a de baixo pichada por cima)
        static void Quadro(Casa c, Vector3 pe, string linha1, string linha2, string madeira, string letras)
        {
            foreach (float dx in new[] { -1.0f, 1.0f })
                c.Caixa(pe + new Vector3(dx, 0.75f, 0f), new Vector3(0.12f, 1.5f, 0.12f), madeira, colide: true);
            c.Caixa(pe + new Vector3(0f, 1.45f, 0f), new Vector3(2.4f, 0.9f, 0.08f), madeira, colide: true);
            c.Caixa(pe + new Vector3(0f, 1.45f, -0.045f), new Vector3(2.2f, 0.75f, 0.01f), "TroncoEscuro");
            c.Letreiro(linha1, pe + new Vector3(0f, 1.55f, -0.05f), Vector3.right, Vector3.back, 0.12f, letras, plano: true, sai: 0.002f);
            c.Letreiro(linha2, pe + new Vector3(0f, 1.25f, -0.05f), Vector3.right, Vector3.back, 0.14f, "PlacaVermelha", plano: true, sai: 0.002f);
        }

        static void ExtrasDaEscola(Casa c)
        {
            var p = c.p;
            var r = new System.Random(p.semente + 23);
            float y = Chao(c), e = c.m.esp / 2;
            foreach (var k in p.comodos)
            {
                if (k.tipo != "aula") continue;
                bool naFrente = k.z1 <= 0f;   // a porta da sala fica do lado do corredor
                // o quadro-negro numa parede do lado que é de dentro (a de fora tem as janelas): a de x0, ou a de x1 na
                // sala da ponta de x0; os alunos olham para ele (s = -1: para -x)
                bool noX1 = Mathf.Abs(k.x0 - p.X0) < 0.01f;
                float s = noX1 ? 1f : -1f, xq = noX1 ? k.x1 - e : k.x0 + e;
                float cz = (k.z0 + k.z1) / 2;
                c.Caixa(new Vector3(xq - s * 0.04f, y + 1.55f, cz), new Vector3(0.06f, 1.3f, 3.4f), "Madeira");
                c.Caixa(new Vector3(xq - s * 0.075f, y + 1.55f, cz), new Vector3(0.02f, 1.18f, 3.28f), "PlacaVerde");
                c.Caixa(new Vector3(xq - s * 0.12f, y + 0.92f, cz), new Vector3(0.08f, 0.04f, 3.0f), "Madeira");
                c.Caixa(new Vector3(xq - s * 1.3f, y + 0.39f, cz + (naFrente ? -1.2f : 1.2f)), new Vector3(0.8f, 0.78f, 1.4f), "MadeiraVelha", colide: true);
                // as carteiras em fileiras, longe da porta (algumas caídas)
                float xPerto = xq - s * 2.6f, xLonge = noX1 ? k.x0 + 0.9f : k.x1 - 0.9f;
                float xIni = Mathf.Min(xPerto, xLonge), xFim = Mathf.Max(xPerto, xLonge);
                float zIni = naFrente ? k.z0 + 1.1f : k.z0 + 2.2f, zFim = naFrente ? k.z1 - 2.2f : k.z1 - 1.1f;
                for (float x = xIni; x <= xFim; x += 1.35f)
                    for (float z = zIni; z <= zFim; z += 1.3f)
                    {
                        if (r.NextDouble() < 0.1) continue;
                        bool caida = r.NextDouble() < 0.12;
                        float giro = (noX1 ? 180f : 0f) + (caida ? 90f : (float)(r.NextDouble() * 14 - 7));
                        Carteira(c, new Vector3(x, y, z), giro, caida);
                    }
            }
            // o nome em cima da entrada, o mastro com a bandeira rasgada e o bicicletário
            float z0 = p.Z0;
            c.Letreiro("WOODCREEK SCHOOL", new Vector3(0f, y + c.m.altura - 0.75f, z0 - e - 0.01f), Vector3.right, Vector3.back, 0.42f, "PlacaBranca", sai: 0.04f, grossura: 0.03f);
            c.Tubo(new Vector3(-7f, 0f, z0 - 4.5f), new Vector3(-7f, 9f, z0 - 4.5f), 0.06f, "Metal");
            c.Caixa(new Vector3(-7f, 0.25f, z0 - 4.5f), new Vector3(0.6f, 0.5f, 0.6f), "Concreto", colide: true);
            c.Caixa(new Vector3(-6.4f, 8.4f, z0 - 4.5f), new Vector3(1.1f, 0.65f, 0.02f), "PlacaVermelha");
            c.Caixa(new Vector3(-6.75f, 8.55f, z0 - 4.5f), new Vector3(0.45f, 0.35f, 0.025f), "ParedeAzulEscura");
            for (int i = 0; i < 6; i++)
                c.Tubo(new Vector3(5f + i * 0.6f, 0.05f, z0 - 3f), new Vector3(5f + i * 0.6f, 0.8f, z0 - 3f), 0.025f, "Ferrugem");
            c.Caixa(new Vector3(6.5f, 0.8f, z0 - 3f), new Vector3(3.6f, 0.05f, 0.05f), "Ferrugem");
        }

        // a carteira de aluno: a mesa (o tampo e o pé de ferro) e a cadeira atrás dela, para +x (o aluno olha para -x)
        static void Carteira(Casa c, Vector3 pe, float giro, bool caida)
        {
            var M = Matrix4x4.TRS(pe, Quaternion.Euler(0f, giro, caida ? 80f : 0f), Vector3.one);
            if (caida) M = Matrix4x4.TRS(pe + Vector3.up * 0.22f, Quaternion.Euler(0f, giro, 82f), Vector3.one);
            c.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 0.72f, 0f)), new Vector3(0.48f, 0.04f, 0.62f), "Madeira");
            foreach (float pz in new[] { -0.26f, 0.26f })
                c.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 0.35f, pz)), new Vector3(0.4f, 0.7f, 0.04f), "Metal");
            c.CaixaM(M * Matrix4x4.Translate(new Vector3(0f, 0.12f, 0f)), new Vector3(0.04f, 0.04f, 0.5f), "Metal");
            c.CaixaM(M * Matrix4x4.Translate(new Vector3(0.5f, 0.44f, 0f)), new Vector3(0.4f, 0.04f, 0.4f), "ParedeAzul");
            c.CaixaM(M * Matrix4x4.Translate(new Vector3(0.7f, 0.68f, 0f)), new Vector3(0.04f, 0.42f, 0.4f), "ParedeAzul");
            foreach (float pz in new[] { -0.17f, 0.17f })
                c.CaixaM(M * Matrix4x4.Translate(new Vector3(0.5f, 0.21f, pz)), new Vector3(0.36f, 0.42f, 0.03f), "Metal");
            if (!caida) c.CaixaM(M * Matrix4x4.Translate(new Vector3(0.25f, 0.4f, 0f)), new Vector3(0.95f, 0.8f, 0.62f), "Metal", colide: true, visual: false);
            else c.Caixa(pe + new Vector3(0.1f, 0.25f, 0f), new Vector3(0.8f, 0.5f, 0.7f), "Metal", giro, colide: true, visual: false);
        }

        static void ExtrasDosBombeiros(Casa c)
        {
            var p = c.p;
            var r = new System.Random(p.semente + 31);
            float y = Chao(c), e = c.m.esp / 2, x0 = p.X0, z0 = p.Z0, z1 = p.Z1, xg = 2.5f;
            // os armários do pessoal na parede do fundo do galpão, com os capacetes em cima
            for (int i = 0; i < 7; i++)
            {
                float x = x0 + 1.0f + i * 0.62f;
                if (x > xg - 1.2f) break;
                c.Caixa(new Vector3(x, y + 0.95f, z1 - e - 0.3f), new Vector3(0.58f, 1.9f, 0.55f), "Metal", colide: true);
                c.Caixa(new Vector3(x, y + 1.5f, z1 - e - 0.585f), new Vector3(0.5f, 0.6f, 0.02f), "Grade");
                if (r.NextDouble() < 0.6) c.Cilindro(new Vector3(x, y + 1.9f, z1 - e - 0.3f), 0.16f, 0.12f, 0.14f, 8, r.NextDouble() < 0.5 ? "PlacaAmarela" : "PlacaVermelha");
            }
            // o enrolador de mangueira e as mangueiras penduradas na parede de x0
            c.Cilindro(new Vector3(x0 + e + 0.5f, y, -2f), 0.45f, 0.45f, 0.9f, 10, "PlacaVermelha");
            c.Caixa(new Vector3(x0 + e + 0.5f, y + 0.45f, -2f), new Vector3(0.9f, 0.9f, 0.9f), "PlacaVermelha", colide: true, visual: false);
            for (int i = 0; i < 4; i++)
                c.Caixa(new Vector3(x0 + e + 0.08f, y + 1.7f, 1.5f + i * 0.5f), new Vector3(0.12f, 1.4f, 0.3f), "Saco");
            // as faixas amarelas no chão das vagas e o nome em cima dos portões
            foreach (float xv in new[] { x0 + 2.75f, x0 + 8.25f })
                foreach (float dx in new[] { -2.3f, 2.3f })
                    c.Caixa(new Vector3(xv + dx, y + 0.004f, (z0 + z1) / 2), new Vector3(0.12f, 0.006f, p.D - 1.5f), "Faixa");
            c.Letreiro("WOODCREEK FIRE DEPT", new Vector3(x0 + 5.5f, y + c.m.altura - 0.4f, z0 - e - 0.01f), Vector3.right, Vector3.back, 0.3f, "PlacaBranca", sai: 0.03f, grossura: 0.02f);
            c.Letreiro("STATION 1", new Vector3((xg + p.X1) / 2, y + 2.95f, z0 - e - 0.01f), Vector3.right, Vector3.back, 0.22f, "PlacaBranca", sai: 0.03f, grossura: 0.02f);
            // a sirene no telhado (no mastro da frente)
            c.Tubo(new Vector3(p.X1 - 1f, y + c.m.altura, z0 + 1f), new Vector3(p.X1 - 1f, y + c.m.altura + 3f, z0 + 1f), 0.05f, "Metal");
            c.Cilindro(new Vector3(p.X1 - 1f, y + c.m.altura + 3f, z0 + 1f), 0.25f, 0.3f, 0.35f, 8, "Ferrugem");
        }

        static void ExtrasDaGuarda(Casa c)
        {
            var p = c.p;
            float z0 = p.Z0, z1 = p.Z1;
            // a placa de madeira na entrada, à moda das florestas americanas (as letras amarelas no marrom)
            var pe = new Vector3(p.X0 - 1.2f, 0f, z0 - 4.2f);
            foreach (float dx in new[] { -1.3f, 1.3f })
                c.Caixa(pe + new Vector3(dx, 0.9f, 0f), new Vector3(0.22f, 1.8f, 0.22f), "TroncoEscuro", colide: true);
            c.Caixa(pe + new Vector3(0f, 1.45f, 0f), new Vector3(3.1f, 1.0f, 0.12f), "MadeiraVelha", colide: true);
            c.Letreiro("WOODCREEK", pe + new Vector3(0f, 1.62f, -0.065f), Vector3.right, Vector3.back, 0.22f, "PlacaAmarela", plano: true, sai: 0.002f);
            c.Letreiro("RANGER STATION", pe + new Vector3(0f, 1.22f, -0.065f), Vector3.right, Vector3.back, 0.2f, "PlacaAmarela", plano: true, sai: 0.002f);
            // a antena do rádio atrás, com os estais
            var b = new Vector3(p.X0 + 2f, 0f, z1 + 2.5f);
            c.Tubo(b, b + Vector3.up * 15f, 0.08f, "Metal");
            c.Caixa(b + Vector3.up * 0.3f, new Vector3(0.7f, 0.6f, 0.7f), "Concreto", colide: true);
            for (int k = 0; k < 5; k++) c.Caixa(b + Vector3.up * (2.5f + k * 2.6f), new Vector3(0.5f, 0.05f, 0.05f), "Metal");
            foreach (var d in new[] { new Vector3(4f, 0f, 2f), new Vector3(-4f, 0f, 2f), new Vector3(0f, 0f, -4f) })
                c.Tubo(b + Vector3.up * 11f, b + d + Vector3.up * 0.05f, 0.012f, "Metal");
            c.Caixa(b + Vector3.up * 15.1f, new Vector3(0.9f, 0.08f, 0.08f), "Metal");
        }

        static void ExtrasDoMotel(Casa c)
        {
            var p = c.p;
            float y = Chao(c), x0 = p.X0, x1 = p.X1, z0 = p.Z0, e = c.m.esp / 2;
            // o estacionamento de asfalto na frente da varanda, com as vagas de cada quarto
            float zv = z0 - p.fundoDaVaranda - 0.1f, za = zv - 8f;
            c.Laje(x0 - 1f, x1 + 1f, za, zv, 0.03f, 0.2f, "Asfalto");
            for (int i = 0; i <= QuartosDoMotel; i++)
            {
                float x = x0 + LarguraDaRecepcao + i * LarguraDoQuarto;
                c.Caixa(new Vector3(x, 0.035f, zv - 2.8f), new Vector3(0.12f, 0.01f, 5.2f), "ListraBranca");
            }
            // os números dos quartos em cima das portas
            for (int i = 0; i < QuartosDoMotel; i++)
            {
                float xp = x0 + LarguraDaRecepcao + i * LarguraDoQuarto + 0.85f;
                c.Letreiro((i + 1).ToString(), new Vector3(xp, y + c.m.alturaPortaFrente + 0.12f, z0 - e - 0.01f), Vector3.right, Vector3.back, 0.2f, "Cromo", sai: 0.02f);
            }
            c.Letreiro("OFFICE", new Vector3(p.xPortaFrente, y + c.m.alturaPortaFrente + 0.15f, z0 - e - 0.01f), Vector3.right, Vector3.back, 0.18f, "PlacaVermelha", sai: 0.02f);
            // a placa alta na beira da estrada: o poste e a caixa com o MOTEL dos dois lados e o "NO VACANCY" embaixo
            var pe = new Vector3(x1 + 2.2f, 0f, za + 1.2f);
            c.Tubo(pe, pe + Vector3.up * 6.4f, 0.12f, "Ferrugem");
            c.Caixa(pe + Vector3.up * 0.3f, new Vector3(0.6f, 0.6f, 0.6f), "Concreto", colide: true);
            c.Caixa(pe + Vector3.up * 6.2f, new Vector3(3.4f, 1.3f, 0.35f), "PlacaBranca");
            c.Caixa(pe + Vector3.up * 5.05f, new Vector3(2.4f, 0.55f, 0.25f), "PlacaBranca");
            c.Letreiro("MOTEL", pe + new Vector3(0f, 5.85f, -0.18f), Vector3.right, Vector3.back, 0.7f, "PlacaVermelha", plano: true, sai: 0.003f);
            c.Letreiro("MOTEL", pe + new Vector3(0f, 5.85f, 0.18f), Vector3.left, Vector3.forward, 0.7f, "PlacaVermelha", plano: true, sai: 0.003f);
            c.Letreiro("NO VACANCY", pe + new Vector3(0f, 4.93f, -0.13f), Vector3.right, Vector3.back, 0.24f, "PlacaVermelha", plano: true, sai: 0.003f);
            c.Letreiro("NO VACANCY", pe + new Vector3(0f, 4.93f, 0.13f), Vector3.left, Vector3.forward, 0.24f, "PlacaVermelha", plano: true, sai: 0.003f);
            // a máquina de gelo na varanda, ao lado da recepção
            c.Caixa(new Vector3(x0 + LarguraDaRecepcao + 0.3f, y + 0.75f, z0 - 0.6f), new Vector3(0.8f, 1.5f, 0.7f), "CarroBranco", colide: true);
            c.Letreiro("ICE", new Vector3(x0 + LarguraDaRecepcao + 0.3f, y + 1.2f, z0 - 0.96f), Vector3.right, Vector3.back, 0.16f, "CarroAzul", plano: true, sai: 0.002f);
        }
    }
}
