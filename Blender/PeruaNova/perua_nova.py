# A perua nova (o visual refeito do carro que se dirige, 28/10/2026): a mesma perua dos anos 90 do carro.py, com as
# mesmas medidas, peças, origens e pontos (portas, rodas, capô, tampa, volante, painel, ignição, bancos, porta-malas),
# mas com mais detalhe e materiais PBR assados em texturas (pintura mostarda desbotada, painéis imitando madeira com
# frisos, cromados, borrachas, ferrugem, sujeira; interior azul-turquesa de vinil).
#
# O carro.py NÃO muda: este script só lê as medidas dele.
#
# Como rodar (Blender 5.2):
#   import sys, importlib; sys.path.append(r"<repo>\Blender\PeruaNova"); import perua_nova; importlib.reload(perua_nova)
#   perua_nova.construir()   # refaz as peças na cena "PeruaNova" (com os materiais de autoria, procedurais)
#   perua_nova.assar()       # desembrulha (UV) e assa as texturas PBR em Assets/_Game/Art/Veiculos/PeruaNova/Texturas
#   perua_nova.exportar()    # grava Assets/_Game/Art/Veiculos/PeruaNova/PeruaNova.fbx (5 materiais finais)
#   perua_nova.salvar()      # guarda o .blend editável em Blender/PeruaNova/PeruaNova.blend
# Depois, na Unity: Zombie City Cleaner > Veículos > Aplicar o visual novo da perua (Editor/PeruaNova/VisualDaPerua.cs).
#
# As peças (os nomes do FBX; as origens são as do carro.py):
#   Perua_Carroceria (materiais: Perua_Lataria, Perua_Interior, Perua_Vidro, Perua_Farol, Perua_Lanterna),
#   Perua_Porta_DE/DD/TE/TD, Perua_Roda (eixo X, calota para +X, raio 0,30, largura 0,21), Perua_Volante, Perua_Capo,
#   Perua_Tampa.
# Os eixos são os da Unity (x direita, y cima, z frente; o chão em y = 0), e cada peça vira um objeto girado 90° em X
# com a malha espelhada em X, como no carro.py.

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
PASTA_BLENDER = os.path.dirname(AQUI)
if PASTA_BLENDER not in sys.path:
    sys.path.append(PASTA_BLENDER)
import carro as base  # noqa: E402  (só as medidas e as funções de perfil)

REPO = os.path.dirname(PASTA_BLENDER)
DESTINO = os.path.join(REPO, "Assets", "_Game", "Art", "Veiculos", "PeruaNova")
TEXTURAS = os.path.join(DESTINO, "Texturas")
BLEND = os.path.join(AQUI, "PeruaNova.blend")

# as medidas do carro.py (não mudam: a física e os pontos da Unity dependem delas)
W, EIXO = base.W, base.EIXO
R_RODA, X_RODA, R_ARCO = base.R_RODA, base.X_RODA, base.R_ARCO
Y_BASE, Y_CINTURA, Y_TETO = base.Y_BASE, base.Y_CINTURA, base.Y_TETO
Z_PORTA_D, Z_COLUNA_B, Z_PORTA_T, Z_COLUNA_C = base.Z_PORTA_D, base.Z_COLUNA_B, base.Z_PORTA_T, base.Z_COLUNA_C
PARABRISA, VIDRO_TRAS, PIVO_TAMPA = base.PARABRISA, base.VIDRO_TRAS, base.PIVO_TAMPA
X_CAPO, Z_CAPO, Y_COFRE = base.X_CAPO, base.Z_CAPO, base.Y_COFRE
X_MALAS, Y_PISO_MALAS, Y_SOLEIRA = base.X_MALAS, base.Y_PISO_MALAS, base.Y_SOLEIRA
_y_capo, _arco, _sem_repetidos, _para_blender = base._y_capo, base._arco, base._sem_repetidos, base._para_blender

# a largura do pneu (Unity: PneusDoCarro.MeiaLargura = 0,105, o cilindro do tiro)
MEIA_LARGURA_PNEU = 0.105
# a faixa de madeira dos lados (altura de baixo e de cima)
Y_MADEIRA = (0.42, 0.84)
FRISO = 0.022            # a largura da moldura bege em volta da madeira

# ---------------------------------------------------------------------------------------------------- materiais
# Cada superfície tem um material de autoria (procedural, para ver no Blender e assar). Na exportação, eles viram 5:
# os do atlas "A" (a lataria e as rodas) viram Perua_Lataria; os do "B" (o interior), Perua_Interior; e o vidro e as
# lentes ficam com materiais próprios (a Unity acende as lentes trocando o material do submesh).
SUPERFICIES = {
    # nome: (atlas, material final)
    "pintura": ("A", "Perua_Lataria"), "madeira": ("A", "Perua_Lataria"), "friso": ("A", "Perua_Lataria"),
    "cromo": ("A", "Perua_Lataria"), "borracha": ("A", "Perua_Lataria"), "pneu": ("A", "Perua_Lataria"),
    "plastico": ("A", "Perua_Lataria"), "grade": ("A", "Perua_Lataria"), "escuro": ("A", "Perua_Lataria"),
    "aco": ("A", "Perua_Lataria"), "placa_f": ("A", "Perua_Lataria"), "placa_t": ("A", "Perua_Lataria"),
    "pisca": ("A", "Perua_Lataria"), "re": ("A", "Perua_Lataria"), "refletor": ("A", "Perua_Lataria"),
    "lente_farol": ("A", "Perua_Farol"), "lente_lanterna": ("A", "Perua_Lanterna"),
    "vinil": ("B", "Perua_Interior"), "painel": ("B", "Perua_Interior"), "carpete": ("B", "Perua_Interior"),
    "forro": ("B", "Perua_Interior"), "int_cromo": ("B", "Perua_Interior"), "int_plastico": ("B", "Perua_Interior"),
    "madeira_int": ("B", "Perua_Interior"), "cinto": ("B", "Perua_Interior"), "tapete": ("B", "Perua_Interior"),
    "alto_falante": ("B", "Perua_Interior"),
    "vidro": ("-", "Perua_Vidro"),
}
FINAIS = ["Perua_Lataria", "Perua_Interior", "Perua_Vidro", "Perua_Farol", "Perua_Lanterna"]
TAMANHO_ATLAS = {"A": 2048, "B": 2048}


def _lin(r, g, b):
    """Uma cor sRGB (0-255) em linear, para os nós."""
    def c(v):
        v = v / 255.0
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    return (c(r), c(g), c(b), 1.0)


# a pintura: um mostarda desbotado pelo sol (a perua do jogo era mostarda; aqui, envelhecida). Mude para (214, 197, 160)
# para um creme como o da referência.
COR_PINTURA = (188, 146, 58)


# ---------------------------------------------------------------------------------------------------- a malha
class Malha:
    """Junta peças numa malha (bmesh), nos eixos da Unity, com um material de autoria por face. Cada peça é feita no
    lugar e só as faces novas (marcadas pela camada "feito") recebem o quadro de referência e o material."""

    def __init__(self):
        self.bm = bmesh.new()
        self.bm.loops.layers.uv.new("UVMap")
        self.feito = self.bm.faces.layers.int.new("feito")
        self.mats = []          # os nomes das superfícies, na ordem dos índices
        self.M = Matrix.Identity(4)
        self._pilha = []

    # -- o quadro de referência (para montar peças giradas, como os bancos)
    def entrar(self, m):
        self._pilha.append(self.M)
        self.M = self.M @ m

    def sair(self):
        self.M = self._pilha.pop()

    def _idx(self, sup):
        if sup not in SUPERFICIES:
            raise KeyError(sup)
        if sup not in self.mats:
            self.mats.append(sup)
        return self.mats.index(sup)

    def _novas(self):
        return [f for f in self.bm.faces if f[self.feito] == 0]

    def _fim(self, sup, liso=False):
        faces = self._novas()
        vs = list({v for f in faces for v in f.verts})
        bmesh.ops.transform(self.bm, matrix=self.M, verts=vs)
        i = self._idx(sup)
        for f in faces:
            f.material_index = i
            f[self.feito] = 1
            if liso and len(f.verts) == 4:
                f.smooth = True
        return faces

    def _chanfrar(self, chanfro, seg=1, so_tampas=None):
        faces = self._novas() if so_tampas is None else so_tampas
        arestas = list({e for f in faces for e in f.edges})
        geom = arestas if so_tampas is not None else list({v for f in faces for v in f.verts}) + arestas
        bmesh.ops.bevel(self.bm, geom=geom, offset=chanfro, segments=seg, affect="EDGES", profile=0.5,
                        clamp_overlap=True)

    def caixa(self, sup, tam, centro, giro=(0, 0, 0), chanfro=0.006, seg=1):
        vs = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        m = Matrix.Translation(Vector(centro)) @ Matrix.Rotation(math.radians(giro[2]), 4, "Z") @ \
            Matrix.Rotation(math.radians(giro[1]), 4, "Y") @ Matrix.Rotation(math.radians(giro[0]), 4, "X") @ \
            Matrix.Diagonal((tam[0], tam[1], tam[2], 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=vs)
        if chanfro > 0 and min(tam) > chanfro * 2.5:
            self._chanfrar(chanfro, seg)
        return self._fim(sup)

    def _puxar(self, pts3a, pts3b):
        a = [self.bm.verts.new(p) for p in pts3a]
        b = [self.bm.verts.new(p) for p in pts3b]
        n = len(a)
        tampas = [self.bm.faces.new(a), self.bm.faces.new(b[::-1])]
        lados = [self.bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i])) for i in range(n)]
        bmesh.ops.recalc_face_normals(self.bm, faces=tampas + lados)
        return tampas

    def perfil(self, sup, pontos, x0, x1, chanfro=0.0, seg=2):
        """Um polígono no plano de lado (pontos em (z, y)), puxado em X de x0 a x1, com as bordas arredondadas."""
        pontos = _sem_repetidos(pontos)
        tampas = self._puxar([(x0, y, z) for z, y in pontos], [(x1, y, z) for z, y in pontos])
        if chanfro > 0:
            self._chanfrar(chanfro, seg, so_tampas=tampas)
        return self._fim(sup)

    def frente(self, sup, pontos, z0, z1, chanfro=0.0, seg=1):
        """Um polígono no plano da frente (pontos em (x, y)), puxado em Z de z0 a z1."""
        pontos = _sem_repetidos(pontos)
        tampas = self._puxar([(x, y, z0) for x, y in pontos], [(x, y, z1) for x, y in pontos])
        if chanfro > 0:
            self._chanfrar(chanfro, seg, so_tampas=tampas)
        return self._fim(sup)

    def ripa(self, sup, a, b, tx, ty, chanfro=0.0):
        a, b = Vector(a), Vector(b)
        z = (b - a).normalized()
        x = Vector((1, 0, 0)) - z * z.x
        x = x.normalized() if x.length > 1e-4 else Vector((0, 1, 0))
        y = z.cross(x)
        base_ = Matrix((x, y, z)).transposed().to_4x4()
        vs = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        m = Matrix.Translation((a + b) / 2) @ base_ @ Matrix.Diagonal((tx, ty, (b - a).length, 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=vs)
        if chanfro > 0 and min(tx, ty) > chanfro * 2.5:
            self._chanfrar(chanfro)
        return self._fim(sup)

    def cilindro(self, sup, raio, comprimento, centro, eixo="x", lados=16, raio2=None, liso=True):
        r = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=lados, radius1=raio,
                                  radius2=raio if raio2 is None else raio2, depth=comprimento)
        giro = {"x": Matrix.Rotation(math.radians(90), 4, "Y"), "y": Matrix.Rotation(math.radians(-90), 4, "X"),
                "z": Matrix.Identity(4)}[eixo]
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(Vector(centro)) @ giro, verts=r["verts"])
        return self._fim(sup, liso=liso)

    def torno(self, sup, perfil, centro=(0, 0, 0), eixo="x", lados=32, fechar=(False, False), liso=True, escala=(1, 1)):
        """Gira um perfil [(raio, posição no eixo)] em volta do eixo. escala: o achatamento das duas direções em volta."""
        c = Vector(centro)
        aneis = []
        for r, p in perfil:
            anel = []
            for i in range(lados if r > 1e-6 else 1):
                t = 2 * math.pi * i / lados
                u, v = r * math.cos(t) * escala[0], r * math.sin(t) * escala[1]
                q = (p, u, v) if eixo == "x" else (u, v, p) if eixo == "z" else (u, p, v)
                anel.append(self.bm.verts.new(c + Vector(q)))
            aneis.append(anel)
        for k in range(len(aneis) - 1):
            a, b = aneis[k], aneis[k + 1]
            for i in range(lados):
                j = (i + 1) % lados
                if len(a) == 1 and len(b) == 1:
                    continue
                if len(a) == 1:
                    self.bm.faces.new((a[0], b[j], b[i]))
                elif len(b) == 1:
                    self.bm.faces.new((a[i], a[j], b[0]))
                else:
                    self.bm.faces.new((a[i], a[j], b[j], b[i]))
        if fechar[0] and len(aneis[0]) > 1:
            self.bm.faces.new(aneis[0][::-1])
        if fechar[1] and len(aneis[-1]) > 1:
            self.bm.faces.new(aneis[-1])
        bmesh.ops.recalc_face_normals(self.bm, faces=self._novas())
        return self._fim(sup, liso=liso)

    def tubo(self, sup, pontos, raio, lados=8, liso=True):
        """Um tubo ao longo de uma linha de pontos, com as pontas fechadas."""
        pts = [Vector(p) for p in pontos]
        n = len(pts)
        aneis = []
        ref = None
        for i, p in enumerate(pts):
            t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
            if ref is None:
                ref = Vector((0, 1, 0)) if abs(t.y) < 0.9 else Vector((1, 0, 0))
            u = (ref - t * ref.dot(t)).normalized()
            v = t.cross(u)
            ref = u
            aneis.append([self.bm.verts.new(p + raio * (math.cos(2 * math.pi * k / lados) * u + math.sin(2 * math.pi * k / lados) * v))
                          for k in range(lados)])
        for k in range(n - 1):
            a, b = aneis[k], aneis[k + 1]
            for i in range(lados):
                j = (i + 1) % lados
                self.bm.faces.new((a[i], a[j], b[j], b[i]))
        self.bm.faces.new(aneis[0][::-1])
        self.bm.faces.new(aneis[-1])
        bmesh.ops.recalc_face_normals(self.bm, faces=self._novas())
        return self._fim(sup, liso=liso)

    def toro(self, sup, raio, espessura, centro=(0, 0, 0), lados=32, gomos=8, achatar=1.0):
        """Um anel (o aro do volante) no plano XY, em volta do centro."""
        c = Vector(centro)
        aneis = []
        for i in range(lados):
            t = 2 * math.pi * i / lados
            d = Vector((math.cos(t), math.sin(t), 0))
            aneis.append([self.bm.verts.new(c + d * (raio + espessura * math.cos(2 * math.pi * k / gomos)) +
                                             Vector((0, 0, espessura * achatar * math.sin(2 * math.pi * k / gomos))))
                          for k in range(gomos)])
        for i in range(lados):
            a, b = aneis[i], aneis[(i + 1) % lados]
            for k in range(gomos):
                q = (k + 1) % gomos
                self.bm.faces.new((a[k], b[k], b[q], a[q]))
        bmesh.ops.recalc_face_normals(self.bm, faces=self._novas())
        return self._fim(sup, liso=True)

    def objeto(self, nome, cena, pivo=(0, 0, 0)):
        bm = self.bm
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], quad_method="BEAUTY", ngon_method="BEAUTY")
        bmesh.ops.translate(bm, verts=bm.verts, vec=-Vector(pivo))
        bmesh.ops.scale(bm, vec=(-1, 1, 1), verts=bm.verts)
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
        bm.faces.layers.int.remove(self.feito)
        me = bpy.data.meshes.new(nome)
        bm.to_mesh(me)
        bm.free()
        for s in self.mats:
            me.materials.append(_material_de_autoria(s))
        o = bpy.data.objects.new(nome, me)
        o.rotation_euler = (math.radians(90), 0, 0)
        o.location = _para_blender(pivo)
        cena.collection.objects.link(o)
        return o


# ---------------------------------------------------------------------------------------------------- formas
def _faixa(z0, z1, y0, y1, arcos=(), topo=None, passos=40):
    """O polígono (z, y) de uma faixa de lado de z0 a z1 (z0 > z1), de y0 a y1, sem o recorte das rodas (arcos: os z
    dos eixos). topo(z), se houver, baixa a borda de cima."""
    zs = [z0 + (z1 - z0) * i / passos for i in range(passos + 1)]
    folga = R_ARCO + 0.045

    def baixo(z):
        y = y0
        for za in arcos:
            d = abs(z - za)
            if d < folga:
                y = max(y, R_RODA + math.sqrt(folga * folga - d * d))
        return y

    def cima(z):
        return min(y1, topo(z)) if topo else y1

    pts = [(z, cima(z)) for z in zs] + [(z, baixo(z)) for z in reversed(zs)]
    return pts


def _area(ps):
    return sum(ps[i][0] * ps[(i + 1) % len(ps)][1] - ps[(i + 1) % len(ps)][0] * ps[i][1] for i in range(len(ps))) / 2


def _deslocar(pts, d):
    """Cada aresta de um polígono (z, y) andando d pela normal da esquerda (o canto vai pela bissetriz)."""
    n = len(pts)
    saida = []
    for i in range(n):
        p0, p1, p2 = Vector(pts[i - 1]), Vector(pts[i]), Vector(pts[(i + 1) % n])
        a = (p1 - p0).normalized() if (p1 - p0).length > 1e-6 else Vector((1, 0))
        b = (p2 - p1).normalized() if (p2 - p1).length > 1e-6 else a
        na, nb = Vector((-a.y, a.x)), Vector((-b.y, b.x))
        nn = na + nb
        nn = nn.normalized() if nn.length > 1e-6 else na
        cos = max(0.35, nn.dot(na))
        q = p1 + nn * (d / cos)
        saida.append((q.x, q.y))
    return saida


def _encolher(pts, d):
    """O polígono d para dentro."""
    a = _deslocar(pts, d)
    return a if abs(_area(a)) < abs(_area(pts)) else _deslocar(pts, -d)


def _madeira_de_lado(m, pts, s, recuo=0.0):
    """O painel de madeira com a moldura bege em volta, colado do lado de fora (s: o lado), num polígono (z, y)."""
    x = s * (W + recuo)
    moldura = pts
    madeira = _encolher(pts, FRISO)
    m.perfil("friso", moldura, x + s * 0.0005, x + s * 0.0045)
    m.perfil("madeira", madeira, x + s * 0.0005, x + s * 0.0068)


def _forro_do_arco(m, zc, x0, x1, sup="escuro"):
    """A caixa da roda por dentro: meia casca em volta do arco, de x0 a x1."""
    n = 14
    r = R_ARCO - 0.004
    pts = []
    for i in range(n + 1):
        t = math.pi * i / n
        pts.append((zc + r * math.cos(t), R_RODA + r * math.sin(t)))
    for i in range(n):
        (za, ya), (zb, yb) = pts[i], pts[i + 1]
        m.ripa(sup, ((x0 + x1) / 2, ya, za), ((x0 + x1) / 2, yb, zb), abs(x1 - x0), 0.012)


# o recuo da frente, atrás dos faróis e da grade
Y_RECUO = (0.575, 0.755)
Z_RECUO = 2.085


def _recuo(a, b):
    """Os pontos (z, y) do recuo na linha da frente de a (em cima) até b (embaixo)."""
    def z_em(y):
        return a[0] + (b[0] - a[0]) * (a[1] - y) / (a[1] - b[1])
    return [(z_em(Y_RECUO[1]), Y_RECUO[1]), (Z_RECUO, Y_RECUO[1]), (Z_RECUO, Y_RECUO[0]), (z_em(Y_RECUO[0]), Y_RECUO[0])]


# ---------------------------------------------------------------------------------------------------- a carroceria
def _carroceria(cena):
    m = Malha()
    # ======================================================== a lataria (as mesmas linhas do carro.py, arredondadas)
    # a frente tem um recuo na altura dos faróis (Y_RECUO, até Z_RECUO): os faróis ficam em caixas abertas, e o refletor
    # aparece pela lente (que é transparente apagada)
    frente = [(Z_PORTA_D, Y_BASE), (Z_PORTA_D, Y_CINTURA), (1.00, Y_CINTURA), (1.10, 0.915), (2.02, 0.84), (2.12, 0.79)]
    frente += _recuo((2.12, 0.79), (2.14, 0.52)) + [(2.14, 0.52), (2.10, 0.24)]
    frente += _arco(EIXO, True)
    for s in (-1, 1):
        m.perfil("pintura", frente, s * (X_CAPO + 0.005), s * W, chanfro=0.018)
    meio = [(Z_PORTA_D, Y_BASE), (Z_PORTA_D, Y_CINTURA), (1.00, Y_CINTURA), (1.10, 0.915), (Z_CAPO, _y_capo(Z_CAPO) - 0.026),
            (Z_CAPO, Y_COFRE), (2.03, Y_COFRE), (2.03, 0.80), (2.11, 0.79)]
    meio += _recuo((2.11, 0.79), (2.14, 0.52)) + [(2.14, 0.52), (2.10, 0.24), (2.10, Y_BASE)]
    m.perfil("pintura", meio, -(X_CAPO + 0.005), X_CAPO + 0.005, chanfro=0.012)
    # o que tapa o recuo entre a grade e os faróis e do lado de fora deles
    for s in (-1, 1):
        m.caixa("pintura", (0.11, Y_RECUO[1] - Y_RECUO[0], 0.05), (s * 0.355, sum(Y_RECUO) / 2, Z_RECUO + 0.025), chanfro=0)
        m.caixa("pintura", (0.11, Y_RECUO[1] - Y_RECUO[0], 0.04), (s * 0.795, sum(Y_RECUO) / 2, Z_RECUO + 0.02), chanfro=0)
    for s in (-1, 1):
        _forro_do_arco(m, EIXO, s * (X_CAPO + 0.02), s * (W - 0.012))
        _forro_do_arco(m, -EIXO, s * (X_MALAS + 0.01), s * (W - 0.012))
    # o cofre do motor (o fundo e a parede de trás; o motor é o Cofre.fbx)
    zf = 2.03 - Z_CAPO
    m.caixa("escuro", (2 * X_CAPO, 0.01, zf), (0, Y_COFRE + 0.006, (Z_CAPO + 2.03) / 2), chanfro=0)
    m.caixa("escuro", (2 * X_CAPO, _y_capo(Z_CAPO) - 0.03 - Y_COFRE, 0.01), (0, (_y_capo(Z_CAPO) - 0.03 + Y_COFRE) / 2, Z_CAPO + 0.006), chanfro=0)
    # a grade da ventilação na base do para-brisa (o "cowl")
    m.caixa("plastico", (1.12, 0.012, 0.07), (0, _y_capo(Z_CAPO) - 0.012, Z_CAPO - 0.03), giro=(-8, 0, 0), chanfro=0.003)
    for i in range(14):
        x = -0.52 + i * 0.08
        m.caixa("grade", (0.05, 0.004, 0.035), (x, _y_capo(Z_CAPO) - 0.004, Z_CAPO - 0.03), giro=(-8, 0, 0), chanfro=0)

    # ======================================================== trás
    tras = [(-0.96, Y_BASE), (-0.96, Y_CINTURA), (-2.08, 0.95), (-2.15, 0.93), (-2.16, 0.50), (-2.12, 0.24)]
    tras += _arco(-EIXO, False)
    for s in (-1, 1):
        m.perfil("pintura", tras, s * X_MALAS, s * W, chanfro=0.018)
    meio_tras = [(-0.96, Y_BASE), (-0.96, Y_PISO_MALAS), (-2.12, Y_PISO_MALAS), (-2.12, Y_SOLEIRA), (-2.158, Y_SOLEIRA),
                 (-2.16, 0.50), (-2.12, 0.24)]
    m.perfil("pintura", meio_tras, -X_MALAS, X_MALAS, chanfro=0.008)
    for s in (-1, 1):
        # o porta-malas por dentro: o forro das laterais (com o friso de carpete embaixo) e a caixa da roda
        m.caixa("painel", (0.012, Y_CINTURA - Y_PISO_MALAS, 1.12), (s * (X_MALAS - 0.006), (Y_CINTURA + Y_PISO_MALAS) / 2, -1.54), chanfro=0)
        m.caixa("carpete", (0.014, 0.10, 1.12), (s * (X_MALAS - 0.009), Y_PISO_MALAS + 0.05, -1.54), chanfro=0)
        m.caixa("carpete", (0.18, 0.14, 0.72), (s * (X_MALAS - 0.10), Y_PISO_MALAS + 0.07, -EIXO), chanfro=0.03, seg=2)
        # o canto entre a tampa e a coluna D
        m.ripa("pintura", (s * (X_MALAS + 0.09), 0.95, -2.09), (s * (X_MALAS + 0.09), 1.43, -1.91), 0.18, 0.03)
        # as argolas de amarrar a carga
        for z in (-1.05, -2.02):
            m.cilindro("int_cromo", 0.018, 0.008, (s * (X_MALAS - 0.014), Y_PISO_MALAS + 0.13, z), eixo="x", lados=10)
    m.caixa("carpete", (2 * X_MALAS - 0.02, 0.02, 1.14), (0, Y_PISO_MALAS + 0.01, -1.54), chanfro=0)
    # a soleira do porta-malas (cromada) e o fecho
    m.caixa("int_cromo", (2 * X_MALAS - 0.04, 0.012, 0.05), (0, Y_SOLEIRA + 0.006, -2.13), chanfro=0.003)
    m.caixa("int_plastico", (0.06, 0.02, 0.03), (0, Y_SOLEIRA + 0.012, -2.135), chanfro=0.004)

    # ======================================================== o assoalho, as soleiras, o túnel e o fundo
    m.caixa("carpete", (1.60, 0.10, 1.92), (0, 0.27, -0.02), chanfro=0)
    for s in (-1, 1):
        m.caixa("pintura", (0.08, 0.14, 1.92), (s * 0.82, 0.29, -0.02), chanfro=0.008)
        # o friso cromado da soleira (por dentro, na porta aberta)
        m.caixa("int_cromo", (0.05, 0.004, 1.84), (s * 0.80, 0.362, -0.02), chanfro=0)
    m.caixa("painel", (0.22, 0.14, 1.10), (0, 0.39, 0.15), chanfro=0.02, seg=2)
    m.caixa("escuro", (1.20, 0.02, 3.6), (0, 0.205, 0), chanfro=0)
    # os tapetes de borracha
    for x, z, c in ((-0.37, 0.42, 0.50), (0.37, 0.42, 0.50), (-0.38, -0.36, 0.30), (0.38, -0.36, 0.30)):
        m.caixa("tapete", (0.46, 0.008, c), (x, 0.324, z), chanfro=0.004)

    # ======================================================== teto, colunas, vidros e borrachas
    meio_teto = (PARABRISA[1][0] + VIDRO_TRAS[1][0]) / 2
    comp_teto = PARABRISA[1][0] - VIDRO_TRAS[1][0] + 0.04
    m.caixa("pintura", (1.62, 0.04, comp_teto), (0, Y_TETO - 0.02, meio_teto), chanfro=0.014, seg=2)
    m.caixa("forro", (1.54, 0.03, comp_teto - 0.06), (0, Y_TETO - 0.055, meio_teto), chanfro=0.006)
    for s in (-1, 1):
        # a calha do teto (cromada), dos dois lados
        m.caixa("cromo", (0.012, 0.014, comp_teto - 0.02), (s * 0.812, Y_TETO - 0.025, meio_teto), chanfro=0.003)
        # o bagageiro de teto: os pés pretos e o trilho cromado (um tubo com as pontas dobradas)
        zs = (-0.18, -1.0, -1.80)
        for z in zs:
            m.caixa("plastico", (0.05, 0.035, 0.10), (s * 0.63, Y_TETO + 0.0175, z), chanfro=0.008)
        m.tubo("cromo", [(s * 0.63, Y_TETO + 0.02, -0.12), (s * 0.63, Y_TETO + 0.05, -0.20), (s * 0.63, Y_TETO + 0.05, -1.78),
                         (s * 0.63, Y_TETO + 0.02, -1.86)], 0.014, lados=10)
    # as travessas do bagageiro
    for z in (-0.55, -1.45):
        m.tubo("cromo", [(-0.63, Y_TETO + 0.052, z), (0.63, Y_TETO + 0.052, z)], 0.011, lados=8)
        for s in (-1, 1):
            m.caixa("plastico", (0.04, 0.03, 0.04), (s * 0.63, Y_TETO + 0.052, z), chanfro=0.005)

    (zb, yb), (zt, yt) = PARABRISA
    for s in (-1, 1):
        # coluna A
        m.ripa("pintura", (s * 0.80, yb, zb + 0.01), (s * 0.78, yt, zt), 0.05, 0.08, chanfro=0.008)
        # coluna B (preta em cima; embaixo da linha das janelas, pintada e 1 cm mais funda que as portas)
        m.caixa("plastico", (0.05, Y_TETO - Y_CINTURA - 0.06, 0.07), (s * 0.81, (Y_TETO + Y_CINTURA) / 2 - 0.03, (Z_COLUNA_B + Z_PORTA_T) / 2), chanfro=0.004)
        m.caixa("pintura", (0.05, Y_CINTURA - 0.30, Z_COLUNA_B - Z_PORTA_T - 0.006), (s * 0.825, (Y_CINTURA + 0.30) / 2, (Z_COLUNA_B + Z_PORTA_T) / 2), chanfro=0)
        # coluna C e D
        m.caixa("pintura", (0.05, Y_TETO - Y_CINTURA - 0.06, 0.12), (s * 0.81, (Y_TETO + Y_CINTURA) / 2 - 0.03, Z_COLUNA_C - 0.06), chanfro=0.008)
        m.ripa("pintura", (s * 0.80, 0.95, -2.08), (s * 0.79, Y_TETO - 0.04, -1.90), 0.05, 0.14, chanfro=0.008)
        # o vidro de lado, atrás, com a borracha e o friso cromado em volta
        lado = [(-1.06, 0.95), (-1.06, 1.40), (-1.88, 1.40), (-2.03, 0.95)]
        m.perfil("vidro", lado, s * 0.798, s * 0.814)
        borda = [(-1.03, 0.935), (-1.03, 1.418), (-1.90, 1.418), (-2.06, 0.935)]
        for i in range(4):
            (za, ya), (zc, yc) = borda[i], borda[(i + 1) % 4]
            m.ripa("borracha", (s * 0.812, ya, za), (s * 0.812, yc, zc), 0.016, 0.03)
            m.ripa("cromo", (s * 0.822, ya, za), (s * 0.822, yc, zc), 0.006, 0.012)
        # os forros das colunas por dentro
        m.caixa("painel", (0.02, Y_TETO - Y_CINTURA - 0.10, 0.10), (s * 0.775, (Y_TETO + Y_CINTURA) / 2 - 0.04, (Z_COLUNA_B + Z_PORTA_T) / 2), chanfro=0.006)
        m.ripa("painel", (s * 0.77, yb + 0.03, zb - 0.02), (s * 0.755, yt - 0.03, zt - 0.02), 0.02, 0.07, chanfro=0.006)
        # a alça de segurar, em cima das portas de trás
        m.tubo("int_plastico", [(s * 0.74, 1.33, -0.42), (s * 0.70, 1.30, -0.46), (s * 0.70, 1.30, -0.62), (s * 0.74, 1.33, -0.66)], 0.011, lados=6)
    # o para-brisa, com a borracha e o friso cromado
    m.ripa("vidro", (0, yb, zb), (0, yt, zt), 1.58, 0.01)
    for s in (-1, 1):
        m.ripa("borracha", (s * 0.785, yb + 0.005, zb + 0.008), (s * 0.765, yt, zt + 0.004), 0.03, 0.022)
        m.ripa("cromo", (s * 0.80, yb + 0.005, zb + 0.016), (s * 0.78, yt + 0.005, zt + 0.01), 0.008, 0.014)
    m.ripa("borracha", (-0.78, yt - 0.002, zt + 0.006), (0.78, yt - 0.002, zt + 0.006), 0.03, 0.024)
    m.ripa("cromo", (-0.78, yt + 0.008, zt + 0.014), (0.78, yt + 0.008, zt + 0.014), 0.014, 0.008)
    m.ripa("borracha", (-0.80, yb + 0.004, zb + 0.01), (0.80, yb + 0.004, zb + 0.01), 0.03, 0.02)
    # os limpadores (o braço, a mola e a palheta de borracha), deitados na base do para-brisa
    d = (Vector((0, yt, zt)) - Vector((0, yb, zb))).normalized()
    for x in (-0.40, 0.08):
        p0 = Vector((x, yb + 0.03, zb - 0.045))
        p1 = p0 + Vector((0.40, 0, 0)) + d * 0.05
        m.cilindro("plastico", 0.016, 0.03, tuple(p0 + Vector((0, -0.01, 0))), eixo="y", lados=10)
        m.ripa("plastico", tuple(p0), tuple(p1), 0.014, 0.012)
        m.ripa("borracha", tuple(p0 + Vector((0.03, 0.01, 0.012))), tuple(p1 + Vector((0.04, 0.01, 0.012))), 0.01, 0.018)
    # o retrovisor de dentro e os quebra-sóis
    m.caixa("int_plastico", (0.24, 0.07, 0.03), (0, yt - 0.10, zt - 0.04), chanfro=0.01, seg=2)
    m.caixa("int_cromo", (0.22, 0.055, 0.004), (0, yt - 0.10, zt - 0.057), chanfro=0)
    m.ripa("int_plastico", (0, yt - 0.04, zt - 0.02), (0, yt - 0.08, zt - 0.04), 0.02, 0.02)
    for s in (-1, 1):
        m.caixa("forro", (0.36, 0.022, 0.15), (s * 0.40, Y_TETO - 0.085, zt - 0.10), giro=(6, 0, 0), chanfro=0.008)
        m.cilindro("int_plastico", 0.006, 0.36, (s * 0.40, Y_TETO - 0.075, zt - 0.03), eixo="x", lados=6)

    # ======================================================== frente
    _frente(m)
    # ======================================================== trás
    _traseira(m)
    # ======================================================== os lados: a madeira, os frisos e o resto
    for s in (-1, 1):
        # o para-lama da frente: a faixa de madeira acima do arco
        pts = _faixa(2.02, Z_PORTA_D + 0.015, Y_MADEIRA[0], Y_MADEIRA[1], arcos=(EIXO,), topo=lambda z: _y_capo(z) - 0.06)
        _madeira_de_lado(m, pts, s)
        # o painel de trás (a coluna das lanternas fica de fora)
        pts = _faixa(-0.975, -2.08, Y_MADEIRA[0], Y_MADEIRA[1], arcos=(-EIXO,))
        _madeira_de_lado(m, pts, s)
        # o friso cromado de baixo (a soleira de fora), nos pedaços sem roda
        for z0, z1 in ((Z_PORTA_D - 0.005, EIXO - R_ARCO - 0.06), (2.08, EIXO + R_ARCO + 0.05), (-0.97, -EIXO + R_ARCO + 0.05),
                       (-EIXO - R_ARCO - 0.05, -2.10)):
            if z0 > z1:
                m.caixa("cromo", (0.008, 0.022, z0 - z1), (s * (W + 0.002), 0.37, (z0 + z1) / 2), chanfro=0.002)
        # o pisca de lado, no para-lama
        m.caixa("pisca", (0.008, 0.03, 0.07), (s * (W + 0.006), 0.78, 1.95), chanfro=0.003)
    # a antena, no para-lama da direita
    m.cilindro("cromo", 0.012, 0.03, (0.72, _y_capo(1.05) + 0.01, 1.05), eixo="y", lados=8)
    m.cilindro("cromo", 0.0035, 0.62, (0.72, _y_capo(1.05) + 0.32, 1.05), eixo="y", lados=6)

    # ======================================================== o interior
    _painel(m)
    _bancos(m)
    return m.objeto("Perua_Carroceria", cena)


def _frente(m):
    """O para-choque cromado com a borracha, a grade, os 4 faróis quadrados (bisel cromado, refletor, lâmpada e a lente),
    os piscas de canto, a saia e a placa."""
    # o para-choque: a barra cromada, a faixa de borracha e as pontas que dobram para os lados
    m.caixa("cromo", (1.70, 0.13, 0.10), (0, 0.37, 2.17), chanfro=0.025, seg=2)
    m.caixa("borracha", (1.50, 0.035, 0.012), (0, 0.375, 2.222), chanfro=0.006)
    for s in (-1, 1):
        m.ripa("cromo", (s * 0.82, 0.37, 2.15), (s * 0.89, 0.37, 2.02), 0.10, 0.13, chanfro=0.02)
        m.caixa("borracha", (0.07, 0.10, 0.09), (s * 0.70, 0.36, 2.21), chanfro=0.02, seg=2)
        m.caixa("plastico", (0.06, 0.08, 0.18), (s * 0.30, 0.37, 2.07), chanfro=0.01)   # os suportes
    # a saia de baixo, com a entrada de ar
    m.caixa("plastico", (1.60, 0.07, 0.10), (0, 0.27, 2.08), chanfro=0.015)
    for i in range(5):
        m.caixa("grade", (0.70, 0.008, 0.03), (0, 0.255 + i * 0.012, 2.125), chanfro=0)
    # a placa da frente (no meio do para-choque)
    m.caixa("plastico", (0.38, 0.13, 0.012), (0, 0.37, 2.222), chanfro=0.004)
    m.caixa("placa_f", (0.36, 0.11, 0.008), (0, 0.37, 2.232), chanfro=0.002)
    for x in (-0.15, 0.15):
        m.cilindro("cromo", 0.008, 0.006, (x, 0.405, 2.237), eixo="z", lados=8)
    # a grade: o fundo escuro, a moldura cromada, as barras e o emblema
    xg, yg0, yg1 = 0.30, 0.56, 0.76
    m.caixa("grade", (2 * xg, yg1 - yg0, 2.145 - Z_RECUO), (0, (yg0 + yg1) / 2, (2.145 + Z_RECUO) / 2), chanfro=0)
    for i in range(7):
        y = yg0 + 0.02 + i * 0.027
        m.caixa("cromo", (2 * xg - 0.02, 0.007, 0.012), (0, y, 2.148), chanfro=0.002)
    for x in (-0.20, -0.10, 0.0, 0.10, 0.20):
        m.caixa("cromo", (0.007, yg1 - yg0 - 0.02, 0.012), (x, (yg0 + yg1) / 2, 2.150), chanfro=0.002)
    for p, q in (((-xg, yg0), (xg, yg0)), ((-xg, yg1), (xg, yg1)), ((-xg, yg0), (-xg, yg1)), ((xg, yg0), (xg, yg1))):
        m.ripa("cromo", (p[0], p[1], 2.155), (q[0], q[1], 2.155), 0.02, 0.02, chanfro=0.005)
    m.cilindro("cromo", 0.034, 0.012, (0, (yg0 + yg1) / 2, 2.166), eixo="z", lados=18, raio2=0.03)
    m.cilindro("pintura", 0.024, 0.006, (0, (yg0 + yg1) / 2, 2.173), eixo="z", lados=14)
    # a chapa em volta dos faróis (a frente pintada) e os faróis
    for s in (-1, 1):
        for xc in (0.49, 0.65):
            _farol(m, s * xc, 0.665, 2.135)
        # a moldura cromada dos dois faróis juntos
        x0, x1 = s * 0.405, s * 0.735
        for p, q in (((x0, 0.585), (x1, 0.585)), ((x0, 0.745), (x1, 0.745)), ((x0, 0.585), (x0, 0.745)), ((x1, 0.585), (x1, 0.745))):
            m.ripa("cromo", (p[0], p[1], 2.150), (q[0], q[1], 2.150), 0.016, 0.016, chanfro=0.004)
        # o pisca de canto (dobra para o lado)
        m.caixa("pisca", (0.07, 0.14, 0.05), (s * 0.80, 0.665, 2.11), giro=(0, s * 28, 0), chanfro=0.008)
        m.caixa("cromo", (0.075, 0.15, 0.04), (s * 0.80, 0.665, 2.10), giro=(0, s * 28, 0), chanfro=0.006)
        # o pisca de baixo, no para-choque
        m.caixa("pisca", (0.12, 0.04, 0.02), (s * 0.55, 0.37, 2.222), chanfro=0.006)


def _farol(m, x, y, z):
    """Um farol quadrado: a caixa escura, o refletor cromado (parabólico, achatado no retângulo), a lâmpada, o bisel e a
    lente (material Perua_Farol, que a Unity acende)."""
    lx, ly = 0.145, 0.125
    # a caixa aberta na frente: o fundo e as 4 paredes (do recuo da carroceria até a lente)
    prof = z + 0.012 - Z_RECUO
    zc = (z + 0.012 + Z_RECUO) / 2
    m.caixa("grade", (lx + 0.012, ly + 0.012, 0.004), (x, y, Z_RECUO + 0.002), chanfro=0)
    for s in (-1, 1):
        m.caixa("grade", (0.006, ly + 0.012, prof), (x + s * (lx / 2 + 0.003), y, zc), chanfro=0)
        m.caixa("grade", (lx + 0.012, 0.006, prof), (x, y + s * (ly / 2 + 0.003), zc), chanfro=0)
    # o refletor: um paraboloide em volta do Z, da boca (na lente) ao fundo
    perfil = []
    for i in range(7):
        t = i / 6
        r = 0.068 * (1 - t) + 0.012 * t
        perfil.append((r, z + 0.008 - 0.045 * (t ** 0.6)))
    m.torno("refletor", perfil, centro=(x, y, 0), eixo="z", lados=20, escala=(lx / 0.136, ly / 0.136), fechar=(False, True))
    m.cilindro("re", 0.012, 0.03, (x, y, z - 0.02), eixo="z", lados=10)
    # o bisel cromado
    for p, q in (((-1, -1), (1, -1)), ((-1, 1), (1, 1)), ((-1, -1), (-1, 1)), ((1, -1), (1, 1))):
        m.ripa("cromo", (x + p[0] * lx / 2, y + p[1] * ly / 2, z + 0.012), (x + q[0] * lx / 2, y + q[1] * ly / 2, z + 0.012), 0.012, 0.014, chanfro=0.003)
    # a lente, um pouco abaulada
    m.caixa("lente_farol", (lx - 0.008, ly - 0.008, 0.008), (x, y, z + 0.012), chanfro=0.003)


def _traseira(m):
    """O para-choque de trás, as lanternas altas (o vermelho que acende, a luz de ré e o refletor), o escape."""
    m.caixa("cromo", (1.74, 0.13, 0.10), (0, 0.37, -2.17), chanfro=0.025, seg=2)
    m.caixa("borracha", (1.50, 0.035, 0.012), (0, 0.375, -2.222), chanfro=0.006)
    m.caixa("borracha", (0.60, 0.012, 0.07), (0, 0.437, -2.17), chanfro=0.004)   # o degrau
    for s in (-1, 1):
        m.ripa("cromo", (s * 0.84, 0.37, -2.15), (s * 0.89, 0.37, -2.02), 0.10, 0.13, chanfro=0.02)
        m.caixa("borracha", (0.07, 0.10, 0.09), (s * 0.70, 0.36, -2.21), chanfro=0.02, seg=2)
        # a lanterna: o bisel cromado, o vermelho (acende), a ré (branca) e o refletor redondo
        x = s * 0.70
        m.caixa("cromo", (0.18, 0.39, 0.03), (x, 0.715, -2.158), chanfro=0.008)
        m.caixa("lente_lanterna", (0.15, 0.26, 0.012), (x, 0.775, -2.177), chanfro=0.004)
        m.caixa("re", (0.15, 0.07, 0.012), (x, 0.595, -2.177), chanfro=0.004)
        for k in range(1, 4):
            m.caixa("cromo", (0.152, 0.004, 0.004), (x, 0.775 - 0.13 + k * 0.065, -2.184), chanfro=0)
        m.cilindro("lente_lanterna", 0.022, 0.008, (s * 0.80, 0.45, -2.226), eixo="z", lados=12)   # o refletor no para-choque
    # o escape (o tubo e a ponteira)
    m.tubo("aco", [(-0.52, 0.23, -1.60), (-0.52, 0.23, -2.05), (-0.55, 0.235, -2.20)], 0.026, lados=10)
    m.cilindro("cromo", 0.032, 0.06, (-0.55, 0.235, -2.20), eixo="z", lados=12)


# ---------------------------------------------------------------------------------------------------- o interior
def _painel(m):
    """O painel (a mesma frente do carro.py: a face em z = 0,62 com a ignição e o quadro dos mostradores, que a Unity põe
    em (-0,37; 0,85; 0,626)), com o acolchoado de cima, o friso cromado, as saídas de ar, o rádio, o porta-luvas, a
    coluna da direção com as alavancas, os pedais, o console, o câmbio e o freio de mão."""
    painel = [(0.62, 0.60), (0.62, 0.78), (0.70, 0.90), (1.00, 0.92), (1.00, 0.55), (0.80, 0.52)]
    m.perfil("painel", painel, -0.80, 0.80, chanfro=0.012)
    # o acolchoado de cima (vinil), um pouco mais alto na borda
    m.perfil("vinil", [(0.615, 0.765), (0.615, 0.795), (0.70, 0.912), (0.98, 0.93), (0.98, 0.915), (0.70, 0.895), (0.63, 0.78)],
             -0.795, 0.795, chanfro=0.006, seg=1)
    # o friso cromado de lado a lado (interrompido no quadro do motorista)
    for x0, x1 in ((-0.79, -0.575), (-0.165, 0.79)):
        m.caixa("int_cromo", (x1 - x0, 0.012, 0.006), ((x0 + x1) / 2, 0.745, 0.615), chanfro=0.002)
    # a madeira do painel (embaixo do friso, no lado do passageiro)
    m.caixa("madeira_int", (0.60, 0.035, 0.004), (0.42, 0.715, 0.6175), chanfro=0)
    # o quadro dos mostradores: o fundo, o bisel cromado (a abertura passa um pouco do quadro da Unity, de 0,30 × 0,09)
    # e a viseira por cima. Nada fica na frente do plano z = 0,626, onde a Unity põe os mostradores
    m.caixa("int_plastico", (0.34, 0.12, 0.02), (-0.37, 0.85, 0.645), chanfro=0)
    x0, x1, y0, y1 = -0.37 - 0.162, -0.37 + 0.162, 0.85 - 0.056, 0.85 + 0.056
    for p, q in (((x0, y0), (x1, y0)), ((x0, y1), (x1, y1)), ((x0, y0), (x0, y1)), ((x1, y0), (x1, y1))):
        m.ripa("int_cromo", (p[0], p[1], 0.632), (q[0], q[1], 0.632), 0.01, 0.01)
    m.perfil("painel", [(0.62, 0.92), (0.64, 0.955), (0.80, 0.965), (0.82, 0.92)], -0.37 - 0.19, -0.37 + 0.19, chanfro=0.01)
    for s in (-1, 1):
        m.caixa("painel", (0.025, 0.12, 0.14), (-0.37 + s * 0.18, 0.86, 0.69), chanfro=0.008)
    # as saídas de ar (com as aletas): no meio e nas pontas
    for x in (-0.09, 0.09, -0.70, 0.62):
        w = 0.12 if abs(x) < 0.2 else 0.10
        m.caixa("int_plastico", (w + 0.016, 0.056, 0.012), (x, 0.84, 0.66 if abs(x) < 0.5 else 0.66), chanfro=0.004)
        for k in range(4):
            m.caixa("int_cromo" if k == 0 else "int_plastico", (w, 0.004, 0.02), (x, 0.822 + k * 0.012, 0.652), chanfro=0)
    # o rádio (toca-fitas): a frente, os dois botões, as teclas e a fenda da fita
    m.caixa("int_plastico", (0.20, 0.10, 0.02), (0, 0.70, 0.612), chanfro=0.004)
    m.caixa("int_cromo", (0.19, 0.004, 0.004), (0, 0.744, 0.601), chanfro=0)
    for x in (-0.075, 0.075):
        m.cilindro("int_cromo", 0.012, 0.016, (x, 0.70, 0.596), eixo="z", lados=12)
    m.caixa("grade", (0.09, 0.012, 0.004), (0, 0.72, 0.601), chanfro=0)
    for k in range(5):
        m.caixa("int_cromo", (0.014, 0.01, 0.008), (-0.028 + k * 0.014, 0.678, 0.6), chanfro=0.001)
    # o porta-luvas (a tampa, a fresta e o trinco cromado)
    m.caixa("painel", (0.46, 0.13, 0.008), (0.42, 0.655, 0.6185), chanfro=0.004)
    m.caixa("int_cromo", (0.06, 0.014, 0.008), (0.42, 0.695, 0.612), chanfro=0.002)
    # os botões do farol e do limpador (à esquerda do quadro)
    for y in (0.70, 0.66):
        m.cilindro("int_plastico", 0.012, 0.016, (-0.62, y, 0.612), eixo="z", lados=10)
    # a coluna da direção (a capa) e as alavancas do pisca e do limpador
    m.ripa("int_plastico", (-0.37, 0.74, 0.74), (-0.37, 0.84, 0.55), 0.085, 0.085, chanfro=0.015)
    for s in (-1, 1):
        m.ripa("int_plastico", (-0.37 + s * 0.04, 0.80, 0.585), (-0.37 + s * 0.17, 0.81, 0.565), 0.012, 0.012)
        m.cilindro("int_plastico", 0.009, 0.03, (-0.37 + s * 0.18, 0.81, 0.564), eixo="x", lados=8)
    # os pedais (a embreagem, o freio e o acelerador)
    for x, w, h in ((-0.50, 0.07, 0.06), (-0.37, 0.08, 0.06), (-0.24, 0.05, 0.11)):
        m.ripa("int_plastico", (x, 0.55, 0.88), (x, 0.40, 0.80), 0.015, 0.015)
        m.caixa("tapete", (w, h, 0.012), (x, 0.40, 0.795), giro=(20, 0, 0), chanfro=0.003)
    # o console (entre os bancos da frente), o câmbio e o freio de mão (nos lugares do carro.py)
    m.caixa("painel", (0.24, 0.10, 0.52), (0, 0.47, 0.30), chanfro=0.02, seg=2)
    m.caixa("int_plastico", (0.12, 0.012, 0.14), (0, 0.522, 0.46), chanfro=0.01)    # a coifa do câmbio
    m.ripa("int_cromo", (0, 0.50, 0.48), (0, 0.62, 0.46), 0.016, 0.016)
    m.cilindro("int_plastico", 0.028, 0.03, (0, 0.635, 0.457), eixo="y", lados=12)
    m.ripa("int_plastico", (0, 0.47, -0.02), (0, 0.53, 0.22), 0.04, 0.03, chanfro=0.008)
    m.ripa("int_cromo", (0, 0.505, -0.03), (0, 0.515, -0.05), 0.012, 0.012)
    m.caixa("int_plastico", (0.08, 0.02, 0.20), (0, 0.45, 0.10), chanfro=0.006)
    # o cinzeiro (no console)
    m.caixa("int_cromo", (0.10, 0.004, 0.05), (0, 0.522, 0.28), chanfro=0.001)


def _bancos(m):
    """Os bancos: os dois da frente (concha de vinil com gomos, as laterais, o encosto de cabeça nas hastes cromadas e o
    trilho) e o inteiriço de trás, com os cintos. As medidas (assento, encosto, cabeça) são as do carro.py: o quadril de
    quem senta é o mesmo."""
    for s in (-1, 1):
        x = s * 0.37
        m.caixa("int_plastico", (0.40, 0.08, 0.40), (x, 0.36, 0.08), chanfro=0.01)
        for xx in (x - 0.16, x + 0.16):
            m.caixa("int_cromo", (0.03, 0.02, 0.56), (xx, 0.33, 0.10), chanfro=0.003)
        # o assento
        m.entrar(Matrix.Translation((x, 0.44, 0.10)) @ Matrix.Rotation(math.radians(-6), 4, "X"))
        _almofada(m, 0.50, 0.14, 0.50, gomos=5, eixo="z", vinco=0.012)
        m.sair()
        # o encosto
        m.entrar(Matrix.Translation((x, 0.80, -0.22)) @ Matrix.Rotation(math.radians(-14), 4, "X"))
        _almofada(m, 0.50, 0.64, 0.12, gomos=5, eixo="y", vinco=0.012, frente=True)
        m.sair()
        # o encosto de cabeça e as hastes
        m.entrar(Matrix.Translation((x, 1.20, -0.31)) @ Matrix.Rotation(math.radians(-14), 4, "X"))
        m.caixa("vinil", (0.26, 0.16, 0.10), (0, 0, 0), chanfro=0.03, seg=2)
        for xx in (-0.07, 0.07):
            m.cilindro("int_cromo", 0.006, 0.10, (xx, -0.12, 0), eixo="y", lados=8)
        m.sair()
        # o cinto (do pilar B até o lado de fora do assento) e a fivela do lado de dentro
        xo = s * 0.76
        m.ripa("cinto", (xo, 1.27, -0.13), (s * 0.66, 0.92, -0.20), 0.004, 0.045)
        m.ripa("cinto", (s * 0.66, 0.92, -0.20), (s * 0.63, 0.47, -0.10), 0.004, 0.045)
        m.caixa("int_plastico", (0.03, 0.06, 0.03), (xo, 1.27, -0.13), chanfro=0.006)
        m.caixa("int_plastico", (0.03, 0.08, 0.04), (s * 0.10, 0.50, -0.06), chanfro=0.008)
        m.caixa("int_cromo", (0.024, 0.02, 0.026), (s * 0.10, 0.545, -0.06), chanfro=0.003)
    # o banco de trás
    m.entrar(Matrix.Translation((0, 0.45, -0.70)) @ Matrix.Rotation(math.radians(-5), 4, "X"))
    _almofada(m, 1.36, 0.16, 0.46, gomos=11, eixo="z", vinco=0.012)
    m.sair()
    m.entrar(Matrix.Translation((0, 0.80, -0.99)) @ Matrix.Rotation(math.radians(-16), 4, "X"))
    _almofada(m, 1.36, 0.58, 0.12, gomos=11, eixo="y", vinco=0.012, frente=True)
    m.sair()
    for s in (-1, 1):
        m.entrar(Matrix.Translation((s * 0.38, 1.14, -1.10)) @ Matrix.Rotation(math.radians(-16), 4, "X"))
        m.caixa("vinil", (0.24, 0.14, 0.09), (0, 0, 0), chanfro=0.028, seg=2)
        for xx in (-0.06, 0.06):
            m.cilindro("int_cromo", 0.006, 0.08, (xx, -0.10, 0), eixo="y", lados=8)
        m.sair()
        m.ripa("cinto", (s * 0.77, 1.25, -1.02), (s * 0.68, 0.95, -1.06), 0.004, 0.045)
        m.ripa("cinto", (s * 0.68, 0.95, -1.06), (s * 0.62, 0.53, -0.92), 0.004, 0.045)
        m.caixa("int_plastico", (0.03, 0.06, 0.03), (s * 0.77, 1.25, -1.02), chanfro=0.006)


def _almofada(m, lx, ly, lz, gomos, eixo, vinco, frente=False):
    """Uma almofada de vinil: o miolo e os gomos (rolinhos) na face de cima (eixo z) ou da frente (eixo y)."""
    m.caixa("vinil", (lx, ly, lz), (0, 0, 0), chanfro=min(0.03, min(lx, ly, lz) * 0.3), seg=2)
    borda = 0.07 if lx < 1 else 0.06
    largura = (lx - 2 * borda) / gomos
    for k in range(gomos):
        xc = -lx / 2 + borda + largura * (k + 0.5)
        if eixo == "z":
            # os gomos no sentido do comprimento, em cima do assento
            m.caixa("vinil", (largura - vinco, 0.02, lz - 0.08), (xc, ly / 2 + 0.004, 0.0), chanfro=0.008)
        else:
            m.caixa("vinil", (largura - vinco, ly - 0.14, 0.02), (xc, -0.01, lz / 2 + 0.004), chanfro=0.008)
    # as laterais (o reforço das bordas)
    for s in (-1, 1):
        if eixo == "z":
            m.caixa("vinil", (borda - 0.01, 0.035, lz - 0.04), (s * (lx / 2 - borda / 2), ly / 2 + 0.01, 0), chanfro=0.012)
        else:
            m.caixa("vinil", (borda - 0.01, ly - 0.06, 0.04), (s * (lx / 2 - borda / 2), 0, lz / 2 + 0.012), chanfro=0.012)


# ---------------------------------------------------------------------------------------------------- as portas
def _porta(cena, nome, s, frente):
    m = Malha()
    if frente:
        z0, z1 = Z_PORTA_D, Z_COLUNA_B
    else:
        z0, z1 = Z_PORTA_T, Z_COLUNA_C
    meio, comp = (z0 + z1) / 2, z0 - z1
    y0 = 0.36
    # a chapa de fora (com a dobra da cintura), o forro de dentro e o descanso de braço
    m.caixa("pintura", (0.035, Y_CINTURA - y0, comp), (s * 0.8425, (Y_CINTURA + y0) / 2, meio), chanfro=0.01, seg=2)
    # a madeira e o friso
    pts = [(z0 - 0.025, Y_MADEIRA[1]), (z1 + 0.025, Y_MADEIRA[1]), (z1 + 0.025, Y_MADEIRA[0]), (z0 - 0.025, Y_MADEIRA[0])]
    _madeira_de_lado(m, pts, s)
    # o friso cromado de baixo
    m.caixa("cromo", (0.008, 0.022, comp - 0.012), (s * (W + 0.002), 0.37, meio), chanfro=0.002)
    # o forro de dentro: embaixo vinil, em cima a chapa pintada, o friso de madeira, o descanso de braço com a alça
    m.caixa("vinil", (0.03, 0.40, comp - 0.05), (s * 0.81, 0.58, meio), chanfro=0.008)
    m.caixa("painel", (0.03, Y_CINTURA - 0.78, comp - 0.05), (s * 0.812, (Y_CINTURA + 0.78) / 2, meio), chanfro=0.006)
    m.caixa("madeira_int", (0.006, 0.03, comp - 0.10), (s * 0.794, 0.80, meio), chanfro=0)
    m.caixa("vinil", (0.07, 0.045, comp * 0.42), (s * 0.765, 0.66, meio - comp * 0.08), chanfro=0.012, seg=2)
    m.tubo("int_cromo", [(s * 0.79, 0.69, meio + comp * 0.10), (s * 0.755, 0.71, meio + comp * 0.07),
                         (s * 0.755, 0.71, meio - comp * 0.20), (s * 0.79, 0.69, meio - comp * 0.23)], 0.008, lados=6)
    # a maçaneta de dentro (cromada, numa concha preta) e a manivela do vidro
    zm = z0 - 0.17
    m.caixa("int_plastico", (0.01, 0.05, 0.10), (s * 0.793, 0.76, zm), chanfro=0.004)
    m.caixa("int_cromo", (0.012, 0.02, 0.07), (s * 0.788, 0.76, zm), chanfro=0.004)
    zc = meio - comp * 0.05
    m.cilindro("int_cromo", 0.016, 0.02, (s * 0.788, 0.55, zc), eixo="x", lados=10)
    m.ripa("int_cromo", (s * 0.778, 0.55, zc), (s * 0.778, 0.49, zc - 0.05), 0.012, 0.008)
    m.cilindro("int_plastico", 0.012, 0.035, (s * 0.762, 0.49, zc - 0.05), eixo="x", lados=8)
    # o pino da trava, na cintura
    m.cilindro("int_plastico", 0.006, 0.04, (s * 0.80, Y_CINTURA + 0.01, z1 + 0.10), eixo="y", lados=6)
    if frente:
        # o alto-falante embaixo
        m.cilindro("alto_falante", 0.065, 0.01, (s * 0.792, 0.47, meio + 0.18), eixo="x", lados=16)
    # a maçaneta de fora (cromada, de puxar, numa concha) e a fechadura
    m.caixa("plastico", (0.006, 0.04, 0.15), (s * (W + 0.002), 0.885, z1 + 0.13), chanfro=0.004)
    m.caixa("cromo", (0.018, 0.024, 0.14), (s * (W + 0.009), 0.885, z1 + 0.13), chanfro=0.006)
    if frente:
        m.cilindro("cromo", 0.011, 0.012, (s * (W + 0.006), 0.885, z1 + 0.25), eixo="x", lados=10)
    # a janela: a moldura (borracha preta com o friso cromado por fora) e o vidro
    yt = Y_TETO - 0.08
    if frente:
        (zb, yb), (zt, ytp) = PARABRISA
        fa, fb = (zb - 0.10, Y_CINTURA), (zt - 0.04, yt)
        m.ripa("borracha", (s * 0.82, fa[1], fa[0]), (s * 0.82, fb[1], fb[0]), 0.05, 0.04)
        m.caixa("borracha", (0.05, 0.04, fb[0] - z1), (s * 0.82, yt, (fb[0] + z1) / 2), chanfro=0)
        m.caixa("borracha", (0.05, yt - Y_CINTURA, 0.04), (s * 0.82, (yt + Y_CINTURA) / 2, z1 + 0.02), chanfro=0)
        m.ripa("cromo", (s * 0.848, fa[1], fa[0] + 0.01), (s * 0.848, fb[1] + 0.02, fb[0]), 0.006, 0.012)
        m.caixa("cromo", (0.006, 0.012, fb[0] - z1), (s * 0.848, yt + 0.02, (fb[0] + z1) / 2), chanfro=0)
        vidro = [(fa[0] - 0.03, Y_CINTURA + 0.01), (fb[0] + 0.0, yt - 0.02), (z1 + 0.04, yt - 0.02), (z1 + 0.04, Y_CINTURA + 0.01)]
        # o retrovisor de fora: a base preta, o braço, a caixa cromada e o espelho
        m.caixa("plastico", (0.04, 0.07, 0.10), (s * 0.875, 0.97, zb - 0.16), chanfro=0.01)
        m.ripa("plastico", (s * 0.88, 0.98, zb - 0.16), (s * 0.95, 1.00, zb - 0.17), 0.03, 0.03)
        m.caixa("cromo", (0.10, 0.12, 0.07), (s * 0.97, 1.00, zb - 0.165), chanfro=0.025, seg=2)
        m.caixa("refletor", (0.085, 0.10, 0.006), (s * 0.97, 1.00, zb - 0.203), chanfro=0.002)
    else:
        for z in (z0 - 0.02, z1 + 0.02):
            m.caixa("borracha", (0.05, yt - Y_CINTURA, 0.04), (s * 0.82, (yt + Y_CINTURA) / 2, z), chanfro=0)
        m.caixa("borracha", (0.05, 0.04, comp), (s * 0.82, yt, meio), chanfro=0)
        m.caixa("cromo", (0.006, 0.012, comp), (s * 0.848, yt + 0.02, meio), chanfro=0)
        vidro = [(z0 - 0.04, Y_CINTURA + 0.01), (z0 - 0.04, yt - 0.02), (z1 + 0.04, yt - 0.02), (z1 + 0.04, Y_CINTURA + 0.01)]
    # a borracha de baixo da janela (por fora, com o friso)
    m.caixa("borracha", (0.016, 0.014, comp - 0.02), (s * 0.853, Y_CINTURA + 0.004, meio), chanfro=0.003)
    m.caixa("cromo", (0.006, 0.008, comp - 0.02), (s * 0.862, Y_CINTURA + 0.008, meio), chanfro=0)
    m.perfil("vidro", vidro, s * 0.812, s * 0.828)
    return m.objeto(nome, cena, pivo=(s * W, 0, z0))


# ---------------------------------------------------------------------------------------------------- roda, volante, capô, tampa
def _roda(cena):
    """O pneu (o perfil de lado, com a banda de rodagem), o aro de aço, a calota cromada com as aletas, o cubo com os 5
    parafusos e, por dentro, o tambor do freio. A origem no meio, o eixo em X, a calota para +X; raio 0,30, largura
    0,21 (a do cilindro do tiro)."""
    m = Malha()
    h = MEIA_LARGURA_PNEU
    pneu = [(0.198, -h + 0.012), (0.215, -h), (0.255, -h + 0.002), (0.285, -h + 0.012), (0.298, -h + 0.03), (R_RODA, -h + 0.045),
            (R_RODA, h - 0.045), (0.298, h - 0.03), (0.285, h - 0.012), (0.255, h - 0.002), (0.215, h), (0.198, h - 0.012)]
    m.torno("pneu", pneu, lados=36)
    # o aro (o lado de dentro do pneu, o tambor do aro)
    m.torno("aco", [(0.198, h - 0.012), (0.19, h - 0.02), (0.19, -h + 0.02), (0.198, -h + 0.012)], lados=28)
    # o tambor do freio e a chapa (por dentro)
    m.torno("aco", [(0.0, -0.04), (0.15, -0.04), (0.15, -0.085), (0.12, -0.09), (0.0, -0.09)], lados=20, liso=False)
    # a calota: o aro cromado, o prato com as aletas e o miolo aberto
    calota = [(0.205, h - 0.006), (0.200, h + 0.004), (0.185, h + 0.010), (0.150, h + 0.012), (0.120, h + 0.018),
              (0.100, h + 0.024), (0.092, h + 0.022), (0.088, h + 0.008)]
    m.torno("cromo", calota, lados=36)
    for i in range(12):
        a = 2 * math.pi * i / 12
        r0, r1 = 0.105, 0.175
        p0 = Vector((h + 0.022, r0 * math.cos(a), r0 * math.sin(a)))
        p1 = Vector((h + 0.012, r1 * math.cos(a), r1 * math.sin(a)))
        m.ripa("aco", tuple(p0), tuple(p1), 0.012, 0.010)
    # o cubo de aço dentro do miolo, os 5 parafusos e a tampinha cromada
    m.torno("aco", [(0.088, h + 0.004), (0.0, h + 0.004)], lados=24, liso=False)
    for i in range(5):
        a = 2 * math.pi * i / 5 + math.pi / 10
        m.cilindro("cromo", 0.011, 0.022, (h + 0.014, 0.058 * math.cos(a), 0.058 * math.sin(a)), eixo="x", lados=6, liso=False)
    m.torno("cromo", [(0.032, h + 0.004), (0.032, h + 0.020), (0.022, h + 0.028), (0.0, h + 0.030)], lados=20)
    return m.objeto("Perua_Roda", cena)


def _volante(cena):
    """O volante de 2 raios dos anos 90: o aro (com os gomos de pegada), os raios, o miolo da buzina e a capa da coluna.
    A origem no meio, o aro no plano XY, a coluna para +Z."""
    m = Malha()
    m.toro("vinil", 0.19, 0.017, lados=36, gomos=8, achatar=1.15)
    # os raios: dois na horizontal (como no volante antigo: a ignição aparece pelo vão de baixo, à direita, do volante;
    # inclinados para baixo, eles a tapavam do olho mais alto)
    for s in (-1, 1):
        m.ripa("painel", (s * 0.055, -0.01, 0.025), (s * 0.18, -0.02, 0.0), 0.05, 0.014, chanfro=0.005)
    m.ripa("painel", (0, -0.06, 0.025), (0, -0.175, 0.002), 0.035, 0.012, chanfro=0.004)
    # o miolo (a buzina), acolchoado, com o emblema
    m.caixa("vinil", (0.13, 0.10, 0.045), (0, -0.02, 0.025), chanfro=0.018, seg=2)
    m.cilindro("int_cromo", 0.016, 0.006, (0, -0.02, 0.0), eixo="z", lados=14)
    m.cilindro("painel", 0.035, 0.07, (0, 0, 0.075), eixo="z", lados=14)
    return m.objeto("Perua_Volante", cena)


def _capo(cena):
    m = Malha()
    zt, zf = Z_CAPO, 2.085
    cima = [(zt, _y_capo(zt)), (1.60, _y_capo(1.60) + 0.006), (2.02, 0.84), (zf, 0.805)]
    baixo = [(zf - 0.012, 0.783), (2.02, 0.815), (zt, _y_capo(zt) - 0.025)]
    m.perfil("pintura", cima + baixo, -X_CAPO, X_CAPO, chanfro=0.008)
    # o vinco do meio (dois frisos de chapa) e a borda da frente
    for s in (-1, 1):
        m.ripa("pintura", (s * 0.30, _y_capo(zt + 0.05) + 0.002, zt + 0.05), (s * 0.30, _y_capo(2.0) + 0.002, 2.0), 0.03, 0.006, chanfro=0.002)
    # o reforço por baixo
    yb = lambda z: _y_capo(z) - 0.03
    for a, b in (((-X_CAPO + 0.08, zt + 0.08), (X_CAPO - 0.08, 1.95)), ((X_CAPO - 0.08, zt + 0.08), (-X_CAPO + 0.08, 1.95))):
        m.ripa("escuro", (a[0], yb(a[1]), a[1]), (b[0], yb(b[1]), b[1]), 0.05, 0.01)
    m.ripa("escuro", (-X_CAPO + 0.04, yb(1.98), 1.98), (X_CAPO - 0.04, yb(1.98), 1.98), 0.06, 0.012)
    return m.objeto("Perua_Capo", cena, pivo=(0, _y_capo(zt), zt))


def _tampa(cena):
    m = Malha()
    x = X_MALAS - 0.005
    (zb, yb), (zt, yt) = VIDRO_TRAS
    chapa = [(-2.158, Y_SOLEIRA), (-2.15, 0.93), (zb + 0.01, yb + 0.005), (zb + 0.05, yb - 0.02), (-2.10, 0.92), (-2.115, Y_SOLEIRA)]
    m.perfil("pintura", chapa, -x, x, chanfro=0.008)
    # a madeira e o friso da tampa (a placa fica em cima da madeira, no meio)
    zf = -2.158
    moldura = [(-x + 0.03, 0.645), (x - 0.03, 0.645), (x - 0.03, 0.905), (-x + 0.03, 0.905)]
    madeira = [(-x + 0.03 + FRISO, 0.645 + FRISO), (x - 0.03 - FRISO, 0.645 + FRISO), (x - 0.03 - FRISO, 0.905 - FRISO), (-x + 0.03 + FRISO, 0.905 - FRISO)]
    m.frente("friso", moldura, zf - 0.0005, zf - 0.0045)
    m.frente("madeira", madeira, zf - 0.0005, zf - 0.0068)
    # o vidro, a borracha e o friso cromado em volta
    m.ripa("vidro", (0, yb, zb), (0, yt, zt), 2 * x - 0.08, 0.01)
    for s in (-1, 1):
        m.ripa("borracha", (s * (x - 0.025), yb, zb), (s * (x - 0.025), yt, zt), 0.05, 0.03)
        m.ripa("cromo", (s * (x - 0.004), yb, zb - 0.012), (s * (x - 0.004), yt, zt - 0.012), 0.008, 0.012)
    m.ripa("borracha", (-x + 0.02, yb + 0.004, zb - 0.002), (x - 0.02, yb + 0.004, zb - 0.002), 0.03, 0.035)
    m.caixa("pintura", (2 * x, 0.03, 0.08), (0, yt + 0.015, zt + 0.01), chanfro=0.008)
    # o limpador de trás
    m.cilindro("plastico", 0.014, 0.03, (0, yb + 0.04, zb - 0.02), eixo="z", lados=10)
    m.ripa("plastico", (0, yb + 0.04, zb - 0.02), (0.30, yb + 0.20, zb + 0.028), 0.012, 0.01)
    m.ripa("borracha", (0.05, yb + 0.07, zb - 0.002), (0.34, yb + 0.25, zb + 0.032), 0.008, 0.014)
    # a placa, a luz da placa e a maçaneta cromada
    m.caixa("plastico", (0.38, 0.135, 0.012), (0, 0.74, zf - 0.008), chanfro=0.004)
    m.caixa("placa_t", (0.36, 0.115, 0.008), (0, 0.74, zf - 0.016), chanfro=0.002)
    m.caixa("cromo", (0.16, 0.025, 0.035), (0, 0.818, zf - 0.016), chanfro=0.008)
    m.caixa("re", (0.12, 0.008, 0.02), (0, 0.806, zf - 0.02), chanfro=0.002)
    m.caixa("cromo", (0.30, 0.035, 0.03), (0, 0.88, zf - 0.012), chanfro=0.01, seg=2)
    m.cilindro("cromo", 0.011, 0.012, (0.20, 0.88, zf - 0.012), eixo="z", lados=10)
    # o forro de dentro
    m.caixa("painel", (2 * x - 0.06, 0.26, 0.012), (0, 0.78, -2.092), chanfro=0.004)
    return m.objeto("Perua_Tampa", cena, pivo=PIVO_TAMPA)


# ---------------------------------------------------------------------------------------------------- os materiais
class _Nos:
    """Ajuda a montar os nós de um material (as entradas aceitam números, cores ou saídas de outros nós)."""

    def __init__(self, mat):
        self.nt = mat.node_tree
        self.x = -1400

    def no(self, tipo, **props):
        n = self.nt.nodes.new(tipo)
        for k, v in props.items():
            setattr(n, k, v)
        n.location = (self.x, 0)
        self.x += 30
        return n

    def por(self, entrada, v):
        if isinstance(v, bpy.types.NodeSocket):
            self.nt.links.new(v, entrada)
        elif isinstance(v, (tuple, list)):
            entrada.default_value = v if len(entrada.default_value) == len(v) else tuple(v) + (1.0,) * (len(entrada.default_value) - len(v))
        else:
            entrada.default_value = v

    def math(self, op, a, b=0.0, c=None, clamp=False):
        n = self.no("ShaderNodeMath", operation=op, use_clamp=clamp)
        self.por(n.inputs[0], a)
        self.por(n.inputs[1], b)
        if c is not None:
            self.por(n.inputs[2], c)
        return n.outputs[0]

    def faixa(self, x, a, b, de=0.0, ate=1.0):
        """smoothstep de a até b (vira de para ate)."""
        n = self.no("ShaderNodeMapRange", interpolation_type="SMOOTHSTEP", clamp=True)
        self.por(n.inputs["Value"], x)
        n.inputs["From Min"].default_value = a
        n.inputs["From Max"].default_value = b
        n.inputs["To Min"].default_value = de
        n.inputs["To Max"].default_value = ate
        return n.outputs["Result"]

    def mixf(self, fator, a, b):
        n = self.no("ShaderNodeMix", data_type="FLOAT", clamp_factor=True)
        self.por(n.inputs["Factor"], fator)
        self.por(_entrada(n, "A_Float"), a)
        self.por(_entrada(n, "B_Float"), b)
        return _saida(n, "Result_Float")

    def mixc(self, fator, a, b):
        n = self.no("ShaderNodeMix", data_type="RGBA", clamp_factor=True)
        self.por(n.inputs["Factor"], fator)
        self.por(_entrada(n, "A_Color"), a)
        self.por(_entrada(n, "B_Color"), b)
        return _saida(n, "Result_Color")

    def ruido(self, vetor, escala, detalhe=3.0, aspereza=0.55, distorcao=0.0, cor=False):
        n = self.no("ShaderNodeTexNoise")
        try:
            n.noise_dimensions = "3D"
        except Exception:
            pass
        self.por(n.inputs["Vector"], vetor)
        n.inputs["Scale"].default_value = escala
        n.inputs["Detail"].default_value = detalhe
        n.inputs["Roughness"].default_value = aspereza
        n.inputs["Distortion"].default_value = distorcao
        return n.outputs["Color"] if cor else n.outputs["Fac"]

    def vetor(self, a, escala):
        """O vetor a escalado por (sx, sy, sz)."""
        n = self.no("ShaderNodeVectorMath", operation="MULTIPLY")
        self.por(n.inputs[0], a)
        n.inputs[1].default_value = escala
        return n.outputs[0]

    def separar(self, v):
        n = self.no("ShaderNodeSeparateXYZ")
        self.por(n.inputs[0], v)
        return n.outputs


def _entrada(n, ident):
    return next(s for s in n.inputs if s.identifier == ident)


def _saida(n, ident):
    return next(s for s in n.outputs if s.identifier == ident)


# as cores (sRGB 0-255)
CORES = {
    "pintura": COR_PINTURA, "primer": (118, 116, 108), "ferrugem": (110, 52, 22), "ferrugem_escura": (58, 30, 16),
    "sujeira": (84, 72, 56), "lama": (62, 52, 40),
    "madeira_clara": (128, 78, 42), "madeira_escura": (58, 30, 16), "friso": (196, 166, 112),
    "cromo": (225, 225, 220), "borracha": (14, 14, 14), "plastico": (22, 22, 22), "grade": (10, 10, 10),
    "escuro": (20, 18, 16), "aco": (120, 120, 118), "placa": (222, 222, 210), "letra": (24, 26, 30),
    "pisca": (230, 120, 20), "re": (235, 235, 230), "lente_farol": (238, 240, 236), "lente_lanterna": (150, 12, 8),
    "vinil": (52, 132, 140), "painel": (30, 78, 86), "carpete": (26, 50, 54), "forro": (176, 172, 158),
    "int_plastico": (20, 30, 32), "cinto": (34, 56, 62), "tapete": (16, 16, 16), "alto_falante": (14, 18, 20),
}

# metal, aspereza e o quanto cada superfície envelhece: (metal, aspereza, sujeira, ferrugem, desgaste das bordas)
PROPRIEDADES = {
    "pintura": (0.0, 0.48, 1.0, 1.0, 1.0), "madeira": (0.0, 0.52, 0.9, 0.0, 0.5), "friso": (0.0, 0.55, 0.9, 0.0, 0.4),
    "cromo": (0.75, 0.22, 0.7, 0.25, 0.0), "borracha": (0.0, 0.88, 0.7, 0.0, 0.0), "pneu": (0.0, 0.92, 1.0, 0.0, 0.0),
    "plastico": (0.0, 0.72, 0.8, 0.0, 0.0), "grade": (0.0, 0.7, 0.6, 0.2, 0.0), "escuro": (0.0, 0.9, 1.0, 0.6, 0.0),
    "aco": (0.85, 0.5, 0.9, 1.0, 0.0), "placa_f": (0.2, 0.5, 0.8, 0.25, 0.0), "placa_t": (0.2, 0.5, 0.8, 0.25, 0.0),
    "pisca": (0.0, 0.12, 0.5, 0.0, 0.0), "re": (0.0, 0.12, 0.5, 0.0, 0.0), "refletor": (0.8, 0.12, 0.3, 0.1, 0.0),
    "lente_farol": (0.0, 0.06, 0.25, 0.0, 0.0), "lente_lanterna": (0.0, 0.12, 0.4, 0.0, 0.0),
    "vinil": (0.0, 0.42, 0.45, 0.0, 0.4), "painel": (0.0, 0.62, 0.35, 0.0, 0.3), "carpete": (0.0, 1.0, 0.8, 0.0, 0.0),
    "forro": (0.0, 0.92, 0.5, 0.0, 0.0), "int_cromo": (0.75, 0.25, 0.3, 0.15, 0.0), "int_plastico": (0.0, 0.65, 0.4, 0.0, 0.0),
    "madeira_int": (0.0, 0.4, 0.3, 0.0, 0.5), "cinto": (0.0, 0.85, 0.4, 0.0, 0.0), "tapete": (0.0, 0.9, 1.0, 0.0, 0.0),
    "alto_falante": (0.0, 0.8, 0.4, 0.0, 0.0), "vidro": (0.0, 0.05, 0.0, 0.0, 0.0),
}

_IMAGENS = {}
VERSAO_DOS_MATERIAIS = 8   # mude para refazer os materiais de autoria ao recarregar


def _imagem_do_atlas(atlas, canal):
    nome = f"Perua_{atlas}_{canal}"
    img = bpy.data.images.get(nome)
    tam = TAMANHO_ATLAS.get(atlas, 64)
    if img is None or img.size[0] != tam:
        if img is not None:
            bpy.data.images.remove(img)
        img = bpy.data.images.new(nome, tam, tam, alpha=False, float_buffer=False)
        img.colorspace_settings.name = "sRGB" if canal == "cor" else "Non-Color"
    return img


def _material_de_autoria(sup):
    nome = "Aut_" + sup
    mat = bpy.data.materials.get(nome)
    if mat is not None and mat.get("versao") == VERSAO_DOS_MATERIAIS:
        return mat
    if mat is None:
        mat = bpy.data.materials.new(nome)
    mat["versao"] = VERSAO_DOS_MATERIAIS
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    n = _Nos(mat)
    saida = n.no("ShaderNodeOutputMaterial")
    bsdf = n.no("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], saida.inputs["Surface"])
    metal, asp, k_suj, k_ferr, k_borda = PROPRIEDADES[sup]

    if sup == "vidro":
        bsdf.inputs["Base Color"].default_value = (0.08, 0.10, 0.11, 1)
        bsdf.inputs["Alpha"].default_value = 0.3
        bsdf.inputs["Roughness"].default_value = 0.05
        mat.diffuse_color = (0.08, 0.1, 0.11, 0.3)
        try:
            mat.surface_render_method = "BLENDED"
        except Exception:
            pass
        tex = n.no("ShaderNodeTexImage", name="BAKE")
        tex.image = _imagem_do_atlas("V", "cor")
        nt.nodes.active = tex
        return mat

    geo = n.no("ShaderNodeNewGeometry")
    pos = geo.outputs["Position"]
    nor = geo.outputs["Normal"]
    obj = n.no("ShaderNodeTexCoord").outputs["Object"]
    px, py, pz = n.separar(pos)          # no Blender: z é a altura, y o comprimento do carro (a frente em -y)
    nx, ny, nz = n.separar(nor)
    altura = pz

    # -- as máscaras: o sol de cima, a sujeira de baixo e das frestas, as bordas gastas, a ferrugem
    ao_cav = n.no("ShaderNodeAmbientOcclusion", samples=12, inside=False, only_local=False)
    ao_cav.inputs["Distance"].default_value = 0.12
    cavidade = n.math("SUBTRACT", 1.0, ao_cav.outputs["AO"], clamp=True)
    # as lascas (a tinta que saiu): manchas pequenas espalhadas, mais onde o ruído grande deixa. Não usa a oclusão de
    # dentro para achar as bordas: as peças se cruzam (montadas em blocos) e ela marcava tudo como borda
    r_grande = n.ruido(pos, 2.2, 4, 0.6)
    borda = n.math("MULTIPLY", n.faixa(n.ruido(pos, 16.0, 4, 0.7), 0.66, 0.73), n.faixa(r_grande, 0.4, 0.7))
    sol = n.faixa(nz, 0.2, 0.9)
    r_medio = n.ruido(pos, 9.0, 5, 0.6)
    r_fino = n.ruido(pos, 60.0, 3, 0.5)
    # os escorridos: um ruído esticado na vertical
    escorrido = n.ruido(n.vetor(pos, (14.0, 14.0, 1.2)), 1.0, 2, 0.5)
    baixo = n.faixa(altura, 0.75, 0.22)               # 1 embaixo, 0 a partir de 0,75 m
    k_cav = 0.25 if SUPERFICIES[sup][0] == "B" else 0.8
    sujeira = n.math("ADD", n.math("MULTIPLY", baixo, n.mixf(r_grande, 0.55, 1.0)), n.math("MULTIPLY", cavidade, k_cav), clamp=True)
    sujeira = n.math("ADD", sujeira, n.math("MULTIPLY", n.faixa(escorrido, 0.62, 0.8), 0.25), clamp=True)
    sujeira = n.math("MULTIPLY", sujeira, k_suj)
    interior = SUPERFICIES[sup][0] == "B"
    ferrugem = n.math("MULTIPLY", n.math("ADD", n.math("MULTIPLY", borda, 0.9), n.math("MULTIPLY", baixo, 0.55)),
                      n.faixa(r_medio, 0.48, 0.66))
    ferrugem = n.math("ADD", ferrugem, n.math("MULTIPLY", n.faixa(r_grande, 0.66, 0.72), 0.6), clamp=True)
    ferrugem = n.math("MULTIPLY", n.faixa(ferrugem, 0.25, 0.6), k_ferr)
    desgaste = n.math("MULTIPLY", borda, k_borda)

    def lin(nome):
        return _lin(*CORES[nome])

    altura_bump = n.math("MULTIPLY", r_fino, 0.15)
    if sup in ("pintura",):
        cor = n.mixc(n.math("MULTIPLY", sol, 0.22), lin("pintura"), _lin(206, 176, 112))   # o sol desbota de cima
        cor = n.mixc(n.math("MULTIPLY", n.faixa(r_grande, 0.3, 0.75), 0.25), cor, _lin(150, 112, 44))
        # os riscos: um ruído fino esticado (linhas)
        riscos = n.faixa(n.ruido(n.vetor(pos, (2.0, 0.15, 2.0)), 40.0, 2, 0.4), 0.72, 0.78)
        riscos = n.math("MULTIPLY", riscos, n.faixa(r_grande, 0.45, 0.7))
        cor = n.mixc(n.math("MULTIPLY", riscos, 0.7), cor, lin("primer"))
        cor = n.mixc(desgaste, cor, lin("primer"))
        asp_s = n.mixf(sol, asp, 0.62)
        altura_bump = n.math("ADD", n.math("MULTIPLY", r_fino, 0.08), n.math("MULTIPLY", riscos, -0.4))
    elif sup in ("madeira", "madeira_int"):
        # os veios: ruído muito esticado ao longo do carro (y no Blender), com nós e listras irregulares
        veio = n.ruido(n.vetor(pos, (1.0, 0.03, 1.0)), 24.0, 3, 0.55, distorcao=0.5)
        largo = n.ruido(n.vetor(pos, (1.0, 0.10, 1.0)), 9.0, 3, 0.5)
        onda = n.no("ShaderNodeTexWave", wave_type="BANDS", bands_direction="Z")
        n.por(onda.inputs["Vector"], n.vetor(pos, (1.0, 0.18, 1.0)))
        onda.inputs["Scale"].default_value = 14.0
        onda.inputs["Distortion"].default_value = 14.0
        onda.inputs["Detail"].default_value = 3.0
        onda.inputs["Detail Scale"].default_value = 1.2
        mistura = n.math("ADD", n.math("MULTIPLY", n.faixa(veio, 0.38, 0.68), 0.55),
                         n.math("ADD", n.math("MULTIPLY", largo, 0.35), n.math("MULTIPLY", onda.outputs["Fac"], 0.18)))
        cor = n.mixc(n.faixa(mistura, 0.35, 0.95), lin("madeira_escura"), lin("madeira_clara"))
        cor = n.mixc(n.math("MULTIPLY", sol, 0.2), cor, _lin(150, 104, 70))       # o vinil desbota no sol
        # o vinil descascando nas bordas (aparece a chapa bege por baixo)
        cor = n.mixc(desgaste, cor, lin("friso"))
        asp_s = n.mixf(veio, asp, asp + 0.1)
        altura_bump = n.math("MULTIPLY", n.faixa(veio, 0.45, 0.7), 0.25)
    elif sup == "friso":
        cor = n.mixc(n.math("MULTIPLY", sol, 0.3), lin("friso"), _lin(214, 196, 160))
        cor = n.mixc(desgaste, cor, lin("primer"))
        asp_s = asp
    elif sup in ("cromo", "int_cromo", "refletor"):
        cor = n.mixc(n.math("MULTIPLY", r_grande, 0.4), lin("cromo"), _lin(170, 168, 160))
        asp_s = n.mixf(n.faixa(r_medio, 0.4, 0.7), asp, asp + 0.18)
        # os pontinhos de corrosão
        pontos = n.math("MULTIPLY", n.faixa(n.ruido(pos, 140.0, 2, 0.5), 0.70, 0.76), n.math("ADD", baixo, 0.2))
        ferrugem = n.math("MAXIMUM", ferrugem, n.math("MULTIPLY", pontos, k_ferr))
        altura_bump = n.math("MULTIPLY", pontos, 0.3)
    elif sup == "pneu":
        # a banda de rodagem (cilíndrica em volta do X do objeto) e os sulcos
        ox, oy, oz = n.separar(obj)
        ang = n.no("ShaderNodeMath", operation="ARCTAN2")
        n.por(ang.inputs[0], oz)
        n.por(ang.inputs[1], oy)
        raio = n.no("ShaderNodeVectorMath", operation="LENGTH")
        cy = n.no("ShaderNodeCombineXYZ")
        n.por(cy.inputs[1], oy)
        n.por(cy.inputs[2], oz)
        n.por(raio.inputs[0], cy.outputs[0])
        r = raio.outputs["Value"]
        na_banda = n.faixa(r, 0.29, 0.297)
        blocos = n.math("SINE", n.math("ADD", n.math("MULTIPLY", ang.outputs[0], 30.0), n.math("MULTIPLY", n.math("ABSOLUTE", ox), 40.0)))
        sulco = n.math("MULTIPLY", n.faixa(n.math("ABSOLUTE", ox), 0.02, 0.026), n.faixa(blocos, -0.3, 0.3))
        sulco = n.math("MAXIMUM", sulco, n.math("MULTIPLY", n.faixa(n.math("ABSOLUTE", n.math("SUBTRACT", n.math("ABSOLUTE", ox), 0.045)), 0.004, 0.008), 1.0))
        altura_bump = n.math("ADD", n.math("MULTIPLY", n.math("MULTIPLY", na_banda, sulco), 1.0), n.math("MULTIPLY", r_fino, 0.1))
        # a lateral: as letras (um anel mais claro gasto) e o desbotado cinza
        cor = n.mixc(n.math("MULTIPLY", n.math("SUBTRACT", 1.0, na_banda), 0.35), lin("borracha"), _lin(45, 45, 44))
        cor = n.mixc(n.math("MULTIPLY", n.math("SUBTRACT", 1.0, sulco), na_banda), cor, _lin(34, 33, 31))
        asp_s = asp
        sujeira = n.math("MULTIPLY", n.mixf(r_grande, 0.25, 0.6), 1.0)
    elif sup in ("placa_f", "placa_t"):
        # a placa: o fundo claro, a borda e a tarja escuras e 7 "letras"
        yc = 0.37 if sup == "placa_f" else 0.74
        u = px                              # no Blender, x = -x da Unity: o meio da placa em 0
        vv = n.math("SUBTRACT", altura, yc)
        dentro = n.math("MULTIPLY", n.faixa(n.math("ABSOLUTE", u), 0.17, 0.165), n.faixa(n.math("ABSOLUTE", vv), 0.05, 0.045))
        tarja = n.faixa(vv, 0.028, 0.034)
        k = n.math("MULTIPLY", n.math("ADD", u, 0.14), 1.0 / 0.04)
        frac = n.math("FRACT", k)
        letra = n.math("MULTIPLY", n.faixa(n.math("ABSOLUTE", n.math("SUBTRACT", frac, 0.5)), 0.33, 0.28),
                       n.faixa(n.math("ABSOLUTE", n.math("ADD", vv, 0.008)), 0.03, 0.026))
        letra = n.math("MULTIPLY", letra, n.faixa(n.math("ABSOLUTE", u), 0.145, 0.14))
        escuro = n.math("MAXIMUM", n.math("SUBTRACT", 1.0, dentro), n.math("MAXIMUM", tarja, letra))
        cor = n.mixc(escuro, lin("placa"), lin("letra"))
        asp_s = asp
        altura_bump = n.math("MULTIPLY", letra, 0.3)
    elif sup in ("lente_farol", "lente_lanterna"):
        # as lentes: o prisma (um xadrez fino) em relevo
        q = n.vetor(pos, (180.0, 180.0, 180.0))
        qx, qy, qz = n.separar(q)
        prisma = n.math("MULTIPLY", n.math("ABSOLUTE", n.math("SINE", qx)), n.math("ABSOLUTE", n.math("SINE", qz)))
        cor = n.mixc(n.math("MULTIPLY", prisma, 0.15), lin(sup), _lin(255, 255, 255) if sup == "lente_farol" else _lin(200, 30, 20))
        asp_s = asp
        altura_bump = n.math("MULTIPLY", prisma, 0.5)
    elif sup == "vinil":
        cor = n.mixc(n.math("MULTIPLY", sol, 0.15), lin("vinil"), _lin(90, 158, 160))   # desbotado no sol
        cor = n.mixc(n.math("MULTIPLY", n.faixa(r_grande, 0.4, 0.8), 0.25), cor, _lin(40, 100, 108))
        # as rachaduras do vinil velho
        vor = n.no("ShaderNodeTexVoronoi", feature="DISTANCE_TO_EDGE")
        n.por(vor.inputs["Vector"], pos)
        vor.inputs["Scale"].default_value = 48.0
        racha = n.math("MULTIPLY", n.faixa(vor.outputs["Distance"], 0.012, 0.0), n.math("MULTIPLY", sol, n.faixa(r_grande, 0.58, 0.72)))
        cor = n.mixc(n.math("MULTIPLY", racha, 0.6), cor, _lin(140, 140, 125))
        cor = n.mixc(desgaste, cor, _lin(90, 150, 150))
        asp_s = n.mixf(racha, asp, 0.8)
        altura_bump = n.math("ADD", n.math("MULTIPLY", n.ruido(pos, 220.0, 2, 0.5), 0.04), n.math("MULTIPLY", racha, -0.3))
    elif sup == "carpete":
        fibra = n.ruido(pos, 300.0, 2, 0.5)
        cor = n.mixc(n.math("MULTIPLY", fibra, 0.35), lin("carpete"), _lin(55, 80, 82))
        asp_s = asp
        altura_bump = n.math("MULTIPLY", fibra, 0.6)
    elif sup == "forro":
        mancha = n.faixa(n.ruido(pos, 3.0, 3, 0.6), 0.6, 0.7)
        cor = n.mixc(n.math("MULTIPLY", mancha, 0.25), lin("forro"), _lin(140, 120, 90))
        asp_s = asp
        altura_bump = n.math("MULTIPLY", n.ruido(pos, 160.0, 2, 0.5), 0.3)
    elif sup == "cinto":
        q = n.vetor(pos, (900.0, 900.0, 900.0))
        trama = n.math("ABSOLUTE", n.math("SINE", n.math("ADD", n.separar(q)[0], n.separar(q)[2])))
        cor = n.mixc(n.math("MULTIPLY", trama, 0.25), lin("cinto"), _lin(60, 90, 98))
        asp_s = asp
        altura_bump = n.math("MULTIPLY", trama, 0.3)
    elif sup == "alto_falante":
        q = n.vetor(pos, (500.0, 500.0, 500.0))
        qx, qy, qz = n.separar(q)
        furos = n.math("MULTIPLY", n.math("ABSOLUTE", n.math("SINE", qy)), n.math("ABSOLUTE", n.math("SINE", qz)))
        cor = n.mixc(n.faixa(furos, 0.3, 0.1), lin("alto_falante"), _lin(3, 3, 3))
        asp_s = asp
        altura_bump = n.math("MULTIPLY", furos, 0.4)
    else:
        nome_cor = {"placa_f": "placa", "placa_t": "placa"}.get(sup, sup)
        cor = n.mixc(n.math("MULTIPLY", sol, 0.25), lin(nome_cor), _lin(60, 60, 58) if sup in ("borracha", "plastico", "grade") else lin(nome_cor))
        asp_s = n.mixf(n.math("MULTIPLY", r_medio, 0.5), asp, min(1.0, asp + 0.15))

    # -- a ferrugem e a sujeira por cima de tudo
    cor_ferr = n.mixc(n.faixa(r_fino, 0.3, 0.7), lin("ferrugem_escura"), lin("ferrugem"))
    cor = n.mixc(ferrugem, cor, cor_ferr)
    cor_suj = n.mixc(n.faixa(r_medio, 0.35, 0.65), lin("sujeira"), lin("lama"))
    cor = n.mixc(n.math("MULTIPLY", sujeira, 0.75), cor, cor_suj)
    metal_s = n.math("MULTIPLY", metal, n.math("SUBTRACT", 1.0, n.math("MAXIMUM", ferrugem, n.math("MULTIPLY", sujeira, 0.8)), clamp=True))
    asp_s = n.mixf(n.math("MAXIMUM", ferrugem, n.math("MULTIPLY", sujeira, 0.85)), asp_s, 0.92)
    altura_bump = n.math("ADD", altura_bump, n.math("MULTIPLY", ferrugem, n.math("MULTIPLY", r_fino, 0.8)))

    n.por(bsdf.inputs["Base Color"], cor)
    n.por(bsdf.inputs["Metallic"], metal_s)
    n.por(bsdf.inputs["Roughness"], asp_s)
    bump = n.no("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.18 if interior else 0.35
    bump.inputs["Distance"].default_value = 0.002
    n.por(bump.inputs["Height"], altura_bump)
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    # para o assado do metal: a saída "metal" fica num nó de valor que a etapa de assar liga na emissão
    rot = n.no("ShaderNodeMath", operation="ADD", name="METAL")
    n.por(rot.inputs[0], metal_s)
    rot.inputs[1].default_value = 0.0
    atlas = SUPERFICIES[sup][0]
    tex = n.no("ShaderNodeTexImage", name="BAKE")
    tex.image = _imagem_do_atlas(atlas, "cor")
    tex.interpolation = "Linear"
    nt.nodes.active = tex
    mat.diffuse_color = _lin(*CORES.get("placa" if sup.startswith("placa") else sup, CORES["madeira_clara"]))
    return mat


# ---------------------------------------------------------------------------------------------------- montar a cena
NOMES = ["Perua_Carroceria", "Perua_Porta_DE", "Perua_Porta_DD", "Perua_Porta_TE", "Perua_Porta_TD", "Perua_Roda",
         "Perua_Volante", "Perua_Capo", "Perua_Tampa"]
# onde a roda fica para assar (longe da carroceria, para a sombra dela não girar com a roda) e para ver
LUGAR_DA_RODA_ASSAR = (-3.0, R_RODA, 0.0)


def _cena():
    cena = bpy.data.scenes.get("PeruaNova") or bpy.data.scenes.new("PeruaNova")
    bpy.context.window.scene = cena
    return cena


def construir():
    cena = _cena()
    for o in list(cena.objects):
        if o.name.split(".")[0] in NOMES or o.name.startswith("Ver_Roda"):
            me = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if me is not None and me.users == 0:
                bpy.data.meshes.remove(me)
    objs = [_carroceria(cena)]
    for nome, s, fr in (("Perua_Porta_DE", -1, True), ("Perua_Porta_DD", 1, True), ("Perua_Porta_TE", -1, False), ("Perua_Porta_TD", 1, False)):
        objs.append(_porta(cena, nome, s, fr))
    roda = _roda(cena)
    roda.location = _para_blender(LUGAR_DA_RODA_ASSAR)
    objs.append(roda)
    for i, (x, z) in enumerate(((-X_RODA, EIXO), (X_RODA, EIXO), (-X_RODA, -EIXO), (X_RODA, -EIXO))):
        c = bpy.data.objects.new(f"Ver_Roda_{i}", roda.data)
        cena.collection.objects.link(c)
        c.location = _para_blender((x, R_RODA, z))
        c.rotation_euler = (math.radians(90), 0, math.radians(180) if x < 0 else 0)
    vol = _volante(cena)
    vol.location = _para_blender((-0.37, 0.86, 0.50))
    vol.rotation_euler = (math.radians(90 + 25), 0, 0)
    objs.append(vol)
    objs.append(_capo(cena))
    objs.append(_tampa(cena))
    tris = sum(len(o.data.polygons) for o in objs)
    print("peças:", [o.name for o in objs], "faces:", tris)
    return objs


# ---------------------------------------------------------------------------------------------------- UV e assado
def _pecas(cena=None):
    cena = cena or _cena()
    return [cena.objects[n] for n in NOMES]


def _area_3d():
    for janela in bpy.context.window_manager.windows:
        for area in janela.screen.areas:
            if area.type == "VIEW_3D":
                regiao = next(r for r in area.regions if r.type == "WINDOW")
                return dict(window=janela, area=area, region=regiao, screen=janela.screen)
    raise RuntimeError("Abra uma Vista 3D no Blender (o desembrulhar precisa dela).")


def _superficie_do_slot(o, i):
    return o.data.materials[i].name[len("Aut_"):]


# quanto pixel cada superfície ganha a mais (ou a menos) no atlas: o que se vê de perto e tem detalhe fino (a madeira,
# a placa) ganha mais; o que quase não se vê (o fundo, o tambor do freio, o fundo das grades) ganha menos
PESO_NO_ATLAS = {
    "madeira": 2.6, "madeira_int": 2.0, "placa_f": 2.2, "placa_t": 2.2, "pintura": 1.35, "friso": 1.3,
    "escuro": 0.45, "grade": 0.6, "aco": 0.75, "refletor": 0.8,
    "vinil": 1.35, "painel": 1.2, "carpete": 0.75, "forro": 0.8, "tapete": 0.7,
}


def _pesar_uv(objs):
    """Escala o UV de cada face pelo peso da superfície dela (em volta de (0, 0): as ilhas continuam separadas)."""
    for o in objs:
        bm = bmesh.from_edit_mesh(o.data)
        uvl = bm.loops.layers.uv.active
        sup = [_superficie_do_slot(o, i) for i in range(len(o.data.materials))]
        for f in bm.faces:
            if not f.select:
                continue
            k = PESO_NO_ATLAS.get(sup[f.material_index], 1.0)
            if k != 1.0:
                for l in f.loops:
                    l[uvl].uv = l[uvl].uv * k
        bmesh.update_edit_mesh(o.data)


def desembrulhar(margem=0.0016):
    """O UV de todas as peças: as faces do atlas A (a lataria e a roda) num, as do B (o interior) em outro, juntas
    (o mesmo tamanho de pixel em todas as peças, vezes o PESO_NO_ATLAS de cada superfície)."""
    cena = _cena()
    objs = _pecas(cena)
    ctx = _area_3d()
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for o in cena.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    with bpy.context.temp_override(**ctx, active_object=objs[0], selected_objects=objs, selected_editable_objects=objs):
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_mode(type="FACE")
        ts = bpy.context.scene.tool_settings
        sync = ts.use_uv_select_sync
        ts.use_uv_select_sync = True
        for atlas in ("A", "B"):
            for o in objs:
                bm = bmesh.from_edit_mesh(o.data)
                sup = [_superficie_do_slot(o, i) for i in range(len(o.data.materials))]
                for f in bm.faces:
                    f.select_set(SUPERFICIES[sup[f.material_index]][0] == atlas)
                bm.select_flush_mode()
                bmesh.update_edit_mesh(o.data)
            bpy.ops.uv.smart_project(angle_limit=math.radians(70), island_margin=margem, area_weight=0.0,
                                     correct_aspect=True, scale_to_bounds=False)
            # o smart project empacota cada peça sozinha: iguala o tamanho do pixel, pesa cada superfície e empacota
            # todas juntas
            bpy.ops.uv.average_islands_scale()
            _pesar_uv(objs)
            try:
                bpy.ops.uv.pack_islands(rotate=True, margin=margem, shape_method="CONCAVE")
            except TypeError:
                bpy.ops.uv.pack_islands(rotate=True, margin=margem)
        ts.use_uv_select_sync = sync
        bpy.ops.object.mode_set(mode="OBJECT")
    print("UV ok")


def _ligar_metal(ligar):
    for mat in bpy.data.materials:
        if not mat.name.startswith("Aut_") or mat.name == "Aut_vidro":
            continue
        nt = mat.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        metal = nt.nodes.get("METAL")
        for l in list(bsdf.inputs["Emission Color"].links):
            nt.links.remove(l)
        if ligar and metal is not None:
            nt.links.new(metal.outputs[0], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 1.0 if ligar else 0.0


def _alvo(canal):
    """Aponta o nó BAKE de cada material para a imagem do atlas dele naquele canal."""
    for mat in bpy.data.materials:
        if not mat.name.startswith("Aut_"):
            continue
        sup = mat.name[len("Aut_"):]
        atlas = SUPERFICIES[sup][0]
        if atlas == "-":
            atlas = "V"
        no = mat.node_tree.nodes.get("BAKE")
        no.image = _imagem_do_atlas(atlas, canal)
        mat.node_tree.nodes.active = no


def assar(canais=("cor", "aspereza", "metal", "normal", "ao"), amostras=8, margem=12):
    """Assa os canais (Cycles) nas imagens Perua_<atlas>_<canal>. A roda fica longe da carroceria enquanto assa."""
    import time
    cena = _cena()
    objs = _pecas(cena)
    try:
        cena.render.engine = "CYCLES"
    except TypeError as e:
        print(e)
    cena.cycles.samples = amostras
    cena.cycles.use_denoising = False
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.refresh_devices()
        if any(d.type != "CPU" and d.use for d in prefs.devices):
            cena.cycles.device = "GPU"
    except Exception:
        pass
    if cena.world is None:
        cena.world = bpy.data.worlds.new("MundoPerua")
    cena.world.light_settings.distance = 0.35
    for o in cena.objects:
        o.hide_render = o not in objs
    roda = cena.objects["Perua_Roda"]
    roda.location = _para_blender(LUGAR_DA_RODA_ASSAR)
    for o in cena.objects:
        o.select_set(o in objs)
    bpy.context.view_layer.objects.active = objs[0]
    b = cena.render.bake
    b.margin = margem
    b.margin_type = "EXTEND"
    b.use_clear = True
    tipos = {"cor": "DIFFUSE", "aspereza": "ROUGHNESS", "metal": "EMIT", "normal": "NORMAL", "ao": "AO"}
    for canal in canais:
        t = time.time()
        _alvo(canal)
        _ligar_metal(canal == "metal")
        kw = {}
        if canal == "cor":
            kw = dict(pass_filter={"COLOR"})
        if canal == "normal":
            b.normal_space = "TANGENT"
        bpy.ops.object.bake(type=tipos[canal], margin=margem, use_clear=True, **kw)
        _ligar_metal(False)
        print("assado:", canal, round(time.time() - t, 1), "s")
    _alvo("cor")
    for o in cena.objects:
        o.hide_render = False


def compor():
    """Junta os canais nas texturas da Unity (URP Lit, fluxo metálico) e grava em TEXTURAS:
    <final>_Cor.png (a cor com um pouco da oclusão), <final>_MS.png (R = metal, G = oclusão, A = suavidade) e
    <final>_Normal.png."""
    import numpy as np
    os.makedirs(TEXTURAS, exist_ok=True)
    nomes = {"A": "Perua_Lataria", "B": "Perua_Interior"}
    for atlas, final in nomes.items():
        tam = TAMANHO_ATLAS[atlas]

        def ler(canal):
            img = _imagem_do_atlas(atlas, canal)
            a = np.empty(tam * tam * 4, dtype=np.float32)
            img.pixels.foreach_get(a)
            return a.reshape(tam, tam, 4)

        cor, asp, metal, nor, ao = ler("cor"), ler("aspereza"), ler("metal"), ler("normal"), ler("ao")
        oc = ao[..., 0:1]
        # a cor: a oclusão escurece um pouco (as frestas, os cantos), mesmo sem a luz ambiente
        c = cor.copy()
        c[..., :3] = cor[..., :3] * (0.55 + 0.45 * oc)
        c[..., 3] = 1.0
        ms = np.zeros_like(cor)
        ms[..., 0] = np.clip(metal[..., 0], 0, 1)
        ms[..., 1] = oc[..., 0]
        ms[..., 2] = 0.0
        ms[..., 3] = 1.0 - np.clip(asp[..., 0], 0, 1)
        n = nor.copy()
        n[..., 3] = 1.0
        for sufixo, dados, espaco in (("Cor", c, "sRGB"), ("MS", ms, "Non-Color"), ("Normal", n, "Non-Color")):
            nome = f"{final}_{sufixo}"
            img = bpy.data.images.get(nome)
            if img is None or img.size[0] != tam:
                if img:
                    bpy.data.images.remove(img)
                img = bpy.data.images.new(nome, tam, tam, alpha=True, float_buffer=False)
            img.colorspace_settings.name = espaco
            img.pixels.foreach_set(dados.ravel())
            img.filepath_raw = os.path.join(TEXTURAS, nome + ".png")
            img.file_format = "PNG"
            img.save()
        print("texturas:", final)


# ---------------------------------------------------------------------------------------------------- exportar
def _materiais_finais():
    """Os 5 materiais do FBX (no Blender, com as texturas assadas, só para ver; a Unity faz os dela)."""
    mats = {}
    for final in FINAIS:
        m = bpy.data.materials.get(final) or bpy.data.materials.new(final)
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        nt.links.new(bsdf.outputs[0], out.inputs["Surface"])
        base = {"Perua_Lataria": "Perua_Lataria", "Perua_Farol": "Perua_Lataria", "Perua_Lanterna": "Perua_Lataria",
                "Perua_Interior": "Perua_Interior"}.get(final)
        img = bpy.data.images.get(f"{base}_Cor") if base else None
        if img is not None:
            t = nt.nodes.new("ShaderNodeTexImage")
            t.image = img
            nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
        if final == "Perua_Vidro":
            bsdf.inputs["Base Color"].default_value = (0.08, 0.1, 0.11, 1)
            bsdf.inputs["Alpha"].default_value = 0.3
        mats[final] = m
    return mats


def exportar(caminho=None):
    """Grava o FBX com as 9 peças e os 5 materiais finais (as faces de cada superfície vão para o material final dela)."""
    import numpy as np
    cena = _cena()
    caminho = caminho or os.path.join(DESTINO, "PeruaNova.fbx")
    os.makedirs(os.path.dirname(caminho), exist_ok=True)
    finais = _materiais_finais()
    temporarios = []
    renomeados = []
    for o in _pecas(cena):
        nome = o.name
        me = o.data.copy()
        sup = [_superficie_do_slot(o, i) for i in range(len(o.data.materials))]
        usados = [f for f in FINAIS if any(SUPERFICIES[s][1] == f for s in sup)]
        mapa = np.array([usados.index(SUPERFICIES[s][1]) for s in sup], dtype=np.int32)
        idx = np.empty(len(me.polygons), dtype=np.int32)
        me.polygons.foreach_get("material_index", idx)
        me.materials.clear()
        for f in usados:
            me.materials.append(finais[f])
        me.polygons.foreach_set("material_index", mapa[idx])
        me.update()
        o.name = nome + "_aut"
        renomeados.append((o, nome))
        t = bpy.data.objects.new(nome, me)
        cena.collection.objects.link(t)
        t.rotation_euler = (math.radians(90), 0, 0)
        temporarios.append(t)
    for o in cena.objects:
        o.select_set(o in temporarios)
    bpy.context.view_layer.objects.active = temporarios[0]
    try:
        bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH"},
                                 apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE",
                                 path_mode="STRIP")
    finally:
        for t in temporarios:
            me = t.data
            bpy.data.objects.remove(t, do_unlink=True)
            bpy.data.meshes.remove(me)
        for o, nome in renomeados:
            o.name = nome
    print("FBX:", caminho)


def salvar():
    """Guarda o .blend editável (as peças com os materiais de autoria e as imagens assadas, empacotadas)."""
    for img in bpy.data.images:
        if img.name.startswith("Perua_") and img.has_data and not img.packed_file:
            try:
                img.pack()
            except Exception:
                pass
    bpy.ops.wm.save_as_mainfile(filepath=BLEND, copy=False)
    print("blend:", BLEND)
