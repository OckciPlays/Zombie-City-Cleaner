using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Sobreviventes;
using static ZombieCityCleaner.World.CampanhaDeLakewood;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A conversa com o Gus no tutorial (25/11/2026, a etapa 6, a pedido do Pedro: "a primeira missão após o D0 deveria ser com
    /// o Gus e não com o Capitão... ele poderia nos dar um machado para a gente ir catar madeira, lá a gente iria craftar uma
    /// mesa da oficina e começaríamos a fazer alguns craft básicos que ele nos ensinaria"). O Gus Rodrigues, o dono do bunker
    /// e líder da comunidade, espera no pátio durante a fase "O bunker" (<see cref="LakewoodNaCena"/>.Tutorial.cs): conta o
    /// que houve, explica a mochila (TAB), as habilidades (K) e o craft, dá o machado e a mochila pequena e, a cada submissão,
    /// diz o que fazer. A fase acaba falando com ele de novo, e ele manda falar com o Capitão. Enquanto a tela está aberta, ele
    /// fica parado, olhando para quem fala.
    /// </summary>
    public class ConversaDoGus : MonoBehaviour, IConversavel
    {
        public Sobrevivente gus;

        static MundoDeLakewood M => MundoDeLakewood.Instancia;
        static bool Em(int passo) => M != null && M.Em(FaseDaCampanha.OBunker, passo);
        static bool NaFase => M != null && M.Fase == FaseDaCampanha.OBunker;

        public string NomeNaConversa => "o Gus, líder do bunker";
        public Vector3 Onde => transform.position;
        public bool PodeConversar => gus != null && gus.IsSpawned && !gus.Morto;

        public void AoConversar(bool comecou)
        {
            var m = M;
            if (m != null && m.IsSpawned) m.ConversaComOGusRpc(comecou);
        }

        public string Saudacao()
        {
            if (!NaFase) return "Está tudo em ordem aqui dentro. O Capitão cuida das saídas.";
            switch (M.Passo)
            {
                case FaleComOGus:
                    return "Vocês acordaram. Cinco anos aqui embaixo, hein? Antes de qualquer um sair por aquele portão, deixa eu mostrar como as coisas funcionam por aqui.";
                case PegueOMachado: return "Primeiro, lenha. Venham pegar um machado, cada um. E uma mochila: ninguém carrega uma mesa nos bolsos.";
                case CorteUmaArvore: return "A mata começa logo ali. Uma árvore grande dá até três toras; a pequena, só gravetos.";
                case RacheAsToras: return "Com o machado na mochila, as toras viram madeira. Abram a mochila (TAB) e rachem as toras com o botão direito.";
                case MonteAMesa: return "Agora a mesa de oficina: 2 toras e 4 madeiras, feita à mão, na aba Criar. Depois montem ela aqui perto.";
                case JunteOMetal: return "Para as lanças, vocês vão precisar de metal e fita. Tem coisa largada pelo pátio: procurem no chão, perto do portão.";
                case FacaALanca: return "Na mesa (E), façam uma lança para cada um. Contra os Ocos, distância é vida.";
                case FacaAFogueira: return "Agora o fogo: 4 madeiras e um pano, à mão, e montem no chão. Os Ocos não chegam perto do fogo.";
                case FacaATocha: return "Uma tocha, para a noite: uma madeira e um pano. A lanterna fica sem pilha; o fogo, não.";
                case AprendaUmaHabilidade: return "Tudo o que vocês fazem ensina alguma coisa. Abram as habilidades (K) e escolham uma.";
                case VolteAoGus: return "Pronto. Agora vocês sabem o básico.";
            }
            return "Estou aqui.";
        }

        public List<Pergunta> Perguntas()
        {
            var m = M;
            var lista = new List<Pergunta>
            {
                new Pergunta("Quem é você?",
                    "Gus Rodrigues. Este bunker era meu antes de tudo; hoje é de todo mundo que chegou no portão aquela noite. Eu cuido de quem está aqui dentro.",
                    "Gus Rodrigues, o dono deste buraco. O Capitão cuida de quem sai pelo portão; eu, de quem fica."),
                new Pergunta("O que aconteceu lá fora?",
                    "O surto. Começou como uma gripe no jornal; em uma semana, os doentes viraram Ocos. Vocês chegaram no portão na última noite, e a gente fechou tudo. Cinco anos esperando o mundo acalmar.",
                    "Os Ocos tomaram tudo. De dia, no sol, eles viram pedra; à noite, andam. Lá fora ainda tem muita coisa, mas nada é de graça."),
                new Pergunta("Por que temos que sair?",
                    "Porque o estoque acaba. Comida, remédio, combustível... cinco anos comeram quase tudo. O Capitão cuida das saídas; antes, eu quero vocês prontos.",
                    "Aqui embaixo ninguém planta nem fabrica remédio. O que precisamos ficou lá fora. Mas primeiro, o básico."),
            };
            if (Em(FaleComOGus))
                lista.Add(new Pergunta("Como as coisas funcionam aqui?",
                    "A mochila abre no TAB: lá vocês veem o que carregam, o corpo de vocês e as receitas. O K abre as habilidades. As coisas simples se fazem à mão; o resto, numa mesa de oficina. Venham, vamos começar.")
                { acao = () => m?.ConcluirPelaConversaRpc((byte)FaseDaCampanha.OBunker, (byte)FaleComOGus) });
            // (cada um pega o seu, uma vez: quem chega no meio também)
            if (NaFase && M.Passo >= PegueOMachado)
                lista.Add(new Pergunta("Me dá um machado.",
                    "Tome. E esta mochila, se ainda não tiver uma. Cuidem bem: machado bom não se acha mais.")
                { acao = () => m?.PedirOMachadoRpc() });
            if (Em(CorteUmaArvore))
                lista.Add(new Pergunta("Como corto uma árvore?",
                    "Com o machado na mão, golpeie o tronco até ela cair. Depois juntem as toras do chão (E). Cuidado com a queda: árvore cai em quem estiver embaixo."));
            if (Em(RacheAsToras))
                lista.Add(new Pergunta("Como racho as toras?",
                    "Abra a mochila (TAB), clique com o botão direito na tora e escolha rachar. Precisa do machado na mochila: cada tora vira 3 madeiras."));
            if (Em(MonteAMesa))
                lista.Add(new Pergunta("Como faço a mesa?",
                    "Na mochila, aba Criar: a mesa de oficina sai à mão, com 2 toras e 4 madeiras. Depois, botão direito no kit e montar no chão. Se faltar tora, corte outra árvore."));
            if (Em(JunteOMetal))
                lista.Add(new Pergunta("Onde acho o metal?",
                    "Pelo pátio, perto do portão: peças de metal, fita e alguma sucata. Peguem uma peça e uma fita para cada lança."));
            if (Em(VolteAoGus))
                lista.Add(new Pergunta("Fizemos tudo.",
                    "Então vocês estão prontos para lá fora. Falem com o Capitão, no portão: ele tem um trabalho para vocês.")
                { acao = () => m?.ConcluirPelaConversaRpc((byte)FaseDaCampanha.OBunker, (byte)VolteAoGus) });
            lista.Add(new Pergunta("Tchau.") { fecha = true });
            return lista;
        }
    }
}
