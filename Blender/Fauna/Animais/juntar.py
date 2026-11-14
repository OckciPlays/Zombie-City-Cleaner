# Junta os bichos montados em vários .blend (cada um na coleção dele) num arquivo só, em fileiras por grupo, para ver.
#   blender -b --factory-startup --python juntar.py -- <saida.blend> <entrada1.blend> [<entrada2.blend> ...]
#
# Cada bicho continua com o esqueleto na origem do objeto (o pivô entre as patas): só o objeto do esqueleto é movido
# para a fileira dele (a malha é filha dele). A ação ativa de cada um é a de parado, e a cena vai de 1 a 96.

import bpy
import sys

FILEIRAS = [
    ["Lobo_Macho", "Lobo_Femea", "Lobo_Filhote", "Coiote"],
    ["Cervo_Macho", "Cervo_Femea", "Alce_Macho", "Alce_Femea", "Alce_Filhote"],
    ["UrsoNegro_Macho", "UrsoNegro_Femea", "UrsoNegro_Filhote", "UrsoPardo_Macho", "UrsoPardo_Femea", "UrsoPardo_Filhote"],
    ["Guaxinim", "Gamba", "Castor", "Coelho", "Sapo"],
    ["Pato", "Peru", "Urubu", "Aguia"],
]

args = sys.argv[sys.argv.index("--") + 1:]
saida, entradas = args[0], args[1:]
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
for c in list(bpy.data.collections):
    bpy.data.collections.remove(c)

animais = bpy.data.collections.new("Animais")
bpy.context.scene.collection.children.link(animais)
for f in entradas:
    with bpy.data.libraries.load(f, link=False) as (src, dst):
        dst.collections = [n for n in src.collections]
    for c in dst.collections:
        if c is None:
            continue
        if not c.all_objects:
            bpy.data.collections.remove(c)
            continue
        if c.name not in animais.children:
            animais.children.link(c)

y = 0.0
for fileira in FILEIRAS:
    x = 0.0
    fundo = 0.0
    for nome in fileira:
        c = bpy.data.collections.get(nome)
        if c is None:
            print("@@@ falta", nome)
            continue
        rig = next(o for o in c.objects if o.type == "ARMATURE")
        ob = next(o for o in c.objects if o.type == "MESH")
        larg = ob.dimensions.x
        comp = ob.dimensions.y
        x += larg / 2
        rig.location = (x, y, 0)
        x += larg / 2 + 0.6
        fundo = max(fundo, comp)
        ad = rig.animation_data
        parado = bpy.data.actions.get(nome + "_Parado") or bpy.data.actions.get(nome + "_Pousado")
        if ad and parado:
            ad.action = parado
            if ad.action_slot is None and parado.slots:
                ad.action_slot = parado.slots[0]
    y += fundo + 1.0

sc = bpy.context.scene
sc.render.fps = 24
sc.frame_start = 1
sc.frame_end = 96
for a in bpy.data.actions:
    a.use_fake_user = True
bpy.ops.wm.save_as_mainfile(filepath=saida, relative_remap=True)
print("@@@ juntado", len([c for c in animais.children]), "bichos,", len(bpy.data.actions), "ações")
