# 14 – O save (02/11/2026)

O jogo agora salva. Antes, o Continuar reabria o mesmo mapa do zero. A ideia, nas palavras do Pedro:

- as três formas de salvar entram no jogo, escolhidas ao criar o mundo;
- **cada um leva o seu personagem:** a mochila, o dinheiro, a vida, a fome e a sede ficam no computador de cada jogador e
  vão para qualquer mundo;
- **o mundo fica com quem hospeda:** os itens no chão, os carros, as construções, a hora e o clima;
- o Continuar mostra a lista dos mundos salvos.

Os detalhes técnicos estão no [`CLAUDE.md`](../CLAUDE.md) da raiz, na seção "O save".

## No menu

- **Iniciar novo jogo:** à direita fica o painel do **personagem**. As setas passam pelos personagens salvos neste
  computador e pelo **"Novo personagem"**. O salvo mostra o dinheiro, a mochila e o último mundo, e dá para apagar (com um
  segundo clique). Embaixo da semente, **"Salvar o jogo"**: Automático (5 min), Ao sair ou Só dormindo.
- **Personagem novo:** passa pela tela de criar ao entrar e, num mundo novo, começa pelo Dia 0. **Personagem que já
  existe:** entra direto, com as coisas dele, e pula o Dia 0.
- **Continuar:** a lista dos mundos salvos (o nome, o tamanho, o dia, a hora, o modo e quando salvou), cada um com
  **Jogar** e **Apagar**, e o painel do personagem ao lado. "Jogar local" hospeda sem o código (só na mesma rede). Um
  mundo salvo numa versão anterior do gerador avisa que o mapa pode ter mudado.
- **Entrar com o código de um amigo:** vale o personagem escolhido no painel. As coisas dele vão junto e voltam com ele.

## As três formas de salvar

| Modo | Quando salva |
|---|---|
| Automático | a cada 5 minutos, ao sair da partida e quando o jogo fecha |
| Ao sair | ao sair da partida (ESC > Sair da partida) e quando o jogo fecha |
| Só dormindo | só dormindo: E na sua cama (a que você montou) ou numa cama da casa da família |

- Dormir salva em qualquer modo.
- O menu da partida (ESC) diz como o mundo salva e quando salvou.
- No Dia 0 o jogo não salva: o mundo começa a salvar depois da porta do abrigo.

## O que fica salvo

- **No personagem** (o computador de cada um): o nome, a aparência, o dinheiro, a vida, a fome, a sede, a mochila vestida,
  tudo o que está nela (com o pente de cada arma, o desgaste e as cores das roupas), os atalhos, o pneu nas mãos e as
  roupas vestidas (desde o merge com o saque e as portas).
- **No mundo** (o computador de quem hospeda):
  - o dia e a hora, e o clima;
  - os itens no chão;
  - a perua e o cupê (onde estão, as peças, os pneus, a gasolina e o desgaste);
  - os carros do mundo em que alguém mexeu (as peças e os pneus tirados);
  - as construções (a fogueira continua com o fogo que tinha);
  - as portas e gavetas abertas;
  - os sustos de rastejante já dados;
  - o painel de armas do bunker;
  - onde cada personagem estava. Quem volta aparece ali; quem nunca esteve no mundo nasce na casa da família.
  - os grupos de sobreviventes de cada cidade (quem morreu continua morto) e as mochilas no chão (as deles e as dos
    jogadores que caíram), desde a etapa 2 ([13 – Os sobreviventes](13-Sobreviventes-e-Acampamentos.md));
  - os acampamentos: os hostis, quem morreu, o estoque de cada comerciante e o dia da última reposição (a etapa 3);
  - os carros quebrados: o estado de cada um (esperando, trocando, ajudados, hostis ou mortos), quem morreu, o pneu dado e
    o que faltava da troca (a etapa 4);
  - desde o merge com o saque e as portas (02/11/2026): o saque dos móveis já pego (não volta), as portas e as janelas das
    casas abertas, fechadas, destrancadas ou arrombadas, os itens guardados nas estantes do bunker e as cores das roupas no
    chão e nas mochilas caídas;
  - desde os ajustes da primeira partida (03/11/2026, [15](15-Ajustes-da-Primeira-Partida.md)): o fogo amigo (ligado ou
    desligado, escolhido ao criar o mundo), as bases militares (quem morreu, a geração dos militares e o dia do reforço) e
    as marcas e as anotações do mapa (os pings não ficam). Um save de antes abre com tudo isso vazio (e o fogo amigo
    desligado).

## O dinheiro

- O que sobrou do Dia 0 agora vai com o personagem (antes, ele se perdia na porta do abrigo).
- Aparece no canto de cima da tela da mochila (TAB), ao lado de "DETALHES".
- É o dinheiro que os comerciantes dos acampamentos vão usar (a etapa 3 do plano dos sobreviventes).

## Mudanças que vieram junto

- **Sair da partida não larga mais as coisas no chão:** o personagem leva tudo.
- **Morrer deixa a mochila no chão** (desde a etapa 2): a mochila vestida, com tudo dentro, fica onde a gente caiu, e dá
  para voltar e pegar. A gente volta na cama sem ela, com a roupa e o dinheiro.
- **A cama montada é do personagem, não da conexão.** Na sua cama, E dorme e salva e o clique recolhe; na de outro, E
  recolhe.
- **A pistola e a lanterna do começo** são só do personagem novo.
- **O dia do mundo** agora é contado: mais um a cada meia-noite. Ele aparece na lista do Continuar.

## O F1

O painel **"SAVE"**, embaixo do painel do carro:
- como o mundo salva e quando salvou;
- o personagem e o dinheiro dele;
- **"Salvar agora"** (no anfitrião, em qualquer modo);
- **"+ $ 1.000"**.

## Onde ficam os arquivos

- Os personagens: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Zombie City Cleaner\Personagens\<id>.json`.
- Os mundos: na mesma pasta, em `Mundos\<id>.json`.

São textos (JSON). O jogo grava primeiro num arquivo ao lado e depois troca, para um jogo que fecha no meio não estragar o
save anterior.

## Testado

No Editor, com um jogador, sem erros:
- **Um mundo novo:** um personagem novo, dinheiro, uma mochila média com 4 itens, um machado no chão, o dia 3 às 15h30, a
  perua com 1 L e o jogador 2 m ao lado do nascimento. Ao salvar, sair e entrar pelo save, tudo voltou igual: a posição
  exata, uma chave da perua só e nenhuma lanterna nova no chão.
- **O Dia 0:** com um personagem novo, o jogo não salva no Dia 0. Na porta, os $ 1.000 vão para o personagem, e o mundo e o
  personagem aparecem nas listas.

**Falta testar com dois jogadores:**
- o amigo entrando com o personagem dele;
- o anfitrião saindo com todos dentro;
- um personagem salvo entrando no Dia 0 de outro (as coisas dele chegam depois da porta).
