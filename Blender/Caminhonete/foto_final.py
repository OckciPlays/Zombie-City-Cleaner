# Fotos do modelo leve com as texturas assadas (Cycles na CPU), num Blender em segundo plano:
#   blender -b --factory-startup Caminhonete_v1.blend --python foto_final.py -- <versao> <pasta> [vistas...]
# Os materiais da foto usam as texturas compostas (export/<versao>/Texturas): a cor, o metal e a suavidade (MS) e o
# relevo (normal), como o URP Lit da Unity. As 4 rodas e o estepe são cópias da roda leve.
import math
import os
import sys

import bpy
from mathutils import Euler, Matrix, Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import caminhonete as cam  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
cam.VERSAO = args[0]
pasta = os.path.abspath(args[1])
so = set(args[2:])
cam.preparar_catalogo()
cena = bpy.data.scenes["Caminhonete"]
tex = cam.texturas()


def imagem(nome, cor):
    img = bpy.data.images.load(os.path.join(tex, nome + ".png"), check_existing=True)
    img.colorspace_settings.name = "sRGB" if cor else "Non-Color"
    return img


def pbr(nome, atlas, lente=None):
    mat = bpy.data.materials.new(nome)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(b.outputs[0], out.inputs["Surface"])
    uv = nt.nodes.new("ShaderNodeUVMap")
    tc = nt.nodes.new("ShaderNodeTexImage")
    tc.image = imagem(f"{atlas}_Cor", True)
    tm = nt.nodes.new("ShaderNodeTexImage")
    tm.image = imagem(f"{atlas}_MS", False)
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = imagem(f"{atlas}_Normal", False)
    for t in (tc, tm, tn):
        nt.links.new(uv.outputs[0], t.inputs["Vector"])
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(tm.outputs["Color"], sep.inputs[0])
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    nt.links.new(tm.outputs["Alpha"], inv.inputs[1])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs[0], b.inputs["Normal"])
    if lente == "farol":
        b.inputs["Base Color"].default_value = (0.9, 0.92, 0.95, 1)
        b.inputs["Roughness"].default_value = 0.03
        b.inputs["Transmission Weight"].default_value = 0.9
    else:
        nt.links.new(tc.outputs["Color"], b.inputs["Base Color"])
        nt.links.new(sep.outputs[0], b.inputs["Metallic"])
        nt.links.new(inv.outputs[0], b.inputs["Roughness"])
    return mat


lataria = pbr("Foto_Lataria", "Cam_Lataria")
interior = pbr("Foto_Interior", "Cam_Interior")
farol = pbr("Foto_Farol", "Cam_Lataria", "farol")
vidro = bpy.data.materials.new("Foto_Vidro")
vidro.use_nodes = True
bv = next(n for n in vidro.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
bv.inputs["Base Color"].default_value = (0.10, 0.12, 0.14, 1)
bv.inputs["Roughness"].default_value = 0.02
bv.inputs["Transmission Weight"].default_value = 0.75
bv.inputs["Alpha"].default_value = 0.55

ver = {cam.PREFIXO + n for n in ("Carroceria", "Porta_E", "Porta_D", "Volante", "Capo", "Tampa", "Roda")}
for o in cena.objects:
    o.hide_render = o.name not in ver
    if o.name in ver:
        for i, slot in enumerate(o.material_slots):
            sup = slot.material.name[4:] if slot.material and slot.material.name.startswith("Aut_") else None
            if sup is None:
                continue
            atlas, final = cam.v1.SUPERFICIES[sup]
            slot.link = "OBJECT"
            slot.material = vidro if atlas == "-" else farol if final == "Perua_Farol" else interior if atlas == "B" else lataria


def unity(p):
    x, y, z = p
    return (-x, -z, y)


roda = bpy.data.objects[cam.PREFIXO + "Roda"]
roda.hide_render = True
copias = []
lugares = [((-cam.X_RODA, cam.R_RODA, cam.EIXO), True), ((cam.X_RODA, cam.R_RODA, cam.EIXO), False),
           ((-cam.X_RODA, cam.R_RODA, -cam.EIXO), True), ((cam.X_RODA, cam.R_RODA, -cam.EIXO), False), (cam.ESTEPE, False)]
for i, (p, esquerda) in enumerate(lugares):
    c = bpy.data.objects.new(f"FotoRoda{i}", roda.data)
    cena.collection.objects.link(c)
    c.location = unity(p)
    c.rotation_euler = (math.radians(90), 0, math.radians(180) if esquerda else 0)
    if i == 4:   # o estepe, inclinado com o alto para a lateral (o -x do Blender é o +x da Unity)
        c.rotation_euler = (Matrix.Rotation(math.radians(-cam.TOMBO_ESTEPE), 3, "Y") @ Euler((math.radians(90), 0, 0)).to_matrix()).to_euler()
    for k, slot in enumerate(roda.material_slots):
        c.material_slots[k].link = "OBJECT"
        c.material_slots[k].material = lataria
    copias.append(c)

# o chão, o céu e o sol
chao_me = bpy.data.meshes.new("FotoChao")
chao_me.from_pydata([(-40, -40, 0), (40, -40, 0), (40, 40, 0), (-40, 40, 0)], [], [(0, 1, 2, 3)])
chao = bpy.data.objects.new("FotoChao", chao_me)
mc = bpy.data.materials.new("FotoChao")
mc.use_nodes = True
bc = next(n for n in mc.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
bc.inputs["Base Color"].default_value = (0.32, 0.31, 0.29, 1)
bc.inputs["Roughness"].default_value = 0.85
chao_me.materials.append(mc)
cena.collection.objects.link(chao)
mundo = cena.world or bpy.data.worlds.new("FotoMundo")
cena.world = mundo
mundo.use_nodes = True
bg = next(n for n in mundo.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (0.55, 0.66, 0.80, 1)
bg.inputs["Strength"].default_value = 0.9
sol = bpy.data.objects.new("FotoSol", bpy.data.lights.new("FotoSol", "SUN"))
sol.data.energy = 3.2
sol.data.angle = math.radians(4)
sol.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
cena.collection.objects.link(sol)

r = cena.render
r.engine = "CYCLES"
cena.cycles.device = "CPU"
cena.cycles.samples = 48
cena.cycles.use_denoising = True
r.resolution_x, r.resolution_y = 1400, 860
r.film_transparent = False
cena.view_settings.view_transform = "AgX"
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
        o = bpy.data.objects.get(cam.PREFIXO + n)
        if o is not None and not o.hide_render:
            o.hide_render = True
            escondidos.append(o)
    d = Vector(direcao).normalized()
    cam_o.data.lens = lente
    cam_o.location = Vector(alvo) + d * dist
    cam_o.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    r.filepath = os.path.join(pasta, f"{nome}.png")
    bpy.ops.render.render(write_still=True)
    for o in escondidos:
        o.hide_render = False


foto("frente34", (0.85, -1.0, 0.36), (0, 0.1, 0.95), 9.5)
foto("tras34", (-0.95, 1.0, 0.42), (0, 0.4, 0.95), 9.5)
foto("lado", (1.0, 0.0, 0.10), (0, 0.15, 0.95), 11.5)
foto("frente", (0.0, -1.0, 0.08), (0, 0, 0.92), 7.5)
foto("cacamba", (0.40, 1.0, 1.05), (0, 1.35, 1.0), 5.2)
foto("interior", (1.0, -0.15, 0.30), (0.15, 0.0, 1.25), 2.4, 24, esconder=("Porta_E",))
foto("roda", (1.0, -0.25, 0.10), (cam.X_RODA, -cam.EIXO, cam.R_RODA), 1.9)
print("FOTOS OK")
