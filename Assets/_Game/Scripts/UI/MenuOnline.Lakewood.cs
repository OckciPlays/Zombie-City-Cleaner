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
            GUI.Label(R(cx, 184, cw, 70), "Você começa numa casa na beira do lago, na ponta da Woodcreek, a vila do nordeste, " +
                                           "com as armas nas mesas e os carros no terreiro.", _eDica);
            GUI.Label(R(cx, 266, cw, 70), "O jogo salva sozinho a cada 5 minutos e ao sair da partida, e volta pelo Continuar " +
                                           "do menu.", _eDica);

            // à esquerda: os moradores (desligados)
            Caixa(L, 380, LW, 210, "MORADORES DO BUNKER", "DESLIGADOS");
            GUI.Label(R(cx, 418, cw, 70), "A escolha dos moradores fica desligada por enquanto: o Lakewood ainda não tem o " +
                                           "bunker.", _eDica);

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
