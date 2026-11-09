# As armas de sucata no jogo (09/11/2026, a etapa 1b): as três do sucata.py (a pistola, a submetralhadora e o rifle de
# precisão) exportadas como as armas novas de hoje, cada uma no lugar da equivalente: a pistola sobre os braços e as
# animações da pistola, a submetralhadora sobre os da submetralhadora e o rifle sobre os da sniper (o ferrolho).
#
# A convenção é a dos FBX de Assets/_Game/Art/Armas/Modelos (o armas.exportar()): um objeto por peça, <Nome>_corpo,
# <Nome>_ferrolho e <Nome>_carregador, cada um girado 90° em X, com a malha nos eixos da Unity (o cano em +Z, o alto em
# +Y, a origem no cabo), no mesmo espaço da arma antiga do Armas.fbx (o ArmasNovas.Encaixe mede por ela). Só entram as
# peças que a arma tem: a pistola, de um tiro, tem o corpo e o bloquinho de um tiro (o carregador, que entra por
# baixo do cabo); o rifle, de um tiro por vez, tem o corpo e o ferrolho (sem carregador).
# O material é um só, <Nome>_Paleta, com a Paleta.png do jogo (as cores novas da sucata estão nas células livres dela).
#
# O cabo de cada uma cai onde a mão da equivalente segura (conferido com as mãos dos braços de primeira pessoa no Idle):
# o deslocamento de cada arma, em Y do Blender (o cano para -Y), é posto só aqui, na exportação.
#
# Como rodar (um Blender em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/Armas/sucata_exportar.py -- <paleta|armas|tudo>
#   paleta: copia as 8 células novas da sucata (sucata.NOVAS) para a Assets/_Game/Art/Paleta.png, só se estiverem livres
#   (o cinza 404040), e confere que as outras não mudaram; armas: grava os Sucata_<Arma>.fbx em Art/Armas/Modelos.

import math
import os
import struct
import sys
import zlib

import bmesh
import bpy
from mathutils import Matrix

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import sucata  # noqa: E402

MODELOS = os.path.join(sucata.REPO, "Assets", "_Game", "Art", "Armas", "Modelos")
PALETA = sucata.PALETA
LIVRE = (0x40, 0x40, 0x40)   # a cor das células vazias da paleta

# o nome (do FBX e das peças), a função do sucata.py, o deslocamento em Y (m) e a equivalente de hoje
ARMAS = [
    ("Sucata_Pistola", sucata.pistola, -0.010, "Pistola"),
    ("Sucata_Submetralhadora", sucata.submetralhadora, 0.022, "Submetralhadora3"),
    ("Sucata_Rifle", sucata.rifle, -0.020, "Sniper3Leve"),
]
# a altura do cano (o Z de cada função do sucata.py), para a boca
CANO = {"Sucata_Pistola": 0.088, "Sucata_Submetralhadora": 0.100, "Sucata_Rifle": 0.105}


# ------------------------------------------------------------------------------------------------ a paleta
def _ler_png(caminho):
    """Os pedaços do PNG e os pixels (RGBA de 8 bits, linha 0 em cima), sem passar pela cor do Blender."""
    dados = open(caminho, "rb").read()
    assert dados[:8] == b"\x89PNG\r\n\x1a\n"
    pedacos, i = [], 8
    while i < len(dados):
        n = struct.unpack(">I", dados[i:i + 4])[0]
        pedacos.append((dados[i + 4:i + 8], dados[i + 8:i + 8 + n]))
        i += 12 + n
    w, h, prof, tipo, _, _, entrelacado = struct.unpack(">IIBBBBB", pedacos[0][1])
    assert (prof, tipo, entrelacado) == (8, 6, 0), "a paleta devia ser RGBA de 8 bits"
    cru = zlib.decompress(b"".join(d for t, d in pedacos if t == b"IDAT"))
    linhas, ant, k = [], bytearray(w * 4), 0
    for _ in range(h):
        filtro, lin = cru[k], bytearray(cru[k + 1:k + 1 + w * 4])
        k += 1 + w * 4
        for x in range(w * 4):
            a = lin[x - 4] if x >= 4 else 0
            b = ant[x]
            c = ant[x - 4] if x >= 4 else 0
            if filtro == 1:
                lin[x] = (lin[x] + a) & 255
            elif filtro == 2:
                lin[x] = (lin[x] + b) & 255
            elif filtro == 3:
                lin[x] = (lin[x] + (a + b) // 2) & 255
            elif filtro == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                lin[x] = (lin[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        linhas.append(lin)
        ant = lin
    return pedacos, w, h, linhas


def _gravar_png(caminho, pedacos, linhas):
    """Os mesmos pedaços, com os pixels novos num IDAT só (sem filtro)."""
    idat = zlib.compress(b"".join(b"\x00" + bytes(l) for l in linhas), 9)
    saida, posto = [b"\x89PNG\r\n\x1a\n"], False
    for t, d in pedacos:
        if t == b"IDAT":
            if posto:
                continue
            d, posto = idat, True
        saida.append(struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff))
    open(caminho, "wb").write(b"".join(saida))


def paleta():
    """As 8 células da sucata na Paleta.png do jogo: só nas livres (ou já com a mesma cor); as outras não mudam."""
    pedacos, w, h, linhas = _ler_png(PALETA)
    antes = [bytes(l) for l in linhas]
    mudou = []
    for nome, ((col, lin), cor, alfa) in sucata.NOVAS.items():
        y = h - 1 - lin                    # a linha da sucata conta de baixo; a do PNG, de cima
        nova = (int(cor[0:2], 16), int(cor[2:4], 16), int(cor[4:6], 16), alfa)
        atual = tuple(linhas[y][col * 4:col * 4 + 4])
        if atual == nova:
            continue
        if atual[:3] != LIVRE:
            raise RuntimeError(f"a célula ({col}, {lin}) da paleta não está livre: {bytes(atual).hex()} ({nome})")
        linhas[y][col * 4:col * 4 + 4] = bytes(nova)
        mudou.append((col, lin, bytes(atual).hex(), bytes(nova).hex()))
    if mudou:
        _gravar_png(PALETA, pedacos, linhas)
    # a prova: relê e compara pixel a pixel (só as 8 células podem ter mudado)
    _, _, _, relidas = _ler_png(PALETA)
    celulas = {(c, h - 1 - l) for (c, l), _, _ in sucata.NOVAS.values()}
    outras = [(x, y) for y in range(h) for x in range(w)
              if (x, y) not in celulas and relidas[y][x * 4:x * 4 + 4] != antes[y][x * 4:x * 4 + 4]]
    assert not outras, f"mudaram células que não eram da sucata: {outras}"
    print("paleta:", "mudou" if mudou else "já estava pronta", mudou, "| as outras", w * h - len(celulas),
          "células iguais")


# ------------------------------------------------------------------------------------------------ as armas
def _material(nome):
    m = bpy.data.materials.new(nome)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new("ShaderNodeTexImage")
    img = bpy.data.images.load(PALETA, check_existing=True)
    img.alpha_mode = "NONE"   # o alfa é o brilho da cidade, não transparência
    tex.image = img
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.7
    return m


def _ilhas(bm, faces):
    """As faces separadas em pedaços ligados (cada primitiva do sucata.py é um pedaço)."""
    resto, ilhas = set(faces), []
    while resto:
        f0 = resto.pop()
        ilha, pilha = [f0], [f0]
        while pilha:
            f = pilha.pop()
            for v in f.verts:
                for g in v.link_faces:
                    if g in resto:
                        resto.remove(g)
                        ilha.append(g)
                        pilha.append(g)
        ilhas.append(ilha)
    return ilhas


def _parado(ilha):
    """No rifle, o que é do ferrolho de porta mas fica preso à coronha (a chapa, as guias e os parafusos): só a haste,
    a alavanca e a bola andam com o osso do ferrolho."""
    vs = [v.co for f in ilha for v in f.verts]
    return max(v.y for v in vs) - min(v.y for v in vs) < 0.10 and min(v.x for v in vs) > -0.02


def pecas(nome, func, dy):
    """As peças da arma (o corpo, o ferrolho, o carregador), cada uma num bmesh sem os grupos de vértices."""
    m = sucata.Malha()
    func(m, jogo=True)   # a versão do jogo (sucata.py)
    o = m.objeto(nome + "_autoria", bpy.context.scene.collection, bpy.data.materials.new("tmp"))
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bpy.data.objects.remove(o, do_unlink=True)
    camada = bm.faces.layers.int.new("grupo")   # a peça de cada face (vai junto na cópia)
    dv = bm.verts.layers.deform.active
    for f in bm.faces:
        pesos = f.verts[0][dv]
        f[camada] = next((g for g in range(3) if pesos.get(g, 0) > 0.5), 0)
    if nome == "Sucata_Rifle":
        for ilha in _ilhas(bm, [f for f in bm.faces if f[camada] == 1]):
            if _parado(ilha):
                for f in ilha:
                    f[camada] = 0
    bm.transform(Matrix.Translation((0, dy, 0)))
    saida = {}
    for g, peca in enumerate(m.grupos):
        if not any(f[camada] == g for f in bm.faces):
            continue
        b = bm.copy()
        cb = b.faces.layers.int["grupo"]
        bmesh.ops.delete(b, geom=[f for f in b.faces if f[cb] != g], context="FACES")
        bmesh.ops.delete(b, geom=[v for v in b.verts if not v.link_faces], context="VERTS")
        b.faces.layers.int.remove(cb)
        if b.verts.layers.deform.active:
            b.verts.layers.deform.remove(b.verts.layers.deform.active)
        saida[peca] = b
    bm.free()
    return saida


def exportar():
    os.makedirs(MODELOS, exist_ok=True)
    giro = Matrix.Rotation(math.radians(-90), 4, "X")
    for nome, func, dy, equivalente in ARMAS:
        for o in list(bpy.data.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        mat = _material(nome + "_Paleta")
        objs, tri = [], {}
        for peca, b in pecas(nome, func, dy).items():
            me = bpy.data.meshes.new(f"{nome}_{peca}")
            b.to_mesh(me)
            b.free()
            for p in me.polygons:
                p.use_smooth = False
            me.transform(giro)   # a malha nos eixos da Unity; o objeto girado 90° em X (o armas.exportar())
            me.materials.append(mat)
            o = bpy.data.objects.new(me.name, me)
            o.rotation_euler = (math.radians(90), 0, 0)
            bpy.context.scene.collection.objects.link(o)
            objs.append(o)
            tri[peca] = sum(len(p.vertices) - 2 for p in me.polygons)
        for o in bpy.data.objects:
            o.select_set(o in objs)
        caminho = os.path.join(MODELOS, nome + ".fbx")
        bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH"},
                                 apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="OFF",
                                 use_tspace=False, path_mode="STRIP")
        # a boca (na malha da arma solta: origem no cabo, cano em +Z, o alto em +Y, como o Armas.boca da Unity)
        frente = max(v.co.z for o in objs for v in o.data.vertices)
        print(f"  exportado: {caminho} | equivalente {equivalente} | dy {dy:+.3f} | triângulos {tri} = "
              f"{sum(tri.values())} | boca (0, {CANO[nome]:.3f}, {frente:.3f})")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    etapa = args[0] if args else "tudo"
    if etapa in ("paleta", "tudo"):
        paleta()
    if etapa in ("armas", "tudo"):
        exportar()
