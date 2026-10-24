# Zombie City Cleaner: documentação

Jogo de sobrevivência online (até 4 jogadores, cada um é um membro da família) numa cidade infestada de zumbis. É low-poly, com cores sólidas e o clima sombrio do Silent Hill: neblina pesada de dia e noites escuras com luz de poste. É feito na Unity 6 (URP), com a arte no Blender.

Esta pasta explica o que o jogo vai ser, o que já está pronto e como trabalhar no projeto.

| Arquivo | O que tem |
|---|---|
| [01 – Visão do jogo](01-Visao-do-Jogo.md) | o conceito completo, as decisões já tomadas, a direção de arte e a paleta |
| [02 – Plano da demo](02-Plano-da-Demo.md) | a demo em partes (0 a 9), o que está feito e o que falta, e as fases depois dela |
| [03 – Histórico](03-Historico.md) | o que foi feito em cada parte, com os commits, as mudanças de rumo e as medições |
| [04 – Arquitetura](04-Arquitetura.md) | como o projeto Unity está organizado: a cena, os sistemas, a rede, os zumbis, as armas e os scripts de montagem |
| [05 – Arte e pipeline](05-Arte-e-Pipeline.md) | os modelos do Blender (feitos por script) e como eles chegam na Unity |
| [06 – Como colaborar](06-Como-Colaborar.md) | o git, as regras do projeto, como testar (inclusive online) e o checklist antes do commit |
| [07 – Ideias do modo história](07-Ideias-do-Modo-Historia.md) | as ideias para depois da demo (Dia 0, escolhas e finais, o carro, a floresta e os dois acampamentos), divididas em frentes de trabalho |

A referência técnica mais detalhada é o [`CLAUDE.md`](../CLAUDE.md) da raiz do repositório. Ele é escrito para o assistente de IA que trabalha no projeto, mas é um texto comum, em português, e descreve cada sistema em detalhe. Esta pasta é o resumo para gente.

## Estado atual (23/10/2026)

- **Feito:** as Partes 0 a 6, a Parte 8 (a arte final, que veio antes da 7 a pedido), os "ajustes antes da Parte 7" (tiro novo, 4 modelos de zumbi com ragdoll, 4 armas e o suporte de armas no bunker), a ambientação da cidade e, a pedido, as armas rebalanceadas, a sniper com luneta e o desmembramento (branch `feature/armas-sniper`) e o bunker maior (o salão alto, 4 quartos, o depósito, o painel de armas, a porta que isola o som e os interruptores; esperando a aprovação).
- **Próximo:** a Parte 7 (o bunker, o saque e o dia). Depois, a Parte 9 (o fechamento da demo).
- **O que dá para fazer hoje:**
  - hospedar uma partida (pelo Relay da Unity, com código, ou em rede local) e jogar com até 4 pessoas;
  - nascer no bunker embaixo da casa da família e sair para a cidade (5 casas e um prédio de 4 andares, onde dá para entrar);
  - viver o ciclo de dia e noite (um dia inteiro leva 24 minutos);
  - enfrentar 258 zumbis que vagam, percebem, perseguem, atacam e desistem;
  - usar 5 armas (pistola, rifle, submetralhadora, espingarda e sniper com luneta), trocadas no painel de armas do bunker, e arrancar braços e pernas dos zumbis.

## Começando

1. **Instale:**
   - a Unity **6000.6.5f1** pelo Unity Hub (a mesma versão evita reimportar tudo);
   - o Git com o **Git LFS**. Os modelos, texturas e sons vão pelo LFS (veja o `.gitattributes`).
2. **Clone a branch da demo:**

   ```bash
   git lfs install
   git clone https://github.com/OckciPlays/Zombie-City-Cleaner.git
   cd Zombie-City-Cleaner
   git checkout feature/demo-cidade
   ```

3. **Abra a pasta no Unity Hub.** A primeira abertura importa tudo e demora alguns minutos.
4. **Abra a cena** `Assets/_Game/Scenes/Demo_Cidade.unity` (a única da build) e dê Play.
5. **No menu do jogo:**
   - "Hospedar local" joga sem internet (IP direto, porta 7777);
   - "Hospedar partida" cria a partida pelo Relay e mostra um código; quem vai entrar digita o código e clica em "Entrar".
   - O Relay usa os serviços da Unity do projeto ("Zombie City Cleaner" na Unity Cloud). Se der erro de serviços na sua máquina, peça para ser adicionado ao projeto na Unity Cloud e, enquanto isso, use o modo local.

## Controles

| Tecla | Ação |
|---|---|
| W A S D | andar |
| Shift | correr |
| Espaço | pular |
| Mouse | olhar |
| Botão esquerdo | atirar (o rifle e a submetralhadora atiram com o botão apertado) |
| Botão direito | mirar (aproxima a câmera) |
| R | recarregar (a espingarda põe um cartucho por vez) |
| F | lanterna |
| E | olhando de perto: pegar uma arma do painel do bunker, abrir ou fechar a porta do bunker, acender ou apagar a luz no interruptor |
| ESC | menu (o mundo continua rodando, é online) |

Controle (gamepad) também funciona: as ações estão em `Assets/InputSystem_Actions.inputactions`.

**Teclas de teste:**

- **F3:** no anfitrião, os zumbis por nível de LOD e o tráfego de rede; num cliente, as cópias de zumbis que ele recebe;
- **F9** (só no anfitrião): o benchmark dos zumbis (troca a população por 50, 100, 250 e 500 e mede);
- **Page Up / Page Down** (só no anfitrião): avança ou volta 1 hora do dia.
