# A sniper v3 (30/10/2026, a pedido: "o modelo que eu quero que você faça é uma mosin (...) pegue a cor, o modelo, e siga
# fiel, o scope está excelente do jeito que está"): a Mosin-Nagant que o Pedro mandou (Fontes/mosin-nagant.blend), fiel à
# forma e às cores dela (a nogueira, o metal preto, o ferrolho de aço), com a luneta da sniper v2 (a da antiga, chanfrada,
# com as lentes e a ocular de borracha), na mesma altura de antes, presa num trilho-ponte preto sobre o receptor: preso
# só na frente (no anel do receptor), por cima do caminho do ferrolho. Depois (30/10/2026, a pedido: "o scope dela está
# bem pra trás ainda"), a luneta passou a ser a "scout" da sniper v1 (luneta_scout), à frente do receptor.
#
# O encaixe é o da Mosin da v1 (arma_sniper.py): a escala 0,096 (o tamanho real), a boca do cano no ponto da antiga, o
# gatilho logo à frente da mão direita; a caixa do carregador sai na recarga. As cores são as da paleta das estilizadas
# (estilizadas.py), sem textura assada; as faces lisas até 30° e as quinas vivas acima.
#
# Como rodar: blender -b --factory-startup --disable-autoexec --python Blender/Armas/rodar_v2.py -- v3_mosin tudo

import math
import os

import bmesh
import bpy
from mathutils import Vector

import armas
import arma_sniper as v1
import estilizadas

NOME = "Sniper3"
FOTO = "sniper_v3"
MODELO = "Sniper"
FBX_ANTIGO = "Armas_Sniper.fbx"
PECAS = ["corpo", "ferrolho", "carregador"]
ICONE = (896, 256)
OBJETOS = v1.OBJETOS
# os materiais da Mosin e a cor de cada um na paleta
CORES = {"h-190": "preto", "stell": "aco", "wood (walnut)": "nogueira", "copper": "latao"}
Z_CORTE = 0.142            # a luneta da v2: tudo acima disto (o receptor antigo terminava em 0,131)
# a luneta vai para a frente (30/10/2026, a pedido: "a luneta está muito atrás em relação à arma"): centrada sobre o
# receptor da Mosin (de y -0,25 a -0,02), com a ocular um pouco atrás dele
AVANCO_DA_LUNETA = 0.08
BLEND_V2 = os.path.join(armas.AQUI, "Sniper2.blend")
ANGULO_LISO = 30


def _luneta():
    """A luneta da v2: o corpo da Sniper2 cortado em Z_CORTE (só o que fica acima), com as cores dela."""
    with bpy.data.libraries.load(BLEND_V2, link=False) as (src, dst):
        dst.meshes = ["Sniper2_corpo"]
    me = dst.meshes[0]
    bm = bmesh.new()
    bm.from_mesh(me)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, Z_CORTE), plane_no=(0, 0, 1), clear_inner=True)
    longe = [f for f in bm.faces if not all(-0.34 < v.co.y < 0.22 for v in f.verts)]
    bmesh.ops.delete(bm, geom=longe, context="FACES")
    soltos = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=soltos, context="VERTS")
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0.0, -AVANCO_DA_LUNETA, 0.0))
    corte = [v.co.y for v in bm.verts if abs(v.co.z - Z_CORTE) < 1e-4]
    bpy.data.meshes.remove(me)
    return bm, (min(corte), max(corte)) if corte else (-0.22, -0.04)


# a luneta "scout" (de olho longe) da sniper v1 (arma_sniper.py), nas cores da paleta: o trilho sobre a base da alça de
# mira (que fica dentro dele), os dois anéis com a base e os parafusos, a ocular de borracha e o anel de aumento, o
# tubo, a caixa das torres (a elevação em cima, a deriva à direita, o foco à esquerda, com as tampas serrilhadas) e a
# objetiva com o para-sol e as lentes. O ferrolho corre por baixo dela, sem nada no caminho.
EIXO_Z = 0.172
R_TUBO = 0.0115


def _tubo_aros(m, eixo, centro, raio0, raio1, comp, cor, lados, chanfro):
    """Como o Extras.tubo, mas o chanfro só nos aros (as quinas das pontas), não nas arestas compridas do tubo."""
    from mathutils import Matrix
    r = bmesh.ops.create_cone(m.bm, cap_ends=True, cap_tris=False, segments=lados, radius1=raio0, radius2=raio1,
                              depth=comp)
    vs = r["verts"]
    giro = {"y": Matrix.Rotation(math.radians(90), 4, "X"), "x": Matrix.Rotation(math.radians(90), 4, "Y"),
            "z": Matrix.Identity(4)}[eixo]
    bmesh.ops.transform(m.bm, matrix=Matrix.Translation(Vector(centro)) @ giro, verts=vs)
    faces = list({f for v in vs for f in v.link_faces})
    idx = m.nomes.index(cor)
    for f in faces:
        f.material_index = idx
    if chanfro > 0:
        aros = [e for e in {e for f in faces for e in f.edges}
                if len(e.link_faces) == 2 and e.link_faces[0].normal.dot(e.link_faces[1].normal) < 0.5]
        res = bmesh.ops.bevel(m.bm, geom=aros, offset=chanfro, segments=1, affect="EDGES", profile=0.5,
                              clamp_overlap=True)
        for f in res.get("faces", []):
            f.material_index = idx


# a luneta leve (30/10/2026, a pedido: "8500 triângulos está um pouco pesada demais, será que conseguimos abaixar um
# pouco?"): as mesmas peças, nos mesmos lugares, com menos lados (os 24 viram LEVE[24]...), o chanfro só nos aros e
# as fendas do trilho sem chanfro
LEVE = {24: 16, 20: 10, 10: 6}


def luneta_scout(m, leve=False):
    def tubo(eixo, centro, raio0, raio1, comp, cor, lados, chanfro=0.0):
        if leve:
            _tubo_aros(m, eixo, centro, raio0, raio1, comp, cor, LEVE[lados], chanfro)
        else:
            m.tubo(eixo, centro, raio0, raio1, comp, cor, lados=lados, chanfro=chanfro)

    m.caixa((-0.0110, -0.380, 0.1235), (0.0110, -0.235, 0.1360), "preto", chanfro=0.0008)
    for k in range(7):
        y = -0.250 - k * 0.020
        m.caixa((-0.0113, y - 0.0035, 0.1340), (0.0113, y + 0.0035, 0.1400), "preto", chanfro=0.0 if leve else 0.0004)
    for y in (-0.262, -0.345):
        m.caixa((-0.0110, y - 0.0075, 0.1380), (0.0110, y + 0.0075, 0.1590), "preto", chanfro=0.0008)
        tubo("y", (0.0, y, EIXO_Z), R_TUBO + 0.0040, R_TUBO + 0.0040, 0.012, "preto", 24, chanfro=0.0006)
        for lado in (-1, 1):
            tubo("x", (lado * 0.0123, y, 0.1470), 0.0020, 0.0020, 0.004, "aco", 10)
            tubo("x", (lado * 0.0150, y, EIXO_Z + 0.0035), 0.0017, 0.0017, 0.004, "aco", 10)
    tubo("y", (0.0, -0.0950, EIXO_Z), 0.0172, 0.0172, 0.010, "borracha", 24, chanfro=0.0010)
    tubo("y", (0.0, -0.1150, EIXO_Z), 0.0165, 0.0165, 0.030, "grafite", 24, chanfro=0.0005)
    tubo("y", (0.0, -0.1180, EIXO_Z), 0.0170, 0.0170, 0.012, "preto_fosco", 24)
    tubo("y", (0.0, -0.1400, EIXO_Z), 0.0165, R_TUBO, 0.020, "grafite", 24)
    tubo("y", (0.0, -0.2650, EIXO_Z), R_TUBO, R_TUBO, 0.230, "grafite", 24)
    tubo("y", (0.0, -0.3030, EIXO_Z), 0.0135, 0.0135, 0.032, "grafite", 24, chanfro=0.0006)
    tubo("y", (0.0, -0.3925, EIXO_Z), R_TUBO, 0.0168, 0.025, "grafite", 24)
    tubo("y", (0.0, -0.4175, EIXO_Z), 0.0168, 0.0168, 0.025, "grafite", 24, chanfro=0.0006)
    tubo("y", (0.0, -0.4315, EIXO_Z), 0.0174, 0.0174, 0.003, "preto", 24)
    tubo("y", (0.0, -0.4285, EIXO_Z), 0.0152, 0.0152, 0.0012, "lente", 24)
    tubo("y", (0.0, -0.0905, EIXO_Z), 0.0135, 0.0135, 0.0012, "lente", 24)
    tubo("z", (0.0, -0.303, EIXO_Z + 0.0170), 0.0085, 0.0085, 0.012, "grafite", 20, chanfro=0.0005)
    tubo("z", (0.0, -0.303, EIXO_Z + 0.0265), 0.0095, 0.0095, 0.008, "preto_fosco", 20, chanfro=0.0006)
    tubo("x", (-0.0185, -0.303, EIXO_Z), 0.0085, 0.0085, 0.010, "grafite", 20, chanfro=0.0005)
    tubo("x", (-0.0270, -0.303, EIXO_Z), 0.0095, 0.0095, 0.007, "preto_fosco", 20, chanfro=0.0006)
    tubo("x", (0.0180, -0.303, EIXO_Z), 0.0072, 0.0072, 0.009, "grafite", 20, chanfro=0.0005)


def construir(cfg):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    mats = estilizadas.materiais()
    nomes = estilizadas.NOMES
    G = v1.transformacao()
    por_peca = {p: bmesh.new() for p in PECAS}

    # 1) a Mosin: cada face na cor do material dela; a caixa do carregador vira a peça que sai na recarga
    with bpy.data.libraries.load(os.path.join(armas.FONTES, v1.FONTE), link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n in OBJETOS]
    tmp = bpy.data.meshes.new("_tmp")
    for o in [o for o in dst.objects if o is not None]:
        peca = OBJETOS[o.name]
        razao = getattr(cfg, "DECIMAR", {}).get(o.name)
        if razao:
            # a versão leve: o Decimate (por colapso) na fração pedida, antes de tudo (a peça ainda fechada)
            bpy.context.scene.collection.objects.link(o)
            mod = o.modifiers.new("Decimar", "DECIMATE")
            mod.ratio = razao
            mod.use_collapse_triangulate = True
            dg = bpy.context.evaluated_depsgraph_get()
            me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
            bpy.context.scene.collection.objects.unlink(o)
        else:
            me = o.data.copy()
        me.transform(o.matrix_basis.copy())
        fontes = [m.name if m else "" for m in me.materials]
        bm = bmesh.new()
        bm.from_mesh(me)
        destino = set()
        for f in bm.faces:
            mf = fontes[f.material_index] if f.material_index < len(fontes) else ""
            c = f.calc_center_median()
            cor = CORES.get(mf, "preto")
            if v1.peca_da_face(o.name, c) == "carregador":
                destino.add(f.index)
                cor = "preto"
            f.material_index = nomes.index(cor)
        bm.transform(G)
        bm.faces.ensure_lookup_table()
        if destino:
            copia = bm.copy()
            copia.faces.ensure_lookup_table()
            bmesh.ops.delete(copia, geom=[f for f in copia.faces if f.index not in destino], context="FACES")
            copia.to_mesh(tmp)
            copia.free()
            por_peca["carregador"].from_mesh(tmp)
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index in destino], context="FACES")
        bm.to_mesh(tmp)
        bm.free()
        por_peca[peca].from_mesh(tmp)
        bpy.data.meshes.remove(me)
        bpy.data.objects.remove(o, do_unlink=True)

    # 2) a luneta: a "scout" da sniper v1 (30/10/2026, a pedido: "o scope dela está bem pra trás ainda, o scope de como
    # estava esse modelo estava melhor feito"), à frente do receptor, sobre a base da alça de mira
    bpy.data.meshes.remove(tmp)
    luneta_scout(armas.Extras(por_peca["corpo"], nomes), leve=getattr(cfg, "LUNETA_LEVE", False))

    # 3) as peças: o que ninguém vê sai, as faces num plano se juntam, lisas até 30°, o UV na paleta
    col = armas._colecao(cfg.NOME)
    objs = []
    for peca, bm in por_peca.items():
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        n = armas.tirar_escondidas(bm)
        bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1.0), verts=bm.verts, edges=bm.edges, delimit={"MATERIAL"})
        bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        nome = f"{cfg.NOME}_{peca}"
        me = bpy.data.meshes.new(nome)
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=math.radians(ANGULO_LISO))
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
