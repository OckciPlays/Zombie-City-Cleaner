# 07 – Ideias do modo história (para dividir o trabalho)

Este documento junta as ideias do Pedro para o jogo depois da demo e as divide em frentes de trabalho, para os dois poderem dividir as tarefas. **Nada disto está feito ainda.** São ideias em construção: o que estiver marcado como "sugestão" ainda precisa ser combinado entre os dois.

## A ideia, nas palavras do Pedro (resumida)

- **Um jogo grande, de exploração.** A graça é explorar e viver a jornada.
- **Sem lag, acima de tudo.** Vai haver hordas de zumbis, então tudo o que entrar precisa ser leve.
- **Dia 0, o mundo normal:** você vive na cidade com os amigos e descobre que o apocalipse vai começar. É preciso comprar suprimentos com **pouco dinheiro**, escolhendo os melhores recursos.
- **Dia 1, o apocalipse:** de repente, tudo começa, e é preciso sobreviver.
- **Escolhas que mudam o rumo:** decisões importantes levam a caminhos diferentes, numa **árvore de galhos, com vários finais e alternativas**. Os detalhes ainda serão pensados.
- **Dois destinos:**
  - **à esquerda da cidade, o acampamento militar:**
    - lá há uma horda mais forte, com zumbis de colete e capacete e alguns especiais;
    - em troca, há recursos mais raros e armas melhores.
  - **à direita, um acampamento mais seguro:** com mais gente, porém menos recursos.
- **A aventura de verdade é o caminho até lá.** Há duas formas de ir:
  - **de carro:** na garagem de casa há um carro quebrado há anos. Dá para consertá-lo, mas é a escolha mais difícil e que pede mais recursos.
    - As peças vêm de outros carros da cidade: peças, rodas, pneus e gasolina. Buscá-las é complicado com os zumbis atrás.
    - **O carro faz barulho e atrai os zumbis.**
  - **a pé, pela floresta:** no caminho há acampamentos para explorar, com munição, água e comida.
    - Há menos zumbis, porque não há o barulho do carro.
    - O risco de morrer é maior, porque o carro não protege.
- O amigo também quer um **modo história**, e é isso que estas ideias formam.

## O que já existe e ajuda

- **A cidade da demo** (5 casas, o prédio, a praça e o bunker), com 258 zumbis leves (LOD estilo Dead Rising: os de longe quase não custam).
- **12 carros abandonados** na rua, já pensados para virar fonte de peças.
- **As 5 armas** (pistola, submetralhadora, rifle, espingarda e sniper), o desmembramento e o ciclo de dia e noite.
- **O online** com até 4 jogadores (um é o anfitrião).
- **A Parte 7 da demo** (o saque, a mochila, a despensa, dormir e salvar), que ainda falta, é a base de quase tudo abaixo: sem saque e inventário não há peças, comida nem munição para juntar.

## As frentes de trabalho

Cada frente diz o que é, de que depende e o tamanho aproximado (P pequeno, M médio, G grande).

| # | Frente | O que é | Depende de | Tamanho |
|---|---|---|---|---|
| A | **Saque e inventário** (a Parte 7) | abrir armários, geladeiras e carros; a mochila e a despensa; dormir e salvar | — | G |
| B | **Dia 0: a cidade normal** | a mesma cidade sem zumbis e com gente; lojas para comprar com dinheiro limitado; a notícia do apocalipse; a virada para o Dia 1 | A (os itens comprados vão para a mochila ou a despensa) | G |
| C | **Escolhas e finais** | a árvore de decisões: o que o grupo escolheu fica guardado e muda o que acontece depois; os finais | — (dá para começar no papel) | M no código, G na escrita |
| D | **O carro** | consertar o carro da garagem com peças (rodas, pneus, gasolina…) tiradas de outros carros; dirigir; o barulho atraindo os zumbis | A | G |
| E | **A floresta e os acampamentos do caminho** | o mapa entre a cidade e os destinos, a pé; acampamentos com munição, água e comida | A | G |
| F | **O acampamento militar** (esquerda) | o lugar, os zumbis de colete e capacete, os especiais, as armas e os recursos raros | E ou D (para chegar lá) | G |
| G | **O acampamento seguro** (direita) | o lugar, as pessoas, o que elas oferecem (pouco recurso, mais segurança) | E ou D; C (conversas e escolhas) | M |
| H | **Arte nova** | zumbi militar (colete e capacete por cima dos modelos atuais), pessoas do Dia 0 e dos acampamentos, lojas, floresta, barracas, o carro da garagem | — | G |

### Sugestões técnicas (para continuar sem lag)

- **Mapas separados:** a floresta e cada acampamento podem ser cenas próprias, carregadas na hora de sair da cidade, em vez de um mapa gigante só. Assim cada lugar tem os seus zumbis e a cidade não pesa no resto.
- **Zumbi militar barato:** usar os mesmos 4 modelos e esqueletos, com o colete e o capacete como peças presas aos ossos. Sugestão de regra: o capacete aguenta o primeiro tiro na cabeça (e cai), e o colete diminui o dano no tronco.
- **Barulho do carro:** o jogo já tem o "barulho do tiro" que chama os zumbis (até 35 m). O motor pode usar o mesmo sistema, com um alcance maior.
- **Floresta:** árvores e pinheiros do mesmo material único da cidade (a paleta), o que já mostrou ser leve.
- **Cada frente nova se mede com o benchmark** (F9), antes e depois, como tem sido feito.

## Sugestão de divisão entre os dois

A regra mais importante do projeto para duas pessoas: **não mexer os dois na mesma cena ao mesmo tempo** (juntar cenas da Unity no git dá conflito difícil). Por isso a divisão sugerida separa quem mexe na cidade de quem trabalha em coisas novas:

- **Pessoa 1, a cidade:** A (saque e inventário, a Parte 7) e depois D (o carro), que acontecem na cena da cidade.
- **Pessoa 2, o que é novo:** C (a árvore de escolhas: primeiro no papel, depois o sistema), o roteiro do Dia 0 (B) e a floresta (E), numa cena nova.
- **Os dois, juntos:** a arte nova (H) e os acampamentos (F e G), quando o caminho até eles existir.

Cada frente vira uma branch própria a partir da `feature/demo-cidade`, com pull request, como no [06 – Como colaborar](06-Como-Colaborar.md).

## Ordem sugerida

1. **Terminar a demo:** a Parte 7 (saque, mochila, dormir e salvar) e a Parte 9 (o fechamento), como no [plano](02-Plano-da-Demo.md). Elas são a base do resto.
2. **Ao mesmo tempo, no papel:** escrever a árvore de escolhas e os finais (C) e desenhar os mapas da floresta e dos acampamentos.
3. **O caminho:** a floresta a pé (E) e o carro (D).
4. **Os destinos:** o acampamento militar (F) e o seguro (G).
5. **O Dia 0** (B), que fica no começo do jogo, mas precisa do saque, da loja e das escolhas prontos.

## O que os dois precisam decidir

- **A família ou os amigos?** A visão atual ([01 – Visão do jogo](01-Visao-do-Jogo.md)) é a família no bunker, com a base crescendo (bunker, casa, comunidade). A ideia nova fala de amigos e de viajar até um acampamento. O modo história substitui essa visão ou é um modo à parte?
- **Quantos finais**, e o que leva a cada um.
- **Quem escolhe no online:** o anfitrião decide, ou o grupo vota?
- **O que acontece quando alguém morre:** volta no acampamento, perde itens, ou a partida acaba?
- **O dinheiro do Dia 0:** quanto, o que dá para comprar, e se o dinheiro ainda serve depois do apocalipse.
- **Os zumbis especiais** do acampamento militar: quais são e o que cada um faz.
- **O tamanho do mundo:** só a cidade, a floresta e os dois acampamentos, ou mais lugares depois.
