# Zombie City Cleaner (Unity)

Jogo co-op em primeira pessoa de limpar cidades de zumbis, com visual de PS1. A arte (Blender, referências) fica em `C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner`, que tem o `CLAUDE.md` com o conceito do jogo.

## Projeto

- Unity 6000.6, URP 17.6 (renderer Forward+), apenas o Input System novo (`activeInputHandler = 1`) e AI Navigation 2.0.
- O Play entra com Domain Reload e Scene Reload desligados. Por isso:
  - scripts `[ExecuteAlways]` não recebem `Awake` de novo ao dar Play;
  - estado `static` sobrevive entre sessões de Play, então inscreva e desinscreva eventos estáticos em `OnEnable`/`OnDisable`.
- Não recompile scripts com o Play rodando: os campos não serializados zeram.

## Protótipo (`Assets/Prototype/`, cena `Assets/Scenes/Prototype_Galpao.unity`)

- A cena é gerada pelo menu **Zombie City Cleaner > Recriar cena de protótipo** (`Scripts/Editor/PrototypeSceneBuilder.cs`). Ajustes de layout, luzes, texturas e posição da arma devem ser feitos no construtor, não à mão na cena. `PrototypeSceneBuilder.Build()` faz o mesmo sem perguntar nada.
  - O construtor também gera as texturas 64x64 em `Textures/` (por código, com filtro Point) e os materiais em `Materials/`.
- Visual de PS1:
  - `Shaders/PS1Lit.shader`: vértices presos à grade de pixels (`_VertexSnap`), UV afim (`_Affine`), cor de 15 bits com dithering, neblina, alpha clip em 0,5 e luz por pixel (Forward+ via `LIGHT_LOOP_BEGIN`). A emissão é multiplicada pela textura, e a luz ambiente vem do global `_PS1Ambient` (componente `PS1Atmosphere`).
  - `PS1Screen`: a câmera renderiza numa RenderTexture de 240 linhas, mostrada esticada com filtro Point.
  - `ProceduralBox`: o cenário é feito de caixas subdivididas em células de 1 m, para a distorção afim ficar pequena. A malha é gerada em tempo de execução (`DontSave`), e o NavMesh usa os BoxColliders.
- Jogador: `FirstPersonController` (WASD, Shift, Espaço, mouse; começa pausado até o primeiro clique; ESC pausa), `PlayerHealth`, `Pistol` (raycast do centro da tela, 12 balas, R recarrega) e `Flashlight` (F).
- Zumbis: o prefab `Prefabs/Zombie_Prototype.prefab` é uma variante de `Assets/Characters/ZombiePS1/ZombiePS1.prefab` com escala 1,8 e `ZombieController`.
  - O `ZombieController` cria as hitboxes nos ossos no `Awake` (triggers com `Hitbox`): uma esfera na cabeça e cápsulas no corpo. Ele cria o `NavMeshAgent` no `Start`.
  - Tiro na cabeça mata. Tiro no corpo só faz o zumbi cambalear (`bodyHitsToKill = 0`).
  - O zumbi percebe o jogador pela visão (14 m, cone de 140°), pela proximidade (3,5 m) e pelo barulho de tiro (12 m).
- `PrototypeGame`: gera o NavMesh no `Awake` (ordem de execução -500, antes dos zumbis), conta a cota e o tempo, congela o tempo na pausa e desenha o HUD com OnGUI. R reinicia depois de morrer ou de limpar a área.
- O corpo sólido do zumbi fica na layer 2 (Ignore Raycast), e os tiros ignoram essa layer, então os raycasts acertam as hitboxes.
- Os sons são gerados por código em `Effects.cs`, porque o projeto ainda não tem arquivos de áudio.
