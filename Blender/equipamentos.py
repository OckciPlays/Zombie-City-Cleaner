# As mochilas (pequena, média e grande) e o coldre: os modelos, o Equipamentos.fbx e os ícones e as fotos do inventário.
#
# Como rodar (Blender 5.2, numa cena qualquer):
#   import importlib, sys; sys.path.append(r"<repo>\Blender"); import equipamentos; importlib.reload(equipamentos)
#   equipamentos.construir()    # refaz os 4 modelos na cena "Equipamentos"
#   equipamentos.exportar()     # grava Assets/_Game/Art/Characters/Jogador/Equipamentos.fbx
#   equipamentos.imagens()      # grava Icone_<nome>.png (128 x 128) e Foto_<nome>.png (600 x 500) em Assets/_Game/Art/UI/Icones
# Depois, na Unity: Zombie City Cleaner > Montar armas.
#
# O formato é o do Itens.fbx: cada peça é um objeto girado 90° em X, com a malha guardada nos eixos da Unity (o alto em
# +Y, a frente de quem veste em +Z; a Unity espelha o X). Aqui o modelo é feito nos eixos do Blender (o alto em +Z, a
# frente de quem veste em -Y), e o giro é posto no fim.
# - Mochilas: a origem no meio das costas de quem veste (a face da mochila que encosta nelas), na altura do meio da
#   mochila; o corpo dela vai para trás (+Y no Blender, -Z na Unity).
# - Coldre: a origem no passador do cinto, em cima; a arma entra de cima para baixo, com o cabo para trás.
# Cores: blocos sólidos pela Paleta.png (só cores que já existem nela), como as armas.

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(AQUI)
UNITY = os.path.join(REPO, "Assets", "_Game", "Art", "Characters", "Jogador")
ICONES = os.path.join(REPO, "Assets", "_Game", "Art", "UI", "Icones")
PALETA = os.path.join(REPO, "Assets", "_Game", "Art", "Paleta.png")

NOMES = ["MochilaPequena", "MochilaMedia", "MochilaGrande", "Coldre"]

# (linha da imagem, de cima para baixo; coluna) de cada cor na Paleta.png
CORES = {
    "oliva": (4, 0), "oliva_escuro": (7, 9),
    "caqui": (8, 10), "caqui_escuro": (6, 11),
    "couro": (3, 7), "couro_escuro": (8, 4), "couro_claro": (3, 8),
    "nylon": (6, 6), "nylon_escuro": (5, 13),
    "preto": (3, 2), "metal": (8, 8),
    "saco": (5, 9), "laranja": (6, 9),
}


def _uv(cor):
    linha, coluna = CORES[cor]
    return ((coluna + 0.5) / 16.0, 1.0 - (linha + 0.5) / 16.0)


def _material():
    m = bpy.data.materials.get("Pistola")
    if m is None:
        m = bpy.data.materials.new("Pistola")
    m.use_nodes = True
    nt = m.node_tree
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None)
    if tex is None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    img = bpy.data.images.get("Paleta.png") or bpy.data.images.load(PALETA)
    # o alfa da paleta é o brilho da cidade, não transparência (na Unity o material das armas é opaco)
    img.alpha_mode = "NONE"
    tex.image = img
    tex.interpolation = "Closest"
    return m


def _caixa(bm, uv, cor, tam, centro, giro=(0, 0, 0), chanfro=0.006):
    """Uma caixa de tam (x, y, z) no centro, girada (graus, X Y Z), com as arestas chanfradas."""
    r = bmesh.ops.create_cube(bm, size=1.0)
    vs = r["verts"]
    m = Matrix.Translation(Vector(centro)) @ Matrix.Rotation(math.radians(giro[2]), 4, "Z") @ \
        Matrix.Rotation(math.radians(giro[1]), 4, "Y") @ Matrix.Rotation(math.radians(giro[0]), 4, "X") @ \
        Matrix.Diagonal((tam[0], tam[1], tam[2], 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=vs)
    faces = list({f for v in vs for f in v.link_faces})
    # pinta antes de chanfrar: as faces do chanfro copiam o UV das vizinhas (a mesma cor)
    _pintar(bm, uv, faces, cor)
    if chanfro > 0 and min(tam) > chanfro * 2.5:
        arestas = list({e for f in faces for e in f.edges})
        bmesh.ops.bevel(bm, geom=vs + arestas, offset=chanfro, segments=1, affect="EDGES", profile=0.5)


def _cilindro(bm, uv, cor, raio, comprimento, centro, eixo="X", lados=8):
    r = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=lados, radius1=raio, radius2=raio, depth=comprimento)
    vs = r["verts"]
    giro = Matrix.Rotation(math.radians(90), 4, "Y") if eixo == "X" else Matrix.Rotation(math.radians(90), 4, "X") if eixo == "Y" else Matrix.Identity(4)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(centro)) @ giro, verts=vs)
    faces = list({f for v in vs for f in v.link_faces})
    _pintar(bm, uv, faces, cor)


def _pintar(bm, uv, faces, cor):
    u, v = _uv(cor)
    for f in faces:
        for l in f.loops:
            l[uv].uv = (u, v)


def _objeto(nome, pecas, cena):
    """Junta as peças num objeto (nome_corpo), no formato do Itens.fbx."""
    me = bpy.data.meshes.new(nome + "_corpo")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for p in pecas:
        p[0](bm, uv, *p[1:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    for f in me.polygons:
        f.use_smooth = False
    me.materials.append(_material())
    # a malha nos eixos da Unity (o alto em +Y), e o objeto girado 90° em X, como os do Itens.fbx
    me.transform(Matrix.Rotation(math.radians(-90), 4, "X"))
    o = bpy.data.objects.new(nome + "_corpo", me)
    o.rotation_euler = (math.radians(90), 0, 0)
    cena.collection.objects.link(o)
    return o


def _alcas(altura, x=0.075, preto="preto"):
    """As duas alças de ombro (na frente da mochila, do lado de quem veste) e onde elas entram em cima."""
    pecas = []
    for s in (-1, 1):
        pecas.append((_caixa, preto, (0.045, 0.016, altura), (s * x, -0.012, -0.01)))
        pecas.append((_caixa, preto, (0.045, 0.05, 0.016), (s * x, 0.008, altura / 2 - 0.012)))
        pecas.append((_caixa, "metal", (0.05, 0.02, 0.022), (s * x, -0.022, -altura * 0.3)))
    return pecas


def _pequena():
    # uma mochila de dia, de lona verde-oliva: o corpo, a tampa arredondada, o bolso da frente com o zíper e a alça de mão
    return [
        (_caixa, "oliva", (0.28, 0.13, 0.36), (0, 0.065, 0)),
        (_caixa, "oliva_escuro", (0.26, 0.12, 0.045), (0, 0.065, 0.195)),
        (_caixa, "oliva_escuro", (0.2, 0.045, 0.15), (0, 0.152, -0.07)),
        (_caixa, "preto", (0.2, 0.008, 0.01), (0, 0.176, 0.0), (0, 0, 0), 0),
        (_caixa, "preto", (0.07, 0.03, 0.016), (0, 0.06, 0.225)),
    ] + _alcas(0.34)


def _media():
    # a mochila militar de lona cáqui: a aba por cima, presa por duas tiras de couro com fivela, os bolsos dos lados e da
    # frente e o fundo de couro
    pecas = [
        (_caixa, "caqui", (0.32, 0.17, 0.46), (0, 0.085, 0)),
        (_caixa, "caqui_escuro", (0.33, 0.18, 0.03), (0, 0.085, 0.245)),
        (_caixa, "caqui_escuro", (0.3, 0.02, 0.17), (0, 0.18, 0.165)),
        (_caixa, "caqui_escuro", (0.22, 0.05, 0.16), (0, 0.195, -0.1)),
        (_caixa, "couro", (0.33, 0.18, 0.03), (0, 0.085, -0.235)),
    ]
    for s in (-1, 1):
        pecas.append((_caixa, "couro", (0.035, 0.012, 0.21), (s * 0.08, 0.195, 0.13)))
        pecas.append((_caixa, "metal", (0.045, 0.014, 0.03), (s * 0.08, 0.2, 0.04)))
        pecas.append((_caixa, "caqui_escuro", (0.05, 0.12, 0.2), (s * 0.185, 0.085, -0.08)))
    return pecas + _alcas(0.44, 0.085)


def _grande():
    # a mochila de trilha, de nylon cinza-azulado: alta (passa da cabeça), com a tampa, o bolso grande da frente, os
    # bolsos dos lados, as tiras de compressão, o cinto da cintura e o saco de dormir amarrado embaixo
    pecas = [
        (_caixa, "nylon", (0.36, 0.22, 0.62), (0, 0.11, 0)),
        (_caixa, "nylon_escuro", (0.34, 0.22, 0.08), (0, 0.12, 0.33)),
        (_caixa, "nylon_escuro", (0.26, 0.06, 0.3), (0, 0.245, -0.06)),
        (_caixa, "laranja", (0.05, 0.012, 0.03), (0, 0.235, 0.33)),
        (_caixa, "preto", (0.46, 0.035, 0.06), (0, -0.02, -0.27)),
        (_caixa, "metal", (0.05, 0.02, 0.04), (0, -0.04, -0.27)),
        (_cilindro, "saco", 0.075, 0.42, (0, 0.13, -0.385)),
    ]
    for s in (-1, 1):
        pecas.append((_caixa, "nylon_escuro", (0.06, 0.16, 0.26), (s * 0.21, 0.11, -0.12)))
        pecas.append((_caixa, "preto", (0.008, 0.2, 0.025), (s * 0.183, 0.11, 0.12), (0, 0, 0), 0))
        pecas.append((_caixa, "preto", (0.008, 0.2, 0.025), (s * 0.183, 0.11, 0.02), (0, 0, 0), 0))
        pecas.append((_caixa, "preto", (0.03, 0.17, 0.17), (s * 0.12, 0.13, -0.385)))
    return pecas + _alcas(0.52, 0.09)


def _coldre():
    # um coldre de couro de cintura para pistola: a bainha (o cano para baixo), o volume do guarda-mato, a tira com o
    # botão por cima do cabo e o passador do cinto por trás
    return [
        (_caixa, "couro", (0.036, 0.06, 0.17), (0, 0.0, -0.1)),
        (_caixa, "couro", (0.04, 0.095, 0.06), (0, -0.018, -0.035)),
        (_caixa, "couro_escuro", (0.046, 0.022, 0.03), (0, 0.035, 0.0)),
        (_caixa, "metal", (0.05, 0.012, 0.012), (0, 0.035, 0.0), (0, 0, 0), 0),
        (_caixa, "couro_escuro", (0.008, 0.07, 0.08), (-0.024, 0.0, -0.025), (0, 0, 0), 0.002),
        (_caixa, "couro_claro", (0.038, 0.062, 0.012), (0, 0.0, -0.188), (0, 0, 0), 0.003),
    ]


MODELOS = {"MochilaPequena": _pequena, "MochilaMedia": _media, "MochilaGrande": _grande, "Coldre": _coldre}


def _cena():
    cena = bpy.data.scenes.get("Equipamentos") or bpy.data.scenes.new("Equipamentos")
    bpy.context.window.scene = cena
    return cena


def construir():
    cena = _cena()
    for o in list(cena.objects):
        if o.name.split("_")[0] in NOMES:
            bpy.data.objects.remove(o, do_unlink=True)
    objs = []
    for i, nome in enumerate(NOMES):
        o = _objeto(nome, MODELOS[nome](), cena)
        o.location = (i * 0.7, 0, 0)
        objs.append(o)
    return objs


def exportar():
    cena = _cena()
    for o in cena.objects:
        o.select_set(o.name.split("_")[0] in NOMES)
    bpy.ops.export_scene.fbx(filepath=os.path.join(UNITY, "Equipamentos.fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")


def _camera(cena, orto, alvo, direcao, distancia, escala):
    cam = bpy.data.objects.get("CameraDosIcones")
    if cam is None:
        cam = bpy.data.objects.new("CameraDosIcones", bpy.data.cameras.new("CameraDosIcones"))
    if cam.name not in cena.objects:
        cena.collection.objects.link(cam)
    cam.data.type = "ORTHO" if orto else "PERSP"
    cam.data.ortho_scale = escala
    cam.data.lens = 50
    d = Vector(direcao).normalized()
    cam.location = Vector(alvo) + d * distancia
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cena.camera = cam
    return cam


def imagens():
    """Os ícones (128 x 128, quase de frente) e as fotos (600 x 500, de 3/4), no estilo dos do itens.py: o Workbench com
    a textura, a cavidade, o contorno e o fundo transparente."""
    cena = _cena()
    r = cena.render
    try:
        r.engine = "BLENDER_WORKBENCH"
    except TypeError:
        pass
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
    objs = {o.name.split("_")[0]: o for o in cena.objects if o.name.split("_")[0] in NOMES}
    for nome, o in objs.items():
        for outro in objs.values():
            outro.hide_render = outro is not o
        cantos = [o.matrix_world @ Vector(c) for c in o.bound_box]
        centro = sum(cantos, Vector()) / 8
        tam = max((max(c[i] for c in cantos) - min(c[i] for c in cantos)) for i in range(3))
        # o lado de fora da mochila (atrás de quem veste) fica virado para a câmera; o coldre, de lado
        if nome == "Coldre":
            icone, foto = (1.0, -0.35, 0.25), (1.0, -0.8, 0.45)
        else:
            icone, foto = (0.35, 1.0, 0.25), (0.8, 1.0, 0.45)
        r.resolution_x, r.resolution_y = 128, 128
        _camera(cena, True, centro, icone, 3.0, tam * 1.4)
        r.filepath = os.path.join(ICONES, f"Icone_{nome}.png")
        bpy.ops.render.render(write_still=True)
        r.resolution_x, r.resolution_y = 600, 500
        _camera(cena, False, centro, foto, tam * 2.6, tam)
        r.filepath = os.path.join(ICONES, f"Foto_{nome}.png")
        bpy.ops.render.render(write_still=True)
    for o in objs.values():
        o.hide_render = False
