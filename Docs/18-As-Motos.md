# As motos (06/11/2026)

O jogo ganhou duas motos que se pilotam:
- **a Fat Boy**: a cruiser preta e cromada, larga e pesada;
- **a moto cross**: laranja, branca e roxa, leve e alta.

As duas levam dois (o piloto e a garupa), têm farol, lanterna e buzina, e cada uma tem a sua chave. O detalhe técnico está
na seção "As motos" do `CLAUDE.md`.

## Como se joga

- **Subir:** E na moto ("[E] Pilotar a moto"; com o piloto já nela, "[E] Subir na garupa"). E de novo desce, pelo lado
  esquerdo.
- **Pilotar:** WASD. O S freia; parada, o S é a ré devagar, como empurrar a moto. O Espaço é o freio de trás, que trava a
  roda e faz a traseira escorregar.
- **Farol, buzina e câmera:** L acende o farol e a lanterna, B buzina, e V troca para a câmera de trás.
- **Nas curvas,** a moto inclina sozinha.
- **Partida:** a mesma do carro. Olhe para a ignição e clique.
  - A ignição da Fat Boy fica no tanque, atrás do velocímetro; a da cross, no meio do guidão.
  - Com a chave da moto na mochila, a partida é direta; sem ela, é a ligação direta com o mini-game.
  - As chaves ficam no chão da sala do bunker, ao lado das dos carros: a da Fat Boy tem o chaveiro de couro marrom, e a da
    cross, o laranja.

## A mecânica da moto

**Só a vela se compromete.** A moto não tem pneus que furam, bateria ou radiador. As motos chegam com a vela queimada, e
sem uma vela boa o motor só tosse.

Para trocar a vela:
1. Chegue perto do motor, com a moto parada e ninguém nela, e **agache (C)**. Em pé, a dica é "Agache (C) para mexer no
   motor".
2. Agachado, **E** ("[E] Ver o motor (a vela)") leva a cabeça para o lado do motor.
3. Clique na vela para tirar, ou no lugar vazio (ou numa vela da mochila) para pôr.
4. E ou ESC sai.

As velas se acham no capô dos carros abandonados, como para o carro. A vela gasta com o motor ligado, e quando está gasta
o motor falha. A gasolina gasta como no carro e se repõe com o galão na tampa do tanque.

## Os sons

Cada moto tem o seu motor, a partida e a buzina, gerados por código (`Ferramentas/motos.py`) a partir das medidas de vídeos
de referência:
- **A Fat Boy:** o "batata-batata" grave do V2, com as duas explosões desiguais.
- **A cross:** o monocilíndrico que estala em ponto morto e fica rasgado no giro alto.

O motor toca em camadas: dois laços do giro mais perto misturados, e o giro sobe rápido e cai devagar.

## Onde elas estão

- **No mundo gerado:** na garagem do bunker da família, lado a lado na primeira vaga, a de antes da da perua. Sem o
  bunker, ficam ao lado da laje da partida.
- **Saves antigos:** ganham as motos e as chaves uma vez, ao carregar.
- **Na Demo_Cidade:** na rua, atrás do cupê.

## Como refazer

1. Os modelos vêm do Blender: `Blender/Motos/moto.py` (a Fat Boy) e `Blender/Motos/cross.py` (a cross). Os editáveis e as
   fotos ficam em `D:\teste blender\A Moto` e `A Moto Cross`.
2. Copie os FBX para `Assets/_Game/Art/Veiculos/Motos/` com os nomes `Moto_FatBoy.fbx` e `Moto_Cross.fbx`.
3. As chaves vêm do `Blender/pecas.py`, pela "Montar as peças do carro (e a chave)".
4. Na Unity, rode Zombie City Cleaner > Veículos > "Montar as motos" e depois "Pôr as motos no mundo e na Demo_Cidade".
