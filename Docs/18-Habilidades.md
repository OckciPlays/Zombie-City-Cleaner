# A árvore de habilidades, o lago e a oficina (06/11/2026)

Pedidos pelo Pedro em 06/11/2026 ("um sistema de XP que você melhoraria suas skill") e discutidos antes de programar.
Depois do primeiro teste dele ("estão funcionando"), o resto foi feito de uma vez: as 45 habilidades com efeito, a água
(nadar, o fôlego, o afogamento), o caiaque de dois lugares, a pesca, o machado cortando árvores, a mesa de oficina no
bunker, as receitas novas e a mata de outono em volta do lago.

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

## As habilidades (todas com efeito)

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

## Na mochila: a aba HABILIDADES (a tecla K)

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
