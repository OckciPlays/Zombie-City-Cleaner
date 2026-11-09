# O bastão improvisado (31/10/2026, a pedido: "bastão improvisado (prego + madeira)", da fabricação): um porrete de
# madeira com pregos na ponta, nas duas mãos como o machado (machado.py: o golpe e a segunda mão são os dele, com os
# números daqui).
#
# - O modelo (no espaço da arma solta: a ponta em -Y, o alto em +Z, a origem onde a mão direita segura, perto do cabo):
#   o cabo fino com a fita preta, o corpo que engrossa até a ponta e os pregos atravessados nela.
#
# Como rodar: blender -b --factory-startup --disable-autoexec --python Blender/Armas/bastao.py -- tudo
#   (as etapas: construir, exportar, imagens, fotos, bracos, conferir)

import math
import os
import sys

from mathutils import Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import faca  # noqa: E402
import machado  # noqa: E402

NOME = "Bastao"
FOTO = "bastao"
MODELO = "Bastao"
PECAS = ["corpo"]
ICONE = (512, 128)
ANGULO_LISO = 35
CORES = {
    "madeira": (12, 0),   # 7e5640
    "fita": (3, 2),       # 1b1c1f
    "prego": (4, 6),      # c4c2b8
}
METAL = {"prego"}
# como o machado (31/10/2026, a pedido: "pegado da mesma maneira, do lado direito, e o mesmo padrão de movimento, porém a
# pega dele bem embaixo"): em pé à direita da tela, um pouco mais deitado para a frente (ele é mais comprido), a mão direita
# 5 cm acima da ponta de baixo e a esquerda embaixo dela, junto do pomo; o golpe é o do machado (gira para a esquerda)
GIRO_DE_LADO = -10.0
GIRO_PARA_A_FRENTE = 72.0
GIRO_NO_EIXO = -50.0
NO_PUNHO = Vector((0.0, 0.05, 0.0))
ORTHO_CONFERIR = 1.2
SEGUNDA_MAO = -0.045
# o lugar novo na tela (08/11/2026, a pedido do Pedro, com a foto do taco de beisebol): a posição de rebater, as duas mãos
# juntas na ponta do cabo (a esquerda embaixo, encostada no pomo, e a direita logo acima, encostando nela), embaixo à
# direita da tela (uns 70% da largura), e o bastão subindo inclinado para trás e para a direita, saindo pelo canto de cima
# à direita (os pregos ficam fora da tela) (duas_maos.py)
MAO_ESQUERDA = 0.012       # (o mínimo longe do pomo)
MAO_DIREITA = -0.110       # (12 cm acima da esquerda: as mãos encostadas sem uma entrar na outra)
# (09/11/2026, a pedido) a mira menos para trás (antes, (1,6; 3,5)): a parte grossa, com os pregos, aparece em cima à direita
IDLE_FP = {"ponto": (0.012, 0.42, -0.78, 0.42), "mira": (-0.70, 1.0, 1.25), "alto": (0.0, 1.0, 0.0)}
# o golpe: carrega mais para trás e para a direita (a preparação), bate na horizontal, da direita para a esquerda, na
# altura da mira, com os braços esticados e o bastão atravessando o meio da tela (o acerto no quadro 8, 7/24 s), acompanha
# até a esquerda e volta à posição de rebater. O bastão gira em volta do cabo o que deixar os pulsos melhores (redondo,
# não aparece: ROLAR_GOLPE)
GOLPE_FP = [
    (1, {}),
    (4, {"ponto": (0.012, 0.55, -0.74, 0.40), "mira": (-0.70, 2.5, 4.0), "alto": (0.0, 1.0, 0.0)}),
    (6, {"ponto": (0.012, 0.48, -0.60, 0.42), "mira": (-0.70, 2.2, 0.5), "alto": (0.0, 0.0, 1.0)}),
    # (09/11/2026) no quadro 7 a ponta sobe (antes, a mira em (0,25; 0,0) deixava o bastão de ponta para a câmera, e a
    # torção dos antebraços saltava uns 150 graus de um quadro para o outro; com a ponta baixa, ainda mais): o bastão
    # passa por cima, de pé, e os antebraços giram nos dois quadros (o 6 -> 8 é um giro de 143 graus em dois quadros)
    (7, {"ponto": (0.012, 0.30, -0.55, 0.45), "mira": (-0.70, 0.5, 1.0), "alto": (0.0, 0.0, 1.0)}),
    (8, {"ponto": (0.012, 0.12, -0.55, 0.45), "mira": (-0.70, -0.95, 0.05), "alto": (0.0, 0.0, 1.0)}),
    # (09/11/2026) o acompanhamento: o bastão continua varrendo para a esquerda, a ponta na borda esquerda e perto da tela
    # (antes, a mira em -3,0 deixava o bastão de ponta, como um toco)
    (10, {"ponto": (0.012, -0.12, -0.60, 0.44), "mira": (-0.70, -1.3, 0.15), "alto": (0.0, 0.0, 1.0)}),
    (11, {"ponto": (0.012, -0.32, -0.68, 0.42), "mira": (-0.70, -1.6, 0.3), "alto": (0.0, 0.0, 1.0), "perto": False}),
    (15, {}),
]
ROLAR_GOLPE = True
LIMITE_ULNAR_GOLPE = 25.0
# (09/11/2026) a diagonal do cabo na palma (graus): a melhor para o pulso destas, mão a mão (duas_maos.refazer; a
# escolhida fica em diagonais_fp.json)
DIAGONAIS = {"R": (16, 20, 25, 30, 35), "L": (16, 20, 25, 30, 35)}
QUADROS_DO_GOLPE = (1, 4, 6, 8, 11, 13)


def modelo(m):
    """O porrete: o pomo, o cabo com a fita, o corpo que engrossa e os pregos na ponta."""
    m.tubo("y", (0.0, 0.072, 0.0), 0.024, 0.024, 0.018, "madeira", lados=10)
    m.tubo("y", (0.0, -0.06, 0.0), 0.0165, 0.0175, 0.24, "fita", lados=10)
    m.tubo("y", (0.0, -0.29, 0.0), 0.0175, 0.032, 0.22, "madeira", lados=10)
    m.tubo("y", (0.0, -0.54, 0.0), 0.032, 0.037, 0.28, "madeira", lados=10)
    m.tubo("y", (0.0, -0.69, 0.0), 0.037, 0.030, 0.02, "madeira", lados=10)
    # os pregos: atravessados, saindo dos dois lados em 3 alturas
    for k, y in enumerate((-0.50, -0.57, -0.64)):
        a = math.radians(30 * k)
        for s in (1, -1):
            d = Vector((math.cos(a), 0.0, math.sin(a))) * s
            c = d * 0.045
            m.caixa((c.x - 0.003, y - 0.003, c.z - 0.003), (c.x + 0.003, y + 0.003, c.z + 0.003), "prego")
        m.tubo("x" if k % 2 == 0 else "z", (0.0, y, 0.0), 0.0022, 0.0022, 0.10, "prego", lados=4)


def golpe(rig, idle, prefixo):
    return machado.golpe(rig, idle, prefixo)


def segunda_mao(rig, clipes, P, idle):
    machado.SEGUNDA_MAO = SEGUNDA_MAO
    machado.segunda_mao(rig, clipes, P, idle)


if __name__ == "__main__" and "--" in sys.argv:
    faca.rodar(sys.modules[__name__], sys.argv[sys.argv.index("--") + 1:] or ["tudo"])
