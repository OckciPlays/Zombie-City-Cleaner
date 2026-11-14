# As poses e o passo dos quadrúpedes. Rode depois do animais_kit.py e do animais_quadrupede.py, no mesmo dicionário.
#
# Bicho: o esqueleto montado (o Corpo do kit), as quatro pernas (a Perna do kit, com o IK) e as medidas (a altura do
# quadril e do ombro, o comprimento das pernas). As ações saem de funções do tempo que devolvem uma Pose:
#   - passo(marcha, t): o andar, o trote e o galope pela fase de cada pata (o pé no chão recua na velocidade, no ar
#     sobe e vai para a frente; a canela e a pata dobram no ar e o calcanhar sobe no fim do apoio), com o corpo subindo e
#     descendo, o balanço, a coluna dobrando no galope, a cabeça firme e a cauda;
#   - em_pe(ajuste): de pé com as patas paradas no chão (pelo IK), com o corpo e a cabeça mexidos pelo ajuste;
#   - o chão de verdade (chao_da_pose): a malha deformada pelo Blender, para as poses deitadas e caídas não entrarem no
#     chão.

import math
import bpy
import numpy as np
from mathutils import Vector, Quaternion, Matrix

TAU = 2 * math.pi


class Bicho:
    def __init__(self, nome, ob, rig):
        self.nome = nome
        self.ob = ob
        self.rig = rig
        self.c = Corpo(rig)
        self.k = rig.get("escala", 1.0)
        nomes = set(self.c.ossos)
        self.pernas = {ch: Perna(self.c, os_, ch) for ch, os_ in PERNAS.items() if all(o in nomes for o in os_)}
        c = self.c
        self.h_quadril = c.cab["Quadril"].z
        self.h_ombro = c.cab["Braco.L"].z if "Braco.L" in nomes else self.h_quadril
        self.perna_d = sum(c.comp(o) for o in PERNAS["D.L"][:3]) if "D.L" in self.pernas else 0
        self.perna_t = sum(c.comp(o) for o in PERNAS["T.L"][:3]) if "T.L" in self.pernas else 0
        self.perna = 0.5 * (self.c.cab["Braco.L"].z + self.c.cab["Coxa.L"].z) if "D.L" in self.pernas else self.h_quadril
        self.cauda = [n for n in c.ordem if n.startswith("Cauda_")]
        self.pescoco = [n for n in c.ordem if n.startswith("Pescoco_")]
        self.coluna = [n for n in ("Coluna_1", "Coluna_2", "Coluna_3", "Peito") if n in nomes]
        self.tem = lambda n: n in nomes

    # -- o FK e o IK ----------------------------------------------------------------------------------------------------

    def fk(self, pose):
        return self.c.fk(pose)

    def pes_no_chao(self, pose, alvos, giros3=None, giros4=None, curvas=None):
        """Resolve as pernas com IK: alvos = {chave: o ponto da cabeça da pata no mundo}. Devolve o maior estiramento."""
        Rw, Hw = self.fk(pose)
        maior = 0.0
        for ch, pe in alvos.items():
            per = self.pernas[ch]
            r = per.resolver(pose, Rw, Hw, pe, (giros3 or {}).get(ch, 0.0), (giros4 or {}).get(ch, 0.0),
                             (curvas or {}).get(ch))
            maior = max(maior, r)
        return maior

    def pe_repouso(self, ch):
        return self.pernas[ch].pe.copy()

    def pata_girada(self, ch, giro4, ponta=None):
        """A cabeça da pata com a pata girada giro4 (no mundo) em volta da ponta (que fica no chão)."""
        per = self.pernas[ch]
        pt = per.ponta if ponta is None else ponta
        return pt - Quaternion(X, giro4) @ per.v4

    def estirado(self, pose, alvos, limite=0.985, giros3=None):
        """O quanto o quadril precisa descer para as pernas de apoio alcançarem os alvos (sem esticar demais)."""
        Rw, Hw = self.fk(pose)
        falta = 0.0
        for ch, pe in alvos.items():
            per = self.pernas[ch]
            d3 = Quaternion(X, (giros3 or {}).get(ch, 0.0)) @ per.d3
            W = Vector(pe) - d3 * per.L3
            D = (W - Hw[per.ossos[0]]).length
            falta = max(falta, D - limite * (per.L1 + per.L2))
        return falta

    # -- a cabeça e o pescoço -------------------------------------------------------------------------------------------

    def firmar_cabeca(self, pose, fracao=0.8, extra=None):
        """Desfaz no pescoço o giro (em volta do X) que o corpo deu ao peito, para a cabeça ficar firme."""
        Rw, Hw = self.fk(pose)
        if not self.pescoco:
            return
        peito = self.coluna[-1] if self.coluna else "Quadril"
        fwd = Rw[peito] @ Vector((0, -1, 0))
        ang = math.atan2(-fwd.z, -fwd.y)       # + = o peito apontando para baixo
        n = len(self.pescoco)
        for p in self.pescoco:
            pose.girar(p, qx(-math.degrees(ang) * fracao / n))

    def olhar(self, pose, abaixar=0.0, virar=0.0, inclinar=0.0, pesos=None):
        """Abaixa (+) ou levanta (-) a cabeça e o pescoço, vira (+ para a esquerda do bicho) e inclina, repartido."""
        cad = self.pescoco + ["Cabeca"]
        pesos = pesos or ([0.45, 0.35, 0.2] if len(cad) == 3 else [1.0 / len(cad)] * len(cad))
        for o, w in zip(cad, pesos):
            pose.girar(o, qz(virar * w) @ qx(abaixar * w) @ qy(inclinar * w))

    # -- o chão de verdade ------------------------------------------------------------------------------------------------

    def aplicar(self, pose):
        """Põe a pose nos pose bones (para o Blender deformar a malha)."""
        c = self.c
        for n in c.ossos:
            pb = self.rig.pose.bones[n]
            R = pose.R.get(n, Quaternion())
            pb.rotation_quaternion = (c.Bi[n] @ R.to_matrix() @ c.B[n]).to_quaternion()
            pb.location = c.Bi[n] @ pose.T.get(n, Vector())
            pb.scale = pose.S.get(n, Vector((1, 1, 1)))
        bpy.context.view_layer.update()

    def malha(self, pose):
        self.aplicar(pose)
        deps = bpy.context.evaluated_depsgraph_get()
        oe = self.ob.evaluated_get(deps)
        me = oe.to_mesh()
        V = np.zeros(len(me.vertices) * 3)
        me.vertices.foreach_get("co", V)
        oe.to_mesh_clear()
        return V.reshape(-1, 3)

    def chao_da_pose(self, pose):
        """O z mais baixo da malha deformada."""
        return float(self.malha(pose)[:, 2].min())

    def assentar(self, pose, folga=0.0, so_subir=False):
        """Sobe ou desce o corpo todo para o ponto mais baixo da malha ficar no chão."""
        z = self.chao_da_pose(pose)
        dz = folga - z
        if so_subir and dz < 0:
            return pose
        pose.mover("Quadril", (0, 0, dz))
        return pose


# ---------------------------------------------------------------------------------------------------------------------
# As marchas
# ---------------------------------------------------------------------------------------------------------------------

MARCHAS_PADRAO = {
    # beta: o tempo no chão (fração do ciclo); fases: o toque de cada pata; passo: o deslocamento do pé no chão em
    # comprimentos da perna; altura: o quanto o pé sobe (em comprimentos da perna); sobe: o sobe e desce do corpo;
    # dobra3/dobra4: a canela e a pata dobrando no ar (graus); bate: o calcanhar subindo no fim do apoio (graus).
    "andar": dict(beta=0.66, fases={"T.L": 0.0, "D.L": 0.25, "T.R": 0.5, "D.R": 0.75}, passo=1.0, altura=0.12,
                  sobe=0.012, dobra3=(55, 45), dobra4=(45, 35), bate=28, rolar=2.0, guinar=2.5, cabeca=2.0,
                  cauda=8.0, coluna=0.0, arfar=0.0),
    "trotar": dict(beta=0.45, fases={"D.L": 0.0, "T.R": 0.02, "D.R": 0.5, "T.L": 0.52}, passo=1.05, altura=0.16,
                   sobe=0.025, dobra3=(80, 60), dobra4=(55, 45), bate=32, rolar=1.0, guinar=1.5, cabeca=2.5,
                   cauda=6.0, coluna=0.0, arfar=0.0),
    "galope": dict(beta=0.30, fases={"T.L": 0.0, "T.R": 0.10, "D.R": 0.45, "D.L": 0.57}, passo=1.2, altura=0.22,
                   sobe=0.05, dobra3=(105, 80), dobra4=(70, 60), bate=38, rolar=1.0, guinar=1.0, cabeca=4.0,
                   cauda=5.0, coluna=9.0, arfar=6.0),
}


def passo_do_pe(fase, beta, S, h, giro_bate, dobra3, dobra4):
    """Onde vai o pé e como dobram a canela e a pata, pela fase (0 = o toque, o pé na frente).
    Devolve (dy, dz, giro3, giro4, no_chao). dy: + = para trás."""
    if fase < beta:
        t = fase / beta
        dy = -S / 2 + S * t
        # o calcanhar sobe no fim do apoio
        g4 = giro_bate * suave((t - 0.62) / 0.38) if t > 0.62 else 0.0
        return dy, 0.0, 0.0, g4, True
    t = (fase - beta) / (1 - beta)
    dy = S / 2 - S * suave(t)
    dz = h * math.sin(math.pi * min(1.0, t * 1.08)) ** 0.9 if t < 0.93 else h * 0.12 * (1 - (t - 0.93) / 0.07)
    g3 = dobra3 * math.sin(math.pi * min(1.0, t * 1.15)) ** 0.8 if t < 0.87 else 0.0
    # a pata: do calcanhar subido ao dobrado e de volta ao chão (no toque, a ponta um pouco para cima)
    if t < 0.4:
        g4 = mix(giro_bate, dobra4, suave(t / 0.4))
    else:
        g4 = mix(dobra4, -6.0, suave((t - 0.4) / 0.6))
    return dy, max(dz, 0.0), g3, g4, False


def passo(b, marcha, t, v, T, extra=None):
    """A pose do bicho no tempo t (0 a 1 do ciclo) da marcha (um dict como os de MARCHAS_PADRAO), na velocidade v
    (m/s) e no período T (s). extra(b, pose, t) mexe no corpo antes das pernas (a cabeça baixa da espreita...)."""
    m = marcha
    pose = Pose()
    L = b.perna
    beta = m["beta"]
    S = v * beta * T                 # o quanto o pé recua no chão
    sobe = m["sobe"] * L
    # o corpo: sobe e desce duas vezes por ciclo (no andar e no trote), o balanço e a guinada pela pata de trás
    zc = -sobe * math.cos(2 * TAU * (t - m.get("fase_sobe", 0.0)))
    if m.get("coluna"):
        zc = -sobe * math.cos(TAU * (t - m.get("fase_sobe", 0.15)))
    pose.mover("Quadril", (0, 0, zc))
    pose.girar("Quadril", qy(m["rolar"] * math.sin(TAU * t)) @ qz(m["guinar"] * math.sin(TAU * t)))
    if m.get("arfar"):
        pose.girar("Quadril", qx(m["arfar"] * math.sin(TAU * (t - m.get("fase_arfar", 0.05)))))
    if m.get("coluna"):
        flex = m["coluna"] * math.cos(TAU * (t - m.get("fase_coluna", 0.25)))
        for o in b.coluna[:-1]:
            pose.girar(o, qx(-flex / max(len(b.coluna) - 1, 1)))
        pose.girar("Pelve", qx(flex * 0.6))
    # a omoplata acompanha a pata da frente
    fases = m["fases"]
    for ch in ("D.L", "D.R"):
        if ch in b.pernas and ch in fases:
            f = (t - fases[ch]) % 1.0
            A = m.get("omoplata", 8.0)
            ang = -A * math.cos(math.pi * f / beta) if f < beta else A * math.cos(math.pi * (f - beta) / (1 - beta))
            pose.girar("Omoplata." + ch[2], qx(ang))
    if extra:
        extra(b, pose, t)
    # as pernas
    alvos, g3s, g4s = {}, {}, {}
    apoio = {}
    for ch, per in b.pernas.items():
        if ch not in fases:
            continue
        f = (t - fases[ch]) % 1.0
        d3, d4 = m["dobra3"], m["dobra4"]
        i = 0 if ch[0] == "D" else 1
        dy, dz, g3, g4, chao = passo_do_pe(f, beta, S, m["altura"] * L, m["bate"], d3[i], d4[i])
        ponta = per.ponta + Vector((0, dy, dz))
        pe = ponta - Quaternion(X, rad(g4)) @ per.v4
        alvos[ch] = pe
        g3s[ch] = rad(g3)
        g4s[ch] = rad(g4)
        if chao:
            apoio[ch] = pe
    # não esticar demais as pernas de apoio: o corpo desce (até um limite; depois a pata fica um pouco no ar)
    desceu = 0.0
    limite = m.get("desce_max", 0.08) * L
    for _ in range(3):
        falta = min(b.estirado(pose, apoio or alvos, 0.97, g3s), limite - desceu)
        if falta <= 0:
            break
        pose.mover("Quadril", (0, 0, -falta))
        desceu += falta
    b.firmar_cabeca(pose, 0.75)
    cab = m["cabeca"]
    b.olhar(pose, abaixar=cab * math.cos(2 * TAU * (t - 0.1)))
    # a cauda
    for i, o in enumerate(b.cauda):
        pose.girar(o, qz(m["cauda"] * math.sin(TAU * t - 0.6 * i)) @ qx(-m.get("cauda_x", 0.0) / max(len(b.cauda), 1)))
    b.pes_no_chao(pose, alvos, g3s, g4s)
    return pose


def periodo(b, marcha, v):
    """O período do ciclo (em quadros) pelo quanto o pé recua (passo × perna) na velocidade v."""
    S = marcha["passo"] * b.perna
    T = S / (v * marcha["beta"])
    n = max(8, int(round(T * FPS)))
    return n, n / FPS


def gravar_ciclo(b, nome, n, fn):
    """Um laço de n quadros (o último igual ao primeiro: n + 1 chaves)."""
    g = Gravador(b.c, b.nome + "_" + nome, ciclica=True)
    for i in range(n + 1):
        g.add(fn((i % n) / n))
    return g.gravar()


def gravar_clipe(b, nome, n, fn):
    """Um clipe de n quadros (sem laço): fn recebe t de 0 a 1."""
    g = Gravador(b.c, b.nome + "_" + nome, ciclica=False)
    for i in range(n):
        g.add(fn(i / max(n - 1, 1)))
    return g.gravar()
