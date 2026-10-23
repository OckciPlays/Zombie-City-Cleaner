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

## Mudanças de rumo, em resumo

| Quando | O que mudou |
|---|---|
| Recomeço | A corporação e a cota saíram; o jogo virou a família no bunker. O visual de PS1 saiu. |
| Parte 8 | Veio antes da Parte 7, a pedido. A luz ficou em tempo real. |
| Ajustes antes da Parte 7 | O desmembramento saiu (só a cabeça explode); a morte virou ragdoll; o `NovoZombie` foi trocado por 4 modelos; a pistola única virou 4 armas; o som ambiente saiu. |

## Pendências e observações conhecidas

- **Saque, inventário, dormir e salvar** ainda não existem (Parte 7). A munição de reserva é infinita, e a vida se recupera sozinha depois de 8 s sem apanhar (provisório).
- **O suporte de armas não é salvo:** ao recomeçar a partida, ele volta ao conteúdo inicial.
- **Atirar dentro do bunker atrai os zumbis próximos** (o barulho do tiro chama até 35 m). Defender a base é assunto de depois.
- **A lanterna de perto clareia muito as armas escuras do suporte.**
- **Se o anfitrião sair, a partida acaba** para todos.
