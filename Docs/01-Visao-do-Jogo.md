# 01 – Visão do jogo

## O conceito

- **Começo:** você e sua família vivem na cidade quando começa o apocalipse zumbi. Quando a notícia chega, todos se escondem no bunker embaixo de casa.
- **O bunker:** é o primeiro abrigo, com quartos, despensa e sala comum. Dá para andar por ele e gerenciar as coisas.
- **Explorar:** você sai do bunker para explorar a cidade infestada, coletar recursos, matar zumbis e limpar a cidade.
- **Recursos finitos:** o que foi saqueado não volta, então cada dia é preciso ir mais longe.
  - Com o tempo, entram fontes que se renovam: água da chuva, plantações e criação de itens.
- **Evolução da base:** bunker → casa → comunidade. Em alguns momentos, hordas tentam invadir a cidade, e podem conseguir.
- **Veículos:** aparecem com o tempo e facilitam a exploração.
- **Cidade limpa:** quando a cidade estiver livre de zumbis, é preciso proteger as entradas dela.
- **Fora da cidade:** outras cidades, outros sobreviventes e comunidades. As comunidades pedem missões (limpar cidades, explorar lugares) em troca de recursos.
- **Online:** até 4 jogadores, e cada um é um membro da família.
  - Jogando sozinho, os outros membros ficam no bunker como personagens controlados pelo jogo, fazendo tarefas.
  - Quando um amigo entra, ele assume um deles.
- **Dia e noite:**
  - de dia há neblina pesada, e é a hora de explorar;
  - à noite você enxerga pouco, mas os zumbis também te enxergam menos, e dá para passar sem ser visto;
  - a lanterna é a troca: ligada, você enxerga e é visto de longe.
- **O mundo é salvo** pelo anfitrião: o que foi saqueado, os zumbis mortos de cada área e o estado da base.
- **O nome:** no conceito antigo, uma corporação contratava equipes para limpar cidades. A corporação saiu do jogo, mas o nome ficou, porque agora você limpa a sua cidade.

## Os zumbis

- São lentos e desistem de seguir quando os jogadores se afastam.
- O tiro na cabeça explode a cabeça. Morto, o corpo cai com física (ragdoll).
- O desmembramento: braços e pernas caem com tiros; sem uma perna o zumbi manca, sem as duas ele rasteja.
- As ideias para depois da demo (o modo história) estão no [07 – Ideias do modo história](07-Ideias-do-Modo-Historia.md).
- O jogo quer muitos zumbis ao mesmo tempo. Por isso os modelos têm poucos polígonos, e a IA tem LOD no estilo Dead Rising (os zumbis longe quase não custam nada). Hoje são 258 na cidade.
- Há 4 modelos cartoon, sujos de sangue: um garoto de camiseta branca, um homem de camiseta vermelha e bermuda, uma mulher de cabelo castanho e uma loira de camiseta preta.

## Decisões já tomadas

- **Online desde a demo:** com o Relay da Unity e o Netcode for GameObjects. Um jogador é o anfitrião e roda os zumbis, o saque e o mundo. Não há servidor dedicado.
- **Mundo salvo pelo anfitrião.**
- **Noite:** você enxerga pouco, mas os zumbis também te veem menos. A lanterna ligada te denuncia.
- **Se o anfitrião sair, a partida acaba.** Trocar de anfitrião e reconectar ficam para depois da demo.
- **Recomeço (23/10/2026):** havia um protótipo com visual de PS1 (um elevador infinito num prédio). Ele foi deixado de lado e está guardado na branch `feature/prototipo-fps-zumbis`. Nada dele é usado na demo.

## Direção de arte

- **Low-poly simples, sem realismo e sem textura no cenário:** cores sólidas de uma paleta. O visual pixelado de PS1 foi abandonado.
- **O clima é o que mais importa acertar:** uma cidade sombria no estilo Silent Hill, com neblina pesada de dia, noites escuras com luz de poste e partículas no ar.
- **Personagem do jogador e armas** no estilo "quadradão", com blocos e cores sólidas. As mãos usam luvas pretas sem dedos.
- **As armas** (pistola, rifle, submetralhadora e espingarda) seguem imagens de referência do usuário: low-poly, com cores lisas (o rifle é um M4 preto, a espingarda tem coronha e telha de madeira).
- **Os zumbis** são a exceção: têm textura (são modelos cartoon gerados e ajustados no Blender).

### A paleta

Tudo o que não tem textura usa uma imagem de 16 × 16 pixels (`Assets/_Game/Art/Paleta.png`). O UV de cada face aponta para o quadradinho da cor dela. Na tela não há textura, e o jogo inteiro usa poucos materiais, o que ajuda no desempenho e permite trocar uma cor em tudo de uma vez.

| Linhas | Uso |
|---|---|
| 0 a 3 | as 4 variações de cor da família (pele, cabelo, jaqueta, camisa, calça, sapato, cinto, escuro, jaqueta escura e as luvas) |
| 4 | a pistola antiga (não usada mais) |
| 5 a 11 | a cidade: chão e concreto, casas e prédio, metal e carros, plantas, placas, detalhes das casas e móveis |
| 12 | as armas |
| 13 a 15 | livres |

As linhas contam de baixo para cima. O alfa das linhas da cidade é a suavidade (o brilho) de cada cor.

Os tons base que guiaram a cidade (ficaram um pouco mais claros do que parecem, porque a noite e a neblina escurecem tudo):

| Uso | Cor |
|---|---|
| Asfalto | `#3A3D42` |
| Calçada e concreto | `#8A8A84` |
| Faixas e meio-fio desbotados | `#B8B29A` |
| Tijolo do prédio | `#7A4A3C` |
| Casas (madeira pintada) | `#6E7B6A`, `#8C7B6A`, `#5E6B7A` |
| Telhados | `#4A3B38`, `#3E4A52` |
| Batentes e portas | `#D8D2C0`, `#5A4636` |
| Vidro escuro | `#1E2A30` |
| Janela acesa (rara) | `#E8B86A` |
| Concreto do bunker | `#6A6A64` |
| Pinheiros (folhas e tronco) | `#2F3D33`, `#4A3A2E` |
| Grama e terra | `#4B5240`, `#5A4B3C` |
| Metal dos postes | `#2E3134` |
| Luz de sódio dos postes | `#FFB46B` |
| Luar | `#8FA6C8` |
| Neblina de noite | `#2A3036` |
| Neblina de dia | `#9AA0A0` |
| Sangue e ferrugem | `#6B0F0F`, `#8A4B2E` |
