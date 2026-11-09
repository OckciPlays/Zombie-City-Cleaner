# A mão nova do jogador (08/11/2026, a pedido: "A mão dele eu não gosto muito, acho que está parecida muito com um
# boneco de madeira, está muito quadrado e não combina com o personagem, acho que precisamos deixar mais redondo, com
# detalhes nas unhas, alguns pelos"; e depois: "não é para deixar em um nível realista que destoe da realidade do game,
# ainda continuaria low-poly e que se encaixaria com a estética do personagem"; aprovada: "Aprovo a mão, está muito
# boa"), e com ela o antebraço novo (no mesmo dia: "o pulso está com um aspecto mais quadrado e não está se encaixando
# tão bem com a mão, tem como deixar conexo as duas partes? ... o pulso de um humano precisa ser algo contínuo que se
# afina aos poucos"; e "só toma cuidado para não afinar muito o braço e ficar irregular. Qualquer coisa pode preencher um
# pouco com volume a mão também"). As pegadas das armas brancas vêm depois (aqui só a prévia de uma, num cabo, nas fotos).
#
# Feita por script num Blender sem janela, a partir dos FBX que já estão no projeto (que só são lidos, nunca gravados de
# novo), em dois FBX novos, cada um com uma peça por lado (o antebraço e a mão numa malha só, sem costura no pulso):
#
#   Assets/_Game/Art/Characters/Jogador/Maos_Novas_FP.fbx
#       o rig dos braços de primeira pessoa (FP_Rig_Maos, o do Jogador_FP_Maos.fbx: os mesmos nomes, a mesma hierarquia
#       e o mesmo repouso, sem as ações) e as peças Mao_FP.L e Mao_FP.R, com o material dos braços (PrincipalBloco, a
#       textura do personagem).
#   Assets/_Game/Art/Characters/Personagens/Maos_Novas_Corpo.fbx
#       o esqueleto do corpo (PrincipalBloco_Rig, o do Personagem_PrincipalBloco.fbx e do Roupas.fbx) e as peças
#       Mao_Corpo.L e Mao_Corpo.R, com o material do corpo (PrincipalBloco), o UV "Face" do NPC_Corpo das roupas (x: o
#       número da face velha mais perto, do mesmo osso dono, para as luvas e as mangas esconderem as faces certas; y: -1,
#       a camada do corpo no shader das roupas) e a forma "Mulher" (a do NPC_Corpo, passada osso a osso; no primeiro anel,
#       a dos cantos do braço de cima de hoje, para a emenda continuar fechada). Desde 09/11/2026, também as peças
#       BracoCima_Corpo.L e .R: o braço de cima novo, arredondado e liso, da borda do ombro ao primeiro anel da Mao_Corpo
#       (o Blender/braco_de_cima.py, que diz como ele é feito e o que a MontagemDaMaoNova precisa mudar para usá-lo).
#       As etapas daqui que leem as mãos pegam só a Mao_Corpo (as luvas, o conferir das roupas); as fotos do corpo
#       (faces_do_corte) tiram também o braço de cima de hoje quando a BracoCima vem junto.
#   Assets/_Game/Art/Characters/Roupas/Luvas_Novas.fbx (a fase 2, 08/11/2026)
#       as duas luvas refeitas em volta da mão nova, no formato das peças do Roupas.fbx (veja "as luvas", mais abaixo):
#       o PrincipalBloco_Rig, Roupa_Luva_Tatica e Roupa_Luva_Sem_Dedos (as duas mãos numa malha), para trocar as duas
#       peças do Roupas.fbx pelo nome; o que cada uma esconde do NPC_Corpo (com o braço novo) vai para o
#       luvas_esconde.json da pasta das fotos (o "esconde" do Roupas.json).
#
# A mulher (a fase 2: "uma versão para a mulher, onde é a mesma coisa, porém sem os pelos no braço"): na forma "Mulher"
# os pelos se recolhem para dentro da pele (cada fio vira um ponto 0,8 mm abaixo dela: área zero e por dentro), e a forma
# "SemPelos" faz só isso, no homem também (nos dois FBX). Assim a Vestimenta (a "Mulher" a 100) e as cópias dos braços de
# primeira pessoa (o RoupasNosBracos copia as formas) já ficam sem pelos, sem nada novo na Unity.
#
# O braço de hoje (o mesmo nos braços de primeira pessoa, no Jogador_Corpo e no NPC_Corpo, no espaço dos ossos): caixas
# chanfradas presas inteiras a um osso cada (o peso 1 num osso só). A mão: a palma no hand.X (32 vértices), o polegar no
# thumb.X e no thumb2.X (32 cada) e cada falange no index1/2, middle1/2, ring1/2 e pinky1/2 (42 cada); 432 vértices,
# 452 faces e 816 triângulos. O antebraço (forearm.X, 48 vértices, 45 faces): um bloco de ~10 x 10 cm do começo (~5 cm
# depois do cotovelo) até ~5 cm antes do pulso e um bloco menor no pulso (6,1 x 7,3 cm, com uma tampa 6,5 mm depois
# dele). O braço de cima (upper_arm.X) termina ~2,7 cm antes do cotovelo, num anel de 8 vértices; entre ele e o
# antebraço, 8 faces de emenda (um anel em cada osso, que estica quando o cotovelo dobra).
# O corte (o que sai das malhas de hoje): as faces do material 0 (a pele) com ALGUM vértice com mais da metade do peso no
# forearm.X ou nos ossos da mão e dos dedos (hand, thumb, thumb2, index1/2, middle1/2, ring1/2, pinky1/2) do mesmo lado:
# a mão (452 faces), o antebraço (45) e a emenda do cotovelo (8): 505 faces e 914 triângulos por lado, iguais nos 19
# Jogador_FP_*, no Jogador_Corpo e no NPC_Corpo. Fica aberto só o anel do fim do braço de cima (8 arestas), e a peça nova
# começa em cima dele. Nos braços com uma arma branca (o bastão, a faca, a lança, o machado e a tocha) a arma também pesa
# no hand.R, mas no material 1 (fica).
#
# A peça nova (low-poly arredondada e lisa: 1.324 triângulos e 776 vértices por lado):
# - o antebraço em anéis de 20 vértices: o primeiro em cima do anel do fim do braço de cima de hoje (os 8 cantos exatos e
#   os outros nas arestas entre eles; o peso todo no upper_arm), a junta do cotovelo (metade no upper_arm, metade no
#   forearm), um anel 3,5 cm depois (já quase todo no forearm) e quatro que afinam até o pulso. Perto do cotovelo, o
#   volume de hoje (9,8 x 9,6 cm), um pouco achatado; a metade de cima quase não afina, e daí até o pulso afina aos poucos
#   (smoothstep ** 2,2: sem cintura nem calombo), arredondando (de quase o bloco a uma superelipse redonda);
# - o pulso (6,8 x 5,0 cm, metade no forearm, metade no hand) passa para a palma sem degrau: a palma é mais cheia (4,7 a
#   3,5 cm de grossura, 7 a 8,3 cm de largura) e afina até os nós dos dedos;
# - a mão: a palma em anéis de 20 vértices (o dorso abaulado, a almofada do polegar e a do lado do mínimo, a palma um pouco
#   funda no meio, os nós dos dedos marcados de leve), os dedos em prismas de 8 lados que afinam até a ponta arredondada
#   (com as juntas um pouco mais grossas e as almofadas das pontas), o polegar saindo da almofada dele, as unhas (placas
#   planas em 3 fileiras, de contorno arredondado, 0,7 mm acima da pele nas últimas falanges, com uma borda escura que
#   entra nela) e alguns pelos (11 fios finos e curtos no dorso da mão e 15 no antebraço, deitados 0,15 a 0,35 mm acima
#   da pele; nos dedos eles espetavam a silhueta do punho fechado).
# Os pesos são suaves no cotovelo, no pulso, nos nós dos dedos e nas juntas do meio (até 3 ossos por vértice, somando 1).
# A pele é lisa (as normais suaves; com FACETADA = True ela sai chapada, como o resto do corpo); as unhas e os pelos,
# chapados. A malha é fechada, menos a boca do cotovelo (em cima do fim do braço de cima), o fundo das unhas e os pelos.
#
# As roupas (o conferir e as fotos "Roupas"): as mangas compridas (as camisas, as jaquetas, os moletons, o jaleco e o
# cardigã) não furam: o antebraço novo fica por dentro delas. As luvas (a tática e a sem dedos), feitas em volta da mão de
# hoje (caixas), deixam a pele aparecer nos nós e entre a base dos dedos (a de hoje também aparece um pouco): precisam
# ser refeitas em volta da mão nova.
#
# As medidas são escritas no espaço do osso hand.L (x para o lado do polegar, y para os dedos, z para as costas da mão) e
# no de cada dedo (o mesmo jeito); o lado direito é o espelho (o x trocado no espaço de cada osso). A mão e os dedos são
# iguais em relação ao osso da mão nos dois rigs (só o lugar muda), então a mesma mão serve nos dois sem escala.
#
# As cores (os UVs vão em células da textura do personagem, 1024 px, a linha de baixo, 8 células de 128 px):
#   a pele: a célula 0 (a pele, #D08A63; a mesma da mão de hoje);
#   a borda das unhas: a célula 1 (a pele mais escura, #BB7C59; na máscara do corpo, R: a pele, então segue o tom);
#   o alto das unhas: a célula 7 (hoje igual à pele; nenhuma malha usa). Pintada mais clara (veja "proposta" abaixo),
#     as unhas ficam mais claras que a pele em qualquer tom;
#   os pelos: a célula 5 (o castanho escuro dos pelos, #2B1E16; na máscara do corpo, B: a cor dos pelos, _Cor3).
#   Na primeira pessoa (o material PrincipalBloco tingido pela pele pedida / #D08A63) os pelos ficam castanho-escuros
#   tingidos; no corpo das roupas (o shader ZombieCityCleaner/Roupas) ficam na cor do cabelo e da barba.
#   A "proposta" (só nas fotos e nos arquivos da pasta das fotos; as texturas do projeto não são mexidas): a célula 7 da
#   Personagem_PrincipalBloco.png com a pele clareada (#E1A78E), a da T_Corpo_Cor.png cinza 0,62 linear (206) e a da
#   T_Corpo_Mascara.png com R = 0,75 (191): pela conta do shader das roupas a unha fica 0,155 + 0,93 x a pele (mais clara
#   e um pouco mais branca, em qualquer tom).
#
# Os rigs dos braços de primeira pessoa: o braço, a mão e os dedos são iguais em relação ao pai em todos os
# Jogador_FP_*.fbx; na espingarda, no rifle, na sniper e na submetralhadora só o ombro (o upper_arm, filho do root) está
# em outro lugar (1 a 20 cm). Então a peça nova serve em todos pelos nomes dos ossos, com as poses de ligação do
# Maos_Novas_FP.fbx (ou com os vértices de cada lado deslocados pela diferença do upper_arm.X, uma translação).
#
# As fotos (hoje e o novo com a mesma câmera, na textura da proposta): pela câmera dos braços (55°) o Idle dos punhos,
# as mãos abertas e a direita no cabo; de perto, no repouso, o dorso, a palma, o lado e as unhas; as poses de perto (o
# punho, aberta e o cabo: a prévia da pegada, um cabo de 3,5 cm de raio atravessado na palma, cada falange girando até a
# pele encostar nele e o polegar por cima); o pulso e o cotovelo (o antebraço no repouso, o pulso dobrado 50 graus para
# cima e para baixo com a mão no cabo e o cotovelo dobrado do Idle); o corpo parado (o Parado) com as mãos de perto e o
# braço inteiro; as roupas do antebraço e da mão; e os tons (a conta do shader das roupas em três peles e pelos).
#
# Como rodar (Blender 5.2, em segundo plano; um Blender por vez; tudo leva ~1 min):
#   blender --background --factory-startup --python Blender/maos_novas.py -- tudo [pasta das fotos]
#   (as etapas, que também podem ir sozinhas e nesta ordem: fp, corpo, luvas, conferir, fotos; "medir" só mostra as
#   medidas dos FBX de hoje, sem gravar nada; "fotos:FP,Perto,Poses,Pulso,Corpo,Roupas,Tons,Mulher,Luvas" faz só
#   algumas fotos; a pasta das fotos também recebe o luvas_esconde.json e o roupas_esconde_corrigido.json (as listas que
#   abririam buracos no braço novo: hoje nenhuma), as texturas da proposta das unhas e as tiras das luvas pintadas)
# As folhas das fotos (fora do Blender, com o Pillow; Folha_FP, _Perto_Esquerda, _Perto_Direita, _Poses_Punho_Aberta,
# _Poses_Cabo, _Pulso, _Corpo_Maos, _Corpo_Braco, _Corpo_Inteiro, _Roupas, _Tons, _Mulher e _Luvas_<luva>):
#   py Blender/maos_novas.py folhas <pasta das fotos>

import json
import math
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
if AQUI not in sys.path:
    sys.path.insert(0, AQUI)
import agua_animacoes as ag  # noqa: E402

try:
    import bpy
    import bmesh
    from mathutils import Matrix, Quaternion, Vector
    from mathutils.bvhtree import BVHTree
except ImportError:          # fora do Blender: só as folhas das fotos
    bpy = None
    Matrix = Quaternion = Vector = ag.Vector

ARTE = ag.ARTE
FBX_ROUPAS = os.path.join(ARTE, "Characters", "Roupas", "Roupas.fbx")
T_CORPO_COR = os.path.join(ARTE, "Characters", "Roupas", "T_Corpo_Cor.png")
T_CORPO_MASCARA = os.path.join(ARTE, "Characters", "Roupas", "T_Corpo_Mascara.png")
SAIDA_FP = os.path.join(ag.JOGADOR, "Maos_Novas_FP.fbx")
SAIDA_CORPO = os.path.join(ag.PERSONAGENS, "Maos_Novas_Corpo.fbx")
SAIDA_LUVAS = os.path.join(ARTE, "Characters", "Roupas", "Luvas_Novas.fbx")
T_ROUPAS_MASCARA = os.path.join(ARTE, "Characters", "Roupas", "T_Roupas_Mascara.png")
RIG_FP = "FP_Rig_Maos"
RIG_CORPO = ag.RIG_CORPO

LADOS = "LR"
DEDOS = ag.DEDOS
OSSOS_DA_MAO = ["hand", "thumb", "thumb2"] + [f"{d}{i}" for d in DEDOS for i in (1, 2)]

# ================================================================================================ as cores (os UVs)
# o meio de cada célula da linha de baixo da textura do personagem (u, v)
CELULA = 0.125


def _celula(i):
    return ((i + 0.5) * CELULA, 0.5 * CELULA)


UV_DO_TIPO = {"pele": _celula(0), "borda": _celula(1), "unha": _celula(7), "pelo": _celula(5)}
UNHA_CLARA = (225, 167, 142)        # a proposta da célula 7 na Personagem_PrincipalBloco.png
UNHA_CINZA = 206                    # na T_Corpo_Cor.png (0,62 linear)
UNHA_MASCARA = 191                  # o R da T_Corpo_Mascara.png (0,75)

# ================================================================================================ as medidas da mão
# (m; no espaço do hand.L: x para o lado do polegar, y para os dedos, z para as costas)

# os anéis da palma: (y, x do lado do polegar, x do lado do mínimo, z das costas, z da palma, peso do antebraço); o
# primeiro é o pulso (6,8 x 5,0 cm), a ponta do antebraço; a palma é cheia (a pedido: "pode preencher um pouco com
# volume a mão"), para o pulso passar para ela sem afinar demais
PALMA = [(0.000, 0.0340, -0.0340, 0.0250, -0.0250, 0.50),     # o pulso
         (0.013, 0.0352, -0.0347, 0.0238, -0.0234, 0.12),     # a base da mão
         (0.033, 0.0385, -0.0377, 0.0215, -0.0205, 0.0),
         (0.056, 0.0412, -0.0402, 0.0193, -0.0183, 0.0),
         (0.076, 0.0420, -0.0410, 0.0178, -0.0170, 0.0)]
# o perfil das 9 colunas (do lado do polegar ao do mínimo): o quanto do z das costas e da palma cada uma tem
ARCO_DAS_COSTAS = [0.60, 0.90, 0.97, 1.00, 1.00, 0.99, 0.96, 0.88, 0.60]
ARCO_DA_PALMA = [0.62, 0.92, 1.00, 1.00, 1.00, 1.00, 1.00, 0.92, 0.62]
QUINA = 0.0025              # as colunas da beira entram este tanto (a quina arredondada)
# a frente da palma (os nós dos dedos): o y e o z de cada linha nas colunas do meio dos dedos, entre eles e na beira
FRENTE_COSTAS = {"meio": (0.096, 0.0160), "entre": (0.093, 0.0115), "beira": (0.090, 0.0088)}
FRENTE_PALMA = {"meio": (0.101, -0.0128), "entre": (0.104, -0.0108), "beira": (0.094, -0.0088)}
LADO_DA_FRENTE = (0.092, 0.0)        # o y e o z dos vértices dos lados na frente
ENTRE_DEDOS = (0.106, -0.0020)       # o fundo do "V" entre os dedos (y, z)

# os dedos: (largura, grossura) na base, na junta do meio e na ponta
MEDIDAS_DOS_DEDOS = {"index": ((0.0200, 0.0186), (0.0192, 0.0170), (0.0170, 0.0142)),
                     "middle": ((0.0206, 0.0190), (0.0198, 0.0174), (0.0175, 0.0146)),
                     "ring": ((0.0198, 0.0184), (0.0189, 0.0168), (0.0167, 0.0140)),
                     "pinky": ((0.0178, 0.0168), (0.0169, 0.0153), (0.0150, 0.0128))}
QUINAS_DO_DEDO = (0.78, 0.80)        # as quinas do octógono (da largura e da grossura): 0,707 seria redondo
GIRO_DO_POLEGAR = 35.0               # a unha do polegar: das costas para fora da mão (graus, em volta do osso)
FACETADA = False                     # True: a pele com as normais chapadas (as faces aparecendo, como o resto do corpo);
                                     # False: lisa (a aprovada)

# ================================================================================================ o antebraço
# (m; no espaço do forearm.L, o mesmo jeito: x para o lado do polegar, y para a mão, z para as costas)
# O começo é o fim do bloco do braço de cima de hoje (o anel de 8 vértices ~2,7 cm antes do cotovelo, preso ao
# upper_arm): o primeiro anel novo fica em cima dele (os 8 cantos exatos e os outros nas arestas entre eles), sem degrau.
# Daí a junta do cotovelo, um anel logo depois dela e os anéis que afinam até o pulso (o primeiro anel da palma). Cada
# anel é uma superelipse de 20 pontos nas mesmas colunas da palma (o expoente vai de 3,2, quase o bloco, a 2,4, redondo).
# (y, meia largura, meia altura, expoente, pesos)
COTOVELO = (0.000, 0.0495, 0.0500, 3.2, {"upper_arm": 0.5, "forearm": 0.5})
DEPOIS_DO_COTOVELO = (0.035, 0.0490, 0.0480, 2.9, {"forearm": 0.92, "upper_arm": 0.08})
# os anéis entre o depois do cotovelo e o pulso (a fração do caminho) e o perfil: o quanto já afinou é
# smoothstep(t) ** AFINAR_EXPOENTE (a metade de perto do cotovelo quase com o volume de hoje; sem cintura nem calombo)
AFINAR = (0.22, 0.45, 0.67, 0.86)
AFINAR_EXPOENTE = 2.2

# as unhas: de onde a onde na última falange (do comprimento dela), a largura (da largura da ponta), o quanto saltam e
# o quanto entram na pele
UNHA_Y = (0.52, 0.90)
UNHA_POLEGAR_Y = (0.50, 0.90)
UNHA_LARGURA = (0.70, 0.76)
UNHA_ALTURA = 0.0007
UNHA_FUNDO = 0.0012

# os pelos, só no dorso da mão e no antebraço (nos dedos eles espetavam a silhueta do punho fechado): (osso, x, y,
# ângulo (graus: 0 para os dedos, > 0 para o lado do mínimo), comprimento); os do antebraço no espaço do hand (y < 0:
# antes do pulso), deitados nas costas dele ("c") ou no lado do mínimo ("m")
PELO_LARGURA = (0.0007, 0.0003)     # na raiz e na ponta
PELO_ALTURA = (0.00015, 0.00035)     # o quanto a raiz e a ponta ficam acima da pele
PELOS = [("hand", 0.024, 0.030, 14, 0.0068), ("hand", 0.011, 0.022, 10, 0.0060), ("hand", -0.003, 0.034, 18, 0.0070),
         ("hand", -0.018, 0.025, 22, 0.0062), ("hand", 0.017, 0.049, 9, 0.0064), ("hand", 0.003, 0.056, 15, 0.0070),
         ("hand", -0.012, 0.048, 17, 0.0060), ("hand", -0.026, 0.043, 24, 0.0058), ("hand", 0.027, 0.065, 6, 0.0055),
         ("hand", -0.005, 0.071, 12, 0.0062), ("hand", -0.021, 0.064, 18, 0.0056)]
PELOS_DO_ANTEBRACO = [("c", 0.018, -0.022, 12, 0.0075), ("c", 0.004, -0.030, 16, 0.0080), ("c", -0.012, -0.020, 20, 0.0070),
                      ("c", 0.012, -0.045, 10, 0.0080), ("c", -0.006, -0.052, 18, 0.0078), ("c", -0.020, -0.040, 24, 0.0070),
                      ("c", 0.022, -0.062, 8, 0.0072), ("c", 0.002, -0.070, 14, 0.0080), ("c", -0.016, -0.074, 20, 0.0075),
                      ("c", 0.010, -0.095, 14, 0.0080), ("c", -0.012, -0.112, 18, 0.0080),
                      ("m", 0.010, -0.035, 25, 0.0065), ("m", -0.012, -0.058, 30, 0.0065), ("m", 0.004, -0.078, 22, 0.0060),
                      ("m", -0.004, -0.102, 24, 0.0065)]

# a prévia da pegada: um cabo de 3,5 cm de raio, atravessado na palma (no espaço do hand: o meio e a direção, que vai do
# lado do mínimo, mais perto do pulso, para o do indicador), e as falanges até encostar nele
CABO_RAIO = 0.035
CABO_MEIO = (0.0, 0.088, -0.0520)
CABO_GIRO = 14.0
CABO_FOLGA = 0.0006

# ================================================================================================ contas


def _r(v, n=4):
    return [round(float(x), n) for x in v]


def ossos_da_mao(lado):
    return [f"{n}.{lado}" for n in OSSOS_DA_MAO]


def _pesos(o):
    """{índice do vértice: {grupo: peso}}"""
    nomes = {g.index: g.name for g in o.vertex_groups}
    return {v.index: {nomes[g.group]: g.weight for g in v.groups if g.weight > 1e-4} for v in o.data.vertices}


def _na_mao(pesos, lado):
    nomes = set(ossos_da_mao(lado))
    return sum(w for g, w in pesos.items() if g in nomes) > 0.5


def _no_antebraco_ou_mao(pesos, lado):
    """O vértice tem mais da metade do peso no forearm do lado ou nos ossos da mão e dos dedos dele."""
    return pesos.get("forearm." + lado, 0.0) > 0.5 or _na_mao(pesos, lado)


def faces_do_braco(o, lado, material=0):
    """O corte: as faces do material da pele (o 0) com algum vértice com mais da metade do peso no forearm do lado ou
    nos ossos da mão e dos dedos dele: a mão, o antebraço (os dois blocos) e a emenda do cotovelo (as faces entre o fim
    do bloco do braço de cima, no upper_arm, e o começo do antebraço). Fica o braço de cima inteiro."""
    pesos = _pesos(o)
    return [f.index for f in o.data.polygons
            if f.material_index == material and any(_no_antebraco_ou_mao(pesos[v], lado) for v in f.vertices)]


def faces_do_braco_de_cima(o, lado, material=0):
    """O corte do braço de cima (para a peça nova do braco_de_cima.py): as faces do material da pele com algum vértice
    com mais da metade do peso no upper_arm do lado, fora as do corte do antebraço (faces_do_braco): 40 por lado."""
    pesos = _pesos(o)
    antes = set(faces_do_braco(o, lado, material))
    return [f.index for f in o.data.polygons
            if f.material_index == material and f.index not in antes
            and any(pesos[v].get("upper_arm." + lado, 0.0) > 0.5 for v in f.vertices)]


def faces_do_corte(o, pecas):
    """As faces que as peças novas trocam nos dois lados: o antebraço e a mão (sempre) e o braço de cima, se uma das
    peças for a BracoCima_Corpo (o braco_de_cima.py)."""
    cima = any(p.name.startswith("BracoCima") for p in pecas)
    tirar = set()
    for lado in LADOS:
        tirar |= set(faces_do_braco(o, lado))
        if cima:
            tirar |= set(faces_do_braco_de_cima(o, lado))
    return tirar


def faces_da_mao(o, lado, material=0):
    """O corte de antes (só a mão): as faces com todos os vértices com mais da metade do peso nos ossos da mão e dos
    dedos do lado, no material da pele."""
    pesos = _pesos(o)
    return [f.index for f in o.data.polygons
            if f.material_index == material and all(_na_mao(pesos[v], lado) for v in f.vertices)]


def _lerp(a, b, t):
    return a + (b - a) * t


def _newell(pts):
    n = Vector((0.0, 0.0, 0.0))
    for i, a in enumerate(pts):
        b = pts[(i + 1) % len(pts)]
        n.x += (a.y - b.y) * (a.z + b.z)
        n.y += (a.z - b.z) * (a.x + b.x)
        n.z += (a.x - b.x) * (a.y + b.y)
    return n


# ================================================================================================ a construção


def octogono(W, T, zc=0.0, cima=0.0, baixo=0.0, k=QUINAS_DO_DEDO):
    """Os 8 pontos (x, z) de um anel de dedo, na ordem da base: a quina da palma do lado do polegar, o meio da palma, a
    outra quina da palma, o lado de lá, a quina das costas de lá, o meio das costas, a quina das costas do lado do
    polegar e o lado do polegar."""
    a, t = W / 2, T / 2
    kx, kz = k
    return [(kx * a, zc - kz * t), (0.0, zc - t - baixo), (-kx * a, zc - kz * t), (-a, zc),
            (-kx * a, zc + kz * t), (0.0, zc + t + cima), (kx * a, zc + kz * t), (a, zc)]


def aneis_do_dedo(d, L1, L2):
    """Os anéis de um dedo (o osso, o y nele, a largura, a grossura, o z do meio, o calombo das costas, a almofada da
    palma e os pesos) e a ponta (o osso, o y e o z)."""
    (Wb, Tb), (Wm, Tm), (Wp, Tp) = MEDIDAS_DOS_DEDOS[d]
    b1, b2 = d + "1", d + "2"
    return ([(b1, 0.005, Wb, Tb, 0.0, 0.0008, 0.0, {b1: 0.7, "hand": 0.3}),
             (b1, L1 - 0.005, Wm, Tm, 0.0, 0.0, 0.0, {b1: 0.85, b2: 0.15}),
             (b2, 0.0, Wm * 1.03, Tm * 1.02, 0.0, 0.0012, 0.0, {b1: 0.5, b2: 0.5}),
             (b2, 0.006, Wm * 0.99, Tm * 0.98, 0.0, 0.0, 0.0, {b2: 0.85, b1: 0.15}),
             (b2, 0.42 * L2, _lerp(Wm, Wp, 0.45) * 0.96, _lerp(Tm, Tp, 0.45) * 0.95, 0.0, 0.0, 0.0, {b2: 1.0}),
             (b2, 0.62 * L2, _lerp(Wm, Wp, 0.7), _lerp(Tm, Tp, 0.7), 0.0, 0.0, 0.0008, {b2: 1.0}),
             (b2, 0.80 * L2, Wp * 0.95, Tp * 0.92, -0.0005, 0.0, 0.0004, {b2: 1.0}),
             (b2, 0.905 * L2, Wp * 0.78, Tp * 0.76, -0.0010, 0.0, 0.0, {b2: 1.0})],
            (b2, 0.955 * L2, -0.0013))


def aneis_do_polegar(L1, L2):
    return ([("thumb", 0.017, 0.0240, 0.0215, 0.0, 0.0, 0.0010, {"thumb": 0.7, "hand": 0.3}),
             ("thumb", L1 - 0.004, 0.0208, 0.0186, 0.0, 0.0, 0.0, {"thumb": 0.8, "thumb2": 0.2}),
             ("thumb2", 0.0, 0.0212, 0.0188, 0.0, 0.0012, 0.0, {"thumb": 0.5, "thumb2": 0.5}),
             ("thumb2", 0.006, 0.0204, 0.0180, 0.0, 0.0, 0.0, {"thumb2": 0.85, "thumb": 0.15}),
             ("thumb2", 0.019, 0.0190, 0.0166, 0.0, 0.0, 0.0, {"thumb2": 1.0}),
             ("thumb2", 0.027, 0.0186, 0.0160, 0.0, 0.0, 0.0008, {"thumb2": 1.0}),
             ("thumb2", 0.034, 0.0168, 0.0142, -0.0005, 0.0, 0.0004, {"thumb2": 1.0}),
             ("thumb2", 0.0385, 0.0145, 0.0124, -0.0010, 0.0, 0.0, {"thumb2": 1.0})],
            ("thumb2", 0.0415, -0.0013))


def _anel_no_octogono(ideal, cantos, centro):
    """Os 20 pontos do primeiro anel em cima do anel de hoje (os 8 cantos, no espaço do forearm): cada canto vai para o
    ponto ideal de ângulo mais perto (em volta do meio) e os outros pontos ficam nas arestas entre os cantos, divididas
    por igual. Devolve os pontos e, de cada um, (canto a, canto b, t) (para a forma "Mulher")."""
    def ang(x, z):
        return math.atan2(z - centro[1], x - centro[0])
    ai = [ang(x, z) for x, z in ideal]
    dono = {}
    for j in range(len(cantos)):
        a = ang(cantos[j].x, cantos[j].z)
        for i in sorted(range(len(ideal)), key=lambda i: abs(math.remainder(ai[i] - a, 2 * math.pi))):
            if i not in dono:
                dono[i] = j
                break
    marcados = sorted(dono)
    n = len(ideal)
    pts, mistura = [None] * n, [None] * n
    for k, i in enumerate(marcados):
        i2 = marcados[(k + 1) % len(marcados)]
        ja, jb = dono[i], dono[i2]
        livres = (i2 - i - 1) % n
        pts[i], mistura[i] = cantos[ja].copy(), (ja, ja, 0.0)
        for m in range(1, livres + 1):
            t = m / (livres + 1)
            pts[(i + m) % n] = cantos[ja].lerp(cantos[jb], t)
            mistura[(i + m) % n] = (ja, jb, t)
    return pts, mistura


class Mao:
    """A mão nova de um lado, no espaço da armadura de um rig (o repouso dele): os vértices (com os pesos, pelos ossos
    sem o lado) e as faces (com o tipo: pele, unha, borda ou pelo)."""

    def __init__(self, rest, lado, cotovelo=None):
        self.rest = rest            # {osso: matriz do repouso no espaço da armadura}
        self.lado = lado
        self.sx = 1.0 if lado == "L" else -1.0
        self.cotovelo = cotovelo    # os 8 cantos do fim do bloco do braço de cima de hoje (no espaço da armadura)
        self.co, self.pesos, self.faces = [], [], []
        self.e0 = []                # os vértices do primeiro anel: (índice, canto a, canto b, t)

    # ---------------------------------------------------------------------------------- os espaços
    def M(self, osso):
        return self.rest[osso + "." + self.lado]

    def p(self, osso, x, y, z):
        return self.M(osso) @ Vector((self.sx * x, y, z))

    def d(self, osso, x, y, z):
        return (self.M(osso).to_3x3() @ Vector((self.sx * x, y, z))).normalized()

    def local(self, osso, co):
        v = self.M(osso).inverted() @ co
        return Vector((self.sx * v.x, v.y, v.z))

    # ---------------------------------------------------------------------------------- os vértices e as faces
    def v(self, co, pesos):
        self.co.append(Vector(co))
        self.pesos.append(dict(pesos))
        return len(self.co) - 1

    def face(self, idx, tipo, dentro):
        """Uma face com a normal para fora (longe do ponto de dentro)."""
        pts = [self.co[i] for i in idx]
        c = sum(pts, Vector()) / len(pts)
        if _newell(pts).dot(c - dentro) < 0:
            idx = list(reversed(idx))
        self.faces.append((list(idx), tipo))

    def faixa(self, a, b, tipo, dentro):
        """Os quadrados entre dois anéis do mesmo tamanho, na mesma ordem (dentro: uma função do índice, o ponto de
        dentro de cada quadrado)."""
        n = len(a)
        for i in range(n):
            j = (i + 1) % n
            self.face([a[i], a[j], b[j], b[i]], tipo, dentro(i))

    def ligar(self, a, b):
        """O deslocamento e o sentido do anel a que o deixam mais perto do anel b (a soma das distâncias): devolve o a
        na ordem do b."""
        n = len(a)
        melhor = None
        for sentido in (a, list(reversed(a))):
            for k in range(n):
                s = sum((self.co[sentido[(i + k) % n]] - self.co[b[i]]).length for i in range(n))
                if melhor is None or s < melhor[0]:
                    melhor = (s, [sentido[(i + k) % n] for i in range(n)])
        return melhor[1]

    # ---------------------------------------------------------------------------------- a palma
    def colunas(self):
        """O x das 9 colunas na frente: a beira do polegar, o meio do indicador, entre ele e o médio, o médio, entre ele
        e o anelar, o anelar, entre ele e o mínimo, o mínimo e a beira do mínimo (dos ossos)."""
        centros = [self.local("hand", self.M(d + "1").translation).x for d in DEDOS]
        xs = [centros[0] + 0.0100]
        for i, c in enumerate(centros):
            xs.append(c)
            if i < 3:
                xs.append((c + centros[i + 1]) / 2)
        xs.append(centros[3] - 0.0094)
        return xs

    def anel_da_palma(self, y, xt, xp, zD, zP, xs):
        """Os 20 pontos de um anel da palma (no espaço do hand): a palma da coluna 0 à 8, o lado do mínimo, as costas da
        coluna 8 à 0 e o lado do polegar."""
        f = [(x - xs[8]) / (xs[0] - xs[8]) for x in xs]
        cx = [xp + fk * (xt - xp) for fk in f]
        cx[0] -= QUINA
        cx[8] += QUINA
        pal = [(cx[k], y, zP * ARCO_DA_PALMA[k]) for k in range(9)]
        cos = [(cx[k], y, zD * ARCO_DAS_COSTAS[k]) for k in range(9)]
        meio = (zD + zP) / 2 * 0.15
        return pal + [(xp - 0.0010, y, meio)] + list(reversed(cos)) + [(xt + 0.0010, y, meio)]

    def aneis_do_antebraco(self, xs):
        """Os anéis do antebraço, do fim do braço de cima ao anel antes do pulso: [(pontos no espaço da armadura, pesos)].
        O primeiro fica em cima do anel de hoje (os cantos exatos); os outros são superelipses nas colunas da palma."""
        L = self.comprimentos["forearm"]
        cantos = [self.local("forearm", c) for c in self.cotovelo]
        c0 = (sum(c.x for c in cantos) / len(cantos), sum(c.z for c in cantos) / len(cantos))
        _, xt, xp, zD, zP, _ = PALMA[0]
        aw, bw = (xt - xp) / 2, (zD - zP) / 2
        us = [(2 * (x - xs[8]) / (xs[0] - xs[8]) - 1) * 0.93 for x in xs]

        def eliptico(a, b, cx, cz, n):
            def meia(u):
                return max(0.0, 1.0 - abs(u) ** n) ** (1.0 / n)
            pal = [(cx + a * u, cz - b * meia(u)) for u in us]
            cos = [(cx + a * u, cz + b * meia(u)) for u in us]
            return pal + [(cx - a, cz)] + list(reversed(cos)) + [(cx + a, cz)]

        aneis = []
        # o fim do braço de cima (o anel de hoje)
        ideal = eliptico(0.049, 0.050, c0[0], c0[1], 3.2)
        pts, mistura = _anel_no_octogono(ideal, cantos, c0)
        aneis.append(([self.p("forearm", q.x, q.y, q.z) for q in pts], {"upper_arm": 1.0}))
        self._mistura_e0 = mistura
        # a junta do cotovelo e logo depois dela
        for (y, a, b, n, pesos), k in ((COTOVELO, 0.8), (DEPOIS_DO_COTOVELO, 0.6)):
            pts = eliptico(a, b, c0[0] * k, c0[1] * k, n)
            aneis.append(([self.p("forearm", x, y, z) for x, z in pts], pesos))
        # afinando até o pulso
        y2, a2, b2 = DEPOIS_DO_COTOVELO[0], DEPOIS_DO_COTOVELO[1], DEPOIS_DO_COTOVELO[2]
        for t in AFINAR:
            g = (t * t * (3 - 2 * t)) ** AFINAR_EXPOENTE
            y = y2 + t * (L - y2)
            pts = eliptico(aw + (a2 - aw) * (1 - g), bw + (b2 - bw) * (1 - g), c0[0] * 0.6 * (1 - g),
                           c0[1] * 0.6 * (1 - g), _lerp(2.9, 2.4, t))
            aneis.append(([self.p("forearm", x, y, z) for x, z in pts], {"forearm": 1.0}))
        return aneis

    def construir(self):
        xs = self.colunas()
        cadeia = []           # os anéis de 20, do fim do braço de cima à frente da palma
        if self.cotovelo:
            for j, (pts, pesos) in enumerate(self.aneis_do_antebraco(xs)):
                anel = [self.v(c, pesos) for c in pts]
                if j == 0:
                    self.e0 = [(i, *m) for i, m in zip(anel, self._mistura_e0)]
                cadeia.append(anel)
        n_ante = len(cadeia)
        palma = []
        for j, (y, xt, xp, zD, zP, wa) in enumerate(PALMA):
            pts = self.anel_da_palma(y, xt, xp, zD, zP, xs)
            pesos = {"forearm": wa, "hand": 1.0 - wa} if wa > 0 else {"hand": 1.0}
            anel = []
            for k, (x, yy, z) in enumerate(pts):
                # a almofada do lado do mínimo e a palma funda no meio
                if 1 <= j <= 3 and k in (6, 7):
                    z -= 0.0022
                if 2 <= j <= 3 and k in (3, 4, 5):
                    z += 0.0015
                anel.append(self.v(self.p("hand", x, yy, z), pesos))
            palma.append(anel)
        # a frente (os nós dos dedos)
        frente = []
        tipo_col = ["beira", "meio", "entre", "meio", "entre", "meio", "entre", "meio", "beira"]
        dedo_col = ["index", "index", None, "middle", None, "ring", None, "pinky", "pinky"]
        vizinhos = {2: ("index", "middle"), 4: ("middle", "ring"), 6: ("ring", "pinky")}

        def pesos_frente(k, palma):
            fd = 0.5 if palma else 0.35
            if dedo_col[k]:
                return {"hand": 1.0 - fd, dedo_col[k] + "1": fd}
            a, b = vizinhos[k]
            return {"hand": 1.0 - fd, a + "1": fd / 2, b + "1": fd / 2}
        xsf = list(xs)
        xsf[0] -= QUINA * 0.6
        xsf[8] += QUINA * 0.6
        for k in range(9):
            y, z = FRENTE_PALMA[tipo_col[k]]
            frente.append(self.v(self.p("hand", xsf[k], y, z), pesos_frente(k, True)))
        ly, lz = LADO_DA_FRENTE
        frente.append(self.v(self.p("hand", xs[8] - 0.0010, ly, lz), {"hand": 0.65, "pinky1": 0.35}))
        for k in reversed(range(9)):
            y, z = FRENTE_COSTAS[tipo_col[k]]
            frente.append(self.v(self.p("hand", xsf[k], y, z), pesos_frente(k, False)))
        frente.append(self.v(self.p("hand", xs[0] + 0.0010, ly, lz), {"hand": 0.65, "index1": 0.35}))
        cadeia += palma + [frente]
        # as faces do antebraço e da palma (sem o encaixe do polegar: os anéis 1 a 3 da palma, do meio dela ao lado do
        # polegar); o ponto de dentro de cada faixa é o meio dos dois anéis
        encaixe = {(n_ante + 1, 19), (n_ante + 1, 0), (n_ante + 2, 19), (n_ante + 2, 0)}
        for j in range(len(cadeia) - 1):
            meio = sum((self.co[i] for i in cadeia[j] + cadeia[j + 1]), Vector()) / 40
            for i in range(20):
                if (j, i) in encaixe:
                    continue
                k = (i + 1) % 20
                self.face([cadeia[j][i], cadeia[j][k], cadeia[j + 1][k], cadeia[j + 1][i]], "pele", meio)
        # o fundo dos "V" entre os dedos
        webs = {}
        for c in (2, 4, 6):
            a, b = vizinhos[c]
            webs[c] = self.v(self.p("hand", xs[c], ENTRE_DEDOS[0], ENTRE_DEDOS[1]),
                             {"hand": 0.5, a + "1": 0.25, b + "1": 0.25})
        P = frente[0:9]
        S_min = frente[9]
        D = list(reversed(frente[10:19]))      # D[k]: as costas da coluna k
        S_pol = frente[19]
        bases = {"index": [P[0], P[1], P[2], webs[2], D[2], D[1], D[0], S_pol],
                 "middle": [P[2], P[3], P[4], webs[4], D[4], D[3], D[2], webs[2]],
                 "ring": [P[4], P[5], P[6], webs[6], D[6], D[5], D[4], webs[4]],
                 "pinky": [P[6], P[7], P[8], S_min, D[8], D[7], D[6], webs[6]]}
        self.aneis_dedo = {}
        for d in DEDOS:
            L1 = self.comprimentos[d + "1"]
            L2 = self.comprimentos[d + "2"]
            lista, ponta = aneis_do_dedo(d, L1, L2)
            self._dedo(d, bases[d], lista, ponta, girar=0.0)
        # o polegar: o encaixe nos anéis 1, 2 e 3 da palma (as colunas 1 e 0 da palma e o lado do polegar)
        enc = [palma[1][1], palma[1][0], palma[1][19], palma[2][19], palma[3][19], palma[3][0], palma[3][1], palma[2][1]]
        lista, ponta = aneis_do_polegar(self.comprimentos["thumb"], self.comprimentos["thumb2"])
        # o anel do encaixe: o octógono no começo do polegar, misturado com o lugar dos vértices da palma
        oct0 = [self.v(self._ponto_polegar("thumb", 0.004, x, z), {}) for x, z in octogono(0.026, 0.022)]
        enc = self.ligar(enc, oct0)
        for a, b in zip(enc, oct0):
            self.co[a] = self.co[a].lerp(self.co[b], 0.5)
            self.pesos[a] = {"hand": 0.7, "thumb": 0.3}
        # (os pontos do octógono saem: só serviram de guia)
        self._descartar = set(oct0) | {palma[2][0]}
        self._dedo("thumb", enc, lista, ponta, girar=GIRO_DO_POLEGAR, ligar=True)
        self.aneis_palma = palma
        return self

    def _ponto_polegar(self, osso, y, x, z):
        """Um ponto de um anel do polegar: o anel girado em volta do osso (a unha para fora da mão)."""
        g = math.radians(GIRO_DO_POLEGAR)
        return self.p(osso, x * math.cos(g) + z * math.sin(g), y, -x * math.sin(g) + z * math.cos(g))

    def _dedo(self, d, base, lista, ponta, girar=0.0, ligar=False):
        anteriores = base
        aneis = []
        for (osso, y, W, T, zc, cima, baixo, pesos) in lista:
            pts = octogono(W, T, zc, cima, baixo)
            if girar:
                anel = [self.v(self._ponto_polegar(osso, y, x, z), pesos) for x, z in pts]
            else:
                anel = [self.v(self.p(osso, x, y, z), pesos) for x, z in pts]
            if ligar and anteriores is base:
                anteriores = self.ligar(base, anel)
                self.aneis_polegar_base = anteriores
            eixo = self.p(osso, 0.0, y, zc)
            self.faixa(anteriores, anel, "pele", lambda i, e=eixo: e)
            aneis.append((osso, y, W, T, zc, anel))
            anteriores = anel
        osso, y, zc = ponta
        if girar:
            pv = self.v(self._ponto_polegar(osso, y, 0.0, zc), {osso: 1.0})
        else:
            pv = self.v(self.p(osso, 0.0, y, zc), {osso: 1.0})
        dentro = self.p(osso, 0.0, y - 0.006, zc)
        for i in range(8):
            self.face([anteriores[i], anteriores[(i + 1) % 8], pv], "pele", dentro)
        self.aneis_dedo[d] = aneis

    # ---------------------------------------------------------------------------------- as unhas e os pelos
    def bvh_da_pele(self):
        faces = [f for f, t in self.faces if t == "pele"]
        return BVHTree.FromPolygons([tuple(c) for c in self.co], faces)

    def _na_pele(self, bvh, origem, direcao):
        hit = bvh.ray_cast(origem, direcao, 0.08)
        if hit[0] is None:
            return None, None
        n = hit[1]
        if n.dot(direcao) > 0:
            n = -n
        return hit[0], n

    def unhas(self):
        """As unhas: uma placa plana em 3 fileiras (atrás, no meio e na frente, para seguir a curva da ponta), um pouco
        acima da pele (UNHA_ALTURA), com a borda descendo para dentro dela (UNHA_FUNDO)."""
        bvh = self.bvh_da_pele()
        for d in DEDOS + ("thumb",):
            if d == "thumb":
                osso, L, (y0, y1) = "thumb2", self.comprimentos["thumb2"], UNHA_POLEGAR_Y
                W = 0.0175
            else:
                osso, L, (y0, y1) = d + "2", self.comprimentos[d + "2"], UNHA_Y
                W = MEDIDAS_DOS_DEDOS[d][2][0] * 1.04
            if d == "thumb":
                o0 = self._ponto_polegar(osso, 0.0, 0.0, 0.03)
                dirc = (self._ponto_polegar(osso, 0.0, 0.0, 0.0) - o0).normalized()
            else:
                dirc = -self.d(osso, 0, 0, 1)
            fora = -dirc
            ys = (y0 * L, (y0 + y1) / 2 * L, y1 * L)
            grade = []
            for r, y in enumerate(ys):
                w = _lerp(UNHA_LARGURA[0], UNHA_LARGURA[1], r / 2) * W / 2
                fila = []
                for x in (w, 0.0, -w):
                    # o contorno arredondado: as quinas de trás e da frente entram, o meio da frente avança
                    if x != 0.0 and r != 1:
                        x *= 0.84 if r == 0 else 0.90
                        yy = y + (0.0008 if r == 0 else -0.0004)
                    else:
                        yy = y + (-0.0006 if r == 0 else 0.0010 if r == 2 else 0.0)
                    hit = None
                    for recua in range(16):       # além da ponta: um pouco para trás
                        y2 = yy - 0.0004 * recua
                        o = self._ponto_polegar(osso, y2, x, 0.03) if d == "thumb" else self.p(osso, x, y2, 0.03)
                        hit, _ = self._na_pele(bvh, o, dirc)
                        if hit is not None:
                            break
                    fila.append(hit)
                grade.append(fila)
            cima = [[self.v(h + fora * UNHA_ALTURA, {osso: 1.0}) for h in fila] for fila in grade]
            meio = self.co[cima[1][1]]
            dentro = meio - fora * 0.01
            for r in range(2):
                for c in range(2):
                    self.face([cima[r][c], cima[r][c + 1], cima[r + 1][c + 1], cima[r + 1][c]], "unha", dentro)
            borda = [cima[0][0], cima[0][1], cima[0][2], cima[1][2], cima[2][2], cima[2][1], cima[2][0], cima[1][0]]
            baixo = [self.v(self.co[i] - fora * (UNHA_ALTURA + UNHA_FUNDO), {osso: 1.0}) for i in borda]
            for i in range(8):
                j = (i + 1) % 8
                self.face([borda[i], borda[j], baixo[j], baixo[i]], "borda", meio - fora * 0.0008)

    def _pelo(self, bvh, origem, direcao, ao_longo, comprimento, pesos):
        raiz, n = self._na_pele(bvh, origem, direcao)
        if raiz is None:
            return
        t = (ao_longo - ao_longo.dot(n) * n).normalized()
        lado = n.cross(t)
        a = raiz + lado * (PELO_LARGURA[0] / 2) + n * PELO_ALTURA[0]
        b = raiz - lado * (PELO_LARGURA[0] / 2) + n * PELO_ALTURA[0]
        pt = raiz + t * comprimento
        ponta, n2 = self._na_pele(bvh, pt + n * 0.01, -n)
        if ponta is None:
            ponta, n2 = pt, n
        c = ponta - lado * (PELO_LARGURA[1] / 2) + n2 * PELO_ALTURA[1]
        e = ponta + lado * (PELO_LARGURA[1] / 2) + n2 * PELO_ALTURA[1]
        if pesos is None:
            pesos = self._pesos_perto(raiz)
        idx = [self.v(q, pesos) for q in (a, b, c, e)]
        self.face(idx, "pelo", raiz - n * 0.01)

    def _pesos_perto(self, co):
        melhor = min(((c - co).length, i) for i, c in enumerate(self.co[:self.n_pele]) if self.pesos[i])
        return dict(self.pesos[melhor[1]])

    def pelos(self):
        self.n_pele = len(self.co)
        bvh = self.bvh_da_pele()
        for (osso, x, y, ang, comp) in PELOS:
            a = math.radians(ang)
            if osso == "thumb":
                o = self._ponto_polegar(osso, y, x, 0.03)
                dirc = (self._ponto_polegar(osso, y, x, 0.0) - o).normalized()
                ao_longo = self.d(osso, 0, 1, 0)
            else:
                o = self.p(osso, x, y, 0.04)
                dirc = -self.d(osso, 0, 0, 1)
                ao_longo = self.d(osso, -math.sin(a), math.cos(a), 0)
            self._pelo(bvh, o, dirc, ao_longo, comp, None)
        if not self.cotovelo:
            return
        for (face, x, y, ang, comp) in PELOS_DO_ANTEBRACO:
            a = math.radians(ang)
            ao_longo = self.d("hand", -math.sin(a), math.cos(a), 0)
            if face == "c":
                o = self.p("hand", x, y, 0.08)
                dirc = -self.d("hand", 0, 0, 1)
            else:        # o lado do mínimo (x < 0): x aqui é o z
                o = self.p("hand", -0.08, y, x)
                dirc = self.d("hand", 1, 0, 0)
                ao_longo = self.d("hand", 0, math.cos(a), -math.sin(a) * 0.3)
            self._pelo(bvh, o, dirc, ao_longo, comp, {"forearm": 1.0})


# ================================================================================================ o objeto


def _uv(m, idx, tipo, origem):
    """Os UVs de uma face: o meio da célula do tipo e a face projetada pequena em volta (0,25 de UV por metro, para não
    ficar com o UV degenerado)."""
    pts = [m.co[i] for i in idx]
    n = _newell(pts)
    eixo = max(range(3), key=lambda k: abs(n[k]))
    a, b = [k for k in range(3) if k != eixo]
    cu, cv = UV_DO_TIPO[tipo]
    lim = CELULA / 2 - 0.004
    return [(cu + max(-lim, min(lim, 0.25 * (p[a] - origem[a]))), cv + max(-lim, min(lim, 0.25 * (p[b] - origem[b]))))
            for p in pts]


def objeto_da_mao(m, nome, rig, material, faces_velhas=None):
    """O objeto da malha (preso ao rig, com o modificador de armadura): os grupos dos ossos, o UVMap, as faces lisas na
    pele e chapadas nas unhas e nos pelos. Com faces_velhas ([(centro, número, osso)], do NPC_Corpo), o UV "Face"."""
    usados = set()
    for i, (idx, t) in enumerate(m.faces):
        usados.update(idx)
    usados -= getattr(m, "_descartar", set())
    novo = {}
    co = []
    for i in sorted(usados):
        novo[i] = len(co)
        co.append(m.co[i])
    m.novo = novo
    faces = [([novo[i] for i in idx], t) for idx, t in m.faces]
    m.faces_dos_pelos = [f for f, t in faces if t == "pelo"]
    me = bpy.data.meshes.new(nome)
    me.from_pydata([tuple(c) for c in co], [], [f for f, _ in faces])
    me.update()
    me.materials.append(material)
    ob = bpy.data.objects.new(nome, me)
    bpy.context.scene.collection.objects.link(ob)
    # os grupos
    grupos = {}
    for i_velho, i in novo.items():
        for osso, w in m.pesos[i_velho].items():
            if w <= 0:
                continue
            g = grupos.get(osso)
            if g is None:
                g = grupos[osso] = ob.vertex_groups.new(name=osso + "." + m.lado)
            g.add([i], w, "REPLACE")
    # os UVs
    uv = me.uv_layers.new(name="UVMap")
    origem = m.M("hand").translation
    for f, (idx, t) in zip(me.polygons, m.faces):
        for li, (u, v) in zip(f.loop_indices, _uv(m, idx, t, origem)):
            uv.data[li].uv = (u, v)
        f.use_smooth = (t == "pele") and not FACETADA
    if faces_velhas is not None:
        uvf = me.uv_layers.new(name="Face")
        pesos_v = {novo[i]: m.pesos[i] for i in novo}
        for f, (idx, t) in zip(me.polygons, faces):
            c = sum((me.vertices[k].co for k in f.vertices), Vector()) / len(f.vertices)
            soma = {}
            for k in f.vertices:
                for osso, w in pesos_v[k].items():
                    soma[osso] = soma.get(osso, 0.0) + w
            dono = max(soma, key=soma.get) + "." + m.lado
            cand = [fv for fv in faces_velhas if fv[2] == dono] or faces_velhas
            num = min(cand, key=lambda fv: (fv[0] - c).length)[1]
            for li in f.loop_indices:
                uvf.data[li].uv = (num + 0.5, -1.0)
    # as quinas vivas entre a pele e as unhas e os pelos
    sharp = me.attributes.get("sharp_edge") or me.attributes.new("sharp_edge", "BOOLEAN", "EDGE")
    duro = set()
    for f, (idx, t) in zip(me.polygons, faces):
        if t != "pele":
            duro.update(f.edge_keys)
    for e in me.edges:
        sharp.data[e.index].value = tuple(sorted(e.vertices)) in duro
    me.update()
    ob.parent = rig
    ob.matrix_parent_inverse = Matrix.Identity(4)
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    return ob


# ================================================================================================ as etapas


def _rest(rig):
    return {b.name: b.matrix_local.copy() for b in rig.data.bones}


def anel_do_cotovelo(o, rig, lado):
    """O fim do bloco do braço de cima de hoje: os 8 vértices do upper_arm do lado mais perto do cotovelo (o maior y no
    espaço do forearm), no espaço da armadura."""
    pesos = _pesos(o)
    inv = rig.data.bones["forearm." + lado].matrix_local.inverted()
    vs = [(o.matrix_world @ o.data.vertices[i].co) for i, p in pesos.items() if p.get("upper_arm." + lado, 0) > 0.5]
    vs.sort(key=lambda c: -(inv @ c).y)
    return vs[:8]


def maos_do_rig(rig, malha_velha, material, nome, faces_velhas=None, formas=None):
    rest = _rest(rig)
    obs = []
    for lado in LADOS:
        m = Mao(rest, lado, anel_do_cotovelo(malha_velha, rig, lado))
        m.comprimentos = {}
        for n in OSSOS_DA_MAO + ["forearm", "upper_arm"]:
            m.comprimentos[n] = rig.data.bones[n + "." + lado].length
        m.construir()
        m.unhas()
        m.pelos()
        ob = objeto_da_mao(m, f"{nome}.{lado}", rig, material,
                           faces_velhas[lado] if faces_velhas else None)
        if formas:
            _forma_mulher(ob, formas[lado], m)
        _sem_pelos(ob, m)
        obs.append(ob)
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        print(f"maos: {ob.name}: {len(ob.data.vertices)} vértices, {tris} triângulos")
    return obs


def _limpar_rig(rig):
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    ag._limpar_animacao(rig)
    for pb in rig.pose.bones:
        pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)


def _exportar(caminho, rig, malhas):
    for o in bpy.context.scene.objects:
        o.select_set(o in [rig] + malhas)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=False, add_leaf_bones=False, use_mesh_modifiers=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
                             bake_anim=False, mesh_smooth_type="FACE", use_tspace=False,
                             path_mode="AUTO", embed_textures=False)
    print("maos: gravado", caminho)


def etapa_fp():
    ag.nova_cena()
    novos = ag.importar(ag.FBX_MAOS)
    rig = bpy.data.objects[RIG_FP]
    velha = bpy.data.objects["FP_Bracos_Maos"]
    material = velha.data.materials[0]
    _limpar_rig(rig)
    obs = maos_do_rig(rig, velha, material, "Mao_FP")
    for o in novos:
        if o not in (rig,):
            bpy.data.objects.remove(o, do_unlink=True)
    _exportar(SAIDA_FP, rig, obs)


def _ler_roupas():
    """Do NPC_Corpo (Roupas.fbx): as faces do braço de cada lado (o centro, o número da face e o osso dono) e, para a
    forma "Mulher", os vértices de cada osso da mão e do fim do antebraço (no espaço do osso: a base e a forma)."""
    ag.nova_cena()
    novos = ag.importar(FBX_ROUPAS)
    rig = [o for o in novos if o.type == "ARMATURE"][0]
    npc = bpy.data.objects["NPC_Corpo"]
    me = npc.data
    pesos = _pesos(npc)
    uvf = me.uv_layers["Face"].data
    kb = me.shape_keys.key_blocks
    base, mulher = kb["Basis"], kb["Mulher"]
    faces, formas = {}, {}
    for lado in LADOS:
        nomes = set(ossos_da_mao(lado)) | {"forearm." + lado, "upper_arm." + lado}
        lst = []
        for f in me.polygons:
            soma = {}
            for v in f.vertices:
                for g, w in pesos[v].items():
                    soma[g] = soma.get(g, 0.0) + w
            dono = max(soma, key=soma.get)
            if dono not in nomes:
                continue
            c = sum((base.data[v].co for v in f.vertices), Vector()) / len(f.vertices)
            lst.append((npc.matrix_world @ c, int(math.floor(uvf[f.loop_indices[0]].uv.x + 1e-3)), dono))
        faces[lado] = lst
        fl = {}
        ante = rig.data.bones["forearm." + lado].matrix_local.inverted()
        for osso in nomes:
            B = rig.data.bones[osso].matrix_local
            ps, qs = [], []
            for i, p in pesos.items():
                if p.get(osso, 0) <= 0.5:
                    continue
                # do braço de cima, só os dois anéis de perto do cotovelo
                if osso.startswith("upper_arm") and (ante @ base.data[i].co).y < -0.12:
                    continue
                ps.append(B.inverted() @ base.data[i].co)
                qs.append(B.inverted() @ mulher.data[i].co)
            fl[osso] = (ps, qs)
        # os vértices do fim do bloco do braço de cima (a base e a forma), para o primeiro anel novo
        fim = [(npc.matrix_world @ base.data[i].co, npc.matrix_world @ mulher.data[i].co) for i, p in pesos.items()
               if p.get("upper_arm." + lado, 0) > 0.5]
        fim.sort(key=lambda bm: -(ante @ bm[0]).y)
        fl["_cotovelo"] = fim[:8]
        formas[lado] = fl
    return faces, formas


def _ajuste_afim(ps, qs):
    """A matriz 4x4 (afim) que leva ps a qs pelos mínimos quadrados."""
    import numpy as np
    X = np.array([[p.x, p.y, p.z, 1.0] for p in ps])
    Q = np.array([[q.x, q.y, q.z] for q in qs])
    A, *_ = np.linalg.lstsq(X, Q, rcond=None)
    M = Matrix.Identity(4)
    for i in range(3):
        for j in range(4):
            M[i][j] = A[j][i]
    return M


def _forma_mulher(ob, dados, m=None):
    """A forma "Mulher" da peça nova: em cada osso, o ajuste afim do braço de hoje (da base para a forma, no espaço do
    osso), misturado pelos pesos; no primeiro anel (em cima do fim do braço de cima de hoje), a forma dos cantos de
    hoje, misturada como os pontos (para a emenda continuar fechada na mulher)."""
    rig = ob.parent
    afins = {}
    for osso, par in dados.items():
        if osso.startswith("_"):
            continue
        ps, qs = par
        if len(ps) >= 8:
            B = rig.data.bones[osso].matrix_local
            afins[osso] = B @ _ajuste_afim(ps, qs) @ B.inverted()
    ob.shape_key_add(name="Basis", from_mix=False)
    kb = ob.shape_key_add(name="Mulher", from_mix=False)
    nomes = {g.index: g.name for g in ob.vertex_groups}
    for v in ob.data.vertices:
        soma, tot = Vector((0.0, 0.0, 0.0)), 0.0
        for g in v.groups:
            A = afins.get(nomes[g.group])
            if A is None or g.weight <= 0:
                continue
            soma += (A @ v.co) * g.weight
            tot += g.weight
        kb.data[v.index].co = soma / tot if tot > 0 else v.co.copy()
    if m is not None and m.e0 and dados.get("_cotovelo"):
        # o deslocamento de cada canto de hoje (pelo canto do NPC_Corpo no mesmo lugar)
        desl = []
        for c in m.cotovelo:
            b, f = min(dados["_cotovelo"], key=lambda bf: (bf[0] - c).length)
            desl.append(f - b)
        for i, ja, jb, t in m.e0:
            if i in m.novo:
                k = m.novo[i]
                kb.data[k].co = ob.data.vertices[k].co + desl[ja].lerp(desl[jb], t)


RECOLHE_PELO = 0.0008   # o quanto os pelos recolhidos entram na pele (m)


def _recolher(pts, face, base):
    """Os vértices de um pelo juntos num ponto só, abaixo da pele (o triângulo some: área zero e por dentro)."""
    cs = [base[i] for i in face]
    c = sum(cs, Vector()) / len(cs)
    n = _newell(cs)
    n = n.normalized() if n.length > 1e-12 else Vector((0, 0, 1))
    for i in face:
        pts[i] = c - n * RECOLHE_PELO


def _sem_pelos(ob, m):
    """A mulher e quem não tem pelos (08/11/2026, a pedido: "uma versão para a mulher, onde é a mesma coisa, porém sem
    os pelos no braço"): na forma "Mulher" (se houver) os pelos se recolhem para dentro da pele, e a forma "SemPelos" faz
    só isso (para usar em quem quiser, no homem também). Assim a Vestimenta (a "Mulher" a 100) e as cópias dos braços de
    primeira pessoa (o RoupasNosBracos copia as formas) já ficam sem pelos."""
    me = ob.data
    if not me.shape_keys:
        ob.shape_key_add(name="Basis", from_mix=False)
    kb = me.shape_keys.key_blocks
    base = [v.co.copy() for v in me.vertices]
    sem = ob.shape_key_add(name="SemPelos", from_mix=False)
    pts = [c.copy() for c in base]
    for f in m.faces_dos_pelos:
        _recolher(pts, f, base)
    for i, c in enumerate(pts):
        sem.data[i].co = c
    if "Mulher" in kb:
        mu = kb["Mulher"]
        cur = [d.co.copy() for d in mu.data]
        for f in m.faces_dos_pelos:
            _recolher(cur, f, cur)
        for i, c in enumerate(cur):
            mu.data[i].co = c


# ================================================================================================ as luvas
# As duas luvas refeitas em volta da mão nova (08/11/2026, a fase 2): a casca da pele (as faces da pele da peça nova do
# corpo, empurradas para fora pela normal) com uma borda em cada abertura (do lado de fora até um pouco dentro da pele),
# no formato das peças do Roupas.fbx: o objeto Roupa_<nome> (as duas mãos numa malha), o PrincipalBloco_Rig, o material
# com o nome da peça, o UVMap na tira da peça (a camada do catálogo) e o UV "Face" (x: o número da face, y: a camada), a
# forma "Mulher" e as faces lisas. As cores pelas partes da máscara da tira (R, G e B: as 3 cores da peça), como as de
# hoje: R nas costas e nos lados, B na palma (o couro da pegada), G nos protetores dos nós (só a tática) e na tira do punho.
# Os UVs de cada parte vão para dentro de uma ilha grande da cor dela na tira (o miolo achado por erosão da máscara).
LUVAS = {
    # nome: (a camada, a grossura, onde o punho termina (o y no espaço do hand, < 0), dedos inteiros?, a faixa do punho)
    "Luva_Tatica": (58, 0.0024, -0.075, True, (-0.060, -0.040)),
    "Luva_Sem_Dedos": (59, 0.0020, -0.040, False, (-0.040, -0.026)),
}
PROTETOR = (0.082, 0.114, 0.0016)   # os protetores dos nós da tática: de onde a onde (o y no hand) e o quanto saltam
BORDA_DENTRO = 0.0004               # a borda das aberturas desce até este tanto dentro da pele


def _ilhas(camada):
    """O miolo de uma ilha de cada parte (R, G, B) na tira da peça: {parte: (u, v, meia largura em UV)}."""
    import numpy as np
    img = bpy.data.images.load(T_ROUPAS_MASCARA, check_existing=True)
    W, H = img.size
    px = np.empty(W * H * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(H, W, 4)
    t = W // 8
    c, r = camada % 8, camada // 8
    y0 = H - (r + 1) * t
    tile = px[y0:y0 + t, c * t:(c + 1) * t, :3]
    saida = {}
    for k, parte in enumerate("RGB"):
        outros = [j for j in range(3) if j != k]
        B = (tile[:, :, k] > 0.5) & (tile[:, :, outros[0]] < 0.5) & (tile[:, :, outros[1]] < 0.5)
        ultimo, n = B, 0
        while B.any():
            ultimo, n = B, n + 1
            B = B & np.roll(B, 1, 0) & np.roll(B, -1, 0) & np.roll(B, 1, 1) & np.roll(B, -1, 1)
        ys, xs = np.nonzero(ultimo)
        if len(xs) == 0:
            continue
        i = len(xs) // 2
        saida[parte] = ((xs[i] + 0.5) / t, (ys[i] + 0.5) / t, max(1.0, n * 0.7) / t)
    return saida


def _incluido(rig, lado, co, pesos, spec):
    """Se o vértice da pele fica dentro da luva: pelo osso (o antebraço até o punho; a mão; os dedos inteiros na tática ou
    só a primeira falange; o polegar inteiro ou só o osso de baixo), com as contas no espaço de cada osso."""
    _, _, punho, inteiros, _ = spec
    b = lambda n: rig.data.bones[n + "." + lado].matrix_local
    if not pesos:
        return False
    dono = max(pesos, key=pesos.get).split(".")[0]
    if dono in ("upper_arm",):
        return False
    if dono == "forearm":
        return (b("hand").inverted() @ co).y >= punho
    if inteiros or dono == "hand":
        return True
    for d in DEDOS:
        if dono.startswith(d):
            return (b(d + "1").inverted() @ co).y <= rig.data.bones[d + "1." + lado].length - 0.003
    if dono.startswith("thumb"):
        return (b("thumb").inverted() @ co).y <= rig.data.bones["thumb." + lado].length - 0.002
    return False


def _tipo_da_face_pela_uv(u, v):
    """O tipo de uma face da peça nova pela célula do UV dela (a pele, a borda e o alto das unhas e os pelos)."""
    if v >= CELULA:
        return "pele"
    k = int(u // CELULA)
    return {0: "pele", 1: "borda", 7: "unha", 5: "pelo"}.get(k, "pele")


def luva(nome, spec, rig, bracos):
    """A malha de uma luva (as duas mãos) e as faces de cada braço que ela cobre: (objeto, {número da face: coberta?})."""
    camada, grossura, punho, inteiros, faixa = spec
    ilhas = _ilhas(camada)
    co, mulher, pesos, faces, partes = [], [], [], [], []
    cobertas = {}
    for braco in bracos:
        lado = braco.name.rsplit(".", 1)[1]
        me = braco.data
        kb = me.shape_keys.key_blocks if me.shape_keys else None
        nomes = {g.index: g.name for g in braco.vertex_groups}
        pv = [{nomes[g.group]: g.weight for g in v.groups if g.weight > 1e-4} for v in me.vertices]
        uv = me.uv_layers["UVMap"].data
        uvf = me.uv_layers["Face"].data
        dentro = [_incluido(rig, lado, v.co, pv[v.index], spec) for v in me.vertices]
        mao = rig.data.bones["hand." + lado].matrix_local.inverted()
        regiao = []
        for f in me.polygons:
            u = sum(uv[l].uv.x for l in f.loop_indices) / len(f.loop_indices)
            v = sum(uv[l].uv.y for l in f.loop_indices) / len(f.loop_indices)
            tipo = _tipo_da_face_pela_uv(u, v)
            todos = all(dentro[i] for i in f.vertices)
            num = int(math.floor(uvf[f.loop_indices[0]].uv.x + 1e-3))
            cobertas[num] = cobertas.get(num, True) and todos
            if tipo == "pele" and todos:
                regiao.append(f)
        # os vértices de fora (a casca) e o quanto cada um sai
        novo = {}
        base = len(co)
        for f in regiao:
            for i in f.vertices:
                if i in novo:
                    continue
                v = me.vertices[i]
                n = v.normal.copy()
                t = grossura
                hl = mao @ v.co
                dono = max(pv[i], key=pv[i].get).split(".")[0]
                if inteiros and PROTETOR[0] <= hl.y <= PROTETOR[1] and (mao.to_3x3() @ n).z > 0.35 and dono != "thumb" \
                        and not dono.startswith("thumb"):
                    t += PROTETOR[2]
                novo[i] = len(co)
                co.append(v.co + n * t)
                mulher.append((kb["Mulher"].data[i].co if kb else v.co) + n * t)
                pesos.append(pv[i])
        for f in regiao:
            idx = [novo[i] for i in f.vertices]
            hl = mao @ f.center
            nl = (mao.to_3x3() @ f.normal)
            dono = max((sum(pv[i].get(g, 0) for i in f.vertices), g) for g in pv[f.vertices[0]])[1].split(".")[0]
            if nl.z < -0.5 and dono != "forearm":
                parte = "B"
            elif dono == "forearm" and faixa[0] <= hl.y <= faixa[1]:
                parte = "G"
            elif inteiros and PROTETOR[0] <= hl.y <= PROTETOR[1] and nl.z > 0.35 and not dono.startswith("thumb"):
                parte = "G"
            else:
                parte = "R"
            faces.append(idx)
            partes.append(parte)
        # as bordas das aberturas (as arestas da região com uma face só)
        uso = {}
        for f in regiao:
            for e in f.edge_keys:
                uso.setdefault(e, []).append(f)
        dentro_i = {}
        for (a, b2), fs in uso.items():
            if len(fs) != 1:
                continue
            for i in (a, b2):
                if i not in dentro_i:
                    v = me.vertices[i]
                    dentro_i[i] = len(co)
                    co.append(v.co - v.normal * BORDA_DENTRO)
                    mulher.append((kb["Mulher"].data[i].co if kb else v.co) - v.normal * BORDA_DENTRO)
                    pesos.append(pv[i])
            f = fs[0]
            # a ordem da aresta na face (para a borda sair com a normal para fora da abertura)
            vs = list(f.vertices)
            ia, ib = vs.index(a), vs.index(b2)
            if (ia + 1) % len(vs) != ib:
                a, b2 = b2, a
            faces.append([novo[b2], novo[a], dentro_i[a], dentro_i[b2]])
            partes.append("R")
        del base
    me = bpy.data.meshes.new("Roupa_" + nome)
    me.from_pydata([tuple(c) for c in co], [], faces)
    me.update()
    mat = bpy.data.materials.get("Roupa_" + nome) or bpy.data.materials.new("Roupa_" + nome)
    me.materials.append(mat)
    ob = bpy.data.objects.new("Roupa_" + nome, me)
    bpy.context.scene.collection.objects.link(ob)
    grupos = {}
    for i, pw in enumerate(pesos):
        for g, w in pw.items():
            if g not in grupos:
                grupos[g] = ob.vertex_groups.new(name=g)
            grupos[g].add([i], w, "REPLACE")
    uv0 = me.uv_layers.new(name="UVMap")
    uv1 = me.uv_layers.new(name="Face")
    for fi, (f, parte) in enumerate(zip(me.polygons, partes)):
        cu, cv, meia = ilhas.get(parte, ilhas.get("R"))
        pts = [me.vertices[i].co for i in f.vertices]
        n = f.normal
        eixo = max(range(3), key=lambda k: abs(n[k]))
        a1, a2 = [k for k in range(3) if k != eixo]
        escala = meia / 0.12
        for li, pt in zip(f.loop_indices, pts):
            du = max(-meia, min(meia, (pt[a1] % 0.24 - 0.12) * escala))
            dv = max(-meia, min(meia, (pt[a2] % 0.24 - 0.12) * escala))
            uv0.data[li].uv = (cu + du, cv + dv)
            uv1.data[li].uv = (fi + 0.5, float(camada))
        f.use_smooth = True
    ob.shape_key_add(name="Basis", from_mix=False)
    km = ob.shape_key_add(name="Mulher", from_mix=False)
    for i, c in enumerate(mulher):
        km.data[i].co = c
    ob.parent = rig
    ob.matrix_parent_inverse = Matrix.Identity(4)
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    print(f"maos: {ob.name}: {len(me.vertices)} vértices, {sum(len(p.vertices) - 2 for p in me.polygons)} triângulos, "
          f"partes {dict((k, partes.count(k)) for k in 'RGB')}")
    return ob, cobertas


def _faixas_texto(nums):
    """Os números em faixas ("1-4 7 9-12"), como o catálogo das roupas."""
    nums = sorted(nums)
    out, i = [], 0
    while i < len(nums):
        j = i
        while j + 1 < len(nums) and nums[j + 1] == nums[j] + 1:
            j += 1
        out.append(str(nums[i]) if i == j else f"{nums[i]}-{nums[j]}")
        i = j + 1
    return " ".join(out)


def etapa_luvas(pasta=None):
    """As duas luvas novas (Luvas_Novas.fbx) e o que cada uma esconde do NPC_Corpo com o braço novo (os números das faces
    que ficam inteiras dentro dela: o "esconde" do catálogo; gravado em luvas_esconde.json na pasta das fotos)."""
    ag.nova_cena()
    novos = ag.importar(SAIDA_CORPO)
    rig = [o for o in novos if o.type == "ARMATURE"][0]
    bracos = sorted([o for o in novos if o.type == "MESH" and o.name.startswith("Mao_Corpo")], key=lambda o: o.name)
    for o in novos:
        if o.type == "MESH" and o not in bracos:          # (o braço de cima não entra nas luvas)
            bpy.data.objects.remove(o, do_unlink=True)
    for b in bracos:
        b.data.update()
    obs, esconde = [], {}
    for nome, spec in LUVAS.items():
        ob, cobertas = luva(nome, spec, rig, bracos)
        obs.append(ob)
        esconde[nome] = {"alvo": "NPC_Corpo", "faces": _faixas_texto(n for n, c in cobertas.items() if c)}
    for b in bracos:
        bpy.data.objects.remove(b, do_unlink=True)
    _limpar_rig(rig)
    _exportar(SAIDA_LUVAS, rig, obs)
    texto = json.dumps(esconde, indent=1, ensure_ascii=False)
    print("ESCONDE " + texto)
    if pasta:
        os.makedirs(pasta, exist_ok=True)
        with open(os.path.join(pasta, "luvas_esconde.json"), "w", encoding="utf-8") as f:
            f.write(texto)
    return esconde


def etapa_corpo():
    """A Mao_Corpo de cada lado e, desde 09/11/2026, a BracoCima_Corpo (o braço de cima novo, do braco_de_cima.py, que
    começa na borda do ombro e termina no primeiro anel da Mao_Corpo)."""
    import braco_de_cima as bc
    faces, formas = _ler_roupas()
    braco_hoje = bc.ler_braco_de_hoje()          # (o Roupas.fbx ainda está na cena)
    ag.nova_cena()
    novos = ag.importar(ag.FBX_CORPO)
    rig = bpy.data.objects[RIG_CORPO]
    velha = bpy.data.objects["Jogador_Corpo"]
    material = velha.data.materials[0]
    _limpar_rig(rig)
    obs = maos_do_rig(rig, velha, material, "Mao_Corpo", faces_velhas=faces, formas=formas)
    obs += bc.pecas(rig, material, obs, braco_hoje)
    for o in novos:
        if o is not rig:
            bpy.data.objects.remove(o, do_unlink=True)
    _exportar(SAIDA_CORPO, rig, obs)


# ================================================================================================ medir


def _medir_malha(o, rig, saida):
    """As caixas de cada osso do braço no espaço do osso (o repouso) e o corte do pulso."""
    pesos = _pesos(o)
    me = o.data
    r = {}
    for lado in LADOS:
        for nome in ["forearm." + lado] + ossos_da_mao(lado):
            b = rig.data.bones[nome]
            inv = b.matrix_local.inverted()
            pts = [inv @ (o.matrix_world @ me.vertices[i].co) for i, p in pesos.items() if p.get(nome, 0) > 0.5]
            if not pts:
                continue
            mn = [min(p[k] for p in pts) for k in range(3)]
            mx = [max(p[k] for p in pts) for k in range(3)]
            r[nome] = {"n": len(pts), "min": _r(mn), "max": _r(mx)}
    saida["caixas"] = r
    for lado in LADOS:
        b = rig.data.bones["forearm." + lado]
        inv = b.matrix_local.inverted()
        pts = [(i, inv @ (o.matrix_world @ me.vertices[i].co)) for i, p in pesos.items() if p.get(b.name, 0) > 0.5]
        ymax = max(p.y for _, p in pts)
        fim = sorted({tuple(_r(p, 4)) for _, p in pts if p.y > ymax - 0.03}, key=lambda p: (p[1], p[0], p[2]))
        saida["fim_do_antebraco_" + lado] = {"comprimento_do_osso": round(b.length, 4), "pontos": fim}
    for lado in LADOS:
        fs = faces_da_mao(o, lado)
        saida["mao_" + lado] = {"faces": len(fs), "triangulos": sum(len(me.polygons[f].vertices) - 2 for f in fs),
                                "vertices": len({v for f in fs for v in me.polygons[f].vertices})}
        fb = set(faces_do_braco(o, lado))
        so_ante = [f for f in fb if all(pesos[v].get("forearm." + lado, 0) > 0.5 for v in me.polygons[f].vertices)]
        emenda = [f for f in fb if any(pesos[v].get("upper_arm." + lado, 0) > 0.5 for v in me.polygons[f].vertices)]
        # as arestas que ficam abertas depois do corte (as de uma face só que tinham duas): o fim do braço de cima
        uso, uso_fica = {}, {}
        for f in me.polygons:
            for e in f.edge_keys:
                uso[e] = uso.get(e, 0) + 1
                if f.index not in fb:
                    uso_fica[e] = uso_fica.get(e, 0) + 1
        abertas = [e for e, n in uso_fica.items() if n == 1 and uso.get(e) == 2]
        saida["braco_" + lado] = {"faces": len(fb), "triangulos": sum(len(me.polygons[f].vertices) - 2 for f in fb),
                                  "so_antebraco": len(so_ante), "emenda_do_cotovelo": len(emenda),
                                  "arestas_abertas_depois_do_corte": len(abertas),
                                  "vertices_da_borda": len({v for e in abertas for v in e})}
    n = {}
    for i, p in pesos.items():
        if any(g in p for g in ossos_da_mao("L") + ossos_da_mao("R")):
            n[len(p)] = n.get(len(p), 0) + 1
    saida["ossos_por_vertice_da_mao"] = n
    # as células da linha de baixo da textura que a malha usa (no material da pele)
    if me.uv_layers:
        uv = me.uv_layers[0].data
        cel = {}
        for f in me.polygons:
            if f.material_index != 0:
                continue
            for li in f.loop_indices:
                u, v = uv[li].uv
                k = int(u // CELULA) if v < CELULA else "fora"
                cel[k] = cel.get(k, 0) + 1
        saida["celulas_usadas"] = {str(k): c for k, c in sorted(cel.items(), key=lambda x: str(x[0]))}
    return saida


def medir():
    rel = {}
    fbx_fp = sorted(f for f in os.listdir(ag.JOGADOR) if f.startswith("Jogador_FP_") and f.endswith(".fbx"))
    ref = None
    for f in fbx_fp:
        ag.nova_cena()
        novos = ag.importar(os.path.join(ag.JOGADOR, f))
        rigs = [o for o in novos if o.type == "ARMATURE"]
        if not rigs:
            continue
        rig = rigs[0]
        mats = _rest(rig)
        r = {"rig": rig.name, "malhas": {}}
        if ref is None:
            ref = mats
        else:
            nomes = [n for n in ref if n.split(".")[0] in OSSOS_DA_MAO + ["forearm", "upper_arm"]]
            difs = {}
            for n in nomes:
                if n not in mats or ag._dif(ref[n], mats[n]) < 1e-3:
                    continue
                pai = rig.data.bones[n].parent
                a = (ref[pai.name].inverted() @ ref[n]) if pai else ref[n]
                b = (mats[pai.name].inverted() @ mats[n]) if pai else mats[n]
                ang = math.degrees(a.to_quaternion().rotation_difference(b.to_quaternion()).angle)
                dl = (a.translation - b.translation).length * 100
                if ang > 0.01 or dl > 0.01:
                    difs[n] = (round(ang, 2), round(dl, 3))
            r["ossos_com_repouso_diferente_do_Maos (graus, cm em relação ao pai)"] = difs
        for o in novos:
            if o.type == "MESH" and any(g.name.startswith("hand.") for g in o.vertex_groups):
                r["malhas"][o.name] = _medir_malha(o, rig, {"materiais": [m.name for m in o.data.materials],
                                                            "triangulos": sum(len(p.vertices) - 2 for p in o.data.polygons)})
        rel[f] = r
    ag.nova_cena()
    ag.importar(ag.FBX_CORPO)
    rig = bpy.data.objects[RIG_CORPO]
    corpo = bpy.data.objects["Jogador_Corpo"]
    rel["Personagem_PrincipalBloco.fbx"] = {"Jogador_Corpo": _medir_malha(corpo, rig, {})}
    rc = _rest(rig)
    ag.nova_cena()
    novos = ag.importar(FBX_ROUPAS)
    rr = [o for o in novos if o.type == "ARMATURE"][0]
    npc = bpy.data.objects["NPC_Corpo"]
    r = {"dif_do_repouso_vs_PrincipalBloco": max((ag._dif(rc[n], m), n) for n, m in _rest(rr).items() if n in rc),
         "NPC_Corpo": _medir_malha(npc, rr, {"materiais": [m.name for m in npc.data.materials],
                                             "uvs": [u.name for u in npc.data.uv_layers]})}
    rel["Roupas.fbx"] = r
    return rel


# ================================================================================================ conferir


def conferir(pasta=None):
    """Os dois FBX novos importados de volta: o esqueleto igual ao de origem (as matrizes de repouso), as malhas, os
    triângulos, os grupos, os pesos por vértice, os UVs, as formas e se a malha é fechada (as bordas abertas)."""
    rel = {}
    for caminho, origem, rig_nome in ((SAIDA_FP, ag.FBX_MAOS, RIG_FP), (SAIDA_CORPO, ag.FBX_CORPO, RIG_CORPO)):
        ag.nova_cena()
        ag.importar(origem)
        ro = bpy.data.objects[rig_nome]
        ro.name = "Origem"
        for o in list(bpy.data.objects):
            if o is not ro:
                bpy.data.objects.remove(o, do_unlink=True)
        novos = ag.importar(caminho)
        rn = [o for o in novos if o.type == "ARMATURE"][0]
        info = {"armadura": rn.name, "ossos": len(rn.data.bones), "ossos_origem": len(ro.data.bones),
                "repouso_max_dif": max((ag._dif(b.matrix_local, rn.data.bones[b.name].matrix_local), b.name)
                                       for b in ro.data.bones if b.name in rn.data.bones),
                "objeto_rig": [_r(r_, 5) for r_ in rn.matrix_world]}
        for o in novos:
            if o.type != "MESH":
                continue
            me = o.data
            bm = bmesh.new()
            bm.from_mesh(me)
            abertas = sum(1 for e in bm.edges if not e.is_manifold)
            bm.free()
            pesos = _pesos(o)
            info[o.name] = {"vertices": len(me.vertices), "triangulos": sum(len(p.vertices) - 2 for p in me.polygons),
                            "materiais": [m.name for m in me.materials], "uvs": [u.name for u in me.uv_layers],
                            "grupos": sorted(g.name for g in o.vertex_groups),
                            "ossos_por_vertice": {k: sum(1 for p in pesos.values() if len(p) == k) for k in range(1, 5)},
                            "soma_dos_pesos": [round(min(sum(p.values()) for p in pesos.values()), 4),
                                               round(max(sum(p.values()) for p in pesos.values()), 4)],
                            "formas": [k.name for k in me.shape_keys.key_blocks] if me.shape_keys else [],
                            "arestas_nao_manifold": abertas}
        if caminho == SAIDA_CORPO:
            info["escondidas_pelas_pecas"] = _faces_escondidas([o for o in novos if o.type == "MESH"])
        rel[os.path.basename(caminho)] = info
    # a mesma conta na mão de hoje do NPC_Corpo (as faces do braço de cada lado)
    ag.nova_cena()
    ag.importar(FBX_ROUPAS)
    npc = bpy.data.objects["NPC_Corpo"]
    rel["NPC_Corpo_hoje_escondidas_pelas_pecas"] = _faces_escondidas([npc], lados=("L",))
    rel["roupas_furadas"] = conferir_roupas()
    if pasta:
        with open(os.path.join(pasta, "roupas_esconde_corrigido.json"), "w", encoding="utf-8") as f:
            f.write(json.dumps(conferir_roupas.corrigidas, indent=1, ensure_ascii=False))
    return rel


FOLGA_DA_ROUPA = 0.0005


def _esconde_do_catalogo():
    cat = json.load(open(os.path.join(os.path.dirname(FBX_ROUPAS), "Roupas.json"), encoding="utf-8"))
    esc, lugar = {}, {}
    for p in cat["pecas"]:
        lugar[p["nome"]] = p.get("lugar")
        esc[p["nome"]] = set()
        for e in p.get("esconde") or []:
            if e["alvo"] == "NPC_Corpo":
                esc[p["nome"]] |= _faixas(e["faces"])
    return esc, lugar


def _coords(ob, dg):
    ev = ob.evaluated_get(dg)
    me = ev.to_mesh()
    cs = [ob.matrix_world @ v.co for v in me.vertices]
    fs = [tuple(f.vertices) for f in me.polygons]
    ev.to_mesh_clear()
    return cs, fs


def conferir_roupas(lado="L"):
    """As peças do Roupas.fbx que vestem o antebraço, o pulso ou a mão (as com vértices com mais da metade do peso
    neles): para cada uma, no corpo do homem e no da mulher (a forma "Mulher"), quantos vértices da pele à mostra (os de
    faces que a peça não esconde, pelo número da face) ficam fora da peça ou a menos de 0,5 mm dela, no braço de hoje
    (o NPC_Corpo) e no novo. A conta: um raio do eixo do osso dono do vértice, passando por ele; se a peça está no
    caminho antes do vértice (mais a folga), ele fura. Devolve, por peça: o lugar, e para hoje e o novo, nas duas formas,
    (vértices que furam, o mais fundo em mm)."""
    esc, lugar = _esconde_do_catalogo()
    ag.nova_cena()
    novos = ag.importar(FBX_ROUPAS)
    rig = [o for o in novos if o.type == "ARMATURE"][0]
    npc = bpy.data.objects["NPC_Corpo"]
    pecas = [o for o in novos if o.type == "MESH" and o.name.startswith("Roupa_")]
    nova = [o for o in ag.importar(SAIDA_CORPO) if o.type == "MESH" and o.name == "Mao_Corpo." + lado][0]
    ossos_braco = set(ossos_da_mao(lado)) | {"forearm." + lado}
    # as peças do antebraço e da mão
    vestem = []
    for pc in pecas:
        nomes = {g.index: g.name for g in pc.vertex_groups}
        if any(nomes[g.group] in ossos_braco and g.weight > 0.5 for v in pc.data.vertices for g in v.groups):
            vestem.append(pc)

    def eixo(osso):
        b = rig.data.bones[osso]
        return rig.matrix_world @ b.head_local, (rig.matrix_world.to_3x3() @ (b.tail_local - b.head_local)).normalized()

    def pele(ob, so_braco):
        """Os vértices (índice, osso dono) e, de cada face, os vértices e o número da face."""
        nomes = {g.index: g.name for g in ob.vertex_groups}
        uvf = ob.data.uv_layers["Face"].data
        dono = {}
        for v in ob.data.vertices:
            gs = [(g.weight, nomes[g.group]) for g in v.groups]
            if gs:
                dono[v.index] = max(gs)[1]
        faces = []
        for f in ob.data.polygons:
            if so_braco and not all(dono.get(v) in ossos_braco for v in f.vertices):
                continue
            faces.append((tuple(f.vertices), int(math.floor(uvf[f.loop_indices[0]].uv.x + 1e-3))))
        return dono, faces

    peles = {"hoje": (npc,) + pele(npc, True), "novo": (nova,) + pele(nova, False)}
    rel = {}
    buracos = {}
    for forma in ("Basis", "Mulher"):
        for ob in [npc, nova] + vestem:
            if ob.data.shape_keys and "Mulher" in ob.data.shape_keys.key_blocks:
                ob.data.shape_keys.key_blocks["Mulher"].value = 1.0 if forma == "Mulher" else 0.0
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        coords = {qual: _coords(ob, dg)[0] for qual, (ob, _, _) in peles.items()}
        for pc in vestem:
            cs, fs = _coords(pc, dg)
            bvh = BVHTree.FromPolygons([tuple(c) for c in cs], fs)
            nome = pc.name[len("Roupa_"):]
            esconde = esc.get(nome, set())
            r = rel.setdefault(nome, {"lugar": lugar.get(nome)})
            for qual, (ob, dono, faces) in peles.items():
                visiveis = {v for vs, num in faces if num not in esconde for v in vs}
                fura, fundo = 0, 0.0
                for i in visiveis:
                    osso = dono.get(i)
                    if osso is None or osso not in rig.data.bones:
                        continue
                    a, d = eixo(osso)
                    p = coords[qual][i]
                    q = a + d * (p - a).dot(d)
                    r_v = (p - q).length
                    if r_v < 1e-5:
                        continue
                    hit = bvh.ray_cast(q, (p - q).normalized(), r_v + 0.05)
                    if hit[0] is not None and hit[3] < r_v + FOLGA_DA_ROUPA:
                        fura += 1
                        fundo = max(fundo, (r_v + FOLGA_DA_ROUPA - hit[3]) * 1000)
                r[f"{qual}_{forma}"] = (fura, round(fundo, 1))
                # os buracos (só no braço novo): um número que a peça esconde e que também está numa face nova que ela
                # não cobre (uma face de fora da peça sumiria junto); a lista do "esconde" sem esses números
                if qual == "novo":
                    tirar = set()
                    for vs, num in faces:
                        if num not in esconde:
                            continue
                        for i in vs:
                            osso = dono.get(i)
                            if osso is None or osso not in rig.data.bones:
                                continue
                            a, d = eixo(osso)
                            pv = coords[qual][i]
                            q = a + d * (pv - a).dot(d)
                            r_v = (pv - q).length
                            if r_v < 1e-5:
                                continue
                            hit = bvh.ray_cast(q, (pv - q).normalized(), r_v + 0.08)
                            if hit[0] is None or hit[3] < r_v - 0.002:
                                tirar.add(num)
                                break
                    r[f"buracos_{forma}"] = len(tirar)
                    buracos.setdefault(nome, set()).update(tirar)
    # as listas do "esconde" sem os números que abririam buracos no braço novo (as outras ficam como estão; as luvas
    # novas têm as listas delas no luvas_esconde.json)
    corrigidas = {}
    for nome, nums in buracos.items():
        if nums and not nome.startswith("Luva_"):
            corrigidas[nome] = {"alvo": "NPC_Corpo", "faces": _faixas_texto(sorted(esc.get(nome, set()) - nums)),
                                "tirados": _faixas_texto(sorted(nums))}
    rel["esconde_corrigido"] = corrigidas
    conferir_roupas.corrigidas = corrigidas
    return rel


def _faixas(texto):
    faces = set()
    for item in texto.split():
        a, _, b = item.partition("-")
        faces.update(range(int(a), int(b or a) + 1))
    return faces


PECAS_PARA_CONFERIR = ("Luva_Tatica", "Luva_Sem_Dedos", "Jaqueta_Couro", "Camiseta")


def _faces_escondidas(maos, lados=None):
    """Quantas faces de cada parte da mão nova (pelo osso dono) cada peça do catálogo das roupas esconde (pelo número
    da face no UV "Face"), e o mesmo na mão de hoje do NPC_Corpo."""
    cat = json.load(open(os.path.join(os.path.dirname(FBX_ROUPAS), "Roupas.json"), encoding="utf-8"))
    esconde = {}
    for p in cat.get("pecas", cat if isinstance(cat, list) else []):
        if p.get("nome") in PECAS_PARA_CONFERIR:
            esconde[p["nome"]] = set()
            for e in p.get("esconde") or []:
                if e["alvo"] == "NPC_Corpo":
                    esconde[p["nome"]] |= _faixas(e["faces"])

    def parte(osso):
        b = osso.split(".")[0]
        return ("antebraco" if b == "forearm" else "palma" if b == "hand" else "polegar" if b.startswith("thumb")
                else "braco_de_cima" if b in ("upper_arm", "shoulder", "chest") else "dedos")

    rel = {}
    for ob in maos:
        me = ob.data
        uvf = me.uv_layers["Face"].data
        nomes = {g.index: g.name for g in ob.vertex_groups}
        cont = {}
        for f in me.polygons:
            soma = {}
            for v in f.vertices:
                for g in me.vertices[v].groups:
                    soma[nomes[g.group]] = soma.get(nomes[g.group], 0.0) + g.weight
            dono = max(soma, key=soma.get)
            if lados and (dono.split(".")[0] not in OSSOS_DA_MAO + ["forearm"] or not dono.endswith(lados)):
                continue
            pt = parte(dono)
            num = int(math.floor(uvf[f.loop_indices[0]].uv.x + 1e-3))
            c = cont.setdefault(pt, {"faces": 0})
            c["faces"] += 1
            for peca, fs in esconde.items():
                if num in fs:
                    c[peca] = c.get(peca, 0) + 1
        rel[ob.name] = cont
    return rel


# ================================================================================================ as poses


def _pose_estatica(rig, acao, quadro=1):
    """A pose de um quadro de uma ação, posta direto nos ossos (sem a ação)."""
    ag.usar_acao(rig, acao)
    bpy.context.scene.frame_set(quadro)
    vals = {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy(), pb.scale.copy()) for pb in rig.pose.bones}
    ag.usar_acao(rig, None)
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.location, pb.rotation_quaternion, pb.scale = vals[pb.name]
    bpy.context.view_layer.update()
    return vals


def _punho(rig):
    """Os giros dos dedos no punho fechado (o quadro 1 do Idle dos braços de primeira pessoa)."""
    q = {}
    for lado in LADOS:
        for n in ossos_da_mao(lado):
            if n.startswith("hand"):
                continue
            q[n] = rig.pose.bones[n].rotation_quaternion.copy()
    return q


def maos_abertas(rig, punho):
    d = ag.Dedos(punho)
    for lado in LADOS:
        for n, q in d.aberta(lado).items():
            rig.pose.bones[n].rotation_quaternion = q
    bpy.context.view_layer.update()


def _cabo_no_mundo(rig, lado):
    """O eixo do cabo (um ponto e a direção) no espaço da armadura, preso à mão do lado (na pose)."""
    M = rig.pose.bones["hand." + lado].matrix
    sx = 1.0 if lado == "L" else -1.0
    g = math.radians(CABO_GIRO)
    c = M @ Vector((sx * CABO_MEIO[0], CABO_MEIO[1], CABO_MEIO[2]))
    d = (M.to_3x3() @ Vector((sx * math.cos(g), math.sin(g), 0.0))).normalized()
    return c, d


def _dist_ao_eixo(p, c, d):
    v = p - c
    return (v - v.dot(d) * d).length


def mao_no_cabo(rig, lado, mao_nova):
    """Fecha a mão do lado em volta do cabo (pelos pontos da pele da mão nova presos a cada osso): cada falange dos dedos
    gira para a palma até a pele dela encostar no cabo; o polegar procura (girando para a palma e dobrando) o lugar mais
    perto da falange do meio do indicador sem entrar no cabo, e a ponta dele dobra até encostar. Põe a pose nos ossos e
    devolve os ângulos."""
    bpy.context.view_layer.update()
    c, d = _cabo_no_mundo(rig, lado)
    R = CABO_RAIO + CABO_FOLGA
    ossos = rig.data.bones
    pesos = _pesos(mao_nova)

    def rel(n):
        return ossos[n].parent.matrix_local.inverted() @ ossos[n].matrix_local

    def pontos(n, depois=0.0):
        """Os pontos da pele presos ao osso (no espaço dele), só os depois da junta (y > depois)."""
        inv = ossos[n].matrix_local.inverted()
        pts = [inv @ mao_nova.data.vertices[i].co for i, p in pesos.items() if p.get(n, 0) >= 0.5]
        return [q for q in pts if q.y > depois]

    def perto(M, pts):
        return min(_dist_ao_eixo(M @ p, c, d) for p in pts)

    def dobra(a):
        return Matrix.Rotation(math.radians(-a), 4, "X")

    Mh = rig.pose.bones["hand." + lado].matrix.copy()
    giros = {}
    posado = {}
    for dd in DEDOS:
        n1, n2 = f"{dd}1.{lado}", f"{dd}2.{lado}"
        p1, p2 = pontos(n1, 0.004), pontos(n2, 0.010)
        a1 = 0
        while a1 < 130 and perto(Mh @ rel(n1) @ dobra(a1), p1) > R:
            a1 += 1
        M1 = Mh @ rel(n1) @ dobra(a1)
        a2 = 0
        while a2 < 130 and perto(M1 @ rel(n2) @ dobra(a2), p2) > R:
            a2 += 1
        giros[n1], giros[n2] = a1, a2
        posado[n2] = M1 @ rel(n2) @ dobra(a2)
    # o polegar
    nt, nt2 = "thumb." + lado, "thumb2." + lado
    pt1, pt2 = pontos(nt, 0.012), pontos(nt2, 0.010)
    sz = 1.0 if lado == "L" else -1.0
    alvo = posado["index2." + lado] @ Vector((0.0, ossos["index2." + lado].length * 0.5, 0.0))
    comp2 = ossos[nt2].length
    melhor = None
    for a in range(-30, 101, 5):
        for b in range(-40, 91, 5):
            q = Matrix.Rotation(math.radians(sz * a), 4, "Z") @ dobra(b)
            Mt = Mh @ rel(nt) @ q
            Mt2 = Mt @ rel(nt2)
            dm = min(perto(Mt, pt1), perto(Mt2, pt2))
            if dm < R:
                continue
            s = ((Mt2 @ Vector((0.0, comp2, 0.0))) - alvo).length + 0.3 * (dm - R)
            if melhor is None or s < melhor[0]:
                melhor = (s, a, b, q, Mt)
    if melhor is None:
        q, Mt, giros[nt] = Matrix.Identity(4), Mh @ rel(nt), None
    else:
        _, a, b, q, Mt = melhor
        giros[nt] = (a, b)
    a3 = 0
    while a3 < 90 and perto(Mt @ rel(nt2) @ dobra(a3), pt2) > R:
        a3 += 1
    giros[nt2] = a3
    # na pose
    for n, a in giros.items():
        pb = rig.pose.bones[n]
        pb.rotation_mode = "QUATERNION"
        if n == nt:
            pb.rotation_quaternion = q.to_quaternion()
        else:
            pb.rotation_quaternion = dobra(a).to_quaternion()
    bpy.context.view_layer.update()
    return giros


# ================================================================================================ as fotos


def _cena_com_maos(fbx_velho, rig_nome, malha_nome, fbx_novo):
    """O FBX de hoje (o rig e a malha dos braços ou do corpo) e as mãos novas (importadas de volta do FBX novo) presas ao
    mesmo rig. Devolve (rig, malha de hoje, malha nova: a de hoje sem a mão de hoje e com as novas, numa malha só)."""
    ag.nova_cena()
    ag.importar(fbx_velho)
    rig = bpy.data.objects[rig_nome]
    velha = bpy.data.objects[malha_nome]
    for o in list(bpy.data.objects):
        if o not in (rig, velha):
            bpy.data.objects.remove(o, do_unlink=True)
    novos = ag.importar(fbx_novo)
    rig2 = [o for o in novos if o.type == "ARMATURE"][0]
    maos = [o for o in novos if o.type == "MESH"]
    # a malha nova: a de hoje sem as faces da mão (dos dois lados) e com as duas mãos novas
    nova = velha.copy()
    nova.data = velha.data.copy()
    nova.name = malha_nome + "_Nova"
    bpy.context.scene.collection.objects.link(nova)
    tirar = faces_do_corte(nova, maos)
    bm = bmesh.new()
    bm.from_mesh(nova.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in tirar], context="FACES")
    soltos = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=soltos, context="VERTS")
    bm.to_mesh(nova.data)
    bm.free()
    if nova.data.shape_keys:
        nova.shape_key_clear()
    for m in maos:
        m.parent = None
        m.matrix_world = rig.matrix_world.copy()
        if m.data.shape_keys:
            m.shape_key_clear()
        for md in m.modifiers:
            if md.type == "ARMATURE":
                md.object = rig
    for o in bpy.context.scene.objects:
        o.select_set(o in [nova] + maos)
    bpy.context.view_layer.objects.active = nova
    with bpy.context.temp_override(active_object=nova, selected_editable_objects=[nova] + maos,
                                   selected_objects=[nova] + maos):
        bpy.ops.object.join()
    for md in nova.modifiers:
        if md.type == "ARMATURE":
            md.object = rig
    bpy.data.objects.remove(rig2, do_unlink=True)
    # no repouso (sem a ação que veio no FBX)
    ag.usar_acao(rig, None)
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)
    bpy.context.view_layer.update()
    # (a facetada, para comparar o estilo, saiu: a lisa foi a aprovada)
    return rig, velha, nova, None


def _texturas(pasta):
    """As texturas da proposta (a célula 7 mais clara, a das unhas), gravadas na pasta das fotos: a do personagem e as
    duas do corpo das roupas. Devolve os caminhos."""
    import numpy as np
    saidas = {}
    for nome, cor, caminho in (("Personagem_PrincipalBloco", UNHA_CLARA, ag.TEXTURA),
                               ("T_Corpo_Cor", (UNHA_CINZA,) * 3, T_CORPO_COR),
                               ("T_Corpo_Mascara", (UNHA_MASCARA, 0, 0), T_CORPO_MASCARA)):
        img = bpy.data.images.load(caminho, check_existing=False)
        w, h = img.size
        px = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        px = px.reshape(h, w, 4)
        c0 = int(7 * CELULA * w)
        c1 = int(8 * CELULA * w)
        r1 = int(CELULA * h)
        px[0:r1, c0:c1, 0:3] = [c / 255.0 for c in cor]
        nova = bpy.data.images.new("proposta_" + nome, w, h, alpha=False)
        if nome == "T_Corpo_Mascara":
            nova.colorspace_settings.name = "Non-Color"
        nova.pixels.foreach_set(px.ravel())
        s = os.path.join(pasta, "proposta_" + nome + ".png")
        nova.filepath_raw = s
        nova.file_format = "PNG"
        nova.save()
        saidas[nome] = s
        bpy.data.images.remove(img)
    return saidas


def _srgb_para_linear(c):
    import numpy as np
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def _linear_para_srgb(c):
    import numpy as np
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


TONS = [("Pele_Padrao", "#D08A63", "#2B1E16"), ("Pele_Escura", "#6B4330", "#141010"),
        ("Pele_Clara", "#EBC3A4", "#A87B4A")]
CUECA = "#434348"


def _hex(h):
    h = h.lstrip("#")
    return [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]


def _textura_do_tom(pasta, cor, mascara, pele, pelos):
    """A cor do corpo pelo shader das roupas (na luz linear): base x ((1 - (R + G + B)) + 2 x (pele R + cueca G +
    pelos B)), gravada como imagem (para as fotos no Workbench)."""
    import numpy as np
    ims = []
    for c in (cor, mascara):
        img = bpy.data.images.load(c, check_existing=False)
        w, h = img.size
        px = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        ims.append(px.reshape(h, w, 4)[:, :, :3].copy())
        bpy.data.images.remove(img)
    base = _srgb_para_linear(ims[0])
    m = ims[1]
    c1 = _srgb_para_linear(np.array(_hex(pele)))
    c2 = _srgb_para_linear(np.array(_hex(CUECA)))
    c3 = _srgb_para_linear(np.array(_hex(pelos)))
    tinta = m[:, :, 0:1] * c1 + m[:, :, 1:2] * c2 + m[:, :, 2:3] * c3
    alb = base * ((1.0 - m.sum(axis=2, keepdims=True)) + 2.0 * tinta)
    out = np.ones((h, w, 4), dtype=np.float32)
    out[:, :, :3] = _linear_para_srgb(alb)
    nome = f"tom_{pele[1:]}_{pelos[1:]}"
    img = bpy.data.images.new(nome, w, h, alpha=False)
    img.pixels.foreach_set(out.ravel())
    s = os.path.join(pasta, nome + ".png")
    img.filepath_raw = s
    img.file_format = "PNG"
    img.save()
    return s


def _mostrar(objs, visiveis):
    for o in objs:
        if o is not None:
            o.hide_render = o not in visiveis


def _cilindro(nome, raio, comprimento, lados=20):
    me = bpy.data.meshes.new(nome)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=lados, radius1=raio, radius2=raio, depth=comprimento)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(nome, me)
    bpy.context.scene.collection.objects.link(ob)
    mat = bpy.data.materials.new(nome)
    mat.diffuse_color = (0.55, 0.42, 0.28, 1)
    me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = True
    return ob


def _por_o_cabo(cil, rig, lado):
    c, d = _cabo_no_mundo(rig, lado)
    m = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    m.translation = c
    cil.matrix_world = rig.matrix_world @ m


def _ponto_da_mao(rig, lado, x=0.0, y=0.05, z=0.0):
    M = rig.pose.bones["hand." + lado].matrix
    sx = 1.0 if lado == "L" else -1.0
    return rig.matrix_world @ (M @ Vector((sx * x, y, z)))


def _eixos_da_mao(rig, lado):
    """As direções da mão no mundo (na pose): para os dedos, para as costas e para o lado do polegar."""
    M = (rig.matrix_world @ rig.pose.bones["hand." + lado].matrix).to_3x3()
    sx = 1.0 if lado == "L" else -1.0
    return (M @ Vector((0, 1, 0))).normalized(), (M @ Vector((0, 0, 1))).normalized(), (M @ Vector((sx, 0, 0))).normalized()


def _foto(pasta, nome, objs, hoje, nova, facetada=None):
    """As fotos com a mesma câmera: a mão de hoje, a nova e (se vier) a nova facetada."""
    saidas = []
    for qual, ob in (("hoje", hoje), ("nova", nova), ("facetada", facetada)):
        if ob is None:
            continue
        _mostrar(objs, [ob] + [o for o in objs if o is not None and o.name.startswith(("Cabo", "Marca"))])
        c = os.path.join(pasta, f"{nome}_{qual}.png")
        ag._salvar(c)
        saidas.append(c)
    return saidas


def fotos_fp(pasta, textura):
    """Pela câmera dos braços (55°): o Idle dos punhos, as mãos abertas e a direita fechada no cabo."""
    rig, velha, nova, fac = _cena_com_maos(ag.FBX_MAOS, RIG_FP, "FP_Bracos_Maos", SAIDA_FP)
    ag._textura_no_material("PrincipalBloco", textura)
    ag._render_base(960, 540)
    ag._camera("Olho", (0, 0, 0), rot=(math.radians(90), 0, math.radians(180)), angulo_y=55)
    cil = _cilindro("Cabo", CABO_RAIO, 0.30)
    objs = [velha, nova, fac, cil]
    saidas = []
    _pose_estatica(rig, bpy.data.actions[RIG_FP + "|Idle"])
    punho = _punho(rig)
    cil.hide_render = True
    saidas += _foto(pasta, "FP_1Idle", [velha, nova, fac], velha, nova, fac)
    maos_abertas(rig, punho)
    saidas += _foto(pasta, "FP_2Aberta", [velha, nova, fac], velha, nova, fac)
    # o cabo na direita (a mão nova decide onde as falanges encostam; a de hoje usa a mesma pose)
    _pose_estatica(rig, bpy.data.actions[RIG_FP + "|Idle"])
    giros = mao_no_cabo(rig, "R", _so_a_mao(nova, "R"))
    print("maos: pegada no cabo (graus de dobra; o polegar: o giro para a palma e a dobra):", giros)
    _por_o_cabo(cil, rig, "R")
    cil.hide_render = False
    saidas += _foto(pasta, "FP_3Cabo", objs, velha, nova, fac)
    return saidas


def _so_a_mao(ob, lado):
    """Uma cópia (fora da cena) só com os vértices da mão de um lado (para medir onde a pele encosta)."""
    me = ob.data.copy()
    tmp = bpy.data.objects.new("tmp_mao", me)
    for g in ob.vertex_groups:
        tmp.vertex_groups.new(name=g.name)
    nomes = {g.index: g.name for g in ob.vertex_groups}
    bm = bmesh.new()
    bm.from_mesh(me)
    dl = bm.verts.layers.deform.active
    tirar = [v for v in bm.verts if not _na_mao({nomes[k]: w for k, w in v[dl].items()}, lado)]
    bmesh.ops.delete(bm, geom=tirar, context="VERTS")
    bm.to_mesh(me)
    bm.free()
    return tmp


def fotos_perto(pasta, textura):
    """De perto, no repouso (os braços abertos, as palmas para baixo): o dorso, a palma, a frente (o lado do polegar) e
    de 3/4 da esquerda; o dorso e a palma da direita; e as unhas da direita bem de perto (os dedos e o polegar). Com a
    nova facetada (as normais chapadas) ao lado, para comparar o estilo."""
    rig, velha, nova, fac = _cena_com_maos(ag.FBX_MAOS, RIG_FP, "FP_Bracos_Maos", SAIDA_FP)
    ag._textura_no_material("PrincipalBloco", textura)
    ag._render_base(640, 480)
    objs = [velha, nova, fac]
    saidas = []
    for lado in LADOS:
        c = _ponto_da_mao(rig, lado, 0.0, 0.095, 0.0)
        dedos, costas, polegar = _eixos_da_mao(rig, lado)
        vistas = [("dorso", c + costas * 0.42, dedos, c), ("palma", c - costas * 0.42, dedos, c)]
        if lado == "L":
            vistas += [("frente", c + polegar * 0.42, costas, c),
                       ("34", c + (costas + polegar * 0.8 - dedos * 0.6).normalized() * 0.42, costas, c)]
        else:
            u = _ponto_da_mao(rig, lado, 0.0, 0.175, 0.0)
            vistas += [("unhas", u + (costas * 1.0 + dedos * 0.35).normalized() * 0.16, dedos, u)]
            t = rig.matrix_world @ rig.pose.bones["thumb2." + lado].matrix @ Vector((0.0, 0.03, 0.0))
            vistas += [("unha_polegar", t + (costas * 0.6 + polegar * 1.0).normalized() * 0.14, dedos, t)]
        for nome, pos, cima, alvo in vistas:
            cam = ag._camera("Perto", pos, alvo=alvo, lente=50)
            cam.rotation_euler = _olhar(pos, alvo, cima).to_euler()
            saidas += _foto(pasta, f"Perto_Repouso{lado}_{nome}", objs, velha, nova, fac)
    return saidas


def _olhar(pos, alvo, cima):
    d = (Vector(alvo) - Vector(pos)).normalized()
    x = d.cross(cima).normalized()
    y = x.cross(d).normalized()
    return Matrix((x, y, -d)).transposed().to_quaternion()


def fotos_poses(pasta, textura):
    """As mãos em pose, de perto: o punho do Idle, aberta e a direita no cabo (de três lados)."""
    rig, velha, nova, fac = _cena_com_maos(ag.FBX_MAOS, RIG_FP, "FP_Bracos_Maos", SAIDA_FP)
    ag._textura_no_material("PrincipalBloco", textura)
    ag._render_base(640, 480)
    cil = _cilindro("Cabo", CABO_RAIO, 0.30)
    objs = [velha, nova, fac, cil]
    saidas = []
    _pose_estatica(rig, bpy.data.actions[RIG_FP + "|Idle"])
    punho = _punho(rig)
    cil.hide_render = True

    def vistas(lado, prefixo, lista, objs):
        c = _ponto_da_mao(rig, lado, 0.0, 0.07, -0.01)
        dedos, costas, polegar = _eixos_da_mao(rig, lado)
        out = []
        for nome, d, cima in lista:
            v = (dedos * d[0] + costas * d[1] + polegar * d[2]).normalized()
            pos = c + v * 0.32
            cam = ag._camera("Perto", pos, alvo=c, lente=50)
            cam.rotation_euler = _olhar(pos, c, cima(dedos, costas, polegar)).to_euler()
            out += _foto(pasta, f"Poses_{prefixo}_{nome}", objs, velha, nova, fac)
        return out
    cima_dedos = lambda de, co, po: de
    cima_costas = lambda de, co, po: co
    saidas += vistas("R", "Punho", [("dedos", (1.0, 0.3, 0.2), cima_costas), ("polegar", (0.2, 0.4, 1.0), cima_costas),
                                    ("costas", (0.3, 1.0, 0.3), cima_dedos)], [velha, nova, fac])
    maos_abertas(rig, punho)
    saidas += vistas("R", "Aberta", [("costas", (0.2, 1.0, 0.4), cima_dedos), ("palma", (1.0, -0.7, 0.0), cima_dedos)],
                     [velha, nova, fac])
    _pose_estatica(rig, bpy.data.actions[RIG_FP + "|Idle"])
    mao_no_cabo(rig, "R", _so_a_mao(nova, "R"))
    _por_o_cabo(cil, rig, "R")
    cil.hide_render = False
    saidas += vistas("R", "Cabo", [("polegar", (0.2, 0.4, 1.0), cima_costas), ("mindinho", (0.2, 0.3, -1.0), cima_costas),
                                   ("dedos", (1.0, -0.2, 0.1), cima_costas), ("costas", (0.1, 1.0, 0.2), cima_dedos)],
                     objs)
    return saidas


def fotos_corpo(pasta, textura):
    """O corpo parado (o quadro 1 do Parado): as mãos de perto, de frente e de lado, e o corpo inteiro."""
    rig, velha, nova, fac = _cena_com_maos(ag.FBX_CORPO, RIG_CORPO, "Jogador_Corpo", SAIDA_CORPO)
    ag._textura_no_material("PrincipalBloco", textura)
    ag._render_base(640, 480)
    _pose_estatica(rig, bpy.data.actions[RIG_CORPO + "|Parado"])
    objs = [velha, nova, fac]
    saidas = []
    for lado in LADOS:
        c = _ponto_da_mao(rig, lado, 0.0, 0.06, 0.0)
        fora = Vector((1.0 if lado == "L" else -1.0, 0.0, 0.0))
        for nome, pos in (("frente", c + Vector((0.0, -0.42, 0.04))), ("lado", c + fora * 0.42 + Vector((0, 0, 0.04))),
                          ("tras", c + Vector((0.0, 0.42, 0.04)))):
            cam = ag._camera("Perto", pos, alvo=c, lente=50)
            cam.rotation_euler = _olhar(pos, c, Vector((0, 0, 1))).to_euler()
            saidas += _foto(pasta, f"Corpo_Mao{lado}_{nome}", objs, velha, nova, fac)
    # o braço esquerdo inteiro (o ombro ao fim da mão), de frente e de lado, para ver a proporção com o braço de cima
    ombro = rig.matrix_world @ rig.pose.bones["upper_arm.L"].head
    mao = _ponto_da_mao(rig, "L", 0.0, 0.10, 0.0)
    meio = (ombro + mao) / 2
    for nome, pos in (("BracoL_frente", meio + Vector((0.05, -1.05, 0.05))), ("BracoL_lado", meio + Vector((1.05, 0.0, 0.05)))):
        cam = ag._camera("Perto", pos, alvo=meio, lente=50)
        cam.rotation_euler = _olhar(pos, meio, Vector((0, 0, 1))).to_euler()
        saidas += _foto(pasta, f"Corpo_{nome}", objs, velha, nova, fac)
    ag._render_base(480, 640)
    for nome, pos, alvo in (("inteiro", (0.9, -3.4, 1.15), (0, 0, 0.95)),):
        ag._camera("Inteiro", pos, alvo=alvo, lente=40)
        saidas += _foto(pasta, f"Corpo_{nome}", objs, velha, nova, fac)
    return saidas


def _eixos_do_osso(rig, osso, lado):
    """As direções de um osso no mundo (na pose): ao longo dele, para as costas e para o lado do polegar."""
    M = (rig.matrix_world @ rig.pose.bones[osso + "." + lado].matrix).to_3x3()
    sx = 1.0 if lado == "L" else -1.0
    return (M @ Vector((0, 1, 0))).normalized(), (M @ Vector((0, 0, 1))).normalized(), (M @ Vector((sx, 0, 0))).normalized()


def fotos_pulso(pasta, textura):
    """O pulso e o cotovelo de perto, hoje e o novo: o antebraço inteiro no repouso (de lado e de cima); a mão direita
    no cabo com o pulso dobrado para cima e para baixo (50 graus; de lado e de cima); e o cotovelo dobrado (o Idle dos
    braços de primeira pessoa; de lado e de fora)."""
    rig, velha, nova, fac = _cena_com_maos(ag.FBX_MAOS, RIG_FP, "FP_Bracos_Maos", SAIDA_FP)
    ag._textura_no_material("PrincipalBloco", textura)
    ag._render_base(640, 480)
    cil = _cilindro("Cabo", CABO_RAIO, 0.30)
    cil.hide_render = True
    saidas = []
    lado = "R"
    # o antebraço inteiro, no repouso
    cot = rig.matrix_world @ rig.pose.bones["forearm." + lado].head
    ponta = _ponto_da_mao(rig, lado, 0.0, 0.20, 0.0)
    meio = (cot + ponta) / 2 + (ponta - cot).normalized() * -0.07
    ao_longo, costas, polegar = _eixos_do_osso(rig, "forearm", lado)
    for nome, pos, cima in (("1Repouso_lado", meio + polegar * 0.80, costas), ("1Repouso_cima", meio + costas * 0.80, polegar)):
        cam = ag._camera("Perto", pos, alvo=meio, lente=50)
        cam.rotation_euler = _olhar(pos, meio, cima).to_euler()
        saidas += _foto(pasta, f"Pulso_{nome}", [velha, nova], velha, nova)
    # o pulso dobrado, com a mão no cabo
    so_mao = _so_a_mao(nova, lado)
    for nome, graus in (("2Cima", 50.0), ("3Baixo", -50.0)):
        for pb in rig.pose.bones:
            pb.rotation_mode = "QUATERNION"
            pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)
        rig.pose.bones["hand." + lado].rotation_quaternion = Quaternion((1, 0, 0), math.radians(graus))
        bpy.context.view_layer.update()
        mao_no_cabo(rig, lado, so_mao)
        _por_o_cabo(cil, rig, lado)
        cil.hide_render = False
        pulso = rig.matrix_world @ rig.pose.bones["hand." + lado].head
        ao_longo, costas, polegar = _eixos_do_osso(rig, "forearm", lado)
        alvo = pulso + ao_longo * 0.01
        for vista, pos, cima in (("lado", alvo + polegar * 0.36, costas), ("cima", alvo + costas * 0.36, -ao_longo)):
            cam = ag._camera("Perto", pos, alvo=alvo, lente=50)
            cam.rotation_euler = _olhar(pos, alvo, cima).to_euler()
            saidas += _foto(pasta, f"Pulso_{nome}_{vista}", [velha, nova, cil], velha, nova)
        cil.hide_render = True
    # o cotovelo dobrado (o Idle)
    _pose_estatica(rig, bpy.data.actions[RIG_FP + "|Idle"])
    ombro = rig.matrix_world @ rig.pose.bones["upper_arm." + lado].head
    cot = rig.matrix_world @ rig.pose.bones["forearm." + lado].head
    pulso = rig.matrix_world @ rig.pose.bones["hand." + lado].head
    cima_d, ante_d = (cot - ombro).normalized(), (pulso - cot).normalized()
    n = cima_d.cross(ante_d).normalized()
    dentro = (-cima_d + ante_d).normalized()
    for vista, pos, cima in (("lado", cot + n * 0.42 + ante_d * 0.05, -dentro), ("fora", cot - dentro * 0.42, n)):
        cam = ag._camera("Perto", pos, alvo=cot + ante_d * 0.05, lente=50)
        cam.rotation_euler = _olhar(pos, cot + ante_d * 0.05, cima).to_euler()
        saidas += _foto(pasta, f"Pulso_4Cotovelo_{vista}", [velha, nova], velha, nova)
    return saidas


def fotos_tons(pasta, propostas):
    """As mãos novas do corpo com a conta do shader das roupas, em três tons de pele e de pelos (de frente e de perto,
    a mão direita do Parado)."""
    rig, velha, nova, fac = _cena_com_maos(ag.FBX_CORPO, RIG_CORPO, "Jogador_Corpo", SAIDA_CORPO)
    ag._render_base(640, 480)
    _pose_estatica(rig, bpy.data.actions[RIG_CORPO + "|Parado"])
    _mostrar([velha, nova, fac], [nova])
    saidas = []
    for nome, pele, pelos in TONS:
        img = _textura_do_tom(pasta, propostas["T_Corpo_Cor"], propostas["T_Corpo_Mascara"], pele, pelos)
        ag._textura_no_material("PrincipalBloco", img)
        for lado, vista in (("R", "costas"), ("L", "frente")):
            c = _ponto_da_mao(rig, lado, 0.0, 0.06, 0.0)
            dedos, costas, polegar = _eixos_da_mao(rig, lado)
            pos = c + (costas * 1.0 + Vector((0, -0.6, 0.15))).normalized() * 0.30 if vista == "costas" else \
                c + Vector((0.0, -0.32, 0.03))
            cam = ag._camera("Perto", pos, alvo=c, lente=50)
            cam.rotation_euler = _olhar(pos, c, Vector((0, 0, 1))).to_euler()
            s = os.path.join(pasta, f"Tons_{nome}_{lado}{vista}.png")
            ag._salvar(s)
            saidas.append(s)
    return saidas


ROUPAS_DAS_FOTOS = ("Luva_Tatica", "Luva_Sem_Dedos", "Camisa_Uniforme_Longa", "Jaqueta_Couro", "Moletom", "Jaleco")


def _sem_faces(ob, numeros, nome):
    """Uma cópia (na cena) sem as faces com o número (o UV "Face") na lista: o que a Vestimenta faz com o corpo."""
    c = ob.copy()
    c.data = ob.data.copy()
    c.name = nome
    bpy.context.scene.collection.objects.link(c)
    if numeros:
        uvf = c.data.uv_layers["Face"].data
        tirar = [f.index for f in c.data.polygons if int(math.floor(uvf[f.loop_indices[0]].uv.x + 1e-3)) in numeros]
        bm = bmesh.new()
        bm.from_mesh(c.data)
        bm.faces.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[bm.faces[i] for i in tirar], context="FACES")
        bm.to_mesh(c.data)
        bm.free()
    return c


def fotos_roupas(pasta, textura):
    """As roupas que vestem o antebraço e a mão (as luvas, uma camisa de manga comprida e as jaquetas) no braço esquerdo
    de hoje e no novo (o NPC_Corpo do Roupas.fbx, no repouso, sem as faces que cada peça esconde): para ver se a pele
    fura a roupa. As peças vão numa cor só (azul acinzentado)."""
    esc, _ = _esconde_do_catalogo()
    ag.nova_cena()
    novos = ag.importar(FBX_ROUPAS)
    rig = [o for o in novos if o.type == "ARMATURE"][0]
    npc = bpy.data.objects["NPC_Corpo"]
    pecas = {o.name[len("Roupa_"):]: o for o in novos if o.type == "MESH" and o.name.startswith("Roupa_")}
    novos2 = ag.importar(SAIDA_CORPO)
    rig2 = [o for o in novos2 if o.type == "ARMATURE"][0]
    maos = [o for o in novos2 if o.type == "MESH"]
    nova = npc.copy()
    nova.data = npc.data.copy()
    nova.name = "NPC_Corpo_Novo"
    bpy.context.scene.collection.objects.link(nova)
    tirar = faces_do_corte(nova, maos)
    bm = bmesh.new()
    bm.from_mesh(nova.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in tirar], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(nova.data)
    bm.free()
    for m in maos:
        m.parent = None
        for md in m.modifiers:
            if md.type == "ARMATURE":
                md.object = rig
    for o in bpy.context.scene.objects:
        o.select_set(o in [nova] + maos)
    bpy.context.view_layer.objects.active = nova
    with bpy.context.temp_override(active_object=nova, selected_editable_objects=[nova] + maos,
                                   selected_objects=[nova] + maos):
        bpy.ops.object.join()
    bpy.data.objects.remove(rig2, do_unlink=True)
    for m in bpy.data.materials:
        if m.name.startswith(("Roupa_Corpo", "PrincipalBloco")):
            ag._textura_em(m, textura)
    tecido = bpy.data.materials.new("Tecido")
    tecido.diffuse_color = (0.18, 0.24, 0.34, 1.0)
    for o in pecas.values():
        o.hide_render = True
        for i in range(len(o.material_slots)):
            o.material_slots[i].link = "OBJECT"
            o.material_slots[i].material = tecido
    npc.hide_render = nova.hide_render = True
    ag._render_base(480, 360)
    cot = rig.matrix_world @ rig.data.bones["forearm.L"].head_local
    pulso = rig.matrix_world @ rig.data.bones["hand.L"].head_local
    meio = cot.lerp(pulso, 0.6)
    vistas = [("braco_frente", meio + Vector((0, -0.62, 0.02)), meio, Vector((0, 0, 1))),
              ("braco_cima", meio + Vector((0, 0.0, 0.62)), meio, Vector((0, 1, 0))),
              ("pulso_frente", pulso + Vector((0.02, -0.30, 0.02)), pulso + Vector((0.02, 0, 0)), Vector((0, 0, 1))),
              ("pulso_cima", pulso + Vector((0.02, 0.0, 0.30)), pulso + Vector((0.02, 0, 0)), Vector((0, 1, 0)))]
    saidas = []
    for nome in ROUPAS_DAS_FOTOS:
        pc = pecas.get(nome)
        if pc is None:
            continue
        pc.hide_render = False
        corpos = {"hoje": _sem_faces(npc, esc.get(nome), "Hoje_" + nome),
                  "nova": _sem_faces(nova, esc.get(nome), "Nova_" + nome)}
        for vista, pos, alvo, cima in vistas:
            if nome.startswith("Luva") == vista.startswith("braco"):
                continue          # as luvas de perto do pulso; as mangas no braço inteiro
            cam = ag._camera("Perto", pos, alvo=alvo, lente=50)
            cam.rotation_euler = _olhar(pos, alvo, cima).to_euler()
            for qual, ob in corpos.items():
                for o2 in corpos.values():
                    o2.hide_render = o2 is not ob
                c = os.path.join(pasta, f"Roupas_{nome}_{vista}_{qual}.png")
                ag._salvar(c)
                saidas.append(c)
        for o2 in corpos.values():
            bpy.data.objects.remove(o2, do_unlink=True)
        pc.hide_render = True
    return saidas


def _tira_pintada(pasta, camada, cores):
    """A tira de uma peça (a camada da T_Roupas_Cor) pintada com as 3 cores dela pela conta do shader das roupas, numa
    imagem (para as fotos no Workbench)."""
    import numpy as np
    ims = []
    for c in (os.path.join(os.path.dirname(T_ROUPAS_MASCARA), "T_Roupas_Cor.png"), T_ROUPAS_MASCARA):
        img = bpy.data.images.load(c, check_existing=True)
        W, H = img.size
        px = np.empty(W * H * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        px = px.reshape(H, W, 4)
        t = W // 8
        col, lin = camada % 8, camada // 8
        y0 = H - (lin + 1) * t
        ims.append(px[y0:y0 + t, col * t:(col + 1) * t, :3].copy())
    base = _srgb_para_linear(ims[0])
    m = ims[1]
    cs = [_srgb_para_linear(np.array(_hex(c))) for c in cores]
    tinta = m[:, :, 0:1] * cs[0] + m[:, :, 1:2] * cs[1] + m[:, :, 2:3] * cs[2]
    alb = base * ((1.0 - m.sum(axis=2, keepdims=True)) + 2.0 * tinta)
    t = alb.shape[0]
    out = np.ones((t, t, 4), dtype=np.float32)
    out[:, :, :3] = _linear_para_srgb(alb)
    img = bpy.data.images.new(f"tira_{camada}", t, t, alpha=False)
    img.pixels.foreach_set(out.ravel())
    caminho = os.path.join(pasta, f"tira_{camada}.png")
    img.filepath_raw = caminho
    img.file_format = "PNG"
    img.save()
    return caminho


def fotos_luvas(pasta, textura):
    """As luvas de hoje (no braço de hoje, sem as faces que o catálogo esconde) e as novas (no braço novo, sem as do
    luvas_esconde.json), no homem e na mulher, com a mão aberta (o repouso) e fechada (o punho do Idle dos braços)."""
    esc, _ = _esconde_do_catalogo()
    try:
        novo_esc = json.load(open(os.path.join(pasta, "luvas_esconde.json"), encoding="utf-8"))
    except OSError:
        novo_esc = {}
    cat = json.load(open(os.path.join(os.path.dirname(FBX_ROUPAS), "Roupas.json"), encoding="utf-8"))
    dados = {p["nome"]: p for p in cat["pecas"]}
    # o punho fechado (os giros dos dedos no quadro 1 do Idle dos braços de primeira pessoa: os mesmos eixos no corpo)
    ag.nova_cena()
    ag.importar(ag.FBX_MAOS)
    punho = {k: q.copy() for k, q in ag.punho_do_fp().items()}
    ag.nova_cena()
    novos = ag.importar(FBX_ROUPAS)
    rig = [o for o in novos if o.type == "ARMATURE"][0]
    npc = bpy.data.objects["NPC_Corpo"]
    velhas = {n: bpy.data.objects["Roupa_" + n] for n in LUVAS}
    for o in novos:
        if o.type == "MESH" and o not in [npc] + list(velhas.values()):
            bpy.data.objects.remove(o, do_unlink=True)
    novas_l = ag.importar(SAIDA_LUVAS)
    novas = {}
    for o in novas_l:
        if o.type == "MESH":
            o.parent = None
            for md in o.modifiers:
                if md.type == "ARMATURE":
                    md.object = rig
            novas[o.name.split(".")[0][len("Roupa_"):]] = o
    for o in novas_l:
        if o.type == "ARMATURE":
            bpy.data.objects.remove(o, do_unlink=True)
    novos2 = ag.importar(SAIDA_CORPO)
    rig2 = [o for o in novos2 if o.type == "ARMATURE"][0]
    maos = [o for o in novos2 if o.type == "MESH"]
    nova = npc.copy()
    nova.data = npc.data.copy()
    nova.name = "NPC_Corpo_Novo"
    bpy.context.scene.collection.objects.link(nova)
    tirar = faces_do_corte(nova, maos)
    bm = bmesh.new()
    bm.from_mesh(nova.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in tirar], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(nova.data)
    bm.free()
    for m in maos:
        m.parent = None
        # (a "SemPelos" sai: o join junta as formas pelo nome, e o NPC_Corpo só tem a "Mulher")
        if m.data.shape_keys and "SemPelos" in m.data.shape_keys.key_blocks:
            m.shape_key_remove(m.data.shape_keys.key_blocks["SemPelos"])
        for md in m.modifiers:
            if md.type == "ARMATURE":
                md.object = rig
    for o in bpy.context.scene.objects:
        o.select_set(o in [nova] + maos)
    bpy.context.view_layer.objects.active = nova
    with bpy.context.temp_override(active_object=nova, selected_editable_objects=[nova] + maos,
                                   selected_objects=[nova] + maos):
        bpy.ops.object.join()
    bpy.data.objects.remove(rig2, do_unlink=True)
    for m in bpy.data.materials:
        if m.name.startswith(("Roupa_Corpo", "PrincipalBloco")):
            ag._textura_em(m, textura)
    for nome, (camada, *_r) in LUVAS.items():
        tira = _tira_pintada(pasta, camada, dados[nome]["cores"])
        for o in (velhas[nome], novas.get(nome)):
            if o is None:
                continue
            mat = bpy.data.materials.new(f"Foto_{o.name}")
            mat.use_nodes = True
            ag._textura_em(mat, tira)
            for i in range(len(o.material_slots)):
                o.material_slots[i].link = "OBJECT"
                o.material_slots[i].material = mat
    todos = [npc, nova] + list(velhas.values()) + list(novas.values())
    for o in todos:
        o.hide_render = True
    ag._render_base(480, 360)
    saidas = []
    for forma in ("Homem", "Mulher"):
        for o in todos:
            if o.data.shape_keys and "Mulher" in o.data.shape_keys.key_blocks:
                o.data.shape_keys.key_blocks["Mulher"].value = 1.0 if forma == "Mulher" else 0.0
        for pose in ("aberta", "fechada"):
            for pb in rig.pose.bones:
                pb.rotation_mode = "QUATERNION"
                pb.rotation_quaternion = (1, 0, 0, 0)
            if pose == "fechada":
                for k, q in punho.items():
                    if k.endswith(".L") and k in rig.pose.bones:
                        rig.pose.bones[k].rotation_quaternion = q
            bpy.context.view_layer.update()
            c = rig.matrix_world @ (rig.pose.bones["hand.L"].matrix @ Vector((0.0, 0.07, 0.0)))
            for nome in LUVAS:
                corpos = {"hoje": _sem_faces(npc, esc.get(nome), "Hoje_" + nome),
                          "nova": _sem_faces(nova, _faixas(novo_esc[nome]["faces"]) if nome in novo_esc else set(),
                                             "Nova_" + nome)}
                pecas = {"hoje": velhas[nome], "nova": novas.get(nome)}
                for vista, pos, cima in (("costas", c + Vector((0.02, 0.0, 0.30)), Vector((0, 1, 0))),
                                         ("palma", c + Vector((0.02, 0.0, -0.30)), Vector((0, 1, 0))),
                                         ("lado", c + Vector((0.02, -0.30, 0.04)), Vector((0, 0, 1)))):
                    cam = ag._camera("Perto", pos, alvo=c, lente=50)
                    cam.rotation_euler = _olhar(pos, c, cima).to_euler()
                    for qual in ("hoje", "nova"):
                        for o in todos + list(corpos.values()):
                            o.hide_render = True
                        corpos[qual].hide_render = False
                        if pecas[qual] is not None:
                            pecas[qual].hide_render = False
                        arq = os.path.join(pasta, f"Luvas_{nome}_{forma}_{pose}_{vista}_{qual}.png")
                        ag._salvar(arq)
                        saidas.append(arq)
                for o2 in corpos.values():
                    bpy.data.objects.remove(o2, do_unlink=True)
    return saidas


def fotos_mulher(pasta, textura):
    """A mulher sem os pelos: o NPC_Corpo com a forma "Mulher" a 100, o braço de hoje e o novo (as costas da mão e o
    antebraço), e o homem com o novo, para comparar."""
    ag.nova_cena()
    novos = ag.importar(FBX_ROUPAS)
    rig = [o for o in novos if o.type == "ARMATURE"][0]
    npc = bpy.data.objects["NPC_Corpo"]
    for o in novos:
        if o.type == "MESH" and o is not npc:
            bpy.data.objects.remove(o, do_unlink=True)
    novos2 = ag.importar(SAIDA_CORPO)
    rig2 = [o for o in novos2 if o.type == "ARMATURE"][0]
    maos = [o for o in novos2 if o.type == "MESH"]
    nova = npc.copy()
    nova.data = npc.data.copy()
    bpy.context.scene.collection.objects.link(nova)
    tirar = faces_do_corte(nova, maos)
    bm = bmesh.new()
    bm.from_mesh(nova.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in tirar], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(nova.data)
    bm.free()
    for m in maos:
        m.parent = None
        if m.data.shape_keys and "SemPelos" in m.data.shape_keys.key_blocks:
            m.shape_key_remove(m.data.shape_keys.key_blocks["SemPelos"])
        for md in m.modifiers:
            if md.type == "ARMATURE":
                md.object = rig
    for o in bpy.context.scene.objects:
        o.select_set(o in [nova] + maos)
    bpy.context.view_layer.objects.active = nova
    with bpy.context.temp_override(active_object=nova, selected_editable_objects=[nova] + maos,
                                   selected_objects=[nova] + maos):
        bpy.ops.object.join()
    bpy.data.objects.remove(rig2, do_unlink=True)
    for m in bpy.data.materials:
        if m.name.startswith(("Roupa_Corpo", "PrincipalBloco")):
            ag._textura_em(m, textura)
    ag._render_base(480, 360)
    pulso = rig.matrix_world @ rig.data.bones["hand.L"].head_local
    c = pulso + Vector((0.0, 0.0, 0.0))
    saidas = []
    for forma in ("Homem", "Mulher"):
        for o in (npc, nova):
            o.data.shape_keys.key_blocks["Mulher"].value = 1.0 if forma == "Mulher" else 0.0
        bpy.context.view_layer.update()
        for vista, pos, cima in (("cima", c + Vector((0.0, 0.0, 0.42)), Vector((0, 1, 0))),
                                 ("frente", c + Vector((0.0, -0.42, 0.04)), Vector((0, 0, 1)))):
            cam = ag._camera("Perto", pos, alvo=c, lente=50)
            cam.rotation_euler = _olhar(pos, c, cima).to_euler()
            for qual, o in (("hoje", npc), ("nova", nova)):
                npc.hide_render = o is not npc
                nova.hide_render = o is not nova
                arq = os.path.join(pasta, f"Mulher_{forma}_{vista}_{qual}.png")
                ag._salvar(arq)
                saidas.append(arq)
    return saidas


def etapa_fotos(pasta, quais=("FP", "Perto", "Poses", "Pulso", "Corpo", "Roupas", "Tons", "Mulher", "Luvas")):
    os.makedirs(pasta, exist_ok=True)
    ag.nova_cena()
    propostas = _texturas(pasta)
    tex = propostas["Personagem_PrincipalBloco"]
    if "FP" in quais:
        fotos_fp(pasta, tex)
    if "Perto" in quais:
        fotos_perto(pasta, tex)
    if "Poses" in quais:
        fotos_poses(pasta, tex)
    if "Pulso" in quais:
        fotos_pulso(pasta, tex)
    if "Corpo" in quais:
        fotos_corpo(pasta, tex)
    if "Roupas" in quais:
        fotos_roupas(pasta, tex)
    if "Mulher" in quais:
        fotos_mulher(pasta, tex)
    if "Luvas" in quais:
        fotos_luvas(pasta, tex)
    if "Tons" in quais:
        fotos_tons(pasta, propostas)


# ================================================================================================ as folhas (Pillow)


def _folha(pasta, nome, titulo, vistas, quais, por_linha=1, escala=1.0):
    """Uma folha: cada vista com as versões (quais) lado a lado, por_linha vistas em cada linha."""
    from PIL import Image, ImageDraw
    if not vistas:
        return None
    w0, h0 = Image.open(os.path.join(pasta, vistas[0][1][quais[-1]])).size
    w, h = int(w0 * escala), int(h0 * escala)
    larg = w * len(quais)
    linhas = (len(vistas) + por_linha - 1) // por_linha
    folha = Image.new("RGB", (larg * por_linha + 10 * (por_linha - 1), linhas * (h + 22) + 28), (30, 30, 30))
    d = ImageDraw.Draw(folha)
    try:                       # uma fonte com os acentos (a do Pillow não tem o "ã")
        from PIL import ImageFont
        d.font = ImageFont.truetype("arial.ttf", 15)
    except OSError:
        pass
    d.text((6, 7), titulo, fill=(255, 255, 255))
    for i, (rotulo, arqs) in enumerate(vistas):
        x0 = (i % por_linha) * (larg + 10)
        y0 = 28 + (i // por_linha) * (h + 22)
        d.text((x0 + 6, y0 + 4), rotulo, fill=(255, 220, 0))
        for j, qual in enumerate(quais):
            if qual in arqs:
                im = Image.open(os.path.join(pasta, arqs[qual])).convert("RGB")
                folha.paste(im.resize((w, h)), (x0 + j * w, y0 + 20))
                d.text((x0 + j * w + 6, y0 + 24), qual, fill=(255, 255, 255))
    c = os.path.join(pasta, f"Folha_{nome}.png")
    folha.save(c)
    return c


def folhas(pasta):
    """Junta as fotos em folhas: cada vista com a mão de hoje e a nova lado a lado (nas de perto do repouso, também a
    nova facetada); e os tons de pele."""
    import re
    vistas = {}
    for f in sorted(os.listdir(pasta)):
        m = re.match(r"(FP|Perto|Poses|Pulso|Corpo|Roupas|Mulher|Luvas)_(\w+?)_(hoje|nova)\.png$", f)
        if m:
            vistas.setdefault((m.group(1), m.group(2)), {})[m.group(3)] = f
    def do_grupo(g, filtro=lambda n: True):
        return [(n, arqs) for (gr, n), arqs in sorted(vistas.items()) if gr == g and filtro(n)]
    hn = ("hoje", "nova")
    saidas = [_folha(pasta, "FP", "Primeira pessoa (55 graus): o Idle, as mãos abertas e a direita no cabo de 3,5 cm de raio "
                     "- a mão de hoje e a nova", do_grupo("FP"), hn),
              _folha(pasta, "Perto_Esquerda", "De perto, no repouso, a mão esquerda: hoje e a nova",
                     do_grupo("Perto", lambda n: n.startswith("RepousoL")), hn, por_linha=2, escala=0.75),
              _folha(pasta, "Perto_Direita", "De perto, no repouso, a mão direita (o dorso, a palma e as unhas): hoje e a "
                     "nova", do_grupo("Perto", lambda n: n.startswith("RepousoR")), hn, por_linha=2, escala=0.75),
              _folha(pasta, "Pulso", "O pulso e o cotovelo: o antebraço no repouso, o pulso dobrado para cima e para "
                     "baixo (50 graus) com a mão no cabo e o cotovelo dobrado (o Idle) - hoje e o novo",
                     do_grupo("Pulso"), hn, por_linha=2, escala=0.75),
              _folha(pasta, "Poses_Punho_Aberta", "A mão direita fechada (o Idle) e aberta: hoje e a nova",
                     do_grupo("Poses", lambda n: not n.startswith("Cabo")), hn, por_linha=2, escala=0.75),
              _folha(pasta, "Poses_Cabo", "A prévia da pegada: a direita num cabo de 3,5 cm de raio (os dedos em volta, o "
                     "polegar por cima): hoje e a nova", do_grupo("Poses", lambda n: n.startswith("Cabo")), hn,
                     por_linha=2, escala=0.75),
              _folha(pasta, "Corpo_Maos", "O corpo parado (o Parado): as mãos de frente, de lado e de trás - hoje e a nova",
                     do_grupo("Corpo", lambda n: n.startswith("Mao")), hn, por_linha=2, escala=0.75),
              _folha(pasta, "Corpo_Braco", "O corpo parado: o braço esquerdo inteiro, de frente e de lado (o braço de "
                     "cima é o de hoje nos dois) - hoje e o novo", do_grupo("Corpo", lambda n: n.startswith("Braco")),
                     hn, por_linha=1, escala=1.0),
              _folha(pasta, "Corpo_Inteiro", "O corpo inteiro: hoje e a nova", do_grupo("Corpo", lambda n: n == "inteiro"),
                     hn),
              _folha(pasta, "Roupas", "As roupas do antebraço e da mão (em azul) no braço de hoje e no novo, sem as faces "
                     "que cada uma esconde: as luvas de perto do pulso, as mangas no braço inteiro", do_grupo("Roupas"),
                     hn, por_linha=2, escala=1.0),
              _folha(pasta, "Mulher", "A mulher (a forma Mulher a 100) e o homem: o braço de hoje e o novo (na mulher, sem "
                     "os pelos)", do_grupo("Mulher"), hn, por_linha=2, escala=1.0)]
    for luva_ in ("Luva_Tatica", "Luva_Sem_Dedos"):
        saidas.append(_folha(pasta, "Luvas_" + luva_, f"{luva_}: a de hoje (no braço de hoje) e a nova (no braço novo), "
                             "sem as faces que cada uma esconde; no homem e na mulher, a mão aberta e fechada",
                             do_grupo("Luvas", lambda n, l=luva_: n.startswith(l)), hn, por_linha=2, escala=1.0))
    tons = sorted(f for f in os.listdir(pasta) if f.startswith("Tons_") and f.endswith(".png"))
    if tons:
        por_tom = {}
        for f in tons:
            m = re.match(r"Tons_(\w+?)_([LR]\w+)\.png$", f)
            if m:
                por_tom.setdefault(m.group(2), {})[m.group(1)] = f
        nomes = [n for n, _, _ in TONS]
        rot = {n: f"{n} ({p} / pelos {q})" for n, p, q in TONS}
        saidas.append(_folha(pasta, "Tons", "Os tons pela conta do shader das roupas, com a proposta da célula das unhas: "
                             + ", ".join(rot[n] for n in nomes), sorted(por_tom.items()), nomes))
    saidas = [c for c in saidas if c]
    print("\n".join(saidas))
    return saidas


# ================================================================================================ main


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    etapa = args[0] if args else "tudo"
    pasta = args[1] if len(args) > 1 else None
    if etapa == "medir":
        print("MEDIR " + json.dumps(medir(), indent=1, default=str, ensure_ascii=False))
    if etapa in ("fp", "tudo"):
        etapa_fp()
    if etapa in ("corpo", "tudo"):
        etapa_corpo()
    if etapa in ("luvas", "tudo"):
        etapa_luvas(pasta)
    if etapa in ("conferir", "tudo"):
        print("CONFERIR " + json.dumps(conferir(pasta), indent=1, default=str, ensure_ascii=False))
    if etapa in ("fotos", "tudo") and pasta:
        etapa_fotos(pasta)
    if etapa.startswith("fotos:") and pasta:     # fotos:FP,Perto,Poses,Corpo,Tons
        etapa_fotos(pasta, etapa.split(":", 1)[1].split(","))


if __name__ == "__main__":
    if bpy is None:
        if len(sys.argv) > 2 and sys.argv[1] == "folhas":
            folhas(sys.argv[2])
        else:
            print("uso: py maos_novas.py folhas <pasta das fotos>")
    else:
        main()
