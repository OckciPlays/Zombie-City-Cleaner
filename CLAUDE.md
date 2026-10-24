# Zombie City Cleaner (Unity)

Jogo de sobrevivência online (até 4 jogadores, a família) numa cidade infestada de zumbis, em low-poly com cores sólidas e clima sombrio no estilo Silent Hill.

- A arte (Blender, referências, sons originais) fica em `C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner`. O `CLAUDE.md` de lá tem o conceito do jogo.
- O plano, dividido em partes, está no `Plano_Demo.md` da mesma pasta. Siga a ordem dele e só avance com a parte anterior aprovada.

## Branches

- `feature/demo-cidade` (esta): a demo da cidade, começada do zero.
  - Parte 0 (fundação), Parte 1 (o zumbi), Parte 2 (o jogador e a pistola), Parte 3 (o online base), Parte 4 (a cidade em blocos, o clima e o dia), Parte 5 (os zumbis na cidade) e Parte 6 (combate; o desmembramento dela saiu nos ajustes antes da Parte 7) estão feitas.
  - A pedido do usuário, a Parte 8 (a arte final) veio antes da Parte 7 (o bunker, o saque e o dia), que fica para depois.
  - A Parte 8 está feita, nas 6 fatias do `Plano_Demo.md`: 1 (a paleta da cidade, o material único e os objetos de rua), 2 (as casas), 3 (o prédio), 4 (o bunker e os móveis), 5 (o chão) e 6 (oclusão, neblina dentro de casa e benchmark; a luz ficou em tempo real e não houve LOD, veja "Clima e ciclo do dia").
  - Depois dela, os "ajustes antes da Parte 7", em 3 fatias, todas feitas: 1 (o tiro novo, sem som ambiente, nascer no bunker), 2 (os 4 modelos de zumbi, só a cabeça explode, morte com ragdoll) e 3 (as 4 armas, os braços de luva e o suporte de armas no bunker).
  - Depois, a pedido, as armas rebalanceadas, a sniper com luneta e o desmembramento (branch `feature/armas-sniper`, a partir da `feature/ambientacao-teste`), veja "Combate e morte".
  - Depois, a Parte 7 (o bunker, o saque e o dia).
- `feature/prototipo-fps-zumbis`: o protótipo antigo, com visual de PS1 e elevador infinito. Fica só como referência; nada dele é usado aqui.

## Projeto

- **Versões:** Unity 6000.6, URP 17.6 (renderer Forward+), apenas o Input System novo e AI Navigation 2.0.
- **Rede:** Netcode for GameObjects 2.13, `com.unity.services.multiplayer` 2.4 (a partida, o código de entrada e o Relay) e Multiplayer Play Mode 3.0 (vários jogadores no Editor).
  - O projeto está ligado à Unity Cloud (projeto "Zombie City Cleaner", id `c6d7d134-66e6-4af9-a402-28aad4b0e691`).
  - Topologia: um jogador é o anfitrião e roda os zumbis, o saque e o mundo. Nenhum código de jogo pode depender de "o jogador" único ou de `Camera.main`; tudo usa a lista de jogadores.
- **Play sem Domain Reload e sem Scene Reload.** Por isso:
  - estado `static` sobrevive entre sessões de Play, então inscreva e desinscreva eventos estáticos em `OnEnable`/`OnDisable`;
  - scripts `[ExecuteAlways]` não recebem `Awake` de novo ao dar Play;
  - para tempos guardados em `static`, use `Time.realtimeSinceStartup`.
- **Não recompile scripts com o Play rodando:** os campos não serializados zeram.
- **URP (`Assets/Settings/PC_RPAsset`):** gradação de cor em HDR, sombras até 40 m com 2 cascatas e HDR ligado.
  - `PC_Renderer` (renderizador 0) tem o recurso "NeblinaDeAltura" (Full Screen Pass, antes dos transparentes, com profundidade).
  - `PC_Renderer_Bracos` (renderizador 1, sem recursos) é usado pela câmera dos braços. Sem ele, a neblina de altura seria aplicada de novo por cima da imagem, só com a profundidade dos braços.
  - Os dois são montados por `Editor/MontagemDoClima`, que só mexe nos assets de URP dentro de `Assets/` (nunca no asset do pacote).
  - `PC_Renderer` também tem o recurso "Decals" (técnica de tela, até 60 m), para as manchas de sangue e os furos de tiro. Quem o põe é a montagem dos zumbis.
- **Escala zero é proibida:** uma peça com escala 0 em algum eixo tem normais infinitas no shader. O pixel vira NaN e o bloom espalha um clarão branco, com um risco preto no meio. (Foi uma laje de largura zero dos blocos da Parte 4; hoje o chão que se vê vem do Blender, e a `Laje` só faz colisão, sem faixas vazias.)
- **Camadas:**
  - 6 "ArmasPrimeiraPessoa" (os braços);
  - 7 "Zumbi" (cápsulas e hitboxes dos zumbis; não colide com ela mesma nem com a 6);
  - 8 "Corpos" (os zumbis mortos, com física): só colide com o cenário e com ela mesma, e o tiro passa por ela;
  - 9 "Jogador" (a raiz do jogador, com o CharacterController).
  - O cenário fica na Default.
  - Um corpo sólido na camada 7 ficaria parado em cima da cápsula de um zumbi ou do jogador e taparia o tiro na cabeça (faísca em vez de sangue). Por isso os corpos têm camada própria (antes, era a dos membros caídos).

## Pastas

- `Assets/_Game/` é tudo da demo:
  - `Scenes/Demo_Cidade.unity`: a cena principal, a única nas Build Settings. `Scenes/Demo_Cidade/NavMesh da cidade.asset` é o NavMesh dela.
  - `Prefabs/`: `Jogador.prefab` e, em `Prefabs/Zumbis/`, um `Zumbi_<modelo>.prefab` e um `Corpo_<modelo>.prefab` para cada zumbi, todos montados por scripts de editor.
  - `Settings/Demo_Volume.asset` (noite) e `Settings/Demo_Volume_Dia.asset` (dia): o pós-processamento (ACES, cores dessaturadas, vinheta, granulado, bloom).
  - `Art/Characters/Zombie/`: os 4 modelos de zumbi e, de cada um, o `<modelo>_Extra.fbx` (as animações do desmembramento: cair, rastejar e mancar, feitas pelos scripts de `Blender/`).
  - `Art/Characters/Jogador/`: o jogador (corpo, os braços de primeira pessoa de cada arma, as armas avulsas e as malhas delas). A sniper tem os braços em `Jogador_FP_Sniper.fbx` e a arma avulsa num FBX próprio, `Armas_Sniper.fbx` (os dois feitos pelos scripts de `Blender/`).
  - `Art/Paleta.png`: a paleta de cores 16×16 dos personagens (linhas 0 a 4) e da cidade (linhas 5 a 10). O alfa das linhas da cidade é a suavidade.
  - `Art/Cidade/`: a arte final (Parte 8), exportada de `Modelo 3D/Cidade/Cidade.blend`: `Cidade_Rua.fbx` (objetos de rua), `Cidade_Moveis.fbx` (móveis), `Cidade_Chao.fbx` (o chão, em 25 pedaços "Chao_i_j"), e `Cidade_Casas.fbx` (as 5 casas), `Cidade_Predio.fbx` (o prédio) e `Cidade_Bunker.fbx` (o bunker), estas três com o seu `_Colisao.fbx` (legível).
  - `Art/Materials/Cidade.mat`: o material único da arte final (URP Lit com a paleta e a suavidade vinda do alfa).
  - `Art/Materials/Blocos/`: o que sobrou dos blocos: `Lampada.mat` (a lente dos postes, que acende) e `ConeDeLuz.mat` (o cone falso, aditivo, com o degradê `DegradeCone.png`). Também o `PiscaAlerta.mat` (as lanternas do pisca-alerta dos carros, ambientação).
  - `Art/Materials/`: também `NeblinaDeAltura.mat`, `Particulas.mat` e `Clarao.mat`. Os `Teste_*` são da fundação.
  - `Art/Materials/Decals/`: os decals de sangue e de furo de tiro (shader "Shader Graphs/Decal", texturas geradas por código), e o `Decal_Rua` (as marcas no chão da ambientação, com a textura `Rua.png` de 4 desenhos, feita pela montagem da cidade).
  - `Art/Meshes/`: malhas geradas pela montagem da cidade (`ConeDeLuz`).
  - `Shaders/NeblinaDeAltura.shader`: a neblina de altura em tela cheia.
  - `Audio/SomZombie1.mp3`: a voz do zumbi (mono, descomprimida ao carregar), tocada em trechos.
- Os scripts vão para `Assets/_Game/Scripts/`, separados por sistema (Network, Player, Zombies, Combat, Loot, World, Atmosphere, UI).
- `Blender/` (fora de `Assets`): os scripts do Blender da sniper e das animações do desmembramento dos zumbis, com o passo a passo no README de lá. O zumbi antigo que eles leem (`NovoZombie_antigo.fbx`) sai do histórico do git e fica fora do git.
- `Docs/` (fora de `Assets`): a documentação para quem colabora no projeto, em português: visão do jogo, plano da demo com o estado de cada parte, histórico, arquitetura, arte e como colaborar. O `README.md` da raiz aponta para ela. Ao terminar uma parte ou mudar algo importante, atualize também o `Docs/` (sobretudo o histórico e o plano).

## Cena `Demo_Cidade`

- **Raízes:** "Cidade", "Navegação" (o NavMesh), "Zumbis", "Clima", "Sol e lua", "Pós-processamento (noite)" e "(dia)", "Rede", "Efeitos", "Pontos de nascimento", "Parede de armas" e "Câmera de teste".
- **Os scripts de montagem**, no menu Zombie City Cleaner, refazem partes da cena. Mudanças feitas à mão nessas partes se perdem; mude o script e rode de novo.
  - **Controlador apagado e recriado:** as montagens dos zumbis e das armas apagam o controlador de animação e criam outro (que fica com o mesmo GUID). Ao ser apagado, os prefabs que usam o controlador são reimportados sem ele. Se depois são salvos iguais, a Unity não os importa de novo, e o personagem nascia sem animação (pose T). Por isso, as duas montagens forçam a reimportação dos prefabs no fim.
  - Montar cidade em blocos: "Cidade", os pontos de nascimento e o NavMesh.
  - Montar clima e ciclo do dia: "Clima" e os volumes.
  - Montar jogador e rede: o prefab do jogador, "Rede", "Efeitos" e "Pontos de nascimento".
  - Montar zumbis: a importação do zumbi, a camada, as malhas, o Animator, o prefab do zumbi, o prefab do jogador, "Zumbis", o sangue no "Efeitos", os decals e o NavMesh.
  - Montar armas: a importação dos braços de cada arma, do corpo e das armas avulsas, as malhas das armas, os Animators dos braços e os override de cada arma, o prefab do jogador e o "Parede de armas" (o suporte do bunker, que precisa da cidade montada).
- Os valores de neblina e luz guardados na cena (densidade 0,05, ambiente azulado) são só os do Editor parado. Em Play, o `AmbienteDoDia` reescreve tudo a cada quadro.

## A cidade em blocos (Parte 4)

- **Montada por código:** `Editor/MontagemDaCidade`, menu Zombie City Cleaner > Montar cidade em blocos. Ela apaga e refaz a raiz "Cidade" (e o antigo "Teste (temporário)"), então mudanças feitas à mão na cidade se perdem. Mude o script e rode de novo.
  - Tudo é estático (batching e oclusão), menos as luzes, os postes e os cones de luz, que mudam em Play.
- **Arte final (Parte 8):** tudo o que se vê é modelo do Blender (objetos de rua, casas, prédio, bunker, móveis e o chão), com o material único `Cidade`.
  - **O chão colide com caixas invisíveis** (`Bloco`: só BoxCollider, sem renderer): ruas, saídas, calçadas, a grama (`Laje`, com o buraco da escada do bunker), o piso da praça, o coreto e o estacionamento. As alturas batem com o visual: rua no 0, calçada em 0,12, grama em -0,02, praça em 0,08, estacionamento em 0,01 e coreto em 0,6.
  - O visual do chão são os 25 pedaços "Chao_i_j" (grupo "Chão (arte)"), já nas coordenadas do mundo.
  - `ArteDaCidade()` acerta a importação dos FBX da cidade (sem materiais; o visual com UV de lightmap, a colisão legível e sem UV de lightmap) e o material `Cidade`, e carrega todas as malhas por nome. `Modelo(pai, malha, pos, yaw)` põe uma malha com o material da cidade, sem colisor.
  - **Casas:** `Casa(grupo, "Casa_1", centro, yaw, W, D, bunker)` põe o modelo ("Casa") e um "Colisão" com um MeshCollider da malha `Casa_1_Colisao`.
    - O modelo foi feito no Blender com a mesma planta (`cidade_casas.py`): o W × D e as aberturas têm de bater com o `CASAS` de lá.
    - A montagem ainda põe os móveis em blocos, o eco, o bunker (blocos) e abre no chão de grama o buraco da escada.
    - Conferido: NavMesh na sala e no quarto das 5 casas, caminho completo da rua até o quarto e da cozinha da família até o bunker, e o buraco da escada aberto na colisão.
  - **Bunker:** `Bunker()` põe o modelo "Bunker" e um "Colisão" com `Bunker_Colisao` (no grupo da casa da família, nas coordenadas dela), os móveis, a lâmpada (a única luz lá embaixo) e o eco.
  - **Móveis:** `Movel(pai, "Geladeira", x, y, z, yaw)` põe um móvel do `Cidade_Moveis.fbx` com uma caixa de colisão do tamanho da malha. O objeto leva o nome da malha (a Parte 7 vai achar os de saque por ele).
    - `MoveisDaCasa`: sala (tapete, sofá ou poltrona, TV, mesa com cadeiras), cozinha (geladeira e fogão na divisória, balcão sob a janela) e quarto (cama, criado-mudo, armário e cômoda).
      - O sofá acaba antes da porta da frente; se não couber, vira poltrona (casas de 9 m). O rack fica antes da porta da cozinha.
      - Na casa da família, a boca da escada do bunker fica livre: o balcão vai para a parede da sala, depois da porta.
    - `MoveisDoApartamento`: sofá contra a fachada, TV, mesa com cadeiras e a cozinha americana na parede do quarto; no quarto, cama, criado-mudo, armário e cômoda.
    - Bunker: mesa grande com rádio e 4 cadeiras, 2 beliches, 3 prateleiras cheias na despensa, tambores, caixas e o `Suporte_Armas` na parede da esquerda da sala comum, logo depois da porta (as armas dele são o "Parede de armas", da montagem das armas).
    - Conferido com os 187 móveis: caminho completo da rua até a sala, a cozinha e o quarto das 5 casas, da cozinha da família até as 3 salas do bunker e da rua até os apartamentos dos 4 andares.
  - **Prédio:** `Predio()` põe o modelo "Predio" e um "Colisão" com a malha `Predio_Colisao`, mais os móveis de cada apartamento e o eco.
    - Escada, patamares, muretas, guarda-corpo do último andar, degraus da entrada e portas estão na colisão. A escada de incêndio, a marquise e a cobertura são só visuais.
    - Conferido: caminho completo da rua até o corredor e os dois apartamentos dos 4 andares.
  - **Colisores à parte:** `ColisorDaMalha` (uma caixa do tamanho da malha: carros, barreiras, lixo, bancos) ou caixas e cápsulas à mão (a haste do poste, o poste das placas, o tronco das árvores). Os pinheiros, atrás da barreira, não têm colisor.
  - **Poste:** o modelo e a lente ("Lâmpada", com o material `Lampada`, que o `PosteDeLuz` acende). A luz fica em (0; 5,8; 1,4) e o cone, em (0; 5,82; 1,4).
  - **Carros:** `Carro(pai, pos, yaw, "Carro_Sedan_Vinho")`, por exemplo.
  - **Cerca:** trechos de ~2 m, esticados para fechar a linha (15% quebrados), o mourão da ponta `a` (cada trecho só tem o do lado +X) e uma caixa de colisão só, fina, na linha toda.
  - **Árvores:** 40% secas. **Pinheiros:** os três tipos, sorteados, com escala de 0,9 a 1,6.
  - **"Objetos de rua":** 8 placas de pare nos cruzamentos (a frente olha para quem chega), 4 placas de rua, 6 hidrantes virados para a rua, 10 lixeiras (2 caídas), 2 caçambas e sacos de lixo. A praça tem os bancos virados para o coreto e 2 lixeiras.
  - **Barricadas:** 5 barreiras de concreto, o cavalete e 3 cones do lado de dentro, e os 2 carros do lado de fora.
  - **"Abandono"** (ambientação, fatia 1, em teste na branch `feature/ambientacao-teste`): o começo do apocalipse na rua, só com peças que já existem.
    - "Carros abandonados" (12, com o nome "Carro", que no futuro podem virar fonte de peças): a fila da fuga nas saídas norte e leste, a batida no cruzamento do meio, um capotado na rua oeste (z ≈ -1), um que subiu na calçada e parou no poste (2,9; 24,3) e a picape da blitz.
    - "Blitz abandonada" na avenida sul (z ≈ -31): 2 barreiras abertas no meio, o cavalete deitado e cones, 2 deles caídos.
    - "Lixo": sacos, caixas, tambores (alguns deitados) e uma lixeira caída nas calçadas, fora das portas, do portão da família e da saída do bunker.
    - `Peca(pai, malha, pos, giro, sombra)` gira a peça em qualquer eixo e a apoia no chão (`NoChao`: o ponto mais baixo da malha girada vai para `pos.y`). Sacos, caixas e cones não fazem sombra.
    - Custo medido (2 rodadas antes e 2 depois, mesma vista, noite): ~50 a 60 draw calls a mais (+5 a 6%), mesmo número de SetPass, a GPU dentro da variação (8,3 a 9,1 ms contra 8,4 a 9,9 ms).
  - **"Marcas no chão"** (ambientação, fatia 2, na mesma branch): 23 decals com um material só (`Decal_Rua`; a textura tem 4 desenhos, um em cada quarto, e cada decal escolhe o seu pelo `uvBias`): freadas atrás das batidas, óleo embaixo dos carros batidos, sangue seco arrastado e papéis.
    - A caixa de cada decal tem 0,6 m de altura: pega o chão (rua, calçada ou praça) e não o coreto (60 cm). Até 40 m.
    - O sangue seco é vermelho-escuro: marrom, de noite e com a gradação fria, virava cinza e parecia sombra.
  - **"Sinais de vida"** (fatia 2): o que ficou ligado quando as pessoas fugiram. Luzes pontuais pequenas, sem sombra, que o `World/SinalDeVida` faz mudar em Play (só visual, cada máquina tem o seu):
    - 2 TVs (a casa 2 e o apartamento leste do 2º andar): a luz fria sai da frente do `Rack_TV` e muda de força e de tom a cada "corte de cena";
    - 2 lâmpadas esquecidas (a sala da casa 4 e o apartamento oeste do 3º andar), que às vezes tremem;
    - o pisca-alerta em 2 carros (o sedã batido no cruzamento do meio e um da fila da saída norte): 4 caixinhas com o `PiscaAlerta.mat` (Unlit com a cor em HDR: o `_EMISSION` do Lit some do material depois de carregado) nos cantos e uma luz laranja em cima, a 1,4 Hz. As caixinhas aparecem e somem (o renderer liga e desliga): mudar o brilho por MaterialPropertyBlock as tirava do SRP Batcher e custava ~5 SetPass a mais. As caixinhas encostam na lataria: um raio na malha (com um MeshCollider temporário) acha a superfície, porque o carro não é uma caixa.
    - A marcação de estático pula o que está dentro de um `SinalDeVida` (as caixinhas ligam e desligam).
  - Custo medido da fatia 2 com a lanterna nova (2 rodadas limpas, mesma vista, noite, contra a fatia 1): draw calls iguais, 2 a 3 SetPass a mais, a GPU dentro da variação (8,5 a 9,4 ms contra 8,3 a 9,1 ms).
  - **Cuidado com `_EMISSION` no Lit:** a palavra-chave some do material depois de carregado (o arquivo ainda a tem). O `PiscaAlerta` usa Unlit por isso; a `Lampada` dos postes não brilha pela emissão (pendente).
  - A calçada fica 12 cm acima da rua: o que fica em cima dela usa y = 0,12.
  - No fim, ela põe os 4 pontos de nascimento na sala comum do bunker (`PontosNoBunker`), olhando para a mesa.
- **Planta (metros, +Z = norte):**
  - Ruas em grade nas linhas x e z = −40, 0 e 40, com 8 m de largura, faixa amarela tracejada e calçadas de 2,5 m em volta dos 4 quarteirões. As 4 saídas, em ±60, têm barricadas. (O chão de arte, `cidade_chao.py`, repete esta planta: ao mudá-la, mude lá também.)
  - **Prédio** em (20, 0, 21), 22 × 14 m, 4 andares de 3 m: escada em U no fundo, corredor no meio e 2 apartamentos por andar, com eco (reverb) dentro.
  - **Casa da família** em (−20, 0, −18), virada para o sul (yaw 180), 12 × 10 m, azul, com cerca. É a única com bunker.
  - **Outras casas:** Casa 1 (−27, 0, 19), Casa 2 (−12, 0, 23), Casa 4 (13, 0, −14) e Casa 5 (28, 0, −26). Todas dá para entrar.
  - Praça em (24, 0, −12), estacionamento em (10, 0, 33), 8 carros, cercas e árvores.
  - Um anel de 320 pinheiros (raio 64 a 98) e a barreira invisível de 48 lados no raio 63.
  - O chão é uma laje de 220 × 220 m feita por último, com um buraco na escada do bunker.
- **Bunker** (embaixo do quintal da casa da família):
  - a escada começa no meio do cômodo de trás, com grade em volta do buraco, e desce para os fundos (19 degraus, do piso a −3,2 m);
  - um patamar de 1,2 m e a porta blindada (1,2 × 2,3 m) levam às salas: sala comum (mesa e rádio), quarto (2 beliches) e despensa (prateleiras e tambores);
  - uma lâmpada quente (intensidade 5, alcance 9) e eco de corredor de pedra;
  - lá embaixo a luz do dia não chega (veja "Clima e ciclo do dia").
- **Postes (`World/PosteDeLuz`):** 28 postes com luz de sódio (spot, intensidade 95, alcance 16, ângulo 105°, sem sombra) e um cone falso de luz.
  - Eles acendem com a noite, cada um com um atraso próprio. Alguns piscam (`i % 7 == 3`) e alguns estão quebrados (`i % 11 == 5`).
  - O cone e a lâmpada mudam por MaterialPropertyBlock. A luz só fica ligada quando o poste está aceso.

## Clima e ciclo do dia (Parte 4)

- **Montado por `Editor/MontagemDoClima`** (menu Zombie City Cleaner > Montar clima e ciclo do dia): o objeto "Clima", o perfil do dia, a neblina de altura no renderizador, e o renderizador dos braços. Pode rodar de novo.
- **"Clima"** tem um NetworkObject e estes scripts:
  - **`World/CicloDoDia`:** o anfitrião manda na hora e a envia a cada 1 s (`HoraRede`); entre um envio e outro, cada um avança sozinho. Fora da partida o relógio roda localmente.
    - `CicloDoDia.Hora` (0 a 24) e `CicloDoDia.Noite` (0 das 8h às 17h, 1 das 20h às 5h, com transição suave) são estáticos, para qualquer script ler.
    - Começa às 21h, e um dia inteiro leva 24 minutos reais.
    - Teste: o anfitrião avança ou volta 1 hora com Page Up / Page Down. `-hora N` na linha de comando muda a hora inicial.
  - **`World/AmbienteDoDia`:** troca os tons de dia e de noite (neblina, luz ambiente, cor, força e altura de "Sol e lua", o peso dos dois volumes e o fundo das câmeras) e passa os valores da neblina de altura ao shader.
    - Dia: neblina pesada e clara (cinza, densidade 0,032). Noite: neblina escura e azulada (densidade 0,042) e luar frio. Neblina clara à noite apagaria as poças de luz dos postes.
    - **Subsolo:** quando a câmera desce de −0,3 m para −1,5 m (a escada do bunker), a luz ambiente, o sol e as duas neblinas caem para 15%. Lá embaixo só a lâmpada e a lanterna iluminam.
    - **Dentro das casas e do prédio** (Parte 8): com a câmera numa `World/AreaInterna`, as duas neblinas caem para 40% (`neblinaDentro`), em 0,6 s (`trocaDentro`). A luz não muda, porque ela entra pelas janelas.
    - **Na luneta da sniper:** só na máquina de quem mira, as duas neblinas caem para 35% (`neblinaNaLuneta`; quem liga é a `ArmaJogador`, por `AmbienteDoDia.NaLuneta`). Sem isso, a neblina escondia tudo a mais de ~40 m, e a luneta não servia.
      - A montagem da cidade põe uma área em cada casa (do piso ao forro, W × D) e uma no prédio (os 4 andares). O bunker usa a regra do subsolo.
      - Sem isso, a neblina de fora enchia as salas.
  - **Sem som ambiente:** o vento, os gemidos distantes e a sirene saíram a pedido (o `SomAmbiente` foi apagado). O que se ouve é a voz dos zumbis, o tiro e os sons da arma e da lanterna, com o eco das construções.
  - **"Partículas no ar" (`Atmosphere/ParticulasNoAr`):** uma caixa de 26 × 12 × 26 m que segue a câmera, com cinza de dia e poeira à noite.
- **Neblina de altura (`Shaders/NeblinaDeAltura.shader`):** um passe de tela cheia antes dos transparentes (os cones e as partículas ficam por cima).
  - A densidade cai com a altura (exponencial, calculada exata ao longo do raio) e tem manchas de ruído arrastadas pelo vento.
  - Abaixo da altura base a conta usa a base, para a neblina não engrossar debaixo da terra.
- **Lanterna:** intensidade 34, alcance 28, abertura 62° (52° por dentro), com um cookie gerado por código (o centro forte, um anel leve do refletor e a borda mais fraca e quente). A sombra dela começa a 0,6 m da luz (`shadowNearPlane`): a pistola e os braços do corpo ficam na frente do peito e cortavam a luz ao meio.
  - **O facho converge na mira:** um raio sai do olho (sem os jogadores, os braços, os corpos, a cápsula do zumbi de perto e os gatilhos), e a luz aponta do peito para onde ele bateu (de 1,2 a 30 m). Antes ela apontava 9° abaixo do olhar e seguia com atraso: o centro caía uns 2 m abaixo da mira a 10 m, e ela se arrastava ao virar.
  - O dono usa a câmera dele, sem atraso (o `Pitch` da rede só muda a cada 0,5°). Os outros usam o `Pitch`, suavizado (`atraso`).
  - Ao ligar, ela falha duas vezes em 0,22 s antes de firmar.
- **Como testar:** em Play, Page Up / Page Down mudam a hora. Por código, `typeof(CicloDoDia).GetProperty("Hora").GetSetMethod(true).Invoke(null, new object[] { 12f })`.
- **Luz em tempo real (decidido na Parte 8):** a cidade não tem luz assada. Os 28 postes são spots sem sombra (Forward+), a lanterna tem sombra e o SSAO do `PC_Renderer` dá o sombreado de contato.
  - O ciclo do dia é contínuo, e os postes acendem, apagam e piscam: lightmap ou APV não acompanham. Seriam dois cenários de APV misturados pela hora, só para ganhar a luz rebatida.
  - No benchmark, a GPU gastou 2,4 a 3,0 ms à noite com tudo isso ligado.
- **Oclusão:** assada pela montagem da cidade, no fim (`AssarOclusao`, uns 2 s). Os dados ficam em `Scenes/Demo_Cidade/OcclusionCullingData.asset`.
  - Só a arquitetura grande é oclusora: as casas, o prédio, o bunker e os 25 pedaços do chão (que escondem o bunker). O resto (objetos de rua, móveis, pinheiros) só é escondido. Os zumbis usam a oclusão dinâmica, que é o padrão.
  - Menor oclusor 3 m e menor buraco 0,25 m: portas e janelas deixam ver através.
  - Mudou a cidade, rode a montagem de novo, e a oclusão vem junto.
- **LOD:** não há LOD Groups. Os triângulos vêm quase todos dos zumbis (o benchmark tem a conta), a câmera corta a 150 m com neblina, e os móveis de dentro das casas somem pela oclusão.

## Os zumbis (`Art/Characters/Zombie/`)

- **Os 4 modelos:** `Zombie1` (o garoto, 1,75 m), `ZombieHomem2` (1,80 m), `ZombieMulher` (1,68 m) e `ZombieMulherLoira` (1,70 m).
  - Vêm de `Modelo 3D/Zumbis/` na pasta de arte (`Zumbis.blend` e o script `zumbis.py`). Ao atualizar, copie de novo os `<modelo>.fbx` e as texturas `Texturas/<modelo>_basecolor.jpg` para cá e rode a montagem dos zumbis.
  - Os 4 têm o mesmo esqueleto (os 22 ossos do zumbi antigo, com os mesmos nomes) e a mesma hierarquia (`Zombie_Body`, `Zombie_Rig`), cada um com as juntas nas medidas dele.
  - O antigo `NovoZombie` (com desmembramento e os pedaços soltos) saiu do projeto.
- **Importação:**
  - mede a altura do modelo na escala 1 e olha para +Z;
  - deixe **"Bake Axis Conversion" desligado**, porque ligado ele vira o zumbi de costas. Os filhos do FBX ficam com rotação de 270° em X, o que é normal;
  - rig Generic, com avatar criado do próprio modelo;
  - 5 clipes, sem root motion: `Idle` (3 s), `Walk` (1,33 s) e `Chase` (1 s) em loop; `Attack` (1 s) e `HitReact` (0,5 s) sem loop.
  - Cada FBX traz as animações assadas no próprio esqueleto (as posições dos ossos são as dele): os clipes de um modelo não servem no outro.
  - Com o braço levantado (perseguir, atacar, o braço meio erguido do andar), a palma fica para baixo; pendurado, o braço fica como no zumbi antigo.
- **Malha:** 3 submeshes, achados pelo nome do material que veio do Blender: `<modelo>_Corpo`, `<modelo>_Cabeca` e `Zombie_Flesh` (a tampa de carne no corte do pescoço). A montagem monta duas malhas em `Malhas/`:
  - `<modelo>_Inteira`: corpo e cabeça num submesh só (um draw call), a do zumbi vivo e do corpo inteiro;
  - `<modelo>_SemCabeca`: o corpo (submesh 0) e a tampa (submesh 1), a do corpo que caiu com a cabeça explodida.
- **Materiais:** `Materials/<modelo>.mat` (URP Lit com a textura, suavidade 0,15, sem o passe de motion vectors) e `Materials/Zombie_Flesh.mat` (a carne da tampa, cor sólida `#6B0F0F`).
- **`Zombie.controller`** (refeito pela montagem, com os clipes do `Zombie1`; Write Defaults desligado em todos os estados):
  - **Camada base:** "Andar", uma árvore pelo float `Speed` (m/s): `Idle` em 0, `Walk` em 0,6 e `Chase` em 1,12. A morte não tem estado: o zumbi some e o corpo com física cai no lugar.
    - O desmembramento, pelo inteiro `Pernas` (de qualquer estado, sem voltar para si mesmo): 1 "Mancando sem a esquerda" e 2 "sem a direita" (`Idle` e `MancarE`/`MancarD` em 0,45 m/s), 3 "Caindo" (`Cair`, 0,8 s) e 4 "Rastejando" (`RastejarParado` e `Rastejar` em 0,35 m/s). Os clipes vêm do `<modelo>_Extra.fbx`.
  - **Camada "Ações":** máscara `Zombie_Tronco.mask` (da `spine` para cima). Os triggers `Atacar` (`Attack`) e `Tiro` (`HitReact`) voltam sozinhos ao estado vazio.
  - Os outros modelos usam `Zombie_<modelo>.overrideController`, com os clipes deles no lugar dos do `Zombie1` (os 5 de sempre e os 5 do desmembramento).

## O jogador e as armas (`Art/Characters/Jogador/`)

- **Arquivos:** exportados de `Modelo 3D/Jogador/Jogador.blend` na pasta de arte (por script; veja "Convenções das armas" no `CLAUDE.md` de lá). Ao atualizar, copie os FBX e a `Paleta.png` de novo e rode a montagem das armas.
  - `Jogador.fbx`: o corpo, visto pelos outros jogadores.
  - `Jogador_FP_<Arma>.fbx` (Pistola, Rifle, Submetralhadora, Espingarda e Sniper): os braços de primeira pessoa de cada arma, com a arma junto.
    - A sniper (um fuzil de ferrolho de caça, com coronha de madeira e luneta) foi feita pelos scripts de `Blender/` a partir dos braços do rifle: usa as animações dele, menos o tiro (`Fire`, 1,25 s: o coice e a mão direita puxando o ferrolho). O osso `slide` dela é o ferrolho, no eixo do cano. A luneta fica baixa e para a frente, para não tapar a tela.
  - `Armas.fbx`: as 4 armas avulsas, em peças. A montagem junta as peças de cada uma em `Malhas/Arma_<Arma>.asset` (a origem no cabo, o cano em +Z, o topo em +Y), usadas no corpo e no suporte.
  - A textura de cores fica em `Art/Paleta.png` (movida para lá na Parte 8, porque a cidade usa a mesma): 16×16, RGBA, filtro Point, sem mipmaps e sem compressão.
- **Cores pela paleta:** o personagem é feito de blocos de cor sólida, e o UV de cada face aponta para um quadradinho da `Paleta.png`.
  - As linhas 0 a 3 são as 4 variações da família (pele, cabelo, jaqueta, camisa, calça, sapato, cinto, escuro e jaqueta escura). As luvas ficam nas colunas 9 e 10, iguais nas 4 linhas.
  - `Materials/Jogador_Variacao0..3`: os UVs apontam para a linha 0, e cada material desloca a textura em `k/16` no V para mostrar a linha `k`.
  - As armas usam a linha 12 com o material `Materials/Pistola` (sem deslocamento). Por isso elas têm material próprio, inclusive dentro dos braços (`FP_Bracos_<Arma>`, dois submeshes).
- **As 5 armas (`Combat/Armas`):** a tabela dos números de cada uma (`DadosDaArma`), na ordem de `TipoDeArma` (o número que vai pela rede). A vida dos zumbis é contada em tiros de pistola: o corpo aguenta 6 e cada braço ou perna, 3 (entre parênteses, quantos tiros no tronco matam e quantos num membro o arrancam):

  | Arma | Pente | Cadência | Dano no tronco | Dano num membro | Dispersão (parado / correndo +) | Recarga |
  |---|---|---|---|---|---|---|
  | Pistola | 12 | 0,18 s | 1 (6) | 1 (3) | 0,3° / 1,2° | 1,67 s |
  | Rifle (automático) | 30 | 0,1 s | 1,5 (4) | 1,5 (2) | 0,5° / 1,8° | 1,92 s |
  | Submetralhadora (automática) | 25 | 0,075 s | 1 (6) | 1 (3) | 0,9° / 1,6° | 1,67 s |
  | Espingarda (8 chumbos) | 6 | 0,92 s (com a bomba) | até 5 m: 7 (1); até 15 m: 3 (2); mais longe: 1 | até 5 m: 3 (1); até 15 m: 1,5 (2); mais longe: 0,5 | 3,5° / 1° | 0,54 s por cartucho |
  | Sniper (de ferrolho, com luneta) | 5 | 1,25 s (com o ferrolho) | 8 (1) | 8 (1) | na luneta 0°; sem ela 4° / 2,5° | 1,92 s |

  - Na cabeça, qualquer acerto explode a cabeça (na espingarda, um chumbo só de perto; veja abaixo). A submetralhadora é a pistola automática; o rifle é a arma de grau acima (mais dano, mais pente, mais alcance).
  - **A espingarda conta os chumbos juntos** (`chumbosJuntos`): cada zumbi atingido leva um acerto só por disparo, com o dano da faixa da distância (`faixas`). A parte que conta é a que o chumbo do meio pegou (um chumbo sai sempre bem no centro da mira); se ele não pegou aquele zumbi, a que mais levou chumbo. De perto (até 5 m), um chumbo na cabeça basta, e o tiro que mata joga o corpo para trás (`arremessoAte`; o corpo voa a 4,5 m/s para trás e 2,2 m/s para cima, `ZombieManager.arremesso`). Mais longe, um chumbo perdido na cabeça não conta.
  - A tabela também tem o coice, o alcance, o perfil do som do tiro, quando o carregador sai e entra (para o som), a boca do cano na malha avulsa e, na sniper, o campo de visão da luneta (`fovLuneta`, 12°), a dispersão sem ela, o ferrolho (o som dele 0,42 s depois do tiro) e as animações do corpo (`corpoComo`: as do rifle).
  - **Medido** (com a mira exata, num zumbi parado de frente): pistola, submetralhadora e rifle matam com 6, 6 e 4 tiros no tronco; a espingarda mata com 1 de perto (arremessando) e 2 a 10 m; a canela cai com 3 tiros de pistola ou de submetralhadora, a coxa com 2 de rifle ou de espingarda a 9 m, e com 1 de sniper ou de espingarda de perto.
- **Corpo (`Jogador.fbx`):**
  - 1,80 m de altura, olhando para +Z, com "Bake Axis Conversion" desligado como no zumbi;
  - rig Generic, com os ossos do zumbi mais `fingers` e `thumb` em cada mão e `weapon.R` (filho de `hand.R`); as mãos são de luva;
  - **a arma no corpo:** o vazio `Arma_TP` (filho do `weapon.R`) tem os eixos da arma. A montagem põe dentro dele as 4 armas avulsas (`Arma_<Arma>`, com a malha e uma "Boca" na ponta do cano), só a da mão ligada.
    - As armas ficam com 90° em X: um vazio filho de osso chega do FBX com o eixo do osso convertido (o cano no Y dele e o topo no -Z).
  - os clipes da pistola são `Idle` (2 s), `Walk` (0,75 s, ~1,33 m/s), `Run` (0,58 s, ~3,33 m/s) e `Aim` (2 s), em loop, mais `Fire` (0,25 s). Idle, Walk e Run seguram a pistola baixa com as duas mãos;
  - os de arma longa são `<clipe>_Rifle`, `<clipe>_Submetralhadora` e `<clipe>_Espingarda`, com as mesmas durações: o tronco girado para a direita, a coronha no ombro e a mão esquerda no guarda-mão (ou na telha).
- **`Jogador_TP.controller`:**
  - a camada base é uma árvore pelo float `Speed` (0, 1,33 e 3,33);
  - a camada "Mira" usa a máscara `Jogador_Tronco.mask` (da `spine` para cima): `Aiming` (bool) leva ao `Aim`, e `Fire` (trigger) toca o tiro;
  - `Jogador_TP_<Arma>.overrideController` (rifle, submetralhadora e espingarda) troca os 5 clipes pelos de arma longa. O `JogadorRede` troca o controlador do corpo quando a arma da mão muda.
- **Primeira pessoa (`Jogador_FP_<Arma>.fbx`):**
  - a origem é o olho: o modelo é filho da câmera, com posição e rotação zero, enquadrado para o FOV de 55° da câmera dos braços;
  - **o cano aponta para a mira:** a arma fica quase paralela ao olhar (girada só o bastante para a reta do cano cruzar o centro da tela a 20 m), então na tela o cano aponta para a mira, de onde sai o tiro. Girada para o lado (como antes, 5° a 12°), ela parecia torta;
  - ossos: `upper_arm/forearm/hand/fingers/thumb` de cada lado, mais `gun` (a arma) e as peças que mexem: `slide` (o ferrolho), `mag` (o carregador), `pump` (a telha) e `shell` (o cartucho da recarga da espingarda);
  - os ombros ficam fora da tela, em lugares diferentes em cada arma. Os braços são 12% mais compridos e as mãos 12% maiores que no corpo, um truque de primeira pessoa;
  - a boca do cano é o vazio `Boca_FP_<Arma>`;
  - clipes: `Idle`, `Walk` e `Run` (a arma baixa e deitada), em loop, mais `Fire` (na espingarda, com a bomba), `DryFire`, `Reload` (na espingarda, um cartucho) e `Sacar` (a arma sobe de baixo da tela).
- **`Jogador_FP.controller`** (refeito pela montagem das armas, com os clipes da pistola):
  - a base é uma árvore pelo `Speed` (0, 2,0 e 4,2);
  - a camada "Ações" tem os estados `Fire`, `DryFire`, `Reload` e `Sacar`, que o código toca do começo (`Animator.Play`). No automático, um tiro recomeça o anterior. Cada um volta sozinho ao estado vazio;
  - `Jogador_FP_<Arma>.overrideController` troca os clipes pelos da arma.
- **Write Defaults desligado nos Animators:** está desligado em todos os estados (do jogador e do zumbi). Com ele ligado, o estado vazio de uma camada de cima jogaria o personagem na pose T.
- **Lanterna:** não a cole na câmera, porque ela estoura a arma de luz. Ela fica no peito (veja "Rede e jogador online") e não ilumina os braços de primeira pessoa: o `LanternaJogador` a desliga só durante o desenho da câmera dos braços (`RenderPipelineManager.beginCameraRendering`). Sem isso, a arma e a manga esquerda, dentro do facho, ficavam brancas. De perto, ela ainda clareia muito as armas escuras do suporte.

## Rede e jogador online (`Assets/_Game/Scripts/`)

- **Entrar na partida (`Network/SessaoOnline`, no objeto "Rede" junto do NetworkManager e do UnityTransport):**
  - `Hospedar()` cria uma partida privada de até 4 jogadores com `WithRelayNetwork()` e guarda o `Codigo`. `Entrar(codigo)` usa `JoinSessionByCodeAsync`. O pacote de multiplayer liga o Netcode sozinho (o anfitrião vira host).
  - Antes, os serviços da Unity são iniciados com um perfil anônimo sorteado a cada execução, porque duas cópias no mesmo PC não podem usar a mesma conta.
  - `HospedarLocal()` / `EntrarLocal(ip)` usam IP direto na porta 7777, sem Relay. Servem para testar sem internet ou jogar em rede local.
  - Quando o anfitrião sai ou a conexão cai, o estado volta a `Desconectado` com uma mensagem, e o menu reaparece.
- **Menu (`UI/MenuOnline`, OnGUI):**
  - telas de hospedar, entrar com código e modo local;
  - na partida, o código e o número de jogadores ficam no canto, e ESC abre o menu (o mundo continua rodando);
  - `MenuOnline.Aberto` bloqueia os comandos do jogador.
- **Linha de comando, para testes:** `-entrar CÓDIGO`, `-hospedar-local`, `-entrar-local [ip]`, `-bot` e `-hora N`.
  - Com `-bot`, o jogador local anda em círculo, atira a cada 1,5 s e troca a lanterna a cada 5 s.
    - Com um zumbi vivo a até 18 m, ele mira no peito desse zumbi e recua devagar (o acerto vai ao anfitrião como o de um jogador).
    - Com a sniper na mão, ele fica na luneta (testa o nível de detalhe pela luneta, na rede).
    - A cada 3 s, ele registra no log:
      - a hora, os zumbis que recebe e a vida (`[robô] hora HH:MM, zumbis N (perto P, médio M; caídos C, sem cabeça S), vida V`);
      - o que vê dos outros (`[robô] jogador N: posição, olhar, tiros recebidos, lanterna`).
  - Matar a build sem fechar a conexão deixa o jogador dela na partida até o tempo limite do transporte.
- **Como testar com dois jogadores:**
  1. gere a build em `Builds/Teste/` (Development, ignorada pelo git). A build deixa o `InputSystem_Actions` nos Preloaded Assets do Player Settings; tire de lá antes de commitar;
  2. no Editor, em Play, hospede (com código, ou "Hospedar local" para testar sem internet);
  3. rode a build sem janela, por exemplo `ZombieCityCleaner.exe -batchmode -nographics -entrar CÓDIGO -bot -logFile caminho.log` (ou `-entrar-local 127.0.0.1` no lugar de `-entrar CÓDIGO`);
  4. confira no Editor (`JogadorRede.Todos`, `TirosRecebidos`) e no log da build.
  - Ao terminar, desligue `MenuOnline.Bot` se ele foi ligado no Editor: sem Domain Reload, ele continuaria ligado no próximo Play.
- **O jogador (`Assets/_Game/Prefabs/Jogador.prefab`, montado por `Editor/MontagemDaRede`, menu Zombie City Cleaner > Montar jogador e rede):**
  - **Raiz:** CharacterController (1,8 m, raio 0,3), NetworkObject e NetworkTransform com autoridade do dono (posição e só o giro em Y).
    - `JogadorRede`: as câmeras, o corpo, a cor da família (o servidor dá a primeira livre), a arma da mão (`Arma`) e o RPC do tiro.
    - `MovimentoPrimeiraPessoa`: andar 2,0 m/s, correr 4,2 m/s, mirando 1,3 m/s, pulo; abaixo de y = −20, volta ao ponto de nascimento.
    - `ArmaJogador`: a arma da mão (os números em `Combat/Armas`), raycast do centro da tela, recarga com R, mira no botão direito (FOV 70 → 58), mira, munição e o nome da arma na tela.
      - Tem os 4 modelos de primeira pessoa (`modelos`, `animadores`, `bocas`); `Trocar(arma)` liga o da arma nova, enche o pente e toca o `Sacar` (0,4 s sem atirar).
      - O rifle e a submetralhadora atiram com o gatilho apertado; o próximo tiro conta do anterior, sem perder a cadência com o tempo do quadro. A espingarda solta 8 chumbos, cada um com o seu raio, e bombeia sozinha.
      - A dispersão é um círculo em volta do olhar, girada com a câmera.
      - A espingarda recarrega cartucho a cartucho (R enche o tubo), e atirar interrompe a recarga. As outras trocam o pente inteiro, com o som do carregador saindo e entrando.
      - Mirando uma arma do suporte do bunker a até 2,2 m, aparece "[E] Pegar … (deixa …)", e E pede a troca (`ParedeDeArmas.PedirTroca`).
      - **A luneta (a sniper):** mirando, a câmera fecha de 70° para 12° (~6,7× de aumento); quando ela já fechou, os braços somem (a câmera dos braços desliga) e a tela mostra a luneta (OnGUI: o círculo da altura da tela, preto em volta, e o retículo com as linhas grossas da borda e um ponto vermelho no meio). O mouse fica mais lento na proporção do aumento (`MovimentoPrimeiraPessoa.MultiplicadorDoOlhar`). Entrar e sair tocam sons gerados por código (`Efeitos.LunetaEntra`, `LunetaSai`). Recarregando, ela sai da luneta. A `JogadorRede.Luneta` (rede) avisa o anfitrião.
      - Antes do raio, `Physics.SyncTransforms()` (as hitboxes andam com a animação).
      - O raio ignora a camada Ignore Raycast (a cápsula do zumbi de perto) e os pedaços (camada 8).
      - Ele pega todos os acertos (`RaycastNonAlloc`, buffer de 64, em ordem de distância). Com o buffer cheio, o Unity não garante os mais perto.
      - Os acertos de um disparo vão para o anfitrião no fim dele (`MandarAcertos`), cada um com o dano da distância, no tronco ou no membro (veja a tabela). Uma bala que atravessa o antebraço e pega o braço logo atrás conta uma vez só naquele membro.
      - **Ignora o CharacterController do próprio jogador.** O olho (1,62 m) fica bem no topo da cápsula física, que medida vai de 0,18 a 1,62 m, menor que a configurada.
        - Um raio que nasce dentro de um colisor não o acerta, mas um que nasce na superfície e desce, acerta. Então o tiro para baixo (na cabeça de um zumbi perto) batia nela a distância zero e soltava faíscas em cima da cabeça do jogador.
        - Qualquer raio novo que saia do olho precisa pular esse colisor.
      - À queima-roupa, os primeiros 35 cm do tiro são conferidos com uma cápsula fina: a hitbox que estiver ali leva o tiro, mesmo que a câmera esteja dentro dela (um raio que nasce dentro de um colisor não o acerta).
      - Acertou uma `Hitbox`: sangue na hora, a marca de acerto na mira (vermelha na cabeça) e o acerto vai para o anfitrião (`JogadorRede.AcertarZumbi`).
      - **O tiro atravessa até dois braços ou antebraços:** o braço leva o acerto (e pode cair), e a bala segue para o que estiver atrás. Numa multidão, as mãos de quem ataca ficam na frente da mira.
      - No cenário: faíscas e a marca do tiro (decal).
    - `LanternaJogador`: luz no peito cujo facho converge no ponto da mira; F liga e desliga. Ela fica desligada só enquanto a câmera dos braços desenha (os braços não recebem a luz dela).
  - **A arma no corpo do dono fica desligada** (o renderer, nem sombra): a ponta do rifle e da espingarda passa dos 60 cm em que a sombra da lanterna começa, e a sombra do cano aparecia no facho. O corpo continua só como sombra.
    - `VidaJogador`: `Vida` (100, só o anfitrião escreve). O dono vê a barra, o clarão vermelho e o tranco do golpe. Se a vida acaba, ele volta ao ponto de nascimento com a vida cheia ("VOCÊ CAIU"). Depois de 8 s sem apanhar, recupera 2 por segundo (provisório, até o saque da Parte 7).
  - **"Corpo":** `Jogador.fbx` com o `Jogador_TP.controller`. Para o dono, os renderers viram só sombra. O peito e a cabeça giram por cima da animação para acompanhar o olhar (`Pitch`).
  - **"Cabeca" (1,62 m) > "Camera"** (a do mundo, que não desenha a camada 6) **> "CameraBracos"** (sobreposição, FOV 55, só a camada 6) e **"Bracos"** (os braços de primeira pessoa de cada arma, na camada 6 "ArmasPrimeiraPessoa"). A câmera dos braços está na pilha da câmera do mundo, então os braços nunca atravessam paredes.
  - **"Lanterna"** fica no peito, abaixo e à frente dos olhos. Assim ela não estoura a arma de luz. O facho aponta do peito para o ponto da mira (veja "Lanterna" em "Clima e ciclo do dia").
  - **"Bracos"** tem um filho por arma (`Bracos_<Arma>`, o FBX de primeira pessoa com o controlador ou o override dele), só o da arma da mão ligado.
  - Só o dono liga as câmeras, o ouvido, os braços, o movimento e a arma.
- **O que vai pela rede:**
  - a posição e o giro (NetworkTransform);
  - `Pitch`, `Mirando`, `Luneta` e `Ligada` da lanterna (NetworkVariables escritas pelo dono);
  - `Cor` e `Arma` (escritas pelo servidor). Quando a `Arma` muda, todos trocam a arma do corpo e o controlador dele; o dono troca também a de primeira pessoa;
  - o tiro, por `TiroRpc` (`SendTo.NotMe`), com um `Impacto` por chumbo (um só nas armas de bala): o ponto e o que ele acertou (`TipoDeAcerto`: nada, cenário, carne ou outro). Quem recebe toca a animação de tiro do corpo, o clarão e o som 3D da arma dele (e a bomba da espingarda), e o efeito certo de cada impacto (sangue, ou faíscas e o furo).
  - o acerto num zumbi, por `AcertoRpc` (`SendTo.Server`): Id, geração, um byte com a região (bits 0 a 2), o membro (bits 3 a 6) e o arremesso (bit 7), o dano e o ponto; um por zumbi atingido (na espingarda) ou por pedaço atingido (nas outras). O anfitrião confere quem mandou (só o dono atira com o jogador dele).
  - **o suporte de armas** (`Combat/ParedeDeArmas`, no NetworkObject da cena "Parede de armas"): o conteúdo das 5 vagas num `NetworkVariable<long>` (8 bits por vaga: 0 vazia, senão o tipo + 1), escrito pelo servidor. O pedido de troca vai por `TrocarRpc` (`SendTo.Server`); o anfitrião confere a distância (3 m dos pés ao meio da vaga) e troca a vaga e a `Arma` do jogador.
  - A velocidade dos outros, usada pela animação, é medida pela posição recebida a cada 0,1 s.
- **Ações de input do projeto** (`InputSystem_Actions`, mapa Player): as do modelo da Unity, mais `Aim` (botão direito / gatilho esquerdo), `Reload` (R / X) e `Flashlight` (F / direcional para cima). O tiro no controle foi para o gatilho direito.
- **`Combat/Efeitos`** (objeto "Efeitos" na cena): sons gerados por código (tiro, clique, recarga, lanterna), o clarão do cano e as faíscas e poeira do impacto.
  - **O tiro (`Combat/SomDeTiro`):** camadas geradas por código e normalizadas pela energia. O peso de cada uma no `Perfil` é a raiz da parte da energia. Há um perfil por arma: `Pistola`, `Rifle` (estalo mais agudo, cauda mais longa), `Submetralhadora` (seca e curta) e `Espingarda` (grave e comprida, sem ferrolho).
    - As camadas: o estalo (ruído em ~3 kHz, 6 ms), o soco (um seno que cai de 160 para 60 Hz), o corpo (ruído em ~700 Hz, 40 ms), os cliques do ferrolho, a cauda (ruído grave que escurece em ~0,35 s) e dois rebatimentos abafados (130 e 290 ms).
    - No fim, uma saturação suave (tanh) e o pico em 0,95. Saem 3 variações de cada arma (`Efeitos.Tiros[arma]`), sorteadas a cada tiro.
    - Sons à parte: `Carregador` (o pente saindo e entrando), `Bomba` (para trás e para a frente, 0,3 s depois) e `Cartucho`.
    - Para ouvir fora do jogo: `SomDeTiro.Amostras(perfil, semente)` devolve as amostras (44,1 kHz, mono). As prévias ficam em `Sons/Previas/` da pasta de arte.
  - `Efeitos.Som(...)` espacial tem alcance (o tiro de outro jogador vai a 150 m) e um passa-baixa por fonte: quanto mais longe, mais abafado (22 kHz perto, ~1,8 kHz no alcance).
  - Os materiais `Particulas` e `Clarao` ficam em `Art/Materials/`. O mesmo objeto tem o `Combat/Sangue` (veja "Combate e morte").
  - O brilho do clarão é um Quad primitivo. O colisor dele é apagado na hora (`DestroyImmediate`): com `Destroy`, ele só sumiria no fim do quadro, e o raio do tiro, feito logo depois, podia batê-lo.
- **Cena:**
  - **"Pontos de nascimento":** 4 pontos na sala comum do bunker, postos pela montagem da cidade. É também onde o jogador volta quando cai.
  - **"Câmera de teste":** com `CameraDoMenu`, aparece no menu e some quando o jogador local nasce.
- **Peças das armas nos modelos:** a "Boca" de cada arma do corpo e o `Boca_FP_<Arma>` (filho de `gun` nos braços) marcam a ponta do cano.
- **O suporte de armas ("Parede de armas"):** montado pela montagem das armas no lugar do móvel `Suporte_Armas` do bunker, mais a "Vaga da mesa", em cima da `Mesa_Grande` da sala comum (a sniper, deitada de lado ao longo da mesa: o "alto" da vaga aponta para cima).
  - 4 vagas ("Vaga 1" a "Vaga 4", de baixo para cima, no topo de cada prateleira), cada uma com um BoxCollider gatilho (o que a mira procura), o `VagaDeArma` e a arma deitada ("Arma": a malha muda com o que está na vaga).
  - A arma fica com o cano ao longo da parede, o lado esquerdo para fora e a parte de baixo encostada na prateleira, centrada pelo tamanho da malha.
  - Começa com pistola, submetralhadora, rifle e espingarda (de baixo para cima) e a sniper na mesa. A arma pega vem cheia, e a munição de reserva é infinita até o saque da Parte 7.

## Os zumbis na cidade (Parte 5, `Assets/_Game/Scripts/Zombies/`)

- **Montados por `Editor/MontagemDosZumbis`** (menu Zombie City Cleaner > Montar zumbis). Pode rodar de novo.
- **Os prefabs `Prefabs/Zumbis/Zumbi_<modelo>.prefab`** (um por modelo, todos iguais na montagem):
  - **Raiz (camada 7):** `Zumbi` (com o índice do `modelo`), NavMeshAgent (desligado no prefab, raio 0,3, altura do modelo), CapsuleCollider (a altura do modelo, raio 0,28), Rigidbody cinemático e AudioSource (a voz).
  - **"Modelo":** o FBX do modelo com o controller (ou o override dele) e a malha `<modelo>_Inteira`. O Animator guarda o estado quando é desligado (`keepAnimatorStateOnDisable`).
  - **A cápsula da raiz** também é uma `Hitbox` (região `Corpo`): é ela que leva o tiro no nível médio.
  - **11 hitboxes nos ossos** (camada 7, com `Hitbox`, a `RegiaoDoCorpo` e, nos membros, o `Membro`: braço, antebraço, coxa ou canela de cada lado), medidas na malha com o modelo posto na pose de repouso (pela bindpose; o FBX abre numa pose da animação):
    - a cabeça é uma esfera: a cabeça acima do corte do pescoço (a altura da tampa), com o queixo e sem o cabelo comprido que desce até os ombros. O raio é a média das três metades (14 a 16 cm), para a borda do cabelo também contar;
    - o resto são cápsulas de osso a osso: duas no tronco (`Tronco`), braço e antebraço de cada lado (`Braco`) e coxa e canela de cada lado (`Perna`). O raio é a distância dos vértices presos a cada osso até o segmento, no percentil 60 a 75 (braço 6 a 8 cm, coxa 8 a 10 cm).
  - `raizDoModelo`, `esqueleto` (os 22 ossos, na ordem da malha) e `partes` (os 11 ossos com física, na ordem de `CorpoDeZumbi.Partes`): o corpo que cai copia a pose deles.
  - O `Zumbi` não tem Update próprio: o `ZombieManager` faz um laço só para todos.
- **Os corpos `Prefabs/Zumbis/Corpo_<modelo>.prefab`** (ragdoll, `CorpoDeZumbi`): o FBX desempacotado, sem Animator, na camada 8 e na pose de repouso.
  - 11 partes com Rigidbody e colisor (cápsulas um pouco mais finas que as hitboxes e a esfera da cabeça a 85%): quadril, peito, cabeça, braço e antebraço (com a mão) de cada lado, coxa e canela de cada lado. Massa total de 64 kg.
  - 10 CharacterJoints com limites do corpo humano (o joelho e o cotovelo só dobram para um lado; o quadril dobra 100° para a frente). Os limites contam da pose em que a junta foi criada (a de repouso): por isso o corpo nunca é desligado no pool, só fica cinemático, sem colisão e invisível.
  - A pele usa o quadril como `rootBone` (a raiz do esqueleto fica parada enquanto o corpo cai).
- **"Zumbis" na cena:** um NetworkObject com o `ZombieManager` e o `BenchmarkDeZumbis`.
  - Os filhos são 29 `PontoDeHorda` (quantidade e raio; 258 zumbis na rua, na praça, no estacionamento, nas casas e nos 4 andares do prédio).
    - Eram 129 na Parte 5; o usuário pediu o dobro (ajustes antes da Parte 7). Na rua o raio de cada ponto cresceu 1 m, para os grupos não ficarem amontoados; nas casas e nos andares do prédio ficou o mesmo.
    - No benchmark (14:42), o quadro foi de 3,8 ms com 100 para 4,2 ms com 250 zumbis.
  - `areasLivres`: a casa da família com o bunker e a frente dela não recebem zumbis.
- **NavMesh ("Navegação"):** um `NavMeshSurface` pelos colisores, só no volume da cidade (128 × 24 × 128 m, dentro da barreira). Ele é refeito pela montagem dos zumbis e pela da cidade.
- **Comportamento (`Zumbi`, só no anfitrião):**
  - Parado e vagando: vaga devagar (0,55 m/s) em volta de onde está. Um em cada três para três vezes mais.
  - Perceber, só no nível Perto:
    - contato (2,5 m) e passos correndo (9 m) são na hora;
    - a visão (140°, com linha de visada no cenário) enche um alerta, mais rápido de perto; desconfiado, ele para e vira para o jogador;
    - o alcance da visão é de 16 m de dia e 6 m à noite; à noite, sobe para 13 m embaixo de um poste aceso (`PosteDeLuz.Ilumina`) e 26 m com a lanterna ligada;
    - a lanterna apontada para ele denuncia mesmo de costas.
  - Tiro (`ZombieManager.Barulho`, chamado pelo `JogadorRede` no anfitrião): até 12 m, o zumbi vai atrás de quem atirou; até 35 m, vai investigar o lugar.
  - Perseguir a 1,1 m/s (o jogador andando já se afasta).
  - Atacar a 1,3 m: a cada 1,4 s, a animação `Attack`. O dano (12) sai 0,45 s depois, quando ela agarra, se o alvo ainda estiver ao alcance.
  - Desistir com o alvo a mais de 20 m, ou 6 s sem vê-lo: vai até onde o viu, olha em volta e volta a vagar ali.
  - Gemidos com trechos da `SomZombie1.mp3`: ao começar a perseguir e, de vez em quando, os que estão a até 18 m de quem escuta (no máximo 3 por segundo).
  - Os números ficam em `ajustes` (`AjustesDeZumbi`), no Inspector do `ZombieManager`.
- **LOD de IA (`ZombieManager`)**, pela distância até o jogador mais próximo, refeito a cada 0,2 s:

  | Nível | Distância | IA | Navegação | Animação | Colisão e sombra |
  |---|---|---|---|---|---|
  | Perto | até 15 m | completa, a cada 0,1 s | agente, desvio médio | `CullUpdateTransforms` | hitboxes; a cápsula só segura o jogador (camada Ignore Raycast); com sombra |
  | Médio | 15 a 40 m | só vagar e tiros, a cada 0,5 s | agente, desvio barato | `CullCompletely` | a cápsula é a hitbox; sem sombra |
  | Longe | mais de 40 m | nenhuma (zumbi virtual, parado) | agente desligado | desligada | nada; não desenha |

  - A histerese é de 3 m: para chegar mais perto é preciso passar a distância menos 3 m.
  - **A luneta:** para quem olha pela luneta, os zumbis na linha dela (o cone de meio ângulo 8°, até 160 m) contam a distância dividida pelo aumento (~6,7×): a 70 m, um zumbi vira Perto (desenhado, com as hitboxes, a da cabeça também, e enviado 10 vezes por segundo a quem mira). Vale no anfitrião (nível e rede) e nas cópias (as hitboxes), pelo olhar de cada jogador (`ZombieManager.Distancia`).
  - Quem persegue, ataca ou levou tiro há menos de 5 s fica sempre no Perto. Morto, o zumbi some (quem aparece é o corpo com física).
  - No máximo 40 zumbis pensam por quadro (`orcamentoPorQuadro`), em rodízio.
  - Além de 65 m de quem joga na máquina, o zumbi não é desenhado.
- **Os modelos e o pool:** cada modelo tem o seu pool. O modelo de cada zumbi sai do Id e da geração (`(id * 7 + geração * 3) % 4`), então o anfitrião e os clientes chegam ao mesmo sem mandar nada a mais. Numa população de 258, ficam 64 ou 65 de cada.
  - `Repovoar…` devolve todos e cria de novo, 6 por quadro. `Zerar()` limpa estado, agente, voz, Animator e vida. O ponto de nascimento é validado no NavMesh, na mesma altura do ponto.
- **Rede (`ZombieManager.Rede.cs`):** os zumbis não são NetworkObjects.
  - O anfitrião manda a cada cliente uma mensagem nomeada (`ZCC.Zumbis`, não confiável e em ordem) só com os zumbis perto dele, com a mesma histerese do LOD:
    - até 15 m, 10 vezes por segundo;
    - de 15 a 40 m, a cada 3 envios (em rodízio pelo Id);
    - mais longe, não manda.
  - Cada zumbi ocupa 14 bytes:
    - Id, geração, posição em cm, giro e velocidade;
    - o estado (4 bits), com as contagens de golpes e de tiros levados (2 bits cada);
    - como ele morreu (`InfoDaMorte`): o bit 0 diz se a cabeça explodiu, os bits 1 a 4, a parte do corpo que levou o tiro, e o bit 5, se o corpo foi arremessado (a espingarda de perto). Morto, o byte da velocidade traz a direção do tiro (em 256 avos de volta);
    - os membros perdidos (`Cortes`, 2 bits por membro).
  - São até 80 zumbis por mensagem (~1,1 KB, abaixo do limite de 1.300 bytes).
  - No cliente, cada Id vira uma cópia (do mesmo pool), mostrada um pouco no passado (~0,2 s perto, ~0,5 s no médio) e interpolada. Ela some depois de 1,5 s sem notícias. A geração muda quando o Id volta do pool, e aí a cópia é trocada.
  - Não há RPC de evento: o estado inteiro vai em todos os pacotes, e a cópia transforma o que mudou em efeito.
    - A morte some com o zumbi e derruba o corpo dele, com o mesmo empurrão (e a cabeça explodindo, se foi na cabeça).
    - Uma contagem de golpes nova toca o ataque, e uma de tiros nova toca a reação. O gemido toca quando o estado vira Perseguindo.
    - Na primeira vez que a cópia aparece, ela só mostra o zumbi como está, sem efeito.
  - As hitboxes da cópia seguem a distância de quem joga ali.
- **Na tela:** F3 mostra os zumbis por nível e a rede (KB/s) no anfitrião, ou as cópias no cliente.
- **Benchmark (`BenchmarkDeZumbis`):**
  - F9 no anfitrião, ou `-hospedar-local -benchmark -resultado caminho.md` na build (espera um cliente por até 25 s e fecha no fim).
  - Troca a população por 50, 100, 250 e 500 zumbis espalhados e mede cada quantidade por 10 s, sem vsync e sem dano.
  - Mede o quadro (média e p95), a thread principal, a GPU, a IA + LOD, a rede, `Animators.Update`, `NavMeshManager`, `Physics.Simulate`, os draw calls, os batches, os SetPass, os triângulos e os KB/s por cliente.
    - Os marcadores só funcionam no Editor e em builds Development.
    - O tempo da GPU vem do `FrameTimingManager` e precisa de "Frame Timing Stats" ligado nas Player Settings (ligado na Parte 8). Sem ele, a coluna sai "—".
  - Cada rodada entra como uma seção nova no fim de `Benchmarks/Benchmark_Zumbis.md` (no Editor), com a data, o rótulo (`BenchmarkDeZumbis.Rotulo`) e a posição da câmera. O histórico fica para comparar. Depois as hordas voltam.
  - Para comparar duas rodadas, use a mesma vista: a câmera muda muito os draw calls e os triângulos.
  - A coluna "Batches" sai 0 no Editor (o contador não responde lá).
  - **Números da Parte 8** (Editor, à noite, do cruzamento do meio olhando para o prédio):
    - a cidade de arte deu 940 a 1.100 draw calls, contra ~2.460 da cidade em blocos;
    - o quadro ficou igual, 4,3 a 6,2 ms (preso na CPU e no Editor); a GPU, 2,4 a 3,0 ms;
    - os triângulos vão de ~0,5 milhão (50 zumbis) a ~1,3 milhão (500): quase tudo são zumbis.
    - A oclusão cortou 4 a 5% dos draw calls e 8% dos triângulos dessa vista aberta; ela rende mais dentro das casas e atrás dos prédios.
  - **Com os 4 modelos e o ragdoll** (mesma vista, às 23 h): 617 a 695 draw calls, quadro de 3,7 a 4,9 ms, GPU 2,1 a 2,5 ms, 265 a 784 mil triângulos. Não piorou; a queda em relação à Parte 8 pode ser em parte da hora da noite (os postes acesos mudam as sombras), então não é uma comparação limpa.

## Combate e morte (Parte 6, refeita nos ajustes antes da Parte 7)

- **O acerto:** quem atira decide se acertou (na própria máquina), e o anfitrião confirma e aplica (`ZombieManager.Acertar`).
  - A conferência é tolerante, porque é co-op: o zumbi tem que existir com o mesmo Id e a mesma geração, estar vivo e estar a até 3,5 m do ponto do acerto. O dano fica entre 0 e 10.
  - A direção do tiro (do olho de quem atirou até o ponto) vai junto: é ela que empurra o corpo, se o zumbi cair.
- **Vida, em tiros de pistola** (dano 1 por tiro; números em `AjustesDeZumbi`; o dano de cada arma está na tabela de "O jogador e as armas"):
  - cabeça: um tiro explode a cabeça e mata;
  - corpo 6: cada tiro no tronco (ou na cápsula do nível médio) tira o dano dele; nos braços e nas pernas, metade (`danoNosMembros`);
  - cada braço e cada perna 3 (`vidaDoMembro`): acabou, o membro cai, e o que sobrou dele ganha vida nova;
  - cada tiro que não mata toca a reação (`HitReact`) e põe o zumbi atrás de quem atirou.
- **O tiro atravessa até dois braços** (`ArmaJogador`): numa multidão, as mãos de quem ataca ficam na frente da mira. O braço leva o acerto e a bala segue (mas não conta duas vezes no mesmo braço).
- **Desmembramento** (`Zombies/Membros`, feito de novo nos 4 modelos novos, a pedido):
  - Cada membro tem 3 estados (2 bits no `Zumbi.Cortes`, que vai pela rede): inteiro, sem a parte de baixo (cortado no cotovelo ou no joelho) e sem o membro todo (no ombro ou no quadril). O tiro que acaba com a vida do membro corta no pedaço que pegou: no antebraço ou na canela, a parte de baixo; no braço ou na coxa, o membro todo.
  - **A malha cortada é feita na hora** (`Membros.Variante`), a partir da `<modelo>_Inteira` (legível; a montagem marca): saem os triângulos presos sobretudo aos ossos perdidos, e cada buraco ganha uma tampa de carne (um leque até o meio da borda, um pouco para dentro, com o material `Zombie_Flesh`, no submesh 1). Fica guardada por (malha, cortes).
  - **O pedaço que cai** (`Membros.Pedaco`) é feito do mesmo jeito, com os triângulos que saíram e as tampas dele, no espaço do osso do corte; ele nasce onde o osso está e voa para trás (`ZombieManager.Pedacos.cs`: um pool de 24, na camada dos corpos, que param, afundam e voltam ao pool como os corpos). O toco jorra sangue (`Sangue.Jorro`, 2,5 s) e mancha o chão.
  - **Custo:** cortar a malha leva de 3 a 15 ms e o pedaço de 6 a 20 ms, na primeira vez de cada corte. Por isso a partida deixa prontos, um por quadro no começo (`PreaquecerCortes`), os cortes de um membro só (nos dois pontos), as duas pernas e os pedaços. Depois, trocar a malha custa ~0,005 ms.
  - As hitboxes do que caiu desligam. **Sem uma perna, ele manca** (55% da velocidade, `fatorMancando`); **sem as duas, cai e rasteja** (35%, `fatorRastejando`), com a cápsula e o agente baixos (0,7 m) e sem as animações de ataque e de reação por cima (a camada "Ações" fica com peso 0; o golpe ainda tira vida). Sem os braços, ele continua mordendo.
  - O corpo que cai sai cortado (a malha cortada, com ou sem cabeça) e as partes perdidas não colidem.
  - **Custo medido** (2 rodadas, mesma vista, noite, contra a ambientação): GPU de 8,4 a 9,2 ms (antes 8,5 a 9,4), draw calls e SetPass na mesma faixa, IA, animação, navegação e física iguais.
- **Morte (ragdoll):**
  - o zumbi some (renderer, hitboxes, cápsula e Animator desligados), e um corpo do pool do modelo dele (`ZombieManager.Corpos.cs`, 12 por modelo, criados no começo da partida) toma o lugar: a mesma pose, osso a osso, e a velocidade que ele tinha;
  - cada parte com física recebe a pose também no Rigidbody (`position` e `rotation`). Só mexer nos ossos não basta: a física não lê a pose nova sozinha, e as partes caíam de onde o corpo estava guardado no pool (o meio do mapa);
  - nessa troca, a interpolação das partes fica desligada até um passo de física rodar na pose nova (o `CorpoDeZumbi` só fica ligado para isso). Ligada, ela desenhava o corpo entre a pose antiga e a nova por uns 4 quadros: um clarão do corpo sendo puxado pela rua (aparecia quando um corpo ainda caindo era reaproveitado em outro lugar);
  - a parte do corpo mais perto do ponto do tiro leva um empurrão de 30 N·s na direção do tiro (um pouco para cima). Na cabeça, o corpo cai sem ela (a malha `_SemCabeca`), a cabeça explode (`Sangue.Explosao`) e o pescoço jorra por 4 s;
  - com todos os corpos daquele modelo em uso, o mais antigo que está fora da tela some e é reaproveitado (se todos estão na tela, o mais antigo);
  - depois de 6 s a física para (o corpo fica cinemático, parado no chão); aos 15 s (`tempoDoCorpo`) ele afunda 0,6 m em 2,5 s e volta ao pool;
  - o zumbi morto (invisível) fica na lista o mesmo tempo, para a morte chegar aos clientes que passarem perto, e depois volta ao pool. Zumbis mortos não renascem.
  - Cada máquina tem os seus corpos (são só visuais): caem para o mesmo lado em todas, mas podem parar em lugares um pouco diferentes.
  - **Custo medido:** 20 zumbis mortos no mesmo quadro deram um quadro de 19 ms (as 220 partes começando a cair, mais o sangue); os seguintes ficaram em 5 a 8 ms até os corpos assentarem. Uma morte sozinha fica abaixo de 1 ms.
- **Efeitos (`Combat/Sangue`, no "Efeitos"):** tudo vem de pools, criados no começo.
  - respingo de gotas no acerto (as gotas param no chão);
  - explosão da cabeça: muito sangue, pedacinhos e 4 manchas em volta;
  - o jorro do pescoço, preso ao osso do corpo que cai;
  - manchas de sangue no chão (40 decals) e furos de tiro no cenário (40 decals), reaproveitando o mais antigo.
- **Na rede:** o estado do zumbi (a morte com a parte e a direção do tiro, as contagens de golpes e de tiros) vai em todos os pacotes de zumbis. Cada máquina transforma o que mudou em efeito, então todos veem o mesmo zumbi cair, para o mesmo lado.
