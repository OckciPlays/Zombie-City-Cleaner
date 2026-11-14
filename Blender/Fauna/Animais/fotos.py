# As folhas de conferência: para cada bicho do .blend, uma linha por ação (os quadros espalhados), de lado e de 3/4.
#   blender -b <arquivo.blend> --python fotos.py -- <pasta> [<Nome> ...] [--acoes A,B] [--quadros 6] [--px 220]
import bpy, os, sys, math
import numpy as np
from mathutils import Vector
args = sys.argv[sys.argv.index("--") + 1:]
pasta = args[0]
os.makedirs(pasta, exist_ok=True)
def opc(n, d):
    return args[args.index(n) + 1] if n in args else d
NQ = int(opc("--quadros", "6"))
PX = int(opc("--px", "220"))
so_acoes = opc("--acoes", "")
so_acoes = set(so_acoes.split(",")) if so_acoes else None
VALS = [args[i + 1] for i, a in enumerate(args[:-1]) if a.startswith("--")]
nomes = [a for a in args[1:] if not a.startswith("--") and a not in VALS]
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sh = sc.display.shading
sh.light = "STUDIO"
sh.color_type = "TEXTURE"
sh.show_shadows = False
sh.show_cavity = False
sc.render.resolution_x = PX
sc.render.resolution_y = PX
sc.render.film_transparent = False
cam = bpy.data.objects.new("cam_f", bpy.data.cameras.new("cam_f"))
cam.data.type = "ORTHO"
sc.collection.objects.link(cam)
sc.camera = cam
chao = bpy.data.objects.get("Chao_Fotos")
if chao is None:
    bpy.ops.mesh.primitive_plane_add(size=60)
    chao = bpy.context.object
    chao.name = "Chao_Fotos"
    m = bpy.data.materials.new("Chao_Fotos"); m.diffuse_color = (0.32, 0.34, 0.3, 1); chao.data.materials.append(m)
def grupos():
    out = []
    for c in bpy.data.collections:
        rig = next((o for o in c.objects if o.type == "ARMATURE"), None)
        ob = next((o for o in c.objects if o.type == "MESH"), None)
        if rig and ob:
            out.append((c.name, ob, rig))
    return out
def ler(p):
    im = bpy.data.images.load(p)
    a = np.array(im.pixels[:], np.float32).reshape(PX, PX, 4)
    bpy.data.images.remove(im)
    return a
for nome, ob, rig in grupos():
    if nomes and nome not in nomes:
        continue
    for o in bpy.data.objects:
        o.hide_render = o not in (ob, chao)
    acoes = [t.strips[0].action for t in rig.animation_data.nla_tracks] if rig.animation_data and rig.animation_data.nla_tracks else []
    if so_acoes:
        acoes = [a for a in acoes if a.name[len(nome) + 1:] in so_acoes]
    rig.animation_data.action_blend_type = "REPLACE"
    for t in rig.animation_data.nla_tracks:
        t.mute = True
    linhas = []
    H = max(rig.data.bones["Raiz"].head_local.z, 0) + max((ob.matrix_world @ Vector(v)).z for v in ob.bound_box)
    Lg = max(ob.dimensions)
    tam = Lg * float(opc('--zoom', '1.12'))
    cam.data.ortho_scale = tam
    for a in acoes:
        rig.animation_data.action = a
        if rig.animation_data.action_slot is None and a.slots:
            rig.animation_data.action_slot = a.slots[0]
        f0, f1 = int(a.frame_start), int(a.frame_end)
        qs = [round(f0 + (f1 - f0) * i / max(NQ - 1, 1)) for i in range(NQ)] if not a.use_cyclic else [round(f0 + (f1 - f0) * i / NQ) for i in range(NQ)]
        lin = []
        for qd in qs:
            sc.frame_set(qd)
            cel = []
            for vista in ("lado", "34"):
                if vista == "lado":
                    cam.location = (Lg * 3, 0, tam * 0.42); cam.rotation_euler = (math.pi / 2, 0, math.pi / 2)
                else:
                    az = math.radians(40); el = math.radians(18); d = Lg * 3
                    cam.location = (d * math.sin(az) * math.cos(el), -d * math.cos(az) * math.cos(el), tam * 0.42 + d * math.sin(el))
                    cam.rotation_euler = (math.pi / 2 - el, 0, az)
                p = os.path.join(pasta, "_f_%s.png" % nome)
                sc.render.filepath = p
                bpy.ops.render.render(write_still=True)
                cel.append(ler(p))
                os.remove(p)
            lin.append(np.concatenate([cel[1], cel[0]], axis=0))
        linhas.append(np.concatenate(lin, axis=1))
        print("@@@ linha", nome, a.name, qs)
    if not linhas:
        continue
    POR = int(opc("--linhas", "5"))
    W = max(l.shape[1] for l in linhas)
    linhas = [np.pad(l, ((0, 0), (0, W - l.shape[1]), (0, 0))) for l in linhas]
    sep = np.ones((4, W, 4), np.float32)
    sufixo = ("_" + "_".join(sorted(so_acoes))) if so_acoes and len(so_acoes) <= 3 else ""
    for pg in range(0, len(linhas), POR):
        partes = []
        for l in linhas[pg:pg + POR][::-1]:
            partes += [l, sep]
        img = np.concatenate(partes[:-1], axis=0)
        out = bpy.data.images.new("folha", img.shape[1], img.shape[0], alpha=True)
        out.pixels.foreach_set(img.ravel())
        pag = "" if len(linhas) <= POR else "_%d" % (pg // POR + 1)
        out.filepath_raw = os.path.join(pasta, nome + sufixo + pag + ".png")
        out.file_format = "PNG"
        out.save()
        bpy.data.images.remove(out)
