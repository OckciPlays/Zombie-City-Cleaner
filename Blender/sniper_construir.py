# Refaz a sniper inteira e exporta os dois FBX para o projeto Unity:
#   Jogador_FP_Sniper.fbx (os braços de primeira pessoa com a sniper e as 7 animações) e
#   Armas_Sniper.fbx (a arma avulsa, para o corpo e o suporte).
# Precisa das cenas "Sniper" (Jogador_FP_Rifle.fbx importado) e "ArmasRef" (Armas.fbx importado).
# Exportação: escala "FBX_SCALE_ALL", sem ossos-folha e um take por faixa de NLA (o nome da faixa é o nome do clipe),
# o que reproduz os FBX que já estão no projeto (conferido: ida e volta sem diferença).
import bpy, math, os, importlib.util
from mathutils import Matrix, Vector

PASTA = os.path.dirname(os.path.abspath(__file__))
UNITY = os.path.join(os.path.dirname(PASTA), "Assets", "_Game", "Art", "Characters", "Jogador")


def _modulo(nome):
    spec = importlib.util.spec_from_file_location(nome, os.path.join(PASTA, nome + ".py"))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def construir():
    sm = _modulo("sniper_modelo")
    st = _modulo("sniper_tiro")
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.window.scene = bpy.data.scenes["Sniper"]
    rifle = bpy.data.objects["FP_Rig_Rifle"]
    for o in (rifle, bpy.data.objects["FP_Bracos_Rifle"], bpy.data.objects["Boca_FP_Rifle"]):
        o.hide_set(False)
    # o rifle no Idle (com o slot ligado): é dele que a cópia do esqueleto sai
    rifle.animation_data_create()
    st.usar_acao(rifle, bpy.data.actions["FP_Rig_Rifle|Idle"])
    bpy.context.scene.frame_set(1)
    rig, bra, boca = sm.nova_sniper_fp()
    # a boca com o giro igual ao da boca do rifle (no repouso)
    rifle.data.pose_position = "REST"; rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    giro = bpy.data.objects["Boca_FP_Rifle"].matrix_world.to_quaternion()
    boca.matrix_world = Matrix.Translation(Vector((sm.CX, -1.155, sm.EIXO_Z))) @ giro.to_matrix().to_4x4()
    rifle.data.pose_position = "POSE"; rig.data.pose_position = "POSE"
    for o in (rifle, bpy.data.objects["FP_Bracos_Rifle"], bpy.data.objects["Boca_FP_Rifle"]):
        o.hide_set(True)

    st.ferrolho_no_eixo(rig)
    acao = st.montar_tiro(rig)
    acao = st.assar(rig, acao)

    # um take por faixa de NLA
    ad = rig.animation_data
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    fontes = {n: bpy.data.actions["FP_Rig_Rifle|" + n] for n in ("Idle", "Walk", "Run", "DryFire", "Reload", "Sacar")}
    fontes["Fire"] = bpy.data.actions["FP_Rig_Sniper|Fire"]
    for nome in ("Idle", "Walk", "Run", "Fire", "DryFire", "Reload", "Sacar"):
        a = fontes[nome]
        tr = ad.nla_tracks.new(); tr.name = nome
        s = tr.strips.new(nome, int(a.frame_range[0]), a)
        s.name = nome
        if hasattr(s, "action_slot") and len(a.slots):
            s.action_slot = a.slots[0]
    ad.action = None

    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in (rig, bra, boca):
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=os.path.join(UNITY, "Jogador_FP_Sniper.fbx"), use_selection=True, object_types={"ARMATURE", "MESH", "EMPTY"},
                             apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False, bake_anim=True, bake_anim_use_nla_strips=True,
                             bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                             mesh_smooth_type="FACE")

    bpy.context.window.scene = bpy.data.scenes["ArmasRef"]
    av = sm.nova_sniper_avulsa()
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    av.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(UNITY, "Armas_Sniper.fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")
    bpy.context.window.scene = bpy.data.scenes["Sniper"]
    return rig
