# O Brutamonte novo (03/11/2026, a pedido: "tem um outro modelo de zumbi brutamonte que eu quero colocar no lugar do atual,
# mas primeiro vamos montar a cena de comparação com os dois lado a lado; o novo precisa ter esqueleto e animações que já
# existem"). O modelo é um Tripo da pasta de arte (Monstros\MonstroGigante\MonstroGigante.fbx: uma malha só, 5.126 faces, a
# textura de cor e mais nada, 1 m de altura, em pose A, olhando para -Y), com a massa de bolhas no lugar da cabeça e o olho
# nas costas, moldado na própria malha (no atual, o olho e as pálpebras são peças à parte).
#
# Aqui ele ganha o mesmo que os especiais do jogo (as funções do especiais.py):
#  - a altura do Brutamonte de hoje (ALTURA, a do Brutamonte_V1.fbx), os pés no chão e o meio em x = 0;
#  - o esqueleto do Oco encaixado pelas medidas da malha (Medidas e juntas). O "queixo" é o fim de baixo da massa de bolhas na
#    frente (QUEIXO, a fração da altura: não há uma peça da cabeça para medir);
#  - os ossos do olho e das pálpebras (os nomes do atual, no meio do olho, filhos do chest): o globo (achado pela cor da
#    textura, o âmbar, nas costas) fica preso inteiro ao osso do olho; as pálpebras ficam sem malha (o modelo não tem);
#  - os pesos pela casca de voxels, um material só, até 4 ossos por vértice;
#  - as três texturas do material do Oco: a cor (a do Tripo), a normal lisa e o metal/liso (fosco).
# Sai Assets/_Game/Art/Characters/Zombie/Especiais/Brutamonte_Novo.fbx e Brutamonte_Novo_BaseColor/_Normal/_MetalLiso.png.
# Fica fora da lista do jogo: só a cena de teste dos especiais (Zombie City Cleaner > Exemplos > Zumbis especiais) o usa.
#
# Rodar (Blender em segundo plano): blender -b --factory-startup --python brutamonte_novo.py

import colorsys
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import especiais as E  # noqa: E402

NOME = "Brutamonte_Novo"
FONTE = r"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner\Monstros\MonstroGigante\MonstroGigante.fbx"
COR = os.path.join(os.path.splitext(FONTE)[0] + ".fbm", "MonstroGigante_basecolor.jpg")
ALTURA = 2.184          # a do Brutamonte_V1 (o especiais.py não muda a escala dos modelos dele)
QUEIXO = 0.735          # o fim de baixo da massa de bolhas na frente, na fração da altura (medido nas fotos)
OLHO_Z = (0.66, 0.90)   # onde procurar o olho (a fração da altura), nas costas
LISO = 0.45             # a lisura do mapa (o material multiplica por 0,35)


def log(*a):
    print("[brutamonte novo]", *a, flush=True)


def importar_na_altura():
    E.limpar_cena()
    novos = E.importar(FONTE)
    malhas = [o for o in novos if o.type == "MESH"]
    for o in list(novos):
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    ob = malhas[0]
    if len(malhas) > 1:
        for o in bpy.context.selected_objects:
            o.select_set(False)
        for o in malhas:
            o.select_set(True)
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.join()
        ob = bpy.context.view_layer.objects.active
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    V = np.array([tuple(v.co) for v in ob.data.vertices])
    lo, hi = V.min(0), V.max(0)
    s = ALTURA / (hi[2] - lo[2])
    meio = np.array([(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]])
    for v in ob.data.vertices:
        v.co = Vector(((np.array(v.co) - meio) * s).tolist())
    ob.data.update()
    ob.name = NOME + "_Jogo"
    log(f"importado: {len(ob.data.vertices)} vértices, {len(ob.data.polygons)} faces, escala {s:.3f}, {ALTURA} m")
    return ob


def olho_pela_cor(ob):
    """Os vértices do globo do olho: os da cor âmbar da textura, nas costas, na altura do olho; o grupo maior."""
    img = bpy.data.images.load(COR, check_existing=True)
    w, h = img.size
    px = np.array(img.pixels[:]).reshape(h, w, 4)
    uv = ob.data.uv_layers.active.data
    eh = np.zeros(len(ob.data.vertices), bool)
    for p in ob.data.polygons:
        for li, vi in zip(p.loop_indices, p.vertices):
            u, v = uv[li].uv
            r, g, b, _ = px[min(h - 1, max(0, int(v * h))), min(w - 1, max(0, int(u * w)))]
            hh, ss, vv = colorsys.rgb_to_hsv(r, g, b)
            if 0.07 <= hh <= 0.16 and ss >= 0.5 and vv >= 0.35:
                eh[vi] = True
    V = np.array([tuple(v.co) for v in ob.data.vertices])
    dentro = eh & (V[:, 1] > 0.05) & (V[:, 2] > OLHO_Z[0] * ALTURA) & (V[:, 2] < OLHO_Z[1] * ALTURA)
    idx = np.where(dentro)[0]
    if len(idx) == 0:
        return [], None
    # o grupo maior, ligado pelas arestas
    vizinhos = {i: set() for i in idx}
    conj = set(idx.tolist())
    for e in ob.data.edges:
        a, b = e.vertices
        if a in conj and b in conj:
            vizinhos[a].add(b)
            vizinhos[b].add(a)
    vistos, maior = set(), []
    for i in idx:
        if i in vistos:
            continue
        pilha, grupo = [i], []
        vistos.add(i)
        while pilha:
            k = pilha.pop()
            grupo.append(k)
            for n in vizinhos[k]:
                if n not in vistos:
                    vistos.add(n)
                    pilha.append(n)
        if len(grupo) > len(maior):
            maior = grupo
    c = V[maior].mean(0)
    return [int(i) for i in maior], Vector(c.tolist())


def texturas():
    os.makedirs(E.SAIDA, exist_ok=True)
    cor = bpy.data.images.load(COR, check_existing=True)
    cor.filepath_raw = os.path.join(E.SAIDA, NOME + "_BaseColor.png")
    cor.file_format = "PNG"
    cor.save()
    for nome, rgba in (("_Normal", (0.5, 0.5, 1.0, 1.0)), ("_MetalLiso", (0.0, 0.0, 0.0, LISO))):
        im = bpy.data.images.new(NOME + nome, 64, 64, alpha=True)
        im.colorspace_settings.name = "Non-Color"
        im.pixels[:] = list(rgba) * (64 * 64)
        im.filepath_raw = os.path.join(E.SAIDA, NOME + nome + ".png")
        im.file_format = "PNG"
        im.save()
    log("texturas gravadas")


def perna_seguindo(M, passo=0.01):
    """A linha da perna esquerda subindo do pé, cada fatia no grupo mais perto da de baixo. Neste modelo a mão desce até a
    altura do joelho, e o grupo com mais pontos (o do especiais.py) era a mão ali: o joelho ia parar nela."""
    P = M.P
    esp = passo * 0.6
    perna = []
    ultimo = None
    z = 0.03
    while z < M.virilha - 0.02:
        S = P[np.abs(P[:, 2] - z) < esp]
        S = S[S[:, 0] > 0.004]
        gs = E.grupos_em_x(S[:, 0], 0.018)
        if gs:
            if ultimo is None:
                g = max(gs, key=lambda g: len(g[2]))
            else:
                g = min(gs, key=lambda g: abs(S[g[2]][:, 0].mean() - ultimo))
            pts = S[g[2]]
            perna.append((z, pts[:, 0].mean(), pts[:, 1].mean(), pts[:, 1].min(), pts[:, 1].max()))
            ultimo = pts[:, 0].mean()
        z += passo
    M.perna = np.array(perna)


def principal():
    oco, rep, caudas = E.medir_oco()
    ob = importar_na_altura()
    M = E.Medidas(E.pontos_da_superficie([ob]), QUEIXO * ALTURA)
    perna_seguindo(M)
    log(f"medidas: virilha {M.virilha:.3f} axila {M.axila:.3f} pescoço {M.pescoco:.3f} ponta {M.ponta:.3f} queixo {M.queixo:.3f} topo {M.topo:.3f}")
    log("  a perna (z, x): " + ", ".join(f"({p[0]:.2f}, {p[1]:.2f})" for p in M.perna[::10]))
    J = E.juntas(oco, rep, caudas, M)
    globo, c = olho_pela_cor(ob)
    if c is None:
        raise RuntimeError("não achei o olho pela cor da textura")
    log(f"o olho: {len(globo)} vértices, o meio em ({c.x:.3f}, {c.y:.3f}, {c.z:.3f})")
    extras = [("olho", c, c + Vector((0, 0.12, 0)), "chest", (0, 0, 1)),
              ("palpebra_sup", c, c + Vector((0, 0.10, 0.06)), "chest", (0, 0, 1)),
              ("palpebra_inf", c, c + Vector((0, 0.10, -0.06)), "chest", (0, 0, 1))]
    arm = E.montar_armadura(NOME, J, rep, extras)
    mat = bpy.data.materials.new(NOME + "_Mat")
    ob.data.materials.clear()
    ob.data.materials.append(mat)
    for b in arm.data.bones:
        b.use_deform = b.name in E.ESQUELETO
    for tentativa in range(4):
        if E.pesos_pela_casca(ob, arm, 0.03 * (1.0, 1.2, 0.85, 1.45)[tentativa]):
            break
    E.corrigir_sem_peso(ob, arm)
    # o globo do olho: inteiro no osso do olho (o jogo o gira depois da animação)
    for g in ob.vertex_groups:
        g.remove(globo)
    ob.vertex_groups.new(name="olho").add(globo, 1.0, "REPLACE")
    for b in arm.data.bones:
        b.use_deform = b.name != "root"
    for m in list(ob.modifiers):
        if m.type == "ARMATURE":
            ob.modifiers.remove(m)
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    ob.parent = arm
    ob.matrix_parent_inverse = arm.matrix_world.inverted()
    E.limitar_pesos(ob)
    E.exportar(NOME, arm, ob)
    texturas()
    # para conferir: as juntas
    for n in ("hips", "chest", "neck", "head", "upper_arm.L", "forearm.L", "hand.L", "thigh.L", "shin.L", "foot.L"):
        h, t = J[n]
        log(f"  {n}: ({h.x:.3f}, {h.y:.3f}, {h.z:.3f})")


if __name__ == "__main__":
    principal()
