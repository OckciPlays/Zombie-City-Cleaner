# Zombie City Cleaner (Unity)

Jogo de sobrevivência online (até 4 jogadores, a família) numa cidade infestada de zumbis, em low-poly com cores sólidas e clima sombrio no estilo Silent Hill.

- A arte (Blender, referências, sons originais) fica em `C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner`. O `CLAUDE.md` de lá tem o conceito do jogo.
- O plano, dividido em partes, está no `Plano_Demo.md` da mesma pasta. Siga a ordem dele e só avance com a parte anterior aprovada.

## Branches

- `feature/demo-cidade` (esta): a demo da cidade, começada do zero.
  - Parte 0 (fundação), Parte 1 (o zumbi), Parte 2 (o jogador e a pistola), Parte 3 (o online base), Parte 4 (a cidade em blocos, o clima e o dia) e Parte 5 (os zumbis na cidade) estão feitas.
  - O próximo passo é a Parte 6 (combate e desmembramento).
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
  - `PC_Renderer` (renderizador 0) tem o recurso "NeblinaDeAltura" (Full Screen Pass, antes dos transparentes, com profundidade).
  - `PC_Renderer_Bracos` (renderizador 1, sem recursos) é usado pela câmera dos braços. Sem ele, a neblina de altura seria aplicada de novo por cima da imagem, só com a profundidade dos braços.
  - Os dois são montados por `Editor/MontagemDoClima`, que só mexe nos assets de URP dentro de `Assets/` (nunca no asset do pacote).
- **Camadas:** 6 "ArmasPrimeiraPessoa" (os braços) e 7 "Zumbi" (cápsulas e hitboxes dos zumbis; não colide com ela mesma nem com a 6). O cenário e os jogadores ficam na Default.

## Pastas

- `Assets/_Game/` é tudo da demo:
  - `Scenes/Demo_Cidade.unity`: a cena principal, a única nas Build Settings. `Scenes/Demo_Cidade/NavMesh da cidade.asset` é o NavMesh dela.
  - `Prefabs/`: `Jogador.prefab` e `Zumbi.prefab`, os dois montados por scripts de editor.
  - `Settings/Demo_Volume.asset` (noite) e `Settings/Demo_Volume_Dia.asset` (dia): o pós-processamento (ACES, cores dessaturadas, vinheta, granulado, bloom).
  - `Art/Characters/Zombie/`: o zumbi.
  - `Art/Characters/Jogador/`: o jogador (corpo, braços de primeira pessoa e pistola).
  - `Art/Materials/Blocos/`: os materiais da cidade em blocos, de cor sólida (URP Lit, com instancing). `ConeDeLuz.mat` é o cone falso dos postes (aditivo, com o degradê `DegradeCone.png`).
  - `Art/Materials/`: também `NeblinaDeAltura.mat`, `Particulas.mat` e `Clarao.mat`. Os `Teste_*` são da fundação.
  - `Art/Meshes/`: malhas geradas pela montagem da cidade (`Prisma`, `ConeDeLuz`, `Pinheiro`).
  - `Shaders/NeblinaDeAltura.shader`: a neblina de altura em tela cheia.
  - `Audio/SomZombie1.mp3`: a voz do zumbi (mono, descomprimida ao carregar), usada nos gemidos distantes.
- Os scripts vão para `Assets/_Game/Scripts/`, separados por sistema (Network, Player, Zombies, Combat, Loot, World, Atmosphere, UI).

## Cena `Demo_Cidade`

- **Raízes:** "Cidade", "Navegação" (o NavMesh), "Zumbis", "Clima", "Sol e lua", "Pós-processamento (noite)" e "(dia)", "Rede", "Efeitos", "Pontos de nascimento" e "Câmera de teste".
- **Os scripts de montagem**, no menu Zombie City Cleaner, refazem partes da cena. Mudanças feitas à mão nessas partes se perdem; mude o script e rode de novo.
  - Montar cidade em blocos: "Cidade", os pontos de nascimento e o NavMesh.
  - Montar clima e ciclo do dia: "Clima" e os volumes.
  - Montar jogador e rede: o prefab do jogador, "Rede", "Efeitos" e "Pontos de nascimento".
  - Montar zumbis: a camada, o prefab do zumbi, o prefab do jogador, "Zumbis" e o NavMesh.
- Os valores de neblina e luz guardados na cena (densidade 0,05, ambiente azulado) são só os do Editor parado. Em Play, o `AmbienteDoDia` reescreve tudo a cada quadro.

## A cidade em blocos (Parte 4)

- **Montada por código:** `Editor/MontagemDaCidade`, menu Zombie City Cleaner > Montar cidade em blocos. Ela apaga e refaz a raiz "Cidade" (e o antigo "Teste (temporário)"), então mudanças feitas à mão na cidade se perdem. Mude o script e rode de novo.
  - Os materiais (`Art/Materials/Blocos/`) usam as cores do `Plano_Demo.md`. As peças são caixas, com ajudantes para parede com portas e janelas (`Parede` + `Abertura`), laje com buraco (`Laje`), escada e telhado.
  - Tudo é estático (batching e oclusão), menos as luzes, os postes e os cones de luz, que mudam em Play.
  - No fim, ela põe os 4 pontos de nascimento na frente da casa da família.
- **Planta (metros, +Z = norte):**
  - Ruas em grade nas linhas x e z = −40, 0 e 40, com 8 m de largura, faixa amarela tracejada e calçadas de 2,5 m em volta dos 4 quarteirões. As 4 saídas, em ±60, têm barricadas.
  - **Prédio** em (20, 0, 21), 22 × 14 m, 4 andares de 3 m: escada em U no fundo, corredor no meio e 2 apartamentos por andar, com eco (reverb) dentro.
  - **Casa da família** em (−20, 0, −18), virada para o sul (yaw 180), 12 × 10 m, azul, com cerca. É a única com bunker.
  - **Outras casas:** Casa 1 (−27, 0, 19), Casa 2 (−12, 0, 23), Casa 4 (13, 0, −14) e Casa 5 (28, 0, −26). Todas dá para entrar.
  - Praça em (24, 0, −12), estacionamento em (10, 0, 33), 8 carros, cercas e árvores.
  - Um anel de 320 pinheiros (raio 64 a 98) e a barreira invisível de 48 lados no raio 63.
  - O chão é uma laje de 220 × 220 m feita por último, com um buraco na escada do bunker.
- **Bunker** (embaixo do quintal da casa da família):
  - a escada começa no meio do cômodo de trás, com grade em volta do buraco, e desce para os fundos (19 degraus, do piso a −3,2 m);
  - um patamar de 1,2 m e a porta (1,2 × 2,3 m) levam às salas: sala comum (mesa), quarto (beliche) e despensa (prateleiras);
  - uma lâmpada quente (intensidade 5, alcance 9) e eco de corredor de pedra;
  - lá embaixo a luz do dia não chega (veja "Clima e ciclo do dia").
- **Postes (`World/PosteDeLuz`):** 28 postes com luz de sódio (spot, intensidade 95, alcance 16, ângulo 105°, sem sombra) e um cone falso de luz.
  - Eles acendem com a noite, cada um com um atraso próprio. Alguns piscam (`i % 7 == 3`) e alguns estão quebrados (`i % 11 == 5`).
  - O cone e a lâmpada mudam por MaterialPropertyBlock. A luz só fica ligada quando o poste está aceso.

## Clima e ciclo do dia (Parte 4)

- **Montado por `Editor/MontagemDoClima`** (menu Zombie City Cleaner > Montar clima e ciclo do dia): o objeto "Clima", o perfil do dia, a neblina de altura no renderizador, o renderizador dos braços e o som. Pode rodar de novo.
- **"Clima"** tem um NetworkObject e estes scripts:
  - **`World/CicloDoDia`:** o anfitrião manda na hora e a envia a cada 1 s (`HoraRede`); entre um envio e outro, cada um avança sozinho. Fora da partida o relógio roda localmente.
    - `CicloDoDia.Hora` (0 a 24) e `CicloDoDia.Noite` (0 das 8h às 17h, 1 das 20h às 5h, com transição suave) são estáticos, para qualquer script ler.
    - Começa às 21h, e um dia inteiro leva 24 minutos reais.
    - Teste: o anfitrião avança ou volta 1 hora com Page Up / Page Down. `-hora N` na linha de comando muda a hora inicial.
  - **`World/AmbienteDoDia`:** troca os tons de dia e de noite (neblina, luz ambiente, cor, força e altura de "Sol e lua", o peso dos dois volumes e o fundo das câmeras) e passa os valores da neblina de altura ao shader.
    - Dia: neblina pesada e clara (cinza, densidade 0,032). Noite: neblina escura e azulada (densidade 0,042) e luar frio. Neblina clara à noite apagaria as poças de luz dos postes.
    - **Subsolo:** quando a câmera desce de −0,3 m para −1,5 m (a escada do bunker), a luz ambiente, o sol e as duas neblinas caem para 15%. Lá embaixo só a lâmpada e a lanterna iluminam.
  - **`Atmosphere/SomAmbiente`:** o vento é gerado por código, em loop. Eventos distantes (35 a 60 m) tocam a cada 10 a 26 s, e à noite com o dobro da frequência: gemidos (trechos de `SomZombie1.mp3` mais graves), sirene e pancada de metal.
  - **"Partículas no ar" (`Atmosphere/ParticulasNoAr`):** uma caixa de 26 × 12 × 26 m que segue a câmera, com cinza de dia e poeira à noite.
- **Neblina de altura (`Shaders/NeblinaDeAltura.shader`):** um passe de tela cheia antes dos transparentes (os cones e as partículas ficam por cima).
  - A densidade cai com a altura (exponencial, calculada exata ao longo do raio) e tem manchas de ruído arrastadas pelo vento.
  - Abaixo da altura base a conta usa a base, para a neblina não engrossar debaixo da terra.
- **Lanterna:** intensidade 30, alcance 28, inclinada 9° para baixo do olhar.
- **Como testar:** em Play, Page Up / Page Down mudam a hora. Por código, `typeof(CicloDoDia).GetProperty("Hora").GetSetMethod(true).Invoke(null, new object[] { 12f })`.

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
  - O prefab usa `Malhas/Zumbi_Inteiro.asset`: as 6 regiões juntas num submesh só, com um material (um draw call em vez de seis). É gerada pela montagem dos zumbis.
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
- **Linha de comando, para testes:** `-entrar CÓDIGO`, `-hospedar-local`, `-entrar-local [ip]`, `-bot` e `-hora N`.
  - Com `-bot`, o jogador local anda em círculo, atira a cada 1,5 s, troca a lanterna a cada 5 s e registra no log, a cada 3 s:
    - a hora, os zumbis que recebe e a vida (`[robô] hora HH:MM, zumbis N (perto P, médio M), vida V`);
    - o que vê dos outros (`[robô] jogador N: posição, olhar, tiros recebidos, lanterna`).
  - Matar a build sem fechar a conexão deixa o jogador dela na partida até o tempo limite do transporte.
- **Como testar com dois jogadores:**
  1. gere a build em `Builds/Teste/` (Development, ignorada pelo git);
  2. no Editor, em Play, hospede (com código, ou "Hospedar local" para testar sem internet);
  3. rode a build sem janela, por exemplo `ZombieCityCleaner.exe -batchmode -nographics -entrar CÓDIGO -bot -logFile caminho.log` (ou `-entrar-local 127.0.0.1` no lugar de `-entrar CÓDIGO`);
  4. confira no Editor (`JogadorRede.Todos`, `TirosRecebidos`) e no log da build.
  - Ao terminar, desligue `MenuOnline.Bot` se ele foi ligado no Editor: sem Domain Reload, ele continuaria ligado no próximo Play.
- **O jogador (`Assets/_Game/Prefabs/Jogador.prefab`, montado por `Editor/MontagemDaRede`, menu Zombie City Cleaner > Montar jogador e rede):**
  - **Raiz:** CharacterController (1,8 m, raio 0,3), NetworkObject e NetworkTransform com autoridade do dono (posição e só o giro em Y).
    - `JogadorRede`: as câmeras, o corpo, a cor da família (o servidor dá a primeira livre) e o RPC do tiro.
    - `MovimentoPrimeiraPessoa`: andar 2,0 m/s, correr 4,2 m/s, mirando 1,3 m/s, pulo; abaixo de y = −20, volta ao ponto de nascimento.
    - `ArmaJogador`: 12 balas, raycast do centro da tela, recarga com R, mira no botão direito (FOV 70 → 58), mira e munição na tela.
    - `LanternaJogador`: luz no peito que segue o olhar com atraso; F liga e desliga.
    - `VidaJogador`: `Vida` (100, só o anfitrião escreve). O dono vê a barra, o clarão vermelho e o tranco do golpe. Se a vida acaba, ele volta ao ponto de nascimento com a vida cheia ("VOCÊ CAIU"). Depois de 8 s sem apanhar, recupera 2 por segundo (provisório, até o saque da Parte 7).
  - **"Corpo":** `Jogador.fbx` com o `Jogador_TP.controller`. Para o dono, os renderers viram só sombra. O peito e a cabeça giram por cima da animação para acompanhar o olhar (`Pitch`).
  - **"Cabeca" (1,62 m) > "Camera"** (a do mundo, que não desenha a camada 6) **> "CameraBracos"** (sobreposição, FOV 55, só a camada 6) e **"Bracos"** (`Jogador_FP.fbx`, na camada 6 "ArmasPrimeiraPessoa"). A câmera dos braços está na pilha da câmera do mundo, então os braços nunca atravessam paredes.
  - **"Lanterna"** fica no peito, abaixo e à frente dos olhos. Assim ela não estoura a pistola de luz. Ela segue o olhar 9° mais baixo (`inclinacao`).
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
  - **"Pontos de nascimento":** 4 pontos na frente da casa da família, postos pela montagem da cidade.
  - **"Câmera de teste":** com `CameraDoMenu`, aparece no menu e some quando o jogador local nasce.
- **Peças das armas nos modelos:** `Boca_TP` (filho de `weapon.R` no corpo) e `Boca_FP` (filho de `gun` nos braços) marcam a ponta do cano.

## Os zumbis na cidade (Parte 5, `Assets/_Game/Scripts/Zombies/`)

- **Montados por `Editor/MontagemDosZumbis`** (menu Zombie City Cleaner > Montar zumbis). Pode rodar de novo.
- **O prefab `Zumbi.prefab`:**
  - **Raiz (camada 7):** `Zumbi`, NavMeshAgent (desligado no prefab, raio 0,3), CapsuleCollider (1,75 m, raio 0,28), Rigidbody cinemático e AudioSource (a voz).
  - **"Modelo":** o `NovoZombie.fbx` com o `Zombie.controller` e a malha `Zumbi_Inteiro`.
  - **11 hitboxes nos ossos** (camada 7, com `Hitbox` e a `RegiaoDoCorpo`):
    - a cabeça é uma esfera, medida na malha e levada para o osso pela bindpose;
    - o resto são cápsulas de osso a osso: duas no tronco, braço e antebraço de cada lado, e coxa e canela de cada lado.
    - A pose em que o FBX abre não é a T-pose; por isso as medidas usam os ossos e a bindpose, e não a posição dos vértices.
  - O `Zumbi` não tem Update próprio: o `ZombieManager` faz um laço só para todos.
- **"Zumbis" na cena:** um NetworkObject com o `ZombieManager` e o `BenchmarkDeZumbis`.
  - Os filhos são 29 `PontoDeHorda` (quantidade e raio; ~130 zumbis na rua, na praça, no estacionamento, nas casas e nos 4 andares do prédio).
  - `areasLivres`: a casa da família com o bunker e a frente dela não recebem zumbis.
- **NavMesh ("Navegação"):** um `NavMeshSurface` pelos colisores, só no volume da cidade (128 × 24 × 128 m, dentro da barreira). Ele é refeito pela montagem dos zumbis e pela da cidade.
- **Comportamento (`Zumbi`, só no anfitrião):**
  - Parado e vagando: vaga devagar (0,55 m/s) em volta de onde está. Um em cada três para três vezes mais.
  - Perceber, só no nível Perto:
    - contato (2,5 m) e passos correndo (9 m) são na hora;
    - a visão (140°, com linha de visada no cenário) enche um alerta, mais rápido de perto; desconfiado, ele para e vira para o jogador;
    - o alcance da visão é de 16 m de dia e 6 m à noite; à noite, sobe para 13 m embaixo de um poste aceso (`PosteDeLuz.Ilumina`) e 26 m com a lanterna ligada;
    - a lanterna apontada para ele denuncia mesmo de costas.
  - Tiro (`ZombieManager.Barulho`, chamado pelo `JogadorRede` no anfitrião): até 12 m, o zumbi vai atrás de quem atirou; até 35 m, vai investigar o lugar.
  - Perseguir a 1,1 m/s (o jogador andando já se afasta). Ataca a 1,3 m: 12 de dano a cada 1,4 s, com um "bote" (tronco e braços girados por cima da animação, no LateUpdate).
  - Desistir com o alvo a mais de 20 m, ou 6 s sem vê-lo: vai até onde o viu, olha em volta e volta a vagar ali.
  - Gemidos com trechos da `SomZombie1.mp3`: ao começar a perseguir e, de vez em quando, os que estão a até 18 m de quem escuta (no máximo 3 por segundo).
  - Os números ficam em `ajustes` (`AjustesDeZumbi`), no Inspector do `ZombieManager`.
- **LOD de IA (`ZombieManager`)**, pela distância até o jogador mais próximo, refeito a cada 0,2 s:

  | Nível | Distância | IA | Navegação | Animação | Colisão e sombra |
  |---|---|---|---|---|---|
  | Perto | até 15 m | completa, a cada 0,1 s | agente, desvio médio | `CullUpdateTransforms` | hitboxes; a cápsula só segura o jogador (camada Ignore Raycast); com sombra |
  | Médio | 15 a 40 m | só vagar e tiros, a cada 0,5 s | agente, desvio barato | `CullCompletely` | a cápsula é a hitbox; sem sombra |
  | Longe | mais de 40 m | nenhuma (zumbi virtual, parado) | agente desligado | desligada | nada; não desenha |

  - A histerese é de 3 m: para chegar mais perto é preciso passar a distância menos 3 m.
  - Quem persegue, ataca ou levou tiro (`LevouTiro`, para a Parte 6) fica sempre no Perto.
  - No máximo 40 zumbis pensam por quadro (`orcamentoPorQuadro`), em rodízio.
  - Além de 65 m de quem joga na máquina, o zumbi não é desenhado.
- **Pool:** `Repovoar…` devolve todos e cria de novo, 6 por quadro. `Zerar()` limpa estado, agente, voz e Animator (na Parte 6, também a vida e os membros). O ponto de nascimento é validado no NavMesh, na mesma altura do ponto.
- **Rede (`ZombieManager.Rede.cs`):** os zumbis não são NetworkObjects.
  - O anfitrião manda a cada cliente uma mensagem nomeada (`ZCC.Zumbis`, não confiável e em ordem) só com os zumbis perto dele, com a mesma histerese do LOD:
    - até 15 m, 10 vezes por segundo;
    - de 15 a 40 m, a cada 3 envios (em rodízio pelo Id);
    - mais longe, não manda.
  - Cada zumbi ocupa 12 bytes: Id, geração, posição em cm, giro, estado com a contagem de golpes e velocidade. São até 90 por mensagem (~1,1 KB, abaixo do limite de 1.300 bytes).
  - No cliente, cada Id vira uma cópia (do mesmo pool), mostrada um pouco no passado (~0,2 s perto, ~0,5 s no médio) e interpolada. Ela some depois de 1,5 s sem notícias. A geração muda quando o Id volta do pool, e aí a cópia é trocada.
  - A cópia toca o gemido quando o estado vira Perseguindo e o bote quando a contagem de golpes muda. As hitboxes dela seguem a distância de quem joga ali.
  - Eventos que acontecem uma vez (membro arrancado, cabeça, morte) vão entrar na Parte 6, como RPCs com o Id.
- **Na tela:** F3 mostra os zumbis por nível e a rede (KB/s) no anfitrião, ou as cópias no cliente.
- **Benchmark (`BenchmarkDeZumbis`):**
  - F9 no anfitrião, ou `-hospedar-local -benchmark -resultado caminho.md` na build (espera um cliente por até 25 s e fecha no fim).
  - Troca a população por 50, 100, 250 e 500 zumbis espalhados e mede cada quantidade por 10 s, sem vsync e sem dano.
  - Mede o quadro (média e p95), a thread principal, a IA + LOD, a rede, `Animators.Update`, `NavMeshManager`, `Physics.Simulate`, os draw calls e os KB/s por cliente. Os marcadores só funcionam no Editor e em builds Development.
  - A tabela vai para `Benchmarks/Benchmark_Zumbis.md` (no Editor) e depois as hordas voltam.
