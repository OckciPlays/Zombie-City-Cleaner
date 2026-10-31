# A mão esquerda da submetralhadora na empunhadura vertical da MP5K (30/10/2026, no conceito da sniper: "faça com que o
# personagem segure no cabo"): os braços de primeira pessoa da SMG (Jogador_FP_Submetralhadora.fbx) com a mão esquerda
# levada da frente do receptor da SMG antiga (onde ela abraçava a caixa por baixo) para a empunhadura vertical da v3
# (v3_mp5.py), girando para abraçar um cabo em pé. Quando a mão sai da arma (na recarga, para trocar o carregador), ela
# segue o caminho original; perto da arma, vai para a empunhadura. A mão direita e o resto não mudam.
#
# Como é feito (como no maos_sniper.py): em cada quadro de cada clipe, a mão no espaço da arma solta (o cano em -Y, o
# alto em +Z) passa pela troca (o deslocamento e o giro que levam a pose de segurar do Idle até a empunhadura), pesada
# pela distância até a pose de segurar; um alvo de IK leva o braço até lá, e tudo é assado em quadros-chave. A exportação
# é a do maos_sniper.py (um take por faixa de NLA).
#
# Como rodar (num Blender em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/Armas/maos_smg.py -- [medir|conferir|exportar]
# "medir" mostra a mão na pose de segurar; "conferir" grava fotos com a MP5K em Docs/Armas/rascunho; "exportar" grava o
# Jogador_FP_Submetralhadora.fbx (o original está em Backups/Submetralhadora_antes_da_v3_2026-10-30/Jogador) e o
# Bracos_Submetralhadora.blend.

import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import armas  # noqa: E402
import maos_sniper as ms  # noqa: E402
import v3_mp5  # noqa: E402

FBX = os.path.join(armas.JOGADOR, "Jogador_FP_Submetralhadora.fbx")
# o original (antes do ajuste): o ajuste parte sempre dele, para não se acumular
ORIGINAL = os.path.join(armas.REPO, "Backups", "Submetralhadora_antes_da_v3_2026-10-30", "Jogador",
                        "Jogador_FP_Submetralhadora.fbx")
LADO = "L"
DEDOS = ("index", "middle", "ring", "pinky")
# o meio da empunhadura vertical. Na antiga, a mão abraçava a frente do receptor por baixo: os dedos dobravam em volta
# do eixo do cano (deitado). Na empunhadura, o mesmo abraço gira para o eixo dela (em pé): o indicador em cima (junto do
# receptor), o mindinho embaixo, a palma virada para o cabo (DIR_PALMA: para a direita e um pouco para a frente, com a mão
# do lado esquerdo e um pouco atrás dele). O miolo da mão (a palma e os dedos) fica R_MAO m do eixo do cabo, do lado
# de trás da palma, na altura do meio da empunhadura.
EMPUNHADURA = v3_mp5.EMPUNHADURA
DIR_PALMA = Vector((-1.0, -0.30, 0.0)).normalized()
R_MAO = 0.024
ALTURA_RECEPTOR = 0.110     # o meio da caixa do receptor antigo (o que a mão abraçava)
LONGE = 0.08        # a partir desta distância da pose de segurar, a mão segue só o caminho original


def _encaixe(rig, bracos):
    """O encaixe da SMG antiga solta (Armas.fbx) na arma dos braços (no repouso)."""
    me = bracos.data
    grupos = {g.index: g.name for g in bracos.vertex_groups}

    def osso(v):
        return grupos[max(v.groups, key=lambda g: g.weight).group] if len(v.groups) else ""

    mw = bracos.matrix_world
    da_arma = {i for p in me.polygons if p.material_index == 1 for i in p.vertices}
    destino = [mw @ me.vertices[i].co for i in da_arma]
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(armas.JOGADOR, "Armas.fbx"))
    soltas = [o for o in bpy.data.objects if o not in antes]
    antigas = [o for o in soltas if o.type == "MESH" and o.name.startswith("Submetralhadora_")]
    org = next(o for o in antigas if o.name.startswith("Submetralhadora_corpo")).matrix_world.translation.copy()
    origem = [o.matrix_world @ v.co - org for o in antigas for v in o.data.vertices]
    E, erro = armas.encaixe(destino, origem)
    for o in soltas:
        bpy.data.objects.remove(o, do_unlink=True)
    print(f"  encaixe: {erro * 1000:.2f} mm")
    return E


def _miolo(rig):
    """O miolo da mão (no mundo): a média das cabeças e pontas da mão e dos dedos."""
    pb = rig.pose.bones
    M = rig.matrix_world
    pts = [M @ pb[f"hand.{LADO}"].head]
    for d in DEDOS:
        for k in ("1", "2"):
            b = pb.get(f"{d}{k}.{LADO}")
            if b is not None:
                pts += [M @ b.head, M @ b.tail]
    return sum(pts, Vector()) / len(pts)


def _quadro(eixo, palma):
    """Uma base ortonormal: X no eixo do abraço, Y na palma (sem a parte no eixo), Z o terceiro."""
    x = eixo.normalized()
    y = (palma - x * palma.dot(x)).normalized()
    return Matrix((x, y, x.cross(y))).transposed()


def importar():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=ORIGINAL)
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    bracos = next(o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("FP_Bracos"))
    boca = next(o for o in bpy.data.objects if o.name.startswith("Boca_FP"))
    rig.animation_data_create()
    E = _encaixe(rig, bracos)
    clipes = [a for a in bpy.data.actions if a.name.startswith(rig.name + "|")]
    return rig, bracos, boca, E, clipes


def pose_de_segurar(rig, E, clipes):
    """A mão na pose de segurar (o Idle, quadro 1) no espaço da arma: a matriz dela, o miolo, o eixo do abraço (do
    mindinho ao indicador) e a direção da palma (o eixo local que aponta para o que ela abraça)."""
    idle = next(a for a in clipes if a.name.endswith("|Idle"))
    ms._usar(rig, idle)
    bpy.context.scene.frame_set(int(idle.frame_range[0]))
    L = ms._da_arma(rig, E)
    Li = L.inverted()
    M = rig.matrix_world
    pb = rig.pose.bones
    H = Li @ M @ pb[f"hand.{LADO}"].matrix
    miolo = Li @ _miolo(rig)
    indicador = Li @ (M @ pb[f"index1.{LADO}"].head)
    mindinho = Li @ (M @ pb[f"pinky1.{LADO}"].head)
    abraco = (Vector((0.0, miolo.y, ALTURA_RECEPTOR)) - miolo).normalized()
    R = H.to_3x3()
    eixos = {"x": R.col[0], "-x": -R.col[0], "z": R.col[2], "-z": -R.col[2]}
    palma = max(eixos, key=lambda k: eixos[k].dot(abraco))
    return H, miolo, indicador, mindinho, eixos[palma].normalized(), palma


def alvo_na_empunhadura(H, miolo, indicador, mindinho, palma):
    """A troca X (no espaço da arma) que leva o abraço da frente do receptor antigo para a empunhadura em pé."""
    F0 = _quadro(indicador - mindinho, palma)
    F1 = _quadro(Vector((0.0, 0.0, 1.0)), DIR_PALMA)
    R = (F1 @ F0.transposed()).to_4x4()
    return Matrix.Translation(EMPUNHADURA - DIR_PALMA * R_MAO) @ R @ Matrix.Translation(-miolo)


def medir(rig, E, clipes):
    H, miolo, indicador, mindinho, palma, nome = pose_de_segurar(rig, E, clipes)
    print(f"  mão {LADO} (Idle): cabeça {tuple(round(v, 3) for v in H.translation)}  miolo {tuple(round(v, 3) for v in miolo)}")
    print(f"    indicador {tuple(round(v, 3) for v in indicador)}  mindinho {tuple(round(v, 3) for v in mindinho)}")
    print(f"    palma ({nome}): {tuple(round(v, 2) for v in palma)}")
    X = alvo_na_empunhadura(H, miolo, indicador, mindinho, palma)
    T = X @ H
    print("  depois: cabeça", tuple(round(v, 3) for v in T.translation), " miolo", tuple(round(v, 3) for v in X @ miolo),
          " indicador", tuple(round(v, 3) for v in X @ indicador), " mindinho", tuple(round(v, 3) for v in X @ mindinho))


def ajustar_clipe(rig, acao, E, H0, X):
    """Como o maos_sniper.ajustar_clipe, para a mão esquerda: perto da pose de segurar, a troca X; longe, o original."""
    cena = bpy.context.scene
    ms._usar(rig, acao)
    f0, f1 = int(acao.frame_range[0]), int(acao.frame_range[1])
    pb = rig.pose.bones
    M = rig.matrix_world
    quadros = {}
    for f in range(f0, f1 + 1):
        cena.frame_set(f)
        L = ms._da_arma(rig, E)
        quadros[f] = (L.copy(), (M @ pb[f"hand.{LADO}"].matrix).copy(), (M @ pb[f"forearm.{LADO}"].head).copy(),
                      (M @ pb[f"upper_arm.{LADO}"].head).copy())
    alvo = ms._vazio(f"alvo.{LADO}")
    polo = ms._vazio(f"polo.{LADO}")
    pesos = []
    for f, (L, mao, cot, omb) in quadros.items():
        h = L.inverted() @ mao
        d = (h.translation - H0.translation).length
        w = max(0.0, 1.0 - d / LONGE)
        w = w * w * (3 - 2 * w)
        pesos.append(w)
        trocada = X @ h
        p = h.translation.lerp(trocada.translation, w)
        q = h.to_quaternion().slerp(trocada.to_quaternion(), w)
        nova = L @ (Matrix.Translation(p) @ q.to_matrix().to_4x4())
        alvo.location = nova.translation
        alvo.rotation_quaternion = nova.to_quaternion()
        alvo.keyframe_insert("location", frame=f)
        alvo.keyframe_insert("rotation_quaternion", frame=f)
        meio = (omb + mao.translation) / 2
        polo.location = cot + (cot - meio).normalized() * 0.3 + (nova.translation - mao.translation)
        polo.keyframe_insert("location", frame=f)
    ante, mao_b = pb[f"forearm.{LADO}"], pb[f"hand.{LADO}"]
    for b in (ante, mao_b):
        for c in list(b.constraints):
            b.constraints.remove(c)
    ik = ante.constraints.new("IK")
    ik.target = alvo
    ik.pole_target = polo
    ik.chain_count = 2
    ik.use_tail = True
    cp = mao_b.constraints.new("COPY_TRANSFORMS")
    cp.target = alvo
    cena.frame_set(f0)
    melhor, erro_min = 0.0, 1e9
    for g in range(-180, 180, 3):
        ik.pole_angle = math.radians(g)
        bpy.context.view_layer.update()
        e = ((M @ pb[f"forearm.{LADO}"].head) - quadros[f0][2]).length
        if e < erro_min:
            erro_min, melhor = e, g
    ik.pole_angle = math.radians(melhor)
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.view_layer.objects:
        o.select_set(o == rig)
    bpy.ops.object.mode_set(mode="POSE")
    for p in pb:
        if hasattr(p, "select"):
            p.select = True
        else:
            p.bone.select = True
    bpy.ops.nla.bake(frame_start=f0, frame_end=f1, only_selected=True, visual_keying=True, clear_constraints=True,
                     use_current_action=True, bake_types={"POSE"})
    bpy.ops.object.mode_set(mode="OBJECT")
    for nome in (f"alvo.{LADO}", f"polo.{LADO}"):
        bpy.data.objects.remove(bpy.data.objects[nome], do_unlink=True)
    na = sum(1 for w in pesos if w > 0.99)
    print(f"  {acao.name}: quadros {f0}-{f1} ({na} na empunhadura), polo {melhor}°, cotovelo a "
          f"{erro_min * 100:.1f} cm do original")


def ajustar(rig, E, clipes):
    H0, miolo, indicador, mindinho, palma, _ = pose_de_segurar(rig, E, clipes)
    X = alvo_na_empunhadura(H0, miolo, indicador, mindinho, palma)
    for a in clipes:
        ajustar_clipe(rig, a, E, H0, X)


def conferir(rig, E, clipes):
    """Fotos com a MP5K (o corpo, a alavanca e o carregador na arma), no Idle, no tiro e no meio da recarga."""
    with bpy.data.libraries.load(os.path.join(AQUI, "Submetralhadora3.blend"), link=False) as (src, dst):
        dst.objects = ["Submetralhadora3_corpo", "Submetralhadora3_ferrolho", "Submetralhadora3_carregador"]
    pecas = [o for o in dst.objects if o is not None]
    for o in pecas:
        bpy.context.scene.collection.objects.link(o)
    cena = bpy.context.scene
    r = cena.render
    r.engine = "BLENDER_WORKBENCH"
    s = cena.display.shading
    s.light = "STUDIO"
    s.color_type = "MATERIAL"
    s.show_cavity = True
    r.resolution_x, r.resolution_y = 1100, 700
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cena.collection.objects.link(cam)
    cena.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 0.6
    pasta = os.path.join(armas.REPO, "Docs", "Armas", "rascunho")
    for nome_clipe in ("Idle", "Fire", "Reload"):
        a = next(a for a in clipes if a.name.endswith("|" + nome_clipe))
        ms._usar(rig, a)
        f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
        for f in sorted({f0, (f0 + f1) // 2} if nome_clipe == "Reload" else {f0}):
            cena.frame_set(f)
            L = ms._da_arma(rig, E)
            for o in pecas:
                o.matrix_world = L
            alvo = L @ Vector((0.0, -0.12, 0.04))
            for vista, d in (("direita", Vector((-1, 0.0, 0.1))), ("esquerda", Vector((1, 0.0, 0.1))),
                             ("frente", Vector((0.3, -1, 0.2))), ("camera", Vector((0.15, 0.9, 0.45)))):
                dd = (L.to_3x3() @ d).normalized()
                cam.location = alvo + dd * 2
                cam.rotation_euler = (-dd).to_track_quat("-Z", "Y").to_euler()
                r.filepath = os.path.join(pasta, f"maos_smg_{nome_clipe.lower()}_{f}_{vista}.png")
                bpy.ops.render.render(write_still=True)
    for o in pecas:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    print("  fotos de conferência em", pasta)


def exportar(rig, bracos, boca, clipes):
    """Um take por faixa de NLA (o nome do clipe), como o maos_sniper.py; grava também o Bracos_Submetralhadora.blend."""
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
        o.select_set(o in (rig, bracos, boca))
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={"ARMATURE", "MESH", "EMPTY"},
                             apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, mesh_smooth_type="FACE")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(AQUI, "Bracos_Submetralhadora.blend"))
    print("  exportado:", FBX)


if __name__ == "__main__" and "--" in sys.argv:
    import traceback
    etapas = sys.argv[sys.argv.index("--") + 1:] or ["medir"]
    try:
        rig, bracos, boca, E, clipes = importar()
        print("  clipes:", [a.name for a in clipes])
        print("  ossos da mão:", [b.name for b in rig.pose.bones if b.name.endswith("." + LADO)][:30])
        if "medir" in etapas:
            medir(rig, E, clipes)
        if "conferir" in etapas or "exportar" in etapas:
            ajustar(rig, E, clipes)
        if "conferir" in etapas:
            conferir(rig, E, clipes)
        if "exportar" in etapas:
            exportar(rig, bracos, boca, clipes)
        print("ETAPAS OK")
    except Exception:
        traceback.print_exc()
        print("ETAPA FALHOU")
        sys.exit(1)
