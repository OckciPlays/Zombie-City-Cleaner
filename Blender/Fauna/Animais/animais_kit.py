# Os animais do Tripo (Animais 3d/<Nome>/<Nome>.fbx): o kit comum.
#
# Rode no Python do Blender (de preferência num Blender sem janela), no mesmo dicionário dos outros scripts:
#   ns = {}; exec(open(r"...\Modelo 3D\Animais\animais_kit.py", encoding="utf-8").read(), ns)
#
# O que tem aqui:
#   - importar(): o FBX do Tripo na orientação e no tamanho do jogo (olhando para -Y, o alto em +Z, em metros, o pivô
#     no chão entre as patas; o lado L em +X), a malha soldada e a textura copiada para Texturas/;
#   - as medidas da malha (as patas achadas pelo chão e subidas pela seção, os cortes, o centro de uma seção);
#   - o esqueleto (a lista de ossos, com o roll dos cervos do Pedro: os ossos em pé com o Z para a frente, os deitados
#     com o Z para cima) e os pesos (os automáticos do Blender, com a conferência);
#   - o motor das poses (Pose: o giro de cada osso no espaço do bicho, relativo ao pai, a partir do repouso; o FK feito
#     aqui, sem o Blender) e o IK das patas (as duas de cima por IK de dois ossos até o tornozelo, a canela e o pé pela
#     fase do passo);
#   - a gravação das ações (uma chave por quadro, lineares, uma faixa de NLA por ação) e as fotos de conferência.
#
# A convenção dos ossos é a dos cervos do Pedro (Docs/20-Fauna-Modelos-e-Animacoes.md, do develop): Quadril, Pescoco_1,
# Pescoco_2, Cabeca, Braco/Antebraco, Coxa/Perna, Cauda_1/2, Orelha.L/R (e Asa/Asa_2 nas aves).

import bpy
import bmesh
import math
import os
import shutil

import numpy as np
from mathutils import Vector, Matrix, Quaternion, Euler

RAIZ = r"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner"
PASTA_FBX = os.path.join(RAIZ, "Animais 3d")
AQUI = os.path.join(RAIZ, "Modelo 3D", "Animais")
TEXTURAS = os.path.join(AQUI, "Texturas")
RENDERS = os.path.join(AQUI, "Renders")
FPS = 24
X = Vector((1, 0, 0))
Y = Vector((0, 1, 0))
Z = Vector((0, 0, 1))


def rad(g):
    return math.radians(g)


def qx(g):
    """O giro de g graus em volta do X do bicho (o eixo do lado esquerdo): + abaixa o focinho e leva a ponta de um osso
    que aponta para baixo para trás."""
    return Quaternion(X, rad(g))


def qy(g):
    """Em volta do Y (o eixo de trás): + rola o lado esquerdo (+X) para cima."""
    return Quaternion(Y, rad(g))


def qz(g):
    """Em volta do Z (o alto): + vira o focinho (-Y) para a direita do bicho (-X)... veja: -Y girado +90 vira +X."""
    return Quaternion(Z, rad(g))


def q(x=0.0, y=0.0, z=0.0):
    """Os três giros juntos (primeiro o Z, depois o Y, depois o X), em graus."""
    return qx(x) @ qy(y) @ qz(z)


def suave(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def mix(a, b, t):
    return a + (b - a) * t


def slerp(a, b, t):
    if a.dot(b) < 0:
        b = -b
    return a.slerp(b, t)


# ---------------------------------------------------------------------------------------------------------------------
# A importação
# ---------------------------------------------------------------------------------------------------------------------

def colecao(nome, pai=None):
    c = bpy.data.collections.get(nome)
    if c is None:
        c = bpy.data.collections.new(nome)
        (pai or bpy.context.scene.collection).children.link(c)
    return c


def apagar_colecao(nome):
    c = bpy.data.collections.get(nome)
    if c is None:
        return
    for o in list(c.all_objects):
        d = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if d is not None and d.users == 0:
            if isinstance(d, bpy.types.Mesh):
                bpy.data.meshes.remove(d)
            elif isinstance(d, bpy.types.Armature):
                bpy.data.armatures.remove(d)
    for a in [a for a in bpy.data.actions if a.name.startswith(nome + "_")]:
        bpy.data.actions.remove(a)
    bpy.data.collections.remove(c)


def importar(arquivo, nome):
    """O FBX do Tripo (normalizado em 1 m na maior medida, já olhando para -Y com os pés no chão): a malha com o giro
    e a escala aplicados, soldada (o Tripo separa os vértices nas costuras do UV) e com o material e a textura com o
    nome do bicho. A escala do jogo vem depois (escalar)."""
    caminho = os.path.join(PASTA_FBX, arquivo, arquivo + ".fbx")
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=caminho)
    novos = [o for o in bpy.data.objects if o not in antes]
    malhas = [o for o in novos if o.type == "MESH"]
    assert len(malhas) == 1, (arquivo, [o.name for o in novos])
    ob = malhas[0]
    for o in novos:
        if o is not ob:
            bpy.data.objects.remove(o, do_unlink=True)
    ob.parent = None
    ob.matrix_world = ob.matrix_world.copy()
    me = ob.data
    me.transform(ob.matrix_world)
    ob.matrix_world = Matrix.Identity(4)
    ob.name = nome
    me.name = nome
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    colecao(nome).objects.link(ob)
    # solda
    bm = bmesh.new()
    bm.from_mesh(me)
    antes_v = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.to_mesh(me)
    bm.free()
    me.update()
    # material e textura
    mat = me.materials[0]
    mat.name = nome
    os.makedirs(TEXTURAS, exist_ok=True)
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image is not None:
            origem = bpy.path.abspath(n.image.filepath)
            if not os.path.isfile(origem):
                origem = os.path.join(PASTA_FBX, arquivo, arquivo + ".fbm", arquivo + "_basecolor.jpg")
            destino = os.path.join(TEXTURAS, nome + "_Cor.jpg")
            shutil.copyfile(origem, destino)
            n.image.filepath = destino
            n.image.name = nome + "_Cor"
            n.image.reload()
    for n in mat.node_tree.nodes:
        if n.type == "BSDF_PRINCIPLED":
            n.inputs["Roughness"].default_value = 0.85
            if "Specular IOR Level" in n.inputs:
                n.inputs["Specular IOR Level"].default_value = 0.2
            n.inputs["Metallic"].default_value = 0.0
    return ob, antes_v - len(me.vertices)


def vertices(ob):
    me = ob.data
    V = np.zeros(len(me.vertices) * 3)
    me.vertices.foreach_get("co", V)
    return V.reshape(-1, 3)


def transformar(ob, escala, deslocar=(0, 0, 0)):
    """A malha vezes a escala, com os pés no chão (o z mínimo em 0) e deslocada."""
    V = vertices(ob)
    zmin = V[:, 2].min()
    M = Matrix.Translation(Vector(deslocar)) @ Matrix.Scale(escala, 4) @ Matrix.Translation((0, 0, -zmin))
    ob.data.transform(M)
    ob.data.update()


# ---------------------------------------------------------------------------------------------------------------------
# As medidas
# ---------------------------------------------------------------------------------------------------------------------

def patas_no_chao(V, altura_chao=None):
    """As quatro patas pelo chão: os vértices mais baixos de cada lado (x) e de cada metade (y; a da frente em -Y).
    Devolve {"D.L": (x, y), "D.R", "T.L", "T.R"} (D = dianteira, T = traseira) com o centro da pegada de cada uma."""
    H = V[:, 2].max()
    h = altura_chao or 0.035 * H
    P = V[V[:, 2] < h]
    out = {}
    ymeio = 0.5 * (P[:, 1].min() + P[:, 1].max())
    # duas manchas no y: a separação no maior vão
    ys = np.sort(P[:, 1])
    gaps = np.diff(ys)
    i = int(np.argmax(gaps))
    corte = 0.5 * (ys[i] + ys[i + 1]) if gaps[i] > 0.02 * (ys[-1] - ys[0]) else ymeio
    for nome, sel in (("D", P[:, 1] < corte), ("T", P[:, 1] >= corte)):
        for lado, s in (("L", 1), ("R", -1)):
            Q = P[sel & (P[:, 0] * s > 0)]
            if len(Q) == 0:
                continue
            out[nome + "." + lado] = np.array((0.5 * (Q[:, 0].min() + Q[:, 0].max()), 0.5 * (Q[:, 1].min() + Q[:, 1].max()),
                                               Q[:, 1].min(), Q[:, 1].max()))
    return out


def centro_da_secao(V, ponto, normal, raio, espessura=None):
    """O meio (do mínimo ao máximo) dos vértices da fatia pelo ponto, perpendicular à normal, até o raio do ponto.
    Devolve o ponto no meio da seção (na fatia) ou o próprio ponto se a fatia estiver vazia."""
    p = np.asarray(ponto, float)
    n = np.asarray(normal, float)
    n = n / np.linalg.norm(n)
    e = espessura or raio * 0.25
    d = (V - p) @ n
    sel = np.abs(d) < e
    W = V[sel]
    if len(W) < 3:
        return p, 0.0
    W = W - np.outer((W - p) @ n, n)
    r = np.linalg.norm(W - p, axis=1)
    W = W[r < raio]
    if len(W) < 3:
        return p, 0.0
    # dois eixos da fatia
    a = np.cross(n, (0, 0, 1) if abs(n[2]) < 0.9 else (1, 0, 0))
    a /= np.linalg.norm(a)
    b = np.cross(n, a)
    ua, ub = (W - p) @ a, (W - p) @ b
    c = p + a * 0.5 * (ua.min() + ua.max()) + b * 0.5 * (ub.min() + ub.max())
    larg = max(ua.max() - ua.min(), ub.max() - ub.min())
    return c, larg


def subir_pata(V, pe_xy, z0, z1, passo, raio):
    """A linha do meio de uma pata: de z0 a z1, a cada passo, o centro (x, y) da seção horizontal perto do anterior.
    Devolve a lista de (x, y, z, largura_x, largura_y)."""
    out = []
    c = np.array(pe_xy[:2], float)
    z = z0
    while z <= z1 + 1e-9:
        sel = (np.abs(V[:, 2] - z) < passo * 0.75)
        W = V[sel]
        if len(W):
            r = np.linalg.norm(W[:, :2] - c, axis=1)
            W = W[r < raio]
        if len(W) >= 3:
            nc = np.array((0.5 * (W[:, 0].min() + W[:, 0].max()), 0.5 * (W[:, 1].min() + W[:, 1].max())))
            c = 0.5 * c + 0.5 * nc
            out.append((c[0], c[1], z, W[:, 0].max() - W[:, 0].min(), W[:, 1].max() - W[:, 1].min()))
        z += passo
    return out



def triangulos(ob):
    """Os triângulos da malha (M, 3, 3), no espaço do objeto."""
    me = ob.data
    me.calc_loop_triangles()
    idx = np.zeros(len(me.loop_triangles) * 3, np.int32)
    me.loop_triangles.foreach_get("vertices", idx)
    V = vertices(ob)
    return V[idx.reshape(-1, 3)]


def corte(T, ponto, normal):
    """Os pontos onde as arestas dos triângulos cruzam o plano (pelo ponto, com a normal)."""
    n = np.asarray(normal, float)
    n = n / np.linalg.norm(n)
    s = (T - np.asarray(ponto, float)) @ n            # (M, 3)
    pts = []
    for a, b in ((0, 1), (1, 2), (2, 0)):
        sa, sb = s[:, a], s[:, b]
        m = (sa * sb) < 0
        t = (sa[m] / (sa[m] - sb[m]))[:, None]
        pts.append(T[m, a] + (T[m, b] - T[m, a]) * t)
    return np.concatenate(pts) if pts else np.zeros((0, 3))


def centro_do_corte(T, ponto, normal, raio):
    """O meio (do mínimo ao máximo, nos dois eixos do plano) do corte perto do ponto: para pôr uma junta no meio da
    pata, do pescoço ou da cauda. Devolve (centro, largura no eixo a, largura no eixo b)."""
    p = np.asarray(ponto, float)
    n = np.asarray(normal, float)
    n = n / np.linalg.norm(n)
    P = corte(T, p, n)
    if len(P):
        P = P[np.linalg.norm(P - p, axis=1) < raio]
    if len(P) < 3:
        return p, 0.0, 0.0
    a = np.cross(n, (0, 0, 1) if abs(n[2]) < 0.9 else (1, 0, 0))
    a /= np.linalg.norm(a)
    b = np.cross(n, a)
    ua, ub = (P - p) @ a, (P - p) @ b
    c = p + a * 0.5 * (ua.min() + ua.max()) + b * 0.5 * (ub.min() + ub.max())
    return c, float(ua.max() - ua.min()), float(ub.max() - ub.min())


def topo(V, y, x=0.0, faixa=0.02, largura=None):
    """O alto (z máximo) da malha na fatia y (e perto do x)."""
    sel = np.abs(V[:, 1] - y) < faixa
    if largura is not None:
        sel &= np.abs(V[:, 0] - x) < largura
    if not sel.any():
        return None
    return float(V[sel, 2].max())


def fundo(V, y, x=0.0, faixa=0.02, largura=None):
    sel = np.abs(V[:, 1] - y) < faixa
    if largura is not None:
        sel &= np.abs(V[:, 0] - x) < largura
    if not sel.any():
        return None
    return float(V[sel, 2].min())


def meio_yz(V, y, z, raio, x=0.0):
    """O centro da seção vertical (perpendicular ao Y) perto de (x, y, z): para a coluna, o pescoço e a cauda."""
    c, _ = centro_da_secao(V, (x, y, z), (0, 1, 0), raio)
    return c


# ---------------------------------------------------------------------------------------------------------------------
# O esqueleto
# ---------------------------------------------------------------------------------------------------------------------

def esqueleto(nome, ossos, colecao_):
    """ossos: [(nome, cabeça, ponta, pai, deforma)]. O roll como nos cervos do Pedro: os ossos em pé (|d.z| > 0,7) com o
    Z para a frente (-Y), os outros com o Z para cima."""
    arm = bpy.data.armatures.new(nome + "_Esqueleto")
    ob = bpy.data.objects.new(nome + "_Esqueleto", arm)
    colecao_.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.view_layer.objects:
        o.select_set(o == ob)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = {}
    for n, a, b, pai, _ in ossos:
        e = arm.edit_bones.new(n)
        e.head, e.tail = Vector(a), Vector(b)
        d = (Vector(b) - Vector(a)).normalized()
        e.align_roll(Vector((0, -1, 0)) if abs(d.z) > 0.7 else Vector((0, 0, 1)))
        if pai:
            e.parent = eb[pai]
        eb[n] = e
    for n, e in eb.items():
        if e.parent is not None and (e.head - e.parent.tail).length < 1e-4:
            e.use_connect = True
    bpy.ops.object.mode_set(mode="OBJECT")
    for n, a, b, pai, deforma in ossos:
        arm.bones[n].use_deform = deforma
    arm.display_type = "OCTAHEDRAL"
    ob.show_in_front = True
    for pb in ob.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return ob


def pesar(ob, rig):
    """Os pesos automáticos do Blender (o calor dos ossos). Devolve quantos vértices ficaram sem peso (esses pegam os
    do vizinho mais perto)."""
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    ob.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    return completar_pesos(ob, rig)


def pesos(ob):
    """Matriz (vértices × grupos) dos pesos."""
    nomes = [g.name for g in ob.vertex_groups]
    W = np.zeros((len(ob.data.vertices), len(nomes)))
    for v in ob.data.vertices:
        for g in v.groups:
            W[v.index, g.group] = g.weight
    return nomes, W


def completar_pesos(ob, rig, limite=4):
    """Os vértices sem peso pegam os do vizinho mais perto com peso; cada vértice fica com os 4 maiores, somando 1."""
    deforma = {b.name for b in rig.data.bones if b.use_deform}
    for g in list(ob.vertex_groups):
        if g.name not in deforma:
            ob.vertex_groups.remove(g)
    nomes, W = pesos(ob)
    V = vertices(ob)
    soma = W.sum(1)
    sem = np.nonzero(soma < 1e-6)[0]
    com = np.nonzero(soma >= 1e-6)[0]
    if len(sem) and len(com):
        for i in sem:
            j = com[np.argmin(np.linalg.norm(V[com] - V[i], axis=1))]
            W[i] = W[j]
    ordem = np.argsort(-W, 1)
    corte = np.zeros_like(W, bool)
    np.put_along_axis(corte, ordem[:, :limite], True, 1)
    W = np.where(corte, W, 0)
    W[W < 0.01] = 0
    W /= np.maximum(W.sum(1, keepdims=True), 1e-9)
    aplicar_pesos(ob, nomes, W)
    return len(sem)


def aplicar_pesos(ob, nomes, W):
    for g in list(ob.vertex_groups):
        ob.vertex_groups.remove(g)
    grupos = [ob.vertex_groups.new(name=n) for n in nomes]
    for j, g in enumerate(grupos):
        idx = np.nonzero(W[:, j] > 0)[0]
        for i in idx:
            g.add([int(i)], float(W[i, j]), "REPLACE")


# ---------------------------------------------------------------------------------------------------------------------
# O motor das poses
# ---------------------------------------------------------------------------------------------------------------------

class Pose:
    """Uma pose: R[osso] = o giro no espaço do bicho (relativo ao pai, a partir do repouso), T[osso] = o deslocamento
    no espaço do bicho (depois do giro do pai), S[osso] = a escala no espaço do osso."""

    def __init__(self):
        self.R = {}
        self.T = {}
        self.S = {}

    def copia(self):
        p = Pose()
        p.R = {k: v.copy() for k, v in self.R.items()}
        p.T = {k: v.copy() for k, v in self.T.items()}
        p.S = {k: v.copy() for k, v in self.S.items()}
        return p

    def girar(self, osso, rot):
        """Mais um giro (no espaço do bicho, depois dos que já tem)."""
        self.R[osso] = rot @ self.R.get(osso, Quaternion())
        return self

    def mover(self, osso, d):
        self.T[osso] = self.T.get(osso, Vector()) + Vector(d)
        return self


def misturar(a, b, t):
    p = Pose()
    for k in set(a.R) | set(b.R):
        p.R[k] = slerp(a.R.get(k, Quaternion()), b.R.get(k, Quaternion()), t)
    for k in set(a.T) | set(b.T):
        p.T[k] = a.T.get(k, Vector()).lerp(b.T.get(k, Vector()), t)
    for k in set(a.S) | set(b.S):
        p.S[k] = a.S.get(k, Vector((1, 1, 1))).lerp(b.S.get(k, Vector((1, 1, 1))), t)
    return p


class Corpo:
    """O repouso do esqueleto e o FK feito aqui (sem o Blender)."""

    def __init__(self, rig):
        self.rig = rig
        self.ossos = [b.name for b in rig.data.bones]
        self.pai = {b.name: (b.parent.name if b.parent else None) for b in rig.data.bones}
        self.cab = {b.name: b.head_local.copy() for b in rig.data.bones}
        self.pon = {b.name: b.tail_local.copy() for b in rig.data.bones}
        self.B = {b.name: b.matrix_local.to_3x3() for b in rig.data.bones}
        self.Bi = {n: m.inverted() for n, m in self.B.items()}
        ordem, feito = [], set()

        def vis(n):
            if n in feito:
                return
            if self.pai[n]:
                vis(self.pai[n])
            feito.add(n)
            ordem.append(n)
        for n in self.ossos:
            vis(n)
        self.ordem = ordem

    def dir(self, n):
        return (self.pon[n] - self.cab[n]).normalized()

    def comp(self, n):
        return (self.pon[n] - self.cab[n]).length

    def fk(self, pose, ate=None):
        """Rw (o giro no mundo) e Hw (a cabeça no mundo) de cada osso."""
        Rw, Hw = {}, {}
        for n in self.ordem:
            p = self.pai[n]
            R = pose.R.get(n, Quaternion())
            T = pose.T.get(n)
            if p is None:
                Rw[n] = R
                Hw[n] = self.cab[n] + (T if T is not None else Vector())
            else:
                Rw[n] = Rw[p] @ R
                Hw[n] = Hw[p] + Rw[p] @ (self.cab[n] - self.cab[p])
                if T is not None:
                    Hw[n] = Hw[n] + Rw[p] @ T
            if ate is not None and n == ate:
                break
        return Rw, Hw

    def ponta(self, Rw, Hw, n):
        return Hw[n] + Rw[n] @ (self.pon[n] - self.cab[n])

    def ponto(self, Rw, Hw, n, p_repouso):
        """Um ponto preso ao osso n (dado no repouso) na pose."""
        return Hw[n] + Rw[n] @ (Vector(p_repouso) - self.cab[n])

    def apontar(self, pose, Rw, Hw, n, direcao, girar_filhos=True):
        """Gira o osso n (pelo menor giro a partir de como o pai o levou) para a ponta ir na direção dada (no mundo).
        Atualiza Rw do osso (os filhos são refeitos no próximo fk)."""
        p = self.pai[n]
        Rp = Rw[p] if p else Quaternion()
        atual = Rp @ self.dir(n)
        d = Vector(direcao).normalized()
        Rw_n = atual.rotation_difference(d) @ Rp
        pose.R[n] = Rp.inverted() @ Rw_n
        Rw[n] = Rw_n
        return Rw_n


def ik2(H, W, L1, L2, curva):
    """O IK de dois ossos no plano que contém H, W e a direção da curva (perpendicular a H→W): a junta do meio."""
    u = W - H
    D = u.length
    D = min(max(D, abs(L1 - L2) + 1e-4), L1 + L2 - 1e-4)
    u = u.normalized()
    v = curva - u * curva.dot(u)
    if v.length < 1e-6:
        v = Z.cross(u)
    v = v.normalized()
    a = math.acos(min(max((L1 * L1 + D * D - L2 * L2) / (2 * L1 * D), -1), 1))
    M = H + u * (L1 * math.cos(a)) + v * (L1 * math.sin(a))
    Wf = H + u * D
    return M, Wf


class Perna:
    """Uma pata de 4 ossos [b1, b2, b3, b4] (a coxa ou o braço, a perna ou o antebraço, a canela e a pata ou o casco).
    O b4 (a pata) fica deitado no chão no repouso; o pé é a cabeça do b4 (o boleto, a bola do pé)."""

    def __init__(self, corpo, ossos, chave):
        self.c = corpo
        self.ossos = ossos
        self.chave = chave            # "D.L", "T.R"...
        b1, b2, b3, b4 = ossos
        self.L1, self.L2, self.L3 = corpo.comp(b1), corpo.comp(b2), corpo.comp(b3)
        self.pe = corpo.cab[b4].copy()          # a cabeça da pata no repouso
        self.ponta = corpo.pon[b4].copy()
        self.v4 = self.ponta - self.pe
        # o lado da curva da junta do meio no repouso (a perna para trás no braço, para a frente na coxa)
        H, M, W = corpo.cab[b1], corpo.cab[b2], corpo.cab[b3]
        u = (W - H).normalized()
        m = (M - H) - u * (M - H).dot(u)
        self.curva_y = 1.0 if m.y >= 0 else -1.0
        self.d3 = corpo.dir(b3)

    def resolver(self, pose, Rw, Hw, pe, giro3=0.0, giro4=0.0, curva=None):
        """Leva a cabeça do b4 ao ponto pe (no mundo). giro3: o giro da canela (em volta do X, + = a ponta para trás)
        a partir da direção do repouso; giro4: o giro da pata no mundo (+ = a ponta para baixo... em volta do X)."""
        c = self.c
        b1, b2, b3, b4 = self.ossos
        d3 = Quaternion(X, giro3) @ self.d3
        W = Vector(pe) - d3 * self.L3
        H = Hw[b1]
        cv = curva if curva is not None else Vector((0, self.curva_y, 0))
        M, Wf = ik2(H, W, self.L1, self.L2, cv)
        c.apontar(pose, Rw, Hw, b1, M - H)
        Hw[b2] = H + Rw[b1] @ (c.cab[b2] - c.cab[b1])
        c.apontar(pose, Rw, Hw, b2, Wf - Hw[b2])
        Hw[b3] = Hw[b2] + Rw[b2] @ (c.cab[b3] - c.cab[b2])
        c.apontar(pose, Rw, Hw, b3, Vector(pe) - Hw[b3])
        Hw[b4] = Hw[b3] + Rw[b3] @ (c.cab[b4] - c.cab[b3])
        Rw4 = Quaternion(X, giro4)
        pose.R[b4] = Rw[b3].inverted() @ Rw4
        Rw[b4] = Rw4
        return (W - H).length / (self.L1 + self.L2)


# ---------------------------------------------------------------------------------------------------------------------
# A gravação
# ---------------------------------------------------------------------------------------------------------------------

class Gravador:
    """Grava uma ação: uma chave por quadro (linear) de cada osso, com o giro em quaternion contínuo."""

    def __init__(self, corpo, nome, ciclica=False):
        self.c = corpo
        self.nome = nome
        self.ciclica = ciclica
        self.quadros = []

    def add(self, pose):
        self.quadros.append(pose)

    def gravar(self):
        c = self.c
        rig = c.rig
        act = bpy.data.actions.get(self.nome)
        if act is not None:
            bpy.data.actions.remove(act)
        act = bpy.data.actions.new(self.nome)
        if rig.animation_data is None:
            rig.animation_data_create()
        rig.animation_data.action = act
        N = len(self.quadros)
        canais = {}
        for n in c.ossos:
            qs = []
            ant = None
            for p in self.quadros:
                R = p.R.get(n, Quaternion())
                qb = (c.Bi[n] @ R.to_matrix() @ c.B[n]).to_quaternion()
                if ant is not None and qb.dot(ant) < 0:
                    qb = -qb
                ant = qb
                qs.append(tuple(qb))
            qs = np.array(qs)
            if np.abs(qs - (1, 0, 0, 0)).max() > 1e-6:
                canais[("rotation_quaternion", n)] = qs
            if any(n in p.T for p in self.quadros):
                ls = np.array([tuple(c.Bi[n] @ p.T.get(n, Vector())) for p in self.quadros])
                canais[("location", n)] = ls
            if any(n in p.S for p in self.quadros):
                ss = np.array([tuple(p.S.get(n, Vector((1, 1, 1)))) for p in self.quadros])
                canais[("scale", n)] = ss
        # os ossos sem chave nenhuma ficam no repouso: põe ao menos a rotação de todos (a troca de ação limpa a pose)
        for n in c.ossos:
            if ("rotation_quaternion", n) not in canais:
                canais[("rotation_quaternion", n)] = np.tile((1.0, 0, 0, 0), (N, 1))
        quadros = np.arange(1, N + 1, dtype=np.float32)
        for (prop, n), vals in canais.items():
            dp = 'pose.bones["%s"].%s' % (n, prop)
            for i in range(vals.shape[1]):
                fc = act.fcurve_ensure_for_datablock(rig, dp, index=i, group_name=n)
                fc.keyframe_points.add(N)
                co = np.empty(2 * N, np.float32)
                co[0::2] = quadros
                co[1::2] = vals[:, i]
                fc.keyframe_points.foreach_set("co", co)
                fc.keyframe_points.foreach_set("interpolation", np.ones(N, np.int32))
                fc.update()
        act.use_frame_range = True
        act.frame_start = 1
        act.frame_end = max(N, 2)
        act.use_cyclic = self.ciclica
        act.use_fake_user = True
        return act


def faixas_nla(rig, acoes, ativa=None):
    """Uma faixa de NLA por ação (mudas), como no jogador e nos zumbis; a ativa fica tocando."""
    ad = rig.animation_data or rig.animation_data_create()
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    for a in acoes:
        t = ad.nla_tracks.new()
        t.name = a.name
        s = t.strips.new(a.name, 1, a)
        s.extrapolation = "NOTHING"
        t.mute = True
    ad.action = ativa or (acoes[0] if acoes else None)


# ---------------------------------------------------------------------------------------------------------------------
# A limpeza dos pesos
# ---------------------------------------------------------------------------------------------------------------------

def distancia_aos_ossos(V, rig, nomes):
    D = np.zeros((len(V), len(nomes)))
    for j, n in enumerate(nomes):
        b = rig.data.bones[n]
        a, c = np.array(b.head_local), np.array(b.tail_local)
        d = c - a
        t = np.clip(((V - a) @ d) / max(d @ d, 1e-12), 0, 1)
        D[:, j] = np.linalg.norm(V - (a + t[:, None] * d), axis=1)
    return D


def vizinhos(ob):
    E = np.zeros(len(ob.data.edges) * 2, np.int32)
    ob.data.edges.foreach_get("vertices", E)
    return E.reshape(-1, 2)


def suavizar_pesos(W, E, permitido, vezes=3, a=0.5):
    n = len(W)
    for _ in range(vezes):
        S = np.zeros_like(W)
        cnt = np.zeros(n)
        np.add.at(S, E[:, 0], W[E[:, 1]])
        np.add.at(S, E[:, 1], W[E[:, 0]])
        np.add.at(cnt, E[:, 0], 1)
        np.add.at(cnt, E[:, 1], 1)
        M = S / np.maximum(cnt, 1)[:, None]
        tem = cnt > 0
        W[tem] = (1 - a) * W[tem] + a * M[tem]
        W = np.where(permitido, W, 0)
        W /= np.maximum(W.sum(1, keepdims=True), 1e-9)
    return W


def limpar_pesos(ob, rig, regras, vezes=3, limite=4):
    """regras(V, nomes, D) -> a matriz booleana do que cada osso pode pegar. Os pesos fora dela saem; o vértice que
    fica sem nada pega o osso permitido mais perto; depois a suavização (só no permitido) e os 4 maiores."""
    nomes, W = pesos(ob)
    V = vertices(ob)
    D = distancia_aos_ossos(V, rig, nomes)
    P = regras(V, nomes, D, W)
    W = np.where(P, W, 0)
    soma = W.sum(1)
    for i in np.nonzero(soma < 1e-6)[0]:
        d = np.where(P[i], D[i], 1e9)
        j = int(np.argmin(d))
        if d[j] > 2.5 * D[i].min():          # nada permitido perto: o osso mais perto de todos
            j = int(np.argmin(D[i]))
            P[i, j] = True
        W[i, j] = 1.0
    W /= np.maximum(W.sum(1, keepdims=True), 1e-9)
    W = suavizar_pesos(W, vizinhos(ob), P, vezes)
    ordem = np.argsort(-W, 1)
    corte = np.zeros_like(W, bool)
    np.put_along_axis(corte, ordem[:, :limite], True, 1)
    W = np.where(corte, W, 0)
    W[W < 0.01] = 0
    W /= np.maximum(W.sum(1, keepdims=True), 1e-9)
    aplicar_pesos(ob, nomes, W)
    return W
