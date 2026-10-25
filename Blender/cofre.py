# O que fica no cofre do motor de todos os carros (o da família e os da cidade): o motor e o que segura as peças.
# As peças que se tiram (a vela, a bateria e o radiador) são as do pecas.py: a Unity as põe nos lugares delas.
#
# Como rodar (Blender 5.2, numa cena qualquer):
#   import importlib, sys; sys.path.append(r"<repo>\Blender"); import cofre; importlib.reload(cofre)
#   cofre.construir()    # refaz as peças na cena "Cofre"
#   cofre.exportar()     # grava Assets/_Game/Art/Veiculos/Cofre.fbx
# Depois, na Unity: Zombie City Cleaner > Montar veículos.
#
# As medidas estão nos eixos da Unity (x para a direita, y para cima, z para a frente do carro) e o formato é o do
# pecas.py (cada objeto girado 90° em X, a malha nos eixos da Unity):
# - Cofre_Motor: a origem no meio do fundo do cofre. O bloco (deitado, com os 4 cilindros de lado a lado), o cabeçote,
#   a tampa de válvulas com os 4 poços das velas (o 2º, a contar da esquerda, é o lugar da vela que se troca; os outros
#   têm o cachimbo e o cabo), o distribuidor, o filtro de ar, o coletor de escape, a bandeja da bateria com os cabos e o
#   reservatório da água.
# - Cofre_Frente: a origem no meio do fundo, junto da parede da frente. O suporte do radiador (a travessa de baixo, as
#   duas colunas) e as mangueiras.
# Os lugares das peças (LUGARES) têm de bater com a MontagemDosVeiculos (CofreDoMotor).

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import equipamentos as eq
import pecas  # noqa: F401  (as cores das peças)

DESTINO = os.path.join(eq.REPO, "Assets", "_Game", "Art", "Veiculos")
NOMES = ["Cofre_Motor", "Cofre_Frente"]

eq.CORES.update({
    "motor": (10, 3), "tampa_valvulas": (13, 3), "borracha": (8, 5), "plastico": (8, 0), "reservatorio": (13, 5),
    "azul": (13, 2), "interior": (10, 0), "cabo_vermelho": (6, 0),
})

# o meio do motor (o bloco) e a altura de cima da tampa de válvulas
MOTOR_X, MOTOR_Z = -0.04, -0.10
TOPO_TAMPA = 0.31
# os poços das velas, de lado a lado (x); o lugar da vela que se troca é o 2º
VELAS_X = [MOTOR_X - 0.15, MOTOR_X - 0.05, MOTOR_X + 0.05, MOTOR_X + 0.15]
VELA_TROCA = 1
VELA_Z = MOTOR_Z + 0.02
# (a Unity) os lugares das peças: a vela (no poço, a base dela), a bateria (a base, na bandeja) e o radiador (o meio
# das costas dele, em relação à parede da frente)
LUGARES = {
    "Vela": (VELAS_X[VELA_TROCA], TOPO_TAMPA - 0.045, VELA_Z),
    "Bateria": (0.40, 0.02, 0.10),
    "Radiador": (0.0, 0.19, -0.12),
}


# ------------------------------------------------------------------ as peças nos eixos da Unity

def _m(p):
    """Um ponto da Unity (x, y para cima, z para a frente) nos eixos de modelagem do equipamentos.py (z para cima, a
    frente em -y). O X vai espelhado: a importação da Unity espelha o X de volta (como no carro.py)."""
    return (-p[0], -p[2], p[1])


def caixa(cor, tam, centro, chanfro=0.006):
    return (eq._caixa, cor, (tam[0], tam[2], tam[1]), _m(centro), (0, 0, 0), chanfro)


def cilindro(cor, raio, comprimento, centro, eixo="y", lados=10):
    return (eq._cilindro, cor, raio, comprimento, _m(centro), {"x": "X", "y": "Z", "z": "Y"}[eixo], lados)


def _tubo_bm(bm, uv, cor, raio, a, b, lados):
    a, b = Vector(_m(a)), Vector(_m(b))
    d = b - a
    r = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=lados, radius1=raio, radius2=raio, depth=d.length)
    giro = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation((a + b) / 2) @ giro, verts=r["verts"])
    eq._pintar(bm, uv, list({f for v in r["verts"] for f in v.link_faces}), cor)


def tubo(cor, raio, pontos, lados=8):
    """Um cabo ou uma mangueira passando pelos pontos (Unity), em trechos retos, com uma bolinha em cada junta."""
    out = []
    for a, b in zip(pontos, pontos[1:]):
        out.append((_tubo_bm, cor, raio, a, b, lados))
    for p in pontos[1:-1]:
        out.append(cilindro(cor, raio * 1.05, raio * 2.1, p, "y", lados))
    return out


# ------------------------------------------------------------------ o motor

def _motor():
    x, z = MOTOR_X, MOTOR_Z
    p = [
        # o bloco e o cárter, o cabeçote, a tampa de válvulas (com a borda) e o coletor de admissão atrás
        caixa("motor", (0.50, 0.17, 0.36), (x, 0.115, z), 0.015),
        caixa("aco_escuro", (0.44, 0.04, 0.28), (x, 0.02, z), 0.01),
        caixa("motor", (0.47, 0.05, 0.30), (x, 0.225, z), 0.01),
        caixa("tampa_valvulas", (0.43, 0.035, 0.24), (x, 0.265, z), 0.012),
        caixa("tampa_valvulas", (0.40, 0.012, 0.20), (x, TOPO_TAMPA - 0.006, z), 0.004),
        caixa("motor", (0.40, 0.06, 0.07), (x, 0.20, z - 0.19), 0.01),
        # a tampa do óleo, amarela
        cilindro("etiqueta", 0.018, 0.012, (x + 0.12, TOPO_TAMPA + 0.002, z - 0.07), "y", 10),
        # o coletor de escape, na frente do bloco, descendo
        caixa("aco_escuro", (0.36, 0.05, 0.035), (x, 0.17, z + 0.195), 0.006),
        caixa("aco_escuro", (0.07, 0.12, 0.06), (x + 0.10, 0.08, z + 0.21), 0.01),
        # a polia e a correia, na ponta esquerda
        cilindro("aco_escuro", 0.06, 0.02, (x - 0.265, 0.12, z + 0.06), "x", 14),
        cilindro("borracha", 0.064, 0.012, (x - 0.267, 0.12, z + 0.06), "x", 14),
        cilindro("aco", 0.04, 0.05, (x - 0.255, 0.23, z + 0.12), "x", 12),   # o alternador
    ]
    # os 4 poços das velas: um anel de borracha; os cachimbos e os cabos (os do 1º, 3º e 4º) até o distribuidor
    dist = (x - 0.27, 0.27, z - 0.16)
    p.append(cilindro("aco_escuro", 0.035, 0.06, (dist[0], 0.24, dist[2]), "y", 10))
    p.append(cilindro("borracha", 0.042, 0.035, dist, "y", 12))
    for i, vx in enumerate(VELAS_X):
        p.append(cilindro("borracha", 0.019, 0.006, (vx, TOPO_TAMPA + 0.001, VELA_Z), "y", 12))
        if i == VELA_TROCA:
            # o poço aberto (o fundo escuro) e o cachimbo dele largado ao lado, com o cabo
            p.append(cilindro("aco_escuro", 0.012, 0.004, (vx, TOPO_TAMPA + 0.003, VELA_Z), "y", 10))
            solto = (vx + 0.035, TOPO_TAMPA + 0.012, VELA_Z - 0.055)
            p.append(cilindro("cabo_vermelho", 0.012, 0.04, solto, "x", 8))
            p += tubo("borracha", 0.0055, [(solto[0] - 0.02, solto[1], solto[2]), (vx - 0.04, 0.34, z - 0.12), (dist[0] + 0.03, 0.30, dist[2])])
            continue
        p.append(cilindro("cabo_vermelho", 0.013, 0.05, (vx, TOPO_TAMPA + 0.025, VELA_Z), "y", 8))
        p += tubo("borracha", 0.0055, [(vx, TOPO_TAMPA + 0.05, VELA_Z), (vx - 0.02, 0.36, z - 0.10), (dist[0] + 0.03, 0.30, dist[2])])
    # o filtro de ar, redondo, em cima à esquerda, e o cano até a admissão
    p.append(cilindro("plastico", 0.11, 0.07, (-0.42, 0.30, -0.18), "y", 18))
    p.append(cilindro("aco_escuro", 0.115, 0.012, (-0.42, 0.338, -0.18), "y", 18))
    p.append(cilindro("aco", 0.012, 0.03, (-0.42, 0.35, -0.18), "y", 8))
    p.append(caixa("aco_escuro", (0.05, 0.22, 0.05), (-0.42, 0.155, -0.18), 0.008))
    p += tubo("borracha", 0.03, [(-0.33, 0.29, -0.20), (x - 0.18, 0.22, z - 0.20)], 10)
    # a bandeja da bateria (com a trava na frente) e os cabos: o vermelho até o motor de arranque, o preto à lataria
    bx, bz = LUGARES["Bateria"][0], LUGARES["Bateria"][2]
    p.append(caixa("aco_escuro", (0.27, 0.02, 0.20), (bx, 0.01, bz), 0.004))
    for s in (-1, 1):
        p.append(caixa("aco_escuro", (0.27, 0.03, 0.012), (bx, 0.025, bz + s * 0.096), 0.003))
    p += tubo("cabo_vermelho", 0.009, [(bx - 0.09, 0.03, bz + 0.11), (bx - 0.16, 0.05, bz + 0.13), (x + 0.20, 0.05, z + 0.17)])
    p += tubo("borracha", 0.009, [(bx + 0.09, 0.03, bz + 0.11), (bx + 0.13, 0.03, bz + 0.20), (0.56, 0.06, bz + 0.22)])
    # o reservatório da água (a tampa azul) e a mangueira fina
    p.append(caixa("reservatorio", (0.10, 0.14, 0.12), (-0.43, 0.07, 0.12), 0.012))
    p.append(cilindro("azul", 0.025, 0.02, (-0.43, 0.15, 0.12), "y", 10))
    p += tubo("borracha", 0.007, [(-0.43, 0.10, 0.18), (-0.38, 0.12, 0.27)])
    return p


def _frente():
    # a travessa de baixo e as duas colunas do suporte (um pouco fora do radiador e dos canos dele) e as mangueiras
    # (a de cima até o motor, a de baixo some embaixo dele)
    p = [caixa("aco_escuro", (0.78, 0.035, 0.06), (0, 0.0175, -0.10), 0.006)]
    for s in (-1, 1):
        p.append(caixa("aco_escuro", (0.035, 0.36, 0.045), (s * 0.36, 0.18, -0.10), 0.006))
        p.append(caixa("borracha", (0.05, 0.02, 0.05), (s * 0.30, 0.04, -0.10), 0.004))
    p += tubo("borracha", 0.02, [(-0.30, 0.30, -0.13), (-0.30, 0.30, -0.22), (-0.22, 0.25, -0.32)], 10)
    p += tubo("borracha", 0.02, [(0.30, 0.08, -0.13), (0.28, 0.06, -0.24), (0.20, 0.04, -0.32)], 10)
    return p


MODELOS = {"Cofre_Motor": _motor, "Cofre_Frente": _frente}


def _cena():
    cena = bpy.data.scenes.get("Cofre") or bpy.data.scenes.new("Cofre")
    bpy.context.window.scene = cena
    return cena


def construir():
    cena = _cena()
    for o in list(cena.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    objs = []
    for nome in NOMES:
        o = eq._objeto(nome, MODELOS[nome](), cena)
        o.name = nome
        o.data.name = nome
        objs.append(o)
    # para ver junto no Blender: a frente no lugar (o cofre do carro da família, 0,885 m), e as peças nos lugares
    frente = cena.objects["Cofre_Frente"]
    frente.location = (0, -0.4425, 0)   # (no Blender, o carro aparece espelhado)
    return objs


def exportar():
    cena = _cena()
    guardado = {}
    for o in cena.objects:
        o.select_set(o.name in NOMES)
        if o.name in NOMES:
            guardado[o.name] = tuple(o.location)
            o.location = (0, 0, 0)
    os.makedirs(DESTINO, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(DESTINO, "Cofre.fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")
    for nome, loc in guardado.items():
        cena.objects[nome].location = loc
