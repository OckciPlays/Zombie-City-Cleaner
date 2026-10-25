# Scripts do Blender (sniper e animações do desmembramento)

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
3. Carregue `zumbi_extras.py` e chame `construir(modelo, bpy.data.objects["Antigo_Rig"])` para cada modelo: o leve
   `ZombieGaroto` (o da lista da montagem) e, se voltarem, `Zombie1`, `ZombieHomem2`, `ZombieMulher` e
   `ZombieMulherLoira`.
4. Na Unity, rode Zombie City Cleaner > Montar zumbis.
