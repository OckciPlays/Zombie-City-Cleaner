# A sniper nova (29/10/2026): a partir da Mosin-Nagant que o Pedro mandou (Fontes/mosin-nagant.blend), um fuzil de
# ferrolho modernizado: o cano comprido, o receptor e o ferrolho grafite, a coronha de polímero preto (era de nogueira)
# com uma empunhadura de pistola pontilhada (o apoio da mão direita: as animações seguram um cabo, e a Mosin tem o punho
# reto), a caixa do carregador e, modelada aqui, a luneta: o corpo, a campânula da objetiva e a ocular, a torre de
# elevação em cima e a de deriva do lado direito, o anel de aumento, a ocular de borracha, as lentes, os dois anéis com
# os parafusos e o trilho à frente do receptor (uma luneta "scout").
#
# O encaixe na antiga (a do Armas_Sniper.fbx): a boca no mesmo ponto (y -0,905, z 0,115) e a escala 0,096 (o tamanho real),
# com que o gatilho fica logo à frente da mão direita, na empunhadura nova, e a esquerda segura a coronha logo à frente da
# caixa do carregador. A luneta fica como a antiga: baixa e para a frente (a ocular em y -0,09, a objetiva em -0,43).
# As peças: o corpo ("gun"), o ferrolho ("slide") e o carregador ("mag": a caixa embaixo da coronha).
#
# Limite: na animação do tiro, a mão direita vai ao ferrolho onde ele estava na sniper antiga (atrás da mão); na Mosin a
# alavanca fica à frente do gatilho, e a mão puxa uns 20 cm atrás dela. Acertar isso pede uma animação nova.

from mathutils import Matrix, Vector

NOME = "Sniper"
MODELO = "Sniper"
FBX_ANTIGO = "Armas_Sniper.fbx"
FONTE = "mosin-nagant.blend"
OBJETOS = {
    "mosin9130 barrel": "corpo",
    "mosin9130 receiver": "corpo",
    "mosin9130 stock": "corpo",
    "mosin9130 handguard upper": "corpo",
    "mosin9130 trigga": "corpo",
    "mosin9130 bolt body": "ferrolho",
    "mosin9130 bolt head": "ferrolho",
}
PECAS = ["corpo", "ferrolho", "carregador"]
TIRAR_ESCONDIDAS = True
DISSOLVER = 1.0

CHANFRO = 0.0009
TAMANHO = 2048
MARGEM_UV = 0.003
DISTANCIA_AO = 0.02
AO_NA_COR = 0.45
ICONE = (896, 256)      # o tamanho do ícone do inventário (o de antes)

ESCALA = 0.096
LARGURA = 1.0
BOCA_FONTE = Vector((6.2, 0.0, 0.513))      # o centro da boca na Mosin
BOCA = Vector((0.0, -0.905, 0.115))         # a boca da antiga (Armas.cs: (0, 0,115, 0,905))
# a caixa do carregador (do objeto do receptor, na Mosin): embaixo da coronha, entre o guarda-mato e a frente dela
CAIXA_DO_CARREGADOR = (-2.16, -0.62, 0.02)  # x de, x até, z abaixo de


def transformacao():
    """Da Mosin (a boca em +X, a esquerda em +Y, o alto em +Z) para o espaço da arma (a boca em -Y, a esquerda em +X)."""
    s = ESCALA
    G = Matrix(((0, s * LARGURA, 0, 0), (-s, 0, 0, 0), (0, 0, s, 0), (0, 0, 0, 1)))
    return Matrix.Translation(BOCA - G @ BOCA_FONTE) @ G


def peca_da_face(obj, c):
    """A caixa do carregador sai do receptor (a peça "carregador"); o resto fica na peça do objeto."""
    x0, x1, z = CAIXA_DO_CARREGADOR
    if obj == "mosin9130 receiver" and x0 < c.x < x1 and c.z < z:
        return "carregador"
    return None


def superficie(obj, mat, c, n):
    """Os materiais da Mosin: h-190 (o preto), stell (o aço), wood (a nogueira: aqui, polímero preto), copper."""
    if mat == "wood (walnut)":
        return "polimero"
    if obj in ("mosin9130 bolt body", "mosin9130 bolt head"):
        return "grafite" if mat == "stell" else "pintado"
    if obj in ("mosin9130 barrel", "mosin9130 receiver"):
        x0, x1, z = CAIXA_DO_CARREGADOR
        if obj == "mosin9130 receiver" and x0 < c.x < x1 and c.z < z:
            return "pintado"
        return "exposto" if mat == "stell" else "grafite"
    return "pintado"


# ------------------------------------------------------------------------------------------------ a luneta e a empunhadura
# A luneta "scout" (de olho longe): o ferrolho corre por cima do receptor inteiro (até y -0,22), então o trilho vai à
# frente dele, sobre a base da alça de mira (que fica dentro do trilho), e a luneta passa por cima do ferrolho sem
# nenhum suporte no caminho. Baixa e para a frente, como a antiga, para não tapar a tela.
EIXO_Z = 0.172
R_TUBO = 0.0115


def extras(pecas):
    m = pecas["corpo"]
    # o trilho (com as ranhuras) e os dois anéis com a base e os parafusos
    m.caixa((-0.0110, -0.380, 0.1235), (0.0110, -0.235, 0.1360), "pintado", chanfro=0.0008)
    for k in range(7):
        y = -0.250 - k * 0.020
        m.caixa((-0.0113, y - 0.0035, 0.1340), (0.0113, y + 0.0035, 0.1400), "pintado", chanfro=0.0004)
    for y in (-0.262, -0.345):
        m.caixa((-0.0110, y - 0.0075, 0.1380), (0.0110, y + 0.0075, 0.1590), "pintado", chanfro=0.0008)
        m.tubo("y", (0.0, y, EIXO_Z), R_TUBO + 0.0040, R_TUBO + 0.0040, 0.012, "pintado", lados=24, chanfro=0.0006)
        for lado in (-1, 1):
            m.tubo("x", (lado * 0.0123, y, 0.1470), 0.0020, 0.0020, 0.004, "exposto", lados=10)
            m.tubo("x", (lado * 0.0150, y, EIXO_Z + 0.0035), 0.0017, 0.0017, 0.004, "exposto", lados=10)
    # o corpo: a ocular (com a borracha e o anel de aumento), o tubo, a caixa das torres e a objetiva com o para-sol
    m.tubo("y", (0.0, -0.0950, EIXO_Z), 0.0172, 0.0172, 0.010, "borracha", lados=24, chanfro=0.0010)
    m.tubo("y", (0.0, -0.1150, EIXO_Z), 0.0165, 0.0165, 0.030, "grafite", lados=24, chanfro=0.0005)
    m.tubo("y", (0.0, -0.1180, EIXO_Z), 0.0170, 0.0170, 0.012, "texturizado", lados=24)
    m.tubo("y", (0.0, -0.1400, EIXO_Z), 0.0165, R_TUBO, 0.020, "grafite", lados=24)       # r0 atrás, r1 na frente
    m.tubo("y", (0.0, -0.2650, EIXO_Z), R_TUBO, R_TUBO, 0.230, "grafite", lados=24)
    m.tubo("y", (0.0, -0.3030, EIXO_Z), 0.0135, 0.0135, 0.032, "grafite", lados=24, chanfro=0.0006)
    m.tubo("y", (0.0, -0.3925, EIXO_Z), R_TUBO, 0.0168, 0.025, "grafite", lados=24)
    m.tubo("y", (0.0, -0.4175, EIXO_Z), 0.0168, 0.0168, 0.025, "grafite", lados=24, chanfro=0.0006)
    m.tubo("y", (0.0, -0.4315, EIXO_Z), 0.0174, 0.0174, 0.003, "pintado", lados=24)
    # as lentes (um pouco para dentro das bordas)
    m.tubo("y", (0.0, -0.4285, EIXO_Z), 0.0152, 0.0152, 0.0012, "lente", lados=24)
    m.tubo("y", (0.0, -0.0905, EIXO_Z), 0.0135, 0.0135, 0.0012, "lente", lados=24)
    # as torres: a elevação em cima e a deriva à direita (-X), com as tampas pontilhadas; a do foco à esquerda
    m.tubo("z", (0.0, -0.303, EIXO_Z + 0.0170), 0.0085, 0.0085, 0.012, "grafite", lados=20, chanfro=0.0005)
    m.tubo("z", (0.0, -0.303, EIXO_Z + 0.0265), 0.0095, 0.0095, 0.008, "texturizado", lados=20, chanfro=0.0006)
    m.tubo("x", (-0.0185, -0.303, EIXO_Z), 0.0085, 0.0085, 0.010, "grafite", lados=20, chanfro=0.0005)
    m.tubo("x", (-0.0270, -0.303, EIXO_Z), 0.0095, 0.0095, 0.007, "texturizado", lados=20, chanfro=0.0006)
    m.tubo("x", (0.0180, -0.303, EIXO_Z), 0.0072, 0.0072, 0.009, "grafite", lados=20, chanfro=0.0005)
    # a empunhadura de pistola: do fundo da coronha (z ~0,09, logo atrás do guarda-mato) até embaixo da mão, inclinada
    perfil = [(-0.046, 0.092), (-0.039, 0.055), (-0.033, 0.012), (-0.029, -0.030), (-0.025, -0.057), (-0.013, -0.066),
              (0.012, -0.066), (0.022, -0.058), (0.019, -0.020), (0.022, 0.030), (0.030, 0.060), (0.034, 0.084)]
    m.perfil_x(perfil, -0.0150, 0.0150, "texturizado", chanfro=0.0030, seg=2)
    m.caixa((-0.0158, -0.019, -0.071), (0.0158, 0.023, -0.064), "polimero", chanfro=0.0015)
