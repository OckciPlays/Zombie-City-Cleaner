# 04 – Arquitetura

Como o projeto Unity está organizado. Para cada detalhe (números, motivos de cada escolha), veja o [`CLAUDE.md`](../CLAUDE.md) da raiz.

## Tecnologia

- **Unity 6000.6.5f1** com **URP 17.6** (renderizador Forward+), o **Input System** novo e o **AI Navigation 2.0** (NavMesh).
- **Rede:** **Netcode for GameObjects 2.13** e **Multiplayer Services 2.4** (`com.unity.services.multiplayer`: a sessão, o código de entrada e o Relay). O **Multiplayer Play Mode 3.0** permite vários jogadores no mesmo Editor.
- O projeto está ligado à Unity Cloud (projeto "Zombie City Cleaner").
- **Play sem Domain Reload e sem Scene Reload** (entra no Play rápido). Por isso, estado `static` sobrevive entre um Play e outro (veja as [regras](06-Como-Colaborar.md#regras-do-projeto)).

## Pastas

```text
Assets/_Game/
  Art/
    Characters/Jogador/   corpo, braços de 1ª pessoa de cada arma e item, armas e itens avulsos, controladores e materiais
    Characters/Zombie/    os 4 modelos de zumbi, malhas, materiais e o Animator
    Cidade/               a arte final da cidade (FBX exportados do Blender)
    Materials/            materiais de efeitos, decals, neblina, partículas
    UI/Icones/            os ícones do inventário (um por arma e item)
    Paleta.png            a paleta 16 × 16 de tudo o que não tem textura
  Audio/                  SomZombie1.mp3 (a voz dos zumbis)
  Prefabs/                Jogador.prefab e Zumbis/ (um zumbi e um corpo com física por modelo)
  Scenes/                 Demo_Cidade.unity (+ o NavMesh e a oclusão dela)
  Scripts/                Network, Player, Zombies, Combat, Loot, World, Atmosphere, UI e Editor
  Settings/               volumes de pós-processamento (dia e noite)
  Shaders/                NeblinaDeAltura.shader
Assets/Settings/          os assets do URP (PC_RPAsset, PC_Renderer, PC_Renderer_Bracos)
Benchmarks/               o histórico do benchmark dos zumbis
Docs/                     esta documentação
```

## A cena `Demo_Cidade`

| Raiz | O que é |
|---|---|
| Cidade | a cidade inteira: chão, casas, prédio, bunker, móveis, objetos de rua, postes |
| Bunker (rede) | a porta, as luzes e os interruptores do bunker (objeto de rede) |
| Navegação | o NavMesh (um `NavMeshSurface` pelos colisores, só dentro da cidade) |
| Zumbis | o `ZombieManager` (objeto de rede) com os 29 pontos de horda |
| Clima | o ciclo do dia e o ambiente (objeto de rede), com as partículas no ar |
| Sol e lua | a luz direcional |
| Pós-processamento (noite) e (dia) | os dois volumes, misturados pela hora |
| Rede | o NetworkManager, o transporte, a sessão e o menu |
| Efeitos | sons, clarão do cano, faíscas e sangue (os sons, as faíscas e o sangue em pools) |
| Pontos de nascimento | 4 pontos no salão do bunker, em volta da mesa |
| Parede de armas | o painel de armas do bunker (objeto de rede) |
| Itens no mundo | a comida, a água e o que os jogadores largam (objeto de rede) |
| Câmera de teste | a câmera do menu (some quando o jogador nasce) |

## Os scripts de montagem

Quase tudo na cena e nos prefabs é **gerado por código**, pelos scripts de `Scripts/Editor/`, no menu **Zombie City Cleaner** do Editor. Eles podem rodar de novo à vontade. **Mudanças feitas à mão nas partes geradas se perdem:** mude o script e rode de novo.

| Menu | O que monta |
|---|---|
| Montar cidade em blocos (`MontagemDaCidade`) | a raiz "Cidade" com a arte final, os colisores, os móveis (e os lugares da comida e da água) e os postes, o "Bunker (rede)", os "Itens no mundo", o NavMesh e a oclusão; e move os pontos de nascimento (se já existem) para o bunker |
| Montar clima e ciclo do dia (`MontagemDoClima`) | o "Clima", os volumes, a neblina de altura no renderizador e o renderizador dos braços |
| Montar jogador e rede (`MontagemDaRede`) | o prefab do jogador, a "Rede", os "Efeitos" e os pontos de nascimento |
| Montar zumbis (`MontagemDosZumbis`) | a importação dos 4 zumbis, as malhas, o Animator, os prefabs, os "Zumbis" com as hordas, os decals e o NavMesh |
| Montar armas (`MontagemDasArmas`) | a importação das armas, dos itens, dos braços e dos ícones, as malhas, os Animators dos braços, o prefab do jogador, a "Parede de armas" e os "Itens no mundo" |

A ordem, do zero: cidade → clima → armas → jogador e rede → zumbis.

- A montagem das armas precisa do móvel do painel de armas, que a cidade põe no bunker.
- O prefab do jogador (refeito pelas montagens das armas, da rede e dos zumbis) precisa das armas já preparadas.
- A de jogador e rede cria os pontos de nascimento e os põe no bunker, que a cidade monta. A da cidade só move os pontos que já existem.
- A de jogador e rede apaga e recria o objeto "Efeitos", e o sangue (`Sangue`) é posto nele pela dos zumbis. **Rodou "Montar jogador e rede" sozinha? Rode "Montar zumbis" depois**, senão o jogo fica sem sangue.
- "Do zero" pressupõe a cena com os objetos que nenhuma montagem cria: "Sol e lua", os volumes de pós-processamento e a "Câmera de teste".

## Os sistemas

### Sessão e menu (`Network/`, `UI/`)

- **`SessaoOnline`:** `Hospedar()` cria uma partida privada de até 4 jogadores pelo Relay e guarda o código; `Entrar(codigo)` entra por ele. `HospedarLocal()` e `EntrarLocal(ip)` usam IP direto (porta 7777), sem Relay. Os serviços da Unity entram com um perfil anônimo sorteado (duas cópias no mesmo PC não podem usar a mesma conta).
- **`MenuOnline`** (OnGUI): as telas de hospedar, entrar e modo local; ESC abre o menu durante a partida (o mundo continua). Também é o dono do mouse: preso no jogo assim que a partida começa ou o menu fecha; `MenuOnline.Controla` diz quando o jogador recebe comandos (o clique que devolve o foco à janela não atira).
- **`Opcoes`:** o volume geral, o limite de FPS (VSync, 30, 60, 120, 144 ou sem limite) e o contador de FPS, no menu do ESC e no do começo, gravados no `PlayerPrefs`.
- **Linha de comando** (para testes com a build): `-entrar CÓDIGO`, `-hospedar-local`, `-entrar-local [ip]`, `-bot`, `-hora N`, e `-benchmark -resultado caminho.md` (com `-hospedar-local`: roda o benchmark e grava o resultado).

### O jogador (`Player/`, prefab `Jogador.prefab`)

- **Raiz:** CharacterController, NetworkObject e NetworkTransform com autoridade do dono (cada um controla o próprio movimento).
  - `JogadorRede`: as câmeras, o corpo, a cor da família, o que está na mão (no corpo) e o tiro pela rede.
  - `MovimentoPrimeiraPessoa`: andar (2,0 m/s), correr (4,2 m/s), mirando (1,3 m/s) e pular.
  - `InventarioJogador`: a mochila em grade (9 × 6; cada item ocupa os seus espaços) e os 3 atalhos (o do escolhido fica na mão), a roda e 1–3 para trocar, G para largar e a barra no HUD. A mochila é do anfitrião; os atalhos e o escolhido, do dono.
  - `TelaDoInventario` (TAB): a tela da mochila, com o retrato do jogador (`RetratoDoJogador`, uma cópia do corpo com câmera própria), a grade, os atalhos, os detalhes e o arrastar e soltar.
  - `MenuDoDesenvolvedor` (F1, só no Editor e nas builds de desenvolvimento): criar itens, contar, criar e matar zumbis, pelo anfitrião.
  - `ArmaJogador`: o que está na mão: com arma, o tiro, a recarga e a mira (e a luneta da sniper), com o pente guardado por lugar; com comida ou água, comer ou beber no clique; com a mão vazia, o soco (uma esfera curta da câmera; no zumbi, pouco dano e o recuo). E o E: mirando de perto uma coisa que se usa (`Interagivel`: um item no chão, uma arma do painel, a porta do bunker, um interruptor), mostra a dica e usa.
  - `LanternaJogador`: a luz no peito; o facho converge no ponto da mira (um raio sai do olho).
  - `VidaJogador`: a vida (100), que só o anfitrião muda. Sem vida, o jogador volta ao bunker. Ela volta sozinha só sem fome e sem sede.
  - `FomeESede`: duas barras (0 a 100) do anfitrião, que esvaziam com o tempo (25 min a sede, 40 a fome; mais rápido correndo) e tiram vida quando vazias.
  - `PassosJogador`: os passos, em todas as cópias, pela distância andada e pelo chão embaixo do pé (concreto, madeira, carpete, grama). Dentro das casas e do bunker, o `World/PisoPorZona` (posto pela montagem da cidade) diz o piso de cada cômodo.
- **"Corpo":** o modelo que os outros veem. Para o próprio jogador, ele só faz sombra.
- **"Cabeca" > "Camera" > "CameraBracos" e "Bracos":** a câmera do mundo, e por cima dela a câmera dos braços (camada 6), que desenha só os braços de primeira pessoa. Por isso os braços nunca atravessam paredes. "Bracos" tem um modelo por arma e item, mais os punhos (a mão vazia), e só o da mão fica ligado.

### Os itens (`Loot/`)

- **`Itens`:** o que cabe num lugar do inventário (`Item`: as 5 armas, a lata de comida e a garrafa de água) e quanto a comida e a água enchem a fome e a sede.
- **`ItensNoMundo`** (no objeto "Itens no mundo"): os itens soltos pelo mundo, numa lista de rede só (Id, item, ponto de apoio, giro). Cada máquina monta os objetos (a malha e o colisor que a mira procura); pegar pede ao anfitrião. Os 34 do começo vêm das marcas que a montagem da cidade põe nos tampos dos móveis.

### As armas (`Combat/Armas`, `Player/ArmaJogador`, `Combat/ParedeDeArmas`)

- **`Armas`** é a tabela dos números de cada arma (`DadosDaArma`), na ordem de `TipoDeArma` (o número que vai pela rede). A vida dos zumbis é contada em tiros de pistola: o corpo aguenta 6 e cada braço ou perna, 3 (entre parênteses, os tiros que matam ou arrancam):

  | Arma | Pente | Cadência | No tronco | Num membro | Recarga |
  |---|---|---|---|---|---|
  | Pistola | 12 | 0,18 s | 1 (6) | 1 (3) | 1,67 s |
  | Rifle (automático) | 30 | 0,1 s | 1,5 (4) | 1,5 (2) | 1,92 s |
  | Submetralhadora (automática) | 25 | 0,075 s | 1 (6) | 1 (3) | 1,67 s |
  | Espingarda (8 chumbos) | 6 | 0,92 s (com a bomba) | 7 até 5 m (1), 3 até 15 m (2), 1 depois | 3 (1), 1,5 (2), 0,5 | 0,54 s por cartucho |
  | Sniper (ferrolho e luneta) | 5 | 1,25 s (com o ferrolho) | 8 (1) | 8 (1) | 1,92 s |

  Na cabeça, qualquer acerto explode a cabeça (na espingarda, um chumbo perdido só conta de perto). A espingarda conta os chumbos juntos: um acerto por zumbi, na parte do chumbo do meio da mira; de perto, o tiro que mata joga o corpo para trás.
- **O tiro:** um raio do centro da tela (um por chumbo na espingarda), com dispersão em volta do olhar. O raio atravessa até dois braços (numa multidão, as mãos de quem ataca ficam na frente; o mesmo braço não conta duas vezes) e confere os primeiros 35 cm à parte (à queima-roupa).
- **Quem decide:** quem atira vê o acerto na hora (sangue, a marca na mira) e manda o acerto para o anfitrião (a região, o membro, o dano da distância e se o corpo é arremessado), que confere e aplica o dano.
- **A luneta da sniper:** o botão direito fecha a câmera para 12° (~6,7×), esconde os braços e desenha a luneta na tela; o mouse fica mais lento e a neblina afina, só para quem mira. O anfitrião sabe quem está na luneta (`JogadorRede.Luneta`) e, na linha dela, trata os zumbis de longe como de perto (veja o LOD).
- **O painel de armas** (`ParedeDeArmas`, um objeto de rede da cena): na parede do fundo do salão do bunker, com um lugar para cada arma (as 4 longas e 4 pistolas; `VagaDeArma.dono`). Mirando uma arma de perto, E pede ao anfitrião, que confere a distância e põe a arma no inventário (na mão vazia ou num lugar livre); com o inventário cheio, troca pela arma da mão, que volta para uma vaga do tipo dela. Mirando uma vaga vazia com a arma dela na mão, E guarda. Quem sai da partida devolve as armas. O conteúdo das vagas vai pela rede num número só (8 bits por vaga).
- **As animações:** cada arma e item tem os seus braços de primeira pessoa (um FBX por modelo) com o mesmo Animator, trocando os clipes por um override (nos itens, o `Usar` no lugar do tiro; nos punhos, o soco de direita e o de esquerda). O peso da camada das ações é do código: 1 durante uma ação, e descendo a 0 depois (parada, ela segurava a última pose, e os braços ficavam congelados). O corpo usa outro override para as armas longas (a sniper usa o do rifle) e um para os punhos (sem arma na mão).

### Os zumbis (`Zombies/`)

- **`ZombieManager`** (no objeto "Zumbis"): cria os zumbis dos pontos de horda, roda a IA de todos num laço só e manda o estado pela rede. Os zumbis **não são objetos de rede**.
- **`Zumbi`:** o comportamento (só no anfitrião): vagar, perceber, perseguir, atacar e desistir.
  - Visão de 16 m de dia e 6 m à noite (13 m embaixo de um poste aceso e 26 m com a lanterna ligada), passos correndo (9 m), contato (2,5 m) e o barulho do tiro (até 35 m).
  - Persegue a 1,1 m/s (andando, o jogador se afasta) e desiste a mais de 20 m ou depois de 6 s sem ver o alvo.
  - Os números ficam em `AjustesDeZumbi`, no Inspector do `ZombieManager`.
- **LOD de IA**, pela distância até o jogador mais próximo, com 3 m de histerese:

  | Nível | Distância | IA | Animação | Colisão |
  |---|---|---|---|---|
  | Perto | até 15 m | completa, a cada 0,1 s | completa | hitboxes nos ossos |
  | Médio | 15 a 40 m | só vagar e tiros, a cada 0,5 s | só quando aparece na tela | uma cápsula |
  | Longe | mais de 40 m | nenhuma (fica parado) | desligada | nenhuma |

  No máximo 40 zumbis pensam por quadro, em rodízio. Quem persegue, ataca ou levou tiro há menos de 5 s fica sempre no Perto. Para quem olha pela luneta, os zumbis na linha dela contam a distância dividida pelo aumento (a 70 m, viram Perto).
- **Os 4 modelos:** cada um tem o seu pool. O modelo de cada zumbi é calculado do Id e da geração dele (`(id * 7 + geração * 3) % 4`), então anfitrião e clientes chegam ao mesmo sem mandar nada.
- **Rede (`ZombieManager.Rede.cs`):** o anfitrião manda a cada cliente só os zumbis perto dele (até 15 m, 10 vezes por segundo; de 15 a 40 m, menos), 14 bytes por zumbi, numa mensagem não confiável. O cliente mostra cópias interpoladas um pouco no passado. Não há evento separado: a morte, os golpes, os tiros levados e os membros perdidos estão no estado de cada zumbi.
- **Desmembramento (`Membros`, `ZombieManager.Pedacos.cs`):** cada braço e cada perna tem vida (3 tiros de pistola); sem ela, o membro cai no cotovelo ou no joelho (tiro no antebraço ou na canela) ou inteiro (no braço ou na coxa). A malha cortada (com tampas de carne) e o pedaço que voa são feitos na hora, a partir da malha inteira, e guardados; os cortes comuns ficam prontos no começo da partida. Sem uma perna o zumbi manca; sem as duas, cai e rasteja.
- **Morte (`CorpoDeZumbi`, `ZombieManager.Corpos.cs`):** o zumbi some, e um corpo com física (11 partes com juntas) cai no lugar, empurrado pelo tiro (e cortado, se perdeu membros). Os corpos são só visuais (cada máquina tem os seus) e voltam ao pool afundando depois de 15 s.

### Efeitos e som (`Combat/`)

- **`Efeitos`:** os sons gerados por código (o tiro de cada arma, clique, carregador, bomba, cartucho, ferrolho, a luneta entrando e saindo e lanterna), o clarão do cano (criado a cada tiro) e as faíscas. O som espacial fica mais abafado quanto mais longe.
- **`SomDeTiro`:** o tiro sintetizado em camadas, com um perfil por arma e 3 variações de cada.
- **`SomDePasso`:** os passos (o calcanhar e a ponta do pé) em concreto, madeira, carpete e grama, 4 variações de cada, andando e correndo. **`SomDosItens`:** abrir a lata, mastigar, abrir a garrafa, o gole e largar no chão. **`SomDoSoco`:** o soco no ar, na carne e na parede.
- **`SomGerado`:** todo som feito por código passa por ele, que troca valores inválidos (NaN) por zero. Um NaN num clipe cala o áudio da Unity inteiro (o jogo ficava mudo depois de comer).
- **`Sangue`:** respingo, explosão da cabeça, jorro do pescoço e manchas no chão (decals), tudo em pools.

### A cidade, o clima e o dia (`World/`, `Atmosphere/`)

- **`CicloDoDia`:** o anfitrião manda na hora (começa às 21 h; um dia leva 24 minutos). `CicloDoDia.Noite` (0 a 1) é lido por quem precisa.
- **`AmbienteDoDia`:** troca a neblina, a luz e os volumes entre o dia e a noite. Embaixo da terra (o bunker) a luz de fora some e não há neblina; dentro das casas (`AreaInterna`) a neblina fica rala.
- **`PosteDeLuz`:** os 28 postes acendem com a noite, alguns piscam e alguns estão quebrados.
- **`ParticulasNoAr`:** cinza de dia e poeira à noite em volta da câmera; embaixo da terra, elas somem.
- **`BunkerRede`** (no "Bunker (rede)"): a porta blindada (`PortaDoBunker`) e as luzes do bunker, em 7 grupos com os seus interruptores (`Interruptor`). O anfitrião decide. Com a porta fechada, o lado de dentro fica isolado (`BunkerRede.Isolado`): o som não passa (`Efeitos.Som` e a voz dos zumbis), e os zumbis de fora não ouvem os tiros nem sentem quem está dentro.
- **`NeblinaDeAltura.shader`:** uma neblina que engrossa perto do chão, com manchas arrastadas pelo vento, num passe de tela cheia.
- **A colisão do chão** são caixas invisíveis; o que se vê são os 25 pedaços de chão do Blender.

## O que vai pela rede

| O quê | Como | Quem escreve |
|---|---|---|
| posição e giro do jogador | NetworkTransform | o dono |
| para onde olha, se está mirando, a lanterna | NetworkVariables | o dono |
| a cor da família | NetworkVariable | o anfitrião |
| a mochila (Id, item e posição de cada um) e o pedido de mover | `NetworkList` e `MoverRpc` | o anfitrião |
| os 3 atalhos e o da mão | NetworkVariables | o dono |
| o menu do desenvolvedor (criar itens e zumbis, matar todos) | `CriarItemRpc`, `CriarZumbisRpc`, `MatarZumbisRpc` | o anfitrião faz |
| largar e comer | `LargarRpc` e `ConsumirRpc`, para o anfitrião | o dono (o anfitrião confere) |
| os itens no chão e o pedido de pegar | `NetworkList` e `PegarRpc` | o anfitrião |
| a fome e a sede | NetworkVariables (uma vez por segundo) | o anfitrião |
| o tiro (a arma e os impactos de cada chumbo) | `TiroRpc`, para os outros | o dono |
| o soco (a animação, o som e o que ele acertou) | `SocoRpc` e `PancadaRpc`, para os outros | o dono |
| o acerto num zumbi | `AcertoRpc`, para o anfitrião | o dono (o anfitrião confere) |
| os zumbis perto de cada jogador | mensagem nomeada `ZCC.Zumbis` | o anfitrião |
| a hora do dia | a cada 1 s | o anfitrião |
| o painel de armas e o pedido (pegar, trocar, guardar) | NetworkVariable e `VagaRpc` | o anfitrião |
| a porta e as luzes do bunker | NetworkVariables, `PortaRpc` e `LuzRpc` | o anfitrião |
| a vida do jogador, o golpe e a queda | NetworkVariable `Vida`, `GolpeRpc` e `CaiuRpc` para o dono | o anfitrião |

## Camadas

| Camada | Uso |
|---|---|
| 6 ArmasPrimeiraPessoa | os braços de primeira pessoa (só a câmera dos braços desenha) |
| 7 Zumbi | as cápsulas e as hitboxes dos zumbis |
| 8 Corpos | os zumbis mortos com física (o tiro passa por eles) |
| 9 Jogador | a raiz do jogador |
| Default | o cenário |

## Desempenho

- O benchmark (F9 no anfitrião) mede o quadro, a GPU, a IA, a animação, a navegação, a física, os draw calls e a rede com 50, 100, 250 e 500 zumbis, e grava uma seção nova em `Benchmarks/Benchmark_Zumbis.md`.
- Último resultado (Editor, noite): 3,7 ms de quadro com 50 zumbis, 4,2 ms com 250 e 4,9 ms com 500; GPU de 2,1 a 2,5 ms; 620 a 700 draw calls.
- Quem mais pesa são os zumbis (quase todos os triângulos). A neblina e o corte a 150 m escondem o que está longe, e a oclusão esconde o que está atrás das casas.
