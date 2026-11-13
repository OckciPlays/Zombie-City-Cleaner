# A fauna: a família de cervos low-poly (o macho, a fêmea e o filhote), no Blender em segundo plano.
#
#   bash D:/Zombiegame/Trabalho-Claude/armas_fogo2/blender_um.sh --python Blender/Fauna/familia.py -- [tudo|modelos]
#
# Grava (fora do repositório, em D:\Zombiegame\Trabalho-Claude\fauna):
#   Familia_Cervos.blend          os três animais, cada um na sua coleção (a malha, os materiais e o esqueleto), e a
#                                 coleção "Apresentacao" (as câmeras, as luzes e o chão)
#   fbx\Cervo_<Animal>.fbx        cada animal sozinho, na origem, pronto para a Unity
#   apresentacao\*.png            as vistas (frente, lado, trás e 3/4), a família, a comparação de tamanho, o aramado e
#                                 o teste do esqueleto
#
# Como é feito: o corpo sai do campo das peças (formas.py: a união suave, sem emendas de esferas e cilindros), é
# reduzido (as faces planas e grandes do low-poly), as faces quase planas viram quadrados e cada face recebe o material
# da região dela (cervo.py) e um tom um pouco diferente das vizinhas (o atributo de cor "Variacao", que o material
# multiplica). As orelhas, os olhos, os cascos e a galhada são malhas baixas à parte, na mesma malha. Os pesos dos ossos
# saem das peças (cada peça sabe os ossos dela).

import math
import os
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Vector, Quaternion, Euler

AQUI = os.path.dirname(os.path.abspath(__file__))
if AQUI not in sys.path:
    sys.path.insert(0, AQUI)

import formas  # noqa: E402
import malhas as cs  # noqa: E402
import cervo  # noqa: E402

TRABALHO = r"D:\Zombiegame\Trabalho-Claude\fauna"
APRESENTACAO = os.path.join(TRABALHO, "apresentacao")
FBX = os.path.join(TRABALHO, "fbx")
NOMES = {"macho": "Cervo_Macho", "femea": "Cervo_Femea", "filhote": "Cervo_Filhote"}
REGIAO = {"corpo": 0, "olho": 1, "orelha": 2, "casco": 3, "galhada": 4}


def linear(c):
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)


# ---------------------------------------------------------------------------------------------------------------------
# Os materiais
# ---------------------------------------------------------------------------------------------------------------------

def materiais():
    """Os materiais da família (os mesmos para os três): a cor da região vezes o tom de cada face (o atributo
    "Variacao"). Foscos: a aspereza alta e o brilho baixo (nada de plástico); os olhos brilham um pouco."""
    out = []
    for nome, cor, aspereza in cervo.MATERIAIS:
        m = bpy.data.materials.get(nome) or bpy.data.materials.new(nome)
        m.use_nodes = True
        nt = m.node_tree
        for n in list(nt.nodes):
            if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL"):
                nt.nodes.remove(n)
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        lin = linear(cor)
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.label = "Cor"
        rgb.outputs[0].default_value = (*lin, 1.0)
        at = nt.nodes.new("ShaderNodeAttribute")
        at.attribute_name = "Variacao"
        at.attribute_type = "GEOMETRY"
        mul = nt.nodes.new("ShaderNodeVectorMath")
        mul.operation = "MULTIPLY"
        nt.links.new(rgb.outputs[0], mul.inputs[0])
        nt.links.new(at.outputs["Color"], mul.inputs[1])
        nt.links.new(mul.outputs[0], bsdf.inputs["Base Color"])
        rgb.location, at.location, mul.location = (-520, 120), (-520, -80), (-260, 40)
        bsdf.inputs["Roughness"].default_value = aspereza
        for nome_in, val in (("Specular IOR Level", 0.5 if "Olhos" in nome else 0.18), ("Metallic", 0.0)):
            if nome_in in bsdf.inputs:
                bsdf.inputs[nome_in].default_value = val
        m.diffuse_color = (*lin, 1.0)
        m.roughness = aspereza
        out.append(m)
    return out


# ---------------------------------------------------------------------------------------------------------------------
# As peças à parte
# ---------------------------------------------------------------------------------------------------------------------

def malha_casco_fendido(c):
    """O casco grande de dois dedos, como na referência: cada dedo é arredondado na frente, do talão (atrás, alto) ao
    bico (na frente, no chão), com o vão no meio; e os dois esporões pequenos atrás do boleto."""
    talao, ponta, w = c["talao"], c["ponta"], c["larg"]
    frente = ponta - talao
    frente[2] = 0
    comp = np.linalg.norm(frente)
    frente /= comp
    lado = np.cross(frente, (0, 0, 1))
    h0 = talao[2]
    verts, quads, tris = [], [], []
    perfil = ((-1.0, 0.0), (-0.95, 0.55), (-0.6, 0.92), (0.0, 1.0), (0.6, 0.92), (0.95, 0.55), (1.0, 0.0))
    np_ = len(perfil)
    for dedo in (-1, 1):
        o = lado * dedo * (w * 0.56)
        b = len(verts)
        base = talao.copy()
        base[2] = 0.0
        aneis = ((0.0, 0.50, 1.00, 0.0), (0.30, 0.56, 0.86, 0.04), (0.62, 0.55, 0.58, 0.10), (0.86, 0.42, 0.30, 0.16))
        for t, hw, h, abre in aneis:
            c0 = base + frente * comp * t + o * (1 + abre)
            for a, z in perfil:
                verts.append(c0 + lado * (a * hw * w) + np.array((0, 0, z * h * h0)))
        verts.append(base + frente * comp + o * 1.22 + np.array((0, 0, 0.004)))
        ip = len(verts) - 1
        na = len(aneis)
        for i in range(na - 1):
            for k in range(np_ - 1):
                a0 = b + i * np_ + k
                quads.append((a0, a0 + 1, a0 + 1 + np_, a0 + np_))
            quads.append((b + i * np_ + np_ - 1, b + i * np_, b + (i + 1) * np_, b + (i + 1) * np_ + np_ - 1))
        ul = b + (na - 1) * np_
        for k in range(np_ - 1):
            tris.append((ul + k, ul + k + 1, ip))
        tris.append((ul + np_ - 1, ul, ip))
        for k in range(1, np_ - 2):                         # o talão (a tampa de trás)
            tris.append((b, b + k + 1, b + k))
    # os esporões (as unhas de trás do boleto), pequenos e escuros
    for dedo in (-1, 1):
        b0 = c["boleto"] + lado * dedo * (w * 0.55)
        b = len(verts)
        r_ = w * 0.42
        for k in range(4):
            a = 2 * math.pi * k / 4
            verts.append(b0 + lado * (math.cos(a) * r_) + np.array((0, 0, math.sin(a) * r_)))
        verts.append(b0 - frente * (w * 1.3) - np.array((0, 0, w * 0.9)))
        for k in range(4):
            tris.append((b + k, b + (k + 1) % 4, b + 4))
        quads.append((b + 3, b + 2, b + 1, b + 0))
    return np.array(verts), np.array(quads).reshape(-1, 4), np.array(tris).reshape(-1, 3)


def malha_galhada_angulosa(lado):
    """A galhada grossa e facetada: a haste e as pontas em tubos de 6 lados (os segmentos angulosos); o u vai de 0 na
    base a 1 nas pontas (a cor clareia para as pontas)."""
    pts = formas.interpolar(np.asarray(lado["haste"]), 3)
    seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    total = s[-1]
    verts, quads, tris, us = [], [], [], []

    def juntar(v, q, t, u):
        o = len(verts)
        verts.extend(v)
        us.extend(list(u))
        quads.extend([tuple(int(i) + o for i in a) for a in q])
        tris.extend([tuple(int(i) + o for i in a) for a in t])

    aneis = []
    for u in np.linspace(0, 1, 9):
        p = np.array([np.interp(u * total, s, pts[:, e]) for e in range(3)])
        r = lado["r0"] + (lado["r1"] - lado["r0"]) * u
        if u == 0:
            r = lado["roseta"]
        aneis.append(formas.Anel(p, r, r))
    v, q, t = formas.tubo(aneis, lados=6, sub=1, tampas=True, lateral=(0, 0, 1), tampa_ext=0.9, ntampa=1)
    d = np.linalg.norm(np.asarray(v)[:, None, :] - pts[None, :, :], axis=2)
    juntar(v, q, t, 0.15 + 0.75 * s[np.argmin(d, 1)] / total)
    for t_, d_, comp in lado["pontas"]:
        p0 = np.array([np.interp(t_ * total, s, pts[:, e]) for e in range(3)])
        rb = (lado["r0"] + (lado["r1"] - lado["r0"]) * t_) * 0.95
        curva = [p0 - d_ * rb * 0.5, p0 + d_ * comp * 0.35, p0 + d_ * comp * 0.70 + np.array((0, -0.010, 0)) * comp / 0.2,
                 p0 + d_ * comp + np.array((0, -0.024, 0)) * comp / 0.2]
        aneis = [formas.Anel(curva[0], rb, rb), formas.Anel(curva[1], rb * 0.78, rb * 0.78),
                 formas.Anel(curva[2], rb * 0.50, rb * 0.50), formas.Anel(curva[3], 0.004, 0.004)]
        v, q, t = formas.tubo(aneis, lados=6, sub=1, tampas=True, lateral=(1, 0, 0), tampa_ext=0.5, ntampa=1)
        ao_longo = np.clip(((np.asarray(v) - p0) @ d_) / comp, 0, 1)
        juntar(v, q, t, 0.3 + 0.7 * ao_longo)
    return (np.array(verts), np.array(quads).reshape(-1, 4), np.array(tris).reshape(-1, 3)), dict(u=np.array(us))


def malha_olho_amendoado(o, lados=10, faixas=6):
    """O olho amendoado: um elipsóide achatado ao longo da cabeça, com o canto de dentro mais baixo."""
    longo, cima, fora = o["eixos"]
    ra, rb, rc = o["raios"]
    verts = [o["c"] - cima * rb]
    for i in range(1, faixas):
        a = -math.pi / 2 + math.pi * i / faixas
        for j in range(lados):
            b = 2 * math.pi * j / lados
            # a ponta dos cantos (o formato de amêndoa): o longo encolhe menos perto do meio
            x = math.cos(a) * math.cos(b)
            y = math.sin(a)
            z = math.cos(a) * math.sin(b)
            x *= 1.0 + 0.25 * (1 - abs(y))
            verts.append(o["c"] + longo * (x * ra) + cima * (y * rb) + fora * (z * rc))
    verts.append(o["c"] + cima * rb)
    quads, tris = [], []
    for j in range(lados):
        tris.append((0, 1 + (j + 1) % lados, 1 + j))
    for i in range(faixas - 2):
        for j in range(lados):
            a = 1 + i * lados + j
            b = 1 + i * lados + (j + 1) % lados
            quads.append((a, b, b + lados, a + lados))
    topo = len(verts) - 1
    base = 1 + (faixas - 2) * lados
    for j in range(lados):
        tris.append((topo, base + j, base + (j + 1) % lados))
    return np.array(verts), np.array(quads).reshape(-1, 4), np.array(tris).reshape(-1, 3)


def malha_orelha_baixa(o):
    """A orelha grande em concha (8 lados, 6 anéis): o lado de dentro (w > 0) fica para fora e para a frente; o v vai
    de -1 a 1 de borda a borda (a borda escura) e o u da base à ponta."""
    D = o["dir"]
    frente = np.array((0.38 * o["lado"], -1.0, 0.10))
    N = frente - D * np.dot(frente, D)
    N /= np.linalg.norm(N)
    W = np.cross(D, N)
    perfil = [(0.0, 0.40), (0.16, 0.82), (0.38, 1.0), (0.60, 0.86), (0.80, 0.52), (0.93, 0.20)]
    esp = 0.012 * o["comp"] / 0.165
    concha = 0.46
    lados = 8
    verts, quads, tris, ws, vs, us = [], [], [], [], [], []
    for t, w in perfil:
        hw = 0.5 * o["larg"] * w
        c = o["base"] + D * o["comp"] * t
        for k in range(lados):
            a = 2 * math.pi * (k + 0.5) / lados
            x = math.cos(a) * hw
            y = math.sin(a) * esp * 0.5 - concha * hw * (1 - (x / max(hw, 1e-6)) ** 2)
            verts.append(c + W * x + N * y)
            ws.append(1.0 if math.sin(a) > 0.1 else -1.0)
            vs.append(math.cos(a))
            us.append(t)
    n = len(perfil)
    for i in range(n - 1):
        for k in range(lados):
            a = i * lados + k
            b = i * lados + (k + 1) % lados
            quads.append((a, b, b + lados, a + lados))
    ip = len(verts)
    verts.append(o["base"] + D * o["comp"] - N * 0.004)
    ws.append(0.0)
    vs.append(0.0)
    us.append(1.0)
    base = (n - 1) * lados
    for k in range(lados):
        tris.append((base + k, base + (k + 1) % lados, ip))
    ib = len(verts)
    verts.append(o["base"] - D * 0.008)
    ws.append(0.0)
    vs.append(0.0)
    us.append(0.0)
    for k in range(lados):
        tris.append((ib, (k + 1) % lados, k))
    return ((np.array(verts), np.array(quads).reshape(-1, 4), np.array(tris).reshape(-1, 3)),
            dict(w=np.array(ws), v=np.array(vs), u=np.array(us)))


def pecas_a_parte(esp):
    """As malhas baixas: (malha, osso, região, atributos u/v/w)."""
    out = []
    for o in esp.olhos():
        out.append((malha_olho_amendoado(o), o["osso"], "olho", {}))
    for o in esp.orelhas():
        m, a = malha_orelha_baixa(o)
        out.append((m, o["osso"], "orelha", a))
    for c in esp.cascos():
        out.append((malha_casco_fendido(c), c["osso"], "casco", {}))
    for lado in esp.galhada():
        m, a = malha_galhada_angulosa(lado)
        out.append((m, "Cabeca", "galhada", a))
    return out


# ---------------------------------------------------------------------------------------------------------------------
# A malha
# ---------------------------------------------------------------------------------------------------------------------

def arrumar_faces(ob):
    """O acabamento do low-poly: os triângulos mais bem formados e os pares quase planos juntados em quadrados."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.beautify_fill(bm, faces=bm.faces[:], edges=bm.edges[:])
    bmesh.ops.join_triangles(bm, faces=bm.faces[:], angle_face_threshold=math.radians(14),
                             angle_shape_threshold=math.radians(38))
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()


def colar_manchas(me, esp, manchas, raio):
    """As manchas do filhote: pentágonos pequenos colados na pele (2 mm para fora da face), no ponto de cada mancha
    (um raio do meio do tronco para o lado acha a face). Cada um pesa nos ossos como a pele embaixo dele."""
    from mathutils.bvhtree import BVHTree
    V = [Vector(v.co) for v in me.vertices]
    F = [list(p.vertices) for p in me.polygons]
    arvore = BVHTree.FromPolygons(V, F)
    bm = bmesh.new()
    bm.from_mesh(me)
    mancha = list(me.materials).index(bpy.data.materials["Cervo_Manchas_Filhote"])
    for f, h, sx, k in manchas:
        c = Vector(esp.ponto_do_tronco(f, h))
        loc, nor, idx, dist = arvore.ray_cast(c, Vector((sx, 0, 0.12 * (h - 0.6))))
        if loc is None:
            continue
        t1 = nor.cross(Vector((0, 0, 1)))
        if t1.length < 1e-4:
            t1 = Vector((0, 1, 0))
        t1.normalize()
        t2 = nor.cross(t1).normalized()
        meio = loc + nor * 0.002
        r = raio * k
        giro = (f * 37.0 + h * 11.0) % (2 * math.pi)
        vs = [bm.verts.new(meio + t1 * (math.cos(giro + a) * r * 1.25) + t2 * (math.sin(giro + a) * r))
              for a in np.linspace(0, 2 * math.pi, 5, endpoint=False)]
        face = bm.faces.new(vs if (vs[1].co - vs[0].co).cross(vs[2].co - vs[0].co).dot(nor) > 0 else vs[::-1])
        face.material_index = mancha
    bm.to_mesh(me)
    bm.free()
    me.update()


def malha_do_animal(var):
    """A malha do animal: o corpo reduzido e as peças à parte, os materiais de cada face, a variação de tom e os pesos.
    Devolve (esp, pecas, objeto da malha, osso fixo de cada vértice)."""
    esp = cervo.Cervo(var)
    pecas, campo, pontos, quads, tris = cs.corpo(esp)
    alto = cs.objeto_da_malha("alto", pontos, quads, tris)
    cs.reduzir(alto, esp.v["tris"])
    arrumar_faces(alto)
    me = alto.data
    pv = np.zeros(len(me.vertices) * 3)
    me.vertices.foreach_get("co", pv)
    V = [pv.reshape(-1, 3)]
    faces = cs.para_fora(V[0], [list(p.vertices) for p in me.polygons])
    bpy.data.objects.remove(alto, do_unlink=True)
    bpy.data.meshes.remove(me)
    regiao = [0] * len(faces)
    n0 = len(V[0])
    atr = {k: [np.zeros(n0)] for k in "uvw"}
    fixo = [""] * n0
    total = n0
    for (v, q, t), osso, reg, a in pecas_a_parte(esp):
        v = np.asarray(v, float)
        V.append(v)
        for f in cs.para_fora(v, list(q) + list(t)):
            faces.append([int(i) + total for i in f])
            regiao.append(REGIAO[reg])
        for k in "uvw":
            atr[k].append(np.asarray(a.get(k, np.zeros(len(v))), float))
        fixo += [osso] * len(v)
        total += len(v)
    V = np.concatenate(V)
    atr = {k: np.concatenate(x) for k, x in atr.items()}
    regiao = np.array(regiao)
    nome = NOMES[var]
    me = bpy.data.meshes.new(nome)
    me.from_pydata([tuple(map(float, x)) for x in V], [], faces)
    me.update()
    ob = bpy.data.objects.new(nome, me)

    # os centros e as normais das faces; a peça do corpo mais perto de cada face
    nfc = len(me.polygons)
    cen = np.zeros(nfc * 3)
    nor = np.zeros(nfc * 3)
    me.polygons.foreach_get("center", cen)
    me.polygons.foreach_get("normal", nor)
    cen, nor = cen.reshape(-1, 3), nor.reshape(-1, 3)
    corpo = regiao == 0
    dist = np.full((nfc, len(pecas)), 9.0)
    for i, p in enumerate(pecas):
        dist[corpo, i] = p["campo"].amostrar(cen[corpo])
    parte = np.array([p["nome"] for p in pecas])[np.argmin(dist, 1)]
    parte[~corpo] = ""
    vf = [list(p.vertices) for p in me.polygons]
    ext = {k: np.array([x[f].mean() for f in vf]) for k, x in atr.items()}
    mat, h, fc, sc = esp.materiais_das_faces(cen, nor, parte, regiao, ext)
    todos = materiais()
    usados = sorted(set(int(i) for i in mat) | ({cervo.M["Cervo_Manchas_Filhote"]} if esp.v["manchas"] else set()))
    novo = {i: k for k, i in enumerate(usados)}
    for i in usados:
        me.materials.append(todos[i])
    me.polygons.foreach_set("material_index", np.array([novo[int(i)] for i in mat], np.int32))
    manchas = esp.manchas_do_filhote()
    if manchas:
        n_antes, f_antes = len(me.vertices), len(me.polygons)
        colar_manchas(me, esp, manchas, 0.0088 * esp.v["A"] / 0.58)
        fixo += [""] * (len(me.vertices) - n_antes)
        regiao = np.concatenate([regiao, np.full(len(me.polygons) - f_antes, 5)])
        nfc = len(me.polygons)
        cen = np.zeros(nfc * 3)
        me.polygons.foreach_get("center", cen)
        cen = cen.reshape(-1, 3)
    for p in me.polygons:
        p.use_smooth = False

    # o tom de cada face (a variação que destaca as faces do low-poly)
    rng = np.random.default_rng({"macho": 3, "femea": 5, "filhote": 9}[var])
    A = esp.v["A"]
    base = rng.uniform(0.80, 1.15, nfc) * (0.86 + 0.26 * formas.fbm(cen / A, 7.0, 2, 33))
    tom = np.stack([base * (1 + 0.035 * rng.uniform(-1, 1, nfc)), base * (1 + 0.025 * rng.uniform(-1, 1, nfc)),
                    base * (1 + 0.03 * rng.uniform(-1, 1, nfc))], 1)
    tom[regiao == 1] = 1.0                                  # os olhos sem variação
    tom[regiao == 3] = np.clip(tom[regiao == 3], 0.9, 1.1)
    gal = np.nonzero(regiao == 4)[0]                        # a galhada: mais escura na base, clara nas pontas
    if len(gal):
        ug = ext["u"][gal] if len(ext["u"]) > gal.max() else np.full(len(gal), 0.5)
        tom[gal] *= (0.66 + 0.40 * ug)[:, None]
    cor = me.color_attributes.new("Variacao", "FLOAT_COLOR", "CORNER")
    canto = np.zeros((len(me.loops), 4), np.float32)
    canto[:, 3] = 1.0
    for p in me.polygons:
        canto[p.loop_start:p.loop_start + p.loop_total, :3] = tom[p.index]
    cor.data.foreach_set("color", canto.ravel())
    me.color_attributes.active_color = cor
    return esp, pecas, ob, fixo


# ---------------------------------------------------------------------------------------------------------------------
# O esqueleto e os pesos
# ---------------------------------------------------------------------------------------------------------------------

def esqueleto(esp, nome, colecao):
    arm = bpy.data.armatures.new(nome + "_Esqueleto")
    ob = bpy.data.objects.new(nome + "_Esqueleto", arm)
    colecao.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.view_layer.objects:
        o.select_set(o == ob)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = {}
    for n, a, b, pai in esp.ossos():
        e = arm.edit_bones.new(n)
        e.head, e.tail = Vector(a), Vector(b)
        d = (Vector(b) - Vector(a)).normalized()
        e.align_roll(Vector((0, -1, 0)) if abs(d.z) > 0.7 else Vector((0, 0, 1)))
        if pai:
            e.parent = eb[pai]
        eb[n] = e
    for n, e in eb.items():   # liga as juntas que se encostam (a cadeia das pernas, a coluna, o pescoço)
        if e.parent is not None and (e.head - e.parent.tail).length < 1e-4:
            e.use_connect = True
    bpy.ops.object.mode_set(mode="OBJECT")
    arm.bones["Raiz"].use_deform = False
    arm.display_type = "STICK"
    ob.show_in_front = True
    return ob


def pesos_da_cadeia(P, segs):
    """O peso de cada osso de uma peça pela posição ao longo da cadeia (as juntas misturam os dois ossos)."""
    if len(segs) == 1:
        return np.ones((len(P), 1))
    a = np.array([s[1] for s in segs], float)
    b = np.array([s[2] for s in segs], float)
    L = np.linalg.norm(b - a, axis=1)
    S = np.concatenate([[0], np.cumsum(L)])
    best = np.full(len(P), 1e9)
    s = np.zeros(len(P))
    for i in range(len(segs)):
        d = b[i] - a[i]
        t = np.clip(((P - a[i]) @ d) / max(d @ d, 1e-12), 0, 1)
        dist = np.linalg.norm(P - (a[i] + t[:, None] * d), axis=1)
        m = dist < best
        best[m] = dist[m]
        s[m] = S[i] + t[m] * L[i]
    w = np.ones((len(P), len(segs)))
    for i in range(len(segs)):
        if i > 0:
            B = 0.32 * min(L[i - 1], L[i])
            w[:, i] *= formas.suave(S[i] - B, S[i] + B, s)
        if i < len(segs) - 1:
            B = 0.32 * min(L[i], L[i + 1])
            w[:, i] *= 1 - formas.suave(S[i + 1] - B, S[i + 1] + B, s)
    return w / np.maximum(w.sum(1, keepdims=True), 1e-9)


def pesar(esp, pecas, ob, fixo, rig):
    me = ob.data
    V = np.zeros(len(me.vertices) * 3)
    me.vertices.foreach_get("co", V)
    V = V.reshape(-1, 3)
    nomes = [n for n, _, _, _ in esp.ossos() if n != "Raiz"]
    ib = {n: i for i, n in enumerate(nomes)}
    Wt = np.zeros((len(V), len(nomes)))
    corpo = np.array([f == "" for f in fixo])
    P = V[corpo]
    d = np.stack([p["campo"].amostrar(P) for p in pecas], 1)
    tau = 0.012 * esp.v["A"]
    wp = np.exp(-(d - d.min(1, keepdims=True)) / tau)
    wp /= wp.sum(1, keepdims=True)
    for k, p in enumerate(pecas):
        wb = pesos_da_cadeia(P, p["ossos"])
        for j, (osso, _, _) in enumerate(p["ossos"]):
            Wt[corpo, ib[osso]] += wp[:, k] * wb[:, j]
    for i, f in enumerate(fixo):
        if f:
            Wt[i, ib[f]] = 1.0
    # os 4 maiores de cada vértice
    ordem = np.argsort(-Wt, 1)
    corte = np.zeros_like(Wt, bool)
    np.put_along_axis(corte, ordem[:, :4], True, 1)
    Wt = np.where(corte, Wt, 0)
    Wt[Wt < 0.01] = 0
    Wt /= np.maximum(Wt.sum(1, keepdims=True), 1e-9)
    grupos = {n: ob.vertex_groups.new(name=n) for n in nomes}
    for j, n in enumerate(nomes):
        idx = np.nonzero(Wt[:, j])[0]
        for i in idx:
            grupos[n].add([int(i)], float(Wt[i, j]), "REPLACE")
    ob.parent = rig
    m = ob.modifiers.new("Esqueleto", "ARMATURE")
    m.object = rig


# ---------------------------------------------------------------------------------------------------------------------
# A apresentação
# ---------------------------------------------------------------------------------------------------------------------

def cena_de_apresentacao(col):
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    sc.view_settings.look = "None"
    if sc.world is None:
        sc.world = bpy.data.worlds.new("Fundo")
    sc.world.use_nodes = True
    bg = next(n for n in sc.world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (*linear((0.115, 0.12, 0.13)), 1.0)
    bg.inputs[1].default_value = 1.0
    sc.render.film_transparent = False
    # o chão neutro (recebe as sombras)
    me = bpy.data.meshes.new("Chao")
    me.from_pydata([(-150, -150, 0), (150, -150, 0), (150, 150, 0), (-150, 150, 0)], [], [(0, 1, 2, 3)])
    chao = bpy.data.objects.new("Chao", me)
    col.objects.link(chao)
    mc = bpy.data.materials.new("Apresentacao_Chao")
    mc.use_nodes = True
    nt = mc.node_tree
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*linear((0.20, 0.205, 0.215)), 1.0)
    b.inputs["Roughness"].default_value = 1.0
    tc = nt.nodes.new("ShaderNodeTexCoord")
    dist = nt.nodes.new("ShaderNodeVectorMath")
    dist.operation = "LENGTH"
    faixa = nt.nodes.new("ShaderNodeMapRange")
    faixa.inputs["From Min"].default_value = 3.0
    faixa.inputs["From Max"].default_value = 9.0
    faixa.inputs["To Min"].default_value = 1.0
    faixa.inputs["To Max"].default_value = 0.0
    nt.links.new(tc.outputs["Object"], dist.inputs[0])
    nt.links.new(dist.outputs["Value"], faixa.inputs["Value"])
    nt.links.new(faixa.outputs["Result"], b.inputs["Alpha"])
    me.materials.append(mc)
    for nome, energia, loc, alvo, tam in (("Luz_Principal", 900.0, (-3.5, -4.5, 5.0), (0, 0, 0.6), 4.0),
                                          ("Luz_Preenchimento", 160.0, (4.5, -3.0, 2.0), (0, 0, 0.7), 6.0),
                                          ("Luz_Contraluz", 520.0, (1.0, 5.0, 3.5), (0, 0, 0.8), 3.0)):
        l = bpy.data.lights.new(nome, "AREA")
        l.energy = energia
        l.size = tam
        o = bpy.data.objects.new(nome, l)
        col.objects.link(o)
        o.location = loc
        o.rotation_euler = (Vector(alvo) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()


def camera(col, nome, pos, alvo, lente=50, orto=None):
    c = bpy.data.cameras.new(nome)
    if orto:
        c.type = "ORTHO"
        c.ortho_scale = orto
    else:
        c.lens = lente
    o = bpy.data.objects.new(nome, c)
    col.objects.link(o)
    o.location = pos
    o.rotation_euler = (Vector(alvo) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
    return o


def fotografar(cam, nome, l=1600, a=1000):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = l, a
    sc.render.resolution_percentage = 100
    sc.camera = cam
    sc.render.filepath = os.path.join(APRESENTACAO, nome + ".png")
    sc.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)


def mostrar_so(animais, quais):
    for v, (rig, ob) in animais.items():
        vis = v in quais
        rig.hide_render = True
        ob.hide_render = not vis


def colocar(rig, x, y, giro):
    rig.location = (x, y, 0)
    rig.rotation_euler = (0, 0, math.radians(giro))


def pose(rig, rot):
    """Gira os ossos (graus, em X/Y/Z do osso) para o teste do esqueleto."""
    for pb in rig.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
    for n, (x, y, z) in rot.items():
        if n in rig.pose.bones:
            rig.pose.bones[n].rotation_euler = (math.radians(x), math.radians(y), math.radians(z))
    bpy.context.view_layer.update()


def aramado(obs, col):
    """Cópias com o modificador de arame por cima (para ver a malha nas fotos)."""
    m = bpy.data.materials.get("Apresentacao_Arame") or bpy.data.materials.new("Apresentacao_Arame")
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (0.02, 0.02, 0.02, 1)
    b.inputs["Roughness"].default_value = 1.0
    copias = []
    for ob in obs:
        d = ob.copy()
        d.data = ob.data.copy()
        d.name = ob.name + "_Arame"
        col.objects.link(d)
        d.data.materials.clear()
        d.data.materials.append(m)
        w = d.modifiers.new("Arame", "WIREFRAME")
        w.thickness = 0.0022 * (1.0 if "Filhote" not in ob.name else 0.7)
        w.use_replace = True
        w.offset = 1.0
        copias.append(d)
    return copias


# ---------------------------------------------------------------------------------------------------------------------
# A montagem
# ---------------------------------------------------------------------------------------------------------------------

def montar_tudo(fotos=True):
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.unit_settings.scale_length = 1.0
    os.makedirs(APRESENTACAO, exist_ok=True)
    os.makedirs(FBX, exist_ok=True)
    animais = {}
    linha = {"macho": -1.5, "femea": 0.0, "filhote": 1.3}
    for var in ("macho", "femea", "filhote"):
        nome = NOMES[var]
        col = bpy.data.collections.new(nome)
        sc.collection.children.link(col)
        esp, pecas, ob, fixo = malha_do_animal(var)
        col.objects.link(ob)
        rig = esqueleto(esp, nome, col)
        pesar(esp, pecas, ob, fixo, rig)
        colocar(rig, linha[var], 0, 0)
        animais[var] = (rig, ob)
        tri = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        print("fauna: %s: %d faces, %d triangulos, %d vertices, %d ossos" % (
            nome, len(ob.data.polygons), tri, len(ob.data.vertices), len(rig.data.bones)))
    ap = bpy.data.collections.new("Apresentacao")
    sc.collection.children.link(ap)
    cena_de_apresentacao(ap)
    # cada animal sozinho num FBX, na origem
    for var, (rig, ob) in animais.items():
        loc = rig.location.copy()
        rig.location = (0, 0, 0)
        bpy.context.view_layer.update()
        for o in bpy.context.view_layer.objects:
            o.select_set(o in (rig, ob))
        bpy.context.view_layer.objects.active = rig
        bpy.ops.export_scene.fbx(filepath=os.path.join(FBX, NOMES[var] + ".fbx"), use_selection=True,
                                 object_types={"ARMATURE", "MESH"}, apply_scale_options="FBX_SCALE_ALL",
                                 axis_forward="-Z", axis_up="Y", add_leaf_bones=False, primary_bone_axis="Y",
                                 secondary_bone_axis="X", mesh_smooth_type="FACE", colors_type="SRGB",
                                 use_armature_deform_only=True, bake_anim=False, path_mode="AUTO")
        rig.location = loc
    print("fauna: os modelos em %.1f s" % (time.time() - t0))
    if fotos:
        apresentar(animais, ap)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(TRABALHO, "Familia_Cervos.blend"))
    print("fauna: pronto em %.1f s" % (time.time() - t0))


def apresentar(animais, ap):
    # as vistas de cada animal, sozinho na origem
    linha = {v: (rig.location.copy(), rig.rotation_euler.copy()) for v, (rig, ob) in animais.items()}
    for var, (rig, ob) in animais.items():
        mostrar_so(animais, [var])
        colocar(rig, 0, 0, 0)
        A = cervo.VARIANTES[var]["A"]
        bpy.context.view_layer.update()
        cx = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
        zmax = max(c.z for c in cx)
        ys = [c.y for c in cx]
        comp = max(ys) - min(ys)
        alvo = (0, (max(ys) + min(ys)) / 2, zmax * 0.5)
        dist = max(zmax * 0.62 / math.tan(math.radians(15.1)), comp * 0.62 / math.tan(math.radians(19.8))) * 1.08
        vistas = {"frente": (0, -dist, zmax * 0.55), "lado": (-dist, alvo[1], zmax * 0.55),
                  "tras": (0, dist, zmax * 0.62), "tres_quartos": (-dist * 0.62, -dist * 0.78, zmax * 0.80)}
        for nome, pos in vistas.items():
            cam = camera(ap, "Camera_%s_%s" % (NOMES[var], nome), pos, alvo, lente=50)
            fotografar(cam, "%s_%s" % (NOMES[var], nome), 1200, 900)
        colocar(rig, *linha[var][0][:2], 0)
    # a família (como a referência: o macho à esquerda olhando para a direita, a fêmea à direita, o filhote no meio)
    mostrar_so(animais, list(animais))
    colocar(animais["macho"][0], -1.25, 0.25, 62)
    colocar(animais["femea"][0], 1.35, 0.25, -60)
    colocar(animais["filhote"][0], 0.15, -0.35, 28)
    fotografar(camera(ap, "Camera_Familia", (0, -5.6, 1.45), (0.05, 0, 0.62), lente=42), "Familia", 1800, 1000)
    # o aramado (a topologia)
    copias = aramado([ob for rig, ob in animais.values()], ap)
    fotografar(camera(ap, "Camera_Aramado", (-2.2, -4.6, 1.5), (0.05, 0, 0.62), lente=42), "Familia_Aramado", 1800, 1000)
    for d in copias:
        bpy.data.objects.remove(d, do_unlink=True)
    # a comparação de tamanho: de lado, lado a lado, na mesma escala (ortográfica)
    colocar(animais["macho"][0], -1.35, 0, 0)
    colocar(animais["femea"][0], 0.15, 0, 0)
    colocar(animais["filhote"][0], 1.35, 0, 0)
    for v, (rig, ob) in animais.items():
        rig.rotation_euler = (0, 0, math.radians(90))   # de lado para a câmera da frente: a cabeça para a direita
    fotografar(camera(ap, "Camera_Comparacao", (0, -8, 0.85), (0, 0, 0.85), orto=4.4), "Comparacao_Tamanho", 1800, 1000)
    # o teste do esqueleto: o macho comendo, a fêmea alerta (orelhas e cabeça) e o filhote no meio de um passo
    colocar(animais["macho"][0], -1.25, 0.25, 62)
    colocar(animais["femea"][0], 1.35, 0.25, -60)
    colocar(animais["filhote"][0], 0.15, -0.35, 28)
    pose(animais["macho"][0], {"Pescoco_1": (40, 0, 0), "Pescoco_2": (28, 0, 0), "Cabeca": (18, 0, 0),
                                 "Braco.L": (12, 0, 0), "Antebraco.L": (-8, 0, 0), "Orelha.L": (0, 0, -15)})
    pose(animais["femea"][0], {"Pescoco_1": (-10, 0, 0), "Pescoco_2": (-6, 0, 18), "Cabeca": (-8, 0, 12),
                                "Orelha.L": (0, 0, 30), "Orelha.R": (0, 0, -30), "Cauda_1": (35, 0, 0)})
    pose(animais["filhote"][0], {"Braco.L": (-25, 0, 0), "Antebraco.L": (50, 0, 0), "Canela.D.L": (40, 0, 0),
                                  "Coxa.R": (20, 0, 0), "Perna.R": (-30, 0, 0), "Canela.T.R": (25, 0, 0),
                                  "Cabeca": (10, 0, -10)})
    fotografar(bpy.data.objects["Camera_Familia"], "Teste_Esqueleto", 1800, 1000)
    for v, (rig, ob) in animais.items():
        pose(rig, {})
        rig.location, rig.rotation_euler = linha[v]
    mostrar_so(animais, list(animais))


# ---------------------------------------------------------------------------------------------------------------------
# A versão para a Unity (um material só: a cor de cada face num ponto de uma textura pequena)
# ---------------------------------------------------------------------------------------------------------------------

UNITY = os.path.join(os.path.dirname(os.path.dirname(AQUI)), "Assets", "_Game", "Art", "Fauna")


def srgb(x):
    return 1.055 * (x ** (1 / 2.4)) - 0.055 if x > 0.0031308 else 12.92 * x


def exportar_para_unity(var, rig, ob, tam=128):
    """O FBX da Unity: a mesma malha e o mesmo esqueleto, com um material só e a textura de cores (cada face num ponto,
    a cor do material vezes o tom da face). Na Unity, a textura sem filtro e sem mipmap (Editor/MontagemDaFauna.cs)."""
    os.makedirs(UNITY, exist_ok=True)
    nome = NOMES[var]
    me = ob.data.copy()
    copia = ob.copy()
    copia.data = me
    copia.name = nome
    ob.name = nome + "_Blender"
    bpy.context.scene.collection.objects.link(copia)
    cores = {n: linear(c) for n, c, _ in cervo.MATERIAIS}
    var_cor = me.color_attributes.get("Variacao")
    nf = len(me.polygons)
    assert nf <= tam * tam, "faces demais para a textura"
    px = np.zeros((tam * tam, 4), np.float32)
    px[:, 3] = 1.0
    uv = me.uv_layers.new(name="Cor")
    for f in me.polygons:
        base = cores.get(me.materials[f.material_index].name, (0.5, 0.5, 0.5))
        tom = var_cor.data[f.loop_start].color if var_cor is not None else (1, 1, 1, 1)
        px[f.index, :3] = [srgb(min(1.0, base[k] * tom[k])) for k in range(3)]
        u, v = ((f.index % tam) + 0.5) / tam, ((f.index // tam) + 0.5) / tam
        for li in range(f.loop_start, f.loop_start + f.loop_total):
            uv.data[li].uv = (u, v)
    for outra in [l for l in me.uv_layers if l.name != "Cor"]:
        me.uv_layers.remove(outra)
    me.uv_layers.active = me.uv_layers["Cor"]
    im = bpy.data.images.new(nome + "_Cor", tam, tam, alpha=False)
    im.pixels.foreach_set(px.ravel())
    im.filepath_raw = os.path.join(UNITY, nome + "_Cor.png")
    im.file_format = "PNG"
    im.save()
    mat = bpy.data.materials.new(nome)
    me.materials.clear()
    me.materials.append(mat)
    for f in me.polygons:
        f.material_index = 0
    if var_cor is not None:
        me.color_attributes.remove(var_cor)
    loc = rig.location.copy()
    rig.location = (0, 0, 0)
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        o.select_set(o in (rig, copia))
    bpy.context.view_layer.objects.active = rig
    caminho = os.path.join(UNITY, nome + ".fbx")
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", mesh_smooth_type="FACE", colors_type="NONE",
                             use_armature_deform_only=True, bake_anim=False, path_mode="AUTO", embed_textures=False)
    rig.location = loc
    bpy.data.objects.remove(copia, do_unlink=True)
    bpy.data.meshes.remove(me)
    bpy.data.images.remove(im)
    ob.name = nome
    print("fauna: unity", caminho, nf, "faces")


def unity():
    """Só a versão da Unity, do .blend salvo."""
    bpy.ops.wm.open_mainfile(filepath=os.path.join(TRABALHO, "Familia_Cervos.blend"))
    for var, nome in NOMES.items():
        exportar_para_unity(var, bpy.data.objects[nome + "_Esqueleto"], bpy.data.objects[nome])


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if args and args[0] == "unity":
        unity()
        return
    montar_tudo(fotos=not (args and args[0] == "modelos"))


if __name__ == "__main__":
    main()
