# O maço de cigarros e o fumar

O pedido (03/11/2026): "quero trabalhar em um novo item, maço de cigarros, e aí vamos ter um sistema de fumar onde o
personagem vai pegar um cigarro do maço ao usar o item quando estiver na mão e fumar com animação, mostrando o cigarro
na mão e saindo fumaça do cigarro".

## Como é hoje

- **O que se consome** (a comida, a bebida e os remédios) vai na mão com os braços da lata ou da garrafa (a malha do item
  no lugar da lata, `ItemNaMaoProprio`), e o clique toca o clipe `Usar` inteiro (1,7 a 1,8 s): o item vem até a boca e,
  no fim, o anfitrião tira ele da mochila (`ConsumirRpc`) e a mão volta para a última arma. Os outros veem o clipe no
  corpo.
- **As pilhas** (`Itens.Pilha`): a munição e o saque; a célula mostra quantos há, e o save já guarda a quantidade.
- **Um item com braços próprios** (e não os da lata): o taco de sinuca (o `Jogador_FP_Taco.fbx` e o `Taco_Segurar` do
  corpo, feitos pelo `bloco_taco.py`). A tocha tem o fogo na ponta (as chamas e a luz, `ConstrucoesNoMundo.Fogo`).
- **As partículas** do jogo são as do `Efeitos` (as faíscas e a poeira do tiro). Fumaça ainda não há.
- **De noite**, os zumbis enxergam a 6 m e, com a lanterna ligada, a 26 m (`AjustesDeZumbi`).
- Não há cigarro, isqueiro nem fósforo no jogo.

## As decisões

**Aprovadas pela recomendação** ("Aprovar, ir à parte 2", 03/11/2026).

1. **O fumar:** com o maço na mão, o clique **acende um cigarro** (uns 3 s): a mão direita sacode o maço, um cigarro
   sobe, a boca o puxa, o maço vai para o bolso, a esquerda traz o isqueiro e acende (a chama e a brasa), e a direita
   pega o cigarro da boca. Daí **o cigarro fica na mão**, entre o indicador e o médio, com a brasa acesa e o fio de
   fumaça subindo da ponta; ele anda, corre e pula com ele. Cada clique é **uma tragada** (a mão vai até a boca e a brasa
   aumenta, uns 1,5 s) e, logo depois, **a baforada** (a fumaça saindo da boca). O cigarro dura **6 tragadas ou 2
   minutos**, o que vier primeiro (ele queima sozinho, e o papel vai encurtando). No fim, **o peteleco**: a bituca cai no
   chão e fica uns 20 s soltando um fio de fumaça. *(A outra: um clipe só, de uns 10 s, que acende, fuma e joga fora,
   como o comer; mais simples, mas ele fica preso no clipe esse tempo todo.)*
2. **O maço é uma pilha de cigarros:** o item "maço de cigarros" guarda quantos cigarros tem (até 20, o número na
   célula, como a munição), numa célula. Cada cigarro aceso tira um; com o último, o maço some. No saque, vem com 2 a 20
   (a maioria pela metade). *(A outra: o cigarro solto como item, e o maço como uma caixa que se abre.)*
3. **O isqueiro:** vem com o maço, sem item novo (ele só aparece na animação). *(A outra: um item à parte, o isqueiro ou
   os fósforos, sem o qual não se acende; serviria também para acender a fogueira.)*
4. **O efeito:** nenhuma barra muda (é o clima), **mas tem um preço**: de noite, a brasa acende uma luz pequena,
   laranja, de 1,5 m (mais forte na tragada), e quem fuma é visto pelos zumbis a 10 m (eram 6; com a lanterna ligada,
   26). *(As outras: sem preço nenhum; ou um bônus, como a mira tremendo menos por uns minutos depois de fumar.)*
5. **Trocar de item com o cigarro aceso** (a roda, os números, entrar no carro): **o peteleco**, como no fim, e a mão
   fica livre. *(A outra: o cigarro vai para a boca e continua queimando com a arma na mão; no corpo aparece na boca e, na
   primeira pessoa, só a fumaça subindo na frente da tela. Fica para uma parte depois, se quiser.)*
6. **A fumaça, em blocos:** partículas low-poly facetadas (icosferas de 20 faces, cinza-claro, sem textura) que crescem,
   sobem, giram e somem esmaecendo, no estilo do jogo. Da ponta, um fio fino e contínuo (bolinhas pequenas que balançam e
   se desmancham subindo); da boca, a baforada (umas 12 maiores, para a frente e para cima, abrindo). Na primeira pessoa,
   a baforada sai da frente da câmera, rala e subindo, sem tapar a vista. *(A outra: a fumaça macia, com uma textura
   borrada; mais realista, mas seria a única coisa assim no jogo.)*
7. **Os modelos:** uma marca inventada, sem nenhuma de verdade: **"Blackwater"** (o condado do mapa fixo), em inglês, o
   maço vermelho e branco com o nome em blocos (como as placas da rua), a tampa que abre e os filtros aparecendo dentro.
   O cigarro tem o filtro laranja, o papel branco, a brasa e a cinza na ponta; a bituca é o filtro com um toco amassado;
   o isqueiro, de plástico colorido. **Uns 30% maiores que os de verdade** (como os móveis em ×1,30): a mão de blocos é
   25% maior que uma mão normal, e o cigarro de 8 cm sumiria entre os dedos. *(As outras: no tamanho real; ou uma marca
   por cor, sorteada no saque.)*
8. **O saque:** o maço é **normal** (a raridade mais comum), achado no criado-mudo, na mesa de centro, no rack, nas
   gavetas da cozinha e do escritório e na bancada da oficina (as casas e os apartamentos). Na mesa dos itens da cena das
   variações, junto do resto. O save guarda o maço com os cigarros que sobraram; o cigarro aceso não (carregando a
   partida, ele não está mais aceso).

O que não muda com as decisões: **tudo vai pela rede** (os outros veem o corpo fumando, o cigarro na mão, a brasa, a
fumaça da ponta, as baforadas e a bituca no chão; o anfitrião tira o cigarro do maço quando ele acende, como no
`ConsumirRpc`), a bituca no chão é só um efeito (não é item nem vai no save), e os sons são gerados por código, como os
da comida (o maço sacudindo, a pedra do isqueiro e a chama, o chiado da brasa na tragada, o sopro e o peteleco).

## As partes

Uma de cada vez, cada uma aprovada antes da próxima.

1. **As decisões** (este plano). *Aprovadas pela recomendação em 03/11/2026.*
2. **Os modelos** (no `loot.py` do projeto Unity, nas cores da `Paleta.png`): o maço (fechado e aberto), o cigarro (aceso,
   com o papel que encurta), a bituca e o isqueiro, com o ícone e a foto do maço. Uma folha de fotos com eles de perto e
   ao lado da mão do personagem principal (para ver o tamanho).
3. **As animações** (um `bloco_cigarro.py` no `PrincipalBloco.blend`, como o do taco), no corpo e na primeira pessoa:
   - com o maço: segurar, andar, correr e sacar (o maço na mão direita, como a lata);
   - acender (uns 3 s), tragar (uns 1,5 s) e o peteleco (uns 0,6 s);
   - com o cigarro: segurar, andar, correr e sacar (a mão baixa, o cigarro entre os dedos; na primeira pessoa, embaixo, à
     direita da tela, com a brasa à vista).

   As tiras das fotos, as conferências de sempre (os braços até uns 99%, os dedos sem entrar no cigarro, nada girando de
   um quadro para o outro) e o export para a Unity.
4. **No jogo:** o item (a pilha, a célula, o ícone, o F1 e a mesa dos itens), o fumar (acender, segurar, tragar, o tempo e
   as tragadas, o peteleco e trocar de item), a brasa (a luz) e os zumbis de noite, a fumaça (o fio e a baforada), a
   bituca, os sons, a rede, o saque e o save. Conferido no Play do Editor com um jogador; o teste com dois jogadores é do
   usuário.

## O estado (03/11/2026): a parte 4 feita, esperando a aprovação e o teste com dois jogadores

### A parte 4: no jogo

Conferido no Play do Editor (a cena `Exemplos/Sinuca.unity`, uma partida local, um jogador), na primeira e na terceira
pessoa. O teste com dois jogadores (o corpo dos outros fumando, a fumaça e a bituca nas outras máquinas) é do usuário.

- **O item:** `Item.MacoDeCigarros` (119), no saque como um material (a pilha de até 20, uma célula; o número na
  célula), com o nome, a descrição ("Blackwater. Na mão, clique: acende um. De noite, a brasa chama os zumbis de mais
  longe."), o ícone e a foto; no F3 (em "Outros"); no catálogo do saque como normal, nos móveis da decisão 8 (o
  `ItensDoSaque` refeito); no "Itens no mundo" da cena `Mundo` (45 malhas de saque); e na mesa "os materiais" da cena das
  variações (76 itens nas mesas). Na mão, ele usa os braços do `Jogador_FP_Cigarro.fbx` (dois modelos: o do maço e o do
  cigarro) e, no corpo, os controladores do maço e do cigarro. A dica embaixo da tela: "maço de cigarros (N cigarros) ·
  clique: acender um · G: largar" e, aceso, "cigarro aceso · clique: tragar · R: jogar fora".
- **O fumar** (o `FumoJogador` do prefab do jogador): o clique acende (3 s, a mão ocupada), daí o cigarro fica aceso na
  mão; cada clique é uma tragada (1,5 s, a baforada no 1,0 s); ele dura 6 tragadas ou 2 minutos, com o papel encolhendo
  até 15% (a brasa e a ponta vão junto), e no fim, no R ou trocando de item, o peteleco (a bituca sai da mão no 0,25 s,
  com a velocidade do corpo e o empurrão do dedo). Trocando de item, a bituca só cai (a mão já saiu), e um acender pela
  metade para. **O cigarro sai do maço no peteleco, não no acender** (uma mudança da decisão 2): com o último cigarro
  aceso, o maço continuaria na mão até o fim; conferido, de 20 para 19 no peteleco e na troca.
- **A rede:** o dono escreve o estado (nada, acendendo, aceso, jogando fora) e quanto do papel sobra; as outras máquinas
  tocam o mesmo clipe no corpo (os gatilhos do tiro e da recarga), os sons e a baforada, e a bituca cai em todas. O
  anfitrião tira o cigarro do maço (o `FumarRpc`).
- **A brasa e a chama:** acendem pela escala dos ossos delas (as animações) e, no corpo, com uma luz laranja fraca (a
  luz cai com o quadrado da distância, e o rosto fica a uns 10 cm: com a força de uma lâmpada, o corpo inteiro ficava
  laranja), mais forte na tragada; a brasa e a chama têm um material sem luz (acesas no escuro). **De noite, quem fuma é
  visto a 10 m** (o `visaoComBrasa` dos `AjustesDeZumbi`; eram 6, e com a lanterna, 26).
- **A fumaça** (bolinhas de 20 faces, facetadas, cinza-claro, transparentes, que sobem, crescem, giram e somem): o fio da
  ponta enquanto aceso e a baforada da boca. **Na primeira pessoa, menores e mais fracas** (a câmera dos braços fica a
  30 cm do cigarro: com as do corpo, as bolinhas pareciam pedras na tela): o fio até 1,3 cm e a baforada até 4 cm, rala,
  saindo de baixo da tela para a frente. No corpo, o fio até 3,6 cm e a baforada até 16 cm.
- **A bituca:** a malha do `Loot.fbx`, com física (cai e quica), soltando o fio de fumaça uns 18 s e sumindo nos 22 s.
  Não é item nem vai no save.
- **Os sons** (gerados por código, o `SomDoFumo`): o maço sacudindo, a pedra do isqueiro com a chama pegando, a chama, a
  tragada (o ar e os estalinhos da brasa), o sopro da baforada e o peteleco.
- **O save:** o maço é uma pilha, e o save já guarda a quantidade. O cigarro aceso não vai: como ele só sai do maço no
  peteleco, salvando com ele aceso, ele volta para o maço.
- **Um detalhe da cena de teste:** na terceira pessoa, o que está na mão do dono (as armas e o cigarro) fica na camada de
  luz dele (para a lanterna do peito não estourar a arma), e as lâmpadas da sinuca só acendem a camada normal: lá, o
  cigarro do corpo fica escuro, como as armas. No mundo, o sol e a lua acendem as duas.

- **Os ajustes depois do primeiro teste do usuário** (03/11/2026: "a ponta do cigarro tá do lado do cigarro" e "tem itens
  que na mão ficam meio que se mexendo, o maço e o cigarro por exemplo"):
  - **A brasa do lado do cigarro:** o export do Blender grava a posição dos vazios e das malhas deles a cada quadro, e
    nos ossos que encolhem para sumir (0,001: a brasa e a chama), a conta dele estraga em centímetros (nos braços, até a
    posição fixa no FBX vinha assim); o jogo ainda guardava a posição da brasa de um momento qualquer. Agora, depois da
    animação, cada vazio volta à cabeça do osso dele e cada malha ao lugar dela (o `FumoJogador.Assentar`). Conferido: a
    brasa na ponta (0 mm) e a chama no isqueiro, encostando na ponta, nos braços e no corpo.
  - **O maço e o cigarro mexendo na mão:** era a compressão das animações da Unity (até 0,5° de erro por osso): somados
    nos ossos do braço, os erros davam uns milímetros na mão, e o que fica num osso à parte (o maço, o cigarro, as armas
    e os itens) escorregava em relação aos dedos. Os braços de primeira pessoa (todos os 15) agora vêm sem compressão, e
    o corpo com um décimo do erro. Conferido: em 3 s com o cigarro na mão, ele mexe 0,03 mm em relação à mão nos braços
    e menos de 1 mm no corpo.
  - **O polegar atravessando o maço** (a pedido: "ao segurar o maço o dedão está atravessando o meio da caixa"): ele
    apontava reto para a frente do maço, perto da tampa, mas a base dele fica atrás das costas do maço (na palma) e
    dentro da largura dele, e a primeira falange passava 7 mm por dentro da caixa. A frente não se alcança sem passar pela
    quina: agora o polegar dá a volta por fora da quina de trás e fica na lateral do maço (o lado estreito dele), perto da
    quina da frente e da beira da tampa. Ele é procurado (o `polegar_por_fora` do `bloco_cigarro.py`): para onde a
    primeira falange aponta, o quanto o polegar gira em volta dele mesmo e o quanto a ponta fecha, sem nenhum ponto do
    eixo dele a menos de 7,5 mm da caixa (a meia grossura do dedo). Conferido: o mais perto fica a 8,5 mm, e a polpa a 6 mm
    do lugar pedido; no corpo e nos braços, segurando e acendendo. Exportados de novo o corpo e os braços do fumo. A foto
    de perto em `Imgs/Renders/Cigarro_Polegar_Maco.png` (de frente, do lado de fora, de cima e do lado de dentro).

As fotos no jogo: `Imgs/Renders/Cigarro_Jogo_FP_Maco.png` (o maço na mão), `_FP_Aceso.png` (o cigarro aceso, com o fio
de fumaça), `_FP_Baforada.png`, `_Corpo_Maco.png` e `_Corpo_Acender.png` (de frente, o isqueiro aceso na ponta).

### A parte 3: as animações

No `Modelo 3D/Principal/bloco_cigarro.py` (no `PrincipalBloco.blend`; ele roda o `bloco_taco.py` e as peças do fumo do
`loot.py` do projeto Unity, com as mesmas medidas). A cópia de antes está em `PrincipalBloco_Antes_do_Cigarro.blend`.

- **As peças nos ossos:** seis ossos novos, que não deformam (`maco`, `tampa`, `cigarro`, `brasa`, `isqueiro` e `chama`;
  no corpo, filhos do `chest`; na primeira pessoa, da raiz; a tampa no maço, a brasa no cigarro e a chama no isqueiro),
  cada um com um vazio na cabeça dele, no espaço da peça do `loot.py` (`Fumo_<Peça>` no corpo e `Fumo_FP_<Peça>` nos
  braços): **o jogo põe a malha de cada peça no vazio dela** (a parte 4). A escala do osso diz se a peça aparece (1) ou
  não (0,001): o maço some no bolso, o cigarro só sai do maço no acender, a brasa acende quando a chama encosta, o
  isqueiro só aparece no acender e a chama só enquanto ele acende. No Blender, as prévias (`Previa_Fumo_*`, as peças do
  `loot.py` nos vazios) só aparecem nas ações do fumo (a propriedade `arma` no 9).
- **As pegadas:** o maço na mão direita com as costas na palma e os dedos dando a volta no lado estreito até a frente,
  o polegar na lateral do outro lado estreito, perto da beira da tampa (desde o teste do usuário: veja a parte 4); **o cigarro entre o indicador e o médio**, os dois abertos 10° em V (em repouso, eles
  ficam a 2 cm um do outro; aberto, o cigarro passa com 0,4 mm de folga), o filtro do lado da palma e a ponta acesa do
  lado das costas da mão, com o anelar e o mínimo dobrados; o isqueiro na mão esquerda, em pé, com o polegar atrás, na
  alavanca do gás (em cima da roda, o polegar passava por cima do capuz e tapava a chama).
- **No corpo** (do peito para cima, por cima das animações do corpo, como as das armas):
  - `PrincipalBloco_Maco_Segurar` (laço de 48 quadros): o maço fechado na mão, como a lata.
  - `PrincipalBloco_Maco_Acender` (72 quadros, 3 s): o maço sobe e a tampa abre (até o 9); duas sacudidas e o cigarro
    sobe 3,5 cm (do 10 ao 18); o maço vai à boca, com as costas para baixo (a palma por baixo dele), e a boca pega o
    cigarro (no 28 ele passa para a boca); o maço fecha e desce para o bolso direito (some no 39); a mão esquerda vai ao
    bolso, traz o isqueiro (aparece no 32) até a ponta do cigarro e acende (a chama do 42 ao 52, **a brasa acende no 46**),
    com a cabeça olhando para a chama, e volta (o isqueiro some no 60); a direita vem do bolso até a boca, pega o cigarro
    (no 59) e desce até segurar.
  - `PrincipalBloco_Cigarro_Segurar` (laço de 48 quadros): o cotovelo do lado do corpo, o antebraço para a frente, a mão
    com a palma para baixo e a ponta acesa para cima (com os dedos para cima, o V parecia o sinal de paz).
  - `PrincipalBloco_Cigarro_Tragar` (36 quadros, 1,5 s): a mão vem à boca (até o 9), puxa (do 9 ao 20: o jogo aumenta a
    brasa) e volta (até o 31); a baforada sai do 24, com a cabeça subindo um pouco.
  - `PrincipalBloco_Cigarro_Peteleco` (16 quadros): o punho dobra para trás e estala para a frente; **o cigarro sai da
    mão no 6** (o jogo solta a bituca ali, com a velocidade da mão), e a mão desce, vazia.
- **Na primeira pessoa** (`FP_Cigarro`, em x = 20): os clipes `Maco_Idle`, `Maco_Walk`, `Maco_Run` e `Maco_Sacar` (os da
  lata, com o maço), `Acender` (os tempos do corpo), `Idle`, `Walk`, `Run` e `Sacar` (o cigarro aceso na mão, embaixo, à
  direita da tela, com a ponta para cima: a fumaça sobe na frente da tela), `Tragar` e `Peteleco` (a mão desce para fora
  da tela no fim). **A boca da primeira pessoa é um truque, como a da lata:** mais alta e mais para a frente que a de
  verdade, e o cigarro nela inclinado para a direita, para a ponta e a chama aparecerem embaixo, no meio da tela; quando
  a boca pega o cigarro do maço, ele aponta para baixo (o maço fica fora da tela: com o cigarro já para cima, o maço
  tapava um quarto dela) e sobe até a vista do 29 ao 40. A chama fica 6 mm na frente da ponta e 1,2 cm abaixo dela (o
  cigarro vem de baixo, perto do olho, e na tela cobria a chama que ficava embaixo da ponta).
- **Conferido:** os braços chegam a no máximo 99% (o esquerdo, levando o isqueiro à ponta, no corpo) e 76% na primeira
  pessoa; nenhum giro de um quadro para o outro passa de uns 20° (os do acender e do peteleco, rápidos, como as recargas
  das armas), sem meia volta; o cigarro não entra nos dedos.
- **O export** (o `bloco_exportar.py`, que agora roda o `bloco_cigarro.py`): o `Personagem_PrincipalBloco.fbx` (com os
  ossos e os vazios do fumo e as 5 ações novas como clipes: `Maco_Segurar`, `Maco_Acender`, `Cigarro_Segurar`,
  `Cigarro_Tragar` e `Cigarro_Peteleco`) e o `Jogador_FP_Cigarro.fbx` (os braços, os ossos e os vazios do fumo e os 11
  clipes), já no projeto Unity. A montagem deles na Unity (os controladores, as peças nos vazios, a fumaça) é a parte 4.

As fotos (as do Blender, Workbench, com as prévias): `Imgs/Renders/Cigarro_Corpo_Acender.png`, `_Tragar.png`,
`_Peteleco.png`, `_Maco_Segurar.png` e `_Cigarro_Segurar.png` (de 3/4, de frente e de lado) e `Cigarro_FP.png` (pelo
olho); as mesmas em `Modelo 3D/Principal/Renders/Clipe_PrincipalBloco_<ação>.png` e `FP_PrincipalBloco_Cigarro.png`.

### A parte 2: os modelos

No `Blender/loot.py` do projeto Unity (a seção "o fumo"), nas cores da `Paleta.png` (sem célula nova: o filtro é a cor
cortiça da linha 9, coluna 13, e a cinza a da linha 9, coluna 3), tudo 30% maior que o de verdade (`FUMO = 1.3`), nos
eixos do Blender (o alto em +Z, a frente em -Y), como os outros do saque.

- **`MacoDeCigarros`** (o item; 276 triângulos), a origem no meio: 11,4 × 7,2 × 2,9 cm. A caixa branca com a faixa
  vermelha do nome (os 10 blocos brancos das letras de BLACKWATER, o W mais largo, como as placas da rua), a linha cinza
  embaixo dela e o aviso preto com duas linhas brancas na frente e atrás; a tampa vermelha (o corte inclinado nos lados:
  3,4 cm na frente e 1,7 atrás), com o brasão dourado na frente e a fita dourada do lacre por cima. O ícone e a foto
  (`Icone_MacoDeCigarros.png` e `Foto_MacoDeCigarros.png`, em `Art/UI/Icones/` do projeto Unity), em pé, como a comida.
- **O maço aberto, em duas peças:** `MacoDeCigarrosCaixa` (968 triângulos, quase todos os filtros), a caixa com a gola de
  papelão creme subindo na frente e nos lados, o papel-alumínio, a aba dele rasgada caída para trás e 17 filtros em pé nas
  3 fileiras (faltam três: o do meio da fileira da frente, de onde sai o cigarro que sobe, `SAIDA_DO_CIGARRO`, e um do
  meio e um de trás); e `MacoDeCigarrosTampa` (48), a tampa sozinha, com a origem na dobradiça (`DOBRADICA`, atrás, no
  alto da caixa): fechada no `DOBRADICA` do espaço do maço, aberta girando uns -105° em X (o lado de dentro creme fica à
  mostra).
- **O cigarro, em peças** (em pé, a boca embaixo; a origem é a emenda do filtro com o papel, onde os dedos seguram):
  `CigarroFiltro` (84: o filtro cor de cortiça de 2,7 cm, a ponta creme da boca e o friso dourado), `CigarroPapel` (28: o
  papel branco de 8,2 cm, de 0 a `PAPEL`; o jogo encolhe em Z enquanto ele queima), e na ponta do papel `CigarroPonta`
  (28: o fumo, apagado) ou `CigarroBrasa` (28: a faixa laranja, que o jogo acende) com `CigarroCinza` (56: a beira
  queimada e a cinza). Inteiro: 11 cm, 1 cm de grossura.
- **`Bituca`** (120), deitada, a origem no meio: o filtro e o toco de papel amassado, dobrado 18° para baixo, queimado na
  ponta.
- **`Isqueiro`** (328), em pé, a origem no meio do corpo: o plástico azul (10,5 × 3,3 × 1,8 cm) com a base azul-escura,
  o capuz de metal baixo com as duas orelhas, a roda da pedra entre elas, a alavanca do gás atrás e o bico na frente; e
  **`IsqueiroChama`** (98), a gota (a base azul, a barriga laranja e a ponta amarela, 2,3 cm), com a origem na boca do
  isqueiro (`BOCA_DO_ISQUEIRO`).

A folha: `Imgs/Renders/Cigarro_Modelos.png` (o maço de frente e de costas; o maço aberto com um cigarro subindo; os
cigarros em pé, o novo, o aceso, pela metade e no fim, e a bituca; o isqueiro aceso; e tudo deitado ao lado da mão
esquerda do personagem principal, de cima e de 3/4). Ela é feita pelo `loot.py`: `blender -b --factory-startup
--disable-autoexec --python Blender/loot.py -- folha <saída.png> <PrincipalBloco.blend>` (a mão vem do arquivo do
principal, na pose T, sem o esqueleto). No Workbench, a brasa e a chama ficam opacas; no jogo, elas acendem (a parte 4).

**O `Loot.fbx` ainda não foi exportado** (as peças entram nele na parte 4).
