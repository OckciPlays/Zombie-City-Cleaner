# Zombie City Cleaner: o mapa do jogo (o gerador de mapas)

Proposta de 26/10/2026, esperando aprovação. É o passo seguinte à demo: sair da cidade pequena feita à mão e criar a cena do jogo de verdade, com um mapa gerado por semente.

- Os zumbis ficam como estão: o usuário quer pensar mais sobre eles. O mapa só deixa os pontos onde eles vão nascer.
- A `Demo_Cidade` continua no projeto como cena de testes. O jogo novo fica numa cena nova (`Mundo.unity`).
- Assim como na demo, o trabalho vai em partes (M0 a M10, no fim deste arquivo), cada uma aprovada antes da próxima.

## O pedido (como veio)

- Mapas gerados, com 3 tamanhos e semente. Primeiro eram 1, 3 e 5 km; mudou em 26/10/2026 para Pequeno, 3 km; Médio, 5 km; Grande, 8 km (em 1 km não cabe uma cidade grande de verdade).
- O mapa é dividido em blocos (chunks), e o que está longe do jogador não carrega (LOD).
- **Cada mapa tem:**
  - cidades grandes, cidades pequenas e vilas residenciais;
  - bases militares e comunidades aliadas e inimigas;
  - bunkers espalhados pelas cidades e uma instalação científica estilo Umbrella numa das cidades grandes;
  - uma casa no meio de um lago, fazendas, cabanas e postos de gasolina (para o carro);
  - rodovias ligando as cidades e estradas menores ligando as construções.
- **Nas cidades:**
  - prédios de até 15 andares e casas de 1 e 2 andares, algumas com porão;
  - lojas: mercado, loja de armas, hospital, farmácia e 24 horas;
  - as cidades grandes precisam ser grandes de verdade.
- **Relevo:** o terreno não é plano. A base é o sistema do Guardiões da Floresta, com textura de grama e grama 3D (volumétrica).
- **Proporção:** as casas por dentro ficam maiores, porque hoje são apertadas para o jogador. O bunker é a referência de altura boa.
- **Sem móveis por enquanto:** casas, prédios, lojas e instalações ficam vazios. Os móveis vêm depois.
- **Nomes:** as cidades têm nome, e as estradas têm placas dizendo para onde levam.
- **Metrô:** túneis ligando o mapa inteiro, com saídas pela cidade e em outras cidades.
  - Por enquanto o jogador anda a pé neles.
  - Eles só são desenhados quando alguém entra; na superfície, não existem.
- **Portas:** as portas fechadas das casas e dos apartamentos precisam ser arrombadas, com uma ferramenta ou uma habilidade.

## As ideias que eu acrescento

Marcadas com "(sugestão)" no resto do plano. Dá para tirar qualquer uma.

1. **Ver o mapa em 2D antes do 3D.** O gerador primeiro desenha o mapa como imagem (cidades, estradas, lagos, bairros), e a gente olha 10 sementes de cada tamanho. Acertar a distribuição numa imagem leva minutos; no 3D, leva dias.
2. **Construções feitas pelo jogo, a partir de plantas com variações.** Cada tipo de construção tem algumas plantas desenhadas (como o `CASAS` de hoje), que o gerador espelha, estica, troca de cômodo, de cor, de telhado e de varanda.
   - Plantas sorteadas do zero costumam sair estranhas (cômodo sem porta, corredor sem fim). Com as plantas desenhadas, dá centenas de casas diferentes que parecem de verdade.
3. **Marcos que furam a neblina,** para se orientar sem bússola:
   - a caixa d'água com o nome da cidade pintado;
   - a torre de rádio com a luz vermelha piscando à noite;
   - a torre da igreja e o prédio do laboratório.
   - No clima do Silent Hill, você vê a silhueta, mas não o caminho.
4. **A borda do mapa é a quarentena.** Serras nas bordas, com a cerca da quarentena no meio delas, em volta de tudo; onde a rodovia sai do mapa, a cerca vira uma barreira militar (contêineres, barreiras de concreto, blindados parados). Isso explica o fim do mundo e liga com as bases militares.
5. **Placas que contam a história:**
   - "Bem-vindo a Santa Rita do Vale, população 4.312", com a população riscada à mão;
   - as placas de direção nos cruzamentos ("Pedra Branca 2,4 km") e os nomes dos bairros.
6. **Mapas de papel:** achados nos postos e nas lojas 24 horas, eles revelam as estradas da região no mapa (M). Sem eles, o mapa só mostra onde o grupo já passou.
7. **Gasolina que acaba:** cada bomba de posto tem uma quantidade (salva no mundo). O carro já tem o ponteiro, só falta gastar.
8. **Lugares que fazem sentido com o que já existe:**
   - a garagem com carro (fonte de veículo e peças, que o capô já usa) e a oficina mecânica;
   - a loja de ferragens (onde se acha o pé de cabra e a marreta), a delegacia (armas) e o corpo de bombeiros (machado);
   - o motel de beira de estrada.
9. **A cabana de preparador,** com um bunker escondido embaixo: saque bom, longe de tudo.
10. **Uma estação secreta do metrô dentro do laboratório.**
11. **Dentro de um prédio alto, só os andares perto do jogador carregam** (2 para cima e 2 para baixo, mais a escada).
12. **Chave embaixo do tapete ou do vaso,** em algumas casas: entrar sem barulho e sem ferramenta.
13. **F1 para o mundo grande:** ir para qualquer lugar da lista, voar e mostrar a semente na tela. Sem isso, testar um mapa de 8 km leva horas.
14. **Para depois:** um trole (carrinho de trilho) no metrô.

- **Tirada em 26/10/2026:** o bairro como unidade da "limpeza" (o mapa mostrando quanto de cada bairro foi limpo). O jogo mudou, e a limpeza talvez não faça mais sentido.

## Como o gerador funciona

- **A semente decide tudo.** O anfitrião escolhe o tamanho e a semente (ou uma aleatória) e manda só isso, mais a versão do gerador. Cada máquina gera o mesmo mundo sozinha.
  - No fim da geração, um resumo dos números (checksum) é comparado com o do anfitrião. Se não bater, o jogo avisa.
  - É como no Guardiões: um sorteio próprio por etapa, com a semente e o nome da etapa, e nunca o `UnityEngine.Random`.
- **O mundo inteiro existe em dados; só o que está perto vira malha.**
  - O carregamento gera tudo em dados: as alturas, as estradas, os lotes e a lista das construções. O Guardiões gera 5 km em ~1,7 s.
  - As malhas (terreno, fachadas, interiores, colisão) são montadas em outras threads, só nos blocos perto dos jogadores, e soltas quando eles se afastam.
- **Cada construção tem a própria semente,** tirada do número dela. A mesma casa sai sempre igual, na ordem em que for carregada.
- **O salvar guarda pouco:** a semente, o tamanho, a versão e o que mudou (portas arrombadas, saque, carros, gasolina dos postos).
  - Se a versão do gerador mudar, o mundo salvo deixa de abrir (como no Guardiões). Durante o desenvolvimento, isso é aceitável.
- **Um material só nas construções.** As fachadas de um bloco viram uma malha só (a paleta da Parte 8 permite isso). Assim, um bloco inteiro de cidade custa poucas chamadas de desenho.

### O que vem do Guardiões da Floresta

O código fica em `C:\Users\gusta\Projeto Guardões da Floresta\Assets\_Project\Scripts\WorldGen` e `Gameplay\World`. É copiado e adaptado, não ligado: os nomes e as pastas são outros, e o URP lá é o 17.7 (aqui, 17.6).

| Do Guardiões | O que faz lá | Aqui |
|---|---|---|
| `WorldRandom`, `Noise2D` | Sorteio por semente e por etapa, ruído | Igual |
| `WorldBuilder`, `WorldData` | Grade de alturas a cada 2 m, pesos do chão e checksum, em várias threads | Igual, com cidades, estradas e lotes no lugar dos rios e das aldeias |
| `WorldStreamer`, `ChunkMeshBuilder` | Blocos de 64 m em 3 níveis de detalhe, com saias entre os blocos. A montagem roda em outra thread, com limite de tempo por quadro, e a colisão fica só perto dos jogadores | A base dos blocos. Entram os buracos (porões, metrô, bunkers) e as construções de cada bloco |
| `TerrainBlend` (shader) | Grama, terra, areia e folhas pela cor do vértice; rocha pela inclinação | Grama seca, terra, cascalho, folhas mortas e rocha, nos tons do clima daqui |
| `GrassField` | Grama 3D por GPU instancing, em blocos de 8 m, até 42 m, com vento e dobrando quando alguém passa | Igual, com a cor daqui |
| `VegetationPlanner`, `DistanceCuller` | Árvores e pedras instanciadas, cortadas pela neblina | Pinheiros, árvores secas e arbustos |
| `EnemyTrails` (A*) | Trilhas entre as bases | Rodovias e estradas pelo relevo |
| `WorldMapPainter`, `MapHud` | O mapa pintado na hora, com descoberta compartilhada | O mapa (M) |
| NavMesh em blocos | Só no anfitrião, em volta dos jogadores | Igual, com os andares dos prédios |
| Tela de carregamento, "Prévia com semente X" | Etapas na tela; gerar no Editor sem dar Play | Igual |

## Os tamanhos e o que cada mapa tem

O tamanho é o lado do quadrado (decidido em 26/10/2026): Pequeno, 3 × 3 km (9 km²); Médio, 5 × 5 km (25 km², o mesmo do Guardiões); Grande, 8 × 8 km (64 km²). Os números abaixo são ponto de partida, para acertar olhando as imagens da Parte M3.

| | Pequeno (3 km) | Médio (5 km) | Grande (8 km) |
|---|---|---|---|
| Cidade grande | 1 (~900 m) | 2 (uma de ~1,5 km e uma de ~1 km) | 3 (uma de ~2 km e duas de 1 a 1,5 km) |
| Cidade pequena | 2 | 3 a 4 | 6 a 8 |
| Vila residencial | 2 a 3 | 4 a 6 | 8 a 12 |
| Fazendas | 5 a 8 | 10 a 15 | 20 a 30 |
| Base militar | 1 | 2 | 3 |
| Comunidades (aliada + inimiga) | 2 + 2 | 3 + 3 | 5 + 5 |
| Laboratório | Na cidade grande | Numa das cidades grandes | Numa das cidades grandes |
| O lago com a casa no meio | 1 | 1 | 1 |
| Cabanas | 8 a 12 | 15 a 25 | 30 a 50 |
| Postos de gasolina | 4 a 6 | 8 a 12 | 15 a 20 |
| Bunkers | 5 a 8 | 10 a 15 | 20 a 30 |
| Metrô | 1 a 2 linhas | Uma rede ligando todas as cidades | Uma rede ligando todas as cidades |

- **A maior cidade do mapa Grande** (~2 × 2 km) tem uns 380 quarteirões e por volta de 4 mil construções:
  - o centro (um quarto) com prédios de 6 a 15 andares e lojas no térreo;
  - o anel comercial;
  - os bairros de casas.
  - Atravessar correndo (4,2 m/s) leva uns 8 minutos, e o mapa de ponta a ponta, uns 32.
- **8 km cabem sem mudar a origem do mundo:** a 4 km do meio, a precisão dos números da Unity ainda é de meio milímetro.

## O relevo, o chão e a água (sugestão nos detalhes)

- **O terreno:** colinas suaves e vales no meio, e serras nas bordas, pelo método do Guardiões (ruído em camadas mais os lugares escolhidos pelo plano).
- **As cidades num chão quase plano:**
  - o lugar de cada cidade é escolhido onde o terreno é mais suave e depois amaciado (sobra só a ondulação grande, até uns 5% de inclinação);
  - cada lote é nivelado, e a construção ganha a fundação: numa rua inclinada, a casa tem degraus até a porta e o porão aparecendo nos fundos;
  - lembra as cidades de morro do Silent Hill.
- **Buracos no chão:** o terreno pula as células de 2 m marcadas como buraco (debaixo das casas com porão, nas bocas do metrô e nas escadas dos bunkers). A fundação da construção cobre a borda.
  - Sem isso, a superfície do terreno passaria pelo meio do porão.
- **As texturas do chão:** grama seca, terra, cascalho, folhas mortas e rocha nas encostas, nos tons dessaturados do clima.
  - **Muda a direção de arte:** até aqui o cenário não tinha textura nenhuma. A textura fica só no chão; construções e objetos continuam nas cores da paleta.
- **A grama 3D:** a do Guardiões (touceiras por instancing, até ~40 m, com vento e dobrando com quem passa), mais seca e mais baixa. Ela não nasce nas estradas, nas calçadas nem nos lotes.
- **A água:** sem rio, só o lago com a casa no meio (decidido em 26/10/2026), com a água escura e parada (o shader do Guardiões, mais turvo).

## As estradas e as placas

- **Os tipos:**
  - rodovia: 2 pistas largas com acostamento, ligando as cidades;
  - estrada: 2 pistas, ligando as vilas, as fazendas, os postos e as bases;
  - rua da cidade: com calçada e meio-fio;
  - estrada de terra: para as cabanas, as fazendas e a casa do lago.
- **Como nascem:**
  - as cidades são ligadas pela árvore de caminhos mais curta, mais 1 ou 2 ligações a mais (para sempre haver dois caminhos);
  - cada trecho é traçado pelo relevo (A*, como as trilhas do Guardiões), evitando subida forte (rodovia até ~8%, estrada ~12%, terra ~15%) e a água;
  - o terreno é acomodado embaixo da estrada (corte e aterro).
- **A malha:** a estrada é uma malha própria por cima do terreno (o asfalto, a faixa, o acostamento e o guard-rail nas curvas e nos aterros), com os cruzamentos no mesmo nível.
- **As placas de direção:** em cada cruzamento, cada saída ganha uma placa com os próximos lugares com nome por aquele caminho e a distância ("Pedra Branca 2,4 km"), tiradas do próprio desenho das estradas.
  - Em português, verdes com letra branca, como as rodovias brasileiras.
  - O texto vai em TextMeshPro 3D, com uma fonte aberta parecida com a das placas de estrada (escolhida na Parte M4).
  - Também: a placa de entrada e a de saída da cidade, os nomes das ruas nas esquinas e os marcos de quilômetro (sugestão).
- **A ambientação nas estradas** (sugestão): carros abandonados e batidos, as blitz militares nas entradas das cidades (como a da demo) e os postos de gasolina com a loja 24 horas.

## As cidades

- **Os nomes:** um gerador de nomes por semente (sem repetir no mesmo mapa), para as cidades, os bairros e as ruas.
  - Brasileiros e em português, por enquanto (decidido em 26/10/2026): cidades como Santa Rita do Vale, Pedra Branca e São Jerônimo da Serra, bairros como Jardim das Flores e Vila Operária, ruas como Rua Sete de Setembro.
  - O gerador junta pedaços (Santa, São, Vila, Nova, Porto, Campo, Monte; Rita, Luzia, Jerônimo, Cruz; do Vale, da Serra, das Pedras, Velha) e confere que o nome não ficou estranho.
- **Cidade grande:**
  - a grade de ruas (girada e um pouco torta, para não parecer papel quadriculado; o centro antigo com ruas mais irregulares);
  - os quarteirões divididos em lotes;
  - as zonas pela distância do centro mais um ruído:
    - **centro:** prédios de 6 a 15 andares e lojas no térreo;
    - **comércio:** lojas de 1 a 3 andares, o mercado e o posto;
    - **bairros:** casas de 1 e 2 andares, algumas com porão e garagem;
    - **praças e parques:** o coreto da demo serve;
  - os lugares obrigatórios: hospital, delegacia, bombeiros, escola, igreja, loja de armas, farmácias, mercados, lojas 24 horas, a caixa d'água com o nome e a estação de metrô.
- **Cidade pequena:** 400 a 600 m, uns 300 lotes, o centrinho de 2 a 4 andares, o posto, o mercado, a farmácia e a delegacia.
- **Vila residencial:** 1 a 3 ruas, de 15 a 40 casas e às vezes uma loja 24 horas.
- **O que cada cômodo já sai sabendo**, mesmo sem móvel:
  - o tipo de piso (para os passos: taco, carpete, piso frio, concreto);
  - os pontos onde os móveis e o saque vão entrar depois;
  - a área interna (a neblina de dentro) e o eco.

### As construções (sem móveis)

- **Casas:**
  - 1 andar, 2 andares, com porão (escada na cozinha, no corredor ou a porta de porão de fora, à americana);
  - **o lugar do carro** (pedido em 26/10/2026), onde o carro nasce: toda casa tem um dos dois:
    - a garagem anexa ao lado, com o portão aberto ou fechado e, às vezes, uma porta para dentro da casa;
    - ou a vaga de concreto ao lado da casa, descoberta;
    - e a entrada de carro até a rua.
  - sala, cozinha, 2 ou 3 quartos, banheiro e corredor;
  - por fora, o acabamento das casas da demo (lambri, telhado de telha, varanda, chaminé, ar-condicionado, janelas abertas, com cacos ou com tábuas).
- **Prédios** (4 a 15 andares):
  - o núcleo com a escada e o poço do elevador (parado; sugestão: dá para cair nele);
  - o corredor e os apartamentos de cada andar;
  - o térreo com a portaria ou as lojas, e a cobertura com a caixa d'água;
  - a escada de incêndio por fora nos baixos.
- **Lojas:**
  - mercado (salão alto, depósito, doca de carga);
  - loja de armas (balcão e o depósito dos fundos com porta de aço: o melhor saque atrás da porta mais difícil);
  - farmácia, loja 24 horas e posto (a cobertura das bombas e a loja);
  - hospital (vários andares, alas, corredores, pronto-socorro).
- **As especiais:**
  - **Fazenda:** a casa de 2 andares, o celeiro, o silo, as cercas, o campo (as plantações de verdade vêm com a sobrevivência que se renova) e a estrada de terra.
  - **Cabana:** na mata, na beira do lago ou no morro; 1 andar com mezanino; algumas são de preparador, com o bunker escondido (sugestão).
  - **Casa no lago:** a ilha com a casa, sem ponte (decidido em 26/10/2026): chega-se nadando, quando o nado existir. Até lá, só pelo F1.
  - **Base militar:**
    - a cerca com arame e as torres, o portão com a guarita;
    - os alojamentos, o paiol (porta de aço com cartão), as garagens com os blindados parados, o heliponto, o comando e as barracas.
  - **Comunidade:**
    - uma vila ou fazenda cercada de barricadas (carros, contêineres, madeira), com portões e torres;
    - a aliada e a inimiga se diferem pela decoração por enquanto. As pessoas e as missões vêm depois.
  - **Laboratório:**
    - o terreno murado na cidade grande;
    - o prédio de escritórios por cima (a fachada de empresa) e os níveis fundos por baixo (o subsolo, Parte M8), com a estação secreta do metrô (sugestão).

## A escala nova (a proporção por dentro)

**Decidido na Parte M0 (26/10/2026): as casas de hoje × 1,15** (pé-direito 3,22 m, porta 1,27 × 2,42 m). A tabela abaixo foi a proposta antes da sala de medidas; as medidas que valem estão na Parte M0.

Os jogos em primeira pessoa costumam fazer os espaços uns 15% a 30% maiores que os de verdade, porque a câmera não tem visão de lado e o corpo não aparece. As medidas abaixo são para testar na Parte M0, numa sala de medidas, com o jogador andando pelas 3 versões.

| Medida | Hoje | Proposta |
|---|---|---|
| Jogador | 1,80 m, olho a 1,62 m, raio 0,3 m | Igual |
| Pé-direito das casas | 2,8 m | 3,2 m |
| Pé-direito dos prédios | Andares de 3 m | Andares de 3,6 m (3,2 m livres) |
| Pé-direito das lojas | — | 4,5 m (mercado e hospital no térreo: 5 a 6 m) |
| Porta de dentro | 1,1 × 2,1 m | 1,3 × 2,4 m |
| Porta da frente | 1,1 × 2,1 m | 1,4 × 2,5 m; as lojas com porta dupla de 2,4 m |
| Corredor | Prédio 3,2 m | Casa ao menos 1,8 m; prédio 3,2 m |
| Escada | Prédio em U, degrau de 0,167 m | Casa 1,3 m de largura, prédio 1,8 m, degrau 0,18 × 0,30 m |
| Quarto | ~5 × 4 m | Ao menos 4,5 × 4,5 m |
| Sala | ~10 × 4 m | Ao menos 6 × 5 m |
| Casa de 1 andar | 9 a 12 × 8 a 10 m, 3 cômodos | 14 a 18 × 11 a 14 m, 5 a 7 cômodos |
| Bunker (referência) | Quartos com 3 m, salão com 5,3 m | Igual |

- **A sala de medidas:** a mesma casa em 3 escalas lado a lado (a de hoje, +15% e +30%), com um zumbi parado no corredor, para sentir o espaço de luta. A escolhida vira a regra de todas as construções.

## As portas e o arrombamento

- **Os estados:** aberta, fechada, trancada e arrombada (não tranca mais; depois, com a base, dá para pregar tábuas).
- **Quantas trancadas** (para acertar jogando):
  - casas: a da frente em 60%, a dos fundos em 25%;
  - apartamentos: 50%;
  - lojas: a da frente trancada e a porta de enrolar de aço;
  - base militar e laboratório: portas de aço com cartão.
- **Os jeitos de abrir uma trancada:**

  | Jeito | Precisa de | Tempo | Barulho | Serve em |
  |---|---|---|---|---|
  | Chave | Achar a chave (embaixo do tapete ou do vaso, em ~10% das casas) | Na hora | Nenhum | A porta daquela casa |
  | Chute | Nada | 3 a 6 chutes; pode falhar e cansa | Alto, a cada chute | Madeira |
  | Pé de cabra | O item | ~3 s | Médio | Madeira, metal leve e a porta de enrolar |
  | Marreta ou machado | O item | ~1,5 s | Muito alto | Madeira |
  | Gazua | O item e a habilidade | 6 a 10 s | Nenhum | Fechaduras comuns |
  | Tiro na fechadura | A espingarda | Na hora | O do tiro | Madeira |
  | Cartão | Achar o cartão (base, laboratório) | Na hora | Nenhum | As portas de aço |

- **O barulho chama os zumbis,** com o mesmo sistema que já faz eles ouvirem o tiro. Abrir em silêncio custa tempo ou ferramenta; abrir rápido custa barulho.
- **A habilidade:** enquanto não existir o sistema de habilidades, a gazua funciona para todos, só mais devagar (sugestão).
- **No jogo:**
  - segurar E com uma barra de progresso;
  - a dica mostra o que dá para usar ("Trancada — [E] chutar · pé de cabra").
  - O anfitrião decide, o estado vai para todos e é salvo.
- **Os zumbis:** porta fechada bloqueia o caminho deles no NavMesh; aberta ou arrombada, não. Zumbi forçando porta fica para depois.
- **Os itens novos:** pé de cabra, marreta, gazua e cartão, no estilo quadradão, com ícone e foto (como a lata e a garrafa).
  - Sugestão: o pé de cabra e a marreta servirem também de arma branca.
- **Para depois:** entrar pela janela (quebrar o resto do vidro faz barulho).

## O metrô e o subsolo

- **A rede:**
  - as estações nas cidades: 4 a 8 numa cidade grande, 1 ou 2 numa pequena;
  - as linhas ligando as cidades (a árvore mais curta, mais uma volta para haver caminho alternativo);
  - os túneis a uns 12 a 15 m abaixo da rua, em retas e curvas largas, com subida suave quando o relevo muda.
- **As peças:**
  - o túnel de 2 trilhos com a passarela do lado;
  - a estação: plataformas, mezanino, catracas e a escada até a boca na calçada;
  - as saídas de emergência: escada de serviço até um alçapão, em lugares da cidade e de outras cidades;
  - trens parados ou descarrilados (para se esconder e saquear) e as salas técnicas.
- **Só existe quando alguém desce:**
  - o subsolo é uma camada de blocos à parte, que carrega quando um jogador chega a ~25 m de uma entrada (para a escada já estar lá) e é solta quando todos saem;
  - na superfície, do metrô só existe a boca;
  - no anfitrião, a colisão e o NavMesh de baixo ficam só em volta de quem está embaixo.
- **O clima de baixo:** a regra do subsolo que já existe (sem neblina, escuro, com eco), com as luzes de emergência, algumas piscando.
- **Os bunkers espalhados:**
  - debaixo de algumas casas, nos parques (abrigo público), na base militar e nas cabanas de preparador;
  - a mesma camada do metrô, com a porta blindada;
  - o da família continua com a planta de hoje (`PlantaDoBunker`); os outros são variações menores.
- **Os porões** não são do subsolo: fazem parte do interior da casa (carregam com ela).
- **A distância:** entre duas cidades a 3 km, uns 12 minutos correndo. Depois dá para entrar o trole.

## O que carrega e até onde (os blocos e o LOD)

A ideia geral: o raio de desenho segue a neblina (hoje a câmera corta a 150 m), a colisão segue os jogadores, e o que está dentro (interiores, subsolo) só existe com alguém perto.

| O quê | Até onde | Como |
|---|---|---|
| Terreno | Um pouco além da neblina (~200 m) | Blocos de 64 m em 3 níveis de detalhe (pontos a cada 2, 4 e 8 m) |
| Fachadas | ~200 m | Perto, completas; longe (mais de ~80 m), a caixa com o telhado e as janelas escuras |
| Interiores | ~40 m, ou com alguém dentro | Sem interior, a janela mostra o escuro |
| Prédio alto, por dentro | 2 andares para cima e 2 para baixo, mais a escada | Os outros andares não carregam |
| Objetos de rua | ~120 m | Instanciados; um material só |
| Árvores | Até a neblina | Instanciadas (como no Guardiões) |
| Grama 3D | ~40 m | A quantidade cai com a distância |
| Postes | Apagados (sem energia, desde 27/10/2026) | Só o modelo; de noite, a luz é a da lua, a da lanterna e a do carro |
| Colisão | 100 m de cada jogador | No anfitrião, de todos os jogadores (os zumbis rodam lá) |
| NavMesh | Blocos em volta de cada jogador | Só no anfitrião, montado em segundo plano |
| Metrô e bunkers | Só com alguém embaixo, ou a ~25 m de uma entrada | Camada à parte |
| Zumbis | Como hoje | O LOD de IA da Parte 5, sem mudar |

- **A oclusão:** a de hoje é assada no Editor, e um mundo gerado na hora não tem como assar.
  - O GPU Resident Drawer já está ligado no `PC_RPAsset`; falta testar a oclusão pela GPU dele (`m_GPUResidentDrawerEnableOcclusionCullingInCameras`, hoje desligada), que não precisa assar nada.
  - Junto com a regra dos interiores, deve bastar. Vai ser medido na Parte M5.

## O que já existe e precisa vir junto

O mundo gerado tem de entregar o que a `Demo_Cidade` montava à mão:

- a casa da família com o bunker de hoje, onde a partida começa (o painel de armas, a porta, as luzes e os interruptores, a parede dos fundadores). Ela fica numa zona residencial sorteada pelo gerador (decidido em 26/10/2026);
- os pisos de cada cômodo (os passos) e as áreas internas e o eco de cada construção;
- os postes (`PosteDeLuz`), com o limite de luzes de verdade;
- os carros da cidade (fonte de peças, com o capô) nas ruas, nos estacionamentos, nas garagens e nas vagas das casas (cada casa já sai com o ponto do carro);
- os zumbis (decidido em 26/10/2026): os de hoje, com o mesmo comportamento, mas nascendo espalhados, e sempre uns 250 num raio em volta do jogador (os de longe somem, e outros nascem perto);
- os lugares da comida, da água e da munição (no chão por enquanto, nos pontos onde os móveis vão entrar);
- o NavMesh (agora em blocos), a cena nova, o menu (Novo mundo e Continuar) e o mapa (M).

A Parte 7 da demo (o saque nos móveis, a despensa e dormir) fica para depois do mapa, já no mundo novo. O salvar do mundo começa aqui (Partes M1 e M10).

## Partes, em ordem

### Parte M0: a escala e as decisões

- A sala de medidas: a mesma casa em 3 escalas, com o jogador e um zumbi, para escolher a proporção.
- As respostas às decisões do fim deste arquivo.
- **Pronto quando:** as medidas novas estão aprovadas e escritas aqui.
- **Escolhida (26/10/2026): ×1,15.** As construções do jogo usam as casas de hoje esticadas por 1,15 (`Medidas.DoJogo` no projeto Unity): pé-direito 3,22 m, porta 1,27 × 2,42 m, corredor da térrea 1,80 m, escada 1,27 m e porão 2,76 m. A tabela abaixo fica como registro. A borda do mapa e os zumbis no mundo novo foram decididos depois (veja "Decisões", 9 e 10).
- **Estado (26/10/2026):** a sala de medidas foi feita, e a escala foi escolhida nela.
  - **A cena:** `Scenes/Exemplos/Sala_de_Medidas.unity` no projeto Unity (menu Zombie City Cleaner > Exemplos > Sala de medidas).
    - Do lado norte, a mesma térrea com porão; do lado sul, o mesmo sobrado com porão. Cada escala numa coluna, com a placa do número perto da calçada.
    - Em cada casa, um zumbi parado no corredor (ou no hall) e o boneco do jogador (1,80 m) na sala.
    - Com Play, a tela mostra as medidas da casa em que o jogador está; R troca a planta (a mesma nas 3 escalas).
  - **A escala** estica as casas de hoje: o pé-direito, as portas, as janelas, a escada e os cômodos. O que é do tamanho do corpo fica igual (o degrau, o guarda-corpo, o peitoril).

    | | ×1,00 (hoje) | ×1,15 (a proposta) | ×1,30 |
    |---|---|---|---|
    | Pé-direito | 2,80 m | 3,22 m | 3,64 m |
    | Porta | 1,10 × 2,10 m | 1,27 × 2,42 m | 1,43 × 2,73 m |
    | Passagem sem porta | 1,80 m | 2,07 m | 2,34 m |
    | Janela | 1,20 × 1,20 m | 1,38 × 1,52 m | 1,56 × 1,83 m |
    | Escada (largura) | 1,10 m | 1,27 m | 1,43 m |
    | Corredor da térrea | 1,60 m | 1,80 m | 2,03 m |
    | Porão (pé-direito) | 2,40 m | 2,76 m | 3,12 m |
    | Térrea (semente 7) | 13,2 × 11,1 m | 15,1 × 12,8 m | 17,1 × 14,5 m |
    | Sobrado (semente 7) | 10,0 × 9,9 m | 11,5 × 11,3 m | 13,0 × 12,8 m |
    | Triângulos (térrea / sobrado) | 8,2 mil / 9,4 mil | 8,9 mil / 10,2 mil | 9,2 mil / 11,2 mil |

  - **Conferido nas 3 escalas:** as 42 portas e passagens passam nos dois sentidos, os sobrados sobem da rua até o andar de cima e descem ao porão, e o fosso da térrea leva ao porão.
  - **As fotos:** `Imgs/Renders/Medidas_*` (a mesma vista nas 3 escalas, lado a lado: a sala com o boneco e o zumbi na porta, o corredor, o quarto, a fachada, o hall do sobrado e o corredor de cima; e a rua vista de cima).

### Parte M1: a fundação do gerador e a cena nova

- Uma pasta e um assembly de jogo para o gerador (`Scripts/Mundo`), sem nada de cena, testável no Editor.
- **O sorteio:** a semente, o tamanho, a versão e o sorteio por etapa.
- **A cena e o fluxo:**
  - a cena `Mundo.unity`;
  - o menu: Novo mundo (o tamanho, a semente e o nome) e entrar com código;
  - o anfitrião manda a semente, e cada um gera o mundo e confere o checksum;
  - a tela de carregamento com as etapas.
- **Para testar:** o F1 do mundo grande (ir para, voar, a semente na tela) e a "Prévia com semente X" no Editor.
- O terreno ainda plano.
- Testes no Editor: a mesma semente gera os mesmos números; sementes diferentes, mundos diferentes.
- **Pronto quando:** o anfitrião e um cliente geram o mesmo mundo plano de 8 km e andam juntos nele.
- **Estado (26/10/2026): feita e aprovada** ("Testei e está ok"). Os detalhes estão no `CLAUDE.md` do projeto Unity ("O mundo gerado").
  - **O gerador** (`Scripts/Mundo/Gerador`, no assembly `ZombieCityCleaner.Mundo`): o sorteio por etapa e o ruído do Guardiões (`Sorteio` e `Ruido`), os 3 tamanhos, a versão do gerador (hoje 1) e o `DadosDoMundo`: os blocos de 64 m (125 × 125 no mapa Grande), a altura (plana), as manchas do chão (grama, grama seca e terra), a partida (um ponto sorteado no meio do mapa, com um terreno de concreto de 14 m e os 4 lugares de nascimento, até a casa da família entrar, na M5) e o resumo (checksum). O mapa Grande é gerado em uns 40 ms.
  - **A cena `Scenes/Mundo.unity`** (fora das Build Settings por enquanto; a demo continua sendo a Demo_Cidade): o clima, a rede e o menu da demo, mais o "Mundo".
    - O menu começa no "Novo mundo": o nome, o tamanho (3, 5 ou 8 km) e a semente (um número ou uma palavra; vazia, sorteada), e "Criar o mundo" (com o código para os amigos) ou "Criar local". O entrar com código continua igual.
    - O anfitrião manda só a semente, o tamanho, a versão e o nome. Cada máquina gera o mundo e o chão em volta da partida, e **o personagem só nasce quando o mundo está pronto na máquina de cada um**. O anfitrião compara o resumo de quem entra e avisa se não bater; com a versão do gerador diferente, quem entra sai com a mensagem.
    - A tela de carregamento mostra o nome, o tamanho, a semente, a etapa e a barra (no mundo plano ela pisca: tudo leva menos de 0,1 s).
    - O chão provisório: os blocos até 220 m do jogador viram malha, e a colisão é uma caixa do tamanho do mapa, com paredes invisíveis na borda (as serras e a barreira da quarentena entram na M2).
  - **Para testar:** o F1 ganhou o painel do mundo (o nome, a semente, o resumo, onde você está, voar com F2, ir para x e z, a partida, o meio e as bordas) e o canto da tela mostra a semente. No Editor, Zombie City Cleaner > Mundo > Prévia com semente mostra o mapa inteiro sem dar Play.
  - **Conferido:**
    - os 10 testes do gerador passam (a mesma semente dá o mesmo mundo, também em outras threads; 40 sementes dão 40 mundos; os tamanhos; a partida no meio do mapa);
    - com dois jogadores (o Editor hospedando e uma build como cliente robô, semente 4242, 8 km): os dois geraram o mesmo resumo (`48ACCC1BD55B78DF`, o mesmo da prévia do Editor), o cliente nasceu no lugar dele na partida e andou pelo chão, e cada um viu o outro; o anfitrião foi até a borda norte (a 4 km do meio), o cliente o viu lá, o chão foi junto e a parede da borda segurou;
    - sair da partida apaga o mundo, e criar outro em seguida funciona.
  - **As fotos:** `Imgs/Renders/Mundo_M1_*` (o menu, o F1, a partida com o marco da prévia, de cima e o mapa inteiro; as de cima sem a neblina, que no jogo deixa o chão plano quase todo cinza).

### Parte M2: o relevo, o chão e a água

- O relevo, os blocos, os níveis de detalhe e a colisão, trazidos do Guardiões.
- As texturas do chão, o lago e as serras das bordas.
- Os buracos no terreno, já preparados.
- **Pronto quando:** dá para andar e dirigir o carro pelos 8 km sem travadas, com ≥ 60 FPS, e o cliente vê o mesmo chão.
- **Estado (26/10/2026): feita e aprovada** ("Testado e ok", com o pedido do "Sempre de dia" no F1, que entrou junto com a M3). Os detalhes estão no `CLAUDE.md` do projeto Unity ("O mundo gerado").
  - **O relevo:** a grade de alturas a cada 2 m, passando 192 m da borda (as serras continuam fora da parede), gerada em várias threads (~1,4 s no mapa Grande). Colinas suaves de ~700 m e até ~30 m, vales largos, o detalhe e as serras das bordas (de 60 a 136 m de altura, numa faixa de 260 a 620 m, pelo tamanho do mapa), com rocha nas encostas.
  - **O lago:** um por mapa, no ponto mais baixo de 12 sorteados, com a margem irregular, a água abaixo de toda a margem, uma rampa suave em volta (sem ela, ficava numa cratera) e a ilha no meio para a casa do lago. A água é escura, parada e opaca (assim a neblina passa por cima dela também). Na água, por enquanto, o jogador anda no fundo.
  - **A partida:** agora num platô nivelado (17 m em volta da laje), longe do lago e no lugar menos inclinado de 8 sorteados.
  - **O chão com textura** (a primeira do cenário; as construções continuam na paleta): grama seca, terra, cascalho e folhas mortas, misturados pela cor do vértice, e a rocha nas encostas, nos tons das cores do chão da paleta. As texturas são feitas por código.
  - **Os blocos:** 64 m em 3 níveis de detalhe (2, 4 e 8 m), montados em outra thread até 180 m do jogador, com saias entre os níveis; a colisão até 90 m de cada jogador (e em volta dos carros, no anfitrião, olhando à frente deles).
  - **Os buracos** (para os porões, o metrô e os bunkers): o terreno já sabe pular células marcadas; ninguém marca ainda.
  - **O carro:** o anfitrião põe o carro que se dirige ao lado da laje; no F1, "Trazer o carro" chama o livre mais perto.
  - **O F1** ganhou a altura do chão, os blocos montados, a margem do lago e o teste de travessia (voar baixo a 160 km/h por 60 s, medindo os quadros).
  - **Conferido no Editor** (semente 4242, 8 km): os 15 testes do gerador passam; o jogador nasce na laje e o carro fica no chão; a travessia (2,7 km em 60 s a 160 km/h) deu média de 3,1 ms por quadro (~320 FPS no Editor), o 1% pior 4,7 ms, nenhum quadro acima de 33 ms e nenhum quadro sem a colisão do chão embaixo.
  - **As fotos:** `Imgs/Renders/Mundo_M2_*` (no jogo, de dia; e as da prévia, sem a neblina: as colinas e o lago, a margem, a serra e a partida).

### Parte M3: o desenho do mapa (em 2D)

- Os lugares de cada coisa pela tabela dos tamanhos, os nomes, as estradas e os bairros.
- O mapa pintado como imagem (no Editor e no M do jogo).
- **Pronto quando:** 10 sementes de cada tamanho viram imagens, e a distribuição está aprovada.
- **Estado (26/10/2026): feita e aprovada** ("está ok, pode seguir"; as imagens em `Imgs/Mapas/`). Os detalhes estão no `CLAUDE.md` do projeto Unity ("O mundo gerado").
  - **O desenho** (`PlanoDoMapa`, feito no começo da geração, antes do relevo): cada lugar com o tipo, o nome, o centro, o raio e o giro, as estradas, o metrô e a casa da família. Leva ~0,1 s no Pequeno e ~0,5 s no Grande; o mundo inteiro, 0,3, 0,8 e 2 s.
    - **As cidades:** o contorno irregular, o centro (até 28% do raio), o anel do comércio (até 52%) e os bairros de casas em fatias em volta, cada um com nome (6 a 8 na cidade grande, 3 a 4 na pequena; na pequena, o comércio é o "Centro"). A vila é um bairro só. Cada cidade fica no lugar mais liso de vários sorteados, longe da serra, do lago e das outras.
    - **Os outros lugares:** o laboratório no comércio da maior cidade; as bases nos lugares lisos mais para fora; as comunidades (metade aliadas, metade inimigas), as fazendas e as cabanas (um quarto na beira do lago, o resto até o pé da serra); a casa do lago na ilha.
    - **As estradas**, traçadas pelo relevo (A* numa grade de 24 m): fogem da subida forte (rodovia 8%, estrada 12%, terra 15%), não passam no lago e só sobem a serra nas saídas e nas de terra; elas se juntam onde já há estrada. As rodovias ligam as cidades grandes e pequenas pela árvore mais curta, mais uma ou duas ligações a mais; as saídas (2 a 4) vão até a borda, subindo a serra em curvas, com a barreira da quarentena; as vilas, as bases e as comunidades ganham uma estrada, e as fazendas, as cabanas e a beira do lago, uma de terra.
    - **Os postos** em cada saída de cidade e na beira das estradas (a 650 m um do outro), **os bunkers** nas cabanas de preparadores, nas fazendas e nos bairros de casas, e **o metrô** ligando as cidades, quebrado em linhas com nome e cor (3 estações por cidade grande, 1 nas pequenas).
    - **A casa da família** fica num bairro de casas de uma cidade pequena, de frente para o meio dela, e **a partida começa nela** (o platô em volta), até a casa de verdade entrar.
    - **Os nomes** são brasileiros e não repetem no mapa: as cidades (São Sebastião, Pedra Branca, Santa Helena Velha), os bairros (Recanto São Jorge, Jardim das Flores), as fazendas e os sítios, as bases, as comunidades (Colônia Nova Vida; Os Abutres), os postos e o laboratório.
  - **O mapa pintado:** o relevo sombreado, a serra, o lago, as cidades com as zonas e as ruas, as fazendas em faixas, as bases e as comunidades cercadas (verde as aliadas, vermelho as inimigas), as estradas (a rodovia amarela, a estrada branca e a de terra tracejada), o metrô em vermelho e os ícones (postos, bunkers, cabanas, barreiras, a casa do lago e a da família). Os nomes vão por cima, sem um encostar no outro.
    - **No jogo, a tecla M:** o mapa inteiro, a roda do mouse aproxima, arrastar move, Espaço volta para você, e os jogadores são setas para onde olham (você em ciano). Por enquanto mostra tudo; a descoberta (só onde o grupo passou) fica para depois.
    - **No Editor:** Zombie City Cleaner > Mundo > Imagens do mapa grava as sementes 1 a 10 de cada tamanho em `Imgs/Mapas/` (com o título, os nomes e a legenda) e uma folha de cada tamanho com as 10 lado a lado (`Mapas_Pequeno.png`, `Mapas_Medio.png` e `Mapas_Grande.png`).
  - **As quantidades nas 30 sementes** seguem a tabela de cima: no Grande, 3 cidades grandes, 6 a 8 pequenas, 8 a 12 vilas, 20 a 30 fazendas, 31 a 45 cabanas, 15 a 20 postos e 20 a 29 bunkers. Estradas: 13 a 18 km no Pequeno, 32 a 39 no Médio e 68 a 76 no Grande (uns 45% de rodovia). O metrô: 1 linha no Pequeno, 1 a 3 no Médio e 2 a 4 no Grande.
  - **Conferido:** os 23 testes passam (os 8 novos: as quantidades, nada se encosta, as cidades se ligam e todo lugar tem estrada, os nomes não repetem, a casa da família num bairro de casas, o laboratório, os postos, as barreiras e o metrô, a mesma semente dá o mesmo desenho, o mapa pintado); nas 30 sementes, nenhum lugar ficou sem estrada; em Play (Médio, semente 7), o jogador nasce na casa da família e o M abre o mapa.
  - **O F1 ganhou "Sempre de dia"** (a pedido): o relógio para ao meio-dia para todos; clicando de novo, ele volta a andar dali.
  - **As fotos:** as imagens em `Imgs/Mapas/` e o M no jogo em `Imgs/Renders/Mundo_M3_MapaM_Inteiro.png` e `_Perto.png` (Médio, semente 7).

### Parte M4: as estradas e as placas

- As malhas das estradas, o terreno acomodado embaixo, os cruzamentos e o guard-rail.
- As placas de direção com os nomes e as distâncias, e as placas de entrada das cidades.
- Os carros abandonados e as blitz.
- Os postos de gasolina, por enquanto só por fora.
- **Pronto quando:** dá para ir de carro de uma cidade a outra só seguindo as placas.
- **Estado (26/10/2026): feita e aprovada** depois do conserto do primeiro teste ("Testei e está ok, pode seguir para a M5"). Os detalhes estão no `CLAUDE.md` do projeto Unity ("O mundo gerado").
  - **O conserto (26/10/2026)**, do teste do usuário ("na estrada que sobe até o fim do mapa em zigue-zague, as estradas estão bugadas e tem placas no meio da rua" e "a pé eu consigo passar o bloqueio"):
    - **As curvas da serra:** a busca do caminho agora vira no máximo 45° de cada vez (antes ela voltava em zigue-zagues de 135°, e a faixa do asfalto dobrava por cima dela mesma, com pontas no ar). As curvas ficam abertas: a mais fechada do asfalto tem uns 15 m de raio. As saídas sobem menos (até 16 m depois da cerca) e as cabanas ficam um pouco mais para dentro.
    - **As placas e os carros longe das outras pistas:** cada placa procura um lugar livre (o lado direito, o esquerdo ou outra distância do cruzamento) e não fica em cima de nenhuma estrada; os carros da pista e do acostamento só saem na própria estrada, e os batidos longe de todas. As placas com nomes compridos ficam mais largas.
    - **A cerca da quarentena em volta do mapa inteiro:** o alambrado com arame farpado no meio da faixa da serra, de quina a quina, acompanhando a subida, com a placa "ÁREA DE QUARENTENA / Proibida a passagem" a cada 120 m. Onde uma estrada sai, a cerca vira o portão: a fila de contêineres em cima da linha dela, fechando a estrada e o acostamento, com as barreiras de concreto, os sacos de areia e o blindado do lado de dentro. As paredes invisíveis ficam logo atrás da cerca (2,5 m). O mapa mostra a cerca tracejada em vermelho.
    - **Conferido:** os 34 testes passam (os novos: as curvas não dobram, as placas não ficam na pista, a cerca fecha o mapa sem vão e nada fica além dela); as viagens pelas placas continuam todas chegando. Em Play, empurrando o jogador para fora em 45 pontos dos portões do sul e do leste, ele parou sempre na cerca ou nas barreiras.
  - **A rede de estradas:** o desenho da M3 virou uma rede sem estrada repetida (onde uma estrada andava por cima de outra, esse pedaço sai e as pontas encostam nela, e ali fica o cruzamento), com os nós (os cruzamentos e os lugares) e os trechos entre eles. No Grande (semente 4242): 97 pedaços, 67 km, 163 nós e 69 cruzamentos.
  - **A altura do asfalto:** o relevo borrado em ~22 m em volta de cada ponto, suavizado ao longo da estrada, igual nas estradas que se encontram e plano em volta de cada nó (9 m, voltando ao relevo em 30 m). No asfalto, a rampa mais forte fica entre 18 e 24%, e só 1% dos trechos passa de 12%.
  - **O chão acomodado:** na altura da estrada até 4 m além do asfalto (o acostamento, de cascalho; de terra nas estradas de terra), e depois o corte ou o aterro, de uns 30°. O chão bate com o meio do asfalto (no máximo 13 cm, em 12 de 5.600 pontos acima de 5 cm). Nos zigue-zagues mais fechados da serra, em menos de 1% da beira, o barranco ainda passa um pouco por cima.
  - **As malhas:** a rodovia de 10 m (duas faixas, as linhas brancas da beira, a amarela tracejada no meio e o acostamento asfaltado), a estrada de 7,5 m e a de terra de 4,5 m; o pátio redondo nos cruzamentos (as linhas param perto deles); e o guard-rail onde o aterro passa de 1,5 m, com colisão. De longe, os blocos com estrada ficam com mais detalhe (no relevo mais grosso, o barranco cobriria a beira do asfalto).
  - **As placas de direção** (verdes, com as letras brancas e a seta de cada saída): em cada cruzamento, uma para quem chega por cada lado, com os lugares de cada saída pelo caminho mais curto e a distância ("→ Ibitinga 1,0 km"). Por saída, como nas rodovias: a próxima cidade, a cidade grande e a mais longe daquele lado. Quem procura uma cidade que não está escrita segue a escrita mais perto dela no mapa até ela aparecer.
    - **Conferido** (um teste que dirige de cada cidade, grande ou pequena, para cada outra só pelas placas): nas sementes 1 a 10 e 12345 de cada tamanho, todas as viagens chegam (66 no Pequeno, 300 no Médio e 996 no Grande), e no Grande 6 em cada 10 só pelo nome do destino.
  - **As placas de entrada** ("Bem-vindo a São Sebastião", com a "População 12.678" riscada à mão), em cada estrada que entra numa cidade ou vila.
  - **A beira das estradas:**
    - as blitz abandonadas em quase metade das entradas das cidades: as barreiras, o cavalete e os cones derrubados, o caminhão do exército, os sacos de areia e a fila dos carros que tentaram sair;
    - a cerca da quarentena em volta do mapa e os portões nas saídas: os contêineres atravessando a estrada (uns empilhados), as barreiras de concreto, os sacos de areia e o blindado;
    - os carros abandonados: parados na pista, no acostamento ou batidos no barranco, alguns capotados (uns 150 no Grande);
    - os postos, por fora: a loja, a cobertura com as bombas e o totem, num pátio plano no nível da estrada.
  - **A casa da família** agora fica a pelo menos 58 m de qualquer estrada (o platô dela não encosta no asfalto).
  - **O texto das placas** usa a Arial do sistema (parecida com a das placas brasileiras), sem baixar fonte nenhuma.
  - **Os passos** no asfalto e no acostamento soam como concreto.
  - **A geração** leva ~3 s no Grande (antes da M4, ~2), 1,3 s no Médio e 0,4 s no Pequeno.
  - **O desempenho** (no Editor, a 108 km/h por 45 s, criando o chão, o asfalto e a beira da estrada na frente): pela rodovia mais comprida, média de 3,1 ms por quadro (~320 FPS), 1% pior 4,6 ms e nenhum quadro acima de 33 ms; pela saída do leste até a cerca e depois ao longo dela, média 3,1 ms, 1% pior 4,4 ms e um quadro de 40 ms. Os picos de ~28 ms a cada ~5 s são a coleta de lixo, não o chão nem os objetos. Nenhum quadro sem o chão embaixo.
  - **As fotos:** `Imgs/Renders/Mundo_M4_*` (a placa de direção, a de entrada, o posto, a blitz, um cruzamento, o portão da quarentena do sul, a placa de aviso na cerca e o fim da saída do leste, subindo a serra).

### Parte M5: as cidades em blocos

- Como a Parte 4 da demo: primeiro caixas, para acertar o tamanho.
  - As ruas, as calçadas, os quarteirões e os lotes, com as zonas.
  - As construções como caixas do tamanho certo (de 1 a 15 andares), com os postes, os carros e as placas de rua.
  - As vilas.
- O clima ligado, a oclusão pela GPU e o benchmark no centro da cidade grande.
- **Pronto quando:** andar no centro da cidade grande em blocos, de dia e de noite, já passa a sensação de cidade grande, e os números do benchmark estão aqui.
- **Estado (26/10/2026): feita e aprovada** ("Testei e está ok, pode seguir para a M6"). Os detalhes estão no `CLAUDE.md` do projeto Unity ("As cidades em blocos").
  - **A grade de cada cidade e vila** (`TracadoDaCidade`): as ruas no giro da cidade, um pouco tortas (ondas largas no plano todo e curtas no centro da cidade grande: as ruas antigas), com as linhas mais juntas no centro. As avenidas são as do meio e, na cidade grande, uma a cada 4 linhas; rua de 8 m com 2,5 m de calçada, avenida de 14 m com 4 m. Os cruzamentos têm o asfalto do meio, as esquinas e a calçada fechando os braços sem rua. As ruas e as avenidas têm nome ("R. Visconde de Mauá", "Av. ..."), na placa de cada esquina. A vila é a rua principal com as travessas e, se couber, as paralelas.
  - **Os lotes por zona**, como caixas do tamanho certo: no centro, os prédios de 5 a 15 andares (mais altos perto do meio), com o saguão de vidro e as faixas de janela; no comércio, as lojas de 1 a 3 andares (na pequena, 2 a 4) com a vitrine e o toldo, coladas na calçada; nos bairros, as casas de 1 ou 2 andares com o telhado de duas águas, recuadas, com o quintal. Mais as praças (a do meio e umas nos bairros), os estacionamentos, os terrenos baldios e o laboratório murado. Os lotes de casa têm 16 a 22 m de frente (a casa e a garagem em ×1,15 da M6).
  - **As quantidades** (Grande, semente 12345): a maior cidade (raio 992 m) tem 368 quarteirões e 2.994 construções; as outras grandes, 1.440 a 1.650; as pequenas, 97 a 189; as vilas, 23 a 57. **Ficou abaixo da conta da tabela** (uns 380 quarteirões e 4 mil construções na maior; uns 300 lotes na pequena): os quarteirões batem, mas cada lote de casa é largo para caber a casa com a garagem. Se faltar densidade, dá para estreitar os lotes ou pôr casas geminadas.
  - **As rodovias entram pelos portões** (as pontas das avenidas do meio): a estrada chega pela avenida e, dentro do contorno, só anda nela; o caminho pelas ruas até o meio da cidade entra nas placas, sem asfalto a mais. O A* contorna as cidades.
  - **O chão da cidade** é o relevo borrado, com a subida limitada a 4,5% (no teste, nenhum cruzamento passa de 7% até o ponto 8 m ao lado), e volta ao relevo fora do contorno. Perto do lago, a volta cede à bacia sem degrau.
  - **A casa da família** ocupa um lote duplo num bairro de uma cidade pequena, e a laje da partida fica nele, sem o platô de antes.
  - **As ruas têm** os postes (dos dois lados nas avenidas, alternando nas ruas; uns quebrados, uns piscando), as placas de rua, os carros parados junto do meio-fio e uns largados tortos, as árvores (40% secas) nos bairros e nas praças, os hidrantes e as lixeiras.
  - **De noite:** todos os postes acendem a lente e o cone na neblina, e os 24 mais perto ganham a luz de verdade; umas janelas de cada construção acendem. (Saiu em 27/10/2026: anos depois do apocalipse, não há energia.)
  - **A geração** leva ~5 s no Grande (antes da M5, ~3 s), 1,9 s no Médio e 0,7 s no Pequeno.
  - **O benchmark** (no Editor, o F1 do mundo, "Benchmark da cidade"; a tabela em `Benchmarks/Benchmark_Cidade.md` do projeto Unity): no centro de Cruz Alta (Grande, semente 12345), uma volta de 8 s no cruzamento do meio e 14 s pela avenida do meio a 8 m/s, sem vsync, sem zumbis (a cena do mundo ainda não tem):

    | Hora | Game view | Quadro médio | p95 | Pior | GPU | Draw calls | SetPass | Triângulos | Luzes de verdade |
    |---|---|---|---|---|---|---|---|---|---|
    | 12h | 1918 × 887 | 4,3 ms | 6,0 ms | 25,2 ms | 1,7 ms | 281 | 76 | 226 mil | 1 |
    | 12h | 1080 × 1920 | 3,9 ms | 5,2 ms | 58,8 ms | 1,7 ms | 193 | 75 | 115 mil | 1 |
    | 23h | 1080 × 1920 | 4,1 ms | 5,4 ms | 28,5 ms | 1,7 ms | 209 | 77 | 118 mil | 18 |

    Uns 240 FPS no Editor; a GPU sobra (1,7 ms), e quem pesa é a thread principal. A neblina esconde quase tudo além de ~30 a 40 m, e a câmera vê até 150 m.
  - **A oclusão pela GPU ficou desligada.** Ligada no meio do Play (a primeira versão do benchmark fazia isso), o driver de vídeo caiu na hora (D3D11, "device removed", na RX 9060 XT) e o Editor fechou. O benchmark agora mede com o asset da URP como está. Com 200 draw calls e a GPU folgada, ela não faz falta por enquanto; dá para tentar de novo ligando no asset fora do Play, ou no D3D12.
  - **Conferido:** os 40 testes passam (os 6 novos: toda cidade tem ruas, lotes e portões, com as quantidades; os lotes nos quarteirões e fora das estradas; o chão quase plano; as estradas só pelas avenidas dos portões; as coisas das ruas no lugar certo; as malhas da cidade, com colisão e até 20 mil triângulos por bloco). Nas 33 sementes, as viagens pelas placas continuam todas chegando (66, 300 e 996) e nenhuma placa ficou sem lugar. As 30 imagens do mapa foram refeitas (`Imgs/Mapas/`), com as ruas e as zonas das cidades.
  - **As fotos:** `Imgs/Renders/Mundo_M5_*` (Grande, semente 12345): o centro de Cruz Alta de dia e de noite, de cima (sem a neblina, só para a foto), uma rua de casas e a vila Santa Clara do Sul.

### Parte M6: as construções de verdade

- O kit de construção no jogo (o `Obra` e o `cidade_kit` do Blender passados para C#):
  - paredes com aberturas, lajes com buraco, escadas, telhados, molduras e janelas;
  - as cores da paleta e as malhas de colisão.
- As plantas com variações, em fatias, cada uma aprovada antes da próxima:
  1. as casas (1 e 2 andares, porão, a garagem ou a vaga com o ponto do carro) e a casa da família;
  2. os prédios (até 15 andares), com os andares carregando perto do jogador;
  3. as lojas (mercado, armas, hospital, farmácia, 24 horas, posto);
  4. as especiais: fazendas, cabanas, a casa do lago, a base militar, as comunidades e o laboratório por cima (deixada para depois, a pedido em 27/10/2026: antes vêm os pontos dos móveis e o corte das travadas).
- **Em todas as fatias:** os interiores vazios, mas com o piso, os pontos dos móveis, a área interna e o eco; os interiores carregando de perto; o NavMesh por dentro.
- **Pronto quando** (para cada fatia): andar por dentro de todas as variações sem cair, sem prender e sem buraco, e o zumbi chega em todos os cômodos.
- **Estado da primeira fatia, as casas (26/10/2026): feita e aprovada** ("Testei e está ok, pode seguir para a segunda fatia da M6"). Os detalhes estão no `CLAUDE.md` do projeto Unity ("As casas de verdade").
  - **As casas nos lotes:** nos bairros e nas vilas, cada lote é do tamanho da casa sorteada para ele, mais o vão de 1,7 a 2,4 m de cada lado (5% ficam baldios). São 24 variações: térrea ou sobrado, com ou sem porão, garagem, garagem com porta para dentro ou vaga, espelhada. O chão fica plano em volta de cada casa (o patamar), com o buraco do porão no terreno e a calçada de concreto em volta dele, o caminho até a calçada e a entrada do carro. Há carros em 70% das garagens e vagas.
  - **A casa da família:** uma térrea sem porão no meio do lote duplo (o bunker dela é da M10). A partida começa na sala, olhando para a porta da frente, e o carro que se dirige fica na garagem.
  - **Perto e longe:** até 95 m de quem precisa de colisão, as casas de verdade (por dentro, com a colisão, o piso de cada cômodo para os passos, a neblina de dentro e o eco); longe, as caixas da M5. O NavMesh é montado em volta de cada jogador (no anfitrião), e o F1 confere as casas (o zumbi chega em cada cômodo? o jogador anda até lá?).
  - **A densidade caiu:** com os lotes do tamanho da casa e o vão de lado, a maior cidade do Grande (semente 12345) tem 2.465 construções (1.652 casas), contra 2.994 da M5.
  - **Conferido no Médio (semente 7):** os 47 testes passam. Numa casa de cada variação, o zumbi chega nos 168 cômodos, e o jogador andou até os 168 sem cair nem prender. Para isso, a entrada do porão por fora das térreas (o único caminho até o porão delas) ficou mais larga, com as duas folhas da tampa abertas e a porta da largura das outras.
  - **A travessia** (60 s a 160 km/h pela cidade): média 5,6 ms (~180 FPS no Editor), 1% pior 15,9 ms, pior 58 ms, 14 quadros acima de 33 ms, o terreno no máximo 20,7 ms num quadro e nenhum quadro sem o chão embaixo. Sem as casas de verdade, 5,3 / 10,9 / 43 ms e 2 quadros acima de 33 ms. A diferença é a coleta de lixo das montagens das casas: cortar as alocações do kit é o próximo passo de desempenho, antes dos prédios de 15 andares da segunda fatia.
  - As fotos em `Imgs/Renders/Mundo_M6_*`: a sala da casa da família, o bairro de cima, a garagem da família com o carro, o fosso do porão (de lado e ao longo da escada) e o porão por dentro.
- **Estado da segunda fatia, os prédios (27/10/2026): feita e aprovada** ("vamos seguir"; antes da mobília, cortar as alocações do kit das construções, no fim da M6). Os detalhes estão no `CLAUDE.md` do projeto Unity ("Os prédios de verdade").
  - **Os prédios nos lotes:** no centro das cidades, cada lote de prédio (9 a 52 m de frente) ganha uma lâmina de até 44 × 24 m, de 5 a 15 andares, com o corredor de um lado ao outro e, atrás dele, o núcleo: a escada em U e o poço do elevador. Os apartamentos ficam dos dois lados do corredor (cozinha, sala, banheiro e quarto; a quitinete nos estreitos), com a lavanderia atrás do poço e os depósitos onde não cabe apartamento. No térreo, a portaria e as lojas (60%) ou mais apartamentos; na cobertura, a casinha da escada, a casa de máquinas, a caixa d'água e a antena; a escada de incêndio do 1º ao 4º andar. Variam: espelhados, as lojas, o poço aberto (metade: num andar, as portas do elevador ficam abertas e dá para cair), as cores, os papéis e os pisos de cada apartamento em cada andar. Lote de prédio pequeno demais vira baldio. No Médio (semente 7), 115 prédios.
  - **Perto e longe:** a casca (a fachada de todos os andares, as lajes, o núcleo com a escada inteira e a cobertura) existe com as casas de verdade, a 95 m de quem precisa; por dentro, só carregam os andares perto de alguém: o dele e os dois de cima e de baixo, e de fora (até 30 m) o térreo e o primeiro. Os outros andares ficam com as janelas escuras. Uma casca tem 10 a 13 mil triângulos; um andar, 3,5 a 8 mil, montado em outra thread em ~5 ms. O NavMesh vai da rua à cobertura (15 m abaixo do chão a 65 m acima) e se refaz quando um andar carrega ou sai.
  - **Conferido no Médio (semente 7):** os 53 testes passam. Num prédio de cada uma das 8 variações (10 a 14 andares), o zumbi chega nos 3.120 cômodos e na cobertura, o jogador andou até os 502 cômodos do térreo e do primeiro andar sem cair nem prender, e subiu da calçada à cobertura pela escada nos 8. Para isso:
    - o NavMesh passou a se refazer em volta do jogador: antes, com ele a uns metros do meio da área, ela não se refazia mais, e os andares que carregavam ficavam fora (o zumbi não subia);
    - a conferência mede os caminhos de patamar em patamar (da rua até o 11º andar de uma vez, a busca do NavMesh passava do limite e voltava pela metade).
  - **A travessia** (60 s a 160 km/h, pelo centro da maior cidade, com os prédios): média 3,6 ms (~280 FPS no Editor), 1% pior 9,8 ms, pior 70,9 ms, 13 quadros acima de 33 ms, o terreno no máximo 15,4 ms num quadro e nenhum quadro sem o chão embaixo.
  - **A memória:** carregar e soltar os andares não deixa sobra (72 andares, +6 MB), e o terreno quase nada (+5 MB por ida e volta de 400 m). O NavMesh deixa uns 0,35 MB por montagem. No Editor, a memória se acumula de partida em partida e não volta ao parar o Play: numa sessão longa ele passou dos 9 GB e caiu sem memória. Para medir ou rodar a conferência inteira (~40 min), reinicie o Editor antes.
  - As fotos em `Imgs/Renders/Mundo_M6_Predios_*`: a rua, a vitrine (de dentro da loja da frente), a portaria, uma loja, o corredor, um apartamento, a escada, a cobertura, o poço aberto e as janelas escuras.
- **Estado da terceira fatia, as lojas (27/10/2026): feita e aprovada** ("Testado"). Os detalhes estão no `CLAUDE.md` do projeto Unity ("As lojas de verdade" e "O hospital").
  - **As lojas nos lotes do comércio:** cada lote ganha a loja do tamanho da construção, colada na calçada e nos vizinhos, de 1 a 4 andares. O salão na frente, com as portas e as vitrines (com as portas de enrolar recolhidas em cima), e os fundos atrás. Com mais de um andar, a escada em U nos fundos e um apartamento em cada andar de cima (sala, quarto, cozinha e banheiro). Por fora, a placa, o toldo, os ar-condicionados e, na cobertura, a platibanda e a caixa d'água.
  - **Os tipos, por cotas em cada cidade** (a grande: 3 mercados, 1 loja de armas, 4 farmácias e 4 lojas 24 horas; a pequena: 1, 0, 2 e 1; o resto é loja comum):
    - **o mercado:** um andar alto (5,5 m), as duas portas, os caixas, o açougue, o depósito com a doca de carga para a rua de trás, o escritório e o banheiro;
    - **a loja de armas:** as grades nas vitrines e nas janelas, o balcão atravessado no salão e, nos fundos, o escritório e o depósito atrás da porta de aço (o melhor saque atrás da porta mais difícil, quando houver saque e arrombamento);
    - **a farmácia** (a cruz verde e o balcão) e **a loja 24 horas** (o caixa ao lado da porta), com o estoque e o banheiro;
    - **o posto:** a loja de conveniência de verdade, de frente para as bombas, no lugar da caixa fechada da M4;
    - **o hospital** (um em cada cidade grande, no lugar de um prédio perto da beira do centro, de 4 a 6 andares): a recepção, a emergência com a porta para a rua, a triagem, a espera, os consultórios e o raio-x no térreo; as enfermarias, o posto da enfermagem, os quartos e a rouparia em cima; a escada e o poço do elevador dos prédios, a placa e a cruz vermelha.
  - **Perto e longe:** de perto, a loja inteira de uma vez (por dentro, com a colisão, os pisos, a área interna e o eco), como as casas; longe, as caixas da M5. O hospital é um prédio: a casca sempre perto, e os andares carregando.
  - **Conferido no Médio (semente 7):** os 60 testes passam. 590 lojas, em média 6 mil triângulos cada (a maior, 16 mil). Numa loja de cada uma das 9 variações (os tipos, com e sem apartamento em cima), o zumbi chega nos 60 cômodos e o jogador andou até os 60 sem cair nem prender. No hospital de São Bento (5 andares), o zumbi chega nos 71 cômodos e na cobertura, e o jogador andou pelos 29 cômodos do térreo e do primeiro andar e subiu à cobertura. Na loja do posto, o zumbi chega nos 3 cômodos.
  - **A travessia** (o caminho da segunda fatia, com as lojas): média 3,5 ms (~290 FPS no Editor), 1% pior 10,2 ms, pior 75,1 ms, 22 quadros acima de 33 ms (eram 13), o terreno no máximo 9,2 ms num quadro e nenhum quadro sem o chão embaixo. Os picos a mais devem ser a coleta de lixo das montagens: cortar as alocações do kit, no fim da M6, antes da mobília.
  - **Ficou para depois:** o texto de verdade nas placas das lojas (hoje, blocos que lembram as letras) e as lojas das vilas (a vila é um bairro só de casas, sem comércio).
  - As fotos em `Imgs/Renders/Mundo_M6_Lojas_*`: o mercado, a farmácia e a loja de armas (de fora e por dentro), a escada e o apartamento de cima de uma loja, o hospital e a recepção, e o posto (de fora e por dentro).
- **O que falta da M6 (27/10/2026):** a quarta fatia (as especiais, deixada para depois); os pontos dos móveis, que estão em "Em todas as fatias" mas nenhuma das três fez (as casas, os prédios e as lojas não têm onde os móveis e o saque vão entrar); e cortar as alocações do kit das construções, antes da mobília. O usuário pediu os dois últimos agora (27/10/2026: "ao andar pela cidade fica dando umas travadinhas").
- **Estado do fim da M6, sem a luz da noite, as travadas e os pontos dos móveis (27/10/2026): feito e aprovado** ("Testei e as travadas sumiram"). A M6 fica completa menos a quarta fatia (as especiais), deixada para depois. Os detalhes estão no `CLAUDE.md` do projeto Unity ("Os pontos dos móveis", "O NavMesh" e o estoque das malhas no kit).
  - **Sem energia:** os postes ficam apagados (sem a lente acesa, o cone e a luz de verdade) e as janelas das caixas de longe não acendem mais. De noite, a luz é a da lua, a da lanterna e a do carro.
  - **As travadas eram do NavMesh, não da coleta de lixo:** a montagem da área de 120 × 120 m em volta do jogador usava todos os workers de jobs, e a física e o desenho esperavam por ela (o Profiler mostrou o passo da física esperando de 20 a 60 ms). Com 2 workers para o NavMesh, andando pelo centro de São Bento, os quadros acima de 33 ms caíram de 20 para nenhum em 60 s (o 1% pior de 8,6 para 5,1 ms); a 160 km/h, de 18 a 22 para nenhum (o pior quadro de 75 a 92 ms para 20 ms; a média de 4,3 para 3,3 ms).
  - **O corte das alocações:** o estoque das malhas do kit refeito (três tamanhos, cada malha volta para o dela, e a montagem fica com a colisão em vez de copiar). Montar 50 blocos do centro e do comércio deixava 332 MB de lixo; agora, 10 MB. As coletas de lixo, de 33 para 22 por minuto.
  - **Os pontos dos móveis:** em cada cômodo, os lugares dos móveis (encostados nas paredes; as gôndolas no meio dos salões), com o tamanho e se guardam saque: 75 mil no Médio, uns 19 por casa. Nenhum na frente das portas nem alto na frente das janelas, e as portas de cada cômodo continuam ligadas. No F1, "Mostrar os pontos dos móveis" mostra cada um como uma caixa (amarela onde vai ter saque). Por enquanto são só dados: os móveis e o saque entram depois.
  - **Conferido no Médio (semente 7):** os 67 testes passam. As fotos em `Imgs/Renders/Mundo_M6_Moveis_*`: a sala, o quarto e a cozinha da casa da família, o salão do mercado e o da farmácia, o depósito da loja de armas, a sala de um apartamento e a enfermaria do hospital.

### Parte M9: a vegetação e o mundo abandonado (agora; reordenado a pedido em 27/10/2026)

O mapa todo se passa anos depois do começo do apocalipse: entre as cidades, a mata tomou conta; dentro delas, as construções e as ruas mostram o abandono. A floresta entra direto no jogo; o abandono (as texturas, as raízes, as pichações, a sujeira das ruas) é montado antes numa cena de testes, para o usuário aprovar o visual.

**A pesquisa (27/10/2026).** Nos jogos de cidade tomada pelo mato (a referência mais citada é The Last of Us), o abandono sai de duas camadas: no material, a mistura de camadas (sujeira, limo, rachaduras, umidade, a pintura descascando) por máscaras (a cor do vértice, a altura sobre o chão, a direção da face, um ruído); e por cima, geometria (as trepadeiras em módulos, os cartões de folhas e o mato no chão) para quebrar a silhueta. As pichações e as manchas costumam ser decalques. Aqui, como as construções são feitas pelo jogo, as máscaras podem sair do próprio kit (cada face sabe se é parede de fora, a altura e onde fica na parede), e a geometria do abandono pode ser montada junto com a casa, sem custo novo de objetos. O estilo continua low-poly e de cores apagadas: as texturas são desenhadas (manchas, escorridos e rachaduras em formas simples), não fotos.

**A inspiração do usuário (27/10/2026, `Imgs/Inspiracao_Cidades.webp`):** uma rua do centro e uma de bairro, abandonadas, na neblina. **A vegetação é de cubos**, no estilo "quadradão" do jogador e das armas: as copas das árvores são montes de cubos em vários tons de verde (cada cubo do seu tom, uns saindo da forma), a hera desce pelas paredes, pelas quinas e pelos postes em faixas de cubos pequenos, e o mato das calçadas, das sarjetas e das rachaduras são tufos de cubos. As paredes têm textura (o tijolo, o reboco, o lambri); as janelas estão quebradas (o buraco preto torto) ou fechadas com tábuas em X; os toldos rasgados. Na rua, as caçambas, os sacos pretos, as caixas de papelão, os tijolos soltos, as poças, o asfalto rachado e os carros enferrujados; no bairro, a cerca branca, a caixa de correio, a varanda, o gramado e a placa de velocidade.

- **M9a: as árvores e a floresta (direto no jogo).**
  - **As espécies** (no Blender, por script, como os objetos da rua; a mata do norte da América do Norte, a pedido em 27/10/2026: o jogo não se passa no Brasil; low-poly facetado, sem os cubos da primeira versão, também a pedido; cores apagadas para a neblina, com o começo do outono; 2 ou 3 variações de cada e os LODs de longe; o vento na cor do vértice, como no Guardiões): o abeto (o spruce, o cone escuro, e o novo, das fazendas de árvores de Natal), o pinheiro-branco (os galhos em andares tortos), o pinheiro-vermelho (o dos pinheirais plantados), o bordo (verde, vermelho e laranja), o carvalho, a bétula (os troncos brancos), o salgueiro-chorão (na beira do lago), as árvores mortas (a folhosa e o pinheiro morto em pé), os tocos, os troncos caídos, os arbustos (um do outono), as samambaias, o capim alto, a grama baixa, a taboa e as pedras.
  - **As manchas do mapa:** a mata mista (os bordos, os carvalhos, as bétulas e os pinheiros) e a de coníferas (os abetos e os pinheiros), fechadas; a rebrota (o campo virando mata: as mudas, as bétulas e os arbustos); o campo (o pasto abandonado, de capim alto, com as mudas e umas árvores soltas); os plantios abandonados em fileiras (o pinheiral e a fazenda de árvores de Natal); a beira do lago (os salgueiros, as bétulas e a taboa) e as serras (os abetos e as pedras). Nada nas estradas, nas ruas, no lago e nas construções; **sem vazios** (a pedido): o campo também é coberto de mato, a grama baixa cobre o chão livre perto do jogador, a mata chega até a cidade e fecha a beira das estradas (até o acostamento), e nas cidades os baldios e os fundos dos quintais têm árvores. **Os pinheiros e os abetos na maior parte** (a pedido: 75% das árvores).
  - **O desenho:** sem um objeto por árvore: as árvores de cada bloco de 64 m em listas de instâncias (`Graphics.RenderMeshInstanced` por espécie e LOD, o caminho do `VegetationStreamer` do Guardiões), a sombra só perto, a colisão do tronco (uma cápsula) só perto dos jogadores e o obstáculo no NavMesh.
  - **Pronto quando:** as matas entre as cidades têm variedade e o clima certo, e a travessia continua sem travar.
  - **O estado (27/10/2026): aprovada** ("Testei a floresta e está ok"). A primeira versão (as espécies brasileiras, de copa de cubos) foi trocada no mesmo dia, a pedido. As 34 plantas no Blender (`cidade_arvores.py`, `Cidade_Arvores.fbx`), cada uma com 2 LODs de longe (as árvores grandes de 300 a 1.400 triângulos de perto); no jogo, a `Floresta` do gerador (as manchas, os plantios, as plantas e a grama de cada bloco e as folhas no chão embaixo da mata; a versão do gerador passou para 9), a `FlorestaDoMundo` (as instâncias, as sombras de perto, o mato e a grama encolhendo no fim do alcance, os troncos com colisão e no NavMesh, o vento) e o shader da vegetação (a grama e o capim com a luz do chão). Depois, também a pedido: as trocas de LOD longe do jogador (a 55 e 100 m; a 30 m, apareciam), a mata indo até as cidades e fechando a beira das estradas, as árvores nos baldios e nos fundos dos quintais e mais coníferas. No Médio 7: 75% dos blocos com mata fechada, 75% das árvores coníferas, nenhum bloco do campo vazio, 16.800 árvores dentro das cidades; 13 plantios no Médio e 44 no Grande. A travessia: andando pela mata a 18 km/h, 4,8 ms de média, 1% pior 6,5 ms, nenhum quadro acima de 33 ms; a 160 km/h, 4,7 ms, 1% pior 8,9 ms, nenhum acima de 33 ms (sem a floresta: 3,3 e 7,0 ms). As fotos em `Imgs/Renders/Arvores_*` (as plantas, no Blender) e `Mundo_M9_*` (a mata mista, as coníferas, o campo, a rebrota, o pinheiral, a fazenda de árvores de Natal, a beira do lago, um baldio, a estrada na mata e a beira da cidade, no jogo). Ficaram para a M9b: as árvores das calçadas e das praças das cidades (continuam as do `Cidade_Rua.fbx`, de outro estilo) e o resto do abandono das cidades.
- **M9b: a cena de testes do abandono (para o usuário aprovar antes de ir para o jogo).** Uma cena de exemplo (`Scenes/Exemplos/Abandono.unity`, como a da rua de casas) com casas, um prédio e uma loja do kit, e as propostas lado a lado em intensidades (pouco, médio e muito abandono), como a sala de medidas:
  - **As paredes:** a sujeira escorrendo das janelas, dos peitoris e do telhado, a mancha de umidade e o limo verde na base, a pintura descascando (o reboco e o tijolo aparecendo) e as rachaduras, por um shader com as máscaras do kit (a altura sobre o chão, a posição na parede, a normal) e um ruído; as cores continuam as da paleta, só manchadas.
  - **As raízes e as trepadeiras:** a hera em folhas (sem os cubos, como a floresta da M9a) descendo das calhas, das platibandas e das quinas e subindo das paredes, com o vento, umas casas tomadas e outras quase limpas; as raízes de árvores que cresceram no quintal levantando a calçada.
  - **As janelas e as fachadas:** a janela quebrada (o buraco preto de borda torta) e a fechada com tábuas em X, os toldos rasgados nas lojas.
  - **O lixo:** as caçambas, os sacos pretos, as caixas de papelão e os tijolos soltos na calçada e no asfalto.
  - **Os objetos** (a segunda referência do usuário, 27/10/2026, `Imgs/Inspiracao_Objetos.webp`: um kit de objetos de rua abandonados, low-poly, com a ferrugem e o limo em manchas). Os que já existem no `Cidade_Rua.fbx` ganham a versão gasta: os carros com a ferrugem em manchas e os vidros quebrados, as barreiras de concreto com o limo em cima, o poste e a cerca com a hera pendurada. Os novos: o semáforo, o poste de madeira com o transformador, a caixa de correio, as placas de seta e as em branco, a van (com a porta de lado aberta), os pneus empilhados, o caixote, o tambor enferrujado, o galão, os paletes, as caixas de papelão, as lixeiras de rodinha e o cesto de arame, os sacos de areia, a barreira de plástico, o paraciclo, o alambrado, a cerca de ripas (inteira e quebrada), as caixas de energia, a tampa de bueiro e a grade da boca de lobo, os pedaços de asfalto quebrado (com a faixa amarela), os tijolos, os blocos de concreto e o entulho. A caçamba pode levar a pichação. Os arbustos, o capim, o tronco caído e a hera pendurada são da vegetação (M9a).
  - **As pichações:** placas na parede com um atlas desenhado em código: as tags, as mensagens dos sobreviventes, em inglês (o jogo se passa nos Estados Unidos: "HELP", "STAY OUT", "DEAD INSIDE", "MOM, WE WENT NORTH", as setas e as contagens de dias) e o X das equipes de busca (a data, a equipe, o perigo e quantos mortos), mais no centro, nos muros e nas portas das lojas.
  - **As ruas e as calçadas:** o asfalto com rachaduras, remendos e manchas, a terra e as folhas acumuladas nas sarjetas, as poças, e o mato saindo das rachaduras, das sarjetas e das juntas das calçadas (tufos de cubos, em instâncias); os baldios e os quintais com o mato alto, os arbustos e as árvores pequenas nascendo.
  - **Pronto quando:** o usuário escolhe, na cena, o visual e a intensidade de cada coisa.
  - **O estado (27/10/2026): aprovada, com o grau "muito"** ("Seria o muito então [...] pode levar pro jogo o abandono"). A cena (menu Zombie City Cleaner > Exemplos > Abandono (Parte M9b)) tem 4 trechos de rua de 75 m, com as mesmas 3 casas, a loja de 2 andares e o prédio de 6 andares do kit, cada um num grau: hoje (o material da cidade, sem nada), pouco (0,33), médio (0,66) e muito (1). O que já tem:
    - **o material do abandono** (o shader `ZombieCityCleaner/Abandono`, sem mudar as malhas: o tipo de cada cor da paleta numa textura, e as marcas de um ruído no plano da face): a pintura desbotada, escorrida, descascando em grupos de manchas, mais embaixo (o reboco e, no muito, um pouco do tijolo), e a umidade e o limo na base; o salitre na alvenaria; o musgo nos telhados; a ferrugem nos carros e nos postes; a madeira acinzentada; o vidro encardido; nas ruas e nas calçadas, as rachaduras com limo, os remendos, as manchas de óleo e de água, a terra e as folhas e as poças. Cada marca tem a sua força no material (`Abandono.mat`), para a escolha;
    - **as janelas:** o vidro sujo, inteiro ou com o buraco torto; as tábuas (em X ou deitadas) e a chapa de compensado, às vezes pichada; no prédio também (as fachadas da casca);
    - **a hera** subindo pelas paredes (passando para as dos andares de cima e desviando das janelas) e, no médio e no muito, caindo do alto; umas folhas vermelhas do outono, com o vento;
    - **as pichações:** um atlas de 38, desenhado em código no Blender (`cidade_pichacoes.py`: as fontes do Windows e o spray, com a névoa, os respingos, os escorridos e o desgaste): os recados e os avisos dos sobreviventes, o recado da família, os X das equipes de busca, as contagens de dias, a caveira, a carinha morta, a seta, as assinaturas e as tags coloridas. Nas paredes de fora do térreo, ao lado das portas e nas chapas das janelas, com a cor da lata e o desgaste do tempo;
    - **o mato** nas frestas das calçadas e na sarjeta, os quintais com o capim alto, os arbustos, as árvores novas e a mata chegando atrás dos lotes; **o lixo** (os sacos, os tijolos, as caixas de papelão e a caçamba) e os carros parados.
    - As fotos em `Imgs/Renders/Abandono_*` (a rua, a casa, a frente de perto, a loja, o prédio, a calçada e as pichações; com a neblina do jogo, `Abandono_*_Neblina`) e o atlas em `Pichacoes_Atlas.png`. Faltam, para a segunda rodada: os toldos rasgados, as raízes levantando a calçada e o kit novo de objetos da segunda referência.
    - **O conserto e o peso (27/10/2026, depois do teste do usuário: "Vamos seguir com a versão 3"):** as tábuas e o compensado das janelas do prédio saíam atravessados nas fachadas que correm em z (a fachada da casca registrada sem o giro); a grama da cena ficava no plano do asfalto, e as duas piscavam; e a terra do asfalto, em manchas grandes, parecia uma camada por cima da rua (agora menos e mais fraca; nas calçadas, como estava). O peso no médio, nas 5 construções e na rua de um trecho: de 40,2 mil para 59,6 mil triângulos (+48%: a hera 16,1 mil, as janelas quebradas e tapadas 2,7 mil, o lixo 0,2 mil e as pichações 96); no muito, 91,6 mil (+128%, a hera 46,6 mil). Mais 2 objetos de desenho por construção (a hera e as pichações; no jogo, juntos por bloco), o atlas das pichações (5,3 MB no jogo, em BC7) e o ruído do abandono (0,3 MB). O desenho da vista da rua em 1920 × 1080, na RX 9060 XT: 3,53 ms hoje e 3,64 ms no médio inteiro (sem o shader do abandono, 3,41), perto do ruído da medida. Para a M9c: a hera só perto (até uns 60 m), juntada por bloco.
- **M9c: o abandono no jogo** (depois da M9b aprovada): o que foi escolhido nas casas, nos prédios, nas lojas, nas ruas e nas calçadas do mundo, com variação por bairro (o centro mais pichado, os bairros mais tomados pelo mato) e o desempenho medido.
  - **O estado (27/10/2026): aprovada** ("a M9c foi testada e está ok"). O grau "muito" em todo o mundo (a variação vem do ruído do shader, em manchas de uns 9 m; a variação por bairro ficou para depois), e a casa da família mais leve (0,4: a família mora embaixo dela):
    - **os materiais:** o shader do abandono nas estradas, nas ruas, nas calçadas, nas caixas de longe, nas casas, nas lojas e nos prédios de verdade (por fora e por dentro: o mofo) e nos objetos de rua (os carros, os postes, as placas). Para a umidade na base das paredes, as casas de um bloco guardam a altura de cada vértice sobre o chão do lote (na cor do vértice), e os objetos usam a própria altura;
    - **as construções:** as janelas quebradas e tapadas (sem mexer nas que a casa já tinha tapado), a hera (só até 70 m do jogador, sem sombra) e as pichações, em todas as casas, lojas e prédios;
    - **as ruas:** o mato nas frestas (na sarjeta, na beira da calçada e nas rachaduras; vai com a grama, só perto) e o lixo na calçada e na sarjeta (os tijolos soltos, as caixas de papelão e os sacos de lixo).
    - **O desempenho** (o benchmark do centro de Cruz Alta, Grande 12345, na mesma sessão, com e sem o abandono): o quadro igual (3,9 a 4,1 ms), a GPU +0,1 a 0,2 ms (3,0 contra 2,9), +14 draw calls (541 contra 527) e +370 mil triângulos (2,45 contra 2,08 milhões; quase tudo a hera), e o pior quadro também igual (30 a 35 ms). Dois consertos no caminho: o shader ganhou a variante das instâncias DOTS (sem ela, o GPU Resident Drawer não juntava os objetos: eram 773 draw calls), e a malha da hera de cada bloco (até 30 mil folhas no centro, 2 a 4 ms para virar malha) sai depois da das casas, um bloco por quadro e só perto (no mesmo quadro, o pior chegava a 78 ms).
    - **Conferido:** os testes das casas, dos prédios, das lojas, das cidades, dos móveis e da floresta passam (com um novo: o mato das frestas só nas ruas e nas calçadas). As fotos em `Imgs/Renders/Mundo_M9c_Bairro.png` (em volta da casa da família) e `Mundo_M9c_Centro.png` (o centro de Cruz Alta).
- **M9d: a grama 3D** (o que já estava na M9; a grama baixa com o vento já entrou na M9a, em tufos, a pedido): a grama dobrando com o jogador, perto dele; e os campos das fazendas (só o visual).
  - **O estado (27/10/2026): aprovada** ("testado e ok, pode seguir").
    - **A grama dobrando** (como o `Grass.shader` do Guardiões): no shader da vegetação, quem passa empurra a planta para longe e para baixo, mais em cima; o jogador (0,8 m em volta do pé, com um rastro de 0,9 s que volta aos poucos) e os carros (2,2 m), até 24 ao mesmo tempo. Cada espécie dobra de um tanto: a grama, o capim e o trigo inteiros, a taboa e as samambaias quase, o milho pela metade, os arbustos pouco; as árvores, os tocos, as pedras e os fardos, nada. Tudo no shader (nada vai pela rede: cada máquina dobra pelos jogadores e carros que vê).
    - **Os campos das fazendas:** no giro de cada fazenda, as faixas de 13 m do mapa pintado: o milharal velho (touceiras de milho seco em fileiras de 1 m, uma a cada 1,3 m, 30% faltando e umas tombadas, com as espigas que ninguém colheu) e o trigal tomado (o trigo que nasceu sozinho, sem fileira, mais ralo em uns trechos, acamado em outros e com o capim no meio); os fardos redondos de feno esquecidos em fila, umas mudas e o capim; o chão de terra entre as fileiras do milho. A beira da lavoura tem o capim e a mata chega até ela; o pátio da casa fica vazio, para as construções especiais.
    - **As plantas novas** (no `cidade_arvores.py`, no fim da lista, com 3 LODs cada; 7 cores novas na linha 14 da paleta): `Milho_A` e `Milho_B` (o tombado), 270 a 330 triângulos de perto e 50 de longe; `Trigo_A` e `Trigo_B` (o acamado), 234 e 72; `Fardo`, 136 e 20, com a colisão (o resto da lavoura não tem). O milho vai até 90 m (com sombra só de perto) e o trigo até 70 m, encolhendo no fim.
    - **O peso** (Grande 12345, a Fazenda Jatobá, no Editor): na beira da lavoura olhando para dentro, 5,8 ms de média (pior 8,5 ms), GPU 2,8 ms, 415 draw calls e 2,16 milhões de triângulos (7 mil plantas na vista); na mata a 300 m dali, 5,1 ms (pior 7,8), GPU 2,7 ms, 387 draw calls e 1,79 milhão (3,8 mil plantas). Um bloco de lavoura sai em 4,9 ms em outra thread (o da mata, 1 ms). No Médio 7: 14 fazendas, 73 fardos e a menor lavoura com 3.557 plantas de um tipo.
    - **Conferido:** os testes da floresta passam (com um novo: o milho e o trigo cada um na sua faixa, os fardos e nada no pátio). As fotos em `Imgs/Renders/Arvores_Lavouras*.png` (as plantas, no Blender), `Mundo_M9d_Lavoura.png` (a beira da lavoura, no jogo) e `Mundo_M9d_Dobra.png` / `_SemDobra.png` (o trigo em volta do jogador, com e sem a dobra).
- **Pronto quando** (a M9 inteira): as matas entre as cidades têm o clima certo, as cidades parecem abandonadas há anos, sem cair o FPS.

### Parte M10: o jogo no mundo novo

- Nascer no bunker da família, os carros, os itens, os pontos de horda, o salvar do mundo (a semente e o que mudou) e o Continuar.
- O desempenho final com 4 jogadores em pontas opostas do mapa.
- **Pronto quando:** dá para jogar vários dias no mundo gerado, sair e voltar com tudo igual, com os amigos.

## Para depois (reordenado a pedido em 27/10/2026)

O usuário pediu a M9 agora e deixou estas para depois, nesta ordem por enquanto. A M10 usa partes delas (o bunker da família é do subsolo; os itens e as portas, da M7): quando chegar nela, ver o que precisa vir antes.

### O mapa fixo (uma ideia do usuário, 27/10/2026, para outra fase)

- **O plano dele, de 15 km, está em `Plano_Mapa_Fixo.md`** (29/10/2026, esperando aprovação).

- **O pedido:** fixar o mapa para construir a história em cima dele ("fica mais fácil de controlar a história"), e aproveitar para deixar o jogo mais leve.
- **A conversa:** só a semente fixa não segura o mapa (qualquer mudança no gerador mexe em tudo). O caminho: usar o gerador como a ferramenta de desenhar o mapa, escolher a semente e o tamanho e congelar o plano (as cidades, as estradas, os lotes, os nomes e os lugares) num arquivo, com as edições da história por cima; as malhas continuam montadas na hora, perto dos jogadores (assar todas daria gigabytes de malha no disco e na memória).
- **O que dá para ganhar:** o NavMesh assado por bloco (montá-lo durante o jogo foi o que travava na M6), as cidades de longe simplificadas (uma malha por bloco), o acerto à mão dos pontos pesados (a densidade do centro, as linhas de visão) e um mapa menor e cuidado; a geração do plano (2 a 5 s) sai do carregamento. Os nomes americanos das cidades e das placas entram junto (escritos à mão).

### A quarta fatia da M6: as construções especiais

- As fazendas, as cabanas, a casa do lago, a base militar, as comunidades e o laboratório por cima (veja "As construções", acima).
- **Pronto quando:** como nas outras fatias, andar por dentro de todas as variações sem cair, sem prender e sem buraco, e o zumbi chega em todos os cômodos.

### A mobília: os móveis nos pontos e o saque

- Os móveis (do Blender) nos pontos dos móveis da M6 (`MoveisDaObra`), com a colisão e o NavMesh em volta, e o saque nos que guardam saque (a Parte 7 da demo, no mundo novo).
- **Pronto quando:** as casas, as lojas e os prédios têm os móveis e o saque, sem prender o jogador nem o zumbi, sem cair o FPS.
- **Começou depois da M9d** (a pedido em 27/10/2026), em duas fatias, cada uma aprovada antes da próxima:
  1. **Os móveis:** um modelo para cada um dos 41 tipos do catálogo dos pontos (no Blender, `cidade_mobilia.py`, `Cidade_Mobilia.fbx`; os da demo, do `cidade_moveis.py`, servem de base), do tamanho do ponto, com a frente para a frente dele e as costas na parede, no estilo dos móveis da demo e abandonados (a madeira velha, as gavetas abertas, as prateleiras quase vazias, umas coisas caídas, a ferrugem e o sangue), com variações (as cores dos sofás e dos cobertores, o guarda-roupa grande e o pequeno, as prateleiras da garagem, da loja e da rouparia, o balcão com a caixa registradora nas lojas). No jogo, os móveis entram nas casas, nas lojas, nos andares dos prédios e na loja do posto, montados em outra thread com o resto: o desenho dos de cada bloco numa malha só, que aparece só perto do jogador (como a hera; nos prédios, no desenho do andar, que já carrega só perto), e uma caixa de colisão por móvel na colisão da construção (o NavMesh desvia delas).
  2. **O saque:** mirando um móvel que guarda saque, E abre a tela dele (como a do capô dos carros): o que tem dentro, sorteado pelo tipo do móvel e do lugar (a comida e a água na cozinha, a munição e as armas na loja de armas, as mochilas nos guarda-roupas...), para levar para a mochila. O que foi tirado fica tirado para todos (o anfitrião guarda; salvar no disco é da M10).
- **O estado (27/10/2026): a primeira fatia, os móveis, aprovada pelo usuário ("Testado").** O saque (a segunda fatia) fica para depois de uma pausa, a pedido. No Blender, as 52 malhas (os 41 tipos com as variações) no `Cidade_Mobilia.fbx`; as fotos em `Imgs/Renders/Mobilia_*.png`. No jogo, os móveis em todas as casas, lojas, andares de prédio e na loja do posto, com uma caixa de colisão por móvel (15 cm menor de cada lado, para o NavMesh passar), e a malha dos móveis de cada bloco só até 60 m do jogador.
  - **A passagem:** com os móveis de verdade, as conferências do F1 acharam cômodos fechados para o zumbi. Os pontos agora deixam a passagem com mais folga, desviam das folhas das portas abertas e dos balcões das lojas, não cortam o chão livre do cômodo e não deixam cantos onde o NavMesh tem chão sem caminho; entre a sala e a cozinha, a porta abre para o cômodo maior.
  - **A conferência rápida** (a pedido: as do F1, andando casa a casa e andar por andar, demoravam demais): um teste do Editor (`PassagemTestes`) monta cada casa, loja e andar de prédio do Médio 7 (2.756 construções, 30.159 cômodos) com um NavMesh só dele, sem e com os móveis, e confere se cada pedaço de chão tem caminho da rua, em uns 4 minutos. A primeira rodada achou 225 cômodos fechados pelos móveis; hoje, nenhum. As conferências do F1 continuam para o jogador andando, as escadas dos prédios e o mundo carregando.
  - **A cena das variações** (29/10/2026, a pedido, para rever o layout e a mobília de cada uma antes do saque): `Scenes/Exemplos/Variacoes.unity` do projeto Unity, uma construção de cada variação do mapa (as casas, as lojas e os prédios), com os móveis, a ficha de cada uma, as setas para pular de uma para a outra e a planta de cada piso (P). As plantas numa folha por grupo em `Imgs/Renders/Variacoes_*.png`.
  - **Conferido:** os 81 testes do Editor passam. O desempenho (Grande 12345): no centro de Cruz Alta, o quadro igual (4,0 ms) com e sem os móveis; a pé pela vizinhança da casa da família, média 4,9 ms com e sem eles (os picos raros aparecem nos dois). As fotos no jogo em `Imgs/Renders/Mundo_Mobilia_*.png` (a sala, a cozinha e o quarto de um sobrado, o mercado, a loja de armas e um apartamento).

### Parte M7: as portas e o arrombamento

- Os estados das portas, os jeitos de abrir, o barulho, a rede e o salvar.
- Os itens novos (pé de cabra, marreta, gazua, cartão), modelados no Blender.
- **Pronto quando:** dois jogadores arrombam portas de casas, apartamentos e lojas de todos os jeitos, os zumbis ouvem, e o estado volta igual depois de salvar.

### Parte M8: o subsolo

- O metrô (túneis, estações, saídas), os bunkers espalhados e os níveis de baixo do laboratório, na camada que só carrega com alguém embaixo.
- **Pronto quando:** dá para descer numa estação de uma cidade e sair noutra, e na superfície o subsolo não custa nada.

## Decisões

### Respondidas em 26/10/2026

1. **O tamanho é o lado do quadrado:** sim.
2. **Os tamanhos:** Pequeno, 3 km; Médio, 5 km; Grande, 8 km (em 1 km não cabia uma cidade grande de verdade).
3. **Os nomes e as placas:** brasileiros e em português, por enquanto.
4. **Onde a família começa:** no bunker, debaixo de uma casa numa zona residencial.
5. **A casa no lago:** sem ponte; depois entra o nado para chegar nela.
6. **Rio:** não. Só o lago.
7. **A "limpeza" por bairro:** saiu (o jogo mudou).
8. **As construções são feitas pelo jogo (C#),** depois do exemplo (veja "O exemplo", abaixo). Os objetos (carros, postes, placas, árvores) continuam vindo do Blender.

9. **A borda do mapa:** serras em volta, e a barreira da quarentena onde as rodovias saem do mapa (cercas, contêineres, blindados parados).
10. **Zumbis no mundo novo (por enquanto):** os zumbis de hoje, com o comportamento deles, mas nascendo espalhados, não amontoados em hordas (o mapa é grande). A conta fica em 250 zumbis num raio em volta do jogador: os que ficam longe somem e nascem outros perto, espalhados.

### Respondidas em 27/10/2026

11. **Anos depois do começo do apocalipse:** o mapa todo se passa anos depois. Não há mais energia: os postes ficam apagados e as janelas escuras (a iluminação da noite da M5 saiu). O visual abandonado (a textura nas construções, o mato nas paredes e nas ruas, as rachaduras) vem com a vegetação, na M9.

### As construções: Blender ou jogo

As casas de hoje já são feitas por código: o `cidade_casas.py` monta tudo (paredes, lambri, telhado, janelas) no Blender e grava o FBX. A pergunta é onde esse código roda.

- **Pelo Blender, com modelos fixos (como hoje):**
  - cada casa é um FBX pronto, com o seu FBX de colisão;
  - para a cidade não se repetir, seriam dezenas de modelos de cada tipo (casas de 1 e 2 andares, com e sem porão, prédios de 4 a 15 andares, as lojas);
  - mesmo assim, cada modelo tem um tamanho só e não se ajusta ao lote;
  - a planta fica em dois lugares (o script do Blender e a Unity, que precisa saber os cômodos, as portas e os pisos).
- **Pelo Blender, com um kit de peças:**
  - pedaços de parede com janela, com porta e de canto, de piso, de escada e de telhado, que o jogo encaixa numa grade;
  - a arte continua no Blender, mas tudo fica preso à grade (por exemplo, de 2 em 2 m), e o telhado de duas águas com larguras diferentes não encaixa bem em peças fixas.
- **Pelo jogo:**
  - o mesmo código das casas de hoje, passado para C#, roda na hora de gerar o mapa, com o tamanho do lote e a semente da casa;
  - o visual é o mesmo (as mesmas receitas de lambri, telha e moldura), e cada casa sai do tamanho que o lote pede;
  - a planta fica num lugar só.
  - Os detalhes que não dependem do tamanho (a folha da porta, o ar-condicionado, a luminária, a caixa de correio) podem continuar vindo do Blender, como peças que o jogo põe no lugar.

#### O exemplo (26/10/2026)

- **O que é:** o kit das casas (o `cidade_kit.py` e o `Obra` do `cidade_casas.py`) passado para C#, no projeto Unity (`Scripts/Mundo/Construcoes`), e uma rua gerada na hora pela semente.
- **A cena:** `Scenes/Exemplos/Exemplo_Casas.unity` (menu Zombie City Cleaner > Exemplos > Casas geradas pelo jogo).
  - Com Play, dá para andar nela com as medidas do jogador (WASD, Shift, V voa, R gera outra rua com uma semente nova).
- **O que tem:**
  - para comparar, no começo da rua:
    - a Casa_1 do Blender;
    - a mesma casa refeita pelo jogo com as medidas de hoje (fica quase igual);
    - a mesma com a escala nova;
  - 10 casas sorteadas, em dois moldes:
    - **térrea:** sala, cozinha, corredor, 2 quartos e banheiro;
    - **sobrado:** hall com escada, sala e cozinha; em cima, corredor, 2 quartos e banheiro.
  - Metade com porão: na térrea, a entrada de fora, à americana (o fosso com a tampa inclinada); no sobrado, a escada por baixo da escada.
  - Variam o tamanho, o lado (espelhada), o telhado (nos dois sentidos), as cores, a varanda (estreita ou larga), a chaminé, o ar-condicionado, as janelas (abertas ou com tábuas), os papéis e os pisos.
- **O que a planta faz sozinha:**
  - as paredes de dentro saem de onde dois cômodos se encostam;
  - as janelas são distribuídas por cômodo, desviando das portas;
  - os vãos das escadas, os guarda-corpos, as janelinhas do porão rente ao chão e os buracos no chão em volta.
- **Conferido:**
  - o piso de todos os cômodos;
  - o jogador anda da calçada até a sala, sobe e desce a escada do sobrado e desce ao porão pelas duas entradas.
  - Um acerto saiu do teste: o vão da escada começa mais cedo (o CharacterController sobe 30 cm antes de andar, e batia a cabeça na borda da laje).
- **Depois do olhar do usuário (26/10/2026):** aprovado o caminho "pelo jogo", com 5 acertos:
  - os pontinhos pretos piscando nos batentes: a ponta de cada friso do lambri ficava no mesmo plano da moldura. Agora o lambri sai em faixas contínuas, que acabam dentro da moldura;
  - saíram os cacos de vidro, as cortinas e as persianas das janelas (pareciam pedaços pretos) e os papéis pelo chão;
  - a parede de fora com duas cores por dentro: um pedaço passava de um cômodo para o outro. Agora ela é cortada onde muda o cômodo;
  - a porta da frente do sobrado abria em cima do pé da escada. Agora a dobradiça troca de lado quando a folha aberta fica na frente de uma escada. As portas de dentro não abrem mais para o corredor e abrem quase em ângulo reto;
  - por enquanto, toda porta da frente fica aberta (fechar, trancar e arrombar é a Parte M7).
  - Conferido: as 76 portas e passagens da rua passam nos dois sentidos, e nos 3 sobrados dá para entrar pela frente e subir a escada.
- **As garagens (26/10/2026, a pedido):**
  - toda casa tem a garagem anexa ou a vaga ao lado (metade cada), com o ponto onde o carro nasce e a entrada de carro até a rua; na rua de exemplo, o carro está lá em 70% das casas;
  - a garagem fica do lado do corredor na térrea e do lado da sala e da cozinha no sobrado (o outro lado é o da escada);
  - tem as paredes com lambri, o telhado de uma água embaixo do beiral da casa, a janela de lado às vezes, a lâmpada e a mancha de óleo no chão;
  - o portão de placas fica fechado ou recolhido no teto (metade cada; abrir e fechar é da Parte M7);
  - em 60% delas há uma porta para dentro da casa, que fica aberta: pela da térrea se chega ao corredor, pela do sobrado à cozinha. Com ela, a garagem é mais larga: os degraus da porta (a casa com porão fica a 0,75 m do chão) ficam do lado da casa, e o carro, encostado na parede de fora;
  - a chaminé e o ar-condicionado vão para o outro lado;
  - conferido (semente 1 da rua de exemplo): os 6 carros não encostam em nada; as 76 portas passam; das garagens abertas se entra pela rua e, pela porta, na casa; nas fechadas se chega por dentro da casa.
- **O peso:**
  - de 6 a 10 mil triângulos por casa (as da demo têm de 4 a 5 mil; a Casa_1 refeita tem 4,7 mil), porque elas são maiores, com mais cômodos, portas com folha e escadas;
  - para uma cidade grande, vão precisar do LOD de longe (a caixa com o telhado e as janelas escuras) e de juntar as fachadas por bloco, como está no plano.
- **As fotos:** `Imgs/Renders/Exemplo_Casas_*` (a comparação, a rua de cima e da calçada, cada casa de fora, a sala, o corredor, o hall com a escada, o andar de cima, o porão com lanterna, o fosso e a mesma sala nas duas escalas).

## Riscos

- **A Parte M6 é a maior de todas:** passar o kit de construção para C# e desenhar as plantas. Ela vai em 4 fatias de propósito.
- **O tempo de carregamento:** a meta é até ~20 s no mapa Grande. Só os dados são gerados no começo; as malhas, em volta dos jogadores.
- **O mapa de 8 km tem 2,6 vezes a área do Guardiões:** a grade de 2 m dá 16 milhões de pontos (uns 64 MB só nas alturas). Se pesar, a grade fica de 4 m longe das cidades e das estradas.
- **O NavMesh dos prédios altos:** montar os blocos com 15 andares pode pesar. Medido na M6 (segunda fatia): a área vai da rua à cobertura, mas por dentro só entram os andares carregados (perto de alguém), e a escada inteira é da casca. A coleta das fontes levou no máximo ~4,7 ms, e na conferência (o jogador pulando de prédio em prédio e os andares carregando o tempo todo) a área se refez uma vez a cada 2,5 a 3,5 s.
- **A oclusão sem assar:** se a da GPU não der conta, a saída é cortar mais cedo pela neblina e pelos interiores.
- **A mesma conta em todas as máquinas:** o checksum pega na hora se uma máquina gerar diferente.
