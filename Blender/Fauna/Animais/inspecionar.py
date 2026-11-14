import bpy, os, json
from mathutils import Vector
PASTA = r"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner\Animais 3d"
out = {}
for nome in sorted(os.listdir(PASTA)):
    p = os.path.join(PASTA, nome, nome + ".fbx")
    if not os.path.isfile(p):
        continue
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=p)
    novos = [o for o in bpy.data.objects if o not in antes]
    info = []
    for o in novos:
        d = {"nome": o.name, "tipo": o.type, "loc": list(o.location), "rot": list(o.rotation_euler), "esc": list(o.scale), "pai": o.parent.name if o.parent else None}
        if o.type == "MESH":
            me = o.data
            bpy.context.view_layer.update()
            cs = [o.matrix_world @ Vector(c) for c in o.bound_box]
            mn = [min(c[i] for c in cs) for i in range(3)]
            mx = [max(c[i] for c in cs) for i in range(3)]
            d.update(v=len(me.vertices), f=len(me.polygons), tri=sum(len(p.vertices)-2 for p in me.polygons), mn=mn, mx=mx, mats=[m.name for m in me.materials], uvs=[u.name for u in me.uv_layers])
        if o.type == "ARMATURE":
            d["ossos"] = len(o.data.bones)
        info.append(d)
    out[nome] = info
    for o in novos:
        bpy.data.objects.remove(o, do_unlink=True)
print("@@@" + json.dumps(out))
