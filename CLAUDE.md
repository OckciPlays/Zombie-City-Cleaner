# Zombie City Cleaner (Unity)

Jogo de sobrevivência online (até 4 jogadores, a família) numa cidade infestada de zumbis, em low-poly com cores sólidas e clima sombrio no estilo Silent Hill.

- A arte (Blender, referências, sons originais) fica em `C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner`. O `CLAUDE.md` de lá tem o conceito do jogo.
- O plano, dividido em partes, está no `Plano_Demo.md` da mesma pasta. Siga a ordem dele e só avance com a parte anterior aprovada.

## Branches

- `feature/demo-cidade` (esta): a demo da cidade, começada do zero.
  - Parte 0 (fundação), Parte 1 (o zumbi), Parte 2 (o jogador e a pistola) e Parte 3 (o online base) estão feitas.
  - O próximo passo é a Parte 4 (a cidade em blocos, o clima e o dia).
- `feature/prototipo-fps-zumbis`: o protótipo antigo, com visual de PS1 e elevador infinito. Fica só como referência; nada dele é usado aqui.

## Projeto

- **Versões:** Unity 6000.6, URP 17.6 (renderer Forward+), apenas o Input System novo e AI Navigation 2.0.
- **Rede:** Netcode for GameObjects 2.13, `com.unity.services.multiplayer` 2.4 (a partida, o código de entrada e o Relay) e Multiplayer Play Mode 3.0 (vários jogadores no Editor).
  - O projeto está ligado à Unity Cloud (projeto "Zombie City Cleaner", id `c6d7d134-66e6-4af9-a402-28aad4b0e691`).
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
- **"Teste (temporário)":** uma rua, calçadas, casas em caixas, postes com luz de sódio, um chão grande e três zumbis de teste (Idle, Walk, Chase). Tudo isso será trocado pela cidade em blocos da Parte 4.
- **"Câmera de teste":** a câmera do menu (veja "Rede e jogador online").

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
- **Lanterna:** não a cole na câmera, porque ela estoura a pistola de luz. Ela fica no peito (veja "Rede e jogador online").

## Rede e jogador online (`Assets/_Game/Scripts/`)

- **Entrar na partida (`Network/SessaoOnline`, no objeto "Rede" junto do NetworkManager e do UnityTransport):**
  - `Hospedar()` cria uma partida privada de até 4 jogadores com `WithRelayNetwork()` e guarda o `Codigo`. `Entrar(codigo)` usa `JoinSessionByCodeAsync`. O pacote de multiplayer liga o Netcode sozinho (o anfitrião vira host).
  - Antes, os serviços da Unity são iniciados com um perfil anônimo sorteado a cada execução, porque duas cópias no mesmo PC não podem usar a mesma conta.
  - `HospedarLocal()` / `EntrarLocal(ip)` usam IP direto na porta 7777, sem Relay. Servem para testar sem internet ou jogar em rede local.
  - Quando o anfitrião sai ou a conexão cai, o estado volta a `Desconectado` com uma mensagem, e o menu reaparece.
- **Menu (`UI/MenuOnline`, OnGUI):**
  - telas de hospedar, entrar com código e modo local;
  - na partida, o código e o número de jogadores ficam no canto, e ESC abre o menu (o mundo continua rodando);
  - `MenuOnline.Aberto` bloqueia os comandos do jogador.
- **Linha de comando, para testes:** `-entrar CÓDIGO`, `-hospedar-local`, `-entrar-local [ip]` e `-bot`.
  - Com `-bot`, o jogador local anda em círculo, atira a cada 1,5 s, troca a lanterna a cada 5 s e registra no log, a cada 3 s, o que vê dos outros (`[robô] jogador N: posição, olhar, tiros recebidos, lanterna`).
- **Como testar com dois jogadores:**
  1. gere a build em `Builds/Teste/` (Development, ignorada pelo git);
  2. no Editor, em Play, hospede;
  3. rode a build sem janela, por exemplo `ZombieCityCleaner.exe -batchmode -nographics -entrar CÓDIGO -bot -logFile caminho.log`;
  4. confira no Editor (`JogadorRede.Todos`, `TirosRecebidos`) e no log da build.
  - Ao terminar, desligue `MenuOnline.Bot` se ele foi ligado no Editor: sem Domain Reload, ele continuaria ligado no próximo Play.
- **O jogador (`Assets/_Game/Prefabs/Jogador.prefab`, montado por `Editor/MontagemDaRede`, menu Zombie City Cleaner > Montar jogador e rede):**
  - **Raiz:** CharacterController (1,8 m, raio 0,3), NetworkObject e NetworkTransform com autoridade do dono (posição e só o giro em Y).
    - `JogadorRede`: as câmeras, o corpo, a cor da família (o servidor dá a primeira livre) e o RPC do tiro.
    - `MovimentoPrimeiraPessoa`: andar 2,0 m/s, correr 4,2 m/s, mirando 1,3 m/s, pulo; abaixo de y = −20, volta ao ponto de nascimento.
    - `ArmaJogador`: 12 balas, raycast do centro da tela, recarga com R, mira no botão direito (FOV 70 → 58), mira e munição na tela.
    - `LanternaJogador`: luz no peito que segue o olhar com atraso; F liga e desliga.
  - **"Corpo":** `Jogador.fbx` com o `Jogador_TP.controller`. Para o dono, os renderers viram só sombra. O peito e a cabeça giram por cima da animação para acompanhar o olhar (`Pitch`).
  - **"Cabeca" (1,62 m) > "Camera"** (a do mundo, que não desenha a camada 6) **> "CameraBracos"** (sobreposição, FOV 55, só a camada 6) e **"Bracos"** (`Jogador_FP.fbx`, na camada 6 "ArmasPrimeiraPessoa"). A câmera dos braços está na pilha da câmera do mundo, então os braços nunca atravessam paredes.
  - **"Lanterna"** fica no peito, abaixo e à frente dos olhos. Assim ela não estoura a pistola de luz.
  - Só o dono liga as câmeras, o ouvido, os braços, o movimento e a arma.
- **O que vai pela rede:**
  - a posição e o giro (NetworkTransform);
  - `Pitch`, `Mirando` e `Ligada` da lanterna (NetworkVariables escritas pelo dono);
  - `Cor` (escrita pelo servidor);
  - o tiro, por `TiroRpc` (`SendTo.NotMe`), com o ponto de impacto. Quem recebe toca a animação de tiro do corpo, o clarão, o som 3D e as faíscas.
  - A velocidade dos outros, usada pela animação, é medida pela posição recebida a cada 0,1 s.
- **Ações de input do projeto** (`InputSystem_Actions`, mapa Player): as do modelo da Unity, mais `Aim` (botão direito / gatilho esquerdo), `Reload` (R / X) e `Flashlight` (F / direcional para cima). O tiro no controle foi para o gatilho direito.
- **`Combat/Efeitos`** (objeto "Efeitos" na cena): sons gerados por código (tiro, clique, recarga, lanterna), o clarão do cano e as faíscas e poeira do impacto. Os materiais `Particulas` e `Clarao` ficam em `Art/Materials/`.
- **Cena:**
  - **"Pontos de nascimento":** 4 pontos na rua de teste.
  - **"Câmera de teste":** com `CameraDoMenu`, aparece no menu e some quando o jogador local nasce.
  - **"Teste (temporário)":** ganhou um chão grande.
- **Peças das armas nos modelos:** `Boca_TP` (filho de `weapon.R` no corpo) e `Boca_FP` (filho de `gun` nos braços) marcam a ponta do cano.
