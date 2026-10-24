# A sniper (fuzil de ferrolho de caça com luneta), em blocos de cor sólida pela Paleta.png (linha 12, a das armas).
# Rode no Blender com a cena "Sniper" (os braços do rifle importados de Jogador_FP_Rifle.fbx).
# As medidas estão no espaço de repouso dos braços de primeira pessoa: o cano aponta para -Y, o alto é +Z e a direita da
# arma é -X. O cabo (a origem da arma avulsa) é a cabeça do osso "gun": (-0,12; -0,25; -0,235).
# A empunhadura e o guarda-mão ficam onde o rifle tem os dele: as mãos das animações do rifle seguram a sniper igual.
import bpy, bmesh, math
from mathutils import Vector, Matrix

CX = -0.12          # o meio da arma em X
EIXO_Z = -0.12      # a altura do cano
ORIGEM = Vector((-0.12, -0.25, -0.235))   # a cabeça do osso gun (o cabo)

# células da paleta (coluna, linha)
MADEIRA, MADEIRA_ESCURA = (8, 12), (7, 12)
METAL, METAL_MEDIO, METAL_CLARO, PRETO, QUASE_PRETO = (0, 12), (1, 12), (4, 12), (2, 12), (6, 12)
LENTE = (8, 11)


class Malha:
    """Junta peças num bmesh, cada face com o UV no meio de uma célula da paleta e o grupo de vértices do osso."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.dv = self.bm.verts.layers.deform.new()
        self.grupos = ["gun", "slide", "mag"]

    def _face(self, vs, cor, grupo):
        f = self.bm.faces.new(vs)
        u, v = (cor[0] + 0.5) / 16, (cor[1] + 0.5) / 16
        for l in f.loops:
            l[self.uv].uv = (u, v)
        f.smooth = False
        return f

    def _vert(self, p, grupo):
        v = self.bm.verts.new(p)
        v[self.dv][self.grupos.index(grupo)] = 1.0
        return v

    def prisma(self, perfil, x0, x1, cor, grupo="gun", cor_lado=None):
        """Um perfil no plano YZ (pontos (y, z) em volta), esticado em X de x0 a x1."""
        a = [self._vert((x0, y, z), grupo) for y, z in perfil]
        b = [self._vert((x1, y, z), grupo) for y, z in perfil]
        n = len(perfil)
        self._face(list(reversed(a)), cor_lado or cor, grupo)
        self._face(b, cor_lado or cor, grupo)
        for i in range(n):
            j = (i + 1) % n
            self._face([a[i], a[j], b[j], b[i]], cor, grupo)

    def caixa(self, c, t, cor, grupo="gun"):
        """Caixa pelo centro e tamanho."""
        x0, x1 = c[0] - t[0] / 2, c[0] + t[0] / 2
        y0, y1 = c[1] - t[1] / 2, c[1] + t[1] / 2
        z0, z1 = c[2] - t[2] / 2, c[2] + t[2] / 2
        self.prisma([(y0, z0), (y1, z0), (y1, z1), (y0, z1)], x0, x1, cor, grupo)

    def cilindro(self, centro, eixo, comprimento, r0, r1, cor, grupo="gun", lados=8, tampa=None, giro=math.pi / 8):
        """Um tronco de cone ao longo do eixo (de centro, por comprimento), raio r0 no começo e r1 no fim."""
        eixo = Vector(eixo).normalized()
        aux = Vector((0, 0, 1)) if abs(eixo.z) < 0.9 else Vector((1, 0, 0))
        u = eixo.cross(aux).normalized()
        w = eixo.cross(u).normalized()
        c0 = Vector(centro)
        c1 = c0 + eixo * comprimento
        anel = lambda c, r: [self._vert(c + (u * math.cos(giro + 2 * math.pi * k / lados) + w * math.sin(giro + 2 * math.pi * k / lados)) * r, grupo) for k in range(lados)]
        a, b = anel(c0, r0), anel(c1, r1)
        for k in range(lados):
            j = (k + 1) % lados
            self._face([a[k], b[k], b[j], a[j]], cor, grupo)
        self._face(list(reversed(a)) if True else a, tampa or cor, grupo)
        self._face(b, tampa or cor, grupo)

    def objeto(self, nome, colecao):
        me = bpy.data.meshes.new(nome)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(me)
        self.bm.free()
        o = bpy.data.objects.new(nome, me)
        colecao.objects.link(o)
        for g in self.grupos:
            o.vertex_groups.new(name=g)
        return o


def montar(m):
    xl, xr = CX + 0.024, CX - 0.024          # as faces da coronha (esquerda +X, direita -X)

    # --- coronha de madeira, em três peças (a de trás, o cabo e a do meio até a ponta do guarda-mão)
    m.prisma([(0.175, -0.100), (0.175, -0.312), (0.10, -0.285), (-0.05, -0.208), (-0.15, -0.200), (-0.15, -0.118),
              (-0.03, -0.098), (0.06, -0.094)], xr, xl, MADEIRA)
    m.prisma([(-0.150, -0.168), (-0.205, -0.305), (-0.268, -0.292), (-0.255, -0.168)], CX - 0.020, CX + 0.020, MADEIRA)
    m.prisma([(-0.13, -0.112), (-0.13, -0.200), (-0.25, -0.170), (-0.70, -0.168), (-0.79, -0.150), (-0.80, -0.128),
              (-0.80, -0.112)], CX - 0.026, CX + 0.026, MADEIRA)
    # a sola de borracha da coronha e a ponta escura do guarda-mão
    m.prisma([(0.193, -0.098), (0.193, -0.316), (0.175, -0.314), (0.175, -0.098)], xr - 0.002, xl + 0.002, QUASE_PRETO)
    m.prisma([(-0.80, -0.112), (-0.80, -0.150), (-0.83, -0.146), (-0.83, -0.114)], CX - 0.024, CX + 0.024, MADEIRA_ESCURA)

    # --- a caixa da culatra e o cano
    m.cilindro((CX, -0.135, EIXO_Z), (0, -1, 0), 0.33, 0.024, 0.024, METAL)
    m.cilindro((CX, -0.465, EIXO_Z), (0, -1, 0), 0.62, 0.0135, 0.0105, METAL_MEDIO, lados=8)
    # o freio de boca: um bloco com as janelas laterais
    m.cilindro((CX, -1.085, EIXO_Z), (0, -1, 0), 0.07, 0.017, 0.017, METAL, lados=8, tampa=QUASE_PRETO)
    for y in (-1.105, -1.135):
        m.caixa((CX, y, EIXO_Z), (0.038, 0.010, 0.012), QUASE_PRETO)

    # --- o ferrolho (osso "slide"): o corpo atrás da culatra e a alavanca para a direita, com a bola na ponta
    m.cilindro((CX, -0.100, EIXO_Z + 0.004), (0, -1, 0), 0.07, 0.015, 0.015, METAL_CLARO, grupo="slide")
    m.cilindro((CX - 0.010, -0.150, EIXO_Z + 0.006), (-0.85, 0.15, -0.5), 0.07, 0.006, 0.006, METAL_CLARO, grupo="slide", lados=6)
    m.caixa((CX - 0.072, -0.140, EIXO_Z - 0.030), (0.024, 0.024, 0.024), METAL_CLARO, grupo="slide")

    # --- o carregador (osso "mag"), embaixo, na frente do gatilho
    m.caixa((CX, -0.385, -0.186), (0.034, 0.075, 0.042), METAL, grupo="mag")
    m.caixa((CX, -0.385, -0.210), (0.038, 0.080, 0.008), QUASE_PRETO, grupo="mag")

    # --- guarda-mato e gatilho
    m.caixa((CX, -0.305, -0.212), (0.012, 0.070, 0.008), METAL)
    m.caixa((CX, -0.338, -0.192), (0.012, 0.008, 0.044), METAL)
    m.caixa((CX, -0.288, -0.186), (0.006, 0.008, 0.030), METAL_CLARO)

    # --- a luneta (preta), baixa, fina e mais para a frente (a ocular fica a ~9 cm do olho, para não tapar a tela), nos
    # dois anéis, com a lente azulada nas duas pontas
    ZL = -0.060
    for y in (-0.215, -0.385):
        m.caixa((CX, y, (ZL - 0.096) / 2), (0.022, 0.016, 0.038), PRETO)
        m.cilindro((CX, y + 0.010, ZL), (0, -1, 0), 0.020, 0.0165, 0.0165, QUASE_PRETO)
    m.cilindro((CX, -0.130, ZL), (0, -1, 0), 0.32, 0.0135, 0.0135, PRETO)
    m.cilindro((CX, -0.090, ZL), (0, -1, 0), 0.045, 0.018, 0.0145, PRETO, tampa=LENTE)
    m.cilindro((CX, -0.450, ZL), (0, -1, 0), 0.065, 0.0145, 0.023, PRETO)
    m.cilindro((CX, -0.515, ZL), (0, -1, 0), 0.03, 0.023, 0.023, PRETO, tampa=LENTE)
    # as torres de regulagem: em cima e do lado direito
    m.cilindro((CX, -0.300, ZL + 0.010), (0, 0, 1), 0.016, 0.009, 0.009, QUASE_PRETO)
    m.cilindro((CX - 0.010, -0.300, ZL), (-1, 0, 0), 0.016, 0.008, 0.008, QUASE_PRETO)
    # o anel de ajuste do aumento, perto da ocular
    m.cilindro((CX, -0.135, ZL), (0, -1, 0), 0.018, 0.0155, 0.0155, METAL_MEDIO)


def nova_sniper_fp():
    """O modelo de primeira pessoa: um esqueleto novo (cópia do do rifle), os braços e a sniper no lugar do rifle."""
    cena = bpy.data.scenes["Sniper"]
    col = cena.collection
    rig_r = bpy.data.objects["FP_Rig_Rifle"]
    bra_r = bpy.data.objects["FP_Bracos_Rifle"]
    for nome in ("FP_Rig_Sniper", "FP_Bracos_Sniper", "Boca_FP_Sniper", "Sniper_temp"):
        if nome in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[nome], do_unlink=True)
    rig = rig_r.copy(); rig.data = rig_r.data.copy(); rig.name = "FP_Rig_Sniper"; rig.data.name = "FP_Rig_Sniper"
    if rig.animation_data:
        rig.animation_data_clear()
    col.objects.link(rig)
    bra = bra_r.copy(); bra.data = bra_r.data.copy(); bra.name = "FP_Bracos_Sniper"; bra.data.name = "FP_Bracos_Sniper"
    col.objects.link(bra)
    bra.parent = rig
    for md in bra.modifiers:
        if md.type == "ARMATURE":
            md.object = rig
    # tira o rifle (os vértices da arma) e deixa os braços
    gi = {g.index: g.name for g in bra.vertex_groups}
    bm = bmesh.new(); bm.from_mesh(bra.data)
    dv = bm.verts.layers.deform.active
    tirar = [v for v in bm.verts if v[dv] and gi[max(v[dv].items(), key=lambda kv: kv[1])[0]] in ("gun", "slide", "mag")]
    bmesh.ops.delete(bm, geom=tirar, context="VERTS")
    bm.to_mesh(bra.data); bm.free()
    # a sniper, juntada aos braços (os grupos gun/slide/mag casam pelo nome)
    m = Malha(); montar(m)
    s = m.objeto("Sniper_temp", col)
    s.data.materials.append(bpy.data.materials["Pistola"])
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    s.select_set(True); bra.select_set(True)
    bpy.context.view_layer.objects.active = bra
    bpy.ops.object.join()
    # a boca do cano, presa ao osso gun
    boca = bpy.data.objects.new("Boca_FP_Sniper", None)
    col.objects.link(boca)
    boca.empty_display_size = 0.03
    boca.parent = rig; boca.parent_type = "BONE"; boca.parent_bone = "gun"
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    rot = Matrix.Rotation(math.pi, 4, "Z")   # como a do rifle: o eixo da boca olha para a frente da arma
    boca.matrix_world = Matrix.Translation((CX, -1.155, EIXO_Z)) @ rot
    rig.data.pose_position = "POSE"
    return rig, bra, boca


def nova_sniper_avulsa():
    """A arma avulsa (no corpo e no suporte): a mesma malha, na convenção das Armas.fbx (origem no cabo, o cano em +Z
    local, o alto em +Y local), num objeto girado 90° em X como os de lá."""
    cena = bpy.data.scenes["ArmasRef"]
    for nome in ("Sniper_corpo",):
        if nome in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[nome], do_unlink=True)
    m = Malha(); montar(m)
    o = m.objeto("Sniper_corpo", cena.collection)
    o.data.materials.append(bpy.data.materials["Pistola"])
    # do espaço dos braços para o da arma avulsa: x' = x + 0,12; y' = z + 0,235; z' = -(y + 0,25)
    M = Matrix(((1, 0, 0, -ORIGEM.x), (0, 0, 1, -ORIGEM.z), (0, -1, 0, ORIGEM.y), (0, 0, 0, 1)))
    o.data.transform(M)
    if M.determinant() < 0:
        o.data.flip_normals()
    o.vertex_groups.clear()
    o.rotation_euler = (math.pi / 2, 0, 0)
    o.location = (0, 0, -1.6)
    return o
