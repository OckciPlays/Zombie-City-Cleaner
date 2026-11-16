# Fotos de conferência da forma (Workbench, as cores dos materiais de autoria), num Blender em segundo plano:
#   blender -b --factory-startup Caminhonete_v1.blend --python foto.py -- <pasta> <prefixo: Cam_ ou CamAlto_> [vistas...]
# As 4 rodas e o estepe são cópias da roda da versão, nos lugares.
import math
import os
import sys

import bpy
from mathutils import Euler, Matrix, Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import caminhonete as cam  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
pasta, prefixo = os.path.abspath(args[0]), args[1]
so = set(args[2:])
cena = bpy.data.scenes["Caminhonete"]
ver = {prefixo + n for n in ("Carroceria", "Porta_E", "Porta_D", "Volante", "Capo", "Tampa")}
for o in cena.objects:
    o.hide_render = o.name not in ver


def unity(p):
    x, y, z = p
    return (-x, -z, y)


roda = bpy.data.objects[prefixo + "Roda"]
copias = []
lugares = [((-cam.X_RODA, cam.R_RODA, cam.EIXO), True), ((cam.X_RODA, cam.R_RODA, cam.EIXO), False),
           ((-cam.X_RODA, cam.R_RODA, -cam.EIXO), True), ((cam.X_RODA, cam.R_RODA, -cam.EIXO), False),
           (cam.ESTEPE, False)]
for i, (p, esquerda) in enumerate(lugares):
    c = bpy.data.objects.new(f"FotoRoda{i}", roda.data)
    cena.collection.objects.link(c)
    c.location = unity(p)
    c.rotation_euler = (math.radians(90), 0, math.radians(180) if esquerda else 0)
    if i == 4:   # o estepe, inclinado com o alto para a lateral (o -x do Blender é o +x da Unity)
        c.rotation_euler = (Matrix.Rotation(math.radians(-cam.TOMBO_ESTEPE), 3, "Y") @ Euler((math.radians(90), 0, 0)).to_matrix()).to_euler()
    copias.append(c)
# o chão
chao_me = bpy.data.meshes.new("FotoChao")
chao_me.from_pydata([(-30, -30, 0), (30, -30, 0), (30, 30, 0), (-30, 30, 0)], [], [(0, 1, 2, 3)])
chao = bpy.data.objects.new("FotoChao", chao_me)
mat = bpy.data.materials.new("FotoChao")
mat.diffuse_color = (0.30, 0.29, 0.27, 1)
chao_me.materials.append(mat)
cena.collection.objects.link(chao)

r = cena.render
r.engine = "BLENDER_WORKBENCH"
r.resolution_x, r.resolution_y = 1400, 860
s = cena.display.shading
s.light = "STUDIO"
s.color_type = "MATERIAL"
s.show_cavity = True
s.cavity_type = "BOTH"
s.show_shadows = True
s.show_object_outline = False
s.background_type = "VIEWPORT"
s.background_color = (0.55, 0.62, 0.70)
cena.display.render_aa = "16"
cam_o = bpy.data.objects.get("CamFoto") or bpy.data.objects.new("CamFoto", bpy.data.cameras.new("CamFoto"))
if cam_o.name not in cena.objects:
    cena.collection.objects.link(cam_o)
cena.camera = cam_o
cam_o.data.clip_start = 0.02
os.makedirs(pasta, exist_ok=True)


def foto(nome, direcao, alvo, dist, lente=50, esconder=()):
    if so and nome not in so:
        return
    escondidos = []
    for n in esconder:
        o = bpy.data.objects.get(prefixo + n)
        if o is not None and not o.hide_render:
            o.hide_render = True
            escondidos.append(o)
    d = Vector(direcao).normalized()
    a = Vector(alvo)
    cam_o.data.lens = lente
    cam_o.location = a + d * dist
    cam_o.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    r.filepath = os.path.join(pasta, f"{prefixo}{nome}.png")
    bpy.ops.render.render(write_still=True)
    for o in escondidos:
        o.hide_render = False


# (eixos do Blender: a frente da caminhonete em -Y, a esquerda dela em +X)
foto("frente34", (0.85, -1.0, 0.36), (0, 0.1, 0.95), 9.5)
foto("tras34", (-0.95, 1.0, 0.42), (0, 0.4, 0.95), 9.5)
foto("lado", (1.0, 0.0, 0.10), (0, 0.15, 0.95), 11.5)
foto("frente", (0.0, -1.0, 0.08), (0, 0, 0.92), 7.5)
foto("tras", (0.0, 1.0, 0.12), (0, 0, 0.92), 7.5)
foto("cacamba", (0.40, 1.0, 1.05), (0, 1.35, 1.0), 5.2)
foto("interior", (1.0, -0.15, 0.30), (0.15, 0.0, 1.25), 2.4, 24, esconder=("Porta_E",))
foto("motorista", (0.0, 1.0, 0.25), (0.37, -0.45, 1.45), 0.35, 22)
foto("roda", (1.0, -0.25, 0.10), (cam.X_RODA, -cam.EIXO, cam.R_RODA), 1.9)
for c in copias:
    bpy.data.objects.remove(c, do_unlink=True)
bpy.data.objects.remove(chao, do_unlink=True)
print("FOTOS OK")
