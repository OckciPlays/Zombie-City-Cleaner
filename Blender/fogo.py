# O fogo (10/11/2026, a etapa 4 das armas de sucata): o lampião, o coquetel molotov e a fogueira portátil.
#
# - Fogo.fbx: as peças que vão na mão e no mundo, cada uma com o vazio do fogo (filho dela):
#   Lampiao_corpo (com o vazio Chama, onde a luz nasce) e Molotov_corpo (com o vazio Pavio, onde o fogo nasce), no
#   formato do Itens_<Arma>.fbx (o da tocha: a origem onde a mão segura, a cabeça em +Z e o alto em +Y na Unity; o
#   material "Pistola", a paleta dos itens); e Fogueira_Portatil_corpo (com o vazio Fogo, a 12 cm do chão, como a
#   fogueira montada), no formato do Loot.fbx (a origem embaixo, no meio), do mesmo tamanho da FogueiraMontada do
#   Loot.fbx (o anel de pedras de 0,8 m), com as pedras menores, a lenha em pirâmide, a base de cinza e a latinha de
#   álcool vazia do lado.
#   Os modelos do lampião e do molotov são os do Blender/Armas/lampiao.py e molotov.py (os mesmos dos braços de primeira
#   pessoa).
# - Os itens soltos (no formato do Loot.fbx: Item_<Nome>_corpo, a origem embaixo, no meio), que o loot_etapa34.py junta
#   no Loot_Etapa34.fbx: Item_Lampiao (em pé), Item_Molotov (em pé, o pano em cima) e Item_KitFogueira (o feixe de lenha
#   amarrado com a latinha de álcool).
# - A tabela da mão do corpo (a terceira pessoa usa as poses da tocha, Tocha_Segurar e Tocha_Golpe do
#   Personagem_PrincipalBloco_Brancas.fbx): o lugar e o giro do Lampiao_corpo e do Molotov_corpo no hand.R, na convenção
#   da MontagemDaRede.PegadaNaMao (a posição local e o quatérnio xyzw da Unity). O molotov vai na pegada da tocha (o
#   gargalo no lugar do cabo); o lampião, na mesma pegada girado o mínimo para ficar de pé (pendurado) no Tocha_Segurar.
#   Grava Fogo_MaoDoCorpo.json e as fotos do corpo segurando os dois.
#
# Como rodar (Blender 5.2, em segundo plano; um Blender por vez; os FBX do projeto só são lidos):
#   blender -b --factory-startup --disable-autoexec --python Blender/fogo.py -- tudo <pasta de saída>
#   (as etapas: construir, exportar, fotos, corpo; a saída padrão é a pasta do jogador)

import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
for p in (AQUI, os.path.join(AQUI, "Armas")):
    if p not in sys.path:
        sys.path.insert(0, p)
import equipamentos as eq  # noqa: E402
import loot  # noqa: E402
import sucata_itens as si  # noqa: E402

C, Z, T = loot.C, loot.Z, si.T
ARTE = os.path.join(eq.REPO, "Assets", "_Game", "Art")
BRANCAS = os.path.join(ARTE, "Characters", "Personagens", "Personagem_PrincipalBloco_Brancas.fbx")
ROUPAS = os.path.join(ARTE, "Characters", "Roupas", "Roupas.fbx")
RIG = "PrincipalBloco_Rig"
FOGO_Y = 0.12          # a altura do fogo na fogueira (a ConstrucoesNoMundo põe o da montada em (0, 0,12, 0))

eq.CORES.update({"f_lata_azul": (4, 8), "f_lata_branca": (11, 14)})


def _cfgs():
    import lampiao
    import molotov
    return {"Lampiao": lampiao, "Molotov": molotov}


# ------------------------------------------------------------------------------------------------ as peças em bmesh
def bm_da_cfg(cfg, M=Matrix.Identity(4)):
    """O modelo da cfg (o faca.malha: sem o escondido, triangulado) com o UV de cada face na célula da cor dela, levado
    por M."""
    import faca
    bm = faca.malha(cfg)
    uv = bm.loops.layers.uv.verify()
    nomes = list(cfg.CORES)
    for f in bm.faces:
        u, v = faca.celula(cfg, nomes[f.material_index])
        for l in f.loops:
            l[uv].uv = (u, v)
        f.material_index = 0
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)
    return bm


def bm_das_pecas(pecas):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for p in pecas:
        p[0](bm, uv, *p[1:])
    return bm


def objeto(nome, bm, cena, origem="embaixo"):
    """O objeto no formato do Loot.fbx / Itens.fbx: a malha nos eixos da Unity (girada -90° em X) e o objeto girado 90° em
    X. origem: "embaixo" (o meio da caixa embaixo, no chão) ou "aqui" (a origem do modelo: onde a mão segura)."""
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if origem == "embaixo":
        vs = [v.co for v in bm.verts]
        meio = Vector(((min(v.x for v in vs) + max(v.x for v in vs)) / 2, (min(v.y for v in vs) + max(v.y for v in vs)) / 2,
                       min(v.z for v in vs)))
        bmesh.ops.translate(bm, vec=-meio, verts=bm.verts)
    me = bpy.data.meshes.new(nome)
    bm.to_mesh(me)
    bm.free()
    for f in me.polygons:
        f.use_smooth = False
    me.materials.append(eq._material())
    me.transform(Matrix.Rotation(math.radians(-90), 4, "X"))
    o = bpy.data.objects.new(nome, me)
    o.rotation_euler = (math.radians(90), 0, 0)
    cena.collection.objects.link(o)
    return o


def vazio(nome, pai, ponto_blender):
    """Um vazio filho de pai (um objeto deste formato), no ponto dado nos eixos do Blender do modelo (antes do giro)."""
    e = bpy.data.objects.new(nome, None)
    e.empty_display_type = "SPHERE"
    e.empty_display_size = 0.02
    bpy.context.scene.collection.objects.link(e)
    e.parent = pai
    e.matrix_parent_inverse = Matrix.Identity(4)
    e.location = Matrix.Rotation(math.radians(-90), 3, "X") @ Vector(ponto_blender)
    return e


# ------------------------------------------------------------------------------------------------ a fogueira e o kit
def _lata_de_alcool(centro, deitada=True, giro=0.0):
    """A latinha de álcool (azul, a faixa branca e a tampa vermelha), deitada ou em pé."""
    r, h = 0.034, 0.105
    if deitada:
        M = Matrix.Translation(Vector(centro)) @ Matrix.Rotation(math.radians(giro), 4, "Z") @ Matrix.Rotation(math.radians(90), 4, "Y")
    else:
        M = Matrix.Translation(Vector(centro))
    return (loot._com_matriz, M, [
        si.L("f_lata_azul", r, h, -h / 2),
        si.L("f_lata_branca", r + 0.0008, 0.035, -0.012),
        si.L("lata_clara", r - 0.004, 0.006, h / 2),
        Z("vermelho", 0.009, 0.012, (0.012, 0.0, h / 2 + 0.008), 8),
    ])


def _fogueira_portatil():
    """O anel de pedras pequenas (o mesmo raio da FogueiraMontada: 0,33 m), a base de cinza com carvão, a lenha em pirâmide
    (5 achas encostadas umas nas outras e 2 deitadas embaixo, com gravetos) e a latinha vazia do lado."""
    p = [Z("carvao", 0.26, 0.016, (0, 0, 0.008), 12)]
    for i, (a, d) in enumerate(((20, 0.12), (110, 0.08), (200, 0.15), (290, 0.1), (160, 0.18))):
        t = math.radians(a)
        p.append(C("preto" if i % 2 else "cinza_escuro", (0.05, 0.035, 0.025), (d * math.cos(t), d * math.sin(t), 0.018),
                   giro=(0, 0, a), chanfro=0.008))
    for i in range(12):
        a = 2 * math.pi * i / 12 + 0.12
        tam = (0.095, 0.075, 0.065) if i % 3 else (0.11, 0.08, 0.075)
        p.append(C("pedra" if i % 2 else "pedra_escura", tam, (0.33 * math.cos(a), 0.33 * math.sin(a), tam[2] / 2),
                   giro=(0, 0, math.degrees(a) + 90 + 9 * (i % 4)), chanfro=0.015))
    # as 2 achas deitadas em cruz embaixo
    for a, cor in ((25, "madeira_escura"), (115, "casca")):
        t = math.radians(a)
        d = Vector((math.cos(t), math.sin(t), 0)) * 0.21
        p.append(T(cor, tuple(-d + Vector((0, 0, 0.04))), tuple(d + Vector((0, 0, 0.04))), 0.032, 7))
    # a pirâmide: 5 achas da beira até o alto, no meio
    topo = Vector((0, 0, 0.36))
    for i in range(5):
        t = 2 * math.pi * i / 5 + 0.3
        base = Vector((0.2 * math.cos(t), 0.2 * math.sin(t), 0.03))
        p.append(T("casca" if i % 2 else "madeira", tuple(base), tuple(topo + (base - topo) * -0.06), 0.028, 7))
        p.append(T("cerne", tuple(base + (topo - base) * -0.01), tuple(base + (topo - base) * 0.001), 0.025, 7))
    # os gravetos no meio
    for i in range(4):
        t = 2 * math.pi * i / 4 + 0.7
        a = Vector((0.11 * math.cos(t), 0.11 * math.sin(t), 0.02))
        p.append(T("madeira_clara", tuple(a), (0.02 * math.cos(t + 2), 0.02 * math.sin(t + 2), 0.16), 0.008, 4))
    # a latinha de álcool vazia, deitada do lado de fora das pedras
    p.append(_lata_de_alcool((0.47, -0.16, 0.034), True, 30.0))
    return p


def _kit_fogueira():
    """O feixe de lenha (6 achas de 0,42 m) amarrado com duas voltas de corda, e a latinha de álcool presa por cima."""
    p = []
    achas = [(-0.035, 0.032), (0.035, 0.032), (-0.07, 0.032), (0.07, 0.032), (0.0, 0.088), (-0.05, 0.088), (0.05, 0.088)][:6]
    achas = [(-0.06, 0.032), (0.0, 0.032), (0.06, 0.032), (-0.03, 0.086), (0.03, 0.086), (0.0, 0.138)]
    for i, (y, z) in enumerate(achas):
        cor = "casca" if i % 2 else "madeira_escura"
        p.append(Z(cor, 0.031, 0.42 + 0.015 * (i % 3), (0.01 * (i % 3) - 0.01, y, z), 8, "X"))
        p.append(Z("cerne", 0.026, 0.42 + 0.015 * (i % 3) + 0.004, (0.01 * (i % 3) - 0.01, y, z), 8, "X"))
    # as duas voltas de corda (em volta do feixe) e o nó
    for x in (-0.13, 0.13):
        anel = [Vector((x, 0.1 * math.cos(2 * math.pi * k / 10), 0.088 + 0.088 * math.sin(2 * math.pi * k / 10)))
                for k in range(11)]
        p.append((lambda bm, uv, pts: _corda_pts(bm, uv, "corda", pts, 0.007), anel))
        p.append(C("corda", (0.024, 0.02, 0.02), (x, 0.0, 0.18)))
    # a latinha de álcool por cima, em pé, presa na corda
    p.append(_lata_de_alcool((0.0, 0.0, 0.17 + 0.0525), False))
    return p


def _corda_pts(bm, uv, cor, pts, raio):
    import protecao_sucata as ps
    ps.corda(bm, uv, cor, pts, raio, 6)


# ------------------------------------------------------------------------------------------------ construir e exportar
def construir():
    """Os objetos do Fogo.fbx (com os vazios) e os itens do fogo, numa cena limpa."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cena = bpy.context.scene
    cfg = _cfgs()
    rx = Matrix.Rotation(math.radians(-90), 4, "X")
    fogo = {}
    o = objeto("Lampiao_corpo", bm_da_cfg(cfg["Lampiao"]), cena, origem="aqui")
    vazio("Chama", o, cfg["Lampiao"].CHAMA)
    fogo["Lampiao"] = o
    o = objeto("Molotov_corpo", bm_da_cfg(cfg["Molotov"]), cena, origem="aqui")
    vazio("Pavio", o, cfg["Molotov"].PAVIO)
    fogo["Molotov"] = o
    o = objeto("Fogueira_Portatil_corpo", bm_das_pecas(_fogueira_portatil()), cena)
    vazio("Fogo", o, (0.0, 0.0, FOGO_Y))
    fogo["Fogueira"] = o
    itens = {
        "Item_Lampiao": objeto("Item_Lampiao_corpo", bm_da_cfg(cfg["Lampiao"]), cena),
        # em pé: o pano (a ponta, -Y da arma) para cima
        "Item_Molotov": objeto("Item_Molotov_corpo", bm_da_cfg(cfg["Molotov"], rx), cena),
        "Item_KitFogueira": objeto("Item_KitFogueira_corpo", bm_das_pecas(_kit_fogueira()), cena),
    }
    for i, ob in enumerate(list(fogo.values()) + list(itens.values())):
        ob.location = (i * 0.9, 0, 0)
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        vs = [v.co for v in ob.data.vertices]
        tam = [max(v[k] for v in vs) - min(v[k] for v in vs) for k in range(3)]
        print(f"  {ob.name:26s} {tris:5d} triângulos; {tam[0]:.3f} x {tam[1]:.3f} x {tam[2]:.3f} m (Unity: x, y alto, z)")
    return fogo, itens


def exportar(fogo, pasta):
    objs = list(fogo.values()) + [c for o in fogo.values() for c in o.children]
    for ob in bpy.context.scene.objects:
        ob.select_set(ob in objs)
    bpy.context.view_layer.objects.active = objs[0]
    caminho = os.path.join(pasta, "Fogo.fbx")
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")
    print("fogo: gravado", caminho)


def fotos(objs, pasta, prefixo="Foto_"):
    """As fotos de conferência (600 x 500, de 3/4), no estilo dos ícones do saque, uma por objeto."""
    os.makedirs(pasta, exist_ok=True)
    cena = bpy.context.scene
    r = loot._estilo(cena)
    todos = [o for o in cena.objects if o.type == "MESH"]
    for o in objs:
        for outro in todos:
            outro.hide_render = outro is not o
        cantos = [o.matrix_world @ Vector(c) for c in o.bound_box]
        centro = sum(cantos, Vector()) / 8
        tam = max((max(c[i] for c in cantos) - min(c[i] for c in cantos)) for i in range(3))
        r.resolution_x, r.resolution_y = 600, 500
        eq._camera(cena, False, centro, (0.8, -1.0, 0.55), tam * 2.6, tam)
        r.filepath = os.path.join(pasta, f"{prefixo}{o.name.replace('_corpo', '')}.png")
        bpy.ops.render.render(write_still=True)
    for outro in todos:
        outro.hide_render = False


# ------------------------------------------------------------------------------------------------ a mão do corpo
def corpo(pasta, cfgs=None):
    """O lugar do lampião e do molotov no hand.R do corpo, nas poses da tocha (Personagem_PrincipalBloco_Brancas.fbx), e as
    fotos do corpo (o Roupas.fbx, no Tocha_Segurar e no meio do Tocha_Golpe) com eles na mão."""
    import brancas_corpo as bc
    cfgs = cfgs or _cfgs()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=BRANCAS)
    rb = bpy.data.objects[RIG]
    acoes = {a.name.split("|")[-1]: a for a in bpy.data.actions if a.name.startswith(RIG + "|")}
    seg = acoes["Tocha_Segurar"]
    for a in acoes.values():
        a.use_fake_user = True
    bc.ag.usar_acao(rb, seg)
    bpy.context.scene.frame_set(int(seg.frame_range[0]))
    bpy.context.view_layer.update()
    rel = rb.pose.bones["hand.R"].matrix.inverted() @ rb.pose.bones["arma"].matrix
    H_tocha = rel @ bc.EIXOS_DO_OSSO.inverted()
    Mmao = rb.matrix_world @ rb.pose.bones["hand.R"].matrix
    # o lampião: o alto (+Z da arma) para o alto do mundo, girado o mínimo a partir da pegada da tocha
    R = Mmao.to_3x3() @ H_tocha.to_3x3()
    alto = (R @ Vector((0, 0, 1))).normalized()
    giro = alto.rotation_difference(Vector((0, 0, 1))).to_matrix()
    H_lamp = Matrix.Translation(H_tocha.translation) @ (Mmao.to_3x3().inverted() @ giro @ R).to_4x4()
    tab = {
        "convencao": ("A malha avulsa (Lampiao_corpo e Molotov_corpo do Fogo.fbx: a origem onde a mão segura, a cabeça em "
                      "+Z e o alto em +Y) como filha do hand.R do corpo: localPosition (m) e localRotation (quatérnio xyzw), "
                      "como a MontagemDaRede.PegadaNaMao das armas brancas; os mesmos no Tocha_Segurar e no Tocha_Golpe."),
        "Tocha_conferir": bc._item_unity_em(H_tocha),
        "Molotov": bc._item_unity_em(H_tocha),
        "Lampiao": bc._item_unity_em(H_lamp),
    }
    with open(os.path.join(pasta, "Fogo_MaoDoCorpo.json"), "w", encoding="utf-8") as arq:
        json.dump(tab, arq, indent=1, ensure_ascii=False)
    for k in ("Tocha_conferir", "Molotov", "Lampiao"):
        print(f"  corpo {k}: {tab[k]['localPosition']} {tab[k]['localRotation_xyzw']}")
    # as fotos: o corpo do Roupas.fbx com as poses da tocha
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=ROUPAS)
    for img in bpy.data.images:
        if img.name.startswith("Paleta"):
            img.filepath = eq.PALETA
            img.reload()
    rig = bpy.data.objects[RIG]
    rig.animation_data_create()
    import protecao_sucata as ps
    vestir = ["NPC_Corpo", "Roupa_Jaqueta_Lona", "Roupa_Jeans", "Roupa_Tenis", "Roupa_Cabelo_Curto"]
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = o.name not in vestir
            for s in o.material_slots:
                m = s.material
                if m and m.node_tree:
                    for nd in list(m.node_tree.nodes):
                        if nd.type == "TEX_IMAGE":
                            m.node_tree.nodes.remove(nd)
                if m:
                    m.diffuse_color = (*ps._cor_roupa(o.name), 1.0)
    cena = bpy.context.scene
    pecas = {}
    for nome, H in (("Lampiao", H_lamp), ("Molotov", H_tocha)):
        ob = objeto(nome + "_mao", bm_da_cfg(cfgs[nome]), cena, origem="aqui")
        ob.data.transform(Matrix.Rotation(math.radians(90), 4, "X"))   # de volta aos eixos do Blender do modelo
        ob.rotation_euler = (0, 0, 0)
        pecas[nome] = (ob, H)
    r = ps._estilo_fotos(cena)
    cam = bpy.data.objects.new("CamCorpo", bpy.data.cameras.new("CamCorpo"))
    cena.collection.objects.link(cam)
    cena.camera = cam
    cam.data.type = "ORTHO"
    saidas = []
    for pose, quadro in (("Tocha_Segurar", 0.0), ("Tocha_Golpe", 0.25)):
        a = acoes[pose]
        ps._usar(rig, a)
        f0, f1 = a.frame_range
        cena.frame_set(int(round(f0 + (f1 - f0) * quadro)))
        bpy.context.view_layer.update()
        Mm = rig.matrix_world @ rig.pose.bones["hand.R"].matrix
        for nome, (ob, H) in pecas.items():
            for outro, _ in pecas.values():
                outro.hide_render = outro is not ob
            ob.matrix_world = Mm @ H
            for vista, d, esc in (("frente", (0.35, -1, 0.15), 1.3), ("lado", (1, -0.2, 0.1), 1.3)):
                alvo = (rig.matrix_world @ rig.pose.bones["chest"].matrix).translation + Vector((-0.1, -0.1, 0.0))
                dv = Vector(d).normalized()
                cam.location = alvo + dv * 4
                cam.rotation_euler = (-dv).to_track_quat("-Z", "Y").to_euler()
                cam.data.ortho_scale = esc
                r.resolution_x, r.resolution_y = 480, 520
                r.filepath = os.path.join(pasta, "fotos", f"corpo_{nome.lower()}_{pose.lower()}_{vista}.png")
                bpy.ops.render.render(write_still=True)
                saidas.append(r.filepath)
    return tab


def rodar(etapas, pasta):
    import traceback
    if "tudo" in etapas:
        etapas = ["construir", "exportar", "fotos", "corpo"]
    try:
        os.makedirs(pasta, exist_ok=True)
        fogo, itens = construir()
        if "exportar" in etapas:
            exportar(fogo, pasta)
        if "fotos" in etapas:
            fotos(list(fogo.values()) + list(itens.values()), os.path.join(pasta, "fotos"))
        if "corpo" in etapas:
            corpo(pasta)
        print("ETAPAS OK")
    except Exception:
        traceback.print_exc()
        print("ETAPA FALHOU")
        sys.exit(1)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    etapas = [a for a in args if not (os.sep in a or "/" in a or ":" in a)] or ["tudo"]
    pastas = [a for a in args if a not in etapas]
    rodar(etapas, pastas[0] if pastas else eq.UNITY)
