# O saque (31/10/2026, a pedido: "precisamos criar alguns loots ... mais variações de latas de comida e de água,
# refrigerantes e algumas matérias-primas para craft"; cada item numa célula do inventário, por enquanto): a comida, a
# bebida, os remédios, os materiais e os kits de montar, o Loot.fbx, os ícones e as fotos do inventário. Também as
# construções montadas no mundo (a fogueira, a cama e a barricada) e a bancada de trabalho.
#
# Como rodar (Blender 5.2, em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/loot.py -- tudo
#   (as etapas: construir, exportar, imagens; "imagens Feijao Carne" só as de alguns; "folha <saída.png>
#   <PrincipalBloco.blend>", a folha das peças do fumo ao lado da mão do personagem principal)
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

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector

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
    # o fumo (03/11/2026): o filtro cor de cortiça e a cinza
    "filtro": (9, 13), "cinza_claro": (9, 3),
    # a pesca (06/11/2026): o grafite da vara e as cores dos peixes (todas já existiam na paleta)
    "grafite": (11, 1), "azul_aco": (9, 2), "verde_cinza": (5, 3), "verde_cinza_escuro": (9, 0), "perola": (5, 5),
    "musgo_escuro": (2, 1), "verde_quase_preto": (7, 1),
})

_caixa = eq._caixa
_cilindro = eq._cilindro


def C(cor, tam, centro, giro=(0, 0, 0), chanfro=0.0):
    return (_caixa, cor, tam, centro, giro, chanfro)


def Z(cor, raio, altura, centro, lados=12, eixo="Z"):
    return (_cilindro, cor, raio, altura, centro, eixo, lados)


def _ripa(bm, uv, cor, a, b, largura, grossura, lado=(0.0, 1.0, 0.0)):
    """Uma barra de a até b: a largura na direção do lado (sem a parte ao longo da barra) e a grossura na outra."""
    a, b = Vector(a), Vector(b)
    z = (b - a).normalized()
    x = Vector(lado) - z * z.dot(Vector(lado))
    if x.length < 1e-6:
        x = Vector((1.0, 0.0, 0.0)) - z * z.x
    x.normalize()
    y = z.cross(x)
    base = Matrix((x, y, z)).transposed().to_4x4()
    vs = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    m = Matrix.Translation((a + b) / 2) @ base @ Matrix.Diagonal((largura, grossura, (b - a).length, 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=vs)
    eq._pintar(bm, uv, list({f for v in vs for f in v.link_faces}), cor)


def R(cor, a, b, largura, grossura, lado=(0.0, 1.0, 0.0)):
    return (_ripa, cor, a, b, largura, grossura, lado)


def _placa(bm, uv, cor, pontos, z0, z1):
    """Um polígono convexo no plano XY (os pontos (x, y) em volta) estendido de z0 a z1 (as nadadeiras, a serra)."""
    a = [bm.verts.new((x, y, z0)) for x, y in pontos]
    b = [bm.verts.new((x, y, z1)) for x, y in pontos]
    n = len(pontos)
    faces = [bm.faces.new(a[::-1]), bm.faces.new(b)]
    for k in range(n):
        faces.append(bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k])))
    eq._pintar(bm, uv, faces, cor)


def F(cor, pontos, z0, z1):
    return (_placa, cor, pontos, z0, z1)


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


def _bandagem_antisseptica():
    # A bandagem antisséptica (05/11/2026, a pedido: o sistema de vida, a etapa 2; "1 álcool + 3 bandagens = 3"): o rolo
    # da bandagem, mas branco como a atadura (o miolo creme), com duas faixas verdes (o verde do spray) no lugar da
    # vinho e uma cruz verde na ponta solta, para não confundir com a bandagem comum no ícone de 128 px.
    # O mesmo tamanho da bandagem, o meio na origem (o espaço da lata).
    return [
        Z("branco", 0.026, 0.07, (0, 0, 0), 10, "X"),
        Z("creme", 0.02, 0.072, (0, 0, 0), 8, "X"),
        C("branco", (0.068, 0.002, 0.045), (0, -0.026, -0.024)),
        C("verde", (0.007, 0.055, 0.055), (0.022, 0, 0)),
        C("verde", (0.007, 0.055, 0.055), (-0.022, 0, 0)),
        C("verde", (0.018, 0.002, 0.006), (0, -0.0275, -0.03)),
        C("verde", (0.006, 0.002, 0.018), (0, -0.0275, -0.03)),
    ]


def _tala():
    # A tala (05/11/2026, a pedido: o sistema de vida, "a Tala imobiliza o membro quebrado"): a tala de alumínio
    # dobrável (a SAM splint) enrolada e deitada, o rolo achatado com o eixo em Y (a ponta do rolo para a frente, -Y,
    # para a espiral aparecer no ícone): laranja por fora e azul por dentro, cada volta uma lâmina com as duas cores,
    # 3 voltas em espiral (o vão escuro entre elas) e a ponta de fora solta em cima, um pouco levantada (o azul por baixo).
    # Uns 0,10 x 0,11 x 0,06 m, o meio na origem (o espaço da lata, como a atadura).
    larg = 0.11                    # a largura da tala (o comprimento do rolo, em Y)
    a0, b0 = 0.05, 0.03            # os semieixos da volta de fora (X e Z: o rolo achatado)
    passo, esp = 0.0055, 0.0042    # de uma volta à outra; a grossura da lâmina (metade laranja, metade azul)
    voltas, n = 3, 18              # as voltas e os pedaços por volta
    p = []

    def lamina(x0, z0, x1, z1, extra=0.0012):
        # um pedaço reto da lâmina entre dois pontos do corte (XZ): o laranja para fora, o azul para dentro
        dx, dz = x1 - x0, z1 - z0
        comp = math.hypot(dx, dz)
        nx, nz = dz / comp, -dx / comp          # a normal para fora (a espiral anda no sentido anti-horário)
        mx, mz = (x0 + x1) / 2, (z0 + z1) / 2
        g = math.degrees(math.atan2(-dz, dx))   # o giro em Y que leva o X da caixa ao longo do pedaço
        for cor, lado in (("laranja_vivo", 1), ("azul", -1)):
            k = lado * esp / 4
            p.append(C(cor, (comp + extra, larg, esp / 2), (mx + nx * k, 0, mz + nz * k), giro=(0, g, 0)))

    def ponto(t):
        # a espiral: começa em cima (t = 0) e entra a cada volta um passo
        encolhe = passo * t / (2 * math.pi)
        return (a0 - encolhe) * math.cos(t + math.pi / 2), (b0 - encolhe) * math.sin(t + math.pi / 2)

    ts = [2 * math.pi * voltas * i / (voltas * n) for i in range(voltas * n + 1)]
    for t0, t1 in zip(ts, ts[1:]):
        lamina(*ponto(t0), *ponto(t1))
    # a ponta de fora, solta: sai de cima para +X, levantando um pouco (uns 2,5 cm)
    x0, z0 = ponto(0)
    lamina(x0 + 0.025 * math.cos(math.radians(12)), z0 + 0.025 * math.sin(math.radians(12)), x0, z0)
    return p


def _calmante():
    # O calmante (06/11/2026, a pedido: a sanidade, o Item.Calmante): o frasco de comprimidos de receita, baixo e largo,
    # de plástico claro (o azul-claro do copo), com a tampa de segurança roxa (o roxo da mente), um pouco mais larga que
    # o frasco e com a borda de baixo saliente, e o rótulo branco com a faixa roxa em cima. Para não confundir
    # com o xarope (o vidro âmbar, alto, a tampa branca) nem com os comprimidos (a cartela prateada) no ícone de 128 px.
    # Uns 5 x 9 cm, o meio na origem (o espaço da lata).
    return [
        Z("azul_claro", 0.022, 0.066, (0, 0, -0.013), 12),
        Z("azul_claro", 0.0195, 0.006, (0, 0, 0.022), 10),
        Z("branco", 0.0222, 0.04, (0, 0, -0.016), 12),
        Z("roxo", 0.0224, 0.008, (0, 0, -0.004), 12),
        Z("roxo", 0.0242, 0.02, (0, 0, 0.036), 14),
        Z("roxo", 0.0252, 0.005, (0, 0, 0.0275), 14),
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


# (a vara de pesca, _vara, está na seção da pesca, mais abaixo)


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


# ------------------------------------------------------------------ a gasolina (01/11/2026: o galão repõe o tanque do carro)

def _galao():
    # o galão de plástico vermelho de 10 litros, em pé: o corpo com os painéis em relevo dos lados, a faixa escura de
    # baixo, a alça em cima (dois pés e a pega), o bico na quina da frente com a tampa amarela, o respiro preto atrás e
    # o rótulo amarelo na frente
    p = [
        C("vermelho", (0.30, 0.17, 0.25), (0, 0, 0.125), chanfro=0.025),
        C("vermelho", (0.26, 0.15, 0.03), (0, 0, 0.258), chanfro=0.012),
        C("vermelho_escuro", (0.302, 0.172, 0.014), (0, 0, 0.007), chanfro=0.004),
        C("vermelho", (0.022, 0.03, 0.05), (-0.10, 0, 0.29), chanfro=0.006),
        C("vermelho", (0.022, 0.03, 0.05), (0.02, 0, 0.29), chanfro=0.006),
        C("vermelho", (0.16, 0.032, 0.024), (-0.04, 0, 0.318), chanfro=0.008),
        Z("vermelho", 0.024, 0.035, (0.112, 0, 0.285), 12),
        Z("amarelo", 0.028, 0.03, (0.112, 0, 0.315), 12),
        Z("preto", 0.011, 0.012, (-0.125, 0.045, 0.279), 8),
        C("amarelo", (0.11, 0.004, 0.07), (0.0, -0.0905, 0.15), chanfro=0.002),
        C("preto", (0.08, 0.004, 0.012), (0.0, -0.0925, 0.15)),
    ]
    for y in (-0.086, 0.086):
        p.append(C("vermelho_escuro", (0.22, 0.006, 0.16), (0.0, y, 0.12), chanfro=0.004))
    return p


# ------------------------------------------------------------------ arrombar (02/11/2026: o Plano_Portas.md da pasta de arte)

def _gazua():
    # o estojo de couro aberto, deitado, com as hastes de aço: o tensor em L, o gancho e a de ondas (os cabos pretos)
    p = [
        C("chocolate", (0.11, 0.06, 0.006), (0, 0, 0.003), chanfro=0.002),
        C("madeira_escura", (0.11, 0.004, 0.009), (0, 0.028, 0.0045)),
        C("madeira_escura", (0.11, 0.004, 0.009), (0, -0.028, 0.0045)),
    ]
    for k, y in enumerate((-0.016, 0.0, 0.016)):
        p.append(C("preto", (0.034, 0.009, 0.005), (-0.034, y, 0.0085), chanfro=0.0015))
        p.append(C("aco", (0.062, 0.0028, 0.0018), (0.014, y, 0.0075)))
        # a ponta: o gancho, as ondas, a bola
        if k == 0:
            p.append(C("aco", (0.006, 0.0028, 0.0018), (0.046, y + 0.002, 0.0075), giro=(0, 0, 55)))
        elif k == 1:
            for j in range(3):
                p.append(C("aco", (0.007, 0.0028, 0.0018), (0.034 + j * 0.006, y + (0.0018 if j % 2 else -0.0018), 0.0075), giro=(0, 0, 40 if j % 2 else -40)))
        else:
            p.append(Z("aco", 0.0025, 0.003, (0.046, y, 0.0075), 8))
    # o tensor em L, solto em cima do estojo
    p += [
        C("aco_escuro", (0.07, 0.004, 0.0015), (0.005, 0.034, 0.0075), giro=(0, 0, -12)),
        C("aco_escuro", (0.004, 0.016, 0.0015), (-0.029, 0.04, 0.0075), giro=(0, 0, -12)),
    ]
    return p


def _pe_de_cabra():
    # o pé de cabra deitado (o comprido em X): a barra vermelha com o gancho em J na ponta de trás (a unha de aço com o
    # V) e a ponta chata de aço na da frente, um pouco dobrada
    import math as _m
    z = 0.009
    p = [C("vermelho", (0.47, 0.018, 0.018), (0.0, 0, z), chanfro=0.004)]
    # o gancho: um arco de raio 4,5 cm em volta de (-0.235, 0.045), de -90° a -270°, em 4 pedaços
    cx, cy, r = -0.235, 0.045, 0.045
    angs = [-90, -135, -180, -225, -270]
    for a, b in zip(angs, angs[1:]):
        pa = (cx + r * _m.cos(_m.radians(a)), cy + r * _m.sin(_m.radians(a)))
        pb = (cx + r * _m.cos(_m.radians(b)), cy + r * _m.sin(_m.radians(b)))
        mx, my = (pa[0] + pb[0]) / 2, (pa[1] + pb[1]) / 2
        comp = _m.hypot(pb[0] - pa[0], pb[1] - pa[1]) + 0.012
        p.append(C("vermelho", (comp, 0.018, 0.018), (mx, my, z), giro=(0, 0, _m.degrees(_m.atan2(pb[1] - pa[1], pb[0] - pa[0]))), chanfro=0.004))
    # a unha (aço), voltada para a frente, com o V do meio (duas pontas)
    p += [
        C("aco_escuro", (0.03, 0.02, 0.012), (cx + 0.012, cy + r, z), chanfro=0.002),
        C("aco_escuro", (0.016, 0.007, 0.01), (cx + 0.034, cy + r + 0.0065, z)),
        C("aco_escuro", (0.016, 0.007, 0.01), (cx + 0.034, cy + r - 0.0065, z)),
    ]
    # a ponta chata da frente, larga e fina, dobrada 12°
    p += [
        C("aco", (0.06, 0.024, 0.009), (0.262, 0.004, z), giro=(0, 0, 12), chanfro=0.002),
        C("aco", (0.012, 0.02, 0.004), (0.293, 0.011, z), giro=(0, 0, 12)),
    ]
    return p


# ------------------------------------------------------------------ o fumo (03/11/2026: o Plano_Cigarro.md da pasta de arte)
# O maço de cigarros (o item), as peças do maço aberto (a caixa e a tampa, que gira na dobradiça), o cigarro em peças (o
# filtro, o papel, que o jogo encolhe enquanto ele queima, a ponta apagada, a brasa e a cinza), a bituca e o isqueiro com
# a chama. Tudo 30% maior que o de verdade (a decisão 7: a mão de blocos é 25% maior que uma mão normal, e o cigarro de
# 8 cm sumia entre os dedos). Só o maço tem ícone e foto; as outras peças são das animações e do jogo.

FUMO = 1.3
# o maço (o de verdade: 88 x 55 x 22 mm, a tampa com 26 mm na frente e 13 atrás), a origem no meio
MACO_A, MACO_L, MACO_P = 0.088 * FUMO, 0.055 * FUMO, 0.022 * FUMO
TAMPA_NA_FRENTE, TAMPA_ATRAS = 0.026 * FUMO, 0.013 * FUMO
ZF = MACO_A / 2 - TAMPA_NA_FRENTE   # onde a tampa começa na frente (o alto da caixa na frente)
ZB = MACO_A / 2 - TAMPA_ATRAS       # e atrás (a dobradiça)
# a dobradiça da tampa, no espaço do maço: a tampa gira em volta do X que passa aqui (aberta, uns -105°)
DOBRADICA = (0.0, MACO_P / 2, ZB)
# o cigarro (o de verdade: 84 mm, o filtro com 21, 7,8 mm de grossura), em pé, a boca embaixo: a origem é a emenda do
# filtro com o papel (onde os dedos seguram), o filtro de -FILTRO a 0 e o papel de 0 a PAPEL (o jogo encolhe o papel em Z,
# e a brasa e a cinza vão para a ponta dele)
FILTRO, PAPEL, RAIO_DO_CIGARRO = 0.021 * FUMO, 0.063 * FUMO, 0.0039 * FUMO
# os cigarros dentro do maço (os filtros para cima, em 3 fileiras de 7, 6 e 7): o espaço entre eles, o meio das fileiras
# em y e de onde sai o cigarro que sobe (o meio da fileira da frente, que já está faltando)
ENTRE_OS_CIGARROS = 0.0096
_Y0 = (-MACO_P / 2 + 0.0015 + MACO_P / 2 - 0.0006) / 2
_DY = 0.866 * ENTRE_OS_CIGARROS
SAIDA_DO_CIGARRO = (0.0, _Y0 - _DY)
# a boca do isqueiro (onde a chama nasce), no espaço dele (a origem no meio do corpo de plástico)
BOCA_DO_ISQUEIRO = (0.0, -0.0040, 0.0545)


def _prisma(bm, uv, cores, pontos, x0, x1, cor_lados):
    """Um polígono convexo no plano YZ (os pontos (y, z) em volta) estendido de x0 a x1: cores[k] é a cor da face que
    vai do ponto k ao seguinte, e cor_lados a das duas tampas (as de -X e +X). As normais saem certas no _objeto."""
    a = [bm.verts.new((x0, y, z)) for y, z in pontos]
    b = [bm.verts.new((x1, y, z)) for y, z in pontos]
    n = len(pontos)
    eq._pintar(bm, uv, [bm.faces.new(a[::-1]), bm.faces.new(b)], cor_lados)
    for k in range(n):
        eq._pintar(bm, uv, [bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))], cores[k])


def _tronco(bm, uv, cor, r1, r2, altura, centro, lados=8):
    """Um tronco de cone em pé (o raio r1 embaixo e r2 em cima; 0 é a ponta), com o meio no centro."""
    cx, cy, cz = centro

    def anel(r, z):
        if r <= 0:
            return [bm.verts.new((cx, cy, z))]
        return [bm.verts.new((cx + r * math.cos(2 * math.pi * k / lados), cy + r * math.sin(2 * math.pi * k / lados), z))
                for k in range(lados)]

    baixo, cima = anel(r1, cz - altura / 2), anel(r2, cz + altura / 2)
    faces = []
    if len(baixo) > 1:
        faces.append(bm.faces.new(baixo[::-1]))
    if len(cima) > 1:
        faces.append(bm.faces.new(cima))
    for k in range(lados):
        vs = []
        for v in (baixo[k % len(baixo)], baixo[(k + 1) % lados % len(baixo)], cima[(k + 1) % lados % len(cima)], cima[k % len(cima)]):
            if v not in vs:
                vs.append(v)
        faces.append(bm.faces.new(vs))
    eq._pintar(bm, uv, faces, cor)


def P(cores, pontos, x0, x1, cor_lados):
    return (_prisma, cores, pontos, x0, x1, cor_lados)


def T(cor, r1, r2, altura, centro, lados=8):
    return (_tronco, cor, r1, r2, altura, centro, lados)


def _corpo_do_maco():
    """A caixa do maço, sem a tampa: branca, com a faixa vermelha do nome (os blocos brancos das letras de BLACKWATER, como
    as placas da rua), a linha de baixo dela e o aviso preto embaixo, na frente e atrás."""
    A, L, Pr = MACO_A, MACO_L, MACO_P
    f = -Pr / 2
    p = [P(["branco"] * 4, [(-Pr / 2, -A / 2), (Pr / 2, -A / 2), (Pr / 2, ZB), (-Pr / 2, ZF)], -L / 2, L / 2, "branco")]
    zn = ZF - 0.017
    p.append(C("vermelho", (L - 0.001, 0.0008, 0.016), (0, f - 0.0004, zn)))
    # BLACKWATER: uma letra por bloco (o W mais largo)
    larguras = [0.0042, 0.0042, 0.0042, 0.0042, 0.0042, 0.006, 0.0042, 0.0042, 0.0042, 0.0042]
    x = -(sum(larguras) + 0.0013 * 9) / 2
    for w in larguras:
        p.append(C("branco", (w, 0.0006, 0.0072), (x + w / 2, f - 0.0010, zn)))
        x += w + 0.0013
    # a linha de baixo ("20 CLASS A CIGARETTES") e o aviso preto com as duas linhas brancas, na frente e atrás
    p.append(C("cinza", (0.034, 0.0006, 0.002), (0, f - 0.0003, zn - 0.014)))
    for s in (-1, 1):
        y = s * (Pr / 2 + 0.0004)
        p.append(C("preto", (0.058, 0.0008, 0.024), (0, y, -A / 2 + 0.019)))
        for dz, w in ((0.004, 0.046), (-0.003, 0.034)):
            p.append(C("branco", (w, 0.0006, 0.0022), (0, y + s * 0.0005, -A / 2 + 0.019 + dz)))
    return p


def _pecas_da_tampa(o=(0.0, 0.0, 0.0)):
    """A tampa vermelha (o lado de dentro creme) com o brasão dourado na frente, no espaço do maço menos o ponto o."""
    A, L, Pr = MACO_A, MACO_L, MACO_P
    ox, oy, oz = o
    pts = [(-Pr / 2 - oy, ZF - oz), (Pr / 2 - oy, ZB - oz), (Pr / 2 - oy, A / 2 - oz), (-Pr / 2 - oy, A / 2 - oz)]
    zc = (ZF + A / 2) / 2 - oz
    return [
        P(["creme", "vermelho", "vermelho", "vermelho"], pts, -L / 2 - ox, L / 2 - ox, "vermelho"),
        C("dourado", (0.016, 0.0008, 0.013), (-ox, -Pr / 2 - 0.0004 - oy, zc)),
        C("vermelho_escuro", (0.008, 0.0006, 0.007), (-ox, -Pr / 2 - 0.0010 - oy, zc)),
        # a fita do lacre, por cima da tampa
        C("dourado", (L - 0.001, 0.003, 0.0006), (-ox, -oy, A / 2 + 0.0003 - oz)),
    ]


def _maco():
    """O maço fechado (o item: no chão, na mochila e no ícone), a origem no meio."""
    return _corpo_do_maco() + _pecas_da_tampa()


def _caixa_do_maco():
    """O maço aberto sem a tampa: a gola de papelão creme subindo na frente e nos lados, o papel-alumínio rasgado e os
    filtros dos cigarros (17: faltam o do meio da fileira da frente, o que sai, e um do meio e um de trás)."""
    A, L, Pr = MACO_A, MACO_L, MACO_P
    p = _corpo_do_maco()
    e = 0.0008
    p.append(C("creme", (L - 0.0012, e, 0.020), (0, -Pr / 2 + 0.0003 + e / 2, ZF + 0.002)))
    for s in (-1, 1):
        p.append(C("creme", (e, 0.009, 0.020), (s * (L / 2 - 0.0003 - e / 2), -Pr / 2 + 0.0045, ZF + 0.002)))
    # o papel-alumínio em volta dos cigarros, até um pouco acima da dobradiça, e a aba rasgada de trás, caída para trás
    zfo = ZB + 0.002
    p.append(C("prata", (L - 0.0012, Pr - 0.0021, zfo - (ZF - 0.004)), (0, _Y0, (zfo + ZF - 0.004) / 2)))
    p.append(C("prata", (0.03, 0.0006, 0.012), (0.012, Pr / 2 - 0.0016, zfo + 0.005), giro=(-25, 0, 4)))
    topo = A / 2 - 0.0025
    r = ENTRE_OS_CIGARROS / 2 - 0.0001
    faltam = {(0, 3), (1, 0), (2, 5)}
    for fila, (n, y) in enumerate(((7, _Y0 - _DY), (6, _Y0), (7, _Y0 + _DY))):
        for k in range(n):
            if (fila, k) in faltam:
                continue
            x = (k - (n - 1) / 2) * ENTRE_OS_CIGARROS
            p.append(Z("filtro", r, topo - ZB, (x, y, (topo + ZB) / 2), 6))
            p.append(Z("creme", r * 0.8, 0.0006, (x, y, topo + 0.0002), 6))
    return p


def _tampa_do_maco():
    """A tampa sozinha, com a origem na dobradiça (fechada, ela cai em cima da caixa quando vai para o DOBRADICA)."""
    return _pecas_da_tampa(DOBRADICA)


def _cigarro_filtro():
    """O filtro cor de cortiça, de -FILTRO a 0, com a ponta creme (a da boca) e o friso dourado perto do papel."""
    F, R = FILTRO, RAIO_DO_CIGARRO
    return [
        Z("filtro", R, F, (0, 0, -F / 2), 8),
        Z("creme", R * 0.82, 0.0006, (0, 0, -F - 0.0002), 8),
        Z("dourado", R * 1.02, 0.0012, (0, 0, -0.0022), 8),
    ]


def _cigarro_papel():
    """O papel branco, de 0 a PAPEL (o jogo encolhe em Z enquanto ele queima)."""
    return [Z("branco", RAIO_DO_CIGARRO * 0.99, PAPEL, (0, 0, PAPEL / 2), 8)]


def _cigarro_ponta():
    """A ponta apagada (o fumo aparecendo na boca do papel), a origem na ponta do papel."""
    return [Z("chocolate", RAIO_DO_CIGARRO * 0.85, 0.0008, (0, 0, 0.0001), 8)]


def _cigarro_brasa():
    """A brasa (a faixa laranja que o jogo acende), a origem na ponta do papel."""
    return [Z("laranja_vivo", RAIO_DO_CIGARRO * 0.97, 0.0020, (0, 0, 0.0020), 8)]


def _cigarro_cinza():
    """A beira queimada do papel e a cinza da ponta, em volta da brasa, a origem na ponta do papel."""
    R = RAIO_DO_CIGARRO
    return [
        Z("carvao", R, 0.0012, (0, 0, 0.0004), 8),
        T("cinza_claro", R * 0.9, R * 0.5, 0.0025, (0, 0, 0.00425), 8),
    ]


def _bituca():
    """A bituca deitada (o comprido em X), a origem no meio: o filtro e o toco de papel amassado, queimado na ponta."""
    F, R = FILTRO, RAIO_DO_CIGARRO
    x0 = -0.01865
    j = x0 + F   # a emenda
    d = (math.cos(math.radians(18)), 0, -math.sin(math.radians(18)))   # o toco dobrado 18° para baixo

    def em(t):
        return (j + d[0] * t, 0, d[2] * t)

    return [
        Z("filtro", R, F, (x0 + F / 2, 0, 0), 8, "X"),
        Z("creme", R * 0.82, 0.0006, (x0 - 0.0002, 0, 0), 8, "X"),
        Z("dourado", R * 1.02, 0.0012, (j - 0.0022, 0, 0), 8, "X"),
        C("branco", (0.010, 0.0112, 0.0058), em(0.005), giro=(0, 18, 0)),
        C("carvao", (0.0025, 0.0102, 0.0052), em(0.0112), giro=(0, 18, 0)),
        C("cinza_claro", (0.0015, 0.006, 0.004), em(0.0128), giro=(0, 18, 0)),
    ]


def _isqueiro():
    """O isqueiro de plástico azul, em pé, a origem no meio do corpo: o capuz de metal baixo com as duas orelhas, a roda
    da pedra entre elas, a alavanca do gás atrás da roda e o bico na frente (a chama nasce no BOCA_DO_ISQUEIRO)."""
    p = [
        C("azul", (0.0325, 0.0182, 0.087), (0, 0, 0), chanfro=0.006),
        C("azul_escuro", (0.0315, 0.0172, 0.004), (0, 0, -0.0425), chanfro=0.0012),
        C("aco", (0.027, 0.0155, 0.009), (0, 0, 0.0465), chanfro=0.0012),
        Z("aco_escuro", 0.0050, 0.012, (0, 0.0025, 0.0565), 12, "X"),
        C("preto", (0.010, 0.0055, 0.006), (0, 0.0075, 0.053), chanfro=0.001),
        Z("carvao", 0.0016, 0.004, (0, -0.0040, 0.0525), 6),
    ]
    for s in (-1, 1):
        p.append(C("aco", (0.003, 0.0125, 0.012), (s * 0.0095, 0.0005, 0.0555), chanfro=0.0008))
    return p


def _isqueiro_chama():
    """A chama (que o jogo acende), a origem na boca do isqueiro: a gota, com a base azul, a barriga laranja e a ponta
    amarela."""
    return [
        T("azul_claro", 0.0012, 0.0032, 0.0025, (0, 0, 0.00125), 8),
        T("laranja_vivo", 0.0032, 0.0052, 0.0040, (0, 0, 0.0045), 8),
        T("amarelo", 0.0052, 0.0046, 0.0040, (0, 0, 0.0085), 8),
        T("amarelo", 0.0046, 0.0, 0.0120, (0, 0, 0.0165), 8),
    ]


# ------------------------------------------------------------------ o prato (04/11/2026: o Plano_NPCs_Bunker.md da pasta de arte, a parte 4)
# O prato de comida que o cozinheiro do bunker faz: o prato fundo de inox com o ensopado (o molho, a carne, a cenoura, a
# batata, a ervilha e o feijão) e a colher; o prato vazio (os restos e a colher) e o estragado (o mofo e a mosca). Os
# três são itens, com o ícone e a foto. As peças, sem ícone, são das animações e do jogo: o prato sozinho, a comida (a
# origem no fundo do prato, como ele: o jogo a encolhe em z enquanto o morador come), a comida estragada, os restos, a
# colher (a origem onde os dedos seguram, ao longo de +X, a concha na ponta) e o bocado na colher (a mesma origem). Tudo
# 30% maior que o de verdade, como o fumo (a mão de blocos e a mobília em ×1,30): o prato tem 29 cm. A origem do prato é
# embaixo, no meio.

PRATO = 1.3
# onde a colher fica no prato cheio e no vazio (no espaço do prato: a pegada, o giro em z e a inclinação em y)
# (no cheio, o cabo apoiado na aba e a concha no meio do ensopado; no vazio, a concha no fundo e o cabo subindo pela aba)
COLHER_NO_PRATO = ((0.072 * PRATO, -0.040 * PRATO, 0.036 * PRATO), 151.0, 2.0)
COLHER_NO_VAZIO = ((0.060 * PRATO, 0.045 * PRATO, 0.0425), 217.0, 12.0)

eq.CORES.update({
    "molho": (2, 3), "carne_cozida": (8, 4), "batata": (9, 13), "cenoura": (11, 8), "ervilha": (11, 10),
    "feijao_cozido": (8, 13), "inox": (11, 15), "inox_fora": (8, 11),
    "molho_estragado": (7, 6), "estragado_escuro": (5, 10), "mofo": (2, 8), "mofo_claro": (2, 12), "mofo_branco": (10, 5),
})


def _torno(bm, uv, pontos, lados=20, centro=(0.0, 0.0, 0.0)):
    """Uma peça de torno: os pontos (raio, z, cor) girados em volta de z; a cor de cada ponto é a do trecho que vai dele
    ao próximo. Um raio 0 numa ponta fecha ela num ponto (o prato é fechado: começa e acaba no eixo)."""
    cx, cy, cz = centro
    aneis = []
    for r, z, _ in pontos:
        if r <= 1e-7:
            aneis.append([bm.verts.new((cx, cy, cz + z))])
        else:
            aneis.append([bm.verts.new((cx + r * math.cos(2 * math.pi * i / lados), cy + r * math.sin(2 * math.pi * i / lados),
                                        cz + z)) for i in range(lados)])
    for k in range(len(aneis) - 1):
        a, b = aneis[k], aneis[k + 1]
        faces = []
        for i in range(lados):
            j = (i + 1) % lados
            if len(a) == 1 and len(b) == 1:
                continue
            if len(a) == 1:
                vs = [a[0], b[j], b[i]]
            elif len(b) == 1:
                vs = [a[i], a[j], b[0]]
            else:
                vs = [a[i], a[j], b[j], b[i]]
            faces.append(bm.faces.new(vs))
        eq._pintar(bm, uv, faces, pontos[k][2])


def _elipsoide(bm, uv, cor, raios, centro, giro=(0, 0, 0), u=8, v=5):
    """Uma bolha (o bocado, o mofo, a mosca): a esfera baixa achatada pelos raios (x, y, z), girada (graus, X Y Z)."""
    r = bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=1.0)
    m = Matrix.Translation(Vector(centro)) @ Matrix.Rotation(math.radians(giro[2]), 4, "Z") @ \
        Matrix.Rotation(math.radians(giro[1]), 4, "Y") @ Matrix.Rotation(math.radians(giro[0]), 4, "X") @ \
        Matrix.Diagonal((raios[0], raios[1], raios[2], 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=r["verts"])
    eq._pintar(bm, uv, list({f for vv in r["verts"] for f in vv.link_faces}), cor)


def _com_matriz(bm, uv, M, pecas):
    """As peças (no espaço delas) levadas por M (a colher no prato). As novas são achadas pelo conjunto, e não pela posição
    na lista: o chanfro apaga vértices, e os novos ocupam os buracos que ficaram antes (pela posição, um pedaço de cenoura
    do ensopado ia junto com a colher)."""
    antes = set(bm.verts)
    for p in pecas:
        p[0](bm, uv, *p[1:])
    bmesh.ops.transform(bm, matrix=M, verts=[v for v in bm.verts if v not in antes])


def E(cor, raios, centro, giro=(0, 0, 0), u=8, v=5):
    return (_elipsoide, cor, raios, centro, giro, u, v)


def _no_lugar(lugar):
    (x, y, z), giro, inclina = lugar
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(giro), 4, "Z") @ \
        Matrix.Rotation(math.radians(inclina), 4, "Y")


def _prato_fundo():
    """O prato fundo de inox (22 cm de verdade): o pé, a parede, a aba larga e o lado de dentro."""
    k = PRATO
    pontos = [(0.0, 0.003, "aco"), (0.064, 0.003, "aco"), (0.067, 0.0, "aco"), (0.073, 0.0, "inox_fora"),
              (0.076, 0.005, "inox_fora"), (0.086, 0.026, "inox_fora"), (0.110, 0.034, "inox"), (0.112, 0.037, "inox"),
              (0.110, 0.039, "inox"), (0.085, 0.031, "inox"), (0.073, 0.009, "inox"), (0.0, 0.006, "inox")]
    return [(_torno, [(r * k, z * k, c) for r, z, c in pontos], 24)]


def _altura_da_comida(r):
    """O alto do monte de comida no raio r (de verdade, sem o PRATO): o perfil do _monte."""
    perfil = [(0.0, 0.030), (0.032, 0.028), (0.055, 0.022), (0.068, 0.014), (0.071, 0.0085)]
    for (r0, z0), (r1, z1) in zip(perfil, perfil[1:]):
        if r <= r1:
            return z0 + (z1 - z0) * (r - r0) / (r1 - r0)
    return perfil[-1][1]


def _monte(cor, cores_dos_pedacos, seed, n=18):
    """O monte de comida no prato (o espaço do prato): o domo e os pedaços por cima, das cores sorteadas pelos pesos."""
    import random
    k = PRATO
    rnd = random.Random(seed)
    pontos = [(0.0, 0.0062, cor), (0.071, 0.0085, cor), (0.068, 0.014, cor), (0.055, 0.022, cor), (0.032, 0.028, cor),
              (0.0, 0.030, cor)]
    pecas = [(_torno, [(r * k, z * k, c) for r, z, c in pontos], 16)]
    total = sum(p for _, _, p in cores_dos_pedacos)
    for _ in range(n):
        x = rnd.uniform(0, total)
        for c, tam, p in cores_dos_pedacos:
            x -= p
            if x <= 0:
                break
        a = rnd.uniform(0, 2 * math.pi)
        r = math.sqrt(rnd.random()) * 0.058
        s = tam * rnd.uniform(0.75, 1.15)
        z = _altura_da_comida(r) + s * 0.2
        pecas.append(C(c, (s * k, s * rnd.uniform(0.7, 1.0) * k, s * 0.75 * k), (r * math.cos(a) * k, r * math.sin(a) * k, z * k),
                       giro=(rnd.uniform(-25, 25), rnd.uniform(-25, 25), rnd.uniform(0, 90)), chanfro=0.0012))
    return pecas


def _comida():
    return _monte("molho", [("carne_cozida", 0.016, 3), ("cenoura", 0.013, 2.5), ("batata", 0.016, 2.5),
                            ("ervilha", 0.006, 1.5), ("feijao_cozido", 0.008, 1.2)], 4401, 22)


def _comida_estragada():
    import random
    p = _monte("molho_estragado", [("estragado_escuro", 0.015, 3), ("mofo", 0.012, 2), ("batata", 0.014, 0.6)], 4401, 18)
    rnd = random.Random(4402)
    k = PRATO
    # as manchas de mofo felpudo por cima, o branco esverdeado e o claro
    for i in range(9):
        a = rnd.uniform(0, 2 * math.pi)
        r = math.sqrt(rnd.random()) * 0.055
        z = _altura_da_comida(r)
        s = rnd.uniform(0.008, 0.016)
        p.append(E("mofo_branco" if i % 3 else "mofo_claro", (s * k, s * 0.8 * k, 0.003 * k),
                   (r * math.cos(a) * k, r * math.sin(a) * k, (z + 0.0015) * k), (0, 0, rnd.uniform(0, 180))))
    return p


def _fundo_do_prato(r):
    """O alto do fundo do prato por dentro no raio r (de verdade, sem o PRATO)."""
    return 0.006 + 0.003 * min(r, 0.073) / 0.073


def _restos():
    """Os restos no prato vazio: as manchas de molho no fundo, umas ervilhas, um feijão e um pedaço de cenoura."""
    import random
    rnd = random.Random(4410)
    k = PRATO
    p = []
    for i in range(11):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(0.0, 0.055)
        p.append(C("molho", (rnd.uniform(0.015, 0.042) * k, rnd.uniform(0.008, 0.022) * k, 0.0012 * k),
                   (r * math.cos(a) * k, r * math.sin(a) * k, (_fundo_do_prato(r) + 0.0003) * k), giro=(0, 0, rnd.uniform(0, 180)),
                   chanfro=0))
    for c, x, y in (("ervilha", 0.03, -0.02), ("ervilha", -0.02, 0.035), ("feijao_cozido", 0.045, 0.02), ("cenoura", -0.04, -0.03)):
        z = _fundo_do_prato(math.hypot(x, y)) + 0.0022
        p.append(C(c, (0.007 * k, 0.006 * k, 0.005 * k), (x * k, y * k, z * k), giro=(0, 0, rnd.uniform(0, 90)), chanfro=0.001))
    return p


def _colher():
    """A colher de inox (17,5 cm de verdade), ao longo de +X com a concha na ponta, a origem na pegada (a uns 6 cm do fim
    do cabo): o cabo chato, a ponta redonda dele, o pescoço descendo e a concha."""
    k = PRATO
    return [
        C("inox", (0.085 * k, 0.014 * k, 0.003 * k), (-0.0175 * k, 0, 0), chanfro=0.0008),
        Z("inox", 0.0085 * k, 0.003 * k, (-0.060 * k, 0, 0), 10),
        C("inox", (0.028 * k, 0.007 * k, 0.003 * k), (0.038 * k, 0, -0.002 * k), giro=(0, 10, 0), chanfro=0.0006),
        E("inox", (0.030 * k, 0.020 * k, 0.0055 * k), (0.078 * k, 0, -0.0055 * k), u=10, v=5),
    ]


def _bocado():
    """O bocado na concha da colher (a mesma origem da colher)."""
    k = PRATO
    return [
        E("molho", (0.022 * k, 0.014 * k, 0.006 * k), (0.078 * k, 0, 0.0005 * k)),
        C("cenoura", (0.008 * k, 0.007 * k, 0.005 * k), (0.072 * k, 0.003 * k, 0.005 * k), giro=(0, 0, 30), chanfro=0.001),
        C("carne_cozida", (0.009 * k, 0.008 * k, 0.006 * k), (0.085 * k, -0.004 * k, 0.005 * k), giro=(0, 0, 70), chanfro=0.001),
    ]


def _mosca():
    """A mosca pousada na aba do prato estragado (o corpo, a cabeça e as asas)."""
    k = PRATO
    x, z = 0.098 * k, 0.0372 * k
    return [
        E("carvao", (0.0045 * k, 0.0025 * k, 0.0022 * k), (x, 0.0, z + 0.0022 * k), (0, 0, 20)),
        E("vermelho_escuro", (0.0018 * k, 0.0018 * k, 0.0016 * k), (x + 0.004 * k, 0.0015 * k, z + 0.0024 * k)),
        C("cinza_claro", (0.006 * k, 0.003 * k, 0.0004 * k), (x - 0.002 * k, 0.003 * k, z + 0.0045 * k), giro=(0, 0, 50), chanfro=0),
        C("cinza_claro", (0.006 * k, 0.003 * k, 0.0004 * k), (x - 0.002 * k, -0.002 * k, z + 0.0045 * k), giro=(0, 0, -10), chanfro=0),
    ]


def _prato_de_comida():
    return _prato_fundo() + _comida() + [(_com_matriz, _no_lugar(COLHER_NO_PRATO), _colher())]


def _prato_vazio():
    return _prato_fundo() + _restos() + [(_com_matriz, _no_lugar(COLHER_NO_VAZIO), _colher())]


def _prato_estragado():
    return _prato_fundo() + _comida_estragada() + _mosca()


# ------------------------------------------------------------------ a pesca (06/11/2026, a pedido: a vara refeita, os peixes e a pólvora)
# A vara de pesca nova (a de antes, os dois gomos deitados lado a lado, "achei um pouco ruim"): a vara montada, de 2 m em
# dois gomos (a emenda no meio, com as amarrações), afinando até a ponta; o cabo de cortiça atrás e na frente, a tampa de
# borracha, o porta-molinete grafite com as duas roscas de aço; o molinete pendurado embaixo (o pé, a haste, a caixa, o
# rotor, o carretel com a linha amarela, o freio vermelho, a alça de arame e a manivela); 5 passadores embaixo, cada vez
# menores, com as amarrações vermelhas, e a ponteira.
# Ela vai na mão para pescar: a origem é a pegada (o pé do molinete, no eixo da vara), a ponta para a frente (-Y no
# Blender, +Z na Unity), o molinete embaixo (-Z; -Y na Unity) e a manivela à esquerda de quem segura (+X no Blender, -X
# na Unity). No chão a Unity apoia pela caixa da malha, como os outros.
VARA = 2.0                    # o comprimento, do fim do cabo à ponta
PEGADA_DA_VARA = 0.365        # a pegada (o pé do molinete), a contar do fim do cabo
# a ponta da vara (de onde a linha sai), no espaço do item (eixos do Blender); na Unity, (0, 0, VARA - PEGADA_DA_VARA)
PONTA_DA_VARA = (0.0, -(VARA - PEGADA_DA_VARA), 0.0)
# a vara é feita em pé (o eixo em +Z, o fim do cabo em z = 0, o molinete e os passadores do lado -X) e deitada no fim
# por esta matriz: o z da vara vai para -Y, o x para +Z (o molinete, no -X da vara, fica embaixo) e o y para -X
_VARA_DEITADA = Matrix(((0, -1, 0, 0), (0, 0, -1, 0), (1, 0, 0, 0), (0, 0, 0, 1))) @ Matrix.Translation((0, 0, -PEGADA_DA_VARA))
# o perfil da vara em pé (o raio, a altura e a cor do trecho até o próximo ponto), do fim do cabo à ponta
_PERFIL_DA_VARA = [
    (0.0, 0.0, "preto"), (0.0115, 0.0, "preto"), (0.0145, 0.007, "preto"), (0.0145, 0.026, "preto"),   # a tampa
    (0.0138, 0.026, "filtro"), (0.0143, 0.16, "filtro"),                                              # o cabo de trás
    (0.013, 0.298, "aco"), (0.0132, 0.298, "aco"), (0.0132, 0.314, "aco"),                             # a rosca de baixo
    (0.0112, 0.318, "grafite"),                                                                       # o porta-molinete
    (0.0112, 0.412, "aco"), (0.0128, 0.416, "aco"), (0.0128, 0.432, "aco"),                            # a rosca de cima
    (0.0124, 0.432, "filtro"),                                                                        # o cabo da frente
    (0.0106, 0.528, "aco"), (0.0088, 0.528, "aco"), (0.0088, 0.536, "aco"),                            # o anel do fim
    (0.0068, 0.536, "grafite"), (0.0067, 0.555, "vermelho_escuro"), (0.0067, 0.575, "grafite"),        # o gomo de baixo
    (0.0054, 0.99, "vermelho_escuro"), (0.0062, 0.99, "vermelho_escuro"), (0.0062, 1.004, "grafite"),  # a emenda
    (0.0062, 1.056, "vermelho_escuro"), (0.0062, 1.07, "grafite"), (0.0048, 1.07, "grafite"),          # o gomo de cima
    (0.0016, VARA - 0.015, "aco"), (0.0026, VARA - 0.015, "aco"), (0.0026, VARA, "aco"), (0.0, VARA, "aco"),  # a ponteira
]
# os passadores: onde (a contar do fim do cabo), o raio do anel e o quanto o meio do anel fica embaixo do eixo
PASSADORES = [(0.80, 0.0125, 0.030), (1.12, 0.0092, 0.023), (1.40, 0.0072, 0.018), (1.63, 0.0058, 0.014),
              (1.83, 0.0047, 0.011)]


def _raio_da_vara(z):
    """O raio da vara (o blank) em z, dos gomos do _PERFIL_DA_VARA."""
    if z < 0.99:
        return 0.0068 + (0.0054 - 0.0068) * (z - 0.536) / (0.99 - 0.536)
    if z < 1.07:
        return 0.0062
    return 0.0048 + (0.0016 - 0.0048) * (z - 1.07) / (VARA - 0.015 - 1.07)


def _passador(z, r, h):
    """Um passador embaixo da vara em pé: o anel de aço com o miolo escuro, as duas pernas em V até a vara e as
    amarrações vermelhas dos pés."""
    rb = _raio_da_vara(z)
    p = [(_com_matriz, Matrix.Translation((-h, 0, 0)), _anel("aco", r, 0.0016, 0.0024, z, 10)),
         (_com_matriz, Matrix.Translation((-h, 0, 0)), _anel("grafite", r - 0.0015, 0.0012, 0.0026, z, 10))]
    topo = -h + r
    for s in (-1, 1):
        p.append(R("aco", (topo, 0, z), (-rb + 0.0004, 0, z + s * (0.006 + h * 0.4)), 0.0024, 0.0014))
        p.append(Z("vermelho_escuro", rb + 0.0007, 0.008, (0, 0, z + s * (0.008 + h * 0.4)), 8))
    return p


def _molinete():
    """O molinete pendurado embaixo do porta-molinete (o lado -X da vara em pé), o carretel virado para a ponta (+Z): o
    pé, a haste, a caixa das engrenagens, o rotor com os braços, o carretel com a linha, o freio, a alça de arame e a
    manivela do lado -Y (a esquerda de quem segura)."""
    x0, zp = -0.077, PEGADA_DA_VARA
    p = [
        C("grafite", (0.004, 0.011, 0.052), (-0.0132, 0, zp), chanfro=0.001),
        R("grafite", (-0.014, 0, zp), (x0 + 0.02, 0, zp + 0.008), 0.011, 0.008),
        Z("grafite", 0.024, 0.032, (x0, 0, zp + 0.004), 14, "Y"),
        Z("aco", 0.016, 0.004, (x0, -0.017, zp + 0.004), 12, "Y"),
        Z("aco", 0.012, 0.004, (x0, 0.017, zp + 0.004), 12, "Y"),
        Z("preto", 0.0235, 0.028, (x0, 0, zp + 0.034), 14),
        Z("aco", 0.0215, 0.006, (x0, 0, zp + 0.051), 14),
        Z("amarelo", 0.0195, 0.018, (x0, 0, zp + 0.063), 14),
        Z("aco", 0.0215, 0.004, (x0, 0, zp + 0.074), 14),
        Z("vermelho", 0.009, 0.012, (x0, 0, zp + 0.082), 12),
    ]
    for s in (-1, 1):
        p.append(C("preto", (0.006, 0.005, 0.03), (x0, s * 0.0245, zp + 0.05)))
    # a alça: o arco de arame na frente do carretel, de um braço ao outro, passando por baixo (longe da vara)
    n, ra, za = 8, 0.026, zp + 0.064
    pts = [(x0 - ra * math.sin(math.pi * k / n), ra * math.cos(math.pi * k / n)) for k in range(n + 1)]
    for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
        p.append(R("prata", (xa, ya, za), (xb, yb, za), 0.0022, 0.0022, lado=(0, 0, 1)))
    p.append(Z("aco", 0.0032, 0.007, (x0, 0.026, za), 8, "X"))
    # a manivela: o eixo, o braço para longe da vara e o punho
    p += [
        Z("aco", 0.003, 0.014, (x0, -0.024, zp + 0.004), 8, "Y"),
        R("prata", (x0, -0.031, zp + 0.004), (x0 - 0.042, -0.031, zp - 0.006), 0.007, 0.004, lado=(0, 0, 1)),
        Z("preto", 0.0065, 0.02, (x0 - 0.042, -0.042, zp - 0.006), 10, "Y"),
    ]
    return p


def _vara():
    p = [(_torno, _PERFIL_DA_VARA, 10)]
    for z, r, h in PASSADORES:
        p += _passador(z, r, h)
    # a ponteira: o anelzinho na ponta
    p.append((_com_matriz, Matrix.Translation((-0.0042, 0, 0)), _anel("aco", 0.0026, 0.0012, 0.004, VARA - 0.008, 8)))
    p += _molinete()
    return [(_com_matriz, _VARA_DEITADA, p)]


# Os peixes (06/11/2026, a pedido: a pesca): o lambari (o pequeno), a tilápia (o médio), o tucunaré (o grande) e o dourado
# (o raro), deitados de lado como no chão (o flanco para cima), a cabeça para +X e as costas para +Y; a origem embaixo, no
# meio. Cada um é um corpo de anéis elípticos (as costas, o flanco e a barriga de cores diferentes, as faixas e a guelra
# pintadas nos anéis), as nadadeiras em placas finas (a cauda em garfo ou redonda) e, por cima do flanco (dos dois
# lados), o olho, a boca, as manchas e as linhas.

def _perfil_do_peixe(u, pico, ponta, fim, expoente=0.8):
    """A altura (ou a grossura) do corpo em u (0 no focinho, 1 no pedúnculo), de 0 a 1: sobe rápido da ponta até o pico
    e desce devagar até o fim."""
    if u <= pico:
        return ponta + (1 - ponta) * math.sin(u / pico * math.pi / 2) ** expoente
    return fim + (1 - fim) * math.cos((u - pico) / (1 - pico) * math.pi / 2) ** 1.2


class _Peixe:
    """A forma de um peixe (o dicionário da espécie): onde fica cada u do corpo, a altura, a grossura e o flanco."""

    def __init__(self, e):
        self.e = e
        self.xs = e["comp"] / 2                    # o focinho
        self.lb = e["comp"] * (1 - e["cauda"])      # o corpo, do focinho ao pedúnculo (o resto é a cauda)
        self.xp = self.xs - self.lb                # o pedúnculo
        self.zc = e["grossura"] / 2                # o meio da grossura (o flanco de baixo no chão)

    def x(self, u):
        return self.xs - u * self.lb

    def u_de(self, x):
        return min(1.0, max(0.0, (self.xs - x) / self.lb))

    def hd(self, u):
        """A meia altura."""
        return self.e["altura"] / 2 * _perfil_do_peixe(u, self.e["pico"], self.e.get("ponta", 0.16), self.e["pedunculo"],
                                                       self.e.get("cabeca", 0.8))

    def ht(self, u):
        """A meia grossura."""
        return self.e["grossura"] / 2 * _perfil_do_peixe(u, min(0.6, self.e["pico"] * 1.1), 0.34, 0.3, 0.6)

    def cy(self, u):
        """O meio da altura (as costas mais arqueadas que a barriga)."""
        return self.e["altura"] * self.e.get("arco", 0.03) * math.sin(math.pi * u)

    def costas(self, u):
        return self.cy(u) + self.hd(u)

    def barriga(self, u):
        return self.cy(u) - self.hd(u)

    def ponto(self, u, f, folga=0.0004):
        """O ponto no flanco de cima em u, na altura f (de -1, a barriga, a 1, as costas), um pouco para fora."""
        return (self.x(u), self.cy(u) + f * self.hd(u), self.zc + self.ht(u) * math.sqrt(max(0.0, 1 - f * f)) + folga)

    def espelho(self, q):
        """O mesmo ponto no flanco de baixo."""
        return (q[0], q[1], 2 * self.zc - q[2])


def _corpo_do_peixe(bm, uv, p):
    """O corpo: os anéis elípticos do focinho ao pedúnculo, fechados na ponta do focinho e no pedúnculo."""
    e = p.e
    n = 16
    us = [0.012, 0.03, 0.06, 0.1, 0.15, 0.21, 0.28, 0.36, 0.44, 0.52, 0.6, 0.68, 0.76, 0.84, 0.92, 1.0]
    for u0, u1, _ in e.get("faixas", []) + ([e["guelra"]] if "guelra" in e else []):
        us += [u0, u1]
    us = sorted(set(round(u, 4) for u in us))
    angs = [2 * math.pi * k / n for k in range(n)]
    aneis = [[bm.verts.new((p.x(u), p.cy(u) + p.hd(u) * math.cos(a), p.zc + p.ht(u) * math.sin(a))) for a in angs]
             for u in us]
    ponta = bm.verts.new((p.xs + 0.004 * e["comp"], p.cy(0.0), p.zc))
    fim = bm.verts.new((p.x(1.0) - 0.001, p.cy(1.0), p.zc))

    def cor(um, a):
        c = math.cos(a)
        regiao = e["costas"] if c > 0.62 else e["barriga"] if c < -0.55 else e["flanco"]
        for u0, u1, cf in e.get("faixas", []):
            if u0 <= um <= u1 and c > -0.45:
                regiao = cf
        if "guelra" in e:
            u0, u1, cg = e["guelra"]
            if u0 <= um <= u1 and -0.6 < c < 0.75:
                regiao = cg
        return regiao

    for k in range(n):
        j = (k + 1) % n
        am = math.pi * (2 * k + 1) / n
        eq._pintar(bm, uv, [bm.faces.new((ponta, aneis[0][j], aneis[0][k]))], cor(0.0, am))
        for i in range(len(us) - 1):
            f = bm.faces.new((aneis[i][k], aneis[i][j], aneis[i + 1][j], aneis[i + 1][k]))
            eq._pintar(bm, uv, [f], cor((us[i] + us[i + 1]) / 2, am))
        eq._pintar(bm, uv, [bm.faces.new((aneis[-1][k], aneis[-1][j], fim))], e["flanco"])


def _peixe(e):
    p = _Peixe(e)
    L, D = e["comp"], e["altura"]
    tf = max(0.0012, e["grossura"] * 0.05)       # a grossura das nadadeiras
    z0, z1 = p.zc - tf / 2, p.zc + tf / 2
    emb = 0.05 * D                               # o quanto a base das nadadeiras entra no corpo
    cn, ca = e["nadadeira"], e.get("cor_anal", e["nadadeira"])
    pecas = [(_corpo_do_peixe, p)]
    # a cauda: em garfo (dois lobos) ou redonda
    hp, cyp, xt = p.hd(1.0), p.cy(1.0), -L / 2
    xb = p.xp + 0.012 * L
    ht = e["cauda_alt"] * D
    cc = e.get("cor_cauda", cn)
    if e.get("forma_cauda", "garfo") == "garfo":
        g = e.get("garfo", 0.5) * (xb - xt)
        for s in (1, -1):
            pecas.append(F(cc, [(xb, cyp), (xb, cyp + s * hp * 0.9), (xt, cyp + s * ht), (xt + g, cyp)], z0, z1))
        if "listra_cauda" in e:
            xf = xt + g + 0.01 * L
            pecas.append(F(e["listra_cauda"], [(xb, cyp + 0.3 * hp), (xb, cyp - 0.3 * hp), (xf, cyp - 0.025 * D),
                                               (xf, cyp + 0.025 * D)], z0 - 0.0005, z1 + 0.0005))
    else:
        cauda = [(xb, cyp - hp * 0.9), (xb, cyp + hp * 0.9)]
        for k in range(7):
            a = math.radians(70 - 140 * k / 6)
            cauda.append((xb - (xb - xt) * (0.55 + 0.45 * math.cos(a)), cyp + ht * math.sin(a)))
        pecas.append(F(cc, cauda, z0, z1))
    # as dorsais (a de espinhos e a mole), a adiposa, a anal e as pélvicas
    for u0, u1, h0, h1, inclina in e.get("dorsais", []):
        pecas.append(F(cn, [(p.x(u0), p.costas(u0) - emb), (p.x(u1), p.costas(u1) - emb),
                            (p.x(u1) - inclina * L, p.costas(u1) + h1 * D),
                            (p.x(u0) - inclina * L * 0.4, p.costas(u0) + h0 * D)], z0, z1))
    if "adiposa" in e:
        u0, u1, h = e["adiposa"]
        pecas.append(F(cn, [(p.x(u0), p.costas(u0) - emb * 0.5), (p.x(u1), p.costas(u1) - emb * 0.5),
                            (p.x(u1) - 0.01 * L, p.costas(u1) + h * D)], z0, z1))
    if "anal" in e:
        u0, u1, h0, h1, inclina = e["anal"]
        pecas.append(F(ca, [(p.x(u0), p.barriga(u0) + emb), (p.x(u1), p.barriga(u1) + emb),
                            (p.x(u1) - inclina * L, p.barriga(u1) - h1 * D),
                            (p.x(u0) - inclina * L * 0.4, p.barriga(u0) - h0 * D)], z0, z1))
    if "pelvica" in e:
        u0, u1, h = e["pelvica"]
        pecas.append(F(ca, [(p.x(u0), p.barriga(u0) + emb), (p.x(u1), p.barriga(u1) + emb),
                            (p.x(u1) - 0.03 * L, p.barriga(u1) - h * D)], z0, z1))
    # as peitorais, deitadas nos flancos, para trás
    if "peitoral" in e:
        u, comp, larg = e["peitoral"]
        x0, y0 = p.x(u), p.cy(u) - 0.3 * p.hd(u)
        c, w = comp * L, larg * D
        pts = [(x0, y0 + w / 2), (x0, y0 - w / 2), (x0 - c, y0 - w * 0.9), (x0 - c * 0.92, y0 - w * 0.2)]
        topo = max(p.ponto(p.u_de(x), (y - p.cy(p.u_de(x))) / p.hd(p.u_de(x)), 0.0)[2] for x, y in pts)
        pecas += [F(ca, pts, topo - 0.0006, topo + tf * 0.8), F(ca, pts, 2 * p.zc - topo - tf * 0.8, 2 * p.zc - topo + 0.0006)]
    # o olho (a íris e a pupila), a boca, as manchas e as linhas, dos dois lados
    u, f, r, iris = e["olho"]
    a = p.ponto(u, f, 0.0003)
    for q in (a, p.espelho(a)):
        s = 1 if q[2] > p.zc else -1
        pecas += [Z(iris, r * D, 0.0016, q, 12), Z("preto", r * D * 0.55, 0.0016, (q[0], q[1], q[2] + s * 0.0008), 10)]
    if "boca" in e:
        u1, f = e["boca"]
        a, b = p.ponto(0.012, f, 0.0005), p.ponto(u1, f - 0.08, 0.0005)
        pecas += [R("preto", a, b, 0.006 * L, 0.0012), R("preto", p.espelho(a), p.espelho(b), 0.006 * L, 0.0012)]
    for u, f, r, fora, dentro in e.get("manchas", []):
        a = p.ponto(u, f, 0.0003)
        for q in (a, p.espelho(a)):
            s = 1 if q[2] > p.zc else -1
            pecas.append(Z(fora, r * D, 0.0014, q, 12))
            if dentro:
                pecas.append(Z(dentro, r * D * 0.55, 0.0014, (q[0], q[1], q[2] + s * 0.0007), 10))
    for u0, u1, f, cor, larg in e.get("linhas", []):
        pts = [p.ponto(u0 + (u1 - u0) * k / 6, f, 0.0004) for k in range(7)]
        for lado in (pts, [p.espelho(q) for q in pts]):
            for q0, q1 in zip(lado, lado[1:]):
                pecas.append(R(cor, q0, q1, larg * D, 0.001))
    return pecas


# as espécies: o tamanho (o comprimento com a cauda, a altura e a grossura do corpo, em m), a forma (a cauda, o pico da
# altura, o pedúnculo, o arco das costas, a altura do focinho e o expoente da cabeça, mais cheia quanto menor, em
# frações), as cores e as nadadeiras (em u, do focinho ao pedúnculo, e em frações da altura)
PEIXES = {
    # o lambari-do-rabo-amarelo (uns 15 cm): prateado, as costas azuladas, a mancha preta atrás da guelra, a listra preta
    # do pedúnculo até o meio da cauda, a cauda em garfo e a dorsal amarelas, o olho grande
    "PeixePequeno": dict(
        comp=0.15, altura=0.042, grossura=0.016, cauda=0.24, pico=0.36, pedunculo=0.24, arco=0.02, ponta=0.2,
        costas="azul_aco", flanco="prata", barriga="branco", nadadeira="amarelo", cor_anal="prata",
        guelra=(0.25, 0.275, "azul_aco"), dorsais=[(0.47, 0.57, 0.3, 0.32, 0.02)], adiposa=(0.8, 0.86, 0.1),
        anal=(0.62, 0.92, 0.14, 0.1, 0.01), pelvica=(0.44, 0.5, 0.14), peitoral=(0.27, 0.12, 0.12),
        cauda_alt=0.44, garfo=0.55, listra_cauda="preto", olho=(0.09, 0.2, 0.1, "branco"), boca=(0.04, -0.1),
        manchas=[(0.31, 0.3, 0.09, "preto", None)], linhas=[(0.72, 1.0, 0.0, "preto", 0.06)]),
    # a tilápia (uns 30 cm): alta e achatada, cinza-esverdeada com as barras verticais escuras, a dorsal comprida (a de
    # espinhos e a mole, alta atrás) e a cauda reta
    "PeixeMedio": dict(
        comp=0.30, altura=0.115, grossura=0.036, cauda=0.17, pico=0.42, pedunculo=0.33, arco=0.05, ponta=0.32,
        cabeca=0.6,
        costas="verde_cinza_escuro", flanco="verde_cinza", barriga="perola", nadadeira="musgo_escuro",
        guelra=(0.24, 0.265, "musgo_escuro"),
        faixas=[(u, u + 0.035, "verde_cinza_escuro") for u in (0.33, 0.43, 0.53, 0.63, 0.73, 0.83)],
        dorsais=[(0.24, 0.66, 0.14, 0.2, 0.0), (0.64, 0.97, 0.22, 0.34, 0.07)], anal=(0.7, 0.96, 0.18, 0.3, 0.06),
        pelvica=(0.3, 0.37, 0.24), peitoral=(0.27, 0.14, 0.14), cauda_alt=0.36, forma_cauda="redonda",
        olho=(0.11, 0.3, 0.075, "dourado"), boca=(0.05, -0.02)),
    # o tucunaré (uns 50 cm): verde, a barriga amarela, as 3 faixas pretas, o olho vermelho, as nadadeiras de baixo
    # laranja, a dorsal dupla, a cauda redonda e o ocelo (a mancha preta com o anel amarelo) na base da cauda
    "PeixeGrande": dict(
        comp=0.50, altura=0.13, grossura=0.06, cauda=0.17, pico=0.4, pedunculo=0.34, arco=0.03, ponta=0.3,
        cabeca=0.65,
        costas="verde_escuro", flanco="verde", barriga="amarelo", nadadeira="verde_escuro", cor_anal="laranja_vivo",
        guelra=(0.25, 0.27, "verde_escuro"),
        faixas=[(0.33, 0.39, "verde_quase_preto"), (0.52, 0.58, "verde_quase_preto"), (0.7, 0.76, "verde_quase_preto")],
        dorsais=[(0.27, 0.56, 0.12, 0.17, 0.0), (0.56, 0.93, 0.18, 0.28, 0.05)], anal=(0.72, 0.94, 0.15, 0.24, 0.04),
        pelvica=(0.31, 0.37, 0.2), peitoral=(0.28, 0.12, 0.13), cauda_alt=0.36, forma_cauda="redonda",
        olho=(0.085, 0.3, 0.065, "vermelho"), boca=(0.09, -0.15), manchas=[(0.96, 0.0, 0.12, "amarelo", "preto")]),
    # o dourado (o raro, uns 45 cm): dourado, as costas alaranjadas, as linhas escuras ao longo do flanco, as nadadeiras
    # laranja, a adiposa e a cauda em garfo laranja com a listra preta no meio
    "PeixeDourado": dict(
        comp=0.45, altura=0.115, grossura=0.05, cauda=0.2, pico=0.36, pedunculo=0.26, arco=0.04, ponta=0.24,
        cabeca=0.7,
        costas="laranja_escuro", flanco="dourado", barriga="filtro", nadadeira="laranja_vivo",
        guelra=(0.24, 0.265, "laranja_escuro"), dorsais=[(0.42, 0.55, 0.22, 0.24, 0.03)], adiposa=(0.82, 0.88, 0.07),
        anal=(0.72, 0.9, 0.12, 0.16, 0.02), pelvica=(0.43, 0.49, 0.16), peitoral=(0.27, 0.12, 0.12),
        cauda_alt=0.42, garfo=0.5, listra_cauda="preto", olho=(0.09, 0.25, 0.065, "amarelo"), boca=(0.11, -0.05),
        linhas=[(0.27, 0.95, f, "chocolate", 0.018) for f in (0.55, 0.3, 0.05, -0.2)]),
}


def _peixe_pequeno():
    return _peixe(PEIXES["PeixePequeno"])


def _peixe_medio():
    return _peixe(PEIXES["PeixeMedio"])


def _peixe_grande():
    return _peixe(PEIXES["PeixeGrande"])


def _peixe_dourado():
    return _peixe(PEIXES["PeixeDourado"])


def _polvora():
    # A pólvora (06/11/2026, a pedido: a matéria-prima da munição): a lata redonda de pólvora, em pé: o corpo de lata com
    # as bordas, o rótulo vermelho-escuro com a faixa preta em cima, o losango amarelo de perigo com a chama preta, duas
    # linhas brancas de letras embaixo e a tampa de rosca preta em cima. Uns 8,6 x 12 cm, a origem embaixo, no meio.
    r, h = 0.043, 0.105
    return [
        Z("lata", r * 0.97, h, (0, 0, h / 2), 16),
        Z("lata", r, 0.006, (0, 0, 0.003), 16),
        Z("lata", r, 0.006, (0, 0, h - 0.003), 16),
        Z("vermelho_escuro", r * 0.995, h * 0.72, (0, 0, h * 0.47), 16),
        Z("preto", r, 0.014, (0, 0, h * 0.76), 16),
        C("amarelo", (0.018, 0.005, 0.018), (0, -r + 0.0012, h * 0.47), giro=(0, 45, 0)),
        C("preto", (0.007, 0.004, 0.009), (0, -r - 0.0016, h * 0.47)),
        C("branco", (0.022, 0.004, 0.0035), (0, -r + 0.0012, h * 0.25)),
        C("branco", (0.016, 0.004, 0.003), (0, -r + 0.0012, h * 0.19)),
        Z("lata_clara", r * 0.8, 0.004, (0, 0, h + 0.001), 16),
        Z("preto", 0.019, 0.004, (0, 0, h + 0.004), 14),
        Z("preto", 0.017, 0.012, (0, 0, h + 0.008), 14),
    ]


# as peças do prato (sem ícone: as animações e o jogo)
PECAS_DO_PRATO = {
    "PratoFundo": _prato_fundo, "PratoComida": _comida, "PratoComidaEstragada": _comida_estragada,
    "PratoRestos": _restos, "Colher": _colher, "ColherBocado": _bocado,
}


# as peças do fumo (sem ícone: as animações e o jogo)
PECAS_DO_FUMO = {
    "MacoDeCigarrosCaixa": _caixa_do_maco, "MacoDeCigarrosTampa": _tampa_do_maco,
    "CigarroFiltro": _cigarro_filtro, "CigarroPapel": _cigarro_papel, "CigarroPonta": _cigarro_ponta,
    "CigarroBrasa": _cigarro_brasa, "CigarroCinza": _cigarro_cinza, "Bituca": _bituca,
    "Isqueiro": _isqueiro, "IsqueiroChama": _isqueiro_chama,
}


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
    "Galao": _galao,
    "Gazua": _gazua, "PeDeCabra": _pe_de_cabra,
    "MacoDeCigarros": _maco,
    "PratoDeComida": _prato_de_comida, "PratoVazio": _prato_vazio, "PratoEstragado": _prato_estragado,
    "Tala": _tala, "BandagemAntisseptica": _bandagem_antisseptica, "Calmante": _calmante,
    "PeixePequeno": _peixe_pequeno, "PeixeMedio": _peixe_medio, "PeixeGrande": _peixe_grande,
    "PeixeDourado": _peixe_dourado, "Polvora": _polvora,
}
# as construções montadas no mundo e a bancada (sem ícone)
MONTADAS = {
    "FogueiraMontada": _fogueira_montada, "CamaMontada": _cama_montada, "BarricadaMontada": _barricada_montada,
    "Bancada": _bancada,
}
# os deitados (vistos mais de cima no ícone)
DEITADOS = {"Madeira", "Toras", "Pano", "Corda", "FitaAdesiva", "Ferramenta", "PecasDeMetal", "Fios", "Pregos", "VaraDePesca",
            "Roupa", "Fogueira", "Cama", "Barricada", "Atadura", "Bandagem", "Lanterna", "Pilhas", "Gazua", "PeDeCabra",
            "PratoDeComida", "PratoVazio", "PratoEstragado", "Tala", "BandagemAntisseptica",
            "PeixePequeno", "PeixeMedio", "PeixeGrande", "PeixeDourado"}
# os que ocupam mais de uma célula na mochila (o ícone na proporção delas, 128 px por célula; o Itens.Tamanho da Unity)
CELULAS = {"PeDeCabra": (2, 1), "PeixeGrande": (2, 1), "PeixeDourado": (2, 1)}
# as vistas próprias (a direção da câmera do ícone e a da foto, o giro da câmera do ícone em graus, a escala do ícone e
# a distância da foto, vezes o tamanho): a vara comprida, vista de lado (o molinete e a manivela para a câmera), na
# diagonal no ícone e com a foto de trás do cabo; os peixes, deitados de lado, quase de cima (o perfil deles)
VISTAS = {"VaraDePesca": ((1.0, 0.0, 0.3), (0.9, 1.0, 0.6), 45.0, 0.75, 1.45)}
VISTAS.update({nome: ((0.22, -0.5, 1.0), (0.3, -0.5, 1.0), 0.0, 1.2, 2.2)
               for nome in ("PeixePequeno", "PeixeMedio", "PeixeGrande", "PeixeDourado")})

SAIDA = os.path.join(eq.UNITY, "Loot.fbx")


def _cena():
    return bpy.context.scene


def construir():
    cena = _cena()
    for o in list(cena.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    todos = list(MODELOS.items()) + list(MONTADAS.items()) + list(PECAS_DO_FUMO.items()) + list(PECAS_DO_PRATO.items())
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


def _estilo(cena):
    """O estilo dos ícones: o Workbench com a textura, a cavidade, o contorno e o fundo transparente."""
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
    return r


def imagens(so=None):
    """Os ícones (128 x 128) e as fotos (600 x 500, de 3/4), no estilo dos outros: o Workbench com a textura, a
    cavidade, o contorno e o fundo transparente."""
    cena = _cena()
    bpy.context.view_layer.update()   # as posições dos objetos (sem isto, a câmera mirava a origem e saía vazio)
    r = _estilo(cena)
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
        vista = VISTAS.get(nome)
        if vista:
            icone, foto = vista[0], vista[1]
        cx, cy = CELULAS.get(nome, (1, 1))
        r.resolution_x, r.resolution_y = 128 * cx, 128 * cy
        cam = eq._camera(cena, True, centro, icone if vista else (0.0, -0.25, 1.0) if cx > cy else icone, 3.0,
                         tam * (vista[3] if vista else 1.45 if nome in ("Atadura", "Bandagem", "BandagemAntisseptica", "FitaAdesiva")
                                else 1.15 if cx > cy else 1.3))
        if vista:
            # o giro em volta do eixo da câmera (a vara na diagonal)
            cam.rotation_euler = (cam.rotation_euler.to_quaternion() @ Quaternion((0, 0, 1), math.radians(vista[2]))).to_euler()
        r.filepath = os.path.join(eq.ICONES, f"Icone_{nome}.png")
        bpy.ops.render.render(write_still=True)
        r.resolution_x, r.resolution_y = 600, 500
        eq._camera(cena, False, centro, foto, tam * (vista[4] if vista else 2.4), tam)
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


def _mao_do_principal(caminho):
    """A mão esquerda do personagem principal (a Maos do PrincipalBloco.blend da pasta de arte, na pose T, sem o
    esqueleto): a palma para baixo, os dedos para +X e o meio dela na origem."""
    with bpy.data.libraries.load(caminho, link=False) as (_de, para):
        para.objects = ["Maos", "PrincipalBloco_Rig"]
    cena = _cena()
    maos, rig = para.objects
    for o in (maos, rig):
        cena.collection.objects.link(o)
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(maos.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    me.transform(maos.matrix_world)
    for o in (maos, rig):
        bpy.data.objects.remove(o, do_unlink=True)
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.x < 0.3], context="VERTS")
    meio = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    bmesh.ops.translate(bm, vec=-meio, verts=bm.verts)
    bm.to_mesh(me)
    bm.free()
    for im in bpy.data.images:
        if im.filepath.startswith("//"):
            im.filepath = os.path.normpath(os.path.join(os.path.dirname(caminho), im.filepath[2:]))
    o = bpy.data.objects.new("MaoDoPrincipal", me)
    cena.collection.objects.link(o)
    return o


def folha_do_fumo(caminho, principal):
    """A folha da parte 2 do Plano_Cigarro.md (3 x 2 fotos): o maço de frente e de costas; o maço aberto com um cigarro
    subindo; os cigarros em pé (o novo, o aceso, o queimado pela metade e no fim) e a bituca; o isqueiro aceso; e tudo
    deitado ao lado da mão do personagem principal, de cima e de 3/4 (o tamanho)."""
    import numpy as np
    cena = _cena()
    bpy.context.view_layer.update()
    r = _estilo(cena)
    base = {o.name[:-6]: o for o in cena.objects if o.type == "MESH" and o.name.endswith("_corpo")}
    mao = _mao_do_principal(principal)
    atual, paineis = [], []

    def M(x, y, z):
        return Matrix.Translation((x, y, z))

    def R(graus, eixo):
        return Matrix.Rotation(math.radians(graus), 4, eixo)

    def por(nome, m):
        c = base[nome].copy()   # a mesma malha
        cena.collection.objects.link(c)
        c.matrix_world = m @ Matrix.Rotation(math.radians(90), 4, "X")
        atual.append(c)

    def cigarro(m, sobra=None):
        """Um cigarro inteiro na emenda m: apagado (sobra None) ou aceso, com o papel encolhido até a sobra."""
        s = 1.0 if sobra is None else sobra
        por("CigarroFiltro", m)
        por("CigarroPapel", m @ Matrix.Diagonal((1.0, 1.0, s, 1.0)))
        ponta = m @ M(0, 0, PAPEL * s)
        if sobra is None:
            por("CigarroPonta", ponta)
        else:
            por("CigarroBrasa", ponta)
            por("CigarroCinza", ponta)

    def painel(orto, direcao, folga, com_mao=False):
        paineis.append((list(atual) + ([mao] if com_mao else []), orto, direcao, folga))
        atual.clear()

    # 1. o maço fechado, de frente e de costas
    por("MacoDeCigarros", M(10.0, 0, MACO_A / 2) @ R(-15, "Z"))
    por("MacoDeCigarros", M(10.085, 0.02, MACO_A / 2) @ R(165, "Z"))
    painel(False, (0.35, -1.0, 0.45), 1.0)
    # 2. o maço aberto (a tampa a -105°) com um cigarro subindo da fileira da frente
    m = M(20.0, 0, MACO_A / 2)
    por("MacoDeCigarrosCaixa", m)
    por("MacoDeCigarrosTampa", m @ M(*DOBRADICA) @ R(-105, "X"))
    topo = MACO_A / 2 - 0.0025
    cigarro(m @ M(SAIDA_DO_CIGARRO[0], SAIDA_DO_CIGARRO[1], topo + 0.035 - FILTRO) @ R(180, "Y"))
    painel(False, (0.45, -1.0, 0.95), 1.0)
    # 3. os cigarros em pé (a boca embaixo): o novo, o aceso, pela metade e no fim; e a bituca deitada
    for k, sobra in enumerate((None, 1.0, 0.5, 0.15)):
        cigarro(M(30.0 + k * 0.03, 0, FILTRO), sobra)
    por("Bituca", M(30.0 + 4 * 0.03 + 0.01, 0, RAIO_DO_CIGARRO + 0.001) @ R(-30, "Z"))
    painel(True, (0.0, -1.0, 0.3), 1.25)
    # 4. o isqueiro aceso
    m = M(40.0, 0, 0.0435)
    por("Isqueiro", m)
    por("IsqueiroChama", m @ M(*BOCA_DO_ISQUEIRO))
    painel(False, (0.55, -1.0, 0.35), 1.0)
    # 5 e 6. deitados ao lado da mão do principal (a palma para baixo, os dedos para +X)
    mao.matrix_world = M(50.0, 0.0, 0.035)
    por("MacoDeCigarros", M(49.97, -0.13, MACO_P / 2) @ R(-90, "X"))
    cigarro(M(50.03, -0.085, RAIO_DO_CIGARRO) @ R(90, "Y"))
    cigarro(M(50.03, -0.11, RAIO_DO_CIGARRO) @ R(90, "Y"), 1.0)
    por("Isqueiro", M(50.12, -0.12, 0.0091) @ R(-90, "X"))
    grupo = list(atual)
    painel(True, (0.0, -0.3, 1.0), 1.15, True)
    atual.extend(grupo)
    painel(False, (0.5, -1.0, 0.8), 0.85, True)

    W, H, COLUNAS = 640, 560, 3
    linhas = (len(paineis) + COLUNAS - 1) // COLUNAS
    folha = np.zeros((linhas * H, COLUNAS * W, 4), np.float32)
    folha[..., :3] = (0.20, 0.21, 0.23)
    folha[..., 3] = 1.0
    todos = [o for o in cena.objects if o.type == "MESH"]
    for k, (objs, orto, direcao, folga) in enumerate(paineis):
        for o in todos:
            o.hide_render = o not in objs
        bpy.context.view_layer.update()
        cantos = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
        lo = Vector((min(c.x for c in cantos), min(c.y for c in cantos), min(c.z for c in cantos)))
        hi = Vector((max(c.x for c in cantos), max(c.y for c in cantos), max(c.z for c in cantos)))
        tam = max(hi - lo)
        r.resolution_x, r.resolution_y = W, H
        if orto:
            eq._camera(cena, True, (lo + hi) / 2, direcao, 3.0, tam * folga)
        else:
            eq._camera(cena, False, (lo + hi) / 2, direcao, tam * 2.3 * folga, tam)
        arq = os.path.join(bpy.app.tempdir, f"folha_do_fumo_{k}.png")
        r.filepath = arq
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(arq)
        px = np.empty(W * H * 4, np.float32)
        im.pixels.foreach_get(px)
        px = px.reshape(H, W, 4)
        bpy.data.images.remove(im)
        y0, x0 = (linhas - 1 - k // COLUNAS) * H, (k % COLUNAS) * W   # as linhas da imagem contam de baixo
        a = px[..., 3:4]
        folha[y0:y0 + H, x0:x0 + W, :3] = folha[y0:y0 + H, x0:x0 + W, :3] * (1 - a) + px[..., :3] * a
    out = bpy.data.images.new("FolhaDoFumo", COLUNAS * W, linhas * H, alpha=False)
    out.pixels.foreach_set(folha.ravel())
    out.filepath_raw = caminho
    out.file_format = "PNG"
    out.save()
    print("loot: a folha do fumo em", caminho)


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
    if etapa == "folha":   # -- folha <saída.png> <PrincipalBloco.blend>
        folha_do_fumo(args[1], args[2])
