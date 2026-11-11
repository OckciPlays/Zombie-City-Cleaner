# Os ícones das habilidades (10/11/2026, a pedido: "uma imagem em cada nó", como no Dungeons & Developers): a aba
# HABILIDADES (tecla K) tem 74 nós; cada um ganha um PNG de 256 x 256 com um objeto (ou uma cena pequena) em 3D
# low-poly, nas cores da Paleta.png, visto de 3/4 pela mesma câmera, com a luz quente na frente e um contorno de luz
# na cor da área (Combate laranja, Resistência verde, Sobrevivência roxo). O fundo é escuro, neutro, com vinheta.
# A moldura e o brilho de "aprendida" são da tela do jogo, não do ícone.
#
# Como rodar (Blender 5.x, em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/icones_habilidades.py -- --saida <pasta> [g1 a2 ...]
#   (sem ids, faz os 74 em ~1 min, no EEVEE; os PNG saem como Hab_<id>.png, opacos, com o fundo já pintado)
# As folhas de conferência (uma por área, na posição da árvore, com o nome embaixo; e a prova em 64 x 64), com o
# Python do sistema e o Pillow:
#   python Blender/icones_habilidades.py folhas --saida <pasta>
#
# Tudo num lugar: a lista NOS (o id, o nome do enum Habilidade, a área, o ramo, a coluna e a fileira da árvore, o nome e
# a função que monta a cena) fica no fim do arquivo. Para refazer um ícone, mude a função dele e rode só o id.
# Os FBX e a Paleta.png do projeto só são lidos.

import math
import os
import sys

try:
    import bpy
    import bmesh
    from mathutils import Matrix, Vector, Euler
    TEM_BLENDER = True
except ImportError:
    TEM_BLENDER = False

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(AQUI)
ARTE = os.path.join(REPO, "Assets", "_Game", "Art")
PALETA = os.path.join(ARTE, "Paleta.png")
JOG = os.path.join("Characters", "Jogador")

TAM = 256            # o lado do ícone
CAM_AZ, CAM_EL = 30.0, 20.0   # a câmera: 30° à direita da frente (-Y), 20° acima
OCUPA = 0.80         # quanto do quadrado o desenho ocupa (o resto é respiro para a moldura)

# a cor de cada área (o contorno de luz e o brilho de trás)
AREAS = {
    "Combate": (1.0, 0.50, 0.12),
    "Resistencia": (0.30, 0.90, 0.35),
    "Sobrevivencia": (0.66, 0.42, 1.0),
}

# (linha da imagem, de cima para baixo; coluna) de cada cor na Paleta.png (os nomes do loot.py e do equipamentos.py)
CORES = {
    "vermelho": (11, 6), "amarelo": (11, 7), "laranja_vivo": (11, 8), "azul": (11, 9), "verde": (11, 10),
    "roxo": (11, 11), "azul_claro": (11, 12), "rosa": (11, 13), "branco": (11, 14), "prata": (11, 15),
    "vermelho_escuro": (0, 0), "verde_escuro": (0, 1), "chocolate": (0, 2), "dourado": (0, 3), "azul_escuro": (0, 4),
    "laranja_escuro": (0, 5),
    "lata": (8, 8), "lata_clara": (4, 6), "aco": (8, 2), "aco_escuro": (8, 1), "ferro": (8, 0),
    "madeira": (12, 0), "madeira_clara": (1, 4), "madeira_escura": (3, 7), "casca": (7, 4), "cerne": (1, 2),
    "corda": (1, 6), "cobre": (2, 7), "creme": (3, 9), "papelao": (6, 11), "oliva": (2, 8), "oliva_escuro": (7, 9),
    "vinho": (6, 4), "garrafa_verde": (2, 10), "preto": (3, 2), "pedra": (8, 2), "carvao": (8, 5),
    "ferrugem": (8, 3), "minhoca": (13, 0), "cinza_claro": (9, 3), "grafite": (11, 1), "azul_aco": (9, 2),
    "osso": (9, 10), "areia": (1, 5), "pele": (13, 9), "couro": (3, 7), "couro_escuro": (8, 4), "cacto": (7, 12),
    "verde_musgo": (2, 2), "vidro": (4, 9), "tijolo": (2, 3), "rosa_velho": (5, 4), "zumbi": (5, 3),
    "zumbi_escuro": (2, 1), "cinza": (8, 1), "noite": (8, 15), "laranja": (6, 9), "vinho_escuro": (6, 15),
    "palha": (1, 6), "branco_sujo": (6, 1), "ambar": (2, 7), "pedra_escura": (10, 3),
}


# ----------------------------------------------------------------------------------------------------------------------
# a paleta e os materiais

_PAL = None


def _srgb_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def rgb(cor):
    """A cor (linear) de uma célula da paleta; aceita também uma tupla pronta (linear)."""
    global _PAL
    if not isinstance(cor, str):
        return tuple(cor[:3])
    if _PAL is None:
        img = bpy.data.images.load(PALETA, check_existing=True)
        w, h = img.size
        px = list(img.pixels)
        _PAL = (w, h, px)
    w, h, px = _PAL
    linha, coluna = CORES[cor]
    x = int((coluna + 0.5) * w / 16)
    y = h - 1 - int((linha + 0.5) * h / 16)
    i = (y * w + x) * 4
    return tuple(_srgb_lin(px[i + k]) for k in range(3))


_MATS = {}


def mat(cor, metal=0.0, rugo=0.6, brilho=0.0, alfa=1.0, espec=0.5):
    """Um material liso de uma cor; brilho > 0 acende (o fogo, as faíscas, as telas); espec 0 tira o reflexo."""
    chave = (str(cor), metal, rugo, brilho, alfa, espec)
    if chave in _MATS:
        return _MATS[chave]
    m = bpy.data.materials.new("Cor_" + str(cor))
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    c = rgb(cor)
    b.inputs["Base Color"].default_value = (*c, 1.0)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rugo
    b.inputs["Specular IOR Level"].default_value = espec
    if brilho > 0:
        b.inputs["Emission Color"].default_value = (*c, 1.0)
        b.inputs["Emission Strength"].default_value = brilho
    if alfa < 1.0:
        b.inputs["Alpha"].default_value = alfa
        try:
            m.surface_render_method = "BLENDED"
        except Exception:
            m.blend_method = "BLEND"
    _MATS[chave] = m
    return m


# ----------------------------------------------------------------------------------------------------------------------
# a pilha de transformações: tudo o que se cria dentro de "with grupo(...)" sai movido, girado e escalado junto

PILHA = []


def _rot(giro):
    return Euler(tuple(math.radians(g) for g in giro), "XYZ").to_matrix().to_4x4()


def _esc(e):
    if isinstance(e, (int, float)):
        e = (e, e, e)
    return Matrix.Diagonal((e[0], e[1], e[2], 1.0))


class grupo:
    def __init__(self, pos=(0, 0, 0), giro=(0, 0, 0), escala=1.0):
        self.m = Matrix.Translation(Vector(pos)) @ _rot(giro) @ _esc(escala)

    def __enter__(self):
        PILHA.append(PILHA[-1] @ self.m)

    def __exit__(self, *a):
        PILHA.pop()


def _objeto(bm, material, nome="peca"):
    """Fecha o bmesh num objeto da cena do ícone, já na transformação da pilha."""
    bmesh.ops.transform(bm, matrix=PILHA[-1], verts=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(nome)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(material)
    o = bpy.data.objects.new(nome, me)
    COLECAO[0].objects.link(o)
    return o


COLECAO = [None]


# ----------------------------------------------------------------------------------------------------------------------
# as peças feitas à mão (low-poly, faces chapadas)

def caixa(tam, centro=(0, 0, 0), giro=(0, 0, 0), m=None, chanfro=0.0):
    bm = bmesh.new()
    vs = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(centro)) @ _rot(giro) @ _esc(tam), verts=vs)
    if chanfro > 0:
        bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=chanfro, segments=1, affect="EDGES", profile=0.5)
    return _objeto(bm, m, "caixa")


def cilindro(r, h, centro=(0, 0, 0), giro=(0, 0, 0), m=None, lados=8, r2=None, chanfro=0.0):
    """Um cilindro (ou cone, com r2) em pé no eixo Z local, girado e posto no centro."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=lados, radius1=r,
                          radius2=r if r2 is None else r2, depth=h)
    if chanfro > 0:
        bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=chanfro, segments=1, affect="EDGES", profile=0.5)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(centro)) @ _rot(giro), verts=bm.verts)
    return _objeto(bm, m, "cilindro")


def esfera(r, centro=(0, 0, 0), m=None, sub=1, escala=(1, 1, 1), giro=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(centro)) @ _rot(giro) @ _esc(escala), verts=bm.verts)
    return _objeto(bm, m, "esfera")


def toro(R, r, centro=(0, 0, 0), giro=(0, 0, 0), m=None, seg=16, lados=6, arco=360.0, ini=0.0):
    """Um anel no plano XY local (o furo em Z); arco < 360 faz um pedaço (uma alça, um arco)."""
    bm = bmesh.new()
    fechado = arco >= 359.9
    n = seg if fechado else seg + 1
    aneis = []
    for i in range(n):
        a = math.radians(ini + arco * i / seg)
        c, s = math.cos(a), math.sin(a)
        anel = []
        for j in range(lados):
            b = 2 * math.pi * j / lados
            rr = R + r * math.cos(b)
            anel.append(bm.verts.new((rr * c, rr * s, r * math.sin(b))))
        aneis.append(anel)
    for i in range(n if fechado else n - 1):
        a1, a2 = aneis[i], aneis[(i + 1) % n]
        for j in range(lados):
            bm.faces.new((a1[j], a2[j], a2[(j + 1) % lados], a1[(j + 1) % lados]))
    if not fechado:
        bm.faces.new(list(reversed(aneis[0])))
        bm.faces.new(aneis[-1])
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(centro)) @ _rot(giro), verts=bm.verts)
    return _objeto(bm, m, "toro")


def tubo(pontos, r, m=None, lados=6, r_fim=None, tampa=True):
    """Um tubo pelos pontos (o fio, a mola, o anzol); r_fim afina até a ponta."""
    pts = [Vector(p) for p in pontos]
    bm = bmesh.new()
    aneis = []
    t0 = (pts[1] - pts[0]).normalized()
    ref = Vector((0, 0, 1)) if abs(t0.z) < 0.9 else Vector((1, 0, 0))
    n = t0.cross(ref).normalized()
    for i, p in enumerate(pts):
        if i == 0:
            t = pts[1] - pts[0]
        elif i == len(pts) - 1:
            t = pts[-1] - pts[-2]
        else:
            t = pts[i + 1] - pts[i - 1]
        t.normalize()
        n = (n - t * n.dot(t)).normalized()
        b = t.cross(n)
        rr = r if r_fim is None else r + (r_fim - r) * i / (len(pts) - 1)
        anel = []
        for j in range(lados):
            a = 2 * math.pi * j / lados
            anel.append(bm.verts.new(p + (n * math.cos(a) + b * math.sin(a)) * rr))
        aneis.append(anel)
    for i in range(len(aneis) - 1):
        a1, a2 = aneis[i], aneis[i + 1]
        for j in range(lados):
            bm.faces.new((a1[j], a1[(j + 1) % lados], a2[(j + 1) % lados], a2[j]))
    if tampa:
        bm.faces.new(list(reversed(aneis[0])))
        bm.faces.new(aneis[-1])
    return _objeto(bm, m, "tubo")


def perfil(pts, esp, m=None, centro=(0, 0, 0), giro=(0, 0, 0), escala=1.0, chanfro=0.0):
    """Um desenho 2D (x, z) puxado na grossura esp (em Y): o coração, a estrela, o escudo... de frente para a câmera."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, -esp / 2, z)) for x, z in pts]
    f = bm.faces.new(vs)
    r = bmesh.ops.extrude_face_region(bm, geom=[f])
    novos = [e for e in r["geom"] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=(0, esp, 0), verts=novos)
    if chanfro > 0:
        bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=chanfro, segments=1, affect="EDGES",
                        profile=0.5, clamp_overlap=True)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(centro)) @ _rot(giro) @ _esc(escala), verts=bm.verts)
    return _objeto(bm, m, "perfil")


def revolucao(perfil_rz, m=None, centro=(0, 0, 0), giro=(0, 0, 0), lados=10, desvio=0.0):
    """Um sólido de revolução em volta de Z local: perfil_rz é a lista (raio, z) de baixo para cima; desvio entorta
    a ponta para o lado (+X), como a chama."""
    bm = bmesh.new()
    z0, z1 = perfil_rz[0][1], perfil_rz[-1][1]
    aneis = []
    for r, z in perfil_rz:
        dx = desvio * ((z - z0) / (z1 - z0)) ** 2 if z1 != z0 else 0.0
        if r < 1e-5:
            aneis.append([bm.verts.new((dx, 0, z))])
        else:
            aneis.append([bm.verts.new((dx + r * math.cos(2 * math.pi * j / lados), r * math.sin(2 * math.pi * j / lados), z))
                          for j in range(lados)])
    for a1, a2 in zip(aneis, aneis[1:]):
        if len(a1) == 1 and len(a2) == 1:
            continue
        for j in range(lados):
            if len(a1) == 1:
                bm.faces.new((a1[0], a2[j], a2[(j + 1) % lados]))
            elif len(a2) == 1:
                bm.faces.new((a1[j], a1[(j + 1) % lados], a2[0]))
            else:
                bm.faces.new((a1[j], a1[(j + 1) % lados], a2[(j + 1) % lados], a2[j]))
    if len(aneis[0]) > 1:
        bm.faces.new(list(reversed(aneis[0])))
    if len(aneis[-1]) > 1:
        bm.faces.new(aneis[-1])
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(centro)) @ _rot(giro), verts=bm.verts)
    return _objeto(bm, m, "revolucao")


# os desenhos 2D

def circulo(r, n=16, cx=0.0, cz=0.0, ini=0.0, rz=None):
    rz = r if rz is None else rz
    return [(cx + r * math.cos(math.radians(ini) + 2 * math.pi * i / n), cz + rz * math.sin(math.radians(ini) + 2 * math.pi * i / n))
            for i in range(n)]


def estrela(pontas, r_fora, r_dentro, ini=90.0):
    pts = []
    for i in range(pontas * 2):
        r = r_fora if i % 2 == 0 else r_dentro
        a = math.radians(ini) + math.pi * i / pontas
        pts.append((r * math.cos(a), r * math.sin(a)))
    return pts


def coracao(tam=1.0, n=28):
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x = 16 * math.sin(t) ** 3
        z = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((x * tam / 32, (z + 2) * tam / 32))
    pts.reverse()
    return pts


def arco_faixa(r_fora, r_dentro, de, ate, n=12, cx=0.0, cz=0.0):
    """Uma faixa em arco (a lua, a pálpebra, a seta curva)."""
    fora = [(cx + r_fora * math.cos(math.radians(de + (ate - de) * i / n)), cz + r_fora * math.sin(math.radians(de + (ate - de) * i / n)))
            for i in range(n + 1)]
    dentro = [(cx + r_dentro * math.cos(math.radians(de + (ate - de) * i / n)), cz + r_dentro * math.sin(math.radians(de + (ate - de) * i / n)))
              for i in range(n + 1)]
    return fora + list(reversed(dentro))


def engrenagem(dentes, r_fora, r_dentro):
    pts = []
    for i in range(dentes):
        a0 = 2 * math.pi * i / dentes
        d = 2 * math.pi / dentes
        for f, r in ((0.0, r_dentro), (0.18, r_fora), (0.5, r_fora), (0.68, r_dentro)):
            pts.append((r * math.cos(a0 + f * d), r * math.sin(a0 + f * d)))
    return pts


# ----------------------------------------------------------------------------------------------------------------------
# os modelos do jogo (os FBX só são lidos: cada arquivo entra uma vez numa coleção escondida e as peças são copiadas)

_BIB = {}


def _biblioteca():
    c = bpy.data.collections.get("Biblioteca")
    if c is None:
        c = bpy.data.collections.new("Biblioteca")
        bpy.context.scene.collection.children.link(c)
        bpy.context.view_layer.layer_collection.children["Biblioteca"].exclude = True
    return c


def _importar(arquivo):
    if arquivo in _BIB:
        return _BIB[arquivo]
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ARTE, arquivo))
    novos = [o for o in bpy.data.objects if o not in antes]
    bib = _biblioteca()
    pal = bpy.data.images.load(PALETA, check_existing=True)
    pal.alpha_mode = "NONE"
    for o in novos:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        bib.objects.link(o)
        if o.type == "MESH":
            for mt in o.data.materials:
                if not mt or not mt.use_nodes:
                    continue
                for n in mt.node_tree.nodes:
                    if n.type == "TEX_IMAGE":
                        if n.image is None or not n.image.has_data and not os.path.exists(bpy.path.abspath(n.image.filepath)):
                            n.image = pal
                        n.interpolation = "Closest"
                        if n.image == pal:
                            n.image.alpha_mode = "NONE"
                b = next((n for n in mt.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
                if b is not None:
                    if not any(n.type == "TEX_IMAGE" for n in mt.node_tree.nodes):
                        # sem textura no FBX (as armas brancas): a paleta pelos UV, como na Unity
                        t = mt.node_tree.nodes.new("ShaderNodeTexImage")
                        t.image = pal
                        t.interpolation = "Closest"
                        mt.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
                    b.inputs["Roughness"].default_value = 0.6
                    b.inputs["Specular IOR Level"].default_value = 0.4
                    # o alfa da paleta é o brilho da cidade, não transparência
                    for l in list(mt.node_tree.links):
                        if l.to_socket == b.inputs["Alpha"]:
                            mt.node_tree.links.remove(l)
    _BIB[arquivo] = {o.name.split(".")[0]: o for o in novos if o.type == "MESH"}
    return _BIB[arquivo]


def modelo(arquivo, nomes, tam=1.0, pos=(0, 0, 0), giro=(0, 0, 0), base=False):
    """Copia peças de um FBX do jogo para a cena: juntas, com o centro da caixa em pos, a maior medida = tam e girado
    (graus, X Y Z, antes de ir para o lugar). base=True apoia o fundo em pos (em vez do centro)."""
    if isinstance(nomes, str):
        nomes = [nomes]
    pecas = _importar(arquivo)
    objs = []
    for nome in nomes:
        o = pecas[nome]
        me = o.data.copy()
        me.transform(o.matrix_world)
        n = bpy.data.objects.new(nome, me)
        COLECAO[0].objects.link(n)
        objs.append(n)
    vs = [v.co for o in objs for v in o.data.vertices]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    meio = (lo + hi) / 2
    s = tam / max(hi - lo)
    m = PILHA[-1] @ Matrix.Translation(Vector(pos)) @ _rot(giro) @ _esc(s) @ Matrix.Translation(-meio)
    if base:
        # o fundo depois do giro: mede de novo
        pts = [_rot(giro) @ (v - meio) * s for v in vs]
        dz = -min(p.z for p in pts)
        m = PILHA[-1] @ Matrix.Translation(Vector(pos) + Vector((0, 0, dz))) @ _rot(giro) @ _esc(s) @ Matrix.Translation(-meio)
    for o in objs:
        o.data.transform(m)
        o.data.update()
    return objs


# ----------------------------------------------------------------------------------------------------------------------
# a cena: a câmera, as luzes e o render

def _cena():
    sc = bpy.context.scene
    # a cena de fábrica vem com o cubo, a luz e a câmera
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    try:
        sc.render.engine = "BLENDER_EEVEE"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.render.resolution_x = sc.render.resolution_y = TAM
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    try:
        sc.eevee.taa_render_samples = 48
    except Exception:
        pass
    for k in ("use_shadows", "use_raytracing"):
        try:
            setattr(sc.eevee, k, True)
        except Exception:
            pass
    w = bpy.data.worlds.new("Fundo")
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.05, 0.05, 0.055, 1.0)
    bg.inputs["Strength"].default_value = 1.0
    sc.world = w

    cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    cam.data.type = "ORTHO"
    cam.data.clip_start = 0.01
    cam.data.clip_end = 100
    sc.collection.objects.link(cam)
    sc.camera = cam
    return sc, cam


def _luzes(cor_area):
    # a principal, quente, do alto à esquerda da câmera
    def sol(nome, cor, forca, giro, angulo=8.0):
        l = bpy.data.lights.new(nome, "SUN")
        l.color = cor
        l.energy = forca
        l.angle = math.radians(angulo)
        o = bpy.data.objects.new(nome, l)
        o.rotation_euler = Euler(tuple(math.radians(g) for g in giro), "XYZ")
        COLECAO[0].objects.link(o)
        return o
    sol("Principal", (1.0, 0.86, 0.68), 4.2, (50, 0, -35))
    sol("Preenche", (0.62, 0.72, 1.0), 0.9, (75, 0, 60), 20)
    # o contorno de luz, de trás, na cor da área
    sol("Contorno", cor_area, 7.0, (-62, 0, 25), 4)
    sol("Contorno2", cor_area, 3.0, (-70, 0, -40), 4)


def _apontar(cam, alvo=None):
    az, el = math.radians(CAM_AZ), math.radians(CAM_EL)
    d = Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)))
    cam.location = (alvo or Vector((0, 0, 0))) + d * 20
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()


def _enquadrar(cam, ocupa):
    """Põe todo o desenho no quadrado: o centro da caixa vista pela câmera e o tamanho (orto)."""
    bpy.context.view_layer.update()
    inv = cam.matrix_world.inverted()
    xs, ys = [], []
    for o in COLECAO[0].objects:
        if o.type != "MESH":
            continue
        for v in o.data.vertices:
            p = inv @ (o.matrix_world @ v.co)
            xs.append(p.x)
            ys.append(p.y)
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    lado = max(max(xs) - min(xs), max(ys) - min(ys))
    cam.location = cam.matrix_world @ Vector((cx, cy, 0))
    cam.data.ortho_scale = lado / ocupa


# ----------------------------------------------------------------------------------------------------------------------
# o fundo e os retoques (numpy, no próprio Blender)

def _borrar(a, raio):
    """Três caixas seguidas (quase um gaussiano)."""
    import numpy as np
    r = max(1, int(raio))
    for _ in range(3):
        for eixo in (0, 1):
            p = np.pad(a, [(r + 1, r) if k == eixo else (0, 0) for k in range(a.ndim)], mode="edge")
            c = np.cumsum(p, axis=eixo)
            if eixo == 0:
                a = (c[2 * r + 1:] - c[:-2 * r - 1]) / (2 * r + 1)
            else:
                a = (c[:, 2 * r + 1:] - c[:, :-2 * r - 1]) / (2 * r + 1)
    return a


def _finalizar(cru, saida, cor_area):
    import numpy as np
    img = bpy.data.images.load(cru)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    rgb_o, a = px[..., :3], px[..., 3:4]
    ys, xs = np.mgrid[0:h, 0:w]
    # o fundo: escuro e neutro, um pouco mais claro no meio (a vinheta)
    r = np.sqrt(((xs - w / 2 + 0.5) / (w / 2)) ** 2 + ((ys - h * 0.52) / (h / 2)) ** 2)[..., None]
    t = np.clip(r / 1.25, 0, 1) ** 1.3
    meio, borda = np.array([0.155, 0.158, 0.165]), np.array([0.040, 0.041, 0.045])
    fundo = meio * (1 - t) + borda * t
    cor = np.array(cor_area)
    fundo = fundo + cor * 0.035 * np.clip(1 - r, 0, 1) ** 2
    # o brilho da área atrás do desenho (bem fraco) e a sombra embaixo
    halo = _borrar(a[..., 0], 9)[..., None]
    fundo = fundo + cor * 0.16 * halo
    sombra = np.roll(_borrar(a[..., 0], 5), -5, axis=0)[..., None]
    fundo = fundo * (1 - 0.45 * sombra)
    out = rgb_o * a + fundo * (1 - a)
    # o fogo e as faíscas espalham um pouco de luz
    lum = (rgb_o * a).max(axis=2)
    quente = np.clip(lum - 0.92, 0, 1)[..., None] * (rgb_o * a)
    if quente.max() > 0:
        out = out + _borrar(quente, 7) * 2.2
    out = np.clip(out, 0, 1)
    final = np.concatenate([out, np.ones((h, w, 1), dtype=np.float32)], axis=2)
    im = bpy.data.images.new("Hab", w, h, alpha=False)
    im.pixels[:] = final.ravel()
    im.filepath_raw = saida
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def fazer(no, pasta):
    ident, area, funcao = no[0], no[2], no[7]
    sc = bpy.context.scene
    col = bpy.data.collections.new("Icone_" + ident)
    sc.collection.children.link(col)
    COLECAO[0] = col
    PILHA[:] = [Matrix.Identity(4)]
    opcoes = funcao() or {}
    cor_area = AREAS[area]
    _luzes(cor_area)
    cam = sc.camera
    _apontar(cam)
    _enquadrar(cam, opcoes.get("ocupa", OCUPA))
    cru = os.path.join(pasta, "_cru_%s.png" % ident)
    sc.render.filepath = cru
    bpy.ops.render.render(write_still=True)
    _finalizar(cru, os.path.join(pasta, "Hab_%s.png" % ident), cor_area)
    os.remove(cru)
    # limpa a cena do ícone (as peças, as luzes e as malhas)
    for o in list(col.objects):
        dados = o.data
        bpy.data.objects.remove(o)
        if dados is not None and dados.users == 0:
            if isinstance(dados, bpy.types.Mesh):
                bpy.data.meshes.remove(dados)
            elif isinstance(dados, bpy.types.Light):
                bpy.data.lights.remove(dados)
    bpy.data.collections.remove(col)


# ----------------------------------------------------------------------------------------------------------------------
# peças que se repetem em vários ícones

def ponta(objs, direcao):
    """O ponto das peças mais longe na direção (a boca do cano, a cabeça da tocha)."""
    d = Vector(direcao).normalized()
    vs = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    mx = max(v.dot(d) for v in vs)
    lado = max(v.dot(d) for v in vs) - min(v.dot(d) for v in vs)
    perto = [v for v in vs if v.dot(d) > mx - lado * 0.03]
    return sum(perto, Vector()) / len(perto)


def medidas(objs):
    vs = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return lo, hi


def fita(pontos, largura, m, lado=(0, 1, 0)):
    """Uma tira chata pelos pontos (a atadura desenrolando, o pavio de pano), larga na direção do lado."""
    bm = bmesh.new()
    l = Vector(lado).normalized() * largura / 2
    pares = [(bm.verts.new(Vector(p) - l), bm.verts.new(Vector(p) + l)) for p in pontos]
    for (a1, b1), (a2, b2) in zip(pares, pares[1:]):
        bm.faces.new((a1, b1, b2, a2))
    return _objeto(bm, m, "fita")


def chama(pos, alt=1.0, larg=0.42, inclina=0.0, fase=0.0):
    """Uma chama low-poly: o corpo laranja com a ponta torta, duas línguas menores dos lados e o miolo amarelo."""
    def forma(a, l):
        return [(0.0, 0.0), (l * 0.75, a * 0.10), (l, a * 0.28), (l * 0.78, a * 0.52), (l * 0.38, a * 0.78), (0.0, a)]
    with grupo(pos=pos, giro=(0, inclina, fase)):
        revolucao(forma(alt, larg), mat("laranja_vivo", brilho=1.25), lados=6, desvio=larg * 0.35)
        for s in (-1, 1):
            with grupo(pos=(s * larg * 0.62, 0.02, 0.0), giro=(0, s * 24, 0)):
                revolucao(forma(alt * 0.55, larg * 0.55), mat("laranja_escuro", brilho=1.0), lados=6,
                          desvio=s * larg * 0.25)
        revolucao(forma(alt * 0.6, larg * 0.55), mat("amarelo", brilho=2.0), centro=(0.03 * larg, -larg * 0.5, 0.0),
                  lados=6, desvio=larg * 0.2)


def faisca(pos, tam=0.2, cor="branco", giro=(0, 0, 0), pontas=4, forca=3.0):
    perfil(estrela(pontas, tam, tam * 0.28), tam * 0.12, mat(cor, brilho=forca), centro=pos, giro=giro)


def bala(pos=(0, 0, 0), giro=(0, 0, 0), escala=1.0):
    """A bala inteira (o estojo de latão e a ponta de cobre), em pé no Z, a base em pos."""
    with grupo(pos=pos, giro=giro, escala=escala):
        latao = mat("dourado", metal=0.55, rugo=0.35)
        cilindro(0.095, 0.035, (0, 0, 0.0175), m=latao, lados=10)
        cilindro(0.088, 0.30, (0, 0, 0.18), m=latao, lados=10)
        revolucao([(0.083, 0.33), (0.08, 0.40), (0.062, 0.48), (0.035, 0.545), (0.0, 0.575)],
                  mat("cobre", metal=0.5, rugo=0.35), lados=10)


def mao(m_pele=None, m_manga=None):
    """A mão aberta de bloco (como as do personagem), a palma para a frente, os dedos para cima; o pulso em z=0."""
    pele = m_pele or mat("pele")
    caixa((0.50, 0.16, 0.52), (0, 0, 0.30), m=pele, chanfro=0.02)
    for i, (x, alt) in enumerate(((-0.18, 0.36), (-0.06, 0.40), (0.06, 0.38), (0.18, 0.31))):
        caixa((0.105, 0.14, alt), (x, 0, 0.56 + alt / 2), m=pele, chanfro=0.015)
    caixa((0.12, 0.14, 0.30), (0.33, 0, 0.36), giro=(0, -38, 0), m=pele, chanfro=0.015)
    caixa((0.56, 0.22, 0.20), (0, 0, -0.04), m=m_manga or mat("vermelho_escuro"), chanfro=0.02)


def coracao_3d(pos=(0, 0, 0), tam=1.0, cor="vermelho", giro=(0, 0, 0), esp=0.28):
    perfil(coracao(tam), esp * tam, mat(cor, rugo=0.45), centro=pos, giro=giro, chanfro=0.03 * tam)


def escudo_pts(l=0.5, h=0.62):
    pts = [(-l, h), (0, h * 1.12), (l, h)]
    for i in range(1, 9):
        t = i / 9
        pts.append((l * (1 - t ** 1.6), h - (h * 2.0) * t))
    pts.append((0, -h))
    for i in range(8, 0, -1):
        t = i / 9
        pts.append((-l * (1 - t ** 1.6), h - (h * 2.0) * t))
    return pts


def escudo(pos=(0, 0, 0), tam=1.0, cor_borda="prata", cor_meio="verde", giro=(0, 0, 0)):
    with grupo(pos=pos, giro=giro, escala=tam):
        perfil(escudo_pts(0.5, 0.62), 0.14, mat(cor_borda, metal=0.6, rugo=0.35), chanfro=0.02)
        perfil(escudo_pts(0.40, 0.50), 0.06, mat(cor_meio, rugo=0.5), centro=(0, -0.085, 0.0))


def mola(pos=(0, 0, 0), voltas=6, raio=0.28, alt=1.1, fio=0.045, giro=(0, 0, 0), m=None):
    pts = []
    n = int(voltas * 14)
    for i in range(n + 1):
        t = i / n
        a = 2 * math.pi * voltas * t
        pts.append((raio * math.cos(a), raio * math.sin(a), -alt / 2 + alt * t))
    with grupo(pos=pos, giro=giro):
        tubo(pts, fio, m or mat("prata", metal=0.8, rugo=0.25), lados=6)


def pe(pos=(0, 0, 0), esquerdo=False, m=None, giro=(0, 0, 0), escala=1.0):
    """A pegada de um pé descalço (a planta e os cinco dedos), no plano XZ."""
    s = -1 if esquerdo else 1
    planta = [(0, 0), (0.12, 0.02), (0.17, 0.10), (0.18, 0.25), (0.16, 0.40), (0.19, 0.55), (0.21, 0.70),
              (0.18, 0.80), (0.10, 0.86), (0, 0.87), (-0.12, 0.86), (-0.20, 0.80), (-0.21, 0.68), (-0.15, 0.55),
              (-0.08, 0.42), (-0.08, 0.28), (-0.13, 0.15), (-0.11, 0.04)]
    if esquerdo:
        planta = [(-x, z) for x, z in reversed(planta)]
    with grupo(pos=pos, giro=giro, escala=escala):
        perfil(planta, 0.06, m, chanfro=0.01)
        for x, z, r in ((-0.13, 0.99, 0.075), (-0.01, 1.01, 0.055), (0.075, 0.985, 0.048), (0.14, 0.945, 0.042),
                        (0.195, 0.885, 0.036)):
            perfil(circulo(r, 10, s * x, z), 0.06, m)


def virus(pos=(0, 0, 0), r=0.3, cor="verde", cor_ponta="verde_escuro"):
    esfera(r, pos, mat(cor, rugo=0.5), sub=1)
    m = mat(cor_ponta, rugo=0.5)
    for i in range(12):
        # os espinhos nas direções do icosaedro
        phi = math.acos(1 - 2 * (i + 0.5) / 12)
        th = math.pi * (1 + 5 ** 0.5) * i
        d = Vector((math.sin(phi) * math.cos(th), math.sin(phi) * math.sin(th), math.cos(phi)))
        p = Vector(pos) + d * r
        tubo([p, p + d * r * 0.45], r * 0.07, m, lados=5, tampa=True)
        esfera(r * 0.14, p + d * r * 0.5, m, sub=0)


def risco(pos=(0, 0, 0), comp=1.2, giro=-45.0, cor="vermelho", grossura=0.11):
    """O risco de "não" (a barra vermelha em diagonal, na frente de tudo)."""
    caixa((comp, 0.06, grossura), pos, giro=(0, giro, 0), m=mat(cor, rugo=0.4, brilho=0.8), chanfro=0.015)


def estojo_de_tabua(tam=(1, 0.6, 0.5), pos=(0, 0, 0)):
    """O caixote de tábuas (o saque do Mãos leves)."""
    x, y, z = tam
    with grupo(pos=pos):
        caixa((x, y, z), (0, 0, z / 2), m=mat("madeira"), chanfro=0.015)
        for i in range(3):
            caixa((x * 1.01, y * 1.01, z * 0.07), (0, 0, z * (0.2 + 0.3 * i)), m=mat("madeira_escura"))
        for sx in (-1, 1):
            caixa((x * 0.08, y * 1.02, z * 1.01), (sx * x * 0.46, 0, z / 2), m=mat("madeira_clara"))


def crescente(r=0.4, deslocamento=(0.17, 0.08), r2=0.34, n=24):
    """A lua crescente: o círculo de fora menos o de dentro, deslocado."""
    cx, cz = deslocamento
    fora = []
    for i in range(n * 4):
        a = 2 * math.pi * i / (n * 4)
        x, z = r * math.cos(a), r * math.sin(a)
        if (x - cx) ** 2 + (z - cz) ** 2 > r2 ** 2:
            fora.append((a, x, z))
    # da ponta de cima, pelo lado de fora (a esquerda), até a de baixo; depois volta pelo círculo de dentro
    fora.sort(key=lambda t: (t[0] - math.pi / 4) % (2 * math.pi))
    pts = [(x, z) for _, x, z in fora]
    dois_pi = 2 * math.pi
    a0 = math.atan2(pts[0][1] - cz, pts[0][0] - cx) % dois_pi
    a1 = math.atan2(pts[-1][1] - cz, pts[-1][0] - cx) % dois_pi
    dentro = []
    for i in range(1, n):
        a = a1 + (a0 - a1) * i / n
        dentro.append((cx + r2 * math.cos(a), cz + r2 * math.sin(a)))
    return pts + dentro


# os arquivos do jogo
ARMAS = os.path.join(JOG, "Armas.fbx")
LOOT = os.path.join(JOG, "Loot.fbx")
SUCATA = os.path.join(JOG, "Loot_Sucata.fbx")
ITENS = os.path.join(JOG, "Itens.fbx")
PISTOLA = ["Pistola_corpo", "Pistola_ferrolho", "Pistola_carregador"]
SUC_PISTOLA = (os.path.join("Armas", "Modelos", "Sucata_Pistola.fbx"), ["Sucata_Pistola_corpo", "Sucata_Pistola_carregador"])
SUC_RIFLE = (os.path.join("Armas", "Modelos", "Sucata_Rifle.fbx"), ["Sucata_Rifle_corpo", "Sucata_Rifle_ferrolho"])
SUC_SUB = (os.path.join("Armas", "Modelos", "Sucata_Submetralhadora.fbx"),
           ["Sucata_Submetralhadora_corpo", "Sucata_Submetralhadora_ferrolho", "Sucata_Submetralhadora_carregador"])


def pistola_do_jogo(tam=1.0, pos=(0, 0, 0), inclina=-8):
    """A pistola do Armas.fbx com o cano para a direita (+X)."""
    with grupo(pos=pos, giro=(0, inclina, 0)):
        return modelo(ARMAS, PISTOLA, tam=tam, giro=(0, 0, 90))


def pistola_de_sucata(tam=1.0, pos=(0, 0, 0), inclina=-8):
    """A pistola de sucata com o cano para a direita (+X)."""
    with grupo(pos=pos, giro=(0, inclina, 0)):
        return modelo(*SUC_PISTOLA, tam=tam, giro=(0, 0, 90))


# ----------------------------------------------------------------------------------------------------------------------
def carregador(pos=(0, 0, 0), giro=(0, 0, 0), escala=1.0, cor="aco"):
    """O carregador feito à mão (o do jogo é preto e some no fundo): o corpo curvo, a base e a bala na boca."""
    corpo = mat(cor, metal=0.5, rugo=0.4)
    escuro = mat("aco_escuro", metal=0.5, rugo=0.4)
    with grupo(pos=pos, giro=giro, escala=escala):
        caixa((0.36, 0.14, 0.52), (0, 0, 0.0), giro=(0, -5, 0), m=corpo, chanfro=0.02)
        caixa((0.36, 0.14, 0.46), (0.05, 0, -0.45), giro=(0, -14, 0), m=corpo, chanfro=0.02)
        caixa((0.42, 0.18, 0.07), (0.11, 0, -0.69), giro=(0, -14, 0), m=escuro, chanfro=0.01)
        for z in (0.12, -0.05, -0.22):
            caixa((0.22, 0.02, 0.025), (0.0, -0.075, z), giro=(0, -5, 0), m=escuro)
        bala(pos=(-0.17, 0, 0.3), giro=(0, 90, 0), escala=0.95)


# COMBATE: o Guerreiro

def ic_g1():  # Tiro forte: a bala saindo do estouro
    perfil(estrela(11, 0.62, 0.27), 0.04, mat("laranja_vivo", brilho=2.6), centro=(-0.05, 0.25, -0.05), giro=(0, 8, 0))
    perfil(estrela(9, 0.38, 0.18), 0.05, mat("amarelo", brilho=3.5), centro=(-0.05, 0.18, -0.05), giro=(0, 20, 0))
    bala(pos=(0.0, 0.0, 0.0), giro=(0, 52, 0), escala=2.0)
    for i, d in enumerate((-0.16, 0.0, 0.16)):
        caixa((0.38 - 0.08 * abs(i - 1), 0.02, 0.025), (-0.55 + 0.05 * abs(i - 1), 0.1, -0.42 + d), giro=(0, -38, 0),
              m=mat("branco", brilho=1.2))


def ic_g2():  # Recarga rápida: o carregador com a seta girando
    seta = mat("amarelo", brilho=1.4)
    perfil(arco_faixa(0.7, 0.63, 130, 390, 22), 0.04, seta, centro=(0, 0.35, 0))
    a = math.radians(130)
    perfil([(-0.11, 0.0), (0.11, 0.0), (0.0, -0.17)], 0.05, seta, centro=(0.665 * math.cos(a), 0.35, 0.665 * math.sin(a)),
           giro=(0, -40, 0))
    carregador(pos=(0, 0, 0.12), giro=(0, -8, 0), escala=1.05)


def ic_g3():  # Braço forte: o taco com pregos batendo
    with grupo(giro=(0, -48, 0)):
        # o taco do jogo, mais grosso (é fino demais para ler num quadrado pequeno); a cabeça com os pregos em +X
        with grupo(escala=(1.0, 1.7, 1.7)):
            objs = modelo(os.path.join(JOG, "Itens_Bastao.fbx"), ["Bastao_corpo"], tam=1.5, giro=(0, 0, 90))
    p = ponta(objs, (0.67, 0, 0.74))
    perfil(estrela(9, 0.4, 0.18), 0.04, mat("laranja_vivo", brilho=2.0), centro=p + Vector((0.08, 0.14, 0.06)))
    perfil(estrela(7, 0.22, 0.1), 0.05, mat("amarelo", brilho=2.6), centro=p + Vector((0.08, 0.1, 0.06)))
    for r in (0.5, 0.66):
        perfil(arco_faixa(r, r - 0.05, 105, 165, 10), 0.03, mat("branco", brilho=1.0), centro=(-0.1, 0.1, -0.05))


def ic_g4():  # Mão firme: a mão aberta e a mira na palma
    with grupo(giro=(0, 0, 0)):
        mao(m_manga=mat("vinho"))
    m = mat("vermelho", brilho=1.6)
    toro(0.24, 0.022, (0, -0.15, 0.32), giro=(90, 0, 0), m=m, seg=24, lados=5)
    for a in range(4):
        ang = math.radians(90 * a)
        caixa((0.13, 0.03, 0.03), (math.cos(ang) * 0.3, -0.15, 0.32 + math.sin(ang) * 0.3), giro=(0, -90 * a, 0), m=m)
    esfera(0.035, (0, -0.16, 0.32), m, sub=1)


def ic_g5():  # Mira de aço: a mira de metal brilhante
    aco = mat("prata", metal=0.75, rugo=0.22)
    escuro = mat("aco_escuro", metal=0.6, rugo=0.35)
    toro(0.5, 0.06, giro=(90, 0, 0), m=aco, seg=28, lados=6)
    toro(0.3, 0.025, giro=(90, 0, 0), m=escuro, seg=24, lados=5)
    for a in range(4):
        ang = math.radians(90 * a)
        caixa((0.34, 0.06, 0.05), (math.cos(ang) * 0.36, 0, math.sin(ang) * 0.36), giro=(0, -90 * a, 0), m=aco, chanfro=0.01)
    esfera(0.05, (0, -0.02, 0), mat("vermelho", brilho=2.5), sub=1)
    faisca((0.38, -0.12, 0.38), 0.2)
    faisca((-0.42, -0.1, -0.3), 0.11)


# COMBATE: o Armeiro de sucata

def ic_a1():  # Pistola de sucata
    pistola_de_sucata(1.0, inclina=-10)


def ic_a2():  # Rifle de precisão de sucata
    with grupo(giro=(0, -32, 0)):
        modelo(*SUC_RIFLE, tam=1.0, giro=(0, 0, 180))


def ic_a3():  # Submetralhadora de sucata
    with grupo(giro=(0, -10, 0)):
        modelo(*SUC_SUB, tam=1.0, giro=(0, 0, 90))


def ic_a4():  # Cano reforçado: o cano com braçadeiras
    with grupo(giro=(0, -35, 0)):
        cilindro(0.13, 1.5, giro=(0, 90, 0), m=mat("ferrugem", metal=0.4, rugo=0.6), lados=10)
        cilindro(0.075, 0.02, (0.751, 0, 0), giro=(0, 90, 0), m=mat("preto"), lados=10)
        for x in (-0.45, 0.0, 0.45):
            cilindro(0.165, 0.11, (x, 0, 0), giro=(0, 90, 0), m=mat("prata", metal=0.7, rugo=0.3), lados=10)
            caixa((0.07, 0.09, 0.08), (x, -0.06, 0.18), m=mat("aco_escuro", metal=0.6, rugo=0.35))
            cilindro(0.025, 0.12, (x, -0.06, 0.24), m=mat("prata", metal=0.7, rugo=0.3), lados=6)
    faisca((0.55, -0.2, 0.62), 0.16)


def ic_a5():  # Miras caseiras: a mira de ponto feita de cano, quase de frente
    with grupo(giro=(58, 0, 8)):
        cilindro(0.32, 0.6, m=mat("aco_escuro", metal=0.5, rugo=0.45), lados=12)
        cilindro(0.26, 0.02, (0, 0, 0.301), m=mat("preto", rugo=0.9), lados=12)
        toro(0.3, 0.04, (0, 0, 0.3), m=mat("ferrugem", metal=0.4, rugo=0.5), seg=16, lados=5)
        for z in (-0.12, 0.1):
            cilindro(0.345, 0.06, (0, 0, z), m=mat("prata", metal=0.7, rugo=0.3), lados=12)
        caixa((0.42, 0.16, 0.6), (0, -0.4, 0.0), m=mat("ferrugem", metal=0.3, rugo=0.6), chanfro=0.02)
        caixa((0.6, 0.08, 0.7), (0, -0.5, 0.0), m=mat("aco_escuro", metal=0.5, rugo=0.4), chanfro=0.01)
        esfera(0.065, (0, 0, 0.33), mat("vermelho", brilho=5.0), sub=1)
        for g in (0, 90):
            caixa((0.5, 0.012, 0.01), (0, 0, 0.31), giro=(0, 0, g), m=mat("aco", metal=0.5))


def ic_a6():  # Carregador estendido: dois carregadores emendados com fita
    carregador(pos=(0, 0, 0.42), giro=(0, -6, 0), escala=0.8)
    carregador(pos=(0.1, 0.02, -0.34), giro=(0, 170, 0), escala=0.8, cor="ferrugem")
    caixa((0.38, 0.17, 0.2), (0.06, 0, 0.02), giro=(0, -8, 0), m=mat("prata", metal=0.3, rugo=0.5), chanfro=0.01)
    seta = mat("amarelo", brilho=1.4)
    perfil([(-0.05, -0.35), (0.05, -0.35), (0.05, 0.3), (0.13, 0.3), (0, 0.5), (-0.13, 0.3), (-0.05, 0.3)], 0.05,
           seta, centro=(0.52, -0.1, 0))


def ic_a7():  # Luneta de cano
    corpo = mat("aco_escuro", metal=0.5, rugo=0.4)
    with grupo(giro=(0, -12, 0)):
        cilindro(0.12, 1.1, giro=(0, 90, 0), m=corpo, lados=10)
        revolucao([(0.12, 0.0), (0.2, 0.18), (0.2, 0.3)], corpo, centro=(0.5, 0, 0), giro=(0, 90, 0), lados=10)
        revolucao([(0.12, 0.0), (0.16, 0.12), (0.16, 0.22)], corpo, centro=(-0.5, 0, 0), giro=(0, -90, 0), lados=10)
        cilindro(0.17, 0.02, (0.81, 0, 0), giro=(0, 90, 0), m=mat("azul_claro", metal=0.2, rugo=0.1, brilho=1.2), lados=10)
        cilindro(0.13, 0.02, (-0.73, 0, 0), giro=(0, 90, 0), m=mat("azul_escuro", rugo=0.1, brilho=0.6), lados=10)
        for x in (-0.22, 0.22):
            cilindro(0.135, 0.12, (x, 0, 0), giro=(0, 90, 0), m=mat("prata", metal=0.4, rugo=0.5), lados=10)
            caixa((0.1, 0.12, 0.2), (x, 0, -0.2), m=mat("ferrugem", metal=0.3, rugo=0.6))
        caixa((0.7, 0.16, 0.06), (0, 0, -0.32), m=mat("ferrugem", metal=0.3, rugo=0.6), chanfro=0.01)


def ic_a8():  # Mola nova
    mola(voltas=6, raio=0.28, alt=1.1, fio=0.05, giro=(0, -20, 0))
    faisca((0.42, -0.2, 0.45), 0.2)
    faisca((-0.4, -0.2, -0.2), 0.13)
    faisca((0.35, -0.2, -0.45), 0.09)


def ic_a9():  # Silenciador caseiro: a garrafa PET na boca do cano
    with grupo(giro=(0, -6, 0)):
        p = pistola_de_sucata(1.0, inclina=0)
        boca = ponta(p, (1, 0, 0.05))
        garrafa = modelo(LOOT, ["RefrigeranteGarrafa_corpo"], tam=0.8, giro=(0, -90, 0))
        lo, hi = medidas(garrafa)
        dx = boca.x - lo.x - 0.02
        dz = boca.z - (lo.z + hi.z) / 2
        for o in garrafa:
            o.location = (dx, 0, dz)
        caixa((0.08, 0.2, 0.2), (boca.x + 0.02, 0, boca.z), m=mat("prata", metal=0.3, rugo=0.5), chanfro=0.01)


def ic_a10():  # Mestre armeiro: o troféu de sucata
    ouro = mat("dourado", metal=0.7, rugo=0.3)
    caixa((0.7, 0.5, 0.18), (0, 0, -0.62), m=mat("madeira"), chanfro=0.02)
    caixa((0.56, 0.4, 0.12), (0, 0, -0.48), m=mat("madeira_escura"), chanfro=0.015)
    cilindro(0.07, 0.3, (0, 0, -0.28), m=mat("ferrugem", metal=0.4, rugo=0.6), lados=6)
    cilindro(0.15, 0.08, (0, 0, -0.14), m=mat("aco", metal=0.6, rugo=0.4), lados=6)
    revolucao([(0.0, -0.1), (0.12, -0.08), (0.3, 0.05), (0.4, 0.3), (0.42, 0.52), (0.37, 0.52), (0.0, 0.5)],
              ouro, lados=12)
    for s in (-1, 1):
        toro(0.16, 0.035, (s * 0.42, 0, 0.27), giro=(90, 0, 0), m=mat("ferrugem", metal=0.4, rugo=0.6), seg=12,
             arco=200, ini=-100 if s > 0 else 80)
    with grupo(pos=(0, -0.37, 0.25)):
        perfil(engrenagem(9, 0.17, 0.13), 0.05, mat("aco_escuro", metal=0.6, rugo=0.35))
        perfil(circulo(0.05, 10), 0.07, mat("prata", metal=0.6, rugo=0.3))
    faisca((0.42, -0.2, 0.62), 0.16)


# COMBATE: o Assassino

def ic_s1():  # Passos leves: as pegadas suaves
    m1 = mat("cinza_claro", rugo=0.7)
    m2 = mat("cinza", rugo=0.7)
    with grupo(giro=(58, 0, 0)):
        pe(pos=(0.18, 0, -0.95), esquerdo=False, m=m1, giro=(-90, 0, -8))
        pe(pos=(-0.22, 0, -0.1), esquerdo=True, m=m2, giro=(-90, 0, 8), escala=0.9)
        pe(pos=(0.14, 0, 0.62), esquerdo=False, m=mat("carvao"), giro=(-90, 0, -6), escala=0.8)


def ic_s2():  # Pelas costas: a faca e o zumbi de costas
    pele = mat("zumbi")
    roupa = mat("vinho_escuro")
    cabelo = mat("preto", rugo=0.8)
    with grupo(pos=(-0.15, 0.4, 0.1)):
        caixa((0.62, 0.32, 0.72), (0, 0, -0.1), m=roupa, chanfro=0.02)
        caixa((0.16, 0.26, 0.6), (-0.4, 0, -0.08), giro=(0, 6, 0), m=roupa, chanfro=0.02)
        caixa((0.16, 0.26, 0.6), (0.4, 0, -0.08), giro=(0, -6, 0), m=roupa, chanfro=0.02)
        caixa((0.14, 0.14, 0.1), (0, 0, 0.3), m=pele)
        caixa((0.38, 0.36, 0.38), (0, 0, 0.53), m=pele, chanfro=0.02)
        # o cabelo cobre a nuca: é a parte de trás da cabeça que fica para a câmera
        caixa((0.41, 0.1, 0.32), (0, -0.15, 0.58), m=cabelo, chanfro=0.015)
        caixa((0.41, 0.38, 0.1), (0, 0.0, 0.73), m=cabelo, chanfro=0.015)
    with grupo(pos=(0.3, -0.3, -0.22), giro=(0, 36, 0)):
        modelo(os.path.join(JOG, "Itens_Faca.fbx"), ["Faca_corpo"], tam=1.25, giro=(0, 0, -90))


def ic_s3():  # Discreto: o olho fechado
    claro = mat("creme", rugo=0.5)
    perfil(arco_faixa(0.62, 0.52, 200, 340, 16, cz=0.28), 0.12, claro, chanfro=0.01)
    for a in (215, 242, 270, 298, 325):
        r = math.radians(a)
        p = (0.6 * math.cos(r), 0.28 + 0.6 * math.sin(r))
        caixa((0.05, 0.08, 0.2), (p[0] + 0.1 * math.cos(r), 0, p[1] + 0.1 * math.sin(r)), giro=(0, -(a - 270) - 0, 0),
              m=claro)
    perfil(arco_faixa(0.8, 0.72, 25, 155, 16, cz=-0.12), 0.08, mat("cinza", rugo=0.6))
    perfil(arco_faixa(0.74, 0.66, 205, 335, 16, cz=0.62), 0.06, mat("roxo", rugo=0.6, brilho=0.3))


def ic_s4():  # Tiro abafado: o silenciador na pistola
    with grupo(giro=(0, -6, 0)):
        p = pistola_do_jogo(0.9, inclina=0)
        boca = ponta(p, (1, 0, 0.1))
        preto = mat("grafite", metal=0.4, rugo=0.4)
        cilindro(0.075, 0.62, (boca.x + 0.3, 0, boca.z), giro=(0, 90, 0), m=preto, lados=10)
        for dx in (0.06, 0.54):
            cilindro(0.082, 0.04, (boca.x + dx, 0, boca.z), giro=(0, 90, 0), m=mat("aco", metal=0.6, rugo=0.3), lados=10)
        fumo = mat("cinza_claro", rugo=0.9, alfa=0.55)
        for i, (dx, dz, r) in enumerate(((0.72, 0.02, 0.06), (0.83, 0.06, 0.08), (0.96, 0.04, 0.1))):
            esfera(r, (boca.x + dx, 0, boca.z + dz), fumo, sub=1)


def ic_s5():  # Sangue frio: o floco de neve sobre o coração
    coracao_3d(pos=(0, 0.1, -0.05), tam=1.25, cor="vermelho_escuro")
    gelo = mat("azul_claro", rugo=0.25, brilho=0.7)
    with grupo(pos=(0.0, -0.12, 0.42), escala=0.62):
        for k in range(6):
            a = 60 * k
            r = math.radians(a)
            caixa((0.7, 0.07, 0.07), (0.35 * math.cos(r), 0, 0.35 * math.sin(r)), giro=(0, -a, 0), m=gelo)
            for d in (0.42, 0.62):
                for s in (-1, 1):
                    rr = math.radians(a + s * 45)
                    c = (d * math.cos(r) + 0.09 * math.cos(rr), d * math.sin(r) + 0.09 * math.sin(rr))
                    caixa((0.2 - d * 0.12, 0.06, 0.05), (c[0], 0, c[1]), giro=(0, -(a + s * 45), 0), m=gelo)
        perfil(circulo(0.12, 6), 0.1, mat("branco", rugo=0.3, brilho=0.8))


# COMBATE: o Ladrão

def ic_l1():  # Borracheiro: o pneu com remendo
    with grupo(giro=(0, 0, 18)):
        objs = modelo(os.path.join(JOG, "Pecas.fbx"), ["Pneu_corpo"], tam=1.0, giro=(90, 0, 0))
        lo, hi = medidas(objs)
        with grupo(pos=(0.24, lo.y - 0.005, 0.24), giro=(0, 0, 0)):
            perfil(circulo(0.16, 12, rz=0.12), 0.03, mat("laranja_vivo", rugo=0.5), giro=(0, 35, 0))
            perfil(circulo(0.12, 12, rz=0.085), 0.04, mat("preto", rugo=0.7), centro=(0, -0.01, 0), giro=(0, 35, 0))
    faisca((0.5, -0.4, 0.5), 0.14)


def ic_l2():  # Mãos leves: o pé de cabra abrindo o caixote
    estojo_de_tabua((0.9, 0.6, 0.48), pos=(0, 0, -0.4))
    caixa((0.92, 0.62, 0.07), (0.0, 0.0, 0.13), giro=(-8, 0, 0), m=mat("madeira_clara"), chanfro=0.01)
    modelo(LOOT, ["PeDeCabra_corpo"], tam=1.15, pos=(0.15, -0.36, 0.25), giro=(0, -35, 0))


def ic_l3():  # Chaveiro: o grampo na fechadura
    latao = mat("dourado", metal=0.6, rugo=0.35)
    caixa((0.7, 0.3, 0.6), (0, 0, -0.15), m=latao, chanfro=0.05)
    toro(0.24, 0.06, (0, 0, 0.15), giro=(90, 0, 0), m=mat("prata", metal=0.75, rugo=0.25), seg=14, arco=180, lados=6)
    for x in (-0.24, 0.24):
        cilindro(0.06, 0.12, (x, 0, 0.15), m=mat("prata", metal=0.75, rugo=0.25), lados=6)
    preto = mat("preto", rugo=0.8)
    perfil(circulo(0.07, 10, 0, -0.08), 0.02, preto, centro=(0, -0.155, 0))
    perfil([(-0.03, -0.12), (0.03, -0.12), (0.05, -0.3), (-0.05, -0.3)], 0.02, preto, centro=(0, -0.155, 0))
    gr = mat("prata", metal=0.8, rugo=0.25)
    tubo([(0.0, -0.17, -0.1), (0.22, -0.3, -0.18), (0.62, -0.32, -0.42)], 0.022, gr, lados=5)
    tubo([(0.0, -0.17, -0.24), (0.0, -0.3, -0.24), (0.12, -0.34, -0.5), (0.4, -0.38, -0.62)], 0.022,
         mat("aco_escuro", metal=0.6, rugo=0.3), lados=5)


def ic_l4():  # Alarme mudo: a sirene riscada
    corneta = mat("aco", metal=0.45, rugo=0.4)
    with grupo(giro=(0, 0, -25)):
        revolucao([(0.13, 0.0), (0.17, 0.2), (0.32, 0.45), (0.48, 0.55)], corneta, giro=(90, 0, 0), centro=(0, 0.25, 0),
                  lados=12)
        cilindro(0.44, 0.03, (0, -0.29, 0), giro=(90, 0, 0), m=mat("preto", rugo=0.9), lados=12)
        cilindro(0.12, 0.04, (0, -0.25, 0), giro=(90, 0, 0), m=mat("aco_escuro", metal=0.5), lados=10)
        caixa((0.4, 0.34, 0.34), (0, 0.4, 0), m=mat("grafite", metal=0.3, rugo=0.5), chanfro=0.02)
        caixa((0.1, 0.12, 0.35), (0, 0.4, -0.32), m=mat("aco_escuro", metal=0.5, rugo=0.4))
    onda = mat("amarelo", brilho=1.2)
    for r in (0.66, 0.84):
        perfil(arco_faixa(r, r - 0.06, -40, 40, 10), 0.04, onda, centro=(-0.1, -0.2, 0.0))
    risco((0.05, -0.6, 0.0), comp=1.5, giro=-40)


def ic_l5():  # Ligação direta: os fios com faísca
    verm = mat("vermelho", rugo=0.5)
    azul = mat("azul", rugo=0.5)
    cobre = mat("cobre", metal=0.7, rugo=0.3)
    tubo([(-0.85, 0, -0.6), (-0.6, 0, -0.45), (-0.35, 0, -0.1), (-0.14, 0, 0.02)], 0.06, verm, lados=7)
    tubo([(-0.14, 0, 0.02), (0.02, 0, 0.06)], 0.035, cobre, lados=6)
    tubo([(0.85, 0, -0.65), (0.6, 0, -0.5), (0.4, 0, -0.15), (0.17, 0, 0.0)], 0.06, azul, lados=7)
    tubo([(0.17, 0, 0.0), (0.03, 0, 0.1)], 0.035, cobre, lados=6)
    perfil(estrela(8, 0.36, 0.12, ini=12), 0.04, mat("amarelo", brilho=3.2), centro=(0.02, -0.08, 0.1))
    perfil(estrela(6, 0.18, 0.07), 0.05, mat("branco", brilho=4.0), centro=(0.02, -0.11, 0.1))
    for (x, z) in ((0.3, 0.42), (-0.28, 0.38), (0.38, 0.12)):
        caixa((0.05, 0.03, 0.05), (x, -0.1, z), giro=(0, 45, 0), m=mat("amarelo", brilho=3.0))


# ----------------------------------------------------------------------------------------------------------------------
# RESISTÊNCIA: o Fogo

def ic_f1():  # Tocha duradoura
    with grupo(giro=(0, -55, 0)):
        objs = modelo(os.path.join(JOG, "Itens_Tocha.fbx"), ["Tocha_corpo"], tam=1.2, giro=(0, 0, 90))
    p = ponta(objs, (0.57, 0, 0.82))
    chama(p + Vector((-0.03, 0, -0.08)), alt=0.8, larg=0.28, inclina=10)


def ic_f2():  # Lampião
    metal = mat("vermelho", metal=0.3, rugo=0.45)
    revolucao([(0.0, -0.6), (0.36, -0.6), (0.38, -0.5), (0.34, -0.3), (0.22, -0.26), (0.0, -0.26)], metal, lados=12)
    revolucao([(0.12, -0.27), (0.24, -0.15), (0.27, 0.05), (0.22, 0.24), (0.14, 0.32)],
              mat("amarelo", rugo=0.2, alfa=0.55, brilho=1.2), lados=12)
    chama((0, -0.03, -0.2), alt=0.36, larg=0.09)
    revolucao([(0.0, 0.3), (0.3, 0.3), (0.18, 0.44), (0.08, 0.5), (0.0, 0.5)], metal, lados=12)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        tubo([(0.3 * math.cos(a), 0.3 * math.sin(a), -0.28), (0.3 * math.cos(a), 0.3 * math.sin(a), 0.31)], 0.018,
             mat("aco_escuro", metal=0.6, rugo=0.4), lados=4)
    toro(0.24, 0.022, (0, 0, 0.5), giro=(90, 0, 0), m=mat("prata", metal=0.7, rugo=0.3), seg=14, arco=180, lados=5)


def ic_f3():  # Coquetel molotov
    with grupo(giro=(0, -18, 0)):
        revolucao([(0.0, -0.62), (0.22, -0.62), (0.24, -0.55), (0.24, 0.0), (0.2, 0.12), (0.08, 0.24), (0.07, 0.42),
                   (0.0, 0.42)], mat("garrafa_verde", metal=0.1, rugo=0.15, alfa=0.85), lados=10)
        revolucao([(0.0, -0.6), (0.225, -0.6), (0.225, -0.15), (0.0, -0.15)], mat("ambar", rugo=0.3, brilho=0.3), lados=10)
        caixa((0.4, 0.35, 0.3), (0, 0, -0.38), m=mat("creme", rugo=0.8))
        pano = mat("branco_sujo", rugo=0.9)
        caixa((0.1, 0.1, 0.22), (0, 0, 0.5), giro=(0, 12, 8), m=pano)
        fita([(0.03, 0, 0.55), (0.14, 0, 0.5), (0.2, 0, 0.36), (0.22, 0, 0.2)], 0.1, pano)
        chama((0.0, 0, 0.6), alt=0.55, larg=0.2, inclina=-6)


def ic_f4():  # Fogueira portátil
    modelo(LOOT, ["FogueiraMontada_corpo"], tam=1.15, pos=(0, 0, 0), base=True)
    chama((0.0, 0.0, 0.08), alt=0.85, larg=0.3)
    chama((-0.2, -0.05, 0.06), alt=0.5, larg=0.18, inclina=-14, fase=40)
    chama((0.22, -0.02, 0.06), alt=0.55, larg=0.18, inclina=16, fase=20)


def ic_f5():  # Fogo que pega: as chamas se espalhando
    chao = mat("carvao", rugo=0.9)
    caixa((1.6, 0.4, 0.06), (0, 0, -0.03), m=chao, chanfro=0.01)
    for x, alt, larg in ((-0.55, 0.35, 0.13), (-0.12, 0.6, 0.2), (0.42, 0.95, 0.3)):
        chama((x, 0, 0.0), alt=alt, larg=larg)
    for (x, z) in ((-0.32, 0.45), (0.1, 0.78), (-0.6, 0.6), (0.2, 0.42)):
        caixa((0.04, 0.04, 0.04), (x, -0.1, z), giro=(30, 45, 0), m=mat("laranja_vivo", brilho=3.0))
    seta = mat("amarelo", brilho=1.3)
    perfil([(-0.55, -0.24), (0.3, -0.24), (0.3, -0.18), (0.45, -0.28), (0.3, -0.38), (0.3, -0.32), (-0.55, -0.32)],
           0.04, seta, centro=(0, -0.25, 0))


def ic_f6():  # Calor do fogo: o coração com a chama
    chama((0.0, 0.25, 0.12), alt=1.15, larg=0.42)
    coracao_3d(pos=(0, -0.15, -0.25), tam=1.0, cor="vermelho")


def ic_f7():  # Muralha de fogo: a linha de fogo
    with grupo(giro=(0, 0, 32)):
        caixa((2.2, 0.32, 0.08), (0, 0, -0.04), m=mat("carvao", rugo=0.9), chanfro=0.01)
        for i in range(7):
            x = -0.95 + i * 0.32
            alt = (0.7, 0.95, 0.8, 1.05, 0.85, 0.95, 0.65)[i]
            chama((x, 0, 0.0), alt=alt, larg=0.2, fase=i * 25)


# RESISTÊNCIA: a Saúde

def ic_h1():  # Mãos de enfermeiro: a mão enfaixada
    branco = mat("branco", rugo=0.8)
    with grupo(giro=(0, -8, -12)):
        mao(m_manga=mat("azul_claro", rugo=0.7))
        caixa((0.55, 0.21, 0.15), (0, 0, 0.17), giro=(0, 4, 0), m=branco, chanfro=0.012)
        caixa((0.55, 0.21, 0.13), (0, 0, 0.36), giro=(0, -7, 0), m=branco, chanfro=0.012)
        fita([(0.27, -0.11, 0.2), (0.38, -0.12, 0.08), (0.45, -0.12, -0.1), (0.5, -0.12, -0.28)], 0.12, branco,
             lado=(0.2, 0, 1))
    with grupo(pos=(-0.5, -0.4, 0.62)):
        verde = mat("verde", rugo=0.5, brilho=0.5)
        caixa((0.26, 0.06, 0.085), m=verde)
        caixa((0.085, 0.06, 0.26), m=verde)


def ic_h2():  # Curativo bem feito: dois curativos cruzados
    for g, y in ((35, 0.05), (-35, -0.05)):
        with grupo(pos=(0, y, 0), giro=(0, g, 0)):
            fita_c = []
            for i in range(14):
                a = math.pi / 2 + math.pi * i / 13
                fita_c.append((-0.42 + 0.16 * math.cos(a), 0.16 * math.sin(a)))
            for i in range(14):
                a = -math.pi / 2 + math.pi * i / 13
                fita_c.append((0.42 + 0.16 * math.cos(a), 0.16 * math.sin(a)))
            perfil(fita_c, 0.05, mat("areia", rugo=0.7), chanfro=0.01)
            caixa((0.32, 0.07, 0.24), (0, -0.01, 0), m=mat("branco", rugo=0.7), chanfro=0.015)
            for sx in (-1, 1):
                for (dx, dz) in ((0.32, 0.06), (0.32, -0.06), (0.44, 0.0)):
                    perfil(circulo(0.018, 6, sx * dx, dz), 0.06, mat("madeira_escura"))
    faisca((0.5, -0.3, 0.42), 0.16)
    faisca((-0.52, -0.3, -0.36), 0.1)


def ic_h3():  # Imunidade: o escudo com o vírus riscado
    escudo(tam=1.2, cor_borda="prata", cor_meio="azul")
    virus(pos=(0, -0.3, 0.04), r=0.33, cor="verde", cor_ponta="cacto")
    risco((0, -0.66, 0.04), comp=1.05, giro=-45, grossura=0.1)


def ic_h4():  # Nervos de aço: o cérebro, de lado
    rosa = mat("rosa", rugo=0.5)
    c = rgb("rosa")
    sulco = mat(tuple(v * 0.62 for v in c), rugo=0.55)
    ax, ay, az = 0.27, 0.5, 0.34
    with grupo(giro=(0, 0, -68)):
        for s in (-1, 1):
            cx = s * 0.28
            esfera(1.0, (cx, 0, 0), rosa, sub=2, escala=(ax, ay, az))
            for k, alfa in enumerate((8, 34, 60, 86, 112, 136)):
                pts = []
                for i in range(19):
                    t = -0.9 + 1.8 * i / 18
                    q = math.sqrt(max(0.0, 1 - t * t))
                    a = math.radians(alfa + 12 * math.sin(7 * t + k * 1.7))
                    pts.append((cx + s * ax * q * math.sin(a) * 1.03, ay * t, az * q * math.cos(a) * 1.03))
                tubo(pts, 0.032, sulco, lados=5)
        esfera(1.0, (0, 0.34, -0.26), sulco, sub=1, escala=(0.32, 0.17, 0.13))
        cilindro(0.075, 0.32, (0, 0.14, -0.42), giro=(20, 0, 0), m=sulco, lados=6)
    faisca((0.55, -0.3, 0.42), 0.16, cor="azul_claro")


def ic_h5():  # Médico de campo: o kit médico com o estetoscópio
    objs = modelo(LOOT, ["KitMedico_corpo"], tam=1.0, giro=(0, 0, -10))
    lo, hi = medidas(objs)
    preto = mat("grafite", rugo=0.5)
    z = hi.z + 0.02
    tubo([(-0.35, -0.1, z), (-0.15, -0.05, z + 0.02), (0.05, 0.05, z + 0.02), (0.25, 0.0, z), (0.42, -0.2, z - 0.05),
          (0.48, -0.3, z - 0.2), (0.45, -0.32, z - 0.38)], 0.025, preto, lados=6)
    cilindro(0.08, 0.04, (0.45, -0.34, z - 0.42), giro=(80, 0, 0), m=mat("prata", metal=0.75, rugo=0.25), lados=10)
    for s in (-1, 1):
        tubo([(-0.35, -0.1, z), (-0.42 + s * 0.06, -0.12, z + 0.12), (-0.45 + s * 0.12, -0.15, z + 0.28)], 0.018,
             mat("prata", metal=0.75, rugo=0.25), lados=5)


# RESISTÊNCIA: a Fome e sede

def ic_n1():  # Estômago forte: a lata de comida aberta com a colher
    objs = modelo(LOOT, ["Feijao_corpo"], tam=1.0, giro=(0, 0, 20))
    lo, hi = medidas(objs)
    r = (hi.x - lo.x) / 2 * 0.86
    cilindro(r, 0.03, (0, 0, hi.z + 0.005), m=mat("chocolate", rugo=0.7), lados=12)
    for i in range(7):
        a = i * 0.9
        esfera(0.07, (math.cos(a) * r * 0.55, math.sin(a) * r * 0.55, hi.z + 0.03), mat("tijolo", rugo=0.6), sub=1,
               escala=(1.3, 1, 0.7))
    cilindro(r * 1.08, 0.02, (0, r * 0.95, hi.z + r * 0.85), giro=(-75, 0, 0), m=mat("lata_clara", metal=0.7, rugo=0.3),
             lados=12)
    modelo(LOOT, ["Colher_corpo"], tam=0.85, pos=(0.18, -0.05, hi.z + 0.18), giro=(0, -55, 0))


def ic_n2():  # Cantil
    capa = mat("oliva", rugo=0.8)
    cilindro(0.45, 0.24, giro=(90, 0, 0), m=capa, lados=14, chanfro=0.05)
    toro(0.45, 0.03, giro=(90, 0, 0), m=mat("oliva_escuro", rugo=0.8), seg=28, lados=5)
    cilindro(0.09, 0.16, (0, 0, 0.5), m=mat("aco", metal=0.6, rugo=0.35), lados=8)
    cilindro(0.12, 0.1, (0, 0, 0.6), m=mat("preto", rugo=0.6), lados=8)
    tubo([(-0.45, 0, 0.1), (-0.55, 0, 0.45), (-0.3, 0, 0.85), (0.3, 0, 0.85), (0.55, 0, 0.45), (0.45, 0, 0.1)], 0.04,
         mat("couro", rugo=0.7), lados=4)
    perfil(circulo(0.16, 10), 0.03, mat("oliva_escuro", rugo=0.8), centro=(0.12, -0.14, -0.08))


def ic_n3():  # Fôlego: os pulmões
    rosa = mat("rosa", rugo=0.5)
    pulmao = [(-0.08, 0.72), (-0.2, 0.78), (-0.35, 0.64), (-0.46, 0.34), (-0.51, 0.0), (-0.46, -0.25), (-0.3, -0.31),
              (-0.12, -0.25), (-0.06, 0.0), (-0.07, 0.4)]
    perfil(pulmao, 0.32, rosa, centro=(-0.04, 0, 0), chanfro=0.04)
    perfil([(-x, z) for x, z in reversed(pulmao)], 0.32, rosa, centro=(0.04, 0, 0), chanfro=0.04)
    tra = mat("rosa_velho", rugo=0.6)
    cilindro(0.07, 0.5, (0, -0.05, 0.75), m=tra, lados=8)
    for s in (-1, 1):
        tubo([(0, -0.05, 0.52), (s * 0.12, -0.07, 0.42), (s * 0.2, -0.1, 0.28)], 0.05, tra, lados=6)
    ar = mat("azul_claro", brilho=1.0)
    for z in (0.95, 1.12):
        perfil(arco_faixa(0.2, 0.16, 20, 160, 8), 0.03, ar, centro=(0.0, -0.1, z - 0.15))


def ic_n4():  # Bem nutrido: o prato cheio
    modelo(LOOT, ["PratoDeComida_corpo"], tam=1.25, giro=(28, 0, 0))
    vapor = mat("branco", rugo=0.8, alfa=0.55, brilho=0.4)
    for x in (-0.2, 0.05, 0.3):
        tubo([(x, 0, 0.2), (x + 0.06, 0, 0.32), (x - 0.04, 0, 0.45), (x + 0.04, 0, 0.58), (x, 0, 0.68)], 0.025,
             vapor, lados=5, r_fim=0.012)


def ic_n5():  # Camelo: o cacto e a gota
    verde = mat("cacto", rugo=0.6)
    esfera(0.45, (0, 0, -0.55), mat("areia", rugo=0.9), sub=2, escala=(1.3, 1.0, 0.35))
    cilindro(0.15, 1.05, (0, 0, -0.05), m=verde, lados=8)
    esfera(0.15, (0, 0, 0.47), verde, sub=1)
    for s, z0, h in ((-1, -0.1, 0.42), (1, 0.08, 0.32)):
        cilindro(0.09, 0.26, (s * 0.25, 0, z0), giro=(0, 90, 0), m=verde, lados=7)
        cilindro(0.09, h, (s * 0.36, 0, z0 + h / 2), m=verde, lados=7)
        esfera(0.09, (s * 0.36, 0, z0 + h), verde, sub=1)
    gota = [(0, 0.3)] + [(0.16 * math.cos(math.radians(a)), 0.16 * math.sin(math.radians(a)))
                          for a in range(20, 181 + 140, 20)]
    perfil(gota, 0.1, mat("azul_claro", rugo=0.2, brilho=0.9), centro=(0.5, -0.1, 0.42), chanfro=0.02)


# RESISTÊNCIA: os Recursos

def ic_u1():  # Cuidadoso: a arma com o escudo pequeno
    pistola_do_jogo(1.0, pos=(-0.1, 0.1, 0.1), inclina=-6)
    escudo(pos=(0.35, -0.35, -0.3), tam=0.62, cor_borda="prata", cor_meio="verde")


def ic_u2():  # Econômico: a pilha e o galão
    modelo(LOOT, ["Galao_corpo"], tam=1.0, pos=(0.15, 0.25, 0.1), giro=(0, 0, 15))
    modelo(LOOT, ["Pilhas_corpo"], tam=0.65, pos=(-0.3, -0.45, -0.32), giro=(0, 0, -20))


def ic_u3():  # Farmacêutico: o frasco e as cápsulas
    modelo(LOOT, ["Remedio_corpo"], tam=1.1, pos=(-0.1, 0.1, 0.0), giro=(0, 0, 10))
    for (x, z, g) in ((0.38, -0.42, 30), (0.5, -0.2, -40)):
        with grupo(pos=(x, -0.3, z), giro=(0, g, 0)):
            cilindro(0.065, 0.11, (0, 0, 0.055), m=mat("vermelho", rugo=0.35), lados=10)
            esfera(0.065, (0, 0, 0.11), mat("vermelho", rugo=0.35), sub=1)
            cilindro(0.065, 0.11, (0, 0, -0.055), m=mat("branco", rugo=0.35), lados=10)
            esfera(0.065, (0, 0, -0.11), mat("branco", rugo=0.35), sub=1)


def ic_u4():  # Reparador: o kit de reparo
    objs = modelo(LOOT, ["KitDeReparo_corpo"], tam=1.1, giro=(0, 0, 0))
    lo, hi = medidas(objs)
    modelo(LOOT, ["Ferramenta_corpo"], tam=0.75, pos=(0.05, -0.05, hi.z + 0.04), giro=(0, 0, 15))


def ic_u5():  # Remendão: a chave inglesa com a estrela
    aco = mat("prata", metal=0.7, rugo=0.3)
    with grupo(giro=(0, -40, 0)):
        caixa((0.9, 0.08, 0.16), m=aco, chanfro=0.02)
        perfil(arco_faixa(0.22, 0.1, 40, 320, 14), 0.1, aco, centro=(0.58, 0, 0), chanfro=0.01)
        perfil(circulo(0.17, 12), 0.1, aco, centro=(-0.55, 0, 0), chanfro=0.01)
        perfil(circulo(0.08, 6), 0.12, mat("preto"), centro=(-0.55, 0, 0))
    perfil(estrela(5, 0.3, 0.13), 0.1, mat("dourado", metal=0.5, rugo=0.3, brilho=0.8), centro=(0.38, -0.25, 0.42),
           chanfro=0.015)


# RESISTÊNCIA: a Proteção de sucata

def ic_p1():  # Capacete de panela
    aco = mat("lata", metal=0.7, rugo=0.35)
    revolucao([(0.0, 0.42), (0.38, 0.42), (0.43, 0.38), (0.45, 0.0), (0.5, -0.02), (0.5, -0.05), (0.0, -0.05)], aco,
              lados=14)
    caixa((0.62, 0.1, 0.08), (0.75, 0, -0.01), m=mat("preto", rugo=0.6), chanfro=0.02)
    caixa((0.1, 0.12, 0.1), (0.48, 0, 0.0), m=mat("aco_escuro", metal=0.6, rugo=0.4))
    tubo([(-0.4, -0.12, -0.04), (-0.32, -0.25, -0.35), (0.0, -0.32, -0.5), (0.32, -0.25, -0.35), (0.4, -0.12, -0.04)],
         0.03, mat("couro", rugo=0.7), lados=4)
    for a in (110, 160, 210):
        r = math.radians(a)
        esfera(0.035, (0.44 * math.cos(r), 0.44 * math.sin(r), 0.18), mat("aco_escuro", metal=0.6), sub=1)


def _concha(raio, comp, abre, cor, centro=(0, 0, 0), giro=(0, 0, 0)):
    """Uma chapa curva (meia volta de um cano), o eixo em Y local."""
    perfil(arco_faixa(raio, raio - 0.04, 90 - abre / 2, 90 + abre / 2, 10), comp, cor, centro=centro, giro=giro)


def ic_p2():  # Braçadeiras e caneleiras
    lata = mat("lata", metal=0.6, rugo=0.4)
    couro = mat("couro", rugo=0.7)
    fivela = mat("prata", metal=0.7, rugo=0.3)
    # a caneleira em pé, com a joelheira
    with grupo(pos=(-0.25, 0.1, 0.0), giro=(90, 0, -10)):
        _concha(0.24, 1.15, 200, lata)
        for y in (-0.3, 0.1):
            _concha(0.255, 0.1, 206, couro, centro=(0, y, 0.0))
            caixa((0.08, 0.12, 0.05), (0.0, y, 0.27), m=fivela)
        for y in (-0.48, 0.3):
            for a in (60, 120):
                r = math.radians(a)
                esfera(0.025, (0.25 * math.cos(r), y, 0.25 * math.sin(r)), mat("aco_escuro", metal=0.6), sub=1)
        esfera(0.2, (0, 0.62, 0.12), mat("aco", metal=0.6, rugo=0.35), sub=2, escala=(1.15, 0.9, 0.8))
    # a braçadeira deitada na frente
    with grupo(pos=(0.3, -0.35, -0.4), giro=(0, 62, 0)):
        with grupo(giro=(90, 0, 0)):
            _concha(0.18, 0.62, 200, mat("ferrugem", metal=0.4, rugo=0.5))
            for y in (-0.17, 0.15):
                _concha(0.195, 0.08, 206, couro, centro=(0, y, 0.0))
                caixa((0.07, 0.1, 0.045), (0.0, y, 0.205), m=fivela)


COLETE = [(-0.52, -0.62), (0.52, -0.62), (0.54, 0.2), (0.42, 0.36), (0.38, 0.55), (0.4, 0.8), (0.14, 0.8), (0.1, 0.66),
          (0.0, 0.6), (-0.1, 0.66), (-0.14, 0.8), (-0.4, 0.8), (-0.38, 0.55), (-0.42, 0.36), (-0.54, 0.2)]


def _colete(base="oliva", chapa="lata", escuro=False):
    perfil(COLETE, 0.22, mat(base, rugo=0.75), chanfro=0.03)
    perfil([(x * 0.9, z * 0.97 + 0.01) for x, z in COLETE[6:11]], 0.24, mat("couro_escuro", rugo=0.7))
    m1 = mat(chapa, metal=0.6, rugo=0.4)
    m2 = mat("aco_escuro" if escuro else "aco", metal=0.6, rugo=0.4)
    placas = [(-0.23, -0.35, 0.4, 0.4), (0.23, -0.35, 0.4, 0.4), (-0.22, 0.1, 0.34, 0.36), (0.22, 0.1, 0.34, 0.36)]
    if escuro:
        placas += [(-0.27, 0.55, 0.2, 0.3), (0.27, 0.55, 0.2, 0.3)]
    for i, (x, z, w, h) in enumerate(placas):
        caixa((w, 0.05, h), (x, -0.13, z), giro=(0, (i * 7) % 9 - 4, 0), m=m1 if i % 3 == 0 else m2, chanfro=0.015)
        for sx in (-1, 1):
            for sz in (-1, 1):
                esfera(0.024, (x + sx * (w / 2 - 0.045), -0.16, z + sz * (h / 2 - 0.045)), mat("aco_escuro", metal=0.6),
                       sub=0)


def ic_p3():  # Colete de chapa
    with grupo(giro=(0, 0, -12)):
        _colete()


def ic_p4():  # Remendo de chapa
    with grupo(giro=(0, 0, -15)):
        perfil([(-0.6, -0.5), (0.55, -0.55), (0.62, 0.45), (-0.1, 0.55), (-0.55, 0.42)], 0.08,
               mat("aco_escuro", metal=0.5, rugo=0.5), chanfro=0.02)
        perfil([(-0.25, -0.1), (-0.05, 0.05), (0.08, -0.12), (0.22, 0.1), (0.1, 0.22), (-0.2, 0.15)], 0.1,
               mat("preto"), centro=(0, -0.01, 0))
        caixa((0.62, 0.06, 0.55), (0.05, -0.08, 0.05), giro=(0, 6, 0), m=mat("lata_clara", metal=0.7, rugo=0.3),
              chanfro=0.02)
        for sx in (-1, 1):
            for sz in (-1, 1):
                esfera(0.04, (0.05 + sx * 0.24, -0.12, 0.05 + sz * 0.2), mat("ferrugem", metal=0.5), sub=1)
    faisca((0.45, -0.3, 0.45), 0.16)
    faisca((0.58, -0.3, 0.22), 0.09)


def ic_p5():  # Couraça: o colete com o dente de zumbi quebrado
    with grupo(pos=(-0.12, 0.1, -0.05), giro=(0, 0, -12)):
        _colete(base="couro_escuro", chapa="aco", escuro=True)
    dente = mat("osso", rugo=0.5)
    molar = [(-0.3, 0.3), (-0.2, 0.42), (-0.08, 0.34), (0.0, 0.38), (0.08, 0.34), (0.2, 0.42), (0.3, 0.3), (0.28, 0.0),
             (0.2, -0.4), (0.12, -0.45), (0.06, -0.1), (-0.06, -0.1), (-0.12, -0.45), (-0.2, -0.4), (-0.28, 0.0)]
    with grupo(pos=(0.36, -0.4, 0.25), giro=(0, -18, 0), escala=0.95):
        perfil(molar, 0.2, dente, chanfro=0.025)
        racha = [(0.02, 0.38), (-0.05, 0.22), (0.05, 0.08), (-0.04, -0.08), (0.02, -0.2)]
        for a, b in zip(racha, racha[1:]):
            tubo([(a[0], -0.11, a[1]), (b[0], -0.11, b[1])], 0.02, mat("preto"), lados=4)
    with grupo(pos=(0.78, -0.45, 0.55), giro=(0, 35, 0), escala=0.9):
        perfil([(-0.08, 0.0), (0.06, 0.04), (0.1, -0.08), (-0.04, -0.12)], 0.12, dente, chanfro=0.015)


# ----------------------------------------------------------------------------------------------------------------------
# SOBREVIVÊNCIA: o Craft

def ic_k1():  # Sucateiro: o montinho de sucata
    lata = mat("lata", metal=0.6, rugo=0.4)
    ferrugem = mat("ferrugem", metal=0.4, rugo=0.6)
    aco = mat("aco", metal=0.6, rugo=0.4)
    caixa((1.1, 0.7, 0.12), (0, 0.05, -0.36), giro=(0, 6, -10), m=mat("aco_escuro", metal=0.5, rugo=0.5), chanfro=0.02)
    with grupo(pos=(-0.15, 0.25, 0.05), giro=(80, 0, -12)):
        perfil(engrenagem(9, 0.36, 0.28), 0.08, aco, chanfro=0.01)
        perfil(circulo(0.1, 8), 0.1, mat("preto"))
    tubo([(-0.55, -0.1, -0.22), (0.45, 0.05, -0.05)], 0.09, ferrugem, lados=8)
    tubo([(-0.25, -0.25, -0.25), (0.5, -0.2, -0.2)], 0.06, mat("cobre", metal=0.6, rugo=0.35), lados=8)
    caixa((0.42, 0.04, 0.3), (0.32, 0.12, 0.08), giro=(0, -25, 15), m=lata, chanfro=0.01)
    mola(pos=(0.15, -0.38, -0.2), voltas=4, raio=0.08, alt=0.36, fio=0.025, giro=(0, 80, 10))
    cilindro(0.1, 0.07, (-0.42, -0.35, -0.24), giro=(70, 0, 0), m=mat("dourado", metal=0.6, rugo=0.35), lados=6)
    cilindro(0.04, 0.32, (0.55, -0.3, -0.18), giro=(0, 70, 0), m=aco, lados=6)


def ic_k2():  # Faz-tudo: as ferramentas
    modelo(LOOT, ["Ferramenta_corpo"], tam=1.2, giro=(70, 0, 0))


def ic_k3():  # Artesão: o martelo e a serra
    with grupo(giro=(0, 40, 0)):
        lam = [(-0.55, 0.12), (0.5, 0.12), (0.5, -0.05)]
        for i in range(12):
            x = 0.5 - i * 0.09
            lam += [(x - 0.045, -0.13), (x - 0.09, -0.07)]
        lam += [(-0.55, -0.12)]
        perfil(lam, 0.03, mat("prata", metal=0.75, rugo=0.3))
        cabo = [(-0.55, 0.2), (-0.88, 0.22), (-0.92, -0.18), (-0.55, -0.16)]
        perfil(cabo, 0.1, mat("madeira_clara", rugo=0.6), chanfro=0.02)
        perfil(circulo(0.08, 8, -0.73, 0.03, rz=0.11), 0.12, mat("preto"))
    with grupo(pos=(0.05, -0.18, 0.0), giro=(0, -42, 0)):
        cilindro(0.055, 1.0, (0, 0, -0.1), m=mat("madeira", rugo=0.6), lados=7, giro=(0, 90, 0))
        caixa((0.14, 0.16, 0.36), (0.42, 0, -0.1), m=mat("ferro", metal=0.6, rugo=0.4), chanfro=0.015)
        caixa((0.12, 0.12, 0.16), (0.42, 0, -0.33), giro=(0, 14, 0), m=mat("ferro", metal=0.6, rugo=0.4))


def ic_k4():  # Mãos hábeis: dois itens iguais (dois pacotes de bandagem)
    for (x, y, z) in ((-0.3, 0.22, 0.06), (0.3, -0.15, -0.06)):
        modelo(LOOT, ["BandagemAntisseptica_corpo"], tam=0.75, pos=(x, y, z), giro=(0, 0, -10))
    faisca((0.62, -0.4, 0.42), 0.16)
    faisca((-0.66, -0.3, -0.35), 0.11)


def ic_k5():  # Armeiro: as balas e a pólvora
    modelo(LOOT, ["Polvora_corpo"], tam=0.95, pos=(-0.22, 0.2, 0.0), base=True)
    for i, (x, y, g) in enumerate(((0.32, -0.25, 0), (0.55, -0.1, 8), (0.42, -0.42, -12))):
        bala(pos=(x, y, 0.0), giro=(0, g, 0), escala=0.95)
    esfera(0.25, (0.1, -0.4, 0.0), mat("preto", rugo=0.9), sub=1, escala=(1.2, 1.0, 0.25))


# SOBREVIVÊNCIA: as Armadilhas e defesas

def ic_t1():  # Alarme de latas
    corda = mat("corda", rugo=0.8)
    for x in (-0.85, 0.85):
        cilindro(0.05, 0.9, (x, 0, 0.05), m=mat("madeira"), lados=6)
    tubo([(-0.85, 0, 0.42), (-0.3, 0, 0.32), (0.3, 0, 0.32), (0.85, 0, 0.42)], 0.018, corda, lados=4)
    for i, x in enumerate((-0.4, 0.0, 0.4)):
        g = (-12, 6, 18)[i]
        topo = 0.33
        tubo([(x, 0, topo), (x + 0.03 * g / 10, 0, topo - 0.16)], 0.01, corda, lados=3)
        modelo(ITENS, ["Lata_corpo"], tam=0.32, pos=(x + 0.05 * g / 10, 0, topo - 0.33), giro=(0, g, 0))
    onda = mat("amarelo", brilho=1.2)
    for r in (0.24, 0.34):
        perfil(arco_faixa(r, r - 0.035, -45, 45, 8), 0.03, onda, centro=(0.55, -0.1, -0.05))
        perfil(arco_faixa(r, r - 0.035, 135, 225, 8), 0.03, onda, centro=(-0.55, -0.1, -0.05))


def ic_t2():  # Barricada de tábuas
    for x in (-0.45, 0.45):
        caixa((0.14, 0.12, 1.3), (x, 0.08, 0), m=mat("madeira_escura"), chanfro=0.015)
    for i, (z, g) in enumerate(((0.38, 6), (0.0, -5), (-0.38, 3))):
        caixa((1.3, 0.08, 0.24), (0.02 * i, 0, z), giro=(0, g, 0),
              m=mat("madeira_clara" if i % 2 else "madeira"), chanfro=0.015)
        for x in (-0.45, 0.45):
            esfera(0.03, (x, -0.05, z), mat("prata", metal=0.7, rugo=0.3), sub=0)


def ic_t3():  # Tapete de pregos
    with grupo(giro=(18, 0, 20)):
        caixa((1.2, 0.75, 0.08), (0, 0, 0), m=mat("madeira"), chanfro=0.015)
        aco = mat("prata", metal=0.7, rugo=0.3)
        for i in range(5):
            for j in range(3):
                x, y = -0.48 + i * 0.24 + (0.12 if j % 2 else 0) * 0.5, -0.24 + j * 0.24
                cilindro(0.022, 0.2, (x, y, 0.14), m=aco, lados=5)
                cilindro(0.022, 0.07, (x, y, 0.275), m=aco, lados=5, r2=0.0)


def ic_t4():  # Estacas
    def estaca(a, b, cor):
        a, b = Vector(a), Vector(b)
        d = (b - a)
        n = d.normalized()
        tubo([a, b - n * 0.18], 0.065, mat(cor, rugo=0.7), lados=6)
        tubo([b - n * 0.18, b], 0.065, mat("madeira_clara", rugo=0.7), lados=6, r_fim=0.004)
    estaca((-0.6, 0, -0.55), (0.6, -0.1, 0.62), "madeira")
    estaca((0.6, 0, -0.55), (-0.6, -0.1, 0.62), "madeira_escura")
    estaca((-0.15, 0.3, -0.6), (0.25, -0.45, 0.55), "madeira")
    tubo([(-0.8, 0.05, -0.05), (0.8, 0.05, -0.05)], 0.07, mat("casca", rugo=0.8), lados=6)
    esfera(0.5, (0, 0.1, -0.68), mat("cerne", rugo=0.9), sub=1, escala=(1.4, 0.8, 0.3))


def ic_t5():  # Armadilheiro: a engrenagem com a mola
    perfil(engrenagem(10, 0.55, 0.43), 0.14, mat("aco_escuro", metal=0.6, rugo=0.35), centro=(-0.12, 0.1, 0.08),
           chanfro=0.015)
    perfil(circulo(0.15, 10), 0.18, mat("preto"), centro=(-0.12, 0.1, 0.08))
    perfil(circulo(0.25, 12), 0.16, mat("aco", metal=0.6, rugo=0.35), centro=(-0.12, 0.09, 0.08))
    perfil(circulo(0.1, 10), 0.2, mat("preto"), centro=(-0.12, 0.09, 0.08))
    mola(pos=(0.48, -0.3, -0.3), voltas=5, raio=0.15, alt=0.75, fio=0.035, giro=(0, 35, 0))


def ic_t6():  # Cerca de fogo: o galão com o pavio aceso
    objs = modelo(LOOT, ["Galao_corpo"], tam=1.0, giro=(0, 0, 25))
    bico = ponta(objs, (-0.3, -0.2, 1))
    pano = mat("branco_sujo", rugo=0.9)
    fita([bico - Vector((0, 0, 0.04)), bico + Vector((-0.06, 0, 0.1)), bico + Vector((-0.02, 0, 0.22))], 0.11, pano,
         lado=(1, -0.3, 0))
    chama(bico + Vector((-0.02, 0, 0.2)), alt=0.72, larg=0.24)


def ic_t7():  # Fortaleza: a porta com a barricada
    caixa((0.95, 0.1, 1.35), (0, 0.06, 0), m=mat("madeira_escura"), chanfro=0.015)
    for x in (-0.3, 0.0, 0.3):
        caixa((0.02, 0.11, 1.3), (x + 0.15, 0.05, 0), m=mat("casca"))
    for x in (-0.53, 0.53):
        caixa((0.11, 0.18, 1.5), (x, 0.06, 0.05), m=mat("pedra", rugo=0.8), chanfro=0.01)
    caixa((1.17, 0.18, 0.11), (0, 0.06, 0.75), m=mat("pedra", rugo=0.8), chanfro=0.01)
    for g in (35, -35):
        caixa((1.35, 0.07, 0.18), (0, -0.04, -0.05), giro=(0, g, 0), m=mat("madeira_clara"), chanfro=0.01)
    caixa((1.2, 0.08, 0.16), (0, -0.1, 0.38), giro=(0, 3, 0), m=mat("madeira"), chanfro=0.01)
    caixa((0.42, 0.04, 0.32), (0.18, -0.13, -0.42), giro=(0, -4, 0), m=mat("lata", metal=0.6, rugo=0.4), chanfro=0.01)
    for (x, z) in ((-0.5, 0.48), (0.5, 0.31), (-0.48, -0.36), (0.47, 0.32)):
        esfera(0.03, (x * 0.9, -0.15, z), mat("prata", metal=0.7), sub=0)


# SOBREVIVÊNCIA: o Sortudo

def ic_o1():  # Olho bom: a lupa
    with grupo(giro=(0, -20, 0)):
        toro(0.36, 0.06, (0, 0, 0.15), giro=(90, 0, 0), m=mat("dourado", metal=0.7, rugo=0.3), seg=24, lados=6)
        cilindro(0.33, 0.03, (0, 0, 0.15), giro=(90, 0, 0), m=mat("azul_claro", rugo=0.05, alfa=0.45, brilho=0.4), lados=20)
        perfil(arco_faixa(0.26, 0.21, 100, 160, 8), 0.04, mat("branco", brilho=2.0), centro=(0, -0.04, 0.15))
        cilindro(0.075, 0.14, (0.33, 0, -0.18), giro=(0, -45, 0), m=mat("dourado", metal=0.7, rugo=0.3), lados=8)
        cilindro(0.07, 0.6, (0.58, 0, -0.43), giro=(0, -45, 0), m=mat("madeira_escura", rugo=0.6), lados=8, chanfro=0.02)


def ic_o2():  # Garimpeiro: a pedra preciosa
    for (x, y, z, r) in ((-0.35, 0.15, -0.45, 0.3), (0.35, 0.2, -0.48, 0.28), (0.0, 0.3, -0.5, 0.35)):
        esfera(r, (x, y, z), mat("pedra_escura", rugo=0.9), sub=1, escala=(1.2, 1.0, 0.7))
    gema = mat("azul", metal=0.2, rugo=0.12, brilho=0.5)
    with grupo(pos=(0, -0.05, 0.05), giro=(-12, 0, 18)):
        revolucao([(0.0, -0.45), (0.5, 0.0), (0.5, 0.07), (0.32, 0.27), (0.0, 0.27)], gema, lados=8)
    faisca((0.4, -0.35, 0.38), 0.18)
    faisca((-0.42, -0.35, 0.18), 0.1)


def ic_o3():  # Abutre: a pena e o osso
    osso = mat("osso", rugo=0.6)
    with grupo(pos=(0, 0.12, 0), giro=(0, -40, 0)):
        cilindro(0.075, 1.05, giro=(0, 90, 0), m=osso, lados=7)
        for s in (-1, 1):
            for dz in (-0.07, 0.07):
                esfera(0.11, (s * 0.55, 0, dz), osso, sub=1)
    pena = []
    for i in range(13):
        t = i / 12
        w = 0.24 * math.sin(math.pi * t ** 0.8) + (0.035 if i % 2 else 0)
        pena.append((w, -0.6 + 1.25 * t))
    pena = [(0.02, -0.75)] + pena + [(-x * 0.85, z) for x, z in reversed(pena)] + [(-0.02, -0.75)]
    with grupo(pos=(0, -0.15, 0.0), giro=(0, 40, 0)):
        perfil(pena, 0.05, mat("aco_escuro", rugo=0.7), chanfro=0.008)
        perfil([(x * 0.7, z) for x, z in pena if z > 0.25], 0.06, mat("carvao", rugo=0.7))
        caixa((0.025, 0.07, 1.3), (0.0, 0, -0.07), m=mat("creme", rugo=0.6))


def ic_o4():  # Pé de coelho: o trevo de quatro folhas
    verde = mat("verde", rugo=0.5)
    verde2 = mat("cacto", rugo=0.5)
    tam = 0.55
    with grupo(giro=(25, 0, 0)):
        for k in range(4):
            a = 45 + 90 * k
            perfil([(x, z + 0.47 * tam - 0.03) for x, z in coracao(tam)], 0.08, verde if k % 2 else verde2,
                   giro=(0, -a + 90, 0), chanfro=0.015)
        tubo([(0, 0.02, 0), (0.08, 0.02, -0.3), (0.25, 0.02, -0.62)], 0.04, mat("verde_escuro", rugo=0.6), lados=5)
    faisca((0.5, -0.3, 0.5), 0.15, cor="amarelo")


def ic_o5():  # Faro: o radar
    with grupo(giro=(12, 0, 0)):
        cilindro(0.55, 0.1, giro=(90, 0, 0), m=mat("noite", rugo=0.4), lados=24)
        toro(0.55, 0.06, giro=(90, 0, 0), m=mat("aco", metal=0.6, rugo=0.35), seg=28, lados=6)
        tela = mat("verde", brilho=2.0)
        for r in (0.38, 0.2):
            toro(r, 0.012, (0, -0.06, 0), giro=(90, 0, 0), m=tela, seg=24, lados=4)
        for g in (0, 90):
            caixa((1.0, 0.01, 0.012), (0, -0.06, 0), giro=(0, g, 0), m=tela)
        setor = [(0, 0)] + [(0.5 * math.cos(math.radians(a)), 0.5 * math.sin(math.radians(a))) for a in range(20, 76, 8)]
        perfil(setor, 0.01, mat("verde", brilho=1.2, alfa=0.6), centro=(0, -0.065, 0))
        for (x, z, c) in ((0.22, 0.3, "laranja_vivo"), (-0.28, -0.12, "amarelo"), (0.12, -0.32, "laranja_vivo")):
            esfera(0.04, (x, -0.08, z), mat(c, brilho=3.0), sub=1)
        caixa((0.12, 0.12, 0.3), (0, 0.15, -0.65), m=mat("aco_escuro", metal=0.5))


# SOBREVIVÊNCIA: a Pesca

def ic_q1():  # Paciência: a ampulheta
    madeira = mat("madeira", rugo=0.6)
    for z in (-0.55, 0.55):
        cilindro(0.38, 0.09, (0, 0, z), m=madeira, lados=10, chanfro=0.015)
    for k in range(3):
        a = math.radians(90 + 120 * k)
        cilindro(0.03, 1.02, (0.33 * math.cos(a), 0.33 * math.sin(a), 0), m=mat("madeira_escura"), lados=6)
    revolucao([(0.0, -0.5), (0.27, -0.5), (0.26, -0.32), (0.06, -0.03), (0.06, 0.03), (0.26, 0.32), (0.27, 0.5),
               (0.0, 0.5)], mat("azul_claro", rugo=0.05, alfa=0.35, brilho=0.2), lados=12)
    areia = mat("amarelo", rugo=0.8, brilho=0.25)
    revolucao([(0.0, -0.49), (0.25, -0.49), (0.2, -0.36), (0.0, -0.24)], areia, lados=12)
    revolucao([(0.0, 0.08), (0.16, 0.24), (0.2, 0.3), (0.0, 0.3)], areia, lados=12)
    cilindro(0.012, 0.3, (0, 0, -0.12), m=areia, lados=4)


def ic_q2():  # Isca fina: o anzol com a minhoca
    aco = mat("prata", metal=0.8, rugo=0.25)
    pts = [(0.0, 0, 0.6)]
    pts += [(0.0, 0, 0.6 - 0.75 * i / 6) for i in range(1, 7)]
    for i in range(1, 11):
        a = math.radians(180 + 18 * i)
        pts.append((0.25 + 0.25 * math.cos(a), 0, -0.15 + 0.25 * math.sin(a)))
    pts.append((0.5, 0, 0.12))
    tubo(pts, 0.045, aco, lados=6)
    cilindro(0.045, 0.14, (0.5, 0, 0.19), m=aco, lados=6, r2=0.0)
    caixa((0.035, 0.045, 0.13), (0.45, 0, 0.13), giro=(0, 30, 0), m=aco)
    toro(0.07, 0.025, (0, 0, 0.68), giro=(90, 0, 0), m=aco, seg=10, lados=4)
    tubo([(0, 0, 0.75), (0.01, 0, 0.98)], 0.01, mat("branco", alfa=0.7), lados=3)
    minhoca = mat("minhoca", rugo=0.55)
    w = []
    for i in range(22):
        t = i / 21
        a = math.radians(180 + 160 * t)
        w.append((0.25 + 0.27 * math.cos(a) + 0.05 * math.sin(t * 14), -0.04 * math.cos(t * 14), -0.15 + 0.27 * math.sin(a)))
    tubo(w, 0.085, minhoca, lados=6, r_fim=0.05)
    tubo([(-0.02, 0, -0.1), (-0.14, -0.06, -0.32), (-0.06, 0, -0.52)], 0.08, minhoca, lados=6, r_fim=0.04)
    agua = mat("azul_claro", rugo=0.2, brilho=0.6, alfa=0.85)
    for (x, z, r) in ((0.62, -0.35, 0.05), (0.7, -0.18, 0.035)):
        esfera(r, (x, -0.1, z), agua, sub=1)


def ic_q3():  # Peixe grande: o tucunaré
    with grupo(giro=(0, -14, 0)):
        modelo(LOOT, ["PeixeGrande_corpo"], tam=1.3, giro=(90, 0, 12))
    agua = mat("azul_claro", rugo=0.2, brilho=0.6, alfa=0.85)
    for (x, z, r) in ((-0.62, -0.3, 0.05), (-0.72, -0.12, 0.035), (0.62, 0.32, 0.045), (0.7, 0.17, 0.03)):
        esfera(r, (x, -0.2, z), agua, sub=1)


def ic_q4():  # Noite de pesca: a lua sobre a água
    perfil(crescente(), 0.1, mat("creme", rugo=0.4, brilho=1.4), centro=(-0.3, 0.2, 0.55), giro=(0, -15, 0))
    for (x, z, t) in ((0.4, 0.75, 0.08), (0.15, 0.95, 0.05), (0.62, 0.5, 0.05)):
        faisca((x, 0.2, z), t, forca=2.5)
    cilindro(0.75, 0.04, (0, 0, -0.42), m=mat("azul_escuro", rugo=0.9, espec=0.0), lados=24)
    for r in (0.22, 0.38):
        toro(r, 0.012, (0.15, 0, -0.39), m=mat("azul_claro", brilho=0.8), seg=24, lados=4)
    modelo(os.path.join("Pesca", "Boia.fbx"), ["Boia"], tam=0.36, pos=(0.15, 0, -0.26))


def ic_q5():  # Mestre pescador: o dourado
    with grupo(giro=(0, -22, 0)):
        modelo(LOOT, ["PeixeDourado_corpo"], tam=1.3, giro=(90, 0, 12))
    for (x, z, t) in ((0.5, 0.45, 0.16), (-0.45, 0.4, 0.12), (0.1, 0.58, 0.09)):
        faisca((x, -0.25, z), t, cor="amarelo", forca=3.0)


# ----------------------------------------------------------------------------------------------------------------------
# a lista dos nós (a ordem do enum Habilidade): (id, enum, área, ramo, coluna, fileira, nome, função)

def _nos():
    C, R, S = "Combate", "Resistencia", "Sobrevivencia"
    f = globals()
    lista = [
        ("g1", "TiroForte", C, "Guerreiro", 0, 1, "Tiro forte"),
        ("g2", "RecargaRapida", C, "Guerreiro", 0, 2, "Recarga rápida"),
        ("g3", "BracoForte", C, "Guerreiro", 0, 3, "Braço forte"),
        ("g4", "MaoFirme", C, "Guerreiro", 0, 4, "Mão firme"),
        ("g5", "MiraDeAco", C, "Guerreiro", 0, 5, "Mira de aço"),
        ("a1", "PistolaDeSucata", C, "Armeiro de sucata", 2, 1, "Pistola de sucata"),
        ("a2", "RifleDeSucata", C, "Armeiro de sucata", 1, 2, "Rifle de precisão de sucata"),
        ("a3", "SubmetralhadoraDeSucata", C, "Armeiro de sucata", 3, 2, "Submetralhadora de sucata"),
        ("a4", "CanoReforcado", C, "Armeiro de sucata", 1, 3, "Cano reforçado"),
        ("a5", "MirasCaseiras", C, "Armeiro de sucata", 2, 3, "Miras caseiras"),
        ("a6", "CarregadorEstendido", C, "Armeiro de sucata", 3, 3, "Carregador estendido"),
        ("a7", "LunetaDeCano", C, "Armeiro de sucata", 1, 4, "Luneta de cano"),
        ("a8", "MolaNova", C, "Armeiro de sucata", 2, 4, "Mola nova"),
        ("a9", "SilenciadorCaseiro", C, "Armeiro de sucata", 3, 4, "Silenciador caseiro"),
        ("a10", "MestreArmeiro", C, "Armeiro de sucata", 2, 5, "Mestre armeiro"),
        ("s1", "PassosLeves", C, "Assassino", 4, 1, "Passos leves"),
        ("s2", "PelasCostas", C, "Assassino", 4, 2, "Pelas costas"),
        ("s3", "Discreto", C, "Assassino", 4, 3, "Discreto"),
        ("s4", "TiroAbafado", C, "Assassino", 4, 4, "Tiro abafado"),
        ("s5", "SangueFrio", C, "Assassino", 4, 5, "Sangue frio"),
        ("l1", "Borracheiro", C, "Ladrão", 5, 1, "Borracheiro"),
        ("l2", "MaosLeves", C, "Ladrão", 5, 2, "Mãos leves"),
        ("l3", "Chaveiro", C, "Ladrão", 5, 3, "Chaveiro"),
        ("l4", "AlarmeMudo", C, "Ladrão", 5, 4, "Alarme mudo"),
        ("l5", "LigacaoDireta", C, "Ladrão", 5, 5, "Ligação direta"),
        ("f1", "TochaDuradoura", R, "Fogo", 0.5, 1, "Tocha duradoura"),
        ("f2", "Lampiao", R, "Fogo", 0, 2, "Lampião"),
        ("f3", "CoquetelMolotov", R, "Fogo", 1, 2, "Coquetel molotov"),
        ("f4", "FogueiraPortatil", R, "Fogo", 0, 3, "Fogueira portátil"),
        ("f5", "FogoQuePega", R, "Fogo", 1, 3, "Fogo que pega"),
        ("f6", "CalorDoFogo", R, "Fogo", 0, 4, "Calor do fogo"),
        ("f7", "MuralhaDeFogo", R, "Fogo", 0.5, 5, "Muralha de fogo"),
        ("h1", "MaosDeEnfermeiro", R, "Saúde", 2, 1, "Mãos de enfermeiro"),
        ("h2", "CurativoBemFeito", R, "Saúde", 2, 2, "Curativo bem feito"),
        ("h3", "Imunidade", R, "Saúde", 2, 3, "Imunidade"),
        ("h4", "NervosDeAco", R, "Saúde", 2, 4, "Nervos de aço"),
        ("h5", "MedicoDeCampo", R, "Saúde", 2, 5, "Médico de campo"),
        ("n1", "EstomagoForte", R, "Fome e sede", 3, 1, "Estômago forte"),
        ("n2", "Cantil", R, "Fome e sede", 3, 2, "Cantil"),
        ("n3", "Folego", R, "Fome e sede", 3, 3, "Fôlego"),
        ("n4", "BemNutrido", R, "Fome e sede", 3, 4, "Bem nutrido"),
        ("n5", "Camelo", R, "Fome e sede", 3, 5, "Camelo"),
        ("u1", "Cuidadoso", R, "Recursos", 4, 1, "Cuidadoso"),
        ("u2", "Economico", R, "Recursos", 4, 2, "Econômico"),
        ("u3", "Farmaceutico", R, "Recursos", 4, 3, "Farmacêutico"),
        ("u4", "Reparador", R, "Recursos", 4, 4, "Reparador"),
        ("u5", "Remendao", R, "Recursos", 4, 5, "Remendão"),
        ("p1", "CapaceteDePanela", R, "Proteção de sucata", 5, 1, "Capacete de panela"),
        ("p2", "BracadeirasECaneleiras", R, "Proteção de sucata", 5, 2, "Braçadeiras e caneleiras"),
        ("p3", "ColeteDeChapa", R, "Proteção de sucata", 5, 3, "Colete de chapa"),
        ("p4", "RemendoDeChapa", R, "Proteção de sucata", 5, 4, "Remendo de chapa"),
        ("p5", "Couraca", R, "Proteção de sucata", 5, 5, "Couraça"),
        ("k1", "Sucateiro", S, "Craft", 0, 1, "Sucateiro"),
        ("k2", "FazTudo", S, "Craft", 0, 2, "Faz-tudo"),
        ("k3", "Artesao", S, "Craft", 0, 3, "Artesão"),
        ("k4", "MaosHabeis", S, "Craft", 0, 4, "Mãos hábeis"),
        ("k5", "Armeiro", S, "Craft", 0, 5, "Armeiro"),
        ("t1", "AlarmeDeLatas", S, "Armadilhas e defesas", 1.5, 1, "Alarme de latas"),
        ("t2", "BarricadaDeTabuas", S, "Armadilhas e defesas", 1, 2, "Barricada de tábuas"),
        ("t3", "TapeteDePregos", S, "Armadilhas e defesas", 2, 2, "Tapete de pregos"),
        ("t4", "Estacas", S, "Armadilhas e defesas", 1, 3, "Estacas"),
        ("t5", "Armadilheiro", S, "Armadilhas e defesas", 2, 3, "Armadilheiro"),
        ("t6", "CercaDeFogo", S, "Armadilhas e defesas", 1.5, 4, "Cerca de fogo"),
        ("t7", "Fortaleza", S, "Armadilhas e defesas", 1.5, 5, "Fortaleza"),
        ("o1", "OlhoBom", S, "Sortudo", 3, 1, "Olho bom"),
        ("o2", "Garimpeiro", S, "Sortudo", 3, 2, "Garimpeiro"),
        ("o3", "Abutre", S, "Sortudo", 3, 3, "Abutre"),
        ("o4", "PeDeCoelho", S, "Sortudo", 3, 4, "Pé de coelho"),
        ("o5", "Faro", S, "Sortudo", 3, 5, "Faro"),
        ("q1", "Paciencia", S, "Pesca", 4, 1, "Paciência"),
        ("q2", "IscaFina", S, "Pesca", 4, 2, "Isca fina"),
        ("q3", "PeixeGrande", S, "Pesca", 4, 3, "Peixe grande"),
        ("q4", "NoiteDePesca", S, "Pesca", 4, 4, "Noite de pesca"),
        ("q5", "MestrePescador", S, "Pesca", 4, 5, "Mestre pescador"),
    ]
    return [n + (f.get("ic_" + n[0]),) for n in lista]


NOS = _nos()


# ----------------------------------------------------------------------------------------------------------------------
# as folhas de conferência (com o Pillow, fora do Blender)

def folhas(pasta):
    from PIL import Image, ImageDraw, ImageFont

    def fonte(t):
        for f in ("C:/Windows/Fonts/segoeuib.ttf", "C:/Windows/Fonts/arialbd.ttf"):
            if os.path.exists(f):
                return ImageFont.truetype(f, t)
        return ImageFont.load_default()

    nomes = {"Combate": "Combate", "Resistencia": "Resistencia", "Sobrevivencia": "Sobrevivencia"}
    cel, larg, alt, topo = 128, 168, 128 + 48, 64
    for area in AREAS:
        nos = [n for n in NOS if n[2] == area]
        colunas = max(n[4] for n in nos) + 1
        w = int(colunas * larg) + 40
        h = topo + 5 * alt + 20
        cor = tuple(int(c * 255) for c in AREAS[area])
        folha = Image.new("RGB", (w, h), (66, 68, 64))
        d = ImageDraw.Draw(folha)
        ramos = {}
        for n in nos:
            ramos.setdefault(n[3], []).append(n[4])
        for ramo, cols in ramos.items():
            x0 = 20 + min(cols) * larg
            x1 = 20 + (max(cols) + 1) * larg
            d.text(((x0 + x1) / 2, 30), ramo.upper(), fill=(235, 235, 230), font=fonte(17), anchor="mm")
        for n in nos:
            x = 20 + n[4] * larg + (larg - cel) / 2
            y = topo + (n[5] - 1) * alt
            arq = os.path.join(pasta, "Hab_%s.png" % n[0])
            if os.path.exists(arq):
                folha.paste(Image.open(arq).convert("RGB").resize((cel, cel), Image.LANCZOS), (int(x), int(y)))
            d.rectangle([x - 2, y - 2, x + cel + 1, y + cel + 1], outline=cor, width=2)
            d.text((x + cel / 2, y + cel + 14), n[6], fill=(240, 240, 235), font=fonte(13), anchor="mm")
        folha.save(os.path.join(pasta, "Folha_Icones_%s.png" % nomes[area]))
    # a prova pequena: todos em 64 x 64 (e em 40, o tamanho mais apertado), lado a lado
    for lado in (64, 40):
        por = 15
        linhas = (len(NOS) + por - 1) // por
        prova = Image.new("RGB", (por * (lado + 6) + 6, linhas * (lado + 6) + 6), (66, 68, 64))
        for i, n in enumerate(NOS):
            arq = os.path.join(pasta, "Hab_%s.png" % n[0])
            if os.path.exists(arq):
                prova.paste(Image.open(arq).convert("RGB").resize((lado, lado), Image.LANCZOS),
                            (6 + (i % por) * (lado + 6), 6 + (i // por) * (lado + 6)))
        prova.save(os.path.join(pasta, "Prova_%d.png" % lado))


# ----------------------------------------------------------------------------------------------------------------------
# a entrada

def _argumentos():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    pasta, ids, i = None, [], 0
    while i < len(args):
        if args[i] == "--saida":
            pasta = args[i + 1]
            i += 2
            continue
        ids.append(args[i])
        i += 1
    if pasta is None:
        raise SystemExit("Passe a pasta dos PNG: --saida <pasta>")
    os.makedirs(pasta, exist_ok=True)
    return pasta, ids


def principal():
    pasta, ids = _argumentos()
    if not TEM_BLENDER:
        folhas(pasta)
        return
    ids = [i for i in ids if i != "folhas"]
    _cena()
    feitos = 0
    for no in NOS:
        if ids and no[0] not in ids:
            continue
        try:
            fazer(no, pasta)
            feitos += 1
            print("ICONE ok", no[0])
        except Exception:
            import traceback
            traceback.print_exc()
            print("ICONE ERRO", no[0])
            # limpa o que sobrou do ícone que falhou
            col = COLECAO[0]
            if col is not None and col.name in bpy.data.collections:
                for o in list(col.objects):
                    bpy.data.objects.remove(o)
                bpy.data.collections.remove(col)
    print("ICONES feitos:", feitos)


if __name__ == "__main__":
    principal()
