# Os animais do Tripo, com o rig e as ações (14/11/2026)

Os 24 animais feitos no Tripo, montados no Blender pelo `Docs/20-Fauna-Modelos-e-Animacoes.md` (da develop):
o tamanho de verdade, olhando para -Y (a convenção dos cervos do `Blender/Fauna`), o esqueleto com os nomes do Docs/20
e as ações de cada espécie. **Na Unity desde 14/11/2026** (a pedido do Pedro: "pode ir para a unity"): o
`exportar_unity.py` exporta cada bicho para `Assets/_Game/Art/Fauna/Tripo/` (o FBX com as ações como takes, a textura e o
`Medidas.json`), e o menu da Unity **Zombie City Cleaner > Fauna > Montar os animais do Tripo** monta os prefabs. A
animação no jogo ainda é a do código; as ações daqui entram na fase 2. O passo a passo e o que foi testado estão no
`Docs/20-Fauna-Modelos-e-Animacoes.md` (seção 6) e no `Docs/19-Fauna.md`.

- `exportar_unity.py`: `blender -b --factory-startup Animais.blend --python exportar_unity.py -- <saída> [<Nome> ...]`
  (com a Unity fechada; não salva o `.blend`).

- `Animais.blend`: os 24 bichos em fileiras, com 442 ações (cada uma uma faixa de NLA muda; a ativa é a de parado). Para
  ver uma ação, selecione o esqueleto e escolha a ação no Action Editor. As texturas ficam em `Texturas/` (caminhos
  relativos ao `.blend`).
- `Animais.md`: o que foi feito, o tamanho de cada bicho, o esqueleto, as ações de cada grupo, o que ficou aproximado e
  como refazer.
- `Acoes/<Nome>_<n>.jpg`: as folhas de conferência (cinco ações por folha, 3/4 em cima e de lado embaixo).
- `Fontes/`: os zips do Tripo, como vieram.
- Os scripts (`animais.py` monta, `fotos.py` faz as folhas, `juntar.py` junta os grupos num `.blend` só; o resto é o kit,
  os esqueletos, os pontos das juntas de cada bicho, o passo e as ações). Rodam num Blender sem janela:
  `blender -b --factory-startup --python animais.py -- <saida.blend> <Nomes|todos>`.

O original fica na pasta de arte (`Modelo 3D/Animais/`, fora deste repositório) e isto é uma cópia. Os scripts usam os
caminhos de lá (`RAIZ` no `animais_kit.py` e `AQUI` no `animais.py`) e os FBX descompactados em `Animais 3d/<Nome>/`:
para rodar em outro PC, descompacte os zips de `Fontes/` numa pasta assim e mude os dois caminhos.
