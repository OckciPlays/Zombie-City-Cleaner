# A árvore de habilidades, o lago e a oficina (06/11/2026)

Pedidos pelo Pedro em 06/11/2026 ("um sistema de XP que você melhoraria suas skill") e discutidos antes de programar.
Depois do primeiro teste dele ("estão funcionando"), o resto foi feito de uma vez: as 45 habilidades com efeito, a água
(nadar, o fôlego, o afogamento), o caiaque de dois lugares, a pesca, o machado cortando árvores, a mesa de oficina no
bunker, as receitas novas e a mata de outono em volta do lago.

**Desde 09/11/2026, a árvore é outra** (a seção "A árvore nova", logo abaixo): níveis em cada habilidade, fileiras, 13
ramos e 40 níveis. As seções seguintes contam a de 06/11/2026.

## A árvore nova (09/11/2026)

Aprovada pelo Pedro a partir de uma maquete e feita em etapas. **A etapa 1a** é a árvore em si: o catálogo, os níveis em
cada nó, as fileiras, os pré-requisitos, tirar ponto, a tela nova, o save (com a migração), a rede e o F1. Os nós dos
**ramos novos** (o Armeiro de sucata, o Fogo, a Proteção de sucata, as Armadilhas e defesas) e o Sucateiro aparecem com o
selo "em breve" e ainda não dá para aprender: o efeito deles vem nas próximas etapas (a sucata, na 1b). As seções de
cima (o XP, as habilidades, a aba) contam a árvore de 06/11/2026; o que mudou está aqui. Desde a 1ª leva da etapa 1b
(09/11/2026), os três primeiros nós do Armeiro de sucata já valem (veja "O Armeiro de sucata: o que já vale"); desde a
etapa 2 (10/11/2026), o Armeiro inteiro; desde a etapa 3 (11/11/2026), a Proteção de sucata inteira (veja "A proteção
de sucata"); desde a etapa 4 (11/11/2026), o Fogo inteiro (veja "O fogo"); desde a etapa 5a (11/11/2026), as
Armadilhas e defesas, menos a Barricada de tábuas e a Fortaleza; e desde a etapa 5b (11/11/2026), o ramo inteiro (veja
"As armadilhas" e "As barricadas e a Fortaleza").

### A mecânica

- **Níveis nas habilidades:** cada nó tem de 1 a 3 níveis (o "k/máx" no canto), e cada ponto é um passo. O efeito cresce
  com os pontos; os de liga e desliga (o Chaveiro, a Ligação direta, Pelas costas, as pontas) valem com 1.
- **As fileiras (1 a 5):** para aprender na fileira R, a área precisa de pontos gastos nas fileiras **de cima** dela:
  0 na 1, 3 na 2, 7 na 3, 12 na 4 e 18 na 5 (as pontas). Na tela, à esquerda: 0+, 3+, 7+, 12+, 18+ (acesos quando a
  fileira já abriu).
- **Os pré-requisitos:** cada nó pede todos os da lista com pelo menos 1 ponto. Alguns são de outro ramo da mesma área:
  a Luneta de cano pede a Mão firme, o Silenciador caseiro o Tiro abafado, o Calor do fogo os Nervos de aço, o Remendo de
  chapa o Reparador, as Estacas o Artesão.
- **Tirar ponto (o botão direito):** dá, se nada que já foi aprendido depender dele: nem um nó que o pede (com o último
  ponto), nem as fileiras de baixo da mesma área (que precisam dos pontos gastos acima delas).
- **40 níveis, 40 pontos.** A árvore tem 122 pontos (78 nos nós que já têm efeito): não dá para pegar tudo.
- **A curva do XP:** igual à de antes até o nível 11 (50 do 1 para o 2, 10 a mais a cada nível, 150 do 11 para o 12); dali,
  5 a mais a cada nível (290 do 39 para o 40). O nível 10 pede 810 de XP; o 20, 2.480; o 30, 4.655; o 40, **7.330**, umas
  **16 h** de jogo (pela conta de antes: 5.510 em 12 h, uns 460 por hora). Do nível 12 em diante, a curva é mais baixa
  que a velha (o nível 30 chega em ~10 h, não em 12).
- **A migração do save:** num personagem salvo com a árvore velha (o `aprendidas` preenchido), o anfitrião devolve todos os
  pontos ao carregar (o XP e o nível ficam) e liga o aviso: "A árvore de habilidades mudou: os seus pontos voltaram.
  Distribua de novo." Ele aparece embaixo da mira ao entrar e embaixo da aba K (até o clique nele ou um ponto); quando a
  aba K o mostra, o anfitrião apaga (não volta no próximo save). O personagem do Pedro (o Estômago forte e o Cantil, 473
  de XP) entrou no nível 7 com os 7 pontos livres.
- **A rede:** os pontos de cada nó vão no `ProgressoDoPersonagem`, como antes; a `VersaoDaRede` subiu para 12.
- **Nesta etapa, o Faz-tudo não pedia o Sucateiro** (o Sucateiro era "em breve": sem isso, o Craft ficaria trancado). O
  pedido entrou na 2ª leva da 1b (09/11/2026), com a sucata: veja "O Sucateiro" mais abaixo.
- **O Aproveitador virou o 2º ponto das Mãos hábeis**, e nos Recursos o Farmacêutico (fileira 3) veio antes do Reparador
  (fileira 4).

### A aba HABILIDADES (a tecla K)

- A árvore ocupa o meio e a coluna da direita. Em cima, "NÍVEL N · pontos livres X de N" e a barra do XP (com o mouse
  nela, como ganhar XP).
- **Uma página por área**, com as abas no alto: o Combate (laranja), a Resistência (verde) e a Sobrevivência (roxo), com
  os pontos gastos em cada uma.
- **Os ramos em colunas**, com o nome em cima e o selo "NOVO" nos novos. **Desde 10/11/2026, cada nó é o ícone dele**
  (como no "Dungeons & Developers", a referência do Pedro): um quadrado com a imagem pintada no Blender (um objeto em 3D
  low-poly, com o fundo escuro e um toque da cor da área) e o nome embaixo, numa linha (o nome longo fica com a letra
  menor). O "k/máx" fica numa plaquinha no canto de baixo, e a estrela das pontas num círculo no canto de cima.
- **As linhas** ligam cada nó aos pré-requisitos (também os de outro ramo da página): grossas, com uma seta no nó que
  pede; apagadas, e douradas quando os dois lados estão aprendidos.
- **Os estados:** aprendida (a moldura dourada e grossa, com um brilho em volta; mais forte com todos os pontos), dá para
  aprender (a moldura na cor da área; mais fraca sem ponto livre), trancada (o ícone cinza e escuro, a moldura apagada) e
  "em breve" (como a trancada, com a faixa "EM BREVE" no meio do ícone).
- **Com o mouse num nó,** um quadro com o ícone, o nome, o ramo e a fileira, o nível, o efeito de agora e o do próximo
  nível, e o que falta (os pontos da fileira, os pré-requisitos ou o ponto livre), ou se dá para tirar.
- **O clique põe um ponto; o botão direito tira.** O que não dá pisca a borda em vermelho.

### Os nós

O efeito é por ponto (o número do nível máximo entre parênteses). ⭐ marca as pontas; "em breve" ainda não dá para aprender.

**Combate** (as colunas: o Guerreiro 0, o Armeiro de sucata 1 a 3, o Assassino 4, o Ladrão 5)

| Ramo | Nó | Col. | Fil. | Níveis | Efeito | Pede |
|---|---|---|---|---|---|---|
| Guerreiro | Tiro forte | 0 | 1 | 3 | +4% de dano com arma de fogo (+12%) | — |
| | Recarga rápida | 0 | 2 | 2 | recarrega 8% mais rápido (16%) | Tiro forte |
| | Braço forte | 0 | 3 | 3 | +5% de dano com arma branca (+15%) | Recarga rápida |
| | Mão firme | 0 | 4 | 2 | 12% menos coice e tremor (24%) | Braço forte |
| | ⭐ Mira de aço | 0 | 5 | 1 | a mira não treme | Mão firme |
| Armeiro de sucata (os três primeiros valem desde 09/11/2026; o resto, as melhorias, desde 10/11/2026) | Pistola de sucata | 2 | 1 | 1 (eram 2 até 10/11/2026: o segundo não fazia nada) | a pistola de sucata (um tiro) e a munição de prego | — |
| | Rifle de precisão de sucata | 1 | 2 | 1 | o rifle de sucata e a munição de parafuso | Pistola de sucata |
| | Submetralhadora de sucata | 3 | 2 | 1 | a submetralhadora de sucata | Pistola de sucata |
| | Cano reforçado | 1 | 3 | 3 | o cano reforçado I, II e III: +8% de dano por nível | Rifle de precisão |
| | Miras caseiras | 2 | 3 | 2 | 1: a mira de ferro ajustada; 2: a mira de ponto caseira | Pistola de sucata |
| | Carregador estendido | 3 | 3 | 1 | da pistola e da submetralhadora | Submetralhadora |
| | Luneta de cano | 1 | 4 | 1 | a luneta no rifle de sucata | Cano reforçado, Mão firme |
| | Mola nova | 2 | 4 | 2 | emperram 25% menos por nível | Miras caseiras |
| | Silenciador caseiro | 3 | 4 | 2 | 1: de garrafa PET; 2: de filtro de óleo | Carregador estendido, Tiro abafado |
| | ⭐ Mestre armeiro | 2 | 5 | 1 | as de sucata batem como as reais; fabricar gasta 25% menos sucata | Mola nova |
| Assassino | Passos leves | 4 | 1 | 2 | os zumbis ouvem seus passos 25% menos longe (50%) | — |
| | Pelas costas | 4 | 2 | 1 | o golpe pelas costas mata o Oco distraído | Passos leves |
| | Discreto | 4 | 3 | 2 | os sobreviventes veem 15% menos longe (30%) e ouvem 25% menos (50%) | Pelas costas |
| | Tiro abafado | 4 | 4 | 2 | o tiro chega 15% menos longe (30%) | Discreto |
| | ⭐ Sangue frio | 4 | 5 | 1 | matar não abala a mente | Tiro abafado |
| Ladrão | Borracheiro | 5 | 1 | 1 | remenda o pneu e anda mais rápido com ele | — |
| | Mãos leves | 5 | 2 | 2 | arromba 15% mais rápido (30%); no 2, o pé de cabra não chama zumbis e a gazua não quebra | Borracheiro |
| | Chaveiro | 5 | 3 | 1 | abre as trancadas com um grampo | Mãos leves |
| | Alarme mudo | 5 | 4 | 2 | o alarme dispara 40% menos (80%) | Chaveiro |
| | ⭐ Ligação direta | 5 | 5 | 1 | liga os carros sem a chave | Alarme mudo |

**Resistência** (o Fogo 0 e 1, a Saúde 2, a Fome e sede 3, os Recursos 4, a Proteção de sucata 5)

| Ramo | Nó | Col. | Fil. | Níveis | Efeito | Pede |
|---|---|---|---|---|---|---|
| Fogo | Tocha duradoura | 0,5 | 1 | 2 | a tocha dura 50% mais por ponto | — |
| | Lampião | 0 | 2 | 1 | o lampião (na mesa de oficina) | Tocha duradoura |
| | Coquetel molotov | 1 | 2 | 1 | o coquetel molotov (na bancada) | Tocha duradoura |
| | Fogueira portátil | 0 | 3 | 1 | o kit da fogueira portátil (na bancada) | Lampião |
| | Fogo que pega | 1 | 3 | 2 | o molotov e a tocha queimam 30% mais tempo, e a área do molotov fica 20% maior, por ponto | Coquetel molotov |
| | Calor do fogo | 0 | 4 | 2 | a menos de 4 m de um fogo aceso, a mente sobe 2 por hora por ponto | Fogueira portátil, Nervos de aço |
| | ⭐ Muralha de fogo | 0,5 | 5 | 1 | o seu fogo segura os zumbis 50% mais longe, e eles fogem em pânico | Calor do fogo |
| Saúde | Mãos de enfermeiro | 2 | 1 | 2 | curativos 12% mais rápidos (24%) | — |
| | Curativo bem feito | 2 | 2 | 2 | +5% à parte (+10%) | Mãos de enfermeiro |
| | Imunidade | 2 | 3 | 2 | infecciona 20% menos (40%) | Curativo bem feito |
| | Nervos de aço | 2 | 4 | 3 | a mente cai 10% menos (30%) | Imunidade |
| | ⭐ Médico de campo | 2 | 5 | 1 | a bandagem comum protege; o kit na metade do tempo | Nervos de aço |
| Fome e sede | Estômago forte | 3 | 1 | 3 | a fome 5% mais lenta (15%) | — |
| | Cantil | 3 | 2 | 3 | a sede 5% mais lenta (15%) | Estômago forte |
| | Fôlego | 3 | 3 | 2 | correr gasta 1,4 vez (1,25), no lugar de 1,6 | Cantil |
| | Bem nutrido | 3 | 4 | 2 | comer e beber enchem 12% mais (24%) | Fôlego |
| | ⭐ Camelo | 3 | 5 | 1 | a fome e a sede 30% mais lentas | Bem nutrido |
| Recursos | Cuidadoso | 4 | 1 | 2 | as armas se desgastam 10% menos (20%) | — |
| | Econômico | 4 | 2 | 2 | pilhas e gasolina 12% mais (24%) | Cuidadoso |
| | Farmacêutico | 4 | 3 | 2 | analgésico e xarope 25% mais (50%) | Econômico |
| | Reparador | 4 | 4 | 2 | 15% de não gastar o kit (30%) | Farmacêutico |
| | ⭐ Remendão | 4 | 5 | 1 | as armas nunca emperram | Reparador |
| Proteção de sucata (desde 11/11/2026) | Capacete de panela | 5 | 1 | 1 | o capacete de panela (corta 35% na cabeça) | — |
| | Braçadeiras e caneleiras | 5 | 2 | 1 | as braçadeiras e as caneleiras (30% nos braços e nas pernas) | Capacete de panela |
| | Colete de chapa | 5 | 3 | 1 | o colete de chapa (30% no tronco; pesa e faz barulho) | Braçadeiras e caneleiras |
| | Remendo de chapa | 5 | 4 | 2 | as peças se gastam 25% menos (50%) | Colete de chapa, Reparador |
| | ⭐ Couraça | 5 | 5 | 1 | a mordida numa parte coberta não infecciona | Remendo de chapa |

**Sobrevivência** (o Craft 0, as Armadilhas e defesas 1 e 2, o Sortudo 3, a Pesca 4)

| Ramo | Nó | Col. | Fil. | Níveis | Efeito | Pede |
|---|---|---|---|---|---|---|
| Craft | Sucateiro | 0 | 1 | 3 | desmontar (os painéis e os itens) rende +1 sucata por ponto | — |
| | Faz-tudo | 0 | 2 | 1 | na bancada: a gazua, a faca e a mochila média | Sucateiro (desde 09/11/2026) |
| | Artesão | 0 | 3 | 1 | na oficina: o machado, o pé de cabra, a mochila grande, a lanterna | Faz-tudo |
| | Mãos hábeis | 0 | 4 | 2 | 1: 25% de sair em dobro; 2: também 20% de devolver um material | Artesão |
| | ⭐ Armeiro | 0 | 5 | 1 | a munição na oficina, com a pólvora | Mãos hábeis |
| Armadilhas e defesas (desde 11/11/2026, as etapas 5a e 5b) | Alarme de latas | 1,5 | 1 | 1 | o alarme de latas (na bancada): quem passa em pé faz as latas tocarem, os zumbis a 12 m vêm ver e o dono recebe o aviso; 12 toques | — |
| | Barricada de tábuas | 1 | 2 | 2 | as tábuas para barricada (na bancada, com a ferramenta: 3 madeiras e 6 pregos), pregadas numa porta (150) ou numa janela (100) das casas: ela não abre; 2: +50% de vida e o conserto (o clique, 1 madeira e 2 pregos: +35%) | Alarme de latas |
| | Tapete de pregos | 2 | 2 | 1 | o tapete de pregos (na bancada, com a ferramenta): 20 no zumbi (e 50% mais lento por 4 s), 6 na perna de quem pisa em pé, fura o pneu; 8 usos | Alarme de latas |
| | Estacas | 1 | 3 | 1 | as estacas (na bancada, com a ferramenta): seguram como a barricada, 12 por segundo em quem encosta na frente; o Brutamonte as quebra e leva 60 | Barricada de tábuas (desde a 5b, de novo), Artesão |
| | Armadilheiro | 2 | 3 | 2 | as suas armadilhas: +50% de usos e vida e +25% de dano por ponto; no 2, o seu tapete não fere jogadores | Tapete de pregos |
| | Cerca de fogo | 1,5 | 4 | 1 | a cerca de fogo (na bancada, 2 combustíveis): um rastro de 4 m que, aceso, queima 90 s; os zumbis não atravessam | Estacas |
| | ⭐ Fortaleza | 1,5 | 5 | 1 | as suas barricadas e estacas com o dobro da vida, consertando sozinhas (5% por hora do jogo, você a até 30 m, sem zumbi batendo); o seu alarme avisa de qualquer distância | Cerca de fogo |
| Sortudo | Olho bom | 3 | 1 | 2 | 5% de mais um comum (10%); as pilhas +12% (+25%) | — |
| | Garimpeiro | 3 | 2 | 2 | 4% de um incomum (8%) | Olho bom |
| | Abutre | 3 | 3 | 2 | 1: uma ou duas coisas a mais; 2: duas ou três e a munição | Garimpeiro |
| | Pé de coelho | 3 | 4 | 2 | 2% de um raro (4%) | Abutre |
| | ⭐ Faro | 3 | 5 | 1 | os raros marcados a 15 m | Pé de coelho |
| Pesca | Paciência | 4 | 1 | 2 | o peixe morde 20% mais rápido (40%) | — |
| | Isca fina | 4 | 2 | 2 | 20% de não gastar a isca (40%) | Paciência |
| | Peixe grande | 4 | 3 | 2 | mais tucunarés e tilápias (no 2, como antes) | Isca fina |
| | Noite de pesca | 4 | 4 | 1 | a noite e a chuva ajudam | Peixe grande |
| | ⭐ Mestre pescador | 4 | 5 | 1 | às vezes, o dourado | Noite de pesca |

### Os testes (F1, a aba "Habil.")

- +50 XP, +1 nível, +5 níveis, o nível 40, o nível 10; **Aprender um** (um ponto no primeiro nó que dá) e **Tirar um** (do
  último que dá); Esquecer todas (os pontos voltam), Tudo do zero e **Save velho** (o nível 10 com habilidades da árvore
  velha, passando pela migração, como ao carregar: o aviso aparece na aba K).
- As armas de sucata (09/11/2026): Pistola sucata, Rifle sucata, Submet. sucata, 20+20 munições (de prego e de parafuso),
  20 sucatas e Materiais sucata (20 sucatas, 3 madeiras, 3 fitas e 3 pólvoras).
- As armadilhas (11/11/2026, a etapa 5a): "Armadilhas: nós" (o alarme, o tapete, as estacas, o Armadilheiro 2 e a cerca
  de fogo, com o Artesão), "Armadilhas: peças" (2 alarmes, 2 tapetes, as estacas e 2 cercas) e "Mat. armadilhas" (os
  materiais das 4 receitas, com a ferramenta e 2 álcoois).
- A sucata, 2ª leva (09/11/2026): "Painel na frente" (um painel de teste a 1,3 m, um modelo de cada vez; não vai no save),
  "Itens desmontar" (a ferramenta, o pé de cabra, a garrafa térmica, 3 peças de metal, o radiador, o macaco e uma pistola
  quebrada) e "Ir a um painel" (para perto do painel do mapa mais perto).

### O Armeiro de sucata: o que já vale (09/11/2026, a 1ª leva da etapa 1b)

As armas de sucata são as do começo do jogo: fabricadas, com a munição só delas e uns 75% do dano das reais. Cada uma é
destravada à parte (os números de cada arma estão no `CLAUDE.md`, "As armas de sucata, 1ª leva"):

| Nó | Destrava | Onde | Gasta |
|---|---|---|---|
| Pistola de sucata (`a1`) | a pistola de sucata (um tiro, munição de prego) | bancada | 6 sucatas, 1 madeira, 1 fita |
| | a munição de prego × 10 | bancada | 1 pólvora, 1 sucata |
| Rifle de precisão de sucata (`a2`) | o rifle de sucata (de ferrolho, sem luneta, 3 tiros, munição de parafuso) | mesa de oficina | 10 sucatas, 2 madeiras, 1 fita |
| | a munição de parafuso × 5 | bancada | 1 pólvora, 2 sucatas |
| Submetralhadora de sucata (`a3`) | a submetralhadora de sucata (automática, 20 tiros, munição de prego) | mesa de oficina | 12 sucatas, 1 madeira, 2 fitas |

- A mesa de oficina também vale como bancada. As Mãos hábeis (sair em dobro, devolver um material) valem nelas também.
- O `a1` tem 1 nível desde 10/11/2026 (o 2º não fazia nada). Do `a4` (Cano reforçado) ao `a10` (Mestre armeiro), valem
  desde 10/11/2026 (as melhorias, abaixo). O Sucateiro passou a valer na 2ª leva (abaixo).

### O Armeiro de sucata: as melhorias (10/11/2026, a etapa 2)

Peças fabricadas que se põem e se tiram nas armas de sucata, na bancada (a mesa de oficina vale): na aba Criar, a barra
de cima tem RECEITAS e **MELHORIAS DAS ARMAS**. Ali aparecem as armas de sucata da mochila e, da escolhida, os encaixes:
o Cano (um silenciador), a Mira (a de ferro e, em cima, a de ponto ou a luneta), o Carregador e o Reforço (um cano
reforçado). Clicar numa peça da mochila põe (e gasta a peça; a que estava no lugar volta para a mochila); clicar numa
posta tira (e ela volta). **A melhoria fica na própria arma**: vai junto para o chão, para a estante, para outro jogador
e no save. As armas reais não aceitam as de sucata.

| Nó | Peça | Onde | Gasta | Faz |
|---|---|---|---|---|
| Silenciador caseiro (`a9`), nível 1 | silenciador de garrafa PET | bancada | 1 garrafa de refrigerante, 1 fita | o tiro chega 60% menos longe para os zumbis e tira 5% menos; dura 15 tiros e se desfaz |
| Silenciador caseiro, nível 2 | silenciador de filtro de óleo | mesa de oficina | 4 sucatas, 1 fita | 50% menos longe, 5% menos de dano; dura 80 tiros |
| Miras caseiras (`a5`), nível 1 | mira de ferro ajustada | bancada | 2 sucatas | o tiro espalha 15% menos |
| Miras caseiras, nível 2 | mira de ponto caseira | mesa de oficina | 3 sucatas, 1 fita, 1 par de pilhas | mirando, espalha 30% menos, e o olho vai no tubinho, com o pontinho vermelho no meio |
| Luneta de cano (`a7`) | luneta de cano (só no rifle) | mesa de oficina | 5 sucatas, 2 fitas | mirando, aproxima 2,5 vezes (a tela da luneta da sniper) e espalha 60% menos |
| Carregador estendido (`a6`) | carregador estendido (pistola e submetralhadora) | bancada | 4 sucatas, 1 fita | a pistola leva 3 tiros; a submetralhadora, 32 |
| Cano reforçado (`a4`), níveis 1, 2 e 3 | cano reforçado I, II e III | I na bancada; II e III na mesa de oficina | 4 sucatas e 1 fita; 6 sucatas e 2 fitas; 8 sucatas e 2 peças de metal | +8%, +16% e +24% de dano; a arma gasta 10%, 20% e 30% mais |
| Mola nova (`a8`), 2 níveis | — | — | — | as armas de sucata emperram 25% menos por nível |
| ⭐ Mestre armeiro (`a10`) | — | — | — | as armas de sucata batem como as reais (sem o 75%) e as receitas do Armeiro de sucata gastam 25% menos sucata |

- A mira de ferro ajustada convive com a de ponto e com a luneta; a de ponto e a luneta, não (o mesmo lugar). Os dois
  silenciadores não vão juntos, nem os três canos.
- O silenciador tirado volta com o desgaste dos tiros que já deu ("Ainda dá: 12 tiros"). Tirar o carregador estendido
  com o pente cheio devolve as balas a mais numa caixa. Desmontar a arma devolve as peças.
- Não há garrafa vazia nem filtro de óleo como item: a PET é a garrafa de refrigerante; o filtro sai da sucata.
- O F1 (a aba "Habil."): "Melhorias: nós", "Melhorias: peças", "Mat. melhorias" e "Painel melhorias".

### A proteção de sucata (11/11/2026, a etapa 3)

Quatro peças que se vestem num grupo novo da tela do inventário, o **PROTEÇÃO** (a fileira embaixo do retrato, acima dos
atalhos). Cada uma corta uma parte do golpe que pega na parte do corpo que ela cobre, e se gasta com ele.

| Nó | Peça | Onde | Gasta | Cobre e corta | Mais |
|---|---|---|---|---|---|
| Capacete de panela (`p1`) | capacete de panela | bancada | 3 sucatas, 1 fita, 1 corda | a cabeça, 35% | não vai com o chapéu |
| Braçadeiras e caneleiras (`p2`) | braçadeiras de sucata | bancada | 4 sucatas, 2 fitas | os braços, 30% | — |
| Braçadeiras e caneleiras (`p2`) | caneleiras de sucata | bancada | 4 sucatas, 2 fitas | as pernas, 30% | os passos se ouvem 10% mais longe |
| Colete de chapa (`p3`) | colete de chapa | mesa de oficina | 8 sucatas, 2 fitas, 1 pneu | o tronco, 30% | anda 4% mais devagar; passos 15% mais longe |
| Remendo de chapa (`p4`), 2 níveis | — | — | — | — | as peças se gastam 25% menos por nível |
| ⭐ Couraça (`p5`) | — | — | — | — | a ferida da mordida numa parte coberta não infecciona |

- **O desgaste:** cada golpe que a peça segura gasta um pouco dela (a mordida do Oco, 8% do capacete, das braçadeiras e
  das caneleiras e 5% do colete). Em 0% ela quebra: o lugar mostra "quebrado" e ela não protege mais até ser consertada
  com um kit de reparo (o botão direito no lugar, "Consertar", ou "Reparar" na mochila; qualquer um conserta, e o
  Reparador também poupa o kit). As braçadeiras e as caneleiras são um par: um desgaste só.
- **O que ela segura:** a mordida, o corte, a pancada, o tiro e o esmagamento. A queda, o tornado e o raio passam inteiros.
- **O capacete e o chapéu não vão juntos:** vestir um manda o outro para a mochila. Com o capacete, o cabelo grande fica
  na versão de baixo do chapéu.
- **O pneu do colete** é a roda de carro: a receita gasta a mais gasta da mochila (a rasgada primeiro).
- Na morte, as peças ficam na mochila do chão, com as roupas. Elas vão no save do personagem, com o desgaste.
- O F1 (a aba "Habil."): "Proteção: nós", "Proteção: peças", "Mat. proteção" e "Gastar proteção".
- As caneleiras ficaram largas (parecem baldes, por cima da calça): o Pedro decide depois.

### O fogo (11/11/2026, a etapa 4)

Três peças novas e os efeitos na tocha, na mente e nos zumbis. Os números ficam no `Player/FogoDoJogador.cs` (e os raios
dos zumbis no `AjustesDeZumbi`, "O fogo, etapa 4"). Os minutos são de relógio (o tempo jogado), não do relógio do jogo.

| Nó | Peça | Onde | Gasta | O que faz |
|---|---|---|---|---|
| Tocha duradoura (`f1`), 2 níveis | — | — | — | a tocha dura 50% mais por nível |
| Lampião (`f2`) | lampião (2 × 2) | mesa de oficina | 3 sucatas, 1 garrafa de refrigerante, 1 combustível | na mão: luz quente de 10 m, os zumbis na beira a 4 m; 15 min por carga; a chuva não apaga; o clique só balança |
| Coquetel molotov (`f3`) | coquetel molotov (1 × 2) | bancada | 1 garrafa de refrigerante, 1 combustível, 1 pano | o clique arremessa; onde quebra, fogo de 2,5 m por 8 s |
| Fogueira portátil (`f4`) | kit da fogueira portátil (2 × 2) | bancada | 3 madeiras, 1 corda, 1 combustível | montada, queima 60 min e segura os zumbis a 8 m; o E recolhe |
| Fogo que pega (`f5`), 2 níveis | — | — | — | o molotov e a tocha queimam 30% mais tempo, e a área do molotov fica 20% maior, por nível |
| Calor do fogo (`f6`), 2 níveis | — | — | — | a menos de 4 m de um fogo aceso, a mente sobe 2 por hora por nível |
| ⭐ Muralha de fogo (`f7`) | — | — | — | o seu fogo segura os zumbis 50% mais longe, e eles fogem em pânico |

- **O combustível:** 1 álcool ou, sem ele, meio litro de gasolina do galão mais cheio. Não existe vidro como item: a garrafa
  é a de refrigerante.
- **O lampião:** aceso só na mão (no carro, escondido), gasta o combustível; "Reabastecer" (o botão direito na mochila)
  gasta 1 combustível. Largado da mão aceso, fica aceso no chão até acabar a carga, segurando os zumbis; pego de volta, vai
  apagado para a mochila. A chuva e a gosma não o apagam.
- **O molotov:** a garrafa sai da mão no quadro 8 do arremesso e voa em arco (olhando reto, uns 14 m; mais alto, mais
  longe). Ao quebrar, o fogo pega no chão: os zumbis dentro levam 25 por segundo e pegam fogo (continuam queimando 4 s depois
  de sair, 15 por segundo; quem mata ganha o XP), recuam até a beira de 8 m (como numa fogueira) e não caçam quem jogou só
  por isso; o Brutamonte não queima. Os moradores dentro levam 12 por segundo e saem correndo; os jogadores (até quem
  jogou) levam 8 por segundo. Na chuva, fora de um teto, o fogo dura bem menos (a chuva comum: menos da metade). Um molotov
  sem o Fogo que pega deixa os Ocos que estavam dentro com uns 65 de dano; com o Fogo que pega 2, eles morrem.
- **A fogueira portátil:** é uma fogueira como a de lenha (a chuva, o Long da fogueira, a mente, assar o peixe), com o modelo
  dela, que não aceita mais lenha. Não é montada na chuva fora de um teto. **O E recolhe:** acesa, volta o kit com a lenha
  que sobrou; apagada (acabou, a chuva ou a gosma), a lenha virou cinza e sobra a corda. Com a tocha apagada na mão, o E
  nela acesa reacende a tocha.
- **O Calor do fogo** conta qualquer fogo aceso perto: a fogueira, o lampião ou a tocha na mão e a área do molotov (o motivo
  "CALOR DO FOGO" no HUD da mente).
- **A Muralha de fogo** vale no fogo do jogador: a tocha e o lampião (6 m), a fogueira e o lampião largado que ele acendeu e
  o molotov que ele jogou (12 m). Quem está dentro foge 6 m além da beira, larga quem caçava e grita.
- O F1 (a aba "Habil."): "Fogo: nós", "Fogo: peças", "Mat. fogo" e "Molotov na frente".
- Os modelos (o `Fogo.fbx`, o `Loot_Etapa34.fbx` e os braços `Jogador_FP_Lampiao` e `_Molotov`) entram pelo menu Zombie City
  Cleaner > Habilidades > Montar o fogo, no `Resources/ItensDoFogo.prefab`: nenhuma cena nem o prefab do jogador mudaram.

### As armadilhas (11/11/2026, a etapa 5a)

Quatro peças montadas no chão (o botão direito no kit da mochila: "Montar no chão", a sombra verde ou vermelha, R gira, o
clique monta), como a barricada. Os números ficam no `Player/ArmadilhasDoJogador.cs`. A Barricada de tábuas (`t2`) e a
Fortaleza (`t7`) vieram na etapa 5b (veja "As barricadas e a Fortaleza"); desde então, as Estacas voltam a pedir a
Barricada de tábuas.

| Nó | Peça | Onde | Gasta | O que faz |
|---|---|---|---|---|
| Alarme de latas (`t1`) | alarme de latas (2 × 2) | bancada | 2 sucatas, 1 corda, 1 madeira | o barbante de 1,6 m: quem cruza em pé (o zumbi, o morador, o jogador andando; agachado passa) faz as latas tocarem: o som chega a 35 m, os zumbis a 12 m vêm ver e o dono recebe o aviso no HUD (até 60 m: "As latas tocaram: a 23 m, atrás, à direita"); rearma em 4 s; 12 toques, e o último arrebenta o barbante (sobra 1 sucata) |
| Barricada de tábuas (`t2`), 2 níveis | tábuas para barricada (3 × 1) | bancada, com a ferramenta | 3 madeiras, 6 pregos | pregada numa porta ou numa janela das casas (veja "As barricadas e a Fortaleza") |
| Tapete de pregos (`t3`) | tapete de pregos (3 × 1) | bancada, com a ferramenta | 1 madeira, 12 pregos | o zumbi que pisa leva 20 (uma vez por passagem) e anda na metade da velocidade por 4 s; o morador leva 20; o jogador andando ou correndo (o dono também), 6 na perna, e agachado passa; a roda do carro fura o pneu; 8 usos (o jogador não gasta), e gasto ele some (sobram 4 pregos) |
| Estacas (`t4`) | estacas (4 × 1) | bancada, com a ferramenta | 4 madeiras, 1 corda | seguram como a barricada; quem encosta na frente (o lado das pontas) leva 12 por segundo; a vida (100) cai com os zumbis encostados, na metade do ritmo da barricada; o Brutamonte as quebra, mas leva 60; quebradas, sobram 2 madeiras |
| Armadilheiro (`t5`), 2 níveis | — | — | — | as suas armadilhas têm +50% de usos e de vida (e de tempo na cerca) e +25% de dano por nível; no 2, o seu tapete de pregos não fere jogadores |
| Cerca de fogo (`t6`) | cerca de fogo (2 × 2) | bancada | 1 pano, 2 combustíveis | o galão com o pavio e um rastro de 4 m; acesa, o fogo corre do pavio até a ponta (1 s) e queima 90 s; os zumbis não atravessam, e quem atravessa queima como no molotov |
| ⭐ Fortaleza (`t7`) | — | — | — | as defesas do dono com o dobro da vida e o conserto sozinho; o alarme dele avisa de qualquer distância (veja "As barricadas e a Fortaleza") |

- **O Armadilheiro vale pelo nível de quem montou** (guardado na armadilha): no nível 2, o alarme tem 24 toques, o tapete 16
  usos, as estacas 200 de vida, a cerca queima 180 s, e o tapete tira 30 dos zumbis. O bônus de dano vale nos zumbis e nos
  moradores (o tapete, as estacas e o fogo da cerca); nos jogadores, o tapete tira 6 e o fogo 8, como sempre.
- **O E recolhe:** o alarme volta inteiro com mais da metade dos toques (com o que sobrou: montado de novo, tem só o resto);
  senão, 1 sucata. O tapete volta com os usos que sobraram. As estacas voltam com 60% ou mais da vida; senão, 2 madeiras. A
  cerca apagada volta; acesa, não dá.
- **A cerca de fogo acende** com o E e a tocha acesa ou o lampião aceso na mão, ou com o fogo encostando no rastro (o
  molotov, ou outra cerca acesa: postas em fila, uma acende a outra). Ela acende na chuva também, mas lá fora queima mais
  depressa (como a área do molotov). Uma parede, um móvel ou as estacas no caminho cortam o rastro. Os zumbis esperam na
  beira (a Muralha de fogo do dono os segura mais longe, e eles fogem em pânico) e só entram se a horda criar coragem. Queimada,
  ela some. A sombra de montar mostra o rastro, para a direita de quem monta (e as estacas começam com as pontas para longe).
- **No save:** as armadilhas montadas voltam com os usos e a vida; a cerca acesa não volta (o combustível queimou), a apagada
  volta.
- **O combustível** (o mesmo do fogo): 1 álcool ou meio litro de gasolina do galão mais cheio, por unidade. Não existe lata
  vazia como item: as latas do alarme saem da sucata.
- O F1 (a aba "Habil."): "Armadilhas: nós", "Armadilhas: peças" e "Mat. armadilhas".
- Os modelos (o `Art/Armadilhas/Armadilhas.fbx` e o `Loot_Armadilhas.fbx`) entram pelo menu Zombie City Cleaner >
  Habilidades > Montar as armadilhas, no `Resources/ItensDasArmadilhas.prefab` (com as barricadas da 5b): nenhuma cena
  nem o prefab do jogador mudaram.

### As barricadas e a Fortaleza (11/11/2026, a etapa 5b)

A Barricada de tábuas (`t2`) e a Fortaleza (`t7`): o ramo Armadilhas e defesas inteiro. Os números ficam no
`Player/ArmadilhasDoJogador.cs` (a seção do fim).

| Nó | O que faz |
|---|---|
| Barricada de tábuas (`t2`), nível 1 | as **tábuas para barricada** (3 × 1; na bancada, com a ferramenta: 3 madeiras e 6 pregos). Com elas na mochila, mirando uma porta ou uma janela de casa, **segurar o E por 3 s** prega a barricada do lado de quem prega (o martelo chama os zumbis a 15 m); o toque no E continua abrindo e fechando. A porta ou a janela fecha na hora e **não abre enquanto ela existir, dos dois lados** (a folha treme; a dica diz "Porta barricada"); ninguém arromba, e a janela barricada não deixa pular. Vida: a porta 150, a janela 100 |
| Barricada de tábuas (`t2`), nível 2 | +50% de vida (a porta 225, a janela 150) e **o conserto**: do lado dela, **segurar o clique por 2,5 s** com 1 madeira, 2 pregos e a ferramenta: +35% da vida máxima |
| ⭐ Fortaleza (`t7`) | as suas defesas (as barricadas de tábuas, a barricada solta e as estacas) **aguentam o dobro** e **se consertam sozinhas**: 5% da vida máxima por hora do jogo (uma hora = 1 minuto de relógio), com você a até 30 m e nenhum zumbi batendo nelas há 5 s. O seu **alarme de latas avisa de qualquer distância** (sem os 60 m) |

- **Gastar e quebrar:** os zumbis engajados encostados tiram 2,5 por segundo cada (como na barricada solta), o Brutamonte a
  derruba, e o golpe de arma branca de um jogador tira metade do dano da arma (dos dois lados: do outro lado, batendo na
  porta ou na janela). Abaixo de 50% ela aparece danificada (uma tábua solta, caída). Quebrada, some, sobram 2 madeiras no
  chão, e a porta volta a funcionar (fechada).
- **Desmontar:** do lado em que foi pregada, segurar o E por 2 s com a ferramenta na mochila: voltam as tábuas com 60% da
  vida ou mais; senão, 2 madeiras. Do outro lado, não dá (só quebrando).
- **O Armadilheiro vale nas barricadas** (as de tábuas e a solta): +50% de vida por nível, pelo nível de quem montou. A
  barricada solta mostra a vida em %, e desmontada com 60% ou mais volta inteira, como antes.
- **A vida fica com quem montou:** o nível da Barricada de tábuas, o Armadilheiro e a Fortaleza contam na hora de pregar
  (uma porta da frente com tudo no máximo: 150 × 1,5 × 2 × 2 = 900).
- **Os zumbis e as portas:** a porta fechada já barrava os zumbis (eles não abrem nem arrombam portas; "os zumbis forçando
  as portas" ficou para depois no `Plano_Portas`). Com a barricada por dentro, quem persegue o jogador bate nela pela porta;
  quando ela cai, a porta fechada continua segurando. Hoje, contra os zumbis, ela vale pouco além da porta fechada: vira
  defesa de verdade quando as hordas forçarem as portas e as janelas (a decisão é do Pedro).
- **No save:** a barricada volta na mesma porta ou janela (a casa vem da semente), com a vida, o lado e os níveis, e a porta
  fechada.
- O F1 (a aba "Habil."): "Armadilhas: nós" (com a Barricada de tábuas 2 e a Fortaleza), "Armadilhas: peças" (+2 tábuas e a
  barricada solta), "Mat. armadilhas" (as 5 receitas e um conserto), "Barricar a porta" e "Barricar a janela" (a mais perto,
  do seu lado, sem gastar as tábuas).
- O ícone e a foto das tábuas: o menu Zombie City Cleaner > Habilidades > Montar as tábuas da barricada (o modelo é o do
  `Loot_Armadilhas.fbx`, já no `Resources/ItensDasArmadilhas.prefab`).

### O Sucateiro e a sucata (09/11/2026, a 2ª leva da etapa 1b)

A sucata "é essencial" na fabricação das armas de sucata (a decisão do Pedro). De onde ela vem:

- **Os painéis de metal no chão:** a chapa ondulada amassada, a porta de carro solta e a placa caída, espalhados pelo
  mundo (nas ruas e calçadas das cidades, uns 2 a 4 por quarteirão; perto dos carros abandonados; na boca das garagens;
  dos lados dos postos; perto das barreiras da estrada; nos estacionamentos e terrenos baldios). No mapa pequeno da
  semente 7 são 641; no médio, 1.961. **Segurar o E por 3 s** (com a barra embaixo da mira) desmonta o painel: dá de 2 a
  4 sucatas (o que não cabe na mochila fica no chão), 1 de XP e faz o barulho do metal, que chama os zumbis até 25 m (como
  o pé de cabra). O painel some para todos e não volta: o save do mundo guarda os desmontados.
- **Desmontar itens com metal** (na mochila, o botão direito: "Desmontar"): leva 2 s nas coisas pequenas e 3,5 s nas
  armas e nas peças grandes, com a barra no rodapé da mochila (fechar a mochila interrompe). A munição, as pilhas, a
  lanterna e o que se usa toda hora não se desmontam.

  | Item | Vira |
  |---|---|
  | garrafa térmica | 2 sucatas |
  | ferramenta (o martelo e a chave), peças de metal, pé de cabra | 2 sucatas |
  | faca | 1 sucata |
  | machado | 2 sucatas e 1 madeira |
  | lança | 1 madeira e 1 sucata |
  | vela | 1 sucata |
  | bateria (com a ferramenta) | 2 sucatas |
  | radiador (com a ferramenta) | 3 sucatas |
  | macaco | 3 sucatas |
  | pistola (com a ferramenta) | 1 peça de metal e 3 sucatas |
  | submetralhadora (com a ferramenta) | 1 peça de metal e 5 sucatas |
  | rifle (com a ferramenta) | 2 peças de metal e 5 sucatas |
  | espingarda (com a ferramenta) | 1 peça de metal, 4 sucatas e 1 madeira |
  | sniper (com a ferramenta) | 2 peças de metal, 5 sucatas e 1 madeira |
  | pistola de sucata | 4 sucatas |
  | submetralhadora de sucata (com a ferramenta) | 8 sucatas |
  | rifle de sucata (com a ferramenta) | 7 sucatas e 1 madeira |

  As armas quebradas também se desmontam (a de fogo quebrada fica na mochila, esperando o kit de reparo). Não existe lata
  vazia nem panela como item: as latas de comida e de bebida somem ao comer e beber.
- **O saque:** às vezes, com as ferramentas (a bancada, as prateleiras e as estantes do porão e do depósito), de 1 a 3.
- **Os civis** levam às vezes de 1 a 3 sucatas, e os comerciantes dos acampamentos vendem a sucata ($ 4) na banca dos
  materiais.
- **A loja de caça e ferragens do Dia 0** vende a sucata a $ 3 (no máximo 24 por jogador).

**O Sucateiro** (o `k1`, 3 níveis): +1 sucata por ponto em cada desmontar (os painéis e os itens que já dão sucata; com
3 pontos, um painel dá de 5 a 7). O texto do menu da mochila já mostra a sucata com o bônus. O **Faz-tudo** (`k2`) passou a
pedir o Sucateiro, como na árvore aprovada; um save com o Faz-tudo e sem o Sucateiro continua com ele (o pedido só conta
para aprender, e o ponto ainda pode sair).

### Para quem programa (a árvore nova)

- **O catálogo** `Player/Habilidades.cs`: o `Habilidade` com os 74 nós (a ordem é o lugar dos 2 bits no save e na rede:
  não mudar), o `RamoDaHabilidade` com os 13 ramos, a tabela `Nos` (o ramo, a coluna, a fileira, os níveis, o nome, o
  efeito por nível, o que pede, se já tem efeito), `PontosDaFileira`, `PodeAprender` e `PodeTirar` (o motivo diz o que
  falta), `GastosAcima`, `FileiraAberta`, `Migrar`, a curva (`XpDoNivel`) e `Habilidades.Pontos(jogador, h)` (0 a 3; o
  `Tem` é "pelo menos 1").
- **O progresso** `ProgressoDoPersonagem`: o `xp`, o `aprendidas` (só a migração lê), os `niveis0..2` (2 bits por nó),
  o `avisoDaArvore`; `PontosEm`, `PorPontos`, `Gastos`, `Pontos` (os livres).
- **A rede e o save** no `InventarioJogador.Habilidades.cs`: `PedirAprender`, `PedirTirar`, `PedirVistoOAviso` (os RPCs
  conferem de novo no anfitrião) e o `DefinirProgresso` (a migração e a limpeza: só os nós com efeito, até o máximo, e não
  mais pontos que o nível). `VersaoDaRede` 12.
- **A tela** `TelaDoInventario.Habilidades.cs` (o botão direito entra pelo `Processar`, no `TelaDoInventario.cs`); o
  aviso embaixo da mira no `XpNaTela`. Para os testes: `AbrirNaAbaHabilidades(area)`, `PontoDoNo(h)` e `MouseDeTeste`.
- **Os ícones** (10/11/2026): `Art/UI/Habilidades/Resources/Hab_<Habilidade>.png` e `Hab_<Habilidade>_Cinza.png` (a cópia
  sem cor, dos trancados), pelo `Resources` (`TelaDoInventario.IconeDaHabilidade`). Para refazer: o
  `Blender/icones_habilidades.py` com `--saida <pasta>` (os `Hab_<id>.png`) e o menu Zombie City Cleaner/Habilidades/
  Importar os ícones (o `Editor/MontagemDosIconesDasHabilidades`: o nome do enum, os cinza e a importação, 256 com mipmap
  e BC7).
- **Os efeitos lendo o nível:** `ArmaJogador` (o Tiro forte, o Braço forte, a Mão firme, a Recarga rápida),
  `EfeitosDoCorpo` (a Mão firme), `FomeESede`, `InventarioJogador` (o Reparador, as Mãos hábeis, o Cuidadoso, o
  Econômico), `Carro.Desgaste`, `JogadorRede.RaioDoMeuTiro`, `ZombieManager` e `ZombieManager.Clima` (os Passos leves),
  `Sobrevivente.Cabeca` (o Discreto), `PortasDoMundo.Arrombar` (as Mãos leves), `PecasDosCarrosDoMundo` (o Alarme mudo),
  `VidaJogador.Infeccao`, `VidaJogador.Mente`, `VidaJogador.Corpo` (o Curativo bem feito, o Farmacêutico),
  `MobiliaNovaDoMundo.Saque` (o Sortudo), `SobreviventesDoMundo.DoAbutre` e `PescaJogador`; o tempo do curativo no
  `Habilidades.TempoDoCurativo`.

## As decisões do Pedro

- **O XP vem das ações**, num total só; **cada nível dá 1 ponto livre**, gasto onde ele quiser.
- **Não dá para pegar tudo:** 45 habilidades e 30 níveis ("vai ficar a critério do jogador").
- **O nível 30 chega em umas 12 horas de jogo real**, com números pequenos ("só não quero que o XP fique muito alto").
- **Ao morrer, nada se perde** (até o sistema futuro dos NPCs do bunker).
- **3 áreas × 3 ramos**, cada ramo com 4 habilidades em sequência e uma ponta especial.
- **A ligação direta virou a ponta do Ladrão.** Sem ela, o carro só liga com a chave.
- **O Sangue frio** (matar não abala) é a ponta do Assassino; **a sanidade** fica no ramo Saúde.
- **O Camelo** deixa a fome e a sede 30% mais lentas.
- **As receitas novas** do Craft são destravadas pela árvore.
- **A mesa de oficina já vem no bunker.**
- **Na água:** nadar, com o fôlego e o afogamento.
- **O caiaque tem dois lugares** (um rema, o outro pesca).
- **O lago** com uma mata de outono em volta, como no print, sem a neve.

## O XP

| Ação | XP |
|---|---|
| Matar um Oco (a estátua de pedra: 1) | 2 |
| Matar um Corredor | 3 |
| Matar um Long | 5 |
| Matar um Brutamonte | 15 |
| Matar um sobrevivente (um militar: 20) | 10 |
| Mais um dia vivo (o amanhecer, com 12 h vividas) | 25 |
| Um curativo em você (no amigo: 4) | 3 |
| A infecção curada pelo antibiótico | 10 |
| Criar um item | 1 a 6 (1 mais metade do que a receita gasta) |
| Pegar um item das casas | 1 a 4 (pela raridade) |
| A ligação direta, na primeira vez em cada carro | 8 |
| Pescar um peixe (o dourado: 12) | 4 |
| Cortar uma árvore | 3 |

- **A curva:** do nível 1 para o 2, 50 de XP; cada nível pede 10 a mais (330 do 29 para o 30). O nível 30 pede 5.510 no
  total.
- **Os pontos:** o nível 1 já dá o primeiro, e cada nível seguinte dá mais um.
- **No Dia 0 não se ganha XP.**
- Na tela, o XP aparece embaixo da mira ("+3 XP · curativo"; o mesmo motivo seguido soma na mesma linha), e o nível novo
  aparece maior ("NÍVEL 5 · +1 ponto · K: habilidades").

## As habilidades (todas com efeito; a árvore de 06/11/2026)

### Combate
- **Guerreiro:**
  1. Tiro forte: +10% de dano com arma de fogo;
  2. Recarga rápida: recarrega 15% mais rápido (a animação dos braços também);
  3. Braço forte: +15% de dano com arma branca;
  4. Mão firme: 25% menos coice e menos tremor na mira;
  - ⭐ **Mira de aço:** a mira não treme (nem com dor, pânico, abstinência ou braço machucado).
- **Assassino:**
  1. Passos leves: os zumbis ouvem você correndo (e pisando nas poças) pela metade da distância;
  2. Pelas costas: o golpe de arma branca pelas costas de um Oco que não está caçando você mata na hora;
  3. Discreto: os sobreviventes enxergam você 30% menos longe e ouvem os seus passos pela metade;
  4. Tiro abafado: o barulho do seu tiro chega 30% menos longe;
  - ⭐ **Sangue frio:** matar não abala a mente.
- **Ladrão:**
  1. Borracheiro: remenda o pneu furado com o kit de reparo (só quem tem) e anda mais rápido carregando o pneu (80% da
     velocidade, no lugar de 55%);
  2. Mãos leves: arromba 30% mais rápido; o pé de cabra não chama os zumbis e a gazua nunca quebra;
  3. Chaveiro: abre as portas e as janelas trancadas com um grampo (sem ferramenta e sem barulho), em 6 s;
  4. Alarme mudo: abrir o capô e mexer nos pneus quase nunca dispara o alarme (85%; o tiro ainda dispara);
  - ⭐ **Ligação direta:** liga os carros sem a chave (o mini-game dos fios).

O Chaveiro mudou de ideia: as chaves dos carros não saem no saque (ficam só no bunker), então ele abre as portas.

### Resistência
- **Saúde:**
  1. Mãos de enfermeiro: os curativos levam 25% menos tempo;
  2. Curativo bem feito: a bandagem e a atadura devolvem +10% à parte;
  3. Imunidade: a ferida infecciona com 40% menos chance;
  4. Nervos de aço: a mente cai 30% menos;
  - ⭐ **Médico de campo:** a bandagem comum protege como a antisséptica, e o kit médico leva metade do tempo.
- **Fome e sede:**
  1. Estômago forte: a fome cai 15% mais devagar;
  2. Cantil: a sede cai 15% mais devagar;
  3. Fôlego: correr gasta menos (1,25 vez, no lugar de 1,6);
  4. Bem nutrido: comer e beber enchem 25% mais;
  - ⭐ **Camelo:** a fome e a sede caem 30% mais devagar (no lugar dos 15%).
- **Recursos:**
  1. Cuidadoso: as armas se desgastam 20% menos;
  2. Econômico: as pilhas da lanterna e a gasolina (de quem dirige) duram 25% mais;
  3. Reparador: 30% de chance de o kit de reparo não ser gasto (no conserto da arma e no remendo do pneu);
  4. Farmacêutico: o analgésico e o xarope agem 50% mais tempo;
  - ⭐ **Remendão:** as suas armas nunca emperram (só param quando quebram).

### Sobrevivência
- **Craft:**
  1. Faz-tudo: destrava na bancada a gazua (2), a faca e a mochila média;
  2. Mãos hábeis: ao criar, 25% de chance de sair em dobro (o craft é na hora, então "mais rápido" virou isto);
  3. Artesão: destrava na mesa de oficina o machado, o pé de cabra, a mochila grande e a lanterna;
  4. Aproveitador: ao criar, 20% de chance de devolver um material;
  - ⭐ **Armeiro:** na mesa de oficina, a munição de todos os calibres com a pólvora (9mm ×15, calibre 12 ×8, 5,56 ×12,
    7,62 ×10).
- **Sortudo:**
  1. Olho bom: as pilhas achadas nas casas vêm com 25% a mais, e às vezes (10%) acha mais um item comum;
  2. Garimpeiro: às vezes (8%) acha também um incomum;
  3. Abutre: quem você mata deixa mais coisas na mochila (comida, remédio, munição);
  4. Pé de coelho: às vezes (4%) acha também um raro;
  - ⭐ **Faro:** os itens raros a até 15 m aparecem marcados na tela (só para você), até dentro das gavetas.
- **Pesca:**
  1. Paciência: o peixe morde 40% mais rápido;
  2. Isca fina: 40% de chance de não gastar a isca;
  3. Peixe grande: mais tucunarés e tilápias, menos lambaris;
  4. Noite de pesca: a noite e a chuva ajudam a pescar (sem ela, atrapalham);
  - ⭐ **Mestre pescador:** às vezes (6%) morde o dourado, raro e caro.

## Na mochila: a aba HABILIDADES (a tecla K; a de 06/11/2026)

- Em cima: o nível, os pontos para gastar e a barra do XP até o próximo nível.
- Os botões das 3 áreas. Cada área mostra os 3 ramos, com as 5 habilidades de cima para baixo (a ponta com a estrela).
- **As cores:** verde, aprendida; amarelo, dá para aprender agora; apagada, trancada (falta a anterior).
- O clique escolhe; a coluna da direita mostra o que ela faz, o botão **APRENDER (1 PONTO)** e de onde vem o XP.

## A água: nadar, o fôlego e o afogamento

- Com a água acima do peito, ele nada: boia com os olhos um pouco acima da água, anda devagar para onde olha (1,9 m/s;
  com o pneu, metade), **o Espaço sobe e o C (ou o Ctrl) mergulha**. Sai da água com ela abaixo de 1,05 m.
- Nadando, as mãos nadam: sem tiro, sem golpe, sem comer.
- **O fôlego:** com os olhos embaixo da água, cai de 100 a 0 em 30 s; fora dela, volta em 4 s. A barra "FÔLEGO" aparece
  embaixo da mira enquanto ele não está cheio (vermelha no fim, "SEM AR: SUBA!").
- **O afogamento:** sem fôlego, a vida cai 8 por segundo (do tronco) até ele subir ou morrer ("Afogou-se.").
- Embaixo da água, a tela fica verde-azulada e escura (mais escura no fundo).
- Vale no lago do mundo sorteado e no lago, no rio e no brejo do mapa fixo.
- **Desde 07/11/2026, parece nado** (a pedido: "atualizar a parte de nadar"):
  - **A arma vai para as costas sozinha** ao entrar na água funda, e os braços nadam (a braçada de peito, feita no
    Blender). Saindo, a mão volta para o que estava. Os outros também veem o corpo nadando, sem a arma na mão.
  - **A câmera fica rente à água** e sobe e desce um pouco a cada braçada.
  - **Shift (para a frente) nada mais rápido** (2,8 m/s em vez de 1,9), gastando o fôlego: a barra aparece e esvazia em
    45 s. Acabando, ele volta ao nado normal (sem se afogar, na tona).
  - **A mochila cheia pesa:** a grande lotada deixa o nado 30% mais lento.
  - **Os sons e a água:** o tchibum ao entrar, o barulho de cada braçada, as ondinhas e os respingos em volta, a água
    escorrendo ao sair e a puxada de ar depois de muito tempo embaixo. Embaixo da água, tudo fica abafado. (Os sons são
    provisórios: para trocar, um `.wav` com o mesmo nome em `Assets/_Game/Audio/Resources/Agua/`.)
  - **Os zumbis param na beira:** eles não entram na água mais funda que uns 30 cm; atrás de quem nada, ficam esperando
    na margem.

## O caiaque de dois lugares

- Fica na beira do lago, do lado do bunker (a direção do centro do lago à partida), de lado para a margem. Um por mundo;
  vai no save (onde ficou). Num mundo de antes, aparece uma vez ao carregar.
- **E no casco:** entra no lugar livre. **Atrás rema** (W e S para a frente e para trás, A e D viram; até 3,2 m/s, e
  freia devagar sem remar). **Na frente, as mãos ficam livres:** a vara de pesca e a arma funcionam.
- O mouse olha em volta do banco; **o E sai**: na terra, se houver perto; senão, na água (nadando).
- Ele boia e balança um pouco; encalha onde a água fica rasa na proa. O remo gira na mão de quem rema (sem ninguém,
  fica deitado no casco).
- A rede é a do carro: quem rema é o dono (move o caiaque); sem ninguém, o anfitrião.
- **Desde 07/11/2026:**
  - **Pescar parado, mesmo sozinho:** quem rema larga o remo no casco quando o caiaque para (uns 1,2 s parado) e fica
    com as mãos livres: a vara de pesca e a arma funcionam. W ou S pegam o remo de novo (e recolhem a linha).
  - **A remada de verdade:** os braços remam de um lado e do outro na primeira pessoa; os outros veem o corpo remando, com
    o remo nas duas mãos. Cada pá faz barulho e respinga ao entrar na água, a proa vai e vem um pouco e fica um rastro de
    ondinhas atrás.
  - **No tempo ruim ele balança mais** (o vento e a chuva), mas não vira.
  - **O compartimento:** uma caixinha de 2 × 2 na escotilha de trás. De fora, E nela abre o inventário com ela ao lado
    (arraste os itens); sentado no caiaque, o TAB abre o mesmo. Ela vai no save do mundo.
  - Sentado, a pose é a do caiaque: as pernas esticadas debaixo do convés.

## A pesca

- **A vara vai na mão** (no lugar da lata, com a malha nova do Blender). Com isca na mochila, **o clique mirando a água
  funda** (até 18 m, com pelo menos 0,8 m de fundo) lança: a boia cai na água com a linha.
- Depois de uns 6 a 20 s, **o peixe morde:** a boia afunda e é preciso clicar em 1,2 s. Aí vem **a puxada:** o ponteiro
  corre e o clique precisa cair na faixa verde (mais estreita no peixe maior). Errou: o peixe escapa com a isca.
- Clicar esperando recolhe a linha (sem perder a isca). Trocar de item ou se afastar também recolhe.
- **Os peixes** (comem-se assim mesmo; vendem nos comerciantes): o lambari (15 de fome), a tilápia (30), o tucunaré
  (50; 2 células) e o dourado (45, raro e caro; só com o Mestre pescador).
- Na margem ou no caiaque (o lugar da frente). A boia e a linha só aparecem para quem pesca.
- **Refeita em 07/11/2026** (a pedido: "a linha está saindo fora da ponta da vara e não funcionando muito bem"):
  - **A linha sai da ponta da vara**, na primeira pessoa e no corpo, e cai um pouco quando está frouxa.
  - **O arremesso pela força:** segure o clique (a barra enche) e solte; a boia vai de 4 a 18 m para onde você olha.
  - **As beliscadas:** antes da mordida, a boia às vezes treme ("Beliscando..."). Clicar nela espanta o peixe: espere a
    boia afundar. Clicar sem beliscada recolhe a linha.
  - **A puxada pela tensão da linha:** segure o clique para recolher; quando o peixe puxar ("O PEIXE ESTÁ PUXANDO"),
    solte. Tensão demais (o vermelho) arrebenta a linha; frouxa demais por 2 s, o peixe solta; e longe demais, ele leva a
    linha. O peixe maior puxa mais forte e por mais tempo, e cansa.
  - **Onde e quando:** os peixes maiores ficam na água mais funda (o meio do lago, de caiaque). Eles mordem mais rápido
    de manhã cedo, no fim da tarde e com chuva, e mais devagar de noite (a Noite de pesca muda isso).
  - **Os outros jogadores veem** a boia voando, boiando, afundando e correndo com o peixe, e a linha da sua vara.
  - **O peixe assado:** perto de uma fogueira acesa, a aba CRIAR assa o peixe em filés (o lambari dá 1, a tilápia 2, o
    tucunaré e o dourado 3; cada filé mata 20 de fome). **O peixe cru pode fazer mal:** 40% de chance de enjoo, que
    tira 12 de vida aos poucos.

## O machado e as árvores

- **5 golpes de machado no tronco** derrubam a árvore: ela cai (para longe de quem cortou) e vira toco, para todos.
- No pé dela ficam **as toras** (de 2 a 5, pelo tamanho da árvore); o "rachar" com o machado (CRIAR) faz madeira.
- A árvore caindo faz barulho (chama os zumbis de perto). As cortadas ficam no save do mundo.
- Só as árvores da floresta (não as da cidade, nem os troncos caídos).

## A mesa de oficina e as bancadas

- **A mesa de oficina fica na garagem do bunker** (no lugar da bancada que havia lá). Ela vale como bancada e faz as
  receitas maiores (o Artesão e o Armeiro).
- **As bancadas do bunker novo e das casas não funcionavam** (eram só o desenho: o componente só existia na cena
  antiga). Agora todas funcionam.
- Na aba CRIAR, a receita mostra a habilidade que pede (entre colchetes) e "na mesa de oficina".

## A pólvora e os itens novos

- **A pólvora** (incomum, com a munição e na oficina; $14 nos comerciantes): a munição do Armeiro.
- **Os peixes:** o lambari, a tilápia, o tucunaré e o dourado.

## A mata de outono do lago

Depois da beira do lago (o salgueiro, a bétula), até mais ou menos o dobro do raio dele, a mata vira **outono**: bordos
vermelhos e laranja, bétulas e pinheiros escuros, como no print (sem a neve).

## Os testes (F1, a aba "Habil.")

- O nível, o XP e os pontos.
- Os botões de nível: +50 XP, +1 nível, Nível 10, Nível 30, Esquecer todas e Tudo do zero.
- Os botões do lago e da oficina: Ir ao caiaque, Vara + 10 iscas, Machado, e Pólvora + metal (com a ferramenta).

## Os modelos (o Blender, em segundo plano)

- **A vara refeita** (a pedido: "achei um pouco ruim"), **os 4 peixes e a pólvora** no `Blender/loot.py` (o Loot.fbx, os
  ícones e as fotos). A vara tem 2 m, com o molinete; a origem é a pegada e a ponta fica em +Z (0, 0, 1,635). Na mão, ela
  aponta para onde o jogador olha, uns 35° acima (`Player/VaraNaMao.cs`).
- **O caiaque e o remo** (`Blender/caiaque.py`: 4,6 m, amarelo, as duas aberturas com almofada; o remo de pás
  vermelhas), **a boia** (`Blender/pesca.py`, no jogo com o dobro do tamanho, para aparecer de longe) e **a mesa de
  oficina** (`Blender/oficina.py`: o tampo de 2 × 0,8 m, a morsa, o painel com as ferramentas, as gavetas).
- **Na Unity:** "Itens > Montar só o saque" e "Itens > Escrever os itens do saque" (os itens novos), e "Veículos > Montar
  o caiaque, a mesa de oficina e a boia" (os prefabs no `Assets/_Game/Resources`, o material "Paleta" trocado pelo
  `Cidade.mat`, o caiaque na lista dos prefabs da rede). No modelo, o Assento_0 é o da frente; no jogo, o lugar 0 (quem
  rema) é o de trás.
- **A vara no saque das casas:** com 2 m, ela não cabe mais nos lugares pequenos dos móveis; continua na loja do Dia 0 e na
  receita (2 madeiras e 2 fios).

## Para quem programa

- **O catálogo:** `Player/Habilidades.cs` (os enums, o `ProgressoDoPersonagem`, a curva, o `PodeAprender`, os textos, o
  XP de cada ação, o `TempoDoCurativo`, `Habilidades.Tem`). A ordem do `Habilidade` é o bit do save: não mudar.
- **O progresso:** `Player/InventarioJogador.Habilidades.cs`. A tela: `TelaDoInventario.Habilidades.cs`, `XpNaTela.cs`,
  `FaroNaTela.cs`. O F1: `UI/MenuDoDesenvolvedor.Habilidades.cs`.
- **A água:** `Player/Agua.cs` (o nível e a fundura num ponto, nos dois mundos), o nado no
  `MovimentoPrimeiraPessoa.Nadar`, o fôlego no `VidaJogador.Agua.cs` (anfitrião) e a tela no `AguaNaTela.cs`.
- **O caiaque:** `Vehicles/Caiaque.cs` (os lugares, a rede, o remar e o remo), `Vehicles/PortaDoCaiaque.cs` (o E no
  casco), `Player/PassageiroDoCaiaque.cs` (o corpo no banco, o olhar, sair), `MundoDaPartida.PorOCaiaque` e o save
  (`MundoSalvo.caiaque`, `caiaques`). O prefab vem do `Resources/Caiaque` (a montagem:
  `Editor/MontagemDoLago.cs`, menu Zombie City Cleaner > Veículos > Montar o caiaque, a mesa de oficina e a boia).
- **A pesca:** `Player/PescaJogador.cs` (no dono) e `InventarioJogador.Pesca.cs` (no anfitrião). A vara na mão:
  `Itens.NaMao` (a vara no lugar da lata) e o clique no `ArmaJogador`.
- **As árvores:** `World/FlorestaDoMundo.Corte.cs` (a chave de cada árvore, o toco, a queda) e
  `World/MundoDaPartida.Arvores.cs` (a lista da rede, o corte no anfitrião, as toras); o golpe no
  `ArmaJogador.GolpeNaArvore`; o save `MundoSalvo.arvoresCortadas`.
- **A oficina:** `Loot/Bancada.oficina`, `Receita.oficina` e `Receita.habilidade`, `BunkerDoMundo.PorAMesaDeOficina`
  (o prefab do `Resources/MesaDeOficina`), e as bancadas que agora funcionam (`MobiliaNovaDoMundo.Novo`).
- **O outono:** `Mancha.Outono` no `Mundo/Gerador/Floresta.cs`.
- **A rede:** `SessaoOnline.VersaoDaRede` 8.
