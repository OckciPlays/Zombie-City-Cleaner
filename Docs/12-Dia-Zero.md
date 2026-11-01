# 12 – O Dia 0: o começo do jogo (01/11/2026)

O jogo novo agora começa no **Dia 0**: o mundo normal, antes do apocalipse. A ideia, nas palavras do Pedro:

- vocês estão no sofá, numa festa, e a TV mostra a doença chegando: os primeiros sintomas no Brasil, o homem que virou
  pedra no sol, os ataques depois de 30 dias e, por fim, os primeiros casos nos Estados Unidos, no nosso estado;
- vocês saem para a rua: um dia normal, com os vizinhos regando o quintal, cortando a grama, conversando e acenando, e os
  carros passando;
- cada um tem **1.000 dólares** (as economias) para gastar nas lojas;
- quando alguém fecha a **porta do abrigo** na nossa casa, a tela mostra **"5 anos depois"**, e o jogo continua no mundo
  de hoje, abandonado. É a mesma casa, e as compras estão no chão dela.

Os detalhes técnicos estão no [`CLAUDE.md`](../CLAUDE.md) da raiz, na seção "O Dia 0".

## Como funciona

- **Iniciar novo jogo** (no menu, "Criar o mundo" ou "Criar local") começa pelo Dia 0. O **Continuar** vai direto para o
  mundo (é o mesmo mapa de antes).
- **A cena é separada** (`Scenes/DiaZero.unity`): ela é carregada por cima da cena do mundo, e o mundo só é gerado depois
  da porta (a tela dos "5 anos depois" cobre a geração).
- **O lobby na casa:** o jogo começa na sala da nossa casa, com a festa tocando, e ninguém sai da casa ainda (quem passa da
  porta volta para dentro). Em cima da tela: quantos jogadores já estão na casa. Assim todo mundo entra antes da
  história começar.
- **A introdução** (uns 90 s): quando todos chegaram, alguém aperta **E na TV da sala**, e o jornal começa **para todos ao
  mesmo tempo** (quem entrar no meio vê dali em diante). Não mostra os amigos (cada um tem o seu personagem). Tem a trilha
  estilo cinema (sem voz) e o texto contando a história nas faixas pretas. **Enter ou Espaço pula** (só para quem
  apertou).
- **A rua é liberada** quando todos acabaram de ver (ou pularam) o jornal. Depois disso, E na TV mostra o jornal de novo,
  só para quem apertou. O F1 tem "Liberar a rua (sem a TV)".
- **A rua:** 10 casas (a nossa é a segunda do lado norte), o mercado, a farmácia, a loja de armas, a de ferragens e
  autopeças, a de roupas e o posto no fim. As pontas estão fechadas pela obra (o jogador não sai da rua; os carros passam).
- **O dia** fica parado às 10h30, com sol e cores vivas (o mundo do apocalipse continua com a neblina pesada). A fome e a
  sede não descem no Dia 0. Ninguém começa com a pistola: só o que for comprado.

## As compras

- **Nas prateleiras:** mirando um produto, **E** põe um no carrinho. O aviso embaixo da mira mostra o total.
- **No caixa:** E no balcão do caixa abre a tela do caixa: o que está no carrinho (com o – e o +), o total, o seu
  dinheiro e o **Pagar**. Pago, as compras **aparecem no chão da nossa casa** (na sala, na cozinha e no corredor).
- **Sair da loja sem pagar** devolve o carrinho (e tira as roupas provadas).
- **A loja de roupas:** E numa roupa **prova** (você passa a vestir na hora; V mostra de fora). As peças provadas entram no
  caixa. Tem capacetes, máscaras, óculos, camisas, jaquetas, coletes, cintos, calças, botas e luvas nas prateleiras, e a
  **seção das mochilas**: a mesa no meio da loja, com a placa "MOCHILAS" e a pequena ($ 30), a média ($ 60) e a grande
  ($ 100) em dois andares (E põe no carrinho).
- **No canto da tela:** o dinheiro, quantas compras já estão em casa e o carrinho da loja.
- **Os preços** (em dólares) estão em `Scripts/DiaZero/PrecosDoDiaZero.cs`. Exemplos: lata de comida 3, água 2, kit
  médico 45, pistola 450, espingarda 400, submetralhadora 650 (mais barata que o rifle: ela é pior), rifle 850, sniper
  1.000, caixa de munição 20 a 30, pneu 90, galão com gasolina 32, colete tático 180, máscara de gás 120.
- **O dinheiro que sobra** fica guardado (para os acampamentos, mais para a frente); por enquanto não aparece depois.

## A porta do abrigo

- Fica no fim do corredor da nossa casa (a porta de aço com a placa amarela). O bunker de verdade é do Gustavo: por
  enquanto, a porta é só a passagem.
- **E** mostra quem ainda está fora de casa; **E de novo** fecha. O dia acaba para todos.
- **Quem estiver fora de casa** quando a porta fechar **perde as compras** (a pedido). Todo mundo vai para o mundo do
  mesmo jeito.
- **5 anos depois:** cada um aparece na sala da casa da família, às 8h da manhã, com as compras no chão, no mesmo lugar em
  que estavam no Dia 0.

## Os NPCs

- **28 pessoas**, vestidas pelo sorteio de roupas do Gustavo (os tipos: civil, trabalhador, idoso, estudante, médico,
  policial):
  - nos quintais: regando o canteiro com a mangueira, cortando a grama com o cortador (com o som do motor) e conversando
    em dupla;
  - nas calçadas: passeando e correndo;
  - nas lojas: os atendentes no caixa; no mercado, o faxineiro com o esfregão e duas pessoas fazendo compras.
- **Acenam** para quem chega perto e **olham** para o jogador.
- **5 doentes** (o faxineiro e o caixa do mercado, o atendente do posto, alguém na calçada e alguém regando): os olhos
  vermelhos em volta, a tosse com a mão na boca e a coceira no braço, como num resfriado.
- **Os carros** passam pela rua nas duas mãos.
- Nada disso vai pela rede: cada máquina calcula onde cada um está pelo relógio da partida (todos veem igual).

## O F1 no Dia 0

O painel **"DIA 0"** (no lugar do painel do mundo):
- o seu dinheiro, o quanto gastou e as compras em casa;
- **"Pular o Dia 0"**: todos vão para o mundo com as compras;
- **"+ $ 1.000"**;
- **ir para** a nossa casa ou para a frente de cada loja.

## A nossa casa é sempre a mesma

A casa da família agora é a mesma em todo mundo (antes, ela era sorteada pelo lote). Por isso, a versão do gerador passou
para 10: o **Continuar** de um mundo criado antes abre o mesmo mapa, mas com a casa nova.

## Provisório e para depois

- **As animações dos NPCs** (regar, cortar a grama, conversar, acenar, tossir, coçar, esfregar) são poses por cima das
  animações que já existiam (parado, andando, correndo). Dá para trocar por animações feitas no Blender depois.
- **Os sons** da tosse, do cortador, da mangueira, dos carros e da festa são gerados por código. A tosse pode ser gravada.
- **A loja de roupas** tem as roupas só nas prateleiras das paredes; no meio, só a mesa das mochilas.
- **O carro na nossa garagem** no Dia 0 é um carro qualquer da rua (não a perua).
- **Testado** só com um jogador, no Editor. Com dois, falta testar: quem entra no meio do Dia 0, o caixa do cliente e a
  porta fechando com alguém fora.
- **O porta-malas do carro** para levar as compras (a ideia do Pedro) fica para depois.
