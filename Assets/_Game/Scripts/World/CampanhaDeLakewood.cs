namespace ZombieCityCleaner.World
{
    /// <summary>A fase da campanha do Lakewood (25/11/2026, o caderno das missões: <see cref="CampanhaDeLakewood"/>).</summary>
    public enum FaseDaCampanha : byte { Nenhuma, DiaZero, Fuga, OBunker, RetomeAWoodcreek, Livre }

    /// <summary>
    /// As missões da campanha em fases e submissões (25/11/2026, a pedido do Pedro: "O jogo deve impedir o jogador de pular
    /// missões... Também deverá ter submissões... completar as missões conversando com os npc"). Cada fase tem a lista dela;
    /// o anfitrião só aceita a submissão da vez (<see cref="MundoDeLakewood.Concluir"/>): o que é feito fora de hora não
    /// conta. O caderno na tela (<see cref="UI.CadernoDeMissoes"/>) escreve a da vez e risca as feitas.
    ///  - <b>O Dia 0</b>: o jornal, as compras, a volta para casa, o armazém e as chaves.
    ///  - <b>A fuga</b>: a noite do colapso, até o portão do bunker.
    ///  - <b>O bunker</b>: o tutorial do Gus (25/11/2026, a etapa 6: o machado e a mochila, a lenha, a mesa de oficina, o metal
    ///    do pátio, a lança, a fogueira, a tocha, uma habilidade; LakewoodNaCena.Tutorial.cs). É a primeira missão depois
    ///    do Dia 0 ("deveria ser com o Gus e não com o Capitão").
    ///  - <b>Retome a Woodcreek</b>: com o Capitão, a casa da família, a rua, o armazém, a caminhonete e a volta.
    /// As submissões que ainda não estão no jogo (<see cref="Pronta"/>) pulam sozinhas.
    /// </summary>
    public static class CampanhaDeLakewood
    {
        // ------------------------------------------------------------------ o Dia 0
        public const int JornalNaTv = 0, CompreRecursos = 1, VolteParaCasa = 2, GuardeNoArmazem = 3, FecheACasa = 4;
        /// <summary>Quantas compras (de todo o grupo) fecham o "Compre recursos".</summary>
        public const int MinimoDeCompras = 5;

        // ------------------------------------------------------------------ a fuga
        public const int FujaParaOBunker = 0;

        // ------------------------------------------------------------------ o bunker (o tutorial do Gus)
        // (25/11/2026, a etapa 6: o metal do pátio entrou antes da lança, a pedido: "pegar algumas sucatas no chão")
        public const int FaleComOGus = 0, PegueOMachado = 1, CorteUmaArvore = 2, RacheAsToras = 3, MonteAMesa = 4, JunteOMetal = 5,
                         FacaALanca = 6, FacaAFogueira = 7, FacaATocha = 8, AprendaUmaHabilidade = 9, VolteAoGus = 10;

        // ------------------------------------------------------------------ retome a Woodcreek
        public const int FaleComOCapitao = 0, VaParaACasa = 1, RetomeARua = 2, AbraOArmazem = 3, ConserteACaminhonete = 4,
                         VolteAoBunker = 5, ConteAoCapitao = 6;

        public static string Titulo(FaseDaCampanha f) => f switch
        {
            FaseDaCampanha.DiaZero => "O Dia 0",
            FaseDaCampanha.Fuga => "A noite do colapso",
            FaseDaCampanha.OBunker => "O bunker",
            FaseDaCampanha.RetomeAWoodcreek => "Retome a Woodcreek",
            _ => "",
        };

        public static int Passos(FaseDaCampanha f) => f switch
        {
            FaseDaCampanha.DiaZero => 5,
            FaseDaCampanha.Fuga => 1,
            FaseDaCampanha.OBunker => 11,
            FaseDaCampanha.RetomeAWoodcreek => 7,
            _ => 0,
        };

        /// <summary>
        /// A submissão já está no jogo: as que vêm nas etapas seguintes do plano (o armazém, o tutorial do Gus) pulam sozinhas
        /// até lá. Uma fase sem nenhuma pronta pula inteira.
        /// </summary>
        public static bool Pronta(FaseDaCampanha f, int p) => f switch
        {
            FaseDaCampanha.DiaZero => true,
            FaseDaCampanha.Fuga => true,
            FaseDaCampanha.OBunker => true,   // (25/11/2026, a etapa 6: o tutorial do Gus)
            FaseDaCampanha.RetomeAWoodcreek => true,
            _ => false,
        };

        /// <summary>A submissão acaba numa conversa (o cliente pede pela conversa: <see cref="MundoDeLakewood.ConcluirPelaConversaRpc"/>).</summary>
        public static bool PelaConversa(FaseDaCampanha f, int p) =>
            (f == FaseDaCampanha.OBunker && (p == FaleComOGus || p == VolteAoGus)) ||
            (f == FaseDaCampanha.RetomeAWoodcreek && (p == FaleComOCapitao || p == ConteAoCapitao));

        /// <summary>O texto da submissão no caderno (com os números da rede de agora, se ela é a da vez).</summary>
        public static string Texto(FaseDaCampanha f, int p, MundoDeLakewood m, bool daVez)
        {
            int n1 = daVez && m != null ? m.Numero1 : 0, n2 = daVez && m != null ? m.Numero2 : 0;
            switch (f)
            {
                case FaseDaCampanha.DiaZero:
                    switch (p)
                    {
                        case JornalNaTv: return "Assista ao jornal na TV da sala.";
                        case CompreRecursos: return daVez ? $"Compre recursos nas lojas ({n1} de {MinimoDeCompras})." : "Compre recursos nas lojas.";
                        case VolteParaCasa: return "Volte para casa.";
                        case GuardeNoArmazem:
                            return daVez && n1 > 0 ? $"Descarregue a caçamba no armazém da garagem ({(n1 == 1 ? "1 coisa" : $"{n1} coisas")} nela)."
                                                   : "Guarde as compras no armazém da garagem.";
                        case FecheACasa: return "Tranque a casa com as chaves.";
                    }
                    break;
                case FaseDaCampanha.Fuga:
                    return "Fuja para o bunker da comunidade, na beira do lago.";
                case FaseDaCampanha.OBunker:
                    switch (p)
                    {
                        case FaleComOGus: return "Fale com o Gus, no pátio do bunker.";
                        case PegueOMachado: return "Pegue o machado com o Gus.";
                        case CorteUmaArvore: return daVez ? $"Corte uma árvore e junte as toras ({n1} de {n2})." : "Corte uma árvore e junte as toras.";
                        case RacheAsToras: return daVez ? $"Rache toras em madeira, com o machado ({n1} de {n2} madeiras)." : "Rache as toras em madeira, com o machado.";
                        case MonteAMesa: return "Faça a mesa de oficina (2 toras e 4 madeiras) e monte perto do bunker.";
                        case JunteOMetal:
                            return daVez ? $"Junte peças de metal e fita pelo pátio do bunker ({n1} de {n2})." : "Junte peças de metal e fita pelo pátio do bunker.";
                        case FacaALanca: return daVez ? $"Faça uma lança na mesa para cada um ({n1} de {n2})." : "Faça uma lança na mesa para cada um.";
                        case FacaAFogueira: return "Faça uma fogueira e acenda.";
                        case FacaATocha: return "Faça uma tocha.";
                        case AprendaUmaHabilidade: return "Aprenda uma habilidade (K).";
                        case VolteAoGus: return "Volte a falar com o Gus.";
                    }
                    break;
                case FaseDaCampanha.RetomeAWoodcreek:
                    switch (p)
                    {
                        case FaleComOCapitao: return "Fale com o Capitão, no pátio do bunker.";
                        case VaParaACasa: return "Vá com o Capitão até a casa da família, na Spruce Lane.";
                        case RetomeARua:
                            if (!daVez) return "Retome a rua da casa.";
                            return n2 > 0 ? $"Retome a rua: {n1} {(n1 == 1 ? "zumbi" : "zumbis")} ({n2} de pedra: quebre com o machado, a espingarda de perto ou o carro)."
                                          : $"Retome a rua: {n1} {(n1 == 1 ? "zumbi" : "zumbis")}.";
                        case AbraOArmazem: return "Veja o que sobrou no armazém da garagem.";
                        case ConserteACaminhonete:
                            return daVez ? $"Conserte a caminhonete: {(n1 == 1 ? "falta 1 peça" : $"faltam {n1} peças")} (rodas, vela, bateria, radiador)."
                                         : "Conserte a caminhonete.";
                        case VolteAoBunker: return "Leve a caminhonete de volta ao bunker, com o Capitão.";
                        case ConteAoCapitao: return "Conte ao Capitão como foi.";
                    }
                    break;
            }
            return "";
        }

        /// <summary>A fase depois desta (as fases sem nenhuma submissão pronta pulam).</summary>
        public static FaseDaCampanha Seguinte(FaseDaCampanha f)
        {
            var s = f;
            while (true)
            {
                if (s >= FaseDaCampanha.Livre) return FaseDaCampanha.Livre;
                s = (FaseDaCampanha)((byte)s + 1);
                if (s == FaseDaCampanha.Livre || PrimeiraPronta(s) >= 0) return s;
            }
        }

        /// <summary>A primeira submissão pronta da fase a partir de "de" (-1: nenhuma).</summary>
        public static int PrimeiraPronta(FaseDaCampanha f, int de = 0)
        {
            for (int p = de; p < Passos(f); p++) if (Pronta(f, p)) return p;
            return -1;
        }
    }
}
