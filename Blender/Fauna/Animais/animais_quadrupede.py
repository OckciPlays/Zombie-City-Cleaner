# Os quadrúpedes: o esqueleto a partir dos pontos de cada bicho (lidos no raio-X, no espaço normalizado do Tripo) e as
# poses de base. Rode depois do animais_kit.py, no mesmo dicionário.
#
# Os pontos (P) de cada bicho, no espaço do FBX normalizado (y para trás, z para cima; o x só nas patas e nas orelhas,
# do lado L, +X; o R é o espelho):
#   "quadril": a raiz do corpo (perto da junta do quadril, no meio)        (y, z)
#   "coluna": os pontos da coluna, do quadril até a base do pescoço        [(y, z), ...]  (o 1º é a ponta do Quadril)
#   "cauda": da base à ponta                                                [(y, z), ...]
#   "pescoco": da base do pescoço até a nuca                               [(y, z), ...]  (o 1º é a ponta do Peito)
#   "focinho": a ponta do focinho (a ponta da Cabeca)                       (y, z)
#   "mandibula": a junta e a ponta do queixo                               ((y, z), (y, z)) ou None
#   "orelha": a base e a ponta (do lado L)                                  ((x, y, z), (x, y, z)) ou None
#   "d": a pata da frente (L): o alto da omoplata, o ombro, o cotovelo, o pulso, o boleto e a ponta   [(x, y, z) × 6]
#   "t": a pata de trás (L): a junta do quadril, o joelho, o jarrete, o boleto e a ponta              [(x, y, z) × 5]
#   "extras": [(nome, cabeça, ponta, pai)] (a galhada, o papo...)

import math
import numpy as np
from mathutils import Vector, Quaternion


def _v(p):
    if len(p) == 2:
        return Vector((0.0, p[0], p[1]))
    return Vector(p)


def ossos_quadrupede(P, k=1.0, y0=0.0):
    """A lista de ossos (nome, cabeça, ponta, pai, deforma) no espaço final (vezes k, o y menos y0)."""
    def f(p):
        v = _v(p)
        return Vector((v.x * k, (v.y - y0) * k, v.z * k))

    def esp(p):
        v = f(p)
        return Vector((-v.x, v.y, v.z))

    o = []
    quadril = f(P["quadril"])
    col = [f(p) for p in P["coluna"]]
    o.append(("Raiz", Vector((0, 0, 0)), Vector((0, -0.3 * max(quadril.z, 0.1), 0)), None, False))
    o.append(("Quadril", quadril, col[0], "Raiz", True))
    # a pelve: do quadril até a base da cauda
    cauda = [f(p) for p in P.get("cauda") or []]
    fim_pelve = cauda[0] if cauda else quadril + Vector((0, 0.1 * k, 0))
    o.append(("Pelve", quadril, fim_pelve, "Quadril", True))
    nomes_col = ["Coluna_1", "Coluna_2", "Peito"]
    if len(col) - 1 != 3:
        nomes_col = ["Coluna_%d" % (i + 1) for i in range(len(col) - 2)] + ["Peito"]
    pai = "Quadril"
    for i, n in enumerate(nomes_col):
        o.append((n, col[i], col[i + 1], pai, True))
        pai = n
    pes = [col[-1]] + [f(p) for p in P["pescoco"]]
    for i in range(len(pes) - 1):
        n = "Pescoco_%d" % (i + 1)
        o.append((n, pes[i], pes[i + 1], pai, True))
        pai = n
    nuca = pes[-1]
    o.append(("Cabeca", nuca, f(P["focinho"]), pai, True))
    if P.get("mandibula"):
        a, b = P["mandibula"]
        o.append(("Mandibula", f(a), f(b), "Cabeca", True))
    pai = "Pelve"
    for i in range(len(cauda) - 1):
        n = "Cauda_%d" % (i + 1)
        o.append((n, cauda[i], cauda[i + 1], pai, True))
        pai = n
    for lado, fn in (("L", f), ("R", esp)):
        if P.get("orelha"):
            a, b = P["orelha"]
            o.append(("Orelha." + lado, fn(a), fn(b), "Cabeca", True))
        d = [fn(p) for p in P["d"]]
        o += [
            ("Omoplata." + lado, d[0], d[1], "Peito", True),
            ("Braco." + lado, d[1], d[2], "Omoplata." + lado, True),
            ("Antebraco." + lado, d[2], d[3], "Braco." + lado, True),
            ("Canela.D." + lado, d[3], d[4], "Antebraco." + lado, True),
            ("Pata.D." + lado, d[4], d[5], "Canela.D." + lado, True),
        ]
        t = [fn(p) for p in P["t"]]
        o += [
            ("Coxa." + lado, t[0], t[1], "Pelve", True),
            ("Perna." + lado, t[1], t[2], "Coxa." + lado, True),
            ("Canela.T." + lado, t[2], t[3], "Perna." + lado, True),
            ("Pata.T." + lado, t[3], t[4], "Canela.T." + lado, True),
        ]
    for n, a, b, pai_ in P.get("extras", []):
        if n.endswith(".L"):
            o.append((n, f(a), f(b), pai_, True))
            o.append((n[:-2] + ".R", esp(a), esp(b), pai_[:-2] + ".R" if pai_.endswith(".L") else pai_, True))
        else:
            o.append((n, f(a), f(b), pai_, True))
    return o


PERNAS = {
    "D.L": ["Braco.L", "Antebraco.L", "Canela.D.L", "Pata.D.L"],
    "D.R": ["Braco.R", "Antebraco.R", "Canela.D.R", "Pata.D.R"],
    "T.L": ["Coxa.L", "Perna.L", "Canela.T.L", "Pata.T.L"],
    "T.R": ["Coxa.R", "Perna.R", "Canela.T.R", "Pata.T.R"],
}


PERNA_BAIXA = ("Antebraco", "Canela", "Pata", "Perna")
PERNA_ALTA = ("Braco", "Coxa")


def ajustar_pesos(ob, rig, d):
    """As regras dos pesos dos quadrúpedes, por cima do calor dos ossos:
    - os ossos .L só do lado esquerdo (x > -15% da distância do pé ao meio) e os .R do direito;
    - os ossos das patas (do braço e da coxa para baixo) longe do meio do corpo (o peito e a barriga entre as patas
      ficam com a coluna) e perto do osso (até 2,2 vezes o raio da pata em volta dele);
    - a mandíbula só abaixo da linha da boca."""
    if d["grupo"] in ("ave", "sapo"):
        return ajustar_pesos_ave(ob, rig, d)
    xf = abs(rig.data.bones["Pata.D.L"].head_local.x) if "Pata.D.L" in rig.data.bones else 0.1
    hq = rig.data.bones["Quadril"].head_local.z

    def regras(V, nomes, D, W):
        P = np.ones((len(V), len(nomes)), bool)
        dono = np.argmax(W, 1)
        for j, n in enumerate(nomes):
            if n.endswith(".L"):
                P[:, j] &= V[:, 0] > -0.15 * xf
            elif n.endswith(".R"):
                P[:, j] &= V[:, 0] < 0.15 * xf
            base = n.split(".")[0]
            if base in PERNA_BAIXA or base in PERNA_ALTA or base == "Omoplata":
                P[:, j] &= (np.abs(V[:, 0]) > 0.42 * xf) | (V[:, 2] < 0.5 * hq)
                b = rig.data.bones[n]
                h, tl = np.array(b.head_local), np.array(b.tail_local)
                dd = tl - h
                tt = ((V - h) @ dd) / max(dd @ dd, 1e-12)
                P[:, j] &= (tt > -0.35) & (tt < 1.35)
            if base in PERNA_BAIXA or base in PERNA_ALTA:
                meus = dono == j
                raio = np.percentile(D[meus, j], 75) if meus.sum() > 5 else 0.05 * rig.get("escala", 1.0)
                P[:, j] &= D[:, j] < (2.2 if base in PERNA_BAIXA else 2.4) * raio
            if base == "Orelha":
                meus = dono == j
                raio = np.percentile(D[meus, j], 75) if meus.sum() > 5 else 0.03 * rig.get("escala", 1.0)
                P[:, j] &= D[:, j] < 2.0 * raio
            if n == "Mandibula":
                b = rig.data.bones[n]
                h, t = np.array(b.head_local), np.array(b.tail_local)
                # abaixo da reta da junta à ponta (no plano yz), com uma folga
                zl = h[2] + (V[:, 1] - h[1]) * (t[2] - h[2]) / (t[1] - h[1] if abs(t[1] - h[1]) > 1e-6 else 1e-6)
                P[:, j] &= (V[:, 2] < zl) & (V[:, 1] < h[1] + 0.01)
        return P
    limpar_pesos(ob, rig, regras)
