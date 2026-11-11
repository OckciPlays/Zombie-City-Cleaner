# Os itens soltos das etapas 3 e 4 das armas de sucata (10/11/2026): o Loot_Etapa34.fbx, ao lado do Loot.fbx (sem mexer
# nele), no formato dele (o loot.py e o sucata_itens.py): cada modelo é um objeto Item_<Nome>_corpo girado 90° em X, com
# a malha nos eixos da Unity (o alto em +Y), feito nos eixos do Blender (o alto em +Z, a frente em -Y); a origem
# embaixo, no meio (no chão, a Unity apoia pela caixa da malha); o material "Pistola", com a Paleta.png.
# - A proteção de sucata (as peças do protecao_sucata.py, as do homem, deitadas no chão):
#   Item_Capacete (a panela de boca para baixo, a tira solta do lado), Item_Bracadeiras (as duas revistas enroladas, lado a
#   lado), Item_Caneleiras (os dois canos, a fita só por cima deles) e Item_Colete (o pneu deitado, a chapa por cima, as
#   tiras dobradas e as cordas soltas);
# - O fogo (os modelos do fogo.py): Item_Lampiao (em pé), Item_Molotov (em pé, o pano em cima) e Item_KitFogueira (o
#   feixe de lenha com a latinha de álcool).
#
# Como rodar (Blender 5.2, em segundo plano; um Blender por vez; os FBX do projeto só são lidos):
#   blender -b --factory-startup --disable-autoexec --python Blender/loot_etapa34.py -- tudo <pasta de saída>
#   (grava <pasta>/Loot_Etapa34.fbx e as fotos em <pasta>/fotos/Foto_Item_<Nome>.png; a saída padrão é a pasta do jogador)

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
for p in (AQUI, os.path.join(AQUI, "Armas")):
    if p not in sys.path:
        sys.path.insert(0, p)
import equipamentos as eq  # noqa: E402
import fogo  # noqa: E402
import loot  # noqa: E402
import protecao_sucata as ps  # noqa: E402

C = loot.C


def _bm_em(bm_m, M):
    """O bmesh (feito no espaço do osso) levado por M (de lá para os eixos do item)."""
    bm, _ = bm_m
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)
    return bm


def _juntar(bms):
    """Os bmesh num só (cada um com o UV dele)."""
    out = bmesh.new()
    uv = out.loops.layers.uv.new("UVMap")
    for bm in bms:
        uvb = bm.loops.layers.uv.active
        mapa = {v: out.verts.new(v.co) for v in bm.verts}
        for f in bm.faces:
            nf = out.faces.new([mapa[v] for v in f.verts])
            for l, lb in zip(nf.loops, f.loops):
                l[uv].uv = lb[uvb].uv
        bm.free()
    return out


def _base(rows):
    """A matriz 4x4 com as linhas dadas (os eixos de origem que viram X, Y e Z do item)."""
    return Matrix([list(r) for r in rows]).to_4x4()


def capacete(ctx):
    # o espaço do head: o alto em +Y e a frente em +Z; no item: o alto em +Z, a frente em -Y (a panela de boca para baixo)
    bm = _bm_em(ps.capacete(ctx, tira=False), _base([(1, 0, 0), (0, 0, -1), (0, 1, 0)]))
    vs = [v.co for v in bm.verts]
    z0 = min(v.z for v in vs)
    cx = (min(v.x for v in vs) + max(v.x for v in vs)) / 2
    xr = max(v.x for v in vs)
    tira = fogo.bm_das_pecas([
        C("p_couro", (0.16, 0.022, 0.004), (xr + 0.06, -0.03, z0 + 0.002), giro=(0, 0, 25)),
        C("p_couro", (0.12, 0.022, 0.004), (cx - 0.02, -0.21, z0 + 0.002), giro=(0, 0, -15)),
    ])
    return _juntar([bm, tira])


def bracadeiras(ctx):
    bms = []
    for lado, dy, giro in (("L", -0.11, 6.0), ("R", 0.11, -4.0)):
        # o eixo do antebraço (o Y do osso) deitado em X
        bm = _bm_em(ps.bracadeira(ctx, lado), Matrix.Rotation(math.radians(giro), 4, "Z") @ _base([(0, 1, 0), (-1, 0, 0), (0, 0, 1)]))
        vs = [v.co for v in bm.verts]
        meio = Vector(((min(v.x for v in vs) + max(v.x for v in vs)) / 2, (min(v.y for v in vs) + max(v.y for v in vs)) / 2,
                       min(v.z for v in vs)))
        bmesh.ops.translate(bm, vec=Vector((0, dy, 0)) - meio, verts=bm.verts)
        bms.append(bm)
    return _juntar(bms)


def caneleiras(ctx):
    bms = []
    for lado, dy, giro in (("L", -0.16, 4.0), ("R", 0.16, -7.0)):
        osso = "shin." + lado
        a = ps._ang_de(ctx, osso, (0, -1, 0))
        frente = Vector((math.cos(a), 0, math.sin(a)))
        eixo = Vector((0, 1, 0))
        lado_v = frente.cross(eixo)
        # o eixo da canela em X, a frente do cano para cima (+Z)
        bm = _bm_em(ps.caneleira(ctx, lado, so_frente=True), Matrix.Rotation(math.radians(giro), 4, "Z") @ _base([eixo, lado_v, frente]))
        vs = [v.co for v in bm.verts]
        meio = Vector(((min(v.x for v in vs) + max(v.x for v in vs)) / 2, (min(v.y for v in vs) + max(v.y for v in vs)) / 2,
                       min(v.z for v in vs)))
        bmesh.ops.translate(bm, vec=Vector((0, dy, 0)) - meio, verts=bm.verts)
        bms.append(bm)
    return _juntar(bms)


def colete():
    """O pneu deitado, a chapa por cima (girada um pouco), as tiras de câmara de ar dobradas por cima da chapa e as cordas
    soltas nos lados."""
    p = [
        # o pneu (a banda de rodagem para baixo) e os blocos aparecendo nas bordas
        C("s_borracha", (0.30, 0.32, 0.016), (0, 0.0, 0.008)),
        C("s_quase_preto", (0.302, 0.322, 0.003), (0, 0.0, 0.0015)),
        # a chapa pintada, com a ferrugem, os parafusos e o X de fita
        (loot._com_matriz, Matrix.Translation((0.012, -0.02, 0.016)) @ Matrix.Rotation(math.radians(7), 4, "Z"), [
            C("s_pintura_verde", (0.29, 0.33, 0.006), (0, 0, 0.003)),
            C("s_ferrugem_escura", (0.292, 0.332, 0.002), (0, 0, 0.001)),
            C("s_ferrugem", (0.07, 0.05, 0.0012), (-0.06, -0.08, 0.0066)),
            C("s_ferrugem", (0.05, 0.06, 0.0012), (0.08, 0.09, 0.0066)),
            C("s_fita_prata", (0.075, 0.02, 0.0014), (0.04, -0.04, 0.0068), giro=(0, 0, 35)),
            C("s_fita_prata", (0.075, 0.02, 0.0014), (0.04, -0.04, 0.0072), giro=(0, 0, -35)),
            loot.Z("s_metal_claro", 0.006, 0.004, (-0.12, -0.14, 0.008), 6),
            loot.Z("s_metal_claro", 0.006, 0.004, (0.12, -0.14, 0.008), 6),
            loot.Z("s_metal_claro", 0.006, 0.004, (-0.12, 0.14, 0.008), 6),
            loot.Z("s_metal_claro", 0.006, 0.004, (0.12, 0.14, 0.008), 6),
        ]),
        # as tiras dos ombros, dobradas por cima da chapa (a parte de cima de cada uma)
        C("s_borracha", (0.042, 0.20, 0.007), (-0.09, 0.09, 0.026), giro=(0, 0, 4)),
        C("s_borracha", (0.042, 0.20, 0.007), (0.10, 0.09, 0.026), giro=(0, 0, 9)),
        C("s_borracha", (0.042, 0.06, 0.007), (-0.088, 0.205, 0.016), giro=(40, 0, 4)),
        C("s_borracha", (0.042, 0.06, 0.007), (0.11, 0.205, 0.016), giro=(40, 0, 9)),
    ]
    # as cordas soltas nos lados (no chão, em curva)
    for s, z in ((1, 0.006), (-1, 0.006)):
        pts = [Vector((s * (0.15 + 0.06 * math.sin(k / 5 * math.pi)), -0.12 + 0.06 * k, z)) for k in range(6)]
        p.append((lambda bm, uv, pp=pts: ps.corda(bm, uv, "s_corda", pp, 0.0055, 6),))
    return fogo.bm_das_pecas(p)


def construir():
    rig, _ = ps.importar(poses=False)
    env = ps.Envelope(rig)
    ctx = ps.Ctx(env, rig, False)
    # a cena dos itens: limpa (o corpo e as roupas saem depois de medidos)
    bms = {
        "Item_Capacete": capacete(ctx),
        "Item_Bracadeiras": bracadeiras(ctx),
        "Item_Caneleiras": caneleiras(ctx),
        "Item_Colete": colete(),
    }
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    cena = bpy.context.scene
    cfg = fogo._cfgs()
    objs = [fogo.objeto(nome + "_corpo", bm, cena) for nome, bm in bms.items()]
    objs += [
        fogo.objeto("Item_Lampiao_corpo", fogo.bm_da_cfg(cfg["Lampiao"]), cena),
        fogo.objeto("Item_Molotov_corpo", fogo.bm_da_cfg(cfg["Molotov"], Matrix.Rotation(math.radians(-90), 4, "X")), cena),
        fogo.objeto("Item_KitFogueira_corpo", fogo.bm_das_pecas(fogo._kit_fogueira()), cena),
    ]
    for i, o in enumerate(objs):
        o.location = (i * 0.8, 0, 0)
        tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
        vs = [v.co for v in o.data.vertices]
        tam = [max(v[k] for v in vs) - min(v[k] for v in vs) for k in range(3)]
        print(f"  {o.name:26s} {tris:5d} triângulos; {tam[0]:.3f} x {tam[2]:.3f} x {tam[1]:.3f} m (larg x fundo x alto)")
    return objs


def exportar(objs, pasta):
    for o in bpy.context.scene.objects:
        o.select_set(o in objs)
    bpy.context.view_layer.objects.active = objs[0]
    caminho = os.path.join(pasta, "Loot_Etapa34.fbx")
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             bake_anim=False, mesh_smooth_type="FACE")
    print("loot_etapa34: gravado", caminho)


def rodar(etapas, pasta):
    import traceback
    try:
        os.makedirs(pasta, exist_ok=True)
        objs = construir()
        if "tudo" in etapas or "exportar" in etapas:
            exportar(objs, pasta)
        if "tudo" in etapas or "fotos" in etapas:
            fogo.fotos(objs, os.path.join(pasta, "fotos"))
        print("ETAPAS OK")
    except Exception:
        traceback.print_exc()
        print("ETAPA FALHOU")
        sys.exit(1)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    etapas = [a for a in args if not (os.sep in a or "/" in a or ":" in a)] or ["tudo"]
    pastas = [a for a in args if a not in etapas]
    rodar(etapas, pastas[0] if pastas else eq.UNITY)
