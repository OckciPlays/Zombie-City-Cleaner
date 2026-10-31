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
QUADROS_DO_GOLPE = (1, 4, 7, 10)
SEGUNDA_MAO = 0.24
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
