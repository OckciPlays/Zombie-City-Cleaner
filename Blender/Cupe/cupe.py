# O cupê (29/10/2026): o segundo carro que se dirige, um cupê fastback dos anos 70 (inspirado nas fotos do usuário),
# preto com as faixas cinza no capô e dos lados, 2 portas compridas, faróis redondos, grade preta de lado a lado,
# para-choques cromados finos com as garras pretas, rodas de aço pretas com pneus de letras brancas. Interior de vinil
# cinza-escuro.
#
# Mesmo caminho da perua v2: um modelo "alto" (detalhado) e um "leve" (~10 mil triângulos), e a cor, o relevo, a
# oclusão, o metal e a aspereza do alto projetados no leve. As peças seguem o formato da perua (Cupe_Carroceria,
# Cupe_Porta_E/D, Cupe_Roda, Cupe_Volante, Cupe_Capo, Cupe_Tampa). O chassi é o da perua: o entre-eixos (2,72 m), a
# bitola e as rodas (raio 0,30, largura 0,21) são os mesmos, e o lugar do motorista (o volante, o painel e a ignição) é
# o da perua 8 cm mais baixo: os pontos fixos do código do carro (o macaco, o capô, o porta-malas, o olho) continuam
# valendo. A montagem da Unity é a Editor/Cupe/MontagemDoCupe.cs (as medidas de lá têm de bater com estas).
#
# Roda num Blender em segundo plano (Blender/Cupe/rodar.py): construir, assar <canal>, compor_exportar.

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
PASTA_BLENDER = os.path.dirname(AQUI)
for p in (PASTA_BLENDER, os.path.join(PASTA_BLENDER, "PeruaNova"), os.path.join(PASTA_BLENDER, "PeruaNova2")):
    if p not in sys.path:
        sys.path.append(p)
import perua_nova as v1   # noqa: E402  (a Malha, os materiais de autoria, o painel, os bancos, o volante)
import perua_nova2 as v2  # noqa: E402  (a MalhaLeve e as peças leves)

REPO = os.path.dirname(PASTA_BLENDER)
DESTINO = os.path.join(REPO, "Assets", "_Game", "Art", "Veiculos", "Cupe")
TEXTURAS = os.path.join(DESTINO, "Texturas")
BLEND = os.path.join(AQUI, "Cupe.blend")
TAMANHO = 2048

# ---------------------------------------------------------------------------------------------------- medidas (Unity)
W = 0.89                 # meia largura, por fora
EIXO, R_RODA, X_RODA, R_ARCO = 1.36, 0.30, 0.735, 0.37
Y_BASE = 0.22            # embaixo da carroceria (Carro.BaseDaLataria)
Y_CINTURA = 0.90         # a linha das janelas
Y_TETO = 1.36
Z_PORTA = 0.92           # a dobradiça da porta (comprida: vai até Z_FIM_PORTA)
Z_FIM_PORTA = -0.30
PARABRISA = ((1.02, Y_CINTURA), (0.30, 1.33))
Z_FIM_TETO = -0.80       # onde o teto reto acaba e começa a queda do fastback
VIDRO_TRAS = ((-1.74, 0.975), (-0.82, 1.345))   # (z, y) de baixo e de cima
X_CAPO, Z_CAPO = 0.62, 1.145
Y_COFRE = 0.45           # o fundo do cofre (3 cm abaixo do da perua: o capô é mais baixo)
X_MALAS = 0.62
Y_PISO_MALAS = 0.50
Z_TAMPA = -1.76          # a dobradiça da tampa do porta-malas (na frente dela, embaixo do vidro de trás)
PIVO_TAMPA = (0.0, 0.968, Z_TAMPA)
DESCER = -0.08           # o lugar do motorista é o da perua 8 cm mais baixo
Y_RECUO = (0.52, 0.78)
Z_RECUO = 2.10
# a inclinação para dentro das janelas de lado (o "tumblehome"), em metros de x por metro de altura acima da cintura.
# 29/10/2026, a pedido ("dos lados das portas, dá para ver... as portas estão abertas, sem ser fechadas"): a janela da
# porta e o friso dela ficavam em pé, 5 cm para fora da beirada do teto (x 0,80) e 2 cm abaixo dele: de fora (e de
# cima), um vão escuro e o friso solto, como a porta entreaberta. Inclinadas, a coluna de trás encontra a beirada do
# teto e a janela da porta sobe até ele
TOMBO = 0.08
Y_TOPO_JANELA = 1.325    # o alto do vidro da porta (entra 5 mm embaixo do teto, que começa em 1,32)

# os lugares (o quadril de quem senta) e o resto que a montagem da Unity usa (para conferência)
LUGARES = [(-0.37, 0.50, 0.06), (0.37, 0.50, 0.06), (-0.36, 0.48, -0.64), (0.36, 0.48, -0.64)]


def _y_capo(z):
    """A altura de cima do capô (e dos para-lamas) em z."""
    return 0.900 - 0.0714 * (z - 1.10)


def _y_fastback(z):
    (zb, yb), (zt, yt) = (-1.80, 0.951), (Z_FIM_TETO, 1.35)
    return yb + (z - zb) * (yt - yb) / (zt - zb)


def _tombo(s):
    """A inclinação das janelas de lado (TOMBO): x' = x - s * TOMBO * (y - Y_CINTURA); na cintura não muda nada."""
    return Matrix(((1, -s * TOMBO, 0, s * TOMBO * Y_CINTURA), (0, 1, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))


def _arco(zc, para_tras):
    pts = []
    n = 9
    for i in range(n + 1):
        t = math.pi * i / n if para_tras else math.pi * (1 - i / n)
        pts.append((zc + R_ARCO * math.cos(t), R_RODA + R_ARCO * math.sin(t)))
    return [(pts[0][0], Y_BASE)] + pts + [(pts[-1][0], Y_BASE)]


def _recuo(a, b):
    def z_em(y):
        return a[0] + (b[0] - a[0]) * (a[1] - y) / (a[1] - b[1])
    return [(z_em(Y_RECUO[1]), Y_RECUO[1]), (Z_RECUO, Y_RECUO[1]), (Z_RECUO, Y_RECUO[0]), (z_em(Y_RECUO[0]), Y_RECUO[0])]


# ---------------------------------------------------------------------------------------------------- as cores
# (as superfícies novas entram no catálogo da v1 só neste processo; a v1 não muda)
CORES_CUPE = {
    "pintura": (14, 14, 16), "faixa": (86, 88, 92), "aro": (24, 24, 26),
    "vinil": (54, 54, 58), "painel": (26, 26, 28), "carpete": (28, 28, 30), "forro": (132, 130, 124),
    "int_plastico": (18, 18, 20), "cinto": (32, 32, 34), "plastico": (20, 20, 21),
}
# as cores fixas dos ramos dos materiais da v1 (o desbotado da pintura mostarda, o vinil turquesa...) trocadas pelas do
# cupê: a pintura preta desbota para um cinza, o vinil para um cinza mais claro
TROCA_DE_CORES = {
    (206, 176, 112): (62, 62, 66), (150, 112, 44): (6, 6, 7),
    (90, 158, 160): (92, 92, 96), (40, 100, 108): (36, 36, 38), (140, 140, 125): (110, 110, 106), (90, 150, 150): (82, 82, 84),
    (55, 80, 82): (50, 50, 52), (60, 90, 98): (62, 62, 66), (110, 170, 170): (96, 96, 100),
}


def preparar_catalogo():
    v1.SUPERFICIES["faixa"] = ("A", "Perua_Lataria")
    v1.SUPERFICIES["aro"] = ("A", "Perua_Lataria")
    v1.PROPRIEDADES["faixa"] = (0.0, 0.5, 1.0, 0.7, 0.6)
    v1.PROPRIEDADES["aro"] = (0.35, 0.45, 0.9, 0.8, 0.0)
    v1.CORES.update(CORES_CUPE)
    v2.SO_TEXTURA.add("faixa")
    original = v1._lin
    if not getattr(original, "_cupe", False):
        def lin(r, g, b):
            return original(*TROCA_DE_CORES.get((r, g, b), (r, g, b)))
        lin._cupe = True
        v1._lin = lin
    v1.VERSAO_DOS_MATERIAIS = 300


# ---------------------------------------------------------------------------------------------------- a carroceria
def _carroceria(cena, M, nome):
    m = M()
    # ======================================================== a lataria: os para-lamas, o meio da frente e o cofre
    frente = [(Z_PORTA, Y_BASE), (Z_PORTA, Y_CINTURA), (1.10, _y_capo(1.10)), (2.08, _y_capo(2.08)), (2.18, 0.795)]
    frente += _recuo((2.18, 0.795), (2.21, 0.50)) + [(2.21, 0.50), (2.18, 0.26)]
    frente += _arco(EIXO, True)
    for s in (-1, 1):
        m.perfil("pintura", frente, s * (X_CAPO + 0.005), s * W, chanfro=0.03)
    meio = [(Z_PORTA, Y_BASE), (Z_PORTA, Y_CINTURA), (1.10, _y_capo(1.10)), (Z_CAPO, _y_capo(Z_CAPO) - 0.026),
            (Z_CAPO, Y_COFRE), (2.06, Y_COFRE), (2.06, 0.80), (2.16, 0.795)]
    meio += _recuo((2.16, 0.795), (2.21, 0.50)) + [(2.21, 0.50), (2.18, 0.26), (2.18, Y_BASE)]
    m.perfil("pintura", meio, -(X_CAPO + 0.005), X_CAPO + 0.005, chanfro=0.012)
    for s in (-1, 1):
        v1._forro_do_arco(m, EIXO, s * (X_CAPO + 0.02), s * (W - 0.012))
        v1._forro_do_arco(m, -EIXO, s * (X_MALAS + 0.01), s * (W - 0.012))
        # o canto da frente, do lado de fora da grade (tapa o recuo)
        m.caixa("pintura", (W - 0.80, Y_RECUO[1] - Y_RECUO[0], 0.07), (s * (0.80 + W) / 2, sum(Y_RECUO) / 2, Z_RECUO + 0.035), chanfro=0)
    zf = 2.06 - Z_CAPO
    m.caixa("escuro", (2 * X_CAPO, 0.01, zf), (0, Y_COFRE + 0.006, (Z_CAPO + 2.06) / 2), chanfro=0)
    m.caixa("escuro", (2 * X_CAPO, _y_capo(Z_CAPO) - 0.03 - Y_COFRE, 0.01), (0, (_y_capo(Z_CAPO) - 0.03 + Y_COFRE) / 2, Z_CAPO + 0.006), chanfro=0)
    m.caixa("plastico", (1.20, 0.012, 0.07), (0, _y_capo(Z_CAPO) - 0.012, Z_CAPO - 0.03), giro=(-8, 0, 0), chanfro=0.003)
    for i in range(15):
        m.caixa("grade", (0.05, 0.004, 0.035), (-0.56 + i * 0.08, _y_capo(Z_CAPO) - 0.004, Z_CAPO - 0.03), giro=(-8, 0, 0), chanfro=0)

    # ======================================================== trás: os para-lamas com a "anca", o meio (porta-malas e o painel das lanternas)
    tras = [(Z_FIM_PORTA, Y_BASE), (Z_FIM_PORTA, Y_CINTURA), (-0.95, 0.93), (-1.55, 0.966), (-2.24, 0.958), (-2.30, 0.935),
            (-2.32, 0.55), (-2.28, 0.26)]
    tras += _arco(-EIXO, False)
    for s in (-1, 1):
        m.perfil("pintura", tras, s * X_MALAS, s * W, chanfro=0.03)
    meio_tras = [(-1.10, Y_BASE), (-1.10, Y_PISO_MALAS), (-2.22, Y_PISO_MALAS), (-2.22, 0.93), (-2.30, 0.93), (-2.32, 0.55),
                 (-2.28, 0.26), (-2.28, Y_BASE)]
    m.perfil("pintura", meio_tras, -X_MALAS, X_MALAS, chanfro=0.008)
    # o porta-malas por dentro: o piso, as paredes e a soleira; a prateleira embaixo do vidro de trás
    m.caixa("carpete", (2 * X_MALAS - 0.02, 0.02, 1.10), (0, Y_PISO_MALAS + 0.01, -1.66), chanfro=0)
    for s in (-1, 1):
        m.caixa("carpete", (0.014, 0.44, 1.10), (s * (X_MALAS - 0.01), Y_PISO_MALAS + 0.22, -1.66), chanfro=0)
    m.caixa("carpete", (2 * X_MALAS - 0.02, 0.44, 0.014), (0, Y_PISO_MALAS + 0.22, -2.21), chanfro=0)
    m.caixa("int_cromo", (2 * X_MALAS - 0.04, 0.012, 0.05), (0, 0.935, -2.25), chanfro=0.003)
    m.caixa("carpete", (1.50, 0.02, 0.86), (0, 0.935, -1.31), chanfro=0.004)   # a prateleira (de -0,88 a -1,74)
    for x in (-0.45, 0.45):
        m.cilindro("grade", 0.07, 0.012, (x, 0.948, -1.45), eixo="y", lados=14)   # os alto-falantes da prateleira

    # ======================================================== o assoalho, as soleiras, o túnel, o fundo e os tapetes
    m.caixa("carpete", (1.64, 0.10, 2.04), (0, 0.27, -0.08), chanfro=0)
    for s in (-1, 1):
        m.caixa("pintura", (0.08, 0.14, 2.04), (s * 0.85, 0.29, -0.08), chanfro=0.008)
        m.caixa("int_cromo", (0.05, 0.004, 1.96), (s * 0.83, 0.362, -0.08), chanfro=0)
    m.caixa("painel", (0.22, 0.14, 1.10), (0, 0.39, 0.15), chanfro=0.02, seg=2)
    m.caixa("escuro", (1.20, 0.02, 3.7), (0, 0.205, -0.05), chanfro=0)
    for x, z, c in ((-0.37, 0.42, 0.50), (0.37, 0.42, 0.50), (-0.37, -0.36, 0.30), (0.37, -0.36, 0.30)):
        m.caixa("tapete", (0.46, 0.008, c), (x, 0.324, z), chanfro=0.004)

    # ======================================================== o teto, as colunas, os vidros
    (zb, yb), (zt, yt) = PARABRISA
    comp_teto = zt - Z_FIM_TETO + 0.04
    meio_teto = (zt + Z_FIM_TETO) / 2
    m.caixa("pintura", (1.60, 0.04, comp_teto), (0, Y_TETO - 0.02, meio_teto), chanfro=0.014, seg=2)
    m.caixa("forro", (1.52, 0.03, comp_teto - 0.06), (0, Y_TETO - 0.055, meio_teto), chanfro=0.006)
    # o vidro de trás (o fastback) e as faixas pintadas dos lados dele; o forro da queda por dentro
    (vb, ub), (vt, ut) = VIDRO_TRAS
    m.ripa("vidro", (0, ub, vb), (0, ut, vt), 1.46, 0.01)
    for s in (-1, 1):
        m.ripa("pintura", (s * 0.77, ub - 0.01, vb - 0.01), (s * 0.77, ut + 0.012, vt + 0.01), 0.09, 0.03, chanfro=0.006)
        m.ripa("borracha", (s * 0.725, ub, vb + 0.004), (s * 0.725, ut, vt + 0.004), 0.02, 0.024)
        m.ripa("cromo", (s * 0.735, ub + 0.006, vb), (s * 0.735, ut + 0.006, vt), 0.008, 0.012)
    m.ripa("borracha", (-0.73, ut + 0.002, vt + 0.006), (0.73, ut + 0.002, vt + 0.006), 0.024, 0.024)
    m.ripa("borracha", (-0.73, ub + 0.002, vb - 0.004), (0.73, ub + 0.002, vb - 0.004), 0.024, 0.024)
    for s in (-1, 1):
        # a coluna A
        m.ripa("pintura", (s * 0.82, yb, zb + 0.01), (s * 0.79, yt, zt), 0.05, 0.08, chanfro=0.008)
        m.ripa("painel", (s * 0.79, yb + 0.03, zb - 0.02), (s * 0.765, yt - 0.03, zt - 0.02), 0.02, 0.07, chanfro=0.006)
        # a coluna B (fina e preta) e o vidro de lado, atrás da porta (com a borracha e o friso), e a coluna C: inclinados
        # para dentro como a janela da porta (TOMBO)
        m.entrar(_tombo(s))
        m.caixa("plastico", (0.04, Y_TETO - Y_CINTURA - 0.06, 0.06), (s * 0.835, (Y_TETO + Y_CINTURA) / 2 - 0.03, Z_FIM_PORTA - 0.035), chanfro=0.004)
        q = [(-0.37, Y_CINTURA + 0.02), (-0.37, 1.30), (-0.82, 1.30), (-1.18, _y_fastback(-1.18) - 0.06), (-1.18, Y_CINTURA + 0.02)]
        m.perfil("vidro", q, s * 0.832, s * 0.846)
        for i in range(len(q)):
            (za, ya), (zc_, yc) = q[i], q[(i + 1) % len(q)]
            m.ripa("borracha", (s * 0.842, ya, za), (s * 0.842, yc, zc_), 0.014, 0.026)
            m.ripa("cromo", (s * 0.852, ya, za), (s * 0.852, yc, zc_), 0.006, 0.012)
        # a coluna C (a "vela" do fastback): entre o vidro de lado, a queda do teto e a cintura
        vela = [(Z_FIM_TETO + 0.02, Y_TETO), (-1.80, _y_fastback(-1.80)), (-1.80, Y_CINTURA + 0.03), (-1.18, Y_CINTURA + 0.02),
                (-1.18, _y_fastback(-1.18) - 0.06), (-0.82, 1.30), (-0.37, 1.30), (-0.37, Y_TETO - 0.04)]
        m.perfil("pintura", vela, s * 0.80, s * 0.836, chanfro=0.008)
        m.sair()
        m.caixa("painel", (0.02, 0.50, 0.80), (s * 0.78, 0.66, -0.76), chanfro=0.006)   # o forro do lado de trás, por dentro
        m.caixa("cromo", (0.012, 0.014, comp_teto - 0.02), (s * 0.80, Y_TETO - 0.025, meio_teto), chanfro=0.003)   # a calha
    # o para-brisa, as borrachas, os frisos e os limpadores
    m.ripa("vidro", (0, yb, zb), (0, yt, zt), 1.56, 0.01)
    for s in (-1, 1):
        m.ripa("borracha", (s * 0.79, yb + 0.005, zb + 0.008), (s * 0.77, yt, zt + 0.004), 0.03, 0.022)
        m.ripa("cromo", (s * 0.805, yb + 0.005, zb + 0.016), (s * 0.785, yt + 0.005, zt + 0.01), 0.008, 0.014)
    m.ripa("borracha", (-0.78, yt - 0.002, zt + 0.006), (0.78, yt - 0.002, zt + 0.006), 0.03, 0.024)
    m.ripa("cromo", (-0.78, yt + 0.008, zt + 0.014), (0.78, yt + 0.008, zt + 0.014), 0.014, 0.008)
    m.ripa("borracha", (-0.80, yb + 0.004, zb + 0.01), (0.80, yb + 0.004, zb + 0.01), 0.03, 0.02)
    d = (Vector((0, yt, zt)) - Vector((0, yb, zb))).normalized()
    for x in (-0.42, 0.06):
        p0 = Vector((x, yb + 0.03, zb - 0.045))
        p1 = p0 + Vector((0.40, 0, 0)) + d * 0.05
        m.cilindro("plastico", 0.016, 0.03, tuple(p0 + Vector((0, -0.01, 0))), eixo="y", lados=10)
        m.ripa("plastico", tuple(p0), tuple(p1), 0.014, 0.012)
        m.ripa("borracha", tuple(p0 + Vector((0.03, 0.01, 0.012))), tuple(p1 + Vector((0.04, 0.01, 0.012))), 0.01, 0.018)
    # o retrovisor de dentro e os quebra-sóis
    m.caixa("int_plastico", (0.22, 0.065, 0.03), (0, yt - 0.09, zt - 0.04), chanfro=0.01, seg=2)
    m.caixa("int_cromo", (0.20, 0.05, 0.004), (0, yt - 0.09, zt - 0.057), chanfro=0)
    m.ripa("int_plastico", (0, yt - 0.035, zt - 0.02), (0, yt - 0.07, zt - 0.04), 0.02, 0.02)
    for s in (-1, 1):
        m.caixa("forro", (0.34, 0.02, 0.14), (s * 0.40, Y_TETO - 0.085, zt - 0.10), giro=(6, 0, 0), chanfro=0.008)

    _frente(m)
    _traseira(m)
    # ======================================================== os lados: a faixa cinza, os frisos e o pisca
    for s in (-1, 1):
        x = s * (W + 0.0005)
        for z0, z1 in ((2.15, Z_PORTA + 0.02), (Z_FIM_PORTA - 0.02, -2.27)):
            m.perfil("faixa", [(z0, 0.775), (z1, 0.775), (z1, 0.735), (z0, 0.735)], x, x + s * 0.003)
        for z0, z1 in ((Z_PORTA - 0.005, EIXO - R_ARCO - 0.06), (2.12, EIXO + R_ARCO + 0.05), (Z_FIM_PORTA - 0.01, -EIXO + R_ARCO + 0.05),
                       (-EIXO - R_ARCO - 0.05, -2.24)):
            if z0 > z1:
                m.caixa("cromo", (0.008, 0.02, z0 - z1), (s * (W + 0.002), 0.36, (z0 + z1) / 2), chanfro=0.002)
        m.caixa("pisca", (0.008, 0.03, 0.06), (s * (W + 0.006), 0.62, 2.06), chanfro=0.003)
        m.caixa("lente_lanterna", (0.008, 0.03, 0.05), (s * (W + 0.006), 0.62, -2.16), chanfro=0.003)
        # a tampa do tanque (do lado esquerdo, atrás)
        if s < 0:
            m.cilindro("cromo", 0.045, 0.01, (s * (W + 0.004), 0.83, -1.86), eixo="x", lados=16)

    # ======================================================== o interior (o lugar do motorista é o da perua, 8 cm mais baixo)
    m.entrar(Matrix.Translation((0, DESCER, 0)))
    v1._painel(m)
    m.sair()
    _bancos(m)
    return m.objeto(nome, cena)


def _frente(m):
    """A grade preta de lado a lado (com a moldura cromada), os faróis redondos com o bisel cromado e o refletor, as
    luzes quadradas de estacionamento, o emblema, o para-choque cromado fino com as garras pretas e a placa."""
    xg, yg0, yg1 = 0.80, 0.53, 0.77
    m.caixa("grade", (2 * xg, yg1 - yg0, 2.16 - Z_RECUO), (0, (yg0 + yg1) / 2, (2.16 + Z_RECUO) / 2), chanfro=0)
    for i in range(9):
        m.caixa("plastico", (2 * xg - 0.02, 0.008, 0.014), (0, yg0 + 0.02 + i * 0.025, 2.166), chanfro=0.002)
    for x in (-0.53, -0.27, 0.0, 0.27, 0.53):
        m.caixa("plastico", (0.008, yg1 - yg0 - 0.02, 0.014), (x, (yg0 + yg1) / 2, 2.168), chanfro=0.002)
    for p, q in (((-xg, yg0), (xg, yg0)), ((-xg, yg1), (xg, yg1)), ((-xg, yg0), (-xg, yg1)), ((xg, yg0), (xg, yg1))):
        m.ripa("cromo", (p[0], p[1], 2.172), (q[0], q[1], 2.172), 0.016, 0.016, chanfro=0.004)
    m.cilindro("cromo", 0.032, 0.012, (0, (yg0 + yg1) / 2, 2.178), eixo="z", lados=16)
    m.caixa("faixa", (0.04, 0.02, 0.004), (0, (yg0 + yg1) / 2, 2.186), chanfro=0)
    for s in (-1, 1):
        _farol(m, s * 0.66, 0.655, 2.168)
        # a luz quadrada de estacionamento, dentro da grade
        m.caixa("cromo", (0.11, 0.085, 0.016), (s * 0.43, 0.65, 2.170), chanfro=0.004)
        m.caixa("re", (0.094, 0.07, 0.012), (s * 0.43, 0.65, 2.177), chanfro=0.003)
    # o para-choque: a lâmina cromada fina, dobrando nos cantos, as garras pretas e os piscas embaixo
    m.caixa("cromo", (1.70, 0.07, 0.07), (0, 0.45, 2.24), chanfro=0.02, seg=2)
    for s in (-1, 1):
        m.ripa("cromo", (s * 0.84, 0.45, 2.22), (s * 0.91, 0.45, 2.06), 0.07, 0.07, chanfro=0.015)
        m.caixa("borracha", (0.06, 0.22, 0.06), (s * 0.32, 0.43, 2.285), chanfro=0.02, seg=2)
        m.caixa("plastico", (0.05, 0.06, 0.16), (s * 0.32, 0.45, 2.19), chanfro=0.008)
        m.caixa("pisca", (0.14, 0.05, 0.03), (s * 0.58, 0.34, 2.19), chanfro=0.008)
    m.caixa("pintura", (1.60, 0.10, 0.06), (0, 0.33, 2.17), chanfro=0.015)   # a saia de baixo
    m.caixa("plastico", (0.38, 0.13, 0.012), (0, 0.33, 2.205), chanfro=0.004)
    m.caixa("placa_f", (0.36, 0.11, 0.008), (0, 0.33, 2.214), chanfro=0.002)


def _farol(m, x, y, z):
    """O farol redondo: o copo escuro, o refletor cromado, o bisel e a lente (o material que a Unity acende)."""
    r = 0.082
    m.cilindro("grade", r + 0.012, 0.05, (x, y, z - 0.03), eixo="z", lados=20)
    perfil = [(r * (1 - t) + 0.012 * t, z - 0.004 - 0.035 * (t ** 0.6)) for t in (i / 5 for i in range(6))]
    m.torno("refletor", perfil, centro=(x, y, 0), eixo="z", lados=20, fechar=(False, True))
    m.cilindro("re", 0.012, 0.025, (x, y, z - 0.02), eixo="z", lados=10)
    m.torno("cromo", [(r + 0.002, z + 0.0), (r + 0.018, z + 0.004), (r + 0.016, z + 0.012), (r + 0.002, z + 0.010)], centro=(x, y, 0),
            eixo="z", lados=24)
    m.cilindro("lente_farol", r, 0.008, (x, y, z + 0.006), eixo="z", lados=24, raio2=r - 0.004)


def _farol_leve(m, x, y, z):
    r = 0.082
    m.cilindro("grade", r + 0.012, 0.05, (x, y, z - 0.03), eixo="z", lados=12)
    m.cilindro("refletor", r, 0.004, (x, y, z - 0.012), eixo="z", lados=12)
    m.torno("cromo", [(r + 0.002, z + 0.0), (r + 0.018, z + 0.004), (r + 0.016, z + 0.012), (r + 0.002, z + 0.010)], centro=(x, y, 0),
            eixo="z", lados=12)
    m.cilindro("lente_farol", r, 0.008, (x, y, z + 0.006), eixo="z", lados=12, raio2=r - 0.004)


def _traseira(m):
    """O painel de trás: as duas lanternas compridas (o vermelho acende) com o bisel cromado e a ré, a tampa do tanque
    redonda no meio, o para-choque cromado, a placa embaixo e os dois escapes."""
    for s in (-1, 1):
        x0, x1 = s * 0.12, s * 0.74
        xc, lx = (x0 + x1) / 2, abs(x1 - x0)
        m.caixa("cromo", (lx + 0.02, 0.13, 0.02), (xc, 0.715, -2.315), chanfro=0.006)
        m.caixa("lente_lanterna", (lx - 0.01, 0.10, 0.012), (xc, 0.715, -2.328), chanfro=0.004)
        m.caixa("re", (0.08, 0.10, 0.013), (s * 0.17, 0.715, -2.329), chanfro=0.003)
        for k in range(1, 6):
            m.caixa("cromo", (0.004, 0.10, 0.004), (x0 + s * lx * k / 6, 0.715, -2.336), chanfro=0)
    m.cilindro("cromo", 0.05, 0.02, (0, 0.715, -2.322), eixo="z", lados=18)
    m.cilindro("grade", 0.035, 0.006, (0, 0.715, -2.334), eixo="z", lados=14)
    m.caixa("cromo", (1.76, 0.07, 0.07), (0, 0.45, -2.34), chanfro=0.02, seg=2)
    for s in (-1, 1):
        m.ripa("cromo", (s * 0.87, 0.45, -2.32), (s * 0.92, 0.45, -2.18), 0.07, 0.07, chanfro=0.015)
        m.caixa("borracha", (0.05, 0.12, 0.05), (s * 0.46, 0.44, -2.375), chanfro=0.015)
        m.tubo("aco", [(s * 0.42, 0.23, -1.70), (s * 0.42, 0.23, -2.20), (s * 0.44, 0.24, -2.36)], 0.024, lados=10)
        m.cilindro("cromo", 0.03, 0.07, (s * 0.44, 0.24, -2.36), eixo="z", lados=12)
    m.caixa("plastico", (0.38, 0.13, 0.012), (0, 0.33, -2.305), chanfro=0.004)
    m.caixa("placa_t", (0.36, 0.11, 0.008), (0, 0.33, -2.314), chanfro=0.002)


def _bancos(m):
    """Os dois da frente (os da perua, 8 cm mais baixos) e o banco de trás do cupê (mais baixo e mais para a frente,
    embaixo do teto reto), com os cintos da frente."""
    for s in (-1, 1):
        x = s * 0.37
        y = DESCER
        m.caixa("int_plastico", (0.40, 0.08, 0.40), (x, 0.36 + y, 0.08), chanfro=0.01)
        for xx in (x - 0.16, x + 0.16):
            m.caixa("int_cromo", (0.03, 0.02, 0.56), (xx, 0.33 + y, 0.10), chanfro=0.003)
        m.entrar(Matrix.Translation((x, 0.44 + y, 0.10)) @ Matrix.Rotation(math.radians(-6), 4, "X"))
        v1._almofada(m, 0.50, 0.14, 0.50, gomos=5, eixo="z", vinco=0.012)
        m.sair()
        m.entrar(Matrix.Translation((x, 0.80 + y, -0.22)) @ Matrix.Rotation(math.radians(-14), 4, "X"))
        v1._almofada(m, 0.50, 0.64, 0.12, gomos=5, eixo="y", vinco=0.012, frente=True)
        m.sair()
        m.entrar(Matrix.Translation((x, 1.17 + y, -0.30)) @ Matrix.Rotation(math.radians(-14), 4, "X"))
        m.caixa("vinil", (0.24, 0.13, 0.09), (0, 0, 0), chanfro=0.028, seg=2)
        for xx in (-0.06, 0.06):
            m.cilindro("int_cromo", 0.006, 0.08, (xx, -0.10, 0), eixo="y", lados=8)
        m.sair()
        xo = s * 0.78
        m.ripa("cinto", (xo, 1.18, -0.33), (s * 0.66, 0.84, -0.22), 0.004, 0.045)
        m.ripa("cinto", (s * 0.66, 0.84, -0.22), (s * 0.63, 0.39, -0.10), 0.004, 0.045)
        m.caixa("int_plastico", (0.03, 0.06, 0.03), (xo, 1.18, -0.33), chanfro=0.006)
        m.caixa("int_plastico", (0.03, 0.08, 0.04), (s * 0.10, 0.42, -0.06), chanfro=0.008)
    # o banco de trás (o quadril em y 0,48, z -0,64)
    m.entrar(Matrix.Translation((0, 0.335, -0.62)) @ Matrix.Rotation(math.radians(-5), 4, "X"))
    v1._almofada(m, 1.30, 0.15, 0.44, gomos=10, eixo="z", vinco=0.012)
    m.sair()
    m.entrar(Matrix.Translation((0, 0.66, -0.93)) @ Matrix.Rotation(math.radians(-20), 4, "X"))
    v1._almofada(m, 1.30, 0.50, 0.12, gomos=10, eixo="y", vinco=0.012, frente=True)
    m.sair()


# ---------------------------------------------------------------------------------------------------- portas, capô, tampa
def _porta(cena, M, nome, s):
    """A porta comprida do cupê (de Z_PORTA a Z_FIM_PORTA), a origem na dobradiça; o vidro com o friso cromado."""
    m = M()
    z0, z1 = Z_PORTA, Z_FIM_PORTA
    meio, comp = (z0 + z1) / 2, z0 - z1
    y0 = 0.36
    xo = W - 0.0175
    m.caixa("pintura", (0.035, Y_CINTURA - y0, comp), (s * xo, (Y_CINTURA + y0) / 2, meio), chanfro=0.01, seg=2)
    m.perfil("faixa", [(z0 - 0.015, 0.775), (z1 + 0.015, 0.775), (z1 + 0.015, 0.735), (z0 - 0.015, 0.735)], s * (W + 0.0005), s * (W + 0.0035))
    m.caixa("cromo", (0.008, 0.02, comp - 0.012), (s * (W + 0.002), 0.36, meio), chanfro=0.002)
    # por dentro: o forro de vinil, a chapa de cima, o descanso de braço com a alça, a maçaneta e a manivela
    xi = W - 0.06
    m.caixa("vinil", (0.03, 0.40, comp - 0.05), (s * xi, 0.58, meio), chanfro=0.008)
    m.caixa("painel", (0.03, Y_CINTURA - 0.78, comp - 0.05), (s * (xi + 0.002), (Y_CINTURA + 0.78) / 2, meio), chanfro=0.006)
    m.caixa("vinil", (0.07, 0.045, comp * 0.40), (s * (xi - 0.045), 0.64, meio + comp * 0.05), chanfro=0.012, seg=2)
    m.tubo("int_cromo", [(s * (xi - 0.02), 0.67, meio + comp * 0.20), (s * (xi - 0.055), 0.69, meio + comp * 0.17),
                         (s * (xi - 0.055), 0.69, meio - comp * 0.10), (s * (xi - 0.02), 0.67, meio - comp * 0.13)], 0.008, lados=6)
    zm = z0 - 0.17
    m.caixa("int_plastico", (0.01, 0.05, 0.10), (s * (xi - 0.017), 0.74, zm), chanfro=0.004)
    m.caixa("int_cromo", (0.012, 0.02, 0.07), (s * (xi - 0.022), 0.74, zm), chanfro=0.004)
    zc = meio - comp * 0.12
    m.cilindro("int_cromo", 0.016, 0.02, (s * (xi - 0.022), 0.53, zc), eixo="x", lados=10)
    m.ripa("int_cromo", (s * (xi - 0.032), 0.53, zc), (s * (xi - 0.032), 0.47, zc - 0.05), 0.012, 0.008)
    m.cilindro("int_plastico", 0.012, 0.035, (s * (xi - 0.048), 0.47, zc - 0.05), eixo="x", lados=8)
    m.cilindro("alto_falante", 0.065, 0.01, (s * (xi - 0.018), 0.46, z0 - 0.22), eixo="x", lados=16)
    m.cilindro("int_plastico", 0.006, 0.04, (s * (xi - 0.01), Y_CINTURA + 0.01, z1 + 0.12), eixo="y", lados=6)
    # por fora: a maçaneta cromada (de puxar, perto da borda de trás) e a fechadura
    m.caixa("plastico", (0.006, 0.035, 0.13), (s * (W + 0.002), 0.83, z1 + 0.14), chanfro=0.004)
    m.caixa("cromo", (0.018, 0.022, 0.12), (s * (W + 0.009), 0.83, z1 + 0.14), chanfro=0.006)
    m.cilindro("cromo", 0.011, 0.012, (s * (W + 0.006), 0.83, z1 + 0.25), eixo="x", lados=10)
    # a janela: o vidro, a borracha e o friso cromado em volta; o retrovisor no canto da frente
    # (inclinada para dentro, TOMBO, e subindo até embaixo do teto: o friso de cima encosta na beirada dele)
    (zb, yb), (zt, yt) = PARABRISA
    ytopo = Y_TOPO_JANELA
    m.entrar(_tombo(s))
    fa = (zb - 0.13, Y_CINTURA + 0.01)
    fb = (zb - 0.13 - (ytopo - fa[1]) * (zb - zt) / (yt - yb), ytopo)
    vidro = [fa, fb, (z1 + 0.04, ytopo), (z1 + 0.04, Y_CINTURA + 0.01)]
    m.perfil("vidro", vidro, s * 0.838, s * 0.852)
    borda = [(fa[0] + 0.02, fa[1] - 0.01), (fb[0] + 0.02, fb[1] + 0.015), (z1 + 0.02, ytopo + 0.015), (z1 + 0.02, Y_CINTURA)]
    for i in range(3):
        (za, ya), (zc_, yc) = borda[i], borda[i + 1]
        m.ripa("borracha", (s * 0.846, ya, za), (s * 0.846, yc, zc_), 0.03, 0.03)
        m.ripa("cromo", (s * 0.862, ya, za), (s * 0.862, yc, zc_), 0.006, 0.012)
    m.sair()
    m.caixa("borracha", (0.016, 0.014, comp - 0.02), (s * (W - 0.006), Y_CINTURA + 0.004, meio), chanfro=0.003)
    m.caixa("cromo", (0.006, 0.008, comp - 0.02), (s * (W + 0.002), Y_CINTURA + 0.008, meio), chanfro=0)
    m.caixa("plastico", (0.04, 0.06, 0.09), (s * 0.905, 0.95, zb - 0.20), chanfro=0.01)
    m.ripa("plastico", (s * 0.91, 0.96, zb - 0.20), (s * 0.98, 0.98, zb - 0.21), 0.03, 0.03)
    m.caixa("cromo", (0.10, 0.09, 0.07), (s * 1.0, 0.98, zb - 0.205), chanfro=0.025, seg=2)
    m.caixa("refletor", (0.085, 0.075, 0.006), (s * 1.0, 0.98, zb - 0.243), chanfro=0.002)
    return m.objeto(nome, cena, pivo=(s * W, 0, z0))


def _capo(cena, M, nome):
    """O capô comprido: a chapa, a faixa cinza larga no meio, a tomada de ar preta e os dois pinos cromados."""
    m = M()
    zt, zf = Z_CAPO, 2.12
    cima = [(zt, _y_capo(zt)), (1.60, _y_capo(1.60) + 0.006), (2.06, _y_capo(2.06)), (zf, _y_capo(2.06) - 0.03)]
    baixo = [(zf - 0.012, _y_capo(2.06) - 0.052), (2.06, _y_capo(2.06) - 0.025), (zt, _y_capo(zt) - 0.025)]
    m.perfil("pintura", cima + baixo, -X_CAPO, X_CAPO, chanfro=0.008)
    # a faixa cinza (uma chapa fina por cima, seguindo o capô)
    f = [(zt + 0.03, _y_capo(zt + 0.03) + 0.0015), (1.60, _y_capo(1.60) + 0.0075), (2.06, _y_capo(2.06) + 0.0015),
         (2.06, _y_capo(2.06) + 0.0045), (1.60, _y_capo(1.60) + 0.0105), (zt + 0.03, _y_capo(zt + 0.03) + 0.0045)]
    m.perfil("faixa", f, -0.32, 0.32)
    # a tomada de ar (preta, aberta na frente) e os pinos do capô
    ys = _y_capo(1.70) + 0.006
    m.caixa("plastico", (0.32, 0.045, 0.46), (0, ys + 0.02, 1.70), giro=(-4, 0, 0), chanfro=0.015, seg=2)
    m.caixa("grade", (0.26, 0.028, 0.01), (0, ys + 0.022, 1.935), giro=(-4, 0, 0), chanfro=0)
    for s in (-1, 1):
        m.cilindro("cromo", 0.016, 0.012, (s * 0.46, _y_capo(1.98) + 0.012, 1.98), eixo="y", lados=12)
        m.cilindro("cromo", 0.004, 0.03, (s * 0.46, _y_capo(1.98) + 0.03, 1.98), eixo="y", lados=6)
    yb = lambda z: _y_capo(z) - 0.03
    for a, b in (((-X_CAPO + 0.08, zt + 0.08), (X_CAPO - 0.08, 1.98)), ((X_CAPO - 0.08, zt + 0.08), (-X_CAPO + 0.08, 1.98))):
        m.ripa("escuro", (a[0], yb(a[1]), a[1]), (b[0], yb(b[1]), b[1]), 0.05, 0.01)
    return m.objeto(nome, cena, pivo=(0, _y_capo(zt), zt))


def _tampa(cena, M, nome):
    """A tampa do porta-malas (deitada, atrás do vidro): a chapa, o friso, a fechadura e o forro de dentro. A origem na
    dobradiça (PIVO_TAMPA, na borda da frente); abre girando em X, a borda de trás subindo."""
    m = M()
    x = X_MALAS - 0.006
    perfil = [(Z_TAMPA, 0.968), (-1.95, 0.962), (-2.20, 0.955), (-2.235, 0.94), (-2.235, 0.925), (-2.20, 0.935), (-1.95, 0.942),
              (Z_TAMPA, 0.948)]
    m.perfil("pintura", perfil, -x, x, chanfro=0.006)
    m.caixa("cromo", (2 * x - 0.02, 0.006, 0.012), (0, 0.957, -2.225), chanfro=0)
    m.cilindro("cromo", 0.014, 0.01, (0, 0.951, -2.233), eixo="z", lados=12)
    m.caixa("painel", (2 * x - 0.06, 0.008, 0.40), (0, 0.938, -1.98), chanfro=0.003)
    return m.objeto(nome, cena, pivo=PIVO_TAMPA)


def _roda(cena, M, nome, alta):
    """A roda de aço preta com o pneu de letras brancas: 18 lados, chapado (como a da perua v2 e a antiga). Raio 0,30,
    largura 0,21. A alta tem os furos do aro, os 5 parafusos e as letras do pneu (a leve recebe na textura)."""
    m = M()
    h = 0.105
    pneu = [(0.200, -h + 0.012), (0.250, -h), (0.288, -h + 0.010), (R_RODA, -h + 0.032),
            (R_RODA, h - 0.032), (0.288, h - 0.010), (0.250, h), (0.200, h - 0.012)]
    m.torno("pneu", pneu, lados=18, liso=False)
    m.torno("aro", [(0.200, h - 0.012), (0.19, h - 0.02), (0.19, -h + 0.02), (0.200, -h + 0.012)], lados=18, liso=False)
    m.torno("aco", [(0.0, -0.04), (0.15, -0.04), (0.15, -0.085), (0.0, -0.085)], lados=12, liso=False)
    # a cara do aro (o prato de aço com o degrau) e o cubo
    m.torno("aro", [(0.200, h - 0.004), (0.185, h + 0.004), (0.150, h + 0.004), (0.120, h + 0.016), (0.075, h + 0.018),
                    (0.0, h + 0.018)], lados=18, liso=False)
    m.torno("cromo", [(0.030, h + 0.018), (0.030, h + 0.030), (0.0, h + 0.036)], lados=10, liso=False)
    if alta:
        for i in range(8):
            a = 2 * math.pi * i / 8 + math.pi / 8
            m.caixa("grade", (0.006, 0.026, 0.026), (h + 0.010, 0.135 * math.cos(a), 0.135 * math.sin(a)), giro=(math.degrees(a), 0, 0), chanfro=0)
        for i in range(5):
            a = 2 * math.pi * i / 5 + math.pi / 10
            m.cilindro("cromo", 0.010, 0.018, (h + 0.024, 0.052 * math.cos(a), 0.052 * math.sin(a)), eixo="x", lados=6, liso=False)
        # as letras brancas (blocos de larguras diferentes, num anel do lado de fora)
        larguras = [0.022, 0.016, 0.02, 0.018, 0.024, 0.014, 0.02, 0.019]
        a = 0.0
        for volta in range(2):
            a = math.pi * volta
            for k, lg in enumerate(larguras):
                r = 0.262
                ang = lg / r
                m.caixa("re", (0.004, 0.022, lg), (h + 0.0005, r * math.cos(a + ang / 2), r * math.sin(a + ang / 2)),
                        giro=(math.degrees(a + ang / 2), 0, 0), chanfro=0)
                a += ang + 0.007
    return m.objeto(nome, cena)


# ---------------------------------------------------------------------------------------------------- montar a cena
NOMES = ["Cupe_Carroceria", "Cupe_Porta_E", "Cupe_Porta_D", "Cupe_Roda", "Cupe_Volante", "Cupe_Capo", "Cupe_Tampa"]
ALTO = "CupeAlto_"
LUGAR_DA_RODA = (-3.2, R_RODA, 0.0)
ASSAR = "Cupe_Assar"


def _cena():
    cena = bpy.data.scenes.get("Cupe")
    if cena is None:
        cena = bpy.context.scene
        cena.name = "Cupe"
    return cena


def _apagar(nomes):
    for n in nomes:
        o = bpy.data.objects.get(n)
        if o is not None:
            me = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if me is not None and me.users == 0:
                bpy.data.meshes.remove(me)


def _uma_versao(cena, M, prefixo, alta):
    salvos = (v1._almofada, v1._forro_do_arco)
    global _farol
    farol_salvo = _farol
    if not alta:
        v1._almofada, v1._forro_do_arco = v2._almofada_leve, v2._forro_do_arco_leve
        _farol = _farol_leve
    try:
        objs = [_carroceria(cena, M, prefixo + "Carroceria")]
        for nome, s in (("Porta_E", -1), ("Porta_D", 1)):
            objs.append(_porta(cena, M, prefixo + nome, s))
        roda = _roda(cena, v1.Malha, prefixo + "Roda", alta)   # a roda é chapada e de 18 lados nas duas
        roda.location = v1._para_blender(LUGAR_DA_RODA)
        objs.append(roda)
        objs.append(_volante(cena, M, prefixo + "Volante"))
        objs.append(_capo(cena, M, prefixo + "Capo"))
        objs.append(_tampa(cena, M, prefixo + "Tampa"))
    finally:
        v1._almofada, v1._forro_do_arco = salvos
        _farol = farol_salvo
    return objs


def _volante(cena, M, nome):
    """O volante da perua (o mesmo lugar em relação ao banco), nas cores do cupê."""
    salvo = v1.Malha
    v1.Malha = M
    try:
        o = v1._volante(cena)
    finally:
        v1.Malha = salvo
    o.name = nome
    o.data.name = nome
    o.location = v1._para_blender((-0.37, 0.86 + DESCER, 0.50))
    o.rotation_euler = (math.radians(90 + 25), 0, 0)
    return o


def construir():
    preparar_catalogo()
    cena = _cena()
    for o in list(cena.objects):
        if o.name.split(".")[0] in ("Cube", "Camera", "Light"):
            bpy.data.objects.remove(o, do_unlink=True)
    _apagar([ALTO + n[5:] for n in NOMES] + NOMES + [ASSAR])
    altos = _uma_versao(cena, v1.Malha, ALTO, True)
    leves = _uma_versao(cena, v2.MalhaLeve, "Cupe_", False)
    for grupo, rot in ((altos, "alto"), (leves, "leve")):
        tot = 0
        for o in grupo:
            t = sum(len(p.vertices) - 2 for p in o.data.polygons)
            tot += t * (4 if o.name.endswith("Roda") else 1)
            print(rot, o.name, t)
        print("TOTAL", rot, "(com 4 rodas):", tot)


# ---------------------------------------------------------------------------------------------------- UV, bake, exportar
PESO = dict(v2.PESO, aro=1.0)
PESO_DA_RODA = 2.6


def desembrulhar(margem=0.002):
    cena = _cena()
    objs = [bpy.data.objects[n] for n in NOMES]
    vl = cena.view_layers[0]
    for o in cena.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    vl.objects.active = objs[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_mode(type="FACE")
    cena.tool_settings.use_uv_select_sync = True
    for atlas in ("A", "B"):
        for o in objs:
            bm = bmesh.from_edit_mesh(o.data)
            sup = [mt.name[4:] for mt in o.data.materials]
            for f in bm.faces:
                f.select_set(v1.SUPERFICIES[sup[f.material_index]][0] == atlas)
            bm.select_flush_mode()
            bmesh.update_edit_mesh(o.data)
        bpy.ops.uv.smart_project(angle_limit=math.radians(70), island_margin=margem, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
        bpy.ops.uv.average_islands_scale()
        for o in objs:
            bm = bmesh.from_edit_mesh(o.data)
            uvl = bm.loops.layers.uv.active
            sup = [mt.name[4:] for mt in o.data.materials]
            for f in bm.faces:
                k = PESO.get(sup[f.material_index], 1.0) * (PESO_DA_RODA if o.name.endswith("Roda") else 1.0)
                if f.select and k != 1.0:
                    for l in f.loops:
                        l[uvl].uv = l[uvl].uv * k
            bmesh.update_edit_mesh(o.data)
        bpy.ops.uv.pack_islands(rotate=True, margin=margem, shape_method="CONCAVE")
    bpy.ops.object.mode_set(mode="OBJECT")
    print("UV ok")


def _imagem(atlas, canal):
    nome = f"Cupe_{atlas}_{canal}"
    tam = TAMANHO if atlas in ("A", "B") else 64
    img = bpy.data.images.get(nome)
    if img is None or img.size[0] != tam:
        if img is not None:
            bpy.data.images.remove(img)
        img = bpy.data.images.new(nome, tam, tam, alpha=False, float_buffer=False)
        img.colorspace_settings.name = "sRGB" if canal == "cor" else "Non-Color"
    img.use_fake_user = True
    return img


def _alvo(canal, atlas_do_passe=("A", "B")):
    """Aponta o nó BAKE de cada material para a imagem do atlas dele; os atlas fora do passe vão para uma imagem
    minúscula (assar um atlas de cada vez gasta metade da memória)."""
    for mat in bpy.data.materials:
        if not mat.name.startswith("Aut_"):
            continue
        atlas = v1.SUPERFICIES[mat.name[4:]][0]
        if atlas == "-" or atlas not in atlas_do_passe:
            atlas = "V"
        no = mat.node_tree.nodes.get("BAKE")
        no.image = _imagem(atlas, canal)
        mat.node_tree.nodes.active = no


def _juntar():
    cena = _cena()
    _apagar([ASSAR])
    bm = bmesh.new()
    mats = []
    for n in NOMES:
        o = bpy.data.objects[n]
        me = o.data.copy()
        me.transform(o.matrix_world)
        mapa = []
        for mt in me.materials:
            if mt not in mats:
                mats.append(mt)
            mapa.append(mats.index(mt))
        for p in me.polygons:
            p.material_index = mapa[p.material_index]
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(ASSAR)
    bm.to_mesh(me)
    bm.free()
    for mt in mats:
        me.materials.append(mt)
    o = bpy.data.objects.new(ASSAR, me)
    cena.collection.objects.link(o)
    return o


def assar(canais, amostras=8, margem=8, extrusao=0.012, distancia=0.03, atlas_do_passe=("A", "B")):
    import time
    import numpy as np
    preparar_catalogo()
    cena = _cena()
    cena.render.engine = "CYCLES"
    cena.cycles.device = "CPU"
    cena.cycles.samples = amostras
    cena.cycles.use_denoising = False
    if cena.world is None:
        cena.world = bpy.data.worlds.new("MundoCupe")
    cena.world.light_settings.distance = 0.35
    alvo = _juntar()
    altos = [bpy.data.objects[ALTO + n[5:]] for n in NOMES]
    for attr in ("visible_camera", "visible_diffuse", "visible_glossy", "visible_transmission", "visible_volume_scatter", "visible_shadow"):
        setattr(alvo, attr, False)
    for o in cena.objects:
        o.hide_render = not (o in altos or o == alvo)
    vl = cena.view_layers[0]
    for o in cena.objects:
        o.select_set(o in altos or o == alvo)
    vl.objects.active = alvo
    b = cena.render.bake
    b.use_selected_to_active = True
    b.use_cage = False
    b.margin = margem
    b.margin_type = "EXTEND"
    tipos = {"cor": "DIFFUSE", "aspereza": "ROUGHNESS", "metal": "EMIT", "normal": "NORMAL", "ao": "AO"}
    for canal in canais:
        t = time.time()
        _alvo(canal, atlas_do_passe)
        v1._ligar_metal(canal == "metal")
        kw = dict(pass_filter={"COLOR"}) if canal == "cor" else {}
        if canal == "normal":
            b.normal_space = "TANGENT"
        bpy.ops.object.bake(type=tipos[canal], use_selected_to_active=True, cage_extrusion=extrusao, max_ray_distance=distancia,
                            margin=margem, use_clear=True, **kw)
        v1._ligar_metal(False)
        for atlas in atlas_do_passe:
            img = _imagem(atlas, canal)
            a = np.empty(TAMANHO * TAMANHO * 4, np.float32)
            img.pixels.foreach_get(a)
            print(f"assado {canal} {atlas}: média {a.reshape(-1, 4)[:, :3].mean():.3f}, {time.time() - t:.0f} s")
            img.pack()
    _alvo("cor")
    for o in cena.objects:
        o.hide_render = o.name.startswith(ALTO)
    _apagar([ASSAR])


def compor():
    import numpy as np
    os.makedirs(TEXTURAS, exist_ok=True)
    for atlas, final in (("A", "Cupe_Lataria"), ("B", "Cupe_Interior")):
        def ler(canal):
            a = np.empty(TAMANHO * TAMANHO * 4, dtype=np.float32)
            _imagem(atlas, canal).pixels.foreach_get(a)
            return a.reshape(TAMANHO, TAMANHO, 4)
        # um canal de cada vez (com a memória curta: a Unity e outro Blender abertos)
        oc = ler("ao")[..., 0].copy()

        def feito_cor():
            c = ler("cor")
            c[..., :3] *= (0.55 + 0.45 * oc)[..., None]
            c[..., 3] = 1.0
            return c

        def feito_ms():
            ms = ler("metal")
            ms[..., 0] = np.clip(ms[..., 0], 0, 1)
            ms[..., 1] = oc
            ms[..., 2] = 0.0
            ms[..., 3] = 1.0 - np.clip(ler("aspereza")[..., 0], 0, 1)
            return ms

        def feito_normal():
            n = ler("normal")
            n[..., 3] = 1.0
            return n

        for sufixo, fazer, espaco in (("Cor", feito_cor, "sRGB"), ("MS", feito_ms, "Non-Color"), ("Normal", feito_normal, "Non-Color")):
            dados = fazer()
            nome = f"{final}_{sufixo}"
            img = bpy.data.images.get(nome)
            if img is None or img.size[0] != TAMANHO:
                if img:
                    bpy.data.images.remove(img)
                img = bpy.data.images.new(nome, TAMANHO, TAMANHO, alpha=True, float_buffer=False)
            img.colorspace_settings.name = espaco
            img.pixels.foreach_set(dados.ravel())
            img.filepath_raw = os.path.join(TEXTURAS, nome + ".png")
            img.file_format = "PNG"
            img.save()
            bpy.data.images.remove(img)   # já está no disco; não precisa ficar no .blend
            del dados
        print("texturas:", final)


FINAIS = {"Perua_Lataria": "Cupe_Lataria", "Perua_Interior": "Cupe_Interior", "Perua_Vidro": "Cupe_Vidro",
          "Perua_Farol": "Cupe_Farol", "Perua_Lanterna": "Cupe_Lanterna"}


def exportar():
    import numpy as np
    preparar_catalogo()
    cena = _cena()
    os.makedirs(DESTINO, exist_ok=True)
    finais = {f1: (bpy.data.materials.get(f2) or bpy.data.materials.new(f2)) for f1, f2 in FINAIS.items()}
    temporarios = []
    for n in NOMES:
        o = bpy.data.objects[n]
        me = o.data.copy()
        sup = [mt.name[4:] for mt in o.data.materials]
        ordem = list(FINAIS.keys())
        usados = [f for f in ordem if any(v1.SUPERFICIES[s][1] == f for s in sup)]
        mapa = np.array([usados.index(v1.SUPERFICIES[s][1]) for s in sup], dtype=np.int32)
        idx = np.empty(len(me.polygons), dtype=np.int32)
        me.polygons.foreach_get("material_index", idx)
        me.materials.clear()
        for f in usados:
            me.materials.append(finais[f])
        me.polygons.foreach_set("material_index", mapa[idx])
        me.update()
        o.name = n + "_aut"
        t = bpy.data.objects.new(n, me)
        cena.collection.objects.link(t)
        t.rotation_euler = (math.radians(90), 0, 0)
        temporarios.append((t, o, n))
    vl = cena.view_layers[0]
    for o in cena.objects:
        o.select_set(any(o is t for t, _, _ in temporarios))
    vl.objects.active = temporarios[0][0]
    try:
        bpy.ops.export_scene.fbx(filepath=os.path.join(DESTINO, "Cupe.fbx"), use_selection=True, object_types={"MESH"},
                                 apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE", path_mode="STRIP")
    finally:
        for t, o, n in temporarios:
            me = t.data
            bpy.data.objects.remove(t, do_unlink=True)
            bpy.data.meshes.remove(me)
            o.name = n
    print("FBX:", os.path.join(DESTINO, "Cupe.fbx"))


def salvar():
    for img in bpy.data.images:
        if img.name.startswith("Cupe_") and img.has_data and not img.packed_file:
            img.pack()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND, copy=False)
    print("blend:", BLEND)
