# A fauna (12/11/2026): as formas dos bichos.
#
# Cada bicho é feito de "peças": tubos de anéis (o tronco, o pescoço, a cabeça, as pernas, a cauda) nas medidas reais da
# espécie. Cada peça vira um campo de distância (o openvdb do Blender), as peças se juntam com a união suave (as emendas
# arredondadas, como músculo e pele) e a malha sai do campo e é reduzida ao padrão do jogo. As peças finas (as orelhas,
# a galhada, os cascos e os olhos) não entram no campo: são malhas baixas à parte, juntadas no fim.
#
# As medidas das espécies são escritas em (f, s, z): f para a frente do bicho, s para o lado esquerdo dele e z para cima,
# em metros, com a origem no chão, embaixo do meio do corpo. No Blender, o bicho olha para -Y (a frente da Unity, +Z,
# depois do FBX), então x = s, y = -f, z = z.

import math

import numpy as np

try:
    import openvdb as vdb
except ImportError:  # fora do Blender
    vdb = None


def P(f, s, z):
    return np.array((s, -f, z), float)


# ---------------------------------------------------------------------------------------------------------------------
# Os tubos de anéis
# ---------------------------------------------------------------------------------------------------------------------

def _catmull(p0, p1, p2, p3, t):
    t2, t3 = t * t, t * t * t
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)


def interpolar(valores, sub):
    """Uma curva de Catmull-Rom pelas linhas de `valores` (n, k), com `sub` pontos por vão."""
    v = np.asarray(valores, float)
    if len(v) < 2:
        return v
    ext = np.vstack([2 * v[0] - v[1], v, 2 * v[-1] - v[-2]])
    out = []
    for i in range(len(v) - 1):
        for j in range(sub):
            out.append(_catmull(ext[i], ext[i + 1], ext[i + 2], ext[i + 3], j / sub))
    out.append(v[-1])
    return np.array(out)


def _normal(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-12 else v


class Anel:
    """Um anel do tubo: o meio (já no espaço do Blender), a meia largura de lado (rx) e a meia altura (ru, para o lado
    "de cima" do tubo: o dorso no tronco, a frente nas pernas)."""

    def __init__(self, c, rx, ru, p=2.0, qb=0.0, qt=0.0, du=0.0):
        self.c = np.asarray(c, float)
        self.rx, self.ru = float(rx), float(ru)
        self.p = float(p)      # 2 = elipse; maior = mais quadrado
        self.qb = float(qb)    # o estreitamento embaixo (a quilha do peito)
        self.qt = float(qt)    # o estreitamento em cima (o lombo)
        self.du = float(du)    # o meio deslocado para cima (+) ou para baixo (-), em fração de ru

    def vetor(self):
        return np.array([*self.c, self.rx, self.ru, self.p, self.qb, self.qt, self.du])


def tubo(aneis, lados=24, sub=4, tampas=True, lateral=(1.0, 0.0, 0.0), tampa_ext=0.8, ntampa=3):
    """Os vértices e as faces (quads e triângulos) de um tubo fechado pelos anéis."""
    vet = interpolar([a.vetor() for a in aneis], sub)
    n = len(vet)
    cs = vet[:, :3]
    lat = np.asarray(lateral, float)
    verts, quads, tris = [], [], []
    ts = np.linspace(0, 2 * math.pi, lados, endpoint=False)
    cos, sin = np.cos(ts), np.sin(ts)

    def quadro(i):
        if i == 0:
            T = cs[1] - cs[0]
        elif i == n - 1:
            T = cs[-1] - cs[-2]
        else:
            T = cs[i + 1] - cs[i - 1]
        T = _normal(T)
        L = _normal(lat - T * np.dot(lat, T))
        U = _normal(np.cross(T, L))
        return T, L, U

    def secao(i, escala=1.0, avanco=0.0):
        c, rx, ru, p, qb, qt, du = vet[i, :3], *vet[i, 3:]
        T, L, U = quadro(i)
        e = 2.0 / max(p, 0.5)
        sx = np.sign(cos) * np.abs(cos) ** e
        su = np.sign(sin) * np.abs(sin) ** e
        w = 1.0 - qb * np.clip(-su, 0, 1) ** 1.5 - qt * np.clip(su, 0, 1) ** 1.5
        meio = c + U * ru * du + T * avanco
        return meio + np.outer(sx * w * rx * escala, L) + np.outer(su * ru * escala, U)

    aneis_v = []
    if tampas:
        T0, _, _ = quadro(0)
        r0 = min(vet[0, 3], vet[0, 4])
        for k in range(ntampa, 0, -1):
            ang = k / (ntampa + 1) * (math.pi / 2)
            aneis_v.append(secao(0, math.cos(ang), -tampa_ext * r0 * math.sin(ang)))
    for i in range(n):
        aneis_v.append(secao(i))
    if tampas:
        r1 = min(vet[-1, 3], vet[-1, 4])
        for k in range(1, ntampa + 1):
            ang = k / (ntampa + 1) * (math.pi / 2)
            aneis_v.append(secao(n - 1, math.cos(ang), tampa_ext * r1 * math.sin(ang)))
    m = len(aneis_v)
    for a in aneis_v:
        verts.extend(a)
    for i in range(m - 1):
        for j in range(lados):
            a = i * lados + j
            b = i * lados + (j + 1) % lados
            quads.append((a, a + lados, b + lados, b))
    if tampas:
        T0, _, _ = quadro(0)
        T1, _, _ = quadro(n - 1)
        p0 = cs[0] - T0 * tampa_ext * min(vet[0, 3], vet[0, 4])
        p1 = cs[-1] + T1 * tampa_ext * min(vet[-1, 3], vet[-1, 4])
        i0 = len(verts)
        verts.append(p0)
        i1 = len(verts)
        verts.append(p1)
        for j in range(lados):
            tris.append((i0, (j + 1) % lados, j))
            base = (m - 1) * lados
            tris.append((i1, base + j, base + (j + 1) % lados))
    return np.array(verts, np.float32), np.array(quads, np.int32).reshape(-1, 4), np.array(tris, np.int32).reshape(-1, 3)


def elipsoide(c, rx, ry, rz, lados=20, faixas=12):
    """Um elipsóide (nos eixos do Blender), fechado."""
    c = np.asarray(c, float)
    verts = [c + (0, 0, -rz)]
    for i in range(1, faixas):
        a = -math.pi / 2 + math.pi * i / faixas
        for j in range(lados):
            b = 2 * math.pi * j / lados
            verts.append(c + (rx * math.cos(a) * math.cos(b), ry * math.cos(a) * math.sin(b), rz * math.sin(a)))
    verts.append(c + (0, 0, rz))
    quads, tris = [], []
    for j in range(lados):
        tris.append((0, 1 + (j + 1) % lados, 1 + j))
    for i in range(faixas - 2):
        for j in range(lados):
            a = 1 + i * lados + j
            b = 1 + i * lados + (j + 1) % lados
            quads.append((a, b, b + lados, a + lados))
    topo = len(verts) - 1
    base = 1 + (faixas - 2) * lados
    for j in range(lados):
        tris.append((topo, base + j, base + (j + 1) % lados))
    return np.array(verts, np.float32), np.array(quads, np.int32).reshape(-1, 4), np.array(tris, np.int32).reshape(-1, 3)


# ---------------------------------------------------------------------------------------------------------------------
# Os campos de distância
# ---------------------------------------------------------------------------------------------------------------------

class Campo:
    """O campo de distância de uma peça (ou do corpo inteiro), num pedaço da grade: arr[i, j, k] é a distância no voxel
    (i0 + i, j0 + j, k0 + k), negativa dentro."""

    def __init__(self, arr, ijk0, voxel, fundo):
        self.arr = arr
        self.ijk0 = np.asarray(ijk0, int)
        self.voxel = voxel
        self.fundo = fundo

    def amostrar(self, pontos):
        """A distância nos pontos (n, 3), trilinear; fora do pedaço, o fundo."""
        q = np.asarray(pontos, float) / self.voxel - self.ijk0
        i0 = np.floor(q).astype(int)
        t = q - i0
        dims = np.array(self.arr.shape)
        dentro = np.all((i0 >= 0) & (i0 < dims - 1), axis=1)
        out = np.full(len(q), self.fundo, float)
        if not np.any(dentro):
            return out
        i0d, td = i0[dentro], t[dentro]
        a = self.arr
        acc = np.zeros(len(i0d))
        for dx in (0, 1):
            wx = td[:, 0] if dx else 1 - td[:, 0]
            for dy in (0, 1):
                wy = td[:, 1] if dy else 1 - td[:, 1]
                for dz in (0, 1):
                    wz = td[:, 2] if dz else 1 - td[:, 2]
                    acc += wx * wy * wz * a[i0d[:, 0] + dx, i0d[:, 1] + dy, i0d[:, 2] + dz]
        out[dentro] = acc
        return out

    def gradiente(self, pontos, h=None):
        h = h or self.voxel
        g = np.zeros((len(pontos), 3))
        for e in range(3):
            d = np.zeros(3)
            d[e] = h
            g[:, e] = (self.amostrar(pontos + d) - self.amostrar(pontos - d)) / (2 * h)
        return g


def campo_da_malha(verts, quads, tris, voxel, meia_faixa):
    """O campo de distância de uma malha fechada (pelo openvdb), num pedaço da grade que cobre a faixa."""
    xf = vdb.createLinearTransform(voxelSize=voxel)
    g = vdb.FloatGrid.createLevelSetFromPolygons(np.asarray(verts, np.float32),
                                                 triangles=np.asarray(tris, np.uint32) if len(tris) else None,
                                                 quads=np.asarray(quads, np.uint32) if len(quads) else None,
                                                 transform=xf, halfWidth=float(meia_faixa))
    (i0, j0, k0), (i1, j1, k1) = g.evalActiveVoxelBoundingBox()
    arr = np.empty((i1 - i0 + 1, j1 - j0 + 1, k1 - k0 + 1), np.float32)
    g.copyToArray(arr, ijk=(i0, j0, k0))
    return Campo(arr, (i0, j0, k0), voxel, float(g.background))


def uniao_suave(a, b, k):
    """A união suave (o mínimo polinomial): as emendas ficam arredondadas em ~k metros."""
    if k <= 0:
        return np.minimum(a, b)
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b * (1 - h) + a * h - k * h * (1 - h)


class Juntador:
    """Junta os campos das peças num campo só, numa caixa da grade."""

    def __init__(self, caixa_min, caixa_max, voxel, fundo):
        self.voxel = voxel
        self.fundo = fundo
        self.ijk0 = np.floor(np.asarray(caixa_min) / voxel).astype(int)
        ijk1 = np.ceil(np.asarray(caixa_max) / voxel).astype(int)
        self.arr = np.full(tuple(ijk1 - self.ijk0 + 1), fundo, np.float32)

    def juntar(self, campo, k, menos=False):
        o = campo.ijk0 - self.ijk0
        sh = np.array(campo.arr.shape)
        a0 = np.maximum(o, 0)
        a1 = np.minimum(o + sh, self.arr.shape)
        if np.any(a1 <= a0):
            return
        b0 = a0 - o
        b1 = a1 - o
        alvo = self.arr[a0[0]:a1[0], a0[1]:a1[1], a0[2]:a1[2]]
        peca = np.clip(campo.arr[b0[0]:b1[0], b0[1]:b1[1], b0[2]:b1[2]], -self.fundo, self.fundo)
        if menos:  # tirar (a diferença suave)
            alvo[...] = -uniao_suave(-alvo, peca, k)
        else:
            alvo[...] = uniao_suave(alvo, peca, k)

    def campo(self):
        return Campo(self.arr, self.ijk0, self.voxel, self.fundo)

    def malha(self, adaptividade=0.0):
        g = vdb.FloatGrid(self.fundo)
        g.transform = vdb.createLinearTransform(voxelSize=self.voxel)
        g.copyFromArray(self.arr, ijk=tuple(int(v) for v in self.ijk0), tolerance=0.0)
        try:
            g.gridClass = vdb.GridClass.LEVEL_SET
        except Exception:
            pass
        pontos, tris, quads = g.convertToPolygons(isovalue=0.0, adaptivity=float(adaptividade))
        return np.asarray(pontos, float), np.asarray(quads, int).reshape(-1, 4), np.asarray(tris, int).reshape(-1, 3)


# ---------------------------------------------------------------------------------------------------------------------
# Ruído (para a pelagem)
# ---------------------------------------------------------------------------------------------------------------------

def _hash3(ix, iy, iz, sal):
    h = (ix * 73856093) ^ (iy * 19349663) ^ (iz * 83492791) ^ (sal * 2654435761)
    h = (h ^ (h >> 13)) * 1274126177
    h = h ^ (h >> 16)
    return (h & 0xFFFFFF) / float(0xFFFFFF)


def ruido(p, freq=1.0, sal=0):
    """Ruído de valor 3D, suave, de 0 a 1."""
    q = np.asarray(p, float) * freq
    i = np.floor(q).astype(np.int64)
    t = q - i
    t = t * t * (3 - 2 * t)
    acc = np.zeros(len(q))
    for dx in (0, 1):
        wx = t[:, 0] if dx else 1 - t[:, 0]
        for dy in (0, 1):
            wy = t[:, 1] if dy else 1 - t[:, 1]
            for dz in (0, 1):
                wz = t[:, 2] if dz else 1 - t[:, 2]
                acc += wx * wy * wz * _hash3(i[:, 0] + dx, i[:, 1] + dy, i[:, 2] + dz, sal)
    return acc


def fbm(p, freq=1.0, oitavas=4, sal=0):
    acc, amp, tot = np.zeros(len(p)), 1.0, 0.0
    for o in range(oitavas):
        acc += amp * ruido(p, freq * (2 ** o), sal + o * 17)
        tot += amp
        amp *= 0.5
    return acc / tot


def suave(a, b, x):
    t = np.clip((np.asarray(x, float) - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)
