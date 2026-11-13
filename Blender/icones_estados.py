# Os símbolos de estado do HUD (12/11/2026, a pedido do Pedro: "ao invés de ser mostrado como gravidade por cor,
# mostrado como o personagem abalado"): cada estado que dura (a fome, o sangramento, o pânico, a chuva...) ganha um PNG
# de 256 x 256 com o rosto do personagem mostrando o estado. A expressão exagerada conta a gravidade; um objeto pequeno
# num canto do disco (a gota de sangue, o osso, o termômetro, a nuvem com raio...) conta a causa. A cor não conta a
# gravidade: o tom da pele só muda quando faz parte do rosto (pálido, vermelho de febre, cinza, esverdeado, azulado).
#
# O estilo é o dos ícones das habilidades (icones_habilidades.py, de onde vêm a paleta, os materiais e as peças): 3D
# low-poly, nas cores da Paleta.png, a luz quente na frente e o contorno de luz atrás, no EEVEE. A cabeça é uma só; a
# expressão sai de parâmetros (as sobrancelhas, as pálpebras, as pupilas, a boca, o tom da pele e os extras: o suor, a
# baba, as lágrimas, as olheiras, as bochechas, o cabelo arrepiado ou molhado). Os traços do rosto são peças finas
# coladas na curva da cabeça; tudo grande e com contorno escuro, porque no jogo o ícone aparece com 40 a 80 px.
# O fundo é um disco escuro (raio 120 px, a borda suave e um contorno fino claro); fora do disco é transparente.
#
# Como rodar (Blender 5.x, em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/icones_estados.py -- --saida <pasta> [Panico Fome ...]
#   (sem ids, faz os 35; os PNG saem como Estado_<Id>.png, RGBA)
# As folhas de conferência (os 33 em 128 px com o Id embaixo; e a prova em 40 e em 64 px sobre um fundo de jogo
# escuro), com o Python do sistema e o Pillow:
#   python Blender/icones_estados.py folhas --saida <pasta>
#
# A lista ESTADOS (o Id, o que é e a função que monta o rosto) fica no fim do arquivo. Para refazer um ícone, mude a
# função dele e rode só o Id. A Paleta.png do projeto só é lida.

import math
import os
import sys

sys.dont_write_bytecode = True      # o import do outro script não deixa .pyc novo no repositório
AQUI = os.path.dirname(os.path.abspath(__file__))
if AQUI not in sys.path:
    sys.path.insert(0, AQUI)

import icones_habilidades as hab    # noqa: E402  (a paleta, os materiais, a pilha de grupos e as peças low-poly)
from icones_habilidades import (TEM_BLENDER, CORES, rgb, mat, grupo, PILHA, COLECAO, caixa, cilindro, esfera,  # noqa
                                toro, tubo, perfil, revolucao, circulo, estrela, arco_faixa, chama, _borrar, _objeto)

if TEM_BLENDER:
    import bpy
    import bmesh
    from mathutils import Matrix, Vector

TAM = 256               # o lado do ícone
R_DISCO = 120.0         # o raio do disco, em px
ESCALA = 3.0            # o lado do quadro, em metros (a câmera é ortográfica)
PX = TAM / ESCALA       # px por metro
CENTRO = (-5.0, 6.0)    # onde o centro da cabeça cai no quadro (px a partir do meio): um pouco para cima e à esquerda
CANTO = (63.0, -63.0)   # o canto do objeto da causa (embaixo à direita)
CANTO_ALTO = (64.0, 62.0)   # o canto de cima à direita (o "!", o "Zz", a nuvem)
CAM_AZ, CAM_EL = 14.0, 6.0  # quase de frente: 14° à direita, 6° acima

# as cores novas (linha da imagem, coluna) na Paleta.png; as outras vêm do icones_habilidades
CORES.update({
    "pele_clara": (14, 0), "cabelo": (12, 1), "olho_branco": (13, 5), "pupila": (11, 5), "boca": (12, 7),
    "labio": (4, 7), "olheira": (4, 3), "sangue_escuro": (6, 15), "lingua": (11, 13), "dente": (11, 14),
})

# a cabeça: um elipsoide (A, B, C) com o queixo afinando (k); a altura dos olhos, das sobrancelhas, do nariz e da boca
CAB = {"A": 1.0, "B": 0.92, "C": 1.15, "k": 0.20}
OLHO_X, OLHO_Z, SOB_Z, NARIZ_Z, BOCA_Z = 0.37, -0.02, 0.47, -0.25, -0.56


# ----------------------------------------------------------------------------------------------------------------------
# as cores da pele (misturas de duas cores da paleta)

def mistura(a, b, t):
    ca, cb = rgb(a), rgb(b)
    return tuple(ca[i] + (cb[i] - ca[i]) * t for i in range(3))


def _tom(pele):
    if pele in (None, "normal"):
        return rgb("pele")
    cor, t = pele
    return mistura("pele", cor, t)


PALIDA = ("branco_sujo", 0.55)
FEBRE = ("vermelho", 0.36)
CINZA = ("aco", 0.62)
ACINZENTADA = ("aco", 0.45)
ESVERDEADA = ("verde", 0.5)
AZULADA = ("azul_claro", 0.68)
FRIA = ("azul_claro", 0.28)


# ----------------------------------------------------------------------------------------------------------------------
# os desenhos 2D (x, z): recorte, contorno, faixa

def _area(pol):
    return 0.5 * sum(x0 * z1 - x1 * z0 for (x0, z0), (x1, z1) in zip(pol, pol[1:] + pol[:1]))


def _ccw(pol):
    return pol if _area(pol) >= 0 else pol[::-1]


def _norm(v):
    l = math.hypot(v[0], v[1]) or 1.0
    return (v[0] / l, v[1] / l)


def _limpa(pts):
    out = []
    for p in pts:
        if not out or abs(p[0] - out[-1][0]) + abs(p[1] - out[-1][1]) > 1e-5:
            out.append(p)
    while len(out) > 1 and abs(out[0][0] - out[-1][0]) + abs(out[0][1] - out[-1][1]) <= 1e-5:
        out.pop()
    return out


def _clip_semi(pol, a, b, c):
    """Fica a parte do polígono onde a*x + b*z + c >= 0."""
    out = []
    n = len(pol)
    for i in range(n):
        p, q = pol[i], pol[(i + 1) % n]
        fp = a * p[0] + b * p[1] + c
        fq = a * q[0] + b * q[1] + c
        if fp >= 0:
            out.append(p)
        if (fp >= 0) != (fq >= 0):
            t = fp / (fp - fq)
            out.append((p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t))
    return out


def recorte(pol, convexo):
    """O polígono recortado por um polígono convexo."""
    convexo = _ccw(convexo)
    for i in range(len(convexo)):
        p, q = convexo[i], convexo[(i + 1) % len(convexo)]
        dx, dz = q[0] - p[0], q[1] - p[1]
        pol = _clip_semi(pol, -dz, dx, dz * p[0] - dx * p[1])
        if len(pol) < 3:
            return []
    return pol


def _inchar(pol, d):
    """O polígono engordado d para fora (d < 0 emagrece); o contorno escuro dos traços."""
    pol = _ccw(pol)
    n = len(pol)
    out = []
    for i in range(n):
        p0, p1, p2 = pol[i - 1], pol[i], pol[(i + 1) % n]
        n1 = _norm((p1[1] - p0[1], -(p1[0] - p0[0])))
        n2 = _norm((p2[1] - p1[1], -(p2[0] - p1[0])))
        b = _norm((n1[0] + n2[0], n1[1] + n2[1]))
        c = max(0.35, b[0] * n1[0] + b[1] * n1[1])
        out.append((p1[0] + b[0] * d / c, p1[1] + b[1] * d / c))
    return out


def _gira(pol, ang, cx, cz):
    a = math.radians(ang)
    c, s = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * c - (z - cz) * s, cz + (x - cx) * s + (z - cz) * c) for x, z in pol]


def faixa(linha, esp):
    """Uma faixa grossa em volta de uma linha (a sobrancelha, a boca, a lágrima); esp pode variar ponto a ponto."""
    n = len(linha)
    es = list(esp) if isinstance(esp, (list, tuple)) else [esp] * n
    esq, dire = [], []
    for i, (x, z) in enumerate(linha):
        ns = []
        if i > 0:
            ns.append(_norm((-(z - linha[i - 1][1]), x - linha[i - 1][0])))
        if i < n - 1:
            ns.append(_norm((-(linha[i + 1][1] - z), linha[i + 1][0] - x)))
        b = _norm((sum(v[0] for v in ns), sum(v[1] for v in ns)))
        c = max(0.4, b[0] * ns[0][0] + b[1] * ns[0][1])
        h = es[i] / 2 / c
        esq.append((x + b[0] * h, z + b[1] * h))
        dire.append((x - b[0] * h, z - b[1] * h))
    return esq + dire[::-1]


def ret_arred(cx, cz, w, h, r, n=4):
    """O retângulo de cantos redondos (meia largura w, meia altura h)."""
    r = min(r, w, h)
    pts = []
    for qx, qz, a0 in ((cx + w - r, cz + h - r, 0), (cx - w + r, cz + h - r, 90), (cx - w + r, cz - h + r, 180),
                       (cx + w - r, cz - h + r, 270)):
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((qx + r * math.cos(a), qz + r * math.sin(a)))
    return pts


def forma_z(w, h, t):
    """A letra Z (do "Zz" do sono)."""
    return [(-w / 2, h / 2), (w / 2, h / 2), (w / 2, h / 2 - t), (-w / 2 + t * 1.5, -h / 2 + t), (w / 2, -h / 2 + t),
            (w / 2, -h / 2), (-w / 2, -h / 2), (-w / 2, -h / 2 + t), (w / 2 - t * 1.5, h / 2 - t), (-w / 2, h / 2 - t)]


# ----------------------------------------------------------------------------------------------------------------------
# a cabeça e os traços colados nela

def _afina(z):
    return 1.0 - CAB["k"] * max(0.0, -z / CAB["C"]) ** 1.6


def sup(x, z):
    """O y da frente da cabeça (a câmera olha de -Y) no ponto (x, z) do rosto."""
    xu = x / _afina(z)
    q = 1.0 - (xu / CAB["A"]) ** 2 - (z / CAB["C"]) ** 2
    return -CAB["B"] * math.sqrt(max(q, 0.0))


def _sem_sombra(o):
    try:
        o.visible_shadow = False
    except Exception:
        pass
    return o


def decal(pts, m, frente=0.04, fundo=0.06, cortes=2):
    """Um traço do rosto: o desenho (x, z) puxado em Y e colado na curva da cabeça, frente acima da pele."""
    pts = _limpa(pts)
    if len(pts) < 3 or abs(_area(pts)) < 1e-6:
        return None
    bm = bmesh.new()
    vs = [bm.verts.new((x, 0.0, z)) for x, z in pts]
    try:
        f = bm.faces.new(vs)
    except ValueError:
        bm.free()
        return None
    bmesh.ops.triangulate(bm, faces=[f], quad_method="BEAUTY", ngon_method="BEAUTY")
    if cortes:
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cortes, use_grid_fill=True)
    r = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    for e in r["geom"]:
        if isinstance(e, bmesh.types.BMVert):
            e.co.y = 1.0
    for v in bm.verts:
        s = sup(v.co.x, v.co.z)
        v.co.y = s + fundo if v.co.y > 0.5 else s - frente
    return _sem_sombra(_objeto(bm, m, "traco"))


def _cabeca(m_pele):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    for v in bm.verts:
        z = v.co.z * CAB["C"]
        v.co = Vector((v.co.x * CAB["A"] * _afina(z), v.co.y * CAB["B"], z))
    return _objeto(bm, m_pele, "cabeca")


def _cabelo(estilo):
    molhado = estilo in ("molhado", "grudado")
    m = mat(mistura("cabelo", "preto", 0.5), rugo=0.6, espec=0.4) if molhado else mat("cabelo", rugo=0.8, espec=0.3)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    ex, ey, ez, cz0 = (1.05, 0.98, 1.1, 0.06) if estilo == "grudado" else (1.07, 1.0, 1.14, 0.07)
    for v in bm.verts:
        v.co = Vector((v.co.x * ex, v.co.y * ey + 0.04, v.co.z * ez + cz0))
    # a linha do cabelo: alta na testa, baixa na nuca
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-4, plane_co=(0, 0, 0.44),
                           plane_no=Vector((0.0, 0.70, 1.0)).normalized(), clear_inner=True)
    _objeto(bm, m, "cabelo")
    for s in (-1, 1):   # as costeletas: o cabelo dos lados, em cima das orelhas
        esfera(1.0, centro=(s * 0.84, 0.3, 0.42 if estilo != "grudado" else 0.36), m=m, sub=1,
               escala=(0.24, 0.4, 0.34 if estilo != "grudado" else 0.42))
    # a franja: as pontas do cabelo na testa
    n, comp, var = {"normal": (6, 0.12, 0.03), "arrepiado": (6, 0.05, 0.02), "molhado": (8, 0.24, 0.05),
                    "grudado": (8, 0.42, 0.08)}[estilo]
    x0, x1, topo = -0.80, 0.80, 1.0
    passo = (x1 - x0) / n

    def vale(x):
        return 0.86 - 0.2 * (x / 0.8) ** 2

    pts, pontas = [(x0, topo)], []
    for i in range(n):
        xa = x0 + passo * i
        pts.append((xa, vale(xa)))
        xp = xa + passo / 2
        zp = vale(xp) - comp - var * (1 if i % 2 else -1) * (0.5 + 0.5 * math.sin(i * 2.3))
        pts.append((xp, zp))
        pontas.append((xp, zp))
    pts += [(x1, vale(x1)), (x1, topo)]
    decal(pts, m, frente=0.075, fundo=0.04)
    if estilo == "grudado":
        # as mechas grudadas dos lados, descendo pelas têmporas
        for s in (-1, 1):
            decal([(s * 0.86, 0.62), (s * 0.70, 0.66), (s * 0.80, 0.18), (s * 0.90, 0.30)], m, frente=0.05, fundo=0.04)
    if estilo == "arrepiado":
        # os fios em pé, para fora da cabeça (o susto)
        for i, ang in enumerate((-78, -56, -33, -11, 11, 33, 56, 78)):
            a = math.radians(ang)
            d = Vector((math.sin(a), -0.22, math.cos(a))).normalized()
            base = Vector((1.07 * math.sin(a) * 0.9, 0.0, 0.05 + 1.2 * math.cos(a) * 0.9))
            h = 0.50 + 0.10 * (i % 2)
            giro = tuple(math.degrees(g) for g in d.to_track_quat("Z", "Y").to_euler())
            cilindro(0.15, h, centro=tuple(base + d * h / 2), giro=giro, m=m, lados=5, r2=0.0)
    return pontas


def _sobrancelha(cx, cz, s, alto, ang, m, curva=0.05, grossa=1.0, comp=1.0):
    """A sobrancelha; ang > 0 levanta a ponta de dentro (preocupado), ang < 0 abaixa (bravo, esforço)."""
    L = 0.46 * comp
    linha, es = [], []
    for i in range(7):
        t = i / 6
        linha.append((-L / 2 + L * t, curva * (1 - (2 * t - 1) ** 2)))
        es.append((0.13 + 0.07 * t) * grossa)
    pol = faixa(linha, es)
    pol = _gira(pol, ang, 0.0, 0.0)
    if s > 0:
        pol = [(-x, z) for x, z in pol]
    decal([(cx + x, cz + alto + z) for x, z in pol], m, frente=0.09, fundo=0.05)


def _olho(cx, cz, s, p, tom):
    escuro = mat("pupila", rugo=0.4)
    fechado = p.get("fechado")
    if fechado:
        if fechado == "feliz":      # ^
            pol = arco_faixa(0.24, 0.12, 15, 165, n=12, cx=cx, cz=cz - 0.12)
        elif fechado == "calmo":    # o arco para baixo, aliviado
            pol = arco_faixa(0.24, 0.12, 195, 345, n=12, cx=cx, cz=cz + 0.14)
        elif fechado == "apertado":  # > <, a ponta para o nariz
            q = -s
            pol = faixa([(cx - q * 0.17, cz + 0.18), (cx + q * 0.16, cz), (cx - q * 0.17, cz - 0.18)], 0.11)
        else:                       # "linha": dormindo
            pol = faixa([(cx - 0.25, cz - 0.0), (cx - 0.1, cz - 0.07), (cx + 0.1, cz - 0.07), (cx + 0.25, cz - 0.0)],
                        0.09)
        decal(pol, escuro, frente=0.05)
        return
    abre, larg, alto = p.get("abre", 1.0), p.get("larg", 1.0), p.get("alto", 1.0)
    rx, rz = 0.25 * larg, 0.30 * alto
    if abre > 1.0:
        rz *= abre
        rx *= 1 + (abre - 1) * 0.6
    elipse = circulo(rx, 24, cx, cz, rz=rz)
    cortes = []
    m_inc = -s * math.tan(math.radians(p.get("inc", 0.0)))
    if abre < 1.0:
        zl = cz + rz * (2 * abre - 1)
        cortes.append((m_inc, -1.0, zl - m_inc * cx))      # embaixo da pálpebra de cima
    if p.get("baixo", 0.0) > 0:
        zb = cz - rz + 2 * rz * p["baixo"]
        cortes.append((0.0, 1.0, -zb))                      # em cima da pálpebra de baixo
    branco = elipse
    for c in cortes:
        branco = _clip_semi(branco, *c)
    borda = circulo(rx + 0.055, 24, cx, cz + 0.012, rz=rz + 0.06)
    for a, b, c in cortes:
        borda = _clip_semi(borda, a, b, c + (0.07 if b < 0 else 0.05))
    decal(borda, escuro, frente=0.03)
    if abre < 0.97:
        a, b, c = cortes[0]
        palp = _clip_semi(circulo(rx + 0.08, 24, cx, cz + 0.02, rz=rz + 0.09), -a, 1.0, -(c + 0.07))
        decal(palp, mat(mistura_t(tom, "olheira", 0.16), rugo=0.75, espec=0.3), frente=0.045)
    if len(branco) < 3:
        return
    decal(branco, mat("olho_branco", rugo=0.4, brilho=0.15), frente=0.04)
    rp = 0.125 * p.get("pupila", 1.0)
    ox, oz = p.get("olhar", (0.0, 0.0))
    px, pz = cx + ox * (rx - rp * 0.9), cz + oz * (rz - rp * 0.9)
    pup = recorte(circulo(rp, 18, px, pz), branco)
    if len(pup) >= 3:
        decal(pup, escuro, frente=0.05)
        if p.get("brilho", True) and rp > 0.085:
            br = recorte(circulo(rp * 0.34, 10, px - rp * 0.36, pz + rp * 0.38), pup)
            if len(br) >= 3:
                decal(br, mat("branco", brilho=1.0), frente=0.06)


def mistura_t(tom, cor, t):
    c = rgb(cor)
    return tuple(tom[i] + (c[i] - tom[i]) * t for i in range(3))


def _lingua_fora(mx, topo, larg=1.0, comp=1.0, giro=0.0):
    tw, tl = 0.11 * larg, 0.30 * comp
    pts = [(mx + tw, topo)]
    for i in range(13):
        a = math.radians(-180 * i / 12)
        pts.append((mx + tw * math.cos(a), topo - tl + tw + tw * math.sin(a)))
    pts.append((mx - tw, topo))
    pts = _gira(pts, giro, mx, topo)
    risco = _gira(faixa([(mx, topo - 0.03), (mx, topo - tl * 0.62)], 0.03), giro, mx, topo)
    return [(_inchar(pts, 0.032), mat("boca"), 0.07), (pts, mat("lingua", rugo=0.5), 0.075),
            (risco, mat("labio"), 0.08)]


def _boca(p, tom):
    """A boca; devolve os dois cantos (para a baba)."""
    tipo = p.get("tipo", "linha")
    mx, mz = p.get("dx", 0.0), BOCA_Z + p.get("dz", 0.0)
    larg, abre = p.get("larg", 1.0), p.get("abre", 1.0)
    escuro = mat("boca", rugo=0.6)
    seco = p.get("seco", 0.0)
    m_lab = mat(mistura("labio", "branco_sujo", 0.62) if seco else "labio", rugo=0.7)
    dente = mat("dente", rugo=0.35, brilho=0.12)
    m_lingua = mat("lingua", rugo=0.5)
    pecas = []
    w = 0.3 * larg
    cz_cantos = mz
    if tipo == "linha":
        w = 0.34 * larg
        lin, es = [], []
        for i in range(13):
            t = -1 + 2 * i / 12
            z = (mz + p.get("curva", 0.0) * (t * t - 0.4) + p.get("onda", 0.0) * math.sin(t * math.pi * p.get("ondas", 2.0))
                 + p.get("torto", 0.0) * t + p.get("assim", 0.0) * max(t, 0.0) ** 2)
            lin.append((mx + w * t, z))
            es.append(p.get("esp", 0.12) * (1 + p.get("engrossa", 0.0) * max(t, 0.0)))
        pecas.append((faixa(lin, es), escuro, 0.045))
        cantos = [lin[0], lin[-1]]
    elif tipo in ("O", "aberta", "beico"):
        rx, rz, borda = {"O": (0.14, 0.18, 0.05), "aberta": (0.25, 0.15, 0.05), "beico": (0.07, 0.08, 0.08)}[tipo]
        rx, rz = rx * larg, rz * abre
        inter = circulo(rx, 20, mx, mz - 0.03, rz=rz)
        cor_lab = mat(mistura("labio", "lingua", 0.45), rugo=0.6) if tipo == "beico" else m_lab
        pecas += [(_inchar(inter, borda), cor_lab, 0.035), (inter, escuro, 0.045)]
        if tipo == "aberta" and p.get("lingua_baixo", True):
            ling = recorte(circulo(rx * 0.62, 14, mx, mz - 0.03 - rz * 0.8, rz=rz * 0.55), inter)
            if len(ling) >= 3:
                pecas.append((ling, m_lingua, 0.055))
        w = rx
        cantos = [(mx - rx, mz - 0.03), (mx + rx, mz - 0.03)]
        if seco >= 1.0:
            # os lábios rachados: os riscos escuros atravessando o lábio
            for ang in (70, 105, 135, 235, 270, 300):
                a = math.radians(ang)
                r0, r1 = 0.6, 1.0
                pecas.append((faixa([(mx + rx * math.cos(a) * 0.95, mz - 0.03 + rz * math.sin(a) * 0.95),
                                     (mx + (rx + borda + 0.01) * math.cos(a), mz - 0.03 + (rz + borda + 0.01) * math.sin(a))],
                                    0.03), escuro, 0.05))
    elif tipo == "grito":
        rx, rz = 0.28 * larg, 0.34 * abre
        cz = mz - 0.1
        inter = circulo(rx, 24, mx, cz, rz=rz)
        pecas += [(_inchar(inter, 0.055), m_lab, 0.035), (inter, escuro, 0.045)]
        topo = recorte([(mx - 1, cz + rz - 0.095), (mx + 1, cz + rz - 0.095), (mx + 1, cz + 1), (mx - 1, cz + 1)], inter)
        if len(topo) >= 3:
            pecas.append((topo, dente, 0.055))
        ling = recorte(circulo(rx * 0.62, 16, mx, cz - rz * 0.78, rz=rz * 0.42), inter)
        if len(ling) >= 3:
            pecas.append((ling, m_lingua, 0.055))
        w = rx
        cantos = [(mx - rx, cz), (mx + rx, cz)]
    elif tipo == "careta":
        w, h = 0.33 * larg, 0.13 * abre
        fora = ret_arred(mx, mz, w + 0.05, h + 0.05, 0.11)
        den = ret_arred(mx, mz, w, h, 0.07)
        pecas += [(fora, escuro, 0.04), (den, dente, 0.05)]
        if p.get("zig"):
            n = 7
            lin = [(mx - w + 2 * w * i / n, mz + (0.035 if i % 2 else -0.035)) for i in range(n + 1)]
            pecas.append((recorte(faixa(lin, 0.04), den), escuro, 0.06))
        else:
            pecas.append((recorte(ret_arred(mx, mz, w + 0.1, 0.02, 0.0), den), escuro, 0.06))
        for k in range(1, 6):
            x = mx - w + 2 * w * k / 6
            pecas.append((recorte(ret_arred(x, mz, 0.016, h + 0.1, 0.0), den), escuro, 0.06))
        cantos = [(mx - w, mz), (mx + w, mz)]
    elif tipo == "louco":
        # o sorriso grande em D (no louco, cheio de dentes; no feliz, aberto com a língua)
        w, h = 0.36 * larg, 0.30 * abre
        topo = mz + 0.07
        inter = [(mx + w, topo), (mx - w, topo)]
        for i in range(1, 16):
            a = math.radians(180 + 180 * i / 16)
            inter.append((mx + w * math.cos(a), topo + h * math.sin(a)))
        pecas.append((_inchar(inter, 0.05), escuro, 0.04))
        if p.get("dentes", True):
            den = _inchar(inter, -0.025)
            pecas += [(inter, escuro, 0.045), (den, dente, 0.05)]
            pecas.append((recorte(ret_arred(mx, topo - h * 0.40, w + 0.1, 0.022, 0.0), den), escuro, 0.06))
            for k in range(1, 7):
                x = mx - w + 2 * w * k / 7
                pecas.append((recorte(ret_arred(x, topo - h / 2, 0.016, h, 0.0), den), escuro, 0.06))
        else:
            pecas.append((inter, escuro, 0.045))
            ling = recorte(circulo(w * 0.55, 16, mx, topo - h * 0.95, rz=h * 0.5), inter)
            if len(ling) >= 3:
                pecas.append((ling, m_lingua, 0.055))
        cantos = [(mx - w, topo), (mx + w, topo)]
    elif tipo == "mordendo":
        w = 0.26 * larg
        lin = [(mx + w * (-1 + 2 * i / 10), mz + 0.025 * math.sin(i * 1.4) - 0.03 * (-1 + 2 * i / 10) ** 2)
               for i in range(11)]
        pecas.append((faixa(lin, 0.08), escuro, 0.045))
        for s in (-1, 1):
            d = ret_arred(mx + s * 0.058, mz - 0.035, 0.052, 0.065, 0.018)
            pecas += [(_inchar(d, 0.025), escuro, 0.05), (d, dente, 0.055)]
        cantos = [lin[0], lin[-1]]
    else:
        raise ValueError("boca desconhecida: " + tipo)
    if p.get("lingua") == "fora":
        topo_l = mz - 0.03 if tipo != "grito" else mz - 0.1
        pecas += _lingua_fora(mx + p.get("lingua_dx", 0.0), topo_l, p.get("lingua_larg", 1.0), p.get("comp", 1.0),
                              p.get("lingua_giro", 0.0))
    elif p.get("lingua") == "lado":
        # lambendo os lábios: a língua no canto, por cima do lábio de cima
        cx, cz = mx + w * 0.72, mz + 0.06
        ling = _gira(circulo(0.12, 16, cx, cz, rz=0.085), 28, cx, cz)
        pecas += [(_inchar(ling, 0.032), escuro, 0.07), (ling, m_lingua, 0.075)]
    g = p.get("giro", 0.0)
    for pts, m, fr in pecas:
        if pts and len(pts) >= 3:
            decal(_gira(pts, g, mx, mz) if g else pts, m, frente=fr)
    return _gira(cantos, g, mx, mz) if g else cantos


def gota(pos=(0, 0, 0), tam=1.0, cor="azul_claro", escuro="azul_escuro", giro=(0, 0, 0), brilho=0.35, contorno=True):
    """A gota (o suor, a chuva, o sangue): redonda embaixo e pontuda em cima, com o reflexo e o contorno escuro."""
    perf = [(0.0, -1.0), (0.5, -0.87), (0.87, -0.5), (1.0, 0.0), (0.88, 0.45), (0.6, 0.95), (0.3, 1.45), (0.0, 1.95)]
    with grupo(pos=pos, giro=giro, escala=tam):
        _sem_sombra(revolucao(perf, mat(cor, rugo=0.25, brilho=brilho), lados=10))
        _sem_sombra(esfera(0.2, centro=(-0.38, -0.86, 0.15), m=mat("branco", brilho=1.5), sub=1, escala=(1, 0.6, 1.5)))
        if contorno:
            with grupo(pos=(0, 1.1, -0.1), escala=1.24):
                _sem_sombra(revolucao(perf, mat(escuro, rugo=0.6), lados=10))


def rosto(pele="normal", giro=(0, 0, 0), sob=(0.0, 0.0), sob_e=None, sob_d=None, olho=None, olho_e=None, olho_d=None,
          boca=None, cabelo="normal", magro=False, olheiras=0.0, bochechas=None, suor=(), baba=None, lagrimas=0,
          pingos=0):
    """O rosto inteiro. giro = (abaixa, inclina, vira) em graus. sob = (altura, ângulo) das sobrancelhas; olho = dict
    (abre, larg, alto, pupila, olhar, inc, baixo, fechado); boca = dict (tipo, larg, abre, giro, lingua...).
    _e é o lado esquerdo da tela, _d o direito. suor = [(x, z, tamanho, giro)]; baba = (lado, comprimento)."""
    CAB["k"] = 0.40 if magro else 0.20
    tom = _tom(pele)
    m_pele = mat(tom, rugo=0.75, espec=0.3)
    olho = olho or {}
    oe, od = dict(olho, **(olho_e or {})), dict(olho, **(olho_d or {}))
    se, sd = sob_e or sob, sob_d or sob
    with grupo(giro=giro):
        _cabeca(m_pele)
        for s in (-1, 1):   # as orelhas
            esfera(1.0, centro=(s * 0.97, 0.1, 0.0), m=m_pele, sub=1, escala=(0.15, 0.13, 0.25))
            esfera(1.0, centro=(s * 1.0, 0.07, 0.0), m=mat(mistura_t(tom, "olheira", 0.3), rugo=0.8), sub=1,
                   escala=(0.08, 0.07, 0.15))
        esfera(1.0, centro=(0, sup(0, NARIZ_Z) - 0.05, NARIZ_Z), m=mat(mistura_t(tom, "olheira", 0.1), rugo=0.75, espec=0.3),
               sub=1, escala=(0.13, 0.12, 0.15))
        pontas = _cabelo(cabelo)
        if magro:
            # as bochechas fundas: a sombra embaixo das maçãs
            for s in (-1, 1):
                decal(faixa([(s * 0.76, -0.06), (s * 0.64, -0.3), (s * 0.5, -0.66)], [0.1, 0.17, 0.08]),
                      mat(mistura_t(tom, "olheira", 0.6), rugo=0.8), frente=0.025)
        if olheiras:
            for s in (-1, 1):
                decal(arco_faixa(0.38 + 0.04 * olheiras, 0.27, 208, 332, n=12, cx=s * OLHO_X, cz=OLHO_Z + 0.03),
                      mat(mistura_t(tom, "olheira", 0.55 + 0.35 * olheiras), rugo=0.8), frente=0.025)
        if bochechas == "rosadas":
            for s in (-1, 1):
                decal(circulo(0.16, 14, s * 0.62, -0.3, rz=0.095), mat(mistura_t(tom, "vermelho", 0.55), rugo=0.8),
                      frente=0.03)
        _olho(-OLHO_X, OLHO_Z, -1, oe, tom)
        _olho(OLHO_X, OLHO_Z, 1, od, tom)
        m_sob = mat("pupila", rugo=0.8)
        _sobrancelha(-OLHO_X, SOB_Z, -1, se[0], se[1], m_sob, *se[2:])
        _sobrancelha(OLHO_X, SOB_Z, 1, sd[0], sd[1], m_sob, *sd[2:])
        cantos = _boca(boca or {}, tom)
        if bochechas == "cheias":
            for s in (-1, 1):
                esfera(1.0, centro=(s * 0.64, sup(s * 0.64, -0.44) + 0.14, -0.46), m=m_pele, sub=2,
                       escala=(0.42, 0.34, 0.34))
        for x, z, t, g in suor:
            gota((x, min(sup(x, z), -0.15) - 0.32, z), t, giro=(0, g, 0))
        if baba:
            lado, comp = baba
            x0, z0 = cantos[0] if lado < 0 else cantos[1]
            y0 = sup(x0, z0) - 0.09
            # o fio de baba: fino em cima, torto, e a gota pesada na ponta (azulado, com o contorno escuro)
            pts = [(x0, y0, z0 - 0.02), (x0 + lado * 0.05, y0 - 0.02, z0 - comp * 0.35),
                   (x0 - lado * 0.01, y0 - 0.03, z0 - comp * 0.7), (x0 + lado * 0.03, y0 - 0.04, z0 - comp)]
            cor_baba = mistura("azul_claro", "branco", 0.25)
            _sem_sombra(tubo(pts, 0.03, m=mat(cor_baba, rugo=0.2, brilho=0.5), lados=6, r_fim=0.055))
            _sem_sombra(tubo([(x, y + 0.1, z) for x, y, z in pts], 0.055, m=mat("azul_escuro"), lados=6, r_fim=0.08))
            gota((x0 + lado * 0.03, y0 - 0.05, z0 - comp - 0.1), 0.1, cor=cor_baba, giro=(0, 0, 0), brilho=0.5)
        if lagrimas:
            m_lag = mat("azul_claro", rugo=0.2, brilho=0.6)
            for s in ((-1, 1) if lagrimas >= 2 else (1,)):
                cx = s * OLHO_X
                z0 = OLHO_Z - 0.2
                lin = [(cx, z0), (cx + s * 0.03, z0 - 0.2), (cx + s * 0.02, z0 - 0.42), (cx + s * 0.06, z0 - 0.66)]
                decal(faixa(lin, [0.08, 0.1, 0.12, 0.13]), m_lag, frente=0.085)
        escolha = [pontas[i] for i in (1, len(pontas) - 2, 3, len(pontas) - 4)][:pingos] if pingos else []
        for i, (xp, zp) in enumerate(escolha):
            gota((xp, sup(xp, zp) - 0.2, zp - 0.13), 0.06, giro=(0, 0, 0))


# ----------------------------------------------------------------------------------------------------------------------
# a câmera, o que fica fora da cabeça (na tela) e os objetos da causa

CAM = {}


def _camera(cam):
    az, el = math.radians(CAM_AZ), math.radians(CAM_EL)
    d = Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)))
    rot = (-d).to_track_quat("-Z", "Y").to_matrix()
    direita, cima = rot.col[0].copy(), rot.col[1].copy()
    olhar = -direita * (CENTRO[0] / PX) - cima * (CENTRO[1] / PX)
    cam.location = olhar + d * 20
    cam.rotation_euler = rot.to_euler()
    cam.data.ortho_scale = ESCALA
    CAM.update(d=d, direita=direita, cima=cima, olhar=olhar)


def tela(u, v, frente=1.8):
    """O ponto do mundo que cai no pixel (u, v) do quadro (a partir do meio), na frente da cabeça."""
    return CAM["olhar"] + CAM["direita"] * (u / PX) + CAM["cima"] * (v / PX) + CAM["d"] * frente


class na_tela(grupo):
    """Um grupo de frente para a câmera, no pixel (u, v); 1 metro dentro dele vale tam px. X para a direita, Z para
    cima, -Y para a câmera (como no outro script)."""

    def __init__(self, u, v, tam=50.0, giro=(0, 0, 0), frente=1.8):
        base = Matrix((CAM["direita"], -CAM["d"], CAM["cima"])).transposed().to_4x4()
        self.m = Matrix.Translation(tela(u, v, frente)) @ base @ hab._rot(giro) @ hab._esc(tam / PX)


def com_contorno(pts, esp, cor, escuro="carvao", borda=0.07, brilho=0.0, centro=(0, 0, 0)):
    """Um desenho chapado de frente com o contorno escuro atrás (o "!", o Z, o raio)."""
    _sem_sombra(perfil(pts, esp, mat(cor, brilho=brilho, rugo=0.5), centro=centro))
    _sem_sombra(perfil(_inchar(pts, borda), esp * 0.6, mat(escuro, rugo=0.7),
                       centro=(centro[0], centro[1] + esp, centro[2])))


def exclamacao(u=CANTO_ALTO[0] + 4, v=CANTO_ALTO[1] + 4, tam=56):
    with na_tela(u, v, tam, giro=(0, 14, 0)):
        com_contorno([(-0.15, 0.55), (0.15, 0.55), (0.075, -0.12), (-0.075, -0.12)], 0.12, "amarelo", brilho=1.4)
        com_contorno(circulo(0.105, 12, 0, -0.36), 0.12, "amarelo", brilho=1.4)


def zzz(u=CANTO_ALTO[0], v=CANTO_ALTO[1] + 2):
    with na_tela(u + 10, v + 8, 46, giro=(0, -8, 0)):
        com_contorno(forma_z(0.9, 0.9, 0.26), 0.12, "azul_claro", escuro="noite", brilho=1.1)
    with na_tela(u - 22, v - 18, 28, giro=(0, -8, 0)):
        com_contorno(forma_z(0.9, 0.9, 0.3), 0.12, "azul_claro", escuro="noite", borda=0.1, brilho=1.1)


def tremor():
    """As linhas de tremor dos dois lados da cabeça."""
    with na_tela(0, 0, 1.0):
        m = mat("branco", brilho=0.9)
        for s in (-1, 1):
            for r, ab in ((99, 22), (110, 15)):
                de, ate = (-ab, ab) if s > 0 else (180 - ab, 180 + ab)
                _sem_sombra(perfil(arco_faixa(r + 3.5, r - 3.5, de, ate, n=8, cx=CENTRO[0], cz=CENTRO[1] - 6), 3, m))


def chuva(lista, cor="azul_claro"):
    """Gotas de chuva soltas na tela: (u, v, raio em px, giro)."""
    for u, v, r, g in lista:
        with na_tela(u, v, r, giro=(0, g, 0)):
            gota((0, 0, 0), 1.0, cor=cor, brilho=0.5)


def bafo(u, v, tam=26, giro=0):
    """A nuvenzinha de ar (o fôlego, o alívio)."""
    with na_tela(u, v, tam, giro=(0, giro, 0)):
        m = mat("branco_sujo", rugo=0.9, brilho=0.35)
        for x, z, r in ((0, 0, 0.42), (0.42, 0.12, 0.32), (0.72, 0.3, 0.22), (0.2, -0.22, 0.28)):
            _sem_sombra(esfera(r, centro=(x, 0, z), m=m, sub=2))


def bolhas():
    m = mat("azul_claro", rugo=0.2, brilho=0.8)
    br = mat("branco", brilho=1.5)
    for u, v, r in ((60, -12, 8), (78, 20, 12), (74, 62, 17)):
        with na_tela(u, v, 1.0):
            _sem_sombra(toro(r, max(2.6, r * 0.24), centro=(0, 0, 0), giro=(90, 0, 0), m=m, seg=16, lados=6))
            _sem_sombra(esfera(r * 0.22, centro=(-r * 0.35, -3, r * 0.35), m=br, sub=1))


# os objetos da causa (no canto de baixo à direita, salvo dito)

def comprimido():
    with na_tela(*CANTO, tam=54, giro=(18, 42, 0)):
        for cor, s in (("vermelho", 1), ("branco", -1)):
            m = mat(cor, rugo=0.35)
            cilindro(0.2, 0.36, centro=(0, 0, s * 0.18), m=m, lados=12)
            esfera(0.2, centro=(0, 0, s * 0.36), m=m, sub=2)
        caixa((0.4, 0.4, 0.02), centro=(0, 0, 0), m=mat("cinza_claro"))


def cigarro_quebrado():
    with na_tela(CANTO[0] - 2, CANTO[1] + 2, tam=58, giro=(8, 0, 0)):
        papel, filtro, cinza, fumo = mat("branco", rugo=0.6), mat("laranja"), mat("cinza_claro"), mat("madeira")
        with grupo(pos=(-0.2, 0, 0.0), giro=(0, -38, 0)):
            cilindro(0.12, 0.26, centro=(0, 0, 0.28), m=filtro, lados=10)
            cilindro(0.12, 0.34, centro=(0, 0, -0.02), m=papel, lados=10)
            esfera(0.09, centro=(0, 0, -0.2), m=fumo, sub=1)
        with grupo(pos=(0.22, 0, 0.0), giro=(0, 38, 0)):
            cilindro(0.12, 0.44, centro=(0, 0, 0.03), m=papel, lados=10)
            cilindro(0.12, 0.08, centro=(0, 0, 0.29), m=cinza, lados=10)
            esfera(0.09, centro=(0, 0, -0.2), m=fumo, sub=1)
        for x, z in ((-0.02, -0.38), (0.06, -0.44), (0.0, -0.5)):
            caixa((0.06, 0.06, 0.05), centro=(x, 0, z), giro=(0, 30, 0), m=fumo)


def prato_vazio():
    with na_tela(CANTO[0] - 2, CANTO[1], tam=58, giro=(58, 0, 0)):
        m = mat("branco", rugo=0.4)
        revolucao([(0.0, 0.0), (0.3, 0.0), (0.34, 0.03), (0.5, 0.09), (0.52, 0.12), (0.47, 0.12), (0.31, 0.06),
                   (0.0, 0.06)], m, lados=18)
        cilindro(0.29, 0.01, centro=(0, 0, 0.062), m=mat("creme", rugo=0.5), lados=18)
        # o garfo do lado, deitado no prato
        prata = mat("prata", metal=0.6, rugo=0.3)
        with grupo(pos=(0.36, -0.02, 0.16), giro=(0, 0, 25)):
            caixa((0.07, 0.62, 0.04), centro=(0, 0.12, 0), m=prata)
            caixa((0.2, 0.08, 0.04), centro=(0, -0.2, 0), m=prata)
            for x in (-0.07, 0.0, 0.07):
                caixa((0.035, 0.18, 0.04), centro=(x, -0.32, 0), m=prata)


def copo_vazio():
    with na_tela(CANTO[0], CANTO[1] + 2, tam=56, giro=(26, 22, 0)):
        vidro = mat(mistura("azul_claro", "branco", 0.35), rugo=0.15, brilho=0.15)
        cilindro(0.22, 0.72, centro=(0, 0, 0), m=vidro, lados=12, r2=0.29)
        cilindro(0.255, 0.012, centro=(0, 0, 0.36), m=mat("noite"), lados=12)
        toro(0.27, 0.03, centro=(0, 0, 0.365), m=mat("branco", brilho=0.4), seg=12, lados=4)
        cilindro(0.2, 0.05, centro=(0, 0, -0.33), m=mat("branco", brilho=0.3), lados=12)


def gota_sangue(n=1):
    with na_tela(*CANTO, tam=56):
        if n == 1:
            gota((0, 0, -0.08), 0.32, cor="vermelho", escuro="sangue_escuro", brilho=0.25)
        else:
            gota((-0.22, 0, 0.05), 0.24, cor="vermelho", escuro="sangue_escuro", brilho=0.25, giro=(0, -12, 0))
            gota((0.24, 0, 0.18), 0.17, cor="vermelho", escuro="sangue_escuro", brilho=0.25, giro=(0, 10, 0))
            gota((0.2, 0, -0.32), 0.14, cor="vermelho", escuro="sangue_escuro", brilho=0.25)
            _sem_sombra(perfil(circulo(0.32, 16, -0.12, -0.5, rz=0.08), 0.06, mat("vermelho", brilho=0.2)))


def tornozelo_enfaixado():
    with na_tela(CANTO[0] - 4, CANTO[1] + 4, tam=62, giro=(6, 0, -12)):
        pele, pano = mat("pele"), mat("branco", rugo=0.7, brilho=0.15)
        cilindro(0.15, 0.6, centro=(-0.2, 0, 0.22), m=pele, lados=8)
        caixa((0.72, 0.28, 0.22), centro=(0.06, 0, -0.2), m=pele, chanfro=0.07)
        esfera(0.13, centro=(-0.24, 0, -0.2), m=pele, sub=1)
        for z in (-0.02, 0.13):
            toro(0.165, 0.06, centro=(-0.2, 0, z), m=pano, seg=10, lados=5)
        caixa((0.2, 0.32, 0.27), centro=(-0.04, 0, -0.17), giro=(0, 32, 0), m=pano, chanfro=0.03)
        for x, z, g in ((0.2, 0.18, -30), (0.3, 0.05, -60)):
            caixa((0.05, 0.05, 0.16), centro=(x, -0.05, z), giro=(0, g, 0), m=mat("vermelho", brilho=0.8))


def _meio_osso(m, quebra=True):
    """Metade do osso: a cabeça (duas bolas) em z ~ 0.5 e a ponta quebrada em z = 0."""
    cilindro(0.1, 0.46, centro=(0, 0, 0.23), m=m, lados=8)
    for x in (-0.09, 0.09):
        esfera(0.135, centro=(x, 0, 0.5), m=m, sub=2)
    if quebra:
        for x, h in ((-0.055, 0.12), (0.02, 0.2), (0.07, 0.09)):
            cilindro(0.05, h, centro=(x, 0, -h / 2 + 0.01), giro=(180, 0, 0), r2=0.0, m=m, lados=4)


def osso_quebrado():
    m = mat("osso", rugo=0.6)
    with na_tela(CANTO[0] - 2, CANTO[1] + 2, tam=50):
        with grupo(pos=(-0.08, 0, -0.1), giro=(0, 180 + 45, 0)):
            _meio_osso(m)
        with grupo(pos=(0.1, 0, 0.08), giro=(0, 55, 0)):
            _meio_osso(m)


def osso_exposto():
    with na_tela(CANTO[0] - 4, CANTO[1] + 4, tam=54):
        pts = [(x * (1 + 0.18 * math.sin(i * 2.7)), z * (1 + 0.18 * math.sin(i * 2.7)))
               for i, (x, z) in enumerate(estrela(8, 0.42, 0.27, ini=10))]
        _sem_sombra(perfil(pts, 0.08, mat("vermelho", brilho=0.25), centro=(0, 0.1, -0.12)))
        _sem_sombra(perfil(circulo(0.16, 12, 0, -0.12, rz=0.11), 0.08, mat("sangue_escuro"), centro=(0, 0.02, 0)))
        with grupo(pos=(0.02, -0.05, -0.12), giro=(0, 180 + 40, 0)):
            _meio_osso(mat("osso", rugo=0.6))
        caixa((0.12, 0.05, 0.16), centro=(-0.1, -0.12, 0.0), giro=(0, 40, 0), m=mat("vermelho", brilho=0.2))
        gota((-0.4, 0, -0.42), 0.09, cor="vermelho", escuro="sangue_escuro", brilho=0.25)


def curativo_sujo():
    with na_tela(*CANTO, tam=56, giro=(0, 32, 0)):
        _sem_sombra(perfil(ret_arred(0, 0, 0.52, 0.18, 0.17), 0.06, mat(mistura("pele", "creme", 0.5), rugo=0.8)))
        _sem_sombra(perfil(ret_arred(0, 0, 0.18, 0.14, 0.04), 0.06, mat("branco_sujo", rugo=0.9), centro=(0, -0.03, 0)))
        mancha = [(x * (1 + 0.2 * math.sin(i * 1.9)), z * (1 + 0.2 * math.sin(i * 1.9))) for i, (x, z) in
                  enumerate(circulo(0.15, 14, 0.02, 0.0, rz=0.12))]
        _sem_sombra(perfil(mancha, 0.04, mat("verde", rugo=0.4, brilho=0.35), centro=(0, -0.06, 0)))
        _sem_sombra(perfil(circulo(0.05, 8, -0.06, 0.04), 0.04, mat("verde_escuro"), centro=(0, -0.08, 0)))
        gota((0.05, -0.06, -0.24), 0.06, cor="verde", escuro="verde_escuro", giro=(0, 0, 0), brilho=0.4)


def termometro(alto=0.7, estourado=False):
    with na_tela(CANTO[0] + 2, CANTO[1] + 4, tam=58, giro=(0, 34, 0)):
        vidro = mat(mistura("branco", "azul_claro", 0.25), rugo=0.2, brilho=0.12)
        verm = mat("vermelho", brilho=0.5)
        topo = 0.42 if estourado else 0.5
        cilindro(0.1, topo + 0.32, centro=(0, 0, (topo - 0.32) / 2), m=vidro, lados=10)
        if not estourado:
            esfera(0.1, centro=(0, 0, topo), m=vidro, sub=1)
        esfera(0.18, centro=(0, 0, -0.42), m=verm, sub=2)
        col = (topo + 0.32) * (1.0 if estourado else alto)
        cilindro(0.05, col, centro=(0, -0.07, -0.32 + col / 2), m=verm, lados=8)
        for z in (-0.1, 0.05, 0.2, 0.35):
            caixa((0.05, 0.03, 0.025), centro=(0.1, -0.05, z), m=mat("carvao"))
        if estourado:
            for x, h, g in ((-0.06, 0.12, -20), (0.0, 0.18, 5), (0.06, 0.1, 25)):
                cilindro(0.04, h, centro=(x, 0, topo + h / 2), giro=(0, g, 0), r2=0.0, m=vidro, lados=4)
            _sem_sombra(perfil(estrela(8, 0.3, 0.13, ini=8), 0.05, mat("vermelho", brilho=1.2), centro=(0, -0.1, topo + 0.1)))
            _sem_sombra(perfil(estrela(8, 0.17, 0.08, ini=30), 0.05, mat("amarelo", brilho=1.6), centro=(0, -0.14, topo + 0.1)))
            for x, z, r in ((-0.34, 0.36, 0.05), (0.32, 0.32, 0.045), (0.3, 0.62, 0.035)):
                _sem_sombra(esfera(r, centro=(x, -0.1, topo + z - 0.2), m=verm, sub=1))


def muleta():
    """A muleta de alumínio: as duas hastes do apoio do braço, a manopla, a perna só e o pé de borracha."""
    with na_tela(CANTO[0] - 4, CANTO[1] + 4, tam=60, giro=(0, 22, 0)):
        metal, preto, pad = mat("prata", metal=0.5, rugo=0.35, brilho=0.1), mat("preto"), mat("grafite", rugo=0.8)
        for s in (-1, 1):
            tubo([(s * 0.1, 0, 0.52), (s * 0.1, 0, 0.12), (0, 0, -0.2)], 0.05, m=metal, lados=6)
        cilindro(0.05, 0.42, centro=(0, 0, -0.4), m=metal, lados=6)
        cilindro(0.075, 0.1, centro=(0, 0, -0.63), m=preto, lados=8)
        caixa((0.32, 0.16, 0.12), centro=(0, 0, 0.56), m=pad, chanfro=0.04)
        caixa((0.26, 0.1, 0.08), centro=(0, 0, 0.14), m=pad, chanfro=0.02)


def tipoia():
    """O braço dobrado na tipoia: o pano em triângulo, a alça subindo para o pescoço, o antebraço e a mão de fora."""
    with na_tela(CANTO[0] - 2, CANTO[1] + 4, tam=64, giro=(0, -6, 0)):
        pano = mat("branco", rugo=0.8, brilho=0.12)
        perfil([(-0.48, 0.22), (0.3, 0.22), (-0.14, -0.36)], 0.16, pano, centro=(0, 0.06, 0), chanfro=0.03)
        caixa((0.1, 0.08, 0.5), centro=(-0.36, 0.06, 0.44), giro=(0, 28, 0), m=pano)
        caixa((0.1, 0.08, 0.5), centro=(0.18, 0.06, 0.44), giro=(0, -30, 0), m=pano)
        caixa((0.72, 0.2, 0.2), centro=(-0.06, -0.12, 0.2), m=mat("vermelho_escuro"), chanfro=0.04)
        caixa((0.18, 0.2, 0.2), centro=(0.38, -0.12, 0.2), m=mat("pele"), chanfro=0.05)
        for x in (-0.3, -0.1):
            caixa((0.07, 0.24, 0.24), centro=(x, -0.12, 0.2), m=pano)


def nuvem_raio(u=CANTO_ALTO[0] + 2, v=CANTO_ALTO[1] + 2):
    with na_tela(u, v, 52):
        claro, escuro = mat("aco", rugo=0.9), mat("aco_escuro", rugo=0.9)
        for x, z, r, m in ((-0.3, 0.0, 0.22, claro), (-0.06, 0.13, 0.3, claro), (0.24, 0.06, 0.25, claro),
                           (0.04, -0.08, 0.24, escuro), (-0.24, -0.08, 0.18, escuro), (0.38, -0.06, 0.16, escuro)):
            esfera(r, centro=(x, 0.1, z), m=m, sub=2)
        raio = [(0.04, -0.12), (-0.16, -0.46), (-0.03, -0.46), (-0.14, -0.8), (0.18, -0.36), (0.04, -0.36),
                (0.16, -0.12)]
        com_contorno(raio, 0.08, "amarelo", brilho=2.2, borda=0.05, centro=(0, -0.2, 0))


def funil_tornado(u=CANTO_ALTO[0] - 4, v=CANTO_ALTO[1] - 24, tam=62):
    """O funil do tornado: as faixas achatadas, cinza claro e escuro alternados, afinando para baixo e se torcendo
    (cada uma para um lado), a ponta torta encostando no chão, os riscos do vento e os pedaços voando em volta. Atrás,
    a silhueta escura (como o contorno do "!"), que separa o cinza do cabelo."""
    with na_tela(u, v, tam, giro=(0, -16, 0)):
        tons = (mat("cinza_claro", rugo=0.9), mat("azul_aco", rugo=0.9))
        faixas = ((0.0, 0.62, 0.13), (0.11, 0.5, 0.12), (-0.01, 0.38, 0.11), (0.1, 0.27, 0.1), (0.03, 0.18, 0.09),
                  (-0.03, 0.11, 0.08))
        for i, (x, r, h) in enumerate(faixas):
            esfera(1.0, centro=(x, 0.0, 0.42 - 0.2 * i), m=tons[i % 2], sub=2, escala=(r, r * 0.75, h),
                   giro=(10, 0, 12 if i % 2 else -12))
        cilindro(0.08, 0.26, centro=(-0.09, 0.0, -0.74), m=tons[0], lados=6, r2=0.0, giro=(0, 200, 0))
        x0, r0, h0 = faixas[0]
        sil = [(x0 - r0 * 0.75, 0.42 + h0), (x0 + r0 * 0.75, 0.42 + h0)]
        sil += [(x + r, 0.42 - 0.2 * i) for i, (x, r, h) in enumerate(faixas)] + [(-0.12, -0.88)]
        sil += [(x - r, 0.42 - 0.2 * i) for i, (x, r, h) in reversed(list(enumerate(faixas)))]
        _sem_sombra(perfil(_inchar(sil, 0.07), 0.04, mat("carvao", rugo=0.8), centro=(0, 0.6, 0)))
        # os riscos do vento rodando, na frente do funil
        vento = mat("branco", rugo=0.6, brilho=0.5)
        for cx, cz, rf, de, ate in ((0.04, 0.3, 0.74, 200, 280), (0.08, -0.08, 0.5, 260, 340)):
            _sem_sombra(perfil(arco_faixa(rf, rf - 0.06, de, ate, n=8, cx=cx, cz=cz + rf * 0.55), 0.04, vento,
                               centro=(0, -0.55, 0)))
        # os pedaços voando: a tábua, a telha e as lasquinhas
        for x, z, tam_p, g, cor in ((0.58, -0.44, (0.32, 0.07, 0.1), (10, 30, 35), "madeira_clara"),
                                    (0.52, 0.0, (0.18, 0.07, 0.14), (40, -25, -20), "tijolo"),
                                    (0.36, -0.8, (0.13, 0.05, 0.09), (0, 40, 70), "madeira"),
                                    (-0.5, 0.64, (0.1, 0.06, 0.1), (30, 30, 10), "cinza_claro")):
            caixa(tam_p, centro=(x, -0.3, z), giro=g, m=mat(cor, rugo=0.8))


def porta_bunker():
    """O escudo de aço com a porta redonda do abrigo no meio (o lugar seguro)."""
    with na_tela(CANTO[0] - 2, CANTO[1] + 4, tam=58, giro=(0, 0, -16)):
        hab.escudo(tam=0.9, cor_borda="prata", cor_meio="verde")
        roda = mat("amarelo", rugo=0.5, brilho=0.3)
        cilindro(0.2, 0.06, centro=(0, -0.13, 0.04), giro=(90, 0, 0), m=mat("aco", metal=0.5, rugo=0.35), lados=14)
        toro(0.12, 0.03, centro=(0, -0.17, 0.04), giro=(90, 0, 0), m=roda, seg=12, lados=4)
        for g in (0, 60, 120):
            caixa((0.25, 0.03, 0.03), centro=(0, -0.17, 0.04), giro=(0, g, 0), m=roda)


def fogo():
    with na_tela(CANTO[0] + 2, CANTO[1] - 6, tam=60):
        chama((0, 0, -0.42), alt=0.95, larg=0.36)
        for x, g in ((-0.18, 30), (0.16, -35)):
            cilindro(0.06, 0.5, centro=(x, 0.05, -0.44), giro=(0, 90 + g * 0.3, 20), m=mat("madeira"), lados=6)


# ----------------------------------------------------------------------------------------------------------------------
# os estados (uma função por ícone)

def e_abalado():  # a mente abaixo de 70: preocupado, a gota de suor
    rosto(sob=(0.07, 22), olho=dict(pupila=0.85, olhar=(0.3, 0.0)),
          boca=dict(tipo="linha", larg=0.72, curva=-0.13, esp=0.11, dz=-0.05),
          suor=[(0.78, 0.52, 0.16, -12)])


def e_apavorado():  # abaixo de 40: o sorriso louco, os olhos arregalados de pupila pequena, babando
    rosto(sob_e=(0.17, 10), sob_d=(0.06, -14), olho=dict(abre=1.3, pupila=0.4), olho_d=dict(abre=1.38, pupila=0.34),
          boca=dict(tipo="louco", larg=1.08, abre=0.95, giro=-9), baba=(1, 0.42))


def e_colapso():  # abaixo de 15: o olhar perdido, o sorriso torto, a baba, a pele cinza, a cabeça caída de lado
    rosto(pele=("aco", 0.58), giro=(6, 16, 0), sob_e=(0.1, 14), sob_d=(-0.02, 2), olheiras=0.8,
          olho_e=dict(abre=0.82, pupila=1.15, olhar=(-0.7, 0.55), inc=-6),
          olho_d=dict(abre=1.05, pupila=0.45, olhar=(0.55, -0.6)),
          boca=dict(tipo="louco", larg=0.62, abre=0.55, dentes=False, giro=-16, dx=0.04, dz=-0.03),
          baba=(-1, 0.5))


def e_panico():  # a crise de pânico: gritando, os olhos esbugalhados, as lágrimas e o suor
    rosto(sob=(0.17, 24), olho=dict(abre=1.36, pupila=0.38), boca=dict(tipo="grito", larg=1.05, abre=1.08),
          lagrimas=2, suor=[(-0.86, 0.5, 0.13, 18), (0.86, 0.46, 0.15, -18)])


def e_susto():  # o susto: os olhos muito abertos, a boca em O, o cabelo arrepiado e o "!"
    rosto(sob=(0.2, 6), olho=dict(abre=1.3, pupila=0.55), boca=dict(tipo="O", larg=1.0, abre=1.0),
          cabelo="arrepiado")
    exclamacao()


def e_tornado():  # o tornado perto ("Mas que $#&# é essa???"): o queixo caído na boca enorme, os olhos arregalados de
    # pupila pequena, as sobrancelhas lá no alto (uma mais que a outra), o cabelo arrepiado e o funil no canto
    rosto(sob_e=(0.15, 6, 0.12, 1.1), sob_d=(0.21, -2, 0.14, 1.1),
          olho=dict(abre=1.45, larg=0.92, pupila=0.36, olhar=(0.15, 0.2)),
          boca=dict(tipo="aberta", larg=0.82, abre=2.6, dz=-0.08), cabelo="arrepiado")
    funil_tornado()


def e_sonolento():  # o calmante: as pálpebras caídas, o olhar zonzo (vesgo), o sorriso bobo, o comprimido
    rosto(giro=(0, -13, 0), sob_e=(0.06, 4), sob_d=(-0.02, 0), olho=dict(abre=0.5, inc=6, pupila=1.15),
          olho_e=dict(olhar=(0.9, -0.5)), olho_d=dict(olhar=(-0.9, -0.3), abre=0.42),
          boca=dict(tipo="linha", larg=0.8, curva=0.09, onda=0.05, ondas=2.5, torto=-0.05, esp=0.13))
    comprimido()


def e_abstinencia():  # inquieto, mordendo o lábio, o cigarro quebrado
    rosto(sob_e=(0.08, 22), sob_d=(-0.01, 8), olho=dict(abre=1.05, pupila=0.7, olhar=(0.9, -0.15)),
          boca=dict(tipo="mordendo", larg=1.2, giro=-5))
    cigarro_quebrado()


def e_abstinencia_forte():  # tremendo, suado, o cigarro quebrado
    rosto(sob=(0.12, 24), olho=dict(abre=1.22, pupila=0.5), olho_e=dict(olhar=(-0.4, 0.1)),
          olho_d=dict(olhar=(-0.4, 0.1)), boca=dict(tipo="mordendo", larg=1.15, giro=-5),
          suor=[(-0.84, 0.48, 0.14, 15), (0.82, 0.5, 0.15, -15), (0.6, -0.35, 0.11, -10)])
    tremor()
    cigarro_quebrado()


def e_cansado():  # as olheiras, as pálpebras meio fechadas, o bocejo pequeno
    rosto(sob=(0.04, 14), olheiras=1.0, olho=dict(abre=0.4, inc=10),
          boca=dict(tipo="O", larg=1.0, abre=1.35))


def e_exausto():  # os olhos quase fechados, a cabeça pendendo, o "Zz"
    rosto(giro=(16, -10, 0), sob=(-0.03, 10), olheiras=1.0, olho=dict(abre=0.12, inc=8),
          boca=dict(tipo="aberta", larg=0.55, abre=0.7, lingua_baixo=False))
    zzz()


def e_fome():  # lambendo os lábios, o olhar faminto, o prato vazio
    rosto(sob=(0.07, -2), olho=dict(abre=1.08, pupila=0.9, olhar=(0.6, -0.55)),
          boca=dict(tipo="linha", larg=0.8, curva=0.1, lingua="lado"))
    prato_vazio()


def e_faminto():  # o rosto magro e fundo, as olheiras, a boca entreaberta fraca, o prato vazio
    rosto(pele=("branco_sujo", 0.35), magro=True, olheiras=1.0, sob=(0.02, 22), olho=dict(abre=0.5, inc=16),
          boca=dict(tipo="aberta", larg=0.55, abre=0.9, dz=-0.04, lingua_baixo=False))
    prato_vazio()


def e_sede():  # os lábios secos e a língua um pouco para fora, o copo vazio
    rosto(sob=(0.03, 12), olho=dict(abre=0.72, inc=6),
          boca=dict(tipo="aberta", larg=0.8, abre=0.85, seco=0.6, lingua="fora", comp=0.78, lingua_larg=1.1,
                    lingua_baixo=False))
    copo_vazio()


def e_muita_sede():  # os lábios rachados, desesperado, a língua para fora, o suor, o copo vazio
    rosto(sob=(0.13, 26), olho=dict(abre=1.15, pupila=0.6), olheiras=0.4,
          boca=dict(tipo="aberta", larg=0.9, abre=1.0, seco=1.0, lingua="fora", comp=1.2, lingua_baixo=False),
          suor=[(-0.84, 0.48, 0.14, 15), (0.84, 0.46, 0.14, -15)])
    copo_vazio()


def e_sangrando():  # a careta de dor, a gota de sangue grande
    rosto(sob=(0.02, 22), olho=dict(abre=0.62, baixo=0.3, pupila=0.9), boca=dict(tipo="careta", larg=0.95))
    gota_sangue(1)


def e_sangrando_muito():  # pálido, os olhos caídos, várias gotas de sangue
    rosto(pele=PALIDA, sob=(-0.02, 16), olheiras=0.6, olho=dict(abre=0.4, inc=16, pupila=0.9),
          boca=dict(tipo="aberta", larg=0.55, abre=0.7, lingua_baixo=False),
          suor=[(0.8, 0.5, 0.12, -12)])
    gota_sangue(3)


def e_entorse():  # fazendo careta (um olho fechado), o tornozelo enfaixado
    rosto(sob_e=(-0.02, -10), sob_d=(0.06, 18), olho_e=dict(fechado="apertado"), olho_d=dict(abre=0.75, baixo=0.2),
          boca=dict(tipo="careta", larg=0.7, abre=0.8, giro=8))
    tornozelo_enfaixado()


def e_fratura():  # os dentes cerrados de dor, o osso quebrado
    rosto(sob=(0.02, 20), olho=dict(fechado="apertado"), boca=dict(tipo="careta", larg=1.12, abre=1.15),
          suor=[(0.82, 0.5, 0.13, -14)])
    osso_quebrado()


def e_fratura_exposta():  # gritando de dor, o osso saindo com sangue
    rosto(sob=(0.06, 26), olho=dict(fechado="apertado"), boca=dict(tipo="grito", larg=1.0, abre=1.05),
          lagrimas=2, suor=[(-0.84, 0.5, 0.13, 16)])
    osso_exposto()


def e_infeccao():  # um pouco enjoado, a pele esverdeada, o curativo sujo
    rosto(pele=ESVERDEADA, sob=(0.0, 12), olho=dict(abre=0.6, inc=8, olhar=(0.0, -0.3)),
          boca=dict(tipo="linha", larg=0.75, onda=0.055, ondas=3.0, curva=-0.03, esp=0.1, dz=-0.06))
    curativo_sujo()


def e_febre():  # o rosto vermelho, suando, os olhos cansados, o termômetro
    rosto(pele=FEBRE, sob=(0.0, 16), olho=dict(abre=0.5, inc=10), bochechas="rosadas",
          boca=dict(tipo="linha", larg=0.6, curva=-0.06, onda=0.02),
          suor=[(-0.82, 0.5, 0.14, 16), (0.8, 0.52, 0.15, -14)])
    termometro(0.78)


def e_sepse():  # a pele cinza e doente, os olhos revirando, o termômetro estourado
    rosto(pele=CINZA, sob=(-0.04, 12), olheiras=0.9, olho=dict(abre=0.7, olhar=(0.0, 1.6), pupila=1.1),
          boca=dict(tipo="aberta", larg=0.7, abre=0.8, giro=-7, lingua_baixo=False),
          suor=[(0.8, 0.5, 0.12, -12)])
    termometro(estourado=True)


def e_mancando():  # o esforço, a muleta
    rosto(sob=(-0.03, -16), olho_e=dict(abre=0.7, baixo=0.25), olho_d=dict(abre=0.85),
          boca=dict(tipo="careta", larg=0.75, abre=0.8, giro=-6), suor=[(0.8, 0.5, 0.14, -12)])
    muleta()


def e_braco_inutil():  # a dor contida, a tipoia
    rosto(sob_e=(0.0, 24), sob_d=(0.06, 26), olho_e=dict(abre=0.45, baixo=0.3), olho_d=dict(abre=0.72, baixo=0.15),
          boca=dict(tipo="linha", larg=0.62, curva=-0.06, esp=0.1, giro=-9, dz=-0.08),
          suor=[(0.8, 0.5, 0.13, -12)])
    tipoia()


def e_tempestade():  # olhando para cima com medo, a nuvem escura com o raio
    rosto(giro=(-12, 0, 0), sob=(0.14, 22), olho=dict(abre=1.2, pupila=0.7, olhar=(0.55, 0.8)),
          boca=dict(tipo="O", larg=0.8, abre=0.75), suor=[(-0.82, 0.48, 0.13, 15)])
    nuvem_raio()
    chuva([(-92, 52, 7, 0), (-70, -70, 6, 0)])


def e_molhado():  # o cabelo molhado e pingando, umas gotas de chuva
    rosto(cabelo="molhado", sob=(0.02, 14), olho=dict(abre=0.7, inc=10),
          boca=dict(tipo="linha", larg=0.6, curva=-0.08), pingos=2)
    chuva([(-90, 60, 8, 0), (82, 54, 8, 0), (-96, -20, 6, 0), (90, -8, 6, 0)])


def e_encharcado():  # muito molhado, tremendo de frio, o cabelo grudado, muitas gotas, os dentes batendo
    rosto(pele=FRIA, cabelo="grudado", sob=(0.06, 22), olho=dict(abre=0.85, pupila=0.8),
          boca=dict(tipo="careta", larg=0.8, abre=0.85, zig=True), pingos=3)
    tremor()
    chuva([(-80, 76, 8, 0), (-48, 98, 7, 0), (40, 98, 7, 0), (76, 76, 8, 0), (-88, -46, 7, 0), (58, -86, 7, 0),
           (-30, -96, 6, 0), (92, 28, 6, 0)])


def e_bunker():  # aliviado e calmo, a porta blindada do abrigo (positivo)
    rosto(sob=(0.08, 10), olho=dict(fechado="calmo"), boca=dict(tipo="linha", larg=0.7, curva=0.09))
    bafo(-64, -50, 24, giro=160)
    porta_bunker()


def e_perto_do_fogo():  # confortável, quente, as bochechas rosadas, a chama (positivo)
    rosto(sob=(0.06, 0), olho=dict(fechado="feliz"), bochechas="rosadas",
          boca=dict(tipo="louco", larg=0.75, abre=0.8, dentes=False))
    fogo()


def e_sem_folego():  # o vigor baixo: ofegante, a boca aberta, o suor
    rosto(sob=(0.06, 16), olho=dict(abre=0.68, inc=6), boca=dict(tipo="aberta", larg=1.0, abre=1.35),
          suor=[(-0.84, 0.48, 0.14, 16), (0.84, 0.46, 0.14, -16)])
    bafo(-62, -54, 22, giro=170)


def e_sem_vigor():  # o vigor a zero: a língua para fora, muito suor, a cabeça baixa
    rosto(giro=(18, 0, 0), sob=(0.06, 24), olheiras=0.6, olho=dict(abre=0.3, inc=16),
          boca=dict(tipo="aberta", larg=1.05, abre=1.25, lingua="fora", comp=1.15, lingua_baixo=False),
          suor=[(-0.86, 0.52, 0.15, 18), (0.86, 0.5, 0.15, -18), (-0.7, -0.1, 0.11, 10), (0.72, -0.12, 0.11, -10)])
    bafo(-64, -56, 22, giro=170)


def e_afogando():  # embaixo da água, sem fôlego: o rosto azulado, as bochechas cheias, as bolhas subindo
    rosto(pele=AZULADA, sob=(0.15, 22), olho=dict(abre=1.25, pupila=0.5), bochechas="cheias",
          boca=dict(tipo="beico", dz=-0.02))
    bolhas()


# a fauna (12/11/2026, a etapa 4): a picada da cascavel e o cheiro do gambá

def cobra():
    """A cascavel no canto: o corpo em S, a cabeça larga, as presas e a gota de veneno."""
    with na_tela(CANTO[0] - 2, CANTO[1] + 4, tam=56, giro=(0, 18, 0)):
        corpo = mat("oliva", rugo=0.6)
        escuro = mat("oliva_escuro", rugo=0.7)
        pts = [(-0.55, 0, -0.45), (-0.2, 0, -0.55), (0.1, 0, -0.35), (-0.05, 0, -0.1), (0.15, 0, 0.12)]
        tubo(pts, 0.11, m=corpo, lados=8, r_fim=0.13)
        _sem_sombra(tubo([(x, 0.08, z) for x, y, z in pts], 0.15, m=mat("carvao", rugo=0.8), lados=8, r_fim=0.17))
        esfera(1.0, centro=(0.28, 0, 0.22), m=corpo, sub=2, escala=(0.27, 0.15, 0.18))
        for x, z in ((-0.32, -0.5), (0.0, -0.47), (-0.06, -0.2)):
            esfera(0.055, centro=(x, -0.1, z), m=escuro, sub=1)
        esfera(0.04, centro=(0.36, -0.13, 0.3), m=mat("preto"), sub=1)
        for dx in (0.38, 0.47):
            cilindro(0.025, 0.16, centro=(dx, -0.06, 0.07), m=mat("branco", brilho=0.4), lados=6, r2=0.0, giro=(180, 0, 0))
        gota((0.44, -0.12, -0.1), 0.075, cor="verde", escuro="verde_escuro", giro=(0, 0, 0), brilho=0.45)


def fedor(u, v, tam=56, giro=0):
    """O fedor: três linhas verdes ondulando para cima, com o contorno escuro."""
    with na_tela(u, v, tam, giro=(0, giro, 0)):
        m = mat("verde", rugo=0.4, brilho=0.7)
        esc = mat("verde_escuro", rugo=0.6)
        for i, x0 in enumerate((-0.32, 0.04, 0.4)):
            pts = [(x0 + 0.13 * math.sin(k * 1.5 + i * 1.3), 0, -0.55 + k * 0.23) for k in range(6)]
            _sem_sombra(tubo(pts, 0.055, m=m, lados=6))
            _sem_sombra(tubo([(x, 0.06, z) for x, y, z in pts], 0.09, m=esc, lados=6))


def e_envenenado():  # a picada da cascavel: a pele esverdeada, suando, um olho apertado de dor, a careta; a cobra no canto
    rosto(pele=ESVERDEADA, sob_e=(0.04, 22), sob_d=(0.02, 14), olho_e=dict(fechado="apertado"), olho_d=dict(abre=0.55, inc=12),
          boca=dict(tipo="careta", larg=0.85, abre=0.6), suor=[(-0.84, 0.5, 0.14, 16), (0.82, 0.44, 0.13, -14)])
    cobra()


def e_fedendo():  # o cheiro do gambá: os olhos apertados, a careta, os dedos tampando o nariz, o fedor verde subindo
    rosto(sob=(0.04, 18), olho=dict(fechado="apertado"), boca=dict(tipo="careta", larg=0.8, abre=0.7))
    m_mao = mat(_tom("normal"), rugo=0.75, espec=0.3)
    yn = sup(0, NARIZ_Z) - 0.05
    # o polegar e o indicador apertando o nariz, e a mão embaixo
    for s in (-1, 1):
        cilindro(0.06, 0.34, centro=(s * 0.15, yn - 0.1, NARIZ_Z - 0.02), giro=(0, s * 10, 0), m=m_mao, lados=8)
    esfera(1.0, centro=(0.04, yn - 0.2, NARIZ_Z - 0.3), m=m_mao, sub=2, escala=(0.27, 0.15, 0.22))
    fedor(-80, -46, 56, 10)
    fedor(72, -60, 50, -10)


ESTADOS = [
    ("Abalado", "a mente abaixo de 70", e_abalado),
    ("Apavorado", "a mente abaixo de 40", e_apavorado),
    ("Colapso", "a mente abaixo de 15", e_colapso),
    ("Panico", "a crise de pânico", e_panico),
    ("Susto", "o susto", e_susto),
    ("Tornado", "o tornado perto: Mas que $#&# é essa???", e_tornado),
    ("Sonolento", "o calmante", e_sonolento),
    ("Abstinencia", "a falta do cigarro", e_abstinencia),
    ("AbstinenciaForte", "a falta forte do cigarro", e_abstinencia_forte),
    ("Cansado", "o sono", e_cansado),
    ("Exausto", "o sono demais", e_exausto),
    ("Fome", "a fome", e_fome),
    ("Faminto", "a fome demais", e_faminto),
    ("Sede", "a sede", e_sede),
    ("MuitaSede", "a sede demais", e_muita_sede),
    ("Sangrando", "o sangramento", e_sangrando),
    ("SangrandoMuito", "o sangramento forte", e_sangrando_muito),
    ("Entorse", "a entorse", e_entorse),
    ("Fratura", "a fratura", e_fratura),
    ("FraturaExposta", "a fratura exposta", e_fratura_exposta),
    ("Infeccao", "a infecção", e_infeccao),
    ("Febre", "a febre", e_febre),
    ("Sepse", "a sepse", e_sepse),
    ("Mancando", "a perna inutilizada", e_mancando),
    ("BracoInutil", "o braço inutilizado", e_braco_inutil),
    ("Tempestade", "a tempestade", e_tempestade),
    ("Molhado", "molhado de chuva", e_molhado),
    ("Encharcado", "encharcado", e_encharcado),
    ("Bunker", "dentro do abrigo (positivo)", e_bunker),
    ("PertoDoFogo", "perto do fogo, secando e aquecendo (positivo)", e_perto_do_fogo),
    ("SemFolego", "o vigor baixo", e_sem_folego),
    ("SemVigor", "o vigor a zero", e_sem_vigor),
    ("Afogando", "embaixo da água, sem fôlego", e_afogando),
    ("Envenenado", "a picada da cascavel", e_envenenado),
    ("Fedendo", "o cheiro do gambá", e_fedendo),
]


# ----------------------------------------------------------------------------------------------------------------------
# a luz, o render e o disco

def _luzes():
    def sol(nome, cor, forca, giro, angulo=8.0):
        l = bpy.data.lights.new(nome, "SUN")
        l.color = cor
        l.energy = forca
        l.angle = math.radians(angulo)
        o = bpy.data.objects.new(nome, l)
        o.rotation_euler = tuple(math.radians(g) for g in giro)
        COLECAO[0].objects.link(o)
    sol("Principal", (1.0, 0.88, 0.72), 3.5, (48, 0, -30))
    sol("Preenche", (0.62, 0.72, 1.0), 1.9, (75, 0, 60), 20)
    sol("Contorno", (0.78, 0.86, 1.0), 5.0, (-62, 0, 25), 4)
    sol("Contorno2", (0.78, 0.86, 1.0), 2.5, (-70, 0, -40), 4)


def _finalizar(cru, saida):
    import numpy as np
    img = bpy.data.images.load(cru)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    cor, a = px[..., :3], px[..., 3:4]
    ys, xs = np.mgrid[0:h, 0:w]
    r = np.sqrt((xs + 0.5 - w / 2) ** 2 + (ys + 0.5 - h / 2) ** 2)[..., None]
    disco = np.clip((R_DISCO - r) / 1.4 + 0.5, 0, 1)
    t = np.clip(r / R_DISCO, 0, 1) ** 1.6
    fundo = np.array([0.205, 0.212, 0.228]) * (1 - t) + np.array([0.075, 0.078, 0.088]) * t
    # um brilho fraco atrás da cabeça (separa o cabelo escuro do disco) e a sombra embaixo
    halo = _borrar(a[..., 0], 7)[..., None]
    fundo = fundo + np.array([0.85, 0.88, 1.0]) * 0.07 * halo
    sombra = np.roll(_borrar(a[..., 0], 4), -4, axis=0)[..., None]
    fundo = fundo * (1 - 0.35 * sombra)
    out = cor * a + fundo * (1 - a)
    # o fogo e o raio espalham um pouco de luz
    lum = (cor * a).max(axis=2)
    quente = np.clip(lum - 0.92, 0, 1)[..., None] * (cor * a)
    if quente.max() > 0:
        out = out + _borrar(quente, 6) * 1.6
    # o contorno fino claro do disco
    anel = np.clip(1.0 - np.abs(r - (R_DISCO - 1.8)) / 1.2, 0, 1) * 0.8
    out = out * (1 - anel) + np.array([0.80, 0.78, 0.72]) * anel
    out = np.clip(out, 0, 1)
    final = np.concatenate([out, disco], axis=2).astype(np.float32)
    im = bpy.data.images.new("Estado", w, h, alpha=True)
    im.alpha_mode = "STRAIGHT"
    im.pixels[:] = final.ravel()
    im.filepath_raw = saida
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def fazer(estado, pasta):
    ident, funcao = estado[0], estado[2]
    sc = bpy.context.scene
    col = bpy.data.collections.new("Estado_" + ident)
    sc.collection.children.link(col)
    COLECAO[0] = col
    PILHA[:] = [Matrix.Identity(4)]
    CAB["k"] = 0.20
    funcao()
    _luzes()
    cru = os.path.join(pasta, "_cru_%s.png" % ident)
    sc.render.filepath = cru
    bpy.ops.render.render(write_still=True)
    _finalizar(cru, os.path.join(pasta, "Estado_%s.png" % ident))
    os.remove(cru)
    for o in list(col.objects):
        dados = o.data
        bpy.data.objects.remove(o)
        if dados is not None and dados.users == 0:
            if isinstance(dados, bpy.types.Mesh):
                bpy.data.meshes.remove(dados)
            elif isinstance(dados, bpy.types.Light):
                bpy.data.lights.remove(dados)
    bpy.data.collections.remove(col)


# ----------------------------------------------------------------------------------------------------------------------
# as folhas de conferência (com o Pillow, fora do Blender)

def folhas(pasta):
    import random
    from PIL import Image, ImageDraw, ImageFont, ImageFilter

    def fonte(t):
        for f in ("C:/Windows/Fonts/segoeuib.ttf", "C:/Windows/Fonts/arialbd.ttf"):
            if os.path.exists(f):
                return ImageFont.truetype(f, t)
        return ImageFont.load_default()

    def icone(ident, lado):
        arq = os.path.join(pasta, "Estado_%s.png" % ident)
        if not os.path.exists(arq):
            return None
        return Image.open(arq).convert("RGBA").resize((lado, lado), Image.LANCZOS)

    ids = [e[0] for e in ESTADOS]
    por = 8
    linhas = (len(ids) + por - 1) // por
    # os 32 em 128 px, com o Id embaixo
    cel, larg, alt = 128, 150, 128 + 30
    folha = Image.new("RGBA", (por * larg + 20, linhas * alt + 20), (70, 72, 70, 255))
    d = ImageDraw.Draw(folha)
    for i, ident in enumerate(ids):
        x = 10 + (i % por) * larg + (larg - cel) // 2
        y = 10 + (i // por) * alt
        im = icone(ident, cel)
        if im:
            folha.alpha_composite(im, (x, y))
        d.text((x + cel // 2, y + cel + 13), ident, fill=(240, 240, 235), font=fonte(13), anchor="mm")
    folha.convert("RGB").save(os.path.join(pasta, "Folha_Estados.png"))

    # a prova no tamanho do jogo: um fundo escuro de rua à noite, os ícones em 64 e em 40 px
    def fundo_jogo(w, h):
        rnd = random.Random(7)
        f = Image.new("RGB", (w, h), (24, 27, 30))
        dd = ImageDraw.Draw(f)
        for _ in range(60):
            x, y, r = rnd.randrange(w), rnd.randrange(h), rnd.randrange(8, 60)
            c = rnd.choice([(40, 46, 40), (18, 20, 24), (52, 48, 40), (30, 36, 44), (60, 56, 46)])
            dd.ellipse([x - r, y - r, x + r, y + r], fill=c)
        return f.filter(ImageFilter.GaussianBlur(10))

    for lado in (64, 40):
        passo, alt_t = lado + 14, 14
        w = por * passo + 20
        h = linhas * (lado + alt_t + 8) + 20
        prova = fundo_jogo(w, h).convert("RGBA")
        d = ImageDraw.Draw(prova)
        for i, ident in enumerate(ids):
            x = 10 + (i % por) * passo + 7
            y = 10 + (i // por) * (lado + alt_t + 8)
            im = icone(ident, lado)
            if im:
                prova.alpha_composite(im, (x, y))
            d.text((x + lado // 2, y + lado + 7), ident[:12], fill=(190, 190, 185), font=fonte(9), anchor="mm")
        prova = prova.convert("RGB")
        prova.save(os.path.join(pasta, "Prova_%d.png" % lado))
        if lado == 40:
            prova.resize((w * 3, h * 3), Image.NEAREST).save(os.path.join(pasta, "Prova_40_ampliada.png"))


# ----------------------------------------------------------------------------------------------------------------------
# a entrada

def _argumentos():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    pasta, ids, i = None, [], 0
    while i < len(args):
        if args[i] == "--saida":
            pasta = args[i + 1]
            i += 2
            continue
        ids.append(args[i])
        i += 1
    if pasta is None:
        raise SystemExit("Passe a pasta dos PNG: --saida <pasta>")
    os.makedirs(pasta, exist_ok=True)
    return pasta, ids


def principal():
    pasta, ids = _argumentos()
    if not TEM_BLENDER:
        folhas(pasta)
        return
    ids = [i for i in ids if i != "folhas"]
    desconhecidos = [i for i in ids if i not in [e[0] for e in ESTADOS]]
    if desconhecidos:
        print("ESTADO desconhecido:", desconhecidos)
    sc, cam = hab._cena()
    _camera(cam)
    feitos = 0
    for estado in ESTADOS:
        if ids and estado[0] not in ids:
            continue
        try:
            fazer(estado, pasta)
            feitos += 1
            print("ESTADO ok", estado[0])
        except Exception:
            import traceback
            traceback.print_exc()
            print("ESTADO ERRO", estado[0])
            col = COLECAO[0]
            if col is not None and col.name in bpy.data.collections:
                for o in list(col.objects):
                    bpy.data.objects.remove(o)
                bpy.data.collections.remove(col)
    print("ESTADOS feitos:", feitos)


if __name__ == "__main__":
    principal()
