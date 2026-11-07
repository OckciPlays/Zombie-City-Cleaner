# A segunda moto do Zombie City Cleaner: uma motocross 450 de competição "inspirada" na foto
# referencia/Cross_lateral.png (laranja, branca e preta com detalhes roxos), low poly, em peças separadas como a Fat Boy
# (A Moto/moto.py) e o carro do jogo.
#
# Como rodar (Blender 5.2, num processo em segundo plano; não mexe no Blender aberto):
#   set OCO_CPU=1
#   blender -b --factory-startup --python cross.py -- tudo v1              (modelo + .blend + FBX + medidas + fotos Cycles CPU)
#   blender -b --factory-startup --python cross.py -- rascunho v1 <pasta>  (só fotos rápidas no Workbench, sem gravar aqui)
#   blender -b --factory-startup --python cross.py -- teste v1 <pasta> [vistas]   (o mesmo em Cycles)
#   python cross_fotos.py v1                                                (fora do Blender: fundo, prancha e comparação)
# Cada versão grava ao lado das outras (Cross_v1.blend, Cross_v2.blend...): nenhuma é apagada nem sobrescrita.
#
# Os eixos são os da Unity, como no carro.py e na Fat Boy: x para a direita da moto, y para cima, z para a frente; o chão
# em y = 0 e o meio entre os eixos em z = 0. No fim cada peça vira um objeto girado 90° em X com a malha espelhada em X
# (o formato do Carro.fbx: a Unity espelha o X de volta ao importar).
#
# As peças (objetos do FBX):
# - Cross_Corpo: o que não mexe (quadro, motor, escapamento, tanque, banco, carenagens do radiador, tampas laterais,
#   paralama de trás, balança, amortecedor, corrente, pedaleiras, pedal do freio e câmbio). A origem no chão, no meio
#   entre os eixos.
# - Cross_Direcao: o que gira com o guidão (garfo invertido, mesas, guidão, manetes, placa de número da frente, paralama da
#   frente, pinça e mangueira do freio). A origem no pivô da direção e a malha com o eixo da direção em pé; na moto ela
#   fica tombada pelo ângulo do garfo (na Unity, rotação X = -RAKE, o alto para trás).
# - Cross_RodaFrente / Cross_RodaTras: pneu de cravos, aro preto, raios, cubo e disco do freio (atrás também a coroa),
#   a origem no meio, o eixo em X. Na frente o disco fica à esquerda; atrás, o disco à direita e a coroa à esquerda.
#   A da frente é filha da direção no jogo.
# - Cross_Descanso: um descanso lateral fino (à esquerda), a origem na dobradiça. A moto de competição da foto não tem
#   descanso; este é para ela ficar em pé parada no jogo.
# Os pontos para a Unity (os lugares, as mãos e os pés) são vazios na coleção Cross_Pontos (não vão no FBX).

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

# ---------------------------------------------------------------------------------------------------- a foto
# referencia/Cross_lateral.png (710 x 432 px): o lado direito, a frente à direita. 465 px entre os eixos = 1,485 m (o
# entre-eixos da moto de verdade): 313 px por metro. O chão fica em y = 418 px e o meio entre os eixos em x = 352,5 px.
FOTO_TAM = (710, 432)
PX_M = 313.0
FOTO_Z0 = 352.5
FOTO_CHAO = 418.0


def F(px, py):
    """Um ponto da foto (em px) em (z, y) da moto, em metros."""
    return ((px - FOTO_Z0) / PX_M, (FOTO_CHAO - py) / PX_M)


def FL(lista):
    return [F(px, py) for px, py in lista]


# ---------------------------------------------------------------------------------------------------- medidas (m)
EIXO = 0.7425             # z do eixo da frente (o de trás em -EIXO): 1,485 m entre os eixos
R_F = 0.345               # raio do pneu da frente com os cravos (80/100-21)
R_T = 0.335               # e do de trás (120/80-19)
RAKE = math.radians(26.5)  # a inclinação da direção (o alto do garfo para trás)
D_GARFO = 0.022           # o garfo fica 2,2 cm à frente do eixo da direção (o avanço das mesas)
X_GARFO = 0.092           # meia distância entre as bengalas
Y_PIVO = 1.14             # a altura do pivô da direção (em cima da coluna do quadro, embaixo da mesa de cima)
S_MESA_BAIXO = 0.732      # as mesas, em metros ao longo do garfo a partir do eixo da roda
S_MESA_CIMA = 0.911
S_TOPO = 0.95             # o alto das bengalas
RAIOS = 28                # os raios de cada roda

FRENTE = V(0, R_F, EIXO)
TRAS = V(0, R_T, -EIXO)
U = V(0, math.cos(RAKE), -math.sin(RAKE))     # o eixo da direção, para cima
FW = V(0, math.sin(RAKE), math.cos(RAKE))     # perpendicular a ele, para a frente
A0 = FRENTE - FW * D_GARFO
PIVO = A0 + U * ((Y_PIVO - A0.y) / U.y)
QUADRO_DIRECAO = Matrix.Translation(PIVO) @ Matrix.Rotation(-RAKE, 4, "X")
BALANCA = V(0, 0.44, -0.195)       # o eixo da balança
DESCANSO = V(-0.105, 0.365, -0.29)  # a dobradiça do descanso
PE = V(0.165, 0.385, -0.235)       # o meio da pedaleira direita (a esquerda é o espelho)
PE_GARUPA = V(0.165, 0.52, -0.43)  # e a do garupa (v2)

# o guidão (metade direita, do meio para a ponta); o punho vai do 4º ao 5º ponto
GUIDAO = [(0.07, 1.21, 0.29), (0.16, 1.217, 0.278), (0.24, 1.232, 0.252), (0.29, 1.237, 0.238), (0.41, 1.241, 0.206)]
_mao = (Vector(GUIDAO[3]) + Vector(GUIDAO[4])) / 2

# os pontos para a Unity (eixos da Unity)
PONTOS = {
    "Lugar_Piloto": (0.0, 0.975, -0.25),
    "Lugar_Garupa": (0.0, 0.98, -0.56),
    "Mao_Direita": (_mao.x, _mao.y, _mao.z),
    "Mao_Esquerda": (-_mao.x, _mao.y, _mao.z),
    "Pe_Piloto_Direito": (PE.x, PE.y + 0.012, PE.z),
    "Pe_Piloto_Esquerdo": (-PE.x, PE.y + 0.012, PE.z),
    "Pe_Garupa_Direito": (PE_GARUPA.x, PE_GARUPA.y + 0.011, PE_GARUPA.z),
    "Pe_Garupa_Esquerdo": (-PE_GARUPA.x, PE_GARUPA.y + 0.011, PE_GARUPA.z),
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
MATERIAIS = ["Laranja", "Branco", "Preto", "Roxo", "Escuro", "Aluminio", "Titanio", "Cromado", "Borracha", "Farol",
             "Lanterna"]
MI = {n: i for i, n in enumerate(MATERIAIS)}
PBR = {
    "Laranja": dict(cor=(0.95, 0.15, 0.004), metal=0.0, asp=0.38, verniz=0.25),   # plásticos e banco
    "Branco": dict(cor=(0.80, 0.80, 0.79), metal=0.0, asp=0.40, verniz=0.2),       # carenagens e tampas
    "Preto": dict(cor=(0.012, 0.012, 0.013), metal=0.0, asp=0.45),                 # quadro, aros, adesivos
    "Roxo": dict(cor=(0.10, 0.022, 0.26), metal=0.0, asp=0.38, verniz=0.2),        # os detalhes dos adesivos
    "Escuro": dict(cor=(0.045, 0.045, 0.05), metal=0.5, asp=0.45),                 # o motor
    "Aluminio": dict(cor=(0.76, 0.76, 0.78), metal=1.0, asp=0.28),                 # balança, cubos, raios, mesas
    "Titanio": dict(cor=(0.62, 0.56, 0.48), metal=1.0, asp=0.32),                  # o coletor do escapamento
    "Cromado": dict(cor=(0.92, 0.92, 0.94), metal=1.0, asp=0.07),                  # os tubos do garfo
    "Borracha": dict(cor=(0.014, 0.014, 0.014), metal=0.0, asp=0.85, brilho=0.2),
    # (v2, para o jogo: o farol de LED na placa da frente e a lanterna na ponta do paralama; acendem na Unity)
    "Farol": dict(cor=(0.92, 0.95, 1.0), metal=0.0, asp=0.05, luz=0.6),
    "Lanterna": dict(cor=(0.75, 0.02, 0.02), metal=0.0, asp=0.12, luz=0.4),
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
    """Junta peças (caixas, barras, canos, tornos, cascas, chapas, decalques e textos) numa malha, nos eixos da Unity."""

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

    def concha(self, mat, linha, meias, quedas, esp, ref, lados=3, forma=2.0):
        """
        Uma chapa curva (paralama, placa) ao longo de uma linha da lateral [(z, y)]: em cada ponto a seção é um arco de
        meia largura meias[i], com as bordas quedas[i] para trás de ref (o lado de fora do arco), e a espessura esp.
        """
        pts = [V(0, y, z) for z, y in linha]
        aneis = []
        for i, p in enumerate(pts):
            if i == 0:
                t = pts[1] - pts[0]
            elif i == len(pts) - 1:
                t = pts[-1] - pts[-2]
            else:
                t = pts[i + 1] - pts[i - 1]
            t.normalize()
            n = V(0, -t.z, t.y)
            if n.dot(Vector(ref)) < 0:
                n = -n
            w, d = meias[i], quedas[i]
            fora, dentro = [], []
            for k in range(2 * lados + 1):
                u = -1 + k / lados
                q = p + X * (u * w) - n * (d * abs(u) ** forma)
                fora.append(q)
                dentro.append(q - n * esp)
            aneis.append(fora + list(reversed(dentro)))
        self.casca(mat, aneis)

    def chapa(self, mat, pts, xf, esp, s):
        """Uma chapa recortada (os pontos (z, y) da lateral) do lado s (+1 direita): a face de fora em x = s * xf(z, y)
        e a de dentro esp para dentro."""
        a = [self._v((s * xf(z, y), y, z)) for z, y in pts]
        b = [self._v((s * (xf(z, y) - esp), y, z)) for z, y in pts]
        n = len(pts)
        faces = [self._f(a, mat), self._f(list(reversed(b)), mat)]
        faces += [self._f((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]), mat) for i in range(n)]
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in faces if f])

    def adesivo(self, mat, pts, xf, s, alto=0.0015):
        """Um decalque sobre uma chapa (os pontos (z, y) da lateral), um pouco para fora dela."""
        self.poligono(mat, [(s * (xf(z, y) + alto), y, z) for z, y in pts], V(s, 0, 0))

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

    def disco_onda(self, mat, centro, x0, x1, r0, r1, onda, n):
        """Um disco em volta do eixo X (de x0 a x1, do raio r0 ao r1) com a borda de fora em zigue-zague (onda: a fundura):
        o disco de freio ondulado e os dentes da coroa."""
        c = Vector(centro)
        i0, i1, o0, o1 = [], [], [], []
        for k in range(n):
            a = 2 * math.pi * k / n
            d = V(0, math.sin(a), math.cos(a))
            ro = r1 - (onda if k % 2 else 0.0)
            i0.append(self._v(c + X * x0 + d * r0))
            i1.append(self._v(c + X * x1 + d * r0))
            o0.append(self._v(c + X * x0 + d * ro))
            o1.append(self._v(c + X * x1 + d * ro))
        faces = []
        for k in range(n):
            k2 = (k + 1) % n
            faces += [self._f((i1[k], i1[k2], o1[k2], o1[k]), mat), self._f((i0[k], o0[k], o0[k2], i0[k2]), mat),
                      self._f((o0[k], o1[k], o1[k2], o0[k2]), mat), self._f((i0[k], i0[k2], i1[k2], i1[k]), mat)]
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in faces if f])

    def cravo(self, mat, c, n, t, a, w, h, afina=0.72):
        """Um cravo do pneu: a base (a ao longo de t, w de lado) em c, a altura h para n; sem o fundo."""
        n, t = Vector(n).normalized(), Vector(t).normalized()
        b = n.cross(t).normalized()
        cantos = ((-1, -1), (1, -1), (1, 1), (-1, 1))
        base = [self._v(c + t * (sa * a / 2) + b * (sb * w / 2)) for sa, sb in cantos]
        topo = [self._v(c + n * h + t * (sa * a / 2 * afina) + b * (sb * w / 2 * afina)) for sa, sb in cantos]
        meio = c + n * (h / 2)
        faces = [self._f(topo, mat)] + [self._f((base[i], base[(i + 1) % 4], topo[(i + 1) % 4], topo[i]), mat)
                                        for i in range(4)]
        for f in faces:
            if f is None:
                continue
            f.normal_update()
            if f.normal.dot(f.calc_center_median() - meio) < 0:
                f.normal_flip()

    def disco(self, mat, centro, u, v, r, lados, normal):
        c, u, v = Vector(centro), Vector(u), Vector(v)
        pts = [c + u * (r * math.cos(2 * math.pi * k / lados)) + v * (r * math.sin(2 * math.pi * k / lados))
               for k in range(lados)]
        self.poligono(mat, pts, normal)

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
        bpy.data.meshes.remove(me)

    def bvh(self):
        return BVHTree.FromBMesh(self.bm)

    def objeto(self, nome, colecao, quadro=None):
        """
        A malha vira um objeto do formato do Carro.fbx: a origem no quadro (o pivô; sem quadro, a origem do mundo),
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


# ---------------------------------------------------------------------------------------------------- as rodas
def _pneu(m, o, R, w, aro):
    """O pneu de cravos: a carcaça e, em 32 fileiras, um cravo no meio e dois nos ombros, alternados."""
    h = 0.016                      # a altura dos cravos
    rc = R - h                     # o alto da carcaça
    hg = rc - aro
    rl = aro + 0.5 * hg            # a altura do ombro
    m.torno("Borracha", [(-0.62 * w, aro - 0.002), (-w, rl), (-0.55 * w, rc), (0.55 * w, rc), (w, rl),
                         (0.62 * w, aro - 0.002)], o, X, 24)
    folga = math.cos(math.pi / 24)  # (entre as quinas a carcaça fica um pouco mais baixa)
    # o meio do ombro e a normal dele, no plano (x, r)
    om_x, om_r = 0.775 * w, (rl + rc) / 2 * folga - 0.002
    nx, nr = rc - rl, 0.45 * w
    k = math.hypot(nx, nr)
    nx, nr = nx / k, nr / k
    n_fil = 32
    for i in range(n_fil):
        a = 2 * math.pi * (i + 0.5) / n_fil
        d = V(0, math.sin(a), math.cos(a))
        t = V(0, math.cos(a), -math.sin(a))
        if i % 2 == 0:
            m.cravo("Borracha", o + d * (rc * folga - 0.002), d, t, 0.028, 0.85 * w, h + 0.002)
        else:
            for s in (-1, 1):
                c = o + X * (s * om_x) + d * om_r
                m.cravo("Borracha", c, X * (s * nx) + d * nr, t, 0.026, 0.45 * w, h - 0.002)


def roda(colecao, nome, centro, frente):
    """A roda de raios: o pneu de cravos, o aro preto, os raios, o cubo e o disco do freio (atrás, também a coroa)."""
    m = Malha()
    o = Vector(centro)
    if frente:   # 21": aro 1.6 x 21, pneu 80/100
        R, w, aro, aw, fl, rfl, cw = R_F, 0.040, 0.2667, 0.020, 0.044, 0.042, 0.062
    else:        # 19": aro 2.15 x 19, pneu 120/80
        R, w, aro, aw, fl, rfl, cw = R_T, 0.060, 0.2413, 0.027, 0.055, 0.048, 0.075
    _pneu(m, o, R, w, aro)
    n = _contar(m, nome + " pneu", 0)
    # o aro preto (a seção fechada) e o cubo
    m.torno("Preto", [(-0.6 * aw, aro - 0.022), (-aw, aro + 0.006), (aw, aro + 0.006), (0.6 * aw, aro - 0.022)], o, X,
            20, fechado=True)
    m.torno("Aluminio", [(-cw - 0.003, 0.0), (-cw, 0.02), (-fl, rfl), (-fl + 0.008, rfl), (-fl + 0.014, 0.03),
                         (fl - 0.014, 0.03), (fl - 0.008, rfl), (fl, rfl), (cw, 0.02), (cw + 0.003, 0.0)], o, X, 10)
    n = _contar(m, nome + " aro e cubo", n)
    # os raios, cruzados (metade de cada flange)
    for i in range(RAIOS):
        s = 1 if i % 2 == 0 else -1
        ah = 2 * math.pi * i / RAIOS
        ar = ah + (0.45 if (i // 2) % 2 == 0 else -0.45)
        ph = o + V(s * (fl - 0.004), (rfl - 0.006) * math.sin(ah), (rfl - 0.006) * math.cos(ah))
        pr = o + V(s * 0.005, (aro - 0.02) * math.sin(ar), (aro - 0.02) * math.cos(ar))
        m.cano("Aluminio", [ph, pr], 0.0022, 3, tampas=False)
    n = _contar(m, nome + " raios", n)
    # o disco ondulado preso no cubo por um miolo vazado de 5 braços (laranja na frente, como na foto) e, atrás, a coroa
    if frente:
        m.disco_onda("Aluminio", o, -0.058, -0.054, 0.085, 0.13, 0.006, 20)
        m.disco_onda("Laranja", o, -0.0575, -0.0545, 0.028, 0.09, 0.036, 10)
    else:
        m.disco_onda("Aluminio", o, 0.068, 0.072, 0.072, 0.11, 0.005, 20)
        m.disco_onda("Aluminio", o, 0.0685, 0.0715, 0.028, 0.075, 0.03, 10)
        m.disco_onda("Escuro", o, -0.074, -0.068, 0.04, 0.122, 0.008, 24)
    n = _contar(m, nome + " disco e coroa", n)
    return m.objeto(nome, colecao, quadro=Matrix.Translation(centro))


# ---------------------------------------------------------------------------------------------------- o corpo
# o banco (z, topo, base, meia largura): reto e comprido, o mais baixo onde o piloto senta, subindo para o tanque
SECOES_BANCO = [
    (-0.695, 0.957, 0.925, 0.055),
    (-0.645, 0.960, 0.885, 0.105),
    (-0.490, 0.958, 0.872, 0.122),
    (-0.330, 0.953, 0.866, 0.124),
    (-0.170, 0.961, 0.862, 0.118),
    (-0.040, 0.975, 0.870, 0.105),
    (0.040, 0.990, 0.890, 0.090),
    (0.095, 1.003, 0.925, 0.075),
    (0.125, 1.006, 0.962, 0.045),
]

# o tanque (z, topo, base, meia largura), quase todo escondido entre as carenagens
SECOES_TANQUE = [
    (0.00, 0.985, 0.850, 0.085),
    (0.10, 1.022, 0.840, 0.105),
    (0.20, 1.035, 0.850, 0.105),
    (0.28, 1.018, 0.870, 0.090),
    (0.33, 0.990, 0.895, 0.055),
]

# as carenagens do radiador e as tampas laterais (o contorno na foto, em px)
ABA = [(300, 178), (305, 158), (322, 136), (345, 122), (372, 112), (395, 104), (425, 110), (460, 121), (500, 136),
       (490, 148), (474, 160), (458, 188), (450, 220), (440, 222), (430, 214), (421, 178), (412, 167), (352, 166),
       (346, 186), (306, 187)]
ADESIVOS_ABA = [
    ("Laranja", [(398, 125), (500, 135), (490, 142), (408, 142)]),
    ("Preto", [(305, 142), (350, 125), (398, 123), (402, 138), (360, 137), (320, 150)]),
    ("Preto", [(335, 151), (380, 141), (432, 141), (420, 146), (375, 148)]),
    ("Preto", [(302, 161), (330, 153), (362, 154), (340, 158)]),
    ("Preto", [(410, 156), (440, 149), (472, 148), (450, 154)]),
    ("Roxo", [(455, 153), (497, 138), (485, 156)]),
    ("Preto", [(424, 182), (440, 172), (457, 175), (440, 183)]),
    ("Roxo", [(421, 186), (457, 183), (452, 207), (422, 206)]),
]
TAMPA = [(128, 148), (140, 139), (200, 145), (260, 151), (300, 158), (306, 172), (300, 200), (292, 226), (262, 226),
         (240, 222), (210, 203), (160, 172)]
TAMPA_FAIXA = [(165, 141), (300, 158), (303, 166), (250, 160)]


def xf_aba(z, y):
    """A carenagem do radiador: mais aberta na frente e fechando por cima do tanque."""
    return 0.125 + 0.12 * (z + 0.15) - 0.20 * (y - 0.8)


def xf_tampa(z, y):
    return 0.110 + 0.02 * (z + 0.70) / 0.55


def _anel_banco(z, topo, base, hw):
    h = topo - base
    meio = [(0.0, topo), (0.6 * hw, topo - 0.04 * h), (0.92 * hw, topo - 0.2 * h), (hw, topo - 0.5 * h),
            (0.94 * hw, base + 0.12 * h), (0.5 * hw, base), (0.0, base)]
    return [V(x, y, z) for x, y in meio + [(-x, y) for x, y in reversed(meio[1:-1])]]


def _anel_tanque(z, topo, base, hw):
    h = topo - base
    meio = [(0.0, topo), (0.6 * hw, topo - 0.08 * h), (hw, topo - 0.4 * h), (0.95 * hw, base + 0.2 * h),
            (0.5 * hw, base), (0.0, base)]
    return [V(x, y, z) for x, y in meio + [(-x, y) for x, y in reversed(meio[1:-1])]]


def _tanque(m):
    """O tanque preto entre as carenagens, com a tampa."""
    m.casca("Preto", [_anel_tanque(*s) for s in SECOES_TANQUE])
    bvh = m.bvh()
    hit = bvh.ray_cast(V(0, 2.0, 0.165), V(0, -1, 0))[0]
    y = hit.y if hit else 1.03
    m.torno("Preto", [(-0.004, 0.028), (0.010, 0.028), (0.014, 0.0)], V(0, y, 0.165), Y, 8)


def _carenagens(m):
    """As carenagens do radiador (brancas, com os adesivos pretos, laranja e roxos e o "ZCC"), os radiadores atrás
    delas e as tampas laterais brancas embaixo do banco."""
    for s in (-1, 1):
        m.chapa("Branco", FL(ABA), xf_aba, 0.004, s)
        for mat, pts in ADESIVOS_ABA:
            m.adesivo(mat, FL(pts), xf_aba, s)
        zc, yc = F(438, 195)
        m.texto("Branco", "ZCC", V(s * (xf_aba(zc, yc) + 0.003), yc, zc), V(0.12, 0, s), V(-0.20 * s, 1, 0), 0.020,
                V(s, 0, 0))
        m.chapa("Branco", FL(TAMPA), xf_tampa, 0.004, s)
        m.adesivo("Preto", FL(TAMPA_FAIXA), xf_tampa, s)
        # o radiador (atrás da carenagem, acima da câmara do escapamento e atrás da borda da frente dela). Na v2 ele vai
        # só de z = 0,12 a 0,30: na janela da carenagem aparece o cabeçote, onde fica a vela que se troca no jogo
        m.caixa("Preto", (0.07, 0.20, 0.18), (s * 0.115, 0.76, 0.21))


def _quadro(m):
    """O quadro preto: a coluna da direção, as duas vigas, o tubo da frente que vira berço embaixo do motor, as placas
    da balança e o subquadro embaixo do banco."""
    a = no_eixo(1.025)
    m.torno("Preto", [(0, 0), (0, 0.029), ((Y_PIVO - 1.025) / U.y, 0.029), ((Y_PIVO - 1.025) / U.y, 0.0)], a, U, 8)
    for s in (-1, 1):
        viga = [(0.03, 1.10, 0.33), (0.09, 1.01, 0.22), (0.105, 0.87, 0.02), (0.115, 0.76, -0.15), (0.12, 0.58, -0.185),
                (0.12, 0.42, -0.20)]
        m.cano("Preto", [(s * x, y, z) for x, y, z in viga], (0.018, 0.03), 6, n0=X)
        berco = [(0.0, 0.57, 0.215), (0.045, 0.50, 0.205), (0.055, 0.40, 0.185), (0.06, 0.35, 0.12), (0.065, 0.345, 0.0),
                 (0.08, 0.355, -0.11), (0.11, 0.38, -0.175), (0.12, 0.42, -0.20)]
        m.cano("Preto", [(s * x, y, z) for x, y, z in berco], 0.016, 6)
        # a placa da balança
        m.caixa("Preto", (0.03, 0.17, 0.11), (s * 0.125, 0.43, -0.205), chanfro=0.01)
        m.torno("Aluminio", [(0.0, 0.016), (0.008, 0.016), (0.011, 0.0)], (s * 0.14, BALANCA.y, BALANCA.z), V(s, 0, 0), 6)
        # o subquadro (embaixo do banco, quase escondido pelas tampas)
        m.cano("Preto", [(s * 0.10, 0.84, -0.14), (s * 0.085, 0.88, -0.62)], 0.012, 4)
        m.cano("Preto", [(s * 0.115, 0.60, -0.19), (s * 0.09, 0.86, -0.58)], 0.012, 4)
    m.cano("Preto", [(0, 1.03, 0.36), (0, 0.80, 0.285), (0, 0.57, 0.215)], 0.02, 6)
    # a caixa do filtro de ar, embaixo do banco
    m.caixa("Preto", (0.17, 0.15, 0.32), (0, 0.79, -0.42))


def _motor(m):
    """O monocilíndrico: o cárter escuro, o cilindro e o cabeçote (a tampa de válvulas laranja), a tampa da embreagem
    preta à direita, a do volante à esquerda, a bomba d'água e o corpo de borboleta com a mangueira até o filtro."""
    carter = [(-0.165, 0.58), (0.10, 0.60), (0.185, 0.53), (0.18, 0.42), (0.13, 0.37), (0.0, 0.365), (-0.12, 0.375),
              (-0.17, 0.45)]
    m.perfil("Escuro", carter, -0.095, 0.095)
    # a parte de baixo do cárter, em alumínio (na foto, embaixo da tampa da embreagem)
    m.perfil("Aluminio", [(0.183, 0.44), (0.18, 0.418), (0.13, 0.368), (0.0, 0.363), (-0.12, 0.373), (-0.155, 0.42),
                          (-0.10, 0.432), (0.05, 0.432)], -0.097, 0.097)
    m.caixa("Escuro", (0.13, 0.14, 0.12), (0, 0.645, 0.06), giro=(8, 0, 0), chanfro=0.012)
    m.caixa("Escuro", (0.16, 0.10, 0.165), (0, 0.745, 0.075), giro=(8, 0, 0), chanfro=0.015)
    m.caixa("Laranja", (0.13, 0.03, 0.14), (0, 0.80, 0.083), giro=(8, 0, 0), chanfro=0.008)
    # a tampa da embreagem (à direita, a peça redonda preta da foto) e a do volante (à esquerda)
    zc, yc = F(336, 262)
    m.torno(["Escuro", "Preto", "Preto"], [(0.0, 0.08), (0.022, 0.079), (0.030, 0.065), (0.033, 0.0)],
            V(0.095, yc, zc), X, 14)
    m.torno(["Escuro", "Escuro"], [(0.0, 0.07), (0.025, 0.068), (0.032, 0.0)], V(-0.095, 0.47, -0.03), -X, 12)
    m.torno("Aluminio", [(0.0, 0.035), (0.02, 0.033), (0.026, 0.0)], V(0.095, 0.43, 0.105), X, 8)
    # o corpo de borboleta e a mangueira até a caixa do filtro
    m.caixa("Preto", (0.06, 0.06, 0.08), (0, 0.72, -0.07))
    m.cano("Preto", [(0, 0.72, -0.10), (0, 0.745, -0.18), (0, 0.775, -0.26)], 0.032, 8)
    # as mangueiras do radiador até o motor
    for s in (-1, 1):
        m.cano("Preto", [(s * 0.10, 0.68, 0.17), (s * 0.07, 0.62, 0.15), (s * 0.04, 0.60, 0.12)], 0.012, 6)


def _escapamento(m):
    """O coletor de titânio (sai na frente do cabeçote, dá a volta para baixo e corre pela direita com a câmara mais
    grossa), o tubo do meio por dentro do quadro e o silencioso preto com as pontas de alumínio, subindo para trás."""
    pts = [(0.02, 0.665, 0.17), (0.05, 0.63, 0.205), (0.07, 0.56, 0.235), (0.085, 0.51, 0.247), (0.105, 0.52, 0.243),
           (0.12, 0.56, 0.22), (0.125, 0.60, 0.19), (0.125, 0.617, 0.14), (0.125, 0.618, 0.05), (0.125, 0.616, -0.04),
           (0.115, 0.614, -0.10), (0.088, 0.612, -0.17), (0.10, 0.618, -0.24), (0.14, 0.627, -0.31),
           (0.168, 0.633, -0.372)]
    raios = [0.019, 0.019, 0.019, 0.019, 0.019, 0.021, 0.025, 0.033, 0.036, 0.033, 0.025, 0.02, 0.02, 0.02, 0.02]
    m.cano("Titanio", pts, raios, 8, tampas=False)
    # o silencioso (da boca na frente até a ponteira atrás)
    za, ya = F(236, 220)
    zb, yb = F(118, 158)
    a, b = V(0.168, ya, za), V(0.168, yb, zb)
    d = (b - a).normalized()
    comp = (b - a).length
    m.torno(["Aluminio", "Aluminio", "Preto", "Aluminio", "Aluminio", "Aluminio", "Escuro"],
            [(0.0, 0.0), (0.0, 0.021), (0.05, 0.047), (comp - 0.075, 0.049), (comp - 0.06, 0.05), (comp - 0.012, 0.047),
             (comp, 0.032), (comp - 0.006, 0.0)], a, d, 10)
    # o suporte no subquadro
    m.caixa("Preto", (0.04, 0.05, 0.012), (0.13, 0.80, -0.62), giro=(-28, 0, 0))


def _balanca(m):
    """A balança de alumínio, o amortecedor, a corrente (à esquerda) com o pinhão, a guia e o patim, a pinça do freio
    de trás e a mangueira."""
    for s in (-1, 1):
        aneis = []
        for t, xb, hb in ((-0.06, 0.10, 0.046), (0.5, 0.108, 0.04), (1.0, 0.115, 0.03)):
            c = BALANCA + (TRAS - BALANCA) * t
            ex = V(0, BALANCA.y - TRAS.y, BALANCA.z - TRAS.z).normalized()   # para a frente, ao longo do braço
            nn = V(0, ex.z, -ex.y)
            if nn.y < 0:
                nn = -nn
            a = 0.02
            pts = [(a, 0.6 * hb), (0.6 * a, hb), (-0.6 * a, hb), (-a, 0.6 * hb), (-a, -0.6 * hb), (-0.6 * a, -hb),
                   (0.6 * a, -hb), (a, -0.6 * hb)]
            aneis.append([c + V(s * xb, 0, 0) + X * px + nn * py for px, py in pts])
        m.casca("Aluminio", aneis)
        # o esticador da corrente e a porca do eixo
        m.caixa("Aluminio", (0.02, 0.03, 0.05), (s * 0.135, R_T, -EIXO))
        m.torno("Aluminio", [(0.0, 0.016), (0.01, 0.016), (0.014, 0.0)], (s * 0.145, R_T, -EIXO), V(s, 0, 0), 6)
    m.cano("Aluminio", [(-0.12, BALANCA.y, BALANCA.z), (0.12, BALANCA.y, BALANCA.z)], 0.024, 6)
    m.caixa("Aluminio", (0.2, 0.05, 0.06), (0, 0.43, -0.30), giro=(-11, 0, 0))
    # o amortecedor (o corpo, a mola e o reservatório)
    cima, baixo = V(0, 0.84, -0.245), V(0, 0.47, -0.33)
    dd = cima - baixo
    m.cano("Aluminio", [baixo, cima], 0.022, 6)
    m.cano("Escuro", [baixo + dd * 0.15, baixo + dd * 0.72], 0.034, 8)
    m.cano("Aluminio", [cima + V(0.035, -0.03, 0.02), cima + V(0.05, -0.16, 0.035)], 0.02, 6)
    # a corrente (à esquerda, no plano da coroa), o pinhão coberto, o patim e a guia
    xc = -0.071
    pinhao = V(xc, 0.47, -0.11)
    m.caixa("Preto", (0.03, 0.09, 0.11), pinhao + V(-0.02, 0, 0), chanfro=0.008)
    m.ripa("Escuro", pinhao + V(0, 0.033, 0), V(xc, R_T + 0.12, -EIXO), 0.012, 0.010)
    m.ripa("Escuro", pinhao + V(0, -0.033, 0), V(xc, R_T - 0.12, -EIXO), 0.012, 0.010)
    m.ripa("Preto", (-0.10, 0.49, -0.17), (-0.10, 0.455, -0.33), 0.03, 0.02)
    m.caixa("Preto", (0.025, 0.06, 0.13), (xc - 0.01, 0.235, -0.60), giro=(-12, 0, 0))
    # a pinça do freio de trás (à direita, embaixo e atrás do eixo) e a mangueira
    a = math.radians(245)
    pc = TRAS + V(0.075, 0.09 * math.sin(a), 0.09 * math.cos(a))
    m.caixa("Preto", (0.03, 0.045, 0.075), pc, giro=(-(245 + 90), 0, 0))
    m.ripa("Preto", pc, TRAS + V(0.085, 0, 0), 0.012, 0.03)
    m.cano("Preto", [pc + V(0.01, 0.02, 0.02), TRAS + V(0.09, -0.07, 0.14), (0.10, 0.355, -0.40), (0.12, 0.35, -0.27)],
           0.005, 4, tampas=False)


def _pedaleiras(m):
    """As pedaleiras largas, o pedal do freio (direita) e o pedal do câmbio (esquerda)."""
    for s in (-1, 1):
        m.caixa("Escuro", (0.10, 0.022, 0.055), (s * PE.x, PE.y, PE.z), chanfro=0.004)
        m.caixa("Escuro", (0.03, 0.06, 0.05), (s * 0.115, PE.y + 0.01, PE.z))
    m.ripa("Escuro", (0.125, 0.37, -0.215), (0.14, 0.36, -0.07), 0.014, 0.018, ref=Y)
    m.caixa("Escuro", (0.04, 0.012, 0.035), (0.15, 0.368, -0.06))
    m.ripa("Escuro", (-0.11, 0.465, -0.13), (-0.15, 0.43, -0.06), 0.014, 0.012, ref=Y)
    m.cano("Escuro", [(-0.13, 0.43, -0.06), (-0.17, 0.43, -0.06)], 0.009, 6)
    # (v2, a pedido: a moto leva dois) as pedaleiras do garupa, presas no subquadro por um suporte que desce de dentro
    # da tampa lateral; ficam abaixo do silencioso e acima da balança
    for s in (-1, 1):
        g = PE_GARUPA
        m.ripa("Preto", (s * 0.10, 0.68, g.z + 0.03), (s * 0.125, g.y + 0.005, g.z), 0.02, 0.03)
        m.caixa("Escuro", (0.08, 0.02, 0.045), (s * g.x, g.y, g.z), chanfro=0.004)


def _traseira(m):
    """O paralama de trás laranja, comprido e fino, que sai embaixo do banco e sobe um pouco na ponta."""
    linha = [(-0.56, 0.915), (-0.70, 0.947), (-0.87, 0.958), (-1.027, 0.966)]
    m.concha("Laranja", linha, [0.13, 0.115, 0.085, 0.035], [0.07, 0.05, 0.03, 0.008], 0.006, Y, forma=2.5)
    # (v2, para o jogo) a lanterna pequena em cima do paralama, perto da ponta, virada para trás
    m.caixa("Preto", (0.075, 0.03, 0.05), (0, 0.974, -0.94), chanfro=0.006)
    m.caixa("Lanterna", (0.064, 0.02, 0.006), (0, 0.975, -0.9655))


def corpo(colecao):
    m = Malha()
    m.casca("Laranja", [_anel_banco(*s) for s in SECOES_BANCO])
    n = _contar(m, "banco", 0)
    # a altura do banco: o ponto mais baixo do assento do piloto, no meio
    bvh = m.bvh()
    alturas = []
    for i in range(41):
        z = -0.45 + 0.40 * i / 40
        hit = bvh.ray_cast(V(0, 2.0, z), V(0, -1, 0))[0]
        if hit:
            alturas.append((hit.y, z))
    banco = min(alturas)
    _tanque(m)
    n = _contar(m, "tanque", n)
    _carenagens(m)
    n = _contar(m, "carenagens", n)
    _quadro(m)
    n = _contar(m, "quadro", n)
    _motor(m)
    n = _contar(m, "motor", n)
    _escapamento(m)
    n = _contar(m, "escapamento", n)
    _balanca(m)
    n = _contar(m, "balanca", n)
    _pedaleiras(m)
    n = _contar(m, "pedaleiras", n)
    _traseira(m)
    n = _contar(m, "traseira", n)
    return m.objeto("Cross_Corpo", colecao), {"altura_do_banco": banco[0], "z_do_ponto_mais_baixo": banco[1]}


# ---------------------------------------------------------------------------------------------------- a direção
def direcao(colecao):
    m = Malha()
    rot = Matrix.Rotation(-RAKE, 4, "X")
    for s in (-1, 1):
        x = s * X_GARFO
        b = garfo(0, x)
        # o garfo invertido: o tubo grosso em cima (alumínio, com a faixa laranja), o tubo fino embaixo, o protetor
        # branco e o pé com o eixo
        m.torno("Aluminio", [(0.40, 0.0), (0.40, 0.0265), (0.43, 0.0305), (S_TOPO, 0.0305), (S_TOPO + 0.005, 0.0)], b,
                U, 10)
        m.torno("Cromado", [(0.06, 0.0235), (0.41, 0.0235)], b, U, 8)
        m.torno("Branco", [(0.10, 0.0), (0.10, 0.0285), (0.36, 0.0285), (0.375, 0.0)], b + FW * 0.006, U, 8)
        m.torno("Aluminio", [(-0.045, 0.0), (-0.045, 0.027), (0.105, 0.027), (0.11, 0.0)], b, U, 8)
        m.caixa("Aluminio", (0.03, 0.06, 0.07), FRENTE + V(x, 0, 0), rot=rot)
        xo = s * (X_GARFO + 0.0315)
        m.poligono("Laranja", [garfo(0.50, xo) + FW * 0.012, garfo(0.50, xo) - FW * 0.012, garfo(0.64, xo) - FW * 0.012,
                               garfo(0.64, xo) + FW * 0.012], V(s, 0, 0))
    n = _contar(m, "garfo", 0)
    m.cano("Aluminio", [FRENTE + V(-0.125, 0, 0), FRENTE + V(0.125, 0, 0)], 0.011, 6)
    # as mesas, a porca da coluna e os suportes do guidão
    m.caixa("Aluminio", (2 * X_GARFO + 0.075, 0.05, 0.085), garfo(S_MESA_BAIXO) - FW * 0.012, rot=rot, chanfro=0.008)
    m.caixa("Aluminio", (2 * X_GARFO + 0.07, 0.03, 0.08), garfo(S_MESA_CIMA) - FW * 0.012, rot=rot, chanfro=0.006)
    m.torno("Aluminio", [(0.0, 0.018), (0.012, 0.018), (0.014, 0.0)], garfo(S_MESA_CIMA + 0.015) - FW * D_GARFO, U, 6)
    for s in (-1, 1):
        m.caixa("Aluminio", (0.035, 0.055, 0.045), (s * 0.04, 1.20, 0.29))
    n = _contar(m, "mesas", n)
    # o guidão, os punhos, os manetes, o acelerador e o cilindro do freio
    lado = GUIDAO
    m.cano("Aluminio", [(-x, y, z) for x, y, z in reversed(lado)] + list(lado), 0.0125, 8)
    for s in (-1, 1):
        a, b = V(s * lado[3][0], lado[3][1], lado[3][2]), V(s * lado[4][0], lado[4][1], lado[4][2])
        dd = (b - a).normalized()
        m.cano("Borracha", [a + (b - a) * 0.05, b + dd * 0.004], 0.017, 8)
        m.cano("Preto", [b + dd * 0.004, b + dd * 0.012], 0.0165, 8)
        m.caixa("Preto", (0.04, 0.032, 0.042), (s * 0.222, 1.236, 0.254))
        m.ripa("Aluminio", (s * 0.235, 1.232, 0.282), (s * 0.375, 1.236, 0.258), 0.012, 0.006, ref=Y)
    m.caixa("Preto", (0.035, 0.04, 0.04), (0.268, 1.238, 0.243))
    m.caixa("Preto", (0.03, 0.02, 0.035), (0.215, 1.262, 0.255))
    n = _contar(m, "guidao", n)
    # a placa de número da frente (laranja, na frente das bengalas)
    linha = [(garfo(s).z + FW.z * 0.048, garfo(s).y + FW.y * 0.048) for s in (0.70, 0.79, 0.87, 0.925)]
    m.concha("Laranja", linha, [0.09, 0.103, 0.108, 0.10], [0.03, 0.032, 0.032, 0.03], 0.005, FW)
    # (v2, para o jogo) o farol de LED oval no meio da placa, como nas motos de trilha com farol: a moldura preta
    # (funda, para encostar na placa curva dos lados) e a lente, que acende na Unity
    m.torno(["Preto", "Preto", "Farol"], [(-0.022, 0.05), (0.007, 0.05), (0.011, 0.044), (0.012, 0.0)],
            garfo(0.80) + FW * 0.052, FW, 12, escala=(0.8, 1.25))
    n = _contar(m, "placa", n)
    # o paralama da frente (alto, preso embaixo da mesa de baixo, até a ponta)
    linha = FL([(478, 121), (520, 113), (570, 116), (612, 132), (647, 153)])
    m.concha("Laranja", linha, [0.062, 0.078, 0.075, 0.055, 0.014], [0.045, 0.05, 0.04, 0.022, 0.004], 0.005, Y)
    n = _contar(m, "paralama", n)
    # a pinça do freio (à esquerda, atrás da bengala), o suporte e a mangueira até o cilindro no guidão
    a = 140.0
    pc = FRENTE + V(-0.06, 0.105 * math.sin(math.radians(a)), 0.105 * math.cos(math.radians(a)))
    m.caixa("Preto", (0.035, 0.045, 0.085), pc, giro=(-(a + 90), 0, 0))
    m.ripa("Preto", pc + V(-0.01, 0, 0), garfo(0.06, -X_GARFO) - FW * 0.02, 0.012, 0.03)
    mang = [V(0.215, 1.225, 0.255), V(0.15, 1.14, 0.19), V(0.09, 1.06, 0.21), V(0.0, 0.99, 0.30),
            garfo(0.62, -X_GARFO - 0.025) - FW * 0.035, garfo(0.30, -X_GARFO - 0.03) - FW * 0.035, pc + V(0, 0.03, 0.0)]
    m.cano("Preto", mang, 0.005, 4, tampas=False)
    return m.objeto("Cross_Direcao", colecao, quadro=QUADRO_DIRECAO)


def descanso(colecao):
    m = Malha()
    pe = V(-0.30, 0.012, -0.16)
    m.ripa("Preto", DESCANSO, pe, 0.018, 0.014)
    m.caixa("Preto", (0.04, 0.010, 0.035), pe + V(-0.006, -0.006, 0.003))
    return m.objeto("Cross_Descanso", colecao, quadro=Matrix.Translation(DESCANSO))


# ---------------------------------------------------------------------------------------------------- a montagem
NOMES = ["Cross_Corpo", "Cross_Direcao", "Cross_RodaFrente", "Cross_RodaTras", "Cross_Descanso"]


def limpar():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)
    cena = bpy.context.scene
    cena.name = "Cross"
    cena.unit_settings.system = "METRIC"
    return cena


def construir():
    cena = limpar()
    col = bpy.data.collections.new("Cross")
    cena.collection.children.link(col)
    corpo_o, medidas = corpo(col)
    partes = [corpo_o, direcao(col), roda(col, "Cross_RodaFrente", FRENTE, True),
              roda(col, "Cross_RodaTras", TRAS, False), descanso(col)]
    pontos = bpy.data.collections.new("Cross_Pontos")
    cena.collection.children.link(pontos)
    for nome, p in list(PONTOS.items()) + [("Pivo_Direcao", tuple(PIVO)), ("Eixo_Frente", tuple(FRENTE)),
                                           ("Eixo_Tras", tuple(TRAS)), ("Eixo_Balanca", tuple(BALANCA))]:
        e = bpy.data.objects.new(nome, None)
        e.empty_display_type = "PLAIN_AXES"
        e.empty_display_size = 0.06
        e.location = para_blender(p)
        e["unity"] = list(p)
        pontos.objects.link(e)
    return partes, medidas


def relatorio(partes, medidas):
    bpy.context.view_layer.update()   # (sem isto as peças novas ainda estão com a posição zerada e as medidas saem erradas)
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
        "raio_roda_frente": R_F,
        "raio_roda_tras": R_T,
        "rake_graus": math.degrees(RAKE),
        "pivo_da_direcao_unity": list(PIVO),
        "roda_da_frente_nos_eixos_da_direcao": list(loc),
        "eixo_da_balanca_unity": list(BALANCA),
        "descanso_unity": list(DESCANSO),
        "pontos_unity": {k: list(v) for k, v in PONTOS.items()},
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
        # o mundo: o HDRI de estúdio que vem com o Blender (os reflexos do metal)
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
        s.background_color = (0.92, 0.92, 0.90)
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
    motor = [V(*para_blender((x, y, z))) for x in (0.0, 0.2) for y in (0.33, 0.82) for z in (-0.30, 0.30)]
    feitos = []
    for nome, direcao, lente, so_motor in VISTAS:
        if vistas and nome not in vistas:
            continue
        if nome == "frente":
            r.resolution_x, r.resolution_y = 1200, 1100
        else:
            r.resolution_x, r.resolution_y = 1600, 1000
        _enquadrar(cena, cam, direcao, motor if so_motor else pts, lente, 0.92 if so_motor else 0.86)
        r.filepath = os.path.join(pasta, f"Cross_{nome}.png")
        t0 = time.time()
        bpy.ops.render.render(write_still=True)
        print(f"FOTO {nome}: {time.time() - t0:.1f} s")
        feitos.append(r.filepath)
    if vistas and "comparacao" not in vistas:
        return feitos
    # a lateral direita em projeção ortogonal, na escala da foto de referência (o dobro do tamanho), para a comparação
    r.resolution_x, r.resolution_y = FOTO_TAM[0] * 2, FOTO_TAM[1] * 2
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = FOTO_TAM[0] / PX_M
    zc, yc = F(FOTO_TAM[0] / 2, FOTO_TAM[1] / 2)
    cam.location = para_blender((5.0, yc, zc))
    cam.rotation_euler = V(1, 0, 0).to_track_quat("-Z", "Y").to_euler()
    r.filepath = os.path.join(pasta, "Cross_comparacao_ortho.png")
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
        blend = os.path.join(AQUI, f"Cross_{versao}.blend")
        if os.path.exists(blend):
            raise SystemExit(f"{blend} já existe: use uma versão nova (nenhuma versão é sobrescrita)")
        with open(os.path.join(AQUI, f"medidas_{versao}.json"), "w", encoding="utf-8") as f:
            json.dump(info, f, ensure_ascii=False, indent=1)
        exportar(os.path.join(AQUI, "export", f"Cross_{versao}.fbx"), partes)
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
