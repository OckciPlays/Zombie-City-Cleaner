# Zombie City Cleaner: plano do jogo e da demo

O projeto é grande, então é feito em fatias, com calma. Cada parte termina com algo que dá para ver ou jogar, e só passamos para a próxima quando a anterior estiver aprovada.

## O jogo (visão completa)

- **Começo:** você e sua família vivem na cidade quando começa o apocalipse zumbi. Quando a notícia chega, todos se escondem no bunker embaixo de casa.
- **O bunker:** é o primeiro abrigo, com quartos, despensa e sala comum. Dá para andar por ele e gerenciar as coisas.
- **Explorar:** você sai do bunker para explorar a cidade infestada, coletar recursos, matar zumbis e limpar a cidade.
- **Recursos finitos:** o que foi saqueado não volta, então cada dia é preciso ir mais longe.
  - Com o tempo, entram fontes que se renovam: água da chuva, plantações e criação de itens.
- **Evolução da base:** bunker → casa → comunidade.
  - Em alguns momentos, hordas tentam invadir a cidade, e podem conseguir.
- **Veículos:** aparecem com o tempo e facilitam a exploração.
- **Cidade limpa:** quando a cidade estiver livre de zumbis, é preciso proteger as entradas dela.
- **Fora da cidade:** outras cidades, outros sobreviventes e comunidades. As comunidades pedem missões (limpar cidades, explorar lugares) em troca de recursos.
- **Online:** até 4 jogadores, e cada um é um membro da família.
  - Jogando sozinho, os outros membros ficam no bunker como personagens controlados pelo jogo, fazendo tarefas.
  - Quando um amigo entra, ele assume um deles.
- **Dia e noite:**
  - de dia há neblina pesada, e é a hora de explorar;
  - à noite você enxerga pouco, mas os zumbis também te enxergam menos, e dá para passar sem ser visto;
  - a lanterna é a troca: ligada, você enxerga e é visto de longe.
- **O mundo é salvo:** o que foi saqueado, os zumbis mortos de cada área e o estado da base.
- **Nome:** a corporação que contratava equipes de limpeza saiu do jogo. O nome continua, porque você limpa a sua cidade.

## Visual e clima

- **Arte:** low-poly simples e sem textura no cenário, com cores sólidas da paleta (no fim deste arquivo). Nada realista. O gráfico pixelado de PS1 foi abandonado.
- **Clima:** cidade sombria no estilo Silent Hill, com neblina, partículas no ar e luz de poste. É o que mais importa acertar.
- **Personagens:** o jogador e as armas no estilo quadradão.
- **Zumbis:** os 4 modelos cartoon (`Zombie1`, `ZombieHomem2`, `ZombieMulher` e `ZombieMulherLoira`), que têm textura. O `NovoZombie` das Partes 1 a 6 saiu.

## A demo (primeira fatia)

### O que entra

- **Online desde já, com o Relay da Unity:** até 4 jogadores. Um hospeda e os amigos entram com um código.
- **A cidade:** pequena, com um prédio de 4 andares e 5 casas, onde dá para entrar, cercada de pinheiros.
  - Uma das casas é a casa da família, com o bunker embaixo.
- **O bunker:** dá para andar por ele: o salão alto, 4 quartos (um para cada um da família) e o depósito. Foi aumentado a pedido (veja "O bunker maior").
- **Um dia completo:**
  - sair do bunker;
  - explorar;
  - saquear lugares que não se renovam;
  - matar zumbis;
  - voltar e guardar tudo na despensa;
  - dormir para passar ao próximo dia.
- **Dia e noite:** dia com neblina e noite com a vantagem da furtividade.
- **Zumbis:** lentos e desistem de seguir quando o jogador se afasta. Perdem membros, e o tiro na cabeça explode a cabeça.
- **Salvar:** o anfitrião salva o mundo.
- **Objetivo da demo (proposta):** limpar a cidade.

### O que fica para depois

A família controlada pelo jogo, água da chuva, plantações, criação de itens, a expansão para a casa, as invasões de hordas, os veículos, as outras cidades e as comunidades.

## Como o online funciona

- **Quem roda a partida:** a máquina de um dos jogadores, o anfitrião. Ela roda os zumbis, o saque e o mundo.
  - Os amigos se conectam pelo Relay, sem abrir portas no roteador.
  - Para co-op de 4, isso basta e não precisa de servidor dedicado.
- **Pacotes:** Netcode for GameObjects e o pacote principal de multiplayer da Unity (`com.unity.services.multiplayer`), que cria a partida, gera o código de entrada e liga o Relay.
  - Precisa do login anônimo dos serviços da Unity.
  - O projeto precisa estar ligado a um projeto na Unity Cloud. Isso você faz no Editor, com a sua conta.
- **Quem decide o quê:**
  - **Movimento:** cada jogador controla o próprio personagem, para a resposta ser imediata.
  - **Mundo e zumbis:** o anfitrião decide zumbis, dano, mortes, saque, inventário e o dia.
  - **Tiro:** o jogador mira e atira na própria máquina, e o anfitrião confirma o acerto. Como é co-op, dá para ser tolerante com o atraso da rede.
- **Zumbis na rede:** não dá para mandar 150 zumbis completos a cada quadro.
  - Cada jogador só recebe os zumbis perto dele, numa taxa menor e com interpolação (o movimento é suavizado entre as atualizações).
  - Membro arrancado, cabeça explodida e morte vão como eventos, uma vez só.
- **Regra para todo o código:** nada de "o jogador" único ou `Camera.main`. A IA, o saque e o dia sempre pensam na lista de jogadores.
- **Testes:** o Multiplayer Play Mode, com vários jogadores virtuais no mesmo Editor, mais uma build de verdade com um amigo.
- **Se o anfitrião sair, a partida acaba.** Trocar de anfitrião e reconectar ficam para depois da demo.

## Partes da demo, em ordem

### Parte 0: fundação do projeto Unity

- **Branch:** o mesmo projeto Unity (6000.6, URP, Input System, AI Navigation), numa branch nova, `feature/demo-cidade`.
  - O protótipo de PS1 fica guardado na branch dele, `feature/prototipo-fps-zumbis`, depois de commitar o que ficou pendente lá.
- **Pastas:**
  - `Assets/_Game/` com `Art/` (Characters, Weapons, Environment, Materials) e `Scripts/` por sistema (Network, Player, Zombies, Combat, Loot, World, Atmosphere, UI);
  - `Scenes/Demo_Cidade.unity`.
- **URP:** sem a tela pixelada, com pós-processamento (Volume), Forward+ e sombras só onde fizer diferença.
- **Rede:** os pacotes de rede instalados, e o projeto ligado à Unity Cloud (essa parte é sua).
- **Pronto quando:** a cena vazia abre com noite, neblina e o pós-processamento base, e os pacotes de rede compilam.

### Parte 1: o zumbi no Blender

Arquivo de trabalho: `Modelo 3D/NovoZombie_Anim/NovoZombie_Anim.blend`. O original em `Modelo 3D/NovoZombie/` não é modificado.

- **Rig** no padrão que já usamos:
  - a cadeia `root > hips > spine > chest > neck > head`;
  - os braços `shoulder/upper_arm/forearm/hand` e as pernas `thigh/shin/foot/toe`, com os lados `.L`/`.R`.
- **Desmembramento já preparado no rig:**
  - **Cortes:** a malha leva cortes no pescoço, nos ombros e nos cotovelos, com uma "tampa" de carne escondida em cada corte.
  - **Pesos nos cortes:** os pesos são 100% de um osso só. Assim, ao encolher o osso a zero no jogo, o membro some sem esticar o corpo.
  - **Pedaços soltos:** cada membro também vira uma peça separada, para cair no chão com física.
- **Animações** a 24 fps, em loop e sem root motion:
  - `Idle`: parado, balançando;
  - `Walk`: andar lento e arrastado, para quando o zumbi está vagando;
  - `Chase`: andar atrás do jogador, um pouco mais rápido, com os braços para a frente.
- **Pronto quando:** o FBX com as três animações toca na Unity, na escala certa.

### Parte 2: o personagem do jogador e a pistola

- **Modelos no Blender, no estilo quadradão e com as cores da paleta:**
  - o corpo inteiro, que é o que os amigos veem;
  - os braços em primeira pessoa;
  - a pistola.
- **Animações em primeira pessoa:** parado, andando, tiro (coice), recarga e sem munição.
- **Animações do corpo, que os outros veem:** parado, andar, correr e mirar ou atirar com a pistola nas duas mãos.
- **Variações de cor** para diferenciar os membros da família até o sistema de família existir.
- **Pronto quando:** os modelos e as animações tocam na Unity.

### Parte 3: o online base

- **Entrar no jogo:** hospedar uma partida, ver o código e entrar com o código. Há um menu simples para isso.
- **Os jogadores:**
  - aparecem no bunker ou na rua e andam juntos;
  - cada um enxerga as próprias mãos com a pistola, desenhadas por uma câmera separada, sem atravessar paredes;
  - cada um vê o corpo dos outros.
- **Controle em primeira pessoa:** andar, correr, olhar e pular, sincronizado na rede. A lanterna liga e desliga, e os outros veem.
- **Pronto quando:** 2 a 4 jogadores (no Multiplayer Play Mode e numa build) se veem andando e atirando, sem zumbis ainda.

### Parte 4: a cidade em blocos, o clima e o dia

Primeiro a cidade é montada com caixas cinzas (blockout), para acertar o tamanho das ruas, das casas, do prédio e do bunker. Logo depois vem o clima, porque é ele que decide as cores finais.

- **Layout:**
  - ruas em volta de um quarteirão central;
  - o prédio de 4 andares, com escada e apartamentos;
  - 5 casas, uma delas a da família, com o alçapão para o bunker;
  - o bunker;
  - carros abandonados e cercas;
  - o anel de pinheiros com uma barreira invisível.
- **Clima:**
  - **Neblina:** a do URP, mais uma neblina de altura com ruído que se mexe, feita num passe de tela cheia.
  - **Partículas:** cinza ou poeira em volta de cada jogador.
  - **Pós-processamento:** tonemapping, cores dessaturadas, vinheta, granulado e bloom.
  - **Som ambiente:** vento, barulhos distantes e sirene. Os ambientes fechados têm eco.
    - **Saiu nos ajustes antes da Parte 7:** o vento, os barulhos distantes e a sirene foram tirados a pedido. O eco ficou.
- **Ciclo de dia e noite**, controlado pelo anfitrião:
  - **Dia:** neblina pesada e clara.
  - **Noite:** escura, com luar frio e postes de luz de sódio, alguns piscando. Cones de luz falsos dão o efeito de luz na neblina.
- **Pronto quando:** andar pela cidade em blocos, de dia e de noite, já passa a sensação de medo.

### Parte 5: os zumbis na cidade (IA, LOD, pool e rede)

- **Comportamento**, decidido pelo anfitrião:
  - **Vagar:** o zumbi anda devagar.
  - **Perceber:** pela visão, pela proximidade e pelo barulho do tiro.
    - A visão depende da luz: à noite o alcance cai muito, e a lanterna ligada te torna visível de longe.
    - Passos correndo fazem barulho.
  - **Perseguir:** lento.
  - **Atacar:** quando chega perto.
  - **Desistir:** quando os jogadores ficam longe (por exemplo, a mais de 20 m) ou somem de vista por alguns segundos.
- **`ZombieManager`**, que comanda todos os zumbis:
  - registra os zumbis;
  - atualiza a IA em fatias, alguns zumbis por quadro, com um orçamento configurável;
  - mede a distância até o jogador mais próximo da lista.
- **LOD de IA**, com distâncias no Inspector e histerese (a distância para voltar de nível é um pouco menor que a de mudar, para o zumbi não ficar trocando de nível):

  | Nível | Distância do jogador mais próximo | IA | Navegação | Animação | Hitboxes | Rede |
  |---|---|---|---|---|---|---|
  | Perto | 0 a 15 m | completa, decide a cada ~0,1 s | NavMeshAgent com desvio de obstáculos | completa | ligadas | taxa alta |
  | Médio | 15 a 40 m | simples (vagar), decide a cada ~0,5 s | NavMeshAgent com desvio barato | com culling | cápsula simples | taxa baixa |
  | Longe | mais de 40 m | nenhuma ("zumbi virtual": posição e estado) | desligada | desligada | desligadas | não envia |

  - Um zumbi que está atacando ou levando tiro nunca cai de nível.
  - A neblina ajuda: o que está longe nem precisa ser desenhado.
- **Pool:**
  - os zumbis são criados aos poucos e reaproveitados;
  - ao voltar para o pool, vida, IA, Animator, agente e membros são zerados;
  - o ponto de nascimento é validado no NavMesh.
- **Medição:** Profiler Markers próprios e um benchmark com 50, 100, 250 e 500 zumbis, que mede o tempo de quadro, a animação, a navegação, os draw calls e o tráfego de rede.
- **Pronto quando:** a cidade tem as hordas espalhadas, em rede, e o benchmark tem os números.

### Parte 6: combate e desmembramento

- **Mudou nos ajustes antes da Parte 7:** o desmembramento saiu por enquanto (só a cabeça explode), e a morte virou ragdoll. O resto desta parte fica como registro.

- **Pistola:**
  - **Tiro:** raycast do centro da tela, com dispersão pequena. O anfitrião confirma o acerto.
  - **Munição:** pente e recarga. A munição de reserva vem do saque.
  - **Efeitos:** clarão, som e marca de tiro.
- **Regiões do corpo:** cabeça, tronco, braço e antebraço de cada lado e pernas. Cada membro tem vida própria (por exemplo, 3 tiros no braço).
- **Membro que cai:**
  - o osso encolhe a zero, e a tampa de carne aparece no toco;
  - a peça solta cai com física, saída de um pool;
  - o toco jorra sangue, e o chão ganha manchas (decals do URP);
  - o zumbi continua andando, e sem braço ele agarra menos.
  - Tudo isso é replicado como evento para os outros jogadores.
- **Cabeça:** o tiro explode a cabeça, e o zumbi morre.
- **Pernas (decidido na Parte 6):** também caem, no joelho ou a perna inteira. Sem perna, o zumbi cai e passa a rastejar, mais devagar.
- **Animações extras do zumbi:** ataque, morte, reação ao tiro, queda e rastejar (andando e parado).
- **Pronto quando:** atirar nos braços e na cabeça dá o resultado certo para todos os jogadores, sem queda de FPS.

### Parte 7: o bunker, o saque e o dia

- **Bunker:** os quartos (camas para dormir e passar o dia), o depósito (o estoque da família, com a mesa para empilhar a comida) e o salão.
- **Saque:** armários, geladeiras, gavetas e carros com itens (munição, comida, água, remédio e materiais).
  - O que foi saqueado fica vazio para sempre.
  - A cidade é dividida em áreas, para o jogo saber o que já foi limpo.
- **Inventário:** espaço limitado na mochila de cada jogador (já existe: 3 lugares, veja "O começo do saque"), e a despensa compartilhada.
- **Dia:** sair, explorar, voltar antes de escurecer (ou arriscar a noite) e dormir. Dormir passa o dia e salva.
- **Salvar:** o anfitrião guarda o mundo (o que foi saqueado, os zumbis de cada área, a despensa e o dia).
- **Pronto quando:** dá para jogar vários dias seguidos, sair do jogo, voltar e encontrar tudo como estava.

### Parte 8: a arte final da cidade e do bunker

- **Ordem mudada (23/10/2026):** a pedido do usuário, a Parte 8 vem antes da Parte 7. Os móveis de saque ganham o modelo agora; o saque, o inventário e o dia ficam para a Parte 7.
- **Fatias:**
  1. a base da arte (a paleta da cidade, o material único e o caminho Blender → Unity) e os objetos de rua: poste, carros, cercas, barreiras, lixeiras, placas, hidrante, banco, árvores e pinheiros;
  2. as casas, por dentro e por fora, com a casa da família;
  3. o prédio de 4 andares;
  4. o bunker e os móveis (os de saque, a cama, a mesa);
  5. o chão: ruas, calçadas, meio-fio, grama e entulho;
  6. a luz assada, os LODs e a oclusão, conferindo o clima e o benchmark.
- **Modelos no Blender, com as cores da paleta:**
  - o prédio de 4 andares e as 5 casas, por dentro e por fora;
  - o bunker;
  - postes, carros, cercas, lixeiras, placas, árvores e pinheiros;
  - os móveis onde se saqueia.
- **Um material só para quase tudo:** uma textura pequena de paleta, com um quadradinho de cor por tom, e o UV de cada face apontando para a cor dela. Fica "sem textura" na tela, mas com um material só, o que ajuda o desempenho (SRP Batcher e batching estático) e permite trocar uma cor no jogo todo de uma vez.
- **Luz:** iluminação de noite assada (lightmaps ou Adaptive Probe Volumes) para o cenário, e luz em tempo real só nos postes que piscam e nas lanternas.
  - **Decidido na fatia 6: a luz continua em tempo real.** O benchmark mediu 2,4 a 3,0 ms de GPU à noite, com os 28 postes, a lanterna e o SSAO.
  - O ciclo do dia é contínuo, e os postes acendem, apagam e piscam. Luz assada não acompanha nada disso: precisaria de dois cenários de APV misturados pela hora, para ganhar só a luz rebatida.
  - Fica como opção para depois da demo, se a luz de dentro das casas pedir.
- **Desempenho:** LOD Groups e occlusion culling.
  - **Feito na fatia 6:** oclusão assada (casas, prédio, bunker e chão escondem o que está atrás) e a neblina rala dentro das casas e do prédio.
  - **LOD não foi preciso:** os triângulos vêm quase todos dos zumbis, o plano de corte já é 150 m com neblina, e os móveis de dentro somem pela oclusão.
  - **Números (Editor, noite):** a cidade de arte tem 940 a 1.100 draw calls (a de blocos tinha ~2.460), a GPU gasta 2,4 a 3,0 ms e o quadro ficou nos mesmos 4,3 a 6,2 ms da Parte 5, preso na CPU. O histórico está em `Benchmarks/Benchmark_Zumbis.md`, no projeto Unity.
- **Estado (23/10/2026):** as 6 fatias foram feitas. Depois vieram os ajustes antes da Parte 7.
- **Pronto quando:** os blocos foram trocados pelos modelos finais, com o clima da Parte 4 mantido.

### Ajustes antes da Parte 7

Pedidos pelo usuário depois da Parte 8, em três fatias, cada uma aprovada antes da próxima:

1. **Som e nascimento:**
   - o tiro novo, gerado em camadas (estalo, soco grave, corpo, ferrolho, cauda e eco), em 3 variações;
   - sem o som ambiente e a sirene;
   - os jogadores nascem (e voltam quando caem) na sala comum do bunker.
2. **Zumbis:**
   - o zumbi atual troca pelo modelo com a mão menor (`Modelos 3D/Zombie1`), mais as variações `ZombieHomem2`, `ZombieMulher` e `ZombieMulherLoira`, todos com o mesmo esqueleto e as mesmas animações;
   - sem desmembramento por enquanto: só a cabeça explode;
   - a morte vira ragdoll (o corpo cai com física, sem animação pronta).
3. **Armas:**
   - pistola, rifle, submetralhadora e espingarda, no estilo quadradão, seguindo as imagens de referência do usuário;
   - braços e mãos novos, que seguram melhor a arma (luvas sem dedos, como na referência);
   - as armas ficam penduradas na parede do bunker: ao pegar uma, a que o jogador segurava vai para a parede.

- **Fatia 2, como ficou:**
  - os 4 modelos ganharam o esqueleto e as 5 animações do zumbi antigo (Idle, Walk, Chase, Attack, HitReact), cada um com as juntas nas medidas dele, por script (`Modelo 3D/Zumbis/zumbis.py`);
  - a cabeça é separada do corpo por um corte no pescoço, com a tampa de carne no toco; a explosão troca o corpo pela malha sem cabeça;
  - morto, o zumbi some e um corpo com física (11 partes e juntas com os limites do corpo humano) cai no lugar, empurrado pelo tiro. Os corpos vêm de um pool por modelo e somem afundando, como antes;
  - na rede, a morte leva a parte atingida e a direção do tiro (13 bytes por zumbi, 2 a menos que antes), e o modelo sai do Id, sem bytes a mais;
  - depois dos testes do usuário: o corpo que às vezes ia para o meio do mapa e o clarão do corpo sendo puxado (a interpolação, quando um corpo era reaproveitado) foram corrigidos; os corpos passaram a 12 por modelo, e o reaproveitado é o mais antigo fora da tela; com o braço levantado, a palma da mão fica para baixo.
- **Fatia 3, como ficou** (em duas partes: 3a, a arte no Blender, aprovada; 3b, as animações, a Unity e o suporte):
  - as 4 armas e os braços de luva sem dedos, por script (`Modelo 3D/Jogador/armas.py`); na primeira pessoa, cada arma tem o seu esqueleto e os seus braços. Nas longas, a mão esquerda pega o guarda-mão (ou a telha) por baixo, como na referência da AK;
  - animações de primeira pessoa de cada arma (parada, andando, correndo, tiro, gatilho sem bala, recarga e sacar), por código com IK. A espingarda bombeia no tiro e recarrega cartucho a cartucho;
  - no corpo (o que os outros veem): luvas, a arma da mão presa no osso da arma, e animações de arma longa para o rifle, a submetralhadora e a espingarda (o tronco gira para a direita, a coronha no ombro);
  - no jogo: cada arma com cadência, dano, pente, dispersão e coice próprios. O rifle e a submetralhadora são automáticos, e a espingarda solta 8 chumbos. Cada uma tem o seu som de tiro, e há os sons da bomba, do cartucho e do carregador;
  - o suporte de armas na parede da sala comum do bunker, com 4 vagas (começa com pistola, submetralhadora, rifle e espingarda). Mirando uma arma de perto, E troca pela da mão, e a da mão fica na vaga. Quem manda na troca é o anfitrião, e o conteúdo das vagas vai pela rede. A arma pega vem cheia; a munição de reserva fica infinita até o saque da Parte 7;
  - testado no Editor (troca, tiro e recarga das 4) e com um robô pela rede (os chumbos da espingarda e a arma do outro no corpo);
  - depois do teste do usuário: o cano passou a apontar para a mira (a arma ficava girada para o lado), a lanterna não ilumina mais os braços de primeira pessoa (estourava de branco) e a arma do corpo do dono não faz mais sombra no facho;
  - a pedido, o dobro de zumbis: de 129 para 258 (os mesmos 29 pontos de horda).
- **Estado (23/10/2026):** as 3 fatias foram feitas e aprovadas (commit `0d32276`, no GitHub). Próximo passo: a Parte 7.
- **Documentação para colaboradores:** a pasta `Docs/` do projeto Unity (no repositório) resume este plano, o que foi feito, a arquitetura, a arte e como colaborar. Ao mudar o plano aqui, atualize lá também.

### O bunker maior (pedido em 24/10/2026)

Pedido pelo usuário antes da Parte 7, com um desenho da planta e uma foto de referência do painel de armas.

- **Planta:** o salão (10 × 14 m) com o pé-direito dobrado (5,3 m), 4 quartos (um para cada um da família) e o depósito, cada um no fim de um corredor curto. Quartos e depósito têm 3 m de pé-direito, para o salão parecer grande.
  - A escada ficou mais funda: dois lances de 15 degraus com um patamar no meio, até o vestíbulo e a porta.
  - No salão: a mesa comprida com 6 cadeiras, o painel de armas com a bancada na parede do fundo (de frente para a porta), prateleiras, sofá e poltrona.
  - Nos quartos: cama de solteiro (um cobertor de cada cor), criado-mudo, armário, cômoda e tapete. No depósito: armários de aço, prateleiras, tambores e uma mesa onde, na Parte 7, a comida vai ser empilhada.
- **Painel de armas:** maior, de ripas, com um lugar fixo para cada arma (as 4 longas e 4 pistolas). A sniper saiu da mesa e foi para o painel. Ao pegar uma arma, a da mão volta para o lugar dela; quem sai da partida devolve a arma.
- **Porta blindada:** abre e fecha com E, para todos (o anfitrião decide; não fecha com alguém no caminho). Fechada, ela isola o bunker: o som não passa de um lado para o outro, e os zumbis lá fora não ouvem os tiros nem sentem quem está dentro.
- **Luzes:** um interruptor por cômodo, um no salão e dois na escada (no vestíbulo e na boca da escada, na cozinha), sincronizados na rede.
- **Clima:** dentro do bunker não há neblina nem as partículas do ar.
- **Estado (24/10/2026):** feito e testado no Editor, com um jogador (a troca das armas, a porta, as luzes, os caminhos do NavMesh e a neblina). Falta o teste com dois jogadores numa build e a aprovação do usuário.

### O começo do saque (pedido em 24/10/2026)

Pedido pelo usuário em 6 pontos, "faça tudo e depois eu testo":

1. **3 lugares de arma:** os 3 lugares embaixo, no meio da tela; o jogador leva até 3 armas e passa por elas com a roda do mouse (e 1, 2, 3). Pegar no painel do bunker vai para o inventário; com ele cheio, troca pela arma da mão; com a mão vazia na frente da vaga da arma, E guarda.
2. **Passos:** som de concreto, madeira (dentro das casas) ou grama, pelo chão embaixo do pé; os dos outros jogadores saem no pé deles.
3. **Fome e sede:** duas barras que esvaziam (a sede em 25 min, a fome em 40; mais rápido correndo). Com pouca, a vida não volta sozinha; vazias, tiram vida.
4. **A arma na frente da câmera correndo:** as armas longas corriam com o cano para baixo, e só uma ponta aparecia no meio de baixo da tela. Agora atravessam a tela inteiras. No teste, apareceu um bug antigo: os braços ficavam congelados no fim do sacar (sem respirar, balançar nem correr); corrigido na Unity.
5. **Comida e água:** latas de comida e garrafas de água (modeladas por `Modelo 3D/Jogador/itens.py`) nos tampos dos móveis das casas e dos apartamentos, e 2 na mesa do depósito do bunker. Cada uma ocupa um lugar; G larga e o clique, com ela na mão, come ou bebe.
6. **Opções no ESC:** volume, limite de FPS e mostrar o FPS na tela.

- **Estado (24/10/2026):** feito e testado no Editor, com um jogador. Falta o teste do usuário (e o com dois jogadores).
- **Depois do primeiro teste do usuário** (24/10/2026, 4 pedidos):
  1. A porta do bunker abre em silêncio (só o fechar e a batida têm som). No teste seguinte, saiu também o som de fechar: a porta não faz som nenhum.
  2. Os passos dentro das casas: cada cômodo soa como o piso dele no modelo (taco, carpete ou o piso frio da cozinha), e os quartos do bunker como taco. A madeira foi refeita (era um "tum" grave) e entrou o carpete.
  3. O jogo ficava mudo, só com um chiado, depois de comer ou beber: o som do ar da lata saía inválido (NaN, de um filtro instável) e calava o áudio da Unity inteiro. Corrigido, e a mastigada refeita.
  4. O soco: com a mão vazia (um lugar vazio), os punhos ficam em guarda (`Modelo 3D/Jogador/maos.py`) e o clique soca, direita e esquerda; o zumbi recua a cada soco, e 8 matam. Depois, a roda do mouse passou a parar na mão vazia também (se houver um lugar vazio).
  - Feito, testado no Editor e aprovado pelo usuário (commit `53a6004`).
- **A mochila em grade e o menu do desenvolvedor** (24/10/2026, a pedido, com um esboço da tela):
  - **F1 (desenvolvedor):** criar itens (as armas, a lata e a garrafa, na mochila ou no chão), ver quantos zumbis há, matar todos e criar N em volta do jogador. Só no Editor e nas builds de desenvolvimento.
  - **TAB (a mochila):** uma grade de 9 × 6 espaços, e cada item ocupa os seus (a sniper 7 × 2, a lata 1; sem pilhas). Clique mostra à direita o nome, a foto e os detalhes; passando o mouse, o nome aparece ao lado. Arrastar move, põe num atalho ou, para fora da janela, larga no chão; G larga o escolhido. Há uma aba de criar, ainda vazia. Fecha com TAB ou o X.
  - À esquerda, o boneco do jogador em 3D (gira arrastando), os lugares da armadura (ainda vazios) e, embaixo dos pés, os 3 atalhos: o atalho só aponta para o item, que continua na mochila.
  - O fundo é o cinza do chão do bunker, com a borda escura com rebites. Os ícones foram refeitos coloridos (`itens.py`), e a barra do HUD usa os mesmos.
  - Com a tela aberta, o jogador fica parado (escolha do usuário).
  - Depois do primeiro olhar do usuário: sem as legendas (sobrou "G: largar o escolhido"), sem o friso amarelo da borda e sem o atalho nos detalhes; a roda passa pelos 3 atalhos, vazios ou não; e 1.000 zumbis na cidade (eram 258).
  - Girar: segurando um item na mochila, R gira ele 90°; o que é pego entra girado se só couber assim. Embaixo da grade, a dica "R: girar item", abaixo da "G: largar o escolhido".
  - Feito e testado no Editor, com um jogador. Falta o teste do usuário.
- **Para depois (Parte 7):** a comida e a água não são salvas; o saque dentro dos móveis (armários, geladeiras, gavetas), a munição de reserva, a despensa e dormir.

### Parte 9: o fechamento da demo

- **Objetivo:** limpar a cidade, com o número de dias mostrado no fim.
- **HUD:** mira, munição, vida, mochila e a hora do dia.
- **Menus:** hospedar ou entrar, e pausa (a pausa não congela o mundo, porque é online).
- **Sons:** passos em cada piso, a voz do zumbi (`Sons/SomZombie1.mp3`) e o tiro de cada arma.
- **Ajuste final de desempenho e de rede**, medido com o benchmark da Parte 5.

## Fases depois da demo

1. **Família:**
   - criar os membros;
   - quem não é jogado fica no bunker como personagem do jogo, fazendo tarefas;
   - os amigos assumem membros ao entrar.
2. **Sobrevivência que se renova:** água da chuva, plantações e criação de itens; fome e sede.
3. **Base:**
   - do bunker para a casa (barricadas e defesas), e depois para a comunidade;
   - hordas tentam invadir a cidade e podem conseguir.
4. **Veículos.**
5. **Cidade limpa:** proteger as entradas.
6. **Mundo maior:** outras cidades, sobreviventes e comunidades com missões em troca de recursos.
7. **Rede mais robusta:** trocar de anfitrião e reconectar quem caiu.

## Sobre o sistema de zumbis estilo Dead Rising

- **O que achei:** não encontrei material técnico oficial sobre como o Dead Rising fazia as hordas.
  - O caso documentado mais próximo é a palestra da Ubisoft sobre Assassin's Creed Unity. Lá, só 40 NPCs tinham IA de verdade e 120 tinham modelo de alta resolução, mas a cena mostrava milhares, trocando o NPC simples pelo completo sem o jogador perceber.
  - É a mesma ideia do LOD de IA da Parte 5.
- **O que vale do texto do ChatGPT:**
  - o gerenciador central;
  - os 3 níveis com histerese;
  - o pool;
  - medir com o Profiler antes de otimizar;
  - não prender a IA à câmera.
- **O que muda no nosso caso:**
  - **O prompt foi escrito para otimizar um sistema que já existe.** Como vamos começar do zero, o LOD já nasce no projeto.
  - **Já medimos o gargalo.** No teste antigo, 500 zumbis custavam ~18 ms por quadro, quase tudo em física das hitboxes nos ossos. Por isso as hitboxes completas só ficam ligadas no nível "perto".
  - **Zumbis lentos que desistem simplificam muito.** Ninguém atravessa a cidade atrás do jogador, então não precisamos de flow field nem de rota longa para centenas de zumbis.
  - **A IA roda só no anfitrião.** O nível de cada zumbi também decide se ele é enviado pela rede (interest management).
  - **GPU Instancing não funciona com SkinnedMeshRenderer.** Para multidões muito grandes ao longe, a saída seria assar a animação numa textura (Vertex Animation Texture) e desenhar instanciado. Com a neblina escondendo o que está longe, isso provavelmente não vai ser necessário na demo.
- **Meta de quantidade:** ~100 a 150 zumbis espalhados pela cidade, com uns 20 a 40 no nível "perto" ao mesmo tempo. O benchmark vai até 500 para sabermos o limite real. Não dá para prometer 60 FPS antes de medir.

## Paleta de cores (proposta)

Cores um pouco mais claras do que parecem, porque a noite e a neblina escurecem tudo. Ajustamos na Parte 4, já com a luz.

| Uso | Cor |
|---|---|
| Asfalto | `#3A3D42` |
| Calçada e concreto | `#8A8A84` |
| Faixas e meio-fio desbotados | `#B8B29A` |
| Tijolo do prédio | `#7A4A3C` |
| Casas (madeira pintada) | `#6E7B6A`, `#8C7B6A`, `#5E6B7A` |
| Telhados | `#4A3B38`, `#3E4A52` |
| Batentes e portas | `#D8D2C0`, `#5A4636` |
| Vidro escuro | `#1E2A30` |
| Janela acesa (rara) | `#E8B86A` |
| Concreto do bunker | `#6A6A64` |
| Pinheiros (folhas e tronco) | `#2F3D33`, `#4A3A2E` |
| Grama e terra | `#4B5240`, `#5A4B3C` |
| Metal dos postes | `#2E3134` |
| Luz de sódio dos postes | `#FFB46B` |
| Luar | `#8FA6C8` |
| Neblina de noite | `#2A3036` |
| Neblina de dia | `#9AA0A0` |
| Sangue e ferrugem | `#6B0F0F`, `#8A4B2E` |
