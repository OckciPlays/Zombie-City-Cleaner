# Os itens da sucata (09/11/2026, a etapa 1b das armas de sucata): a sucata (o material novo, essencial para fabricar as
# armas), as duas munições caseiras (a de prego, da pistola e da submetralhadora; a de parafuso, do rifle) e os painéis
# de metal do mundo, de onde se tira a sucata. Grava o Loot_Sucata.fbx, ao lado do Loot.fbx (sem mexer nele).
#
# O formato é o do Loot.fbx (o loot.py e o equipamentos.py): cada modelo é um objeto <Nome>_corpo girado 90° em X, com a
# malha nos eixos da Unity (o alto em +Y), feito aqui nos eixos do Blender (o alto em +Z, a frente em -Y); a origem
# embaixo, no meio (no chão, a Unity apoia pela caixa da malha); o material "Pistola", com a Paleta.png (o dos itens).
# - Item_Sucata: um montinho de sucata (~18 cm): duas chapas tortas, um pedaço de cano, duas porcas, um parafuso e uma mola.
# - Item_MunicaoPrego: uma lata de conserva com os pregos (as cabeças por cima), a tampa aberta para trás e a fita crepe
#   com um prego desenhado; no tamanho das caixas de munição do Municao.fbx.
# - Item_MunicaoParafuso: uma caixinha de tábua crua com os parafusos sextavados de latão, a tampa aberta, a fita prata e
#   um parafuso pintado na frente: outra forma (caixa e não lata) e outras cores, para não confundir com a de prego.
# - Painel_Chapa, Painel_Porta e Painel_Placa: os painéis de metal do mundo, deitados no chão: a telha ondulada amassada
#   (1,2 m), a porta de carro solta (1,05 m) e a placa de aviso com o poste quebrado (1,5 m), com ferrugem.
# Cores: as da Paleta.png (equipamentos.py: linha de cima para baixo, coluna), com as da sucata, nas células novas das
# linhas 3 e 4 (colunas 12 a 15), postas pelo Blender/Armas/sucata_exportar.py.
#
# Como rodar (um Blender em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/sucata_itens.py -- [construir|tudo]
#   (tudo: constrói e grava o Assets/_Game/Art/Characters/Jogador/Loot_Sucata.fbx)
# Na Unity, as peças de cada modelo se juntam pelo prefixo <Nome>_, como o JuntarLoot faz com o Loot.fbx.

import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import equipamentos as eq  # noqa: E402
import loot  # noqa: E402  (as peças C, Z, R e F e o _com_matriz)

C, Z, R, F = loot.C, loot.Z, loot.R, loot.F
SAIDA = os.path.join(eq.UNITY, "Loot_Sucata.fbx")

eq.CORES.update({
    # as da sucata (as células novas: Blender/Armas/sucata.py, a linha 12 e a 11 de baixo)
    "s_ferrugem": (3, 12), "s_ferrugem_escura": (3, 13), "s_fita_prata": (3, 14), "s_pintura_verde": (3, 15),
    "s_madeira_crua": (4, 12), "s_corda": (4, 13), "s_borracha": (4, 14), "s_cobre": (4, 15),
    # as das armas (a linha 3 de cima: o metal, o preto, o latão)
    "s_metal": (3, 0), "s_metal_medio": (3, 1), "s_preto": (3, 2), "s_metal_escuro": (3, 3), "s_metal_claro": (3, 4),
    "s_quase_preto": (3, 6), "s_madeira_escura": (3, 7), "s_latao": (3, 11),
    # as outras que já existiam
    "s_galvanizado": (8, 2), "s_galvanizado_escuro": (8, 1), "s_lata": (8, 8), "s_lata_clara": (4, 6),
    "s_fita_crepe": (3, 9), "s_tinta_carro": (4, 9), "s_tinta_carro_escura": (8, 6), "s_vidro": (11, 12),
    "s_amarelo": (11, 7),
})


# ------------------------------------------------------------------------------------------------ as peças novas
def _tubo(bm, uv, cor, a, b, r, lados=8, r2=None, fase=0.0):
    """Um cilindro (ou um cone, com r2) de a até b, de poucos lados; a fase gira os lados em volta do eixo (graus)."""
    a, b = Vector(a), Vector(b)
    d = b - a
    res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=lados, radius1=r,
                                radius2=r if r2 is None else r2, depth=d.length)
    m = Matrix.Translation((a + b) / 2) @ d.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4() @ \
        Matrix.Rotation(math.radians(fase), 4, "Z")
    bmesh.ops.transform(bm, matrix=m, verts=res["verts"])
    eq._pintar(bm, uv, list({f for v in res["verts"] for f in v.link_faces}), cor)


def T(cor, a, b, r, lados=8, r2=None, fase=0.0):
    return (_tubo, cor, a, b, r, lados, r2, fase)


def _lata(bm, uv, cor, raio, altura, z0, lados=10):
    """Um cilindro em pé com uma face virada para a frente (-Y), para os decalques ficarem colados nela."""
    angs = [math.radians(-90 + 180 / lados + 360 * k / lados) for k in range(lados)]
    baixo = [bm.verts.new((raio * math.cos(t), raio * math.sin(t), z0)) for t in angs]
    cima = [bm.verts.new((raio * math.cos(t), raio * math.sin(t), z0 + altura)) for t in angs]
    faces = [bm.faces.new(baixo[::-1]), bm.faces.new(cima)]
    faces += [bm.faces.new((baixo[k], baixo[(k + 1) % lados], cima[(k + 1) % lados], cima[k])) for k in range(lados)]
    eq._pintar(bm, uv, faces, cor)


def L(cor, raio, altura, z0, lados=10):
    return (_lata, cor, raio, altura, z0, lados)


def _mola(bm, uv, cor, a, b, raio, fio, voltas, passos=5):
    """Uma mola de a até b: o arame (um tubo de 3 lados) numa hélice."""
    a, b = Vector(a), Vector(b)
    eixo = b - a
    e = eixo.normalized()
    u = e.orthogonal().normalized()
    w = e.cross(u)
    n = int(voltas * passos)
    aneis = []
    for k in range(n + 1):
        ang = 2 * math.pi * k / passos
        radial = u * math.cos(ang) + w * math.sin(ang)
        p = a + eixo * (k / n) + radial * raio
        t = (u * -math.sin(ang) + w * math.cos(ang)) * (2 * math.pi * raio / passos) + eixo / n
        y = t.cross(radial).normalized()
        x = y.cross(t).normalized()
        aneis.append([bm.verts.new(p + (x * math.cos(2 * math.pi * j / 3) + y * math.sin(2 * math.pi * j / 3)) * fio)
                      for j in range(3)])
    faces = [bm.faces.new((aneis[k][j], aneis[k][(j + 1) % 3], aneis[k + 1][(j + 1) % 3], aneis[k + 1][j]))
             for k in range(n) for j in range(3)]
    faces += [bm.faces.new(aneis[0][::-1]), bm.faces.new(aneis[-1])]
    eq._pintar(bm, uv, faces, cor)


def _ruido(semente):
    """Um ruído suave e fixo (uma grade de valores ao acaso, interpolada), para as manchas de ferrugem."""
    rnd = random.Random(semente)
    grade = [[rnd.random() for _ in range(16)] for _ in range(16)]

    def r(x, y):
        i, j = int(math.floor(x)), int(math.floor(y))
        fx, fy = x - i, y - j
        fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
        g = lambda a, b: grade[a % 16][b % 16]  # noqa: E731
        return (g(i, j) * (1 - fx) + g(i + 1, j) * fx) * (1 - fy) + (g(i, j + 1) * (1 - fx) + g(i + 1, j + 1) * fx) * fy
    return r


def _chapa_ondulada(bm, uv, largura, comprimento, ondas, amp, esp, segs, deforma, cor_da_face):
    """A telha ondulada: o perfil em zigue-zague (4 pontos por onda) na largura (X), puxado no comprimento (Y) em segs
    pedaços e fechado (a grossura esp); deforma(x, y, z) amassa; cor_da_face(centro) pinta cada face."""
    n = ondas * 4
    perfil = [(-largura / 2 + largura * i / n, amp * (0, 1, 0, -1)[i % 4]) for i in range(n + 1)]
    anel = perfil + [(x, z - esp) for x, z in reversed(perfil)]   # em cima, na ida; embaixo, na volta
    m = len(anel)
    aneis = [[bm.verts.new(deforma(x, -comprimento / 2 + comprimento * s / segs, z)) for x, z in anel]
             for s in range(segs + 1)]
    faces = [bm.faces.new((aneis[s][i], aneis[s][(i + 1) % m], aneis[s + 1][(i + 1) % m], aneis[s + 1][i]))
             for s in range(segs) for i in range(m)]
    for s in (0, segs):   # as pontas: as tiras entre a face de cima e a de baixo
        r = aneis[s]
        faces += [bm.faces.new((r[i], r[i + 1], r[m - 2 - i], r[m - 1 - i])) for i in range(n)]
    for f in faces:
        eq._pintar(bm, uv, [f], cor_da_face(f.calc_center_median()))


# ------------------------------------------------------------------------------------------------ os itens de mão
def _sucata():
    """O montinho: a chapa dobrada no chão, a chapinha furada encostada nela, o cano por cima, as porcas, o parafuso e
    a mola, de ~18 x 14 cm."""
    dobra = Matrix.Translation((0.045, 0, 0.003)) @ Matrix.Rotation(math.radians(-32), 4, "Y")
    encosta = Matrix.Translation((-0.05, 0.035, 0.0)) @ Matrix.Rotation(math.radians(24), 4, "Z") @ \
        Matrix.Rotation(math.radians(26), 4, "X")
    return [
        # a chapa dobrada: metade enferrujada no chão, a aba galvanizada levantada
        (loot._com_matriz, Matrix.Rotation(math.radians(12), 4, "Z"), [
            C("s_ferrugem", (0.10, 0.075, 0.003), (-0.005, 0, 0.0015)),
            C("s_ferrugem_escura", (0.03, 0.02, 0.0008), (-0.025, -0.012, 0.0034), giro=(0, 0, 25)),
            (loot._com_matriz, dobra, [C("s_galvanizado", (0.05, 0.075, 0.003), (0.025, 0, 0.0)),
                                       C("s_ferrugem", (0.022, 0.03, 0.0008), (0.02, 0.012, 0.0019))]),
        ]),
        # a chapinha encostada, com um furo
        (loot._com_matriz, encosta, [
            C("s_ferrugem_escura", (0.06, 0.045, 0.003), (0, 0.0225, 0.0015)),
            Z("s_quase_preto", 0.007, 0.0032, (0.012, 0.028, 0.0015), 6),
        ]),
        # o pedaço de cano (a boca escura nas duas pontas)
        T("s_metal_medio", (-0.07, -0.035, 0.016), (0.045, 0.02, 0.026), 0.013, 8),
        T("s_quase_preto", (-0.0705, -0.0352, 0.016), (0.0455, 0.0202, 0.026), 0.0095, 8),
        # as porcas (o furo escuro passa de lado a lado) e o parafuso
        Z("s_metal_claro", 0.011, 0.008, (0.06, -0.045, 0.004), 6),
        Z("s_quase_preto", 0.0055, 0.0084, (0.06, -0.045, 0.004), 6),
        Z("s_ferrugem", 0.009, 0.007, (-0.02, -0.05, 0.0035), 6),
        Z("s_quase_preto", 0.0045, 0.0074, (-0.02, -0.05, 0.0035), 6),
        T("s_metal_claro", (0.02, 0.055, 0.005), (0.075, 0.035, 0.005), 0.0035, 4),
        T("s_metal_escuro", (0.075, 0.035, 0.0055), (0.082, 0.0325, 0.0055), 0.0075, 6),
        # a mola, deitada na frente
        (_mola, "s_metal_claro", (0.005, -0.07, 0.0095), (0.058, -0.076, 0.0095), 0.008, 0.0015, 4, 6),
    ]


def _municao_prego():
    """A lata de conserva aberta com os pregos de cabeça para cima, a tampa dobrada para trás e a fita crepe com o
    prego desenhado (na frente, -Y)."""
    r, h = 0.038, 0.085
    ap = r * math.cos(math.pi / 10)          # a distância da face da frente ao meio
    prego = Vector((0, -(ap + 0.0014), 0.041))                                  # o meio do desenho, na fita
    eixo = Vector((math.sin(math.radians(35)), 0, -math.cos(math.radians(35))))  # da cabeça para a ponta
    tampa = Matrix.Translation((0, r - 0.002, h + 0.0005)) @ Matrix.Rotation(math.radians(-112), 4, "X") @ \
        Matrix.Translation((0, -(r - 0.002), -(h + 0.0005)))
    p = [
        L("s_lata", r, h, 0.0),
        L("s_lata_clara", r + 0.0015, 0.005, h - 0.005),                # a borda enrolada
        L("s_quase_preto", r - 0.003, 0.0015, h - 0.001),               # o fundo escuro da boca
        L("s_fita_crepe", r + 0.0008, 0.034, 0.024),                    # a fita crepe
        # o prego desenhado na fita, inclinado (a cabeça, o corpo e a ponta em losango)
        C("s_preto", (0.011, 0.0012, 0.0025), tuple(prego - eixo * 0.0135), giro=(0, -35, 0)),
        C("s_preto", (0.0028, 0.0012, 0.023), tuple(prego - eixo * 0.0005), giro=(0, -35, 0)),
        C("s_preto", (0.002, 0.0012, 0.002), tuple(prego + eixo * 0.0118), giro=(0, 10, 0)),
        # a ferrugem na lata
        C("s_ferrugem", (0.0012, 0.012, 0.009), ((ap + 0.0006) * math.cos(math.radians(-18)),
                                                 (ap + 0.0006) * math.sin(math.radians(-18)), 0.013), giro=(0, 0, -18)),
        C("s_ferrugem_escura", (0.0012, 0.009, 0.007), ((ap + 0.0006) * math.cos(math.radians(162)),
                                                        (ap + 0.0006) * math.sin(math.radians(162)), 0.07), giro=(0, 0, 162)),
        # a tampa, presa atrás e dobrada para cima
        (loot._com_matriz, tampa, [L("s_lata_clara", r - 0.002, 0.0015, h)]),
    ]
    # os pregos: as cabeças por cima da boca, uns mais altos e tortos
    for x, y, alto, inc in ((0.0, 0.0, 0.021, 0), (0.017, 0.008, 0.012, 8), (-0.016, 0.01, 0.016, -6),
                            (0.008, -0.019, 0.010, 5), (-0.012, -0.015, 0.014, -9)):
        topo = Vector((x + math.sin(math.radians(inc)) * alto, y, h + alto))
        p.append(T("s_metal_claro", (x, y, h - 0.01), tuple(topo), 0.0013, 3))
        p.append(T("s_galvanizado", tuple(topo), tuple(topo + Vector((0, 0, 0.0016))), 0.0045, 6))
    return p


def _municao_parafuso():
    """A caixinha de tábua crua com os parafusos de latão em pé, a tampa aberta para trás, a fita prata na ponta e um
    parafuso pintado de preto na frente."""
    w, d, h = 0.12, 0.07, 0.055
    fy = -(d / 2 + 0.0006)                   # a frente (-Y), para os decalques
    p = [
        C("s_madeira_crua", (w, d, h), (0, 0, h / 2), chanfro=0.002),
        # as frestas das tábuas na frente e atrás
        C("s_madeira_escura", (w - 0.004, 0.0012, 0.0016), (0, fy, h * 0.5)),
        C("s_madeira_escura", (w - 0.004, 0.0012, 0.0016), (0, -fy, h * 0.5)),
        # a boca escura e a fita prata em volta da ponta
        C("s_quase_preto", (w - 0.012, d - 0.012, 0.0016), (0, 0, h + 0.0002)),
        C("s_fita_prata", (0.022, d + 0.003, h - 0.0012), (0.038, 0, (h - 0.0012) / 2)),
        # a tampa, aberta para trás (como as caixas do Municao.fbx)
        C("s_madeira_crua", (w, 0.005, d), (0, d / 2 + math.sin(math.radians(15)) * d / 2,
                                            h + math.cos(math.radians(15)) * d / 2), giro=(-15, 0, 0)),
        # o parafuso pintado: a cabeça, o corpo, a rosca e a ponta
        C("s_preto", (0.014, 0.0012, 0.0045), (-0.025, fy - 0.0005, 0.043)),
        C("s_preto", (0.0045, 0.0012, 0.024), (-0.025, fy - 0.0005, 0.029)),
        C("s_preto", (0.0018, 0.0012, 0.004), (-0.025, fy - 0.0005, 0.0155)),
    ]
    for z in (0.022, 0.029, 0.036):
        p.append(C("s_preto", (0.0095, 0.0012, 0.0014), (-0.025, fy - 0.0008, z), giro=(0, 28, 0)))
    # os parafusos sextavados de latão: as cabeças por cima da boca (dois mais altos, com a rosca à mostra)
    for i, (x, y) in enumerate(((-0.04, -0.015), (-0.012, -0.015), (0.012, -0.012), (-0.038, 0.014), (-0.01, 0.015),
                                (0.014, 0.014))):
        alto = 0.012 if i in (1, 5) else 0.004
        if alto > 0.006:
            p.append(T("s_metal_claro", (x, y, h - 0.01), (x, y, h + alto), 0.0028, 3))
        p.append(Z("s_latao", 0.0068, 0.004, (x, y, h + alto + 0.002), 6))
    return p


# ------------------------------------------------------------------------------------------------ os painéis
def _painel_chapa():
    """Uma telha ondulada de 1,2 x 0,65 m largada no chão: uma ponta enrolada para cima, o canto dobrado e um calombo
    no meio; o galvanizado com as manchas de ferrugem (mais nas bordas)."""
    larg, comp = 0.65, 1.2
    ruido = _ruido(7)

    def deforma(x, y, z):
        z += 0.9 * max(0.0, y - 0.25) ** 2                                   # a ponta enrolada
        z += 0.6 * max(0.0, x - 0.12) * max(0.0, -0.3 - y)                     # o canto dobrado
        z += 0.035 * math.exp(-((x + 0.1) ** 2 + (y + 0.05) ** 2) / 0.02)      # o calombo
        return (x + 0.02 * math.sin(y * 3.0), y, z)

    def cor(c):
        borda = max(abs(c.x) / (larg / 2), abs(c.y) / (comp / 2))
        v = ruido(c.x * 7 + 3, c.y * 7 + 5) + 0.45 * max(0.0, borda - 0.7) / 0.3
        return "s_ferrugem_escura" if v > 0.92 else "s_ferrugem" if v > 0.68 else "s_galvanizado"
    return [(_chapa_ondulada, larg, comp, 4, 0.011, 0.003, 6, deforma, cor)]


def _painel_porta():
    """Uma porta de carro solta, deitada (o lado de fora para cima): a lata pintada de azul desbotado com a ferrugem
    embaixo, a moldura da janela de borracha com os cacos de vidro, a maçaneta, a fechadura, o toco do retrovisor e as
    dobradiças arrancadas (na frente, -Y). 1,05 x 0,98 m."""
    e = 0.065                                 # a grossura
    alto = e
    return [
        C("s_tinta_carro", (0.55, 1.05, e), (0, 0, e / 2), chanfro=0.012),
        # a ferrugem: a faixa de baixo, as manchas e o furo podre
        C("s_ferrugem", (0.06, 0.86, 0.002), (-0.235, 0.03, alto + 0.0005)),
        C("s_ferrugem_escura", (0.09, 0.16, 0.002), (-0.2, -0.28, alto + 0.0008), giro=(0, 0, 18)),
        C("s_ferrugem", (0.11, 0.07, 0.002), (-0.17, 0.3, alto + 0.0008), giro=(0, 0, -12)),
        C("s_ferrugem", (0.05, 0.08, 0.002), (0.05, -0.38, alto + 0.0008), giro=(0, 0, 30)),
        C("s_quase_preto", (0.035, 0.05, 0.002), (-0.215, -0.27, alto + 0.0012), giro=(0, 0, 18)),
        C("s_tinta_carro_escura", (0.12, 0.05, 0.002), (0.06, 0.12, alto + 0.0005), giro=(0, 0, 8)),   # o risco
        # o friso da janela e a moldura (o pilar da frente inclinado, o de trás reto e o de cima)
        C("s_metal_claro", (0.022, 1.0, 0.012), (0.27, 0, alto - 0.004)),
        R("s_borracha", (0.29, -0.49, 0.014), (0.66, -0.22, 0.014), 0.028, 0.03, (0, 0, 1)),
        R("s_borracha", (0.66, -0.235, 0.014), (0.69, 0.485, 0.014), 0.028, 0.03, (0, 0, 1)),
        R("s_borracha", (0.285, 0.47, 0.014), (0.70, 0.47, 0.014), 0.028, 0.03, (0, 0, 1)),
        # os cacos do vidro presos embaixo e num canto
        F("s_vidro", [(0.29, -0.3), (0.36, -0.22), (0.29, -0.12)], 0.010, 0.014),
        F("s_vidro", [(0.29, 0.02), (0.41, 0.12), (0.29, 0.2)], 0.010, 0.014),
        F("s_vidro", [(0.62, 0.45), (0.67, 0.25), (0.67, 0.45)], 0.010, 0.014),
        # a maçaneta, a fechadura e o toco do retrovisor
        C("s_quase_preto", (0.028, 0.15, 0.012), (0.17, 0.33, alto + 0.006)),
        Z("s_metal_claro", 0.009, 0.008, (0.17, 0.44, alto + 0.004), 6),
        C("s_borracha", (0.08, 0.07, 0.05), (0.33, -0.45, 0.025)),
        # as dobradiças arrancadas, na frente
        C("s_metal", (0.07, 0.035, 0.035), (0.13, -0.54, 0.03)),
        C("s_ferrugem", (0.07, 0.035, 0.035), (-0.13, -0.54, 0.03)),
    ]


def _painel_placa():
    """Uma placa de aviso (o losango amarelo com a borda e o ponto de exclamação pretos) caída com o poste de cano
    galvanizado quebrado embaixo dela: a placa apoiada no poste e um canto no chão. 1,5 x 0,72 m."""
    ry, rz, rp = 0.30, 0.0, 0.03             # o meio do losango (em Y), a altura da placa e o raio do poste
    pz = -rp - 0.004                         # o eixo do poste, embaixo da placa
    placa = [
        F("s_preto", [(0, ry + 0.36), (-0.36, ry), (0, ry - 0.36), (0.36, ry)], rz, rz + 0.004),
        F("s_amarelo", [(0, ry + 0.335), (-0.335, ry), (0, ry - 0.335), (0.335, ry)], rz + 0.004, rz + 0.0052),
        C("s_preto", (0.036, 0.20, 0.0012), (0, ry + 0.06, rz + 0.0056)),
        C("s_preto", (0.04, 0.04, 0.0012), (0, ry - 0.09, rz + 0.0056)),
        # a ferrugem nas bordas e dois furos de bala
        C("s_ferrugem", (0.11, 0.05, 0.0012), (0.17, ry + 0.16, rz + 0.0058), giro=(0, 0, -45)),
        C("s_ferrugem_escura", (0.08, 0.04, 0.0012), (-0.21, ry - 0.11, rz + 0.0058), giro=(0, 0, -45)),
        C("s_ferrugem", (0.07, 0.035, 0.0012), (0.06, ry - 0.27, rz + 0.0058), giro=(0, 0, 45)),
        Z("s_quase_preto", 0.011, 0.0014, (-0.13, ry + 0.06, rz + 0.0058), 6),
        Z("s_quase_preto", 0.009, 0.0014, (0.1, ry - 0.03, rz + 0.0058), 6),
        # as braçadeiras e o poste, quebrado embaixo (a ponta rasgada e a ferrugem)
        T("s_metal", (0, ry + 0.215, pz), (0, ry + 0.185, pz), rp + 0.006, 8),
        T("s_metal", (0, ry - 0.185, pz), (0, ry - 0.215, pz), rp + 0.006, 8),
        T("s_galvanizado_escuro", (0, ry + 0.3, pz), (0, -0.78, pz), rp, 8),
        T("s_ferrugem", (0, -0.68, pz), (0, -0.781, pz), rp + 0.0006, 8),
        T("s_quase_preto", (0, -0.77, pz), (0, -0.7815, pz), rp - 0.005, 8),
    ]
    for ang, ponta in ((20, 0.06), (150, 0.04), (265, 0.075)):
        c = Vector((math.cos(math.radians(ang)) * (rp - 0.006), -0.775, pz + math.sin(math.radians(ang)) * (rp - 0.006)))
        placa.append(T("s_galvanizado_escuro", tuple(c), tuple(c + Vector((0, -ponta, 0))), 0.008, 3, 0.0008))
    # deitada no poste: girada no eixo dele até a borda da placa encostar no chão
    return [(loot._com_matriz, Matrix.Rotation(math.radians(11.7), 4, "Y"), placa)]


MODELOS = {
    "Item_Sucata": _sucata, "Item_MunicaoPrego": _municao_prego, "Item_MunicaoParafuso": _municao_parafuso,
    "Painel_Chapa": _painel_chapa, "Painel_Porta": _painel_porta, "Painel_Placa": _painel_placa,
}


# ------------------------------------------------------------------------------------------------ a cena e o FBX
def objeto(nome, pecas, cena):
    """Como o equipamentos._objeto (nome_corpo, a malha nos eixos da Unity, o objeto girado 90° em X), com a origem
    embaixo, no meio da caixa."""
    me = bpy.data.meshes.new(nome + "_corpo")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for p in pecas:
        p[0](bm, uv, *p[1:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    vs = [v.co for v in bm.verts]
    meio = Vector(((min(v.x for v in vs) + max(v.x for v in vs)) / 2, (min(v.y for v in vs) + max(v.y for v in vs)) / 2,
                   min(v.z for v in vs)))
    bmesh.ops.translate(bm, vec=-meio, verts=bm.verts)
    bm.to_mesh(me)
    bm.free()
    for f in me.polygons:
        f.use_smooth = False
    me.materials.append(eq._material())
    me.transform(Matrix.Rotation(math.radians(-90), 4, "X"))
    o = bpy.data.objects.new(nome + "_corpo", me)
    o.rotation_euler = (math.radians(90), 0, 0)
    cena.collection.objects.link(o)
    return o


def construir():
    cena = bpy.context.scene
    for o in list(cena.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    objs = []
    for i, (nome, f) in enumerate(MODELOS.items()):
        o = objeto(nome, f(), cena)
        o.location = (i * 1.6, 0, 0)
        objs.append(o)
    bpy.context.view_layer.update()
    for o in objs:
        vs = [o.matrix_world @ v.co for v in o.data.vertices]
        tam = [max(v[k] for v in vs) - min(v[k] for v in vs) for k in range(3)]
        print(f"  {o.name:28s} {tam[0]:.3f} x {tam[1]:.3f} x {tam[2]:.3f} m (larg x fundo x alto) | "
              f"{sum(len(p.vertices) - 2 for p in o.data.polygons)} triângulos")
    return objs


def exportar(objs):
    for o in bpy.context.scene.objects:
        o.select_set(o in objs)
    bpy.ops.export_scene.fbx(filepath=SAIDA, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")
    print("sucata_itens: gravado", SAIDA)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    objs = construir()
    if (args[0] if args else "tudo") == "tudo":
        exportar(objs)
