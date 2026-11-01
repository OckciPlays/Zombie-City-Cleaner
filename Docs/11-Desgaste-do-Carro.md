# 11 – O desgaste do carro e a gasolina (01/11/2026)

O carro agora se gasta com o uso: os pneus, a vela, o radiador e a gasolina. A ideia combinada com o Pedro foi **não deixar
tudo quebrar de uma vez**. Se a falha viesse de um limite de km, os 4 pneus novos estourariam juntos. Por isso:

- o desgaste é lento e diferente em cada peça;
- a falha é um sorteio, cuja chance cresce com o desgaste;
- depois de uma falha, o carro passa um tempo sem outra (um problema por vez).

Os detalhes técnicos estão no [`CLAUDE.md`](../CLAUDE.md) da raiz, na seção "O desgaste do carro e a gasolina".

## Como testar

- **F1, painel "Carro (teste)"** (embaixo do painel do Oco):
  - mostra o carro mais perto (até 25 m): os pneus, a gasolina, a vela, o radiador e a carência;
  - os botões provocam cada falha: furo lento, estourar um pneu, pneus a 30%, vazar o radiador, vela a 25% e gasolina
    1 L (a reserva, quase no fim);
  - e consertam tudo de uma vez: pneus novos, motor novo (a vela, a bateria e o radiador a 100%) e tanque cheio;
  - "sem carência" faz a próxima falha poder vir já.
- **F1, Materiais:** o **galão de gasolina**.
- **A perua começa sem vela boa e com o radiador ruim** (de propósito, desde antes), e com o pneu dianteiro esquerdo
  rasgado. Para dirigir no teste, use "Motor novo" e "Pneus novos".

## 1. Os pneus

- **O desgaste:** rodando, cada pneu gasta.
  - Um pneu de resistência média vai de 100% a careca em uns **60 km**.
  - **Cada pneu tem uma resistência sorteada** quando é posto no carro (de 0,7 a 1,3): os 4 nunca ficam iguais.
  - Os da frente gastam mais (×1,2) que os de trás (×0,85).
  - **Derrapar** (frear forte, o freio de mão, curvas rápidas, patinar) gasta muito mais.
  - **Fora do asfalto** (longe das estradas e fora das cidades) gasta ×1,6.
  - O desgaste sozinho **para em 12%**: careca, o pneu não fura por gasto. Quem fura são os furos.
- **Os furos são sorteados**, por km rodado e cada pneu por si.
  - Um pneu bom fura, em média, a cada 90 km; um gasto (30% ou menos), a cada 7 km.
  - Com os 4 pneus bons, isso dá um furo a cada meia hora de direção, mais ou menos.
  - **Atropelar um zumbi** tem 4% de chance de furar o pneu da frente daquele lado (os ossos).
- **Um problema por vez:** depois de qualquer falha (um furo, o radiador vazando), o carro passa **3 minutos dirigindo**
  sem outra sorteada. O tiro, a batida e a gosma do Long não esperam.
- **Os dois tipos de furo:**
  - **o furo lento** (o mais comum): o pneu murcha em 40 a 60 s. Dá para ouvir o ar escapando (todos ouvem), o carro
    começa a escorregar e, no fim, puxa para o lado. Dá tempo de escolher onde parar;
  - **o estouro:** na hora. É mais provável com o pneu gasto e acima de 65 km/h. O barulho chama os zumbis em volta, como
    um tiro, e o pneu rasga (0%: sem remendo).
- **Rodar com o pneu furado** acaba com ele: cada 100 m tira 3%, até rasgar.
- **Consertar:**
  - **trocar pelo estepe**, como já funcionava: o macaco e o estepe no porta-malas;
  - **remendar:** o pneu furado (de 1 a 9%), fora do carro, gasta 1 kit de reparo e fica com 40%:
    - **nas mãos** (sem mochila, o pneu de 3 × 3 vai para as mãos): **R** remenda; o aviso embaixo, à direita, mostra
      "PNEU FURADO NAS MÃOS · R: REMENDAR";
    - **na mochila:** no botão direito, "Remendar o pneu";
    - o furo de bala também tem remendo (fica em 5%);
  - **o pneu rasgado** (0%) não tem remendo: só trocando. Os carros abandonados têm pneus para tirar com o macaco.

## 2. O motor

- **A vela** gasta com o motor ligado: uma vela de resistência média dura umas **2,5 horas** de motor ligado.
  - Abaixo de 35%, o motor **engasga** de vez em quando: perde a força por meio segundo, com a tossida. Quanto pior a
    vela, mais seguido.
  - Abaixo de 10%, não pega mais (como antes).
- **O radiador** gasta devagar (umas **5 horas** de motor ligado).
  - Ele pode **começar a vazar**: sorteado (raro no radiador bom, até 6 vezes mais no gasto) ou numa batida forte de frente.
  - Vazando, perde 10% por minuto de motor ligado. O motor esquenta cada vez mais rápido até ferver, e ouve-se o vapor.
  - Trocar o radiador pelo capô acaba com o vazamento.
- **A bateria** continua como era: a partida e os faróis gastam a carga, e o motor ligado recarrega.

## 3. As batidas

- Conta a batida da lataria a partir de uns 18 km/h contra uma parede (5 m/s de mudança de velocidade):
  - **de frente:** o radiador amassa e pode começar a vazar. A 21 km/h perde uns 4% (10% de chance de vazar); a 50 km/h,
    uns 36%, e quase sempre vaza;
  - **do lado de uma roda:** o pneu pode estourar.
- A roda presa num meio-fio não conta como batida.

## 4. A gasolina

- **O tanque:** 45 litros na perua e 40 no cupê. A perua começa com 68%.
- **O gasto** vem do giro do motor: 0,12 L por minuto parado, até 2,4 L por minuto no giro máximo.
  - Dirigindo normal, o tanque cheio dura uns 40 minutos de direção; acelerando sempre, uns 20.
- **A reserva** (menos de 3 L): o motor engasga de vez em quando.
- **Sem gasolina:** o motor apaga e não pega ("acabou a gasolina").
- **O ponteiro** do painel mostra o tanque de verdade. No HUD do motorista aparecem "GASOLINA NA RESERVA" e
  "SEM GASOLINA".

## 5. O galão de gasolina

- Um item de saque novo: **o galão de plástico vermelho de 10 litros** (2 × 2 na mochila).
- **Abastecer:** na tampa do tanque (do lado esquerdo, atrás; na perua há um disco escuro), a mira mostra quanto há no
  tanque. Com um galão com gasolina na mochila, **E** despeja o que couber (o mais cheio primeiro).
- O galão vazio continua na mochila: quando existir o posto, ele se enche lá.
- **A barra** do galão na mochila mostra quanto ele tem; os detalhes mostram os litros.
- **Onde aparece:** como o resto do saque, por enquanto pelo F1. O saque nas casas ainda não existe.

## 6. Os avisos

Para o motorista, quando a coisa começa:
- "O pneu traseiro direito está murchando: pare para trocar";
- "O pneu ... estourou!" ou "esvaziou";
- "O radiador está vazando: o motor vai esquentar";
- "A gasolina está na reserva" e "Acabou a gasolina";
- "O motor está falhando: a vela está gasta" (ou "a gasolina está acabando").

No HUD, embaixo da velocidade, aparecem "PNEU MURCHANDO", "RADIADOR VAZANDO" e as linhas da gasolina, junto com as que já
existiam.

**Os sons novos são provisórios**, gerados por código como os outros do carro: o ar do pneu, o vapor do radiador e a
gasolina caindo do galão. Para mudar, veja o `SomDoCarro.cs`.

## Os números

Todos ficam em **`AjustesDoDesgaste`**, no componente `Carro` de cada prefab (`Prefabs/Veiculos/Carro` e `Cupe`), no grupo
"O desgaste e a gasolina". Dá para ajustar no Inspector, sem mexer no código.

## O que ficou para depois

- **O posto de gasolina:** encher o galão e o carro.
- **Tirar a gasolina dos carros abandonados.**
- **O saque nas casas** (o galão entra junto com o resto).
- **Testar com dois jogadores:** o motorista num cliente (o desgaste vai pela rede a cada segundo).
