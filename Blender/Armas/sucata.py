# Três armas de fogo de sucata, de exemplo (09/11/2026, a pedido do Pedro: "somente os modelos no Blender"): uma
# pistola, um rifle de precisão sem luneta e uma submetralhadora, feitas em casa com cano de ferro, chapa, tábua, fita,
# abraçadeira e vergalhão. Só os modelos: sem animação e sem exportar para o jogo.
#
# O estilo é o das armas de hoje (Blender/sniper_modelo.py): peças em caixas e cilindros de poucos lados, cada face com
# o UV no meio de uma célula da paleta (filtro Point). As cores que a sucata pede e a paleta não tinha (ferrugem, fita
# prata, pintura verde, madeira crua, corda, borracha, cobre) ficam numa cópia, a Paleta_Sucata.png, aqui na pasta, em
# células livres das linhas 12 e 11; a Paleta.png do projeto não muda.
#
# O espaço da arma (o mesmo do Armas.fbx aberto no Blender): o cano para -Y, o alto em +Z, a direita da arma em -X e a
# origem no cabo (onde a mão direita segura). Para levar à Unity depois: o exportar() do armas.py (a malha girada -90°
# em X e o objeto +90° em X) dá o cano em +Z e o alto em +Y. Os grupos de vértices (corpo, ferrolho, carregador)
# separam as peças que mexem, se um dia forem animadas.
#
# No jogo (09/11/2026, a etapa 1b): cada função aceita jogo=True, a versão que o Blender/Armas/sucata_exportar.py
# exporta, com o mínimo mudado para caber nas animações das armas de hoje (a cara é a mesma): o rifle com o ferrolho de
# porta e a culatra 21 cm à frente (a bola onde a mão direita pega no tiro da sniper) e a coronha 10 cm mais curta; a
# submetralhadora com o poço e o carregador 5 cm à frente (a mão esquerda da recarga); a pistola com um bloquinho de um
# tiro (o carregador, que entra por baixo do cabo). Sem jogo=True sai o modelo aprovado, como antes.
#
# Como rodar (um Blender em segundo plano):
#   blender -b --factory-startup --python Blender/Armas/sucata.py
# Grava a Paleta_Sucata.png e o Armas_Sucata.blend aqui na pasta: as três armas na coleção Armas_Sucata e as de hoje
# (do Armas.fbx e do Armas_Sniper.fbx) na coleção Armas_de_hoje, ao lado, para comparar.

import math
import os
from contextlib import contextmanager

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(AQUI))
PALETA = os.path.join(REPO, "Assets", "_Game", "Art", "Paleta.png")
PALETA_SUCATA = os.path.join(AQUI, "Paleta_Sucata.png")
BLEND = os.path.join(AQUI, "Armas_Sucata.blend")
JOGADOR = os.path.join(REPO, "Assets", "_Game", "Art", "Characters", "Jogador")

# células da paleta (coluna, linha; a linha 0 é a de baixo, como no sniper_modelo.py): as que já existem
MADEIRA, MADEIRA_ESCURA = (8, 12), (7, 12)
METAL, METAL_MEDIO, METAL_CLARO, PRETO, QUASE_PRETO = (0, 12), (1, 12), (4, 12), (2, 12), (6, 12)
VERMELHO, LATAO = (10, 12), (11, 12)
# as novas, só na Paleta_Sucata.png: a célula, a cor (sRGB) e o alfa (o brilho, como o das vizinhas)
NOVAS = {
    "FERRUGEM": ((12, 12), "9a4f27", 0x14),
    "FERRUGEM_ESCURA": ((13, 12), "63301f", 0x10),
    "FITA_PRATA": ((14, 12), "a3a6a4", 0x40),
    "PINTURA_VERDE": ((15, 12), "56653f", 0x26),
    "MADEIRA_CRUA": ((12, 11), "a77d4f", 0x14),
    "CORDA": ((13, 11), "b49f73", 0x0a),
    "BORRACHA": ((14, 11), "302d2a", 0x1a),
    "COBRE": ((15, 11), "b0683c", 0x4c),
}
FERRUGEM, FERRUGEM_ESCURA, FITA_PRATA, PINTURA_VERDE, MADEIRA_CRUA, CORDA, BORRACHA, COBRE = (
    NOVAS[k][0] for k in ("FERRUGEM", "FERRUGEM_ESCURA", "FITA_PRATA", "PINTURA_VERDE", "MADEIRA_CRUA", "CORDA",
                          "BORRACHA", "COBRE"))

DECALQUE = 0.0005   # o quanto um decalque fica fora da face


def _base(eixo):
    """Dois vetores perpendiculares ao eixo (u e w)."""
    e = Vector(eixo).normalized()
    aux = Vector((0, 0, 1)) if abs(e.z) < 0.9 else Vector((1, 0, 0))
    u = e.cross(aux).normalized()
    return e, u, e.cross(u).normalized()


class Malha:
    """Junta as peças de uma arma num bmesh, cada face com o UV no meio de uma célula da paleta."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.dv = self.bm.verts.layers.deform.new()
        self.grupos = ["corpo", "ferrolho", "carregador"]
        self.grupo = "corpo"
        self.pilha = [Matrix.Identity(4)]
        self.abertas = set()   # as faces que não fecham volume (decalques, cintas): o normal já sai certo

    # ------------------------------------------------------------------ o básico
    @contextmanager
    def giro(self, centro, graus):
        """Gira as peças de dentro em volta do centro (graus em X, Y e Z): o desalinhado da sucata."""
        c = Matrix.Translation(Vector(centro))
        r = Euler([math.radians(g) for g in graus]).to_matrix().to_4x4()
        self.pilha.append(self.pilha[-1] @ c @ r @ c.inverted())
        try:
            yield
        finally:
            self.pilha.pop()

    @contextmanager
    def peca(self, grupo):
        """As peças de dentro vão para outro grupo de vértices (ferrolho, carregador)."""
        antes, self.grupo = self.grupo, grupo
        try:
            yield
        finally:
            self.grupo = antes

    def _vert(self, p):
        v = self.bm.verts.new(self.pilha[-1] @ Vector(p))
        v[self.dv][self.grupos.index(self.grupo)] = 1.0
        return v

    def _face(self, vs, cor):
        f = self.bm.faces.new(vs)
        u, v = (cor[0] + 0.5) / 16, (cor[1] + 0.5) / 16
        for lo in f.loops:
            lo[self.uv].uv = (u, v)
        f.smooth = False
        return f

    def _virar(self, f, n_mundo):
        """Deixa o normal da face aberta do lado de n_mundo."""
        f.normal_update()
        if f.normal.dot(n_mundo) < 0:
            f.normal_flip()
        self.abertas.add(f)

    def _local(self, n):
        return (self.pilha[-1].to_3x3() @ Vector(n)).normalized()

    # ------------------------------------------------------------------ as peças
    def prisma(self, perfil, x0, x1, cor, cor_lado=None):
        """Um perfil no plano YZ (pontos (y, z) em volta), esticado em X de x0 a x1."""
        a = [self._vert((x0, y, z)) for y, z in perfil]
        b = [self._vert((x1, y, z)) for y, z in perfil]
        self._face(list(reversed(a)), cor_lado or cor)
        self._face(b, cor_lado or cor)
        for i in range(len(perfil)):
            j = (i + 1) % len(perfil)
            self._face([a[i], a[j], b[j], b[i]], cor)

    def caixa(self, c, t, cor):
        """Caixa pelo centro e tamanho."""
        y0, y1 = c[1] - t[1] / 2, c[1] + t[1] / 2
        z0, z1 = c[2] - t[2] / 2, c[2] + t[2] / 2
        self.prisma([(y0, z0), (y1, z0), (y1, z1), (y0, z1)], c[0] - t[0] / 2, c[0] + t[0] / 2, cor)

    def cilindro(self, centro, eixo, comprimento, r0, r1, cor, lados=8, tampa=None, tampas=True, oval=1.0, giro=None):
        """Um tronco de cone ao longo do eixo (de centro, por comprimento), raio r0 no começo e r1 no fim. tampas: True,
        False (um anel), "inicio" ou "fim". oval achata o corte (em w). Com giro None, uma face fica de frente para u."""
        e, u, w = _base(eixo)
        g = math.pi / lados if giro is None else giro
        c0 = Vector(centro)
        c1 = c0 + e * comprimento

        def anel(c, r):
            return [self._vert(c + (u * math.cos(g + 2 * math.pi * k / lados) +
                                    w * oval * math.sin(g + 2 * math.pi * k / lados)) * r) for k in range(lados)]

        a, b = anel(c0, r0), anel(c1, r1)
        lado = [self._face([a[k], b[k], b[(k + 1) % lados], a[(k + 1) % lados]], cor) for k in range(lados)]
        ini = self._face(list(reversed(a)), tampa or cor) if tampas in (True, "inicio") else None
        fim = self._face(b, tampa or cor) if tampas in (True, "fim") else None
        if tampas is not True:
            M = self.pilha[-1]
            cw, ew = M @ c0, self._local(e)
            for f in lado:
                d = f.calc_center_median() - cw
                self._virar(f, d - ew * d.dot(ew))
            if ini:
                self._virar(ini, -ew)
            if fim:
                self._virar(fim, ew)

    def haste(self, pontos, r, cor, lados=4, fechada=False):
        """Um arame, vergalhão, prego ou corda: um tubo de poucos lados pelo caminho de pontos (a quina das dobras
        esticada para o tubo não afinar)."""
        P = [Vector(p) for p in pontos]
        n = len(P)
        aneis, u_ant = [], None
        for i in range(n):
            a = (P[i] - P[i - 1]).normalized() if (i > 0 or fechada) else None
            b = (P[(i + 1) % n] - P[i]).normalized() if (i < n - 1 or fechada) else None
            a, b = a or b, b or a
            t = (a + b).normalized()
            if u_ant is None:
                _, u, _ = _base(t)
            else:
                u = (u_ant - t * u_ant.dot(t)).normalized()
            w = t.cross(u)
            u_ant = u
            m = b - a
            m = m.normalized() if m.length > 1e-6 else None
            esticar = 1 / max(a.dot(t), 0.35) - 1
            anel = []
            for k in range(lados):
                ang = math.pi / lados + 2 * math.pi * k / lados
                q = (u * math.cos(ang) + w * math.sin(ang)) * r
                if m is not None:
                    q = q + m * q.dot(m) * esticar
                anel.append(self._vert(P[i] + q))
            aneis.append(anel)
        for i in range(n if fechada else n - 1):
            A, B = aneis[i], aneis[(i + 1) % n]
            for k in range(lados):
                j = (k + 1) % lados
                self._face([A[k], B[k], B[j], A[j]], cor)
        if not fechada:
            self._face(list(reversed(aneis[0])), cor)
            self._face(aneis[-1], cor)

    def mola(self, inicio, fim, r, fio, voltas, cor, passos=4):
        """Uma mola em espiral (passos pontos por volta), do início ao fim."""
        a, b = Vector(inicio), Vector(fim)
        _, u, w = _base(b - a)
        n = int(voltas * passos)
        pts = [a + (b - a) * (i / n) + (u * math.cos(2 * math.pi * i / passos) + w * math.sin(2 * math.pi * i / passos)) * r
               for i in range(n + 1)]
        self.haste(pts, fio, cor, lados=3)

    @staticmethod
    def _plano(normal, giro):
        n = Vector(normal).normalized()
        aux = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((0, 1, 0))
        s = aux.cross(n).normalized()
        rot = Matrix.Rotation(math.radians(giro), 3, n)
        return n, rot @ s, rot @ n.cross(s)

    def placa(self, centro, normal, tam, cor, giro=0.0):
        """Um decalque (um retângulo só, colado na face): furo, janela, borda gasta. tam: na +X e na -X é (Y, Z); na +Z
        e na -Z é (X, Y); na +Y e na -Y é (X, Z)."""
        n, s, t = self._plano(normal, giro)
        c = Vector(centro) + n * DECALQUE
        a, b = tam[0] / 2, tam[1] / 2
        f = self._face([self._vert(c + s * x + t * y) for x, y in ((-a, -b), (a, -b), (a, b), (-a, b))], cor)
        self._virar(f, self._local(n))

    def mancha(self, centro, normal, tam, cor, giro=0.0, lados=6):
        """Uma mancha de contorno torto (ferrugem, tinta saindo): um decalque de poucos lados com os raios desiguais."""
        n, s, t = self._plano(normal, giro)
        c = Vector(centro) + n * DECALQUE
        semente = centro[0] * 131 + centro[1] * 977 + centro[2] * 313
        vs = []
        for k in range(lados):
            ang = 2 * math.pi * k / lados
            f = 0.70 + 0.30 * abs(math.sin(semente + k * 2.3))
            vs.append(self._vert(c + (s * math.cos(ang) * tam[0] + t * math.sin(ang) * tam[1]) * f / 2))
        f = self._face(vs, cor)
        self._virar(f, self._local(n))

    def furo(self, centro, eixo, r, cor=PRETO, lados=8):
        """A boca do cano: um disco escuro colado na tampa."""
        e, u, w = _base(eixo)
        c = Vector(centro) + e * DECALQUE
        g = math.pi / lados
        f = self._face([self._vert(c + (u * math.cos(g + 2 * math.pi * k / lados) + w * math.sin(g + 2 * math.pi * k / lados)) * r)
                        for k in range(lados)], cor)
        self._virar(f, self._local(e))

    def cinta(self, y, largura, contorno, cor):
        """Uma volta de fita (ou a cinta da abraçadeira) em volta do corte, de y para a frente (-Y). contorno: pontos (x, z)
        em volta, por fora das peças."""
        a = [self._vert((x, y, z)) for x, z in contorno]
        b = [self._vert((x, y - largura, z)) for x, z in contorno]
        n = len(contorno)
        cx, cz = sum(p[0] for p in contorno) / n, sum(p[1] for p in contorno) / n
        for i in range(n):
            j = (i + 1) % n
            f = self._face([a[i], a[j], b[j], b[i]], cor)
            meio = ((contorno[i][0] + contorno[j][0]) / 2 - cx, 0, (contorno[i][1] + contorno[j][1]) / 2 - cz)
            self._virar(f, self._local(meio))

    def abracadeira(self, y, contorno, onde, largura=0.009):
        """Abraçadeira de mangueira: a cinta de aço, a caixinha do sem-fim (em onde = (x, z), no contorno) e o parafuso."""
        self.cinta(y + largura / 2, largura, contorno, METAL_CLARO)
        x, z = onde
        s = 1 if x >= 0 else -1
        self.caixa((x + s * 0.0035, y, z), (0.007, 0.012, 0.011), METAL_CLARO)
        self.cilindro((x + s * 0.0035, y, z + 0.0055), (0, 0, 1), 0.004, 0.0032, 0.0032, METAL_MEDIO, lados=6, tampas="fim")

    def parafuso(self, centro, normal, r=0.0036, cor=METAL_CLARO, lados=6):
        """A cabeça de um parafuso (ou rebite) saindo da face."""
        self.cilindro(centro, normal, 0.0022, r, r * 0.8, cor, lados=lados, tampas="fim")

    def objeto(self, nome, colecao, material):
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in self.bm.faces if f not in self.abertas])
        bmesh.ops.triangulate(self.bm, faces=[f for f in self.bm.faces if len(f.verts) > 4],
                              quad_method="BEAUTY", ngon_method="EAR_CLIP")
        me = bpy.data.meshes.new(nome)
        self.bm.to_mesh(me)
        self.bm.free()
        me.materials.append(material)
        o = bpy.data.objects.new(nome, me)
        colecao.objects.link(o)
        for g in self.grupos:
            o.vertex_groups.new(name=g)
        return o


# ---------------------------------------------------------------------------------------------------- a pistola
def pistola(m, jogo=False):
    """Um tiro só: um cano de ferro numa chapa dobrada em U, o cão de parafuso puxado por uma mola por cima, o gatilho
    de prego num guarda-mato de arame e o cabo de tábua com fita isolante. No jogo, o bloquinho de um tiro no cabo."""
    Z = 0.088   # a altura do cano sobre o cabo

    # --- o cano: um pedaço de cano de ferro; na boca, a contraporca e a luva; atrás, o tampão com o percussor
    m.cilindro((0, 0.004, Z), (0, -1, 0), 0.160, 0.0115, 0.0115, METAL)
    m.cilindro((0, -0.146, Z), (0, -1, 0), 0.008, 0.0150, 0.0150, FERRUGEM_ESCURA, lados=6, giro=0.3)
    m.cilindro((0, -0.156, Z), (0, -1, 0), 0.030, 0.0152, 0.0152, METAL_MEDIO)
    m.furo((0, -0.186, Z), (0, -1, 0), 0.0085)
    m.cilindro((0, 0.026, Z), (0, -1, 0), 0.024, 0.0146, 0.0146, METAL_MEDIO)
    m.cilindro((0, 0.032, Z), (0, -1, 0), 0.008, 0.0028, 0.0028, METAL_CLARO, lados=6, tampas="inicio")
    ap = 0.0115 * math.cos(math.pi / 8)
    m.mancha((ap, -0.114, Z), (1, 0, 0), (0.026, 0.007), FERRUGEM, giro=4)
    m.mancha((0, -0.100, Z + ap), (0, 0, 1), (0.007, 0.024), FERRUGEM)
    m.mancha((-ap, -0.126, Z), (-1, 0, 0), (0.020, 0.007), FERRUGEM)

    # --- a armação: uma chapa dobrada em U por baixo do cano (um lado mais torto que o outro), enferrujada, com o cano
    # soldado em cima por um cordão grosso
    lado = [(0.046, 0.040), (0.046, 0.082), (-0.018, 0.0855), (-0.068, 0.081), (-0.078, 0.062), (-0.076, 0.040)]
    m.prisma(lado, 0.0135, 0.0175, FERRUGEM_ESCURA)
    with m.giro((0, -0.016, 0.060), (1.5, 0, 0)):
        m.prisma([(y + 0.003, z - 0.002) for y, z in lado], -0.0175, -0.0135, FERRUGEM_ESCURA)
    m.caixa((0, -0.015, 0.0405), (0.035, 0.122, 0.005), FERRUGEM_ESCURA)
    for s in (1, -1):
        m.haste([(s * 0.0128, -0.072, 0.0835), (s * 0.0133, -0.030, 0.0860), (s * 0.0128, 0.012, 0.0835)], 0.0034,
                METAL_CLARO)
    m.mancha((0.0175, 0.016, 0.060), (1, 0, 0), (0.026, 0.018), FERRUGEM, giro=12)
    m.mancha((0.0175, -0.052, 0.066), (1, 0, 0), (0.016, 0.012), FERRUGEM, giro=-20)
    m.placa((0.0175, -0.025, 0.0810), (1, 0, 0), (0.028, 0.0025), METAL_CLARO)
    m.mancha((-0.0175, -0.012, 0.060), (-1, 0, 0), (0.032, 0.014), FERRUGEM, giro=-8)

    # --- a barra chata por baixo do cano e as duas abraçadeiras de mangueira que prendem os dois
    m.caixa((0, -0.112, 0.0725), (0.012, 0.080, 0.004), FERRUGEM)
    contorno = [(0.0140, 0.0680), (0.0140, 0.0885), (0.0090, 0.1010), (-0.0090, 0.1010), (-0.0140, 0.0885),
                (-0.0140, 0.0680)]
    m.abracadeira(-0.094, contorno, (0.0140, 0.080))
    with m.giro((0, -0.138, Z), (0, 8, 0)):
        m.abracadeira(-0.138, contorno, (0.0140, 0.083))

    # --- o cão: um parafuso grande que gira num pino atrás do cano (armado, para trás), puxado por uma mola por cima
    piv = Vector((0, 0.036, 0.058))
    d = Vector((0, 0.40, 0.92)).normalized()
    m.cilindro((0.0195, 0.036, 0.058), (-1, 0, 0), 0.039, 0.0035, 0.0035, METAL_CLARO, lados=6)
    m.cilindro(piv, d, 0.044, 0.0045, 0.0045, METAL_MEDIO, lados=6)
    m.cilindro(piv + d * 0.044, d, 0.008, 0.0092, 0.0092, METAL_CLARO, lados=6)
    m.caixa((0, -0.036, 0.1040), (0.004, 0.004, 0.012), METAL)                        # o gancho da mola
    m.mola((0, 0.050, 0.1085), (0, -0.034, 0.1085), 0.0042, 0.0012, 4, METAL_CLARO)

    # --- o gatilho: um prego dobrado (a cabeça aparece do lado esquerdo), num guarda-mato de arame grosso
    m.haste([(0.0195, -0.044, 0.052), (0, -0.044, 0.052), (0, -0.046, 0.028), (0, -0.039, 0.013)], 0.0021, METAL_CLARO)
    m.cilindro((0.0175, -0.044, 0.052), (1, 0, 0), 0.0025, 0.0045, 0.0045, METAL_CLARO, lados=6, tampas="fim")
    m.haste([(0, -0.074, 0.041), (0, -0.076, 0.012), (0, -0.066, -0.002), (0, -0.030, -0.004), (0, -0.014, 0.004)],
            0.0024, METAL_CLARO)

    # --- o cabo: um toco de tábua inclinado, preso por duas chapinhas parafusadas, com três voltas de fita isolante
    cy, cz = 0.012, -0.016
    with m.giro((0, cy, cz), (18, 0, 0)):
        m.prisma([(cy - 0.020, cz + 0.064), (cy + 0.021, cz + 0.064), (cy + 0.023, cz - 0.056), (cy + 0.006, cz - 0.066),
                  (cy - 0.019, cz - 0.061)], -0.013, 0.013, MADEIRA)
        for dz, gx, gy, h in ((0.030, 3, -2, 0.016), (0.010, -4, 3, 0.018), (-0.012, 2, 0, 0.015)):
            with m.giro((0, cy, cz + dz), (gx, gy, 0)):
                m.caixa((0, cy + 0.001, cz + dz), (0.0295, 0.0455, h), PRETO)
    for s in (1, -1):
        m.caixa((s * 0.0145, -0.004, 0.035), (0.003, 0.030, 0.022), METAL)
        m.parafuso((s * 0.016, -0.004, 0.030), (s, 0, 0))

    # --- no jogo: o bloquinho de um tiro (o carregador, no osso mag), que entra por baixo do cabo: a tábua com a fita
    # prata, o cartucho de prego em cima (escondidos no cabo) e a chapinha de fundo, rente ao cabo
    if jogo:
        with m.peca("carregador"), m.giro((0, cy, cz), (18, 0, 0)):
            m.caixa((0, cy + 0.003, cz - 0.037), (0.018, 0.022, 0.054), MADEIRA_CRUA)
            m.caixa((0, cy + 0.003, cz - 0.045), (0.0186, 0.0226, 0.012), FITA_PRATA)
            m.cilindro((0, cy + 0.003, cz - 0.010), (0, 0, 1), 0.018, 0.0055, 0.0055, COBRE, lados=6)
            m.cilindro((0, cy + 0.003, cz + 0.008), (0, 0, 1), 0.002, 0.0065, 0.0065, METAL_CLARO, lados=6)
            m.caixa((0, cy + 0.004, cz - 0.065), (0.024, 0.028, 0.006), FERRUGEM_ESCURA)


# ---------------------------------------------------------------------------------------------------- o rifle de precisão
def rifle(m, jogo=False):
    """De ferrolho, um tiro por vez: o cano de ferro com o quebra-chamas de porcas e arruelas, o ferrolho de porta no
    punho, as miras de chapa dobrada, as tábuas parafusadas, a bandoleira de corda e o bipé de vergalhão recolhido."""
    Z = 0.105   # a altura do cano
    # no jogo: a ação (a culatra, a redução, o começo do cano e o ferrolho de porta) mais à frente, com a bola do
    # ferrolho onde a mão direita pega no tiro da sniper, e a soleira mais perto (a coronha 10 cm mais curta)
    A = -0.2075 if jogo else 0.0
    K = -0.100 if jogo else 0.0

    # --- a coronha: a tábua velha do corpo (do punho à soleira) e a tábua nova do guarda-mão, emendadas com duas chapas
    m.prisma([(-0.135, 0.092), (-0.135, 0.046), (-0.060, 0.043), (-0.026, 0.040), (0.006, -0.090), (0.050, -0.094),
              (0.030, -0.006), (0.090, -0.018), (0.438 + K, -0.108), (0.438 + K, 0.070), (0.300 + K, 0.078), (0.130, 0.088),
              (0.040, 0.090)], -0.016, 0.016, MADEIRA)
    m.prisma([(-0.135, 0.093), (-0.500, 0.093), (-0.507, 0.080), (-0.500, 0.058), (-0.440, 0.050), (-0.135, 0.047)],
             -0.016, 0.016, MADEIRA_CRUA)
    for s in (1, -1):
        with m.giro((0, -0.135, 0.069), (2 * s, 0, 0)):
            m.caixa((s * 0.0175, -0.135, 0.069), (0.003, 0.080, 0.030), METAL)
        for y in (-0.160, -0.110):
            m.parafuso((s * 0.019, y, 0.069), (s, 0, 0))
    # o remendo da coronha: uma tábua de pinus na face da bochecha, e a soleira de um pedaço de pneu
    m.prisma([(0.170 + K, 0.030), (0.380 + K, 0.024), (0.385 + K, 0.071), (0.175 + K, 0.078)], 0.016, 0.025,
             MADEIRA_CRUA)
    for y, z in ((0.205 + K, 0.054), (0.350 + K, 0.048)):
        m.parafuso((0.025, y, z), (1, 0, 0))
    with m.giro((0, 0.446 + K, -0.019), (-2, 0, 0)):
        m.caixa((0, 0.446 + K, -0.019), (0.036, 0.016, 0.184), BORRACHA)
    # fita isolante no punho
    for zc, h, g in ((-0.018, 0.014, 15), (-0.046, 0.012, 10), (-0.070, 0.012, 16)):
        yc = 0.011 + (-0.018 - zc) * 0.23
        with m.giro((0, yc, zc), (g, 0, 0)):
            m.caixa((0, yc, zc), (0.0355, 0.048, h), PRETO)

    # --- a culatra (um cano mais grosso), a redução e o cano comprido
    m.cilindro((0, 0.038 + A, Z), (0, -1, 0), 0.188, 0.020, 0.020, METAL)
    m.cilindro((0, -0.150 + A, Z), (0, -1, 0), 0.024, 0.020, 0.0125, METAL_MEDIO)
    m.cilindro((0, -0.170 + A, Z), (0, -1, 0), 0.690 + A, 0.0105, 0.0105, METAL)
    apc = 0.020 * math.cos(math.pi / 8)
    m.placa((-apc, -0.012 + A, Z), (-1, 0, 0), (0.044, 0.012), PRETO)                 # a janela de carregar
    m.mancha((apc, -0.095 + A, Z), (1, 0, 0), (0.036, 0.012), FERRUGEM, giro=6)
    m.placa((0, -0.050 + A, Z + apc), (0, 0, 1), (0.004, 0.070), METAL_CLARO)
    ap = 0.0105 * math.cos(math.pi / 8)
    for y, s, comp in (((-0.565, 1, 0.040) if jogo else (-0.300, 1, 0.050)), (-0.520, -1, 0.040), (-0.690, 1, 0.030)):
        m.mancha((s * ap, y, Z), (s, 0, 0), (comp, 0.0075), FERRUGEM)
    m.mancha((0, -0.600, Z + ap), (0, 0, 1), (0.0075, 0.060), FERRUGEM)

    # --- o quebra-chamas: arruelas e porcas enfiadas na ponta do cano e soldadas, cada uma de um jeito
    y = -0.856
    for comp, r, cor, lados, giro, tort in ((0.004, 0.0185, METAL_CLARO, 8, None, 0), (0.012, 0.0160, METAL_MEDIO, 6, 0.2, 0),
                                            (0.004, 0.0195, METAL_CLARO, 8, None, 5), (0.012, 0.0160, METAL_MEDIO, 6, 0.7, 0),
                                            (0.004, 0.0175, METAL_CLARO, 8, None, -3)):
        with m.giro((0, y - comp / 2, Z), (tort, 0, 0)):
            m.cilindro((0, y, Z), (0, -1, 0), comp, r, r, cor, lados=lados, giro=giro)
        y -= comp
    m.furo((0, y, Z), (0, -1, 0), 0.0075)

    # --- as miras de ferro: uma aba de chapa dobrada numa braçadeira na boca, e o entalhe de chapa atrás, na culatra
    m.cilindro((0, -0.795, Z), (0, -1, 0), 0.012, 0.0125, 0.0125, METAL_MEDIO, tampas=False)
    with m.giro((0, -0.801, Z + 0.012), (0, 4, 0)):
        m.caixa((0, -0.801, Z + 0.0125), (0.010, 0.012, 0.002), METAL_CLARO)
        m.caixa((0, -0.801, Z + 0.0225), (0.0028, 0.010, 0.020), METAL_CLARO)
    m.caixa((0, -0.128 + A, 0.1250), (0.022, 0.014, 0.003), METAL_MEDIO)
    m.caixa((0, -0.128 + A, 0.1290), (0.018, 0.0028, 0.005), METAL_CLARO)
    for x in (0.0062, -0.0062):
        m.caixa((x, -0.128 + A, 0.1350), (0.0056, 0.0028, 0.012), METAL_CLARO)

    # --- o ferrolho: um ferrolho de porta parafusado no punho, atrás da culatra, com a alavanca dobrada para a direita
    with m.peca("ferrolho"):
        m.caixa((0, 0.082 + A, 0.0900), (0.026, 0.090, 0.004), LATAO)
        for y in (0.050, 0.114):
            m.caixa((0, y + A, Z), (0.020, 0.012, 0.026), LATAO)
        for y in (0.064, 0.100):
            m.parafuso((0.0085, y + A, 0.092), (0, 0, 1), r=0.0025, lados=4)
        m.cilindro((0, 0.136 + A, Z), (0, -1, 0), 0.110, 0.0062, 0.0062, METAL_CLARO)
        if jogo:   # a alavanca reta para a direita: a bola na altura do cano, onde fica a da Mosin
            m.haste([(-0.004, 0.082 + A, Z), (-0.034, 0.082 + A, Z), (-0.042, 0.088 + A, Z + 0.006)], 0.0045,
                    METAL_CLARO, lados=6)
            m.cilindro((-0.042, 0.088 + A, Z + 0.006), (-0.80, 0.30, 0.30), 0.018, 0.0082, 0.0066, LATAO, lados=6)
        else:
            m.haste([(-0.004, 0.082, Z), (-0.036, 0.082, Z), (-0.046, 0.090, Z - 0.030)], 0.0045, METAL_CLARO,
                    lados=6)
            m.cilindro((-0.046, 0.090, Z - 0.030), (-0.30, 0.25, -0.92), 0.020, 0.0082, 0.0066, LATAO, lados=6)

    # --- o gatilho de prego (a cabeça do lado esquerdo da tábua) e o guarda-mato de arame
    m.haste([(0.0165, -0.050, 0.060), (0, -0.050, 0.060), (0, -0.052, 0.030), (0, -0.045, 0.016)], 0.0021, METAL_CLARO)
    m.cilindro((0.0160, -0.050, 0.060), (1, 0, 0), 0.0018, 0.0045, 0.0045, METAL_CLARO, lados=6, tampas="fim")
    m.haste([(0, -0.090, 0.044), (0, -0.092, 0.012), (0, -0.080, 0.000), (0, -0.036, -0.002), (0, -0.017, 0.006)],
            0.0024, METAL_CLARO)

    # --- o cano preso ao guarda-mão: duas abraçadeiras e uma volta larga de fita prata
    def contorno(y, folga=0.0):
        fundo = 0.050 + (y + 0.440) * (0.047 - 0.050) / 0.305 - 0.0015
        return [(0.0175 + folga, fundo - folga), (0.0175 + folga, 0.097), (0.0090, 0.1180 + folga),
                (-0.0090, 0.1180 + folga), (-0.0175 - folga, 0.097), (-0.0175 - folga, fundo - folga)]
    ya, yb, yf = (-0.455, -0.398, -0.428) if jogo else (-0.430, -0.225, -0.335)   # no jogo, à frente da culatra
    m.abracadeira(ya, contorno(ya), (0.0175, 0.075))
    with m.giro((0, yb, 0.080), (0, -6, 0)):
        m.abracadeira(yb, contorno(yb), (0.0175, 0.072))
    with m.giro((0, yf, 0.080), (3, 0, 0)):
        m.cinta(yf + 0.015, 0.032, contorno(yf, 0.0003), FITA_PRATA)

    # --- a bandoleira: uma corda entre dois parafusos com olhal (um no guarda-mão, outro na coronha), caída para a esquerda
    olhais = [(-0.300, 0.0485), (0.360 + K, -0.018 - (0.270 + K) * 0.090 / (0.348 + K))]   # o de trás, na quina de baixo
    for y, z in olhais:
        r = 0.0062
        m.haste([(0, y, z), (0, y - r, z - r), (0, y, z - 2 * r), (0, y + r, z - r)], 0.0016, METAL_CLARO, lados=3, fechada=True)
        m.caixa((0, y, z - 2 * r - 0.003), (0.010, 0.009, 0.007), CORDA)
    (y0, z0), (y1, z1) = olhais
    z0, z1 = z0 - 0.0155, z1 - 0.0155
    pts = []
    for i in range(9):
        t = i / 8
        pts.append((0.026 * math.sin(math.pi * t), y0 + (y1 - y0) * t, z0 + (z1 - z0) * t - 0.055 * 4 * t * (1 - t)))
    m.haste(pts, 0.0048, CORDA)

    # --- o bipé: duas pernas de vergalhão dobrado, recolhidas para trás debaixo do guarda-mão, numa dobradiça
    m.caixa((0, -0.482, 0.0475), (0.034, 0.020, 0.015), METAL_MEDIO)
    m.cilindro((0.021, -0.482, 0.044), (-1, 0, 0), 0.042, 0.0035, 0.0035, METAL_CLARO, lados=6)
    for s in (1, -1):
        m.haste([(s * 0.013, -0.482, 0.041), (s * 0.017, -0.300, 0.0375), (s * 0.019, -0.262, 0.0365),
                 (s * 0.029, -0.247, 0.027)], 0.0034, FERRUGEM)


# ---------------------------------------------------------------------------------------------------- a submetralhadora
def submetralhadora(m, jogo=False):
    """Um tubo quadrado de chapa pintado de verde (descascando), com rebites e solda, a camisa de cano furado, o
    carregador reto com fita, o cabo de ferramenta, a empunhadura de cabo de vassoura e a coronha de vergalhão."""
    Z = 0.100   # a altura do cano

    # --- o corpo: o tubo quadrado, as tampas soldadas, a tinta saindo e as bordas gastas
    m.caixa((0, -0.055, Z), (0.042, 0.260, 0.056), PINTURA_VERDE)
    m.caixa((0, 0.078, Z), (0.047, 0.006, 0.061), METAL)
    m.caixa((0, -0.188, Z), (0.047, 0.006, 0.061), METAL)
    m.cilindro((-0.023, -0.1843, 0.1290), (1, 0, 0), 0.046, 0.0028, 0.0034, METAL_CLARO, lados=5)
    m.cilindro((-0.023, 0.0743, 0.1290), (1, 0, 0), 0.046, 0.0034, 0.0026, METAL_CLARO, lados=5)
    X = 0.021
    for c, n, t, cor, g in (((X, -0.040, 0.105), 1, (0.045, 0.020), FERRUGEM, 8),
                            ((X, -0.130, 0.112), 1, (0.034, 0.016), METAL, -10),
                            ((X, -0.160, 0.088), 1, (0.026, 0.016), FERRUGEM, 15),
                            ((X, -0.060, 0.1263), 1, (0.150, 0.0025), METAL_CLARO, 0),
                            ((-X, -0.100, 0.086), -1, (0.055, 0.020), FERRUGEM, -6),
                            ((-X, 0.045, 0.084), -1, (0.028, 0.016), METAL, 12),
                            ((-X, -0.120, 0.1263), -1, (0.110, 0.0025), METAL_CLARO, 0)):
        (m.placa if cor == METAL_CLARO else m.mancha)(c, (n, 0, 0), t, cor, giro=g)
    m.mancha((0.005, -0.080, 0.128), (0, 0, 1), (0.022, 0.055), FERRUGEM, giro=6)
    m.placa((0.0185, 0.000, 0.128), (0, 0, 1), (0.0025, 0.090), METAL_CLARO)
    for s in (1, -1):
        for y in (0.055, -0.010, -0.075, -0.150):
            m.parafuso((s * X, y, 0.078), (s, 0, 0), r=0.0028, lados=4)
    # as janelas da direita: a do ferrolho e a da alavanca de armar
    m.placa((-X, -0.040, 0.108), (-1, 0, 0), (0.040, 0.016), PRETO)
    m.placa((-X, 0.0365, 0.119), (-1, 0, 0), (0.063, 0.006), PRETO)

    with m.peca("ferrolho"):
        # a alavanca de armar: um parafuso sextavado saindo da fenda da direita
        m.cilindro((-X, 0.058, 0.119), (-1, 0, 0), 0.020, 0.0034, 0.0034, METAL_MEDIO, lados=6, tampas="fim")
        m.cilindro((-0.041, 0.058, 0.119), (-1, 0, 0), 0.0065, 0.0072, 0.0072, METAL_CLARO, lados=6)
        # a mola aparente, numa janela recortada do lado esquerdo
        m.placa((X, 0.035, 0.101), (1, 0, 0), (0.064, 0.018), PRETO)
        m.haste([(0.0222, 0.064 - i * 0.0072, 0.101 + (0.0058 if i % 2 else -0.0058)) for i in range(9)], 0.0011,
                METAL_CLARO, lados=3)

    # --- as miras: duas abas de chapa
    m.caixa((0, 0.060, 0.1330), (0.016, 0.0025, 0.010), METAL_CLARO)
    m.caixa((0, -0.282, Z + 0.0215), (0.0025, 0.008, 0.012), METAL_CLARO)

    # --- a camisa de cano furado, a porca da frente e a ponta do cano
    m.cilindro((0, -0.191, Z), (0, -1, 0), 0.099, 0.0175, 0.0175, METAL)
    ap = 0.0175 * math.cos(math.pi / 8)
    for i in range(4):
        y = -0.203 - i * 0.024
        m.placa((ap, y, Z), (1, 0, 0), (0.009, 0.007), PRETO)
        m.placa((-ap, y, Z), (-1, 0, 0), (0.009, 0.007), PRETO)
        if i < 3:
            m.placa((0, y - 0.012, Z + ap), (0, 0, 1), (0.007, 0.009), PRETO)
    m.cilindro((0, -0.290, Z), (0, -1, 0), 0.008, 0.0125, 0.0125, METAL_MEDIO, lados=6, giro=0.25)
    m.cilindro((0, -0.290, Z), (0, -1, 0), 0.016, 0.0078, 0.0078, METAL_MEDIO)
    m.furo((0, -0.306, Z), (0, -1, 0), 0.0045)

    # --- a empunhadura da frente: um toco de cabo de vassoura pintado (a tinta saindo), preso por uma braçadeira
    m.cilindro((0, -0.239, Z), (0, -1, 0), 0.012, 0.0192, 0.0192, METAL_CLARO, tampas=False)
    m.caixa((0, -0.245, 0.0810), (0.012, 0.012, 0.008), METAL_CLARO)
    with m.giro((0, -0.245, 0.080), (-6, 0, 0)):
        m.cilindro((0, -0.245, 0.080), (0, 0, -1), 0.088, 0.0125, 0.0125, VERMELHO, tampa=MADEIRA_CRUA)
        m.cilindro((0, -0.245, 0.074), (0, 0, -1), 0.007, 0.0133, 0.0133, COBRE, tampas=False)
        apv = 0.0125 * math.cos(math.pi / 8)
        m.mancha((apv, -0.245, 0.030), (1, 0, 0), (0.0085, 0.024), MADEIRA_CRUA, giro=6)
        m.mancha((0, -0.245 - apv, 0.012), (0, -1, 0), (0.0085, 0.016), MADEIRA_CRUA)

    # --- o carregador: um tubo de chapa reto, com fita prata e fita isolante, e o fundo enferrujado; o poço soldado
    ym = -0.162 if jogo else -0.112   # no jogo, 5 cm à frente: onde a mão esquerda pega na recarga
    m.caixa((0, ym, 0.062), (0.028, 0.042, 0.020), METAL)
    m.cilindro((0.014, ym - 0.021, 0.0720), (0, 1, 0), 0.042, 0.0028, 0.0024, METAL_CLARO, lados=5)
    with m.peca("carregador"), m.giro((0, ym, 0.070), (-4, 0, 0)):
        m.caixa((0, ym, -0.024), (0.023, 0.034, 0.188), METAL_MEDIO)
        m.caixa((0, ym, -0.1215), (0.027, 0.040, 0.007), FERRUGEM)
        with m.giro((0, ym, -0.050), (3, 0, 2)):
            m.caixa((0, ym, -0.050), (0.0255, 0.0365, 0.034), FITA_PRATA)
        m.caixa((0, ym, 0.022), (0.0250, 0.0360, 0.012), PRETO)

    # --- o cabo: um cabo de lima, de madeira, com a virola de metal em cima, soldado numa chapinha
    a, b = Vector((0, -0.012, 0.066)), Vector((0, 0.016, -0.076))
    d = (b - a).normalized()
    m.caixa((0, -0.010, 0.069), (0.022, 0.030, 0.008), METAL)
    m.cilindro(a, d, 0.016, 0.0148, 0.0148, METAL_CLARO, tampas=False)
    m.cilindro(a + d * 0.016, d, 0.118, 0.0138, 0.0165, MADEIRA, tampas=False)
    m.cilindro(a + d * 0.134, d, 0.012, 0.0165, 0.0105, MADEIRA, tampas="fim")

    # --- o gatilho de tira de chapa dobrada e o guarda-mato de vergalhão
    m.haste([(0, -0.044, 0.070), (0, -0.046, 0.046), (0, -0.040, 0.032)], 0.0022, METAL_CLARO)
    m.haste([(0, -0.092, 0.058), (0, -0.090, 0.030), (0, -0.080, 0.020), (0, -0.026, 0.019), (0, -0.016, 0.034)],
            0.0026, FERRUGEM)

    # --- a coronha: uma moldura de vergalhão dobrado soldada atrás, a soleira de pneu amarrada com fio de cobre
    m.haste([(0, 0.076, 0.121), (0.002, 0.262, 0.110), (0.001, 0.262, 0.004), (0, 0.076, 0.079)], 0.0042, FERRUGEM)
    m.haste([(0, 0.170, 0.1154), (0, 0.170, 0.041)], 0.0038, FERRUGEM)
    m.caixa((0, 0.268, 0.057), (0.026, 0.012, 0.118), BORRACHA)
    for z in (0.088, 0.024):
        m.caixa((0, 0.2655, z), (0.029, 0.020, 0.006), COBRE)


# ---------------------------------------------------------------------------------------------------- a cena
def paleta_sucata():
    """A Paleta_Sucata.png: a Paleta.png do projeto com as células novas (só a cópia muda)."""
    base = bpy.data.images.load(PALETA, check_existing=False)
    px = list(base.pixels)
    w, h = base.size
    bpy.data.images.remove(base)
    for (col, lin), cor, alfa in NOVAS.values():
        i = (lin * w + col) * 4
        px[i:i + 4] = [int(cor[0:2], 16) / 255, int(cor[2:4], 16) / 255, int(cor[4:6], 16) / 255, alfa / 255]
    nova = bpy.data.images.new("Paleta_Sucata_nova", w, h, alpha=True)
    nova.pixels = px
    nova.filepath_raw = PALETA_SUCATA
    nova.file_format = "PNG"
    nova.save()
    bpy.data.images.remove(nova)
    img = bpy.data.images.load(PALETA_SUCATA, check_existing=False)
    img.name = "Paleta_Sucata.png"
    img.alpha_mode = "NONE"   # o alfa é o brilho, não transparência
    return img


def material(img):
    m = bpy.data.materials.new("Paleta_Sucata")
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    tex.location = (-360, 240)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.7
    bsdf.inputs["Metallic"].default_value = 0.0
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.3
    m.use_backface_culling = True   # como na Unity: uma face invertida some
    return m


def colecao(nome):
    c = bpy.data.collections.new(nome)
    bpy.context.scene.collection.children.link(c)
    return c


def armas_de_hoje(mat, col):
    """A pistola, a submetralhadora e a sniper de hoje (lidas dos FBX), atrás das de sucata, com o mesmo material."""
    bpy.context.view_layer.active_layer_collection = bpy.context.view_layer.layer_collection.children[col.name]
    for f in ("Armas.fbx", "Armas_Sniper.fbx"):
        bpy.ops.import_scene.fbx(filepath=os.path.join(JOGADOR, f))
    lugar = {"Pistola_": 0.0, "Submetralhadora_": -0.45, "Sniper_": -0.90}
    for o in list(col.objects):
        pre = next((p for p in lugar if o.name.startswith(p)), None)
        if pre is None or o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
            continue
        o.location = (0, 1.6, lugar[pre])
        o.data.materials.clear()
        o.data.materials.append(mat)
    bpy.data.orphans_purge(do_recursive=True)


def medir(objs):
    vs = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    tri = sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)
    mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return mx - mn, tri


def invertidas(o):
    """Quantas peças fechadas da malha estão do avesso (o volume com sinal negativo)."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    vistas, ruins = set(), 0
    for f0 in bm.faces:
        if f0.index in vistas:
            continue
        ilha, pilha = [], [f0]
        vistas.add(f0.index)
        while pilha:
            f = pilha.pop()
            ilha.append(f)
            for e in f.edges:
                for g in e.link_faces:
                    if g.index not in vistas:
                        vistas.add(g.index)
                        pilha.append(g)
        if all(len(e.link_faces) == 2 for f in ilha for e in f.edges):
            vol = sum(f.calc_center_median().dot(f.normal) * f.calc_area() for f in ilha)
            ruins += vol < 0
    bm.free()
    return ruins


def relatorio(sucata, hoje):
    print("=" * 30, "as medidas (m) e os triângulos")
    for o, pre in zip(sucata, ("Pistola_", "Submetralhadora_", "Sniper_")):
        (x, y, z), t = medir([o])
        (hx, hy, hz), ht = medir([h for h in hoje if h.name.startswith(pre)])
        print(f"  {o.name:24s} comp {y:.3f} alt {z:.3f} larg {x:.3f} tri {t:5d} | {pre[:-1]:15s} comp {hy:.3f} "
              f"alt {hz:.3f} larg {hx:.3f} tri {ht:5d} | x{t / ht:.2f} | do avesso: {invertidas(o)}")


def construir():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    bpy.data.orphans_purge(do_recursive=True)
    img = paleta_sucata()
    mat = material(img)
    col = colecao("Armas_Sucata")
    sucata = []
    for nome, func, z in (("Sucata_Pistola", pistola, 0.0), ("Sucata_Submetralhadora", submetralhadora, -0.45),
                          ("Sucata_Rifle_Precisao", rifle, -0.90)):
        m = Malha()
        func(m)
        o = m.objeto(nome, col, mat)
        o.location = (0, 0, z)
        sucata.append(o)
    hoje_col = colecao("Armas_de_hoje")
    armas_de_hoje(mat, hoje_col)
    bpy.context.view_layer.update()
    relatorio(sucata, list(hoje_col.objects))
    # a vista do Blender: sólido com a cor da textura
    for tela in bpy.data.screens:
        for area in tela.areas:
            if area.type == "VIEW_3D":
                for esp in area.spaces:
                    if esp.type == "VIEW_3D":
                        esp.shading.type = "SOLID"
                        esp.shading.color_type = "TEXTURE"
    # grava: o caminho da textura relativo ao .blend e a textura empacotada dentro dele
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    img.filepath = "//" + os.path.basename(PALETA_SUCATA)
    img.pack()
    bpy.ops.wm.save_mainfile()
    print("  gravado:", BLEND, "e", PALETA_SUCATA)


if __name__ == "__main__":
    construir()
