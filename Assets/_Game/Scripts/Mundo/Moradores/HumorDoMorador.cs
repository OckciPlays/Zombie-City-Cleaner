using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Um motivo do humor na vida de um morador (os "pensamentos" do RimWorld, a decisão 16 do Plano_NPCs_Bunker.md da pasta
    /// de arte): a chave da tabela (<see cref="HumorDoMorador.Tabela"/>), quanto ele vale para este morador (com os traços já
    /// contados) e até quando (as horas do jogo desde o dia 0). Os que valem "enquanto" (a fartura, o casal) são renovados a
    /// cada passo, com um prazo curto.
    /// </summary>
    [Serializable]
    public class MotivoDoMorador
    {
        public string chave;
        public float valor;
        public float ate;
    }

    /// <summary>As três faixas do humor (a decisão 16): triste abaixo de 35, feliz acima de 65.</summary>
    public enum FaixaDoHumor : byte { Triste, Neutro, Feliz }

    /// <summary>As crises (a decisão 16): as leves (abaixo de 15 por 2 horas) e a forte (abaixo de 5).</summary>
    public enum CriseDoMorador : byte { Nenhuma, Chorar, Comilanca, Vagar, JogarNoChao }

    /// <summary>
    /// O humor dos moradores (a parte 8 do Plano_NPCs_Bunker.md da pasta de arte, a primeira fatia): um número de 0 a 100 que
    /// começa em 50, feito dos motivos com prazo e do fixo dos traços (o alegre +10, o pessimista −10, o notívago +4 de
    /// noite). O número mostrado anda até a conta aos poucos (<see cref="PorHora"/>), como no RimWorld. As faixas e as
    /// crises saem dele.
    /// </summary>
    public static class HumorDoMorador
    {
        public const float Comeco = 50f, Triste = 35f, Feliz = 65f, CriseLeve = 15f, CriseForte = 5f;
        /// <summary>O quanto o humor anda até a conta por hora do jogo.</summary>
        public const float PorHora = 12f;
        /// <summary>O prazo dos motivos "enquanto" (renovados a cada passo): somem uns minutos depois que a causa some.</summary>
        public const float Enquanto = 0.25f;
        /// <summary>As horas abaixo da crise leve até ela vir, a forte, e o descanso entre duas crises.</summary>
        public const float AteACriseLeve = 2f, AteACriseForte = 1f, EntreCrises = 24f;

        public sealed class Motivo
        {
            public string chave, nome;
            public float valor, horas;
        }

        static Motivo M(string chave, string nome, float valor, float horas) => new Motivo { chave = chave, nome = nome, valor = valor, horas = horas };

        /// <summary>
        /// Os motivos da tabela da decisão 16 que já têm causa (o humor e o lazer; a conversa vem com as falas, a doença e o
        /// luto com a saúde). As horas 0 são os "enquanto".
        /// </summary>
        public static readonly Motivo[] Tabela =
        {
            M("Fome", "passou fome", -15f, 24f),
            M("Sede", "passou sede", -15f, 24f),
            M("CamaBoa", "dormiu na cama dele", 5f, 24f),
            M("DormiuMal", "dormiu mal", -8f, 24f),
            M("Lazer", "se divertiu", 8f, 24f),
            M("SemLazer", "nenhum lazer há 2 dias", -8f, 0f),
            M("Rezou", "rezou", 6f, 12f),
            M("Paixao", "fez o que gosta", 4f, 2f),
            M("Fartura", "fartura", 8f, 0f),
            M("Escassez", "escassez", -12f, 0f),
            M("Casal", "junto do casal", 5f, 0f),
            M("Voltou", "um jogador voltou", 10f, 24f),
            M("Portao", "o portão aberto", -6f, 0f),
            M("Desabafou", "desabafou", 20f, 24f),
            // o lazer (a segunda fatia): o cigarro do fumante e a sinuca (o jogador, o dobro)
            M("Fumou", "fumou", 6f, 6f),
            M("SemCigarro", "sem cigarro há mais de um dia", -12f, 0f),
            M("Sinuca", "jogou sinuca", 8f, 24f),
            M("Teste", "o teste do F1", 0f, 6f),
        };

        public static Motivo DaChave(string chave) => Array.Find(Tabela, m => m.chave == chave);

        /// <summary>O nome do motivo para a tela ("se divertiu"; o do casal pelo sexo de quem tem).</summary>
        public static string Nome(string chave, bool mulher)
        {
            var m = DaChave(chave);
            if (m == null) return chave;
            if (chave == "CamaBoa" && mulher) return "dormiu na cama dela";
            return m.nome;
        }

        /// <summary>O fixo dos traços, na hora do relógio (o notívago fica feliz de noite).</summary>
        public static float DosTracos(FichaDoMorador f, float hora)
        {
            if (f == null) return 0f;
            float h = 0f;
            if (f.Tem("Alegre")) h += 10f;
            if (f.Tem("Pessimista")) h -= 10f;
            if (f.Tem("Notivago") && (hora >= 20f || hora < 6f)) h += 4f;
            return h;
        }

        /// <summary>Para onde o humor vai: 50, os traços e os motivos (de 0 a 100).</summary>
        public static float Alvo(FichaDoMorador f, VidaDoMorador v, float hora)
        {
            float h = Comeco + DosTracos(f, hora);
            if (v?.motivos != null) foreach (var m in v.motivos) h += m.valor;
            return Mathf.Clamp(h, 0f, 100f);
        }

        public static FaixaDoHumor Faixa(float humor) => humor < Triste ? FaixaDoHumor.Triste : humor > Feliz ? FaixaDoHumor.Feliz : FaixaDoHumor.Neutro;

        public static string NomeDaFaixa(FaixaDoHumor f, bool mulher) => f switch
        {
            FaixaDoHumor.Triste => "triste",
            FaixaDoHumor.Feliz => "feliz",
            _ => mulher ? "neutra" : "neutro",
        };

        /// <summary>O que a crise é, em palavras ("chorando trancado no quarto").</summary>
        public static string NomeDaCrise(CriseDoMorador c, bool mulher) => c switch
        {
            CriseDoMorador.Chorar => mulher ? "chorando trancada no quarto" : "chorando trancado no quarto",
            CriseDoMorador.Comilanca => "comendo sem fome",
            CriseDoMorador.Vagar => "andando sem rumo, falando sozinho",
            CriseDoMorador.JogarNoChao => "jogando no chão o que está nas estantes",
            _ => "",
        };

        // ------------------------------------------------------------ os motivos na vida

        /// <summary>Põe o motivo (ou renova o que já tem: o valor novo e o prazo mais longo).</summary>
        public static void Por(VidaDoMorador v, string chave, float valor, float ate)
        {
            if (v.motivos == null) v.motivos = new List<MotivoDoMorador>();
            var m = v.motivos.Find(x => x.chave == chave);
            if (m == null) v.motivos.Add(new MotivoDoMorador { chave = chave, valor = valor, ate = ate });
            else
            {
                m.valor = valor;
                m.ate = Mathf.Max(m.ate, ate);
            }
        }

        /// <summary>Um motivo "enquanto" (renovado a cada passo; some uns minutos depois da causa).</summary>
        public static void PorEnquanto(VidaDoMorador v, string chave, float valor, float agora) => Por(v, chave, valor, agora + Enquanto);

        public static void Tirar(VidaDoMorador v, string chave) => v.motivos?.RemoveAll(x => x.chave == chave);

        public static bool Tem(VidaDoMorador v, string chave) => v.motivos != null && v.motivos.Exists(x => x.chave == chave);

        /// <summary>Tira os motivos vencidos.</summary>
        public static void Vencer(VidaDoMorador v, float agora) => v.motivos?.RemoveAll(x => x.ate < agora);
    }
}
