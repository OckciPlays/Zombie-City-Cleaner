# A boia de pesca (06/11/2026, a pedido: a pesca): a boia redonda, vermelha em cima e branca embaixo, com a haste branca
# e o botão vermelho em cima e a presilha de arame embaixo. Ela fica na água, na ponta da linha da vara.
#
# Como rodar (Blender 5.2, em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/pesca.py -- tudo
#   (as etapas: construir, exportar)
# Grava Assets/_Game/Art/Pesca/Boia.fbx.
#
# O formato é o do Loot.fbx (as peças e as cores do loot.py): o objeto girado 90° em X, com a malha nos eixos da Unity; o
# modelo é feito nos eixos do Blender (o alto em +Z). A origem é a linha d'água: o meio da bola, onde o vermelho encontra
# o branco (a metade branca fica dentro da água). A linha da vara prende em cima, na ponta da haste (PONTA_DA_BOIA), e a
# do anzol embaixo, na presilha (PRESILHA_DA_BOIA). Cores: blocos da Paleta.png, no material Paleta.

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import equipamentos as eq  # noqa: E402
import loot as lt  # noqa: E402

DESTINO = os.path.join(eq.REPO, "Assets", "_Game", "Art", "Pesca")

R_BOLA = 0.015                      # o raio da bola (3 cm)
TOPO_DA_HASTE = R_BOLA + 0.013      # a ponta da haste
# onde as linhas prendem (no espaço do Blender; na Unity, o z daqui é o y)
PONTA_DA_BOIA = (0.0, 0.0, TOPO_DA_HASTE + 0.0005)
PRESILHA_DA_BOIA = (0.0, 0.0, -R_BOLA - 0.0055)


def _material():
    """A paleta (o mesmo material do equipamentos.py, com o nome Paleta)."""
    m = bpy.data.materials.get("Paleta") or bpy.data.materials.new("Paleta")
    m.use_nodes = True
    nt = m.node_tree
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None)
    if tex is None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    img = bpy.data.images.get("Paleta.png") or bpy.data.images.load(eq.PALETA)
    img.alpha_mode = "NONE"   # o alfa da paleta é o brilho da cidade, não transparência
    tex.image = img
    tex.interpolation = "Closest"
    return m


def objeto(nome, pecas, cena):
    """Junta as peças num objeto (o nome sem o _corpo), no formato do Loot.fbx, com o material Paleta."""
    me = bpy.data.meshes.new(nome)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for p in pecas:
        p[0](bm, uv, *p[1:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    for f in me.polygons:
        f.use_smooth = False
    me.materials.append(_material())
    # a malha nos eixos da Unity (o alto em +Y), e o objeto girado 90° em X
    me.transform(Matrix.Rotation(math.radians(-90), 4, "X"))
    o = bpy.data.objects.new(nome, me)
    o.rotation_euler = (math.radians(90), 0, 0)
    cena.collection.objects.link(o)
    return o


def _boia():
    # a bola (de torno): branca da presilha até o meio, vermelha do meio para cima
    bola = []
    for k in range(13):
        a = math.radians(-90 + 15 * k)
        bola.append((R_BOLA * math.cos(a) if 0 < k < 12 else 0.0, R_BOLA * math.sin(a), "branco" if k < 6 else "vermelho"))
    p = [(lt._torno, bola, 16)]
    # a haste branca em cima, com o botão vermelho, e o anel branco em volta da base dela
    p += [
        lt.Z("branco", 0.0022, 0.016, (0, 0, R_BOLA + 0.004), 8),
        lt.Z("vermelho", 0.0034, 0.003, (0, 0, TOPO_DA_HASTE - 0.0015), 10),
        lt.Z("branco", 0.0045, 0.002, (0, 0, R_BOLA - 0.0003), 10),
    ]
    # embaixo: a haste curta e a presilha de arame (o anel em pé)
    p += [
        lt.Z("branco", 0.0018, 0.005, (0, 0, -R_BOLA - 0.0005), 8),
        (lt._com_matriz, Matrix.Translation(PRESILHA_DA_BOIA) @ Matrix.Rotation(math.radians(90), 4, "X"),
         lt._anel("aco", 0.0024, 0.0008, 0.0012, 0.0, 8)),
    ]
    return p


NOMES = ["Boia"]


def _cena():
    return bpy.context.scene


def construir():
    cena = _cena()
    for o in list(cena.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    o = objeto("Boia", _boia(), cena)
    print(f"pesca: a boia, {len(o.data.polygons)} faces")
    return o


def exportar():
    cena = _cena()
    os.makedirs(DESTINO, exist_ok=True)
    for o in cena.objects:
        o.select_set(o.name in NOMES)
    caminho = os.path.join(DESTINO, "Boia.fbx")
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")
    print("pesca: gravado", caminho)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    etapa = args[0] if args else "tudo"
    construir()
    if etapa in ("exportar", "tudo"):
        exportar()
