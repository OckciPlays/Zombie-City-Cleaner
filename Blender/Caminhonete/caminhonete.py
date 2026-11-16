# A caminhonete (15/11/2026): o primeiro carro do Zombie City Cleaner, o da família no começo da partida. Uma picape
# grande de cabine simples dos anos 90 (inspirada nas fotos do usuário; sem marca, nome nem emblema em lugar nenhum),
# verde-escura metálica com dois frisos dourados dos lados, a moldura da grade e os para-choques cromados, rodas de liga
# de 6 raios. A caçamba é aberta (sem a lona), com o estepe de pé no meio, logo atrás da cabine, e o macaco deitado no
# piso, perto da tampa. Dois lugares na cabine (bancos separados de tecido cinza) e dois na caçamba (sentados no piso,
# encostados na frente dela, um de cada lado do estepe, olhando para trás).
#
# O mesmo caminho do cupê (Zombie-City-Cleaner/Blender/Cupe/cupe.py): um modelo "alto" (detalhado) e um "leve" (~12
# mil triângulos), e a cor, o relevo, a oclusão, o metal e a aspereza do alto projetados no leve. Reaproveita a Malha,
# os materiais de autoria e o painel da perua nova (perua_nova.py) e a MalhaLeve da v2 (perua_nova2.py), sem mudar
# nenhum dos dois: as cores e os ajustes daqui valem só neste processo.
#
# Eixos da Unity: x para a direita, y para cima, z para a frente; o chão em y = 0 e o meio dos eixos em z = 0. Cada peça
# vira um objeto girado 90° em X com a malha espelhada em X (como o carro.py), e a origem de cada uma é o pivô dela.
#
# Roda num Blender em segundo plano (rodar.py): construir, assar <canal> <atlas>, compor_exportar.

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = r"D:\Zombiegame\Zombie-City-Cleaner"
PASTA_BLENDER = os.path.join(REPO, "Blender")
for p in (PASTA_BLENDER, os.path.join(PASTA_BLENDER, "PeruaNova"), os.path.join(PASTA_BLENDER, "PeruaNova2")):
    if p not in sys.path:
        sys.path.append(p)
import perua_nova as v1   # noqa: E402  (a Malha, os materiais de autoria, o painel)
import perua_nova2 as v2  # noqa: E402  (a MalhaLeve)

VERSAO = "v1"            # o rodar.py troca (as versões ficam lado a lado: nunca se apaga uma)
TAMANHO = 2048


def destino():
    return os.path.join(AQUI, "export", VERSAO)


def texturas():
    return os.path.join(destino(), "Texturas")


def blend():
    return os.path.join(AQUI, f"Caminhonete_{VERSAO}.blend")


# ---------------------------------------------------------------------------------------------------- medidas (Unity)
W = 0.97                 # meia largura por fora (os para-lamas, as portas, a caçamba)
EIXO = 1.49              # os eixos em +-EIXO (entre-eixos de 2,98 m)
R_RODA = 0.385           # o pneu 245/75 aro 16
X_RODA = 0.83
MEIA_LARGURA = 0.1225
RZ_ARCO, RY_ARCO = 0.48, 0.45    # o recorte da roda: meio comprimento e altura acima de Y_BASE (um arco achatado)
Y_BASE = 0.44            # embaixo da lataria (os para-lamas, as soleiras, a caçamba)
Y_PORTA = 0.47           # a borda de baixo das portas (a soleira por baixo)
Y_VINCO = 1.10           # o vinco de lado a lado (na altura da maçaneta)
Y_FAIXA = 0.83           # os frisos dourados
Y_CINTURA = 1.31         # a linha das janelas
Y_CACAMBA = 1.38         # o alto das laterais da caçamba
Y_TETO = 1.87
Z_PORTA = 0.95           # a dobradiça da porta
Z_FIM_PORTA = -0.37      # a borda de trás da porta
Z_CABINE = -0.48         # a traseira da cabine, por fora
PARABRISA = ((0.965, 1.36), (0.44, 1.845))   # (z, y) de baixo e de cima
Z_CAPO = 1.06            # a dobradiça do capô (atrás, perto do para-brisa)
X_CAPO = 0.80
Y_COFRE = 0.80           # o fundo do cofre do motor
Z_FRENTE_COFRE = 2.10    # a parede da frente do cofre (o suporte do radiador)
Z_GRADE = 2.20           # a frente da grade e dos faróis
Y_GRADE_TOPO = 1.06      # o alto da moldura cromada da grade (logo embaixo da borda do capô)
Z_CACAMBA = -0.52        # a frente da caçamba, por fora
Z_TRASEIRA = -2.52       # a tampa da caçamba, por fora
X_DENTRO = 0.85          # as paredes de dentro da caçamba
Y_PISO_CACAMBA = 0.84
PIVO_TAMPA = (0.0, 0.83, -2.495)   # a dobradiça da tampa (embaixo; ela abre para trás, deitando)
TOMBO = 0.15             # a cabine estreita acima da cintura: x' = x - s * TOMBO * (y - Y_CINTURA)
Y_TOPO_JANELA = 1.80     # o alto do vidro das portas
# o lugar do motorista: o painel, a coluna, os pedais e os bancos são os da perua (perua_nova.py) DY mais altos e DZ
# para trás (o volante da perua fica em (-0,37; 0,86; 0,50); aqui, em (-0,37; 1,28; 0,46))
DY, DZ = 0.42, -0.04
Y_PISO = 0.70            # o assoalho da cabine (o carpete)

# os lugares (o quadril de quem senta) e o resto que a montagem da Unity usa (para conferência)
LUGARES = [(-0.37, 0.58 + DY, 0.06 + DZ), (0.37, 0.58 + DY, 0.06 + DZ),
           (-0.52, Y_PISO_CACAMBA + 0.11, Z_CACAMBA - 0.20), (-0.02, Y_PISO_CACAMBA + 0.11, Z_CACAMBA - 0.20)]
# o estepe (v3, a pedido: "seria mais interessante ser mais lateral e não central, encostado em um dos lados"): de pé, o
# eixo em X, do lado direito, ao lado da caixa da roda, logo atrás da cabine, inclinado TOMBO_ESTEPE graus com o alto
# encostado na beirada da caixa da roda. Os dois da caçamba sentam do lado esquerdo dele, encostados na frente
TOMBO_ESTEPE = 6.0
ESTEPE = (0.47, Y_PISO_CACAMBA + R_RODA * math.cos(math.radians(TOMBO_ESTEPE)) + MEIA_LARGURA * math.sin(math.radians(TOMBO_ESTEPE)) + 0.004,
          Z_CACAMBA - 0.06 - R_RODA - 0.02)
MACACO = (0.45, Y_PISO_CACAMBA, -2.20)


def _y_capo(z):
    """A altura de cima do capô (e dos para-lamas) em z: 1,275 na dobradiça, caindo até 1,10 na frente."""
    return 1.275 - (z - Z_CAPO) * 0.155


def _tombo(s):
    return Matrix(((1, -s * TOMBO, 0, s * TOMBO * Y_CINTURA), (0, 1, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))


def _x_tombo(y):
    """A meia largura da cabine na altura y (acima da cintura ela estreita)."""
    return W - TOMBO * max(0.0, y - Y_CINTURA)


def _arco(zc, para_tras, n=14, folga=0.0):
    """O recorte da roda em volta do eixo zc, achatado em cima (um superelipse): de Y_BASE de um lado, por cima, até
    Y_BASE do outro, andando para trás ou para a frente."""
    pts = []
    for i in range(n + 1):
        t = math.pi * i / n if para_tras else math.pi * (1 - i / n)
        c, s = math.cos(t), math.sin(t)
        cz = math.copysign(abs(c) ** 0.8, c)
        sy = abs(s) ** 0.75
        pts.append((zc + (RZ_ARCO + folga) * cz, Y_BASE + (RY_ARCO + folga) * sy))
    return pts


def _placa(m, sup, pts, esp):
    """Uma chapa fina num polígono 3D (os pontos em volta, num plano), com a espessura para o lado da normal."""
    pts = [Vector(p) for p in pts]
    n = (pts[1] - pts[0]).cross(pts[2] - pts[0]).normalized()
    m._puxar([tuple(p) for p in pts], [tuple(p + n * esp) for p in pts])
    return m._fim(sup)


# ---------------------------------------------------------------------------------------------------- as cores
# as superfícies novas (entram no catálogo da v1 só neste processo)
NOVAS = {
    # nome: (atlas, material final, (metal, aspereza, sujeira, ferrugem, desgaste), cor sRGB)
    "faixa": ("A", "Perua_Lataria", (0.35, 0.40, 0.4, 0.0, 0.2), (182, 146, 72)),      # os frisos dourados
    "liga": ("A", "Perua_Lataria", (0.55, 0.30, 0.25, 0.0, 0.0), (214, 216, 218)),     # as rodas de liga (v2: mais claras)
    "revest": ("A", "Perua_Lataria", (0.0, 0.86, 0.7, 0.0, 0.0), (36, 36, 37)),        # o revestimento da caçamba
    "tecido": ("B", "Perua_Interior", (0.0, 0.95, 0.3, 0.0, 0.0), (150, 151, 150)),    # o tecido dos bancos
}
CORES_CAM = {
    "pintura": (20, 74, 54), "primer": (34, 86, 64), "sujeira": (92, 82, 66), "lama": (70, 60, 46),
    "cromo": (225, 226, 224), "plastico": (24, 24, 25), "grade": (12, 12, 13), "escuro": (22, 22, 22),
    "vinil": (138, 140, 142), "painel": (126, 128, 131), "carpete": (74, 76, 78), "forro": (172, 172, 166),
    "int_plastico": (92, 94, 97), "cinto": (52, 54, 56), "tapete": (30, 30, 30), "alto_falante": (24, 24, 24),
    "int_cromo": (196, 196, 194), "pneu": (20, 20, 20),
}
# a picape está quase nova ("estado de zero km" nas fotos): pouca sujeira, sem ferrugem na pintura, poucas lascas
PROPS_CAM = {
    "pintura": (0.12, 0.30, 0.30, 0.0, 0.10), "cromo": (0.78, 0.20, 0.35, 0.04, 0.0), "aco": (0.85, 0.5, 0.6, 0.35, 0.0),
    "escuro": (0.0, 0.9, 0.7, 0.15, 0.0), "plastico": (0.0, 0.70, 0.45, 0.0, 0.0), "borracha": (0.0, 0.88, 0.5, 0.0, 0.0),
    "grade": (0.0, 0.7, 0.45, 0.05, 0.0), "vinil": (0.0, 0.55, 0.25, 0.0, 0.1), "painel": (0.0, 0.62, 0.2, 0.0, 0.1),
    "carpete": (0.0, 1.0, 0.5, 0.0, 0.0), "pneu": (0.0, 0.92, 0.8, 0.0, 0.0),
}
# as cores fixas dos ramos dos materiais da v1 (o desbotado da pintura mostarda, o vinil turquesa...) trocadas pelas da
# caminhonete: o verde desbota para um verde mais claro; o interior fica cinza
TROCA_DE_CORES = {
    (206, 176, 112): (40, 104, 78), (150, 112, 44): (12, 52, 38),
    (90, 158, 160): (150, 152, 154), (40, 100, 108): (114, 116, 118), (140, 140, 125): (146, 146, 142), (90, 150, 150): (142, 144, 146),
    (55, 80, 82): (90, 92, 94), (60, 90, 98): (76, 78, 80), (110, 170, 170): (150, 152, 154), (140, 120, 90): (150, 148, 140),
}
# superfícies trocadas na hora de fazer a malha (o painel da perua tem um friso de madeira)
TROCA_DE_SUPERFICIE = {"madeira_int": "int_plastico"}


def preparar_catalogo():
    for nome, (atlas, final, props, cor) in NOVAS.items():
        v1.SUPERFICIES[nome] = (atlas, final)
        v1.PROPRIEDADES[nome] = props
        v1.CORES[nome] = cor
    v1.CORES.update(CORES_CAM)
    v1.PROPRIEDADES.update(PROPS_CAM)
    v2.SO_TEXTURA.add("faixa")
    original = v1._lin
    if not getattr(original, "_cam", False):
        def lin(r, g, b):
            return original(*TROCA_DE_CORES.get((r, g, b), (r, g, b)))
        lin._cam = True
        v1._lin = lin
    v1.VERSAO_DOS_MATERIAIS = 400


def ajustar_materiais():
    """Os materiais da v1 têm números da perua: o pneu dela tem 0,30 de raio (a banda de rodagem começa em 0,29) e as
    placas ficam em 0,37 (a da frente) e 0,74 (a de trás). Aqui, os da caminhonete."""
    def trocar(nome, tipo, campo, de, para):
        mat = bpy.data.materials.get(nome)
        if mat is None:
            return 0
        n = 0
        for no in mat.node_tree.nodes:
            if no.bl_idname != tipo:
                continue
            if tipo == "ShaderNodeMapRange":
                e = no.inputs[campo]
                if abs(e.default_value - de) < 1e-6:
                    e.default_value = para
                    n += 1
            elif no.operation == "SUBTRACT" and no.inputs[0].links and abs(no.inputs[1].default_value - de) < 1e-6:
                no.inputs[1].default_value = para
                n += 1
        return n
    k = R_RODA / 0.30
    trocar("Aut_pneu", "ShaderNodeMapRange", "From Min", 0.29, R_RODA - 0.008)
    trocar("Aut_pneu", "ShaderNodeMapRange", "From Max", 0.297, R_RODA - 0.002)
    trocar("Aut_placa_f", "ShaderNodeMath", 1, 0.37, Y_PLACA_F)
    trocar("Aut_placa_t", "ShaderNodeMath", 1, 0.74, Y_PLACA_T)
    return k


Y_PLACA_F, Y_PLACA_T = 0.515, 0.60


class Alta(v1.Malha):
    def _idx(self, sup):
        return super()._idx(TROCA_DE_SUPERFICIE.get(sup, sup))


class Leve(v2.MalhaLeve):
    def _idx(self, sup):
        return super()._idx(TROCA_DE_SUPERFICIE.get(sup, sup))


ALTA = True   # a versão sendo feita (alta: os detalhes; leve: o que fica na malha)


# ---------------------------------------------------------------------------------------------------- a carroceria
def _carroceria(cena, M, nome):
    m = M()
    _frente_e_cofre(m)
    _cabine(m)
    _cacamba(m)
    _de_baixo(m)
    _lados(m)
    _interior(m)
    return m.objeto(nome, cena)


def _frente_e_cofre(m):
    # ======================================================== os para-lamas da frente (com o vão das luzes de canto)
    fr = [(Z_PORTA, Y_BASE), (Z_PORTA, 1.30), (Z_CAPO, _y_capo(Z_CAPO)), (2.15, _y_capo(2.15)), (2.19, _y_capo(2.19) - 0.012),
          (2.19, Y_GRADE_TOPO - 0.015), (2.08, Y_GRADE_TOPO - 0.015), (2.08, 0.70), (2.19, 0.70), (2.19, 0.62), (2.14, 0.52), (2.10, Y_BASE)]
    fr += _arco(EIXO, True)
    for s in (-1, 1):
        m.perfil("pintura", fr, s * (X_CAPO + 0.004), s * W, chanfro=0.025)
        _forro_do_arco(m, EIXO, s * (X_RODA - 0.17), s * (W - 0.01))
    # o vão entre o para-lama e a porta (a coluna da dobradiça, um pouco para dentro)
    # ======================================================== o cofre: a grade de baixo do para-brisa, a parede de fogo,
    # o fundo, a parede da frente (o suporte do radiador) e o painel embaixo dos faróis
    (zb, yb), _ = PARABRISA
    m.perfil("plastico", [(Z_PORTA - 0.02, 1.20), (zb, yb - 0.01), (Z_CAPO, _y_capo(Z_CAPO) - 0.02), (Z_CAPO, 1.20)],
             -(X_CAPO + 0.004), X_CAPO + 0.004, chanfro=0.006)
    if ALTA:
        for i in range(22):
            m.caixa("grade", (0.045, 0.004, 0.05), (-0.70 + i * 0.0667, _y_capo(Z_CAPO) - 0.012, (zb + Z_CAPO) / 2 + 0.01), giro=(-30, 0, 0), chanfro=0)
    m.caixa("escuro", (2 * X_CAPO, _y_capo(Z_CAPO) - 0.03 - Y_COFRE, 0.02), (0, (_y_capo(Z_CAPO) - 0.03 + Y_COFRE) / 2, Z_CAPO + 0.01), chanfro=0)
    m.caixa("escuro", (2 * X_CAPO, 0.02, Z_FRENTE_COFRE - Z_CAPO), (0, Y_COFRE - 0.01, (Z_CAPO + Z_FRENTE_COFRE) / 2), chanfro=0)
    m.caixa("escuro", (2 * X_CAPO, Y_GRADE_TOPO - Y_COFRE, 0.03), (0, (Y_GRADE_TOPO + Y_COFRE) / 2, Z_FRENTE_COFRE - 0.015), chanfro=0)
    m.caixa("escuro", (1.70, 0.20, 0.06), (0, 0.62, 2.11), chanfro=0)
    _frente(m)


def _forro_do_arco(m, zc, x0, x1, sup="escuro"):
    """A caixa da roda por dentro: a casca em volta do arco (achatado), de x0 a x1."""
    pts = _arco(zc, True, n=14 if ALTA else 7, folga=-0.006)
    xm, lx = (x0 + x1) / 2, abs(x1 - x0)
    for i in range(len(pts) - 1):
        (za, ya), (zb, yb) = pts[i], pts[i + 1]
        m.ripa(sup, (xm, ya, za), (xm, yb, zb), lx, 0.012)


def _frente(m):
    """A frente: a moldura cromada larga em volta da grade e dos faróis, a grade preta com a barra cromada no meio (e
    uma plaqueta lisa, sem emblema), os faróis retangulares com as luzes de seta embaixo, as luzes de canto âmbar que
    dobram o para-lama, o para-choque cromado com a borracha, a placa e o defletor preto embaixo."""
    z = Z_GRADE
    yg0, yg1, xg = 0.70, Y_GRADE_TOPO, 0.845
    # o fundo da grade e a grade de ovos (as barras ficam na textura da leve)
    m.caixa("grade", (1.00, yg1 - yg0 - 0.03, 0.06), (0, (yg0 + yg1) / 2, z - 0.07), chanfro=0)
    if ALTA:
        for i in range(10):
            # (v2) as barras de deitar em aço escovado: pretas no fundo preto, não apareciam
            m.caixa("aco", (0.98, 0.008, 0.03), (0, yg0 + 0.03 + i * 0.034, z - 0.035), chanfro=0.001)
        for i in range(13):
            m.caixa("plastico", (0.006, yg1 - yg0 - 0.04, 0.03), (-0.48 + i * 0.08, (yg0 + yg1) / 2, z - 0.035), chanfro=0.001)
    # a barra cromada larga no meio da grade e a plaqueta lisa
    m.caixa("cromo", (1.00, 0.06, 0.035), (0, 0.895, z - 0.015), chanfro=0.012, seg=2)
    m.caixa("cromo", (0.15, 0.075, 0.02), (0, 0.895, z + 0.008), chanfro=0.015, seg=2)
    m.caixa("pintura", (0.11, 0.045, 0.006), (0, 0.895, z + 0.018), chanfro=0.006)
    # a moldura cromada (por fora de tudo) e os montantes entre a grade e os faróis
    for p, q in (((-xg, yg0), (xg, yg0)), ((-xg, yg1), (xg, yg1)), ((-xg, yg0), (-xg, yg1)), ((xg, yg0), (xg, yg1))):
        m.ripa("cromo", (p[0], p[1], z - 0.01), (q[0], q[1], z - 0.01), 0.03, 0.03, chanfro=0.008)
    for s in (-1, 1):
        m.ripa("cromo", (s * 0.505, yg0, z - 0.01), (s * 0.505, yg1, z - 0.01), 0.022, 0.03, chanfro=0.006)
        m.ripa("cromo", (s * 0.505, 0.885, z - 0.012), (s * xg, 0.885, z - 0.012), 0.018, 0.024, chanfro=0.005)
        xc = s * 0.675
        # o farol (em cima): a caixa, o refletor, as lâmpadas e a lente (o material que a Unity acende)
        _farol(m, xc, 0.965, z)
        # a seta (embaixo): âmbar
        m.caixa("grade", (0.32, 0.15, 0.08), (xc, 0.79, z - 0.05), chanfro=0)
        m.caixa("pisca", (0.31, 0.14, 0.012), (xc, 0.79, z - 0.012), chanfro=0.004)
        # a luz de canto (âmbar, dobrando o para-lama)
        m.caixa("pisca", (W - xg + 0.002, Y_GRADE_TOPO - 0.015 - 0.70, Z_GRADE - 2.08 + 0.002), (s * (xg + W) / 2, (Y_GRADE_TOPO - 0.015 + 0.70) / 2, (Z_GRADE + 2.08) / 2 - 0.006),
                chanfro=0.012, seg=2)
        if ALTA:
            for k in range(1, 4):
                m.caixa("re", (W - xg - 0.02, 0.004, 0.004), (s * (xg + W) / 2, 0.70 + k * 0.09, Z_GRADE - 0.003), chanfro=0)
    # o para-choque: a lâmina cromada larga, as pontas dobrando para trás, a borracha preta e a placa
    perfil = [(2.20, 0.44), (2.20, 0.665), (2.29, 0.665), (2.325, 0.645), (2.335, 0.56), (2.325, 0.47), (2.29, 0.44)]
    m.perfil("cromo", perfil, -0.86, 0.86, chanfro=0.012)
    for s in (-1, 1):
        m.entrar(Matrix.Translation((s * 0.86, 0, 2.29)) @ Matrix.Rotation(math.radians(s * 38), 4, "Y"))
        m.perfil("cromo", [(z_ - 2.29, y_) for z_, y_ in perfil], 0.0, s * 0.15, chanfro=0.012)
        m.sair()
    m.caixa("borracha", (1.62, 0.035, 0.012), (0, 0.585, 2.336), chanfro=0.004)
    m.caixa("plastico", (0.38, 0.135, 0.014), (0, Y_PLACA_F, 2.338), chanfro=0.004)
    m.caixa("placa_f", (0.36, 0.115, 0.008), (0, Y_PLACA_F, 2.346), chanfro=0.002)
    # o defletor de baixo e os ganchos
    m.caixa("plastico", (1.66, 0.10, 0.12), (0, 0.40, 2.22), chanfro=0.015)
    for s in (-1, 1):
        m.caixa("escuro", (0.05, 0.06, 0.14), (s * 0.42, 0.36, 2.25), chanfro=0.01)


def _farol(m, x, y, z):
    lx, ly = 0.31, 0.15
    m.caixa("grade", (lx + 0.01, ly + 0.01, 0.08), (x, y, z - 0.05), chanfro=0)
    m.caixa("refletor", (lx - 0.01, ly - 0.01, 0.01), (x, y, z - 0.03), chanfro=0)
    if ALTA:
        for k in (-1, 1):
            m.torno("refletor", [(0.058, z - 0.026), (0.03, z - 0.05), (0.0, z - 0.056)], centro=(x + k * 0.075, y, 0), eixo="z", lados=20)
            m.cilindro("re", 0.012, 0.03, (x + k * 0.075, y, z - 0.035), eixo="z", lados=10)
    m.caixa("lente_farol", (lx, ly, 0.012), (x, y, z - 0.008), chanfro=0.004)


# ---------------------------------------------------------------------------------------------------- a cabine
def _cabine(m):
    (zb, yb), (zt, yt) = PARABRISA
    # ======================================================== as soleiras e a cabine atrás das portas (embaixo da cintura)
    for s in (-1, 1):
        m.caixa("pintura", (0.07, Y_PORTA - Y_BASE + 0.03, Z_PORTA - Z_CABINE), (s * (W - 0.035), (Y_BASE + Y_PORTA + 0.03) / 2, (Z_PORTA + Z_CABINE) / 2), chanfro=0.01)
        m.perfil("pintura", [(Z_FIM_PORTA, Y_BASE), (Z_FIM_PORTA, Y_CINTURA), (Z_CABINE + 0.01, Y_CINTURA), (Z_CABINE, Y_CINTURA - 0.05),
                             (Z_CABINE, Y_BASE)], s * (W - 0.06), s * W, chanfro=0.02)
        # a coluna da dobradiça, por dentro do vão da porta (fecha a cabine na frente da porta)
        m.caixa("pintura", (0.06, Y_CINTURA - Y_PORTA, 0.06), (s * (W - 0.05), (Y_CINTURA + Y_PORTA) / 2, Z_PORTA - 0.03), chanfro=0)
        # acima da cintura, inclinados para dentro (TOMBO): a coluna de trás (larga, sem vidro) e a coluna A
        m.entrar(_tombo(s))
        m.perfil("pintura", [(Z_FIM_PORTA + 0.005, Y_CINTURA), (Z_FIM_PORTA + 0.005, Y_TETO - 0.035), (Z_CABINE + 0.03, Y_TETO - 0.035),
                             (Z_CABINE, Y_TETO - 0.08), (Z_CABINE, Y_CINTURA)], s * (W - 0.05), s * W, chanfro=0.015)
        m.ripa("pintura", (s * (W - 0.03), yb - 0.02, zb + 0.01), (s * (W - 0.03), yt, zt - 0.01), 0.06, 0.09, chanfro=0.01)
        m.sair()
    # ======================================================== o teto, a calha, o para-brisa e o vidro de trás
    xt = _x_tombo(Y_TETO - 0.02)
    m.caixa("pintura", (2 * xt, 0.05, zt - Z_CABINE + 0.01), (0, Y_TETO - 0.025, (zt + Z_CABINE) / 2), chanfro=0.022, seg=2)
    for s in (-1, 1):
        m.caixa("borracha", (0.016, 0.018, zt - Z_CABINE - 0.04), (s * (xt - 0.004), Y_TETO - 0.05, (zt + Z_CABINE) / 2), chanfro=0.004)
    # o para-brisa (um trapézio: estreita em cima, com a cabine) e a borracha em volta
    xb0, xt0 = _x_tombo(yb) - 0.065, _x_tombo(yt) - 0.065
    _placa(m, "vidro", [(-xb0, yb, zb), (xb0, yb, zb), (xt0, yt, zt), (-xt0, yt, zt)], 0.008)
    for a, b in (((-xb0, yb, zb), (xb0, yb, zb)), ((-xt0, yt, zt), (xt0, yt, zt))):
        m.ripa("borracha", (a[0], a[1] + 0.004, a[2] + 0.004), (b[0], b[1] + 0.004, b[2] + 0.004), 0.03, 0.025)
    for s in (-1, 1):
        m.ripa("borracha", (s * xb0, yb + 0.004, zb + 0.004), (s * xt0, yt + 0.004, zt + 0.004), 0.03, 0.025)
    # os limpadores (os braços e as palhetas)
    d = (Vector((0, yt, zt)) - Vector((0, yb, zb))).normalized()
    for x in (-0.50, 0.04):
        p0 = Vector((x, yb + 0.03, zb + 0.06))
        p1 = p0 + Vector((0.46, 0, 0)) + d * 0.06
        m.cilindro("plastico", 0.018, 0.03, tuple(p0 + Vector((0, -0.01, 0))), eixo="y", lados=10)
        m.ripa("plastico", tuple(p0), tuple(p1), 0.016, 0.012)
        m.ripa("borracha", tuple(p0 + Vector((0.04, 0.012, -0.01))), tuple(p1 + Vector((0.05, 0.012, -0.01))), 0.01, 0.02)
    # a traseira da cabine: o painel com o vão do vidro de trás (o vidro corrediço do meio com as duas colunas pretas)
    zc0, zc1 = Z_CABINE, Z_CABINE + 0.04
    yv0, yv1, xv = 1.42, 1.78, 0.62
    m.frente("pintura", [(-W, Y_BASE + 0.05), (W, Y_BASE + 0.05), (W, Y_CINTURA), (_x_tombo(yv0), yv0), (-_x_tombo(yv0), yv0), (-W, Y_CINTURA)],
             zc0, zc1, chanfro=0.01)
    m.frente("pintura", [(-_x_tombo(yv1), yv1), (_x_tombo(yv1), yv1), (_x_tombo(Y_TETO - 0.03), Y_TETO - 0.03), (-_x_tombo(Y_TETO - 0.03), Y_TETO - 0.03)],
             zc0, zc1)
    for s in (-1, 1):
        m.frente("pintura", [(s * xv, yv0), (s * _x_tombo(yv0), yv0), (s * _x_tombo(yv1), yv1), (s * xv, yv1)], zc0, zc1)
    m.caixa("vidro", (2 * xv, yv1 - yv0, 0.008), (0, (yv0 + yv1) / 2, zc0 + 0.02), chanfro=0)
    for p, q in (((-xv, yv0), (xv, yv0)), ((-xv, yv1), (xv, yv1)), ((-xv, yv0), (-xv, yv1)), ((xv, yv0), (xv, yv1))):
        m.ripa("borracha", (p[0], p[1], zc0 - 0.003), (q[0], q[1], zc0 - 0.003), 0.03, 0.02)
    for x in (-0.20, 0.20):
        m.ripa("borracha", (x, yv0, zc0 - 0.004), (x, yv1, zc0 - 0.004), 0.025, 0.02)
    m.caixa("int_cromo", (0.05, 0.02, 0.012), (0, yv0 + 0.18, zc0 - 0.006), chanfro=0.003)
    # a terceira luz de freio, em cima do vidro de trás
    m.caixa("plastico", (0.34, 0.05, 0.04), (0, Y_TETO - 0.055, Z_CABINE - 0.01), chanfro=0.01)
    m.caixa("lente_lanterna", (0.30, 0.03, 0.01), (0, Y_TETO - 0.055, Z_CABINE - 0.03), chanfro=0.003)


# ---------------------------------------------------------------------------------------------------- a caçamba
def _cacamba(m):
    zf, zt = Z_CACAMBA, Z_TRASEIRA
    # as laterais (sólidas, da parede de dentro até a de fora), com o recorte da roda de trás
    lado = [(zf, Y_BASE), (zf, Y_CACAMBA), (zt, Y_CACAMBA), (zt, Y_BASE)] + _arco(-EIXO, False)
    for s in (-1, 1):
        m.perfil("pintura", lado, s * X_DENTRO, s * W, chanfro=0.022)
        _forro_do_arco(m, -EIXO, s * (X_RODA - 0.17), s * (W - 0.01))
        # o revestimento das paredes de dentro e o friso de cima
        m.caixa("revest", (0.006, Y_CACAMBA - Y_PISO_CACAMBA - 0.03, zf - zt - 0.12), (s * (X_DENTRO - 0.003), (Y_CACAMBA + Y_PISO_CACAMBA) / 2 - 0.015, (zf + zt) / 2), chanfro=0)
        # as caixas das rodas, por dentro (arredondadas em cima)
        zc = -EIXO
        caixa = [(zc + 0.45, Y_PISO_CACAMBA - 0.02), (zc + 0.45, 1.05), (zc + 0.38, 1.12), (zc - 0.38, 1.12), (zc - 0.45, 1.05), (zc - 0.45, Y_PISO_CACAMBA - 0.02)]
        m.perfil("revest", caixa, s * 0.62, s * X_DENTRO, chanfro=0.02)
        # os furos das estacas, em cima das laterais, e os ganchos de amarrar nos cantos
        if ALTA:
            for z in (zf - 0.10, -1.90, zt + 0.12):
                m.caixa("escuro", (0.05, 0.006, 0.09), (s * 0.91, Y_CACAMBA + 0.001, z), chanfro=0)
            for z in (zf - 0.12, zt + 0.14):
                m.tubo("aco", [(s * (X_DENTRO - 0.005), 1.18, z - 0.03), (s * (X_DENTRO - 0.03), 1.20, z), (s * (X_DENTRO - 0.005), 1.18, z + 0.03)], 0.006, lados=6)
        # as lanternas de trás (de pé, nas pontas das laterais): o vermelho acende, a luz de ré no meio
        xl = s * (X_DENTRO + W) / 2
        m.caixa("cromo", (W - X_DENTRO + 0.006, 0.46, 0.02), (xl, 1.125, zt - 0.006), chanfro=0.006)
        m.caixa("lente_lanterna", (W - X_DENTRO - 0.014, 0.20, 0.012), (xl, 1.235, zt - 0.016), chanfro=0.004)
        m.caixa("re", (W - X_DENTRO - 0.014, 0.075, 0.013), (xl, 1.09, zt - 0.016), chanfro=0.003)
        m.caixa("lente_lanterna", (W - X_DENTRO - 0.014, 0.12, 0.012), (xl, 0.98, zt - 0.016), chanfro=0.004)
    # a frente da caçamba (pintada por fora, revestida por dentro), o piso com as nervuras e o fundo
    m.caixa("pintura", (2 * X_DENTRO, Y_CACAMBA - Y_BASE - 0.12, 0.035), (0, (Y_CACAMBA + Y_BASE + 0.12) / 2, zf - 0.0175), chanfro=0.008)
    m.caixa("revest", (2 * X_DENTRO, Y_CACAMBA - Y_PISO_CACAMBA, 0.03), (0, (Y_CACAMBA + Y_PISO_CACAMBA) / 2, zf - 0.05), chanfro=0.006)
    m.caixa("pintura", (2 * X_DENTRO + 0.02, 0.025, 0.07), (0, Y_CACAMBA - 0.0125, zf - 0.035), chanfro=0.008)
    m.caixa("revest", (2 * X_DENTRO, 0.04, zf - zt - 0.10), (0, Y_PISO_CACAMBA - 0.02, (zf + zt) / 2 - 0.02), chanfro=0)
    if ALTA:
        for i in range(13):
            m.caixa("revest", (0.05, 0.014, zf - zt - 0.16), (-0.72 + i * 0.12, Y_PISO_CACAMBA + 0.005, (zf + zt) / 2 - 0.02), chanfro=0.005)
    m.caixa("escuro", (2 * X_DENTRO, 0.04, zf - zt), (0, Y_PISO_CACAMBA - 0.06, (zf + zt) / 2), chanfro=0)
    # o para-choque de trás (o degrau cromado): a lâmina, as pontas, o degrau de borracha e a placa no meio
    perfil = [(-2.55, 0.50), (-2.55, 0.71), (-2.64, 0.71), (-2.665, 0.69), (-2.67, 0.52), (-2.645, 0.50)]
    m.perfil("cromo", perfil, -0.84, 0.84, chanfro=0.012)
    for s in (-1, 1):
        m.entrar(Matrix.Translation((s * 0.84, 0, -2.55)) @ Matrix.Rotation(math.radians(-s * 30), 4, "Y"))
        m.perfil("cromo", [(z_ + 2.55, y_) for z_, y_ in perfil], 0.0, s * 0.14, chanfro=0.012)
        m.sair()
    m.caixa("borracha", (0.50, 0.008, 0.08), (0, 0.714, -2.595), chanfro=0.003)
    m.caixa("plastico", (0.38, 0.14, 0.014), (0, Y_PLACA_T, -2.672), chanfro=0.004)
    m.caixa("placa_t", (0.36, 0.115, 0.008), (0, Y_PLACA_T, -2.68), chanfro=0.002)
    m.caixa("re", (0.06, 0.012, 0.02), (0, Y_PLACA_T + 0.08, -2.672), chanfro=0.002)
    # o engate (o tubo quadrado embaixo do para-choque, a bola)
    m.caixa("escuro", (0.07, 0.07, 0.30), (0, 0.44, -2.56), chanfro=0.008)
    m.caixa("escuro", (0.10, 0.02, 0.12), (0, 0.48, -2.70), chanfro=0.005)
    m.cilindro("aco", 0.012, 0.06, (0, 0.52, -2.72), eixo="y", lados=8)
    if ALTA:
        m.torno("cromo", [(0.0, 0.535), (0.018, 0.54), (0.024, 0.555), (0.018, 0.57), (0.0, 0.575)], centro=(0, 0, -2.72), eixo="y", lados=14)


def _tampa(cena, M, nome):
    """A tampa da caçamba: a chapa de fora com o rebaixo de cima e a maçaneta preta no meio, o revestimento de dentro,
    os frisos dourados. A origem na dobradiça (PIVO_TAMPA, embaixo); abre girando em X para trás (deitando)."""
    m = M()
    x = X_DENTRO - 0.006
    zi, zo = Z_TRASEIRA + 0.05, Z_TRASEIRA
    m.perfil("pintura", [(zi, 0.83), (zi, Y_CACAMBA), (zo + 0.008, Y_CACAMBA), (zo, Y_CACAMBA - 0.02), (zo, 0.85), (zo + 0.01, 0.83)],
             -x, x, chanfro=0.008)
    m.caixa("revest", (2 * x - 0.04, Y_CACAMBA - 0.88, 0.006), (0, (Y_CACAMBA + 0.88) / 2, zi + 0.003), chanfro=0)
    if ALTA:
        # o rebaixo de cima (onde as picapes de verdade têm as letras: aqui, liso) e o vinco de baixo
        for y in (1.14, 1.30):
            m.ripa("pintura", (-x + 0.06, y, zo - 0.002), (x - 0.06, y, zo - 0.002), 0.006, 0.012)
        for xx in (-x + 0.06, x - 0.06):
            m.ripa("pintura", (xx, 1.14, zo - 0.002), (xx, 1.30, zo - 0.002), 0.006, 0.012)
        m.ripa("pintura", (-x + 0.04, 0.95, zo - 0.002), (x - 0.04, 0.95, zo - 0.002), 0.006, 0.012)
    # a maçaneta (o rebaixo preto e a alavanca)
    m.caixa("plastico", (0.24, 0.07, 0.012), (0, 1.22, zo - 0.004), chanfro=0.006)
    m.caixa("escuro", (0.20, 0.03, 0.012), (0, 1.215, zo - 0.008), chanfro=0.004)
    return m.objeto(nome, cena, pivo=PIVO_TAMPA)


def _faixas():
    """Os dois frisos dourados (y de baixo e de cima): um largo e, logo acima, um fino."""
    return [(Y_FAIXA - 0.012, Y_FAIXA + 0.004), (Y_FAIXA + 0.014, Y_FAIXA + 0.020)]


# ---------------------------------------------------------------------------------------------------- por baixo
def _de_baixo(m):
    # o chassi (as longarinas), o fundo da cabine, o tanque, o eixo de trás com o diferencial, os feixes de molas, o
    # escapamento e a travessa da frente
    for s in (-1, 1):
        m.caixa("escuro", (0.08, 0.17, 4.70), (s * 0.50, 0.53, -0.20), chanfro=0)
        m.caixa("escuro", (0.07, 0.06, 1.30), (s * 0.50, 0.35, -EIXO), chanfro=0.01)
        m.ripa("escuro", (s * 0.70, R_RODA, EIXO), (s * 0.30, 0.42, EIXO - 0.05), 0.08, 0.06)
    m.caixa("escuro", (1.84, 0.05, Z_PORTA - Z_CABINE), (0, 0.48, (Z_PORTA + Z_CABINE) / 2), chanfro=0)
    m.caixa("escuro", (0.60, 0.30, 0.55), (0, 0.55, -0.85), chanfro=0.03)
    m.cilindro("escuro", 0.055, 1.50, (0, R_RODA, -EIXO), eixo="x", lados=10)
    m.cilindro("escuro", 0.15, 0.20, (0, R_RODA, -EIXO + 0.02), eixo="z", lados=14)
    m.caixa("escuro", (1.40, 0.10, 0.12), (0, 0.48, EIXO + 0.05), chanfro=0)
    m.tubo("aco", [(0.28, 0.42, 1.20), (0.28, 0.40, -0.30), (0.36, 0.40, -1.10), (0.62, 0.42, -1.95), (0.62, 0.42, -2.40)], 0.035, lados=8)
    m.cilindro("cromo", 0.042, 0.10, (0.62, 0.42, -2.42), eixo="z", lados=12)


# ---------------------------------------------------------------------------------------------------- os lados
def _lados(m):
    """O vinco de lado a lado, os frisos dourados, as luzes de lado, a tampa do tanque (à direita, atrás da roda) e a
    antena."""
    vincos = [(Z_GRADE - 0.03, Z_PORTA + 0.005), (Z_FIM_PORTA - 0.005, Z_CABINE + 0.02), (Z_CACAMBA - 0.005, Z_TRASEIRA + 0.005)]
    # os frisos dourados: na porta (no _porta), na coluna de trás e na frente da caçamba, até perto do arco (como nas
    # fotos); a ponta de trás é cortada em diagonal
    frisos = [(Z_FIM_PORTA - 0.005, Z_CABINE + 0.005, 0.0), (Z_CACAMBA - 0.005, -EIXO + 0.40, 0.05)]
    for s in (-1, 1):
        x = s * (W + 0.0005)
        if ALTA:
            for z0, z1 in vincos:
                m.ripa("pintura", (s * W, Y_VINCO, z0), (s * W, Y_VINCO, z1), 0.006, 0.012)
        for z0, z1, corte in frisos:
            for y0, y1 in _faixas():
                m.perfil("faixa", [(z0, y1), (z1 + corte * (y1 - Y_FAIXA) / 0.02, y1), (z1 + corte * (y0 - Y_FAIXA) / 0.02, y0), (z0, y0)],
                         x, x + s * 0.003)
        m.caixa("pisca", (0.008, 0.04, 0.09), (s * (W + 0.004), 0.66, 2.06), chanfro=0.003)
        m.caixa("lente_lanterna", (0.008, 0.04, 0.09), (s * (W + 0.004), 0.66, Z_TRASEIRA + 0.10), chanfro=0.003)
    # a tampa do tanque (à direita, atrás da roda de trás)
    m.cilindro("pintura", 0.075, 0.008, (W + 0.003, 1.12, -2.22), eixo="x", lados=20)
    if ALTA:
        m.torno("escuro", [(0.079, W + 0.0025), (0.075, W + 0.0025)], centro=(0, 1.12, -2.22), eixo="x", lados=24)
    # a antena (no para-lama da frente, à direita)
    m.cilindro("plastico", 0.012, 0.02, (0.89, _y_capo(1.25) + 0.01, 1.25), eixo="y", lados=8)
    m.ripa("aco", (0.89, _y_capo(1.25) + 0.02, 1.25), (0.89, _y_capo(1.25) + 0.80, 1.12), 0.006, 0.006)


# ---------------------------------------------------------------------------------------------------- o interior
def _interior(m):
    (zb, yb), (zt, yt) = PARABRISA
    zi = Z_CABINE + 0.05          # a parede de trás por dentro
    # o assoalho, a rampa dos pés, o túnel e as soleiras por dentro
    m.caixa("carpete", (2 * (W - 0.07), 0.04, 0.95 - zi), (0, Y_PISO - 0.02, (0.95 + zi) / 2), chanfro=0)
    m.ripa("carpete", (0, Y_PISO - 0.01, 0.93), (0, 1.02, 1.10), 2 * (W - 0.07), 0.03)
    m.caixa("carpete", (0.32, 0.12, 0.95 - zi), (0, Y_PISO + 0.05, (0.95 + zi) / 2), chanfro=0.04, seg=2)
    for x, z, c in ((-0.37, 0.52, 0.42), (0.37, 0.52, 0.42)):
        m.caixa("tapete", (0.50, 0.008, c), (x, Y_PISO + 0.004, z), chanfro=0.004)
    for s in (-1, 1):
        m.caixa("int_plastico", (0.12, 0.04, Z_PORTA - Z_FIM_PORTA), (s * (W - 0.10), Y_PISO + 0.03, (Z_PORTA + Z_FIM_PORTA) / 2), chanfro=0.008)
        m.caixa("painel", (0.03, Y_PISO + 0.03 - Y_PORTA, Z_PORTA - Z_FIM_PORTA), (s * (W - 0.16), (Y_PISO + Y_PORTA) / 2, (Z_PORTA + Z_FIM_PORTA) / 2), chanfro=0)
        # o forro do lado de trás (atrás da porta) e da coluna A, por dentro
        m.entrar(_tombo(s))
        m.caixa("painel", (0.02, Y_TETO - 0.08 - Y_PISO, Z_FIM_PORTA - zi), (s * (W - 0.065), (Y_TETO - 0.08 + Y_PISO) / 2, (Z_FIM_PORTA + zi) / 2), chanfro=0.006)
        m.ripa("painel", (s * (W - 0.07), yb - 0.04, zb - 0.03), (s * (W - 0.07), yt - 0.03, zt - 0.03), 0.03, 0.08, chanfro=0.006)
        m.sair()
        # o cinto do lado de trás
        xo = s * (W - 0.09)
        m.ripa("cinto", (xo, 1.68, -0.36), (s * 0.66, 1.34, -0.24), 0.004, 0.045)
        m.ripa("cinto", (s * 0.66, 1.34, -0.24), (s * 0.63, 0.90, -0.12), 0.004, 0.045)
        m.caixa("int_plastico", (0.03, 0.06, 0.03), (xo, 1.68, -0.36), chanfro=0.006)
    # a parede de trás por dentro, o forro do teto, as luzes e o retrovisor
    m.caixa("painel", (2 * (W - 0.07), Y_TETO - 0.06 - Y_PISO, 0.02), (0, (Y_TETO - 0.06 + Y_PISO) / 2, zi + 0.01), chanfro=0)
    m.caixa("forro", (2 * (_x_tombo(Y_TETO) - 0.07), 0.03, zt - zi - 0.02), (0, Y_TETO - 0.065, (zt + zi) / 2), chanfro=0.006)
    m.caixa("int_plastico", (0.24, 0.07, 0.035), (0, yt - 0.10, zt - 0.05), chanfro=0.012, seg=2)
    m.caixa("int_cromo", (0.22, 0.055, 0.004), (0, yt - 0.10, zt - 0.069), chanfro=0)
    m.ripa("int_plastico", (0, Y_TETO - 0.07, zt - 0.02), (0, yt - 0.075, zt - 0.045), 0.02, 0.02)
    for s in (-1, 1):
        m.caixa("forro", (0.36, 0.02, 0.15), (s * 0.40, Y_TETO - 0.09, zt - 0.10), giro=(6, 0, 0), chanfro=0.008)
    # o painel: o da perua (perua_nova._painel), DY mais alto e DZ para trás, com as pontas que chegam às portas
    m.entrar(Matrix.Translation((0, DY, DZ)))
    v1._painel(m)
    pontas = [(0.62, 0.60), (0.62, 0.78), (0.70, 0.90), (1.00, 0.92), (1.00, 0.55), (0.80, 0.52)]
    for s in (-1, 1):
        m.perfil("painel", pontas, s * 0.795, s * (W - 0.075), chanfro=0.012)
    m.sair()
    _bancos(m)


def _almofada(m, lx, ly, lz, gomos, eixo):
    """Uma almofada de tecido: o miolo e os gomos na face de cima (eixo z) ou da frente (eixo y). Na leve, o miolo só."""
    m.caixa("tecido", (lx, ly, lz), (0, 0, 0), chanfro=min(0.03, min(lx, ly, lz) * 0.3), seg=2 if ALTA else 1)
    if not ALTA:
        return
    borda = 0.07
    largura = (lx - 2 * borda) / gomos
    for k in range(gomos):
        xc = -lx / 2 + borda + largura * (k + 0.5)
        if eixo == "z":
            m.caixa("tecido", (largura - 0.012, 0.016, lz - 0.08), (xc, ly / 2 + 0.003, 0.0), chanfro=0.007)
        else:
            m.caixa("tecido", (largura - 0.012, ly - 0.14, 0.016), (xc, -0.01, lz / 2 + 0.003), chanfro=0.007)
    for s in (-1, 1):
        if eixo == "z":
            m.caixa("vinil", (borda - 0.01, 0.035, lz - 0.04), (s * (lx / 2 - borda / 2), ly / 2 + 0.01, 0), chanfro=0.012)
        else:
            m.caixa("vinil", (borda - 0.01, ly - 0.06, 0.04), (s * (lx / 2 - borda / 2), 0, lz / 2 + 0.012), chanfro=0.012)


def _bancos(m):
    """Os dois bancos da frente (separados, de tecido cinza com as laterais de vinil, o encosto de cabeça), o descanso
    de braço no meio. As medidas são as da perua, DY mais altas e DZ para trás: o quadril de quem senta fica em
    LUGARES[0] e LUGARES[1]."""
    for s in (-1, 1):
        x = s * 0.37
        m.entrar(Matrix.Translation((x, DY, DZ)))
        m.caixa("int_plastico", (0.42, 0.08, 0.42), (0, 0.36, 0.08), chanfro=0.01)
        for xx in (-0.17, 0.17):
            m.caixa("int_cromo", (0.03, 0.02, 0.56), (xx, 0.33, 0.10), chanfro=0.003)
        m.entrar(Matrix.Translation((0, 0.44, 0.10)) @ Matrix.Rotation(math.radians(-6), 4, "X"))
        _almofada(m, 0.52, 0.14, 0.52, gomos=4, eixo="z")
        m.sair()
        m.entrar(Matrix.Translation((0, 0.80, -0.22)) @ Matrix.Rotation(math.radians(-12), 4, "X"))
        _almofada(m, 0.52, 0.66, 0.12, gomos=4, eixo="y")
        m.sair()
        m.entrar(Matrix.Translation((0, 1.20, -0.30)) @ Matrix.Rotation(math.radians(-12), 4, "X"))
        m.caixa("tecido", (0.28, 0.17, 0.10), (0, 0, 0), chanfro=0.03, seg=2 if ALTA else 1)
        for xx in (-0.07, 0.07):
            m.cilindro("int_cromo", 0.006, 0.10, (xx, -0.12, 0), eixo="y", lados=8)
        m.sair()
        m.caixa("int_plastico", (0.03, 0.08, 0.04), (-s * 0.27, 0.50, -0.06), chanfro=0.008)
        m.sair()
    # o descanso de braço do meio (entre os encostos) e o console com a tampa
    m.entrar(Matrix.Translation((0, DY, DZ)))
    m.caixa("tecido", (0.20, 0.13, 0.42), (0, 0.66, -0.06), chanfro=0.03, seg=2 if ALTA else 1)
    m.caixa("tecido", (0.20, 0.40, 0.10), (0, 0.86, -0.25), giro=(-12, 0, 0), chanfro=0.03, seg=2 if ALTA else 1)
    m.sair()


# ---------------------------------------------------------------------------------------------------- portas, capô, volante, roda
def _porta(cena, M, nome, s):
    """A porta comprida (de Z_PORTA a Z_FIM_PORTA), a origem na dobradiça: a chapa com o vinco e os frisos dourados, a
    maçaneta preta, a janela com a moldura preta (inclinada para dentro, TOMBO), o triângulo preto do retrovisor e o
    retrovisor grande; por dentro, o forro cinza, o descanso de braço, a maçaneta e o alto-falante."""
    m = M()
    z0, z1 = Z_PORTA - 0.006, Z_FIM_PORTA + 0.006
    meio, comp = (z0 + z1) / 2, z0 - z1
    m.perfil("pintura", [(z0, Y_PORTA), (z0, Y_CINTURA), (z1, Y_CINTURA), (z1, Y_PORTA)], s * (W - 0.045), s * W, chanfro=0.012)
    if ALTA:
        m.ripa("pintura", (s * W, Y_VINCO, z0 - 0.01), (s * W, Y_VINCO, z1 + 0.01), 0.006, 0.012)
    for y0, y1 in _faixas():
        m.perfil("faixa", [(z0 - 0.004, y1), (z1 + 0.004, y1), (z1 + 0.004, y0), (z0 - 0.004, y0)], s * (W + 0.0005), s * (W + 0.0035))
    # a maçaneta preta (de puxar para cima, na borda de trás) e a fechadura
    zm = z1 + 0.17
    m.caixa("plastico", (0.014, 0.05, 0.19), (s * (W + 0.004), Y_VINCO + 0.03, zm), chanfro=0.006)
    m.caixa("escuro", (0.008, 0.02, 0.15), (s * (W + 0.009), Y_VINCO + 0.024, zm), chanfro=0.002)
    m.cilindro("cromo", 0.012, 0.012, (s * (W + 0.005), Y_VINCO - 0.04, zm + 0.12), eixo="x", lados=10)
    # a janela: o vidro, a moldura preta, o triângulo do retrovisor e a borracha da cintura
    (zb, yb), (zt, yt) = PARABRISA

    def z_coluna(y):
        return min(z0 - 0.015, zb + (y - yb) * (zt - zb) / (yt - yb) - 0.06)

    ytop = Y_TOPO_JANELA
    m.entrar(_tombo(s))
    zg = 0.74                     # onde o vidro começa (na frente dele, o triângulo preto)
    vidro = [(zg, Y_CINTURA + 0.01), (zg, 1.46), (z_coluna(1.46), 1.46), (z_coluna(ytop), ytop), (z1 + 0.035, ytop),
             (z1 + 0.035, Y_CINTURA + 0.01)]
    m.perfil("vidro", vidro, s * (W - 0.034), s * (W - 0.026))
    tri = [(z_coluna(Y_CINTURA + 0.01), Y_CINTURA + 0.01), (zg - 0.01, Y_CINTURA + 0.01), (zg - 0.01, 1.46), (z_coluna(1.46), 1.46)]
    m.perfil("plastico", tri, s * (W - 0.04), s * (W - 0.018), chanfro=0.004)
    borda = [(z_coluna(Y_CINTURA), Y_CINTURA), (z_coluna(ytop + 0.02), ytop + 0.02), (z1 + 0.015, ytop + 0.02), (z1 + 0.015, Y_CINTURA)]
    for i in range(3):
        (za, ya), (zc_, yc) = borda[i], borda[i + 1]
        m.ripa("borracha", (s * (W - 0.03), ya, za), (s * (W - 0.03), yc, zc_), 0.03, 0.035)
    m.ripa("borracha", (s * (W - 0.03), Y_CINTURA, zg), (s * (W - 0.03), 1.46, zg), 0.025, 0.025)
    m.sair()
    m.caixa("borracha", (0.02, 0.016, comp - 0.03), (s * (W - 0.012), Y_CINTURA + 0.005, meio), chanfro=0.003)
    # o retrovisor grande (preto, retangular), no triângulo
    zr = 0.83
    m.caixa("plastico", (0.05, 0.08, 0.11), (s * (W + 0.008), 1.38, zr), chanfro=0.012)
    m.ripa("plastico", (s * (W + 0.02), 1.38, zr), (s * 1.10, 1.40, zr - 0.02), 0.035, 0.03)
    m.caixa("plastico", (0.12, 0.21, 0.075), (s * 1.14, 1.41, zr - 0.02), chanfro=0.022, seg=2)
    m.caixa("refletor", (0.105, 0.19, 0.006), (s * 1.14, 1.41, zr - 0.06), chanfro=0.003)
    # por dentro: o forro, o descanso de braço com a alça, a maçaneta, os botões do vidro e o alto-falante
    xi = W - 0.07
    m.caixa("painel", (0.03, Y_CINTURA - Y_PORTA - 0.06, comp - 0.05), (s * xi, (Y_CINTURA + Y_PORTA) / 2, meio), chanfro=0.008)
    m.caixa("tecido", (0.012, 0.22, comp * 0.55), (s * (xi - 0.016), 1.08, meio + comp * 0.10), chanfro=0.004)
    m.caixa("int_plastico", (0.05, 0.03, comp - 0.06), (s * (xi + 0.01), Y_CINTURA - 0.01, meio), chanfro=0.008)
    m.caixa("painel", (0.08, 0.05, comp * 0.42), (s * (xi - 0.05), 0.98, meio - comp * 0.12), chanfro=0.015, seg=2)
    m.caixa("int_plastico", (0.04, 0.012, 0.12), (s * (xi - 0.06), 1.007, meio - comp * 0.05), chanfro=0.004)
    m.tubo("int_plastico", [(s * (xi - 0.02), 1.01, meio - comp * 0.26), (s * (xi - 0.06), 1.05, meio - comp * 0.22),
                            (s * (xi - 0.06), 1.05, meio - comp * 0.02), (s * (xi - 0.02), 1.01, meio + comp * 0.02)], 0.01, lados=6)
    m.caixa("int_plastico", (0.01, 0.05, 0.10), (s * (xi - 0.017), 1.14, z0 - 0.20), chanfro=0.004)
    m.caixa("int_cromo", (0.012, 0.02, 0.07), (s * (xi - 0.022), 1.14, z0 - 0.20), chanfro=0.004)
    m.cilindro("alto_falante", 0.07, 0.01, (s * (xi - 0.018), 0.66, z0 - 0.24), eixo="x", lados=16)
    m.caixa("int_plastico", (0.02, 0.10, comp * 0.5), (s * (xi - 0.02), 0.62, meio - comp * 0.15), chanfro=0.006)
    m.cilindro("int_plastico", 0.006, 0.04, (s * (xi + 0.005), Y_CINTURA + 0.01, z1 + 0.12), eixo="y", lados=6)
    return m.objeto(nome, cena, pivo=(s * W, 0, Z_PORTA))


def _capo(cena, M, nome):
    """O capô: a chapa (quase reta, caindo para a frente), o meio um pouco mais alto e o reforço por baixo. A origem na
    dobradiça (atrás); abre girando em X."""
    m = M()
    zt, zf = Z_CAPO, 2.175
    x = X_CAPO - 0.004
    cima = [(zt, _y_capo(zt)), (2.12, _y_capo(2.12) + 0.004), (2.17, _y_capo(2.17) - 0.008), (zf, _y_capo(zf) - 0.03)]
    baixo = [(zf - 0.015, _y_capo(zf) - 0.05), (2.12, _y_capo(2.12) - 0.03), (zt, _y_capo(zt) - 0.03)]
    m.perfil("pintura", cima + baixo, -x, x, chanfro=0.01)
    # o meio um pouco mais alto (o "domo" do capô), com as bordas suaves
    d = [(zt + 0.06, _y_capo(zt + 0.06) + 0.001), (2.06, _y_capo(2.06) + 0.005), (2.06, _y_capo(2.06) + 0.016), (zt + 0.08, _y_capo(zt + 0.08) + 0.013)]
    m.perfil("pintura", d, -0.44, 0.44, chanfro=0.006)
    yb = lambda z: _y_capo(z) - 0.035
    for a, b in (((-x + 0.10, zt + 0.10), (x - 0.10, 2.05)), ((x - 0.10, zt + 0.10), (-x + 0.10, 2.05))):
        m.ripa("escuro", (a[0], yb(a[1]), a[1]), (b[0], yb(b[1]), b[1]), 0.06, 0.012)
    m.ripa("escuro", (-x + 0.05, yb(2.10), 2.10), (x - 0.05, yb(2.10), 2.10), 0.07, 0.014)
    return m.objeto(nome, cena, pivo=(0, _y_capo(zt), zt))


def _volante(cena, M, nome):
    """O volante de 4 raios (cinza): o aro, os raios, o miolo da buzina (liso) e a capa da coluna. A origem no meio, o
    aro no plano XY, a coluna para +Z (como o da perua)."""
    m = M()
    m.toro("painel", 0.195, 0.018, lados=36, gomos=8, achatar=1.1)
    for a in (-12, 192, 232, 308):
        t = math.radians(a)
        d = Vector((math.cos(t), math.sin(t), 0))
        m.ripa("int_plastico", tuple(d * 0.06 + Vector((0, 0, 0.03))), tuple(d * 0.185), 0.045, 0.016, chanfro=0.005)
    m.caixa("painel", (0.15, 0.115, 0.05), (0, -0.01, 0.032), chanfro=0.02, seg=2 if ALTA else 1)
    m.caixa("int_plastico", (0.10, 0.06, 0.006), (0, -0.01, 0.006), chanfro=0.004)
    m.cilindro("int_plastico", 0.036, 0.08, (0, 0, 0.085), eixo="z", lados=14)
    o = m.objeto(nome, cena)
    o.location = v1._para_blender((-0.37, 0.86 + DY, 0.50 + DZ))
    o.rotation_euler = (math.radians(90 + 25), 0, 0)
    return o


def _raio(m, a, h):
    """Um raio da roda de liga: um trapézio (estreito no miolo, largo no aro) com a cara inclinada."""
    u = Vector((0, math.cos(a), math.sin(a)))
    v = Vector((0, -math.sin(a), math.cos(a)))
    r0, r1, l0, l1 = 0.078, 0.200, 0.026, 0.046
    x0, x1 = h + 0.024, h + 0.004            # a cara (perto do miolo, perto do aro)
    fora = [Vector((x0, 0, 0)) + u * r0 - v * l0, Vector((x0, 0, 0)) + u * r0 + v * l0,
            Vector((x1, 0, 0)) + u * r1 + v * l1, Vector((x1, 0, 0)) + u * r1 - v * l1]
    dentro = [p - Vector((0.032, 0, 0)) for p in fora]
    m._puxar([tuple(p) for p in fora], [tuple(p) for p in dentro])
    if ALTA:
        m._chanfrar(0.006, 1)
    m._fim("liga")


def _roda(cena, M, nome):
    """A roda de liga de 6 raios com o pneu 245/75 aro 16: raio 0,385, largura 0,245. A origem no meio, o eixo em X, a
    cara para +X. A alta tem os parafusos e mais lados."""
    m = M()
    h, R = MEIA_LARGURA, R_RODA
    # (v3, a pedido: "não deixar tão realistas/redondos, vai consumir muitos triângulos") 16 lados chapados, na alta e na
    # leve (a mesma silhueta: o relevo assado não arredonda o que a malha não tem), e o perfil do pneu com 6 pontos. Os
    # tornos passam direto pela Malha da v1 (a MalhaLeve dividiria os lados por 2)
    lados = 16

    def torno(sup, perfil, n=lados):
        return v1.Malha.torno(m, sup, perfil, lados=n, liso=False)

    pneu = [(0.215, -h + 0.016), (0.300, -h), (R, -h + 0.05), (R, h - 0.05), (0.300, h), (0.215, h - 0.016)]
    torno("pneu", pneu)
    # o tambor do aro (por dentro) e o fundo escuro (o freio, que aparece entre os raios)
    torno("aco", [(0.212, h - 0.02), (0.212, -h + 0.02)])
    torno("escuro", [(0.0, 0.005), (0.17, 0.005)], 12)
    # a borda do aro e o miolo com a tampa do meio
    torno("liga", [(0.224, h - 0.012), (0.212, h + 0.008), (0.192, h - 0.012)])
    torno("liga", [(0.085, h - 0.02), (0.085, h + 0.014), (0.042, h + 0.026), (0.0, h + 0.03)], 8)
    if ALTA:
        torno("cromo", [(0.034, h + 0.027), (0.034, h + 0.034), (0.0, h + 0.04)], 8)
    # os 6 raios: largos, abrindo para fora, a cara inclinada (o aro é côncavo)
    for i in range(6):
        a = 2 * math.pi * i / 6 + math.pi / 6
        _raio(m, a, h)
    if ALTA:
        for i in range(6):
            a = 2 * math.pi * i / 6
            m.cilindro("cromo", 0.011, 0.022, (h + 0.03, 0.058 * math.cos(a), 0.058 * math.sin(a)), eixo="x", lados=6, liso=False)
    return m.objeto(nome, cena)


# ---------------------------------------------------------------------------------------------------- montar a cena
PREFIXO = "Cam_"
ALTO = "CamAlto_"
PECAS = ["Carroceria", "Porta_E", "Porta_D", "Roda", "Volante", "Capo", "Tampa"]
NOMES = [PREFIXO + p for p in PECAS]
LUGAR_DA_RODA = (-3.4, R_RODA, 0.0)
ASSAR = "Cam_Assar"


def _cena():
    cena = bpy.data.scenes.get("Caminhonete")
    if cena is None:
        cena = bpy.context.scene
        cena.name = "Caminhonete"
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
    global ALTA
    ALTA = alta
    objs = [_carroceria(cena, M, prefixo + "Carroceria")]
    for nome, s in (("Porta_E", -1), ("Porta_D", 1)):
        objs.append(_porta(cena, M, prefixo + nome, s))
    roda = _roda(cena, M, prefixo + "Roda")
    roda.location = v1._para_blender(LUGAR_DA_RODA)
    objs.append(roda)
    objs.append(_volante(cena, M, prefixo + "Volante"))
    objs.append(_capo(cena, M, prefixo + "Capo"))
    objs.append(_tampa(cena, M, prefixo + "Tampa"))
    ALTA = True
    return objs


def _tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def construir():
    preparar_catalogo()
    cena = _cena()
    for o in list(cena.objects):
        if o.name.split(".")[0] in ("Cube", "Camera", "Light"):
            bpy.data.objects.remove(o, do_unlink=True)
    _apagar([ALTO + p for p in PECAS] + NOMES + [ASSAR])
    altos = _uma_versao(cena, Alta, ALTO, True)
    leves = _uma_versao(cena, Leve, PREFIXO, False)
    ajustar_materiais()
    for grupo, rot in ((altos, "alto"), (leves, "leve")):
        tot = 0
        for o in grupo:
            t = _tris(o)
            tot += t * (5 if o.name.endswith("Roda") else 1)
            print(rot, o.name, t)
        print("TOTAL", rot, "(com 4 rodas e o estepe):", tot)


# ---------------------------------------------------------------------------------------------------- UV, bake, exportar
PESO = dict(v2.PESO, liga=1.4, revest=0.8, tecido=1.3, faixa=1.0)
PESO_DA_RODA = 2.2


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
    nome = f"Cam_{atlas}_{canal}"
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
        cena.world = bpy.data.worlds.new("MundoCam")
    cena.world.light_settings.distance = 0.35
    alvo = _juntar()
    altos = [bpy.data.objects[ALTO + p] for p in PECAS]
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
    os.makedirs(texturas(), exist_ok=True)
    for atlas, final in (("A", "Cam_Lataria"), ("B", "Cam_Interior")):
        def ler(canal):
            a = np.empty(TAMANHO * TAMANHO * 4, dtype=np.float32)
            _imagem(atlas, canal).pixels.foreach_get(a)
            return a.reshape(TAMANHO, TAMANHO, 4)
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
            img.filepath_raw = os.path.join(texturas(), nome + ".png")
            img.file_format = "PNG"
            img.save()
            bpy.data.images.remove(img)
            del dados
        print("texturas:", final)


FINAIS = {"Perua_Lataria": "Cam_Lataria", "Perua_Interior": "Cam_Interior", "Perua_Vidro": "Cam_Vidro",
          "Perua_Farol": "Cam_Farol", "Perua_Lanterna": "Cam_Lanterna"}


def exportar():
    import numpy as np
    preparar_catalogo()
    cena = _cena()
    os.makedirs(destino(), exist_ok=True)
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
    caminho = os.path.join(destino(), "Caminhonete.fbx")
    try:
        bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH"},
                                 apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE", path_mode="STRIP")
    finally:
        for t, o, n in temporarios:
            me = t.data
            bpy.data.objects.remove(t, do_unlink=True)
            bpy.data.meshes.remove(me)
            o.name = n
    print("FBX:", caminho)


def salvar():
    for img in bpy.data.images:
        if img.name.startswith("Cam_") and img.has_data and not img.packed_file:
            img.pack()
    bpy.ops.wm.save_as_mainfile(filepath=blend(), copy=False)
    print("blend:", blend())
