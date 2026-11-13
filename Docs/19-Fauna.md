# A fauna dos EUA (12/11/2026)

O pedido: a fauna americana no mapa que já existe (o lago e a mata), com a cadeia alimentar à vista, a caça (o tiro, a
carcaça, carnear com a faca, assar na fogueira) e os peixes americanos. O plano foi aprovado pelo Pedro em 5 etapas:

1. **a base e o veado** (feita, 12/11/2026): o gerenciador, a rede, a mente, os sentidos, o povoamento por região, a caça;
2. **os predadores** (feita, 12/11/2026): lobos e coiotes em matilha, os ursos e o coelho (a seção da etapa 2, abaixo);
3. **a água** (feita, 12/11/2026): os peixes americanos, o salmão, o urso pescando, o castor, o alce, os patos, os gansos e a
   águia (a seção da etapa 3, abaixo);
4. **a noite e os detalhes** (feita, 12/11/2026): o sapo, a cascavel, o guaxinim no lixo, o gambá (o cheiro chama os
   zumbis), o urubu nos restos, o esquilo, o peru, os vaga-lumes e os olhos na lanterna (a seção da etapa 4, abaixo);
5. o futuro: domar filhotes (só os ganchos), o veado parado no farol, a batida do carro.

As decisões do Pedro: a população cai com a caça, **nunca a zero**, e volta sozinha ("não quero que morram para sempre");
os zumbis não atacam os bichos, e os bichos fogem dos zumbis; os bichos não morrem de fome; o ambiente não favorece o
jogador, e a sorte conta (a investida do alfa é uma chance). **Os modelos são provisórios**: os três cervos vêm do
Blender, e o resto são caixas simples, só para diferenciar ("depois eu vou trocar todos por outros modelos").

## O que existe (a etapa 1)

- **`Fauna/Especies.cs`**: as 19 espécies (`Especie`), as variantes (adulto, macho, fêmea, filhote), o corpo (quadrúpede,
  ave, sapo, cobra, peixe), o jeito de viver (rebanho, matilha, sozinho, pequeno) e os `DadosDaEspecie`: os tamanhos, as
  velocidades, os sentidos (visão, ouvido, faro), a distância de fuga, o tamanho do grupo, o que a carcaça dá, os habitats
  e as marcas `noturno`, `domavel` e `noMundo`. Saem sozinhos no mundo o veado (etapa 1), o lobo, o coiote, os dois
  ursos e o coelho (etapa 2); as outras espécies existem para o F1 e as próximas etapas.
- **`Fauna/FaunaDoMundo.cs`** (um MonoBehaviour comum, não um NetworkBehaviour): nasce pelo `VigiaDaFauna` quando a rede
  está ouvindo e o `MundoDaPartida` está gerado e pronto. No anfitrião, ele pensa (de 0,3 a 3 s, pela distância), anda
  (até 170 m) e anima (até 140 m) cada bicho; nos clientes, segue as cópias.
  - `Acertar` (o tiro e o golpe), `Carnear`, `Matar`, `Golpear` (o ataque num jogador; a cabeçada do alfa);
  - o F1: `PorPerto` (um grupo a ~18 m, **distraído por 10 s**: o veado foge a 40 m), `FilaDeTodos` (um de cada espécie,
    parados lado a lado a 9 m), `MatarOMaisPerto`, `TirarTodos`, `Resumo`.
- **`Fauna/FaunaDoMundo.Rede.cs`**: a mensagem com nome "ZCC.Fauna" (anfitrião → clientes), 13 bytes por bicho: o id, a
  geração, a espécie, a variante, a posição em cm (relativa ao jogador que recebe), o giro, o estado (com os bits ferido,
  carneado e alfa) e a velocidade. Perto (até 30 m) vai todo pacote; até 90 m, um a cada 3; mais longe, um a cada 6. A
  cópia some se fica 2 s sem notícia. Os pedidos dos clientes (o acerto e o carnear) são RPCs do `JogadorRede.Fauna.cs`.
  **A `VersaoDaRede` foi para 22.**
- **`Fauna/FaunaDoMundo.Povoamento.cs`**: os veados nascem de 95 a 190 m dos jogadores (2 rebanhos por jogador, na beira
  da mata) e somem a 270 m. A população de cada região de 512 m cai com a caça (o piso é 15%) e volta 3,5% por hora do
  jogo (~1 dia para encher); fica no save do mundo (`MundoSalvo.fauna`).
- **`Fauna/FaunaDoMundo.Sentidos.cs`**: o faro com o vento (o `_ZccVento`), o ouvido (os passos, agachado, o Passos leves),
  a visão (a noite, a lanterna, embaixo das árvores, a linha de visão) e os zumbis (os não petrificados: 24 m de dia, 14 m
  à noite). Os bichos ouvem os barulhos do `ZombieManager.Barulho` no dobro do raio.
- **`Fauna/Bicho.cs` e `Bicho.Mente.cs`**: o bicho e a mente. O rebanho pasta, troca de pasto, faz a vigia (levanta a
  cabeça e olha em volta), deita à noite; o perigo no meio do caminho (da fuga à visão) dá o alerta (para, olha; a chance
  de fugir cresce com a aproximação); perto demais, foge (o grupo foge junto, atrás do líder). O alfa, com filhote no
  grupo, às vezes investe (`ChanceDaInvestida` 35%, 45 s entre uma e outra). O ferido sangra (o rastro de sangue no
  chão), foge mais cedo e pode morrer longe; às vezes estanca.
- **`Fauna/ModelosDosBichos.cs` e `AnimacaoDoBicho.cs`**: os modelos e a animação feita na hora (andar, trotar e galopar
  pelas fases das patas, a cabeça pela pose, a cauda em bandeira do veado, as orelhas, as aves, a cobra, o peixe, o sapo,
  a morte). Os cervos vêm dos prefabs `Art/Fauna/Resources/Fauna/Cervo_Macho|Femea|Filhote` (o `Blender/Fauna/familia.py`
  -- unity e o menu Zombie City Cleaner > Fauna > Montar os cervos); sem eles, e para as outras espécies, as caixas. Cada
  parte tem um colisor na camada 14 (Fauna) e um `ParteDoBicho` (a cabeça vale mais no tiro).
- **A caça**: o tiro e o golpe nos bichos (`ArmaJogador.Fauna.cs`); morto, o bicho vira a carcaça (`Carcaca`): com a faca
  na mão, segure E (8 s no veado; o filhote, 60%). O veado dá 4 carnes cruas, 1 couro e 1 gordura (o filhote, a metade);
  o que não cabe fica no chão. XP: 6 na caça grande, 1 na pequena, 2 por carnear. A carcaça apodrece em 48 h do jogo; a
  carneada some logo.
- **Os itens** (`Loot/Itens.cs`, 134 a 137): a carne crua (pode fazer mal, como o peixe cru), a carne assada (a receita de
  fogo: 1 crua na fogueira acesa), o couro e a gordura (para receitas futuras). Os modelos são provisórios, feitos no Play
  pelo `Loot/ItensDaCaca.cs`: caixinhas pintadas pela paleta, e o ícone e a foto desenhados delas.
- **O F1, aba Fauna** (`UI/MenuDoDesenvolvedor.Fauna.cs`): o resumo, um botão por espécie, matar ou ferir o mais perto, a
  faca na mochila, 3 carnes cruas, tirar todos, ver os nomes e os estados em cima dos bichos, a fila de todos e a região
  de volta a 100%.

## O teste no Play (12/11/2026)

Três roteiros (`D:\Zombiegame\Trabalho-Claude\fauna\teste_fauna*.cs`, fotos e registros em `play`, `play2`, `play3`):
o povoamento natural (16 veados em volta), o rebanho do F1 (distraído pasta a 10–18 m; sem a distração, foge na hora),
o alerta e a fuga a 9 m, o tiro (pelo caminho do dono, `AcertarBicho`), a carcaça, o carnear pela rede com a mochila
(4/1/1), o ferido fugindo e sangrando, a região a 95% depois de uma morte e a fila das 19 espécies. 0 erros.

Dois defeitos achados e corrigidos no teste: os 4 itens novos entraram sem nome de modelo e ficavam sem tamanho (não
cabiam na mochila, e a carne caía no chão); e o cubo das caixas estava do avesso (o triângulo 0-2-1: de fora se via o
lado de dentro), conferido com o `RecalculateNormals`.

## A etapa 2: os predadores (12/11/2026)

- **O papel na cadeia** (`Especies.Papel`): a presa foge (`Bicho.Mente.cs`, o `PensarPresa`), a matilha caça
  (`Bicho.Matilha.cs`) e o urso avisa e ataca (`Bicho.Urso.cs`). O `Bicho.Pensar` escolhe pela espécie. Cada espécie tem
  agora `gruposPorJogador`, `raridade` (a chance de um grupo que falta nascer a cada volta do povoamento: o lobo 0,02, o
  coiote 0,035, o urso-negro 0,015, o pardo 0,01; o coelho 0,4, até 3 grupos) e `mordida`.
- **Os sentidos** (`FaunaDoMundo.Sentidos.cs`): a parte das pessoas virou o `PerceberGente`. A presa foge do predador que
  caça (correndo: notado até a visão dela) ou que passa perto (60% da distância de fuga); o coiote foge do lobo e do urso;
  o lobo, do urso bem perto (`Teme`).
- **A matilha** (o lobo, 4 a 7 com o casal alfa e às vezes um filhote; o coiote, 3 a 5):
  - vaga atrás do líder (o pasto novo a 40–110 m) e deita de dia;
  - **a caça entre eles** (`TentarCaca`, a cada 15–30 s do líder): só com alguém a até 160 m para ver (os bichos não
    morrem de fome), por sorteio (de dia 15%, à noite 50%), a presa mais fácil a até 100 m (o filhote e o ferido valem
    como se estivessem mais perto; o lobo caça o veado, o coelho e o filhote do alce; o coiote, o coelho e o filhote do
    veado). À noite o lobo uiva antes (o uivo tira 2,5 da mente de quem está a até 250 m). A matilha **espreita** devagar
    de cabeça baixa até uns 32 m e então dispara de lado, cercando; cada um corre com o seu fôlego (80–97% da corrida) e
    cansa depois de 15 s; a presa cansa depois de 10 s de fuga. A mordida na presa a faz sangrar e fugir até cair (a
    morte natural não baixa a população da região). Depois come na carcaça de 1 a 3 minutos (o `Bicho.Restante` cai:
    carneada depois, dá menos carne e gordura; abaixo da metade, o couro rasgou);
  - **a gente**: de dia foge (a distância de fuga); à noite fica olhando de longe e acompanha a uns 30 m. Caçando, não
    para para olhar quem está longe e só foge de quem chega a menos da metade da fuga;
  - **o cerco** (`PodeAtacar`): só à noite (o escuro acima de 0,55), em matilha (2 adultos; o coiote, 3), quem está
    ferido (sangrando ou com menos de 50 de vida) ou sozinho (ninguém a 30 m), fora do carro e do teto, longe do fogo. O
    sorteio a cada 5 s (o lobo 60%, o coiote 35%, mais 20% no ferido) começa com o rosnado e o susto (4 a 7 na mente). A
    roda a 9 m (o coiote, 11 m) gira devagar; o líder manda um de cada vez dar o bote (a cada 2,5–5,5 s; o coiote 4–8 s),
    o que está mais atrás de quem é cercado. A mordida vai nas pernas (65%) e nos braços (o `TipoDeGolpe.Mordida`: a
    ferida e a infecção do sistema dos membros). No fogo (a lista de fogos do `ZombieManager`), a roda fica na beira dele e
    ninguém morde. Largam com o dia, o carro, o teto, a queda do jogador e a matilha sem 2 adultos;
  - **o tiro** espanta com chance (por tiro, a matilha toda: o coiote 55%, o lobo 25%); o ferido foge; **matar o alfa**
    (ou o líder) desfaz a matilha: todos fogem e não cercam ninguém por 5 minutos.
- **O urso** (o macho sozinho; a mãe com 1 ou 2 filhotes, que vão atrás dela e se escondem nas costas dela no perigo):
  - fuça o chão e dorme à noite (os filhotes deitam junto);
  - **quando ataca**: provocado (ferido por alguém: o negro 50%, o pardo 85%, direto, sem aviso), assustado de perto
    (alguém a menos de 12 m sem ele ter notado gente por 20 s: 35% / 70%) ou alguém a menos de 20 m dos filhotes
    (60% / 90%). Um sorteio por encontro. De resto, vai embora (o negro 65%, o pardo 35%) ou fica olhando de pé, e quem
    insiste chegando a 15 m pode levar o aviso;
  - **o aviso**: de pé, bufa e bate os dentes (1,5 a 3,5 s; o susto de 4 a 6 na mente). Quem se afasta a mais de 25 m o
    acalma; quem chega a 5 m ou sai correndo (70%) faz ele vir;
  - **o falso ataque** (o negro 50%, o pardo 30%; perto do fogo, metade das vezes o fogo o segura): corre e para a 3–5 m e
    volta a avisar;
  - **o ataque**: o rugido, a patada (60%, o corte) ou a mordida, de 2 a 4 golpes (a cada 1,1–1,7 s), com o empurrão que
    joga longe (o `JogadorRede.EmpurraoDoBicho`: o `Arremessar` e o tremor na máquina do dono); o pardo quebra um osso
    em 12% (o negro, 5%). Depois vai embora. Abaixo de 30% da vida, foge. O tiro faz o negro desistir às vezes (30%);
- **O golpe no jogador** (`FaunaDoMundo.Golpear`): o dano pela `mordida` da espécie (a fêmea 85%, o filhote 40%); a
  cabeçada do veado e do alce no tronco, com o empurrão.
- **Os sons provisórios** (`Fauna/SomDosBichos.cs`, feitos em código como os do `Combat/Efeitos.cs`): o bufo do veado (no
  alerta e na fuga), o uivo do lobo (ouvido a 420 m), o coro do coiote, o rosnado, a mordida, o bufo e os dentes do urso,
  o rugido, a patada e o guincho do coelho. O anfitrião toca e manda a quem está no alcance a mensagem "ZCC.FaunaSom"
  (`FaunaDoMundo.Sons.cs`). **A `VersaoDaRede` foi para 23.**
- **A animação**: o urso fica de pé no alerta (o corpo gira em volta do quadril; as patas de trás a prumo), e a matilha
  espreita de cabeça baixa.
- **O F1**: "Lobos caçando" e "Coiotes caçando" (as presas a 45 m na frente, a matilha a uns 70 m espreitando a mais fácil,
  e ninguém nota quem pediu por 45 s) e "A matilha mais perto me cerca" (vale de dia). O "Meia-noite" do F1 serve para
  ver a matilha decidir sozinha. Os nomes em cima dos bichos dizem "caçando", "cercando", "dando o bote", "comendo",
  "farejando", "de pé, avisando" e "atacando".

**O teste no Play (12/11/2026, `teste_fauna4.cs` e `teste_fauna5.cs`, fotos em `play4` e `play5`):** 0 erros. Os lobos
espreitaram, dispararam e derrubaram um filhote de veado em 18 s e comeram (a carcaça carneada depois deu 2 carnes e 1
couro, sem gordura); os coiotes pegaram um coelho depois de 20 s de zigue-zague. O cerco de dia pelo F1 mordeu de 10 a 12
por vez; matar o alfa fez os outros fugirem. À noite, a matilha posta pelo F1 decidiu sozinha cercar e morder. A ursa-parda
de surpresa ficou de pé, avisou e deu 4 patadas de 38 (com sangramento) e foi embora; a ursa-negra baleada veio direto, deu
2 patadas de 25 e saiu. No primeiro teste, a matilha parava para olhar o jogador longe em vez de caçar, e a presa a menos
de 40 m fugia dele: a caça passou na frente de olhar a gente, entrou a espreita e o F1 pôs tudo mais longe.

## A etapa 3: a água (12/11/2026)

- **Os peixes americanos** (`Loot/Itens.cs`, os mesmos números: o save não perde nada): a perca-sol (era o lambari), a
  truta-arco-íris (a tilápia), o black bass (o tucunaré) e a truta-dourada (o dourado). Os modelos e os ícones ainda são os
  antigos. **O bagre** é novo (o item 138): no fundo e à noite (de 3% no raso de dia a ~30% no fundo de noite,
  `PescaJogador.SortearPeixe`), assa na fogueira (2 peixes assados), faz mal cru como os outros, os comerciantes compram
  (8). O modelo, o ícone e a foto são provisórios (`Loot/ItensDaCaca.cs`). O Peixe grande e o Mestre pescador falam dos
  nomes novos.
- **O meio de cada um** (`Especies.Meio`): terra, água (o peixe: a terra é parede), anfíbio (o alce, o castor; as aves da
  água boiam) e ar (a águia). A altura de cada um (`Bicho.Agua.cs`, `AlturaDe`): o peixe uns 30 cm embaixo da superfície, a
  ave boiando, o anfíbio nadando onde não dá pé (o lombo a 80% da altura), o castor mergulhado, a ave voando. Os clientes
  acham a pose de nadar e de voar pela posição.
- **O lago** (`FaunaDoMundo.Agua.cs`): o mundo gerado tem um lago (`DadosDoMundo.Lago`). Os pontos na água pela fundura, a
  margem, a água mais perto. Os da água nascem no lago, se ele estiver no anel do povoamento (`LugarNaAgua`).
- **O salmão**: cardumes no raso (0,6 a 1,8 m), pulando de vez em quando (o respingo); foge de quem chega a 4 m.
- **O urso pescando** (`Bicho.Pesca.cs`): de dia, calmo, com um cardume a até 70 m, às vezes vai pescar: entra no raso (até
  0,9 m), espera de cabeça baixa e dá a patada na água (de 4 a 9 s); com um salmão a até 2,8 m, pega em 40%; leva até a
  margem e come (15 a 30 s).
- **O castor** (`Bicho.Castor.cs` e `FaunaDoMundo.Castores.cs`): a família vive na toca (no máximo 2 no lago, no raso),
  que cresce com os dias (5% por dia) e com os galhos (1,2% cada), e fica no save (`MundoSalvo.tocasDosCastores`). Um
  adulto vai roer uma árvore da beira (a até 35 m da toca): o tempo é da árvore (90 s somando os castores); ela cai pelo
  sistema do machado (`MundoDaPartida.DerrubarPeloCastor`, sem toras) e fica o toco roído em ponta de lápis
  (`MundoSalvo.arvoresRoidas`). Depois leva um galho até a toca. Assustado (a 15 m), corre para a água, bate a cauda e
  mergulha por 10 a 25 s. A represa fica para quando houver riacho.
- **As casas dos bichos** (a toca, o toco roído, o ninho): desenhadas em cada máquina (caixas); a mensagem "ZCC.FaunaCasas"
  manda a cada 3 s as que estão a até 320 m de cada cliente.
- **O alce**: o macho sozinho ou a mãe com o filhote (às vezes dois), na margem; come no raso metade das vezes e atravessa
  nadando. A mãe com filhote investe em quem chega a 22 m (60%), de orelhas para trás, e pisoteia (35, com o empurrão); o
  macho investe quando é ferido (60%). O mugido no alerta e na investida.
- **Os patos e os gansos**: o bando boia e mergulha a cabeça; com o tiro, gente a menos da fuga (25 m; o ganso 18 m), o
  zumbi ou o predador, decola grasnando (de 12 a 24 m de altura), voa uns 10 a 16 s e pousa em outro ponto do lago, longe
  do perigo (o bando junto). Metade dos bandos de gansos tem **o ninho** na margem: a fêmea choca, e o macho avança
  grasnando (de asas abertas) em quem chega a 9 m dele, bica (3, nas pernas, com um empurrão) e volta; larga quem se
  afasta a 15 m. **O V dos gansos**: de 4 a 9 minutos, de dia, um V de 7 a 11 passa a ~55 m por cima de um jogador.
- **A águia-careca**: roda a 40 m sobre o lago; de 25 a 70 s, mergulha num ponto fundo, sai com o peixe nas garras
  (o bit "carregando" vai pela rede) e volta a rodar depois de 15 a 25 s. O tiro perto a faz subir e ir embora.
- **Os sons provisórios novos**: o respingo, o pato, o ganso, o grito da águia, a cauda do castor e o mugido do alce.
- **O F1**: "Ir para o lago" (a margem mais perto), "Gansos em V", "Urso pescando" (um cardume no raso e um urso-pardo na
  margem, que vai pescar) e "Castor rói rápido" (a árvore cai em ~8 s de roída). Os botões das espécies da água põem o
  grupo no lago, perto de quem pediu.
- **A `VersaoDaRede` foi para 24.**

**O teste no Play (12/11/2026, `teste_fauna6.cs`, fotos em `play6`):** 0 erros. A ursa-parda entrou no raso, pegou um
salmão em 20 s e foi comer na margem; os patos decolaram com o tiro (a 15–25 m de altura) e pousaram de novo no lago; o V
de 8 gansos passou a 55 m; a águia rodou a 40 m, mergulhou e saiu com o peixe; 4 castores roeram uma árvore até cair (o
toco roído apareceu), levaram os galhos e, assustados, correram para a água e mergulharam; a mãe alce investiu a 7 m e
pisoteou (−27). O ninho dos gansos não saiu nesse teste (a margem era procurada a até 35 m do bando, que boiava mais
longe): agora ele vai na margem mais perto, na direção do bando. **Essa correção ainda não foi vista no Play.**

## A etapa 4: a noite e os detalhes (12/11/2026)

- **Os bichos da noite** (`FaunaDoMundo.Noite.cs`): o sapo, o guaxinim e o gambá só nascem à noite; de dia, os que sobraram
  somem quando ninguém está a 60 m.
- **O sapo** (`Bicho.Noite.cs`): na beira do lago, parado, pulando de vez em quando. O coro coaxa e cala por 20 a 35 s
  quando alguém chega a 12 m; ele foge para a água. Come o vaga-lume que passa perto (a língua estica por um instante).
- **A cascavel**: enrolada nas pedras e no campo. Com gente a 5 m, o chocalho (o aviso); a 1,6 m, o bote. **O veneno**
  (`Player/VidaJogador.Veneno.cs`, sem item novo): uma em quatro picadas é seca; nas outras, o membro picado perde de 25 a
  40 de vida em 240 s, com a dor na mente e o enjoo. O curativo não cura (passa sozinho), mas o veneno pode matar quem já
  está fraco. O símbolo "Envenenado" fica até passar. Ela também dá o bote no sapo e o engole.
- **O guaxinim**: à noite, vai às lixeiras da cidade (`DadosDoMundo.Objetos`), derruba (o barulho da lata) e revira; o lixo
  espalhado fica um dia do jogo (vai na mensagem "ZCC.FaunaCasas"). Com gente perto, sobe na árvore.
- **O gambá** (o de listra): com gente a 6 m, o aviso (a cauda levantada, as patas batendo). Quem chega a 3 m, ou ainda está
  a menos de 4,5 m atrás dele quando o aviso acaba, leva o espirro. **O cheiro** (`Player/JogadorRede.Fauna.cs`, o
  `Fedor`): 10 minutos reais (na água sai 6 vezes mais rápido); enquanto dura, os zumbis sentem o jogador de 70 m
  (`ZombieManager.Cheiro`, sem o evento da fauna). O símbolo "Fedendo".
- **Os restos dos corpos** (`Zombies/RestosDosCorpos.cs`, a decisão do Pedro no plano): o corpo do zumbi que ia afundar
  vira um resto parado (a malha congelada na pose, sem física). Ficam os últimos 20, por 10 minutos reais, e então afundam
  devagar. Cada máquina tem os seus (os corpos são só visuais).
- **O urubu**: o bando chega a um resto (o corpo do zumbi ou a carcaça) uns 25 s depois e roda alto; desce para comer
  quando ninguém está a 30 m e sobe quando alguém chega. A carcaça perde um pouco a cada bicada; o bando vai embora quando o
  resto some.
- **O esquilo**: no chão perto das árvores; com perigo, corre para o tronco mais perto, sobe (de 3 a 6 m) e chia o alarme.
- **O peru**: o bando no campo (como a presa) e o glu-glu; à noite, dorme nas árvores.
- **Os vaga-lumes** (`Fauna/VagaLumes.cs`, em cada máquina): ao anoitecer e à noite, no campo, na rebrota e na beira da
  mata, até 70 pontinhos em volta da câmera (de 4 a 40 m), boiando perto do chão e piscando cada um no seu ritmo. A chuva
  apaga todos.
- **Os olhos na lanterna** (`Fauna/Bicho.Olhos.cs`, em cada máquina): à noite, quando a lanterna do jogador desta máquina
  aponta para o bicho (num cone de ~16°, até 70 m, sem nada na frente, o bicho virado para cá), os dois olhos brilham na
  cor de cada um (o veado verde-branco, o lobo e o coiote amarelo-verde, o urso laranja, o guaxinim amarelo...).
- **Os sons provisórios novos**: o coaxar, o chocalho, o chiado do esquilo, o glu-glu, a lata, o espirro e o urubu.
- **O F1**: os botões dos bichos novos e "Urubus no corpo mais perto" (o bando agora, sem esperar). Os bichos de terra que
  o F1 põe perto procuram o chão seco (antes, perto do lago, podiam cair na água).
- **A `VersaoDaRede` foi para 25.**

**Os testes no Play (12/11/2026, `teste_fauna7.cs` e `teste_fauna8.cs`, fotos em `play7` e `play8`):** 0 erros. Os
vaga-lumes acenderam à noite (7 em volta); a cascavel picou (o veneno de 240 s tirou vida aos poucos); o gambá avisou e
espirrou (o cheiro de 600 s); o guaxinim derrubou a lixeira (o lixo espalhado apareceu); os olhos do veado acenderam na
lanterna quando ele olhou para cá; 5 urubus chegaram à carcaça, rodaram a 35 m, pousaram com o jogador a 43 m, comeram e
subiram quando ele chegou; o esquilo subiu no tronco (a 3,2 m); os 9 perus dormiram nas árvores (a ~5 m). Duas correções
depois dos testes: o ganso macho do ninho nascia no lago com o bando (a ~50 m do ninho) e só via quem os sentidos
percebiam; agora nasce do lado do ninho e vigia quem chega a 9 m dele. E as aves não têm mais os olhos na lanterna (como
na vida real, quase não brilham).

## O que ficou para depois

- O "derrubar" do urso é o empurrão com o tremor (o jogador não fica caído no chão: não há queda sem morrer no jogo).
- O veado parado no farol do carro e a batida (a etapa 5).
- A etapa 5 (a lista no começo).
- Os modelos e os ícones dos peixes americanos (hoje, os dos peixes antigos).
- A represa do castor (quando houver riacho) e o salmão no rio do Gustavo.
