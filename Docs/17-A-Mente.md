# A mente: a sanidade (06/11/2026)

O pedido do Pedro (05 e 06/11/2026): um mecanismo de sanidade. Ela cai com o medo e volta com a segurança e a companhia.
A 0 não há game over: o jogador fica em colapso. A vida virou um monitor cardíaco, e a mente aparece como um cérebro.

É feita em três etapas:
1. **A mente:** o número, o que tira e o que devolve, o monitor cardíaco e o cérebro no HUD. Está feita.
2. **Os efeitos** (feita):
   - a crise de pânico: ofegante, as palpitações, a trilha ansiosa e o monitor disparado;
   - os vultos e os sons falsos (abaixo de 40);
   - o colapso (abaixo de 15): os amigos vistos como zumbis, os zumbis falsos que viram fantasma no tiro, a fuga em pânico
     (até 6 s) e o desmaio. O desmaio tem o som do ouvido tampando, e um amigo acorda você com o E.
3. **O sono e os itens** (feita):
   - dormir de verdade: um dormindo recupera devagar e o tempo não passa; todos dormindo, a noite passa e a mente volta bem;
   - o calmante (item novo): 1 dose não dá nada além de acalmar; 2 seguidas deixam sonolento (a vista um pouco embaçada,
     escrito "Sonolento"); 3 fazem desmaiar e dormir;
   - o vício na nicotina: quem fuma muitos maços seguidos fica viciado; sem fumar, vêm os tremores e a mente cai;
   - a sinuca.

## O que mudou para quem joga (a etapa 1)

- **A barra da vida virou um monitor cardíaco**, no mesmo lugar (embaixo, à esquerda):
  - o traçado é um ECG de verdade (a onda P, o QRS e a onda T), desenhado pela varredura como no monitor do hospital;
  - **a cor é a vida:** verde acima de 60, amarelo de 30 a 60, vermelho abaixo de 30;
  - **os batimentos** (o número ao lado, em bpm) ficam em uns 70 com o jogador calmo e sobem:
    - correndo (até uns 130);
    - com a mente abalada;
    - perdendo sangue (o coração compensa);
    - com febre;
    - logo depois de um golpe.

    Acelerado, o RR diminui e as ondas ficam mais curtas e regulares.
  - Com a vida abaixo de 15, aparece às vezes uma batida fora do ritmo (a extrassístole).
- **A mente é um cérebro**, ao lado das barras da fome e da sede. Ele se enche de baixo para cima com a mente, numa cor
  para cada faixa:

  | Faixa | Mente | Cor |
  |---|---|---|
  | Calmo | 70 ou mais | azul-claro |
  | Abalado | de 40 a 70 | lilás |
  | Apavorado | de 15 a 40 | roxo, pulsando com o coração |
  | Em colapso | abaixo de 15 | vermelho, rachado e tremendo |

- **Em cima das barras aparece o porquê:** "ABALADO: ESCURO · ZUMBIS CAÇANDO". Quando só há o que acalma, aparece
  "ACALMANDO: FOGUEIRA · AMIGOS PERTO".

## O que abala

Por hora do jogo (1 minuto real):

| O quê | Quanto |
|---|---|
| O escuro (à noite sem luz nenhuma, ou o bunker com as luzes apagadas) | −4 |
| Zumbis caçando a até 15 m | −6, mais 2 por zumbi a mais (até −16) |
| Zumbis só investigando ou procurando perto, ou qualquer zumbi a até 12 m (parado, vagando ou se abrigando; a pedra não conta) | −2 |
| Sozinho à noite (com outro jogador na partida e ninguém a 20 m) | −2 |
| A tempestade, fora do teto | −2 |
| A dor (a entorse ou a fratura sem o analgésico) | −1,5 |
| A febre (a sepse) | −3 (−5) |
| Sangrando | −2 |
| A fome e a sede baixas (vazias) | −1 cada (−2) |

De uma vez:

| O quê | Quanto |
|---|---|
| O susto: um zumbi que começa a caçar a menos de 7 m | −4 a −10 |
| O Brutamonte saindo da terra perto | −12 |
| Um raio perto (em cima de alguém) | −5 (−10) |
| Um golpe (no máximo um a cada 1,5 s) | −1,5 |
| Uma fratura | −6 |
| Um amigo caindo a até 25 m | −15 |
| Matar uma pessoa (um sobrevivente) | −8 |

**A mente cai mais devagar** (06/11/2026, à noite, a pedido: "acho que está caindo muito rápido"): o que abala por hora
vale 65% dos números da tabela abaixo, e o que abala de uma vez (o susto, o golpe, o amigo caindo, matar) vale 75%
(`RitmoDasPerdas` e `RitmoDosSustos`, no VidaJogador.Mente.cs). O que acalma não mudou.

**O bunker é o abrigo** (06/11/2026, a pedido: "dentro do bunker, independente se há zumbis perto, que eu não perca
sanidade"): lá dentro nada tira da mente (nem os zumbis, nem os sustos, nem o escuro, nem o corpo), e o bunker aceso
acalma mesmo com zumbi por perto. **Fora dele**, com um zumbi caçando, investigando ou a até 12 m, a fogueira e o dia não
acalmam; os amigos continuam acalmando.

A luz que conta é a lanterna acesa (com pilha), a tocha acesa, o carro, a fogueira acesa a até 8 m e o bunker aceso. No
bunker vale a luz do andar onde o jogador está, e ele conta como fechado com o portão e a portinhola fechados.

## O que acalma

| O quê | Quanto |
|---|---|
| O dia | +2 por hora, até 85 |
| Os amigos a até 8 m | +3 por hora cada (até +6) |
| A fogueira acesa a até 8 m | +4 por hora |
| O bunker aceso (com a porta fechada) | +2,5 (+5) por hora |
| Cada tragada do cigarro | +2,5 |

- Uma noite sozinho no escuro, sem susto, leva a mente de 100 a uns 60. Com a horda e os sustos, ela vai para uns 30. Com
  os amigos e a fogueira, ela quase não cai.
- **No Dia 0 a mente não anda**, como o corpo. Os 5 anos acalmam tudo.
- **Quem renasce volta com a mente como estava**, mas com pelo menos 30 (não nasce em colapso).
- Ela vai no save do personagem.

## Os efeitos (a etapa 2)

Tudo é só de quem sente: os amigos não veem nem ouvem as alucinações dele ("você viu aquilo?").

**Abaixo de 70:** a mão treme um pouco (a mira).

**Abaixo de 40:**
- o coração se ouve baixinho a cada batida do monitor, e as bordas da tela escurecem no ritmo dele;
- uma respiração funda de vez em quando;
- **os vultos:** um Oco todo preto, parado na beira da vista (de 10 a 20 m), que some quando você olha para ele, com um
  sussurro de onde estava;
- **os sons falsos:** a voz de um Oco, um chamado ou um sussurro atrás ou do lado de você.
- Quanto mais baixa a mente, mais seguidos (de uns 45 s até uns 18 s).

**A crise de pânico** começa com um susto (a mente caindo de uma vez) abaixo de 40, ou sozinha abaixo de 25. Ela dura de 20
a 35 s:
- o coração dispara (uns 55 batimentos a mais no monitor);
- a palpitação fica alta e a respiração ofegante;
- a mira treme forte;
- a tela pulsa em vermelho escuro com o coração;
- em cima das barras aparece "CRISE DE PÂNICO".

**O colapso (abaixo de 15, e também em 0, sem game over):** a cada 18 a 35 s, um destes:
- **os amigos como zumbis** (de 8 a 12 s): o corpo do amigo some, e um Oco anda no lugar dele, na velocidade dele; o nome em
  cima da cabeça some. Com o fogo amigo ligado, o tiro pega no amigo de verdade;
- **os zumbis falsos:** de 1 a 3 Ocos vêm andando pela frente; o tiro que passa por um deles o vira fantasma (pálido, sobe e
  some com um sussurro), e o que chega perto some do mesmo jeito;
- **a fuga em pânico** (de 3 a 6 s): o personagem corre sozinho de costas para o que via, virando quando bate numa parede;
  aparece "FUGINDO EM PÂNICO";
- **o desmaio** (de 7 a 12 s; no máximo um a cada 2 minutos): o baque e o zumbido do ouvido tampando, a tela apaga e o
  som fica abafado. O corpo fica deitado para todos, e os zumbis continuam atacando. **Um amigo acorda você** mirando e
  segurando o E ("Segure E: acordar Fulano").
- Um susto no colapso também pode fazer fugir.

## O sono, o calmante, o vício e a sinuca (a etapa 3)

**O sono:**
- O cansaço sobe devagar com as horas acordado: só pesa no fim de **3 dias do jogo sem dormir** (06/11/2026, a pedido: "O
  sono só vai acontecer em algumas determinadas ocasiões (3 dias sem dormir, 3 comprimidos de calmante)"). No último meio
  dia, com sono, a mente cai 1 por hora e, perto do fim, a vista pisca. Nos 3 dias, ele **apaga onde estiver** (o som do
  desmaio) e dorme pelo menos 4 h.
- **A cama dorme de verdade** ("[E] Dormir e salvar": continua salvando). O corpo deita, a tela escurece e o controle sai;
  o relógio aparece no meio da tela, e o E levanta.
- **Um só dormindo:** o tempo anda normal e a mente sobe 2,5 por hora (o cansaço cai 16 por hora).
- **Todos dormindo** (cada um onde estiver): o relógio corre 12 vezes mais rápido ("todos dormindo: a noite passa") e a
  mente sobe 5 por hora (o cansaço cai 20). Quem dormiu de noite acorda às 6h30; de dia, quando o cansaço acaba.
- Um golpe acorda. Um amigo também acorda quem dorme, mirando e segurando o E.
- Dormindo, o coração do monitor desce uns 12 batimentos, se nada o acelera: **a febre vence o sono** (com febre, o
  coração fica acelerado mesmo dormindo), e a perda de sangue também.

**O calmante** (o item novo, um frasco com a tampa roxa; incomum na farmácia e no hospital, raro nas casas, no comerciante
e na farmácia do Dia 0, no máximo 3):
- Sem sorte: acalma 8 por hora durante 5 h do jogo (as doses somam até 8 h) e passa a crise de pânico na hora.
- **Seguidos** (a menos de 3 h um do outro): o 2º deixa **sonolento** por 2 h (a vista um pouco embaçada e "SONOLENTO" na
  linha da mente); o 3º faz **apagar**: o baque do desmaio e o sono onde ele estiver, por pelo menos 3 h (o E não levanta;
  um golpe ou um amigo acordam).
- Dá para dar o calmante ao amigo (mirar e segurar E).

**O vício na nicotina:**
- Cada tragada soma na dependência (uns 2,5 maços seguidos enchem), que cai uns 10 por dia sem fumar.
- Com a metade, ele **fica viciado** (o aviso na tela) e só deixa quando ela cai abaixo de um quarto.
- Viciado, 4 h do jogo sem fumar trazem a **abstinência**: a mente cai 2 por hora e a mão treme. Com 10 h, a forte: a mente
  cai 4 por hora e o tremor é maior. Fumar passa na hora.

**A sinuca:** cada tacada acalma 1,2 (no máximo uma a cada 3 s); o fim de uma partida, 6.

## Para quem programa

- **O estado:** `Player/VidaJogador.Mente.cs` (parte do `VidaJogador`). O anfitrião conta a cada 0,5 s. As
  `NetworkVariable`s são a `Sanidade` (0 a 100) e os `MotivosDaMente` (um bit por `MotivoDaMente`: do 0 ao 15 o que
  abala, do 16 em diante o que acalma).
- **Os eventos:**
  - `AbalarAgora` e `AcalmarAgora` (os de uma vez ficam 6 s no motivo);
  - o golpe e a fratura, no `Dano`;
  - o amigo caindo, no `Perder`;
  - matar uma pessoa, no `Sobrevivente.Ferir`;
  - o raio, pelo `Tempo.AoCairUmRaio`;
  - a tragada, pelo `FumoJogador` → `TragouRpc`.
- **Os zumbis:** `Zombies/ZombieManager.Mente.cs` (`PerigoParaAMente`, só no anfitrião, separado da conta da trilha).
- **O HUD:**
  - `Player/MonitorCardiaco.cs`: o ECG por gaussianas no tempo, o QT pela fórmula de Bazett e o `Extra` para a etapa 2;
  - `Player/MenteNoHud.cs`: o cérebro e o porquê;
  - `UI/IconesDaMente.cs`: o cérebro gerado por código (o miolo, as dobras e as rachaduras).
- **O save:** `PersonagemSalvo.abaloDaMente` e `PacoteDoPersonagem.abaloDaMente`. Guarda 100 menos a mente, para o save
  antigo entrar calmo.
- **A versão da rede** subiu para 4.
- **O F1:** a aba Corpo mostra a mente e os motivos, e tem a linha "Mente" (100, 70, 40, 15, 0 e "Susto") e a dos efeitos
  ("Crise", "Vulto", "Falsos", "Amigos", "Fuga", "Desmaio").
- **A etapa 2 (os efeitos, no dono):**
  - `Player/CriseDePanico.cs`: a crise, a palpitação a cada R do monitor, a respiração, a vinheta no ritmo e o
    `TremorAgora`, que o `EfeitosDoCorpo` soma ao tremor do braço. A `Intensidade` é para a trilha.
  - `Player/Alucinacoes.cs`: os vultos, os sons falsos e os eventos do colapso. Os Ocos são os `ZombieManager.modelos` sem
    a IA, a rede e a colisão, com o "Speed" da animação. O tiro avisa pelo `ArmaJogador.AtirouLocal`, e o
    `NomesDosJogadores` pula quem está sendo visto como zumbi.
  - `Player/ColapsoDoJogador.cs`: a fuga (o movimento e a arma desligados, o `CharacterController` andando) e a tela do
    desmaio (o `AudioLowPassFilter` no ouvido).
  - `Player/VidaJogador.Desmaio.cs`: o `Desmaiado` (a `NetworkVariable` do anfitrião), o `DesmaiarRpc` e o `Acordar`. O amigo
    acorda pelo `InventarioJogador.AcordarOAmigo` (o `TratarOAmigo`). O corpo deita como o caído.
  - `Combat/SomDaMente.cs`: a palpitação, o sussurro e o desmaio, gerados por código.
- A versão da rede subiu para 5.
- **A etapa 3:** `Player/VidaJogador.Sono.cs` (o cansaço, o `Dormindo`, o calmante, o vício e a sinuca; as
  `NetworkVariable`s `Cansaco`, `Nicotina` e `EfeitosDaMente`, os bits que o dono mostra). O relógio corre pelo
  `CicloDoDia.Pressa` (vai pela rede, e o `HorasPorSegundo` corre junto), que o anfitrião põe em 12 com todos dormindo
  (`TodosDormindo`). A cama chama o `PedirDormir` (a `ConstrucoesNoMundo` e a `CamaDeDormir`), e a sinuca chama o
  `JogouSinuca` (o `SinucaDoMundo.FimRpc`). A tela de quem dorme é a do `ColapsoDoJogador`. O calmante é o
  `Item.Calmante = 91`, pela boca (`PartesDoCorpo.PelaBoca`), no fim do `_loot`; o modelo é o `_calmante` do
  `Blender/loot.py`. O save guarda o cansaço, a nicotina e se está viciado.
- A versão da rede subiu para 6. O F1: a linha do sono ("Sono 90", "Exausto", "Dormir aqui", "Viciar") e os 3 calmantes nos
  curativos de teste.
- **Falta:** a trilha mais ansiosa na crise lendo a `CriseDePanico.Intensidade`, nos arquivos da sessão da música.
