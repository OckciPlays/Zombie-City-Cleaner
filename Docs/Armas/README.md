# As armas novas (reformulação visual) — 29 e 30/10/2026

## O machado (31/10/2026): a segunda arma branca, nas duas mãos

A pedido ("vamos criar o machado agora", com a imagem de um machado de cabo comprido; "o machado pode fazer a pegada em 2
mãos"):

- **O modelo** (`Blender/Armas/machado.py`, o editável `Machado.blend`; as funções são as do `faca.py`, com a cfg dele):
  a cabeça preta, o fio de aço e o cabo de madeira, mais grosso perto da cabeça e com o gancho no fim; 310 triângulos, nas
  cores da `Art/Paleta.png`. A cabeça foi refeita no mesmo dia, a pedido ("a parte de metal está muito bugada e não forma
  um padrão de machado ... o cabo entra dentro da parte de metal"; a referência, um machado Vonder): o olho é um bloco que
  envolve a ponta do cabo (o cabo some dentro dele), a lâmina é um trapézio que abre para baixo e afina até o fio
  (`_cunha`: a grossura vai de 1,65 cm no olho a 0,45 cm no fio), e o fio de aço é levemente curvo. O cabo não mudou.
  Backup do machado de antes: `Backups/Machado_antes_da_cabeca_nova_2026-10-31`. A malha avulsa no `Itens_Machado.fbx`, o ícone
  (4 × 1 células) e a foto.
- **Segurar, nas duas mãos:** os braços (`Jogador_FP_Machado.fbx`) são os dos punhos com o machado no punho direito,
  deitado na diagonal, atravessando a tela (apontando para a frente, ele ficava encurtado e a machadada não aparecia), e a
  mão esquerda no cabo, 22 cm acima da direita, em todos os clipes (IK por quadro, assado em quadros-chave).
- **Atacar:** a machadada (o clipe `Golpe`, 15 quadros): as mãos sobem à direita, descem na diagonal atravessando a tela
  e voltam; pega no quadro 8. 3,5 de dano, 1,6 m, um golpe a cada 0,9 s, na parte do corpo onde pega: a cabeça sai, um
  braço ou uma perna sai num golpe, dois no tronco matam.
- **Os números das armas brancas** ficam no `Itens` (`DadosDaBranca`: o dano, o alcance, a grossura, o intervalo, o
  momento do impacto e se acerta pela parte do corpo); o `ArmaJogador.GolpearComBranca` usa os da arma da mão.
- **Conferido** (Demo_Cidade): na mão (`capturas/jogo_machado_parado.png`), no meio da machadada
  (`jogo_machado_golpe_a.png`) e num zumbi na rua (a cabeça saiu no primeiro golpe). Prancha: `capturas/machado_prancha.png`.
- **A pegada e o golpe refeitos (31/10/2026, a pedido, com a imagem de referência: "a mão direita segurando o cabo mais
  em cima, a mão esquerda mais embaixo ... o golpe, um movimento de rotação no sentido da esquerda"):** nos braços, o
  machado em pé à direita da tela, a cabeça inteira em cima e o fio virado para a esquerda (`GIRO_DE_LADO` -10,
  `GIRO_PARA_A_FRENTE` 62, `GIRO_NO_EIXO` -50); a mão direita 18 cm acima da ponta do cabo (`NO_PUNHO`) e a esquerda
  embaixo dela (`SEGUNDA_MAO` 0,05). O golpe (`CHAVES_DO_GOLPE`): prepara um pouco à direita (quadro 4), o machado gira
  para a esquerda e deita (quadros 7 e 8, o impacto, na altura da mira) e segue até o quadro 11. No corpo, o mesmo:
  em pé na mão direita, a cabeça para cima e o fio para a esquerda, a mão 18 cm acima no cabo
  (`MontagemDaRede.GiroNoCorpo` e `PegadaNoCorpo`). As fotos de conferência do `faca.py` agora usam o campo de visão
  da câmera dos braços do jogo (55°; antes, 70°, e o que cabia na foto saía cortado no jogo).
- **Falta:** a machadada no corpo (os outros veem o soco direito, com o machado na mão), o som próprio e o lugar no loot.
  No corpo do dono, o machado (como as outras armas na mão) ainda fica escuro perto das lâmpadas (as lâmpadas não
  iluminam a camada das armas da mão desde o conserto da sombra da lanterna).

## A faca (31/10/2026): a primeira arma branca, o sistema básico

A pedido ("vamos fazer agora um mecanismo para armas brancas. Vamos começar pela faca. Por hora crie um sistema básico de
atacar e segurar, vou arrumar posteriormente"; a imagem de referência é uma baioneta M9):

- **O modelo** (`Blender/Armas/faca.py`, o editável `Faca.blend`): a lâmina preta com o fio de aço, os dentes do dorso e o
  furo sextavado, a guarda com o anel de cima, o cabo de 4 gomos e o pomo com a trava; 722 triângulos, nas cores da
  `Art/Paleta.png` (a paleta dos itens, como a lata e a garrafa). A malha avulsa vai no `Itens_Faca.fbx`, e o ícone e a
  foto em `Art/UI/Icones` (`Icone_Faca`, 2 × 1 células, e `Foto_Faca`).
- **Segurar:** os braços de primeira pessoa (`Jogador_FP_Faca.fbx`, o editável `Bracos_Faca.blend`) são os dos punhos
  com a faca no punho direito (presa ao `hand.R`; o cabo atravessa a mão, a lâmina sai pelo lado do polegar, deitada
  40° para a frente e girada 70° para o lado chato aparecer: `GIRO_PARA_A_FRENTE` e `GIRO_NO_EIXO` no script). No corpo,
  a faca fica no encaixe da lata, na guarda dos punhos.
- **Atacar:** segurando o clique, o soco direito com a faca (a estocada), 2 de dano por golpe no corpo do zumbi (3
  golpes matam), até 1,35 m do olho. Os números ficam no `Itens` (`DadosDaBranca`, a tabela das armas brancas).
- **Conferido** (Demo_Cidade): na mão parada e golpeando (`capturas/jogo_faca_parada.png`, `jogo_faca_golpe.png`), em
  terceira pessoa (`jogo_faca_terceira_frente.png`) e num zumbi na rua (morto no terceiro golpe). Para pegar: F1, o
  menu do desenvolvedor.
- **Falta (para o "vou arrumar"):** o som próprio (hoje, o vento e a pancada do soco), as animações próprias (a guarda e
  o golpe são os dos punhos), o lugar dela no corpo quando não está na mão (o cinto) e no loot do mundo.

## A submetralhadora v3 (30/10/2026): a MP5K, no jogo

A pedido ("vamos atualizar a smg agora", com a imagem de uma MP5K de coronha dobrável, empunhadura vertical e carregador
curvo; no conceito das outras v3):

- **O modelo** (`Blender/Armas/v3_mp5.py`, o editável `Submetralhadora3.blend`, `Modelos/Submetralhadora3.fbx`): sem
  modelo fonte (não veio um .blend da MP5), toda modelada em blocos a partir da imagem, pela régua `Y()`/`Z()` do
  script (de um pixel da imagem para o espaço da arma, com a boca do cano no ponto da SMG antiga e o punho na mão
  direita). O receptor preto de topo arredondado (com a linha funda dos lados, a janela de ejeção à direita e a fenda
  da alavanca à esquerda), a capa da frente com a argola e o cano, a massa de mira com o capuz redondo e a alça de
  tambor, a caixa do gatilho preta fosca com a frente inclinada, o punho, o guarda-mato, o seletor dos dois lados, o
  retém, a empunhadura vertical com os anéis, a coronha dobrável aberta (a dobradiça, a barra, a soleira e a escora),
  o carregador curvo com as nervuras e a alavanca de manejo na frente, à esquerda. Cores chapadas da paleta, quinas
  chanfradas e lisas só até 37°. **1.486 triângulos** (corpo 1.242, alavanca 56, carregador 188).
- **A mão esquerda na empunhadura** (`Blender/Armas/maos_smg.py`, o editável `Bracos_Submetralhadora.blend`): nos
  braços de primeira pessoa da SMG (`Jogador_FP_Submetralhadora.fbx`), a mão esquerda abraçava a frente do receptor
  antigo por baixo (os dedos dobrando em volta do eixo do cano); agora o mesmo abraço gira para a empunhadura em pé (o
  indicador em cima, o mindinho embaixo, a palma virada para o cabo) e o miolo da mão fica do lado esquerdo dele.
  Perto da pose de segurar a mão vai para a empunhadura; longe (na recarga, para trocar o carregador) segue o caminho
  original. O braço segue por IK e tudo é assado em quadros-chave, como no `maos_sniper.py`. O original está no
  backup, e o script parte sempre dele.
- **No jogo:** `ArmasNovas.Ativas` tem a `Submetralhadora3` (a SMG não estava na reformulação: antes, os braços usavam
  a malha do FBX direto). O backup do que estava no jogo antes (os braços FP, a arma solta, os ícones, o prefab e o
  `ArmasNovas.cs`) está em `Backups/Submetralhadora_antes_da_v3_2026-10-30/`.
- **Conferido** (Demo_Cidade, anfitrião local): na mão em primeira pessoa (`capturas/jogo_submetralhadora_v3.png`),
  limpa, com a mão esquerda na empunhadura; na prévia parada, no tiro e na recarga (`capturas/previa_submetralhadora_v3/`);
  no ícone do atalho. Prancha: `capturas/submetralhadora_v3_prancha.png`. O corpo (terceira pessoa) usa a animação da
  SMG antiga; o tiro com o mouse de verdade não foi testado.

## O rifle v3 (30/10/2026): a M4A1, no jogo

A pedido ("melhorar o rifle, seguindo o mesmo conceito da sniper (...) utilizar essas imagens como referências (...)
próximo a ~3 mil triângulos"; das duas imagens de referência, a escolhida foi a M4A1 com a alça de transporte; "cuidado
para não ficar com aquela textura de massinha novamente"):

- **O modelo** (`Blender/Armas/v3_m4.py`, o editável `Rifle3.blend`, `Modelos/Rifle3.fbx`): o AR-15 que o Pedro mandou
  (`Fontes/cc_iur.blend`), fiel, nas cores chapadas da paleta (sem textura assada: nada de "massinha"): o receptor e o
  metal pretos, o cano grafite, a coronha, o punho e o guarda-mão no `polimero` (um cinza fosco que entrou no fim da
  paleta). Do AR-15 saem as miras rebatíveis, o guarda-mão de trilhos (o receptor de cima é cortado na frente), o
  quebra-chamas e a alavanca de manejo ambidestra; modelados no lugar, como na M4A1 da imagem: a alça de transporte (com
  a mira de trás, as orelhas, o tambor e os parafusos), o anel do guarda-mão, o guarda-mão redondo de 8 gomos, a tampa
  dele, a massa de mira em "A" sobre o bloco de gases (com a alça da baioneta), o quebra-chamas de 8 lados e a alavanca
  em "T". **3.352 triângulos** (corpo 2.976, alavanca 68, carregador 308).
- **Como ficou leve sem virar massinha:** as peças do AR-15 passam pelo Decimate (o receptor de baixo e o de cima a 30%,
  as miúdas de 10% a 60%), e as quinas delas ficam vivas acima de 12°: facetadas, no estilo do jogo. Lisas até 30° (como
  na sniper), as faces irregulares do Decimate davam um sombreamento borrado; as peças modeladas aqui ficam lisas até
  37° (o guarda-mão redondo). As peças que viravam lascas no Decimate (a alavanca ambidestra) foram modeladas.
- **O encaixe** é o da M4 v1 (`arma_rifle.py`): a escala 0,12, a boca do cano no ponto da antiga, a empunhadura na mão
  direita e o guarda-mão na esquerda. As peças: o corpo ("gun"), a alavanca ("slide") e o carregador ("mag").
- **No jogo:** `ArmasNovas.Ativas` usa o `Rifle3`. O `Rifle2` (o estilizado) e o v1 continuam no projeto; o backup do que
  estava no jogo antes da troca (as malhas, os ícones, o prefab do jogador, a paleta e o `ArmasNovas.cs`) está em
  `Backups/Rifle2_antes_do_rifle_v3_2026-10-30/`. Fotos: `capturas/rifle_v3_*.png`, `capturas/previa_rifle_v3/`; prancha (a referência, o modelo e o jogo):
  `capturas/rifle_v3_prancha.png`.
- **Conferido** (Demo_Cidade, anfitrião local): na mão em primeira pessoa (`capturas/jogo_rifle_v3.png`), limpo, sem
  manchas; no ícone do atalho. As 4 armas no prefab conferidas depois da troca. O tiro com o mouse de verdade e a
  terceira pessoa não foram olhados de perto.

## A sniper v3 (30/10/2026): a Mosin, no jogo

A pedido ("o modelo que eu quero que você faça é uma mosin (...) pegue a cor, o modelo, e siga fiel, o scope está
excelente do jeito que está"; "tire aquele apoio embaixo da arma, faça com que o personagem segure no cabo"; "a luneta
está muito atrás em relação à arma"):

- **O modelo** (`Blender/Armas/v3_mosin.py`, o editável `Sniper3.blend`): a Mosin-Nagant que o Pedro mandou, fiel à forma
  e às cores dela (a nogueira, o metal preto, o ferrolho de aço), nas cores chapadas da paleta das estilizadas (a cor
  `nogueira` entrou no fim da paleta), sem apoio: só a coronha lisa, com o punho. A luneta é a "scout" da sniper v1
  (pedido: "o scope dela está bem pra trás ainda, o scope de como estava esse modelo estava melhor feito"): à frente do
  receptor, sobre a base da alça de mira, com o trilho, os dois anéis, as torres, o anel de aumento, a ocular de
  borracha e as lentes; o ferrolho corre por baixo dela. (Antes, a luneta da v2 sobre o receptor: ficava atrás demais.)
  8.548 triângulos. A caixa do carregador sai na recarga.
- **A mão no punho** (`Blender/Armas/maos_sniper.py`, o editável `Bracos_Sniper.blend`): nos braços de primeira pessoa da
  sniper (`Jogador_FP_Sniper.fbx`), a mão direita sai do cabo da antiga (quase em pé) para o punho da Mosin (quase
  deitado), girando 88°, em todos os clipes; no tiro, ela vai à alavanca da Mosin (25 cm à frente da antiga, um pouco
  mais baixa e para a direita, para não passar por cima da luneta) e volta. O braço segue por IK, e tudo é assado em
  quadros-chave. A mão esquerda e os clipes não mudam (os mesmos nomes e durações). O original está no backup
  (`Backups/Armas_antes_do_visual_novo_2026-10-29/Jogador/Jogador_FP_Sniper.fbx`), e o script parte sempre dele.
- **A versão leve** (30/10/2026, a pedido: "8500 triângulos está um pouco pesada demais, será que conseguimos abaixar
  um pouco?"; `Blender/Armas/v3_mosin_leve.py`, o editável `Sniper3Leve.blend`, `Modelos/Sniper3Leve.fbx`): a mesma Mosin
  com a mesma luneta, com **4.219 triângulos** (era 8.548; corpo 3.503, ferrolho 533, carregador 183). A luneta tem as
  mesmas peças nos mesmos lugares, com 16 lados nos tubos (eram 24), 10 nas torres (eram 20), 6 nos parafusos (eram
  10), o chanfro só nos aros e as fendas do trilho sem chanfro (só isso tirou 2.134 triângulos). As peças da Mosin passam
  pelo Decimate (por colapso) antes de tudo: a coronha 0,5, o receptor 0,6, o cano 0,4, a telha de cima 0,6, a cabeça do
  ferrolho 0,5 e o corpo dele 0,6. A forma, as cores, o encaixe e a alavanca (onde a mão vai no tiro) não mudam; os
  braços de primeira pessoa são os mesmos. Comparação: `capturas/sniper_v3_leve_prancha.png` (e `sniper_v3_leve_*.png`,
  `previa_sniper_v3_leve/`). A `Sniper3` (8.548) fica no projeto; o backup do que estava no jogo antes da troca está em
  `Backups/Sniper3_antes_da_versao_leve_2026-10-30/`.
- **No jogo:** `ArmasNovas.Ativas` usa a `Sniper3Leve`. A `Sniper3` e a v2 continuam no projeto.
- **Conferido** (Demo_Cidade, anfitrião local): na mão em primeira pessoa (`capturas/jogo_sniper_v3.png`), na prévia
  parada e no meio do ferrolho, em terceira pessoa (`capturas/jogo_terceira_sniper_v3_*.png`), no painel do bunker e no
  ícone. Prancha: `capturas/sniper_v3_prancha.png`.
- **Falta:** o corpo (terceira pessoa) ainda usa a animação da sniper antiga: a mão direita fica perto do punho, mas não
  foi ajustada como a de primeira pessoa. O tiro com o mouse de verdade não foi testado.

## Estado (30/10/2026): a pistola v1 e as estilizadas (v2) no jogo

Depois de ver a primeira versão no jogo, o Pedro pediu outro caminho para as armas longas: "o rifle está com a textura
muito bugada (...) massinha borrada (...) deixar menos detalhista e mais low poly ainda, como era a antiga"; "a sniper
está com esse apoio no seu cabo sem motivo algum"; "a espingarda também parece que está muito menor (...) a antiga tinha
uma identidade"; "eu queria apenas uma melhoria das anteriores, porém parece que só ficou mais realista, não é o tema do
jogo". Então:

- **O rifle, a espingarda e a sniper (v2, estilizadas)** são as próprias armas antigas do jogo, melhoradas: a mesma
  forma, o mesmo tamanho e as mesmas cores (o rifle preto; a espingarda e a sniper com a madeira), com as quinas
  chanfradas, faces planas, alguns detalhes no mesmo estilo de blocos e uma paleta própria de cores chapadas (com o metal
  e a lisura de cada cor). Sem textura assada e sem normal map (era o que dava a "massinha"):
  - **rifle:** a tampa da janela de ejeção, o botão de avanço, os pinos de desmontagem, as nervuras e a base do
    carregador e a soleira de borracha (4.244 triângulos; a antiga, 1.000);
  - **espingarda:** o porta-cartuchos do lado esquerdo do receptor com 4 cartuchos vermelhos, os pinos e a soleira de
    borracha (2.540; a antiga, 500);
  - **sniper:** as lentes da luneta, a ocular de borracha, a bola de aço na ponta da alavanca do ferrolho e a soleira de
    borracha (2.462; a antiga, 592). A sniper volta ao desenho da antiga, em que a mão e o ferrolho batem com a
    animação: não precisou mexer na animação nem pôr o apoio.
- **A pistola** continua a da v1 (a Fort-17, aprovada).
- As v1 do rifle, da espingarda e da sniper (as realistas) ficam no projeto, fora do jogo: os FBX e as texturas em
  `Art/Armas`, os materiais em `Art/Armas/Materiais`, os `.blend` em `Blender/Armas`.

Prancha da v2: `capturas/armas_v2_prancha.png` (de 3/4, de lado e na mão) e `capturas/jogo_terceira_v2_prancha.png`
(em terceira pessoa).

### Como a v2 é feita

- `Blender/Armas/estilizadas.py` (o caminho), `v2_rifle.py`, `v2_espingarda.py`, `v2_sniper.py` (os detalhes e as cores
  de cada uma) e `rodar_v2.py` (as etapas). Os editáveis: `Rifle2.blend`, `Espingarda2.blend` e `Sniper2.blend` (cada
  cor é um material `Arma2_<cor>`: troque o material de uma face e exporte de novo).
- A base é a arma antiga dos braços de primeira pessoa (a pose de repouso), levada para o espaço da arma solta e
  separada pelos ossos: a arma nova fica exatamente onde a antiga estava nas mãos.
- A paleta: `Art/Armas/Texturas/Armas_Paleta_Cor.png` e `Armas_Paleta_MS.png` (64 x 64, 4 x 4 cores, sem filtro e sem
  mipmaps), compartilhadas pelas três. Os materiais (URP Lit, só a cor e o metal/lisura) ficam em
  `Art/Armas/Resources/MateriaisProprios/<modelo>.mat`.
- Na Unity: `ArmasNovas.Ativas` (a pistola v1 e as três v2) e o menu **Zombie City Cleaner > Armas > Pôr as armas
  novas**. Refazer: `blender -b --factory-startup --disable-autoexec --python Blender/Armas/rodar_v2.py -- <rifle|espingarda|sniper> tudo`
  (~1,5 min por arma) e o menu.

### Testado (30/10/2026, Demo_Cidade, anfitrião local)

As três v2 na mão em primeira pessoa e nas mãos do corpo em terceira pessoa (dos dois lados), as soltas presas na
mochila, os ícones novos na barra; o Console sem erros. Não testados: o tiro e a recarga com mouse e teclado, o
multijogador com 2 ou mais jogadores.

---

## A primeira versão (v1, 29/10/2026)

As 4 armas (a pistola 9 mm, a M4, a espingarda calibre 12 e a sniper) ganharam modelos novos, feitos a partir dos
modelos prontos que o Pedro mandou e adaptados ao jogo: preto e grafite, com materiais PBR (metal pintado, grafite,
aço exposto, polímero, polímero pontilhado, borracha, lentes) assados num atlas por arma. Já estão no jogo: nos braços
de primeira pessoa, no corpo (terceira pessoa), na arma solta (o chão, o painel do bunker, as costas e o coldre) e nos
ícones e fotos do inventário. Nenhuma mecânica mudou (dano, cadência, recuo, munição, recarga, rede, animações).

Prancha final: `capturas/armas_prancha.png` (de 3/4, de lado e na mão, no jogo).

## Números

| Arma | Base (modelo do Pedro) | Triângulos | A antiga | Peças (osso) |
|---|---|---:|---:|---|
| Pistola | Fort-17 | 3.340 | 364 | corpo (gun), ferrolho (slide), carregador (mag) |
| M4 | AR-15 "cc iur" | 14.042 | 1.000 | corpo, ferrolho (a alavanca de manejo), carregador |
| Espingarda | Remington 870 | 4.962 | 500 | corpo, telha (pump: a telha e o ferrolho), cartucho (shell) |
| Sniper | Mosin-Nagant | 8.854 | 592 | corpo, ferrolho, carregador (a caixa) |

Cada arma: 1 material (URP Lit) e 3 texturas de 2048 (`<Arma>_Cor`, `<Arma>_MS`: metal no R, oclusão no G, lisura no A,
e `<Arma>_Normal`).

## O que foi feito em cada uma

- **Pistola (Fort-17):** o ferrolho grafite com o serrilhado, a janela de ejeção com o cano de aço, as miras com pontos
  brancos (eram emissivos no modelo; aqui são tinta, sem brilho), a armação de polímero com a empunhadura pontilhada, o
  cão, a trava, a alavanca do ferrolho e o carregador. Escala 0,115 (1,2 vez o tamanho real, como a antiga) e 20% mais
  larga (o cabo da antiga era mais grosso).
- **M4 (AR-15):** os receptores pretos, o guarda-mão de trilhos, as miras rebatíveis, o quebra-chamas e o cano grafite,
  a empunhadura pontilhada, a coronha de polímero com a soleira de borracha e o carregador de aço. A tampa da janela de
  ejeção foi fechada e o ferrolho de dentro saiu (não aparece). Para caber no jogo: as faces que nenhum ângulo vê foram
  tiradas (testadas por raios), as faces no mesmo plano foram juntadas e as peças miúdas (as miras, o seletor, os
  retens, o gatilho, o quebra-chamas) foram simplificadas: de 21 mil para 14 mil triângulos. Escala 0,12.
- **Espingarda (870):** o cano comprido de faixa ventilada, o receptor grafite, o carregador tubular, a telha e a
  coronha em polímero preto (eram de nogueira) com a soleira de borracha; o ferrolho de aço aparece na janela e anda com
  a telha. O cartucho da recarga é o do modelo (plástico vermelho-escuro e base de latão), no lugar do antigo. Tamanho
  real (a antiga também era).
- **Sniper (Mosin-Nagant):** o cano comprido, o receptor e o ferrolho grafite, a coronha em polímero preto, a caixa do
  carregador como a peça que sai na recarga e, modeladas aqui:
  - **a luneta** ("scout", de olho longe: baixa e para a frente, como a antiga): o corpo, as campânulas da objetiva e
    da ocular, a torre de elevação em cima e a de deriva à direita (com as tampas pontilhadas), a do foco à esquerda, o
    anel de aumento, a ocular de borracha, as lentes (vidro escuro com reflexo, sem emissão), os dois anéis com os
    parafusos e o trilho. Fica à frente do receptor porque o ferrolho corre por cima dele inteiro;
  - **a empunhadura de pistola** pontilhada embaixo do punho da coronha: as animações seguram um cabo (como o do rifle),
    e a Mosin tem o punho reto, bem acima da mão.

## Como entra no jogo

- **Unity:** `Editor/Armas/ArmasNovas.cs` (menu **Zombie City Cleaner > Armas > Pôr as armas novas**): importa as
  texturas e o FBX, faz o material (em `Art/Armas/Resources/MateriaisProprios/<Arma>.mat`, que o
  `Itens.MaterialProprio` carrega pelo nome do modelo) e troca:
  - nos braços de primeira pessoa: a malha dos braços com a arma nova no lugar da antiga, cada peça presa ao osso dela
    (`Malhas/FP_Bracos_<Arma>`), e o material no submesh dela;
  - no corpo: as peças `P_<Arma>_<peça>` com as malhas novas (`Malhas/TP_<Arma>_<peça>`), encaixadas onde as antigas
    estavam;
  - a arma solta: `Malhas/Arma_<Arma>` (as peças juntas, sem o cartucho da espingarda).
  O encaixe é medido na arma antiga (a boca do cano e o cabo ficam onde estavam; as mãos das animações seguram igual).
  A "Montar armas" e a montagem do prefab chamam o mesmo: refazer não volta as antigas.
- **Os ganchos** (no lugar dos da pistola enferrujada, de outra sessão): `MontagemDasArmas.Preparar` chama
  `ArmasNovas.PrepararTodas()`, a `MontarMalha` usa a arma nova se o modelo tem uma, e a `MontagemDaRede` chama
  `ArmasNovas.NosBracos` e `ArmasNovas.NoCorpo`.
- **A pistola enferrujada** (feita hoje por outra sessão, não commitada) saiu do `Resources` (o material dela tinha o
  mesmo nome do novo) para `Art/Characters/Jogador/PistolaEnferrujada/`, sem apagar nada. A `MontagemDaPistola.cs` dela
  continua no projeto, mas não é mais chamada. Tudo está também no backup.

## Arquivos

**Novos**
- `Blender/Armas/`: `armas.py` (o caminho comum: a biblioteca de materiais, construir, tirar o escondido, UV, assar,
  exportar, as fotos, o ícone e as referências das mãos), `arma_pistola.py`, `arma_rifle.py`, `arma_espingarda.py`,
  `arma_sniper.py` (a configuração de cada uma), `rodar.py` (as etapas), os editáveis `Pistola.blend`, `Rifle.blend`,
  `Espingarda.blend` e `Sniper.blend` (com as mãos de primeira pessoa como referência, na coleção "Referências") e
  `Fontes/` (os 4 modelos do Pedro, sem mudança).
- `Assets/_Game/Art/Armas/`: `Modelos/<Arma>.fbx`, `Texturas/<Arma>_Cor|MS|Normal.png`,
  `Resources/MateriaisProprios/<Arma>.mat`.
- `Assets/_Game/Scripts/Editor/Armas/ArmasNovas.cs`.
- `Docs/Armas/`: este relatório, `capturas/` (as fotos de cada arma, as do jogo e as de antes) e `rascunho/` (as
  conferências com as mãos).
- `Backups/Armas_antes_do_visual_novo_2026-10-29/`: os FBX das armas antigas, as malhas, a pistola enferrujada (FBX,
  texturas, material, Blender), os ícones, o `Jogador.prefab` e os scripts, como estavam antes.

**Modificados**
- `Assets/_Game/Prefabs/Jogador.prefab`: as malhas e o material das armas nos braços e nos corpos.
- `Assets/_Game/Art/Characters/Jogador/Malhas/`: `Arma_*`, `FP_Bracos_*` e `TP_*` das 4 armas.
- `Assets/_Game/Art/UI/Icones/`: `Icone_` e `Foto_` das 4 armas (nos mesmos tamanhos de antes).
- `Assets/_Game/Scripts/Editor/MontagemDasArmas.cs` e `MontagemDaRede.cs`: os ganchos (acima).
- `Assets/_Game/Scripts/Combat/ParedeDeArmas.cs`: o `_paleta` (da outra sessão) não é mais serializado. No editor, ao
  entrar no Play, a Unity o guardava como uma lista vazia, o índice estourava e o painel de armas do bunker não aparecia
  (com qualquer arma, antiga ou nova).

## Como refazer

1. Num Blender em segundo plano, para cada arma:
   `blender -b --factory-startup --disable-autoexec --python Blender/Armas/rodar.py -- <pistola|rifle|espingarda|sniper> tudo`
   (as etapas: `construir`, `assar`, `exportar`, `fotos`, `imagens`; `rascunho` mostra as superfícies em cores com as
   mãos). ~4 min por arma, na CPU.
2. Na Unity: **Zombie City Cleaner > Armas > Pôr as armas novas**.

## Testes

**Feitos (Unity 6000.6.5, Editor, Demo_Cidade, anfitrião local, 1 jogador)**
- Compila sem erros; o Console fica sem erros no Play.
- Primeira pessoa: as 4 na mão (`capturas/jogo_<arma>.png`), com as mãos no cabo e no guarda-mão (ou na telha); na
  prévia, a pistola atirando (o ferrolho recua) e recarregando (o carregador sai); a espingarda recarregando (a telha
  anda junto).
- Terceira pessoa: as 4 nas mãos do corpo, dos dois lados (`capturas/jogo_terceira_<arma>_*.png`), e as armas soltas
  presas na mochila (a M4 e a sniper).
- Os ícones e as fotos novas no inventário (a barra de atalhos).

**Não testados**
- O tiro e a recarga com o mouse e o teclado de verdade (as animações foram tocadas direto no Animator), o multijogador
  com 2 ou mais jogadores, a arma no chão e o painel do bunker de perto, a luneta mirando.

## Limites

- **O ferrolho da sniper:** na animação do tiro, a mão direita vai aonde o ferrolho estava na sniper antiga (atrás da
  mão); na Mosin a alavanca fica à frente do gatilho, e a mão puxa uns 20 cm atrás dela. Acertar isso pede uma
  animação nova (fora deste trabalho).
- O encaixe da espingarda e da sniper avisa 7 a 15 mm (a arma antiga dos braços não é igual à solta); nas fotos as mãos
  ficam no lugar certo.
- A licença dos 4 modelos de base não veio nos arquivos: confirme que permite o uso no jogo (e se pede crédito).
- 12 texturas de 2048 (~5 MB cada na memória de vídeo). Se pesar, a pistola cabe em 1024 (`TAMANHO` no `arma_pistola.py`
  e o `tamanho` dela no `ArmasNovas`).
