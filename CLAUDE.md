# Zombie City Cleaner (Unity)

Jogo co-op em primeira pessoa de limpar zumbis, com visual de PS1. A arte (Blender, referências, sons originais) fica em `C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner`, que tem o `CLAUDE.md` com o conceito do jogo.

## Conceito atual: o prédio

- Os jogadores começam dentro de um elevador. Ele sobe andar por andar, sem fim por enquanto.
- Cada andar é gerado só quando o elevador está indo para ele: um corredor com apartamentos dos dois lados e ~30 zumbis (+1 por andar, até 45) espalhados pelos cômodos e pelo corredor.
- Com o andar limpo (todos os zumbis mortos), o elevador libera a subida: dentro da cabine, aperte E.
- Existe um andar por vez na memória, para o jogo continuar leve (o teste com 500 zumbis ativos custava ~18 ms de CPU por quadro, quase tudo em física das hitboxes).

## Projeto

- Unity 6000.6, URP 17.6 (renderer Forward+), apenas o Input System novo (`activeInputHandler = 1`) e AI Navigation 2.0.
- O agente Humanoid do NavMesh foi ajustado pelo construtor do prédio para raio 0,35 m e altura 1,8 m, porque os vãos de porta têm 1,1 m.
- O Play entra com Domain Reload e Scene Reload desligados. Por isso:
  - scripts `[ExecuteAlways]` não recebem `Awake` de novo ao dar Play;
  - estado `static` sobrevive entre sessões de Play, então inscreva e desinscreva eventos estáticos em `OnEnable`/`OnDisable`. Para tempos guardados em `static`, use `Time.realtimeSinceStartup`, que não zera entre sessões.
- Não recompile scripts com o Play rodando: os campos não serializados zeram.

## Cenas e construtores (`Assets/Prototype/`)

- As cenas são geradas pelo menu **Zombie City Cleaner**. Ajustes de layout, luzes, texturas e posição da arma devem ser feitos nos construtores (`Scripts/Editor/`), não à mão na cena:
  - **Recriar cena do prédio** (`TowerSceneBuilder`) gera `Assets/Scenes/Prototype_Predio.unity`, que é a cena principal (primeira nas Build Settings);
  - **Recriar cena do galpão** (`PrototypeSceneBuilder`) gera `Assets/Scenes/Prototype_Galpao.unity`, o primeiro protótipo, com 6 zumbis fixos.
- `Ps1Assets` reúne o que os dois construtores compartilham: as texturas 64x64 geradas por código (filtro Point, em `Textures/`), os materiais (`Materials/`), a variante do zumbi, o jogador e a atmosfera.

## Modo prédio

- `Elevator`: a cabine fica parada em x < 0, com a porta em x = 0. Na viagem (portas fechadas, motor e tremor) ela chama `FloorGenerator.Generate`. As portas têm `NavMeshModifier` com ignoreFromBuild, então o zumbi entra na cabine quando elas abrem. O indicador acima da porta fica vermelho com o andar sujo, verde com o andar limpo e laranja em movimento.
- `FloorGenerator` fica na raiz "Prédio", que tem o `NavMeshSurface` (filhos, colisores físicos).
  - A cada andar, ele sorteia o número de apartamentos (3 ou 4 por lado), as larguras e profundidades, as portas, a divisão em sala, quarto e banheiro, os móveis, as luzes (algumas apagadas ou piscando), o sangue e os zumbis. A semente é `seed da partida + andar * 7919`.
  - O andar anterior é desativado e depois destruído, gera o NavMesh e só então cria os zumbis (a mais de 5 m do elevador).
  - Paredes com materiais diferentes em cada face viram duas meias-paredes. O UV das paredes cobre do chão ao teto (`uvScale = 1 / altura`), por isso as texturas de parede têm rodapé e barrado embaixo.
- `TowerGame`: cota por andar, recorde em `PlayerPrefs` ("ZombieCityCleaner.MelhorAndar"), pausa (congela o tempo e o áudio) e HUD. R reinicia depois de morrer.

## Peças comuns

- Visual de PS1:
  - `Shaders/PS1Lit.shader`: vértices presos à grade de pixels (`_VertexSnap`), UV afim (`_Affine`), cor de 15 bits com dithering, neblina, alpha clip em 0,5 e luz por pixel (Forward+ via `LIGHT_LOOP_BEGIN`). A emissão é multiplicada pela textura, e a luz ambiente vem do global `_PS1Ambient` (componente `PS1Atmosphere`).
  - `PS1Screen`: a câmera renderiza numa RenderTexture de 240 linhas, mostrada esticada com filtro Point.
  - `ProceduralBox`: o cenário é feito de caixas subdivididas em células de 1 m, para a distorção afim ficar pequena. A malha é gerada em tempo de execução (`DontSave`). `ProceduralBox.Create` monta uma caixa pronta.
- `Hud`: desenho com OnGUI (mira com hitmarker, vida, munição, mensagens), usado por `PrototypeGame` e `TowerGame`.
- Jogador (montado por `Ps1Assets.BuildPlayer`):
  - `FirstPersonController`: WASD, Shift, Espaço e mouse. Começa pausado até o primeiro clique, e ESC pausa. Tem `Teleport`, `SetLook`, `Pause` e `Resume`. Se o jogador cair 5 m abaixo do último chão firme, ele volta para esse chão.
  - `PlayerHealth`.
  - `WeaponHolder` com dois `Gun`, cada um num filho "Arma ...". As teclas 1/2 ou a rodinha do mouse trocam a arma, e só a arma atual fica ligada.
    - A SCAR é a principal: automática, 30 balas, modelo `Assets/Weapons/Scar/` (vindo de `Modelo 3D/Scar`), com a textura reduzida para 256 px.
    - A pistola, feita de blocos, é a secundária: semiautomática, 12 balas.
  - O `Gun` dispara um raycast do centro da tela, com uma dispersão que cresce ao andar e ao atirar rápido. R recarrega, e `TryFire` é público.
  - `Flashlight` (F). A luz fica à frente do cano e recua quando o jogador encosta numa parede.
  - `NavMeshObstacle` (sem carving): os zumbis desviam do jogador em vez de empurrá-lo.
- As paredes e o piso da cabine têm 0,4 m de espessura, e a laje dos andares tem 1 m. Assim, zumbis espremidos contra o jogador não o empurram através delas.
- Zumbis: o prefab `Prefabs/Zombie_Prototype.prefab` é uma variante de `Assets/Characters/ZombiePS1/ZombiePS1.prefab` com escala 1,8 e `ZombieController`.
  - O `ZombieController` cria as hitboxes nos ossos no `Awake` (triggers com `Hitbox`): uma esfera na cabeça e cápsulas no corpo. Ele cria o `NavMeshAgent` no `Start`. O zumbi corre a 2,2 m/s.
  - A esfera da cabeça usa `headCenter`/`headRadius`, que ficam no espaço do osso `head` e são gravados no prefab:
    - `CreateZombieVariant` os calcula com `ComputeHeadSphere`, a partir dos vértices com peso na cabeça e da bindpose (menu de contexto "Calcular hitbox da cabeça").
    - Não meça a cabeça pela posição dos ossos: a pose padrão do FBX é o 1º quadro do Idle, não a de bind. Era isso que deixava a hitbox deslocada uns 35 cm.
  - Tiro na cabeça mata. Tiro no corpo só faz o zumbi cambalear (`bodyHitsToKill = 0`). O tiro conta como na cabeça se a esfera da cabeça do primeiro zumbi atingido estiver em qualquer ponto do raio, antes de uma parede ou de outro zumbi.
  - O zumbi percebe o jogador pela visão (14 m, cone de 140°), pela proximidade (3,5 m) e pelo barulho de tiro (12 m).
  - A voz é `Audio/SomZombie1.mp3` (o som do usuário): 4 grunhidos, e cada vez toca um trecho sorteado (`voiceSegments`) com variação de tom. O som é importado em mono e descomprimido ao carregar.
  - `Effects.PlayVoice` limita a 6 vozes de zumbi ao mesmo tempo.
- O corpo sólido do zumbi fica na layer 2 (Ignore Raycast), e os tiros ignoram essa layer, então os raycasts acertam as hitboxes.
- Os outros sons (tiro, recarga, carne, elevador) são gerados por código em `Effects.cs`.
