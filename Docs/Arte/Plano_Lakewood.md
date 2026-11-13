# Zombie City Cleaner: o mapa fixo do Condado de Lakewood

O mapa fixo do jogo, refeito do zero em 08/11/2026 a partir do prompt mestre e da imagem de referência do usuário
(`Imgs/Lakewood/Lakewood_Referencia.webp`). Substitui o mapa fixo anterior (o Condado de Blackwater, de 29/10/2026), que
foi apagado inteiro (o código, o asset, a cena, o plano e as imagens). O jogo de verdade (a cena `Mundo`) continua no
mundo sorteado por semente até o Lakewood estar pronto: tudo daqui fica **numa cena separada**, `Scenes/Lakewood.unity`.

A ideia do usuário: com esse mapa começa a história do jogo, em volta do condado, com o laboratório sob a Ilha Central como
o ponto de partida do surto e o lago ligando tudo.

## O pedido (como veio, 08/11/2026)

- "Pode apagar tudo o que você tinha feito do mapa fixo da Blackwater, vamos refazer em uma nova ideia"; "importante que
  isso seja feito em uma cena separada da cena mundo atual".
- O prompt mestre: o Condado de Lakewood, fictício, nos Estados Unidos, cinco anos depois do apocalipse; a região em volta
  de um reservatório artificial feito por uma hidrelétrica; a cidade principal (Lakewood, a sudeste), três cidades pequenas
  (Riverside, turística, a noroeste; Fairview, agrícola, a oeste; Woodcreek, a vila florestal, a nordeste), a comunidade
  rural (Fazendas Sullivan, a sudoeste), o aeroporto regional, a base militar, o parque estadual de Pine Ridge, a estação de
  tratamento de água, as marinas, as casas de lago, as cinco ilhas (a Central, com o laboratório secreto; a do Acampamento;
  a da Antena; a Residencial; a da Pesquisa), a barragem na saída sudoeste, os rios (o principal entrando pelo norte, os
  afluentes do leste e do oeste, o rio que sai da barragem para o sul) e a rede de túneis que liga o laboratório à
  hidrelétrica, ao hospital da Lakewood, à Fairview, à Woodcreek e à Riverside.
- As medidas: uns 12 km × 10 km, 1 unidade = 1 m, o x para o leste, o z para o norte, a origem no canto sudoeste, 16
  setores (4 × 4, de 3.000 × 2.500 m). As cotas: a água do reservatório a ~150 m, o fundo abaixo de 145, o campo de 160 a
  260, as colinas de 220 a 400, as montanhas até 600 a 650, o vale logo abaixo da barragem de 90 a 110.
- A ordem obrigatória: a estrutura do mundo (Etapa 1), o relevo (2), o reservatório (3), a hidrelétrica (4), as estradas
  (5), as cidades e os pontos de interesse (6), os biomas e a ambientação (7), o subterrâneo (8), a otimização e os testes
  (9). "Comece pela Etapa 1, seguida da Etapa 2."
- A imagem de referência é obrigatória para a organização da região, "mas não confunda a ilustração com uma representação
  cartográfica de precisão".

## O que foi aproveitado e o que saiu

- **Saiu** (apagado, 08/11/2026): tudo o que era do Blackwater: `Scripts/Mundo/MapaFixo/` (o `DesenhoDoMapa`, o
  `RelevoDoDesenho`, o `ChaoDoMapaFixo`, o `PintorDoDesenho`, o `PontesDoDesenho`), o `MapaFixoNaCena`, a
  `MontagemDoMapaFixo`, a `ImagemDoDesenho`, a `JanelaDoDesenho`, o painel do F1 dele, os testes, o asset
  (`Settings/MapaFixo/`), a cena `MapaFixo.unity`, o `Plano_Mapa_Fixo.md` e as imagens `Imgs/Mapas/MapaFixo*`.
- **Ficou, adaptado** (a engenharia era genérica e já estava medida no jogo): a geometria das linhas (`Linhas`, o Chaikin,
  as distâncias e a varredura de polígonos), o terreno em blocos de 64 m montados em outra thread em 4 níveis de detalhe com
  a colisão perto do jogador e dos carros, o relevo de longe em cores da paleta (agora um por setor), a água dos lagos e dos
  rios, as plantas em instâncias pela cobertura, o perfil das estradas com as pontes (para a Etapa 5), o pintor do mapa, a
  janela de editar o desenho, a imagem do mapa com a legenda e o painel do F1.
- **Ficou como está**, do resto do jogo: o kit das construções, a mobília, o saque, as portas, os zumbis, o carro, a rede,
  o clima, o save. O gerador por semente (a cena `Mundo`) não foi tocado.
- **Novo:** o `MapaDeLakewood` (o dado central), o `RelevoDeLakewood` (as zonas de relevo, a barragem, o aplainamento das
  cidades e das instalações), a leitura da imagem de referência (`LeituraDaReferencia`), o esboço em código
  (`EsbocoDeLakewood`), os setores, a prévia do relevo na cena, as fotos e o relatório dos critérios.

## O condado em números

| | |
|---|---|
| O mapa | 12.000 × 10.000 m (120 km²), a origem (0, 0) no canto sudoeste, o x para o leste e o z para o norte |
| Os setores | 4 × 4, de 3.000 × 2.500 m; A a D de oeste a leste, 1 a 4 de sul a norte (A1 no sudoeste, D4 no nordeste) |
| Os blocos do terreno | 188 × 157 blocos de 64 m (29.516); a grade do relevo a 20 m (601 × 501 pontos, ~1,2 s para montar) |
| A água do reservatório | 150 m, fixa; o fundo até ~115 m no meio e junto da barragem |
| A água do mapa | 19,7 km² (16,4% do mapa) com as ilhas tiradas (o plano pedia de 20 a 25%; a ilustração tem o que tem) |
| As ilhas | 5 nomeadas (a Central 1,96 km², a Camp 1,03, a Research 0,98, a Cottage 0,69, a Antenna 0,61) e 3 ilhotes |
| A planície do oeste | a média de 181 m (90% abaixo de 203 m) |
| As montanhas | o ponto mais alto 644 m (o Mount Crowley, 647 m); o norte com a média de 256 m e o leste de 265 m |
| A barragem | 881 m de crista a 158 m (12 m de largura, com a estrada); o fundo do lago junto dela a 112 m; o pé a 108 m; o vale 400 m abaixo a 105 m |

As cidades (as áreas reservadas, em metros; o relevo aplainado dentro delas):

| Cidade | Tipo | Centro (x; z) | Área | Cota média | Inclinação média |
|---|---|---:|---:|---:|---:|
| Lakewood | principal | 9.820; 3.500 | 7,0 km² | 184 m | 3,3% |
| Riverside | pequena (turística) | 4.022; 8.470 | 1,5 km² | 178 m | 7,4% |
| Fairview | pequena (agrícola) | 1.782; 6.324 | 1,5 km² | 169 m | 2,6% |
| Woodcreek | vila (florestal) | 9.964; 6.690 | 1,2 km² | 187 m | 4,6% |
| Sullivan Farms | rural | 1.393; 2.188 | 2,1 km² | 173 m | 1,0% |

Os lugares (os pontos de interesse reservados; "aplainado" é o chão nivelado no raio): Lakewood Dam (a barragem, de
(4.150; 3.130) a (5.000; 2.900)), Lakewood Hydroelectric Plant (4.800; 2.600), Central Island Laboratory (6.125; 5.180),
Lakewood Regional Airport (10.050; 985, raio 1.000, aplainado), Water Treatment Plant (6.300; 1.150, aplainado), Pine Ridge
State Park (5.700; 9.300), Visitor Center (6.900; 8.050), Fort Crowley (a base militar, 8.900; 9.100, aplainado), as
marinas (Riverside, Lakewood e a do braço norte), Camp Island, Antenna Island, Cottage Island, Research Island, Woodcreek
Sawmill, Crowley Quarry, East Lookout, North Crossing (a ponte do norte) e Westshore Cottages. Os picos: Mount Crowley
(10.990; 9.520) e Pine Ridge (5.900; 9.550).

## A referência e como ela vira dados

Duas fontes, que a ferramenta junta no asset `Settings/Lakewood/MapaDeLakewood.asset` (a fonte daqui em diante: o que for
mexido na janela do desenho fica; o esboço e a leitura só se refazem quando se pede):

1. **O esboço em código** (`EsbocoDeLakewood`, menu Zombie City Cleaner > Lakewood > 1): o que foi lido à mão da imagem
   (a moldura do mapa fica entre os pixels x 32 a 1264 e y 29 a 984 do webp: 1 px ≈ 9,7 × 10,5 m) e do prompt: o tamanho,
   os setores, as zonas de relevo, os picos, os rios, a barragem, as cidades, os lugares, os túneis e a partida.
2. **A leitura da imagem** (`LeituraDaReferencia`, o menu 2, ~4 s): cada pixel vira uma classe pela cor (a água azul, a
   mata verde escura, o campo verde claro, a lavoura amarela, a rocha cinza e o urbano bege, este só dentro das cidades e
   das instalações do esboço; o cinza das pistas e dos tanques não vira montanha); o que não dá para ler (os rótulos, as
   estradas, as linhas) pega a classe mais comum da vizinhança, em passadas. Daí saem a **cobertura** em células de 25 m
   (480 × 400: mata 51%, lavoura 16%, rocha 9%, urbano 6,5%, água 16%, campo 1,4%) e a **grade das montanhas** (a fração de
   rocha em volta). Quem lê a cobertura (o mapa pintado, as texturas do chão, as plantas e o relevo de longe) mistura as
   4 células em volta do ponto e desloca o ponto por um ruído de uns 36 m: a beira da mata, do campo e da rocha sai torta e
   suave, não em quadras (08/11/2026, a pedido: "não dá pra fazer algo mais natural?"); **os lagos** (a máscara da água aberta, para os rios finos sumirem; as manchas compridas são rios, não
   lagos; o contorno traçado pixel a pixel e simplificado; o maior é o reservatório e o seu contorno é recortado na
   barragem: o funil e a crista do esboço no lugar da ponta da ilustração; a lagoa de Pine Ridge ao norte); e **as ilhas**
   (a terra cercada de água, as cinco nomeadas pela posição e os ilhotes).

A imagem em PNG (`Imgs/Lakewood/Lakewood_Referencia.png`) vem do webp pelo Blender sem janela
(`Imgs/Lakewood/Lakewood_Referencia_Converter.py`; a Unity não lê webp). A janela do desenho mostra a referência por
baixo do mapa pintado, com a transparência ajustável, para comparar.

## O relevo (a Etapa 2)

Uma grade de 20 m, feita em ordem (`RelevoDeLakewood`; código puro, roda em outra thread):

1. **As colinas da base**: 185 m com ±30 m de ruído em 2,2 km.
2. **As zonas de relevo** (polígonos com a altitude, a amplitude e o tamanho das colinas, a aspereza das cristas, o fator
   das montanhas da imagem e a largura da passagem na beira): West Plains (176 m, colinas de 16 m, as montanhas da imagem
   valendo 35%), North Range (300 m, ±80, cristas), Crowley Mountains (340 m, ±80, cristas fortes, as montanhas a 110%),
   East Hills (245 m, ±70), Southeast Ridge e Southeast Corner (235 e 260 m), South Hills (195 m, ±40), Woodcreek Bench (a
   plataforma da vila, 190 m), as duas encostas da barragem (232 e 236 m, em pé) e o pátio da usina (112 m, plano).
3. **As montanhas da imagem**: a grade da rocha vezes o fator da zona, amaciada em 220 m, somando até 230 m (mais as
   cristas do ruído, 45 m).
4. **Os picos** (Mount Crowley +40 m, Pine Ridge +35 m) e as passagens (o colo da cabeceira oeste da barragem, a Dam
   West Saddle, a 166 m, desde a Etapa 4).
5. **O aplainamento** das cidades (o chão vai para a média do relevo em 250 m, com 60 a 85% de força e a passagem na
   beira) e das instalações (o aeroporto, a estação, a base, a pedreira, o parque, o centro de visitantes).
6. **A bacia do reservatório**: a água fixa a 150 m; o fundo desce até 32 m longe da margem (a crista da barragem não
   conta como margem: junto dela o lago é fundo); fora, a margem em rampa até o relevo (900 m a oeste, rasa e suave; 350 m a
   leste, as encostas; 120 m na garganta da barragem) e, onde o chão de fora é mais baixo que a água, a beira levantada;
   a jusante da barragem, nada disso.
7. **As ilhas**: sobem da água até a altura de cada uma (a Central 42 m, para o laboratório escavado na rocha).
8. **Os rios**: a água de cada um pelo chão (2 m abaixo, sempre descendo) até a do lago onde cai; o vale em volta (mais
   largo nos rios largos, perto da nascente e na serra), o leito abaixo da água e a margem seca. O Lakewood River, que sai
   da barragem, tem a água fixa: de 106 m no pé a 84 m na borda sul.
9. **O pé da barragem**: entre as cabeceiras, do lado de fora, o chão fica a 108 m e volta ao relevo em 200 m; nas
   pontas, a passagem para o relevo em uns 70 m (desde 10/11/2026: o corte seco deixava um paredão).

Por cima, no jogo (`ChaoDeLakewood`): o detalhe pequeno do ruído, o leito dos rios na largura de verdade, as estradas (na
Etapa 5) e os pesos das 4 texturas do chão pela mistura das coberturas (a mata com as folhas, o campo com a grama seca, a
lavoura com a terra, a rocha com o cascalho, o urbano com terra e cascalho), misturadas entre as células com as beiras
deformadas pelo ruído. O relevo de longe (o mapa inteiro sem a neblina) leva a cor em cada vértice, as mesmas cores do mapa
pintado interpoladas pela face, num shader de cor por vértice (`RelevoDeLonge`), sem degraus entre os quadrados de 25 m.

## O reservatório, as ilhas e os rios (a Etapa 3, feita em 08/11/2026, aprovada em 10/11/2026)

- O formato é o da ilustração: o corpo central, os braços (o norte, com o estreitamento por onde o Pine River entra, e a
  baía estreita da ponte do norte, que voltou com a abertura da leitura em 2 px; o nordeste, até perto da Woodcreek; o
  oeste, até a Fairview; o sudeste, a baía da Lakewood), as penínsulas e as enseadas. A ponta sudoeste afunila até a
  barragem.
- **O fundo:** o vale afogado (`canalDoReservatorio` no mapa: a linha do rio de antes da barragem, do braço norte, pelo
  leste da Ilha Central e pelo sul dela, até a barragem; 450 m de largura e até 26 m a mais de fundura, crescendo para a
  barragem), por baixo da fundura do lago (14 m longe da margem, com ondulações de até 5 m). O fundo junto da barragem fica
  a 108 m (42 m de água); os braços do oeste são rasos (até uns 15 m). As ilhas contam como margem: o fundo sobe até elas
  (antes, a beira das ilhas era um paredão embaixo d'água).
- **As margens:** a linha d'água tremida por um ruído de até 14 m (as pontas e as enseadas pequenas, no lago e nas ilhas;
  a água do chão segue o relevo, não o contorno), a rampa de cada trecho mais em pé ou mais deitada (o ruído de 600 m) e,
  em trechos (mais a leste), a **costa rochosa**: a encosta sobe em pé logo depois de uma faixa de uns 5 m de cascalho (o
  relevo chega em 30% da rampa), com a textura de rocha e pedras na encosta e saindo da água rasa; nas cidades e nos
  lugares da beira (a 200 m das cidades e no raio + 120 m dos lugares), a margem é sempre deitada. A **praia** de terra e
  cascalho numa faixa de 6 a 20 m da água (torta pelo ruído, só onde o chão está baixo), o lodo embaixo d'água, a taboa e o
  capim na beira deitada (até dentro da água rasa) e nenhuma árvore a menos de 14 m da água.
- Os rios: Pine River (o principal, entra pelo norte em (7.237; 10.000) e chega no braço norte, alargando de 12 a 110 m no
  estreitamento), Riverside Creek (pela Riverside), Wood Creek (das Crowley para o braço nordeste), Lookout Creek (do
  mirante do leste para o braço leste; novo), Fairview Creek e Sullivan Creek (das colinas do oeste), Lakewood River (sai
  da barragem e vai até a borda sul). Todos descem e chegam onde devem (o teste confere). Dentro do lago, a água do rio não
  é desenhada (as duas na mesma altura brigavam).
- **O relevo de longe** (sem a neblina) fica 6 m abaixo do chão, e a copa da mata (até 11 m, no alfa da cor do vértice) só
  sobe no shader longe da câmera (de 380 a 700 m): perto, ela furava o terreno e as árvores.
- **As fotos de perto** (Zombie City Cleaner > Lakewood > Fotos de perto, `Imgs/Lakewood/Lakewood_Perto_*.png`): o
  terreno de 2 m, as plantas e a água na beira, em 7 vistas (a praia do oeste, a costa leste, a encosta mais em pé do
  leste vista da água, a Ilha Central, a foz do Pine River, a barragem vista da cabeceira oeste e a enseada do norte).
- **Fica para depois:** os ilhotes decorativos a mais, o nado (o `Agua.cs` já lê a água daqui), a água com o reflexo
  simples do URP (hoje é a água do mundo sorteado) e a vegetação e as rochas de verdade das margens (a Etapa 7).

## Os ajustes depois do teste da Etapa 3 (09/11/2026, a pedido: a água, os rios e a mata)

O usuário testou a Etapa 3 e pediu três coisas: o porquê do relevo de longe (de longe ele vê o relevo liso e, chegando
perto, o chão de verdade carregando), uma água mais realista com os rios seguindo o chão ("o rio tá acima do chão", uma
linha reta) e a mata cheia de natureza como o Guardiões da Floresta, com as árvores de folhas de verdade em vez das
formas quadradas. O Guardiões foi lido antes: lá a mata parece cheia pela densidade (uma árvore a cada ~23 m², os
arbustos e as samambaias, a grama a 3,5 tufos/m² até 42 m) e pela neblina curta (16 a 72 m na mata); as copas são
bolhas com uma textura de folhas pintada, sem recorte; a água é transparente, com a profundidade da tela e a refração;
os rios são cavados numa cota única.

- **Os rios** (feito): a linha de cada um ganha os **meandros** (`MapaDeLakewood.LinhaDoRio`: a linha do desenho
  reamostrada de 6 em 6 m, com uma onda de umas 10 larguras de comprimento e 1,6 largura de amplitude, mais o ruído
  miúdo, menor na serra e zero na nascente e na foz, que ficam onde o desenho diz) e o **canal de verdade** na largura
  exata, cavado na hora pelo `ChaoDeLakewood.Altura` (o relevo de 20 m só tem o vale): o leito em parábola (0,5 m mais
  8% da largura, no meio), o barranco subindo do pé até o alto (a margem de 0,6 a 2 m pela largura, a rampa de 1,8 a
  3,4 margens, variando pelo ruído, menor chegando no lago) e o chão de fora nunca abaixo da água; o vale do relevo
  passa a ter o piso no alto do barranco. A água dos rios é exata (`RioMaisPerto`, pelo índice dos segmentos: o nado e o
  caiaque funcionam nos riachos); a grade de 12 m fica só para os lagos. A faixa d'água segue os meandros, com o UV (os
  metros de lado e desde a nascente) e o G da cor dizendo que a água corre. As margens têm a terra e o cascalho (mais
  pedra onde o barranco é em pé), nenhuma árvore em cima do barranco, a taboa e o capim no pé dele. O teste novo
  `OsRiosTemOsMeandrosOLeitoEOsBarrancos` confere a sinuosidade, a nascente e a foz no lugar, o leito abaixo da água e
  as margens acima dela; as fotos de perto ganharam 4 vistas dos rios (`Lakewood_Perto_Rio_*.png`, da margem, rio
  abaixo).
- **A água** (feito; o `Agua.shader` novo, no lago e nos rios, e também no lago do mundo sorteado): transparente, com a
  fundura pela profundidade da tela (a absorção por canal: o vermelho some primeiro, a água fica verde-azulada), a
  refração do fundo pela cópia da tela (na Alta e na Ultra; na Média e na Baixa, por alfa), as ondinhas de um normal
  map feito por código (`TexturasDaAgua`, `T_Agua_Normal.png`; nos lagos arrastadas pelo vento do mundo, nos rios pela
  correnteza), o reflexo do céu pelo Fresnel e, no lago, **o reflexo do cenário pela câmera espelhada** (`ReflexoDaAgua`,
  meia resolução, sem sombras nem pós; só na Ultra desde 09/11/2026, antes também na Alta), o brilho do sol e das luzes (a lanterna) pela PBR da URP, a
  espuma na beira rasa e a neblina de altura feita no próprio shader (o passe dela vem antes dos transparentes).
  Conferido no Play, de dia e sem a neblina: as colinas e as árvores espelhadas no lago
  (`Imgs/Lakewood/Lakewood_Play_Reflexo.png`); na beira do lago, 3,7 ms por quadro no Editor com o reflexo.
- **A mata cheia** (feito): as árvores numa grade de 5,8 m (era 7 m; a mata a 90%); o **chão da mata** numa grade de
  3,2 m até 90 m (as samambaias, os arbustos, os tocos, os troncos caídos, as pedras e o capim; no campo, o capim alto
  e os arbustos; na rocha, as pedras), que antes só existia até 45 m; a grama com vários tufos por célula de 3,2 m (4 na
  mata, 7 no campo, 3 na lavoura e nas cidades), as samambaias pequenas entre eles, e a taboa e o capim na beira da
  água. As matrizes das plantas são feitas uma vez por bloco (a cada quadro, o TRS de 20 mil plantas pesava). Fica para
  depois: a textura do campo (a grama seca lê como terra), as clareiras e as lavouras (a Etapa 7).
- **O relevo de longe** (a resposta à dúvida): o terreno de verdade (os blocos de 64 m, de 2 m perto, com a colisão, as
  texturas e as plantas) é montado em volta dos jogadores, até 200 m com a neblina e 480 m sem ela; o resto do condado
  (12 × 10 km) é uma malha de 25 m por setor, em cores sólidas, que só aparece sem a neblina (o N), para o mapa inteiro
  caber na tela. Com a neblina do jogo, ela nem é desenhada. Para a passagem ficar menos visível, as árvores de longe
  (o LOD mais simples) agora vão até 480 m sem a neblina, a copa da mata de longe só sobe de 430 a 650 m e ganhou o
  salpicado das copas (o ruído na cor e na normal). **O horizonte que sumiu** (09/11/2026, a pedido: "eu quero
  conseguir ver o horizonte quando a neblina está desligada"): o `Graficos` forçava a cada quadro o alcance da câmera
  do prefab do jogador (150 m) por cima dos 16 km que o `LakewoodNaCena` põe sem a neblina, e a briga era decidida pela
  ordem dos Updates; agora, sem a neblina, o `Graficos` também usa os 16 km (`LakewoodNaCena.FarSemNeblina`).
  Conferido no Play, hospedando: o far da câmera do jogador fica em 16.000 m com o N.
- **As árvores com folhas de verdade** (o piloto no Blender, esperando a escolha do usuário; o `cidade_folhas.py` e as
  fotos em `Imgs/Renders/Folhas_Piloto_*.png`): quatro espécies (o bordo, o carvalho, a bétula e o abeto) em três
  opções, A a textura de folhas nas copas de hoje (a cara do Guardiões, custo zero), B as folhas recortadas em cartões
  (a silhueta de folhas; de 1,3 a 4 vezes os triângulos e o overdraw do recorte) e C a mistura (a bolha com a textura e
  metade dos cartões), com o atlas das folhas desenhado por código. A recomendação: C no LOD0 e A nos de longe. Depois
  da escolha: as outras 30 plantas, o segundo UV e o atlas no `Cidade_Arvores.fbx`, o `Vegetacao.shader` com a textura
  e o recorte, e o mundo sorteado junto.

## A hidrelétrica (a Etapa 4, feita e aprovada em 10/11/2026)

O pedido: "pode seguir com a etapa 4", depois da lista do que faltava. O prompt mestre pede, na Etapa 4, "posicionar a
barragem, adequar a garganta do vale, criar estrutura simplificada, definir rio de saída, preparar estradas de acesso". Na
seção 7, ele pede a estrutura de fora:
- a barragem com a estrada em cima e o vertedouro;
- a casa de força, a subestação e as torres de transmissão;
- os prédios administrativos, a oficina, o estacionamento e os galpões;
- as estradas de acesso, os postos de segurança e o rio abaixo.

E o estado depois do apocalipse: cinco anos sem energia, os portões fechados ou danificados, a vegetação, a ferrugem, os
veículos abandonados e as infiltrações; a barragem reconhecível, sem destruir. Tudo o que foi feito é de fora. Os
interiores (as salas e os corredores escuros) ficam para uma parte própria, como as do hospital.

**Onde fica cada coisa.** O sistema da barragem: o u ao longo da crista (0 na cabeceira oeste, 881 m na leste) e o v para
jusante.
- **A barragem de gravidade** (u de -45 a 926):
  - os blocos de uns 18 m, com as juntas escuras;
  - a face de montante em pé até 128 m e depois um pouco inclinada; a de jusante em pé até 154 m e depois em talude de
    0,8;
  - cada bloco com a base abaixo do chão em volta (nas pontas, enterrado na rocha);
  - as bicas da crista com o escorrido escuro, a umidade e o musgo no pé;
  - o nome "LAKEWOOD DAM" pintado na face de jusante (letras de 9 m) e a placa de bronze no parapeito ("COMPLETED 1962").
- **A crista de 12 m**, a 158 m:
  - a estrada de duas pistas com as linhas e os remendos, e as calçadas;
  - o parapeito de concreto do lado do lago e o guarda-corpo de pilaretes e tubos enferrujados do lado do vale;
  - os postes apagados (um torto e um caído);
  - a colisão de tudo: o carro anda nela de uma ponta à outra.
- **O vertedouro**, no meio (u 476 a 572, em cima do rio de antes):
  - 6 vãos de 12 m entre 7 pilares de 3,5 m com o nariz, as casas dos guinchos em cima e a ponte da estrada;
  - as comportas de segmento, fechadas e enferrujadas (a chapa curva, as vigas, os braços e os munhões);
  - a soleira em ogiva e a calha descendo até a bacia, com o limo.
- **As tomadas d'água** da casa de força, na face de montante: 4, com a boca escura, as grades e as travessas.
- **O pórtico rolante** amarelo em cima delas:
  - as pernas nas duas calçadas (os carros passam embaixo);
  - as vigas avançando sobre o lago, o carro do guincho, os cabos e o gancho.
- **A torre do elevador**, colada na face de jusante (u 440), com as portas no pé e na calçada.
- **As bocas das galerias de inspeção**, no pé (u 300 e 770), com as portas de aço fechadas.
- **A casa de força**, no pé, a leste do vertedouro (u 596 a 734, v 24 a 80; 138 × 56 m, 23,5 m de altura):
  - a parte de baixo de concreto, desde o fundo do canal de fuga;
  - as paredes de chapa azul com os 13 pilares e os janelões (sujos, quebrados ou tapados);
  - o letreiro "LAKEWOOD HYDROELECTRIC PLANT", com uma letra caída;
  - as bocas dos tubos de sucção, embaixo d'água;
  - a porta de enrolar e a porta de gente, na ponta leste;
  - a laje com os ventiladores, a claraboia, o musgo e a hera.
- **O canal de fuga**:
  - a bacia de dissipação no pé da calha (com os blocos amortecedores e a soleira) e o poço na frente da casa de força;
  - o fundo de concreto a 100 m, os muros e a água parada a 106 m (o material da água do lago);
  - o Lakewood River começa na abertura do muro sul (u 504 a 544, v 138), na mesma água;
  - a água dele conta como água para o nado e para a queda.
- **O pátio**, na margem leste (entre a barragem, o rio e a encosta):
  - a subestação, no alambrado: os três transformadores, os disjuntores, o barramento, os dois pórticos treliçados e a
    casinha de comando;
  - a administração: dois andares, as janelas em fita, a entrada com a marquise, o letreiro e o mastro sem a bandeira;
  - o estacionamento com as vagas pintadas e os carros;
  - a galeria de serviço: o bloco de concreto com a porta de aço e o "RESTRICTED AREA" (a entrada dos túneis da Etapa 8);
  - a oficina de manutenção e os dois galpões: de chapa, com as portas de enrolar (umas meio abertas) e os buracos no
    telhado;
  - o tanque de diesel na bacia de contenção;
  - a guarita e o alambrado com o portão (uma folha fechada com a corrente, a outra caída no chão), as placas e o
    monumento com o nome, do lado de fora.
- **O posto de controle da quarentena**, na cabeceira leste da crista:
  - o caminhão do exército, o contêiner atravessado e as barreiras de concreto em zigue-zague (passa um carro por vez);
  - os sacos de areia e as guaritas das duas cabeceiras, com as cancelas quebradas.
- **Os carros abandonados**: 13 (os do Cidade_Rua.fbx), no estacionamento, na crista (um batido no parapeito) e pelo
  pátio.
- **As linhas de transmissão**:
  - 26 torres treliçadas de circuito duplo (36 m, os braços com os isoladores, as sapatas), em duas linhas: uma para o
    leste (até a beira da cidade de Lakewood) e outra para o sul (até a borda do mapa);
  - os 6 condutores e o cabo de terra de vão em vão, com a flecha de 3% (menor onde o chão sobe; nunca a menos de 3 m do
    chão);
  - uma faixa de 16 m sem árvores embaixo.

**O chão** (os pátios da `UsinaDeLakewood`):
- Os prédios, a subestação, o estacionamento e as guaritas ficam em pátios planos, na altura média do relevo embaixo deles
  (a casa de força a 108 m; as guaritas das cabeceiras na crista), com o talude em volta.
- A crista e o canal de fuga só cortam o chão.
- O pátio e o corpo da barragem tiram as árvores e misturam o cascalho.
- A grama fica rala no cascalho e some embaixo dos prédios, do asfalto e na água.
- O relevo ganhou duas coisas:
  - **O colo da cabeceira oeste**: a passagem "Dam West Saddle", a 166 m, da ponta da crista até a planície. O morro da
    cabeceira ficava 55 m acima da crista, e a estrada não tinha por onde sair.
  - **A passagem suave no fim do pé da barragem**, em uns 70 m: o corte seco deixava um paredão reto do lado de jusante,
    nas duas cabeceiras.

**As estradas de acesso** (Secundárias, feitas pela usina e juntadas às do mapa; o mapa pintado as mostra):
- A **Lakewood Dam Road** sai da ponta leste da crista a 158 m, num corte. Ela só desce (até 12%), rente à encosta, até o
  portão do pátio (107 m), e segue pelo vale para o sul. A ponta dela, a 1 km do portão, é onde a Etapa 5 liga a rede.
- A **Powerhouse Road** sai da porta da casa de força (108 m) e chega na da barragem, no portão.
- A **Dam West Road** sai da ponta oeste a 158 m, passa pelo colo (164 m) e desce até a planície do oeste. A ponta dela,
  em (3.450; 2.900), também é da Etapa 5.
- Nenhuma vira ponte, e o chão acompanha o asfalto (o corte e o aterro). Para isso, as vias do mapa ganharam:
  - a altura presa no começo e no fim (`alturaNoComeco`, `alturaNoFim`);
  - a rampa máxima (`rampaMaxima`);
  - o "só desce" (`soDesce`).

**O túnel da barragem** (Etapa 8) agora termina no prédio da galeria de serviço, no pátio, em (4.745; 2.620).

**Os números:**
- 87 peças e uns 71 mil triângulos: a barragem 16 mil, os dois alambrados 13,5 mil, a subestação 6 mil; as 26 torres usam
  uma malha só.
- 9,7 mil triângulos de colisão.
- No Play do Editor, no pátio, com a neblina e sem hospedar: 4,0 ms de CPU, 0,5 ms de GPU, 402 draw calls e 830 mil
  triângulos na vista.

**As fotos** (`Imgs/Lakewood/Lakewood_Usina_*.png`): o vale (de jusante), a vista aérea, a casa de força e o canal de
fuga, a crista, a crista na neblina, as comportas vistas do lago, o posto de controle, os galpões e o portão.

**Fica para depois:**
- os interiores: a casa de força com as turbinas, a sala de controle, a administração, as galerias e a galeria de serviço;
- o saque e os zumbis da usina, e as portas que abrem;
- a ligação com os túneis (Etapa 8) e as estradas da rede que ligam às de acesso (Etapa 5);
- a água vertendo por uma comporta emperrada (a calha está seca), os barcos e o píer do lado do lago;
- a NavMesh (Etapa 9).

**O conserto depois do primeiro teste** (10/11/2026, a pedido: "Temos que arrumar esse bug onde a agua fica pra fora na
represa", com a foto de cima da casa de força):
- **O defeito:** a água do lago passava até 61 m para o lado de jusante da barragem, na altura do lago (150 m). Ela cobria
  o pé da barragem, a calha do vertedouro, o canal de fuga e parte do telhado da casa de força, com a beirada em degraus.
- **A causa:** a malha da água é uma grade de 24 m que passa duas células além do contorno do lago, e o chão da margem
  esconde a beirada. O contorno acaba na linha da crista, e a jusante o chão fica 40 m abaixo da água. De baixo não se
  via (a água só tem a face de cima), por isso as fotos do pé da barragem não mostravam.
- **O conserto:** na faixa da barragem, a água é cortada na linha da chapa das comportas fechadas (1,55 m a montante do
  eixo da crista; nos blocos, por dentro do concreto). O teste novo (`AAguaDoLagoParaNaBarragem`, são 18) confere o corte
  e que, no mapa todo, a água do lago não fica por cima de chão aberto fora do lago.
- As fotos: `Imgs/Lakewood/Lakewood_Usina_Agua_Depois.png` (o mesmo lugar da foto do usuário) e `_Agua_Comportas.png`.

## As estradas (a Etapa 5, feita e aprovada em 10/11/2026)

O pedido: "vamos seguir para a parte das estradas, nao vamos fazer as estradas de terra igual tem no mapa do Mundo,
vamos fazer so as estradas normais pelo mapa"; "Faça estradas que se conectem certinho, nao precisa colocar placas por
agora, o mapa tem montanhas e etc, entao algumas estradas vao subir essas montanhas e isso vai mudar a velocidade do carro
e etc, vamos fazer algo que faça sentido"; e "vamos mudar a velocidade do carro para 110km hora, o mapa é grande".

**A rede** (o plano das ligações no `EstradasDeLakewood` do projeto Unity; as decisões, tomadas pela recomendação, mais
abaixo):
- **O anel da rodovia, a SR-27** (13 m: duas faixas e o acostamento asfaltado; 6% no máximo), em cinco trechos que passam
  pelo meio das cidades (as ruas da Etapa 6 se ligam nela):
  - a East Shore Highway, da Lakewood até a Woodcreek (7,2 km): ela sobe pelas colinas do leste, passando perto do
    mirante e da serraria, porque a margem leste é a costa rochosa;
  - a North Shore Highway, da Woodcreek até a Riverside (7,4 km): ela atravessa o Pine River e cruza a ponta da baía
    estreita na **North Crossing**, uma ponte reta de 120 m de cabeceira a cabeceira (a reta mais curta de terra a terra
    sobre a baía, com pelo menos 90 m de vão);
  - a Fairview Highway, da Riverside até a Fairview (3,2 km);
  - a West Shore Highway, da Fairview até a ponta da Dam West Road (4,4 km);
  - a crista da barragem é a travessia;
  - a South Shore Highway, da estrada da barragem (logo depois da cabeceira leste, ainda no corte) até a Lakewood (5,5 km).
    A saída da garganta sobe a 7% por um corte em rocha de até 36 m: a cabeceira é uma encosta em pé.
- **As saídas do mapa**, que chegam de frente na borda (num trecho da beira escolhido pela busca): a Eastern Highway, a
  SR-14 (rodovia, da Lakewood para o leste); a River Valley Road (pelo vale abaixo da barragem, para o sul); e a Fairview
  West Road (para o oeste).
- **As secundárias** (8,4 m; 9%, as de serra 10%):
  - até os lugares: a Airport Road, a Treatment Plant Road (do fim da estrada da barragem), a Pine Ridge Road (da Riverside
    até o parque, subindo de 184 a 361 m) e a Crowley Road (da Woodcreek pelo vale até o Fort Crowley, com o ramal da
    pedreira, a Quarry Road);
  - os ramais que saem do anel: a Lookout Road (o mirante), a Sullivan Farms Road, a Westshore Road, a Marina Road (a
    marina do braço norte, por uma ponta estreita de terra), a Sawmill Road e a Visitor Center Road.
- 19 estradas e as 3 da usina: uns 30 km de rodovias e 26 de secundárias, 15 nós, 9 pontes (8 sobre os rios e a do
  lago). Sem as estradas de terra (a pedido) e sem as placas (por enquanto).

**O traçado** (`TracadoDeLakewood`, o menu Zombie City Cleaner > Lakewood > Etapa 5: traçar as estradas, uns 11 s; grava
os pontos de controle no asset, a cada 30 m):
- uma busca A* numa grade de 24 m (a ideia do traçado do mundo sorteado), com a direção de chegada no estado (a estrada
  só vira 45° por passo, e a subida da serra vira curva em vez de subir reto);
- o custo de cada passo é a distância vezes a subida (acima da rampa da estrada, o custo dispara), a encosta de lado, o rio
  (uma ponte), a beira do lago (a estrada longe da praia), as outras estradas (cruzar pode, andar junto não), as cidades
  (mais barato) e os lugares cercados (só quem vai até eles). O lago, a hidrelétrica e a beira do mapa não passam;
- depois, o caminho das células é puxado para retas em qualquer ângulo, onde a reta não custa mais, e arredondado: sem
  isso, as estradas saíam em retas de 45° com as curvas duras;
- os ramais buscam a estrada mais barata de chegar, a 45° ou mais dela e só em terra firme (o ramal do centro de
  visitantes chegava em cima da ponte do norte).

**No jogo** (`ChaoDeLakewood`, a rede, o perfil e as malhas):
- **A rede:** a ponta que encosta numa outra estrada vai para um ponto exato dela (o T), as pontas juntas viram um nó só
  (a continuação, o Y) e onde duas se cortam nasce um nó nas duas (o X).
- **As alturas:** a estrada mais importante passa primeiro (a da usina, a rodovia, a secundária) e dá a altura do nó; as
  outras chegam nela. Em volta de cada nó, todas ficam planas na altura dele (9 m) e voltam ao chão pela rampa delas: no
  cruzamento, os asfaltos ficam no mesmo plano.
- **O perfil:** o chão suavizado ao longo da estrada, a rampa limitada (a média do corte por cima com o aterro por baixo),
  as pontes com a folga pelo vão (o riacho de 15 m, uns 2,6 m; o lago e o rio largo, 6 m) e os nós por último.
- **As malhas:** o asfalto com as linhas brancas da beira e a amarela do meio (dupla na rodovia, tracejada na
  secundária; perto dos nós, nenhuma), as saias das beiradas, o guard-rail onde o aterro passa de 2,5 m, a ponte com as
  muretas e os pilares, e o pátio redondo de cada nó, cobrindo os cantos. A colisão é à parte (o asfalto, as muretas, os
  pilares e o guard-rail).
- Longe do terreno de perto, o desenho dos pedaços some (só a colisão fica): o relevo de longe fica abaixo do chão de
  verdade, e a estrada apareceria boiando.
- As torres das linhas de transmissão saem de cima das estradas novas.

**O relevo:** o "vale de passagem das rodovias" (do mapa da Blackwater: a montanha baixava numa faixa de 900 m em volta de
toda rodovia) saiu. Com o anel, ele mudava o relevo do mapa inteiro, inclusive a cabeceira da barragem. Agora as estradas
seguem o relevo.

**O carro** (a perua, o carro do Lakewood): chega a ~110 km/h no plano. Era 700 N·m e 22 m/s (64 km/h); agora 850 N·m e 44 m/s
(o som das marchas até 31 m/s). Medido na física de verdade (o prefab numa rampa de teste no Editor, com a força do
motor do jogo):

| | no plano | 6% | 9% | 12% | 0 a 60 km/h |
|---|---:|---:|---:|---:|---:|
| A perua de antes | 64 km/h | 55 | 50 | 46 | — |
| A perua de agora | 112 km/h | 99 | 92 | 86 | 5,5 s |
| O cupê de antes | 114 km/h | 102 | 96 | 91 | — |
| O cupê de agora (1250 N·m e 51 m/s, a pedido: "mude para 140") | 139 km/h | 127 | 120 (10%) | — | 4,9 s (0 a 100 em 8,5 s) |

Nas subidas do anel (6%) a perua cai para uns 100 km/h, e nas de serra (10%) para uns 90: a serra pesa, como pedido.

**Os números:** a cena monta em 4,9 s (4,4 s antes das estradas); a rede e os perfis levam uns décimos. Os 23 testes do
Editor passam (5 novos: a rede ligada, os cruzamentos na mesma altura e planos, a rampa e a água só pela ponte, as
estradas só se encostando nos nós, as saídas na beira).

**As fotos:** `Imgs/Lakewood/Lakewood_Mapa.png` (o mapa com a rede) e `Lakewood_Estradas_Cruzamento.png` (o meio da
Lakewood), `_PonteDoNorte.png`, `_Serra.png` (a Pine Ridge Road subindo a 10%) e `_CorteDaBarragem.png` (de quem dirige).

**Fica para depois:**
- as placas (as de direção nos cruzamentos e as de entrada das cidades) e as barreiras da quarentena nas saídas;
- as ruas das cidades (a Etapa 6: as rodovias entram pelo meio delas);
- os carros abandonados e as blitz pelas estradas;
- a East Shore Highway pode ser puxada para mais perto da margem leste (ela dá a volta pelas colinas), se o usuário
  quiser;
- o relevo de longe não mostra as estradas (desde 10/11/2026, ele desce nos cortes, mas sem o asfalto).

## O terreno por cima da pista (10/11/2026, a pedido: feito, aprovado com a Etapa 5)

O pedido: "um pouco antes de chegar na represa, no meio da pista o terreno tá cobrindo ela, mas dá pra passar e aí some",
o mesmo na estrada que desce da crista sem entrar na barragem e, na vista da crista, "uma parte do terreno cobrindo toda
uma parte do terreno" (três prints de moto).

**A causa:** o relevo de longe (a malha de 25 m de cada setor, sempre desenhada por baixo dos blocos de perto) era o relevo
natural 6 m abaixo. Onde o chão de perto desce mais que isso, ele aparecia por cima, sem colisão (por isso dava para
passar), e sumia quando a câmera passava por baixo dele. Medido no mapa todo, de 5 em 5 m: 8.377 pontos. Os piores:
- o corte da South Shore Highway chegando na cabeceira leste: 32 m;
- a Dam Road descendo da crista: 19 m;
- a Powerhouse Road: 12 m;
- os pátios e a crista da usina;
- o canal dos rios: até 6 m;
- o pé de dois penhascos do relevo, entre dois vértices de 25 m: até 12 m.

**O conserto** (`ChaoDeLakewood.Longe.cs`):
- A grade de longe do mapa todo é feita uma vez, em outra thread enquanto o resto monta (uns 0,9 s, sem espera).
- Cada vértice desce só o que precisa para a malha ficar 2 m abaixo do chão de perto nos pontos de conferência:
  - a grade de 10 m do relevo no mapa todo (o relevo menos o detalhe de 1,4 m);
  - a de 5 m com a altura de verdade perto das estradas (até 34 m do talude), dos rios e na caixa da usina (a mais baixa
    entre a exata e a dos blocos de 16 m).
- A conta é uma passada só, abaixando os três cantos do triângulo de cada ponto pelo peso de cada um.
- Abaixaram 2.266 dos 193 mil vértices, o mais fundo 76 m (o corte da crista); o resto fica 6 m abaixo do relevo, como antes.
- Os setores pegam a altura dessa grade.

**Junto, o mesmo defeito no terreno de perto:**
- **Os blocos de 8 e 16 m** (a mais de 220 m, só sem a neblina) cobriam a pista em 8% e 25% dos pontos, até 3,1 m. Agora,
  perto da pista, os cantos dos triângulos ficam embaixo do asfalto mais baixo ao alcance deles.
- **Perto dos nós**, a estrada que sobe até o nó passa por dentro do acostamento da outra, e o chão da mais perto (a mais
  alta) cobria a pista dela (32 cm na Marina Road). Agora o chão fica embaixo da mais baixa.
- **A grama densa** furava o tabuleiro da crista (o chão fica 10 cm abaixo dele) e agora não nasce mais ali.

**Os números:**
- O relevo de longe não fica por cima do chão de perto em nenhum dos 19,2 milhões de pontos do mapa (de 2,5 em 2,5 m).
- Nos blocos de 2 m (os de perto, com a colisão), o terreno não cobre a pista em nenhum ponto. Nos de 4 e 8 m, são 6
  pontos de 92 mil, até 21 cm (nas curvas em rampa). Nos de 16 m, são 94, até 51 cm.
- A consulta das estradas virou uma passada só (perto da represa, cada uma varre uns 150 trechos).
- A cena monta em 5,4 s (4,8 antes). Os 25 testes passam, com dois novos: o relevo de longe embaixo do chão de perto e o
  terreno sem cobrir a pista em cada passo.

**As fotos** (no Play, com a neblina), nos três lugares dos prints:
- `Imgs/Lakewood/Lakewood_Longe_SouthShore.png` (o corte da South Shore);
- `_DamRoad.png` (a Dam Road descendo da crista);
- `_Crista.png` (a crista olhando para o leste);
- `_Represa_DeCima.png` (de longe, sem a neblina).

## A casa da partida (10/11/2026, a pedido: feita, aprovada com a Etapa 5)

O pedido: "coloque para o jogador nascer perto de alguma estrada, pode ser a estrada que ajustamos que vai para a represa,
ele já deve nascer com os carros e as motos ao lado todos arrumados, ... algumas mesas com as armas do jogo e munição, ...
uma casa perto da estrada ... sem mobília, só com mesas e armas".

**O lugar:** na South Shore Highway, a 1,48 km da cabeceira leste da barragem, numa reta, ao sul da pista, em (6.254;
2.724), setor C2. Ali o chão fica na altura do asfalto (de 196,2 a 196,5 m, com a pista a 196,3). Ele é achado pela
ferramenta "Etapa 5: a casa da partida". A ferramenta olha os primeiros 2,2 km da estrada, nos dois lados, e fica com o
trecho reto mais plano, longe da água, dos rios, da usina e das outras estradas. Ela grava o lugar no mapa
(`casaDaPartida`) e a partida no terreiro.

**O terreiro** (`LugarDaPartida`, no `ChaoDeLakewood.Partida.cs`):
- Tem 28 × 32 m, plano na altura da pista, de cascalho, com um talude de 10 m até o chão de fora.
- Não tem grama nem pinheiros (os tufos furariam o piso da casa).
- A grade do relevo de longe confere o terreiro também.

**A casa:** uma térrea do kit (a semente 11: 15,4 × 11,7 m, com a varanda e a garagem de portão aberto), de frente para a
estrada, com a parede da frente a 24 m da linha do meio dela.
- Sem móveis.
- As portas são vãos abertos: as folhas que abririam saem da malha, porque o Lakewood ainda não tem o sistema das portas.
- As janelas têm o abandono do mundo, e a casa tem os pisos dos passos, a neblina de dentro e o eco.
- Cinco mesas de 6 lugares da mobília nova (em ×1,30), a 1 m das paredes: duas na sala, uma na cozinha e uma em cada
  quarto.

**Os veículos e as armas** (o anfitrião, ao abrir a partida):
- **Os veículos:** a perua, o cupê, a moto cross e a Fat Boy, nas vagas do terreiro, de frente para a estrada.
  - Estão arrumados: as peças, os pneus e o estepe a 100%, o macaco e o tanque cheio (o `Carro.ArrumadoDeFabrica`, o
    mesmo do F3).
  - O carro solto de antes, ao lado da partida, saiu.
- **As mesas** recebem 41 itens do chão, que se pegam com o E:
  - na primeira mesa da sala: as 4 chaves dos veículos, as duas pistolas, a sniper e o rifle;
  - na segunda: as duas submetralhadoras, o rifle de sucata e a espingarda;
  - na cozinha: as 6 munições, com 4 caixas cheias de cada;
  - num quarto: as 5 armas brancas.
- **O jogador** nasce entre os veículos e a casa, de frente para ela, com os veículos à esquerda.

**Os números:** a cena monta em 6,6 s, porque a mata em volta da partida nova é mais cheia (3.888 pinheiros em volta, contra
2.564 na margem da Lakewood). Os 26 testes do Editor passam, com um novo: a casa no terreiro plano, as vagas e os
nascimentos dentro dele, fora da pista e da casa, e sem grama, pinheiros nem água.

**As fotos:** `Imgs/Lakewood/Lakewood_CasaDaPartida_Terreiro.png` (a casa e os veículos), `_Sala.png` e `_Mesas.png` (as
armas) e `_Municao.png` (a cozinha).

**A casa perto do lago** (12/11/2026, a pedido: "vamos mudar a casa de spawn hoje para a casa perto do lago, assim a gente
nasce perto da cidade"; feita, esperando o teste): a casa da partida saiu da South Shore e foi para o lote de casa da
Woodcreek mais perto do lago, o 114, na Sawmill Road, no canto sudoeste da vila (a água a 33 m do meio do lote; os outros
lotes de casa ficam a mais de 280 m dela). O lote ficou vazio (`CasaDaPartida.TomaOLote`: sem a casa da cidade e sem o
patamar; o desenho da cidade, refeito, também o deixa vazio), e a casa, o terreiro, os veículos, as mesas e o nascimento
são os mesmos, de frente para a Sawmill Road em (9.691; 6.288), com o terreiro a 153,95 m. A ferramenta virou "A casa da
partida (perto do lago, na Woodcreek)" (`CasaDaPartidaDeLakewood.NaBeiraDoLago`; o achar da South Shore ficou no código).
A Sawmill Road desce uns 0,4 m nos 14 m de cada lado da casa: nos 3 m do terreiro junto do acostamento o chão acompanha a
pista em rampa, e o plano começa antes das vagas (o teste aceita essa faixa com 0,5 m). No Play, o jogador nasce no
terreiro, a 11 m da casa, com os 4 veículos ao lado. As fotos em `Imgs/Lakewood/Lakewood_CasaDaPartida_Lago_Rua.png` e
`_Alto.png`.

## A Woodcreek (a Etapa 6, a primeira cidade; 11/11/2026, o plano aprovado: "Nome do hospital ok", "Tunel por enquanto vamos deixar sem", "pode seguir")

O pedido: "vamos começar pela woodcreek" e, no meio do plano, "pode colocar o hospital e a loja de armas novas nessa
cidade".

**O que o prompt pede.** Para a Etapa 6: marcar os limites urbanos, preparar os quarteirões, reservar as áreas dos edifícios
e posicionar as instalações importantes ("nesta primeira fase, não é necessário construir todos os interiores ou edifícios
definitivos"). Para a Woodcreek: o pequeno conjunto de ruas, as casas de madeira, a serraria, o posto de combustível, o
pequeno comércio, as cabines e os chalés e as trilhas e estradas florestais. O túnel dela (a Etapa 8) é a "saída de
emergência".

**O lugar** (medido no chão de hoje):
- A vila fica num banco plano de 0,58 km², a 187 a 192 m, de x 9.650 a 10.420 e de z 6.430 a 7.300.
- Em volta, encostas: a oeste e ao sul o chão desce uns 40 m até o braço norte do lago; a leste ele sobe para as East
  Hills (até 286 m).
- A área reservada tem 1,22 km²: 64% dela com menos de 6% de inclinação, 81% com menos de 10%.
- **A Main Street é a SR-27.** A East Shore Highway chega do sul (sobe pela volta em grampo ao sul do banco) até o nó do meio,
  em (9.964; 6.690). Dali, a North Shore Highway sai reta para o norte (x 9.996, de z 6.690 a 7.240) e atravessa o Wood
  Creek.
- **A Crowley Road** sai do mesmo nó e corre paralela à Main Street, 48 m a leste, por uns 600 m.
- **A Sawmill Road** sai 54 m ao sul do nó, vai para oeste e desce a encosta até a serraria, na beira do lago.
- O Wood Creek passa no noroeste do banco.

**O esboço** (`Imgs/Lakewood/Lakewood_Woodcreek_Esboco.png`, 1 px = 1 m; feito num comando do Editor, ainda não é uma
ferramenta):
- **As ruas novas**, de 7 m:
  - a Pine Street (para leste, a partir da Main, no sul do comércio);
  - a Fir Street (para leste, a partir da Crowley Road);
  - a Cedar Street (de norte a sul, no leste);
  - a Creek Street (de norte a sul, no oeste) e a Spruce Lane (entre a Creek e a Main);
  - a alça das cabanas, de 5 m.
- **O comércio:** 12 lojas nos dois lados da Main Street, entre a Pine Street e a Spruce Lane.
- **Os lotes de casa:** 96 lotes de 26 × 36 a 40 m, de frente para a rua. Um em cada quatro fica vazio, com a mata: 71 casas
  e 25 lotes vazios.
- **Na entrada sul:** o posto (do lado leste), a loja de armas logo ao norte dele e o motel (do lado oeste).
- **O hospital**, num lote de 96 × 62 m, de frente para a Pine Street, a 120 m da Main Street. A igreja fica ao lado dele.
- **Na Cedar Street:** a escola.
- **Na Main Street, ao norte:** os bombeiros e a guarda florestal, na entrada norte.
- **No noroeste:** umas 20 cabanas numa alça, na beira do Wood Creek.
- Cada lote cabe no chão com menos de 2,5 m de desnível e fica a mais de 14 m da água e dos rios.

**O que se reaproveita** (o levantamento do código):
- **O kit das construções serve direto** (as casas, as lojas, o posto, o hospital e a loja de armas). A casa de verdade é
  `new Casa(planta, medidas) { portasVivas = true }.Montar()`. Ela precisa só da planta, da matriz (o meio, o chão e o giro) e,
  na loja, da altura do chão em volta.
- **A montagem das cidades do mundo sorteado não serve como está.** O traçado, os lotes, o chão, o streaming (o
  `TerrenoDoMundo`: as casas de verdade até 95 m, a hera até 70 m e os móveis até 60 m), o NavMesh, os zumbis e os objetos da
  rua estão presos aos dados do mundo sorteado (`DadosDoMundo`, `MundoDaPartida`, a semente). Mexer no gerador mudaria os
  mundos sorteados (a versão 13).
- **A cena do Lakewood ainda não tem** os sistemas das portas (`PortasDoMundo`), dos móveis e do saque (`MobiliaDoMundo`,
  `MobiliaNovaDoMundo`), do NavMesh e dos zumbis.
- **A casa da partida já usa o kit:** a térrea, o abandono das janelas, os pisos, a caixa de dentro e o eco. Ela não tem os
  móveis, o saque, as portas, a hera, as pichações nem a caixa de longe.

### As decisões (aprovadas em 11/11/2026, com o túnel fora por enquanto)

1. **O desenho é fixo** e fica no `MapaDeLakewood`.
   - Ele guarda as ruas novas e os lotes: o lugar, o giro, o tamanho, o tipo e a semente de cada um.
   - Uma ferramenta faz o desenho uma vez, a partir do esboço; depois dá para mexer nele.
   - Toda partida vê a mesma vila, com as mesmas casas.
   - O saque, as portas trancadas e o estado da sala segura da loja de armas saem da semente da partida, como no mundo
     sorteado.
2. **O kit das construções direto**, por um montador do Lakewood: as casas, as lojas e o posto, com a mobília nova, o
   abandono, a hera e as pichações. O gerador do mundo sorteado não muda.
3. **A vila fica no banco plano.** O resto da área reservada continua mata.
4. **As ruas.**
   - As da Etapa 5 continuam como estão: a Main Street é a SR-27, sem mudar o traçado.
   - As novas têm 7 m, com a faixa amarela e sem calçada (uma vila de floresta).
   - A Main Street ganha a calçada e o meio-fio no trecho do comércio.
   - A Crowley Road fica onde está: as lojas do lado leste da Main dão os fundos para ela.
5. **O hospital** é o Hospital Principal inteiro: os 4 andares, os móveis, o abandono, os papéis e os sons.
   - Fica na Pine Street, de frente para ela.
   - Vira o hospital do condado (o que explica o tamanho dele numa vila).
   - O nome na torre passa de BLACKWATER GENERAL HOSPITAL para **LAKEWOOD COUNTY HOSPITAL**, e os papéis citam o condado.
6. **Sem túnel por enquanto** (a pedido: "Tunel por enquanto vamos deixar sem"). A proposta era o túnel "Lab – Lakewood
   Hospital" (da Etapa 8) chegar na área A-01 do subsolo do hospital da Woodcreek e ser também a saída de emergência dela.
   Os túneis ficam para a Etapa 8; até lá, a porta da A-01 continua soldada, como na cena do hospital.
7. **A loja de armas nova** fica na entrada sul, do lado leste da Main Street, entre o posto e o comércio, com o
   estacionamento na frente.
   - O estado da sala segura sai da semente da partida: a saqueada (55%), a parcial (30%) e a intacta (15%).
   - Ela vai como está na cena de teste: a parte 2 dela ainda espera a aprovação.
8. **Os especiais que ainda não existem** ficam com o lote reservado, com o chão plano e sem as árvores: a igreja, a escola,
   os bombeiros, a guarda florestal, o motel e as cabanas.
   - Eles são feitos depois, um por um, como o hospital: cada um com a cena de teste.
   - A serraria fica com os lugares, fora da vila, na beira do lago.
9. **Abandonada há 5 anos e sem energia:** o grau "muito" do abandono do mundo, os carros parados e o mato.
10. **Os zumbis e o NavMesh** entram numa parte própria, a 5 (o Lakewood ainda não tem zumbis). Os dados do mundo sorteado
    são trocados por uma interface pequena (a altura do chão, se o ponto é cidade, se o chão em volta está pronto), que o
    Lakewood também cumpre.

### As partes, em ordem

1. **O desenho.**
   - A ferramenta "Etapa 6: a Woodcreek" grava as ruas e os lotes no asset.
   - A imagem do plano é feita a partir do asset.
   - Os testes conferem que os lotes estão planos, fora das ruas, da água e dos rios, de frente para uma rua e sem um em
     cima do outro.
2. **O chão e as ruas.**
   - O banco e cada lote aplainados: o patamar, como o terreiro da partida.
   - As ruas novas no chão: o asfalto, a faixa e, na Main, o meio-fio e a calçada.
   - Os cruzamentos com a SR-27, os caminhos e as entradas dos carros.
   - Sem mata e sem grama dentro dos lotes.
   - Os postes apagados, as placas das ruas e a placa "Welcome to Woodcreek" (com a população riscada).
3. **As construções do kit.**
   - As casas, as lojas e o posto de verdade perto de quem está (até uns 95 m), com os móveis, o abandono, a hera, as
     pichações e os carros nas garagens.
   - A caixa de longe para o resto.
   - O montador e o streaming do Lakewood.
4. **O hospital e a loja de armas** no lugar, cada um com o terreno dele.
   - A casca e os andares carregam perto de alguém, como no mundo sorteado.
   - O hospital ganha o nome novo.
5. **O jogo dentro das construções:** as portas que abrem e trancam, o saque, o save, o NavMesh e os zumbis de dentro e da
   rua, no Lakewood.
6. **Os especiais:** a igreja, a escola, os bombeiros, a guarda florestal, o motel e as cabanas, um por um.

### A parte 1, o desenho (11/11/2026, feita; as partes 2 a 6 seguiram sem esperar a aprovação, a pedido)

- **No mapa** (`MapaDeLakewood.ruas` e `.lotes`; os tipos em `Mundo/Lakewood/CidadesDeLakewood.cs` do projeto Unity):
  - cada rua nova guarda a cidade, os pontos do meio (retas entre eles), a largura e se é uma alça;
  - cada lote guarda a cidade, o uso (casa, loja, vazio, posto, hospital, loja de armas, especial ou cabana), o nome dos
    especiais, a rua da frente, o meio, o giro (o de uma obra do kit, com a frente no -z dela), a largura na frente, o fundo
    e a semente (pelo lugar: refeito o desenho, o mesmo lote sai com a mesma casa).
- **A ferramenta** (Zombie City Cleaner > Lakewood > Etapa 6: a Woodcreek (as ruas e os lotes), uns 26 s) é a receita do
  esboço:
  - as 6 ruas novas, com as pontas ligadas na linha do meio da estrada ou da rua mais perto (a Pine Street na Main, a Creek
    Street na Sawmill Road, a Spruce Lane na Main);
  - os 8 especiais nos lugares do esboço (todos couberam, com o desnível de 0,6 a 1,4 m);
  - os lotes enchidos ao longo das ruas onde cabem: dentro do contorno da cidade, a 2 m da beira das ruas e das estradas,
    a 1 m dos outros lotes (o choque exato dos dois retângulos), com menos de 2,5 m de desnível e longe da água e dos rios
    (14 m).
  - Rodar de novo troca as ruas e os lotes da Woodcreek pelos da receita (o que foi mexido à mão se perde). Ela grava também
    o plano.
- **O resultado:** 6 ruas novas (2 km) e 141 lotes: 69 casas, 26 vazios (27%), 12 lojas, o posto, o hospital, a loja de
  armas, os 5 especiais e 26 cabanas.
  - O lote da loja de armas cresceu para 36 × 52 m: o terreno dela tem 32 × 49,5 m (o estacionamento de 12 m na frente).
  - O do hospital ficou em 96 × 62 m (o terreno tem 82,5 × 51,5).
  - Entre o posto e a loja de armas ficou uma casa (o lugar sobrava).
- **O plano** (`Imgs/Lakewood/Lakewood_Woodcreek.png`, 1 m por pixel; Lakewood > Etapa 6: a imagem da Woodcreek refaz só
  ele, do asset): o chão sombreado com as curvas de 2 m, a água, as estradas e as ruas na largura de verdade, os lotes com
  a construção de cada tipo, os nomes e o painel com a legenda e os números.
- **O mapa grande** (a Imagem do mapa): na cidade com os lotes, a mata fica, o contorno sai tracejado, as ruas novas e os
  lotes aparecem (a construção de cada tipo, com a cor do plano) por cima das estradas, e a legenda e os números contam a
  Etapa 6.
- **Os testes** (3 novos, são 29):
  - `AWoodcreekTemAsRuasEOsLotesDoPlano`: as ruas, as contas de cada tipo, os especiais, um em cada quatro vazios, o hospital
    e a loja de armas com o terreno deles no lote, a loja de armas entre o posto e o comércio, de frente para a Main, e as
    sementes;
  - `OsLotesDasCidadesFicamNoLugar`: dentro da cidade, o desnível (2,5 m nos comuns, 6 nos grandes), longe da água e dos
    rios, fora das ruas e das estradas, de frente para a rua dele e nenhum encostando no outro;
  - `AsRuasDasCidadesSeLigamNasEstradasESaoSecas`: cada rua nova ligada na rede das estradas e nenhum ponto na água ou em
    cima de um rio.

### As partes 2 a 6 (11 e 12/11/2026, feitas com o usuário fora, esperando o teste)

O pedido: "quero que vc fique trabalhando nessas 6 partes da cidade que vc listou... Pode fazer tudo e depois faça uma
lista do que foi feito e do que eu preciso testar... nao precisa para para me pedir aprovação de nada". As fotos do Play
estão em `Imgs/Lakewood/Lakewood_Woodcreek_*.png` (a rua, as obras, o hospital, a loja de armas, os zumbis e os
especiais).

**A parte 2, o chão e as ruas** (`Mundo/Lakewood/ChaoDeLakewood.Lotes.cs`, `RuasDaCidade.cs` e
`World/LakewoodNaCena.Cidades.cs` do projeto Unity):
- cada lote ocupado é um patamar plano na altura dele, com o talude em volta (o chão de fora passa para o do lote), sem a
  mata e com a grama baixa e umas manchas de terra batida; os vazios ficam com a mata fechada;
- as ruas novas são estradas do tipo Rua (7 m, sem as linhas brancas da beira) na rede das estradas: os cruzamentos em T
  com a SR-27 saem sozinhos, planos;
- a calçada do comércio na Main, as placas com o nome das ruas nas esquinas, a "Welcome to Woodcreek" nas duas entradas
  (com a população riscada) e os postes apagados, numa malha só por cidade, com a colisão.

**A parte 3, as casas, as lojas e o posto** (`Mundo/Lakewood/ObrasDeLakewood.cs` e `World/LakewoodNaCena.Obras.cs`):
- as 69 casas são as do kit (a térrea e o sobrado, sem porão), as 12 lojas pela cota da vila (um mercado, uma farmácia, duas
  24 horas e o resto comum) e o posto com a loja de conveniência;
- de longe, uma caixa por lote (as paredes e o telhado; desde 12/11/2026, nas casas, a forma da de verdade: veja "Os ajustes
  do layout"); perto de alguém (95 m), a obra de verdade, montada em outra thread
  (duas ao mesmo tempo, uma posta por quadro, a colisão cozida em outra thread), com a mobília antiga e a nova (com o saque),
  as portas e as janelas que abrem, a hera, as pichações, os carros nas garagens e os caminhos de concreto até a rua;
- na cena, as "Obras das cidades" (o NetworkObject com a `MobiliaDoMundo`, a `MobiliaNovaDoMundo`, as `PortasDoMundo`, o
  `HospitalDoMundo` e o `NavMeshDoMundo`), pela ferramenta Lakewood > Etapa 6: as cidades na cena.

**A parte 4, o hospital e a loja de armas:**
- **O hospital** é o `HospitalDoMundo` do mundo sorteado, fixado no lote (`Fixar`): a casca e os andares carregando perto de
  alguém, os móveis, o saque, as portas, os papéis, os sons, os zumbis de dentro e o subsolo escuro, como lá. Na torre, o
  LAKEWOOD COUNTY HOSPITAL (o `nomeNaTorre`), e os papéis são os do condado (`Resources/Documentos/Hospital_Lakewood.txt`:
  os mesmos 20, com Lakewood no lugar de Blackwater). O chão do Lakewood abre o buraco do subsolo debaixo do corpo. O túnel
  do laboratório fica para depois (a decisão).
- **A loja de armas** é a da cena de teste (`LojaDeArmas`), com o estado da sala segura pela semente (a saqueada, a parcial ou
  a intacta), as marcas, as pichações, a hera, os 105 móveis (a porta do cofre aberta, entreaberta ou trancada pelo estado),
  a calçada, o estacionamento e os becos, de frente para a Main.

**A parte 5, o jogo dentro das construções:**
- as portas (abrir, trancar, a gazua e o pé de cabra) e o saque nos móveis já valiam no Lakewood desde a parte 3 (as mesmas
  `PortasDoMundo` e `MobiliaNovaDoMundo`); conferido no Play: o saque aparece dentro das casas;
- **o NavMesh e os zumbis:** o `NavMeshDoMundo` e o povoamento dos zumbis (o `ZombieManager.Povoamento`, o dos prédios e o do
  hospital) passaram a ler o chão por uma interface (`IChaoDosZumbis`, em `World/ChaoDosZumbis.cs`): a altura, se o ponto é
  cidade, se o chão em volta está pronto, a água e a semente. O mundo sorteado a cumpre pelos dados dele (o mesmo de antes) e
  o Lakewood pelo `LakewoodNaCena.Zumbis.cs`: o chão está pronto quando os blocos em volta estão no detalhe de 2 m com a
  colisão e as obras perto já têm a colisão cozida; a cidade é o que fica a 12 m de um lote; a água do lago, dos rios e do
  canal sai do NavMesh. Na cena, o "Zumbis" do mundo sorteado (o `ZombieManager` com os 19 modelos, sem hordas). No
  anfitrião, o chão do Lakewood tem a colisão também em volta dos outros jogadores (os zumbis deles andam ali).
- Medido no Play (um jogador, no meio da Woodcreek): a área do NavMesh montada com 9 ms de coleta; uns 20 zumbis em volta (9
  dentro das casas, o mais perto a 44 m) e, na frente do hospital, 34 dentro dele, nos três andares que carregaram.
- **O save não entrou:** o save é o do mundo sorteado (a lista de mundos do menu, o Dia 0, a versão do gerador), e o
  Lakewood não tem como começar e continuar uma partida; fica para a decisão do usuário (veja "Ainda abertas").

**A parte 6, os especiais** (`Mundo/Lakewood/EspeciaisDeLakewood.cs`): cada um é uma planta desenhada para o kit das casas (o
`Casa` levanta as paredes, as janelas, o telhado, as portas que abrem, a mobília dos cômodos com receita e o saque nela), e o
que a casa não tem vai por cima (`Extras`). O kit ganhou os cômodos "nave" (as janelas só nos lados) e "aula", as janelas do
"escritorio" e o vão largo para fora sem folha (os portões dos bombeiros). Os nomes nas placas são em inglês, e eles não se
espelham (as letras sairiam ao contrário), menos as cabanas.
- **A igreja** (12 × 22 m, o pé-direito de 5,4 m, as janelas altas): a nave com uns 18 bancos dos dois lados do corredor, o
  presbitério com o degrau, o altar com a toalha, o púlpito e a cruz; a sacristia e o banheiro no fundo; a torre na frente
  (a entrada passa por baixo dela), o campanário aberto com o sino, a ponta e a cruz, o WOODCREEK COMMUNITY CHURCH e o quadro
  de avisos no gramado ("SUNDAY SERVICE 10AM", e por cima, pichado, "GOD HELP US ALL").
- **A escola** (34 × 19 m): o corredor de ponta a ponta com as saídas nas pontas, a entrada com a secretaria, seis salas de
  aula com o quadro-negro na parede de dentro, a mesa da professora e umas 20 carteiras (umas caídas), o banheiro e o
  depósito; o WOODCREEK SCHOOL na fachada, o mastro com a bandeira rasgada e o bicicletário.
- **Os bombeiros** (17 × 14 m, o pé-direito de 4,4 m, de tijolo): o galpão com os dois portões abertos (4,2 m), as faixas
  amarelas das vagas, os armários com os capacetes, o carretel e as mangueiras; o escritório, a cozinha, o alojamento e o
  banheiro do lado; o WOODCREEK FIRE DEPT, o STATION 1 e a sirene; a entrada de concreto até a estrada.
- **A guarda florestal** (13 × 10 m): a recepção, a copa, o alojamento, o banheiro e o depósito, a garagem da caminhonete, a
  chaminé, a placa de madeira com as letras amarelas (WOODCREEK / RANGER STATION) e a antena do rádio com os estais.
- **O motel** (37,6 × 8,4 m): a recepção e 8 quartos em fila, cada um com o banheiro e a porta para a varanda comprida (com o
  número em cima), o OFFICE, a máquina de gelo, o estacionamento de asfalto com as vagas e a placa alta MOTEL / NO VACANCY.
  O guarda-corpo da varanda corre a frente toda: entra-se pelos degraus da recepção.
- **As 26 cabanas** do Cabin Loop (7 a 7,8 × 6 a 6,6 m): a sala e a cozinha na frente, o quarto e o banheiro atrás, a varanda
  e a chaminé, umas com as tábuas por dentro das janelas, cada uma sorteada (as cores, o telhado, o espelho).
- Montadas em outra thread em 13 a 134 ms (o motel é o maior: 16 mil triângulos).
- O teste novo (`OsEspeciaisEAsCabanasTemPlantaCabemEMontam`): os 5 especiais e as 26 cabanas têm planta, cabem no lote e
  montam, com a colisão e as portas (são 31).

**Ficou de fora ou para depois:** o save do Lakewood; o túnel do laboratório; o caminho da porta do hospital até a rua (a
calçada dele para no gramado); as outras três cidades (só a Woodcreek tem os lotes); e o teste com dois jogadores.

### Os ajustes do layout (12/11/2026, a pedido, depois do primeiro olhar; feitos, esperando o teste)

O pedido: "as casa tem muito espaçamento entre elas ficando espaços vazios, temos que repensar o layout e tbm colocar arvores
nos espaços que ficarem muito vazios"; "com a neblina desligada, a mudança do lod das casas da casa simples com a casa mais
detalhada ta numa distancia muito pequena"; e "Essa trilha da rua ate a porta ta meio bugada e poderia ser melhor, tipo um
trilho em vez de ser reto". Os especiais ficam como estão até as referências do usuário ("por enquanto deixa esses ai").
As fotos: `Imgs/Lakewood/Lakewood_Woodcreek_Layout.png` (de cima), `_Trilha.png` e `_SemNeblina.png` (a rua comprida sem a
neblina, com as casas de verdade até o fim).

- **O layout** (a receita do `WoodcreekDeLakewood`, rodada de novo; os lotes e o plano no asset e na imagem):
  - os lotes de casa com 24 m de frente e 30 a 34 de fundo, 2 m entre um e outro, a frente a 3 m da beira da rua (5 nas
    estradas), e um vazio em cada oito (12%; eram 27%); as lojas com 24 × 30 m, a 2,5 m da beira e 2 m entre elas;
  - **o resultado:** 162 lotes: 100 casas (36 térreas e 64 sobrados), 15 vazios, 13 lojas, o posto, o hospital, a loja de
    armas, os 5 especiais e as 26 cabanas;
  - a casa fica a 6 m da frente do lote (eram 4: o gramado da frente e o lugar da trilha fazer a curva; o
    `ObrasDeLakewood.RecuoDaCasa`). Todas as 100 cabem.
- **As árvores** (`ChaoDeLakewood.PlantaNaCidade`, a chance mínima de uma árvore em cada ponto):
  - nos vazios, a mata fechada (85%);
  - nos quintais das casas, das cabanas e dos especiais, umas árvores atrás (20% nas casas, 7% nos especiais, 55% nas
    cabanas) e dos lados, a 4 m da construção, a 2,5 m da trilha e a 1,2 m da beira do lote; a frente fica limpa;
  - entre os lotes (até 2,5 m de um lote ocupado), nada;
  - no resto da cidade com os lotes (entre as ruas, atrás dos quintais), a mata com as clareiras (de 45% a 3%, pelo ruído
    das clareiras).
- **As casas de longe** (`ObrasDeLakewood.CasaDeLonge`): a caixa de longe das casas, das cabanas e dos especiais é a forma da
  casa de verdade: o corpo até o forro, o telhado de duas águas com a cumeeira para o mesmo lado, o beiral e os oitões, as
  janelas escuras com a moldura (uma a cada uns 3,6 m de cada parede de cada andar, fora da porta, da garagem e da
  chaminé), a porta da frente na cor dela, a varanda com o telhadinho e os pilares, a garagem coberta com o telhado de uma
  água e o portão, e a chaminé; a igreja ganha a torre com a ponta e a cruz (`EspeciaisDeLakewood.TorreDeLonge`). Uns 300 a
  600 triângulos, com o material das casas (o abandono), como a de verdade: a troca não muda a cor.
- **A distância sem a neblina** (`LakewoodNaCena.raioDasObrasSemNeblina`, 170 m; com a neblina continua 95 m): sem a neblina,
  as obras de verdade montam até 170 m. Medido na Fir Street, de dia e sem a neblina: a CPU 11,1 ms, a GPU 9,2 ms, 1.577
  draw calls, 2,7 milhões de triângulos e 25 obras montadas; com 95 m, 9,4 ms, 7,9 ms, 1.553 draw calls e 14 obras. As obras
  a mais custam uns 1,7 ms de CPU e 1,3 de GPU; o resto é a mata desenhada até 480 m sem a neblina.
- **A trilha da porta até a rua** (`ChaoDeLakewood.Lotes.cs`, o `TracarATrilha`, e o `PedrasDaTrilha` do
  `LakewoodNaCena.Obras.cs`), nas casas, nas cabanas e nos especiais:
  - uma curva (a Bézier cúbica) do pé dos degraus até a beira do acostamento da rua, chegando 2 a 5 m para o lado (o lado
    oposto ao da entrada do carro), com um ponto a cada meio metro;
  - as pedras chatas e tortas (de 6 lados, de 0,4 a 0,6 m) a cada 0,62 a 0,78 m, um pouco para cada lado, umas faltando, em
    quatro tons de pedra e de concreto, cada uma 3 cm acima da malha do terreno;
  - por baixo, a terra batida (uns 1,5 m de cada lado, por cima do peso do lote) e sem a grama alta na trilha;
  - a entrada do carro e a porta das lojas continuam com a faixa de concreto reta, agora 3 cm acima da malha do terreno (pela
    altura exata, ela afundava entre os vértices no talude dos lotes).
- **Os testes** (os 31 passam): o `AWoodcreekTemAsRuasEOsLotesDoPlano` aceita de 5% a 22% de vazios, e o
  `OsPatamaresDosLotesSaoPlanos` aceita as árvores dos quintais (reclama só das que ficam a menos de 3,5 m da construção, na
  frente, a menos de 2 m da trilha ou num lote sem quintal).

## O jogo do mundo sorteado no Lakewood (12/11/2026; aprovado e feito em 12 e 13/11/2026, esperando o teste)

O pedido: "temos que levar as mesmas coisas do mundo sorteado para esse novo mapa, a comunidade pode ficar perto da
cidade que tem hoje a woodcreek, vamos mudar a casa de spawn hoje para a casa perto do lago, assim a gente nasce perto da
cidade". Antes, no mesmo dia: o Lakewood virou o jogo normal (a build abre nele, sem as cartas dos moradores).

### O que falta no Lakewood (o levantamento de 12/11/2026)

Muita coisa do jogo só liga com o `MundoDaPartida` (o objeto do mundo sorteado, com os dados do gerador), que a cena
Lakewood não tem. Ali elas não dão erro, só não existem:
- **a fauna** (os bichos, os peixes, os castores, os patos, os vaga-lumes): a vigia dela espera o mundo sorteado pronto; lê
  a altura, a água (um lago só, com o meio e o raio), as manchas da floresta (o habitat de cada bicho), as árvores de cada
  bloco (o castor e o esquilo), as lixeiras (o guaxinim) e a borda do mapa pelo meio;
- **os painéis de sucata** (a chapa, a porta e a placa): o lugar sai das ruas das cidades e dos objetos da rua do
  gerador, e as listas da rede (os desmontados) ficam no `MundoDaPartida`;
- **os sobreviventes** (os grupos de busca nas cidades), **a base militar** (os militares), **os acampamentos** (os
  comerciantes) e **os carros quebrados** (o pneu trocado): leem as cidades do plano, as bases, os acampamentos (com o chão
  aplainado pelo gerador) e os carros parados das estradas;
- **as peças dos carros parados** (o capô, as rodas, o alarme): os carros da beira da estrada do gerador, com número; os do
  Lakewood são só malha;
- **o corte das árvores** (o machado): a lista das cortadas e os RPCs ficam no `MundoDaPartida`; os pinheiros do Lakewood
  são de outro jeito (objetos por bloco de 64 m), mas com a mesma chave possível (o bloco e o índice);
- **o mapa M, o minimapa e as marcas**: pintam os dados do gerador (um quadrado centrado); o Lakewood tem o pintor dele
  (`PintorDeLakewood`), que hoje só o Editor usa;
- **o save** (o Continuar), **as mochilas no chão** de quem morre e **a tela de carregamento**;
- o bunker e os moradores (ficam para depois, a pedido: "já que não temos bunker por enquanto").

Dois defeitos achados no caminho: os blocos do chão do Lakewood se chamam "Chão x z" (no sorteado, "Chão"), e por isso os
passos no mato soam como no concreto; e as poças da chuva procuram o chão de y = 300 para baixo, e no Lakewood o chão passa
de 500 m (lá em cima, o raio nasce dentro do morro).

**As comunidades** (`ComunidadeAliada` e `ComunidadeInimiga` do gerador) nunca foram construídas: no mundo sorteado elas
são uma clareira no fim de uma estrada, com o nome nas placas e a cerca no mapa M. O que existe de verdade é o
**acampamento** do Pedro (o Docs/13): o lugar aplainado na beira da rodovia, com as barracas, os comerciantes, a zona
segura dos zumbis e o comércio.

### O jeito

Uma base comum antes de tudo: o Lakewood ganha o objeto de rede dele (como o "Mundo" do sorteado: o pronto, a semente da
partida, as listas da rede das árvores cortadas, dos painéis e das marcas, o fogo amigo) e cada sistema passa a ler o
mundo por uma interface só (a altura, a água, as manchas, as cidades, as estradas, a borda, as árvores de cada bloco),
como os zumbis já fazem (o `IChaoDosZumbis`), com o mundo sorteado e o Lakewood cumprindo a mesma. Assim cada sistema
serve nas duas cenas, sem cópia.

### As decisões (respondidas: "pode fazer tudo", 12/11/2026, todas pela recomendação)

1. **A comunidade é o acampamento dos comerciantes do Pedro**, o grande (o da cidade grande: raio 45 m), perto da
   Woodcreek, na beira da SR-27 (a recomendação). Ou é outra coisa (a base dos jogadores, uma vila de sobreviventes com
   missões)?
2. **A semente das coisas sorteadas** (os bichos, os grupos, o que os comerciantes vendem): uma por partida (cada partida
   nova sai diferente; a recomendação) ou a fixa do mapa?
3. **Os sobreviventes**: os grupos de busca na Woodcreek (é a única cidade com casas; a recomendação: 2 grupos, como uma
   cidade pequena) e os militares em Fort Crowley.
4. **A ordem** (abaixo; a recomendação é esta).

### As partes, em ordem

- **Parte 0, a casa da partida perto do lago: feita** (veja "A casa da partida", "A casa perto do lago").
- **Parte 1, a base comum:** o objeto de rede do Lakewood, a interface do mundo (com o sorteado e o Lakewood), o pronto, a
  semente da partida; e os dois defeitos (os passos e as poças).
- **Parte 2, a fauna:** os habitats pela cobertura do Lakewood (a mata, o campo, a rocha, a beira d'água), os vários lagos e
  rios no lugar do lago único, as árvores dos blocos; os vaga-lumes. As lixeiras do guaxinim, nos lotes da Woodcreek.
- **Parte 3, o corte das árvores:** o machado nos pinheiros do Lakewood, a lista das cortadas na rede, as toras; o castor
  roendo.
- **Parte 4, os carros na estrada e as peças:** os carros abandonados na beira das estradas do Lakewood (com número, o capô,
  as rodas e o alarme), e os painéis de sucata nas ruas da Woodcreek e nas estradas.
- **Parte 5, os sobreviventes:** os grupos de busca na Woodcreek, os militares em Fort Crowley e os carros quebrados.
- **Parte 6, a comunidade:** o acampamento perto da Woodcreek (o lugar no mapa, o chão aplainado, as barracas, os
  comerciantes, a zona segura).
- **Parte 7, o mapa M, o minimapa e as marcas** (o pintor do Lakewood no jogo).
- **Parte 8, o save do Lakewood** (o Continuar), as mochilas no chão e a tela de carregamento.
- Depois: o bunker e os moradores.

### O que foi feito (12 e 13/11/2026, esperando o teste com dois jogadores)

Tudo conferido no Play pelo MCP, com um jogador (a partida local), e os 31 testes do Lakewood passam (o dos lotes ganhou a
exceção do acampamento, que fica fora da cidade de propósito); o mundo sorteado também foi conferido (o pequeno 3: a fauna,
os objetos da beira das estradas, os painéis, o mapa, as cidades, a base e os acampamentos).

- **A base comum (a parte 1):** o objeto de rede "Mundo (Lakewood)" na cena (o `MundoDeLakewood`: o pronto e a semente da
  partida, nova a cada partida, a do save quando ela vem dele, e as listas da rede), e o mundo do jogo (`IMundoDoJogo`), que
  o mundo sorteado e o Lakewood cumprem: a altura, a água, a borda, as estradas, as cidades (no Lakewood, as que têm lotes,
  hoje a Woodcreek), as bases militares, os acampamentos e a semente. Os dois defeitos: os passos no mato do Lakewood soam
  como grama, e as poças da chuva acham o chão dele.
- **A fauna (a parte 2):** os bichos nascem no Lakewood (vistos: os veados, a águia, os coelhos, os guaxinins e os salmões
  na água). As manchas saem do mapa (os quintais e os baldios da Woodcreek, a beira da água a até 35 m, a mata de coníferas
  com umas manchas de mata mista e a rebrota nas clareiras, a serra acima de 480 m, o campo e a lavoura); a água é a de cada
  lugar (o reservatório, os rios e o canal, cada um na sua altura); o guaxinim revira a lata de lixo na frente das casas
  (ela não é desenhada); o castor rói os pinheiros da beira; o F1 "Ir para o lago" acha a margem mais perto.
- **O corte das árvores (a parte 3):** o machado nos pinheiros do Lakewood, como no mundo sorteado (os 4 lados, os entalhes,
  a queda para o lado do primeiro corte, as toras, o XP e o barulho); a lista das cortadas vai pela rede e no save; a
  cortada vira um toco (a casca escura e o corte claro em cima) e some dos cartazes de longe.
- **Os carros e a sucata (a parte 4):** 135 carros abandonados (nas rodovias, de 250 a 800 m entre eles: encostados no
  acostamento, parados na pista ou batidos no barranco; nas ruas da Woodcreek, junto da calçada), com o capô, as rodas, o
  macaco e o alarme dos do mundo sorteado (o que sobrou em cada um sai da semente da partida); e 72 painéis de metal (nas
  ruas da Woodcreek, ao lado dos carros, nos lotes vazios e na frente das lojas e do posto), que se desmontam com o E,
  pela rede e no save.
- **Os sobreviventes (a parte 5):** os grupos de busca na Woodcreek (2, um em cada metade da vila), os 6 a 8 militares em
  Fort Crowley (a base inteira, com o portão para a estrada da serra e o pátio sem os pinheiros) e 3 carros quebrados nas
  rodovias, com os dois trocando o pneu.
- **A comunidade (a parte 6):** o acampamento grande dos comerciantes na beira da North Shore Highway (a SR-27), a norte da
  Woodcreek, em (9.584; 7.644): um lote de 64 × 64 m (o tipo novo `Acampamento`), plano pelo patamar, com a cerca de
  contêineres, a casa, as bancas, as fogueiras, os guardas, os comerciantes e a zona segura. A ferramenta: Lakewood > Etapa 6:
  o acampamento dos comerciantes (perto da Woodcreek).
- **O mapa (a parte 7):** o M mostra o mapa do Lakewood (a imagem do pintor dele, a mesma do `Lakewood_Mapa.png`, sem a
  legenda, 2.048 × 1.707), com os nomes; o minimapa, os pings, as marcas e as anotações (pela rede do Lakewood).
- **O save (a parte 8):** o jogo novo do Lakewood salva sozinho (a cada 5 minutos e ao sair) e volta pelo Continuar do menu
  (que no Lakewood mostra só os jogos dele, e no mundo sorteado só os dele): a semente da partida, a hora e o clima, os itens
  no chão (as armas das mesas), as construções, os veículos da casa da partida (pela vaga; eles e as armas não são postos de
  novo), os carros mexidos, as árvores cortadas, os painéis, a fauna, a mobília, o saque, as portas, os grupos, o
  acampamento, os carros quebrados, a base, as marcas, as mochilas no chão e onde cada um estava (ele espera parado o chão de
  lá). Conferido salvando, saindo e voltando: tudo voltou igual, sem duplicar.

**O que testar:** os bichos pela mata e pela beira do lago (de dia e de noite), o machado num pinheiro (e o toco), um carro
abandonado (o capô, as rodas, o alarme), um painel de metal, um grupo na Woodcreek (aparece de 1,5 a 4 min depois de entrar
nela), o acampamento (o comércio), Fort Crowley, um carro quebrado, o M e o minimapa, e o Continuar (salvar, sair e voltar),
com dois jogadores.

**Ficou de fora:** a tela de carregamento (o Lakewood monta no menu, antes de hospedar); o F1 "Painel na frente" (é do mundo
sorteado); o bunker e os moradores; e a imagem `Lakewood_Mapa.png` ainda não mostra o acampamento (a próxima Imagem do mapa
já mostra). As chaves das árvores (o bloco e a ordem) mudaram nos blocos da base e do acampamento: não há saves de antes.

## As ferramentas do Editor (menu Zombie City Cleaner > Lakewood)

1. **Refazer o esboço do mapa**: grava o esboço em código no asset (sem mexer no lago, nas ilhas e na cobertura).
2. **Ler a imagem de referência**: a cobertura, as montanhas, os lagos e as ilhas, do PNG da pasta de arte.
3. **Montar a cena**: refaz `Scenes/Lakewood.unity` (a luz, o volume, o clima, a câmera do menu, a rede e o jogador de hoje,
   os itens no chão, o carro ao lado da partida e o "Lakewood" com o `LakewoodNaCena`). A partida fica no chão seco mais
   perto do ponto do mapa (hoje (8.600; 3.700), a margem da Lakewood, setor C2, olhando o reservatório). A cena entra nas
   Build Settings no lugar da antiga.
- **Desenho do mapa** (`JanelaDeLakewood`): o mapa pintado com os nomes e a grade dos setores, a referência por baixo
  (ligável, com a transparência), e as camadas em linhas (as zonas de relevo, os picos, as passagens, os lagos, as ilhas,
  os rios, a represa, as estradas, as pontes, as cidades, os lugares, os túneis e a partida): clicar escolhe, arrastar move
  o ponto, Shift + clicar põe um ponto, Ctrl + clicar tira, Alt + arrastar move o elemento inteiro; o botão direito arrasta
  o mapa e a roda aproxima. Do lado, os campos do escolhido e os parâmetros gerais do relevo. Tudo vai para o Desfazer e o
  mapa se repinta em outra thread.
- **Imagem do mapa**: `Imgs/Lakewood/Lakewood_Mapa.png` (3.600 px nos 12 km, com a legenda) e `_Metade.png`.
- **Prévia do relevo na cena**: o relevo de longe dos 16 setores a 25 m, a água e a barragem, em objetos que não são salvos,
  na cena aberta (o mesmo menu tira).
- **Fotos do relevo**: `Imgs/Lakewood/Lakewood_Relevo_*.png` (a barragem, a margem da Lakewood, o norte, o oeste, as
  aéreas do sul e do leste e a de cima).
- **Conferir o relevo (relatório)**: os números dos critérios de qualidade do plano no console.
- **Fotos de perto**: `Imgs/Lakewood/Lakewood_Perto_*.png` (o terreno de 2 m até 180 m e de 8 m até 520 m, as plantas até
  300 m, a grama até 90 m, a água e a prévia do relevo de longe por trás), nas 7 vistas da beira e nas 4 dos rios (da
  margem, rio abaixo; `_Rio_*.png`), com a luz ambiente do dia do jogo (a guardada na cena é escura) e a refração da
  água ligada; a câmera fica em terra seca a uns 10 m da água, ou em cima dela.
- **Etapa 4: a usina no mapa e na cena** (10/11/2026): grava no asset o colo da cabeceira oeste, a crista de 12 m e o fim
  do túnel da barragem, e põe na cena os materiais e os modelos da usina (os carros e a barreira do Cidade_Rua.fbx).
- **Etapa 5: traçar as estradas** (10/11/2026): traça o plano das ligações pelo relevo (uns 11 s) e grava os pontos no
  asset, no lugar das estradas de antes (as da usina são do código); depois, a Imagem do mapa mostra a rede.
- **Etapa 5: a casa da partida** (10/11/2026): acha o lugar na South Shore, grava no asset (a casa e a partida) e acerta
  a cena aberta (os pontos de nascimento, a câmera do menu, os veículos e a mesa); o 3. Montar a cena também põe.
- **Etapa 6: a Woodcreek (as ruas e os lotes)** (11/11/2026): a receita da vila (as ruas novas, os especiais e os lotes
  enchidos) gravada no asset no lugar das ruas e dos lotes da Woodcreek, e o plano dela; **Etapa 6: a imagem da
  Woodcreek** refaz só o plano, do asset (veja "A Woodcreek", a parte 1).
- **Etapa 6: as cidades na cena** (11/11/2026): acerta a cena aberta (os materiais das obras, das marcas, da hera e das
  pichações no `LakewoodNaCena`, e as "Obras das cidades" com a mobília, a mobília nova, as portas, o hospital e o NavMesh, e o
  "Zumbis") e salva; o 3. Montar a cena também põe.
- **Build para testar (só o Lakewood)** (10/11/2026, a pedido: "uma build para eu testar com meus amigos"): só a cena
  Lakewood, sem Development, em `Builds/Lakewood/` do projeto Unity (17,6 min a primeira, 38 s com o cache, 823 MB); o zip
  para os amigos é o `Builds/ZombieCityCleaner_Lakewood_10-11.zip` (342 MB). Antes, ela grava o material do relevo de longe
  (`Settings/Lakewood/ChaoDeLonge.mat`) na cena: feito na hora, ele não ia para a build.
- **Testes > Lakewood**: os 31 testes do Editor (`Tests/Editor/LakewoodTestes`; os 6 da usina, os 5 das estradas, os 2
  do terreno por cima da pista e o da casa da partida desde 10/11/2026, os 3 das cidades desde 11/11/2026, o dos patamares
  dos lotes e o dos especiais desde 12/11/2026).

## No jogo (a cena Lakewood)

- O `LakewoodNaCena` monta no `Awake` (uns 3 s): o relevo, o chão, os 16 setores como objetos, a água, a hidrelétrica
  (as 87 peças da usina, com a colisão, os carros e as barreiras), as estradas (as 19 da rede e as três da usina, com os
  pátios dos cruzamentos; o desenho só perto, a colisão sempre), a parede invisível na borda do mapa, o relevo de longe (desligado) e os blocos em volta da
  partida, com a colisão. Depois, a cada 0,2 s: os blocos de 64 m em volta da câmera (2 m entre os pontos até 120 m, 4 m até
  220, 8 m até 360 e 16 m além; até 200 m com a neblina e 480 m sem), em até 6 tarefas, com a colisão a 110 m do jogador e
  dos carros; os que ninguém vê há 3 s saem; quem passou pelo chão volta para cima. As plantas de cada bloco em outra
  tarefa, pela cobertura da imagem. **Desde 09/11/2026, a floresta nova** (o `Plano_Floresta.md`, "A floresta nova
  no Lakewood"): só os pinheiros leves, na densidade da cena de floresta e com as clareiras. Perto, eles são
  objetos (até uns 300 m na Alta); depois, só os cartazes, em instâncias, até 480 m. A grama é a densa, e a sombra
  do sol segue a tabela da floresta. A mata de antes saiu: as folhosas, o chão da mata, as pedras e a taboa. A cena
  guardava as plantas até 320 m sem a neblina, e agora são os 480 m do plano. **Os ajustes de 10/11/2026** (o
  `Plano_Floresta.md`, "Os ajustes depois do teste no Lakewood"):
  - o chão é a grama em quase tudo (a terra e o cascalho só na praia e nos barrancos dos rios);
  - a grama não treme mais quando o vento vira;
  - os tufos de longe vão até 90 m na Alta e na Ultra, na cor do chão;
  - a água ficou sem o risco do normal map e com a cor mais viva.
- **O F1** tem o painel LAKEWOOD: onde você está (o setor, a altura, a água, a velocidade), os blocos e as plantas; a neblina
  (N); voar (F2); a régua; ir para (as coordenadas, a partida, o meio, a barragem, as cidades, os lugares e as ilhas); e
  trazer o carro.
- A água (`Agua.cs`) e a volta de quem cai (`QuedaDoJogador`) leem o lago e os rios daqui.
- **As cidades** (a Etapa 6; só a Woodcreek por enquanto): as ruas, as calçadas, as placas e os postes, e as obras dos lotes
  (as caixas de longe e as de verdade perto de alguém), o hospital, a loja de armas e os especiais; os zumbis pelo
  povoamento, no NavMesh em volta dos jogadores (veja "A Woodcreek", as partes 2 a 6).

## Decisões

### Tomadas por mim em 08/11/2026 (o usuário fora; todas mudam no asset)

1. **O tamanho é o do prompt**, 12 × 10 km, e a ilustração (uns 15 × 12 de proporção) foi esticada 8% para caber: ela não
   é cartográfica.
2. **O reservatório é o da ilustração**, lido pixel a pixel: 16,4% do mapa com as ilhas (o prompt pedia 20 a 25%). Dá para
   alargar as margens na janela do desenho, se o usuário quiser.
3. **A barragem** tem 881 m de crista (a ilustração desenha uma de 2,4 km, exagerada): a ponta do lago foi afunilada até
   ela, entre duas encostas rochosas feitas pelas zonas de relevo.
4. **Os nomes do mundo em inglês** (Lakewood Reservoir, Lakewood Dam, Sullivan Farms, Pine Ridge State Park, Crowley
   Mountains, Central Island...), como as placas e as pichações do jogo; o plano e o código em português.
5. **A lavoura e o campo da ilustração** viram a cobertura (as fazendas no oeste e no noroeste), e as cidades são áreas
   reservadas aplainadas; o detalhe delas é da Etapa 6.
6. **A borda do mapa** é uma parede invisível por enquanto; o que fecha o condado (as montanhas, uma cerca, os bloqueios)
   fica para depois.
7. **A partida** fica na margem da Lakewood, olhando o lago (o lugar muda no asset).
8. **A neblina** continua a de hoje (a decisão de 29/10/2026); sem ela, o relevo de longe aparece e a câmera vê o condado
   inteiro.

### Tomadas por mim em 10/11/2026 (a Etapa 4, pela recomendação)

1. **Só a estrutura de fora** (o prompt: "estrutura simplificada"). Os interiores, o saque e os zumbis da usina ficam para
   uma parte própria, como as do hospital.
2. **O vertedouro no meio**, sobre o rio de antes, e **a casa de força no pé**, a leste dele. O pátio fica na margem leste;
   a oeste do rio, a mata.
3. **As comportas fechadas**: sem a operação, nada verte. A calha fica seca e a água do canal de fuga, parada. A água
   vertendo por uma comporta emperrada pode vir depois.
4. **A crista é uma estrada de verdade**, de duas pistas, e as duas pontas saem para a rede: a leste desce para o pátio e
   o vale; a oeste sai pelo colo novo do relevo.
5. **Duas linhas de transmissão** (leste e sul), com 26 torres.
6. **O posto de controle da quarentena** na cabeceira leste: a barragem como passagem vigiada no começo do surto (a
   história).
7. **Os nomes e as placas em inglês**; a barragem é de 1962.

### Ainda abertas (para o usuário)

- Aumentar a água até os 20% do plano (alargando as margens ou os braços)?
- Onde fica a base militar (hoje Fort Crowley, em (8.900; 9.100), no vale entre o Pine River e as Crowley; a ilustração não
  a mostra)?
- O nado (decidido para o Blackwater; vale aqui?). (A velocidade do carro foi respondida: a perua a 110 km/h, na Etapa 5.)
- O save do Lakewood (12/11/2026): como começar e continuar uma partida nele (um mundo salvo só, ou a lista do menu como no
  mundo sorteado)? Sem isso, o saque pego, as portas e o resto voltam a cada partida.

### Tomadas por mim em 10/11/2026 (a Etapa 5, pela recomendação)

1. **O anel da rodovia em volta do lago**, como a da ilustração: pelo meio das quatro cidades, pela crista da barragem e
   pela ponte do norte. As secundárias vão até os lugares, e os ramais saem do anel.
2. **Três saídas do mapa** (o leste, o sul e o oeste), terminando na borda por enquanto (a barreira da quarentena vem
   depois).
3. **As rampas:** 6% na rodovia, 9% nas secundárias e 10% nas de serra; 7% só na saída da garganta da barragem.
4. **A ponte do norte** na ponta da baía estreita, onde a água tem uns 100 m (uma reta).
5. **A perua** foi para 110 km/h (o carro do Lakewood) e, depois, **o cupê para 140** (a pedido: "sim, mude para 140"),
   para continuar o carro rápido.

**Os veículos arrumados no F3** (10/11/2026, a pedido: "coloque uma opção no f3 para conseguir dar spawn em carros já
arrumados 100% e com gasolina e chave"): F3 > Veículos cria a perua, o cupê, a moto cross ou a Fat Boy 6 m à frente (de
lado, no chão; com outro carro ali, mais à frente ou do lado), com a vela, a bateria, o radiador, os pneus e o estepe a
100%, o macaco e o tanque cheio. A chave dele vai para a mochila (ou para o chão, com "No chão"). Vale no Lakewood e no
mundo sorteado. Esses veículos não entram no save (o save guarda só a perua e o cupê da partida).

## Partes, em ordem (as etapas do prompt)

**As Etapas 1 a 5 foram aprovadas em 10/11/2026** ("as etapas de 1 até 5 estão aprovadas, tem pequenos ajustes que depois vamos fazer"): os ajustes pequenos
ficam para quando ele disser quais. A próxima é a Etapa 6.

- **Etapa 1 (a estrutura do mundo): feita.** As dimensões e as coordenadas, os 16 setores, o sistema de terreno (o relevo
  em grade, os blocos de 64 m em níveis de detalhe, a colisão perto), a cena separada e navegável.
- **Etapa 2 (o relevo): feita e aprovada.** As montanhas ao norte e a leste, as colinas, a planície agrícola do
  oeste, os vales dos rios, as transições suaves, as áreas urbanas preparadas (aplainadas). As fotos em
  `Imgs/Lakewood/Lakewood_Relevo_*.png` e o mapa em `Lakewood_Mapa.png`.
- **Etapa 3 (o reservatório): feita em 08/11/2026, aprovada em 10/11/2026.** A bacia com o vale afogado, o nível, as
  margens (a linha d'água tremida, as praias de terra e cascalho, as costas rochosas a leste, a taboa e as pedras), as
  enseadas e as penínsulas (a baía do norte voltou), as cinco ilhas com o raso em volta, os sete rios entrando sem brigar
  com a água do lago e as fotos de perto. Ficam os ilhotes a mais e o nado. Os ajustes depois do teste (09/11/2026:
  os rios com meandros e o canal cavado, a água nova, a mata cheia e o piloto das árvores com folhas) estão na seção
  deles, esperando o teste e a escolha das folhas.
- **Etapa 4 (a hidrelétrica): feita e aprovada em 10/11/2026.** A barragem de verdade com a estrada na
  crista, o vertedouro, a casa de força, o canal de fuga, o pátio com a subestação e os prédios, as linhas de
  transmissão, as estradas de acesso e o abandono (veja "A hidrelétrica").
- **Etapa 5 (as estradas): feita e aprovada em 10/11/2026.** O anel da rodovia com a ponte do norte, as
  saídas, as secundárias e os ramais, traçados pelo relevo e ligados nos nós, com os cruzamentos planos, as pontes e o
  guard-rail; a perua a 110 km/h (veja "As estradas"). Sem as estradas de terra (a pedido) e sem as placas.
- **Etapa 6 (as cidades e os pontos de interesse): em planejamento** (10/11/2026: o usuário pediu o mapa para planejar
  por onde começar; em 11/11/2026, "vamos começar pela woodcreek", com o hospital e a loja de armas novos nela: o plano, o
  esboço, as decisões e as partes em "A Woodcreek"; aprovado em 11/11/2026, sem o túnel por enquanto; as 6 partes feitas em 11 e 12/11/2026, esperando o teste: o desenho, o chão e as ruas, as construções do kit, o
  hospital e a loja de armas, as portas, o saque, o NavMesh e os zumbis, e os especiais; sem o save; o plano em
  `Imgs/Lakewood/Lakewood_Woodcreek.png`). As áreas e os pontos estão reservados; falta o detalhe. O prompt pede: marcar os
  limites urbanos, preparar os quarteirões, reservar as áreas dos edifícios, posicionar as instalações importantes e
  definir a base militar, o aeroporto e as zonas rurais ("nesta primeira fase, não é necessário construir todos os
  interiores ou edifícios definitivos"). O mapa com a casa da partida marcada e o nome do aeroporto (que caía fora da
  imagem: agora 330 m abaixo do ícone, no esboço e no asset) está em `Imgs/Lakewood/Lakewood_Mapa_Etapa6.png` (e a
  metade, `_Metade.png`), desenhado por cima do `Lakewood_Mapa.png` com o Editor fechado; a próxima Imagem do mapa já
  sai com o nome do aeroporto no lugar.
- **Etapa 7 (os biomas e a ambientação):** as plantas já vêm pela cobertura (a mata, o campo, a rocha, a beira d'água),
  com a mata cheia desde 09/11/2026; faltam as lavouras de verdade, as rochas, as clareiras, o abandono e as árvores
  com folhas (depois da escolha do piloto).
- **Etapa 8 (o subterrâneo):** os cinco túneis estão reservados como linhas no mapa (o da barragem termina no prédio da
  galeria de serviço da usina); falta o resto.
- **Etapa 9 (a otimização e os testes):** os 31 testes do Editor passam; falta medir com dois jogadores.

## Os critérios de qualidade (o relatório de 08/11/2026)

- O formato do reservatório corresponde à referência: sim (lido dela).
- A barragem na saída correta: sim, a ponta sudoeste, com o rio de saída até a borda sul.
- O rio de entrada ao norte: o Pine River; o de saída ao sul: o Lakewood River.
- As montanhas no norte e no leste (256 e 265 m de média contra 181 no oeste; o máximo 644 m no nordeste): sim.
- O oeste com a planície e as lavouras: sim (90% abaixo de 203 m).
- A cidade principal a sudeste, as pequenas nos lugares planejados: sim.
- A Ilha Central com espaço para a entrada do laboratório: 1,96 km², a 193 m no meio.
- As cidades em terra firme e suaves (de 1 a 7,4% de inclinação média; até 5% da área na água, a margem da Lakewood).
- O relevo contínuo entre os setores (uma função só; o teste confere as divisas).
- O mapa funciona sem carregar tudo: os blocos em volta da câmera e o relevo de longe por setor.
- Os sistemas do jogo continuam: a cena `Mundo` não foi tocada; a água e a queda leem o Lakewood.

## Riscos

- **A água em 16%:** se o usuário quiser os 20%, as margens crescem na janela, mas as cidades da margem (a Lakewood, a
  Riverside) e os lugares precisam ser conferidos de novo (o relatório e os testes dizem).
- **A ilustração esticada:** as proporções das cidades e das ilhas estão 8% mais altas que largas em relação ao desenho.
- **O pontilhado da cobertura:** a leitura é por cor, e as sombras entre as árvores e os topos claros deixam manchas
  pequenas de rocha e de lavoura fora do lugar; uma passada de maioria, a mistura entre as células e o ruído nas beiras
  deixam tudo orgânico, mas o mapa ainda tem umas manchas de campo e de rocha espalhadas pela mata (a ilustração também
  tem). A Etapa 7 pinta os biomas por cima.
- **A memória do Editor:** o relevo e a leitura são leves (segundos), mas a janela do desenho repinta a cada mudança (uns
  2 s em outra thread).
