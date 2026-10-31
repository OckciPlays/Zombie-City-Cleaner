# A faca (31/10/2026, a pedido: "vamos fazer agora um mecanismo para armas brancas. Vamos começar pela faca. Por hora
# crie um sistema básico de atacar e segurar"; a imagem de referência é uma baioneta M9): a primeira arma branca, um item
# que vai na mão como a lata e a garrafa (Itens.Modelos: "Faca"). Este arquivo também tem o que as armas brancas usam
# (as funções recebem a cfg: este módulo, ou o machado.py).
#
# - O modelo (no espaço da arma solta: a lâmina em -Y, o dorso em +Z, a direita em -X, a origem onde a mão segura): a
#   lâmina preta com o fio de aço, os dentes do dorso e o furo sextavado perto da ponta, a guarda com o anel de cima, o
#   cabo com os 4 gomos e o pomo com a trava. Nas cores da paleta dos itens (Art/Paleta.png, 16 x 16, a mesma da lata e
#   da garrafa: o material "Pistola" da montagem), com o UV de cada face no meio da célula da cor dela.
# - A malha solta: Itens_<Modelo>.fbx (a peça <Modelo>_corpo), na pasta do jogador; a montagem das armas junta em
#   Item_<Modelo>.
# - Os braços de primeira pessoa: os dos punhos (Jogador_FP_Maos.fbx, com a guarda e os socos) com a arma no punho
#   direito, presa ao osso hand.R (Jogador_FP_<Modelo>.fbx). O golpe da faca é o soco direito; a cfg pode fazer um clipe
#   "Golpe" próprio (a função golpe(rig, clipes), como a machadada do machado.py).
# - O ícone e a foto do inventário (Icone_<Modelo> e Foto_<Modelo>).
#
# Como rodar: blender -b --factory-startup --disable-autoexec --python Blender/Armas/faca.py -- tudo
#   (as etapas: construir, exportar, imagens, fotos, bracos, conferir)

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import armas  # noqa: E402
import estilizadas  # noqa: E402
from v3_mp5 import _anel_y  # noqa: E402

NOME = "Faca"
FOTO = "faca"
MODELO = "Faca"
PECAS = ["corpo"]
ICONE = (256, 128)
ANGULO_LISO = 35
PALETA_ITENS = os.path.join(armas.REPO, "Assets", "_Game", "Art", "Paleta.png")
MAOS = os.path.join(armas.JOGADOR, "Jogador_FP_Maos.fbx")
# as cores (a célula da Paleta.png: a linha de cima é a 0, a coluna da esquerda é a 0); as de METAL brilham nas fotos
CORES = {
    "lamina": (3, 2),    # 1b1c1f
    "fio": (4, 6),       # c4c2b8
    "cabo": (3, 0),      # 26282c
    "anel": (3, 1),      # 33363b
    "guarda": (3, 6),    # 202225
    "furo": (11, 5),     # 121314
    "pomo": (3, 5),      # 2f3235
}
METAL = {"fio"}
# o lugar da faca no punho direito (os braços dos punhos, no quadro 1 do Idle): o cabo atravessa a mão do mindinho ao
# indicador, a lâmina sai do lado do polegar, o dorso para o lado do pulso; GIRO_PARA_A_FRENTE deita a lâmina para a
# frente (graus, em volta do eixo do dorso) e NO_PUNHO desloca o cabo (m, no espaço da faca: y para o pomo)
GIRO_PARA_A_FRENTE = 40.0
# o giro da faca em volta da lâmina (graus): na pegada certa, a lâmina fica de quina para a câmera (um palito); girada,
# o lado chato aparece
GIRO_NO_EIXO = -70.0
NO_PUNHO = Vector((0.0, 0.016, 0.0))


def avulsa(cfg):
    return os.path.join(armas.JOGADOR, f"Itens_{cfg.MODELO}.fbx")


def fp(cfg):
    return os.path.join(armas.JOGADOR, f"Jogador_FP_{cfg.MODELO}.fbx")


def _cor(cfg, nome):
    """A cor (os valores da imagem, 0-1) da célula da Paleta.png."""
    img = bpy.data.images.load(PALETA_ITENS, check_existing=True)
    w, h = img.size
    linha, coluna = cfg.CORES[nome]
    x, y = coluna * (w // 16) + (w // 32), (h - 1) - (linha * (h // 16) + (h // 32))
    i = (y * w + x) * 4
    return tuple(img.pixels[i:i + 3])


def celula(cfg, nome):
    linha, coluna = cfg.CORES[nome]
    return (coluna + 0.5) / 16.0, 1.0 - (linha + 0.5) / 16.0


def materiais(cfg):
    mats = []
    for nome in cfg.CORES:
        mat = bpy.data.materials.get(f"{cfg.NOME}_{nome}") or bpy.data.materials.new(f"{cfg.NOME}_{nome}")
        mat.use_nodes = True
        lin = tuple(estilizadas._linear(round(c * 255)) for c in _cor(cfg, nome))
        mat.diffuse_color = (*lin, 1.0)
        b = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        b.inputs["Base Color"].default_value = (*lin, 1.0)
        b.inputs["Metallic"].default_value = 0.85 if nome in cfg.METAL else 0.2
        b.inputs["Roughness"].default_value = 0.35 if nome in cfg.METAL else 0.75
        mats.append(mat)
    return mats


def modelo(m):
    """A faca no espaço da arma solta (m): a lâmina, os dentes, o furo, a guarda com o anel, o cabo, os gomos e o pomo."""
    # a lâmina: a parte preta (o dorso reto e a ponta "clip") e o fio de aço embaixo, mais fino
    m.perfil_x([(-0.064, 0.0145), (-0.200, 0.0145), (-0.246, 0.001), (-0.232, -0.004), (-0.064, -0.006)],
               -0.0028, 0.0028, "lamina", chanfro=0.0006)
    m.perfil_x([(-0.064, -0.006), (-0.232, -0.004), (-0.246, 0.001), (-0.240, -0.005), (-0.222, -0.0115),
                (-0.192, -0.0165), (-0.064, -0.0165)], -0.0013, 0.0013, "fio", chanfro=0.0)
    # os dentes do dorso (inclinados para o cabo) e o furo sextavado perto da ponta
    for k in range(8):
        y = -0.095 - k * 0.010
        m.perfil_x([(y, 0.0140), (y - 0.010, 0.0140), (y - 0.0025, 0.0195)], -0.0020, 0.0020, "lamina")
    m.tubo("x", (0.0, -0.214, 0.004), 0.0042, 0.0042, 0.0062, "furo", lados=6)
    # a guarda e o anel de cima (o do cano, na baioneta)
    m.caixa((-0.008, -0.064, -0.026), (0.008, -0.055, 0.027), "guarda", chanfro=0.002)
    _anel_y(m, (0.0, -0.0595, 0.036), 0.0105, 0.0070, 0.009, "guarda", lados=8)
    # o cabo: o miolo, os 4 gomos e o pomo com a trava de baixo
    m.caixa((-0.0105, -0.055, -0.0145), (0.0105, 0.068, 0.0145), "cabo", chanfro=0.003)
    for y in (-0.040, -0.012, 0.016, 0.044):
        m.caixa((-0.0125, y - 0.009, -0.0168), (0.0125, y + 0.009, 0.0168), "anel", chanfro=0.004)
    m.caixa((-0.012, 0.068, -0.017), (0.012, 0.084, 0.017), "pomo", chanfro=0.003)
    m.caixa((-0.004, 0.060, -0.022), (0.004, 0.080, -0.016), "pomo", chanfro=0.0)


def malha(cfg):
    """O bmesh da arma pronto: sem o escondido, triangulado, as normais e o material (o índice em CORES) de cada face."""
    bm = bmesh.new()
    cfg.modelo(armas.Extras(bm, list(cfg.CORES)))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    armas.tirar_escondidas(bm)
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def _uv_e_quinas(cfg, me):
    nomes = list(cfg.CORES)
    uv = me.uv_layers.active.data
    for p in me.polygons:
        u, v = celula(cfg, nomes[p.material_index])
        for k in p.loop_indices:
            uv[k].uv = (u, v)
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(cfg.ANGULO_LISO))


def construir(cfg):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    mats = materiais(cfg)
    bm = malha(cfg)
    me = bpy.data.meshes.new(f"{cfg.NOME}_corpo")
    bm.to_mesh(me)
    bm.free()
    for mat in mats:
        me.materials.append(mat)
    me.uv_layers.new(name="UVMap")
    _uv_e_quinas(cfg, me)
    col = armas._colecao(cfg.NOME)
    o = bpy.data.objects.new(f"{cfg.NOME}_corpo", me)
    col.objects.link(o)
    print(f"  {o.name}: {len(me.polygons)} triângulos")
    return [o]


def exportar(cfg):
    """A malha solta (Itens_<Modelo>.fbx, a peça <Modelo>_corpo): girada como as armas novas (a frente em +Z e o alto em +Y
    na Unity) e com um material só, "Pistola" (a montagem troca pelo da paleta dos itens)."""
    o = bpy.data.objects[f"{cfg.NOME}_corpo"]
    me = o.data.copy()
    me.transform(Matrix.Rotation(math.radians(-90), 4, "X"))
    me.materials.clear()
    mat = bpy.data.materials.get("Pistola") or bpy.data.materials.new("Pistola")
    me.materials.append(mat)
    for p in me.polygons:
        p.material_index = 0
    c = bpy.data.objects.new(f"{cfg.NOME}_corpo_exportar", me)
    c.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.scene.collection.objects.link(c)
    o.name = f"{cfg.NOME}_corpo_autoria"
    c.name = f"{cfg.NOME}_corpo"
    armas._selecionar([c])
    bpy.ops.export_scene.fbx(filepath=avulsa(cfg), use_selection=True, object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             bake_anim=False, mesh_smooth_type="OFF", use_tspace=False, path_mode="STRIP")
    bpy.data.objects.remove(c, do_unlink=True)
    o.name = f"{cfg.NOME}_corpo"
    print("  exportado:", avulsa(cfg))


# ------------------------------------------------------------------------------------------------ os braços (FP)
def _usar(rig, acao):
    ad = rig.animation_data
    ad.action = acao
    if hasattr(ad, "action_slot") and len(acao.slots):
        ad.action_slot = acao.slots[0]


def _no_punho(rig, cfg):
    """A matriz que leva o espaço da arma ao do mundo com ela no punho direito (no quadro 1 do Idle)."""
    pb = rig.pose.bones
    M = rig.matrix_world
    dedos = []
    for d in ("index", "middle", "ring", "pinky"):
        for k in ("1", "2"):
            b = pb[f"{d}{k}.R"]
            dedos += [M @ b.head, M @ b.tail]
    miolo = sum(dedos, Vector()) / len(dedos)
    lamina = ((M @ pb["index1.R"].head) - (M @ pb["pinky1.R"].head)).normalized()
    pulso = ((M @ pb["hand.R"].head) - (M @ pb["hand.R"].tail)).normalized()
    dorso = (pulso - lamina * pulso.dot(lamina)).normalized()
    # a frente da arma (a lâmina, a cabeça) é o -Y dela e o dorso o +Z: o +X sai pela regra da mão direita
    y = -lamina
    z = dorso
    x = y.cross(z)
    R = Matrix((x, y, z)).transposed().to_4x4()
    # GIRO_DE_LADO (graus, em volta do dorso): deita a arma para o lado, atravessando a tela (o machado)
    R = (R @ Matrix.Rotation(math.radians(getattr(cfg, "GIRO_DE_LADO", 0.0)), 4, "Z")
         @ Matrix.Rotation(math.radians(cfg.GIRO_PARA_A_FRENTE), 4, "X") @ Matrix.Rotation(math.radians(cfg.GIRO_NO_EIXO), 4, "Y"))
    return Matrix.Translation(miolo) @ R @ Matrix.Translation(cfg.NO_PUNHO)


def bracos(cfg):
    """Os braços dos punhos com a arma no punho direito (o osso hand.R), e o clipe Golpe, se a cfg fizer um."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=MAOS)
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    corpo = next(o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("FP_Bracos"))
    boca = next(o for o in bpy.data.objects if o.name.startswith("Boca_FP"))
    clipes = [a for a in bpy.data.actions if a.name.startswith(rig.name + "|")]
    prefixo = rig.name
    rig.name, corpo.name, boca.name = f"FP_Rig_{cfg.MODELO}", f"FP_Bracos_{cfg.MODELO}", f"Boca_FP_{cfg.MODELO}"
    rig.animation_data_create()
    idle = next(a for a in clipes if a.name.endswith("|Idle"))
    _usar(rig, idle)
    bpy.context.scene.frame_set(int(idle.frame_range[0]))
    bpy.context.view_layer.update()
    P = _no_punho(rig, cfg)
    # do quadro do Idle para o repouso: o inverso da deformação do hand.R (a malha está no repouso)
    pb = rig.pose.bones["hand.R"]
    D = rig.matrix_world @ pb.matrix @ pb.bone.matrix_local.inverted() @ rig.matrix_world.inverted()
    para_repouso = corpo.matrix_world.inverted() @ D.inverted() @ P

    arma = malha(cfg)
    nomes = list(cfg.CORES)
    me = corpo.data
    bm = bmesh.new()
    bm.from_mesh(me)
    deform = bm.verts.layers.deform.verify()
    uv = bm.loops.layers.uv.active
    g = corpo.vertex_groups["hand.R"].index
    slot = next(i for i, m in enumerate(me.materials) if m and m.name.startswith("Pistola"))
    novos = {}
    for v in arma.verts:
        n = bm.verts.new(para_repouso @ v.co)
        n[deform][g] = 1.0
        novos[v] = n
    for f in arma.faces:
        nf = bm.faces.new([novos[v] for v in f.verts])
        nf.material_index = slot
        nf.smooth = True
        u, vv = celula(cfg, nomes[f.material_index])
        for l in nf.loops:
            l[uv].uv = (u, vv)
    n_arma = len(arma.faces)
    arma.free()
    bm.to_mesh(me)
    bm.free()
    print(f"  {cfg.MODELO} no punho: {n_arma} triângulos; os braços com {len(me.polygons)} faces")
    if hasattr(cfg, "golpe"):
        clipes.append(cfg.golpe(rig, idle, prefixo))
    if hasattr(cfg, "segunda_mao"):
        cfg.segunda_mao(rig, clipes, P, idle)
    if hasattr(cfg, "PONTA"):
        # a boca vai para a ponta da arma (a chama da tocha), presa ao punho direito
        _usar(rig, idle)
        bpy.context.scene.frame_set(int(idle.frame_range[0]))
        bpy.context.view_layer.update()
        boca.parent = rig
        boca.parent_type = "BONE"
        boca.parent_bone = "hand.R"
        bpy.context.view_layer.update()
        boca.matrix_world = P @ Matrix.Translation(cfg.PONTA)
        print(f"  a boca na ponta: {tuple(round(c, 3) for c in boca.matrix_world.translation)}")
    return rig, corpo, boca, clipes


def conferir(rig, clipes, cfg):
    """Fotos da câmera de primeira pessoa (na origem, olhando -Y): o Idle, o golpe e o sacar; e de lado e de cima."""
    cena = bpy.context.scene
    r = cena.render
    r.engine = "BLENDER_WORKBENCH"
    s = cena.display.shading
    s.light = "STUDIO"
    s.color_type = "MATERIAL"
    s.show_cavity = True
    r.resolution_x, r.resolution_y = 1280, 720
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cena.collection.objects.link(cam)
    cena.camera = cam
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle = math.radians(55)   # a câmera dos braços do jogo (MontagemDaRede: camBracos.fieldOfView = 55)
    cam.data.clip_start = 0.01
    cam.location = (0, 0, 0)
    cam.rotation_euler = (math.radians(90), 0, math.radians(180))
    pasta = os.path.join(armas.REPO, "Docs", "Armas", "rascunho")
    os.makedirs(pasta, exist_ok=True)
    nome_golpe = "Golpe" if any(a.name.endswith("|Golpe") for a in clipes) else "SocoDireito"
    quadros_golpe = getattr(cfg, "QUADROS_DO_GOLPE", (1, 3, 5))
    base = cfg.MODELO.lower()
    for nome, quadros in (("Idle", (1,)), (nome_golpe, quadros_golpe), ("Sacar", (1, 7))):
        a = next(a for a in clipes if a.name.endswith("|" + nome))
        _usar(rig, a)
        for f in quadros:
            cena.frame_set(f)
            r.filepath = os.path.join(pasta, f"{base}_fp_{nome.lower()}_{f}.png")
            bpy.ops.render.render(write_still=True)
    # de lado, de longe, para ver a arma na mão
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = getattr(cfg, "ORTHO_CONFERIR", 0.6)
    a = next(a for a in clipes if a.name.endswith("|Idle"))
    _usar(rig, a)
    cena.frame_set(1)
    for vista, loc, rot in (("lado", (1.2, -0.35, -0.1), (math.radians(90), 0, math.radians(90))),
                            ("cima", (-0.2, -0.35, 1.2), (0, 0, 0))):
        cam.location = loc
        cam.rotation_euler = rot
        r.filepath = os.path.join(pasta, f"{base}_fp_idle_{vista}.png")
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    print("  fotos em", pasta)


def exportar_bracos(rig, corpo, boca, clipes, cfg):
    """Um take por faixa de NLA (o nome do clipe), como o maos_sniper.py."""
    ad = rig.animation_data
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    for a in clipes:
        nome = a.name.split("|", 1)[1]
        tr = ad.nla_tracks.new()
        tr.name = nome
        st = tr.strips.new(nome, int(a.frame_range[0]), a)
        st.name = nome
        if hasattr(st, "action_slot") and len(a.slots):
            st.action_slot = a.slots[0]
    ad.action = None
    for o in bpy.context.view_layer.objects:
        o.select_set(o in (rig, corpo, boca))
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=fp(cfg), use_selection=True, object_types={"ARMATURE", "MESH", "EMPTY"},
                             apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, mesh_smooth_type="FACE")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(AQUI, f"Bracos_{cfg.MODELO}.blend"))
    print("  exportado:", fp(cfg))


def rodar(cfg, etapas):
    import traceback
    if "tudo" in etapas:
        etapas = ["construir", "exportar", "imagens", "fotos", "bracos"]
    try:
        if any(e in etapas for e in ("construir", "exportar", "imagens", "fotos")):
            bpy.ops.wm.read_factory_settings(use_empty=True)
            construir(cfg)
            if "exportar" in etapas:
                exportar(cfg)
            if "imagens" in etapas:
                armas.imagens(cfg, usar_final=False)
            if "fotos" in etapas:
                armas.fotos(cfg, usar_final=False)
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(AQUI, f"{cfg.NOME}.blend"))
        if "bracos" in etapas or "conferir" in etapas:
            rig, corpo, boca, clipes = bracos(cfg)
            if "conferir" in etapas:
                conferir(rig, clipes, cfg)
            if "bracos" in etapas:
                exportar_bracos(rig, corpo, boca, clipes, cfg)
        print("ETAPAS OK")
    except Exception:
        traceback.print_exc()
        print("ETAPA FALHOU")
        sys.exit(1)


if __name__ == "__main__" and "--" in sys.argv:
    rodar(sys.modules[__name__], sys.argv[sys.argv.index("--") + 1:] or ["tudo"])
