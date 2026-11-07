# A moto do Zombie City Cleaner: uma Harley-Davidson Fat Boy 2019 "inspirada" (Softail, motor Milwaukee-Eight 114),
# low poly, para dois (o piloto e o garupa), em peças separadas como o carro do jogo (Blender/carro.py do repositório).
#
# Como rodar (Blender 5.2, num processo em segundo plano; não mexe no Blender aberto):
#   set OCO_CPU=1
#   blender -b --factory-startup --python moto.py -- tudo v1            (modelo + .blend + FBX + medidas + fotos Cycles CPU)
#   blender -b --factory-startup --python moto.py -- rascunho v1 <pasta> (só o modelo e fotos rápidas no Workbench)
#   python moto_fotos.py v1                                              (fora do Blender: fundo, prancha e comparação)
# Cada versão grava ao lado das outras (Moto_v1.blend, Moto_v2.blend...): nenhuma é apagada nem sobrescrita.
#
# As medidas são escritas nos eixos da Unity, como no carro.py: x para a direita da moto, y para cima, z para a frente;
# o chão em y = 0 e o meio entre os eixos em z = 0. No fim cada peça vira um objeto girado 90° em X com a malha
# espelhada em X (o mesmo formato do Itens.fbx e do Carro.fbx: a Unity espelha o X de volta ao importar).
#
# As peças (objetos do FBX):
# - Moto_Corpo: tudo o que não mexe (quadro, tanque, banco de dois lugares, sissy bar com encosto, motor, escapamentos,
#   paralama de trás, placa, piscas de trás, balança, pedaleiras). A origem no chão, no meio entre os eixos.
# - Moto_Direcao: o que gira com o guidão (garfo, mesas, carenagem do farol, farol, guidão, manetes, retrovisores,
#   piscas da frente, paralama da frente, pinça do freio). A origem no pivô da direção (em cima da coluna do quadro) e a
#   malha com o eixo da direção em pé (o Y dela); na moto ela fica tombada pelo ângulo do garfo (RAKE: na Unity,
#   rotação X = -RAKE, o alto para trás).
# - Moto_RodaFrente / Moto_RodaTras: a roda inteira (pneu, roda de disco "Lakester", disco do freio; atrás também a polia
#   da correia), a origem no meio, o eixo em X. Na frente o disco fica à esquerda; atrás, o disco à direita e a polia à
#   esquerda. A da frente é filha da direção no jogo.
# - Moto_Descanso: o descanso lateral (à esquerda), a origem na dobradiça.
# Os pontos para a Unity (os lugares, as mãos e os pés) são vazios na coleção Moto_Pontos (não vão no FBX).

import json
import math
import os
import sys
import time

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

AQUI = os.path.dirname(os.path.abspath(__file__))


def V(x, y, z):
    return Vector((x, y, z))


X, Y, Z = V(1, 0, 0), V(0, 1, 0), V(0, 0, 1)

# ---------------------------------------------------------------------------------------------------- medidas (m)
EIXO = 0.8325             # z do eixo da frente (o de trás em -EIXO): 1,665 m entre os eixos
R_RODA = 0.325            # raio dos pneus (160/60 R18 na frente, 240/40 R18 atrás: o mesmo diâmetro)
MEIO_PNEU_F = 0.086       # meia largura do pneu da frente (gordo)
MEIO_PNEU_T = 0.12        # e do de trás
RAKE = math.radians(32)   # a inclinação da direção (o alto do garfo para trás)
D_GARFO = 0.065           # o garfo fica 6,5 cm à frente do eixo da direção
X_GARFO = 0.155           # meia distância entre as bengalas
Y_PIVO = 0.985            # a altura do pivô da direção (em cima da coluna do quadro)
C_MOTOR = V(0, 0.37, 0.08)       # o virabrequim
MEIO_V = math.radians(22.5)      # o V de 45°

FRENTE = V(0, R_RODA, EIXO)
TRAS = V(0, R_RODA, -EIXO)
U = V(0, math.cos(RAKE), -math.sin(RAKE))     # o eixo da direção, para cima
FW = V(0, math.sin(RAKE), math.cos(RAKE))     # perpendicular a ele, para a frente
A0 = FRENTE - FW * D_GARFO
PIVO = A0 + U * ((Y_PIVO - A0.y) / U.y)
QUADRO_DIRECAO = Matrix.Translation(PIVO) @ Matrix.Rotation(-RAKE, 4, "X")
DESCANSO = V(-0.13, 0.155, 0.10)   # a dobradiça do descanso

# os pontos para a Unity (eixos da Unity)
PONTOS = {
    "Lugar_Piloto": (0.0, 0.70, -0.30),
    "Lugar_Garupa": (0.0, 0.82, -0.68),
    "Mao_Direita": (0.38, 1.133, 0.131),
    "Mao_Esquerda": (-0.38, 1.133, 0.131),
    "Pe_Piloto_Direito": (0.31, 0.32, 0.22),
    "Pe_Piloto_Esquerdo": (-0.31, 0.32, 0.22),
    "Pe_Garupa_Direito": (0.24, 0.578, -0.40),
    "Pe_Garupa_Esquerdo": (-0.24, 0.578, -0.40),
}


def garfo(s, x=0.0):
    """Um ponto na linha das bengalas, s metros acima do eixo da roda da frente (x: o lado)."""
    return FRENTE + U * s + V(x, 0, 0)


def no_eixo(y):
    """O ponto do eixo da direção na altura y."""
    return A0 + U * ((y - A0.y) / U.y)


def para_blender(p):
    """Um ponto nos eixos da Unity para a posição de um objeto no Blender (o X espelhado, o Y da Unity no Z)."""
    x, y, z = p
    return (-x, -z, y)


# ---------------------------------------------------------------------------------------------------- materiais
MATERIAIS = ["Pintura", "Preto", "Escuro", "Cromado", "Aluminio", "Borracha", "Couro", "Lente", "Farol", "Lanterna",
             "Placa"]
MI = {n: i for i, n in enumerate(MATERIAIS)}
PBR = {
    # o "Black Denim": preto acinzentado acetinado (o tanque e os paralamas)
    "Pintura": dict(cor=(0.030, 0.030, 0.032), metal=0.15, asp=0.5, verniz=0.10),
    "Preto": dict(cor=(0.010, 0.010, 0.011), metal=0.0, asp=0.25, verniz=0.6),      # quadro, balança, cárter
    "Escuro": dict(cor=(0.032, 0.032, 0.034), metal=0.6, asp=0.42),                  # cilindros, a faixa das rodas
    "Cromado": dict(cor=(0.93, 0.93, 0.95), metal=1.0, asp=0.05),
    "Aluminio": dict(cor=(0.78, 0.78, 0.80), metal=1.0, asp=0.2),                    # usinado: aletas, roda, discos
    "Borracha": dict(cor=(0.012, 0.012, 0.012), metal=0.0, asp=0.88, brilho=0.2),
    "Couro": dict(cor=(0.016, 0.015, 0.014), metal=0.0, asp=0.55, brilho=0.3, verniz=0.06),
    "Lente": dict(cor=(1.0, 0.33, 0.02), metal=0.0, asp=0.12, luz=0.5),             # piscas e refletores
    "Farol": dict(cor=(0.92, 0.95, 1.0), metal=0.0, asp=0.05, luz=2.5),
    "Lanterna": dict(cor=(0.75, 0.02, 0.02), metal=0.0, asp=0.12, luz=0.8),
    "Placa": dict(cor=(0.86, 0.86, 0.84), metal=0.0, asp=0.45),
}


def material(nome):
    m = bpy.data.materials.get(nome)
    if m is None:
        m = bpy.data.materials.new(nome)
    try:
        m.use_nodes = True
    except Exception:
        pass
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    d = PBR[nome]
    b.inputs["Base Color"].default_value = (*d["cor"], 1.0)
    b.inputs["Metallic"].default_value = d.get("metal", 0.0)
    b.inputs["Roughness"].default_value = d.get("asp", 0.5)
    if "verniz" in d:
        b.inputs["Coat Weight"].default_value = d["verniz"]
    if "brilho" in d:
        b.inputs["Specular IOR Level"].default_value = d["brilho"]
    if "luz" in d:
        b.inputs["Emission Color"].default_value = (*d["cor"], 1.0)
        b.inputs["Emission Strength"].default_value = d["luz"]
    m.diffuse_color = (*d["cor"], 1.0)
    m.metallic = min(d.get("metal", 0.0), 0.15)
    m.roughness = max(d.get("asp", 0.5), 0.3)
    return m


# ---------------------------------------------------------------------------------------------------- a malha
class Malha:
    """Junta peças (caixas, barras, canos, tornos, cascas, decalques e textos) numa malha, nos eixos da Unity."""

    def __init__(self):
        self.bm = bmesh.new()
        self.usados = set()

    def _pinta(self, faces, mat):
        i = MI[mat]
        for f in faces:
            f.material_index = i
        self.usados.add(i)

    def _v(self, p):
        return self.bm.verts.new(Vector(p))

    def _f(self, vs, mat):
        if len(set(vs)) < 3:
            return None
        try:
            f = self.bm.faces.new(vs)
        except ValueError:
            return None
        self._pinta([f], mat)
        return f

    def caixa(self, mat, tam, centro, giro=(0, 0, 0), chanfro=0.0, rot=None):
        """Uma caixa de tam (x, y, z), girada (graus: X, depois Y, depois Z, ou a matriz rot) e chanfrada."""
        vs = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        if rot is None:
            rot = Matrix.Rotation(math.radians(giro[2]), 4, "Z") @ Matrix.Rotation(math.radians(giro[1]), 4, "Y") @ \
                Matrix.Rotation(math.radians(giro[0]), 4, "X")
        m = Matrix.Translation(Vector(centro)) @ rot.to_4x4() @ Matrix.Diagonal((tam[0], tam[1], tam[2], 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=vs)
        faces = list({f for v in vs for f in v.link_faces})
        self._pinta(faces, mat)
        if chanfro > 0 and min(tam) > chanfro * 2.2:
            arestas = list({e for f in faces for e in f.edges})
            r = bmesh.ops.bevel(self.bm, geom=vs + arestas, offset=chanfro, segments=1, affect="EDGES", profile=0.5)
            self._pinta(r["faces"], mat)

    def ripa(self, mat, a, b, tx, ty, chanfro=0.0, ref=X):
        """Uma barra de a até b: tx na direção de ref (o X, se não disser) e ty na outra."""
        a, b = Vector(a), Vector(b)
        z = (b - a).normalized()
        x = Vector(ref) - z * Vector(ref).dot(z)
        if x.length < 1e-4:
            x = Y - z * Y.dot(z)
        x.normalize()
        y = z.cross(x)
        self.caixa(mat, (tx, ty, (b - a).length), (a + b) / 2, rot=Matrix((x, y, z)).transposed(), chanfro=chanfro)

    def cano(self, mat, pts, raio, lados=8, tampas=True, n0=None, fase=0.0, mat_tampa=None):
        """Um tubo pelos pontos (o raio pode ser (ra, rb), uma elipse, ou uma lista por ponto)."""
        pts = [Vector(p) for p in pts]
        n_p = len(pts)
        tang = []
        for i in range(n_p):
            if i == 0:
                t = pts[1] - pts[0]
            elif i == n_p - 1:
                t = pts[-1] - pts[-2]
            else:
                t = (pts[i + 1] - pts[i]).normalized() + (pts[i] - pts[i - 1]).normalized()
            tang.append(t.normalized())
        ref = Vector(n0) if n0 is not None else (X if abs(tang[0].x) < 0.9 else Y)
        n = (ref - tang[0] * ref.dot(tang[0])).normalized()
        aneis = []
        for i in range(n_p):
            t = tang[i]
            if i > 0:
                n = tang[i - 1].rotation_difference(t) @ n
                n = (n - t * n.dot(t)).normalized()
            b = t.cross(n)
            r = raio[i] if isinstance(raio, list) else raio
            ra, rb = r if isinstance(r, tuple) else (r, r)
            esc = 1.0
            if 0 < i < n_p - 1:
                esc = 1.0 / max(0.6, (pts[i] - pts[i - 1]).normalized().dot(t))
            anel = []
            for j in range(lados):
                a = fase + 2 * math.pi * j / lados
                anel.append(self._v(pts[i] + (n * (math.cos(a) * ra) + b * (math.sin(a) * rb)) * esc))
            aneis.append(anel)
        for i in range(n_p - 1):
            A, B = aneis[i], aneis[i + 1]
            for j in range(lados):
                j2 = (j + 1) % lados
                self._f((A[j], A[j2], B[j2], B[j]), mat)
        if tampas:
            self._f(list(reversed(aneis[0])), mat_tampa or mat)
            self._f(aneis[-1], mat_tampa or mat)

    def torno(self, mat, perfil, centro, eixo, lados=16, n0=None, escala=(1.0, 1.0), fechado=False, fase=0.0):
        """
        Gira um perfil de pontos (h ao longo do eixo, r) em volta do eixo. As normais saem para fora quando o perfil anda
        para +h por fora e entra (r diminuindo) na ponta de +h. mat: um material ou um por trecho do perfil.
        """
        ax = Vector(eixo).normalized()
        ref = Vector(n0) if n0 is not None else (Y if abs(ax.y) < 0.9 else X)
        e1 = (ref - ax * ref.dot(ax)).normalized()
        e2 = ax.cross(e1)
        c = Vector(centro)
        aneis = []
        for h, r in perfil:
            if r < 1e-6:
                aneis.append([self._v(c + ax * h)])
            else:
                aneis.append([self._v(c + ax * h + e1 * (math.cos(fase + 2 * math.pi * j / lados) * r * escala[0]) +
                                      e2 * (math.sin(fase + 2 * math.pi * j / lados) * r * escala[1]))
                              for j in range(lados)])
        trechos = list(range(len(aneis) - 1)) + ([len(aneis) - 1] if fechado else [])
        mats = list(mat) if isinstance(mat, (list, tuple)) else [mat] * len(trechos)
        for k, i in enumerate(trechos):
            A, B = aneis[i], aneis[(i + 1) % len(aneis)]
            if len(A) == 1 and len(B) == 1:
                continue
            for j in range(lados):
                j2 = (j + 1) % lados
                if len(A) == 1:
                    vs = (A[0], B[j2], B[j])
                elif len(B) == 1:
                    vs = (A[j], A[j2], B[0])
                else:
                    vs = (A[j], A[j2], B[j2], B[j])
                self._f(vs, mats[k])

    def casca(self, mat, aneis, tampas=True):
        """Uma casca fechada pelos anéis (listas de pontos com a mesma contagem); as normais são acertadas no fim."""
        vs = [[self._v(p) for p in anel] for anel in aneis]
        faces = []
        n = len(vs[0])
        for i in range(len(vs) - 1):
            A, B = vs[i], vs[i + 1]
            for j in range(n):
                j2 = (j + 1) % n
                f = self._f((A[j], A[j2], B[j2], B[j]), mat)
                if f:
                    faces.append(f)
        if tampas:
            for anel in (vs[0], vs[-1]):
                f = self._f(anel, mat)
                if f:
                    faces.append(f)
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)

    def perfil(self, mat, pontos, x0, x1):
        """Um polígono no plano de lado (pontos em (z, y)), puxado em X de x0 a x1."""
        a = [self._v((x0, y, z)) for z, y in pontos]
        b = [self._v((x1, y, z)) for z, y in pontos]
        n = len(pontos)
        faces = [self._f(a, mat), self._f(list(reversed(b)), mat)]
        faces += [self._f((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]), mat) for i in range(n)]
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in faces if f])

    def poligono(self, mat, pts, normal):
        """Uma face só (um decalque), virada para a normal."""
        f = self._f([self._v(p) for p in pts], mat)
        if f is None:
            return
        f.normal_update()
        if f.normal.dot(Vector(normal)) < 0:
            f.normal_flip()

    def disco(self, mat, centro, u, v, r, lados, normal):
        c, u, v = Vector(centro), Vector(u), Vector(v)
        pts = [c + u * (r * math.cos(2 * math.pi * k / lados)) + v * (r * math.sin(2 * math.pi * k / lados))
               for k in range(lados)]
        self.poligono(mat, pts, normal)

    def anel(self, mat, centro, u, v, r0, r1, lados, normal):
        """Um anel chato (quadriláteros de uma face só) entre os raios r0 e r1."""
        c, u, v = Vector(centro), Vector(u), Vector(v)
        dirs = [u * math.cos(2 * math.pi * k / lados) + v * math.sin(2 * math.pi * k / lados) for k in range(lados)]
        for k in range(lados):
            a, b = dirs[k], dirs[(k + 1) % lados]
            self.poligono(mat, [c + a * r0, c + b * r0, c + b * r1, c + a * r1], normal)

    def texto(self, mat, txt, centro, u, v, altura, normal):
        """Letras chapadas (a fonte embutida do Blender), centradas, com a altura das letras."""
        cu = bpy.data.curves.new("_txt", type="FONT")
        cu.body = txt
        cu.resolution_u = 2
        ob = bpy.data.objects.new("_txt", cu)
        bpy.context.scene.collection.objects.link(ob)
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
        bpy.data.objects.remove(ob)
        bpy.data.curves.remove(cu)
        xs = [p.co.x for p in me.vertices]
        ys = [p.co.y for p in me.vertices]
        cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        k = altura / (max(ys) - min(ys))
        u, v, c = Vector(u).normalized(), Vector(v).normalized(), Vector(centro)
        novos = [self._v(c + u * ((p.co.x - cx) * k) + v * ((p.co.y - cy) * k)) for p in me.vertices]
        for pol in me.polygons:
            f = self._f([novos[i] for i in pol.vertices], mat)
            if f:
                f.normal_update()
                if f.normal.dot(Vector(normal)) < 0:
                    f.normal_flip()
        if os.environ.get("MOTO_CONTAR"):
            print(f"CONTA   texto {txt}: {sum(len(p.vertices) - 2 for p in me.polygons)}")
        bpy.data.meshes.remove(me)

    def bvh(self):
        return BVHTree.FromBMesh(self.bm)

    def objeto(self, nome, colecao, quadro=None):
        """
        A malha vira um objeto do formato do Itens.fbx: a origem no quadro (o pivô; sem quadro, a origem do mundo),
        espelhada em X e girada 90° em X.
        """
        bm = self.bm
        if quadro is not None:
            bmesh.ops.transform(bm, matrix=quadro.inverted(), verts=bm.verts)
        ngons = [f for f in bm.faces if len(f.verts) > 4]
        if ngons:
            bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
        bm.normal_update()
        # um UV simples (projeção de caixa, 1 m = 1 unidade), para texturas de detalhe ou um bake depois
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            n = f.normal
            eixo = max(range(3), key=lambda i: abs(n[i]))
            a, b = ((2, 1), (0, 2), (0, 1))[eixo]
            for lp in f.loops:
                lp[uv].uv = (lp.vert.co[a], lp.vert.co[b])
        bmesh.ops.scale(bm, vec=(-1, 1, 1), verts=bm.verts)
        bmesh.ops.reverse_faces(bm, faces=bm.faces)   # o espelho virou as faces do avesso
        usados = sorted(self.usados)
        novo = {m: i for i, m in enumerate(usados)}
        for f in bm.faces:
            f.material_index = novo[f.material_index]
        me = bpy.data.meshes.new(nome)
        bm.to_mesh(me)
        bm.free()
        for m in usados:
            me.materials.append(material(MATERIAIS[m]))
        for p in me.polygons:
            p.use_smooth = True
        try:
            me.set_sharp_from_angle(angle=math.radians(40))
        except Exception as e:  # noqa: BLE001
            print("aviso: set_sharp_from_angle", e)
        o = bpy.data.objects.new(nome, me)
        colecao.objects.link(o)
        if quadro is None:
            o.rotation_euler = (math.radians(90), 0, 0)
        else:
            # (só giros em X: eles comutam com o espelho em X)
            o.location = para_blender(quadro.translation)
            o.rotation_euler = (Matrix.Rotation(math.radians(90), 3, "X") @ quadro.to_3x3()).to_euler()
            o["pivo_unity"] = list(quadro.translation)
        return o


def _tris(m):
    return sum(len(f.verts) - 2 for f in m.bm.faces)


def _contar(m, nome, antes):
    if os.environ.get("MOTO_CONTAR"):
        print(f"CONTA {nome}: {_tris(m) - antes}")
    return _tris(m)


def _arco(centro, ang, secao):
    """Os pontos de uma seção (x, r) num ângulo (graus, de +z para +y) em volta de um eixo em X."""
    a = math.radians(ang)
    return [Vector(centro) + V(x, r * math.sin(a), r * math.cos(a)) for x, r in secao]


def _faixa(a, b, n):
    return [a + (b - a) * i / (n - 1) for i in range(n)]


# ---------------------------------------------------------------------------------------------------- as rodas
def roda(colecao, nome, centro, meio, disco=0, polia=False):
    """
    A roda de disco "Lakester": o pneu gordo, a borda polida, a faixa escura com as pastilhas usinadas, o miolo e o cubo
    cromado. disco: o lado do disco do freio (+1 direita, -1 esquerda); polia: a polia da correia à esquerda.
    """
    m = Malha()
    o = Vector(centro)
    rb, R = 0.236, R_RODA
    h = R - rb
    w = meio
    pneu = [(-0.86 * w, rb), (-w, rb + 0.50 * h), (-0.88 * w, rb + 0.85 * h), (-0.45 * w, R), (0.45 * w, R),
            (0.88 * w, rb + 0.85 * h), (w, rb + 0.50 * h), (0.86 * w, rb)]
    m.torno("Borracha", pneu, o, X, 24)
    # a roda, um lado de cada vez (do aro para o cubo; o lado esquerdo ao contrário, para as normais saírem para fora)
    lado = [(0.74, 0.242), (0.80, 0.228), (0.60, 0.214), (0.60, 0.150), (0.64, 0.140), (0.64, 0.062), (0.80, 0.0)]
    mats = ["Aluminio", "Aluminio", "Escuro", "Aluminio", "Escuro", "Cromado"]
    direito = [(x * w, r) for x, r in lado]
    m.torno(mats, direito, o, X, 16)
    m.torno(list(reversed(mats)), [(-x, r) for x, r in reversed(direito)], o, X, 16)
    # as pastilhas usinadas na faixa escura (decalques um pouco acima dela)
    for s in (-1, 1):
        x = s * (0.60 * w + 0.0025)
        for k in range(12):
            a0 = 2 * math.pi * (k + 0.5) / 12
            da = math.radians(11.0)
            pts = [o + V(x, r * math.sin(a), r * math.cos(a)) for r, a in
                   ((0.158, a0 - da), (0.207, a0 - da * 0.9), (0.207, a0 + da * 0.9), (0.158, a0 + da))]
            m.poligono("Aluminio", pts, V(s, 0, 0))
    if disco:
        s = disco
        x0, x1 = sorted((s * (0.64 * w + 0.004), s * (0.64 * w + 0.010)))
        r_ext = 0.148 if meio > 0.1 else 0.150
        m.torno("Aluminio", [(x0, 0.088), (x0, r_ext), (x1, r_ext), (x1, 0.088)], o, X, 16, fechado=True)
    if polia:
        x0, x1 = -(0.64 * w + 0.030), -(0.64 * w + 0.004)
        m.torno(["Cromado", "Escuro", "Escuro", "Escuro"], [(x0, 0.072), (x0, 0.165), (x1, 0.165), (x1, 0.072)], o, X,
                16, fechado=True)
    return m.objeto(nome, colecao, quadro=Matrix.Translation(centro))


# ---------------------------------------------------------------------------------------------------- o corpo
SECOES_TANQUE = [  # (z, topo, base, meia largura): a gota, mais alta e larga na frente do meio
    (-0.150, 0.725, 0.700, 0.045),
    (-0.105, 0.775, 0.700, 0.100),
    (-0.020, 0.848, 0.708, 0.142),
    (0.080, 0.915, 0.718, 0.166),
    (0.180, 0.950, 0.730, 0.176),
    (0.270, 0.948, 0.745, 0.170),
    (0.330, 0.922, 0.760, 0.150),
    (0.362, 0.880, 0.778, 0.112),
    (0.376, 0.842, 0.795, 0.060),
]

SECOES_BANCO = [  # (z, topo, base, meia largura): o piloto fundo e baixo, o degrau do encosto e o garupa por cima
    (-0.105, 0.728, 0.690, 0.050),
    (-0.150, 0.708, 0.640, 0.105),
    (-0.215, 0.688, 0.612, 0.150),
    (-0.300, 0.676, 0.602, 0.178),
    (-0.400, 0.684, 0.602, 0.186),
    (-0.465, 0.712, 0.606, 0.180),
    (-0.505, 0.760, 0.610, 0.166),
    (-0.540, 0.788, 0.615, 0.150),
    (-0.640, 0.796, 0.620, 0.142),
    (-0.740, 0.792, 0.625, 0.135),
    (-0.805, 0.775, 0.630, 0.118),
    (-0.838, 0.745, 0.640, 0.075),
]

# o paralama de trás (x, r): por fora e por dentro (a chapa tem 7 mm)
PARALAMA_T = [(0.158, 0.290), (0.158, 0.352), (0.132, 0.380), (0.0, 0.392), (-0.132, 0.380), (-0.158, 0.352),
              (-0.158, 0.290), (-0.151, 0.290), (-0.151, 0.347), (-0.126, 0.373), (0.0, 0.385), (0.126, 0.373),
              (0.151, 0.347), (0.151, 0.290)]
PARALAMA_F = [(0.108, 0.270), (0.108, 0.318), (0.086, 0.352), (0.0, 0.363), (-0.086, 0.352), (-0.108, 0.318),
              (-0.108, 0.270), (-0.102, 0.270), (-0.102, 0.315), (-0.081, 0.346), (0.0, 0.357), (0.081, 0.346),
              (0.102, 0.315), (0.102, 0.270)]


def _anel_tanque(z, topo, base, hw):
    h = topo - base
    meio = [(0.0, topo), (0.42 * hw, topo - 0.06 * h), (0.78 * hw, topo - 0.22 * h), (0.97 * hw, topo - 0.45 * h),
            (hw, base + 0.30 * h), (0.90 * hw, base + 0.06 * h), (0.62 * hw, base), (0.30 * hw, base + 0.10 * h),
            (0.0, base + 0.14 * h)]
    return [V(x, y, z) for x, y in meio + [(-x, y) for x, y in reversed(meio[1:-1])]]


def _anel_banco(z, topo, base, hw):
    alto = max(base + 0.02, topo - 0.075)
    meio = [(0.0, topo), (0.50 * hw, topo - 0.010), (0.88 * hw, topo - 0.035), (hw, alto), (0.92 * hw, base + 0.010),
            (0.50 * hw, base), (0.0, base)]
    return [V(x, y, z) for x, y in meio + [(-x, y) for x, y in reversed(meio[1:-1])]]


def _tanque(m):
    """O tanque gota, o console cromado com o velocímetro, a tampa e os emblemas de asas dos dois lados."""
    m.casca("Pintura", [_anel_tanque(*s) for s in SECOES_TANQUE])
    n = _contar(m, " tanque (casca)", 0)
    bvh = m.bvh()

    def topo(z, x=0.0):
        hit = bvh.ray_cast(V(x, 2.0, z), V(0, -1, 0))[0]
        return hit.y if hit else 0.9

    # o console no meio, por cima
    aneis = []
    for z in (-0.075, 0.0, 0.10, 0.20, 0.27, 0.305):
        t = topo(z)
        aneis.append([V(-0.034, t - 0.006, z), V(0.034, t - 0.006, z), V(0.037, t + 0.004, z), V(0.018, t + 0.009, z),
                      V(-0.018, t + 0.009, z), V(-0.037, t + 0.004, z)])
    m.casca("Cromado", aneis)
    # o velocímetro, virado para o piloto
    zc = 0.03
    eixo = V(0, math.cos(math.radians(40)), -math.sin(math.radians(40)))
    cv = V(0, topo(zc) + 0.012, zc)
    m.torno(["Cromado", "Cromado", "Cromado", "Cromado", "Preto"],
            [(-0.02, 0.040), (0.0, 0.048), (0.012, 0.048), (0.016, 0.042), (0.012, 0.038), (0.011, 0.0)], cv, eixo, 10)
    face = cv + eixo * 0.0125
    lado = eixo.cross(X).normalized()
    m.ripa("Lente", face + X * -0.004 + lado * 0.004, face + X * 0.026 + lado * 0.016, 0.003, 0.002, ref=eixo)
    m.disco("Aluminio", face + eixo * 0.0005, X, lado, 0.006, 8, eixo)
    # a tampa do combustível (à direita do console)
    zt = 0.20
    m.torno("Cromado", [(-0.006, 0.029), (0.006, 0.029), (0.011, 0.022), (0.013, 0.0)],
            V(0.066, topo(zt, 0.066) - 0.002, zt), V(0.28, 1, 0), 8)
    n = _contar(m, " console, velocimetro, tampa", n)
    # os emblemas de asas (o medalhão com o escudo e as três penas para cada lado)
    for s in (-1, 1):
        _emblema(m, bvh, s, 0.20, 0.815)
    n = _contar(m, " emblemas", n)


def _emblema(m, bvh, s, z0, y0):
    def proj(dz, dy, alto):
        hit = bvh.ray_cast(V(s * 0.6, y0 + dy, z0 + dz), V(-s, 0, 0))[0]
        if hit is None:
            hit = V(s * 0.17, y0 + dy, z0 + dz)
        return hit + V(s, 0, 0) * alto

    n = V(s, 0, 0)

    def circ(r, lados, alto, mat):
        m.poligono(mat, [proj(r * math.cos(2 * math.pi * k / lados), r * math.sin(2 * math.pi * k / lados), alto)
                         for k in range(lados)], n)

    circ(0.034, 16, 0.004, "Cromado")
    circ(0.027, 16, 0.0052, "Preto")
    escudo = [(-0.017, 0.015), (0.017, 0.015), (0.017, -0.001), (0.0, -0.020), (-0.017, -0.001)]
    m.poligono("Cromado", [proj(a, b, 0.0064) for a, b in escudo], n)
    barra = [(-0.015, -0.004), (0.015, -0.004), (0.015, 0.007), (-0.015, 0.007)]
    m.poligono("Preto", [proj(a, b, 0.0076) for a, b in barra], n)
    for sentido in (-1, 1):
        for comp, dy in ((0.072, 0.011), (0.060, 0.0), (0.047, -0.011)):
            pena = [(0.028, dy - 0.0042), (0.028 + 0.6 * comp, dy - 0.0036), (0.028 + comp, dy + 0.0030),
                    (0.028 + 0.9 * comp, dy + 0.0052), (0.028, dy + 0.0045)]
            m.poligono("Cromado", [proj(sentido * a, b, 0.004) for a, b in pena], n)


def _quadro(m):
    """O quadro preto: a coluna da direção, a espinha sob o tanque, os dois berços e a balança."""
    a, b = no_eixo(0.775), no_eixo(0.775) + U * 0.22
    m.torno("Preto", [(0, 0), (0, 0.038), (0.22, 0.038), (0.22, 0.0)], a, U, 10)
    m.cano("Preto", [(0, 0.92, 0.37), (0, 0.86, 0.22), (0, 0.76, 0.02), (0, 0.71, -0.13)], 0.028, 6)
    for s in (-1, 1):
        pts = [(0.03, 0.80, 0.445), (0.055, 0.62, 0.425), (0.072, 0.40, 0.40), (0.082, 0.21, 0.37), (0.088, 0.145, 0.30),
               (0.092, 0.135, 0.15), (0.095, 0.135, -0.10), (0.10, 0.15, -0.24), (0.11, 0.21, -0.36),
               (0.12, 0.33, -0.43), (0.125, 0.47, -0.45), (0.125, 0.575, -0.52), (0.125, 0.60, -0.62)]
        m.cano("Preto", [(s * x, y, z) for x, y, z in pts], 0.023, 6, fase=math.pi / 6)
        # a balança (o braço de baixo, o de cima e a placa do eixo) e a tampa do eixo
        m.ripa("Preto", (s * 0.158, 0.395, -0.30), (s * 0.162, 0.325, -0.80), 0.035, 0.06)
        m.ripa("Preto", (s * 0.155, 0.35, -0.775), (s * 0.142, 0.555, -0.47), 0.03, 0.04)
        m.caixa("Preto", (0.03, 0.075, 0.11), (s * 0.165, R_RODA, -EIXO))
        m.torno("Cromado", [(0.0, 0.022), (0.012, 0.022), (0.016, 0.0)], (s * 0.18, R_RODA, -EIXO), V(s, 0, 0), 8)
        # as tampas de lado, embaixo do banco
        m.caixa("Preto", (0.025, 0.13, 0.28), (s * 0.135, 0.535, -0.35), chanfro=0.008)
    # o miolo embaixo do banco (bateria, amortecedor) e o radiador de óleo na frente, entre os berços
    m.caixa("Preto", (0.22, 0.12, 0.30), (0, 0.54, -0.35))
    m.caixa("Preto", (0.15, 0.24, 0.045), (0, 0.50, 0.405), giro=(-12, 0, 0), chanfro=0.008)
    frente = V(0, math.sin(math.radians(12)), math.cos(math.radians(12)))
    for k in range(6):
        cy = 0.41 + k * 0.035
        c = V(0, cy, 0.405 - (cy - 0.50) * math.tan(math.radians(12))) + frente * 0.0235
        m.poligono("Aluminio", [c + V(-0.062, -0.004, 0), c + V(0.062, -0.004, 0), c + V(0.062, 0.004, 0),
                                c + V(-0.062, 0.004, 0)], frente)


def _motor(m):
    """O Milwaukee-Eight 114: o V de 45° preto com as aletas usinadas, os cabeçotes, as tampas cromadas, as varetas, o
    filtro de ar oval "114" à direita, a tampa do comando e, à esquerda, a tampa da primária com o derby."""
    c = C_MOTOR
    m.caixa("Preto", (0.23, 0.20, 0.40), c + V(0, -0.04, -0.02), chanfro=0.035)
    m.caixa("Preto", (0.20, 0.07, 0.42), V(0, 0.205, 0.02))
    for sinal in (1, -1):   # 1: o cilindro da frente
        ang = sinal * MEIO_V
        d = V(0, math.cos(ang), math.sin(ang))
        m.cano("Escuro", [c + d * 0.10, c + d * 0.24], (0.064, 0.060), 8, tampas=False, n0=X, fase=math.pi / 8)
        for s in (0.135, 0.165, 0.195, 0.225):
            m.cano("Aluminio", [c + d * (s - 0.0045), c + d * (s + 0.0045)], (0.094, 0.088), 8, n0=X,
                   fase=math.pi / 8, mat_tampa="Escuro")
        m.cano("Escuro", [c + d * 0.24, c + d * 0.30], (0.092, 0.084), 8, n0=X, fase=math.pi / 8)
        m.cano("Aluminio", [c + d * 0.263, c + d * 0.273], (0.102, 0.096), 8, n0=X, fase=math.pi / 8,
               mat_tampa="Escuro")
        giro = (math.degrees(ang), 0, 0)
        m.caixa("Cromado", (0.165, 0.042, 0.14), c + d * 0.321, giro=giro, chanfro=0.013)
        m.caixa("Cromado", (0.115, 0.022, 0.095), c + d * 0.35, giro=giro)
        # as varetas (no V, do lado direito)
        p = V(0, abs(math.sin(ang)), -sinal * math.cos(ang))
        for x in (0.055, 0.08):
            m.cano("Cromado", [V(x, 0.43, c.z + sinal * 0.02), c + d * 0.235 + p * 0.045 + V(x, 0, 0)], 0.0075, 6,
                   tampas=False)
    # o filtro de ar oval: o fundo escuro, a tampa cromada com o miolo preto e o "114"
    cf = V(0.11, 0.615, 0.08)
    m.torno(["Escuro", "Escuro"], [(0.0, 1.0), (0.035, 1.0), (0.035, 0.0)], cf, X, 14, n0=Y, escala=(0.062, 0.115))
    m.torno(["Cromado", "Cromado", "Cromado", "Cromado", "Preto"],
            [(0.035, 1.0), (0.074, 1.0), (0.086, 0.88), (0.0875, 0.78), (0.085, 0.72), (0.085, 0.0)], cf, X, 16, n0=Y,
            escala=(0.056, 0.106))
    # (o "114" em cinza-claro, o material da placa: em cromado ou alumínio ele refletia o estúdio escuro e sumia no
    # miolo preto)
    m.texto("Placa", "114", V(0.1968, 0.62, 0.08), Z, Y, 0.040, X)
    for dz in (-0.068, 0.068):
        m.disco("Cromado", V(0.1962, 0.615, 0.08 + dz), Z, Y, 0.0055, 6, X)
    # a tampa do comando (direita) com o emblema
    m.torno("Cromado", [(0.0, 0.068), (0.022, 0.066), (0.034, 0.05), (0.037, 0.0)], V(0.115, 0.372, 0.095), X, 12)
    m.disco("Preto", V(0.1525, 0.372, 0.095), Z, Y, 0.030, 12, X)
    escudo = [(-0.013, 0.012), (0.013, 0.012), (0.013, -0.001), (0.0, -0.016), (-0.013, -0.001)]
    m.poligono("Aluminio", [V(0.1532, 0.372 + b, 0.095 + a) for a, b in escudo], X)
    # o câmbio, atrás
    m.caixa("Preto", (0.19, 0.20, 0.22), V(0, 0.36, -0.27), chanfro=0.02)
    m.caixa("Cromado", (0.10, 0.02, 0.12), V(0, 0.465, -0.27))
    m.caixa("Cromado", (0.02, 0.05, 0.12), V(0.10, 0.33, -0.21))
    # a primária (esquerda): o contorno dos dois círculos (o virabrequim e o câmbio), em dois degraus
    def contorno(esc):
        pts = []
        for zc, yc, r in ((c.z, c.y, 0.115 * esc), (-0.29, 0.355, 0.09 * esc)):
            for k in range(11):
                a = 2 * math.pi * k / 11
                pts.append((zc + r * math.cos(a), yc + r * math.sin(a)))
        return _casco(pts)
    m.perfil("Cromado", contorno(1.0), -0.115, -0.165)
    m.perfil("Cromado", contorno(0.88), -0.165, -0.178)
    m.torno("Cromado", [(0.0, 0.07), (0.008, 0.07), (0.014, 0.055), (0.016, 0.0)], V(-0.178, 0.355, -0.29), -X, 12)
    m.torno("Cromado", [(0.0, 0.045), (0.006, 0.045), (0.010, 0.0)], V(-0.178, 0.37, 0.08), -X, 8)


def _casco(pts):
    """O contorno convexo (z, y), no sentido anti-horário."""
    pts = sorted(set(pts))

    def cruz(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    baixo, cima = [], []
    for p in pts:
        while len(baixo) >= 2 and cruz(baixo[-2], baixo[-1], p) <= 0:
            baixo.pop()
        baixo.append(p)
    for p in reversed(pts):
        while len(cima) >= 2 and cruz(cima[-2], cima[-1], p) <= 0:
            cima.pop()
        cima.append(p)
    return baixo[:-1] + cima[:-1]


def _escapamento(m):
    """O escapamento duplo "shotgun" cromado, à direita: o de cima sai do cilindro de trás, o de baixo do da frente."""
    cima = [(0.075, 0.57, -0.075), (0.13, 0.53, -0.075), (0.18, 0.47, -0.05), (0.205, 0.43, -0.012),
            (0.222, 0.405, 0.0), (0.228, 0.39, -0.04), (0.228, 0.386, -0.10), (0.228, 0.385, -0.40)]
    m.cano("Cromado", cima, 0.026, 8, tampas=False)
    baixo = [(0.06, 0.585, 0.235), (0.10, 0.56, 0.29), (0.145, 0.50, 0.315), (0.175, 0.42, 0.29), (0.195, 0.345, 0.22),
             (0.205, 0.295, 0.13), (0.21, 0.272, 0.03), (0.21, 0.267, -0.08), (0.21, 0.266, -0.23)]
    m.cano("Cromado", baixo, 0.026, 8, tampas=False)
    mats = ["Cromado"] * 5 + ["Preto"]
    for (x, y, z0), comp in (((0.228, 0.385, -0.39), 0.445), ((0.21, 0.266, -0.22), 0.47)):
        perfil = [(0.0, 0.0), (0.0, 0.026), (0.045, 0.043), (comp, 0.043), (comp + 0.022, 0.037),
                  (comp + 0.033, 0.025), (comp + 0.023, 0.0)]
        m.torno(mats, perfil, V(x, y, z0), -Z, 12)
    # a braçadeira entre os dois
    m.caixa("Preto", (0.014, 0.08, 0.03), V(0.219, 0.325, -0.60))


def _pedaleiras(m):
    """As plataformas do piloto, o pedal do freio (direita), o câmbio de calcanhar e ponta (esquerda) e as pedaleiras
    do garupa."""
    for s in (-1, 1):
        x = s * 0.31
        m.caixa("Cromado", (0.125, 0.018, 0.30), (x, 0.29, 0.205), giro=(-4, 0, 0), chanfro=0.006)
        m.caixa("Borracha", (0.105, 0.014, 0.27), (x, 0.304, 0.205), giro=(-4, 0, 0))
        for z in (0.12, 0.29):
            m.ripa("Preto", (s * 0.255, 0.283, z), (s * 0.095, 0.15, z - 0.02), 0.03, 0.025)
        # o garupa: o suporte cromado e a pedaleira de borracha com a ponta cromada
        m.ripa("Cromado", (s * 0.13, 0.578, -0.37), (s * 0.205, 0.565, -0.40), 0.02, 0.025, ref=Y)
        m.cano("Borracha", [(s * 0.20, 0.562, -0.40), (s * 0.265, 0.562, -0.40)], 0.014, 6, tampas=False)
        m.cano("Cromado", [(s * 0.265, 0.562, -0.40), (s * 0.28, 0.562, -0.40)], 0.0145, 6)
    m.ripa("Cromado", (0.235, 0.26, 0.31), (0.26, 0.335, 0.43), 0.012, 0.012)
    m.caixa("Borracha", (0.055, 0.02, 0.04), (0.27, 0.34, 0.435))
    m.ripa("Cromado", (-0.11, 0.30, 0.27), (-0.26, 0.30, 0.27), 0.014, 0.014, ref=Y)
    m.ripa("Cromado", (-0.26, 0.30, 0.27), (-0.27, 0.345, 0.41), 0.012, 0.012)
    m.cano("Borracha", [(-0.255, 0.35, 0.41), (-0.31, 0.35, 0.41)], 0.012, 6)
    m.ripa("Cromado", (-0.26, 0.30, 0.27), (-0.27, 0.34, 0.07), 0.012, 0.012)
    m.caixa("Borracha", (0.05, 0.018, 0.035), (-0.28, 0.345, 0.07))


def _traseira(m):
    """O paralama de trás com as hastes cromadas e os refletores, os piscas, a placa com a luz e o sissy bar com o encosto
    do garupa."""
    m.casca("Pintura", [_arco(TRAS, a, PARALAMA_T) for a in _faixa(40.0, 145.0, 8)])
    for s in (-1, 1):
        m.ripa("Cromado", (s * 0.168, 0.645, -1.02), (s * 0.168, 0.565, -0.40), 0.010, 0.035)
        m.poligono("Lente", [V(s * 0.1595, 0.622, -0.80), V(s * 0.1595, 0.622, -0.74), V(s * 0.1595, 0.635, -0.74),
                             V(s * 0.1595, 0.635, -0.80)], V(s, 0, 0))
        # o pisca: a haste e a bala cromada com a lente laranja
        m.ripa("Cromado", (s * 0.166, 0.648, -1.0), (s * 0.20, 0.655, -1.0), 0.012, 0.012, ref=Y)
        m.torno(["Cromado"] * 5 + ["Lente"],
                [(0.0, 0.0), (0.0, 0.012), (0.012, 0.024), (0.045, 0.027), (0.052, 0.025), (0.055, 0.020),
                 (0.068, 0.0)], V(s * 0.205, 0.655, -0.985), -Z, 7)
        # o sissy bar: a placa de fixação
        m.caixa("Cromado", (0.008, 0.14, 0.09), (s * 0.17, 0.72, -0.89), giro=(-12, 0, 0))
    sissy = [(0.168, 0.615, -0.84), (0.16, 0.72, -0.87), (0.148, 0.88, -0.905), (0.135, 1.02, -0.93),
             (0.10, 1.085, -0.94), (0.0, 1.10, -0.945)]
    sissy = sissy + [(-x, y, z) for x, y, z in reversed(sissy[:-1])]
    m.cano("Cromado", sissy, 0.011, 6)
    m.caixa("Couro", (0.30, 0.20, 0.075), (0, 0.99, -0.876), giro=(-10, 0, 0), chanfro=0.025)
    m.caixa("Preto", (0.24, 0.15, 0.02), (0, 0.99, -0.92), giro=(-10, 0, 0))
    # a placa (200 x 170 mm, deitada 30°) com a moldura, o suporte e a luz em cima
    cp = V(0, 0.64, -1.17)
    giro = (30, 0, 0)
    tras = V(0, math.sin(math.radians(30)), -math.cos(math.radians(30)))
    cima = V(0, math.cos(math.radians(30)), math.sin(math.radians(30)))
    m.caixa("Placa", (0.20, 0.17, 0.004), cp, giro=giro)
    m.caixa("Preto", (0.214, 0.184, 0.008), cp - tras * 0.005, giro=giro)
    m.texto("Preto", "ZCC", cp + tras * 0.0025 + cima * 0.037, X, cima, 0.048, tras)
    m.texto("Preto", "2F19", cp + tras * 0.0025 - cima * 0.035, X, cima, 0.050, tras)
    m.ripa("Preto", cp - tras * 0.01 - cima * 0.02, V(0, 0.575, -1.10), 0.05, 0.02, ref=X)
    m.caixa("Cromado", (0.075, 0.03, 0.03), cp + cima * 0.10 - tras * 0.01, giro=giro)
    m.caixa("Lanterna", (0.064, 0.020, 0.012), cp + cima * 0.10 + tras * 0.008, giro=giro)
    # a pinça do freio de trás (direita, embaixo e na frente do eixo) e a correia (esquerda)
    a = math.radians(-40)
    pc = TRAS + V(0.096, 0.12 * math.sin(a), 0.12 * math.cos(a))
    m.caixa("Preto", (0.034, 0.05, 0.085), pc, giro=(-(-40 + 90), 0, 0))
    m.ripa("Preto", pc + V(0.01, 0, 0), (0.16, 0.30, -0.70), 0.012, 0.03)
    m.ripa("Borracha", (-0.094, 0.403, -0.29), (-0.094, 0.488, -EIXO), 0.028, 0.008)
    m.ripa("Borracha", (-0.094, 0.317, -0.29), (-0.094, 0.162, -EIXO), 0.028, 0.008)


def corpo(colecao):
    m = Malha()
    n = _contar(m, "inicio", 0)
    _tanque(m)
    n = _contar(m, "tanque", n)
    m.casca("Couro", [_anel_banco(*s) for s in SECOES_BANCO])
    n = _contar(m, "banco", n)
    # a altura do banco: o ponto mais baixo do assento do piloto, no meio
    bvh = m.bvh()
    alturas = []
    for i in range(41):
        z = -0.20 - 0.25 * i / 40
        hit = bvh.ray_cast(V(0, 2.0, z), V(0, -1, 0))[0]
        if hit:
            alturas.append((hit.y, z))
    banco = min(alturas)
    _quadro(m)
    n = _contar(m, "quadro", n)
    _motor(m)
    n = _contar(m, "motor", n)
    _escapamento(m)
    n = _contar(m, "escapamento", n)
    _pedaleiras(m)
    n = _contar(m, "pedaleiras", n)
    _traseira(m)
    n = _contar(m, "traseira", n)
    return m.objeto("Moto_Corpo", colecao), {"altura_do_banco": banco[0], "z_do_ponto_mais_baixo": banco[1]}


# ---------------------------------------------------------------------------------------------------- a direção
def direcao(colecao):
    m = Malha()
    rot = Matrix.Rotation(-RAKE, 4, "X")

    def L(x, y, z):
        return QUADRO_DIRECAO @ V(x, y, z)

    for s in (-1, 1):
        x = s * X_GARFO
        # a bengala: a botina cromada embaixo, o tubo grosso e o tubo fino dentro da carenagem, com a tampa em cima
        m.torno("Cromado", [(-0.05, 0.0), (-0.035, 0.036), (0.03, 0.044), (0.37, 0.041), (0.38, 0.033)],
                garfo(0, x), U, 10)
        m.torno("Cromado", [(0.37, 0.036), (0.53, 0.036), (0.535, 0.028), (0.835, 0.028), (0.845, 0.0)],
                garfo(0, x), U, 10)
        m.torno("Cromado", [(0.0, 0.022), (0.008, 0.022), (0.012, 0.0)], FRENTE + V(s * (X_GARFO + 0.044), 0, 0),
                V(s, 0, 0), 8)
        # o refletor laranja do lado de fora da botina
        xr = s * (X_GARFO + 0.0425)
        m.poligono("Lente", [garfo(0.13, xr) + FW * 0.008, garfo(0.13, xr) - FW * 0.008, garfo(0.24, xr) - FW * 0.008,
                             garfo(0.24, xr) + FW * 0.008], V(s, 0, 0))
    n = _contar(m, "garfo", 0)
    # as mesas e a carenagem do farol (uma cápsula cromada de bengala a bengala, atrás do farol)
    m.caixa("Preto", (2 * X_GARFO + 0.09, 0.035, 0.12), L(0, -0.283, 0.035), rot=rot)
    m.caixa("Cromado", (2 * X_GARFO + 0.08, 0.03, 0.12), L(0, -0.002, 0.035), rot=rot, chanfro=0.01)
    m.torno("Cromado", [(-0.19, 0.0), (-0.185, 0.6), (-0.165, 0.92), (-0.14, 1.0), (0.14, 1.0), (0.165, 0.92),
                        (0.185, 0.6), (0.19, 0.0)], L(0, -0.165, 0.07), X, 10, n0=U, escala=(0.062, 0.058))
    n = _contar(m, "mesas e carenagem", n)
    # o farol redondo de LED, com o anel de luz
    hl = V(0, 0.885, 0.60)
    m.torno(["Cromado"] * 5 + ["Farol", "Farol"],
            [(-0.075, 0.0), (-0.07, 0.05), (-0.035, 0.092), (0.03, 0.106), (0.038, 0.098), (0.036, 0.089),
             (0.047, 0.05), (0.052, 0.0)], hl, Z, 16)
    m.anel("Cromado", hl + Z * 0.0505, X, Y, 0.047, 0.057, 16, Z)
    n = _contar(m, "farol", n)
    # o guidão largo, os punhos, os comandos, os manetes e os retrovisores redondos
    lado = [(0.44, 1.142, 0.095), (0.29, 1.12, 0.185), (0.17, 1.098, 0.275), (0.06, 1.088, 0.322)]
    guidao = [(-x, y, z) for x, y, z in lado] + list(reversed(lado))
    m.cano("Cromado", guidao, 0.0127, 8)
    n = _contar(m, "guidao (tubo)", n)
    m.caixa("Cromado", (0.17, 0.035, 0.05), (0, 1.09, 0.323))
    for s in (-1, 1):
        m.ripa("Cromado", (s * 0.06, 1.0, 0.355), (s * 0.06, 1.088, 0.325), 0.035, 0.035)
        a, b = V(s * 0.29, 1.12, 0.185), V(s * 0.44, 1.142, 0.095)
        d = (b - a).normalized()
        m.cano("Borracha", [a + (b - a) * 0.22, b], 0.019, 8)
        m.cano("Cromado", [b, b + d * 0.012], 0.016, 8)
        m.ripa("Preto", a + (b - a) * 0.06, a + (b - a) * 0.2, 0.05, 0.045, ref=Y)
        m.caixa("Cromado", (0.05, 0.035, 0.04), (s * 0.255, 1.13, 0.215))
        m.ripa("Cromado", (s * 0.27, 1.125, 0.237), (s * 0.42, 1.13, 0.167), 0.016, 0.009, ref=Y)
        # o retrovisor
        m.cano("Cromado", [(s * 0.255, 1.135, 0.225), (s * 0.27, 1.20, 0.215), (s * 0.29, 1.245, 0.20)], 0.006, 6)
        m.torno(["Aluminio", "Cromado", "Cromado"], [(-0.010, 0.0), (-0.010, 0.048), (0.006, 0.051), (0.016, 0.0)],
                V(s * 0.30, 1.275, 0.195), Z, 10)
        # o pisca da frente (a bala cromada com a lente laranja), preso no guidão
        m.ripa("Cromado", (s * 0.15, 1.088, 0.322), (s * 0.17, 1.058, 0.335), 0.012, 0.012)
        m.torno(["Cromado"] * 4 + ["Lente"],
                [(-0.035, 0.0), (-0.035, 0.010), (-0.02, 0.022), (0.015, 0.025), (0.022, 0.022), (0.034, 0.0)],
                V(s * 0.17, 1.055, 0.365), Z, 7)
    n = _contar(m, "punhos, manetes, retrovisores, piscas", n)
    # o paralama da frente (cheio)
    m.casca("Pintura", [_arco(FRENTE, a, PARALAMA_F) for a in _faixa(50.0, 168.0, 9)])
    n = _contar(m, "paralama", n)
    # a pinça do freio (esquerda, atrás da bengala)
    a = 125.0
    pc = FRENTE + V(-0.084, 0.12 * math.sin(math.radians(a)), 0.12 * math.cos(math.radians(a)))
    m.caixa("Preto", (0.032, 0.05, 0.09), pc, giro=(-(a + 90), 0, 0))
    m.ripa("Preto", pc + V(-0.01, 0.01, 0), garfo(0.17, -X_GARFO + 0.03), 0.012, 0.03)
    return m.objeto("Moto_Direcao", colecao, quadro=QUADRO_DIRECAO)


def descanso(colecao):
    m = Malha()
    pe = V(-0.29, 0.035, 0.17)
    m.ripa("Preto", DESCANSO, pe, 0.022, 0.018)
    m.caixa("Preto", (0.05, 0.012, 0.04), pe + V(-0.008, -0.008, 0.004))
    return m.objeto("Moto_Descanso", colecao, quadro=Matrix.Translation(DESCANSO))


# ---------------------------------------------------------------------------------------------------- a montagem
NOMES = ["Moto_Corpo", "Moto_Direcao", "Moto_RodaFrente", "Moto_RodaTras", "Moto_Descanso"]


def limpar():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)
    cena = bpy.context.scene
    cena.name = "Moto"
    cena.unit_settings.system = "METRIC"
    return cena


def construir():
    cena = limpar()
    col = bpy.data.collections.new("Moto")
    cena.collection.children.link(col)
    corpo_o, medidas = corpo(col)
    partes = [corpo_o, direcao(col), roda(col, "Moto_RodaFrente", FRENTE, MEIO_PNEU_F, disco=-1),
              roda(col, "Moto_RodaTras", TRAS, MEIO_PNEU_T, disco=1, polia=True), descanso(col)]
    pontos = bpy.data.collections.new("Moto_Pontos")
    cena.collection.children.link(pontos)
    for nome, p in list(PONTOS.items()) + [("Pivo_Direcao", tuple(PIVO)), ("Eixo_Frente", tuple(FRENTE)),
                                           ("Eixo_Tras", tuple(TRAS))]:
        e = bpy.data.objects.new(nome, None)
        e.empty_display_type = "PLAIN_AXES"
        e.empty_display_size = 0.06
        e.location = para_blender(p)
        e["unity"] = list(p)
        pontos.objects.link(e)
    return partes, medidas


def relatorio(partes, medidas):
    dg = bpy.context.evaluated_depsgraph_get()
    tri, todos = {}, []
    for o in partes:
        me = o.data
        tri[o.name] = sum(len(p.vertices) - 2 for p in me.polygons)
        mw = o.matrix_world
        todos += [mw @ v.co for v in me.vertices]
    xs = [p.x for p in todos]
    ys = [p.y for p in todos]
    zs = [p.z for p in todos]
    loc = QUADRO_DIRECAO.inverted() @ FRENTE
    return {
        "triangulos": tri,
        "triangulos_total": sum(tri.values()),
        "materiais": {o.name: [m.name for m in o.data.materials] for o in partes},
        "comprimento": max(ys) - min(ys),
        "largura": max(xs) - min(xs),
        "altura": max(zs) - min(zs),
        "entre_eixos": 2 * EIXO,
        "altura_do_banco": medidas["altura_do_banco"],
        "raio_das_rodas": R_RODA,
        "rake_graus": math.degrees(RAKE),
        "pivo_da_direcao_unity": list(PIVO),
        "roda_da_frente_nos_eixos_da_direcao": list(loc),
        "descanso_unity": list(DESCANSO),
        "pontos_unity": PONTOS,
        "versao_blender": bpy.app.version_string,
    }


def exportar(caminho, partes):
    os.makedirs(os.path.dirname(caminho), exist_ok=True)
    guardado = {}
    for o in bpy.context.scene.objects:
        o.select_set(o in partes)
    for o in partes:
        # cada peça vai na origem (a Unity monta pelas medidas), como no Carro.fbx
        guardado[o.name] = (tuple(o.location), tuple(o.rotation_euler))
        o.location = (0, 0, 0)
        o.rotation_euler = (math.radians(90), 0, 0)
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="OFF")
    for nome, (loc, rot) in guardado.items():
        o = bpy.data.objects[nome]
        o.location = loc
        o.rotation_euler = rot
    bpy.context.view_layer.update()   # (sem isto, as fotos enquadravam as peças ainda na origem)


# ---------------------------------------------------------------------------------------------------- as fotos
def _pontos_da_moto(partes):
    pts = []
    for o in partes:
        mw = o.matrix_world
        vs = o.data.vertices
        passo = max(1, len(vs) // 400)
        pts += [mw @ vs[i].co for i in range(0, len(vs), passo)]
    return pts


def _palco(cena, cycles):
    for o in list(cena.objects):
        if o.name.startswith(("Foto_", "Luz_", "Chao")):
            bpy.data.objects.remove(o, do_unlink=True)
    col = bpy.data.collections.get("Fotos") or bpy.data.collections.new("Fotos")
    if col.name not in cena.collection.children:
        cena.collection.children.link(col)
    r = cena.render
    if cycles:
        r.engine = "CYCLES"
        cena.cycles.device = "CPU"
        cena.cycles.samples = int(os.environ.get("MOTO_AMOSTRAS", "128"))
        cena.cycles.use_adaptive_sampling = True
        cena.cycles.use_denoising = True
        try:
            cena.cycles.denoiser = "OPENIMAGEDENOISE"
        except TypeError:
            pass
        r.film_transparent = True
        # o mundo: o HDRI de estúdio que vem com o Blender (os reflexos do cromado)
        w = bpy.data.worlds.get("Estudio") or bpy.data.worlds.new("Estudio")
        cena.world = w
        try:
            w.use_nodes = True
        except Exception:
            pass
        nt = w.node_tree
        nt.nodes.clear()
        tex = nt.nodes.new("ShaderNodeTexEnvironment")
        hdr = os.path.join(bpy.utils.system_resource("DATAFILES"), "studiolights", "world", "studio.exr")
        tex.image = bpy.data.images.load(hdr, check_existing=True)
        fundo = nt.nodes.new("ShaderNodeBackground")
        fundo.inputs["Strength"].default_value = float(os.environ.get("MOTO_MUNDO", "0.15"))
        sai = nt.nodes.new("ShaderNodeOutputWorld")
        nt.links.new(tex.outputs["Color"], fundo.inputs["Color"])
        nt.links.new(fundo.outputs["Background"], sai.inputs["Surface"])
        # as luzes de área (Blender: a frente da moto em -Y, a direita em -X)
        luz = float(os.environ.get("MOTO_LUZ", "0.32"))
        for nome, loc, energia, tam in (("Luz_Chave", (-3.2, -2.6, 3.6), 900, 5.0),
                                        ("Luz_Enche", (3.6, -1.2, 2.2), 380, 6.0),
                                        ("Luz_Recorte", (1.2, 3.8, 2.8), 800, 4.0),
                                        ("Luz_Cima", (0.0, 0.0, 4.6), 250, 6.0)):
            ld = bpy.data.lights.new(nome, "AREA")
            ld.energy = energia * luz
            ld.size = tam
            lo = bpy.data.objects.new(nome, ld)
            lo.location = loc
            d = (V(0, 0, 0.5) - V(*loc)).normalized()
            lo.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
            col.objects.link(lo)
        # o chão que só pega a sombra
        me = bpy.data.meshes.new("Chao")
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=12.0)
        bm.to_mesh(me)
        bm.free()
        chao = bpy.data.objects.new("Chao", me)
        chao.is_shadow_catcher = True
        col.objects.link(chao)
        cena.view_settings.view_transform = "AgX"
        for look in ("AgX - Medium High Contrast", "Medium High Contrast", "None"):
            try:
                cena.view_settings.look = look
                break
            except TypeError:
                continue
        print("COR", cena.view_settings.view_transform, cena.view_settings.look)
    else:
        r.engine = "BLENDER_WORKBENCH"
        r.film_transparent = False
        s = cena.display.shading
        s.light = "STUDIO"
        s.color_type = "MATERIAL"
        s.show_cavity = True
        s.cavity_type = "BOTH"
        s.show_object_outline = True
        s.object_outline_color = (0.05, 0.05, 0.05)
        s.background_type = "VIEWPORT"
        s.background_color = (0.78, 0.78, 0.76)
        s.show_specular_highlight = True
        cena.display.render_aa = "8"
        cena.view_settings.view_transform = "Standard"
    r.resolution_percentage = int(os.environ.get("MOTO_PCT", "100"))
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGBA" if cycles else "RGB"
    cam = bpy.data.objects.new("Foto_Camera", bpy.data.cameras.new("Foto_Camera"))
    col.objects.link(cam)
    cena.camera = cam
    cam.data.clip_start = 0.02
    cam.data.sensor_fit = "AUTO"
    return cam


def _enquadrar(cena, cam, direcao, pontos, lente, margem=0.86):
    """Põe a câmera na direção dada, a olhar para os pontos, longe o bastante para caberem (com a margem)."""
    d = V(*direcao).normalized()
    cam.data.type = "PERSP"
    cam.data.lens = lente
    rx, ry = cena.render.resolution_x, cena.render.resolution_y
    tx = 18.0 / lente if rx >= ry else 18.0 / lente * rx / ry
    ty = tx * ry / rx
    alvo = sum(pontos, Vector()) / len(pontos)
    dist = 4.0
    rot = (-d).to_track_quat("-Z", "Y").to_matrix()
    direita, cima = rot.col[0], rot.col[1]
    for _ in range(12):
        pos = alvo + d * dist
        rt = rot.transposed()
        xs, ys = [], []
        for p in pontos:
            q = rt @ (p - pos)
            prof = -q.z
            xs.append(q.x / prof / tx)
            ys.append(q.y / prof / ty)
        cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
        ext = max((max(xs) - min(xs)) / 2, (max(ys) - min(ys)) / 2)
        alvo = alvo + direita * (cx * dist * tx) + cima * (cy * dist * ty)
        dist = dist * ext / margem
    cam.location = alvo + d * dist
    cam.rotation_euler = rot.to_euler()


VISTAS = [
    # (nome, direção da câmera no Blender (a frente da moto em -Y, a direita em -X), lente, só o motor)
    ("lateral_direita", (-1.0, 0.0, 0.07), 85, False),
    ("lateral_esquerda", (1.0, 0.0, 0.07), 85, False),
    ("tres_quartos_frente", (-0.78, -1.0, 0.42), 55, False),
    ("tres_quartos_tras", (0.80, 1.0, 0.45), 55, False),
    ("frente", (0.0, -1.0, 0.10), 85, False),
    ("motor_close", (-1.0, -0.22, 0.16), 70, True),
]


def fotos(pasta, partes, cycles=True, vistas=None):
    cena = bpy.context.scene
    cam = _palco(cena, cycles)
    r = cena.render
    os.makedirs(pasta, exist_ok=True)
    bpy.context.view_layer.update()
    pts = _pontos_da_moto(partes)
    # a caixa do motor (eixos da Unity -> Blender) para o close
    motor = [V(*para_blender((x, y, z))) for x in (-0.05, 0.25) for y in (0.17, 0.74) for z in (-0.42, 0.36)]
    feitos = []
    for nome, direcao, lente, so_motor in VISTAS:
        if vistas and nome not in vistas:
            continue
        if nome == "frente":
            r.resolution_x, r.resolution_y = 1200, 1100
        else:
            r.resolution_x, r.resolution_y = 1600, 1000
        _enquadrar(cena, cam, direcao, motor if so_motor else pts, lente, 0.92 if so_motor else 0.86)
        r.filepath = os.path.join(pasta, f"Moto_{nome}.png")
        t0 = time.time()
        bpy.ops.render.render(write_still=True)
        print(f"FOTO {nome}: {time.time() - t0:.1f} s")
        feitos.append(r.filepath)
    if vistas:
        return feitos
    # a lateral direita em projeção ortogonal, na escala da foto de referência (para a comparação)
    r.resolution_x, r.resolution_y = 1890, 1417
    cam.data.type = "ORTHO"
    px_m = 1185.0 / (2 * EIXO)          # na foto, 1185 px entre os eixos
    cam.data.ortho_scale = 1890.0 / px_m
    yc = EIXO - (365 - 945) / px_m * -1   # o eixo de trás em x = 365 px
    zc = R_RODA + (1110 - 708.5) / px_m   # e em y = 1110 px
    cam.location = (-5.0, EIXO - (945 - 365) / px_m, zc)
    cam.rotation_euler = V(1, 0, 0).to_track_quat("-Z", "Y").to_euler()
    _ = yc
    r.filepath = os.path.join(pasta, "Moto_comparacao_ortho.png")
    bpy.ops.render.render(write_still=True)
    feitos.append(r.filepath)
    return feitos


# ---------------------------------------------------------------------------------------------------- a linha de comando
def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    etapa = args[0] if args else "rascunho"
    versao = args[1] if len(args) > 1 else "v1"
    t0 = time.time()
    partes, medidas = construir()
    info = relatorio(partes, medidas)
    print("MEDIDAS", json.dumps(info, ensure_ascii=False, indent=1))
    if etapa in ("rascunho", "teste"):
        # rascunho: fotos rápidas no Workbench; teste: fotos em Cycles (CPU) numa pasta à parte, sem gravar o .blend
        pasta = args[2] if len(args) > 2 else os.path.join(AQUI, "rascunho")
        os.makedirs(pasta, exist_ok=True)
        with open(os.path.join(pasta, f"medidas_{versao}.json"), "w", encoding="utf-8") as f:
            json.dump(info, f, ensure_ascii=False, indent=1)
        fotos(pasta, partes, cycles=etapa == "teste", vistas=args[3].split(",") if len(args) > 3 else None)
    elif etapa == "tudo":
        blend = os.path.join(AQUI, f"Moto_{versao}.blend")
        if os.path.exists(blend):
            raise SystemExit(f"{blend} já existe: use uma versão nova (nenhuma versão é sobrescrita)")
        with open(os.path.join(AQUI, f"medidas_{versao}.json"), "w", encoding="utf-8") as f:
            json.dump(info, f, ensure_ascii=False, indent=1)
        exportar(os.path.join(AQUI, "export", f"Moto_{versao}.fbx"), partes)
        pasta = os.path.join(AQUI, "renders", versao)
        fotos(pasta, partes, cycles=True)
        # o .blend fica com a câmera, as luzes e o chão das fotos (as peças na posição da moto montada)
        bpy.ops.wm.save_as_mainfile(filepath=blend, compress=True)
    else:
        raise SystemExit("etapa desconhecida: " + etapa)
    print(f"PRONTO {etapa} {versao} em {time.time() - t0:.1f} s")


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception:
        import traceback
        traceback.print_exc()
        print("FALHOU")
        sys.exit(1)
