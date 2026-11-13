# A pegada das armas de fogo na primeira pessoa: os braços de cada arma (Jogador_FP_<Arma>.fbx, 7 clipes) refeitos a
# partir do FBX ORIGINAL, com a mão nova (o pulso contínuo, os pesos suaves) segurando a arma como se segura de verdade.
#
# A HISTÓRIA
# - 11/11/2026, o pedido do Pedro: nas armas de fogo "o pulso parece quebrado". As animações são as de antes da mão nova:
#   o pulso dobrado 80 a 160 graus, a torção toda no pulso, os dedos abertos (a esquerda espalmada de lado no guarda-mão,
#   a direita atrás do punho, com os dedos esticados). Com a mão nova isso aparece.
# - A primeira tentativa (a primeira versão deste arquivo: uma busca automática da pegada, o cabo atravessando a palma, o
#   cotovelo escolhido quadro a quadro) foi REPROVADA pelo Pedro: "vamos precisar mudar a pegada de todas as armas, ainda
#   as antigas ficaram melhores". Ela foi julgada pelos números (os ângulos do pulso ficaram bons) e por fotos de perto,
#   e não pelo que o jogador vê. Na câmera do jogo ficou pior que o original: a mão de apoio foi parar embaixo/atrás da
#   arma e quase não aparecia (no rifle só um pedaço de polegar; na sniper uns dedos saindo por trás da coronha); o
#   antebraço saiu do caminho antigo na tela (mais baixo, quase deitado, meio fora da tela) e em algumas armas um braço
#   que aparecia sumiu; os dedos ficaram esticados ou abertos, apontando para a frente; na pistola as duas mãos viraram
#   um emaranhado (os dedos da esquerda na frente da arma, um polegar solto embaixo).
# - As referências do Pedro: prints de pegadas que existem na vida real e devem ser postas no jogo (primeira e terceira
#   pessoa): a pistola de duas mãos "polegares para a frente" (a esquerda abraçando os dedos da direita); o M4 com a
#   esquerda em concha por baixo e à esquerda do guarda-mão, o polegar deitado ao longo dele, e a direita no punho, o
#   indicador no gatilho; o rifle de madeira e a Mosin com a esquerda por baixo da madeira, o polegar ao longo, e a direita
#   no pescoço da coronha, o polegar dando a volta; a espingarda com a esquerda fechada na telha da bomba e a direita no
#   pescoço, o indicador no gatilho.
# - 11/11/2026, a segunda tentativa: um ajudante por arma, cada um com o seu motor (a pose das mãos escrita à mão no
#   espaço da arma, os dedos fechando até encostar, o braço posto para o antebraço entrar na tela por onde o original
#   entrava), julgando sempre pela câmera do jogo ao lado do original e das referências. As rodadas escolhidas: a
#   Pistola na 2, o Rifle na 2, a Sniper e o rifle de sucata na 5, a Espingarda na 3.
# - 12/11/2026: as quatro juntas neste arquivo, uma entrada por arma (ENTRADAS). Os quatro motores são caminhos
#   diferentes (cada arma pediu o seu) e ficam como os ajudantes os deixaram, no fim do arquivo (_MOTORES): misturar um
#   motor com o outro mudaria as poses aprovadas. Gerados por aqui, os FBX saem iguais aos das rodadas escolhidas (os
#   ossos dos braços e dos dedos a menos de 0,1 mm e 0,1 grau em todos os quadros). A melhoria que serve a todas foi
#   para o pegada_fogo_conferir.py (a pele dentro da arma medida peça por peça) e a conferência do original (abaixo).
# - 12/11/2026, depois: a Sniper e o rifle de sucata trocados pela rodada 5 (a rodada 4, com a mão direita rolada por
#   cima do pescoço e o cotovelo baixo ao lado do rosto, e a corrida consertada: nela a mão e o antebraço direitos
#   sumiam na borda de baixo; agora a pegada da corrida fica 3 cm mais à frente, girada no plano da palma, com o
#   cotovelo mais baixo). O motor passou a ser o fazer5.py (com o braco4.py), no lugar do fazer2.py da rodada 2.
# - A submetralhadora NÃO entra: ela voltou ao FBX original (a entrada dela está marcada como não usada).
#
# AS REGRAS DA PEGADA (o que a segunda tentativa aprendeu)
# 1. Julgar pela câmera do jogo (55 graus na vertical, 1280x720), lado a lado com o original e com as referências, em
#    todos os clipes; nunca só pelos ângulos do pulso nem por fotos de perto.
# 2. A mão de apoio aparece: por baixo e do lado de fora da arma, como na referência, nunca escondida embaixo ou atrás.
# 3. O antebraço entra na tela por onde o original entrava (a mesma borda, a mesma inclinação); nenhum braço que aparecia
#    some; o braço de cima e o ombro nunca aparecem (os ombros ficam fora da tela, mesmo deslocados).
# 4. Os dedos fechados em volta da arma, encostados (a pele no máximo ~1 mm dentro), juntos, sem nenhum esticado para a
#    frente; o polegar deitado ao longo da arma (não em pé); o indicador da direita no gatilho (ou reto, acima do
#    guarda-mato, quando a mão não alcança ou na recarga).
# 5. O pulso no guia: a dobra até ~55, o desvio até ~22, a torção até ~85 graus (o limite: 60, 25, 90), sem saltos (a
#    torção anda no máximo 30 graus por quadro).
# 6. A arma, os filhos dela e a Boca_FP não saem do lugar (0 mm em todos os quadros); os mesmos clipes, nomes, quadros,
#    ossos, malha e pesos do original, a 24 quadros por segundo.
# 7. Onde a mão sai da arma (a recarga, o ferrolho), ela segue o caminho do original, com os dedos curvos (sem abrir), e
#    volta para a pegada aos poucos.
#
# COMO RODAR (um Blender em segundo plano por arma; lê o FBX ORIGINAL e grava só a saída)
#   blender -b --factory-startup --disable-autoexec --python Blender/Armas/pegada_fogo.py -- <Arma> <saida.fbx>
#       [original=<pasta com os Jogador_FP_<Arma>.fbx originais>]
#   <Arma>: Pistola (os mesmos braços levam a pistola de sucata), Rifle, Sniper, Sucata_Rifle (parte do original da
#   sniper) ou Espingarda. Sem original=, o original é lido de Assets/_Game/Art/Characters/Jogador. O arquivo é
#   conferido pelo SHA-256 (ORIGINAIS_SHA256): depois de o FBX novo ir para Assets, passe original= com a pasta dos
#   originais (eles estão no histórico do git), senão o script para (a pegada aplicada duas vezes sai errada).
#   O Rifle e a Espingarda gravam também <saida>_params.json (as pegadas calculadas).
#   Opções do motor da pistola: segue=Clipe:fração,... e limitar=0|1; do rifle: so=, provar=, varios= (ver o motor).
# A conferência (os clipes, os ossos e a arma contra o original ou outro FBX; as medidas e as fotos):
#   blender -b ... --python Blender/Armas/pegada_fogo_conferir.py -- comparar <a.fbx> <b.fbx>
#   blender -b ... --python Blender/Armas/pegada_fogo_conferir.py -- <rotulo> <Arma> fbx=<FBX> medir [fotos] <pasta>
#
# O QUE HÁ AQUI
# - ENTRADAS: uma por arma, com as poses que o ajudante dela acertou e o motor que as assa nos clipes;
# - a base comum (as partes da primeira versão que os motores usam: _matrizes, _no_limite, _misturar, exportar...);
# - a conferência do original (original_fbx) e o principal(), que chama o motor da arma;
# - _MOTORES: o código de cada motor, carregado como módulo (fogo_pistola, fogo_rifle, fogo_sniper, fogo_espingarda e
#   os que eles usam). Eles fazem "import pegada_fogo as pf" e recebem este arquivo.

import copy
import hashlib
import importlib
import importlib.abc
import importlib.util
import json
import linecache
import math
import os
import sys
import traceback

import bpy
from mathutils import Matrix, Quaternion, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = r"D:\Zombiegame\Zombie-City-Cleaner"
for p in (AQUI, os.path.join(REPO, "Blender", "Armas"), os.path.join(REPO, "Blender")):
    if p not in sys.path:
        sys.path.insert(0, p)
import agua_animacoes as ag  # noqa: E402
import duas_maos as dm  # noqa: E402
import pegada as pg  # noqa: E402
import pegada_fogo_conferir as fc  # noqa: E402

NO_CAMINHO = (55.0, 22.0, 90.0)  # fora da pegada (a recarga), a mão gira o mínimo para o pulso ficar nestes limites
PERTO_MIN = 0.12          # (o braço direito encosta no rosto nas armas de fogo: o limite da câmera é menor que nas brancas)

# Os FBX originais (os de antes da pegada nova, iguais aos do projeto em 11/11/2026). O rifle de sucata parte do da sniper.
ORIGINAIS_SHA256 = {
    "Pistola": "8b065f2d2f1a5bb16f4f983c4dc92c087e5a86a72a05da8445bee1e781638b8e",
    "Rifle": "63c640cc83ec78be315c074e3b8be93cbac99bf0e81c7c63d8acccc71edcd3eb",
    "Sniper": "a7f366d1c19b52b916b0353bd51801541ca4b74626c454e907fc3dd4003d3245",
    "Espingarda": "0edd460c6cf61dabf28718f02f16ee6821eb3c910473ba1d94f638cfc198db2f",
    "Submetralhadora": "e00360b7c38eaa123aba905030b9bf582f69c3ef94b2382bf15e983127fd77c4",
}

# PISTOLA: o pose_r2.py da rodada 2.
# A pose da rodada 2 (variante: PARAMS no fim) (as mãos no espaço da arma: o cano em -Y, o alto em +Z, a direita da arma em -X; m). Os ângulos dos
# dedos em graus ("auto": fechados até encostar pelo posar.py, que grava o resultado em dobras_<rotulo>.json).
_POSE_PISTOLA = {
    # a direita alta no punho: os nós na quina da frente, a base da palma dando a volta por trás do punho (15 graus),
    # os três dedos de baixo fechados na frente do punho, o indicador no gatilho, o polegar passando por trás da cauda
    # e deitado ao longo da armação, do lado esquerdo
    "R": {
        "dedos": (-0.3086, -0.9497, -0.052), "costas": (-0.951, 0.309, 0.0),
        "ancora": (-0.0106, 0.099, 0.0), "em": (-0.036, -0.036, 0.015),
        "dobras": {"index": (8, 80, 12), "middle": (40, 60), "ring": (40, 60), "pinky": (40, 60)},
        "polegar_dir": ((0.76, -0.60, 0.26), (0.0, -0.99, -0.08)),
        "auto": ("middle", "ring", "pinky"), "a1max": {"index": 10}, "a2max": {"index": 100},
        "cotovelo": (-0.14, -0.075, -0.225), "ombro_em": (-0.31, 0.135, -0.03),
    },
    # a esquerda abraça os dedos da direita pelo lado esquerdo: os quatro dedos deitados por cima dos três da direita,
    # embaixo do guarda-mato; a base da palma na lateral esquerda do punho; o polegar para a frente, embaixo do da direita
    "L": {
        "dedos": (0.25, -0.965, -0.087), "costas": (0.965, 0.25, 0.0), "giros": (("Y", 16.0),),
        "ancora": (0.0106, 0.099, 0.0), "em": (0.051, -0.032, 0.003),
        "dobras": {"index": (10, 10), "middle": (10, 10), "ring": (10, 10), "pinky": (10, 10)},
        "polegar_dir": ((0.12, -0.99, 0.10), (0.0, -0.99, -0.03)),
        "auto": ("index", "middle", "ring", "pinky"), "a1max": {"pinky": 72}, "a2max": {"pinky": 60},
        "cotovelo": (-0.02, -0.075, -0.20), "ombro_em": (0.05, 0.22, -0.08),
    },
}

# os dedos fechados até encostar (o dobras_r2.json, gravado pelo posar2.py: index/middle/ring/pinky -> as dobras)
_DOBRAS_PISTOLA = {'R': {'index': [8, 80, 12], 'middle': [92, 76, 0.0, 0.0], 'ring': [94, 62, 0.0, 0.0], 'pinky': [94, 60, 0.0, 0.0]},
 'L': {'index': [68, 36, 0.0, 0.0],
       'middle': [68, 34, 0.0, 0.0],
       'ring': [74, 10, 0.0, 0.0],
       'pinky': [72, 34, 0.0, 0.0]}}

# RIFLE: o params.json (a pegada; somado ao PADRAO do motor) e o params2.json da rodada 2 (por cima dele).
_PARAMS_RIFLE = {'L': {'theta': 48,
       'phi': 66,
       'incl': -18,
       'y': -0.345,
       'ombro': [0.1, 0, 0.1],
       'cotovelo': [-0.014, 0.032, -0.044],
       'giro': 0,
       'abrir': {'index': 42, 'middle': 36, 'ring': 32, 'pinky': 28},
       'a1max': 72,
       'a2max': 90,
       'polegar_dir': [0.25, -1.0, 0.25]},
 'R': {'psi': 5,
       'phi': 45,
       'ponto': [0.0, -0.012, -0.02],
       'ombro': [0, 0.2, 0.2],
       'giro': 20,
       'abrir': -30,
       'a1max': 90},
 'clipes': {'Run': {'L': {'ombro': [0.227, 0.037, 0.114], 'cotovelo': [-0.068, 0.082, -0.021]}},
            'Sacar': {'L': {'cotovelo_inicio': [-0.02, 0.1, 0.15], 'ate': [4, 9]}}},
 'lim_custo': [53, 22, 75],
 'afasta': 0.07}

_PARAMS2_RIFLE = {'L': {'cotovelo': [0.05, 0.032, -0.044]},
 'R': {'dedo_fora': [-0.03, 0.025, 0.006], 'dedo_fora_ini': 0.3, 'dedo_fora_k': 2.5},
 'clipes': {'Run': {'L': {'ombro': [0.227, 0.037, 0.114], 'cotovelo': [-0.01, 0.082, -0.021]}},
            'Sacar': {'L': {'cotovelo_inicio': [-0.084, 0.1, 0.15], 'ate': [4, 9]}},
            'Reload': {'lim_custo': [53, 15, 75]}},
 'trans2': {'k_abrir': 3.0,
            'k_dobra': 2.2,
            'afasta_dir': [0.4, 0, -1],
            'afasta': 0.035,
            'rolar_lim': 0,
            'arfar_lim': 0,
            'polo_passagem': 0,
            'polo_lim': 0,
            'juntar': {'index': 8, 'middle': 2, 'ring': -4, 'pinky': -10},
            'rolar_longe': 90,
            'soltar': 25,
            'abrir_ini': 0.3,
            'dobra_ini': 0.3}}

# SNIPER e RIFLE DE SUCATA: o poses5.py da rodada 5 (a rodada 4 com a corrida consertada).
# As pegadas escritas à mão, no espaço da arma solta (o cano em -Y, o alto em +Z, a direita da arma em -X; m).
# y: para onde os dedos apontam (do pulso para os nós); palma: para onde a palma olha; palma_em: o ponto da arma onde o
# meio da palma encosta (a mão desliza na direção 'desliza', por omissão a da palma, até a pele tocar a malha);
# dedos: nome -> (dobra máxima da 1ª falange, da 2ª, abre para o lado[, dobra mínima da 1ª, da 2ª]): cada falange fecha
# até tocar a arma; polegar: abrir / rolar / dobra (a pose de partida), max1 / max2 (fecha até tocar);
# cotovelo: o ponto para onde o cotovelo é puxado (no espaço da arma); ombro: o deslocamento do ombro (espaço da armadura).
_POSES_SNIPER = {
    "Sniper": {
        "L": {
            # (rodada 4) 8,5 cm mais à frente (na ponta da coronha dianteira: o pulso sobe na tela) e girada 15 graus para o
            # antebraço (o pulso dobra menos e o antebraço vai ao cotovelo do original, mais em pé na tela)
            "y": (-0.653, -0.752, 0.086), "palma": (-0.50, 0.514, 0.697), "palma_em": (-0.006, -0.420, 0.080), "desliza": (0, 0, 1),
            "dedos": {"index": (90, 100, 4), "middle": (90, 100, 1), "ring": (90, 100, -3), "pinky": (90, 100, -7)},
            "polegar": {"alvo": (0.022, -0.460, 0.100), "direcao": (-0.1, -0.99, 0.05), "faixa_dobra2": (0, 40), "deitado1": 0.4},
            "cotovelo": (0.26, -0.195, -0.12), "ombro": (0.19, -0.12, 0.23), "giro_longe": (0.05, 0.12), "dedos_caminho": 0.35, "punho_caminho_k": 0.5, "k_mao": 0.5,
        },
        "R": {
            # (rodada 4) a mão rolada por cima do pescoço (a palma olhando para a esquerda e para baixo): as costas da mão e os
            # nós ficam para cima, virados para a câmera, e o pulso mais alto; o polegar dá a volta por cima
            "y": (0.10, -0.75, -0.65), "palma": (0.70, 0.0, -0.70), "palma_em": (-0.012, -0.015, 0.068), "desliza": (1, 0, 0),
            "dedos": {"index": (10, 15, 0), "middle": (100, 100, 2), "ring": (100, 100, -2), "pinky": (100, 100, -5)},
            "polegar": {"alvo": (0.018, -0.040, 0.092), "direcao": (0.5, -0.2, -0.85), "faixa_dobra2": (25, 80), "deitado1": 0.4},
            "gatilho": (0.0, -0.074, 0.046), "gatilho_max": 0.030, "gatilho_abrir": (-30, -20, -10, 0, 10, 20, 30), "gatilho_a1max": 95,
            "cotovelo": (-0.27, 0.21, 0.20), "ombro_auto": (95, -80), "ombro_na_arma": False, "cotovelo_fixo": 1.0, "ancora_pegada": "polegar", "ancora_peso": 0.35, "braco_proprio": True, "polo_caminho": "original",
            "lim_caminho": (58, 24, 88), "ancora_caminho": "pinca", "pinca_exp": 2.0, "levantar": 0.04, "passagem_reta": True, "palma_nao_desce": 0.004,
            # (rodada 3) a pinça no ferrolho (espaço da arma) e a passagem com arco para fora
            "pinca": {"y": "original", "y_gira": (0, -40, -25), "punho": (85, 100), "off": (-0.010, 0.0, -0.006)}, "ombro_da_pegada": True, "cotovelo_alvo": "pegada", "fora": 0.03,
            # (rodada 4) o cotovelo baixo, ao lado do rosto (espaço da armadura, onde fica a câmera), levado com o
            # deslocamento da arma: o antebraço sai pelo canto de baixo à direita como no original; o ombro perto do
            # original; na corrida a mão gira na lateral do pescoço (pivo_max) para o antebraço não subir pela borda
            "cotovelo_alvo": "armadura", "cotovelo_arm": (-0.28, -0.02, -0.14), "cotovelo_arm_k": 1.0, "pivo_max": 30, "ombro_da_pegada": False,
            # (rodada 4) o ferrolho no tempo do original (sai depois do q8, volta no q24), os dedos que não pegam a bola
            # fechados (nunca abertos menos de 50/60 graus) e um arco menor
            "passo_w": 1.0, "abre_min": (50, 60), "levantar": 0.015, "fora": 0.02, "dedos_soltos": (60, 80),
            # (rodada 5) só na corrida (a arma desce e gira; na rodada 4 a mão e o antebraço sumiam na borda de baixo): a
            # pegada resolvida de novo 3 cm mais à frente no pescoço (mais perto do gatilho; com a arma inclinada na corrida,
            # a frente da arma fica mais alta na tela), girada 30 graus no plano da palma (o pivô, fixo em todos os quadros)
            # e o cotovelo 8 cm mais baixo (ao lado do corpo): o antebraço sai pela borda de baixo, sem tocar a borda direita
            "clipes": {"Run": {"pivo_fixo": -30, "cotovelo_arm": (-0.28, -0.02, -0.22), "pegada": {"palma_em": (-0.012, -0.045, 0.068)}}},
        },
    },
    # o rifle de sucata (as animações da sniper): a tábua da coronha dianteira 2,5 cm mais baixa que a madeira da Mosin
    # (rodada 2: a esquerda acima da vareta; a direita em cima da tábua, opção b; a opção a, no punho, está em trab/poses_r1.py)
    "Sucata_Rifle": {
        "L": {
            # (rodada 2) como a da Mosin (alinhada com o cano, o polegar deitado na lateral esquerda), um pouco mais para
            # trás que na rodada 1 e com o polegar acima da vareta (que corre embaixo à esquerda da tábua, de y -0,33 a -0,50)
            # (rodada 4) como a nova da Mosin: na ponta da tábua (10 cm mais à frente: a bandoleira fica atrás da mão e a
            # vareta ao longo da tábua, ao lado do polegar), girada 15 graus para o antebraço
            "y": (-0.653, -0.752, 0.086), "palma": (-0.50, 0.514, 0.697), "palma_em": (-0.004, -0.450, 0.046), "desliza": (0, 0, 1),
            "dedo_todo": True,
            "dedos": {"index": (90, 100, 14, 22, 30), "middle": (90, 100, 3), "ring": (90, 100, -3), "pinky": (90, 100, -7)},
            "polegar": {"alvo": (0.020, -0.490, 0.080), "direcao": (-0.1, -0.99, 0.05), "faixa_dobra2": (0, 40), "deitado1": 0.4},
            "cotovelo": (0.25, -0.25, -0.16), "ombro": (0.19, -0.12, 0.23), "giro_longe": (0.05, 0.12), "dedos_caminho": 0.0, "punho_caminho": True, "punho_caminho_k": 0.5, "k_mao": 0.5,
        },
        "R": {
            # (rodada 4) como a da Mosin: a mão rolada por cima da quina de cima da tábua (a palma olhando para a esquerda e
            # para baixo), o polegar dando a volta por cima até a face esquerda, o indicador reto ao longo da face direita
            # acima do guarda-mato de arame, os outros dedos em garra na face direita (nenhum abaixo da tábua nem no arame);
            # o cotovelo baixo ao lado do rosto: o antebraço sai pelo canto de baixo à direita, como no original
            # (os dedos médio, anelar e mínimo abertos para trás, em cima do punho: nenhum entra no guarda-mato de arame)
            "y": (0.10, -0.75, -0.65), "palma": (0.70, 0.0, -0.70), "palma_em": (-0.014, 0.002, 0.076), "desliza": (1, 0, 0),
            "dedos_ordem": "ponta", "dedos": {"index": (8, 10, 0), "middle": (30, 40, -15), "ring": (30, 40, -30), "pinky": (30, 40, -40)},
            "polegar": {"alvo": (0.018, -0.035, 0.094), "direcao": (0.5, -0.2, -0.85), "faixa_dobra2": (25, 80), "deitado1": 0.4},
            "cotovelo": (-0.25, 0.20, 0.25), "ombro_auto": (95, -80), "ombro_na_arma": False, "cotovelo_fixo": 1.0, "ancora_pegada": "polegar", "ancora_peso": 0.35, "braco_proprio": True, "polo_caminho": "original",
            "lim_caminho": (58, 24, 88), "ancora_caminho": "pinca", "pinca_exp": 2.0, "passagem_reta": True, "palma_nao_desce": 0.004,
            "pinca": {"y": "original", "y_gira": (0, -40, -25), "punho": (85, 100), "off": (-0.010, 0.0, -0.006)},
            "cotovelo_alvo": "armadura", "cotovelo_arm": (-0.28, -0.02, -0.14), "cotovelo_arm_k": 1.0, "pivo_max": 30, "ombro_da_pegada": False,
            "passo_w": 1.0, "abre_min": (50, 60), "levantar": 0.015, "fora": 0.02, "dedos_soltos": (60, 80),
            # (rodada 5) só na corrida, como na Mosin: a pegada 3 cm mais à frente na quina da tábua, girada 12 graus no plano
            # da palma (o pivô fixo) e o cotovelo 8 cm mais baixo; o mínimo reto e aberto para trás (dobrado, ele descia
            # até o arame do guarda-mato)
            "clipes": {"Run": {"pivo_fixo": -12, "cotovelo_arm": (-0.28, -0.02, -0.22), "pegada": {"palma_em": (-0.014, -0.028, 0.076),
                       "dedos": {"index": (8, 10, 0), "middle": (30, 40, -15), "ring": (30, 40, -30), "pinky": (0, 0, -40)}}}},
        },
    },
}

# ESPINGARDA: o p_final.json da rodada 3 (o params_r3.json da rodada3/).
_PARAMS_ESPINGARDA = {'L': {'osso': 'pump',
       'C': [0.001, -0.498, 0.0255],
       'phi': 30,
       'alfa': 45,
       'g': [0.0, 0.09, -0.0375],
       'polegar_alvo': [0.03, -0.53, 0.032],
       'direto': 1,
       'peso_tela': 600,
       'peso_cot': 0,
       'tol': 0.001,
       'tol_polegar': 0.0008,
       'modo_dedos': 'fila',
       'pre': 5,
       'peso_encosto': 3.0,
       'dobra_boa': 50,
       'rigido_cot': 0.9,
       'rigido_ombro': 0.9,
       'giro_caminho': 'antes',
       'dedos': {},
       'por_quadro': 1,
       'pq_gira_ombro': 1,
       'abre_dedos': 1.0,
       'pq_tela': 400,
       'pq_meio': 1500,
       'pq_suave': 1,
       'pq_cont': 150,
       'peso_vao_polegar': 8000.0,
       'abre': {},
       'polegar_passos': [[-120, -100, -80, -60, -40, -20, 0, 20, 40, 60, 80, 100, 120],
                          [-90, -70, -50, -30, -10, 10, 30, 50, 70, 90],
                          [-70, -50, -30, -10, 10, 30, 50, 70, 90, 110, 130, 150],
                          [0, 20, 40, 60]],
       'reforco': {'Reload': {'2': [5, 60],
                              '3': [6, 75, 35],
                              '4': [6, 75, 35],
                              '8': [6, 75, 35],
                              '9': [6, 75, 35],
                              '10': [6, 75, 35],
                              '11': [6, 75, 35],
                              '13': [5, 60]}},
       'no_caminho': [60, 24, 90],
       'z_max': 0.04,
       'peso_z': 3.0,
       'peso_vizinho': 4.0,
       'vao_vizinho': 3.0,
       'abd_passos': [-24, -20, -16, -12, -8, -4, 0, 4, 8, 12, 16, 20, 24],
       'polegar_fino': 1,
       'ajuste': {'Reload': {'12': {'mover': [-0.005, 0, -0.02], 'dedos': 0.0, 'giro': 1.0},
                             '3': {'mover': [0.01, -0.03, -0.05], 'dedos': 1.0},
                             '9': {'mover': [0, 0, -0.015]}}},
       'ombro_visivel': 300.0},
 'R': {'osso': 'gun',
       'C': [-0.002, -0.01, 0.014],
       'psi': -20,
       'gama': 55,
       'g': [0.0, 0.07, -0.043],
       'gatilho': [0.0, -0.08, -0.004],
       'polegar_alvo': [0.026, -0.03, 0.032],
       'direto': 0,
       'ombro_alvo': [-0.22, 0.1, -0.2],
       'peso_ombro': 250,
       'tol': 0.0,
       'tol_polegar': 0.0008,
       'modo_dedos': 'fechar',
       'pre': 5,
       'ombro': [-0.3972320556640625, 0.09938657283782959, 0.451646625995636],
       'cot': [-0.05825674533843994, 0.2300896942615509, 0.31573086977005005],
       'rec_polegar_alvo': [-0.03, -0.06, 0.02],
       'rec_clipe': 'Reload',
       'rec_w': {'2': 0.5,
                 '3': 1,
                 '4': 1,
                 '5': 1,
                 '6': 1,
                 '7': 1,
                 '8': 1,
                 '9': 1,
                 '10': 1,
                 '11': 1,
                 '12': 1,
                 '13': 0.5},
       'rec_polegar_meio': [-0.024, -0.07, 0.05],
       'polegar_passos': [[-70, -60, -50, -40, -30, -20, -10, 0, 10, 20, 30, 40, 50, 60, 70],
                          [-80, -70, -60, -50, -40, -30, -20, -10, 0, 10, 20, 30, 40, 50, 60, 70, 80],
                          [-70, -60, -50, -40, -30, -20, -10, 0, 10, 20, 30, 40, 50, 60, 70],
                          [25, 35, 45]]}}

# Uma entrada por arma. "motor": o módulo (em _MOTORES) que assa a pose nos clipes; "com_nome": o motor recebe o nome
# da arma antes da saída; o resto são as poses e os parâmetros que o ajudante da arma acertou (o espaço da arma: o cano
# em -Y, o alto em +Z, a direita da arma em -X; m e graus).
ENTRADAS = {
    # PISTOLA (rodada 2): a de duas mãos "polegares para a frente". A direita alta no punho (os nós na quina da frente,
    # a base da palma dando a volta por trás, os três dedos de baixo fechados na frente do punho, o indicador no
    # gatilho, o polegar deitado ao longo da armação, do lado esquerdo); a esquerda abraça os dedos da direita pelo lado
    # esquerdo, o polegar para a frente, logo embaixo do da direita. Na recarga a esquerda anda presa ao movimento do
    # original (sai para baixo, volta com o pente) e os dedos vão direto da pegada para os do original, sem abrir.
    # "pose": a do pose_r2.py; "dobras": os dedos fechados até encostar (o dobras_r2.json, gravado pelo posar2.py).
    # Os mesmos braços levam a pistola de sucata.
    "Pistola": {"motor": "fogo_pistola", "pose": _POSE_PISTOLA, "dobras": _DOBRAS_PISTOLA},
    # RIFLE (M4, rodada 2): a esquerda em concha por baixo e à esquerda do guarda-mão (theta 48: a palma em volta do
    # cano; phi 66: a diagonal), os dedos dando a volta por baixo, o polegar deitado no lado esquerdo; a direita no punho
    # de pistola, o indicador no gatilho. O cotovelo esquerdo posto para o antebraço entrar na tela como o original. Na
    # recarga a esquerda vai por baixo da arma até o carregador (a palma para a arma), e o indicador direito sai do
    # gatilho e fica reto ao longo da caixa. "params": o params.json; "params2": o params2.json da rodada 2 (somado por
    # cima, como o rp.juntar faz). As pegadas calculadas saem em <saida>_params.json.
    "Rifle": {"motor": "fogo_rifle", "params": _PARAMS_RIFLE, "params2": _PARAMS2_RIFLE},
    # SNIPER (Mosin, rodada 5): a esquerda por baixo da madeira, na ponta da coronha dianteira, alinhada com o cano, o
    # polegar deitado ao longo do lado esquerdo; a direita rolada por cima do pescoço da coronha (as costas da mão para a
    # câmera, o polegar dando a volta por cima, o indicador no gatilho), o cotovelo baixo ao lado do rosto: o antebraço
    # sai pelo canto de baixo à direita, como no original. No ferrolho a mão direita faz a pinça na bola do ferrolho, no
    # tempo do original. Na corrida ("clipes" -> "Run" na pose da direita), a pegada 3 cm mais à frente, girada 30 graus
    # no plano da palma, e o cotovelo mais baixo: a mão e o antebraço ficam no canto de baixo à direita.
    "Sniper": {"motor": "fogo_sniper", "com_nome": True, "pose": _POSES_SNIPER["Sniper"]},
    # RIFLE DE SUCATA (as animações da sniper, rodada 5): a esquerda como a da Mosin, na ponta da tábua; a direita rolada
    # por cima da quina de cima da tábua, logo à frente do punho (a opção b: no punho de pistola a mão fica toda abaixo
    # da tela), o indicador reto acima do guarda-mato de arame, os outros dedos abertos para trás. Na corrida, a pegada
    # 3 cm mais à frente, girada 12 graus, o mínimo reto, e o cotovelo mais baixo.
    "Sucata_Rifle": {"motor": "fogo_sniper", "com_nome": True, "pose": _POSES_SNIPER["Sucata_Rifle"]},
    # ESPINGARDA (Remington 870, rodada 3): a esquerda fechada na telha da bomba (presa ao osso pump), os dedos em fila
    # (cada um encostado no anterior, sem entrar na arma), o polegar deitado ao longo da telha; a direita no pescoço, o
    # indicador no gatilho, o polegar por cima. O cotovelo esquerdo escolhido por quadro (o pulso reto, o antebraço na
    # tela perto do de antes). "params": o p_final.json da rodada 3 (L e R). As pegadas saem em <saida>_params.json.
    "Espingarda": {"motor": "fogo_espingarda", "params": _PARAMS_ESPINGARDA},
    # SUBMETRALHADORA: NÃO USADA. Voltou ao FBX original (a primeira tentativa dela foi reprovada junto com as outras).
    "Submetralhadora": {"motor": None},
}


def entrada(arma):
    """Uma cópia da entrada da arma (o motor pode mexer nela à vontade)."""
    return copy.deepcopy(ENTRADAS[arma])


# ---------------------------------------------------------------------------------------------------------------------
# A base comum: as partes da primeira versão deste arquivo que os motores usam (copiadas sem mudar nada).

def _usar(rig, acao):
    ag.usar_acao(rig, acao)


def _matrizes(rig, acao, f):
    _usar(rig, acao)
    bpy.context.scene.frame_set(f)
    return {pb.name: pb.matrix.copy() for pb in rig.pose.bones}


def _da_arma(M, rest, osso, E):
    """A matriz que leva o espaço da arma solta ao da armadura, na pose M (a arma anda com o osso)."""
    return M[osso] @ rest[osso].inverted() @ E


def _no_limite(esq, M, lado, lim, antes=None):
    """O giro da mão com o pulso dentro dos limites (a dobra, o desvio e a torção cortados em lim), o mesmo lugar.
    antes: a torção cortada do quadro anterior (rad): passando do limite, a torção fica do lado dela (onde a de antes
    dá a volta em 180 graus, o corte não troca de +90 para -90). Devolve (o giro, a torção cortada)."""
    ua, fa, mao = "upper_arm." + lado, "forearm." + lado, "hand." + lado
    S, Ec, W = M[ua].translation, M[fa].translation, M[mao].translation
    u = (W - S).normalized()
    e = (Ec - S) - (Ec - S).dot(u) * u
    e = e.normalized() if e.length > 1e-6 else u.orthogonal().normalized()
    z = u.cross(e) if lado == "L" else -u.cross(e)
    B = ag.quadro_yz(W - Ec, z, Ec).to_3x3() @ (esq.rest[fa].to_3x3().inverted() @ esq.rest[mao].to_3x3())
    q = (B.inverted() @ M[mao].to_3x3()).to_quaternion()
    if q.w < 0:
        q = -q
    n = math.hypot(q.w, q.y)
    tw = Quaternion((q.w / n, 0.0, q.y / n, 0.0)) if n > 1e-9 else Quaternion()
    s = tw.inverted() @ q
    if s.w < 0:
        s = -s
    tor = 2 * math.atan2(tw.y, tw.w)
    tor = (tor + math.pi) % (2 * math.pi) - math.pi
    ang = s.angle
    eixo = s.axis if ang > 1e-9 else Vector((1.0, 0.0, 0.0))
    dx, dz = ang * eixo.x, ang * eixo.z
    lx, lz, lt = (math.radians(v) for v in lim)
    if antes is not None:
        # no máximo 30 graus de um quadro para o outro, pelo lado da de antes (onde a de antes dá a volta em 180)
        d = (tor - antes + math.pi) % (2 * math.pi) - math.pi
        passo = math.radians(dm.PASSO_MAX)
        tor = antes + max(-passo, min(passo, d))
    tor2 = max(-lt, min(lt, tor))
    dx2, dz2 = max(-lx, min(lx, dx)), max(-lz, min(lz, dz))
    if abs(tor2 - tor) < 1e-6 and abs(dx2 - dx) < 1e-6 and abs(dz2 - dz) < 1e-6:
        return M[mao].to_3x3(), tor2
    v = Vector((dx2, 0.0, dz2))
    s2 = Quaternion(v.normalized(), v.length) if v.length > 1e-9 else Quaternion()
    tw2 = Quaternion(Vector((0.0, 1.0, 0.0)), tor2)
    return B @ (tw2 @ s2).to_matrix(), tor2


def _suave(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def _misturar(A, B, t):
    """De A (t = 0) para B (t = 1): o lugar em linha reta e o giro pelo menor caminho."""
    if t <= 0:
        return A.copy()
    if t >= 1:
        return B.copy()
    q = A.to_quaternion().slerp(B.to_quaternion(), t)
    m = q.to_matrix().to_4x4()
    m.translation = A.translation.lerp(B.translation, t)
    return m


def exportar(rig, bracos, boca, acoes, caminho):
    """Um take por faixa de NLA (o nome do clipe), como o maos_sniper.py; a 24 quadros por segundo (o do original)."""
    ad = rig.animation_data
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    for nome, a in sorted(acoes.items()):
        tr = ad.nla_tracks.new()
        tr.name = nome
        st = tr.strips.new(nome, int(a.frame_range[0]), a)
        st.name = nome
        if hasattr(st, "action_slot") and len(a.slots):
            st.action_slot = a.slots[0]
    ad.action = None
    ad.use_nla = True       # (o usar_acao desliga o NLA: sem isto, todos os takes saem no repouso)
    for o in bpy.context.view_layer.objects:
        o.select_set(o in (rig, bracos, boca))
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"ARMATURE", "MESH", "EMPTY"},
                             apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, mesh_smooth_type="FACE")
    print("  EXPORTADO", caminho)


# ---------------------------------------------------------------------------------------------------------------------
# O original: os motores partem sempre dele.

_PASTA_ORIGINAIS = None


def _sha256(caminho):
    h = hashlib.sha256()
    with open(caminho, "rb") as fh:
        for bloco in iter(lambda: fh.read(1 << 20), b""):
            h.update(bloco)
    return h.hexdigest()


def original_fbx(arma):
    """O FBX original dos braços da arma (o rifle de sucata: o da sniper), conferido pelo SHA-256."""
    nome = fc.ARMAS[arma][2]
    caminho = os.path.join(_PASTA_ORIGINAIS or ag.JOGADOR, f"Jogador_FP_{nome}.fbx")
    if not os.path.exists(caminho):
        raise RuntimeError(f"ORIGINAL: não achei {caminho} (passe original=<pasta dos FBX originais>)")
    h = _sha256(caminho)
    if h != ORIGINAIS_SHA256[nome]:
        raise RuntimeError(f"ORIGINAL: {caminho} não é o FBX original (SHA-256 {h[:12]}...): a pegada seria aplicada em "
                           f"cima de outra. Passe original=<pasta com os originais> (eles estão no histórico do git)")
    return caminho


# ---------------------------------------------------------------------------------------------------------------------
# Os motores, carregados como módulos (o código está em _MOTORES, no fim do arquivo).

class _Motores(importlib.abc.MetaPathFinder, importlib.abc.Loader):
    def find_spec(self, nome, caminho=None, alvo=None):
        if nome in _MOTORES:
            return importlib.util.spec_from_loader(nome, self, origin=os.path.abspath(__file__))
        return None

    def create_module(self, spec):
        return None

    def exec_module(self, mod):
        fonte = _MOTORES[mod.__name__]
        arq = f"{os.path.abspath(__file__)} <{mod.__name__}>"
        linecache.cache[arq] = (len(fonte), None, fonte.splitlines(True), arq)   # (as linhas aparecem nos erros)
        mod.__file__ = os.path.abspath(__file__)
        exec(compile(fonte, arq, "exec"), mod.__dict__)


def principal():
    """-- <Arma> <saida.fbx> [original=<pasta>] [opções do motor]"""
    global _PASTA_ORIGINAIS
    args = sys.argv[sys.argv.index("--") + 1:]
    arma, saida = args[0], os.path.abspath(args[1])
    pasta = next((a.split("=", 1)[1] for a in args[2:] if a.startswith("original=")), None)
    resto = [a for a in args[2:] if not a.startswith("original=")]
    e = ENTRADAS.get(arma)
    if not e or not e.get("motor"):
        print(f"PEGADA_FOGO: {arma} não tem pegada nova (a submetralhadora usa o FBX original)")
        sys.exit(2)
    try:
        _PASTA_ORIGINAIS = os.path.abspath(pasta) if pasta else None
        fc.ORIGINAIS = _PASTA_ORIGINAIS     # (os motores que leem pelo fc.bracos_fbx)
        print(f"PEGADA_FOGO {arma}: original {original_fbx(arma)}, motor {e['motor']}, saída {saida}")
        os.makedirs(os.path.dirname(saida), exist_ok=True)
        sys.argv = [sys.argv[0], "--"] + ([arma] if e.get("com_nome") else []) + [saida] + resto
        if not any(isinstance(f, _Motores) for f in sys.meta_path):
            sys.meta_path.insert(0, _Motores())
        mod = importlib.import_module(e["motor"])
        if hasattr(mod, "principal"):
            mod.principal()
        print(f"PEGADA_FOGO OK {arma}")
    except SystemExit:
        raise
    except Exception:
        traceback.print_exc()
        print(f"PEGADA_FOGO FALHOU {arma}")
        sys.exit(1)


# ---------------------------------------------------------------------------------------------------------------------
# _MOTORES: o código dos quatro motores, como os ajudantes deixaram (só os imports e a leitura das poses mudaram: as
# poses vêm de ENTRADAS, o original vem do original_fbx, e o "if __name__" virou principal()).

_MOTORES = {}

# ---------------------------------------------------------------------------------------------------------------------
# PISTOLA (rodada 2): o pegada_pistola_r2.py (assa a pose nos 7 clipes; a recarga presa ao original)
_MOTORES["fogo_pistola"] = r'''# A pegada nova da pistola, rodada 2 (11/11/2026): a de duas mãos "polegares para a frente" das referências do Pedro,
# escrita à mão no espaço da arma (pose_r2.py + dobras_r2.json, experimentadas com o posar2.py), assada nos 7 clipes do
# Jogador_FP_Pistola.fbx ORIGINAL. A arma não sai do lugar (o osso gun e os filhos ficam como estavam).
#   (pelo pegada_fogo.py: -- Pistola <saida.fbx> [segue=Clipe:fração,...] [limitar=0|1]; a pose vem de ENTRADAS)
# Em cada quadro: a mão na pose dela, presa à arma; o braço por IK (o agua_animacoes.braco: metade da torção no antebraço)
# com o ombro e o cotovelo que acompanham a arma (SEGUE). Onde a mão do original sai da pegada (a esquerda na recarga),
# a mão nova anda presa ao movimento do original (o mesmo deslocamento rígido, a partir da pegada nova: sai para baixo
# junto com ele, sem subir nem abrir) e a diferença entre as duas pegadas some aos poucos (SAI quadros); na volta, o
# contrário (VOLTA quadros: ela sobe pela lateral do punho com o original e fecha na pegada nova). Os dedos vão direto
# da pegada para os do original (os dois curvos), sem abrir.
import importlib.util
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import fogo_pistola_comum as pc  # noqa: E402
import pegada_fogo as pf  # noqa: E402
import pegada_fogo_conferir as fc  # noqa: E402
import agua_animacoes as ag  # noqa: E402
import duas_maos as dm  # noqa: E402
import pegada as pg  # noqa: E402

LIMIAR = 0.015            # a mão do original a mais disto da pegada dela (no espaço da arma): fora da pegada
SAI = {"Reload": 5}       # quadros para a diferença entre as pegadas sumir depois de a mão sair
VOLTA = {"Reload": 6}     # e para ela aparecer antes de a mão voltar
PALMA = Vector((0.0, 0.06, -0.015))   # o meio da palma (no osso hand)
# na recarga, com o pente entrando e a mão subindo para a pegada, a esquerda passa um pouco à frente (o cano em -Y) do
# caminho do original: os dedos dela em concha passam na frente dos da direita (que agora fecham na frente do punho)
FRENTE = {"Reload": {26: 0.4, 27: 0.8, 28: 1.0, 29: 1.0, 30: 0.85, 31: 0.6, 32: 0.35, 33: 0.12}}
FRENTE_M = 0.02
SEGUE = {"Fire": 0.5, "DryFire": 0.5, "Run": 0.8, "Sacar": 0.8}     # quanto o ombro e o cotovelo acompanham a arma (os outros clipes: 1)
NO_CAMINHO = (55.0, 22.0, 90.0)   # fora da pegada, a mão gira o mínimo para o pulso ficar nestes limites


def carregar_pose(e):
    """A pose da entrada da pistola (ENTRADAS do pegada_fogo.py: "pose", a do pose_r2.py) com os dedos fechados até
    encostar ("dobras", as do dobras_r2.json)."""
    POSE = e["pose"]
    for lado, d in e["dobras"].items():
        POSE[lado]["dobras"].update({k: tuple(v) for k, v in d.items()})
    return POSE


def _parte(X, t):
    """A parte t da diferença rígida X (t = 1: X; t = 0: nada), girando em volta do meio da palma."""
    q = X.to_quaternion()
    if q.w < 0:
        q = -q
    qt = Quaternion().slerp(q, t)
    c = PALMA
    d = (X @ c - c) * t
    R = qt.to_matrix().to_4x4()
    R.translation = c + d - qt.to_matrix() @ c
    return R


def _mistura(A, B, t):
    if t <= 0:
        return A.copy()
    if t >= 1:
        return B.copy()
    m = A.to_quaternion().slerp(B.to_quaternion(), t).to_matrix().to_4x4()
    m.translation = A.translation.lerp(B.translation, t)
    return m


def consertar(arma, POSE, limitar=True):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = ag.importar(fc.bracos_fbx(arma))
    rig = next(o for o in objs if o.type == "ARMATURE")
    bracos = next(o for o in objs if o.type == "MESH" and o.name.startswith("FP_Bracos"))
    acoes = {a.name.split("|", 1)[1]: a for a in bpy.data.actions if a.name.startswith(rig.name + "|")}
    rig.animation_data_create()
    fps = bpy.context.scene.render.fps
    E, erro, _ = fc._encaixe(rig, bracos, fc.ARMAS[arma][1], fc.ARMAS[arma][2])
    Eg = rig.matrix_world.inverted() @ E
    esq = ag.Esqueleto(rig)
    rest = esq.rest
    dm.PERTO_MIN = pf.PERTO_MIN
    print(f"CONSERTAR {arma}: encaixe {erro * 1000:.2f} mm, fps {fps}")

    def da_arma(M):
        return M["gun"] @ rest["gun"].inverted() @ Eg
    M_idle = pf._matrizes(rig, acoes["Idle"], 1)
    Wi = da_arma(M_idle)
    peg = {}
    for lado, p in POSE.items():
        Hg = pc.mao_H(p)
        S = Vector(p["ombro_em"])
        Ec = Vector(p["cotovelo"])
        peg[lado] = {"Hg": Hg, "dedos": pc.dedos_q(lado, p, esq, Hg), "S": S, "Sg": Wi.inverted() @ S, "Eg": Wi.inverted() @ Ec,
                     "h_ref": (Wi.inverted() @ M_idle["hand." + lado]).translation.copy()}
    novas, pior_perto, na_tela = {}, {}, {}
    for nome in sorted(acoes):
        a = acoes[nome]
        f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
        M0s = {f: pf._matrizes(rig, a, f) for f in range(f0, f1 + 1)}
        quadros = {f: dict(M0s[f]) for f in M0s}
        k = SEGUE.get(nome, 1.0)
        for lado, g in peg.items():
            ua, fa, mao = "upper_arm." + lado, "forearm." + lado, "hand." + lado
            ws_ = {}
            # onde a mão do original sai da pegada dela (LIMIAR): os trechos fora; em cada um, a mão nova anda presa ao
            # movimento do original (o mesmo deslocamento rígido, a partir da pegada nova) e a diferença entre a pegada
            # nova e a do original some aos poucos (SAI quadros depois de sair; VOLTA quadros antes de voltar)
            fs = sorted(M0s)
            G0 = {f: da_arma(M0s[f]).inverted() @ M0s[f][mao] for f in fs}
            dist = {f: (G0[f].translation - g["h_ref"]).length for f in fs}
            fora = [f for f in fs if dist[f] > LIMIAR]
            trechos = []
            for f in fora:
                if trechos and f == trechos[-1][1] + 1:
                    trechos[-1][1] = f
                else:
                    trechos.append([f, f])
            plano = {f: (None, 0.0) for f in fs}      # (a diferença X que falta, o peso do braço do original)
            for i0, i1 in trechos:
                a_, b_ = i0 - 1, i1 + 1
                Xa = G0[a_].inverted() @ g["Hg"] if a_ in G0 else None
                Xb = G0[b_].inverted() @ g["Hg"] if b_ in G0 else None
                sai, volta = SAI.get(nome, 5), VOLTA.get(nome, 5)
                for f in range(i0, i1 + 1):
                    ra = (f - a_) / sai if Xa is not None else 9.0           # 0 na pegada, 1 já no caminho
                    rb = (b_ - f) / volta if Xb is not None else 9.0
                    if ra < 1.0 and ra <= rb:
                        s_ = pf._suave(ra)
                        plano[f] = (_parte(Xa, 1.0 - s_), s_)
                    elif rb < 1.0:
                        s_ = pf._suave(rb)
                        plano[f] = (_parte(Xb, 1.0 - s_), s_)
                    else:
                        plano[f] = (Matrix.Identity(4), 1.0)
            if trechos:
                print(f"  PLANO {nome} {lado}: fora {trechos}; peso do original " +
                      " ".join(f"{f}:{plano[f][1]:.2f}" for f in fs if plano[f][0] is not None))
            tor_antes = None
            for f in fs:
                M0, M = M0s[f], quadros[f]
                Wf = da_arma(M0)
                X, w = plano[f]
                S_peg = g["S"].lerp(Wf @ g["Sg"], k)
                E_peg = Wf @ g["Eg"]
                S0, E0 = M0[ua].translation, M0[fa].translation
                S = S_peg.lerp(S0, w)
                polo = (E_peg - S_peg).normalized().lerp((E0 - S0).normalized(), w)
                if X is None:
                    H = Wf @ g["Hg"]
                else:
                    H = Wf @ G0[f] @ X
                    fr = FRENTE.get(nome, {}).get(f, 0.0) if lado == "L" else 0.0
                    if fr:
                        H.translation = H.translation + Wf.to_3x3() @ Vector((0.0, -FRENTE_M * fr, 0.0))
                dedos = {}
                for n, q in g["dedos"].items():
                    dedos[n] = q if w <= 0 else q.slerp(esq.base(M0, n).to_quaternion(), w)
                Ma = dict(M0)
                m = Ma[ua].copy()
                m.translation = S
                Ma[ua] = m
                dm.braco_no_lugar(esq, M, Ma, lado, H, polo, dedos)
                if w > 0 and limitar:
                    # o pulso nos limites, girando a mão em volta do meio da palma (ela fica no caminho)
                    for _ in range(2):
                        R, tor_antes = pf._no_limite(esq, M, lado, NO_CAMINHO, tor_antes)
                        H2 = R.to_4x4()
                        H2.translation = (H @ PALMA) - R @ PALMA
                        dm.braco_no_lugar(esq, M, Ma, lado, H2, polo, dedos)
                else:
                    tor_antes = None
                ws_[f] = w
                # o braço de cima nunca na frente da câmera (12 pontos do ombro ao cotovelo, 5,5 cm de raio)
                Sx, Ex = M[ua].translation, M[fa].translation
                for i in range(13):
                    P = Sx.lerp(Ex, i / 12)
                    d = -P.y
                    if d > 0.03 and abs(P.x) - 0.055 < dm.TX * d and abs(P.z) - 0.055 < dm.TY * d:
                        na_tela.setdefault((nome, lado), set()).add(f)
                pior_perto[(nome, lado)] = min(pior_perto.get((nome, lado), 9.0), dm.perto(dm.capsulas(M, lado)))
            longe = [f for f in fs if ws_[f] > 0]
            print(f"  {nome} {lado}: fora da pegada nos quadros {(min(longe), max(longe)) if longe else '-'}; o braço a "
                  f"{pior_perto[(nome, lado)] * 100:.0f} cm da câmera (o mais perto); o braço de cima na tela: "
                  f"{sorted(na_tela.get((nome, lado), [])) or 'nunca'}")
        for f, M in quadros.items():
            for n in esq.ordem:
                if n not in M:
                    M[n] = M0s[f][n]
        novas[nome] = (a, quadros)
    for nome, (a, quadros) in novas.items():
        nome_acao = a.name
        nova = pg._gravar(rig, esq, nome_acao + "_nova", quadros)
        bpy.data.actions.remove(a)
        nova.name = nome_acao
        acoes[nome] = nova
    boca = next(o for o in bpy.data.objects if o.name.startswith("Boca_FP"))
    bpy.context.scene.render.fps, bpy.context.scene.render.fps_base = fps, 1.0
    return rig, bracos, boca, acoes


def principal():
    import traceback
    args = sys.argv[sys.argv.index("--") + 1:]
    opc = dict(a.split("=", 1) for a in args[1:] if "=" in a)
    try:
        for item in opc.get("segue", "").split(","):
            if ":" in item:
                SEGUE[item.split(":")[0]] = float(item.split(":")[1])
        print("SEGUE", SEGUE)
        POSE = carregar_pose(pf.entrada("Pistola"))
        r, b, bo, ac = consertar("Pistola", POSE, limitar=opc.get("limitar", "1") == "1")
        pf.exportar(r, b, bo, ac, args[0])
        print("ETAPAS OK")
    except Exception:
        traceback.print_exc()
        print("ETAPA FALHOU")
        sys.exit(1)
'''

# ---------------------------------------------------------------------------------------------------------------------
# PISTOLA: o pistola_comum.py (a matriz da mão, os giros dos dedos, fechar até encostar)
_MOTORES["fogo_pistola_comum"] = r'''# A pegada da pistola escrita à mão (rodada 1, 11/11/2026): a pose de cada mão no espaço da arma (o cano em -Y, o alto em
# +Z, a direita da arma em -X) e os giros dos dedos; o braço segue por IK (pegada_pistola.py). Aqui: as contas comuns
# ao posar.py (experimentar num quadro, com a mão nova e a arma do jogo) e ao pegada_pistola.py (assar os clipes).
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = r"D:\Zombiegame\Zombie-City-Cleaner"
for p in (AQUI, os.path.join(REPO, "Blender", "Armas"), os.path.join(REPO, "Blender")):
    if p not in sys.path:
        sys.path.insert(0, p)
import agua_animacoes as ag  # noqa: E402
import duas_maos as dm  # noqa: E402
import brancas_corpo as bc  # noqa: E402

DEDOS = ("index", "middle", "ring", "pinky")
rad = math.radians


def dobra(a):
    return Matrix.Rotation(rad(-a), 4, "X")


def mao_H(p):
    """A matriz do hand no espaço da arma: os dedos (o Y do osso) para p["dedos"], as costas da mão (o Z) para
    p["costas"] (tirada a parte ao longo dos dedos), giros a mais em volta dos eixos da mão (p["giros"]), e o ponto
    p["ancora"] (no espaço do hand: x para o mínimo na direita e para o polegar na esquerda, y do pulso para os nós,
    z para as costas) posto em p["em"] (na arma)."""
    Y = Vector(p["dedos"]).normalized()
    Z = Vector(p["costas"])
    Z = (Z - Z.dot(Y) * Y).normalized()
    X = Y.cross(Z)
    R = Matrix((X, Y, Z)).transposed()
    for eixo, g in p.get("giros", ()):
        R = R @ Matrix.Rotation(rad(g), 3, eixo)
    H = R.to_4x4()
    H.translation = Vector(p["em"]) - R @ Vector(p["ancora"])
    return H


def _euler(v, s):
    return Matrix.Rotation(rad(v[2] * s), 4, "Z") @ Matrix.Rotation(rad(v[1] * s), 4, "Y") @ Matrix.Rotation(rad(v[0]), 4, "X")


def _mirar(P, d, rola=0.0):
    """O giro local (4x4) que leva o Y do osso (com a matriz P sem giro, no espaço da arma) à direção d (na arma), pelo
    caminho mais curto, e depois rola graus em volta dele."""
    alvo = (P.to_3x3().inverted() @ Vector(d)).normalized()
    q = Vector((0.0, 1.0, 0.0)).rotation_difference(alvo)
    return q.to_matrix().to_4x4() @ Matrix.Rotation(rad(rola), 4, "Y")


def dedos_q(lado, p, esq=None, Hg=None):
    """{osso: giro local}: p["dobras"] = {dedo: (a1, a2[, abre, rola])} (a dobra de cada falange; abre: para o lado do
    polegar; rola: em volta do dedo); o polegar: p["polegar"] = ((x, y, z), (x, y, z)) (os giros de Euler do thumb e do
    thumb2, na convenção da mão direita: na esquerda o y e o z trocam de sinal) ou p["polegar_dir"] = (d1, d2[, rola1,
    rola2]) (para onde cada osso do polegar aponta, no espaço da arma; precisa de esq e de Hg)."""
    s = 1.0 if lado == "R" else -1.0
    out = {}
    for d, v in p["dobras"].items():
        abre = v[2] if len(v) > 2 else 0.0
        rola = v[3] if len(v) > 3 else 0.0
        out[f"{d}1.{lado}"] = (Matrix.Rotation(rad(abre * s), 4, "Z") @ Matrix.Rotation(rad(rola * s), 4, "Y") @ dobra(v[0])).to_quaternion()
        out[f"{d}2.{lado}"] = dobra(v[1]).to_quaternion()
    if "polegar_dir" in p:
        v = p["polegar_dir"]
        rest = esq.rest
        n1, n2 = "thumb." + lado, "thumb2." + lado
        P1 = Hg @ (rest["hand." + lado].inverted() @ rest[n1])
        B1 = _mirar(P1, v[0], (v[2] if len(v) > 2 else 0.0) * s)
        P2 = P1 @ B1 @ (rest[n1].inverted() @ rest[n2])
        B2 = _mirar(P2, v[1], (v[3] if len(v) > 3 else 0.0) * s)
        out[n1], out[n2] = B1.to_quaternion(), B2.to_quaternion()
    else:
        t1, t2 = p["polegar"]
        out["thumb." + lado] = _euler(t1, s).to_quaternion()
        out["thumb2." + lado] = _euler(t2, s).to_quaternion()
    return out


def bvh_de(vs, fs):
    return BVHTree.FromPolygons(vs, fs)


def fundo(bvh, p):
    """Quanto o ponto p entra na malha (m; 0 fora)."""
    loc, nor, _, dist = bvh.find_nearest(p, 0.03)
    if loc is None:
        return 0.0
    if (p - loc).dot(nor) < 0 and bc._dentro(bvh, p)[0]:
        return dist
    return 0.0


def perto(bvh, p):
    loc, nor, _, dist = bvh.find_nearest(p, 0.05)
    return 0.05 if loc is None else dist


def fechar(esq, pts, lado, Hg, p, bvhs, quais=DEDOS, tol=0.0015, a1max=95, a2max=110, passo=2):
    """Fecha cada dedo até encostar (nas malhas de bvhs, no espaço da arma): primeiro a falange de baixo, depois a de
    cima. Parte das dobras de p["dobras"] (o mínimo) e devolve {dedo: (a1, a2, abre, rola)} e um texto."""
    rest = esq.rest
    s = 1.0 if lado == "R" else -1.0
    out, txt = {}, []
    for d in quais:
        v = p["dobras"][d]
        abre = v[2] if len(v) > 2 else 0.0
        rola = v[3] if len(v) > 3 else 0.0
        n1, n2 = f"{d}1.{lado}", f"{d}2.{lado}"
        rel1 = rest["hand." + lado].inverted() @ rest[n1]
        rel2 = rest[n1].inverted() @ rest[n2]
        S = Matrix.Rotation(rad(abre * s), 4, "Z") @ Matrix.Rotation(rad(rola * s), 4, "Y")
        P1 = [q for q in pts[n1] if q.y > 0.014][::2]
        P2 = pts[n2][::2]

        def pen(M, P):
            return max((fundo(b, M @ q) for b in bvhs for q in P), default=0.0)
        a1 = v[0]
        for a in range(int(v[0]), int(p.get("a1max", {}).get(d, a1max)) + 1, passo):
            if pen(Hg @ rel1 @ S @ dobra(a), P1) > tol:
                break
            a1 = a
        M1 = Hg @ rel1 @ S @ dobra(a1)
        a2 = v[1]
        p0 = pen(M1 @ rel2 @ dobra(v[1]), P2)
        for a in range(int(v[1]), int(p.get("a2max", {}).get(d, a2max)) + 1, passo):
            if pen(M1 @ rel2 @ dobra(a), P2) > tol:
                break
            a2 = a
        M2 = M1 @ rel2 @ dobra(a2)
        vao = min((perto(b, M2 @ q) for b in bvhs for q in P2), default=0.05)
        out[d] = (a1, a2, abre, rola)
        txt.append(f"{d} ({a1}, {a2}) vão {vao * 1000:.0f} mm" + (f" [já entra {p0 * 1000:.0f} mm]" if p0 > tol else ""))
    return out, "; ".join(txt)


def ombro_e_polo(esq, M0, lado, H, p):
    """Do antebraço (p["antebraco"]: a direção do cotovelo para o pulso) e do braço de cima (p["braco"]: do ombro para o
    cotovelo), no espaço da armadura, com a mão em H: o deslocamento do ombro e o polo do cotovelo (fixos nos clipes)."""
    ua, fa, mao = "upper_arm." + lado, "forearm." + lado, "hand." + lado
    a, b = esq.comprimento(ua, fa), esq.comprimento(fa, mao)
    W = H.translation
    E = W - b * Vector(p["antebraco"]).normalized()
    S = E - a * 0.97 * Vector(p["braco"]).normalized()
    return S - M0[ua].translation, (E - S).normalized()


def braco(esq, M, M0, lado, H, polo, ombro=None, dedos=None):
    """O braço por IK com a mão em H (no espaço da armadura), o cotovelo para o polo (um vetor) e o ombro deslocado.
    Devolve o avanço do ombro."""
    Ma = dict(M0)
    if ombro:
        m = Ma["upper_arm." + lado].copy()
        m.translation = m.translation + Vector(ombro)
        Ma["upper_arm." + lado] = m
    return dm.braco_no_lugar(esq, M, Ma, lado, H, Vector(polo), dedos)
'''

# ---------------------------------------------------------------------------------------------------------------------
# RIFLE (rodada 2): o gerar2.py (calcula a pose de cada quadro e exporta)
_MOTORES["fogo_rifle"] = r'''# gera o FBX novo do rifle (rodada 2; pelo pegada_fogo.py: -- Rifle <saida.fbx> [extra.json] [so=Reload] [provar=...] [varios=...];
# sem extra.json, o params2 de ENTRADAS)
# 1) carrega o original com a mão nova e a arma do jogo (para o contato com a malha), calcula as pegadas e a pose de
#    cada quadro de cada clipe; 2) importa o original de novo, grava os clipes (os mesmos nomes e quadros) e exporta.
# Rodada 2 (o que muda em relação a gerar.py, quando P["trans2"] existe):
#  - a passagem da esquerda na recarga (sair do guarda-mão para o carregador e voltar): a mão vai da pegada para a
#    orientação do original (a palma virada para a arma, de quina para a câmera), e o COTOVELO é procurado (o giro do
#    polo do IK) para o antebraço ficar alinhado com a mão, em vez de girar a mão (que virava a palma para a câmera);
#  - os dedos na passagem: o "abrir" some logo (os dedos juntos) e a dobra vai para a do original (o "C");
#  - a direita na recarga: o indicador sai do gatilho e fica reto ao longo da caixa, acima do guarda-mato (P["R"]["dedo_fora"]).
import os, sys, json, math, time, traceback
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import bpy
from mathutils import Vector, Matrix, Euler
import fogo_rifle_pose as rp
import pegada_fogo as pf
fc, ag, dm, pg = rp.fc, rp.ag, rp.dm, pf.pg
ORIGINAL = pf.original_fbx("Rifle")      # (o pegada_fogo.py confere que é o original)


def pesos(ctx, M0s, lado, h_ref, longe, n_sair=3, n_voltar=2):
    """0: a mão segura a arma (a pegada); 1: longe da arma (o caminho de antes)."""
    fs = sorted(M0s)
    fora = {}
    for f in fs:
        h = ctx.W(M0s[f]).inverted() @ M0s[f]["hand." + lado]
        fora[f] = (h.translation - h_ref.translation).length > longe[1]
    ws = {}
    for i, f in enumerate(fs):
        if not fora[f]:
            ws[f] = 0.0
            continue
        n = next((k for k in range(1, len(fs) + 1) if i - k >= 0 and not fora[fs[i - k]]), None)
        m = next((k for k in range(1, len(fs) + 1) if i + k < len(fs) and not fora[fs[i + k]]), None)
        ws[f] = min(1.0, (n / n_sair) if n else 1.0, (m / n_voltar) if m else 1.0)
    return ws


def dedo_fora(ctx, H, P):
    """O indicador direito fora do gatilho: reto ao longo da caixa, acima do guarda-mão (P["dedo_fora"]: [dy, dz, folga])."""
    dy, dz, folga = P["dedo_fora"]
    g = Vector(P["gatilho"])
    y, z = g.y + dy, g.z + dz
    hit = ctx.bvh.ray_cast(Vector((-0.2, y, z)), Vector((1.0, 0.0, 0.0)), 0.4)
    x = hit[0].x if hit[0] is not None else -0.015
    T = Vector((x - folga, y, z))
    q, txt = ctx.indicador(H, "R", T, P["tol"])
    return q, f"dedo fora: alvo {tuple(round(v, 3) for v in T)}; {txt}"


def calcular(ctx, P, so=None):
    c = ctx.c
    esq = ctx.esq
    grips = {"L": ctx.mao_esquerda(P["L"]), "R": ctx.mao_direita(P["R"])}
    for lado in "LR":
        print(f"PEGADA {lado}: {grips[lado][2]}")
    fora_R = None
    if P["R"].get("dedo_fora"):
        fora_R, txt = dedo_fora(ctx, grips["R"][0], P["R"])
        print("PEGADA R " + txt)
    M_idle = ctx.matrizes("Idle", 1)
    Wi = ctx.W(M_idle).inverted()
    h_ref = {lado: Wi @ M_idle["hand." + lado] for lado in "LR"}
    novas, resumo, todos_pesos = {}, {}, {}
    T2 = P.get("trans2")
    for nome in sorted(c["acoes"]):
        if so and nome not in so:
            continue
        a = c["acoes"][nome]
        f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
        M0s = {f: ctx.matrizes(nome, f) for f in range(f0, f1 + 1)}
        quadros = {f: dict(M0s[f]) for f in M0s}
        resumo[nome] = {}
        ws_lado = {}
        for lado in "LR":
            H, dedos, _ = grips[lado]
            Pl = dict(P[lado])
            Pl.update(P.get("clipes", {}).get(nome, {}).get(lado, {}))
            ws = pesos(ctx, M0s, lado, h_ref[lado], P["longe"])
            ws_lado[lado] = ws
            todos_pesos.setdefault(nome, {})[lado] = {str(f): round(w, 2) for f, w in ws.items()}
            por_quadro = []
            pior = {"dobra": 0, "desvio": 0, "torcao": 0, "alcance": 0, "avanco": 0}
            fs = sorted(M0s)
            novo2 = bool(T2) and lado == "L"
            eul_g = {n: q.to_matrix().to_euler("XYZ") for n, q in dedos.items()}

            def dedos_passagem(M0, w):
                """Os dedos na passagem: o abrir some primeiro (os dedos juntos), a dobra vai para a do original (o C)."""
                s_ab = pf._suave(min(1.0, max(0.0, w - T2.get("abrir_ini", 0.0)) * T2.get("k_abrir", 3.0)))
                s_c = pf._suave(min(1.0, max(0.0, w - T2.get("dobra_ini", 0.0)) * T2.get("k_dobra", 2.2)))
                extra = math.radians(T2.get("mais_dobra", 0.0)) * math.sin(math.pi * min(1.0, w))
                # (soltar: os dedos abrem um pouco logo que a mão larga o guarda-mão, e fecham no C depois)
                extra -= math.radians(T2.get("soltar", 0.0)) * math.sin(math.pi * min(1.0, 1.5 * w))
                dd = {}
                for n, q in dedos.items():
                    q0 = esq.base(M0, n).to_quaternion()
                    if n.startswith("thumb"):
                        dd[n] = q.slerp(q0, s_c)
                        continue
                    eg, eo = eul_g[n], q0.to_matrix().to_euler("XYZ")
                    x = min(math.radians(3.0), eg.x + (eo.x - eg.x) * s_c - extra)
                    y = eg.y + (eo.y - eg.y) * s_c
                    jt = T2.get("juntar", {}).get(n.split(".")[0].rstrip("0123456789"), 0.0) if n[-3] == "1" else 0.0
                    z = eg.z * (1.0 - s_ab) + (eo.z + math.radians(jt)) * s_ab
                    dd[n] = Euler((x, y, z), "XYZ").to_quaternion()
                return dd

            ancoras = {}

            def alvo_mao(f, giro, rot=None):
                """A mão (armadura) que se quer no quadro f, antes do corte do pulso."""
                M0 = M0s[f]
                w = pf._suave(ws[f])
                if novo2 and f in ancoras:
                    # (rodada 2) a passagem: da pegada (no espaço da arma) direto para a mão do quadro longe mais perto
                    # (a âncora, no espaço da arma), afastando-se da arma no meio; a orientação ajustada por rot (graus:
                    # o rolar em volta dos dedos e o arfar), para a palma não virar para a câmera
                    Gn = pf._misturar(H, ancoras[f], w)
                    dvec = Vector(T2.get("afasta_dir", (1.0, 0.0, -1.0))).normalized()
                    Gn.translation = Gn.translation + dvec * (T2.get("afasta", 0.04) * math.sin(math.pi * ws[f]))
                    Hw = ctx.W(M0) @ Gn
                    if rot:
                        Hw = Hw @ rp.Ry(rot[0]) @ rp.Rx(rot[1])
                    return Hw
                Hg = H
                if giro and lado == "L":
                    C = Vector((0.0, 0.0, P["L"]["eixo_z"]))
                    Hg = Matrix.Translation(C) @ Matrix.Rotation(math.radians(giro), 4, "Y") @ Matrix.Translation(-C) @ H
                Hw = pf._misturar(ctx.W(M0) @ Hg, M0["hand." + lado], w)
                if novo2 and ws[f] >= 1.0 and T2.get("rolar_longe"):
                    # (longe da arma: a mão do original girada em volta dos dedos, para a palma virar para a arma)
                    fl = [k for k in sorted(ws) if ws[k] >= 1.0]
                    na, nv = T2.get("rolar_rampa", (1, 1))
                    # (a rampa: o giro chega aos poucos nos na primeiros quadros longe e vai embora nos nv últimos;
                    # cada trecho longe separado conta por si)
                    ini = f
                    while ini - 1 in ws and ws[ini - 1] >= 1.0:
                        ini -= 1
                    fim = f
                    while fim + 1 in ws and ws[fim + 1] >= 1.0:
                        fim += 1
                    fator = min(1.0, (f - ini + 1) / float(na), (fim - f + 1) / float(nv))
                    Hw = Hw @ rp.Ry(T2["rolar_longe"] * fator)
                if novo2 and ws[f] >= 1.0 and T2.get("longe_ponto"):
                    # (e um pouco para fora da arma, para a palma não entrar no carregador)
                    Hw.translation = Hw.translation + ctx.W(M0).to_3x3() @ Vector(T2["longe_ponto"])
                if novo2 and 0.0 < w and T2.get("mix_orig", 1.0) < 1.0:
                    # (a mão leva a orientação da pegada consigo e só vira mix_orig do caminho até a do original)
                    Hr = pf._misturar(ctx.W(M0) @ Hg, M0["hand." + lado], w * T2["mix_orig"])
                    Hr.translation = Hw.translation
                    Hw = Hr
                if 0.0 < w < 1.0:
                    if novo2 and T2.get("afasta_dir"):
                        d = (ctx.W(M0).to_3x3() @ Vector(T2["afasta_dir"])).normalized()
                    else:
                        d = (ctx.W(M0).to_3x3() @ (H.to_3x3() @ Vector((0.0, 0.0, 1.0)))).normalized()
                    am = T2.get("afasta", P.get("afasta", 0.04)) if novo2 else P.get("afasta", 0.04)
                    Hw.translation = Hw.translation + d * (am * math.sin(math.pi * w))
                return Hw

            def dedos_do_quadro(f):
                M0 = M0s[f]
                if novo2 and ws[f] > 0:
                    return dedos_passagem(M0, ws[f])
                dd = {}
                wd = pf._suave(min(1.0, 1.7 * ws[f]))
                for n, q in dedos.items():
                    q0 = esq.base(M0, n).to_quaternion()
                    dd[n] = q.slerp(q0, wd) if wd > 0 else q
                if lado == "R" and fora_R and nome == "Reload" and "L" in ws_lado:
                    # (o dedo sai do gatilho depressa, quando a esquerda já largou: no meio do caminho ele cortaria o guarda-mato)
                    k = pf._suave(min(1.0, max(0.0, ws_lado["L"][f] - P["R"].get("dedo_fora_ini", 0.0)) * P["R"].get("dedo_fora_k", 1.0)))
                    if k > 0:
                        for n, q in fora_R.items():
                            dd[n] = dd[n].slerp(q, k)
                return dd

            def config(f, polo):
                Pg = dict(Pl)
                if "cotovelo_inicio" in Pl:
                    qa, qb = Pl["ate"]
                    k = pf._suave(max(0.0, min(1.0, (qb - f) / float(qb - qa))))
                    Pg["cotovelo"] = tuple(a + k * b for a, b in zip(Pl["cotovelo"], Pl["cotovelo_inicio"]))
                if polo:
                    Pg["giro"] = Pg.get("giro", 0.0) + polo
                return Pg

            def resolver(f, giro, tor_antes, M=None, polo=0.0, cortar=True, rot=None):
                M0 = M0s[f]
                M = dict(M0) if M is None else M
                Hw = alvo_mao(f, giro, rot)
                dd = dedos_do_quadro(f)
                Pg = config(f, polo)
                av = ctx.braco(M, M0, lado, Hw, Pg, dd)
                tor = None
                if ws[f] > 0 and cortar:
                    R, tor = pf._no_limite(esq, M, lado, P["no_caminho"], tor_antes)
                    H2 = R.to_4x4()
                    H2.translation = Hw.translation
                    av = ctx.braco(M, M0, lado, H2, Pg, dd)
                return M, av, tor

            def custo_pulso(M):
                d, dv, t = dm.pulso(esq, M, lado)
                l_d, l_dv, l_t = P.get("clipes", {}).get(nome, {}).get("lim_custo", P.get("lim_custo", (53.0, 28.0, 75.0)))
                return 2.0 * max(0.0, abs(d) - l_d) + 3.0 * max(0.0, abs(dv) - l_dv) + 1.5 * max(0.0, abs(t) - l_t)

            # 1) o giro da mão esquerda em volta do guarda-mão em cada quadro de segurar; 2) alisado no tempo
            giros = {}
            for f in fs:
                melhor = None
                livre = P.get("giro_livre", True) and lado == "L" and ws[f] < 1.0 and not (novo2 and ws[f] > 0)
                for g in (range(-40, 41, 5) if livre else (0,)):
                    M, av, _ = resolver(f, g, None)
                    cg = custo_pulso(M) + 0.5 * abs(g) + 300.0 * av
                    if melhor is None or cg < melhor[0]:
                        melhor = (cg, g)
                giros[f] = float(melhor[1])
            laco = nome in ("Idle", "Walk", "Run")
            presos = set() if laco else ({fs[-1]} if nome == "Sacar" else {fs[0], fs[-1]})
            for f in presos:
                giros[f] = 0.0
            for _ in range(2):
                g2 = {}
                for i, f in enumerate(fs):
                    viz = [giros[fs[(i + k) % len(fs)]] if laco else giros[fs[max(0, min(len(fs) - 1, i + k))]] for k in (-1, 0, 1)]
                    g2[f] = 0.0 if f in presos else sum(viz) / 3.0
                giros = g2
            # 3) (rodada 2) fora da pegada, o cotovelo procurado para o antebraço seguir a mão (o pulso reto sem girar a mão)
            polos = {f: 0.0 for f in fs}
            rots = {f: None for f in fs}
            if novo2 and any(0 < ws[f] < 1 for f in fs):
                longe_fs = [f for f in fs if ws[f] >= 1.0]
                for f in fs:
                    if 0 < ws[f] < 1 and longe_fs:
                        fa = min(longe_fs, key=lambda k: abs(k - f))
                        Ma, _, _ = resolver(fa, giros[fa], None)
                        ancoras[f] = ctx.W(M0s[fa]).inverted() @ Ma["hand." + lado]
                for f in fs:
                    if f not in ancoras:
                        continue
                    melhor = None
                    for rr in range(-T2.get("rolar_lim", 60), T2.get("rolar_lim", 60) + 1, 15):
                        for aa in range(-T2.get("arfar_lim", 45), T2.get("arfar_lim", 45) + 1, 15):
                            for g in range(-T2.get("polo_passagem", 60), T2.get("polo_passagem", 60) + 1, 15):
                                M, av, _ = resolver(f, giros[f], None, polo=g, cortar=False, rot=(rr, aa))
                                d, dv, t = dm.pulso(esq, M, lado)
                                Hm = M["hand." + lado]
                                E = M["forearm." + lado].translation
                                cam = -Hm.translation.normalized()
                                pc = -(Hm.to_3x3() @ Vector((0.0, 0.0, 1.0))).dot(cam)
                                dc = (Hm.to_3x3() @ Vector((0.0, 1.0, 0.0))).dot(cam)
                                cg = (3.0 * max(0.0, abs(d) - 50) + 4.0 * max(0.0, abs(dv) - 20) + 2.0 * max(0.0, abs(t) - 80)
                                      + 0.03 * (abs(d) + abs(dv) + 0.3 * abs(t))
                                      + T2.get("peso_palma", 80.0) * max(0.0, pc - T2.get("palma_max", 0.12))
                                      + T2.get("peso_dedos", 40.0) * max(0.0, dc - T2.get("dedos_max", -0.5))
                                      + 0.12 * (abs(rr) + abs(aa)) + 0.05 * abs(g)
                                      + 400.0 * max(0.0, E.z - (Hm.translation.z - T2.get("cot_abaixo", 0.04))) + 300.0 * av)
                                if melhor is None or cg < melhor[0]:
                                    melhor = (cg, rr, aa, g, pc, dc, d, dv, t)
                    rots[f] = (melhor[1], melhor[2])
                    polos[f] = float(melhor[3])
                    print(f"PASSAGEM {nome} q{f}: rolar {melhor[1]} arfar {melhor[2]} polo {melhor[3]} | palma.cam {melhor[4]:+.2f} dedos.cam {melhor[5]:+.2f} | pulso {melhor[6]:.0f},{melhor[7]:.0f},{melhor[8]:.0f}")
            if novo2 and T2.get("polo_lim", 0) and any(ws[f] >= 1 for f in fs):
                lim = T2.get("polo_lim", 90)
                for f in fs:
                    if ws[f] <= 0:
                        continue
                    melhor = None
                    if f in ancoras:
                        continue
                    for g in range(-lim, lim + 1, 10):
                        M, av, _ = resolver(f, giros[f], None, polo=g, cortar=False)
                        d, dv, t = dm.pulso(esq, M, lado)
                        E, Wp = M["forearm." + lado].translation, M["hand." + lado].translation
                        cg = (2.0 * max(0.0, abs(d) - 45) + 3.0 * max(0.0, abs(dv) - 18) + 1.5 * max(0.0, abs(t) - 70)
                              + 0.05 * (abs(d) + abs(dv) + 0.5 * abs(t)) + 400.0 * max(0.0, E.z - (Wp.z - T2.get("cot_abaixo", 0.04)))
                              + T2.get("peso_polo", 0.08) * abs(g) * (1.0 - 0.5 * ws[f]) + 300.0 * av)
                        if melhor is None or cg < melhor[0]:
                            melhor = (cg, g)
                    polos[f] = float(melhor[1])
                for _ in range(T2.get("alisar_polo", 2)):
                    p2 = {}
                    for i, f in enumerate(fs):
                        if ws[f] <= 0 or f in ancoras:
                            p2[f] = polos[f]
                            continue
                        viz = [polos[fs[max(0, min(len(fs) - 1, i + k))]] for k in (-1, 0, 1)]
                        p2[f] = sum(viz) / 3.0
                    polos = p2
            tor_antes = None
            tela = []
            for f in fs:
                M, av, tor_antes = resolver(f, giros[f], tor_antes, quadros[f], polo=polos[f], rot=rots[f])
                d, dv, t = dm.pulso(esq, M, lado)
                por_quadro.append(f"{f}:{ws[f]:.1f}/{d:.0f},{dv:.0f},{t:.0f}/g{giros[f]:.0f}" + (f"/p{polos[f]:.0f}" if polos[f] else ""))
                if novo2 and ws[f] > 0 or (lado == "L" and nome == "Reload" and f in (4, 5, 41, 42, 43)):
                    Hm = M["hand." + lado]
                    palma = -(Hm.to_3x3() @ Vector((0.0, 0.0, 1.0)))
                    cam = -Hm.translation.normalized()
                    tela.append(f"{f}: palma.cam {palma.dot(cam):+.2f} mão {rp.na_tela(Hm.translation)} cot {rp.na_tela(M['forearm.' + lado].translation)}")
                S = M["upper_arm." + lado].translation
                alc = (M["hand." + lado].translation - S).length / dm._alcance_max(esq, lado)
                for k, v in (("dobra", abs(d)), ("desvio", abs(dv)), ("torcao", abs(t)), ("alcance", alc), ("avanco", av * 1000)):
                    pior[k] = max(pior[k], v)
            longe = [f for f, w in ws.items() if w > 0]
            resumo[nome][lado] = {k: round(v, 2 if k == "alcance" else 0) for k, v in pior.items()}
            print(f"CLIPE {nome:8s} {lado}: dobra {pior['dobra']:.0f} desvio {pior['desvio']:.0f} torção {pior['torcao']:.0f} | alcance {pior['alcance']:.2f}"
                  f" avanço do ombro {pior['avanco']:.0f} mm | fora da pegada: {(min(longe), max(longe)) if longe else '-'}")
            if lado == "L" and nome in ("Run", "Sacar", "Reload"):
                print(f"QUADROS {nome} {lado} (quadro:peso/dobra,desvio,torção): " + " ".join(por_quadro))
            if tela:
                print(f"TELA {nome} {lado}: " + " | ".join(tela))
        novas[nome] = quadros
    return novas, grips, resumo, todos_pesos


def exportar(novas, caminho):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = ag.importar(ORIGINAL)
    rig = next(o for o in objs if o.type == "ARMATURE")
    bracos = next(o for o in objs if o.type == "MESH" and o.name.startswith("FP_Bracos"))
    acoes = {a.name.split("|", 1)[1]: a for a in bpy.data.actions if a.name.startswith(rig.name + "|")}
    rig.animation_data_create()
    fps = bpy.context.scene.render.fps
    esq = ag.Esqueleto(rig)
    for nome, quadros in novas.items():
        a = acoes[nome]
        nome_acao = a.name
        nova = pg._gravar(rig, esq, nome_acao + "_nova", quadros)
        bpy.data.actions.remove(a)
        nova.name = nome_acao
        acoes[nome] = nova
    boca = next(o for o in bpy.data.objects if o.name.startswith("Boca_FP"))
    bpy.context.scene.render.fps, bpy.context.scene.render.fps_base = fps, 1.0
    pf.exportar(rig, bracos, boca, acoes, caminho)


def provar(ctx, novas, pasta, quadros, recorte=None, w=1280, h=720):
    """Fotos da câmera do jogo das poses calculadas (sem exportar): <pasta>/p_<Clipe>_<f>.png"""
    os.makedirs(pasta, exist_ok=True)
    for nome, f in quadros:
        if nome not in novas:
            continue
        ctx.aplicar(novas[nome][f])
        dg = bpy.context.evaluated_depsgraph_get()
        k = fc.contato(ctx.c, "L", dg, fc._bvh_arma(ctx.c, dg))
        print(f"PROVA_CONTATO {nome} q{f}: {k['dentro_mm']} ({k['dentro_osso']})")
        cam = os.path.join(pasta, f"p_{nome}_{f:03d}.png")
        rp.foto_olho(cam, w, h)


def principal():
    args = sys.argv[sys.argv.index("--") + 1:]
    try:
        t0 = time.time()
        eh_opc = lambda a: "=" in a and a.split("=", 1)[0] in ("so", "provar", "varios")  # noqa: E731
        opc = {a.split("=", 1)[0]: a.split("=", 1)[1] for a in args if eh_opc(a)}
        pos = [a for a in args if not eh_opc(a)]
        extra = json.load(open(pos[1], encoding="utf-8")) if len(pos) > 1 else json.loads(json.dumps(pf.entrada("Rifle")["params2"]))
        P = rp.parametros(extra)
        print("PARAMETROS", json.dumps(P))
        c = fc.carregar("Rifle", ORIGINAL)
        ctx = rp.Ctx(c)
        so = opc["so"].split(",") if "so" in opc else None
        if "varios" in opc:
            # várias provas numa chamada: cada JSON (juntado ao extra) -> <pasta da saída>/<nome do json>/p_*.png
            for arq in opc["varios"].split(","):
                nomej = os.path.splitext(os.path.basename(arq))[0]
                Pj = rp.juntar(P, json.load(open(arq, encoding="utf-8")))
                print(f"=== PROVA {nomej}")
                nv, _, _, _ = calcular(ctx, Pj, so)
                qs = []
                for item in opc["provar"].split(","):
                    nome, fr = item.split(":")
                    a, b = (fr.split("-") + [fr])[:2]
                    qs += [(nome, f) for f in range(int(a), int(b) + 1)]
                provar(ctx, nv, os.path.join(os.path.dirname(pos[0]), nomej), qs)
            print(f"VARIOS OK {time.time() - t0:.1f} s")
            sys.exit(0)
        novas, grips, resumo, todos_pesos = calcular(ctx, P, so)
        if "provar" in opc:
            qs = []
            for item in opc["provar"].split(","):
                nome, fr = item.split(":")
                a, b = (fr.split("-") + [fr])[:2]
                qs += [(nome, f) for f in range(int(a), int(b) + 1)]
            provar(ctx, novas, os.path.dirname(pos[0]), qs)
            print(f"PROVAR OK {time.time() - t0:.1f} s")
        else:
            exportar(novas, pos[0])
            with open(os.path.splitext(pos[0])[0] + "_params.json", "w", encoding="utf-8") as fh:
                json.dump({"parametros": P, "pegadas": {l: grips[l][2] for l in "LR"}, "clipes": resumo, "pesos": todos_pesos}, fh, ensure_ascii=False, indent=1)
            print(f"GERAR OK {time.time() - t0:.1f} s")
    except Exception:
        traceback.print_exc()
        print("GERAR FALHOU")
        sys.exit(1)
'''

# ---------------------------------------------------------------------------------------------------------------------
# RIFLE: o rifle_pose.py (a pose das mãos no espaço da arma, o braço por IK)
_MOTORES["fogo_rifle_pose"] = r'''# A pegada do rifle (M4) na primeira pessoa, rodada 1 da segunda tentativa (11/11/2026).
# A pose das mãos é escrita à mão no espaço da arma (o cano em -Y, o alto em +Z, a esquerda da arma em +X):
#  - a esquerda em concha por baixo/à esquerda do guarda-mão: a palma encostada (empurrada até tocar a malha da arma),
#    os dedos fechando falange a falange até encostar na malha (dão a volta por baixo e sobem pelo lado direito), o
#    polegar deitado no lado esquerdo, apontando para a boca;
#  - a direita no punho de pistola: a palma no lado direito/atrás, os três dedos fechando em volta da frente do punho,
#    o indicador no gatilho, o polegar dando a volta pelo lado esquerdo;
#  - o braço por IK (ag.braco: metade da torção no antebraço), o cotovelo perto do cotovelo original (mais um
#    deslocamento fixo), para o antebraço entrar na tela como antes.
# Os parâmetros ficam em PADRAO (e podem vir de um JSON, para experimentar).
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

AQUI = os.path.dirname(os.path.abspath(__file__))
if AQUI not in sys.path:
    sys.path.insert(0, AQUI)
import pegada_fogo_conferir as fc  # noqa: E402
import pegada_fogo as pf  # noqa: E402

ag, dm, bc, mn = fc.ag, fc.dm, fc.bc, fc.mn
DEDOS = ("index", "middle", "ring", "pinky")
K_TELA = 360.0 / math.tan(math.radians(27.5))      # (a câmera do jogo: 55 graus na vertical, 1280x720)

PADRAO = {
    "L": {
        "eixo_z": 0.105, "raio": 0.036,     # o guarda-mão: o eixo em x = 0, z = 0.105; o raio
        "y": -0.345,                        # onde o meio da palma encosta (ao longo do cano)
        "theta": 40.0,                      # a palma em volta do cano: 0 = embaixo, 90 = no lado esquerdo
        "phi": 45.0,                        # a diagonal: 0 = a mão atravessada, 90 = ao longo do cano (dedos para a boca)
        "incl": 0.0, "rolar": 0.0, "guinar": 0.0,   # ajustes finos da mão (graus, nos eixos X, Y e Z dela)
        "palma": (0.0, 0.062),              # o ponto da palma (x, y no espaço do hand) que encosta
        "tol": 0.0012,                      # o quanto a pele pode entrar na arma (m)
        "a1max": 84, "a2max": 100,
        "polegar_dir": (0.25, -1.0, 0.35),  # para onde o polegar aponta (no espaço da arma)
        "ombro": (0.0, 0.0, 0.0),           # o deslocamento do ombro (m, armadura)
        "cotovelo": (0.0, 0.0, 0.0),        # o do cotovelo original (o alvo do polo do IK)
    },
    "R": {
        "ponto": (0.0, -0.001, -0.040), "eixo": (0.0, -0.48, 1.0),   # o punho: um ponto do eixo e a direção (para cima)
        "psi": 20.0,                        # a palma em volta do punho: 0 = no lado direito, 90 = atrás
        "phi": 0.0,                         # a diagonal (0: os nós dos dedos ao longo do punho)
        "incl": 0.0, "rolar": 0.0, "guinar": 0.0,
        "raio": 0.016,
        "palma": (0.0, 0.070),
        "tol": 0.0012,
        "a1max": 84, "a2max": 100,
        "gatilho": (0.0, -0.075, 0.030),
        "polegar_dir": (0.6, -1.0, -0.2),
        "ombro": (0.0, 0.0, 0.0),
        "cotovelo": (0.0, 0.0, 0.0),
    },
    "longe": (0.03, 0.08),
    "no_caminho": (55.0, 22.0, 90.0),
}


def Rx(g):
    return Matrix.Rotation(math.radians(g), 4, "X")


def Ry(g):
    return Matrix.Rotation(math.radians(g), 4, "Y")


def Rz(g):
    return Matrix.Rotation(math.radians(g), 4, "Z")


def dobra(a):
    return Matrix.Rotation(math.radians(-a), 4, "X")


def juntar(base, extra):
    out = json.loads(json.dumps(base))
    for k, v in (extra or {}).items():
        if isinstance(v, dict) and isinstance(out.get(k), dict):
            out[k].update(v)
        else:
            out[k] = v
    return out


def parametros(extra=None):
    """PADRAO + params (os escolhidos: o params.json, em ENTRADAS do pegada_fogo.py) + extra."""
    P = juntar(PADRAO, json.loads(json.dumps(pf.entrada("Rifle")["params"])))
    return juntar(P, extra)


def na_tela(p):
    """O ponto (no espaço da armadura = o do mundo) em pixels de 1280x720 da câmera do jogo."""
    d = -p.y
    if d < 1e-4:
        return None
    return (round(640 + (-p.x / d) * K_TELA), round(360 - (p.z / d) * K_TELA))


class Ctx:
    def __init__(self, c):
        self.c = c
        rig = c["rig"]
        self.rig, self.esq = rig, c["esq"]
        self.rest, self.pai = self.esq.rest, self.esq.pai
        self.E = rig.matrix_world.inverted() @ c["E"]       # o espaço da arma solta -> a armadura (no repouso)
        Ei = self.E.inverted()
        vs, fs = [], []
        for nome, o in c["arma"].items():
            base = len(vs)
            M = Ei @ rig.matrix_world.inverted() @ o.matrix_world
            vs += [M @ v.co for v in o.data.vertices]
            fs += [[base + i for i in p.vertices] for p in o.data.polygons]
        self.bvh = BVHTree.FromPolygons(vs, fs)
        self.verts = vs
        self.pts = {}
        for lado, o in c["maos"].items():
            nomes = {g.index: g.name for g in o.vertex_groups}
            M = rig.matrix_world.inverted() @ o.matrix_world
            for v in o.data.vertices:
                if not len(v.groups):
                    continue
                g = max(v.groups, key=lambda g: g.weight)
                n = nomes[g.group]
                if n in self.rest and not n.startswith(("forearm", "upper_arm")):
                    self.pts.setdefault(n, []).append(self.rest[n].inverted() @ (M @ v.co))

    def rel(self, n):
        return self.rest[self.pai[n]].inverted() @ self.rest[n]

    def W(self, M):
        """O espaço da arma -> a armadura, na pose M."""
        return M["gun"] @ self.rest["gun"].inverted() @ self.E

    def sd(self, p):
        """A distância (m) do ponto (no espaço da arma) à malha da arma: negativa dentro dela."""
        loc, nor, _, dist = self.bvh.find_nearest(p, 0.08)
        if loc is None:
            return 0.08
        if (p - loc).dot(nor) < 0 and bc._dentro(self.bvh, p, 0.08)[0]:
            return -dist
        return dist

    def pior(self, M, ps):
        return min(self.sd(M @ p) for p in ps) if ps else 0.08

    # ------------------------------------------------------------------------------------------ os dedos
    def fechar(self, H, lado, dedo, tol, a1max, a2max, a1min=0, a2min=0, abrir=0.0):
        """Fecha o dedo falange a falange até a pele encostar na arma (ou até o máximo). Devolve (a1, a2).
        abrir: o dedo virado para o lado (graus, em volta do Z dele) antes de fechar."""
        n1, n2 = f"{dedo}1.{lado}", f"{dedo}2.{lado}"
        R1, R2 = self.rel(n1) @ Rz(abrir), self.rel(n2)
        p1 = [p for p in self.pts.get(n1, []) if p.y > 0.010]
        p2 = [p for p in self.pts.get(n2, []) if p.y > 0.008]
        a1 = a1min
        for a in range(a1min, a1max + 1, 2):
            if self.pior(H @ R1 @ dobra(a), p1) < -tol:
                break
            a1 = a
        M1 = H @ R1 @ dobra(a1)
        a2 = a2min
        for a in range(a2min, a2max + 1, 2):
            if self.pior(M1 @ R2 @ dobra(a), p2) < -tol:
                break
            a2 = a
        return a1, a2

    def polegar(self, H, lado, alvo, tol, passo=10, lim=70):
        """O polegar deitado na arma, apontando para alvo (direção no espaço da arma): a busca nos giros do thumb e na
        dobra do thumb2, sem entrar na arma e com a almofada o mais perto possível dela."""
        n1, n2 = "thumb." + lado, "thumb2." + lado
        R1, R2 = self.rel(n1), self.rel(n2)
        p1 = [p for p in self.pts.get(n1, []) if p.y > 0.012]
        p2 = [p for p in self.pts.get(n2, []) if p.y > 0.006]
        alvo = Vector(alvo).normalized()
        melhor = None
        for az in range(-lim, lim + 1, passo):
            for ax in range(-lim, lim + 1, passo):
                for ay in (-40, -20, 0, 20, 40):
                    Q1 = Rz(az) @ Rx(ax) @ Ry(ay)
                    M1 = H @ R1 @ Q1
                    s1 = self.pior(M1, p1)
                    if s1 < -tol:
                        continue
                    for a2 in (0, 10, 20, 30, 45):
                        M2 = M1 @ R2 @ dobra(a2)
                        s2 = self.pior(M2, p2)
                        if s2 < -tol:
                            continue
                        d2 = (M2.to_3x3() @ Vector((0.0, 1.0, 0.0))).normalized()
                        d1 = (M1.to_3x3() @ Vector((0.0, 1.0, 0.0))).normalized()
                        custo = (math.degrees(d2.angle(alvo)) + 0.4 * math.degrees(d1.angle(alvo))
                                 + 3000.0 * max(0.0, s2) + 1500.0 * max(0.0, s1)
                                 + 0.08 * (abs(az) + abs(ax) + abs(ay)) + 0.1 * a2)
                        if melhor is None or custo < melhor[0]:
                            melhor = (custo, Q1.to_quaternion(), a2, (az, ax, ay, a2), s1, s2, math.degrees(d2.angle(alvo)))
        if melhor is None:
            return {n1: Quaternion(), n2: Quaternion()}, "polegar sem saída"
        _, q1, a2, ang, s1, s2, erro = melhor
        return ({n1: q1, n2: dobra(a2).to_quaternion()},
                f"polegar {ang}: a {s1 * 1000:.1f}/{s2 * 1000:.1f} mm, {erro:.0f}° do alvo")

    def indicador(self, H, lado, T, tol):
        """O indicador no gatilho: a almofada da ponta no ponto T (no espaço da arma), sem entrar na arma."""
        n1, n2 = "index1." + lado, "index2." + lado
        R1, R2 = self.rel(n1), self.rel(n2)
        p1 = [p for p in self.pts.get(n1, []) if p.y > 0.012]
        p2 = [p for p in self.pts.get(n2, []) if p.y > 0.008]
        T = Vector(T)
        melhor = None
        for a1 in range(-10, 81, 5):
            for ab in range(-30, 31, 5):
                Q1 = Rz(ab) @ dobra(a1)
                M1 = H @ R1 @ Q1
                if self.pior(M1, p1) < -tol - 0.002:
                    continue
                for a2 in range(0, 96, 5):
                    M2 = M1 @ R2 @ dobra(a2)
                    almofada = M2 @ Vector((0.0, 0.026, -0.008))
                    dist = (almofada - T).length
                    if melhor is not None and dist > melhor[4] + 0.004:
                        continue
                    pen = self.pior(M2, p2)
                    custo = dist + 0.00005 * (abs(ab) + 0.2 * a1 + 0.2 * a2) + (3.0 * (-pen - tol - 0.003) if pen < -tol - 0.003 else 0.0)
                    if melhor is None or custo < melhor[0]:
                        melhor = (custo, Q1.to_quaternion(), dobra(a2).to_quaternion(), (a1, ab, a2), dist, pen)
        _, q1, q2, ang, dist, pen = melhor
        return {n1: q1, n2: q2}, f"indicador {ang}: a ponta a {dist * 1000:.0f} mm do gatilho, {pen * 1000:.1f} mm da arma"

    def encostar(self, H, n, palma, lado, alvo=-0.0005):
        """Empurra a mão (H, no espaço da arma) ao longo de n (para fora da arma) até a pele da palma encostar."""
        ps = [p for p in self.pts["hand." + lado] if p.z < -0.004 and 0.025 < p.y < 0.105]
        lo, hi = -0.04, 0.06
        for _ in range(18):
            m = (lo + hi) / 2
            Hm = Matrix.Translation(n * m) @ H
            if self.pior(Hm, ps) < alvo:
                lo = m
            else:
                hi = m
        return Matrix.Translation(n * hi) @ H, hi

    # ------------------------------------------------------------------------------------------ as mãos
    def mao_esquerda(self, P):
        H = self.mao_esquerda_H(P)
        return self._dedos_esquerda(H, P)

    def mao_esquerda_H(self, P):
        th, ph = math.radians(P["theta"]), math.radians(P["phi"])
        n = Vector((math.sin(th), 0.0, -math.cos(th)))          # para fora do cano, onde a palma encosta
        t = Vector((-math.cos(th), 0.0, -math.sin(th)))         # em volta do cano, por baixo, para a direita
        fw = Vector((0.0, -1.0, 0.0))
        Y = (math.cos(ph) * t + math.sin(ph) * fw).normalized()
        Z = n
        X = Y.cross(Z)
        R = Matrix((X, Y, Z)).transposed().to_4x4() @ Rz(P["guinar"]) @ Rx(P["incl"]) @ Ry(P["rolar"])
        C = Vector((0.0, P["y"], P["eixo_z"])) + n * P["raio"]
        pc = Vector((P["palma"][0], P["palma"][1], -0.012))
        H = R.copy()
        H.translation = C - R.to_3x3() @ pc
        H, s = self.encostar(H, n, P["palma"], "L")
        self.empurrada = s
        return H

    def _dedos_esquerda(self, H, P):
        dedos, info = {}, [f"palma empurrada {self.empurrada * 1000:.1f} mm"]
        for dd in DEDOS:
            ab = P.get("abrir", {}).get(dd, 0.0) if isinstance(P.get("abrir"), dict) else P.get("abrir", 0.0)
            a1, a2 = self.fechar(H, "L", dd, P["tol"], P["a1max"], P["a2max"], abrir=ab)
            a1 += P.get("mais1", {}).get(dd, 0)
            a2 += P.get("mais2", {}).get(dd, 0)
            dedos[f"{dd}1.L"], dedos[f"{dd}2.L"] = (Rz(ab) @ dobra(a1)).to_quaternion(), dobra(a2).to_quaternion()
            info.append(f"{dd} {a1}/{a2}")
        if "polegar" in P:
            az, ax, ay, a2 = P["polegar"]
            dedos["thumb.L"], dedos["thumb2.L"] = (Rz(az) @ Rx(ax) @ Ry(ay)).to_quaternion(), dobra(a2).to_quaternion()
        else:
            q, txt = self.polegar(H, "L", P["polegar_dir"], P["tol"])
            dedos.update(q)
            info.append(txt)
        return H, dedos, "; ".join(info)

    def mao_direita(self, P):
        H = self.mao_direita_H(P)
        return self._dedos_direita(H, P)

    def mao_direita_H(self, P):
        a = Vector(P["eixo"]).normalized()                       # o punho, para cima
        dir0 = Vector((-1.0, 0.0, 0.0))                          # o lado direito da arma
        tras = a.cross(dir0).normalized()                        # para trás (perpendicular ao punho)
        if tras.y < 0:
            tras = -tras
        ps = math.radians(P["psi"])
        n = (math.cos(ps) * dir0 + math.sin(ps) * tras).normalized()     # para fora do punho, onde a palma encosta
        t = (-math.sin(ps) * dir0 + math.cos(ps) * tras)                 # em volta do punho, para trás
        Yp = -t                                                          # os dedos vão para a frente
        ph = math.radians(P["phi"])
        Y = (math.cos(ph) * Yp + math.sin(ph) * a).normalized()
        Z = n
        X = Y.cross(Z)
        R = Matrix((X, Y, Z)).transposed().to_4x4() @ Rz(P["guinar"]) @ Rx(P["incl"]) @ Ry(P["rolar"])
        C = Vector(P["ponto"]) + n * P["raio"]
        pc = Vector((P["palma"][0], P["palma"][1], -0.012))
        H = R.copy()
        H.translation = C - R.to_3x3() @ pc
        H, s = self.encostar(H, n, P["palma"], "R")
        self.empurrada = s
        return H

    def _dedos_direita(self, H, P):
        dedos, info = {}, [f"palma empurrada {self.empurrada * 1000:.1f} mm"]
        for dd in DEDOS[1:]:
            ab = P.get("abrir", {}).get(dd, 0.0) if isinstance(P.get("abrir"), dict) else P.get("abrir", 0.0)
            a1, a2 = self.fechar(H, "R", dd, P["tol"], P["a1max"], P["a2max"], abrir=ab)
            a1 += P.get("mais1", {}).get(dd, 0)
            a2 += P.get("mais2", {}).get(dd, 0)
            dedos[f"{dd}1.R"], dedos[f"{dd}2.R"] = (Rz(ab) @ dobra(a1)).to_quaternion(), dobra(a2).to_quaternion()
            info.append(f"{dd} {a1}/{a2}")
        q, txt = self.indicador(H, "R", P["gatilho"], P["tol"])
        dedos.update(q)
        info.append(txt)
        if "polegar" in P:
            az, ax, ay, a2 = P["polegar"]
            dedos["thumb.R"], dedos["thumb2.R"] = (Rz(az) @ Rx(ax) @ Ry(ay)).to_quaternion(), dobra(a2).to_quaternion()
        else:
            q, txt = self.polegar(H, "R", P["polegar_dir"], P["tol"])
            dedos.update(q)
            info.append(txt)
        return H, dedos, "; ".join(info)

    # ------------------------------------------------------------------------------------------ o braço
    def braco(self, M, M0, lado, Hw, P, dedos, cot=None):
        """A mão em Hw (armadura), o ombro original + P["ombro"], o cotovelo para o lado do original + P["cotovelo"]."""
        ua = "upper_arm." + lado
        Ma = dict(M0)
        m = Ma[ua].copy()
        m.translation = m.translation + Vector(P["ombro"])
        Ma[ua] = m
        alvo = (cot if cot is not None else M0["forearm." + lado].translation + Vector(P["cotovelo"]))
        polo = alvo - m.translation
        if cot is None and P.get("giro"):
            u = (Hw.translation - m.translation).normalized()
            polo = Matrix.Rotation(math.radians(P["giro"]), 3, u) @ polo
        return dm.braco_no_lugar(self.esq, M, Ma, lado, Hw, polo, dedos)

    def aplicar(self, M):
        """Põe a pose M (as matrizes na armadura) no rig, sem ação (para as fotos de prova)."""
        rig = self.rig
        if rig.animation_data:
            rig.animation_data.action = None
        for n in self.esq.ordem:
            pb = rig.pose.bones[n]
            pb.rotation_mode = "QUATERNION"
            loc, rot, esc = self.esq.base(M, n).decompose()
            pb.location, pb.rotation_quaternion, pb.scale = loc, rot, esc
        bpy.context.view_layer.update()

    def matrizes(self, nome, f):
        ag.usar_acao(self.rig, self.c["acoes"][nome])
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        return fc.matrizes(self.rig)


def foto_olho(caminho, w=1280, h=720):
    ag._render_base(w, h)
    ag._camera("Olho", (0, 0, 0), rot=(math.radians(90), 0, math.radians(180)), angulo_y=55)
    ag._salvar(caminho)


VISTAS = {
    "L": (("camera", None, None), ("lado", Vector((1.0, 0.15, 0.05)), Vector((0, 0, 1))),
          ("cima", Vector((0.15, 0.1, 1.0)), Vector((0, -1, 0))), ("baixo", Vector((0.15, 0.1, -1.0)), Vector((0, -1, 0)))),
    "R": (("camera", None, None), ("lado", Vector((-1.0, 0.15, 0.05)), Vector((0, 0, 1))),
          ("cima", Vector((-0.15, 0.1, 1.0)), Vector((0, -1, 0))), ("baixo", Vector((-0.15, 0.1, -1.0)), Vector((0, -1, 0))),
          ("esq", Vector((1.0, 0.15, -0.05)), Vector((0, 0, 1)))),
}


def perto_pose(c, prefixo, w=480, h=360, lados="LR", vistas=None):
    """As mãos de perto na pose atual do rig: da direção da câmera do jogo (com zoom), de lado, de cima e de baixo
    (no espaço da arma)."""
    rig = c["rig"]
    os.makedirs(os.path.dirname(prefixo), exist_ok=True)
    ag._render_base(w, h)
    G = rig.matrix_world @ rig.pose.bones["gun"].matrix @ rig.data.bones["gun"].matrix_local.inverted() @ \
        rig.matrix_world.inverted() @ c["E"]
    R3 = G.to_3x3().normalized()
    saidas = []
    for lado in lados:
        M = rig.matrix_world @ rig.pose.bones["hand." + lado].matrix
        alvo = M @ Vector((0.0, 0.07, -0.02))
        for vista, d, cima in VISTAS[lado]:
            if vistas and vista not in vistas:
                continue
            c["bracos"].hide_render = vista != "camera"
            if vista == "camera":
                pos = Vector((0, 0, 0))
                cam = ag._camera("Perto", pos, alvo=alvo, lente=50)
                cam.data.sensor_fit = "AUTO"
                cam.data.lens = 36.0 * (alvo - pos).length / 0.34
                cam.rotation_euler = mn._olhar(pos, alvo, Vector((0, 0, 1))).to_euler()
            else:
                dd = (R3 @ d).normalized()
                pos = alvo + dd * 0.34
                cam = ag._camera("Perto", pos, alvo=alvo, lente=50)
                cam.rotation_euler = mn._olhar(pos, alvo, (R3 @ cima).normalized()).to_euler()
            caminho = f"{prefixo}_{lado}_{vista}.png"
            ag._salvar(caminho)
            saidas.append(caminho)
    c["bracos"].hide_render = False
    return saidas


def fotos_perto(c, nome, f, prefixo, **k):
    ag.usar_acao(c["rig"], c["acoes"][nome])
    bpy.context.scene.frame_set(f)
    bpy.context.view_layer.update()
    return perto_pose(c, prefixo, **k)


def pose_do_quadro(ctx, P, M0, grips, wl=0.0, wr=0.0):
    """A pose nova de um quadro de segurar (sem a mistura com o caminho de antes: para as provas)."""
    M = dict(M0)
    W = ctx.W(M0)
    for lado in "LR":
        H, dedos, _ = grips[lado]
        ctx.braco(M, M0, lado, W @ H, P[lado], dedos)
    return M


def relato(ctx, M, rotulo=""):
    out = []
    Wi = ctx.W(M).inverted()
    for lado in "LR":
        d, dv, t = dm.pulso(ctx.esq, M, lado)
        tp, ta = fc.torcoes_na_malha(ctx.esq, M, lado)
        E, Wp = M["forearm." + lado].translation, M["hand." + lado].translation
        S = M["upper_arm." + lado].translation
        alc = (Wp - S).length / dm._alcance_max(ctx.esq, lado)
        r3 = lambda v: tuple(round(x, 3) for x in v)  # noqa: E731
        out.append(f"{lado}: dobra {d:.0f} desvio {dv:.0f} torção {t:.0f} (malha: pulso {tp:.0f}, antebraço {ta:.0f}) | "
                   f"alcance {alc:.2f} | cotovelo tela {na_tela(E)} arma {r3(Wi @ E)} | pulso tela {na_tela(Wp)} arma {r3(Wi @ Wp)}")
    print(f"RELATO {rotulo} " + "\n   ".join(out))
'''

# ---------------------------------------------------------------------------------------------------------------------
# SNIPER e RIFLE DE SUCATA (rodada 5): o fazer5.py (a rodada 4: o braço segue a mão, a pinça no ferrolho, o cotovelo no espaço da câmera; e a corrida com a pegada própria do clipe)
_MOTORES["fogo_sniper"] = r'''# Rodada 5 (cópia do fazer4.py com poses5): os ajustes de um clipe só (poses5: "clipes" -> nome), a pegada própria do clipe
# (resolvida de novo, "pegada" com "gira_y"), "pivo_fixo" e "cot_orig_k" (o cotovelo pedido misturado com o do original).
# Rodada 4 (cópia do fazer3.py com poses4/braco4 e o cotovelo pedido no espaço da armadura).
# Rodada 3: a pegada com o braço seguindo a mão (braco3.py).
#  - Em todo quadro a mão é decidida primeiro (no espaço da arma): a pegada (poses.py), o caminho ou a passagem;
#    depois o braço: o antebraço o mais perto do cotovelo do original, dentro do guia do pulso; o ombro onde a torção
#    cabe no guia, sem aparecer na tela.
#  - A esquerda na recarga: a mão do original (o lugar e o giro), os dedos fechados em volta do pente.
#  - A direita no tiro (o ferrolho): a pinça na bola do ferrolho (a mão à direita e abaixo da luneta, o polegar e o
#    indicador fechando na bola, os outros dedos num punho), levada com a bola quadro a quadro.
#  - A passagem: a palma em linha reta no espaço da arma, com um arco para cima e para fora (à direita da arma); os
#    dedos soltam da madeira primeiro e fecham no fim; nenhum dedo entra na arma (abre até sair).
#   (pelo pegada_fogo.py: -- <Sniper|Sucata_Rifle> <saida.fbx>)
import math
import os
import sys
import time
import traceback

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import bpy
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree
import fogo_sniper_lib as lib
import fogo_sniper_poses as poses
import pegada_fogo as pf
import fogo_sniper_fazer as fz
import fogo_sniper_braco as b3

ag, dm, fc, pg = lib.ag, lib.dm, lib.fc, lib.pg
ORIGINAL = fz.ORIGINAL
if os.environ.get("POSE_OVR"):
    import json as _json
    for _arma, _lados in _json.loads(os.environ["POSE_OVR"]).items():
        for _lado, _kv in _lados.items():
            for _k, _v in _kv.items():
                if isinstance(_v, dict) and isinstance(poses.POSES[_arma][_lado].get(_k), dict):
                    poses.POSES[_arma][_lado][_k].update(_v)
                else:
                    poses.POSES[_arma][_lado][_k] = _v
PALMA = Vector((0.0, 0.07, 0.0))
DEDOS = ("index", "middle", "ring", "pinky")
LIM = (55.0, 21.0, 82.0)


def _q(m):
    return m.to_3x3().normalized().to_quaternion()


def _slerp(a, b, t):
    if a.dot(b) < 0:
        b = -b
    return a.slerp(b, t)


def _dobra(a, abrir=0.0):
    return Matrix.Rotation(math.radians(abrir), 4, "Z") @ Matrix.Rotation(math.radians(-a), 4, "X")


class Corpo:
    """O corpo da arma (sem o ferrolho e o carregador, que andam) no espaço da arma solta, para os dedos não entrarem."""

    def __init__(self, c, M, pecas=("corpo",)):
        rig = c["rig"]
        Gi = lib.G_de(c, M).inverted()
        dg = bpy.context.evaluated_depsgraph_get()
        vs, fs = [], []
        for k, o in c["arma"].items():
            if k not in pecas:
                continue
            ev = o.evaluated_get(dg)
            me = ev.to_mesh()
            base = len(vs)
            Mo = Gi @ rig.matrix_world.inverted() @ o.matrix_world
            vs += [Mo @ v.co for v in me.vertices]
            fs += [[base + i for i in p.vertices] for p in me.polygons]
            ev.to_mesh_clear()
        self.bvh = BVHTree.FromPolygons(vs, fs)

    def fundo(self, p):
        loc, nor, _, dist = self.bvh.find_nearest(p, 0.03)
        if loc is None:
            return 0.0
        return dist if (p - loc).dot(nor) < 0 and lib.bc._dentro(self.bvh, p)[0] else 0.0


def bola_do_ferrolho(c, M):
    """O centro da bola (a ponta do cabo) do ferrolho, no espaço da arma: os vértices a menos de 2,2 cm do mais à direita."""
    fer = c["arma"].get("ferrolho")
    if fer is None:
        return None
    rig = c["rig"]
    Gi = lib.G_de(c, M).inverted()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = fer.evaluated_get(dg)
    me = ev.to_mesh()
    Mo = Gi @ rig.matrix_world.inverted() @ fer.matrix_world
    vs = [Mo @ v.co for v in me.vertices]
    ev.to_mesh_clear()
    ext = min(vs, key=lambda p: p.x)
    bola = [p for p in vs if (p - ext).length < 0.022]
    return sum(bola, Vector()) / len(bola)


def resolver3(arma, log=print):
    """A pegada de cada mão (como o fz.resolver) e o que a rodada 3 precisa da cena com a arma do jogo: o corpo da arma
    para os dedos, a pele das mãos, a bola do ferrolho em cada quadro do Fire e a pinça da direita."""
    sol, Erig = fz.resolver(arma, log)
    # (rodada 5) a pegada própria de um clipe ('clipes' -> nome -> 'pegada' no poses5): a mão resolvida de novo (o deslize
    # até encostar, os dedos fechando até a madeira, a busca do polegar) com a pose mudada; 'gira_y' gira os dedos em
    # volta da palma (no plano da palma, em volta do ponto 'palma_em')
    sol_clipe = {}
    for lado in ("L", "R"):
        for nome_c, cc in poses.POSES[arma].get(lado, {}).get("clipes", {}).items():
            if not cc.get("pegada"):
                continue
            salvo = poses.POSES[arma]
            base = dict(salvo[lado])
            base.update(cc["pegada"])
            if base.get("gira_y"):
                Q = Matrix.Rotation(math.radians(base["gira_y"]), 3, Vector(base["palma"]).normalized())
                base["y"] = tuple(Q @ Vector(base["y"]))
                log(f"  PEGADA {nome_c} {lado}: y girado {base['gira_y']} graus -> {lib.v3(Vector(base['y']))}")
            poses.POSES[arma] = {lado: base}
            try:
                sol2, _ = fz.resolver(arma, log)
            finally:
                poses.POSES[arma] = salvo
            sol_clipe[(nome_c, lado)] = sol2[lado]
    c = lib.carregar(arma, ORIGINAL)
    rig, esq = c["rig"], c["esq"]
    M0 = lib.matrizes(rig, c["acoes"]["Idle"], 1)
    ext = {"corpo": Corpo(c, M0), "pele": {l: lib.Pele(c, l) for l in "LR"}, "bola": {}}
    log(f"  ESCALA Erig {lib.v3(Erig.to_scale())}")
    a = c["acoes"]["Fire"]
    for f in range(int(a.frame_range[0]), int(a.frame_range[1]) + 1):
        ext["bola"][f] = bola_do_ferrolho(c, lib.matrizes(rig, a, f))
    cfg = poses.POSES[arma]["R"].get("pinca")
    if cfg:
        # a pinça: o indicador e o polegar (do original no quadro em que ele pega a bola, ou os do poses.py) e os
        # outros dedos num punho; o ponto da pinça (o meio entre as pontas) no espaço da mão
        p1, p2 = cfg.get("punho", (70, 90))
        bases = {}
        for d, ab in (("middle", 0), ("ring", 3), ("pinky", 7)):
            bases[f"{d}1.R"] = _dobra(p1, ab)
            bases[f"{d}2.R"] = _dobra(p2)
        vao = cfg.get("vao", 0.024)

        def ind(a1, a2):
            Mh = {"hand.R": Matrix.Identity(4)}
            Mh["index1.R"] = esq.fk(Mh, "index1.R", _dobra(a1))
            Mh["index2.R"] = esq.fk(Mh, "index2.R", _dobra(a2))
            return Mh["index2.R"] @ Vector((0.0, 0.030, -0.006)), -Mh["index2.R"].to_3x3().col[2].normalized()

        def pol(ab, ro, d1, d2):
            b1, b2 = lib._dobra(d1, -ab, -ro), lib._dobra(d2)
            Mx = {"hand.R": Matrix.Identity(4)}
            Mx["thumb.R"] = esq.fk(Mx, "thumb.R", b1)
            Mx["thumb2.R"] = esq.fk(Mx, "thumb2.R", b2)
            return Mx["thumb2.R"] @ Vector((0.0, 0.026, -0.005)), b1, b2
        tpol = []
        for ab in range(-70, 71, 14):
            for ro in range(-90, 91, 18):
                for d1 in range(-40, 81, 15):
                    for d2 in range(0, 91, 18):
                        tpol.append((pol(ab, ro, d1, d2)[0], ab, ro, d1, d2))
        melhor = None
        fa1, fa2 = cfg.get("faixa_indicador", ((25, 60), (25, 75)))
        for a1 in range(fa1[0], fa1[1] + 1, 5):
            for a2 in range(fa2[0], fa2[1] + 1, 5):
                pi, nz = ind(a1, a2)
                alvo = pi + nz * vao
                for pt, ab, ro, d1, d2 in tpol:
                    e_ = (pt - alvo).length
                    if melhor is None or e_ < melhor[0]:
                        melhor = (e_, ab, ro, d1, d2, a1, a2)
        a1, a2 = melhor[5], melhor[6]
        pi, nz = ind(a1, a2)
        alvo = pi + nz * vao
        melhor = melhor[:5]
        bases["index1.R"], bases["index2.R"] = _dobra(a1), _dobra(a2)
        log(f"  PINCA indicador {a1}/{a2}")
        for passo in (6.0, 3.0, 1.5):
            _, ab0, ro0, d10, d20 = melhor
            for ab in (ab0 - passo, ab0, ab0 + passo):
                for ro in (ro0 - passo, ro0, ro0 + passo):
                    for d1 in (d10 - passo, d10, d10 + passo):
                        for d2 in (d20 - passo, d20, d20 + passo):
                            pt, b1, b2 = pol(ab, ro, d1, max(0.0, d2))
                            e_ = (pt - alvo).length
                            if e_ < melhor[0]:
                                melhor = (e_, ab, ro, d1, max(0.0, d2))
        pt, bases["thumb.R"], bases["thumb2.R"] = pol(*melhor[1:])
        log(f"  PINCA polegar abrir {melhor[1]:.0f} rolar {melhor[2]:.0f} dobra {melhor[3]:.0f}/{melhor[4]:.0f}: ponta a {melhor[0] * 1000:.1f} mm do alvo")
        ext["pinca"] = {"bases": bases, "ponto": (pi + pt) / 2, "abertura": (pi - pt).length,
                        "R": None, "off": Vector(cfg.get("off", (0.0, 0.0, 0.0)))}
        if cfg["y"] == "original":
            y_g = Vector((1.0, 0.0, 0.0))
        elif cfg["y"] == "cotovelo":
            qr = cfg.get("quadro", 13)
            Mr = lib.matrizes(rig, a, qr)
            Gr = lib.G_de(c, Mr)
            yb = (Gr @ ext["bola"][qr] - Mr["forearm.R"].translation).normalized()
            y_g = (Gr.to_3x3().normalized().inverted() @ yb).normalized()
            gir = cfg.get("y_gira", (0.0, 0.0, 0.0))
            y_g = (Matrix.Rotation(math.radians(gir[2]), 3, "Z") @ Matrix.Rotation(math.radians(gir[0]), 3, "X") @ y_g).normalized()
            log(f"  PINCA y {lib.v3(y_g)}")
        else:
            y_g = Vector(cfg["y"])
        if cfg.get("palma") == "eixo":
            # o giro em volta dos dedos que põe o eixo da pinça (do polegar ao indicador) na direção pedida
            R0 = lib.rot_mao(y_g, y_g.orthogonal())
            eixo_h = (pi - pt).normalized()
            dp = Vector(cfg.get("eixo", (0.0, -1.0, 0.3))).normalized()
            melhor_r = max(range(0, 360, 3), key=lambda r: (Matrix.Rotation(math.radians(r), 3, y_g) @ R0 @ eixo_h).dot(dp))
            ext["pinca"]["R"] = Matrix.Rotation(math.radians(melhor_r), 3, y_g) @ R0
            Rg = ext["pinca"]["R"]
            log(f"  PINCA giro {melhor_r}: eixo {lib.v3(Rg @ eixo_h)}, costas da mão para {lib.v3(Rg.col[2])}")
        else:
            ext["pinca"]["R"] = lib.rot_mao(y_g, cfg.get("palma") or y_g.orthogonal())
        log(f"  PINCA abertura {(pi - pt).length * 1000:.0f} mm, ponto {lib.v3((pi + pt) / 2)}")
    ext["sol_clipe"] = sol_clipe
    return sol, Erig, ext


def _px(p):
    d = -p.y
    if d < 0.01:
        return "atrás"
    return f"({640 + 640 * (-p.x / d / dm.TX):.0f},{360 - 360 * (p.z / d / dm.TY):.0f})"


def alvo_cot(cfg, s, M0, G, G_idle, k_orig, w):
    """(rodada 4) o cotovelo pedido: 'pegada' (o do poses no espaço da arma, levado só com o deslocamento da arma),
    'armadura' (um ponto fixo no espaço da armadura, onde fica a câmera, mais 'cotovelo_arm_k' do deslocamento da arma)
    ou o do original; k_orig o mistura com o do original; 'cotovelo_off' o desloca na pegada (espaço da armadura)."""
    lado = cfg["_lado"]
    E0 = M0["forearm." + lado].translation.copy()
    if cfg.get("cotovelo_alvo") == "pegada":
        Ep = G_idle @ s["Eg"] + (G.translation - G_idle.translation)
        E0 = Ep.lerp(E0, k_orig)
    elif cfg.get("cotovelo_alvo") == "armadura":
        dG = G.translation - G_idle.translation
        Ep = Vector(cfg["cotovelo_arm"]) + cfg.get("cotovelo_arm_k", 0.0) * dG
        # (o coice: a arma vem para trás, para a câmera; o cotovelo sobe 'cotovelo_sobe' vezes isso)
        Ep.z += cfg.get("cotovelo_sobe", 0.0) * max(0.0, dG.y)
        # (a corrida: a arma desce; com cotovelo_kz < 0 o cotovelo sobe um pouco em vez de descer junto)
        Ep.z += cfg.get("cotovelo_kz", 0.0) * dG.z
        E0 = Ep.lerp(E0, k_orig)
    if cfg.get("cotovelo_off"):
        E0 = E0 + Vector(cfg["cotovelo_off"]) * (1.0 - w)
    return E0


def fazer(arma, saida, log=print):
    sol, Erig, ext = resolver3(arma, log)
    cfgs = poses.POSES[arma]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = ag.importar(ORIGINAL)
    rig = next(o for o in objs if o.type == "ARMATURE")
    bracos = next(o for o in objs if o.type == "MESH" and o.name.startswith("FP_Bracos"))
    boca = next(o for o in objs if o.name.startswith("Boca_FP"))
    acoes = {a.name.split("|", 1)[1]: a for a in bpy.data.actions if a.name.startswith(rig.name + "|")}
    rig.animation_data_create()
    fps = bpy.context.scene.render.fps
    esq = ag.Esqueleto(rig)
    rest = esq.rest
    corpo = ext["corpo"]
    novas = {}
    dep = os.environ.get("DEPURAR", "")
    for nome in sorted(acoes):
        a = acoes[nome]
        f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
        M0s = {f: lib.matrizes(rig, a, f) for f in range(f0, f1 + 1)}
        quadros = {f: dict(M0s[f]) for f in M0s}
        fs = sorted(M0s)
        Gs = {f: M0s[f]["gun"] @ rest["gun"].inverted() @ Erig for f in fs}
        G_idle = lib.matrizes(rig, acoes["Idle"], 1)["gun"] @ rest["gun"].inverted() @ Erig
        ag.usar_acao(rig, a)
        for lado, s in sol.items():
            cfg = dict(cfgs[lado])
            cfg["_lado"] = lado
            # (rodada 5) os ajustes que valem só num clipe (o 'run': a corrida); os outros clipes ficam como na rodada 4
            if cfg.get("clipes", {}).get(nome):
                cfg.update(cfg["clipes"][nome])
            if (nome, lado) in ext["sol_clipe"]:
                s = ext["sol_clipe"][(nome, lado)]
            mao, fa, ua = "hand." + lado, "forearm." + lado, "upper_arm." + lado
            pele = ext["pele"][lado]
            # quanto a mão do original saiu da pegada (0 segura, 1 no caminho dela), com a passagem em >= 5 quadros
            ws = {}
            for f in fs:
                h = Gs[f].inverted() @ M0s[f][mao]
                d = (h.translation - s["h_ref"].translation).length
                ws[f] = max(0.0, min(1.0, (d - fz.LONGE[0]) / (fz.LONGE[1] - fz.LONGE[0])))
            for _ in range(2):
                for i in range(1, len(fs)):
                    ws[fs[i]] = max(ws[fs[i]], ws[fs[i - 1]] - cfg.get("passo_w", fz.PASSO_W))
                for i in range(len(fs) - 2, -1, -1):
                    ws[fs[i]] = max(ws[fs[i]], ws[fs[i + 1]] - cfg.get("passo_w", fz.PASSO_W))
            pinca = ext.get("pinca") if (lado == "R" and nome == "Fire") else None

            def caminho_g(f):
                """A mão no caminho (espaço da arma) e os dedos."""
                if pinca is not None:
                    R = pinca["R"]
                    if cfg["pinca"]["y"] == "original":
                        # o giro da mão do original (no caminho todo; na passagem, o do quadro mais perto todo no caminho)
                        g = f if ws[f] >= 1.0 - 1e-6 else min((k for k in fs if ws[k] >= 1.0 - 1e-6), key=lambda k: abs(k - f))
                        R = (Gs[g].inverted() @ M0s[g][mao]).to_3x3().normalized()
                        if cfg["pinca"].get("y_gira"):
                            gx, gy, gz = cfg["pinca"]["y_gira"]
                            R = Matrix.Rotation(math.radians(gz), 3, "Z") @ Matrix.Rotation(math.radians(gy), 3, "Y") @ Matrix.Rotation(math.radians(gx), 3, "X") @ R
                    H = R.to_4x4()
                    H.translation = ext["bola"][f] + pinca["off"] - R @ pinca["ponto"]
                    return H, pinca["bases"]
                H = Gs[f].inverted() @ M0s[f][mao]
                bases = {}
                k = cfg.get("punho_caminho_k", 0.0)
                for n in esq.descendentes(mao):
                    q = esq.base(M0s[f], n).to_quaternion()
                    dd = n.split(".")[0]
                    if k and dd[:-1] in DEDOS:
                        alvo = _dobra(75 if dd.endswith("1") else 95).to_quaternion()
                        q = _slerp(q, alvo, k)
                    bases[n] = q.to_matrix().to_4x4()
                return H, bases

            rd = cfg.get("dedos_soltos", (70, 85))
            rel_dedos = {n: _dobra(rd[0] if n.split(".")[0].endswith("1") else rd[1]) for n in s["bases"] if n.split(".")[0][:-1] in DEDOS}
            antes = None
            for f in fs:
                M0, M = M0s[f], quadros[f]
                w = pf._suave(ws[f])
                if ws[f] <= 0:
                    Hg, bases = s["Hg"], dict(s["bases"])
                elif ws[f] >= 1.0 - 1e-6:
                    Hg, bases = caminho_g(f)
                    bases = dict(bases)
                else:
                    Hb, bb = caminho_g(f)
                    if pinca is None and cfg.get("passagem_reta", True):
                        g = min((k for k in fs if ws[k] >= 1.0 - 1e-6), key=lambda k: abs(k - f), default=None)
                        if g is not None:
                            Hb, bb = caminho_g(g)
                    Ha = s["Hg"]
                    A = PALMA
                    P = (Ha @ A).lerp(Hb @ A, w)
                    arco = math.sin(math.pi * w)
                    P += Vector((-cfg.get("fora", 0.0) * (1 if lado == "R" else -1), 0.0, cfg.get("levantar", 0.0))) * arco
                    Rq = _slerp(_q(Ha), _q(Hb), w).to_matrix()
                    Hg = Rq.to_4x4()
                    Hg.translation = P - Rq @ A
                    # os dedos: soltam da pegada (até w 0,4), depois fecham no caminho
                    bases = {}
                    for n, b in s["bases"].items():
                        qa = b.to_quaternion()
                        qb = bb.get(n, b).to_quaternion()
                        qr = rel_dedos[n].to_quaternion() if n in rel_dedos else _slerp(qa, qb, 0.5)
                        if w < 0.4:
                            q = _slerp(qa, qr, w / 0.4)
                        else:
                            q = _slerp(qr, qb, (w - 0.4) / 0.6)
                        bases[n] = q.to_matrix().to_4x4()
                H = Gs[f] @ Hg
                R = H.to_3x3().normalized()
                H = R.to_4x4()
                H.translation = (Gs[f] @ Hg).translation
                # nenhum dedo dentro do corpo da arma: abre o dedo até sair (fora da pegada)
                if ws[f] > 0:
                    Gi = Gs[f].inverted()
                    Mh = {mao: H}
                    for dd in DEDOS + ("thumb",):
                        if pinca is not None and ws[f] >= 1.0 - 1e-6 and dd in ("index", "thumb"):
                            continue      # (a pinça no ferrolho fica como foi feita)
                        ns = [n for n in bases if n.startswith(dd)]
                        if not ns:
                            continue
                        idx = pele.de(*ns)
                        orig = {n: bases[n].to_quaternion() for n in ns}
                        for k in (1.0, 0.75, 0.5, 0.25, 0.0):
                            for n in ns:
                                ab = _dobra(cfg.get("abre_min", (12, 15))[0 if n.split(".")[0].endswith("1") else 1]).to_quaternion() if dd != "thumb" else orig[n]
                                bases[n] = _slerp(ab, orig[n], k).to_matrix().to_4x4()
                            Mx = dict(Mh)
                            for n in esq.descendentes(mao):
                                Mx[n] = esq.fk(Mx, n, bases.get(n))
                            pts = pele.posar(Mx, idx)
                            if max(corpo.fundo(Gi @ p) for p in pts[::2]) < 0.002 or dd == "thumb":
                                break
                # o braço segue a mão
                S0 = M0[ua].translation + (s["ombro"] if cfg.get("ombro_da_pegada") else Vector(cfg.get("ombro_pref", (0.0, 0.0, 0.0))))
                E0 = alvo_cot(cfg, s, M0, Gs[f], G_idle, w if pinca is not None else cfg.get("cot_orig_k", 0.0), w)
                lim_ = cfg.get("lim", LIM)
                pm = cfg.get("pivo_max", 0.0) * (1.0 - w)
                if pm > 0 or cfg.get("pivo_fixo") is not None:
                    # (rodada 4) na pegada, se o antebraço pedido passar do desvio do guia (a corrida: a arma gira), a mão
                    # gira no plano da palma em volta do ponto da palma (desliza girando na lateral do pescoço) só o que
                    # falta, até pivo_max graus
                    nrm = -(R @ Vector((0.0, 0.0, 1.0)))
                    P_ = H @ PALMA
                    f0_ = (H.translation - E0).normalized()
                    def _exc(a_):
                        Q_ = Matrix.Rotation(math.radians(a_), 3, nrm)
                        b_, d_ = b3.angulos(Q_ @ R, f0_, lado)
                        return max(0.0, abs(d_) - (lim_[1] - 1.0)) + max(0.0, abs(b_) - (lim_[0] - 1.0)), b_, d_
                    if cfg.get("pivo_fixo") is not None:
                        melhor_a = cfg["pivo_fixo"] * (1.0 - w)      # (rodada 5) o giro escolhido à mão, o mesmo em todo quadro
                    else:
                        melhor_a = min((a_ for a_ in [x * 0.5 for x in range(-int(2 * pm), int(2 * pm) + 1)]), key=lambda a_: (round(_exc(a_)[0], 1), abs(a_)))
                    if melhor_a:
                        Q_ = Matrix.Rotation(math.radians(melhor_a), 3, nrm)
                        R = Q_ @ R
                        H = R.to_4x4()
                        H.translation = P_ - R @ PALMA
                    if dep == f"{nome}.{lado}" or dep == "tudo":
                        log(f"  DEP pivo {nome} {lado} q{f}: {melhor_a:.1f} graus (excesso {_exc(0)[0]:.0f} -> {_exc(melhor_a)[0]:.0f})")
                km = cfg.get("k_mao", 0.0) * min(1.0, ws[f] * 1.5)
                if km > 0:
                    # (fora da pegada) a mão gira em volta da palma uma parte (km) do que o pulso passaria do guia
                    # para o antebraço apontar para o cotovelo do original: a outra parte fica para o antebraço
                    f0_ = (H.translation - E0).normalized()
                    b0_, d0_ = b3.angulos(R, f0_, lado)
                    eb = b0_ - max(-lim_[0], min(lim_[0], b0_))
                    ed = d0_ - max(-lim_[1], min(lim_[1], d0_))
                    if abs(eb) > 0.5 or abs(ed) > 0.5:
                        v = (R.transposed() @ f0_).normalized()
                        v2 = (R.transposed() @ b3.direcao(R, b0_ - km * eb, d0_ - km * ed, lado)).normalized()
                        Q = v2.rotation_difference(v).to_matrix()
                        P_ = H @ PALMA
                        R = R @ Q
                        H = R.to_4x4()
                        H.translation = P_ - R @ PALMA
                # a referência: o braço da pegada sozinho neste quadro (sem memória), para os clipes voltarem ao Idle
                ref = None
                if ws[f] < 1.0 - 1e-6:
                    Hr = Gs[f] @ s["Hg"]
                    Rr = Hr.to_3x3().normalized()
                    Hr = Rr.to_4x4()
                    Hr.translation = (Gs[f] @ s["Hg"]).translation
                    Er = alvo_cot(cfg, s, M0, Gs[f], G_idle, 0.0, 0.0)
                    _, _, ir = b3.resolver(esq, lado, Hr.translation, Rr, Er, S0, lim_, None, S_orig=M0[ua].translation)
                    ref = (ir["u"], ir["psi"], ir["bd"])
                if ws[f] <= 0:
                    S, E, info = b3.resolver(esq, lado, H.translation, R, E0, S0, lim_, None, S_orig=M0[ua].translation)
                else:
                    S, E, info = b3.resolver(esq, lado, H.translation, R, E0, S0, lim_, antes, ref=ref, k_ref=2.0 * (1.0 - w), k_antes=1.0, S_orig=M0[ua].translation)
                antes = (info["u"], info["psi"], info["bd"], info["tor"])
                H, rolou = b3.rolar(esq, lado, S, E, H, lim_[2])
                if rolou:
                    info["rolou"] = rolou
                b3.aplicar(esq, M, lado, S, E, H, bases)
                if dep == f"{nome}.{lado}" or dep == "tudo":
                    pp = dm.pulso(esq, M, lado)
                    _Gi = Gs[f].inverted()
                    _z = []
                    for d_ in DEDOS + ("thumb",):
                        _ps = [_Gi @ q_ for q_ in pele.posar(M, pele.de(*[n_ for n_ in M if n_.startswith(d_) and n_.endswith("." + lado)]))]
                        _low = min(_ps, key=lambda q_: q_.z)
                        _arame = sum(1 for q_ in _ps if abs(q_.x) < 0.02 and -0.105 < q_.y < -0.033 and -0.01 < q_.z < 0.037)
                        _z.append(f"{d_}: baixo {lib.v3(_low, 3)} arame {_arame}")
                    log("  DEP pontas (arma) " + " | ".join(_z))
                    log("  DEP dedos " + " ".join(f"{n.split('.')[0]}:{math.degrees(bases[n].to_quaternion().angle):.0f}" for n in sorted(bases)))
                    if pinca is not None:
                        mid = (M["index2.R"] @ Vector((0.0, 0.030, -0.006)) + M["thumb2.R"] @ Vector((0.0, 0.026, -0.005))) / 2
                        log(f"  DEP pinça q{f}: meio das pontas a {(mid - Gs[f] @ ext['bola'][f]).length * 1000:.0f} mm da bola; bola {lib.v3(Gs[f] @ ext['bola'][f], 2)} pulso {lib.v3(H.translation, 2)}")
                    log(f"  DEP {nome} {lado} q{f}: w {ws[f]:.2f} pulso {pp[0]:4.0f} {pp[1]:4.0f} {pp[2]:5.0f} (pedido {info['dobra_pedida']:4.0f} {info['desvio_pedido']:4.0f}) S {lib.v3(S, 2)} E {lib.v3(E, 2)} W {lib.v3(H.translation, 2)} custo {info['custo']:.0f}"
                        f" rolou {info.get('rolou', 0):.0f} ombro-orig {(S - M0[ua].translation).length * 100:.0f} cm cot-orig {(E - M0[fa].translation).length * 100:.0f} cm"
                        f" | px W {_px(H.translation)} E {_px(E)} E0 {_px(E0)} S {_px(S)}")
            longe = [f for f in fs if ws[f] > 0]
            log(f"  CLIPE {nome} {lado}: fora da pegada nos quadros {(min(longe), max(longe)) if longe else '-'} de {f0}..{f1}")
        for f, M in quadros.items():
            for n in esq.ordem:
                if n not in M:
                    M[n] = M0s[f][n]
        novas[nome] = (a, quadros)
    for nome, (a, quadros) in novas.items():
        nome_acao = a.name
        nova = pg._gravar(rig, esq, nome_acao + "_nova", quadros)
        bpy.data.actions.remove(a)
        nova.name = nome_acao
        acoes[nome] = nova
    bpy.context.scene.render.fps, bpy.context.scene.render.fps_base = fps, 1.0
    pf.exportar(rig, bracos, boca, acoes, saida)


def principal():
    args = sys.argv[sys.argv.index("--") + 1:]
    t0 = time.time()
    try:
        fazer(args[0], args[1])
        print(f"FAZER OK {args[0]} {time.time() - t0:.1f} s")
    except Exception:
        traceback.print_exc()
        print("FAZER FALHOU")
        sys.exit(1)
'''

# ---------------------------------------------------------------------------------------------------------------------
# SNIPER: o braco4.py da rodada 4 (o braço segue a mão: o antebraço no guia do pulso, o ombro fora da tela)
_MOTORES["fogo_sniper_braco"] = r'''# Rodada 3: o braço segue a mão. Dada a mão (o pulso W e o giro R, espaço da armadura), escolhe o cotovelo e o ombro:
#  - o antebraço aponta o mais perto possível do cotovelo do original (E0), dentro do guia do pulso (dobra e desvio
#    cortados numa caixa): assim o antebraço entra na tela por onde entrava no original e o pulso fica reto;
#  - o ombro gira em volta do antebraço até a torção caber no guia, o mais perto da direção do ombro do original, sem o
#    braço (do cotovelo ao ombro) aparecer na tela e sem saltar de um quadro para o outro.
import math

from mathutils import Matrix, Quaternion, Vector

import fogo_sniper_lib as lib

dm, ag = lib.dm, lib.ag
Y = Vector((0.0, 1.0, 0.0))


def angulos(R, f, lado):
    """A dobra e o desvio (graus, como dm.pulso) do pulso com a mão R e o antebraço na direção f (do cotovelo ao pulso)."""
    v = (R.transposed() @ f).normalized()
    ang = math.acos(max(-1.0, min(1.0, v.y)))
    ax = Vector((-v.z, 0.0, v.x))
    if ax.length < 1e-9:
        return 0.0, 0.0
    ax.normalize()
    return math.degrees(ang * ax.x), math.degrees(ang * ax.z) * (1.0 if lado == "R" else -1.0)


def direcao(R, b, d, lado):
    """A direção do antebraço que dá a dobra b e o desvio d com a mão R."""
    w = Vector((math.radians(b), 0.0, math.radians(d) * (1.0 if lado == "R" else -1.0)))
    if w.length < 1e-9:
        return (R @ Y).normalized()
    s = Quaternion(w.normalized(), w.length)
    return (R @ (s.inverted().to_matrix() @ Y)).normalized()


def na_tela(p, folga=1.0):
    d = -p.y
    if d < 0.02:
        return False
    return abs(-p.x / d / dm.TX) < folga and abs(p.z / d / dm.TY) < folga


def resolver(esq, lado, W, R, E0, S0, lim=(50.0, 20.0, 80.0), antes=None, alvo_f=None, gamas=(0.0, -20.0, 20.0, -40.0),
             ref=None, k_ref=0.0, k_antes=1.0, S_orig=None):
    """Devolve (S, E, info). antes: o (u, psi, (dobra, desvio)) do quadro anterior (para não saltar). alvo_f: a direção do antebraço
    pedida (por omissão a do cotovelo do original ao pulso novo)."""
    ua, fa, mao = "upper_arm." + lado, "forearm." + lado, "hand." + lado
    La, Lf = esq.comprimento(ua, fa), esq.comprimento(fa, mao)
    f0 = (alvo_f if alvo_f is not None else (W - E0)).normalized()
    b0, d0 = angulos(R, f0, lado)
    # a dobra e o desvio dentro da caixa do guia: o antebraço o mais perto de f0, sem saltar do quadro anterior
    def nota_bd(b_, d_):
        n = math.degrees(direcao(R, b_, d_, lado).angle(f0))
        if antes is not None and antes[2] is not None:
            n += 0.03 * k_antes * ((b_ - antes[2][0]) ** 2 + (d_ - antes[2][1]) ** 2)
        if ref is not None and k_ref:
            n += 0.03 * k_ref * ((b_ - ref[2][0]) ** 2 + (d_ - ref[2][1]) ** 2)
        return n
    melhor_bd = None
    nb, nd = 12, 6
    for i in range(nb + 1):
        for j in range(nd + 1):
            b_ = -lim[0] + 2 * lim[0] * i / nb
            d_ = -lim[1] + 2 * lim[1] * j / nd
            n = nota_bd(b_, d_)
            if melhor_bd is None or n < melhor_bd[0]:
                melhor_bd = (n, b_, d_)
    bc_ = max(-lim[0], min(lim[0], b0))
    dc_ = max(-lim[1], min(lim[1], d0))
    n = nota_bd(bc_, dc_)
    if n < melhor_bd[0]:
        melhor_bd = (n, bc_, dc_)
    _, b, d = melhor_bd
    for passo in (4.0, 2.0, 1.0, 0.5):
        for db_ in (-passo, 0.0, passo):
            for dd_ in (-passo, 0.0, passo):
                b_ = max(-lim[0], min(lim[0], b + db_))
                d_ = max(-lim[1], min(lim[1], d + dd_))
                n = nota_bd(b_, d_)
                if n < melhor_bd[0]:
                    melhor_bd = (n, b_, d_)
        _, b, d = melhor_bd
    f = direcao(R, b, d, lado)
    E = W - f * Lf
    u0 = (S0 - E).normalized()
    g0 = math.degrees(u0.angle(f))
    p1 = u0 - u0.dot(f) * f
    if p1.length < 1e-6:
        p1 = f.orthogonal()
    p1.normalize()
    p2 = f.cross(p1)
    H = R.to_4x4()
    H.translation = W
    Mt = {mao: H, fa: Matrix.Translation(E)}

    def avaliar(psi, g):
        gr, pr = math.radians(g), math.radians(psi)
        u = math.cos(gr) * f + math.sin(gr) * (math.cos(pr) * p1 + math.sin(pr) * p2)
        S = E + u * La
        Mt[ua] = Matrix.Translation(S)
        bb, dd, tt = dm.pulso(esq, Mt, lado)
        custo = 8.0 * max(0.0, abs(tt) - lim[2]) + 0.6 * math.degrees(u.angle(u0))
        for t in (0.1, 0.2, 0.3, 0.4, 0.6, 0.8, 1.0):
            p = E + (S - E) * t
            if t < 0.9 and p.length < 0.16:
                custo += 300.0          # (o braço passando junto da cabeça, da câmera)
            dd_ = -p.y
            # (rodada 4) o braço de cima não pode aparecer: a pena maior que a da continuidade, e perto do cotovelo também
            if dd_ > 0.02 and na_tela(p, 1.0 + 0.06 / (dd_ * dm.TY)):
                custo += 2000.0
        if S_orig is not None:
            # a pele esticada entre o ombro do original (o peito) e o ombro novo não pode aparecer
            for t in (0.25, 0.5, 0.75, 1.0):
                p = S_orig + (S - S_orig) * t
                dd_ = -p.y
                if dd_ > 0.02 and na_tela(p, 1.0 + 0.07 / (dd_ * dm.TY)):
                    custo += 400.0
        # (quadráticos: a passagem reparte a mudança entre os quadros, sem saltar)
        if antes is not None:
            custo += 0.25 * k_antes * math.degrees(u.angle(antes[0])) ** 2
            if len(antes) > 3 and antes[3] is not None:
                dt = (tt - antes[3] + 180.0) % 360.0 - 180.0
                custo += 0.25 * k_antes * dt * dt
        if ref is not None and k_ref:
            custo += 0.25 * k_ref * math.degrees(u.angle(ref[0])) ** 2
        return custo, S, u, tt

    melhor = None
    for dg in gamas:
        g = max(45.0, min(170.0, g0 + dg))
        for psi in range(-180, 180, 6):
            r = avaliar(psi, g)
            if melhor is None or r[0] < melhor[0][0]:
                melhor = (r, psi, g)
    _, psi, g = melhor
    for passo in (3.0, 1.5, 0.75):
        for dp in (-passo, 0.0, passo):
            for dgg in (-passo * 2, 0.0, passo * 2):
                gg = max(45.0, min(170.0, g + dgg))
                r = avaliar(psi + dp, gg)
                if r[0] < melhor[0][0]:
                    melhor = (r, psi + dp, gg)
        _, psi, g = melhor
    (custo, S, u, tt), psi, g = melhor
    return S, E, {"u": u, "bd": (b, d), "psi": psi, "dobra": b, "desvio": d, "tor": tt, "custo": custo, "dobra_pedida": b0, "desvio_pedido": d0}


def rolar(esq, lado, S, E, H, lim_t):
    """Se a torção passou do guia, rola a mão em volta do antebraço (pelo pulso) só o que passou. Devolve a mão."""
    ua, fa, mao = "upper_arm." + lado, "forearm." + lado, "hand." + lado
    Mt = {ua: Matrix.Translation(S), fa: Matrix.Translation(E), mao: H}
    t = dm.pulso(esq, Mt, lado)[2]
    exc = abs(t) - lim_t
    if exc <= 0:
        return H, 0.0
    f = (H.translation - E).normalized()
    melhor = None
    for sg in (1.0, -1.0):
        R2 = Matrix.Rotation(math.radians(sg * exc), 3, f) @ H.to_3x3()
        H2 = R2.to_4x4()
        H2.translation = H.translation
        Mt[mao] = H2
        t2 = dm.pulso(esq, Mt, lado)[2]
        if melhor is None or abs(t2) < melhor[0]:
            melhor = (abs(t2), H2, sg * exc)
    return melhor[1], melhor[2]


def aplicar(esq, M, lado, S, E, H, bases):
    """O braço por IK com o ombro em S e o cotovelo em E (já nos comprimentos certos) e a mão em H."""
    ag.braco(esq, M, lado, S, H.translation.copy(), H.to_3x3(), E - S)
    M["hand." + lado] = H.copy()
    esq.refazer_abaixo(M, "hand." + lado, bases)
'''

# ---------------------------------------------------------------------------------------------------------------------
# SNIPER: o fazer.py da rodada 1 (resolve a pegada no Idle 1; o fazer5 usa partes dele)
_MOTORES["fogo_sniper_fazer"] = r'''# Faz o FBX novo de uma arma (a pegada do poses.py em todos os clipes) a partir do FBX original da sniper.
#   blender_um.sh --python fazer.py -- <Arma> <saida.fbx>
# 1) na cena de conferência (os braços com a mão nova e a arma do jogo): resolve a pegada de cada mão no Idle 1 (a mão
#    no espaço da arma, os dedos, o cotovelo e o deslocamento do ombro);
# 2) numa cena limpa com o FBX original: em cada quadro de cada clipe, a mão na pegada (presa à arma) ou, onde a mão do
#    original sai da arma (o ferrolho, a recarga), no caminho do original, com a passagem suave; o braço por IK; o pulso
#    cortado nos limites no caminho; os dedos passando da pegada para os do original; grava e exporta como o original.
import os
import sys
import time
import traceback

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import bpy
from mathutils import Matrix, Vector
import fogo_sniper_lib as lib
import fogo_sniper_poses as poses
import pegada_fogo as pf

ag, dm, fc, pg = lib.ag, lib.dm, lib.fc, lib.pg
ORIGINAL = pf.original_fbx("Sniper")     # (o pegada_fogo.py confere que é o original; o rifle de sucata parte dele)
LONGE = (0.03, 0.08)        # a mão do original a menos de 3 cm do lugar dela no Idle 1: segura; a mais de 8: o caminho dele
PASSO_W = 0.2               # a passagem em pelo menos 5 quadros
NO_CAMINHO = (55.0, 22.0, 90.0)


def limitar(esq, M, lado, lim, antes=None):
    """O giro da mão (3x3) com o pulso dentro dos limites (a dobra, o desvio e a torção cortados em lim, graus), no braço
    da pose M. antes: a torção do quadro anterior (rad): a torção anda no máximo 30 graus por quadro, pelo lado dela.
    Devolve (o giro, a torção). (O do pegada_fogo._no_limite, com o passo máximo valendo sempre.)"""
    import math
    from mathutils import Quaternion
    ua, fa, mao = "upper_arm." + lado, "forearm." + lado, "hand." + lado
    S, Ec, W = M[ua].translation, M[fa].translation, M[mao].translation
    u = (W - S).normalized()
    e = (Ec - S) - (Ec - S).dot(u) * u
    e = e.normalized() if e.length > 1e-6 else u.orthogonal().normalized()
    z = u.cross(e) if lado == "L" else -u.cross(e)
    B = ag.quadro_yz(W - Ec, z, Ec).to_3x3() @ (esq.rest[fa].to_3x3().inverted() @ esq.rest[mao].to_3x3())
    q = (B.inverted() @ M[mao].to_3x3()).to_quaternion()
    if q.w < 0:
        q = -q
    n = math.hypot(q.w, q.y)
    tw = Quaternion((q.w / n, 0.0, q.y / n, 0.0)) if n > 1e-9 else Quaternion()
    sw = tw.inverted() @ q
    if sw.w < 0:
        sw = -sw
    tor = 2 * math.atan2(tw.y, tw.w)
    tor = (tor + math.pi) % (2 * math.pi) - math.pi
    ang = sw.angle
    eixo = sw.axis if ang > 1e-9 else Vector((1.0, 0.0, 0.0))
    dx, dz = ang * eixo.x, ang * eixo.z
    lx, lz, lt = (math.radians(v) for v in lim)
    if antes is not None:
        d = (tor - antes + math.pi) % (2 * math.pi) - math.pi
        passo = math.radians(dm.PASSO_MAX)
        tor = antes + max(-passo, min(passo, d))
    tor2 = max(-lt, min(lt, tor))
    dx2, dz2 = max(-lx, min(lx, dx)), max(-lz, min(lz, dz))
    v = Vector((dx2, 0.0, dz2))
    s2 = Quaternion(v.normalized(), v.length) if v.length > 1e-9 else Quaternion()
    tw2 = Quaternion(Vector((0.0, 1.0, 0.0)), tor2)
    return B @ (tw2 @ s2).to_matrix(), tor2


def resolver(arma, log=print):
    """A pegada de cada mão no Idle 1 (cena de conferência). Devolve {lado: {Hg, bases, Eg, ombro, h_ref, pulso}}."""
    c = lib.carregar(arma, ORIGINAL)
    rig, esq = c["rig"], c["esq"]
    M0 = lib.matrizes(rig, c["acoes"]["Idle"], 1)
    G = lib.G_de(c, M0)
    Gi = G.inverted()
    bvh = lib.Arma(c, M0)
    sol = {}
    for lado, cfg in poses.POSES[arma].items():
        cfg = dict(cfg)
        pele = lib.Pele(c, lado)
        if "palma_em" in cfg:
            R = lib.rot_mao(cfg["y"], cfg["palma"])
            d = Vector(cfg.get("desliza", cfg["palma"])).normalized()
            cfg["pulso"] = Vector(cfg["palma_em"]) - R @ Vector(cfg.get("ponto_mao", (0.0, 0.065, 0.0))) - d * 0.06
            cfg["t0"] = 0.0
        Hg, bases = lib.resolver_mao(c, bvh, pele, lado, cfg, log=log)
        H = G @ Hg
        if "ombro_auto" in cfg:
            cfg["ombro"] = tuple(lib.ombro_para(c, M0, lado, H, G @ Vector(cfg["cotovelo"]), *cfg["ombro_auto"]))
        M = dict(M0)
        lib.braco(c, M, M0, lado, H, bases, G @ Vector(cfg["cotovelo"]), cfg.get("ombro"))
        p = dm.pulso(esq, M, lado)
        log(f"  PEGADA {arma} {lado}: pulso dobra {p[0]:.0f} desvio {p[1]:.0f} torção {p[2]:.0f}; ombro {lib.v3(cfg.get('ombro') or (0, 0, 0))}; "
            f"cotovelo {lib.v3(Gi @ M['forearm.' + lado].translation)}")
        sol[lado] = {"Hg": Hg.copy(), "bases": {n: m.copy() for n, m in bases.items()},
                     "Eg": (Gi @ M["forearm." + lado].translation).copy(), "ombro": Vector(cfg.get("ombro") or (0, 0, 0)),
                     "h_ref": (Gi @ M0["hand." + lado]).copy(), "pulso": p,
                     "Sg": (Gi @ M["upper_arm." + lado].translation).copy(), "na_arma": bool(cfg.get("ombro_na_arma")),
                     "proprio": bool(cfg.get("braco_proprio")), "lim_caminho": cfg.get("lim_caminho")}
    return sol, c["Erig"].copy()


def fazer(arma, saida, log=print):
    sol, Erig = resolver(arma, log)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = ag.importar(ORIGINAL)
    rig = next(o for o in objs if o.type == "ARMATURE")
    bracos = next(o for o in objs if o.type == "MESH" and o.name.startswith("FP_Bracos"))
    boca = next(o for o in objs if o.name.startswith("Boca_FP"))
    acoes = {a.name.split("|", 1)[1]: a for a in bpy.data.actions if a.name.startswith(rig.name + "|")}
    rig.animation_data_create()
    fps = bpy.context.scene.render.fps
    esq = ag.Esqueleto(rig)
    rest = esq.rest
    novas = {}
    for nome in sorted(acoes):
        a = acoes[nome]
        f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
        M0s = {f: lib.matrizes(rig, a, f) for f in range(f0, f1 + 1)}
        quadros = {f: dict(M0s[f]) for f in M0s}
        fs = sorted(M0s)
        for lado, s in sol.items():
            mao, fa, ua = "hand." + lado, "forearm." + lado, "upper_arm." + lado
            Gs = {f: M0s[f]["gun"] @ rest["gun"].inverted() @ Erig for f in fs}
            ws = {}
            for f in fs:
                h = Gs[f].inverted() @ M0s[f][mao]
                d = (h.translation - s["h_ref"].translation).length
                ws[f] = max(0.0, min(1.0, (d - LONGE[0]) / (LONGE[1] - LONGE[0])))
            for _ in range(2):
                for i in range(1, len(fs)):
                    ws[fs[i]] = max(ws[fs[i]], ws[fs[i - 1]] - PASSO_W)
                for i in range(len(fs) - 2, -1, -1):
                    ws[fs[i]] = max(ws[fs[i]], ws[fs[i + 1]] - PASSO_W)
            lim = (max(NO_CAMINHO[0], abs(s["pulso"][0]) + 3), max(NO_CAMINHO[1], abs(s["pulso"][1]) + 3),
                   max(NO_CAMINHO[2], abs(s["pulso"][2]) + 3))
            if s["proprio"] and s.get("lim_caminho"):
                lim = s["lim_caminho"]
            # no caminho do original (w = 1): o braço do original, com o giro da mão cortado nos limites do pulso
            Rp, tor_antes = {}, None
            for f in fs:
                if ws[f] >= 1.0 - 1e-6:
                    Mt = M0s[f]
                    if s["proprio"]:
                        # (o braço da pegada também no caminho: o ombro e o cotovelo presos à arma, só a mão segue o original)
                        Mt = {}
                        Sx = Gs[f] @ s["Sg"]
                        ag.braco(esq, Mt, lado, Sx, M0s[f][mao].translation.copy(), M0s[f][mao].to_3x3(), (Gs[f] @ s["Eg"]) - Sx)
                    Rp[f], tor_antes = limitar(esq, Mt, lado, lim, tor_antes)
                else:
                    tor_antes = None
            for f in fs:
                M0, M = M0s[f], quadros[f]
                w = pf._suave(ws[f])
                Hgrip = Gs[f] @ s["Hg"]
                # o ombro da pegada: o de antes mais o deslocamento, ou preso à arma (o braço inteiro acompanha a arma)
                Sg = (Gs[f] @ s["Sg"]) if s["na_arma"] else (M0[ua].translation + s["ombro"])
                bases = {}
                for n, b in s["bases"].items():
                    q = b.to_quaternion()
                    if ws[f] > 0:
                        q = q.slerp(esq.base(M0, n).to_quaternion(), w)
                    bases[n] = q.to_matrix().to_4x4()

                def por_ik(S, H, cot):
                    """O braço por IK: o ombro em S (avança o que faltar), a mão em H, o cotovelo para o ponto cot."""
                    v = H.translation - S
                    maximo = dm._alcance_max(esq, lado)
                    if v.length > maximo:
                        S = S + v.normalized() * (v.length - maximo)
                    Mx = {}
                    ag.braco(esq, Mx, lado, S, H.translation.copy(), H.to_3x3(), cot - S)
                    return Mx

                if ws[f] <= 0:
                    Mx = por_ik(Sg, Hgrip, Gs[f] @ s["Eg"])
                else:
                    # o braço do caminho: o ombro, o cotovelo e o lugar da mão do original; o giro da mão o do caminho
                    # (cortado nos limites) neste quadro ou, na passagem, o do quadro mais perto em que a mão já está no
                    # caminho (levado com a arma: os giros do original no meio da passagem dão a volta de uma vez)
                    if f in Rp:
                        Rk = Rp[f]
                    elif Rp:
                        perto = min(Rp, key=lambda g: abs(g - f))
                        Rk = Gs[f].to_3x3() @ Gs[perto].to_3x3().inverted() @ Rp[perto]
                    else:
                        Rk = limitar(esq, M0, lado, lim, None)[0]
                    Hk = Rk.to_4x4()
                    Hk.translation = M0[mao].translation
                    if s["proprio"]:
                        Mb = por_ik(Sg, Hk, Gs[f] @ s["Eg"])
                    else:
                        Mb = por_ik(M0[ua].translation.copy(), Hk, M0[fa].translation)
                    if f in Rp:
                        Mx = Mb
                    else:
                        # a passagem: os giros do braço, do antebraço e da mão entre os da pegada e os do caminho (como uma
                        # mistura de animações), o ombro em linha reta
                        Ma = por_ik(Sg, Hgrip, Gs[f] @ s["Eg"])
                        Mx = {}
                        m = Ma[ua].to_quaternion().slerp(Mb[ua].to_quaternion(), w).to_matrix().to_4x4()
                        m.translation = Ma[ua].translation.lerp(Mb[ua].translation, w)
                        Mx[ua] = m
                        m = Ma[fa].to_quaternion().slerp(Mb[fa].to_quaternion(), w).to_matrix().to_4x4()
                        m.translation = (Mx[ua] @ esq.rel[fa]).translation
                        Mx[fa] = m
                        m = Ma[mao].to_quaternion().slerp(Mb[mao].to_quaternion(), w).to_matrix().to_4x4()
                        m.translation = (Mx[fa] @ esq.rel[mao]).translation
                        Mx[mao] = m
                M[ua], M[fa], M[mao] = Mx[ua], Mx[fa], Mx[mao]
                esq.refazer_abaixo(M, mao, bases)
                if os.environ.get("DEPURAR") == f"{nome}.{lado}":
                    pp = dm.pulso(esq, M, lado)
                    p0 = dm.pulso(esq, M0, lado)
                    tp = fc.torcoes_na_malha(esq, M, lado)[0]
                    log(f"  DEP {nome} {lado} q{f}: w {ws[f]:.2f} dobra {pp[0]:4.0f} desvio {pp[1]:4.0f} tor {pp[2]:5.0f} malha {tp:4.0f} | orig {p0[0]:4.0f} {p0[1]:4.0f} {p0[2]:5.0f}"
                        f" | cot {lib.v3(M[fa].translation)} orig {lib.v3(M0[fa].translation)}")
            longe = [f for f in fs if ws[f] > 0]
            log(f"  CLIPE {nome} {lado}: fora da pegada nos quadros {(min(longe), max(longe)) if longe else '-'} de {f0}..{f1}")
        for f, M in quadros.items():
            for n in esq.ordem:
                if n not in M:
                    M[n] = M0s[f][n]
        novas[nome] = (a, quadros)
    for nome, (a, quadros) in novas.items():
        nome_acao = a.name
        nova = pg._gravar(rig, esq, nome_acao + "_nova", quadros)
        bpy.data.actions.remove(a)
        nova.name = nome_acao
        acoes[nome] = nova
    bpy.context.scene.render.fps, bpy.context.scene.render.fps_base = fps, 1.0
    pf.exportar(rig, bracos, boca, acoes, saida)


if __name__ == "__main__" and "--" in sys.argv:
    args = sys.argv[sys.argv.index("--") + 1:]
    t0 = time.time()
    try:
        fazer(args[0], args[1])
        print(f"FAZER OK {args[0]} {time.time() - t0:.1f} s")
    except Exception:
        traceback.print_exc()
        print("FAZER FALHOU")
        sys.exit(1)
'''

# ---------------------------------------------------------------------------------------------------------------------
# SNIPER: o lib.py da rodada 2 (a mão no espaço da arma, os dedos, o polegar buscado para o alvo)
_MOTORES["fogo_sniper_lib"] = r'''# A pegada nova das armas de fogo (rodada 1, sniper e rifle de sucata): a pose da mão escrita à mão no espaço da arma
# (para onde os dedos apontam, para onde a palma olha, onde fica o pulso), a mão encostada na arma por contato (a pele
# da palma desliza até tocar a malha da arma), cada dedo fechado até tocar, o polegar posto à mão e fechado até tocar;
# o braço por IK (o cotovelo puxado para um ponto escolhido).
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

AQUI = os.path.dirname(os.path.abspath(__file__))
if AQUI not in sys.path:
    sys.path.insert(0, AQUI)
import pegada_fogo_conferir as fc  # noqa: E402
ag, dm, bc, mn = fc.ag, fc.dm, fc.bc, fc.mn
import pegada as pg  # noqa: E402

DEDOS = ("index", "middle", "ring", "pinky")
MARGEM = 0.0008


def v3(x, n=3):
    return tuple(round(c, n) for c in x)


def matrizes(rig, acao, f):
    ag.usar_acao(rig, acao)
    bpy.context.scene.frame_set(f)
    bpy.context.view_layer.update()
    return {pb.name: pb.matrix.copy() for pb in rig.pose.bones}


def G_de(c, M):
    """O espaço da arma solta (o cano em -Y, o alto em +Z, a direita em -X) -> o da armadura, na pose M."""
    return M["gun"] @ c["esq"].rest["gun"].inverted() @ c["Erig"]


class Pele:
    """A pele da mão nova de um lado: os vértices no repouso (espaço da armadura) e os pesos; posa por conta própria
    (a mesma conta do modificador de armadura), sem o depsgraph."""

    def __init__(self, c, lado):
        o = c["maos"][lado]
        rig = c["rig"]
        Mw = rig.matrix_world.inverted() @ o.matrix_world
        nomes = {g.index: g.name for g in o.vertex_groups}
        ossos = set(c["esq"].rest)
        self.p, self.w, self.dono = [], [], []
        for v in o.data.vertices:
            ws = [(nomes[g.group], g.weight) for g in v.groups if nomes[g.group] in ossos and g.weight > 0]
            t = sum(w for _, w in ws) or 1.0
            self.p.append(Mw @ v.co)
            self.w.append([(n, w / t) for n, w in ws])
            self.dono.append(max(ws, key=lambda x: x[1])[0] if ws else "")
        self.rest_inv = {n: m.inverted() for n, m in c["esq"].rest.items()}

    def de(self, *ossos):
        return [i for i, d in enumerate(self.dono) if d in ossos]

    def posar(self, M, idx):
        D = {}
        out = []
        for i in idx:
            s = Vector()
            for n, w in self.w[i]:
                if n not in D:
                    D[n] = M[n] @ self.rest_inv[n]
                s += w * (D[n] @ self.p[i])
            out.append(s)
        return out


class Arma:
    """A malha da arma do jogo (todas as peças, na pose M) no espaço da arma solta, para os contatos."""

    def __init__(self, c, M):
        rig = c["rig"]
        Gi = G_de(c, M).inverted()
        dg = bpy.context.evaluated_depsgraph_get()
        vs, fs = [], []
        for o in c["arma"].values():
            ev = o.evaluated_get(dg)
            me = ev.to_mesh()
            base = len(vs)
            Mo = Gi @ rig.matrix_world.inverted() @ o.matrix_world
            vs += [Mo @ v.co for v in me.vertices]
            fs += [[base + i for i in p.vertices] for p in me.polygons]
            ev.to_mesh_clear()
        self.bvh = BVHTree.FromPolygons(vs, fs)

    def toca(self, p, margem=MARGEM):
        loc, nor, _, dist = self.bvh.find_nearest(p, 0.03)
        if loc is None:
            return False
        if dist < margem:
            return True
        return (p - loc).dot(nor) < 0 and bc._dentro(self.bvh, p)[0]

    def fundo(self, p):
        """Quanto o ponto está dentro da arma (m; 0 fora)."""
        loc, nor, _, dist = self.bvh.find_nearest(p, 0.03)
        if loc is None:
            return 0.0
        return dist if (p - loc).dot(nor) < 0 and bc._dentro(self.bvh, p)[0] else 0.0

    def dist(self, p):
        loc, nor, _, dist = self.bvh.find_nearest(p, 0.2)
        return dist if loc is not None else 1.0


def rot_mao(y, palma):
    """O giro da mão (3x3): o Y para onde os dedos apontam (exato) e a palma olhando para 'palma' (ortogonalizada)."""
    return ag.rot_da_mao(Vector(y).normalized(), Vector(palma).normalized())


def _dobra(a, abrir=0.0, rolar=0.0):
    """O giro local de uma falange: dobra 'a' graus para a palma, abre 'abrir' para o lado, rola em volta do osso."""
    m = Matrix.Rotation(math.radians(abrir), 4, "Z") @ Matrix.Rotation(math.radians(-a), 4, "X")
    if rolar:
        m = m @ Matrix.Rotation(math.radians(rolar), 4, "Y")
    return m


def esq_completar(esq, M, mao, bases):
    for n in esq.descendentes(mao):
        M[n] = esq.fk(M, n, bases.get(n))


def resolver_mao(c, arma_bvh, pele, lado, cfg, log=None):
    """A mão do lado na arma: devolve (H: a matriz do hand no espaço da arma, bases: {osso do dedo: matriz local}).
    cfg: y, palma, pulso (o lugar de partida), desliza (a direção do deslize até encostar; por omissão a palma),
    fixo (não desliza), dedos {nome: (max1, max2, abrir[, min1, min2])}, polegar {abrir, rolar, dobra, max1, max2,
    pre2}."""
    esq = c["esq"]
    R = rot_mao(cfg["y"], cfg["palma"])
    d = Vector(cfg.get("desliza", cfg["palma"])).normalized()
    mao = "hand." + lado
    e = 1.0 if lado == "L" else -1.0        # (o abrir e o rolar espelhados na direita)
    cfg = dict(cfg)
    cfg["dedos"] = {n: tuple(v[:2]) + (e * v[2],) + tuple(v[3:]) for n, v in cfg["dedos"].items()}
    pol0 = dict(cfg.get("polegar", {}))
    pol0["abrir"], pol0["rolar"] = e * pol0.get("abrir", 0.0), e * pol0.get("rolar", 0.0)
    cfg["polegar"] = pol0
    idx_palma = pele.de(mao)
    bases0 = {}
    for n in DEDOS:
        dd = cfg["dedos"].get(n, (90, 90, 0))
        bases0[f"{n}1.{lado}"] = _dobra(dd[3] if len(dd) > 3 else 0.0, dd[2])
        bases0[f"{n}2.{lado}"] = _dobra(dd[4] if len(dd) > 4 else 0.0)
    pol = cfg.get("polegar", {})
    bases0[f"thumb.{lado}"] = _dobra(pol.get("dobra", 0.0), pol.get("abrir", 0.0), pol.get("rolar", 0.0))
    bases0[f"thumb2.{lado}"] = _dobra(pol.get("pre2", 0.0))

    def pose(t, bases):
        H = R.to_4x4()
        H.translation = Vector(cfg["pulso"]) + d * t
        M = {mao: H, "forearm." + lado: H}      # (os vértices do pulso com peso no antebraço: como se reto)
        esq_completar(esq, M, mao, bases)
        return M

    def toca(M, idx):
        return any(arma_bvh.toca(p) for p in pele.posar(M, idx))

    # 1) desliza a mão (os dedos abertos) até a palma encostar
    if cfg.get("fixo"):
        t = 0.0
    else:
        t = cfg.get("t0", -0.06)
        passo = 0.004
        while passo > 0.0002:
            while t < 0.06 and not toca(pose(t + passo, bases0), idx_palma):
                t += passo
            passo /= 4
        t -= cfg.get("folga", 0.0)       # (rodada 2) a palma recuada da madeira (os dedos fecham até as pontas tocarem)
    bases = dict(bases0)
    # 2) cada dedo: a primeira falange até tocar, depois a segunda
    rel = {}
    for n in DEDOS:
        dd = cfg["dedos"].get(n, (90, 90, 0))
        a1, a2 = (dd[3] if len(dd) > 3 else 0.0), (dd[4] if len(dd) > 4 else 0.0)
        o1, o2 = f"{n}1.{lado}", f"{n}2.{lado}"
        i1, i2 = pele.de(o1), pele.de(o2)
        if cfg.get("dedos_ordem") == "ponta":
            # (rodada 2) a garra: a segunda falange fecha primeiro até a ponta tocar, depois a primeira (o dedo todo)
            bases[o1] = _dobra(a1, dd[2])
            while a2 < dd[1]:
                bases[o2] = _dobra(a2 + 1.5)
                if toca(pose(t, bases), i2):
                    break
                a2 += 1.5
            bases[o2] = _dobra(a2)
            while a1 < dd[0]:
                bases[o1] = _dobra(a1 + 1.5, dd[2])
                if toca(pose(t, bases), i1 + i2):
                    break
                a1 += 1.5
            bases[o1] = _dobra(a1, dd[2])
            rel[n] = (round(a1), round(a2))
            continue
        while a1 < dd[0]:
            bases[o1] = _dobra(a1 + 1.5, dd[2])
            if toca(pose(t, bases), i1 + (i2 if cfg.get("dedo_todo") else [])):
                break
            a1 += 1.5
        bases[o1] = _dobra(a1, dd[2])
        while a2 < dd[1]:
            bases[o2] = _dobra(a2 + 1.5)
            if toca(pose(t, bases), i2):
                break
            a2 += 1.5
        bases[o2] = _dobra(a2)
        rel[n] = (round(a1), round(a2))
    # 2b) o indicador no gatilho: a busca dos giros que levam a almofada da ponta ao ponto pedido, sem entrar na arma
    if cfg.get("gatilho"):
        alvo = Vector(cfg["gatilho"])
        o1, o2 = f"index1.{lado}", f"index2.{lado}"
        i1, i2 = pele.de(o1), pele.de(o2)
        r2i = esq.rest[o2].inverted()
        loc = [r2i @ pele.p[i] for i in i2]
        ymax = max(p.y for p in loc)
        ponta = [p for p in loc if p.y > ymax - 0.016]
        almofada = Vector((sum(p.x for p in ponta) / len(ponta), ymax - 0.009, min(p.z for p in ponta) + 0.001))
        melhor = None
        for ab in cfg.get("gatilho_abrir", (-20, -10, 0, 10, 20)):
            for a1 in range(-10, cfg.get("gatilho_a1max", 75), 5):
                for a2 in range(0, 95, 5):
                    bases[o1], bases[o2] = _dobra(a1, e * ab), _dobra(a2)
                    M = pose(t, bases)
                    dist = (M[o2] @ almofada - alvo).length
                    if melhor is not None and dist >= melhor[0]:
                        continue
                    if max(arma_bvh.fundo(p) for p in pele.posar(M, i1 + i2)) > 0.0012:
                        continue
                    melhor = (dist, a1, a2, ab)
        if melhor is not None and melhor[0] <= cfg.get("gatilho_max", 0.010):
            bases[o1], bases[o2] = _dobra(melhor[1], e * melhor[3]), _dobra(melhor[2])
            rel["index"] = ("gatilho", melhor[1], melhor[2], melhor[3], round(melhor[0] * 1000, 1))
        else:
            dd = cfg["dedos"]["index"]
            bases[o1], bases[o2] = _dobra(dd[3] if len(dd) > 3 else dd[0], dd[2]), _dobra(dd[4] if len(dd) > 4 else dd[1])
            rel["index"] = ("longe", melhor)
    # 3) o polegar
    o1, o2 = f"thumb.{lado}", f"thumb2.{lado}"
    a1, a2 = pol.get("dobra", 0.0), pol.get("pre2", 0.0)
    i1, i2 = pele.de(o1), pele.de(o2)
    if pol.get("alvo"):
        # (rodada 2) a busca do polegar: a ponta no ponto pedido (espaço da arma), deitado na madeira (a distância média
        # da pele do thumb2 à arma), sem entrar nela
        res = _polegar_busca(esq, pose, t, bases, pele, arma_bvh, lado, e, pol, log)
        bases[o1], bases[o2] = res["b1"], res["b2"]
        rel["thumb"] = res["rel"]
        M = pose(t, bases)
        if log is not None:
            log(f"  MAO {lado}: deslizou {t * 1000:.1f} mm, pulso {v3(M[mao].translation)}, dedos {rel}")
        return M[mao], bases
    while a1 < pol.get("max1", a1):
        bases[o1] = _dobra(a1 + 1.5, pol.get("abrir", 0.0), pol.get("rolar", 0.0))
        if toca(pose(t, bases), i1 + (i2 if pol.get("junto", True) else [])):
            break
        a1 += 1.5
    bases[o1] = _dobra(a1, pol.get("abrir", 0.0), pol.get("rolar", 0.0))
    while a2 < pol.get("max2", a2):
        bases[o2] = _dobra(a2 + 1.5)
        if toca(pose(t, bases), i2):
            break
        a2 += 1.5
    bases[o2] = _dobra(a2)
    rel["thumb"] = (round(a1), round(a2))
    M = pose(t, bases)
    if log is not None:
        log(f"  MAO {lado}: deslizou {t * 1000:.1f} mm, pulso {v3(M[mao].translation)}, dedos {rel}")
    return M[mao], bases


def _polegar_busca(esq, pose, t, bases, pele, arma_bvh, lado, e, pol, log=None):
    """A pose do polegar (thumb e thumb2) que leva a ponta ao ponto pol['alvo'] (espaço da arma) com a pele do thumb2
    o mais perto da madeira (deitado; peso pol['deitado'], por omissão 0,6) e nada dentro da arma (> 1,2 mm).
    Busca em grade larga e depois fina em volta da melhor. abrir/rolar no lado da esquerda (espelhados na direita)."""
    mao = "hand." + lado
    o1, o2 = f"thumb.{lado}", f"thumb2.{lado}"
    i1, i2 = pele.de(o1), pele.de(o2)
    r2i = esq.rest[o2].inverted()
    loc = [r2i @ pele.p[i] for i in i2]
    ymax = max(p.y for p in loc)
    pl = [p for p in loc if p.y > ymax - 0.010]
    ponta = sum(pl, Vector()) / len(pl)
    amostra = [loc[k] for k in range(0, len(loc), max(1, len(loc) // 24))]
    r1i = esq.rest[o1].inverted()
    loc1 = [r1i @ pele.p[i] for i in i1]
    amostra1 = [loc1[k] for k in range(0, len(loc1), max(1, len(loc1) // 16))]
    k1 = pol.get("deitado1", 0.0)
    alvo = Vector(pol["alvo"])
    kd = pol.get("deitado", 0.6)
    direcao = Vector(pol["direcao"]).normalized() if pol.get("direcao") else None
    M0 = pose(t, bases)
    base_m = {mao: M0[mao]}

    def avaliar(ab, ro, d1, d2, checar=True):
        b1, b2 = _dobra(d1, e * ab, e * ro), _dobra(d2)
        M = dict(base_m)
        M[o1] = esq.fk(M, o1, b1)
        M[o2] = esq.fk(M, o2, b2)
        dist = (M[o2] @ ponta - alvo).length
        perto = sum(arma_bvh.dist(M[o2] @ p) for p in amostra) / len(amostra)
        nota = dist + kd * perto
        if k1:
            nota += k1 * sum(arma_bvh.dist(M[o1] @ p) for p in amostra1) / len(amostra1)
        if direcao is not None:
            nota += 0.02 * (1.0 - M[o2].to_3x3().col[1].normalized().dot(direcao))
        return nota, M, b1, b2, dist, perto

    def ok(M):
        Mx = dict(M0)
        Mx[o1], Mx[o2] = M[o1], M[o2]
        return max(arma_bvh.fundo(p) for p in pele.posar(Mx, i1 + i2)) <= 0.0012

    fa = pol.get("faixa_abrir", (-70, 70))
    fr = pol.get("faixa_rolar", (-80, 80))
    f1 = pol.get("faixa_dobra", (-40, 80))
    f2 = pol.get("faixa_dobra2", (0, 100))

    def grade(a0, a1_, p):
        n = max(1, int(round((a1_ - a0) / p)))
        return [a0 + (a1_ - a0) * k / n for k in range(n + 1)]

    melhor = None
    cands = []
    for ab in grade(*fa, 10):
        for ro in grade(*fr, 15):
            for d1 in grade(*f1, 10):
                for d2 in grade(*f2, 15):
                    nota, M, b1, b2, dist, perto = avaliar(ab, ro, d1, d2)
                    cands.append((nota, ab, ro, d1, d2))
    cands.sort()
    vistos = 0
    for nota, ab, ro, d1, d2 in cands[:3000]:
        r = avaliar(ab, ro, d1, d2)
        vistos += 1
        if ok(r[1]):
            melhor = (nota, ab, ro, d1, d2)
            break
    if melhor is None:
        melhor = cands[0]
    # fina
    passo = (5.0, 7.5, 5.0, 7.5)
    for _ in range(3):
        _, ab0, ro0, d10, d20 = melhor
        for ab in (ab0 - passo[0], ab0, ab0 + passo[0]):
            for ro in (ro0 - passo[1], ro0, ro0 + passo[1]):
                for d1 in (d10 - passo[2], d10, d10 + passo[2]):
                    for d2 in (d20 - passo[3], d20, d20 + passo[3]):
                        if not (f2[0] <= d2 <= f2[1]):
                            continue
                        r = avaliar(ab, ro, d1, d2)
                        if r[0] < melhor[0] and ok(r[1]):
                            melhor = (r[0], ab, ro, d1, d2)
        passo = tuple(p / 2 for p in passo)
    nota, ab, ro, d1, d2 = melhor
    r = avaliar(ab, ro, d1, d2)
    if log is not None:
        log(f"  POLEGAR {lado} ({vistos} vistos): abrir {ab:.1f} rolar {ro:.1f} dobra {d1:.1f} dobra2 {d2:.1f} | ponta a {r[4] * 1000:.1f} mm do alvo,"
            f" pele a {r[5] * 1000:.1f} mm da arma; base {v3(M0[o1].translation)} ponta {v3(r[1][o2] @ ponta)}"
            f" alcance {(esq.rest[o1].inverted() @ esq.rest[o2] @ ponta).length * 1000:.0f} mm, alvo a {(alvo - M0[o1].translation).length * 1000:.0f} mm da base")
    return {"b1": r[2], "b2": r[3], "rel": ("busca", round(ab), round(ro), round(d1), round(d2), round(r[4] * 1000, 1))}


def braco(c, M, M0, lado, H, bases, cotovelo, ombro=None):
    """O braço do lado na pose M (espaço da armadura): a mão em H, o cotovelo puxado para o ponto 'cotovelo', o ombro o
    de antes (M0) mais 'ombro'. Devolve o avanço do ombro."""
    esq = c["esq"]
    S = M0["upper_arm." + lado].translation.copy()
    if ombro is not None:
        S = S + Vector(ombro)
    v = H.translation - S
    maximo = dm._alcance_max(esq, lado)
    avancou = 0.0
    if v.length > maximo:
        avancou = v.length - maximo
        S = S + v.normalized() * avancou
    polo = Vector(cotovelo) - S
    ag.braco(esq, M, lado, S, H.translation.copy(), H.to_3x3(), polo)
    esq.refazer_abaixo(M, "hand." + lado, bases)
    return avancou


def ombro_para(c, M0, lado, H, cotovelo, flex, torcao):
    """O deslocamento do ombro (espaço da armadura) que põe o cotovelo no ponto pedido (a um antebraço do pulso), com o
    braço dobrado 'flex' graus e a torção do antebraço o mais perto de 'torcao'."""
    esq = c["esq"]
    W = H.translation
    b_ = esq.comprimento("forearm." + lado, "hand." + lado)
    a_ = esq.comprimento("upper_arm." + lado, "forearm." + lado)
    f = (W - Vector(cotovelo)).normalized()
    Ec = W - f * b_
    S0 = M0["upper_arm." + lado].translation
    base = f.orthogonal().normalized()
    melhor = None
    for psi in range(0, 360, 5):
        dirp = Matrix.Rotation(math.radians(psi), 3, f) @ base
        S = Ec + a_ * (math.cos(math.radians(flex)) * f + math.sin(math.radians(flex)) * dirp)
        Mt = dict(M0)
        braco(c, Mt, M0, lado, H, {}, Ec, tuple(S - S0))
        tt = dm.pulso(esq, Mt, lado)[2]
        erro = abs((tt - torcao + 180) % 360 - 180)
        if melhor is None or erro < melhor[0]:
            melhor = (erro, S - S0)
    return melhor[1]


def carregar(arma, fbx=None):
    c = fc.carregar(arma, fbx)
    c["Erig"] = c["rig"].matrix_world.inverted() @ c["E"]
    return c


def gravar_pose(c, nome, M, f=1):
    """Uma ação de um quadro só com a pose M (para as fotos)."""
    rig = c["rig"]
    rig.animation_data_create()
    a = pg._gravar(rig, c["esq"], nome, {f: M})
    c["acoes"][nome] = a
    return a


# ------------------------------------------------------------------------------------------------ as fotos

def foto_jogo(c, nome, f, caminho, w=1280, h=720):
    fc.foto_jogo(c, nome, f, caminho, w, h)


def fotos_perto(c, nome, f, prefixo, w=480, h=360, lados="LR"):
    """Cada mão de 4 ângulos: da direção da câmera do jogo (com zoom), de lado, de cima e de baixo (no espaço da arma)."""
    rig = c["rig"]
    ag.usar_acao(rig, c["acoes"][nome])
    bpy.context.scene.frame_set(f)
    bpy.context.view_layer.update()
    ag._render_base(w, h)
    M = {pb.name: pb.matrix.copy() for pb in rig.pose.bones}
    G = G_de(c, M)
    R3 = G.to_3x3().normalized()
    out = []
    for lado in lados:
        H = M["hand." + lado]
        alvo = H @ Vector((0.0, 0.07, -0.02))
        s = 1.0 if lado == "L" else -1.0
        vistas = (("camera", None, None),
                  ("lado", Vector((s, -0.05, 0.12)), Vector((0, 0, 1))),
                  ("cima", Vector((s * 0.15, 0.2, 1.0)), Vector((0, -1, 0))),
                  ("baixo", Vector((s * 0.15, 0.1, -1.0)), Vector((0, -1, 0))),
                  ("outro", Vector((-s, -0.3, -0.25)), Vector((0, 0, 1))))
        for vista, d, cima in vistas:
            c["bracos"].hide_render = vista != "camera"
            if vista == "camera":
                cam = ag._camera("Perto", (0, 0, 0), alvo=alvo, angulo_y=16)
                cam.rotation_euler = mn._olhar(Vector((0, 0, 0)), alvo, Vector((0, 0, 1))).to_euler()
            else:
                dd = (R3 @ d).normalized()
                pos = alvo + dd * 0.36
                cam = ag._camera("Perto", pos, alvo=alvo, lente=50)
                cam.rotation_euler = mn._olhar(pos, alvo, (R3 @ cima).normalized()).to_euler()
            caminho = f"{prefixo}_{lado}_{vista}.png"
            ag._salvar(caminho)
            out.append(caminho)
    c["bracos"].hide_render = False
    return out
'''

# ---------------------------------------------------------------------------------------------------------------------
# SNIPER: o poses.py (as poses vêm de ENTRADAS)
_MOTORES["fogo_sniper_poses"] = r'''import pegada_fogo as pf

POSES = {a: pf.entrada(a)["pose"] for a in ("Sniper", "Sucata_Rifle")}
'''

# ---------------------------------------------------------------------------------------------------------------------
# ESPINGARDA (rodada 3): o gravar.py (roda inteiro ao ser importado: a pegada no Idle 1, depois os clipes)
_MOTORES["fogo_espingarda"] = r'''# grava os clipes da espingarda com a pegada nova (esp.py) num FBX:
#   (pelo pegada_fogo.py: -- Espingarda <saida.fbx> [depurar] [tela]; os parâmetros vêm de ENTRADAS)
# Os dedos e o braço são medidos no Idle 1 (com a mão nova e a arma do jogo, como no pose_teste.py); depois o FBX original
# é lido de novo, limpo, e cada quadro de cada clipe é refeito: a mão na pegada (presa ao osso da peça), o braço por IK;
# onde a mão de antes sai da arma (a recarga), ela segue o caminho de antes, com a passagem suave.
import json, math, os, sys, time
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
import bpy
from mathutils import Matrix, Vector
import fogo_espingarda_esp as esp
pf, fc, ag, pg, dm = esp.pf, esp.fc, esp.ag, esp.pg, esp.dm

args = sys.argv[sys.argv.index("--") + 1:]
spec = json.loads(json.dumps(pf.entrada("Espingarda")["params"]))   # (o p_final.json da rodada 3)
saida = args[0]
t0 = time.time()
ARMA = "Espingarda"
# 1) a pegada, no Idle 1
c = fc.carregar(ARMA)
rig, esq = c["rig"], c["esq"]
E = rig.matrix_world.inverted() @ c["E"]
M_idle = pf._matrizes(rig, c["acoes"]["Idle"], 1)
pegs, dedos, dedos_rec, dedos_meio = {}, {}, {}, {}
for lado in "LR":
    p = dict(spec[lado])
    if p.get("direto"):
        p["ombro"], p["cot"] = esp.buscar_direto(esq, M_idle, lado, p, E)
    dedos[lado] = {n: m.copy() for n, m in esp.dedos_da_pegada(c, lado, p, M_idle, E).items()}
    if p.get("rec_polegar_alvo"):
        # na recarga (rodada 2): o polegar deixa o alto do pescoço e deita ao longo do lado direito (o que a câmera não vê)
        Wg_ = pf._da_arma(M_idle, c["esq"].rest, p["osso"], E)
        q_ = dict(p); q_["polegar_alvo"] = p["rec_polegar_alvo"]; q_.pop("polegar", None)
        gp_, ip_ = esp.polegar(c["esq"], lado, Wg_ @ esp.mao_na_arma(lado, q_), esp.Fundura(esp.bvh_da_arma(c)), pg.pontos_da_mao_nova(), q_, Wg_)
        dedos_rec[lado] = dict(dedos[lado]); dedos_rec[lado].update({n: m.copy() for n, m in gp_.items()})
        print(f"  DEDOS {lado} na recarga: polegar {ip_}")
        dedos_meio[lado] = None
        if p.get("rec_polegar_meio"):
            q_["polegar_alvo"] = p["rec_polegar_meio"]
            gp_, ip_ = esp.polegar(c["esq"], lado, Wg_ @ esp.mao_na_arma(lado, q_), esp.Fundura(esp.bvh_da_arma(c)), pg.pontos_da_mao_nova(), q_, Wg_)
            dedos_meio[lado] = dict(dedos[lado]); dedos_meio[lado].update({n: m.copy() for n, m in gp_.items()})
            print(f"  DEDOS {lado} na passagem: polegar {ip_}")
    pegs[lado] = p
    print(f"PEGADA {lado}: ombro {tuple(round(x, 3) for x in p.get('ombro', (0, 0, 0)))} cot {tuple(round(x, 3) for x in p.get('cot', (0, 0, 0)))}")
# 2) o FBX original, limpo
bpy.ops.wm.read_factory_settings(use_empty=True)
objs = ag.importar(fc.bracos_fbx(ARMA))
rig = next(o for o in objs if o.type == "ARMATURE")
bracos = next(o for o in objs if o.type == "MESH" and o.name.startswith("FP_Bracos"))
acoes = {a.name.split("|", 1)[1]: a for a in bpy.data.actions if a.name.startswith(rig.name + "|")}
rig.animation_data_create()
fps = bpy.context.scene.render.fps
E2, erro, _ = fc._encaixe(rig, bracos, fc.ARMAS[ARMA][1], fc.ARMAS[ARMA][2])
E2 = rig.matrix_world.inverted() @ E2
print(f"GRAVAR: encaixe {erro * 1000:.2f} mm, fps {fps}, diferença do E {max(abs(E[i][j] - E2[i][j]) for i in range(4) for j in range(4)):.6f}")
E = E2
esq = ag.Esqueleto(rig)
rest = esq.rest
M_idle = pf._matrizes(rig, acoes["Idle"], 1)
for lado in "LR":
    # o braço resolvido no Idle 1, guardado no espaço da arma (o ombro) e da peça que a mão segura (o cotovelo)
    p = pegs[lado]
    M = dict(M_idle)
    esp.posar(esq, M, M_idle, lado, p, E, None)
    p["S_arma"] = tuple(pf._da_arma(M_idle, rest, "gun", E).inverted() @ M["upper_arm." + lado].translation)
    p["E_mao"] = tuple(M["hand." + lado].inverted() @ M["forearm." + lado].translation)
    p["E_osso"] = tuple(pf._da_arma(M_idle, rest, p["osso"], E).inverted() @ M["forearm." + lado].translation)
h_ref = {lado: pf._da_arma(M_idle, rest, pegs[lado]["osso"], E).inverted() @ M_idle["hand." + lado] for lado in "LR"}


def ajustado(p, nome, f, M0):
    """(rodada 3) os ajustes de um quadro: "mover" (m, no espaço da arma), "dedos" (a fração dos dedos de antes), "giro"
    (a fração do giro da pegada), "w" (a fração do caminho de antes), "no_caminho" (os limites do pulso)"""
    aj = p.get("ajuste", {}).get(nome, {}).get(str(f))
    if not aj:
        return p, None
    q = dict(p)
    if "mover" in aj:
        q["_mover"] = tuple(pf._da_arma(M0, rest, "gun", E).to_3x3() @ Vector(aj["mover"]))
    if "dedos" in aj:
        q["_w_dedos"] = aj["dedos"]
    if "giro" in aj:
        q["_giro"] = aj["giro"]
    if "no_caminho" in aj:
        q["no_caminho"] = aj["no_caminho"]
    return q, aj.get("w")
novas, resumo = {}, {}
for nome in sorted(acoes):
    a = acoes[nome]
    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
    M0s = {f: pf._matrizes(rig, a, f) for f in range(f0, f1 + 1)}
    quadros = {f: dict(M0s[f]) for f in M0s}
    for lado in "LR":
        p = pegs[lado]
        # onde a mão de antes sai da arma (a recarga): perto, a mão nova faz o mesmo movimento da de antes a partir da
        # pegada nova (o deslocamento e o giro da de antes em relação à pegada dela, no espaço da peça); longe (de 5 a
        # 25 cm), passa para o caminho de antes
        ws, Hp = {}, {}
        G = esp.mao_na_arma(lado, p)
        hr = h_ref[lado]
        longe = p.get("longe", (0.05, 0.25))
        for f, M0 in M0s.items():
            Wf = pf._da_arma(M0, rest, p["osso"], E)
            h = Wf.inverted() @ M0["hand." + lado]
            d = (h.translation - hr.translation).length
            ws[f] = max(0.0, min(1.0, (d - longe[0]) / (longe[1] - longe[0])))
            if d > 0.002:
                Rn = (h.to_3x3() @ hr.to_3x3().inverted() @ G.to_3x3()).to_4x4()
                Rn.translation = G.translation + (h.translation - hr.translation)
                Hp[f] = Wf @ Rn
        fs = sorted(ws)
        fixos = {}
        if p.get("por_quadro"):
            # 1) o cotovelo e o ombro de cada quadro; 2) suavizados no tempo (em volta, nos clipes que dão a volta: o
            # último quadro igual ao primeiro); 3) o braço posto com eles
            prev = None
            for f in fs:
                pf_ = p
                rf = p.get("reforco", {}).get(nome, {}).get(str(f))
                if rf:
                    # (rodada 2) neste quadro o antebraço segue mais o caminho de antes na tela: rf = (vezes o peso da
                    # tela, a dobra máxima)
                    pf_ = dict(p); pf_["pq_tela"] = p.get("pq_tela", 600.0) * rf[0]; pf_["pq_meio"] = p.get("pq_meio", 0.0) * rf[0]
                    pf_["dobra_corte"] = rf[1]; pf_["dobra_boa"] = min(rf[1] - 4, p.get("dobra_boa", 50.0) + (rf[1] - 58))
                    if len(rf) > 2:
                        pf_["desvio_corte"] = rf[2]
                pf_, w_aj = ajustado(pf_, nome, f, M0s[f])
                Ec, S, H = esp.posar(esq, dict(M0s[f]), M0s[f], lado, pf_, E, None, pf._suave(ws[f]) if w_aj is None else w_aj, H_perto=Hp.get(f), so_cotovelo=True, prev=prev)
                fixos[f] = [Ec, S, H.translation.copy()]
                prev = Ec
            volta = all((M0s[fs[0]][n].translation - M0s[fs[-1]][n].translation).length < 1e-4 for n in ("hand." + lado, "forearm." + lado, "gun"))
            seq = fs[:-1] if volta else fs
            for _ in range(p.get("pq_suave", 2)):
                novos = {}
                for i, f in enumerate(seq):
                    if not volta and i in (0, len(seq) - 1):
                        novos[f] = fixos[f]
                        continue
                    fa_, fb_ = seq[(i - 1) % len(seq)], seq[(i + 1) % len(seq)]
                    # (o cotovelo em relação ao pulso, para a distância do pulso ao cotovelo não mudar)
                    d = (fixos[fa_][0] - fixos[fa_][2]) + 2 * (fixos[f][0] - fixos[f][2]) + (fixos[fb_][0] - fixos[fb_][2])
                    Wf = fixos[f][2]
                    Ec = Wf + d.normalized() * (fixos[f][0] - Wf).length
                    S = (fixos[fa_][1] + 2 * fixos[f][1] + fixos[fb_][1]) / 4.0
                    a_ = esq.comprimento("upper_arm." + lado, "forearm." + lado)
                    S = Ec + (S - Ec).normalized() * a_
                    novos[f] = [Ec, S, Wf]
                fixos.update(novos)
            if volta:
                fixos[fs[-1]] = fixos[fs[0]]
        tor_antes = None
        pior = [0.0, 0.0, 0.0, 0.0]
        vis = [0, 0]
        for f in fs:
            M0, M = M0s[f], quadros[f]
            w = pf._suave(ws[f])
            p_aj, w_aj = ajustado(p, nome, f, M0)
            if w_aj is not None:
                w = w_aj
            dd = dedos[lado]
            wr = float(p.get("rec_w", {}).get(str(f), 0.0)) if (lado in dedos_rec and nome == p.get("rec_clipe", "Reload")) else 0.0
            if wr > 0:
                if dedos_meio.get(lado):
                    A_, B_, t_ = (dedos[lado], dedos_meio[lado], wr * 2) if wr <= 0.5 else (dedos_meio[lado], dedos_rec[lado], wr * 2 - 1)
                else:
                    A_, B_, t_ = dedos[lado], dedos_rec[lado], wr
                dd = {n: A_[n].to_quaternion().slerp(B_[n].to_quaternion(), t_).to_matrix().to_4x4() for n in dedos[lado]}
            av, Mas, polo, H, ds = esp.posar(esq, M, M0, lado, p_aj, E, dd, w, H_perto=Hp.get(f),
                                             fixo=(fixos[f][0], fixos[f][1]) if f in fixos else None)
            if f in Hp:
                R, tor_antes = pf._no_limite(esq, M, lado, tuple(p_aj.get("no_caminho", pf.NO_CAMINHO)), tor_antes)
                H2 = R.to_4x4()
                H2.translation = H.translation
                dm.braco_no_lugar(esq, M, Mas, lado, H2, polo, ds)
            dobra, desvio, tor = dm.pulso(esq, M, lado)
            if f in Hp and "depurar" in args:
                v3 = lambda x: tuple(round(k, 2) for k in x)  # noqa: E731
                d0 = dm.pulso(esq, M0, lado)
                print(f"    DEP {nome} {lado} q{f}: w {w:.2f} pulso {v3(M['hand.' + lado].translation)} (antes {v3(M0['hand.' + lado].translation)}) cotovelo "
                      f"{v3(M['forearm.' + lado].translation)} (antes {v3(M0['forearm.' + lado].translation)}) ombro {v3(M['upper_arm.' + lado].translation)} "
                      f"(antes {v3(M0['upper_arm.' + lado].translation)}) pulso {round(dobra)},{round(desvio)},{round(tor)} (antes {[round(x) for x in d0]})"
                      f" giro da mão {math.degrees(M['hand.' + lado].to_quaternion().rotation_difference(M0['hand.' + lado].to_quaternion()).angle):.0f}")
            if "tela" in args and lado == "L" and nome in ("Reload", "Run", "Sacar"):
                tn, to = esp.na_tela(M["forearm.L"].translation), esp.na_tela(M0["forearm.L"].translation)
                print(f"    TELA {nome} q{f}: w {w:.2f} cotovelo novo {tuple(round(x, 2) for x in tn[:2]) if tn else '-'} antes {tuple(round(x, 2) for x in to[:2]) if to else '-'} pulso {round(dobra)},{round(desvio)},{round(tor)}")
            pior = [max(pior[0], abs(dobra)), max(pior[1], abs(desvio)), max(pior[2], abs(tor)), max(pior[3], av * 1000)]
            for k, MM in enumerate((M0, M)):
                S, Ec, W = (MM[f"{n}.{lado}"].translation for n in ("upper_arm", "forearm", "hand"))
                vis[k] += esp.visivel(S, Ec, 0.05) + (esp.visivel(Ec, W, 0.045) if lado == "R" else 0)
        longe = [f for f in fs if f in Hp]
        print(f"  CLIPE {nome} {lado}: dobra {pior[0]:.0f} desvio {pior[1]:.0f} torção {pior[2]:.0f} avanço {pior[3]:.0f} mm; fora da pegada "
              f"{(min(longe), max(longe)) if longe else '-'}; braço na tela (pontos, antes -> depois) {vis[0]} -> {vis[1]}")
    for f, M in quadros.items():
        for n in esq.ordem:
            if n not in M:
                M[n] = M0s[f][n]
    novas[nome] = (a, quadros)
for nome, (a, quadros) in novas.items():
    nome_acao = a.name
    nova = pg._gravar(rig, esq, nome_acao + "_nova", quadros)
    bpy.data.actions.remove(a)
    nova.name = nome_acao
    acoes[nome] = nova
boca = next(o for o in bpy.data.objects if o.name.startswith("Boca_FP"))
bpy.context.scene.render.fps, bpy.context.scene.render.fps_base = fps, 1.0
pf.exportar(rig, bracos, boca, acoes, saida)
json.dump({l: {k: (list(v) if isinstance(v, tuple) else v) for k, v in p.items()} for l, p in pegs.items()},
          open(os.path.splitext(saida)[0] + "_params.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(f"GRAVAR FIM {time.time() - t0:.0f} s")
'''

# ---------------------------------------------------------------------------------------------------------------------
# ESPINGARDA: o esp.py (a mão na arma, os dedos em fila, o polegar, o cotovelo por quadro)
_MOTORES["fogo_espingarda_esp"] = r'''# A pegada da espingarda, rodada 1 (11/11/2026): a pose de cada mão escrita à mão no espaço da arma (o cano em -Y, o alto
# em +Z, a esquerda da arma em +X), os dedos fechados até encostar na malha da arma e o braço por IK com o cotovelo
# puxado para perto do de antes. Usado pelo teste (pose_teste.py: só o Idle 1, várias variantes) e pela gravação
# (gravar.py: todos os clipes).
import math
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
if AQUI not in sys.path:
    sys.path.insert(0, AQUI)
import bpy  # noqa: E402
from mathutils import Euler, Matrix, Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import pegada_fogo as pf  # noqa: E402

fc, ag, pg, dm = pf.fc, pf.ag, pf.pg, pf.dm
bc = fc.bc
DEDOS = ("index", "middle", "ring", "pinky")


def rad(a):
    return math.radians(a)


def _cols(X, Y, Z):
    return Matrix((X, Y, Z)).transposed()


def _euler(e):
    return Euler((rad(e[0]), rad(e[1]), rad(e[2])), "XYZ").to_matrix()


def mao_na_arma(lado, p):
    """A matriz do hand no espaço da arma.
    L (por baixo da telha): phi = a volta em torno do cano (0: a palma para cima, a mão embaixo; +: a mão para o lado
    esquerdo), alfa = a diagonal (0: a mão atravessada, o lado do polegar para a boca; 90: os dedos para a boca).
    R (o pescoço da coronha): psi = as costas da mão para a direita (0) ou para cima (90), gama = os dedos para a frente
    (0) ou para baixo (90).
    extra: giros a mais no espaço do hand (graus, XYZ); C: o ponto do eixo da peça (arma); g: o mesmo ponto no hand."""
    if lado == "L":
        phi, alfa = rad(p["phi"]), rad(p["alfa"])
        Z = Vector((math.sin(phi), 0.0, -math.cos(phi)))
        t = Vector((-math.cos(phi), 0.0, -math.sin(phi)))
        Y = (math.cos(alfa) * t + math.sin(alfa) * Vector((0.0, -1.0, 0.0))).normalized()
    else:
        psi, gama = rad(p["psi"]), rad(p["gama"])
        Z = Vector((-math.cos(psi), 0.0, math.sin(psi)))
        t = Vector((-math.sin(psi), 0.0, -math.cos(psi)))
        Y = (math.cos(gama) * Vector((0.0, -1.0, 0.0)) + math.sin(gama) * t).normalized()
    X = Y.cross(Z)
    R = _cols(X, Y, Z) @ _euler(p.get("extra", (0, 0, 0)))
    H = R.to_4x4()
    H.translation = Vector(p["C"]) - R @ Vector(p["g"])
    return H


class Fundura:
    """A fundura de um ponto (no espaço da armadura) dentro da arma: > 0 dentro, < 0 fora (m)."""

    def __init__(self, bvh):
        self.bvh = bvh

    def __call__(self, w):
        loc, nor, _, dist = self.bvh.find_nearest(w, 0.06)
        if loc is None:
            return -0.06
        return dist if (w - loc).dot(nor) < 0 else -dist


def bvh_da_arma(c, pecas=None):
    dg = bpy.context.evaluated_depsgraph_get()
    vs, fs = [], []
    for nome, o in c["arma"].items():
        if pecas and nome not in pecas:
            continue
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        base = len(vs)
        mw = c["rig"].matrix_world.inverted() @ o.matrix_world
        vs += [mw @ v.co for v in me.vertices]
        fs += [[base + i for i in pl.vertices] for pl in me.polygons]
        ev.to_mesh_clear()
    return BVHTree.FromPolygons(vs, fs)


def _q_dedo(a, abd=0.0, rol=0.0):
    return Matrix.Rotation(rad(abd), 4, "Z") @ Matrix.Rotation(rad(rol), 4, "Y") @ pg._dobra(a)


def _eixo_dedo(M, n, comp, k=7):
    """pontos ao longo do eixo do osso n (matriz M no espaço da armadura), de 0 a comp"""
    return [M @ Vector((0.0, comp * i / (k - 1), 0.0)) for i in range(k)]


def _dist_eixos(A, B):
    return min((a - b).length for a in A for b in B)


def _ponto_seg(P, A, B):
    ab = B - A
    t = max(0.0, min(1.0, (P - A).dot(ab) / max(1e-12, ab.dot(ab))))
    return (P - (A + ab * t)).length


def _lado_a_lado(A, B):
    """a distância média dos pontos de A (do terço ao fim) à linha B (para dedos vizinhos: juntos ao longo da ponta)"""
    ds = [min(_ponto_seg(a, B[k], B[k + 1]) for k in range(len(B) - 1)) for a in A[2:]]
    return sum(ds) / len(ds)


def dedos_em_fila(esq, lado, Hm, fundo, pts, p, Wg):
    """(rodada 3) os quatro dedos em fila: do indicador ao mínimo, cada dedo dobra as duas juntas (no mínimo "dobra_min")
    com as almofadas encostadas na arma, sem entrar nela, e encosta no dedo anterior (o vão entre os eixos das pontas
    menos a grossura dos dois até "vao_vizinho" mm); as pontas não passam da altura "z_max" (m, no espaço da peça)."""
    rest, pai = esq.rest, esq.pai
    tol = p.get("tol", 0.001)
    dmin1, dmin2 = p.get("dobra_min", (25.0, 30.0))
    zmax = p.get("z_max")
    vviz = p.get("vao_vizinho", 1.5)
    Wi = Wg.inverted()
    giros, info = {}, {}
    ant = None
    abds = p.get("abd_passos", list(range(-16, 17, 4)))
    for d in DEDOS:
        n1, n2 = f"{d}1.{lado}", f"{d}2.{lado}"
        R1 = rest[pai[n1]].inverted() @ rest[n1]
        R2 = rest[pai[n2]].inverted() @ rest[n2]
        p1 = [q for q in pts.get(n1, []) if q.y > 0.006]
        p2 = [q for q in pts.get(n2, [])]
        y1 = max(q.y for q in pts.get(n1, []))
        y2 = max(q.y for q in p2)
        r2_ = sorted(math.hypot(q.x, q.z) for q in p2 if 0.2 * y2 < q.y < 0.8 * y2)
        raio = r2_[len(r2_) // 2] if r2_ else 0.008
        pad1 = [q for q in p1 if q.z < 0.002 and q.y > 0.4 * y1]
        pad2 = [q for q in p2 if q.z < 0.002 and q.y < y2 - 0.004]
        ponta = [q for q in p2 if q.y > y2 - 0.012]
        melhor = None
        for abd in abds:
            for a1 in range(0, 96, 5):
                M1 = Hm @ R1 @ _q_dedo(a1, abd)
                e1 = max((fundo(M1 @ q) for q in p1), default=-1.0)
                if e1 > tol:
                    continue
                g1 = sum(max(0.0, -fundo(M1 @ q)) for q in pad1) / max(1, len(pad1))
                for a2 in range(10, 101, 5):
                    M2 = M1 @ R2 @ pg._dobra(a2)
                    fs = [fundo(M2 @ q) for q in p2]
                    if max(fs) > tol:
                        continue
                    g2 = sum(max(0.0, -fundo(M2 @ q)) for q in pad2) / max(1, len(pad2))
                    gp = sum(max(0.0, -fundo(M2 @ q)) for q in ponta) / max(1, len(ponta))
                    c = 1000.0 * (0.5 * g1 + 2.0 * g2 + 2.0 * gp)
                    c += 0.3 * max(0.0, dmin1 - a1) + 0.3 * max(0.0, dmin2 - a2)
                    c += 0.02 * abs(abd) + 0.004 * abs(a2 - a1)
                    if zmax is not None:
                        zt = max((Wi @ (M2 @ q)).z for q in ponta)
                        c += 1000.0 * p.get("peso_z", 0.5) * max(0.0, zt - zmax)
                    eixo = _eixo_dedo(M2, n2, y2)
                    vz = None
                    if ant is not None:
                        vz = (_lado_a_lado(eixo, ant[0]) - raio - ant[1]) * 1000.0
                        vmin = (_dist_eixos(eixo, ant[0]) - raio - ant[1]) * 1000.0
                        c += p.get("peso_vizinho", 1.5) * max(0.0, vz - vviz) + 20.0 * max(0.0, -1.0 - vmin)
                        e1x = _eixo_dedo(M1, n1, y1)
                        v1 = (_dist_eixos(e1x, ant[2]) - raio - ant[1]) * 1000.0
                        c += 20.0 * max(0.0, -1.0 - v1)
                    if melhor is None or c < melhor[0]:
                        melhor = (c, a1, a2, abd, eixo, _eixo_dedo(M1, n1, y1), g1, g2, gp, vz, max(e1, max(fs)))
        if melhor is None:
            melhor = (0, 0, 10, 0, None, None, 0, 0, 0, None, 0)
        c, a1, a2, abd, eixo, eixo1, g1, g2, gp, vz, ent = melhor
        ant = (eixo, raio, eixo1) if eixo is not None else ant
        giros[n1], giros[n2] = _q_dedo(a1, abd), pg._dobra(a2)
        info[d] = (a1, a2, abd, round(g1 * 1000, 1), round(g2 * 1000, 1), round(gp * 1000, 1), None if vz is None else round(vz, 1), round(ent * 1000, 1))
    return giros, info


def fechar_dedos(esq, lado, Hm, fundo, pts, p):
    """Os giros locais dos dedos (4x4): cada dedo fecha a base até a pele encostar na arma e depois a ponta; "dedos" em
    p dá, por dedo, (a1, a2, abertura) fixos (None = automático), "pre" a dobra da ponta enquanto a base fecha, "tol" o
    quanto a pele pode entrar (m), "max" os limites (a1, a2), "abre" a abertura para o lado de cada dedo."""
    rest, pai = esq.rest, esq.pai
    tol = p.get("tol", 0.0015)
    pre = p.get("pre", 25.0)
    a1max, a2max = p.get("max", (95.0, 100.0))
    fixos = p.get("dedos", {})
    abre = p.get("abre", {})
    giros, info = {}, {}
    for d in DEDOS:
        n1, n2 = f"{d}1.{lado}", f"{d}2.{lado}"
        R1 = rest[pai[n1]].inverted() @ rest[n1]
        R2 = rest[pai[n2]].inverted() @ rest[n2]
        p1 = [q for q in pts.get(n1, []) if q.y > 0.014]
        p2 = [q for q in pts.get(n2, []) if q.y > 0.004]
        y2 = max(q.y for q in p2)
        ponta = [q for q in p2 if q.y > y2 - 0.014 and q.z < 0.002]
        abd = abre.get(d, 0.0)

        def pior(a1, a2):
            M1 = Hm @ R1 @ _q_dedo(a1, abd)
            M2 = M1 @ R2 @ pg._dobra(a2)
            return max(max((fundo(M1 @ q) for q in p1), default=-1.0), max((fundo(M2 @ q) for q in p2), default=-1.0))

        q1 = [q for q in p1 if q.z < 0.002]
        q2 = [q for q in p2 if q.z < 0.002]

        def folga(a1, a2):
            M1 = Hm @ R1 @ _q_dedo(a1, abd)
            M2 = M1 @ R2 @ pg._dobra(a2)
            f1 = sum(max(0.0, -fundo(M1 @ q)) for q in q1) / max(1, len(q1))
            f2 = sum(max(0.0, -fundo(M2 @ q)) for q in q2) / max(1, len(q2))
            f3 = sum(max(0.0, -fundo(M2 @ q)) for q in ponta) / max(1, len(ponta))
            return f1 + 2.0 * f2 + p.get("peso_ponta", 2.0) * f3

        fx = fixos.get(d)
        if fx:
            a1, a2 = fx[0], fx[1]
            if len(fx) > 2:
                abd = fx[2]
        elif p.get("modo_dedos", "justo") == "justo":
            # a dobra das duas falanges que deixa o dedo mais encostado na arma sem entrar nela
            melhor = None
            lim = p.get("lim_dedo", {}).get(d, (0, a1max, 10, a2max))
            for x1 in range(int(lim[0]), int(lim[1]) + 1, 4):
                for x2 in range(int(lim[2]), int(lim[3]) + 1, 5):
                    if pior(x1, x2) > tol:
                        continue
                    c = folga(x1, x2) + 0.00002 * abs(x2 - 0.9 * x1)
                    if melhor is None or c < melhor[0]:
                        melhor = (c, x1, x2)
            if melhor is None:
                melhor = (0, lim[0], lim[2])
            _, a1, a2 = melhor
        else:
            # como a mão fecha: a base dobra até a falange encostar; depois a ponta dobra até encostar
            pre_d = p.get("pre_dedo", {}).get(d, pre)
            p2b = [q for q in p2 if q.y > 0.014]

            def toca1(a):
                M1 = Hm @ R1 @ _q_dedo(a, abd)
                M2 = M1 @ R2 @ pg._dobra(pre_d)
                return max(max((fundo(M1 @ q) for q in p1), default=-1.0), max((fundo(M2 @ q) for q in p2b), default=-1.0))

            def toca2(a1, a):
                M2 = Hm @ R1 @ _q_dedo(a1, abd) @ R2 @ pg._dobra(a)
                return max((fundo(M2 @ q) for q in p2b), default=-1.0)

            lim = p.get("lim_dedo", {}).get(d, (0, a1max, pre_d, a2max))
            a1 = lim[0]
            for a in range(int(lim[0]), int(lim[1]) + 1, 2):
                if toca1(a) > tol:
                    break
                a1 = a
            a2 = lim[2]
            for a in range(int(lim[2]), int(lim[3]) + 1, 2):
                if toca2(a1, a) > tol:
                    break
                a2 = a
        giros[n1], giros[n2] = _q_dedo(a1, abd), pg._dobra(a2)
        info[d] = (round(a1), round(a2), round(pior(a1, a2) * 1000, 1))
    return giros, info


def polegar(esq, lado, Hm, fundo, pts, p, Wg):
    """O polegar: "polegar" em p = (ax, ay, az, a3) fixo, ou "polegar_alvo" = o ponto da arma (espaço da arma) onde a
    ponta deita (uma busca nos giros da base e na dobra da ponta, sem entrar na arma)."""
    rest, pai = esq.rest, esq.pai
    nt, nt2 = "thumb." + lado, "thumb2." + lado
    R1 = rest[pai[nt]].inverted() @ rest[nt]
    R2 = rest[pai[nt2]].inverted() @ rest[nt2]
    p1 = [q for q in pts.get(nt, []) if q.y > 0.012][::2]
    p2 = [q for q in pts.get(nt2, [])]
    ymax = max(q.y for q in p2)
    ponta = [q for q in p2 if q.y > ymax - 0.012]
    cp = sum(ponta, Vector()) / len(ponta)

    def Q(ax, ay, az):
        return (Matrix.Rotation(rad(ax), 4, "X") @ Matrix.Rotation(rad(ay), 4, "Y") @ Matrix.Rotation(rad(az), 4, "Z"))

    if "polegar" in p:
        ax, ay, az, a3 = p["polegar"]
        M1 = Hm @ R1 @ Q(ax, ay, az)
        M2 = M1 @ R2 @ pg._dobra(a3)
        pen = max(max((fundo(M1 @ q) for q in p1), default=-1), max(fundo(M2 @ q) for q in p2))
        return {nt: Q(ax, ay, az), nt2: pg._dobra(a3)}, (ax, ay, az, a3, round(pen * 1000, 1))
    if p.get("polegar_segue"):
        # (rodada 3) o alvo do polegar acompanha a mão: o alvo de uma pegada de referência, no espaço da mão
        ref = dict(p); ref.update(p["polegar_segue"])
        T = Hm @ (mao_na_arma(lado, ref).inverted() @ Vector(ref["polegar_alvo"]))
    else:
        T = Wg @ Vector(p["polegar_alvo"])
    tol = p.get("tol_polegar", 0.0015)
    melhor = None
    passos = p.get("polegar_passos", (range(-70, 71, 10), range(-80, 81, 10), range(-70, 71, 10), (0, 15, 30, 45)))
    for ax in passos[0]:
        for ay in passos[1]:
            for az in passos[2]:
                M1 = Hm @ R1 @ Q(ax, ay, az)
                pen1 = max((fundo(M1 @ q) for q in p1), default=-1.0)
                if pen1 > tol + 0.002:
                    continue
                for a3 in passos[3]:
                    M2 = M1 @ R2 @ pg._dobra(a3)
                    fs = [fundo(M2 @ q) for q in p2]
                    pen = max(pen1, max(fs))
                    if pen > tol and not p.get("polegar_frouxo"):
                        continue
                    vao = max(0.0, -max(fs))
                    f1 = [max(0.0, -fundo(M1 @ q)) for q in p1]
                    enc = (sum(f1) / max(1, len(f1)) + sum(max(0.0, -f) for f in fs) / len(fs)) * 1000
                    c = (M2 @ cp - T).length * 1000 + p.get("peso_encosto", 0.0) * enc + 4000.0 * max(0.0, pen - tol) + p.get("peso_vao_polegar", 1500.0) * vao + 0.02 * (abs(ax) + abs(ay) + abs(az))
                    if melhor is None or c < melhor[0]:
                        melhor = (c, (ax, ay, az, a3), pen, (M2 @ cp - T).length)
    if melhor is None:
        q = dict(p)
        q["polegar_frouxo"] = 1
        return polegar(esq, lado, Hm, fundo, pts, q, Wg)
    if p.get("polegar_fino") and not p.get("_fino"):
        # (rodada 3) refina em volta do melhor, de 5 em 5 graus
        bx, by, bz, b3 = melhor[1]
        q = dict(p)
        q["_fino"] = 1
        q["polegar_passos"] = (range(bx - 10, bx + 11, 5), range(by - 10, by + 11, 5), range(bz - 10, bz + 11, 5),
                               sorted({max(0, b3 - 15), max(0, b3 - 8), b3, b3 + 8, b3 + 15}))
        return polegar(esq, lado, Hm, fundo, pts, q, Wg)
    c, (ax, ay, az, a3), pen, dist = melhor
    return {nt: Q(ax, ay, az), nt2: pg._dobra(a3)}, (ax, ay, az, a3, round(pen * 1000, 1), round(dist * 1000))


def indicador_no_gatilho(esq, lado, Hm, fundo, pts, T, p):
    """O indicador com a almofada da ponta no gatilho T (no espaço da armadura)."""
    rest, pai = esq.rest, esq.pai
    n1, n2 = "index1." + lado, "index2." + lado
    R1 = rest[pai[n1]].inverted() @ rest[n1]
    R2 = rest[pai[n2]].inverted() @ rest[n2]
    p2 = pts.get(n2, [])
    ymax = max(q.y for q in p2)
    almofada = Vector((0.0, ymax - 0.014, -0.008))
    melhor = None
    for a1 in range(-10, 81, 5):
        for ab in range(-25, 26, 5):
            M1 = Hm @ R1 @ _q_dedo(a1, ab)
            for a2 in range(0, 96, 5):
                M2 = M1 @ R2 @ pg._dobra(a2)
                d = (M2 @ almofada - T).length
                c = d + 0.00004 * (abs(ab) + 0.2 * a1 + 0.2 * a2)
                if melhor is None or c < melhor[0]:
                    melhor = (c, (a1, ab, a2), d)
    c, (a1, ab, a2), d = melhor
    return {n1: _q_dedo(a1, ab), n2: pg._dobra(a2)}, (a1, ab, a2, round(d * 1000))


def dedos_da_pegada(c, lado, p, M_idle, E):
    """Os giros locais dos dedos (4x4) da pegada do lado, medidos no Idle 1 (a arma parada)."""
    esq = c["esq"]
    rest = esq.rest
    Wg = pf._da_arma(M_idle, rest, p["osso"], E)
    Hm = Wg @ mao_na_arma(lado, p)
    fundo = Fundura(bvh_da_arma(c))
    pts = pg.pontos_da_mao_nova()
    if p.get("modo_dedos") == "fila":
        giros, info = dedos_em_fila(esq, lado, Hm, fundo, pts, p, Wg)
    else:
        giros, info = fechar_dedos(esq, lado, Hm, fundo, pts, p)
    gp, ip = polegar(esq, lado, Hm, fundo, pts, p, Wg)
    giros.update(gp)
    ig = None
    if p.get("gatilho"):
        gg, ig = indicador_no_gatilho(esq, lado, Hm, fundo, pts, Wg @ Vector(p["gatilho"]), p)
        if ig[3] <= p.get("gatilho_max", 12):
            giros.update(gg)
    print(f"  DEDOS {lado}: {info} polegar {ip} gatilho {ig}")
    return giros


def posar(esq, M, M0, lado, p, E, dedos, w=0.0, H_caminho=None, H_perto=None, fixo=None, so_cotovelo=False, prev=None):
    """Põe o braço do lado na pegada (w = 0) ou no caminho de antes (w = 1; no meio, a mistura): a mão na arma, o ombro
    deslocado (p["ombro"], no espaço da arma) e o cotovelo para perto do de antes (mais p["cot"], no espaço da arma)."""
    rest = esq.rest
    Wg = pf._da_arma(M0, rest, p["osso"], E)
    H = H_perto if H_perto is not None else Wg @ mao_na_arma(lado, p)
    if w > 0:
        # no caminho de antes: o lugar da mão de antes, com o giro da pegada nova (mais o giro que a de antes faz)
        Hc = (H_caminho if H_caminho is not None else M0["hand." + lado]).copy()
        if p.get("giro_caminho", "antes") == "pegada":
            t = Hc.translation.copy()
            Hc = H.copy()
            Hc.translation = t
        if p.get("_giro"):
            # (rodada 3) neste quadro, o giro da mão vai em parte para o da pegada
            t = Hc.translation.copy()
            Hc = Hc.to_quaternion().slerp(H.to_quaternion(), p["_giro"]).to_matrix().to_4x4()
            Hc.translation = t
        H = pf._misturar(H, Hc, w)
    if p.get("_mover") is not None:
        # (rodada 3) neste quadro, a mão vai um pouco para o lado (um vetor no espaço da armadura)
        H = H.copy()
        H.translation = H.translation + Vector(p["_mover"])
    Mas = dict(M0)
    m = Mas["upper_arm." + lado].copy()
    # (o ombro deslocado no espaço da armadura, o da câmera; no caminho de antes ele fica deslocado: com o ombro de
    # antes, a mão de palma para cima fica a mais de 140 graus de torção)
    S = m.translation + Vector(p.get("ombro", (0, 0, 0))) * (1.0 - w * p.get("solta_ombro", 0.0))
    E_alvo = M0["forearm." + lado].translation + Vector(p.get("cot", (0, 0, 0)))
    if p.get("S_arma") is not None:
        # o braço acompanha a arma: o ombro preso (em parte) ao osso gun e o cotovelo puxado para o lugar dele no espaço
        # da peça que a mão segura (o pulso fica como no repouso quando a arma gira ou sobe); no caminho de antes (w),
        # o cotovelo volta ao de antes
        Wa = pf._da_arma(M0, rest, "gun", E)
        S = S.lerp(Wa @ Vector(p["S_arma"]), p.get("rigido_ombro", 1.0) * (1.0 - w))
        E_alvo = E_alvo.lerp(Wg @ Vector(p["E_osso"]), p.get("rigido_cot", 1.0) * (1.0 - w))
    if w > 0 and p.get("E_mao") is not None:
        # no caminho de antes, o cotovelo vai para o lugar dele em relação à mão (como na pegada): o cotovelo de antes
        # ficava à frente do pulso, com o antebraço voltado para a câmera e o pulso dobrado mais de 100 graus
        E_alvo = E_alvo.lerp(H @ Vector(p["E_mao"]), w)
    if fixo is not None:
        # o cotovelo e o ombro já escolhidos (e suavizados no tempo) por cotovelo_por_quadro
        E_alvo, S = fixo
    elif p.get("por_quadro"):
        # o cotovelo escolhido neste quadro: o pulso reto com o antebraço cruzando a tela como o de antes (o cotovelo
        # na tela perto do de antes), perto do cotovelo preferido (E_alvo); o ombro na esfera em volta dele
        Ec, S = cotovelo_por_quadro(esq, lado, H, S, E_alvo, M0["forearm." + lado].translation, p, w, prev,
                                    W0=M0["hand." + lado].translation)
        if so_cotovelo:
            return Ec, S, H
        E_alvo = Ec
    m.translation = S
    Mas["upper_arm." + lado] = m
    polo = E_alvo - S
    if p.get("polo_th") is not None:
        u = (H.translation - S).normalized()
        polo = Matrix.Rotation(rad(p["polo_th"]), 3, u) @ Vector((0.0, 0.0, -1.0))
    ds = None
    if dedos is not None:
        ds = {}
        for n, g in dedos.items():
            q = g.to_quaternion()
            if p.get("_w_dedos") is not None:
                q = q.slerp(esq.base(M0, n).to_quaternion(), p["_w_dedos"])
            elif w > 0:
                q = q.slerp(esq.base(M0, n).to_quaternion(), w * p.get("abre_dedos", 0.6))
            ds[n] = q
    av = dm.braco_no_lugar(esq, M, Mas, lado, H, polo, ds)
    return av, Mas, polo, H, ds


def cotovelo_por_quadro(esq, lado, H, S_pref, E_pref, E0, p, w=0.0, prev=None, W0=None):
    """O cotovelo (e o ombro) de um quadro: a direção do antebraço que deixa o pulso reto (a dobra até dobra_boa, o
    desvio até 15, a torção baixa), o cotovelo na tela perto do de antes (E0) e perto do preferido (E_pref). O ombro é
    o preferido (S_pref) puxado para a esfera em volta do cotovelo e, com pq_gira_ombro, girado em volta do antebraço
    (o braço todo gira no ombro, o cotovelo para fora) para tirar a torção do antebraço. Busca em três passos."""
    ua, fa, mao = "upper_arm." + lado, "forearm." + lado, "hand." + lado
    a, b = esq.comprimento(ua, fa), esq.comprimento(fa, mao)
    W = H.translation
    t0 = na_tela(E0)
    k_meio = p.get("pq_k_meio", 0.5)
    m0 = na_tela(E0.lerp(W0, k_meio)) if W0 is not None else None
    pm = p.get("pq_meio", 0.0)
    pt, pp, po = p.get("pq_tela", 600.0), p.get("pq_pref", 150.0), p.get("pq_ombro", 150.0)
    db = p.get("dobra_boa", 50.0)

    def avaliar(Ec, S):
        M = {ua: Matrix.Translation(S), fa: Matrix.Translation(Ec), mao: H}
        dobra, desvio, tor = dm.pulso(esq, M, lado)
        c = 0.4 * abs(dobra) + 4.0 * max(0.0, abs(dobra) - db) + 40.0 * max(0.0, abs(dobra) - p.get("dobra_corte", 58.0))
        c += 0.6 * abs(desvio) + 6.0 * max(0.0, abs(desvio) - 15.0) + 40.0 * max(0.0, abs(desvio) - p.get("desvio_corte", 24.0))
        c += p.get("pq_tor", 0.3) * abs(tor) + 4.0 * max(0.0, abs(tor) - p.get("tor_boa", 50.0)) + 40.0 * max(0.0, abs(tor) - 85.0)
        c += 5000.0 * max(0.0, 0.15 - perto_da_camera(Ec, W)) + 5000.0 * max(0.0, 0.13 - perto_da_camera(S, Ec))
        c += 300.0 * visivel(S, Ec, 0.06) + po * (S - S_pref).length
        if p.get("ombro_visivel") and w > 0:
            # (rodada 3) o ombro e o braço de cima (a malha é mais grossa ali e passa do ombro) fora da tela
            ext = S + (S - Ec).normalized() * 0.12
            c += p["ombro_visivel"] * (visivel(S, ext, 0.10, 5) + visivel(S, Ec, 0.09))
        ang = math.degrees((S - Ec).angle(W - Ec))
        c += 20.0 * max(0.0, 40.0 - ang) + 20.0 * max(0.0, ang - 170.0)
        return c, tor

    def custo(az, el):
        d = Vector((math.cos(rad(el)) * math.cos(rad(az)), math.cos(rad(el)) * math.sin(rad(az)), math.sin(rad(el))))
        Ec = W - d * b
        u = S_pref - Ec
        S0 = Ec + (u.normalized() if u.length > 1e-6 else Vector((0.0, 0.3, 0.1)).normalized()) * a
        c, tor0 = avaliar(Ec, S0)
        melhor = (c, Ec, S0)
        if p.get("pq_gira_ombro") and abs(tor0) > 5.0:
            eixo = (W - Ec).normalized()
            gira = lambda th: Ec + Matrix.Rotation(rad(th), 3, eixo) @ (S0 - Ec)  # noqa: E731
            _, t1 = avaliar(Ec, gira(10.0))
            k = (t1 - tor0) / 10.0
            if abs(k) > 0.3:
                for fr in (0.35, 0.7, 1.0):
                    S = gira(-fr * tor0 / k)
                    c2, _ = avaliar(Ec, S)
                    if c2 < melhor[0]:
                        melhor = (c2, Ec, S)
        c, Ec, S = melhor
        t = na_tela(Ec)
        if t0 is not None:
            c += pt * (math.hypot(t[0] - t0[0], t[1] - t0[1]) if t is not None else 3.0)
        if m0 is not None and pm > 0:
            m = na_tela(Ec.lerp(W, k_meio))
            c += pm * (math.hypot(m[0] - m0[0], m[1] - m0[1]) if m is not None else 3.0)
        c += pp * (Ec - E_pref).length
        if prev is not None:
            c += p.get("pq_cont", 300.0) * (Ec - prev).length
        return c, Ec, S

    melhor = (None, None)
    for az in range(0, 360, 10):
        for el in range(-80, 81, 10):
            r = custo(az, el)
            if melhor[0] is None or r[0] < melhor[0][0]:
                melhor = (r, (az, el))
    for passo, raio in ((3, 12), (1, 4)):
        az0, el0 = melhor[1]
        for i in range(-raio, raio + 1, passo):
            for j in range(-raio, raio + 1, passo):
                if (i, j) == (0, 0) or abs(el0 + j) > 89:
                    continue
                r = custo(az0 + i, el0 + j)
                if r[0] < melhor[0][0]:
                    melhor = (r, (az0 + i, el0 + j))
    return melhor[0][1], melhor[0][2]


# ================================================================================================ a tela e as buscas

TAN_Y = math.tan(math.radians(27.5))
TAN_X = TAN_Y * 16.0 / 9.0


def na_tela(P):
    """(x, y, fundo) de um ponto do espaço da armadura na tela da câmera do jogo (-1..1; x + para a direita)."""
    d = -P.y
    if d < 1e-4:
        return None
    return (-P.x / d / TAN_X, P.z / d / TAN_Y, d)


def visivel(A, B, raio=0.045, n=9):
    """Quantos pontos do segmento A-B (um braço de raio dado) aparecem na tela."""
    k = 0
    for i in range(n):
        P = A.lerp(B, i / (n - 1))
        t = na_tela(P)
        if t is None:
            continue
        m = raio / t[2]
        if abs(t[0]) < 1 + m / TAN_X and abs(t[1]) < 1 + m / TAN_Y:
            k += 1
    return k


def perto_da_camera(A, B, n=9):
    return min(A.lerp(B, i / (n - 1)).length for i in range(n))


def _posar_copia(esq, M0, lado, p, E):
    M = dict(M0)
    av = posar(esq, M, M0, lado, p, E, None)[0]
    return M, av


def buscar_braco(esq, M0, lado, p, E, grade, ths=None, quieto=False):
    """O deslocamento do ombro (e o giro do cotovelo, se ths) que deixa o pulso mais reto, com o braço de cima fora da
    tela e o cotovelo na tela perto do de antes (L) ou o braço todo fora da tela (R). Devolve (ombro, th)."""
    E0, S0 = M0["forearm." + lado].translation, M0["upper_arm." + lado].translation
    t0 = na_tela(E0)
    achados = []
    for dx in grade[0]:
        for dy in grade[1]:
            for dz in grade[2]:
                for th in (ths or (None,)):
                    q = dict(p)
                    q["ombro"] = (dx, dy, dz)
                    if th is not None:
                        q["polo_th"] = th
                    M, av = _posar_copia(esq, M0, lado, q, E)
                    S, Ec, W = (M[f"{n}.{lado}"].translation for n in ("upper_arm", "forearm", "hand"))
                    dobra, desvio, tor = dm.pulso(esq, M, lado)
                    c = abs(tor) * p.get("peso_tor", 1.0) + 4.0 * max(0.0, abs(dobra) - 35.0) + abs(dobra) * 0.3
                    c += 6.0 * max(0.0, abs(desvio) - 15.0) + abs(desvio) * 0.5 + 3000.0 * av
                    c += 2000.0 * visivel(S, Ec, 0.06)
                    c += 3000.0 * max(0.0, 0.14 - min(perto_da_camera(S, Ec), perto_da_camera(Ec, W)))
                    if lado == "L":
                        t = na_tela(Ec)
                        if t is None:
                            continue
                        c += p.get("peso_tela", 150.0) * math.hypot(t[0] - t0[0], t[1] - t0[1])
                        c += p.get("peso_cot", 100.0) * (Ec - E0).length
                    else:
                        c += 2000.0 * visivel(Ec, W, 0.05)
                        c += 60.0 * (Vector((dx, dy, dz))).length
                        # o cotovelo direito para a direita e para baixo do pulso, nunca acima dele
                        c += 500.0 * max(0.0, Ec.z - W.z + 0.03) + 500.0 * max(0.0, Ec.x - W.x + 0.02)
                    achados.append((c, (dx, dy, dz), th, (round(dobra), round(desvio), round(tor)), round(av * 1000),
                                    tuple(round(v, 3) for v in Ec)))
    achados.sort(key=lambda a: a[0])
    if not quieto:
        for a in achados[:4]:
            print(f"  BUSCA {lado}: custo {a[0]:.0f} ombro {a[1]} th {a[2]} pulso {a[3]} avanço {a[4]} mm cotovelo {a[5]}")
    return achados[0][1], achados[0][2]


def buscar_direto(esq, M0, lado, p, E, quieto=False):
    """O cotovelo e o ombro escolhidos direto (no Idle 1): primeiro a direção do antebraço que deixa o pulso menos
    dobrado (L: o cotovelo na tela perto do de antes; R: o cotovelo à direita e abaixo do pulso, o braço fora da tela),
    depois o ombro (na esfera em volta do cotovelo, fora da tela) que deixa a torção menor. Devolve (ombro, cot): os
    deslocamentos do ombro e do cotovelo de antes (espaço da armadura)."""
    rest = esq.rest
    ua, fa, mao = "upper_arm." + lado, "forearm." + lado, "hand." + lado
    a, b = esq.comprimento(ua, fa), esq.comprimento(fa, mao)
    Wg = pf._da_arma(M0, rest, p["osso"], E)
    H = Wg @ mao_na_arma(lado, p)
    W = H.translation
    E0, S0 = M0[fa].translation, M0[ua].translation
    t0 = na_tela(E0)
    rel = rest[fa].to_3x3().inverted() @ rest[mao].to_3x3()

    def angulos(Ec, S):
        M = {ua: Matrix.Translation(S), fa: Matrix.Translation(Ec), mao: H}
        return dm.pulso(esq, M, lado)

    # 1) o cotovelo
    cands = []
    n = 0
    for az in range(0, 360, 6):
        for el in range(-90, 91, 6):
            d = Vector((math.cos(rad(el)) * math.cos(rad(az)), math.cos(rad(el)) * math.sin(rad(az)), math.sin(rad(el))))
            Ec = W - d * b          # d: a direção do antebraço (do cotovelo para o pulso)
            dobra, desvio, _ = angulos(Ec, Ec + Vector((0.0, 0.3, 0.1)))
            c = abs(dobra) + 1.5 * abs(desvio) + 4.0 * max(0.0, abs(dobra) - p.get("dobra_boa", 35.0)) + 8.0 * max(0.0, abs(desvio) - 15.0)
            c += 5000.0 * max(0.0, 0.15 - perto_da_camera(Ec, W))
            if lado == "L":
                t = na_tela(Ec)
                if t is None:
                    continue
                c += p.get("peso_tela", 300.0) * math.hypot(t[0] - t0[0], t[1] - t0[1]) + p.get("peso_cot", 150.0) * (Ec - E0).length
            else:
                c += 300.0 * visivel(Ec, W, 0.05)
                c += 3000.0 * (max(0.0, Ec.x - W.x + 0.08) + max(0.0, Ec.z - W.z - p.get("cot_acima", 0.10))
                               + max(0.0, W.y + p.get("cot_atras", -0.05) - Ec.y))
                c += p.get("peso_altura", 100.0) * max(0.0, Ec.z - W.z)
            cands.append((c, Ec, round(dobra), round(desvio)))
    cands.sort(key=lambda x: x[0])
    c1, Ec, dobra, desvio = cands[0]
    # 2) o ombro
    alvo = Vector(p["ombro_alvo"]) if p.get("ombro_alvo") else S0
    melhor = None
    for az in range(0, 360, 6):
        for el in range(-90, 91, 6):
            d = Vector((math.cos(rad(el)) * math.cos(rad(az)), math.cos(rad(el)) * math.sin(rad(az)), math.sin(rad(el))))
            S = Ec + d * a
            ang = math.degrees((S - Ec).angle(W - Ec))
            if ang < 35.0 or ang > 172.0:
                continue
            _, _, tor = angulos(Ec, S)
            c = abs(tor) + 4.0 * max(0.0, abs(tor) - p.get("tor_boa", 50.0)) + p.get("peso_ombro", 60.0) * (S - alvo).length
            c += 300.0 * visivel(S, Ec, 0.06) + 5000.0 * max(0.0, p.get("perto_min", 0.13) - perto_da_camera(S, Ec))
            if melhor is None or c < melhor[0]:
                melhor = (c, S, round(tor))
    if melhor is None:
        print(f"  DIRETO {lado}: sem ombro possível para o cotovelo {tuple(round(k, 3) for k in Ec)}")
        melhor = (0, S0, 999)
    c2, S, tor = melhor
    if not quieto:
        v3 = lambda x: tuple(round(k, 3) for k in x)  # noqa: E731
        print(f"  DIRETO {lado}: cotovelo {v3(Ec)} (antes {v3(E0)}) dobra {dobra} desvio {desvio}; ombro {v3(S)} (antes {v3(S0)}) torção {tor}")
    return tuple(S - S0), tuple(Ec - E0)
'''



if __name__ == "__main__" and "--" in sys.argv:
    # este arquivo de novo, como o módulo pegada_fogo (o que os motores importam), e o principal() dele
    importlib.import_module("pegada_fogo").principal()
