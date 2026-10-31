# A mão direita da sniper no punho da Mosin (30/10/2026, a pedido: "faça com que o personagem segure no cabo e não crie
# um apoio na sniper"): os braços de primeira pessoa da sniper (Jogador_FP_Sniper.fbx) com a mão direita levada do cabo
# da sniper antiga (quase em pé) para o punho da coronha da Mosin (quase deitado), girando junto; no tiro, ela vai à
# alavanca da Mosin (25 cm à frente de onde ficava a antiga) e volta. A mão esquerda e o resto não mudam.
#
# Como é feito: em cada quadro de cada clipe, a mão no espaço da arma (o da arma solta: o cano em -Y, o alto em +Z) passa
# pela troca (o giro em volta do cabo antigo e o deslocamento até o punho; no ferrolho, o deslocamento entre as
# alavancas); um alvo de IK leva o braço (ombro e cotovelo) até lá, e tudo é assado em quadros-chave. A exportação é a do
# sniper_construir.py (um take por faixa de NLA), que a Unity lê igual.
#
# Como rodar (num Blender em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/Armas/maos_sniper.py -- [conferir|exportar]
# "conferir" grava fotos com a Mosin em Docs/Armas/rascunho; "exportar" grava o Jogador_FP_Sniper.fbx (o original está
# em Backups/Armas_antes_do_visual_novo_2026-10-29/Jogador) e o Bracos_Sniper.blend.

import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import armas  # noqa: E402

FBX = os.path.join(armas.JOGADOR, "Jogador_FP_Sniper.fbx")
# o original (da pasta de arte, antes do ajuste): o ajuste parte sempre dele, para não se acumular
ORIGINAL = os.path.join(armas.REPO, "Backups", "Armas_antes_do_visual_novo_2026-10-29", "Jogador", "Jogador_FP_Sniper.fbx")
# no espaço da arma solta: o meio do cabo antigo (na altura da mão) e o do punho da Mosin, o giro (em X) que leva o
# eixo do cabo (em pé) ao do punho (deitado, caindo para trás) e as pontas das duas alavancas
CABO = Vector((0.0, 0.030, 0.006))
PUNHO = Vector((0.0, -0.012, 0.069))
GIRO = 88.0
ALAVANCA_ANTIGA = Vector((-0.0915, 0.1079, 0.0857))
ALAVANCA_NOVA = Vector((-0.0571, -0.1372, 0.1142))
# na alavanca, a mão um pouco mais baixa e mais para a direita que na antiga (a alavanca da Mosin fica embaixo da
# luneta: sem isto, a mão passava por cima dela)
NA_ALAVANCA = Vector((-0.025, 0.0, -0.030))
LONGE = 0.10        # a partir desta distância do cabo (no tiro), a mão segue só a alavanca


def _usar(rig, acao):
    ad = rig.animation_data
    ad.action = acao
    if hasattr(ad, "action_slot") and len(acao.slots):
        ad.action_slot = acao.slots[0]


def _encaixe(rig, bracos):
    """O encaixe da arma solta antiga na arma dos braços (no repouso) e a matriz de repouso do osso gun."""
    me = bracos.data
    grupos = {g.index: g.name for g in bracos.vertex_groups}

    def osso(v):
        return grupos[max(v.groups, key=lambda g: g.weight).group] if len(v.groups) else ""

    mw = bracos.matrix_world
    da_arma = {i for p in me.polygons if p.material_index == 1 for i in p.vertices}
    destino = [mw @ me.vertices[i].co for i in da_arma if osso(me.vertices[i]) != "shell"]
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(armas.JOGADOR, "Armas_Sniper.fbx"))
    soltas = [o for o in bpy.data.objects if o not in antes]
    antigas = [o for o in soltas if o.type == "MESH" and o.name.startswith("Sniper_")]
    org = next(o for o in antigas if o.name.startswith("Sniper_corpo")).matrix_world.translation.copy()
    origem = [o.matrix_world @ v.co - org for o in antigas for v in o.data.vertices]
    E, erro = armas.encaixe(destino, origem)
    for o in soltas:
        bpy.data.objects.remove(o, do_unlink=True)
    print(f"  encaixe: {erro * 1000:.2f} mm")
    return E


def _da_arma(rig, E):
    """A matriz que leva o espaço da arma solta ao mundo, no quadro atual (a arma anda com o osso gun)."""
    pb = rig.pose.bones["gun"]
    M = rig.matrix_world
    return M @ pb.matrix @ pb.bone.matrix_local.inverted() @ M.inverted() @ E


def _troca(h, w):
    """A mão (no espaço da arma solta) depois da troca: no cabo (w 0) ou na alavanca (w 1)."""
    no_punho = Matrix.Translation(PUNHO) @ Matrix.Rotation(math.radians(GIRO), 4, "X") @ Matrix.Translation(-CABO) @ h
    if w <= 0:
        return no_punho
    na_alavanca = Matrix.Translation(ALAVANCA_NOVA - ALAVANCA_ANTIGA + NA_ALAVANCA) @ h
    if w >= 1:
        return na_alavanca
    p = no_punho.translation.lerp(na_alavanca.translation, w)
    q = no_punho.to_quaternion().slerp(na_alavanca.to_quaternion(), w)
    return Matrix.Translation(p) @ q.to_matrix().to_4x4()


def _vazio(nome):
    o = bpy.data.objects.get(nome)
    if o is not None:
        bpy.data.objects.remove(o, do_unlink=True)
    o = bpy.data.objects.new(nome, None)
    bpy.context.scene.collection.objects.link(o)
    o.rotation_mode = "QUATERNION"
    return o


def ajustar_clipe(rig, acao, E):
    cena = bpy.context.scene
    _usar(rig, acao)
    f0, f1 = int(acao.frame_range[0]), int(acao.frame_range[1])
    pb = rig.pose.bones
    M = rig.matrix_world
    # 1) a pose original de cada quadro: a mão e o cotovelo
    quadros = {}
    for f in range(f0, f1 + 1):
        cena.frame_set(f)
        L = _da_arma(rig, E)
        mao = M @ pb["hand.R"].matrix
        cot = M @ pb["forearm.R"].head
        omb = M @ pb["upper_arm.R"].head
        quadros[f] = (L.copy(), mao.copy(), cot.copy(), omb.copy())
    h0 = quadros[f0][0].inverted() @ quadros[f0][1]
    # 2) os alvos: a mão trocada e o polo do cotovelo (do lado de fora do braço, como no original)
    alvo = _vazio("alvo.R")
    polo = _vazio("polo.R")
    for f, (L, mao, cot, omb) in quadros.items():
        h = L.inverted() @ mao
        d = (h.translation - h0.translation).length
        w = 0.0 if "Fire" not in acao.name else min(1.0, d / LONGE)
        w = w * w * (3 - 2 * w)
        nova = L @ _troca(h, w)
        alvo.location = nova.translation
        alvo.rotation_quaternion = nova.to_quaternion()
        alvo.keyframe_insert("location", frame=f)
        alvo.keyframe_insert("rotation_quaternion", frame=f)
        meio = (omb + mao.translation) / 2
        polo.location = cot + (cot - meio).normalized() * 0.3 + (nova.translation - mao.translation)
        polo.keyframe_insert("location", frame=f)
    # 3) o IK no antebraço e a mão copiando o alvo
    ante, mao_b = pb["forearm.R"], pb["hand.R"]
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
    # o ângulo do polo que deixa o cotovelo mais perto do original (no primeiro quadro)
    cena.frame_set(f0)
    melhor, erro_min = 0.0, 1e9
    for g in range(-180, 180, 3):
        ik.pole_angle = math.radians(g)
        bpy.context.view_layer.update()
        e = ((M @ pb["forearm.R"].head) - quadros[f0][2]).length
        if e < erro_min:
            erro_min, melhor = e, g
    ik.pole_angle = math.radians(melhor)
    # 4) assa tudo em quadros-chave (FK) e tira as restrições
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
    for nome in ("alvo.R", "polo.R"):
        bpy.data.objects.remove(bpy.data.objects[nome], do_unlink=True)
    print(f"  {acao.name}: quadros {f0}-{f1}, polo {melhor}°, cotovelo a {erro_min * 100:.1f} cm do original")


def ajustar():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=ORIGINAL)
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    bracos = next(o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("FP_Bracos"))
    boca = next(o for o in bpy.data.objects if o.name.startswith("Boca_FP"))
    print("  boca: pai", boca.parent.name if boca.parent else None, boca.parent_type, boca.parent_bone)
    rig.animation_data_create()
    E = _encaixe(rig, bracos)
    clipes = [a for a in bpy.data.actions if a.name.startswith(rig.name + "|")]
    for a in clipes:
        ajustar_clipe(rig, a, E)
    return rig, bracos, boca, E, clipes


def conferir(rig, E, clipes):
    """Fotos de lado com a Mosin v3 (o corpo e o ferrolho na arma), no Idle e no meio do ferrolho."""
    with bpy.data.libraries.load(os.path.join(AQUI, "Sniper3.blend"), link=False) as (src, dst):
        dst.objects = ["Sniper3_corpo", "Sniper3_ferrolho"]
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
    cam.data.ortho_scale = 0.75
    pasta = os.path.join(armas.REPO, "Docs", "Armas", "rascunho")
    for nome_clipe, quadros in (("Idle", (1,)), ("Fire", (1, 12, 15, 19))):
        a = next(a for a in clipes if a.name.endswith("|" + nome_clipe))
        _usar(rig, a)
        for f in quadros:
            cena.frame_set(f)
            L = _da_arma(rig, E)
            for o in pecas:
                o.matrix_world = L
            alvo = L @ Vector((0.0, -0.05, 0.06))
            for vista, d in (("direita", Vector((-1, 0.0, 0.1))), ("esquerda", Vector((1, 0.0, 0.1)))):
                dd = (L.to_3x3() @ d).normalized()
                cam.location = alvo + dd * 2
                cam.rotation_euler = (-dd).to_track_quat("-Z", "Y").to_euler()
                r.filepath = os.path.join(pasta, f"maos_sniper_{nome_clipe.lower()}_{f}_{vista}.png")
                bpy.ops.render.render(write_still=True)
    for o in pecas:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    print("  fotos de conferência em", pasta)


def exportar(rig, bracos, boca, clipes):
    """Um take por faixa de NLA (o nome do clipe), como o sniper_construir.py; grava também o Bracos_Sniper.blend."""
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
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(AQUI, "Bracos_Sniper.blend"))
    print("  exportado:", FBX)


if __name__ == "__main__" and "--" in sys.argv:
    import traceback
    etapas = sys.argv[sys.argv.index("--") + 1:] or ["conferir"]
    try:
        rig, bracos, boca, E, clipes = ajustar()
        if "conferir" in etapas:
            conferir(rig, E, clipes)
        if "exportar" in etapas:
            exportar(rig, bracos, boca, clipes)
        print("ETAPAS OK")
    except Exception:
        traceback.print_exc()
        print("ETAPA FALHOU")
        sys.exit(1)
