# As armas estilizadas (v2, 30/10/2026, a pedido: "eu queria apenas uma melhoria das anteriores, porém parece que só
# ficou mais realista, não é o tema do jogo"): o rifle, a espingarda e a sniper refeitos a partir das próprias armas
# antigas do jogo (as do Gustavo), low poly como elas, com a mesma identidade (as formas, o tamanho e as cores: o rifle
# preto, a espingarda e a sniper com a madeira), melhoradas: as quinas chanfradas, alguns detalhes no mesmo estilo de
# blocos e uma paleta própria de cores chapadas (com o metal e a lisura de cada cor), sem textura assada nem normal map.
#
# A base é a arma antiga dos braços de primeira pessoa (a pose de repouso), levada para o espaço da arma solta e
# separada pelos ossos (o corpo no "gun", o ferrolho no "slide", o carregador no "mag", a telha no "pump", o cartucho no
# "shell"): a arma nova fica exatamente onde a antiga estava nas mãos, e as animações seguram igual.
#
# A paleta: uma imagem de 4 x 4 cores (Assets/_Game/Art/Armas/Texturas/Armas_Paleta_Cor.png) e a do metal/lisura
# (Armas_Paleta_MS.png); o UV de cada face aponta para o meio da cor dela. No .blend, cada cor é um material (Arma2_<cor>):
# para trocar a cor de uma face, troque o material dela e exporte de novo (o UV sai do material).
#
# Como rodar (num Blender em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/Armas/rodar_v2.py -- <rifle|espingarda|sniper> tudo
# (as etapas: construir, exportar, fotos, imagens, rascunho).

import math
import os

import bmesh
import bpy
from mathutils import Vector

import armas

# (nome, cor sRGB, metal, lisura)
PALETA = [
    ("preto", (30, 31, 34), 0.25, 0.35),
    ("preto_fosco", (37, 38, 41), 0.05, 0.25),
    ("grafite", (49, 52, 57), 0.60, 0.45),
    ("metal", (72, 76, 82), 0.80, 0.50),
    ("aco", (132, 136, 142), 0.90, 0.60),
    ("aco_claro", (176, 179, 183), 0.95, 0.65),
    ("madeira_escura", (86, 55, 37), 0.0, 0.35),
    ("madeira", (114, 76, 51), 0.0, 0.40),
    ("branco", (214, 209, 190), 0.0, 0.40),
    ("vermelho", (128, 36, 30), 0.0, 0.45),
    ("latao", (170, 136, 72), 0.90, 0.60),
    ("lente", (38, 56, 84), 0.30, 0.92),
    ("borracha", (22, 22, 23), 0.0, 0.12),
    # a nogueira da Mosin (a sniper v3): sempre no fim, para não mudar as cores das outras
    ("nogueira", (152, 100, 56), 0.0, 0.38),
    # o polímero cinza fosco da M4 (o rifle v3: a coronha, o punho e o guarda-mão), também no fim
    ("polimero", (66, 68, 72), 0.0, 0.30),
]
NOMES = [p[0] for p in PALETA]
CELULAS = 4
TAM_PALETA = 64
# as cores da Paleta.png que as armas antigas usam, e a cor nova de cada uma
ANTIGAS = {
    (27, 28, 31): "preto", (32, 34, 37): "preto_fosco", (38, 40, 44): "grafite", (47, 50, 53): "grafite",
    (51, 54, 59): "metal", (63, 66, 70): "metal", (108, 112, 117): "aco", (107, 74, 54): "madeira_escura",
    (126, 91, 68): "madeira", (210, 203, 178): "branco", (138, 46, 38): "vermelho", (168, 138, 74): "latao",
    (58, 74, 106): "lente",
}
PALETA_ANTIGA = os.path.join(armas.REPO, "Assets", "_Game", "Art", "Paleta.png")


def _linear(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def celula(indice):
    """O meio da célula da cor no UV (a linha 0 embaixo)."""
    return ((indice % CELULAS + 0.5) / CELULAS, (indice // CELULAS + 0.5) / CELULAS)


def materiais():
    """Um material por cor (para editar no Blender e para as fotos): a cor, o metal e a aspereza dela."""
    mats = []
    for nome, cor, metal, lisura in PALETA:
        mat = bpy.data.materials.get("Arma2_" + nome) or bpy.data.materials.new("Arma2_" + nome)
        mat.use_nodes = True
        lin = tuple(_linear(c) for c in cor)
        mat.diffuse_color = (*lin, 1.0)
        b = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        b.inputs["Base Color"].default_value = (*lin, 1.0)
        b.inputs["Metallic"].default_value = metal
        b.inputs["Roughness"].default_value = 1.0 - lisura
        mats.append(mat)
    return mats


def gravar_paleta():
    """As duas imagens da paleta: a cor (sRGB) e o MS (o metal no R, a oclusão no G, a lisura no A)."""
    import numpy as np
    os.makedirs(armas.TEXTURAS, exist_ok=True)
    t = TAM_PALETA
    lado = t // CELULAS
    cor = np.zeros((t, t, 4), dtype=np.float32)
    ms = np.zeros((t, t, 4), dtype=np.float32)
    cor[..., 3] = 1.0
    ms[..., 1] = 1.0
    for i, (nome, c, metal, lisura) in enumerate(PALETA):
        x, y = i % CELULAS, i // CELULAS
        cor[y * lado:(y + 1) * lado, x * lado:(x + 1) * lado, :3] = [v / 255.0 for v in c]
        ms[y * lado:(y + 1) * lado, x * lado:(x + 1) * lado, 0] = metal
        ms[y * lado:(y + 1) * lado, x * lado:(x + 1) * lado, 3] = lisura
    for nome, px, e_cor in (("Armas_Paleta_Cor", cor, True), ("Armas_Paleta_MS", ms, False)):
        img = bpy.data.images.get(nome)
        if img is not None:
            bpy.data.images.remove(img)
        img = bpy.data.images.new(nome, t, t, alpha=True, float_buffer=False)
        if not e_cor:
            img.colorspace_settings.name = "Non-Color"
        img.pixels.foreach_set(px.ravel())
        img.filepath_raw = os.path.join(armas.TEXTURAS, nome + ".png")
        img.file_format = "PNG"
        img.save()
        bpy.data.images.remove(img)
    print("  paleta gravada")


def _cor_antiga(px, w, h, u, v):
    x = min(w - 1, max(0, int(u * w)))
    y = min(h - 1, max(0, int(v * h)))
    i = (y * w + x) * 4
    c = tuple(round(px[i + k] * 255) for k in range(3))
    if c in ANTIGAS:
        return ANTIGAS[c]
    # uma cor que a tabela não tem: a mais parecida
    return ANTIGAS[min(ANTIGAS, key=lambda a: sum((a[k] - c[k]) ** 2 for k in range(3)))]


def construir(cfg):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    mats = materiais()
    pal = bpy.data.images.load(PALETA_ANTIGA)
    pw, ph = pal.size
    ppx = list(pal.pixels)

    # 1) a arma antiga dos braços, peça por osso, com a cor de cada face, no espaço da arma solta
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(armas.JOGADOR, f"Jogador_FP_{cfg.MODELO}.fbx"))
    fp = [o for o in bpy.data.objects if o not in antes]
    bracos = next(o for o in fp if o.type == "MESH" and o.name.startswith("FP_Bracos"))
    me = bracos.data
    uv = me.uv_layers.active.data
    grupos = {g.index: g.name for g in bracos.vertex_groups}

    def osso(v):
        return grupos[max(v.groups, key=lambda g: g.weight).group] if len(v.groups) else ""

    mw = bracos.matrix_world.copy()
    da_arma = {i for p in me.polygons if p.material_index == 1 for i in p.vertices}
    destino = [mw @ me.vertices[i].co for i in da_arma if osso(me.vertices[i]) != "shell"]
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(armas.JOGADOR, cfg.FBX_ANTIGO))
    soltas = [o for o in bpy.data.objects if o not in antes]
    antigas = [o for o in soltas if o.type == "MESH" and o.name.startswith(cfg.MODELO + "_")]
    org = next(o for o in antigas if o.name.startswith(cfg.MODELO + "_corpo")).matrix_world.translation.copy()
    origem = [o.matrix_world @ v.co - org for o in antigas for v in o.data.vertices]
    M, erro = armas.encaixe(destino, origem)
    para = M.inverted() @ mw
    print(f"  encaixe da antiga nos braços: {erro * 1000:.2f} mm")

    por_peca = {p: bmesh.new() for p in cfg.PECAS}
    mapas = {p: {} for p in cfg.PECAS}
    for p in me.polygons:
        if p.material_index != 1:
            continue
        peca = cfg.OSSOS.get(osso(me.vertices[p.vertices[0]]))
        if peca is None:
            continue
        bm, mapa = por_peca[peca], mapas[peca]
        vs = []
        for i in p.vertices:
            if i not in mapa:
                mapa[i] = bm.verts.new(para @ me.vertices[i].co)
            vs.append(mapa[i])
        try:
            f = bm.faces.new(vs)
        except ValueError:
            continue
        us = [uv[k].uv for k in p.loop_indices]
        nome = _cor_antiga(ppx, pw, ph, sum(x[0] for x in us) / len(us), sum(x[1] for x in us) / len(us))
        if hasattr(cfg, "cor_da_face"):
            # a cfg pode trocar a cor de uma face pela posição (por exemplo, a soleira de borracha)
            nome = cfg.cor_da_face(peca, f.calc_center_median(), f.normal, nome)
        f.material_index = NOMES.index(nome)
    for o in fp + soltas:
        if o.name in bpy.data.objects:
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.images.remove(pal)

    # 2) os detalhes novos (no mesmo estilo de blocos)
    if hasattr(cfg, "extras"):
        cfg.extras({p: armas.Extras(bm, NOMES) for p, bm in por_peca.items()})

    # 3) as quinas chanfradas, as faces planas e o UV na paleta
    col = armas._colecao(cfg.NOME)
    objs = []
    for peca, bm in por_peca.items():
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        arestas = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(cfg.ANGULO_CHANFRO)]
        if arestas and cfg.CHANFRO > 0:
            bmesh.ops.bevel(bm, geom=arestas, offset=cfg.CHANFRO, segments=1, profile=0.5, affect="EDGES",
                            clamp_overlap=True)
        bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
        nome = f"{cfg.NOME}_{peca}"
        velho = bpy.data.objects.get(nome)
        if velho is not None:
            bpy.data.objects.remove(velho, do_unlink=True)
        m = bpy.data.meshes.new(nome)
        bm.to_mesh(m)
        bm.free()
        for p in m.polygons:
            p.use_smooth = False
        for mat in mats:
            m.materials.append(mat)
        uvs = m.uv_layers.new(name="UVMap")
        uv_da_cor(m)
        o = bpy.data.objects.new(nome, m)
        col.objects.link(o)
        objs.append(o)
        print(f"  {nome}: {len(m.polygons)} triângulos")
    print("  total:", sum(len(o.data.polygons) for o in objs), "triângulos")
    return objs


def uv_da_cor(m):
    """O UV de cada face no meio da célula da cor dela (o material da face)."""
    uv = m.uv_layers.active.data
    for p in m.polygons:
        u, v = celula(p.material_index)
        for k in p.loop_indices:
            uv[k].uv = (u, v)


def material_paleta(cfg):
    """O material que vai para o FBX (<Nome>_Paleta): a paleta (sem filtro) num BSDF."""
    nome = f"{cfg.NOME}_Paleta"
    mat = bpy.data.materials.get(nome) or bpy.data.materials.new(nome)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    saida = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], saida.inputs["Surface"])
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(os.path.join(armas.TEXTURAS, "Armas_Paleta_Cor.png"), check_existing=True)
    t.interpolation = "Closest"
    nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def exportar(cfg):
    """O <Nome>.fbx (como o das armas novas): as peças com o UV refeito pela cor de cada face e o material da paleta."""
    import math as _m
    from mathutils import Matrix
    os.makedirs(armas.MODELOS, exist_ok=True)
    gravar_paleta()
    mat = material_paleta(cfg)
    giro = Matrix.Rotation(_m.radians(-90), 4, "X")
    originais = armas.objetos(cfg)
    copias = []
    for o in originais:
        uv_da_cor(o.data)
        me = o.data.copy()
        me.transform(giro)
        me.materials.clear()
        me.materials.append(mat)
        for p in me.polygons:
            p.material_index = 0
        c = bpy.data.objects.new(o.name + "_exportar", me)
        c.rotation_euler = (_m.radians(90), 0, 0)
        bpy.context.scene.collection.objects.link(c)
        copias.append(c)
    for o in originais:
        o.name = o.name + "_autoria"
    for c in copias:
        c.name = c.name.replace("_exportar", "")
    armas._selecionar(copias)
    caminho = os.path.join(armas.MODELOS, f"{cfg.NOME}.fbx")
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="OFF",
                             use_tspace=False, path_mode="STRIP")
    for c in copias:
        me = c.data
        bpy.data.objects.remove(c, do_unlink=True)
        bpy.data.meshes.remove(me)
    for o in originais:
        o.name = o.name.replace("_autoria", "")
    print("  exportado:", caminho)
