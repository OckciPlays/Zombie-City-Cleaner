# As melhorias das armas de sucata (10/11/2026, a etapa 2): as peças que a árvore destrava, modeladas no estilo do
# sucata.py (caixas e cilindros de poucos lados, cada face no meio de uma célula da Paleta.png), em dois FBX:
#
# - Sucata_Acessorios.fbx: as peças já no lugar de cada arma, no espaço dela (o mesmo do Sucata_<Arma>.fbx: um objeto
#   por peça, girado 90° em X, a malha nos eixos da Unity, o cano em +Z, o alto em +Y, a origem no cabo, com o mesmo
#   deslocamento dy do sucata_exportar.py). A Unity só põe cada uma como filha do corpo da arma, sem mexer na posição.
#     <Arma>_acc_SilenciadorPET     a garrafa PET de 300 ml presa na boca com fita prata, cortada e emendada com fita
#                                   no meio, o rótulo e o furo no fundo (a9, nível 1);
#     <Arma>_acc_SilenciadorFiltro  um filtro de óleo de carro: a luva de redução sextavada na boca com a abraçadeira,
#                                   o niple, a borracha de vedação, a lata azul com o rótulo e a ferrugem (a9, nível 2);
#     <Arma>_acc_MiraFerro          a alça de chapa com o entalhe e o parafuso de ajuste (de lado, no bloco) e a massa
#                                   com as orelhas e o ponto claro; no rifle e na submetralhadora elas cobrem as de hoje
#                                   (que são do corpo), na pistola (que não tinha) entram na altura de silenciador (a5, 1);
#     <Arma>_acc_MiraPonto          um tubinho de cano (de parede grossa, aberto), a lente na frente (de uma face só,
#                                   virada para a frente: o olho vê através), o pontinho vermelho virado para trás, a
#                                   pilha de relógio na fita com o fio de cobre, numa ponte de chapa (a5, nível 2);
#     Sucata_Rifle_acc_Luneta       um cano com a ocular de mangueira, a objetiva de luva, as lentes nas pontas, a
#                                   volta de fita, o parafuso de ajuste em cima e as duas braçadeiras, numa ponte de
#                                   chapa parafusada na culatra (a7, só no rifle);
#     <Arma>_acc_CanoReforcado1..3  a luva de cano galvanizado com as braçadeiras; o II com mais braçadeiras e a tala
#                                   (em cima, na pistola; dos lados, no rifle); o III com tudo, as voltas de fio de cobre
#                                   e, na pistola, a luva da contraporca (a4, níveis 1 a 3);
#     <Arma>_carregadorLongo        o carregador estendido (a6), no lugar do _carregador (anda com o osso mag): na
#                                   pistola, o bloco com um remendo de tábua parafusado embaixo, para 3 tiros; na
#                                   submetralhadora, o tubo de hoje com um pedaço de outra chapa emendado com fita.
#   As duas miras e a luneta ficam sobre pontes de chapa (as paredes dos lados, a chapa por cima), altas o bastante para
#   a linha da mira de ferro passar por baixo: a mira de ferro (a de hoje ou a ajustada) convive com a de ponto ou com
#   a luneta, como numa montagem "co-witness". A mira de ponto e a luneta é que não vão juntas (o mesmo lugar no rifle).
#   Os silenciadores não vão juntos, nem os três níveis do cano reforçado (cada nível é uma peça inteira).
# - Loot_Acessorios.fbx: as peças soltas, uma de cada tipo (no saque, no chão, no inventário), no formato do Loot.fbx:
#   Item_<Peca>_corpo, a origem embaixo e no meio, os itens um ao lado do outro em X.
# - Sucata_Acessorios_Miras.json: o ponto da mira de ponto e da luneta (onde o olho alinha), na malha da Unity; com o
#   eixo, a frente, um olho sugerido, a linha da mira de ferro ajustada e a boca nova de cada silenciador.
#
# Cores novas da paleta (já existiam na Paleta.png, nenhuma muda): o vidro (12, 4), o plástico da garrafa (9, 11), o
# rótulo verde (10, 13), a lata do filtro (8, 11), o rótulo dele (4, 11), o vermelho do ponto (6, 4), o claro da massa
# (9, 12) e o cano galvanizado (2, 7), na conta do sucata.py (a coluna e a linha de baixo para cima).
#
# Como rodar (um Blender em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/Armas/sucata_acessorios.py -- <etapa> <destino>
#   etapa: conferir (compara com os FBX do jogo, sem gravar), modelos (os FBX e o JSON), fotos (as fotos e a folha)
#   ou tudo; destino: --saida <pasta> (tudo nela) ou --assets (os FBX em Art/Armas/Modelos e Characters/Jogador).

import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import sucata  # noqa: E402
import sucata_exportar as sx  # noqa: E402
from sucata import (BORRACHA, COBRE, FERRUGEM, FERRUGEM_ESCURA, FITA_PRATA, MADEIRA, MADEIRA_CRUA,  # noqa: E402
                    METAL, METAL_CLARO, METAL_MEDIO, PRETO, QUASE_PRETO)

VIDRO = (12, 4)          # 8ab8d8, liso: as lentes
PLASTICO = (9, 11)       # 6a8a9a: a garrafa PET
ROTULO = (10, 13)        # 4d6a34: o rótulo verde da garrafa
FILTRO = (8, 11)         # 3a4a6a: a lata do filtro de óleo
ROTULO_FILTRO = (4, 11)  # b8b2a0
PONTO = (6, 4)           # c8302a: o pontinho vermelho
BRANCO = (9, 12)         # d2cbb2: o ponto claro da massa e da alça
GALVANIZADO = (2, 7)     # 9a9c98: a luva de cano

GIRO = Matrix.Rotation(math.radians(-90), 4, "X")
DY = {nome: dy for nome, _, dy, _ in sx.ARMAS}
FUNC = {nome: f for nome, f, _, _ in sx.ARMAS}
COS8 = math.cos(math.pi / 8)
NOMES_CURTOS = {"Sucata_Pistola": "Pistola", "Sucata_Submetralhadora": "Submetralhadora", "Sucata_Rifle": "Rifle"}


# ------------------------------------------------------------------------------------------------ o básico
def anel_pts(r, Z, lados=8, giro=None):
    """Os vértices (x, z) do corte de um cilindro do sucata.Malha ao longo de -Y, no mesmo giro (o centro em x 0)."""
    g = math.pi / lados if giro is None else giro
    return [(-math.cos(g + 2 * math.pi * k / lados) * r, Z - math.sin(g + 2 * math.pi * k / lados) * r)
            for k in range(lados)]


def casco(pts):
    """O contorno convexo (x, z) em volta dos pontos, em ordem (para a cinta das braçadeiras)."""
    P = sorted({(round(x, 6), round(z, 6)) for x, z in pts})

    def giro(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    baixo, cima = [], []
    for p in P:
        while len(baixo) >= 2 and giro(baixo[-2], baixo[-1], p) <= 1e-12:
            baixo.pop()
        baixo.append(p)
    for p in reversed(P):
        while len(cima) >= 2 and giro(cima[-2], cima[-1], p) <= 1e-12:
            cima.pop()
        cima.append(p)
    return baixo[:-1] + cima[:-1]


def bracadeira(m, y, Z, contorno, angulo=30.0, largura=0.008, parafuso=True):
    """Uma abraçadeira de mangueira em volta do contorno (x, z), centrada em y: a cinta, a caixinha do sem-fim no ponto
    do contorno mais perto do ângulo (graus, do +X para cima) e o parafuso dela."""
    m.cinta(y + largura / 2, largura, contorno, METAL_CLARO)
    d = (math.cos(math.radians(angulo)), math.sin(math.radians(angulo)))
    v = max(contorno, key=lambda p: d[0] * p[0] + d[1] * (p[1] - Z))
    phi = math.degrees(math.atan2(v[1] - Z, v[0]))
    dist = math.hypot(v[0], v[1] - Z)
    with m.giro((0, y, Z), (0, -phi, 0)):
        m.caixa((dist + 0.003, y, Z), (0.006, 0.010, 0.009), METAL_CLARO)
        if parafuso:
            m.cilindro((dist + 0.003, y, Z + 0.0045), (0, 0, 1), 0.003, 0.0026, 0.0026, METAL_MEDIO, lados=6,
                       tampas="fim")


def tubo_oco(m, y_tras, y_frente, za, r_fora, r_dentro, lados=8):
    """Um pedaço de cano de parede grossa, aberto: a parede de fora, a de dentro (virada para o eixo) e os dois aros.
    De dentro, o olho vê o escuro da parede em volta e o mundo pelo meio."""
    g = math.pi / lados

    def anel(y, r):
        return [m._vert((-math.cos(g + 2 * math.pi * k / lados) * r, y, za - math.sin(g + 2 * math.pi * k / lados) * r))
                for k in range(lados)]

    fa, fb, da, db = anel(y_tras, r_fora), anel(y_frente, r_fora), anel(y_tras, r_dentro), anel(y_frente, r_dentro)
    tras = 1 if y_tras > y_frente else -1
    for k in range(lados):
        j = (k + 1) % lados
        a = g + 2 * math.pi * (k + 0.5) / lados
        meio = Vector((-math.cos(a), 0, -math.sin(a)))
        m._virar(m._face([fa[k], fb[k], fb[j], fa[j]], METAL_MEDIO), m._local(meio))
        m._virar(m._face([da[k], db[k], db[j], da[j]], QUASE_PRETO), m._local(-meio))
        m._virar(m._face([fa[k], fa[j], da[j], da[k]], METAL), m._local((0, tras, 0)))
        m._virar(m._face([fb[k], fb[j], db[j], db[k]], METAL), m._local((0, -tras, 0)))


# ------------------------------------------------------------------------------------------------ os silenciadores
R_PET = 0.025
R_FILTRO = 0.027


def silenciador_pet(m, y0, Z, rb):
    """A garrafa PET (y0: a boca do cano; rb: o raio do que a fita abraça na boca). Devolve a boca nova."""
    e = (0, -1, 0)
    m.cilindro((0, y0 + 0.014, Z), e, 0.030, rb + 0.0015, 0.0153, FITA_PRATA)        # a fita: a boca e o gargalo
    m.furo((0, y0 + 0.014, Z), (0, 1, 0), rb * 0.7)
    m.cilindro((0, y0 - 0.016, Z), e, 0.003, 0.0178, 0.0178, PLASTICO)                # o anel do gargalo
    m.cilindro((0, y0 - 0.019, Z), e, 0.026, 0.0150, R_PET, PLASTICO, lados=10, tampas=False)   # o ombro
    m.cilindro((0, y0 - 0.045, Z), e, 0.080, R_PET, R_PET, PLASTICO, lados=10, tampas=False)    # o corpo
    m.cilindro((0, y0 - 0.074, Z), e, 0.014, R_PET + 0.0006, R_PET + 0.0006, FITA_PRATA, lados=10, tampas=False)
    m.cilindro((0, y0 - 0.094, Z), e, 0.024, R_PET + 0.0004, R_PET + 0.0004, ROTULO, lados=10, tampas=False)
    m.cilindro((0, y0 - 0.125, Z), e, 0.012, R_PET, 0.019, PLASTICO, lados=10, tampas="fim")   # o fundo
    m.furo((0, y0 - 0.137, Z), e, 0.0065)
    return (0, y0 - 0.137, Z)


def silenciador_filtro(m, y0, Z, rb):
    """O filtro de óleo (y0: a boca; rb: o raio do que a luva de redução abraça). Devolve a boca nova."""
    e = (0, -1, 0)
    ra = rb + 0.003
    m.cilindro((0, y0 + 0.016, Z), e, 0.026, ra, ra, METAL_MEDIO, lados=6, giro=0)    # a luva de redução
    m.furo((0, y0 + 0.016, Z), (0, 1, 0), rb * 0.7)
    bracadeira(m, y0 + 0.008, Z, anel_pts(ra + 0.0007, Z, 6, 0), angulo=40)
    m.cilindro((0, y0 - 0.010, Z), e, 0.009, 0.0100, 0.0100, METAL, tampas=False)    # o niple
    m.cilindro((0, y0 - 0.019, Z), e, 0.003, 0.0225, 0.0225, BORRACHA, lados=10, tampas="inicio")
    m.cilindro((0, y0 - 0.022, Z), e, 0.004, 0.0260, 0.0260, METAL_CLARO, lados=10, tampas="inicio")
    m.cilindro((0, y0 - 0.026, Z), e, 0.076, R_FILTRO, R_FILTRO, FILTRO, lados=10, tampas="inicio")
    m.cilindro((0, y0 - 0.102, Z), e, 0.009, R_FILTRO, 0.019, FILTRO, lados=10, tampas="fim")
    m.furo((0, y0 - 0.111, Z), e, 0.0065)
    m.cilindro((0, y0 - 0.040, Z), e, 0.044, R_FILTRO + 0.0005, R_FILTRO + 0.0005, ROTULO_FILTRO, lados=10,
               tampas=False)
    c10 = math.cos(math.pi / 10)
    m.mancha(((R_FILTRO + 0.0005) * c10, y0 - 0.060, Z + 0.003), (1, 0, 0), (0.016, 0.009), FERRUGEM, giro=10)
    m.mancha((-R_FILTRO * c10, y0 - 0.093, Z), (-1, 0, 0), (0.012, 0.008), FERRUGEM)
    return (0, y0 - 0.111, Z)


# ------------------------------------------------------------------------------------------------ as miras
def alca(m, y, zb0, z0, ze, zt, meia=0.0105, entalhe=0.003, esp=0.004, bloco=(0.013, 0.016), zp=None):
    """A alça ajustada: o bloco (de zb0 a z0) com o parafuso de ajuste atravessado em X, a chapa com o entalhe (o fundo
    em ze, as pontas em zt) e o ponto claro embaixo do entalhe. Devolve o fundo do entalhe."""
    bx, by = bloco
    m.caixa((0, y, (zb0 + z0) / 2), (2 * bx, by, z0 - zb0), METAL)
    m.caixa((0, y, (z0 + ze) / 2), (2 * meia, esp, ze - z0), METAL_CLARO)
    for s in (1, -1):
        m.caixa((s * (entalhe + meia) / 2, y, (ze + zt) / 2), (meia - entalhe, esp, zt - ze), METAL_CLARO)
    m.placa((0, y + esp / 2, ze - 0.0017), (0, 1, 0), (0.0016, 0.0016), BRANCO)
    zp = (zb0 + z0) / 2 if zp is None else zp
    m.cilindro((bx + 0.003, y, zp), (-1, 0, 0), 2 * bx + 0.006, 0.0013, 0.0013, METAL_CLARO, lados=4)
    m.cilindro((bx, y, zp), (1, 0, 0), 0.0022, 0.0032, 0.0032, METAL_MEDIO, lados=6, tampas="fim")
    m.cilindro((-bx, y, zp), (-1, 0, 0), 0.0024, 0.0030, 0.0030, METAL_MEDIO, lados=6, tampas="fim")
    return (0, y, ze)


def massa(m, y, zb0, zb1, zt, larg=0.0045, fundo=0.010, base=(0.016, 0.014), orelha=0.0065, banda=None):
    """A massa ajustada: a base (de zb0 a zb1), o poste até zt com o ponto claro atrás, as duas orelhas e, se houver,
    a cinta em volta do cano (banda: contorno e largura). Devolve a ponta do poste."""
    m.caixa((0, y, (zb0 + zb1) / 2), (base[0], base[1], zb1 - zb0), METAL)
    m.caixa((0, y, (zb1 + zt) / 2), (larg, fundo, zt - zb1), METAL_CLARO)
    for s in (1, -1):
        m.caixa((s * orelha, y, (zb1 + zt - 0.0015) / 2), (0.0015, fundo, zt - 0.0015 - zb1), METAL_CLARO)
    m.placa((0, y + fundo / 2, zt - 0.0018), (0, 1, 0), (larg * 0.6, 0.0018), BRANCO)
    if banda:
        contorno, largura = banda
        m.cinta(y + largura / 2, largura, contorno, METAL_CLARO)
    return (0, y, zt)


R_PONTO, R_PONTO_DENTRO, L_PONTO = 0.0095, 0.0074, 0.038


def mira_ponto(m, yc, zp, x_dentro, x_fora, z0, zb):
    """A mira de ponto numa ponte de chapa: a chapa de cima (embaixo em zp), as paredes dos lados (de x_dentro a x_fora,
    do pé em z0) com os parafusos em zb, o tubinho, a lente, o ponto, a abraçadeira, a pilha e o fio."""
    topo = zp + 0.003
    m.caixa((0, yc, zp + 0.0015), (2 * x_fora, 0.024, 0.003), METAL_MEDIO)
    for s in (1, -1):
        m.caixa((s * (x_dentro + x_fora) / 2, yc, (z0 + topo) / 2), (x_fora - x_dentro, 0.016, topo - z0), METAL_MEDIO)
        if zb is not None:
            m.parafuso((s * x_fora, yc, zb), (s, 0, 0), r=0.0026, lados=4)
    za = topo + R_PONTO * COS8
    yt, yf = yc + L_PONTO / 2, yc - L_PONTO / 2
    tubo_oco(m, yt, yf, za, R_PONTO, R_PONTO_DENTRO)
    m.furo((0, yf + 0.0025, za), (0, -1, 0), R_PONTO_DENTRO, VIDRO)          # a lente: só vista de frente
    m.placa((0, yf + 0.0030, za), (0, 1, 0), (0.0018, 0.0018), PONTO)        # o ponto: só visto de trás
    contorno = casco(anel_pts(R_PONTO + 0.0006, za) + [(0.0105, zp - 0.0006), (-0.0105, zp - 0.0006)])
    bracadeira(m, yc, za, contorno, angulo=160)   # a caixinha do lado direito, em cima: o lado esquerdo fica limpo
    xb = -(R_PONTO + 0.0035)
    m.caixa((xb, yt - 0.0068, za - 0.0015), (0.007, 0.010, 0.009), FITA_PRATA)
    m.haste([(xb, yt - 0.012, za - 0.004), (xb + 0.0012, yc - 0.008, za - 0.0045), (-0.0072, yf + 0.004, za - 0.0068)],
            0.0007, COBRE, lados=3)
    return {"ponto": (0, yt, za), "frente": (0, yf, za), "ponto_vermelho": (0, yf + 0.003, za),
            "olho": (0, yt + 0.080, za)}


def luneta(m, y_ocular=-0.072, ponte=(-0.172, -0.300), paredes=(-0.181, -0.291), zp=0.1425, x_dentro=0.019,
           x_fora=0.021, z0=0.100, zb=0.1055, za=0.172, torre=-0.236, fita=-0.120):
    """A luneta de cano: a ponte de chapa (a chapa de y ponte[0] a ponte[1], as paredes e os calços nos y das paredes),
    o tubo, a ocular de mangueira (a lente de trás em y_ocular), a objetiva de luva com a lente da frente, a fita, o
    parafuso de ajuste em cima e as duas braçadeiras."""
    R = 0.0135
    topo = zp + 0.003
    m.caixa((0, (ponte[0] + ponte[1]) / 2, zp + 0.0015), (2 * x_fora, ponte[0] - ponte[1], 0.003), METAL_MEDIO)
    fundo = za - R * COS8
    for yw in paredes:
        for s in (1, -1):
            m.caixa((s * (x_dentro + x_fora) / 2, yw, (z0 + topo) / 2), (x_fora - x_dentro, 0.014, topo - z0),
                    METAL_MEDIO)
            if zb is not None:
                m.parafuso((s * x_fora, yw, zb), (s, 0, 0), r=0.0026, lados=4)
        m.caixa((0, yw, (topo + fundo + 0.0006) / 2), (0.012, 0.010, fundo + 0.0006 - topo), METAL)
        contorno = casco(anel_pts(R + 0.0006, za) + [(0.0066, topo + 0.0004), (-0.0066, topo + 0.0004)])
        bracadeira(m, yw, za, contorno, angulo=25, parafuso=False)
    y_ob = y_ocular - 0.260
    m.cilindro((0, y_ocular - 0.016, za), (0, -1, 0), 0.212, R, R, METAL, tampas=False)
    m.cilindro((0, y_ocular, za), (0, -1, 0), 0.018, 0.0165, 0.0165, BORRACHA)             # a ocular de mangueira
    m.cilindro((0, y_ob + 0.036, za), (0, -1, 0), 0.036, 0.0175, 0.0175, METAL_MEDIO)       # a objetiva de luva
    m.furo((0, y_ocular, za), (0, 1, 0), 0.0115, VIDRO)
    m.furo((0, y_ob, za), (0, -1, 0), 0.0148, VIDRO)
    m.cilindro((0, fita, za), (0, -1, 0), 0.014, R + 0.0006, R + 0.0006, FITA_PRATA, tampas=False)
    m.cilindro((0, torre, za + R * COS8 - 0.001), (0, 0, 1), 0.009, 0.0042, 0.0042, METAL_MEDIO, lados=6,
               tampas="fim")
    return {"ponto": (0, y_ocular, za), "frente": (0, y_ob, za), "olho": (0, y_ocular + 0.070, za)}


# ------------------------------------------------------------------------------------------------ o cano reforçado
def reforco(m, Z, luvas, bracadeiras, talas=None, tala_cima=None, aneis=(), extra=(), r_nu=0.0105, furos=False):
    """A luva de cano e o que a prende. luvas: [(y0, y1, r)]; talas: (y0, y1), as dos lados; tala_cima: (y0, y1, z0,
    por_dentro), a de cima (por dentro das braçadeiras ou por cima delas); aneis: os y das voltas de fio de cobre;
    extra: pontos (x, z) a mais no contorno (a barra da pistola); r_nu: o raio do cano onde não há luva."""
    for y0, y1, r in luvas:
        m.cilindro((0, y0, Z), (0, -1, 0), y0 - y1, r, r, GALVANIZADO)
        if furos:
            m.furo((0, y0, Z), (0, 1, 0), r_nu)
            m.furo((0, y1, Z), (0, -1, 0), r_nu)

    def raio(y):
        return next((r for y0, y1, r in luvas if y1 - 1e-6 <= y <= y0 + 1e-6), None)

    xl = max(r for _, _, r in luvas) * COS8 + 0.0015
    if talas:
        for s in (1, -1):
            m.caixa((s * xl, (talas[0] + talas[1]) / 2, Z), (0.003, talas[0] - talas[1], 0.008), FERRUGEM)
    if tala_cima:
        y0, y1, z0, _ = tala_cima
        m.caixa((0, (y0 + y1) / 2, z0 + 0.0015), (0.009, y0 - y1, 0.003), FERRUGEM)

    def contorno(y):
        pts = anel_pts((raio(y) or r_nu) + 0.0007, Z) + list(extra)
        if talas and talas[1] <= y <= talas[0]:
            pts += [(s * (xl + 0.0022), Z + t * 0.0047) for s in (1, -1) for t in (1, -1)]
        if tala_cima and tala_cima[3] and tala_cima[1] <= y <= tala_cima[0]:
            pts += [(s * 0.0052, tala_cima[2] + 0.0037) for s in (1, -1)]
        return casco(pts)

    for y in bracadeiras:
        bracadeira(m, y, Z, contorno(y), angulo=30)
    for y in aneis:
        m.cinta(y + 0.0015, 0.003, contorno(y), COBRE)


def reforco_tabela(centro, nivel, r=0.0136, Z=0.105):
    """Os três níveis do rifle (e dos itens), em volta de um centro em y."""
    zc = Z + r * COS8 + 0.0002
    return {
        1: dict(luvas=[(centro + 0.060, centro - 0.060, r)], bracadeiras=[centro + 0.045, centro - 0.045]),
        2: dict(luvas=[(centro + 0.090, centro - 0.090, r)], talas=(centro + 0.095, centro - 0.095),
                bracadeiras=[centro + 0.075, centro, centro - 0.075]),
        3: dict(luvas=[(centro + 0.120, centro - 0.120, r)], talas=(centro + 0.125, centro - 0.125),
                tala_cima=(centro + 0.115, centro - 0.115, zc, True),
                bracadeiras=[centro + 0.105, centro + 0.035, centro - 0.035, centro - 0.105],
                aneis=[centro + 0.070, centro - 0.070]),
    }[nivel]


BARRA = [(0.0066, 0.0699), (-0.0066, 0.0699)]   # a barra chata debaixo do cano da pistola (dentro das braçadeiras)
REFORCO = {
    "Sucata_Pistola": {
        1: dict(luvas=[(-0.0995, -0.1330, 0.0138)], bracadeiras=[-0.1162]),
        2: dict(luvas=[(-0.0995, -0.1330, 0.0138)], bracadeiras=[-0.108, -0.124],
                tala_cima=(-0.084, -0.1415, 0.1016, False)),
        3: dict(luvas=[(-0.0995, -0.1330, 0.0138), (-0.1432, -0.1555, 0.0168)], bracadeiras=[-0.106, -0.116, -0.126],
                tala_cima=(-0.084, -0.1415, 0.1016, False), aneis=[-0.0815, -0.0855]),
    },
    "Sucata_Submetralhadora": {
        1: dict(luvas=[(-0.194, -0.234, 0.0200)], bracadeiras=[-0.214]),
        2: dict(luvas=[(-0.194, -0.234, 0.0200), (-0.255, -0.2745, 0.0200)], bracadeiras=[-0.204, -0.224, -0.265]),
        3: dict(luvas=[(-0.194, -0.234, 0.0200), (-0.255, -0.2745, 0.0200)], talas=(-0.193, -0.2755),
                tala_cima=(-0.193, -0.2745, 0.100 + 0.0200 * COS8 + 0.0002, True),
                bracadeiras=[-0.204, -0.224, -0.265], aneis=[-0.2315, -0.271]),
    },
    "Sucata_Rifle": {n: reforco_tabela(-0.640, n) for n in (1, 2, 3)},
}
REFORCO_EXTRA = {"Sucata_Pistola": dict(extra=BARRA, r_nu=0.0115), "Sucata_Submetralhadora": dict(r_nu=0.0175),
                 "Sucata_Rifle": dict(r_nu=0.0105)}
Z_CANO = {"Sucata_Pistola": 0.088, "Sucata_Submetralhadora": 0.100, "Sucata_Rifle": 0.105}


# ------------------------------------------------------------------------------------------------ os carregadores
def carregador_longo_pistola(m):
    """O bloco de um tiro com um remendo de tábua parafusado embaixo (mais dois tiros): o de hoje, inteiro, por dentro
    do cabo, e o remendo saindo por baixo, com as chapinhas, a fita e o fundo novo."""
    cy, cz = 0.012, -0.016
    with m.giro((0, cy, cz), (18, 0, 0)):
        m.caixa((0, cy + 0.003, cz - 0.037), (0.018, 0.022, 0.054), MADEIRA_CRUA)
        m.caixa((0, cy + 0.003, cz - 0.045), (0.0186, 0.0226, 0.012), FITA_PRATA)
        m.cilindro((0, cy + 0.003, cz - 0.010), (0, 0, 1), 0.018, 0.0055, 0.0055, COBRE, lados=6)
        m.cilindro((0, cy + 0.003, cz + 0.008), (0, 0, 1), 0.002, 0.0065, 0.0065, METAL_CLARO, lados=6)
        m.caixa((0, cy + 0.003, cz - 0.084), (0.0190, 0.0232, 0.042), MADEIRA)        # o remendo (z -0.105 a -0.063)
        for s in (1, -1):
            m.caixa((s * 0.0105, cy + 0.003, cz - 0.070), (0.002, 0.016, 0.019), METAL)
            for dz in (-0.0665, -0.0745):
                m.parafuso((s * 0.0115, cy + 0.003, cz + dz), (s, 0, 0), r=0.0021, lados=4)
        m.caixa((0, cy + 0.003, cz - 0.089), (0.0198, 0.0240, 0.009), FITA_PRATA)
        m.mancha((0.0095, cy - 0.003, cz - 0.098), (1, 0, 0), (0.008, 0.006), MADEIRA_CRUA)
        m.caixa((0, cy + 0.004, cz - 0.108), (0.024, 0.028, 0.006), FERRUGEM_ESCURA)


def carregador_longo_smg(m, ym=-0.162, inclinacao=-4.0):
    """O tubo de hoje com um pedaço de outra chapa emendado embaixo (7,5 cm a mais), a fita na emenda, os rebites e o
    fundo enferrujado."""
    with m.giro((0, ym, 0.070), (inclinacao, 0, 0)):
        m.caixa((0, ym, -0.024), (0.023, 0.034, 0.188), METAL_MEDIO)
        m.caixa((0, ym, -0.1555), (0.0236, 0.0346, 0.075), METAL)
        m.caixa((0, ym, -0.1215), (0.0252, 0.0362, 0.016), FITA_PRATA)
        for s in (1, -1):
            for d in (0.010, -0.010):
                m.parafuso((s * 0.0118, ym + d, -0.140), (s, 0, 0), r=0.0018, lados=4)
        with m.giro((0, ym, -0.050), (3, 0, 2)):
            m.caixa((0, ym, -0.050), (0.0255, 0.0365, 0.034), FITA_PRATA)
        m.caixa((0, ym, 0.022), (0.0250, 0.0360, 0.012), PRETO)
        m.mancha((0.0118, ym + 0.004, -0.168), (1, 0, 0), (0.020, 0.014), FERRUGEM, giro=8)
        m.caixa((0, ym, -0.1965), (0.027, 0.040, 0.007), FERRUGEM)


# ------------------------------------------------------------------------------------------------ as peças de cada arma
def pecas_da_arma(nome):
    """As peças de cada arma: {sufixo do objeto: função(m) -> dict dos pontos ou None}."""
    Z = Z_CANO[nome]
    rf = {n: (lambda m, n=n: reforco(m, Z, **REFORCO[nome][n], **REFORCO_EXTRA[nome])) for n in (1, 2, 3)}
    if nome == "Sucata_Pistola":
        boca, rb = -0.186, 0.0152

        def mira_ferro(m):
            a = alca(m, -0.044, 0.0945, 0.1015, 0.119, 0.1255, meia=0.0090, entalhe=0.0022, esp=0.003,
                     bloco=(0.008, 0.009), zp=0.098)
            f = massa(m, -0.165, 0.1008, 0.1045, 0.119, larg=0.0030, fundo=0.006, base=(0.013, 0.010), orelha=0.0050)
            return {"ponto": a, "frente": f}

        def ponto(m):
            return mira_ponto(m, -0.066, 0.1215, 0.0177, 0.0197, 0.073, 0.077)
    elif nome == "Sucata_Submetralhadora":
        boca, rb = -0.306, 0.0125

        def mira_ferro(m):
            a = alca(m, 0.060, 0.128, 0.133, 0.1385, 0.1465, meia=0.0105, entalhe=0.0028, esp=0.004,
                     bloco=(0.012, 0.010), zp=0.1305)
            f = massa(m, -0.282, 0.1145, 0.1205, 0.1385, larg=0.0045, fundo=0.010, base=(0.016, 0.012),
                      orelha=0.0065, banda=(anel_pts(0.0175 + 0.0007, Z), 0.008))
            return {"ponto": a, "frente": f}

        def ponto(m):
            return mira_ponto(m, -0.090, 0.142, 0.0212, 0.0232, 0.110, 0.117)
    else:
        boca, rb = -0.892, 0.0195

        def mira_ferro(m):
            a = alca(m, -0.3355, 0.118, 0.1295, 0.139, 0.150, meia=0.0105, entalhe=0.0030, esp=0.004,
                     bloco=(0.013, 0.016), zp=0.1240)
            f = massa(m, -0.801, 0.1150, 0.1210, 0.139, larg=0.0060, fundo=0.012, base=(0.016, 0.014),
                      orelha=0.0070, banda=(anel_pts(0.0125 + 0.0008, Z), 0.016))
            return {"ponto": a, "frente": f}

        def ponto(m):
            return mira_ponto(m, -0.260, 0.1425, 0.0190, 0.0210, 0.100, 0.1055)
    pecas = {
        "acc_SilenciadorPET": lambda m: {"boca": silenciador_pet(m, boca, Z, rb)},
        "acc_SilenciadorFiltro": lambda m: {"boca": silenciador_filtro(m, boca, Z, rb)},
        "acc_MiraFerro": mira_ferro,
        "acc_MiraPonto": ponto,
    }
    if nome == "Sucata_Rifle":
        pecas["acc_Luneta"] = luneta
    for n in (1, 2, 3):
        pecas[f"acc_CanoReforcado{n}"] = rf[n]
    if nome == "Sucata_Pistola":
        pecas["carregadorLongo"] = carregador_longo_pistola
    elif nome == "Sucata_Submetralhadora":
        pecas["carregadorLongo"] = carregador_longo_smg
    return pecas


# ------------------------------------------------------------------------------------------------ os itens soltos
def _item_pet(m):
    with m.giro((0, 0, R_PET), (0, 18, 0)):   # deitada numa face, não numa quina
        silenciador_pet(m, 0.0, R_PET, 0.0115)


def _item_filtro(m):
    with m.giro((0, 0, R_FILTRO), (0, 18, 0)):
        silenciador_filtro(m, 0.0, R_FILTRO, 0.0115)


def _item_mira_ferro(m):
    m.caixa((0, 0, 0.001), (0.036, 0.052, 0.002), FERRUGEM)    # a tira de chapa de onde saíram
    alca(m, 0.012, 0.002, 0.0135, 0.023, 0.034)
    massa(m, -0.012, 0.002, 0.007, 0.025, larg=0.0060, fundo=0.012, base=(0.016, 0.014), orelha=0.0070)


def _item_mira_ponto(m):
    mira_ponto(m, 0.0, 0.030, 0.0190, 0.0210, 0.0, None)


def _item_luneta(m):
    luneta(m, y_ocular=0.130, ponte=(0.030, -0.098), paredes=(0.021, -0.089), zp=0.026, z0=0.0, zb=0.0065,
           za=0.0555, torre=-0.034, fita=0.082)


def _item_carregador(m):
    with m.giro((0, 0, 0), (0, 90, 90)):   # deitado na face larga, comprido em Y
        carregador_longo_smg(m, ym=0.0, inclinacao=0.0)


def _item_reforco(nivel):
    r = 0.0136
    Z = r * COS8   # a face de baixo da luva no chão

    def f(m):
        reforco(m, Z, **reforco_tabela(0.0, nivel, r=r, Z=Z), r_nu=0.0105, furos=True)
    return f


ITENS = {
    "Item_SilenciadorPET": _item_pet, "Item_SilenciadorFiltro": _item_filtro, "Item_MiraFerro": _item_mira_ferro,
    "Item_MiraPonto": _item_mira_ponto, "Item_Luneta": _item_luneta, "Item_CarregadorLongo": _item_carregador,
    "Item_CanoReforcado1": _item_reforco(1), "Item_CanoReforcado2": _item_reforco(2),
    "Item_CanoReforcado3": _item_reforco(3),
}


# ------------------------------------------------------------------------------------------------ os objetos
def _fechar(m):
    """O fim do sucata.Malha.objeto: os normais das peças fechadas, as faces de mais de 4 lados em triângulos e sem os
    grupos de vértices. Devolve o bmesh."""
    bm = m.bm
    bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f not in m.abertas])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], quad_method="BEAUTY",
                          ngon_method="EAR_CLIP")
    if bm.verts.layers.deform.active:
        bm.verts.layers.deform.remove(bm.verts.layers.deform.active)
    return bm


def _malha_objeto(nome, bm, mat, colecao):
    me = bpy.data.meshes.new(nome)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    me.transform(GIRO)   # a malha nos eixos da Unity; o objeto girado 90° em X (o armas.exportar())
    me.materials.append(mat)
    o = bpy.data.objects.new(nome, me)
    o.rotation_euler = (math.radians(90), 0, 0)
    colecao.objects.link(o)
    return o


def triangulos(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def construir_acessorios(mat, colecao):
    """As peças nas armas. Devolve os objetos ({arma: {sufixo: objeto}}) e os pontos de cada uma (no Blender)."""
    objs, pontos = {}, {}
    for nome in DY:
        objs[nome], pontos[nome] = {}, {}
        for sufixo, func in pecas_da_arma(nome).items():
            m = sucata.Malha()
            p = func(m)
            bm = _fechar(m)
            bm.transform(Matrix.Translation((0, DY[nome], 0)))
            objs[nome][sufixo] = _malha_objeto(f"{nome}_{sufixo}", bm, mat, colecao)
            if p:
                pontos[nome][sufixo] = p
    return objs, pontos


def construir_itens(mat, colecao):
    objs = {}
    for i, (nome, func) in enumerate(ITENS.items()):
        m = sucata.Malha()
        func(m)
        bm = _fechar(m)
        vs = [v.co for v in bm.verts]
        meio = Vector(((min(v.x for v in vs) + max(v.x for v in vs)) / 2, (min(v.y for v in vs) + max(v.y for v in vs)) / 2,
                       min(v.z for v in vs)))
        bmesh.ops.translate(bm, vec=-meio, verts=bm.verts)
        o = _malha_objeto(nome + "_corpo", bm, mat, colecao)
        o.location = (i * 0.4, 0, 0)
        objs[nome] = o
    return objs


def para_unity(p, dy):
    """Um ponto do Blender (o espaço do sucata.py, antes do dy) na malha da Unity (x, z, -y)."""
    return [round(p[0], 4), round(p[2], 4), round(-(p[1] + dy), 4)]


def relatorio(acc, itens):
    print("=" * 30, "as peças nas armas (triângulos, peças do avesso, tamanho em m)")
    for nome, pecas in acc.items():
        for suf, o in pecas.items():
            vs = [v.co for v in o.data.vertices]
            tam = [max(v[k] for v in vs) - min(v[k] for v in vs) for k in range(3)]
            print(f"  {o.name:44s} {triangulos(o):4d} tri | avesso {sucata.invertidas(o)} | "
                  f"{tam[0]:.3f} x {tam[1]:.3f} x {tam[2]:.3f}")
    print("=" * 30, "os itens")
    for nome, o in itens.items():
        vs = [v.co for v in o.data.vertices]
        tam = [max(v[k] for v in vs) - min(v[k] for v in vs) for k in range(3)]
        print(f"  {o.name:44s} {triangulos(o):4d} tri | avesso {sucata.invertidas(o)} | "
              f"{tam[0]:.3f} x {tam[1]:.3f} x {tam[2]:.3f} | min y (o chão) {min(v.y for v in vs):+.4f}")


def gravar_json(pontos, caminho):
    miras, bocas = [], []
    for nome, pecas in pontos.items():
        dy = DY[nome]
        for suf, p in pecas.items():
            peca = suf.replace("acc_", "")
            if "boca" in p:
                bocas.append({"arma": nome, "peca": peca, "objeto": f"{nome}_{suf}", "boca": para_unity(p["boca"], dy)})
                continue
            d = {"arma": nome, "peca": peca, "objeto": f"{nome}_{suf}"}
            d.update({k: para_unity(v, dy) for k, v in p.items()})
            d["eixo"] = [0.0, 0.0, 1.0]
            miras.append(d)
    dados = {
        "espaco": "a malha da Unity de cada Sucata_<Arma> (a origem no cabo, o cano em +Z, o alto em +Y, em metros), a "
                  "mesma das peças do Sucata_Acessorios.fbx",
        "miras": miras,
        "como_ler": {
            "MiraPonto e Luneta": "ponto: o meio da ponta de trás (o tubinho ou a lente da ocular), onde o olho alinha; "
                                  "frente: o meio da ponta da frente; eixo: a direção da mira (o olho em qualquer ponto "
                                  "do eixo, atrás do ponto, vê pelo meio); olho: um lugar sugerido para a câmera (8 cm "
                                  "atrás na de ponto, 7 cm de alívio na luneta); ponto_vermelho: o pontinho.",
            "MiraFerro": "ponto: o fundo do entalhe da alça; frente: a ponta da massa (a linha da mira de ferro "
                         "ajustada, na mesma altura).",
        },
        "bocas": bocas,
    }
    with open(caminho, "w", encoding="utf-8") as f:
        json.dump(dados, f, ensure_ascii=False, indent=2)
    print("  gravado:", caminho)


def exportar_fbx(objs, caminho, suave):
    for o in bpy.context.scene.objects:
        o.select_set(o in objs)
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type=suave,
                             use_tspace=False, path_mode="STRIP")
    print("  gravado:", caminho, "|", len(objs), "objetos")


# ------------------------------------------------------------------------------------------------ conferir
def conferir():
    """Compara as armas refeitas (sx.pecas) com os Sucata_<Arma>.fbx do jogo (só lidos) e mede a ocular da luneta da
    Sniper3Leve (a que o rifle de sucata substitui), para comparar com a luneta de cano."""
    cena = bpy.context.scene
    for o in list(cena.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for nome in DY:
        antes = set(cena.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(sx.MODELOS, nome + ".fbx"))
        bpy.context.view_layer.update()
        novos = [o for o in cena.objects if o not in antes and o.type == "MESH"]
        for o in novos:
            vs = [o.matrix_world @ v.co for v in o.data.vertices]
            mn = [min(v[k] for v in vs) for k in range(3)]
            mx = [max(v[k] for v in vs) for k in range(3)]
            print(f"  FBX {o.name:32s} min {mn[0]:+.4f} {mn[1]:+.4f} {mn[2]:+.4f} max {mx[0]:+.4f} {mx[1]:+.4f} "
                  f"{mx[2]:+.4f}")
        for peca, b in sx.pecas(nome, FUNC[nome], DY[nome]).items():
            vs = [v.co for v in b.verts]
            mn = [min(v[k] for v in vs) for k in range(3)]
            mx = [max(v[k] for v in vs) for k in range(3)]
            print(f"  refeito {nome + '_' + peca:28s} min {mn[0]:+.4f} {mn[1]:+.4f} {mn[2]:+.4f} max {mx[0]:+.4f} "
                  f"{mx[1]:+.4f} {mx[2]:+.4f}")
            b.free()
        for o in list(cena.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    antes = set(cena.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(sx.MODELOS, "Sniper3Leve.fbx"))
    bpy.context.view_layer.update()
    vs = [o.matrix_world @ v.co for o in cena.objects if o not in antes and o.type == "MESH" for v in o.data.vertices]
    alto = [v for v in vs if v.z > 0.150]
    if alto:
        print(f"  Sniper3Leve: a luneta (z > 0,150) vai de y {min(v.y for v in alto):+.4f} a {max(v.y for v in alto):+.4f}"
              f", z até {max(v.z for v in alto):.4f} (no Blender; o eixo no v3_mosin.py é z 0,172)")


# ------------------------------------------------------------------------------------------------ as fotos
LARG, ALT = 400, 250
VISTAS = [("3/4", (-1.0, -0.85, 0.65)), ("lado", (1.0, 0.0, 0.0)), ("mira", (0.0, 1.0, 0.0))]


def configs(nome):
    """As fotos de cada arma: (título, peças)."""
    c = [("sem nada", []), ("silenciador PET", ["acc_SilenciadorPET"]),
         ("silenciador de filtro", ["acc_SilenciadorFiltro"]), ("mira de ferro ajustada", ["acc_MiraFerro"]),
         ("mira de ponto", ["acc_MiraPonto"])]
    if nome == "Sucata_Rifle":
        c.append(("luneta", ["acc_Luneta"]))
    else:
        c.append(("carregador estendido", ["carregadorLongo"]))
    c += [(f"cano reforçado {r}", [f"acc_CanoReforcado{n}"]) for n, r in ((1, "I"), (2, "II"), (3, "III"))]
    if nome == "Sucata_Rifle":
        c.append(("todas: filtro, ferro, luneta, cano III",
                  ["acc_SilenciadorFiltro", "acc_MiraFerro", "acc_Luneta", "acc_CanoReforcado3"]))
        c.append(("todas: PET, ferro, ponto, cano III",
                  ["acc_SilenciadorPET", "acc_MiraFerro", "acc_MiraPonto", "acc_CanoReforcado3"]))
    else:
        c.append(("todas: filtro, ferro, ponto, carregador, cano III",
                  ["acc_SilenciadorFiltro", "acc_MiraFerro", "acc_MiraPonto", "carregadorLongo",
                   "acc_CanoReforcado3"]))
    return c


def _imagem_lisa(nome, cor):
    img = bpy.data.images.new(nome, 1, 1)
    img.pixels = list(cor) + [1.0]
    mat = bpy.data.materials.new(nome)
    mat.use_nodes = True
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def preparar_cena():
    cena = bpy.context.scene
    for eng in ("BLENDER_WORKBENCH", "WORKBENCH"):
        try:
            cena.render.engine = eng
            break
        except TypeError:
            pass
    r = cena.render
    r.resolution_x, r.resolution_y, r.resolution_percentage = LARG, ALT, 100
    r.image_settings.file_format = "PNG"
    r.film_transparent = False
    sh = cena.display.shading
    sh.light = "STUDIO"
    sh.color_type = "TEXTURE"
    sh.show_backface_culling = True   # como na Unity: uma face de um lado só some de trás
    sh.show_cavity = True
    sh.show_object_outline = True
    try:
        cena.display.render_aa = "8"
    except TypeError:
        pass
    cena.view_settings.view_transform = "Standard"
    mundo = bpy.data.worlds.new("Fundo")
    mundo.color = (0.50, 0.52, 0.55)
    cena.world = mundo
    cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    cam.data.type = "ORTHO"
    cam.data.clip_start, cam.data.clip_end = 0.01, 20.0
    cena.collection.objects.link(cam)
    cena.camera = cam
    cur = bpy.data.curves.new("Rotulo", "FONT")
    txt = bpy.data.objects.new("Rotulo", cur)
    cur.materials.append(_imagem_lisa("Rotulo_preto", (0.02, 0.02, 0.02)))
    cena.collection.objects.link(txt)
    txt.parent = cam
    return cena, cam, txt


def enquadrar(cam, txt, objs, direcao, rotulo, filtro=None, margem=1.12):
    d = Vector(direcao).normalized()
    q = (-d).to_track_quat("-Z", "Y")
    R = q.to_matrix()
    Ri = R.transposed()
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    if filtro:
        pts = [p for p in pts if filtro(p)] or pts
    loc = [Ri @ p for p in pts]
    x0, x1 = min(p.x for p in loc), max(p.x for p in loc)
    y0, y1 = min(p.y for p in loc), max(p.y for p in loc)
    asp = LARG / ALT
    escala = max(x1 - x0, (y1 - y0 + 0.004) * asp / 0.84) * margem   # a faixa de baixo é do rótulo
    cam.data.ortho_scale = escala
    cam.rotation_euler = q.to_euler()
    meio_y = (y0 + y1) / 2 - escala / asp * 0.07
    cam.location = R @ Vector(((x0 + x1) / 2, meio_y, max(p.z for p in loc) + 3.0))
    txt.data.body = rotulo
    txt.data.size = escala * 0.045
    txt.location = (-escala / 2 * 0.97, -escala / asp / 2 * 0.90, -0.5)


def render(caminho):
    cena = bpy.context.scene
    cena.render.filepath = caminho
    bpy.ops.render.render(write_still=True)


def corpos_das_armas(mat, colecao):
    """As peças das armas de hoje (refeitas como o sucata_exportar.py, já com o dy), no espaço do Blender."""
    corpos = {}
    for nome in DY:
        corpos[nome] = {}
        for peca, b in sx.pecas(nome, FUNC[nome], DY[nome]).items():
            me = bpy.data.meshes.new(f"{nome}_{peca}")
            b.to_mesh(me)
            b.free()
            for p in me.polygons:
                p.use_smooth = False
            me.materials.append(mat)
            o = bpy.data.objects.new(me.name, me)
            colecao.objects.link(o)
            corpos[nome][peca] = o
    return corpos


def fotos(acc, itens, mat, pasta):
    import numpy as np
    cena, cam, txt = preparar_cena()
    corpos = corpos_das_armas(mat, cena.collection)
    todos = [o for d in acc.values() for o in d.values()] + list(itens.values()) + \
        [o for d in corpos.values() for o in d.values()]
    pasta_fotos = os.path.join(pasta, "fotos")
    os.makedirs(pasta_fotos, exist_ok=True)
    blocos = {}
    for nome in DY:
        linhas = []
        for i, (titulo, sufixos) in enumerate(configs(nome)):
            vis = [o for p, o in corpos[nome].items() if not (p == "carregador" and "carregadorLongo" in sufixos)]
            vis += [acc[nome][s] for s in sufixos]
            for o in todos:
                o.hide_render = o not in vis
            bpy.context.view_layer.update()
            fila = []
            for vista, direcao in VISTAS:
                filtro = (lambda p: p.z > Z_CANO[nome] - 0.03) if vista == "mira" else None
                enquadrar(cam, txt, vis, direcao, f"{NOMES_CURTOS[nome]} · {titulo} · {vista}", filtro)
                arq = os.path.join(pasta_fotos, f"{NOMES_CURTOS[nome]}_{i:02d}_{vista.replace('/', '')}.png")
                render(arq)
                fila.append(arq)
            linhas.append(fila)
        blocos[nome] = linhas
    fila_itens = []
    for nome, o in itens.items():
        for x in todos:
            x.hide_render = x is not o
        bpy.context.view_layer.update()
        for vista, direcao in (("3/4", (-1.0, -0.9, 0.85)), ("lado", (1.0, 0.0, 0.25))):
            enquadrar(cam, txt, [o], direcao, f"{nome} · {vista}", margem=1.25)
            arq = os.path.join(pasta_fotos, f"{nome}_{vista.replace('/', '')}.png")
            render(arq)
            fila_itens.append(arq)

    # a folha: as três armas lado a lado (uma linha por foto, as três vistas), e os itens embaixo
    def ler(arq):
        img = bpy.data.images.load(arq)
        a = np.empty(LARG * ALT * 4, dtype=np.float32)
        img.pixels.foreach_get(a)
        bpy.data.images.remove(img)
        return np.flipud(a.reshape(ALT, LARG, 4))

    n_lin = max(len(v) for v in blocos.values())
    por_linha_itens = 3 * len(VISTAS)
    lin_itens = math.ceil(len(fila_itens) / por_linha_itens)
    W, H = 3 * len(VISTAS) * LARG, (n_lin + lin_itens) * ALT
    folha = np.full((H, W, 4), 0.85, dtype=np.float32)
    folha[..., 3] = 1.0
    for b, nome in enumerate(DY):
        for li, fila in enumerate(blocos[nome]):
            for ci, arq in enumerate(fila):
                x = (b * len(VISTAS) + ci) * LARG
                folha[li * ALT:(li + 1) * ALT, x:x + LARG] = ler(arq)
    for k, arq in enumerate(fila_itens):
        li, ci = n_lin + k // por_linha_itens, k % por_linha_itens
        folha[li * ALT:(li + 1) * ALT, ci * LARG:(ci + 1) * LARG] = ler(arq)

    def gravar(arr, caminho):
        h, w = arr.shape[:2]
        img = bpy.data.images.new(os.path.basename(caminho), w, h, alpha=False)
        img.pixels.foreach_set(np.flipud(arr).ravel())
        img.filepath_raw = caminho
        img.file_format = "PNG"
        img.save()
        bpy.data.images.remove(img)
        print("  gravado:", caminho, f"({w} x {h})")

    gravar(folha, os.path.join(pasta, "Folha_Acessorios.png"))
    # as prévias reduzidas (metade), uma por arma e a dos itens, para conferir
    previas = os.path.join(os.path.dirname(pasta.rstrip("\\/")), "etapa2_previas")
    os.makedirs(previas, exist_ok=True)
    for b, nome in enumerate(DY):
        x0 = b * len(VISTAS) * LARG
        bloco = folha[:n_lin * ALT, x0:x0 + len(VISTAS) * LARG]
        gravar(bloco[::2, ::2].copy(), os.path.join(previas, f"Previa_{NOMES_CURTOS[nome]}.png"))
    gravar(folha[n_lin * ALT:, :][::2, ::2].copy(), os.path.join(previas, "Previa_Itens.png"))


# ------------------------------------------------------------------------------------------------ tudo
def _args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    etapa, saida, i = "tudo", None, 0
    while i < len(a):
        if a[i] == "--saida":
            saida, i = a[i + 1], i + 2
            continue
        if a[i] == "--assets":
            saida, i = "ASSETS", i + 1
            continue
        etapa, i = a[i], i + 1
    return etapa, saida


def principal():
    etapa, saida = _args()
    if etapa == "conferir":
        conferir()
        return
    if saida is None:
        raise SystemExit("diga onde gravar: --saida <pasta> ou --assets")
    if saida == "ASSETS":
        p_acc, p_loot, p_json = sx.MODELOS, sucata.JOGADOR, sx.MODELOS
        p_fotos = os.path.join(AQUI, "fotos_acessorios")
    else:
        p_acc = p_loot = p_json = p_fotos = saida
    for p in {p_acc, p_loot, p_json, p_fotos}:
        os.makedirs(p, exist_ok=True)
    cena = bpy.context.scene
    for o in list(cena.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    mat = sx._material("Pistola")
    acc, pontos = construir_acessorios(mat, cena.collection)
    itens = construir_itens(mat, cena.collection)
    bpy.context.view_layer.update()
    relatorio(acc, itens)
    if etapa in ("modelos", "tudo"):
        exportar_fbx([o for d in acc.values() for o in d.values()], os.path.join(p_acc, "Sucata_Acessorios.fbx"),
                     "OFF")
        exportar_fbx(list(itens.values()), os.path.join(p_loot, "Loot_Acessorios.fbx"), "FACE")
        gravar_json(pontos, os.path.join(p_json, "Sucata_Acessorios_Miras.json"))
    if etapa in ("fotos", "tudo"):
        fotos(acc, itens, mat, p_fotos)


if __name__ == "__main__":
    principal()
