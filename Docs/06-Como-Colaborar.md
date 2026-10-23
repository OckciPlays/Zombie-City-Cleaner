# 06 – Como colaborar

## Git

- **Branches:**
  - `feature/demo-cidade`: a demo, onde o trabalho acontece;
  - `main`: o check-in inicial;
  - `feature/prototipo-fps-zumbis`: o protótipo antigo (PS1 e elevador), só como referência.
- **Git LFS:** os arquivos grandes (FBX, imagens e sons) vão pelo LFS, conforme o `.gitattributes`. Os assets da Unity em texto (cenas, prefabs, malhas geradas `.asset`) vão como texto normal. Rode `git lfs install` uma vez antes de clonar.
- **Ignorados:** `Library/`, `Temp/`, `Logs/`, `UserSettings/` e `Builds/` (a Unity recria tudo isso).
- **Sugestão de fluxo para duas pessoas:**
  - cada um trabalha numa branch própria a partir da `feature/demo-cidade` (por exemplo, `feature/saque`) e abre um pull request para ela;
  - **evitem mexer os dois na cena `Demo_Cidade.unity` ao mesmo tempo:** juntar cenas da Unity no git dá conflito difícil. Como a cena é quase toda gerada pelos scripts de montagem, prefira mudar o script e rodar a montagem de novo; se os dois mudarem a cena, quem juntar por último roda as montagens de novo;
  - o mesmo vale para os prefabs `Jogador.prefab` e `Zumbis/`, que também são gerados.
- **Mensagens de commit:** em português, com um título curto e uma lista do que mudou (veja o `git log`). Os commits grandes seguem as partes do plano ("Parte 8: …").

## Como o trabalho anda

- O jogo segue o [plano da demo](02-Plano-da-Demo.md), parte por parte. Uma parte só começa quando a anterior foi aprovada pelo autor.
- Partes grandes são divididas em fatias, e cada fatia é testada e aprovada antes da próxima.
- Ao terminar algo, atualize esta documentação ([histórico](03-Historico.md) e, se for o caso, o [plano](02-Plano-da-Demo.md)) e o [`CLAUDE.md`](../CLAUDE.md), que é a referência técnica detalhada.

## Regras do projeto

- **Nada de "o jogador" único nem de `Camera.main` na lógica do jogo:** é online. A IA, o saque e o dia sempre pensam na lista de jogadores (`JogadorRede.Todos`). Quem decide o mundo é o anfitrião (`IsServer`). O `Camera.main` (a câmera do jogador local) só serve para o que é visual e local, como o som abafado pela distância, as partículas em volta da câmera e o desenho dos zumbis longe.
- **O que é gerado pelos scripts de montagem não se edita à mão:** muda-se o script e roda-se de novo (menu Zombie City Cleaner). Isso vale para a cidade, o clima, os zumbis, o jogador, as armas, o NavMesh e a oclusão.
- **Play sem Domain Reload e sem Scene Reload:**
  - estado `static` sobrevive entre um Play e outro: inscreva e desinscreva eventos estáticos em `OnEnable`/`OnDisable`;
  - scripts `[ExecuteAlways]` não recebem `Awake` de novo ao dar Play;
  - para tempos guardados em `static`, use `Time.realtimeSinceStartup`.
- **Não recompile os scripts com o Play rodando:** os campos que não são salvos voltam a zero e tudo quebra. Saia do Play antes.
- **Escala zero é proibida:** uma peça com escala 0 em algum eixo gera normais infinitas, e o bloom espalha um clarão branco na tela.
- **Write Defaults desligado em todos os estados dos Animators:** ligado, o estado vazio de uma camada de cima joga o personagem na pose T.
- **Depois de gerar uma build,** a Unity coloca o `InputSystem_Actions` nos Preloaded Assets do Player Settings. Tire de lá antes de commitar (o `ProjectSettings.asset` não deve mudar por causa disso).
- **Novas armas:** em `Combat/Armas.cs`, o `TipoDeArma`, a linha da tabela e o nome do modelo (`Armas.Modelos`); no Blender, o modelo e a pose (`armas.py`), as animações de primeira pessoa (`armas_anim.py`) e, se for arma longa, as do corpo (`jogador_tp.py`); depois, o export e a montagem das armas.

## Como testar

- **Sozinho, no Editor:** abra `Demo_Cidade`, dê Play e escolha "Hospedar local".
- **Vários jogadores no Editor:** o Multiplayer Play Mode (menu Window > Multiplayer) abre jogadores virtuais no mesmo Editor.
- **Com uma build e um robô** (testa a rede de verdade):
  1. gere uma build Development em `Builds/Teste/` (a pasta é ignorada pelo git);
  2. no Editor, dê Play e hospede (com código, ou "Hospedar local");
  3. rode a build sem janela:

     ```bash
     ZombieCityCleaner.exe -batchmode -nographics -entrar-local 127.0.0.1 -bot -logFile robo.log
     ```

     (ou `-entrar CÓDIGO` para entrar pelo Relay). O robô anda em círculo, atira a cada 1,5 s, mira no zumbi mais perto e escreve no log, a cada 3 s, o que vê;
  4. confira no Editor (os jogadores em `JogadorRede.Todos`, os tiros recebidos) e no log da build;
  5. ao terminar, feche a build e tire o `InputSystem_Actions` dos Preloaded Assets.
- **Com um amigo de verdade:** um hospeda pelo menu ("Hospedar partida"), o outro digita o código e clica em "Entrar".
- **Desempenho:** F9 no anfitrião roda o benchmark dos zumbis e grava o resultado em `Benchmarks/Benchmark_Zumbis.md`. Para comparar duas rodadas, use a mesma vista e a mesma hora do dia. F3 mostra os zumbis por nível e a rede.

## Checklist antes do commit

- [ ] O projeto compila sem erros e o Play funciona (hospedar local, andar, atirar).
- [ ] Se mudou algo de rede, testou com a build e o robô (ou com o Multiplayer Play Mode).
- [ ] Se mudou um script de montagem, rodou a montagem e a cena foi salva.
- [ ] O `ProjectSettings.asset` não ficou com o `InputSystem_Actions` nos Preloaded Assets.
- [ ] Nada de `Builds/`, `Library/` ou arquivos temporários no commit.
- [ ] A documentação (`Docs/` e `CLAUDE.md`) foi atualizada com o que mudou.
