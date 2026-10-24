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
    Characters/Jogador/   corpo, braços de 1ª pessoa de cada arma, armas avulsas, controladores e materiais
    Characters/Zombie/    os 4 modelos de zumbi, malhas, materiais e o Animator
    Cidade/               a arte final da cidade (FBX exportados do Blender)
    Materials/            materiais de efeitos, decals, neblina, partículas
    Paleta.png            a paleta 16 × 16 de tudo o que não tem textura
  Audio/                  SomZombie1.mp3 (a voz dos zumbis)
  Prefabs/                Jogador.prefab e Zumbis/ (um zumbi e um corpo com física por modelo)
  Scenes/                 Demo_Cidade.unity (+ o NavMesh e a oclusão dela)
  Scripts/                Network, Player, Zombies, Combat, World, Atmosphere, UI e Editor
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
| Navegação | o NavMesh (um `NavMeshSurface` pelos colisores, só dentro da cidade) |
| Zumbis | o `ZombieManager` (objeto de rede) com os 29 pontos de horda |
| Clima | o ciclo do dia e o ambiente (objeto de rede), com as partículas no ar |
| Sol e lua | a luz direcional |
| Pós-processamento (noite) e (dia) | os dois volumes, misturados pela hora |
| Rede | o NetworkManager, o transporte, a sessão e o menu |
| Efeitos | sons, clarão do cano, faíscas e sangue (os sons, as faíscas e o sangue em pools) |
| Pontos de nascimento | 4 pontos na sala comum do bunker |
| Parede de armas | o suporte de armas do bunker (objeto de rede) |
| Câmera de teste | a câmera do menu (some quando o jogador nasce) |

## Os scripts de montagem

Quase tudo na cena e nos prefabs é **gerado por código**, pelos scripts de `Scripts/Editor/`, no menu **Zombie City Cleaner** do Editor. Eles podem rodar de novo à vontade. **Mudanças feitas à mão nas partes geradas se perdem:** mude o script e rode de novo.

| Menu | O que monta |
|---|---|
| Montar cidade em blocos (`MontagemDaCidade`) | a raiz "Cidade" com a arte final, os colisores, os móveis e os postes, o NavMesh e a oclusão; e move os pontos de nascimento (se já existem) para o bunker |
| Montar clima e ciclo do dia (`MontagemDoClima`) | o "Clima", os volumes, a neblina de altura no renderizador e o renderizador dos braços |
| Montar jogador e rede (`MontagemDaRede`) | o prefab do jogador, a "Rede", os "Efeitos" e os pontos de nascimento |
| Montar zumbis (`MontagemDosZumbis`) | a importação dos 4 zumbis, as malhas, o Animator, os prefabs, os "Zumbis" com as hordas, os decals e o NavMesh |
| Montar armas (`MontagemDasArmas`) | a importação das armas e dos braços, as malhas das armas, os Animators dos braços, o prefab do jogador e a "Parede de armas" |

A ordem, do zero: cidade → clima → armas → jogador e rede → zumbis.

- A montagem das armas precisa do móvel do suporte, que a cidade põe no bunker.
- O prefab do jogador (refeito pelas montagens das armas, da rede e dos zumbis) precisa das armas já preparadas.
- A de jogador e rede cria os pontos de nascimento e os põe no bunker, que a cidade monta. A da cidade só move os pontos que já existem.
- A de jogador e rede apaga e recria o objeto "Efeitos", e o sangue (`Sangue`) é posto nele pela dos zumbis. **Rodou "Montar jogador e rede" sozinha? Rode "Montar zumbis" depois**, senão o jogo fica sem sangue.
- "Do zero" pressupõe a cena com os objetos que nenhuma montagem cria: "Sol e lua", os volumes de pós-processamento e a "Câmera de teste".

## Os sistemas

### Sessão e menu (`Network/`, `UI/`)

- **`SessaoOnline`:** `Hospedar()` cria uma partida privada de até 4 jogadores pelo Relay e guarda o código; `Entrar(codigo)` entra por ele. `HospedarLocal()` e `EntrarLocal(ip)` usam IP direto (porta 7777), sem Relay. Os serviços da Unity entram com um perfil anônimo sorteado (duas cópias no mesmo PC não podem usar a mesma conta).
- **`MenuOnline`** (OnGUI): as telas de hospedar, entrar e modo local; ESC abre o menu durante a partida (o mundo continua).
- **Linha de comando** (para testes com a build): `-entrar CÓDIGO`, `-hospedar-local`, `-entrar-local [ip]`, `-bot`, `-hora N`, e `-benchmark -resultado caminho.md` (com `-hospedar-local`: roda o benchmark e grava o resultado).

### O jogador (`Player/`, prefab `Jogador.prefab`)

- **Raiz:** CharacterController, NetworkObject e NetworkTransform com autoridade do dono (cada um controla o próprio movimento).
  - `JogadorRede`: as câmeras, o corpo, a cor da família, a arma da mão e o tiro pela rede.
  - `MovimentoPrimeiraPessoa`: andar (2,0 m/s), correr (4,2 m/s), mirando (1,3 m/s) e pular.
  - `ArmaJogador`: o tiro, a recarga, a mira e a troca no suporte.
  - `LanternaJogador`: a luz no peito; o facho converge no ponto da mira (um raio sai do olho).
  - `VidaJogador`: a vida (100), que só o anfitrião muda. Sem vida, o jogador volta ao bunker.
- **"Corpo":** o modelo que os outros veem. Para o próprio jogador, ele só faz sombra.
- **"Cabeca" > "Camera" > "CameraBracos" e "Bracos":** a câmera do mundo, e por cima dela a câmera dos braços (camada 6), que desenha só os braços de primeira pessoa. Por isso os braços nunca atravessam paredes. "Bracos" tem um modelo por arma, e só o da arma da mão fica ligado.

### As armas (`Combat/Armas`, `Player/ArmaJogador`, `Combat/ParedeDeArmas`)

- **`Armas`** é a tabela dos números de cada arma (`DadosDaArma`), na ordem de `TipoDeArma` (o número que vai pela rede):

  | Arma | Pente | Cadência | Dano | Recarga |
  |---|---|---|---|---|
  | Pistola | 12 | 0,18 s | 1 | 1,67 s |
  | Rifle (automático) | 30 | 0,1 s | 1,4 | 1,92 s |
  | Submetralhadora (automática) | 25 | 0,075 s | 0,8 | 1,67 s |
  | Espingarda (8 chumbos) | 6 | 0,92 s (com a bomba) | 0,5 por chumbo | 0,54 s por cartucho |

  A vida dos zumbis é contada em tiros de pistola (dano 1). Na cabeça, qualquer acerto explode a cabeça.
- **O tiro:** um raio do centro da tela (um por chumbo na espingarda), com dispersão em volta do olhar. O raio atravessa até dois braços (numa multidão, as mãos de quem ataca ficam na frente) e confere os primeiros 35 cm à parte (à queima-roupa).
- **Quem decide:** quem atira vê o acerto na hora (sangue, a marca na mira) e manda o acerto para o anfitrião, que confere e aplica o dano.
- **O suporte de armas** (`ParedeDeArmas`, um objeto de rede da cena): 4 vagas com uma arma deitada em cada. Mirando uma arma de perto, E pede a troca; o anfitrião confere a distância e troca a vaga e a arma do jogador. O conteúdo das vagas vai pela rede num número só (8 bits por vaga).
- **As animações:** cada arma tem os seus braços de primeira pessoa (um FBX por arma) com o mesmo Animator, trocando os clipes por um override. O corpo usa outro override para as armas longas.

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

  No máximo 40 zumbis pensam por quadro, em rodízio. Quem persegue, ataca ou levou tiro há menos de 5 s fica sempre no Perto.
- **Os 4 modelos:** cada um tem o seu pool. O modelo de cada zumbi é calculado do Id e da geração dele (`(id * 7 + geração * 3) % 4`), então anfitrião e clientes chegam ao mesmo sem mandar nada.
- **Rede (`ZombieManager.Rede.cs`):** o anfitrião manda a cada cliente só os zumbis perto dele (até 15 m, 10 vezes por segundo; de 15 a 40 m, menos), 13 bytes por zumbi, numa mensagem não confiável. O cliente mostra cópias interpoladas um pouco no passado. Não há evento separado: a morte, os golpes e os tiros levados estão no estado de cada zumbi.
- **Morte (`CorpoDeZumbi`, `ZombieManager.Corpos.cs`):** o zumbi some, e um corpo com física (11 partes com juntas) cai no lugar, empurrado pelo tiro. Os corpos são só visuais (cada máquina tem os seus) e voltam ao pool afundando depois de 15 s.

### Efeitos e som (`Combat/`)

- **`Efeitos`:** os sons gerados por código (o tiro de cada arma, clique, carregador, bomba, cartucho e lanterna), o clarão do cano (criado a cada tiro) e as faíscas. O som espacial fica mais abafado quanto mais longe.
- **`SomDeTiro`:** o tiro sintetizado em camadas, com um perfil por arma e 3 variações de cada.
- **`Sangue`:** respingo, explosão da cabeça, jorro do pescoço e manchas no chão (decals), tudo em pools.

### A cidade, o clima e o dia (`World/`, `Atmosphere/`)

- **`CicloDoDia`:** o anfitrião manda na hora (começa às 21 h; um dia leva 24 minutos). `CicloDoDia.Noite` (0 a 1) é lido por quem precisa.
- **`AmbienteDoDia`:** troca a neblina, a luz e os volumes entre o dia e a noite. Embaixo da terra (o bunker) a luz de fora some; dentro das casas (`AreaInterna`) a neblina fica rala.
- **`PosteDeLuz`:** os 28 postes acendem com a noite, alguns piscam e alguns estão quebrados.
- **`ParticulasNoAr`:** cinza de dia e poeira à noite em volta da câmera.
- **`NeblinaDeAltura.shader`:** uma neblina que engrossa perto do chão, com manchas arrastadas pelo vento, num passe de tela cheia.
- **A colisão do chão** são caixas invisíveis; o que se vê são os 25 pedaços de chão do Blender.

## O que vai pela rede

| O quê | Como | Quem escreve |
|---|---|---|
| posição e giro do jogador | NetworkTransform | o dono |
| para onde olha, se está mirando, a lanterna | NetworkVariables | o dono |
| a cor da família e a arma da mão | NetworkVariables | o anfitrião |
| o tiro (os impactos de cada chumbo) | `TiroRpc`, para os outros | o dono |
| o acerto num zumbi | `AcertoRpc`, para o anfitrião | o dono (o anfitrião confere) |
| os zumbis perto de cada jogador | mensagem nomeada `ZCC.Zumbis` | o anfitrião |
| a hora do dia | a cada 1 s | o anfitrião |
| o suporte de armas e o pedido de troca | NetworkVariable e `TrocarRpc` | o anfitrião |
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
