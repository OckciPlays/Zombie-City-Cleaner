# A animação do tiro da sniper (FP_Rig_Sniper|Fire): o coice e o ferrolho (a mão direita sai do cabo, levanta a
# alavanca, puxa para trás, empurra, abaixa e volta). 30 quadros a 24 fps = 1,25 s, o intervalo entre os tiros.
# Como é feita: alvos de IK presos ao osso "gun" seguram as duas mãos onde elas estão no Idle do rifle; o coice mexe só
# a arma, e o caminho da mão direita é o do alvo dela. No fim tudo é assado em quadros-chave (FK), sem restrições.
import bpy, math
from mathutils import Vector, Matrix, Quaternion

QUADROS = 30


def usar_acao(rig, acao):
    """Liga a ação ao esqueleto. No Blender 5 a ação tem slots: sem ligar o slot, ela não mexe em nada (e a pose fica a
    que estava, como a do Sacar)."""
    ad = rig.animation_data
    ad.action = acao
    if hasattr(ad, "action_slot") and len(acao.slots):
        ad.action_slot = acao.slots[0]


def _pose_idle(rig):
    """Põe o esqueleto na pose do quadro 1 do Idle do rifle."""
    usar_acao(rig, bpy.data.actions["FP_Rig_Rifle|Idle"])
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    g = rig.pose.bones["gun"]
    assert g.location.length < 0.01, "a pose do Idle não pegou"


def _vazio(nome, rig, osso, matriz_mundo):
    if nome in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects[nome], do_unlink=True)
    e = bpy.data.objects.new(nome, None)
    bpy.context.scene.collection.objects.link(e)
    e.empty_display_size = 0.02
    e.parent = rig
    e.parent_type = "BONE"
    e.parent_bone = osso
    bpy.context.view_layer.update()
    e.matrix_world = matriz_mundo
    return e


def ferrolho_no_eixo(rig):
    """O osso do ferrolho (slide) passa a ficar no eixo do cano, para a alavanca girar em volta dele."""
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.view_layer.objects:
        o.select_set(o == rig)
    bpy.ops.object.mode_set(mode="EDIT")
    b = rig.data.edit_bones["slide"]
    rolo = b.roll
    b.head = Vector((-0.12, -0.13, -0.116))
    b.tail = Vector((-0.12, -0.20, -0.116))
    b.roll = rolo
    bpy.ops.object.mode_set(mode="OBJECT")


def _melhor_polo(rig, ik, ref, mao_osso):
    """O ângulo do polo que deixa o braço como no Idle (procura de 2 em 2 graus)."""
    ref = {n: ref[n] for n in ("upper_arm" + mao_osso, "forearm" + mao_osso)}
    melhor, erro_min = 0.0, 1e9
    for g in range(-180, 180, 2):
        ik.pole_angle = math.radians(g)
        bpy.context.view_layer.update()
        erro = sum((rig.pose.bones[n].matrix.translation - ref[n].translation).length +
                   (rig.pose.bones[n].matrix.to_quaternion().rotation_difference(ref[n].to_quaternion())).angle * 0.05
                   for n in ref)
        if erro < erro_min:
            erro_min, melhor = erro, g
    ik.pole_angle = math.radians(melhor)
    bpy.context.view_layer.update()
    return melhor, erro_min


def montar_tiro(rig):
    rig.animation_data_create()
    _pose_idle(rig)
    pb = rig.pose.bones
    M = rig.matrix_world
    # 1) a ação nova, com a pose do Idle em todos os ossos (no começo e no fim)
    nome = "FP_Rig_Sniper|Fire"
    if nome in bpy.data.actions:
        bpy.data.actions.remove(bpy.data.actions[nome])
    acao = bpy.data.actions.new(nome)
    usar_acao(rig, acao)
    for p in pb:
        p.rotation_mode = "QUATERNION"
        for f in (1, QUADROS):
            p.keyframe_insert("location", frame=f)
            p.keyframe_insert("rotation_quaternion", frame=f)
            p.keyframe_insert("scale", frame=f)

    # 2) alvos das mãos e polos dos cotovelos, presos ao osso gun (a mão acompanha a arma no coice)
    bpy.context.view_layer.update()
    ref = {p.name: p.matrix.copy() for p in pb}
    alvos = {}
    for lado in (".L", ".R"):
        mao = pb["hand" + lado]
        ante = pb["forearm" + lado]
        braco = pb["upper_arm" + lado]
        alvo = _vazio("alvo" + lado, rig, "gun", M @ mao.matrix)       # posição e giro da mão no Idle
        cotovelo = M @ ante.head
        meio = (M @ braco.head + M @ mao.head) / 2
        polo = _vazio("polo" + lado, rig, "gun", Matrix.Translation(cotovelo + (cotovelo - meio).normalized() * 0.3))
        ik = ante.constraints.new("IK")
        ik.target = alvo
        ik.pole_target = polo
        ik.chain_count = 2
        ik.use_tail = True
        cr = mao.constraints.new("COPY_TRANSFORMS")
        cr.target = alvo
        alvos[lado] = (alvo, polo, ik)
    # o alvo do IK é a ponta do antebraço (= a cabeça da mão): o Copy Transforms põe a mão no mesmo lugar
    for lado in (".L", ".R"):
        alvo, polo, ik = alvos[lado]
        print("polo", lado, _melhor_polo(rig, ik, ref, lado))

    # 3) o coice: a arma vai para trás e o cano sobe, e volta devagar
    g = pb["gun"]
    base_loc, base_rot = g.location.copy(), g.rotation_quaternion.copy()
    def chave_arma(f, recuo, sobe, gira=0.0):
        g.location = base_loc + Vector((0.0, -recuo, recuo * 0.15))
        g.rotation_quaternion = base_rot @ Quaternion((1, 0, 0), math.radians(sobe)) @ Quaternion((0, 1, 0), math.radians(gira))
        g.keyframe_insert("location", frame=f)
        g.keyframe_insert("rotation_quaternion", frame=f)
    chave_arma(1, 0, 0)
    chave_arma(2, 0.045, 6.0)
    chave_arma(4, 0.035, 4.5)
    chave_arma(8, 0.008, 1.0)
    # no ferrolho a arma tomba um pouco para a esquerda (a mão direita puxa)
    chave_arma(12, 0.0, 0.5, -6.0)
    chave_arma(19, 0.0, 0.3, -7.0)
    chave_arma(24, 0.0, 0.0, -2.0)
    chave_arma(QUADROS, 0, 0)

    # 4) o ferrolho: levanta a alavanca (gira em volta do cano), puxa, empurra e abaixa
    s = pb["slide"]
    s_loc, s_rot = s.location.copy(), s.rotation_quaternion.copy()
    def chave_ferrolho(f, giro, recuo):
        s.rotation_quaternion = s_rot @ Quaternion((0, 1, 0), math.radians(giro))
        s.location = s_loc + Vector((0.0, -recuo, 0.0))
        s.keyframe_insert("location", frame=f)
        s.keyframe_insert("rotation_quaternion", frame=f)
    for f, giro, recuo in ((10, 0, 0), (12, 62, 0), (15, 62, 0.075), (18, 62, 0.0), (20, 0, 0)):
        chave_ferrolho(f, giro, recuo)

    # 5) o caminho da mão direita: do cabo à alavanca, segue a alavanca e volta ao cabo
    alvo_r = alvos[".R"][0]
    bpy.context.view_layer.update()
    cabo = alvo_r.matrix_world.copy()
    def chave_mao(f, matriz):
        alvo_r.matrix_world = matriz
        bpy.context.view_layer.update()
        alvo_r.keyframe_insert("location", frame=f)
        alvo_r.keyframe_insert("rotation_quaternion" if alvo_r.rotation_mode == "QUATERNION" else "rotation_euler", frame=f)
    alvo_r.rotation_mode = "QUATERNION"
    # a mão na alavanca: o pulso um pouco atrás e para fora da bola, a palma virada para ela
    def na_alavanca(f):
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        bola = M @ (s.matrix @ s.bone.matrix_local.inverted() @ Vector((-0.192, -0.14, -0.15)))
        giro = cabo.to_quaternion()
        return Matrix.Translation(bola + Vector((-0.035, 0.07, 0.035))) @ (giro @ Quaternion((0, 0, 1), math.radians(-25))).to_matrix().to_4x4()
    chave_mao(1, cabo)
    chave_mao(8, cabo)
    for f in (11, 12, 15, 18, 20):
        chave_mao(f, na_alavanca(f))
    chave_mao(24, cabo)
    chave_mao(QUADROS, cabo)
    return acao


def assar(rig, acao):
    """Assa a pose de cada quadro em quadros-chave (FK) e tira as restrições e os alvos."""
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.view_layer.objects:
        o.select_set(o == rig)
    bpy.ops.object.mode_set(mode="POSE")
    for p in rig.pose.bones:
        if hasattr(p, "select"):
            p.select = True          # Blender 5: a seleção fica no osso da pose
        else:
            p.bone.select = True
    bpy.ops.nla.bake(frame_start=1, frame_end=QUADROS, only_selected=True, visual_keying=True, clear_constraints=True,
                     use_current_action=True, bake_types={"POSE"})
    bpy.ops.object.mode_set(mode="OBJECT")
    for nome in ("alvo.L", "alvo.R", "polo.L", "polo.R"):
        if nome in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[nome], do_unlink=True)
    return rig.animation_data.action
