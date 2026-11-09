# O braço de cima novo (09/11/2026, a pedido do Pedro, com um recorte do cotovelo no jogo: "o braço está um pouco mais
# quadrado e o antebraço mais redondo, se tiver como deixar os dois mais iguais, só cuidado para não polir demais o braço
# e deixar muito pesado"): no estilo do antebraço novo do maos_novas.py (liso, arredondado e com poucos polígonos), do
# ombro até o anel onde o antebraço novo começa.
#
# O braço de cima de hoje (o NPC_Corpo do Roupas.fbx; o do Jogador_Corpo é o mesmo, a menos de 0,1 mm), de cada lado:
# 40 faces (80 triângulos) e 48 vértices, em 6 anéis de 8 (um octógono: uma caixa chanfrada de ~9,8 cm de frente a trás
# e ~13 cm de cima a baixo no meio, que afina para ~9,8 x 10,3 no fim). No espaço do upper_arm (y ao longo dele):
#   S0 (y -2,5 cm): a borda com o ombro e o tronco (8 vértices e 8 arestas, que ficam no corpo): 75% no chest (os 4 de
#       baixo) ou no shoulder (os 4 de cima) e 25% no upper_arm;
#   S1 (1,3 cm): o sovaco e o ombro, mais largo na frente (60% no upper_arm, 40% no chest ou no shoulder);
#   S2 (6,3 cm): 97% no upper_arm; S3 (12,3), S4 (19,5) e S5 (26,0 cm): 100%;
#   S5 é o anel do fim (2,7 cm antes do cotovelo), em cima do qual a peça nova do antebraço (Mao_Corpo) já começa: o
#       primeiro anel dela tem 20 vértices (os 8 cantos e 12 nas arestas entre eles).
# As 40 faces são as faces da pele (o material 0) com algum vértice com mais da metade do peso no upper_arm do lado (os
# números delas, no UV "Face": 71-73, 134-142, 146-150, 159-173 e 198-205 no esquerdo; 564-566, 627-635, 639-643,
# 652-666 e 691-698 no direito). A textura ali é a cor da pele (a mesma da célula 0, a do antebraço).
#
# A peça nova (BracoCima_Corpo.L e .R, no Maos_Novas_Corpo.fbx, ao lado da Mao_Corpo; gravada pelo maos_novas.py, na
# etapa "corpo"), em anéis:
# - o primeiro é a borda do ombro de hoje (os 8 vértices, no mesmo lugar, com os mesmos pesos e a mesma forma "Mulher");
# - os do meio (ANEIS: em S1, S2, S3, S4 e 3,9 cm antes do S5) têm LADOS_NO_MEIO (12) vértices na direção de uma
#   superelipse (expoente EXPOENTE, 3, com a largura e a altura do octógono de hoje naquele ponto, como o começo do
#   antebraço), mas cada um a no máximo SAIR (3 mm) para fora do braço de hoje e ENTRAR (4 mm) para dentro: o mesmo
#   comprimento e a mesma grossura (9,8 x 12,9 cm no meio; de frente, de lado e de cima o contorno é quase o de hoje), só
#   as quinas arredondadas e a pele lisa. Sem o limite, a superelipse passava 8 mm do chanfro da frente e furava a barra
#   da manga da camiseta (as mangas estão a ~7 mm do braço de hoje, as camisetas, e ~17 mm, as jaquetas). No S1 (o
#   sovaco e o ombro) ela não sai do braço de hoje;
# - o último é o primeiro anel da Mao_Corpo (os mesmos 20 vértices, no mesmo lugar, com a mesma forma "Mulher" e as
#   mesmas normais: a emenda do cotovelo não aparece);
# - os pesos e a forma "Mulher" de cada vértice vêm do braço de hoje no mesmo lugar (a mistura dos vértices de hoje em
#   volta): perto do ombro, os mesmos pesos de hoje (o ombro dobra igual); do S3 em diante, 100% no upper_arm;
# - entre anéis com o mesmo número de vértices, quadrados; entre o de 8 e o de 12 (no ombro) e entre o de 12 e o de 20
#   (perto do cotovelo), triângulos que costuram um no outro pelo lugar em volta;
# - a pele lisa (as normais suaves), como a do antebraço; sem pelos (o antebraço só tem pelos na metade de perto do
#   pulso: a de perto do cotovelo, e então o braço de cima, fica sem); a forma "SemPelos" vai igual à base (para a peça
#   ter as mesmas formas da Mao_Corpo);
# - o UV0 na célula da pele (a 0, a mesma do antebraço); o UV "Face": o número da face de hoje no mesmo lugar (o anel e
#   o lado do octógono do meio de cada face nova), para as mangas continuarem escondendo as faces certas.
# Por lado: 88 vértices, 100 faces e 148 triângulos (hoje: 48, 40 e 80), em anéis de 8, 12, 12, 12, 12, 12 e 20.
# Conferido (09/11/2026): as emendas no ombro e no cotovelo a 0,0 mm (o lugar e a "Mulher"; as normais do cotovelo iguais
# às da Mao_Corpo), a Mao_Corpo igual vértice a vértice à de antes; nas 15 peças com manga, no repouso (o Parado) e na
# guarda dos punhos (o Maos_Guarda), no homem e na mulher, a pele nova não fura nenhuma (a menor folga, nas camisetas:
# 3,7 mm) e o "esconde" de hoje não abre buraco: nenhuma lista precisa mudar. Na guarda, a frente do ombro (a faixa
# S0-S1, a dobra do sovaco) fica com uma prega escura lisa, onde hoje há uma dobra escura em facetas.
#
# Uma peça à parte (e não junto da Mao_Corpo): a mão e o antebraço ficam iguais vértice a vértice (aprovados), as
# luvas e as outras etapas do maos_novas.py continuam lendo só a Mao_Corpo, e a peça nova é fácil de tirar.
# O que a MontagemDaMaoNova (Assets/_Game/Scripts/Editor/MontagemDaMaoNova.cs) precisa mudar para usar a peça:
#   - PecasDaMao = { "Mao_Corpo.L", "Mao_Corpo.R", "BracoCima_Corpo.L", "BracoCima_Corpo.R" };
#   - OssosDoCorte ganha "upper_arm": o corte passa a tirar também as faces com algum vértice com mais da metade do peso
#     no upper_arm do lado (as 40 de cada lado, acima: 545 faces e 994 triângulos por lado no total). A borda aberta
#     passa a ser a do ombro (8 vértices de cada lado), onde a BracoCima começa; o anel do cotovelo fica entre as duas
#     peças novas (a Emenda() vai medir a borda do ombro).
#
# Como rodar (Blender 5.2, em segundo plano, um por vez):
#   blender --background --factory-startup --disable-autoexec --python Blender/maos_novas.py -- corpo
#       (grava o Maos_Novas_Corpo.fbx: a Mao_Corpo e a BracoCima_Corpo)
#   blender --background --factory-startup --disable-autoexec --python Blender/braco_de_cima.py -- <etapa> [pasta] [fbx]
#       medir: o braço de cima de hoje (os anéis, os pesos, as faces e a borda);
#       conferir: a peça nova (os vértices, os triângulos, os lados de cada anel, as emendas no ombro e no cotovelo, as
#           bordas abertas), a Mao_Corpo igual à de um FBX de antes (o [fbx], vértice a vértice) e as mangas (no
#           repouso e na guarda dos punhos, no homem e na mulher: se a pele fura alguma e se o "esconde" abre buraco,
#           hoje e no novo; grava o braco_cima_roupas.json na pasta, e o braco_cima_esconde_corrigido.json se alguma
#           lista precisar mudar);
#       fotos: o braço de hoje (com a mão e o antebraço novos) e o novo, com a mesma câmera, na pasta (BracoCima_*.png);
#   py Blender/braco_de_cima.py folha <pasta>   (a Folha_BracoCima.png, com o Pillow)

import json
import math
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
if AQUI not in sys.path:
    sys.path.insert(0, AQUI)
import agua_animacoes as ag  # noqa: E402
import maos_novas as mn  # noqa: E402

try:
    import bpy
    import bmesh
    from mathutils import Matrix, Vector
    from mathutils.bvhtree import BVHTree
except ImportError:          # fora do Blender: só a folha das fotos
    bpy = None

NOME = "BracoCima_Corpo"
LADOS_NO_MEIO = 12                     # os vértices dos anéis do meio (a seção)
# os anéis do meio: o lugar (nos anéis de hoje: 1 = S1, ...; 4,6 = 3,9 cm antes do S5) e o quanto a seção pode sair do
# braço de hoje (m). A seção é uma superelipse com a largura e a altura do octógono de hoje (como no antebraço), mas
# cada ponto fica perto do braço de hoje: no máximo SAIR para fora (as mangas estão a ~7 mm dele, a camiseta, e ~17 mm,
# a jaqueta) e ENTRAR para dentro (nas quinas: sem afinar). No S1 (o sovaco e o ombro) não sai.
SAIR = 0.003
ENTRAR = 0.004
ANEIS = ((1.0, 0.0), (2.0, SAIR), (3.0, SAIR), (4.0, SAIR), (4.6, SAIR))
EXPOENTE = 3.0                         # a superelipse da seção (2 = elipse; o antebraço vai de 3,2 a 2,4)
POSES = ("Parado", "Maos_Guarda")      # o repouso (de pé) e o braço dobrado (a guarda dos punhos), do Personagem_PrincipalBloco
ROUPAS_DAS_FOTOS = ("Camiseta", "Jaqueta_Couro")
FOLGA = 0.0005                         # a pele a menos disto da roupa (por dentro) já conta como furando (m)


# ================================================================================================ contas


def _sg(c, e):
    return math.copysign(abs(c) ** e, c)


def _mistura(a, b, t):
    """Os pesos (ou qualquer {nome: valor}) de a a b, somando 1, sem os pequenos."""
    m = {}
    for k in set(a) | set(b):
        w = (1 - t) * a.get(k, 0.0) + t * b.get(k, 0.0)
        if w > 0.002:
            m[k] = w
    s = sum(m.values())
    return {k: w / s for k, w in m.items()}


def _dist_circ(a, b, n=8):
    return abs(((a - b + n / 2) % n) - n / 2)


def faces_de_cima(o, lado, material=0):
    """As faces do braço de cima de hoje de um lado (as da pele com algum vértice com mais da metade do peso no
    upper_arm, fora as do corte do antebraço)."""
    return mn.faces_do_braco_de_cima(o, lado, material)


# ================================================================================================ o braço de hoje


def ler_braco_de_hoje():
    """No Roupas.fbx já carregado na cena: de cada lado, os 6 anéis do braço de cima de hoje (do S0, a borda do ombro, ao
    S5, o fim), cada um com os 8 vértices na mesma ordem em volta (a face entre os anéis k e k+1 liga o canto j de um ao
    j do outro): o lugar (no espaço da armadura), o da forma "Mulher" e os pesos; o número de cada face (k, j); e, na
    borda do ombro, as normais das faces do ombro (para ver se são lisas)."""
    npc = bpy.data.objects["NPC_Corpo"]
    rig = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    me = npc.data
    pesos = mn._pesos(npc)
    kb = me.shape_keys.key_blocks
    uvf = me.uv_layers["Face"].data
    A = rig.matrix_world.inverted() @ npc.matrix_world
    me.update()
    cn = me.corner_normals
    dados = {}
    for lado in mn.LADOS:
        ua = "upper_arm." + lado
        B = rig.data.bones[ua].matrix_local.copy()
        Bi = B.inverted()
        fs = [me.polygons[i] for i in faces_de_cima(npc, lado)]
        vs = sorted({v for f in fs for v in f.vertices}, key=lambda v: (Bi @ (A @ me.vertices[v].co)).y)
        if len(fs) != 40 or len(vs) != 48:
            raise RuntimeError(f"o braço de cima de hoje ({lado}) mudou: {len(fs)} faces, {len(vs)} vértices")
        aneis = []
        for k in range(6):
            g = vs[8 * k: 8 * k + 8]
            loc = {v: Bi @ (A @ me.vertices[v].co) for v in g}
            cx, cz = sum(p.x for p in loc.values()) / 8, sum(p.z for p in loc.values()) / 8
            g.sort(key=lambda v: math.atan2(loc[v].z - cz, loc[v].x - cx))
            aneis.append(g)
        # o anel k+1 girado para a face entre k e k+1 ligar o canto j ao j
        for k in range(5):
            pos = {v: j for j, v in enumerate(aneis[k])}
            pos1 = {v: j for j, v in enumerate(aneis[k + 1])}
            desl = set()
            for f in fs:
                for a, b in f.edge_keys:
                    if a in pos and b in pos1:
                        desl.add((pos1[b] - pos[a]) % 8)
                    elif b in pos and a in pos1:
                        desl.add((pos1[a] - pos[b]) % 8)
            if len(desl) != 1:
                raise RuntimeError(f"os anéis {k} e {k + 1} do braço de cima ({lado}) não se ligam canto a canto: {desl}")
            o = desl.pop()
            aneis[k + 1] = [aneis[k + 1][(j + o) % 8] for j in range(8)]
        onde = {v: (k, j) for k, g in enumerate(aneis) for j, v in enumerate(g)}
        numeros = {}
        for f in fs:
            ks = sorted({onde[v] for v in f.vertices})
            k = min(kk for kk, _ in ks)
            js = sorted({j for kk, j in ks if kk == k})
            j = js[0] if (js[1] - js[0]) == 1 else js[1]       # o par (7, 0) começa no 7
            numeros[(k, j)] = int(math.floor(uvf[f.loop_indices[0]].uv.x + 1e-3))
        if len(numeros) != 40:
            raise RuntimeError("as faces do braço de cima de hoje não são 5 faixas de 8")
        # as normais das faces do ombro (fora do braço) na borda
        dentro = {f.index for f in fs}
        normais = {}
        for f in me.polygons:
            if f.index in dentro:
                continue
            for li in f.loop_indices:
                v = me.loops[li].vertex_index
                if v in onde and onde[v][0] == 0:
                    normais.setdefault(onde[v][1], []).append((A.to_3x3() @ cn[li].vector).normalized())
        dados[lado] = {
            "B": B,
            "aneis": [[{"i": v, "co": A @ me.vertices[v].co, "mu": A @ kb["Mulher"].data[v].co,
                        "pesos": dict(pesos[v])} for v in g] for g in aneis],
            "numeros": numeros,
            "normais_do_ombro": normais,
        }
    return dados


# ================================================================================================ a construção


def _anel_do_meio(dl, u, n, sair=SAIR):
    """Um anel do meio, no lugar u (nos anéis de hoje): os n pontos (lugar, "Mulher", pesos, lugar em volta: j + w no
    octógono de hoje). Cada ponto vai na direção da superelipse, no raio dela, mas a no máximo sair (m) para fora do
    octógono de hoje e ENTRAR para dentro; a "Mulher" e os pesos vêm do ponto do octógono naquela direção (a "Mulher"
    afastada do meio na mesma proporção)."""
    B = dl["B"]
    Bi = B.inverted()
    k = min(int(u), 4)
    t = u - k
    a0, a1 = dl["aneis"][k], dl["aneis"][k + 1]
    Q = [a0[j]["co"].lerp(a1[j]["co"], t) for j in range(8)]
    Qm = [a0[j]["mu"].lerp(a1[j]["mu"], t) for j in range(8)]
    W = [_mistura(a0[j]["pesos"], a1[j]["pesos"], t) for j in range(8)]
    q = [Bi @ p for p in Q]
    xs, zs = [p.x for p in q], [p.z for p in q]
    cx, cz = (max(xs) + min(xs)) / 2, (max(zs) + min(zs)) / 2
    a, b = (max(xs) - min(xs)) / 2, (max(zs) - min(zs)) / 2
    C = B @ Vector((cx, sum(p.y for p in q) / 8, cz))
    Cm = C + (sum(Qm, Vector()) - sum(Q, Vector())) / 8
    pts = []
    for i in range(n):
        phi = 2 * math.pi * i / n
        ex, ez = a * _sg(math.cos(phi), 2 / EXPOENTE), b * _sg(math.sin(phi), 2 / EXPOENTE)
        re = math.hypot(ex, ez)
        dx, dz = ex / re, ez / re
        melhor = None
        for j in range(8):
            p1x, p1z = q[j].x - cx, q[j].z - cz
            p2x, p2z = q[(j + 1) % 8].x - cx, q[(j + 1) % 8].z - cz
            exx, exz = p2x - p1x, p2z - p1z
            den = dx * exz - dz * exx
            if abs(den) < 1e-12:
                continue
            s = (p1x * exz - p1z * exx) / den
            w = (p1x * dz - p1z * dx) / den
            if s > 0 and -1e-9 <= w <= 1 + 1e-9 and (melhor is None or s < melhor[0]):
                melhor = (s, j, min(1.0, max(0.0, w)))
        r_oct, j, w = melhor
        f = (r_oct + max(-ENTRAR, min(sair, re - r_oct))) / r_oct
        O = Q[j].lerp(Q[(j + 1) % 8], w)
        Om = Qm[j].lerp(Qm[(j + 1) % 8], w)
        pts.append((C + (O - C) * f, Cm + (Om - Cm) * f, _mistura(W[j], W[(j + 1) % 8], w), j + w))
    return pts


def _no_octogono(p, anel):
    """O lugar em volta (j + w) de um ponto em cima do octógono de um anel de hoje (a aresta mais perto)."""
    melhor = None
    for j in range(8):
        a, b = anel[j]["co"], anel[(j + 1) % 8]["co"]
        e = b - a
        w = max(0.0, min(1.0, (p - a).dot(e) / e.length_squared))
        d = (a + e * w - p).length
        if melhor is None or d < melhor[0]:
            melhor = (d, j + w)
    return melhor


def _quadrados(A, B):
    """As faces entre dois anéis com o mesmo número de vértices ([(índice, lugar em volta)], na ordem): o B começa no
    vértice mais perto do primeiro do A."""
    n = len(A)
    o = min(range(n), key=lambda i: _dist_circ(B[i][1], A[0][1]))
    return [[A[i][0], A[(i + 1) % n][0], B[(i + 1 + o) % n][0], B[(i + o) % n][0]] for i in range(n)]


def _costura(A, B):
    """Os triângulos entre dois anéis com números diferentes de vértices: anda em volta dos dois juntos, sempre no que
    tem o próximo vértice antes."""
    n, m = len(A), len(B)
    p0 = A[0][1]
    ib = min(range(m), key=lambda i: _dist_circ(B[i][1], p0))
    B = B[ib:] + B[:ib]

    def desenrolar(ps):
        out = []
        for p in ps:
            q = p0 + ((p - p0 + 4) % 8) - 4 if not out else p
            while out and q < out[-1]:
                q += 8
            out.append(q)
        return out
    UA = desenrolar([p for _, p in A])
    UB = desenrolar([p for _, p in B])
    UA.append(UA[0] + 8)
    UB.append(UB[0] + 8)
    AA = [i for i, _ in A] + [A[0][0]]
    BB = [i for i, _ in B] + [B[0][0]]
    i = j = 0
    tris = []
    while i < n or j < m:
        if j == m or (i < n and UA[i + 1] <= UB[j + 1]):
            tris.append([AA[i], AA[i + 1], BB[j]])
            i += 1
        else:
            tris.append([AA[i], BB[j + 1], BB[j]])
            j += 1
    return tris


def construir(dl, mao, lado):
    """A peça de um lado: (lugares, "Mulher", pesos, faces, números das faces, normais fixas {vértice: normal}, anéis).
    mao: o primeiro anel da Mao_Corpo do lado ([(lugar, "Mulher", normal)])."""
    co, mu, pesos, onde = [], [], [], []
    aneis = []

    def novo(c, m, p, u, ang):
        co.append(c.copy())
        mu.append(m.copy())
        pesos.append(p)
        onde.append((u, ang))
        return len(co) - 1
    # a borda do ombro (a de hoje)
    aneis.append([(novo(v["co"], v["mu"], dict(v["pesos"]), 0.0, float(j)), float(j))
                  for j, v in enumerate(dl["aneis"][0])])
    # os do meio
    for u, sair in ANEIS:
        aneis.append([(novo(c, m, p, u, ang), ang) for c, m, p, ang in _anel_do_meio(dl, u, LADOS_NO_MEIO, sair)])
    # o do cotovelo (o primeiro da Mao_Corpo)
    fixas = {}
    ult = []
    for c, m, nrm in mao:
        d, ang = _no_octogono(c, dl["aneis"][5])
        if d > 0.0005:
            raise RuntimeError(f"o anel da Mao_Corpo.{lado} não está em cima do fim do braço de cima ({d * 1000:.2f} mm)")
        i = novo(c, m, {"upper_arm." + lado: 1.0}, 5.0, ang)
        fixas[i] = nrm
        ult.append((i, ang))
    ult.sort(key=lambda x: x[1])
    aneis.append(ult)
    # as faces
    faces = []
    for a, b in zip(aneis, aneis[1:]):
        faces += _quadrados(a, b) if len(a) == len(b) else _costura(a, b)
    # para fora (longe do eixo do osso)
    B = dl["B"]
    h, d = B.translation, B.to_3x3().col[1].normalized()
    for k, f in enumerate(faces):
        pts = [co[i] for i in f]
        c = sum(pts, Vector()) / len(pts)
        dentro = h + d * (c - h).dot(d)
        if mn._newell(pts).dot(c - dentro) < 0:
            faces[k] = list(reversed(f))
    # o número de cada face: o da face de hoje no meio dela
    numeros = []
    for f in faces:
        uc = sum(onde[i][0] for i in f) / len(f)
        a0 = onde[f[0]][1]
        ac = sum(a0 + ((onde[i][1] - a0 + 4) % 8) - 4 for i in f) / len(f)
        k = max(0, min(4, int(uc)))
        numeros.append(dl["numeros"][(k, int(math.floor(ac)) % 8)])
    return co, mu, pesos, faces, numeros, fixas, [[i for i, _ in a] for a in aneis]


def _objeto(nome, rig, material, co, mu, pesos, faces, numeros, fixas, origem):
    me = bpy.data.meshes.new(nome)
    me.from_pydata([tuple(c) for c in co], [], faces)
    me.update()
    me.materials.append(material)
    ob = bpy.data.objects.new(nome, me)
    bpy.context.scene.collection.objects.link(ob)
    grupos = {}
    for i, p in enumerate(pesos):
        for osso, w in p.items():
            g = grupos.get(osso)
            if g is None:
                g = grupos[osso] = ob.vertex_groups.new(name=osso)
            g.add([i], w, "REPLACE")
    # o UV0: a célula da pele, com a face projetada pequena em volta (como na Mao_Corpo)
    uv = me.uv_layers.new(name="UVMap")
    cu, cv = mn.UV_DO_TIPO["pele"]
    lim = mn.CELULA / 2 - 0.004
    for f in me.polygons:
        f.use_smooth = True
        pts = [me.vertices[k].co for k in f.vertices]
        n = mn._newell(pts)
        eixo = max(range(3), key=lambda k: abs(n[k]))
        a, b = [k for k in range(3) if k != eixo]
        for li, p in zip(f.loop_indices, pts):
            uv.data[li].uv = (cu + max(-lim, min(lim, 0.25 * (p[a] - origem[a]))),
                              cv + max(-lim, min(lim, 0.25 * (p[b] - origem[b]))))
    uvf = me.uv_layers.new(name="Face")
    for f, num in zip(me.polygons, numeros):
        for li in f.loop_indices:
            uvf.data[li].uv = (num + 0.5, -1.0)
    # as formas
    ob.shape_key_add(name="Basis", from_mix=False)
    km = ob.shape_key_add(name="Mulher", from_mix=False)
    for i, c in enumerate(mu):
        km.data[i].co = c
    ob.shape_key_add(name="SemPelos", from_mix=False)
    # as normais: lisas, e no anel do cotovelo as da Mao_Corpo
    me.update()
    ns = [v.normal.copy() for v in me.vertices]
    for i, n in fixas.items():
        ns[i] = n
    me.normals_split_custom_set_from_vertices([tuple(n) for n in ns])
    ob.parent = rig
    ob.matrix_parent_inverse = Matrix.Identity(4)
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    return ob


def _primeiro_anel_da_mao(ob, lado):
    """O primeiro anel da Mao_Corpo (os vértices com o peso todo no upper_arm): [(lugar, "Mulher", normal)] no espaço
    da armadura (a peça fica no lugar do rig)."""
    me = ob.data
    me.update()
    nomes = {g.index: g.name for g in ob.vertex_groups}
    kb = me.shape_keys.key_blocks if me.shape_keys else None
    cn = me.corner_normals
    nrm = {}
    for li, lp in enumerate(me.loops):
        nrm.setdefault(lp.vertex_index, cn[li].vector.copy())
    M = ob.matrix_world
    out = []
    for v in me.vertices:
        if any(nomes[g.group] == "upper_arm." + lado and g.weight > 0.999 for g in v.groups):
            m = kb["Mulher"].data[v.index].co if kb and "Mulher" in kb else v.co
            out.append((M @ v.co, M @ m, (M.to_3x3() @ nrm[v.index]).normalized()))
    if len(out) != 20:
        raise RuntimeError(f"o primeiro anel da {ob.name} tem {len(out)} vértices (eram 20)")
    return out


def pecas(rig, material, maos, hoje):
    """As duas peças (a BracoCima_Corpo.L e a .R), presas ao rig (o do Personagem_PrincipalBloco.fbx, o mesmo repouso
    do Roupas.fbx), a partir do braço de hoje (ler_braco_de_hoje) e do primeiro anel das Mao_Corpo já feitas."""
    obs = []
    for lado in mn.LADOS:
        mao = [o for o in maos if o.name == f"Mao_Corpo.{lado}"][0]
        co, mu, pesos, faces, numeros, fixas, aneis = construir(hoje[lado], _primeiro_anel_da_mao(mao, lado), lado)
        origem = rig.data.bones["upper_arm." + lado].head_local
        ob = _objeto(f"{NOME}.{lado}", rig, material, co, mu, pesos, faces, numeros, fixas, origem)
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        print(f"braco_de_cima: {ob.name}: {len(ob.data.vertices)} vértices, {tris} triângulos, anéis de "
              f"{[len(a) for a in aneis]}")
        obs.append(ob)
    return obs


# ================================================================================================ medir


def medir():
    ag.nova_cena()
    ag.importar(mn.FBX_ROUPAS)
    hoje = ler_braco_de_hoje()
    rel = {}
    for lado, dl in hoje.items():
        Bi = dl["B"].inverted()
        an = []
        for k, g in enumerate(dl["aneis"]):
            q = [Bi @ v["co"] for v in g]
            ps = {}
            for v in g:
                for o, w in v["pesos"].items():
                    ps[o] = ps.get(o, 0) + w / 8
            an.append({"anel": f"S{k}", "y_cm": round(sum(p.y for p in q) / 8 * 100, 1),
                       "frente_tras_cm": round((max(p.x for p in q) - min(p.x for p in q)) * 100, 1),
                       "cima_baixo_cm": round((max(p.z for p in q) - min(p.z for p in q)) * 100, 1),
                       "pesos_medios": {o: round(w, 3) for o, w in sorted(ps.items())}})
        ns = dl["normais_do_ombro"]
        espalho = max(max(math.degrees(a.angle(b)) for a in lst for b in lst) for lst in ns.values())
        rel[lado] = {"aneis": an, "faces": len(dl["numeros"]), "triangulos": 2 * len(dl["numeros"]),
                     "numeros": mn._faixas_texto(sorted(dl["numeros"].values())),
                     "normais_do_ombro_na_borda_max_graus_entre_faces": round(espalho, 1)}
    return rel


# ================================================================================================ conferir


def _poses():
    """As poses (os quadros 1 do Parado e do Maos_Guarda do Personagem_PrincipalBloco.fbx), para pôr em outro rig do
    mesmo esqueleto."""
    ag.nova_cena()
    ag.importar(ag.FBX_CORPO)
    rig = bpy.data.objects[ag.RIG_CORPO]
    poses = {}
    for nome in POSES:
        vals = mn._pose_estatica(rig, bpy.data.actions[ag.RIG_CORPO + "|" + nome])
        poses[nome] = {k: (l.copy(), q.copy(), s.copy()) for k, (l, q, s) in vals.items()}
    return poses


def _por_pose(rig, vals):
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        if vals and pb.name in vals:
            pb.location, pb.rotation_quaternion, pb.scale = vals[pb.name]
        else:
            pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)
    bpy.context.view_layer.update()


def _mulher(obs, valor):
    for o in obs:
        ks = o.data.shape_keys
        if ks and "Mulher" in ks.key_blocks:
            ks.key_blocks["Mulher"].value = valor
    bpy.context.view_layer.update()


def _comparar_mao(antes, agora):
    """A Mao_Corpo de agora e a de antes, vértice a vértice: os lugares, os pesos, as faces, os UVs, as formas e as
    normais."""
    a, b = antes.data, agora.data
    r = {"vertices": [len(a.vertices), len(b.vertices)], "faces": [len(a.polygons), len(b.polygons)]}
    if len(a.vertices) != len(b.vertices) or len(a.polygons) != len(b.polygons):
        r["igual"] = False
        return r
    r["lugar_max_mm"] = round(max((va.co - vb.co).length for va, vb in zip(a.vertices, b.vertices)) * 1000, 4)
    r["faces_iguais"] = all(tuple(fa.vertices) == tuple(fb.vertices) for fa, fb in zip(a.polygons, b.polygons))
    pa, pb = mn._pesos(antes), mn._pesos(agora)
    r["pesos_max_dif"] = round(max(max(abs(pa[i].get(g, 0) - pb[i].get(g, 0)) for g in set(pa[i]) | set(pb[i]))
                                   for i in pa), 6)
    for uv in ("UVMap", "Face"):
        r[f"uv_{uv}_max_dif"] = round(max((x.uv - y.uv).length for x, y in zip(a.uv_layers[uv].data,
                                                                                 b.uv_layers[uv].data)), 6)
    fa = [k.name for k in a.shape_keys.key_blocks]
    r["formas"] = fa == [k.name for k in b.shape_keys.key_blocks] and fa
    r["formas_max_mm"] = round(max(max((x.co - y.co).length for x, y in zip(a.shape_keys.key_blocks[n].data,
                                                                             b.shape_keys.key_blocks[n].data))
                                   for n in fa) * 1000, 4)
    a.update()
    b.update()
    r["normais_max_graus"] = round(max(math.degrees(x.vector.angle(y.vector, 0.0)) for x, y in
                                       zip(a.corner_normals, b.corner_normals)), 4)
    r["igual"] = (r["lugar_max_mm"] < 0.01 and r["faces_iguais"] and r["pesos_max_dif"] < 1e-4
                  and r["formas_max_mm"] < 0.01 and r["normais_max_graus"] < 0.05)
    return r


def _borda(ob):
    """As arestas abertas (de uma face só) de uma malha."""
    uso = {}
    for f in ob.data.polygons:
        for e in f.edge_keys:
            uso[e] = uso.get(e, 0) + 1
    return [e for e, n in uso.items() if n == 1]


def _pele_do_braco(ob, faces, lado):
    """[(vértices da face, número)] e o osso dono de cada vértice, das faces dadas."""
    uvf = ob.data.uv_layers["Face"].data
    nomes = {g.index: g.name for g in ob.vertex_groups}
    dono = {}
    for v in ob.data.vertices:
        gs = [(g.weight, nomes[g.group]) for g in v.groups]
        if gs:
            dono[v.index] = max(gs)[1]
    fs = [(tuple(ob.data.polygons[i].vertices), int(math.floor(uvf[ob.data.polygons[i].loop_indices[0]].uv.x + 1e-3)))
          for i in faces]
    return fs, dono


def _coords_e_normais(ob, dg):
    """Os vértices (no mundo) da malha avaliada (a pose e as formas) e as normais deles."""
    ev = ob.evaluated_get(dg)
    me = ev.to_mesh()
    M = ob.matrix_world
    R = M.to_3x3()
    cs = [M @ v.co for v in me.vertices]
    ns = [(R @ v.normal).normalized() for v in me.vertices]
    ev.to_mesh_clear()
    return cs, ns


def _bvh_de(partes):
    """Uma BVH de várias malhas: [(vértices, faces)]."""
    pts, fs = [], []
    for cs, faces in partes:
        k = len(pts)
        pts += [tuple(c) for c in cs]
        fs += [tuple(i + k for i in f) for f in faces]
    return BVHTree.FromPolygons(pts, fs)


def _conta_roupa(rig, lado, coords, faces, dono, bvh, esconde, normais=None, oclusor=None):
    """Na pele dada (os vértices do braço de cima, de dono o upper_arm), com a peça na pose: (os vértices das faces que
    ela não esconde que furam, o mais fundo em mm, os números deles, os números escondidos que abririam buraco e a menor
    folga entre a pele e a peça, mm). Um raio sai do eixo do osso (na pose) e passa pelo vértice: fura se a peça está no
    caminho antes dele (mais FOLGA). Buraco: um número escondido com algum vértice onde a peça não cobre (o raio não a
    acha) e longe dela (o ponto mais perto a mais de 5 mm: o raio passa pelas frestas da peça no sovaco e na dobra do
    cotovelo) e à mostra (com o oclusor: um raio pela normal da pele sai sem bater no corpo nem na peça; a pele dobrada
    para dentro do ombro não aparece). A pele escondida que passa da peça onde ela cobre não abre buraco (a peça
    aparece por cima)."""
    pb = rig.pose.bones["upper_arm." + lado]
    a = rig.matrix_world @ pb.head
    d = (rig.matrix_world.to_3x3() @ (pb.tail - pb.head)).normalized()
    fura, fundo, nums_fura, buracos = set(), 0.0, set(), set()
    folga = None
    for vs, num in faces:
        for i in vs:
            if dono.get(i) != "upper_arm." + lado:
                continue
            p = coords[i]
            q = a + d * (p - a).dot(d)
            r = (p - q).length
            if r < 1e-5:
                continue
            hit = bvh.ray_cast(q, (p - q).normalized(), r + 0.08)
            if hit[0] is not None and hit[3] >= r:
                folga = hit[3] - r if folga is None else min(folga, hit[3] - r)
            if num not in esconde:
                if hit[0] is not None and hit[3] < r + FOLGA:
                    fura.add(i)
                    nums_fura.add(num)
                    fundo = max(fundo, (r + FOLGA - hit[3]) * 1000)
            elif num not in buracos and hit[0] is None:
                loc, _, _, dist = bvh.find_nearest(p)
                if loc is None or dist > 0.005:
                    if oclusor is not None and normais is not None:
                        n = normais[i]
                        if oclusor.ray_cast(p + n * 0.002, n, 0.3)[0] is not None:
                            continue
                    buracos.add(num)
    return (len(fura), round(fundo, 1), sorted(nums_fura), sorted(buracos),
            None if folga is None else round(folga * 1000, 1))


def conferir(pasta=None, fbx_antes=None, lado="L"):
    rel = {}
    poses = _poses()
    ag.nova_cena()
    novos = ag.importar(mn.FBX_ROUPAS)
    rig = [o for o in novos if o.type == "ARMATURE"][0]
    npc = bpy.data.objects["NPC_Corpo"]
    pecas_r = {o.name[len("Roupa_"):]: o for o in novos if o.type == "MESH" and o.name.startswith("Roupa_")}
    hoje = ler_braco_de_hoje()
    # a Mao_Corpo de antes (o FBX dado), com outro nome
    antes = {}
    if fbx_antes and os.path.exists(fbx_antes):
        for o in ag.importar(fbx_antes):
            if o.type == "MESH":
                antes[o.name] = o
                o.name = "Antes_" + o.name
            else:
                bpy.data.objects.remove(o, do_unlink=True)
    novos2 = ag.importar(mn.SAIDA_CORPO)
    rig2 = [o for o in novos2 if o.type == "ARMATURE"][0]
    malhas = {o.name: o for o in novos2 if o.type == "MESH"}
    rel["objetos_no_fbx"] = sorted(malhas)
    for nome, ob in antes.items():
        if nome in malhas:
            rel["igual_ao_de_antes_" + nome] = _comparar_mao(ob, malhas[nome])
    # a peça nova
    pesos_hoje = mn._pesos(npc)
    for ld in mn.LADOS:
        ob = malhas[f"{NOME}.{ld}"]
        me = ob.data
        mao = malhas[f"Mao_Corpo.{ld}"]
        pesos = mn._pesos(ob)
        kb = me.shape_keys.key_blocks
        Bi = rig.data.bones["upper_arm." + ld].matrix_local.inverted()
        # os anéis (pelo y no espaço do osso, agrupados)
        ys = sorted(((Bi @ v.co).y, v.index) for v in me.vertices)
        grupos, ult = {}, None
        for y, i in ys:
            if ult is None or y - ult > 0.012:
                chave = y
            grupos.setdefault(chave, []).append(i)
            ult = y
        grupos = {round(sum((Bi @ me.vertices[i].co).y for i in idx) / len(idx) * 100, 1): idx
                  for idx in grupos.values()}
        borda = _borda(ob)
        vb = {v for e in borda for v in e}
        # a emenda no ombro: os vértices da borda de hoje (S0) e os da peça no mesmo lugar
        s0 = hoje[ld]["aneis"][0]
        d_ombro, dm_ombro, dp_ombro = 0.0, 0.0, 0.0
        for v in s0:
            k = min(range(len(me.vertices)), key=lambda i: (me.vertices[i].co - v["co"]).length)
            d_ombro = max(d_ombro, (me.vertices[k].co - v["co"]).length)
            dm_ombro = max(dm_ombro, (kb["Mulher"].data[k].co - v["mu"]).length)
            dp_ombro = max(dp_ombro, max(abs(pesos[k].get(g, 0) - v["pesos"].get(g, 0))
                                         for g in set(pesos[k]) | set(v["pesos"])))
        # a emenda no cotovelo: o primeiro anel da Mao_Corpo (o lugar, a "Mulher" e as normais)
        an = _primeiro_anel_da_mao(mao, ld)
        me.update()
        cn = me.corner_normals
        nv = {}
        for li, lp in enumerate(me.loops):
            nv.setdefault(lp.vertex_index, []).append(cn[li].vector.copy())
        d_cot, dm_cot, ang = 0.0, 0.0, 0.0
        for c, m, n in an:
            k = min(range(len(me.vertices)), key=lambda i: (me.vertices[i].co - c).length)
            d_cot = max(d_cot, (me.vertices[k].co - c).length)
            dm_cot = max(dm_cot, (kb["Mulher"].data[k].co - m).length)
            ang = max(ang, max(math.degrees(x.angle(n, 0.0)) for x in nv[k]))
        # a seção no meio (os lados) e o tamanho de cada anel
        aneis = []
        for y, idx in sorted(grupos.items()):
            q = [Bi @ me.vertices[i].co for i in idx]
            aneis.append({"y_cm": y, "vertices": len(idx),
                          "frente_tras_cm": round((max(p.x for p in q) - min(p.x for p in q)) * 100, 1),
                          "cima_baixo_cm": round((max(p.z for p in q) - min(p.z for p in q)) * 100, 1),
                          "upper_arm_min": round(min(pesos[i].get("upper_arm." + ld, 0) for i in idx), 3)})
        rel[ob.name] = {
            "vertices": len(me.vertices), "faces": len(me.polygons),
            "triangulos": sum(len(p.vertices) - 2 for p in me.polygons),
            "aneis": aneis, "arestas_abertas": len(borda), "vertices_das_bordas": len(vb),
            "formas": [k.name for k in kb], "uvs": [u.name for u in me.uv_layers],
            "grupos": sorted(g.name for g in ob.vertex_groups),
            "soma_dos_pesos": [round(min(sum(p.values()) for p in pesos.values()), 4),
                               round(max(sum(p.values()) for p in pesos.values()), 4)],
            "ossos_por_vertice_max": max(len(p) for p in pesos.values()),
            "emenda_ombro_mm": {"lugar": round(d_ombro * 1000, 3), "mulher": round(dm_ombro * 1000, 3),
                                "pesos_max_dif": round(dp_ombro, 4)},
            "emenda_cotovelo_mm": {"lugar": round(d_cot * 1000, 3), "mulher": round(dm_cot * 1000, 3),
                                   "normais_max_graus": round(ang, 3)},
            "numeros_usados": mn._faixas_texto(sorted({int(math.floor(d.uv.x + 1e-3))
                                                       for d in me.uv_layers["Face"].data})),
            "celulas_uv0": sorted({(int(d.uv.x // mn.CELULA), int(d.uv.y // mn.CELULA))
                                   for d in me.uv_layers["UVMap"].data}),
        }
    # a borda do corte novo no corpo (como a Emenda da MontagemDaMaoNova): os vértices do NPC_Corpo entre as faces que
    # saem (o antebraço, a mão e o braço de cima) e as que ficam, e a distância ao vértice mais perto das peças
    tirar = set()
    for ld in mn.LADOS:
        tirar |= set(mn.faces_do_braco(npc, ld)) | set(faces_de_cima(npc, ld))
    usa_tira, usa_fica = set(), set()
    for f in npc.data.polygons:
        (usa_tira if f.index in tirar else usa_fica).update(f.vertices)
    bordas = usa_tira & usa_fica
    pts = [o.matrix_world @ v.co for o in malhas.values() for v in o.data.vertices]
    pior = max(min((p - npc.matrix_world @ npc.data.vertices[v].co).length for p in pts) for v in bordas)
    rel["corte_novo"] = {"faces_por_lado": {ld: len(set(mn.faces_do_braco(npc, ld)) | set(faces_de_cima(npc, ld)))
                                            for ld in mn.LADOS},
                         "triangulos_por_lado": {ld: sum(len(npc.data.polygons[f].vertices) - 2 for f in
                                                         set(mn.faces_do_braco(npc, ld)) | set(faces_de_cima(npc, ld)))
                                                 for ld in mn.LADOS},
                         "vertices_na_borda": len(bordas), "o_mais_longe_de_um_vertice_das_pecas_mm": round(pior * 1000, 3)}
    # as roupas
    esc, lugar = mn._esconde_do_catalogo()
    nova = malhas[f"{NOME}.{lado}"]
    for o in [nova]:
        o.parent = None
        o.matrix_world = rig.matrix_world.copy()
        for md in o.modifiers:
            if md.type == "ARMATURE":
                md.object = rig
    nums_hoje = set(hoje[lado]["numeros"].values())
    vestem = {n: p for n, p in pecas_r.items() if esc.get(n, set()) & nums_hoje}
    f_hoje, dono_hoje = _pele_do_braco(npc, faces_de_cima(npc, lado), lado)
    f_novo, dono_novo = _pele_do_braco(nova, range(len(nova.data.polygons)), lado)
    mao_l = malhas[f"Mao_Corpo.{lado}"]
    mao_l.parent = None
    mao_l.matrix_world = rig.matrix_world.copy()
    for md in mao_l.modifiers:
        if md.type == "ARMATURE":
            md.object = rig
    f_mao, _ = _pele_do_braco(mao_l, range(len(mao_l.data.polygons)), lado)
    # o corpo de hoje (sem o antebraço e a mão novos) e o novo (sem o braço de cima também), com o número das faces
    corte_ante = set(mn.faces_do_braco(npc, "L")) | set(mn.faces_do_braco(npc, "R"))
    corte_cima = set(faces_de_cima(npc, "L")) | set(faces_de_cima(npc, "R"))
    todas, _ = _pele_do_braco(npc, range(len(npc.data.polygons)), lado)
    corpo_hoje = [f for k, f in enumerate(todas) if k not in corte_ante]
    corpo_novo = [f for k, f in enumerate(todas) if k not in corte_ante and k not in corte_cima]
    roupas = {}
    furam = {}
    for pose in POSES:
        _por_pose(rig, poses[pose])
        for forma in ("Homem", "Mulher"):
            _mulher([npc, nova] + list(vestem.values()), 1.0 if forma == "Mulher" else 0.0)
            dg = bpy.context.evaluated_depsgraph_get()
            c_hoje, n_hoje = _coords_e_normais(npc, dg)
            c_novo, n_novo = _coords_e_normais(nova, dg)
            c_mao = _coords_e_normais(mao_l, dg)[0]
            for nome, pc in sorted(vestem.items()):
                cs, fs = mn._coords(pc, dg)
                bvh = BVHTree.FromPolygons([tuple(c) for c in cs], fs)
                e = esc.get(nome, set())
                # o que tapa a vista: a peça, o corpo sem as faces escondidas e as peças novas sem as escondidas
                fora = lambda lst: [vs for vs, num in lst if num not in e]          # noqa: E731
                ocl_h = _bvh_de([(cs, fs), (c_hoje, fora(corpo_hoje)), (c_mao, fora(f_mao))])
                ocl_n = _bvh_de([(cs, fs), (c_hoje, fora(corpo_novo)), (c_mao, fora(f_mao)), (c_novo, fora(f_novo))])
                r = roupas.setdefault(nome, {"lugar": lugar.get(nome), "esconde_do_braco_de_cima": len(e & nums_hoje)})
                h = _conta_roupa(rig, lado, c_hoje, f_hoje, dono_hoje, bvh, e, n_hoje, ocl_h)
                n = _conta_roupa(rig, lado, c_novo, f_novo, dono_novo, bvh, e, n_novo, ocl_n)
                r[f"{pose}_{forma}"] = {"fura_hoje": h[:2], "fura_novo": n[:2], "buracos_hoje": h[3],
                                        "buracos_novo": n[3], "folga_min_mm": [h[4], n[4]]}
                if n[0] > h[0] or set(n[3]) - set(h[3]):
                    furam.setdefault(nome, {"fura": set(), "buracos": set()})
                    furam[nome]["fura"].update(n[2])
                    furam[nome]["buracos"].update(set(n[3]) - set(h[3]))
    _por_pose(rig, None)
    rel["roupas"] = roupas
    rel["roupas_com_problema_no_novo"] = {n: {k: sorted(v) for k, v in d.items()} for n, d in furam.items()}
    corrigidas = {}
    for nome, d in furam.items():
        if d["buracos"]:
            corrigidas[nome] = {"alvo": "NPC_Corpo", "faces": mn._faixas_texto(sorted(esc[nome] - d["buracos"])),
                                "tirados": mn._faixas_texto(sorted(d["buracos"]))}
    rel["esconde_corrigido"] = corrigidas
    if pasta:
        os.makedirs(pasta, exist_ok=True)
        with open(os.path.join(pasta, "braco_cima_roupas.json"), "w", encoding="utf-8") as f:
            f.write(json.dumps(rel, indent=1, default=str, ensure_ascii=False))
        if corrigidas:
            with open(os.path.join(pasta, "braco_cima_esconde_corrigido.json"), "w", encoding="utf-8") as f:
                f.write(json.dumps(corrigidas, indent=1, ensure_ascii=False))
    return rel


# ================================================================================================ as fotos


def _juntar(npc, pecas, rig, nome, cima):
    """Uma cópia do NPC_Corpo sem o antebraço e a mão (e sem o braço de cima, com cima) e com as peças dadas, numa
    malha só (com a forma "Mulher")."""
    nova = npc.copy()
    nova.data = npc.data.copy()
    nova.name = nome
    bpy.context.scene.collection.objects.link(nova)
    tirar = set()
    for lado in mn.LADOS:
        tirar |= set(mn.faces_do_braco(nova, lado))
        if cima:
            tirar |= set(faces_de_cima(nova, lado))
    bm = bmesh.new()
    bm.from_mesh(nova.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in tirar], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(nova.data)
    bm.free()
    copias = []
    for p in pecas:
        c = p.copy()
        c.data = p.data.copy()
        bpy.context.scene.collection.objects.link(c)
        c.parent = None
        c.matrix_world = rig.matrix_world.copy()
        if c.data.shape_keys and "SemPelos" in c.data.shape_keys.key_blocks:
            c.shape_key_remove(c.data.shape_keys.key_blocks["SemPelos"])
        for md in c.modifiers:
            if md.type == "ARMATURE":
                md.object = rig
        copias.append(c)
    for o in bpy.context.scene.objects:
        o.select_set(o in [nova] + copias)
    bpy.context.view_layer.objects.active = nova
    with bpy.context.temp_override(active_object=nova, selected_editable_objects=[nova] + copias,
                                   selected_objects=[nova] + copias):
        bpy.ops.object.join()
    for md in nova.modifiers:
        if md.type == "ARMATURE":
            md.object = rig
    return nova


def _camera_em(pos, alvo, cima=(0, 0, 1), lente=50):
    cam = ag._camera("Foto", pos, alvo=alvo, lente=lente)
    cam.rotation_euler = mn._olhar(pos, alvo, Vector(cima)).to_euler()
    return cam


def _par(pasta, nome, versoes, mostrar_tambem=()):
    """A foto de cada versão (hoje e novo) com a mesma câmera."""
    saidas = []
    for qual, ob in versoes.items():
        for o in versoes.values():
            o.hide_render = o is not ob
        for o in mostrar_tambem:
            o.hide_render = False
        c = os.path.join(pasta, f"BracoCima_{nome}_{qual}.png")
        ag._salvar(c)
        saidas.append(c)
    for o in mostrar_tambem:
        o.hide_render = True
    return saidas


def fotos(pasta):
    os.makedirs(pasta, exist_ok=True)
    poses = _poses()
    ag.nova_cena()
    novos = ag.importar(mn.FBX_ROUPAS)
    rig = [o for o in novos if o.type == "ARMATURE"][0]
    npc = bpy.data.objects["NPC_Corpo"]
    pecas_r = {o.name[len("Roupa_"):]: o for o in novos if o.type == "MESH" and o.name.startswith("Roupa_")}
    novos2 = ag.importar(mn.SAIDA_CORPO)
    maos = [o for o in novos2 if o.type == "MESH" and o.name.startswith("Mao_Corpo")]
    bracos = [o for o in novos2 if o.type == "MESH" and o.name.startswith(NOME)]
    hoje = _juntar(npc, maos, rig, "Hoje", False)
    novo = _juntar(npc, maos + bracos, rig, "Novo", True)
    for o in novos2:
        bpy.data.objects.remove(o, do_unlink=True)
    for m in bpy.data.materials:
        if m.name.startswith(("Roupa_Corpo", "PrincipalBloco")):
            ag._textura_em(m, ag.TEXTURA)
    tecido = bpy.data.materials.new("Tecido")
    tecido.diffuse_color = (0.18, 0.24, 0.34, 1.0)
    for o in pecas_r.values():
        o.hide_render = True
        for i in range(len(o.material_slots)):
            o.material_slots[i].link = "OBJECT"
            o.material_slots[i].material = tecido
    npc.hide_render = True
    esc, _ = mn._esconde_do_catalogo()
    versoes = {"hoje": hoje, "novo": novo}
    ag._render_base(400, 400)
    saidas = []
    for pose in POSES:
        _por_pose(rig, poses[pose])
        pb = rig.pose.bones
        M = rig.matrix_world
        ombro, cot, mao = M @ pb["upper_arm.L"].head, M @ pb["forearm.L"].head, M @ pb["hand.L"].tail
        meio = (ombro + cot + mao) / 3
        p = "Repouso" if pose == "Parado" else "Dobrado"
        # o braço inteiro: de frente, de lado (de fora) e de cima
        for vista, desl, cima in (("1frente", (0, -1.0, 0), (0, 0, 1)), ("2lado", (1.0, 0, 0), (0, 0, 1)),
                                  ("3cima", (0, 0, 1.0), (0, 1, 0))):
            _camera_em(meio + Vector(desl), meio, cima)
            saidas += _par(pasta, f"{p}_{vista}", versoes)
        # o cotovelo de perto (o fim do braço de cima, a emenda com o antebraço)
        emenda = ombro.lerp(cot, 0.9)
        for vista, desl in (("4cotovelo_lado", (0.30, 0.0, 0.0)), ("5cotovelo_tras", (0.10, 0.28, 0.0))):
            if pose != "Parado" and vista.startswith("5"):
                continue
            _camera_em(emenda + Vector(desl), emenda)
            saidas += _par(pasta, f"{p}_{vista}", versoes)
        # a mulher
        if pose == "Parado":
            _mulher([hoje, novo], 1.0)
            for vista, desl in (("6mulher_frente", (0, -1.0, 0)), ("7mulher_lado", (1.0, 0, 0))):
                _camera_em(meio + Vector(desl), meio)
                saidas += _par(pasta, f"{p}_{vista}", versoes)
            _mulher([hoje, novo], 0.0)
        # as roupas
        for nome in ROUPAS_DAS_FOTOS:
            pc = pecas_r.get(nome)
            if pc is None:
                continue
            corpos = {"hoje": mn._sem_faces(hoje, esc.get(nome), "Hoje_" + nome),
                      "novo": mn._sem_faces(novo, esc.get(nome), "Novo_" + nome)}
            hoje.hide_render = novo.hide_render = True
            vista, desl = ("8", (0, -1.0, 0)) if pose == "Parado" else ("8", (0.75, -0.6, 0.15))
            _camera_em(meio + Vector(desl), meio)
            saidas += _par(pasta, f"{p}_{vista}{nome}", corpos, [pc])
            # o ombro de perto, pela frente (o começo da peça nova, embaixo da manga)
            perto = ombro.lerp(cot, 0.3)
            _camera_em(perto + Vector((0.25, -0.50, 0.12)), perto)
            saidas += _par(pasta, f"{p}_9ombro_{nome}", corpos, [pc])
            for o in corpos.values():
                bpy.data.objects.remove(o, do_unlink=True)
    _por_pose(rig, None)
    print("\n".join(saidas))
    return saidas


def folha(pasta):
    """Folha_BracoCima.png: cada vista com o braço de hoje e o novo lado a lado."""
    import re
    vistas = {}
    for f in sorted(os.listdir(pasta)):
        m = re.match(r"BracoCima_(\w+?)_(hoje|novo)\.png$", f)
        if m:
            vistas.setdefault(m.group(1), {})[m.group(2)] = f
    rot = {"1frente": "de frente", "2lado": "de lado (de fora)", "3cima": "de cima", "4cotovelo_lado": "o cotovelo, de fora",
           "5cotovelo_tras": "o cotovelo, de trás", "6mulher_frente": "a mulher, de frente",
           "7mulher_lado": "a mulher, de lado"}
    for nome in ROUPAS_DAS_FOTOS:
        rot["9ombro_" + nome] = "o ombro de perto, com a " + nome.replace("_", " ")

    def rotulo(n):
        pose, _, v = n.partition("_")
        r = rot.get(v) or ("com a " + v[1:].replace("_", " "))
        return f"{'repouso' if pose == 'Repouso' else 'dobrado (a guarda)'}: {r}"
    lista = [(rotulo(n), arqs) for n, arqs in sorted(vistas.items(), key=lambda x: (x[0].startswith("Dobrado"), x[0]))]
    c = mn._folha(pasta, "BracoCima", "O braço de cima: hoje (a caixa, com a mão e o antebraço novos) e o novo "
                  "(arredondado e liso) - o homem, a mulher, a camiseta e a jaqueta", lista, ("hoje", "novo"),
                  por_linha=2, escala=0.7)
    print(c)
    return c


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    etapa = args[0] if args else ""
    pasta = args[1] if len(args) > 1 else None
    if etapa == "medir":
        print("MEDIR " + json.dumps(medir(), indent=1, default=str, ensure_ascii=False))
    elif etapa == "conferir":
        print("CONFERIR " + json.dumps(conferir(pasta, args[2] if len(args) > 2 else None), indent=1, default=str,
                                       ensure_ascii=False))
    elif etapa == "fotos" and pasta:
        fotos(pasta)
    else:
        print("uso: blender --background --python braco_de_cima.py -- medir | conferir [pasta] [fbx de antes] | "
              "fotos <pasta>")


if __name__ == "__main__":
    if bpy is None:
        if len(sys.argv) > 2 and sys.argv[1] == "folha":
            folha(sys.argv[2])
        else:
            print("uso: py braco_de_cima.py folha <pasta das fotos>")
    else:
        main()
