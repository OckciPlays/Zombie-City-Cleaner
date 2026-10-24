# Scripts do Blender (sniper, mochilas, munição, carro e animações do desmembramento)

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
3. Carregue `zumbi_extras.py` e chame `construir(modelo, bpy.data.objects["Antigo_Rig"])` para `Zombie1`,
   `ZombieHomem2`, `ZombieMulher` e `ZombieMulherLoira`.
4. Na Unity, rode Zombie City Cleaner > Montar zumbis.

## O carro

- `carro.py`: o carro que se dirige (uma perua dos anos 90, mostarda), em blocos de cor da `Paleta.png`, com a carroceria
  (e o interior), as 4 portas (a origem na dobradiça), a roda e o volante como peças à parte, e os vidros num material
  próprio. As medidas são escritas nos eixos da Unity; o formato do FBX é o do `Itens.fbx`.
- Como rodar: carregue `carro.py` e chame `construir()`, `exportar()` (grava `Assets/_Game/Art/Veiculos/Carro.fbx`) e,
  se quiser conferir, `foto(caminho)`. Depois, na Unity, rode Zombie City Cleaner > Montar veículos. Ao mudar uma medida
  (portas, rodas, bancos), mude também a `MontagemDosVeiculos`.
