# O personagem do jogador (plano, 31/10/2026)

O pedido: quando um jogador começa no servidor, ele monta o personagem dele numa tela como a da imagem do usuário (`Imgs/Personagem_Criacao_Referencia.webp`). Ele escolhe:
- o nome;
- o sexo e a cor da pele;
- o cabelo, a barba e a cor dos dois;
- uma camiseta simples e a calça.

O calçado é o mesmo para todos: o tênis de cano (o All Star que já existe). O resto (as jaquetas, os outros calçados, as máscaras, os óculos, os cintos, os coldres...) é achado no mapa. **O nome aparece em cima da cabeça dele no jogo.**

É o "personagem principal vestido no jogo", que estava no fim do `Plano_Roupas.md`, e as roupas como itens que se pegam (os modelos já estão nas mesas da cena das variações).

## O que tem hoje

- **O jogador** (o `Jogador.prefab` do projeto Unity) é o personagem principal em blocos, sem roupa: o corpo e a textura do usuário, com a cueca.
  - Tem o esqueleto de 47 ossos e todas as animações (as armas, os itens, os punhos, o carro, a janela e o taco).
  - Tem os braços de primeira pessoa e a terceira pessoa no V.
  - O menu escolhe o "personagem" por um número que vai pela rede, e hoje só existe um.
- **As roupas e os NPCs:**
  - 90 peças, no mesmo esqueleto do principal (todas as animações servem nelas);
  - o corpo da mulher e os 6 tons de pele;
  - as faces cobertas somem, e as cores trocam por peça;
  - 51 NPCs prontos e o sorteio por tipo;
  - cada roupa e acessório já é um item, mas o jogo ainda não os pega.
- **O inventário:**
  - os bolsos e as mochilas, o coldre e a alça da arma;
  - na tela (TAB), o retrato do jogador com os 4 lugares da armadura apagados (Cabeça, Colete, Calças e Botas), mais a Mochila e o Coldre.
  - Pegar uma mochila veste; uma nova troca a vestida, que cai aos pés.
- **O mundo ainda não é salvo:** cada partida começa do zero. Os serviços da Unity entram com um perfil anônimo sorteado a cada vez, então ainda não há como reconhecer o mesmo jogador de uma partida para a outra.

## Como vai funcionar (a proposta)

1. **O corpo do jogador vira o corpo das roupas.**
   - O esqueleto, as armas na mão, os itens e as animações continuam os de hoje.
   - O corpo do principal sai, e no lugar dele entram o corpo dos NPCs e as peças, presos aos mesmos ossos (os nomes são os mesmos).
   - Tudo o que funciona hoje continua igual, só que vestido: as armas, a comida, o carro, a janela e o taco.
   - A pele e o corpo (o do homem ou o da mulher) vêm da aparência.
2. **Os braços de primeira pessoa** ficam com a cor da pele escolhida e, na mulher, mais finos.
3. **A aparência vai pela rede** no lugar do número do personagem:
   - o nome, o sexo, a pele, o cabelo, a barba, a cor deles, a camiseta e a calça (umas dezenas de bytes, escritos pelo dono quando ele entra);
   - quem entra depois recebe a dos outros.
   - As roupas achadas vão pelo inventário, escritas pelo anfitrião, como a mochila de hoje.
4. **A tela de criar o personagem** segue a imagem.
   - O personagem fica no meio, num cenário escuro, com uma luz por cima.
   - À esquerda, o sexo, a cor da pele, o cabelo e a barba (com a cor deles).
   - À direita, a camiseta e a calça.
   - Em cima, o campo do nome.
   - Embaixo: "Girar" (arrastar o mouse), "R: aleatório" e "Confirmar".
   - Cada opção tem a miniatura dela, feita pela Unity a partir da própria peça, na cor dela.
   - O visual é o das outras telas do jogo, no estilo da imagem: os painéis escuros, o destaque vermelho e as letras condensadas.
5. **O nome em cima da cabeça:** na tela de cada jogador, em cima dos outros (o dele não), menor com a distância.
6. **As roupas no mapa:**
   - Pegar uma roupa (E) veste na hora.
   - Na tela do inventário, os lugares da roupa ficam em volta do retrato (no lugar dos 4 apagados da armadura): a cabeça, os óculos, a máscara, o tronco, por cima, o colete, a cintura, as pernas, os pés e as mãos.
   - Lá dá para ver o que está vestido, tirar e largar.
   - Cada roupa no chão leva as cores dela.
7. **As mangas na primeira pessoa:** com uma jaqueta, uma camisa de manga comprida ou luvas, os braços de primeira pessoa mostram as mangas e as luvas (hoje eles são sempre a pele).

## As decisões (com a recomendação)

**Respondidas (31/10/2026: "Vamos lá"):** a 2, a 3, a 4, a 5 e a 6 pela recomendação; a 1, a 7, a 8 e a 9 mudaram:
- **1. O personagem é de cada partida:** ele é criado uma vez, ao entrar na partida, e não muda mais nela; outra partida pede outro personagem. O menu não ganha "Personagem".
- **7. As roupas vão para o inventário:** pegar uma roupa a põe na mochila (ou nos bolsos), se houver espaço, e cada roupa ocupa 1 célula. Vestir é na tela do inventário. Em volta do retrato do jogador ficam os lugares da roupa: o chapéu, os óculos, a máscara, a camiseta, o colete ou a jaqueta (não dá para usar os dois juntos), o coldre, a calça e o sapato, e a luva (pedida logo depois). Faltava o cinto (o da polícia, o de ferramentas e o tático: só enfeite, mas precisa de um lugar para vestir): com ele, 10 lugares, 5 de cada lado (a proposta: à esquerda o chapéu, os óculos, a máscara, a camiseta e as luvas; à direita o colete ou a jaqueta, o cinto, o coldre, a calça e o sapato). A mochila continua embaixo, como hoje; o cabelo, a barba e os cílios vêm da criação do personagem.
- **8. As roupas ainda não ficam espalhadas pelo mapa:** só se pegam pelo menu do desenvolvedor, num menu novo, o F3, com as roupas e os outros itens (o F1 já está grande demais).
- **9. Os cintos, por enquanto, são só enfeite:** não aumentam o espaço.

O texto abaixo é o da proposta.

1. **Quando a tela aparece:** ao entrar numa partida pela primeira vez neste PC, antes de o jogador aparecer no mundo. O personagem fica guardado no PC: nas próximas partidas, ele entra direto, e o menu ganha "Personagem" para mudar. Quando o mundo passar a ser salvo, o personagem passa a ser de cada mundo, com o que ele achou. A outra opção: a tela em toda partida, já com o último personagem escolhido.
2. **A roupa do começo:**
   - a camiseta em 8 cores: branca, preta, cinza, vermelha, verde, azul-marinho, bege e vinho;
   - a calça: o jeans em 3 lavagens e a cargo preta, marrom, cáqui e verde-oliva;
   - o tênis de cano, preto, para todos.

   A outra opção: mais peças na escolha (a regata, o top, a camiseta gola V, a xadrez da imagem).
3. **O cabelo e a barba:**
   - Todos os cabelos servem nos dois sexos (a cabeça é a mesma): os 7, mais o careca.
   - A barba (a cheia, a curta, o bigode e o cavanhaque) só no homem.
   - Uma cor só para o cabelo, a barba e as sobrancelhas, de 8 tons: preto, castanho-escuro, castanho, castanho-claro, ruivo, loiro, grisalho e branco.

   A outra opção: a cor da barba separada da do cabelo.
4. **A pele:** os 6 tons que já existem (a imagem mostra 5).
5. **O nome em cima da cabeça:** até 16 letras, nos outros jogadores, até 30 m, só quando dá para ver o jogador (não atravessa as paredes). A outra opção: atravessar as paredes, para achar o amigo.
6. **A aba "Equipamento" da imagem:** sai, porque o equipamento é achado no mapa. A tela fica só com a aparência e o nome.
7. **Pegar uma roupa:**
   - Ela veste na hora. Se o lugar já tem uma, as duas trocam, e a vestida cai aos pés (como a mochila).
   - Tirar, na tela do inventário, larga no chão. A roupa não ocupa a grade da mochila.
   - Dá para ficar sem a camiseta ou sem a calça (de cueca, como o corpo do principal).

   A outra opção: guardar as roupas na mochila, ocupando as células.
8. **Onde as roupas aparecem:** junto com o saque (a etapa 5 do `Plano_Mobilia.md`): nos guarda-roupas, nas cômodas e nas lojas, cada lugar com as dele (o jaleco no hospital, o uniforme da polícia nas delegacias, o capacete nas obras), como a ideia "o uniforme pelo lugar" do `Plano_Roupas.md`. Até o saque existir, o F1 cria as roupas, para testar.
9. **Os cintos** (do lote 6, "itens que vão aumentar os espaços"): cada cinto soma uma fileira de células à grade (os bolsos de 4 × 2 viram 4 × 3; a mochila também ganha uma). O da polícia leva também uma arma curta, como o coldre.

Fica para depois:
- **A roupa com efeito**, outra ideia do `Plano_Roupas.md`: o colete e a jaqueta de couro protegem, e o capacete protege a cabeça. Por enquanto, as roupas são só a aparência (e os cintos, o espaço).
- **Os NPCs no mundo.**

## As partes (cada uma aprovada antes da próxima)

1. **O jogador vestido:**
   - o corpo das roupas no jogador, com a roupa do começo (uma aparência sorteada, para testar antes da tela existir);
   - todas as animações, as armas e os itens como hoje;
   - os braços de primeira pessoa com a pele;
   - o retrato do TAB e o boneco do F1 vestidos;
   - a aparência pela rede, no lugar do número do personagem.

   Conferido com dois jogadores pelo usuário: cada um vê o outro vestido.
   - **O estado (31/10/2026): aprovado ("Testado, siga").**
     - **O corpo:** no prefab do jogador (`Editor/MontagemDaRede` do projeto Unity), o corpo do `Personagem_PrincipalBloco.fbx` fica desligado, e no lugar dele entram o corpo das roupas e as 90 peças do `Roupas.fbx`, num filho "Roupas" do corpo, cada uma presa aos ossos dele pelo nome (o esqueleto é o mesmo), com a `Vestimenta`. O esqueleto, as armas e os itens da mão, os controladores e as animações continuam os do personagem principal.
     - **A aparência** (`AparenciaDoJogador`: o nome, o sexo, a pele, o cabelo, a cor dos pelos, a barba, a camiseta e a calça, uns 40 bytes) vai pela rede, escrita pelo dono quando ele nasce; por enquanto, sorteada ("Jogador 1", "Jogador 2"...), até a tela de criar. As opções são as das decisões: os 7 cabelos e o careca, as 4 barbas, as 8 cores dos pelos, os 6 tons de pele, a camiseta em 8 cores, o jeans em 3 lavagens e a cargo em 4 cores, e o tênis de cano preto para todos.
     - **Os braços de primeira pessoa** pegam a pele (eles são só pele: a cor do material vira a pele pedida dividida pela da textura). Na mulher, os braços de primeira pessoa ainda são os do homem: ficam mais finos junto com as mangas (a parte 5), que também mexem neles no Blender.
     - **O retrato do TAB e o boneco do F1** vestem a cópia do corpo como o jogador; no retrato, as roupas com a luz fixa dele.
     - Conferido no Play (na cena da sinuca, hospedando): a mulher de rabo de cavalo baixo, camiseta branca, jeans claro e o tênis, e, trocando a aparência pela rede, o homem de pele escura, cabelo cacheado, barba cheia, camiseta vermelha e cargo marrom, com a pistola na mão e as mãos de primeira pessoa escuras; o retrato vestido; sem erro nem aviso. Uns 5.400 a 6.300 triângulos vestido (o corpo de antes tinha uns 3.900).
     - As fotos: `Imgs/Renders/Personagem_P1_Mulher.png` (de frente, de lado e o retrato do TAB), `_Homem.png` e `_Bracos.png`.
2. **A tela de criar o personagem:** o cenário, os painéis, as miniaturas, o nome, girar, aleatório e confirmar; ela aparece ao entrar na partida, uma vez (a decisão 1 respondida).
   - **O estado (31/10/2026): feito, esperando a aprovação.**
     - **Quando:** o jogador entra na partida (hospedando ou entrando) e a tela abre na hora, antes de ele aparecer no mundo. Até ele confirmar, ninguém vê o corpo dele nem a arma da mão, os zumbis não o veem e os comandos do jogo ficam parados (o TAB, o F1, o M e o V também). ESC abre o menu da partida por cima dela (de onde se sai), mas ela não fecha sem criar. Outra partida, outro personagem. O robô de teste (`-bot`) entra sorteado, sem a tela. A escolha dos retratos saiu do menu.
     - **A tela** (como a imagem do usuário: os painéis escuros, o destaque vermelho e as letras condensadas da Bahnschrift, a do Windows): "CRIAR PERSONAGEM" em cima; à esquerda, o nome (até 16 letras), o sexo (homem ou mulher), a cor da pele (os 6 tons), o cabelo (os 7 e o careca, de 4 em 4 com as setas), a cor do cabelo e da barba (os 8 tons) e a barba (só no homem: sem, a curta, a cheia, o bigode e o cavanhaque); à direita, a camiseta (as 8 cores) e a calça (o jeans e a cargo, as 7), com o nome da escolhida embaixo; no meio, o personagem, que gira arrastando o mouse; embaixo, "R: aleatório" (a tecla ou o botão: sorteia tudo, menos o nome) e "Confirmar" (só com o nome escrito). A aba "Equipamento" da imagem saiu (a decisão 6).
     - **O personagem e as miniaturas:** uma cópia do corpo do jogador longe do mundo, parada, com as roupas na luz fixa do retrato do inventário (igual de dia, de noite e no bunker), vestida a cada escolha. As miniaturas são fotos dessa cópia: o cabelo e a barba na cabeça (com a pele e a cor escolhidas, refeitas quando elas mudam), a camiseta e a calça sozinhas.
     - Conferido no Play (na cena da sinuca, hospedando): a tela abre sozinha, a escolha veste o personagem do meio, o nome comprido é cortado em 16 letras, e o "Confirmar" cria: a tela fecha, o corpo e a pistola aparecem no mundo e os comandos voltam; sem erros.
     - A foto: `Imgs/Renders/Personagem_P2_Tela.png`.
3. **O nome em cima da cabeça.**
4. **As roupas como itens:**
   - pegar (para a mochila, 1 célula), vestir, trocar, tirar e largar;
   - os lugares da roupa na tela do inventário, 5 de cada lado do retrato (com a luva e o cinto);
   - as roupas no chão e na mochila com as cores delas, pela rede;
   - o F3, com as roupas e os outros itens.

   Onde elas aparecem no mundo vem com o saque.
5. **As mangas e as luvas na primeira pessoa.**
6. **Depois:** salvar o personagem e as roupas com o mundo (quando o mundo for salvo).
