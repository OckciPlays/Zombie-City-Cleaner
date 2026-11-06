# As animações do Brutamonte novo (03/11/2026, a pedido: "o zumbi brutamonte você usou as animações do antigo, eu quero que
# você refaça as animações dele, porque tem umas bem estranhas; ainda mantenha somente na cena de comparação"). As de antes
# eram as do Oco (um homem do tamanho de uma pessoa) passadas para o corpo dele pelo Humanoid: o tronco reto, a cabeça
# olhando em volta sozinha (aqui a massa de bolhas inteira, do peito para cima, está presa à cabeça: girando sozinha, ela
# se soltava dos ombros), as pernas de gente e os braços compridos soltos.
#
# Aqui elas são feitas no esqueleto dele (o Brutamonte_Novo.fbx do brutamonte_novo.py), com as medidas dele: 2,18 m, o
# quadril a 1,27 m, as pernas de 1,11 m, os braços que caem até o joelho. O jeito é o de um gorila pesado: as pernas
# abertas e dobradas, o tronco curvado para a frente, a massa da cabeça indo junto com o peito (o pescoço e a cabeça giram
# quase o mesmo que ele), os ombros para a frente e os braços pendurados, balançando com o peso.
#
#   Idle      parado (laço de 72 quadros): a respiração pesada, o peso de um pé para o outro, um ronco com os ombros
#   Walk      andando (laço de 40 quadros, a 0,9 m/s): os passos pesados, o corpo rolando por cima da perna de apoio
#   Chase     andando atrás (laço de 32 quadros, a 1,6 m/s): mais curvado, os braços balançando mais
#   Correr    o avanço (laço de 18 quadros, a 6 m/s): o tronco baixo como um aríete, os braços socando o ar
#   Attack    a marretada (40 quadros): os dois braços sobem por cima da cabeça e descem juntos no chão
#   HitReact  apanhando (20 quadros): o tranco para trás e o rosnado para a frente
#   Emergir   brotando da terra (108 quadros = 3,6 s, o TempoDeEmergir): a mão direita fura o chão e bate nele, depois a
#             esquerda; ele se puxa para fora, pisa na borda, sobe e ruge de braços abertos
#   Furia     as braçadas (laço de 36 quadros = 1,2 s: uma a cada 0,6 s, o intervaloDaFuria), o tronco girando
#   Gritar    o rugido (66 quadros): se encolhe, se levanta de peito aberto, ruge e bate no peito
#
# Os pés: na caminhada, na perseguição e na corrida, cada quadro é calculado (um gerador de passos): o pé de apoio fica
# parado no chão (o chão anda para trás na velocidade do passo, como o agente anda para a frente), batendo o calcanhar,
# assentando e saindo na ponta (os dedos ficam no chão), e o pé no ar faz a curva até a próxima batida. Nas outras, as
# poses de cada chave (as curvas do equilibrio_oco.py), com os pés plantados.
#
# As mãos: a posição do pulso em relação ao ombro ("rel.L": o braço pendurado acompanha o tronco) ou no mundo ("abs.L": a
# mão plantada no chão), com o peso entre as duas ("peso.L", de 0 a 1); o IK de dois ossos do equilibrio_oco.py faz o
# resto. O mundo do Blender: ele olha para -Y; +X é a esquerda dele; Z para cima; o chão em Z = 0. Os giros são em graus,
# no mundo, relativos ao repouso: (inclinar para a frente, tombar para a esquerda, virar para a esquerda).
#
# Sai Assets/_Game/Art/Characters/Zombie/Especiais/Brutamonte_Novo_Animacoes.fbx (o esqueleto, a malha e uma faixa do NLA
# por animação, com o nome dela), que a cena de comparação dos especiais importa como Generic (os mesmos ossos do modelo).
#
# Rodar (Blender em segundo plano):
#   blender -b --factory-startup --python Blender/Especiais/brutamonte_novo_animacoes.py -- [fotos <pasta>] [so <Nome,...>]

import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
BLENDER = os.path.dirname(AQUI)
for _p in (AQUI, BLENDER):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import equilibrio_oco as eq  # noqa: E402

NOME = "Brutamonte_Novo"
PASTA = os.path.join(eq.REPO, "Assets", "_Game", "Art", "Characters", "Zombie", "Especiais")
FBX_MODELO = os.path.join(PASTA, NOME + ".fbx")
FBX_SAIDA = os.path.join(PASTA, NOME + "_Animacoes.fbx")
FPS = eq.FPS
BAIXO, CIMA = (0, 0, -1), (0, 0, 1)


def log(*a):
    print("[brutamonte anim]", *a, flush=True)


# ================================================================================================ o modelo e as medidas

class Corpo:
    """O esqueleto e as medidas do pé e da mão (do lado esquerdo; o direito é o espelho)."""

    def __init__(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=FBX_MODELO, automatic_bone_orientation=False)
        cena = bpy.context.scene
        cena.render.fps = FPS
        self.arm = next(o for o in cena.objects if o.type == "ARMATURE")
        self.malha = next(o for o in cena.objects if o.type == "MESH")
        self.arm.animation_data_create()
        self.esq = eq.Esqueleto(self.arm)
        ossos = self.arm.data.bones
        mw = self.arm.matrix_world
        self.tornozelo = mw @ ossos["foot.L"].head_local          # o tornozelo no repouso (o pé plano no chão)
        self.bola = mw @ ossos["toe.L"].head_local                 # onde o pé dobra (o pivô da ponta)
        gi = {g.index: g.name for g in self.malha.vertex_groups}
        sola, mao = [], []
        for v in self.malha.data.vertices:
            if not v.groups:
                continue
            g = gi[max(v.groups, key=lambda g: g.weight).group]
            p = self.malha.matrix_world @ v.co
            if g in ("foot.L", "toe.L") and p.z < 0.035:
                sola.append(p)
            elif g == "hand.L":
                mao.append(p)
        self.calcanhar = Vector((self.tornozelo.x, max(p.y for p in sola), 0.0))
        self.ponta = Vector((self.bola.x, min(p.y for p in sola), 0.0))
        # a mão espalmada: do eixo do osso até a palma (o pulso fica nesta altura com a palma no chão)
        w = mw @ ossos["hand.L"].head_local
        palma0 = -(mw @ ossos["hand.L"].matrix_local).to_3x3().col[0].normalized()
        self.chao_da_mao = max((p - w).dot(palma0) for p in mao) + 0.01
        self.alcance_braco = self.esq.comp["upper_arm.L"] + self.esq.comp["forearm.L"]
        self.alcance_perna = self.esq.comp["thigh.L"] + self.esq.comp["shin.L"]
        log(f"o pé: tornozelo {tuple(round(c, 3) for c in self.tornozelo)}, bola {tuple(round(c, 3) for c in self.bola)}, "
            f"calcanhar y {self.calcanhar.y:.3f}, ponta y {self.ponta.y:.3f}; a mão no chão {self.chao_da_mao:.3f}; "
            f"braço {self.alcance_braco:.3f}, perna {self.alcance_perna:.3f}")

    def lado(self, v, lado):
        return Vector((v.x if lado == "L" else -v.x, v.y, v.z))


C = None   # o Corpo (feito no começo)


# ================================================================================================ ajudantes das poses

def coluna(sp, sp2, ch, nk=None, hd=None, r=0.0, y=0.0, rh=None, yh=None):
    """A coluna de uma vez. A massa da cabeça vai do peito para cima: o pescoço e a cabeça seguem o peito (um pouco menos,
    se não for dito), com o mesmo tombo e quase a mesma virada."""
    nk = ch - 2 if nk is None else nk
    hd = ch - 4 if hd is None else hd
    rh = r if rh is None else rh
    yh = y if yh is None else yh
    return {"spine": (sp, r * 0.5, y * 0.5), "spine_02": (sp2, r * 0.8, y * 0.8), "chest": (ch, r, y),
            "neck": (nk, (r + rh) * 0.5, (y + yh) * 0.5), "head": (hd, rh, yh)}


def pe(lado, x, y, ang=0.0, giro=8.0, joelho=(0.25, -1, 0.1)):
    """O pé no chão: (x, y) é onde fica o tornozelo do pé plano (no chão). ang > 0 levanta o calcanhar (gira na bola, os
    dedos ficam no chão); ang < 0 levanta a ponta (gira no calcanhar). giro: a ponta para fora (graus)."""
    s = 1 if lado == "L" else -1
    A = C.lado(C.tornozelo, lado)
    g0 = Vector((A.x, A.y, 0.0))
    yaw = s * giro
    rot = (ang, 0.0, yaw)
    R = eq.rot_mundo(*rot)
    Ry = eq.rot_mundo(0, 0, yaw)
    g = Vector((x, y, 0.0))
    if ang > 0:
        P0 = C.lado(C.bola, lado)
    elif ang < 0:
        P0 = C.lado(C.calcanhar, lado)
    else:
        P0 = g0
    P = g + Ry @ (P0 - g0) + Vector((0, 0, P0.z))
    tornozelo = P + R @ (A - P0)
    dedos = (0.0, 0.0, yaw) if ang > 0 else rot
    return {"pe." + lado: tuple(tornozelo), "foot." + lado: rot, "toe." + lado: dedos,
            "joelho." + lado: (s * joelho[0], joelho[1], joelho[2])}


def pe_no_ar(lado, tornozelo, ang=0.0, giro=8.0, joelho=(0.25, -1, 0.2), dedos=None):
    s = 1 if lado == "L" else -1
    rot = (ang, 0.0, s * giro)
    return {"pe." + lado: tuple(tornozelo), "foot." + lado: rot, "toe." + lado: rot if dedos is None else (dedos, 0.0, s * giro),
            "joelho." + lado: (s * joelho[0], joelho[1], joelho[2])}


def folga(tornozelo, rot, lado, dedo=None, minimo=0.015):
    """Sobe o pé no ar até a sola (o calcanhar e a bola, no giro do pé; a ponta, no dos dedos) ficar acima do chão."""
    R = eq.rot_mundo(*rot)
    Rd = R if dedo is None else eq.rot_mundo(dedo, 0.0, rot[2])
    A = C.lado(C.tornozelo, lado)
    B = C.lado(C.bola, lado)
    bola = tornozelo + R @ (B - A)
    pontos = [tornozelo + R @ (C.lado(C.calcanhar, lado) - A), bola, bola + Rd @ (C.lado(C.ponta, lado) - B)]
    menor = min(p.z for p in pontos)
    return tornozelo + Vector((0, 0, max(0.0, minimo - menor)))


def braco(lado, rel, dedos, palma, cotovelo):
    """O braço pelo pulso em relação ao ombro (a mão vai com o tronco). Os vetores são dados para o lado esquerdo."""
    s = 1 if lado == "L" else -1
    return {"rel." + lado: (s * rel[0], rel[1], rel[2]), "dedos." + lado: (s * dedos[0], dedos[1], dedos[2]),
            "palma." + lado: (s * palma[0], palma[1], palma[2]), "cotovelo." + lado: (s * cotovelo[0], cotovelo[1], cotovelo[2])}


def plantada(lado, x, y, dedos=(0.1, -1, -0.2), cotovelo=(1, 0.4, 0.5), peso=1.0):
    """A mão espalmada no chão (no mundo), com o peso dela."""
    s = 1 if lado == "L" else -1
    return {"abs." + lado: (s * x, y, C.chao_da_mao), "peso." + lado: (peso,), "dedos." + lado: (s * dedos[0], dedos[1], dedos[2]),
            "palma." + lado: BAIXO, "cotovelo." + lado: (s * cotovelo[0], cotovelo[1], cotovelo[2])}


def ambos(f):
    """Os dois lados de uma função de um lado (f("L") e f("R"))."""
    d = dict(f("L"))
    d.update(f("R"))
    return d


POSICOES = ("mao", "dedos", "palma", "cotovelo", "pe", "joelho", "rel", "abs")


def espelhar(c):
    """O espelho em X: as posições e as direções trocam o sinal do X; os giros, o do tombo e o da virada; .L e .R trocam."""
    saida = {}
    for canal, v in c.items():
        nome = canal[:-2] + (".R" if canal.endswith(".L") else ".L") if canal[-2:] in (".L", ".R") else canal
        base = canal.split(".")[0]
        if base == "peso":
            saida[nome] = v
        elif canal == "hips" or base in POSICOES:
            saida[nome] = (-v[0], v[1], v[2])
        else:
            saida[nome] = (v[0], -v[1], -v[2])
    return saida


def suave(k):
    k = max(0.0, min(1.0, k))
    return k * k * (3 - 2 * k)


def curva(pontos, t):
    ts = [p[0] for p in pontos]
    vs = [p[1] for p in pontos]
    return eq._curva(ts, vs, eq._tangentes(ts, vs), t)


def hermite(p0, p1, m0, m1, t):
    t2, t3 = t * t, t * t * t
    return (2 * t3 - 3 * t2 + 1) * p0 + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2) * p1 + (t3 - t2) * m1


# ================================================================================================ a postura de sempre

def braco_pendurado(lado, frente=0.0, fora=0.0, desce=0.0):
    return braco(lado, (0.19 + fora, -0.08 + frente, -0.55 + desce), (0.06, -0.22, -1), (-1, 0.4, 0), (0.6, 1, 0.1))


def neutro():
    d = {"hips": (0.0, 0.04, 1.165), "hips.rot": (12, 0, 0), **coluna(16, 22, 26),
         "shoulder.L": (0, -3, -8), "shoulder.R": (0, 3, 8),
         "peso.L": (0.0,), "peso.R": (0.0,), "abs.L": (0.4, -0.5, 0.1), "abs.R": (-0.4, -0.5, 0.1)}
    d.update(ambos(braco_pendurado))
    d.update(pe("L", 0.27, -0.04, 0, 10, (0.3, -1, 0.1)))
    d.update(pe("R", -0.27, 0.06, 0, 10, (0.3, -1, 0.1)))
    return d


# ================================================================================================ o gerador de passos

ANDAR = dict(quadros=40, vel=0.9, apoio=0.64, largura=0.25, recuo=0.03, giro=9, batida=14, saida=30, plano=0.10,
             calcanhar=0.24, altura_pe=0.11, pico=0.8, quadril=(0.0, 0.05, 1.155), quadril_lado=0.055,
             quadril_sobe=0.016, impacto=0.018, janela=0.16, bacia=(12, 4, 8), coluna=(16, 22, 27), coluna_lado=5,
             coluna_giro=7, aceno=2.5, ombro=6, ombro_alto=3, joelho_fora=0.12, correndo=False,
             braco=dict(ang=15, base=5, fora=0.23, atraso=0.07, alcance=0.90, dobra=0.08, curva=12, aberto=0.03))

PERSEGUIR = dict(ANDAR, quadros=32, vel=1.6, apoio=0.60, largura=0.24, recuo=0.10, batida=12, saida=36,
                 altura_pe=0.15, quadril=(0.0, 0.03, 1.125), quadril_lado=0.05, quadril_sobe=0.022, impacto=0.026,
                 bacia=(16, 4, 10), coluna=(22, 30, 36), coluna_lado=5, coluna_giro=10, aceno=3.5, ombro=9,
                 ombro_alto=4, braco=dict(ang=26, base=12, fora=0.23, atraso=0.08, alcance=0.88, dobra=0.14, curva=14,
                                          aberto=0.05))

CORRER = dict(quadros=18, vel=6.0, apoio=0.36, largura=0.17, recuo=0.16, giro=6, batida=6, saida=52, plano=0.06,
              calcanhar=0.16, altura_pe=0.34, pico=0.55, quadril=(0.0, 0.0, 1.10), quadril_lado=0.03,
              quadril_sobe=0.035, impacto=0.03, janela=0.14, bacia=(26, 3, 12), coluna=(32, 38, 44), coluna_lado=3,
              coluna_giro=14, aceno=3.0, ombro=12, ombro_alto=2, joelho_fora=0.12, correndo=True,
              braco=dict(ang=48, base=18, fora=0.16, atraso=0.04, alcance=0.68, dobra=0.0, curva=4, aberto=0.07))


def passo(G, f):
    """A pose de um quadro do ciclo: as duas pernas (a esquerda bate o calcanhar no quadro 0), o quadril, a coluna e os
    braços balançando ao contrário das pernas."""
    N = G["quadros"]
    phi = f / N
    d = G["apoio"]
    v = G["vel"]
    T = N / FPS
    L_ap = v * d * T                       # quanto o chão anda embaixo do pé de apoio
    y_ini = -L_ap / 2 + G["recuo"]         # onde o tornozelo bate (na frente)
    y_fim = y_ini + L_ap                   # onde ele sai (atrás)
    c = {}
    for lado, off in (("L", 0.0), ("R", 0.5)):
        s = 1 if lado == "L" else -1
        psi = (phi + off) % 1.0
        x = s * G["largura"]
        jo = (G["joelho_fora"], -1, 0.1)
        if psi < d:
            # no chão: bate o calcanhar, assenta, levanta o calcanhar e sai na ponta
            ay = y_ini + L_ap * (psi / d)
            if psi < G["plano"]:
                ang = -G["batida"] * (1 - suave(psi / G["plano"]))
            elif psi > d - G["calcanhar"]:
                k = (psi - (d - G["calcanhar"])) / G["calcanhar"]
                ang = G["saida"] * (k ** 1.6)
            else:
                ang = 0.0
            c.update(pe(lado, x, ay, ang, G["giro"], jo))
        else:
            # no ar: do fim do apoio até a próxima batida, saindo e chegando na velocidade do chão
            u = (psi - d) / (1 - d)
            Tsw = (1 - d) * T
            a0 = Vector(pe(lado, x, y_fim, G["saida"], G["giro"])["pe." + lado])
            a1 = Vector(pe(lado, x, y_ini, -G["batida"], G["giro"])["pe." + lado])
            m = v * Tsw
            y = hermite(a0.y, a1.y, m, m, u)
            z = a0.z + (a1.z - a0.z) * u + G["altura_pe"] * math.sin(math.pi * (u ** G["pico"]))
            ang = curva([(0.0, G["saida"]), (0.3, G["saida"] * (0.75 if G["correndo"] else 0.45)), (0.7, 0.0),
                         (1.0, -G["batida"])], u)
            xx = x - s * 0.025 * math.sin(math.pi * u)
            rot = (ang, 0.0, s * G["giro"])
            dedo = ang * suave(u / 0.25)
            t = folga(Vector((xx, y, z)), rot, lado, dedo)
            jo_ar = (G["joelho_fora"], -1, 0.35 if G["correndo"] else 0.2)
            c.update(pe_no_ar(lado, t, ang, G["giro"], jo_ar, dedos=dedo))
    # o quadril: para o lado da perna de apoio, sobe e desce duas vezes por ciclo, e afunda depois de cada batida
    qx, qy, qz = G["quadril"]
    hx = qx + G["quadril_lado"] * math.cos(2 * math.pi * (phi - d / 2))
    sobe = G["quadril_sobe"] * math.cos(4 * math.pi * (phi - d / 2))
    hz = qz + (-sobe if G["correndo"] else sobe)
    impacto = 0.0
    for off in (0.0, 0.5):
        psi = (phi + off) % 1.0
        if psi < G["janela"]:
            impacto = max(impacto, math.sin(math.pi * psi / G["janela"]))
    hz -= G["impacto"] * impacto
    c["hips"] = (hx, qy, hz)
    bp, br, by = G["bacia"]
    yb = -by * math.cos(2 * math.pi * phi)                   # o quadril vira com a perna que está na frente
    rb = -br * math.cos(2 * math.pi * (phi - d / 2))          # cai do lado da perna no ar
    c["hips.rot"] = (bp + 1.5 * impacto, rb, yb)
    # a coluna: o peito vira ao contrário do quadril e rola por cima da perna de apoio; a massa da cabeça vai junto
    sp, sp2, ch = G["coluna"]
    yc = G["coluna_giro"] * math.cos(2 * math.pi * phi)
    rc = G["coluna_lado"] * math.cos(2 * math.pi * (phi - d / 2))
    aceno = G["aceno"] * impacto
    c["spine"] = (sp + aceno * 0.3, rc * 0.4, yb * 0.6 + yc * 0.4)
    c["spine_02"] = (sp2 + aceno * 0.6, rc * 0.7, yb * 0.3 + yc * 0.7)
    c["chest"] = (ch + aceno, rc, yc)
    c["neck"] = (ch - 2 + aceno * 1.2, rc * 0.8, yc * 0.75)
    c["head"] = (ch - 4 + aceno * 1.4, rc * 0.6, yc * 0.55)
    # os ombros: o do braço da frente vai para a frente; e um tranco para cima na batida
    for lado in "LR":
        s = 1 if lado == "L" else -1
        c["shoulder." + lado] = (0.0, -s * (G["ombro_alto"] + 2 * impacto), -s * 8 + G["ombro"] * math.cos(2 * math.pi * phi))
    # os braços: pêndulos pesados, ao contrário das pernas e um pouco atrasados
    B = G["braco"]
    for lado in "LR":
        s = 1 if lado == "L" else -1
        sw = -s * math.cos(2 * math.pi * (phi - B["atraso"]))     # +1 na frente, -1 atrás
        th = math.radians(B["base"] + B["ang"] * sw)
        Lh = C.alcance_braco * (B["alcance"] - B["dobra"] * max(0.0, sw))
        fora = B["fora"] + B["aberto"] * abs(sw)
        yz = math.sqrt(max(Lh * Lh - fora * fora, 0.01))
        rel = (fora, -yz * math.sin(th), -yz * math.cos(th) + 0.03 * impacto)
        cv = math.radians(B["base"] + B["ang"] * sw + B["curva"] + (40 if G["correndo"] else 0) * max(0.0, sw))
        dedos = (0.08, -math.sin(cv), -math.cos(cv))
        palma = (-1, 0.35, 0) if not G["correndo"] else (-1, 0.0, 0.15)
        cot = (0.55, 1, 0.1) if not G["correndo"] else (0.5, 1, -0.2)
        c.update(braco(lado, rel, dedos, palma, cot))
        c["peso." + lado] = (0.0,)
    return c


# ================================================================================================ as poses-chave

def idle():
    n = neutro()

    def com(dz=0.0, dx=0.0, peito=0.0, ombro=0.0, olha=0.0, braco_l=0.0, braco_r=0.0):
        d = dict(n)
        hx, hy, hz = n["hips"]
        d["hips"] = (hx + dx, hy, hz + dz)
        d["hips.rot"] = (12 - peito * 0.3, -dx * 40, 0)
        d.update(coluna(16 + peito * 0.3, 22 + peito * 0.6, 26 + peito, r=dx * 60, y=olha * 0.5, yh=olha))
        d["shoulder.L"] = (0, -3 - ombro, -8)
        d["shoulder.R"] = (0, 3 + ombro, 8)
        d.update(braco_pendurado("L", frente=braco_l, desce=-dz))
        d.update(braco_pendurado("R", frente=braco_r, desce=-dz))
        return d

    return [
        (0, com()),
        (16, com(dz=0.012, dx=0.018, peito=-3, ombro=3, olha=5, braco_l=-0.01, braco_r=0.01)),     # puxa o ar
        (34, com(dz=-0.012, dx=0.028, peito=2, ombro=-1, olha=8, braco_l=0.01, braco_r=-0.01)),    # solta
        (44, com(dz=-0.016, dx=0.01, peito=4, ombro=5, olha=2, braco_l=0.02, braco_r=0.015)),      # o ronco: os ombros sobem
        (50, com(dz=-0.006, dx=-0.004, peito=1, ombro=-2, olha=-3)),
        (60, com(dz=0.01, dx=-0.02, peito=-2, ombro=2, olha=-7, braco_l=0.012, braco_r=-0.01)),
        (72, com()),
    ], 72


def attack():
    n = neutro()
    pes = {**pe("L", 0.30, -0.12, 0, 12, (0.35, -1, 0.1)), **pe("R", -0.30, 0.14, 0, 12, (0.35, -1, 0.1))}
    subindo = ambos(lambda l: braco(l, (0.30, 0.02, 0.22), (0.5, 0.2, 1), (-0.4, -1, 0), (0.6, 0.6, -0.8)))
    acima = ambos(lambda l: braco(l, (0.12, 0.20, 0.52), (-0.1, 0.6, 1), (-1, 0, 0), (1, 0.2, 0.6)))
    return [
        (0, dict(n)),
        (5, {**pes, "hips": (0.0, 0.07, 1.13), "hips.rot": (16, 0, 0), **coluna(22, 28, 34),
             **ambos(lambda l: braco(l, (0.12, -0.12, -0.46), (0.05, -0.4, -1), (-1, 0.2, 0), (0.8, 1, 0)))}),
        # 13: armando: sobe, o peito arqueia para trás, as mãos juntas lá no alto
        (9, {"hips": (0.0, 0.08, 1.20), "hips.rot": (6, 0, 0), **coluna(4, 0, -4, -6, -8), **subindo,
             "shoulder.L": (0, -10, 0), "shoulder.R": (0, 10, 0)}),
        (13, {"hips": (0.0, 0.08, 1.24), "hips.rot": (0, 0, 0), **coluna(-6, -12, -18, -18, -20), **acima,
              "shoulder.L": (0, -14, 2), "shoulder.R": (0, 14, -2)}),
        (16, {"hips": (0.0, 0.08, 1.25), **coluna(-8, -14, -20, -20, -22)}),
        # 20: a marretada: o tronco desaba e as mãos descem juntas na frente, na altura da barriga de uma pessoa
        (20, {"hips": (0.0, 0.02, 1.02), "hips.rot": (30, 0, 0), **coluna(42, 50, 56),
              **ambos(lambda l: braco(l, (-0.15, -0.50, -0.36), (-0.25, -1, -0.5), (-1, 0, -0.3), (1, 0.4, 0.5))),
              "shoulder.L": (0, 6, -16), "shoulder.R": (0, -6, 16)}),
        (24, {"hips": (0.0, 0.03, 1.00), "hips.rot": (32, 0, 0), **coluna(44, 52, 58),
              **ambos(lambda l: braco(l, (-0.13, -0.47, -0.42), (-0.25, -0.9, -0.7), (-1, 0, -0.3), (1, 0.4, 0.5)))}),
        (30, {"hips": (0.0, 0.04, 1.06), "hips.rot": (26, 0, 0), **coluna(36, 44, 48),
              **ambos(lambda l: braco(l, (-0.02, -0.36, -0.50), (-0.1, -0.7, -1), (-1, 0.2, 0), (0.9, 0.7, 0.3)))}),
        (40, {**n, **pes}),
    ], 40


def hitreact():
    n = neutro()
    return [
        (0, dict(n)),
        # 3: o tranco: o tronco vai para trás e para cima, os braços abrem
        (3, {"hips": (0.0, 0.12, 1.19), "hips.rot": (2, 0, 0), **coluna(2, 4, 6, 2, -2, r=-3, y=-6),
             **ambos(lambda l: braco(l, (0.32, 0.06, -0.48), (0.4, 0.1, -1), (-1, 0.2, 0.2), (0.8, 1, 0))),
             "shoulder.L": (0, -8, 0), "shoulder.R": (0, 8, 0)}),
        (6, {"hips": (0.0, 0.10, 1.18), **coluna(4, 6, 8, 4, 0, r=-2, y=-4)}),
        # 11: o rosnado: se curva para a frente mais que o normal
        (11, {"hips": (0.0, 0.02, 1.13), "hips.rot": (18, 0, 0), **coluna(22, 30, 36, r=1, y=2),
              **ambos(lambda l: braco(l, (0.17, -0.16, -0.52), (0.05, -0.4, -1), (-1, 0.3, 0), (0.7, 1, 0))),
              "shoulder.L": (0, -6, -14), "shoulder.R": (0, 6, 14)}),
        (20, dict(n)),
    ], 20


def furia():
    pes = {**pe("L", 0.42, -0.04, 0, 20, (0.6, -1, 0.1)), **pe("R", -0.42, 0.04, 0, 20, (0.6, -1, 0.1))}
    meio = [
        # 0: armando a braçada da direita: o tronco virado para a direita, o braço direito aberto para trás, na altura do
        # ombro; o esquerdo na frente do peito
        (0, {**pes, "hips": (0.04, 0.03, 1.08), "hips.rot": (12, 3, -16),
             **coluna(16, 18, 18, r=-4, y=-40, yh=-36),
             **braco("R", (0.52, 0.22, 0.02), (0.6, 0.6, 0.4), (0.2, -1, 0), (0.6, 0.6, -0.8)),
             **braco("L", (-0.10, -0.50, -0.14), (-0.4, -1, 0), (-1, 0, -0.2), (1, 0.2, -0.4)),
             "shoulder.R": (0, 6, -10), "shoulder.L": (0, -2, -14), "peso.L": (0.0,), "peso.R": (0.0,)}),
        # 5: a braçada passa na frente, na altura do peito, o braço quase reto
        (5, {"hips": (0.0, 0.0, 1.05), "hips.rot": (14, 0, -2), **coluna(20, 24, 26, r=0, y=-4),
             **braco("R", (0.12, -0.62, -0.06), (-0.4, -1, 0.1), (0, 0, -1), (1, 0.3, -0.4)),
             **braco("L", (0.30, -0.12, -0.32), (0.3, -0.6, -1), (-1, 0.2, 0), (0.6, 1, 0))}),
        # 9: o golpe: o braço direito varre até a esquerda, um pouco abaixo do ombro; o quadril afunda
        (9, {"hips": (-0.04, -0.02, 1.01), "hips.rot": (18, -4, 14), **coluna(26, 30, 32, r=4, y=36),
             **braco("R", (-0.40, -0.42, -0.20), (-1, -0.4, -0.1), (0, 0.2, -1), (0.4, 1, -0.6)),
             **braco("L", (0.46, 0.14, -0.12), (0.6, 0.5, -0.4), (-0.4, 0, -1), (0.6, 0.8, -0.6)),
             "shoulder.R": (0, -4, 18), "shoulder.L": (0, -4, -2)}),
        # 13: o tranco de volta; o braço esquerdo já está aberto para trás, armando o outro lado
        (13, {"hips": (-0.04, 0.0, 1.05), "hips.rot": (14, -3, 16), **coluna(20, 22, 22, r=4, y=40, yh=36),
              **braco("R", (-0.16, -0.40, -0.36), (-0.6, -0.8, -0.4), (1, 0, -0.3), (0.6, 1, -0.2)),
              **braco("L", (0.50, 0.20, 0.00), (0.6, 0.6, 0.4), (0.2, -1, 0), (0.6, 0.6, -0.8)),
              "shoulder.L": (0, -6, 10), "shoulder.R": (0, 2, 14)}),
    ]
    return meio + [(f + 18, espelhar(d)) for f, d in meio], 36


def gritar():
    n = neutro()
    aberto = ambos(lambda l: braco(l, (0.50, 0.04, 0.06), (1, 0.1, 0.3), (0, -1, 0.2), (0.3, 0.6, -1)))
    no_peito = lambda l: braco(l, (-0.16, -0.30, -0.12), (-1, -0.2, 0.3), (0, 1, 0), (1, 0.2, -0.6))   # noqa: E731
    fora = lambda l: braco(l, (0.34, -0.06, -0.10), (0.6, -0.4, 0.6), (-0.3, 0, -1), (1, 0.4, -0.6))   # noqa: E731
    k = [
        (0, dict(n)),
        # 8: se encolhe (o fôlego): curvado, os braços na frente, baixos
        (8, {"hips": (0.0, 0.06, 1.10), "hips.rot": (18, 0, 0), **coluna(26, 34, 42),
             **ambos(lambda l: braco(l, (0.06, -0.26, -0.50), (-0.1, -0.5, -1), (-1, 0.3, 0), (0.8, 1, 0))),
             "shoulder.L": (0, -6, -16), "shoulder.R": (0, 6, 16)}),
        # 16: se levanta: o peito aberto, a massa da cabeça para trás, os braços abertos
        (16, {"hips": (0.0, 0.06, 1.25), "hips.rot": (-2, 0, 0), **coluna(-6, -12, -16, -18, -20), **aberto,
              "shoulder.L": (0, -16, 4), "shoulder.R": (0, 16, -4)}),
        (26, {**coluna(-6, -12, -17, -19, -21, y=10)}),
        (36, {"hips": (0.0, 0.06, 1.24), **coluna(-5, -11, -16, -18, -20, y=-8)}),
        # 40 a 56: bate no peito, uma mão de cada vez
        (40, {"hips": (0.0, 0.05, 1.22), "hips.rot": (2, 0, 0), **coluna(0, -4, -8), **fora("L"), **fora("R"),
              "shoulder.L": (0, -10, 0), "shoulder.R": (0, 10, 0)}),
        (43, {**no_peito("R"), **fora("L"), **coluna(1, -3, -6, r=-2)}),
        (45, {**fora("R"), **fora("L")}),
        (47, {**no_peito("L"), **fora("R"), **coluna(1, -3, -6, r=2)}),
        (49, {**fora("L"), **fora("R")}),
        (51, {**no_peito("R"), **fora("L"), **coluna(1, -3, -6, r=-2)}),
        (53, {**fora("R"), **fora("L")}),
        (55, {**no_peito("L"), **fora("R"), **coluna(1, -3, -6, r=2)}),
        (58, {**fora("L"), **fora("R"), **coluna(4, 4, 6)}),
        (66, dict(n)),
    ]
    return k, 66


def emergir():
    n = neutro()
    chao = C.chao_da_mao

    def pernas(hz, hy=0.04, abre=0.15):
        return {**pe_no_ar("L", (abre, hy + 0.05, hz - 0.98), 22, 8, (0.2, -1, 0.05)),
                **pe_no_ar("R", (-abre, hy + 0.05, hz - 0.98), 22, 8, (0.2, -1, 0.05))}

    para_cima = lambda l, alto=0.60, frente=-0.06: braco(l, (0.04, frente, alto), (0.05, -0.2, 1), (-1, 0, 0), (1, 0.3, 0))  # noqa: E731
    k = []
    # 0: inteiro debaixo da terra, os braços para cima
    hz = -1.95
    k.append((0, {**n, "hips": (0.0, 0.05, hz), "hips.rot": (4, 0, 0), **coluna(6, 8, 8), **pernas(hz),
                  **para_cima("R"), **para_cima("L", 0.46), "shoulder.L": (0, -6, 0), "shoulder.R": (0, 6, 0)}))
    hz = -1.40
    k.append((9, {"hips": (0.0, 0.05, hz), **pernas(hz)}))
    # 14: a mão direita fura o chão (o braço esticado para cima, a garra para fora)
    hz = -0.98
    k.append((14, {"hips": (0.0, 0.04, hz), "hips.rot": (8, -2, 0), **coluna(10, 12, 12, r=-4), **pernas(hz),
                   **para_cima("R", 0.63, -0.10), **para_cima("L", 0.42, -0.10)}))
    hz = -0.82
    k.append((18, {"hips": (0.0, 0.03, hz), **pernas(hz), **coluna(14, 16, 18, r=-5),
                   **braco("R", (0.02, -0.30, 0.56), (0.1, -0.6, 1), (-1, -0.3, 0), (1, 0.3, 0.3))}))
    # 23: a mão direita bate no chão na frente; o alto da massa aparece
    hz = -0.70
    k.append((23, {"hips": (0.0, 0.02, hz), "hips.rot": (18, -3, 0), **coluna(24, 26, 28, r=-4), **pernas(hz),
                   **plantada("R", 0.40, -0.48)}))
    # 30: a esquerda fura o chão
    hz = -0.66
    k.append((30, {"hips": (0.0, 0.02, hz), "hips.rot": (18, 3, 0), **coluna(24, 26, 28, r=3), **pernas(hz),
                   **para_cima("L", 0.62, -0.18)}))
    # 36: a esquerda bate no chão
    hz = -0.60
    k.append((36, {"hips": (0.0, 0.02, hz), "hips.rot": (20, 0, 0), **coluna(26, 28, 30), **pernas(hz),
                   **plantada("L", 0.40, -0.48)}))
    # 48: puxa: a massa e os ombros saem, os cotovelos para cima e para fora
    hz = -0.36
    k.append((48, {"hips": (0.0, -0.02, hz), "hips.rot": (30, 0, 0), **coluna(38, 42, 44), **pernas(hz, 0.0),
                   **plantada("R", 0.40, -0.48, cotovelo=(1, 0.6, 0.8)), **plantada("L", 0.40, -0.48, cotovelo=(1, 0.6, 0.8)),
                   "shoulder.L": (0, -8, -12), "shoulder.R": (0, 8, 12)}))
    # 60: empurra: o peito passa por cima das mãos, os braços quase retos
    hz = -0.04
    k.append((60, {"hips": (0.0, -0.10, hz), "hips.rot": (46, 0, 0), **coluna(54, 58, 60),
                   **pe_no_ar("L", (0.16, 0.10, hz - 0.78), 30, 8, (0.2, -1, 0.3)),
                   **pe_no_ar("R", (-0.16, 0.02, hz - 0.70), 30, 8, (0.2, -1, 0.4)),
                   **plantada("R", 0.40, -0.48, cotovelo=(1, 0.8, 0.3)), **plantada("L", 0.40, -0.48, cotovelo=(1, 0.8, 0.3))}))
    # 72: o pé direito sai e pisa na borda do buraco, o joelho alto
    hz = 0.30
    k.append((72, {"hips": (0.0, -0.04, hz), "hips.rot": (52, -4, 0), **coluna(58, 60, 62, r=-3),
                   **pe("R", -0.26, -0.10, 0, 8, (0.3, -1, 0.7)),
                   **pe_no_ar("L", (0.16, 0.22, -0.40), 30, 8, (0.2, -1, 0.1)),
                   **plantada("R", 0.40, -0.48, cotovelo=(1, 0.8, 0.1)), **plantada("L", 0.40, -0.48, cotovelo=(1, 0.8, 0.1))}))
    # 84: sobe no pé direito; as mãos saem do chão; a perna esquerda ainda no buraco
    hz = 0.74
    k.append((84, {"hips": (0.0, 0.00, hz), "hips.rot": (34, -3, 0), **coluna(38, 40, 40, r=-2),
                   **pe("R", -0.26, -0.10, 0, 8, (0.3, -1, 0.3)),
                   **pe_no_ar("L", (0.20, 0.26, 0.02), 40, 8, (0.2, -1, -0.1)),
                   **ambos(lambda l: braco(l, (0.20, -0.20, -0.50), (0.2, -0.6, -1), (-1, 0.2, 0), (0.8, 1, 0))),
                   "peso.L": (0.0,), "peso.R": (0.0,)}))
    # 94: o pé esquerdo plantado; de pé, os braços se abrindo
    k.append((94, {"hips": (0.0, 0.03, 1.13), "hips.rot": (12, 0, 0), **coluna(10, 8, 6),
                   **pe("R", -0.26, -0.10, 0, 8, (0.3, -1, 0.1)), **pe("L", 0.27, -0.04, 0, 10, (0.3, -1, 0.1)),
                   **ambos(lambda l: braco(l, (0.44, -0.16, -0.24), (0.8, -0.3, -0.4), (0, -0.3, -1), (0.6, 0.6, -0.6))),
                   "shoulder.L": (0, -2, 0), "shoulder.R": (0, 2, 0)}))
    # 100: o rugido: o peito estufado, a massa para trás, os braços abertos no alto
    k.append((100, {"hips": (0.0, 0.05, 1.22), "hips.rot": (0, 0, 0), **coluna(-4, -10, -14, -16, -18),
                    **ambos(lambda l: braco(l, (0.48, 0.04, 0.20), (1, 0, 0.6), (0, -1, 0), (0.6, 0.4, -0.8))),
                    "shoulder.L": (0, -12, 2), "shoulder.R": (0, 12, -2)}))
    k.append((103, {**pe_no_ar("R", (-0.27, -0.03, 0.24), 6, 10, (0.3, -1, 0.3))}))
    k.append((108, dict(n)))
    return k, 108


# o tremor de cada uma (o esforço): (quadro do começo, do pico, do fim, amplitude em graus)
TREMORES = {
    "Emergir": [(40, 50, 62, 1.6), (62, 70, 80, 1.4), (96, 100, 106, 2.2)],
    "Attack": [(19, 21, 28, 2.4)],
    "HitReact": [(2, 4, 9, 1.6)],
    "Gritar": [(15, 18, 38, 2.6)],
}


def tremer(canais, f, lista):
    amp = 0.0
    for ini, pico, fim, a in lista:
        if ini <= f <= fim:
            k = (f - ini) / max(1e-3, pico - ini) if f <= pico else (fim - f) / max(1e-3, fim - pico)
            amp = max(amp, a * suave(k))
    if amp <= 0:
        return canais
    t = f / FPS
    # o tronco e a massa tremem juntos (girando a massa sozinha, ela se soltava dos ombros)
    for i, osso in enumerate(("spine_02", "chest", "neck", "head")):
        if osso in canais:
            p, r, y = canais[osso]
            canais[osso] = (p + amp * (math.sin(2 * math.pi * 9.0 * t) + 0.5 * math.sin(2 * math.pi * 13.7 * t + 1.1)),
                            r + amp * 0.6 * math.sin(2 * math.pi * 7.3 * t + 0.7), y)
    return canais


# ================================================================================================ a montagem

class Laco(eq.Linha):
    """Uma linha que dá a volta (as chaves de antes do começo e de depois do fim são as do outro lado)."""

    def __init__(self, chaves, periodo):
        chaves = [(f, d) for f, d in chaves if f < periodo]   # a chave do último quadro é a do primeiro
        ext = [(f - periodo, d) for f, d in chaves[-2:]] + list(chaves) + [(f + periodo, d) for f, d in chaves[:2]]
        super().__init__(ext)
        self.fim = periodo


def completar(chaves, base):
    """Cada chave com todos os canais: o que ela não diz fica como na chave de antes (a primeira parte da base)."""
    saida, atual = [], dict(base)
    for f, d in sorted(chaves, key=lambda k: k[0]):
        atual = dict(atual)
        atual.update(d)
        saida.append((f, atual))
    return saida


def maos(canais):
    """O pulso de cada mão: do ombro (rel) ou do mundo (abs), pelo peso; tira os canais auxiliares."""
    tronco = {k: v for k, v in canais.items() if k.split(".")[0] not in ("mao", "rel", "abs", "peso")}
    mundo, _ = eq.posar(C.esq, tronco)
    for lado in "LR":
        if "rel." + lado not in canais:
            continue
        ombro = mundo["upper_arm." + lado].translation
        alvo = ombro + Vector(canais["rel." + lado])
        w = canais.get("peso." + lado, (0.0,))[0]
        if w > 1e-4 and "abs." + lado in canais:
            alvo = alvo.lerp(Vector(canais["abs." + lado]), min(1.0, w))
        canais["mao." + lado] = tuple(alvo)
    for k in [k for k in canais if k.split(".")[0] in ("rel", "abs", "peso")]:
        del canais[k]
    return canais


def animacoes():
    """(nome, quadro -> canais, último quadro, laço)."""
    lista = []
    for nome, G in (("Walk", ANDAR), ("Chase", PERSEGUIR), ("Correr", CORRER)):
        lista.append((nome, (lambda G: lambda f: passo(G, f))(G), G["quadros"], True))
    for nome, fazer, laco in (("Idle", idle, True), ("Attack", attack, False), ("HitReact", hitreact, False),
                              ("Emergir", emergir, False), ("Furia", furia, True), ("Gritar", gritar, False)):
        chaves, fim = fazer()
        base = neutro() if nome != "Furia" else chaves[0][1]
        chaves = completar(chaves, base)
        linha = Laco(chaves, fim) if laco else eq.Linha(chaves)
        lista.append((nome, (lambda linha: lambda f: linha.no_quadro(f))(linha), fim, laco))
    return lista


POR_FOLHA = None   # quantos quadros em cada folha das fotos (None: 6 ou 8)
ORDEM = ["Idle", "Walk", "Chase", "Correr", "Attack", "HitReact", "Emergir", "Furia", "Gritar"]


def construir(so=None):
    arm = C.arm
    ad = arm.animation_data
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    relatorio, feitas = {}, []
    lista = {n: (fz, fim, laco) for n, fz, fim, laco in animacoes()}
    for nome in ORDEM:
        if so and nome not in so:
            continue
        fazer, fim, laco = lista[nome]
        acao_nome = "Bruto_" + nome
        if acao_nome in bpy.data.actions:
            bpy.data.actions.remove(bpy.data.actions[acao_nome])
        acao = bpy.data.actions.new(acao_nome)
        acao.use_fake_user = True
        ad.action = acao
        anteriores, piores = {}, {}
        for f in range(0, fim + 1):
            canais = tremer(maos(dict(fazer(f))), f, TREMORES.get(nome, []))
            mundo, erros = eq.posar(C.esq, canais)
            for k, e in erros.items():
                if e > piores.get(k, (-9, 0))[0]:
                    piores[k] = (e, f)
            eq.aplicar(C.esq, mundo, f, anteriores)
        relatorio[nome] = {k: (round(e, 3), f) for k, (e, f) in piores.items() if e > -0.01}
        feitas.append((nome, acao, fim))
    ad.action = None
    for nome, acao, fim in feitas:
        tr = ad.nla_tracks.new()
        tr.name = nome
        s = tr.strips.new(nome, 0, acao)
        s.name = nome
        if hasattr(s, "action_slot") and len(acao.slots):
            s.action_slot = acao.slots[0]
        tr.mute = True
    return relatorio, feitas


def exportar():
    arm, me = C.arm, C.malha
    ad = arm.animation_data
    ad.action = None
    for t in ad.nla_tracks:
        t.mute = False
    cena = bpy.context.scene
    cena.frame_set(0)
    for o in list(cena.objects):
        o.select_set(False)
    arm.select_set(True)
    me.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=FBX_SAIDA, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                             mesh_smooth_type="FACE", use_mesh_modifiers=False, path_mode="STRIP")
    for t in ad.nla_tracks:
        t.mute = True
    log("gravado", FBX_SAIDA)
    return FBX_SAIDA


# ================================================================================================ conferir

def conferir(feitas):
    """Em cada animação: o mais baixo da malha (o que afunda no chão), e na caminhada e na corrida quanto o pé de apoio
    escorrega (a sola mais baixa deveria andar para trás na velocidade do passo)."""
    cena = bpy.context.scene
    arm, me = C.arm, C.malha
    ad = arm.animation_data
    vel = {"Walk": ANDAR["vel"], "Chase": PERSEGUIR["vel"], "Correr": CORRER["vel"]}
    gi = {g.index: g.name for g in me.vertex_groups}
    dono = [gi[max(v.groups, key=lambda g: g.weight).group] if v.groups else "" for v in me.data.vertices]
    saida = {}
    for nome, acao, fim in feitas:
        ad.action = acao
        if hasattr(ad, "action_slot") and len(acao.slots):
            ad.action_slot = acao.slots[0]
        menor_corpo, menor_pe = 9.0, 9.0
        sola_ant, escorrega = {}, 0.0
        for f in range(0, fim + 1):
            cena.frame_set(f)
            dg = bpy.context.evaluated_depsgraph_get()
            ev = me.evaluated_get(dg)
            m = ev.to_mesh()
            mw = me.matrix_world
            sola = {}
            for i, v in enumerate(m.vertices):
                p = mw @ v.co
                if dono[i].startswith(("foot", "toe")):
                    menor_pe = min(menor_pe, p.z)
                    if p.z < 0.012:
                        sola[i] = (dono[i][-1], p.y)
                elif not dono[i].startswith(("hand",)) and nome != "Emergir":
                    menor_corpo = min(menor_corpo, p.z)
            ev.to_mesh_clear()
            if nome in vel:
                # cada vértice que estava no chão e continua: quanto ele andou, menos o chão
                por_lado = {}
                for i, (lado, y) in sola.items():
                    if i in sola_ant:
                        por_lado.setdefault(lado, []).append(y - sola_ant[i][1])
                for lado, dys in por_lado.items():
                    if len(dys) >= 3:
                        dys.sort()
                        escorrega = max(escorrega, abs(dys[len(dys) // 2] - vel[nome] / FPS))
            sola_ant = sola
        saida[nome] = {"pé mais baixo": round(menor_pe, 3), "corpo mais baixo": round(menor_corpo, 3),
                       "escorrega (m por quadro)": round(escorrega, 4) if nome in vel else "-"}
    ad.action = None
    return saida


def fotos(pasta, feitas, tamanho=(380, 440), quadros_de=None):
    """Uma folha por animação (de lado e de frente, uns quadros espalhados), com o Workbench."""
    os.makedirs(pasta, exist_ok=True)
    cena = bpy.context.scene
    arm, me = C.arm, C.malha
    ad = arm.animation_data
    if "ChaoDasFotos" not in bpy.data.objects:
        bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0))
        chao = bpy.context.object
        chao.name = "ChaoDasFotos"
        chao.color = (0.32, 0.30, 0.27, 1)
    me.color = (0.78, 0.74, 0.70, 1)
    cam = bpy.data.objects.get("CameraDasFotos")
    if cam is None:
        cd = bpy.data.cameras.new("CameraDasFotos")
        cd.type = "ORTHO"
        cd.ortho_scale = 3.1
        cam = bpy.data.objects.new("CameraDasFotos", cd)
        cena.collection.objects.link(cam)
    cena.camera = cam
    cena.render.engine = "BLENDER_WORKBENCH"
    cena.display.shading.light = "STUDIO"
    cena.display.shading.color_type = "OBJECT"
    cena.display.shading.show_shadows = False
    cena.render.resolution_x, cena.render.resolution_y = tamanho
    cena.render.film_transparent = False
    vistas = {"lado": ((5.0, -0.1, 1.75), (math.radians(82), 0, math.radians(90))),
              "tres_quartos": ((3.2, -3.8, 1.75), (math.radians(82), 0, math.radians(40)))}
    folhas = []
    for nome, acao, fim in feitas:
        ad.action = acao
        if hasattr(ad, "action_slot") and len(acao.slots):
            ad.action_slot = acao.slots[0]
        n = POR_FOLHA or (8 if fim > 30 else 6)
        quadros = (quadros_de or {}).get(nome) or sorted(set(round(fim * i / (n - 1 if fim > 30 else n)) for i in range(n)))
        linhas = []
        for vista, (pos, rot) in vistas.items():
            cam.location = pos
            cam.rotation_euler = rot
            imgs = []
            for f in quadros:
                cena.frame_set(f)
                cam.location = (pos[0], pos[1], pos[2] if nome != "Emergir" else pos[2] - 0.35)
                caminho = os.path.join(pasta, f"_q_{nome}_{vista}_{f:03d}.png")
                cena.render.filepath = caminho
                bpy.ops.render.render(write_still=True)
                im = bpy.data.images.load(caminho)
                px = np.array(im.pixels[:], dtype=np.float32).reshape(tamanho[1], tamanho[0], 4)
                bpy.data.images.remove(im)
                os.remove(caminho)
                imgs.append(px)
            linhas.append(np.concatenate(imgs, axis=1))
        folha = np.concatenate(linhas[::-1], axis=0)   # (as linhas das imagens do Blender vão de baixo para cima)
        h, w = folha.shape[:2]
        im = bpy.data.images.new("folha_" + nome, w, h, alpha=True)
        im.pixels[:] = folha.ravel()
        im.filepath_raw = os.path.join(pasta, f"Brutamonte_Novo_Anim_{nome}.png")
        im.file_format = "PNG"
        im.save()
        bpy.data.images.remove(im)
        folhas.append(os.path.join(pasta, f"Brutamonte_Novo_Anim_{nome}.png"))
        log("folha", nome, quadros)
    ad.action = None
    return folhas


def principal(argv):
    global C
    pasta = None
    so = None
    if "fotos" in argv:
        pasta = argv[argv.index("fotos") + 1]
    if "so" in argv:
        so = argv[argv.index("so") + 1].split(",")
    if "por_folha" in argv:
        global POR_FOLHA
        POR_FOLHA = int(argv[argv.index("por_folha") + 1])
    C = Corpo()
    rel, feitas = construir(so)
    for nome, piores in rel.items():
        log("ANIMACAO", nome, "longe do alvo:", piores)
    for nome, c in conferir(feitas).items():
        log("CONFERIR", nome, c)
    if pasta:
        fotos(pasta, feitas)
    if not so:
        exportar()


if __name__ == "__main__":
    principal(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
