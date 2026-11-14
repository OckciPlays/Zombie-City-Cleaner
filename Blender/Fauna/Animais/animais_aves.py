# As aves e o sapo: o esqueleto a partir dos pontos (no espaço normalizado do Tripo, como os quadrúpedes) e os pesos.
# Rode depois do animais_kit.py, no mesmo dicionário.
#
# Os pontos das aves:
#   "quadril": ((y, z) a raiz do corpo, perto da junta das pernas; (y, z) a ponta, para o peito)
#   "peito": (y, z) a base do pescoço (a ponta do Peito)
#   "pescoco": [(y, z), ...] até a nuca;  "focinho": (y, z) a ponta do bico
#   "cauda": [(y, z), ...] da base à ponta (a Pelve vai da raiz à base da cauda)
#   "asa": a asa L: o ombro, o cotovelo, o pulso e a ponta [(x, y, z) × 4] (Asa, Asa_2, Asa_3)
#   "t": a perna L: a junta do quadril, o joelho, o tornozelo, o começo dos dedos e a ponta do dedo do meio [(x, y, z) × 5]
#        (Coxa, Perna, Canela.T, Pata.T: os nomes das pernas de trás dos quadrúpedes, para o mesmo IK)
#   "y0": o y do pivô (entre os pés);  "extras": [(nome, cabeça, ponta, pai)] (o papo do peru)
# O sapo: "quadril", "pelve" (a ponta de trás), "peito", "focinho", "mandibula", "d" (o ombro, o cotovelo, o pulso, a
# mão e a ponta dos dedos), "t" (como a das aves), "papo" ((y, z) o meio do papo, raio), "y0".

import numpy as np
from mathutils import Vector


def _fns(k, y0):
    def f(p):
        v = Vector((0.0, p[0], p[1])) if len(p) == 2 else Vector(p)
        return Vector((v.x * k, (v.y - y0) * k, v.z * k))

    def esp(p):
        v = f(p)
        return Vector((-v.x, v.y, v.z))
    return f, esp


def ossos_ave(P, k=1.0, y0=0.0):
    f, esp = _fns(k, y0)
    q0, q1 = f(P["quadril"][0]), f(P["quadril"][1])
    o = [("Raiz", Vector((0, 0, 0)), Vector((0, -0.3 * max(q0.z, 0.1), 0)), None, False),
         ("Quadril", q0, q1, "Raiz", True),
         ("Peito", q1, f(P["peito"]), "Quadril", True)]
    pes = [f(P["peito"])] + [f(p) for p in P["pescoco"]]
    pai = "Peito"
    for i in range(len(pes) - 1):
        n = "Pescoco_%d" % (i + 1)
        o.append((n, pes[i], pes[i + 1], pai, True))
        pai = n
    o.append(("Cabeca", pes[-1], f(P["focinho"]), pai, True))
    cauda = [f(p) for p in P["cauda"]]
    o.append(("Pelve", q0, cauda[0], "Quadril", True))
    pai = "Pelve"
    for i in range(len(cauda) - 1):
        n = "Cauda_%d" % (i + 1)
        o.append((n, cauda[i], cauda[i + 1], pai, True))
        pai = n
    for lado, fn in (("L", f), ("R", esp)):
        a = [fn(p) for p in P["asa"]]
        o += [("Asa." + lado, a[0], a[1], "Peito", True),
              ("Asa_2." + lado, a[1], a[2], "Asa." + lado, True),
              ("Asa_3." + lado, a[2], a[3], "Asa_2." + lado, True)]
        t = [fn(p) for p in P["t"]]
        o += [("Coxa." + lado, t[0], t[1], "Pelve", True),
              ("Perna." + lado, t[1], t[2], "Coxa." + lado, True),
              ("Canela.T." + lado, t[2], t[3], "Perna." + lado, True),
              ("Pata.T." + lado, t[3], t[4], "Canela.T." + lado, True)]
    for n, a, b, pai_ in P.get("extras", []):
        o.append((n, f(a), f(b), pai_, True))
    return o


def ossos_sapo(P, k=1.0, y0=0.0):
    f, esp = _fns(k, y0)
    q0, q1 = f(P["quadril"][0]), f(P["quadril"][1])
    o = [("Raiz", Vector((0, 0, 0)), Vector((0, -0.3 * max(q0.z, 0.1), 0)), None, False),
         ("Quadril", q0, q1, "Raiz", True),
         ("Pelve", q0, f(P["pelve"]), "Quadril", True),
         ("Peito", q1, f(P["peito"]), "Quadril", True),
         ("Cabeca", f(P["peito"]), f(P["focinho"]), "Peito", True)]
    a, b = P["mandibula"]
    o.append(("Mandibula", f(a), f(b), "Cabeca", True))
    c, r = P["papo"]
    o.append(("Papo", f(c) + Vector((0, 0, 0.3 * r * k)), f(c) - Vector((0, 0, 0.3 * r * k)), "Cabeca", True))
    for lado, fn in (("L", f), ("R", esp)):
        d = [fn(p) for p in P["d"]]
        o += [("Braco." + lado, d[0], d[1], "Peito", True),
              ("Antebraco." + lado, d[1], d[2], "Braco." + lado, True),
              ("Canela.D." + lado, d[2], d[3], "Antebraco." + lado, True),
              ("Pata.D." + lado, d[3], d[4], "Canela.D." + lado, True)]
        t = [fn(p) for p in P["t"]]
        o += [("Coxa." + lado, t[0], t[1], "Pelve", True),
              ("Perna." + lado, t[1], t[2], "Coxa." + lado, True),
              ("Canela.T." + lado, t[2], t[3], "Perna." + lado, True),
              ("Pata.T." + lado, t[3], t[4], "Canela.T." + lado, True)]
    return o


def ajustar_pesos_ave(ob, rig, d):
    """As regras dos pesos das aves e do sapo: os ossos .L só do lado esquerdo e os .R do direito; as asas e as pernas
    perto dos ossos delas; o papo do sapo pela distância do meio dele (uma bola), a mandíbula abaixo da boca."""
    k = rig.get("escala", 1.0)
    P = d["P"]

    def regras(V, nomes, D, W):
        R = np.ones((len(V), len(nomes)), bool)
        dono = np.argmax(W, 1)
        xs = 0.012 * k
        for j, n in enumerate(nomes):
            if n.endswith(".L"):
                R[:, j] &= V[:, 0] > -xs
            elif n.endswith(".R"):
                R[:, j] &= V[:, 0] < xs
            base = n.split(".")[0]
            if base in ("Asa", "Asa_2", "Asa_3", "Coxa", "Perna", "Canela", "Pata", "Braco", "Antebraco"):
                meus = dono == j
                raio = np.percentile(D[meus, j], 80) if meus.sum() > 5 else 0.05 * k
                R[:, j] &= D[:, j] < 2.4 * raio
                b = rig.data.bones[n]
                h, tl = np.array(b.head_local), np.array(b.tail_local)
                dd = tl - h
                tt = ((V - h) @ dd) / max(dd @ dd, 1e-12)
                R[:, j] &= (tt > -0.4) & (tt < 1.4)
            if n == "Papo" and d["grupo"] == "sapo":
                b = rig.data.bones[n]
                c = 0.5 * (np.array(b.head_local) + np.array(b.tail_local))
                r = P["papo"][1] * k
                R[:, j] &= np.linalg.norm(V - c, axis=1) < r
            if n == "Mandibula":
                b = rig.data.bones[n]
                h, t = np.array(b.head_local), np.array(b.tail_local)
                zl = h[2] + (V[:, 1] - h[1]) * (t[2] - h[2]) / (t[1] - h[1] if abs(t[1] - h[1]) > 1e-6 else 1e-6)
                R[:, j] &= (V[:, 2] < zl) & (V[:, 1] < h[1] + 0.01 * k)
        return R
    W = limpar_pesos(ob, rig, regras)
    if d["grupo"] == "sapo":
        # o papo: o peso pela distância do meio (o quanto ele incha)
        nomes = [g.name for g in ob.vertex_groups]
        V = vertices(ob)
        b = rig.data.bones["Papo"]
        c = 0.5 * (np.array(b.head_local) + np.array(b.tail_local))
        r = P["papo"][1] * k
        w = np.clip(1 - np.linalg.norm(V - c, axis=1) / r, 0, 1) ** 0.7
        j = nomes.index("Papo")
        W[:, j] = 0
        W = W / np.maximum(W.sum(1, keepdims=True), 1e-9) * (1 - w[:, None])
        W[:, j] = w
        aplicar_pesos(ob, nomes, W)
