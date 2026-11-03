# 15 – Os ajustes da primeira partida com os amigos (03/11/2026)

O Pedro jogou com os amigos e trouxe o que achou. O plano foi aprovado, e ele pediu para fazer tudo de uma vez ("pode fazer
todos os passos a passos dos planos de fazer e corrigir do jeito que quiser, posteriormente eu vejo e avalio"). Esta é a
lista do que mudou, em cinco etapas, com o que foi conferido no Editor e as decisões que ficaram para ele confirmar.

Os detalhes técnicos estão no [`CLAUDE.md`](../CLAUDE.md) da raiz, na seção "Os ajustes da primeira partida".

## 1. Os bugs

- **O jogo fechando ao matar as pessoas do acampamento** (o pior deles): a mochila de quem morre arruma os itens numa grade,
  e um item sem tamanho (as mochilas do comerciante de roupas) nunca cabia. O jogo ficava preso para sempre.
  - Agora o item sem tamanho vai para o chão ao lado da mochila, e a arrumação tem um limite (com um aviso no log).
  - Conferido: os 7 comerciantes de um acampamento grande mortos, inclusive com a espingarda, sem travar.
- **Todos presos no "globo" dentro da casa:** os comerciantes e quem ficava sem munição corriam para o mesmo ponto da casa e se
  amontoavam.
  - Agora cada um vai para um cômodo do térreo; quem nasce em cima de um balcão desce para o chão.
  - O comerciante escondido atira em quem entra perto (10 m, à vista).
- **As 4 armas flutuando no fim da rua do Dia 0:** era a parede de armas do bunker, que só vai para o lugar quando o mundo fica
  pronto. Ela fica escondida (e sem o E) durante o Dia 0.
- **Travado na janela ao lado da TV (Dia 0):** antes da TV, quem sai da casa volta para dentro, e o pulo da janela brigava com
  isso. Agora a janela não deixa pular antes da hora ("Ainda não: esperem todos chegarem e liguem a TV da sala").

## 2. Os civis: armas brancas e menos saque

A pedido: "o loot dos NPC está muito alto" e "os civis com armas brancas, tacos, facas, pistolas, raramente uma
submetralhadora".

- **As armas dos grupos de busca:**
  - o líder com a pistola (às vezes a submetralhadora);
  - os outros: metade de arma branca (a faca, o bastão com pregos, o machado ou a lança), 40% a pistola e 10% a
    submetralhadora (no máximo uma por grupo);
  - nada de rifle, sniper ou espingarda.
- **A arma branca na mão:** o NPC chega perto pelos lados (de longe, para não ir reto na mira) e golpeia a menos do alcance
  da arma, com a animação dela (a mesma dos jogadores). O dano é o da arma (70% no jogador, como o tiro; uma vez e meia no
  zumbi).
- **O saque menor:**
  - o pente de 40% a 100% (antes, cheio);
  - em menos da metade deles, uma caixa com 3 a 12 balas (antes, de 2 a 4 caixas cheias);
  - mais comida (1 a 3) e bebida (1 a 2), remédio às vezes, materiais, pilhas.
- **Os outros civis** (os guardas e os moradores do acampamento, os dois do carro quebrado) ficam com as armas de antes, mas
  com o mesmo saque menor.

## 3. Os militares e a base provisória

A pedido: "os militares, mais resistentes (colete e capacete), com rifle, sniper, espingarda e munição pesada, só nas bases
militares". Como a base de verdade ainda não existe, vai uma **provisória**, simples, para os testes.

- **Onde:** nos lugares "Base Militar" do mapa (1 no pequeno, 2 no médio, 3 no grande), que até agora estavam vazios. O nome
  aparece no mapa (M), como antes.
- **A base:** um quadrado de 40 × 40 m no meio do lugar, com o portão para a estrada que chega lá.
  - a cerca de contêineres, alambrado e sacos de areia;
  - o portão entre duas barreiras de concreto, com a cancela levantada e a guarita;
  - 2 ou 3 barracas do exército, o caminhão e o blindado;
  - dois ninhos de sacos de areia nos cantos, as pilhas de caixas de munição, o mastro com a bandeira e dois postes de luz;
  - sem mexer no gerador: o chão não é aplainado, e cada peça acompanha o relevo.
- **Os militares:**
  - de 6 a 8 por base, sempre hostis;
  - aparecem com alguém a menos de 170 m e saem com todos a mais de 260 m;
  - ficam nos postos e na ronda; de longe (antes de o chão deles entrar no NavMesh), ficam parados, mas veem e atiram;
  - veem mais longe que os civis (90 m de dia; o de sniper, 150 m);
  - as armas: rifle (50%), espingarda (25%), sniper (15%) e submetralhadora (10%);
  - a roupa "Militar" do sorteio, com o colete tático e o capacete por cima.
- **Mais resistentes:**
  - vida 150;
  - o colete corta pela metade o dano no tronco;
  - com o capacete, o tiro na cabeça não mata de uma vez: vale 2,5 vezes.
- **O saque** (diminuído em 03/11/2026, à noite, a pedido: "o loot dos NPC militares também está alto"; antes, 8 mortos
  deixavam 634 balas em caixas, mais os pentes cheios):
  - para lutar, cada um tem uma reserva de 2 pentes que **não fica no saque**;
  - no corpo: a arma com o que sobrou no pente (de 50% a 100% no começo) e, em 40% deles, uma caixa com 30% a 60% de um
    pente;
  - a arma cai gasta (de 50% a 85%): passando de 70%, ela engasga às vezes;
  - a barra; às vezes o feijão (30%), a água (60%) e o remédio (30%); raramente o colete (8%) ou o capacete (6%) como item.
  - Conferido: 6 mortos deixaram 6 armas (5 que engasgam), 119 balas nos pentes e 55 em caixas (~29 por militar; antes, ~98).
- **Os mortos continuam mortos.** Três dias de jogo depois da última morte (com ninguém perto), chega outro grupo (com outras
  caras).
- **O save:** os mortos, a geração do grupo e o dia do reforço.

## 4. A tela de morte, as roupas e o fogo amigo

A pedido: "uma tela de morte melhor, com um botão de renascer depois de uns 5 segundos", "perder também as roupas" e "fogo
amigo como opção ao criar o mundo".

- **A tela de morte:**
  - o jogador fica caído: o corpo deitado para os outros, e os zumbis e os sobreviventes largam dele;
  - a tela escurece com "VOCÊ MORREU", do que ele morreu ("Mordido pelos zumbis.", "Baleado por um militar.", "Golpeado com
    o machado de um sobrevivente.", "De fome.", "Atingido por um raio.", "Morto por Fulano." no fogo amigo...) e o que ficou
    no chão;
  - o botão **Renascer** acende em 5 s (ou Enter); ele leva à cama do jogador, ou ao ponto de nascimento, com a vida cheia;
  - o ESC continua abrindo o menu por cima (para sair da partida).
- **As roupas:** as vestidas entram na mochila que fica no chão, com as cores; o personagem volta só com o corpo da criação.
- **No Dia 0** continua sem perder nada (a tela diz isso).
- **O fogo amigo:**
  - na tela de criar o mundo, junto do "Salvar o jogo": "Fogo amigo: Desligado / Ligado", desligado de começo;
  - fica no save do mundo e aparece no ESC ("Fogo amigo: ligado.");
  - ligado: o tiro e o golpe de um jogador em outro ferem, com o dano da arma (sem o tiro na cabeça); o anfitrião confere
    cada acerto; quem morre vê o nome de quem atirou;
  - desligado: como antes (o tiro para no outro, sem ferir).

## 5. O minimapa, os nomes, os pings e as anotações

A pedido: "um minimapa com os jogadores", "pings, marcações e anotações" e "o nome dos jogadores no mapa (só no mapa, por
enquanto)".

- **O minimapa:** no canto de cima, à direita (embaixo do contador de FPS), quadrado, com o norte para cima.
  - mostra uns 250 m em volta (o grande, 400 m), as setas dos jogadores (você em ciano), os pings e as marcas;
  - a tecla **N** muda o tamanho: pequeno, grande ou escondido (fica lembrado no computador);
  - some nas telas cheias (o inventário, o mapa, o menu, a tela de morte) e no Dia 0.
- **Os nomes:** no mapa grande (M), o nome de cada jogador ao lado da seta dele (o seu também, em ciano). O minimapa fica só
  com as setas.
- **Os pings (somem sozinhos):**
  - no mundo: o **botão do meio do mouse** marca o ponto para onde se olha (até 300 m);
  - no mapa grande: o **clique direito** e "Ping";
  - todos veem: um losango com o nome de quem marcou e a distância, por cima de tudo, mais o ponto nos dois mapas e um bipe;
  - duram 20 s; cada jogador tem até 3 (o quarto tira o mais velho).
- **As marcas e as anotações (ficam):**
  - no mapa grande, o clique direito abre o menu: escrever a anotação (opcional, até 40 letras) e escolher a cor (5 cores);
    sem texto, fica só o alfinete;
  - clicar num alfinete abre o dele: mudar o texto e a cor, ou apagar (qualquer um mexe);
  - o texto da anotação aparece ao passar o mouse;
  - são de todos (na rede), aparecem nos dois mapas e vão no save do mundo.

## Como testar (F1, aba "Gente")

- **Base militar:** "Ir à base" (a estrada na frente do portão, a 45 m do meio) e "Recomeçar a base" (todos vivos de novo).
- **A morte:** "Morrer agora" (a tela de morte) e "Fogo amigo: ligado/desligado" (muda o do mundo, e o save junto).
- Os grupos de busca, o acampamento e o carro quebrado continuam com os botões de antes.

## Conferido no Editor (03/11/2026, um jogador, semente 7, média)

- **Os bugs:** os 7 comerciantes mortos em 2 a 25 ms cada; 6 de 7 escondidos em cômodos diferentes; no Dia 0, nenhuma das 8
  armas da parede à vista, e o pulo da janela bloqueado (e liberado de dentro para dentro). 0 erros.
- **Os civis:** os 6 grupos de uma cidade grande: 10 brancas, 27 pistolas e 5 submetralhadoras (os números mudaram depois:
  agora metade dos não líderes tem branca); 16 caixas com 111 balas em 42 pessoas.
  - a luta: o machado e a faca chegaram a 1,5 m e golpearam (as fotos do golpe); alguns de pistola sem munição fugiram;
  - os 5 mortos deixaram 2 armas de fogo e 3 balas em caixas;
  - o golpe tirava ~30 de vida por segundo com dois atacando; ficou ~10 cada.
- **A base:** 2 bases no médio (8 e 6 militares), montada em 18 ms.
  - todos com o colete e o capacete (as fotos), vida 150;
  - o tiro de 50 na cabeça deixou 25 de vida (vivo); o de 40 no tronco tirou 20;
  - 20 s de tiroteio: o jogador levou 175 de dano (curado o tempo todo);
  - os 8 mortos: 8 armas, 634 balas em caixas, 2 capacetes;
  - o save dos mortos voltou igual, e ninguém apareceu de novo; longe, 3 dias depois, o reforço chegou (8 novos);
  - fotos de dia e de noite, com as luzes. 0 erros.
- **A morte:** caiu (a tela, o corpo deitado, os zumbis largam); a mochila no chão com as 3 roupas do começo, a pistola e a
  água; o botão antes de 5 s não fez nada; renasceu com 100 de vida e sem roupa. 0 erros.
- **O minimapa e as marcas:** 4 pings, ficaram 3; a anotação cortada em 40 letras; editar, apagar e o save das marcas; os
  pings sumiram em 20 s; o mapa grande com o nome e o alfinete (as capturas). 0 erros.
- **A regressão:** o Dia 0 com as peças novas, o fogo amigo pelo pedido e pelo F1, o save novo e um save antigo (sem os campos
  novos) pelo JSON.
- **Falta (precisa de dois jogadores):** o fogo amigo de verdade (o tiro e o golpe de uma máquina na outra, o nome na tela de
  morte), os pings e as marcas chegando na outra máquina, e o corpo deitado visto pelo outro.

## Decisões minhas para o Pedro confirmar

- **Os civis:** metade de arma branca entre os que não são o líder; o golpe com 70% do dano da arma no jogador, a cada 1,2 a
  2 s.
- **Os militares:** vida 150; o colete pela metade no tronco; o capacete faz o tiro na cabeça valer 2,5 vezes; de 6 a 8 por
  base; o reforço em 3 dias; o saque grande (2 ou 3 caixas cheias de munição: a base é perigosa e compensa).
- **A base provisória:** o quadrado de 40 m no meio do lugar, com o portão para a estrada; os militares parados até o NavMesh
  chegar neles (atiram de onde estão).
- **A morte:** 5 s até o botão; a roupa vai para a mochila; renasce sem roupa (só com o corpo da criação).
- **O fogo amigo:** começa desligado; sem tiro na cabeça entre jogadores; o dano é o da arma (a espingarda de perto mata de
  uma vez).
- **O minimapa:** embaixo do FPS, 250 m (o grande, 400 m), a tecla N; o ping no botão do meio e no clique direito do mapa;
  até 200 marcas no mundo.

## Depois do primeiro teste do Pedro (03/11/2026, à noite)

O resto ficou bom; ele achou três coisas.

- **A janela do quarto ainda deixava pular (Dia 0).** O "dentro da casa" do lobby era a caixa da planta inteira, e a garagem
  coberta esticava essa caixa por todo o lado da casa. Na casa do mundo dele (semente 156653), a garagem é mais curta que a
  casa: a faixa ao lado dela, do lado de fora da janela do quarto, contava como dentro. Dava para pular a janela para lá, e o
  lobby não puxava de volta. Agora o "dentro" são os cômodos da planta (com 20 cm de folga, para a porta entre dois cômodos),
  mais a garagem coberta só no comprimento dela.
  - Conferido nessa casa: as 28 janelas de fora bloqueadas antes da TV (antes, 24: as 4 do quarto passavam); quem fica na
    faixa de fora é puxado para dentro; os 4 nascimentos dentro.
- **O nome do jogador no mapa (M) longe da seta e "mudando" com o zoom.** O nome saía centrado numa caixa de 160 px à
  direita da seta (uns 90 px longe dela), na letra dos nomes dos lugares; e os nomes que aparecem com o zoom (os bairros,
  os lugares menores) passavam por cima. Agora o nome fica colado na seta, numa etiqueta escura, na cor do jogador (você em
  ciano), e os nomes dos lugares desviam dele. Conferido em 4 zooms (as capturas).
- **As quedas de FPS ao começar no bunker.** Medido com o Profiler da Unity (o mundo grande da semente 156653, começando no
  bunker, duas vezes):
  - **os cortes dos zumbis** (o aquecimento do `ZombieManager`): 21 modelos, 504 passos, ~10 s de trabalho; os 9 especiais
    (até 24 mil vértices) no fim da fila. O resto que não acabava no carregamento caía no jogo: um quadro de 627 ms (um corte
    de 342 ms mais 245 ms de coleta do lixo) e uns 2,5 s entre 30 e 90 FPS. Agora eles só andam com a tela coberta (o
    carregamento, a tela de criar o personagem, o cinema), vários por quadro, e **a tela de carregamento espera por eles**
    ("Preparando os zumbis", com a porcentagem). No Dia 0, eles esperam o carregamento do mundo (antes, engasgavam a rua);
  - **as árvores e os objetos da estrada** eram montados em volta da câmera do menu (a origem do mapa) durante o
    carregamento, e tudo em volta do bunker começava de uma vez quando o jogador nascia. Agora, em volta de onde ele vai
    nascer;
  - **a tela de carregamento fica mais 0,8 s** depois de o jogador nascer: o primeiro desenho do bunker (a sombra de tudo em
    volta, ~110 ms) acontece por trás dela;
  - **a imagem do mapa:** os mipmaps são feitos na outra thread, junto com a pintura (antes, ~30 ms num quadro do jogo).
  - **Depois das correções** (a mesma medição): os cortes acabaram durante a geração do mundo (504 passos em 11 s), e o
    carregamento não ficou mais longo (12,3 s). Do fim da tela em diante, em 45 s de jogo, só 1 quadro passou de 50 ms (17 ms
    dele eram do próprio Editor) e 14 passaram de 33 ms; antes, quadros de 627, 226, 97 e 85 ms. O primeiro 1,5 s ainda fica
    perto de 45 FPS (o chão em volta terminando de montar); depois, de 105 a 117 FPS no Editor.
  - **O fim do Dia 0:** na casa, os cortes ficam parados (1%); depois da porta do abrigo, andam durante a geração. No mapa
    médio, a tela esperou 1 a 2 s a mais ("Preparando os zumbis") e o mundo ficou pronto em 8,6 s, com os cortes prontos.
  - Medidos uma vez só (e não repetiram): 595 ms nas árvores e 424 ms no desenho da interface. São custos de primeira vez do
    Editor (os shaders e a fonte).
  - Fica de olho (não mexi): o bunker tem ~45 lâmpadas acesas, todas sem sombra, mas as do andar de baixo iluminam o de cima
    através da laje (custo de placa de vídeo dentro do bunker).

## O crash do Editor (03/11/2026, à noite)

- **O que foi:** falta de memória. O log da Unity (`Logs/Editor.log`, "Crash!!!") mostra o alocador nativo falhando
  (`mi_os_prim_alloc_aligned`) quando o terreno montava as casas de um bloco, num mundo grande (semente 831896), depois de
  horas de Unity aberta com muitas partidas. Não foi um erro do jogo em si.
- **Medido:** 3 partidas seguidas no mesmo Play (o mapa médio) não deixam a memória crescer (a Unity volta a ~1,9 GB depois
  de cada saída; o processo fica em ~9,8 a 10 GB). Mas, durante as partidas, o Windows ficou com só 0,7 a 1 GB livres (a RAM
  de 15,8 GB mais o arquivo de paginação): o Editor sozinho tem ~10 GB, e dois importadores de assets da Unity ficam parados
  de reserva com 1,8 GB cada.
- **Corrigido** (uma revisão do código atrás do que crescia e não voltava):
  - **as roupas dos NPCs**, o maior: cada NPC sorteado novo guardava para sempre uma cópia do corpo e das peças sem as faces
    cobertas (~2 MB). Agora cada cópia conta quem a usa; sem ninguém, espera numa fila de 24 (o NPC que some e volta acha a
    dele pronta) e depois sai. Medido: 40 NPCs diferentes por rodada, 3 rodadas; as malhas sobem a ~95 e voltam a 26 quando
    eles somem (antes somavam ~70 MB a cada rodada, sem parar);
  - **o NavMesh** em volta de cada jogador ficava na memória ao fim da partida e quando alguém saía (agora sai junto);
  - o cache da planta da base militar guardava o mundo inteiro de cada partida da sessão (agora só o de agora);
  - pequenos: o cerco dos zumbis guardava quem morreu cercado; os passos guardavam o chão dos blocos que já sumiram; a
    planta do acampamento não olhava o tamanho do mundo (a mesma semente em outro tamanho pegava a planta errada).
- **Também:** o F3 (Itens) dava um erro ao clicar em "Roupas" (aparecia no log); corrigido.
- **Para não cair de novo:** fechar e abrir a Unity de tempos em tempos (o heap do C# dela nunca devolve memória ao sistema),
  fechar o que pesa durante os testes (a Steam, o Blender), aumentar o arquivo de paginação do Windows, e, na Unity,
  Project Settings > Editor > "Standby Import Worker Count" em 0 (os dois importadores parados saem).

## Fica para a próxima fase

- **A emboscada:** os civis escondidos nos prédios das cidades, um em cada andar, atirando de cima.
- **Os NPCs dirigindo carros**, junto com ela.
- **Os acampamentos e as bases rivais "de verdade"** (do Gustavo).
- **A base militar de verdade**, no lugar da provisória.
