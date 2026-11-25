# A campanha nova e o mapa (o plano, 21/11/2026)

O pedido do Pedro (21/11/2026): construir as cidades e os lugares que faltam no Lakewood (a parte que era do Gustavo fica
conosco), refazer o Dia 0 na cidade perto de onde se nasce, pôr o bunker no mapa novo e consertar o chão que faltava. Junto
veio a proposta da campanha nova: o D0 → os 30 dias de quarentena → o colapso no D+30 e a fuga → o bunker de outra
comunidade → os 5 anos → a primeira missão e a morte do major → a liberdade (comunidade própria, patentes, conquista).

**Estado:** só o levantamento e o plano. Nada foi programado. Cada etapa só começa com a aprovação do Pedro, é testada no
Play pelo MCP (fotos e 0 erros) e termina com uma pausa para ele testar.

---

## 1. Como o projeto está hoje (o levantamento)

### O mapa (o Lakewood)
- A única cena da build é a `Scenes/Lakewood.unity`, com 12 × 10 km e 16 setores. Os dados ficam num asset só
  (`Settings/Lakewood/MapaDeLakewood.asset`): o relevo, a água, as estradas, as cidades, os lugares, as ruas e os lotes.
  A cena monta tudo no `Awake` e carrega o detalhe em volta da `Camera.main`.
- **A única cidade construída é a Woodcreek** (D3, 1,2 km²). Ela tem 163 lotes: 99 casas, 13 lojas, o posto, o hospital
  do condado, a loja de armas nova, 5 especiais, 26 cabanas e o acampamento. Tudo vem do kit das construções, e cada
  lote sai da sua semente, então a vila é igual em toda partida.
- **As outras cidades só têm a área aplainada:** a Lakewood (a principal, 7 km², D2), a Riverside (B4), a Fairview (A3) e
  a Sullivan Farms (a rural, A1). Os lugares (o aeroporto, a estação de água, Pine Ridge, as marinas, as ilhas, a
  serraria, a pedreira) estão reservados e pintados no mapa, mas sem construção. A usina já está feita.
- **Onde se nasce:** na "casa da partida" (o lote 114 da Woodcreek, na Sawmill Road, na beira do lago), uma casa de teste
  com mesas cheias de armas e os 4 veículos arrumados.
- **O abandono já é uma passada separada** (`AbandonoDaObra`, o grau `k`, a `Obra.nova` do Dia 0, o shader com a
  `_Intensidade`). Mas o Lakewood ainda não tem a opção de "antes": o grau, a mata dos lotes vazios e os carros
  abandonados estão fixos.

### O chão (o "cair no infinito")
- A queda vinha de dois defeitos, ambos consertados pelo Gustavo na develop:
  - o chão perto do jogador sem a colisão (17/11, `5bb0503`);
  - o colisor ligado sem a malha (18/11, `9847a8a`). Na Unity 6000.6, o `sharedMesh = null` num colisor desligado não
    pega.
- O jogo já põe de volta para cima quem passa pelo chão. O defeito era que, no mesmo lugar, ele caía de novo, sem parar.
- O log da sua build V13 (16/11) tem a série de quedas na Woodcreek. **A sua última build, a V14, é de 17/11:** ela não tem
  o conserto de 18/11.
- **O teste dos consertos com você nunca aconteceu.**

### O Dia 0
- **É uma cena à parte** (`Scenes/DiaZero.unity`, uma rua de 10 casas e 5 lojas com o posto, 28 NPCs).
  - Ela funciona só com o mundo sorteado (`MundoDaPartida`).
  - **Ela não está na build:** o Novo jogo do Lakewood vai direto para a casa da partida. Hoje o Dia 0 não roda no jogo.
- **Dá para reaproveitar quase tudo:**
  - as lojas (prateleiras, gôndolas, geladeiras), os preços, o carrinho, o caixa e a carteira de $1.000;
  - o lobby com a TV para todos e a introdução na TV (91 s, a trilha);
  - os NPCs com as rotinas (regar, cortar a grama, conversar, os 5 doentes);
  - a passagem "5 ANOS DEPOIS".
- **Os carros da rua** são malhas deslizando, sem física. Não há NPC dirigindo de verdade.

### O bunker e os moradores
- **O bunker existe só no mundo sorteado.** São 5 andares (B0 a B4): a garagem, a sala de operações, o arsenal, os
  depósitos, a cozinha, os dormitórios e a manutenção, mais o portão, a portinhola e a casinha no morro.
  **No Lakewood ele não existe.**
- **Os moradores** (52 por mundo, a ficha, as cartas, os modelos, as animações, a agenda, o humor, o terminal) também
  ficam desligados no Lakewood, porque exigem o `MundoDaPartida` e o bunker.
- **Hoje o bunker é da família dos jogadores:** o primeiro morador é sempre um parente, e quem escolhe é o anfitrião.
  Não existe líder, dono, major nem missão.

### Os sobreviventes, as bases e o resto do mapa
- **O acampamento:** há um no Lakewood, na North Shore Highway, perto da Woodcreek. Ele tem cerca, bancas, guardas,
  comerciantes e a zona segura. O código aceita até 31 acampamentos.
- **A base militar provisória:** um quadrado de 40 × 40 m em Fort Crowley (C4). Tem a cerca, a guarita, as barracas, os
  ninhos e de 6 a 8 militares, sempre hostis.
- **Os grupos de busca, as emboscadas e os 3 carros quebrados** já funcionam no Lakewood, mas só na Woodcreek, porque é
  a única cidade com lotes. **Uma cidade nova com lotes ganha os grupos sozinha.**
- **Não existem:**
  - a base inimiga e o clã rival (no mundo sorteado são só pintura no mapa);
  - a reputação, as missões, as patentes;
  - a fazenda em 3D (o celeiro e o silo não existem);
  - as lavouras de verdade.

---

## 2. O plano em etapas

A ordem pensa no que uma etapa precisa da outra: o chão primeiro, porque atrapalha tudo; depois o bunker, porque a
campanha inteira termina nele; depois a Woodcreek de "antes", que é o palco do D0, da quarentena e da fuga. As
construções do mapa (parte C) não dependem da campanha e podem trocar de lugar na fila, se você preferir.

### Parte A: a base

**Etapa 1. O chão.**
- Uma varredura automática no Play, com o detalhe da `Camera.main` indo junto:
  - a pé e de moto, por todas as ruas da Woodcreek e pelas rodovias em volta;
  - até o acampamento, Fort Crowley e a usina;
  - indo e voltando a 30 m/s.
- Conta cada "passou pelo chão" e cada bloco sem colisão.
- Uma conferência por raios numa grade, nas áreas construídas, para achar buracos que a rota não pega.
- O conserto do que sobrar e uma build nova para você conferir.
- Se você lembra de onde caiu (fora da Woodcreek), isso entra na rota.

**Etapa 2. O bunker no Lakewood.**
- **O lugar:** o bunker do mundo sorteado vai para a encosta do lago, ao lado do lote 114 (onde se nasce hoje), com a
  estrada de terra até a Sawmill Road. A encosta desce uns 40 m até a água, então o morro do bunker fica natural ali.
- **Ligar o bunker no Lakewood:** o buraco no terreno, o portão, as portas e as luzes, as estantes, o terminal, o NavMesh
  de dentro e o save.
- **A rede contra a queda não pode puxar quem está nos andares de baixo:** hoje ela leva para cima quem fica 2,5 m abaixo
  do relevo.
- **Os moradores ligados no Lakewood** (trocar o `MundoDaPartida` pela interface do mundo).
- **A comunidade passa a ter dono:**
  - um morador fixo, o líder (o proprietário), na sala de operações;
  - o major, outro morador fixo;
  - o "parente da família" sai da campanha.
- **A casa de teste com as mesas de armas e os 4 veículos sai do jogo normal** e vai para o menu TESTES.

**Etapa 3. A Woodcreek de "antes" (o mesmo lugar, sem os 5 anos).**
- Uma marca no mundo, a época ("antes" ou "depois"), que vale para a região da Woodcreek e para a estrada até o bunker.
- **No "antes":**
  - as casas novas: o abandono em 0, a `Obra.nova`, sem hera, pichação, tábuas e sangue;
  - os gramados aparados no lugar do mato;
  - os postes e as janelas acesos à noite;
  - os carros limpos nas garagens no lugar dos 135 abandonados e dos quebrados;
  - as lojas abertas com as prateleiras do Dia 0;
  - sem zumbis, sem grupos, sem acampamento e sem painéis de sucata.
- **A geografia é a mesma** (os lotes, as ruas e as árvores vêm do mesmo asset).
- As fotos lado a lado, antes e depois, dos mesmos lugares.

### Parte B: o começo da campanha

**Etapa 4. O D0 na Woodcreek.**
- **A casa da família:** uma casa da Woodcreek do lado norte ou leste da vila, para a fuga atravessar a cidade até o
  bunker. É a mesma casa que, 5 anos depois, aparece abandonada.
- **O lobby e a TV:** a introdução nova, com o governo anunciando a possível pandemia de uma doença desconhecida, tratada
  como emergência sanitária, como na COVID-19. A de hoje já conta os ataques, que agora ficam para o D+30.
- **As lojas da Woodcreek vendendo:**
  - o mercado, a farmácia, a loja de armas, a de roupas e o posto;
  - a carteira de $1.000.
- **A cidade viva:**
  - os NPCs do Dia 0 com as rotinas pela vila;
  - os carros circulando pelas ruas, em trilhos e parando diante do jogador e de outro carro (sem a IA de dirigir, que é
    outra fase).
- **As compras vão para a caçamba da caminhonete** (o Pedro, 22/11/2026): são a comida e a água dos 30 dias, e vão junto
  na fuga.

**Etapa 5. Os 30 dias de quarentena.**
- **Em casa:**
  - a TV e o rádio com um boletim por dia, cada vez pior;
  - os comunicados do governo;
  - a comida e a água das compras sendo gastas (o que se comprou no D0 faz diferença).
- **A cidade pela janela, piorando aos poucos:**
  - menos carros;
  - a polícia e os caminhões do exército;
  - as barreiras da quarentena nas saídas (já estavam na fila do plano do Lakewood);
  - as lojas fechando com tábuas e os saques;
  - mais doentes tossindo;
  - as sirenes à noite e o toque de recolher.

**Etapa 6. O colapso (D+30) e a fuga.**
- **A cinemática:** a câmera passa pela Woodcreek à noite, com os gritos, as sirenes, os incêndios, os carros batidos, as
  pessoas se transformando e o mundo ganhando a aparência petrificada.
- **A missão "fuja da cidade":**
  - da casa até a beira do lago, pela Woodcreek ainda de "antes", mas no caos;
  - os NPCs correndo, os primeiros infectados, a barreira do exército caindo.
- **A chegada:** a entrada do bunker na encosta, aberta por alguém de dentro.

**Etapa 7. O bunker, a comunidade e os 5 anos.**
- **A chegada:**
  - o líder recebe o grupo (a fala em texto com o som das letras, como os outros NPCs);
  - a comunidade aparece;
  - o grupo vira a equipe de exploradores recrutada.
- **A passagem "5 ANOS DEPOIS"** troca a época para "depois", e a Woodcreek fica como é hoje.
- **O save guarda a fase da campanha:** o D0, a quarentena, o colapso, a fuga, o bunker, os 5 anos e a missão. Um
  Continuar no meio volta ao começo da fase, e não cai no mundo de depois.
  - A regra "personagem que já existe pula o Dia 0" passa a valer por campanha.

**Etapa 8. A primeira missão e a morte do major.**
- **Um sistema pequeno de missões:**
  - o objetivo na tela;
  - a marca no minimapa e no mapa M;
  - quem dá a missão (o líder, no terminal ou falando).
- **A missão:**
  - o major leva o grupo para buscar remédios no hospital da Woodcreek, que já está pronto por dentro;
  - na volta, a emboscada do clã rival, aproveitando a emboscada e a IA de combate que já existem;
  - o major morre (roteirizado, não dá para salvar);
  - o grupo volta ao bunker sem liderança.
- **Daqui em diante, o jogo livre.**

### Parte C: o mapa (as construções que faltam)

**Etapa 9. A base militar de verdade (Fort Crowley).**
- A muralha ou cerca dupla com as torres, o portão com o posto de controle e os sacos de areia.
- Os alojamentos, o quartel-general, a garagem dos blindados, o heliponto, o arsenal e o depósito de combustível.
- As patrulhas e os postos dos militares, e o saque militar.
- Ela substitui a provisória.

**Etapa 10. Os acampamentos espalhados.**
- De 1 para uns 6 a 8, de tamanhos diferentes (de 3 a 8 sobreviventes); nem todos com comerciantes.
- Os lugares candidatos: a Camp Island (de caiaque), as Westshore Cottages, o Visitor Center de Pine Ridge, uma marina,
  um posto na rodovia e a serraria.

**Etapa 11. A base inimiga (o clã rival).**
- A sugestão é a pedreira (Crowley Quarry): isolada, com o fosso como a arena do capítulo 3, onde o chefe fica preso
  para as lutas e as apostas.
- Muros de sucata, torres e os NPCs do clã, sempre hostis.

**Etapa 12. A fazenda (Sullivan Farms).**
- Os modelos novos: o celeiro e os silos.
- As casas de fazenda (do kit), as cercas, as lavouras de verdade (a Etapa 7 do plano do Lakewood) e o trator.
- Uma comunidade de fazendeiros, que vende comida.

**Etapa 13. As outras cidades, uma por vez, como a Woodcreek** (as ruas e os lotes gravados no asset, o kit e os
especiais):
- a Fairview (a agrícola, pequena);
- a Riverside (a turística, com a marina);
- a Lakewood (a principal, 7 km²), em partes: o centro com os prédios, os bairros e a área industrial.

### Parte D: a progressão (depois de você detalhar)

**Etapa 14. As patentes e as missões do bunker.**
- Começa como recruta e sobe com as missões, a ajuda à comunidade e a liderança, até poder assumir o comando.
- Junto, a reputação por acampamento e por clã (a ideia de 16/11).

**Etapa 15. Os caminhos livres.**
- A comunidade própria (as construções e a fortaleza já existem e servem de base).
- A volta ao bunker para subir de patente.
- A conquista das comunidades rivais.

---

## 3. As decisões

### Para começar (as etapas 1 a 4)
1. **O D0 na Woodcreek de "antes"**, com a casa da família do lado norte ou leste, a fuga cruzando a vila até o bunker na
   beira do lago, e a mesma casa abandonada depois. *(recomendado)*
2. **O bunker na encosta do lago, ao lado do lote 114**, onde se nasce hoje. *(recomendado)*
3. **A casa de teste das armas e dos veículos sai do jogo normal** e vai para o menu TESTES. *(recomendado)*
4. **As cartas dos moradores.** O bunker agora é de outra comunidade, então escolher quem mora lá não faz mais sentido.
   - A recomendação: na campanha, a comunidade já vem sorteada (os 52, com o líder e o major fixos) e as cartas saem.
   - O mundo sorteado continua como está.

### Para depois (perguntar quando chegar na etapa)
- **A quarentena** (23/11/2026: por enquanto, uma tela; a fuga já está feita):
  - todos os 30 dias jogados (um dia do jogo dura 24 min: seriam 12 h); ou
  - uns 7 dias-chave jogados, e os outros passando com a tela preta e o boletim (uns 30 a 40 min no total). Recomendo
    estes.
  - Dá para sair de casa nesses dias?
- ~~**O dinheiro do D0**~~: decidido em 22/11/2026, a carteira de $1.000 e a pistola a $1.000.
- ~~**A fuga:** a pé ou na caminhonete~~: a pé, pela vila (o Pedro, 23/11/2026). E o que chega ao bunker fica com o grupo
  ou vai para o estoque da comunidade?
- **A primeira missão:** o hospital da Woodcreek como destino. E de onde vem o major: do exército, da comunidade?
- ~~**A ordem da parte C**~~: decidida em 23/11/2026, a cidade de Lakewood primeiro, em partes (o centro e o comércio,
  depois os bairros e a zona industrial). Ainda em aberto: a ordem do resto e se a base inimiga fica mesmo na pedreira.
- **Os nomes:** o líder do bunker, o major e o clã rival.

---

## 4. O andamento

**21/11/2026: o plano e as 4 decisões aprovados** ("Aprovo, comece").

**22/11/2026: a etapa 1 (o chão), feita, esperando o teste com os amigos.**
- **O que o Pedro contou:**
  - ele caía nas florestas e nos campos ("onde tem gramas em geral");
  - às vezes ele não caía e os amigos sim.
- **Combina com os dois defeitos que o Gustavo consertou:**
  - o chão atrasado em PC de menos núcleos, com as árvores entupindo a fila (17/11, `5bb0503`);
  - o colisor do bloco ligado sem a malha, que dependia de quando a pessoa saía e voltava (18/11, `9847a8a`).
- **Por que só na grama:** nas ruas e nos lotes há colisões próprias (o asfalto, a calçada, o patamar do lote). Na grama,
  no campo e na floresta, só há o chão dos blocos do terreno.
- **Os consertos valem na máquina dos amigos:** o jogador é simulado na máquina de cada um (o `NetworkTransform` do
  Jogador com a autoridade do dono). A garantia do chão e a volta para cima rodam para o jogador local.
- **As builds:**
  - a V14 do Pedro (o `Player.log` dele é de 17/11) não tem nenhum dos dois consertos;
  - a V15 (`Builds/v15.zip`, de 18/11 às 21:42) tem os dois.
- **A varredura, no Play pelo MCP:** os roteiros e os logs ficam em `D:\Zombiegame\Trabalho-Claude\chao_varredura\`.
  - A cada passo, raios de cima para baixo em anéis de 4 a 45 m em volta do jogador.
  - Os lotes perto da rota conferem o piso.
  - A cada 2 s, a conta dos `MeshCollider` ligados sem malha.
  - As linhas "[Lakewood]" do jogo vão para o log.
  - **Parte 1:** 11,6 km a 15 m/s.
    - As ruas da Woodcreek.
    - O mato e a floresta em volta da casa da partida, em linhas a cada 140 m.
    - Os dois lugares dos buracos da V14.
    - 140.903 raios.
  - **Parte 2:** 12,8 km a 30 m/s.
    - A East Shore, a North Shore, a Crowley Road, a Marina Road, a Sawmill Road e a South Shore na barragem.
    - Fort Crowley e o acampamento por dentro.
  - **Parte 3:** o PC fraco simulado (as threads do jogo atrás de 36 tarefas girando, o jogo a 8–17 FPS).
    - 1,4 km a pé, na floresta das East Hills e no campo da Fairview.
    - 2,3 km de perua na East Shore, a até 87 km/h.
- **O resultado:**
  - **0** ponto sem chão até 45 m;
  - **0** queda e **0** volta para cima;
  - **0** colisor ligado sem malha;
  - os 211 lotes conferidos com piso;
  - **0** erro no console.
- **A garantia de perto:** ela fez o chão na hora 29 vezes, em uns 0 a 40 ms (no carro, com o PC fraco, uma vez 107 ms),
  sempre antes de alguém chegar.
- **O código não mudou.**
- **O que fica para o teste com os amigos:** jogar a build nova, ir longe a pé e de carro pela grama e pela floresta.
  - Se alguém cair, o `Player.log` da máquina de quem caiu tem as linhas "[Lakewood] O jogador passou pelo chão em..."
    com o lugar e o motivo.
  - O arquivo fica em `C:\Users\<nome>\AppData\LocalLow\DefaultCompany\Zombie City Cleaner\Player.log`.
- **Ficou sem teste:** a perua no campo da Sullivan Farms. O radiador vazou nas batidas da curva em grampo, e o motor
  apagou.

**22/11/2026: a etapa 2 (o bunker no Lakewood), feita, esperando o teste do Pedro.** O "Não, vamos continuar para as
alterações" foi o OK para seguir sem a build. Os detalhes estão no `CLAUDE.md`, na seção "O bunker da comunidade no
Lakewood".
- **O lugar:** o bunker do mundo sorteado, cavado na encosta do outro lado da Sawmill Road, de frente para a casa da
  partida (a 50 m dela).
  - O portão fica para a rua, o pátio na altura do asfalto e o barranco sobe uns 11 m atrás do morro.
  - O chão: a pegada plana, um talude de 16 m e o buraco embaixo da caixa.
  - O pátio de concreto é plano até a beira, que fica fora do asfalto e do acostamento.
- **O jogo novo começa na sala de operações:**
  - a perua, o cupê e as motos ficam na garagem, e as chaves na sala;
  - o jogador fica parado até o andar dele carregar;
  - dentro do bunker (e no subsolo do hospital), a volta para cima não vale.
- **A casa de teste:** as armas e os 4 veículos dela saíram do jogo normal. Ficaram no F1, painel do Lakewood, no botão
  "Armas e veículos na casa de teste".
- **A comunidade:** já vem sorteada, sem as cartas e sem o parente da família.
  - Fixos: o líder (o dono do bunker, de 56 a 64 anos) e o major do Exército (de 44 a 52), com histórias próprias.
  - Moram com eles os grupos seguintes inteiros, até uns 8; o resto (uns 43) fica pelo mapa.
  - O terminal mostra o cargo ao lado do nome.
  - Os jogos salvos do Lakewood de antes ganham a comunidade ao abrir.
- **Conferido no Play:** tudo acima, com 0 queda e 0 erro, e os testes novos do Editor.
- **Para a etapa das cidades (22/11/2026, o Pedro), anotado para depois:**
  - manter o padrão das cidades do mapa antigo, mas no tamanho que o mapa e o minimapa mostram (a Woodcreek construída
    ocupa uns 0,58 dos 1,2 km² dela, e as outras estão vazias);
  - construir o comércio com saque: a farmácia, o mercado e outros;
  - algumas prateleiras não têm o saque;
  - os itens que vêm no modelo das prateleiras atrapalham os itens de verdade: no D0, tirar; no apocalipse, eles podem
    ficar ("dá imersão de algo deserto, comida estragada que ficou para trás").
  - Ele escolheu fazer a etapa 3 (a Woodcreek de antes) antes das cidades.
- **Os nomes, fixos (22/11/2026, o Pedro):**
  - o líder do bunker é Gus Rodrigues;
  - o major é Peter Ziquinatti;
  - o líder do clã rival é Cristian (para a base inimiga).

**22/11/2026: a etapa 3 (a Woodcreek de antes), feita, esperando o teste do Pedro.** Os detalhes técnicos estão no
`CLAUDE.md`, na seção "A Woodcreek de antes: a época do Lakewood".
- **A época:** o mundo do Lakewood tem duas épocas, o antes e o depois do apocalipse, e troca durante a partida sem
  recarregar. Hoje a troca é pelo F1; na campanha, o D0 começa no antes, e a passagem dos 5 anos vai para o depois.
- **No antes:**
  - as casas, as lojas, o posto, a loja de armas, o hospital e a escola ficam novos: sem as tábuas, o sangue, o mofo, a
    hera e as pichações, com todas as janelas inteiras e os materiais limpos;
  - os quintais ficam com o gramado aparado, e o asfalto, sem a sujeira;
  - os postes são de madeira nova, e à noite os de perto acendem;
  - a placa da entrada aparece sem o risco;
  - sem zumbis, carros abandonados, painéis de sucata, grupos, acampamento e bases;
  - sem a neblina do abandono (o dia limpo);
  - as árvores ficam iguais: a mesma geografia.
- **Conferido no Play:** as fotos dos mesmos lugares nas duas épocas, a ida e a volta, 0 erro.
- **Fica para a etapa 4 (o D0):** as lojas por dentro com os produtos à venda (e sem os itens que vêm no modelo das
  prateleiras), o caixa, os carros andando e as pessoas na rua.

**22/11/2026: a etapa 4 (o D0 na Woodcreek), feita, esperando o teste do Pedro.** Os detalhes técnicos estão no
`CLAUDE.md`, na seção "O Dia 0 na Woodcreek".
- **As decisões do Pedro:** a carteira de $1.000 com a pistola a $1.000; as compras na caçamba da caminhonete; a casa da
  família no norte da vila, escolhida por mim e mostrada a ele.
- **A casa da família:** o lote 104, um sobrado com garagem na Spruce Lane, em (9921; 6901), a ~660 m do bunker, de frente
  para o sul. As lojas da North Shore Highway ficam a ~150 m, e a fuga atravessa a vila inteira até o lago.
- **O jogo novo do Lakewood começa no Dia 0** (o menu do novo jogo tem o "COMEÇAR PELO DIA 0: SIM/NÃO"; o padrão é sim):
  - a Woodcreek fica no antes, e cada um nasce na sala da casa, de frente para a TV;
  - o lobby é o do Dia 0 de antes: ninguém sai até a TV ser ligada e o jornal acabar (ou ser pulado);
  - sem a pistola no começo, sem XP, sem a trilha do jogo, sem clima, e quem morre não perde nada.
- **O jornal novo na TV** (os mesmos 91 s e a mesma trilha): a sala; o hospital no Brasil com os primeiros casos; o
  aeroporto com as máscaras e a febre medida; a entrevista do governo declarando a emergência de saúde pública e pedindo
  comida, água e remédios para 30 dias; o mercado lotado; os primeiros casos nos EUA, no nosso estado; "As lojas da
  Woodcreek ainda estão abertas. Cada um tem mil dólares." Sem o homem de pedra, a praça das estátuas e o ataque: ficam
  para o D+30.
- **As lojas que vendem:** o mercado, a farmácia, as duas 24 horas (como a conveniência), a loja de roupas e a de
  ferragens e autopeças (as duas primeiras lojas comuns), o posto e a loja de armas nova (a pistola a $1.000 no balcão de
  vidro, as brancas penduradas nos painéis). As outras lojas comuns ficam fechadas.
  - As prateleiras são desenhadas só no Dia 0, sem os itens que vêm nos modelos antigos; as lojas ganham gôndolas no meio
    do salão (antes, só havia os móveis das paredes).
  - O carrinho, o caixa e os preços são os do Dia 0 de antes.
- **As compras vão para a caçamba da caminhonete,** estacionada na entrada da garagem da casa. De trás dela, o E abre a
  caçamba no inventário (uma grade de 12 × 6). Cheia, o resto fica no chão atrás dela.
- **A vila viva:** 33 pessoas (os atendentes, os quintais regando, cortando a grama e conversando, as calçadas do comércio,
  a beira da Creek Street; 5 doentes) e 9 carros circulando pelas ruas e pelas rodovias da vila, parando diante de quem
  está na frente.
- **O fim do dia (por enquanto):** as chaves da casa, no gancho ao lado da porta da frente, por dentro: E duas vezes
  tranca a casa. A tela "5 ANOS DEPOIS" cobre a troca para o depois; cada um aparece na sala de operações do bunker, e a
  caminhonete vai para o pátio dele, com a caçamba como estava e a chave na sala. A quarentena, o colapso e a fuga são as
  etapas 5 e 6.
- **O F1:** o painel DIA 0 (pular, +$1.000, liberar a rua, ir para a casa e para cada loja) e, no painel do Lakewood, o
  "Começar o Dia 0" numa partida aberta.
- **Conferido no Play (um jogador):** do jogo novo ao bunker, as fotos de cada loja, o jornal inteiro, a pistola paga e na
  caçamba, as compras do mercado, a caçamba aberta, a passagem e a caminhonete no pátio; 0 erro.
- **Ainda não:**
  - com dois jogadores (o cliente entrando no meio do Dia 0);
  - as compras à mostra na caçamba (por enquanto, só no inventário);
  - os carros da vila param para os jogadores, mas não para as pessoas;
  - as lojas fechadas ficam vazias por dentro;
  - a casa da família depois dos 5 anos é uma casa abandonada qualquer (é a mesma obra, no depois).
- **23/11/2026:** as chaves da casa não levam mais direto aos 5 anos: levam à fuga (abaixo). O "Pular o Dia 0" do F1
  continua indo direto.

**23/11/2026: as etapas 5 e 6 (os 30 dias, o colapso e a fuga) e a chegada da 7, feitas, esperando o teste do Pedro.** O
pedido (o Pedro, antes de sair): "uma missão principal no canto direito superior, com o objetivo de correr... o caos,
pessoas sendo devoradas pelos Ocos, brutamontes na cidade, long também... daria para falhar e morrer, que se acontecer
retornaria ao começo. Até quando chegarmos perto do bunker descendo a rua a pé... vamos ver o bunker, algumas pessoas
entrando e vamos entrar junto (como se um amigo nosso estivesse ali dentro já e nos chamasse para entrar)". Os detalhes
técnicos estão no `CLAUDE.md`, na seção "A fuga: os 30 dias, o colapso e a chegada ao bunker".
- **Os 30 dias, por enquanto, numa tela:** as chaves da casa trancam o dia, e a tela "30 DIAS DEPOIS" conta a quarentena
  pelo rádio (as escolas fechando, os hospitais cheios, o exército nas estradas, a TV saindo do ar, "não abram a porta
  para os doentes", os gritos na rua). As decisões da etapa 5 (os dias-chave jogados, sair de casa, a comida das compras
  sendo gasta) continuam em aberto.
- **A noite do colapso:** à meia-noite (parada), na mesma Woodcreek de antes, cada um volta para a sala da casa.
  - A missão no canto de cima, à direita: "FUJA PARA O BUNKER", com o que fazer ("pegue o que der na caçamba da
    caminhonete e desça a rua") e os metros até o portão; a marca "Bunker" no mapa e no minimapa (o alfinete com a
    distância).
  - O caos: os zumbis voltam a nascer em volta de quem joga; pelo caminho (a Spruce Lane, a North Shore Highway pelo
    comércio, a East Shore e a Sawmill Road), um Oco em cima de cada um dos 7 corpos com o sangue, o Brutamonte e uns Ocos
    no comércio, o Long na beira da rodovia com dois Corredores, um grupo na descida e outro perto do bunker. Gente
    correndo pelas ruas, carros batidos atravessados (uns pegando fogo, outros com o alarme disparado) e gritos pela vila.
  - As compras estão na caçamba da caminhonete, na entrada da garagem: dá para pegar a pistola, a faca e o resto antes de
    correr.
- **Falhar:** se alguém cai, a tela "A FUGA FALHOU", e a noite recomeça para todos na casa, com todos curados e os zumbis
  postos de novo (o contador da tentativa aparece na missão). Na fuga, quem morre não perde a mochila, e ela não dá XP.
- **A chegada:** o portão do bunker aberto e aceso, a comunidade correndo do portão da cerca para dentro e o Gus Rodrigues
  do lado do portão, de braços abertos, chamando ("Aqui! Rápido, pra dentro! O portão vai fechar!", embaixo da tela, e o
  grito dele). Com todos a menos de 7 m do portão (ou já dentro), a tela "5 ANOS DEPOIS": a época volta para depois, cada
  um aparece na sala de operações, o portão fecha, a marca sai e a caminhonete vai para o pátio, com a caçamba.
- **O F1:** "Pular a fuga (chegar ao bunker)", no painel do Lakewood.
- **Ainda não:**
  - os 30 dias jogados (a etapa 5 de verdade);
  - a fala do Gus com a voz dele e o líder recebendo o grupo lá dentro (a etapa 7);
  - os NPCs da vila sendo atacados de verdade (hoje são corpos com os Ocos em cima; os zumbis do jogo só atacam os
    jogadores e os sobreviventes);
  - uma sirene de polícia (não há o som);
  - com dois jogadores.

**23/11/2026: a parte C começou pela cidade de Lakewood ("Sim, comece o mapa"; "A cidade de Lakewood"); a parte 1 (o
desenho), feita, esperando o teste do Pedro.** Os detalhes técnicos estão no `CLAUDE.md`, na seção "A cidade de Lakewood".
- **O padrão das cidades do mapa sorteado, no tamanho do mapa:** a grade de ruas retas, com os quarteirões de 100 × 75 m,
  as avenidas mais largas (12 m, a cada 5 linhas de norte a sul e a cada 6 de leste a oeste) e as ruas de 8 m, nos
  quarteirões planos e secos dentro do contorno (649 quarteirões, 113 km de ruas). A encosta da baía do noroeste e a beira
  do contorno ficam de fora.
- **As rodovias que passam pela cidade continuam:** o Lakewood Boulevard (a Airport Road e a East Shore Highway, de norte a
  sul) e a Central Avenue (a Eastern Highway, para o leste; para o oeste, uma avenida nova na mesma linha). Onde as
  diagonais delas cortam a grade, as ruas param a 12 m e chegam nelas em T (105 encontros).
- **As zonas, pela distância do meio (o nó das quatro rodovias):**
  - o centro (até uns 340 m): 188 prédios, com estacionamentos no meio e a praça do meio;
  - o comércio (até uns 540 m) e a beira das rodovias e das duas avenidas do meio (até 1 km): 685 lojas;
  - os bairros: 1.979 casas, de 24 m de frente, como na Woodcreek, e uns 10% de lotes vazios;
  - a zona industrial (o sudeste, perto da Airport Road): 255 galpões reservados;
  - 5 praças, 2 escolas, 3 igrejas, os bombeiros e 3 postos.
  - No total, 3.399 lotes, com os nomes das ruas: as árvores a oeste do boulevard (Maple, Oak, Elm...), os presidentes a
    leste (Washington, Jefferson, Lincoln...) e as numeradas (North 1st Street, South 6th Avenue...).
- **O que já aparece no jogo:** as ruas, as casas, as lojas (por fora e por dentro), os postos, os especiais, os postes e as
  placas das esquinas, com os zumbis e os carros parados como na Woodcreek.
- **O que ainda não:** os prédios, os galpões, os estacionamentos e as praças são só o chão plano por enquanto; as calçadas
  da grade; o saque das lojas (o levantamento achou que, depois do apocalipse, nenhum móvel das lojas recebe saque, e o
  mercado, a farmácia, a 24 horas e as comuns ficam com o salão vazio).
- **O desempenho:** com a cidade, o Lakewood levou 49 s para abrir. As pedras das trilhas e as entradas de carro de cada
  casa eram montadas todas no começo; agora vêm com a casa, perto de quem joga. Abre em uns 10 s (eram 6,5 s antes da
  cidade), a 75 FPS nas ruas do Editor.
- **Conferido:** os 34 testes do Lakewood (um novo, o da cidade) e o Play (o comércio, a avenida, dois bairros, uma
  diagonal e o centro), com 0 erro. A rede das estradas da Woodcreek e das rodovias continua igual.
- **As próximas partes, propostas:** (2) as calçadas da grade e as ruas em pedaços (a malha dos postes e das placas da
  cidade é uma só, grande demais); (3) os prédios do kit no centro, com os andares carregando perto; (4) o comércio com o
  saque nas prateleiras; (5) as praças, os estacionamentos e os galpões; (6) a marina.

**23/11/2026: as partes 2 a 5 da cidade de Lakewood ("Sim, siga"), feitas, esperando o teste do Pedro.** Os detalhes técnicos
estão no `CLAUDE.md`, na seção "A cidade de Lakewood".
- **As calçadas e as ruas:** a calçada com o meio-fio dos dois lados de todas as ruas da grade (uns 190 km), parando onde
  outra rua chega, e o quadrado que fecha cada esquina, como no mapa antigo. As placas das esquinas da Lakewood dizem
  "Lakewood Boulevard" e "Central Avenue" (antes, a East Shore aparecia como a Main Street da Woodcreek). A malha das ruas
  de cada cidade agora vai em pedaços de 256 m.
- **Os prédios do centro:** os 188, de 4 a 15 andares (os mais altos perto do meio), com a fachada de verdade, o saguão, a
  escada, o poço do elevador, as janelas escuras de longe e os andares por dentro carregando perto de quem está (como no
  mapa sorteado). Conferido: um prédio de 14 andares com 3 andares carregados ao entrar no saguão.
- **O comércio com saque** (o pedido de 22/11: "construir dentro dela os lugares de comércio, farmácia, mercado e alguns
  outros (onde vão ter loots)" e "em algumas prateleiras não tem spawn de loots"):
  - o achado: no Lakewood depois do apocalipse, as lojas não tinham os móveis do salão e nenhum móvel de loja recebia
    saque;
  - agora as lojas têm os móveis antigos (as prateleiras, as gôndolas, os freezers, os caixas), com os produtos do modelo
    (como você quis: "dá imersão de algo deserto"), e o saque em cima deles, pelo tipo da loja: o mercado (comida, bebida,
    lanche, limpeza; o galão no estoque), a farmácia (como antes), a 24 horas e a loja do posto (lanche, bebida, lanterna,
    remédio), e as comuns em quatro jeitos (roupas, ferragens, esportes e variedades); o escritório e o banheiro de todas;
  - os lugares das prateleiras das lojas foram medidos pela ferramenta que já media os da farmácia (88 lugares em 22
    móveis); os da farmácia e do hospital saíram iguais;
  - vale também para a Woodcreek e para as lojas do mapa sorteado;
  - conferido: o mercado com 15 itens na cena (lata de feijão, carne enlatada, refrigerante, álcool...), a farmácia com 16,
    a 24 horas com 13 e uma comum com 10.
- **As praças** (a cruz de caminhos, a volta, o chafariz com a água parada, os bancos e as árvores nos canteiros), **os
  estacionamentos** (o asfalto, as vagas pintadas e os carros largados) e **os galpões** da zona industrial (o corpo de
  metal, o telhado de duas águas, os portões de enrolar e a plataforma das cargas; por enquanto só por fora).
- **O desempenho:** abre em ~10 s (a versão das ruas "de antes" só é montada quando a época vira; as juntas das calçadas
  da grade saíram).
- **Conferido:** os 34 testes do Lakewood, os do saque, das lojas, da mobília, dos móveis, dos prédios, do hospital e da loja
  de armas (70 de 71; o que falha, `LojasTestes.AsCidadesTemAsLojas`, já falhava: ele procura o hospital do mapa sorteado
  como um prédio, e desde 13/11 o hospital tem o quarteirão dele); o Play com 0 erros.
- **Ainda não:** os galpões por dentro, a marina, a loja de armas do Lakewood com saque (ela tem os móveis próprios, sem os
  lugares), o expositor refrigerado e o armário de armas sem lugares (a ferramenta não achou superfícies livres neles), e
  os salões grandes das lojas ficam meio vazios no meio.

**23/11/2026: a marina e os galpões por dentro ("Siga: marina e galpões"), feitos, esperando o teste do Pedro.** Os detalhes
técnicos estão no `CLAUDE.md`, na seção "A cidade de Lakewood".
- **Os galpões por dentro:** o piso de concreto, as paredes de metal com os portões de enrolar (um aberto, os outros
  fechados) e a porta, o telhado de duas águas visto de dentro, as fileiras de estantes de aço, as caixas no fundo e um
  escritório com a mesa e o arquivo junto da porta. O saque é o que sobrou de uma distribuidora: ferramentas, galões,
  lanternas, machado, panos, mochilas, limpeza e umas conservas e bebidas (10 a 20 itens). Conferido: um galpão de 42 × 23 m
  com 16 itens na cena.
- **A marina:** a Marina Road (420 m, da grade até a beira da baía), a loja da marina e o estacionamento no fim dela, e o píer
  na água: a passarela na beira, o píer de 52 m com 7 pontes, as estacas, 5 barcos (2 afundados), a bomba de combustível e
  a rampa dos barcos.
- **A loja da marina** é uma loja de esportes de um andar. As lojas de esportes ficaram sem as roupas (a primeira vez, a da
  marina saiu com 13 itens quase todos roupa): agora vendem a vara e a isca, as bebidas, as pilhas, a lanterna, a faca, as
  mochilas, o machado e o violão.
- **Conferido:** o Play com 0 erros; os 34 testes do Lakewood e os do saque e das lojas (16 de 17: o que falha é o do hospital
  do mapa sorteado, que já falhava). A regra nova dos esportes foi conferida pelo catálogo e pelos testes, não no Play.
- **Ainda não:** o saque da loja de armas do Lakewood, o expositor refrigerado e o armário de armas sem lugares, e os salões
  grandes das lojas meio vazios no meio.

**23/11/2026: os bugs do D0 que o Pedro achou, consertados.** Os detalhes técnicos estão no `CLAUDE.md`, em "O Dia 0 na
Woodcreek".
- **Os NPCs andando e correndo parados:** eram os do jornal da TV, no saguão do aeroporto e no mercado lotado. Agora eles
  andam e correm de verdade.
- **A chave da caminhonete:** fica em cima da mesa de centro da sala da nossa casa. Quem a pega fica com ela.
- **A loja de armas:** vende a submetralhadora, a espingarda, o rifle e a sniper, e a munição, com os preços do Pedro para
  armas de veterano. As armas longas ficam nos expositores e no rack; a munição, nas estantes. Os preços:

  | Arma | Preço | Munição | Caixa |
  |---|---|---|---|
  | Pistola | $1.000 | 9mm | $50 |
  | Submetralhadora | $2.000 | 9mm | $50 |
  | Espingarda | $2.500 | calibre 12 | $100 |
  | Rifle | $4.000 | 5,56 | $120 |
  | Sniper | $5.000 | 7,62 | $150 |

- **A farmácia:** já tinha todos os itens; agora cada prateleira tem a etiqueta com o nome e o preço (em todas as lojas do
  Dia 0).
- **Na fuga, a caminhonete não pega** (o Pedro: "pode deixar ela, só colocar como ela não está pegando na hora de dar a
  partida"): ela fica na casa, mas o motor só tosse. A fuga é a pé, e no bunker ela volta a funcionar.
- **Em aberto:** a carteira do Dia 0 é sempre de $1.000: o dinheiro guardado do personagem não entra, então só a pistola
  cabe.

**24/11/2026: o D0 mais vivo e a primeira missão depois dos 5 anos.** Os detalhes técnicos estão no `CLAUDE.md`, em "O Dia 0
mais vivo e a primeira missão depois dos 5 anos".
- **Conversar com os NPCs:** com o E, perto de um vizinho, de um atendente ou de quem passa na rua. Aparecem a fala e
  algumas perguntas (teclas 1 a 9): a doença, se a pessoa está bem (quem está doente tosse e disfarça), o que a loja
  vende, onde ficam as lojas (com a direção e os metros), a quarentena. Na fuga, ninguém para para conversar.
- **A fuga mais cheia:** 22 pessoas fugindo, 12 corpos e 9 carros batidos. Em cada corpo, um Oco devorando; atrás de
  algumas pessoas, um Oco correndo. Quem chega perto (14 m) faz o Oco virar um zumbi de verdade, que vem atrás.
- **O limite da cidade:** no Dia 0 e na fuga, a polícia fechou as estradas. Quem passa da beira da Woodcreek (a pé ou de
  carro) volta para dentro, com o aviso da quarentena. Nas estradas e nas ruas que saem da cidade, um bloqueio com
  cavaletes, cones e a placa "QUARENTENA".
- **As lojas com nome:** cada loja tem o nome na fachada ("MERCADO WOODCREEK", "FARMÁCIA WOODCREEK", "FERRAGENS E
  AUTOPEÇAS"…), e a loja de armas tem a placa vermelha "ARMAS E MUNIÇÃO".
- **O portão do bunker:** os da comunidade não aparecem mais do nada. Ficam na entrada do pátio, olhando para a rua, e
  correm para dentro quando a gente vem descendo a rua (a 40 m deles), uma vez só.
- **A primeira missão, "Retome a Woodcreek":**
  1. Na chegada ao bunker, perdemos tudo o que levávamos: foi para a caçamba da caminhonete, que ficou na casa.
  2. Cinco anos depois, o Capitão espera no pátio do bunker. Ele conta o que houve e chama para retomar a Woodcreek.
  3. Ele vem junto (dá para pedir que espere). Perto da casa, a rua está tomada: 14 zumbis, 2 deles Corredores.
  4. Limpa a rua, recupera as compras na caçamba e conserta a caminhonete: ela está sem as 4 rodas, sem a vela, sem a
     bateria e sem o radiador. As peças saem dos carros largados da vila (o macaco ficou na caminhonete).
  5. Com ela andando, a missão acaba ("MISSÃO CUMPRIDA").


## 5. Cuidados técnicos
- **O asset do mapa (`MapaDeLakewood.asset`) é um arquivo só:** duas sessões mexendo nele ao mesmo tempo se atropelam. Por
  isso, as etapas do mapa vão em fila, e não em paralelo.
- **O save:**
  - a fase da campanha e a época entram no `MundoSalvo`;
  - um campo novo de posição precisa entrar no `MigrarAOrigemDoLakewood`.
- **O CLAUDE.md tem trechos desatualizados,** a corrigir na etapa que mexer neles:
  - a linha 1118, a partida em (8.600; 3.700);
  - a linha 3250, o Dia 0 "nas Build Settings";
  - o `Docs/12-Dia-Zero.md`: as compras no chão, o dinheiro perdido e os sons.
