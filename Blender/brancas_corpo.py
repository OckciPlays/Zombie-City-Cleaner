# As armas brancas no corpo (08/11/2026, a pedido: "Vamos dar continuidade para o pegar das armas brancas e ajustar";
# "na terceira pessoa, o machado fica um pouco torto, eu acho que deveria ser mais inclinado para trás, para facilitar o
# golpe"; decidido: todas as armas brancas (a faca, o machado, o bastão com pregos, a lança e a tocha), e o machado parado
# fica apoiado no ombro, com uma mão no cabo, e no golpe desce com as duas mãos). Hoje o corpo usa a guarda dos punhos
# (Maos_Guarda) e o soco direito (Maos_SocoDireito), com a arma num punho só.
#
# Feito por script num Blender sem janela, a partir dos FBX que já estão no projeto (que só são lidos), num FBX novo:
#
#   Assets/_Game/Art/Characters/Personagens/Personagem_PrincipalBloco_Brancas.fbx
#       só o esqueleto do corpo (PrincipalBloco_Rig, o do Personagem_PrincipalBloco.fbx: os mesmos nomes, a mesma
#       hierarquia e o mesmo repouso) com um osso a mais, "arma" (filho do hand.R, não deforma nada; a cabeça na origem da
#       arma, a do Itens_<Arma>.fbx, onde a mão segura; o Y dele para a cabeça da arma e o Z para o alto dela), e os clipes
#       de cada arma: "<Arma>_Segurar" (em laço) e "<Arma>_Golpe" (uma vez). Em cada clipe o osso "arma" fica parado no
#       espaço da mão (a pegada da arma: a mesma da primeira pessoa, a do Armas/pegada.py), e os dedos fecham no cabo.
#       Os clipes gravam todos os ossos em todos os quadros (posição, giro e escala), a 30 quadros por segundo; o tronco e
#       os braços se mexem, as pernas ficam no repouso (na Unity, a camada do tronco, com a máscara dele).
#
# Os clipes (a duração do golpe é o "intervalo" da arma no Loot/Itens.cs e o acerto bate com o "impacto" dela):
#   Faca:    Faca_Segurar (2,0 s): empunhada na mão direita, na frente da cintura, a lâmina para a frente e para cima, a
#            esquerda na guarda; Faca_Golpe (0,5 s): a estocada para a frente, o acerto em 0,125 s (o braço esticado).
#   Machado: Machado_Segurar (2,0 s): pronto para o golpe, nas duas mãos (a esquerda embaixo da direita), na frente do
#            ombro direito, a cabeça para trás e para cima (09/11/2026; antes, apoiado no ombro com uma mão);
#            Machado_Golpe (0,9 s): sobe atrás da cabeça e desce de cima para baixo, reto para a frente, com as duas mãos e
#            o fio na frente; o acerto em 0,292 s (7/24).
#   Bastao:  Bastao_Segurar (2,0 s): nas duas mãos, como um taco, em cima do ombro direito; Bastao_Golpe (0,8 s): o golpe de
#            lado, da direita para a esquerda, na altura do peito; o acerto em 0,292 s.
#   Lanca:   Lanca_Segurar (2,0 s): nas duas mãos, deitada na altura do peito, a ponta reto para a frente (09/11/2026);
#            Lanca_Golpe (0,85 s): a estocada nessa linha; o acerto em 0,25 s (6/24).
#   Tocha:   Tocha_Segurar (2,0 s): na mão direita, erguida para a frente; Tocha_Golpe (0,5 s): o golpe de cima para baixo;
#            o acerto em 0,125 s.
#
# Como as poses são feitas: chaves da arma (onde a pegada da mão direita fica no mundo, para onde a cabeça da arma aponta e
# para onde o alto dela fica), da mão esquerda (no cabo ou solta) e do tronco, misturadas por curvas (a _Curva do
# agua_animacoes.py); a mão direita sai da arma (a pegada ao contrário) e os braços por IK de dois ossos (o do
# agua_animacoes.py). Daqui também saem as fotos das armas brancas (a primeira pessoa e o corpo, hoje e o novo).
#
# Como rodar (Blender 5.2, em segundo plano; um Blender por vez):
#   blender --background --factory-startup --python Blender/brancas_corpo.py -- tudo [pasta das fotos]
#   (as etapas: corpo, conferir, fotos; "fotos:FP,Corpo" faz só umas; as fotos de hoje da primeira pessoa usam as cópias
#   dos FBX de antes em <pasta das fotos>/fbx_hoje/Jogador_FP_<Arma>.fbx)
# As folhas (fora do Blender, com o Pillow): py Blender/brancas_corpo.py folhas <pasta das fotos>

import json
import math
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
for p in (AQUI, os.path.join(AQUI, "Armas")):
    if p not in sys.path:
        sys.path.insert(0, p)
import agua_animacoes as ag  # noqa: E402

try:
    import bpy
    import bmesh
    from mathutils import Matrix, Quaternion, Vector
    import maos_novas as mn  # noqa: E402
except ImportError:
    bpy = None

ARMAS = ("Faca", "Machado", "Bastao", "Lanca", "Tocha")
MODULOS = {"Faca": "faca", "Machado": "machado", "Bastao": "bastao", "Lanca": "lanca", "Tocha": "tocha"}
SAIDA = os.path.join(ag.PERSONAGENS, "Personagem_PrincipalBloco_Brancas.fbx")
PALETA = os.path.join(ag.ARTE, "Paleta.png")
FPS = 30
RIG = ag.RIG_CORPO


def cfg_de(arma):
    import importlib
    return importlib.import_module(MODULOS[arma])


def fbx_fp(arma):
    return os.path.join(ag.JOGADOR, f"Jogador_FP_{arma}.fbx")


# ================================================================================================ as fotos da primeira pessoa

def _bracos_com_a_mao_nova(fbx):
    """O FBX dos braços (o rig, a malha com a arma e a boca) com a peça nova (o antebraço e a mão do Maos_Novas_FP.fbx) no
    lugar da pele de hoje (as faces do corte saem; a arma fica). Devolve (rig, malha, ações)."""
    ag.nova_cena()
    novos = ag.importar(fbx)
    rig = next(o for o in novos if o.type == "ARMATURE")
    bracos = next(o for o in novos if o.type == "MESH" and o.name.startswith("FP_Bracos"))
    for o in novos:
        if o not in (rig, bracos):
            bpy.data.objects.remove(o, do_unlink=True)
    acoes = {a.name.split("|", 1)[1]: a for a in bpy.data.actions if a.name.startswith(rig.name + "|")}
    tirar = set(mn.faces_do_braco(bracos, "L")) | set(mn.faces_do_braco(bracos, "R"))
    bm = bmesh.new()
    bm.from_mesh(bracos.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in tirar], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(bracos.data)
    bm.free()
    novos2 = ag.importar(mn.SAIDA_FP)
    for o in novos2:
        if o.type == "MESH":
            o.parent = rig
            o.matrix_parent_inverse = Matrix.Identity(4)
            if o.data.shape_keys:
                o.shape_key_clear()
            for md in o.modifiers:
                if md.type == "ARMATURE":
                    md.object = rig
    for o in novos2:
        if o.type == "ARMATURE":
            bpy.data.objects.remove(o, do_unlink=True)
    ag._textura_no_material("PrincipalBloco", ag.TEXTURA)
    ag._textura_no_material("Pistola", PALETA)
    return rig, bracos, acoes


def _quadros_do_golpe(cfg, n):
    q = getattr(cfg, "QUADROS_DO_GOLPE", (1, 3, 5, 8))
    return [f for f in q if f <= n]


def fotos_fp(pasta, arma):
    """A arma na primeira pessoa, hoje e a nova (as duas com a mão nova): pela câmera dos braços (55°) o Idle e os
    quadros do golpe; e de perto, a mão direita (e a esquerda, nas de duas mãos) no Idle."""
    cfg = cfg_de(arma)
    hoje = os.path.join(pasta, "fbx_hoje", f"Jogador_FP_{arma}.fbx")
    saidas = []
    for qual, fbx in (("hoje", hoje), ("nova", fbx_fp(arma))):
        if not os.path.exists(fbx):
            continue
        rig, bracos, acoes = _bracos_com_a_mao_nova(fbx)
        golpe = "Golpe" if "Golpe" in acoes else "SocoDireito"
        ag._render_base(640, 360)
        cam = ag._camera("Olho", (0, 0, 0), rot=(math.radians(90), 0, math.radians(180)), angulo_y=55)
        for nome, quadros in (("Idle", [1]), (golpe, _quadros_do_golpe(cfg, int(acoes[golpe].frame_range[1])))):
            ag.usar_acao(rig, acoes[nome])
            for f in quadros:
                bpy.context.scene.frame_set(f)
                cam.location, cam.rotation_euler = (0, 0, 0), (math.radians(90), 0, math.radians(180))
                c = os.path.join(pasta, f"BrancasFP_{arma}_{'1Idle' if nome == 'Idle' else '2Golpe'}{f:02d}_{qual}.png")
                ag._salvar(c)
                saidas.append(c)
        # de perto, no Idle
        ag.usar_acao(rig, acoes["Idle"])
        bpy.context.scene.frame_set(1)
        bpy.context.view_layer.update()
        ag._render_base(480, 360)
        lados = ["R"] + (["L"] if hasattr(cfg, "SEGUNDA_MAO") else [])
        pecas = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Mao_FP")]
        for lado in lados:
            # de perto, só a peça (o antebraço e a mão) do lado: a do outro braço não fica na frente da câmera
            for o in pecas:
                o.hide_render = not o.name.endswith("." + lado)
            M = rig.matrix_world @ rig.pose.bones["hand." + lado].matrix
            c0 = M @ Vector((0.0, 0.07, -0.02))
            dedos, costas = (M.to_3x3() @ Vector((0, 1, 0))).normalized(), (M.to_3x3() @ Vector((0, 0, 1))).normalized()
            pol = (M.to_3x3() @ Vector((1.0 if lado == "L" else -1.0, 0, 0))).normalized()
            for vista, d, cima in (("polegar", pol * 0.9 + costas * 0.3 + dedos * 0.2, costas),
                                   ("palma", -costas * 0.8 + dedos * 0.5, dedos),
                                   ("dedos", dedos * 0.9 + costas * 0.2 - pol * 0.3, costas)):
                pos = c0 + d.normalized() * 0.26
                cam = ag._camera("Perto", pos, alvo=c0, lente=50)
                cam.rotation_euler = mn._olhar(pos, c0, cima).to_euler()
                c = os.path.join(pasta, f"BrancasFP_{arma}_3Perto{lado}_{vista}_{qual}.png")
                ag._salvar(c)
                saidas.append(c)
            # o pulso de lado (do lado do polegar, na linha do antebraço: a passagem do antebraço para a mão, sem quebra)
            Mf = rig.matrix_world @ rig.pose.bones["forearm." + lado].matrix
            eixo = ((Mf.to_3x3() @ Vector((0, 1, 0))) + dedos).normalized()
            lado_p = (pol - pol.dot(eixo) * eixo).normalized()
            w0 = M.translation + eixo * 0.015
            pos = w0 + lado_p * 0.30
            cam = ag._camera("Perto", pos, alvo=w0, lente=50)
            cam.rotation_euler = mn._olhar(pos, w0, eixo).to_euler()
            c = os.path.join(pasta, f"BrancasFP_{arma}_3Perto{lado}_pulso_{qual}.png")
            ag._salvar(c)
            saidas.append(c)
        for o in pecas:
            o.hide_render = False
    return saidas


# o quadro do acerto no Golpe da primeira pessoa (o impacto do Loot/Itens.cs, em quadros de 24 por segundo, mais 1)
ACERTO_FP = {"Lanca": 7, "Machado": 8, "Bastao": 8}
DUAS_MAOS = ("Lanca", "Machado", "Bastao")


def _pontos_da_arma(bracos):
    """Os índices dos vértices da arma na malha dos braços (as faces do material da arma, o "Pistola")."""
    slot = next(i for i, m in enumerate(bracos.data.materials) if m and m.name.startswith("Pistola"))
    return sorted({v for f in bracos.data.polygons if f.material_index == slot for v in f.vertices}), slot


def _dentro(bvh, p, longe=0.03):
    """Se o ponto está dentro da malha (a paridade dos cortes em três raios, para +X, +Y e +Z: dentro se dois ou mais
    dizem que sim; a malha da arma tem peças que se cruzam e faces escondidas tiradas, e um raio só erra) e a que
    distância da superfície."""
    loc, nor, _, dist = bvh.find_nearest(p, longe)
    if loc is None:
        return False, None
    votos = 0
    for d in (Vector((1.0, 0.0, 0.0)), Vector((0.0, 1.0, 0.0)), Vector((0.0, 0.0, 1.0))):
        n, o = 0, p.copy()
        for _ in range(40):
            hit, _, _, t = bvh.ray_cast(o, d)
            if hit is None:
                break
            n += 1
            o = hit + d * 1e-5
        votos += n % 2
    return votos >= 2, dist


def _quadros_a_conferir(arma, nome, a):
    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
    return sorted({f0, (f0 + f1) // 2, f1} | ({ACERTO_FP[arma]} if nome == "Golpe" and arma in ACERTO_FP else set()))


def _pecas_do_braco(rig, bracos):
    """As malhas dos braços (sem a arma): as peças novas (o antebraço e a mão de cada lado) e as faces de pele do
    FP_Bracos (o braço de cima). Devolve [(objeto, índices das faces ou None para todas)]."""
    slot = next(i for i, m in enumerate(bracos.data.materials) if m and m.name.startswith("Pistola"))
    pele = [p.index for p in bracos.data.polygons if p.material_index != slot]
    out = [(o, None) for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Mao_FP")]
    return out + [(bracos, pele)]


def _triangulos_do_braco(pecas):
    """Os triângulos dos braços no quadro atual, no mundo (a câmera na origem): uma matriz (n, 3, 3)."""
    import numpy as np
    dg = bpy.context.evaluated_depsgraph_get()
    tris = []
    for o, faces in pecas:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        mw = o.matrix_world
        co = [mw @ v.co for v in me.vertices]
        polys = me.polygons if faces is None else [me.polygons[i] for i in faces]
        for p in polys:
            vs = list(p.vertices)
            for k in range(1, len(vs) - 1):
                tris.append([tuple(co[vs[0]]), tuple(co[vs[k]]), tuple(co[vs[k + 1]])])
        ev.to_mesh_clear()
    return np.array(tris)


def _cobertura_da_tela(tris, nx=192, ny=108):
    """A fração da tela (a câmera dos braços, 55 graus, 16:9) coberta pelos triângulos: cada um projetado e pintado numa
    grade (os que passam para trás da câmera ficam de fora)."""
    import numpy as np
    import duas_maos as dm
    if not len(tris):
        return 0.0
    d = -tris[:, :, 1]
    ok = (d > 0.02).all(axis=1)
    t = tris[ok]
    d = d[ok]
    u = (-t[:, :, 0] / d / dm.TX + 1) / 2 * nx
    v = (1 - (t[:, :, 2] / d / dm.TY + 1) / 2) * ny
    grade = np.zeros((ny, nx), bool)
    xs, ys = np.meshgrid(np.arange(nx) + 0.5, np.arange(ny) + 0.5)
    for i in range(len(t)):
        x0, x1 = int(max(0, np.floor(u[i].min()))), int(min(nx, np.ceil(u[i].max())))
        y0, y1 = int(max(0, np.floor(v[i].min()))), int(min(ny, np.ceil(v[i].max())))
        if x1 <= x0 or y1 <= y0:
            continue
        px, py = xs[y0:y1, x0:x1], ys[y0:y1, x0:x1]
        (ax, bx, cx), (ay, by, cy) = u[i], v[i]
        e0 = (bx - ax) * (py - ay) - (by - ay) * (px - ax)
        e1 = (cx - bx) * (py - by) - (cy - by) * (px - bx)
        e2 = (ax - cx) * (py - cy) - (ay - cy) * (px - cx)
        dentro = ((e0 >= 0) & (e1 >= 0) & (e2 >= 0)) | ((e0 <= 0) & (e1 <= 0) & (e2 <= 0))
        grade[y0:y1, x0:x1] |= dentro
    return float(grade.mean())


def _anel_do_pulso(o, lado):
    """Os vértices do pulso da peça (os que têm pelo menos 1/4 do peso no antebraço e 1/4 na mão)."""
    nomes = {g.index: g.name for g in o.vertex_groups}
    out = []
    for v in o.data.vertices:
        w = {nomes[g.group]: g.weight for g in v.groups}
        if w.get("forearm." + lado, 0) >= 0.25 and w.get("hand." + lado, 0) >= 0.25:
            out.append(v.index)
    return out


def _largura_minima(pts, eixo):
    """A largura mínima (m) dos pontos vistos ao longo do eixo (o menor calibre, de 5 em 5 graus)."""
    eixo = eixo.normalized()
    a = eixo.orthogonal().normalized()
    b = eixo.cross(a)
    q = [(p.dot(a), p.dot(b)) for p in pts]
    menor = 9.0
    for g in range(0, 180, 5):
        c, s = math.cos(math.radians(g)), math.sin(math.radians(g))
        proj = [x * c + y * s for x, y in q]
        menor = min(menor, max(proj) - min(proj))
    return menor


def _largura_do_pulso(rig, o, lado, idx, posado):
    """A largura mínima da malha do pulso (m): no repouso (posado=False) ou no quadro atual."""
    if not idx:
        return None
    if posado:
        dg = bpy.context.evaluated_depsgraph_get()
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        pts = [o.matrix_world @ me.vertices[i].co for i in idx]
        ev.to_mesh_clear()
        Mf = rig.matrix_world @ rig.pose.bones["forearm." + lado].matrix
        Mh = rig.matrix_world @ rig.pose.bones["hand." + lado].matrix
    else:
        pts = [o.matrix_world @ o.data.vertices[i].co for i in idx]
        Mf = rig.matrix_world @ rig.data.bones["forearm." + lado].matrix_local
        Mh = rig.matrix_world @ rig.data.bones["hand." + lado].matrix_local
    eixo = Mf.to_3x3() @ Vector((0, 1, 0)) + Mh.to_3x3() @ Vector((0, 1, 0))
    return _largura_minima(pts, eixo)


def _pontos_da_arma_mundo(bracos, idx):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = bracos.evaluated_get(dg)
    me = ev.to_mesh()
    pa = [bracos.matrix_world @ me.vertices[i].co for i in idx]
    ev.to_mesh_clear()
    return pa


def _perto_da_camera(tris):
    import numpy as np
    return float(np.linalg.norm(tris.reshape(-1, 3), axis=1).min()) if len(tris) else 9.0


def conferir_fp(pasta, armas=None):
    """Os números da primeira pessoa, em cada clipe de cada arma (o primeiro, o do meio e o último quadro, e o do acerto no
    Golpe), da nova e (o que dá para comparar) da de hoje (fbx_hoje):
    - a pele que entra na arma (mm) e o vão entre a palma e o cabo (mm);
    - nas de duas mãos: a distância entre as mãos (cm), a folga entre elas, se uma entra na outra (mm) e se os antebraços
      se cruzam (mm);
    - o pulso de cada mão que segura a arma (graus, o pior nos quadros conferidos: a dobra, o desvio para o lado e a torção
      do antebraço) e a largura mínima da malha do pulso em relação à do repouso (%), no quadro de maior torção do clipe;
    - a menor distância dos braços (a pele: o braço, o antebraço e a mão) à câmera (cm), a do meio da palma ao olho (cm) e
      a fração da tela coberta pelos braços (%: os triângulos projetados na câmera dos braços), na nova e na de hoje;
    - quanto a arma andou em relação à de hoje (cm: o vértice da arma nova mais longe da arma de hoje no mesmo quadro)."""
    from mathutils.bvhtree import BVHTree
    from mathutils.kdtree import KDTree
    import duas_maos as dm
    saida = {}
    for arma in (armas or ARMAS):
        hoje = os.path.join(pasta, "fbx_hoje", f"Jogador_FP_{arma}.fbx")
        antes, antes_bracos = {}, {}
        if os.path.exists(hoje):
            rig, bracos, acoes = _bracos_com_a_mao_nova(hoje)
            idx, _ = _pontos_da_arma(bracos)
            pecas = _pecas_do_braco(rig, bracos)
            for nome, a in acoes.items():
                ag.usar_acao(rig, a)
                for f in _quadros_a_conferir(arma, nome, a):
                    bpy.context.scene.frame_set(f)
                    antes[(nome, f)] = _pontos_da_arma_mundo(bracos, idx)
                    tris = _triangulos_do_braco(pecas)
                    antes_bracos[(nome, f)] = (_perto_da_camera(tris), _cobertura_da_tela(tris))
        rig, bracos, acoes = _bracos_com_a_mao_nova(fbx_fp(arma))
        esq = ag.Esqueleto(rig)
        idx, slot = _pontos_da_arma(bracos)
        pos = {v: k for k, v in enumerate(idx)}
        pecas_o = {o.name[-1]: o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Mao_FP")}
        pecas = _pecas_do_braco(rig, bracos)
        anel = {lado: _anel_do_pulso(o, lado) for lado, o in pecas_o.items()}
        largura0 = {lado: _largura_do_pulso(rig, o, lado, anel[lado], False) for lado, o in pecas_o.items()}
        lados_pega = ("R", "L") if arma in DUAS_MAOS else ("R",)
        res = {}
        for nome, a in sorted(acoes.items()):
            ag.usar_acao(rig, a)
            qs = _quadros_a_conferir(arma, nome, a)
            linha = {"quadros": qs, "dentro_mm": 0.0, "vao_palma_mm": {}, "andou_cm": 0.0, "pulso": {},
                     "pulso_largura_pct": {}, "bracos_camera_cm": 99.0, "maos_olho_cm": 99.0, "tela_coberta_pct": 0.0}
            hq = [antes_bracos[(nome, f)] for f in qs if (nome, f) in antes_bracos]
            if hq:
                linha["hoje_bracos_camera_cm"] = round(min(h[0] for h in hq) * 100, 1)
                linha["hoje_tela_coberta_pct"] = round(max(h[1] for h in hq) * 100, 1)
            if arma in DUAS_MAOS:
                linha.update({"entre_maos_cm": [], "folga_maos_mm": 1e9, "uma_na_outra_mm": 0.0,
                              "antebracos_cruzam_mm": 0.0})
            pior_tor = {}
            for f in qs:
                bpy.context.scene.frame_set(f)
                bpy.context.view_layer.update()
                dg = bpy.context.evaluated_depsgraph_get()
                pa = _pontos_da_arma_mundo(bracos, idx)
                ev = bracos.evaluated_get(dg)
                me = ev.to_mesh()
                tri = [[pos[v] for v in p.vertices] for p in me.polygons if p.material_index == slot]
                ev.to_mesh_clear()
                bvh = BVHTree.FromPolygons(pa, tri)
                if (nome, f) in antes:
                    kd = KDTree(len(antes[(nome, f)]))
                    for i_k, p in enumerate(antes[(nome, f)]):
                        kd.insert(p, i_k)
                    kd.balance()
                    linha["andou_cm"] = max(linha["andou_cm"], max(kd.find(p)[2] for p in pa) * 100)
                # os braços e a câmera
                tris = _triangulos_do_braco(pecas)
                linha["bracos_camera_cm"] = min(linha["bracos_camera_cm"], _perto_da_camera(tris) * 100)
                linha["tela_coberta_pct"] = max(linha["tela_coberta_pct"], _cobertura_da_tela(tris) * 100)
                M = {pb.name: rig.matrix_world @ pb.matrix for pb in rig.pose.bones}
                for lado in lados_pega:
                    linha["maos_olho_cm"] = min(linha["maos_olho_cm"], dm.meio_da_mao(M, lado).length * 100)
                    d, dv, t = dm.pulso(esq, M, lado)
                    p = linha["pulso"].setdefault(lado, {"dobra": 0.0, "desvio": 0.0, "torcao": 0.0})
                    p["dobra"] = max(p["dobra"], abs(d))
                    p["desvio"] = max(p["desvio"], abs(dv))
                    p["torcao"] = max(p["torcao"], abs(t))
                    if lado in pecas_o and largura0.get(lado) and abs(t) >= pior_tor.get(lado, (-1, 0, 0))[0]:
                        lp = _largura_do_pulso(rig, pecas_o[lado], lado, anel[lado], True)
                        pior_tor[lado] = (abs(t), f, lp / largura0[lado] * 100)
                mao_pts = {}
                for lado, o in pecas_o.items():
                    evm = o.evaluated_get(dg)
                    mm = evm.to_mesh()
                    ws = [o.matrix_world @ v.co for v in mm.vertices]
                    nomes = {g.index: g.name for g in o.vertex_groups}
                    Hi = (rig.matrix_world @ rig.pose.bones["hand." + lado].matrix).inverted()
                    vao = 1e9
                    for i, w in enumerate(ws):
                        dentro, dist = _dentro(bvh, w)
                        if dist is None:
                            continue
                        gs = o.data.vertices[i].groups
                        if dentro:
                            if dist * 1000 > linha["dentro_mm"]:
                                # (09/11/2026) onde a pele entra: o lado, o osso do vértice e o quadro
                                osso = nomes[max(gs, key=lambda g: g.weight).group] if len(gs) else "?"
                                linha["dentro_onde"] = f"{lado} {osso} quadro {f}"
                            linha["dentro_mm"] = max(linha["dentro_mm"], dist * 1000)
                        if lado in lados_pega and len(gs) and \
                                nomes[max(gs, key=lambda g: g.weight).group] == "hand." + lado:
                            h = Hi @ w
                            if h.z < -0.004 and 0.03 < h.y < 0.105:
                                vao = min(vao, 0.0 if dentro else dist)
                    if vao < 1e9:
                        linha["vao_palma_mm"][lado] = max(linha["vao_palma_mm"].get(lado, 0.0), vao * 1000)
                    mao = [i for i, v in enumerate(o.data.vertices) if len(v.groups) and not nomes[
                        max(v.groups, key=lambda g: g.weight).group].startswith(("forearm", "upper_arm"))]
                    mao_pts[lado] = (ws, [list(p.vertices) for p in mm.polygons], mao)
                    evm.to_mesh_clear()
                if arma in DUAS_MAOS and len(mao_pts) == 2:
                    linha["entre_maos_cm"].append(round((dm.meio_da_mao(M, "R") - dm.meio_da_mao(M, "L")).length * 100, 1))
                    bR = BVHTree.FromPolygons(mao_pts["R"][0], mao_pts["R"][1])
                    so_mao_R = set(mao_pts["R"][2])
                    so_mao_L = set(mao_pts["L"][2])
                    for i, w in enumerate(mao_pts["L"][0]):
                        loc, nor, idx_f, dist = bR.find_nearest(w, 0.05)
                        if loc is None:
                            continue
                        dentro = (w - loc).dot(nor) < 0 and dist < 0.015
                        da_mao = i in so_mao_L and any(v in so_mao_R for v in mao_pts["R"][1][idx_f])
                        if da_mao:
                            if dentro:
                                linha["uma_na_outra_mm"] = max(linha["uma_na_outra_mm"], dist * 1000)
                            else:
                                linha["folga_maos_mm"] = min(linha["folga_maos_mm"], dist * 1000)
                        elif dentro:
                            linha["antebracos_cruzam_mm"] = max(linha["antebracos_cruzam_mm"], dist * 1000)
            for lado, (t, f, pct) in pior_tor.items():
                linha["pulso_largura_pct"][lado] = {"quadro": f, "torcao": round(t), "largura_pct": round(pct, 1)}
            for lado in linha["pulso"]:
                linha["pulso"][lado] = {k: round(v) for k, v in linha["pulso"][lado].items()}
            for k in ("dentro_mm", "andou_cm", "uma_na_outra_mm", "folga_maos_mm", "antebracos_cruzam_mm",
                      "bracos_camera_cm", "maos_olho_cm", "tela_coberta_pct"):
                if k in linha:
                    linha[k] = round(linha[k], 1) if linha[k] < 1e8 else "> 50"
            linha["vao_palma_mm"] = {k: round(v, 1) for k, v in linha["vao_palma_mm"].items()}
            res[nome] = linha
        saida[arma] = res
        print(f"CONFERIR_FP {arma} " + json.dumps(res, ensure_ascii=False))
    caminho = os.path.join(pasta, "conferir_fp.json")
    tudo = {}
    if os.path.exists(caminho):
        try:
            with open(caminho, encoding="utf-8") as fh:
                tudo = json.load(fh)
        except (OSError, ValueError):
            tudo = {}
    tudo.update(saida)
    with open(caminho, "w", encoding="utf-8") as fh:
        json.dump(tudo, fh, ensure_ascii=False, indent=1)
    return saida


def fotos_detalhe(pasta):
    """A mão direita no cabo de cada arma branca, no repouso, de quatro lados (dedos, palma, polegar e de cima), para
    comparar com a referência do Pedro (o polegar dando a volta, a mão inclinada, o punho firme)."""
    import pegada as pg
    saidas = []
    for arma in ("Machado", "Lanca", "Faca", "Bastao", "Tocha"):
        cfg = cfg_de(arma)
        # (09/11/2026) a diagonal do cabo na palma escolhida na primeira pessoa (das DIAGONAIS da cfg, a melhor para o
        # pulso; duas_maos.diagonal_escolhida), e não mais a mesma em todas
        import duas_maos as dm
        diagonal_antes = pg.DIAGONAL
        pg.DIAGONAL = dm.diagonal_escolhida(cfg.MODELO, "R")
        ag.nova_cena()
        pts = pg.pontos_da_mao_nova()
        novos = ag.importar(mn.SAIDA_FP)
        rig = next(o for o in novos if o.type == "ARMATURE")
        for o in novos:
            if o.type == "MESH" and not o.name.endswith("R"):
                bpy.data.objects.remove(o, do_unlink=True)
        ag._textura_no_material("PrincipalBloco", ag.TEXTURA)
        esq = ag.Esqueleto(rig)
        pai = {b.name: b.parent.name for b in rig.data.bones if b.parent}
        if rig.animation_data:
            rig.animation_data.action = None
        y = getattr(cfg, "MAO_DIREITA", getattr(cfg, "PEGADA_Y", -cfg.NO_PUNHO.y))
        malha = _malha_da_arma(cfg)
        raio, meio = pg.cabo(malha, y)
        malha.free()
        G = pg.pegada(esq.rest, "R", raio, meio, y, 0.0)
        for n, q in pg.dedos_no_cabo(esq.rest, pai, "R", pts, raio + getattr(cfg, "DEDOS_RAIO_A_MAIS", 0.0)).items():
            pb = rig.pose.bones[n]
            pb.rotation_mode = "QUATERNION"
            pb.rotation_quaternion = q
        print(f"DETALHE {arma}: raio {raio * 100:.2f} cm, a diagonal {pg.DIAGONAL}°; os dedos (1ª/2ª falange, mm dentro): "
              f"{getattr(pg.dedos_no_cabo, 'ultimo', {})}; o polegar: {getattr(pg._polegar, 'ultimo', {}).get('giro')} a "
              f"{getattr(pg._polegar, 'ultimo', {}).get('longe_do_alvo')} m do alvo")
        pg.DIAGONAL = diagonal_antes
        ob = _arma_solta(cfg, "Arma_" + arma)
        Hm = rig.matrix_world @ esq.rest["hand.R"]
        ob.matrix_world = Hm @ G
        bpy.context.view_layer.update()
        ag._render_base(480, 360)
        c0 = Hm @ Vector((0.0, 0.075, -0.02))
        R3 = Hm.to_3x3()
        dedos, costas = (R3 @ Vector((0, 1, 0))).normalized(), (R3 @ Vector((0, 0, 1))).normalized()
        pol = (R3 @ Vector((-1.0, 0, 0))).normalized()
        for vista, d, cima in (("1dedos", dedos * 0.9 + costas * 0.2 - pol * 0.3, costas),
                               ("2palma", -costas * 0.8 + dedos * 0.5, dedos),
                               ("3polegar", pol * 0.9 + costas * 0.3 + dedos * 0.2, costas),
                               ("4cima", costas * 0.9 + dedos * 0.15 + pol * 0.15, dedos)):
            pos = c0 + d.normalized() * 0.28
            cam = ag._camera("Perto", pos, alvo=c0, lente=50)
            cam.rotation_euler = mn._olhar(pos, c0, cima).to_euler()
            c = os.path.join(pasta, f"Detalhe_{arma}_{vista}.png")
            ag._salvar(c)
            saidas.append(c)
    return saidas


# ================================================================================================ a pegada no corpo

# o giro da mão direita em volta do cabo (graus): o mesmo que o Armas/pegada.py escolheu na primeira pessoa (a menor volta
# da mão de antes); a esquerda gira para o pulso dela ficar do mesmo lado do cabo que o da direita
GIROS = {"Faca": 20, "Machado": 54, "Bastao": 68, "Lanca": 198, "Tocha": 136}
# o osso "arma": os eixos da arma com o Y para a cabeça (o -Y da arma) e o Z para o alto dela (o X fica o -X da arma)
EIXOS_DO_OSSO = Matrix(((-1, 0, 0, 0), (0, -1, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1))) if bpy else None
PERNAS = ("thigh.L", "shin.L", "foot.L", "toe.L", "thigh.R", "shin.R", "foot.R", "toe.R")


def _malha_da_arma(cfg):
    import faca
    return faca.malha(cfg)


def pegada_no_corpo(arma, esq, pai, pts):
    """A pegada da arma (a mesma da primeira pessoa): {lado: {"G": a arma no espaço do hand, "dedos": os giros, "y": o
    lugar no cabo, "ponto": o ponto do cabo na pegada (no espaço da arma)}}."""
    import pegada as pg
    cfg = cfg_de(arma)
    bm = _malha_da_arma(cfg)
    out = {}
    yR = -cfg.NO_PUNHO.y
    rR, mR = pg.cabo(bm, yR)
    GR = pg.pegada(esq.rest, "R", rR, mR, yR, GIROS[arma])
    out["R"] = {"G": GR, "dedos": pg.dedos_no_cabo(esq.rest, pai, "R", pts, rR), "y": yR, "raio": rR,
                "ponto": Vector((mR[0], yR, mR[1]))}
    if hasattr(cfg, "SEGUNDA_MAO"):
        yL = -cfg.SEGUNDA_MAO
        rL, mL = pg.cabo(bm, yL)
        wR = GR.inverted() @ Vector((0.0, 0.0, 0.0))
        alvo = Vector((wR.x - mR[0], 0.0, wR.z - mR[1])).normalized()
        melhor = None
        for phi in range(0, 360, 2):
            GL = pg.pegada(esq.rest, "L", rL, mL, yL, phi)
            wL = GL.inverted() @ Vector((0.0, 0.0, 0.0))
            v = Vector((wL.x - mL[0], 0.0, wL.z - mL[1])).normalized()
            if melhor is None or v.dot(alvo) > melhor[0]:
                melhor = (v.dot(alvo), phi, GL)
        out["L"] = {"G": melhor[2], "dedos": pg.dedos_no_cabo(esq.rest, pai, "L", pts, rL), "y": yL, "raio": rL,
                    "phi": melhor[1], "ponto": Vector((mL[0], yL, mL[1]))}
    bm.free()
    return out


# ================================================================================================ as chaves do corpo
# (o mundo do Blender: o personagem olha para -Y, +X é a esquerda dele, Z para cima, os pés na origem; os ombros (a
# cabeça dos upper_arm) ficam em (±0,215, 0,055, 1,415))
# Cada chave: G, a pegada da mão direita (o ponto do cabo dela) no mundo; h, para onde a cabeça da arma aponta; z, para
# onde o alto dela fica; wL, a mão esquerda no cabo (1) ou solta (0: na guarda ou largada, Lp/Ld/Lpa, o lugar, os dedos e a
# palma); o tronco (coluna, peito, pescoco, cabeca: inclinação, giro (> 0 para a esquerda) e lado, em graus) e os ombros.

TRONCO0 = {"coluna": (3.0, 0.0, 0.0), "peito": (2.0, 0.0, 0.0), "pescoco": (0.0, 0.0, 0.0), "cabeca": (-3.0, 0.0, 0.0),
           "ombro.L": (0.0, 0.0), "ombro.R": (0.0, 0.0)}
SOLTA_L = {"Lp": (0.24, -0.03, 0.87), "Ld": (0.04, -0.06, -1.0), "Lpa": (-1.0, 0.15, 0.0), "polo.L": (0.3, 1.0, -0.2),
           "punhoL": 0.0}
GUARDA_L = {"Lp": (0.10, -0.27, 1.20), "Ld": (-0.25, -0.45, 0.85), "Lpa": (-0.85, 0.0, -0.4), "polo.L": (0.6, 0.2, -0.8),
            "punhoL": 1.0}


def _soma(a, b):
    return tuple(x + y for x, y in zip(a, b))


BASE = {
    "Faca": dict(TRONCO0, G=(-0.17, -0.31, 1.08), h=(0.06, -1.0, 0.60), z=(0.0, 0.6, 1.0), wL=0.0,
                 **{"polo.R": (-0.6, 0.5, -0.6)}, **GUARDA_L),
    # (09/11/2026, a pedido: "deveria ter seguido a sua recomendação") o machado pronto para o golpe, nas duas mãos (a
    # esquerda no fim do cabo, embaixo da direita), na frente do ombro direito, a cabeça para trás e para cima, por fora
    # do ombro, com o fio para a frente (antes: apoiado no ombro só com a direita)
    "Machado": dict(TRONCO0, G=(-0.16, -0.24, 1.18), h=(-0.50, 0.60, 0.62), z=(0.38, 0.80, -0.47), wL=1.0,
                    **{"polo.R": (-0.8, 0.2, -0.6), "coluna": (2.0, -8.0, 0.0), "peito": (2.0, -10.0, 0.0),
                       "pescoco": (0.0, 6.0, 0.0), "cabeca": (-3.0, 8.0, 0.0)},
                    **dict(SOLTA_L, **{"polo.L": (0.5, 0.0, -0.9)})),
    "Bastao": dict(TRONCO0, G=(-0.13, -0.20, 1.30), h=(-0.35, 0.55, 0.76), z=(1.0, 0.0, 0.0), wL=1.0,
                   **{"polo.R": (-0.7, 0.3, -0.6), "coluna": (2.0, -8.0, 0.0), "peito": (2.0, -10.0, 0.0)},
                   **dict(SOLTA_L, **{"polo.L": (0.5, 0.0, -0.9)})),
    # (09/11/2026, a pedido: "a ponta dela não condiz com a mira", "também ocorre em 3ª pessoa") a lança deitada na
    # altura do peito, apontando reto para a frente (para onde ele olha; só 7 graus para o meio), a mão direita do lado
    # do peito e a esquerda à frente, o fim do cabo por fora do corpo; o tronco virado para a direita e a cabeça de volta
    # para a frente (antes: na cintura, cruzando o corpo, a ponta para baixo e para a esquerda)
    "Lanca": dict(TRONCO0, G=(-0.22, -0.10, 1.16), h=(0.12, -1.0, 0.0), z=(0.0, 0.0, 1.0), wL=1.0,
                  **{"polo.R": (-0.9, 0.6, -0.3), "coluna": (2.0, -15.0, 0.0), "peito": (2.0, -12.0, 0.0),
                     "pescoco": (0.0, 12.0, 0.0), "cabeca": (-3.0, 13.0, 0.0)},
                  **dict(SOLTA_L, **{"polo.L": (0.6, 0.0, -0.8)})),
    "Tocha": dict(TRONCO0, G=(-0.20, -0.33, 1.36), h=(0.05, -0.30, 1.0), z=(0.0, 1.0, 0.3), wL=0.0,
                  **{"polo.R": (-0.7, 0.3, -0.7)}, **SOLTA_L),
}
# o golpe: (quadros a 30 por segundo, o acerto (s), [(t de 0 a 1, o que muda na chave)])
GOLPE = {
    "Faca": (15, 0.125, [
        (0.10, {"G": _soma(BASE["Faca"]["G"], (0.0, 0.05, -0.01))}),
        (0.25, {"G": (-0.11, -0.50, 1.24), "h": (0.05, -1.0, 0.15), "z": (0.0, 0.15, 1.0), "coluna": (6.0, 10.0, 0.0),
                "peito": (6.0, 12.0, 0.0)}),
        (0.45, {"G": (-0.13, -0.44, 1.20), "h": (0.05, -1.0, 0.25)}),
        (1.00, {}),
    ]),
    # (09/11/2026, a pedido: "se fosse só para frente seria um pouco melhor", "vamos bater com a lâmina") da pose pronta,
    # o machado sobe atrás da cabeça e desce de cima para baixo, reto para a frente, num plano em pé na frente do lado
    # direito do corpo (o x das mãos e da cabeça quase não muda), com as duas mãos e o fio na frente; o acerto (0,292 s)
    # com o cabo deitado para a frente na altura do peito; depois volta à pose pronta (antes: na diagonal, de cima à direita
    # para baixo à esquerda, e a esquerda largava o cabo no fim)
    "Machado": (27, 7 / 24, [
        (0.13, {"G": (-0.12, -0.10, 1.45), "h": (-0.10, 0.75, 0.65), "z": (0.11, 0.66, -0.74),
                "coluna": (-4.0, -10.0, 0.0), "peito": (-5.0, -12.0, 0.0), "ombro.R": (8.0, 0.0)}),
        (0.23, {"G": (-0.10, -0.34, 1.40), "h": (-0.02, -0.50, 0.87), "z": (0.0, 0.87, 0.5),
                "coluna": (3.0, -4.0, 0.0), "peito": (4.0, -4.0, 0.0), "ombro.R": (5.0, 3.0)}),
        (0.324, {"G": (-0.08, -0.48, 1.20), "h": (0.02, -1.0, 0.0), "z": (0.0, 0.0, 1.0),
                 "coluna": (10.0, 3.0, 0.0), "peito": (12.0, 5.0, 0.0), "ombro.R": (2.0, 6.0),
                 "pescoco": (4.0, 0.0, 0.0), "cabeca": (0.0, 0.0, 0.0)}),
        (0.45, {"G": (-0.07, -0.42, 1.02), "h": (0.02, -0.77, -0.64), "z": (0.0, -0.64, 0.77),
                "coluna": (14.0, 4.0, 0.0), "peito": (14.0, 5.0, 0.0)}),
        (0.62, {"G": (-0.13, -0.30, 1.16), "h": (-0.15, -0.50, 0.85), "z": (0.0, 0.85, 0.5),
                "coluna": (6.0, -4.0, 0.0), "peito": (6.0, -6.0, 0.0)}),
        (0.80, {"G": (-0.15, -0.26, 1.20), "h": (-0.32, 0.30, 0.90), "z": (0.1, 0.94, -0.33),
                "coluna": (3.0, -7.0, 0.0), "peito": (3.0, -9.0, 0.0), "pescoco": (0.0, 6.0, 0.0),
                "cabeca": (-3.0, 8.0, 0.0)}),
        (1.00, {}),
    ]),
    "Bastao": (24, 7 / 24, [
        (0.12, {"G": (-0.18, -0.08, 1.33), "h": (-0.55, 0.62, 0.55), "coluna": (0.0, -15.0, 0.0),
                "peito": (0.0, -20.0, 0.0)}),
        (0.365, {"G": (-0.04, -0.44, 1.22), "h": (0.75, -0.65, 0.10), "z": (0.0, 0.0, 1.0), "coluna": (4.0, 12.0, 0.0),
                 "peito": (5.0, 15.0, 0.0)}),
        (0.55, {"G": (0.12, -0.32, 1.20), "h": (0.82, 0.50, 0.18), "z": (0.0, 0.0, 1.0), "coluna": (4.0, 25.0, 0.0),
                "peito": (5.0, 30.0, 0.0)}),
        (1.00, {}),
    ]),
    # (09/11/2026) a estocada na linha da lança: recua, empurra para a frente ao longo dela (o tronco inclina e vira para
    # a frente) e volta
    "Lanca": (25, 6 / 24, [
        (0.12, {"G": _soma(BASE["Lanca"]["G"], (-0.008, 0.07, 0.0))}),
        (0.30, {"G": _soma(BASE["Lanca"]["G"], (0.030, -0.25, 0.0)), "coluna": (9.0, -20.0, 0.0),
                "peito": (9.0, -16.0, 0.0), "pescoco": (-4.0, 16.0, 0.0), "cabeca": (-6.0, 18.0, 0.0)}),
        (0.50, {"G": _soma(BASE["Lanca"]["G"], (0.020, -0.17, 0.0)), "coluna": (6.0, -18.0, 0.0),
                "peito": (6.0, -14.0, 0.0)}),
        (1.00, {}),
    ]),
    "Tocha": (15, 0.125, [
        (0.10, {"G": (-0.18, -0.22, 1.50), "h": (0.0, 0.40, 1.0), "z": (0.0, -1.0, 0.4)}),
        (0.25, {"G": (-0.12, -0.46, 1.30), "h": (0.05, -1.0, -0.35), "z": (0.0, -0.35, 1.0), "peito": (6.0, 6.0, 0.0)}),
        (0.45, {"G": (-0.11, -0.40, 1.16), "h": (0.05, -0.75, -0.65), "z": (0.0, -0.65, 0.75)}),
        (1.00, {}),
    ]),
}
QUADROS_SEGURAR = 60
RESPIRA = 0.006


def _eixos_da_arma(h, z):
    """Os eixos da arma no mundo: o -Y (a cabeça) para h, o +Z (o alto) o mais perto de z, o X = Y x Z."""
    y = -Vector(h).normalized()
    zz = Vector(z)
    zz = (zz - zz.dot(y) * y).normalized()
    return Matrix((y.cross(zz), y, zz)).transposed()


class Trilha:
    """As chaves (cada uma completa: a base com as mudanças das chaves até ela) misturadas por curvas (a _Curva do
    agua_animacoes.py), canal a canal; o giro da arma vira um quatérnio (no mesmo hemisfério de chave em chave)."""

    def __init__(self, base, mudancas, laco):
        estado = dict(base)
        chaves = [(0.0, dict(estado))]
        for t, d in mudancas:
            estado = dict(base) if (t >= 1.0 and not d) else dict(estado, **d)
            chaves.append((t, dict(estado)))
        qs, ant = [], None
        for _, d in chaves:
            q = _eixos_da_arma(d["h"], d["z"]).to_quaternion()
            if ant is not None and ant.dot(q) < 0:
                q = -q
            qs.append(tuple(q))
            ant = q
        self.curvas = {}
        for k in chaves[0][1]:
            if k in ("h", "z"):
                continue
            self.curvas[k] = ag._Curva([(t, d[k]) for t, d in chaves], laco)
        self.curvas["q"] = ag._Curva([(t, q) for (t, _), q in zip(chaves, qs)], laco)

    def __call__(self, t):
        d = {k: c(t) for k, c in self.curvas.items()}
        d["q"] = Quaternion(tuple(d["q"])).normalized()
        return d


def _respirar(arma):
    base = BASE[arma]

    def f(t):
        a = 2 * math.pi * t
        d = {k: (Vector(v) if isinstance(v, tuple) and len(v) == 3 else v) for k, v in base.items() if k not in ("h", "z")}
        d["G"] = Vector(base["G"]) + Vector((0.0, 0.0, RESPIRA * math.sin(a)))
        d["peito"] = Vector(base["peito"]) + Vector((0.8 * math.sin(a), 0.0, 0.0))
        d["q"] = _eixos_da_arma(base["h"], base["z"]).to_quaternion()
        return d
    return f


def pose_com_arma(esq, peg, dedos, d):
    """As matrizes do corpo numa chave: a arma no mundo (pela pegada da mão direita), as mãos nela (ou a esquerda solta),
    o tronco, os dedos e o osso "arma"; as pernas no repouso."""
    R = d["q"].to_matrix().to_4x4()
    pR = peg["R"]["ponto"]
    W = Matrix.Translation(Vector(d["G"])) @ R @ Matrix.Translation(-pR)
    HR = W @ peg["R"]["G"].inverted()
    wL = max(0.0, min(1.0, d["wL"]))
    rot_solta = ag.rot_da_mao(Vector(d["Ld"]), Vector(d["Lpa"]))
    HL = rot_solta.to_4x4()
    HL.translation = Vector(d["Lp"])
    dedos_L = ag.Dedos.misturar(dedos.aberta("L", 0.35), {k: dedos.punho[k].copy() for k in dedos.aberta("L")},
                                max(0.0, min(1.0, d.get("punhoL", 0.0))))
    HL_solta = HL.copy()
    if "L" in peg and wL > 0:
        dedos_L = ag.Dedos.misturar(dedos_L, peg["L"]["dedos"], wL)
    p = {"quadril": esq.rest["hips"].translation.copy(), "quadril.rot": (0, 0, 0)}
    for k in ("coluna", "peito", "pescoco", "cabeca", "ombro.L", "ombro.R"):
        p[k] = tuple(d[k])
    # ao alcance: com o tronco na pose (os ombros onde ficam), a arma chega para perto do ombro que não alcança a mão dele
    # (e a mão esquerda solta também)
    ombros = _ombros(esq, p, dedos)
    maximo = {l: 0.97 * (esq.comprimento("upper_arm." + l, "forearm." + l) + esq.comprimento("forearm." + l, "hand." + l))
              for l in "LR"}
    for _ in range(3):
        desl = Vector((0.0, 0.0, 0.0))
        v = HR.translation - ombros["R"]
        if v.length > maximo["R"]:
            desl -= v.normalized() * (v.length - maximo["R"])
        if "L" in peg and wL > 0.5:
            v = (W @ peg["L"]["G"].inverted()).translation - ombros["L"]
            if v.length > maximo["L"]:
                desl -= v.normalized() * (v.length - maximo["L"])
        if desl.length < 1e-5:
            break
        W = Matrix.Translation(desl) @ W
        HR = W @ peg["R"]["G"].inverted()
    if "L" in peg and wL > 0:
        HLc = W @ peg["L"]["G"].inverted()
        HL = HL_solta.to_quaternion().slerp(HLc.to_quaternion(), wL).to_matrix().to_4x4()
        HL.translation = HL_solta.translation.lerp(HLc.translation, wL)
    v = HL.translation - ombros["L"]
    if v.length > maximo["L"]:
        HL.translation = ombros["L"] + v.normalized() * maximo["L"]
    p["mao.R"], p["giro_mao.R"], p["polo.R"] = HR.translation, HR.to_3x3(), Vector(d["polo.R"])
    p["mao.L"], p["giro_mao.L"], p["polo.L"] = HL.translation, HL.to_3x3(), Vector(d["polo.L"])
    p["dedos_q.R"] = peg["R"]["dedos"]
    p["dedos_q.L"] = dedos_L
    for lado in "LR":
        r = esq.rest["foot." + lado]
        p["pe." + lado], p["pe_y." + lado], p["pe_z." + lado] = r.translation, r.col[1].xyz, r.col[2].xyz
        p["joelho." + lado] = (0, -1, 0)
    M = ag.pose_corpo(esq, p, dedos)
    for n in PERNAS:
        M.pop(n, None)
    M.pop("arma", None)
    esq.completar(M, {})
    M["arma"] = M["hand.R"] @ peg["R"]["G"] @ EIXOS_DO_OSSO
    pose_com_arma.alcance = dict(ag.pose_corpo.alcance)
    pose_com_arma.W = W
    return M


pose_com_arma.alcance = {}


def _ombros(esq, p, dedos):
    """Onde ficam as cabeças dos upper_arm com o tronco e os ombros da chave (o resto qualquer)."""
    q = dict(p)
    for lado in "LR":
        r = esq.rest["hand." + lado]
        q["mao." + lado], q["giro_mao." + lado], q["polo." + lado] = r.translation, r.to_3x3(), Vector((0, 1, 0))
        rp = esq.rest["foot." + lado]
        q["pe." + lado], q["pe_y." + lado], q["pe_z." + lado] = rp.translation, rp.col[1].xyz, rp.col[2].xyz
        q["joelho." + lado] = (0, -1, 0)
    M = ag.pose_corpo(esq, q, dedos)
    return {l: M["upper_arm." + l].translation.copy() for l in "LR"}


def preparar_rig():
    """O PrincipalBloco_Rig (sem as ações) com o osso "arma" (filho do hand.R; no repouso, a faca na mão do repouso)."""
    rig, esq = ag.preparar_corpo()
    for o in list(bpy.data.objects):
        if o is not rig:
            bpy.data.objects.remove(o, do_unlink=True)
    return rig, esq


def criar_osso_da_arma(rig, M):
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.scene.objects:
        o.select_set(o == rig)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones.new("arma")
    eb.parent = rig.data.edit_bones["hand.R"]
    eb.head = M.translation
    eb.tail = M.translation + M.to_3x3().col[1] * 0.10
    eb.align_roll(M.to_3x3().col[2])
    eb.use_deform = False
    bpy.ops.object.mode_set(mode="OBJECT")


def etapa_corpo():
    """Os clipes do corpo de todas as armas brancas, no Personagem_PrincipalBloco_Brancas.fbx."""
    import pegada as pg
    dedos = ag.dedos_do_fp()
    ag.nova_cena()
    pts = pg.pontos_da_mao_nova()
    rig, esq = preparar_rig()
    pai = {b.name: b.parent.name for b in rig.data.bones if b.parent}
    pegs = {a: pegada_no_corpo(a, esq, pai, pts) for a in ARMAS}
    criar_osso_da_arma(rig, esq.rest["hand.R"] @ pegs["Faca"]["R"]["G"] @ EIXOS_DO_OSSO)
    esq = ag.Esqueleto(rig)
    acoes = []
    for arma in ARMAS:
        peg = pegs[arma]
        f_seg = _respirar(arma)
        acoes.append((f"{arma}_Segurar", ag.gravar(esq, f"{arma}_Segurar", QUADROS_SEGURAR,
                                                    lambda t, f=f_seg, p=peg: pose_com_arma(esq, p, dedos, f(t)), True)))
        quadros, acerto, mud = GOLPE[arma]
        tr = Trilha(BASE[arma], mud, False)
        acoes.append((f"{arma}_Golpe", ag.gravar(esq, f"{arma}_Golpe", quadros,
                                                  lambda t, f=tr, p=peg: pose_com_arma(esq, p, dedos, f(t)), False)))
        # o alcance dos braços (1 = esticado)
        mx = {}
        for i in range(quadros + 1):
            pose_com_arma(esq, peg, dedos, tr(i / quadros))
            for k, v in pose_com_arma.alcance.items():
                mx[k] = max(mx.get(k, 0.0), round(v, 3))
        print(f"brancas: {arma}: o golpe em {quadros} quadros ({quadros / FPS:.3f} s), o acerto em {acerto:.3f} s "
              f"(quadro {acerto * FPS:.2f}); o alcance {mx}; a pegada: " +
              ", ".join(f"{k} y {v['y']:.3f} raio {v['raio'] * 100:.2f} cm" for k, v in peg.items()))
    rep = ag.acao_de_repouso(esq)
    ag.faixas(rig, acoes)
    ag.exportar(SAIDA, [rig], rig, {"ARMATURE"}, rig, rep)


# ================================================================================================ conferir (o corpo)

def _unity(v):
    return [round(-v.x, 5), round(v.y, 5), round(v.z, 5)]


def _giro_unity(xu, yu, zu):
    R = Matrix((Vector(xu), Vector(yu), Vector(zu))).transposed()
    q = R.to_quaternion()
    e = R.to_euler("ZXY")
    return {"localRotation_xyzw": [round(q.x, 5), round(q.y, 5), round(q.z, 5), round(q.w, 5)],
            "localEulerAngles": [round(math.degrees(a), 3) for a in e]}


def _item_unity_em(H):
    """O Itens_<Arma>.fbx (na Unity: a frente, a cabeça, em +Z; o alto em +Y; o X é o -X da arma) num espaço da Unity,
    dado H: os eixos da arma para o espaço do Blender desse osso. Devolve a posição e o giro locais na Unity."""
    R = H.to_3x3()
    d = {"localPosition": _unity(H.translation)}
    d.update(_giro_unity(_unity(R @ Vector((-1, 0, 0))), _unity(R @ Vector((0, 0, 1))), _unity(R @ Vector((0, -1, 0)))))
    return d


def conferir():
    """O FBX novo importado de volta: o esqueleto (os ossos de antes iguais aos do Personagem_PrincipalBloco.fbx), os
    clipes (os quadros), o osso "arma" parado na mão em cada clipe, e o lugar do Itens_<Arma> na mão direita e no osso
    "arma" (para a Unity)."""
    rel = {}
    ag.nova_cena()
    ag.importar(ag.FBX_CORPO)
    ro = bpy.data.objects[RIG]
    ro.name = "Original"
    for o in list(bpy.data.objects):
        if o is not ro:
            bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    novos = ag.importar(SAIDA)
    rn = next(o for o in novos if o.type == "ARMATURE")
    rel["ossos"] = (len(ro.data.bones), len(rn.data.bones))
    rel["repouso_max_dif"] = max((ag._dif(b.matrix_local, rn.data.bones[b.name].matrix_local), b.name)
                                 for b in ro.data.bones)
    arma_rest = rn.data.bones["arma"]
    rel["osso_arma"] = {"pai": arma_rest.parent.name, "comprimento": round(arma_rest.length, 4)}
    clipes = {}
    for a in sorted(bpy.data.actions, key=lambda a: a.name):
        if not a.name.startswith(RIG + "|"):
            continue
        nome = a.name.split("|", 1)[1]
        ag.usar_acao(rn, a)
        f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
        rels = []
        for f in range(f0, f1 + 1):
            bpy.context.scene.frame_set(f)
            rels.append(rn.pose.bones["hand.R"].matrix.inverted() @ rn.pose.bones["arma"].matrix)
        var = max(ag._dif(r, rels[0]) for r in rels)
        H = rels[0] @ EIXOS_DO_OSSO.inverted()
        c = {"quadros": f1 - f0, "segundos": round((f1 - f0) / FPS, 3), "variacao_do_osso_arma_na_mao": var}
        if nome.endswith("_Segurar"):
            c["Itens_na_hand.R_unity"] = _item_unity_em(H)
            c["Itens_no_osso_arma_unity"] = _item_unity_em(EIXOS_DO_OSSO.inverted())
            c["osso_arma_na_hand.R_unity"] = {"localPosition": _unity(rels[0].translation),
                                              **_giro_unity(_unity(rels[0].to_3x3() @ Vector((-1, 0, 0))),
                                                            _unity(rels[0].to_3x3().col[1]),
                                                            _unity(rels[0].to_3x3().col[2]))}
        clipes[nome] = c
    rel["clipes"] = clipes
    return rel


# ================================================================================================ as fotos do corpo

def _arma_solta(cfg, nome):
    """A malha da arma (no espaço dela), com a paleta dos itens (para as fotos)."""
    import faca
    bm = faca.malha(cfg)
    me = bpy.data.meshes.new(nome)
    bm.to_mesh(me)
    bm.free()
    mat = bpy.data.materials.get("Pistola_Fotos") or bpy.data.materials.new("Pistola_Fotos")
    mat.use_nodes = True
    ag._textura_em(mat, PALETA)
    me.materials.append(mat)
    uv = me.uv_layers.new(name="UVMap")
    nomes = list(cfg.CORES)
    for p in me.polygons:
        u, v = faca.celula(cfg, nomes[p.material_index])
        p.material_index = 0
        for li in p.loop_indices:
            uv.data[li].uv = (u, v)
        p.use_smooth = True
    ob = bpy.data.objects.new(nome, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _no_punho_de_hoje(arma):
    """A arma no espaço da mão direita como hoje (o "miolo" do punho do Idle dos braços, do faca.py de antes): para a
    foto de hoje no corpo (aproximada: no jogo o corpo usa o encaixe da lata com o giro da MontagemDaRede)."""
    import faca
    cfg = cfg_de(arma)
    ag.nova_cena()
    ag.importar(ag.FBX_MAOS)
    rig = bpy.data.objects["FP_Rig_Maos"]
    ag.usar_acao(rig, bpy.data.actions["FP_Rig_Maos|Idle"])
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    P = faca._no_punho(rig, cfg)
    return (rig.matrix_world @ rig.pose.bones["hand.R"].matrix).inverted() @ P


def fotos_corpo(pasta, arma):
    """O corpo com a arma, hoje (a guarda e o soco direito dos punhos, a arma no punho) e o novo (o Segurar e o golpe): de
    frente e de lado, no Segurar (o quadro 1) e no golpe (o começo, o acerto e o fim)."""
    cfg = cfg_de(arma)
    no_punho = _no_punho_de_hoje(arma)
    rig, velha, nova, _ = mn._cena_com_maos(ag.FBX_CORPO, RIG, "Jogador_Corpo", mn.SAIDA_CORPO)
    ag._textura_no_material("PrincipalBloco", ag.TEXTURA)
    rig.name = "Original"
    novos = ag.importar(SAIDA)
    rn = next(o for o in novos if o.type == "ARMATURE")
    acao = {n: bpy.data.actions[RIG + "|" + n] for n in (f"{arma}_Segurar", f"{arma}_Golpe")}
    hoje = {"Segurar": bpy.data.actions[RIG + "|Maos_Guarda"], "Golpe": bpy.data.actions[RIG + "|Maos_SocoDireito"]}
    ob = _arma_solta(cfg, "Arma_" + arma)
    quadros, acerto, _ = GOLPE[arma]
    momentos = [("1Segurar", "Segurar", 0.0), ("2Golpe_comeco", "Golpe", 0.0),
                ("3Golpe_acerto", "Golpe", acerto / (quadros / FPS)), ("4Golpe_fim", "Golpe", 0.55)]
    ag._render_base(480, 480)
    saidas = []
    for nome, clipe, t in momentos:
        for qual in ("hoje", "nova"):
            a = hoje[clipe] if qual == "hoje" else acao[f"{arma}_{clipe}"]
            ag.usar_acao(rig, a)
            ag.usar_acao(rn, a)
            f0, f1 = a.frame_range
            bpy.context.scene.frame_set(int(round(f0 + t * (f1 - f0))))
            bpy.context.view_layer.update()
            if qual == "hoje":
                ob.matrix_world = rig.matrix_world @ rig.pose.bones["hand.R"].matrix @ no_punho
            else:
                ob.matrix_world = rn.matrix_world @ rn.pose.bones["arma"].matrix @ EIXOS_DO_OSSO.inverted()
            velha.hide_render = qual != "hoje"
            nova.hide_render = qual != "nova"
            for vista, pos, alvo in (("frente", (0.25, -2.6, 1.30), (-0.05, -0.15, 1.15)),
                                     ("lado", (-2.6, -0.35, 1.30), (-0.05, -0.20, 1.15))):
                ag._camera("Foto", pos, alvo=alvo, lente=45)
                c = os.path.join(pasta, f"BrancasCorpo_{arma}_{nome}_{vista}_{qual}.png")
                ag._salvar(c)
                saidas.append(c)
    return saidas


# ================================================================================================ main

def etapa_fotos(pasta, quais=("FP", "Corpo")):
    os.makedirs(pasta, exist_ok=True)
    armas = [a for a in ARMAS if a in quais] or list(ARMAS)
    for arma in armas:
        if "FP" in quais:
            fotos_fp(pasta, arma)
        if "Corpo" in quais and "fotos_corpo" in globals():
            fotos_corpo(pasta, arma)


def folhas(pasta):
    import re
    vistas = {}
    for f in sorted(os.listdir(pasta)):
        m = re.match(r"(BrancasFP|BrancasCorpo)_(\w+?)_(\w+?)_(hoje|nova|ref)\.png$", f)
        if m:
            vistas.setdefault((m.group(1), m.group(2)), {}).setdefault(m.group(3), {})[m.group(4)] = f
    saidas = []
    for (grupo, arma), vs in sorted(vistas.items()):
        titulo = (f"{arma} na primeira pessoa (com a mão nova): o Idle e o golpe pela câmera dos braços, e as mãos de perto "
                  "- hoje e a nova") if grupo == "BrancasFP" else (f"{arma} no corpo: o Segurar e o golpe, de frente e de "
                                                                    "lado - hoje e a nova")
        lista = sorted(vs.items())
        perto = [(n, a) for n, a in lista if "Perto" in n]
        longe = [(n, a) for n, a in lista if "Perto" not in n]
        for nome, parte in (("", longe), ("_Perto", perto)):
            if parte:
                # a referência do Pedro (BrancasFP_<Arma>_1Idle01_ref.png), quando houver, numa coluna ao lado do Idle
                quais = ("hoje", "nova", "ref") if any("ref" in a for _, a in parte) else ("hoje", "nova")
                parte = sorted(parte, key=lambda v: "ref" not in v[1])     # (a primeira vista dá o tamanho: a com a ref)
                saidas.append(mn._folha(pasta, f"{grupo}_{arma}{nome}", titulo, parte, quais, por_linha=2,
                                        escala=0.75 if grupo == "BrancasFP" and not nome else 1.0))
    # a pegada de perto (fotos_detalhe), com a referência do Pedro (Detalhe_ref.png) na última coluna
    det = {}
    for f in sorted(os.listdir(pasta)):
        m = re.match(r"Detalhe_(Machado|Lanca|Faca|Bastao|Tocha)_(\w+)\.png$", f)
        if m:
            det.setdefault(m.group(1), {})[m.group(2)] = f
    if det:
        cols = ("1dedos", "2palma", "3polegar", "4cima")
        linhas = []
        for arma, vs in sorted(det.items()):
            if os.path.exists(os.path.join(pasta, "Detalhe_ref.png")):
                vs = dict(vs, ref="Detalhe_ref.png")
            linhas.append((f"{arma}: a mão direita no cabo", vs))
        saidas.append(mn._folha(pasta, "Pegada_Detalhe", "A pegada nova de perto: a mão direita no cabo (dos dedos, da "
                                "palma, do polegar e de cima) e a referência", linhas, cols + ("ref",), por_linha=1))
    print("\n".join(c for c in saidas if c))
    return saidas


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    etapa = args[0] if args else "tudo"
    pasta = args[1] if len(args) > 1 else None
    if etapa in ("corpo", "tudo") and "etapa_corpo" in globals():
        globals()["etapa_corpo"]()
    if etapa in ("conferir", "tudo") and "conferir" in globals():
        print("CONFERIR " + json.dumps(globals()["conferir"](), indent=1, default=str, ensure_ascii=False))
    if etapa in ("fotos", "tudo") and pasta:
        etapa_fotos(pasta)
    if etapa.startswith("fotos:") and pasta:
        etapa_fotos(pasta, etapa.split(":", 1)[1].split(","))
    if etapa.startswith("conferir_fp") and pasta:
        conferir_fp(pasta, etapa.split(":", 1)[1].split(",") if ":" in etapa else None)
    if etapa == "detalhe" and pasta:
        fotos_detalhe(pasta)


if __name__ == "__main__":
    if bpy is None:
        if len(sys.argv) > 2 and sys.argv[1] == "folhas":
            sys.path.insert(0, AQUI)
            import maos_novas as mn  # noqa: F811
            folhas(sys.argv[2])
        else:
            print("uso: py brancas_corpo.py folhas <pasta das fotos>")
    else:
        main()
