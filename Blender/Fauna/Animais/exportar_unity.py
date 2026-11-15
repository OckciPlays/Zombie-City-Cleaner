# A ida para a Unity (14/11/2026): cada bicho do Animais.blend vira <saída>/<Nome>.fbx (a malha e o esqueleto na
# origem, na pose de repouso, com as ações como takes: uma por faixa de NLA, com o nome da ação), a textura
# <Nome>_Cor.jpg ao lado e as medidas de todos em Medidas.json (os colisores do corpo e da cabeça, a boca, a altura e o
# comprimento, e onde ficam alguns ossos, para a Unity conferir), que o Editor/MontagemDosAnimais.cs lê.
#
#   blender -b --factory-startup Animais.blend --python exportar_unity.py -- <pasta de saída> [<Nome> ...]
#   (sem nomes: todos os que têm a malha <Nome> e o esqueleto <Nome>_Esqueleto)
#
# Não salva o .blend: o esqueleto volta para a fileira dele e as faixas voltam mudas.
#
# As medidas estão no espaço da Unity (o x para a direita do bicho, o y para cima, o z para a frente), em metros:
# o Blender (o bicho olhando para -Y, o lado L em +X) vira (-x, z, -y), como os cervos do familia.py.

import bpy
import json
import os
import shutil
import sys

import numpy as np

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SAIDA = args[0]
PEDIDOS = args[1:]
AQUI = os.path.dirname(bpy.data.filepath)
TEXTURAS = os.path.join(AQUI, "Texturas")

# os dados do Gustavo (a altura do lombo de verdade de cada um)
ns = {"__name__": "dados"}
for arq in ["animais_dados.py"] + sorted(f for f in os.listdir(AQUI) if f.startswith("animais_dados_") and f.endswith(".py")):
    p = os.path.join(AQUI, arq)
    exec(compile(open(p, encoding="utf-8").read(), p, "exec"), ns)
MODELOS = ns["MODELOS"]

# o tronco (a caixa do corpo) e a cabeça (a caixa da cabeça: sem as orelhas, a galhada e o papo)
TRONCO = {"Quadril", "Pelve", "Coluna_1", "Coluna_2", "Coluna_3", "Peito", "Omoplata.L", "Omoplata.R"}
CABECA = {"Cabeca", "Mandibula"}
CONFERIR = ["Quadril", "Cabeca", "Pata.D.L", "Pata.T.L", "Asa_3.L", "Coxa.L"]


def unity(v):
    return [round(float(-v[0]), 4), round(float(v[2]), 4), round(float(-v[1]), 4)]


def caixa(P, baixo, alto):
    """A caixa (o meio e o tamanho, no espaço da Unity) entre os percentis dos pontos."""
    lo = np.percentile(P, baixo, axis=0)
    hi = np.percentile(P, alto, axis=0)
    meio = unity(0.5 * (lo + hi))
    tam = np.abs(hi - lo)
    return meio, [round(float(tam[0]), 4), round(float(tam[2]), 4), round(float(tam[1]), 4)]


def medir(nome, ob, rig):
    me = ob.data
    M = np.array(ob.matrix_world)
    V = np.array([v.co[:] for v in me.vertices])
    V = (np.c_[V, np.ones(len(V))] @ M.T)[:, :3]
    grupos = [g.name for g in ob.vertex_groups]
    W = np.zeros((len(V), len(grupos)))
    for v in me.vertices:
        for g in v.groups:
            W[v.index, g.group] = g.weight
    dono = np.array([grupos[i] for i in np.argmax(W, 1)])
    tronco = np.isin(dono, list(TRONCO))
    cabeca = np.isin(dono, list(CABECA))
    corpo_meio, corpo_tam = caixa(V[tronco], 2, 98)
    cab_meio, cab_tam = caixa(V[cabeca], 1, 99)
    B = rig.data.bones
    cab = B["Cabeca"]
    h, t = np.array(cab.head_local), np.array(cab.tail_local)
    L = np.linalg.norm(t - h)
    d = (t - h) / max(L, 1e-9)
    if "Mandibula" in B:
        boca = 0.5 * (t + np.array(B["Mandibula"].tail_local)) - d * 0.15 * L
    else:
        boca = t - d * 0.15 * L - np.array((0, 0, 0.08 * L))
    dados = MODELOS.get(nome)
    if dados and dados["escala"][0] == "lombo":
        altura = dados["escala"][2]
    else:
        # as aves, o sapo e o coelho: a altura da cabeça (sem as orelhas), como no Docs/20
        altura = cab_meio[1] + 0.5 * cab_tam[1]
    comprimento = float(V[:, 1].max() - V[:, 1].min())
    ossos = [dict(nome=n, pos=unity(np.array(B[n].head_local))) for n in CONFERIR if n in B]
    return dict(nome=nome, grupo=rig.get("grupo", ""), corpoCentro=corpo_meio, corpoTamanho=corpo_tam,
                cabecaCentro=cab_meio, cabecaTamanho=cab_tam, boca=unity(boca), altura=round(altura, 4),
                comprimento=round(comprimento, 4), vertices=len(V), triangulos=sum(len(p.vertices) - 2 for p in me.polygons),
                suaves=round(sum(p.use_smooth for p in me.polygons) / max(1, len(me.polygons)), 3),
                verticesDoTronco=int(tronco.sum()), verticesDaCabeca=int(cabeca.sum()), ossos=ossos,
                acoes=[])


def repouso(rig):
    for pb in rig.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)


def exportar(nome):
    ob = bpy.data.objects[nome]
    rig = bpy.data.objects[nome + "_Esqueleto"]
    loc, rot = rig.location.copy(), rig.rotation_euler.copy()
    rig.location = (0, 0, 0)
    rig.rotation_euler = (0, 0, 0)
    ad = rig.animation_data
    acao = ad.action if ad else None
    slot = getattr(ad, "action_slot", None) if ad else None
    if ad:
        ad.action = None
    repouso(rig)
    faixas = []
    if ad:
        for t in ad.nla_tracks:
            faixas.append((t, t.mute))
            t.mute = not t.name.startswith(nome + "_")
    # no quadro 0, antes das faixas (que começam no 1, sem extrapolar): a pose que o FBX grava nos nós é a de repouso
    sc = bpy.context.scene
    quadro = sc.frame_current
    sc.frame_set(0)
    bpy.context.view_layer.update()
    m = medir(nome, ob, rig)
    m["acoes"] = [t.name for t, _ in faixas if t.name.startswith(nome + "_")]
    for o in bpy.context.view_layer.objects:
        o.select_set(o in (rig, ob))
    bpy.context.view_layer.objects.active = rig
    caminho = os.path.join(SAIDA, nome + ".fbx")
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", mesh_smooth_type="FACE", colors_type="NONE",
                             use_armature_deform_only=False, path_mode="STRIP", embed_textures=False,
                             bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
                             bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
                             bake_anim_simplify_factor=1.0)
    # conferir: de volta ao quadro 0, cada osso na pose de repouso (o que a Unity vai ter como o repouso)
    sc.frame_set(0)
    bpy.context.view_layer.update()
    desvio = 0.0
    for pb in rig.pose.bones:
        a = np.array(pb.matrix)
        b = np.array(pb.bone.matrix_local)
        desvio = max(desvio, float(np.abs(a - b).max()))
    m["desvioDoRepouso"] = round(desvio, 6)
    tex = os.path.join(TEXTURAS, nome + "_Cor.jpg")
    if os.path.isfile(tex):
        shutil.copyfile(tex, os.path.join(SAIDA, nome + "_Cor.jpg"))
    else:
        print("@@@ SEM TEXTURA", tex)
    # de volta como estava
    for t, mudo in faixas:
        t.mute = mudo
    if ad:
        ad.action = acao
        if slot is not None and hasattr(ad, "action_slot"):
            try:
                ad.action_slot = slot
            except Exception:
                pass
    rig.location, rig.rotation_euler = loc, rot
    sc.frame_set(quadro)
    print("@@@ exportado %s: %d triângulos, %d ações, desvio do repouso %.6f, %.0f KB" % (
        nome, m["triangulos"], len(m["acoes"]), desvio, os.path.getsize(caminho) / 1024))
    return m


def main():
    os.makedirs(SAIDA, exist_ok=True)
    nomes = PEDIDOS or sorted(o.name for o in bpy.data.objects
                              if o.type == "MESH" and (o.name + "_Esqueleto") in bpy.data.objects)
    print("@@@ blender", bpy.app.version_string, "bichos:", len(nomes))
    medidas = []
    for n in nomes:
        medidas.append(exportar(n))
    arq = os.path.join(SAIDA, "Medidas.json")
    if PEDIDOS and os.path.isfile(arq):
        velhas = {d["nome"]: d for d in json.load(open(arq, encoding="utf-8"))["bichos"]}
        for d in medidas:
            velhas[d["nome"]] = d
        medidas = [velhas[k] for k in sorted(velhas)]
    with open(arq, "w", encoding="utf-8") as f:
        json.dump({"bichos": medidas}, f, ensure_ascii=False, indent=1)
    print("@@@ medidas", arq, len(medidas))


main()
