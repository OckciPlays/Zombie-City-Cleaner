# As armas de fogo na primeira pessoa (11/11/2026), a conferência da pegada_fogo.py: os braços de um FBX (o do projeto ou
# um novo) com a mão nova (a Maos_Novas_FP, levada ao repouso deste rig: os braços das armas de fogo têm os ombros em
# outro lugar) e a arma que o jogo mostra (a de Art/Armas/Modelos, encaixada como a ArmasNovas da Unity faz); as medidas
# de cada clipe (a dobra, o desvio e a torção dos pulsos, a torção na malha, o salto de um quadro para o outro, a largura
# do pulso, o vão da palma e a pele dentro da arma) e as fotos (a câmera dos braços, de 55 graus, e as mãos de perto).
#   blender -b --factory-startup --disable-autoexec --python pegada_fogo_conferir.py -- <rotulo> <Arma> [fbx=<FBX>]
#   [medir] [fotos] <pasta>   (as fotos e o medidas_<rotulo>_<Arma>.json vão para a pasta)
# E a comparação de dois FBX dos braços (o novo contra o original ou contra outro novo): o fps, os objetos, os ossos, os
# clipes (os nomes e os quadros), a malha e os pesos, e em cada quadro de cada clipe a maior diferença dos ossos dos
# braços e dos dedos (os .L/.R), dos ossos da arma (gun, slide, mag, pump, shell), dos outros ossos e da Boca_FP:
#   blender -b --factory-startup --disable-autoexec --python pegada_fogo_conferir.py -- comparar <a.fbx> <b.fbx>
#   (imprime as linhas COMP e uma linha COMPJSON com tudo)
# 12/11/2026: a pele dentro da arma é medida peça por peça (_bvhs_arma, dentro_robusto; da rodada 2 da pistola): com a
# união das peças (o pente dentro do punho) a paridade dos cortes dava falsos de 14 e 22 mm em quadros soltos.
# ORIGINAIS: o pegada_fogo.py põe aqui a pasta dos FBX originais conferidos (o bracos_fbx lê deles); None: os do projeto.
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

REPO = r"D:\Zombiegame\Zombie-City-Cleaner"
for p in (os.path.join(REPO, "Blender", "Armas"), os.path.join(REPO, "Blender")):
    if p not in sys.path:
        sys.path.insert(0, p)
import agua_animacoes as ag  # noqa: E402
import maos_novas as mn  # noqa: E402
import duas_maos as dm  # noqa: E402
import brancas_corpo as bc  # noqa: E402
import armas  # noqa: E402

MODELOS = os.path.join(REPO, "Assets", "_Game", "Art", "Armas", "Modelos")
TEXTURAS = os.path.join(REPO, "Assets", "_Game", "Art", "Armas", "Texturas")
# os braços -> (a arma do jogo, a arma solta antiga, a textura)
ARMAS = {
    "Rifle": ("Rifle3", "Armas.fbx", "Rifle", os.path.join(TEXTURAS, "Armas_Paleta_Cor.png")),
    "Sniper": ("Sniper3Leve", "Armas_Sniper.fbx", "Sniper", os.path.join(TEXTURAS, "Armas_Paleta_Cor.png")),
    "Espingarda": ("Espingarda2", "Armas.fbx", "Espingarda", os.path.join(TEXTURAS, "Armas_Paleta_Cor.png")),
    "Pistola": ("Pistola", "Armas.fbx", "Pistola", os.path.join(TEXTURAS, "Pistola_Cor.png")),
    "Submetralhadora": ("Submetralhadora3", "Armas.fbx", "Submetralhadora", os.path.join(TEXTURAS, "Armas_Paleta_Cor.png")),
    "Sucata_Rifle": ("Sucata_Rifle", "Armas_Sniper.fbx", "Sniper", os.path.join(REPO, "Assets", "_Game", "Art", "Paleta.png")),
    "Sucata_Submetralhadora": ("Sucata_Submetralhadora", "Armas.fbx", "Submetralhadora",
                               os.path.join(REPO, "Assets", "_Game", "Art", "Paleta.png")),
    "Sucata_Pistola": ("Sucata_Pistola", "Armas.fbx", "Pistola", os.path.join(REPO, "Assets", "_Game", "Art", "Paleta.png")),
}
OSSO_DA_PECA = {"corpo": "gun", "ferrolho": "slide", "carregador": "mag", "telha": "pump", "cartucho": "shell"}


ORIGINAIS = None


def bracos_fbx(arma):
    return os.path.join(ORIGINAIS or ag.JOGADOR, f"Jogador_FP_{ARMAS[arma][2]}.fbx")


def _encaixe(rig, bracos, fbx_antigo, modelo):
    """A matriz que leva o espaço da arma solta (o das armas novas) para a arma dos braços, no repouso."""
    me = bracos.data
    grupos = {g.index: g.name for g in bracos.vertex_groups}
    slot = next(i for i, m in enumerate(me.materials) if m and m.name.startswith("Pistola"))

    def osso(v):
        return grupos[max(v.groups, key=lambda g: g.weight).group] if len(v.groups) else ""
    mw = bracos.matrix_world
    da_arma = {i for p in me.polygons if p.material_index == slot for i in p.vertices}
    destino = [mw @ me.vertices[i].co for i in da_arma if osso(me.vertices[i]) != "shell"]
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ag.JOGADOR, fbx_antigo))
    soltas = [o for o in bpy.data.objects if o not in antes]
    antigas = [o for o in soltas if o.type == "MESH" and o.name.startswith(modelo + "_")]
    org = next(o for o in antigas if o.name.startswith(modelo + "_corpo")).matrix_world.translation.copy()
    origem = [o.matrix_world @ v.co - org for o in antigas for v in o.data.vertices]
    E, erro = armas.encaixe(destino, origem)
    for o in soltas:
        bpy.data.objects.remove(o, do_unlink=True)
    return E, erro, slot


def carregar(arma, fbx=None):
    """O FBX dos braços (o original ou outro), com a mão nova (Maos_Novas_FP) presa ao rig dele e a arma do jogo no lugar
    da antiga. Devolve um dicionário: rig, bracos, maos {L, R}, arma (as peças), acoes, E (o espaço da arma solta)."""
    novo, fbx_antigo, modelo, textura = ARMAS[arma]
    ag.nova_cena()
    objs = ag.importar(fbx or bracos_fbx(arma))
    rig = next(o for o in objs if o.type == "ARMATURE")
    bracos = next(o for o in objs if o.type == "MESH" and o.name.startswith("FP_Bracos"))
    acoes = {a.name.split("|", 1)[1]: a for a in bpy.data.actions if a.name.startswith(rig.name + "|")}
    E, erro, slot = _encaixe(rig, bracos, fbx_antigo, modelo)
    # tira dos braços de hoje a arma antiga, o antebraço e a mão (fica o braço de cima)
    tirar = set(mn.faces_do_braco(bracos, "L")) | set(mn.faces_do_braco(bracos, "R"))
    tirar |= {p.index for p in bracos.data.polygons if p.material_index == slot}
    bm = bmesh.new()
    bm.from_mesh(bracos.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in tirar], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(bracos.data)
    bm.free()
    # a mão nova: os vértices levados do repouso do rig dos punhos para o deste rig (o mesmo giro, outro lugar)
    rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
    novos = ag.importar(mn.SAIDA_FP)
    rig_m = next(o for o in novos if o.type == "ARMATURE")
    rest_m = {b.name: b.matrix_local.copy() for b in rig_m.data.bones}
    maos = {}
    for o in novos:
        if o.type != "MESH" or not o.name.startswith("Mao_FP"):
            continue
        if o.data.shape_keys:
            o.shape_key_clear()
        nomes = {g.index: g.name for g in o.vertex_groups}
        Mw = rig.matrix_world.inverted() @ o.matrix_world
        for v in o.data.vertices:
            p = Mw @ v.co
            soma, w = Vector(), 0.0
            for g in v.groups:
                n = nomes[g.group]
                if n in rest and n in rest_m:
                    soma += g.weight * (rest[n] @ rest_m[n].inverted() @ p)
                    w += g.weight
            if w > 0:
                v.co = Mw.inverted() @ (soma / w)
        o.parent = rig
        o.matrix_parent_inverse = Matrix.Identity(4)
        o.matrix_world = rig.matrix_world @ Mw
        for md in o.modifiers:
            if md.type == "ARMATURE":
                md.object = rig
        maos[o.name[-1]] = o
    for o in novos:
        if o.type == "ARMATURE":
            bpy.data.objects.remove(o, do_unlink=True)
    for o in list(bpy.data.objects):
        if o.type == "MESH" and o.parent is None and o not in maos.values():
            bpy.data.objects.remove(o, do_unlink=True)
    # a arma do jogo: cada peça presa ao osso dela (peso 1), no espaço da arma solta levado por E
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELOS, novo + ".fbx"))
    pecas = {}
    for o in [o for o in bpy.data.objects if o not in antes]:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
            continue
        peca = o.name.split(".")[0][len(novo) + 1:]
        osso = OSSO_DA_PECA.get(peca)
        if osso is None or osso not in rig.data.bones:
            bpy.data.objects.remove(o, do_unlink=True)
            continue
        o.data.transform(rig.matrix_world.inverted() @ E @ o.matrix_world)
        o.parent = rig
        o.matrix_parent_inverse = Matrix.Identity(4)
        o.matrix_world = rig.matrix_world.copy()
        g = o.vertex_groups.new(name=osso)
        g.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
        md = o.modifiers.new("Armature", "ARMATURE")
        md.object = rig
        for m in o.data.materials:
            if m:
                ag._textura_em(m, textura)
        pecas[peca] = o
    ag._textura_no_material("PrincipalBloco", ag.TEXTURA)
    print(f"CARREGAR {arma}: encaixe {erro * 1000:.2f} mm, peças {sorted(pecas)}, mãos {sorted(maos)}")
    return {"rig": rig, "bracos": bracos, "maos": maos, "arma": pecas, "acoes": acoes, "E": E, "esq": ag.Esqueleto(rig)}


# ================================================================================================ as medidas

def _twist(q):
    """A torção (graus) de um giro em volta do Y (a decomposição swing-twist)."""
    if q.w < 0:
        q = -q
    n = math.hypot(q.w, q.y)
    if n < 1e-9:
        return 180.0
    return math.degrees(2 * math.atan2(q.y / n, q.w / n))


def torcoes_na_malha(esq, M, lado):
    """A torção da mão em relação ao osso do antebraço (a que amassa o pulso da malha) e a do antebraço em relação ao
    braço de cima (a do cotovelo), em graus, a partir do repouso."""
    fa, mao, ua = "forearm." + lado, "hand." + lado, "upper_arm." + lado
    r = (esq.rest[fa].inverted() @ esq.rest[mao]).to_quaternion()
    c = (M[fa].inverted() @ M[mao]).to_quaternion()
    t_pulso = _twist(r.inverted() @ c)
    # o antebraço: o giro dele em volta do próprio eixo, comparado com o quadro do plano do braço (o do IK)
    S, Ecot, W = M[ua].translation, M[fa].translation, M[mao].translation
    u = (W - S).normalized()
    e = (Ecot - S) - (Ecot - S).dot(u) * u
    e = e.normalized() if e.length > 1e-6 else u.orthogonal().normalized()
    z = u.cross(e) if lado == "L" else -u.cross(e)
    Mf0 = ag.quadro_yz(W - Ecot, z, Ecot)
    t_ante = _twist((Mf0.to_3x3().inverted() @ M[fa].to_3x3()).to_quaternion())
    return t_pulso, t_ante


def matrizes(rig):
    return {pb.name: pb.matrix.copy() for pb in rig.pose.bones}


def _bvh_arma(c, dg):
    vs, fs = [], []
    for o in c["arma"].values():
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        base = len(vs)
        vs += [o.matrix_world @ v.co for v in me.vertices]
        fs += [[base + i for i in p.vertices] for p in me.polygons]
        ev.to_mesh_clear()
    return BVHTree.FromPolygons(vs, fs)


# (rodada 2 da pistola) a arma por peça: as peças se cruzam (o pente dentro do punho), e a paridade dos cortes na união
# delas erra; cada peça sozinha, com 6 raios inclinados (dentro: 5 ou mais dizem que sim)
_RAIOS = [Vector(v).normalized() for v in ((1, 0.13, 0.07), (-1, 0.09, -0.11), (0.08, 1, 0.12), (-0.1, -1, 0.06),
                                            (0.11, -0.07, 1), (-0.06, 0.1, -1))]


def _bvhs_arma(c, dg):
    out = []
    for o in c["arma"].values():
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        vs = [o.matrix_world @ v.co for v in me.vertices]
        fs = [list(p.vertices) for p in me.polygons]
        ev.to_mesh_clear()
        out.append(BVHTree.FromPolygons(vs, fs))
    return out


def dentro_robusto(bvhs, p, longe=0.03):
    """Quanto o ponto p entra na arma (m; 0 fora): a mais funda das peças em que ele está dentro."""
    fundo = 0.0
    for b in bvhs:
        loc, nor, _, dist = b.find_nearest(p, longe)
        if loc is None or (p - loc).dot(nor) >= 0:
            continue
        votos = 0
        for d in _RAIOS:
            n, o = 0, p.copy()
            for _ in range(40):
                hit = b.ray_cast(o, d)[0]
                if hit is None:
                    break
                n += 1
                o = hit + d * 1e-5
            votos += n % 2
        if votos >= 5:
            fundo = max(fundo, dist)
    return fundo


_PONTOS = {}


def _pontos_da_mao(c, lado):
    """Os vértices da peça do lado: (índices da mão (palma e dedos), índices da palma, osso dono de cada um)."""
    if lado in _PONTOS:
        return _PONTOS[lado]
    o = c["maos"][lado]
    nomes = {g.index: g.name for g in o.vertex_groups}
    dono = {}
    for v in o.data.vertices:
        if len(v.groups):
            dono[v.index] = nomes[max(v.groups, key=lambda g: g.weight).group]
    mao = [i for i, n in dono.items() if not n.startswith(("forearm", "upper_arm"))]
    _PONTOS[lado] = (mao, dono)
    return _PONTOS[lado]


def contato(c, lado, dg, bvh):
    """O contato da mão do lado com a arma no quadro atual: o vão da palma (mm, o menor entre a pele da palma e a arma),
    a pele que entra na arma (mm, a mais funda, e o osso), a distância do meio da mão à arma (cm)."""
    o = c["maos"][lado]
    rig = c["rig"]
    mao, dono = _pontos_da_mao(c, lado)
    ev = o.evaluated_get(dg)
    me = ev.to_mesh()
    ws = {i: o.matrix_world @ me.vertices[i].co for i in mao}
    ev.to_mesh_clear()
    Hi = (rig.matrix_world @ rig.pose.bones["hand." + lado].matrix).inverted()
    vao, dentro, onde, perto = 1e9, 0.0, "", 1e9
    pecas = bvh if isinstance(bvh, list) else None
    for i, w in ws.items():
        if pecas:
            achados = [b.find_nearest(w, 0.08) for b in pecas]
            achados = [x for x in achados if x[0] is not None]
            if not achados:
                continue
            loc, nor, _, dist = min(achados, key=lambda x: x[3])
        else:
            loc, nor, _, dist = bvh.find_nearest(w, 0.08)
        if loc is None:
            continue
        perto = min(perto, dist)
        # dentro: a paridade dos cortes em três raios (o brancas_corpo._dentro; o lado da normal erra nas quinas)
        # (e a normal da superfície mais perto concordando: a paridade sozinha erra onde o raio passa numa emenda)
        if pecas:
            fundo = dentro_robusto(pecas, w)
            inside = fundo > 0
            dist_d = fundo
        else:
            inside = dist < 0.03 and (w - loc).dot(nor) < 0 and bc._dentro(bvh, w)[0]
            dist_d = dist
        if inside and dist_d > dentro:
            dentro, onde = dist_d, dono[i]
        if dono[i] == "hand." + lado:
            h = Hi @ w
            if h.z < -0.004 and 0.03 < h.y < 0.105:
                vao = min(vao, 0.0 if inside else dist)
    return {"vao_palma_mm": round(vao * 1000, 1) if vao < 1e8 else None, "dentro_mm": round(dentro * 1000, 1),
            "dentro_osso": onde, "perto_cm": round(perto * 100, 1) if perto < 1e8 else None}


def medir(c, clipes=None, passo_contato=2, rotulo=""):
    """Por clipe e por lado: o pior e a média da dobra, do desvio e da torção (a do braço, dm.pulso, e a da malha: a
    mão em relação ao antebraço), o maior salto da torção de um quadro para o outro, a largura do pulso no quadro de
    maior torção (% do repouso) e o contato com a arma (nos quadros de passo_contato em passo_contato, com a mão a menos
    de 4 cm da arma: o pior vão da palma e a pele mais funda dentro da arma)."""
    rig, esq = c["rig"], c["esq"]
    anel = {lado: bc._anel_do_pulso(o, lado) for lado, o in c["maos"].items()}
    larg0 = {lado: bc._largura_do_pulso(rig, o, lado, anel[lado], False) for lado, o in c["maos"].items()}
    res = {}
    for nome, a in sorted(c["acoes"].items()):
        if clipes and nome not in clipes:
            continue
        ag.usar_acao(rig, a)
        f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
        lin = {}
        antes = {}
        for f in range(f0, f1 + 1):
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()
            M = matrizes(rig)
            dg = bpy.context.evaluated_depsgraph_get()
            bvh = _bvhs_arma(c, dg) if (f - f0) % passo_contato == 0 or f == f1 else None
            for lado in ("L", "R"):
                d, dv, t = dm.pulso(esq, M, lado)
                tp, ta = torcoes_na_malha(esq, M, lado)
                L = lin.setdefault(lado, {"dobra": [], "desvio": [], "torcao": [], "tor_pulso": [], "tor_ante": [],
                                          "salto": 0.0, "vao": [], "dentro": 0.0, "dentro_osso": "", "pior_tp": (-1, f0)})
                L["dobra"].append(abs(d))
                L["desvio"].append(abs(dv))
                L["torcao"].append(abs(t))
                L["tor_pulso"].append(abs(tp))
                L["tor_ante"].append(abs(ta))
                if lado in antes:
                    dd = abs((tp - antes[lado] + 180) % 360 - 180)
                    L["salto"] = max(L["salto"], dd)
                antes[lado] = tp
                if abs(tp) > L["pior_tp"][0]:
                    L["pior_tp"] = (abs(tp), f)
                if bvh is not None and lado in c["maos"]:
                    k = contato(c, lado, dg, bvh)
                    if k["perto_cm"] is not None and k["perto_cm"] < 1.0:
                        if k["vao_palma_mm"] is not None:
                            L["vao"].append(k["vao_palma_mm"])
                        if k["dentro_mm"] > L["dentro"]:
                            L["dentro"], L["dentro_osso"] = k["dentro_mm"], f"{k['dentro_osso']} q{f}"
        out = {}
        for lado, L in lin.items():
            f = L["pior_tp"][1]
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()
            lp = bc._largura_do_pulso(rig, c["maos"][lado], lado, anel[lado], True)
            med = lambda xs: round(sum(xs) / len(xs)) if xs else None  # noqa: E731
            out[lado] = {"dobra": (round(max(L["dobra"])), med(L["dobra"])),
                         "desvio": (round(max(L["desvio"])), med(L["desvio"])),
                         "torcao": (round(max(L["torcao"])), med(L["torcao"])),
                         "tor_pulso": (round(max(L["tor_pulso"])), med(L["tor_pulso"])),
                         "tor_ante": (round(max(L["tor_ante"])), med(L["tor_ante"])),
                         "salto": round(L["salto"]),
                         "largura_pct": round(lp / larg0[lado] * 100) if lp and larg0.get(lado) else None,
                         "vao_mm": (round(max(L["vao"]), 1), round(sorted(L["vao"])[len(L["vao"]) // 2], 1)) if L["vao"] else None,
                         "dentro_mm": L["dentro"], "dentro_onde": L["dentro_osso"]}
        res[nome] = {"quadros": f1 - f0 + 1, **out}
    print(f"MEDIDAS {rotulo} " + json.dumps(res, ensure_ascii=False))
    return res


def tabela(res, rotulo=""):
    print(f"--- {rotulo}: clipe lado | dobra max/méd | desvio | torção(braço) | torção pulso(malha) | antebraço | salto |"
          " largura% | vão palma max/mediana | dentro mm")
    for nome, r in res.items():
        for lado in ("L", "R"):
            v = r.get(lado)
            if not v:
                continue
            print(f"  {nome:8s} {lado} | {v['dobra'][0]:3d}/{v['dobra'][1]:3d} | {v['desvio'][0]:3d}/{v['desvio'][1]:3d} |"
                  f" {v['torcao'][0]:3d}/{v['torcao'][1]:3d} | {v['tor_pulso'][0]:3d}/{v['tor_pulso'][1]:3d} |"
                  f" {v['tor_ante'][0]:3d}/{v['tor_ante'][1]:3d} | {v['salto']:3d} | {v['largura_pct']} | {v['vao_mm']} |"
                  f" {v['dentro_mm']} {v['dentro_onde']}")


# ================================================================================================ as fotos

def foto_jogo(c, nome, f, caminho, w=640, h=360):
    rig = c["rig"]
    ag.usar_acao(rig, c["acoes"][nome])
    bpy.context.scene.frame_set(f)
    bpy.context.view_layer.update()
    ag._render_base(w, h)
    ag._camera("Olho", (0, 0, 0), rot=(math.radians(90), 0, math.radians(180)), angulo_y=55)
    for o in c["maos"].values():
        o.hide_render = False
    ag._salvar(caminho)


def fotos_perto(c, nome, f, prefixo, w=420, h=320):
    """De perto, no espaço da arma (o cano em -Y, o alto em +Z, a direita em -X): a esquerda de baixo e de lado (da
    esquerda), a direita de lado (da direita) e de cima."""
    rig = c["rig"]
    ag.usar_acao(rig, c["acoes"][nome])
    bpy.context.scene.frame_set(f)
    bpy.context.view_layer.update()
    ag._render_base(w, h)
    G = rig.matrix_world @ rig.pose.bones["gun"].matrix @ rig.data.bones["gun"].matrix_local.inverted() @ \
        rig.matrix_world.inverted() @ c["E"]
    R3 = G.to_3x3().normalized()
    saidas = []
    vistas = {"L": (("baixo", Vector((0.25, -0.15, -1.0)), Vector((0, -1, 0))),
                    ("lado", Vector((1.0, -0.1, -0.25)), Vector((0, 0, 1)))),
              "R": (("lado", Vector((-1.0, 0.15, -0.1)), Vector((0, 0, 1))),
                    ("cima", Vector((-0.35, 0.25, 1.0)), Vector((0, -1, 0))),
                    ("gatilho", Vector((1.0, -0.25, -0.15)), Vector((0, 0, 1))))}
    for lado, vs in vistas.items():
        if lado not in c["maos"]:
            continue
        for o in c["maos"].values():
            o.hide_render = o is not c["maos"][lado]
        c["bracos"].hide_render = True      # (o braço de cima dos braços de hoje: na frente da câmera de perto)
        M = rig.matrix_world @ rig.pose.bones["hand." + lado].matrix
        alvo = M @ Vector((0.0, 0.06, -0.02))
        for vista, d, cima in vs:
            dd = (R3 @ d).normalized()
            pos = alvo + dd * 0.32
            cam = ag._camera("Perto", pos, alvo=alvo, lente=50)
            cam.rotation_euler = mn._olhar(pos, alvo, (R3 @ cima).normalized()).to_euler()
            caminho = f"{prefixo}_{lado}_{vista}.png"
            ag._salvar(caminho)
            saidas.append(caminho)
    for o in c["maos"].values():
        o.hide_render = False
    c["bracos"].hide_render = False
    return saidas


OSSOS_ARMA = ("gun", "slide", "mag", "pump", "shell")


def _ler_fbx(caminho):
    """Tudo o que a comparação olha num FBX dos braços (as matrizes no mundo de todos os ossos em todos os quadros)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = ag.importar(caminho)
    rig = next(o for o in objs if o.type == "ARMATURE")
    boca = next((o for o in objs if o.name.startswith("Boca")), None)
    malha = next(o for o in objs if o.type == "MESH")
    acoes = {x.name.split("|", 1)[1]: x for x in bpy.data.actions if x.name.startswith(rig.name + "|")}
    nomes = {g.index: g.name for g in malha.vertex_groups}
    info = {"fps": bpy.context.scene.render.fps, "ossos": [b.name for b in rig.data.bones],
            "objs": sorted(o.name for o in objs), "malha": (len(malha.data.vertices), len(malha.data.polygons)),
            "clipes": {n: tuple(int(v) for v in x.frame_range) for n, x in acoes.items()},
            "pesos": [{nomes[g.group]: g.weight for g in v.groups} for v in malha.data.vertices],
            "vs": [v.co.copy() for v in malha.data.vertices], "pose": {}}
    for n, x in acoes.items():
        ag.usar_acao(rig, x)
        for f in range(int(x.frame_range[0]), int(x.frame_range[1]) + 1):
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()
            info["pose"][(n, f)] = ({pb.name: (rig.matrix_world @ pb.matrix).copy() for pb in rig.pose.bones},
                                    boca.matrix_world.copy() if boca else None)
    return info


def comparar(a, b):
    """A diferença entre dois FBX dos braços (ver o cabeçalho). Devolve o resumo (também impresso)."""
    A, B = _ler_fbx(a), _ler_fbx(b)
    r = {"fps": [A["fps"], B["fps"]], "ossos_iguais": A["ossos"] == B["ossos"], "objetos_iguais": A["objs"] == B["objs"],
         "clipes_iguais": A["clipes"] == B["clipes"], "clipes": A["clipes"], "clipes_b": B["clipes"],
         "malha_igual": A["malha"] == B["malha"]}
    r["peso_max"] = max((abs(pa.get(k, 0.0) - pb.get(k, 0.0)) for pa, pb in zip(A["pesos"], B["pesos"])
                         for k in set(pa) | set(pb)), default=0.0)
    r["vertice_max_mm"] = max(((va - vb).length * 1000 for va, vb in zip(A["vs"], B["vs"])), default=0.0)
    grupos = {"bracos": [0.0, 0.0, None], "arma": [0.0, 0.0, None], "outros": [0.0, 0.0, None], "boca": [0.0, 0.0, None]}
    faltam = []
    for k, (ma, ba) in A["pose"].items():
        if k not in B["pose"]:
            faltam.append(k)
            continue
        mb, bb = B["pose"][k]
        for osso, x in ma.items():
            y = mb.get(osso)
            if y is None:
                faltam.append((k, osso))
                continue
            g = "bracos" if osso.endswith((".L", ".R")) else ("arma" if osso in OSSOS_ARMA else "outros")
            d = (x.translation - y.translation).length * 1000
            ang = math.degrees(x.to_3x3().normalized().to_quaternion().rotation_difference(
                y.to_3x3().normalized().to_quaternion()).angle)
            ang = min(ang, 360 - ang)
            if d > grupos[g][0]:
                grupos[g][0], grupos[g][2] = d, f"{k[0]} q{k[1]} {osso}"
            grupos[g][1] = max(grupos[g][1], ang)
        if ba is not None and bb is not None:
            d = (ba.translation - bb.translation).length * 1000
            ang = math.degrees(ba.to_3x3().normalized().to_quaternion().rotation_difference(
                bb.to_3x3().normalized().to_quaternion()).angle)
            if d > grupos["boca"][0]:
                grupos["boca"][0], grupos["boca"][2] = d, f"{k[0]} q{k[1]}"
            grupos["boca"][1] = max(grupos["boca"][1], min(ang, 360 - ang))
    r["faltam"] = [str(x) for x in faltam[:20]]
    r["n_faltam"] = len(faltam)
    r["quadros"] = len(A["pose"])
    for g, (d, ang, onde) in grupos.items():
        r[g] = {"mm": round(d, 4), "graus": round(ang, 4), "onde": onde}
    print(f"COMP {os.path.basename(a)} x {os.path.basename(b)}")
    print(f"COMP fps {r['fps']} | ossos iguais {r['ossos_iguais']} | objetos iguais {r['objetos_iguais']} | malha igual "
          f"{r['malha_igual']} | peso {r['peso_max']:.6f} | vértice {r['vertice_max_mm']:.4f} mm")
    print(f"COMP clipes iguais {r['clipes_iguais']}: {r['clipes']}" + ("" if r["clipes_iguais"] else f" x {r['clipes_b']}"))
    print(f"COMP {r['quadros']} quadros; faltam {r['n_faltam']}")
    for g in grupos:
        print(f"COMP {g}: {r[g]['mm']:.4f} mm, {r[g]['graus']:.4f} graus ({r[g]['onde']})")
    print("COMPJSON " + json.dumps(r, ensure_ascii=False))
    return r


if __name__ == "__main__" and "--" in sys.argv and sys.argv[sys.argv.index("--") + 1:][:1] == ["comparar"]:
    _a = sys.argv[sys.argv.index("--") + 1:]
    comparar(_a[1], _a[2])
elif __name__ == "__main__" and "--" in sys.argv:
    import time
    args = sys.argv[sys.argv.index("--") + 1:]
    rotulo, arma, pasta = args[0], args[1], args[-1]
    fbx = next((a.split("=", 1)[1] for a in args if a.startswith("fbx=")), None)
    os.makedirs(pasta, exist_ok=True)
    t0 = time.time()
    c = carregar(arma, fbx)
    if "medir" in args:
        res = medir(c, rotulo=f"{rotulo}_{arma}")
        tabela(res, f"{rotulo} {arma}")
        with open(os.path.join(pasta, f"medidas_{rotulo}_{arma}.json"), "w", encoding="utf-8") as fh:
            json.dump(res, fh, ensure_ascii=False, indent=1)
    if "fotos" in args:
        n_rel = int(c["acoes"]["Reload"].frame_range[1])
        for nome, f in (("Idle", 1), ("Fire", 2), ("Reload", max(1, n_rel // 2)), ("Walk", 5)):
            foto_jogo(c, nome, f, os.path.join(pasta, f"{rotulo}_{arma}_{nome}{f:02d}.png"))
        fotos_perto(c, "Idle", 1, os.path.join(pasta, f"{rotulo}_{arma}_perto"))
    print(f"CONFERIR {rotulo} {arma}: {time.time() - t0:.1f} s")
