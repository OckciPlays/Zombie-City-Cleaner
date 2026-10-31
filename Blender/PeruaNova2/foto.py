# Fotos de conferência da v2 (Blender em segundo plano):
#   blender -b --factory-startup Blender/PeruaNova2/PeruaNova2.blend --python Blender/PeruaNova2/foto.py -- <pasta> <prefixo> [arame]
# prefixo: "Perua2_" (a v2) ou "Perua_" (a v1). Fotos de 3/4 da frente, de 3/4 de trás e da roda.
import math
import os
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
pasta, prefixo = args[0], args[1]
arame = len(args) > 2 and args[2] == "arame"
cena = bpy.data.scenes["PeruaNova"]
bpy.context.window_manager  # (sem janela em segundo plano)
nomes = ["Carroceria", "Porta_DE", "Porta_DD", "Porta_TE", "Porta_TD", "Volante", "Capo", "Tampa"]
ver = {prefixo + n for n in nomes}
for o in cena.objects:
    o.hide_render = o.name not in ver
# as 4 rodas: cópias da roda da versão, nos lugares
roda = bpy.data.objects[prefixo + "Roda"]
copias = []
for i, (x, z) in enumerate(((-0.735, 1.36), (0.735, 1.36), (-0.735, -1.36), (0.735, -1.36))):
    c = bpy.data.objects.new(f"FotoRoda{i}", roda.data)
    cena.collection.objects.link(c)
    c.location = (-x, -z, 0.30)
    c.rotation_euler = (math.radians(90), 0, math.radians(180) if x < 0 else 0)
    copias.append(c)
r = cena.render
r.engine = "BLENDER_WORKBENCH"
r.resolution_x, r.resolution_y = 1100, 700
r.film_transparent = False
s = cena.display.shading
s.light = "STUDIO"
s.color_type = "MATERIAL"
s.show_cavity = True
s.cavity_type = "BOTH"
s.show_object_outline = True
s.background_type = "VIEWPORT"
s.background_color = (0.42, 0.42, 0.39)
cena.display.render_aa = "8"
if arame:
    s.type = "SOLID"
    s.show_xray = False
cam = bpy.data.objects.get("CamFoto") or bpy.data.objects.new("CamFoto", bpy.data.cameras.new("CamFoto"))
if cam.name not in cena.objects:
    cena.collection.objects.link(cam)
cena.camera = cam
cam.data.lens = 50
cam.data.clip_start = 0.02
os.makedirs(pasta, exist_ok=True)


def foto(nome, direcao, alvo, dist, lente=50):
    d = Vector(direcao).normalized()
    a = Vector(alvo)
    cam.data.lens = lente
    cam.location = a + d * dist
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    r.filepath = os.path.join(pasta, f"{prefixo}{nome}.png")
    bpy.ops.render.render(write_still=True)


# (eixos do Blender: a frente do carro em -Y, a esquerda em +X)
foto("frente34", (-0.9, -1.0, 0.45), (0, 0, 0.75), 7.5)
foto("tras34", (0.9, 1.0, 0.45), (0, 0, 0.75), 7.5)
foto("roda", (1.0, -0.25, 0.12), (-0.735 * -1, -1.36, 0.30), 1.6, 50)
foto("interior", (0.0, 0.35, 1.0), (0, -0.1, 0.6), 3.2, 35)
for c in copias:
    bpy.data.objects.remove(c, do_unlink=True)
print("FOTOS OK")
