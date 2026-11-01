# Os zumbis especiais (01/11/2026, a pedido): o Brutamonte, o Long (o "Guspidor" da pasta de arte) e o Corredor, cada um
# com as 3 roupas, prontos para o jogo. Os modelos vêm da pasta de arte ("D:\teste blender\O <nome>\...\export\*.fbx",
# feitos por outro chat, em partes soltas e sem esqueleto). Aqui cada um ganha:
#  - o esqueleto do Oco (os mesmos nomes de ossos: hips, spine, spine_02, chest, neck, head, shoulder/upper_arm/forearm/
#    hand, thigh/shin/foot/toe .L/.R; sem os dedos), encaixado nas proporções dele pelas medidas da malha (a virilha, a
#    axila, a linha do braço e da perna, o queixo e o alto da cabeça), medidas do mesmo jeito no Oco: as juntas do Oco
#    passam para o especial pela posição relativa a essas medidas. Assim as animações dos zumbis (Humanoid) servem;
#  - os ossos dele: o olho e as pálpebras do Brutamonte (nas costas) e o papo do Long;
#  - os pesos: os automáticos do Blender (o calor dos ossos) no corpo, as peças rígidas (o olho, as pálpebras, o papo, a
#    boca) presas ao osso delas;
#  - um material só (o jogo divide a malha em corpo, cabeça e carne): as partes já usam o mesmo atlas; a íris do
#    Brutamonte (textura própria) entra numa faixa nova embaixo do atlas (2048 × 2304).
# Sai Assets/_Game/Art/Characters/Zombie/Especiais/<Tipo>_V<n>.fbx com <Tipo>_V<n>_BaseColor/_Normal/_MetalLiso.png.
#
# Rodar (Blender em segundo plano): blender -b --factory-startup --python especiais.py -- tudo
#   ou só um: -- Brutamonte_V1

import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(AQUI))
ARTE = r"D:\teste blender"
OCO = os.path.join(REPO, "Assets", "_Game", "Art", "Characters", "Zombie", "Oco", "Oco_Original.fbx")
SAIDA = os.path.join(REPO, "Assets", "_Game", "Art", "Characters", "Zombie", "Especiais")

# tipo -> (pasta, [(variante, fbx relativo)])
FONTES = {
    "Brutamonte": ("O Brutamonte", [("V1", r"export\Brutamonte.fbx"), ("V2", r"Variantes\V2_Regata\export\Brutamonte_V2.fbx"),
                                    ("V3", r"Variantes\V3_Xadrez\export\Brutamonte_V3.fbx")]),
    "Long": ("O Guspidor", [("V1", r"export\Guspidor.fbx"), ("V2", r"Variantes\V2_Camisa_Aberta\export\Guspidor_V2.fbx"),
                            ("V3", r"Variantes\V3_Regata\export\Guspidor_V3.fbx")]),
    "Corredor": ("O Corredor v2", [("V1", r"export\Corredor.fbx"), ("V2", r"Variantes\V2_Manga_Longa\export\Corredor_V2.fbx"),
                                   ("V3", r"Variantes\V3_Regata\export\Corredor_V3.fbx")]),
}

# as peças presas inteiras a um osso (pelo fim do nome do objeto)
RIGIDAS = {
    "Brutamonte": {"Olho_Globo": "olho", "Palpebra_Sup": "palpebra_sup", "Palpebra_Inf": "palpebra_inf"},
    "Long": {"Papo": "papo", "Boca": "head", "Olhos": "head"},
    "Corredor": {},
}

ESQUELETO = ["hips", "spine", "spine_02", "chest", "neck", "head",
             "shoulder.L", "upper_arm.L", "forearm.L", "hand.L", "thigh.L", "shin.L", "foot.L", "toe.L",
             "shoulder.R", "upper_arm.R", "forearm.R", "hand.R", "thigh.R", "shin.R", "foot.R", "toe.R"]
PAI = {"hips": "root", "spine": "hips", "spine_02": "spine", "chest": "spine_02", "neck": "chest", "head": "neck",
       "shoulder": "chest", "upper_arm": "shoulder", "forearm": "upper_arm", "hand": "forearm",
       "thigh": "hips", "shin": "thigh", "foot": "shin", "toe": "foot"}


def log(*a):
    print("[especiais]", *a, flush=True)


# ================================================================================================ as medidas da malha

def pontos_da_superficie(objetos, n=160000, semente=1):
    """Pontos espalhados na superfície (por área), no mundo: as fatias ficam cheias mesmo nas malhas de poucos polígonos."""
    tris, areas = [], []
    for o in objetos:
        me = o.data
        me.calc_loop_triangles()
        mw = o.matrix_world
        V = np.array([tuple(mw @ v.co) for v in me.vertices])
        for t in me.loop_triangles:
            a, b, c = V[t.vertices[0]], V[t.vertices[1]], V[t.vertices[2]]
            tris.append((a, b, c))
            areas.append(0.5 * np.linalg.norm(np.cross(b - a, c - a)))
    T = np.array(tris)
    A = np.array(areas)
    rng = np.random.default_rng(semente)
    escolha = rng.choice(len(T), size=n, p=A / A.sum())
    r1, r2 = rng.random(n), rng.random(n)
    s = np.sqrt(r1)
    a, b, c = T[escolha, 0], T[escolha, 1], T[escolha, 2]
    return (1 - s)[:, None] * a + (s * (1 - r2))[:, None] * b + (s * r2)[:, None] * c


def grupos_em_x(xs, vao=0.02):
    """As faixas de x separadas por um vão maior que 'vao' (cada uma: min, max, índices)."""
    if len(xs) == 0:
        return []
    ordem = np.argsort(xs)
    x = xs[ordem]
    cortes = np.where(np.diff(x) > vao)[0]
    inicios = np.concatenate([[0], cortes + 1])
    fins = np.concatenate([cortes + 1, [len(x)]])
    return [(x[i], x[j - 1], ordem[i:j]) for i, j in zip(inicios, fins)]


class Medidas:
    """As medidas do corpo (de frente para -Y, a esquerda em +X): a virilha, a axila, as linhas do braço e da perna esquerdos,
    o meio do tronco em y, o queixo e o alto."""

    def __init__(self, P, queixo, passo=0.01):
        self.P = P
        self.topo = P[:, 2].max()
        self.queixo = queixo
        esp = passo * 0.6
        def fatia(z):
            return P[np.abs(P[:, 2] - z) < esp]
        # a virilha: subindo, a primeira altura com corpo no meio (x perto de 0)
        z = 0.25 * self.topo
        while z < 0.7 * self.topo:
            S = fatia(z)
            if len(S) and np.any(np.abs(S[:, 0]) < 0.012):
                break
            z += passo * 0.5
        self.virilha = z
        # a axila: descendo do queixo, a primeira altura em que o braço esquerdo se separa do tronco (e continua separado)
        def separado(z):
            S = fatia(z)
            S = S[S[:, 0] > 0.0]
            return len(grupos_em_x(S[:, 0], 0.018)) >= 2
        z = self.queixo - 0.04
        while z > self.virilha + 0.1:
            if separado(z) and separado(z - 0.03) and separado(z - 0.06):
                break
            z -= passo * 0.5
        self.axila = z
        # a base do pescoço: subindo da axila, a altura em que o tronco (o grupo que passa pelo meio) estreita
        def largura_do_meio(z):
            S = fatia(z)
            gs = grupos_em_x(S[:, 0], 0.018)
            meio = [g for g in gs if g[0] <= 0.0 <= g[1]]
            return (meio[0][1] - meio[0][0]) if meio else 0.0
        ref = largura_do_meio(self.axila + 0.02)
        z = self.axila + 0.02
        while z < self.queixo - 0.01 and largura_do_meio(z) > 0.55 * ref:
            z += passo * 0.5
        self.pescoco = z
        # a linha do braço esquerdo: o grupo mais de fora, descendo da axila até a ponta dos dedos
        braco = []
        ultimo = None
        z = self.axila
        while z > 0.05:
            S = fatia(z)
            S = S[S[:, 0] > 0.0]
            gs = grupos_em_x(S[:, 0], 0.018)
            if len(gs) < 2:
                break
            g = max(gs, key=lambda g: g[1])
            pts = S[g[2]]
            c = pts.mean(0)
            if ultimo is not None and abs(c[0] - ultimo[0]) > 0.06:
                break
            braco.append((z, c[0], c[1]))
            ultimo = c
            z -= passo
        self.braco = np.array(braco)
        self.ponta = self.braco[-1, 0]
        # a linha da perna esquerda: o grupo mais de dentro (x > 0), do chão até a virilha; e a frente do pé
        perna = []
        z = 0.03
        while z < self.virilha - 0.02:
            S = fatia(z)
            S = S[S[:, 0] > 0.004]
            gs = grupos_em_x(S[:, 0], 0.018)
            if gs:
                g = max(gs, key=lambda g: len(g[2]))
                pts = S[g[2]]
                perna.append((z, pts[:, 0].mean(), pts[:, 1].mean(), pts[:, 1].min(), pts[:, 1].max()))
            z += passo
        self.perna = np.array(perna)
        pe = P[(P[:, 2] < 0.12) & (P[:, 0] > 0.0)]
        self.frente_do_pe = pe[:, 1].min()
        # o meio do tronco em y (e a grossura), da virilha ao alto
        tronco = []
        z = self.virilha
        while z < self.topo:
            S = fatia(z)
            S = S[np.abs(S[:, 0]) < 0.05]
            if len(S) > 4:
                tronco.append((z, 0.5 * (S[:, 1].min() + S[:, 1].max()), S[:, 1].max() - S[:, 1].min()))
            z += passo
        self.tronco = np.array(tronco)

    def nos(self):
        return [0.0, self.virilha, self.axila, self.pescoco, self.queixo, self.topo]

    def meio_y(self, z):
        t = self.tronco
        return float(np.interp(z, t[:, 0], t[:, 1]))

    def grossura(self, z):
        t = self.tronco
        return float(np.interp(z, t[:, 0], t[:, 2]))

    def ponto_do_braco(self, s):
        """O ponto da linha do braço a uma fração s do comprimento (0 na axila, 1 na ponta dos dedos); fora, segue a reta
        das pontas."""
        b = self.braco
        pts = np.stack([b[:, 1], b[:, 2], b[:, 0]], 1)
        seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
        acum = np.concatenate([[0], np.cumsum(seg)])
        L = acum[-1]
        alvo = s * L
        if alvo <= 0:
            d = pts[min(5, len(pts) - 1)] - pts[0]
            return pts[0] + d / np.linalg.norm(d) * alvo
        if alvo >= L:
            d = pts[-1] - pts[max(0, len(pts) - 6)]
            return pts[-1] + d / np.linalg.norm(d) * (alvo - L)
        return np.array([np.interp(alvo, acum, pts[:, k]) for k in range(3)])

    def comprimento_do_braco(self):
        b = self.braco
        pts = np.stack([b[:, 1], b[:, 2], b[:, 0]], 1)
        return float(np.linalg.norm(np.diff(pts, axis=0), axis=1).sum())

    def perna_em(self, z):
        p = self.perna
        return float(np.interp(z, p[:, 0], p[:, 1])), float(np.interp(z, p[:, 0], p[:, 2]))


def mapa_z(de, para, z):
    """A altura z no corpo 'de' levada ao corpo 'para' pelas medidas (chão, virilha, axila, queixo, alto)."""
    return float(np.interp(z, de.nos(), para.nos()))


# ================================================================================================ o Oco (a referência)

def importar(caminho):
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=caminho, automatic_bone_orientation=False)
    return [o for o in bpy.data.objects if o not in antes]


def medir_oco():
    novos = importar(OCO)
    arm = next(o for o in novos if o.type == "ARMATURE")
    me = next(o for o in novos if o.type == "MESH")
    # o queixo do Oco: o menor z dos vértices presos sobretudo à cabeça
    g = me.vertex_groups.get("head")
    zs = []
    mw = me.matrix_world
    for v in me.data.vertices:
        pesos = {gg.group: gg.weight for gg in v.groups}
        if g is not None and pesos.get(g.index, 0) > 0.5:
            zs.append((mw @ v.co).z)
    queixo = float(np.percentile(zs, 2))
    M = Medidas(pontos_da_superficie([me]), queixo)
    rep = {b.name: (arm.matrix_world @ b.matrix_local) for b in arm.data.bones}
    caudas = {b.name: (arm.matrix_world @ b.tail_local) for b in arm.data.bones}
    for o in novos:
        bpy.data.objects.remove(o, do_unlink=True)
    log(f"Oco: virilha {M.virilha:.3f} axila {M.axila:.3f} pescoço {M.pescoco:.3f} ponta {M.ponta:.3f} queixo {M.queixo:.3f} topo {M.topo:.3f}")
    return M, rep, caudas


# ================================================================================================ o esqueleto encaixado

def juntas(oco, rep, caudas, M):
    """As cabeças e as caudas dos ossos no especial (só o lado esquerdo e o meio; a direita é o espelho)."""
    J = {}
    s_prof = M.grossura(M.axila - 0.05) / max(0.05, oco.grossura(oco.axila - 0.05))
    def coluna(nome):
        h = rep[nome].translation
        z = mapa_z(oco, M, h.z)
        y = M.meio_y(z) + (h.y - oco.meio_y(h.z)) * s_prof
        return Vector((0.0, y, z))
    for nome in ("hips", "spine", "spine_02", "chest", "neck", "head"):
        J[nome] = [coluna(nome), None]
    cauda_cabeca = caudas["head"]
    J["head"][1] = Vector((0.0, M.meio_y(M.topo - 0.05) + (cauda_cabeca.y - oco.meio_y(oco.topo - 0.05)) * s_prof,
                          M.topo - (oco.topo - cauda_cabeca.z) * (M.topo - M.queixo) / (oco.topo - oco.queixo)))
    # o braço: as juntas do Oco viram frações da linha do braço dele (com o desvio da linha), e voltam na linha deste
    Lo, Lm = oco.comprimento_do_braco(), M.comprimento_do_braco()
    esc = Lm / Lo
    def no_braco(p):
        # a fração da linha do Oco mais perto do ponto
        melhor, sm = 1e9, 0.0
        for s in np.linspace(-0.4, 1.2, 321):
            d = np.linalg.norm(oco.ponto_do_braco(s) - np.array(p))
            if d < melhor:
                melhor, sm = d, s
        desvio = np.array(p) - oco.ponto_do_braco(sm)
        q = M.ponto_do_braco(sm) + desvio * esc
        return Vector(q.tolist())
    for nome in ("forearm.L", "hand.L"):
        J[nome] = [no_braco(rep[nome].translation), None]
    ua = rep["upper_arm.L"].translation
    z_ua = mapa_z(oco, M, ua.z)
    x_ua = ua.x * M.braco[0, 1] / oco.braco[0, 1]
    J["upper_arm.L"] = [Vector((x_ua, M.meio_y(z_ua) + (ua.y - oco.meio_y(ua.z)) * s_prof, z_ua)), None]
    J["hand.L"][1] = Vector(M.ponto_do_braco(1.0).tolist())
    # o ombro (clavícula): do pescoço até o braço
    sh = rep["shoulder.L"].translation
    z = mapa_z(oco, M, sh.z)
    J["shoulder.L"] = [Vector((sh.x * J["upper_arm.L"][0].x / rep["upper_arm.L"].translation.x, M.meio_y(z) + (sh.y - oco.meio_y(sh.z)) * s_prof, z)), None]
    # a perna: a altura pelas medidas, o x e o y pela linha da perna (com o desvio do Oco em relação à linha dele)
    def na_perna(p):
        z = mapa_z(oco, M, p.z)
        zo = min(p.z, oco.virilha - 0.03)
        xo, yo = oco.perna_em(zo)
        xm, ym = M.perna_em(min(z, M.virilha - 0.03))
        larg = (M.perna_em(M.virilha * 0.5)[0]) / max(0.02, oco.perna_em(oco.virilha * 0.5)[0])
        return Vector((xm + (p.x - xo) * larg, ym + (p.y - yo) * larg, z))
    for nome in ("thigh.L", "shin.L", "foot.L"):
        J[nome] = [na_perna(rep[nome].translation), None]
    # o dedão do pé: entre o tornozelo e a frente do pé, como no Oco
    tor_o, toe_o, ponta_o = rep["foot.L"].translation, rep["toe.L"].translation, caudas["toe.L"]
    tor = J["foot.L"][0]
    k_toe = (toe_o.y - tor_o.y) / (oco.frente_do_pe - tor_o.y)
    alt = M.topo / oco.topo
    J["toe.L"] = [Vector((tor.x + (toe_o.x - tor_o.x), tor.y + k_toe * (M.frente_do_pe - tor.y), toe_o.z * alt)),
                  Vector((tor.x + (ponta_o.x - tor_o.x), M.frente_do_pe, ponta_o.z * alt))]
    # as caudas que faltam: até a cabeça do filho
    filho = {"hips": "spine", "spine": "spine_02", "spine_02": "chest", "chest": "neck", "neck": "head",
             "shoulder.L": "upper_arm.L", "upper_arm.L": "forearm.L", "forearm.L": "hand.L",
             "thigh.L": "shin.L", "shin.L": "foot.L", "foot.L": "toe.L"}
    for n, f in filho.items():
        J[n][1] = J[f][0].copy()
    # o espelho
    for n in list(J):
        if n.endswith(".L"):
            h, t = J[n]
            J[n[:-2] + ".R"] = [Vector((-h.x, h.y, h.z)), Vector((-t.x, t.y, t.z))]
    return J


def montar_armadura(nome, J, rep, extras):
    arm_data = bpy.data.armatures.new(nome + "_Rig")
    arm = bpy.data.objects.new(nome + "_Rig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    for o in bpy.context.selected_objects:
        o.select_set(False)
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    raiz = eb.new("root")
    raiz.head = (0, 0, 0)
    raiz.tail = (0, 0.25, 0)
    raiz.use_deform = False
    for n in ESQUELETO:
        b = eb.new(n)
        b.head, b.tail = J[n]
        # o rolo do Oco: o eixo Z dele, girado junto com a direção do osso
        m0 = rep[n].to_3x3()
        y0 = m0.col[1].normalized()
        y1 = (b.tail - b.head).normalized()
        giro = y0.rotation_difference(y1).to_matrix()
        b.align_roll(giro @ m0.col[2])
    for n in ESQUELETO:
        base = n.split(".")[0]
        lado = n[len(base):]
        pai = PAI[base] + (lado if PAI[base] not in ("root", "hips", "chest", "spine", "spine_02", "neck") else "")
        eb[n].parent = eb[pai]
    for (n, h, t, pai, rolo_z) in extras:
        b = eb.new(n)
        b.head, b.tail = Vector(h), Vector(t)
        b.align_roll(Vector(rolo_z))
        b.parent = eb[pai]
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


# ================================================================================================ a textura

FAIXA = 256   # a faixa da íris do Brutamonte, embaixo do atlas (as texturas: texturas.py, no Python comum)


def tamanho_da_imagem(caminho):
    img = bpy.data.images.load(caminho, check_existing=True)
    w, h = img.size
    return int(w), int(h)


# ================================================================================================ um modelo

def limpar_cena():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images):
        for d in list(coll):
            if d.users == 0:
                coll.remove(d)


def fazer(tipo, var, oco, rep, caudas, esqueleto_pronto=None):
    pasta, lista = FONTES[tipo]
    rel = dict(lista)[var]
    fbx = os.path.join(ARTE, pasta, rel)
    nome = f"{tipo}_{var}"
    log("==", nome, fbx)
    limpar_cena()
    novos = importar(fbx)
    malhas = [o for o in novos if o.type == "MESH"]
    # tudo no mundo, sem pais (as peças do olho eram filhas de um vazio)
    for o in malhas:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    bpy.context.view_layer.update()
    for o in malhas:
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(novos):
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    rigidas = {}
    corpo = []
    for o in malhas:
        chave = next((k for k in RIGIDAS[tipo] if o.name.endswith(k) or ("_" + k) in o.name), None)
        if chave:
            rigidas[o] = RIGIDAS[tipo][chave]
        else:
            corpo.append(o)
    # as medidas: o queixo é o fundo da cabeça (a peça Cabeca)
    cab = next(o for o in corpo if "Cabeca" in o.name)
    zs_cab = np.array([(cab.matrix_world @ cab.data.vertices[i].co).z for p in cab.data.polygons for i in p.vertices])
    queixo = float(np.percentile(zs_cab, 2))
    M = esqueleto_pronto[0] if esqueleto_pronto else Medidas(pontos_da_superficie(corpo + list(rigidas)), queixo)
    log(f"{nome}: virilha {M.virilha:.3f} axila {M.axila:.3f} pescoço {M.pescoco:.3f} ponta {M.ponta:.3f} queixo {M.queixo:.3f} topo {M.topo:.3f}")
    J = esqueleto_pronto[1] if esqueleto_pronto else juntas(oco, rep, caudas, M)
    # os ossos dele
    extras = []
    if tipo == "Brutamonte":
        globo = next(o for o in rigidas if "Globo" in o.name)
        c = np.array([tuple(v.co) for v in globo.data.vertices]).mean(0)
        c = Vector(c.tolist())
        extras += [("olho", c, c + Vector((0, 0.12, 0)), "chest", (0, 0, 1)),
                   ("palpebra_sup", c, c + Vector((0, 0.10, 0.06)), "chest", (0, 0, 1)),
                   ("palpebra_inf", c, c + Vector((0, 0.10, -0.06)), "chest", (0, 0, 1))]
    if tipo == "Long":
        papo = next(o for o in rigidas if "Papo" in o.name)
        P = np.array([tuple(v.co) for p in papo.data.polygons for v in [papo.data.vertices[i] for i in p.vertices]])
        c = Vector(P.mean(0).tolist())
        extras += [("papo", c, c + Vector((0, -0.12, 0)), "neck", (0, 0, 1))]
    arm = montar_armadura(nome, J, rep, extras)
    # os UVs (antes de juntar): a íris do Brutamonte vai para a faixa nova embaixo do atlas (texturas.py faz as imagens)
    os.makedirs(SAIDA, exist_ok=True)
    if tipo == "Brutamonte":
        fbm = os.path.splitext(fbx)[0] + ".fbm"
        W, H = tamanho_da_imagem(os.path.join(fbm, "Brutamonte_BaseColor.png"))
        NH = H + FAIXA
        for o in malhas:
            uv = o.data.uv_layers.active.data
            eh_globo = "Globo" in o.name
            for l in uv:
                u, v = l.uv
                l.uv = (u * FAIXA / W, v * FAIXA / NH) if eh_globo else (u, (FAIXA + v * H) / NH)
    # um material só
    mat = bpy.data.materials.new(nome + "_Mat")
    for o in malhas:
        o.data.materials.clear()
        o.data.materials.append(mat)
    # o corpo: junta as peças e põe os pesos automáticos
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in corpo:
        o.select_set(True)
    bpy.context.view_layer.objects.active = corpo[0]
    bpy.ops.object.join()
    corpo_ob = bpy.context.view_layer.objects.active
    corpo_ob.name = nome + "_Jogo"
    for b in arm.data.bones:
        b.use_deform = b.name in ESQUELETO
    # (as placas soltas do Brutamonte só se fundem ao corpo com voxels maiores)
    voxel = 0.03 if tipo == "Brutamonte" else 0.016
    for tentativa in range(4):
        if pesos_pela_casca(corpo_ob, arm, voxel * (1.0, 1.2, 0.85, 1.45)[tentativa]):
            break
    # o que ainda ficou sem peso: o osso mais perto
    corrigir_sem_peso(corpo_ob, arm)
    # as peças rígidas: peso 1 no osso delas
    for o, osso in rigidas.items():
        g = o.vertex_groups.new(name=osso)
        g.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    for b in arm.data.bones:
        b.use_deform = b.name != "root"
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in [corpo_ob] + list(rigidas):
        o.select_set(True)
    bpy.context.view_layer.objects.active = corpo_ob
    bpy.ops.object.join()
    corpo_ob = bpy.context.view_layer.objects.active
    corpo_ob.name = nome + "_Jogo"
    for m in list(corpo_ob.modifiers):
        if m.type == "ARMATURE":
            corpo_ob.modifiers.remove(m)
    mod = corpo_ob.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    corpo_ob.parent = arm
    corpo_ob.matrix_parent_inverse = arm.matrix_world.inverted()
    limitar_pesos(corpo_ob)
    exportar(nome, arm, corpo_ob)
    return M, J, arm, corpo_ob


def pesos_pela_casca(ob, arm, voxel=0.016):
    """
    Os pesos automáticos (o calor dos ossos) numa casca fechada do corpo: as peças soltas e cruzadas do modelo fazem o
    calor falhar, então ele é calculado numa cópia refeita em voxels (uma malha só, fechada) e passado para a malha de
    verdade pela face mais perto (interpolado).
    """
    for o in bpy.context.selected_objects:
        o.select_set(False)
    casca = ob.copy()
    casca.data = ob.data.copy()
    casca.name = ob.name + "_Casca"
    bpy.context.scene.collection.objects.link(casca)
    for g in list(casca.vertex_groups):
        casca.vertex_groups.remove(g)
    r = casca.modifiers.new("Voxel", "REMESH")
    r.mode = "VOXEL"
    r.voxel_size = voxel
    r.adaptivity = 0.0
    bpy.context.view_layer.objects.active = casca
    casca.select_set(True)
    bpy.ops.object.modifier_apply(modifier=r.name)
    if len(casca.data.polygons) > 60000:
        d = casca.modifiers.new("Menos", "DECIMATE")
        d.ratio = 60000 / len(casca.data.polygons)
        bpy.ops.object.modifier_apply(modifier=d.name)
    log(f"  casca: {len(casca.data.vertices)} vértices")
    # o calor dos ossos falha em tudo nesta escala (2 m): a conta é feita 10 vezes maior, numa cópia da armadura
    arm_t = arm.copy()
    arm_t.data = arm.data.copy()
    bpy.context.scene.collection.objects.link(arm_t)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in (casca, arm_t):
        o.scale = (10.0, 10.0, 10.0)
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm_t
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    casca.select_set(True)
    arm_t.select_set(True)
    bpy.context.view_layer.objects.active = arm_t
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    vazios = sum(1 for v in casca.data.vertices if not any(g.weight > 1e-4 for g in v.groups))
    log(f"  casca sem peso: {vazios}")
    if vazios > 0.3 * len(casca.data.vertices):
        # o calor falhou nesta casca: outro tamanho de voxel
        bpy.data.objects.remove(arm_t, do_unlink=True)
        bpy.data.objects.remove(casca, do_unlink=True)
        return False
    # de volta ao tamanho de verdade
    casca.parent = None
    for m in list(casca.modifiers):
        casca.modifiers.remove(m)
    casca.matrix_world = Matrix.Diagonal((0.1, 0.1, 0.1, 1.0))
    for o in bpy.context.selected_objects:
        o.select_set(False)
    casca.select_set(True)
    bpy.context.view_layer.objects.active = casca
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.data.objects.remove(arm_t, do_unlink=True)
    corrigir_sem_peso(casca, arm)
    # da casca para o modelo
    for g in list(ob.vertex_groups):
        ob.vertex_groups.remove(g)
    for g in casca.vertex_groups:
        ob.vertex_groups.new(name=g.name)
    t = ob.modifiers.new("Pesos", "DATA_TRANSFER")
    t.object = casca
    t.use_vert_data = True
    t.data_types_verts = {"VGROUP_WEIGHTS"}
    t.vert_mapping = "POLYINTERP_NEAREST"
    t.layers_vgroup_select_src = "ALL"
    t.layers_vgroup_select_dst = "NAME"
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=t.name)
    for m in list(ob.modifiers):
        if m.type == "ARMATURE":
            ob.modifiers.remove(m)
    bpy.data.objects.remove(casca, do_unlink=True)
    return True


def corrigir_sem_peso(ob, arm):
    """Os vértices sem nenhum peso (o calor dos ossos falha em peças soltas) vão para o osso mais perto."""
    ossos = [b for b in arm.data.bones if b.use_deform]
    segs = [((arm.matrix_world @ b.head_local), (arm.matrix_world @ b.tail_local)) for b in ossos]
    grupos = {g.name: g for g in ob.vertex_groups}
    sem = 0
    for v in ob.data.vertices:
        if any(g.weight > 1e-4 for g in v.groups):
            continue
        p = ob.matrix_world @ v.co
        melhor, nome = 1e9, None
        for b, (h, t) in zip(ossos, segs):
            d = t - h
            k = max(0.0, min(1.0, (p - h).dot(d) / max(1e-9, d.length_squared)))
            dist = (p - (h + d * k)).length
            if dist < melhor:
                melhor, nome = dist, b.name
        g = grupos.get(nome) or ob.vertex_groups.new(name=nome)
        grupos[nome] = g
        g.add([v.index], 1.0, "REPLACE")
        sem += 1
    log(f"  {sem} vértices sem peso foram para o osso mais perto")


def limitar_pesos(ob, maximo=4):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=maximo)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)


def exportar(nome, arm, ob):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    arm.select_set(True)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = arm
    caminho = os.path.join(SAIDA, nome + ".fbx")
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False, bake_anim=False,
                             mesh_smooth_type="FACE", use_mesh_modifiers=False, path_mode="STRIP")
    log("  gravado", caminho)


def principal(nomes):
    oco, rep, caudas = medir_oco()
    feitos = {}
    for tipo, (_, lista) in FONTES.items():
        pronto = None
        for var, _ in lista:
            nome = f"{tipo}_{var}"
            if nomes and nome not in nomes and tipo not in nomes and "tudo" not in nomes:
                continue
            # as roupas mudam, o corpo não: o esqueleto da V1 serve às outras (as animações batem iguais)
            M, J, _, _ = fazer(tipo, var, oco, rep, caudas, pronto)
            if pronto is None:
                pronto = (M, J)
            feitos[nome] = True
    log("feitos:", ", ".join(feitos))


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    principal(args)
