# A base e o saque dos zumbis e do lixo (24/11/2026)

O pedido do Pedro:
- começar o modo construção, com os muros e tudo, em volta de um núcleo da base;
- o núcleo sobe de nível com troféus dos zumbis especiais;
- os zumbis mortos têm o que saquear, e alguns andam com mochila;
- o E no zumbi e na lixeira abre um inventário, como o de uma pessoa, em vez de entregar os itens direto;
- cada lixeira tem um tamanho diferente.

## O núcleo da base

**O que é:**
- um braseiro grande de sucata, com uma roda de pedras, sempre aceso (a chuva não apaga);
- o kit sai da fabricação: 6 sucatas, 2 peças de metal e 2 toras, com a ferramenta (2 × 2 na mochila);
- monta-se como os outros kits: no inventário, clique direito → "Montar no chão".

**A área:**
- o muro, o portão e a barricada só se montam dentro da área de um núcleo;
- a fogueira, a cama e as armadilhas continuam livres;
- um núcleo não fica dentro da área de outro;
- montando uma peça da base, a área de cada núcleo aparece no chão (uma roda laranja);
- fora da área, a sombra fica vermelha com o aviso "Fora da área de um núcleo da base (monte o núcleo primeiro)".

**Os níveis** (o E no núcleo, com os troféus na mochila):

| Nível | Área (raio) | Para subir ao próximo |
|---|---|---|
| 1 | 12 m | 3 cabeças de Oco |
| 2 | 18 m | 2 gargantas do Long |
| 3 | 26 m | 1 olho do Brutamonte |
| 4 | 35 m | 1 cabeça de chefe |
| 5 | 45 m | (o máximo) |

O fogo e a luz do braseiro crescem com o nível.

**Desmontar:** segurar o E por 2,5 s. O kit volta para a mochila, mas o nível e os troféus gastos se perdem.

## O muro e o portão de toras

**O muro:**
- 3,2 m de largura e 2,4 m de altura, de toras deitadas, com as estacas enterradas no chão;
- a receita: 3 toras e 4 pregos, com a ferramenta;
- vida de 500.

**O portão:**
- tem os batentes e uma folha de toras em pé; o E abre e fecha a folha;
- a receita: 3 toras, 6 pregos e 1 peça de metal, com a ferramenta;
- vida de 380.

**O encaixe:**
- o muro e o portão encaixam na ponta de outro muro ou portão quando a mira chega a 1,4 m do encaixe;
- encaixam em linha ou virados 90°, na quina;
- o giro fica o mais perto do que o jogador escolheu, de 180 em 180 graus: dá para escolher o lado para onde o portão abre.

**Os zumbis:**
- o muro e o portão fechado cortam o NavMesh, e os zumbis encostados batem neles, como na barricada: 2,5 de dano por zumbi por segundo;
- bate quem persegue o jogador e também quem o perdeu de vista atrás do muro e procura ali;
- 3 zumbis levam uns 65 s para derrubar um muro; uma horda de 10, uns 20 s;
- quebrados, sobra uma tora;
- o Brutamonte passa por cima e derruba tudo.

**Desmontar:** segurar o E por 2,5 s. Inteiro (com mais da metade da vida), o kit volta; senão, sobra uma tora.

Os modelos são feitos em código (MalhaDeObra, as cores da paleta) até o Pedro trocar.

## Os troféus

| Troféu | De quem | Chance |
|---|---|---|
| Cabeça de Oco | do Oco que morre com a cabeça | 3% |
| Garganta do Long | do Long | sempre |
| Olho do Brutamonte | do Brutamonte | sempre |
| Cabeça de chefe | dos chefes que morrem de vez (não do Blindado, que se divide, nem do Alongado, que vira o Longo) | sempre |

São itens novos, caixinhas feitas no ItensDaCaca.Base.cs.

## O corpo do zumbi

**O que acontece quando ele morre:**
- o anfitrião cria um recipiente no chão, onde o corpo cai;
- segurar o E por 1,3 s revista o corpo ("Revistando o corpo") e abre a tela do saque, com tudo para pegar.

**Os bolsos:**
- a grade é de 4 × 3;
- 60% dos Ocos e dos Corredores levam de 1 a 3 coisas;
- o que pode vir: água, refrigerante, lanche, comida, pano, roupa velha, algumas roupas, um maço, uma atadura, pilhas...;
- às vezes vem algo do ofício de cada Oco:
  - a enfermeira: remédios, o jaleco, a máscara;
  - o mecânico: as peças, os fios, os pregos, a ferramenta;
  - o executivo: a carteira com dinheiro;
  - e assim também o estudante, o lenhador, a corredora, o vigia e o turista.

**O zumbi de mochila:**
- uns 8% dos Ocos têm uma mochila nas costas: a pequena (60%), a média (30%) ou a grande (10%);
- é o mesmo sorteio em todas as máquinas, pelo Id e pela geração do zumbi;
- morto, a grade é a da mochila, com 3 a 6 coisas de quem sobrevivia: água, comida, remédio, umas balas, materiais;
- a mochila fica no chão, do lado do corpo, e o "Trocar de mochila" funciona.

**Quanto dura:**
- o recipiente fica o mesmo tempo que o corpo (10 min);
- são no máximo 60 ao mesmo tempo (o mais velho sai);
- não vai no save;
- só vira recipiente o corpo que cai a até 150 m de algum jogador (os zumbis que os sobreviventes matam longe não gastam o limite);
- o recipiente leva o Id e a geração do zumbi (o Id volta do pool e, sem a geração, um corpo novo podia abrir o recipiente de um antigo).

## O inventário do lixo

**Como funciona:**
- segurar o E revira o lixo, como antes;
- no fim, em vez de entregar os itens, abre a tela com o que tem dentro;
- depois disso, o E abre a mesma lixeira, sem sortear de novo;
- o que ficou dentro vai no save.

**O tamanho de cada lixeira:**

| Lixeira | Grade | Quantos itens |
|---|---|---|
| A lata de lixo | 3 × 3 | 1 a 2 |
| A lixeira de rodas | 4 × 4 | 1 a 3 |
| As sacolas | 3 × 2, 4 × 3 ou 5 × 3, pelo tanto de sacolas | |
| A caçamba | 8 × 5 | 2 a 4 |

## No F1 (a aba Fauna)

- **"Kits da base e troféus":** o núcleo, 4 muros, o portão e os troféus dos 4 níveis.
- **"Zumbi de mochila na frente":** um Oco com a mochila média a 6 m. A mochila forçada só vale no anfitrião.

## Os arquivos

**Novos:**
- `Loot/ConstrucoesNoMundo.Base.cs`;
- `Loot/ItensDaCaca.Base.cs`;
- `Zombies/Zumbi.Mochila.cs`;
- `Zombies/SaqueDosZumbis.cs`;
- `Player/JogadorRede.Saque.cs`.

**Mudados:**
- `ConstrucoesNoMundo` (com Barricadas e Armadilhas);
- `MontadorDeConstrucao`;
- `MochilasNoChao` e `TelaDoSaque` (os tipos de recipiente);
- `FaunaDoMundo.Lixo` e `LixoNaMira`;
- `Itens` (os itens de 36 a 42) e `Receitas` (3 no fim);
- `Zumbi` (Morrer, Zerar, Preparar, AtualizarRenderers);
- `ArmaJogador` (a mira acha o corpo);
- o save (o tipo e a origem do recipiente).

A versão da rede é a 36.

## Como foi testado (24/11/2026, no Play pelo MCP, semente 7)

- **O lixo:** a lixeira de rodas abriu a tela com a grade 4 × 4, as sacolas com 3 × 2 e a caçamba com 8 × 5 e 4 itens. O E de novo reabre a mesma lixeira; vazia, ela some e fica "Lixo já revirado".
- **O zumbi de mochila:** a mochila aparece nas costas e some com o corpo. O recipiente cai a 0,6 m de onde ele morreu, com a mochila média (8 × 5), 5 itens e $54. O "Pegar tudo" funciona.
- **14 Ocos comuns:**
  - 12 tinham alguma coisa (22 itens), 1 estava de mochila;
  - os nomes saem pelo ofício ("o corpo da enfermeira", "o corpo do mecânico");
  - o Brutamonte deixou o olho, e o Long, a garganta;
  - o zumbi morto a 200 m não virou recipiente.
- **A base:**
  - o muro fora da área é recusado, e o kit fica na mochila;
  - outro núcleo a 10 m é recusado;
  - o 2º muro encaixou a 3,20 m do 1º, o portão a 3,35 m e a quina virou 90°;
  - a área aparece só montando;
  - o núcleo subiu do nível 1 ao 4 com os troféus;
  - o portão abriu (a folha girou 100°);
  - 4 zumbis gastaram 14 da vida do muro em 32 s;
  - desmontar devolveu o kit.
- **Sem teste no Play:** o ajuste depois do teste (os zumbis que procuram também batem, e a vida 500/380).

**Conhecido:** com o jogador colado no muro (a menos de 1 m), o zumbi do outro lado ainda alcança com o golpe. É o mesmo da barricada.

## Fica para depois

- Os modelos de verdade do núcleo, do muro, do portão e dos troféus, no Blender.
- Mais peças da base: a torre, o muro de pedra e a cerca de arame, com o núcleo subindo o que se pode montar.
- Zumbis atacando a base de propósito (a horda que vem atrás do núcleo).
- O recipiente do corpo indo junto quando o corpo escorrega para longe (hoje ele fica onde o zumbi caiu, um pouco para o lado do empurrão).
