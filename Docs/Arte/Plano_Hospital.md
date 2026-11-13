# O Hospital Principal (plano, 07/11/2026)

O pedido (o "prompt mestre" do usuário, com as 4 plantas conceituais): começar a dar característica às cidades com construções
específicas e detalhadas, a primeira delas o **Hospital Principal**, um hospital americano de porte médio, abandonado há anos,
com o clima de Silent Hill: 4 pavimentos exploráveis (o térreo do atendimento e da emergência, o 2º andar da internação e do
isolamento, o 3º da administração e da direção, e o subsolo da infraestrutura, do necrotério e da área restrita A-01), a
mobília, a sujeira, o sangue, a luz, o som, a narrativa ambiental, o saque, os zumbis e a otimização. **Por agora: a inspeção
do projeto e o blockout, numa cena separada.**

As referências estão nas 4 imagens que o usuário mandou na conversa (as plantas do térreo, do 2º, do 3º e do subsolo). Elas
são conceituais: o núcleo (as escadas e o elevador) muda de lugar de uma para a outra, e as escalas não batem (no térreo, a
régua daria um prédio de 100 × 80 m; nas outras, uns 56 × 32).

## O diagnóstico do projeto (a fase 1 do pedido)

- **A Unity:** 6000.6.5f1, URP 17.6 (Forward+), o Input System novo, AI Navigation 2.0, Netcode for GameObjects 2.13 (o
  anfitrião roda os zumbis, o saque e o mundo). Play sem Domain Reload.
- **As construções são feitas pelo jogo, em código** (o kit de `Scripts/Mundo/Construcoes`, desde a Parte M6): as paredes com
  as aberturas, as lajes com os vãos, as escadas em U, as portas, a colisão, os pisos dos passos, o eco e o NavMesh, montados
  na hora (em outra thread) e por andar, perto dos jogadores. É o mesmo caminho do bunker novo (`PlantaDeBunker` + `Bunker`),
  que é o modelo mais parecido com o hospital: uma construção única, de andares, primeiro numa cena de teste e depois no mundo.
  Por isso o hospital **não** segue a pasta sugerida no pedido (`Assets/ZombieCityCleaner/Hospital/...`), e sim as do projeto.
- **A luz é em tempo real:** o mundo é montado na hora, então não dá para assar a luz (o pedido fala em luz "baked quando
  adequado"). O jeito do projeto é o do bunker: poucas luzes pontuais sem sombra, só nos andares carregados.
- **Já existe um hospital sorteado:** um em cada cidade grande do mundo sorteado (`PlantasDePredio.ParaHospital`, de 4 a 6
  andares, num lote de prédio), com os móveis antigos (`Cidade_Mobilia.fbx`: a maca, a cama de hospital, o armário de
  remédios, o banco de espera e o aparelho de raio-X). O Hospital Principal é outro, feito à mão.
- **O que já existe e serve:**
  - o kit (as paredes, as portas, as escadas, os balcões, a colisão e o NavMesh) e o teste de caminhos do bunker;
  - as portas que abrem, trancam e se arrombam (o `Plano_Portas.md`: a gazua e o pé de cabra) e as janelas com tábuas;
  - o saque nos móveis, com as raridades e as unidades da farmácia e do hospital (o `Plano_Saque.md`);
  - os zumbis com NavMeshAgent, o `ZombieManager` (com os zumbis nos prédios) e os especiais (o Brutamonte, o Long, o Oco);
  - o material do abandono (a M9: a pintura descascando, a umidade, o mofo, a ferrugem, as rachaduras, as pichações) e o
    recurso "Decals" do URP (o sangue e os furos de tiro);
  - a mobília nova (com textura, as portas e as gavetas que abrem, em ×1,30) e o lote do bunker, que já tem muito do hospital:
    a `CamaHospital`, o `Biombo`, a `BancadaInox`, a `PiaInox`, o `ArmarioDeAco`, a `MaquinaDeLavar`, a `Secadora`, o
    `Vestiario` (os armários), o `Gerador`, o `QuadroEletrico`, as `Baterias`, a `Bomba`, os `FiltroDeAr`, o `Ventilador`,
    os canos e os dutos (`CanoReto`, `CanoCurva`, `CanoT`, `Registro`, `Duto`), as `EstanteAco90/120`, o `FreezerHorizontal`;
    e o escritório (a `MesaEscritorio`, o `Computador`, a `CadeiraEscritorio`, o `Arquivo`, o `Gaveteiro40`);
  - os sons de ambiente da trilha (`MaestroDaTrilha.Ambiente`) e os sons gerados por código.
- **O que não existe:** um modelo de ambulância (no blockout, caixas do tamanho de uma), a luz que pisca, as gavetas do
  necrotério, a maioria dos móveis médicos (a maca de emergência, o monitor, o suporte de soro, o carrinho de parada, o
  respirador, a mesa e o foco cirúrgico, a mesa de autópsia, o microscópio, a centrífuga, os racks de servidor) e os documentos
  da narrativa.

## A planta consolidada (a fase 2)

As fotos: `Imgs/Renders/Hospital_Blockout_Plantas.png` (os 4 andares de cima, com os nomes e os tamanhos; em vermelho, o que
está fechado), `_Corte_Escada.png` e `_Corte_Meio.png` (os cortes), `_Fora.png` e `_Dentro.png` (as vistas).

1. **O corpo:** 56 × 32 m em todos os andares (1.792 m² cada, uns 7.400 m² com os anexos), 4 m de um chão ao outro (a laje
   com o forro de 0,6 m e o pé-direito de 3,4 m), a cobertura a 12 m e a platibanda até 13,1 m. O subsolo fica embaixo do
   corpo inteiro. As portas e as janelas na escala das casas (×1,15: a porta de 1,27 × 2,42 m; a dupla, de 2 m).
2. **A grade de todos os andares** (de trás para a frente): a faixa do fundo (8 m), o corredor de cima (3 m), a faixa do meio
   (10 m, com o saguão no meio, em volta do elevador), o corredor de baixo (3 m) e a faixa da frente (8 m). Os dois corredores
   atravessam o prédio de ponta a ponta e se juntam no saguão: dá a volta inteira, sempre com duas rotas.
3. **O núcleo, igual nos 4** (as divergências das imagens resolvidas):
   - a **escada oeste e a leste** nos cantos do fundo (como nas plantas do 2º, do 3º e do subsolo), em U, do subsolo ao 3º
     (caixas de 4 × 8 m, lances de 1,8 m, 11 degraus de 18 cm por lance, o patamar de cada andar junto da porta e o do meio
     com 3 m);
   - o **elevador** de maca no meio (3 × 3,5 m), com a porta para o saguão de cada andar: atrás da recepção no térreo (como na
     planta do térreo), de frente para o posto de enfermagem no 2º, o hall de espera no 3º e o hall do necrotério no subsolo
     (os corpos descem por ele). Parado, com as portas fechadas em todos os andares;
   - a **escada de serviço**, só do térreo ao subsolo, no fundo, no meio do serviço (o "acesso ao porão" das plantas).
4. **Os andares** (de oeste para leste; o tamanho em metros):
   - **Subsolo:** no fundo, a lavanderia (8 × 8), a manutenção (6 × 8), a escada de serviço, a sala de geradores (12 × 8), a
     sala elétrica (8 × 8) e os resíduos biológicos (10 × 8); no meio, o depósito geral (10 × 10), a sala de autópsia (12 × 10),
     o hall do necrotério (no elevador), o necrotério (12 × 10) e as câmaras frias (10 × 10, só pelo necrotério); na frente, a
     oficina (12 × 8), o vestiário (6 × 8), a **A-01** (o corredor curto de 3 × 4 m, aberto, até a porta reforçada fechada, as
     duas salas técnicas com as portas bloqueadas e a área de dentro, de 20 × 4 m, encostada na parede da frente: o
     que vem depois cresce para baixo da praça, sem mexer no subsolo), a casa de bombas (10 × 8) e o banheiro técnico (8 × 8).
   - **Térreo:** a **emergência a oeste** (o recebimento das ambulâncias, colado na garagem, a emergência de 11 × 10, as triagens
     1 e 2 entre ela e a recepção, a sala vermelha e a medicação do outro lado do corredor do fundo, a observação do outro lado
     do corredor da frente); a **recepção** no saguão (o balcão em U, aberto atrás) e a **sala de espera** de 20 × 8 na frente,
     com a entrada principal; os **exames a leste** (os consultórios 1 e 2, o laboratório de 15 × 10, o raio-X com o comando
     e o vidro, a coleta); o **serviço no fundo** (o depósito, a limpeza, a escada de serviço e a saída de serviço para o pátio
     do lixo); a segurança na ponta da frente, a oeste; os banheiros na ponta da frente, a leste. **Os anexos:** a garagem das
     ambulâncias (15 × 13 m, 4,2 m livres, as 3 vagas, um portão por vaga) e a entrada (8 × 4 m de vidro, com a marquise).
   - **2º andar:** no fundo, os consultórios 3 e 4, a UTI (16 × 8) e o centro cirúrgico (o preparo, a sala de cirurgia 1, de
     12 × 8, e a 2, de 5 × 8); no meio, as enfermarias A e B (16 × 10) dos lados do posto de enfermagem (o balcão em U de
     frente para o elevador); na frente, o depósito médico, a **ala de isolamento** (o corredor de dentro, atrás da porta
     reforçada; a outra porta da ala bloqueada; os 4 quartos de 7 × 5,5 m com a porta reforçada e o vidro de observação; o
     quarto 4 lacrado), o descanso da equipe e os banheiros.
   - **3º andar:** no fundo, a TI e os servidores, a copa, a sala de reunião (14 × 8) e o arquivo (20 × 8, para os corredores
     entre as estantes); no meio, os escritórios A e B (20 × 10) dos lados do hall de espera; na frente, a segurança e o
     monitoramento, o RH, a sala de planejamento, a **sala do diretor** (12 × 8, no meio da fachada, com a porta dupla para o
     corredor e a da secretaria), a secretaria (12 × 8, com a espera) e os banheiros.
   - **Os banheiros ficam empilhados** no canto da frente, a leste, em todos os andares (os canos sobem juntos).
5. **Por fora:** a fachada de concreto claro com as janelas numa grade de 4 m, alinhadas em todos os andares (só onde a janela
   cabe inteira num cômodo que aceita janela: sem nas escadas, no raio-X, nas câmaras e no subsolo; pequenas e altas nos
   banheiros), a cruz vermelha na platibanda, a casa de máquinas do elevador e os aparelhos de ar na cobertura, a praça com a
   marquise, o estacionamento, o pátio e o acesso das ambulâncias (do lado oeste), o pátio de serviço com a caçamba (no fundo)
   e a rua. As saídas de emergência nas pontas do corredor da frente (a oeste, barricada).
6. **Os cômodos que não estão nas plantas:** o recebimento das ambulâncias (está na do térreo), o comando do raio-X (a "área
   de operação protegida" do pedido), a coleta e a saída de serviço (para fechar a grade do térreo), o preparo cirúrgico, o
   corredor do isolamento, a casa de bombas e os banheiros do 3º andar (empilhados).

## As decisões (com a recomendação)

**Tomadas pela recomendação para fazer o blockout (07/11/2026); todas podem mudar na aprovação.**

1. **A planta consolidada** (acima): o corpo de 56 × 32 m, a grade das faixas, o núcleo (as escadas nos cantos do fundo e o
   elevador no meio, de frente para o saguão) e a escada de serviço. A outra opção: o núcleo inteiro no meio (as duas escadas
   e o elevador atrás da recepção, como na planta do térreo), que tira a segunda rota das pontas.
2. **Onde ele vai:** primeiro só na cena de teste (`Scenes/Exemplos/Hospital.unity`); depois, no mundo sorteado, no lugar
   do hospital gerado da maior cidade (ocupando um quarteirão inteiro, com a praça e o estacionamento); no mapa fixo, na
   Blackwater City, quando as cidades chegarem. A outra opção: direto no mapa fixo.
3. **Os móveis:** um lote novo no `Mobilia.blend` (o `mobilia_hospital.py`, no estilo da mobília nova: a textura, as portas e
   as gavetas que abrem e o de longe), reaproveitando o que o bunker e o escritório já têm. A outra opção: os móveis antigos
   do hospital sorteado (sem textura e sem abrir).
4. **A sujeira e o sangue:** o material do abandono da M9 em tudo (o grau "muito", mais leve na administração) e um atlas novo
   de marcas (o sangue seco, os arrastos, as mãos, a sujeira dos cantos, o mofo e as infiltrações) em placas planas no chão e
   nas paredes, juntadas na malha de cada andar, postas por receitas por cômodo e pela semente (nada solto ao acaso). Os decals
   do URP só nuns poucos lugares de destaque (a sala vermelha, o isolamento). A outra opção: tudo em decals (mais caro na GPU).
5. **A luz** (**mudou em 07/11/2026, a pedido: "o hospital não vai ter energia"**: nenhuma luz acende, nem as de destaque, as
   que piscam ou as de emergência; as luminárias ficam, apagadas; dentro, só o dia pelas janelas, como nas casas do jogo, e a
   lanterna; o subsolo, quase escuro. O que estava decidido antes:) em tempo real, sem assar (veja o diagnóstico), só nos andares carregados, sem sombra, com 1 ou 2 luzes de
   destaque por andar (a vermelha da emergência, a da sala do diretor) e as calhas apagadas na maioria. As que piscam e as de
   emergência por um componente só (um por andar, não um por luz).
6. **As portas:** as do sistema de portas (abrir, trancar, gazua e pé de cabra), com as do hospital: a dupla de vai-e-vem, a
   corta-fogo da escada, a reforçada (o isolamento, o raio-X, a A-01), a de vidro e o portão da garagem.
7. **O elevador:** parado e fechado (sem energia, para sempre: a missão do gerador saiu). Os zumbis não usam.
8. **A A-01:** fechada, como gancho; o que fica atrás dela é para depois (sem o laboratório gigante).
9. **Os zumbis:** poucos e espalhados, pelos pontos e pelas zonas de cada setor (uns isolados, uns grupos pequenos, salas
   fechadas com zumbis), ativados por andar; nada de horda dentro.
10. **O que fica fechado de propósito no blockout:** a porta da A-01 e as das salas técnicas, o quarto 4 e a segunda porta do
    isolamento, a saída de emergência oeste e dois portões da garagem. As outras portas, abertas (as trancas vêm com o sistema
    de portas).

## Fica para depois (ideias)

- ~~**O gerador do subsolo:** ligar a luz do prédio e o elevador (uma missão).~~ Saiu (07/11/2026): o hospital não tem
  energia.
- **Os documentos:** os relatórios da quarentena, os avisos de evacuação, as fichas dos pacientes e as decisões da direção,
  que contam a história do térreo (o caos), do 2º (o isolamento), do 3º (o que a administração sabia) e do subsolo (a A-01).
  O plano deles está na parte 6 ("A narrativa, os documentos").
- **O heliponto** na cobertura (as plantas mostram um H) e a escada até ela.
- **Os NPCs:** um sobrevivente escondido num andar, um médico morto com uma pista.

## As partes (cada uma aprovada antes da próxima)

As fases do pedido foram juntadas em partes, como nos outros planos.

1. **A inspeção e a planta consolidada** (este plano).
   - **O estado (07/11/2026): aprovada** ("Aprovar, parte 3", junto com a parte 2).
2. **O blockout, numa cena de teste** (`Scenes/Exemplos/Hospital.unity` do projeto Unity, no menu Zombie City Cleaner >
   Exemplos > Hospital principal; as fotos, em Exemplos > Fotos do hospital):
   - **O estado (07/11/2026): aprovado** ("Aprovar, parte 3"). As fotos do blockout ficaram em `Imgs/Renders/Hospital_Blockout_*`.
     - **O hospital é feito pelo kit** (`PlantaDoHospital` e `Hospital`, como o bunker), em 5 partes: a casca (as paredes de
       fora com as janelas e as portas de fora, as lajes, a cobertura, o núcleo, as 3 escadas, os anexos, a marquise e a
       cobertura) e os 4 andares por dentro (as paredes, as portas, os pisos, os balcões e as luzes). As paredes saem das
       linhas da planta: entre dois cômodos, a de dentro; onde nenhum encosta, a de fora.
     - **O piso de cada cômodo na cor da zona** (como as legendas das plantas): a circulação, a área médica (azul), o serviço
       (concreto), a administração (bege) e a área restrita (vermelho).
     - **As portas:** as de madeira, as de metal, as de vidro, as reforçadas, a do elevador (fechada), as corta-fogo das
       escadas (abertas rentes à parede do patamar: a 90°, atravessavam o patamar e cortavam o caminho do lance que chega) e os
       portões da garagem; as fechadas são chapas sólidas, e as bloqueadas têm as tábuas.
     - **No térreo:** o balcão da recepção, as vagas pintadas e duas ambulâncias de caixa na garagem (e uma largada no pátio).
       **No 2º:** o balcão do posto de enfermagem. As luzes vermelhas na sala vermelha, no isolamento e na A-01.
     - **Para andar:** as setas pulam entre a frente, o saguão de cada andar (de frente para o elevador) e a garagem; o P mostra a
       planta (↑ ↓ os andares); a tela diz o andar e o cômodo, com o tamanho; o T, a terceira pessoa; o L acende e apaga.
       Dentro, só as luzes do andar da câmera e dos vizinhos ficam acesas, e a luz de fora cai (30%; no subsolo, 6%).
     - **Os testes do Editor** (`Tests/Editor/HospitalTestes`, ~2,4 s): a planta fecha (os cômodos cobrem o corpo sem se
       sobrepor, as portas nas paredes certas), o núcleo fica no mesmo lugar nos 4 andares, a escada cabe na caixa, e, com um
       NavMesh só do hospital (o agente do zumbi), **os 24.206 pedaços de chão (de 0,5 m) dos 85 cômodos abertos têm caminho da
       praça**, subindo e descendo as escadas; os 4 lacrados ficam sem caminho nenhum.
     - **Os números:** 26.676 triângulos (a casca 14.340; os andares de 2.472 a 3.456) e 173 lâmpadas.
3. **A arquitetura final:** os acabamentos de hospital por setor (o rodapé, as faixas e o bate-maca nos corredores, o forro com
   as calhas, os azulejos, o verde hospitalar e o bege da administração), as portas de vai-e-vem com o visor, as janelas com o
   vidro sujo, as persianas e as tábuas, a cabine do elevador parada, as placas dos setores e dos andares (em inglês), o letreiro
   "HOSPITAL" na fachada e as rampas da entrada.
   - **O estado (07/11/2026): aprovada** ("pode seguir pra mobilia", depois dos ajustes). As fotos: `Imgs/Renders/Hospital_Arquitetura_Fora.png`,
     `_Dentro.png`, `_Detalhes.png`, `_Plantas.png` e os dois cortes (`_Corte_Escada.png` e `_Corte_Meio.png`).
     - **Os acabamentos por setor** (o `Acabamento` de cada cômodo, na `PlantaDoHospital`; o `Hospital.Acabamento.cs`), na face
       de dentro de cada parede, cortados nas portas e nas janelas:
       - **a circulação** (os corredores, os saguões, a recepção, o posto, a espera e a entrada): a parede clara, o verde até
         1 m, o rodapé escuro e o **bate-maca** (a régua clara de 18 cm, de 0,82 a 1 m, saindo 4 cm); o piso vinílico claro;
       - **a área médica** (a emergência, as triagens, os consultórios, o laboratório, a UTI, as enfermarias): o verde-claro, a
         barra verde até 1,2 m com o friso claro e o rodapé verde-escuro; o piso vinílico verde-acinzentado;
       - **o azulejado** (os banheiros, o centro cirúrgico, a autópsia, o necrotério e as câmaras, a copa, a limpeza, o vestiário,
         a lavanderia e os resíduos): o azulejo até 2,1 m (1,5 m na copa, na limpeza, na lavanderia e nos resíduos) com o
         rejunte a cada 30 cm, o listelo verde-escuro a 1,2 m e o arremate; o chão de lajotas de 40 cm com o rejunte e o ralo;
       - **a administração**: o bege, o lambri bege até 0,9 m e o rodameio de madeira (na sala do diretor, o lambri de madeira
         até 1 m); o carpete nos escritórios, no RH, no planejamento e no descanso, o taco na diretoria, na secretaria e na
         sala de reunião, o piso elevado de placas na TI;
       - **o serviço** (os depósitos, a garagem e o subsolo, menos o necrotério): a barra verde-escura até 1,2 m com o friso
         amarelo, como no bunker; o concreto;
       - **o isolamento**: o branco, a faixa vermelha de 1,05 a 1,2 m e o rodapé verde-escuro;
       - **a área restrita A-01**: o concreto escuro e a faixa de aviso amarela e preta;
       - **as escadas**: a barra verde-escura com o friso, só em volta do patamar de cada andar, e o número do andar grande na
         parede da porta, com o nome da escada ("STAIR A", "STAIR B", "SERVICE STAIR").
     - **O forro modular** (as placas de 0,6 × 1,2 m na mesma grade em todos os cômodos) na circulação, na área médica, na
       administração e no isolamento, com as luminárias embutidas no meio das placas; nos azulejados, no serviço e nas escadas,
       o teto liso com as calhas penduradas.
     - **As portas** (o `Hospital.Portas.cs`): a **de vai-e-vem** (um tipo novo: as duplas da emergência, do recebimento, da
       sala vermelha, da observação, do laboratório, da UTI, das enfermarias, do centro cirúrgico, da lavanderia, da autópsia e
       do necrotério), clara, com o visor, a chapa de inox embaixo e a de empurrar; a **corta-fogo** das escadas, com a barra
       antipânico e o visor estreito; a **de madeira** com a maçaneta (na área médica, com o visor); a **de metal** de serviço,
       com a veneziana; a **reforçada**, escura, com o visor pequeno e o aviso amarelo ("ISOLATION", "X-RAY", "A-01"); a **de
       vidro** no caixilho de alumínio; as **de correr da entrada** (a da esquerda aberta, a da direita emperrada no meio, sem o
       vidro); as molduras de metal, de madeira ou de inox em volta de cada uma. As abertas ficam entre 74° e 104°, sorteadas
       pela semente (mais fechadas, o zumbi não passava); as fechadas ficam dentro do vão.
     - **As janelas de fora**: o caixilho de alumínio, o montante e a bandeira (na entrada, a vitrine com os montantes a cada
       1,25 m; nos banheiros, a pequena), e o vidro sujo sorteado: inteiro, com um ou dois vidros quebrados (o buraco torto),
       sem vidro ou tapado por fora com as tábuas ou o compensado (no térreo, quatro em cada dez, da evacuação; os quartos do
       isolamento, todas). Por dentro, a moldura clara e, nos escritórios, nos consultórios, nas enfermarias, na UTI e na
       observação, as **persianas** claras, baixadas até uma altura sorteada, umas tortas (um cordão arrebentou) e com lâminas
       faltando (as das casas saíram em 26/10/2026 porque, escuras, pareciam pedaços pretos: estas são claras). **As de
       dentro**: o vidro do comando do raio-X e os de observação do isolamento, inteiros; o do quarto 4, tapado com o
       compensado do lado do corredor.
     - **O elevador** (o `Hospital.Elevador.cs`): os trilhos-guia no poço, os para-choques no fundo e **a cabine parada entre o
       2º e o 3º andar** (o inox por dentro, o piso de borracha, as portas meio abertas, o painel, o corrimão, a luz de
       emergência fraca e os cabos até a casa de máquinas). **No 2º andar, as folhas foram forçadas**: pela fresta de 35 cm se
       vê a cabine na altura da cabeça e o poço escuro embaixo (o jogador e o zumbi não passam). Em cada saguão, o painel de
       chamada, o mostrador parado no 2 e, no térreo, o papel de "OUT OF ORDER" colado na porta.
     - **As placas, em inglês** (o `Hospital.Placas.cs`; as letras pintadas passaram do bunker para o kit, as `Letras`, com o
       alfabeto inteiro, os números e as setas): a placa verde em cima de cada porta, com o nome do cômodo ("TRAUMA",
       "TRIAGE 1", "ICU", "OR 1", "WARD A", "MORGUE", "A-01  RESTRICTED AREA"...), do lado da circulação (entre dois cômodos, do
       lado de quem tem mais portas); o EXIT em cima das portas das escadas e das saídas; as placas penduradas nos corredores,
       logo fora do saguão, dos dois lados ("^ EMERGENCY", "^ X-RAY  LAB"...); o diretório na face oeste do poço do elevador (o
       andar em que se está, em amarelo) e o número e o nome do andar na face leste (o térreo é o 1, o subsolo é o B).
     - **Por fora** (o `Hospital.Fora.cs`): **o embasamento** (o chão em volta 45 cm abaixo do térreo); a **varanda da entrada**
       embaixo da marquise, com os degraus no meio (com a faixa amarela na quina) e **as duas rampas** dos lados (de 1:12, com o
       patamar em cima e os guarda-corpos); a **rampa das ambulâncias** na frente dos portões da garagem; o patamar e os
       degraus das saídas (a do leste, a barricada do oeste e a de serviço); o **letreiro HOSPITAL** em letras vermelhas em
       relevo na platibanda, entre as duas cruzes; o **EMERGENCY** na placa branca em cima dos portões; o **MAIN ENTRANCE** na
       marquise; o rufo de metal no alto da platibanda.
     - **No térreo, as linhas no chão** desde a entrada: a vermelha até a emergência e a azul até o laboratório.
     - **Os testes do Editor** passam (`HospitalTestes`, ~2,5 s): com o embasamento, os 24.187 pedaços de chão dos 85 cômodos
       abertos têm caminho da praça, subindo pelos degraus e pelas rampas; os 4 lacrados continuam sem caminho. Os do bunker
       também (o letreiro dele agora vem do kit).
     - **Os números:** 96 mil triângulos (a casca 27,6 mil; os andares de 14,8 a 19,3 mil, contra 2,5 a 3,5 mil no blockout:
       a maior parte são o rejunte, a grade do forro, as persianas, as faixas cortadas nas portas e as letras) e 174 lâmpadas.
       No jogo, só o andar de perto e os vizinhos carregam (a parte 7).
     - **Depois das imagens de referência do usuário** (07/11/2026: "um prédio não tão quadrado e com o visual mais
       bonito/realista por fora e por dentro"; as três imagens do "Riverside General Hospital" de tijolo e concreto), a
       fachada foi refeita (`Hospital.Fachada.cs`):
       - **o tijolo aparente**: as paredes de fora numa cor nova da paleta, a `TijoloAparente` (linha 14, coluna 9; no
         `cidade_kit.py`, no `Paleta.cs` e na `Paleta.png` das duas pastas, que ficaram iguais: a do projeto Unity tinha 16
         cores a mais, nas linhas 4 e 15, e foi a base), um tipo novo do material do abandono (o 9): o shader desenha os
         tijolos de 23 × 7,5 cm em amarração, cada um num tom, o rejunte claro e, de longe, o tom médio; por cima, o abandono
         da alvenaria (os escorridos, o salitre mais fraco e escuro, a umidade na base, as rachaduras). O `Tijolo` do resto do
         mapa não mudou;
       - **o concreto em relevo**: o rodapé, as faixas na linha das lajes do 2º e do 3º andar, as pilastras nas quinas e a
         cada 8 m (entre as janelas), a cornija em dois degraus com o capeamento da platibanda e a moldura de cada janela (o
         peitoril saindo 10 cm, a verga e os batentes); as janelas com mais uma travessa (4 vidros embaixo e a bandeira);
       - **a torre da entrada**: de concreto, 10 m de largura, saindo 45 cm da fachada e subindo 4,6 m acima da cobertura, com
         as faixas de janela do 2º e do 3º andar (por elas se veem as janelas do prédio), as juntas das placas, o nome em
         relevo, **BLACKWATER GENERAL HOSPITAL** (o hospital da Blackwater City do mapa fixo; o `NomeNaTorre`, fácil de trocar),
         e a cruz vermelha em cima;
       - **o pórtico**: a marquise de 60 cm com a faixa, o forro escuro embaixo, as quatro luminárias e os dois pilares
         quadrados de concreto, com a base e o capitel;
       - **a ala da emergência**: a faixa de concreto em cima da garagem, o capeamento, a cruz vermelha e a placa branca do
         EMERGENCY embaixo dela;
       - **a cobertura**: as casas das escadas dos cantos do fundo e a casa de máquinas, de tijolo, com o capeamento; seis
         aparelhos de ar com as hélices e um duto; a antena com a luz vermelha;
       - **os detalhes**: os condutores da chuva, as arandelas em cima das saídas e a cobertura da saída de serviço;
       - **na cena**: o material do abandono no hospital, no chão e nos objetos (por dentro, 60% do de fora: com o grau todo,
         a pintura verde descascava em manchas de camuflagem), a hera subindo pelas paredes de fora (com a normal das folhas
         um pouco para cima: de perto, as da sombra saíam pretas), os arbustos na frente e do lado, os frades amarelos na
         frente dos degraus e dos lados da rampa das ambulâncias, os postes do estacionamento, a placa da vaga especial e a
         lixeira da entrada; o chão dos corredores e dos azulejados passou para cores "de dentro" (o concreto ganhava as
         folhas e as poças da calçada).
       - Os números: a casca com 35,6 mil triângulos (eram 27,6 mil) e a hera com uns 42 mil (no jogo, ela só aparece perto
         do jogador). Os testes continuam passando (24.189 pedaços de chão).
     - **Os ajustes depois da fachada nova** (07/11/2026, a pedido: "não entendi essas janelas, elas têm vidro? o que são
       essas estruturas metálicas", "não gosto da fonte", "muitos bugs de sobreposição nas janelas", "quinas de paredes, como
       na recepção principal, que não se encaixam, deixando um vão no meio"):
       - **as janelas:** o vidro passou para o escuro ("Vidro") e enche cada quadro, entrando 1,2 cm no caixilho (o
         `AbandonoDaObra.Vidro` ganhou a cor e a margem); com a margem de 3 cm do abandono, ficava uma fresta clara em volta
         de cada vidro, que parecia uma segunda moldura de metal, de fora e de dentro. As tábuas viraram madeira marrom (as
         cores da madeira das casas ficam cinza no material do abandono e pareciam uma grade de metal): em X com uma deitada no
         meio, de três a quatro deitadas ou a chapa de compensado clara, pregadas na frente da moldura de concreto (antes
         elas atravessavam o peitoril, a verga e os batentes). Sem vidro, só 4% das janelas;
       - **a fonte nova** (`Construcoes/Letras`, para o hospital e o bunker): uma sem serifa de placa, com as letras cheias,
         as curvas de verdade e a largura de cada letra, em polígonos (chatos nas placas, extrudados no relevo); a folha em
         `Imgs/Renders/Fonte_Construcoes.png`. Ela é uns 20% mais larga que a de antes: os nomes dos andares do bunker
         continuam cabendo (o maior, com 3 m numa parede de 3,4 m);
       - **as quinas:** as paredes de dentro que acabam numa quina ou soltas avançam meia parede (`Avanco`): antes, as duas
         paravam no meio uma da outra, e ficava um vão de 10 × 10 cm de cima a baixo (como na quina do poço do elevador, na
         recepção); onde a linha continua com outra parede ou a ponta encosta numa parede de fora, não. As faixas em relevo
         (o rodapé, o bate-maca, o rodameio) dobram a quina junto.
       - Os testes continuam passando (o do hospital e o do bunker). Os números: 166 mil triângulos na cena (a casca 37,8 mil,
         os andares de 19,3 a 24,7 mil: as letras redondas da fonte nova pesam mais que os traços de antes; a hera uns 42 mil).
     - **O que ficou para as outras partes:** a mobília (a 4), as marcas do abandono, a sujeira, o sangue e as placas do
       forro faltando (a 5), as portas que abrem e trancam pelo sistema de portas (a 7: aqui, as folhas são paradas) e um
       modelo de ambulância (as de caixa continuam).
4. **A mobília:** o lote novo (as famílias hospitalar, escritório, industrial e decoração do pedido) e os móveis de cada cômodo,
   escritos um a um (como os do bunker), com a colisão e a passagem livre para o zumbi.
   - **O estado (07/11/2026): aprovada** ("aprovado, pode seguir para a próxima parte", depois dos 13 ajustes), em duas fatias
     (como a parte 4 do bunker; "pode seguir pra mobília", depois da aprovação dos ajustes da parte 3). **A primeira fatia (os
     modelos novos) foi aprovada** ("Aprovar, pôr nos andares", 07/11/2026):
     - **O lote do hospital** (`Modelo 3D/Cidade/mobilia_hospital.py`, no `Mobilia.blend`, no kit e no estilo da mobília nova:
       a textura, as partes que abrem e o de longe; em ×1,30 no jogo): **47 modelos em 8 fileiras** ("MN_Hospital<grupo>"):
       - **os leitos**: a maca de emergência (o colchão de vinil com as costas levantadas, as grades, o soro e o cilindro
         de oxigênio no chassi), a maca de exame do consultório (as 2 gavetas, a porta e o rolo de papel), o criado-mudo de
         hospital (a gaveta e a porta azuis), a mesa de refeição de cama e a cadeira de rodas;
       - **os aparelhos**: o monitor de sinais (a tela rachada, os cabos), o suporte de soro com a bomba de infusão, o
         carrinho de parada (as 5 gavetas, o desfibrilador com as pás, a prancha e o oxigênio), o respirador com a
         traqueia, a mesa de instrumentos e a máquina de anestesia (as 3 gavetas, os rotâmetros, o fole e os cilindros);
       - **a cirurgia e as imagens**: o foco cirúrgico **pendurado** (a origem no teto; ele desce 1,1 m, ×1,30 no jogo, e
         fica com a cabeça grande e a satélite tortas), a mesa cirúrgica, a pia de escovação (as 3 bicas altas) e o raio-X
         (a mesa, o trilho no chão, a coluna, o tubo com o colimador e o avental de chumbo);
       - **o laboratório e a farmácia**: a bancada (as portas, as 3 gavetas, o tampo de resina com a cuba, a prateleira dos
         reagentes, as estantes de tubos), o microscópio, a centrífuga (a tampa abre), a geladeira de vidro quebrado, o
         armário de remédios (as portas de vidro quebrado em cima, quase vazio: o saque) e a cadeira de coleta;
       - **o necrotério**: as gavetas frigoríficas (3 × 3; cada porta e a bandeja atrás dela são uma gaveta só, que sai
         1,4 m, e 4 delas têm um saco de corpo), a mesa de autópsia (a cuba no pé, os furos, a mangueira), o saco de corpo, a
         maca do necrotério e a maca com o corpo embaixo do lençol (um pé para fora, com a etiqueta no dedão);
       - **a espera**: os bancos de longarina de 3 e 4 lugares (no de 4, a concha do canto quebrada), o bebedouro de
         galão, a máquina de lanches (a porta da frente inteira abre; a vitrine quebrada, as espirais com uns poucos
         pacotes), a TV de tubo no suporte de parede e o mural de cortiça com os avisos;
       - **o escritório**: a mesa do diretor (os 2 gaveteiros, o couro verde, o abajur de banco, a placa do nome), a
         estação de trabalho da baia (as divisórias, a mesa em L, o gaveteiro), o quadro branco riscado, o rack dos
         servidores (a porta de grade), a mesa do monitoramento (os 6 monitores), a copiadora (as gavetas e a tampa) e a
         estante do arquivo (as caixas de arquivo morto, uma caída no chão);
       - **o serviço**: o carrinho de limpeza, o carrinho de roupa suja, o contêiner de lixo hospitalar (a tampa abre), os
         cilindros de oxigênio, a pilha de caixas de suprimentos, as cabines do banheiro público (a "CabineDeBanheiro", com
         as duas divisórias, e a "CabineDeBanheiroLado", só com a da direita, para enfileirar sem as divisórias se
         sobreporem; a porta abre; a privada vai à parte) e o mictório.
     - **Sem vidro transparente**, como na mobília: as portas de vidro estão quebradas, com os cacos nas beiradas.
     - **Os triângulos:** de 316 (a TV) a uns 3,8 mil (o rack, a estante do arquivo, a bancada); os mais pesados são as
       gavetas do necrotério (uns 6 mil, com os sacos simplificados) e a máquina de lanches (uns 3 mil, com as espirais de
       seção triangular). Os de longe, de 56 a 672.
     - **As fotos** (`fotos_hospital`, no tamanho do jogo, com o personagem principal na ponta de cada fileira; as compridas
       viradas de lado): `Imgs/Renders/Hospital_Objetos_<grupo>.png` (e `_Aberto`) e a folha `Hospital_Objetos_Folha.png`.
       Pelo Cycles na CPU: com a Unity aberta e pesada, o EEVEE do Blender sem janela caía (sem memória).
   - **A segunda fatia (a mobília nos andares) foi aprovada** (07/11/2026, com os ajustes abaixo):
     - **O export:** o `Mobilia_Nova.fbx` com o lote do hospital (o `exportar_bunker`), na Unity a montagem da mobília nova
       (os prefabs dos 47 e a tabela das medidas, agora com 176 modelos). O `CatalogoDaMobilia` (o mostruário e as cores)
       fica para quando os modelos forem para a cena das variações.
     - **Os móveis** (`Mundo/Construcoes/MoveisDoHospital` do projeto Unity): **730 em 4 andares**, por **receitas por tipo
       de cômodo** (uns 30 tipos em 85 cômodos abertos; o hospital é grande demais para escrever um por um como o bunker):
       cada receita encosta os móveis nas paredes livres (primeiro a do fundo, a oposta à porta de entrar, depois as dos
       lados e por último a da porta) ou põe no meio (as macas da sala vermelha, as mesas da autópsia, a mesa cirúrgica com
       o foco em cima e a anestesia na cabeceira, a ilha do laboratório, as fileiras de bancos da espera, as estantes do
       arquivo em fileiras do fundo até 2,2 m da porta, as 14 baias de cada escritório, as mesas de 6 emendadas da reunião,
       a mesa do diretor de frente para a porta com o canto do sofá), pela semente.
       - **O que fica livre:** o giro de cada porta (a largura mais 35 cm de cada lado, da parede até a ponta da folha) e a
         frente dos vãos abertos (1 m); a frente das janelas, para os altos (mais de 1,25 m no jogo, menos as camas, as macas
         e os aparelhos de haste fina); e o chão de um móvel para o outro (4 cm de folga), menos os conjuntos (o computador
         na mesa, o microscópio na bancada, a cadeira na frente da mesa, o monitor e o respirador do lado da cama, a privada
         dentro da cabine, os módulos da copa encostados na pia).
       - **Os corredores:** poucas coisas, uma a cada 7 m, de cada andar (o subsolo: os carrinhos de roupa, a maca do
         necrotério, o lixo; o térreo: as macas, as cadeiras de rodas, os bancos; o 2º: as macas, os soros, o carrinho de
         parada; o 3º: os bancos, os vasos, o arquivo), e as compridas **de lado**, paralelas à parede (de frente, a maca
         atravessava os 3 m do corredor e cortava a ponta dele, com a segurança e o depósito médico atrás); o corredor do
         isolamento (2,5 m) fica vazio.
       - **As camas** (a observação, a UTI e as enfermarias) deixam 1 m livre nos cantos, e o comprimento de uma cama mais
         1 m quando a parede vizinha já tem camas (as duas fileiras fechavam um bolso no canto).
     - **A colisão:** a caixa de cada móvel na malha de colisão do andar (`MoveisDoHospital.Caixas`, no `Hospital.MontarAndar`,
       como as do bunker); na cena, os prefabs sem a colisão do corpo, as portas e as gavetas com a delas.
     - **O teste** (`HospitalTestes`): os 85 cômodos abertos continuam com caminho do zumbi desde a praça, com os móveis
       (20.097 pedaços de chão livres; antes deles, 24.190); os do bunker também passam.
     - **As fotos** (Zombie City Cleaner > Exemplos > Fotos da mobília do hospital): `Imgs/Renders/Hospital_Mobilia_Dentro.png`
       (12 vistas: a emergência, a espera, o laboratório, a sala vermelha, o necrotério, a autópsia, a UTI, a cirurgia 1, a
       enfermaria A, o escritório A, o diretor e o arquivo) e `Hospital_Mobilia_Plantas.png` (os 4 andares de cima).
     - **Os ajustes depois do primeiro teste** (07/11/2026, a pedido, 13 itens; feitos, esperando o teste do usuário):
       - **Os modelos** (no `mobilia_hospital.py` e na cama do `mobilia_bunker.py`): o lixo hospitalar, a máquina de lanches,
         a porta do rack dos servidores e a tampa da copiadora não abrem mais (o `_fundir` junta a parte no corpo, fechada);
         o armário de remédios ficou vazio (os remédios vão ser itens); a cama de hospital (a do hospital e a da enfermaria do
         bunker) ficou com o colchão deitado, sem a cabeceira levantada; o nicho do bebedouro virou uma chapa na frente do
         gabinete (a face dele ficava no mesmo plano da do gabinete e piscava em volta dos botões); a estante do arquivo
         perdeu a caixa caída e as fichas no chão (fechavam o corredor entre as fileiras do 3º andar). As partes velhas que
         ficaram soltas no `Mobilia.blend` (a porta da máquina e do rack, as tampas da copiadora e do lixo) foram apagadas:
         sem pai, elas iam no FBX como modelos à parte.
       - **As caixas de colisão próprias** (`MoveisDoHospital.CaixasProprias`): a cabine do banheiro (as divisórias e os
         montantes; a porta tem a caixa dela, que gira com ela) e a baia do escritório (as três divisórias e a mesa em L),
         no lugar da caixa do tamanho inteiro, que não deixava entrar na cabine nem chegar na cadeira. A cadeira da baia foi
         para o vão livre (o canto da frente, do lado sem mesa) e o computador para a mesa de trás, na frente dela; as baias
         ficam a 1,8 m das paredes (com 1,6, um arquivo encostado fechava a faixa de trás delas no escritório A).
       - **A fileira das cabines** vai da esquerda para a direita de quem olha para elas: a "Lado" não tem a divisória da
         esquerda e usa a da direita da cabine de antes (na ordem contrária, a última ficava aberta do lado).
       - **Os móveis com portas** (`ComPortas`: os armários, as geladeiras, a bancada do laboratório, o criado, a maca de
         exame, a lavadora e a secadora, as pias, o vestiário) ficam a 22 cm das paredes do lado: a porta abre 100°, e a
         ponta dela entrava na parede.
       - **O corrimão das escadas** tem a colisão.
       - **As quinas** (a "parede perto da placa LAUNDRY"): numa quina em L as duas paredes avançavam meia parede, e a ponta
         de uma ficava no mesmo plano da face da outra (uma faixa de 10 cm, de cima a baixo, que piscava; em todas as quinas
         em L e nos T de todos os andares). Agora, no L, só a parede em z avança e a em x recua até a face da outra; no T,
         nenhuma avança (`Hospital.Avanco`). Conferido por código: as faixas sumiram dos 4 andares.
       - **A soleira das portas da escada:** as paredes da caixa da escada sobem até a laje de cima, e no vão da porta do
         andar de cima a face de cima da parede de baixo ficava no plano do chão e piscava; a corta-fogo ganhou a soleira de
         metal, como a do elevador.
       - Os testes do hospital e do bunker passam (20.143 pedaços de chão com caminho). As fotos em
         `Imgs/Renders/Hospital_Mobilia_Ajustes.png` (as cabines, a baia, o armário de remédios, a cama, o bebedouro, o
         arquivo, a quina do LAUNDRY e a soleira).
   - **Ficou para depois:** os modelos do hospital na cena das variações (o `CatalogoDaMobilia`), a ambulância de verdade (as
     de caixa continuam) e o saque nos móveis novos (a parte 7).
5. **O abandono:** o material, o atlas das marcas e as receitas por cômodo (o sangue onde a história pede: a emergência, a sala
   vermelha, o isolamento; quase nada na administração).
   - **Sem energia** (07/11/2026, a pedido, logo depois da aprovação): o `Hospital` não tem mais as luzes (`Luzes` saiu; as
     luminárias, as arandelas, a luz da antena e a da cabine do elevador ficaram, apagadas), e a cena de teste não tem
     lâmpada nenhuma: o L liga e desliga a lanterna (a do jogo, presa na câmera: a força 34, a abertura de 62° e 28 m), e a
     luz de dentro é a do jogo (o `AmbienteDoDia`: nos andares de cima, a mesma de fora, que entra pelas janelas; no subsolo,
     15%). As fotos de dentro saem com a lanterna. A foto: `Imgs/Renders/Hospital_Sem_Energia.png`.
   - **O estado (07/11/2026): aprovada** ("Legal, aprovado", depois do brilho do azulejo no escuro); começada logo depois
     da aprovação da parte 4:
     - **O atlas das marcas** (`Modelo 3D/Cidade/hospital_marcas.py`, com o numpy do Python que vem com o Blender, sem abrir
       cena: o T_Marcas de 2048 px em células de 256, a tabela `AtlasDasMarcas.cs` do projeto Unity e a folha
       `Imgs/Renders/Hospital_Marcas_Atlas.png`): **20 marcas**, desenhadas em código. No chão: as duas poças grandes de
       sangue seco (a beirada mais escura, o miolo craquelado e as gotas em volta), a pequena, o arrasto (a poça e a faixa
       afinando, com os riscos ao comprido e os dedos de quem foi puxado), as pegadas de sola (a frente, o arco e o salto, com
       as garras em V, apagando a cada passo), os respingos, o rastro das rodinhas de uma maca, a mancha de água com as marcas
       das bordas, a sujeira junto da parede, os papéis (as folhas com o texto, a pasta e uma radiografia) e os cacos de
       vidro. Na parede: as três mãos descendo (alguém escorregou se apoiando), a mão arrastada, o respingo com os
       escorridos, a infiltração descendo do teto, o mofo subindo do chão, os furos de tiro (o reboco lascado e as
       rachaduras) e o encardido. No teto: a infiltração com as marcas das goteiras e o mofo. Quase todas em cinza, tingidas
       pela cor do vértice (o sangue, a água, o mofo, a sujeira); os papéis e os cacos, coloridos.
     - **O material** (`Marcas.mat`, o shader `ZombieCityCleaner/Marcas`: o das pichações com o plano do chão e do teto;
       transparente, sem escrever a profundidade, puxado para a frente, pouco desgaste), feito pelo
       `TexturasDoAbandono.Marcas` (a montagem da cena chama).
     - **As receitas** (`Hospital.Marcas.cs`, no `MontarAndar`): o sangue onde a história pede (muito na emergência, na sala
       vermelha, no recebimento e no isolamento; médio na triagem, na observação, na UTI, na cirurgia, na autópsia e no
       necrotério e nos corredores do térreo; pouco nas enfermarias, no posto, na espera, na recepção e nos corredores do
       2º; nada na administração e no serviço), os furos de tiro onde houve a última resistência (a entrada, a recepção, a
       espera, a emergência e os corredores do térreo; o isolamento), e em todo cômodo a sujeira junto das paredes, umas
       manchas de água, as infiltrações (no teto, com a água embaixo, mais no 3º, embaixo da cobertura; nas paredes de fora),
       o mofo nos úmidos (os banheiros, a lavanderia, a copa e o subsolo inteiro), o encardido nos corredores e os papéis na
       administração, na recepção e no posto. No chão, nada com o meio debaixo de um móvel; nas paredes, fora das portas e
       das janelas e a 25 cm das quinas; tudo inteiro dentro do cômodo. **736 marcas** (o SS uns 145, o térreo 230, o 2º
       170, o 3º 135). O sangue seco é opaco (meio transparente, por cima da parede clara, ele ficava rosado).
     - **As placas do forro que caíram** (`PlacasQueCairam`: 74, no térreo e no 2º; no 3º, a laje de cima é a cobertura):
       na grade do forro, longe das paredes e das luminárias e só onde o andar de cima tem o piso por cima. A laje tem o
       furo só no desenho (a colisão fica inteira), com o vão escuro do entreforro (45 cm) e, às vezes, um fio pendurado; no
       chão, a placa caída (inteira ou quebrada em duas) ou pendurada pela grade, caída de um lado.
     - Os testes do hospital continuam passando (20.143 pedaços de chão). As fotos: `Imgs/Renders/Hospital_Abandono_Dentro.png`
       (9 vistas: a emergência, a sala vermelha, o corredor da frente, a espera, a enfermaria A, o isolamento, a UTI, o
       corredor do 3º e o necrotério) e `_Perto.png` (o arrasto, a poça, o respingo na parede, as mãos, os tiros e o forro).
     - **O brilho do azulejo no escuro** (07/11/2026, a pedido: "quando está tudo escuro, o chão de azulejo fica com um
       brilho"): era o reflexo do céu (o padrão, fixo, o do dia) nas faces lisas, que não escurecia com a luz ambiente.
       Agora o reflexo acompanha a luz: na cena de teste (o `Iluminar`) e no jogo (o `AmbienteDoDia`: de noite, no subsolo e
       no bunker também). No subsolo, sem a lanterna, fica tudo escuro. A foto: `Imgs/Renders/Hospital_Azulejo_No_Escuro.png`.
     - **Ficou de fora:** os decals do URP (a decisão 4 os deixava para uns poucos lugares; as marcas planas bastaram) e o
       grau do material do abandono por cômodo (a administração mais limpa: hoje, o material vai no andar inteiro).
6. **A luz, o som e a narrativa** (sem energia: a luz é só o dia pelas janelas e a lanterna; o que sobra desta parte são o
   som e a narrativa): as luzes de destaque, as que piscam, os pontos de som (o zumbido, as goteiras, os canos, os
   aparelhos apitando) e os documentos.
   - **O som (07/11/2026, a pedido, com o prompt do usuário: "o mais importante é o silêncio"; aprovado: "os sons estão
     legais"; sem a música, a pedido: "vamos deixar sem a música, somente os sons"):** sem energia, nada apita, nada liga,
     nada anuncia. A maior parte do tempo, só os passos e os sons de fora.
     - **O que foi aproveitado:** o `MaestroDaTrilha` (a trilha e o ambiente de fora, de cada jogador, sem rede), o
       `Abafamento` (as paredes e as lajes tiram os agudos), os volumes das Configurações (a música e os sons de ambiente) e
       a voz gravada do zumbi (o `SomZombie1`).
     - **Os sons** (`Ferramentas/hospital_sons.py` do projeto Unity, gerados por código como os outros, mas só com o numpy:
       roda no Python que vem com o Blender, uns 15 s; em `Audio/Hospital`): 21 eventos curtos, mono, cada um com o eco de
       dentro do prédio (o concreto, o azulejo, os corredores): a porta rangendo devagar (3), a goteira de uma a três gotas
       (3), o metal estalando (3), o cano batendo (2), a estrutura rangendo (2), o vento na janela quebrada com o assobio
       (2), os papéis mexidos pelo vento (2), a pancada longe (2) e a maca rolando, com a rodinha guinchando e o tranco no
       fim (2); a voz dos zumbis atrás das portas é a gravada, num trecho e abafada.
     - **Sem a música** (07/11/2026, a pedido: "vamos deixar sem a música, somente os sons"): ela chegou a ser feita (duas
       de 150 s, de piano velho no ré menor da trilha; a primeira tinha "muitos agudos", e a segunda, refeita mais escura,
       também não ficou), e saiu: os arquivos, a `MusicaDoLugar`, os lugares especiais e a tecla M.
     - **O componente** (no jogo, reaproveitável): o `AmbienteSonoro` (um som de cada vez, de um ponto do andar de quem
       escuta, a cada 45 a 120 s; o tipo sorteado pelo peso dele no andar, entre os que têm um ponto de 4 a 45 m; abafado
       pelo que estiver no meio; só com quem escuta dentro do hospital). Dentro, ele pede o silêncio ao maestro
       (`pedeSilencio`, que renova o `MaestroDaTrilha.QuietoAte`): a exploração da trilha não começa (a que estiver tocando
       sai pela saída no fim do bloco) e os sons de fora ficam três vezes mais raros; o perigo da trilha continua igual.
     - **Os pontos** (`SonsDoHospital`, da planta): as portas de folha, as paredes de fora com janela (e a entrada e a
       recepção), as goteiras embaixo das placas do forro caídas, nos úmidos, no subsolo e no 3º, o metal e os canos do
       subsolo, a estrutura nos corredores, os papéis na administração, as macas e as camas dos móveis, os lacrados (a A-01,
       as salas técnicas, o quarto 4) e as câmaras frias. Os pesos por andar fazem a diferença entre eles: no subsolo, os
       canos, as goteiras, o metal e os zumbis abafados; no térreo, as portas, o vento e as goteiras; no 2º, as macas, as
       portas, os zumbis do quarto lacrado e as pancadas longe; no 3º, a estrutura, os papéis e o vento.
     - **Na cena de teste:** o objeto "Sons do hospital" (posto pelo menu da cena); o J toca um som agora.
   - **A narrativa, os documentos (07/11/2026; o plano aprovado: "Pode fazer", sem a lista dos lidos no TAB por enquanto;
     feita em 08/11/2026 e aprovada: "Aprovado, mas tira esse som ao abrir para ler e ao fechar"; a folha abre e fecha
     sem som):** papéis que se leem, espalhados nos
     cômodos certos, que juntos contam o que aconteceu no hospital há 5 anos. O jogo ainda não tem nada de ler (o terminal
     do bunker é a coisa mais parecida), então é um sistema novo, pequeno e reaproveitável (os acampamentos, as bases e as
     outras construções especiais vão poder ter os deles).
     - **A história, ligada à introdução do Dia 0** (a doença que começou no Brasil: a febre, os olhos vermelhos e a
       coceira; uns viraram pedra sob o sol; os outros, 30 dias depois, viraram outra coisa e atacaram; e os primeiros
       casos dos Estados Unidos, no estado de Washington). O Blackwater General recebeu os primeiros casos do condado: o
       primeiro foi um homem que voltou de São Paulo. A direção, por ordem do estado, concentrou ali todos os casos do
       condado e escondeu das famílias e da imprensa (o diagnóstico oficial era "infecção respiratória grave"); os que
       paravam de responder desciam para a A-01, no subsolo. No dia 30, os de cima se levantaram, o hospital caiu, a
       evacuação foi às pressas e o condado foi fechado pela cerca da quarentena (a borda do mapa). **A A-01 continua um
       gancho:** os papéis falam dela (quem desceu, a porta reforçada, a última entrada no registro), mas não dizem o que
       ficou lá dentro.
     - **O que cada andar conta** (uns 5 papéis por andar, 20 no todo; cada um se entende sozinho, a ordem de achar não
       importa):
       - **Térreo, o caos:** o quadro da triagem (os nomes, as horas e os "OBS"), o aviso de evacuação pregado na
         recepção, o registro do segurança (as portas da frente fechadas com gente do lado de fora), o bilhete de uma
         enfermeira para a filha, o pedido das ambulâncias que não voltaram.
       - **2º, o isolamento:** a ficha do paciente do quarto 4 (o homem de São Paulo, dia a dia, até o "não entrar
         sozinho"), o protocolo do isolamento, o diário da enfermeira-chefe, o recado na porta do quarto 4 e a lista da UTI
         (quem foi para a A-01).
       - **3º, o que a administração sabia:** o memorando confidencial do diretor, a ata da reunião (a votação de não
         contar), a ordem do Departamento de Saúde do estado, a carta do diretor que ficou (a última, na sala dele) e o
         e-mail impresso da TI apagando os registros.
       - **Subsolo, a A-01:** o livro do necrotério (os corpos que "não ficaram"), o laudo da autópsia (as placas
         calcificadas crescendo do crânio, como as do Oco), a ordem de serviço de soldar a porta da A-01, o registro de
         entrada dela (a última linha) e o recado do vigia do subsolo.
     - **O tom** (dois de exemplo, como ficariam na tela):
       - *A ficha do quarto 4:* "Paciente: Daniel Costa, 34 anos. Voltou de São Paulo no dia 2. Dia 1: febre de 39,4,
         olhos vermelhos, coceira nos braços e no pescoço. Dia 9: a pele da nuca endureceu em placas; diz que não sente
         mais frio. Dia 17: parou de falar. Fica virado para a janela, mas pediu, por escrito, para fecharmos a persiana: o
         sol 'pesa'. Dia 26: sem pulso no punho. Está de pé. Dia 29: (rabiscado) NÃO ENTRAR SOZINHO. O Dr. Reyes mandou
         lacrar."
       - *O memorando do diretor:* "CONFIDENCIAL. Aos chefes de setor. Recebemos hoje a orientação do estado: até segunda
         ordem, os casos do condado vêm todos para cá. Com as famílias e com a imprensa, ninguém fala em 'pedra': o
         diagnóstico é infecção respiratória grave. Os pacientes que pararem de responder descem para a unidade A-01; só a
         equipe da lista tem acesso. Eu sei o que estou pedindo. — H. Whitaker, diretor."
     - **As decisões (com a recomendação):**
       1. **A língua:** o texto que se lê em **português** (como a interface e o jornal do Dia 0: textos compridos em
          inglês poucos leriam), com o papel do jeito americano na tela (o cabeçalho "BLACKWATER GENERAL HOSPITAL", os
          carimbos, os nomes dos campos dos formulários e os nomes das pessoas em inglês, como as placas). As outras
          opções: tudo em inglês (como as placas e as pichações) ou o original em inglês com a tradução ao lado.
       2. **Como se lê:** o E no papel (a dica "[E] Ler") abre a folha no meio da tela, por cima do jogo, que não para (os
          zumbis continuam vindo; o E ou o ESC fecha); o papel fica no lugar, e todos podem ler. A outra opção: pegar o
          papel como item da mochila e ler pelo inventário.
       3. **Os lidos ficam guardados:** uma lista por jogador (no TAB, a página "Documentos", para ler de novo), no save. A
          outra opção: sem lista (ler de novo só no lugar). **Ficou sem a lista, por enquanto** (a pedido: "não vamos colocar
          essa parte de os papéis lidos irem para o TAB por enquanto"): lê-se de novo no lugar.
       4. **Os modelos:** feitos pelo jogo, no kit (a folha solta, a prancheta, a pasta, o bilhete dobrado, a ficha presa
          no pé da cama e o aviso pregado na parede), no papel velho e amarelado, sem brilho: no escuro, só a lanterna acha.
          A outra opção: os modelos no Blender (o `loot.py`).
       5. **Onde:** em lugares fixos da planta, cada um no cômodo da história dele (na mesa, na cama, na parede, no chão),
          sempre os mesmos (o hospital é feito à mão); nada sorteado.
       6. **A rede:** nada vai pela rede (o lugar é fixo e cada um lê o seu), como os sons.
       7. **O tamanho:** 20 papéis curtos (de 60 a 150 palavras), uns 5 por andar.
       8. **As datas:** sem o ano (o jogo não diz em que ano é): os dias contados ("dia 9") e os meses.
     - **O que foi feito** (08/11/2026; conferido no Play da cena de teste, com um jogador):
       - **Os textos** ficam num arquivo só, fácil de mudar: `Resources/Documentos/Hospital.txt` do projeto Unity (o formato
         está no começo dele: o id, o título, o timbre, o tipo, o carimbo, a letra e, depois do "---", o texto; a linha
         com "~ " foi escrita à mão por cima). Os 20, com a história acima: no térreo, o quadro da triagem, o aviso de
         evacuação, o registro do segurança, o bilhete da enfermeira para a filha e o registro das ambulâncias; no 2º, a
         ficha do quarto 4, o recado ao lado dele ("NÃO ABRA"), o protocolo do isolamento, o diário da enfermeira-chefe e a
         lista da UTI para a A-01; no 3º, o memorando confidencial, a ata da reunião, a ordem do estado, a última carta do
         diretor e o e-mail da TI; no subsolo, o livro do necrotério, o laudo da autópsia (as placas e a fenda do Oco), a
         ordem de soldar a porta da A-01, o registro de entrada dela (o Dr. Reyes desceu sozinho antes da solda) e o
         recado do vigia. As pessoas: o diretor Harold Whitaker, o infectologista Samuel Reyes, a enfermeira-chefe
         Margaret "Peg" Lowell, o paciente Daniel Costa (o primeiro caso, que voltou de São Paulo), a filha dele, Ana, de
         9 anos, e os outros dos papéis.
       - **Os lugares** (`DocumentosDoHospital`, no assembly do mundo): cada papel no cômodo dele, no tampo de um móvel da
         receita (as alturas medidas nas malhas dos prefabs, em ×1,30; nas mesas de escritório, em cima da pilha de
         papéis da mesa, porque o mouse fica no meio), pregado na parede (ao lado da porta do quarto 4, na entrada do
         isolamento, ao lado da porta da A-01) ou no mural da espera, ou caído no chão (o das ambulâncias). O teste do
         hospital ganhou um caso: os 20 postos, cada um num cômodo aberto, e todo papel com o texto (e todo texto com o
         papel).
       - **Os papéis no mundo** (`PapeisDoLugar`, `MalhaDoPapel` e `PapelNaMira`, no `Scripts/Documentos` do projeto
         Unity): feitos pelo jogo, em 7 formas (a folha, a carta, o aviso com duas tiras de fita crepe, a prancheta com o
         prendedor, a pasta parda entreaberta com a folha aparecendo, o bilhete dobrado ao meio e o caderno aberto), com o
         atlas `Resources/Documentos/T_Papeis.png` (desenhado pelo Editor, `MontagemDosPapeis`: o papel velho, amarelado
         nas beiradas, com as manchas de café e a dobra, as linhas de texto de mentira de máquina, de mão e de pincel, o
         timbre com a cruz, a faixa vermelha do aviso, a tabela do formulário, o caderno pautado, a etiqueta e o carimbo
         da pasta) e o material `M_Papeis` (o Simple Lit da URP, fosco). Sem brilho: no escuro, só a lanterna acha. A
         mira acha o papel pelo gatilho dele e passa pela caixa de colisão do móvel embaixo, como no saque: "[E] Ler".
       - **A folha na tela** (`LeituraDePapel`): o papel velho no meio da tela, com o timbre (a cruz e as letras em
         inglês), o tipo, o carimbo vermelho torto (na faixa dele, embaixo do timbre) e o texto na letra do papel: a de
         máquina (o Courier), a de mão (o Ink Free, azul), as linhas do formulário ou do caderno (com a margem vermelha),
         as anotações à mão em vermelho ou azul e o recado de pincel preto, grande. A letra diminui até caber. O jogo não
         para; o E ou o ESC fecha; o barulho de papel ao abrir e ao fechar (gerado por código). No jogo, ela é uma tela do
         menu (o mouse solto e o jogador sem comandos); na cena de teste, ela trava o andar.
       - **Na cena de teste:** o objeto "Papéis do hospital" (posto pelo menu da cena), e o E lê o papel na mira. Ainda
         não estão no mundo (é a parte 7).
       - As fotos: `Imgs/Renders/Hospital_Documentos_Lugares.png` e `_Lugares_2.png` (16 papéis no lugar, com a luz de uma
         lanterna) e `Hospital_Documentos_Folhas.png` (quatro folhas na tela: o memorando, a ficha do quarto 4, o diário e
         o recado).
7. **No jogo:** as portas que abrem e trancam, o saque nos móveis, os pontos e as zonas dos zumbis, o NavMesh por andar e o
   carregamento por andar (como o `BunkerDoMundo`), no mundo sorteado (e depois no mapa fixo).
   - **A primeira fatia, o hospital no mapa (08/11/2026, a pedido: "vamos colocar o hospital no mapa, quero fazer uma build
     para testar com meus amigos, pode deixar sem o loot e zombies e portas, depois fazemos essa parte"; feita, esperando o
     teste com dois jogadores):**
     - **O lugar:** um quarteirão inteiro da maior cidade grande (um lote novo, o `TipoDeLote.Hospital`), escolhido antes
       dos lotes dela: com as 4 ruas em volta, fora da praça do meio, do laboratório e das estradas, onde o terreno do
       hospital (o `PlantaDoHospital.TerrenoNoMundo`, 82,5 × 51,5 m: o prédio, a garagem com a rampa das ambulâncias, a
       varanda com os degraus, as saídas e os pátios) cabe rígido, com a deformação das ruas, de frente para a rua de
       baixo ou para a de cima; o mais perto de 70% do raio do centro (onde ficava o hospital sorteado), o mais plano e
       fora das casas. Os quarteirões da cidade grande têm de 75 a 99 × 52 a 63 m: ele cabe nos de fora do centro. Nas
       sementes 1 a 10 dos três tamanhos, todos os 30 mapas têm o hospital (o teste `OHospitalTemQuarteiraoNosMapas`). A
       cidade dele fica sem o hospital sorteado de antes (as outras cidades grandes continuam com o delas).
     - **O chão:** plano no quarteirão inteiro, na média da altura dele (o `NoPatamar` do traçado), voltando à da calçada
       no primeiro 1,2 m; o buraco do terreno embaixo do corpo (o subsolo), como o do bunker. Sem as plantas, os objetos e
       a construção de longe no lote (no mapa do M, o corpo aparece como construção). O gerador passou para a **versão 13**
       (os mundos salvos antes não abrem).
     - **No mundo** (o `HospitalDoMundo`, no objeto "Mundo" da cena Mundo; o menu Zombie City Cleaner > Mundo > Pôr o
       hospital na cena): até 300 m de alguém, a casca (com a hera e as marcas) e o chão do terreno (o
       `TerrenoDoHospital`: as calçadas em volta do corpo, que tapam a beira do buraco, a praça da frente, o
       estacionamento com as vagas, o acesso e o pátio das ambulâncias, o pátio de serviço com a caçamba, os frades e duas
       ambulâncias na garagem), as áreas de dentro, a área sem zumbis (48 m em volta) e os sons do hospital; por dentro, o
       andar de quem está lá e os vizinhos e, de fora (até 40 m), o térreo e os de cima, cada um com a colisão, as
       marcas, os pisos dos passos, o eco, os móveis (com as portas e as gavetas pela rede da mobília) e os papéis. No
       subsolo, a luz cai para 15% e a neblina para 20% (o `AmbienteDoDia.LuzNoHospital`, à parte da do bunker), e quem
       está lá não volta para a superfície. O F1 tem "O Hospital Principal" (vai para a praça da frente).
     - Conferido no Play do Editor, com um jogador (o Médio 7: o hospital em São Bento): a casca e os andares carregando, o
       subsolo com a luz baixa, os móveis e os papéis; no térreo, uns 8,4 ms por quadro no Editor (95% abaixo de 10,3 ms).
       As fotos: `Imgs/Renders/Hospital_Mundo_Fora.png` (sem a neblina, para ver), `_Vistas.png` e `_Dentro.png`.
   - **A segunda fatia, as portas, os zumbis e o saque (08/11/2026, a pedido: "pode fazer a parte 7"; feita, esperando o
     teste com dois jogadores; as regras abaixo tomadas por mim pela recomendação):**
     - **As portas que abrem:** todas as de folha que a planta deixa abertas (125 folhas, 35 duplas): a de madeira, a de
       metal, a corta-fogo das escadas, a de vidro de dentro, a reforçada e a de vai-e-vem, com a mesma folha da cena de
       teste (o visor, a chapa de inox, a barra antipânico, o aviso amarelo). Ficam paradas as fechadas e as bloqueadas de
       propósito (a A-01, as salas técnicas, o quarto 4, a segunda do isolamento, a saída oeste), as de correr da entrada,
       as do elevador e os portões da garagem. É o sistema das casas (o Plano_Portas.md): o E, a tremida da trancada, a
       gazua e o pé de cabra, a rede, o save e a porta fechada barrando o zumbi; a porta dupla abre, fecha e tranca junta,
       a corta-fogo abre até encostar na parede (172°) e as saídas para fora destrancam por dentro (elas abrem para fora).
     - **Como começam** (pela semente do mundo: em cada mundo, outras): as saídas 80% fechadas e, fechadas, 60% trancadas;
       a corta-fogo 35% e a de vai-e-vem 40% fechadas, nunca trancadas; a reforçada (o raio-X, os quartos do isolamento)
       50% e 30%; as salas que guardam alguma coisa (a medicação, os depósitos, o arquivo, a TI, a direção, a segurança e
       as salas de máquinas) 70% e 50%; os escritórios 50% e 30%; os banheiros e o vestiário 40% e 10%; o resto (os
       consultórios, as enfermarias, a copa...) 30% e 15%. No Médio 7, umas 4 em cada 10 começam fechadas e uma em cada
       sete trancada.
     - **Os zumbis de dentro** (a decisão 9): o povoamento da rua não nasce no corpo do hospital; cada andar ganha os seus
       quando carrega perto de alguém (no anfitrião): as salas fechadas no começo, 4 vezes em 10, com 2 ou 3 lá dentro
       (quem abre a porta acha); a área médica (a emergência, as enfermarias, a UTI, o isolamento, a sala vermelha, a
       cirurgia, o necrotério) 35% com 1 ou 2; os corredores e a espera um a cada uns 140 m²; o serviço 20% e o resto 10%
       com um; no máximo 10 no subsolo, 14 no térreo, 12 no 2º e 8 no 3º (uns 30 a 44 no prédio). Eles nascem onde
       ninguém vê e fora do sol das janelas. Quando o andar sai (o jogador subiu ou desceu), os que não estão atrás de
       ninguém se guardam e voltam ao mesmo lugar na volta (os mortos não); longe de tudo, o hospital se esquece e se
       povoa de novo, como os prédios.
     - **O saque:** nos móveis do hospital (21 modelos novos com lugares medidos: os armários de remédios, os carrinhos de
       parada, os criados, as macas, as bancadas, a geladeira do laboratório, as mesas da administração e da segurança, as
       estantes, as caixas de suprimentos, o vestiário, os carrinhos de limpeza e de roupa) e nos das casas que ele usa
       (as estantes de aço, a geladeira e as mesas da copa). Por andar: o subsolo de 6 a 10 itens, o térreo de 10 a 16, o
       2º de 9 a 14 e o 3º de 6 a 10 (uns 41 por hospital), com as raridades do hospital sorteado (normal 45, incomum 40,
       raro 15). O que cada móvel guarda vem do cômodo: os remédios e os curativos na área médica (o kit médico é o raro
       mais achado), a mesa, a lanterna, a gazua e os comprimidos na administração, a pistola e a munição na segurança e
       na direção, as ferramentas, a luz e o galão nas salas de máquinas, o pano e a limpeza na lavanderia, a roupa da
       equipe (o jaleco, as máscaras, os óculos de proteção, a viseira, o respirador) no vestiário e nos carrinhos de
       roupa, o lanche e a bebida na copa. A medicação e os depósitos pesam mais. As casas não mudam (os lugares e o
       saque delas ficaram iguais).
     - **O F1:** embaixo do "O Hospital Principal", os zumbis de dentro, o saque dos andares carregados e as portas.
     - Conferido nos testes do Editor (as folhas, os zumbis e o saque de 20 sementes; o caminho de todos os cômodos com as
       portas do mundo abertas; o saque das casas do Médio 7 igual) e no Play, com um jogador (o Médio 7): 99 folhas com o
       térreo e os de cima (41 fechadas, 13 trancadas); o E abrindo a corta-fogo; a saída trancada destrancando por dentro,
       com as duas folhas; a trancada da sala de comando tremendo, "Trancada" na dica e arrombada com a gazua (e aberta
       depois de o andar sair e voltar); 34 zumbis nos andares carregados, guardados e de volta no mesmo lugar ao trocar
       de andar; o saque pego pelo anfitrião, que não volta. As fotos: `Imgs/Renders/Hospital_Jogo_Parte7.png` (a de
       vai-e-vem fechada, a corta-fogo aberta, um zumbi no banheiro e o armário de remédios) e `Hospital_Jogo_Saque.png`
       (um pano na estante do depósito).
     - **Fica para depois:** o mapa fixo (na Blackwater City, quando as cidades chegarem) e as ideias de "Fica para
       depois" (o heliponto, os NPCs, o que fica atrás da A-01).
8. **A medida e a validação:** o Profiler e o Frame Debugger dentro e em volta, com dois jogadores (o teste do usuário).
