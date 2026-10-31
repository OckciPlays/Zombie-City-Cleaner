# A submetralhadora v3 (30/10/2026, a pedido: "vamos atualizar a smg agora", com a imagem de uma MP5K de coronha
# dobrável, empunhadura vertical e carregador curvo): sem modelo fonte (não veio um .blend dela), toda modelada aqui em
# blocos, no estilo das outras v3: as cores chapadas da paleta (o receptor preto, o polímero cinza do punho, da coronha
# e da empunhadura, o carregador preto fosco), as quinas chanfradas e lisas só até 37°.
#
# O tamanho e o lugar são os da SMG antiga do jogo (a do Armas.fbx e dos braços Jogador_FP_Submetralhadora.fbx): a boca
# do cano no mesmo ponto (Armas.cs: (0, 0,105, 0,291) -> aqui (0, -0,291, 0,105)) e o punho na mão direita. As medidas
# vêm da imagem de referência pela régua Y()/Z(): de um pixel dela (px, py) para o espaço da arma solta (o cano em -Y, o
# alto em +Z, a direita da arma em -X, a origem no cabo), com a boca em px 905 e o punho (px 525) na mão direita.
# As peças: o corpo ("gun"), a alavanca de manejo ("slide", na frente, do lado esquerdo, como na MP5) e o carregador
# ("mag"). A mão esquerda vai para a empunhadura vertical pelo maos_smg.py (os braços de primeira pessoa).
#
# Como rodar: blender -b --factory-startup --disable-autoexec --python Blender/Armas/rodar_v2.py -- v3_mp5 tudo

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import armas
import estilizadas
from v3_mosin import _tubo_aros

NOME = "Submetralhadora3"
FOTO = "submetralhadora_v3"
MODELO = "Submetralhadora"
FBX_ANTIGO = "Armas.fbx"
PECAS = ["corpo", "ferrolho", "carregador"]
ICONE = (384, 256)
ANGULO_LISO = 37
EIXO = 0.105              # a altura do cano
LARGURA = 0.022           # a meia largura do receptor

# a régua da imagem de referência (a MP5K de lado, 1080 x 505): 1 px = K m
K = 0.000737


def Y(px):
    return -0.291 + (905 - px) * K


def Z(py):
    return EIXO + (205 - py) * K


# o meio da empunhadura vertical (para onde vai a mão esquerda) e o punho (a mão direita)
EMPUNHADURA = Vector((0.0, Y(810), 0.039))


def _secao_y(m, pontos, y0, y1, cor, chanfro=0.0):
    """Uma seção (pontos (x, z)) puxada em Y de y0 a y1: o receptor de topo arredondado."""
    a = [m.bm.verts.new((x, y0, z)) for x, z in pontos]
    b = [m.bm.verts.new((x, y1, z)) for x, z in pontos]
    n = len(pontos)
    m.bm.faces.new(a)
    m.bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        m.bm.faces.new((a[i], a[j], b[j], b[i]))
    m._fim(a + b, cor, chanfro)


def _anel_y(m, centro, r_fora, r_dentro, comp, cor, lados=10):
    """Um anel (tubo oco) no eixo Y: o capuz da massa de mira."""
    cx, cy, cz = centro
    aneis = []
    for y, r in ((cy + comp / 2, r_dentro), (cy + comp / 2, r_fora), (cy - comp / 2, r_fora), (cy - comp / 2, r_dentro)):
        aneis.append([m.bm.verts.new((cx + r * math.sin(2 * math.pi * k / lados), y, cz - r * math.cos(2 * math.pi * k / lados)))
                      for k in range(lados)])
    faces = []
    for i in range(4):
        a, b = aneis[i], aneis[(i + 1) % 4]
        for k in range(lados):
            j = (k + 1) % lados
            faces.append(m.bm.faces.new((a[k], a[j], b[j], b[k])))
    idx = m.nomes.index(cor)
    for f in faces:
        f.material_index = idx


def receptor(m):
    """O receptor (o de cima, de aço preto): a seção de topo arredondado, as linhas dos lados, a janela de ejeção (à
    direita), a fenda da alavanca (à esquerda), as bases do trilho em cima, a capa da frente com a argola e o cano."""
    L = LARGURA
    secao = [(-L, Z(238)), (L, Z(238)), (L, 0.128), (0.016, 0.1405), (0.008, Z(150)), (-0.008, Z(150)), (-0.016, 0.1405),
             (-L, 0.128)]
    _secao_y(m, secao, Y(895), Y(425), "preto", chanfro=0.0025)
    for lado in (-1, 1):
        # a linha funda que corre pelo lado do receptor
        m.caixa((lado * (L - 0.0005), Y(720), Z(213)), (lado * (L + 0.0006), Y(515), Z(207)), "borracha", chanfro=0.0)
    m.caixa((-(L + 0.0006), Y(690), 0.105), (-(L - 0.0005), Y(635), 0.125), "borracha", chanfro=0.0)
    m.caixa((L - 0.0005, Y(880), 0.126), (L + 0.0006, Y(700), 0.132), "borracha", chanfro=0.0)
    for a, b in ((600, 640), (650, 700), (720, 760)):
        m.caixa((-0.009, Y(b), Z(150) - 0.001), (0.009, Y(a), Z(150) + 0.005), "preto", chanfro=0.0)
    m.caixa((-0.006, Y(900), Z(265)), (0.006, Y(878), Z(238) + 0.002), "preto", chanfro=0.0)
    _tubo_aros(m, "y", (0.0, -0.2865, EIXO), 0.0085, 0.0085, 0.010, "grafite", 10, 0.0012)
    m.tubo("y", (0.0, -0.2915, EIXO), 0.004, 0.004, 0.0008, "borracha", lados=8)


def miras(m):
    """A massa de mira com o capuz redondo (na frente) e a alça de tambor (atrás), em cima do receptor."""
    m.caixa((-0.012, Y(902), Z(150) - 0.002), (0.012, Y(862), 0.157), "preto", chanfro=0.0015)
    _anel_y(m, (0.0, Y(885), 0.166), 0.0115, 0.0085, 0.014, "preto")
    m.caixa((-0.0016, Y(888), 0.156), (0.0016, Y(882), 0.170), "preto", chanfro=0.0)
    m.caixa((-0.013, Y(532), Z(150) - 0.002), (0.013, Y(478), 0.152), "preto", chanfro=0.0)
    m.caixa((-0.012, Y(528), 0.152), (0.012, Y(484), Z(120)), "preto", chanfro=0.002)
    m.caixa((-0.0125, Y(510), 0.159), (0.0125, Y(502), 0.163), "borracha", chanfro=0.0)


def baixo(m):
    """A caixa do gatilho (preto fosco, a frente inclinada), o punho inclinado, o guarda-mato, o gatilho, o seletor (dos
    dois lados), o retém do carregador, os pinos e o poço do carregador."""
    m.perfil_x([(Y(440), Z(238) + 0.001), (Y(690), Z(238) + 0.001), (Y(690), Z(262)), (Y(662), Z(300)), (Y(470), Z(300)),
                (Y(440), Z(272))], -0.019, 0.019, "preto_fosco", chanfro=0.003)
    m.perfil_x([(-0.0380, Z(300) + 0.003), (0.0085, Z(300) + 0.003), (0.0620, -0.042), (0.0560, -0.047), (0.0080, -0.047),
                (-0.0040, -0.030), (-0.0200, 0.000)], -0.0175, 0.0175, "polimero", chanfro=0.0035)
    m.caixa((-0.004, Y(645), Z(345)), (0.004, -0.012, Z(345) + 0.005), "grafite", chanfro=0.0)
    m.caixa((-0.004, Y(645), Z(345)), (0.004, Y(636), Z(300) + 0.001), "grafite", chanfro=0.0)
    m.perfil_x([(Y(598), Z(300) + 0.001), (Y(590), Z(300) + 0.001), (Y(584), Z(338)), (Y(590), Z(338))], -0.0025, 0.0025,
               "grafite")
    for lado in (-1, 1):
        x0, x1 = sorted((lado * 0.019, lado * 0.0245))
        m.caixa((x0, Y(580), 0.059), (x1, Y(530), 0.067), "preto", chanfro=0.0)
    m.perfil_x([(Y(668), Z(262)), (Y(686), Z(262)), (Y(684), Z(290)), (Y(672), Z(290))], -0.006, 0.006, "preto")
    m.tubo("x", (0.0, Y(450), 0.070), 0.0032, 0.0032, 0.0405, "grafite", lados=8)
    m.tubo("x", (0.0, Y(675), 0.070), 0.0032, 0.0032, 0.0405, "grafite", lados=8)
    m.caixa((-0.017, Y(737), Z(262)), (0.017, Y(688), Z(238) + 0.001), "preto", chanfro=0.002)


def empunhadura(m):
    """A empunhadura vertical de polímero embaixo da frente, com a base e os quatro anéis de baixo."""
    m.caixa((-0.010, Y(838), Z(238) - 0.004), (0.010, Y(782), Z(238) + 0.001), "preto", chanfro=0.0)
    _tubo_aros(m, "z", (0.0, EMPUNHADURA.y, 0.040), 0.0165, 0.0165, 0.078, "polimero", 12, 0.0025)
    for z in (0.006, 0.0145, 0.023, 0.0315):
        m.tubo("z", (0.0, EMPUNHADURA.y, z), 0.0183, 0.0183, 0.0042, "polimero", lados=12)


def coronha(m):
    """A coronha dobrável (aberta): a dobradiça preta no fim do receptor, a barra de cima, a soleira e a escora de
    baixo, de polímero."""
    m.caixa((-0.023, Y(460), 0.058), (0.023, Y(420), 0.125), "preto", chanfro=0.003)
    m.caixa((-0.011, Y(420), Z(222)), (0.011, Y(100), Z(195)), "polimero", chanfro=0.0025)
    m.perfil_x([(Y(140), Z(195)), (Y(80), Z(195)), (Y(80), Z(368)), (Y(128), Z(368))], -0.015, 0.015, "polimero",
               chanfro=0.003)
    m.perfil_x([(Y(134), Z(368)), (Y(132), Z(338)), (Y(300), Z(222)), (Y(350), Z(222))], -0.0095, 0.0095,
               "polimero", chanfro=0.002)


def carregador(m):
    """O carregador curvo (preto fosco) e a base dele."""
    m.perfil_x([(-0.136, 0.072), (-0.146, 0.0), (-0.159, -0.055), (-0.170, -0.088), (-0.204, -0.088), (-0.195, -0.055),
                (-0.182, 0.0), (-0.168, 0.072)], -0.0115, 0.0115, "preto_fosco", chanfro=0.0015)
    m.perfil_x([(-0.168, -0.087), (-0.207, -0.087), (-0.208, -0.095), (-0.167, -0.095)], -0.013, 0.013, "preto_fosco",
               chanfro=0.001)
    for lado in (-1, 1):
        x = lado * 0.0115
        m.perfil_x([(-0.149, 0.060), (-0.156, 0.0), (-0.168, -0.050), (-0.176, -0.080), (-0.180, -0.080), (-0.172, -0.050),
                    (-0.160, 0.0), (-0.153, 0.060)], x - 0.0008, x + 0.0008, "preto", chanfro=0.0)


def alavanca(m):
    """A alavanca de manejo da MP5: na frente, do lado esquerdo (+X), saindo da fenda."""
    m.caixa((LARGURA - 0.001, Y(872), 0.1265), (0.034, Y(862), 0.1325), "preto", chanfro=0.0)
    m.caixa((0.032, Y(876), 0.1245), (0.040, Y(858), 0.1350), "preto", chanfro=0.0015)


def construir(cfg):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    mats = estilizadas.materiais()
    nomes = estilizadas.NOMES
    por_peca = {p: bmesh.new() for p in cfg.PECAS}
    feitos = {"corpo": (receptor, miras, baixo, empunhadura, coronha), "ferrolho": (alavanca,), "carregador": (carregador,)}
    for peca, funcoes in feitos.items():
        m = armas.Extras(por_peca[peca], nomes)
        for f in funcoes:
            f(m)

    col = armas._colecao(cfg.NOME)
    objs = []
    for peca, bm in por_peca.items():
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        n = armas.tirar_escondidas(bm)
        bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1.0), verts=bm.verts, edges=bm.edges, delimit={"MATERIAL"})
        bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.normal_update()
        for e in bm.edges:
            e.smooth = len(e.link_faces) == 2 and \
                e.link_faces[0].normal.angle(e.link_faces[1].normal, 0.0) <= math.radians(cfg.ANGULO_LISO)
        nome = f"{cfg.NOME}_{peca}"
        me = bpy.data.meshes.new(nome)
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = True
        for mat in mats:
            me.materials.append(mat)
        me.uv_layers.new(name="UVMap")
        estilizadas.uv_da_cor(me)
        o = bpy.data.objects.new(nome, me)
        col.objects.link(o)
        objs.append(o)
        print(f"  {nome}: {len(me.polygons)} triângulos ({n} faces escondidas tiradas)")
    print("  total:", sum(len(o.data.polygons) for o in objs), "triângulos")
    return objs
