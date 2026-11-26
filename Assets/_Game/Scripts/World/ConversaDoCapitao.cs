using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Sobreviventes;
using static ZombieCityCleaner.World.CampanhaDeLakewood;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A conversa com o Capitão (24/11/2026; 25/11/2026, o caderno das missões: <see cref="CampanhaDeLakewood"/>). Ele é o
    /// Peter Ziquinatti ("mas me chame de Capitão"), que cuida de quem sai pelo portão: conta o que houve e por que sair do
    /// bunker (o estoque acabando), chama para retomar a Woodcreek (a escolta começa), espera ou vem junto, explica a
    /// caminhonete e, no fim, ouve como foi (a fase acaba na conversa). O que muda vai pela <see cref="MundoDeLakewood"/>
    /// (o anfitrião confere a submissão da vez). Enquanto a tela está aberta, ele fica parado, olhando para quem fala.
    /// </summary>
    public class ConversaDoCapitao : MonoBehaviour, IConversavel
    {
        public Sobrevivente capitao;

        static MundoDeLakewood M => MundoDeLakewood.Instancia;
        static bool Em(int passo) => M != null && M.Em(FaseDaCampanha.RetomeAWoodcreek, passo);
        static bool NaFase => M != null && M.Fase == FaseDaCampanha.RetomeAWoodcreek;
        static bool AntesDaFase => M != null && M.Fase < FaseDaCampanha.RetomeAWoodcreek;

        public string NomeNaConversa => "o Capitão, da comunidade";
        public Vector3 Onde => transform.position;
        public bool PodeConversar => capitao != null && capitao.IsSpawned && !capitao.Morto;

        // (25/11/2026) o anfitrião o segura parado, olhando para quem conversa, até a tela fechar
        public void AoConversar(bool comecou)
        {
            var m = M;
            if (m != null && m.IsSpawned) m.ConversaComOCapitaoRpc(comecou);
        }

        public string Saudacao()
        {
            if (AntesDaFase) return "Agora não. Primeiro o Gus quer ver vocês: ele explica como as coisas funcionam aqui.";
            if (!NaFase) return "Tudo tranquilo por aqui. Se precisar de mim, é só chamar.";
            switch (M.Passo)
            {
                case FaleComOCapitao:
                    return "Até que enfim. Cinco anos aqui embaixo... Lá fora, a Woodcreek é deles agora, e o nosso estoque não vai durar para sempre.";
                case VaParaACasa: return "Estou com vocês. Fiquem perto e não gastem bala à toa.";
                case RetomeARua: return "Eles estão por toda a rua! Limpem a área, eu cubro vocês!";
                case AbraOArmazem: return "A rua é nossa. Vejam o que sobrou no armazém de vocês.";
                case ConserteACaminhonete:
                    return "A caminhonete está acabada: sem rodas, sem vela, sem bateria, sem radiador. Os carros largados pela vila ainda têm peças.";
                case VolteAoBunker: return "Ela anda! Agora, de volta ao bunker. Eu vou na caçamba.";
                case ConteAoCapitao: return "Chegamos inteiros. E então?";
            }
            return "Tudo tranquilo por aqui.";
        }

        public List<Pergunta> Perguntas()
        {
            var m = M;
            var lista = new List<Pergunta>
            {
                // (25/11/2026, a pedido do Pedro: "quando perguntarmos quem ele é, deveria aparecer, meu nome é Peter
                // ziquinatti, mas me chame de capitão")
                new Pergunta("Quem é você?",
                    "Meu nome é Peter Ziquinatti, mas me chame de Capitão. Servi no exército antes de tudo isso; hoje cuido de quem sai pelo portão.",
                    "Meu nome é Peter Ziquinatti, mas me chame de Capitão. O Gus cuida de quem está aqui dentro; eu cuido de quem sai pelo portão."),
                new Pergunta("O que aconteceu lá fora?",
                    "Na noite em que vocês chegaram, a cidade caiu. O que sobrou da Woodcreek é deles. Mas ainda tem muita coisa útil lá, inclusive o que é de vocês.",
                    "Os Ocos tomaram as ruas. De dia, no sol, eles viram pedra; à noite, nem pense em sair sem luz."),
                // (25/11/2026, a pedido: "está faltando explicação do pq exploramos")
                new Pergunta("Por que temos que sair do bunker?",
                    "Porque o estoque acaba. Comida, remédio, combustível, munição: cinco anos comeram quase tudo. Cada saída traz o que falta, ou não temos mais um inverno.",
                    "Porque aqui dentro ninguém planta nem fabrica remédio. O que precisamos ficou lá fora, nas casas, nas lojas, nos carros largados. Alguém tem que ir buscar."),
            };
            if (Em(FaleComOCapitao))
                lista.Add(new Pergunta("A nossa casa, na Spruce Lane, ainda tem o que compramos antes de tudo. Vamos retomar a Woodcreek.",
                    "Então vamos. Se a casa ainda estiver de pé, o que vocês guardaram lá vale ouro. Peguem armas no painel do bunker antes de sair.")
                { acao = () => m?.EscoltaRpc(true), fecha = true });
            if (NaFase && M.Passo >= VaParaACasa && M.Passo <= VolteAoBunker)
            {
                lista.Add(new Pergunta("Venha comigo.", "Estou logo atrás de você.", "Vamos.") { acao = () => m?.EscoltaRpc(true) });
                lista.Add(new Pergunta("Espere aqui.", "Fico de olho daqui. Grite se precisar.", "Certo, seguro esta posição.") { acao = () => m?.EscoltaRpc(false) });
            }
            if (Em(ConserteACaminhonete))
                lista.Add(new Pergunta("Como conserto a caminhonete?",
                    "Quatro rodas, uma vela, uma bateria e um radiador. Tire dos carros largados: o capô abre, e as rodas saem com o macaco, que ficou na caçamba.",
                    "Procure os carros abandonados da vila. Roda sem furo, bateria com carga, vela boa. O radiador pode estar meio gasto, mas tem que ter um."));
            // (25/11/2026, a revisão: o bug 77) num jogo sem o Dia 0, a chave fica no armazém da garagem; no Dia 0, ela foi
            // para lá na fuga, com quem a levava, ou ficou na mesa da sala
            if (Em(ConserteACaminhonete) || Em(VolteAoBunker))
                lista.Add(new Pergunta("E a chave da caminhonete?",
                    "Se não está com vocês, procurem na casa: no armazém da garagem, ou onde vocês a deixaram. Sem a chave, só na ligação direta.",
                    "Vejam no armazém da garagem. O que vocês guardaram ficou lá. Sem a chave, só fazendo a ligação direta."));
            if (Em(ConteAoCapitao))
                lista.Add(new Pergunta("A caminhonete anda, e trouxemos o que dava. A Woodcreek é nossa de novo.",
                    "Bom trabalho. Com ela, dá para ir mais longe e trazer mais. Descansem: amanhã tem mais rua para limpar.")
                { acao = () => m?.ConcluirPelaConversaRpc((byte)FaseDaCampanha.RetomeAWoodcreek, (byte)ConteAoCapitao) });
            lista.Add(new Pergunta("Tchau.") { fecha = true });
            return lista;
        }
    }
}
