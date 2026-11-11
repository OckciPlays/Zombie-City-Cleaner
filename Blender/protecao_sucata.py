# A proteção de sucata (10/11/2026, a etapa 3 das armas de sucata): as peças rígidas que vão presas aos ossos do corpo
# (como o equipamento rígido: a mochila no corpo, o EquipamentoNoCorpo), feitas por cima das roupas mais grossas do
# Roupas.fbx (as jaquetas, os moletons, o jaleco, o colete tático, as calças e as botas), sem furar e sem flutuar:
# - Protecao_Capacete (osso head): a panela de ponta-cabeça, com o toco do cabo cortado, o fundo queimado e a tira de
#   couro no queixo;
# - Protecao_Bracadeira.L / .R (forearm.L / .R): a revista enrolada no antebraço, com três voltas de fita prata;
# - Protecao_Caneleira.L / .R (shin.L / .R): o cano de PVC cortado ao meio na frente da canela, com duas voltas de fita;
# - Protecao_Colete (chest): a chapa pintada na frente e o pedaço de pneu atrás, presos por tiras de câmara de ar nos
#   ombros e por cordas nos lados.
# O tamanho sai do próprio corpo: cada peça é medida em volta do osso dela (no repouso do PrincipalBloco_Rig, o do
# Roupas.fbx) pelo envelope do corpo e de todas as roupas que passam ali (o homem e a forma "Mulher"): os tubos são
# polígonos que contêm todos os pontos do envelope (com uma folga), as placas ficam na frente (e atrás) do ponto mais
# saliente e as tiras seguem o fecho convexo do envelope (a tira esticada). Cada malha tem a forma "Mulher" (a mesma
# forma do corpo e das roupas, que a Unity liga junto), medida do mesmo jeito no corpo da mulher.
#
# A convenção (o JSON Protecao_Sucata.json conta tudo): a malha de cada peça está no espaço do osso dela, na cabeça do
# osso e na orientação dele, como a Unity o vê (o X da Unity é o -X do Blender, como em todo FBX); na Unity é só pôr a
# peça como filha do osso com a posição local zero, o giro zero e a escala 1. No FBX cada peça é um objeto girado 90°
# em X com a malha guardada nos eixos da Unity (o mesmo truque do Loot.fbx e do Itens.fbx: o nó fica sem giro).
# Cores: os blocos da Paleta.png (as 8 da sucata, na linha 12 e na 11 de baixo, e as de sempre da linha 12).
#
# Também os itens soltos (Item_Capacete, Item_Bracadeiras, Item_Caneleiras e Item_Colete), no formato do Loot.fbx, que o
# loot_etapa34.py junta no Loot_Etapa34.fbx.
#
# Como rodar (Blender 5.2, em segundo plano; um Blender por vez; os FBX do projeto só são lidos):
#   blender -b --factory-startup --disable-autoexec --python Blender/protecao_sucata.py -- tudo <pasta de saída>
#   (as etapas: medir, construir, exportar, conferir, fotos; "tudo" faz todas. A saída padrão é a pasta do jogador,
#   Assets/_Game/Art/Characters/Jogador; as fotos vão para <pasta de saída>/fotos)

import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import equipamentos as eq  # noqa: E402
import loot  # noqa: E402,F401  (as cores do saque)
import sucata_itens as si  # noqa: E402  (as cores da sucata)

ARTE = os.path.join(eq.REPO, "Assets", "_Game", "Art")
ROUPAS = os.path.join(ARTE, "Characters", "Roupas", "Roupas.fbx")
CORPO = os.path.join(ARTE, "Characters", "Personagens", "Personagem_PrincipalBloco.fbx")
RIG = "PrincipalBloco_Rig"

eq.CORES.update({
    "p_creme": (3, 9), "p_capa_vermelha": (3, 10), "p_pvc": (11, 14), "p_couro": (3, 7), "p_couro_claro": (3, 8),
    "p_pele": (13, 0),
})

# as peças: o nome, o osso e o construtor (preenchido no fim do arquivo)
PECAS = [
    ("Protecao_Capacete", "head"),
    ("Protecao_Bracadeira.L", "forearm.L"),
    ("Protecao_Bracadeira.R", "forearm.R"),
    ("Protecao_Caneleira.L", "shin.L"),
    ("Protecao_Caneleira.R", "shin.R"),
    ("Protecao_Colete", "chest"),
]
# as roupas que não contam no envelope de cada lugar (os chapéus saem quando o capacete entra; o cabelo passa por cima das
# tiras do colete)
CHAPEUS = ("Roupa_Capacete", "Roupa_Capacete_Militar", "Roupa_Chapeu_Cauboi", "Roupa_Quepe", "Roupa_Gorro", "Roupa_Bone",
           "Roupa_Bone_Militar", "Roupa_Bone_Policia", "Roupa_Bandana", "Roupa_Viseira", "Roupa_Visao_Noturna")

FOLGA = 0.004          # entre o envelope das roupas e a parte de dentro da peça (m)
TRECHO_BRACO = (0.06, 0.21)      # a braçadeira no antebraço (m, da cabeça do osso; o osso tem 0,248): longe do cotovelo
TRECHO_CANELA = (0.07, 0.32)     # a caneleira na canela (o osso tem 0,48): o joelho dobra por cima


# ================================================================================================ o envelope
def _pontos(o, mulher):
    """Os vértices de o no mundo (com o esqueleto como estiver), no homem ou na mulher (a forma "Mulher")."""
    kb = o.data.shape_keys.key_blocks if o.data.shape_keys else None
    if kb and "Mulher" in kb:
        kb["Mulher"].value = 1.0 if mulher else 0.0
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    e = o.evaluated_get(dg)
    me = e.to_mesh()
    M = o.matrix_world
    r = [M @ v.co for v in me.vertices]
    e.to_mesh_clear()
    return r


class Envelope:
    """Os pontos do corpo e de todas as roupas no repouso, no homem e na mulher."""

    def __init__(self, rig):
        self.rig = rig
        self.malhas = [o for o in bpy.data.objects if o.type == "MESH" and (o.name == "NPC_Corpo" or o.name.startswith("Roupa_"))]
        self.pts = {m: {o.name: _pontos(o, m) for o in self.malhas} for m in (False, True)}
        for o in self.malhas:
            if o.data.shape_keys and "Mulher" in o.data.shape_keys.key_blocks:
                o.data.shape_keys.key_blocks["Mulher"].value = 0.0

    def todos(self, mulher, tirar=()):
        out = []
        for nome, vs in self.pts[mulher].items():
            if nome in tirar or any(nome.startswith(t[:-1]) for t in tirar if t.endswith("*")):
                continue
            out += vs
        return out

    def de(self, mulher, nomes):
        out = []
        for nome in nomes:
            out += self.pts[mulher].get(nome, [])
        return out


# ================================================================================================ geometria
def _hull(pts):
    """O fecho convexo 2D (as tuplas (a, b)), no sentido anti-horário."""
    p = sorted(set((round(a, 6), round(b, 6)) for a, b in pts))
    if len(p) < 3:
        return p

    def cruz(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    baixo, cima = [], []
    for q in p:
        while len(baixo) >= 2 and cruz(baixo[-2], baixo[-1], q) <= 0:
            baixo.pop()
        baixo.append(q)
    for q in reversed(p):
        while len(cima) >= 2 and cruz(cima[-2], cima[-1], q) <= 0:
            cima.pop()
        cima.append(q)
    return baixo[:-1] + cima[:-1]


def _caminho_no_hull(h, a, b, lado):
    """Do vértice a ao b do fecho (índices), pelo lado em que a média de lado(ponto) é maior."""
    n = len(h)
    ida = [h[(a + k) % n] for k in range(((b - a) % n) + 1)]
    volta = [h[(a - k) % n] for k in range(((a - b) % n) + 1)]
    med = lambda c: sum(lado(q) for q in c) / len(c)  # noqa: E731
    return ida if med(ida) >= med(volta) else volta


def _afastar(cadeia, centro, d):
    """Afasta cada ponto 2D da cadeia (convexa) por d, para fora (a média das normais das arestas vizinhas)."""
    out = []
    n = len(cadeia)
    cx, cy = centro
    for i, (x, y) in enumerate(cadeia):
        ns = []
        for j in (i - 1, i):
            if 0 <= j < n - 1:
                ax, ay = cadeia[j]
                bx, by = cadeia[j + 1]
                tx, ty = bx - ax, by - ay
                L = math.hypot(tx, ty) or 1.0
                nx, ny = ty / L, -tx / L
                mx, my = (ax + bx) / 2 - cx, (ay + by) / 2 - cy
                if nx * mx + ny * my < 0:
                    nx, ny = -nx, -ny
                ns.append((nx, ny))
        nx = sum(v[0] for v in ns) / len(ns)
        ny = sum(v[1] for v in ns) / len(ns)
        L = math.hypot(nx, ny) or 1.0
        cosm = max(0.5, (nx / L) * ns[0][0] + (ny / L) * ns[0][1])
        out.append((x + nx / L * d / cosm, y + ny / L * d / cosm))
    return out


def _reamostrar(cadeia, n):
    """A cadeia (pontos Vector ou tuplas) com n pontos igualmente espaçados pelo comprimento."""
    pts = [Vector(p) for p in cadeia]
    acum = [0.0]
    for i in range(1, len(pts)):
        acum.append(acum[-1] + (pts[i] - pts[i - 1]).length)
    L = acum[-1]
    out = []
    j = 0
    for k in range(n):
        s = L * k / (n - 1)
        while j < len(pts) - 2 and acum[j + 1] < s:
            j += 1
        seg = acum[j + 1] - acum[j] or 1.0
        out.append(pts[j].lerp(pts[j + 1], (s - acum[j]) / seg))
    return out


def poligono(pts2d, angs, margem):
    """Os raios dos vértices de um polígono (os ângulos angs, igualmente espaçados numa volta inteira em volta de (0, 0))
    que contém todos os pontos (x, z), afastado deles por margem. Cada aresta fica na frente dos pontos do setor dela (a
    reta perpendicular à bissetriz, na distância da projeção mais longe), e cada vértice pega o maior das suas duas
    arestas: com um vértice mais para fora, a aresta só sai mais para fora dentro do setor."""
    n = len(angs)
    passo = 2 * math.pi / n
    rho = [0.0] * n
    for x, z in pts2d:
        r = math.hypot(x, z)
        if r < 1e-6:
            continue
        a = math.atan2(z, x)
        i = int(((a - angs[0]) % (2 * math.pi)) / passo) % n
        meio = angs[i] + passo / 2
        rho[i] = max(rho[i], r * math.cos(a - meio) / math.cos(passo / 2))
    R = [max(rho[i - 1], rho[i]) for i in range(n)]
    piso = 0.6 * (sum(R) / n)
    return [max(r, piso) + margem / math.cos(passo / 2) for r in R]


def _face(bm, vs):
    return bm.faces.new(vs)


def casca(bm, uv, cores, dentro, fora, fechado=True):
    """Uma casca sólida: os anéis de dentro e de fora (listas de pontos, um anel por posição ao longo da peça, todos com o
    mesmo número de pontos), a face de fora, a de dentro, as pontas e, se aberta, as laterais.
    cores: (fora, dentro, pontas, laterais)."""
    nA, nK = len(dentro), len(dentro[0])
    vi = [[bm.verts.new(p) for p in anel] for anel in dentro]
    vo = [[bm.verts.new(p) for p in anel] for anel in fora]
    kmax = nK if fechado else nK - 1
    fs = {0: [], 1: [], 2: [], 3: []}
    for a in range(nA - 1):
        for k in range(kmax):
            k2 = (k + 1) % nK
            fs[0].append(_face(bm, (vo[a][k], vo[a][k2], vo[a + 1][k2], vo[a + 1][k])))
            fs[1].append(_face(bm, (vi[a][k2], vi[a][k], vi[a + 1][k], vi[a + 1][k2])))
    for a in (0, nA - 1):
        for k in range(kmax):
            k2 = (k + 1) % nK
            fs[2].append(_face(bm, (vi[a][k], vi[a][k2], vo[a][k2], vo[a][k])))
    if not fechado:
        for k in (0, nK - 1):
            for a in range(nA - 1):
                fs[3].append(_face(bm, (vi[a][k], vo[a][k], vo[a + 1][k], vi[a + 1][k])))
    for i, lista in fs.items():
        if lista:
            eq._pintar(bm, uv, lista, cores[min(i, len(cores) - 1)])
    return fs


def fita(bm, uv, cor, pontos, largura, espessura, lado, fora_de):
    """Uma tira (um retângulo puxado pela polilinha pontos): a largura na direção lado (sem a parte ao longo da tira) e a
    espessura para fora (do ponto fora_de, ou da função fora_de(p))."""
    dentro, fora = [], []
    n = len(pontos)
    for k, p in enumerate(pontos):
        t = (pontos[min(k + 1, n - 1)] - pontos[max(k - 1, 0)]).normalized()
        w = Vector(lado) - t * t.dot(Vector(lado))
        w.normalize()
        nn = t.cross(w)
        ref = fora_de(p) if callable(fora_de) else Vector(fora_de)
        if nn.dot(p - ref) < 0:
            nn = -nn
        dentro.append([p - nn * espessura / 2 + w * largura / 2, p - nn * espessura / 2 - w * largura / 2])
        fora.append([p + nn * espessura / 2 + w * largura / 2, p + nn * espessura / 2 - w * largura / 2])
    return casca(bm, uv, (cor, cor, cor, cor), dentro, fora, fechado=False)


def corda(bm, uv, cor, pontos, raio, lados=6):
    """Um tubo redondo pela polilinha (a corda), com as pontas fechadas."""
    aneis = []
    n = len(pontos)
    u_ant = None
    for k, p in enumerate(pontos):
        t = (pontos[min(k + 1, n - 1)] - pontos[max(k - 1, 0)]).normalized()
        u = (u_ant - t * t.dot(u_ant)) if u_ant is not None else t.orthogonal()
        u.normalize()
        w = t.cross(u)
        u_ant = u
        aneis.append([bm.verts.new(p + (u * math.cos(2 * math.pi * j / lados) + w * math.sin(2 * math.pi * j / lados)) * raio)
                      for j in range(lados)])
    faces = []
    for k in range(n - 1):
        for j in range(lados):
            j2 = (j + 1) % lados
            faces.append(bm.faces.new((aneis[k][j], aneis[k][j2], aneis[k + 1][j2], aneis[k + 1][j])))
    faces += [bm.faces.new(aneis[0][::-1]), bm.faces.new(aneis[-1])]
    eq._pintar(bm, uv, faces, cor)


def caixa(bm, uv, cor, centro, eixos, tam):
    """Uma caixa no centro, com os eixos (3 vetores) e o tamanho (3 números), sem chanfro (a mesma topologia sempre)."""
    vs = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                vs.append(bm.verts.new(Vector(centro) + Vector(eixos[0]) * sx * tam[0] / 2 + Vector(eixos[1]) * sy * tam[1] / 2
                                       + Vector(eixos[2]) * sz * tam[2] / 2))
    q = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    faces = [bm.faces.new([vs[i] for i in f]) for f in q]
    eq._pintar(bm, uv, faces, cor)
    return faces


def cilindro(bm, uv, cor, a, b, raio, lados=8):
    si._tubo(bm, uv, cor, tuple(a), tuple(b), raio, lados)


# ================================================================================================ as peças
class Ctx:
    """O que cada construtor recebe: o envelope (homem ou mulher), o esqueleto e onde ele constrói."""

    def __init__(self, env, rig, mulher):
        self.env, self.rig, self.mulher = env, rig, mulher

    def osso(self, nome):
        return self.rig.data.bones[nome].matrix_local.copy()

    def no_osso(self, nome, pts):
        Bi = self.osso(nome).inverted()
        return [Bi @ p for p in pts]


def _tubo_medido(ctx, osso, trecho, corte, mesmo_lado=None, n=10, fase=0.0, aneis=3, margem=FOLGA, tirar=()):
    """O envelope em volta do osso, no trecho (m ao longo dele): o meio (x, z) no espaço do osso, os ângulos e, para cada
    anel (aneis igualmente espaçados), os raios de dentro (os pontos do envelope numa janela em volta do anel)."""
    pts = ctx.env.todos(ctx.mulher, tirar)
    if mesmo_lado is not None:
        pts = [p for p in pts if p.x * mesmo_lado > 0.01]
    loc = [l for l in ctx.no_osso(osso, pts) if trecho[0] - 0.03 <= l.y <= trecho[1] + 0.03 and math.hypot(l.x, l.z) < corte]
    xs = [l.x for l in loc]
    zs = [l.z for l in loc]
    meio = ((min(xs) + max(xs)) / 2, (min(zs) + max(zs)) / 2)
    angs = [fase + 2 * math.pi * k / n for k in range(n)]
    ts = [trecho[0] + (trecho[1] - trecho[0]) * k / (aneis - 1) for k in range(aneis)]
    passo = (trecho[1] - trecho[0]) / (aneis - 1)
    raios = []
    for t in ts:
        janela = [(l.x - meio[0], l.z - meio[1]) for l in loc if abs(l.y - t) <= passo + 0.03]
        raios.append(poligono(janela, angs, margem))
    # o raio de um anel nunca é menor que o dos vizinhos no meio do caminho (os pontos entre dois anéis cabem na reta)
    return meio, angs, ts, raios


def _ang_de(ctx, osso, direcao_mundo):
    """O ângulo (no plano x, z do osso) de uma direção do mundo."""
    d = ctx.osso(osso).to_3x3().inverted() @ Vector(direcao_mundo)
    return math.atan2(d.z, d.x)


def bracadeira(ctx, lado):
    """A revista enrolada no antebraço: o tubo (a capa por fora, as folhas por dentro e nas pontas), a borda da última
    folha por cima e três voltas de fita prata."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    osso = "forearm." + lado
    cima = _ang_de(ctx, osso, (0, 0, 1))
    meio, angs, ts, raios = _tubo_medido(ctx, osso, TRECHO_BRACO, 0.13, n=10, fase=cima)
    cx, cz = meio
    esp = 0.010
    capa = "p_capa_vermelha" if lado == "L" else "s_pintura_verde"

    raios[0] = [r + 0.007 for r in raios[0]]

    def anel(t, rs, d=0.0):
        return [Vector((cx + (r + d) * math.cos(a), t, cz + (r + d) * math.sin(a))) for r, a in zip(rs, angs)]
    dentro = [anel(t, rs) for t, rs in zip(ts, raios)]
    fora = [anel(t, rs, esp) for t, rs in zip(ts, raios)]
    casca(bm, uv, (capa, "p_creme", "p_creme"), dentro, fora)

    # o raio de fora numa altura t (entre os anéis)
    def r_fora(t, k):
        i = max(0, min(len(ts) - 2, int((t - ts[0]) / (ts[1] - ts[0]))))
        f = (t - ts[i]) / (ts[i + 1] - ts[i])
        return raios[i][k] * (1 - f) + raios[i + 1][k] * f + esp
    # a borda da última folha: uma faixa fina por cima, ao longo da revista (no lado de cima, um pouco para fora)
    k0 = 2
    for t0, t1 in ((ts[0] + 0.002, ts[-1] - 0.002),):
        d0 = [[Vector((cx + (r_fora(t, k0) + 0.0005) * math.cos(angs[k0] + da), t,
                       cz + (r_fora(t, k0) + 0.0005) * math.sin(angs[k0] + da))) for da in (-0.07, 0.07)]
              for t in (t0, (t0 + t1) / 2, t1)]
        f0 = [[Vector((cx + (r_fora(t, k0) + 0.003) * math.cos(angs[k0] + da), t,
                       cz + (r_fora(t, k0) + 0.003) * math.sin(angs[k0] + da))) for da in (-0.07, 0.07)]
              for t in (t0, (t0 + t1) / 2, t1)]
        casca(bm, uv, ("p_creme", "p_creme", "p_creme", capa), d0, f0, fechado=False)
    # as três voltas de fita
    larg = 0.022
    for tc in (ts[0] + 0.016, (ts[0] + ts[-1]) / 2, ts[-1] - 0.016):
        dd, ff = [], []
        for t in (tc - larg / 2, tc + larg / 2):
            dd.append([Vector((cx + (r_fora(t, k) + 0.0004) * math.cos(a), t, cz + (r_fora(t, k) + 0.0004) * math.sin(a)))
                       for k, a in enumerate(angs)])
            ff.append([Vector((cx + (r_fora(t, k) + 0.0030) * math.cos(a), t, cz + (r_fora(t, k) + 0.0030) * math.sin(a)))
                       for k, a in enumerate(angs)])
        casca(bm, uv, ("s_fita_prata", "s_fita_prata", "s_fita_prata"), dd, ff)
    # a ponta solta da fita do meio, levantada
    tc = (ts[0] + ts[-1]) / 2
    k = 7
    p = Vector((cx + (r_fora(tc, k) + 0.004) * math.cos(angs[k]), tc, cz + (r_fora(tc, k) + 0.004) * math.sin(angs[k])))
    rad = Vector((math.cos(angs[k]), 0, math.sin(angs[k])))
    tg = Vector((-math.sin(angs[k]), 0, math.cos(angs[k])))
    caixa(bm, uv, "s_fita_prata", p + tg * 0.008 + rad * 0.002, (tg, Vector((0, 1, 0)), rad), (0.018, 0.02, 0.0016))
    return bm, Matrix.Identity(4)


def caneleira(ctx, lado, so_frente=False):
    """O cano de PVC cortado ao meio na frente da canela (redondo, como o cano), a linha impressa e duas voltas de fita
    prata em volta da perna (por cima do cano na frente e da calça atrás)."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    osso = "shin." + lado
    frente = _ang_de(ctx, osso, (0, -1, 0))
    sinal = 1 if lado == "L" else -1
    n = 24
    # (a barra do jaleco passa por cima do joelho: ela não conta)
    meio, angs, ts, raios = _tubo_medido(ctx, osso, TRECHO_CANELA, 0.17, mesmo_lado=sinal, n=n, fase=frente, aneis=3,
                                         tirar=("Roupa_Jaleco",))
    cx, cz = meio
    # o cano: um arco de 210 graus em volta da frente, com o raio que cobre a frente da perna em todo o trecho
    abre = math.radians(105)
    arco = [frente - abre + 2 * abre * k / 14 for k in range(15)]

    def raio_em(rs, a):
        # o raio do polígono no ângulo a (entre dois vértices)
        rel = (a - angs[0]) % (2 * math.pi)
        i = int(rel / (2 * math.pi / n)) % n
        j = (i + 1) % n
        f = (rel - i * 2 * math.pi / n) / (2 * math.pi / n)
        A = Vector((math.cos(angs[i]), math.sin(angs[i]))) * rs[i]
        B = Vector((math.cos(angs[j]), math.sin(angs[j]))) * rs[j]
        d = Vector((math.cos(a), math.sin(a)))
        E = B - A
        det = -d.x * E.y + E.x * d.y
        return (-A.x * E.y + E.x * A.y) / det if abs(det) > 1e-12 else rs[i] * (1 - f) + rs[j] * f
    R = max(raio_em(rs, a) for rs in raios for a in arco)
    esp = 0.006

    def anel(t, r):
        return [Vector((cx + r * math.cos(a), t, cz + r * math.sin(a))) for a in arco]
    t0, t1 = ts[0], ts[-1]
    tm = (t0 + t1) / 2
    casca(bm, uv, ("p_pvc", "p_pvc", "p_pvc", "p_pvc"), [anel(t, R) for t in (t0, tm, t1)],
          [anel(t, R + esp) for t in (t0, tm, t1)], fechado=False)
    # a bolsa do cano (a ponta de encaixe, mais larga), em cima, perto do joelho
    casca(bm, uv, ("p_pvc", "p_pvc", "p_pvc", "p_pvc"), [anel(t, R + esp - 0.001) for t in (t0, t0 + 0.012, t0 + 0.035)],
          [anel(t, R + esp + 0.007) for t in (t0, t0 + 0.012, t0 + 0.035)], fechado=False)
    # a linha impressa no cano (de lado, a 35 graus da frente)
    a = frente + math.radians(35)
    lin_d = [[Vector((cx + (R + esp + 0.0003) * math.cos(a + s), t, cz + (R + esp + 0.0003) * math.sin(a + s)))
              for s in (-0.012, 0.012)] for t in (t0 + 0.02, t1 - 0.02)]
    lin_f = [[Vector((cx + (R + esp + 0.0012) * math.cos(a + s), t, cz + (R + esp + 0.0012) * math.sin(a + s)))
              for s in (-0.012, 0.012)] for t in (t0 + 0.02, t1 - 0.02)]
    casca(bm, uv, ("s_quase_preto",) * 4, lin_d, lin_f, fechado=False)
    # as duas voltas de fita: por cima do cano na frente, e por cima do envelope atrás
    larg = 0.03
    for tc in (t0 + 0.06, t1 - 0.045):
        i = max(0, min(len(ts) - 2, int((tc - ts[0]) / (ts[1] - ts[0]))))
        f = (tc - ts[i]) / (ts[i + 1] - ts[i])
        rs = [max(raios[i][k], raios[i + 1][k]) for k in range(n)]
        dd, ff = [], []
        volta = arco if so_frente else angs
        for t in (tc - larg / 2, tc + larg / 2):
            di, fo = [], []
            for k, a in enumerate(volta):
                dif = abs(math.atan2(math.sin(a - frente), math.cos(a - frente)))
                r = (R + esp + 0.0004) if (so_frente or dif <= abs(abre) + 1e-6) else rs[k]
                di.append(Vector((cx + r * math.cos(a), t, cz + r * math.sin(a))))
                fo.append(Vector((cx + (r + 0.0028) * math.cos(a), t, cz + (r + 0.0028) * math.sin(a))))
            dd.append(di)
            ff.append(fo)
        casca(bm, uv, ("s_fita_prata",) * 4, dd, ff, fechado=not so_frente)
    return bm, Matrix.Identity(4)


def _y_rim():
    """A altura da borda da panela (m, no mundo): logo acima dos óculos (a linha dos olhos), no mínimo 1,66."""
    zs = [(o.matrix_world @ v.co).z for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Roupa_Oculos")
          for v in o.data.vertices]
    return max(1.66, (max(zs) if zs else 1.69) + 0.008)


def capacete(ctx, tira=True):
    """A panela de ponta-cabeça (no espaço do head: o alto em +Y, a frente em +Z, a esquerda do personagem em +X): as
    paredes com a borda enrolada, o fundo queimado (em cima), a chapinha com dois rebites e o toco do cabo cortado no
    lado direito, e a tira de couro do queixo, presa por dentro da borda, com o nó de corda."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    B = ctx.osso("head")
    tirar = CHAPEUS + tuple(o.name for o in ctx.env.malhas if o.name.startswith("Roupa_Cabelo") and not o.name.endswith("_Chapeu")
                            and o.name != "Roupa_Cabelo_Curto")
    pts = [p for p in ctx.env.todos(ctx.mulher, tirar) if p.z > 1.50 and abs(p.x) < 0.3]
    loc = ctx.no_osso("head", pts)
    # a tira passa por cima da máscara de pano e da barba, mas não dá a volta na máscara de gás (ela fica por baixo)
    loc_tira = ctx.no_osso("head", [p for p in ctx.env.todos(ctx.mulher, tirar + ("Roupa_Mascara_Gas", "Roupa_Respirador"))
                                    if p.z > 1.50 and abs(p.x) < 0.3])
    y_rim = _y_rim() - B.translation.z
    acima = [l for l in loc if l.y >= y_rim - 0.012]
    xs, zs = [l.x for l in acima], [l.z for l in acima]
    cx, cz = (min(xs) + max(xs)) / 2, (min(zs) + max(zs)) / 2
    n = 16
    angs = [2 * math.pi * k / n for k in range(n)]
    R = poligono([(l.x - cx, l.z - cz) for l in acima], angs, 0.006)
    # a panela é redonda: o polígono alisado (cada raio pelo menos a média dos vizinhos) e puxado para o círculo
    for _ in range(3):
        R = [max(R[k], (R[k - 1] + R[(k + 1) % n]) / 2) for k in range(n)]
    Rm = max(R)
    R = [0.55 * r + 0.45 * Rm for r in R]
    topo = max(l.y for l in loc) + 0.006
    esp, fundo = 0.004, 0.006

    def anel(y, d=0.0, esc=1.0):
        return [Vector((cx + (R[k] * esc + d) * math.cos(a), y, cz + (R[k] * esc + d) * math.sin(a))) for k, a in enumerate(angs)]
    # as paredes (abertas embaixo) e o fundo
    alt = [y_rim, (y_rim + topo) / 2, topo]
    casca(bm, uv, ("s_galvanizado", "s_metal_claro", "s_metal_claro"), [anel(y) for y in alt], [anel(y, esp) for y in alt])
    # o fundo (em cima): a quina chanfrada (a parede entra 8% até o fundo) e o fundo chato, com a fuligem de um lado
    cima = [Vector((cx + (R[k] + esp) * math.cos(a), topo, cz + (R[k] + esp) * math.sin(a))) for k, a in enumerate(angs)]
    meio_v = [Vector((cx + (R[k] * 0.92 + esp) * math.cos(a), topo + fundo, cz + (R[k] * 0.92 + esp) * math.sin(a)))
              for k, a in enumerate(angs)]
    vc = [bm.verts.new(p) for p in cima]
    vm = [bm.verts.new(p) for p in meio_v]
    vb = [bm.verts.new(Vector((cx + R[k] * math.cos(a), topo, cz + R[k] * math.sin(a)))) for k, a in enumerate(angs)]
    fs = [bm.faces.new(vm)] + [bm.faces.new((vc[k], vc[(k + 1) % n], vm[(k + 1) % n], vm[k])) for k in range(n)]
    fs += [bm.faces.new(vb[::-1])] + [bm.faces.new((vb[k], vb[(k + 1) % n], vc[(k + 1) % n], vc[k])) for k in range(n)]
    eq._pintar(bm, uv, fs, "s_galvanizado")
    for dx, dz, tam, cor in ((0.05, -0.02, (0.09, 0.07), "s_metal_escuro"), (0.07, 0.03, (0.05, 0.05), "s_metal_escuro"),
                             (-0.06, 0.05, (0.03, 0.02), "s_ferrugem")):
        caixa(bm, uv, cor, Vector((cx + dx, topo + fundo + 0.0006, cz + dz)), (Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0))),
              (tam[0], tam[1], 0.0012))
    # a borda enrolada (embaixo, mais grossa)
    casca(bm, uv, ("s_metal_claro",) * 3, [anel(y_rim - 0.002), anel(y_rim + 0.010)],
          [anel(y_rim - 0.002, esp + 0.005), anel(y_rim + 0.010, esp + 0.005)])
    # as manchas de ferrugem na parede (3 placas finas coladas)
    for k, (yy, alto) in ((3, (0.35, 0.03)), (9, (0.55, 0.025)), (13, (0.25, 0.02))):
        a = angs[k] + 0.5 * (2 * math.pi / n)
        rr = (R[k] + R[(k + 1) % n]) / 2 * math.cos(math.pi / n) + esp + 0.0004
        c = Vector((cx + rr * math.cos(a), y_rim + (topo - y_rim) * yy, cz + rr * math.sin(a)))
        rad = Vector((math.cos(a), 0, math.sin(a)))
        tg = Vector((-math.sin(a), 0, math.cos(a)))
        caixa(bm, uv, "s_ferrugem" if k != 9 else "s_ferrugem_escura", c, (tg, Vector((0, 1, 0)), rad), (0.03, alto, 0.0012))
    # o toco do cabo, no lado direito do personagem (-X): a chapinha, dois rebites, o cabo de baquelite e a ponta cortada
    a = math.pi
    k = n // 2
    rr = R[k] + esp
    yc = y_rim + (topo - y_rim) * 0.52
    base = Vector((cx + rr * math.cos(a), yc, cz + rr * math.sin(a)))
    rad = Vector((math.cos(a), 0, math.sin(a)))
    caixa(bm, uv, "s_metal_claro", base + rad * 0.003, (Vector((0, 0, 1)), Vector((0, 1, 0)), rad), (0.05, 0.03, 0.006))
    for dz in (-0.015, 0.015):
        cilindro(bm, uv, "s_metal", base + rad * 0.005 + Vector((0, 0, dz)), base + rad * 0.0085 + Vector((0, 0, dz)), 0.004, 6)
    ini = base + rad * 0.006
    fim = ini + rad * 0.034 + Vector((0, -0.006, 0))
    cilindro(bm, uv, "s_borracha", ini, fim, 0.011, 8)
    cilindro(bm, uv, "s_metal_claro", fim - rad * 0.0005, fim + rad * 0.0025, 0.0085, 8)
    if not tira:
        return bm, Matrix.Identity(4)
    # a tira do queixo: a volta esticada em volta da cabeça num plano inclinado (para baixo e para a frente), da borda de
    # um lado até a do outro por baixo do queixo
    baixo = Vector((0, -1, 0.42)).normalized()
    E = Vector((cx, y_rim + 0.02, cz + 0.015))
    nrm = Vector((1, 0, 0)).cross(baixo).normalized()
    perto = [l for l in loc_tira if abs((l - E).dot(nrm)) < 0.014 and (l - E).dot(baixo) > -0.03]
    p2 = [((l - E).x, (l - E).dot(baixo)) for l in perto]
    lx = max(abs(x) for x, s in p2 if s < 0.03) + 0.004
    ancora_e, ancora_d = (lx, -0.03), (-lx, -0.03)
    h = _hull(p2 + [ancora_e, ancora_d])
    ie = min(range(len(h)), key=lambda i: (h[i][0] - ancora_e[0]) ** 2 + (h[i][1] - ancora_e[1]) ** 2)
    idd = min(range(len(h)), key=lambda i: (h[i][0] - ancora_d[0]) ** 2 + (h[i][1] - ancora_d[1]) ** 2)
    cad = _caminho_no_hull(h, ie, idd, lambda q: q[1])
    cad = _afastar(cad, (0.0, 0.06), 0.006)
    pts3 = [E + Vector((x, 0, 0)) + baixo * s for x, s in _reamostrar(cad, 18)]
    fita(bm, uv, "p_couro", pts3, 0.022, 0.004, nrm, lambda p: E + baixo * 0.07)
    # o nó (de corda) embaixo do queixo, do lado esquerdo
    q = pts3[11]
    corda(bm, uv, "s_corda", [q + Vector((0.0, -0.004, -0.006)), q + Vector((0.004, -0.012, -0.004)), q + Vector((0.008, -0.018, 0.002))],
          0.004, 5)
    return bm, Matrix.Identity(4)


def colete(ctx):
    """O colete (no mundo, depois levado ao espaço do chest): a chapa pintada na frente (três painéis, os dos lados
    dobrados para trás), com ferrugem, parafusos e um remendo de fita; o pedaço de pneu atrás, com a banda de rodagem; as
    tiras de câmara de ar nos ombros e as cordas nos lados, todas esticadas por cima do envelope das roupas."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    pts = [p for p in ctx.env.todos(ctx.mulher, ("Roupa_Cabelo*", "Roupa_Cinto*") + CHAPEUS)]
    pts = [p for p in pts if 0.98 < p.z < 1.62]
    tronco = [p for p in pts if 1.07 <= p.z <= 1.28 and abs(p.x) < 0.22]
    larg = max(abs(p.x) for p in tronco)
    w = 0.80 * larg
    z0, z1f, z1b = 1.07, 1.40, 1.37
    margem = FOLGA + 0.002
    th_f, th_b = 0.006, 0.014

    def frente_em(xa, xb, za, zb):
        sel = [p.y for p in pts if xa - 0.02 <= p.x <= xb + 0.02 and za - 0.02 <= p.z <= zb + 0.02 and abs(p.x) < 0.24]
        return min(sel)

    def tras_em(xa, xb, za, zb):
        sel = [p.y for p in pts if xa - 0.02 <= p.x <= xb + 0.02 and za - 0.02 <= p.z <= zb + 0.02 and abs(p.x) < 0.24]
        return max(sel)

    def placa(z1, frente):
        """Os 4 nós (x) da placa e o y de cada um (a face de dentro), dobrada nos lados até 30 graus, sem entrar."""
        xm = 0.42 * w
        if frente:
            yc = frente_em(-xm, xm, z0, z1) - margem
        else:
            yc = tras_em(-xm, xm, z0, z1) + margem
        ys = {}
        for s in (-1, 1):
            lado = [p for p in pts if s * p.x > xm - 0.01 and s * p.x <= w + 0.02 and z0 - 0.02 <= p.z <= z1 + 0.02 and abs(p.x) < 0.24]
            dob = math.tan(math.radians(30)) * (w - xm)
            yl = yc + dob if frente else yc - dob
            for p in lado:
                f = (abs(p.x) - xm) / (w - xm)
                if f <= 0.02:
                    continue
                if frente:
                    lim = yc + (p.y - margem - yc) / f
                    yl = min(yl, lim)
                else:
                    lim = yc + (p.y + margem - yc) / f
                    yl = max(yl, lim)
            ys[s] = yl
        return [(-w, ys[-1]), (-xm, yc), (xm, yc), (w, ys[1])]

    nos_f = placa(z1f, True)
    nos_b = placa(z1b, False)

    def painel(nos, z1, d0, d1):
        """A placa como casca: o anel de baixo e o de cima (os 4 nós), de d0 a d1 (para fora, -y na frente)."""
        dentro = [[Vector((x, y + d0, z)) for x, y in nos] for z in (z0, z1)]
        fora = [[Vector((x, y + d1, z)) for x, y in nos] for z in (z0, z1)]
        return dentro, fora
    d, f = painel(nos_f, z1f, 0.0, -th_f)
    casca(bm, uv, ("s_pintura_verde", "s_ferrugem_escura", "s_ferrugem", "s_ferrugem"), d, f, fechado=False)
    d, f = painel(nos_b, z1b, 0.0, th_b)
    casca(bm, uv, ("s_borracha", "s_borracha", "s_quase_preto", "s_quase_preto"), d, f, fechado=False)

    def y_na_placa(nos, x):
        for (xa, ya), (xb, yb) in zip(nos, nos[1:]):
            if xa - 1e-6 <= x <= xb + 1e-6:
                return ya + (yb - ya) * (x - xa) / (xb - xa)
        return nos[0][1] if x < nos[0][0] else nos[-1][1]

    def colado(nos, x, z, d, frente=True):
        """Um ponto na face de fora da placa (x, z), d para fora dela, e a normal (para fora) ali."""
        for (xa, ya), (xb, yb) in zip(nos, nos[1:]):
            if xa - 1e-6 <= x <= xb + 1e-6:
                t = Vector((xb - xa, yb - ya, 0)).normalized()
                nn = Vector((t.y, -t.x, 0)) if frente else Vector((-t.y, t.x, 0))
                y = ya + (yb - ya) * (x - xa) / (xb - xa)
                return Vector((x, y, z)) + nn * d, nn, t
        return Vector((x, nos[-1][1], z)), Vector((0, -1 if frente else 1, 0)), Vector((1, 0, 0))
    # a chapa: as manchas de ferrugem, os 4 parafusos dos cantos e o remendo de fita prata em X
    for x, z, tam, cor in ((-0.06, 1.13, (0.07, 0.05), "s_ferrugem"), (0.09, 1.31, (0.05, 0.06), "s_ferrugem"),
                           (0.02, 1.22, (0.03, 0.02), "s_ferrugem_escura"), (-0.11, 1.35, (0.04, 0.03), "s_ferrugem_escura")):
        c, nn, t = colado(nos_f, x, z, th_f + 0.0006)
        caixa(bm, uv, cor, c, (t, Vector((0, 0, 1)), nn), (tam[0], tam[1], 0.0012))
    for x in (-0.82 * w, 0.82 * w):
        for z in (z0 + 0.03, z1f - 0.03):
            c, nn, t = colado(nos_f, x, z, th_f)
            cilindro(bm, uv, "s_metal_claro", c, c + nn * 0.004, 0.006, 6)
    c, nn, t = colado(nos_f, 0.05, 1.17, th_f + 0.0012)
    for g in (35, -35):
        r = Matrix.Rotation(math.radians(g), 3, nn)
        caixa(bm, uv, "s_fita_prata", c, (r @ t, r @ Vector((0, 0, 1)), nn), (0.075, 0.02, 0.0014))
    # o pneu: os blocos da banda de rodagem, em duas fileiras, com o sulco no meio
    for x in [-0.8 * w + k * (1.6 * w / 5) for k in range(6)]:
        for z, dz in ((1.13, 0.0), (1.3, 0.02)):
            c, nn, t = colado(nos_b, x + (0.01 if dz else -0.01), z, th_b + 0.003, frente=False)
            caixa(bm, uv, "s_borracha", c, (t, Vector((0, 0, 1)), nn), (0.04, 0.09, 0.006))

    # as tiras dos ombros: por cima do envelope no plano (y, z), em x = ±xo
    xo = 0.115
    largura_tira, esp_tira = 0.042, 0.007
    for s in (-1, 1):
        x = s * xo
        # (só o ombro e o capuz deitado: a cabeça e o pescoço ficam por dentro, acima de 1,53 m)
        perto = [(p.y, p.z) for p in pts if abs(p.x - x) < 0.028 and 1.30 < p.z < 1.53]
        yf = y_na_placa(nos_f, x) - th_f
        yb = y_na_placa(nos_b, x) + th_b
        ancora_f, ancora_b = (yf - 0.002, z1f - 0.06), (yb + 0.002, z1b - 0.06)
        cant_f, cant_b = (yf + th_f, z1f + 0.003), (yb - th_b, z1b + 0.003)
        h = _hull(perto + [ancora_f, ancora_b, cant_f, cant_b])
        i_f = min(range(len(h)), key=lambda i: (h[i][0] - ancora_f[0]) ** 2 + (h[i][1] - ancora_f[1]) ** 2)
        i_b = min(range(len(h)), key=lambda i: (h[i][0] - ancora_b[0]) ** 2 + (h[i][1] - ancora_b[1]) ** 2)
        cad = _caminho_no_hull(h, i_f, i_b, lambda q: q[1])
        cad = _afastar(cad, (0.0, 1.2), esp_tira / 2 + 0.002)
        p3 = [Vector((x, yy, zz)) for yy, zz in _reamostrar(cad, 16)]
        fita(bm, uv, "s_borracha", p3, largura_tira, esp_tira, (1, 0, 0), Vector((x, 0.0, 1.2)))
        # os parafusos que prendem a tira nas placas
        for p, nn in ((p3[0], Vector((0, -1, 0))), (p3[-1], Vector((0, 1, 0)))):
            cilindro(bm, uv, "s_metal_claro", p, p + nn * (esp_tira / 2 + 0.003), 0.007, 6)
    # as cordas dos lados: por cima do envelope no plano (x, y), em duas alturas
    for zc in (1.12, 1.25):
        for s in (-1, 1):
            perto = [(p.x * s, p.y) for p in pts if abs(p.z - zc) < 0.025 and p.x * s > 0.02]
            xf, yf = nos_f[-1] if s > 0 else (-nos_f[0][0], nos_f[0][1])
            xb, yb = nos_b[-1] if s > 0 else (-nos_b[0][0], nos_b[0][1])
            ancora_f = (xf - 0.03, yf - th_f - 0.004)
            ancora_b = (xb - 0.03, yb + th_b + 0.004)
            h = _hull(perto + [ancora_f, ancora_b, (xf + 0.002, yf - th_f), (xb + 0.002, yb + th_b)])
            i_f = min(range(len(h)), key=lambda i: (h[i][0] - ancora_f[0]) ** 2 + (h[i][1] - ancora_f[1]) ** 2)
            i_b = min(range(len(h)), key=lambda i: (h[i][0] - ancora_b[0]) ** 2 + (h[i][1] - ancora_b[1]) ** 2)
            cad = _caminho_no_hull(h, i_f, i_b, lambda q: q[0])
            cad = _afastar(cad, (0.0, 0.0), 0.0075)
            p3 = [Vector((xx * s, yy, zc)) for xx, yy in _reamostrar(cad, 12)]
            corda(bm, uv, "s_corda", p3, 0.0055, 6)
            # os nós nas pontas
            for p in (p3[0], p3[-1]):
                cilindro(bm, uv, "s_corda", p - Vector((0, 0, 0.009)), p + Vector((0, 0, 0.009)), 0.009, 6)
    return bm, ctx.osso("chest").inverted()


CONSTRUTORES = {
    "Protecao_Capacete": capacete,
    "Protecao_Bracadeira.L": lambda c: bracadeira(c, "L"),
    "Protecao_Bracadeira.R": lambda c: bracadeira(c, "R"),
    "Protecao_Caneleira.L": lambda c: caneleira(c, "L"),
    "Protecao_Caneleira.R": lambda c: caneleira(c, "R"),
    "Protecao_Colete": colete,
}


# ================================================================================================ a cena
def importar(poses=True):
    """O Roupas.fbx (o corpo, as roupas e o esqueleto, no repouso) e as poses do Personagem_PrincipalBloco.fbx (só as
    ações: Parado, Maos_Guarda e Agachado)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    acoes = {}
    if poses and os.path.exists(CORPO):
        bpy.ops.import_scene.fbx(filepath=CORPO)
        for a in bpy.data.actions:
            nome = a.name.split("|")[-1]
            if nome in ("Parado", "Maos_Guarda", "Agachado") and a.name.startswith(RIG):
                a.use_fake_user = True
                acoes[nome] = a
        for o in list(bpy.data.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        for a in list(bpy.data.actions):
            if a not in acoes.values():
                bpy.data.actions.remove(a)
    bpy.ops.import_scene.fbx(filepath=ROUPAS)
    # a Paleta.png que vem nos FBX aponta para a máquina de quem fez a arte: a do projeto no lugar
    for img in bpy.data.images:
        if img.name.startswith("Paleta"):
            img.filepath = eq.PALETA
            img.reload()
    rig = bpy.data.objects[RIG]
    rig.animation_data_create()
    return rig, acoes


def _bm_para_malha(bm, M, nome):
    """O bmesh (no espaço em que foi feito) levado por M ao espaço do osso: a malha da peça."""
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(nome)
    bm.to_mesh(me)
    bm.free()
    for f in me.polygons:
        f.use_smooth = False
    me.materials.append(eq._material())
    return me


def construir(env, rig):
    """As peças: a malha do homem com a forma "Mulher" (a mesma topologia, medida no corpo da mulher)."""
    pecas = {}
    for nome, osso in PECAS:
        bm_h, M = CONSTRUTORES[nome](Ctx(env, rig, False))
        bm_m, _ = CONSTRUTORES[nome](Ctx(env, rig, True))
        me = _bm_para_malha(bm_h, M, nome)
        me_m = _bm_para_malha(bm_m, M, nome + "_m")
        o = bpy.data.objects.new(nome, me)
        bpy.context.scene.collection.objects.link(o)
        if len(me_m.vertices) == len(me.vertices):
            o.shape_key_add(name="Basis")
            k = o.shape_key_add(name="Mulher")
            for i, v in enumerate(me_m.vertices):
                k.data[i].co = v.co
        else:
            print(f"  AVISO {nome}: a mulher saiu com outra topologia ({len(me_m.vertices)} x {len(me.vertices)}); sem a forma")
        bpy.data.meshes.remove(me_m)
        o["osso"] = osso
        pecas[nome] = o
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        vs = [v.co for v in me.vertices]
        tam = [max(v[i] for v in vs) - min(v[i] for v in vs) for i in range(3)]
        print(f"  {nome:24s} {osso:10s} {tris:5d} triângulos; {tam[0]:.3f} x {tam[1]:.3f} x {tam[2]:.3f} m (no osso)")
    return pecas


def exportar(pecas, rig, pasta):
    """Protecao_Sucata.fbx (cada peça girada 90° em X, com a malha nos eixos da Unity: o espaço do osso dela) e o JSON."""
    copias = []
    for nome, o in pecas.items():
        o.name = nome + "_autoria"
        me = o.data.copy()
        # a malha (no espaço do osso, eixos do Blender) já é a do osso na Unity (o X espelhado pelo próprio FBX); o objeto
        # girado 90° em X e a malha girada -90° (com a forma "Mulher"), como no Loot.fbx: o nó do FBX fica sem giro
        me.transform(Matrix.Rotation(math.radians(-90), 4, "X"), shape_keys=True)
        c = bpy.data.objects.new(nome, me)
        c.rotation_euler = (math.radians(90), 0, 0)
        bpy.context.scene.collection.objects.link(c)
        copias.append((c, o, nome))
    for ob in bpy.context.scene.objects:
        ob.select_set(any(ob is c for c, _, _ in copias))
    bpy.context.view_layer.objects.active = copias[0][0]
    caminho = os.path.join(pasta, "Protecao_Sucata.fbx")
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             bake_anim=False, mesh_smooth_type="FACE", use_mesh_modifiers=False)
    for c, o, nome in copias:
        bpy.data.objects.remove(c, do_unlink=True)
        o.name = nome
    print("protecao: gravado", caminho)
    info = {
        "arquivo": "Protecao_Sucata.fbx",
        "convencao": ("Cada malha está no espaço do osso dela no repouso do PrincipalBloco_Rig (o do Roupas.fbx e do "
                      "Personagem_PrincipalBloco.fbx): a origem na cabeça do osso, os eixos os do osso como a Unity os vê. "
                      "Na Unity: a peça filha do osso, localPosition (0,0,0), localRotation identidade, localScale (1,1,1). "
                      "A forma 'Mulher' (blend shape) vai a 100 junto com a do corpo e das roupas (SkinnedMeshRenderer sem "
                      "ossos, ou só a malha do homem num MeshRenderer)."),
        "material": "Pistola (a Paleta.png, o dos itens); o UV de cada face no meio da célula da cor",
        "pecas": {},
    }
    for nome, o in pecas.items():
        b = rig.data.bones[o["osso"]]
        q = b.matrix_local.to_quaternion()
        vs = [v.co for v in o.data.vertices]
        info["pecas"][nome] = {
            "osso": o["osso"],
            "triangulos": sum(len(p.vertices) - 2 for p in o.data.polygons),
            "forma_mulher": bool(o.data.shape_keys),
            "caixa_no_osso_unity_m": {"min": [round(-max(v.x for v in vs), 4), round(min(v.y for v in vs), 4), round(min(v.z for v in vs), 4)],
                                      "max": [round(-min(v.x for v in vs), 4), round(max(v.y for v in vs), 4), round(max(v.z for v in vs), 4)]},
            "osso_no_repouso_blender": {"cabeca": [round(c, 4) for c in b.head_local], "cauda": [round(c, 4) for c in b.tail_local],
                                        "giro_wxyz": [round(c, 5) for c in q]},
        }
    with open(os.path.join(pasta, "Protecao_Sucata.json"), "w", encoding="utf-8") as arq:
        json.dump(info, arq, indent=1, ensure_ascii=False)
    return info


# ================================================================================================ conferir e fotos
def _usar(rig, a):
    ad = rig.animation_data
    ad.action = a
    if hasattr(ad, "action_slot") and a is not None and len(a.slots):
        ad.action_slot = a.slots[0]


def _por_no_osso(pecas, rig, mulher):
    for o in pecas.values():
        pb = rig.pose.bones[o["osso"]]
        o.matrix_world = rig.matrix_world @ pb.matrix
        if o.data.shape_keys:
            o.data.shape_keys.key_blocks["Mulher"].value = 1.0 if mulher else 0.0


ROUPAS_FOTOS = {
    "homem_leve": (False, ["NPC_Corpo", "Roupa_Camiseta", "Roupa_Jeans", "Roupa_Tenis", "Roupa_Cabelo_Curto"]),
    "homem_jaqueta": (False, ["NPC_Corpo", "Roupa_Jaqueta_Capuz", "Roupa_Colete_Tatico", "Roupa_Calca_Cargo", "Roupa_Coturno",
                              "Roupa_Luva_Tatica", "Roupa_Cabelo_Curto"]),
    "mulher_leve": (True, ["NPC_Corpo", "Roupa_Regata_Feminina", "Roupa_Jeans", "Roupa_Tenis", "Roupa_Cabelo_Rabo_Chapeu"]),
    "mulher_jaqueta": (True, ["NPC_Corpo", "Roupa_Jaqueta_Lona", "Roupa_Colete_Tatico", "Roupa_Calca_Militar",
                              "Roupa_Bota_Cauboi", "Roupa_Cabelo_Coque_Chapeu"]),
}
CORES_ROUPA = {"NPC_Corpo": (0.83, 0.62, 0.50), "Camiseta": (0.85, 0.85, 0.8), "Regata": (0.8, 0.8, 0.75),
               "Jeans": (0.23, 0.30, 0.45), "Tenis": (0.9, 0.9, 0.9), "Cabelo": (0.2, 0.13, 0.08),
               "Jaqueta": (0.45, 0.32, 0.2), "Colete": (0.25, 0.28, 0.2), "Calca": (0.33, 0.33, 0.26),
               "Coturno": (0.12, 0.12, 0.12), "Bota": (0.35, 0.2, 0.1), "Luva": (0.15, 0.15, 0.15)}


def _cor_roupa(nome):
    for k, c in CORES_ROUPA.items():
        if k in nome:
            return c
    return (0.6, 0.6, 0.6)


# as roupas que não contam na conferência de cada peça (as mesmas da medida: os chapéus e o cabelo comprido no capacete,
# a máscara de gás por baixo da tira, o jaleco no joelho)
TIRAR_NA_CONFERENCIA = {
    "head": CHAPEUS + ("Roupa_Mascara_Gas", "Roupa_Respirador", "Roupa_Cabelo_Cacheado", "Roupa_Cabelo_Coque",
                       "Roupa_Cabelo_Para_Tras", "Roupa_Cabelo_Penteado", "Roupa_Cabelo_Rabo", "Roupa_Cabelo_Rabo_Baixo"),
    "chest": ("Roupa_Cabelo*", "Roupa_Cinto*") + CHAPEUS,
    "shin": ("Roupa_Jaleco",),
}


def conferir(pecas, rig, acoes, env):
    """Em cada pose (o repouso, Parado, Maos_Guarda e Agachado, no quadro do meio) e em cada corpo (o homem e a mulher), os
    pontos do corpo e das roupas que furam a peça: o raio do eixo da peça até o ponto atravessa a malha dela, e o ponto
    está até 2,5 cm além (na parede ou logo depois; mais longe é outra parte do corpo, como o quadril ao lado do braço).
    Imprime quantos e o quanto (o pior, em cm), peça por peça."""
    rel = {}
    for pose in ["Repouso"] + [p for p in ("Parado", "Maos_Guarda", "Agachado") if p in acoes]:
        _usar(rig, acoes.get(pose))
        if pose in acoes:
            f0, f1 = acoes[pose].frame_range
            bpy.context.scene.frame_set(int((f0 + f1) / 2))
        else:
            bpy.context.scene.frame_set(1)
        bpy.context.view_layer.update()
        for mulher in (False, True):
            _por_no_osso(pecas, rig, mulher)
            bpy.context.view_layer.update()
            pts_de = {o.name: _pontos(o, mulher) for o in env.malhas}
            for nome, o in pecas.items():
                tirar = TIRAR_NA_CONFERENCIA.get(o["osso"].split(".")[0], ())
                pts = [p for k, vs in pts_de.items() if not (k in tirar or any(k.startswith(t[:-1]) for t in tirar if t.endswith("*")))
                       for p in vs]
                dg = bpy.context.evaluated_depsgraph_get()
                e = o.evaluated_get(dg)
                me = e.to_mesh()
                M = o.matrix_world
                vs = [M @ v.co for v in me.vertices]
                arvore = BVHTree.FromPolygons(vs, [tuple(p.vertices) for p in me.polygons])
                lo = Vector([min(v[i] for v in vs) - 0.01 for i in range(3)])
                hi = Vector([max(v[i] for v in vs) + 0.01 for i in range(3)])
                e.to_mesh_clear()
                pb = rig.pose.bones[o["osso"]]
                Mb = rig.matrix_world @ pb.matrix
                eixo_o = Mb.translation
                eixo_d = (Mb.to_3x3() @ Vector((0, 1, 0))).normalized()
                n_fora, pior = 0, 0.0
                for p in pts:
                    if not all(lo[i] <= p[i] <= hi[i] for i in range(3)):
                        continue
                    o0 = eixo_o + eixo_d * (p - eixo_o).dot(eixo_d)
                    d = p - o0
                    L = d.length
                    if L < 1e-4:
                        continue
                    hit = arvore.ray_cast(o0, d / L, L)
                    if hit[0] is not None and L - 0.025 < hit[3] < L - 0.003:
                        n_fora += 1
                        pior = max(pior, L - hit[3])
                rel[f"{pose}|{'mulher' if mulher else 'homem'}|{nome}"] = (n_fora, round(pior * 100, 1))
    for k, (n, p) in rel.items():
        if n:
            print(f"  conferir: {k}: {n} pontos para fora, o pior {p} cm")
    print(f"  conferir: {sum(1 for v in rel.values() if v[0] == 0)} de {len(rel)} sem nenhum ponto para fora")
    return rel


def _estilo_fotos(cena):
    r = cena.render
    try:
        r.engine = "BLENDER_WORKBENCH"
    except TypeError:
        pass
    r.film_transparent = False
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGB"
    s = cena.display.shading
    s.light = "STUDIO"
    s.color_type = "TEXTURE"
    s.show_cavity = True
    s.cavity_type = "BOTH"
    s.show_object_outline = True
    s.object_outline_color = (0.05, 0.05, 0.05)
    s.background_type = "VIEWPORT"
    s.background_color = (0.62, 0.66, 0.70)
    cena.view_settings.view_transform = "Standard"
    return r


def fotos(pecas, rig, acoes, pasta):
    """As peças no homem e na mulher, com e sem jaqueta: de frente e de lado (Parado), na guarda (de 3/4, o tronco) e
    agachado (de lado)."""
    os.makedirs(pasta, exist_ok=True)
    cena = bpy.context.scene
    r = _estilo_fotos(cena)
    # as roupas em cores chapadas (as texturas do projeto de arte não vêm no FBX)
    for o in bpy.data.objects:
        if o.type == "MESH" and (o.name == "NPC_Corpo" or o.name.startswith("Roupa_")):
            for s in o.material_slots:
                m = s.material
                if m is None:
                    continue
                if m.use_nodes:
                    for nd in list(m.node_tree.nodes):
                        if nd.type == "TEX_IMAGE":
                            m.node_tree.nodes.remove(nd)
                c = _cor_roupa(o.name)
                m.diffuse_color = (*c, 1.0)
    cam = bpy.data.objects.new("CamFotos", bpy.data.cameras.new("CamFotos"))
    cena.collection.objects.link(cam)
    cena.camera = cam
    cam.data.type = "ORTHO"
    saidas = []
    vistas = [("frente", "Parado", (0, -1, 0.05), 2.0, (0, 0, 0.92), (520, 640)),
              ("lado", "Parado", (1, -0.25, 0.05), 2.0, (0, 0, 0.92), (520, 640)),
              ("guarda", "Maos_Guarda", (0.75, -1, 0.25), 1.05, (0, -0.05, 1.32), (520, 520)),
              ("agachado", "Agachado", (1, -0.35, 0.12), 1.7, (0, 0, 0.7), (520, 640)),
              ("cabeca", "Parado", (0.8, -1, 0.15), 0.55, None, (420, 420)),
              ("costas", "Parado", (-0.6, 1, 0.2), 1.1, None, (420, 420)),
              ("braco", "Maos_Guarda", (0.2, -1, -0.1), 0.6, None, (420, 420)),
              ("perna", "Agachado", (1, -0.6, 0.0), 0.75, None, (420, 420))]
    for roupa, (mulher, nomes) in ROUPAS_FOTOS.items():
        for o in bpy.data.objects:
            if o.type == "MESH" and (o.name == "NPC_Corpo" or o.name.startswith("Roupa_")):
                o.hide_render = o.name not in nomes
                if o.data.shape_keys and "Mulher" in o.data.shape_keys.key_blocks:
                    o.data.shape_keys.key_blocks["Mulher"].value = 1.0 if mulher else 0.0
        for vista, pose, direcao, escala, alvo, res in vistas:
            a = acoes.get(pose)
            _usar(rig, a)
            if a is not None:
                f0, f1 = a.frame_range
                cena.frame_set(int((f0 + f1) / 2))
            bpy.context.view_layer.update()
            _por_no_osso(pecas, rig, mulher)
            bpy.context.view_layer.update()
            alvo_v = Vector(alvo) if alvo else Vector()
            if vista == "guarda":
                alvo_v = (rig.matrix_world @ rig.pose.bones["chest"].matrix).translation + Vector((0, -0.08, 0.05))
            elif vista == "agachado":
                alvo_v = (rig.matrix_world @ rig.pose.bones["hips"].matrix).translation + Vector((0, 0, 0.12))
            elif vista == "cabeca":
                alvo_v = (rig.matrix_world @ rig.pose.bones["head"].matrix).translation + Vector((0, 0, 0.12))
            elif vista == "costas":
                alvo_v = (rig.matrix_world @ rig.pose.bones["chest"].matrix).translation + Vector((0, 0, 0.05))
            elif vista == "braco":
                pb = rig.pose.bones["forearm.R"]
                alvo_v = rig.matrix_world @ ((pb.head + pb.tail) / 2)
            elif vista == "perna":
                pb = rig.pose.bones["shin.L"]
                alvo_v = rig.matrix_world @ ((pb.head + pb.tail) / 2)
            d = Vector(direcao).normalized()
            cam.location = alvo_v + d * 4.0
            cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
            cam.data.ortho_scale = escala
            r.resolution_x, r.resolution_y = res
            r.filepath = os.path.join(pasta, f"protecao_{roupa}_{vista}.png")
            bpy.ops.render.render(write_still=True)
            saidas.append(r.filepath)
    print("protecao: fotos em", pasta)
    return saidas


def rodar(etapas, pasta):
    import traceback
    if "tudo" in etapas:
        etapas = ["medir", "construir", "exportar", "conferir", "fotos"]
    try:
        os.makedirs(pasta, exist_ok=True)
        rig, acoes = importar()
        env = Envelope(rig)
        print(f"protecao: {len(env.malhas)} malhas no envelope; as poses: {sorted(acoes)}; a borda da panela em {_y_rim():.3f} m")
        pecas = construir(env, rig)
        if "exportar" in etapas:
            exportar(pecas, rig, pasta)
        if "conferir" in etapas:
            rel = conferir(pecas, rig, acoes, env)
            with open(os.path.join(pasta, "Protecao_Sucata_conferir.json"), "w", encoding="utf-8") as arq:
                json.dump(rel, arq, indent=0)
        if "fotos" in etapas:
            fotos(pecas, rig, acoes, os.path.join(pasta, "fotos"))
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
