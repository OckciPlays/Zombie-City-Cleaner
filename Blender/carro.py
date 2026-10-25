# O carro que se dirige: uma perua dos anos 90, mostarda, com as 4 portas, as rodas e o volante separados (para animar
# na Unity), o interior (bancos, painel, console, forro) e os vidros num material próprio (dá para ver de dentro).
#
# Como rodar (Blender 5.2, numa cena qualquer):
#   import importlib, sys; sys.path.append(r"<repo>\Blender"); import carro; importlib.reload(carro)
#   carro.construir()    # refaz as peças na cena "Carro"
#   carro.exportar()     # grava Assets/_Game/Art/Veiculos/Carro.fbx
#   carro.foto(caminho)  # (opcional) uma foto de 3/4 para conferir
# Depois, na Unity: Zombie City Cleaner > Montar veículos.
#
# As medidas são escritas nos eixos da Unity (x para a direita do carro, y para cima, z para a frente; o chão em y = 0 e
# o meio entre os eixos em z = 0). No fim, cada peça vira um objeto girado 90° em X com a malha espelhada em X, o mesmo
# formato do Itens.fbx e do Equipamentos.fbx (a Unity espelha o X de volta ao importar).
#
# As peças (objetos do FBX):
# - Carro_Carroceria: tudo o que não mexe, com a origem no chão, no meio do carro. Materiais: Carro (a paleta), Vidro
#   (para-brisa, vidro de trás e os vidros laterais de trás), Farol e Lanterna (as lentes, que acendem).
# - Carro_Porta_DE, _DD, _TE, _TD (dianteira/traseira, esquerda/direita): a origem na dobradiça (a borda da frente da
#   porta, por fora), com o vidro da janela (material Vidro). Abrem girando em Y.
# - Carro_Roda: uma roda, a origem no meio, o eixo em X e a calota para +X (as da esquerda giram 180° em Y).
# - Carro_Volante: a origem no meio, o aro no plano XY, a coluna para +Z.
# - Carro_Capo: o capô, a origem na dobradiça (atrás, em cima, no meio). Abre girando em X. Embaixo dele, na carroceria,
#   o cofre do motor aberto por cima, entre os para-lamas (o que fica dentro é o Cofre.fbx, do cofre.py).
# - Carro_Tampa: a tampa traseira (o porta-malas: a chapa de baixo com a placa e o vidro de trás), a origem na dobradiça
#   (PIVO_TAMPA, embaixo da borda do teto). Abre girando em X (a borda de baixo sobe e vai para trás). Atrás dela, o
#   porta-malas: o piso (Y_PISO_MALAS), as caixas das rodas e o forro das laterais (o macaco e o estepe são as malhas do
#   pecas.py, postas pela Unity).
# Cores: blocos sólidos pela Paleta.png, só cores que já existem nela (as da linha 8 são as dos carros da cidade).

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(AQUI)
DESTINO = os.path.join(REPO, "Assets", "_Game", "Art", "Veiculos")
PALETA = os.path.join(REPO, "Assets", "_Game", "Art", "Paleta.png")

# (linha da imagem, de cima para baixo; coluna) de cada cor na Paleta.png
CORES = {
    "pintura": (6, 3),      # mostarda, um pouco de brilho
    "plastico": (8, 0),     # para-choque, molduras, retrovisores
    "borracha": (8, 5),     # pneu
    "roda": (8, 2),         # aro prateado
    "calota": (8, 11),
    "farol": (8, 12),
    "lanterna": (8, 13),
    "pisca": (9, 13),
    "painel": (10, 1),
    "interior": (10, 0),
    "banco": (9, 11),
    "forro": (10, 4),
    "carpete": (8, 14),
    "placa": (9, 10),
    "grade": (10, 14),
    # o cofre do motor
    "motor": (10, 3),
    "tampa_valvulas": (13, 3),
    "bateria": (11, 5),
    "polo_mais": (6, 0),
    "etiqueta": (10, 6),
    "aleta": (8, 1),
    "reservatorio": (13, 5),
    "azul": (13, 2),
}

MATERIAIS = ["Carro", "Vidro", "Farol", "Lanterna"]
CARRO, VIDRO, FAROL, LANTERNA = range(4)

# medidas (m)
W = 0.86                 # meia largura, por fora
EIXO = 1.36              # z do eixo da frente (o de trás em -EIXO)
R_RODA, L_RODA, X_RODA = 0.30, 0.19, 0.735
R_ARCO = 0.37
Y_BASE = 0.22            # embaixo da carroceria
Y_CINTURA = 0.93         # a linha das janelas
Y_TETO = 1.47
Z_PORTA_D = 0.92         # a dobradiça da porta da frente (o fim do para-lama)
Z_COLUNA_B = -0.10       # o fim da porta da frente
Z_PORTA_T = -0.16        # a dobradiça da porta de trás
Z_COLUNA_C = -0.94       # o fim da porta de trás
PARABRISA = ((1.00, Y_CINTURA), (0.30, 1.43))     # (z, y) de baixo e de cima
VIDRO_TRAS = ((-2.10, 0.97), (-1.92, 1.43))
PIVO_TAMPA = (0, 1.445, -1.91)   # a dobradiça da tampa traseira (MontagemDosVeiculos.PivoDaTampa)
# o capô: a dobradiça atrás (perto do para-brisa), a largura entre os para-lamas e o fundo do cofre do motor
X_CAPO = 0.575           # meia largura do capô (os para-lamas vão de 0,58 a W)
Z_CAPO = 1.145           # a borda de trás do capô (a dobradiça)
Y_COFRE = 0.48           # o fundo do cofre
X_MALAS = 0.60           # meia largura do porta-malas (e da tampa traseira); dali para fora, as laterais
Y_PISO_MALAS = 0.66      # o piso do porta-malas (logo acima das rodas de trás)
Y_SOLEIRA = 0.62         # a borda de baixo da tampa traseira


def _y_capo(z):
    """A altura de cima do capô (e dos para-lamas) em z: a linha de (1,10; 0,915) a (2,02; 0,84)."""
    return 0.915 + (z - 1.10) * (0.84 - 0.915) / (2.02 - 1.10)

# onde ficam os bancos (para a Unity: o ponto do quadril de quem senta), só para conferência
BANCOS = [(-0.37, 0.58, 0.06), (0.37, 0.58, 0.06), (-0.38, 0.60, -0.68), (0.38, 0.60, -0.68)]


def _uv(cor):
    linha, coluna = CORES[cor]
    return ((coluna + 0.5) / 16.0, 1.0 - (linha + 0.5) / 16.0)


def _material(nome):
    m = bpy.data.materials.get(nome) or bpy.data.materials.new(nome)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    if nome == "Vidro":
        # só para ver no Blender: a Unity troca pelo Vidro.mat (transparente)
        bsdf.inputs["Base Color"].default_value = (0.12, 0.16, 0.19, 1.0)
        bsdf.inputs["Alpha"].default_value = 0.35
        m.diffuse_color = (0.12, 0.16, 0.19, 0.35)
        return m
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None)
    if tex is None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    img = bpy.data.images.get("Paleta.png") or bpy.data.images.load(PALETA)
    img.alpha_mode = "NONE"   # o alfa da paleta é o brilho da cidade, não transparência
    tex.image = img
    tex.interpolation = "Closest"
    return m


class Malha:
    """Junta peças (caixas, perfis, ripas, cilindros) numa malha, nos eixos da Unity."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.usados = set()

    def _pintar(self, faces, cor, mat):
        u, v = _uv(cor)
        for f in faces:
            f.material_index = mat
            for l in f.loops:
                l[self.uv].uv = (u, v)
        self.usados.add(mat)

    def _faces(self, vs):
        return list({f for v in vs for f in v.link_faces})

    def caixa(self, cor, tam, centro, giro=(0, 0, 0), chanfro=0.01, mat=CARRO):
        """Uma caixa de tam (x, y, z), girada (graus: X, depois Y, depois Z) em volta do centro, chanfrada."""
        vs = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        m = Matrix.Translation(Vector(centro)) @ Matrix.Rotation(math.radians(giro[2]), 4, "Z") @ \
            Matrix.Rotation(math.radians(giro[1]), 4, "Y") @ Matrix.Rotation(math.radians(giro[0]), 4, "X") @ \
            Matrix.Diagonal((tam[0], tam[1], tam[2], 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=vs)
        faces = self._faces(vs)
        self._pintar(faces, cor, mat)
        if chanfro > 0 and min(tam) > chanfro * 2.5:
            arestas = list({e for f in faces for e in f.edges})
            bmesh.ops.bevel(self.bm, geom=vs + arestas, offset=chanfro, segments=1, affect="EDGES", profile=0.5)

    def perfil(self, cor, pontos, x0, x1, mat=CARRO):
        """Um polígono no plano de lado (pontos em (z, y)), puxado em X de x0 a x1 (as tampas triangulas, côncavas também)."""
        a = [self.bm.verts.new((x0, y, z)) for z, y in pontos]
        b = [self.bm.verts.new((x1, y, z)) for z, y in pontos]
        n = len(pontos)
        tampas = [self.bm.faces.new(a), self.bm.faces.new(b[::-1])]
        lados = [self.bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i])) for i in range(n)]
        faces = tampas + lados
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._pintar(faces, cor, mat)
        bmesh.ops.triangulate(self.bm, faces=tampas, quad_method="BEAUTY", ngon_method="BEAUTY")

    def ripa(self, cor, a, b, tx, ty, mat=CARRO, chanfro=0.0):
        """Uma barra de a até b: tx de largura em X (ou perto de X) e ty na outra direção, de lado."""
        a, b = Vector(a), Vector(b)
        z = (b - a).normalized()
        x = Vector((1, 0, 0)) - z * z.x
        x = x.normalized() if x.length > 1e-4 else Vector((0, 1, 0))
        y = z.cross(x)
        base = Matrix((x, y, z)).transposed().to_4x4()
        vs = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        m = Matrix.Translation((a + b) / 2) @ base @ Matrix.Diagonal((tx, ty, (b - a).length, 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=vs)
        faces = self._faces(vs)
        self._pintar(faces, cor, mat)
        if chanfro > 0:
            arestas = list({e for f in faces for e in f.edges})
            bmesh.ops.bevel(self.bm, geom=vs + arestas, offset=chanfro, segments=1, affect="EDGES", profile=0.5)

    def cilindro(self, cor, raio, comprimento, centro, eixo="x", lados=16, mat=CARRO, raio2=None):
        r = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=lados, radius1=raio,
                                  radius2=raio if raio2 is None else raio2, depth=comprimento)
        vs = r["verts"]
        giro = {"x": Matrix.Rotation(math.radians(90), 4, "Y"), "y": Matrix.Rotation(math.radians(-90), 4, "X"),
                "z": Matrix.Identity(4)}[eixo]
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(Vector(centro)) @ giro, verts=vs)
        self._pintar(self._faces(vs), cor, mat)

    def objeto(self, nome, cena, pivo=(0, 0, 0)):
        """A malha vira um objeto do formato do Itens.fbx: a origem no pivô, espelhada em X e girada 90° em X."""
        bm = self.bm
        bmesh.ops.translate(bm, verts=bm.verts, vec=-Vector(pivo))
        bmesh.ops.scale(bm, vec=(-1, 1, 1), verts=bm.verts)
        bmesh.ops.reverse_faces(bm, faces=bm.faces)   # o espelho virou as faces do avesso
        # só os materiais usados, na ordem de MATERIAIS
        usados = sorted(self.usados)
        novo = {m: i for i, m in enumerate(usados)}
        for f in bm.faces:
            f.material_index = novo[f.material_index]
        me = bpy.data.meshes.new(nome)
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = False
        for m in usados:
            me.materials.append(_material(MATERIAIS[m]))
        o = bpy.data.objects.new(nome, me)
        o.rotation_euler = (math.radians(90), 0, 0)
        cena.collection.objects.link(o)
        return o


def _arco(zc, para_tras):
    """
    Os pontos (z, y) do recorte da roda em volta do eixo zc: sobe de baixo de um lado, passa por cima e desce do outro,
    andando para trás (da frente do arco para a traseira dele) ou para a frente.
    """
    pts = []
    n = 9
    for i in range(n + 1):
        t = math.pi * i / n if para_tras else math.pi * (1 - i / n)
        pts.append((zc + R_ARCO * math.cos(t), R_RODA + R_ARCO * math.sin(t)))
    return [(pts[0][0], Y_BASE)] + pts + [(pts[-1][0], Y_BASE)]


def _carroceria(cena):
    m = Malha()
    # ---------------------------------------------------------------- frente: para-lamas, capô e o cofre do motor
    frente = [(Z_PORTA_D, Y_BASE), (Z_PORTA_D, Y_CINTURA), (1.00, Y_CINTURA), (1.10, 0.915), (2.02, 0.84), (2.12, 0.79),
              (2.14, 0.52), (2.10, 0.24)]
    # embaixo, de volta para trás, passando pelo arco da roda
    frente += _arco(EIXO, True)
    # os para-lamas (dos lados do capô até a lateral), com o recorte da roda
    for s in (-1, 1):
        m.perfil("pintura", _sem_repetidos(frente), s * (X_CAPO + 0.005), s * W)
    # o meio, entre os para-lamas: a base do para-brisa (onde o capô se apoia), o cofre do motor aberto por cima e a
    # frente (a travessa do radiador, a grade e o para-choque). O capô é outra peça (Carro_Capo)
    meio = [(Z_PORTA_D, Y_BASE), (Z_PORTA_D, Y_CINTURA), (1.00, Y_CINTURA), (1.10, 0.915), (Z_CAPO, _y_capo(Z_CAPO) - 0.026),
            (Z_CAPO, Y_COFRE), (2.03, Y_COFRE), (2.03, 0.80), (2.11, 0.79), (2.14, 0.52), (2.10, 0.24), (2.10, Y_BASE)]
    m.perfil("pintura", _sem_repetidos(meio), -(X_CAPO + 0.005), X_CAPO + 0.005)
    # o fundo do vão da roda, escuro (a parede do cofre)
    for s in (-1, 1):
        m.caixa("plastico", (0.01, 0.46, 0.74), (s * (X_CAPO + 0.012), 0.45, EIXO), chanfro=0)
    _cofre(m)
    # ---------------------------------------------------------------- trás: o porta-malas da perua e os para-lamas
    # as laterais (de X_MALAS até a lateral) sobem até a linha das janelas, com o recorte da roda; o meio fica baixo, no
    # piso do porta-malas, e a tampa traseira (Carro_Tampa, outra peça) fecha a abertura, da soleira até o teto
    tras = [(-0.96, Y_BASE), (-0.96, Y_CINTURA), (-2.08, 0.95), (-2.15, 0.93), (-2.16, 0.50), (-2.12, 0.24)]
    tras += _arco(-EIXO, False)
    for s in (-1, 1):
        m.perfil("pintura", _sem_repetidos(tras), s * X_MALAS, s * W)
    meio_tras = [(-0.96, Y_BASE), (-0.96, Y_PISO_MALAS), (-2.12, Y_PISO_MALAS), (-2.12, Y_SOLEIRA), (-2.158, Y_SOLEIRA),
                 (-2.16, 0.50), (-2.12, 0.24)]
    m.perfil("pintura", meio_tras, -X_MALAS, X_MALAS)
    for s in (-1, 1):
        # o fundo do vão da roda, escuro
        m.caixa("plastico", (0.01, 0.46, 0.74), (s * (X_MALAS + 0.007), 0.45, -EIXO), chanfro=0)
        # dentro do porta-malas: o forro das laterais e a caixa da roda
        m.caixa("interior", (0.01, Y_CINTURA - Y_PISO_MALAS, 1.12), (s * (X_MALAS - 0.006), (Y_CINTURA + Y_PISO_MALAS) / 2, -1.54), chanfro=0)
        m.caixa("interior", (0.18, 0.14, 0.72), (s * (X_MALAS - 0.10), Y_PISO_MALAS + 0.07, -EIXO), chanfro=0.02)
        # o canto de trás, entre a tampa e a coluna D (acima da lanterna)
        m.ripa("pintura", (s * (X_MALAS + 0.09), 0.95, -2.09), (s * (X_MALAS + 0.09), 1.43, -1.91), 0.18, 0.03)
    # o piso (o carpete)
    m.caixa("carpete", (2 * X_MALAS - 0.02, 0.02, 1.14), (0, Y_PISO_MALAS + 0.01, -1.54), chanfro=0)
    # ---------------------------------------------------------------- o meio: assoalho, soleiras, túnel
    m.caixa("carpete", (1.60, 0.10, 1.92), (0, 0.27, -0.02), chanfro=0)
    for s in (-1, 1):
        m.caixa("pintura", (0.08, 0.14, 1.92), (s * 0.82, 0.29, -0.02), chanfro=0.008)
    m.caixa("interior", (0.22, 0.14, 1.10), (0, 0.39, 0.15))
    m.caixa("plastico", (1.20, 0.02, 3.6), (0, 0.205, 0), chanfro=0)   # o fundo, embaixo
    # ---------------------------------------------------------------- teto, colunas e vidros
    meio_teto = (PARABRISA[1][0] + VIDRO_TRAS[1][0]) / 2
    comp_teto = PARABRISA[1][0] - VIDRO_TRAS[1][0] + 0.04
    m.caixa("pintura", (1.62, 0.04, comp_teto), (0, Y_TETO - 0.02, meio_teto), chanfro=0.015)
    m.caixa("forro", (1.54, 0.03, comp_teto - 0.06), (0, Y_TETO - 0.055, meio_teto), chanfro=0)
    # as barras do teto (perua)
    for s in (-1, 1):
        m.caixa("plastico", (0.04, 0.03, 1.72), (s * 0.63, Y_TETO + 0.03, -0.98), chanfro=0.006)
        for z in (-0.16, -1.80):
            m.caixa("plastico", (0.05, 0.03, 0.09), (s * 0.63, Y_TETO + 0.008, z), chanfro=0)
    (zb, yb), (zt, yt) = PARABRISA
    for s in (-1, 1):
        # coluna A (na borda do para-brisa), B (preta, entre as portas), C e D (nos vidros de trás)
        m.ripa("pintura", (s * 0.80, yb, zb + 0.01), (s * 0.78, yt, zt), 0.05, 0.08)
        m.caixa("plastico", (0.05, Y_TETO - Y_CINTURA - 0.06, 0.07), (s * 0.81, (Y_TETO + Y_CINTURA) / 2 - 0.03, (Z_COLUNA_B + Z_PORTA_T) / 2))
        # a coluna B embaixo da linha das janelas, da soleira até a cintura: fecha o vão entre as duas portas (sem ela,
        # via-se o interior pela fresta). Pintada e 1 cm mais funda que a chapa das portas, para ler como uma emenda; não
        # encosta nelas (3 mm de cada lado), para elas abrirem sem atravessar.
        m.caixa("pintura", (0.05, Y_CINTURA - 0.30, Z_COLUNA_B - Z_PORTA_T - 0.006), (s * 0.825, (Y_CINTURA + 0.30) / 2, (Z_COLUNA_B + Z_PORTA_T) / 2), chanfro=0)
        m.caixa("pintura", (0.05, Y_TETO - Y_CINTURA - 0.06, 0.12), (s * 0.81, (Y_TETO + Y_CINTURA) / 2 - 0.03, Z_COLUNA_C - 0.06))
        m.ripa("pintura", (s * 0.80, 0.95, -2.08), (s * 0.79, Y_TETO - 0.04, -1.90), 0.05, 0.14)
        # o vidro de lado, atrás (entre as colunas C e D)
        lado = [(-1.06, 0.95), (-1.06, 1.40), (-1.88, 1.40), (-2.03, 0.95)]
        m.perfil("grade", lado, s * 0.798, s * 0.814, mat=VIDRO)
    m.ripa("grade", (0, yb, zb), (0, yt, zt), 1.58, 0.01, mat=VIDRO)
    # o vidro de trás é da tampa traseira (Carro_Tampa)
    # os limpadores e o retrovisor de dentro
    for x in (-0.36, 0.12):
        m.ripa("plastico", (x, yb + 0.03, zb - 0.03), (x + 0.38, yb + 0.06, zb - 0.06), 0.015, 0.015)
    m.caixa("plastico", (0.24, 0.07, 0.03), (0, yt - 0.10, zt - 0.04), chanfro=0.008)
    m.ripa("plastico", (0, yt - 0.04, zt - 0.02), (0, yt - 0.08, zt - 0.04), 0.02, 0.02)
    # ---------------------------------------------------------------- frente: para-choque, grade, faróis e placa
    m.caixa("plastico", (1.80, 0.20, 0.16), (0, 0.36, 2.12), chanfro=0.03)
    m.caixa("grade", (0.62, 0.15, 0.04), (0, 0.65, 2.13), chanfro=0)
    for i in range(3):
        m.caixa("plastico", (0.58, 0.012, 0.01), (0, 0.61 + i * 0.04, 2.152), chanfro=0)
    for s in (-1, 1):
        m.caixa("farol", (0.32, 0.15, 0.04), (s * 0.57, 0.65, 2.13), chanfro=0.008, mat=FAROL)
        m.caixa("plastico", (0.35, 0.18, 0.03), (s * 0.57, 0.65, 2.115), chanfro=0)
        m.caixa("pisca", (0.12, 0.05, 0.03), (s * 0.66, 0.48, 2.195), chanfro=0.005)
    m.caixa("placa", (0.36, 0.11, 0.02), (0, 0.36, 2.205), chanfro=0)
    # ---------------------------------------------------------------- trás: para-choque, lanternas, placa, escape
    m.caixa("plastico", (1.80, 0.20, 0.16), (0, 0.36, -2.12), chanfro=0.03)
    for s in (-1, 1):
        m.caixa("lanterna", (0.16, 0.36, 0.03), (s * 0.70, 0.72, -2.165), chanfro=0.008, mat=LANTERNA)
        m.caixa("pisca", (0.16, 0.06, 0.03), (s * 0.70, 0.51, -2.162), chanfro=0.005)
    # (a placa de trás e a maçaneta são da tampa traseira)
    m.cilindro("plastico", 0.03, 0.14, (-0.52, 0.25, -2.16), eixo="z", lados=8)
    # ---------------------------------------------------------------- o painel
    painel = [(0.62, 0.60), (0.62, 0.78), (0.70, 0.90), (1.00, 0.92), (1.00, 0.55), (0.80, 0.52)]
    m.perfil("painel", painel, -0.80, 0.80)
    # o quadro de instrumentos do motorista (à esquerda) e os mostradores
    m.caixa("painel", (0.36, 0.08, 0.14), (-0.37, 0.92, 0.72), chanfro=0.01)
    m.caixa("grade", (0.30, 0.09, 0.02), (-0.37, 0.85, 0.645), chanfro=0)
    for x in (-0.44, -0.30):
        m.cilindro("placa", 0.04, 0.01, (x, 0.85, 0.633), eixo="z", lados=10)
    # o rádio e as saídas de ar
    m.caixa("grade", (0.20, 0.10, 0.02), (0, 0.70, 0.615), chanfro=0)
    for x in (-0.08, 0.08, 0.55):
        m.caixa("grade", (0.12, 0.04, 0.02), (x, 0.84, 0.66), chanfro=0)
    # a coluna da direção (o volante é outra peça)
    m.ripa("interior", (-0.37, 0.74, 0.72), (-0.37, 0.84, 0.55), 0.07, 0.07)
    # o câmbio e o freio de mão
    m.ripa("interior", (0, 0.46, 0.50), (0, 0.62, 0.46), 0.022, 0.022)
    m.caixa("plastico", (0.05, 0.05, 0.05), (0, 0.64, 0.455), chanfro=0.01)
    m.ripa("interior", (0, 0.47, -0.02), (0, 0.53, 0.22), 0.04, 0.03)
    # ---------------------------------------------------------------- os bancos
    for s in (-1, 1):
        x = s * 0.37
        m.caixa("plastico", (0.40, 0.08, 0.40), (x, 0.36, 0.08), chanfro=0)
        m.caixa("banco", (0.50, 0.14, 0.50), (x, 0.44, 0.10), giro=(-6, 0, 0), chanfro=0.03)
        m.caixa("banco", (0.50, 0.64, 0.12), (x, 0.80, -0.22), giro=(-14, 0, 0), chanfro=0.03)
        m.caixa("banco", (0.26, 0.16, 0.10), (x, 1.20, -0.31), giro=(-14, 0, 0), chanfro=0.02)
    m.caixa("banco", (1.36, 0.16, 0.46), (0, 0.45, -0.70), giro=(-5, 0, 0), chanfro=0.03)
    m.caixa("banco", (1.36, 0.58, 0.12), (0, 0.80, -0.99), giro=(-16, 0, 0), chanfro=0.03)
    for s in (-1, 1):
        m.caixa("banco", (0.24, 0.14, 0.09), (s * 0.38, 1.14, -1.10), giro=(-16, 0, 0), chanfro=0.02)
    return m.objeto("Carro_Carroceria", cena)


def _cofre(m):
    """
    O cofre do motor, aberto por cima: o fundo e a parede de trás (escuros). O que fica dentro (o motor e os suportes)
    é o Cofre.fbx (Blender/cofre.py), e as peças que se trocam (a vela, a bateria e o radiador) são as do pecas.py: a
    Unity põe tudo no lugar (MontagemDosVeiculos) e mostra cada peça pelo estado dela.
    """
    zf = 2.03 - Z_CAPO
    m.caixa("grade", (2 * X_CAPO, 0.01, zf), (0, Y_COFRE + 0.006, (Z_CAPO + 2.03) / 2), chanfro=0)
    m.caixa("interior", (2 * X_CAPO, _y_capo(Z_CAPO) - 0.03 - Y_COFRE, 0.01), (0, (_y_capo(Z_CAPO) - 0.03 + Y_COFRE) / 2, Z_CAPO + 0.006), chanfro=0)


def _capo(cena):
    """O capô: a chapa de cima (a mesma linha dos para-lamas) e a dobra da frente, a origem na dobradiça (atrás, em
    cima, no meio). Abre girando em X (a frente sobe)."""
    m = Malha()
    zt, zf = Z_CAPO, 2.085
    cima = [(zt, _y_capo(zt)), (2.02, 0.84), (zf, 0.805)]
    baixo = [(zf - 0.012, 0.783), (2.02, 0.815), (zt, _y_capo(zt) - 0.025)]
    m.perfil("pintura", cima + baixo, -X_CAPO, X_CAPO)
    # o reforço por baixo (escuro), em X
    yb = lambda z: _y_capo(z) - 0.03
    m.ripa("interior", (-X_CAPO + 0.08, yb(zt + 0.08), zt + 0.08), (X_CAPO - 0.08, yb(1.95), 1.95), 0.05, 0.01)
    m.ripa("interior", (X_CAPO - 0.08, yb(zt + 0.08), zt + 0.08), (-X_CAPO + 0.08, yb(1.95), 1.95), 0.05, 0.01)
    return m.objeto("Carro_Capo", cena, pivo=(0, _y_capo(zt), zt))


def _tampa(cena):
    """A tampa traseira da perua (o porta-malas): a chapa de baixo, com a placa e a maçaneta, e o vidro de trás com a
    moldura, a origem na dobradiça (em cima, embaixo da borda do teto). Abre girando em X: a borda de baixo sobe e vai
    para trás (o giro positivo na Unity)."""
    m = Malha()
    x = X_MALAS - 0.005
    (zb, yb), (zt, yt) = VIDRO_TRAS
    chapa = [(-2.158, Y_SOLEIRA), (-2.15, 0.93), (zb + 0.01, yb + 0.005), (zb + 0.05, yb - 0.02), (-2.10, 0.92), (-2.115, Y_SOLEIRA)]
    m.perfil("pintura", chapa, -x, x)
    m.ripa("grade", (0, yb, zb), (0, yt, zt), 2 * x - 0.08, 0.01, mat=VIDRO)
    for s in (-1, 1):
        m.ripa("plastico", (s * (x - 0.025), yb, zb), (s * (x - 0.025), yt, zt), 0.05, 0.03)
    m.caixa("pintura", (2 * x, 0.03, 0.08), (0, yt + 0.015, zt + 0.01), chanfro=0)
    m.caixa("placa", (0.36, 0.12, 0.02), (0, 0.74, -2.165), chanfro=0)
    m.caixa("plastico", (0.30, 0.035, 0.02), (0, 0.88, -2.160), chanfro=0)
    # o forro de dentro
    m.caixa("interior", (2 * x - 0.06, 0.26, 0.012), (0, 0.78, -2.092), chanfro=0)
    return m.objeto("Carro_Tampa", cena, pivo=PIVO_TAMPA)


def _sem_repetidos(pts):
    saida = []
    for p in pts:
        if not saida or (abs(saida[-1][0] - p[0]) > 1e-4 or abs(saida[-1][1] - p[1]) > 1e-4):
            saida.append(p)
    if len(saida) > 1 and abs(saida[0][0] - saida[-1][0]) < 1e-4 and abs(saida[0][1] - saida[-1][1]) < 1e-4:
        saida.pop()
    return saida


def _porta(cena, nome, s, frente):
    """Uma porta (s = -1 esquerda, +1 direita; frente = a dianteira), com a origem na dobradiça."""
    m = Malha()
    if frente:
        z0, z1 = Z_PORTA_D, Z_COLUNA_B
    else:
        z0, z1 = Z_PORTA_T, Z_COLUNA_C
    meio, comp = (z0 + z1) / 2, z0 - z1
    y0 = 0.36
    # a chapa de fora, o forro de dentro e o descanso de braço
    m.caixa("pintura", (0.035, Y_CINTURA - y0, comp), (s * 0.8425, (Y_CINTURA + y0) / 2, meio), chanfro=0.008)
    m.caixa("interior", (0.04, Y_CINTURA - y0 - 0.02, comp - 0.04), (s * 0.805, (Y_CINTURA + y0) / 2, meio), chanfro=0.006)
    m.caixa("painel", (0.07, 0.04, comp * 0.42), (s * 0.765, 0.66, meio - comp * 0.08), chanfro=0.008)
    # a maçaneta, perto da borda de trás
    m.caixa("plastico", (0.02, 0.03, 0.13), (s * 0.866, 0.80, z1 + 0.12), chanfro=0.005)
    # a moldura da janela (preta) e o vidro
    yt = Y_TETO - 0.08
    if frente:
        (zb, yb), (zt, ytp) = PARABRISA
        # a borda da frente segue a coluna A, um pouco atrás dela
        fa, fb = (zb - 0.10, Y_CINTURA), (zt - 0.04, yt)
        m.ripa("plastico", (s * 0.82, fa[1], fa[0]), (s * 0.82, fb[1], fb[0]), 0.05, 0.04)
        m.caixa("plastico", (0.05, 0.04, fb[0] - z1), (s * 0.82, yt, (fb[0] + z1) / 2), chanfro=0)
        m.caixa("plastico", (0.05, yt - Y_CINTURA, 0.04), (s * 0.82, (yt + Y_CINTURA) / 2, z1 + 0.02), chanfro=0)
        vidro = [(fa[0] - 0.03, Y_CINTURA + 0.01), (fb[0] + 0.0, yt - 0.02), (z1 + 0.04, yt - 0.02), (z1 + 0.04, Y_CINTURA + 0.01)]
        # o retrovisor de fora, no canto da frente
        m.caixa("plastico", (0.10, 0.04, 0.06), (s * 0.90, 0.98, zb - 0.16), chanfro=0)
        m.caixa("plastico", (0.08, 0.12, 0.16), (s * 0.97, 1.00, zb - 0.18), chanfro=0.015)
        m.caixa("roda", (0.06, 0.09, 0.006), (s * 0.97, 1.00, zb - 0.262), chanfro=0)
    else:
        for z in (z0 - 0.02, z1 + 0.02):
            m.caixa("plastico", (0.05, yt - Y_CINTURA, 0.04), (s * 0.82, (yt + Y_CINTURA) / 2, z), chanfro=0)
        m.caixa("plastico", (0.05, 0.04, comp), (s * 0.82, yt, meio), chanfro=0)
        vidro = [(z0 - 0.04, Y_CINTURA + 0.01), (z0 - 0.04, yt - 0.02), (z1 + 0.04, yt - 0.02), (z1 + 0.04, Y_CINTURA + 0.01)]
    m.perfil("grade", vidro, s * 0.812, s * 0.828, mat=VIDRO)
    return m.objeto(nome, cena, pivo=(s * W, 0, z0))


def _roda(cena):
    m = Malha()
    m.cilindro("borracha", R_RODA, L_RODA, (0, 0, 0), eixo="x", lados=18)
    # o ombro do pneu, um pouco mais estreito, e o aro com a calota
    m.cilindro("borracha", R_RODA - 0.035, L_RODA + 0.02, (0, 0, 0), eixo="x", lados=18)
    m.cilindro("roda", 0.19, 0.03, (L_RODA / 2 + 0.002, 0, 0), eixo="x", lados=14)
    m.cilindro("calota", 0.065, 0.03, (L_RODA / 2 + 0.012, 0, 0), eixo="x", lados=10)
    # os furos do aro (mostram a roda girando)
    for i in range(5):
        a = 72 * i
        r = math.radians(a)
        m.caixa("grade", (0.012, 0.05, 0.075), (L_RODA / 2 + 0.012, 0.125 * math.sin(r), 0.125 * math.cos(r)), giro=(-a, 0, 0), chanfro=0)
    return m.objeto("Carro_Roda", cena)


def _volante(cena):
    m = Malha()
    n = 18
    for i in range(n):
        a = 360.0 * i / n
        r = math.radians(a)
        m.caixa("plastico", (0.036, 0.072, 0.036), (0.19 * math.cos(r), 0.19 * math.sin(r), 0), giro=(0, 0, a), chanfro=0)
    for s in (-1, 1):
        m.caixa("interior", (0.16, 0.035, 0.02), (s * 0.10, -0.01, 0.01), chanfro=0)
    m.caixa("interior", (0.035, 0.16, 0.02), (0, -0.10, 0.01), chanfro=0)
    m.caixa("interior", (0.11, 0.09, 0.05), (0, -0.005, 0.02), chanfro=0.012)
    return m.objeto("Carro_Volante", cena)


NOMES = ["Carro_Carroceria", "Carro_Porta_DE", "Carro_Porta_DD", "Carro_Porta_TE", "Carro_Porta_TD", "Carro_Roda", "Carro_Volante",
         "Carro_Capo", "Carro_Tampa"]


def _cena():
    cena = bpy.data.scenes.get("Carro") or bpy.data.scenes.new("Carro")
    bpy.context.window.scene = cena
    return cena


def construir():
    cena = _cena()
    for o in list(cena.objects):
        if o.name.split(".")[0] in NOMES:
            me = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if me is not None and me.users == 0:
                bpy.data.meshes.remove(me)
    objs = [_carroceria(cena)]
    for nome, s, frente in (("Carro_Porta_DE", -1, True), ("Carro_Porta_DD", 1, True), ("Carro_Porta_TE", -1, False), ("Carro_Porta_TD", 1, False)):
        o = _porta(cena, nome, s, frente)
        # no Blender, a porta fica no lugar dela (a origem na dobradiça), para conferir o conjunto
        z0 = Z_PORTA_D if frente else Z_PORTA_T
        o.location = _para_blender((s * W, 0, z0))
        objs.append(o)
    roda = _roda(cena)
    roda.location = (-3.0, 0, 0)
    objs.append(roda)
    for i, (x, z) in enumerate(((-X_RODA, EIXO), (X_RODA, EIXO), (-X_RODA, -EIXO), (X_RODA, -EIXO))):
        # cópias só para ver (não são exportadas)
        c = bpy.data.objects.get(f"Ver_Roda_{i}") or bpy.data.objects.new(f"Ver_Roda_{i}", roda.data)
        c.data = roda.data
        if c.name not in cena.objects:
            cena.collection.objects.link(c)
        c.location = _para_blender((x, R_RODA, z))
        c.rotation_euler = (math.radians(90), 0, math.radians(180) if x < 0 else 0)
    vol = _volante(cena)
    vol.location = _para_blender((-0.37, 0.86, 0.50))
    vol.rotation_euler = (math.radians(90 + 25), 0, 0)
    objs.append(vol)
    capo = _capo(cena)
    capo.location = _para_blender((0, _y_capo(Z_CAPO), Z_CAPO))
    objs.append(capo)
    tampa = _tampa(cena)
    tampa.location = _para_blender(PIVO_TAMPA)
    objs.append(tampa)
    return objs


def _para_blender(p):
    """Um ponto nos eixos da Unity para a posição de um objeto no Blender (o X espelhado, o Y da Unity no Z)."""
    x, y, z = p
    return (-x, -z, y)


def exportar():
    cena = _cena()
    os.makedirs(DESTINO, exist_ok=True)
    guardado = {}
    for o in cena.objects:
        o.select_set(o.name in NOMES)
        if o.name in NOMES:
            # exporta cada peça na origem (a Unity só usa as malhas)
            guardado[o.name] = (tuple(o.location), tuple(o.rotation_euler))
            o.location = (0, 0, 0)
            o.rotation_euler = (math.radians(90), 0, 0)
    bpy.ops.export_scene.fbx(filepath=os.path.join(DESTINO, "Carro.fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")
    for nome, (loc, rot) in guardado.items():
        cena.objects[nome].location = loc
        cena.objects[nome].rotation_euler = rot


def foto(caminho, direcao=(0.9, 0.55, 1.0), resolucao=(900, 600), alvo=(0, 0.7, 0), distancia=7.5, lente=50,
         esconder=()):
    """Uma foto no Workbench (a textura, a cavidade e o contorno), de uma direção (eixos da Unity)."""
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
    for o in cena.objects:
        o.hide_render = o.name in esconder or o.name.startswith("Camera") and o.type != "CAMERA"
    cam = bpy.data.objects.get("CameraDoCarro")
    if cam is None:
        cam = bpy.data.objects.new("CameraDoCarro", bpy.data.cameras.new("CameraDoCarro"))
    if cam.name not in cena.objects:
        cena.collection.objects.link(cam)
    cam.data.lens = lente
    cam.data.clip_start = 0.02
    d = Vector(_para_blender(direcao)).normalized()
    a = Vector(_para_blender(alvo))
    cam.location = a + d * distancia
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cena.camera = cam
    r.filepath = caminho
    bpy.ops.render.render(write_still=True)
    for o in cena.objects:
        o.hide_render = False
