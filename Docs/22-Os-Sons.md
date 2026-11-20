# Os sons: o que mudou e o que apagar (20/11/2026)

Para o Gustavo e o Claude dele, depois do `git pull` da `develop`. Entre 19 e 20/11/2026 o Pedro trocou quase todos os
sons feitos em código por sons gravados (pacotes grátis da Asset Store e sons CC0 do sfxjar.com, que não pedem crédito).
Os sons antigos que foram substituídos **saíram do jogo**: o pull apaga os arquivos que o git rastreia, mas confira a
lista da seção 2 e apague à mão o que ainda existir aí (uma cópia local, outra branch, um merge que trouxe de volta).

## 1. Como os sons funcionam agora

- **Onde ficam:** `Assets/_Game/Audio/Resources/<pasta>/` (Agua, Ambiente, Armadilhas, Armas, Arvore, Bichos, Bunker,
  Carro, Clima, Comida, Corpo, DiaZero, Fogo, Fumo, Golpes, Interface, Passos, Pesca, Portas, Vozes...). Fora de
  `Resources`, ligados por referência nos assets: `Audio/Carro` (o alarme), `Audio/Motos`, `Audio/Hospital` e
  `Audio/Trilha` (a música da trilha, que **não mudou**; só o vento dela foi refeito com ventos gravados).
- **Como o código acha:** `Combat/SonsGravados.cs`, pelo **começo do nome** do arquivo. `SonsGravados.Um("Corpo",
  "baque_")` sorteia entre `baque_1`, `baque_2`...; `Varios` devolve todos. Por isso um nome não pode começar com o nome
  de outro da mesma pasta: a buzina da perua é `perua_buzina` (e não `buzina_antiga`, que entraria no sorteio da
  `buzina`), e a do cupê e da caminhonete é `outra_buzina`.
- **Os bichos:** `Fauna/SomDosBichos.cs` carrega `Resources/Bichos/<o nome do SomDoBicho>`. O enum vai pela rede num
  byte: som novo entra **no fim** (o último é o `Guaxinim`, de 20/11/2026).
- **Quem toca:** quase tudo passa pelo `Combat/Efeitos.Som` (um conjunto de fontes de 24 a 64, sem categoria). A chuva e
  o trovão (`World/ClimaNaTela`), a água (`World/SomDaAgua`), o fogo (`Combat/SomDasChamas`), os carros e as motos, o
  Dia 0 (`DiaZero/NpcDoDiaZero`, `NpcsDoDiaZero`), a TV e o rádio do bunker (`World/LazerDoBunker`) e a comida
  chiando no fogo (`Loot/ConstrucoesNoMundo.Assar`) têm as fontes próprias. A música e o vento da trilha seguem o volume
  da Música (`MaestroDaTrilha.Volume`); os bichos e os galhos do ambiente, o `VolumeDoAmbiente`. Um mixer de som mais
  detalhado está sendo feito na sessão do menu de criação.
- **Os pacotes da Asset Store** de onde os sons saíram (Nature Sound FX, Footsteps - Essentials, Nature - Essentials,
  Rain Forest SFX, Animal Sound Effect Pack Lite, Voices - Essentials, Vehicle_Essentials, RegularImpactsSFX, Deadly
  Kombat, FreeWeaponSFX, Weapons of Choice e no9_Game_Sounds) **não estão no git** (quase 1 GB) e não fazem falta: o
  jogo só usa as cópias preparadas em `Audio/Resources`. Estão no `.gitignore`. Para mexer neles, importe da Asset Store
  (são grátis).
- **A motosserra:** um dos sons do sfxjar tinha a árvore caindo com a motosserra ligada. Ele **não entrou**: o tombo da
  árvore (`Arvore/tombo_*`) foi montado com galhos estalando, a madeira rangendo e as batidas no chão, sem a motosserra.

## 2. O que saiu (apague se ainda existir aí)

- **A pasta `Assets/_Game/Audio/Ambiente/` inteira:** os 26 sons de ambiente feitos em código (Corvos, Grilos, Janela,
  Lata, Latido, Madeira, Placa, Porta e Uivo, `_01` a `_04`) e o `Ferramentas/ambiente.py`, que os gerava. Junto saíram
  os campos deles do `Music/TrilhaDoJogo.cs` (`grilos`, `latidos`, `uivos`, `janelas`, `portas`, `placas`, `latas`,
  `madeira`, `corvos`), as listas no `Audio/Resources/TrilhaDoJogo.asset` e a montagem no
  `Editor/MontagemDaTrilha.cs`. O ambiente toca só os gravados de `Audio/Resources/Ambiente` (Passaros, Corvos, Coruja,
  Lobos, Galhos e Grilos).
- **Os trovões e o estalo feitos em código**, em `Audio/Resources/Clima/`: `estalo_1` e `estalo_2`, `trovao_longe_1` a
  `trovao_longe_4`, `trovao_medio_1` a `trovao_medio_3` e `trovao_perto_1` a `trovao_perto_3`. Os gravados são os de
  número 4 em diante (`trovao_longe_5`, `trovao_medio_4`, `trovao_perto_4`...).
- **As reservas feitas em código** que ficavam atrás dos gravados (o som de código só tocava se o gravado faltasse):

  - **o tiro feito em código inteiro** (`Combat/SomDeTiro.cs` e o campo `som` do `DadosDaArma` em `Combat/Armas.cs`): as
    5 armas têm `Armas/<arma>/tiro_*`, e as de sucata usam os da arma real;
  - no `Combat/Efeitos`: o clique, o pente, a bomba, o cartucho, o ferrolho, a luneta, a lanterna, o interruptor, as
    mordidas, os goles, os socos e os passos de concreto, madeira e grama (o carpete continua feito em código);
  - a árvore (`SomDaMadeira`: o machado e o tombo), a mente (`SomDaMente`: o coração e o sussurro), as armadilhas (as
    latas, as estacas e o martelo), o fogo (`SomDoFogo`: o galão, o vidro e a labareda), o isqueiro e a chama
    (`SomDoFumo`), os curativos (o osso e o zíper), o Dia 0 (`SonsDoDiaZero`: as tosses, o cortador, o carro passando e a
    mangueira), as portas das casas (abrir e fechar), a TV e o rádio do bunker, o baque da queda e do golpe, a catraca do
    molinete, a respiração da febre, os estalos de lenha das fogueiras (o laço gravado do fogo já tocava);
  - no carro (`SomDoCarro`): o arranque, o motor pegando, a chave, o pneu furando, as portas, a buzina, o ar vazando e o
    vapor;
  - os 21 sons de bicho que têm pasta em `Resources/Bichos` (os geradores deles saíram do `SomDosBichos`; um som com
    pasta e sem arquivo fica calado).

- **Não volte os sons antigos** dos arquivos que foram trocados com o mesmo nome. A versão nova é a certa:
  `Audio/Carro/Alarme_01..03`, `Audio/Hospital/*`, `Audio/Motos/Cross_Pegar` e `FatBoy_Pegar`,
  `Audio/Resources/Agua/Agua_Entrar`, `Arvore/Estalo_01..03`, `Clima/chuva*`, `Clima/poca_1..5`, `Clima/zumbido` e
  `Audio/Trilha/vento/vento_dia_*` e `vento_noite_*` (com as rajadas medidas de novo no `trilha.json` e no
  `TrilhaDoJogo.asset`).
- **Se um merge der conflito** num desses arquivos, fique com a versão da `develop`. Se a Unity mostrar um som
  "Missing" no `TrilhaDoJogo`, é do ambiente antigo: pode ignorar.

## 3. Para quem for mexer nos sons

- Antes de pôr um som de código, procure um gravado (o Pedro baixa do sfxjar.com e dos pacotes grátis).
- O nome do arquivo novo não pode começar com o nome de outro da mesma pasta (ver a seção 1).
- O preparo dos sons (cortar, igualar o volume, fazer o laço) foi feito com scripts Python na máquina do Pedro
  (`D:\Zombiegame\Trabalho-Claude`, fora do repositório); os originais e os antigos ficaram guardados lá.
- O menu F1 (o do desenvolvedor) tem a lista "Sons para ouvir" com os gravados por grupo
  (`UI/MenuDoDesenvolvedor.Fauna.cs`, `SonsParaOuvir`).

## 4. O que ainda falta (20/11/2026)

Os sons que ainda são feitos em código (não há gravação) ou que não existem:

- **Os veículos:**
  - a caminhonete tem a marcha lenta própria (o motor de caminhão do sfxjar), mas acelera com os giros do cupê; faltam
    os giros dela (1000 e 2000 rpm);
  - a partida, o motor pegando, o desligar e as portas são os mesmos para todos os carros;
  - as motos não têm o arranque nem o desligar próprios;
  - feitos em código: o solenoide e o pinhão soltando do arranque, o motor tossindo (pane), o macaco, o abastecer com o
    galão, o capô abrindo e a tampa do porta-malas;
  - não existem: o freio, o pneu cantando (derrapagem) e o cascalho embaixo das rodas;
  - dos que o Pedro mandou, o `car-engine-2` (6,5 s acelerando e voltando) e a moto 75 cc (uma acelerada só) não
    entraram: nenhum dos dois tem um trecho de giro parado para virar o laço do motor.
- **As portas:** a trancada (forçar), a gazua e o pé de cabra.
- **O corpo e a mente:** o desmaio; o calafrio e os dentes batendo (quando a voz do personagem não tem); o rasgo, o
  enrolar, o velcro e a cartela dos curativos; o spray do tratamento.
- **O cigarro:** sacudir o maço, a tragada, o sopro e o peteleco.
- **As armadilhas e a construção:** o barbante arrebentando, os pregos e as tábuas arrancadas.
- **As armas:** a recarga genérica; a sucata (bater, desmontar, soltar); o soco na parede.
- **Os itens:** abrir a lata, abrir a tampa e largar no chão.
- **A pesca:** o plop da isca e a linha arrebentando.
- **A sinuca:** a tacada, a tabela, a caçapa e as bolas.
- **A interface:** o bipe do ping e o das falas dos NPCs.
- **Os passos:** o carpete (feito em código); não há terra, cascalho, folhas nem metal.
- **Os bichos:** a mordida, o guincho do coelho, a patada do urso, o espirro do gambá e o guaxinim no lixo (a lata, a
  sacola, remexer e a tampa da caçamba).
