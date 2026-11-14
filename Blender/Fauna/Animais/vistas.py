# Fotos ortográficas de cada FBX (de lado, de frente, de cima), com a grade de 0,1 m, para achar a orientação e as juntas.
import bpy, os, sys, math
import numpy as np
from mathutils import Vector
PASTA = r"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner\Animais 3d"
SAIDA = r"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner\Modelo 3D\Animais\Renders\Inspecao"
os.makedirs(SAIDA, exist_ok=True)
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
sc = bpy.context.scene
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "FLAT"
sc.display.shading.color_type = "TEXTURE"
sc.render.film_transparent = False
sc.world = bpy.data.worlds.new("w") if sc.world is None else sc.world
R = 512
sc.render.resolution_x = R
sc.render.resolution_y = R
sc.render.resolution_percentage = 100
cam_d = bpy.data.cameras.new("cam")
cam_d.type = "ORTHO"
cam_d.ortho_scale = 1.2
cam = bpy.data.objects.new("cam", cam_d)
sc.collection.objects.link(cam)
sc.camera = cam
VISTAS = {  # nome: (posição, rotação)
    "lado_X+": ((3, 0, 0.45), (math.pi/2, 0, math.pi/2)),
    "frente_Y-": ((0, -3, 0.45), (math.pi/2, 0, 0)),
    "cima": ((0, 0, 3), (0, 0, 0)),
}
def render(path):
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(path)
    a = np.array(im.pixels[:], np.float32).reshape(R, R, 4)
    bpy.data.images.remove(im)
    return a
def grade(a, vista):
    # linhas a cada 0,1 m (mais fortes a cada 0,5); a imagem cobre 1,2 m
    px = R / 1.2
    cx = R / 2
    cy = R / 2
    centro = {"lado_X+": (0, 0.45), "frente_Y-": (0, 0.45), "cima": (0, 0)}[vista]
    for k in range(-10, 11):
        w = k * 0.1
        forte = (k % 5 == 0)
        c = np.array((1, 0, 0, 1) if k == 0 else ((0.2, 0.2, 0.9, 1) if forte else (0.6, 0.6, 0.6, 1)), np.float32)
        x = int(round(cx + (w - centro[0]) * px))
        y = int(round(cy + (w - centro[1]) * px))
        if 0 <= x < R:
            a[:, x] = a[:, x] * 0.5 + c * 0.5
        if 0 <= y < R:
            a[y, :] = a[y, :] * 0.5 + c * 0.5
    return a
nomes = args or sorted(n for n in os.listdir(PASTA) if os.path.isdir(os.path.join(PASTA, n)))
for nome in nomes:
    p = os.path.join(PASTA, nome, nome + ".fbx")
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=p)
    novos = [o for o in bpy.data.objects if o not in antes]
    linhas = []
    for v, (pos, rot) in VISTAS.items():
        cam.location = pos
        cam.rotation_euler = rot
        a = render(os.path.join(SAIDA, "_tmp.png"))
        linhas.append(grade(a, v))
    img = np.concatenate(linhas, axis=1)[::-1][::-1]
    out = bpy.data.images.new(nome, img.shape[1], img.shape[0], alpha=True)
    out.pixels.foreach_set(img.ravel())
    out.filepath_raw = os.path.join(SAIDA, nome + ".png")
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    for o in novos:
        bpy.data.objects.remove(o, do_unlink=True)
os.remove(os.path.join(SAIDA, "_tmp.png"))
