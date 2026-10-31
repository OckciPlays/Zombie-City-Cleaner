# A pistola nova (29/10/2026, a pedido: "um modelo um pouco mais realista do que temos hoje", pela foto de referência do
# Pedro): uma semiautomática de aço gasta e enferrujada, com o ferrolho serrilhado atrás, a janela de ejeção, as miras de
# três pontos, o trilho embaixo do cano, o guarda-mato quadrado, as placas do punho e o carregador comprido enrolado em
# fita de pano.
#
# É feita no mesmo espaço e nas mesmas três peças da pistola antiga (a do Armas.fbx do Gustavo), para entrar no lugar
# dela sem mexer nas animações: o corpo (no osso "gun"), o ferrolho ("slide", corre para trás no tiro) e o carregador
# ("mag", sai na recarga). O espaço da arma, aqui no Blender: o cano para -Y, o alto em +Z, a direita da arma em -X e a
# origem no cabo (onde a mão segura). O cabo, a altura do cano (z 0,093) e o comprimento do ferrolho são os da antiga:
# as mãos das animações seguram a nova igual.
#
# A cor não é a da paleta: é uma textura própria (1024 x 1024), assada de materiais feitos com nós (o aço escuro, as
# quinas gastas, a ferrugem nos cantos, o punho, a fita suja). Duas imagens: Pistola_Cor (a cor) e Pistola_MS (o metal
# no R, a lisura no A, como o "Metallic" do URP).
#
# Como rodar (num Blender em segundo plano; não mexe no Blender aberto):
#   blender -b --factory-startup --python Blender/Pistola/pistola.py -- tudo
# (ou as etapas: construir, assar, exportar, imagens). Grava o Blender/Pistola/Pistola.blend, o
# Assets/_Game/Art/Characters/Jogador/Armas_Pistola.fbx, as texturas em Art/Characters/Jogador/Texturas e o ícone e a
# foto em Art/UI/Icones. Depois, na Unity: Zombie City Cleaner > Armas > Montar a pistola nova.

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(AQUI))
JOGADOR = os.path.join(REPO, "Assets", "_Game", "Art", "Characters", "Jogador")
TEXTURAS = os.path.join(JOGADOR, "Texturas")
ICONES = os.path.join(REPO, "Assets", "_Game", "Art", "UI", "Icones")
BLEND = os.path.join(AQUI, "Pistola.blend")
TAMANHO = 1024

NOMES = ["Pistola_corpo", "Pistola_ferrolho", "Pistola_carregador"]
MATERIAIS = ["aco", "punho", "fita", "escuro", "branco", "camara"]

# ---------------------------------------------------------------------------------------------------- medidas
Y_FRENTE, Y_TRAS = -0.190, 0.014          # o ferrolho, da boca até atrás
Z_BASE, Z_TOPO = 0.072, 0.120              # o ferrolho, de baixo até em cima
MEIA = 0.0135                              # a meia largura do ferrolho
Z_CANO = 0.0935                            # o eixo do cano
RAKE = 11.0                                # a inclinação do punho (graus; o de baixo vai para trás)
PIVO_PUNHO = Vector((0.0, -0.004, 0.045))  # o alto do punho, no meio


# ---------------------------------------------------------------------------------------------------- a malha
class Malha:
    """Peças num bmesh, cada uma com o material dela, chanfradas, com uma pilha de transformações."""

    def __init__(self):
        self.bm = bmesh.new()
        self.M = Matrix.Identity(4)
        self._pilha = []

    def entrar(self, m):
        self._pilha.append(self.M)
        self.M = self.M @ m

    def sair(self):
        self.M = self._pilha.pop()

    def _fim(self, vs, mat, chanfro, seg=1):
        bmesh.ops.transform(self.bm, matrix=self.M, verts=vs)
        faces = list({f for v in vs for f in v.link_faces})
        idx = MATERIAIS.index(mat)
        for f in faces:
            f.material_index = idx
        if chanfro > 0:
            arestas = list({e for f in faces for e in f.edges})
            r = bmesh.ops.bevel(self.bm, geom=arestas, offset=chanfro, segments=seg, affect="EDGES", profile=0.5,
                                clamp_overlap=True)
            for f in r.get("faces", []):
                f.material_index = idx

    def caixa(self, c, t, mat="aco", giro=(0, 0, 0), chanfro=0.0008, seg=1):
        vs = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        m = Matrix.Translation(Vector(c)) @ Matrix.Rotation(math.radians(giro[2]), 4, "Z") @ \
            Matrix.Rotation(math.radians(giro[1]), 4, "Y") @ Matrix.Rotation(math.radians(giro[0]), 4, "X") @ \
            Matrix.Diagonal((t[0], t[1], t[2], 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=vs)
        self._fim(vs, mat, chanfro if min(t) > chanfro * 2.6 else 0.0, seg)

    def de_ate(self, a, b, mat="aco", **k):
        """Uma caixa de um canto (x0, y0, z0) a outro (x1, y1, z1)."""
        c = [(a[i] + b[i]) / 2 for i in range(3)]
        t = [abs(b[i] - a[i]) for i in range(3)]
        self.caixa(c, t, mat, **k)

    def prisma_y(self, perfil, y0, y1, mat="aco", chanfro=0.0008, seg=1):
        """Um polígono no plano XZ (pontos (x, z)), puxado em Y de y0 a y1."""
        a = [self.bm.verts.new((x, y0, z)) for x, z in perfil]
        b = [self.bm.verts.new((x, y1, z)) for x, z in perfil]
        n = len(perfil)
        self.bm.faces.new(a)
        self.bm.faces.new(list(reversed(b)))
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((a[i], a[j], b[j], b[i]))
        self._fim(a + b, mat, chanfro, seg)

    def cilindro(self, c, eixo, raio, comp, mat="aco", lados=16, chanfro=0.0):
        r = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=lados, radius1=raio, radius2=raio,
                                  depth=comp)
        vs = r["verts"]
        giro = Matrix.Rotation(math.radians(90), 4, "X") if eixo == "y" else \
            Matrix.Rotation(math.radians(90), 4, "Y") if eixo == "x" else Matrix.Identity(4)
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(Vector(c)) @ giro, verts=vs)
        self._fim(vs, mat, chanfro)

    def objeto(self, nome, colecao):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-6)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(nome)
        self.bm.to_mesh(me)
        self.bm.free()
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=math.radians(38))
        for nome_mat in MATERIAIS:
            me.materials.append(_material(nome_mat))
        o = bpy.data.objects.new(nome, me)
        colecao.objects.link(o)
        return o


def _punho():
    """O espaço do punho: inclinado RAKE graus em volta do alto dele (o de baixo vai para trás, +Y)."""
    return Matrix.Translation(PIVO_PUNHO) @ Matrix.Rotation(math.radians(RAKE), 4, "X") @ Matrix.Translation(-PIVO_PUNHO)


# ---------------------------------------------------------------------------------------------------- as peças
def _corpo(m):
    # a armação de cima (embaixo do ferrolho, da frente até atrás) e o trilho de acessórios embaixo do cano
    m.de_ate((-0.0128, -0.184, 0.046), (0.0128, 0.026, 0.0725), chanfro=0.0012, seg=2)
    m.de_ate((-0.0108, -0.172, 0.040), (0.0108, -0.110, 0.0465), chanfro=0.0006)
    for k in range(5):
        y = -0.1665 + k * 0.0120
        m.de_ate((-0.0108, y - 0.0027, 0.0355), (0.0108, y + 0.0027, 0.0405), chanfro=0.0005)
    # o cano saindo na frente do ferrolho (fica parado: o ferrolho corre por cima dele) e a boca escura
    m.cilindro((0, -0.1880, Z_CANO), "y", 0.0072, 0.0110, "aco", lados=18)
    m.cilindro((0, -0.1932, Z_CANO), "y", 0.0047, 0.0012, "escuro", lados=14)
    # a guia da mola, embaixo do cano
    m.cilindro((0, -0.1848, 0.0615), "y", 0.0042, 0.0020, "escuro", lados=12)
    # o guarda-mato quadrado: a frente, o de baixo e a junção com o punho
    m.de_ate((-0.0054, -0.1125, 0.0200), (0.0054, -0.1010, 0.0470), chanfro=0.0018, seg=2)
    m.de_ate((-0.0054, -0.1125, 0.0160), (0.0054, -0.0240, 0.0262), chanfro=0.0018, seg=2)
    # o gatilho (a lâmina curva) e o pino
    m.caixa((0, -0.0672, 0.0455), (0.0052, 0.0060, 0.0110), giro=(-8, 0, 0), chanfro=0.0012)
    m.caixa((0, -0.0700, 0.0350), (0.0050, 0.0058, 0.0110), giro=(-24, 0, 0), chanfro=0.0012)
    for s in (-1, 1):
        m.cilindro((s * 0.01305, -0.0600, 0.0620), "x", 0.0016, 0.0010, "escuro", lados=10)
        m.cilindro((s * 0.01305, -0.0150, 0.0590), "x", 0.0014, 0.0010, "escuro", lados=10)
    # do lado esquerdo (+X): a trava do ferrolho (com o apoio do dedão), a alavanca de desmontar e o botão do carregador
    m.de_ate((0.0127, -0.068, 0.0645), (0.0142, -0.024, 0.0705), chanfro=0.0005)
    m.de_ate((0.0127, -0.031, 0.0570), (0.0150, -0.019, 0.0710), chanfro=0.0008)
    m.de_ate((0.0127, -0.099, 0.0600), (0.0141, -0.085, 0.0665), chanfro=0.0005)
    # a cauda de castor, atrás em cima do punho
    m.caixa((0, 0.0285, 0.0655), (0.0200, 0.0130, 0.0085), giro=(-18, 0, 0), chanfro=0.0025, seg=2)

    # o punho (inclinado): o miolo de aço, as placas de punho dos dois lados, a boca do carregador embaixo
    m.entrar(_punho())
    m.de_ate((-0.0150, -0.0280, -0.0700), (0.0150, 0.0200, 0.0500), chanfro=0.0040, seg=3)
    for s in (-1, 1):
        m.de_ate((s * 0.0142, -0.0225, -0.0590), (s * 0.0167, 0.0165, 0.0360), "punho", chanfro=0.0012, seg=2)
        # os parafusos das placas
        for z in (0.0255, -0.0480):
            m.cilindro((s * 0.0168, -0.0030, z), "x", 0.0024, 0.0012, "aco", lados=12)
    m.de_ate((-0.0158, -0.0305, -0.0745), (0.0158, 0.0225, -0.0685), chanfro=0.0015)
    # o botão do carregador, do lado esquerdo, atrás do guarda-mato
    m.de_ate((0.0145, -0.0250, 0.0315), (0.0166, -0.0165, 0.0410), chanfro=0.0006)
    m.sair()


def _ferrolho(m):
    # o ferrolho: o perfil com o alto chanfrado, puxado da boca até atrás
    perfil = [(-MEIA, Z_BASE), (MEIA, Z_BASE), (MEIA, 0.1095), (0.0106, Z_TOPO), (-0.0106, Z_TOPO), (-MEIA, 0.1095)]
    m.prisma_y(perfil, Y_FRENTE, Y_TRAS, chanfro=0.0012, seg=2)
    # o serrilhado de trás, dos dois lados (ripas finas)
    for s in (-1, 1):
        for k in range(8):
            y = 0.0045 - k * 0.0047
            m.de_ate((s * MEIA - s * 0.0002, y - 0.0011, 0.0785), (s * (MEIA + 0.0009), y + 0.0011, 0.1075), chanfro=0.0003)
    # a janela de ejeção (do lado direito, -X): a moldura escura e a câmara do cano aparecendo
    m.de_ate((-0.0141, -0.0765, 0.0985), (-0.0025, -0.0310, 0.1206), "escuro", chanfro=0.0004)
    m.de_ate((-0.0143, -0.0725, 0.1015), (-0.0055, -0.0350, 0.1180), "camara", chanfro=0.0008)
    # a mira da frente, com o ponto branco atrás
    m.de_ate((-0.0016, -0.1830, Z_TOPO - 0.001), (0.0016, -0.1770, 0.1275), chanfro=0.0006)
    m.de_ate((-0.0009, -0.1769, 0.1240), (0.0009, -0.1764, 0.1258), "branco")
    # a mira de trás: a base e as duas orelhas, com os pontos brancos
    m.de_ate((-0.0105, 0.0030, Z_TOPO - 0.001), (0.0105, 0.0105, 0.1230), chanfro=0.0008)
    for s in (-1, 1):
        m.de_ate((s * 0.0030, 0.0030, 0.1220), (s * 0.0100, 0.0105, 0.1285), chanfro=0.0007)
        m.de_ate((s * 0.0057, 0.0106, 0.1245), (s * 0.0073, 0.0111, 0.1261), "branco")
    # a placa de trás do ferrolho, um pouco mais escura
    m.de_ate((-0.0115, Y_TRAS - 0.0005, 0.0760), (0.0115, Y_TRAS + 0.0008, 0.1150), "escuro", chanfro=0.0006)


def _carregador(m):
    # no espaço do punho: o tubo (escondido no punho até sair embaixo), a base e a fita enrolada
    m.entrar(_punho())
    m.de_ate((-0.0112, -0.0205, -0.1300), (0.0112, 0.0140, 0.0400), chanfro=0.0012)
    m.de_ate((-0.0131, -0.0245, -0.1365), (0.0131, 0.0190, -0.1290), chanfro=0.0018, seg=2)
    # as voltas de fita, cada uma um pouco torta, de cima para baixo (uma por cima da outra)
    voltas = [(-0.0820, (2.5, 0.0, -1.5)), (-0.0935, (-2.0, 1.5, 1.0)), (-0.1045, (3.0, -1.0, 2.0)), (-0.1155, (-1.5, 0.5, -2.5))]
    for k, (zc, giro) in enumerate(voltas):
        folga = 0.0014 + 0.0003 * (k % 2)
        m.caixa((0.0, -0.0032, zc), (0.0224 + 2 * folga, 0.0345 + 2 * folga, 0.0150), "fita", giro=giro, chanfro=0.0010, seg=2)
    # a ponta da fita, solta do lado esquerdo
    m.caixa((0.0128, 0.0040, -0.1220), (0.0008, 0.0110, 0.0160), "fita", giro=(14, 0, 6), chanfro=0.0)
    m.sair()


def _colecao():
    col = bpy.data.collections.get("Pistola")
    if col is None:
        col = bpy.data.collections.new("Pistola")
        bpy.context.scene.collection.children.link(col)
    return col


def construir():
    for n in NOMES:
        o = bpy.data.objects.get(n)
        if o is not None:
            me = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if me.users == 0:
                bpy.data.meshes.remove(me)
    col = _colecao()
    objs = []
    for nome, f in (("Pistola_corpo", _corpo), ("Pistola_ferrolho", _ferrolho), ("Pistola_carregador", _carregador)):
        m = Malha()
        f(m)
        objs.append(m.objeto(nome, col))
    for o in objs:
        print(o.name, len(o.data.polygons), "faces", sum(len(p.vertices) - 2 for p in o.data.polygons), "triângulos")
    return objs


# ---------------------------------------------------------------------------------------------------- os materiais (para assar)
def _no(nt, tipo, x, y, **props):
    n = nt.nodes.new(tipo)
    n.location = (x, y)
    for k, v in props.items():
        setattr(n, k, v)
    return n


def _mistura(nt, a, b, fator, x, y, tipo="MIX"):
    n = nt.nodes.new("ShaderNodeMix")
    n.data_type = "RGBA"
    n.blend_type = tipo
    n.location = (x, y)
    nt.links.new(fator, n.inputs["Factor"])
    for s, v in ((n.inputs[6], a), (n.inputs[7], b)):
        if isinstance(v, (tuple, list)):
            s.default_value = (*v, 1.0) if len(v) == 3 else v
        else:
            nt.links.new(v, s)
    return n.outputs[2]


def _valor(nt, a, b, fator, x, y):
    n = nt.nodes.new("ShaderNodeMix")
    n.data_type = "FLOAT"
    n.location = (x, y)
    nt.links.new(fator, n.inputs["Factor"])
    for s, v in ((n.inputs[2], a), (n.inputs[3], b)):
        if isinstance(v, (int, float)):
            s.default_value = v
        else:
            nt.links.new(v, s)
    return n.outputs[0]


def _faixa(nt, entrada, a, b, x, y):
    n = _no(nt, "ShaderNodeMapRange", x, y, clamp=True)
    n.inputs["From Min"].default_value = a
    n.inputs["From Max"].default_value = b
    nt.links.new(entrada, n.inputs["Value"])
    return n.outputs["Result"]


def _ruido(nt, escala, detalhe, x, y, w=None):
    n = _no(nt, "ShaderNodeTexNoise", x, y)
    n.inputs["Scale"].default_value = escala
    n.inputs["Detail"].default_value = detalhe
    n.inputs["Roughness"].default_value = 0.6
    if w is not None:
        n.noise_dimensions = "4D"
        n.inputs["W"].default_value = w
    return n.outputs["Fac"]


def _material(nome):
    """Os materiais de autoria: cada um dá a cor, o metal e a aspereza por nós (as quinas gastas pelo Bevel, os cantos
    pelo AO, a ferrugem e a sujeira por ruído). Os três ficam em nós de valor nomeados ("cor", "metal", "aspereza")
    para a etapa de assar ligar cada um a uma emissão."""
    mat = bpy.data.materials.get("Pistola_" + nome)
    if mat is not None:
        return mat
    mat = bpy.data.materials.new("Pistola_" + nome)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    saida = _no(nt, "ShaderNodeOutputMaterial", 1200, 0)
    bsdf = _no(nt, "ShaderNodeBsdfPrincipled", 900, 0)
    nt.links.new(bsdf.outputs[0], saida.inputs["Surface"])

    geo = _no(nt, "ShaderNodeNewGeometry", -1400, 300)
    bev = _no(nt, "ShaderNodeBevel", -1400, 100)
    bev.samples = 8
    bev.inputs["Radius"].default_value = 0.0011
    dot = _no(nt, "ShaderNodeVectorMath", -1200, 200, operation="DOT_PRODUCT")
    nt.links.new(geo.outputs["Normal"], dot.inputs[0])
    nt.links.new(bev.outputs["Normal"], dot.inputs[1])
    # a quina: 1 - (normal arredondada . normal)
    quina = _faixa(nt, dot.outputs["Value"], 0.985, 0.90, -1000, 200)
    ao = _no(nt, "ShaderNodeAmbientOcclusion", -1400, -100, samples=16, only_local=True)
    ao.inputs["Distance"].default_value = 0.010
    canto = _faixa(nt, ao.outputs["AO"], 0.85, 0.35, -1000, -100)   # 1 nos cantos fechados

    if nome == "aco":
        quebra = _ruido(nt, 260.0, 4.0, -1000, 450)
        gasto = _faixa(nt, _valor(nt, quina, 0.0, quebra, -800, 400), 0.15, 0.55, -600, 400)
        # a ferrugem: manchas grandes de ruído, mais nos cantos e nas partes de baixo
        manchas = _faixa(nt, _ruido(nt, 22.0, 8.0, -1000, -300), 0.58, 0.68, -800, -300)
        miuda = _faixa(nt, _ruido(nt, 140.0, 6.0, -1000, -500), 0.55, 0.66, -800, -500)
        fer = _no(nt, "ShaderNodeMath", -600, -350, operation="MAXIMUM")
        nt.links.new(manchas, fer.inputs[0])
        nt.links.new(_valor(nt, 0.0, miuda, canto, -700, -450), fer.inputs[1])
        ferrugem = fer.outputs[0]
        tom = _ruido(nt, 90.0, 5.0, -1000, -700)
        cor_ferrugem = _mistura(nt, (0.085, 0.045, 0.025), (0.24, 0.11, 0.045), tom, -400, -600)
        base0 = _mistura(nt, (0.034, 0.034, 0.037), (0.052, 0.050, 0.052), _ruido(nt, 400.0, 3.0, -1000, 650), -400, 600)
        # a pátina: o aço manchado de marrom em áreas grandes (de leve)
        patina = _faixa(nt, _ruido(nt, 7.0, 5.0, -1000, 850), 0.42, 0.72, -800, 850)
        base = _mistura(nt, base0, (0.075, 0.052, 0.036), _valor(nt, 0.0, 0.65, patina, -600, 800), -300, 700)
        c1 = _mistura(nt, base, (0.34, 0.33, 0.31), gasto, -200, 450)
        c2 = _mistura(nt, c1, cor_ferrugem, ferrugem, 0, 200)
        sujo = _mistura(nt, c2, (0.025, 0.020, 0.015), _valor(nt, 0.0, 0.55, canto, -100, 0), 200, 100)
        # o metal não é puro: com 0,95 o aço refletia o céu e ficava azul no jogo
        metal = _valor(nt, _valor(nt, 0.72, 0.9, gasto, -200, -800), 0.1, ferrugem, 0, -800)
        aspereza = _valor(nt, _valor(nt, 0.42, 0.28, gasto, -200, -950), 0.88, ferrugem, 0, -950)
        cor = sujo
    elif nome == "punho":
        pontos = _ruido(nt, 900.0, 2.0, -1000, 450)
        base = _mistura(nt, (0.020, 0.020, 0.022), (0.045, 0.044, 0.046), pontos, -600, 450)
        c1 = _mistura(nt, base, (0.10, 0.10, 0.10), _faixa(nt, quina, 0.2, 0.8, -800, 250), -300, 300)
        lama = _faixa(nt, _ruido(nt, 45.0, 6.0, -1000, -300), 0.55, 0.70, -800, -300)
        cor = _mistura(nt, c1, (0.07, 0.055, 0.040), _valor(nt, lama, 1.0, canto, -500, -200), 0, 100)
        metal = _valor(nt, 0.0, 0.0, canto, 0, -800)
        aspereza = _valor(nt, 0.78, 0.9, pontos, 0, -950)
    elif nome == "fita":
        trama = _no(nt, "ShaderNodeTexWave", -1000, 450)
        trama.inputs["Scale"].default_value = 260.0
        trama.inputs["Distortion"].default_value = 1.5
        fio = _faixa(nt, trama.outputs["Fac"], 0.2, 0.9, -800, 450)
        base = _mistura(nt, (0.24, 0.21, 0.155), (0.34, 0.31, 0.235), fio, -600, 450)
        manchas = _faixa(nt, _ruido(nt, 28.0, 7.0, -1000, -300), 0.50, 0.68, -800, -300)
        graxa = _mistura(nt, (0.22, 0.17, 0.11), (0.12, 0.09, 0.06), _ruido(nt, 120.0, 4.0, -1000, -500), -600, -400)
        c1 = _mistura(nt, base, graxa, manchas, -300, 100)
        cor = _mistura(nt, c1, (0.10, 0.08, 0.05), _valor(nt, 0.0, 0.7, canto, -400, -100), 0, 100)
        metal = _valor(nt, 0.0, 0.0, canto, 0, -800)
        aspereza = _valor(nt, 0.93, 0.98, fio, 0, -950)
    elif nome == "escuro":
        cor = _mistura(nt, (0.010, 0.010, 0.011), (0.03, 0.025, 0.02), _ruido(nt, 200.0, 3.0, -1000, 450), -300, 300)
        metal = _valor(nt, 0.6, 0.6, canto, 0, -800)
        aspereza = _valor(nt, 0.55, 0.55, canto, 0, -950)
    elif nome == "branco":
        cor = _mistura(nt, (0.80, 0.79, 0.72), (0.55, 0.50, 0.40), _ruido(nt, 300.0, 3.0, -1000, 450), -300, 300)
        metal = _valor(nt, 0.0, 0.0, canto, 0, -800)
        aspereza = _valor(nt, 0.6, 0.6, canto, 0, -950)
    else:   # a câmara do cano, vista pela janela: aço mais claro e liso, com sujeira de pólvora nos cantos
        cor = _mistura(nt, (0.30, 0.29, 0.27), (0.06, 0.05, 0.04), _valor(nt, 0.2, 0.9, canto, -500, 0), -300, 300)
        metal = _valor(nt, 1.0, 1.0, canto, 0, -800)
        aspereza = _valor(nt, 0.3, 0.5, canto, 0, -950)

    # os três valores em nós nomeados, e o BSDF para ver no Blender
    for rotulo, sock, x, y in (("cor", cor, 600, 300), ("metal", metal, 600, -300), ("aspereza", aspereza, 600, -450)):
        r = _no(nt, "NodeReroute", x, y)
        r.name = rotulo
        r.label = rotulo
        nt.links.new(sock, r.inputs[0])
    nt.links.new(nt.nodes["cor"].outputs[0], bsdf.inputs["Base Color"])
    nt.links.new(nt.nodes["metal"].outputs[0], bsdf.inputs["Metallic"])
    nt.links.new(nt.nodes["aspereza"].outputs[0], bsdf.inputs["Roughness"])
    return mat


# ---------------------------------------------------------------------------------------------------- UV, assar, exportar
def _objetos():
    return [bpy.data.objects[n] for n in NOMES]


def _selecionar(objs, ativo=None):
    for o in bpy.context.scene.objects:
        if o is not None:
            o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = ativo or objs[0]


def desembrulhar():
    objs = _objetos()
    _selecionar(objs)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(55), island_margin=0.004, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=0.006)
    bpy.ops.object.mode_set(mode="OBJECT")


def _imagem(nome, cor=True):
    img = bpy.data.images.get(nome)
    if img is not None:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(nome, TAMANHO, TAMANHO, alpha=False, float_buffer=False)
    if not cor:
        img.colorspace_settings.name = "Non-Color"
    return img


def assar(amostras=48, margem=6):
    """Assa a cor, o metal e a aspereza dos materiais de autoria no atlas (por emissão: o valor, sem luz)."""
    cena = bpy.context.scene
    cena.render.engine = "CYCLES"
    cena.cycles.device = "CPU"
    cena.cycles.samples = amostras
    cena.render.bake.margin = margem
    cena.render.bake.use_clear = True
    objs = _objetos()
    imagens = {"cor": _imagem("Pistola_Cor_assada"), "metal": _imagem("Pistola_Metal_assado", False),
               "aspereza": _imagem("Pistola_Aspereza_assada", False)}
    mats = [bpy.data.materials["Pistola_" + n] for n in MATERIAIS]
    for canal, img in imagens.items():
        for mat in mats:
            nt = mat.node_tree
            for n in [n for n in nt.nodes if n.name.startswith("_assar")]:
                nt.nodes.remove(n)
            em = _no(nt, "ShaderNodeEmission", 900, -600)
            em.name = "_assar_emissao"
            nt.links.new(nt.nodes[canal].outputs[0], em.inputs["Color"])
            nt.links.new(em.outputs[0], nt.nodes["Material Output"].inputs["Surface"])
            tex = _no(nt, "ShaderNodeTexImage", 900, -800)
            tex.name = "_assar_imagem"
            tex.image = img
            nt.nodes.active = tex
        _selecionar(objs)
        bpy.ops.object.bake(type="EMIT", margin=margem)
        print("assado:", canal)
    # volta o BSDF
    for mat in mats:
        nt = mat.node_tree
        for n in [n for n in nt.nodes if n.name.startswith("_assar")]:
            nt.nodes.remove(n)
        nt.links.new(nt.nodes["Principled BSDF"].outputs[0], nt.nodes["Material Output"].inputs["Surface"])
    _gravar_texturas(imagens)
    _material_assado()


def _gravar_texturas(imagens):
    import numpy as np
    os.makedirs(TEXTURAS, exist_ok=True)
    n = TAMANHO * TAMANHO
    cor = np.array(imagens["cor"].pixels[:], dtype=np.float32).reshape(n, 4)
    metal = np.array(imagens["metal"].pixels[:], dtype=np.float32).reshape(n, 4)[:, 0]
    asp = np.array(imagens["aspereza"].pixels[:], dtype=np.float32).reshape(n, 4)[:, 0]
    saida_cor = bpy.data.images.get("Pistola_Cor") or bpy.data.images.new("Pistola_Cor", TAMANHO, TAMANHO, alpha=False)
    saida_cor.pixels.foreach_set(cor.ravel())
    saida_cor.filepath_raw = os.path.join(TEXTURAS, "Pistola_Cor.png")
    saida_cor.file_format = "PNG"
    saida_cor.save()
    ms = np.zeros((n, 4), dtype=np.float32)
    ms[:, 0] = metal
    ms[:, 3] = 1.0 - asp
    saida_ms = bpy.data.images.get("Pistola_MS") or bpy.data.images.new("Pistola_MS", TAMANHO, TAMANHO, alpha=True)
    saida_ms.colorspace_settings.name = "Non-Color"
    saida_ms.pixels.foreach_set(ms.ravel())
    saida_ms.filepath_raw = os.path.join(TEXTURAS, "Pistola_MS.png")
    saida_ms.file_format = "PNG"
    saida_ms.save()
    print("texturas gravadas em", TEXTURAS)


def _material_assado():
    """O material que vai para o FBX (Pistola_Textura): as texturas assadas num BSDF (também o que o ícone usa)."""
    mat = bpy.data.materials.get("Pistola_Textura") or bpy.data.materials.new("Pistola_Textura")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    saida = _no(nt, "ShaderNodeOutputMaterial", 600, 0)
    bsdf = _no(nt, "ShaderNodeBsdfPrincipled", 300, 0)
    nt.links.new(bsdf.outputs[0], saida.inputs["Surface"])
    cor = _no(nt, "ShaderNodeTexImage", -200, 200)
    cor.image = bpy.data.images.load(os.path.join(TEXTURAS, "Pistola_Cor.png"), check_existing=True)
    nt.links.new(cor.outputs["Color"], bsdf.inputs["Base Color"])
    ms = _no(nt, "ShaderNodeTexImage", -200, -150)
    ms.image = bpy.data.images.load(os.path.join(TEXTURAS, "Pistola_MS.png"), check_existing=True)
    ms.image.colorspace_settings.name = "Non-Color"
    sep = _no(nt, "ShaderNodeSeparateColor", 0, -150)
    nt.links.new(ms.outputs["Color"], sep.inputs[0])
    nt.links.new(sep.outputs[0], bsdf.inputs["Metallic"])
    inv = _no(nt, "ShaderNodeMath", 100, -300, operation="SUBTRACT")
    inv.inputs[0].default_value = 1.0
    nt.links.new(ms.outputs["Alpha"], inv.inputs[1])
    nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    nt.nodes.active = cor
    return mat


def exportar():
    """O Armas_Pistola.fbx: as três peças com o material assado, no formato do Armas.fbx (cada uma girada 90° em X,
    com a malha nos eixos da Unity: o cano em +Z, o alto em +Y)."""
    mat = _material_assado()
    copias = []
    giro = Matrix.Rotation(math.radians(-90), 4, "X")
    for o in _objetos():
        me = o.data.copy()
        me.transform(giro)
        me.materials.clear()
        me.materials.append(mat)
        c = bpy.data.objects.new(o.name + "_exportar", me)
        c.rotation_euler = (math.radians(90), 0, 0)
        bpy.context.scene.collection.objects.link(c)
        copias.append(c)
    # o nome da peça no FBX é o do objeto: troca os nomes enquanto exporta
    originais = _objetos()
    for o in originais:
        o.name = o.name + "_autoria"
    for c in copias:
        c.name = c.name.replace("_exportar", "")
    _selecionar(copias)
    bpy.ops.export_scene.fbx(filepath=os.path.join(JOGADOR, "Armas_Pistola.fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="OFF",
                             path_mode="STRIP")
    for c in copias:
        me = c.data
        bpy.data.objects.remove(c, do_unlink=True)
        bpy.data.meshes.remove(me)
    for o in originais:
        o.name = o.name.replace("_autoria", "")
    print("exportado:", os.path.join(JOGADOR, "Armas_Pistola.fbx"))


# ---------------------------------------------------------------------------------------------------- o ícone e a foto
def _camera(cena, orto, alvo, direcao, distancia, escala):
    cam = bpy.data.objects.get("CameraDaPistola")
    if cam is None:
        cam = bpy.data.objects.new("CameraDaPistola", bpy.data.cameras.new("CameraDaPistola"))
        cena.collection.objects.link(cam)
    cam.data.type = "ORTHO" if orto else "PERSP"
    d = Vector(direcao).normalized()
    cam.location = Vector(alvo) + d * distancia
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    if orto:
        cam.data.ortho_scale = escala
    else:
        cam.data.lens = 50
    cam.data.clip_start = 0.01
    cena.camera = cam


def imagens():
    """O ícone (256 x 128, de lado, o cano para a direita, como os outros) e a foto (600 x 500, de 3/4), no estilo do
    itens.py: o Workbench com a textura, a cavidade, o contorno e o fundo transparente."""
    cena = bpy.context.scene
    mat = _material_assado()
    salvos = {}
    for o in _objetos():
        salvos[o.name] = list(o.data.materials)
        o.data.materials.clear()
        o.data.materials.append(mat)
    r = cena.render
    r.engine = "BLENDER_WORKBENCH"
    r.film_transparent = True
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGBA"
    s = cena.display.shading
    s.light = "STUDIO"
    s.color_type = "TEXTURE"
    s.show_cavity = True
    s.cavity_type = "BOTH"
    s.show_object_outline = True
    s.object_outline_color = (0.05, 0.05, 0.05)
    cena.view_settings.view_transform = "Standard"
    cantos = [o.matrix_world @ Vector(c) for o in _objetos() for c in o.bound_box]
    mn = Vector((min(c.x for c in cantos), min(c.y for c in cantos), min(c.z for c in cantos)))
    mx = Vector((max(c.x for c in cantos), max(c.y for c in cantos), max(c.z for c in cantos)))
    centro = (mn + mx) / 2
    tam = max(mx - mn)
    # de lado, pelo lado direito da arma (-X): o cano (-Y) para a direita da imagem, como os outros. O quadro é 2 x 1 e
    # a escala do ortográfico vale para a largura: o carregador comprido deixa a arma alta, então conta a altura também
    r.resolution_x, r.resolution_y = 256, 128
    _camera(cena, True, centro, (-1.0, 0.0, 0.05), 1.0, max(mx.y - mn.y, (mx.z - mn.z) * 2.0) * 1.06)
    r.filepath = os.path.join(ICONES, "Icone_Pistola.png")
    bpy.ops.render.render(write_still=True)
    r.resolution_x, r.resolution_y = 600, 500
    _camera(cena, False, centro, (-1.0, 0.45, 0.35), tam * 2.4, tam)
    r.filepath = os.path.join(ICONES, "Foto_Pistola.png")
    bpy.ops.render.render(write_still=True)
    for o in _objetos():
        o.data.materials.clear()
        for m in salvos[o.name]:
            o.data.materials.append(m)
    print("ícone e foto gravados")


def salvar():
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)


if __name__ == "__main__" and "--" in sys.argv:
    import traceback
    args = sys.argv[sys.argv.index("--") + 1:]
    try:
        etapa = args[0] if args else "tudo"
        if etapa in ("construir", "tudo"):
            for o in list(bpy.data.objects):
                bpy.data.objects.remove(o, do_unlink=True)
            construir()
            desembrulhar()
        if etapa in ("assar", "tudo"):
            assar()
        if etapa in ("exportar", "tudo"):
            exportar()
        if etapa in ("imagens", "tudo"):
            imagens()
        salvar()
        print("ETAPA OK:", etapa)
    except Exception:
        traceback.print_exc()
        print("ETAPA FALHOU")
        sys.exit(1)
