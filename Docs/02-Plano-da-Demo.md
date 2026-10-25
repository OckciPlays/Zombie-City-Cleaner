# 02 – Plano da demo

O projeto é grande, então é feito em partes, com calma. Cada parte termina com algo que dá para ver ou jogar, e só se passa para a próxima quando a anterior está aprovada.

## A demo

### O que entra

- **Online desde já, com o Relay da Unity:** até 4 jogadores. Um hospeda e os amigos entram com um código.
- **A cidade:** pequena, com um prédio de 4 andares e 5 casas onde dá para entrar, cercada de pinheiros. Uma das casas é a da família, com o bunker embaixo.
- **O bunker:** dá para andar por ele: o salão alto, 4 quartos (um para cada um da família) e o depósito. Foi aumentado a pedido (veja abaixo).
- **Um dia completo:** sair do bunker, explorar, saquear lugares que não se renovam, matar zumbis, voltar, guardar tudo na despensa e dormir para passar ao próximo dia.
- **Dia e noite:** dia com neblina e noite com a vantagem da furtividade.
- **Zumbis:** lentos, desistem de seguir quando o jogador se afasta, e o tiro na cabeça explode a cabeça.
- **Salvar:** o anfitrião salva o mundo.
- **Objetivo da demo:** limpar a cidade.

### O que fica para depois

A família controlada pelo jogo, água da chuva, plantações, criação de itens, a expansão para a casa, as invasões de hordas, os veículos, as outras cidades e as comunidades.

## Como o online funciona

- **Quem roda a partida:** a máquina de um dos jogadores, o anfitrião. Ela roda os zumbis, o saque e o mundo. Os amigos se conectam pelo Relay, sem abrir portas no roteador. Para co-op de 4, isso basta.
- **Quem decide o quê:**
  - **movimento:** cada jogador controla o próprio personagem, para a resposta ser imediata;
  - **mundo e zumbis:** o anfitrião decide zumbis, dano, mortes, saque, inventário e o dia;
  - **tiro:** o jogador mira e atira na própria máquina, e o anfitrião confirma o acerto. Como é co-op, dá para ser tolerante com o atraso da rede.
- **Zumbis na rede:** não dá para mandar centenas de zumbis completos a cada quadro. Cada jogador só recebe os zumbis perto dele, numa taxa menor, e a máquina dele suaviza o movimento entre as atualizações.
- **Regra para a lógica do jogo:** nada de "o jogador" único ou `Camera.main`. A IA, o saque e o dia sempre pensam na lista de jogadores. (O `Camera.main` só aparece no que é visual e local, como o som abafado pela distância e as partículas em volta da câmera.)

## As partes, em ordem

Legenda: ✅ feita · 🔎 feita, esperando a aprovação · ⏭️ próxima · ⬜ por fazer. Os detalhes do que foi feito estão no [histórico](03-Historico.md).

### ✅ Parte 0: fundação do projeto Unity

- A branch `feature/demo-cidade` no mesmo projeto Unity (6000.6, URP, Input System, AI Navigation). O protótipo antigo ficou na branch dele.
- As pastas em `Assets/_Game/`, a cena `Demo_Cidade`, o URP com pós-processamento e Forward+, e os pacotes de rede ligados ao projeto na Unity Cloud.
- **Pronto quando:** a cena abre com noite, neblina e o pós-processamento base, e os pacotes de rede compilam.

### ✅ Parte 1: o zumbi no Blender

- O esqueleto padrão (`root > hips > spine > chest > neck > head`, braços e pernas com `.L`/`.R`), o desmembramento preparado na malha e as animações `Idle`, `Walk` e `Chase` (24 fps, sem root motion).
- **Pronto quando:** o FBX com as animações toca na Unity, na escala certa.
- **Mudou depois:** o zumbi dessa parte (o `NovoZombie`) foi trocado por 4 modelos novos nos ajustes antes da Parte 7. O esqueleto e as animações dele continuam sendo a base dos novos.

### ✅ Parte 2: o personagem do jogador e a pistola

- O corpo no estilo quadradão (o que os amigos veem), os braços em primeira pessoa e a pistola, com as animações dos dois e 4 variações de cor para a família.
- **Pronto quando:** os modelos e as animações tocam na Unity.
- **Mudou depois:** os braços e a pistola foram refeitos com as 4 armas.

### ✅ Parte 3: o online base

- Hospedar uma partida, ver o código e entrar com ele (menu simples).
- Os jogadores aparecem e andam juntos; cada um vê as próprias mãos com a arma (numa câmera separada, que não atravessa paredes) e o corpo dos outros.
- Andar, correr, olhar, pular e a lanterna, tudo sincronizado.
- **Pronto quando:** 2 a 4 jogadores se veem andando e atirando, sem zumbis ainda.

### ✅ Parte 4: a cidade em blocos, o clima e o dia

- A cidade montada primeiro com caixas (blockout): ruas, o prédio, as 5 casas, o bunker, carros, cercas e o anel de pinheiros com uma barreira invisível.
- **Clima:** a neblina do URP mais uma neblina de altura com ruído que se mexe, partículas no ar, pós-processamento (tonemapping, cores dessaturadas, vinheta, granulado e bloom) e eco nos ambientes fechados.
- **Dia e noite** controlados pelo anfitrião: dia com neblina pesada e clara; noite escura, com luar frio e postes de luz de sódio (alguns piscando) com cones de luz falsos na neblina.
- **Pronto quando:** andar pela cidade, de dia e de noite, já passa a sensação de medo.

### ✅ Parte 5: os zumbis na cidade

- **Comportamento** (decidido pelo anfitrião): vagar, perceber (visão que depende da luz, proximidade, passos correndo e o barulho do tiro), perseguir devagar, atacar e desistir.
- **LOD de IA:** perto (até 15 m, IA completa), médio (15 a 40 m, só vagar) e longe (mais de 40 m, o zumbi fica parado e não custa nada), com histerese.
- **Pool** (os zumbis são reaproveitados), **rede** só com os zumbis perto de cada jogador e um **benchmark** com 50, 100, 250 e 500 zumbis.
- **Pronto quando:** a cidade tem as hordas espalhadas, em rede, e o benchmark tem os números.

### ✅ Parte 6: combate

- Tiro por raycast do centro da tela, confirmado pelo anfitrião; pente e recarga; clarão, som e marca de tiro.
- Regiões do corpo com vida própria, a cabeça que explode, sangue e manchas no chão.
- **Mudou depois:** o desmembramento (braços e pernas caindo, zumbi rastejando) foi feito, mas saiu nos ajustes antes da Parte 7. A morte virou ragdoll.

### ✅ Parte 8: a arte final da cidade e do bunker

- **Veio antes da Parte 7, a pedido.** Os móveis de saque ganharam o modelo agora; o saque fica para a Parte 7.
- Em 6 fatias: a paleta e os objetos de rua; as casas; o prédio; o bunker e os móveis; o chão; e a oclusão com o benchmark.
- **Decidido:** a luz continua em tempo real (o dia é contínuo e os postes piscam, e luz assada não acompanha isso); LOD não foi preciso.
- **Pronto quando:** os blocos foram trocados pelos modelos finais, com o clima mantido.

### ✅ Ajustes antes da Parte 7

Pedidos depois da Parte 8, em 3 fatias:

1. **Som e nascimento:** o tiro novo, gerado em camadas; sem o som ambiente e a sirene; os jogadores nascem na sala comum do bunker.
2. **Zumbis:** 4 modelos novos com o mesmo esqueleto e as mesmas animações; só a cabeça explode; a morte com ragdoll.
3. **Armas:** pistola, rifle, submetralhadora e espingarda, com braços de luva novos, animações de cada uma e um suporte na parede do bunker (pegar uma arma deixa a da mão no lugar).

Depois dos testes: o cano das armas aponta para a mira, a lanterna não estoura mais os braços e o número de zumbis dobrou (258).

### ✅ Armas, sniper e desmembramento (antes da Parte 7, a pedido)

- **Armas rebalanceadas:** dano no tronco e nos membros por arma; a submetralhadora ficou igual à pistola por tiro (e mais rápida), o rifle um grau acima, e a espingarda muda com a distância (de perto mata com um tiro e arremessa o corpo; na média, dois).
- **A sniper:** de ferrolho, com luneta (12°), som do ferrolho e da luneta; um tiro no corpo mata e arranca um membro. Fica no painel de armas do bunker.
- **Desmembramento nos 4 modelos novos:** cada braço e cada perna cai (no cotovelo/joelho ou inteiro); sem uma perna o zumbi manca, sem as duas cai e rasteja.
- **Pronto quando:** as armas se comportam como pedido, a sniper acerta de longe pela luneta e os membros caem em todas as máquinas, sem perder desempenho.

### ✅ O bunker maior (a pedido, antes da Parte 7)

- **Planta:** o salão de 10 × 14 m com o pé-direito dobrado (5,3 m), 4 quartos e o depósito no fim de corredores curtos (3 m de pé-direito); a escada mais funda, em dois lances.
- **Painel de armas:** um lugar fixo para cada arma (as 4 longas e 4 pistolas), na parede do fundo do salão, com a bancada embaixo.
- **Porta blindada:** abre e fecha com E; fechada, isola o som nos dois sentidos, e os zumbis de fora não ouvem os tiros de dentro.
- **Luzes:** interruptores em cada cômodo, no salão e na escada. **Sem neblina e sem partículas** lá dentro.
- **Pronto quando:** o usuário aprova, e o teste com dois jogadores mostra a porta e as luzes iguais nas duas máquinas.

### 🔎 O começo do saque (a pedido, 24/10/2026; esperando o teste do usuário)

- **Mochila em grade (TAB)** de 9 × 6 espaços, e cada coisa ocupa os seus (a sniper 7 × 2, a lata 1). À esquerda, o boneco do jogador (gira com o mouse), a armadura (ainda vazia) e a barra de 3 atalhos; à direita, o nome, a foto e os detalhes do item; e uma aba de criar, ainda vazia. Arrastar move, põe num atalho ou larga (para fora); os ícones são coloridos.
- **A barra de 3 atalhos**, embaixo, no meio da tela: o atalho aponta para um item da mochila, que continua nela. A roda do mouse passa pelos atalhos, 1, 2 e 3 escolhem, G larga e E pega.
- **Menu do desenvolvedor (F1):** criar itens, contar, criar e matar zumbis.
- **Comida e água:** latas de comida e garrafas de água nas mesas, balcões, cômodas e criados-mudos das casas e dos apartamentos (34, e 2 na mesa do depósito do bunker). Com o item na mão, o clique come ou bebe (com a animação e o som).
- **Fome e sede:** duas barras que esvaziam com o tempo (mais rápido correndo); com pouca, a vida não volta sozinha; vazias, tiram vida.
- **Passos** com som de concreto, madeira, carpete ou grama (o piso de cada cômodo das casas), e o **menu de opções** no ESC (volume, limite de FPS e o FPS na tela).
- **Soco:** com a mão vazia, o clique soca (direita e esquerda); o zumbi recua a cada soco.
- **Correção:** correndo, a arma longa não mostra mais só uma ponta no meio de baixo da tela (e os braços voltaram a respirar, balançar andando e baixar correndo: estavam congelados no fim do sacar).
- **Depois do primeiro teste:** a porta do bunker não faz mais som (nem abrindo nem fechando), a roda do mouse passa pela mão vazia, os passos das casas foram refeitos por cômodo, o jogo não fica mais mudo depois de comer ou beber (e a mastigada foi refeita) e entrou o soco.
- **Pronto quando:** o usuário testa e aprova.

### 🔎 Bolsos, mochilas e o porte de armas (a pedido, 24/10/2026; branch `feature/mochilas`)

- **Fatia 1 (feita, esperando o teste):** começar só com os bolsos (4 × 2); as mochilas pequena (6 × 4), média (8 × 5) e grande (10 × 7) e o coldre, vestidos; a primeira arma pela alça (fora da grade), mais uma nas costas com mochila e uma curta no coldre; a tela do TAB com a alça e os lugares da mochila e do coldre.
- **Fatia 2:** no corpo, para todos (e no boneco do TAB): a mochila vestida, o coldre e as armas penduradas (a da alça no ombro, a das costas presa na mochila, a curta no coldre), menos a que está na mão.
- **Fatia 3:** mochilas e coldres espalhados pela cidade (a grande nos lugares mais perigosos).
- **Prévia da fatia 2 (feita):** o boneco de teste do F1 ("Boneco equipado"), com o `EquipamentoNoCorpo`: sem mochila, a arma da alça na diagonal das costas e a pistola no cós; com mochila, as armas presas nas laterais dela (a pedido, em vez do X nas costas); a curta no coldre do quadril direito.
- **O boneco do TAB (feito):** o retrato do inventário já mostra a mochila, o coldre e as outras armas, e a pose foi refeita (a pistola baixa numa mão; as armas de coronha no parado delas, com as duas mãos).

### 🔎 A munição (a pedido, 24/10/2026; branch `feature/mochilas`; esperando o teste)

- Cada arma usa um calibre: a pistola e a submetralhadora **9mm**, o rifle **5,56**, a espingarda **calibre 12** e a sniper **7,62**.
- As balas vêm em **caixas de até 60** (1 × 1 na grade, uma cor por calibre). A recarga tira as balas direto das caixas da mochila; sem caixa, não recarrega ("SEM MUNIÇÃO 9MM"). A caixa vazia some.
- Pegar uma caixa completa primeiro as do mesmo calibre; arrastar uma caixa em cima de outra junta as duas.
- O jogador nasce só com o pente da pistola (12). O pente vai junto com a arma (no chão, no painel do bunker e de volta).
- 16 caixas pela cidade (2 por casa e 1 por apartamento; 9mm é a mais comum), com as balas sorteadas a cada partida.

### 🔎 O carro que se dirige (a pedido, 24/10/2026; branch `feature/carro`; esperando o teste)

- Uma perua (4 lugares) na rua em frente à casa da família: E na porta entra (a porta abre, ele senta, a porta bate), E dentro sai.
- A partida é com a chave: olhar para a ignição (à direita do volante), clicar para pôr a chave e de novo para pegá-la, arrastar o mouse até a partida (ela pesa) e, com o motor de arranque girando, um quick time event num mostrador redondo: clicar quando o ponteiro passa nas marcas (a pequena liga na hora; as 3 grandes, todas acertadas). Só anda com o motor ligado. No volante: WASD, Espaço (freio de mão), L (faróis), B (buzina), V (câmera de trás), R (desvirar); F liga a lanterna também dentro do carro. Atropela zumbis. O painel acende com a ignição (velocímetro e gasolina), e a luz de teto acende com as portas.
- Na rede: quem dirige simula o carro; os outros veem.
- Depois: os zumbis atacarem quem está no carro (hoje eles alcançam o jogador através da lataria), o barulho do motor chamar zumbis, buzina, combustível, mais carros (e os carros abandonados com colisão melhor).

### ⏭️ Parte 7: o bunker, o saque e o dia

- **Bunker:** os quartos (camas para dormir e passar o dia), o depósito (o estoque da família, com a mesa para empilhar a comida) e o salão.
- **Saque:** armários, geladeiras, gavetas e carros com itens (munição, comida, água, remédio e materiais).
  - O que foi saqueado fica vazio para sempre.
  - A cidade é dividida em áreas, para o jogo saber o que já foi limpo.
  - Os móveis de saque já existem na cena, com o nome da malha (armário, cômoda, criado-mudo, geladeira, balcão e prateleiras).
  - Já feito (acima): a comida e a água soltas pelos tampos, que se pegam, largam e consomem.
- **Inventário:** a mochila de 3 lugares já existe (acima); falta a despensa compartilhada. A munição já vem das caixas espalhadas pela cidade (veja "A munição"); a despensa vai guardá-las também.
- **Dia:** sair, explorar, voltar antes de escurecer (ou arriscar a noite) e dormir. Dormir passa o dia e salva.
- **Salvar:** o anfitrião guarda o mundo (o que foi saqueado, os zumbis de cada área, a despensa e o dia). Sugestão: guardar também o conteúdo do painel de armas, a porta e as luzes do bunker, que hoje voltam ao inicial a cada partida.
- **Pronto quando:** dá para jogar vários dias seguidos, sair do jogo, voltar e encontrar tudo como estava.

### ⬜ Parte 9: o fechamento da demo

- **Objetivo:** limpar a cidade, com o número de dias mostrado no fim.
- **HUD:** mira, munição, vida, mochila e a hora do dia.
- **Menus:** hospedar ou entrar, e pausa (a pausa não congela o mundo, porque é online).
- **Sons:** passos em cada piso, a voz do zumbi e o tiro de cada arma.
- **Ajuste final de desempenho e de rede**, medido com o benchmark.

## Fases depois da demo

As ideias novas do Pedro para depois da demo (o Dia 0, as escolhas com vários finais, o carro, a floresta e os dois acampamentos) estão no [07 – Ideias do modo história](07-Ideias-do-Modo-Historia.md), divididas em frentes de trabalho. Os dois ainda vão decidir como elas se encaixam nas fases abaixo.

1. **Família:** criar os membros; quem não é jogado fica no bunker fazendo tarefas; os amigos assumem membros ao entrar.
2. **Sobrevivência que se renova:** água da chuva, plantações e criação de itens; fome e sede.
3. **Base:** do bunker para a casa (barricadas e defesas) e depois para a comunidade; hordas tentam invadir a cidade.
4. **Veículos.**
5. **Cidade limpa:** proteger as entradas.
6. **Mundo maior:** outras cidades, sobreviventes e comunidades com missões em troca de recursos.
7. **Rede mais robusta:** trocar de anfitrião e reconectar quem caiu.

## Sobre o sistema de zumbis "estilo Dead Rising"

- Não há material técnico oficial sobre como o Dead Rising fazia as hordas. O caso documentado mais próximo é a palestra da Ubisoft sobre o Assassin's Creed Unity: poucos NPCs com IA de verdade e milhares na tela, trocando o simples pelo completo sem o jogador perceber. É a mesma ideia do LOD de IA da Parte 5.
- **O que adotamos:** um gerenciador central, 3 níveis com histerese, pool, medir com o Profiler antes de otimizar e não prender a IA à câmera.
- **O que muda no nosso caso:**
  - o LOD já nasce no projeto, em vez de otimizar algo que existe;
  - no protótipo antigo, 500 zumbis custavam ~18 ms por quadro, quase tudo na física das hitboxes. Por isso as hitboxes completas só ficam ligadas no nível "perto";
  - zumbis lentos que desistem simplificam muito: ninguém atravessa a cidade atrás do jogador;
  - a IA roda só no anfitrião, e o nível de cada zumbi também decide se ele é enviado pela rede;
  - GPU Instancing não funciona com malha animada (SkinnedMeshRenderer). Para multidões enormes ao longe, a saída seria assar a animação numa textura. Com a neblina escondendo o que está longe, isso não foi necessário.
