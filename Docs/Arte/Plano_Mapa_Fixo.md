# Zombie City Cleaner: o mapa fixo de 15 km

Proposta de 29/10/2026. **As decisões foram respondidas e o esboço aprovado no mesmo dia** (a Parte F0) e a F1 foi feita. **No mesmo dia o rumo mudou, a pedido** (veja "O mapa limpo"): o mapa começa limpo, só com o relevo, a água, a mata, a quarentena e as estradas principais, tirado do mapa de referência do usuário, numa cena nova, e o resto entra aos poucos. **A cena do mapa limpo está feita e espera a aprovação do usuário** (a Parte F1b). É o passo seguinte ao gerador de mapas do `Plano_Mapa.md`: em vez de um mapa sorteado por semente, um mapa só, desenhado e congelado, de 15 km, com montanhas, o grande lago, rios e cidades com pontes.

- Como no `Plano_Mapa.md`, o trabalho vai em partes (F0 a F9, "F" de fixo), cada uma aprovada antes da próxima.
- O que está marcado com "(sugestão)" é ideia minha e pode sair.
- O esboço de agora está em `Imgs/Mapas/MapaFixo_Esboco.png` (e o `.svg`, que abre no navegador; o script que o desenha é o `MapaFixo_Esboco.py` da mesma pasta). É só para conversar: as posições, os nomes e os tamanhos mudam à vontade.
- As decisões do usuário estão em "Decisões", no fim (as 12 respondidas em 29/10/2026).

## O mapa limpo (a mudança de 29/10/2026)

O pedido, com as duas imagens do mapa de referência (agora em `Imgs/Mapas/MapaFixo_Referencia_1.webp` e `_2.webp`): "vamos limpar o mapa, deixar somente o relevo, montanhas, florestas, rios, lago, a borda de quarentena, sem todo o resto. A ideia é que a gente vá construindo o mapa aos poucos em vez de já ter tudo; acho que assim conseguimos criar um mapa mais preciso. Vamos fazer esse mapa numa cena nova. Pode manter as estradas grandes também: vai servir para andar um pouco pelo mapa de carro e medir algumas coisas. Deixa uma opção para ligar e desligar a névoa. Vamos tentar seguir mais fielmente a imagem, mas em escala menor. Somente as estradas principais, as que estão em vermelho na imagem."

- **O desenho foi refeito pela imagem**, nos mesmos 15 km: o contorno de cada lago e de cada ilha, as linhas dos rios, as montanhas, o brejo e a cobertura do chão (a mata, o campo, as lavouras e a rocha) saíram da imagem, pixel por pixel, e as estradas vermelhas, pela linha delas. A linha da quarentena e os bloqueios foram desenhados à mão. As cidades, os distritos, os lugares, a casa da família, a ferrovia, as serras e os picos do esboço saíram; a primeira versão (a do esboço) ficou guardada no projeto Unity.
- **A ordem das partes muda:** o mapa cresce aos poucos na cena nova, uma coisa por vez, quando o usuário pedir (as cidades, os lugares, as estradas menores, a ferrovia...), e cada coisa nova é aprovada antes da próxima. As Partes F2 a F9 continuam como a lista do que falta, não mais como a ordem.
- **A neblina:** na cena nova, ela liga e desliga (o N, ou o botão do F1), só na máquina de quem aperta. No jogo, a decisão 2 continua: a neblina de sempre.

## O pedido (como veio, 29/10/2026)

1. **O mapa vai ser fixo:** não vamos mais gerar pela semente. Isso facilita controlar a história e o andamento do jogo.
2. **Mudar todo o layout:** montanhas, lagos e rios, e mudar também como as cidades são construídas. Cidades com pontes.
3. **O mapa precisa crescer: 15 km.** A ideia do mapa grande é ter áreas no horizonte que não dá para acessar, e um grande lago.
4. Pensar junto antes de fazer.

## As referências

- **Os dois mapas ilustrados** (Blackwater City): a cidade grande no meio, cortada por um rio com pontes; o Silver Lake ao sul, com a Pine Island e outras ilhas; as montanhas nevadas no nordeste; as cidades médias e as vilas em volta; a base militar ao norte; as fazendas em retalhos a oeste e o brejo no sudoeste; a interestadual de leste a oeste, as rodovias estaduais com o número no escudo, as estradas secundárias e de terra e a ferrovia; a represa, a usina, a serraria, o parque industrial, a pedreira, o resort, a torre de vigia e a estação dos guardas florestais.
- **As fotos do noroeste dos Estados Unidos:** o lago parado com o pico nevado atrás (o Mount Rainier visto de um lago), o lago comprido entre as montanhas, os picos com a mata fechada de coníferas até a água e a rodovia passando pela floresta escura num dia fechado. É a região de Washington, nas Cascatas: a mata da M9a (os abetos e os pinheiros) já é essa.
- **A escala das referências:** pela régua de cada uma, o primeiro mapa tem uns 105 km de lado e o segundo uns 55 km. Em 15 km a composição cabe, mas comprimida de 4 a 7 vezes: **menos cidades, mais perto umas das outras**. O esboço segue o segundo mapa (o mais compacto), com os nomes dele e alguns do primeiro.

## O esboço (`Imgs/Mapas/MapaFixo_Esboco.png`)

O quadrado de 15 × 15 km, com o norte para cima, no Condado de Blackwater, em Washington (fictício). O quadrado tracejado branco no meio é o tamanho do mapa Grande de hoje (8 km), para comparar.

| | Hoje (Grande, 8 km) | O esboço (15 km) |
|---|---|---|
| Área | 64 km² | 225 km², dos quais ~141 km² jogáveis (dentro da quarentena) e ~84 km² de horizonte |
| Cidades | 3 grandes, 6 a 8 pequenas, 8 a 12 vilas | 1 grande (Blackwater City), 2 médias, 4 pequenas e 6 vilas |
| Lago | 1, de ~330 m | o Silver Lake, 6,8 × 4,5 km (~23 km² de água), com a Pine Island (1,5 km²) e 4 ilhotas, e o Clearwater Lake (pequeno, ao norte) |
| Rios | nenhum | 4: o Black River (11 km), o Clearwater Creek (4 km), o Gray Creek (5 km) e o Silver River (4 km) |
| Pontes | nenhuma | 14 no esboço (9 delas na Blackwater), 2 bloqueadas |
| Estradas | 67 a 76 km | ~85 km no esboço (16 de interestadual, 23 de estaduais, 35 do condado e 10 de terra), mais as de terra das fazendas e das cabanas: uns 100 km |
| Ferrovia | — | 16 km, de leste a oeste |
| Altura | serras de 60 a 136 m | o vale a ~100 m, o lago a 80 m, os morros das bordas de 200 a 400 m e os picos de 1.400 a 2.120 m (os do relevo do desenho, na F1) |

## O mapa, região por região

- **O nordeste, as Eagle Mountains (o horizonte):** a cordilheira em diagonal no canto, com os picos nevados (Gray Peak, 2.120 m; Pine Mountain, 1.740 m; Eagle Peak, 1.720 m; Mount Ash, 1.410 m, as alturas do relevo do desenho), a rocha das encostas e a neve acima de uns 1.300 m. **Fica toda fora do mapa jogável**: é o vulto no fundo, atrás da neblina (veja "A neblina"). Do centro da Blackwater, o Gray Peak fica a 6,8 km e sobe uns 17° acima do horizonte, quase o mesmo do Mount Rainier visto da foto do lago.
- **O centro, a Blackwater City:** a cidade grande (~3,7 × 2,3 km), no encontro do Black River (que vem das montanhas, do leste) com o Clearwater Creek (que desce do norte). O rio vira para o sul no meio da cidade e desce até o lago. A interestadual (I-90) atravessa a cidade de oeste a leste, e a ferrovia vai junto. Veja "As cidades".
- **O sul, o Silver Lake:** o grande lago, logo abaixo da cidade, com as cidades da margem: Lakeside (média, na foz do Black River, com a marina), Cedar Point (pequena, na margem leste), Granite Bay (vila, no sul) e Southhaven (pequena, no sudoeste). A Pine Island no meio, com a casa do lago (a Lake House) e mata fechada. O Lakeshore Road dá a volta no lago. A água sai pela represa (Silver Dam, no leste), que vira o Silver River e sai do mapa passando pela usina (Rivermoor Power Plant).
- **O oeste, as fazendas:** a planície com as lavouras em retalhos (Sunnyfield Farms e as outras, com o milharal e o trigal da M9d), Westview (pequena, na interestadual), Oak Valley (vila) e o posto de beira de estrada (Traders Rest). No sudoeste, o brejo (Misty Marsh: a água rasa, a taboa e as árvores mortas).
- **O norte, a mata:** a Pinecrest National Forest, fechada de coníferas, com o Clearwater Lake, a estação dos guardas (Ranger Station), as vilas de Pine Ridge, Maple Grove (na beira do riacho, 2 km acima do centro) e Evergreen, e a base militar (North Pine Military Base) colada na cerca, com a pista de pouso.
- **O leste, o pé da serra:** Riverton (pequena, no Black River, logo acima da cidade), a serraria (Cedar Mills, com o lago das toras), a vila de Stonewood no alto, Ashford (média, onde a I-90 sobe para o desfiladeiro da serra, o Ashford Pass, e sai do mapa), a torre de vigia (Gray Peak Lookout, numa crista do pé da serra, por estrada de terra: o ponto mais alto que se alcança), a pedreira (Graystone Quarry) e o Lakeview Resort na margem nordeste do lago.

## O relevo, a água e o horizonte

- **O relevo** sai do desenho (veja "Como fica fixo"), não de um ruído solto: a cordilheira por uma linha de crista com a altura e a largura, os picos por pontos, os morros por zonas com a amplitude de cada uma, a planície das fazendas achatada, e o ruído só por cima, para o detalhe. O chão das cidades e dos lotes continua aplainado como hoje.
- **Os vales dos rios:** cada rio cava o vale dele (mais fechado nas montanhas, largo na planície), e o leito fica abaixo da água. A água do rio desce de trecho em trecho, da nascente até o lago: rápida e rasa na serra, larga e quase parada na cidade.
- **As larguras:** o Black River de ~12 m nas montanhas a ~80 m na cidade (as pontes da cidade têm de 90 a 130 m); o Clearwater Creek de 14 a 30 m; o Gray Creek de 8 a 18 m; o Silver River de 40 a 45 m.
- **Os vaus** (sugestão): nos trechos rasos dos riachos, a água bate no joelho e dá para atravessar a pé (e de carro, devagar). O rio fundo só se atravessa pela ponte ou nadando.
- **O nado** (decidido em 29/10/2026): na água funda, o jogador nada devagar, na superfície, sem a arma na mão; os zumbis não entram nela (o NavMesh não cobre o fundo do lago nem o dos rios fundos), então a água vira refúgio. O carro que cai na água funda para e não sai mais de lá (sugestão).
- **O lago:** a água a 80 m, a margem com as prainhas de cascalho, as pedras e a mata chegando até a água (como nas fotos), as baías, a funda no meio (até uns 30 m), as ilhas, os píeres e a marina de Lakeside.
- **O que não dá para acessar:**
  - **a linha da quarentena** (a vermelha do esboço) fecha o mapa jogável. Ela não é mais um quadrado: segue o relevo. Onde dá para passar (os vales, as estradas, a margem), é a cerca da quarentena de hoje (o alambrado, as placas e as paredes invisíveis logo atrás); onde as estradas saem, os bloqueios de contêineres (4 no esboço: a I-90 a oeste e a leste, a 41 ao sul e a 41 ao norte, ao lado da base militar); no pé das montanhas, o paredão de rocha (mais inclinado do que o jogador e o carro sobem), com a parede invisível na crista;
  - **fora da linha**, o horizonte: as montanhas do nordeste, os morros baixos dos outros lados (a mata subindo, a Pinecrest no norte e as Ashford Hills no sudeste) e o rio que sai. Fora dela não há colisão, NavMesh, objetos nem construções: só o relevo de longe, para os vultos, o que deixa o horizonte barato;
  - **o fundo** (sugestão): depois dos 15 km, um anel de montanhas só desenhado (sem dados, de 15 a 40 km), para o horizonte nunca terminar numa borda reta.

## As cidades (e as pontes)

**Como a cidade é feita hoje** (M5): uma grade de ruas no giro da cidade, um pouco torta, e as zonas pela distância do meio (o centro com os prédios, o anel do comércio e os bairros de casas). Funciona, mas toda cidade sai redonda, em anéis, e não sabe o que é um rio.

**A proposta: a cidade em distritos.** Cada cidade é um conjunto de distritos desenhados à mão (o contorno, o tipo, o giro e o espaçamento das ruas), e o construtor enche cada um com as ruas e os lotes, como hoje. Os rios e o lago cortam a grade; as ruas que chegam na água viram a beira-rio (a avenida da margem, o cais, o parque) ou a ponte. As avenidas principais e as pontes são desenhadas; o resto é preenchido.

- **Os tipos de distrito:**
  - **o centro (downtown):** os prédios de 5 a 15 andares, as lojas no térreo e as praças;
  - **a rua principal (Main Street):** a rua das cidades pequenas, de prédios de tijolo de 2 a 4 andares com as lojas, o banco, a lanchonete e o cinema;
  - **a faixa comercial:** ao longo das rodovias, as lojas recuadas com o estacionamento na frente (o "strip mall"), o posto, o motel, a lanchonete e o mercado grande;
  - **os bairros de casas em grade** (os de hoje) e **os subúrbios** (as ruas curvas e os cul-de-sacs, as casas maiores);
  - **o industrial:** os galpões, o pátio da ferrovia, os silos e as docas na beira do rio (como caixas primeiro; veja a F7);
  - **a beira da água:** o parque da beira-rio, o cais, a marina com os píeres e os barcos parados (Lakeside e Cedar Point).
- **A Blackwater City, no esboço:** o Downtown no ângulo entre os dois rios (a margem norte do Black River e a leste do riacho, uma península: só se chega pelas pontes ou pelo norte), o Old Mill (os moinhos e os depósitos antigos na beira do rio, a leste), o Northside (bairros, do outro lado do riacho), o Westside (bairros a oeste do rio, com a I-90), o Southside (bairros ao sul) e o Riverside Industrial (o pátio da ferrovia e os galpões, no sudeste). **As pontes da cidade** (9 no esboço): a da I-90, a da 41 (a rua principal do centro), mais duas do centro, a da ferrovia, duas sobre o riacho, a do Old Mill e uma entre o Westside e o Southside. Duas delas ficam bloqueadas (os carros da fuga e a barricada do exército: não passa carro), e as outras viram os caminhos de carro até o centro.
- **As outras cidades:** Lakeside (a marina e a foz do rio, colada na Blackwater), Ashford (a cidade da passagem da serra, no Gray Creek), Riverton, Westview, Southhaven e Cedar Point (cada uma com a Main Street, um bairro ou dois e a faixa comercial na rodovia) e as vilas (uma rua ou duas, como hoje).
- **As construções:** as de hoje (as 24 variações de casa, os prédios, as lojas, o hospital e a mobília) servem como estão. As novas (a lanchonete, o motel, o strip mall, o galpão, a igreja, a escola, o posto de bombeiros, a delegacia) entram primeiro como caixas do tamanho certo e ganham o interior em fatias, depois (como a M5 e a M6 fizeram).
- **Os números esperados:** a Blackwater com uns 4 mil construções (a maior cidade de hoje tem 2.465), as médias com 600 a 900, as pequenas 250 a 400 e as vilas 20 a 60: uns 7 a 8 mil no mapa (o Grande de hoje tem uns 6 a 7 mil).

## As pontes

- **Os tipos** (feitos pelo jogo, pelo kit das construções, com a colisão e o NavMesh):
  - **a de concreto em vigas:** a da interestadual e as das avenidas, com os pilares no rio, a mureta, a calçada e os postes apagados;
  - **a treliça de aço** (a ponte antiga americana): a da rua principal e a da ferrovia, com as vigas cruzadas por cima;
  - **a pequena:** sobre os riachos, de concreto ou de madeira nas estradas de terra, e o bueiro onde o riacho é estreito;
  - **a da represa:** a estrada por cima do Silver Dam;
  - (sugestão) **a passarela** de pedestres e **o viaduto**, onde a I-90 passa por cima de uma estrada (com as rampas da saída, como nas interestaduais de verdade).
- **Os estados** (sem as explodidas, decidido em 29/10/2026): inteira ou **bloqueada** (os carros da fuga parados em fila, a barricada do exército no meio, um caminhão atravessado: não passa carro; a pé, passa-se pelo meio dos carros). Na história, uma ponte pode mudar de estado (sugestão: desbloquear uma é uma missão).
- **A altura:** o tabuleiro fica acima da água com folga (5 a 8 m nos rios grandes), com as rampas de acesso suaves; embaixo, o vão livre para o rio e, na margem, a rua da beira-rio passando por baixo.

## As estradas e as placas

- **Os tipos americanos:** a interestadual (a I-90: duas pistas de cada lado com o canteiro no meio, o acostamento e o guard-rail), as rodovias estaduais (a 41 e a 23, com o número no escudo branco), as estradas do condado e as de terra (as fazendas, as cabanas, a torre de vigia, a beira do lago). Nos lugares do esboço; o traçado fino segue o relevo como hoje (o A* entre os pontos desenhados).
- **As placas em inglês**, no padrão americano (verde com letra branca, como as de hoje): "Blackwater City 4", "EXIT 12 – Lakeside", o escudo da I-90 e o das estaduais, "Welcome to Ashford – Pop. 6,240" (riscada à mão, como hoje), o marco de milha e "Speed Limit 55". As distâncias em milhas (decidido em 29/10/2026); o velocímetro e a tela continuam em km/h.
- **A beira da estrada:** o que já existe (os carros abandonados, as blitz, os postos) e, na I-90 saindo da Blackwater (sugestão), o engarrafamento da fuga: os carros parados nas duas pistas por um quilômetro, até a ponte bloqueada.
- **Os bloqueios da quarentena** onde as estradas saem do mapa (os contêineres de hoje), e a cerca nos trechos em que dá para passar.

## Os lugares

Os do esboço, cada um com o nome e o lugar fixos:

- **North Pine Military Base:** a cerca, a guarita (ao lado do bloqueio da 41 ao norte), os alojamentos, o paiol, os blindados, o heliponto e a pista de pouso.
- **Silver Dam** (a represa, com a estrada por cima e a casa de máquinas), **Rivermoor Power Plant** (a usina, as torres de resfriamento que se veem de longe), **Cedar Mills** (a serraria e o lago das toras), **Graystone Quarry** (a pedreira, o buraco em degraus e as máquinas), **Riverside Industrial** (o pátio da ferrovia, os vagões parados e os galpões).
- **Gray Peak Lookout** (a torre de vigia de incêndio, o ponto mais alto que se alcança), **Ranger Station** (a estação dos guardas florestais), **Lakeview Resort** (as cabanas de aluguel e o píer), **Traders Rest** (o posto de caminhoneiros com o motel e a lanchonete), **Sunnyfield Farms** e as outras fazendas (o celeiro vermelho, o silo e a casa), **a Lake House** na Pine Island e **Misty Marsh**.
- **Os de antes, que continuam** (pelo `Plano_Mapa.md`): as comunidades aliadas e inimigas, as cabanas (algumas de preparador, com o bunker), os postos de gasolina, os bunkers espalhados e o laboratório (numa das cidades: proposta, o Riverside Industrial da Blackwater, atrás de um muro de empresa).
- **A casa da família:** em Maple Grove, a vila do riacho, 2 km ao norte do centro (decidido em 29/10/2026), com o bunker embaixo (na F9). A cidade grande fica perto, mas a vila é calma.

## Como fica fixo

A ideia da conversa de 27/10/2026 (veja "O mapa fixo" no `Plano_Mapa.md`), agora em detalhe. Quatro camadas:

1. **O desenho** (a fonte, feita à mão): um arquivo de dados no projeto Unity (`DesenhoDoMapa`, um ScriptableObject, que fica legível no git) com o que só uma pessoa decide: a linha da quarentena e os bloqueios; a cordilheira, os picos, as zonas de morro e a planície; o lago, as ilhas, os rios (os pontos, a largura e o nível da água) e a represa; as estradas principais (os pontos por onde passam, o tipo e o número) e as pontes (o tipo e o estado de cada uma); as cidades com os distritos e as avenidas; os lugares com os nomes; as zonas da mata (a nacional, o brejo, as fazendas, a serra). A primeira versão sai deste esboço, escrita por mim; depois, uma ferramenta do Editor mostra o mapa de cima e deixa mover os pontos (sugestão: arrastar na cena, e o mapa pintado se refaz).
2. **A construção** (no Editor, menu Zombie City Cleaner > Mapa fixo > Construir): o código do gerador de hoje, adaptado, enche o desenho: o relevo de 2 m, o traçado fino das estradas pelo relevo, as alturas do asfalto e o chão acomodado, os distritos com as ruas e os lotes, a planta de cada construção (o tipo, o tamanho, as variações), as placas, os carros, as manchas da mata e os nomes que faltarem. Pode levar um minuto ou dois: roda uma vez por mudança, não no jogo.
3. **O mapa congelado** (o que a construção grava): um arquivo binário versionado (`MapaFixo.bytes`, comprimido, pelo Git LFS, que o projeto já usa) com tudo o que a construção decidiu, cada coisa com um número que não muda (cada lugar, estrada, ponte, quarteirão, lote e construção), mais o mapa pintado e um relatório com os números. **Mudar o código do gerador não mexe no mapa** até alguém construir de novo; e construir de novo com o mesmo desenho dá o mesmo mapa (o sorteio continua, mas com a semente fixa, só para encher o que o desenho não diz).
4. **O jogo:** lê o mapa congelado no carregamento (sem gerar nada) e monta as malhas perto dos jogadores, como hoje (o terreno, as estradas, as pontes, as casas a partir da planta guardada, os móveis, o abandono, a mata e a grama). Mudar o kit das casas muda o visual das casas, mas não onde elas estão nem qual é a planta.

- **A camada da história** (por cima do congelado, num arquivo à parte): as marcas presas aos números (esta casa é a do fulano e está trancada; esta ponte explode na missão tal; aqui fica o bilhete; esta comunidade é a dos Abutres). Quando o mapa é construído de novo, ela confere se cada marca ainda acha o que marca e avisa as que perderam o alvo. As missões, as pessoas e o andamento vêm depois; o que entra agora são os números estáveis e o lugar para as marcas.
- **A rede:** o anfitrião não manda mais a semente nem o tamanho; manda a versão do mapa e o resumo do arquivo, e quem entra com outro mapa sai com a mensagem (como hoje com a versão do gerador).
- **O salvar** (da M10) guarda a versão do mapa e o que mudou nele (portas, saque, carros, gasolina, pontes). Durante o desenvolvimento, um mapa novo pode não abrir o mundo salvo de antes (como hoje).
- **O menu:** o "Novo mundo" fica só com o nome (sem o tamanho e a semente) e o "Continuar" (da M10). O F1 do mundo vai para os lugares pelo nome.

## O desempenho com 15 km

**O tamanho em si quase não pesa no jogo:** o que custa é o que está perto dos jogadores, e isso não muda (os blocos de 64 m, as casas de perto, a mata até 165 m). O que muda é o carregamento e a memória dos dados; com a neblina de hoje (a câmera corta a 150 m, e quase nada se vê além de uns 30 a 60 m), o horizonte é só a camada dos vultos.

| | Hoje (Grande, 8 km) | O mapa fixo (15 km) |
|---|---|---|
| Blocos de 64 m | 17 mil | 55 mil (~36 mil dentro da quarentena) |
| Alturas de 2 m | ~70 MB (floats, o mapa todo) | ~75 MB (16 bits, só dentro da quarentena e 200 m além); fora, uma grade de 16 m (~6 MB) |
| No carregamento | gerar tudo: ~6 s (no 15 km seriam uns 20 s) | ler o arquivo, sem gerar nada: de 1 a 3 s (a medir) |
| O arquivo congelado | — | uns 30 a 60 MB comprimido (a medir) |
| Construções | 6 a 7 mil | 7 a 8 mil |
| A precisão a 7,5 km do meio | — | 0,5 mm (a mesma ordem das quinas de hoje); a rede já manda as posições em floats inteiros (`UseHalfFloatPrecision` desligado no jogador e no carro). Não precisa mudar a origem do mundo. |

**O que carrega e até onde:**

| O quê | Até onde | Como |
|---|---|---|
| O relevo de perto, as estradas, as pontes e a água | ~180 m (como hoje) | os blocos de 64 m em 3 níveis, do mapa congelado |
| As construções | de verdade a 95 m, caixas até 180 m (como hoje) | o kit, pela planta guardada |
| A mata | 165 m (como hoje) | as instâncias da M9a |
| **Os vultos de longe** (novo, sugestão) | do fim da neblina ao horizonte (~15 km) | as montanhas do nordeste numa malha grossa (32 a 128 m entre os pontos), com a neve e a rocha pela cor, e os marcos altos em caixas simples, com a neblina própria deles; sem colisão |
| **O fundo** (sugestão) | de 15 a 40 km | o anel de montanhas, poucos milhares de triângulos |
| Colisão, NavMesh e zumbis | como hoje | nada muda |

- **A câmera:** continua cortando a ~150 m tudo o que é de perto; só a camada dos vultos vai até o horizonte.
- **O NavMesh** continua montado em volta dos jogadores (a M6 tirou as travadas dele). Assá-lo por bloco no mapa congelado é uma opção para depois, se for preciso medir de novo.
- **A meta:** os mesmos números de hoje (a travessia sem quadros acima de 33 ms); os vultos custam quase nada (umas dezenas de milhares de triângulos e poucas chamadas de desenho).

## A neblina (decidido em 29/10/2026: fica a de hoje)

- **A neblina pesada continua sempre**, de dia e de noite (o clima do Silent Hill: de dia, quase nada se vê além de 30 a 60 m). Não entra o clima mudando de dia para dia.
- **O horizonte, então, é um vulto** (sugestão): as montanhas do nordeste e os marcos mais altos (as torres da usina, a represa, a torre de vigia, os prédios mais altos do centro e a caixa d'água de cada cidade: os "marcos que furam a neblina" do `Plano_Mapa.md`) vão numa camada à parte, com uma neblina própria, bem mais fraca. Eles aparecem claros e apagados, sem detalhe, como uma sombra atrás da névoa: servem para se orientar e dão o tamanho do mundo, mas o caminho até eles continua escondido. O resto (a mata, as casas, as estradas) some na neblina como hoje.
- **De noite**, os vultos quase somem (só o recorte dos picos contra o céu, nas noites de lua).
- **Na luneta da sniper**, a neblina continua caindo para 35%, como hoje.
- (sugestão, depois) **A chuva**, que liga com a água da chuva do conceito (a fonte que se renova).

## O que continua e o que muda

- **Continua como está:** o kit das construções (as casas, os prédios, as lojas, o hospital e a loja do posto), os pontos dos móveis e a mobília, o abandono, as espécies da mata, a grama 3D e as lavouras, os modelos da beira da estrada, as placas (o sistema), os blocos e o streaming do terreno, a colisão, o NavMesh em volta dos jogadores, o mapa (M), os zumbis (os 250 em volta do jogador), o carro e a rede.
- **Muda:** o `PlanejadorDoMapa` (o sorteio dos lugares) sai: os lugares vêm do desenho; o `GeradorDoMundo` vira a construção (no Editor) e a leitura do congelado (no jogo); o `TracadoDaCidade` passa a trabalhar por distrito, cortado pela água; a `RedeDeEstradas` ganha as pontes; o relevo ganha os rios e o horizonte; a `Floresta` lê as zonas do desenho; o `PintorDoMapa` pinta no estilo das referências (sugestão); o menu perde o tamanho e a semente; os testes passam a usar o mapa fixo.
- **Decisões de antes que mudam** (do `Plano_Mapa.md`): "Rio: não, só o lago" (agora há rios); "a borda do mapa: serras em volta" (agora a cordilheira, os morros e a linha da quarentena pelo relevo); "os tamanhos Pequeno, Médio e Grande" e a semente (saem); os nomes brasileiros (já eram para sair: agora em inglês, os do desenho). "A casa no lago sem ponte, só nadando" continua (agora na Pine Island).
- **O que ficou para depois no `Plano_Mapa.md` continua para depois**, no mapa fixo: a quarta fatia da M6 (as especiais; aqui, a F7), o saque, a M7 (as portas), a M8 (o subsolo; o metrô agora só na Blackwater, 2 linhas curtas passando por baixo do rio) e a M10 (o jogo no mundo novo; aqui, a F9).

## Partes, em ordem

### Parte F0: as decisões e o esboço

- As respostas às perguntas de "Decisões" e o esboço acertado (as posições, os tamanhos e os nomes), ainda em imagem.
- **Pronto quando:** o esboço e as decisões estão aprovados e escritos aqui.
- **Estado (29/10/2026): feita e aprovada** ("Aprovar, ir à F1"): as 12 decisões respondidas (veja "Decisões") e o esboço acertado com elas (a casa da família em Maple Grove, as pontes bloqueadas no lugar das explodidas).

### Parte F1: o desenho em dados e o mapa pintado

- O `DesenhoDoMapa` no projeto Unity, com a primeira versão tirada do esboço aprovado.
- O mapa pintado a partir dele, em tamanho real (o relevo sombreado, a água, a mata, as lavouras, as cidades com os distritos e as ruas principais, as estradas, as pontes, a quarentena e os nomes), no Editor; sugestão: no estilo dos mapas das referências, com a legenda. (O M do jogo passa para a F2: até lá o jogo ainda roda o mundo sorteado, e o M mostraria um mapa que não é o do chão.)
- A ferramenta do Editor para ver o desenho de cima e mover os pontos.
- **Pronto quando:** o mapa pintado do desenho está aprovado (como a M3: acertar em imagem leva minutos; no 3D, dias).
- **Estado (29/10/2026): feita; antes da aprovação, a pedido, o desenho dela deu lugar ao do mapa limpo** (a F1b, logo abaixo). O que vem a seguir descreve a primeira versão (a do esboço), guardada em `Settings/MapaFixo/Antigos/`; as ferramentas (o desenho em dados, o relevo, as pontes, a pintura, a imagem e a janela do Editor) continuam e servem ao mapa limpo.
  - **O desenho** (`Settings/MapaFixo/DesenhoDoMapa.asset` do projeto Unity, o `DesenhoDoMapa`): tirado do esboço aprovado, com o que só uma pessoa decide: a linha da quarentena e os 4 bloqueios; o relevo (as 5 serras: a cordilheira das Eagle Mountains e os morros das 4 bordas; os 4 picos; a planície das fazendas; e, novo, **as passagens**: o desfiladeiro da I-90 na serra, o Ashford Pass, onde o relevo desce até 250 m, porque a interestadual subia por cima da cordilheira a ~900 m); os 2 lagos e as 5 ilhas; os 4 rios (a largura e a água da nascente à foz); as 17 vias (a I-90, as estaduais 41 e 23, as do condado e as de terra) e a ferrovia; as 13 cidades, com 26 distritos (a Blackwater com o Downtown, o Old Mill, o Northside, o Westside, o Southside e o Riverside Industrial, e 5 avenidas; as outras com a Main Street, o bairro, a marina de Lakeside e a faixa comercial de Ashford); os 11 lugares; as zonas da mata (a floresta nacional, as lavouras e o brejo); os nomes das regiões; e a casa da família em Maple Grove. Os pontos são os de controle: tudo é suavizado do mesmo jeito por quem usa.
  - **O relevo do desenho** (uma grade de 20 m dos 15 km, em ~1 s): os picos com 2.120, 1.740, 1.720 e 1.410 m; dentro da quarentena, nada acima de uns 850 m; os bloqueios num chão de carro (o da I-90 Leste a 250 m).
  - **As pontes** saem sozinhas, onde uma via cruza um rio: **19** (2 bloqueadas, a da Bridge Street e a da Lake Avenue, na Blackwater; a da Main Street em treliça; a estrada por cima da represa), com o tipo pela via e pela largura do rio.
  - **O mapa pintado** (Zombie City Cleaner > Mapa fixo > Imagem do mapa, em ~6 s): `Imgs/Mapas/MapaFixo.png` (4096 px nos 15 km, 3,7 m por pixel, e o painel da legenda) e `MapaFixo_Metade.png`. Os números dele: 141 km² jogáveis, 23,1 km² de lagos, 24,5 km de rios, 15,9 km de interestadual, 22,9 de estaduais, 35,4 do condado e 10,3 de terra.
  - **A janela do desenho** (Zombie City Cleaner > Mapa fixo > Desenho do mapa): o mapa pintado em outra thread (2048 px), com os nomes, e a camada escolhida em linhas: clicar escolhe, arrastar move o ponto, Shift + clicar põe um ponto, Ctrl + clicar tira, Alt + arrastar move o elemento inteiro (a cidade toda); os campos do escolhido do lado, e os botões de pôr, apagar, pintar de novo e gravar a imagem. Tudo vai para o Desfazer. Conferido por código (mover a cidade inteira e desfazer); arrastar com o mouse ainda não foi testado por ninguém.
  - **Conferido:** os 8 testes novos do Editor (`DesenhoDoMapaTestes`, 1,6 s) passam: a quarentena fecha o mapa do tamanho certo e os bloqueios ficam nela; as cidades, os lugares e a casa da família ficam dentro; os nomes não repetem; as estradas se ligam nas pontas; os rios chegam num lago, noutro rio ou na borda; as pontes do desenho acham o cruzamento delas; o relevo tem as alturas do plano (o fundo dos lagos abaixo da água, as ilhas acima); e a pintura sai igual duas vezes. O código do mundo sorteado não mudou (os testes de antes não foram rodados de novo).

### Parte F1b: o mapa limpo na cena nova

- O desenho do mapa limpo (veja "O mapa limpo"), o chão dele montado na hora numa cena nova, para andar a pé, de carro e voando, com a neblina ligada ou não, e medir.
- **Pronto quando:** o usuário anda pelo mapa (a pé e de carro), confere o relevo, as montanhas, os rios, os lagos, a mata, a quarentena e as estradas, e aprova.
- **Estado (29/10/2026): feita, esperando a aprovação do usuário.** Os detalhes estão no `CLAUDE.md` do projeto Unity ("O mapa fixo").
  - **O desenho** (`Settings/MapaFixo/DesenhoDoMapa.asset`): ~151 km² jogáveis; 11 lagos (25,4 km² de água: o Silver Lake com as ilhas e a Pine Island, e os lagos do norte), 47 rios (74,9 km, a água de cada um calculada pelo chão, descendo até o lago, o rio maior, o brejo ou a borda; os riachos que a imagem deixou soltos foram ligados ao rio ou ao lago mais perto, ou tirados), o brejo do sudoeste (o chão na altura da água, em manchas), as montanhas do leste e do nordeste (a altura pela mancha da imagem, amaciada, com as cristas por cima: até uns 640 m dentro da quarentena e uns 970 m no horizonte, em escala menor que a da imagem), e as 8 estradas vermelhas (a I-90 de oeste a leste, em duas, a estadual em volta do lago, as do noroeste e do nordeste, a diagonal e dois trechos de ligação: 15,5 km de interestadual e 40,6 km de estaduais), com os 2 bloqueios onde a I-90 sai e as 9 pontes onde elas cruzam os rios. A primeira versão (a do esboço) ficou em `Settings/MapaFixo/Antigos/DesenhoDoMapa_Esboco_F1.asset`.
  - **Na serra, as estradas vão pelos vales:** a montanha baixa em volta das estradas principais (o vale de passagem, como o Ashford Pass do esboço; por cima, a I-90 do leste subia a 818 m com rampas de 131%), e a rampa das estradas fica em 6% na interestadual e 8% nas estaduais. O vale dos rios abre mais na serra (estreito, ele virava um cânion de paredes a 70°).
  - **A cena** (`Scenes/MapaFixo.unity` do projeto Unity, menu Zombie City Cleaner > Mapa fixo > Montar a cena do mapa): o chão montado na hora em volta da câmera, em blocos de 64 m (mais detalhe perto, a colisão perto do jogador e do carro), a água dos lagos, do brejo e dos rios, as estradas com as faixas e as pontes, a cerca e a parede invisível da quarentena, os bloqueios de contêineres, a mata em instâncias e o carro na beira da I-90, onde a partida começa. Sem a neblina, o mapa inteiro aparece de longe (em cores sólidas) e a câmera vê até 16 km.
  - **Medido** (no Editor, voando a 2 m da I-90 a 110 km/h): com a neblina, 5,7 ms de média por quadro e nenhum acima de 33 ms; sem a neblina, 5,8 ms e o pior 32 ms (228 blocos e 2.843 plantas na vista); pelo vale da serra, 4,6 ms e o pior 23,5 ms; o chão sempre com a colisão embaixo, até chegando na cerca. As fotos da câmera do jogo estão em `Imgs/Renders/MapaFixo_*` (a partida com a neblina, as serras e o vale da I-90 sem ela); o mapa pintado do mapa limpo está em `Imgs/Mapas/MapaFixo.png` e `MapaFixo_Metade.png`.
  - **Conferido:** os 9 testes do desenho passam (a quarentena, os nomes, as estradas ligadas, os rios chegando em algum lugar e sempre descendo, as pontes, o relevo, a rampa das estradas e a pintura).
  - **O F1** tem o painel do mapa fixo: onde você está (a altura do chão e da água, a velocidade), a neblina (o N), voar (o F2), a régua (marcar um ponto e ver a distância, em metros e em milhas, e a diferença de altura até ele), ir para (as coordenadas, a partida, o meio, a margem dos 4 lagos maiores e os bloqueios) e trazer o carro.
  - **Ainda não tem:** a colisão dos troncos das árvores, o nado (a água não segura ninguém: anda-se pelo fundo), as estradas menores (as brancas e as laranja finas da imagem, como o laço do norte, onde a State Route 17 acaba) e as pontes de verdade (por enquanto, o tabuleiro, o guard-rail e os pilares). A água dos rios é a do lago (o material escuro do jogo): de cima, sem o reflexo do céu, ela fica quase preta.

### Parte F2: a fundação do mapa fixo (construir, congelar e carregar)

- A construção no Editor, o formato do mapa congelado (a versão, as partes e os números estáveis) e a leitura no jogo, sem gerar nada no carregamento.
- Os 15 km em blocos, a rede (a versão do mapa e o resumo) e o menu sem o tamanho e a semente; o F1 com os lugares pelo nome; os testes passando para o mapa fixo.
- O relevo ainda simples (o do desenho, sem os rios de verdade) e o conteúdo de hoje enchendo o desenho (as estradas pelo A*, as cidades como hoje), para o jogo funcionar de ponta a ponta desde já.
- **Pronto quando:** o anfitrião e um cliente entram no mapa fixo lido do arquivo, andam juntos de uma ponta à outra, e o carregamento, a memória e o tamanho do arquivo estão medidos aqui.

### Parte F3: o relevo e a água

- A cordilheira, os picos com a neve e a rocha, os morros, a planície, os vales e os leitos dos rios, a bacia do lago com as ilhas e as prainhas, e o paredão onde não se passa.
- Os rios (a água descendo de trecho em trecho, a correnteza no material, as margens, os vaus), o lago (a margem, a funda) e a represa; as texturas novas do chão (a areia e o cascalho da margem e a lama do brejo; a neve só nos vultos, que ficam fora do mapa jogável).
- A linha da quarentena pelo relevo: a cerca onde dá para passar, o paredão e as paredes invisíveis.
- O nado (na água funda: devagar, na superfície, sem a arma na mão, na rede para os outros verem) e a água funda fora do NavMesh.
- **Pronto quando:** dá para andar, dirigir e voar pelo mapa todo sem travar; os rios descem até o lago sem degrau nem fresta; ninguém sai da quarentena; o rio fundo só se atravessa pela ponte ou nadando; e os zumbis não entram na água funda.

### Parte F4: o horizonte na neblina

- Os vultos de longe (as montanhas do nordeste e os marcos altos, com a neblina própria deles), o fundo e o céu. Primeiro numa cena de testes, em intensidades lado a lado (como a M9b), para o usuário escolher o quanto eles aparecem; depois no mundo.
- **Pronto quando:** da cidade, do lago e da torre de vigia, as montanhas aparecem como vulto atrás da neblina, na intensidade escolhida; o resto fica como hoje; e o custo está medido aqui.

### Parte F5: as estradas e as pontes

- As estradas do desenho (a interestadual com o canteiro, as estaduais, as do condado e as de terra), as pontes (os tipos e os estados, com a colisão e o NavMesh), o viaduto (sugestão), as placas em inglês (as saídas, os escudos, as distâncias e o "Welcome to"), os marcos de milha, os bloqueios da quarentena e a beira da estrada (o engarrafamento da fuga, sugestão).
- O carro mais rápido: até uns 110 km/h (hoje 79), com o chão e a colisão chegando a tempo na frente dele.
- **Pronto quando:** dá para ir de carro de qualquer cidade a qualquer outra só pelas placas, atravessando as pontes; o zumbi e o jogador passam por todas as pontes (pelas bloqueadas, só a pé).
- **Depois, a segunda fatia: a ferrovia** (decidida em 29/10/2026): a linha, as pontes de trem, o pátio da Blackwater no Riverside Industrial, os vagões parados e um trem descarrilado no meio da mata. **Pronto quando:** dá para andar pela linha de ponta a ponta, atravessando as pontes de trem, sem cair nem prender, e o zumbi também.

### Parte F6: as cidades em distritos

Em fatias, cada uma aprovada antes da próxima:

1. **a Blackwater City:** os distritos, a grade de cada um cortada pelos rios, a beira-rio, as pontes da cidade, os lotes e as construções de hoje;
2. **as outras cidades e as vilas:** Lakeside (a marina), Ashford, a Main Street e a faixa comercial das pequenas;
3. **as construções novas** (a lanchonete, o motel, o strip mall, o galpão, a igreja, a escola, os bombeiros, a delegacia), primeiro como caixas do tamanho certo.

- **Pronto quando** (cada fatia): andar pela cidade passa a sensação certa (a cidade americana do rio), as ruas e as pontes se encaixam, e os testes das casas, dos prédios, das lojas e da passagem continuam passando.

### Parte F7: os lugares do mapa

- A base militar, a represa, a usina, a serraria, a pedreira, o pátio da ferrovia, a torre de vigia, a estação dos guardas, o resort, o posto de caminhoneiros, as fazendas, as cabanas, a casa do lago, as comunidades e o laboratório. Por fora primeiro (o que se vê de longe: as torres da usina, a represa, a torre de vigia), e o interior de cada um em fatias. É a quarta fatia da M6, no mapa fixo.
- **Pronto quando:** como nas fatias da M6, andar por dentro de todos sem cair, sem prender e sem buraco, e o zumbi chega em todos os cômodos.

### Parte F8: a mata e os biomas novos

- A mata pelas zonas do desenho (a floresta nacional fechada de coníferas, a serra com os abetos ralos subindo até a rocha, a beira dos rios, o brejo com a taboa e as árvores mortas, as fazendas), com as espécies da M9a; o abandono e a grama como estão.
- **Pronto quando:** as matas e os campos do mapa estão aprovados, sem cair o FPS (a travessia da M9a como referência).

### Parte F9: a camada da história e o jogo no mapa fixo

- As marcas da história (a ferramenta e o arquivo) e o salvar com a versão do mapa.
- O que era da M10: nascer no bunker da família (em Maple Grove), os carros, os itens, os pontos de horda, o salvar e o Continuar.
- O desempenho final, com 4 jogadores em pontas opostas dos 15 km.
- **Pronto quando:** dá para jogar vários dias no mapa fixo, sair e voltar com tudo igual, com os amigos.

## Decisões

### Respondidas em 29/10/2026

A 2 e a 11 pelo usuário; as outras dez com as recomendações deste plano ("Sim, as 10").

1. **A área jogável:** uns 141 km², como no esboço (uns 12 km de um lado ao outro); a cordilheira do nordeste e as bordas são o horizonte.
2. **A neblina:** fica a de hoje, sempre ("vamos manter a neblina"). O horizonte é o vulto das montanhas e dos marcos altos (sugestão; veja "A neblina").
3. **As cidades:** as quatro mudanças, na ordem da F6: a cidade em distritos, cortada pelos rios, com as pontes e a beira-rio; o jeito americano (a Main Street, a faixa comercial com o estacionamento na frente, o motel, a lanchonete); os subúrbios de ruas curvas e cul-de-sacs; e o industrial e as marinas.
4. **O gerador por semente sai do menu:** o código vira a construção do mapa fixo, e os mapas de 3, 5 e 8 km deixam de existir no jogo.
5. **As placas:** em inglês e em milhas, como nos Estados Unidos; o velocímetro e o resto da tela continuam em português e em km/h.
6. **O nado entra com a água** (F3): nadar devagar, sem a arma na mão, e os zumbis não entram na água funda (ela vira refúgio, e a Pine Island fica segura de verdade).
7. **A ferrovia:** sim, depois das estradas (a segunda fatia da F5): a linha, as pontes de trem e os vagões parados (o pátio da Blackwater e um trem descarrilado no meio da mata).
8. **O metrô** (a M8): só na Blackwater, 2 linhas curtas, passando por baixo do rio.
9. **A casa da família:** em Maple Grove, a vila do riacho, 2 km ao norte do centro da Blackwater, com o bunker embaixo (na F9).
10. **Os nomes:** os das referências, como no esboço.
11. **As pontes explodidas:** não ("não"). As pontes ficam inteiras ou bloqueadas.
12. **O carro:** a velocidade máxima sobe de 79 para uns 110 km/h (na F5).

### Ainda aberta

- **Nas cidades de hoje, tem mais alguma coisa que incomoda** (além do traçado em anéis, que a F6 troca)? Dá para responder até a F6.

## Riscos

- **Os vultos podem parecer um cenário pintado:** a F4 os testa numa cena, em intensidades, antes de irem para o mundo (e, se não ficarem bons, saem: o mapa funciona sem eles).
- **O arquivo congelado e o git:** cada construção nova do mapa grava um arquivo de dezenas de MB; pelo Git LFS ele não incha o repositório, mas cada versão conta na cota do LFS. Construir de novo só quando o desenho mudar.
- **Os testes:** os 81 testes de hoje geram mapas por semente (o Médio 7, o Grande 12345). Eles passam a ler o mapa fixo; os que medem coisas por cidade precisam de novos números.
- **A memória do Editor** (que já chegou a 9 GB numa sessão longa): os dados do mapa fixo são uns 150 a 250 MB; medir na F2.
- **A Blackwater é maior que a maior cidade de hoje** (uns 4 mil construções contra 2.465): o benchmark do centro precisa ser refeito lá, na F6.
- **O mapa grande pode ficar vazio:** com 141 km² e 13 cidades, há muito mato entre elas. Os lugares da F7, as cabanas, os postos e a beira das estradas enchem o caminho; se ainda faltar, dá para encolher a área jogável (a linha da quarentena) sem mexer no resto.
