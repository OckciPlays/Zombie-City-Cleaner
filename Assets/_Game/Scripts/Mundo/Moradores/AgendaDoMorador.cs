using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A agenda e as barras dos moradores (as decisões 10 e 11 do Plano_NPCs_Bunker.md da pasta de arte): o que a hora manda
    /// cada um fazer (dormir, o banheiro, as refeições, o trabalho, o lazer, o cochilo dos velhos, as rezas do religioso),
    /// com o notívago virando o dia e o insone dormindo à meia-noite, e o ritmo das barras (por hora do jogo).
    /// </summary>
    public static class AgendaDoMorador
    {
        // ------------------------------------------------------------ as barras (de 0 a 100, por hora do jogo)

        /// <summary>A fome esvazia em 80 horas e a sede em 50 (a metade do ritmo das do jogador).</summary>
        public const float FomePorHora = 100f / 80f, SedePorHora = 100f / 50f;
        /// <summary>O sono desce em 18 horas acordado e enche em 8 na cama dele (12 no sofá, no cochilo).</summary>
        public const float SonoPorHora = 100f / 18f, SonoNaCama = 100f / 8f, SonoNoSofa = 100f / 12f;
        /// <summary>O que uma refeição enche (por enquanto sem gastar o estoque: a fatia 2 da parte 6), e quanto ela dura.</summary>
        public const float FomeDaRefeicao = 45f, SedeDaRefeicao = 50f, Refeicao = 0.5f;
        /// <summary>Come nas refeições abaixo desta fome (ou sede); fora de hora, abaixo de <see cref="Urgente"/>.</summary>
        public const float ComeAbaixo = 60f, Urgente = 25f;
        /// <summary>Acordado e abaixo disto de sono, ele cochila no sofá.</summary>
        public const float SonoUrgente = 10f;

        // ------------------------------------------------------------ o corpo

        /// <summary>
        /// A rapidez dele andando (m/s): 1,6 (o clipe do andar é de 2,0 m/s, com os passos longos: mais devagar que isso, ele
        /// tocava em câmera lenta), o rápido e o lento 20%, o joelho ruim 25% (mancando), os velhos 15%.
        /// </summary>
        public static float Velocidade(FichaDoMorador f)
        {
            float v = 1.6f;
            if (f == null) return v;
            if (f.Tem("Rapido")) v *= 1.2f;
            if (f.Tem("Lento")) v *= 0.8f;
            if (Mancando(f)) v *= 0.75f;
            if (Velho(f)) v *= 0.85f;
            return v;
        }

        public static bool Mancando(FichaDoMorador f) => f != null && f.TemCondicao("JoelhoRuim");
        /// <summary>As vagas do inventário dele (a decisão 13): 6, e 4 com a coluna ruim.</summary>
        public static int Vagas(FichaDoMorador f) => f != null && f.TemCondicao("ColunaRuim") ? 4 : 6;
        /// <summary>O andar do velho: o de hoje, mais devagar e com o corpo curvado (no código).</summary>
        public static bool Velho(FichaDoMorador f) => f != null && f.idade >= 65;

        // ------------------------------------------------------------ a agenda

        /// <summary>Quanto o dia dele está deslocado: o notívago vive 12 horas depois (dorme de dia).</summary>
        public static float Deslocamento(FichaDoMorador f) => f != null && f.Tem("Notivago") ? 12f : 0f;

        /// <summary>Quando ele vai dormir (a hora dele, antes do deslocamento): às 22:00, e o insone à meia-noite.</summary>
        public static float Deita(FichaDoMorador f) => f != null && f.TemCondicao("Insonia") ? 24f : 22f;
        public const float Levanta = 6.5f;

        /// <summary>
        /// O que a agenda manda a uma hora do relógio (de 0 a 24), e até quando (em horas a partir de agora).
        /// </summary>
        public static AtividadeDoMorador Programada(FichaDoMorador f, float hora, out float dura)
        {
            float h = Mathf.Repeat(hora - Deslocamento(f), 24f);
            var a = NaHora(f, h, out float fim);
            dura = Mathf.Repeat(fim - h, 24f);
            if (dura < 0.02f) dura = 0.02f;
            return a;
        }

        // a agenda na hora dele (sem o deslocamento): a atividade e quando ela acaba
        static AtividadeDoMorador NaHora(FichaDoMorador f, float h, out float fim)
        {
            float deita = Deita(f);
            bool religioso = f != null && f.Tem("Religioso"), velho = f != null && f.idade >= 60;
            // a noite (de 'deita' a 6:30, passando da meia-noite)
            if (h < Levanta || h >= deita)
            {
                fim = Levanta;
                return AtividadeDoMorador.Dormir;
            }
            if (h < 7f)
            {
                if (religioso && h >= 6.75f) { fim = 7f; return AtividadeDoMorador.Rezar; }
                fim = religioso ? 6.75f : 7f;
                return AtividadeDoMorador.Banheiro;
            }
            if (h < 7.75f) { fim = 7.75f; return AtividadeDoMorador.Comer; }
            if (h < 12f) { fim = 12f; return AtividadeDoMorador.Trabalho; }
            if (h < 12.75f) { fim = 12.75f; return AtividadeDoMorador.Comer; }
            if (h < 19f)
            {
                if (velho && h >= 14f && h < 15f) { fim = 15f; return AtividadeDoMorador.Cochilo; }
                fim = velho && h < 14f ? 14f : 19f;
                return AtividadeDoMorador.Trabalho;
            }
            if (h < 19.75f) { fim = 19.75f; return AtividadeDoMorador.Comer; }
            if (religioso && h >= 21.5f && h < 22f) { fim = 22f; return AtividadeDoMorador.Rezar; }
            fim = religioso && h < 21.5f ? 21.5f : deita;
            return AtividadeDoMorador.Lazer;
        }

        /// <summary>O nome da atividade para o F1 e o computador.</summary>
        public static string Nome(AtividadeDoMorador a) => a switch
        {
            AtividadeDoMorador.Dormir => "dormindo",
            AtividadeDoMorador.Banheiro => "no banheiro",
            AtividadeDoMorador.Comer => "comendo",
            AtividadeDoMorador.Trabalho => "nos afazeres",
            AtividadeDoMorador.Lazer => "no lazer",
            AtividadeDoMorador.Cochilo => "cochilando",
            AtividadeDoMorador.Rezar => "rezando",
            AtividadeDoMorador.Buscar => "pegando no estoque",
            AtividadeDoMorador.Crise => "em crise",
            AtividadeDoMorador.Sinuca => "jogando sinuca",
            AtividadeDoMorador.Guardar => "guardando no lugar",
            _ => "à toa",
        };
    }
}
