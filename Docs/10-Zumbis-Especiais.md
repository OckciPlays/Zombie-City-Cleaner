# 10 – Os zumbis especiais, a lanterna com pilhas e as luzes (01/11/2026)

O que entrou depois do [09](09-Novidades-29-a-31-10.md): os três zumbis especiais (o Brutamonte, o Long e o Corredor, cada
um com 3 roupas), a lanterna que virou item, com as pilhas, e a luz maior da fogueira e da tocha. Para cada coisa: o que faz
no jogo, os números, onde está no código, como testar e o que ficou faltando. Os detalhes técnicos completos estão no
[`CLAUDE.md`](../CLAUDE.md) da raiz, na seção "Os zumbis especiais".

## Como testar

- **F1 (o menu do desenvolvedor), aba Zumbis:** os botões **Corredor**, **Long** e **Brutamonte** criam um na frente do
  jogador (o Corredor a 6 m, o Brutamonte a 9 m e o Long a 18 m). Na aba de itens estão a **Lanterna** e as **Pilhas**.
- **De noite fica mais fácil:** o Long só aparece de noite no jogo normal, e o Brutamonte aparece mais.
- **Os sons são provisórios:** foram feitos por síntese (`Ferramentas/especiais_sons.py`). Para trocar, ponha um `.wav` com o
  mesmo nome em `Assets/_Game/Audio/Resources/Especiais/`:
  - `Brutamonte_Terra`, `Brutamonte_Rugido`, `Brutamonte_Furia`, `Brutamonte_Baque`;
  - `Long_Grito`, `Long_Cuspe`, `Long_Agonia`;
  - `Gosma_Splash`.

## 1. O Brutamonte (o mini-chefe)

Um tanque cego e surdo, que não liga para a luz nem para o sol.

- **Vida:** 500.
- **O casco** tira quase todo o dano: cada tiro vale ×0,3.
- **O olho nas costas** é o único ponto fraco: ×3 (o mesmo da cabeça dos outros), e só vale aberto. Um tiro de sniper (100)
  no olho tira 300; uma pistola (30) tira 90.
- **Aparece:**
  - só um por mundo, brotando da terra (a terra se abre, ele sai e ruge);
  - a cada 3 minutos há uma chance: 30% de noite, 5% de dia;
  - nasce a 30–45 m de um jogador, fora da vista.
- **Volta para a terra:** sem ninguém a menos de 120 m por 1 minuto, ele afunda e guarda a vida que perdeu.
- **A barra de vida** ("O BRUTAMONTE") aparece no alto da tela de quem está a menos de 60 m.
- **O ciclo do olho:**
  1. **Procurando:** o olho aberto vasculha. Ele gira devagar no lugar e anda até onde viu alguém por último.
  2. **Travando:** viu um jogador por 1 s (até 35 m, atrás dele, até 110° de cada lado das costas). Ele ruge e o olho fecha.
  3. **Preparando:** 0,6 s virando para o ponto onde viu o jogador. É a deixa para sair da frente.
  4. **Avançando:** em linha reta até aquele ponto, a 9 m/s. Quem estiver no caminho leva 50, e as barricadas no caminho caem.
  5. **Fúria:** chegou sem bater em nada. Bate os braços por 3 s, e quem estiver a 2,6 m leva 35 a cada 0,6 s.
  6. **Cansado:** 2 s com o olho abrindo devagar, e volta a procurar.
  - **Tonto:** batendo numa parede ou num carro no avanço, fica 4 s tonto, com o olho aberto. É a hora de mirar.
  - **Acertar o olho** enquanto ele procura, está cansado ou está tonto faz ele travar em quem atirou.
- **Onde:**
  - `Zombies/Zumbi.Brutamonte.cs`;
  - os números estão em `AjustesDeZumbi` > `brutamonte` (`TipoDeZumbi.cs`);
  - o nascer e o voltar estão em `ZombieManager.Especiais.cs`;
  - a barra é `UI/BarraDoChefe.cs`.

## 2. O Long (o cuspidor)

Não mata: ajuda as hordas.

- **Vida:** 150; cada braço e perna, 60.
- **Aparece:** só de noite e raro. A cada 2 minutos há 35% de chance, a 35–50 m de um jogador e fora da vista. No máximo 2 ao
  mesmo tempo.
- **A distância:**
  - fica de 14 a 30 m de quem vê;
  - mais perto, ele se afasta;
  - mais longe, chega;
  - sem ver ninguém, mas com alguém a menos de 50 m, vai na direção dele até ver (ele nasce fora da vista).
- **O cuspe:** a cada 6–9 s, a gosma preta do papo, até 26 m.
  - **Primeiro nas luzes:** a fogueira acesa, a tocha na mão de alguém, o farol do carro. A gosma apaga a fogueira e a tocha
    e suja os faróis por 75 s.
  - **Depois no jogador mais perto.**
  - **Quem leva em cheio fica preso:** segure E por 2 s para se soltar.
  - **A poça** (2,2 m, 20 s) apaga a lanterna de quem pisa nela. A lanterna só volta trocando as pilhas.
- **O grito:** ferido sem morrer, ele grita (no máximo a cada 20 s).
  - Os zumbis a até 60 m vêm: os de perto atrás de quem o feriu, os outros vão ver.
  - Chegam mais 5 a 9 de fora da vista, já atrás de quem o feriu.
- **O papo é um ponto fraco:** acertado, estoura. Ele não cospe mais, e a gosma cai em volta dele.
- **Ele odeia a luz:** a lanterna nele, de perto (até 7 m), por 2 s, o petrifica. Ele grita de agonia e se desfaz.
- **Onde:**
  - `Zombies/Zumbi.Long.cs`;
  - a gosma (o voo, a poça, apagar e sujar) está em `ZombieManager.Gosma.cs`;
  - o grito está em `ZombieManager.Especiais.cs`;
  - os números estão em `AjustesDeZumbi` > `cuspidor`.

## 3. O Corredor

- **Vida:** 60; cada braço e perna, 30. É frágil, com o mesmo desmembramento dos Ocos.
- **Velocidade:** corre atrás a 3,8 m/s e dá o bote a 4,6 m/s nos últimos 4 m. É mais rápido que o jogador correndo. Vagando,
  anda 1,6 vez mais rápido que um Oco.
- **O sol** o petrifica mais rápido (5 s). De dia, ele foge para as casas como os Ocos.
- **Aparece** no lugar de um Oco: 2% de dia e 8% de noite (com as hordas).
- **Os sentidos** são os do Oco: de noite e no escuro, ele só percebe de perto ou com a luz nele.
- **Onde:** os números estão em `AjustesDeZumbi` > `corredor`; o resto é o código do Oco, com os números dele
  (`Zumbi.Especiais.cs`).

## 4. As animações dos especiais

Feitas no Blender, pose a pose, no esqueleto do Oco (`Blender/especiais_animacoes.py`, que usa o `equilibrio_oco.py`). Saem
em `Art/Characters/Oco/Oco_AnimacoesEspeciais.fbx` (Humanoid) e são gravadas em cada um dos 21 modelos.

| Clipe | De quem | O que é |
|---|---|---|
| `Correr` | Corredor e Brutamonte | a disparada (laço de 0,67 s, a 3,8 m/s): curvado, os braços em garra. No avanço do Brutamonte, toca 1,8 vez mais rápido |
| `Emergir` | Brutamonte | 3,6 s: a mão direita fura o chão, depois a esquerda; ele se puxa, põe o pé na borda, sobe e ruge |
| `Furia` | Brutamonte | laço de 1,2 s: duas braçadas, de pernas abertas, o tronco girando |
| `Cuspir` | Long | 1,1 s: a cabeça vai para trás e chicoteia para a frente; a gosma sai aos 0,45 s |
| `Gritar` | Long | 1,7 s: se encolhe, explode para trás de braços abertos e treme |

- **No `Zombie.controller`:**
  - o estado **"Andar dos especiais"** (`Pernas` = 7): o andar do Oco com o `Correr` a 3,8 m/s. Só o Corredor e o Brutamonte
    usam, então os Ocos andam como antes;
  - os estados `Emergir`, `Furia`, `Cuspir` e `Gritar` (`Pernas` = 6, sem transição: o código entra neles).
- **Para mexer:**
  1. Mude as chaves no script.
  2. Rode `blender -b --factory-startup --python Blender/especiais_animacoes.py`.
  3. Na Unity, rode o menu **Zombie City Cleaner > Zumbis > Gravar as animações dos especiais (sem remontar)**.

## 5. A lanterna e as pilhas

- **A lanterna virou item** (com modelo 3D e ícone). Sem ela na mochila, o F não acende nada.
  - Ela vai presa no peito: F liga e desliga.
  - **Pilhas novas duram 8 minutos acesa** (mais ou menos uma noite). A barra de desgaste na mochila mostra a carga.
  - Acabando, a luz pisca e apaga.
- **Trocar as pilhas:** no menu do botão direito da lanterna, na mochila. Gasta um par de pilhas.
- **No começo da partida**, uma lanterna e dois pares de pilhas aparecem no chão, na frente de quem nasce.
- **No saque**, a lanterna e as pilhas aparecem pelas casas.
- **Onde:**
  - `Player/InventarioJogador.cs` (a parte "a lanterna e as pilhas");
  - `Player/LanternaJogador.cs`;
  - `Player/TelaDoInventario.cs`;
  - os modelos em `Blender/loot.py`.

## 6. A luz da fogueira e da tocha

- **A fogueira** ilumina até 16 m (era 7), com a luz tremendo.
- **A tocha** ilumina até 14 m (era 8).

## 7. Montar as construções (já existia)

A barricada, a cama e a fogueira já se montam com a sombra (feita em 31/10):

1. Na mochila, clique com o botão direito no kit e escolha **"Montar no chão"**.
2. A sombra segue a mira: verde onde cabe, vermelha onde não cabe.
3. R ou a roda giram; o clique esquerdo monta; o botão direito, G ou ESC desistem.

Isso foi conferido no Play em 01/11: a sombra verde apareceu e o clique montou a fogueira acesa.

## O que ficou faltando

- **O som do Corredor** (um guincho na disparada).
- **Uma animação própria de "tonto" para o Brutamonte:** hoje ele fica parado na pose do Idle.
- **Testar com dois jogadores:** a barra do Brutamonte, a gosma, o cuspe e o grito vistos de outra máquina.
- **O balanço:** os números são os combinados, mas só o jogo vai dizer. Todos ficam no `AjustesDeZumbi`.
