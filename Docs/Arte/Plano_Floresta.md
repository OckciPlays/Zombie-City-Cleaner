# Plano da floresta, do chão e do longe (09/11/2026)

O pedido do usuário, depois do teste do Lakewood com a água nova e a mata cheia: "temos que rever toda essa parte de
textura de terreno, árvores, sistema de ver o longe". Cinco pontos, as referências (os lagos do Alasca e do Oregon, o
Mount Hood, o Wallowa Lake, a represa de Monticello, uma pintura de um lago com o cais e a barragem) e o prompt dos
modelos de floresta (a folha "Conjunto de floresta, modelos low-poly": os pinheiros alto, médio e pequeno, o carvalho de
verão e de outono, a bétula, o ácer, a árvore morta, o tronco quebrado, os arbustos, as samambaias, as rochas, os troncos
caídos, os tocos e os detalhes, com os LODs e os triângulos de cada um).

## Os cinco pontos e o que foi decidido

1. **A textura do chão de perto é sempre o marrom morto.** As texturas do chão são refeitas: a grama vira um verde de
   campo (com uns tufos secos, o jogo continua sombrio pela neblina e pela luz, não pelo chão morto), as folhas mortas
   viram o chão da mata (a terra escura, as agulhas, as folhas e o musgo em manchas), e os pesos mudam (a mata quase toda
   no chão da mata, o campo quase todo na grama). A neve entra nos picos (acima de 560 m, no `Chao.shader`).
2. **A textura de perto não bate com a de longe.** O relevo de longe passa a usar **as mesmas texturas** do chão de perto
   (o `ChaoDeLonge.shader`: o `Chao.shader` com os pesos no vértice, mais a copa da mata, que sobe longe da câmera e
   ganha a cor salpicada das copas, e a neve), e fica sempre desenhado (não só sem a neblina): quando a neblina afinar,
   o longe já está lá, sem emenda de cor. O céu ganha um shader (`Ceu.shader`): o degradê do dia e da noite, o sol (o
   disco e o clarão, na direção da luz), a lua (o disco com a fase, à noite), as estrelas e as nuvens, com a névoa da
   neblina no horizonte. A neblina continua a do jogo; o usuário pode afiná-la depois nas Configurações.
3. **As árvores e a grama.** O gerador de floresta no Editor (o prompt): os modelos feitos por código em C#, com as
   sementes, as variações, os LODs e os prefabs, numa cena de teste, por fases (os pinheiros primeiro); e a grama densa
   (os tufos de lâminas em volta do jogador, dobrando com o vento e com quem passa).
4. **Em volta do lago não tem floresta.** A classe "água" da imagem de referência, lida nas beiras do lago, valia como
   cobertura em terra firme e não dava árvore nem grama: agora ela conta como mata. A mata vai até a praia.
5. **Atravesso a terra e caio na água.** As poças secas da beira (o ruído do detalhe cavava o chão abaixo da água fora
   das células da água) saem numa faixa de 60 m do lago; a água do lago só vale onde o relevo está abaixo dela (a célula
   de 12 m dizia "água" na praia seca); e os blocos de 4 m também ganham a colisão enquanto o de 2 m não chega.

**O tamanho do mapa** (a dúvida: "diminuir um pouco o tamanho do mapa ajuda a ver melhor as coisas ao longe?"): não é o
tamanho, é o jeito de desenhar o longe. Um mapa de 8 km visto a 2 km fica tão borrado quanto um de 12 km; o que faz ver
longe é o relevo de longe com as mesmas texturas, a copa da mata no lugar das árvores, o céu com o sol e a lua e a
neblina mais rala. Os 12 × 10 km são as proporções da ilustração e o lugar de cada coisa da história; o custo deles (os
16 setores, os blocos em volta de cada jogador) não muda com o alcance da vista. Se um dia o mapa diminuir, que seja pela
densidade do jogo (o tempo a pé entre as cidades), não pela vista.

## O que está feito (09/11/2026, esperando o teste do usuário)

- Os pontos 1, 2, 4 e 5 (as texturas e a neve, o longe com as mesmas texturas e a copa, o céu com o sol, a lua, as
  estrelas e as nuvens, a mata até o lago, a beira sem as poças secas e a colisão nos blocos de 4 m) e a fase 1 do
  gerador (os pinheiros). Os 12 testes do Lakewood passam.
- **Como ver:** a cena `Lakewood` (o N tira a neblina: o condado inteiro com as texturas de perto, a copa da mata ao
  longe, o sol e as nuvens; de noite, a lua e as estrelas); a cena de teste dos modelos (Zombie City Cleaner > Exemplos
  > Floresta (os modelos): a fileira dos 15 pinheiros, a mata de teste e a área dos LODs); e a janela do gerador
  (Zombie City Cleaner > Floresta > Gerador: o tamanho, a variação, a semente e os parâmetros, com a prévia dos 3 LODs
  na cena, o "Gerar todos" e o "Pôr as malhas no Lakewood").
- **Os números dos pinheiros:** no LOD0, uns 400 triângulos em média (os altos ~560, os médios ~400, os pequenos
  ~250; o 1 com a metade, o 2 com uns 60); 15 variações; os prefabs com o LODGroup e a cápsula do tronco. No Play, na
  mata da costa leste, uns 5.000 plantas na vista com a neblina e 8.200 sem, 6 ms por quadro no Editor.
- As fotos: `Imgs/Renders/Floresta_Pinheiros_Fileira.png`, `_Mata.png` e `_LOD.png` (a cena de teste);
  `Imgs/Lakewood/Lakewood_Play_Mata_Neblina.png` e `_SemNeblina.png` (a mata da costa leste no Play, com e sem a
  neblina), `Lakewood_Play_Longe_Ceu.png` (o lago e o longe de dia) e `Lakewood_Play_Noite.png` (a lua e as estrelas).
- Fica para o próximo passo, depois da validação dos pinheiros: a grama densa (a fase 4 antecipada, a pedido: "grama
  volumétrica mesmo ao redor"), as copas largas (a fase 2) e as mortas (a fase 3).

## A cena de floresta e os pinheiros realistas (09/11/2026, depois do "ainda não tá bom")

O usuário viu os pinheiros low-poly da fase 1 no Lakewood e pediu outra coisa: "crie uma cena de floresta, pequena,
vamos testar a floresta lá": uma floresta bem densa, com os pinheiros, um rio, a textura do chão e a grama densa, onde dê
para andar; as outras fases da floresta vão sendo feitas nessa cena; o LOD "carregando muito perto" tem que melhorar; e
os pinheiros deixam de ser ultra low-poly: "faça um mais realista, pode usar o Blender", pela foto de referência (um
spruce: o tronco reto e nu embaixo, os galhos em verticilos caindo, as agulhas densas, a ponta fina), com texturas.
Por partes: nesta, só os pinheiros (e o terreno, o riacho e o chão, que a cena precisa para se andar nela).

**O que foi feito (esperando o teste do usuário):**

- **O atlas dos galhos no Blender** (`Modelo 3D/Floresta/galhos_pinheiro.py`, num Blender sem janela, uns 80 s): cada
  galho é modelado em 3D (o eixo, os raminhos de primeira e de segunda ordem e umas 8 mil agulhas em espiral, apontando
  para a frente, com o verde variando por agulha e as pontas novas mais claras) e fotografado de cima pelo Cycles, com o
  alfa (o fundo transparente dilatado com a cor dos vizinhos, para o filtro não puxar o preto); a normal sai de uma
  segunda foto com um material que emite a normal. `Texturas/Pinheiro_Galhos.png` (2048 px: 4 × 2 cartas de 512 × 1024,
  as 6 primeiras galhos de lado, as 2 últimas a ponta da árvore vista de lado) e `_Normal.png`, copiados para
  `Art/Floresta/Texturas` do projeto Unity; o `Galhos_Pinheiro.blend` fica para olhar.
- **A casca** (`T_Casca_Pinheiro.png` e `_Normal.png`, feitas pelo Editor): as escamas pequenas (células de Worley um
  pouco mais altas que largas, cada uma no seu tom, com as fendas escuras), os sulcos verticais finos, uns pontos de
  líquen; 0,6 m por repetição.
- **O pinheiro realista** (`PinheiroRealista`, no assembly do mundo): o tronco em anéis com a casca (a base alargada, um
  pouco torto, os tocos de galho morto na parte nua); a copa em verticilos a cada 0,38–0,48 m (o alto), 6 a 8 galhos cada,
  cada galho uma carta em três linhas dobrada num Λ, caindo da base e subindo na ponta, com um rolo de ±30°, mais uma
  segunda carta cruzada (uns 60° em volta do eixo do galho) e, nos galhos compridos, uma ou duas cartas penduradas (em
  pé, com a ponta para baixo: os raminhos que caem do spruce, o volume visto de lado); a ponta em cartas em pé cruzadas.
  A normal é "esférica" (sai do eixo da árvore: a copa sombreia como um volume) e o normal map da foto entra por cima.
  Os três tamanhos (alto 14–20 m, médio 8–13, pequeno 3–6), 5 variações cada pela semente (`Semente` = 2000 + tamanho ×
  100 + variação). Uns 1.800 triângulos de perto, 276 no LOD 1 (metade dos verticilos, sem a segunda carta) e 4 no LOD 2.
- **O LOD 2 é um cartaz**: duas cartas em pé cruzadas com a foto da árvore inteira, assada pelo Editor
  (`Pinheiro_Cartazes.png`, 4096 × 2048, 16 vagas de 512 × 1024: cada árvore fotografada de lado por uma câmera
  ortográfica, sem o sol e com a luz ambiente branca, sem vento). Os prefabs (`Art/Floresta/Prefabs/PinheiroReal<Alto|
  Medio|Pequeno>_<1..5>`) têm o LODGroup com o **cross-fade** (o pontilhado entre os LODs, sem pular), as trocas a 14%,
  4,5% e 0,4% da tela (o alto de 17 m: o LOD 0 até uns 80 m, o 1 até uns 250 m, o cartaz até uns 2,5 km), a sombra nos
  dois primeiros e a cápsula do tronco.
- **O shader** (`ArvoreRealista.shader`): a textura com o alfa (as cartas recortam, dos dois lados), o normal map, o
  vento do Vegetacao.shader (a cor do vértice + `_ZccVento`), a luz atravessando a copa com o sol atrás
  (`_Translucencia`), a variação de tom por árvore (pelo pé dela: umas mais amareladas, outras azuladas, mais claras ou
  escuras), o cross-fade dos LODs e as instâncias do GPU Resident Drawer (`DOTS_INSTANCING_ON`: os milhares de
  renderers iguais vão em lotes). Os materiais em `Art/Floresta/Materiais` (o tronco, os galhos e o cartaz).
- **A cena andável** (`Scenes/Exemplos/Floresta_Andar.unity`, menu Zombie City Cleaner > Exemplos > Floresta (andar);
  a `FlorestaDeTeste`, que gera e não salva): 320 × 320 m de colinas (o material do chão do jogo, com os pesos das
  texturas: o chão da mata, a grama nas clareiras, a terra e o cascalho na beira do riacho), o riacho (o canal cavado no
  terreno e a água do jogo, com o reflexo), uns 6.200 pinheiros (7 por 100 m² na mata fechada, com as clareiras e os
  pequenos nas beiras delas) e o jogador das cenas de exemplo (WASD, Shift corre, o mouse olha, T a terceira pessoa, V
  voa). R sorteia outra floresta, F liga e desliga a neblina. A sombra do sol vai até 160 m (4 cascatas) só nessa cena.
  No Editor, uns 65 quadros por segundo com o Game view de 1920 × 890.
- **Como refazer:** Zombie City Cleaner > Floresta > Gerar os pinheiros realistas (as texturas da casca, os materiais,
  as malhas, os cartazes e os prefabs; uns 12 s) e Exemplos > Floresta (andar). Mudou o atlas dos galhos: rode o
  `galhos_pinheiro.py` no Blender e gere de novo.
- As fotos: `Imgs/Renders/Floresta_Andar_1.png` (de onde o jogador nasce, o riacho e a clareira), `_2.png` (a beira do
  riacho), `_3.png` (do alto, sem a neblina: a mata inteira e os cartazes) e `_4.png` (dentro da mata).

**O que fica para as próximas partes, nessa cena:** a grama densa (volumétrica, em volta do jogador), a vegetação
baixa (os arbustos, as samambaias, o chão de agulhas), as copas largas, as mortas, as rochas e os troncos caídos; e
depois, a floresta nova no Lakewood (o sistema de instâncias de lá com os cartazes).

## A versão leve (09/11/2026, depois do "ficou legal, mas vai deixar o jogo bem pesado")

O usuário gostou do tronco e do volume, mas viu 20 milhões de triângulos no meio do mapa; pediu os galhos mais para
cima (uns ficavam na altura do jogador), sem os galhos virados para baixo, sem o brilho nas agulhas, uma versão
intermediária com o tronco menos redondo, a cena realista guardada e a leve em outra cena, e as técnicas do The Forest
(os LODs com cartazes, o terreno em malha, o batching, o atlas, a oclusão e a densidade falsa).

**De onde vinham os 20 milhões** (medido no Play da cena realista, no meio da mata, na mesma vista):

| o que | triângulos | draw calls |
|---|---|---|
| como estava (o reflexo do riacho, a sombra a 160 m em 4 cascatas) | 25,2 milhões | 46.100 |
| sem o reflexo da água (ele desenhava a mata inteira de novo) | 14,8 milhões | 20.500 |
| e a sombra a 40 m em 2 cascatas (o padrão do jogo) | 7,4 milhões | 11.300 |
| e o LOD 0 mais perto (o lodBias 0,6) | 2,2 milhões | 8.500 |

Ou seja: a floresta era desenhada várias vezes por quadro (o reflexo, cada cascata de sombra, a profundidade e as
normais do SSAO), e os cartazes cruzados tinham 4 triângulos cada. As árvores em si não eram o grosso.

**O pinheiro leve** (`PinheiroRealista.Parametros.leve`; os prefabs `PinheiroLeve<Alto|Medio|Pequeno>_<1..5>`, as
sementes 3000 + ...): o tronco de 6 lados com 9 anéis (5 e 5 no LOD 1) e poucos tocos; a copa começando mais alta (o
alto de 34 a 44% da altura, o médio 30 a 40%, o pequeno 25 a 35%: nada na altura do jogador nos altos e médios), os
verticilos a cada 0,55–0,68 m (o alto) com 5 a 7 galhos, os galhos de −8° a +6° (sem cair) com a ponta subindo pouco, as
cartas de 2 linhas num Λ de 26° com o rolo de ±40°, a segunda carta cruzada só em 40% dos galhos compridos, **sem as
cartas penduradas** (eram os "galhos virados para baixo"), a ponta em 2 cartas. Uns 396 triângulos de perto (o alto 518,
o médio 457, o pequeno 213), 126 no LOD 1 e **2 no LOD 2: um cartaz que vira para a câmera** (o `_Cartaz` do shader: a
carta gira em volta do eixo da árvore para encarar a câmera, como os billboards do The Forest; o atlas
`Pinheiro_Cartazes_Leve.png`). As trocas a 20%, 6% e 0,3% da tela (o alto de 17 m: o LOD 0 até uns 55 m, o 1 até uns
190 m) e só o LOD 0 faz sombra. O material dos galhos leve sem brilho (`_Suavidade` 0; a luz atravessando a 0,15).
A mistura dos tamanhos na cena mudou (menos pequenos: 52% altos, 36% médios, 12% pequenos).

**A cena leve** (`Scenes/Exemplos/Floresta_Leve.unity`, menu Exemplos > Floresta (andar, leve); a realista continua em
`Floresta_Andar.unity`): a mesma floresta com os prefabs leves, a sombra do sol a 90 m em 3 cascatas e o reflexo do
riacho desenhando só até 120 m (`ReflexoDaAgua.Alcance`). No mesmo ponto de medida: **1,65 milhão de triângulos e
18 mil draw calls** (contra 25,2 milhões e 46 mil), 1.450 objetos fazendo sombra (contra 11.300).

**As técnicas do The Forest, o que já temos e o que falta:**
- os LODs com billboards: feito (o cartaz que vira para a câmera, de 2 triângulos, a partir de uns 190 m);
- o terreno em malha, não o Terrain da Unity: já é assim (o `ChaoDeLakewood` e a `FlorestaDeTeste` fazem a malha em
  pedaços, com as texturas misturadas pela cor do vértice, o "splat");
- o batching: o GPU Resident Drawer está ligado (o `DOTS_INSTANCING_ON` do shader) e o Forward+ também; o atlas das
  cartas e o dos cartazes são um só por tipo (uma textura para toda a floresta);
- a luz: a URP já calcula só os pixels da tela; o que pesa é desenhar a floresta de novo para as sombras e o reflexo,
  por isso a sombra mais curta e o reflexo limitado;
- a oclusão e a densidade falsa: ficam para as próximas partes (o SSAO já existe; a vegetação baixa e a grama densa
  vão tapar os vazios perto do jogador, e de longe a copa salpicada do relevo).

As fotos: `Imgs/Renders/Floresta_Leve_1.png` (de onde o jogador nasce), `_2.png` (a beira do riacho), `_3.png` (do alto,
sem a neblina) e `_4.png` (dentro da mata).

## Os draw calls e a grama densa (09/11/2026, depois do "ficou bem melhor, vamos seguir para a grama")

O usuário viu os draw calls chegando a 25 mil em partes da floresta, com o FPS caindo, e pediu a grama.

**Os draw calls: o GPU Resident Drawer não agrupava as árvores.** O shader estava certo (o `DOTS_INSTANCING_ON`), mas
o GRD exclui todo renderer de um `LODGroup` com o **cross-fade animado** ligado (`animateCrossFading = true`, o
`GRDExclusionReason.LODAnimateCrossFading` do pacote), e o gerador dos prefabs ligava isso: cada árvore era um draw call
(e, no Play, os objetos gerados com `HideFlags.DontSave` também ficam fora do GRD; a `FlorestaDeTeste` só usa o DontSave
fora do Play). Os 30 prefabs (os leves e os realistas) ficaram com o `animateCrossFading` desligado: o cross-fade continua
(`fadeMode = CrossFade`), mas pela distância, com o pontilhado na faixa de transição, que caiu de 35% para 20% do alcance
de cada LOD (com 35%, uma faixa larga de árvores pontilhadas a uns 40 m). Medido na cena leve, no mesmo quadro:

| onde | antes | depois |
|---|---|---|
| onde o jogador nasce | 26.813 draw calls | 933 |
| o ponto de medida (no meio da mata) | 18.126 draw calls (o setPass 133) | 888 (o setPass 56) |

Os triângulos ficaram os mesmos (1,77 milhão). Vale para o jogo inteiro: qualquer LODGroup com o cross-fade animado
fica fora do GRD.

**A grama densa** (`GramaDensa.cs`, o `GramaDensa.shader`, o gerador `GeradorDeGrama`; na cena leve): os tufos
desenhados em instâncias (`Graphics.RenderMeshInstanced`, sem um objeto por tufo, como a floresta do mundo) em células
de 8 m sorteadas pela semente quando a câmera chega perto (e esquecidas quando se afasta), até 45 m, encolhendo para o
pé no fim do alcance (o `_Some`). A densidade vem da `FlorestaDeTeste.Grama` (4 tufos por m² nas clareiras, 35% disso
sob a mata, nada no canal do riacho e pouca na beira de terra e cascalho), e os tufos são maiores nas clareiras.
- O atlas das folhas (`Grama_Folhas.png`, 1024 × 512, desenhado em código pelo Editor): 4 colunas, cada uma um tufo de
  13 a 17 folhas curvas (uma curva por folha, afinando para a ponta, com a nervura escura e as beiradas mais claras),
  do verde escuro do pé ao verde claro da ponta, uma em cinco seca e amarelada; cada tufo pega uma coluna pelo pé dele.
- O tufo (`GramaTufo.asset`): três cartas cruzadas (0°, 60° e 120°), de 0,9 × 0,5 m, em três linhas (o pé enterrado
  3 cm, o meio e a ponta) para dobrar no vento: 12 triângulos.
- O shader: o vento do `ArvoreRealista.shader` (mais uma onda que passa pelo campo no sentido do vento), **a dobra com
  quem passa do `Vegetacao.shader`** (os mesmos globais `_ZccDobradores`: o pé do jogador e o rastro dele, que volta aos
  poucos; no jogo o `FlorestaDoMundo` os põe), a normal virada para cima (a grama ilumina como o chão), o pé do tufo
  escurecido, o sol atravessando as folhas por trás e o tom por tufo. Sem sombra.
- O custo, no meio da mata, com 5.869 tufos na vista: +69 draw calls e +282 mil triângulos (os 12 por tufo, desenhados
  na profundidade, nas normais do SSAO, na cor e no reflexo do riacho); 751 draw calls e 0,96 milhão de triângulos no
  total. Na clareira, 6.065 tufos: 779 draw calls, 1,08 milhão.

As fotos: `Imgs/Renders/Floresta_Grama_1.png` (a clareira, na beira do riacho) e `_2.png` (a beira do riacho: a terra e
o cascalho sem grama, a mata com a grama rala do chão de agulhas atrás); `Floresta_Leve_GRD.png` é o ponto de medida
depois do conserto.

## As sombras por qualidade (09/11/2026, depois do "as sombras estão sendo desenhadas muito perto do jogador")

O usuário viu a sombra das árvores acabando perto dele, e pediu para pensar nessa cena como os gráficos na Alta, com a
Baixa mudando isso. Duas causas:
- **nos pinheiros leves só o LOD 0 fazia sombra**, e ele troca pelo LOD 1 a uns 50 m: a sombra das árvores sumia ali,
  antes do alcance da sombra do sol (90 m). Agora os LODs 0 e 1 fazem sombra (o LOD 1 tem 126 triângulos; só o cartaz
  do LOD 2 não faz), nos 15 prefabs leves e no gerador;
- **a cena não seguia a qualidade dos gráficos**: a sombra era fixa (90 m em 3 cascatas), e nas cenas de exemplo o
  `Graficos` só aplica a pixelação (a qualidade fica na Alta, 40 m no jogo).

Agora o `AjustesDaFlorestaDeTeste` aplica a sombra do sol e a grama **pela qualidade** (a gravada no jogo entra ao
abrir a cena; o **Q troca na cena**, sem gravar, e a linha de cima da tela mostra qual está), e reaplica quando o
`Graficos.Aplicar` (a pixelação, no X) devolve o asset do URP ao que era. O fim da sombra esmaece mais devagar (a borda
da última cascata em 20%). Medido no ponto de medida da cena leve, no Editor:

| qualidade | a sombra do sol | a grama | draw calls | triângulos | objetos com sombra | GPU |
|---|---|---|---|---|---|---|
| como estava (90 m, 3 cascatas, só o LOD 0) | 90 m | 45 m | 930 | 1,99 milhão | 199 | 3,2 ms |
| Baixa | 40 m, 2 cascatas, 1024 px | 18 m | 931 | 1,71 milhão | 236 | 3,0 ms |
| Média | 70 m, 3 cascatas, 2048 px | 28 m | 1.056 | 2,00 milhão | 349 | 3,2 ms |
| **Alta** | **120 m, 4 cascatas, 2048 px** | 45 m | 1.775 | 2,60 milhão | 1.044 | 4,1 ms |
| Ultra | 200 m, 4 cascatas, 4096 px | 45 m | 1.875 | 2,93 milhão | 1.144 | 3,8 ms |

(O tempo de GPU oscila de quadro para quadro no Editor: a diferença entre a Baixa e a Ultra fica abaixo de 1 ms; o que
muda mais são os triângulos das cascatas a mais.) A tabela é a da cena, nos campos do componente (`sombraAte`,
`cascatas`, `mapaDaSombra` e `gramaAte`, um valor por qualidade); no jogo, quem manda é o `Graficos` (a Alta com a sombra
a 40 m): se essa Alta for a do jogo, os números dele é que mudam.

As fotos, no ponto de medida: `Imgs/Renders/Floresta_Sombras_Antes.png` (como estava: a sombra das árvores acabando
a uns 50 m), `_Alta.png`, `_Ultra.png`, `_Media.png` e `_Baixa.png`.

**As distâncias dos LODs e a sombra pontilhada** (depois do "mesmo no Ultra eu consigo ver as sombras mudando na minha
frente numa distância pequena... qual a distância dos LODs?"). As trocas são por fração da altura da tela, então a
distância depende do tamanho da árvore (com o FOV de 70° da câmera e o lodBias 1: a distância = o tamanho do LODGroup
dividido por 1,4 vezes a fração). Com as frações iguais para os três tamanhos (20%, 6% e 0,3%), o pinheiro pequeno
(3–5 m) trocava para o LOD 1 a 11–18 m e virava cartaz a 38–59 m, e o médio (10–13 m) trocava a 35–46 m; e **a faixa do
cross-fade (20% do alcance de cada LOD) começava a 6 m nos pequenos e a 20 m nos médios**: as árvores pontilhadas na
frente do jogador, e **a passada de sombra pontilhava junto** (o `LOD_FADE_CROSSFADE` no ShadowCaster): a sombra
meio transparente, mudando conforme ele andava. Era isso que ele via, e não as cascatas.

O que mudou (nos 30 prefabs e no gerador):
- a passada de sombra do `ArvoreRealista.shader` não pontilha mais: na faixa de troca os dois LODs fazem a sombra inteira
  (a união das duas formas, quase iguais), e a sombra fica parada;
- as frações por tamanho nos leves: o alto 20%, 6% e 0,3%; o médio 15%, 4,5% e 0,3%; o pequeno 8%, 3% e 0,3%;
- a faixa do cross-fade de 20% para 10% do alcance de cada LOD (um anel estreito);
- a sombra suave do sol pela qualidade (o filtro: baixa na Baixa, média na Média, alta na Alta e na Ultra).

As distâncias agora (o FOV de 70° e a Alta; o Ultra do jogo multiplica por 1,5 e a Média e a Baixa por 0,8 e 0,6):

| pinheiro leve | o LOD 0 até (a faixa pontilhada antes) | o LOD 1 até | depois |
|---|---|---|---|
| alto (15–19 m) | 54–66 m (de 49–60 m) | 178–221 m | o cartaz, até sempre |
| médio (10–13 m) | 47–61 m (de 43–56 m) | 157–205 m | o cartaz |
| pequeno (3–5 m) | 28–45 m (de 26–41 m) | 76–118 m | o cartaz |

Os LODs 0 e 1 fazem sombra; o cartaz não (o pequeno fica sem sombra a partir de 76–118 m). O custo no ponto de medida,
na Alta: de 2,60 para 2,85 milhões de triângulos (o LOD 0 dos médios e dos pequenos dura mais), o GPU igual. O que ainda
muda com a distância são as cascatas da sombra (a resolução cai em cada troca: na Alta, a 14, 35 e 65 m; o URP não mistura
as cascatas), suavizado pelo filtro alto. A foto depois: `Imgs/Renders/Floresta_Sombras_Alta_2.png`.

**A neblina que veio do mundo** (depois do "entrei no mundo normal pra tirar uns prints e, quando voltei, a neblina ficou
mais forte e as árvores de perto vão renderizando conforme eu ando"): não era LOD nem culling. O passe de tela da
neblina de altura do jogo (`NeblinaDeAltura`, sempre ativo no renderizador) lê globais de shader (`_NeblinaParams`: a
densidade no chão, a altura da base, a queda e a distância máxima) que só o `AmbienteDoDia` do mundo escreve, e as
globais ficam na memória do Editor ao trocar de cena: a névoa baixa do mundo (0,07 no chão) ficou ligada na cena de
teste, tapando os troncos de perto enquanto as copas longe apareciam. Agora a `FlorestaDeTeste` escreve as globais da
neblina a cada quadro (o campo `neblinaDeAltura`, 0 por padrão: desligada; no jogo é 0,003 de dia e 0,07 a 0,1 de noite)
e o `AmbienteDoDia` zera a neblina ao sair da cena, para nenhuma cena de exemplo herdar a do mundo.

## A medida das instâncias e as otimizações (09/11/2026, depois da conversa com outro chat sobre os "60 mil draw instances")

O usuário trouxe a análise de outro chat ("60 mil draw instances é muito para uma floresta?") e pediu para analisar sem
mudar nada; depois, pediu as mudanças abaixo. Medido no ponto de medida da cena leve, na Alta de então: o "draw
instances" das estatísticas (o `UnityStats.instances`) soma cada objeto uma vez por passada em que ele é desenhado (a
profundidade e as normais do SSAO, a cor, cada cascata de sombra e as duas passadas do reflexo do riacho), e não as
árvores na tela.

| na Alta de então | draw calls | instâncias | triângulos |
|---|---|---|---|
| tudo ligado | 1.885 | 47.191 | 2,85 milhões |
| sem o reflexo do riacho | 1.500 | 27.687 | 1,95 milhão |
| sem a sombra do sol | 779 | 38.857 | 1,79 milhão |

As árvores davam 29 mil instâncias em 1.674 draw calls; a grama, 18 mil em só 36 (os 4.473 tufos em 4 passadas: as duas
da câmera e as duas do reflexo, que roda o SSAO também). A sombra era 59% dos draw calls. Do que o outro chat sugeria, já
estavam feitos o SRP Batcher e o GPU Resident Drawer, os LODs com o cartaz sem sombra, a sombra e a grama por qualidade,
as clareiras e o culling por células (a grama e a floresta do mapa). Uma dica dele atrapalharia: os Material Property
Blocks para variar a cor das árvores as tiram do GRD (o tom já sai no shader, pelo pé da árvore).

O que foi pedido e feito:
1. **O reflexo do cenário na água só na Ultra**, no jogo (o `Graficos`; a refração continua na Alta) e na cena.
2. **A grama fora do reflexo:** a `GramaDensa`, a grama baixa do `FlorestaDoMundo` e a do `LakewoodNaCena` só na câmera
   do jogador.
3. **A Alta da cena a 90 m em 3 cascatas** (era 120 m em 4).
4. O overdraw das cartas e o depth priming: explicado, sem mudança (o usuário não tinha entendido; fica para ele decidir).
   Depois, testado a pedido (veja "O depth priming, testado", abaixo).
5. **O pinheiro leve com um material só:** o tronco e as cartas na mesma submalha e no mesmo material (o `_UM_MATERIAL`
   do `ArvoreRealista.shader`: a cor do vértice A diz se é a casca, 0, ou os galhos, 1; a casca lê a textura dela, que se
   repete, e as cartas o atlas, cada uma com a sua normal, o seu brilho e a sua variação de tom; só os galhos têm a luz
   atravessando). O visual não muda: a foto no mesmo ponto e na mesma configuração difere em média 3 de 255 (o vento
   nos galhos). Na configuração de antes, os draw calls das árvores caíram de 1.674 para 895 e as instâncias delas de
   29 mil para 17 mil. O pinheiro realista continua com os dois materiais.
6. **A oclusão na GPU do GRD** (o que está escondido atrás de outra coisa não é desenhado): testada nesta mata, ela
   corta as instâncias das árvores de 18,7 mil para 8,3 mil e os triângulos em 37%, mas o quadro fica mais lento (na
   média de 150 quadros, a GPU de 2,92 para 3,32 ms e a CPU de 3,02 para 3,60 ms): numa mata plana e vazada quase nada
   fica escondido de verdade, e a conta da oclusão custa. Fica como opção da cena (o O, desligada por padrão) e para o
   teste no Lakewood, com os morros.

Agora, no mesmo ponto:

| qualidade | draw calls | instâncias | triângulos | draw calls antes |
|---|---|---|---|---|
| Baixa | 364 | 9.752 | 1,10 milhão | 931 |
| Média | 464 | 12.908 | 1,38 milhão | 1.056 |
| Alta | 498 | 17.799 | 1,54 milhão | 1.885 |
| Ultra | 1.182 | 28.797 | 3,15 milhões | 1.875 |

Na Alta, a média de 150 quadros no Editor: 2,9 ms de GPU e 3,2 ms de CPU, iguais às da Alta antiga sem o reflexo:
neste PC o quadro já era leve, e o ganho aparece num PC mais fraco. As fotos: `Imgs/Renders/Floresta_UmMaterial_Depois.png`
(a mesma vista e a mesma configuração de `Floresta_Sombras_Alta_2.png`, para comparar) e `Floresta_Alta_Nova.png`.

**O esquema para o Lakewood:** a oclusão na GPU e o culling por instância do GRD só valem para os MeshRenderers que
ele desenha. A vegetação do Lakewood (o `LakewoodNaCena`) e a do mapa sorteado (o `FlorestaDoMundo`) são desenhadas pelo
`Graphics.RenderMeshInstanced`, por blocos, e ficam fora dele. Quando os pinheiros novos forem para o Lakewood, escolher
entre: (a) renderers do GRD, instanciados por bloco perto do jogador (sem o `HideFlags.DontSave` no Play e sem o
cross-fade animado), com o material só do pinheiro leve, o que dá a oclusão, o cross-fade e o culling por instância; ou
(b) o `RenderMeshInstanced` dos blocos, como hoje, sem a oclusão. Com (a), medir a oclusão de novo lá (a opção
`gpuResidentDrawerEnableOcclusionCullingInCameras` do asset do URP, que a cena de floresta liga pelo O), porque os
morros escondem muito mais que a mata plana.

**O depth priming, testado** (a pedido: "Sim, testar"; o Z da cena, desligado por padrão). A passada de profundidade
já existe (a do SSAO); com ele, a passada de cor desenha com o teste "igual" e sem escrever a profundidade, e cada
pixel é iluminado uma vez só, e não cada carta de agulhas sobreposta. Na Alta, a média de 150 a 200 quadros:

| onde | a GPU sem | a GPU com |
|---|---|---|
| o ponto de medida | 2,91 ms | 1,88 ms |
| a clareira | 3,06 ms | 1,89 ms |

No ponto de medida, a foto é igual (`Imgs/Renders/Floresta_DepthPriming.png`). Mas **na vista da clareira umas árvores
somem**: as de trás da crista do morro, a 40–80 m, no LOD 0 e no LOD 1, de vários prefabs (a mesma vista sem e com:
`Floresta_DepthPriming_Defeito_Sem.png` e `_Com.png`). Não é o LOD (com o lodBias em 20 elas continuam sumidas), nem o
SSAO, nem a oclusão na GPU (desligada). Com o GPU Resident Drawer desligado elas aparecem, mas a cena volta a uns 3.250
draw calls. Pelo que parece, é um defeito da Unity 6000.6 entre o GRD e o depth priming (a profundidade sai sem essas
árvores, e a cor, com o teste "igual", não as desenha). **Fica desligado**; vale testar de novo numa versão nova da
Unity (o Z da cena).

**O lodBias** (achado no fim do teste): o do projeto é 2 (a qualidade PC, o da Alta: o Ultra fica em 3), mas estava em
1 desde um teste meu da versão leve (o "LOD 0 mais perto", que o devolveu para 1 sem ler o de antes); com ele, as
casas e os zumbis do jogo trocavam de LOD na metade da distância. Voltou para 2, e as frações dos 30 pinheiros (os
prefabs e o gerador) dobraram, porque foram medidas com o 1: o leve alto 40%, 12% e 0,6%; o médio 30%, 9% e 0,6%; o
pequeno 16%, 6% e 0,6%; o realista 28%, 9% e 0,8%. As distâncias da tabela das sombras e as medidas desta parte
continuam as mesmas.

**O que ainda dá para fazer** (não pedido): menos variações por tamanho (3 em vez de 5: menos lotes, com menos
variedade).

**O que fica para as próximas partes, nessa cena:** a vegetação baixa (os arbustos, as samambaias, o chão de agulhas
em 3D), as outras árvores (as copas largas, as mortas), as rochas e os troncos caídos. A floresta nova já foi para o
Lakewood (veja abaixo).

## A floresta nova no Lakewood (09/11/2026, a pedido: "essa configuração da floresta vai substituir toda a floresta atual do mapa lakewood"; feita, esperando o teste)

As escolhas do usuário (três perguntas): as folhosas (os bordos, os carvalhos, as bétulas, os salgueiros e as mortas,
uns 25% da mata) viram pinheiros; o chão da mata de antes (os arbustos, as samambaias, os tocos, os troncos caídos, as
pedras, o capim alto, a taboa da beira e a grama baixa) sai, e ficam só os pinheiros leves e a grama densa, como na cena
de teste; e a densidade é a da cena de teste.

**O sorteio** (`ChaoDeLakewood.Plantas`): só os 15 pinheiros leves, numa grade sacudida de 7 árvores por 100 m² (a de
antes tinha umas 2,7). As clareiras e os tamanhos são os da cena de teste (os pequenos e os médios na beira das
clareiras). O ruído do Lakewood foi esticado para dar o mesmo chão aberto: 7,7% em clareira, contra 8,8% lá, e 92% das
árvores mantidas, contra 91%.
- No campo, na cidade e na lavoura, uns pinheiros soltos (2,5%, 1,5% e 0,5% das células), quase todos pequenos e
  médios.
- Na rocha, 20% das células abaixo de 520 m e 3% acima, os menores no alto.
- Nenhum na água, na praia, nos barrancos dos rios, nas estradas (9 m) e nos paredões (inclinação acima de 1,1).
- Custa uns 0,3 ms por bloco, em outra thread.
- O chão das clareiras ganhou os pesos do campo, a grama, como na cena de teste (`Pesos`); antes ficava com o chão de
  agulhas escuro.

**O desenho** (`LakewoodNaCena`, o esquema (a) da parte de cima):
- **Os objetos de perto:** os pinheiros de cada bloco viram os prefabs, pelo GPU Resident Drawer, com os LODs, a
  sombra, o cross-fade pela distância e o material só.
- **Até onde:** até o maior pinheiro já ter virado cartaz. É o alto, 22 m na escala 1,2, que sai do LOD 1 na fração
  0,12 da tela. A conta usa a câmera e o lodBias, com 10% de folga: uns 300 m na Alta e 450 m na Ultra.
- **Os cartazes de longe:** depois disso, até os 480 m das plantas, só os cartazes, em instâncias e sem sombra, com a
  malha e o material do LOD 2 dos prefabs. Na troca, o cartaz do objeto e o das instâncias são o mesmo desenho.
- **O ritmo:** os objetos entram bloco a bloco, o mais perto primeiro, uns 600 por quadro, e um bloco nunca fica pela
  metade. Os da partida já nascem como objetos no `Awake`.
- **A saída:** 40 m além do raio, eles são desligados e guardados num monte por prefab, para o próximo bloco.
- **A colisão** dos troncos é a cápsula do prefab.

**A grama densa** (`GramaDensa`):
- **O chão:** ela lê o chão por uma interface (`IChaoDaGrama`), que a cena de teste e o `ChaoDeLakewood` cumprem. Com
  o chão do Lakewood, as células de 8 m são montadas em outra thread, até 12 ao mesmo tempo.
- **A densidade** (`ChaoDeLakewood.Grama`):
  - cheia no campo e nas clareiras, 35% sob a mata, 30% na rocha, 70% na lavoura e 50% na cidade;
  - nada na água, na areia molhada da praia, no canal dos rios, no asfalto, no acostamento e nos paredões.
- **O alcance** é o do jogo (`Graficos.Grama`): 45 m na Alta e na Ultra, 28 na Média, 18 na Baixa.
- **A dobra:** a grama dobra com a raiz da câmera, como na cena de teste. Não foi conferido hospedando, com o jogador
  de verdade, e com o carro ainda não dobra.

**A tabela das sombras da floresta** (`Graficos.TabelaDaFloresta`, ligada pelo `LakewoodNaCena` enquanto a cena está
aberta) é a da cena de teste:
- Baixa: 40 m em 2 cascatas;
- Média: 70 m em 3;
- Alta: 90 m em 3;
- Ultra: 200 m em 4, com o mapa de 4096.

A borda das cascatas fica em 0,2, e a sombra suave do sol vai pela qualidade. No mundo sorteado continua a tabela de
antes.

**Achado no caminho:** a cena do Lakewood guardava as plantas até 320 m sem a neblina, um valor de antes gravado na
cena; o código e o plano diziam 480 m. Como a copa de longe só sobe a partir de 430 m, os morros entre 320 e 430 m
ficavam pelados. A montagem da floresta (Zombie City Cleaner > Floresta > Pôr a floresta nova no Lakewood) põe os
prefabs, a grama e os 480 m na cena sem trocar a cena aberta.

**Medido no Play** (no Editor, sem a neblina, ao meio-dia, com a câmera do menu na partida; a média de 200 quadros):

| qualidade | GPU | CPU | draw calls | instâncias | triângulos | objetos (até) | cartazes |
|---|---|---|---|---|---|---|---|
| Alta | 2,24 ms | 2,48 ms | 540 | 19.299 | 1,20 milhão | 5.192 (302 m) | 2.383 |
| Ultra | 3,62 ms | 3,47 ms | 1.086 | 17.907 | 2,33 milhões | 7.867 (448 m) | 44 |

Andando a 30 m/s por 12 s, como o carro, com os pinheiros aparecendo pelo caminho: nenhum quadro passou de 33 ms (o
pior foi 16,4 ms).

As fotos, em `Imgs/Lakewood/`:
- `Lakewood_Floresta_Partida.png`;
- `Lakewood_Floresta_Mata.png`, dentro do bloco mais cheio (284 pinheiros);
- `Lakewood_Floresta_Alto.png`, a 45 m do chão: a mata, uma clareira e, à esquerda, a área de cidade ainda vazia.

A luz escura das fotos é a do Lakewood (o céu nublado do jogo, igual às fotos de antes), não a da floresta.

**Ainda não:**
- a grama dobrando com o carro;
- as "Fotos de perto" do Editor (Lakewood > Fotos de perto), que ainda desenham os pinheiros low-poly da fase 1;
- as clareiras na copa de longe (o relevo de 25 m);
- as outras árvores e o chão novos (as copas largas, as mortas, os arbustos, as rochas e os troncos), que vêm pelas
  próximas partes da cena de floresta.

## Os ajustes depois do teste no Lakewood (10/11/2026, a pedido; feitos, esperando o teste)

O usuário pediu quatro coisas, e cada uma tinha uma causa.

**1. "O vento na grama tá muito bugado, tem horas que ela fica tremendo muito de uma forma estranha."**
- **A causa:** no `GramaDensa.shader`, a onda que corre pelo campo usava `dot(pé do tufo, direção do vento)`. A
  direção gira devagar (o `LakewoodNaCena` a vira uns 35° em uns 3 minutos), mas no Lakewood os tufos ficam a uns 9 km
  da origem. Aí esse giro lento fazia a fase da onda andar dezenas de radianos por segundo, e a grama tremia rápido
  sempre que o vento virava. Na cena de teste, a 160 m da origem, isso não aparecia.
- **A correção:** a onda vai numa direção fixa (a média do vento do jogo); o tufo continua deitando para o lado do
  vento de verdade.
- **De quebra:** a força na grama ganhou um teto suave (0,42 × tanh(força / 0,42): a calma fica igual, e a tempestade,
  até 0,95 no jogo, não faz a ponta do tufo ir quase 1 m de um lado ao outro). No vento forte, o tufo deita firme e
  balança menos.

**2. "Tem lugares com clareiras muito grandes e dá pra ver onde a grama acaba... sem pesar."** Três coisas juntas:
- **O chão de grama embaixo** (o item 3).
- **Os tufos indo para a cor do chão:** da metade do alcance até o fim, eles vão para a cor média da textura de grama
  do chão (o `_Desbota` e o `_CorDoChao` do shader). Os tufos eram uns 40% mais escuros e mais saturados que o chão, e
  era isso que desenhava a borda.
- **O anel de longe** (`GramaDensa.alcanceLonge`, ligado pelo `LakewoodNaCena` na Alta e na Ultra até 90 m,
  `raioDaGramaLonge`): depois dos 45 m, 20% dos tufos, 25% maiores, só onde a grama é cheia (o campo e as clareiras; sob
  a mata, as árvores tapam a borda). Eles crescem dentro do fim do de perto enquanto ele encolhe.
- **Para não pesar,** as células de cada faixa são revistas só quando a câmera anda 2 m, quando chega uma célula nova ou
  a cada meio segundo; por quadro, só o teste da vista nas células com tufos.

Na partida, na Alta (a média de uns 1.400 quadros no Editor):

| | GPU | CPU | draw calls | triângulos | tufos |
|---|---|---|---|---|---|
| sem o anel | 2,16 ms | 2,30 ms | 540 | 1,20 milhão | 6.471 |
| com o anel | 2,25 ms | 2,58 ms | 560 | 1,28 milhão | 9.759 |

**3. "Cobrir o mapa com uma textura mais verde e a terra ficar em volta dos rios e do lago somente."** A terra que
cobria boa parte do mapa vinha de dois lugares:
- **A mata:** o chão de folhas (o canal A, quase só terra marrom com umas manchas de musgo), em 60% dela.
- **As áreas de cidade (ainda vazias) e de lavoura:** a terra e o cascalho, de 40 a 55%.

Os pesos do `ChaoDeLakewood.Pesos` passaram a ser a grama em tudo:
- a mata: 62% de grama e 35% de folhas, em manchas;
- o campo: 90% de grama;
- a lavoura e a cidade: 82%;
- a rocha: 55% de grama e 37% de cascalho (a rocha das encostas em pé é a do shader).

A terra e o cascalho ficam só na praia e nos barrancos dos rios, como antes. O relevo de longe usa os mesmos pesos.

**4. "A textura da água tá estranha, dá pra ver meio que um risco... o lago muito escuro e morto."**
- **O risco:** o normal map das ondinhas (`TexturasDaAgua`) não emendava. As ondas compridas usavam `cos(ângulo) ×
  períodos` e `sen(ângulo) × períodos`, que não são inteiros, e a textura deixava uma linha a cada 6 m (e a cada 16 m,
  na segunda camada). Agora os períodos em x e em y são inteiros: a borda da textura difere tanto quanto dois pixels
  vizinhos do meio (0,0088 contra 0,0094).
- **As ondinhas que escorregavam:** a água tinha o mesmo problema do vento. Elas andavam `vento × tempo`, e com a direção
  girando o deslocamento crescia com o tempo de jogo; agora andam em duas direções fixas.
- **A cor:**
  - a funda passou de (0,035; 0,075; 0,08) para (0,08; 0,24; 0,28);
  - a rasa ficou mais azul-esverdeada;
  - a água ficou mais clara (`_Turvo` 0,35 em vez de 0,5);
  - o reflexo ganhou uma tinta, a `_TintaDoReflexo` (0,64; 0,9; 1): o céu nublado e cinza refletido de longe deixava o
    lago morto.

As fotos, em `Imgs/Lakewood/`:
- `Lakewood_Ajustes_Partida.png` e `_Partida_Antes.png`: o chão verde e o anel de longe;
- `Lakewood_Ajustes_Lago.png`: a água nova.

O vento forte não foi visto no Play: sem hospedar, o clima fica zerado.

## As fases do gerador de floresta (o prompt, na ordem dele)

- **Fase 1, os pinheiros** (a primeira versão, low-poly, foi reprovada: "ainda não tá bom"; a segunda, realista, está
  acima): o `GeradorDeFloresta` (Zombie City Cleaner > Floresta): os
  três tamanhos (alto 13–18 m, médio 8–12, pequeno 3–6), cinco variações cada pela semente, três LODs, as malhas em
  `Art/Floresta/Malhas`, os prefabs em `Art/Floresta/Prefabs` (o LODGroup, o material da vegetação e a colisão do
  tronco), a cena `Scenes/Exemplos/Floresta.unity` (a fileira, a mata de teste e a área dos LODs). No Lakewood, a mata
  usou estes pinheiros até a floresta nova (veja "A floresta nova no Lakewood").
- **Fase 2, as copas largas:** o carvalho (verão e outono), a bétula e o ácer.
- **Fase 3, as mortas:** a árvore morta e o tronco quebrado.
- **Fase 4, a vegetação baixa:** os arbustos, as samambaias e as rasteiras; a grama densa.
- **Fase 5, o chão:** as rochas, os troncos caídos, os tocos e os detalhes.
- **Fase 6, a otimização; fase 7, a cena de apresentação.**

## O que fica para depois

- As árvores de longe como silhuetas (impostores) entre 480 m e 1,5 km, se a copa de longe não bastar.
- O reflexo da água lendo o céu novo (hoje é a luz ambiente mais o cenário espelhado).
- A neblina mais rala como opção (hoje a Visão das Configurações vai até 85 m).
