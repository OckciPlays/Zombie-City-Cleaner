# O saque pelo mapa (plano, 01/11/2026)

O pedido ("vamos começar a trabalhar no sistema de loot pelo mapa"):
- **O saque é separado em categorias por cômodo:** a roupa só aparece no quarto, em cima da cama e no guarda-roupa, e assim por diante.
- **Cada item tem uma raridade** (normal, incomum, raro, épico e lendário), que muda a chance de ele aparecer.
- **Cada casa tem um mínimo e um máximo de itens:** nenhuma casa fica lotada, nenhuma fica vazia, e no mapa todo o saque é pouco.
- **O saque aparece nos móveis.**

É a etapa 5 do `Plano_Mobilia.md` ("o saque dentro do que abre") e a segunda fatia de "A mobília" do `CLAUDE.md`.

## O que tem hoje

- **Os itens** (o `Loot/Itens` do projeto Unity): as 5 armas de fogo, as armas brancas (a faca e o machado; o bastão, a lança e a tocha saem da fabricação), a comida (8), a bebida (8), os remédios (9), os materiais (13), os kits de montar (3), a lanterna e as pilhas, o galão, as caixas de munição (4 calibres), as mochilas (3), as peças do carro, a sinuca (a caixa de bolas e os 4 tacos) e as 73 roupas. Todos com o modelo, o ícone e a foto; a munição e o saque empilham (o `qtd`).
- **Os móveis do mundo:** cada cômodo tem a receita dele (o `MoveisDaObra.Novos.cs`), sorteada pela semente da construção: as mesmas em todas as máquinas, sem rede. Os móveis que guardam saque já estão marcados (`saque: true`): o rack, a estante, a mesa de canto, o criado-mudo, o guarda-roupa, a cômoda e a penteadeira, o baú, a mesa de escritório, os módulos da cozinha, a pia, a geladeira, a ilha, a pia e o espelho do banheiro, as estantes, a bancada e o freezer do porão e do depósito. A cama ainda não.
- **As portas, as gavetas e as tampas** abrem no E, pela rede (o anfitrião guarda as abertas numa lista de chaves); os móveis de perto só aparecem com alguém dentro da construção.
- **Os guarda-roupas, as estantes de aço e as prateleiras de madeira ficaram vazios** (a pedido, para o saque), e os armários de parede, o baú, o espelho com o armário e o arquivo já vinham vazios.
- **Os itens no chão** (o `ItensNoMundo`): uma lista só na rede, com todos os itens soltos (os que os jogadores largam, as compras do Dia 0). Serve para dezenas de itens, não para os milhares do mapa.
- **As peças dos carros** já são um saque do mapa: o que sobrou em cada carro sai da semente (igual em todas as máquinas), e só o carro mexido vai para a rede. O saque das casas segue o mesmo caminho.
- No mundo sorteado, **as casas estão vazias de itens** (só o F1 cria).

## A proposta

### 1. Onde o saque fica: os lugares em cada móvel

Cada móvel ganha os **lugares do saque**: retângulos onde um item pode ficar, **em cima** (à vista) ou **dentro** (atrás de uma porta, numa gaveta, embaixo de uma tampa: só aparece quando abre). Cada lugar sabe o tamanho dele, e só recebe o que cabe (o rifle não vai numa gaveta). Os de dentro vão presos à parte que abre: a gaveta leva o que tem dentro quando corre.

Os lugares são desenhados no Blender, junto do móvel (como as portas e as gavetas: `<móvel>.Saque.<n>`, com a parte que abre), e a montagem da mobília nova os lê.

| Móvel | Em cima | Dentro |
|---|---|---|
| As camas (casal, solteiro, beliche, colchão no chão) | o colchão | — |
| Os guarda-roupas (2, 3 e 4 portas) | — | o chão e a prateleira de cima, atrás de cada porta |
| As cômodas (3 e 6 gavetas) e a penteadeira | o tampo | cada gaveta |
| O criado-mudo | o tampo | a gaveta |
| O baú | — | o fundo |
| A mesa de escritório | o tampo | as 2 gavetas |
| Os racks | a prateleira aberta | o armário |
| A mesa de canto | o tampo | a gaveta |
| A mesa de centro | o tampo | — |
| As estantes de livros | os vãos das prateleiras | — |
| A lareira | o console e a laje da frente | — |
| Os módulos da cozinha (os armários de chão, o gaveteiro, o canto, a pia, a ilha) | o tampo | as gavetas e as portas |
| Os armários de parede (os fechados e os abertos) | as prateleiras dos abertos | as prateleiras dos fechados |
| A geladeira (a pequena e a grande) | — | as prateleiras e as da porta |
| O fogão | — | o forno e a gaveta |
| As mesas da cozinha | o tampo | — |
| As pias com gabinete (a simples e a dupla) | — | as portas e as gavetas |
| O espelho com o armário | — | as prateleiras de vidro |
| A bancada | o tampo e a prateleira de baixo | — |
| As estantes de aço e as prateleiras de madeira | cada tábua | — |
| O freezer horizontal | — | o fundo |
| A mesa de sinuca | o pano | — |
| O acampamento (o colchonete, o saco de dormir, as latas, o fogareiro e as garrafas) | em cima e em volta | — |

### 2. As categorias: o que aparece em cada cômodo

| O quê | Onde |
|---|---|
| **As roupas** | **o quarto**: em cima da cama, no guarda-roupa, nas gavetas da cômoda e da penteadeira e no baú; as pequenas (os óculos, as luvas, as máscaras e os chapéus) também no criado-mudo e no tampo da penteadeira |
| **As mochilas** | o quarto (o guarda-roupa e o baú) e o porão (as estantes) |
| **A comida** | **a cozinha** (os armários, as gavetas, a bancada e a mesa) e o porão (as estantes, as conservas, e o freezer); o salgadinho e o chocolate também na sala (a mesa de centro e o rack) |
| **A bebida** | **a cozinha** (a geladeira, os armários, a bancada e a mesa) e o porão (o freezer e as estantes, a água); o refrigerante e a garrafa de água também na sala |
| **Os remédios** | **o banheiro** (o espelho com o armário, o gabinete e as gavetas da pia); o remédio e os comprimidos também no criado-mudo; a atadura e os comprimidos numa gaveta da cozinha |
| **Os materiais** | **o porão e o depósito** (a bancada, as estantes e as prateleiras: a ferramenta, os pregos, a fita adesiva, os fios, as peças de metal, a corda, a madeira, o kit de reparo, a vara de pesca e a isca); a fita adesiva e os fios também nas gavetas da cozinha e na mesa de escritório; o pano no guarda-roupa, no baú e no gabinete do banheiro; as toras e a madeira na lareira |
| **A luz e o galão** | a lanterna e as pilhas no criado-mudo, nas gavetas da cozinha, na mesa de escritório e no porão; o galão no porão e no depósito (a tábua de baixo das estantes) |
| **As armas brancas** | a faca na cozinha (as gavetas e a bancada); o machado no porão (a bancada e as estantes) e na lareira |
| **As armas de fogo e a munição** | a pistola e a caixa de 9 mm no criado-mudo, na mesa de escritório e na prateleira de cima do guarda-roupa; as armas longas no guarda-roupa (a de cima ou o chão, a que couber), em cima da cama (desde a parte 3) e no porão (as estantes e a bancada), com as caixas do calibre delas |
| **A sinuca** | o porão com a mesa: a caixa de bolas e os tacos em cima dela ou numa estante (sem a caixa, as mesas do mapa não se jogam) |
| **O acampamento** | o saque de quem se escondeu ali: a comida, a bebida, os remédios, a faca, a lanterna, as pilhas, as caixas de munição e a mochila pequena |

**Não aparecem nas casas:** os kits de montar (a fogueira, a cama e a barricada), o bastão, a lança e a tocha (da fabricação), a "roupa" de desfiar (as 73 de verdade estão no lugar dela; veja a decisão 7), o copo de água (vem da água da chuva, depois), a caixa de bolas vazia, o coldre (saiu do jogo), as peças do carro (vêm dos capôs) e as chaves da perua e do cupê (ficam no bunker).

### 3. As raridades

**As chances** (de cada 100 itens sorteados): **normal 60, incomum 25, raro 10, épico 4 e lendário 1**; **desde 02/11/2026 (a pedido), normal 40, incomum 30, raro 15, épico 10 e lendário 5**. No Médio 7, com o triplo dos itens: normal 40,0%, incomum 30,6%, raro 15,5%, épico 9,4% e lendário 4,5% (2.274 lendários: 1.743 visões noturnas, 305 rifles e 226 snipers; a visão noturna, pequena, cabe em quase todo lugar e virou o segundo item mais achado do mapa). No mapa, um pouco menos dos raros que isso: um lugar que não tem nenhum item da raridade sorteada (a geladeira não tem nada lendário) desce para a de baixo.

**Os itens:**

| Raridade | Itens |
|---|---|
| **Normal** | a lata de comida, o feijão, o milho, a sopa, o salgadinho, a garrafa de água, o refrigerante (de garrafa e de lata), o suco, os comprimidos, a atadura, a gaze, o pano, a madeira, os pregos, a fita adesiva, os fios, a isca e as pilhas |
| **Incomum** | a carne, o chocolate, a barra de energia (era rara: veja a parte 3), o leite, o energético, a garrafa térmica, o remédio, o desinfetante, o spray antisséptico, a bandagem, a corda, as toras, as peças de metal, a ferramenta, a vara de pesca, a lanterna, o galão, a faca, a caixa de 9 mm, a mochila pequena, a caixa de bolas e os tacos |
| **Raro** | o kit médico, o antibiótico, o kit de reparo, o machado, a pistola, as caixas de calibre 12 e de 5,56 e a mochila média |
| **Épico** | a espingarda, a submetralhadora, a caixa de 7,62 e a mochila grande |
| **Lendário** | o rifle e a sniper |

**As roupas**, por grupo:

| Raridade | Roupas |
|---|---|
| **Normal** (o dia a dia) | a camiseta, a gola V, as regatas, o top, a camisa xadrez, a social, o moletom e o de zíper, o cardigã, o jeans, o jeans rasgado, a calça social, os tênis (o baixo, o de cano e o do piloto), o sapato social, o boné, o gorro, a bandana, os óculos de grau (o do piloto, o grosso, o redondo e o meio-aro), a máscara de pano, a cirúrgica e a gola tubular |
| **Incomum** (o trabalho e a rua) | as jaquetas com capuz, de lona e esportiva, a calça cargo, a de trabalho, a jardineira, o jeans com suspensório, as botas de trabalho, de couro e de caubói, o chapéu de caubói, o capacete de obra, o colete refletivo, o cinto de ferramentas, as luvas sem dedos, os óculos de sol (o quadrado, o redondo e o aviador) e o esportivo, a bandana no rosto, as golas camuflada e de caveira e o boné militar |
| **Raro** (os uniformes) | a jaqueta de couro, as camisas de uniforme (as duas) e a calça de uniforme, o boné da polícia, o quepe, o cinto da polícia, o jaleco, a camisa e a calça militares, as luvas táticas, os óculos de proteção, o respirador, a viseira e o tapa-olho |
| **Épico** (o militar e a proteção) | o colete tático, o cinto tático, o coturno, o capacete militar, os óculos táticos, a máscara de gás e a balaclava |
| **Lendário** | a visão noturna |

### 4. Quantos por casa

| Construção | Itens |
|---|---|
| Casa térrea | 1 a 3 (desde 02/11/2026: 3 a 9) |
| Sobrado | 2 a 4 (desde 02/11/2026: 6 a 12) |
| Com porão | mais 0 a 1, no porão (desde 02/11/2026: mais 0 a 3) |
| Apartamento (dos prédios e em cima das lojas) | 1 a 2 (desde 02/11/2026: 3 a 6) |

**O triplo** (02/11/2026, a pedido, testando a parte 4: "andei em várias casas e achei poucos itens, vamos aumentar para X3"): as quantidades e o máximo por cômodo (de 2 para 6) vezes 3. No Médio 7, 50.415 itens (5,15 por moradia: a térrea 6,1, com porão 7,5; o sobrado 9,0, com porão 10,5; o apartamento 4,5), com as raridades iguais (normal 59,9%, incomum 25,4%, raro 10,1%, épico 3,7%, lendário 0,9%) e só 4 moradias abaixo do mínimo (sem lugar para todos). Os números abaixo são os de antes.

Um item é uma pilha (6 pregos contam como 1). No Médio (1.706 casas na semente 7), uns 2,3 itens por casa: uns 4 mil nas casas, mais os dos apartamentos. Desses, uns 40 lendários (o rifle, a sniper e a visão noturna), uns 150 épicos e uns 400 raros, no mapa inteiro.

### 5. O sorteio de cada casa

Pela semente da construção, junto da distribuição dos móveis (na outra thread, como hoje):
1. a quantidade (entre o mínimo e o máximo da casa);
2. para cada item, **o cômodo** (pelo peso dele: o quarto 3, a cozinha 3, o porão e o depósito 2, o banheiro 2, a sala 1; no máximo 2 itens no mesmo cômodo, se a casa tiver outros com lugar), **o móvel** (pelo peso dele: o guarda-roupa e o acampamento mais que um armário de cozinha, para a cozinha de 8 módulos não engolir tudo) e **um lugar livre** nele (um item por lugar);
3. **a raridade** (as chances de cima; sem item dela no lugar, a de baixo);
4. **o item**, entre os daquela raridade que o lugar aceita e que cabem nele;
5. **a quantidade da pilha e o estado** (a decisão 6).

### 6. No jogo

- **Sem rede para o que está lá:** cada máquina sorteia o mesmo saque, como os móveis e as peças dos carros. **A rede leva só o que foi pego:** uma lista de chaves (a construção, o móvel, o lugar), escrita pelo anfitrião, como as portas abertas e os carros mexidos.
- **Só perto:** o saque aparece com os móveis de perto (com alguém dentro da construção). O de dentro só aparece com a parte aberta, e só se pega com ela aberta.
- **Pegar:** o E no item, como os do chão. O anfitrião confere a distância pelos dados do mundo (um cliente pode estar longe dele), se o item ainda está lá e se cabe na mochila; ele vai para a mochila, e a chave para a lista. Sem lugar, fica onde está, com a dica de hoje ("Sem espaço nos bolsos para …").
- **O que foi pego não volta** (os recursos que não voltam, do conceito). Largado (G), ele vira um item do chão, como hoje.
- **Salvar:** quando o mundo for salvo pelo anfitrião (ainda não existe), a lista dos pegos vai junto.

## As decisões

**Respondidas em 01/11/2026: as 8 pela recomendação** ("Aprovar, ir à parte 2").

1. **As chances das raridades:** normal 60, incomum 25, raro 10, épico 4 e lendário 1 (de cada 100). **Recomendação: essas.** A outra: mais raro ainda (70, 20, 7, 2,5 e 0,5).
2. **A raridade de cada item:** as tabelas da proposta (3). **Recomendação: essas**; mude as que quiser.
3. **Quantos por casa:** a térrea de 1 a 3, o sobrado de 2 a 4, o porão mais 0 a 1 e o apartamento de 1 a 2 (uns 2,3 por casa). **Recomendação: esses.**
4. **Como pegar o que está dentro dos móveis:**
   - **(recomendação)** à vista: abre a porta ou a gaveta, o item está lá dentro, e o E pega, um por um;
   - ou pela tela: o E no móvel abre o inventário com a grade do móvel ao lado (como as estantes do bunker).
5. **A raridade na tela:**
   - **(recomendação)** a cor da raridade no nome do item (nos detalhes do inventário e no "[E] Pegar") e um filete dela no canto da célula na mochila: o cinza, o verde, o azul, o roxo e o dourado;
   - ou só o nome da raridade nos detalhes, sem cor;
   - ou nada na tela (só a chance).
6. **O estado do que se acha:** **(recomendação)** as armas de fogo gastas (de 20 a 70% de desgaste; as épicas e as lendárias, de 0 a 30%) e com o pente vazio ou até a metade; as brancas de 10 a 60%; as caixas de munição com as balas da tabela de hoje (9 mm de 12 a 40, 5,56 de 10 a 30, calibre 12 de 6 a 18, 7,62 de 4 a 12); as pilhas do resto, pequenas (os pregos de 3 a 12, as pilhas de 1 a 4, a isca de 2 a 6, a madeira e o pano de 1 a 3; a comida, a bebida e os remédios, 1).
7. **As roupas de desfiar:** **(recomendação)** as 73 roupas de verdade também se desfiam em 2 panos (hoje só a "roupa" faz isso), e a "roupa" sai do saque.
8. **As lojas, o hospital e o posto** (com os móveis antigos, que não abrem: o saque iria nas prateleiras, nas gôndolas e nos balcões, cada loja com o dela: o mercado a comida e a bebida, a farmácia os remédios, a loja de armas as armas e a munição, a 24 horas e o posto a comida, a bebida, as pilhas e o galão):
   - **(recomendação)** numa parte depois (a 5), quando as casas estiverem aprovadas;
   - ou junto, já na parte 3.

## As partes

Uma de cada vez, cada uma aprovada antes da próxima.

1. **As decisões** (este plano).
2. **Os lugares do saque nos móveis:** no Blender, os lugares de cada móvel (em cima e dentro, o tamanho e a parte que abre), o `Mobilia_Nova.fbx` de novo e a montagem lendo os lugares. Na cena das variações, o mostruário mostra os lugares (uma tecla acende as caixas) e enche cada um com itens de teste, para ver o tamanho e se cabe. As fotos em `Imgs/Renders/Saque_Lugares_*`.
   - **O estado (01/11/2026): aprovada** ("Aprovar, parte 3"). Os detalhes estão no `CLAUDE.md` do projeto Unity ("Os lugares do saque nos móveis").
     - **Medidos na Unity, não no Blender:** as malhas dos prefabs da mobília nova são as mesmas do `Mobilia_Nova.fbx`, então o FBX não precisou ser refeito. Um gerador do Editor (Zombie City Cleaner > Mobília nova > Escrever os lugares do saque; a montagem da mobília nova também chama) acha em cada móvel as superfícies viradas para cima, mede quanto fica livre em cima de cada pedaço de 2 cm (um raio para cima) e descobre o que esconde cada um: a tampa em cima, ou a porta, a gaveta ou o forno na frente (um raio para a frente); é à vista quando nada cobre, ou quando a frente ou um lado fica livre (a prateleira aberta, a cama de baixo do beliche). Os maiores retângulos viram os lugares, com o tamanho, a altura livre e a parte que precisa abrir. O chão da gaveta só conta onde ela sai do móvel quando abre, e o que vai em cima no conjunto (a TV no rack, o computador na mesa, o abajur) tira o lugar embaixo dele. A tabela fica no `LugaresDoSaque.cs` do projeto Unity, que o gerador do mapa vai ler na parte 3.
     - **Os números:** 197 lugares em 54 móveis (os da tabela "Onde o saque fica"), por exemplo: a cama, 1 (o colchão inteiro); o guarda-roupa de 2 portas, o chão atrás de cada porta; o de 4 portas, 8 (o chão e as prateleiras do lado); a cômoda de 3 gavetas, o tampo e as 3 gavetas; a geladeira, 8 (as prateleiras, a gaveta de legumes, o congelador e as prateleiras da porta, que vão com ela); o fogão, o forno e a gaveta; o baú, o fundo; as estantes de aço, 4 tábuas.
     - **O que mudou da tabela do plano:**
       - **a prateleira de cima do guarda-roupa ficou de fora:** em ×1,30 ela fica a 2,05 m, e o olho do jogador fica a 1,62 m (o item não se vê nem se alcança direito); valem o chão dele e as prateleiras do lado (no de 3 e no de 4 portas);
       - **o alto demais:** sem nada em cima, até 1,55 m do chão (a cama de cima do beliche, a 1,76 m, ficou de fora: o item deitado não se vê); coberto (as prateleiras, o que fica atrás de uma porta), até 1,95 m (contando a altura dos armários de parede e do espelho);
       - **a mesa de sinuca ficou sem lugar:** as bolas rolam por cima do pano, e um item ali atrapalharia o jogo; a caixa de bolas e os tacos vão nas estantes do porão com a mesa (a parte 3);
       - nos armários de parede, só a prateleira de baixo (a de cima fica a mais de 2 m).
     - **No mostruário** (a cena das variações), o L troca: nada, as caixas (verde à vista, amarela dentro), as caixas com os itens de teste e só os itens. O item de teste de cada lugar é um que cabe nele no tamanho do jogo, do que o plano põe no cômodo (as roupas e as mochilas no quarto, a comida e a bebida na cozinha, os remédios no banheiro, os materiais, as armas e a munição no porão); ele vai com a gaveta e com a porta da geladeira quando abrem. Dos 197, 194 têm um item; nos 3 de 21 cm de largura do criado-mudo e da penteadeira não cabe nenhuma roupa (na parte 3, eles ficam para o que é pequeno: os comprimidos, as pilhas, os óculos).
     - **Conferido:** os testes do Editor (`LugaresDoSaqueTestes`: cada lugar dentro da caixa do móvel, com o tamanho mínimo e abaixo da mão; a parte de cada lugar de dentro existe e abre) e as fotos, com as portas, as gavetas e as tampas abertas (`Imgs/Renders/Saque_Lugares_Sala.png`, `_Quarto.png`, `_Cozinha.png`, `_Banheiro.png` e `_Servico.png`, com as caixas e os itens, e as mesmas com `_Itens`, só os itens; menu Zombie City Cleaner > Exemplos > Fotos dos lugares do saque). Não conferido no Play (as fotos são do Editor, com as partes abertas pelo mesmo código).
3. **As tabelas e o sorteio:** o catálogo do saque (a raridade, a quantidade e os lugares de cada item), o sorteio de cada casa e apartamento pela semente e o teste do Editor do mapa inteiro, com os números (quantos por casa, por raridade e por espécie, nenhuma casa vazia, nada fora do tamanho do lugar). No F1 do mundo, "Mostrar o saque" (uma caixa da cor da raridade em cada item, nas casas em volta).
   - **O estado (01/11/2026): aprovada** ("Aprovar, parte 4"). Os detalhes estão no `CLAUDE.md` do projeto Unity ("O sorteio do saque").
     - **O catálogo** (o `CatalogoDoSaque` do projeto Unity): os itens das tabelas daqui, com a raridade, a espécie (o que decide onde ele vai: a roupa, a comida, o lanche da sala, o remédio, a cabeceira, a oficina, a lenha, a luz, as armas...), a pilha achada e o estado; o que cada móvel aceita em cima, atrás das portas e nas gavetas; o peso de cada cômodo e de cada móvel; e quantos itens cada moradia tem. O tamanho de cada item (deitado ou em pé, como fica no chão do jogo) vem de uma tabela escrita pelo Editor a partir das malhas (147 itens, as 73 roupas com eles).
     - **O sorteio** (o `SaqueDaObra`), pela semente de cada construção, como os móveis (o mesmo em todas as máquinas): para cada moradia (a casa, ou cada apartamento dos prédios e das lojas), a quantidade; para cada item, **primeiro a raridade**, depois o cômodo (pelo peso; no máximo 2 no mesmo), o móvel e o lugar, entre os que aceitam um item dela que cabe neles, e por fim o item; a pilha, o desgaste e o pente. Na primeira versão o lugar vinha antes da raridade, e quase tudo descia de raridade (uma gaveta da cozinha não tem nada épico): o épico ficava em 1,5% e o lendário em 0,3%, com 1 rifle e nenhuma sniper no mapa.
     - **Os números** (o teste do Editor, o Médio 7 inteiro, em ~55 s): **9.792 moradias**, todas com lugar e nenhuma vazia, e **16.827 itens** (1,7 por moradia). As térreas têm 1,95 (2,52 com porão), os sobrados 3,00 (3,54 com porão) e os apartamentos 1,50; **os apartamentos são 8.086 das moradias** (os prédios do centro), então eles têm 12 mil dos itens. As raridades: normal 59,8%, incomum 25,4%, raro 10,4%, épico 3,6% e lendário 0,9%. Os cômodos: o quarto 6.860, a cozinha 4.461, o banheiro 3.192, a sala 1.632 e o porão 682. As armas no mapa inteiro: 165 pistolas, 62 submetralhadoras, 23 espingardas, 23 rifles e 12 snipers. Os mais achados: os comprimidos (920), a atadura (782), o refrigerante em lata (602), a gaze, as pilhas, o salgadinho e a água.
     - **O que mudou das tabelas, pelos números:**
       - **a barra de energia virou incomum:** rara, ela era o único raro da cozinha e da sala, e o raro mais achado do mapa (712, mais que a água);
       - **a cama aceita as armas longas** ("a espingarda largada em cima da cama"): com 1,2 m, a espingarda e a sniper só cabiam nas estantes grandes do porão, e saíam 1 de cada no mapa.
     - **No F1 do mundo,** "Mostrar o saque": uma caixa do tamanho e da cor da raridade (o cinza, o verde, o azul, o roxo e o dourado) em cada item das construções carregadas, com um palito de 60 cm para cima (o que está dentro de um móvel fechado aparece por cima dele), e no botão quantos de cada raridade. A foto: `Imgs/Renders/Saque_F1_Casas.png` (a casa da família e as vizinhas no Médio 7, de cima).
     - **Conferido:** os testes do Editor (`SaqueTestes`: o catálogo inteiro; no mapa, nenhuma moradia vazia, nenhuma acima do máximo, cada item no tamanho do lugar e no que ele aceita, o mesmo sorteio duas vezes dá o mesmo, e as raridades perto das chances) e, no Play (o Médio 7, hospedando local), o F1 nas casas em volta da da família (96 itens em 29 construções). Ainda nada aparece no jogo: os itens nos móveis e o pegar são a parte 4.
     - **Para a parte 4:** as roupas achadas vêm com as cores do catálogo (dá para sortear pelas receitas dos NPCs); as roupas grandes (as calças, de 93 cm, e as jaquetas) só cabem em cima da cama e no baú (dobradas, caberiam no guarda-roupa e nas gavetas); a rua do Dia 0 usa as casas do mapa e não pode ter saque.
4. **No jogo:** os itens nos móveis de perto (dentro das gavetas e das portas, indo com elas), pegar com o E pela rede, a lista dos pegos, a raridade na tela (a decisão 5) e as roupas desfiando (a decisão 7). Conferido no Play; depois, o teste do usuário com dois jogadores.
   - **O estado (02/11/2026): feito, esperando o teste do usuário com dois jogadores.** Os detalhes estão no `CLAUDE.md` do projeto Unity ("O saque nos móveis").
     - **Os itens nos móveis:** entram com os móveis de perto, como o item fica no chão (as armas deitadas de lado), no lugar do sorteio; o da gaveta, da prateleira da porta e da tampa vai com ela. O de dentro só se pega com a parte aberta.
     - **A mira** passa pela colisão do móvel e pela parte aberta dele até o item (sem isso, a caixa de colisão do forno e o gatilho da gaveta aberta ficavam na frente). Para fechar uma gaveta com itens, mire na frente dela.
     - **Pegar:** o E vai ao anfitrião, que acha o item pelos dados do mundo, confere a distância e se cabe, e põe na mochila (as armas com o pente e o desgaste do sorteio); a chave vai para a lista dos pegos, e o item some para todos. A pilha que só cabe em parte: o resto cai aos pés de quem pegou.
     - **A raridade na tela:** a cor no nome da dica ("[E] Pegar a barra de energia", o nome em verde), um filete em L no canto de baixo de cada célula da mochila e, nos detalhes, o nome na cor e ela escrita à direita. Vale para todo item do catálogo, venha de onde vier (a pistola do bunker aparece rara).
     - **As 73 roupas desfiam** em 2 panos, e **a rua do Dia 0 fica sem saque**.
     - **Conferido no Play** (o Médio 7, hospedando local): a barra de energia no forno da casa da família (a mira, a dica em verde, o E, a mochila e a lista; de volta à casa, ela não volta), a faca na gaveta do fogão de um sobrado (vai com a gaveta, e a gaveta que ficou aberta na rede volta aberta com a faca dentro), o moletom em cima da cama e a mochila com os filetes. O sorteio de uma construção leva uns 0,45 ms, uma vez, quando alguém chega perto. As fotos em `Imgs/Renders/Saque_Jogo_Forno.png`, `_Gaveta.png`, `_Cama.png` e `_Inventario.png`.
     - **Não conferido:** dois jogadores (o cliente pegando, o outro vendo sumir), a mochila cheia e a pilha que cabe em parte.
     - **O triplo** (02/11/2026, a pedido: "achei poucos itens, vamos aumentar para X3"): a quantidade de cada moradia e o máximo por cômodo vezes 3 (veja "Quantos por casa"); 50.415 itens no Médio 7, 5,15 por moradia. Conferido no teste do Editor do mapa inteiro; não conferido no Play.
     - **As raridades novas** (02/11/2026, a pedido): normal 40, incomum 30, raro 15, épico 10 e lendário 5 (veja "As raridades"). No Médio 7: 40,0%, 30,6%, 15,5%, 9,4% e 4,5%; 1.743 visões noturnas, 305 rifles, 226 snipers, 485 submetralhadoras, 138 espingardas e 931 pistolas. Conferido no teste do Editor do mapa inteiro.
5. **As lojas, o hospital e o posto** (a decisão 8).

## Fica para depois

- **A garagem com móveis:** hoje ela fica livre (o carro). Uma estante ou uma bancada no fundo de umas garagens traria o saque de garagem (as ferramentas, o galão, a munição).
- **Guardar os seus itens nos móveis do mapa** (como as estantes do bunker).
- **O saque nos mortos** (os bolsos dos zumbis e dos NPCs) e **nos carros** (o porta-luvas e o porta-malas; as peças do motor já vêm dos capôs).
- **O mapa fixo:** o mesmo sistema, com os lugares especiais da história por cima.
