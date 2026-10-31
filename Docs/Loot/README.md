# O saque e a fabricação (31/10/2026)

O que entrou no jogo a pedido ("gerar os loots do game ... criar o sistema de craft e fazer funcionar"). Os detalhes de
código ficam no `CLAUDE.md` ("O saque, a fabricação e as construções").

## O inventário

- **Sem coldre e sem limite de armas:** leve quantas couberem na grade. Sem mochila, só os bolsos (4 × 2).
- **Nas costas, só as armas dos atalhos 1, 2 e 3** (a da mão não): a pistola e a faca no cós; as outras na diagonal das
  costas ou nas laterais da mochila, como antes.
- **Pilhas:** o saque e a munição empilham numa célula (o número no canto). Arraste uma pilha sobre outra igual para juntar.
- **Botão direito num item:** comer, beber ou usar; pôr ou tirar dos atalhos; reparar; desmontar; montar no chão (os kits);
  largar um ou todos.

## Os itens (cada um numa célula)

| Espécie | Itens |
|---|---|
| Comida | lata de comida, feijão, carne, milho, sopa, salgadinho (dá sede), chocolate, barra de energia |
| Bebida | garrafa de água, copo de água, refrigerante (garrafa e lata), energético, leite, suco, garrafa térmica |
| Remédios | remédio, comprimidos, kit médico, atadura, gaze, desinfetante, spray antisséptico, antibiótico, bandagem |
| Materiais | madeira, toras, pano, corda, fita adesiva, ferramenta, peças de metal, fios, pregos, isca, vara de pesca, kit de reparo, roupa |
| Kits de montar | fogueira, cama improvisada, barricada |

Os modelos são do `Blender/loot.py`; por enquanto o F1 (o menu do desenvolvedor, por espécie) cria no chão ou na mochila.
A pesca ainda não existe.

## Criar (a aba Criar, ou E na bancada)

| Receita | Gasta | Onde |
|---|---|---|
| Bandagem ×2 | 2 panos | qualquer lugar |
| Kit médico | 1 atadura, 2 gazes, 1 desinfetante, 1 cartela de comprimidos | qualquer lugar |
| Corda | 3 panos | qualquer lugar |
| Fogueira | 4 madeiras, 1 pano | qualquer lugar |
| Cama improvisada | 4 panos, 3 madeiras, 1 corda | qualquer lugar |
| Vara de pesca | 2 madeiras, 2 fios | qualquer lugar |
| Isca ×5 | 1 lata de milho | qualquer lugar |
| Tocha | 1 madeira, 1 pano | qualquer lugar |
| Bastão com pregos | 2 madeiras, 6 pregos | qualquer lugar |
| Barricada | 4 madeiras, 8 pregos (+ ferramenta) | bancada |
| Mochila pequena | 6 panos, 2 cordas, 1 fita adesiva | bancada |
| Kit de reparo | 3 peças de metal, 1 fita adesiva, 1 fios (+ ferramenta) | bancada |
| Lança | 2 madeiras, 1 peças de metal, 1 fita adesiva | bancada |

A bancada não se fabrica: há uma no bunker (embaixo do painel de armas).

**Desmontar** (botão direito): roupa → 2 panos; tora → 3 madeiras (com o machado); kit médico, bandagem, corda, vara,
garrafa térmica, kit de reparo, kits de montar e armas (viram peças de metal; as de fogo pedem a ferramenta).

## Montar no mundo

- **Fogueira:** luz e calor (a vida volta mais rápido perto). A lenha dura 4 minutos; E põe mais (1 madeira).
- **Cama:** você volta nela quando cair. E recolhe.
- **Barricada:** os zumbis não passam; os que encostam nela vão quebrando. E desmonta.

## As armas gastam

As de fogo gastam a cada tiro e, muito gastas, emperram; quebradas, não atiram. As brancas gastam a cada golpe e
quebram. O kit de reparo (botão direito → Reparar) deixa a arma nova. A tocha queima uns 10 minutos na mão.
