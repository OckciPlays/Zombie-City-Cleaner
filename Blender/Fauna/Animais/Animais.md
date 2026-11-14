# Os animais do Tripo: a orientação, o rig e as animações (14/11/2026)

O pedido (com o usuário dormindo, para revisar depois): descompactar os modelos do Tripo de `Animais 3d/`, abrir todos no
Blender, orientar, fazer o rig e as animações, pelo documento do Pedro no develop
(`Docs/20-Fauna-Modelos-e-Animacoes.md`). **Nada foi levado para a Unity.**

## O que tem

- **`Animais.blend`** (aqui): os 24 bichos, cada um na coleção dele (dentro da coleção `Animais`), em fileiras
  (os canídeos, os cervídeos, os ursos, os pequenos e o sapo, as aves). Cada um tem a malha `<Nome>`, o esqueleto
  `<Nome>_Esqueleto` e as ações `<Nome>_<Ação>` (uma faixa de NLA muda por ação, como no jogador e nos zumbis; a ação
  ativa é a de parado). Para ver uma ação: selecione o esqueleto, troque a ação no Action Editor (Dope Sheet) e dê play
  (o intervalo da ação vem marcado nela, "Manual Frame Range"; a cena vai de 1 a 96).
- **`Renders/Acoes/<Nome>_<n>.png`**: as folhas de conferência, cinco ações por folha, na ordem das faixas de NLA, cada
  uma com 5 quadros espalhados (em cima, de 3/4 pela frente; embaixo, de lado).
- **`Renders/RaioX/<FBX>.png`**: o raio-X de cada modelo do Tripo com a grade (de onde saíram os pontos das juntas).
- **`Texturas/<Nome>_Cor.jpg`**: as texturas do Tripo, copiadas com o nome do bicho.
- Os modelos descompactados ficaram em `Animais 3d/<FBX>/` (o `.fbx` e o `.fbm`); os `.zip` continuam lá.

## A orientação e o tamanho

- Os FBX do Tripo já vinham olhando para -Y com os pés no chão (normalizados em 1 m na maior medida). A importação aplica
  o giro e a escala do FBX, solda os vértices das costuras do UV e põe no **tamanho de verdade** da tabela do Docs/20
  (a altura da cernelha; nas aves e no sapo, a escala direta):

  | Bicho | Escala | | Bicho | Escala |
  |---|---|---|---|---|
  | Lobo_Macho (0,80 m) | 1,416 | | UrsoNegro_Macho (0,95) | 1,484 |
  | Lobo_Femea (0,72) | 1,358 | | UrsoNegro_Femea (0,85) | 1,518 |
  | Lobo_Filhote (0,42) | 0,785 | | UrsoNegro_Filhote (0,48) | 0,835 |
  | Coiote (0,58) | 1,137 | | UrsoPardo_Macho (1,15) | 1,846 |
  | Cervo_Macho (0,95) | 1,624 | | UrsoPardo_Femea (1,02) | 1,709 |
  | Cervo_Femea (0,86) | 1,250 | | UrsoPardo_Filhote (0,58) | 0,978 |
  | Alce_Macho (1,80) | 2,466 | | Guaxinim (0,30) | 0,682 |
  | Alce_Femea (1,62) | 2,282 | | Gamba (0,25) | 0,641 |
  | Alce_Filhote (1,00) | 1,325 | | Castor (0,30) | 0,870 |
  | Aguia (2,1 m de envergadura) | 2,1 | | Coelho (0,42 m de comprimento) | 0,42 |
  | Urubu (1,75 m de envergadura) | 1,75 | | Pato | 0,45 |
  | Peru | 0,9 | | Sapo (12 cm) | 0,12 |

- **Na convenção dos cervos do Pedro:** o bicho olha para **-Y** (vira +Z na Unity com o preset de exportação dele), o
  alto em +Z, em metros, o pivô no chão entre as patas (o meio do pé da frente e do de trás) e o lado L em +X. (O Docs/20
  diz "o focinho para +X" no `cervo.py`, mas lá o `P(f, s, z) = (s, -f, z)` põe a frente em -Y: é a mesma orientação.)

## O rig

- **Os ossos com os nomes do Docs/20** (os que a animação procedural do jogo mexe): `Quadril`, `Pescoco_1`,
  `Pescoco_2`, `Cabeca`, `Braco`/`Antebraco` (a pata da frente), `Coxa`/`Perna` (a de trás), `Cauda_1`, `Cauda_2`...,
  `Orelha.L/R` e, nas aves, `Asa.L/R` e `Asa_2.L/R` (e `Asa_3`, a mão). Mais os que eles têm de extra: `Raiz` (não
  deforma), `Pelve`, `Coluna_1/2`, `Peito`, `Omoplata`, `Canela.D`/`Pata.D` (o pulso e a mão), `Canela.T`/`Pata.T` (o
  jarrete e o pé), `Mandibula` (a boca abre), `Galhada.L/R` (nos machos com galhada, presa à cabeça) e `Papo` (o peru e o
  sapo: o papo do sapo incha pela escala). O roll é o dos cervos (os ossos em pé com o Z para a frente, os deitados com o
  Z para cima). O sapo não tem pescoço (o Peito vai direto à Cabeca).
- **As juntas** foram lidas no raio-X com a grade (o `raio_x.py`), uma lista de pontos por bicho (`animais_dados*.py`),
  e conferidas com os ossos desenhados por cima.
- **Os pesos:** os automáticos do Blender (o calor dos ossos), limpos por regras (`ajustar_pesos`): cada lado só pega os
  ossos do lado dele, os ossos das patas só perto delas e nunca no meio do peito ou da barriga (o peito entre as patas da
  frente estava com 25% de cada braço e esticava ao andar), as orelhas só perto delas (a galhada não mexe com a orelha),
  a mandíbula só abaixo da linha da boca; depois uma suavização e no máximo 4 ossos por vértice.

## As animações

Feitas por código (o motor de poses do `animais_kit.py` e do `animais_poses.py`): cada pose é o giro de cada osso no
espaço do bicho; **as patas são por IK** (os dois ossos de cima até o pulso/jarrete, a canela e a pata pela fase do
passo), então os pés não escorregam nem entram no chão; uma chave por quadro, lineares, a 24 fps, **no lugar** (o jogo
move o bicho). Os laços repetem o primeiro quadro no último. As poses deitadas e caídas acham a altura do corpo pela
malha deformada (o ponto mais baixo no chão).

O passo: o andar (a sequência lateral), o trote (as diagonais) e o galope (o rotativo nos canídeos e nos ursos, o
transverso nos cervídeos), com o período pelo comprimento da perna e a velocidade do Docs/20 (o pé no chão recua na
velocidade), o corpo subindo e descendo, o balanço, a coluna dobrando no galope, a omoplata acompanhando, a cabeça firme e
a cauda.

| Grupo | As ações |
|---|---|
| **Lobo, coiote** (20) | Parado, Andar, Trotar, Correr, Espreitar (agachado, devagar), Alerta, Rosnar, Morder (o bote), Comer (na carcaça, puxando), Uivar (sentado), Sentar, Sentado, Levantar_Sentado, Deitar, Deitado, Dormir, Levantar, Nadar, Morrer, Morto |
| **Veado** (16 o macho, 15 a fêmea) | Parado, Andar, Trotar, Correr (com a cauda levantada, a "bandeira"), Comer (pastando), Alerta (também o "parado no farol"), Bufar (o sopro e a pata batendo), Cabecada (só o macho), Pisotear, Deitar, Deitado, Dormir, Levantar, Nadar, Morrer, Morto |
| **Alce** (19 o macho, 18 a fêmea e o filhote) | as do veado, mais Bravo (as orelhas para trás), Mugir e Comer_Agua (a cabeça mergulha fundo e sobe mastigando) |
| **Ursos** (24; 25 os filhotes) | Parado, Andar (fuçando), Trotar, Correr, Comer, Fucar, Alerta, Ficar_De_Pe, De_Pe (o aviso, batendo os dentes), Descer, Patada, Pescar (de pé no raso olhando a água), Patada_Agua, Comer_Sentado (com as patas na boca), Sentar, Sentado, Levantar_Sentado, Deitar, Deitado, Dormir, Levantar, Nadar, Morrer, Morto; os filhotes, Chorar |
| **Guaxinim** (20) | Parado, Andar, Trotar, Correr, Ficar_De_Pe, Alerta (de pé), Revirar (o lixo, de pé, as mãos mexendo), Subir (no tronco), Agarrado (no tronco), Comer_Sentado, e as de chão (sentar, deitar, dormir, nadar, morrer) |
| **Gambá** (19) | Parado, Andar, Trotar, Correr, Comer, Aviso (a cauda em pé e as patas batendo), Espirrar (o corpo em U), Chiar e Fingir_Morto (o do gambá-de-orelha-branca), e as de chão |
| **Castor** (18) | Parado, Andar, Correr, Andar_Com_Galho, Nadar (as patas de trás remando e a cauda), Mergulhar, Bater_Cauda, Roer (de pé no tronco), Comer_Sentado, e as de chão (sem o nadar comum) |
| **Coelho** (11) | Parado (o nariz e as orelhas), Pular, Correr (os pulos rápidos), Comer, Alerta (de pé), Guincho (pego, se debatendo), Deitar, Deitado, Levantar, Morrer, Morto |
| **Pato** (14) | Parado, Andar (bamboleando), Comer, Grasnar, Voar, Planar, Decolar, Pousar, Boiar, Nadar, Mergulhar (a cabeça na água, o rabo para cima), Dormir, Morrer, Morto |
| **Peru** (15) | Parado, Andar, Correr, Comer, Glu_Glu (o papo balançando), Voar, Planar, Decolar, Pousar, Ciscar, Exibir (estufado), Voo_Curto, Empoleirado (dormindo), Morrer, Morto |
| **Águia** (14) | Pousado, Andar, Comer (rasgando), Grito, Voar, Planar, Decolar, Pousar, Mergulho, Agarrar (as garras), Voar_Com_Peixe, Dormir, Morrer, Morto |
| **Urubu** (12) | Pousado, Andar, Comer (bicando), Grito, Voar, Planar (balançando), Decolar, Pousar, Asas_Abertas (ao sol), Dormir, Morrer, Morto |
| **Sapo** (7) | Parado (o papo pulsando), Coaxar (o papo inchando), Pular, Lingua (o bote da boca), Nadar, Morrer (de barriga para cima), Morto |

## O que ficou aproximado (para a revisão)

- **As asas da águia e do urubu** vieram abertas no modelo: fechadas (pousado), são dobradas pelos ossos e ficam
  rentes ao corpo, mas não é uma asa fechada de verdade (as penas não se empilham). As do pato e do peru vieram fechadas:
  abertas no voo, parecem placas. Um modelo de cada (aberto e fechado) resolveria.
- **O "Gamba" é um gambá-de-orelha-branca** (o opossum, o rabo pelado), e o Docs/20 fala do gambá-listrado (o skunk). Fiz
  os dois: o aviso e o espirro do Docs/20 e o chiado e o fingir de morto do opossum.
- **A boca:** a malha do Tripo é fechada; a mandíbula abre esticando os lábios (sem o lado de dentro). Pequeno no
  morder, no uivo e no rosnar; bem visível no chiado do gambá.
- **As poses de chão** (sentar, deitar, dormir, morrer) seguem um molde por grupo; nos ursos e nos pequenos a barriga e
  as patas podem se encostar.
- **As sombras do Workbench** (nas fotos que não são estas) fazem riscos nas malhas do Tripo (abertas, com os olhos e as
  garras soltos): é a sombra, não a malha.
- Ainda não foi feito: o export para a Unity (o preset do Pedro está no Docs/20) e o mapeamento dos ossos extras no
  `ModelosDosBichos`.

## Refazer

Tudo é gerado por script, num Blender sem janela (o `blender.exe` da Steam):

```
blender -b --factory-startup --python animais.py -- <saida.blend> <Nome> [<Nome> ...]   (ou "todos"; --sem-acoes)
blender -b <arquivo.blend> --python fotos.py -- <pasta> [<Nome> ...] [--acoes A,B] [--quadros 5] [--px 200]
blender -b --factory-startup --python juntar.py -- Animais.blend <grupo1.blend> <grupo2.blend> ...
blender -b --factory-startup --python raio_x.py -- <FBX>            (o raio-X de um modelo, com a grade)
```

- `animais_kit.py`: a importação, as medidas, o esqueleto, os pesos (e a limpeza), o motor das poses (o FK e o IK das
  patas) e a gravação das ações.
- `animais_quadrupede.py` (o esqueleto e os pesos dos quadrúpedes), `animais_aves.py` (das aves e do sapo).
- `animais_dados.py` e `animais_dados_*.py`: os pontos de cada bicho (`bicho(nome, fbx, grupo, escala, pontos)`).
- `animais_poses.py` (o Bicho e o passo) e `animais_acoes.py` (as ações de cada grupo).
- `animais.py` (monta), `fotos.py` (as folhas), `juntar.py` (o arquivo de todos), `raio_x.py`, `vistas.py` e
  `folha.py` (as fotos de inspeção).
- Os testes de cada etapa ficaram em `Testes/` (pode apagar).
