# Zombie City Cleaner (Unity)

Jogo de sobrevivência online (até 4 jogadores, a família) numa cidade infestada de zumbis, em low-poly com cores sólidas e clima sombrio no estilo Silent Hill.

- A arte (Blender, referências, sons originais) fica em `C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner`. O `CLAUDE.md` de lá tem o conceito do jogo.
- O plano, dividido em partes, está no `Plano_Demo.md` da mesma pasta. Siga a ordem dele e só avance com a parte anterior aprovada.

## Branches

- `feature/demo-cidade` (esta): a demo da cidade, começada do zero.
  - Parte 0 (fundação), Parte 1 (o zumbi) e Parte 2 (o jogador e a pistola) estão feitas.
  - O próximo passo é a Parte 3 (o online base).
- `feature/prototipo-fps-zumbis`: o protótipo antigo, com visual de PS1 e elevador infinito. Fica só como referência; nada dele é usado aqui.

## Projeto

- **Versões:** Unity 6000.6, URP 17.6 (renderer Forward+), apenas o Input System novo e AI Navigation 2.0.
- **Rede:** Netcode for GameObjects 2.13, `com.unity.services.multiplayer` 2.4 (a partida, o código de entrada e o Relay) e Multiplayer Play Mode 3.0 (vários jogadores no Editor).
  - O projeto ainda precisa ser ligado a um projeto na Unity Cloud. O usuário faz isso no Editor, com a conta dele.
  - Topologia: um jogador é o anfitrião e roda os zumbis, o saque e o mundo. Nenhum código de jogo pode depender de "o jogador" único ou de `Camera.main`; tudo usa a lista de jogadores.
- **Play sem Domain Reload e sem Scene Reload.** Por isso:
  - estado `static` sobrevive entre sessões de Play, então inscreva e desinscreva eventos estáticos em `OnEnable`/`OnDisable`;
  - scripts `[ExecuteAlways]` não recebem `Awake` de novo ao dar Play;
  - para tempos guardados em `static`, use `Time.realtimeSinceStartup`.
- **Não recompile scripts com o Play rodando:** os campos não serializados zeram.
- **URP (`Assets/Settings/PC_RPAsset`):** gradação de cor em HDR, sombras até 40 m com 2 cascatas e HDR ligado.

## Pastas

- `Assets/_Game/` é tudo da demo:
  - `Scenes/Demo_Cidade.unity`: a cena principal, a única nas Build Settings.
  - `Settings/Demo_Volume.asset`: o pós-processamento (ACES, cores dessaturadas, vinheta, granulado, bloom).
  - `Art/Characters/Zombie/`: o zumbi.
  - `Art/Characters/Jogador/`: o jogador (corpo, braços de primeira pessoa e pistola).
  - `Art/Materials/`: materiais de teste do cenário (`Teste_*`).
- Os scripts vão para `Assets/_Game/Scripts/`, separados por sistema (Network, Player, Zombies, Combat, Loot, World, Atmosphere, UI).

## Cena `Demo_Cidade`

- **Noite:**
  - neblina exponencial ao quadrado (cor `#353D45`, densidade 0,05) e fundo da câmera na mesma cor;
  - luz ambiente Trilight azulada e "Luar", uma luz direcional fria e fraca com sombra suave.
- **"Teste (temporário)":** uma rua, calçadas, casas em caixas, postes com luz de sódio, três zumbis de teste (Idle, Walk, Chase) e o jogador de terceira pessoa mirando. Tudo isso será trocado pela cidade em blocos da Parte 4.
- **"Câmera de teste":** a câmera provisória, com pós-processamento e SMAA, até existir o jogador de rede. Os braços de primeira pessoa estão presos nela.

## O zumbi (`Art/Characters/Zombie/`)

- **Arquivos:**
  - `NovoZombie.fbx`, exportado de `Modelo 3D/NovoZombie_Anim/` na pasta de arte. Ao atualizar o modelo, copie o FBX de novo.
  - `NovoZombie_Gibs.fbx`, com os pedaços soltos.
  - A textura `NovoZombie_basecolor.jpg`.
- **Importação:**
  - mede 1,75 m na escala 1 e olha para +Z;
  - deixe **"Bake Axis Conversion" desligado**, porque ligado ele vira o zumbi de costas. Os filhos do FBX ficam com rotação de 270° em X, o que é normal;
  - rig Generic, com avatar criado do próprio modelo;
  - os clipes `Idle` (3 s), `Walk` (1,33 s) e `Chase` (1 s) estão em loop, sem root motion.
- **`Zombie.controller`:** uma árvore de blend 1D pelo float `Speed`, em m/s. As animações batem com o deslocamento nestes valores: `Idle` em 0, `Walk` em 0,6 e `Chase` em 1,12.
- **Materiais:**
  - `Materials/Zombie.mat`: URP Lit com a textura;
  - `Materials/Zombie_Flesh.mat`: a carne das tampas, cor sólida `#6B0F0F`.
- **Desmembramento:** a malha tem 11 submeshes, um por material do Blender, nesta ordem na Unity:
  - 0 `Zombie_Body`, 1 `Zombie_Forearm.R`, 2 `Zombie_Head`, 3 `Zombie_Forearm.L`, 4 `Zombie_UpperArm.R`, 5 `Zombie_UpperArm.L`;
  - 6 a 10: as tampas `Cap_Neck`, `Cap_Elbow.L`, `Cap_Shoulder.L`, `Cap_Elbow.R` e `Cap_Shoulder.R`.
  - Os 6 primeiros são as regiões que podem cair. As tampas tampam o buraco que fica quando uma região sai.
  - Com o zumbi inteiro, o renderer recebe só 6 materiais, e os submeshes sem material (as tampas) não são desenhados.
  - Na Parte 6, um script de editor vai gerar as variantes da malha, sem a região perdida e com a tampa dela, montadas pelo nome do submesh.
- **Pedaços soltos (`NovoZombie_Gibs.fbx`):** `Gib_Arm.L/R` (o braço inteiro) e `Gib_Forearm.L/R`. São malhas estáticas na pose T, com a origem no ponto do corte e uma tampa de carne na ponta.

## O jogador (`Art/Characters/Jogador/`)

- **Arquivos:** exportados de `Modelo 3D/Jogador/Jogador.blend` na pasta de arte. Ao atualizar, copie os FBX e a `Paleta.png` de novo.
  - `Jogador.fbx`: o corpo, visto pelos outros jogadores.
  - `Jogador_FP.fbx`: os braços com a pistola, na primeira pessoa.
  - `Pistola.fbx`: a pistola avulsa, estática, para uso futuro (no chão, para pegar).
  - `Paleta.png`: a textura de cores, 16×16, com filtro Point, sem mipmaps e sem compressão.
- **Cores pela paleta:** o personagem é feito de blocos de cor sólida, e o UV de cada face aponta para um quadradinho da `Paleta.png`.
  - As linhas 0 a 3 são as 4 variações da família (pele, cabelo, jaqueta, camisa, calça, sapato, cinto, escuro e jaqueta escura).
  - `Materials/Jogador_Variacao0..3`: os UVs apontam para a linha 0, e cada material desloca a textura em `k/16` no V para mostrar a linha `k`.
  - A pistola usa a linha 4 com o material `Materials/Pistola` (sem deslocamento). Por isso ela tem material próprio, inclusive dentro de `FP_Bracos`, que tem dois submeshes.
- **Corpo (`Jogador.fbx`):**
  - 1,80 m de altura, olhando para +Z, com "Bake Axis Conversion" desligado como no zumbi;
  - rig Generic, com os ossos do zumbi mais `fingers` e `thumb` em cada mão e `weapon.R` (filho de `hand.R`);
  - a pistola de terceira pessoa (`Pistola_TP`) é um SkinnedMeshRenderer separado, preso inteiro ao `weapon.R`; dá para desligá-lo;
  - os clipes são `Idle` (2 s), `Walk` (0,75 s, ~1,33 m/s), `Run` (0,58 s, ~3,33 m/s) e `Aim` (2 s), em loop, mais `Fire` (0,25 s). Idle, Walk e Run seguram a pistola baixa com as duas mãos.
- **`Jogador_TP.controller`:**
  - a camada base é uma árvore pelo float `Speed` (0, 1,33 e 3,33);
  - a camada "Mira" usa a máscara `Jogador_Tronco.mask` (da `spine` para cima): `Aiming` (bool) leva ao `Aim`, e `Fire` (trigger) toca o tiro.
- **Primeira pessoa (`Jogador_FP.fbx`):**
  - a origem é o olho. Ponha o modelo como filho da câmera, com posição e rotação zero;
  - foi enquadrado para um FOV de ~55°. Na Parte 3 ele vai ter uma câmera própria, para não atravessar paredes;
  - ossos: `upper_arm/forearm/hand/fingers/thumb` de cada lado, mais `gun` (a arma), `slide` (o ferrolho) e `mag` (o carregador);
  - os ombros ficam fora da tela, abaixo e um pouco atrás do olho. Os braços são 12% mais compridos e as mãos 12% maiores que no corpo, um truque de primeira pessoa;
  - os clipes são `Idle`, `Walk` e `Run` (a arma baixa e inclinada), em loop, mais `Fire` (coice e ferrolho), `DryFire` (gatilho sem bala) e `Reload` (1,67 s: o carregador cai, a mão esquerda traz outro e puxa o ferrolho).
- **`Jogador_FP.controller`:** a base é uma árvore pelo `Speed`. A camada "Ações" tem os triggers `Fire`, `DryFire` e `Reload`, e cada um volta sozinho ao estado vazio.
- **Write Defaults desligado nos Animators:** está desligado em todos os estados (do jogador e do zumbi). Com ele ligado, o estado vazio de uma camada de cima jogaria o personagem na pose T.
- **Lanterna:** colada na câmera, ela estoura a pistola de luz. Na Parte 3, ela precisa ficar à frente do cano ou não iluminar a camada dos braços.
