# O saque (31/10/2026, a pedido: "precisamos criar alguns loots ... mais variações de latas de comida e de água,
# refrigerantes e algumas matérias-primas para craft"; cada item numa célula do inventário, por enquanto): a comida, a
# bebida, os remédios, os materiais e os kits de montar, o Loot.fbx, os ícones e as fotos do inventário. Também as
# construções montadas no mundo (a fogueira, a cama e a barricada) e a bancada de trabalho.
#
# Como rodar (Blender 5.2, em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/loot.py -- tudo
#   (as etapas: construir, exportar, imagens; "imagens Feijao Carne" só as de alguns)
# Depois, na Unity: Zombie City Cleaner > Montar armas (ou só "Montar o saque").
#
# O formato é o do Itens.fbx (as funções do equipamentos.py): cada peça é um objeto <Nome>_corpo girado 90° em X, com a
# malha nos eixos da Unity. Aqui o modelo é feito nos eixos do Blender (o alto em +Z, a frente em -Y).
# - O que vai na mão (a comida, a bebida e os remédios): a origem onde a mão segura, como a lata (Item_Lata: a origem no
#   meio da lata, o alto em +Z) e a garrafa de água (a origem 2,3 cm abaixo do meio): a montagem põe a malha deles no
#   lugar da lata (ou da garrafa) dos braços.
# - O resto: a origem embaixo, no meio (no chão, a Unity apoia pela caixa da malha).
# Cores: os blocos da Paleta.png (Assets/_Game/Art/Paleta.png). As das embalagens são as células novas da linha 11
# (colunas 6 a 15) e da linha 0 (colunas 0 a 5), postas em 31/10/2026 (estavam vazias, cinza 404040).

import math
import os
import sys

import bpy
from mathutils import Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import equipamentos as eq  # noqa: E402

eq.CORES.update({
    # as novas (as embalagens)
    "vermelho": (11, 6), "amarelo": (11, 7), "laranja_vivo": (11, 8), "azul": (11, 9), "verde": (11, 10),
    "roxo": (11, 11), "azul_claro": (11, 12), "rosa": (11, 13), "branco": (11, 14), "prata": (11, 15),
    "vermelho_escuro": (0, 0), "verde_escuro": (0, 1), "chocolate": (0, 2), "dourado": (0, 3), "azul_escuro": (0, 4),
    "laranja_escuro": (0, 5),
    # as que já existiam
    "lata": (8, 8), "lata_clara": (4, 6), "aco": (8, 2), "aco_escuro": (8, 1), "ferro": (8, 0),
    "madeira": (12, 0), "madeira_clara": (1, 4), "madeira_escura": (3, 7), "casca": (7, 4), "cerne": (1, 2),
    "corda": (1, 6), "cobre": (2, 7), "creme": (3, 9), "papelao": (6, 11), "jeans": (4, 8), "oliva": (2, 8),
    "vinho": (6, 4), "ambar": (2, 7), "cola": (12, 1), "garrafa_verde": (2, 10), "cinza": (8, 1), "preto": (3, 2),
    "pedra": (8, 2), "pedra_escura": (10, 3), "cinza_escuro": (11, 5), "carvao": (8, 5), "colchao": (10, 5),
    "caixa_ferramenta": (8, 6), "ferrugem": (8, 3), "minhoca": (13, 0),
})

_caixa = eq._caixa
_cilindro = eq._cilindro


def C(cor, tam, centro, giro=(0, 0, 0), chanfro=0.0):
    return (_caixa, cor, tam, centro, giro, chanfro)


def Z(cor, raio, altura, centro, lados=12, eixo="Z"):
    return (_cilindro, cor, raio, altura, centro, eixo, lados)


# ------------------------------------------------------------------ a comida (o espaço da lata: o meio na origem)

def _lata(rotulo, faixa, raio=0.037, altura=0.114, z0=None, tampa="lata"):
    """Uma lata de conserva no espaço da lata: o corpo, as bordas e o rótulo com uma faixa."""
    z0 = -altura / 2 if z0 is None else z0
    zm = z0 + altura / 2
    p = [
        Z("lata", raio * 0.97, altura, (0, 0, zm), 14),
        Z(tampa, raio, 0.006, (0, 0, z0 + altura - 0.003), 14),
        Z("lata", raio, 0.006, (0, 0, z0 + 0.003), 14),
        Z(rotulo, raio * 0.995, altura * 0.72, (0, 0, zm), 14),
    ]
    if faixa:
        p.append(Z(faixa, raio * 1.0, altura * 0.18, (0, 0, zm + altura * 0.05), 14))
    return p


def _feijao():
    return _lata("vinho", "creme") + [C("vermelho_escuro", (0.026, 0.006, 0.02), (0, -0.036, -0.012))]


def _carne():
    # a lata baixa e larga (de carne), com a chave de abrir em cima
    return _lata("vermelho", "branco", raio=0.045, altura=0.07) + [C("prata", (0.03, 0.006, 0.003), (0.01, 0, 0.037))]


def _milho():
    return _lata("verde", "amarelo") + [C("amarelo", (0.022, 0.006, 0.03), (0, -0.036, -0.01))]


def _sopa():
    p = _lata("branco", None, raio=0.034, altura=0.10)
    p.append(Z("vermelho", 0.0342, 0.045, (0, 0, 0.012), 14))
    p.append(C("dourado", (0.024, 0.006, 0.012), (0, -0.034, -0.02)))
    return p


def _salgadinho():
    # o pacote (um travesseiro): o meio inchado, as pontas soldadas em cima e embaixo
    w, d, h = 0.15, 0.05, 0.20
    return [
        C("laranja_vivo", (w, d, h * 0.82), (0, 0, 0), chanfro=0.012),
        C("laranja_vivo", (w * 1.02, 0.008, 0.022), (0, 0, h / 2 - 0.01)),
        C("laranja_vivo", (w * 1.02, 0.008, 0.022), (0, 0, -h / 2 + 0.01)),
        C("amarelo", (w * 0.7, 0.004, 0.06), (0, -d / 2 - 0.001, 0.02)),
        C("vermelho", (w * 0.4, 0.004, 0.022), (0, -d / 2 - 0.002, -0.04)),
    ]


def _chocolate():
    # a barra em pé (a mão segura no meio), na embalagem marrom com a faixa vermelha
    return [
        C("chocolate", (0.075, 0.014, 0.16), (0, 0, 0), chanfro=0.003),
        C("vermelho", (0.077, 0.016, 0.04), (0, 0, 0.03)),
        C("dourado", (0.04, 0.004, 0.012), (0, -0.009, 0.03)),
    ]


def _barra():
    return [
        C("verde", (0.04, 0.018, 0.13), (0, 0, 0), chanfro=0.003),
        C("amarelo", (0.042, 0.02, 0.03), (0, 0, 0.02)),
        C("verde", (0.044, 0.006, 0.012), (0, 0, 0.068)),
    ]


# ------------------------------------------------------------------ a bebida (o espaço da garrafa: a mão um pouco abaixo do meio)

def _copo():
    return [
        Z("azul_claro", 0.034, 0.085, (0, 0, 0.005), 14),
        Z("prata", 0.036, 0.004, (0, 0, 0.049), 14),
        C("azul", (0.03, 0.004, 0.015), (0, -0.034, 0.012)),
    ]


def _refri_garrafa():
    # a garrafa PET de 2 litros: o corpo escuro, o rótulo vermelho, o ombro, o gargalo e a tampa
    p = [
        Z("cola", 0.05, 0.22, (0, 0, -0.01), 14),
        Z("cola", 0.044, 0.02, (0, 0, -0.125), 14),
        Z("vermelho", 0.0505, 0.08, (0, 0, 0.02), 14),
        Z("branco", 0.051, 0.012, (0, 0, 0.02), 14),
        Z("cola", 0.038, 0.03, (0, 0, 0.115), 12),
        Z("cola", 0.022, 0.035, (0, 0, 0.145), 10),
        Z("vermelho", 0.016, 0.022, (0, 0, 0.172), 10),
    ]
    return p


def _refri_lata():
    return [
        Z("vermelho", 0.033, 0.112, (0, 0, 0), 14),
        Z("prata", 0.031, 0.008, (0, 0, 0.06), 14),
        Z("prata", 0.03, 0.006, (0, 0, -0.058), 14),
        Z("branco", 0.0332, 0.025, (0, 0, 0.012), 14),
        C("prata", (0.016, 0.01, 0.002), (0, 0.012, 0.065)),
    ]


def _energetico():
    return [
        Z("azul", 0.029, 0.15, (0, 0, 0.01), 14),
        Z("prata", 0.027, 0.008, (0, 0, 0.089), 14),
        Z("prata", 0.0292, 0.03, (0, 0, 0.03), 14),
        C("amarelo", (0.02, 0.006, 0.05), (0, -0.028, -0.02)),
    ]


def _leite():
    # a caixa de 1 litro, com o topo em telhado e a tampa de rosca
    return [
        C("branco", (0.07, 0.065, 0.18), (0, 0, -0.01)),
        C("azul", (0.072, 0.067, 0.05), (0, 0, -0.05)),
        C("branco", (0.07, 0.02, 0.03), (0, 0, 0.093), giro=(0, 0, 0)),
        C("branco", (0.07, 0.045, 0.012), (0, 0.01, 0.085), giro=(-35, 0, 0)),
        C("branco", (0.07, 0.045, 0.012), (0, -0.01, 0.085), giro=(35, 0, 0)),
        Z("azul", 0.011, 0.012, (0.018, -0.02, 0.09), 8),
    ]


def _suco():
    return [
        C("laranja_vivo", (0.055, 0.038, 0.105), (0, 0, 0), chanfro=0.003),
        C("verde", (0.057, 0.04, 0.03), (0, 0, -0.025)),
        C("amarelo", (0.028, 0.004, 0.028), (0, -0.02, 0.018)),
        Z("branco", 0.0025, 0.06, (0.012, 0.006, 0.075), 6),
    ]


def _termica():
    return [
        Z("garrafa_verde", 0.045, 0.24, (0, 0, 0.02), 14),
        Z("aco", 0.046, 0.02, (0, 0, -0.095), 14),
        Z("aco", 0.032, 0.04, (0, 0, 0.16), 12),
        Z("preto", 0.035, 0.03, (0, 0, 0.19), 12),
        C("preto", (0.014, 0.02, 0.12), (0.06, 0, 0.04)),
        C("preto", (0.03, 0.02, 0.014), (0.045, 0, 0.1)),
        C("preto", (0.03, 0.02, 0.014), (0.045, 0, -0.02)),
    ]


# ------------------------------------------------------------------ os remédios (o espaço da lata)

def _xarope():
    return [
        Z("ambar", 0.026, 0.09, (0, 0, -0.01), 12),
        Z("ambar", 0.012, 0.02, (0, 0, 0.044), 10),
        Z("branco", 0.016, 0.02, (0, 0, 0.062), 10),
        Z("branco", 0.0262, 0.05, (0, 0, -0.012), 12),
        C("vermelho", (0.02, 0.005, 0.012), (0, -0.026, -0.005)),
    ]


def _comprimidos():
    # a cartela: a lâmina de alumínio com as bolhas dos comprimidos na frente
    p = [C("prata", (0.06, 0.006, 0.11), (0, 0, 0))]
    for i in range(2):
        for j in range(5):
            p.append(Z("branco", 0.0075, 0.006, (-0.014 + 0.028 * i, -0.004, -0.04 + 0.02 * j), 8, "Y"))
    return p


def _kit_medico():
    return [
        C("vermelho", (0.20, 0.08, 0.15), (0, 0, 0), chanfro=0.008),
        C("branco", (0.06, 0.004, 0.018), (0, -0.041, 0.005)),
        C("branco", (0.018, 0.004, 0.06), (0, -0.041, 0.005)),
        C("vermelho_escuro", (0.202, 0.082, 0.01), (0, 0, 0.045)),
        C("preto", (0.08, 0.02, 0.012), (0, 0, 0.082)),
        C("prata", (0.012, 0.01, 0.02), (-0.06, -0.041, 0.04)),
        C("prata", (0.012, 0.01, 0.02), (0.06, -0.041, 0.04)),
    ]


def _atadura():
    return [
        Z("branco", 0.032, 0.08, (0, 0, 0), 12, "X"),
        Z("creme", 0.012, 0.082, (0, 0, 0), 8, "X"),
        C("branco", (0.078, 0.002, 0.05), (0, -0.032, -0.025)),
    ]


def _gaze():
    return [
        C("branco", (0.10, 0.018, 0.10), (0, 0, 0), chanfro=0.003),
        C("azul", (0.07, 0.004, 0.02), (0, -0.01, 0.025)),
        C("azul_claro", (0.08, 0.004, 0.012), (0, -0.01, -0.02)),
    ]


def _desinfetante():
    return [
        C("branco", (0.065, 0.04, 0.13), (0, 0, -0.01), chanfro=0.006),
        Z("branco", 0.014, 0.02, (0, 0, 0.065), 10),
        Z("vermelho", 0.017, 0.022, (0, 0, 0.084), 10),
        C("azul", (0.05, 0.004, 0.05), (0, -0.021, -0.005)),
        C("vermelho", (0.03, 0.005, 0.012), (0, -0.022, -0.005)),
    ]


def _spray():
    return [
        Z("branco", 0.025, 0.12, (0, 0, -0.01), 12),
        Z("verde", 0.0252, 0.04, (0, 0, -0.01), 12),
        Z("prata", 0.02, 0.012, (0, 0, 0.055), 10),
        Z("verde", 0.016, 0.022, (0, 0, 0.072), 10),
        C("verde", (0.008, 0.012, 0.006), (0, -0.016, 0.076)),
    ]


def _antibiotico():
    return [
        C("branco", (0.10, 0.025, 0.06), (0, 0, 0), chanfro=0.002),
        C("azul", (0.102, 0.027, 0.014), (0, 0, 0.012)),
        C("vermelho", (0.03, 0.004, 0.008), (-0.03, -0.014, -0.014)),
    ]


def _bandagem():
    return [
        Z("creme", 0.026, 0.07, (0, 0, 0), 10, "X"),
        Z("creme", 0.02, 0.072, (0, 0, 0), 8, "X"),
        C("creme", (0.068, 0.002, 0.045), (0, -0.026, -0.024)),
        C("vinho", (0.006, 0.054, 0.054), (0.012, 0, 0)),
    ]


# ------------------------------------------------------------------ os materiais (a origem embaixo, no meio)

def _madeira():
    p = []
    for i, (cor, dz) in enumerate((("madeira_clara", 0), ("madeira", 0.021), ("madeira_clara", 0.042))):
        p.append(C(cor, (0.48, 0.10, 0.02), ((i - 1) * 0.01, (i - 1) * 0.02, 0.01 + dz), giro=(0, 0, (i - 1) * 4)))
    return p


def _toras():
    p = []
    for x, y, z in ((0, -0.065, 0.06), (0, 0.065, 0.06), (0.02, 0, 0.165)):
        p.append(Z("casca", 0.06, 0.38, (x, y, z), 9, "X"))
        p.append(Z("cerne", 0.052, 0.384, (x, y, z), 9, "X"))
    return p


def _pano():
    return [
        C("jeans", (0.22, 0.16, 0.025), (0, 0, 0.0125), chanfro=0.004),
        C("vinho", (0.2, 0.15, 0.02), (0.01, 0.005, 0.035), chanfro=0.004),
        C("oliva", (0.19, 0.14, 0.018), (-0.005, -0.004, 0.054), chanfro=0.004),
    ]


def _anel(cor, raio, espessura, altura, z, n=12):
    lado = 2 * raio * math.sin(math.pi / n) + espessura * 0.4
    return [C(cor, (lado, espessura, altura), (raio * math.cos(2 * math.pi * i / n), raio * math.sin(2 * math.pi * i / n), z),
              giro=(0, 0, 360 * i / n + 90)) for i in range(n)]


def _corda():
    p = []
    for k in range(3):
        p += _anel("corda", 0.085 - k * 0.012, 0.018, 0.018, 0.009 + k * 0.016, 14)
    p.append(C("corda", (0.12, 0.016, 0.016), (0.11, -0.03, 0.009), giro=(0, 0, -20)))
    return p


def _fita():
    return _anel("prata", 0.048, 0.022, 0.048, 0.024, 14) + _anel("papelao", 0.033, 0.008, 0.048, 0.024, 12) + [
        C("prata", (0.05, 0.002, 0.044), (0.03, -0.055, 0.024), giro=(0, 0, 20)),
    ]


def _ferramenta():
    # o martelo e a chave de boca, deitados e cruzados
    return [
        C("madeira", (0.26, 0.025, 0.02), (0, 0, 0.01), giro=(0, 0, 20)),
        C("aco_escuro", (0.03, 0.10, 0.03), (0.125, 0.045, 0.015), giro=(0, 0, 20)),
        C("aco", (0.22, 0.022, 0.008), (0, 0, 0.024), giro=(0, 0, -25)),
        C("aco", (0.035, 0.04, 0.008), (0.11, -0.05, 0.024), giro=(0, 0, -25)),
        C("aco", (0.035, 0.04, 0.008), (-0.11, 0.05, 0.024), giro=(0, 0, -25)),
    ]


def _pecas():
    return [
        Z("aco", 0.035, 0.012, (-0.03, 0.01, 0.006), 10),
        Z("aco_escuro", 0.014, 0.014, (-0.03, 0.01, 0.012), 8),
        C("ferrugem", (0.09, 0.05, 0.006), (0.04, -0.01, 0.003), giro=(0, 0, 25)),
        Z("aco", 0.006, 0.07, (0.02, 0.04, 0.01), 6, "X"),
        Z("lata_clara", 0.01, 0.05, (-0.04, -0.04, 0.01), 8, "Y"),
        C("aco_escuro", (0.025, 0.025, 0.02), (0.06, 0.035, 0.016), giro=(0, 0, 10)),
    ]


def _fios():
    p = []
    for k in range(3):
        p += _anel("cobre", 0.06 - k * 0.008, 0.008, 0.008, 0.004 + k * 0.007, 12)
    p.append(C("cobre", (0.08, 0.006, 0.006), (0.075, 0.02, 0.004), giro=(0, 0, 30)))
    p.append(C("preto", (0.03, 0.008, 0.008), (0.11, 0.04, 0.004), giro=(0, 0, 30)))
    return p


def _pregos():
    p = [C("papelao", (0.09, 0.06, 0.045), (0, 0, 0.0225))]
    for x, y, a in ((0.07, -0.01, 10), (0.075, 0.025, -30), (0.06, -0.04, 60), (0.09, 0.0, 85)):
        p.append(C("aco", (0.05, 0.004, 0.004), (x, y, 0.003), giro=(0, 0, a)))
        p.append(C("aco", (0.006, 0.008, 0.008), (x - 0.025 * math.cos(math.radians(a)), y - 0.025 * math.sin(math.radians(a)), 0.004), giro=(0, 0, a)))
    return p


def _isca():
    return [
        Z("verde_escuro", 0.045, 0.05, (0, 0, 0.025), 12),
        Z("verde", 0.047, 0.008, (0, 0, 0.052), 12),
        C("minhoca", (0.05, 0.007, 0.007), (0.0, -0.01, 0.06), giro=(0, 0, 30)),
        C("minhoca", (0.04, 0.007, 0.007), (0.01, 0.012, 0.06), giro=(0, 0, -40)),
        C("branco", (0.04, 0.004, 0.02), (0, -0.045, 0.025)),
    ]


def _vara():
    # a vara deitada (desmontada em dois gomos, lado a lado), com o molinete e a linha
    return [
        Z("preto", 0.007, 0.62, (0, -0.012, 0.008), 6, "X"),
        Z("preto", 0.005, 0.6, (0.01, 0.012, 0.007), 6, "X"),
        Z("madeira_clara", 0.012, 0.16, (-0.22, -0.012, 0.012), 8, "X"),
        Z("aco", 0.025, 0.02, (-0.12, -0.04, 0.026), 10, "Y"),
        C("aco_escuro", (0.01, 0.03, 0.01), (-0.12, -0.025, 0.012)),
    ]


def _kit_reparo():
    return [
        C("caixa_ferramenta", (0.22, 0.10, 0.09), (0, 0, 0.045), chanfro=0.006),
        C("aco_escuro", (0.222, 0.102, 0.012), (0, 0, 0.07)),
        C("preto", (0.1, 0.014, 0.012), (0, 0, 0.1)),
        C("preto", (0.012, 0.014, 0.02), (-0.045, 0, 0.093)),
        C("preto", (0.012, 0.014, 0.02), (0.045, 0, 0.093)),
        C("prata", (0.02, 0.006, 0.02), (0, -0.052, 0.06)),
    ]


def _roupa():
    # a camisa dobrada: o corpo, a gola e as mangas dobradas por cima
    return [
        C("jeans", (0.26, 0.20, 0.035), (0, 0, 0.0175), chanfro=0.006),
        C("azul_escuro", (0.09, 0.03, 0.012), (0, -0.085, 0.04)),
        C("jeans", (0.06, 0.16, 0.012), (-0.08, 0.01, 0.04), giro=(0, 0, 8)),
        C("jeans", (0.06, 0.16, 0.012), (0.08, 0.01, 0.04), giro=(0, 0, -8)),
        C("branco", (0.01, 0.01, 0.006), (0, -0.07, 0.048)),
    ]


# ------------------------------------------------------------------ os kits (na mochila) e as construções (no mundo)

def _kit_fogueira():
    p = []
    for i, (y, z) in enumerate(((-0.04, 0.035), (0.04, 0.035), (0.0, 0.1), (-0.07, 0.1), (0.07, 0.1))):
        p.append(Z("casca" if i % 2 else "madeira_escura", 0.035, 0.42 + 0.02 * i, (0.01 * i, y, z), 8, "X"))
    for x in (-0.12, 0.12):
        p += [C("corda", (0.02, 0.2, 0.012), (x, 0, 0.145)), C("corda", (0.02, 0.012, 0.15), (x, -0.105, 0.075)),
              C("corda", (0.02, 0.012, 0.15), (x, 0.105, 0.075))]
    return p


def _kit_cama():
    return [
        Z("oliva", 0.08, 0.55, (0, 0, 0.08), 12, "X"),
        Z("cinza_escuro", 0.03, 0.552, (0, 0, 0.08), 8, "X"),
        C("preto", (0.025, 0.17, 0.008), (-0.18, 0, 0.162)),
        C("preto", (0.025, 0.17, 0.008), (0.18, 0, 0.162)),
    ]


def _kit_barricada():
    p = []
    for i in range(4):
        p.append(C("madeira_clara" if i % 2 else "madeira", (0.6, 0.11, 0.022), (0, (i - 1.5) * 0.115, 0.011)))
    p.append(C("madeira", (0.6, 0.11, 0.022), (0, 0, 0.033), giro=(0, 0, 20)))
    p += [C("corda", (0.02, 0.48, 0.014), (-0.2, 0, 0.05)), C("corda", (0.02, 0.48, 0.014), (0.2, 0, 0.05))]
    return p


def _fogueira_montada():
    # o anel de pedras, as cinzas e a lenha em cruz (o fogo é da Unity)
    p = [Z("carvao", 0.27, 0.02, (0, 0, 0.01), 12)]
    for i in range(9):
        a = 2 * math.pi * i / 9
        p.append(C("pedra" if i % 2 else "pedra_escura", (0.13, 0.1, 0.09), (0.33 * math.cos(a), 0.33 * math.sin(a), 0.045),
                   giro=(0, 0, math.degrees(a) + 90), chanfro=0.02))
    for i in range(4):
        a = 45 + 90 * i
        t = math.radians(a)
        p.append(C("casca" if i % 2 else "madeira_escura", (0.44, 0.07, 0.07), (0.07 * math.cos(t), 0.07 * math.sin(t), 0.07 + 0.03 * (i % 2)),
                   giro=(0, 0, a), chanfro=0.015))
    return p


def _cama_montada():
    # dois paletes, o colchão fino, o travesseiro e o cobertor dobrado no pé
    p = []
    for y in (-0.48, 0.48):
        for x in (-0.38, 0, 0.38):
            p.append(C("madeira", (0.08, 0.92, 0.08), (x, y, 0.04)))
        for k in range(5):
            p.append(C("madeira_clara", (0.86, 0.12, 0.022), (0, y - 0.38 + 0.19 * k, 0.091)))
    p.append(C("colchao", (0.8, 1.86, 0.12), (0, 0, 0.162), chanfro=0.03))
    p.append(C("creme", (0.5, 0.3, 0.09), (0, 0.72, 0.265), chanfro=0.035))
    p.append(C("oliva", (0.82, 0.5, 0.05), (0, -0.62, 0.247), chanfro=0.015))
    return p


def _barricada_montada():
    # 1,6 m de largura e 1,5 de altura: dois montantes, quatro tábuas desencontradas e uma na diagonal, com os pregos
    p = []
    for x in (-0.72, 0.72):
        p.append(C("madeira_escura", (0.1, 0.1, 1.5), (x, 0.03, 0.75)))
        p.append(C("madeira_escura", (0.1, 0.4, 0.06), (x, 0.0, 0.03)))
    for i, (z, a, cor) in enumerate(((0.3, 2, "madeira"), (0.62, -3, "madeira_clara"), (0.95, 1.5, "madeira"), (1.28, -2, "madeira_clara"))):
        p.append(C(cor, (1.68, 0.03, 0.2), ((i % 2 - 0.5) * 0.04, -0.035, z), giro=(0, a, 0)))
        for x in (-0.72, 0.72):
            p.append(C("aco_escuro", (0.012, 0.01, 0.012), (x, -0.055, z)))
    p.append(C("madeira_escura", (1.75, 0.03, 0.16), (0, -0.068, 0.78), giro=(0, -38, 0)))
    return p


def _bancada():
    # 1,6 x 0,7 m e 0,9 de altura: o tampo grosso, as pernas, a prateleira de baixo, a morsa na ponta e umas ferramentas
    w, d, h = 1.6, 0.7, 0.9
    p = [C("madeira", (w, d, 0.06), (0, 0, h - 0.03), chanfro=0.008)]
    for x in (-w / 2 + 0.06, w / 2 - 0.06):
        for y in (-d / 2 + 0.06, d / 2 - 0.06):
            p.append(C("madeira_escura", (0.08, 0.08, h - 0.06), (x, y, (h - 0.06) / 2)))
    p.append(C("madeira_clara", (w - 0.1, d - 0.1, 0.03), (0, 0, 0.2)))
    p.append(C("madeira_escura", (w - 0.16, 0.06, 0.08), (0, -d / 2 + 0.06, h - 0.1)))
    # a morsa (na ponta direita, para a frente)
    p += [C("caixa_ferramenta", (0.12, 0.16, 0.08), (w / 2 - 0.15, -d / 2 + 0.06, h + 0.04)),
          C("caixa_ferramenta", (0.12, 0.03, 0.1), (w / 2 - 0.15, -d / 2 - 0.02, h + 0.05)),
          Z("aco", 0.01, 0.2, (w / 2 - 0.15, -d / 2 - 0.08, h + 0.05), 6, "Y"),
          C("aco", (0.12, 0.012, 0.012), (w / 2 - 0.15, -d / 2 - 0.18, h + 0.05))]
    # o martelo e a caixa de pregos em cima, umas tábuas na prateleira
    p += [C("madeira", (0.28, 0.03, 0.025), (-0.3, 0.05, h + 0.012), giro=(0, 0, 15)),
          C("aco_escuro", (0.035, 0.1, 0.035), (-0.165, 0.09, h + 0.017), giro=(0, 0, 15)),
          C("papelao", (0.1, 0.07, 0.05), (0.2, 0.15, h + 0.025)),
          C("madeira_clara", (0.9, 0.12, 0.025), (-0.1, 0.05, 0.23)),
          C("madeira", (0.8, 0.1, 0.025), (0.05, -0.05, 0.255), giro=(0, 0, 3))]
    return p


# ------------------------------------------------------------------ a luz (01/11/2026: a lanterna vira item, com pilhas)

def _lanterna():
    # a lanterna de mão deitada (o comprido em X): o cabo de alumínio preto com os anéis da pega, a cabeça mais larga
    # com a lente amarelada, o botão de borracha vermelho, a tampa de trás e a argola da cordinha
    r, z = 0.018, 0.0275
    p = [
        Z("preto", r, 0.15, (-0.01, 0, z), 12, "X"),
        Z("aco_escuro", 0.027, 0.045, (0.085, 0, z + 0.004), 12, "X"),
        Z("aco_escuro", 0.024, 0.012, (0.06, 0, z + 0.002), 12, "X"),
        Z("creme", 0.023, 0.004, (0.109, 0, z + 0.004), 12, "X"),
        Z("aco_escuro", 0.0195, 0.014, (-0.09, 0, z), 12, "X"),
        C("vermelho", (0.014, 0.01, 0.006), (0.03, 0, z + r + 0.001), chanfro=0.002),
        C("aco", (0.006, 0.012, 0.012), (-0.101, 0, z), chanfro=0.001),
    ]
    for x in (-0.06, -0.045, -0.03, -0.015, 0.0):
        p.append(Z("cinza_escuro", r * 1.04, 0.005, (x, 0, z), 12, "X"))
    return p


def _pilhas():
    # um par de pilhas grandes deitadas lado a lado: o corpo preto, a faixa cor de cobre e os polos de metal
    p = []
    for y in (-0.0135, 0.0135):
        r = 0.013
        p += [
            Z("preto", r, 0.05, (0, y, r), 12, "X"),
            Z("dourado", r * 1.005, 0.017, (0.0165, y, r), 12, "X"),
            Z("prata", 0.0045, 0.004, (0.027, y, r), 8, "X"),
            Z("prata", r * 0.85, 0.002, (-0.0255, y, r), 12, "X"),
        ]
    return p


# nome do modelo -> (as peças, as células do ícone, onde a câmera do ícone olha de, onde a da foto)
MODELOS = {
    "Feijao": _feijao, "Carne": _carne, "Milho": _milho, "Sopa": _sopa, "Salgadinho": _salgadinho,
    "Chocolate": _chocolate, "BarraDeEnergia": _barra,
    "CopoDeAgua": _copo, "RefrigeranteGarrafa": _refri_garrafa, "RefrigeranteLata": _refri_lata, "Energetico": _energetico,
    "Leite": _leite, "Suco": _suco, "GarrafaTermica": _termica,
    "Remedio": _xarope, "Comprimidos": _comprimidos, "KitMedico": _kit_medico, "Atadura": _atadura, "Gaze": _gaze,
    "Desinfetante": _desinfetante, "SprayAntisseptico": _spray, "Antibiotico": _antibiotico, "Bandagem": _bandagem,
    "Madeira": _madeira, "Toras": _toras, "Pano": _pano, "Corda": _corda, "FitaAdesiva": _fita, "Ferramenta": _ferramenta,
    "PecasDeMetal": _pecas, "Fios": _fios, "Pregos": _pregos, "Isca": _isca, "VaraDePesca": _vara,
    "KitDeReparo": _kit_reparo, "Roupa": _roupa,
    "Fogueira": _kit_fogueira, "Cama": _kit_cama, "Barricada": _kit_barricada,
    "Lanterna": _lanterna, "Pilhas": _pilhas,
}
# as construções montadas no mundo e a bancada (sem ícone)
MONTADAS = {
    "FogueiraMontada": _fogueira_montada, "CamaMontada": _cama_montada, "BarricadaMontada": _barricada_montada,
    "Bancada": _bancada,
}
# os deitados (vistos mais de cima no ícone)
DEITADOS = {"Madeira", "Toras", "Pano", "Corda", "FitaAdesiva", "Ferramenta", "PecasDeMetal", "Fios", "Pregos", "VaraDePesca",
            "Roupa", "Fogueira", "Cama", "Barricada", "Atadura", "Bandagem", "Lanterna", "Pilhas"}

SAIDA = os.path.join(eq.UNITY, "Loot.fbx")


def _cena():
    return bpy.context.scene


def construir():
    cena = _cena()
    for o in list(cena.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    todos = list(MODELOS.items()) + list(MONTADAS.items())
    for i, (nome, f) in enumerate(todos):
        o = eq._objeto(nome, f(), cena)
        o.location = ((i % 8) * 0.8, (i // 8) * 0.8, 0)
    print(f"loot: {len(todos)} modelos, {sum(len(o.data.polygons) for o in cena.objects if o.type == 'MESH')} faces")


def exportar():
    cena = _cena()
    for o in cena.objects:
        o.select_set(o.type == "MESH")
    bpy.ops.export_scene.fbx(filepath=SAIDA, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")
    print("loot: gravado", SAIDA)


def imagens(so=None):
    """Os ícones (128 x 128) e as fotos (600 x 500, de 3/4), no estilo dos outros: o Workbench com a textura, a
    cavidade, o contorno e o fundo transparente."""
    cena = _cena()
    bpy.context.view_layer.update()   # as posições dos objetos (sem isto, a câmera mirava a origem e saía vazio)
    r = cena.render
    try:
        r.engine = "BLENDER_WORKBENCH"
    except TypeError:
        pass
    r.film_transparent = True
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGBA"
    s = cena.display.shading
    s.light = "STUDIO"
    s.color_type = "TEXTURE"
    s.show_cavity = True
    s.cavity_type = "BOTH"
    s.show_object_outline = True
    s.object_outline_color = (0.05, 0.05, 0.05)
    cena.view_settings.view_transform = "Standard"
    objs = {o.name.split("_")[0]: o for o in cena.objects if o.type == "MESH"}
    for nome, o in objs.items():
        if nome not in MODELOS or (so and nome not in so):
            continue
        for outro in objs.values():
            outro.hide_render = outro is not o
        cantos = [o.matrix_world @ Vector(c) for c in o.bound_box]
        centro = sum(cantos, Vector()) / 8
        tam = max((max(c[i] for c in cantos) - min(c[i] for c in cantos)) for i in range(3))
        deitado = nome in DEITADOS
        icone = (0.35, -0.7, 1.0) if deitado else (0.45, -1.0, 0.45)
        foto = (0.6, -0.9, 1.0) if deitado else (0.8, -1.0, 0.6)
        r.resolution_x, r.resolution_y = 128, 128
        eq._camera(cena, True, centro, icone, 3.0, tam * (1.45 if nome in ("Atadura", "Bandagem", "FitaAdesiva") else 1.3))
        r.filepath = os.path.join(eq.ICONES, f"Icone_{nome}.png")
        bpy.ops.render.render(write_still=True)
        r.resolution_x, r.resolution_y = 600, 500
        eq._camera(cena, False, centro, foto, tam * 2.4, tam)
        r.filepath = os.path.join(eq.ICONES, f"Foto_{nome}.png")
        bpy.ops.render.render(write_still=True)
    for o in objs.values():
        o.hide_render = False


def prancha(caminho):
    """Uma foto de todos juntos (para conferir), de 3/4."""
    cena = _cena()
    r = cena.render
    r.resolution_x, r.resolution_y = 1600, 1000
    cantos = [o.matrix_world @ Vector(c) for o in cena.objects if o.type == "MESH" for c in o.bound_box]
    lo = Vector((min(c.x for c in cantos), min(c.y for c in cantos), min(c.z for c in cantos)))
    hi = Vector((max(c.x for c in cantos), max(c.y for c in cantos), max(c.z for c in cantos)))
    eq._camera(cena, True, (lo + hi) / 2, (0.0, -1.0, 1.1), 20.0, (hi - lo).x * 1.1)
    r.filepath = caminho
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    etapa = args[0] if args else "tudo"
    construir()
    if etapa in ("exportar", "tudo"):
        exportar()
    if etapa in ("imagens", "tudo"):
        imagens(args[1:] or None)
    if etapa == "prancha":
        prancha(args[1])
