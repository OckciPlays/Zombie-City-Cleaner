# O bunker novo (plano, 01/11/2026)

O pedido: pôr o bunker na cena do jogo, maior e com andares, porque depois ele vai abrigar os NPCs.
- **Ele não fica mais embaixo da casa:** fica perto da cidade do começo, com uma porta grande.
- **O primeiro andar, rente ao terreno, é a garagem** (na referência, a garagem fica num andar de baixo, mas os carros não descem escada). Junto dela, a sala de operações: os armários, as mesas e, depois, as mesas de fabricação.
- Um andar para a sala comum: os sofás, as mesas, a sinuca e a enfermaria.
- Um andar para a cozinha e a sala de jantar, com a despensa.
- Um andar com os beliches, uns quartos e a lavanderia.
- Um andar de acesso restrito, a manutenção do bunker: os canos, o gerador etc.

As referências do usuário estão em `Imgs/Bunker/`:
- `Bunker_Referencia_Portao.png`: o portal de concreto, com as abas e o portão blindado de duas folhas, enfiado num morro de grama;
- `Bunker_Referencia_Corte.webp`: o corte de um bunker de 4 andares, com a garagem, a sala comum, a cozinha e os dormitórios.

O usuário pediu sugestões também: elas estão marcadas como **(sugestão)**.

## O que tem hoje

- **O bunker da demo** (a cena `Demo_Cidade` do projeto Unity), feito no Blender (`cidade_bunker.py`), com a planta repetida na Unity (`PlantaDoBunker`):
  - fica embaixo do quintal da casa da família, com a escada saindo da cozinha;
  - é um andar só: o salão de 10 × 14 m com 5,3 m de pé-direito, 4 quartos e o depósito;
  - tem a porta blindada que abre e isola o som, as luzes e os interruptores (o "Bunker (rede)"), o painel de armas, a bancada da fabricação, a parede dos fundadores e os pontos de nascimento;
  - não tem neblina dentro, pela altura da câmera: abaixo de −0,3 m, ela some.
- **O mundo sorteado** (a cena `Mundo`, onde o jogo roda hoje):
  - a casa da família fica num bairro de uma cidade pequena, sem bunker (ele era da M10);
  - a partida começa na sala dela, com o carro na garagem;
  - o gerador já sorteia os "bunkers espalhados" (de 20 a 29 no Grande), mas só como ícones no mapa: as construções deles são da M8.
- **O mapa fixo** (a cena `MapaFixo`): ainda limpo, sem as cidades. O plano dele põe a casa da família em Maple Grove, com o bunker embaixo (na F9).
- **O que já existe e serve:**
  - o kit das construções (C#) faz as paredes, as lajes, as portas, a escada em U e o poço do elevador dos prédios, a colisão, o NavMesh, os pisos dos passos e o eco, e carrega os andares só perto de alguém;
  - a mobília nova (os sofás, a sinuca, o beliche, as camas, a cozinha, o banheiro, o escritório, a garagem e o porão), em ×1,30, com as portas e as gavetas que abrem;
  - a bancada da fabricação (o `Bancada` do projeto Unity), que abre a aba Criar.

## A proposta (o esboço)

O desenho está em `Imgs/Bunker/Bunker_Esboco.png`: o corte de lado e as plantas dos 5 andares. O SVG e o script que o desenha (`Bunker_Esboco.py`, só com a biblioteca padrão do Python) ficam na mesma pasta. É só o esboço: as medidas e os cômodos mudam com as decisões e quando der para andar por dentro (a parte 2).

1. **Por fora, um morro de terra** (como a foto):
   - o chão da garagem fica rente ao terreno, e a terra cobre a caixa do bunker (1,5 m em cima da laje);
   - na frente, o portal de concreto com as abas e o portão grande;
   - na frente do portal, o pátio de concreto, a cerca de alambrado com o portão dela e a estrada de terra até a estrada;
   - em cima do morro, a casinha da saída de cima, as 2 torres de ventilação e a antena do rádio (com a parabólica).

   De longe, ele parece um morro de grama com uma frente de concreto: escondido.
2. **O tamanho:**
   - 32 × 24 m por andar e 5 andares (uns 3.800 m²), 17 m de fundo;
   - na garagem, o pé-direito de 5 m (as caminhonetes, o macaco);
   - nos outros, 3,4 m livres (4 m de um chão ao outro, com a laje e os canos).
3. **O núcleo,** no mesmo lugar em todos os andares (do lado esquerdo de quem entra):
   - a escada em U (a dos prédios, de 3,6 m);
   - o poço do elevador, vazio por enquanto;
   - o duto dos canos e do ar, que sobe do B4 até as torres de ventilação.

   A escada desce do B0 ao B4 e **sobe do B0 até a casinha no alto do morro**: a segunda saída, se a garagem for tomada **(sugestão)**.
4. **Os andares:**
   - **B0, a garagem e as operações:**
     - **o portão** (5,5 × 4,2 m), de duas folhas, como o da foto, com **a portinhola** numa das folhas **(sugestão)**: o portão é lento e barulhento (os zumbis ouvem), e a portinhola, rápida e quieta, é para quem está a pé;
     - **a garagem** (16 × 24 m): as 4 vagas pintadas no fundo, a área de manobra, a oficina aberta do lado (a bancada, os pneus, as peças e o macaco) e a sala do combustível (os tambores e os galões);
     - **a ala das operações** (12 m de largura), entre a garagem e a escada, por onde todo mundo passa:
       - na frente, a sala de operações, com a janela de vidro para a garagem: a mesa de reunião, o rádio, o mapa na parede e, depois, o quadro das missões;
       - no meio, o hall da escada;
       - no fundo, o arsenal e a fabricação: o painel de armas, os armários e as bancadas (a de hoje e as que vierem).
   - **B1, a sala comum:**
     - a sala de estar (os sofás, a TV, as estantes e a lareira) e, aberta para ela, a de jogos (a sinuca, as cartas e os dardos);
     - a enfermaria (3 camas, a maca e os remédios) e o isolamento **(sugestão)**: 2 quartos com a porta de grade, para quem foi mordido;
     - o depósito de suprimentos e 2 banheiros.
   - **B2, a cozinha e o refeitório:**
     - a cozinha industrial (os fogões, a coifa e as bancadas de inox), com o balcão de servir para o refeitório;
     - o refeitório: 4 mesas grandes, 24 lugares;
     - a despensa (as prateleiras com as latas, os sacos e a água), a câmara fria, a lavagem da louça e o lixo.
   - **B3, os dormitórios:**
     - os 4 quartos da família, um para cada jogador **(sugestão)**;
     - o dormitório dos NPCs, com 6 beliches (12 camas), e 2 quartos de casal a mais;
     - os banheiros com os chuveiros, a lavanderia e a rouparia.
   - **B4, a manutenção (restrito):**
     - a porta de aço trancada na saída da escada;
     - o gerador (2 a diesel e o tanque) e a elétrica (o quadro e as baterias);
     - a água (a cisterna, as bombas e os filtros) e o ar (os filtros, os ventiladores e os dutos subindo);
     - o esgoto e as peças;
     - os canos no teto.
5. **O visual:**
   - o concreto com as marcas da forma, as faixas amarelas e pretas nas bordas e nas portas e as vagas pintadas;
   - o nome de cada andar pintado na parede da escada, em inglês (o jogo se passa nos Estados Unidos): "B1 – COMMONS", "GARAGE", "AUTHORIZED PERSONNEL ONLY";
   - os canos e os dutos no teto e as lâmpadas do gerador;
   - usado, mas não abandonado: o grau "pouco" do abandono, como a casa da família.
   - **Sem neblina dentro,** como hoje. Hoje isso vem da altura da câmera; com a garagem rente ao chão, passa a vir de estar dentro do bunker.
6. **O que vem do bunker de hoje:**
   - a porta, que vira o portão e a portinhola, pela rede;
   - as luzes e os interruptores, um grupo por cômodo;
   - o painel de armas e a bancada, no arsenal;
   - a parede dos fundadores;
   - os pontos de nascimento.
7. **Os zumbis:**
   - não nascem dentro do bunker;
   - com o portão ou a portinhola abertos, eles entram (o NavMesh liga a garagem ao pátio); fechados, não.
   - As hordas no portão ficam para depois, com a cerca e a base crescendo.
8. **Como é feito:**
   - a construção sai do kit das construções do jogo (C#), como as casas, as lojas e os prédios;
   - os objetos novos (o portão, o gerador, os tanques, o rádio, a cozinha industrial...) saem do Blender, no estilo da mobília nova.

## As decisões (com a recomendação)

**Respondidas (01/11/2026: "Aprovar, ir à parte 2"): todas pela recomendação.** O texto abaixo é o da proposta.

1. **Em que mundo:**
   - primeiro numa cena de teste (`Scenes/Exemplos/Bunker.unity` do projeto Unity), para andar por dentro e aprovar;
   - depois no mundo sorteado, onde o jogo roda hoje, perto da cidade da família;
   - no mapa fixo, quando as cidades chegarem: perto de Maple Grove, num lugar escolhido à mão. Isso muda a F9 do `Plano_Mapa_Fixo.md` (o bunker deixa de ficar embaixo da casa).

   A outra opção: direto no mapa fixo, que ainda não tem cidades.
2. **Onde, no mundo sorteado:**
   - fora da cidade da família, de 300 a 800 m dela;
   - na beira de uma estrada, com uns 100 m de estrada de terra até o portão;
   - num lugar mais liso, com o portão virado para a estrada;
   - o morro é feito pelo jogo: o relevo sobe em volta da caixa.

   A outra opção: num morro de verdade do relevo, com a garagem cavada na encosta. Fica mais natural, mas limita os lugares possíveis.
3. **O começo e a casa da família:**
   - a partida passa a começar no bunker, com os 4 jogadores na sala de operações e o carro na garagem;
   - a casa da família continua na cidade como a casa de antes, sem nada especial por enquanto (depois, as lembranças e o saque dela).

   A outra opção: a casa sai e vira uma casa comum do bairro.
4. **O tamanho e a ordem dos andares:**
   - 32 × 24 m, com 5 andares, na ordem do pedido: B0 a garagem, B1 a sala comum, B2 a cozinha, B3 os dormitórios e B4 a manutenção;
   - o pé-direito de 5 m na garagem e de 3,4 m nos outros.

   A outra opção: menor (28 × 20 m), com os cômodos mais apertados para os móveis em ×1,30.
5. **A segunda saída:** a escada sobe do B0 até a casinha no alto do morro. A porta de aço dela abre de dentro e, de fora, fica trancada. A outra opção: só o portão.
6. **O portão:**
   - de duas folhas (5,5 × 4,2 m), abrindo para dentro da garagem (o lado de fora fica livre para o carro);
   - lento (uns 6 s) e barulhento (os zumbis ouvem a uns 40 m);
   - com a portinhola numa das folhas, rápida e quieta.

   A outra opção: só o portão.
7. **Os quartos:**
   - os 4 da família, um por jogador, com o nome dele na porta; o jogador volta na cama dele quando cai, como na cama montada da fabricação;
   - o dormitório e os 2 quartos de casal, para os NPCs, depois.

   A outra opção: todos iguais, sem dono.
8. **O B4 restrito:** a porta de aço trancada. Os jogadores abrem (todos têm a chave do bunker); os NPCs não entram (depois). A outra opção: só o anfitrião abre.
9. **O isolamento na enfermaria:** os 2 quartos com a porta de grade, para quando a mordida virar infecção. A outra opção: sem ele, e a enfermaria fica maior.
10. **O elevador:** só o poço agora, fechado com a porta; o elevador de carga vem depois, ligado no gerador. A outra opção: sem o poço.
11. **Os fundadores:** na parede do fundo da garagem, em cima das vagas (quem chega de carro vê). A outra opção: na sala comum.
12. **O bunker da demo:** a `Demo_Cidade` continua com o de hoje, sem mexer (ela é a demo). A outra opção: trocar lá também.

## Fica para depois (ideias)

- **O gerador de verdade:** as luzes dependem dele, e ele gasta o diesel achado nos postos.
- **A água da chuva** na cisterna: a fonte que se renova.
- **A horta hidropônica:** as plantações sem sol, com a luz do gerador, num andar a mais embaixo ou numa parte do B4.
- **As missões pelo rádio:** as comunidades chamam na sala de operações.
- **Os NPCs morando:** cada um com a cama dele, trabalhando nos andares.
- **O elevador de carga.**
- **A base crescendo:** as hordas no portão, a cerca, as torres e, depois, a comunidade.
- **O que se guarda no bunker:** a despensa e o arsenal como baús de todos, guardados pelo anfitrião (no disco, na M10).

## As partes (cada uma aprovada antes da próxima)

Os andares se chamam B0 a B4; as partes do plano são numeradas, para não confundir.

1. **As decisões e o esboço** (este plano).
   - **O estado (01/11/2026): aprovado** ("Aprovar, ir à parte 2": as 12 decisões pela recomendação). O esboço está em `Imgs/Bunker/Bunker_Esboco.png`.
2. **A casca, numa cena de teste** (`Scenes/Exemplos/Bunker.unity`, no menu Zombie City Cleaner > Exemplos > Bunker):
   - os 5 andares feitos pelo kit: as lajes, as paredes, os cômodos vazios, as portas, o núcleo com a escada e o poço, e a casinha da saída de cima;
   - a frente, em blocos: o portal e as abas de concreto, o morro, o pátio, a cerca, as torres de ventilação e a antena;
   - o portão e a portinhola como caixas que abrem, ainda sem o modelo;
   - a colisão e o NavMesh, do pátio ao B4, com o teste do Editor: todo cômodo tem caminho;
   - as luzes por cômodo, sem neblina dentro, os pisos dos passos e o eco;
   - o jogador anda por tudo na primeira e na terceira pessoa, para sentir o tamanho, e o P mostra a planta de cada andar (como na cena das variações).

   As fotos vão em `Imgs/Renders/Bunker_Casca_*`.
   - **O estado (01/11/2026): aprovado** ("Aprovado, parte 3"). A cena `Scenes/Exemplos/Bunker.unity` do projeto Unity (menu Zombie City Cleaner > Exemplos > Bunker (o novo); as fotos, em Exemplos > Fotos do bunker).
     - **O bunker** é feito pelo kit das construções (`PlantaDeBunker` e `Bunker`, como os prédios), em 6 partes: a casca e os 5 andares por dentro.
       - **A casca:** as paredes de fora (40 cm), as lajes (60 cm), o núcleo, a escada em U do B4 até a casinha, a casinha, a frente e o que vai no morro.
       - **Cada andar:** as paredes de dentro com as portas, as passagens e as janelas de dentro, o piso de cada cômodo e as calhas de luz.
       - Os cômodos e as portas são os do esboço, com as mesmas medidas.
     - **O que tem:**
       - no B0, as vagas pintadas, a janela das operações para a garagem e dois carros nas vagas;
       - no B2, o balcão de inox entre a cozinha e o refeitório;
       - as portas de aço (as folhas de metal) no combustível, no isolamento, na câmara fria e nas salas de máquinas do B4;
       - o elevador parado em todos os andares (a porta fechada);
       - a escada sobe da garagem em duas subidas até a casinha no alto do morro, com um patamar no meio.
     - **A frente:**
       - o portal no corte do morro, com as abas de concreto seguindo o talude, a parede de cima até o alto do morro, o batente de aço e a placa redonda (lisa, por enquanto);
       - o morro de terra (1,5 m em cima da laje e os taludes de 1 para 2), o pátio de concreto, a cerca de alambrado com o vão da estrada e a estrada de terra;
       - em cima, as 2 torres de ventilação, a antena e a parabólica.
     - **O que abre com o E:**
       - o portão: as duas folhas abrem juntas, para dentro, em 6 s;
       - a portinhola, na folha da esquerda, em 0,6 s;
       - a porta de aço do B4 e a da casinha.
       - Por enquanto são caixas com as nervuras, as dobradiças e as trancas (o modelo é da parte 3); o barulho que os zumbis ouvem vem com o jogo (a parte 5).
     - **A luz:**
       - 116 lâmpadas (as calhas de cada cômodo e as arandelas da escada), sem sombra; o L acende e apaga;
       - dentro da caixa não tem neblina nenhuma;
       - na garagem e na casinha, a luz de fora cai para 35% (o dia entra pelo portão); embaixo, para 8%.
     - **Para andar:**
       - as setas pulam entre o pátio, o hall de cada andar (na frente da porta da escada) e o alto do morro;
       - o P mostra a planta do andar em que o jogador está (↑ ↓ os outros);
       - a tela diz o andar e o cômodo, com o tamanho;
       - o T troca para a terceira pessoa.
     - **Os números:** uns 16 mil triângulos (a casca 7,9 mil; os andares de 1,2 a 2,6 mil).
     - **Conferido:**
       - os 2 testes novos do Editor passam (`BunkerTestes`, menos de 1 s): os cômodos cobrem cada andar sem se sobrepor e as portas ficam numa parede de verdade; com o NavMesh do agente do zumbi, os 12.719 pedaços de chão (de 0,5 m) dos 39 cômodos têm caminho do pátio, entrando pelo portão e descendo a escada, e a casinha também;
       - o primeiro teste achou o patamar do meio da subida até o morro embaixo da laje da garagem, com 1,45 m de altura: agora a laje tem o vão da caixa da escada inteira;
       - no Play: o jogador nasce no pátio, as duas folhas do portão abrem juntas, o jogador fica de pé no B4 e a tela diz onde ele está, sem neblina lá embaixo; sem erros no console.
     - **Ficou para a parte 5** (o jogo de verdade): os pisos dos passos e o eco (a cena de teste não tem os passos) e o barulho do portão.
     - As fotos:
       - `Imgs/Renders/Bunker_Casca_Vistas.png`: a frente, o morro, o alto do morro, a garagem, as operações, a escada, a sala comum, o refeitório, os corredores dos dormitórios, a manutenção e a casinha por dentro;
       - `_Plantas.png`: os 5 andares de cima, com os nomes e os tamanhos;
       - `_Corte.png` (pelo meio, de frente) e `_Corte_Lado.png` (pela escada, de lado).
3. **Os objetos novos no Blender** (o lote do bunker, no `Mobilia.blend`, no estilo da mobília nova):
   - o portão blindado de duas folhas com a portinhola, e as portas de aço do B4 e da casinha;
   - o gerador, o tanque de diesel, o quadro elétrico e as baterias;
   - a cisterna, as bombas e os filtros de água, os filtros de ar e os ventiladores;
   - os canos e os dutos, em peças para o kit encaixar;
   - a estação de rádio e o mapa da parede;
   - os armários de aço do arsenal e do vestiário, o macaco e os pneus da oficina, os tambores e os galões;
   - a cozinha industrial (o fogão de 6 bocas, a coifa, a bancada e a pia de inox) e a porta da câmara fria;
   - a mesa do refeitório com os bancos, a cama de hospital e o biombo, as máquinas de lavar e de secar;
   - as torres de ventilação e a antena com a parabólica.

   As fotos vão em `Imgs/Renders/Bunker_Objetos_*`.
   - **O estado (01/11/2026): aprovado** ("Aprovado, parte 4"). O lote do bunker é o `Modelo 3D/Cidade/mobilia_bunker.py`, no `Mobilia.blend`, com o kit da mobília nova (as peças, as texturas, a cor de cada peça, a parte que troca de cor e o de longe de cada modelo).
     - **40 modelos, em 9 fileiras:**
       - **as portas:** o portão de duas folhas com a portinhola, a porta de aço e a da câmara fria;
       - **as máquinas:** o gerador, o tanque de diesel, o quadro elétrico (3 armários) e as baterias;
       - **a água e o ar:** a cisterna, a bomba, os filtros de água, o filtro de ar e o ventilador;
       - **os canos e os dutos:** o cano reto, a curva, o tê, o registro, o suporte, o duto, a curva do duto e a grelha;
       - **as operações:** a estação de rádio, o mapa da parede, o armário de aço e o vestiário;
       - **a oficina:** o macaco, os pneus, os tambores e os galões;
       - **a cozinha e o refeitório:** o fogão industrial, a coifa, a bancada e a pia de inox, a mesa e o banco;
       - **a saúde e a lavanderia:** a cama de hospital, o biombo, a máquina de lavar e a secadora;
       - **o morro:** a torre de ventilação e a antena.
     - **O que abre:**
       - as duas folhas do portão (para dentro), com a portinhola presa na folha da esquerda;
       - as portas de aço, a da câmara fria, as 3 do quadro elétrico, as do armário de aço e as 4 do vestiário;
       - a tampa do filtro de ar, os dois fornos do fogão e as portas da lavadora e da secadora.
     - **A escala:**
       - quase tudo tem o tamanho real e vai para a Unity em ×1,30, como os móveis;
       - o que encaixa na construção tem o tamanho final (×1,00): o portão (o vão de 5,5 × 4,2 m), as portas (os vãos do kit), os canos e os dutos (as peças de 1 m), a torre e a antena;
       - o que sobe até o teto (o escapamento do gerador, a entrada da cisterna, os dutos do filtro de ar e do ventilador) para em 2,6 m, que dão 3,4 m na Unity, o teto do B4.
     - **Os números:** de 60 triângulos (a curva do duto) a 3.044 (o quadro elétrico, com os disjuntores por dentro); de longe, de 24 a 876.
     - **Como as outras peças da mobília,** as chapas grandes são de esmalte com umas manchas de ferrugem. A textura de ferro inteira nelas parecia camuflagem (o mesmo do arquivo de aço).
     - **Na Unity desde a parte 4:** o `Mobilia_Nova.fbx` foi exportado de novo com o lote, e os prefabs foram montados.
     - **As fotos:**
       - `Imgs/Renders/Bunker_Objetos_<fileira>.png`, e `_Aberto` nas fileiras com partes que abrem;
       - todas numa folha só: `Bunker_Objetos_Folha.png`.
       - O inox e o aço galvanizado saem escuros na prévia do Blender (o metálico do material da prévia); na Unity, os móveis são foscos.
4. **A mobília e o acabamento** de cada andar:
   - as receitas de cada cômodo, com a mobília nova e os objetos da parte 3;
   - o concreto, as faixas, as vagas, os nomes dos andares e os canos no teto;
   - o painel de armas, a bancada e os fundadores.

   Um andar por vez, se ficar grande: o B0 e o B1, depois o B2 e o B3, depois o B4. Fotos de cada andar.
   - **O estado (01/11/2026): a primeira fatia (o acabamento de todos e a mobília do B0 e do B1) foi aprovada** ("Aprovado, siga"), e a segunda (o B2 e o B3) também ("Aprovado, siga"). e a terceira (o B4), a última, também ("Aprovado, parte 5"): **a parte 4 está aprovada**.
     - **Os modelos na Unity:** o `Mobilia_Nova.fbx` com o lote do bunker foi para o projeto Unity, e a montagem da mobília nova fez os prefabs (125) e as medidas deles.
     - **O que abre agora é o modelo:** o portão de duas folhas (para dentro, juntas, em 6 s), a portinhola presa na folha da esquerda (0,6 s) e as portas de aço do B4 e da casinha. As caixas da parte 2 saíram.
     - **O acabamento (em todos os andares, pelo kit):**
       - a barra verde-escura até 1,2 m com o friso amarelo nos cômodos de serviço (os corredores, a garagem, a oficina, o combustível, as operações, o arsenal, o depósito e os da manutenção); a sala, a sala de jogos, a enfermaria e os dormitórios ficam sem ela;
       - o nome de cada andar em inglês em cima da porta da escada, dos dois lados ("B0 GARAGE", "B1 COMMONS", "B2 KITCHEN", "B3 QUARTERS" e "B4 MAINTENANCE"), em letras de traço pintadas pelo kit;
       - as faixas pretas e amarelas nos batentes da porta da escada, em volta do portão (por dentro) e no chão na frente dele, e o aviso "AUTHORIZED PERSONNEL ONLY" na porta do B4.
     - **Os canos e os dutos no teto** (as peças de 1 m da parte 3, pintadas pela cor do que levam: a água azul, o diesel vermelho, a ventilação amarela, o esgoto marrom e o duto galvanizado): no B0, o duto pelo meio da garagem e os canos de água e de diesel na parede das operações e no hall; no B1, os do hall e os do fundo da sala de jogos.
     - **Os móveis** (`MoveisDoBunker` do projeto Unity, escritos um por um), em ×1,30 como no mundo, cada um com a caixa de colisão na colisão do andar (o zumbi desvia):
       - **B0, as operações:** a estação de rádio, o mapa da parede, a mesa de 6 lugares, os dois postos na janela da garagem (a mesa, o computador e a cadeira de rodinhas), os arquivos e as estantes de aço;
       - **B0, o arsenal e a fabricação:** o painel de armas do bunker antigo (com a "Parede de armas" do jogo: as armas aparecem nele no Play), duas bancadas, os armários de aço, o vestiário, a estante e a mesa com as cadeiras;
       - **B0, a garagem:** os fundadores na parede do fundo, em cima das vagas, e os dois carros nas vagas; a oficina (a bancada, as estantes, o macaco e os pneus) e o combustível (os tambores e os galões);
       - **B1, o depósito:** as estantes de aço nas paredes e uma ilha de duas no meio, e a prateleira de madeira;
       - **B1, os banheiros:** 4 privadas, 2 boxes, a pia dupla e o toalheiro;
       - **B1, a sala de estar:** a TV de chão entre as estantes baixas, o sofá de canto e duas poltronas em volta da mesa de centro no tapete persa, o canto de leitura (duas poltronas, a mesinha e o tapete redondo), a mesa de cartas com 4 cadeiras, as estantes de livros, o abajur, o quadro e o vaso;
       - **B1, a sala de jogos:** a sinuca, a mesa redonda e uma mesa de 4 lugares, o sofá de 3 lugares com a estante baixa, o sofá de 2 com a poltrona e a mesa de centro no tapete de listras, a estante de livros, o abajur e o quadro;
       - **B1, a enfermaria e o isolamento:** 3 e 2 camas de hospital, os biombos, a mesa com a cadeira, o armário de aço e a pia.
     - **Os números:** 275 peças na cena (os móveis, os canos e as portas); o C troca a cor dos móveis do catálogo e o E abre as portas e as gavetas, como na cena das variações.
     - **Conferido:** os 2 testes do Editor passam com as caixas dos móveis: os 39 cômodos têm caminho do pátio (12.132 pedaços de chão).
     - **As fotos:** `Imgs/Renders/Bunker_Andares_B0B1_Vistas.png` (12 cômodos do B0 e do B1) e `_Plantas.png` (as plantas dos dois, com os móveis); no Unity, Zombie City Cleaner > Exemplos > Fotos da mobília do bunker.
   - **A segunda fatia (o B2 e o B3, 01/11/2026): aprovada** ("Aprovado, siga").
     - **B2, a cozinha e o refeitório:**
       - o refeitório: 8 mesas compridas, cada uma com um banco de cada lado (2 fileiras de 4), a bancada da água com a geladeira e os galões na parede do fundo e um quadro;
       - a cozinha: os dois fogões industriais com as coifas na parede do fundo (a chaminé até o teto), as bancadas de inox, a pia de inox, as duas geladeiras, uma estante e duas bancadas de preparo no meio; o balcão de inox da janela para o refeitório é o da parte 2;
       - a câmara fria: as estantes de aço em volta e dois freezers no meio. **A porta dela agora é o modelo** (a folha grossa e branca, o batente de inox e o termômetro do lado da cozinha), abrindo para a cozinha: o kit faz o vão sem a folha;
       - a despensa: 28 estantes de aço (nas paredes e em duas ilhas de costas), a prateleira de madeira e dois freezers;
       - a lavagem e o lixo: a pia de inox, as bancadas, os tambores, os galões e uma estante.
     - **B3, os dormitórios:**
       - o dormitório: 9 beliches (18 camas), com um baú no pé de cada, e os guarda-roupas;
       - os quartos 1 a 4 (dois deles espelhados): a cama de casal e a de solteiro no fundo, com o criado-mudo entre elas, o guarda-roupa, a cômoda e o tapete; os quartos 5 e 6, nas pontas do corredor: a cama de casal com os dois criados-mudos, o guarda-roupa de 3 portas, a cômoda e o tapete;
       - a lavanderia: 2 máquinas de lavar, 2 secadoras, o tanque (a pia de inox), as estantes e a mesa de dobrar; a rouparia: 3 guarda-roupas, 5 estantes e a mesa de dobrar; o depósito: 5 estantes;
       - os banheiros e os chuveiros: 6 privadas, 6 boxes, 3 pias duplas, os toalheiros e a banheira.
     - **Os canos e os dutos:** no B2, os do hall, o cano de água e o duto do refeitório, o duto da cozinha (em cima das coifas) e o esgoto da lavagem; no B3, os dos dois corredores.
     - **Os números:** 91 móveis no B2 e 94 no B3; na cena, 693 peças (os móveis, os canos e as portas).
     - **Conferido:**
       - os 2 testes do Editor passam: os 39 cômodos têm caminho do pátio (10.984 pedaços de chão). O teste pegou os quartos 1 a 4 fechados duas vezes: com a cama de solteiro numa parede do lado, ela e a ponta da folha da porta aberta (ou a cama de casal) fechavam a passagem. Agora as duas camas ficam no fundo;
       - no Play: o portão abre (as duas folhas para dentro), a porta da câmara fria abre para a cozinha, as armas aparecem no painel e o console fica sem erros.
     - **As fotos:** `Imgs/Renders/Bunker_Andares_B2B3_Vistas.png` (15 cômodos) e `_Plantas.png`; no Unity, Zombie City Cleaner > Exemplos > Fotos da mobília do bunker (B2 e B3).
   - **A terceira fatia (o B4, a manutenção, 01/11/2026): aprovada** ("Aprovado, parte 5").
     - **O gerador:** os dois geradores a diesel no meio da sala (o escapamento subindo até o teto), o tanque de diesel e os tambores no fundo, a bancada e o armário de aço.
     - **A elétrica:** os 3 quadros elétricos na parede da frente (as portas abrem, com os disjuntores por dentro), 5 bancos de baterias nas paredes dos lados e a mesa de controle com o computador e a cadeira.
     - **A água:** as duas cisternas (a entrada da água da chuva subindo até o teto), as duas bombas, os dois filtros de água, a estante e os galões.
     - **O ar:** os dois filtros de ar NBC no fundo (a tampa abre), os dois ventiladores na parede, a bancada e a estante.
     - **O esgoto:** a fossa (por enquanto, o modelo da cisterna, azul) e as duas bombas; **as peças:** as estantes, a bancada, os armários, os tambores e os galões.
     - **Os canos e os dutos:** no hall, a água, a ventilação e o diesel; o esgoto; os eletrodutos (cinza-escuro) em cima dos quadros; o diesel do tanque aos geradores; a linha de água das bombas e, mais baixa, a dos filtros; os dutos do gerador e do ar. **As peças soltas no teto** (novas): os registros nas linhas de água e de diesel (6) e as grelhas de ar (8).
     - **Os números:** 44 móveis e máquinas no B4; na cena, 918 peças (do B0 ao B4).
     - **Conferido:** os 2 testes do Editor passam (os 39 cômodos com caminho do pátio, 10.622 pedaços de chão); no Play, as portas de aço do B4, os quadros e o filtro de ar abrem, e o console fica sem erros.
     - **As fotos:** `Imgs/Renders/Bunker_Andares_B4_Vistas.png` (9 vistas) e `_Plantas.png`; no Unity, Zombie City Cleaner > Exemplos > Fotos da mobília do bunker (B4).
     - **Fica para depois:** a fossa com uma cor própria (hoje ela é a cisterna azul) e as curvas e os tês nos cruzamentos dos canos (hoje as linhas que se cruzam passam em alturas diferentes).
5. **No mundo sorteado:**
   - o lugar no plano do mapa: perto da cidade da família, o morro no relevo, a estrada de terra, o pátio e a cerca;
   - o ícone no mapa (o M);
   - a rede: o portão, a portinhola, a porta do B4 e as luzes (o anfitrião guarda);
   - a partida no bunker e o carro na garagem;
   - os andares carregando só perto de alguém, como os dos prédios;
   - o desempenho: dentro, perto e a 300 m.

   Testado pelo usuário com dois jogadores.
   - **O estado (01/11/2026): feita, esperando o teste do usuário com dois jogadores.**
     - **O lugar** (a geração, agora na versão 10; um mundo de antes não serve mais):
       - a beira de uma estrada asfaltada de 300 a 800 m da cidade da família (sem lugar, de 150 a 1.500 m), dos dois lados dela, fora das cidades, do lago e da cerca da quarentena, longe das outras estradas e com no máximo 12 m de desnível; ganha o lugar mais liso;
       - o portão virado para a estrada, com 100 m de estrada de terra reta até ele;
       - o pátio e o pé do morro planos, na altura da estrada (uma plataforma, como as dos postos); o terreno sai de baixo da caixa inteira, e o morro, o pátio e a cerca são feitos pelo jogo (os mesmos da cena de teste).
     - **O mapa:** o ícone (o quadrado escuro com o miolo amarelo) e o nome, "Bunker da família", no M do jogo e nas imagens do Editor (`Imgs/Mapas/Mapa_<tamanho>_<semente>.png`, refeitas, com o item na legenda).
     - **A partida:**
       - começa na sala de operações, com os 4 em volta da mesa de reunião;
       - a perua e o cupê ficam nas vagas da garagem, de frente para o portão;
       - a casa da família ficou como as outras da cidade.
     - **Perto de alguém** (como os andares dos prédios):
       - a casca, o morro, as portas, o painel de armas (a "Parede de armas" vem para o arsenal) e os fundadores aparecem até 300 m;
       - por dentro, carregam o andar de cada um e os vizinhos dele; de fora, a garagem, até 45 m;
       - cada andar é montado em outra thread, com a colisão cozida em outra thread;
       - os móveis entram aos poucos, 3 ms por quadro. Todos de uma vez, descer do B1 ao B4 (o B3 e o B4 juntos) travava um quadro de uns 70 ms; agora o pior é de 16 ms, e os móveis aparecem em uns 10 quadros;
       - os canos, os dutos e as peças do teto de cada andar são uma malha só. Antes eram de 100 a 180 objetos por andar, e o de longe deles ficava sem a cor.
     - **A rede** (o anfitrião manda e confere a distância, 5 m):
       - o portão, a portinhola, as portas de aço do B4 e da casinha e a da câmara fria;
       - a luz de cada andar, no interruptor ao lado da porta da escada;
       - as portas e as gavetas dos móveis, pela rede da mobília nova.
     - **O portão e os zumbis:**
       - abrir ou fechar o portão chama os zumbis num raio de 40 m;
       - nenhum zumbi nasce em volta do bunker;
       - com o portão aberto, eles entram (o NavMesh passa pelo vão).
     - **Dentro:**
       - a luz de fora cai: na garagem e na casinha sobram 35%, embaixo 8%;
       - não há neblina nem as partículas no ar;
       - os passos têm o piso de cada cômodo, e o eco só vale dentro.
     - **Conferido:**
       - os testes do gerador passam (43), com um novo: o bunker fica fora da cidade, com a estrada de terra, o pátio plano e a partida na sala de operações;
       - no Play (Pequeno, semente 12345), a partida fica pronta em 2,3 s;
       - o portão abre de perto e não de longe;
       - nenhuma colisão do terreno fica dentro da caixa. O teste pegou o rodapé de um bloco do terreno (a faixa de 3 m nas emendas de 64 m) atravessando o B1 como uma parede; o buraco era só no núcleo e agora pega a caixa inteira;
       - o console fica sem erros.
     - **O desempenho** (no Editor, de dia, Pequeno 12345; o quadro, a GPU e os draw calls):

       | Onde | Quadro | GPU | Draw calls |
       | --- | --- | --- | --- |
       | No B1 (a sala) | 6,1 ms | 5,9 ms | 1.310 |
       | No B4 (a manutenção) | 6,4 ms | 6,2 ms | 1.364 |
       | Na garagem | 6,4 ms | 5,3 ms | 1.450 |
       | No pátio, a 25 m do portão | 6,5 ms | 2,9 ms | 1.078 |
       | A 290 m, olhando para o bunker | 6,5 ms | 2,8 ms | 738 |
       | A 400 m, sem o bunker | 6,3 ms | 3,1 ms | 701 |

       - Dentro, o quadro é o mesmo de fora: o tempo é o da CPU.
       - A GPU dobra, por causa das ~30 lâmpadas por andar e de tudo o que entra na sombra do sol.
       - A 290 m, o bunker não pesa nada que se meça.
     - **As fotos** (`Imgs/Renders/`):
       - `Bunker_Mundo_Fora.png` (de dia, com a neblina);
       - `_Portao.png` (o portão aberto);
       - `_Dentro.png` (o B0, o B1 e o B2);
       - `_Dentro_B3B4.png` (os dormitórios e a manutenção).
     - **Ficou para depois:**
       - os quartos com o nome de cada jogador e a volta na cama dele quando cai (a decisão 7);
       - guardar no disco as portas e as luzes (o mundo salvo, a M10).
