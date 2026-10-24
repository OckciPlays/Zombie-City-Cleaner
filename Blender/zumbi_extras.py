# As animações novas dos zumbis (desmembramento): cair, rastejar, rastejar parado e mancar (sem a perna esquerda ou sem a
# direita). Saem num FBX por modelo, <modelo>_Extra.fbx, com o esqueleto e a malha (a malha só mantém a hierarquia igual
# à do <modelo>.fbx: os caminhos dos ossos batem, e os clipes servem no prefab do zumbi).
# Antes de rodar: importe o zumbi antigo (NovoZombie_antigo.fbx, veja o README) numa cena e mude o nome do esqueleto
# para "Antigo_Rig"; depois, construir(modelo, bpy.data.objects["Antigo_Rig"]) para cada um dos 4 modelos.
# - Cair, Rastejar e RastejarParado vêm do zumbi antigo (NovoZombie, Parte 6), transferidos osso a osso: cada osso faz,
#   no mundo, o mesmo giro em relação ao repouso que fazia no antigo (os repousos dos dois esqueletos são quase iguais),
#   e o quadril anda na proporção da altura.
# - Mancar sai do Walk do próprio modelo: na fase em que a perna perdida pisaria, o corpo cai e tomba para o lado dela, o
#   tronco compensa e a perna que sobrou segura; o toco balança pouco.
import bpy, math, os
from mathutils import Matrix, Vector, Quaternion

PASTA = os.path.dirname(os.path.abspath(__file__))
# o zumbi antigo (Parte 6), tirado do histórico do git: veja o README desta pasta
FONTE = os.path.join(PASTA, "NovoZombie_antigo.fbx")
ZUMBIS = os.path.join(os.path.dirname(PASTA), "Assets", "_Game", "Art", "Characters", "Zombie")
TRANSFERIR = {"Fall": "Cair", "Crawl": "Rastejar", "CrawlIdle": "RastejarParado"}


def usar_acao(obj, acao):
    ad = obj.animation_data or obj.animation_data_create()
    ad.action = acao
    if acao is not None and hasattr(ad, "action_slot") and len(acao.slots):
        ad.action_slot = acao.slots[0]


def ordem(arm):
    """Os ossos dos pais para os filhos."""
    def prof(b):
        n = 0
        while b.parent:
            b = b.parent; n += 1
        return n
    return sorted(arm.data.bones, key=prof)


def _chaves(arm, f, poses):
    """Grava a pose (matrizes no espaço do esqueleto, por osso) no quadro f, como base de cada osso."""
    rest = {b.name: b.matrix_local for b in arm.data.bones}
    for b in ordem(arm):
        M = poses[b.name]
        if b.parent:
            P = poses[b.parent.name]
            base = (rest[b.parent.name].inverted() @ rest[b.name]).inverted() @ P.inverted() @ M
        else:
            base = rest[b.name].inverted() @ M
        pb = arm.pose.bones[b.name]
        pb.rotation_mode = "QUATERNION"
        loc, rot, _ = base.decompose()
        pb.location = loc
        pb.rotation_quaternion = rot
        pb.scale = (1, 1, 1)
        pb.keyframe_insert("location", frame=f)
        pb.keyframe_insert("rotation_quaternion", frame=f)
        pb.keyframe_insert("scale", frame=f)


def transferir(fonte, acao_fonte, alvo, nome):
    """O clipe do esqueleto antigo no esqueleto novo (o mesmo giro de cada osso em relação ao repouso, no mundo)."""
    cena = bpy.context.scene
    if fonte.name not in cena.objects:
        cena.collection.objects.link(fonte)
    usar_acao(fonte, acao_fonte)
    Sr = {b.name: b.matrix_local for b in fonte.data.bones}
    Tr = {b.name: b.matrix_local for b in alvo.data.bones}
    k = Tr["hips"].translation.z / Sr["hips"].translation.z
    nova = bpy.data.actions.new("Zombie_Rig|Zombie_" + nome)
    usar_acao(alvo, nova)
    f0, f1 = (int(x) for x in acao_fonte.frame_range)
    for f in range(f0, f1 + 1):
        cena.frame_set(f)
        S = {p.name: p.matrix.copy() for p in fonte.pose.bones}
        poses = {}
        for b in ordem(alvo):
            n = b.name
            if n not in S:
                poses[n] = Tr[n]
                continue
            giro = S[n].to_quaternion() @ Sr[n].to_quaternion().inverted() @ Tr[n].to_quaternion()
            if b.parent is None:
                pos = Tr[n].translation
            elif n == "hips":
                pos = Tr[n].translation + (S[n].translation - Sr[n].translation) * k
            else:
                pos = (poses[b.parent.name] @ (Tr[b.parent.name].inverted() @ Tr[n])).translation
            poses[n] = Matrix.Translation(pos) @ giro.to_matrix().to_4x4()
        _chaves(alvo, f - f0 + 1, poses)
    cena.collection.objects.unlink(fonte)
    return nova


def mancar(alvo, walk, lado):
    """Mancar sem a perna do lado ("L" ou "R"), a partir do Walk do modelo."""
    cena = bpy.context.scene
    usar_acao(alvo, walk)
    f0, f1 = (int(x) for x in walk.frame_range)
    pe = alvo.pose.bones["foot." + lado]
    # a fase em que a perna perdida pisa: o pé dela embaixo (o mais baixo do ciclo)
    alturas = []
    for f in range(f0, f1 + 1):
        cena.frame_set(f)
        alturas.append(pe.head.z)
    hmin, hmax = min(alturas), max(alturas)
    poses_walk = []
    for f in range(f0, f1 + 1):
        cena.frame_set(f)
        poses_walk.append({p.name: p.matrix.copy() for p in alvo.pose.bones})
    nova = bpy.data.actions.new("Zombie_Rig|Zombie_Mancar" + ("E" if lado == "L" else "D"))
    usar_acao(alvo, nova)
    Tr = {b.name: b.matrix_local for b in alvo.data.bones}
    sinal = 1.0 if lado == "L" else -1.0          # o lado perdido é +X (esquerda do zumbi) ou -X
    n = f1 - f0 + 1
    for i in range(n):
        P = dict(poses_walk[i])
        # peso da queda: 1 quando o pé perdido estaria pisando (embaixo), 0 no ar
        w = 1.0 - min(1.0, max(0.0, (alturas[i] - hmin) / max(1e-4, (hmax - hmin) * 0.6)))
        w = w * w * (3 - 2 * w)
        quadril = P["hips"]
        # o quadril cai e tomba para o lado perdido (gira em volta da frente, -Y no Blender)
        tomba = Matrix.Rotation(math.radians(9.0 * w * sinal), 4, Vector((0, -1, 0)))
        cabeca_q = quadril.translation.copy()
        nova_quadril = Matrix.Translation(cabeca_q + Vector((0.025 * sinal * w, 0, -0.09 * w))) @ tomba @ Matrix.Translation(-cabeca_q) @ quadril
        delta = nova_quadril @ quadril.inverted()
        # tudo que pende do quadril vai junto (o resto do esqueleto é refeito pelo giro de cada osso)
        filhos = [b.name for b in alvo.data.bones if b.name != "root" and b.name != "hips"]
        for nm in filhos:
            P[nm] = delta @ P[nm]
        P["hips"] = nova_quadril
        # o tronco compensa para o outro lado (o peito e o pescoço de volta, metade do tombo)
        for nm, parte in (("spine", 0.35), ("chest", 0.35)):
            b = alvo.data.bones[nm]
            centro = P[nm].translation.copy()
            volta = Matrix.Translation(centro) @ Matrix.Rotation(math.radians(-9.0 * w * sinal * parte), 4, Vector((0, -1, 0))) @ Matrix.Translation(-centro)
            for d in [nm] + [c.name for c in b.children_recursive]:
                P[d] = volta @ P[d]
        # o toco da perna perdida: a coxa levantada para a frente, quase parada
        coxa = "thigh." + lado
        b = alvo.data.bones[coxa]
        centro = P[coxa].translation.copy()
        levanta = Matrix.Translation(centro) @ Matrix.Rotation(math.radians(-25.0), 4, Vector((1, 0, 0))) @ Matrix.Translation(-centro)
        for d in [coxa] + [c.name for c in b.children_recursive]:
            P[d] = levanta @ P[d]
        _chaves(alvo, i + 1, P)
    return nova


def construir(modelo, fonte):
    """Importa o modelo, faz os clipes e exporta o <modelo>_Extra.fbx. Devolve o esqueleto (para conferir)."""
    cena_nome = "Extra_" + modelo
    if cena_nome in bpy.data.scenes:
        bpy.data.scenes.remove(bpy.data.scenes[cena_nome])
    cena = bpy.data.scenes.new(cena_nome)
    bpy.context.window.scene = cena
    cena.render.fps = 24
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ZUMBIS, modelo + ".fbx"))
    novos = [o for o in bpy.data.objects if o not in antes]
    alvo = next(o for o in novos if o.type == "ARMATURE")
    walk = next(a for a in bpy.data.actions if a.name.startswith(alvo.name + "|") and a.name.endswith("Zombie_Walk"))
    acoes = []
    for de, para in TRANSFERIR.items():
        acao = bpy.data.actions["Zombie_Rig|Zombie_Rig|Zombie_" + de]   # as ações do zumbi antigo (importado primeiro)
        acoes.append((para, transferir(fonte, acao, alvo, para)))
    acoes.append(("MancarE", mancar(alvo, walk, "L")))
    acoes.append(("MancarD", mancar(alvo, walk, "R")))
    # os objetos com os nomes do FBX original. A malha vai junto: com um nó só no topo, a Unity faz dele a raiz e o
    # caminho dos ossos perde o "Zombie_Rig/" (as curvas não achavam os ossos do modelo)
    corpo = next(o for o in novos if o.type == "MESH")
    for o in bpy.data.objects:
        if o.name in ("Zombie_Rig", "Zombie_Body") and o not in (alvo, corpo):
            o.name = o.name + "_outro"
    alvo.name = "Zombie_Rig"
    corpo.name = "Zombie_Body"
    ad = alvo.animation_data
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    for nome, acao in acoes:
        tr = ad.nla_tracks.new(); tr.name = nome
        s = tr.strips.new("Zombie_Rig|Zombie_" + nome, int(acao.frame_range[0]), acao)
        s.name = "Zombie_Rig|Zombie_" + nome
        if hasattr(s, "action_slot") and len(acao.slots):
            s.action_slot = acao.slots[0]
    ad.action = None
    bpy.context.view_layer.update()
    for o in list(cena.objects):
        o.select_set(False)
    alvo.select_set(True)
    corpo.select_set(True)
    bpy.context.view_layer.objects.active = alvo
    bpy.ops.export_scene.fbx(filepath=os.path.join(ZUMBIS, modelo + "_Extra.fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False, bake_anim=True, bake_anim_use_nla_strips=True,
                             bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    return alvo, acoes
