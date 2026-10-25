# 03 – Histórico

O que foi feito em cada parte, na ordem, com o commit de cada uma (branch `feature/demo-cidade`). Os números de desempenho estão em [`Benchmarks/Benchmark_Zumbis.md`](../Benchmarks/Benchmark_Zumbis.md).

## Antes da demo (21 a 23/10/2026)

- `8d13f38` Check-in inicial.
- `aee8245` e `ee01bfd`: um protótipo de tiro em primeira pessoa com visual de PS1 e um elevador infinito num prédio, com a SCAR como arma. Ele foi deixado de lado no recomeço e está guardado na branch `feature/prototipo-fps-zumbis`.
- **Recomeço (23/10/2026):** o conceito mudou para a família no bunker (veja a [visão do jogo](01-Visao-do-Jogo.md)), e o visual de PS1 foi abandonado pelo low-poly de cores sólidas.

## Parte 0 e Parte 1 – `5c380c6`

- A fundação: a branch da demo, as pastas, a cena com noite, neblina e pós-processamento, e os pacotes de rede.
- O zumbi `NovoZombie` com esqueleto, pesos feitos por regras para cada peça da malha, cortes de desmembramento com tampas de carne e as animações `Idle`, `Walk` e `Chase`.

## Parte 2 – `0e44d5b`

- O jogador quadradão (1,80 m) com as animações do corpo (parado, andar, correr, mirar e atirar), os braços de primeira pessoa e a pistola com as animações deles (parado, andar, correr, tiro, gatilho sem bala e recarga).
- As 4 variações de cor da família pela paleta.

## Parte 3 – `ffc2757`

- Online com o Relay: hospedar, código e entrar, mais o modo local por IP.
- O jogador de rede: a câmera do mundo e a câmera dos braços por cima (os braços não atravessam paredes), o corpo dos outros, a lanterna no peito e o tiro pela rede.
- Os testes com uma build e um "robô" (`-bot`) que entra na partida, anda, atira e registra o que vê.

## Parte 4 – `83a3080`

- A cidade montada por código (`MontagemDaCidade`): ruas em grade, 5 casas com cômodos, o prédio de 4 andares com escada em U, o bunker embaixo da casa da família, carros, cercas, árvores e o anel de 320 pinheiros.
- O clima: a neblina de altura em tela cheia (shader próprio), partículas no ar, os volumes de dia e de noite, 28 postes com luz de sódio (alguns piscam, alguns estão quebrados) e cones de luz falsos.
- O ciclo do dia controlado pelo anfitrião (começa às 21 h; um dia leva 24 minutos).

## Parte 5 – `d29c4cd`

- O `ZombieManager`: IA em fatias com orçamento por quadro, LOD de IA (perto, médio e longe) com histerese, pool e pontos de horda.
- A percepção: visão que depende da luz (curta à noite, longa com a lanterna ligada), passos correndo, contato e o barulho do tiro.
- A rede dos zumbis: eles não são objetos de rede; o anfitrião manda a cada cliente só os zumbis perto dele (12 bytes cada; com o desmembramento da Parte 6 foram 15, e hoje são 13), e o cliente interpola.
- O benchmark com 50 a 500 zumbis.

## Parte 6 – `12bec8d`

- O combate: hitboxes nos ossos, vida por região, a cabeça que explode, sangue, manchas e furos de tiro (decals).
- O desmembramento completo: braços e pernas caíam, e o zumbi sem perna rastejava. (Saiu depois, nos ajustes antes da Parte 7.)

## Parte 8 – `f676a81` (antes da Parte 7, a pedido)

- A arte final, toda feita por script no Blender, com um material único e a paleta:
  - os objetos de rua (postes, 7 carros, cercas, barreiras, lixeiras, placas, hidrantes, bancos, árvores e pinheiros);
  - as 5 casas e o prédio por dentro e por fora, com malhas de colisão próprias;
  - o bunker e 23 tipos de móveis (os de saque com nome próprio, para a Parte 7);
  - o chão em 25 pedaços (ruas, calçadas, grama, entulho, praça e estacionamento).
- A oclusão assada e a neblina rala dentro das casas e do prédio.
- **Decidido:** a luz continua em tempo real; LOD não foi preciso.
- **Números (Editor, noite):** de ~2.460 para 940 a 1.100 draw calls; GPU de 2,4 a 3,0 ms; quadro de 4,3 a 6,2 ms (preso na CPU).

## Ajustes antes da Parte 7 – `0d32276`

Pedidos depois da Parte 8, em 3 fatias aprovadas uma de cada vez.

### Fatia 1: som e nascimento

- O tiro novo, gerado por código em camadas (estalo, soco grave, corpo, ferrolho, cauda e dois rebatimentos), em 3 variações, abafado com a distância.
- Saíram o vento, os gemidos distantes e a sirene. Ficou o eco.
- Os jogadores nascem (e voltam quando caem) na sala comum do bunker.

### Fatia 2: zumbis

- 4 modelos novos (`Zombie1`, `ZombieHomem2`, `ZombieMulher`, `ZombieMulherLoira`), montados por script no Blender com o esqueleto e as 5 animações do zumbi antigo, cada um com as juntas nas medidas dele.
- Sem desmembramento: só a cabeça explode (a malha troca por uma sem cabeça, com a tampa de carne no pescoço).
- A morte com ragdoll: o zumbi some e um corpo com física cai no lugar, empurrado pelo tiro. Os corpos vêm de um pool (12 por modelo) e somem afundando.
- Corrigidos depois dos testes: o corpo que às vezes ia para o meio do mapa, o clarão do corpo sendo puxado ao ser reaproveitado e a palma da mão de lado com o braço levantado.

### Fatia 3: armas

- 4 armas (pistola, rifle, submetralhadora e espingarda), feitas por script no Blender seguindo as imagens de referência, com braços de luva sem dedos.
- Cada arma tem os seus braços de primeira pessoa e as suas animações (parada, andando, correndo, tiro, gatilho sem bala, recarga e sacar), feitas por código com IK. A espingarda bombeia no tiro e recarrega cartucho a cartucho.
- O corpo (o que os outros veem) segura cada arma: as armas longas têm animações próprias, com o tronco girado e a coronha no ombro.
- No jogo: pente, cadência, dano, dispersão e coice de cada arma; rifle e submetralhadora automáticos; a espingarda com 8 chumbos; um som de tiro para cada uma.
- O suporte de armas na parede do bunker, com 4 vagas: E troca a arma da mão pela da vaga, e o anfitrião decide.
- Testado no Editor e com o robô pela rede.

### Depois dos testes do usuário

- O cano das armas passou a apontar para a mira (antes ficava girado para o lado).
- A lanterna não ilumina mais os braços de primeira pessoa (eles ficavam brancos dentro do facho), e a arma do corpo do próprio jogador não faz mais sombra no facho.
- O dobro de zumbis: de 129 para 258, nos mesmos 29 pontos de horda.
- As montagens passaram a forçar a reimportação dos prefabs (o controlador de animação recriado deixava os zumbis em pose T).

## Ambientação da cidade, fatia 1: a rua abandonada (em teste, branch `feature/ambientacao-teste`)

Pedido de teste: mais clima de começo de apocalipse, sem pesar (as hordas vêm aí). Feita só com as peças que já existiam, pela montagem da cidade (grupo "Abandono").

- 12 carros abandonados: a fila de quem tentou fugir nas saídas norte e leste, uma batida no cruzamento do meio, um capotado, um que parou no poste e a picape de uma blitz.
- A blitz abandonada na avenida sul: barreiras abertas, o cavalete e os cones derrubados.
- Lixo nas calçadas: sacos, caixas, tambores e uma lixeira caída.
- **Medido** (no PC do Pedro, Editor em 2560 × 1440, à noite, na vista de sempre, 2 rodadas antes e 2 depois): ~50 a 60 draw calls a mais (+5 a 6%), o mesmo número de SetPass e a GPU dentro da variação entre rodadas (8,3 a 9,1 ms depois, 8,4 a 9,9 ms antes). O quadro ficou travado em ~13,4 ms nas duas medições (parece o limite de 75 Hz da tela), então ele não serve para comparar aqui.

## Ambientação, fatia 2: marcas no chão e sinais de vida, e a lanterna nova (em teste, mesma branch)

- **A lanterna, corrigida:** o facho agora converge no ponto da mira, sem atraso para quem a usa. Antes ela apontava 9° abaixo do olhar e seguia a câmera com atraso, então o centro da luz caía longe da mira (uns 2 m abaixo, a 10 m). Os outros jogadores veem o facho seguir o olhar de quem a usa.
- **A lanterna, melhorada:** um facho de lanterna de verdade (o centro forte, um anel leve e a borda mais fraca e quente, feito por código) e uma falha rápida ao ligar.
- **Marcas no chão:** 23 decals com um material só: freadas atrás das batidas, óleo embaixo dos carros batidos, sangue seco arrastado e papéis espalhados.
- **Sinais de vida:** duas TVs ligadas (a casa 2 e um apartamento do 2º andar), duas lâmpadas esquecidas acesas (a casa 4 e um apartamento do 3º andar) e o pisca-alerta em dois carros abandonados.
- **Medido** (mesma vista, à noite, 2 rodadas limpas, comparando com a fatia 1): draw calls iguais (914 a 1.043), 2 a 3 SetPass a mais (67 a 68 contra 64 a 65) e a GPU dentro da variação entre rodadas (8,5 a 9,4 ms contra 8,3 a 9,1 ms). Física, navegação e animação iguais.
  - O pisca-alerta mudava o brilho das lanternas por MaterialPropertyBlock, o que as tirava do SRP Batcher: ~10 SetPass a mais. Agora ele liga e desliga o renderer.
  - O `_EMISSION` do material Lit some depois que o material carrega (o arquivo ainda o tem). Por isso o pisca usa Unlit com a cor em HDR. A lente dos postes (`Lampada`) tem o mesmo problema e não brilha pela emissão: fica para depois.
- **Testado com dois jogadores** (o Editor hospedando e a build com o robô, `-entrar-local 127.0.0.1 -bot`): em 25 s, com o robô girando e olhando para cima e para baixo, o facho dele, visto do anfitrião, ficou em média a 0,36° da mira dele (no máximo 8°, nas viradas rápidas, pela suavização); a conta antiga daria 27°. As 5 vezes que ele ligou e desligou a lanterna chegaram, e a luz nunca ficou diferente do estado recebido.

## Armas rebalanceadas, a sniper e o desmembramento (antes da Parte 7, a pedido; branch `feature/armas-sniper`)

- **O dano das armas, refeito:** cada arma tem dano no tronco e dano nos membros. A vida dos zumbis continua em tiros de pistola: o corpo aguenta 6 e cada braço ou perna, 3.
  - Pistola e submetralhadora: 1 por tiro (6 no tronco, 3 num membro); a submetralhadora era mais fraca que a pistola por tiro (0,8) e perdia para o rifle em tudo. Rifle: 1,5 (4 no tronco, 2 num membro), a arma de grau acima.
  - Espingarda: o dano muda com a distância e os chumbos contam juntos (um acerto por zumbi, na parte do chumbo do meio da mira). De perto (até 5 m), mata com um tiro no corpo, arranca um membro e joga o corpo para trás; na média (até 15 m), dois tiros; de longe, pouco. Um chumbo perdido na cabeça só explode a cabeça de perto (antes, a 10 m, matava com um tiro).
- **A sniper:** um fuzil de ferrolho de caça com luneta, feito no Blender (scripts em `Blender/`) sobre os braços do rifle, com a animação do tiro puxando o ferrolho (1,25 s). Cinco no pente; um tiro no corpo mata e um num membro o arranca. Sem a luneta, espalha 4°.
  - **A luneta:** o botão direito fecha a câmera para 12°, esconde os braços e desenha a luneta (o retículo com um ponto vermelho), com som de encostar o olho; o mouse fica mais lento. Para quem mira, a neblina afina (sem isso, a 60 m não se via nada) e os zumbis na linha da luneta contam como perto: aparecem, ganham as hitboxes (a cabeça também) e vão pela rede. Testado: a 70 m, a cabeça explode com um tiro; a 60 m, um tiro no corpo mata.
  - Ficava na mesa da sala comum do bunker (a 5ª vaga do suporte); com o bunker maior, foi para o painel de armas.
- **Desmembramento, de volta** (tinha saído com os modelos novos): cada braço e cada perna cai no cotovelo ou no joelho (tiro no antebraço ou na canela) ou inteiro (no braço ou na coxa), com o pedaço voando, o toco jorrando e uma tampa de carne. A malha cortada é feita na hora e guardada. Sem uma perna, o zumbi manca; sem as duas, cai e rasteja. As animações de cair e rastejar vieram do zumbi antigo da Parte 6, transferidas para os 4 modelos; as de mancar saíram do andar de cada um.
  - Medido: pistola e submetralhadora arrancam um membro com 3 tiros; rifle e espingarda a 9 m, com 2; sniper e espingarda de perto, com 1. Uma bala que atravessa o antebraço e pega o braço logo atrás contava duas vezes (o braço caía com 2 tiros): agora conta uma.
  - Cortar uma malha leva de 3 a 15 ms na primeira vez (o pedaço, de 6 a 20 ms): os cortes comuns ficam prontos no começo da partida, um por quadro.
- **Rede:** o estado de cada zumbi ganhou 1 byte (os membros perdidos), 14 por zumbi. O acerto leva o membro e o arremesso no byte da região. Testado só no Editor (o anfitrião); o teste com dois jogadores (a build com o robô) ficou para depois.
- **Medido** (2 rodadas, mesma vista, à noite, contra as últimas da ambientação; veja o fim de [`Benchmarks/Benchmark_Zumbis.md`](../Benchmarks/Benchmark_Zumbis.md)): a GPU de 8,4 a 9,2 ms (antes 8,5 a 9,4), 912 a 1.078 draw calls (antes 914 a 1.043, a mesma faixa com a população sorteada), os mesmos 67 a 68 SetPass, e a IA, a animação, a navegação e a física iguais. O nível de detalhe pela luneta não custou nada que apareça na medida.

## O bunker maior (24/10/2026, a pedido, antes da Parte 7)

- **A planta nova** (desenhada pelo usuário): o salão de 10 × 14 m com o pé-direito dobrado (5,3 m), 4 quartos (um para cada um da família) e o depósito, cada um no fim de um corredor curto; quartos e depósito com 3 m de pé-direito. A escada ficou mais funda (dois lances com um patamar no meio) e passa por baixo do quintal e de parte da rua.
- **Os móveis:** a mesa comprida com 6 cadeiras no meio do salão, sofá, poltrona e prateleiras; em cada quarto, uma cama de solteiro (cada uma com o cobertor de uma cor), criado-mudo, armário, cômoda e tapete; no depósito, armários de aço, prateleiras, tambores e a mesa onde a comida vai ser empilhada (Parte 7).
- **O painel de armas:** no lugar do suporte de 4 prateleiras, um painel de ripas na parede do fundo do salão, de frente para a porta, com uma bancada embaixo. Cada arma tem o seu lugar (as 4 longas e 4 pistolas); a sniper saiu da mesa. Ao pegar uma arma, a da mão volta para o lugar dela; quem sai da partida devolve a arma.
- **A porta blindada abre e fecha** (E), para todos, com o volante, as trancas e a batida. Fechada, ela isola o bunker: o som não passa de um lado para o outro, e os zumbis lá fora não ouvem os tiros nem sentem quem está dentro. Ela não fecha com alguém no caminho.
- **As luzes:** interruptores em cada cômodo, no salão e na escada (no vestíbulo e na boca, na cozinha), sincronizados na rede.
- **Sem neblina e sem as partículas do ar** dentro do bunker.
- **O som da porta, refeito depois do primeiro teste:** o metal virou físico (as ressonâncias de uma folha de aço grossa e das peças da tranca), o rangido virou atrito de dobradiça que acompanha a velocidade, e ao fechar ela ganha velocidade e bate de verdade no batente, com o volante trancando depois. Prévias em `Sons/Previas/` da pasta de arte.
- **E serve para tudo:** a mira procura qualquer coisa que se usa (arma, porta, interruptor), e a dica ganhou sombra para ler na frente de coisa clara.
- **Testado no Editor, com um jogador:** a troca das armas pelo lugar de cada uma, a porta (abre, fecha, não fecha com alguém no caminho, isola só fechada), as luzes, o caminho do NavMesh da cozinha até cada cômodo e a neblina zerada lá embaixo. O teste com dois jogadores (a build com o robô) ficou para depois.

## Correção: o foco do mouse (24/10/2026)

- **O bug:** ao começar a partida, o mouse não ficava preso no jogo, e o clique para prendê-lo dava um tiro (o movimento prendia o mouse e a arma atirava no mesmo quadro).
- **Agora:** o menu prende o mouse sozinho quando a partida começa e quando ele fecha. Se o mouse se soltar (alt-tab), o clique que o prende de novo não atira nem mira: os comandos só voltam depois de soltar o botão.
- Testado no Editor com cliques simulados: a partida começa com o mouse preso, o clique de voltar não gasta bala (nem chegando no mesmo quadro em que o mouse se soltou), o clique seguinte atira, e o ESC abre e fecha o menu soltando e prendendo o mouse.

## O começo do saque: inventário, comida e água, fome e sede (24/10/2026, a pedido)

- **Inventário de 3 lugares** (embaixo, no meio, com ícones renderizados no Blender): até 3 armas, latas de comida ou garrafas de água. A roda do mouse passa pelos lugares ocupados, 1, 2 e 3 escolhem, G larga (no chão, ou na mesa para onde a mira aponta) e E pega. Cada lugar guarda o pente da arma dele. O painel de armas passou a pegar para o inventário, trocar com a arma da mão quando está cheio e guardar a arma da mão na vaga dela.
- **A lata e a garrafa** foram modeladas no Blender (`itens.py`), com os braços de primeira pessoa segurando cada uma e as animações (parado, andando, correndo, sacar, e comer ou beber: o item vem até a boca e deita). 34 delas ficam nos tampos dos móveis das casas e dos apartamentos, e 2 na mesa do depósito do bunker. Os itens no chão vão pela rede numa lista só.
- **Fome e sede:** duas barras que esvaziam em 40 e 25 minutos (mais rápido correndo). Abaixo de 25, a vida para de voltar sozinha; vazias, tiram vida. A lata enche a fome (e um pouco da sede), a garrafa, a sede.
- **Passos:** sons gerados por código para concreto, madeira e grama, pelo chão embaixo do pé; os dos outros jogadores saem no pé deles. Os primeiros saíram graves demais (um "bum" em vez do "toc" da sola) e foram refeitos, medidos por bandas de frequência.
- **Opções no ESC** (e no menu do começo): o volume, o limite de FPS e o contador de FPS, gravados no computador.
- **A arma na corrida:** as armas longas (e a sniper, que usa as animações do rifle) corriam com o cano para baixo e só uma ponta aparecia no meio de baixo da tela; agora atravessam a tela inteiras. No teste apareceu um bug mais antigo: a camada de ações dos braços segurava a pose do fim do sacar, e os braços nunca respiravam, balançavam nem baixavam correndo. O código passou a tirar o peso dela quando não há ação.
- **Testado no Editor, com um jogador:** pegar, trocar, largar, comer, beber, o painel com o inventário, a roda, as 5 armas correndo e o painel de opções. O teste com dois jogadores ficou para depois.

### Depois do primeiro teste do usuário (24/10/2026)

- **A porta do bunker abre em silêncio;** só o fechar e a batida têm som. No teste seguinte, saiu também o de fechar: a porta não faz som nenhum.
- **A roda do mouse passa pela mão vazia** (os punhos), se houver um lugar vazio: uma vez só na volta, pulando os outros vazios.
- **Os passos dentro das casas:** cada cômodo passou a ter o som do piso que ele tem no modelo (taco, carpete ou o piso frio da cozinha; antes era tudo madeira), e os quartos do bunker soam como taco. A madeira foi refeita (era um "tum" grave; agora é o "toc" seco da tábua, com corpo) e entrou o carpete, abafado.
- **O jogo ficava mudo depois de comer ou beber**, só com um chiado: o som do ar da lata saía com valores inválidos (NaN, de um filtro instável), e isso cala a mistura de áudio da Unity inteira. O filtro foi corrigido, todo som gerado passou a ser conferido antes de tocar, e a mastigada foi refeita (três mordidas molhadas, a saliva e a mandíbula).
- **O soco:** com a mão vazia (um lugar vazio escolhido), os punhos ficam em guarda e o clique soca, alternando direita e esquerda (feitos no Blender, `maos.py`, na primeira pessoa e no corpo). O soco faz o zumbi recuar e tira pouco (8 socos matam), com som no ar, na carne e na parede, e os outros jogadores veem e ouvem.
- As prévias dos sons (passos de cada piso, comer, beber, largar, os socos e a porta) se gravam de novo pelo menu Zombie City Cleaner > Gravar prévias dos sons.
- **Testado no Editor:** o piso de cada cômodo das 5 casas e dos quartos do bunker, o som depois de comer e beber (sem NaN, e o tiro logo depois soando normal) e 3 socos num zumbi (3 recuos, e ele parte para o ataque).

## A mochila em grade e o menu do desenvolvedor (24/10/2026, a pedido)

- Antes, o que foi testado e aprovado ganhou um commit (`53a6004`).
- **A mochila (TAB),** pelo esboço do usuário: uma grade de 9 × 6 espaços, e cada coisa ocupa os seus (a pistola 2 × 1, o rifle 5 × 2, a sniper 7 × 2, a lata 1, a garrafa 1 × 2). Sem pilhas. O que é pego entra no primeiro espaço que couber; sem espaço, fica onde está (a troca com a arma da mão saiu).
- **Os 3 lugares viraram atalhos:** cada um aponta para um item da mochila, que continua nela. A arma pega vai para um atalho livre e para a mão, como antes. Cada arma guarda o pente dela.
- **A tela:** o fundo é o concreto do chão do bunker, com a borda escura e os rebites. À esquerda, o boneco do jogador em 3D (uma cópia do corpo com uma câmera própria; gira arrastando), os lugares da armadura (ainda vazios) e a barra de atalhos embaixo dos pés. No meio, a mochila e a aba de criar (ainda vazia). À direita, o nome, a foto e poucos números do item escolhido. Passando o mouse, o nome aparece ao lado.
- **Arrastar** move na grade, põe num atalho ou, para fora da janela, larga no chão; G larga o escolhido; 1, 2 e 3 com o mouse em cima põem no atalho. Com a tela aberta, o jogador fica parado.
- **Os ícones coloridos:** renderizados de novo no Blender com as cores da paleta, na proporção dos espaços de cada item, e uma foto de 3/4 para os detalhes. A barra do HUD também usa os coloridos, com o fundo claro (as armas são escuras).
- **O menu do desenvolvedor (F1),** só no Editor e nas builds de desenvolvimento: criar as armas, a lata e a garrafa (na mochila ou no chão), ver quantos zumbis há, matar todos e criar N em volta do jogador.
- **Testado no Editor, com um jogador:** pegar do painel, a dica sem espaço, os arrastos (para um atalho, mover, onde não cabe, para fora, trocar e tirar atalhos), o clique e o G, o pente por arma, comer pelo atalho, o G no jogo e o F1 (criar 10 e matar os 268). O teste com dois jogadores ficou para depois.
- **Depois do primeiro olhar do usuário:** saíram as legendas da tela (girar, arrastar, a contagem dos espaços) e o atalho nos detalhes, e o friso amarelo da borda da janela; a roda passa pelos 3 atalhos, vazios ou não.
- **1.000 zumbis na cidade** (eram 258): a maior parte na rua, na praça e no estacionamento, com os grupos mais abertos. Todos nascem; no Editor, o quadro médio ficou em 6,0 ms.
- **Girar na mochila:** segurando um item, R gira ele 90° (a largura e a altura trocam), e ele fica girado onde for solto. O que é pego entra girado se só couber assim. Testado: girar, desgirar, recusar onde não cabe e o rifle pego entrando em pé numa coluna livre. Embaixo da grade, a dica "R: girar item" entrou abaixo da "G: largar o escolhido".

## Bolsos, mochilas e o porte de armas, fatia 1: as regras e a tela (24/10/2026, a pedido; branch `feature/mochilas`)

- **O jogador começa sem mochila**, só com os bolsos (4 × 2), e acha (no futuro, compra) as mochilas: a pequena (6 × 4), a média (8 × 5) e a grande (10 × 7, maior que a grade de antes). A mochila e o coldre se vestem: pegar veste, uma mochila nova troca a vestida (a velha cai no chão), e tirar (G ou arrastar para fora da janela) larga no chão. Trocar ou tirar a mochila arruma a grade: o que não cabe mais cai no chão.
- **O porte de armas:** a primeira arma vai pela alça, no ombro, de qualquer tamanho e fora da grade. As outras ocupam a grade e precisam de um lugar no corpo: a mochila prende uma nas costas e o coldre (só estético, sem espaço) leva uma curta (pistola ou submetralhadora). Sem mochila e sem coldre, só uma arma; no máximo 3. A dica diz o que falta ("Para levar outra arma, ache uma mochila").
- **A tela do TAB:** a grade muda de tamanho com a mochila, embaixo dela a alça (arrastar uma arma para lá pendura), os lugares da Mochila e do Coldre ao lado do boneco (com os detalhes: o espaço e as armas que leva) e a contagem "Armas: 2 de 3".
- **Os modelos** das 3 mochilas e do coldre, os ícones e as fotos saíram de um script do Blender no repositório (`Blender/equipamentos.py`), no estilo dos itens do Gustavo. O F1 cria os 4.
- **Testado no Editor, com um jogador:** começar com a pistola na alça; recusar o rifle e a segunda pistola sem mochila; vestir o coldre e pegar a submetralhadora; vestir a pequena e pegar o rifle (nas costas); recusar a sniper com as 3 vagas cheias; trocar a pequena pela grande (a pequena no chão); tirar a grande (o rifle e as garrafas caíram, a submetralhadora e as latas ficaram nos bolsos). O teste com dois jogadores ficou para depois.
- **Falta:** as armas penduradas e a mochila vestida no corpo (fatia 2) e as mochilas e coldres espalhados pela cidade (fatia 3).

## O boneco equipado, a munição e o retrato (24/10/2026, a pedido; branch `feature/mochilas`)

- **O boneco de teste (F1 > "Boneco equipado"):** uma cópia do corpo, 2 m à frente, com 5 combinações de mochila, coldre e armas, para ver como fica (o próprio jogador ainda não se vê). O `Player/EquipamentoNoCorpo` põe as peças presas aos ossos, medidas no corpo em repouso; vai servir à fatia 2. Depois das fotos, o Pedro pediu as armas nas laterais da mochila em vez do X nas costas.
- **A munição:** um calibre por arma (9mm na pistola e na submetralhadora, 5,56 no rifle, calibre 12 na espingarda, 7,62 na sniper) e as caixas de até 60 balas (1 × 1). A recarga gasta as caixas da mochila (a espingarda, cartucho a cartucho, até acabarem); sem munição, não recarrega. Pegar completa as caixas que já estão na mochila, e arrastar uma caixa em cima de outra junta as duas. O HUD mostra o pente, as balas que sobram nas caixas e o calibre.
- **O pente vai com a arma:** largada, guardada no painel ou devolvida quando o jogador sai, ela leva as balas que tinha (antes, voltava cheia). O jogador nasce só com o pente da pistola.
- **As caixas:** modelos e ícones pelo `Blender/municao.py` (papelão claro com faixa azul, verde-oliva com faixa amarela, vermelha com os cartuchos, marrom com faixa preta). A montagem da cidade espalha 16 (2 por casa, 1 por apartamento, longe da comida), e o anfitrião sorteia as balas de cada uma no começo da partida. O F1 cria caixas cheias.
- **O retrato do inventário (pedido depois das fotos):** o Pedro não gostou de como o boneco segurava a pistola no TAB e pediu para ver nele o que ele leva. A pose agora é feita no código: o braço esquerdo solto e o direito baixo, segurando a pistola com o cano para baixo e para a frente (a comida e a água do mesmo jeito; a mão vazia, solta). As armas de coronha usam o parado delas, com as duas mãos, porque numa mão só a coronha entrava no corpo. A mochila, o coldre e as outras armas aparecem no retrato, pelo mesmo `EquipamentoNoCorpo` do boneco.
- **Testado no Editor, com um jogador:** sem caixa, a reserva é 0; uma caixa de 30 recarregou a pistola (2 → 12, sobraram 20); +50 completou a caixa (60) e abriu outra (10); gastar 15 tirou da mais vazia (a de 10 sumiu); juntar 25 em 50 deu 60 e 15; a pistola largada com 7 voltou com 7; a espingarda com 1 e 3 cartuchos ficou com 4 e a caixa sumiu; o painel começa com os pentes cheios. O teste com dois jogadores ficou para depois.

## O carro que se dirige (24/10/2026, a pedido; branch `feature/carro`)

- **O modelo:** uma perua dos anos 90, mostarda, feita do zero pelo `Blender/carro.py`, nas cores da paleta (as dos carros da cidade). As 4 portas, as rodas e o volante são peças à parte (para animar), o interior tem bancos, painel com mostradores, câmbio e forro, e os vidros são transparentes (dá para ver de dentro e de fora). O Pedro achou alguns carros da cidade ruins, inclusive na colisão: este colide com 3 caixas (a parte de baixo, a cabine e o para-brisa inclinado), não com uma caixa do tamanho do carro.
- **Entrar e sair:** E na porta ("[E] Dirigir" na do motorista, "[E] Entrar no carro" nas outras); a porta abre, o jogador vai até o banco e senta, e a porta bate. E dentro sai pela mesma porta (com o carro quase parado), para um lugar livre do lado dela. 4 lugares, um por porta.
- **Dirigir:** WASD (tração na frente, ré, freio), Espaço o freio de mão (a traseira escorrega), L os faróis, B a buzina, V a câmera de trás do carro, R desvira o carro virado. F liga e desliga a lanterna dentro do carro também (antes, no volante, o F era dos faróis e a lanterna ficava presa como estava). Até ~80 km/h. Dentro, o mouse olha em volta; o corpo de quem está sentado é posto numa pose sentada (as mãos no volante ou no colo).
- **Rede:** quem dirige vira o dono do carro e simula a física; os outros veem pela rede. Quem está em cada lugar é do anfitrião.
- **Atropelar:** o carro passa pelos zumbis (não bate neles como num muro) e, acima de 3 m/s, cada zumbi na frente leva um acerto no tronco, mais forte quanto mais rápido; rápido, o corpo voa.
- **A chave (depois do segundo teste):** dava para sair andando antes de a partida acabar, porque ela era automática. Agora a partida é um mini-game: olhando para a ignição (no painel, à direita do volante, com um anel que acende no escuro), um clique põe a chave; segurando o clique e arrastando o mouse para a direita, ela gira para a ignição (o painel acende) e para a partida (com mola). O motor de arranque gira até o motor pegar (de 0,7 a 1,6 s, sorteado); soltar antes não liga, e segurar depois de pegar faz o arranque raspar. Arrastar para a esquerda desliga.
- **A fresta entre as portas:** a coluna B só existia acima da linha das janelas, e pelo vão entre a porta da frente e a de trás via-se o interior do carro. Agora ela desce até a soleira, pintada e um pouco mais funda que as portas (lê como uma emenda).
- **A sombra do próprio corpo na lanterna:** o corpo do jogador, invisível na primeira pessoa, ainda fazia sombra, e a lanterna projetava as mãos e os braços dele como manchas pretas no facho (mirando a pistola, no guarda-mão do rifle, no painel e no banco do carro). Agora a lanterna de cada um não faz sombra do próprio corpo (o corpo fica numa camada de render só dele); a lua e as lanternas dos outros continuam fazendo.
- **A chave, refeita depois do terceiro teste:** segurar e soltar na hora certa não fazia sentido. Agora um clique pega a chave e aparece um mostrador redondo (a ignição vista de frente, com DESL, LIGA e PARTIDA). Da ignição para a partida a chave pesa e volta sozinha (é preciso empurrar o mouse); chegando, ela fica presa e o motor de arranque gira, e começa o quick time event: um ponteiro dá voltas no anel do mostrador, e o clique tem de pegar ele em cima das marcas. A marca pequena (dourada) liga na hora; as 3 grandes (laranja) ligam quando todas foram acertadas (cada uma faz o motor tossir), e errar apaga as acertadas. Em 3 voltas sem ligar, a chave volta. Ligou, a chave volta sozinha para a ignição. Só anda com o motor ligado; quem sai leva a chave e o motor desliga.
- **Os sons**, gerados por código: o motor (com a rotação), a partida, a buzina, a porta abrindo e batendo. A partida foi refeita depois do primeiro teste: o estalo do solenoide, o motor de arranque girando e forçando a cada compressão, o motor pegando com umas falhas, acelerando e caindo para a marcha lenta, que emenda no ronco do motor.
- **Por dentro, à noite:** o painel acende com a ignição (o velocímetro até 160 km/h, o marcador da gasolina e a luz verde do farol, com os ponteiros laranja), uma luz fraca do painel clareia o volante e as mãos, e a luz de teto acende quando uma porta abre e apaga uns segundos depois. A lanterna, no carro, sai do olho e mais fraca (do peito, ela ficava a 25 cm do volante e estourava o painel de branco).
- **O capô e as peças do motor (a pedido, pelo esboço do usuário):** E no capô leva a câmera para dentro dele, como se o personagem enfiasse a cabeça embaixo do capô (a primeira versão era uma tela 2D com as porcentagens; o pedido era ver o motor). O motor, a vela, a bateria e o radiador estão lá em 3D; o lugar vazio mostra a peça apagada; cada peça tem uma etiqueta com o estado em %, e o mouse em cima acende a peça e diz o que ela faz. Clique numa peça: ela vai para a mochila; clique num lugar vazio ou numa peça da mochila (na coluna à direita): ela vai para o lugar dela. As peças se acham no capô dos outros carros da cidade (21 deles, cada um com o que sobrou, sorteado no começo da partida). O carro da família começa quebrado: a vela queimada (o motor não pega), a bateria em 30% (a partida gasta a carga; abaixo de 12%, o motor de arranque não gira) e o radiador em 25% (o motor ferve e apaga depois de uns 100 s ligado). Uma vela boa já arruma o motor. O capô sobe enquanto alguém olha o motor, no carro da família e nos da cidade (o capô deles é recortado da malha pela montagem, e o cofre aparece por dentro).
- **As rodas (etapa 1 da troca de pneu, a pedido):** cada roda do carro tem o estado do pneu. O pneu furado afunda a roda, segura menos e arrasta, e o carro puxa para aquele lado; o gasto escorrega mais; sem a roda, o carro fica no cubo e quase não anda. O carro da família começa com o pneu da frente esquerda furado. O pneu (a roda inteira) é um item de 3 × 3 na mochila. Depois do teste, o furado puxa mais, e um tiro no pneu o fura (com o estouro e o ar escapando).
- **O porta-malas (etapa 2 da troca de pneu, a pedido):** a perua ganhou a tampa traseira, que abre com E (o porta-malas é oco por dentro, com o piso, as caixas das rodas e o forro). Lá dentro ficam o macaco (um item novo, 3 × 1) e o estepe (um pneu em 85%): E pega o que a mira aponta, guarda de volta no lugar vazio o que o jogador leva, ou fecha a tampa.
- **O macaco e a troca de roda (etapa 3, a pedido):** com o macaco na mochila e o carro vazio e parado, E ao lado de uma roda apoia o macaco embaixo da soleira; E levanta aquele lado do carro (a manivela estalando), as rodas desse lado saem do chão e E tira a roda (vai para a mochila como pneu, com o estado dela) ou põe o melhor pneu da mochila; E baixa o carro, e mirando no macaco E o pega de volta. Com o macaco apoiado, ninguém entra no carro.
- **O pneu nas mãos (etapa 4a, a pedido):** o pneu que não cabe na mochila (sem mochila, ele nunca cabe) vai para as mãos. Carregando, o jogador anda devagar, não corre, não pula nem usa a arma; G larga, e o pneu das mãos serve para a roda vazia e para o porta-malas. Os outros veem o pneu na frente do peito.
- **As rodas dos carros da cidade (etapa 4b, a pedido):** os carros abandonados também trocam de roda com o macaco, do mesmo jeito da perua. As rodas deles foram recortadas da malha (pela cor do pneu), e cada carro começa com algumas rodas faltando e pneus furados. Sem uma roda, o canto do carro desce até o cubo; com o macaco, o lado dele sobe. As próximas etapas: o porta-malas com o macaco e o estepe, levantar o carro com o macaco e trocar a roda, e pegar as rodas dos carros da cidade.
- Os testes ficam com o Pedro (a pedido: sem testes automáticos por enquanto).

## Mudanças de rumo, em resumo

| Quando | O que mudou |
|---|---|
| Recomeço | A corporação e a cota saíram; o jogo virou a família no bunker. O visual de PS1 saiu. |
| Parte 8 | Veio antes da Parte 7, a pedido. A luz ficou em tempo real. |
| Ajustes antes da Parte 7 | O desmembramento saiu (só a cabeça explode); a morte virou ragdoll; o `NovoZombie` foi trocado por 4 modelos; a pistola única virou 4 armas; o som ambiente saiu. |
| Armas e desmembramento | O desmembramento voltou, nos modelos novos, com mancar e rastejar; a sniper com luneta virou a 5ª arma; o dano das armas foi refeito (tronco e membros, a espingarda pela distância). |
| Bunker maior | O bunker de 3 salas virou o salão alto com 4 quartos e o depósito; o suporte de armas virou um painel com um lugar por arma; a porta abre, fecha e isola o som; as luzes têm interruptor. |

## Pendências e observações conhecidas

- **O saque nos móveis, a despensa, dormir e salvar** ainda não existem (Parte 7). A comida, a água e a munição no chão não são salvas: cada partida começa com os 50 itens do começo (as balas das caixas são sorteadas de novo).
- **A mochila não empilha os itens** (de propósito, por enquanto), e a armadura e a aba de criar ainda estão vazias.
- **Os outros não veem comer e beber:** o corpo segura a lata ou a garrafa na guarda dos punhos, e só quem come ou bebe ouve o som.
- **O painel de armas, a porta e as luzes do bunker não são salvos:** ao recomeçar a partida, o painel volta ao conteúdo inicial, a porta fecha e as luzes acendem.
- **Atirar dentro do bunker com a porta aberta atrai os zumbis próximos** (o barulho do tiro chama até 35 m). Com a porta fechada, não. Defender a base é assunto de depois.
- **A lanterna de perto clareia muito as armas escuras do painel e a porta do bunker.**
- **A luneta afina a neblina só para quem mira:** é uma ajuda de jogo, não física. Se ficar estranho, o `neblinaNaLuneta` (no `AmbienteDoDia`) volta a 1.
- **Um corte raro** (por exemplo, três membros de uma vez, ou um zumbi sem cabeça e sem membros) ainda é feito na hora do tiro, e custa alguns milissegundos naquele quadro.
- **Se o anfitrião sair, a partida acaba** para todos.
- **Falta testar com dois jogadores** a sniper e o desmembramento: os cortes, a luneta e os acertos vindos de outra máquina.
