# A perua nova (visual refeito do carro que se dirige) — 28/10/2026

O carro da família ganhou um visual novo, **só visual**: a mesma perua dos anos 90 (as mesmas medidas, silhueta, peças,
origens e pontos), com mais detalhe e materiais PBR assados. Inspiração (não cópia) no Ford Country Squire das fotos do
usuário: painéis laterais imitando madeira com moldura bege, bagageiro de teto cromado, 4 faróis quadrados com bisel
cromado, grade e para-choques cromados com borracha, calotas, lanternas altas; por dentro, vinil azul-turquesa com
gomos e painel horizontal com friso cromado. A cor ficou **mostarda desbotado** (para continuar reconhecível como a
perua do jogo); mude `COR_PINTURA` no `perua_nova.py` para `(214, 197, 160)` se quiser o creme da referência.

Nenhuma mecânica, física, rede, gatilho, lugar, câmera, luz ou som mudou: o prefab só teve trocadas a malha e os
materiais das 12 peças visuais, e os dois índices dos submeshes das lentes no `Carro`.

## Capturas (em `capturas/`)

| | Antes | Depois |
|---|---|---|
| 3/4 frente, dia | `antes_dia_34_frente.png` | `depois_dia_34_frente.png` |
| 3/4 trás, dia | `antes_dia_34_tras.png` | `depois_dia_34_tras.png` |
| Lado | `antes_dia_lado.png` | `depois_dia_lado.png` |
| Olho do motorista | `antes_dia_motorista.png` | `depois_dia_motorista.png` |
| Interior (banco de trás) | `antes_dia_interior_tras.png` | `depois_dia_interior_tras.png` |
| Capô (de cima) | `antes_dia_capo.png` | `depois_dia_capo.png` |
| Noite, faróis e lanternas acesos | `antes_noite_34_*.png` | `depois_noite_34_*.png` |
| Detalhes | — | `depois_dia_farol_perto`, `_roda_perto`, `_madeira_perto` |
| Portas, capô e tampa abertos | — | `depois_abertos_esq_frente`, `_dir_tras`, `_de_cima`, `depois_porta_motorista_aberta` |
| No jogo (Play, Demo_Cidade, neblina) | — | `jogo_capo_aberto`, `jogo_primeira_pessoa_motorista`, `jogo_ignicao_chave`, `jogo_dirigindo`, `jogo_porta_malas_aberto`, `jogo_macaco_erguido`, `jogo_banco_de_tras` |
| Blender (o modelo antigo, `carro.py`) | `antes_blender_34_*.png` | — |

(Nas fotos `jogo_*`, os braços que aparecem soltos são os braços de primeira pessoa do jogador, que a câmera de teste
também desenha; no jogo eles só aparecem na câmera dos braços.)

## Arquivos

**Criados**
- `Blender/PeruaNova/perua_nova.py` — o modelo novo (lê as medidas do `carro.py`, que não mudou): `construir()`,
  `desembrulhar()`, `assar()`, `compor()`, `exportar()`, `salvar()`.
- `Blender/PeruaNova/PeruaNova.blend` — o arquivo editável (as peças com os materiais de autoria procedurais e as
  imagens assadas empacotadas; também tem a cena "Carro" do modelo antigo, para comparar).
- `Assets/_Game/Art/Veiculos/PeruaNova/PeruaNova.fbx` — 9 peças: `Perua_Carroceria`, `Perua_Porta_DE/DD/TE/TD`,
  `Perua_Roda`, `Perua_Volante`, `Perua_Capo`, `Perua_Tampa`.
- `Assets/_Game/Art/Veiculos/PeruaNova/Texturas/` — 2 atlas de 2048: `Perua_Lataria_*` e `Perua_Interior_*`, cada um
  com `_Cor` (sRGB), `_MS` (R = metal, G = oclusão, A = suavidade) e `_Normal`.
- `Assets/_Game/Art/Veiculos/PeruaNova/Materiais/` — 5 materiais URP Lit: `Perua_Lataria`, `Perua_Interior`,
  `Perua_Vidro` (transparente, sem sombra, como o Vidro.mat antigo), `Perua_Farol` (lente transparente: apagada,
  vê-se o refletor cromado; acesa, a Unity troca pelo `FarolAceso`), `Perua_Lanterna` (acesa: `LanternaAcesa`).
- `Assets/_Game/Scripts/Editor/PeruaNova/VisualDaPerua.cs` — menus **Zombie City Cleaner > Veículos > Aplicar o visual
  novo da perua** e **Voltar ao visual antigo da perua**. Troca só malha e materiais; tira uma "impressão" de todos os
  componentes e campos serializados do prefab (menos malha/materiais e os 2 índices das lentes) antes e depois, e
  **não salva** se algo diferir.
- `Backups/Perua_antes_do_visual_novo_2026-10-28/` (fora de `Assets`, a Unity não importa) — o `Carro.prefab` (+meta),
  o `Carro.fbx` (+meta), os materiais dos veículos, o `carro.py`, `MontagemDosVeiculos.cs`, `Carro.cs`,
  `ObjetosDoMundo.cs` e as 3 cenas, como estavam antes.
- `Docs/PeruaNova/` — este relatório, as capturas e os retratos do prefab (`prefab_fisica_antes/depois.txt`,
  `prefab_hierarquia_antes/depois.txt`, `prefab_impressao_completa_depois.txt`).

**Modificados**
- `Assets/_Game/Prefabs/Veiculos/Carro.prefab` — a malha e os materiais de Carroceria, Capô, Tampa traseira, Volante,
  Porta DE/DD/TE/TD e Roda DE/DD/TE/TD (modelo); `Carro.submeshFarol` 2 → 3 e `submeshLanterna` 3 → 4.
- `CLAUDE.md` (seção "O carro") e `Blender/README.md` — como refazer o visual.

**Não tocados:** `carro.py`, `Carro.fbx`, `MontagemDosVeiculos.cs`, `Carro.cs`, `PneusDoCarro.cs`, `PecasDoMotor.cs`,
`SomDoCarro.cs`, `PassageiroDoCarro.cs`, cenas, kits e scripts dos carros abandonados. Nada novo vai pela rede.

## Como refazer

1. Blender 5.2: `import sys; sys.path.append(r"<repo>\Blender\PeruaNova"); import perua_nova` e
   `construir()`, `desembrulhar()`, `assar()` (um canal por vez se a conexão cair: ~1 min cada na CPU),
   `compor()`, `exportar()`, `salvar()`. (Só mudou material? Pule `construir`/`desembrulhar` e chame
   `perua_nova._material_de_autoria(...)` para cada superfície antes de `assar`.)
2. Unity: **Zombie City Cleaner > Veículos > Aplicar o visual novo da perua**.
3. **Atenção:** **Montar veículos** refaz o prefab inteiro com o `Carro.fbx` antigo. Depois dela, rode o passo 2 de novo.

## Valores físicos (antes = depois)

Comparados pelos retratos do prefab (`diff prefab_fisica_antes.txt prefab_fisica_depois.txt`): a única diferença são os
índices das lentes (`submeshFarol` 2 → 3, `submeshLanterna` 3 → 4). Transforms, camadas e componentes de todos os
objetos: idênticos (`prefab_hierarquia_*`, sem as colunas de malha/material).

| | Valor (igual antes e depois) |
|---|---|
| Rigidbody | 1.150 kg; arrasto 0,03; angular 0,4; Interpolate; ContinuousSpeculative; centro de massa (0; 0,38; 0,12) (no `Carro.Awake`) |
| WheelCollider ×4 | (±0,735; 0,39; ±1,36); raio 0,30; massa 20; amortecimento 0,4; suspensão 0,18; ponto de força 0,12; mola 32.000; amortecedor 3.800; alvo 0,5 |
| Atrito | frente: 0,4/1/0,8/0,5, rigidez 1,6; lado: 0,25/1/0,5/0,75, rigidez 2 |
| Carro | torque 700; v. máx. 22 m/s; ré 7; freio 2.400; freio de mão 4.500; giro 32°/12°; volante 110; anticapotar 7.000 |
| Colisão | Baixo 1,72×0,70×4,36 @ y 0,58; Cabine 1,60×0,52×2,30 @ (0; 1,19; −0,82); Para-brisa 1,58×0,10×0,85 a 33,9° |
| Gatilhos, lugares, saídas, faróis, luzes, áudio | idênticos (ver os retratos) |

A roda visual nova tem 0,600 m de diâmetro (raio 0,30, o do WheelCollider) e 0,21 m de largura (o cilindro do tiro,
`PneusDoCarro.MeiaLargura` 0,105), a origem no centro, o eixo em X e a calota para +X, como a antiga.

## Testes

**Feitos (Unity 6000.6.5, Editor):**
- Compilação sem erros; nenhum erro ou aviso novo no Console (nem no Play).
- Referências: nenhum material nulo no prefab; nenhuma cena sobrescreve malha/material da perua (Demo_Cidade e Video
  têm instâncias com só posição/rotação/hash sobrescritos; a Mundo cria o carro do prefab em tempo de jogo).
- Visão do motorista: raios do olho (quadril + 0,70 m, variando ±8 cm de altura e ±5 cm de lado) até a ignição, a
  chave, o velocímetro, a gasolina e a luz do farol: 0 de 75 tapados (o volante novo tapava a ignição com o olho mais
  alto; os raios dele foram endireitados).
- **Play (Demo_Cidade, partida local hospedada, 1 jogador):**
  - rodas visuais exatamente sobre as físicas (diferença 0,0000 m), parado e andando; a dianteira esquerda furada
    mais baixa (raio 0,215), como antes;
  - capô: abrir (62°, na dobradiça), pôr a vela pela tela do capô (`PedirPor`, 0 → 100%);
  - entrar no volante (`PedirEntrar`), a câmera no lugar (−0,37; 1,28; −0,02), pôr a chave, girar até a partida
    (arranque, painel aceso), pegar (motor ligado);
  - dirigir (tração na frente), faróis e lanternas acesos, freio de mão; sair;
  - porta-malas: abrir (78°), pegar o macaco;
  - macaco na roda dianteira esquerda, erguer (só aquele canto; a roda a ~22 cm do chão, como o registro antigo),
    tirar a roda (o modelo some, o WheelCollider vira o cubo), pôr de volta (raio 0,215), baixar, tirar o macaco;
  - tiro no pneu: o raio de lado acerta a roda certa na face do pneu novo;
  - lugar de trás (3): entrar, câmera, sair.
- Portas, capô e tampa abertos nos ângulos do jogo (fotos fora do Play): nada atravessa a carroceria.

**Não testados (e por quê):**
- **Multijogador com 2+ jogadores:** não deu para orquestrar o Multiplayer Play Mode por aqui. Risco baixo: nenhum
  componente de rede, NetworkVariable, RPC ou hash mudou (a impressão completa do prefab é igual).
- **Mini-game da chave com o mouse** (o anel e o ponteiro) e o teclado (WASD, B, L, R, V): o Editor estava sem foco, então
  os comandos foram dados direto pela API do carro (`PorAChave`, `GirarChave`, `PegarAgora`, `Comandar`). Para dirigir,
  o `PassageiroDoCarro` do jogador foi desligado por alguns segundos (ele zera os comandos sem o teclado).
- **Atropelamento:** não testado (a caixa de atropelamento e o código não dependem da malha).
- **Os lugares 1 e 2** (só o 0 e o 3 foram testados), o desvirar e a buzina.
- **A cena Mundo em Play** (só a Demo_Cidade; o prefab é o mesmo).

## Carros abandonados (pendente — decisão do usuário)

Os carros abandonados **não são a perua**: são os sedãs e picapes do `Cidade_Rua.fbx` (de outra pasta de arte), com o
shader de abandono do mundo (`Abandono*.mat`) e os kits de capô/rodas/macaco recortados da malha deles
(`MontagemDoCofre.Recortar`). Por isso o visual novo **não foi aplicado** neles. Caminhos possíveis:
1. **Perua abandonada como 8º modelo do gerador** (a malha nova, com mais ferrugem e danos): exige um kit novo e mexe
   em `ModelosDaEstrada.NomesDosCarros`, o que **muda o sorteio de todos os mapas** (cada semente passa a pôr outros
   modelos em cada lugar).
2. **Variações nos carros que já existem** (tom da pintura, mais ferrugem, vidros quebrados) por carro, a partir da
   semente: mexe em `ObjetosDoMundo.CriarComKit` (arquivo com mudanças não commitadas de outra sessão) e no material
   do abandono.
3. Deixar como está.

## Observações

- `Backups/` está fora do `.gitignore` (aparece como não rastreado). Decida se entra no git.
- Memória de textura: 6 texturas de 2048 (~30 MB comprimidas, com mipmaps). Para baixar, ponha `maxTextureSize` 1024 no
  `VisualDaPerua.Textura` (a madeira perde definição de perto).
- Notado de passagem (não mexido): sem a roda, o WheelCollider fica com raio 0,07 no Play; o `CLAUDE.md` fala em 0,13. O
  `PneusDoCarro.cs` já estava modificado (não commitado) antes deste trabalho.
