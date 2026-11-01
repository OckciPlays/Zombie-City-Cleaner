# As animações dos zumbis especiais (01/11/2026, a pedido: "faça o mecanismo dos zumbis especiais"): feitas pose a pose
# no esqueleto do Oco, como o levantar numa perna (Blender/equilibrio_oco.py, de onde vêm o esqueleto, o IK, as curvas e
# o espelho), saem num FBX só (Assets/_Game/Art/Characters/Oco/Oco_AnimacoesEspeciais.fbx) que a Unity importa como
# Humanoid e grava no esqueleto de cada zumbi (MontagemDosZumbis.AnimacoesEspeciais.cs), como os outros clipes:
#   Correr   o Corredor em disparada (laço de 20 quadros: duas passadas a 3,8 m/s; a segunda metade é o espelho da
#            primeira), curvado para a frente, os braços socando o ar com as mãos em garra
#   Emergir  o Brutamonte brotando da terra (108 quadros = 3,6 s, o TempoDeEmergir): a mão direita fura o chão e bate
#            nele, depois a esquerda; ele se puxa para fora, põe o pé direito na borda, sobe e ruge de braços abertos
#   Furia    as braçadas do Brutamonte (laço de 36 quadros = 1,2 s: duas braçadas, uma a cada 0,6 s, o
#            intervaloDaFuria), de pernas abertas, o tronco girando de um lado para o outro
#   Cuspir   o Long cuspindo (33 quadros): joga a cabeça para trás enchendo o papo e chicoteia para a frente; a gosma
#            sai no quadro 13 (0,45 s, o LancamentoDoCuspe)
#   Gritar   o Long gritando (51 quadros = 1,7 s): se encolhe, explode para trás de braços abertos e treme
#
# O mundo do Blender (o mesmo do equilibrio_oco.py): o zumbi olha para -Y; +X é a esquerda dele; Z para cima; o chão em
# Z = 0. Os giros são em graus, no mundo, relativos à pose de repouso: (inclinar para a frente, tombar para a esquerda,
# virar para a esquerda). Todos são no lugar (o agente move o zumbi).
#
# Rodar (em segundo plano):
#   blender -b --factory-startup --python Blender/especiais_animacoes.py
# ou no Blender aberto:
#   import importlib, sys; sys.path.append(r"D:\Zombiegame\Zombie-City-Cleaner\Blender")
#   import especiais_animacoes as ea; importlib.reload(ea); ea.tudo()

import math
import os
import sys

import bpy

AQUI = os.path.dirname(os.path.abspath(__file__))
if AQUI not in sys.path:
    sys.path.insert(0, AQUI)
import equilibrio_oco as eq  # noqa: E402

FBX_SAIDA = os.path.join(eq.REPO, "Assets", "_Game", "Art", "Characters", "Oco", "Oco_AnimacoesEspeciais.fbx")
CHAO = eq.CHAO
BAIXO, CIMA = (0, 0, -1), (0, 0, 1)


def coluna(sp, sp2, ch, nk, hd, r=0.0, y=0.0, rh=0.0, yh=0.0):
    return eq._coluna(sp, sp2, ch, nk, hd, r, y, rh, yh)


def espelho_de(d):
    return eq.espelhar(d)


# ------------------------------------------------------------------------------------------------ poses comuns

# de pé, na postura de zumbi (o fim do levantar, simétrico): curvado, os braços caídos para a frente
NEUTRO = {
    "hips": (0.0, 0.02, 0.92), "hips.rot": (8, 0, 0), **coluna(14, 20, 24, 10, 6),
    "shoulder.L": (6, 0, 0), "shoulder.R": (6, 0, 0),
    "mao.L": (0.25, -0.14, 0.84), "dedos.L": (-0.1, -0.5, -1), "palma.L": (-1, 0, 0), "cotovelo.L": (0.4, 1, 0),
    "mao.R": (-0.25, -0.14, 0.84), "dedos.R": (0.1, -0.5, -1), "palma.R": (1, 0, 0), "cotovelo.R": (-0.4, 1, 0),
    "pe.L": (0.12, 0.06, 0.09), "joelho.L": (0, -1, 0.1), "foot.L": (0, 0, -4),
    "pe.R": (-0.12, 0.06, 0.09), "joelho.R": (0, -1, 0.1), "foot.R": (0, 0, 4),
}


def pes_pendurados(hz, hy=0.04, abre=0.12):
    """As pernas soltas embaixo do quadril (debaixo da terra, no começo do Emergir)."""
    return {"pe.L": (abre, hy + 0.04, hz - 0.84), "joelho.L": (0, -1, 0.05), "foot.L": (20, 0, 0),
            "pe.R": (-abre, hy + 0.04, hz - 0.84), "joelho.R": (0, -1, 0.05), "foot.R": (20, 0, 0)}


# ================================================================================================ Correr (o Corredor)
#
# Meio ciclo (10 quadros): a perna esquerda pisa na frente e empurra para trás; a direita vem pelo ar. A outra metade é o
# espelho. A 3,8 m/s, o pé no chão anda 1,0 m para trás em 0,27 s (de y -0,42 a +0,56).

CORRER_MEIO = [
    # 0: o pé esquerdo pisa na frente; o direito acabou de sair do chão atrás; o braço direito na frente (a garra), o
    # esquerdo atrás; o quadril desce no impacto
    (0, {"hips": (0.015, 0.0, 0.875), "hips.rot": (16, 3, 9), **coluna(30, 34, 38, 34, 4, r=-2, y=-12, yh=8),
         "pe.L": (0.10, -0.40, 0.10), "joelho.L": (0, -1, 0.15), "foot.L": (-6, 0, 0),
         "pe.R": (-0.10, 0.52, 0.20), "joelho.R": (0, -1, -0.3), "foot.R": (52, 0, 0),
         "mao.R": (-0.20, -0.58, 1.16), "dedos.R": (0.2, -1, -0.15), "palma.R": (0.3, 0, -1), "cotovelo.R": (-1, 0.2, -0.6),
         "mao.L": (0.30, 0.16, 0.98), "dedos.L": (0.1, 0.4, -1), "palma.L": (-1, 0, 0), "cotovelo.L": (0.6, 1, 0.3),
         "shoulder.L": (14, 0, 6), "shoulder.R": (16, 0, -10)}),
    # 3: o pé esquerdo embaixo do corpo (o mais baixo); o calcanhar direito sobe atrás, perto do quadril
    (3, {"hips": (0.02, 0.0, 0.845), "hips.rot": (18, 4, 4), **coluna(31, 35, 39, 36, 6, r=-2, y=-5, yh=4),
         "pe.L": (0.10, -0.04, 0.09), "joelho.L": (0, -1, 0.25), "foot.L": (0, 0, 0),
         "pe.R": (-0.10, 0.42, 0.50), "joelho.R": (0, -0.6, -1), "foot.R": (70, 0, 0),
         "mao.R": (-0.25, -0.30, 1.04), "dedos.R": (0.1, -1, -0.5), "palma.R": (0.6, 0, -0.8), "cotovelo.R": (-1, 0.5, -0.3),
         "mao.L": (0.28, -0.04, 1.00), "dedos.L": (0, -0.3, -1), "palma.L": (-1, 0, 0), "cotovelo.L": (0.8, 1, 0.1)}),
    # 6: empurra o chão (o calcanhar esquerdo sobe); o joelho direito vem alto para a frente
    (6, {"hips": (0.01, 0.0, 0.89), "hips.rot": (17, 2, -3), **coluna(30, 34, 38, 35, 4, r=-1, y=4, yh=-2),
         "pe.L": (0.10, 0.34, 0.13), "joelho.L": (0, -1, 0.1), "foot.L": (34, 0, 0),
         "pe.R": (-0.10, -0.12, 0.42), "joelho.R": (0, -1, 0.4), "foot.R": (38, 0, 0),
         "mao.R": (-0.30, 0.05, 0.98), "dedos.R": (0, 0.2, -1), "palma.R": (1, 0, 0), "cotovelo.R": (-0.8, 1, 0.2),
         "mao.L": (0.24, -0.42, 1.10), "dedos.L": (-0.1, -1, -0.3), "palma.L": (-0.4, 0, -1), "cotovelo.L": (1, 0.3, -0.5)}),
    # 8: no ar (o mais alto): o pé esquerdo sai do chão atrás, a perna direita se estica para pisar
    (8, {"hips": (0.0, 0.0, 0.925), "hips.rot": (16, 0, -7), **coluna(29, 33, 37, 34, 3, r=0, y=10, yh=-6),
         "pe.L": (0.10, 0.50, 0.19), "joelho.L": (0, -1, -0.25), "foot.L": (50, 0, 0),
         "pe.R": (-0.10, -0.36, 0.18), "joelho.R": (0, -1, 0.2), "foot.R": (2, 0, 0),
         "mao.R": (-0.31, 0.14, 0.98), "dedos.R": (-0.1, 0.4, -1), "palma.R": (1, 0, 0), "cotovelo.R": (-0.6, 1, 0.3),
         "mao.L": (0.21, -0.55, 1.15), "dedos.L": (-0.2, -1, -0.15), "palma.L": (-0.3, 0, -1), "cotovelo.L": (1, 0.2, -0.6)}),
]


def correr():
    """O ciclo inteiro: a metade de cima e o espelho dela, 10 quadros depois."""
    return CORRER_MEIO + [(f + 10, espelho_de(d)) for f, d in CORRER_MEIO], 20


# ================================================================================================ Furia (o Brutamonte)
#
# Meio ciclo (18 quadros): a braçada da direita. Os pés plantados, abertos; o tronco gira (a virada: negativo vira para
# a direita do zumbi). A outra metade é o espelho (a braçada da esquerda).

PES_ABERTOS = {"pe.L": (0.30, 0.0, 0.09), "joelho.L": (0.5, -1, 0.1), "foot.L": (0, 0, -18),
               "pe.R": (-0.30, 0.0, 0.09), "joelho.R": (-0.5, -1, 0.1), "foot.R": (0, 0, 18)}

FURIA_MEIO = [
    # 0: armando: o tronco virado para a direita, o braço direito lá atrás e no alto; o esquerdo na frente
    (0, {"hips": (0.03, 0.02, 0.84), "hips.rot": (10, 3, -18), **coluna(18, 18, 16, 6, -6, r=-4, y=-42, rh=-4, yh=-20),
         **PES_ABERTOS,
         "mao.R": (-0.62, 0.30, 1.62), "dedos.R": (-0.3, 0.4, 1), "palma.R": (-0.2, -1, 0), "cotovelo.R": (-1, 0.5, 0.3),
         "mao.L": (0.20, -0.42, 1.10), "dedos.L": (-0.4, -1, 0), "palma.L": (-1, 0, -0.2), "cotovelo.L": (1, 0.2, -0.3),
         "shoulder.R": (4, -8, -12), "shoulder.L": (14, 0, 8)}),
    # 5: a braçada passa na frente, na altura do peito (o tronco de frente)
    (5, {"hips": (0.0, 0.0, 0.82), "hips.rot": (12, 0, -2), **coluna(22, 24, 24, 12, 4, r=0, y=-4, yh=-4),
         "mao.R": (-0.38, -0.70, 1.20), "dedos.R": (0.6, -1, 0), "palma.R": (0, 0, -1), "cotovelo.R": (-1, 0, -0.2),
         "mao.L": (0.36, -0.10, 1.04), "dedos.L": (0, -0.5, -1), "palma.L": (-1, 0, 0), "cotovelo.L": (1, 0.6, 0)}),
    # 9: o golpe: o braço direito cruza até a esquerda, baixo; o quadril afunda (o peso todo na braçada)
    (9, {"hips": (-0.03, -0.02, 0.77), "hips.rot": (16, -4, 14), **coluna(28, 30, 30, 18, 10, r=4, y=34, rh=4, yh=16),
         "mao.R": (0.32, -0.52, 0.92), "dedos.R": (1, -0.3, -0.3), "palma.R": (0, 0.2, -1), "cotovelo.R": (-0.3, 1, -0.6),
         "mao.L": (0.48, 0.20, 1.12), "dedos.L": (0.3, 0.5, -1), "palma.L": (-1, 0, 0), "cotovelo.L": (0.6, 1, 0.2),
         "shoulder.R": (16, 0, 14)}),
    # 13: o tranco de volta; o braço esquerdo já sobe para armar o outro lado
    (13, {"hips": (-0.03, 0.0, 0.81), "hips.rot": (12, -3, 18), **coluna(20, 20, 18, 8, -2, r=4, y=40, rh=4, yh=18),
          "mao.R": (0.18, -0.30, 0.80), "dedos.R": (0.4, -0.4, -1), "palma.R": (1, 0, 0), "cotovelo.R": (-0.4, 1, 0),
          "mao.L": (0.60, 0.28, 1.50), "dedos.L": (0.3, 0.4, 1), "palma.L": (0.2, -1, 0), "cotovelo.L": (1, 0.5, 0.3)}),
]


def furia():
    return FURIA_MEIO + [(f + 18, espelho_de(d)) for f, d in FURIA_MEIO], 36


# ================================================================================================ Emergir (o Brutamonte)

def _emergir():
    k = []
    # 0: inteiro debaixo da terra, de pé, os braços para cima
    hz = -1.30
    k.append((0, {"hips": (0.0, 0.06, hz), "hips.rot": (6, 0, 0), **coluna(8, 8, 6, 0, -6), **pes_pendurados(hz, 0.06),
                  "mao.R": (-0.22, -0.08, hz + 1.00), "dedos.R": (0, -0.2, 1), "palma.R": (0.3, -1, 0), "cotovelo.R": (-1, 0.3, 0),
                  "mao.L": (0.24, 0.0, hz + 0.70), "dedos.L": (0, -0.2, 1), "palma.L": (-0.3, -1, 0), "cotovelo.L": (1, 0.3, 0),
                  "shoulder.L": (0, 0, 0), "shoulder.R": (0, 0, 0)}))
    # 9: subindo por baixo (ainda invisível)
    hz = -0.82
    k.append((9, {"hips": (0.0, 0.05, hz), "hips.rot": (8, 0, 0), **pes_pendurados(hz, 0.05),
                  "mao.R": (-0.24, -0.12, hz + 0.95), "mao.L": (0.24, -0.02, hz + 0.72)}))
    # 14: a mão direita fura o chão, o braço esticado para cima, a garra aberta
    hz = -0.72
    k.append((14, {"hips": (0.0, 0.04, hz), "hips.rot": (10, -2, 0), **coluna(12, 12, 10, 4, -10, r=-3),
                   **pes_pendurados(hz, 0.04),
                   "mao.R": (-0.26, -0.22, 0.30), "dedos.R": (0.1, -0.3, 1), "palma.R": (0.2, -1, 0.2), "cotovelo.R": (-1, 0.2, 0.2),
                   "mao.L": (0.24, -0.04, hz + 0.75)}))
    # 22: a mão direita bate no chão, na frente; o corpo ainda embaixo
    hz = -0.70
    k.append((22, {"hips": (0.0, 0.03, hz), "hips.rot": (14, -3, 0), **coluna(18, 18, 16, 8, 0, r=-4),
                   **pes_pendurados(hz, 0.03),
                   "mao.R": (-0.34, -0.46, CHAO), "dedos.R": (-0.2, -1, 0), "palma.R": BAIXO, "cotovelo.R": (-1, 0.4, 0.6)}))
    # 30: a esquerda fura o chão
    hz = -0.68
    k.append((30, {"hips": (0.0, 0.03, hz), "hips.rot": (14, 3, 0), **coluna(18, 18, 16, 8, 0, r=3),
                   **pes_pendurados(hz, 0.03),
                   "mao.R": (-0.34, -0.46, CHAO), "dedos.R": (-0.2, -1, 0), "palma.R": BAIXO, "cotovelo.R": (-1, 0.4, 0.6),
                   "mao.L": (0.26, -0.20, 0.28), "dedos.L": (-0.1, -0.3, 1), "palma.L": (-0.2, -1, 0.2), "cotovelo.L": (1, 0.2, 0.2)}))
    # 38: as duas mãos plantadas; a cabeça chega à superfície
    hz = -0.62
    k.append((38, {"hips": (0.0, 0.02, hz), "hips.rot": (18, 0, 0), **coluna(22, 22, 20, 14, 6),
                   **pes_pendurados(hz, 0.02),
                   "mao.L": (0.34, -0.46, CHAO), "dedos.L": (0.2, -1, 0), "palma.L": BAIXO, "cotovelo.L": (1, 0.4, 0.6)}))
    # 50: puxa: a cabeça e os ombros saem, os cotovelos para cima e para fora
    hz = -0.34
    k.append((50, {"hips": (0.0, 0.0, hz), "hips.rot": (26, 0, 0), **coluna(34, 38, 40, 30, 18),
                   **pes_pendurados(hz, 0.0),
                   "mao.R": (-0.34, -0.46, CHAO), "cotovelo.R": (-1, 0.6, 0.8),
                   "mao.L": (0.34, -0.46, CHAO), "cotovelo.L": (1, 0.6, 0.8),
                   "shoulder.L": (20, 0, -6), "shoulder.R": (20, 0, 6)}))
    # 62: empurra: o peito passa por cima das mãos, os braços quase retos; a cabeça olha para a frente
    hz = -0.06
    k.append((62, {"hips": (0.0, -0.06, hz), "hips.rot": (44, 0, 0), **coluna(52, 56, 58, 40, 24),
                   "pe.L": (0.13, 0.10, hz - 0.70), "joelho.L": (0, -1, 0.3), "foot.L": (30, 0, 0),
                   "pe.R": (-0.13, 0.02, hz - 0.62), "joelho.R": (0, -1, 0.4), "foot.R": (30, 0, 0),
                   "mao.R": (-0.34, -0.46, CHAO), "cotovelo.R": (-1, 0.8, 0.3),
                   "mao.L": (0.34, -0.46, CHAO), "cotovelo.L": (1, 0.8, 0.3)}))
    # 74: o pé direito sai e pisa na borda do buraco, o joelho alto (como quem sai de uma piscina)
    hz = 0.22
    k.append((74, {"hips": (0.0, -0.02, hz), "hips.rot": (52, -4, 0), **coluna(60, 62, 62, 44, 28, r=-3),
                   "pe.R": (-0.16, -0.14, 0.10), "joelho.R": (-0.3, -1, 0.6), "foot.R": (0, 0, 6),
                   "pe.L": (0.13, 0.20, -0.42), "joelho.L": (0, -1, 0.1), "foot.L": (30, 0, 0),
                   "mao.R": (-0.36, -0.48, CHAO), "cotovelo.R": (-1, 0.8, 0.1),
                   "mao.L": (0.34, -0.46, CHAO), "cotovelo.L": (1, 0.8, 0.1)}))
    # 84: sobe no pé direito; as mãos saem do chão; a perna esquerda ainda no buraco
    hz = 0.56
    k.append((84, {"hips": (0.0, 0.0, hz), "hips.rot": (34, -3, 0), **coluna(40, 40, 38, 26, 14, r=-2),
                   "pe.R": (-0.16, -0.14, 0.10), "joelho.R": (-0.2, -1, 0.3),
                   "pe.L": (0.14, 0.24, -0.12), "joelho.L": (0, -1, -0.2), "foot.L": (45, 0, 0),
                   "mao.R": (-0.42, -0.36, 0.52), "dedos.R": (-0.4, -1, -0.3), "palma.R": (0.2, 0, -1), "cotovelo.R": (-1, 0.6, -0.2),
                   "mao.L": (0.42, -0.30, 0.50), "dedos.L": (0.4, -1, -0.3), "palma.L": (-0.2, 0, -1), "cotovelo.L": (1, 0.6, -0.2)}))
    # 94: o pé esquerdo plantado; de pé, os braços se abrindo
    k.append((94, {"hips": (0.0, 0.02, 0.88), "hips.rot": (10, 0, 0), **coluna(10, 8, 4, -4, -10),
                   "pe.R": (-0.16, -0.08, 0.09), "joelho.R": (-0.2, -1, 0.1), "foot.R": (0, 0, 8),
                   "pe.L": (0.16, 0.06, 0.09), "joelho.L": (0.2, -1, 0.1), "foot.L": (0, 0, -8),
                   "mao.R": (-0.64, -0.14, 1.20), "dedos.R": (-1, -0.2, 0.2), "palma.R": (0, -0.3, -1), "cotovelo.R": (-0.6, 0.6, -0.6),
                   "mao.L": (0.64, -0.14, 1.20), "dedos.L": (1, -0.2, 0.2), "palma.L": (0, -0.3, -1), "cotovelo.L": (0.6, 0.6, -0.6),
                   "shoulder.L": (0, 0, 0), "shoulder.R": (0, 0, 0)}))
    # 100: o rugido: o peito estufado, a cabeça para trás, os braços abertos no alto
    k.append((100, {"hips": (0.0, 0.03, 0.90), "hips.rot": (2, 0, 0), **coluna(-2, -8, -14, -20, -26),
                    "mao.R": (-0.70, -0.04, 1.46), "dedos.R": (-1, 0, 0.6), "palma.R": (0, -1, 0), "cotovelo.R": (-0.6, 0.5, -0.6),
                    "mao.L": (0.70, -0.04, 1.46), "dedos.L": (1, 0, 0.6), "palma.L": (0, -1, 0), "cotovelo.L": (0.6, 0.5, -0.6),
                    "shoulder.L": (-6, 0, -10), "shoulder.R": (-6, 0, 10)}))
    # 108: a postura de sempre
    k.append((108, dict(NEUTRO)))
    return k


# ================================================================================================ Cuspir e Gritar (o Long)

def _cuspir():
    return [
        (0, dict(NEUTRO)),
        # 9: joga a cabeça para trás (o papo enchendo), os ombros para trás
        (9, {"hips": (0.0, 0.06, 0.91), "hips.rot": (2, 0, 0), **coluna(2, -6, -14, -26, -32),
             "mao.L": (0.32, 0.06, 0.88), "dedos.L": (0, 0.3, -1), "palma.L": (-1, 0, 0), "cotovelo.L": (0.6, 1, 0.2),
             "mao.R": (-0.32, 0.06, 0.88), "dedos.R": (0, 0.3, -1), "palma.R": (1, 0, 0), "cotovelo.R": (-0.6, 1, 0.2),
             "shoulder.L": (-6, 0, -6), "shoulder.R": (-6, 0, 6)}),
        # 13: chicoteia para a frente: o pescoço esticado, a boca para o alvo (a gosma sai aqui)
        (13, {"hips": (0.0, -0.04, 0.90), "hips.rot": (12, 0, 0), **coluna(26, 36, 46, 46, 34),
              "mao.L": (0.30, -0.30, 0.92), "dedos.L": (0, -1, -0.6), "palma.L": (-1, 0, 0), "cotovelo.L": (0.8, 0.6, 0),
              "mao.R": (-0.30, -0.30, 0.92), "dedos.R": (0, -1, -0.6), "palma.R": (1, 0, 0), "cotovelo.R": (-0.8, 0.6, 0),
              "shoulder.L": (18, 0, 6), "shoulder.R": (18, 0, -6)}),
        # 17: o resto do tranco, a cabeça baixa
        (17, {"hips": (0.0, -0.05, 0.89), "hips.rot": (14, 0, 0), **coluna(30, 38, 48, 52, 48)}),
        # 24: voltando
        (24, {"hips": (0.0, 0.0, 0.91), "hips.rot": (10, 0, 0), **coluna(18, 24, 28, 20, 14),
              "mao.L": (0.27, -0.18, 0.86), "mao.R": (-0.27, -0.18, 0.86)}),
        (33, dict(NEUTRO)),
    ]


def _gritar():
    braco_aberto = {
        "mao.L": (0.64, 0.18, 1.22), "dedos.L": (1, 0.2, 0.3), "palma.L": (0, -1, 0), "cotovelo.L": (1, 0.5, -0.2),
        "mao.R": (-0.64, 0.18, 1.22), "dedos.R": (-1, 0.2, 0.3), "palma.R": (0, -1, 0), "cotovelo.R": (-1, 0.5, -0.2),
    }
    return [
        (0, dict(NEUTRO)),
        # 7: se encolhe (o fôlego): curvado, os braços junto do peito
        (7, {"hips": (0.0, 0.04, 0.86), "hips.rot": (16, 0, 0), **coluna(32, 38, 44, 42, 40),
             "mao.L": (0.16, -0.30, 1.02), "dedos.L": (-0.3, -0.4, 1), "palma.L": (-0.4, 1, 0), "cotovelo.L": (0.8, 0.4, -0.6),
             "mao.R": (-0.16, -0.30, 1.02), "dedos.R": (0.3, -0.4, 1), "palma.R": (0.4, 1, 0), "cotovelo.R": (-0.8, 0.4, -0.6),
             "shoulder.L": (14, 0, 10), "shoulder.R": (14, 0, -10),
             **{k: v for k, v in NEUTRO.items() if k.startswith(("pe.", "joelho.", "foot."))}}),
        # 13: explode para trás: o peito arqueado, a cabeça para trás (a boca para o céu), os braços abertos
        (13, {"hips": (0.0, 0.0, 0.94), "hips.rot": (-4, 0, 0), **coluna(-8, -16, -22, -30, -36), **braco_aberto,
              "shoulder.L": (-10, 0, -14), "shoulder.R": (-10, 0, 14)}),
        # 24 e 34: segura o grito, a cabeça varrendo de um lado para o outro (o tremor vai por cima)
        (24, {**coluna(-6, -14, -20, -26, -30, yh=14), "mao.L": (0.66, 0.12, 1.26), "mao.R": (-0.60, 0.22, 1.16)}),
        (34, {**coluna(-6, -14, -20, -28, -32, yh=-12), "mao.L": (0.60, 0.22, 1.16), "mao.R": (-0.66, 0.12, 1.26)}),
        (40, {"hips": (0.0, 0.0, 0.93), "hips.rot": (-2, 0, 0), **coluna(-4, -10, -16, -22, -26), **braco_aberto}),
        # 46: o fôlego acaba: cai para a frente
        (46, {"hips": (0.0, 0.02, 0.90), "hips.rot": (10, 0, 0), **coluna(20, 26, 30, 24, 20),
              "mao.L": (0.34, -0.10, 0.92), "dedos.L": (0, -0.4, -1), "palma.L": (-1, 0, 0), "cotovelo.L": (0.6, 1, 0),
              "mao.R": (-0.34, -0.10, 0.92), "dedos.R": (0, -0.4, -1), "palma.R": (1, 0, 0), "cotovelo.R": (-0.6, 1, 0),
              "shoulder.L": (6, 0, 0), "shoulder.R": (6, 0, 0)}),
        (51, dict(NEUTRO)),
    ]


# o tremor de cada uma: (quadro do começo, do pico, do fim, amplitude em graus)
TREMORES = {
    "Gritar": [(12, 16, 42, 3.4)],
    "Emergir": [(40, 52, 66, 1.8), (66, 74, 84, 1.6), (98, 101, 106, 2.4)],
    "Furia": [],
    "Cuspir": [(5, 9, 12, 1.2)],
    "Correr": [],
}


def tremer(canais, f, lista):
    antigo = eq.TREMOR
    eq.TREMOR = lista
    try:
        return eq.tremor(canais, f)
    finally:
        eq.TREMOR = antigo


# ================================================================================================ montagem

class Laco(eq.Linha):
    """Uma linha que dá a volta: as chaves de antes do começo e de depois do fim são as do outro lado (as tangentes do
    quadro 0 e do último saem iguais, sem tranco no laço)."""

    def __init__(self, chaves, periodo):
        ext = [(f - periodo, d) for f, d in chaves[-2:]] + list(chaves) + [(f + periodo, d) for f, d in chaves[:2]]
        super().__init__(ext)
        self.fim = periodo


def animacoes():
    """(nome, linha, último quadro, laço)."""
    c, pc = correr()
    fu, pf = furia()
    return [
        ("Correr", Laco(c, pc), pc, True),
        ("Emergir", eq.Linha(_emergir()), 108, False),
        ("Furia", Laco(fu, pf), pf, True),
        ("Cuspir", eq.Linha(_cuspir()), 33, False),
        ("Gritar", eq.Linha(_gritar()), 51, False),
    ]


def _completar(linha, base):
    """Os canais que a animação não usa ficam na pose de base (o IK precisa de todos os da mão e do pé)."""
    for canal, valor in base.items():
        if canal not in linha.curvas:
            linha.curvas[canal] = ([0.0], [([float(v)], [0.0]) for v in valor])


def construir():
    arm = eq.armadura()
    esq = eq.Esqueleto(arm)
    ad = arm.animation_data or arm.animation_data_create()
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    relatorio = {}
    lista = animacoes()
    for nome, linha, fim, _ in lista:
        _completar(linha, NEUTRO)
        acao_nome = "Zombie_" + nome
        if acao_nome in bpy.data.actions:
            bpy.data.actions.remove(bpy.data.actions[acao_nome])
        acao = bpy.data.actions.new(acao_nome)
        acao.use_fake_user = True
        ad.action = acao
        anteriores, piores = {}, {}
        for f in range(0, fim + 1):
            canais = tremer(linha.no_quadro(f), f, TREMORES.get(nome, []))
            mundo, erros = eq.posar(esq, canais)
            for k, e in erros.items():
                if e > piores.get(k, (-9, 0))[0]:
                    piores[k] = (e, f)
            eq.aplicar(esq, mundo, f, anteriores)
        relatorio[nome] = {k: (round(e, 3), f) for k, (e, f) in piores.items() if e > 0.005}
    ad.action = None
    for nome, _, fim, _ in lista:
        acao = bpy.data.actions["Zombie_" + nome]
        tr = ad.nla_tracks.new()
        tr.name = nome
        s = tr.strips.new(eq.RIG + "|Zombie_" + nome, 0, acao)
        s.name = eq.RIG + "|Zombie_" + nome
        if hasattr(s, "action_slot") and len(acao.slots):
            s.action_slot = acao.slots[0]
        tr.mute = True
    return relatorio


def exportar():
    """O FBX com a armadura, a malha e as cinco faixas."""
    cena = bpy.data.scenes[eq.CENA]
    arm, me = eq.armadura(), eq.malha()
    for m in [m for m in me.modifiers if m.type != "ARMATURE"]:
        me.modifiers.remove(m)
    ad = arm.animation_data
    ad.action = None
    for t in ad.nla_tracks:
        t.mute = False
    bpy.context.window.scene = cena
    cena.frame_set(0)
    bpy.context.view_layer.update()
    for o in list(cena.objects):
        o.select_set(False)
    arm.select_set(True)
    me.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=FBX_SAIDA, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    for t in ad.nla_tracks:
        t.mute = True
    return FBX_SAIDA


def fotos(pasta, quadros_por_animacao=None, tamanho=(360, 360)):
    """Uma foto de lado de alguns quadros de cada animação (para conferir), com o Workbench."""
    os.makedirs(pasta, exist_ok=True)
    cena = bpy.data.scenes[eq.CENA]
    arm = eq.armadura()
    cam = bpy.data.objects.get("CameraDasFotos")
    if cam is None:
        cam = bpy.data.objects.new("CameraDasFotos", bpy.data.cameras.new("CameraDasFotos"))
        cena.collection.objects.link(cam)
    cena.camera = cam
    cena.render.engine = "BLENDER_WORKBENCH"
    cena.render.resolution_x, cena.render.resolution_y = tamanho
    cena.render.film_transparent = False
    cena.display.shading.light = "STUDIO"
    cena.display.shading.color_type = "SINGLE"
    # o chão (um plano fino) para ver o que passa por ele
    if "ChaoDasFotos" not in bpy.data.objects:
        bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 0, 0))
        bpy.context.object.name = "ChaoDasFotos"
    saidas = []
    for nome, _, fim, _ in animacoes():
        arm.animation_data.action = bpy.data.actions["Zombie_" + nome]
        acao = arm.animation_data.action
        if hasattr(arm.animation_data, "action_slot") and len(acao.slots):
            arm.animation_data.action_slot = acao.slots[0]
        quadros = (quadros_por_animacao or {}).get(nome) or [round(fim * i / 5) for i in range(6)]
        for vista, (pos, rot) in {"lado": ((4.2, -0.3, 1.0), (math.radians(85), 0, math.radians(90))),
                                  "frente": ((0.4, -4.2, 1.0), (math.radians(85), 0, 0))}.items():
            cam.location = pos
            cam.rotation_euler = rot
            for f in quadros:
                cena.frame_set(f)
                cena.render.filepath = os.path.join(pasta, f"{nome}_{vista}_{f:03d}.png")
                bpy.ops.render.render(write_still=True)
                saidas.append(cena.render.filepath)
    arm.animation_data.action = None
    return saidas


def tudo(pasta_das_fotos=None):
    eq.preparar()
    rel = construir()
    if pasta_das_fotos:
        fotos(pasta_das_fotos)
    caminho = exportar()
    return rel, caminho


if __name__ == "__main__":
    pasta = None
    if "--" in sys.argv:
        resto = sys.argv[sys.argv.index("--") + 1:]
        if resto:
            pasta = resto[0]
    rel, caminho = tudo(pasta)
    for nome, piores in rel.items():
        print("ANIMACAO", nome, "longe do alvo:", piores)
    print("FBX", caminho)
