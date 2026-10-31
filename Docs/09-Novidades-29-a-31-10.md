# 09 – O que mudou de 29 a 31/10/2026 (para o Gustavo)

Resumo de tudo o que entrou no projeto depois do commit `a541f00` (29/10, 21h: o menu do jogo, os gráficos, a chave da
perua e o cupê), escrito para o Gustavo e para o assistente dele se situarem. Para cada coisa: o que faz no jogo, onde está no
código, como testar e o que ficou faltando. Os detalhes técnicos completos estão no [`CLAUDE.md`](../CLAUDE.md) da raiz, nas
seções citadas. As armas têm o próprio resumo em [`Docs/Armas/README.md`](Armas/README.md) e o saque, em
[`Docs/Loot/README.md`](Loot/README.md).

## Antes de abrir o projeto

- **Puxe com o LFS:** `git lfs install` e `git pull`. Entram muitos FBX, `.blend` e PNG novos (as armas, os ícones, o saque,
  as capturas da documentação).
- **Não precisa remontar nada para jogar:** os prefabs, os controladores e os clipes já vêm prontos. As montagens só
  precisam rodar de novo se você mudar a fonte (um FBX seu, um script do Blender).
- **Os seus FBX continuam sendo a base:** os braços de primeira pessoa e o corpo do jogador ainda saem da sua pasta de arte.
  As armas novas entram por cima deles, pela montagem: a malha nova vai presa aos mesmos ossos (`gun`, `slide`, `mag`). Se você
  reexportar os braços ou o corpo, rode **Zombie City Cleaner > Montar armas** e **Montar jogador e rede** de novo.

## 1. As armas de fogo com visual novo (29 e 30/10)

- **No jogo agora:**
  - **pistola v1:** a Fort-17, realista, com textura;
  - **rifle v3:** a M4A1;
  - **espingarda v2:** a antiga melhorada, estilizada, em cores chapadas;
  - **sniper v3 leve:** a Mosin-Nagant;
  - **submetralhadora v3:** a MP5K.

  As outras versões (v1 realistas, v2 estilizadas e a pistola enferrujada) continuam no projeto, fora do jogo.
- **Onde:**
  - os modelos estão em `Assets/_Game/Art/Armas/` (`Modelos/`, `Texturas/`, `Materiais/`, `Resources/`);
  - os scripts do Blender estão em `Blender/Armas/` (cada arma tem o `.py` dela e o `.blend` editável);
  - os 4 modelos de base que o Pedro mandou estão em `Blender/Armas/Fontes/`;
  - na Unity, `Editor/Armas/ArmasNovas.cs`; a lista `ArmasNovas.Ativas` diz qual versão de cada arma vai para o jogo;
  - menus: **Zombie City Cleaner > Armas > Pôr as armas novas** e **Montar a pistola nova**.
- **Material próprio:** as armas novas não usam a `Paleta.png`. `Itens.MaterialProprio(modelo)` carrega o material delas de
  `Resources/`, e o chão, o painel do bunker, as costas e o retrato do inventário usam esse material.
- **Faltando:**
  - a animação do ferrolho da Mosin no corpo (a mão puxa uns 20 cm atrás da alavanca);
  - a licença dos 4 modelos de base: não veio nos arquivos, então confirmar antes de publicar.

## 2. As armas brancas (31/10)

- **As cinco armas:**
  - **faca:** baioneta M9, numa mão; o golpe é o soco direito com a faca;
  - **machado:** nas duas mãos; decepa um braço ou uma perna com um golpe;
  - **bastão com pregos:** nas duas mãos, com o golpe do machado;
  - **lança:** nas duas mãos, estocada com 2,1 m de alcance;
  - **tocha:** na mão direita, com chama e luz; queima em uns 10 minutos e some.

  As três últimas saem da fabricação.
- **Onde:**
  - os modelos vêm de `Blender/Armas/faca.py`, `machado.py`, `bastao.py`, `lanca.py` e `tocha.py`; os braços são os dos
    punhos, com a arma presa ao `hand.R`;
  - os números de cada uma (dano, alcance, intervalo, desgaste, corte) ficam em `Loot/Itens.cs`, na tabela `DadosDaBranca`;
  - o golpe é feito em `Player/ArmaJogador.cs` (`Golpear`).
- **Faltando para você (Gustavo):**
  - as animações do corpo (terceira pessoa): os outros jogadores veem o soco direito com o machado, o bastão ou a lança na mão;
  - os sons próprios de cada arma.

## 3. Vida e dano na escala de 100 (31/10)

O Pedro combinou esses números antes de mexer, pensando nos zumbis especiais que virão (cada tipo terá a sua vida).

- **A vida do Oco agora é 100.** Antes era 6, contada em tiros de pistola.
  - Cada parte do corpo multiplica o dano: a cabeça ×3; os braços e as pernas ×0,5.
  - Cada braço e cada perna tem 50 de vida. Quando a vida acaba, o membro cai.
  - Os números ficam em `AjustesDeZumbi` (`vidaDoCorpo`, `multiplicadorNaCabeca`, `danoNosMembros`, `vidaDoMembro`).
- **Armas de fogo** (entre parênteses, quantos acertos matam: corpo / cabeça):

  | Arma | Dano |
  |---|---|
  | Pistola | 15 (7 / 3) |
  | Submetralhadora | 18 (6 / 2) |
  | Rifle | 25 (4 / 2) |
  | Sniper | 50 (2 / 1) |
  | Espingarda, até 5 m | 115 (1 tiro mata) |
  | Espingarda, até 15 m | 30 |
  | Espingarda, mais longe | 17 |

  Os números ficam em `Combat/Armas.cs`.
- **Armas brancas e soco:**

  | Arma | Dano |
  |---|---|
  | Soco | 6 |
  | Tocha | 8 |
  | Faca | 15 |
  | Bastão | 20 |
  | Lança | 20 |
  | Machado | 25 |

  O machado corta em dobro (`corte` 2).
- **A cabeça só explode com tiro ou com o que corta:**
  - explode com o tiro, a faca, o machado e a lança, no acerto que mata;
  - o soco, o bastão e a tocha (`corte` 0) matam com a cabeça no lugar;
  - antes do acerto que mata, a cabeça leva dano sem explodir.
- **Atropelamento:**
  - cada km/h acima de 10 tira 5 de vida, sem teto;
  - o Oco morre a partir de 30 km/h;
  - fica em `Vehicles/Carro.cs` (`DanoPorKmh`, `KmhSemDano`).
- **Na rede:**
  - o acerto leva o `corte` num byte, em décimos (`JogadorRede.AcertarZumbi`, `ZombieManager.Acertar`);
  - o anfitrião limita o dano de 0 a 1.000 e o corte de 0 a 4.

## 4. O saque, a fabricação e as construções (31/10)

- **O inventário mudou:**
  - acabaram a alça, o coldre e o limite de armas: tudo vai na grade, as armas também;
  - **nas costas aparecem só as armas dos atalhos 1, 2 e 3**;
  - itens iguais empilham numa célula;
  - **o botão direito num item** abre: comer, beber ou usar; pôr ou tirar dos atalhos; reparar; desmontar; montar no chão;
    largar.
- **Uns 40 itens novos**, em `Loot/Itens.cs`:
  - comida e bebida;
  - remédios, que devolvem vida (`VidaJogador.Curar`);
  - materiais: madeira, pano, corda, pregos, peças de metal, fios, kit de reparo, vara de pesca, isca, entre outros;
  - kits de montar: fogueira, cama e barricada.

  Os modelos, os ícones e as fotos saem do `Blender/loot.py`.
- **Fabricação** (`Loot/Receitas.cs`, a aba **Criar** do inventário):
  - algumas receitas funcionam em qualquer lugar, outras só perto de uma bancada (`Loot/Bancada.cs`; no bunker da
    Demo_Cidade, embaixo do painel de armas);
  - **desmontar** transforma um item em material: a roupa vira pano, a tora vira madeira, a arma vira sucata.
- **Construções** (`Loot/ConstrucoesNoMundo.cs`, na rede; `Player/MontadorDeConstrucao.cs` para posicionar):
  - **fogueira:** cura até 3 m e a lenha dura 4 minutos;
  - **cama:** vira o ponto onde você volta quando cai;
  - **barricada:** bloqueia o NavMesh; os zumbis a quebram (2,5 de dano por segundo, cada um).
- **Desgaste:**
  - as armas de fogo emperram a partir de 70% de desgaste;
  - as brancas quebram e somem;
  - o kit de reparo deixa a arma nova.
- **F1:** cria qualquer item por espécie, no chão ou na mochila.
- **Faltando:**
  - **os itens nos móveis (com você, Gustavo):** por enquanto só o F1 cria;
  - a pesca;
  - as bancadas dos povoados;
  - a animação dos zumbis batendo na barricada;
  - não conferidos no jogo: os zumbis quebrando a barricada de verdade, e o bastão e a lança.

## 5. O zumbi sem uma perna: o equilíbrio e o levantar (31/10)

- **Perdendo só uma perna**, o Oco:
  - cai na hora ou cambaleia, tombando para os lados em volta do pé que sobrou e pulando de lado;
  - termina caindo (com física: o corpo cai de verdade) ou se firma e anda pulando numa perna (um tiro pode derrubá-lo de
    novo).
- **No chão**, ele tenta levantar ou rasteja numa perna. A tentativa pode falhar e ele cai de novo; depois de algumas falhas,
  desiste e só rasteja.
- **O levantar** é uma animação de ~4,5 s feita no Blender (`Blender/equilibrio_oco.py`), com os braços apoiando:
  1. de bruços, ele empurra o chão; os braços falham uma vez;
  2. fica de quatro com o joelho embaixo do corpo;
  3. quase tomba para o lado do coto e se segura na mão;
  4. agacha numa perna e sobe empurrando o chão.

  Há uma versão sem a perna esquerda e uma sem a direita, gravadas no esqueleto dos 12 Ocos.
- **Onde:**
  - `Zombies/Zumbi.Equilibrio.cs` (as fases);
  - `Editor/MontagemDosZumbis.Equilibrio.cs`;
  - os clipes `LevantarE`/`LevantarD` em `Art/Characters/Zombie/Oco/Clipes/<Oco>/`;
  - o FBX `Art/Characters/Oco/Oco_AnimacoesEquilibrio.fbx`;
  - os estados `LevantandoE`/`LevantandoD` no `Zombie.controller` (`Pernas` = 5);
  - os números no cabeçalho "Equilíbrio (sem uma perna)" dos `AjustesDeZumbi`.
- **Na rede:** cada zumbi agora ocupa 16 bytes, com a fase e a inclinação no último (`ZombieManager.Rede.cs`, `PorPacote`
  75). Se for mudar o pacote dos zumbis, mexa nos dois lados (o envio e o recebimento).
- **Para mudar a animação:**
  1. edite as chaves da lista `LEVANTAR` no script;
  2. no Blender, rode `eq.preparar(); eq.construir(); eq.exportar()`;
  3. na Unity, rode **Zombie City Cleaner > Zumbis > Gravar o levantar numa perna (sem remontar)**.
- **Testar:** no **F1 > "Tirar 1 perna"**, que corta a coxa do zumbi de pé mais perto.
- **Faltando:** quando o corpo cai de barriga para cima, ele vira para ficar de bruços numa mistura de 0,7 s, quase uma
  rolada rápida. Dá para fazer uma animação própria de rolar.

## 6. Correções menores

- **Lanterna em terceira pessoa:** a arma longa na mão do dono não fica mais estourada de branco pela lanterna do peito, nem
  faz sombra no facho (`JogadorRede.CorpoDoDono`).
- **Qualidade no Editor:** testar a qualidade Ultra no Editor não grava mais o anti-serrilhado 4x nas configurações do
  projeto; tudo volta ao sair do Play (`UI/Graficos.cs`).

## O que não foi para o GitHub (de propósito)

- `Backups/` (cópias de segurança de antes de cada mudança, 51 MB);
- `Assets/_Recovery/` (cena de recuperação da Unity);
- `Blender/PeruaNova/Untitled.blend` (vazio);
- `__pycache__/`.

As três pastas entraram no `.gitignore`; o `Untitled.blend` só ficou de fora.
