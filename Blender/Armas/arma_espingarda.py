# A espingarda calibre 12 nova (29/10/2026): a partir da Remington 870 que o Pedro mandou (Fontes/remington_model_870.blend):
# a de bomba, com o cano comprido de faixa ventilada e a mira de conta, o receptor grafite, o carregador tubular embaixo do
# cano, a telha e a coronha de polímero preto (eram de nogueira) com a soleira de borracha, o gatilho, a trava e o
# elevador, e o ferrolho de aço aparecendo na janela de ejeção (ele anda com a telha, como na de verdade).
#
# O encaixe na antiga (a do Armas.fbx): a boca no mesmo ponto (y -0,892, z 0,056) e a escala 0,095 (o tamanho real: a
# antiga também era), com que o gatilho fica logo à frente da mão direita e a telha cai na esquerda. As peças são as da
# antiga: o corpo ("gun"), a telha ("pump": a telha e o ferrolho) e o cartucho da recarga ("shell": um cartucho de
# plástico vermelho-escuro e base de latão, no lugar do antigo, embaixo do receptor).

import math

from mathutils import Matrix, Vector

NOME = "Espingarda"
MODELO = "Espingarda"
FBX_ANTIGO = "Armas.fbx"
FONTE = "remington_model_870.blend"
OBJETOS = {
    "rem870 27in barrel ": "corpo",
    "rem870 receiver": "corpo",
    "rem870 carrier": "corpo",
    "rem870 safety": "corpo",
    "rem870 trigga": "corpo",
    "rem870 stock": "corpo",
    "rem870 pump forend": "telha",
    "rem870 bolt": "telha",
    "12x70 n1": "cartucho",
}
PECAS = ["corpo", "telha", "cartucho"]
TIRAR_ESCONDIDAS = True
DISSOLVER = 1.0

CHANFRO = 0.0009
TAMANHO = 2048
MARGEM_UV = 0.003
DISTANCIA_AO = 0.02
AO_NA_COR = 0.45
ICONE = (768, 128)      # o tamanho do ícone do inventário (o de antes)

ESCALA = 0.095
LARGURA = 1.0
BOCA_FONTE = Vector((6.015, 0.0, 0.19))     # o centro da boca do cano na 870
BOCA = Vector((0.0, -0.892, 0.056))         # a boca da antiga (Armas.cs: (0, 0,056, 0,892))
# o cartucho: no lugar do antigo (o miolo dele no espaço da arma), deitado com a ponta para a frente, 70 x 20 mm
CARTUCHO = Vector((-0.001, -0.171, 0.035))
CARTUCHO_COMPRIMENTO = 0.070
CARTUCHO_DIAMETRO = 0.020
# a caixa do cartucho "12x70 n1" na fonte (de pé: a base de latão embaixo, -Z)
CARTUCHO_FONTE = (Vector((0.7603, -0.1011, -1.2181)), Vector((0.9624, 0.1011, -0.6981)))


def transformacao():
    """Da 870 (a boca em +X, a esquerda em +Y, o alto em +Z) para o espaço da arma (a boca em -Y, a esquerda em +X)."""
    s = ESCALA
    G = Matrix(((0, s * LARGURA, 0, 0), (-s, 0, 0, 0), (0, 0, s, 0), (0, 0, 0, 1)))
    return Matrix.Translation(BOCA - G @ BOCA_FONTE) @ G


def transformacao_do(nome):
    if nome != "12x70 n1":
        return transformacao()
    mn, mx = CARTUCHO_FONTE
    c = (mn + mx) / 2
    escala = Matrix.Diagonal((CARTUCHO_DIAMETRO / (mx.x - mn.x), CARTUCHO_DIAMETRO / (mx.y - mn.y),
                              CARTUCHO_COMPRIMENTO / (mx.z - mn.z), 1.0))
    deitar = Matrix.Rotation(math.radians(90), 4, "X")   # +Z (a ponta) vai para -Y (a frente)
    return Matrix.Translation(CARTUCHO) @ deitar @ escala @ Matrix.Translation(-c)


def superficie(obj, mat, c, n):
    """Os materiais da 870: h-190 (o preto), stell (o aço), wood (a nogueira: aqui, polímero preto), resin (a soleira),
    brass e black (o cartucho)."""
    if obj == "12x70 n1":
        return "latao" if mat == "brass" else "cartucho"
    if mat == "wood (walnut)":
        return "polimero"
    if mat in ("resin", "black"):
        return "borracha"
    if obj == "rem870 27in barrel ":
        return "grafite"
    if obj == "rem870 bolt":
        return "exposto"
    if obj == "rem870 receiver":
        return "exposto" if mat == "stell" else "grafite"
    return "pintado"
