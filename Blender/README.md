# Scripts do Blender (sniper, mochilas, munição, sinuca, carro e animações do desmembramento)

Estes scripts geram arte que não veio da pasta de arte do projeto (`Projeto Zombie City Cleaner`): a sniper e as animações
novas dos zumbis. Eles leem os FBX que já estão em `Assets/` e escrevem os FBX novos lá, nos mesmos formatos (a ida e
volta de um FBX do projeto por este caminho não muda nada: conferido nos braços do rifle e nas armas avulsas).
Testado no Blender 5.2.

## A sniper

- `sniper_modelo.py`: o modelo (um fuzil de ferrolho de caça com luneta), em blocos de cor sólida pela `Paleta.png`
  (linha 12, a das armas), no espaço dos braços de primeira pessoa: o cano em -Y, o alto em +Z, a direita da arma em -X e
  o cabo na cabeça do osso `gun`. A empunhadura e o guarda-mão ficam onde o rifle tem os dele, então as mãos das animações
  do rifle seguram a sniper igual.
- `sniper_tiro.py`: a animação do tiro (`Fire`, 30 quadros = 1,25 s, o intervalo entre os tiros): o coice e o ferrolho
  (a mão direita sai do cabo, levanta a alavanca, puxa, empurra, abaixa e volta). As mãos seguem alvos de IK presos à
  arma, e tudo é assado em quadros-chave no fim.
- `sniper_construir.py`: refaz tudo e exporta `Jogador_FP_Sniper.fbx` e `Armas_Sniper.fbx`.

Como rodar:

1. Numa cena chamada "Sniper", importe `Assets/_Game/Art/Characters/Jogador/Jogador_FP_Rifle.fbx`. Numa cena chamada
   "ArmasRef", importe `Armas.fbx` da mesma pasta. Ponha a `Paleta.png` nos materiais para ver as cores (opcional).
2. No console do Blender, carregue `sniper_construir.py` (por exemplo com `importlib`) e chame `construir()`.
3. Na Unity, rode Zombie City Cleaner > Montar armas.

## A pistola nova (29/10/2026)

- `Pistola/pistola.py`: a pistola mais realista (aço gasto e enferrujado, ferrolho serrilhado, miras de três pontos,
  trilho, guarda-mato quadrado, placas do punho e o carregador comprido enrolado em fita), nas mesmas três peças e no
  mesmo espaço da antiga do `Armas.fbx` (o cano em -Y, o alto em +Z, a direita da arma em -X, a origem no cabo). Não usa
  a paleta: tem textura própria, assada de materiais de nós (a cor e o metal/lisura, 1024 x 1024).
- Roda num Blender em segundo plano (~1 min): `blender -b --factory-startup --python Blender/Pistola/pistola.py -- tudo`
  (ou as etapas `construir`, `assar`, `exportar`, `imagens`). Grava o `Pistola.blend`, o
  `Assets/_Game/Art/Characters/Jogador/Armas_Pistola.fbx`, as texturas em `Art/Characters/Jogador/Texturas` e o ícone e a
  foto em `Art/UI/Icones`.
- Na Unity: Zombie City Cleaner > Armas > Montar a pistola nova (troca a antiga nos braços, no corpo e na arma solta).

## As armas novas (29/10/2026)

- `Armas/`: as 4 armas (a pistola, a M4, a espingarda e a sniper) refeitas a partir dos modelos prontos que o Pedro mandou
  (`Armas/Fontes`: Fort-17, um AR-15, Remington 870 e Mosin-Nagant), adaptadas ao jogo: no espaço e nas peças das
  antigas (a boca do cano e o cabo onde as animações seguram), preto e grafite, com os materiais PBR assados num atlas
  por arma. `armas.py` é o caminho comum; `arma_<arma>.py`, a configuração de cada uma (a sniper ganha aqui a luneta e a
  empunhadura). Os editáveis: `<Arma>.blend` (com as mãos de primeira pessoa como referência).
- Num Blender em segundo plano (~4 min por arma): `blender -b --factory-startup --disable-autoexec --python
  Blender/Armas/rodar.py -- <pistola|rifle|espingarda|sniper> tudo` (ou as etapas `construir`, `assar`, `exportar`,
  `fotos`, `imagens`, `rascunho`). Na Unity: Zombie City Cleaner > Armas > Pôr as armas novas. Detalhes em
  `Docs/Armas/README.md`.
- A pistola enferrujada do `Pistola/` (de antes, no mesmo dia) não é mais usada.
- **As estilizadas (v2, 30/10/2026):** o rifle, a espingarda e a sniper no jogo são as próprias antigas melhoradas
  (`estilizadas.py`, `v2_<arma>.py`): a arma antiga dos braços de primeira pessoa, separada pelos ossos, com as quinas
  chanfradas, alguns detalhes e a paleta de cores chapadas (`Art/Armas/Texturas/Armas_Paleta_*`). Num Blender em segundo
  plano: `blender -b --factory-startup --disable-autoexec --python Blender/Armas/rodar_v2.py -- <rifle|espingarda|sniper> tudo`
  (as etapas `construir`, `exportar`, `fotos`, `imagens`, `rascunho`). As v1 realistas delas ficam como estavam.
- **O saque (31/10/2026, `loot.py`):** a comida, a bebida, os remédios, os materiais e os kits (39 itens), as construções
  montadas (a fogueira, a cama e a barricada) e a bancada: `Loot.fbx`, os ícones e as fotos (`blender -b --factory-startup
  --disable-autoexec --python Blender/loot.py -- tudo`). Usa as funções do `equipamentos.py` e as células novas da
  `Paleta.png` (a linha 11, colunas 6 a 15, e a linha 0, colunas 0 a 5).
- **O machado e o bastão em pé (31/10/2026):** a pegada dos dois foi refeita a pedido (em pé à direita da tela, o fio do
  machado para a esquerda; o machado com a mão direita 18 cm acima no cabo, o bastão bem embaixo) e o golpe gira para a
  esquerda (`machado.CHAVES_DO_GOLPE`, que o bastão usa). As fotos de conferência do `faca.py` usam 55° de campo de
  visão, o da câmera dos braços do jogo.
- **O bastão, a lança e a tocha (31/10/2026, `Armas/bastao.py`, `lanca.py`, `tocha.py`):** as armas brancas da fabricação,
  com as etapas do `faca.py` (o bastão e a lança nas duas mãos, com o golpe e a segunda mão do `machado.py`; a tocha na mão
  direita, com a `PONTA` onde a chama nasce).
- **O machado (31/10/2026):** a segunda arma branca (`machado.py -- tudo`, as etapas do `faca.py`), nas duas mãos (a mão
  esquerda no cabo por IK em todos os clipes) e com o clipe `Golpe` próprio (a machadada na diagonal).
- **A faca (31/10/2026):** a primeira arma branca (`faca.py -- tudo`: `construir`, `exportar` (o `Itens_Faca.fbx`),
  `imagens`, `fotos` e `bracos` (o `Jogador_FP_Faca.fbx`, a partir dos punhos); `conferir` faz fotos da câmera de
  primeira pessoa).
- **A submetralhadora v3 (30/10/2026):** a MP5K, toda modelada a partir da imagem (`v3_mp5.py`; `rodar_v2.py -- v3_mp5
  tudo`), e os braços de primeira pessoa com a mão esquerda na empunhadura vertical (`maos_smg.py -- exportar`: parte do
  FBX original, no backup da SMG, e grava o `Jogador_FP_Submetralhadora.fbx` e o `Bracos_Submetralhadora.blend`;
  `-- medir` mostra a pose de segurar; `-- conferir` faz fotos de conferência).
- **O rifle v3 (30/10/2026):** a M4A1 com a alça de transporte, do AR-15 fonte (`v3_m4.py`; `rodar_v2.py -- v3_m4 tudo`):
  as peças da fonte com Decimate e quinas vivas acima de 12°, e a alça, o guarda-mão canelado, a massa de mira, o
  quebra-chamas e a alavanca modelados no espaço do AR-15.
- **A sniper v3 (30/10/2026):** a Mosin fiel com a luneta "scout" da v1 (`v3_mosin.py`; `rodar_v2.py -- v3_mosin tudo`; a
  versão leve que vai no jogo, com 4.219 triângulos: `v3_mosin_leve.py`, `rodar_v2.py -- v3_mosin_leve tudo`) e os
  braços de primeira pessoa com a mão direita no punho dela (`maos_sniper.py -- exportar`: parte do FBX original, no
  backup das armas, e grava o `Jogador_FP_Sniper.fbx` e o `Bracos_Sniper.blend`; `-- conferir` faz fotos de conferência).

## As mochilas e o coldre

- `equipamentos.py`: as 3 mochilas (a pequena, de lona verde-oliva; a média, militar, cáqui com tiras de couro; a grande,
  de trilha, de nylon cinza-azulado, com o saco de dormir) e o coldre de couro, em blocos de cor pela `Paleta.png` (só cores
  que já existem nela; o alfa da paleta é ignorado, porque na cidade ele é o brilho).
  - O formato é o do `Itens.fbx`: cada peça é um objeto girado 90° em X, com a malha nos eixos da Unity (o alto em +Y).
  - Mochilas: a origem no meio das costas de quem veste; o corpo vai para trás (-Z na Unity). Coldre: a origem no passador
    do cinto.
- Faz também os ícones (`Icone_<nome>.png`, 128 × 128) e as fotos (`Foto_<nome>.png`, 600 × 500) do inventário, no estilo
  dos do `itens.py` (o Workbench com a textura, a cavidade, o contorno e o fundo transparente).

Como rodar: em qualquer cena, carregue `equipamentos.py` e chame `construir()`, `exportar()` (grava
`Assets/_Game/Art/Characters/Jogador/Equipamentos.fbx`) e `imagens()` (grava em `Assets/_Game/Art/UI/Icones`). Depois, na
Unity, rode Zombie City Cleaner > Montar armas.

## As caixas de munição

- `municao.py`: as 4 caixas (9mm, 5,56, calibre 12 e 7,62), abertas, com as balas aparecendo por cima. Usa as peças, as
  cores e a câmera do `equipamentos.py` (as cores novas vão no `CORES` de lá, só cores que já existem na `Paleta.png`).
  A origem fica embaixo, no meio; a tampa aberta, atrás.
- Como rodar: carregue `municao.py` e chame `construir()`, `exportar()` (grava `Municao.fbx` na pasta do jogador) e
  `imagens()` (os ícones de 128 × 128 e as fotos de 600 × 500). Depois, na Unity, rode Zombie City Cleaner > Montar armas.

## A sinuca

- `sinuca.py` (29/10/2026): a caixa das bolas (o estojo de madeira aberto, com as 16 bolas em duas fileiras de 8, e a
  vazia, com os berços) e os 4 tacos (o de madeira, o escuro, o preto e o vermelho: só a parte de trás muda), no formato
  e nas cores do `equipamentos.py` (só cores que já existem na `Paleta.png`). Os tacos ficam deitados ao longo do X, com
  1,45 m.
- Como rodar: carregue `sinuca.py` e chame `construir()`, `exportar()` (grava `Sinuca.fbx` na pasta do jogador) e
  `imagens()` (os ícones, 128 px por célula, a caixa 3 × 1 e os tacos 4 × 1, e as fotos de 600 × 500). Depois, na Unity,
  rode Zombie City Cleaner > Montar armas.

## As animações do desmembramento dos zumbis

- `zumbi_extras.py`: para cada modelo, `<modelo>_Extra.fbx` com 5 clipes: `Cair`, `Rastejar` e `RastejarParado` (do
  zumbi antigo da Parte 6, transferidos osso a osso para o esqueleto de cada modelo) e `MancarE` / `MancarD` (feitos do
  `Walk` do próprio modelo: o corpo cai e tomba para o lado da perna perdida).

Como rodar:

1. Tire o zumbi antigo do histórico do git (ele saiu do projeto nos ajustes antes da Parte 7) para esta pasta:

   ```bash
   git show 12bec8d:Assets/_Game/Art/Characters/Zombie/NovoZombie.fbx | git lfs smudge > Blender/NovoZombie_antigo.fbx
   ```

   (o arquivo fica fora do git: não o commite.)
2. Importe `NovoZombie_antigo.fbx` numa cena e mude o nome do esqueleto dele para `Antigo_Rig`.
3. Carregue `zumbi_extras.py` e chame `construir(modelo, bpy.data.objects["Antigo_Rig"])` para cada modelo da lista
   da montagem: o leve `ZombieGaroto` e, se voltarem, os 6 em blocos (`ZombieBloco_Cuia`, `_Espetado`, `_Comprido`,
   `ZombieBlocoMulher_Longo`, `_Selvagem` e `_Chanel`; a malha que vai junto é a `Zombie_Body`, sem os LODs) e
   `Zombie1`, `ZombieHomem2`, `ZombieMulher` e `ZombieMulherLoira`.
4. Na Unity, rode Zombie City Cleaner > Montar zumbis.

## O carro

- `carro.py`: o carro que se dirige (uma perua dos anos 90, mostarda), em blocos de cor da `Paleta.png`, com a carroceria
  (e o interior), as 4 portas (a origem na dobradiça), a roda e o volante como peças à parte, e os vidros num material
  próprio. As medidas são escritas nos eixos da Unity; o formato do FBX é o do `Itens.fbx`.
- Como rodar: carregue `carro.py` e chame `construir()`, `exportar()` (grava `Assets/_Game/Art/Veiculos/Carro.fbx`) e,
  se quiser conferir, `foto(caminho)`. Depois, na Unity, rode Zombie City Cleaner > Montar veículos. Ao mudar uma medida
  (portas, rodas, bancos), mude também a `MontagemDosVeiculos`.
- `PeruaNova/perua_nova.py` (28/10/2026): o visual novo da mesma perua (mais detalhe e materiais PBR assados), com as
  medidas lidas do `carro.py`. O editável é o `PeruaNova/PeruaNova.blend`.
  - Como rodar: `sys.path.append(r"<repo>\Blender\PeruaNova")`, `import perua_nova` e chame `construir()`,
    `desembrulhar()`, `assar()` (Cycles; ~1 min por canal na CPU: se a conexão do MCP cair, um canal por chamada,
    `assar(canais=("cor",))`...), `compor()`, `exportar()` (grava `Assets/_Game/Art/Veiculos/PeruaNova/PeruaNova.fbx` e
    as texturas em `.../PeruaNova/Texturas`) e `salvar()`. Depois, na Unity, Zombie City Cleaner > Veículos > Aplicar o
    visual novo da perua (também depois de cada "Montar veículos"). Detalhes em `Docs/PeruaNova/README.md`.
- `PeruaNova2/` (29/10/2026): a versão leve da perua (~9.700 triângulos; os detalhes da v1 projetados nas texturas).
  Roda num Blender em segundo plano, uma etapa por processo (`rodar.py`: `construir` a partir do `PeruaNova/PeruaNova.blend`,
  depois `assar <canal>` e `compor_exportar` no `PeruaNova2/PeruaNova2.blend`). Na Unity: Zombie City Cleaner > Veículos >
  Aplicar o visual novo da perua (v2, leve).
- `Cupe/` (29/10/2026): o segundo carro que se dirige, um cupê fastback preto e cinza (~8.200 triângulos; o mesmo caminho
  da perua v2). Num Blender em segundo plano: `rodar.py -- construir`, depois `assar <canal> <A|B>` (um atlas por processo,
  para caber na memória) e `compor_exportar`. Na Unity: Zombie City Cleaner > Veículos > Montar o cupê.
  Detalhes em `Docs/Cupe/README.md`.
