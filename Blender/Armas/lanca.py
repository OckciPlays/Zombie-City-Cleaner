# A lança improvisada (31/10/2026, da fabricação: "mais algumas coisas para sobrevivência"): um cabo de madeira com uma
# lâmina presa na ponta com fita, nas duas mãos (a esquerda à frente). O golpe é a estocada para a frente (o golpe do
# machado.py com as chaves daqui).
#
# - O modelo (no espaço da arma solta: a ponta em -Y, o alto em +Z, a origem onde a mão direita segura, no terço de trás).
#
# Como rodar: blender -b --factory-startup --disable-autoexec --python Blender/Armas/lanca.py -- tudo

import os
import sys

from mathutils import Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import faca  # noqa: E402
import machado  # noqa: E402

NOME = "Lanca"
FOTO = "lanca"
MODELO = "Lanca"
PECAS = ["corpo"]
ICONE = (768, 128)
ANGULO_LISO = 35
CORES = {
    "madeira": (1, 4),    # b09d6e
    "fita": (8, 2),       # 9a9c98
    "lamina": (4, 6),     # c4c2b8
}
METAL = {"lamina"}
# apontando para a frente (um pouco para cima), quase reta na câmera: a estocada vai na direção do olhar
GIRO_DE_LADO = 20.0
GIRO_PARA_A_FRENTE = 112.0
GIRO_NO_EIXO = 0.0
NO_PUNHO = Vector((0.0, 0.0, 0.0))
ORTHO_CONFERIR = 1.6
QUADROS_DO_GOLPE = (1, 4, 7, 10, 13)
SEGUNDA_MAO = 0.24
# o lugar novo na tela (08/11/2026, a pedido do Pedro, com as referências dele: a "Segura Inferior" e o guerreiro de
# lança): a lança deitada para a frente, apontando para onde se mira (a ponta perto do meio da tela, um pouco abaixo), a
# mão direita atrás, perto da ponta de baixo, e a esquerda bem à frente, as duas por cima do cabo (duas_maos.py)
MAO_DIREITA = 0.14        # o y da arma: a 17 cm da ponta de baixo
MAO_ESQUERDA = -0.13      # 27 cm à frente da direita (três mãos de folga entre os punhos)
# (09/11/2026, a pedido: "a ponta dela não condiz com a mira do jogador") a lança aponta para a mira: o cabo vai reto
# para a frente, na direção de um ponto do meio da tela a uns 3 m (a reta do cabo, prolongada, passa pelo meio da tela), e
# a ponta fica logo abaixo da cruz (uns 6% da altura da tela), sem cobri-la; a mão direita mais embaixo e mais perto do
# meio que antes (antes: a mão em (0,72; -0,80) e a ponta em (-0,20; -0,30), cruzando para a esquerda)
IDLE_FP = {"ponto": (0.14, 0.45, -1.0, 0.34), "mira": (-1.13, 0.05, -0.11), "alto": (0.0, 0.0, 1.0)}
# (08/11/2026, a pedido: "os braços longe da câmera") a mão direita a 40 cm da câmera (antes, 25), e a esquerda mais perto
# dela no cabo, para o braço esquerdo alcançar
COSTAS = {}
LIMITE_ULNAR_GOLPE = 25.0
# (09/11/2026) a diagonal do cabo na palma (graus): a melhor para o pulso destas, mão a mão (duas_maos.refazer; a
# escolhida fica em diagonais_fp.json)
DIAGONAIS = {"R": (35, 40, 45), "L": (35, 40, 45)}
# (11/11/2026, o Pedro: "punho torto e irregular") os pulsos nos limites (a dobra até uns 60 graus, o desvio até 25, a
# torção até 90): o ombro direito 10 cm mais baixo e 4 cm à frente (fora da tela: o antebraço chega à pegada de baixo, e
# o pulso direito deixa de torcer 100 graus), o cabo mais na diagonal das palmas (as duas a 45 graus: a esquerda sai do
# desvio de 33) e a torção pesando mais cedo na escolha do giro e do cotovelo
OMBRO_FP = {"R": (-0.03, -0.04, -0.10)}
LIMITE_TORCAO = 80.0
# a estocada: recua (a preparação), empurra ao longo do cabo com as duas mãos (o acerto no quadro 7, 6/24 s) e volta
# (09/11/2026) no acerto, a ponta passa pela mira: as mãos vão uns 16 cm para a frente e a ponta sobe até o meio da tela
GOLPE_FP = [
    (1, {}),
    (4, {"avanco": -0.06, "desloca": (0.0, 0.0, 0.010)}),
    (7, {"avanco": 0.18, "desloca": (0.064, 0.0, 0.080)}),
    (10, {"avanco": 0.12}),
    (15, {}),
]
# a estocada: recolhe, empurra para a frente e volta
CHAVES_DO_GOLPE = [
    (1, (0.0, 0.0, 0.0), 0.0, 0.0),
    (4, (0.0, 0.10, 0.02), 0.0, -4.0),
    (7, (0.0, -0.30, 0.03), 0.0, 4.0),
    (11, (0.0, -0.12, 0.01), 0.0, 2.0),
    (15, (0.0, 0.0, 0.0), 0.0, 0.0),
]


def modelo(m):
    """O cabo comprido, a fita e a lâmina em losango na ponta."""
    m.tubo("y", (0.0, -0.32, 0.0), 0.016, 0.015, 1.26, "madeira", lados=8)
    m.tubo("y", (0.0, -0.93, 0.0), 0.0195, 0.0195, 0.08, "fita", lados=8)
    m.perfil_x([(-0.94, 0.010), (-1.00, 0.020), (-1.13, 0.0), (-1.00, -0.020), (-0.94, -0.010)], -0.0032, 0.0032, "lamina",
               chanfro=0.0006)


def golpe(rig, idle, prefixo):
    machado.CHAVES_DO_GOLPE = CHAVES_DO_GOLPE
    return machado.golpe(rig, idle, prefixo)


def segunda_mao(rig, clipes, P, idle):
    machado.SEGUNDA_MAO = SEGUNDA_MAO
    machado.segunda_mao(rig, clipes, P, idle)


if __name__ == "__main__" and "--" in sys.argv:
    faca.rodar(sys.modules[__name__], sys.argv[sys.argv.index("--") + 1:] or ["tudo"])
