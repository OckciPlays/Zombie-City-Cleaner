# As portas e as janelas das casas

O pedido (02/11/2026): "toda casa vai ter portas possíveis de abrir; algumas portas vão estar trancadas e outras
destrancadas; as trancadas, ao tentar abrir, vão fazer um barulho e dar uma tremidinha na porta, como se estivesse
tentando abrir; vai dar para arrombar as portas com lockpick ou pé de cabra; as janelas vão ter o mesmo sistema". O
usuário saiu e pediu para eu trabalhar nisso: **as decisões abaixo foram tomadas por mim, pela recomendação de cada uma,
com ele fora**. Todas podem mudar; as partes ficam "feitas, esperando a aprovação".

## Como é hoje

- **As portas** das casas do mundo (`Casa` e `Obra.FolhaDePorta`, no kit das construções do projeto Unity) são parte da
  malha fixa da casa: a folha é desenhada numa posição (as de fora fechadas ou entreabertas, de 15° a 40°; as de dentro
  abertas, de 85° a 100°; a da frente aberta em umas casas) e entra na colisão quando passa de 60°. Nada abre.
- **As janelas** das casas são buracos sem vidro (a pedido: o vidro opaco tapava a vista de dentro), e o abandono
  (`AbandonoDaObra.Janelas`, a Parte M9c) põe em umas as tábuas ou o compensado (uns 35%), em outras o vidro sujo
  quebrado com um buraco (uns 45%) ou inteiro (uns 5%); o resto (uns 15%) fica aberto. Pula-se pelas abertas e pelas
  quebradas (`PuloDaJanela`); as tapadas e as de vidro inteiro têm colisão.
- **Os móveis** que abrem (a mobília nova) já usam o que as portas vão precisar: a parte que gira (`ParteQueAbre`), a
  mira (um gatilho na camada 2, fora do NavMesh) e a lista das abertas na rede (o anfitrião escreve, pelas chaves).
- **Não há** gazua nem pé de cabra no jogo.

## As decisões

**Tomadas por mim pela recomendação em 02/11/2026, com o usuário fora.**

1. **Onde:** as casas do mundo sorteado (as térreas e os sobrados). Os apartamentos dos prédios e das lojas, o hospital
   e o posto ficam para depois (uma parte só deles). A rua do Dia 0 e as cenas de exemplo continuam com as folhas paradas.
2. **Quais portas:** todas as de folha: a da frente, as dos fundos e do lado, a do porão de fora, a da garagem para dentro
   e as de dentro. O portão da garagem e as passagens (os vãos sem folha) ficam como estão.
3. **Como começam** (pela semente da casa, a mesma em todas as máquinas): as de fora (a da frente, as dos fundos e do
   lado e a do porão de fora) fechadas três em cada quatro, e as outras entreabertas (15° a 40°) ou abertas; a da garagem
   (de dentro da casa) fechada uma em cada três; as de dentro abertas (85° a 95°, como hoje), uma em cada cinco fechada.
   **Trancadas** (só as fechadas): a da frente 60%, as outras de fora 40%, a do porão de fora e a da garagem 30%; das de
   dentro, as do quarto e do banheiro 30% (as outras de dentro nunca).
4. **Destrancar por dentro:** a porta de fora trancada se destranca por dentro, sem ferramenta (o trinco): "[E]
   Destrancar e abrir". As de dentro trancadas, dos dois lados, só arrombando. *(A outra: as de fora também só
   arrombando.)*
5. **Trancar:** os jogadores não trancam por enquanto (fica para a defesa da base: as chaves e as barricadas).
6. **A tremida e o barulho:** o E numa porta trancada faz a folha tremer uns 0,45 s (um ou dois graus, indo e voltando,
   e a maçaneta) com o som da maçaneta girando e do trinco batendo. Todos ouvem (pela rede); os zumbis não. Quem tentou
   passa a ver "Trancada" na dica (com o que dá para arrombar, se tiver).
7. **Arrombar:** segurando o E numa porta que se sabe trancada, com o item na mochila; uma barra embaixo da mira mostra o
   tempo, e soltar o E ou sair de perto cancela.
   - **A gazua:** 4 s, sem barulho; uma em cada quatro quebra (some da pilha).
   - **O pé de cabra:** 2 s, com o barulho da madeira estalando, que chama os zumbis num raio de uns 25 m (o tiro chama
     de mais longe); a fechadura fica quebrada.
   - Com os dois, a gazua (o silêncio). Arrombada, a porta fica destrancada para sempre (a lista da rede) e abre.
8. **Os itens:** a **gazua** (o estojo com as hastes) em pilha (até 5; achada de 1 a 3), incomum, nas gavetas do quarto
   (a cabeceira), da cozinha, do escritório e da oficina; o **pé de cabra**, incomum, 2 células, na oficina e no porão (a
   bancada, as prateleiras e as estantes). Sem desgaste por enquanto. O pé de cabra como arma branca (na mão, com o
   golpe) fica para depois.
9. **Os zumbis e as portas:** a porta fechada barra o zumbi (um obstáculo no NavMesh, no anfitrião); ele não abre nem
   arromba (as hordas arrombando ficam para depois). O tiro para na porta fechada (a camada nova "Portas": bate no tiro e
   no jogador, e fica fora do NavMesh).
10. **As janelas:** as de vidro inteiro ficam muito mais comuns e viram janelas que abrem: das de fora, as tapadas
    continuam (uns 35%), as quebradas com buraco ficam em uns 20%, as abertas sem vidro em uns 10%, e **as de vidro
    inteiro, fechadas, em uns 35%**: duas folhas de vidro sujo numa moldura, abrindo para fora como portas (abertas,
    pula-se por elas). Trancadas: metade. Destrancam por dentro (o trinco), e de fora se arrombam como as portas (a
    gazua e o pé de cabra). Quebrar o vidro (o golpe, o tiro) fica para depois.

## As partes

Uma de cada vez, cada uma aprovada antes da próxima (com o usuário fora, elas ficam feitas, esperando a aprovação).

1. **As decisões** (este plano). *Escritas em 02/11/2026, com o usuário fora.*
2. **As portas que abrem:** no kit, as portas das casas viram pontos (a dobradiça, a largura, a altura, a cor, o lado e
   o giro do começo), e a folha só é desenhada parada fora do mundo (o Dia 0 e as cenas de exemplo); a mobília deixa livre
   o giro inteiro de cada folha. No mundo, cada casa de verdade ganha as folhas dela (a malha, a colisão na camada nova,
   o gatilho da mira e, no anfitrião, o obstáculo do NavMesh quando fechada); o E abre e fecha pela rede (o anfitrião
   guarda o que mudou). Conferido no Play e nos testes da passagem dos zumbis.
3. **As trancas:** o sorteio de quais estão trancadas, a tremida e o barulho pela rede, a dica "Trancada" e destrancar
   por dentro.
4. **A gazua e o pé de cabra:** os modelos, os ícones e as fotos (o `loot.py` do projeto Unity), os itens no jogo e no
   saque, e o arrombar (segurar o E, a barra, o barulho do pé de cabra, a gazua que quebra).
5. **As janelas:** as de vidro inteiro mais comuns, as duas folhas que abrem para fora, as trancadas, destrancar por
   dentro e arrombar; pular só pelas abertas.

## O estado (02/11/2026): as partes 2 a 5 feitas, esperando a aprovação e o teste do usuário com dois jogadores

Os detalhes do código estão no `CLAUDE.md` do projeto Unity ("As portas e as janelas das casas").

- **As portas (a parte 2):** no kit, as folhas das casas do mundo viram pontos (`PortaDaObra`, com a dobradiça, a largura, a
  altura, a cor, o lado e o giro do começo; o Dia 0 e as cenas de exemplo continuam com as folhas paradas), e o chão do
  giro inteiro de cada folha fica livre de móveis. No jogo, cada casa de verdade ganha as folhas dela, aos poucos (12 por
  quadro): a folha com as duas almofadas e a maçaneta, na cor da porta e com o abandono, a colisão numa camada nova
  ("Portas", a 12: para o jogador, o tiro e a visão dos zumbis, e fica fora do NavMesh) e, no anfitrião, o obstáculo do
  NavMesh quando fechada. O E abre (para dentro do cômodo, a 90°) e fecha, pela rede: o anfitrião guarda só o que mudou.
  Os sons são gerados no jogo: o rangido de abrir e a batida de fechar.
- **As trancas (a parte 3):** o E numa trancada faz a folha tremer e o som da maçaneta e do trinco, para todos; quem tentou
  passa a ver "Trancada" na dica, com o que dá para arrombar. Por dentro, a porta de fora trancada mostra "[E] Destrancar
  e abrir a porta" e abre sem ferramenta.
- **A gazua e o pé de cabra (a parte 4):** os modelos, os ícones e as fotos no `loot.py` do projeto Unity (a gazua: o estojo
  de couro com três hastes e o tensor; o pé de cabra: vermelho, com o gancho em J, a unha em V e a ponta chata), os itens
  no jogo e no saque (os dois incomuns). Segurar o E numa trancada que se sabe trancada mostra uma barra embaixo da mira:
  a gazua leva 4 s, sem barulho, e uma em cada quatro quebra (o aviso "A gazua quebrou na fechadura"); o pé de cabra leva
  2 s, com o som da madeira forçando e rachando, e chama os zumbis até 25 m. Com as duas, a gazua.
- **As janelas (a parte 5):** nas casas do mundo, das janelas de fora, as tapadas continuam (uns 35%), as quebradas com o
  buraco ficam em uns 20%, as abertas sem vidro em uns 10%, e o resto (uns 35%) vira a janela que abre: duas folhas de
  vidro sujo no caixilho, abrindo para fora até 100°; uma em cada sete começa aberta, e metade das fechadas trancada.
  Fechada, ela fecha o vão (não se pula, o tiro para); aberta, pula-se por ela. Por dentro, o trinco destranca; de fora, a
  trancada treme e se arromba como as portas.
- **Conferido no Play** (o Médio 7, hospedando local, com os zumbis parados): em volta da casa da família, 187 portas (123
  abertas, 22 trancadas; das 64 de fora, 41 fechadas) e 94 janelas (14 abertas, 36 trancadas), e os obstáculos do NavMesh
  só nas portas fechadas. A porta da frente da casa da família, trancada: de fora, o E fez a folha tremer e a dica virou
  "Trancada · segure [E]: arrombar com a gazua (sem barulho)"; arrombada com a gazua, ela abriu para dentro. Uma porta da
  frente trancada, por dentro: "[E] Destrancar e abrir a porta", e abriu. Uma janela fechada: de fora, "[E] Abrir a
  janela", e o pulo bloqueado; aberta, as duas folhas para fora e o pulo liberado. Uma janela trancada: tremeu, e o pé de
  cabra a abriu. **O NavMesh:** numa casa com todas as portas de fora fechadas, o caminho da rua para dentro fica cortado
  (PathPartial); com a da frente aberta, completo (PathComplete).
- **Os testes do Editor** (com o giro livre das portas, os móveis das casas mudaram de lugar): passaram os dos móveis
  (`MoveisTestes`, os 7), o da passagem dos zumbis (`PassagemTestes`: 2.756 construções, nenhum cômodo fechado pelos
  móveis) e os do saque (`SaqueTestes`: 50.408 itens no Médio 7, com 695 gazuas).
- **Não conferido:** dois jogadores (o outro vendo a porta abrir e tremer), o segurar o E com o teclado (a barra e o som
  repetindo; no teste, o fim do segurar foi chamado direto), a gazua quebrando, os zumbis indo atrás do barulho do pé de
  cabra (estavam parados) e o desempenho com as folhas (umas 300 por casa de verdade em volta: não medido).
- **As fotos:** `Imgs/Renders/Portas_Trancada.png` (a dica depois de tremer), `Portas_Arrombada.png` (aberta com a gazua),
  `Portas_Janela_Fechada.png`, `Portas_Janela_Aberta.png` e `Portas_Janela_Trancada.png`; os ícones e as fotos dos itens em
  `Assets/_Game/Art/UI/Icones/Icone_Gazua.png`, `Foto_Gazua.png`, `Icone_PeDeCabra.png` e `Foto_PeDeCabra.png` do projeto
  Unity.

## Fica para depois

- As portas dos apartamentos (os prédios e as lojas), do hospital e do posto.
- Trancar por dentro (as chaves, as barricadas e a defesa da base).
- O pé de cabra como arma branca; quebrar o vidro da janela; arrancar as tábuas de uma janela tapada com o pé de cabra.
- Os zumbis forçando as portas (as hordas).
- Salvar: quando o mundo for salvo pelo anfitrião, o que mudou nas portas e nas janelas vai junto.
