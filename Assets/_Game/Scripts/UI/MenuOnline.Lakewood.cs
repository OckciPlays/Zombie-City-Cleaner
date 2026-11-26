using UnityEngine;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Salvar;

namespace ZombieCityCleaner.UI
{
    // O Novo jogo no Lakewood (12/11/2026, a pedido: "vamos ajustar para que o lakewood seja o mapa que aparece no jogo
    // normal, vamos deixar desligado a parte de selecionar os NPCs já que não temos bunker por enquanto"). Na cena Lakewood,
    // o menu é o do jogo (o título, o vídeo e a trilha do menu: o MenuPrincipal), e o "Iniciar novo jogo" abre esta tela, no
    // estilo da do mundo sorteado (MenuOnline.NovoJogo.cs):
    //  - à esquerda, o MUNDO (o mapa é um só: o Condado de Lakewood, sem o nome, o tamanho e a semente) e os MORADORES DO
    //    BUNKER, desligados (as cartas, a TelaDosMoradores, continuam no código: voltam quando o bunker vier para o Lakewood);
    //  - à direita, o PERSONAGEM, sempre um novo (a aparência na tela de criar, ao entrar) e o JOGAR COM AMIGOS;
    //  - embaixo, o voltar, o erro da conexão e o "Criar o mundo", que hospeda direto (pelo Relay ou na rede local).
    // Desde 12/11/2026 (a parte 8 do "O jogo do mundo sorteado no Lakewood"), o jogo novo salva sozinho (a cada 5 minutos e
    // ao sair) e entra no Continuar do menu principal, que no Lakewood mostra só os jogos dele (com o personagem salvo).
    // Desde 25/11/2026 (o save do Dia 0), ele salva também no Dia 0 e na fuga, e o Continuar volta neles.
    public partial class MenuOnline
    {
        void TelaDoNovoJogoNoLakewood()
        {
            Quadro();
            EstilosNovos();
            Retangulo(new Rect(0, 0, Screen.width, Screen.height), Cores.Escuro);
            bool conectando = _sessao.Atual == SessaoOnline.Estado.Conectando;
            GUI.enabled = !conectando;
            GUI.Label(R(40, 16, 700, 50), "NOVO JOGO", _eTitulo);
            // o personagem é sempre um novo: o "entrar no mundo de um amigo" (o ParaEntrar) também não pega um salvo
            _indiceDoPersonagem = Personagens().Count;

            // à esquerda: o mundo
            const float L = 40, LW = 590, cx = L + 14, cw = LW - 28;
            Caixa(L, 78, LW, 290, "MUNDO");
            GUI.Label(R(cx, 116, cw, 30), "Condado de Lakewood", _eMundo);
            GUI.Label(R(cx, 148, cw, 22), "12 × 10 km em volta do reservatório da hidrelétrica", _eDetalheDoMundo);
            // o Dia 0 (22/11/2026, a etapa 4 do Docs/23): o começo na casa da família, na Woodcreek de antes
            bool d0 = World.MundoDeLakewood.ComecarPeloDiaZero;
            GUI.Label(R(cx, 182, cw, 64), d0
                ? "Você começa no Dia 0: na casa da família, na Woodcreek de antes do apocalipse, com o jornal na TV, $ 1.000 e as " +
                  "lojas abertas. As compras vão para a caçamba da caminhonete."
                : "Você começa no bunker da comunidade, cavado na encosta do lago, na ponta da Woodcreek, a vila do nordeste, com " +
                  "os carros e as motos na garagem.", _eDica);
            if (Botao(cx, 250, 300, 30, d0 ? "COMEÇAR PELO DIA 0: SIM" : "COMEÇAR PELO DIA 0: NÃO")) World.MundoDeLakewood.ComecarPeloDiaZero = !d0;
            // (25/11/2026, o save do Dia 0: antes, "o Dia 0 não é salvo")
            GUI.Label(R(cx, 288, cw, 70), "O jogo salva sozinho a cada 5 minutos e ao sair da partida, também no Dia 0 e na fuga, " +
                                           "e volta pelo Continuar do menu.", _eDica);

            // à esquerda: os moradores (22/11/2026, a etapa 2 do Docs/23: a comunidade do bunker já vem sorteada, sem as cartas)
            Caixa(L, 380, LW, 210, "MORADORES DO BUNKER", "A COMUNIDADE");
            GUI.Label(R(cx, 418, cw, 96), "O bunker não é de vocês: é de uma comunidade, com o líder que o construiu, um major " +
                                           "do Exército e uns seis sobreviventes. Vocês chegam como a nova equipe de exploradores.", _eDica);

            // à direita: o personagem e os amigos
            float px = 650, pw = 590, pcx = px + 14, pcw = pw - 28;
            Caixa(px, 78, pw, 206, "PERSONAGEM");
            GUI.Label(R(pcx, 114, pcw, 52), "NOVO PERSONAGEM", _eNome);
            GUI.Label(R(pcx, 178, pcw, 24), "Você cria a aparência ao entrar.", _eInfo);
            GUI.Label(R(pcx, 202, pcw, 22), "Ele é salvo com o jogo (no Continuar, escolha com quem voltar).", _eInfoApagado);
            PainelDosAmigos(650, 296, 590, 294, true);

            // embaixo: o voltar, o erro e o criar (direto, sem a tela das cartas)
            if (Botao(40, 644, 150, 44, "‹  VOLTAR")) Voltar();
            Erro();
            if (BotaoVermelho(880, 630, 360, 64, conectando ? "CONECTANDO..." : _redeLocal ? "CRIAR LOCAL" : "CRIAR O MUNDO", !conectando))
                ComecarNoLakewood();
            GUI.enabled = true;
        }

        // hospeda o Lakewood: um personagem novo (a tela de criar abre ao entrar) e um jogo salvo novo (12/11/2026, a parte 8:
        // ele salva sozinho a cada 5 minutos e ao sair, e entra no Continuar depois do primeiro salvamento)
        void ComecarNoLakewood()
        {
            EscolhaDoPersonagem.NovoPersonagem();
            Salvamento.Atual = MundoSalvo.NovoNoLakewood(ModoDeSalvar.Automatico);
            if (_redeLocal) _sessao.HospedarLocal();
            else _sessao.Hospedar();
        }
    }
}
