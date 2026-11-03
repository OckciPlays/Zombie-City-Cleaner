# 13 – Os sobreviventes e os acampamentos (02/11/2026)

O mundo agora tem outros vivos:
- a etapa 2 do plano de 02/11/2026: os grupos de busca armados que andam pelas cidades;
- a etapa 3: os acampamentos com os comerciantes ([mais abaixo](#os-acampamentos-a-etapa-3));
- a etapa 4: o carro quebrado na estrada, com dois sobreviventes trocando o pneu ([mais abaixo](#o-carro-quebrado-a-etapa-4)).

Depois da primeira partida com os amigos (03/11/2026), os civis passaram a usar armas brancas e a levar menos munição, e
chegaram os militares, nas bases militares do mapa: veja o [15 – Os ajustes da primeira partida](15-Ajustes-da-Primeira-Partida.md).

O que o Pedro pediu, em resumo:

- todo armado que a gente encontra na cidade é hostil;
- grupos de 6 a 8, de dia e de noite, mais vezes nas cidades grandes;
- vida 100, e o tiro na cabeça mata com um tiro só, de qualquer arma;
- à noite saem de lanterna; no tiroteio apagam, e só acendem de novo com zumbi perto;
- se protegem, a mira depende da arma (a sniper acerta de longe), e o último que sobra foge;
- chamam zumbis como os jogadores (os tiros e o alarme dos carros), e todos os zumbis vão atrás deles;
- quem morre deixa a mochila: dá para ver o que ela tem ao lado da nossa, pegar e trocar de mochila;
- os mortos continuam mortos;
- as falas aparecem escritas na tela, com um som saindo das letras, sem voz;
- e a decisão do save: **quem morre deixa a mochila onde caiu.**

Os detalhes técnicos estão no [`CLAUDE.md`](../CLAUDE.md) da raiz, na seção "Os sobreviventes".

## Os grupos de busca

- **Onde e quando** (aumentado em 02/11/2026, a pedido):
  - **a cidade grande tem 6 grupos**, cada um morando numa parte dela (a cidade dividida em 6 fatias em volta do meio); a
    cidade pequena e a vila têm 1. Cada grupo é sorteado pela semente do mundo (as armas, as roupas, quantos são);
  - quando alguém entra numa parte da cidade, o grupo dela aparece depois de um tempo, fora da vista, a uns 40–52 m (de
    20 s a 1 min na cidade grande; de 1,5 a 4 min na pequena);
  - **até 2 grupos no mundo ao mesmo tempo**, e no máximo um novo a cada 15 s;
  - longe de todos os jogadores (75 m), o grupo vai embora e volta mais tarde, com quem estava vivo (de 4 a 8 min na
    cidade grande; de 10 a 20 min na pequena);
  - um grupo inteiro morto é trocado por outro 2 dias de jogo depois.
- **As armas** (03/11/2026, a pedido: os civis "com armas brancas, tacos, facas, pistolas, raramente uma submetralhadora"):
  o líder com a pistola (às vezes a submetralhadora); os outros, metade de arma branca (a faca, o bastão com pregos, o
  machado ou a lança), 40% a pistola e 10% a submetralhadora (no máximo uma por grupo). Antes, rifles, a espingarda e até a
  sniper.
- **A mochila de cada um** (03/11/2026, a pedido: "o loot dos NPC está muito alto"): o pente de 40% a 100% e, em menos da
  metade deles, uma caixa com 3 a 12 balas; de 1 a 3 comidas e de 1 a 2 bebidas, às vezes remédio, materiais, pilhas e uma
  faca; quase sempre uma mochila (pequena, média ou grande) e algum dinheiro. Antes, de 2 a 4 caixas cheias.

## Como eles se comportam

- **Explorando:** o líder vai a um lugar da cidade a 20–40 m e os outros vão atrás em fila dupla. À noite, de lanterna.
- **O que eles percebem:**
  - **a visão:** de dia até 50 m; à noite 15 m, ou 35 m se a lanterna do jogador estiver acesa, ou 25 m se ele estiver
    embaixo de um poste. É para a frente (75° para cada lado), e a parede tapa;
  - **os passos:** quem anda a menos de 9 m é ouvido de qualquer lado, até atrás de uma parede; correndo, a 20 m. Parado,
    só encostado;
  - **os tiros** dos jogadores até 70 m, e o tiro que levam.

  Quem percebe avisa o grupo, e todos passam a lutar.
- **O tiroteio:**
  - cada um procura uma proteção (um ponto a 2–14 m onde o cenário tapa a linha até o inimigo), espera, sai para o lado,
    atira em rajadas e volta;
  - sem proteção, anda de lado enquanto atira, sem chegar a menos de 10 m;
  - recarrega com as caixas da mochila; **sem munição, foge**;
  - sem ver o inimigo, avança para onde ele foi visto. Sem vê-lo por 30 s, o grupo volta a explorar;
  - o último que sobra foge e some.
- **A mira:** é a da arma, pior de longe, no escuro, andando e contra quem corre. A sniper quase não espalha. Logo que ele
  levanta a arma, o tiro espalha o dobro (assenta em 1,5 s), e ele só atira meio segundo depois de começar a mirar.
- **O dano no jogador** é 70% do da arma (as armas foram medidas para os zumbis: a espingarda de perto, 115, matava com um
  tiro; agora tira 80).
- **Os zumbis:** os zumbis perto levam tiro, em qualquer estado. Muito perto, ele recua (de lado, se estiver num tiroteio). A
  lanterna dele afasta os zumbis como a do jogador. Os tiros chamam os zumbis, e todos os tipos (o Oco, o Brutamonte, o
  Corredor e o Long) vão atrás deles e os mordem.

## O tiro do jogador neles

| Onde | Dano |
|---|---|
| Na cabeça (tiro) | morre, com qualquer arma |
| No tronco | o da arma (pela distância) |
| Nos braços e nas pernas | metade |
| Golpe na cabeça (corpo a corpo) | 2,5 vezes, sem matar na hora |

Na espingarda, os chumbos contam juntos (um acerto por pessoa, na melhor parte que os chumbos pegaram), como nos zumbis. O
sangue aparece na hora para quem atira, e o anfitrião confere cada acerto.

## A morte e a mochila

- **O sobrevivente que morre** desaba no chão. O corpo do jogador não tem uma animação de morte, então a queda é com física:
  o corpo cai mole, empurrado pelo tiro que matou, e em 8 s para onde caiu. A mochila dele fica ao lado, com a arma (e o
  que estava no pente) e o que ele levava.
- **A nossa morte:** a mochila vestida, com tudo dentro, e o pneu das mãos ficam onde a gente caiu ("a mochila de Pedro").
  A gente volta na cama sem ela, com a roupa e o dinheiro. No Dia 0 não se perde nada.
- **A tela do saque** (E na mochila no chão):
  - a nossa grade à esquerda e a dela à direita;
  - um clique num item dela pega; num nosso, guarda lá;
  - **Pegar tudo**, **Pegar $** e **Trocar de mochila**, só quando a dela é maior: a dela vem para as nossas costas, cheia,
    e a nossa fica no chão com o que tinha;
  - fecha com ESC, E ou TAB, ou quando a gente se afasta.
- A mochila vazia (sem itens, sem dinheiro e sem mochila) some.

## As falas

As frases curtas aparecem escritas em cima da cabeça, letra a letra, com um bipe por letra. Cada um tem o tom dele, e não
tem voz. As situações: o contato, atirando, recarregando, os zumbis, um que caiu, fugindo, o inimigo sumiu e "para aí".

## O que fica no save

- **Os grupos de cada cidade:** a geração e quem está vivo (os mortos continuam mortos), e o dia em que o grupo todo morreu.
- **As mochilas no chão:** as dos sobreviventes e as dos jogadores que caíram, com tudo dentro e o dinheiro.

## Como testar (F1)

A terceira aba no canto do painel do F1, **"Gente"**:

- **Grupo perto:** o grupo da cidade onde você está (fora de uma cidade, um de teste), a uns 20 m e à vista. Se o grupo
  dessa cidade já tem mortos, vêm só os vivos.
- **Tirar todos:** o grupo vai embora (sem morrer).
- **Grupos sozinhos:** liga e desliga os grupos que aparecem sozinhos.

Em cima, quantos estão vivos e caídos e o que o grupo está fazendo.

## Conferido no Editor (02/11/2026, um jogador)

- **De dia, numa rua da cidade grande, um grupo de 6 a 20 m:**
  - eles viram o jogador e foram para as proteções;
  - espiaram e atiraram em rajadas, recarregaram, e o último fugiu;
  - o jogador, parado no meio da rua, levou de 50 a 140 de dano em 30 s;
  - as partes do corpo pegaram o tiro (a mira na cabeça pega a cabeça).
- **À noite, com 10 a 12 zumbis do outro lado do grupo:**
  - eles acenderam as lanternas, mataram os zumbis e apagaram as lanternas no tiroteio com o jogador;
  - os zumbis mataram um deles.
- **O tiro na cabeça mata;** o tronco e a perna somam certo (100 − 30 − 15 = 55); o golpe na cabeça vale 2,5 vezes.
- **A queda:** os corpos deitam (a cabeça fica a 10 cm do chão) e param em 8 s.
- **As mochilas:**
  - pegar tudo leva o que cabe;
  - a troca leva a mochila média cheia e deixa a nossa no chão;
  - a nossa morte deixa "a mochila de Teste" no lugar.
- **O save:** salvo, fora do Play e de volta, as três mochilas voltaram iguais e o grupo voltou com 4 dos 6.
- **Falta:**
  - testar com dois jogadores (o tiro de outra máquina, a tela do saque do amigo);
  - ouvir as falas e os bipes no jogo;
  - ver o comportamento com os grupos aparecendo sozinhos, numa partida normal.

## Os acampamentos (a etapa 3)

O que o Pedro pediu:
- um por cidade, fora dela, numa casa que já existia, com barricadas, fogueiras, guardas e comerciantes; maior na cidade
  grande;
- **a zona segura:** zumbi não nasce lá dentro; mas atirar num deles deixa o acampamento inteiro contra a gente, na hora;
- **os comerciantes:**
  - ficam parados, cada um vende uma coisa, só com dinheiro;
  - vender rende sempre menos que comprar (compra por 20, vende por 10);
  - o estoque acaba e repõe a cada dia;
  - dá para roubar e matar.

### No mapa

- Um acampamento por cidade grande e por cidade pequena (as vilas não têm). Na semente 7, média, são 6.
- Fica ao lado da primeira rodovia que sai da cidade, um pouco depois do posto, no chão mais liso que achar. O portão
  olha para a rodovia.
- O chão dele é aplainado (o gerador do mundo passou para a **versão 11**: os mundos salvos antes avisam que o mapa mudou).
- No mapa (M), a barraca verde-oliva e o nome: "Acampamento de São Bento".

### Como ele é

| | Pequeno | Grande |
|---|---|---|
| A cerca | 36 × 36 m | 54 × 54 m |
| A casa | térrea | sobrado |
| As bancas (comerciantes) | 3, de categorias sorteadas | 7, uma de cada |
| Os guardas | 2, no portão | 4: 2 no portão e 2 nos cantos de trás |
| Os moradores | de 2 a 5 | de 2 a 5 |
| As fogueiras | 1 | 2 |

- **A cerca:** pedaços de alambrado (com o arame farpado para fora), muros de sacos de areia e contêineres, e o portão
  entre duas barreiras de concreto.
- **A casa:** com todas as janelas tapadas com tábuas.
- **Dentro:** o chão de terra batida, as bancas (o balcão, as caixas, a lona colorida, a placa escrita e uma lâmpada), as
  barracas, as fogueiras com tocos em volta, os barris e as caixas.
- A construção aparece a uns 230 m (em cada máquina), e as pessoas a uns 35 m da beira. Montar leva uns 50 ms no grande e
  uns 20 ms no pequeno, uma vez.

### As pessoas

- **Os guardas:** rifle ou espingarda, roupa militar ou de policial. Vão e voltam no portão e nos cantos; à noite, de
  lanterna. Atiram nos zumbis que chegam a até 30 m.
- **Os comerciantes:** pistola, atrás da banca. Quem chega perto ouve "Precisa de alguma coisa?".
- **Os moradores:** pistola ou espingarda, em volta das fogueiras, andando um pouco.
- **Mirar** (com o botão direito) numa pessoa do acampamento por 2 s: "Abaixa essa arma."

### A zona segura

- Ela vai até 15 m além da beira do acampamento.
- Lá dentro, zumbi não nasce e não vaga.
- O zumbi que persegue alguém desiste quando a pessoa entra na zona (como no sol).
- Um barulho lá dentro só leva os zumbis até a beira.

### O comércio

**E no comerciante** abre a tela:
- à esquerda, o que ele vende: o estoque de hoje, o preço e "Comprar";
- à direita, o que você tem e ele compra (só a categoria dele) e "Vender";
- em cima, o seu dinheiro.

| A categoria | O que vende (e compra) |
|---|---|
| Comida | as latas, as bebidas, os salgadinhos e as barras |
| Armas | as 5 armas (uma de cada, às vezes nenhuma), a faca, o machado, o bastão e a lança |
| Munição | as 4 caixas de munição |
| Remédios | os remédios, a lanterna, as pilhas, a tocha e os kits de montar (fogueira, cama, barricada) |
| Peças | o pneu, a bateria, a vela, o radiador, o macaco, o galão, o kit de reparo e a ferramenta |
| Materiais | a madeira, as toras, o pano, a corda, a fita, as peças de metal, os fios, os pregos, a isca e a vara |
| Roupas | as mochilas, o coldre, a roupa velha e o pano |

**Os preços:**
- A base é o preço do Dia 0, vezes o fator do mundo de agora:
  - a munição e os remédios, 2,5 vezes;
  - a comida e a bebida, 2 vezes;
  - os materiais, 1,5;
  - as peças, 1,3;
  - as armas, 1,2.
- Exemplos: a caixa de munição de 9 mm custa $ 50; a lata de comida, $ 6; a madeira, $ 6; a pistola, $ 540.
- **Vender rende a metade do preço** e menos se o item estiver gasto: a arma pelo desgaste, a caixa pelas balas que
  sobraram, a peça pela carga, o galão pelos litros.

**O estoque:**
- Cada comerciante tem o dele: de 1 até o máximo de cada produto (as armas, uma ou nenhuma).
- **Acaba**, e **volta uma parte a cada amanhecer** (às 6 h).
- O que você vende vai para a banca dele, até o máximo.

**A compra:**
- O item vem novo: a arma com o pente cheio, a caixa cheia, a peça nova, o galão cheio.
- A mochila comprada vai para as costas, e a velha cai aos pés.
- O comerciante tem dinheiro sem fim.

### Quando fica hostil

- Basta ferir qualquer pessoa do acampamento (um tiro, um golpe). Fica hostil para todos os jogadores, **para sempre neste
  mundo** (fica no save).
- Os guardas e os moradores lutam como os grupos de busca (proteção, rajadas, recarga).
- Os comerciantes correm para dentro da casa e ficam lá. O comércio fecha.
- **O comerciante morto deixa a mochila com a metade do estoque dele.**
- Quem ficar sem munição se esconde na casa.
- Os mortos continuam mortos: quando você volta, só os vivos aparecem.

### No save

- Quais acampamentos estão hostis.
- Quem morreu em cada um.
- O estoque de cada comerciante.
- O dia da última reposição.

### Como testar (F1, aba "Gente")

Embaixo de "ACAMPAMENTOS", o mais perto (o nome, a distância, amigo ou hostil):
- **Ir ao portão:** leva você para a frente do portão dele.
- **Fazer hostil / Fazer amigo:** muda o mais perto.
- **Repor o estoque agora:** a reposição do amanhecer, em todos.

"Tirar todos" também tira as pessoas dos acampamentos (elas voltam quando você chegar perto).

### Conferido no Editor (02/11/2026, um jogador, semente 7, média)

- **O gerador:** 6 acampamentos, um por cidade (2 grandes e 4 pequenos).
- **A construção:** a cerca, a casa fortificada, as bancas com as placas, as fogueiras, as barracas e as luzes, de dia e de
  noite (fotos).
- **As pessoas:** apareceram as 14 do grande e as 9 do pequeno, cada uma no seu lugar; os guardas fizeram a ronda.
- **Os zumbis:**
  - à noite, havia 25 zumbis a até 90 m e nenhum dentro da zona;
  - os guardas, de lanterna, atiraram nos de fora.
- **O comércio:**
  - comprei uma madeira por $ 6 e vendi por $ 3;
  - no dia seguinte, o estoque voltou.
- **A hostilidade:**
  - um tiro na perna de um guarda deixou o acampamento hostil;
  - os comerciantes foram para a casa, e os guardas e os moradores atiraram.
- **O comerciante morto:** a mochila com a pistola, a munição e a metade do estoque de materiais.
- **O save:** salvo, fora do Play e de volta: o acampamento continuou hostil, o morador morto continuou morto (8 de 9) e o
  estoque voltou igual.
- **Falta:**
  - dois jogadores;
  - ver a tela do comércio e as falas no jogo;
  - testar o "Abaixa essa arma" com a mira de verdade.

## O carro quebrado (a etapa 4)

O que o Pedro pediu:
- um carro quebrado na estrada, com dois sobreviventes trocando o pneu;
- o estepe deles também furou: a gente dá um pneu e protege dos zumbis enquanto eles trocam;
- depois eles pagam e ficam ali;
- ou a gente mata os dois e leva o que eles têm.

### No mapa

- Um a cada 2 rodovias, e pelo menos 1 no mundo. Na semente 7, média, são 5 (para 10 rodovias).
- É um carro que já estava encostado no acostamento. Fica longe das cidades, dos acampamentos, dos postos e dos outros
  carros quebrados (700 m).
- **O estado do carro:**
  - o macaco erguido na roda de fora (a dianteira ou a traseira direita), sem o pneu;
  - as outras três rodas boas;
  - sem alarme e sem rastejante embaixo.
- O pneu furado fica deitado no chão, do lado.
- Enquanto um dos dois está vivo, o carro é deles: o capô, as rodas e o macaco dizem "O carro é deles".
- O mapa não mudou: a escolha dos carros tem um sorteio só dela, e o gerador continua na versão 11.

### Os dois

- **O mecânico:** de pistola, agachado na roda, com as mãos nela.
- **O vigia:** de rifle (às vezes de espingarda), de pé do lado de fora, de olho em quem chega.
- À noite, os dois de lanterna.

### Desconfiados

- **A 25 m:** "Ei! Fica aí!" (ou "Parado aí!").
- **Com a arma na mão** (de fogo, ou faca, machado, bastão e lança; a tocha não conta), a menos de 25 m: o vigia aponta a
  arma, sem atirar.
- **Mirar num deles** (com o botão direito): "Abaixa essa arma." Mirando por 2 s, eles atiram e ficam hostis.
- **Chegar armado a menos de 6 m de um deles:** "Guarda essa arma!". Em 3 s, eles atiram e ficam hostis.
- **Atirar num deles** também deixa os dois hostis.
- **Hostis**, eles lutam como os grupos, e ficam assim para sempre neste mundo.
- Olhando para eles de perto com a arma na mão, aparece: "Guarde a arma para falar com eles".

### A ajuda

1. E num deles, de mão vazia: "O estepe furou também. Tem um pneu aí?".
2. **O pneu:** com um de 30% ou mais, na mochila ou nas mãos, aparece "[E] Dar o pneu (80%)". Com um pior, eles dizem "Esse
   pneu não aguenta".
3. **A troca** leva 60 s. Só conta com o mecânico agachado na roda: se a gente se afasta e eles somem, a troca espera.
   - **As batidas de ferro** fazem barulho e chamam os zumbis de perto (a cada 6 a 10 s).
   - **Duas ondas de zumbis** vêm atrás do mecânico, aos 12 e aos 38 s:
     - de dia, 2 ou 3 (o sol para muitos no caminho); à noite, de 4 a 6;
     - mais 1 por jogador a mais;
     - nascem a 40–55 m, do lado contrário de quem está perto, fora da vista.
   - **Quem protege:** o vigia atira neles (até 30 m). O mecânico só larga a roda com um zumbi a menos de 6 m.
   - Durante a troca, mirar neles só leva bronca (a gente mira nos zumbis do lado deles). Acertar um deles, não.
   - **Se o mecânico morre:** o pneu fica na mochila dele, e a troca volta a esperar. O vigia pode trocar, com outro pneu.
4. **Pronto:**
   - a roda fica com o nosso pneu, e o macaco sai;
   - quem deu o pneu ganha de $ 60 a $ 140 e uma caixa de munição da arma dele (a da mão; senão, a primeira da mochila;
     sem arma, de 9 mm). Sem lugar na mochila, a caixa fica no chão.
5. **Depois** eles ficam ali, amigos. E: "Valeu pela ajuda."

### Se a gente mata

- As duas mochilas ficam no chão (com o pneu dado, se a troca não tinha acabado).
- O carro fica livre: dá para pôr um pneu e levar o macaco.
- Quem fica sem munição foge e não volta.

### No save

- O estado de cada um: esperando, trocando (com o pneu e o tempo que falta), ajudados, hostis ou mortos.
- Quem morreu.

### Como testar (F1, aba "Gente")

- **"CARRO QUEBRADO":** o mais perto, com a distância, o estado e quantos estão vivos.
- **"Ir ao carro":** na estrada, 32 m atrás dele.
- **"Recomeçar":** os dois vivos, esperando, e a roda como a semente deu.
- **"Pneu de 80%":** um pneu na mochila.
- **"Acabar a troca":** a troca termina agora.

### Conferido no Editor (02/11/2026, um jogador, semente 7, média)

- **O gerador:** 5 carros quebrados para 10 rodovias.
- **A chegada:**
  - os dois apareceram com o jogador a 32 m;
  - o mecânico agachado na roda, o vigia de guarda, o pneu furado no chão (fotos).
- **A desconfiança:**
  - a 20 m com a pistola na mão: "Não chega mais perto!", e o vigia apontou;
  - de mão vazia, ele abaixou a arma.
- **A ajuda:**
  - E: "Furou o pneu e o estepe. Você tem um pneu?";
  - a dica "[E] Dar o pneu (80%)" (o pneu foi para as mãos).
- **A troca, à noite:**
  - 60 s, com uma onda de 4 e outra de 5 zumbis;
  - o vigia atirou neles;
  - no fim, o pneu na roda (80%) e o macaco fora;
  - o pagamento: $ 120 e 24 balas de 9 mm.
- **Hostis:**
  - mirando 2 s no vigia, os dois atiraram ("Ele vai atirar!");
  - de pistola na mão a 3 m: "Mão longe da arma!" e, 3 s depois, hostis ("Eu avisei!").
- **Mortos:** 2 mochilas no chão, e o carro livre.
- **O save:** a cópia de "ajudados" voltou igual; o JSON ida e volta, e um save antigo sem o campo abre vazio.
- 0 erros.
- **Falta:**
  - dois jogadores;
  - a troca de dia;
  - ouvir as batidas no jogo.

**Decisões minhas para o Pedro confirmar:**
- um tempo antes do tiro (2 s mirando; 3 s armado perto, com o aviso). O plano dizia "na hora";
- o pagamento ($ 60 a $ 140 e uma caixa de munição);
- as duas ondas de zumbis (2 ou 3 de dia, 4 a 6 à noite);
- o mecânico agachado, e não ajoelhado: o FBX não tem um clipe de ajoelhar.

## Ainda não tem

- **As roupas de vestir** no comerciante de roupas (hoje ele vende as mochilas e a roupa velha; trocar a roupa do corpo é a
  loja do Dia 0).
- **Fica para a próxima fase** (o cronograma de 03/11/2026, a pedido):
  - **a emboscada:** os civis escondidos nos prédios das cidades, um em cada andar, atirando de cima;
  - **os NPCs dirigindo carros**, junto com ela;
  - os acampamentos e as bases rivais "de verdade" (do Gustavo);
  - a base militar de verdade, no lugar da provisória ([15](15-Ajustes-da-Primeira-Partida.md)).
