# A fauna: as funções comuns das malhas dos bichos (o corpo pelo campo das peças, a redução, as faces para fora).

import time

import bpy
import numpy as np

from formas import Juntador, campo_da_malha
import formas


def objeto_da_malha(nome, verts, quads, tris):
    me = bpy.data.meshes.new(nome)
    faces = [tuple(int(i) for i in q) for q in quads] + [tuple(int(i) for i in t) for t in tris]
    me.from_pydata([tuple(map(float, v)) for v in verts], [], faces)
    me.validate()
    me.update()
    ob = bpy.data.objects.new(nome, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def ativar(ob):
    for o in bpy.context.scene.objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob


def triangulos(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def reduzir(ob, alvo):
    """Reduz a malha a ~alvo triângulos (o colapso, simétrico em X)."""
    atual = triangulos(ob)
    if atual <= alvo:
        return
    ativar(ob)
    m = ob.modifiers.new("reduzir", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = alvo / atual
    m.use_symmetry = True
    m.symmetry_axis = "X"
    m.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=m.name)


def corpo(esp, voxel=0.006):
    t0 = time.time()
    pecas = esp.pecas()
    kmax = max(p["k"] for p in pecas)
    faixa = kmax / voxel + 4.0
    campos = []
    mn = np.full(3, 1e9)
    mx = np.full(3, -1e9)
    for p in pecas:
        v, q, t = p["malha"]
        c = campo_da_malha(v, q, t, voxel, faixa)
        p["campo"] = c
        campos.append(c)
        mn = np.minimum(mn, c.ijk0 * voxel)
        mx = np.maximum(mx, (c.ijk0 + np.array(c.arr.shape)) * voxel)
    fundo = faixa * voxel
    j = Juntador(mn - 2 * voxel, mx + 2 * voxel, voxel, fundo)
    for p in pecas:            # primeiro as uniões, depois os recortes (as narinas, as órbitas dos olhos)
        if not p.get("menos"):
            j.juntar(p["campo"], p["k"])
    for p in pecas:
        if p.get("menos"):
            j.juntar(p["campo"], p["k"], menos=True)
    pontos, quads, tris = j.malha(0.0)
    print("fauna: o campo %.1f s, %d pecas, %d pontos" % (time.time() - t0, len(pecas), len(pontos)))
    return [p for p in pecas if not p.get("menos")], j.campo(), pontos, quads, tris


def malha_olho(c, r, lados=7, faixas=4):
    return formas.elipsoide(c, r, r, r, lados, faixas)


def volume(v, faces):
    """O volume com sinal da malha (positivo com as faces viradas para fora)."""
    tot = 0.0
    for f in faces:
        a = v[f[0]]
        for i in range(1, len(f) - 1):
            tot += np.dot(a, np.cross(v[f[i]], v[f[i + 1]]))
    return tot / 6.0


def para_fora(v, faces):
    """Vira as faces de uma peça fechada para fora, se precisar."""
    v = np.asarray(v, float)
    faces = [list(f) for f in faces]
    if volume(v, faces) < 0:
        faces = [f[::-1] for f in faces]
    return faces

