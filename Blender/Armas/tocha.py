# A tocha (31/10/2026, a pedido: "fazer tocha", da fabricação): um pau com pano enrolado na ponta, na mão direita como a
# faca (faca.py; o golpe é o soco direito). A chama e a luz são da Unity, na ponta: a boca dos braços vai para lá (PONTA).
#
# - O modelo (no espaço da arma solta: a ponta em -Y, o alto em +Z, a origem onde a mão segura).
#
# Como rodar: blender -b --factory-startup --disable-autoexec --python Blender/Armas/tocha.py -- tudo

import os
import sys

from mathutils import Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import faca  # noqa: E402

NOME = "Tocha"
FOTO = "tocha"
MODELO = "Tocha"
PECAS = ["corpo"]
ICONE = (384, 128)
ANGULO_LISO = 35
CORES = {
    "madeira": (12, 0),   # 7e5640
    "pano": (3, 9),       # d2cbb2
    "queimado": (3, 2),   # 1b1c1f
    "amarra": (1, 6),     # c2a965
}
METAL = set()
# em pé na mão, um pouco para a frente (a chama em cima, à direita da tela)
GIRO_PARA_A_FRENTE = 78.0
GIRO_NO_EIXO = 0.0
NO_PUNHO = Vector((0.0, 0.03, 0.0))
LIMITE_TORCAO = 90.0
# (09/11/2026) o lugar da arma no punho com um ajuste pequeno, buscado pelo custo do braço (duas_maos.py: até 10 graus
# de inclinação e 3 cm), para o pulso ficar natural com a pegada canônica
AJUSTE_FP_LIMITE = (10.0, 0.03)
# (09/11/2026) com até 10 graus o pulso não cabia (33/46/98); o menor ajuste que coube (busca até 45 graus e 4 cm):
# a tocha inclina -35 (X) e -27,5 (Z) graus e anda (3; -1,5; -1,5) cm. Aplicado de noite para o Pedro ver de manhã
# (o antes ficou em maos_fotos\tocha_antes_do_ajuste); para voltar, apague a linha de baixo
AJUSTE_FP = (-35.0, -27.5, 0.03, -0.015, -0.015)
# (09/11/2026) a diagonal do cabo na palma (graus): a melhor para o pulso destas, mão a mão (duas_maos.refazer; a
# escolhida fica em diagonais_fp.json)
DIAGONAIS = {"R": (16, 20, 25, 30, 35), "L": (16, 20, 25, 30, 35)}
ORTHO_CONFERIR = 0.8
# a ponta (onde a chama nasce), no espaço da arma
PONTA = Vector((0.0, -0.40, 0.0))


def modelo(m):
    """O pau e o pano enrolado na ponta, queimado em cima, com a amarra."""
    m.tubo("y", (0.0, -0.13, 0.0), 0.016, 0.014, 0.40, "madeira", lados=8)
    m.tubo("y", (0.0, -0.355, 0.0), 0.029, 0.032, 0.09, "pano", lados=8)
    m.tubo("y", (0.0, -0.405, 0.0), 0.032, 0.026, 0.015, "queimado", lados=8)
    m.tubo("y", (0.0, -0.312, 0.0), 0.031, 0.031, 0.008, "amarra", lados=8)


if __name__ == "__main__" and "--" in sys.argv:
    faca.rodar(sys.modules[__name__], sys.argv[sys.argv.index("--") + 1:] or ["tudo"])
