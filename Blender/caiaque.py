# O caiaque (06/11/2026, a pedido: a pesca e a água): um caiaque de travessia para dois, de sentar dentro, amarelo, com
# as duas aberturas (a borda preta em volta e o assento com o encosto dentro), as escotilhas redondas, os elásticos
# cruzados na proa e na popa, as linhas do convés, a emenda escura do casco com o convés e as alças das pontas; e o remo
# de duas pás.
#
# Como rodar (Blender 5.2, em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/caiaque.py -- tudo
#   (as etapas: construir, exportar; "foto <saída.png>" faz uma foto de 3/4 para conferir)
# Grava Assets/_Game/Art/Veiculos/Caiaque.fbx e Assets/_Game/Art/Veiculos/Remo.fbx.
#
# As medidas são escritas nos eixos da Unity, como no carro.py (x para a direita, y para cima, z para a frente) e o
# formato é o mesmo do Carro.fbx: cada peça é um objeto girado 90° em X com a malha espelhada em X (a Unity espelha o X
# de volta ao importar). Cores: blocos sólidos pela Paleta.png, no material Paleta (só cores que já existem nela).
#
# As peças:
# - Caiaque (no Caiaque.fbx): o casco inteiro, com a origem no meio dele, na linha d'água (12 cm acima do fundo); a proa
#   para +Z. As aberturas são cortadas no casco (um booleano): o fundo delas é o piso onde ficam os assentos.
#   - Assento_0 (o da frente) e Assento_1 (o de trás): vazios filhos dele, no quadril de quem senta (para a Unity), sem
#     giro (a frente de quem senta é o +Z do caiaque).
# - Remo (no Remo.fbx): o remo de duas pás, a origem no meio do cabo, o cabo ao longo do X e as pás em pé (a face em Z).

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(AQUI)
DESTINO = os.path.join(REPO, "Assets", "_Game", "Art", "Veiculos")
PALETA = os.path.join(REPO, "Assets", "_Game", "Art", "Paleta.png")

# (linha da imagem, de cima para baixo; coluna) de cada cor na Paleta.png
CORES = {
    "casco": (11, 7),        # o amarelo das embalagens
    "por_dentro": (6, 3),    # o mostarda do carro (o amarelo na sombra, dentro das aberturas)
    "preto": (3, 2),
    "borracha": (11, 4),     # a emenda, as escotilhas
    "assento": (11, 1),      # o grafite
    "elastico": (11, 5),
    "corda": (11, 3),        # as linhas do convés, claras
    "aco": (8, 2),
    "pa": (11, 6),           # o vermelho das pás
}

# medidas (m)
COMP = 4.6               # o comprimento
MEIA_LARGURA = 0.39      # a meia largura no meio
FUNDO = -0.12            # o fundo do casco (a origem é a linha d'água)
BORDA = 0.10             # a altura da borda (onde o casco encontra o convés) no meio
CONVES = 0.23            # o alto do convés no meio (o casco tem 35 cm)
PONTA_PROA, PONTA_POPA = 0.25, 0.21   # a altura das pontas (a proa e a popa sobem)
# as aberturas: o meio em z, os semieixos (em z e em x) e o piso
ABERTURAS = [0.55, -0.50]
A_ABERTURA, B_ABERTURA = 0.40, 0.21
EXPOENTE_ABERTURA = 3.0  # a superelipse (mais larga perto das pontas que uma elipse)
PISO = -0.07
TOPO_DA_BORDA = 0.25     # o alto da borda preta das aberturas
# os assentos (o quadril de quem senta, para a Unity): 14 cm atrás do meio de cada abertura, 8 cm acima da almofada
ASSENTOS = [(0.0, 0.06, z - 0.14) for z in ABERTURAS]
REMO = 2.2               # o comprimento do remo


def _uv(cor):
    linha, coluna = CORES[cor]
    return ((coluna + 0.5) / 16.0, 1.0 - (linha + 0.5) / 16.0)


def _material():
    m = bpy.data.materials.get("Paleta") or bpy.data.materials.new("Paleta")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None)
    if tex is None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    img = bpy.data.images.get("Paleta.png") or bpy.data.images.load(PALETA)
    img.alpha_mode = "NONE"   # o alfa da paleta é o brilho da cidade, não transparência
    tex.image = img
    tex.interpolation = "Closest"
    return m


# ---------------------------------------------------------------------------------------------------- a forma do casco
def _t(z):
    """De -1 (a popa) a 1 (a proa)."""
    return max(-1.0, min(1.0, z / (COMP / 2)))


def largura(z):
    """A meia largura na borda em z: cheia no meio, as pontas finas."""
    t = abs(_t(z))
    return MEIA_LARGURA * max(0.0, 1 - t ** 2.2) ** 1.1


def _ponta(z):
    return PONTA_PROA if z > 0 else PONTA_POPA


def fundo(z):
    """A quilha em z: sobe nas pontas (o rocker)."""
    t = abs(_t(z))
    return FUNDO + (_ponta(z) - FUNDO) * t ** 3.2


def borda(z):
    t = abs(_t(z))
    return BORDA + (_ponta(z) - BORDA) * t ** 2


def conves(z):
    """O alto do convés no meio: um pouco mais alto na frente da abertura da frente (os joelhos)."""
    t = abs(_t(z))
    return CONVES + (_ponta(z) - CONVES) * t ** 1.5 + 0.02 * math.exp(-((z - 1.0) / 0.35) ** 2)


def altura_do_conves(x, z):
    """O alto do convés em (x, z) (o arco elíptico de uma borda à outra)."""
    w = largura(z)
    if w <= 1e-6:
        return conves(z)
    f = min(1.0, abs(x) / w)
    return borda(z) + (conves(z) - borda(z)) * math.sqrt(1 - f * f)


def _secao(z):
    """Os pontos (x, y, a cor do trecho até o próximo) do lado direito da seção em z, da quilha ao meio do convés."""
    w, yk, ys, yd = largura(z), fundo(z), borda(z), conves(z)
    pts = []
    # o casco: da quilha à emenda (uma superelipse: o fundo arredondado)
    for th in (0, 14, 28, 42, 56, 70, 82):
        a = math.radians(th)
        pts.append((w * math.sin(a) ** 0.75, ys - 0.016 - (ys - 0.016 - yk) * math.cos(a) ** 0.85, "casco"))
    # a emenda escura (uma faixa de 1,6 cm logo abaixo da borda)
    pts.append((w * 0.995, ys - 0.016, "borracha"))
    # o convés: o arco da borda ao meio
    for k, ph in enumerate((0, 12, 25, 40, 57, 75, 90)):
        a = math.radians(ph)
        pts.append((w * math.cos(a), ys + (yd - ys) * math.sin(a), "casco"))
    return pts


def _casco_malha():
    """O casco fechado (sem as aberturas): as seções do fim da popa ao da proa, as pontas fechadas num vértice."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    # as estações (mais juntas nas pontas)
    zs = [COMP / 2 * math.sin(math.pi / 2 * (-1 + 2 * i / 48)) for i in range(1, 48)]
    aneis, cores = [], None
    for z in zs:
        lado = _secao(z)
        direita = [(x, y) for x, y, _ in lado]
        cores = [c for _, _, c in lado]
        # o anel: o lado direito da quilha ao meio do convés, depois o esquerdo de volta (sem repetir as pontas do meio)
        anel = direita + [(-x, y) for x, y in reversed(direita[1:-1])]
        aneis.append([bm.verts.new((x, y, z)) for x, y in anel])
    cores_anel = cores[:-1] + list(reversed(cores[:-1]))   # a cor de cada trecho do anel (o esquerdo espelhado)
    n = len(aneis[0])

    def pintar(f, cor):
        u, v = _uv(cor)
        for l in f.loops:
            l[uv].uv = (u, v)

    for i in range(len(aneis) - 1):
        for k in range(n):
            j = (k + 1) % n
            f = bm.faces.new((aneis[i][k], aneis[i][j], aneis[i + 1][j], aneis[i + 1][k]))
            pintar(f, cores_anel[k])
    for anel, z in ((aneis[0], -COMP / 2), (aneis[-1], COMP / 2)):
        ponta = bm.verts.new((0.0, _ponta(z), z))
        for k in range(n):
            pintar(bm.faces.new((anel[k], anel[(k + 1) % n], ponta)), cores_anel[k])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("Caiaque_Casco_Temp")
    bm.to_mesh(me)
    bm.free()
    return me


def _contorno_da_abertura(zc, folga=0.0, n=40):
    """Os pontos (x, z) da superelipse da abertura em zc, com uma folga para fora."""
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        c, s = math.cos(a), math.sin(a)
        x = (B_ABERTURA + folga) * math.copysign(abs(c) ** (2 / EXPOENTE_ABERTURA), c)
        z = zc + (A_ABERTURA + folga) * math.copysign(abs(s) ** (2 / EXPOENTE_ABERTURA), s)
        pts.append((x, z))
    return pts


def _cortador(zc):
    """O cilindro da abertura (do piso até bem acima do convés), pintado por dentro (o mostarda)."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    pts = _contorno_da_abertura(zc)
    baixo = [bm.verts.new((x, PISO, z)) for x, z in pts]
    cima = [bm.verts.new((x, 0.6, z)) for x, z in pts]
    n = len(pts)
    faces = [bm.faces.new(baixo), bm.faces.new(cima[::-1])]
    faces += [bm.faces.new((baixo[k], baixo[(k + 1) % n], cima[(k + 1) % n], cima[k])) for k in range(n)]
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    u, v = _uv("por_dentro")
    for f in faces:
        for l in f.loops:
            l[uv].uv = (u, v)
    me = bpy.data.meshes.new(f"Caiaque_Cortador_{zc}")
    bm.to_mesh(me)
    bm.free()
    return me


def _casco_aberto(cena):
    """O casco com as aberturas cortadas (o booleano exato), numa malha nova."""
    casco = bpy.data.objects.new("Caiaque_Casco_Temp", _casco_malha())
    cena.collection.objects.link(casco)
    cortadores = []
    for zc in ABERTURAS:
        c = bpy.data.objects.new(f"Caiaque_Cortador_{zc}", _cortador(zc))
        cena.collection.objects.link(c)
        c.hide_render = True
        cortadores.append(c)
        mod = casco.modifiers.new(f"Abertura_{zc}", "BOOLEAN")
        mod.operation = "DIFFERENCE"
        mod.solver = "EXACT"
        mod.object = c
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(casco.evaluated_get(dg))
    for o in [casco] + cortadores:
        velha = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.meshes.remove(velha)
    return me


# ---------------------------------------------------------------------------------------------------- as peças
class Malha:
    """Junta peças (caixas, ripas, cilindros, placas) numa malha, nos eixos da Unity (como a do carro.py)."""

    def __init__(self, inicio=None):
        self.bm = bmesh.new()
        if inicio is not None:
            self.bm.from_mesh(inicio)
        self.uv = self.bm.loops.layers.uv.get("UVMap") or self.bm.loops.layers.uv.new("UVMap")

    def _pintar(self, faces, cor):
        u, v = _uv(cor)
        for f in faces:
            f.material_index = 0
            for l in f.loops:
                l[self.uv].uv = (u, v)

    @staticmethod
    def _faces(vs):
        return list({f for v in vs for f in v.link_faces})

    def caixa(self, cor, tam, centro, giro=(0, 0, 0), chanfro=0.0):
        """Uma caixa de tam (x, y, z), girada (graus: X, depois Y, depois Z) em volta do centro."""
        vs = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        m = Matrix.Translation(Vector(centro)) @ Matrix.Rotation(math.radians(giro[2]), 4, "Z") @ \
            Matrix.Rotation(math.radians(giro[1]), 4, "Y") @ Matrix.Rotation(math.radians(giro[0]), 4, "X") @ \
            Matrix.Diagonal((tam[0], tam[1], tam[2], 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=vs)
        faces = self._faces(vs)
        self._pintar(faces, cor)
        if chanfro > 0 and min(tam) > chanfro * 2.5:
            arestas = list({e for f in faces for e in f.edges})
            bmesh.ops.bevel(self.bm, geom=vs + arestas, offset=chanfro, segments=1, affect="EDGES", profile=0.5)

    def ripa(self, cor, a, b, tx, ty, lado=(1.0, 0.0, 0.0)):
        """Uma barra de a até b: tx de largura na direção do lado (sem a parte ao longo da barra) e ty na outra."""
        a, b = Vector(a), Vector(b)
        z = (b - a).normalized()
        x = Vector(lado) - z * z.dot(Vector(lado))
        if x.length < 1e-6:
            x = Vector((0.0, 1.0, 0.0)) - z * z.y
        x.normalize()
        y = z.cross(x)
        base = Matrix((x, y, z)).transposed().to_4x4()
        vs = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation((a + b) / 2) @ base @ Matrix.Diagonal((tx, ty, (b - a).length, 1.0)),
                            verts=vs)
        self._pintar(self._faces(vs), cor)

    def cilindro(self, cor, raio, comprimento, centro, eixo="x", lados=12, raio2=None, giro=None):
        r = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=lados, radius1=raio,
                                  radius2=raio if raio2 is None else raio2, depth=comprimento)
        vs = r["verts"]
        g = {"x": Matrix.Rotation(math.radians(90), 4, "Y"), "y": Matrix.Rotation(math.radians(-90), 4, "X"),
             "z": Matrix.Identity(4)}[eixo]
        if giro is not None:
            g = giro @ g
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(Vector(centro)) @ g, verts=vs)
        self._pintar(self._faces(vs), cor)

    def placa(self, cor, pontos, z0, z1, matriz=Matrix.Identity(4)):
        """Um polígono convexo (os pontos (u, v) em volta) estendido de z0 a z1, levado pela matriz (u, v, z) -> eixos."""
        a = [self.bm.verts.new(matriz @ Vector((u, v, z0))) for u, v in pontos]
        b = [self.bm.verts.new(matriz @ Vector((u, v, z1))) for u, v in pontos]
        n = len(pontos)
        faces = [self.bm.faces.new(a[::-1]), self.bm.faces.new(b)]
        faces += [self.bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k])) for k in range(n)]
        self._pintar(faces, cor)

    def anel(self, cor, por_dentro, por_fora, y_baixo, y_cima):
        """Um anel de seção retangular entre dois contornos (x, z) (o de dentro e o de fora, com o mesmo número de pontos):
        a borda de baixo segue y_baixo(x, z) e a de cima fica em y_cima."""
        n = len(por_dentro)
        vs = []
        for (xi, zi), (xo, zo) in zip(por_dentro, por_fora):
            vs.append((self.bm.verts.new((xi, y_baixo(xi, zi), zi)), self.bm.verts.new((xi, y_cima, zi)),
                       self.bm.verts.new((xo, y_cima, zo)), self.bm.verts.new((xo, y_baixo(xo, zo), zo))))
        faces = []
        for k in range(n):
            a, b = vs[k], vs[(k + 1) % n]
            for q in range(4):
                faces.append(self.bm.faces.new((a[q], a[(q + 1) % 4], b[(q + 1) % 4], b[q])))
        self._pintar(faces, cor)

    def objeto(self, nome, cena):
        """A malha vira um objeto do formato do Carro.fbx: espelhada em X e girada 90° em X (a origem na origem)."""
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bmesh.ops.scale(bm, vec=(-1, 1, 1), verts=bm.verts)
        bmesh.ops.reverse_faces(bm, faces=bm.faces)   # o espelho virou as faces do avesso
        me = bpy.data.meshes.new(nome)
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = False
            p.material_index = 0
        me.materials.append(_material())
        o = bpy.data.objects.new(nome, me)
        o.rotation_euler = (math.radians(90), 0, 0)
        cena.collection.objects.link(o)
        return o


def _no_conves(m, cor, pts, folga=0.006, grossura=0.007):
    """Uma linha (o elástico, a corda) por cima do convés, pelos pontos (x, z), em pedaços que seguem o arco."""
    for (xa, za), (xb, zb) in zip(pts, pts[1:]):
        passos = max(1, int(math.hypot(xb - xa, zb - za) / 0.05))
        for k in range(passos):
            t0, t1 = k / passos, (k + 1) / passos
            x0, z0 = xa + (xb - xa) * t0, za + (zb - za) * t0
            x1, z1 = xa + (xb - xa) * t1, za + (zb - za) * t1
            m.ripa(cor, (x0, altura_do_conves(x0, z0) + folga, z0), (x1, altura_do_conves(x1, z1) + folga, z1),
                   grossura, grossura, lado=(0, 1, 0))


def _ilhos(m, pts):
    """Os ilhoses (as presilhas pretas) no convés, em cada ponto (x, z)."""
    for x, z in pts:
        m.caixa("preto", (0.024, 0.012, 0.03), (x, altura_do_conves(x, z) + 0.003, z), chanfro=0.003)


def _elasticos(m, z0, z1):
    """Os elásticos cruzados (um X e duas linhas de través) entre z0 e z1, com os ilhoses nas bordas."""
    def x_de(z):
        return largura(z) * 0.78
    a, b = (x_de(z0), z0), (x_de(z1), z1)
    pontos = [a, b, (-a[0], a[1]), (-b[0], b[1])]
    _no_conves(m, "elastico", [(-a[0], z0), (a[0], z0)])
    _no_conves(m, "elastico", [(-b[0], z1), (b[0], z1)])
    _no_conves(m, "elastico", [(-a[0], z0), (b[0], z1)])
    _no_conves(m, "elastico", [(a[0], z0), (-b[0], z1)])
    _ilhos(m, pontos)


def _escotilha(m, z, raio):
    """A escotilha redonda: a tampa de borracha (inclinada como o convés em z) com a borda e a alça."""
    y = altura_do_conves(0, z)
    inclina = math.degrees(math.atan2(conves(z + 0.05) - conves(z - 0.05), 0.1))
    g = Matrix.Rotation(math.radians(-inclina), 4, "X")
    m.cilindro("borracha", raio + 0.012, 0.022, (0, y + 0.002, z), eixo="y", lados=20, giro=g)
    m.cilindro("preto", raio, 0.022, (0, y + 0.010, z), eixo="y", lados=20, giro=g)
    m.ripa("borracha", (-raio * 0.45, y + 0.026, z), (raio * 0.45, y + 0.026, z), 0.03, 0.012, lado=(0, 0, 1))


def _abertura(m, zc):
    """A borda preta em volta da abertura (por cima do convés, um pouco para dentro do corte), o assento e o encosto."""
    dentro = _contorno_da_abertura(zc, -0.006)
    fora = _contorno_da_abertura(zc, 0.03)
    m.anel("preto", dentro, fora, lambda x, z: min(altura_do_conves(x, z), TOPO_DA_BORDA) - 0.02, TOPO_DA_BORDA)
    # o lábio de cima, um pouco mais largo (onde a saia prende)
    m.anel("preto", _contorno_da_abertura(zc, 0.025), _contorno_da_abertura(zc, 0.042),
           lambda x, z: TOPO_DA_BORDA - 0.012, TOPO_DA_BORDA - 0.002)
    zq = zc - 0.14   # o quadril
    # a almofada do assento e o encosto, inclinado para trás
    m.caixa("assento", (0.34, 0.05, 0.36), (0, PISO + 0.025, zq + 0.02), chanfro=0.012)
    m.caixa("assento", (0.30, 0.03, 0.30), (0, PISO + 0.065, zq + 0.04), giro=(-3, 0, 0), chanfro=0.01)
    m.ripa("assento", (0, PISO + 0.05, zq - 0.15), (0, PISO + 0.27, zq - 0.21), 0.28, 0.04)
    # o apoio dos pés (os dois trilhos na frente, nos lados)
    for s in (-1, 1):
        m.caixa("borracha", (0.03, 0.06, 0.12), (s * 0.15, PISO + 0.05, zc + 0.30), chanfro=0.006)


def _pontas(m):
    """As alças das pontas: o puxador preto pendurado na corda, embaixo da ponta."""
    for z, s in ((COMP / 2, 1), (-COMP / 2, -1)):
        y = _ponta(z)
        zf = z - s * 0.06
        m.cilindro("preto", 0.014, 0.13, (0, y - 0.07, zf + s * 0.03), eixo="x", lados=8)
        for x in (-0.04, 0.04):
            m.ripa("corda", (x * 0.3, y - 0.005, zf - s * 0.02), (x, y - 0.065, zf + s * 0.03), 0.008, 0.008, lado=(0, 0, 1))


def _linhas_do_conves(m):
    """As linhas do convés (as cordas claras pelas bordas, da ponta até a abertura) e os ilhoses delas."""
    for z0, z1 in ((1.0, 2.0), (-0.95, -2.0)):
        for s in (-1, 1):
            pts = []
            for k in range(9):
                z = z0 + (z1 - z0) * k / 8
                pts.append((s * largura(z) * 0.86, z))
            _no_conves(m, "corda", pts, folga=0.006, grossura=0.006)
            _ilhos(m, [pts[0], pts[4], pts[-1]])


def _caiaque(cena):
    m = Malha(_casco_aberto(cena))
    for zc in ABERTURAS:
        _abertura(m, zc)
    _escotilha(m, 1.25, 0.11)
    _escotilha(m, -1.3, 0.12)
    _elasticos(m, 1.48, 1.85)
    _elasticos(m, -1.52, -1.88)
    # o elástico pequeno entre as aberturas
    _elasticos(m, -0.02, 0.08)
    _linhas_do_conves(m)
    _pontas(m)
    # a quilha da popa (o skeg, uma barbatana baixa embaixo)
    m.placa("preto", [(-1.95, fundo(-1.95) + 0.01), (-2.2, fundo(-2.2) + 0.01), (-2.2, fundo(-2.2) - 0.07),
                      (-2.02, fundo(-2.02) - 0.05)], -0.006, 0.006,
            matriz=Matrix(((0, 0, 1, 0), (0, 1, 0, 0), (1, 0, 0, 0), (0, 0, 0, 1))))
    casco = m.objeto("Caiaque", cena)
    for i, p in enumerate(ASSENTOS):
        e = bpy.data.objects.new(f"Assento_{i}", None)
        e.empty_display_type = "ARROWS"
        e.empty_display_size = 0.15
        cena.collection.objects.link(e)
        e.parent = casco
        # no espaço do casco (girado 90° em X, a malha nos eixos da Unity com o X espelhado): o ponto (-x, y, z)
        e.location = (-p[0], p[1], p[2])
        e.rotation_euler = (0, 0, 0)
    return casco


def _remo(cena):
    """O remo: o cabo grafite (com as duas pegas pretas e os anéis de pingo) e as pás vermelhas, assimétricas, com a
    nervura no meio; o cabo ao longo do X, as pás em pé com a face em Z (a de puxar para -Z, para quem rema)."""
    m = Malha()
    meio = REMO / 2
    m.cilindro("assento", 0.0145, REMO - 0.86, (0, 0, 0), eixo="x", lados=10)
    for s in (-1, 1):
        m.cilindro("preto", 0.0165, 0.16, (s * 0.30, 0, 0), eixo="x", lados=10)
        m.cilindro("preto", 0.036, 0.008, (s * 0.52, 0, 0), eixo="x", lados=14, raio2=0.02)
        # o colar da pá e a pá: o contorno no plano XY (u ao longo do cabo, a partir da raiz; v para cima)
        x0 = s * (meio - 0.47)
        m.cilindro("assento", 0.017, 0.05, (x0 - s * 0.01, 0, 0), eixo="x", lados=10)
        pa = [(0.0, -0.022), (0.08, -0.06), (0.24, -0.082), (0.40, -0.075), (0.462, -0.045), (0.47, 0.0), (0.462, 0.05),
              (0.40, 0.085), (0.22, 0.09), (0.08, 0.065), (0.0, 0.024)]
        mat = Matrix(((s, 0, 0, x0), (0, 1, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))
        m.placa("pa", pa, -0.004, 0.004, matriz=mat)
        # a nervura (atrás, do lado +Z), afinando até a ponta
        m.ripa("pa", (x0, 0, 0.004), (x0 + s * 0.40, 0, 0.003), 0.02, 0.012, lado=(0, 1, 0))
    return m.objeto("Remo", cena)


NOMES = ["Caiaque", "Assento_0", "Assento_1", "Remo"]


def _cena():
    return bpy.context.scene


def construir():
    cena = _cena()
    for o in list(cena.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    casco = _caiaque(cena)
    remo = _remo(cena)
    # no Blender, o remo fica ao lado do caiaque, para conferir (é exportado na origem)
    remo.location = (1.2, 0, 0.3)
    print(f"caiaque: {len(casco.data.polygons)} faces; remo: {len(remo.data.polygons)} faces")
    return casco, remo


def exportar():
    cena = _cena()
    os.makedirs(DESTINO, exist_ok=True)
    casco, remo = cena.objects["Caiaque"], cena.objects["Remo"]
    for caminho, objs in ((os.path.join(DESTINO, "Caiaque.fbx"), [casco] + list(casco.children)),
                          (os.path.join(DESTINO, "Remo.fbx"), [remo])):
        guardado = {}
        for o in cena.objects:
            o.select_set(o in objs)
        for o in objs:
            if o.parent is None:
                # cada peça vai na origem, girada 90° em X, como no Carro.fbx
                guardado[o.name] = tuple(o.location)
                o.location = (0, 0, 0)
        bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH", "EMPTY"},
                                 apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")
        for nome, loc in guardado.items():
            cena.objects[nome].location = loc
        print("caiaque: gravado", caminho)
    bpy.context.view_layer.update()


def foto(caminho, direcao=(0.9, 0.6, 1.2), resolucao=(1000, 640), alvo=(0, 0.05, 0), distancia=7.0, lente=50):
    """Uma foto no Workbench (a textura, a cavidade e o contorno) sobre fundo neutro, de uma direção (eixos da Unity)."""
    cena = _cena()
    r = cena.render
    try:
        r.engine = "BLENDER_WORKBENCH"
    except TypeError:
        pass
    r.film_transparent = False
    r.image_settings.file_format = "PNG"
    r.resolution_x, r.resolution_y = resolucao
    s = cena.display.shading
    s.light = "STUDIO"
    s.color_type = "TEXTURE"
    s.show_cavity = True
    s.cavity_type = "BOTH"
    s.show_object_outline = True
    s.object_outline_color = (0.05, 0.05, 0.05)
    s.background_type = "VIEWPORT"
    s.background_color = (0.42, 0.42, 0.39)
    cena.view_settings.view_transform = "Standard"
    cam = bpy.data.objects.get("CameraDoCaiaque")
    if cam is None:
        cam = bpy.data.objects.new("CameraDoCaiaque", bpy.data.cameras.new("CameraDoCaiaque"))
        cena.collection.objects.link(cam)
    cam.data.lens = lente
    cam.data.clip_start = 0.02
    cam.data.clip_end = 200
    x, y, z = direcao
    d = Vector((-x, -z, y)).normalized()
    ax, ay, az = alvo
    cam.location = Vector((-ax, -az, ay)) + d * distancia
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cena.camera = cam
    r.filepath = caminho
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    etapa = args[0] if args else "tudo"
    construir()
    if etapa in ("exportar", "tudo"):
        exportar()
    if etapa == "foto":
        foto(args[1])
