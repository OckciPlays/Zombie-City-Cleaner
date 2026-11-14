# A fauna: o que os modelos e as animações precisam fazer (13/11/2026)

Para quem vai fazer os modelos e as animações dos bichos (o Gustavo e o Claude dele). A fauna inteira está em
`Assets/_Game/Scripts/Fauna/` e o que ela faz está em `Docs/19-Fauna.md`; aqui fica só o que importa para o modelo e a
animação: o contrato com o código, o que cada bicho faz e as poses que cada um precisa.

## 1. Como está hoje

- **Os modelos:** só os três cervos (o macho, a fêmea e o filhote) vêm do Blender (`Blender/Fauna/familia.py`, `cervo.py`,
  `formas.py`, `malhas.py`; o menu da Unity **Zombie City Cleaner > Fauna > Montar os cervos**, `Editor/MontagemDaFauna.cs`,
  transforma os FBX em prefabs). Todos os outros bichos são **caixas provisórias** montadas no código
  (`ModelosDosBichos.DasCaixas`), a pedido do Pedro: "depois eu vou trocar todos por outros modelos".
- **A animação é procedural** (`Fauna/AnimacaoDoBicho.cs`): não há Animator nem clipes. A cada quadro o código gira os
  ossos (o corpo, o pescoço, a cabeça, as pernas, a cauda, as orelhas, as asas, os gomos) a partir da pose de repouso, pela
  velocidade e pelo estado do bicho. Por isso um modelo novo **com os ossos nos nomes certos** já sai andando, correndo,
  comendo e morrendo sem nenhum clipe.
- **Os dois caminhos** para o trabalho do Gustavo:
  1. **O mais rápido:** o modelo com o esqueleto na convenção abaixo (seção 2). Entra no jogo com a animação procedural que
     já existe, e o código só ganha o nome do prefab e o tamanho dos colisores.
  2. **Com clipes de animação** (Animator): dá mais vida (o uivo, a patada, o pulo do sapo), mas o código precisa passar a
     tocar os clipes pelo estado (seção 5). Pode ser misto: a procedural para andar e correr, e os clipes para os gestos
     especiais (a lista de cada bicho, na seção 4).

## 2. O contrato do modelo (o que o código espera)

- **O prefab:** em `Assets/_Game/Art/Fauna/Resources/Fauna/<Nome>.prefab` (o código carrega com
  `Resources.Load("Fauna/<Nome>")`). O nome de cada espécie e variante sai de `ModelosDosBichos.NomeDoPronto` (hoje só
  `Cervo_Macho`, `Cervo_Femea`, `Cervo_Filhote`); cada bicho novo ganha uma linha ali. Sem o prefab, o bicho sai de caixas.
- **A orientação e a escala:** a frente em **+Z**, o alto em **+Y**, em **metros**, o pivô **no chão, entre as patas**. O
  código não escala o prefab: o adulto vem no tamanho real (a tabela da seção 3), e a fêmea e o filhote são prefabs à
  parte (como os cervos) ou o mesmo modelo menor (a escala de cada variante: `Especies.Escala`, a fêmea de 0,87 a 0,92 e o
  filhote de 0,4 a 0,58 do adulto).
- **Os ossos (a convenção dos cervos):** o código acha os ossos pelo nome (`ModelosDosBichos.DoModeloPronto`):

  | Osso | O que a animação faz com ele |
  |---|---|
  | `Quadril` | o corpo: o sobe e desce do passo, o balanço do galope, a respiração |
  | `Pescoco_1`, `Pescoco_2` | o pescoço: abaixa para comer, levanta no alerta, estica na corrida |
  | `Cabeca` | a cabeça: o olhar (vira para quem ele olha), o mastigar; os olhos que brilham na lanterna e a carga na boca ficam presos nela |
  | `Braco.L`, `Braco.R` + `Antebraco.L`, `Antebraco.R` | as pernas da frente (o ombro e o joelho) |
  | `Coxa.L`, `Coxa.R` + `Perna.L`, `Perna.R` | as pernas de trás (o quadril e o jarrete) |
  | `Cauda_1`, `Cauda_2` | a cauda: abana; levantada no alerta e na fuga (a "bandeira" do veado) |
  | `Orelha.L`, `Orelha.R` | o tremor de vez em quando; para a frente no alerta; para trás no alce bravo |

  As aves, a cobra, o peixe e o sapo ainda não têm modelo pronto: o código das caixas usa **asas** (uma lista de ossos por
  lado) e **gomos** (a cadeia do corpo da cobra e do peixe, do primeiro ao último). Quando vierem do Blender, sugiro
  `Asa.L`, `Asa.R` (e `Asa_2.L/R` para a ponta) e `Gomo_1` … `Gomo_N`; o código ganha o mapeamento junto.
- **A pose de repouso:** a animação gira cada osso **a partir da pose em que o modelo foi exportado**: as pernas retas,
  apontando para o chão; o pescoço e a cabeça na posição neutra (nem comendo, nem em alerta); a cauda relaxada; as asas
  fechadas. Os giros são no espaço do bicho: "levantar" em volta do eixo da direita (+ = a ponta para baixo e para trás),
  "virar" em volta do alto e "rolar" em volta da frente.
- **Os colisores:** o código põe dois (`ParteDoBicho`): a caixa do corpo na raiz e a caixa da cabeça presa no osso
  `Cabeca` (o tiro na cabeça vale mais). Os tamanhos hoje são os do cervo; cada bicho novo precisa dos dele (é só me passar
  o tamanho do corpo e da cabeça, ou o Claude do Gustavo ajusta em `DoModeloPronto`). O código põe todo o modelo na camada
  **14 (Fauna)**.
- **O material:** um só, URP Lit fosco, com uma textura pequena pintada, sem filtro e sem mipmap (cada face pega a sua cor:
  o estilo facetado do veado). Os olhos que brilham são duas esferas que o código acende na frente do osso da cabeça.
- **A carga:** o salmão na boca do urso e o galho na boca do castor ficam presos no osso `Cabeca` (um pouco à frente e
  abaixo dele: a boca); o peixe da águia fica no corpo (nas garras). O urso sentado segura o peixe nas patas da frente.
- **O orçamento** (do plano aprovado): os grandes com ~3.000 triângulos (o Oco tem ~5.000), os médios com ~1.500, os
  pequenos de 300 a 800. Os vaga-lumes são partículas (não precisam de modelo).

## 3. Os bichos: o tamanho, o corpo e as variantes

O tamanho é do adulto (a altura é a do lombo, ou da cabeça nas aves e nos pequenos); a fêmea e o filhote vêm de
`Especies.Escala`. As velocidades (m/s) servem para o ritmo das passadas.

| Espécie | Corpo | Altura × comprimento (m) | Variantes | Andar / trotar / correr |
|---|---|---|---|---|
| Veado (cauda-branca) | quadrúpede | 0,95 × 1,7 | macho (galhada), fêmea, filhote (manchas) | 1,3 / 2,6 / 6 |
| Alce | quadrúpede | 1,8 × 2,8 | macho (galhada larga), fêmea, filhote | 1,4 / 2,8 / 6 |
| Lobo-cinzento | quadrúpede | 0,8 × 1,4 | macho (o alfa), fêmea, adulto, filhote | 1,5 / 3 / 6,8 |
| Coiote | quadrúpede | 0,6 × 1,1 | macho, adulto | 1,4 / 2,8 / 6,4 |
| Urso-negro | quadrúpede | 0,95 × 1,7 | macho; fêmea com 1 a 2 filhotes | 1,2 / 2,6 / 5,8 |
| Urso-pardo | quadrúpede | 1,15 × 2,1 | macho; fêmea com 1 a 2 filhotes | 1,3 / 2,8 / 6 |
| Coelho | quadrúpede | 0,25 × 0,4 | adulto | 0,8 / 2,2 / 5,5 |
| Castor | quadrúpede (anfíbio) | 0,3 × 1,0 | adulto, filhote | 0,8 / 1,5 / 3 |
| Pato | ave (anfíbio) | 0,35 × 0,5 | adulto | 0,6 / 1,2 / voa a 7 |
| Ganso | ave (anfíbio) | 0,6 × 0,8 | macho, fêmea (o ninho) | 0,7 / 1,5 / voa a 7,5 |
| Águia-careca | ave (do ar) | 0,8 × 0,9 | adulto | voa a 9 |
| Urubu | ave | 0,6 × 0,7 | adulto | 0,5 / 1 / voa a 6 |
| Peru-selvagem | ave | 0,8 × 1,0 | adulto | 1 / 2,5 / 5 |
| Sapo | sapo (anfíbio) | 0,06 × 0,12 | adulto | pula: 0,4 / 0,8 / 1,6 |
| Cascavel | cobra | 0,06 × 1,1 | adulto | 0,3 / 0,5 / 0,9 |
| Guaxinim | quadrúpede (trepador) | 0,3 × 0,65 | adulto | 0,9 / 2 / 4,5 |
| Gambá-listrado | quadrúpede | 0,25 × 0,6 | adulto | 0,7 / 1,5 / 3 |
| Esquilo | quadrúpede (trepador) | 0,12 × 0,25 | adulto | 1 / 2,2 / 4,5 |
| Salmão | peixe | 0,15 × 0,7 | adulto | 0,6 / 1,5 / 3 |

## 4. O que cada bicho faz (as poses e os gestos que a animação precisa)

As poses comuns a todos (a seção 5 diz como o código escolhe cada uma): **parado**, **comendo** (a cabeça baixa,
mastigando), **alerta** (a cabeça alta, as orelhas para a frente), **andando / trotando / galopando** (pela velocidade),
**atacando**, **deitado** (descansando, dormindo), **nadando** (só a cabeça e o lombo fora da água, as patas remando),
**morto** (caído de lado) e, de dia ou de noite, os **olhos que brilham** na lanterna (só nos mamíferos e no sapo; as aves,
a cobra e o peixe não).

- **Veado:** pasta de cabeça baixa; no alerta levanta a cabeça e as orelhas e bufa; foge galopando **com a cauda branca
  levantada** (a "bandeira"); o macho alfa com filhotes por perto às vezes **investe e dá a cabeçada** com a galhada (e
  depois foge); deita para descansar; nada; à noite, **fica parado no farol** do carro, olhando a luz, e depois dispara;
  morto, vira a carcaça (o jogador carneia com a faca).
- **Alce:** come no raso **com a cabeça dentro da água** (a taboa); atravessa nadando; bravo, **deita as orelhas para
  trás**; a mãe com filhote **investe e pisoteia** (o golpe com as patas da frente e o empurrão); o mugido.
- **Lobo e coiote (a matilha):** **espreitam** a presa de cabeça baixa, andando devagar e agachados; disparam a galope
  cercando pelos lados; **mordem** (o bote: o salto curto com a boca); **comem na carcaça** em roda, de cabeça baixa,
  puxando; à noite **cercam** o jogador ferido ou sozinho, rodando em volta, e um de cada vez dá o bote e volta para a roda;
  **uivam** (o lobo: a cabeça para o alto; o coiote em coro); descansam deitados (os coiotes em volta da toca, de dia).
- **Ursos:** andam fuçando o chão; galopam no ataque; no aviso **ficam de pé nas patas de trás**, bufando e batendo os
  dentes; às vezes fazem o **falso ataque** (correm e param a 3–5 m); atacam com a **patada** (forte, com o empurrão que
  derruba); **pescam**: de pé no raso, de cabeça baixa olhando a água, e de tempo em tempo **a patada na água** (o
  respingo); pegando o salmão, **levam na boca** até a margem e **comem sentados**, com o peixe nas patas; **os filhotes
  sentam do lado e comem junto**; dormem **deitados na caverna** (dia e noite, com os filhotes); **esperam de pé embaixo**
  de quem subiu num lugar alto (não alcançam) e depois desistem; nadam. O filhote órfão fica perto da mãe morta e chora.
- **Coelho:** pasta; foge em **zigue-zague**; o guincho quando é pego.
- **Castor:** nada (só a cabeça fora) e **mergulha**; assustado, **bate a cauda na água** (o estalo) e some embaixo d'água;
  **rói o tronco** (de pé encostado na árvore, de cabeça no tronco) até ela cair; **leva o galho na boca** até a toca ou a
  represa e remenda a represa de cabeça baixa.
- **Pato e ganso:** **boiam** e **mergulham a cabeça** na água (o rabo para cima); **decolam** batendo as asas, **voam** e
  **pousam** na água; o ganso macho do ninho **avança de asas abertas**, grasnando, aos pulinhos, e **bica**; a gansa
  **choca deitada no ninho**; o bando de gansos passa **em V** alto no céu.
- **Águia-careca:** **plana em círculos** alto; **mergulha** na água e sai **com o peixe nas garras**; o grito.
- **Urubu:** **roda planando** alto (as asas abertas, quase sem bater); **pousa** perto do corpo, **come bicando** no chão
  e **decola** quando alguém chega.
- **Peru:** anda em bando ciscando; o **glu-glu** (o papo e a cabeça balançando); foge correndo e, apertado, num **voo
  curto**; à noite **dorme empoleirado** na árvore (sobe e desce voando).
- **Sapo:** parado; o **pulo**; o **coaxar** (o papo inflando); **a língua** que pega o vaga-lume (hoje uma caixinha fina
  que estica do focinho até o vaga-lume por um instante).
- **Cascavel:** **enrolada**; no aviso **ergue a cabeça em S e vibra o chocalho** (a ponta da cauda); o **bote** (a cabeça
  dispara para a frente); **rasteja** ondulando; **engole** o sapo (a presa some devagar).
- **Guaxinim:** anda; **revira o lixo** (de pé nas patas de trás, mexendo com as mãos na lata, na sacola, na caçamba);
  **sobe na árvore** e fica agarrado no tronco.
- **Gambá:** no aviso **levanta a cauda e bate as patas da frente no chão**; depois **vira de costas e espirra** (o cheiro).
- **Esquilo:** corre e para fuçando; **sobe o tronco** na vertical (de pé, agarrado) e fica lá **chiando**.
- **Salmão:** nada em cardume no raso; de vez em quando **pula fora da água** (um arco) e cai.

## 5. Como o jogo escolhe a pose (e o que vai pela rede)

- **O estado de cada bicho** (`EstadoDoBicho`, em `Bicho.cs`): Pastando, Andando, Alerta, Fugindo, Investindo, Morto,
  Deitado, Sentado. **Vai pela rede em 3 bits** (o anfitrião pensa; os clientes recebem a posição, o giro, o estado, a
  velocidade e os bits de ferido, carneado, alfa e carregando). Os 8 valores já estão todos usados: um estado novo pede
  mudar a mensagem "ZCC.Fauna" (`FaunaDoMundo.Rede.cs`) e a `VersaoDaRede`.
- **A pose sai do estado, da velocidade e da posição** (`Bicho.Animar`), em todas as máquinas:

  | Pose | Quando |
  |---|---|
  | Parado / Andando | sem estado especial: parado abaixo de 0,15 m/s |
  | Comendo | Pastando e quase parado |
  | Alerta | Alerta (o urso e os trepadores ficam de pé; o lobo e o coiote, de cabeça baixa: a espreita) |
  | Correndo | Fugindo, ou Investindo em movimento |
  | Atacando | Investindo parado (a cabeçada, o bote, a patada; a patada na água do urso pescando também) |
  | Deitado / Morto / Sentado | os estados de mesmo nome |
  | Nadando | o quadrúpede onde não dá pé (pela fundura da água na posição) |
  | Voando | a ave acima de 0,7 m do chão ou da água |

- **O passo:** o andar, o trote e o galope se misturam pela velocidade: o trote entre 18% e 32% de uma referência, o galope
  entre 45% e 62% dela; a referência é o maior entre a corrida e 3,2 vezes o trote da espécie (`Bicho.Animar`). Quem fizer
  clipes de locomoção pode usar a mesma conta para misturar (um Blend Tree pela velocidade).
- **Os gestos especiais** que hoje são só a pose genérica (e que ganhariam com clipes): o uivo, o coaxar, o glu-glu, o
  chocalho, o espirro do gambá, o roer do castor, o mergulhar a cabeça do pato, o pulo do sapo e do salmão, o bater de asas
  da decolagem. Eles acontecem junto com o som de cada um (`SomDosBichos.cs`, o enum `SomDoBicho`): um clipe pode ser
  disparado pelo mesmo evento do som (a mensagem "ZCC.FaunaSom" já chega em todas as máquinas).

## 6. Para ligar um modelo novo no jogo

1. No Blender: o modelo no tamanho real, os ossos na convenção da seção 2, a pose de repouso neutra, na mesma orientação
   dos cervos (em `cervo.py` o focinho fica para +X) e com o mesmo preset de exportação (`Blender/Fauna/familia.py`:
   `axis_forward="-Z"`, `axis_up="Y"`, `apply_scale_options="FBX_SCALE_ALL"`, sem leaf bones, `primary_bone_axis="Y"`,
   `secondary_bone_axis="X"`, sem animação assada); o FBX em `Assets/_Game/Art/Fauna/`. Conferir no prefab que a frente do
   bicho ficou em +Z da Unity. Os cervos têm mais ossos (a pelve, a coluna, as canelas): só os da tabela são mexidos pela
   animação, e os outros podem existir à vontade.
2. Na Unity: o prefab em `Art/Fauna/Resources/Fauna/<Nome>.prefab` (o menu de montar os cervos é o modelo a seguir).
3. No código: o nome em `ModelosDosBichos.NomeDoPronto` e os tamanhos dos dois colisores em `DoModeloPronto`.
4. Testar pelo F1 (o menu do desenvolvedor, a aba Fauna): **"Fila de todos"** põe um de cada espécie parado em fila na
   frente do jogador (para ver os modelos lado a lado), e os botões de cada espécie põem um grupo a 18 m (para ver a
   animação: andar, fugir, comer). "Matar o mais perto" mostra a pose de morto e a carcaça.

O Pedro decidiu que os modelos atuais são provisórios e que ele (com o Gustavo) troca depois; as decisões dele sobre a
fauna estão no começo de `Docs/19-Fauna.md`.
