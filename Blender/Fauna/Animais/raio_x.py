# Fotos em raio-X (de lado e de frente, a grade de 0,05 no espaço normalizado do Tripo), com os ossos por cima se houver
# um esqueleto na cena: blender -b [arquivo.blend] --python raio_x.py -- <nome do FBX ou do objeto> [saida.png] [--blend]
import bpy, os, sys, math
import numpy as np
from mathutils import Vector
AQUI = r"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner\Modelo 3D\Animais"
ns = {}
exec(open(os.path.join(AQUI, "animais_kit.py"), encoding="utf-8").read(), ns)
args = sys.argv[sys.argv.index("--") + 1:]
nome = args[0]
saida = args[1] if len(args) > 1 and not args[1].startswith("--") else os.path.join(AQUI, "Renders", "RaioX", nome + ".png")
os.makedirs(os.path.dirname(saida), exist_ok=True)
sc = bpy.context.scene
if "--blend" not in args:
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    ob, _ = ns["importar"](nome, nome)
else:
    ob = bpy.data.objects[nome]
rig = ob.parent if ob.parent and ob.parent.type == "ARMATURE" else None
for o in bpy.data.objects:
    o.hide_render = o is not ob
sc.render.engine = "BLENDER_WORKBENCH"
sh = sc.display.shading
sh.light = "FLAT"
sh.color_type = "TEXTURE"
sh.show_xray = True
sh.xray_alpha = 0.55
V = ns["vertices"](ob) if rig is None else np.array([tuple(ob.matrix_world @ v.co) for v in ob.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.vertices])
mn, mx = V.min(0), V.max(0)
tam = max(mx - mn) * 1.12
R = 1024
sc.render.resolution_x = R
sc.render.resolution_y = R
cam = bpy.data.objects.new("cam_rx", bpy.data.cameras.new("cam_rx"))
cam.data.type = "ORTHO"
cam.data.ortho_scale = tam
sc.collection.objects.link(cam)
sc.camera = cam
meio = (mn + mx) / 2
passo = 0.05 if tam < 1.5 else (0.1 if tam < 3 else 0.25)

FONTE = {"0": "111101101101111", "1": "010110010010111", "2": "111001111100111", "3": "111001111001111",
         "4": "101101111001001", "5": "111100111001111", "6": "111100111101111", "7": "111001010010010",
         "8": "111101111101111", "9": "111101111001111", "-": "000000111000000", ".": "000000000000010"}
def texto(a, s, x, y, cor=(1, 1, 0.3, 1), k=2):
    """Escreve s com o canto de baixo à esquerda em (x, y) (as linhas da imagem de baixo para cima)."""
    for ch in s:
        b = FONTE.get(ch)
        if b:
            for r in range(5):
                for c in range(3):
                    if b[r * 3 + c] == "1":
                        yy, xx = y + (4 - r) * k, x + c * k
                        if 0 <= yy and yy + k <= a.shape[0] and 0 <= xx and xx + k <= a.shape[1]:
                            a[yy:yy + k, xx:xx + k] = cor
        x += 4 * k
def foto(vista):
    if vista == "lado":
        cam.location = (meio[0] + 10, meio[1], meio[2]); cam.rotation_euler = (math.pi / 2, 0, math.pi / 2)
        eixos = (1, 2)   # tela x = +Y, tela y = +Z
    else:
        cam.location = (meio[0], meio[1] - 10, meio[2]); cam.rotation_euler = (math.pi / 2, 0, 0)
        eixos = (0, 2)
    cam.data.clip_end = 100
    p = os.path.join(os.path.dirname(saida), "_rx_" + nome + ".png")
    sc.render.filepath = p
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(p)
    a = np.array(im.pixels[:], np.float32).reshape(R, R, 4)
    bpy.data.images.remove(im)
    os.remove(p)
    esc = R / tam
    c0 = (meio[eixos[0]], meio[eixos[1]])
    def px(w, e):
        return int(round(R / 2 + (w - c0[e]) * esc))
    k0 = int(math.floor((min(mn[eixos[0]], mn[eixos[1]]) - tam) / passo))
    k1 = int(math.ceil((max(mx[eixos[0]], mx[eixos[1]]) + tam) / passo))
    for k in range(k0, k1 + 1):
        w = k * passo
        forte = abs(w) < 1e-9 and (1, 0.2, 0.2, 1) or ((k % 10 == 0) and (0.3, 0.3, 1, 1) or ((k % 2 == 0) and (0.75, 0.75, 0.75, 1) or (0.45, 0.45, 0.45, 1)))
        cor = np.array(forte, np.float32)
        x = px(w, 0); y = px(w, 1)
        if 0 <= x < R: a[:, x] = a[:, x] * 0.6 + cor * 0.4
        if 0 <= y < R: a[y, :] = a[y, :] * 0.6 + cor * 0.4
        if k % 2 == 0:
            rot = ("%.1f" % w) if passo < 0.1 else ("%.2f" % w).rstrip("0").rstrip(".")
            if 0 <= x < R: texto(a, rot, x + 2, 4)
            if 0 <= y < R: texto(a, rot, 4, y + 2, (0.4, 1, 0.4, 1))
    if rig is not None:
        deps = bpy.context.evaluated_depsgraph_get()
        for pb in rig.pose.bones:
            h = rig.matrix_world @ pb.head; t = rig.matrix_world @ pb.tail
            cor = np.array((1, 1, 0, 1) if ".L" in pb.name else ((0, 1, 1, 1) if ".R" in pb.name else (1, 0.3, 1, 1)), np.float32)
            n = 60
            for i in range(n + 1):
                q = h.lerp(t, i / n)
                x, y = px(q[eixos[0]], 0), px(q[eixos[1]], 1)
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        if 0 <= x + dx < R and 0 <= y + dy < R:
                            a[y + dy, x + dx] = cor
            x, y = px(h[eixos[0]], 0), px(h[eixos[1]], 1)
            a[max(y - 3, 0):y + 4, max(x - 3, 0):x + 4] = (1, 1, 1, 1)
    return a
img = np.concatenate([foto("lado"), foto("frente")], axis=1)
out = bpy.data.images.new("rx", img.shape[1], img.shape[0], alpha=True)
out.pixels.foreach_set(img.ravel())
out.filepath_raw = saida
out.file_format = "PNG"
out.save()
print("@@@ grade", passo, "tam", round(tam, 3), "meio", [round(x, 3) for x in meio])
