# Os moradores do bunker e o computador

O pedido (04/11/2026): "vamos começar a trabalhar no sistema de NPCs do bunker e no sistema de gerenciamento do bunker".
Em resumo:

1. Os NPCs moram no bunker. Por enquanto, só um, para testar: um velho, o pai dos personagens principais.
2. Eles têm uma rotina: dormem, acordam, andam pelo bunker, sentam, deitam, comem, fazem tarefas, gastam água, comida e
   cigarros e jogam sinuca.
3. O que eles usam sai do estoque do bunker. A lata de comida some, e o taco e as bolas saem do estoque e vão para a
   mesa e para o inventário deles. O cigarro também.
4. Cada um tem um inventário com o que pegou (o maço vai sendo fumado até acabar).
5. Os itens não somem sozinhos do estoque: o NPC vai buscar. Cada coisa tem o lugar dela pelo bunker (a comida na
   cozinha, os tacos e a caixa de bolas num suporte perto da sinuca). Se o estoque fica no B1, alguém leva para o
   lugar certo, se tiver espaço.
6. Eles têm funções: o cozinheiro (monta os pratos de todo dia) e o organizador (leva os itens do estoque para os
   cômodos).
7. O gerenciamento fica num PC do bunker: os membros, a comida em estoque e quantos dias ela dura, a lista do que está
   guardado e o mapa (depois, as missões que as comunidades pedem pelo rádio).
8. Dá para conversar com eles, e eles falam sozinhos e entre si, com o texto em cima da cabeça, como os sobreviventes.
9. Eles ficam tristes, neutros ou felizes, conforme a vida no bunker (a fome, o lazer, a fartura).
10. Eles podem ficar doentes e morrer, e a falta de comida e de água por muito tempo também mata.

O segundo pedido (no mesmo dia): **um sistema de personagens como o do RimWorld**. "Antes de criar o mundo você precisa
escolher quem vão ser os NPCs do bunker, você pode escolher 5": o jogo sorteia os 5 e dá mais algumas opções para
trocar. Cada um tem a personalidade, as habilidades, os laços de família e as deficiências, e tudo isso se vê e se
escolhe antes de começar. Durante o jogo, dá para trazer mais moradores.

## O que vem do RimWorld

- No RimWorld, antes de começar, a tela "Criar personagens" mostra os colonos do começo. O "Aleatório" sorteia um de
  novo, sem limite, e uma lista guarda os que ficam para trás, para trocar.
- Cada colono tem:
  - a história, em duas partes (a infância e a vida adulta);
  - de 1 a 3 traços;
  - 12 habilidades, de 0 a 20, cada uma com a paixão;
  - as incapacidades (o que ele não faz);
  - a saúde (as condições e as partes do corpo);
  - as relações com os outros (a família e a opinião de um sobre o outro).
- O humor é a soma de "pensamentos" com prazo. Muito triste, o colono entra em crise.
- Os colonos novos chegam pedindo para entrar, ou são resgatados, ou são presos e convencidos.
- **O que entra aqui:** tudo isso, um pouco menor:
  - 8 habilidades (sem os animais, a mineração, a arte e a pesquisa);
  - os traços e as condições que mexem no que o bunker já tem;
  - as crises leves e o ir embora (sem o "berserk" e sem os ataques);
  - o pedido de abrigo no portão e o resgate.

  Os presos, os romances novos e as partes do corpo ficam para depois.

## Como é hoje

- **O bunker** (o `Plano_Bunker.md`) tem 5 andares no mundo sorteado:
  - **B0:** a garagem e as operações, com a mesa de reunião, o rádio, o mapa e **dois postos com computador** (a mesa,
    o computador dos anos 90 e a cadeira);
  - **B1:** a sala de estar (a TV, os sofás e a mesa de cartas), a sala de jogos (com a sinuca), a enfermaria (3
    camas), o isolamento (2 camas), o depósito (9 estantes) e os banheiros;
  - **B2:** a cozinha industrial (os fogões, as bancadas de inox e o balcão da janela), o refeitório (8 mesas e a
    bancada da água), a despensa (28 estantes) e a câmara fria;
  - **B3:** os 6 quartos (os 1 a 4 para os jogadores, **os 5 e 6 de casal**), o dormitório de 18 camas (9 beliches), a
    lavanderia, a rouparia, o depósito (5 estantes) e os banheiros. **São 22 camas para os moradores**;
  - **B4:** a manutenção, que é restrita (os NPCs não entram: a decisão 8 do bunker).
- **As estantes guardam itens** pela rede e vão no save (as do B1 e as do B3 recebem as compras do Dia 0, em grupos).
- **Os andares só são montados perto de alguém** (o andar de cada um e os vizinhos). O NavMesh de cada andar é refeito
  quando ele entra.
- **O menu do mundo novo** ("Novo mundo") pede o nome, o tamanho, a semente e o modo de salvar, e então "Criar o mundo".
  **O personagem de cada jogador é dele** e vai de um mundo para o outro (o painel do personagem). A tela de criar o
  personagem (com o personagem girando no meio) aparece para um personagem novo, ao entrar.
- **Os sobreviventes e os acampamentos:** o corpo do principal vestido, o NavMesh, a rede e as falas em cima da cabeça
  (letra a letra, com um bipe no tom de cada um). Os grupos de busca são hostis; os acampamentos têm os guardas, os
  comerciantes e os moradores deles. Os zumbis atacam os NPCs (eles são alvos, como os jogadores).
- **As roupas:** os NPCs prontos e o sorteio por tipo (o sobrevivente, o civil, o trabalhador, o policial, o militar, o
  médico, o fazendeiro, o idoso e o estudante, com as peças e as cores que combinam). O "Idoso" das referências (o
  cardigã e a barba curta grisalha) já existe.
- **As animações do corpo:** parado, andar, correr, agachar e pular. Também sentado no carro (motorista e passageiro),
  segurar e usar a lata e a garrafa, o fumo (acender, tragar e o peteleco) e o taco em pé. **Não há:** sentar numa
  cadeira ou num sofá, deitar e dormir, comer de prato, cozinhar, pegar numa estante, carregar e a tacada da sinuca. O
  Dia 0 tem a pose deitada (feita no código) e os gestos de tossir e de conversar.
- **A fome e a sede do jogador:** de cheia a vazia em 40 minutos (a fome) e 25 (a sede). A lata enche 40, e a garrafa
  45. Abaixo de 25, a vida para de voltar. Vazias, elas tiram vida.
- **A sinuca:** a caixa de bolas (com ela, as bolas vão para a mesa e a caixa fica vazia) e o taco na mão. A física e a
  rede já funcionam, com um jogador por mesa.
- **O fumo:** o maço é uma pilha de 20 cigarros, e o corpo tem os clipes de acender, tragar e o peteleco.
- **O relógio:** um dia do jogo dura 24 minutos reais, então **1 hora do jogo dura 1 minuto**.
- **Os itens que se gastam:** a comida (16 a 24), a bebida (17 e 25 a 31) e os remédios (80 a 88: o remédio, os
  comprimidos, o kit médico, a atadura, a gaze, o desinfetante, o spray, o antibiótico e a bandagem).

## As decisões (com a recomendação)

Cada decisão traz a recomendação e, entre parênteses, a outra opção. Dá para aprovar todas pela recomendação ou mudar
só algumas. As 8 primeiras são o sistema de personagens (o segundo pedido); as outras 12 são a vida no bunker.

### Os personagens

1. **Os moradores do começo.** O anfitrião escolhe **de 1 a 5 moradores** numa tela nova, depois do "Novo mundo" e
   antes do "Criar o mundo" (a decisão 7). Eles são os que passaram os anos do apocalipse no bunker com a família.
   - **O primeiro é sempre um parente da família:** o pai, a mãe, o avô, a avó, o tio ou a tia dos personagens, de 55
     a 80 anos. Ele se sorteia como os outros, mas continua parente, e chama os jogadores de "filho" e "filha" (pelo
     corpo de cada um) ou pelo nome. É o velho do primeiro pedido.
   - Os outros 4 são sorteados livres e podem ter laços entre eles (a decisão 6).
   - Quem entra depois, num mundo já criado, encontra os moradores que já estão lá.

   *(As outras: os 5 sorteados livres, sem o parente; ou sempre 5, sem escolher quantos.)*

2. **A ficha.** Cada morador tem uma ficha, sorteada uma vez e guardada no mundo:
   - **o nome**, em inglês (o jogo se passa nos Estados Unidos): o nome, às vezes um apelido, e o sobrenome. Os
     parentes têm o mesmo sobrenome, e os velhos têm nomes de velho (Walter, Dorothy);
   - **a idade** (de 16 a 80) e **o sexo**;
   - **a aparência:** o corpo, a pele, o cabelo, a barba e a roupa, pelo sorteio das roupas. O tipo de roupa vem da
     história; acima de 55 anos o cabelo fica grisalho, e acima de 65 a roupa é a do idoso;
   - **a história, em duas partes**, como no RimWorld: **"Antes"** (o que ele fazia antes do apocalipse) e **"Nos anos
     do apocalipse"** (como ele sobreviveu e como chegou ao bunker). Cada parte dá pontos de habilidade, às vezes uma
     paixão, às vezes um "não faz" e às vezes puxa um traço. Na ficha, cada parte vira uma frase ("Earl dirigiu
     caminhão por 30 anos. Nos primeiros anos, se escondeu num posto de estrada, até ver a fumaça do bunker.");
   - **as habilidades** (a decisão 3), **os traços** (a 4), **as condições** (a 5) e **os laços** (a 6);
   - **o que ele não faz** (as incapacidades do RimWorld), que vem da história e dos traços: "Não cozinha", "Nada de
     trabalho pesado", "Não cuida de doentes" e "Não luta". Uma função que ele não faz não aparece para ele.

   Umas das histórias (a lista inteira, de umas 24 de "Antes" e 12 "Nos anos do apocalipse", fica para a parte 2):

   | Antes | Dá | A roupa |
   |---|---|---|
   | Cozinheiro de lanchonete | Cozinha +6 | civil |
   | Enfermeira | Medicina +6, Social +2 | médico |
   | Médico do hospital | Medicina +8; nada de trabalho pesado | médico |
   | Mecânico de oficina | Mecânica +7, Construção +2 | trabalhador |
   | Pedreiro | Construção +7 | trabalhador |
   | Fazendeiro | Plantio +7, Ofício +2 | fazendeiro |
   | Policial | Combate +6, Social +2 | policial |
   | Soldado | Combate +8 | militar |
   | Professora | Social +5, Medicina +2 | civil |
   | Contador | Social +2; nada de trabalho pesado | civil |
   | Caminhoneiro | Mecânica +4, Combate +2 | trabalhador |
   | Pastor | Social +6; puxa o Religioso | civil |
   | Estudante | +2 em três, sorteadas | estudante |
   | Aposentado (só os velhos) | +3 na do ofício de antes | idoso |

   | Nos anos do apocalipse | Dá |
   |---|---|
   | Entrou no bunker com a família no Dia 0 (o parente, sempre) | Cozinha +2, Social +2 |
   | Sobreviveu sozinho no mato | Combate +3, Plantio +3; puxa o Calado |
   | Veio de um acampamento que caiu | Construção +3, Social +2 |
   | Saqueou as cidades | Combate +3, Social −2 |
   | Cuidou dos feridos de um abrigo | Medicina +4 |
   | Catou ferro-velho | Mecânica +3, Ofício +2 |
   | Ficou escondido num porão | nada; puxa o Medroso |
   | Caçou zumbis | Combate +4; puxa o Sangue frio |

   *(A outra: a história numa parte só, a de "Antes".)*

3. **As habilidades.** **Oito, de 0 a 20**, como no RimWorld (que tem 12).
   - **A paixão:** cada uma pode ter nenhuma, ser "interessado" (uma chama) ou "apaixonado" (duas). Com a paixão, ele
     aprende mais rápido (×0,35, ×1 e ×1,5) e fica feliz fazendo aquilo (+4 e +8, enquanto faz).
   - **Ela cresce com o uso**, devagar, e não cai: um cozinheiro de 8 que cozinha todo dia chega a 12 em uns 10 dias do
     jogo.
   - **O valor do começo** vem da idade, da história e de um pouco de sorte.

   | Habilidade | O que muda agora | Depois |
   |---|---|---|
   | Cozinha | a rapidez do cozinheiro e a chance de o prato dar intoxicação (5% com 0, nenhuma de 12 para cima). De 12 para cima, ele faz o "prato caprichado" (+12 de humor, em vez de +8) | |
   | Medicina | a rapidez do médico e a chance de o grave sarar com o antibiótico (60% com 0, 95% com 15) | as cirurgias |
   | Social | as conversas dele dão mais humor, ele acalma quem está em crise e convence quem pede abrigo | o comércio com os acampamentos |
   | Combate | ainda nada | defender o bunker e as missões |
   | Mecânica | ainda nada | o gerador e os carros |
   | Construção | ainda nada | os consertos e a base crescendo |
   | Plantio | ainda nada | a horta |
   | Ofício | ainda nada | os kits de montar e a munição |

   As que ainda não valem aparecem na ficha (dão o jeito de cada um) e passam a crescer quando o sistema delas entrar.
   *(As outras: só as 3 que valem agora; ou as habilidades sem crescer.)*

4. **Os traços** (a personalidade). **De 1 a 3 por morador.** Os dois de um par não vêm juntos.

   | Traço | O que faz |
   |---|---|
   | Fumante | tem a vontade de fumar e fica triste sem cigarro (uns 40% dos sorteados) |
   | Trabalhador / Preguiçoso | a função 20% mais rápida / 20% mais devagar, e ele para mais para descansar |
   | Alegre / Pessimista | +10 / −10 no humor, sempre |
   | Gentil / Grosso | a conversa dele dá +3 a quem ouve e ele nunca insulta / ele insulta às vezes, e os outros gostam menos dele (−10 na opinião) |
   | Corajoso / Medroso | com um zumbi no bunker, ele fica perto dos outros, sem gritar / foge antes de todos, e o portão aberto deixa ele triste (−6) |
   | Guloso / Simples | come 50% mais e fica feliz com a fartura (+4) / os motivos de dormir mal e da comida fria valem a metade |
   | Notívago | a agenda virada (dorme das 08:00 às 16:00) e +4 de noite |
   | Saúde de ferro / Frágil | adoece a metade / o dobro |
   | Durão | a vida desce a metade com fome, com sede e no grave |
   | Sangue frio / Sensível | o luto vale 30% / 150% |
   | Jogador | o lazer favorito é a sinuca, que dá o dobro (+16) |
   | Religioso | reza de manhã e à noite (um lazer que não gasta nada, +6), e o luto passa mais rápido |
   | Arrumado | fica triste com itens no chão do bunker (−4, ou −8 com mais de 5) e, como organizador, trabalha 25% mais rápido |
   | Falador / Calado | puxa conversa o dobro e, sem conversar um dia, fica triste (−6) / conversa pouco e não sente falta |
   | Rápido / Lento | anda 20% mais rápido / 20% mais devagar |
   | Acumulador | a fartura e a escassez valem o dobro |
   | Mão leve | às vezes guarda um item do estoque nas vagas dele (um chocolate, um maço) e não devolve. O registro do computador avisa ("Sumiu um maço da mesinha do fumo.") |

   *(A outra: só os traços que mexem no humor, sem os de rapidez e de saúde.)*

5. **As condições** (as deficiências e os problemas de saúde). **De 0 a 2 por morador**, mais nos velhos:

   | Idade | Com uma condição |
   |---|---|
   | até 30 anos | 1 em cada 10 |
   | de 31 a 55 | 1 em cada 4 |
   | de 56 a 70 | a metade |
   | acima de 70 | 3 em cada 4, às vezes duas |

   O parente velho quase sempre tem uma.

   | Condição | O que faz |
   |---|---|
   | Coluna ruim | 4 vagas em vez de 6, o trabalho pesado 25% mais devagar, e ele deita no sofá para cochilar |
   | Joelho ruim | anda 25% mais devagar, mancando (uma animação nova), e sobe a escada devagar |
   | Vista fraca (a catarata, nos velhos) | o trabalho de perto 15% mais devagar, e a tacada da sinuca sai mais torta |
   | Surdez | não ouve o portão nem os gritos (foge depois dos outros), e às vezes responde "Hã?" |
   | Asma | o sono desce mais rápido e a gripe vira grave o dobro; fumando, ele tosse |
   | Diabetes | **um comprimido por dia**, que sai do estoque (os remédios); sem ele, a vida desce e ele fica fraco |
   | Coração fraco | nada de trabalho pesado (não é organizador); no grave, a chance de morrer sobe |
   | Insônia | dorme 2 horas a menos, e uma noite em cada três ele dorme mal |

   A perna ou a mão faltando fica para depois (precisa de modelos novos). *(A outra: as condições só nos velhos.)*

6. **Os laços e a opinião.**
   - **Os laços** são sorteados com o grupo:
     - os tipos: o casal, o pai ou a mãe e o filho, os irmãos, o avô e o neto, os primos, os amigos de antes e o
       ex-casal;
     - num grupo de 5, uns dois laços em média (às vezes nenhum);
     - as idades combinam (o pai é de 18 a 45 anos mais velho), e os parentes têm o sobrenome do outro e a pele e o
       cabelo parecidos;
     - **o parente da família** tem o laço com os jogadores (é o pai deles) e pode ter outro com um dos 4 (a "tia
       Martha" é irmã dele).
   - **A opinião** de cada um sobre cada um vai de −100 a 100, como no RimWorld:
     - o laço dá a base: o casal +40, a família +25, os amigos +20 e o ex −20;
     - os traços mudam: o gentil +10, o grosso −10 e os dois fumantes +5;
     - as lembranças com prazo também mudam, como os motivos do humor: "Conversamos" +3 por 3 dias, "Ele me insultou"
       −8 por 5 dias, "Jogamos sinuca" +4 por 2 dias, "Cozinhou um prato caprichado" +2;
     - de 40 para cima, eles são amigos; de −40 para baixo, rivais (eles se evitam e discutem).
   - **O que muda com os laços:**
     - o casal dorme junto num quarto de casal e fica feliz junto (+5, enquanto os dois estão bem);
     - a morte de um parente pesa mais (−45 por 10 dias; a do outro do casal, −60);
     - os amigos conversam mais;
     - o rival perto piora o humor (−3).

   *(A outra: só os laços, sem a opinião.)*

7. **A tela dos moradores.** *(Trocada pela tela das cartas em 04/11/2026, a pedido: veja "A tela das cartas", no fim.)*
   Depois do "Novo mundo" (o nome, o tamanho, a semente e o salvar), o anfitrião aperta
   "Escolher os moradores" e chega na tela, como a do RimWorld:
   - **à esquerda, os do bunker** (de 1 a 5: o número muda ali) e, embaixo, **a reserva**: mais 3 sorteados, que não vão;
   - **no meio, o personagem girando**, como na tela de criar o personagem, e **à direita, a ficha** do escolhido;
   - **"Sortear de novo"**, sem limite, como no RimWorld, refaz o escolhido. Ao sortear um de novo, os laços dele somem
     (o novo pode ganhar outro);
   - **"Trocar com a reserva"** passa um da reserva para o lugar dele (o parente não se troca: ele só se sorteia de
     novo);
   - **o nome** se muda clicando nele;
   - **"Sortear todos"** refaz os 5 e a reserva juntos, com os laços;
   - **"Criar o mundo"** segue para o mundo, com eles no bunker. Um mundo salvo antes disso abre a tela na primeira vez
     que for carregado.

   ```
   OS MORADORES DO BUNKER                                       [Voltar]  [Criar o mundo]

   NO BUNKER   ‹ 5 ›                             WALTER "WALT" HAYES
   ▸ Walter Hayes, 72 · o pai da família         72 anos · homem · o pai da família
     Martha Hayes, 64 · irmã do Walter
     Rosa Medina, 29                             ANTES: mecânico de oficina
     Earl Pruitt, 55                             NOS ANOS DO APOCALIPSE: entrou no bunker
     Danny Cole, 17                                com a família, no Dia 0
                               (o Walter
   A RESERVA                    girando)         HABILIDADES
     Lucy Tran, 23                               Combate     ███░░░░░░░   6
     Hank Doyle, 60                              Cozinha     █░░░░░░░░░   2
     Grace Ellis, 34                             Medicina    ██░░░░░░░░   4
                                                 Social      ████░░░░░░   8  ♦
                                                 Mecânica    ███████░░░  14  ♦♦
                                                 Construção  █████░░░░░   9
                                                 Plantio     ██░░░░░░░░   3
                                                 Ofício      ████░░░░░░   7  ♦

                                                 TRAÇOS      Fumante · Jogador
                                                 CONDIÇÕES   Joelho ruim · Vista fraca
                                                 NÃO FAZ     —
                                                 LAÇOS       o pai dos jogadores
                                                             irmão da Martha (opinião +31)

   [Sortear de novo]  [Trocar com a reserva]  [Mudar o nome]              [Sortear todos]
   ```

   ♦ é o interessado e ♦♦ o apaixonado (no jogo, as chamas). *(As outras: um número de sorteios por morador (3, por
   exemplo), para cada escolha pesar; ou sem a reserva, só o sortear.)*

8. **Trazer mais moradores.** O limite é o das camas: **22** (os 4 lugares dos quartos 5 e 6 e as 18 camas do
   dormitório). *(Com a tela das cartas, quem chega é um dos NPCs do mapa, e não um sorteado na hora.)* Agora, de dois
   jeitos:
   - **O pedido de abrigo no portão:**
     - de 3 a 6 dias (sorteado), com um jogador no bunker e cama sobrando, o rádio das operações chama ("Tem alguém
       aí? Estou vendo o portão do morro..."), com o aviso na tela e no registro do computador;
     - uma hora do jogo depois, alguém sobe a estrada de terra e espera no portão;
     - no E, "Quem é você?" abre a ficha dele (sorteada na hora, sem o laço com a família), e o jogador escolhe "Pode
       entrar" ou "Vá embora";
     - sem resposta em 1 dia, ele vai embora.
   - **O sobrevivente perdido:**
     - uns a cada 5 dias, o rádio pega o pedido de socorro de alguém preso numa casa de uma cidade, que aparece no M e
       no MAPA do computador;
     - lá, ele está trancado num cômodo, com os zumbis em volta. No E, "Venha comigo" (a ficha dele aparece antes);
     - ele **segue o jogador** a pé (pelo NavMesh em volta dos jogadores) e **entra no carro** como passageiro (a
       animação já existe). No pátio do bunker, ele vira morador;
     - se ele morrer no caminho, o pedido acaba;
     - com isso, a aba RÁDIO do computador ganha a primeira coisa de verdade.
   - **Ir embora:** um morador que passa 3 dias muito triste avisa e vai embora (a decisão 16).

   *(Ficam para depois: convidar um morador de um acampamento amigo, os presos (os do grupo de busca que se rendem,
   como no RimWorld) e os filhos que nascem.)*

### A vida no bunker

9. **O tempo do bunker.** O dia dos moradores segue o relógio do jogo. **O bunker continua vivo mesmo com todos
   longe**: o anfitrião segue a vida deles "no papel" (sem o corpo, só o relógio). Eles comem, dormem e gastam o
   estoque. Quando o andar onde um deles está é montado (alguém chegou perto), ele aparece onde a agenda manda
   (sentado na sala, dormindo na cama). Com o jogo fechado, o tempo para. *(A outra: os moradores só vivem com um
   jogador no bunker. É mais simples, mas a comida não acaba enquanto se explora, e "quantos dias a comida dura" perde
   o sentido.)*

10. **Como cada um decide o que fazer.** Cada morador segue **uma agenda do dia** (dormir, as refeições, o trabalho e
    o lazer), e **as necessidades passam na frente**. Com muita fome, ele come fora de hora; muito cansado, cochila;
    doente, vai para a enfermaria. A agenda de todos:

    | Hora | O que ele faz |
    |---|---|
    | 22:00 a 06:30 | dorme na cama dele |
    | 06:30 | levanta e vai ao banheiro (B3) |
    | 07:00 | o café no refeitório (B2): bebe água e come |
    | 08:00 a 12:00 | a função dele (ou os afazeres) |
    | 12:00 | o almoço |
    | 13:00 a 19:00 | a função e o lazer |
    | 19:00 | o jantar |
    | 20:00 a 22:00 | o lazer na sala comum (B1): a sinuca, a TV, a conversa |

    Ela muda com a ficha:
    - o notívago vira o dia;
    - os de mais de 60 anos cochilam 1 hora às 14:00, no sofá;
    - o religioso reza às 06:45 e às 21:30;
    - o fumante fuma a cada 3 horas, onde estiver (fora da cozinha e da enfermaria);
    - o insone dorme às 00:00.

    **As camas:** os casais ficam com os quartos 5 e 6 (o parente da família, sem casal, fica com um deles), e os
    outros com o dormitório. A cama de cada um se muda no computador. *(As outras: só as necessidades, sempre a mais
    urgente primeiro, como no The Sims. Parece mais vivo, mas é menos previsível e mais difícil de acertar. Ou só a
    agenda, que fica rígida.)*

11. **As necessidades e o consumo.** Quatro barras de 0 a 100, como as do jogador:
    - **a fome e a sede descem na metade do ritmo das do jogador** (eles não correm pelo mapa). A fome esvazia em 80
      horas do jogo, e a sede em 50. A comida e a bebida enchem o mesmo. Ele come nas refeições quando a fome está
      abaixo de 60, ou a qualquer hora se ela cair abaixo de 25;
    - **o sono** (a energia): desce em 18 horas acordado e enche dormindo, mais rápido na cama dele que no sofá ou no
      chão;
    - **a vontade de fumar** (só nos fumantes): sobe com o tempo e, cheia, ele procura um cigarro. Sem maço, a
      abstinência deixa ele triste.

    **Na conta:**
    - um morador gasta por dia três quartos de uma lata (ou meia comida, com o cozinheiro) e uma garrafa de água;
    - **com 5 moradores, são umas 4 comidas e 5 garrafas por dia do jogo**, ou umas 10 comidas e 13 garrafas por hora
      de jogo. A água é o que mais pesa, até a água da chuva entrar;
    - para o começo não ser uma corrida, **o bunker começa com uma reserva de 7 dias para o grupo** (o que eles
      guardaram nesses anos), além das compras do Dia 0.

    *(As outras: o mesmo ritmo do jogador, o dobro do gasto; ou um gasto fixo por refeição, sem as barras, que é mais
    fácil de prever, mas todo dia fica igual.)*

12. **Onde os itens ficam: os lugares do bunker.** O estoque do bunker é **só o que está nas estantes e nos lugares**
    (o que está no chão não conta). Cada estante passa a ter **um tipo**, que vem do cômodo dela, e os lugares novos
    também. Os de uso têm **uma meta**, que é quanto o lugar quer ter:

    | Lugar | Onde | Guarda | Meta (no começo) |
    |---|---|---|---|
    | Depósito | as 9 estantes do B1 e as 5 do B3 | qualquer coisa | sem meta |
    | Despensa | as estantes da despensa (B2) | a comida | 12 |
    | Bancada da água | a do refeitório (B2), com a geladeira e os galões | a bebida | 6 |
    | Balcão dos pratos | o balcão de inox da janela da cozinha (B2) | os pratos prontos (só o cozinheiro põe) | sem meta |
    | Suporte dos tacos | **novo**, na parede da sala de jogos (B1) | os tacos e a caixa de bolas | 2 tacos e 1 caixa |
    | Mesinha do fumo | **nova**, a mesa de canto da sala de estar (B1), com o cinzeiro | os maços | 2 |
    | Armário da enfermaria | o armário de aço da enfermaria (B1), que passa a guardar itens | os remédios (e os comprimidos da diabetes) | 5 |
    | Arsenal | o painel de armas (B0), como hoje | as armas | sem meta |

    **O morador pega no lugar mais perto que tenha o que ele quer**, de qualquer tipo: com fome e a despensa vazia, ele
    desce até o depósito do B1. O lugar certo só encurta o caminho, e o organizador (a decisão 14) mantém ele cheio. Os
    jogadores pegam e guardam em qualquer lugar, como hoje. *(A outra: os moradores só pegam no lugar certo. Sem um
    organizador, a despensa vazia é fome, mesmo com o depósito cheio. É mais gerenciamento, e castiga mais.)*

13. **O inventário do morador.** **6 vagas** (4 com a coluna ruim), sem grade. Cada vaga leva um item ou uma pilha (o
    maço com os cigarros que sobram, as latas da viagem do organizador). O que ele vai usar sai do lugar, vai para as
    vagas e é gasto ali (o cigarro sai do maço, a lata é comida). **Dá para ver e mexer:** no E, "O que você carrega?"
    abre a tela do inventário com as vagas dele do lado (como a estante), e o jogador dá e pega. *(A outra: a grade dos
    bolsos do jogador, 4 × 2, com o tamanho de cada item. Combina mais com a mochila, mas a viagem do organizador
    levaria pouco.)*

14. **As funções.** Cada morador tem **uma**, escolhida no E ou no computador. A habilidade diz o quanto ele faz bem, e
    o que ele não faz não aparece:
    - **nenhuma:** ele cuida de si (busca a própria comida) e faz os afazeres;
    - **cozinheiro** (a Cozinha):
      - duas vezes por dia (às 10:00 e às 17:00), se faltam pratos frescos no balcão, ele pega comida na despensa (ou
        no depósito), cozinha no fogão do B2 (uns 30 minutos do jogo com a Cozinha 10, com a panela no fogo) e põe os
        pratos no balcão;
      - **cozinhar rende: cada 2 comidas viram 3 pratos.** O prato enche 40 de fome e 10 de sede, e comer um prato
        quente deixa feliz;
      - o prato estraga em 2 dias, e o estragado pode dar intoxicação;
      - ele faz um prato para cada morador e mais um por jogador: **os jogadores também comem** (o prato é um item);
      - o que é melhor cru (a barra de energia, o chocolate e o salgadinho) ele não cozinha: fica para o lanche;
    - **organizador** (nada de trabalho pesado o impede):
      - de 2 em 2 horas do jogo, ele olha as metas dos lugares e leva o que falta, do depósito até o lugar, até 6 vagas
        por viagem (com uma caixa nas mãos);
      - a ordem: a despensa e a água, depois os remédios, depois o fumo e a sinuca;
      - o que ele achar no chão do bunker, ele guarda no depósito;
    - **médico** (a Medicina): leva o remédio, a comida e a água a quem está doente e fica com o grave na enfermaria.
      Sem médico, os jogadores fazem isso pelo "O que você carrega?".

    Mais de um pode ter a mesma função (dois cozinheiros dividem as panelas). *(A outra: cada morador faz todas as
    funções, numa ordem de prioridade, como na aba de trabalho do RimWorld. Com muitos moradores fica mais flexível,
    mas é mais tela para ajustar.)*

15. **O lazer e os afazeres.** O lazer favorito de cada um vem dos traços e da história (o Jogador, a sinuca; o
    Religioso, rezar; o Calado, ler).
    - **A sinuca:**
      - ele pega um taco e a caixa de bolas no suporte (o taco vai para a mão, e a caixa para as vagas);
      - põe as bolas na mesa (a caixa fica vazia, como com o jogador) e **joga de verdade**: as bolas andam e caem pela
        física da mesa, e quem estiver perto vê;
      - sozinho, ele treina; com dois, um joga contra o outro (a vez de cada um). Se um jogador estiver na mesa, ele
        espera ou vai fazer outra coisa;
      - no fim, ele recolhe as bolas e devolve o taco e a caixa ao suporte.
    - **O cigarro:** ele pega um maço na mesinha do fumo (o maço vai para as vagas) e fuma com as animações do jogador
      (acender, as tragadas e o peteleco), uns 60 s por cigarro. Perto do cinzeiro, a bituca vai nele, e o cinzeiro vai
      enchendo (só de enfeite).
    - **Sem gastar nada:** ver a TV da sala (só chiado e fitas velhas), ler sentado, rezar, mexer no rádio das operações
      e conversar.
    - **Os afazeres** enchem o dia de quem está sem função: lavar a louça (B2), dobrar a roupa (a lavanderia do B3) e
      mexer na bancada do arsenal. Por enquanto, eles não mudam o estoque.

    *(A outra: só a sinuca e o cigarro agora, e o resto depois.)*

16. **A felicidade e as crises.** Um número de 0 a 100, que começa em 50. Ele é feito de **motivos com prazo** (os
    "pensamentos" do RimWorld), mais o fixo dos traços (o Alegre +10). Ele tem três faixas: **triste** (abaixo de 35),
    **neutro** e **feliz** (acima de 65). Os motivos do começo:

    | Motivo | Quanto | Quanto tempo vale |
    |---|---|---|
    | Comeu um prato quente (caprichado) | +8 (+12) | 1 dia |
    | Passou fome (abaixo de 25) | −15 | até comer, e mais 1 dia |
    | Passou sede (abaixo de 25) | −15 | até beber, e mais 1 dia |
    | Dormiu na cama dele | +5 | 1 dia |
    | Dormiu mal (no sofá, no chão, acordado) | −8 | 1 dia |
    | Fumou (fumante) | +6 | 6 horas |
    | Sem cigarro há mais de 1 dia (fumante) | −12 | até fumar |
    | Jogou sinuca ou se divertiu | +8 | 1 dia |
    | Nenhum lazer há 2 dias | −8 | até se divertir |
    | Fez o que gosta (a paixão) | +4 ou +8 | enquanto faz, e mais 2 horas |
    | Fartura (comida e água para mais de 7 dias) | +8 | enquanto durar |
    | Escassez (comida ou água para menos de 2 dias) | −12 | enquanto durar |
    | Conversou com alguém (com um amigo) | +4 (+6) | 1 dia |
    | Junto do casal | +5 | enquanto os dois estão bem |
    | Um jogador voltou depois de 2 dias fora | +10 | 1 dia |
    | Doente | −10 (grave: −20) | enquanto durar |
    | Alguém morreu (um parente; o casal) | −30 (−45; −60) | 5 dias (10) |

    **O que muda com o humor:** feliz, ele trabalha 20% mais rápido e fala bem. Triste, trabalha 30% mais devagar, fala
    reclamando e adoece mais. O computador mostra o número e os motivos.

    **As crises** (as do RimWorld, mais leves):
    - **abaixo de 15 por 2 horas do jogo, uma crise leve** (uma delas, sorteada): se tranca no quarto e chora por 4
      horas; come sem fome (3 porções de uma vez); anda sem rumo pelo bunker, falando sozinho; ou larga a função até o
      dia seguinte;
    - **abaixo de 5, uma crise forte:** discute aos gritos com o rival (a opinião dos dois cai) ou joga no chão o que
      está numa estante;
    - **3 dias seguidos abaixo de 20:** ele avisa ("Não aguento mais esse lugar.") e, no dia seguinte, vai embora pelo
      portão com uma mochila (comida e água para 2 dias, tiradas do estoque). Um jogador que conversa com ele ("Fique,
      por favor") adia a saída 1 dia e dá +10 por 1 dia;
    - um morador com a Social alta acalma quem está em crise (a crise acaba mais cedo).

    *(As outras: só as três faixas, sem os motivos à mostra; ou sem as crises.)*

17. **As falas e a conversa.**
    - **O texto em cima da cabeça**, como o dos sobreviventes: letra a letra, sem caixa e com o bipe no tom dele. As
      falas são em português e aparecem até uns 15 m.
    - De vez em quando, ele **fala sozinho** sobre o que está fazendo e sentindo ("Essa despensa tá ficando vazia...",
      ou "Só mais um e eu paro", ao fumar) e **cumprimenta** quem chega ("Voltaram! Trouxeram comida?"). O parente
      chama pelo "filho" e pela "filha".
    - **O E abre a conversa**, um menu curto:
      - "Como você está?": ele responde pelo humor e pelo motivo mais forte;
      - "Precisa de alguma coisa?": ele diz o que está faltando;
      - "Sua função": as que ele faz;
      - "O que você carrega?": abre a tela das vagas dele;
      - "Ver a ficha": a ficha, como na tela dos moradores;
      - "Conte uma história": as lembranças da história dele, uma fala longa em partes.
    - **Entre eles:** quando dois estão perto e sem fazer nada, um diálogo curto de 2 a 4 falas por assunto (a comida, o
      passado, os zumbis, piadas, reclamações). Os amigos conversam mais, e o grosso às vezes insulta (a decisão 6).

    *(A outra: só as falas soltas e o cumprimento, sem o menu; a função seria escolhida só no computador.)*

18. **A doença e a morte.**
    - **A gripe:** a tosse (o gesto do Dia 0) e o cansaço mais rápido. Passa sozinha em 4 dias, ou em 1 com
      comprimidos ou remédio. Num velho sem remédio, uma em cada quatro vira grave.
    - **A intoxicação:** vem do prato estragado (ou do cozinheiro fraco), e a sede desce no dobro do ritmo. Passa em 2
      dias, ou em 1 com remédio. Sem remédio, uma em cada cinco vira grave.
    - **O grave:** ele fica deitado numa cama da enfermaria e não levanta mais sozinho. O médico (ou um jogador) leva a
      comida, a água e o **antibiótico**. A vida dele vai descendo. Com o antibiótico, ele sara em 2 dias (pela chance
      da Medicina); sem, morre em uns 4.
    - **A chance de adoecer**, por dia: 1%, e no velho 2%. Somam-se:
      - 10% num dia com fome ou sede abaixo de 25;
      - 5% se ele dormiu mal;
      - 3% se está triste.

      O Frágil e a Asma dobram, e a Saúde de ferro divide por dois.
    - **A morte por fome ou sede:** com a fome a zero, a vida acaba em 2 dias; com a sede a zero, em 1 (o Durão aguenta
      o dobro).
    - **Depois da morte:**
      - o corpo cai (como o dos sobreviventes) e fica onde ele morreu;
      - os outros ficam de luto (a decisão 16), e o computador marca o dia e a causa;
      - no corpo, o E dá "Enterrar": o corpo sai, e o nome fica na lista dos que se foram;
      - o F1 revive, para os testes.

    *(A outra: agora só a morte por fome e por sede, e as doenças depois.)*

19. **A segurança.** O morador **não luta**. Se um zumbi entrar no bunker (com o portão aberto), ele grita, foge para
    o andar de baixo e fica no quarto até o zumbi sumir (o Medroso foge antes, o Corajoso fica com os outros, e o surdo
    só foge quando vê). **Os zumbis podem pegá-lo**: ele é um alvo, como os sobreviventes. Os moradores não entram no
    B4 e não saem do bunker (só o que vai embora). *(A outra: os zumbis ignoram os moradores. É mais seguro para
    testar, mas aí o portão aberto deixa de ser um perigo.)*

20. **O computador do bunker.** Nos dois postos com computador da sala de operações (B0), o E abre **a tela do
    terminal**: verde no preto, com letra de máquina, como os computadores dos anos 90 (é a época do modelo da mesa).
    Qualquer jogador abre, e o que aparece vem do anfitrião. As abas:
    - **MEMBROS:** cada morador (o nome, a idade, a função, o humor com os motivos, a saúde, as barras, o que está
      fazendo, onde está, o que carrega e a cama) e, num clique, **a ficha inteira** (a história, as habilidades, os
      traços, as condições, os laços e a opinião sobre cada um); e os jogadores (a vida, a fome, a sede e onde estão:
      "no B1" ou "fora, a 2,4 km");
    - **ESTOQUE:** a comida e a água em porções e em **dias**, contando só os moradores (e, à parte, "com os jogadores
      comendo daqui"), os remédios, o fumo, a sinuca e a lista de tudo por categoria, com a estante e o andar de cada
      coisa;
    - **LUGARES:** cada lugar, o que ele tem e a meta, que se muda ali mesmo (+ e −), e as camas livres;
    - **REGISTRO:** o diário do bunker, com os 100 últimos ("Dia 12, 07:14: Walter comeu um prato.", "Walter está com
      gripe.");
    - **MAPA:** o mapa do M, com o bunker, os jogadores e os pedidos de socorro;
    - **RÁDIO:** os pedidos de abrigo e de socorro (a decisão 8); depois, as missões das comunidades.

    ```
    ┌ BLACKWATER SHELTER · TERMINAL ───────────────────────────── DIA 12 · 14:32 ┐
    │ [MEMBROS]  ESTOQUE   LUGARES   REGISTRO   MAPA   RÁDIO                     │
    │                                                                            │
    │ WALTER HAYES · o pai · 72 anos · cozinheiro                  FELIZ  72     │
    │   agora: cozinhando o jantar (B2, cozinha)                                 │
    │   fome  ████████░░ 80    sede ██████░░░░ 60    sono ███████░░░ 70          │
    │   saúde: bem             carrega: maço de cigarros (7), lata de feijão     │
    │   + prato quente (+8)   + fumou (+6)   + sinuca (+8)   − escassez de água  │
    │                                                                            │
    │ ROSA MEDINA · 29 anos · médica                               NEUTRA 51     │
    │   agora: lendo (B1, sala de estar)                                         │
    │                                                                            │
    │ ANA · jogadora · fora do bunker, a 2,4 km      vida 85  fome 40  sede 55   │
    └────────────────────────────────────────────────────────────────────────────┘

     ESTOQUE
     COMIDA     23 itens · 31 porções · dura 6 dias (5 moradores; 3 com os jogadores)
       lata de comida ........ 10   B1 depósito, estante 3
       feijão ................  6   B2 despensa
     ÁGUA       18 itens · dura 3 dias
     REMÉDIOS   5 · FUMO 3 maços (41 cigarros) · SINUCA 2 tacos, as bolas na mesa
    ```

    *(A outra: a tela no estilo do inventário, com o concreto e os rebites.)*

O que não muda com as decisões:

- **Tudo vai pela rede.** O anfitrião manda nos moradores (as fichas, a agenda, as necessidades, o estoque e as falas).
  As outras máquinas veem o corpo, as animações, o que está na mão, as falas, as fichas e a tela do computador. Os
  pedidos (dar um item, mudar a função, mudar uma meta, aceitar quem pede abrigo) vão para o anfitrião, que confere.
- **O save** guarda os moradores no mundo: a ficha (com o que cresceu nas habilidades), as barras, o humor e os
  motivos, a opinião e as lembranças, a doença, a vida, a função, a cama, as vagas, onde ele está e o que está fazendo.
  Guarda também os pratos (com o dia em que foram feitos), as metas dos lugares, o registro, os que se foram e os
  pedidos do rádio abertos. As estantes já vão no save.
- **O desempenho:** o corpo só existe no andar montado. No papel, a conta roda uma vez por segundo.
- **O F1 ganha o painel BUNKER**, para os testes:
  - o relógio rápido (×10 e ×60);
  - encher e esvaziar as barras e mudar o humor;
  - adoecer, matar e reviver;
  - pôr um morador sorteado;
  - chamar alguém no portão e um pedido de socorro;
  - encher o estoque.

## As partes

Uma de cada vez, cada uma aprovada antes da próxima. Cada parte é conferida no Play do Editor com um jogador; o teste
com dois jogadores é do usuário. A ficha e a tela vêm primeiro: elas não precisam dos modelos nem das animações, e o
resto usa a ficha.

1. **As decisões** (este plano).
2. **A ficha e o sorteio:**
   - as listas em dados: os nomes, as histórias, os traços, as condições e os laços;
   - o gerador (a ficha inteira, a aparência pelo sorteio das roupas e os laços do grupo);
   - um teste do Editor que sorteia 10.000 e confere as regras (os pares de traços, as idades dos laços, as condições
     por idade, as médias das habilidades);
   - uma folha com 20 sorteados, cada um vestido, com a ficha em texto ao lado.
3. **A tela dos moradores:**
   - a tela depois do "Novo mundo", com o personagem girando, a reserva, o sortear, o trocar e o nome;
   - as fichas no mundo salvo, e a tela na primeira vez de um mundo de antes.
4. **Os modelos novos** (no Blender, no estilo da mobília nova):
   - o suporte dos tacos (na parede, com 4 vagas e a prateleirinha da caixa de bolas);
   - o cinzeiro (com as bitucas);
   - o prato de comida, que é um item: o prato fundo de inox com a comida e a colher, com o ícone e a foto, e o prato
     vazio;
   - a panela grande do fogão industrial;
   - a caixa de papelão que o organizador carrega.

   Uma folha de fotos, com cada um ao lado do personagem.
5. **As animações dos moradores** (um `bloco_morador.py` no `PrincipalBloco.blend`; só no corpo, sem a primeira
   pessoa):
   - sentar e levantar (da cadeira, do banco do refeitório e do sofá), sentado parado (e lendo, vendo TV, rezando e
     triste, com a cabeça baixa);
   - deitar, dormir e levantar da cama;
   - comer sentado (o prato e a colher);
   - mexer na bancada (cozinhar, lavar a louça, consertar);
   - pegar e guardar na estante (no alto, no meio e embaixo);
   - carregar a caixa com as duas mãos;
   - a tacada da sinuca (inclinado na mesa);
   - andar mancando (o joelho ruim);
   - os gestos da conversa, o choro e a tosse.

   O andar do velho é o andar de hoje, mais devagar e com o corpo curvado (no código). Depois, o export para a Unity.
6. **Os moradores no bunker:**
   - o corpo (vestido pela ficha, com o nome em cima da cabeça) e a rede;
   - o andar pelo NavMesh dos andares e pela escada (se o andar para onde ele vai não está montado, ele some na escada
     e segue no papel), com a rapidez da ficha;
   - o corpo de perto e a vida no papel;
   - a agenda de cada um, as camas e as quatro barras;
   - dormir e sentar;
   - comer e beber do estoque (ir até a estante, pegar, as vagas e o gasto) e a reserva do começo;
   - os lugares (os tipos e as metas, o suporte e a mesinha);
   - o F1 BUNKER e o save.
7. **O computador:** a tela do terminal e as abas (MEMBROS com a ficha, ESTOQUE com os dias, LUGARES, REGISTRO, MAPA e
   RÁDIO) e a das cartas (os 52 do mundo: os descobertos com a ficha, os outros com "?"). Ele vem cedo porque ajuda a
   testar o resto.
8. **O lazer, o humor, as falas e a opinião:**
   - a sinuca (sozinho e a dois), o cigarro, a TV, ler, rezar e o rádio, e os afazeres;
   - os motivos, os traços no humor, as faixas e as crises;
   - a opinião e as lembranças;
   - as falas sozinho, o cumprimento, o menu do E e as conversas entre eles.
9. **As funções e as habilidades:**
   - o cozinheiro: os pratos, o fogão, o balcão, o prato estragado, o caprichado e os jogadores comendo;
   - o organizador: as metas, as viagens com a caixa e o chão do bunker;
   - o médico;
   - as habilidades crescendo com o uso.
10. **A saúde:**
    - as doenças, as condições (os comprimidos da diabetes), a enfermaria e os remédios;
    - o grave e quem cuida dele;
    - a morte (o corpo, o luto e o enterrar);
    - a segurança (fugir dos zumbis).
11. **Trazer mais moradores:**
    - o pedido de abrigo no portão (o rádio, a ficha e o aceitar);
    - o sobrevivente perdido (o pedido no rádio e no mapa, a casa, seguir o jogador a pé e de carro);
    - o ir embora.

## Fica para depois (ideias)

- **Mais jeitos de trazer moradores:** convidar um morador de um acampamento amigo (com a Social ou pagando) e os
  presos (os do grupo de busca que se rendem, presos no isolamento e convencidos, como no RimWorld).
- **Os romances:** os casais novos, as brigas de ciúme e os filhos que nascem.
- **As habilidades que ainda não valem:** o Combate (os moradores defendendo o portão e saindo em missão), a Mecânica
  (o gerador e os carros), a Construção (os consertos e a base crescendo), o Plantio (a horta) e o Ofício (os kits de
  montar e a munição).
- **As missões pelo rádio**, no computador.
- **O gerador e o diesel:** as luzes e a TV gastando.
- **A água da chuva** na cisterna, a fonte que se renova, e **a horta** (com 5 moradores, a água é o que mais pesa).
- **A louça e a roupa suja de verdade**: os afazeres que mudam alguma coisa.
- **O cemitério no morro**, com as cruzes.
- **As partes do corpo:** a perna e a mão faltando, as próteses e as cicatrizes.
- **A idade passando:** os aniversários e a demência nos muito velhos.
- **As hordas no portão.**
- **Os moradores no Dia 0**, na casa com a família.

## A parte 2: a ficha e o sorteio (04/11/2026, aprovada: "Aprovar, parte 3")

No projeto Unity (os detalhes no `CLAUDE.md` de lá, "Os moradores do bunker"):

- **A ficha e as listas** (`Scripts/Mundo/Moradores/`, no assembly do mundo): a `FichaDoMorador`, que vai no save, e as
  listas, com os nomes em inglês (os novos e os de velho), 135 sobrenomes, os apelidos (o "Walt" do Walter, o "Doc" do
  médico, o "Red" dos ruivos), as 25 histórias de antes, as 12 dos anos do apocalipse, os 26 traços (mais o fumante) e as 8
  condições.
- **O sorteio:**
  - o grupo, com o parente da família primeiro (o pai ou a mãe, o avô ou a avó, o tio ou a tia);
  - os laços (o casal, pai e filho, os irmãos, o avô e o neto, os primos, os amigos e o ex), com as idades, os sobrenomes, a
    pele e o cabelo combinando, e a família que vem junto (o filho de um é filho do outro do casal e irmão dos outros filhos);
  - o "sortear de novo", o "trocar com a reserva", o "mudar quantos" e o sorteio de um só (quem chega depois);
  - a opinião do começo de um sobre o outro;
  - a ficha em texto;
  - a mesma semente dá o mesmo grupo.
- **A roupa:** o sorteio das roupas pelo tipo da história, com o sexo, a pele e a cor do cabelo da ficha (grisalho de 62 anos
  em diante, sem barba abaixo de 19).
- **O teste do Editor** (`MoradoresTestes`, 6, menos de 1 s): 2.000 grupos com a reserva (16.000 fichas), com as regras e os
  números:
  - 1,8 laço por grupo (10% sem nenhum);
  - 40% de fumantes;
  - com condição: 9% até 30 anos, 25% de 31 a 55, 49% de 56 a 70 e 74% acima (a tabela da decisão 5: 10, 25, 50 e 75%);
  - as habilidades em média de 4 a 6, e 1,8 paixão por ficha.
- **A folha:** `Imgs/Renders/Moradores_Folha.png`, 4 grupos de 5 vestidos, com a ficha ao lado (menu Zombie City Cleaner >
  Moradores > Folha dos sorteados).

Ajustes feitos depois de ver a folha:

- os anos no ofício param em 40 (a aposentadoria);
- acima de 66 anos, a história de antes é mais vezes a do aposentado;
- as idades ficaram mais jovens, e o amigo de um velho é quase sempre mais novo que ele (os grupos saíam cheios de velhos).

## A parte 3: a tela dos moradores (04/11/2026, aprovada: "Aprovar, parte 4")

No projeto Unity (os detalhes no `CLAUDE.md` de lá, "Os moradores do bunker"):

- **O caminho:** no "Novo mundo", o botão "Criar o mundo" virou "Escolher os moradores", e o "Criar local" também passa
  pela tela. Na tela, "Criar o mundo" (ou "Criar local") cria o mundo com os escolhidos. No Continuar, um mundo salvo antes
  dos moradores abre a tela na primeira vez (o botão diz "Jogar"), e os escolhidos são gravados no arquivo do mundo na hora.
  Quem entra pelo código de um amigo não vê a tela.
- **A tela** (no estilo da de criar o personagem):
  - à esquerda, os do bunker (de 1 a 5, com as setas; o parente primeiro) e a reserva (mais 3); embaixo de cada nome, o
    parente da família, o primeiro laço ("irmã da Tessa") ou a história de antes;
  - no meio, o escolhido vestido pela ficha, girando com o mouse;
  - à direita, a ficha: o nome ("Mudar o nome" abre o nome, o apelido e o sobrenome), a idade, as duas histórias, as 8
    habilidades com as barras e as chamas da paixão, os traços e as condições em fichinhas, o "não faz" e os laços com a
    opinião (verde de +20 para cima, vermelha abaixo de 0). Com o mouse em cima de um traço, de uma condição, de uma
    habilidade ou de um laço, o que ele faz aparece embaixo da ficha;
  - embaixo: "Voltar", "R Sortear de novo" (o escolhido, do bunker ou da reserva), "Sortear todos" e "Criar o mundo".
  - **A troca:** escolha um do bunker e depois um da reserva; o do bunker fica com a borda âmbar e aparece o botão "Trocar
    Vera por Larry". O parente não se troca.
- **O save:** o mundo salvo guarda os moradores (as fichas inteiras) e o próximo id.
- **Ajustes:** quem não é da família não repete o sobrenome de ninguém do grupo (a Tessa Gomez saía sem laço com o Jimmy
  Gomez; o teste do Editor confere isso agora), e o grisalho usa só os 4 cinzas mais claros (o mais escuro parecia preto).
- **Conferido no Play do Editor** (um jogador): a tela, a troca, o mudar o nome, e o "Criar local" com os 5 moradores no
  mundo salvo da partida, sem erro no Console; as fichas vão e voltam pelo save iguais, e um mundo de antes volta com a lista
  vazia. As fotos: `Imgs/Renders/Moradores_Tela.png` e `Moradores_Tela_Reserva.png`.

## A parte 4: os modelos novos (04/11/2026, aprovada em 05/11/2026: "Aprovar, parte 5")

Na pasta de arte, no `Mobilia.blend` (o lote `mobilia_moradores.py`, no kit e no estilo da mobília nova; os detalhes no
`CLAUDE.md`, "Convenções da mobília nova"):

- **O suporte dos tacos** (`SuporteDeTacos`): de parede, de madeira escura, com o feltro verde atrás dos tacos, o pé com
  os 4 copos onde a base do taco assenta, a travessa com as 4 fendas e a prateleirinha de cima, onde a caixa de bolas fica
  com a tampa aberta encostada atrás. Ele tem o **tamanho final** (×1,00, não ×1,30 como os móveis): os tacos (1,45 m) e a
  caixa de bolas são itens, que não crescem. As vagas e o lugar da caixa estão em constantes do script, que a Unity vai
  repetir na parte 6.
- **O cinzeiro** (`Cinzeiro`): redondo, de vidro âmbar, com os 3 apoios na borda e a cinza no fundo. As **12 bitucas são
  peças soltas**, uma por cigarro: o jogo vai mostrando uma a mais (as de cima depois das de baixo).
- **A panela grande** (`PanelaGrande`): de alumínio, 36 cm (uns 30 litros; 47 cm no jogo, em ×1,30, que cabe numa boca do
  fogão industrial), com as alças, a fuligem embaixo e os escorridos. **A tampa e o ensopado são peças soltas**: a tampa
  sai para mexer, e o ensopado encolhe em z para a panela ir esvaziando.
- **A caixa de papelão** (`CaixaDePapelao`): 45 × 32 × 30 cm (59 × 42 × 39 no jogo), aberta, com as abas caídas para fora,
  as alças vazadas, a fita rasgada, as setas e o "B1" de caneta (o depósito de onde ela sai). **O que vai dentro é uma
  peça solta** (latas, garrafas de água, remédio e maços), que aparece quando ela leva alguma coisa. Para ela, a mobília
  ganhou a textura do papelão (a 23ª, no fim da lista: na parte 6, quando o FBX for para a Unity, ela entra no
  `CatalogoDaMobilia` de lá).

No projeto Unity, no `Blender/loot.py` (os itens, com as cores da paleta):

- **O prato de comida** (`PratoDeComida`): o prato fundo de inox com o ensopado (o molho, a carne, a cenoura, a batata, a
  ervilha e o feijão) e a colher apoiada na aba. Também **o prato vazio** (`PratoVazio`, os restos e a colher no fundo) e
  **o estragado** (`PratoEstragado`, o ensopado esverdeado, o mofo e uma mosca na aba), para o prato que passou de 2 dias.
  Os três têm o ícone e a foto (`Icone_<nome>.png` e `Foto_<nome>.png`, já na pasta dos ícones do projeto). 30% maior que
  o de verdade, como o maço: o prato tem 29 cm.
- **As peças das animações** (sem ícone): o prato sozinho, a comida (que o jogo encolhe enquanto ele come), a comida
  estragada, os restos, a colher (a origem onde os dedos seguram) e o bocado na colher.

**Os FBX ainda não foram exportados** (o `Mobilia_Nova.fbx` e o `Loot.fbx` vão para a Unity na parte 6, com a montagem).

A folha: `Imgs/Renders/Moradores_Modelos.png`, 9 fotos no tamanho do jogo, com o personagem principal ao lado:
1. o suporte na parede com os 4 tacos e a caixa de bolas;
2. o cinzeiro na mesinha do fumo, com o maço e o isqueiro;
3. o cinzeiro enchendo (sem bitucas, com 4 e com 12);
4. as panelas no fogão industrial (a fechada e a aberta);
5. o ensopado de cima;
6. os pratos na mesa do refeitório;
7. os pratos de perto (com o prato sozinho e a colher com o bocado);
8. as caixas (a cheia e a vazia);
9. a cheia de cima.

E a fileira no Blender em `Bunker_Objetos_Moradores.png`.

## A tela das cartas (04/11/2026, a pedido; aprovada em 05/11/2026, depois da tela limpa: "Aprovar as cartas")

A ideia do amigo do usuário, no lugar da tela como a do RimWorld (a parte 3): "cada mundo vai sortear 52 NPCs e isso vai
ficar salvo", e a escolha dos 5 do bunker vira um sistema de cartas. Os outros vão estar pelo mapa, para serem
descobertos (moradores de comunidades, de bases, NPCs para resgatar); essa parte do mapa fica para depois. As duas
perguntas foram respondidas pela recomendação: **o parente da família vem numa carta da primeira rodada** (se ficar de
fora, vai para o mapa, e a família vai ter que encontrá-lo) e **as cartas que apareceram e não foram escolhidas ficam
com "?"** na lista do computador, como as outras (só se descobre encontrando no mundo).

No projeto Unity (os detalhes no `CLAUDE.md` de lá, "Os moradores do bunker"):

- **Os 52 do mundo**, sorteados com a semente dele e guardados no mundo salvo, em grupos (o futuro das comunidades):
  - o grupo 0 é a família: o parente e, às vezes, quem veio com ele (o outro do casal, um irmão, um amigo);
  - os outros são grupos de 1 a 6 (uns 17 por mundo): famílias, casais, bandos e gente sozinha, com os laços dentro deles;
  - uns poucos laços passam de um grupo para o outro (uns 2 por mundo: o irmão que está noutra comunidade);
  - o nome, o sobrenome (fora da família) e o apelido não se repetem no mundo;
  - o "entrou no bunker com a família no Dia 0" só sai para quem é da família. Quem era dela e ficou de fora ganha a
    história nova, "se perdeu da família no Dia 0".
- **As cartas:** 5 rodadas de 5 cartas, sem repetir. Em cada uma, o anfitrião escolhe uma, e ela vai para o bunker.
  - **A tela das rodadas é limpa** (05/11/2026, a pedido: "somente as cartas aparecendo mesmo na tela, sem o resto e o
    botão de escolher embaixo centralizado"): só as 5 cartas no meio, o botão "Escolher <nome>" embaixo delas, no meio, e
    as 5 marquinhas das rodadas embaixo dele. Saíram o título, os textos, os lugares do bunker de cima, a ficha de baixo,
    o "Voltar" e o "Desfazer" (agora no ESC e no Backspace) e a linha das teclas.
  - As cartas:
    - o retrato da cintura para cima (a escolhida gira devagar);
    - a fita "FAMÍLIA";
    - o laço que importa: com quem já está no bunker (verde), com alguém desta mão (âmbar) ou "tem um irmão lá fora";
    - o nome, a idade e o ofício, as 3 melhores habilidades com as chamas, os traços e as condições.
  - De 1 a 5 escolhem a carta; Enter ou clicar duas vezes confirma; o ESC ou o Backspace desfazem a última escolha (sem
    nenhuma, o ESC volta para o menu).
  - **Os detalhes só no fim** (a pedido: "essa parte dos detalhes vai aparecer depois quando você já escolheu todos"): com
    os 5, a revisão, com o título "O bunker está completo", as 5 cartas, a ficha inteira da escolhida embaixo delas (com
    os laços dizendo onde o outro está, no bunker ou lá fora, e o "Mudar o nome") e o "Criar o mundo" no meio, embaixo.
- **O save:** os 5 no bunker (os moradores), os outros 47 no mapa (os NPCs do mapa), os ids descobertos (os 5) e o
  próximo id. **Os mundos da tela de antes** (com os 5 moradores e sem os do mapa) ganham os 47 no próximo Continuar.
- **O teste do Editor** (`MoradoresTestes`, 7, uns 2 s): 300 mundos de 52 (15.600 fichas) com as regras de antes, as
  cartas (o parente na primeira mão, em qualquer lugar dela; 25 cartas sem repetir), a separação do bunker e do mapa, e
  completar um mundo de antes. Uns 24 laços por mundo, 40% de fumantes, as condições por idade na tabela.
- **Conferido no Play do Editor** (um jogador): as rodadas, o desfazer, a revisão e o "Criar local". O mundo novo saiu
  com 5 moradores, 47 no mapa e 5 descobertos, sem laço quebrado e sem erro no Console. A mãe da família, que ficou de
  fora, foi para o mapa com a história nova. O save vai e volta igual pelo JSON. Os saves do usuário não foram mexidos.
  Os dois mundos jogados depois da parte 3 foram completados só na memória, para conferir: 47 no mapa cada um, sem id
  nem nome repetido.
- **As fotos:** `Imgs/Renders/Moradores_Cartas.png` (a segunda rodada da tela limpa, com a primeira marquinha cheia) e
  `Moradores_Cartas_Revisao.png` (a revisão, com a ficha da família).
- **Fica para depois:** a lista do computador (as cartas descobertas e as com "?", na parte 7) e os NPCs no mapa (onde
  cada grupo vive e como se encontra cada um).

## A parte 5: as animações dos moradores (05/11/2026, feita, esperando a aprovação)

No `PrincipalBloco.blend`, pelo `bloco_morador.py` da pasta `Modelo 3D/Principal` (que roda o `bloco_cigarro.py`: o
corpo, as armas, os itens, o taco, o fumo e o IK). Os detalhes no `CLAUDE.md` da pasta de arte ("Convenções do
personagem principal em blocos", "Os moradores"). A cópia de antes está em `PrincipalBloco_Antes_dos_Moradores.blend`.

- **As medidas são as dos móveis do jogo** (a mobília em ×1,30): o assento da cadeira a 0,62 m, o do banco do refeitório a
  0,59 e o do sofá a 0,68 (afundando); a mesa do refeitório a 0,99; a cama a 0,60 (no beliche, a de cima a 1,38); o fogão
  a 1,12, a pia e as bancadas a 1,17; as prateleiras da estante de aço a 0,10, 0,65, 1,20 e 1,74 (a de 2,37 ninguém
  alcança); o pano da sinuca a 1,01. **As pernas do personagem são curtas para os móveis em ×1,30**: com o quadril no
  meio do assento da cadeira, os pés ficavam no ar. Por isso ele senta na frente do assento (o corpo reto), afunda no sofá
  (deitado para trás até o encosto, como quem vê TV) e, no beliche, senta curvado na beira (a cama de cima fica a 1,38).
- **As 29 ações** (`PrincipalBloco_<nome>`):
  - a cadeira: `Sentar_Cadeira`, `Sentado_Cadeira` (laço) e `Levantar_Cadeira`; na cadeira, `Sentado_Lendo` (o livro nas
    duas mãos, e a mão direita vira a página), `Sentado_Rezando` (as mãos juntas, a cabeça baixa) e `Sentado_Triste`
    (curvado, os antebraços nas coxas, um suspiro);
  - o sofá: `Sentar_Sofa`, `Sentado_Sofa` (laço: o de ver TV) e `Levantar_Sofa`;
  - o banco do refeitório: `Sentar_Banco` (a perna direita passa por cima do banco, depois a esquerda, e ele senta com os
    pés embaixo da mesa), `Sentado_Banco` (as mãos na mesa), `Levantar_Banco` e `Comer` (o prato na mesa e a colher na
    mão: três colheradas por laço, o bocado some na boca);
  - a cama: `Deitar` (senta na beira, vira, deita e sobe as pernas), `Dormindo` (laço, a respiração funda) e
    `Levantar_Cama`;
  - as bancadas: `Cozinhar` (mexendo a panela do fogão com a colher), `Lavar_Louca` (o prato na mão esquerda, a direita
    esfregando) e `Consertar` (a esquerda segura, a direita gira a ferramenta);
  - a estante: `Estante_Alto` (na ponta dos pés), `Estante_Meio`, `Estante_Baixo` (curvado) e `Estante_Chao` (ajoelhado
    num joelho), cada uma pegando ou guardando (o jogo troca o item entre os quadros 14 e 22);
  - `Sinuca_Tacada`: do taco em pé (o `Taco_Segurar`), ele se inclina na mesa (a mão esquerda faz a ponte no pano),
    mira duas vezes, puxa e bate (a ponta passa a bola no quadro 60), segura e levanta;
  - `Andar_Mancando` (laço, 1,5 m/s): a perna direita dura (no ar, o pé sobe pouco e vai para fora, e o quadril sobe), o
    corpo caindo para a direita quando ela apoia;
  - do peito para cima (por cima do andar, do parado ou do sentado): `Caixa_Carregar` (laço, a caixa na frente da
    barriga), `Conversa` (laço, os gestos), `Chorar` (laço, as mãos no rosto e os soluços) e `Tossir`.
- **Cada ação fica no lugar, com a raiz parada:** o jogo põe a raiz onde a ação pede. Na Unity (+z para a frente):
  - a cadeira e o sofá: a frente do assento 12 cm atrás da raiz (o móvel de costas para ela); sentado, o quadril a
    0,71 m de altura e 25 cm atrás da raiz (no sofá, 36 cm);
  - o banco: a raiz atrás do banco, olhando para a mesa (a beira de trás do banco 24 cm na frente dela, e a mesa a 80 cm
    do meio do banco, como no `Refeitorio` dos móveis do bunker);
  - a cama: atrás da raiz, ao longo do x (a cabeceira para a esquerda da raiz), a beira 12 cm atrás; deitado, o quadril no
    meio da cama;
  - o fogão, a pia e a bancada a 21 a 25 cm na frente da raiz; a estante a 30 cm; a mesa de sinuca a 25 cm.
- **As peças:** os ossos `prato`, `colher`, `bocado` (filho da colher), `caixa` e `livro` (filhos do peito, sem deformar)
  e um vazio em cada um (`Morador_<Peça>`): o jogo põe as malhas neles (o prato e a colher do `Loot.fbx`, a caixa de
  papelão da mobília em ×1,30 e o livro). A escala do osso diz se a peça aparece. O taco da tacada é o de sempre (o
  `Arma_Taco`).
- **Conferido:** os braços chegam aos alvos (de 0,57 a 1,00 do alcance; só no meio do caminho até o chão da estante o
  braço fica esticado); as tiras de cada ação com os móveis de referência (caixas do tamanho do jogo). Na Unity, o
  `Personagem_PrincipalBloco.fbx` foi exportado de novo e importado (73 clipes, os 15 laços dos moradores na lista
  `EmLacoDosMoradores` da `MontagemDasArmas`); amostrado numa cena de prévia, o quadril sentado fica a 0,71 m e 25 cm atrás
  da raiz e o prato na mesa a 0,99 m. Os controladores e o prefab do jogador não foram mexidos (a parte 6 faz o do NPC).
- **Fica para a parte 6:** o corpo dos NPCs (o `NPC_Corpo` do `Roupas.blend`) ainda não tem os ossos das peças (trazer o
  corpo de novo e exportar as roupas); o livro não tem modelo (a prévia é uma caixa simples); a colher de mexer a panela
  é a de comer; não há a animação de subir na cama de cima do beliche; e o andar do velho é o andar de hoje, mais devagar
  e curvado, no código.
- **As fotos:** `Imgs/Renders/Moradores_Anim_Sentar.png`, `_Sentado.png`, `_Cama_e_Comer.png`, `_Trabalho.png` e
  `_Outras.png` (cada ação de três quartos e de lado, com os móveis de referência).

## O estado (05/11/2026): a parte 5, esperando a aprovação

O sistema de personagens (as decisões 1 a 8) entrou no mesmo dia, a pedido, no lugar do pai sozinho. **As 20 decisões
foram aprovadas pela recomendação** ("Aprovar, parte 2"), a parte 2 também ("Aprovar, parte 3"), e a parte 3 também
("Aprovar, parte 4"). Depois da parte 4, a pedido, a tela dos moradores virou **a tela das cartas** (os 52 do mundo),
que ficou limpa em 05/11/2026 (só as cartas e o botão; a ficha na revisão) e **foi aprovada** ("Aprovar as cartas").
A parte 4 (os modelos novos) também foi aprovada ("Aprovar, parte 5"), e **a parte 5 (as animações) está feita, esperando
a aprovação**. A próxima é a parte 6, os moradores no bunker.
