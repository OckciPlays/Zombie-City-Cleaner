# 08 – Vídeos de divulgação

Vídeos curtos para o Instagram (Reels), mostrando a construção do jogo, com a voz do autor por cima. Cada vídeo mostra o que entrou desde o anterior e termina anunciando o próximo. As tomadas são gravadas numa cena própria da Unity, a `Video.unity`, que não entra no jogo.

## O primeiro vídeo (~40 s)

A fala, mais lenta, com pausas para o jogo aparecer:

> "Estou construindo um jogo de apocalipse zumbi, e você pode fazer parte disso. Assim estava o meu projeto na última semana... e hoje já temos um mapa, um bunker e zumbis. No próximo vídeo vou adicionar armas para matar os zumbis."

| Tempo | Fala | Na tela |
|---|---|---|
| 0–4 s | "Estou construindo um jogo de apocalipse zumbi, e você pode fazer parte disso" | o gancho: 2 s da tomada 4 (ou já a Unity vazia) |
| 4–8 s | "Assim estava o meu projeto na última semana" | a Unity vazia: gravação de tela do Editor, com uma cena nova |
| 8–18 s | "e hoje já temos um mapa" | **tomada 1:** o mapa se montando, visto de cima; no fim, os postes acendem |
| 18–25 s | "um bunker" | **tomada 2:** o bunker em corte, as luzes acendendo cômodo por cômodo |
| 25–31 s | "e zumbis" | **tomada 3:** os zumbis subindo do chão pela cidade |
| 31–40 s | "No próximo vídeo vou adicionar armas para matar os zumbis" | **tomada 4:** o jogador andando no meio dos zumbis, sem arma e sem HUD |

- **Formato:** vertical, 1080 × 1920, 60 quadros por segundo, com o som do jogo.
- **As escolhas:** a última tomada em terceira pessoa (a câmera atrás do jogador), o jogador de punhos em guarda (a animação da mão vazia) e o fim de tarde virando noite (o mapa se monta no fim da tarde, e o jogador anda à noite, com a lanterna).
- **Cada tomada é um MP4.** A edição (cortes, a voz, as legendas e a música) é feita fora, no CapCut. As tomadas são gravadas mais longas do que o roteiro pede, para ter folga.
- **As armas do painel do bunker ficam escondidas:** elas são o assunto do próximo vídeo.

## As partes, em ordem

Como no plano da demo, cada parte só começa com a anterior aprovada.

### ✅ Parte 1: a base (24/10/2026)

- A cena `Video.unity`, a câmera do vídeo, o modo vídeo, o diretor e a gravação pelo Unity Recorder. A tomada "Teste" (5 s chegando no cruzamento do meio) serve para conferir a gravação.
- **Testado:** o Play abre a partida local sozinho; o jogador nasce de mãos vazias, os 1.000 zumbis povoam e o painel fica vazio. O MP4 sai em 1080 × 1920, a 60 fps, com 5 s (300 quadros) e com áudio, sem HUD nenhuma.

### ✅ Parte 2: tomada 1, o mapa se montando (24/10/2026, aprovada)

A tomada `Mapa` tem 13,5 s; o roteiro usa uns 10 s dela.

| Tempo | O que acontece |
|---|---|
| 0–0,5 s | uma planície de grama vazia, vista do alto |
| 0,5–2,5 s | os 25 pedaços do chão caem numa onda, do meio para fora (as ruas e as calçadas "se desenham"); as marcas no chão aparecem logo depois |
| 2,4–5,6 s | as 5 casas caem do céu, uma a uma, e o prédio por último: esticam na queda, amassam ao bater e levantam poeira |
| 5–9 s | os carros caem com um pouco de poeira; as coisas da rua caem de perto; os postes, as placas, as cercas e as árvores brotam do chão; o anel de pinheiros cresce dando a volta na cidade |
| 7–12,8 s | anoitece (das 16h36 às 20h), e os postes acendem um por um, do meio para fora |

- **A câmera:** gira devagar em volta do meio da cidade (36°), de 195 m para 130 m e de 66° para 50° de inclinação, com 55° de campo.
- **Os ajustes da vista do alto:**
  - as sombras vão até 320 m (o jogo usa 40);
  - a neblina fica linear, só no horizonte (do alto, a do jogo cobria a cidade inteira);
  - a imagem fica bem mais clara (sem o véu da neblina, as cores da cidade são escuras);
  - uma planície de grama de 2 km por baixo de tudo faz o mundo não terminar na borda da tela.
- **O que fica de fora:** o bunker (ele está embaixo da casa da família, que cai do céu), as armas do painel, a comida e a água e os zumbis (eles chegam na tomada 3).
- **Só o que se vê se mexe:** a colisão das casas e do bunker fica parada, e o jogador continua no bunker. Nada cresce da escala zero (escala zero dá NaN no shader, veja as regras).

### ✅ Parte 3: tomada 2, o bunker em corte (24/10/2026, aprovada)

A tomada `Bunker` tem 14,5 s (com o passeio pelos cômodos, pedido depois da primeira versão, que só olhava de cima).

| Tempo | O que acontece |
|---|---|
| 0–0,8 s | à noite, a casa da família vista do alto, com os postes acesos (a continuação do fim do mapa) |
| 0,8–2,4 s | o corte de raio-X desce do telhado até abaixo do teto dos cômodos: a cidade some de cima para baixo e o bunker aparece apagado, como uma casa de boneca |
| 2,5–4,4 s | as luzes acendem uma a uma, cada uma com o estalo do interruptor: a escada, o salão, os quatro quartos dando a volta e o depósito; a porta blindada abre |
| 3–7,8 s | a câmera mergulha pela abertura do corte, passa por cima da mesa comprida e desce até a altura dos olhos, na porta do salão, olhando a mesa e o painel de armas vazio no fundo |
| 7,8–14,5 s | um cômodo por vez, em primeira pessoa, com um corte seco entre eles (1,35 s cada): os quartos 2, 1, 3 e 4 e o depósito (com a lata e a garrafa na mesa), da porta para dentro, devagar |

- **O corte:** o plano perto da câmera vira um plano horizontal (o mesmo truque dos reflexos na água), então a câmera olha inclinada e o corte continua reto. A luz é a de debaixo da terra (como descendo a escada), mesmo com a câmera no alto.
- **As luzes de fora apagam** quando o corte passa do chão: o poste da rua iluminava o depósito antes da hora.
- **O mergulho** passa pelo corte olhando bem para baixo: assim o que estava cortado (as paredes de cima, o teto) volta sem aparecer na tela, e o olhar só levanta depois, já dentro do salão.
- Nos cômodos, o campo é mais aberto (75°): o quarto tem 3,6 m de largura, e a tela vertical é estreita.
- O painel de armas aparece vazio (as armas são o próximo vídeo), e a comida e a água estão na mesa do depósito. Sem zumbis.

### ✅ Parte 4: tomada 3, os zumbis aparecendo (24/10/2026, aprovada)

A tomada `Zumbis` tem 8 s; o roteiro usa uns 6 s dela.

| Tempo | O que acontece |
|---|---|
| 0–0,4 s | o cruzamento do meio à noite, vazio, visto de lado e de cima (30 m, 38°), com o prédio no fundo |
| 0,4–5,2 s | os 1.000 zumbis sobem do chão em ondas, horda por horda, do cruzamento para fora (cada um leva 1,1 s para subir, com um pouco de poeira nos pés); a câmera chega mais perto (22 m) e gira devagar |
| 6–8 s | corte para o rosto de um zumbi que vaga no cruzamento, de frente, a 1,5 m, com a luz de uma lanterna e o gemido dele |

- **Só o modelo afunda e sobe:** a raiz do zumbi continua no NavMesh, e a IA e a navegação seguem como no jogo (eles já saem vagando).
- **Na primeira gravação,** a câmera ficou longe (55 m) e os zumbis viraram pontinhos, e a lanterna do close estourou o rosto (ficou branco). A câmera chegou perto do cruzamento, a vista ficou mais clara e a lanterna ficou bem mais fraca.

### 🔎 Parte 5: tomada 4, o jogador entre os zumbis (24/10/2026; esperando o olhar do usuário)

A tomada `Jogador` tem 10 s; o roteiro usa uns 9 s dela.

| Tempo | O que acontece |
|---|---|
| 0–5,5 s | à noite, na avenida sul, o jogador anda para o norte (de punhos em guarda, com a lanterna acesa), visto de trás do ombro; a horda da rua sul, na frente, se vira e vem, e a da saída sul vem por trás |
| 5,5–10 s | ele para perto da blitz abandonada, os zumbis fecham a roda, e a câmera sobe e gira até quase em cima dele: o jogador cercado, sem arma (o gancho do próximo vídeo) |

- **O diretor anda com o jogador** (no lugar do teclado; as gravações saem parecidas): 1,6 m/s, olhando 8° para baixo, para o facho da lanterna pegar o chão e os zumbis da frente. O jogador não leva dano.
- **A lanterna denuncia:** com ela acesa, os zumbis enxergam o jogador a 26 m. Por isso os dois grupos vêm.
- **O fim mudou do plano:** em vez de um zumbi chegar perto da câmera, a câmera sobe e mostra o jogador cercado. Combina com "no próximo vídeo vou adicionar armas".
- **Cada gravação sai um pouco diferente:** a IA dos zumbis sorteia para onde eles vagam e quando param. Vale gravar duas ou três e escolher a melhor.
- **Você mesmo pode andar** (pedido depois da primeira versão): ligue **Jogador No Controle** no diretor. A câmera fica atrás do ombro e segue o seu mouse (olhando para baixo, ela sobe por trás; uma parede entre você e a câmera a puxa para a frente dela).
  - WASD anda, Shift corre, o mouse olha, F liga e desliga a lanterna, o clique soca.
  - **Enter** faz o final: o jogador para, e a câmera sobe até quase em cima dele, em 4,5 s; aí a tomada termina e o vídeo fecha.
  - Parar o Play também termina e fecha o vídeo, sem o final.
  - Você sai do mesmo lugar (a avenida sul, olhando para o norte), mas pode ir para onde quiser.
  - Clique no Game view antes, para o mouse ficar preso no jogo. Não aperte ESC: ele abre o menu do jogo, que fica invisível no modo vídeo, e o jogador para de responder.
  - Com o Recorder, o jogo anda no tempo do vídeo (60 quadros por segundo): se o Editor desenhar menos que isso, tudo fica um pouco lento enquanto você joga, mas o vídeo sai na velocidade certa.
  - Sem folha de conferência nesse modo (a tomada não tem duração certa).
- **Na primeira gravação,** a câmera ficava perto demais (o jogador tomava metade da tela), os zumbis colados na lanterna estouravam de branco e, de 12 m, as cabeças dos zumbis escondiam a dele. A câmera recuou, o brilho cai no fim e a subida para a 7,5 m, quase em cima dele.

## Como gravar

1. Abra `Assets/_Game/Scenes/Video.unity`.
2. Selecione o "Diretor do vídeo", escolha a tomada e ligue **Gravar**.
3. Dê Play. O diretor abre uma partida local (sem internet), espera o jogador nascer e os zumbis povoarem, roda a tomada, e o Play para sozinho no fim.
4. O vídeo fica em `Recordings/<vídeo>/<número>_<tomada>_<data>.mp4` (a pasta `Recordings/` fica fora do git).

Junto com o vídeo sai uma **folha de conferência**, `Recordings/<vídeo>/<número>_<tomada>_folha.png`: 12 quadros da tomada, do começo ao fim, numa imagem só, para conferir sem abrir o vídeo (desliga em "Folha De Conferência", no diretor).

Sem **Gravar**, o Play só mostra a tomada. Para ver como ela fica no celular, ponha o Game view em 1080 × 1920 (o Recorder já deixa assim depois da primeira gravação).

**O Recorder grava com o tempo fixo:** cada quadro avança 1/60 s do jogo, por mais que demore. Se o jogo ficar lento (mil zumbis na tela), no Editor parece câmera lenta, mas o vídeo sai liso.

**Mudou a cidade, os zumbis ou o clima da Demo_Cidade?** Rode de novo o menu **Zombie City Cleaner > Montar cena de vídeo**. Ela apaga e refaz a `Video.unity` a partir de uma cópia da Demo_Cidade (mudanças feitas à mão nela se perdem).

## Dicas para os vídeos

- **O gancho:** os primeiros 1 ou 2 s decidem se a pessoa fica. Abrir com um pedaço da última tomada antes do "assim estava..." prende mais do que começar numa tela vazia.
- **"Você pode fazer parte disso":** peça algo concreto, como "comenta qual arma vem primeiro", que já puxa o próximo vídeo. Outra ideia: zumbis com o nome de quem comentar (a aparência de cada zumbi já é sorteada).
- **A área segura do Reels:** a interface do Instagram cobre a parte de baixo e a lateral direita, então o que importa fica no meio.
