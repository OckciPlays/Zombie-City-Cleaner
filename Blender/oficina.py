# A mesa de oficina (06/11/2026, a pedido: a munição feita em casa e os consertos): a bancada pesada de madeira, de 2 m,
# com o tampo grosso de tábuas coladas, as pernas grossas, a morsa azul no canto da frente, à direita, o painel de
# furinhos atrás (1 m de altura, nos dois pés de trás que sobem) com as ferramentas penduradas (o martelo, duas chaves de
# boca, o alicate, a serra e as chaves de fenda na régua), a prateleira em cima do painel, a prateleira de baixo (a
# caixa de ferramentas e as tábuas) e as duas gavetas na frente, embaixo do tampo.
#
# Como rodar (Blender 5.2, em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/oficina.py -- tudo
#   (as etapas: construir, exportar)
# Grava Assets/_Game/Art/Mundo/MesaDeOficina.fbx.
#
# O formato é o do Loot.fbx (as peças e as cores do loot.py, como a Bancada de lá): cada objeto girado 90° em X, com a
# malha nos eixos da Unity; o modelo é feito nos eixos do Blender (o alto em +Z, a frente em -Y, que é o +Z da Unity, a
# mesma frente do carro e da bancada). Cores: blocos da Paleta.png, no material Paleta.
# Os objetos:
# - MesaDeOficina: a mesa (sem as gavetas), a origem no chão, no meio.
# - MesaDeOficina_Gaveta_0 e _1: as gavetas (a 0 do lado -X do Blender, que é o +X da Unity), filhas da mesa, cada uma
#   com a origem no meio da frente dela; abrem puxando para a frente (o +Z da Unity), até uns 45 cm.

import math
import os
import sys

import bpy
from mathutils import Matrix

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import equipamentos as eq  # noqa: E402
import loot as lt  # noqa: E402
from pesca import objeto  # noqa: E402

C, Z, R, F = lt.C, lt.Z, lt.R, lt.F
DESTINO = os.path.join(eq.REPO, "Assets", "_Game", "Art", "Mundo")

eq.CORES.update({"eucatex": (6, 12)})

# medidas (m)
LARGURA, FUNDO, ALTURA = 2.0, 0.8, 0.95     # o tampo: a largura (x), o fundo (y) e a altura de cima
TAMPO = 0.07                                # a grossura do tampo
X_PE, Y_PE_FRENTE, Y_PE_TRAS = 0.92, -0.32, 0.355   # os pés (o meio deles)
PE = 0.09
Y_PAINEL = 0.304                            # o meio do painel de furinhos (encostado na frente dos pés de trás)
TOPO_DO_PAINEL = 1.95
# as gavetas: o meio da frente de cada uma (no espaço do Blender) e o tamanho da frente
GAVETAS = [(-0.335, -0.37, 0.795), (0.335, -0.37, 0.795)]
FRENTE_DA_GAVETA = (0.63, 0.16)


def _mesa():
    w, d, h = LARGURA, FUNDO, ALTURA
    p = []
    # o tampo: 8 tábuas coladas ao longo do x, de cores alternadas
    cores = ["madeira", "madeira_clara", "madeira", "cerne"]
    for k in range(8):
        y = -d / 2 + d / 16 + k * d / 8
        p.append(C(cores[k % 4], (w, d / 8, TAMPO), (0, y, h - TAMPO / 2), chanfro=0.004))
    # os pés (os de trás sobem até em cima do painel)
    for x in (-X_PE, X_PE):
        p.append(C("madeira_escura", (PE, PE, h - TAMPO), (x, Y_PE_FRENTE, (h - TAMPO) / 2), chanfro=0.006))
        p.append(C("madeira_escura", (PE, PE, TOPO_DO_PAINEL + 0.05), (x, Y_PE_TRAS, (TOPO_DO_PAINEL + 0.05) / 2), chanfro=0.006))
    # as travessas de cima (embaixo do tampo): dos lados, atrás e na frente fora das gavetas
    zt = h - TAMPO - 0.06
    for x in (-X_PE, X_PE):
        p.append(C("madeira_escura", (0.05, Y_PE_TRAS - Y_PE_FRENTE - PE, 0.12), (x, (Y_PE_TRAS + Y_PE_FRENTE) / 2, zt)))
    p.append(C("madeira_escura", (2 * X_PE - PE, 0.04, 0.12), (0, Y_PE_TRAS, zt)))
    for s in (-1, 1):
        p.append(C("madeira_escura", (0.2, 0.04, 0.12), (s * 0.775, -0.35, zt)))
    # as travessas de baixo e a prateleira de ripas
    zb = 0.15
    for x in (-X_PE, X_PE):
        p.append(C("madeira_escura", (0.05, Y_PE_TRAS - Y_PE_FRENTE - PE, 0.07), (x, (Y_PE_TRAS + Y_PE_FRENTE) / 2, zb)))
    for y in (Y_PE_FRENTE, Y_PE_TRAS):
        p.append(C("madeira_escura", (2 * X_PE - PE, 0.05, 0.07), (0, y, zb)))
    for k in range(5):
        y = Y_PE_FRENTE + 0.01 + k * (Y_PE_TRAS - Y_PE_FRENTE - 0.02) / 4
        p.append(C("madeira_clara" if k % 2 else "madeira", (2 * X_PE - 0.02, 0.12, 0.025), (0, y, zb + 0.0475), chanfro=0.003))
    # a caixa das gavetas: o fundo, os lados, o meio, atrás e as réguas da frente
    p += [
        C("madeira", (1.36, 0.62, 0.02), (0, -0.04, 0.70)),
        C("madeira", (1.36, 0.02, 0.17), (0, 0.28, 0.795)),
        C("madeira_escura", (1.40, 0.04, 0.035), (0, -0.36, 0.6925)),
    ]
    for x in (-0.67, 0.0, 0.67):
        p.append(C("madeira", (0.02, 0.62, 0.17), (x, -0.04, 0.795)))
        p.append(C("madeira_escura", (0.03 if x else 0.02, 0.04, 0.19), (x, -0.36, 0.795)))
    p += _painel() + _morsa() + _em_cima() + _embaixo()
    return p


def _painel():
    """O painel de furinhos com as ferramentas, a régua das chaves de fenda e a prateleira de cima."""
    y, z0, z1 = Y_PAINEL, ALTURA, TOPO_DO_PAINEL
    yf = y - 0.006                         # a frente do painel
    p = [C("eucatex", (2 * X_PE - 0.03, 0.012, z1 - z0), (0, y, (z0 + z1) / 2))]
    # os furinhos (escuros, na frente)
    for i in range(23):
        for j in range(13):
            p.append(C("carvao", (0.01, 0.003, 0.01), (-0.825 + i * 0.075, yf - 0.0012, 1.0 + j * 0.075)))
    # a régua de cima e a prateleira, com as mãos-francesas
    p += [C("madeira_escura", (2 * X_PE + PE, 0.05, 0.05), (0, Y_PE_TRAS, z1 + 0.025)),
          C("madeira", (2 * X_PE - 0.05, 0.18, 0.025), (0, Y_PE_TRAS - 0.09, z1 + 0.0625), chanfro=0.004)]
    for x in (-0.55, 0.55):
        # o triângulo (u para a frente, v para cima, a grossura em x), embaixo da prateleira, encostado no painel
        de_lado = Matrix.Translation((x, yf, z1 + 0.05)) @ Matrix.Rotation(math.radians(-90), 4, "Z") @ \
            Matrix.Rotation(math.radians(90), 4, "X")
        p.append((lt._com_matriz, de_lado, [F("madeira_escura", [(0.0, 0.0), (0.10, 0.0), (0.0, -0.11)], -0.012, 0.012)]))
    # o que fica na prateleira: as latas, o pote e a caixa
    zp = z1 + 0.075
    p += [Z("lata", 0.05, 0.11, (-0.7, 0.26, zp + 0.055), 12), Z("azul", 0.0505, 0.05, (-0.7, 0.26, zp + 0.05), 12),
          Z("lata", 0.04, 0.09, (-0.58, 0.27, zp + 0.045), 12), Z("vermelho", 0.0405, 0.04, (-0.58, 0.27, zp + 0.04), 12),
          Z("garrafa_verde", 0.04, 0.12, (-0.2, 0.27, zp + 0.06), 10), Z("preto", 0.042, 0.015, (-0.2, 0.27, zp + 0.127), 10),
          C("papelao", (0.22, 0.13, 0.12), (0.45, 0.27, zp + 0.06), chanfro=0.004)]
    # o martelo (o cabo para baixo, a cabeça em cima, em dois pinos)
    yt = yf - 0.018
    p += [C("madeira_clara", (0.032, 0.022, 0.30), (-0.65, yt, 1.32), chanfro=0.005),
          C("aco_escuro", (0.10, 0.03, 0.036), (-0.635, yt, 1.485), chanfro=0.003),
          Z("aco_escuro", 0.02, 0.03, (-0.585, yt, 1.485), 10, "X"),
          C("aco_escuro", (0.06, 0.024, 0.02), (-0.70, yt, 1.475), giro=(0, -28, 0)),
          Z("aco", 0.003, 0.03, (-0.67, yf - 0.012, 1.46), 6, "Y"), Z("aco", 0.003, 0.03, (-0.63, yf - 0.012, 1.46), 6, "Y")]
    # as chaves de boca (a boca aberta em cima, a estrela embaixo), cada uma num pino
    for x, k in ((-0.43, 1.0), (-0.33, 0.78)):
        z = 1.35
        p += [C("aco", (0.024 * k, 0.008, 0.22 * k), (x, yt, z), chanfro=0.002),
              C("aco", (0.058 * k, 0.008, 0.02 * k), (x, yt, z + 0.115 * k)),
              C("aco", (0.015 * k, 0.008, 0.035 * k), (x - 0.0215 * k, yt, z + 0.14 * k)),
              C("aco", (0.015 * k, 0.008, 0.035 * k), (x + 0.0215 * k, yt, z + 0.14 * k)),
              (lt._com_matriz, Matrix.Translation((x, yt, z - 0.125 * k)) @ Matrix.Rotation(math.radians(90), 4, "X"),
               lt._anel("aco", 0.02 * k, 0.008 * k, 0.008, 0.0, 10)),
              Z("aco", 0.003, 0.03, (x, yf - 0.012, z + 0.14 * k), 6, "Y")]
    # o alicate (os cabos vermelhos abertos em V, as pontas de aço)
    xa, za = -0.17, 1.42
    p += [R("vermelho", (xa, yt, za), (xa - 0.03, yt, za - 0.17), 0.016, 0.012, lado=(1, 0, 0)),
          R("vermelho", (xa, yt, za), (xa + 0.03, yt, za - 0.17), 0.016, 0.012, lado=(1, 0, 0)),
          R("aco_escuro", (xa, yt, za - 0.01), (xa, yt, za + 0.07), 0.022, 0.012, lado=(1, 0, 0)),
          Z("aco", 0.003, 0.03, (xa, yf - 0.012, za + 0.02), 6, "Y")]
    # a serra (deitada, os dentes para baixo, o cabo à esquerda, num pino pelo furo do cabo)
    em_pe = Matrix.Translation((0.0, yt, 0.0)) @ Matrix.Rotation(math.radians(90), 4, "X")   # (x, z, grossura) -> (x, y, z)

    def borda(x):   # a borda dos dentes
        return 1.64 + (1.70 - 1.64) * (x - 0.13) / (0.62 - 0.13)
    serra = [F("aco", [(0.13, 1.64), (0.62, 1.70), (0.62, 1.735), (0.13, 1.765)], -0.0015, 0.0015)]
    for k in range(14):
        x = 0.14 + k * 0.034
        serra.append(F("aco_escuro", [(x, borda(x) + 0.002), (x + 0.03, borda(x + 0.03) + 0.002), (x + 0.015, borda(x) - 0.011)],
                       -0.0012, 0.0012))
    serra += [F("madeira", [(0.0, 1.62), (0.15, 1.63), (0.155, 1.78), (0.03, 1.80)], -0.012, 0.012),
              F("carvao", [(0.04, 1.665), (0.105, 1.67), (0.11, 1.745), (0.05, 1.755)], -0.0125, 0.0125)]
    p.append((lt._com_matriz, em_pe, serra))
    p.append(Z("aco", 0.003, 0.03, (0.075, yf - 0.012, 1.735), 6, "Y"))
    # a régua das chaves de fenda (3 chaves, os cabos de cor em cima)
    p.append(C("madeira_escura", (0.22, 0.05, 0.025), (0.75, yf - 0.025, 1.40)))
    for x, cor in ((0.68, "vermelho"), (0.75, "amarelo"), (0.82, "azul")):
        p += [Z(cor, 0.012, 0.09, (x, yf - 0.03, 1.4575), 8), Z("preto", 0.0125, 0.012, (x, yf - 0.03, 1.418), 8),
              Z("aco", 0.003, 0.13, (x, yf - 0.03, 1.33), 6)]
    # a trena e o esquadro
    p += [C("amarelo", (0.07, 0.035, 0.07), (0.52, yt - 0.008, 1.38), chanfro=0.008),
          Z("preto", 0.016, 0.004, (0.52, yt - 0.027, 1.38), 10, "Y"),
          C("aco", (0.30, 0.004, 0.035), (0.45, yt, 1.15)), C("aco", (0.035, 0.004, 0.20), (0.317, yt, 1.235))]
    return p


def _morsa():
    """A morsa azul no canto da frente, à direita (+X do Blender), passando um pouco da borda da frente."""
    x, h = 0.74, ALTURA
    return [
        C("azul", (0.17, 0.19, 0.03), (x, -0.27, h + 0.015), chanfro=0.006),
        C("azul", (0.11, 0.16, 0.07), (x, -0.28, h + 0.065), chanfro=0.008),
        C("azul", (0.16, 0.05, 0.085), (x, -0.20, h + 0.12), chanfro=0.006),
        C("azul", (0.07, 0.06, 0.022), (x, -0.165, h + 0.175), chanfro=0.004),
        C("azul", (0.16, 0.05, 0.085), (x, -0.335, h + 0.12), chanfro=0.006),
        C("aco", (0.16, 0.008, 0.035), (x, -0.2215, h + 0.142)),
        C("aco", (0.16, 0.008, 0.035), (x, -0.3135, h + 0.142)),
        C("aco", (0.045, 0.14, 0.03), (x, -0.40, h + 0.075), chanfro=0.003),
        Z("aco", 0.009, 0.16, (x, -0.43, h + 0.10), 10, "Y"),
        Z("azul", 0.021, 0.03, (x, -0.475, h + 0.10), 12, "Y"),
        Z("aco", 0.005, 0.22, (x, -0.49, h + 0.10), 8, "X"),
        Z("aco_escuro", 0.009, 0.016, (x - 0.11, -0.49, h + 0.10), 10, "X"),
        Z("aco_escuro", 0.009, 0.016, (x + 0.11, -0.49, h + 0.10), 10, "X"),
    ]


def _em_cima():
    """O que fica em cima do tampo: um pedaço de tábua, a caixa de pregos e a lata de tinta."""
    h = ALTURA
    return [
        C("madeira_clara", (0.5, 0.12, 0.025), (-0.3, -0.06, h + 0.0125), giro=(0, 0, 8), chanfro=0.003),
        C("papelao", (0.1, 0.07, 0.05), (0.2, 0.12, h + 0.025), chanfro=0.003),
        Z("lata", 0.07, 0.1, (-0.75, 0.14, h + 0.05), 14),
        Z("azul", 0.0705, 0.045, (-0.75, 0.14, h + 0.045), 14),
        Z("aco_escuro", 0.066, 0.006, (-0.75, 0.14, h + 0.103), 14),
    ]


def _embaixo():
    """Na prateleira de baixo: a caixa de ferramentas vermelha e a pilha de tábuas."""
    zb = 0.15 + 0.06
    return [
        C("vermelho", (0.45, 0.22, 0.18), (-0.45, -0.02, zb + 0.09), chanfro=0.01),
        C("vermelho_escuro", (0.452, 0.222, 0.02), (-0.45, -0.02, zb + 0.13)),
        C("preto", (0.16, 0.03, 0.02), (-0.45, -0.02, zb + 0.2)),
        C("preto", (0.02, 0.03, 0.03), (-0.52, -0.02, zb + 0.19)),
        C("preto", (0.02, 0.03, 0.03), (-0.38, -0.02, zb + 0.19)),
        C("prata", (0.03, 0.006, 0.03), (-0.45, -0.132, zb + 0.13)),
        C("madeira", (0.9, 0.14, 0.03), (0.35, 0.02, zb + 0.015), chanfro=0.003),
        C("madeira_clara", (0.85, 0.12, 0.03), (0.37, 0.05, zb + 0.045), giro=(0, 0, 3), chanfro=0.003),
        C("madeira", (0.8, 0.13, 0.03), (0.33, 0.0, zb + 0.075), giro=(0, 0, -2), chanfro=0.003),
    ]


def _gaveta():
    """Uma gaveta, a origem no meio da frente: a frente com o puxador, o fundo, os lados e atrás (aberta em cima)."""
    lf, af = FRENTE_DA_GAVETA
    prof = 0.53
    return [
        C("madeira_clara", (lf, 0.02, af), (0, 0.01, 0), chanfro=0.004),
        C("aco_escuro", (0.16, 0.015, 0.02), (0, -0.03, 0.02), chanfro=0.002),
        C("aco_escuro", (0.015, 0.025, 0.02), (-0.065, -0.0125, 0.02)),
        C("aco_escuro", (0.015, 0.025, 0.02), (0.065, -0.0125, 0.02)),
        C("madeira", (0.59, prof, 0.012), (0, 0.02 + prof / 2, -0.065)),
        C("madeira", (0.012, prof, 0.12), (-0.289, 0.02 + prof / 2, -0.012)),
        C("madeira", (0.012, prof, 0.12), (0.289, 0.02 + prof / 2, -0.012)),
        C("madeira", (0.59, 0.012, 0.12), (0, 0.014 + prof, -0.012)),
    ]


NOMES = ["MesaDeOficina", "MesaDeOficina_Gaveta_0", "MesaDeOficina_Gaveta_1"]


def _cena():
    return bpy.context.scene


def construir():
    cena = _cena()
    for o in list(cena.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    mesa = objeto("MesaDeOficina", _mesa(), cena)
    for i, (x, y, z) in enumerate(GAVETAS):
        g = objeto(f"MesaDeOficina_Gaveta_{i}", _gaveta(), cena)
        g.parent = mesa
        # no espaço da mesa (girada 90° em X, a malha nos eixos da Unity): (x, z, -y)
        g.location = (x, z, -y)
        g.rotation_euler = (0, 0, 0)
    print(f"oficina: a mesa, {len(mesa.data.polygons)} faces")
    return mesa


def exportar():
    cena = _cena()
    os.makedirs(DESTINO, exist_ok=True)
    for o in cena.objects:
        o.select_set(o.name in NOMES)
    caminho = os.path.join(DESTINO, "MesaDeOficina.fbx")
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")
    print("oficina: gravado", caminho)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    etapa = args[0] if args else "tudo"
    construir()
    if etapa in ("exportar", "tudo"):
        exportar()
