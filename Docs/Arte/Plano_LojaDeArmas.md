# A loja de armas (plano, 08/11/2026)

O pedido (o prompt do usuário e as duas imagens de referência, a fachada e a planta vista de cima): "vamos trabalhar agora em
outra estrutura, a loja de armas, vamos retirar a que temos hoje e fazer uma nova", uma loja de armas americana com o estande
de tiro (a "Gun Store & Shooting Range"), abandonada há cinco anos, sem energia, explorável por inteiro, com a área de vendas,
o escritório, a sala segura, o estande de tiro com 5 baias e o banheiro; a fachada com o letreiro "GUNS & AMMO"; os móveis,
o abandono, o som, a narrativa ambiental, o saque e os zumbis; leve na Unity e reaproveitável em várias cidades, com as
variações (sobretudo os três estados da sala segura). **"Pode seguir fazendo tudo aí e eu testo depois; crie tudo numa
cena nova e depois coloque no mundo quando eu aprovar."**

As referências: a fachada (o tijolo vermelho e o concreto, o letreiro vermelho "GUNS & AMMO" com o fuzil em cima e a estrela,
a faixa "FIREARMS ★ AMMUNITION ★ SHOOTING RANGE", as luminárias pescoço de ganso, a bandeira e a cabeça de cervo pintadas, as
grades nas vitrines, as tábuas em X, a porta de aço com a portinhola, os frades amarelos, as caixas de munição militares na
calçada, a picape largada e a faixa vermelha do lado) e a planta de cima (a sala segura com a porta de cofre e o estoque no
fundo à esquerda, o escritório no fundo, a área de vendas com os balcões de vidro em L, as três gôndolas no meio e o caixa,
e o estande à direita, com os alvos no fundo, as 5 baias e o banheiro embaixo).

## O diagnóstico do projeto

- **As construções são feitas pelo jogo, em código** (o kit de `Scripts/Mundo/Construcoes`): é o caminho do bunker e do
  Hospital Principal, que é o modelo mais parecido (uma construção feita à mão, primeiro numa cena de teste e depois no
  mundo). Por isso a loja **não** segue a hierarquia sugerida no pedido (`GunStore/Structure/...`, um prefab montado à mão),
  e sim a do projeto: a planta (`PlantaDaLojaDeArmas`), a obra (`LojaDeArmas`, a casca e o lado de dentro numa malha cada,
  com a colisão), os móveis (os prefabs da mobília nova, postos por código) e as marcas, as pichações e a hera em malhas à
  parte. A "hierarquia" do pedido vira as partes desse código (a estrutura, a fachada, a área de vendas, o escritório, a sala
  segura, o estande, o banheiro, as marcas, o saque, os zumbis e o som).
- **A loja de armas de hoje** é a das lojas sorteadas (`PlantasDeLoja`, `TipoDeLoja.Armas`: uma por cidade grande, o salão
  com os balcões de alvenaria, o escritório e o depósito atrás da porta de aço, de 1 a 4 andares). A nova a substitui no
  mundo **depois da aprovação**; até lá, o mundo continua com a de hoje.
- **O que já existe e serve:** o kit (as paredes, as aberturas, as lajes, os letreiros com a fonte das construções), a
  planta em retângulos do hospital (as divisas e as bordas: `PlantaDoHospital.Andar`, `Linhas`, `PortasEntre`), o material
  do abandono (o tijolo aparente, a pintura, a ferrugem, a umidade), a hera, as pichações (o atlas tem "LOOTERS WILL BE
  SHOT", "NO AMMO", "STAY OUT", "KEEP OUT", o X da busca, a caveira e as contagens de dias), o atlas das marcas do hospital (o
  sangue seco, os arrastos, as pegadas, a sujeira, a água, o mofo, os tiros, os papéis e os cacos), os sons do hospital (o
  vento na janela quebrada, o metal, a estrutura, os papéis, a porta), os documentos que se leem (o `PapeisDoLugar`), o
  sistema de portas (a gazua e o pé de cabra), o saque nos móveis com as raridades e os zumbis de dentro do hospital (o
  modelo do `ZumbisDoHospital`); e, na mobília nova, o escritório (a mesa, o computador, a cadeira, o arquivo, a estante do
  arquivo, o mural), o banheiro (a privada, a pia, o espelho), as estantes de aço, a bancada, as caixas, o acampamento (o
  colchonete, o saco de dormir, as latas, o fogareiro, as garrafas) e a caixa de papelão.
- **O que não existe:** os balcões de vidro, os painéis perfurados, os expositores de armas longas, as gôndolas de loja, a
  caixa registradora, as baias de tiro, os alvos e os trilhos, o anteparo do fundo do estande, os abafadores, a porta de
  cofre, os cofres de armas, o rack de fuzis, as caixas de munição militares, o carrinho de carga, o palete, a barricada e o
  troféu de cervo. Viram o lote novo da mobília (o `mobilia_loja_armas.py`).

## A planta

As fotos: `Imgs/Renders/LojaDeArmas_Planta.png` (com os nomes e os tamanhos) e as vistas.

1. **O corpo:** 24 × 30 m (720 m²), um pavimento só, o pé-direito de 3,8 m, a laje da cobertura até 4,3 m e a platibanda; na
   frente, a **fachada falsa** subindo até 5,9 m, com o frontão do meio até 6,9 m e o letreiro, como na referência. As portas e as janelas na
   escala das construções (×1,15: a porta de 1,27 × 2,42 m).
2. **Os cômodos** (x de oeste para leste, z da frente para o fundo, em metros):
   - **a área de vendas** (15,6 × 22), com a entrada principal no meio da frente (a porta dupla de vidro de 2,2 m, a grade de
     enrolar arrombada na frente dela) e as vitrines dos dois lados (com as grades, umas tábuas e uns vidros quebrados);
     dentro: o balcão de vidro em L (o braço do fundo, de oeste a leste, com o caixa na ponta perto do escritório, e o braço
     do oeste, ao longo da parede das armas longas), a faixa dos funcionários atrás dele (de 2,7 m no fundo, com as portas do
     estoque e do escritório), as três gôndolas baixas no meio, as prateleiras de munição na parede da frente, os painéis
     perfurados e o troféu na parede do estande, e a porta do estande;
   - **o fundo** (8 m), atrás da faixa dos funcionários: **a sala segura** (6,4 × 8, de concreto, sem janela; só pela porta
     de cofre, do estoque), **o estoque** (5 × 8, a porta para a faixa dos funcionários e a dos fundos, de metal, para o beco)
     e **o escritório** (4,2 × 8, a porta para a faixa dos funcionários e uma janela alta com a grade nos fundos);
   - **a ala do estande** (8,4 m de largura, do lado leste): na frente, **a antessala** do estande (5 × 7: a recepção do
     estande, os abafadores na parede, o quadro das regras, a porta pesada para o estande e a barricada velha), **o
     banheiro** (3,4 × 3,6, com a janelinha alta na fachada) e **o depósito do estande** (3,4 × 3,4: os alvos, os abafadores,
     a limpeza); atrás, **o estande** (8,4 × 23): a área de espera atrás da linha de tiro (3,4 m, com o banco e a mesa do
     instrutor), a linha das 5 baias (1,40 m cada, encostadas na parede do leste) e, do lado oeste, a passagem de 1,08 m com
     o portãozinho aberto ("STAFF ONLY": com as baias de parede a parede, o zumbi não chegava ao fundo do estande), os
     corredores de tiro (16 m) com os trilhos dos alvos
     no teto e os defletores pendurados, e o anteparo do fundo (as chapas inclinadas e a caixa de areia); a saída de
     emergência do lado leste, perto da linha de tiro (para o beco do lado).
3. **Por fora:** a calçada da frente (2,5 m, no nível da porta), o estacionamento (12 m, as vagas pintadas, os frades na frente
   das vitrines), o alambrado nos lados, o beco dos fundos com a caçamba, os paletes e as caixas, a calçada do lado do
   estande com a saída de emergência.

## As decisões (tomadas por mim pela recomendação, com o usuário fora; todas podem mudar na aprovação)

1. **A planta** acima (a área de vendas na frente, o fundo com a sala segura, o estoque e o escritório, e a ala do estande
   do lado leste, com as baias perto da frente e os corredores indo para o fundo). A outra opção: o estande atravessado no
   fundo (os corredores de oeste para leste), que deixa o prédio mais largo que fundo.
2. **Onde vai:** primeiro só numa cena de teste (`Scenes/Exemplos/LojaDeArmas.unity`, no menu Zombie City Cleaner > Exemplos
   > Loja de armas); depois da aprovação, no mundo sorteado no lugar da loja de armas de hoje (uma por cidade grande, num lote
   do comércio com o estacionamento) e, no mapa fixo, quando as cidades chegarem.
3. **Os móveis:** um lote novo no `Mobilia.blend` (o `mobilia_loja_armas.py`, no estilo da mobília nova: a textura, as portas,
   as gavetas e as tampas que abrem e o de longe), reaproveitando o que o escritório, o banheiro, o serviço e o acampamento já
   têm. Postos um por um (a loja é uma só, como o bunker), com as variações pela semente.
4. **As variações** (a mesma construção, outra semente): o estado da sala segura (A saqueada 55%, B parcialmente saqueada 30%,
   C intacta 15%), quais vitrines e balcões estão quebrados, a gôndola tombada, a barricada da entrada, o acampamento dos
   sobreviventes no estande, as janelas tapadas e quebradas, as pichações, a hera, a quantidade de sangue e de sujeira e o
   saque. Na cena de teste, as teclas trocam a semente e o estado da sala segura.
5. **A sala segura nos três estados**, sem três prédios: a porta de cofre (um modelo, como a da câmara fria do bunker) aberta
   e arrombada (A: torta, a fechadura arrancada, as marcas de pé de cabra), entreaberta (B) ou fechada e trancada (C); dentro,
   os mesmos móveis em outro estado (os cofres abertos e vazios, as caixas abertas e jogadas no chão, as estantes vazias no A;
   umas caixas e um cofre fechados no B; tudo fechado e no lugar no C, com a poeira). **No C, o dono se trancou lá dentro**
   (a história): o bilhete na porta ("não abram") e, no mundo, o zumbi dele lá dentro.
6. **Sem energia** (como o hospital): nenhuma luz acende; as luminárias ficam, apagadas. A luz é a de fora pelas janelas e a
   lanterna. **O estande e a sala segura, sem janela, quase escuros:** na cena de teste, a luz cai pelo cômodo da câmera (a
   área de vendas, o escritório, a antessala e o banheiro com a luz de dentro do jogo; o estoque a 35%; o estande a 10%; a
   sala segura a 6%); no mundo, uma luz por cômodo como a do subsolo do hospital (o `AmbienteDoDia`), na parte do mundo.
7. **As portas:** na cena de teste, as folhas paradas (como no hospital) e a porta de cofre abrindo no E (menos no estado C,
   trancada). No mundo, as do sistema de portas (o E, a tremida, a gazua e o pé de cabra, a rede, o save), com a porta de cofre
   trancada no C: **só a gazua abre** (o pé de cabra não força uma porta de cofre), e demora o dobro.
8. **O abandono:** o material do abandono em tudo (o grau "muito"; por dentro, 60%), a hera nas paredes de fora, as marcas do
   atlas do hospital por receitas por cômodo (o sangue onde a história pede: atrás do balcão, onde o dono resistiu, e na
   antessala; os furos de tiro na entrada e no balcão; a sujeira, a água e as infiltrações; os papéis no escritório e no
   caixa; os cacos dos balcões quebrados), e as pichações do atlas na fachada e dentro ("LOOTERS WILL BE SHOT" na fachada, "NO
   AMMO" na vitrine, o X da busca na porta, "STAY OUT" na porta do estande, a contagem dos dias no acampamento). Sem os decals
   do URP.
9. **A narrativa:** uns 7 papéis que se leem (o sistema do hospital): o aviso de fechamento na porta da frente, o bilhete do
   funcionário no escritório, o caderno de vendas do balcão (as filas dos primeiros dias), o relatório do estoque, a lista do
   inventário da sala segura, o diário do sobrevivente do estande e o bilhete do dono na porta do cofre (no C). Os textos em
   português; o papel, os carimbos e os nomes em inglês.
10. **O som:** os sons do hospital (o vento nas janelas quebradas, o metal rangendo, a estrutura estalando, os papéis, uma
    porta), raros, de pontos da loja; sem música, o silêncio é o principal.
11. **O saque** (no mundo; as regras ficam prontas aqui): pouco na área de vendas (os acessórios, a munição avulsa, a lanterna,
    o kit de limpeza), comum no escritório (a lanterna, os comprimidos, uma pistola na gaveta do dono às vezes), o que os
    sobreviventes deixaram no estande, e a sala segura pelo estado (A: quase nada; B: médio; C: as armas, a munição e o raro).
    Algumas lojas vazias de verdade.
12. **Os zumbis** (no mundo): de 3 a 7, pelos pontos (atrás do balcão, no escritório, no estoque, na antessala, no fundo escuro
    do estande); no C, o dono dentro da sala segura.

## As partes (cada uma aprovada antes da próxima; desta vez, a pedido, as da cena feitas juntas)

1. **O plano** (este arquivo).
2. **A loja na cena de teste** (**feita em 08/11/2026, esperando a aprovação**): a planta e a casca pelo kit (as paredes, as
   janelas com as grades, as portas, a laje, a fachada falsa com os letreiros, as luminárias apagadas), o lado de dentro (os
   pisos, os acabamentos: o piso de loja, o concreto do estoque e do estande, os painéis acústicos e os defletores do
   estande, os trilhos dos alvos, o anteparo do fundo, as placas em inglês), o terreno (a calçada, o estacionamento, o
   alambrado, o beco), os modelos novos, a mobília com as variações e os estados da sala segura, o abandono, as pichações,
   os papéis, os sons, o teste do caminho do zumbi e as fotos. Os pontos do saque e dos zumbis ficaram para a parte 3 (as
   regras estão nas decisões 11 e 12: eles dependem dos sistemas do mundo).
3. **No mundo** (depois da aprovação): no lugar da loja de armas de hoje, com as portas que abrem e trancam, o saque nos
   móveis, os zumbis de dentro e a luz dos cômodos escuros.

## A parte 2 (a cena de teste, feita em 08/11/2026)

- **Os modelos novos** (o lote `mobilia_loja_armas.py` do `Mobilia.blend`; veja "Convenções da mobília nova" no `CLAUDE.md`):
  29 em 3 fileiras. **As vendas:** o balcão de vidro (inteiro, com o vidro escuro, e quebrado, com os cacos), o balcão do
  caixa (a registradora aberta, o monitor de tubo, o leitor, o telefone e 3 gavetas), a gôndola de loja e a tombada, o
  painel perfurado (a textura nova `Pegboard`, com os ganchos e as embalagens que sobraram), o expositor das armas longas
  (as duas portas de grade), a estante de munição, a barricada (as mesas, as tábuas e os sacos) e a cabeça de cervo. **O
  estande:** a baia de tiro (a divisória de aço do lado, a prateleira, o número; inteira e quebrada, a divisória torta),
  o carro de alvo pendurado no trilho (a textura nova `Alvos`: a silhueta e os círculos, com os furos; inteiro e rasgado),
  os alvos no chão e o suporte dos abafadores. **O fundo:** a porta de cofre (fechada e arrombada: a folha torta, a
  fechadura arrancada), o cofre de armas (inteiro, com a porta, e arrombado, aberto), o cofre pequeno, o rack de fuzis, a
  caixa militar (a tampa que abre) e a vazia, as latas de munição, o carrinho de carga, o palete, o dispenser de papel e a
  lixeira pequena. Fotos: `Imgs/Renders/LojaDeArmas_Objetos_Vendas.png`, `_Estande.png`, `_Fundo.png` (e `_Aberto`) e a
  folha `LojaDeArmas_Objetos_Folha.png`.
- **No projeto Unity** (os detalhes no `CLAUDE.md` de lá, "A loja de armas"): a planta (`PlantaDaLojaDeArmas`: os cômodos, as
  portas, as janelas, as baias e o sorteio da variação), a obra (`LojaDeArmas`, em quatro arquivos: a casca, a fachada, o
  lado de dentro e o abandono), os móveis (`MoveisDaLojaDeArmas`), os papéis (`DocumentosDaLojaDeArmas` e o texto em
  `Resources/Documentos/LojaDeArmas.txt`), os sons (`SonsDaLojaDeArmas`), a cena (`CenaDaLojaDeArmas` e
  `LojaDeArmasNaCena`), a montagem e as fotos (`MontagemDoExemploDaLojaDeArmas`) e o teste (`LojaDeArmasTestes`).
- **A cena:** `Scenes/Exemplos/LojaDeArmas.unity` (Zombie City Cleaner > Exemplos > Loja de armas; as fotos em Exemplos >
  Fotos da loja de armas). No Play: as setas pulam entre 6 paradas (a frente, a entrada, o caixa, o estoque com a porta de
  cofre, o estande e o beco dos fundos), o P mostra a planta, o R sorteia outra semente, **1, 2 e 3 trocam o estado da sala segura** (A, B ou C;
  0 volta para o da semente), o L liga a lanterna, o J toca um som, o E abre a porta de cofre (menos no C) e lê os papéis.
- **Os números** (uma semente típica): uns 105 móveis, 76 marcas, 15 pichações e uns 37 mil triângulos na loja inteira
  (a casca, o lado de dentro e os móveis); 7 papéis (8 no C, com o bilhete do dono; o diário só com o acampamento); uns 30
  pontos de som.
- **Os textos** (8, em português, com o papel em inglês): o aviso de fechamento na porta, o caderno de vendas do balcão, o
  bilhete do funcionário (o Jimmy), o relatório do estoque, o inventário da sala segura (com a combinação), as regras do
  estande, o diário dos sobreviventes e o bilhete do dono (o Frank Dolan), contando os dias 22 a 30.
- **Os testes do Editor** (`LojaDeArmasTestes`, 5): a planta fecha (os cômodos cobrem o corpo sem se sobrepor e cada porta
  cabe numa parede), os móveis e os papéis existem em 18 variações, e o caminho do zumbi do estacionamento até cada
  pedaço de chão de 0,5 m de cada cômodo, nos três estados (uns 2.100 pedaços), com a sala segura do C sem caminho. O
  `BunkerTestes` e o `HospitalTestes` continuam passando.
- **As fotos:** `Imgs/Renders/LojaDeArmas_Fora.png` (6 vistas: a frente, de três quartos, o letreiro, a ala do estande, o
  beco dos fundos e de cima), `_Dentro.png` (12, com a lanterna), `_SalaSegura_Estados.png` (os três estados, da porta e de
  dentro) e `_Planta.png`.
- **De passagem:** as letras das construções (`Letras`) ganharam o "&" e a estrela ("*"), e a folha do papel só desenha a
  cruz do timbre quando ele é do hospital.
