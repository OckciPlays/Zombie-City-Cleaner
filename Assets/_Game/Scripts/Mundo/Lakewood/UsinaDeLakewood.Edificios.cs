using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    public sealed partial class UsinaDeLakewood
    {
        // ------------------------------------------------------------------ os lados de um retângulo (u, v) na moldura

        enum Lado { Montante, Jusante, Oeste, Leste }

        static Vector3 ForaDe(Lado l) => l switch
        {
            Lado.Montante => Vector3.forward,
            Lado.Jusante => Vector3.back,
            Lado.Oeste => Vector3.left,
            _ => Vector3.right,
        };

        /// <summary>A direita de quem olha para o lado (para ler um letreiro nele).</summary>
        static Vector3 DireitaDe(Lado l) => Vector3.Cross(Vector3.up, -ForaDe(l));

        // um ponto no lado do retângulo: s ao longo dele (o u nos lados de montante e de jusante, o v nos outros), a altura
        // e o quanto sai da parede
        static Vector3 NoLado(Rect r, Lado l, float s, float y, float sai) => l switch
        {
            Lado.Montante => L(s, y, r.yMin - sai),
            Lado.Jusante => L(s, y, r.yMax + sai),
            Lado.Oeste => L(r.xMin - sai, y, s),
            _ => L(r.xMax + sai, y, s),
        };

        static void Placa(Desenho d, Rect r, Lado l, float s0, float s1, float y0, float y1, float sai, Cor cor) =>
            d.Quad(NoLado(r, l, s0, y0, sai), NoLado(r, l, s1, y0, sai), NoLado(r, l, s1, y1, sai), NoLado(r, l, s0, y1, sai), ForaDe(l), cor);

        // uma janela: a moldura de metal e os vidros (sujos; uns quebrados, com o escuro de dentro; uns tapados com o compensado)
        static void Janela(Desenho d, Rect r, Lado l, float s0, float s1, float y0, float y1, int colunas, int linhas, System.Random sorte, float sai = 0.03f)
        {
            Placa(d, r, l, s0, s1, y0, y1, sai, "Metal");
            float ds = (s1 - s0) / colunas, dy = (y1 - y0) / linhas;
            bool tapada = sorte.NextDouble() < 0.12;
            for (int j = 0; j < linhas; j++)
            for (int i = 0; i < colunas; i++)
            {
                double x = sorte.NextDouble();
                string cor = tapada ? "Compensado" : x < 0.64 ? "VidroSujo" : x < 0.9 ? "Interior" : "Vidro";
                Placa(d, r, l, s0 + i * ds + 0.06f, s0 + (i + 1) * ds - 0.06f, y0 + j * dy + 0.06f, y0 + (j + 1) * dy - 0.06f, sai + 0.012f, cor);
            }
        }

        // uma porta de enrolar (as ripas), fechada ou com a parte de baixo aberta (o escuro de dentro)
        static void PortaDeEnrolar(Desenho d, Rect r, Lado l, float s0, float s1, float y0, float altura, float aberta, string cor = "Metal")
        {
            Placa(d, r, l, s0 - 0.25f, s1 + 0.25f, y0, y0 + altura + 0.3f, 0.02f, "Concreto");
            float baixo = y0 + aberta;
            if (aberta > 0f) Placa(d, r, l, s0, s1, y0, baixo, 0.035f, "Interior");
            for (float y = baixo; y < y0 + altura - 0.01f; y += 0.45f)
                Placa(d, r, l, s0, s1, y, Mathf.Min(y + 0.4f, y0 + altura), 0.04f, ((int)(y * 7f)) % 5 == 0 ? "Ferrugem" : cor);
            Placa(d, r, l, s0 - 0.1f, s1 + 0.1f, y0 + altura, y0 + altura + 0.35f, 0.12f, "Metal");
        }

        // uma porta de abrir (fechada) com a moldura
        static void Porta(Desenho d, Rect r, Lado l, float s, float y0, float largura = 1.1f, float altura = 2.3f, string cor = "Ferrugem")
        {
            Placa(d, r, l, s - largura * 0.5f - 0.1f, s + largura * 0.5f + 0.1f, y0, y0 + altura + 0.1f, 0.02f, "Batente");
            Placa(d, r, l, s - largura * 0.5f, s + largura * 0.5f, y0, y0 + altura, 0.035f, cor);
        }

        // um prédio de caixa (as paredes e a laje de cima com a platibanda), com a cor das paredes e do topo
        static void Caixote(Desenho d, Rect r, float y0, float y1, string parede, string topo = "Concreto", float platibanda = 0.8f, bool colide = true)
        {
            d.Caixa(L(r.center.x, (y0 + y1) * 0.5f, r.center.y), new Vector3(r.width, y1 - y0, r.height), Cor.PorNormal(topo, parede), colide);
            if (platibanda <= 0f) return;
            const float e = 0.25f;
            d.Caixa(L(r.center.x, y1 + platibanda * 0.5f, r.yMin + e * 0.5f), new Vector3(r.width, platibanda, e), parede);
            d.Caixa(L(r.center.x, y1 + platibanda * 0.5f, r.yMax - e * 0.5f), new Vector3(r.width, platibanda, e), parede);
            d.Caixa(L(r.xMin + e * 0.5f, y1 + platibanda * 0.5f, r.center.y), new Vector3(e, platibanda, r.height - 2f * e), parede);
            d.Caixa(L(r.xMax - e * 0.5f, y1 + platibanda * 0.5f, r.center.y), new Vector3(e, platibanda, r.height - 2f * e), parede);
        }

        // o galpão de duas águas (a cumeeira ao longo do v): as paredes, as empenas, o telhado com uns buracos e a barra de baixo
        static void DuasAguas(Desenho d, Rect r, float piso, float beiral, float cumeeira, string parede, string telhado, System.Random sorte, float buracos)
        {
            float xm = r.center.x;
            d.Caixa(L(r.center.x, piso + beiral * 0.5f, r.center.y), new Vector3(r.width, beiral, r.height), Cor.PorNormal("Concreto", parede), true);
            d.Solido(new[] { L(r.xMin, piso + beiral, r.yMin), L(r.xMax, piso + beiral, r.yMin), L(xm, piso + cumeeira, r.yMin) },
                     new[] { L(r.xMin, piso + beiral, r.yMax), L(r.xMax, piso + beiral, r.yMax), L(xm, piso + cumeeira, r.yMax) }, parede, true);
            // o telhado em faixas (as que faltam mostram o escuro de dentro)
            const int faixas = 8;
            float largura = (r.height + 1f) / faixas;
            float k1 = (cumeeira - beiral) / (r.width * 0.5f);
            for (int k = 0; k < faixas; k++)
            {
                float vm = r.yMin - 0.5f + (k + 0.5f) * largura;
                foreach (float s in new[] { -1f, 1f })
                {
                    var de = L(xm + s * (r.width * 0.5f + 0.6f), piso + beiral - 0.6f * k1 + 0.16f, vm);
                    var ate = L(xm, piso + cumeeira + 0.16f, vm);
                    if (sorte.NextDouble() < buracos)
                    {
                        // o buraco: o escuro de dentro na face da empena, embaixo da faixa que caiu
                        var f0 = L(xm + s * r.width * 0.5f, piso + beiral, vm);
                        var f1 = L(xm, piso + cumeeira, vm);
                        var lado = new Vector3(0f, 0f, largura * 0.45f);
                        var ao = Vector3.Lerp(f0, f1, 0.1f) + Vector3.up * 0.03f;
                        var bo = Vector3.Lerp(f0, f1, 0.85f) + Vector3.up * 0.03f;
                        d.Quad(ao - lado, ao + lado, bo + lado, bo - lado, Vector3.up, "Interior");
                        continue;
                    }
                    d.Entre(de, ate, largura - 0.04f, 0.14f, (k + (s > 0f ? 3 : 0)) % 8 == 5 ? "FerrugemEscura" : telhado);
                }
            }
            d.Caixa(L(xm, piso + cumeeira + 0.2f, r.center.y), new Vector3(0.5f, 0.2f, r.height + 1f), "Metal");
            // a barra de concreto de baixo
            foreach (Lado l in new[] { Lado.Montante, Lado.Jusante })
                Placa(d, r, l, r.xMin, r.xMax, piso, piso + 1.2f, 0.02f, "Concreto");
            foreach (Lado l in new[] { Lado.Oeste, Lado.Leste })
                Placa(d, r, l, r.yMin, r.yMax, piso, piso + 1.2f, 0.02f, "Concreto");
        }

        // ------------------------------------------------------------------ os prédios

        void MontarOsEdificios(List<PecaDaUsina> saida, System.Random sorte, Func<float, float, float> H)
        {
            CasaDeForcaDaUsina(saida, sorte);
            AdministracaoDaUsina(saida, sorte);
            EstacionamentoDaUsina(saida, sorte);
            GaleriaDeServico(saida, sorte);
            OficinaEGalpoes(saida, sorte);
            TanqueDeDiesel(saida, sorte);
            Guaritas(saida, sorte);
            Alambrado(saida, sorte, H);
            PostoDeControle(saida, sorte);
            Carros(saida, H);
        }

        // a casa de força: a parte de baixo de concreto (do fundo do poço), as paredes com os pilares e os janelões, o
        // letreiro, as bocas dos tubos de sucção embaixo d'água, as portas da ponta leste e a laje de cima
        void CasaDeForcaDaUsina(List<PecaDaUsina> saida, System.Random sorte)
        {
            var r = CasaDeForca;
            float pe = AlturaDoPe, baixo = FundoDaFuga, meio = pe + 5f, topo = pe + 23.5f;
            var d = new Desenho(L(r.center.x, pe, r.center.y));
            d.Caixa(L(r.center.x, (baixo + meio) * 0.5f, r.center.y), new Vector3(r.width, meio - baixo, r.height), "Concreto", true);
            d.Caixa(L(r.center.x, (meio + topo) * 0.5f, r.center.y), new Vector3(r.width, topo - meio, r.height), Cor.PorNormal("Concreto", "ParedeAzulEscura"), true);
            // a laje e a platibanda
            d.Caixa(L(r.center.x, topo + 0.3f, r.center.y - 0.5f), new Vector3(r.width + 1f, 0.6f, r.height + 1f), "Concreto");
            d.Caixa(L(r.center.x, topo + 1.05f, r.yMax + 0.35f), new Vector3(r.width + 1f, 0.9f, 0.3f), "ConcretoClaro", true);
            foreach (float u in new[] { r.xMin - 0.35f, r.xMax + 0.35f })
                d.Caixa(L(u, topo + 1.05f, r.center.y), new Vector3(0.3f, 0.9f, r.height + 1f), "ConcretoClaro", true);
            // a fachada do canal de fuga: os pilares, a viga do letreiro e os janelões entre os pilares
            const int vaos = 12;
            float largura = r.width / vaos;
            for (int k = 0; k <= vaos; k++)
                d.Caixa(L(r.xMin + k * largura, (meio + topo) * 0.5f, r.yMax + 0.5f), new Vector3(1.2f, topo - meio, 1f), "ConcretoClaro", true);
            d.Caixa(L(r.center.x, topo - 1.5f, r.yMax + 0.55f), new Vector3(r.width + 1.2f, 3f, 1.1f), "ConcretoClaro");
            var fachada = Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, r.yMax);
            for (int k = 0; k < vaos; k++)
            {
                float s0 = r.xMin + k * largura + 1.6f, s1 = r.xMin + (k + 1) * largura - 1.6f;
                Janela(d, fachada, Lado.Jusante, s0, s1, meio + 2.5f, topo - 4.2f, 4, 4, sorte);
                // o escorrido da laje entre as janelas
                if (sorte.NextDouble() < 0.6)
                {
                    float s = r.xMin + k * largura + 0.9f + (float)sorte.NextDouble() * 0.2f;
                    Placa(d, fachada, Lado.Jusante, s - 0.25f, s + 0.25f, meio + 0.5f + (float)sorte.NextDouble() * 6f, topo - 3f, 0.05f, "Mofo");
                }
            }
            d.Letreiro("LAKEWOOD HYDROELECTRIC PLANT", L(r.center.x, topo - 2.75f, r.yMax + 1.1f), Vector3.right, Vector3.up, Vector3.back, 1.7f, "Batente", 0.03f, 25);
            // as bocas dos tubos de sucção, embaixo d'água, e o limo na linha d'água
            for (int k = 0; k < 4; k++)
            {
                float uc = 613f + k * 35f;
                Placa(d, fachada, Lado.Jusante, uc - 4.5f, uc + 4.5f, baixo + 0.6f, NivelDaFuga - 0.6f, 0.03f, "Interior");
            }
            Placa(d, fachada, Lado.Jusante, r.xMin, r.xMax, NivelDaFuga - 0.5f, NivelDaFuga + 0.9f, 0.035f, "Musgo");
            // a ponta leste: a porta de enrolar dos equipamentos, a porta de gente e as janelas altas
            PortaDeEnrolar(d, r, Lado.Leste, 38f, 46f, pe, 8f, 0f);
            Porta(d, r, Lado.Leste, 60.5f, pe);
            d.Caixa(L(r.xMax + 0.8f, pe + 2.55f, 60.5f), new Vector3(1.6f, 0.15f, 2f), "Concreto");
            for (int k = 0; k < 6; k++) Janela(d, r, Lado.Leste, 27f + k * 8f, 32f + k * 8f, pe + 13f, pe + 18f, 2, 2, sorte);
            // a ponta oeste: uma porta e as janelas altas
            Porta(d, r, Lado.Oeste, 70f, pe);
            for (int k = 0; k < 5; k++) Janela(d, r, Lado.Oeste, 28f + k * 9f, 33f + k * 9f, pe + 13f, pe + 18f, 2, 2, sorte);
            // a plataforma de concreto na frente da porta de enrolar
            d.Caixa(L(r.xMax + 6f, pe + 0.05f, 42f), new Vector3(12f, 0.3f, 12f), "Concreto", true);
            // em cima: os ventiladores, a claraboia e o musgo
            for (int k = 0; k < 4; k++)
            {
                var p = L(r.xMin + 20f + k * 33f, topo + 0.6f, 40f);
                d.Caixa(p + Vector3.up * 0.6f, new Vector3(1.8f, 1.2f, 1.8f), "Metal");
                d.Cilindro(p + Vector3.up * 1.2f, 0.7f, 0.5f, 0.5f, 8, "Ferrugem");
            }
            for (int k = 0; k < 10; k++)
            {
                float u = r.xMin + 8f + k * 12.5f;
                d.Quad(L(u, topo + 0.62f, 55f), L(u + 9f, topo + 0.62f, 55f), L(u + 9f, topo + 0.62f, 59f), L(u, topo + 0.62f, 59f), Vector3.up, k % 4 == 1 ? "Interior" : "VidroSujo");
            }
            for (int k = 0; k < 9; k++)
            {
                float u = r.xMin + (float)sorte.NextDouble() * r.width, v = r.yMin + 4f + (float)sorte.NextDouble() * (r.height - 8f);
                float a = 2f + (float)sorte.NextDouble() * 5f, b = 1.5f + (float)sorte.NextDouble() * 3f;
                d.Quad(L(u - a, topo + 0.615f, v - b), L(u + a, topo + 0.615f, v - b), L(u + a, topo + 0.615f, v + b), L(u - a, topo + 0.615f, v + b), Vector3.up, k % 2 == 0 ? "Musgo" : "Terra");
            }
            Hera(d, L(r.xMax + 0.02f, pe, r.yMax - 4f), Vector3.right, Vector3.forward, 7f, 15f, 5);
            Hera(d, L(r.xMin + 18f, pe + 6f, r.yMax + 1.02f), Vector3.back, Vector3.right, 5f, 9f, 6);
            saida.Add(Peca("Casa de força", d));
        }

        // a administração: dois andares, as janelas em fita, a entrada com a marquise na frente (para a estrada), o letreiro,
        // a laje com os aparelhos de ar e o mastro
        void AdministracaoDaUsina(List<PecaDaUsina> saida, System.Random sorte)
        {
            var r = Administracao;
            float piso = AlturaDoPatio("Administração");
            var d = new Desenho(L(r.center.x, piso, r.center.y));
            const float andar = 3.7f;
            Caixote(d, r, piso, piso + 2f * andar, "ParedeBege");
            d.Caixa(L(r.center.x, piso + 0.25f, r.center.y), new Vector3(r.width + 0.2f, 0.5f, r.height + 0.2f), "Concreto");
            foreach (Lado l in new[] { Lado.Oeste, Lado.Leste })
            {
                Placa(d, r, l, r.yMin, r.yMax, piso + andar - 0.15f, piso + andar + 0.15f, 0.04f, "ParedeBegeEscura");
                for (int a = 0; a < 2; a++)
                    for (float s = r.yMin + 1.4f; s < r.yMax - 2f; s += 3.6f)
                    {
                        if (l == Lado.Leste && a == 0 && s > 288f && s < 296f) continue;
                        Janela(d, r, l, s, s + 2.4f, piso + 1f + a * andar, piso + 2.6f + a * andar, 2, 1, sorte);
                    }
            }
            foreach (Lado l in new[] { Lado.Montante, Lado.Jusante })
            {
                Placa(d, r, l, r.xMin, r.xMax, piso + andar - 0.15f, piso + andar + 0.15f, 0.04f, "ParedeBegeEscura");
                for (int a = 0; a < 2; a++)
                    for (float s = r.xMin + 2f; s < r.xMax - 3f; s += 4.4f)
                        Janela(d, r, l, s, s + 2.4f, piso + 1f + a * andar, piso + 2.6f + a * andar, 2, 1, sorte);
            }
            // a entrada: as portas de vidro (quebradas), a marquise com os dois pilares, os degraus e o letreiro
            Placa(d, r, Lado.Leste, 290.4f, 293.6f, piso + 0.5f, piso + 3f, 0.03f, "Metal");
            Placa(d, r, Lado.Leste, 290.55f, 291.95f, piso + 0.5f, piso + 2.9f, 0.045f, "Interior");
            Placa(d, r, Lado.Leste, 292.05f, 293.45f, piso + 0.5f, piso + 2.9f, 0.045f, "Compensado");
            d.Caixa(L(r.xMax + 1.6f, piso + 3.35f, 292f), new Vector3(3.4f, 0.3f, 7f), "Concreto", true);
            foreach (float v in new[] { 289f, 295f })
                d.Caixa(L(r.xMax + 3f, piso + 1.6f, v), new Vector3(0.3f, 3.2f, 0.3f), "ConcretoClaro", true);
            d.Caixa(L(r.xMax + 1.1f, piso + 0.15f, 292f), new Vector3(2.2f, 0.3f, 5f), "Concreto", true);
            d.Letreiro("ADMINISTRATION", L(r.xMax + 3.32f, piso + 3.22f, 292f), DireitaDe(Lado.Leste), Vector3.up, Vector3.right, 0.22f, "Batente", 0.0f);
            // a laje: os aparelhos de ar, a antena, o alçapão e o musgo
            for (int k = 0; k < 3; k++)
            {
                var p = L(r.center.x - 3f + k * 3f, piso + 2f * andar, r.yMin + 10f + k * 12f);
                d.Caixa(p + Vector3.up * 0.55f, new Vector3(1.4f, 1.1f, 1.8f), "Metal");
                d.Caixa(p + new Vector3(0f, 1.12f, 0f), new Vector3(1f, 0.04f, 1f), "Grade");
            }
            d.Cilindro(L(r.xMin + 3f, piso + 2f * andar, r.yMax - 4f), 0.05f, 0.03f, 5f, 5, "Metal");
            d.Caixa(L(r.xMin + 5f, piso + 2f * andar + 0.2f, r.yMin + 5f), new Vector3(1.2f, 0.4f, 1.2f), "Ferrugem");
            for (int k = 0; k < 4; k++)
            {
                float u = r.xMin + 2f + (float)sorte.NextDouble() * (r.width - 4f), v = r.yMin + 3f + (float)sorte.NextDouble() * (r.height - 6f);
                d.Quad(L(u - 2f, piso + 2f * andar + 0.01f, v - 1.5f), L(u + 2f, piso + 2f * andar + 0.01f, v - 1.5f), L(u + 2f, piso + 2f * andar + 0.01f, v + 1.5f), L(u - 2f, piso + 2f * andar + 0.01f, v + 1.5f), Vector3.up, "Musgo");
            }
            // o mastro sem a bandeira, com a corda
            d.Caixa(L(r.xMax + 9f, piso + 0.2f, 292f), new Vector3(1.2f, 0.4f, 1.2f), "Concreto", true);
            d.Cilindro(L(r.xMax + 9f, piso + 0.4f, 292f), 0.09f, 0.05f, 9f, 6, "Cromo");
            d.Entre(L(r.xMax + 9.12f, piso + 9.3f, 292f), L(r.xMax + 9.12f, piso + 1.4f, 292.05f), 0.02f, 0.02f, "Grade");
            Hera(d, L(r.center.x, piso, r.yMax + 0.02f), Vector3.back, Vector3.right, 9f, 7.6f, 9);
            saida.Add(Peca("Administração", d));
        }

        // o estacionamento: o asfalto com os remendos, as vagas pintadas e os dois postes apagados (os carros vão à parte)
        void EstacionamentoDaUsina(List<PecaDaUsina> saida, System.Random sorte)
        {
            var r = Estacionamento;
            float piso = AlturaDoPatio("Estacionamento");
            var d = new Desenho(L(r.center.x, piso, r.center.y));
            d.Caixa(L(r.center.x, piso + 0.04f, r.center.y), new Vector3(r.width, 0.12f, r.height), Cor.PorNormal("Asfalto", "MeioFio"), true);
            float y = piso + 0.102f;
            foreach (float v0 in new[] { r.yMin + 0.5f, r.yMax - 6.5f })
                for (float u = r.xMin + 2f; u <= r.xMax - 1.5f; u += 2.8f)
                    d.Quad(L(u - 0.06f, y, v0), L(u + 0.06f, y, v0), L(u + 0.06f, y, v0 + 6f), L(u - 0.06f, y, v0 + 6f), Vector3.up, "ListraBranca");
            for (int k = 0; k < 14; k++)
            {
                float u = r.xMin + 2f + (float)sorte.NextDouble() * (r.width - 4f), v = r.yMin + 2f + (float)sorte.NextDouble() * (r.height - 4f);
                float a = 0.6f + (float)sorte.NextDouble() * 2.4f, b = 0.5f + (float)sorte.NextDouble() * 1.8f;
                d.Quad(L(u - a, y - 0.002f, v - b), L(u + a, y - 0.002f, v - b), L(u + a, y - 0.002f, v + b), L(u - a, y - 0.002f, v + b), Vector3.up, k % 4 == 0 ? "Oleo" : k % 4 == 1 ? "Terra" : "AsfaltoRemendo");
            }
            foreach (float u in new[] { r.xMin + 14f, r.xMax - 14f })
            {
                var pe = L(u, piso + 0.1f, r.center.y);
                d.Cilindro(pe, 0.14f, 0.1f, 8f, 6, "Poste");
                d.Entre(pe + Vector3.up * 8f + Vector3.forward * 1.2f, pe + Vector3.up * 8f + Vector3.back * 1.2f, 0.12f, 0.12f, "Poste");
                foreach (float s in new[] { -1f, 1f })
                    d.Caixa(pe + new Vector3(0f, 7.9f, s * 1.3f), new Vector3(0.4f, 0.2f, 0.7f), "Metal");
                d.Colide(pe + Vector3.up * 4f, new Vector3(0.35f, 8f, 0.35f));
            }
            saida.Add(Peca("Estacionamento", d));
        }

        // o prédio da galeria de serviço: o bloco de concreto com as entradas de ar e a porta de aço (a Etapa 8 liga os
        // túneis do laboratório a ele)
        void GaleriaDeServico(List<PecaDaUsina> saida, System.Random sorte)
        {
            var r = Galeria;
            float piso = AlturaDoPatio("Galeria de serviço");
            var d = new Desenho(L(r.center.x, piso, r.center.y));
            Caixote(d, r, piso, piso + 4.6f, "Bunker", "Concreto", 0.5f);
            // a porta de aço com as faixas pretas e amarelas, o teclado e os avisos
            float s = r.center.y;
            Placa(d, r, Lado.Leste, s - 1.9f, s + 1.9f, piso, piso + 3.4f, 0.02f, "Concreto");
            for (int k = 0; k < 9; k++)
            {
                float y0 = piso + k * 0.38f;
                Placa(d, r, Lado.Leste, s - 1.75f, s - 1.45f, y0, y0 + 0.19f, 0.03f, "PlacaAmarela");
                Placa(d, r, Lado.Leste, s + 1.45f, s + 1.75f, y0 + 0.19f, y0 + 0.38f, 0.03f, "PlacaAmarela");
            }
            Placa(d, r, Lado.Leste, s - 1.3f, s + 1.3f, piso, piso + 3f, 0.05f, "Ferrugem");
            Placa(d, r, Lado.Leste, s - 1.2f, s + 1.2f, piso + 1.4f, piso + 1.6f, 0.06f, "FerrugemEscura");
            d.Caixa(NoLado(r, Lado.Leste, s + 2.3f, piso + 1.4f, 0.08f), new Vector3(0.16f, 0.3f, 0.22f), "Metal");
            Placa(d, r, Lado.Leste, s - 2.5f, s + 2.5f, piso + 3.6f, piso + 4.3f, 0.03f, "PlacaVermelha");
            d.Letreiro("RESTRICTED AREA", NoLado(r, Lado.Leste, s, piso + 3.78f, 0.04f), DireitaDe(Lado.Leste), Vector3.up, Vector3.right, 0.32f, "PlacaBranca", 0.01f);
            d.Letreiro("SERVICE GALLERY B", NoLado(r, Lado.Oeste, s, piso + 3.6f, 0.03f), DireitaDe(Lado.Oeste), Vector3.up, Vector3.left, 0.4f, "Batente", 0.01f);
            // as entradas de ar com a grelha em cima
            foreach (float v in new[] { r.yMin + 3f, r.yMax - 3f })
            {
                var p = L(r.center.x - 3f, piso + 4.6f, v);
                d.Caixa(p + Vector3.up * 0.9f, new Vector3(2f, 1.8f, 2f), "Concreto");
                d.Caixa(p + Vector3.up * 1.82f, new Vector3(1.7f, 0.06f, 1.7f), "Grade");
            }
            // a janelinha de grade
            Placa(d, r, Lado.Montante, r.center.x - 0.6f, r.center.x + 0.6f, piso + 2.6f, piso + 3.4f, 0.03f, "Interior");
            for (float u = r.center.x - 0.5f; u <= r.center.x + 0.51f; u += 0.25f)
                d.Caixa(L(u, piso + 3f, r.yMin - 0.06f), new Vector3(0.04f, 0.8f, 0.04f), "Grade");
            Hera(d, L(r.xMin - 0.02f, piso, r.center.y), Vector3.left, Vector3.forward, 8f, 4.6f, 13);
            saida.Add(Peca("Galeria de serviço", d));
        }

        // a oficina de manutenção e os dois galpões (de duas águas, com as portas de enrolar para a estrada)
        void OficinaEGalpoes(List<PecaDaUsina> saida, System.Random sorte)
        {
            {
                var r = Oficina;
                float piso = AlturaDoPatio("Oficina");
                var d = new Desenho(L(r.center.x, piso, r.center.y));
                // (a chapa pintada como metal: a tinta de parede, no abandono, descascava em manchas brancas, e o telhado de
                // telha ganhava o musgo: os galpões pareciam camuflados)
                DuasAguas(d, r, piso, 8f, 11f, "CarroAzul", "Metal", sorte, 0.12f);
                PortaDeEnrolar(d, r, Lado.Oeste, r.yMin + 6f, r.yMin + 12f, piso, 6f, 0f);
                PortaDeEnrolar(d, r, Lado.Oeste, r.yMin + 18f, r.yMin + 24f, piso, 6f, 2.6f);
                Porta(d, r, Lado.Oeste, r.yMax - 6f, piso);
                d.Letreiro("MAINTENANCE", NoLado(r, Lado.Oeste, r.yMin + 15f, piso + 6.6f, 0.03f), DireitaDe(Lado.Oeste), Vector3.up, Vector3.left, 0.55f, "Batente", 0.01f);
                foreach (Lado l in new[] { Lado.Montante, Lado.Jusante })
                    for (int k = 0; k < 4; k++) Janela(d, r, l, r.xMin + 2f + k * 6.5f, r.xMin + 6.5f + k * 6.5f, piso + 5.3f, piso + 7f, 3, 1, sorte);
                // os tambores de óleo e a sucata do lado de fora
                for (int k = 0; k < 5; k++)
                    d.Cilindro(L(r.xMin + 3f + k * 0.75f, piso, r.yMax + 1.6f + (k % 2) * 0.7f), 0.3f, 0.3f, 0.9f, 8, k % 3 == 0 ? "CarroAzul" : "Ferrugem");
                d.Caixa(L(r.xMin + 8f, piso + 0.06f, r.yMax + 2f), new Vector3(1.2f, 0.12f, 1.2f), "MadeiraVelha");
                saida.Add(Peca("Oficina de manutenção", d));
            }
            foreach (var (r, nome, buracos, parede, telhado) in new[] { (Galpao1, "Galpão 1", 0.1f, "CarroVerde", "Metal"), (Galpao2, "Galpão 2", 0.35f, "CarroBege", "CarroCinza") })
            {
                float piso = AlturaDoPatio(nome);
                var d = new Desenho(L(r.center.x, piso, r.center.y));
                DuasAguas(d, r, piso, 9f, 12f, parede, telhado, sorte, buracos);
                PortaDeEnrolar(d, r, Lado.Oeste, r.center.y - 3.5f, r.center.y + 3.5f, piso, 7f, nome == "Galpão 1" ? 0f : 4.5f, "Metal");
                Porta(d, r, Lado.Oeste, r.yMin + 3f, piso);
                d.Letreiro(nome == "Galpão 1" ? "WAREHOUSE 1" : "WAREHOUSE 2", NoLado(r, Lado.Oeste, r.center.y, piso + 7.7f, 0.03f), DireitaDe(Lado.Oeste), Vector3.up, Vector3.left, 0.5f, "Batente", 0.01f);
                // o contêiner encostado no primeiro
                if (nome == "Galpão 1")
                {
                    var c = ModelosDaEstrada.Conteiner(1);
                    var M = Matrix4x4.TRS(L(r.center.x, piso, r.yMin - 3f), Quaternion.Euler(0f, 4f, 0f), Vector3.one);
                    d.Juntar(c.Malha, M);
                    foreach (var cx in c.Colisao) d.Colide(M.MultiplyPoint3x4(cx.Centro), cx.Tamanho, 4f + cx.GiroY);
                }
                saida.Add(Peca(nome, d));
            }
        }

        // o tanque de diesel deitado nos berços, dentro da bacia de contenção
        void TanqueDeDiesel(List<PecaDaUsina> saida, System.Random sorte)
        {
            var r = Tanque;
            float piso = AlturaDoPatio("Tanque");
            var d = new Desenho(L(r.center.x, piso, r.center.y));
            const float e = 0.3f, alto = 1.1f;
            d.Caixa(L(r.center.x, piso + alto * 0.5f, r.yMin + 1f), new Vector3(r.width - 2f, alto, e), "Concreto", true);
            d.Caixa(L(r.center.x, piso + alto * 0.5f, r.yMax - 1f), new Vector3(r.width - 2f, alto, e), "Concreto", true);
            d.Caixa(L(r.xMin + 1f, piso + alto * 0.5f, r.center.y), new Vector3(e, alto, r.height - 2f), "Concreto", true);
            d.Caixa(L(r.xMax - 1f, piso + alto * 0.5f, r.center.y), new Vector3(e, alto, r.height - 2f), "Concreto", true);
            d.Caixa(L(r.center.x, piso + 0.05f, r.center.y), new Vector3(r.width - 2f, 0.1f, r.height - 2f), "Concreto");
            var c = L(r.center.x, piso + 2.6f, r.center.y);
            d.CilindroDeitado(c, 2.1f, 15f, 12, "Cromo");
            d.Colide(c, new Vector3(15f, 4.2f, 4.2f));
            foreach (float du in new[] { -5.5f, 0f, 5.5f })
                d.Caixa(c + new Vector3(du, -1.6f, 0f), new Vector3(0.5f, 1.4f, 3.4f), "Concreto");
            // a ferrugem escorrendo, o letreiro, a escada e a passarela de cima
            for (int k = 0; k < 5; k++)
            {
                float du = -6f + k * 3f + (float)sorte.NextDouble();
                d.Quad(c + new Vector3(du - 0.3f, 0.6f, -2.13f), c + new Vector3(du + 0.3f, 0.6f, -2.13f), c + new Vector3(du + 0.2f, -1.4f, -1.6f), c + new Vector3(du - 0.2f, -1.4f, -1.6f), Vector3.back, "Ferrugem");
            }
            d.Letreiro("DIESEL", c + new Vector3(0f, -0.2f, -2.14f), Vector3.right, Vector3.up, Vector3.back, 0.6f, "PlacaVermelha", 0.01f);
            d.Caixa(c + new Vector3(7.75f, -0.2f, -0.4f), new Vector3(0.06f, 4.8f, 0.5f), "Metal");
            d.Caixa(c + new Vector3(0f, 2.15f, 0f), new Vector3(10f, 0.08f, 0.8f), "Grade");
            saida.Add(Peca("Tanque de diesel", d));
        }

        // as guaritas: a do portão do pátio e as das duas cabeceiras da crista, com a cancela (quebrada) de cada uma
        void Guaritas(List<PecaDaUsina> saida, System.Random sorte)
        {
            void Guarita(string nome, Rect area, float piso, Lado porta, Vector3 cancela, float giroDaCancela, int estado)
            {
                var r = Rect.MinMaxRect(area.center.x - 2.2f, area.center.y - 2.2f, area.center.x + 2.2f, area.center.y + 2.2f);
                var d = new Desenho(L(r.center.x, piso, r.center.y));
                Caixote(d, r, piso, piso + 3f, "ParedeBege", "Concreto", 0f);
                d.Caixa(L(r.center.x, piso + 3.15f, r.center.y), new Vector3(r.width + 1f, 0.3f, r.height + 1f), "Concreto");
                foreach (Lado l in new[] { Lado.Montante, Lado.Jusante, Lado.Oeste, Lado.Leste })
                {
                    float s0 = l == Lado.Oeste || l == Lado.Leste ? r.yMin : r.xMin, s1 = s0 + 4.4f;
                    if (l == porta)
                    {
                        Porta(d, r, l, (s0 + s1) * 0.5f, piso, 0.9f, 2.2f, "ParedeBegeEscura");
                        continue;
                    }
                    Janela(d, r, l, s0 + 0.4f, s1 - 0.4f, piso + 1.1f, piso + 2.5f, 3, 1, sorte);
                }
                // a cancela: o pé, a caixa e o braço listrado (em pé e torto, ou caído no chão)
                var pe = cancela;
                d.Caixa(pe + Vector3.up * 0.55f, new Vector3(0.45f, 1.1f, 0.45f), "PlacaAmarela", true);
                var giro = Quaternion.Euler(0f, giroDaCancela, 0f);
                Vector3 inicio, dir;
                if (estado == 0) { inicio = pe + Vector3.up * 1.0f; dir = giro * Quaternion.Euler(0f, 0f, 72f) * Vector3.right; }
                else { inicio = pe + giro * (Vector3.right * 0.8f) + Vector3.up * 0.12f; dir = giro * Quaternion.Euler(0f, 23f, 0f) * Vector3.right; }
                for (int k = 0; k < 7; k++)
                    d.Entre(inicio + dir * (k * 0.7f), inicio + dir * ((k + 1) * 0.7f), 0.12f, 0.12f, k % 2 == 0 ? "PlacaVermelha" : "PlacaBranca");
                saida.Add(Peca(nome, d));
            }
            float c = Crista;
            Guarita("Guarita do portão", GuaritaDoPortao, AlturaDoPatio("Guarita do portão"), Lado.Leste, L(789f, AlturaDoPatio("Guarita do portão"), 572f), 0f, 1);
            Guarita("Guarita leste", GuaritaLeste, c + 0.05f, Lado.Montante, L(903f, c, -5.2f), 90f, 0);
            Guarita("Guarita oeste", GuaritaOeste, c + 0.05f, Lado.Jusante, L(-16f, c, 5.4f), -90f, 1);
        }

        // um alambrado de a até b (u, v), acompanhando o chão: os postes de 3 em 3 m, o tubo de cima, os arames (em X, para
        // lembrar a tela), o arame farpado e a caixa de colisão de cada trecho; os trechos de 'rasgado' ficam no chão
        void TrechoDeAlambrado(Desenho d, Func<float, float, float> H, Vector2 a, Vector2 b, float rasgadoDe = -1f, float rasgadoAte = -1f)
        {
            float comp = Vector2.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(comp / 3f));
            var dir = (b - a) / comp;
            var fora = Vector3.Cross(Vector3.up, new Vector3(dir.x, 0f, -dir.y)).normalized;
            Vector3 P(float t, float y)
            {
                var q = Vector2.Lerp(a, b, t);
                return L(q.x, H(q.x, q.y) + y, q.y);
            }
            // um arame: uma faixa fina no plano do alambrado, dos dois lados
            void Arame(Vector3 p, Vector3 q, float grossura, string cor)
            {
                var o = Vector3.Cross(fora, q - p).normalized * (grossura * 0.5f);
                d.QuadDuplo(p + o, q + o, q - o, p - o, fora, cor);
            }
            for (int k = 0; k < n; k++)
            {
                float t0 = k / (float)n, t1 = (k + 1) / (float)n;
                float s = (t0 + t1) * 0.5f * comp;
                d.Entre(P(t0, -0.3f), P(t0, 2.55f), 0.09f, 0.09f, "Metal");
                if (s > rasgadoDe && s < rasgadoAte) continue;
                Arame(P(t0, 2.45f), P(t1, 2.45f), 0.06f, "Metal");
                Arame(P(t0, 0.12f), P(t1, 0.12f), 0.03f, "Grade");
                Arame(P(t0, 1.25f), P(t1, 1.25f), 0.03f, "Grade");
                Arame(P(t0, 0.15f), P(t1, 2.4f), 0.025f, "Grade");
                Arame(P(t1, 0.15f), P(t0, 2.4f), 0.025f, "Grade");
                Arame(P(t0, 2.8f), P(t1, 2.8f), 0.02f, "Grade");
                var meio = (P(t0, 1.3f) + P(t1, 1.3f)) * 0.5f;
                d.Colide(meio, new Vector3(0.15f, 2.6f, Vector3.Distance(P(t0, 0f), P(t1, 0f))), Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg);
            }
            d.Entre(P(1f, -0.3f), P(1f, 2.55f), 0.09f, 0.09f, "Metal");
        }

        // o alambrado do pátio, com o portão (uma folha fechada com a corrente e a outra caída), as placas e a da subestação
        void Alambrado(List<PecaDaUsina> saida, System.Random sorte, Func<float, float, float> H)
        {
            float v = LinhaDoAlambrado;
            var d = new Desenho(L(Portao.x, H(Portao.x, v), v));
            TrechoDeAlambrado(d, H, new Vector2(AlambradoDe, v), new Vector2(Portao.x, v), 60f, 66f);
            TrechoDeAlambrado(d, H, new Vector2(Portao.y, v), new Vector2(AlambradoAte, v), 92f, 101f);
            TrechoDeAlambrado(d, H, new Vector2(AlambradoAte, v), new Vector2(AlambradoAte + 10f, 440f));
            // a folha fechada (com a tela em X e a corrente) e a caída no chão, rente
            float y0 = H(Portao.x, v), meio = (Portao.x + Portao.y) * 0.5f;
            void Folha(Vector3 o, Vector3 ao, Vector3 cima)
            {
                var b = o + ao;
                d.Entre(o, b, 0.08f, 0.08f, "Metal");
                d.Entre(o + cima, b + cima, 0.08f, 0.08f, "Metal");
                d.Entre(o, o + cima, 0.08f, 0.08f, "Metal");
                d.Entre(b, b + cima, 0.08f, 0.08f, "Metal");
                d.Entre(o, b + cima, 0.03f, 0.03f, "Grade");
                d.Entre(b, o + cima, 0.03f, 0.03f, "Grade");
                d.Entre(o + cima * 0.5f, b + cima * 0.5f, 0.03f, 0.03f, "Grade");
            }
            Folha(L(Portao.x, y0 + 0.1f, v), Vector3.right * (meio - Portao.x), Vector3.up * 2.2f);
            d.Colide(L((Portao.x + meio) * 0.5f, y0 + 1.2f, v), new Vector3(meio - Portao.x, 2.3f, 0.15f));
            d.Caixa(L(meio - 0.1f, y0 + 1.2f, v), new Vector3(0.12f, 0.35f, 0.12f), "Cromo");
            float yb = H(meio + 4f, v + 3f);
            Folha(L(meio + 0.3f, yb + 0.06f, v + 0.6f), Vector3.right * (Portao.y - meio - 0.6f), new Vector3(0f, 0f, -2.2f));
            foreach (float u in new[] { Portao.x, Portao.y })
                d.Entre(L(u, H(u, v) - 0.4f, v), L(u, H(u, v) + 2.8f, v), 0.16f, 0.16f, "Metal", true);
            // as placas do portão
            void PlacaNoAlambrado(float u, string a, string b, string fundo, string letra)
            {
                float y = H(u, v) + 1.35f;
                d.Caixa(L(u, y, v + 0.06f), new Vector3(1.5f, 0.75f, 0.03f), fundo);
                d.Letreiro(a, L(u, y + 0.06f, v + 0.08f), Vector3.right, Vector3.up, Vector3.back, 0.16f, letra, 0f);
                d.Letreiro(b, L(u, y - 0.22f, v + 0.08f), Vector3.right, Vector3.up, Vector3.back, 0.1f, letra, 0f);
            }
            PlacaNoAlambrado(Portao.x - 5f, "NO TRESPASSING", "AUTHORIZED PERSONNEL ONLY", "PlacaBranca", "PlacaVermelha");
            PlacaNoAlambrado(Portao.y + 6f, "DANGER", "HIGH VOLTAGE", "PlacaAmarela", "Grade");
            // o monumento do nome, do lado de fora
            {
                float u = Portao.y + 14f, vv = v + 10f, y = H(u, vv);
                d.Caixa(L(u, y + 0.7f, vv), new Vector3(6.4f, 1.6f, 0.7f), Cor.PorNormal("Concreto", "ConcretoClaro"), true);
                d.Letreiro("LAKEWOOD HYDROELECTRIC PLANT", L(u, y + 0.95f, vv + 0.35f), Vector3.right, Vector3.up, Vector3.back, 0.3f, "Bunker", 0.01f);
                d.Letreiro("LAKEWOOD COUNTY WATER AND POWER", L(u, y + 0.5f, vv + 0.35f), Vector3.right, Vector3.up, Vector3.back, 0.16f, "Bunker", 0.01f);
                for (int k = 0; k < 6; k++)
                    d.Quad(L(u - 3.2f + k * 1.1f, y + 0.12f, vv + 0.37f), L(u - 2.7f + k * 1.1f, y + 0.12f, vv + 0.37f),
                           L(u - 2.7f + k * 1.1f, y + 0.12f + 0.4f * (float)sorte.NextDouble(), vv + 0.37f), L(u - 3.2f + k * 1.1f, y + 0.12f + 0.4f * (float)sorte.NextDouble(), vv + 0.37f), Vector3.back, "Hera");
            }
            saida.Add(Peca("Alambrado e portão", d));

            // o alambrado da subestação (fechado, com o portão aberto do lado da estrada) e as placas de perigo
            var s = Subestacao;
            var e = new Desenho(L(s.center.x, AlturaDoPatio("Subestação"), s.center.y));
            float x0 = s.xMin + 2f, x1 = s.xMax - 2f, v0 = s.yMin + 2f, v1 = s.yMax - 2f;
            TrechoDeAlambrado(e, H, new Vector2(x0, v0), new Vector2(x1, v0));
            TrechoDeAlambrado(e, H, new Vector2(x1, v0), new Vector2(x1, 186f));
            TrechoDeAlambrado(e, H, new Vector2(x1, 196f), new Vector2(x1, v1));
            TrechoDeAlambrado(e, H, new Vector2(x1, v1), new Vector2(x0, v1), 40f, 45f);
            TrechoDeAlambrado(e, H, new Vector2(x0, v1), new Vector2(x0, v0));
            foreach (var (u, vv, lado) in new[] { (x1, 180f, Lado.Leste), (x1, 205f, Lado.Leste), (s.center.x, v0, Lado.Montante), (s.center.x - 30f, v1, Lado.Jusante) })
            {
                var p = lado == Lado.Leste ? L(x1 + 0.08f, H(u, vv) + 1.4f, vv) : lado == Lado.Montante ? L(u, H(u, vv) + 1.4f, v0 - 0.08f) : L(u, H(u, vv) + 1.4f, v1 + 0.08f);
                var fora = ForaDe(lado);
                var dir = DireitaDe(lado);
                e.Caixa(p, lado == Lado.Leste ? new Vector3(0.03f, 0.7f, 1.2f) : new Vector3(1.2f, 0.7f, 0.03f), "PlacaAmarela");
                e.Letreiro("DANGER", p + Vector3.up * 0.08f, dir, Vector3.up, fora, 0.18f, "Grade", 0.02f);
                e.Letreiro("HIGH VOLTAGE", p + Vector3.down * 0.2f, dir, Vector3.up, fora, 0.11f, "PlacaVermelha", 0.02f);
            }
            saida.Add(Peca("Alambrado da subestação", e));
        }

        // o posto de controle da quarentena na cabeceira leste da crista: o caminhão do exército, o contêiner atravessado,
        // as barreiras em zigue-zague (à parte: o modelo do Cidade_Rua) e os sacos de areia
        void PostoDeControle(List<PecaDaUsina> saida, System.Random sorte)
        {
            float c = Crista;
            var d = new Desenho(L(860f, c, 0f));
            void Modelo(ModelosDaEstrada.Modelo m, Vector3 onde, float giro)
            {
                var M = Matrix4x4.TRS(onde, Quaternion.Euler(0f, giro, 0f), Vector3.one);
                d.Juntar(m.Malha, M);
                foreach (var cx in m.Colisao) d.Colide(M.MultiplyPoint3x4(cx.Centro), cx.Tamanho, giro + cx.GiroY);
            }
            Modelo(ModelosDaEstrada.CaminhaoMilitar(), L(826f, c + 0.01f, 2.1f), 96f);
            Modelo(ModelosDaEstrada.Conteiner(2), L(851f, c + 0.01f, -2.3f), 3f);
            Modelo(ModelosDaEstrada.Sacos(), L(906.5f, c + 0.05f, 6.5f), 0f);
            Modelo(ModelosDaEstrada.Sacos(), L(904.5f, c + 0.05f, 8.6f), 90f);
            saida.Add(Peca("Posto de controle", d));
            // as barreiras de concreto (o modelo do Cidade_Rua: 1,8 m no x)
            foreach (var (u, v, g) in new[] { (880f, 1.6f, 88f), (880.4f, 3.4f, 94f), (868f, -3.6f, 15f), (895f, -1f, 140f) })
                saida.Add(new PecaDaUsina { Nome = "Barreira", Tipo = TipoDaPecaDaUsina.Barreira, Posicao = NoMundo(L(u, c, v)), Giro = Rotacao * Quaternion.Euler(0f, g, 0f) });
        }

        // os carros abandonados: no estacionamento, na crista (um batido no parapeito) e pelo pátio
        void Carros(List<PecaDaUsina> saida, Func<float, float, float> H)
        {
            float c = Crista, e = AlturaDoPatio("Estacionamento") + 0.1f;
            var lista = new List<(float u, float y, float v, float giro, int carro)>
            {
                (792.2f, e, 271f, 180f, 0), (797.8f, e, 271.4f, 176f, 2), (809f, e, 271f, 202f, 5), (814.6f, e, 271.2f, 183f, 1),
                (794.6f, e, 317f, 3f, 4), (811.4f, e, 317.2f, 357f, 3), (821f, e, 293f, 64f, 6),
                (310f, c, 1.9f, 92f, 1), (128f, c, -2f, 271f, 6), (705f, c, -2.8f, 56f, 2),
                (792f, -1f, 362f, 4f, 5), (754f, -1f, 286f, 181f, 3), (762f, -1f, 75f, 338f, 0),
            };
            foreach (var (u, y, v, giro, carro) in lista)
            {
                float yy = y >= 0f ? y : H(u, v);
                saida.Add(new PecaDaUsina { Nome = $"Carro: {ModelosDaEstrada.NomesDosCarros[carro]}", Tipo = TipoDaPecaDaUsina.Carro, Carro = carro,
                                            Posicao = NoMundo(L(u, yy, v)), Giro = Rotacao * Quaternion.Euler(0f, giro, 0f) });
            }
        }
    }
}
