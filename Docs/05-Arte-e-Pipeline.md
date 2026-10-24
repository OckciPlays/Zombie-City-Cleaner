# 05 – Arte e pipeline

## Onde fica a arte

**Os arquivos de arte não estão neste repositório.** O repositório tem só o que a Unity usa (os FBX exportados, as texturas, a paleta e os sons). Os arquivos de trabalho do Blender, os scripts que geram os modelos, as imagens de referência e os renders ficam numa pasta à parte, no computador do autor:

```text
Projeto Zombie City Cleaner/
  Plano_Demo.md           o plano original (resumido em Docs/02)
  CLAUDE.md               as convenções da arte, em detalhe
  Imgs/                   imagens de referência; Renders/ tem os renders de conferência
  Sons/                   SomZombie1.mp3 (a voz do zumbi); Previas/ tem os tiros gerados em WAV
  Modelos 3D/             os 4 zumbis como vieram do gerador (Tripo), sem modificações
  Modelo 3D/
    Zumbis/               Zumbis.blend, zumbis.py e os FBX dos 4 zumbis
    Jogador/              Jogador.blend, armas.py, armas_anim.py, jogador_tp.py e os FBX do jogador e das armas
    Cidade/               Cidade.blend, cidade_kit.py e os scripts de cada parte da cidade, e os FBX
    NovoZombie_Anim/      o zumbi antigo (fonte do esqueleto e das animações dos zumbis atuais)
```

Para mexer na arte, peça essa pasta ao autor (ou combine de colocá-la num repositório também).

## Ferramentas

- **Blender 5.2.**
- **Quase tudo é gerado por script Python**, rodado dentro do Blender: os modelos da cidade, as armas, os braços, as animações de primeira pessoa e de arma longa, e a montagem dos zumbis (esqueleto, pesos e cortes). Para mudar um modelo, muda-se o script e roda de novo; os objetos com o mesmo nome são trocados.
- O jeito de rodar está no começo de cada script. Por exemplo, com o `Cidade.blend` aberto:

  ```python
  ns = {}
  exec(open(r"...\Modelo 3D\Cidade\cidade_kit.py", encoding="utf-8").read(), ns)
  exec(open(r"...\Modelo 3D\Cidade\cidade_rua.py", encoding="utf-8").read(), ns)
  ns["montar_rua"]()     # refaz a coleção "Rua"
  ns["exportar_rua"]()   # grava o Cidade_Rua.fbx
  ```

## Convenções que valem para tudo

- **Metros.** Na cidade, a origem de cada peça fica no chão, no centro; nas armas, no meio do cabo; nos braços de primeira pessoa, no olho.
- **A frente olha para -Y no Blender**, que vira **+Z na Unity**. O +X do Blender vira -X na Unity.
- **Sombreamento plano**, sem suavizar normais.
- **Cores pela paleta** (`Paleta.png`, 16 × 16): cada face aponta o UV para o quadradinho da cor dela. As cores ficam num dicionário (`CORES`) no `cidade_kit.py`, que também grava a imagem. Veja a [tabela das linhas](01-Visao-do-Jogo.md#a-paleta).
- **Export FBX:** escala com `FBX_SCALE_ALL`, `axis_forward='-Z'`, `axis_up='Y'`, sem leaf bones e sem suavizar.
  - **O jogador e as armas:** as animações saem como faixas de NLA (uma faixa por clipe), com "NLA strips" ligado e "All actions" desligado, sem embutir a textura (os nomes dos ossos dos braços se repetem entre esqueletos, e "All actions" misturaria as animações).
  - **Os zumbis:** saem com todas as ações ("All actions" ligado, sem NLA) e a textura embutida.
  - **A cidade:** só as malhas, com a conversão de eixos assada na malha (`bake_space_transform`).
- **Animações a 24 fps**, sem root motion. As de laço repetem o primeiro quadro no último.

## Os modelos

### Os zumbis (`Modelo 3D/Zumbis/`)

- 4 modelos cartoon gerados no Tripo, com textura de 2048 px e ~3.100 vértices cada.
- O `zumbis.py` monta cada um a partir da cópia original:
  - um esqueleto de 22 ossos (`root > hips > spine > chest > neck > head`, braços e pernas `.L`/`.R`), com as juntas nas medidas de cada modelo;
  - pesos calculados por regras para cada peça da malha (rosto, cabelo, camiseta, braços, calça, sapatos);
  - o corte do pescoço com a tampa de carne (para a cabeça explodir);
  - as 5 animações do zumbi antigo (Idle, Walk, Chase, Attack e HitReact), com a palma da mão para baixo quando o braço está levantado.
- **Na Unity:** as hitboxes e o corpo com física ficam presos aos ossos e usam o nome deles (`head`, `upper_arm.L`…). Ao refazer o esqueleto, mantenha os nomes.

### O jogador e as armas (`Modelo 3D/Jogador/`)

- **O corpo** (1,80 m): caixas com chanfro pequeno, cada uma presa a um osso. As mãos usam luva.
- **As armas** (`armas.py`): pistola, rifle, submetralhadora e espingarda, desenhadas como perfis de lado extrudados, com as cores da linha 12 da paleta. O "espaço da arma": o cano em -Y, o topo em +Z e a origem no meio do cabo, onde a mão direita segura.
- **Primeira pessoa:** cada arma tem o seu esqueleto e os seus braços (`FP_Rig_<Arma>` e `FP_Bracos_<Arma>`), com a origem no olho.
  - A arma fica quase paralela ao olhar, para o cano apontar para a mira.
  - A mão direita fecha no cabo. Nas armas longas, a esquerda pega o guarda-mão por baixo, com os dedos subindo pelo lado e as pontas aparecendo em cima.
- **As animações de primeira pessoa** (`armas_anim.py`): parado, andando, correndo, tiro, gatilho sem bala, recarga e sacar. Cada clipe descreve o movimento da arma e de onde vai a mão esquerda; os braços saem por IK a cada quadro.
- **As animações de arma longa do corpo** (`jogador_tp.py`): as pernas vêm das animações da pistola, o tronco gira 30° para a direita e a arma fica numa pose fixa em relação ao peito, com os braços por IK.

### A sniper e as animações do desmembramento (`Blender/`, no repositório)

Feitas depois, num PC sem a pasta de arte: os scripts ficam no próprio repositório, em `Blender/`, e partem dos FBX que já estão em `Assets/` (o README de lá tem o passo a passo).

- **A sniper** (`sniper_modelo.py`, `sniper_tiro.py`, `sniper_construir.py`): o modelo no espaço da arma do rifle, os braços do rifle com a sniper no lugar e a animação do tiro com o ferrolho. Sai `Jogador_FP_Sniper.fbx` e `Armas_Sniper.fbx`.
- **Os zumbis** (`zumbi_extras.py`): cair, rastejar e mancar para cada modelo, num `<modelo>_Extra.fbx`. Cair e rastejar vêm do zumbi antigo da Parte 6, tirado do histórico do git.
- **O export que reproduz os FBX do projeto:** escala "FBX_SCALE_ALL", sem ossos-folha e um take por faixa de NLA. Uma ida e volta de um FBX do projeto por esse caminho não muda nada (conferido). Um FBX com um nó só no topo perde esse nó na Unity (ele vira a raiz), e os caminhos dos ossos nos clipes mudam: por isso os `_Extra` levam a malha junto.

### A cidade (`Modelo 3D/Cidade/`)

- `cidade_kit.py`: o kit de modelagem (caixas, cilindros, tubos, perfis), a paleta e o export.
- `cidade_rua.py` (objetos de rua), `cidade_casas.py` (as 5 casas), `cidade_predio.py` (o prédio), `cidade_bunker.py` (o bunker), `cidade_moveis.py` (os móveis e o suporte de armas) e `cidade_chao.py` (o chão em 25 pedaços).
- **As casas, o prédio e o bunker usam a mesma planta da montagem da cidade na Unity** (`MontagemDaCidade`). Ao mudar uma planta, mude nos dois lugares.
- Cada construção sai com uma malha de colisão à parte (`_Colisao`), só com o que é sólido.

## Do Blender para a Unity

| O que mudou | Para onde copiar | O que rodar na Unity |
|---|---|---|
| Um zumbi (`Modelo 3D/Zumbis/<Modelo>.fbx` + a textura de `Texturas/`) | `Assets/_Game/Art/Characters/Zombie/` | Montar zumbis |
| O jogador ou as armas (`Jogador.fbx`, `Jogador_FP_<Arma>.fbx`, `Armas.fbx`, `Armas_Sniper.fbx`) | `Assets/_Game/Art/Characters/Jogador/` | Montar armas |
| As animações do desmembramento (`<Modelo>_Extra.fbx`, pelos scripts de `Blender/`) | `Assets/_Game/Art/Characters/Zombie/` (o script já salva lá) | Montar zumbis |
| A cidade (`Cidade_*.fbx`) | `Assets/_Game/Art/Cidade/` | Montar cidade em blocos |
| A paleta (`Modelo 3D/Jogador/Paleta.png`) | `Assets/_Game/Art/Paleta.png` | — (a Unity reimporta sozinha) |

Os scripts de montagem acertam a importação (escala, clipes, materiais) e refazem os prefabs e a cena. Não é preciso configurar nada à mão.
