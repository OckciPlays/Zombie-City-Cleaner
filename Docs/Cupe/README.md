# O cupê (segundo carro que se dirige) — 29/10/2026

Um cupê fastback dos anos 70 (inspirado nas fotos do usuário, sem marca nem emblema), **preto com as faixas cinza** (a
faixa larga no capô, com a tomada de ar preta e os pinos, e um friso cinza dos lados). Tem 2 portas compridas, faróis
redondos com refletor, a grade preta de lado a lado, as luzes de estacionamento quadradas e os para-choques cromados
finos com as garras pretas. Atrás, as duas lanternas compridas e a tampa do tanque no meio. As rodas são de aço pretas,
com pneus de letras brancas. O interior é de vinil cinza-escuro.

É dirigível como a perua: tem a mesma mecânica (motor, chave e ligação direta, peças do capô, pneus, macaco, porta-malas,
rede, sons e luzes). A perua não mudou.

## Números

| | Perua v2 | **Cupê** |
|---|---:|---:|
| Triângulos (com 4 rodas) | 9.672 | **8.204** |
| Vértices (com 4 rodas) | 17.951 | 14.600 |
| Carroceria / porta / roda | 5.260 / 392 / 600 | 4.524 / 392 / 600 |
| Modelo "alto" (fonte do bake) | 31.716 (a v1) | 20.700 |
| Texturas | 2 atlas de 2048 (cor, MS, normal) | 2 atlas de 2048 (cor, MS, normal) |

Como na perua v2, os detalhes miúdos (frisos, barras da grade, gomos dos bancos, letras do pneu, furos e parafusos da
roda, pinos do capô, placa) estão na textura, projetados do modelo alto.

## Como ele entra no jogo

- **Prefab:** `Assets/_Game/Prefabs/Veiculos/Cupe.prefab`, uma cópia do `Carro.prefab` (a perua) feita pela
  `Editor/Cupe/MontagemDoCupe.cs` (menu **Zombie City Cleaner > Veículos > Montar o cupê**). A física é a mesma da perua
  (1.150 kg e os 4 WheelColliders iguais: o chassi é o mesmo, com 2,72 m de entre-eixos e rodas de 0,30 m). Mudam o
  visual e o que depende da forma:
  - a colisão: a parte de baixo, a cabine do teto reto e as duas caixas inclinadas (o para-brisa e o vidro de trás do
    fastback);
  - os 4 lugares e as saídas, os gatilhos das portas, do capô, do porta-malas e das rodas, e o NavMeshObstacle;
  - o lugar do motorista: o volante, os mostradores e a ignição ficam 8 cm mais baixos que na perua;
  - as luzes de dentro e os faróis;
  - o cofre do motor, 3 cm mais baixo (o capô é mais baixo);
  - o porta-malas: a tampa deitada abre 60°, com o estepe e o macaco no piso.
- **As 2 portas e só 2 lugares, os da frente** (29/10/2026, a pedido: com um gatilho por metade da porta comprida, a
  mira ia ora para o volante, ora para o banco de trás, e o Pedro decidiu "esse carro preto só vai ter apenas dois
  lugares"): `Carro.lugaresUsados` = 2 (o anfitrião recusa os bancos de trás); a mira na porta é um gatilho só, de
  ponta a ponta, que leva ao banco da frente daquele lado. Os gatilhos dos bancos de trás ficam desligados, e os bancos
  continuam desenhados, vazios. Testado: a mira em 7 pontos ao longo de cada porta dá sempre a porta da frente
  ("[E] Dirigir" na esquerda, "[E] Entrar no carro" na direita); o pedido do banco de trás é recusado.
- **As janelas de lado inclinam para dentro** (29/10/2026, a pedido: "as portas estão abertas, sem ser fechadas"): a
  janela da porta e o friso ficavam em pé, 5 cm para fora da beirada do teto e 2 cm abaixo dele, e de fora parecia a
  porta entreaberta. Agora a janela da porta, o vidro de trás dela e as colunas B e C inclinam 8 cm por metro acima da
  cintura (`TOMBO` no `cupe.py`) e o vidro da porta sobe até embaixo do teto (`Y_TOPO_JANELA`): o friso encosta na
  beirada do teto. O modelo e as texturas foram refeitos.
- **Mais rápido que a perua** (29/10/2026, a pedido: "precisa chegar a 110 km de velocidade"): `velocidadeMaxima` 42 m/s,
  `torque` 1.000 N·m e `fimDasMarchas` 32 (a perua: 22, 700 e 23), postos pela montagem
  (`MontagemDoCupe.VelocidadeMaxima`, `Torque` e `FimDasMarchas`). A força do motor cai a zero na `velocidadeMaxima` e o
  atrito segura antes. Medido numa pista reta (pneus e vela bons): **113,6 km/h** no máximo, 100 km/h em 12 s, 110 em
  ~20 s (com 33 e 950 ficava em 93 km/h). Com o pneu furado do começo da partida ele puxa para um lado e não passa de
  uns 50 km/h. O som das marchas imaginárias vai até o `fimDasMarchas`.
- **A chave:** o cupê não tem chave (`chaveDoVeiculo` = Nada) e só liga com a ligação direta, "roubando o carro". A chave
  da perua não serve nele.
- **Estado inicial:** igual ao da perua (vela queimada, bateria a 30%, radiador a 25%, um pneu furado, estepe e macaco no
  porta-malas). Dá para ajustar no Inspector do prefab.
- **No mundo gerado (cena Mundo):** campo novo e opcional `MundoDaPartida.segundoCarro`. O anfitrião cria o cupê ao lado
  da laje da partida, no primeiro lugar dos dois lados que esteja a mais de 5,5 m da perua e sem nada no caminho (no
  primeiro teste ele tinha nascido encostado nela). Vazio, o jogo fica como antes. A `MontagemDoMundo` também liga o cupê,
  se o prefab existir.
- **Na Demo_Cidade:** está na rua, 6,5 m atrás da perua.
- **Rede:** o cupê está na `DefaultNetworkPrefabs`, com identificador próprio (diferente do da perua).

## Arquivos

**Novos**
- `Blender/Cupe/cupe.py`: os modelos alto e leve, o UV, o bake, a composição e a exportação. Reaproveita a `Malha`, os
  materiais, o painel e o volante da `perua_nova.py`, e a `MalhaLeve` da `perua_nova2.py`, sem mudar nenhum dos dois.
- `Blender/Cupe/rodar.py`: as etapas, num Blender em segundo plano. `foto.py`: fotos de conferência. `Cupe.blend`: o
  arquivo editável.
- `Assets/_Game/Art/Veiculos/Cupe/`: `Cupe.fbx` (peças `Cupe_*`), `Texturas/` e `Materiais/` (`Cupe_Lataria`,
  `_Interior`, `_Vidro`, `_Farol`, `_Lanterna`, todos URP Lit).
- `Assets/_Game/Scripts/Editor/Cupe/MontagemDoCupe.cs`.
- `Assets/_Game/Prefabs/Veiculos/Cupe.prefab`.
- `Docs/Cupe/`: este relatório e as capturas.

**Modificados**
- `Assets/_Game/Scripts/World/MundoDaPartida.cs`: o campo `segundoCarro` e o método `CriarSegundoCarro`, chamado no fim
  do `CriarCarro`. O resto do método é de outra sessão (a chave da perua) e não foi mexido.
- `Assets/_Game/Scripts/Editor/MontagemDoMundo.cs`: liga o cupê ao `MundoDaPartida` e à lista da rede.
- `Assets/DefaultNetworkPrefabs.asset`: o cupê na lista.
- `Assets/_Game/Scenes/Mundo.unity`: o `segundoCarro`.
- `Assets/_Game/Scenes/Demo_Cidade.unity`: o cupê atrás da perua.

## Como refazer

1. `blender -b --factory-startup --python Blender/Cupe/rodar.py -- construir` (cria o `Cupe.blend`).
2. Para cada canal (`cor`, `aspereza`, `metal`, `normal`, `ao`) e atlas (`A` e `B`):
   `blender -b --factory-startup Blender/Cupe/Cupe.blend --python Blender/Cupe/rodar.py -- assar <canal> <atlas>`. Um
   atlas por processo gasta metade da memória. Com a Unity e outro Blender abertos, a RAM livre chegou a 0,4 GB e alguns
   passes falharam e foram repetidos. Na CPU: com a Unity aberta, a GPU fica sem memória.
3. `... --python Blender/Cupe/rodar.py -- compor_exportar`.
4. Na Unity: **Zombie City Cleaner > Veículos > Montar o cupê**. Rode de novo depois de um "Montar veículos", porque o
   cupê copia o prefab da perua.

## Testes

**Feitos (Unity 6000.6.5, Editor)**
- Compilação sem erros; Console sem erros nem avisos no Play.
- Visão do motorista: 0 de 75 raios do olho até a ignição e os mostradores tapados; 9 cm do olho até o forro.
- **Demo_Cidade (Play, host local, 1 jogador):** os dois carros nascem (o cupê com a malha nova, 1.150 kg, 4 rodas no
  chão); entrar no banco de trás (lugar 2) pelo gatilho da metade de trás da porta e sair; o capô abre 62° e a vela entra
  (0 → 100%); entrar no volante; a ligação direta (sem chave), pegar, ligar; dirigir para a frente e de ré, virando, com
  os faróis acesos, rodas visuais sobre as físicas (0,0000 m) e freio de mão. Na frente ele encostou na perua estacionada
  6,5 m adiante (parado de propósito ali); de ré andou livre. Também: o porta-malas (a tampa sobe 60°), pegar o macaco,
  apoiar na roda dianteira direita e erguer (só aquele canto; a roda a ~26 cm do chão).
- **Mundo (Play, host local, mundo sorteado):** os dois carros nascem, de pé e parados; com a escolha do lugar
  corrigida, o cupê ficou a 6,1 m da perua, sem encostar.
- A perua continua igual (o prefab dela não foi salvo pela montagem do cupê).

**Não testados**
- Multijogador com 2 ou mais jogadores (os componentes de rede são os da perua; o identificador do prefab é novo).
- O mini-game com o mouse de verdade e o teclado: os comandos foram dados pela API do carro, com o `PassageiroDoCarro`
  desligado por alguns segundos para dirigir.
- Os lugares 1 e 3, trocar a roda no cupê, o tiro no pneu e o atropelamento: o código é o mesmo da perua e as rodas e a
  frente estão no mesmo lugar.

## Limites e próximos passos

- A massa e o chassi são os da perua; o motor é mais forte e a máxima, maior (veja acima). Para mexer, mude as
  constantes da `MontagemDoCupe` e rode a montagem de novo (o Inspector do prefab é refeito por ela).
- O cupê não tem uma chave própria. Uma "chave do cupê" seria um item novo em `Loot/Itens.cs`, arquivo que está sendo
  mexido por outra sessão agora.
- De muito perto, as bordas finas projetadas (as faixas cinza, os frisos) mostram os pixels, como na perua v2.
